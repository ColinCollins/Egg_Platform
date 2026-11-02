using System.Collections;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// DLevel_017 的弹簧机关控制器。
/// 玩家走入独立触发区后，先压缩弹簧按钮，再用可调速度将玩家发射到空中。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/DLevel 017 Ctrl")]
public sealed class DLevel17Ctrl : MonoBehaviour, IDrawStrokeCompletedHandler
{
    #region Inspector

    /// <summary>
    /// 本关通用状态控制器。
    /// 调用边界：必须指向同一 DLevel_017 Prefab 根节点上的 DrawLevelCtrl。
    /// </summary>
    [Header("Runtime")]
    [Tooltip("DLevel_017 根节点上的 DrawLevelCtrl。")]
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;

    /// <summary>
    /// 被弹簧发射的玩家角色。
    /// 调用边界：只接受本关 Player；触发器内其它 Rigidbody2D 不会被发射。
    /// </summary>
    [Tooltip("本关玩家 Actor。")]
    [SerializeField] private ActorCtrl actor;

    /// <summary>
    /// 仅用于播放压缩与回弹表现的弹簧按钮。
    /// 注意事项：按钮不挂 Collider2D，角色可以直接穿过；Rigidbody2D 与 Joint 只负责参考
    /// Level032 的弹性形变表现。
    /// </summary>
    [Tooltip("无实体碰撞的弹簧按钮 Transform。")]
    [SerializeField] private Transform springButton;

    /// <summary>
    /// 弹簧按钮的视觉刚体，不参与任何实体碰撞。
    /// </summary>
    [Tooltip("按钮的视觉 Rigidbody2D；按钮自身仍不挂 Collider2D。")]
    [SerializeField] private Rigidbody2D springButtonBody;

    /// <summary>
    /// 复用 Level032 参数的视觉弹簧关节。
    /// </summary>
    [Tooltip("控制按钮快速拉伸回弹的 SpringJoint2D。")]
    [SerializeField] private SpringJoint2D springJoint;

    /// <summary>
    /// 弹簧释放时直接施加给玩家刚体的右上冲量。
    /// 水平移动始终保持向右；本机关不停止或锁定 DLevel001Ctrl 的自动行走。
    /// </summary>
    [Header("Launch")]
    [Tooltip("通过 AddForce(Impulse) 施加的真实向上冲量；当前 Actor 质量 3 时，72 对应约 24 的向上速度变化。")]
    [SerializeField, Min(0f)] private float upwardImpulse = 72f;

    /// <summary>
    /// 与向上冲量同帧施加的真实向右冲量。
    /// </summary>
    [Tooltip("通过 AddForce(Impulse) 施加的真实向右冲量。")]
    [SerializeField, Min(0f)] private float rightwardImpulse = 12f;

    /// <summary>
    /// 保留右向物理动量的短暂时长，防止自动行走在下一帧立即覆盖 X 冲量。
    /// </summary>
    [Tooltip("右向冲量的保留时长；期间角色仍始终向右，不会被锁定。")]
    [SerializeField, Min(0f)] private float rightwardMomentumRetentionDuration = 0.35f;

    /// <summary>
    /// 物理弹射冲量的整体力度倍率。
    /// 1 使用基准力度，0 不产生弹射，2 表示右上冲量同时加倍。
    /// </summary>
    [Tooltip("物理弹射力度倍率；1 为基准力度，同时缩放向右和向上冲量。")]
    [SerializeField, Min(0f)] private float launchStrength = 1f;

    /// <summary>
    /// 飞行阶段使用的重力倍率。
    /// 注意事项：延时结束或 Reset 时会恢复角色进入本关时的重力倍率。
    /// </summary>
    [Tooltip("飞行阶段的 Rigidbody2D.gravityScale。")]
    [SerializeField, Min(0f)] private float flightGravityScale = 4.5f;

    /// <summary>
    /// 发射后恢复角色原重力的延时。
    /// </summary>
    [Tooltip("发射后多少秒恢复角色原本的重力倍率。")]
    [SerializeField, Min(0f)] private float gravityRestoreDelay = 1.1f;

    /// <summary>
    /// 弹簧按钮预压缩的距离。
    /// </summary>
    [Header("Presentation")]
    [Tooltip("发射前按钮向下压缩的局部距离。")]
    [SerializeField, Min(0f)] private float compressionDistance = 0.2f;

    /// <summary>
    /// 弹簧按钮完成预压缩所需时间。
    /// </summary>
    [Tooltip("按钮压缩动画时长。")]
    [SerializeField, Min(0f)] private float compressionDuration = 0.1f;

    /// <summary>
    /// 启用视觉 SpringJoint2D 后按钮使用的重力倍率。
    /// </summary>
    [Tooltip("释放视觉弹簧时按钮 Rigidbody2D 的重力倍率；Level032 为 1。")]
    [SerializeField, Min(0f)] private float springReleaseGravityScale = 1f;

    /// <summary>
    /// 从开始压缩到真正发射的总延时。
    /// </summary>
    [Tooltip("从触发到发射的总延时，应不小于压缩时长。")]
    [SerializeField, Min(0f)] private float launchDelay = 0.2f;

    /// <summary>
    /// 同一轮弹射的最短冷却时间。
    /// 注意事项：用于过滤玩家多碰撞体在同一帧产生的重复进入事件。
    /// </summary>
    [Tooltip("两次有效弹射之间的最短间隔。")]
    [SerializeField, Min(0f)] private float retriggerCooldown = 1.3f;

    #endregion

    #region Runtime State

    private Rigidbody2D _actorBody;
    private Coroutine _springRoutine;
    private Vector3 _springButtonInitialLocalPosition;
    private Quaternion _springButtonInitialLocalRotation;
    private float _actorInitialGravityScale;
    private float _springButtonInitialGravityScale;
    private bool _springJointInitiallyEnabled;
    private bool _actorInitialStateCaptured;
    private bool _springVisualInitialStateCaptured;
    private float _nextTriggerTime;
    private ActorCtrl _momentumRetentionActor;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        ResolveReferences();
        CaptureInitialState();
    }

    private void OnDisable()
    {
        StopSpringRoutine();
        RestoreRuntimeState();
    }

    #endregion

    #region Trigger Entry

    /// <summary>
    /// 由 Spring 根节点的 OnTrigger2DHandle 调用。
    /// 调用边界：仅处理绑定 Actor 自身或其子碰撞体，重复进入会被冷却过滤。
    /// </summary>
    public void TriggerSpring(Collider2D other)
    {
        if (!Application.isPlaying || other == null)
            return;

        ResolveReferences();
        if (actor == null || _actorBody == null || actor.IsDied)
            return;

        ActorCtrl enteringActor = other.GetComponent<ActorCtrl>();
        if (enteringActor == null)
            enteringActor = other.GetComponentInParent<ActorCtrl>();

        if (enteringActor != actor ||
            (drawLevelCtrl != null &&
             (drawLevelCtrl.IsPaused || drawLevelCtrl.IsOutcomeLocked)) ||
            _springRoutine != null ||
            Time.time < _nextTriggerTime)
        {
            return;
        }

        CaptureInitialState();
        _nextTriggerTime = Time.time + retriggerCooldown;
        _springRoutine = StartCoroutine(PlaySpringRoutine());
    }

    #endregion

    #region IDrawStrokeCompletedHandler

    /// <inheritdoc />
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        // 本机关不依赖画线完成时机；实现接口仅用于接收统一的关卡 Reset。
    }

    /// <inheritdoc />
    public void OnDrawAttemptReset()
    {
        StopSpringRoutine();
        RestoreRuntimeState();
        _nextTriggerTime = 0f;
    }

    #endregion

    #region Internal

    private IEnumerator PlaySpringRoutine()
    {
        AudioManager.PlaySound("boardTrigger");
        PrepareSpringVisualForCompression();

        float actualCompressionDuration = 0f;
        if (springButton != null)
        {
            Vector3 compressionStart = springButton.localPosition;
            Vector3 compressionEnd = compressionStart + Vector3.down * compressionDistance;
            actualCompressionDuration = compressionDuration;

            if (compressionDuration <= 0f)
            {
                SetSpringButtonLocalPosition(compressionEnd);
            }
            else
            {
                float elapsed = 0f;
                while (elapsed < compressionDuration && springButton != null)
                {
                    if (drawLevelCtrl != null && drawLevelCtrl.IsPaused)
                    {
                        yield return null;
                        continue;
                    }

                    elapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(elapsed / compressionDuration);
                    float easedProgress = 1f - (1f - progress) * (1f - progress);
                    SetSpringButtonLocalPosition(Vector3.LerpUnclamped(
                        compressionStart,
                        compressionEnd,
                        easedProgress));
                    yield return null;
                }

                if (springButton != null)
                    SetSpringButtonLocalPosition(compressionEnd);
            }
        }

        float remainingDelay = Mathf.Max(0f, launchDelay - actualCompressionDuration);
        float remainingDelayElapsed = 0f;
        while (remainingDelayElapsed < remainingDelay)
        {
            yield return null;
            if (drawLevelCtrl != null && drawLevelCtrl.IsPaused)
                continue;

            remainingDelayElapsed += Time.deltaTime;
        }

        while (drawLevelCtrl != null && drawLevelCtrl.IsPaused)
            yield return null;

        if (actor == null ||
            _actorBody == null ||
            actor.IsDied ||
            (drawLevelCtrl != null && drawLevelCtrl.IsOutcomeLocked))
        {
            RestoreSpringVisualState();
            _springRoutine = null;
            yield break;
        }

        ReleaseSpringVisual();
        AudioManager.PlaySound("spring");

        if (_actorBody != null)
        {
            _actorBody.gravityScale = flightGravityScale;
            BeginActorHorizontalMomentumRetention();
            _actorBody.AddForce(
                new Vector2(rightwardImpulse, upwardImpulse) * launchStrength,
                ForceMode2D.Impulse);
            _actorBody.WakeUp();
        }

        float preLaunchElapsed = actualCompressionDuration + remainingDelay;
        float remainingCooldown = Mathf.Max(0f, retriggerCooldown - preLaunchElapsed);
        float postLaunchWait = Mathf.Max(
            Mathf.Max(remainingCooldown, gravityRestoreDelay),
            rightwardMomentumRetentionDuration);
        float postLaunchElapsed = 0f;
        bool gravityRestored = false;
        bool horizontalMomentumReleased = _momentumRetentionActor == null;

        if (gravityRestoreDelay <= 0f)
        {
            RestoreActorGravity();
            gravityRestored = true;
        }

        while (postLaunchElapsed < postLaunchWait)
        {
            yield return null;
            if (drawLevelCtrl != null && drawLevelCtrl.IsPaused)
                continue;

            postLaunchElapsed += Time.deltaTime;

            if (!gravityRestored && postLaunchElapsed >= gravityRestoreDelay)
            {
                RestoreActorGravity();
                gravityRestored = true;
            }

            if (!horizontalMomentumReleased &&
                postLaunchElapsed >= rightwardMomentumRetentionDuration)
            {
                ReleaseActorHorizontalMomentumRetention();
                horizontalMomentumReleased = true;
            }

        }

        RestoreActorGravity();
        ReleaseActorHorizontalMomentumRetention();

        _springRoutine = null;
    }

    private void ResolveReferences()
    {
        if (drawLevelCtrl == null)
            drawLevelCtrl = GetComponentInParent<DrawLevelCtrl>(true);

        if (actor == null)
            actor = GetComponentInChildren<ActorCtrl>(true);

        if (springButton == null)
            springButton = transform.Find("Spring/Sp_button_1_02");

        if (springButtonBody == null && springButton != null)
            springButtonBody = springButton.GetComponent<Rigidbody2D>();

        if (springJoint == null && springButton != null)
            springJoint = springButton.GetComponent<SpringJoint2D>();

        _actorBody = actor != null ? actor.RB : null;
    }

    private void CaptureInitialState()
    {
        if (!_springVisualInitialStateCaptured && springButton != null)
        {
            _springButtonInitialLocalPosition = springButton.localPosition;
            _springButtonInitialLocalRotation = springButton.localRotation;
            _springButtonInitialGravityScale = springButtonBody != null
                ? springButtonBody.gravityScale
                : 0f;
            _springJointInitiallyEnabled = springJoint != null && springJoint.enabled;
            _springVisualInitialStateCaptured = true;
        }

        if (!_actorInitialStateCaptured && _actorBody != null)
        {
            _actorInitialGravityScale = _actorBody.gravityScale;
            _actorInitialStateCaptured = true;
        }
    }

    private void StopSpringRoutine()
    {
        if (_springRoutine == null)
            return;

        StopCoroutine(_springRoutine);
        _springRoutine = null;
    }

    private void RestoreRuntimeState()
    {
        ReleaseActorHorizontalMomentumRetention();
        RestoreSpringVisualState();
        RestoreActorGravity();
    }

    private void BeginActorHorizontalMomentumRetention()
    {
        ReleaseActorHorizontalMomentumRetention();
        if (actor == null ||
            rightwardImpulse <= 0f ||
            launchStrength <= 0f ||
            rightwardMomentumRetentionDuration <= 0f)
        {
            return;
        }

        actor.SetPreserveExternalHorizontalMomentum(true);
        _momentumRetentionActor = actor;
    }

    private void ReleaseActorHorizontalMomentumRetention()
    {
        ActorCtrl retentionActor = _momentumRetentionActor;
        _momentumRetentionActor = null;
        if (retentionActor != null)
            retentionActor.SetPreserveExternalHorizontalMomentum(false);
    }

    private void RestoreActorGravity()
    {
        if (_actorInitialStateCaptured && _actorBody != null)
            _actorBody.gravityScale = _actorInitialGravityScale;
    }

    private void PrepareSpringVisualForCompression()
    {
        if (!_springVisualInitialStateCaptured)
            return;

        if (springJoint != null)
            springJoint.enabled = false;

        if (springButtonBody != null)
        {
            springButtonBody.linearVelocity = Vector2.zero;
            springButtonBody.angularVelocity = 0f;
            springButtonBody.gravityScale = _springButtonInitialGravityScale;
        }

        if (springButton != null)
        {
            springButton.localRotation = _springButtonInitialLocalRotation;
            SetSpringButtonLocalPosition(_springButtonInitialLocalPosition);
        }
    }

    private void ReleaseSpringVisual()
    {
        if (springButtonBody == null || springJoint == null)
        {
            if (_springVisualInitialStateCaptured)
                SetSpringButtonLocalPosition(_springButtonInitialLocalPosition);
            return;
        }

        springButtonBody.gravityScale = springReleaseGravityScale;
        springButtonBody.WakeUp();
        springJoint.enabled = true;
    }

    private void RestoreSpringVisualState()
    {
        if (!_springVisualInitialStateCaptured)
            return;

        if (springJoint != null)
            springJoint.enabled = false;

        if (springButtonBody != null)
        {
            springButtonBody.linearVelocity = Vector2.zero;
            springButtonBody.angularVelocity = 0f;
            springButtonBody.gravityScale = _springButtonInitialGravityScale;
        }

        if (springButton != null)
        {
            springButton.localRotation = _springButtonInitialLocalRotation;
            SetSpringButtonLocalPosition(_springButtonInitialLocalPosition);
        }

        if (springJoint != null)
            springJoint.enabled = _springJointInitiallyEnabled;
    }

    private void SetSpringButtonLocalPosition(Vector3 localPosition)
    {
        if (springButton == null)
            return;

        springButton.localPosition = localPosition;
        if (springButtonBody != null)
            springButtonBody.position = springButton.position;
    }

    #endregion
}
