using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Scripts.Common;
using Spine;
using UnityEngine;

/// <summary>
/// Level 083 独立 Box Bad 控制器；拥有状态动画、冲撞移动、首次碰撞门禁和主角击飞表现。
/// </summary>
public sealed class Level83RivalCtrl : MonoBehaviour
{
    #region Inspector

    /// <summary>复用共享移动能力的本地 Box Bad 专用控制器，不得引用 Level46EnemyCtrl。</summary>
    [SerializeField] private Level83RivalActorCtrl actorCtrl;

    /// <summary>冲撞期间用于检测主角的本地碰撞体。</summary>
    [SerializeField] private Collider2D chargeCollider;

    /// <summary>冲撞命中时播放的本地碰撞爆点。</summary>
    [SerializeField] private ParticleSystem knockEffect;

    // 撞击音：音符砸中与冲撞碰撞共用 l9 trap（攻守同音为刻意统一，见 CONTEXT「音符砸击」）。
    private const string HitSfxTag = "trap";

    /// <summary>Level 083 独立冲撞速度，初始值只复制 Level 046 当前参数。</summary>
    [SerializeField] private float moveSpeed = 11f;

    /// <summary>直接碰撞后到击飞生效前的等待时长。</summary>
    [SerializeField] private float hitDelay = 0.1f;

    /// <summary>主角向左上方击飞的本地方向。</summary>
    [SerializeField] private Vector2 knockbackDirection = new Vector2(-0.5f, 0.5f);

    /// <summary>主角击飞力度。</summary>
    [SerializeField] private float knockbackForce = 50f;

    /// <summary>主角每次完整翻滚的时长。</summary>
    [SerializeField] private float rotationDuration = 0.5f;

    /// <summary>die 最后阶段（两眼转圈圈）在动画时间轴内的起点秒数；die 完成后从该时刻起子区间循环。</summary>
    [SerializeField] private float dieDizzyLoopStart = 1.0667f;

    #endregion

    #region Runtime State

    private bool isCharging;
    private bool hasHitActor;
    private TrackEntry stateEntry;
    private Quaternion chargeFacingRotation;

    /// <summary>当前 Box Bad 是否已经开始冲撞移动。</summary>
    public bool IsCharging => isCharging;

    #endregion

    #region Unity Messages

    private void Awake()
    {
        if (actorCtrl != null)
            chargeFacingRotation = actorCtrl.transform.localRotation;
    }

    private void Start()
    {
        if (actorCtrl?.RB != null)
            actorCtrl.RB.simulated = true;

        PlayLoop("idle");
    }

    private void FixedUpdate()
    {
        if (isCharging && !hasHitActor)
            actorCtrl?.OnUpdate();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isCharging || hasHitActor || other == null || !other.CompareTag("Player"))
            return;

        ActorCtrl targetActor = other.GetComponent<ActorCtrl>();
        if (targetActor == null)
            targetActor = other.GetComponentInParent<ActorCtrl>();

        if (targetActor == null)
            return;

        Vector3 hitPosition = transform.position;
        hasHitActor = true;
        isCharging = false;
        // 命中后停止继续判定，但保留刚体已有冲撞速度，让 Box Bad 像 Level 046 一样继续穿过舞台，
        // 避免在与主角/舞台相交的命中点立即清零速度而形成“卡在舞台里”的视觉。
        PlayActorKnockback(targetActor, hitPosition).Forget();
    }

    private void OnDestroy()
    {
        DetachStateEntry();
        transform.DOKill();
        actorCtrl?.transform.DOKill();
    }

    #endregion

    #region Public API

    /// <summary>切换为每轮演奏完成评价期间持续播放的待机状态。</summary>
    public void PlayIdle()
    {
        if (!isCharging && !hasHitActor)
            PlayLoop("idle");
    }

    /// <summary>切换为玩家正式演奏期间持续播放的期待状态。</summary>
    public void PlayWant()
    {
        if (!isCharging && !hasHitActor)
            PlayLoop("want");
    }

    /// <summary>
    /// 完整播放 angry，并只在动画完成后进入 Level 083 独立冲撞链。
    /// </summary>
    public void PlayAngryThenCharge()
    {
        if (isCharging || hasHitActor)
            return;

        // 决策 D3：门禁在 angry 入口开启，保持到重开/销毁（约束 C2）。
        actorCtrl?.BeginChargeAnimationControl();
        DetachStateEntry();
        stateEntry = actorCtrl?.SpineCtrl?.PlayAnimation("angry", false, false);
        if (stateEntry == null)
        {
            BeginCharge();
            return;
        }

        stateEntry.Complete += OnAngryCompleted;
    }

    /// <summary>
    /// 播放胜利收口中的 die；动画完成后调用回调，冲撞期间不得调用。
    /// </summary>
    public void PlayDie(System.Action onCompleted)
    {
        if (isCharging || hasHitActor)
            return;

        DetachStateEntry();
        stateEntry = actorCtrl?.SpineCtrl?.PlayAnimation("die", false, false);
        if (stateEntry == null)
        {
            onCompleted?.Invoke();
            return;
        }

        stateEntry.Complete += HandleComplete;

        void HandleComplete(TrackEntry entry)
        {
            if (stateEntry != entry)
                return;

            entry.Complete -= HandleComplete;
            stateEntry = null;
            PlayDieDizzyLoop();
            onCompleted?.Invoke();
        }
    }

    /// <summary>返回 Box Bad 的命中碰撞体，供实体音符判定头顶释放与首次命中。</summary>
    public Collider2D GetHitCollider()
    {
        return chargeCollider;
    }

    /// <summary>
    /// 在实体音符首次有效命中的接触位置播放一次本关撞击粒子，并同帧播放共享撞击音 trap。
    /// </summary>
    public void PlayNoteHitEffect(Vector3 hitPosition)
    {
        if (knockEffect != null)
        {
            knockEffect.transform.position = hitPosition;
            knockEffect.Play();
        }

        AudioManager.PlaySound(HitSfxTag);
    }

    #endregion

    #region Charge Flow

    private void OnAngryCompleted(TrackEntry entry)
    {
        if (stateEntry != entry)
            return;

        entry.Complete -= OnAngryCompleted;
        stateEntry = null;
        BeginCharge();
    }

    private void BeginCharge()
    {
        if (actorCtrl == null || actorCtrl.RB == null || chargeCollider == null)
            return;

        hasHitActor = false;
        isCharging = true;
        actorCtrl.RB.simulated = true;
        actorCtrl.RB.gravityScale = 0f;
        actorCtrl.RB.linearVelocity = Vector2.zero;
        actorCtrl.ResumeMoving();
        actorCtrl.SetMoveSpeed(moveSpeed);
        FaceChargeDirection();
        actorCtrl.SetMoveInput(-1f);
        actorCtrl.SwitchDash(true);
        actorCtrl.PlayChargeAnimation();
    }

    private async UniTaskVoid PlayActorKnockback(ActorCtrl targetActor, Vector3 hitPosition)
    {
        targetActor.StopMoving();
        await UniTask.WaitForSeconds(hitDelay, cancellationToken: this.GetCancellationTokenOnDestroy());

        if (knockEffect != null)
        {
            // Rival 命中后会继续保持冲撞余速，爆点固定在首次接触位置，避免跟随 Rival 偏移。
            knockEffect.transform.position = hitPosition;
            knockEffect.Play();
        }

        AudioManager.PlaySound(HitSfxTag);
        targetActor.Knockback(knockbackDirection, knockbackForce).Forget();
        targetActor.transform
            .DOLocalRotate(new Vector3(0f, 0f, 360f), rotationDuration, DG.Tweening.RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1)
            .SetLink(targetActor.gameObject);
    }

    #endregion

    #region Helpers

    private void FaceChargeDirection()
    {
        if (actorCtrl == null)
            return;

        if (actorCtrl.FaingRight)
            actorCtrl.SetFaceDir(false);

        actorCtrl.transform.localRotation = chargeFacingRotation;
    }

    /// <summary>
    /// die 完成后循环其最后阶段（两眼转圈圈）。die 的 1.0667s–5.0667s 为美术排好的 4 组同姿循环段，
    /// 结尾姿态与起点一致（仅 eye_control 死键无可见驱动），子区间循环无可见跳变。
    /// </summary>
    private void PlayDieDizzyLoop()
    {
        TrackEntry loopEntry = actorCtrl?.SpineCtrl?.PlayAnimation("die", true, false);
        if (loopEntry == null)
            return;

        loopEntry.AnimationStart = dieDizzyLoopStart;
        // spine 建议：改 AnimationStart 时同步 AnimationLast，避免起点前的一次性触发被重放。
        loopEntry.AnimationLast = dieDizzyLoopStart;
    }

    private void PlayLoop(string animationName)
    {
        DetachStateEntry();
        actorCtrl?.SpineCtrl?.PlayAnimation(animationName, true, false);
    }

    private void DetachStateEntry()
    {
        if (stateEntry != null)
            stateEntry.Complete -= OnAngryCompleted;

        stateEntry = null;
    }

    #endregion
}
