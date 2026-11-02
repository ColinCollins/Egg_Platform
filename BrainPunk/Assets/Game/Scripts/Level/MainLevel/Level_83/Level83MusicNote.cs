using DG.Tweening;
using UnityEngine;

/// <summary>
/// Level 083 单枚实体音符，独立维护生命周期、拖拽计时暂停、释放下落和首次有效命中。
/// </summary>
public sealed class Level83MusicNote : MonoBehaviour
{
    #region Inspector

    /// <summary>实体音符的可见图形。</summary>
    [SerializeField] private SpriteRenderer spriteRenderer;

    /// <summary>释放后负责重力下落和地面碰撞的刚体。</summary>
    [SerializeField] private Rigidbody2D noteRigidbody;

    /// <summary>未被拖拽时保持可见的时长。</summary>
    [SerializeField] private float visibleDuration = 0.8f;

    /// <summary>生命周期到期后的渐隐时长。</summary>
    [SerializeField] private float fadeDuration = 0.2f;

    /// <summary>释放点至少高于 Box Bad 碰撞体顶部的距离，才允许本次下落命中生效。</summary>
    [SerializeField] private float minimumDropHeight = 0.25f;

    /// <summary>弹出弧线上抬高度。</summary>
    [SerializeField] private float emissionArcHeight = 0.45f;

    /// <summary>弹出控制点沿轨迹垂直方向的侧向随机偏移上限。</summary>
    [SerializeField] private float emissionArcSideJitter = 0.08f;

    /// <summary>弹出起始缩放比例；从发声口钻出，远小于原弹出值。</summary>
    [SerializeField] private float emissionStartScaleRatio = 0.1f;

    /// <summary>弹出前随机倾斜上限（度）。</summary>
    [SerializeField] private float emissionRotationMaxAngle = 28f;

    /// <summary>弹出回稳后保留的最大倾角（度）。</summary>
    [SerializeField] private float emissionSettleMaxAngle = 6f;

    /// <summary>弹出时长向下随机比例；只缩短不延长，保证完成上界不变。</summary>
    [SerializeField] private float emissionDurationJitter = 0.15f;

    /// <summary>缩放时长占弹出时长的比例，错峰提前完成。</summary>
    [SerializeField] private float emissionScaleDurationRatio = 0.7f;

    /// <summary>淡入时长占弹出时长的比例，前段快速显形。</summary>
    [SerializeField] private float emissionFadeDurationRatio = 0.15f;

    /// <summary>idle 悬浮振幅。</summary>
    [SerializeField] private float hoverAmplitude = 0.06f;

    /// <summary>idle 悬浮单程时长。</summary>
    [SerializeField] private float hoverHalfCycleDuration = 0.8f;

    /// <summary>idle 呼吸缩放上限。</summary>
    [SerializeField] private float hoverScaleBreathRatio = 1.05f;

    /// <summary>idle 低频摇摆最大倾角（度）。</summary>
    [SerializeField] private float hoverSwayAngle = 8f;

    /// <summary>idle 摇摆单程（基准到极值）时长。</summary>
    [SerializeField] private float hoverSwayHalfCycleDuration = 1.1f;

    /// <summary>落定短促抖动总开关。</summary>
    [SerializeField] private bool arrivalShakeEnabled = true;

    /// <summary>落定短促抖动时长。</summary>
    [SerializeField] private float arrivalShakeDuration = 0.18f;

    /// <summary>落定短促抖动强度（度）。</summary>
    [SerializeField] private float arrivalShakeAngle = 10f;

    /// <summary>消亡上飘距离。</summary>
    [SerializeField] private float expireFloatUpDistance = 0.35f;

    /// <summary>消亡缩小比例。</summary>
    [SerializeField] private float expireShrinkRatio = 0.6f;

    /// <summary>渐隐窗口内拖拽救回时，恢复全亮与原尺寸的时长。</summary>
    [SerializeField] private float rescueRestoreDuration = 0.1f;

    #endregion

    #region Runtime State

    private Level83GameplayCtrl gameplayCtrl;
    private Collider2D rivalCollider;
    private Camera gameplayCamera;
    private Sequence emissionSequence;
    private Sequence lifetimeSequence;
    private Tweener arrivalShakeTweener;
    private Sequence hoverSequence;
    private Sequence swaySequence;
    private Vector3 baseScale;
    private Quaternion baseRotation;
    private Quaternion settleRotation;
    private Vector3 dragOffset;
    private bool hasBeenDragged;
    private bool isDragging;
    private bool isHitArmed;
    private bool awaitingLanding;
    private bool interactionAllowed;
    private bool interactionEnabled;

    #endregion

    #region Unity Messages

    private void OnMouseDown()
    {
        // 拖拽是进入释放下落的唯一入口，先停掉悬浮/落定抖动，避免 transform Tween 与刚体物理抢位置。
        KillHover();

        if (!interactionEnabled || gameplayCamera == null)
            return;

        hasBeenDragged = true;
        isDragging = true;
        isHitArmed = false;
        awaitingLanding = false;
        KillLifetime();

        // 渐隐最后窗口内救回：平滑补回全亮与原尺寸，不停留在半透明/缩小中间态。
        if (spriteRenderer != null && spriteRenderer.color.a < 1f)
            spriteRenderer.DOFade(1f, rescueRestoreDuration).SetLink(gameObject);
        if ((transform.localScale - baseScale).sqrMagnitude > 0.0001f)
            transform.DOScale(baseScale, rescueRestoreDuration).SetLink(gameObject);

        if (noteRigidbody != null)
        {
            noteRigidbody.linearVelocity = Vector2.zero;
            noteRigidbody.angularVelocity = 0f;
            noteRigidbody.bodyType = RigidbodyType2D.Kinematic;
        }

        dragOffset = transform.position - GetPointerWorldPosition();
    }

    private void OnMouseDrag()
    {
        if (!interactionEnabled || !isDragging || gameplayCamera == null)
            return;

        Vector3 targetPosition = GetPointerWorldPosition() + dragOffset;
        targetPosition.z = transform.position.z;
        transform.position = targetPosition;
    }

    private void OnMouseUp()
    {
        if (!isDragging)
            return;

        isDragging = false;
        isHitArmed = interactionEnabled && IsReleasedAboveRival();
        // 下落途中不计时：等待首次碰撞落定后才重新计满可见时长，
        // 避免举高释放后音符还在空中就提前渐隐消亡。
        awaitingLanding = interactionEnabled;

        if (noteRigidbody != null)
        {
            noteRigidbody.bodyType = RigidbodyType2D.Dynamic;
            noteRigidbody.gravityScale = 1f;
            noteRigidbody.WakeUp();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryReportRivalHit(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryReportRivalHit(collision.collider);
        MarkLanded();
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        MarkLanded();
    }

    private void OnDestroy()
    {
        KillLifetime();
        KillHover();
        emissionSequence?.Kill();
        transform.DOKill();
    }

    #endregion

    #region Public API

    /// <summary>
    /// 初始化运行时生成的实体音符；模板对象自身不得调用。
    /// </summary>
    public void Initialize(
        Level83GameplayCtrl owner,
        Camera camera,
        Collider2D targetCollider,
        Vector3 arrivalPosition,
        float emissionDuration)
    {
        gameplayCtrl = owner;
        gameplayCamera = camera;
        rivalCollider = targetCollider;
        hasBeenDragged = false;
        isDragging = false;
        isHitArmed = false;
        interactionAllowed = true;
        interactionEnabled = false;

        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = 1f;
            spriteRenderer.color = color;
        }

        if (noteRigidbody != null)
        {
            noteRigidbody.bodyType = RigidbodyType2D.Kinematic;
            noteRigidbody.gravityScale = 0f;
            noteRigidbody.linearVelocity = Vector2.zero;
        }

        StartEmission(arrivalPosition, emissionDuration);
    }

    /// <summary>
    /// 胜负收口时关闭该音符交互；已释放的物理位置保持不变。
    /// </summary>
    public void SetInteractionEnabled(bool enabled)
    {
        interactionAllowed = enabled;
        interactionEnabled = enabled && emissionSequence == null;
        if (enabled || noteRigidbody == null || !isDragging)
            return;

        isDragging = false;
        noteRigidbody.bodyType = RigidbodyType2D.Dynamic;
        noteRigidbody.gravityScale = 1f;
        // 冻结交互时强制脱手的音符同样等落定后再计时，避免永久滞留或空中消亡。
        awaitingLanding = true;
    }

    /// <summary>命中胜利后立即消费该音符并停止全部本地 Tween。</summary>
    public void Consume()
    {
        interactionEnabled = false;
        interactionAllowed = false;
        awaitingLanding = false;
        emissionSequence?.Kill();
        emissionSequence = null;
        KillLifetime();
        KillHover();
        transform.DOKill();
        Destroy(gameObject);
    }

    /// <summary>说明该音符是否曾经被玩家拖拽过，供验证脚本读取。</summary>
    public bool HasBeenDragged => hasBeenDragged;

    /// <summary>说明音符是否已经完成发声飘出并到达可拖拽位置。</summary>
    public bool IsEmissionComplete => emissionSequence == null;

    /// <summary>说明音符当前是否已经允许玩家拖拽。</summary>
    public bool IsInteractionEnabled => interactionEnabled;

    #endregion

    #region Helpers

    private void StartEmission(Vector3 arrivalPosition, float duration)
    {
        emissionSequence?.Kill();
        Vector3 startPosition = transform.position;
        baseScale = transform.localScale;
        baseRotation = transform.rotation;
        settleRotation = baseRotation * Quaternion.Euler(0f, 0f, Random.Range(-emissionSettleMaxAngle, emissionSettleMaxAngle));

        // 弧线中点上抬，并沿轨迹垂直方向加侧向随机，让每枚音符弧线略有差异；只影响路径形状，不影响落点。
        Vector3 pathDirection = arrivalPosition - startPosition;
        Vector3 sideDirection = pathDirection.sqrMagnitude > 0.0001f
            ? Vector3.Cross(pathDirection, Vector3.forward).normalized
            : Vector3.right;
        Vector3 controlPosition = Vector3.Lerp(startPosition, arrivalPosition, 0.5f)
                                  + Vector3.up * emissionArcHeight
                                  + sideDirection * Random.Range(-emissionArcSideJitter, emissionArcSideJitter);

        transform.localScale = baseScale * emissionStartScaleRatio;
        transform.rotation = baseRotation * Quaternion.Euler(0f, 0f, Random.Range(-emissionRotationMaxAngle, emissionRotationMaxAngle));

        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = 0f;
            spriteRenderer.color = color;
        }

        // 时长只向下随机，绝不延长：既有 PlayMode 验证在 0.45s 断言弹出已完成。
        float actualDuration = duration * Random.Range(1f - emissionDurationJitter, 1f);
        if (actualDuration <= 0f)
        {
            transform.position = arrivalPosition;
            transform.localScale = baseScale;
            transform.rotation = baseRotation;
            CompleteEmission();
            return;
        }

        emissionSequence = DOTween.Sequence().SetLink(gameObject);
        // 吹出节奏：离开发声口快、接近停留点减速（先快后慢），不用 OutBack 回弹。
        emissionSequence.Join(
            transform.DOPath(
                    new[] { controlPosition, arrivalPosition },
                    actualDuration,
                    PathType.CatmullRom,
                    PathMode.Full3D)
                .SetEase(Ease.OutCubic));
        emissionSequence.Join(
            transform.DOScale(baseScale, actualDuration * emissionScaleDurationRatio).SetEase(Ease.OutCubic));
        emissionSequence.Join(
            transform.DORotateQuaternion(settleRotation, actualDuration).SetEase(Ease.OutCubic));
        if (spriteRenderer != null)
            emissionSequence.Join(spriteRenderer.DOFade(1f, actualDuration * emissionFadeDurationRatio).SetEase(Ease.OutQuad));
        emissionSequence.OnComplete(CompleteEmission);
    }

    private void CompleteEmission()
    {
        emissionSequence = null;
        interactionEnabled = interactionAllowed;
        if (!interactionAllowed)
            return;

        StartLifetime();

        // 落定短促抖动作为节拍重音，结束后再进入悬浮 idle；抖动期间已可拖拽，KillHover 会中断它。
        if (arrivalShakeEnabled && arrivalShakeDuration > 0f)
        {
            arrivalShakeTweener?.Kill();
            arrivalShakeTweener = transform
                .DOShakeRotation(arrivalShakeDuration, Vector3.forward * arrivalShakeAngle)
                .SetLink(gameObject)
                .OnComplete(StartHover);
        }
        else
        {
            StartHover();
        }
    }

    private void StartLifetime()
    {
        KillLifetime();
        lifetimeSequence = DOTween.Sequence().SetLink(gameObject);
        lifetimeSequence.AppendInterval(visibleDuration);
        // 消亡开始前停掉悬浮，随后上飘 + 缩小 + 渐隐三者同步；
        // 先 Append 上飘段，保证 spriteRenderer 为 null 时缩小仍挂接到上飘段。
        lifetimeSequence.AppendCallback(KillHover);
        lifetimeSequence.Append(
            transform.DOBlendableMoveBy(Vector3.up * expireFloatUpDistance, fadeDuration).SetEase(Ease.OutQuad));
        lifetimeSequence.Join(transform.DOScale(baseScale * expireShrinkRatio, fadeDuration).SetEase(Ease.InQuad));
        if (spriteRenderer != null)
            lifetimeSequence.Join(spriteRenderer.DOFade(0f, fadeDuration));
        lifetimeSequence.OnComplete(() => Destroy(gameObject));
    }

    private void KillLifetime()
    {
        lifetimeSequence?.Kill();
        lifetimeSequence = null;
        spriteRenderer?.DOKill();
    }

    /// <summary>
    /// 启动 idle 悬浮：上下浮动、呼吸缩放与低频旋转摇摆三者周期互不相同，避免机械感；
    /// 摇摆只改变姿态不改变位置，不影响拖拽瞄准与判定。
    /// </summary>
    private void StartHover()
    {
        KillHover();

        hoverSequence = DOTween.Sequence().SetLink(gameObject);
        hoverSequence.Join(
            transform.DOBlendableMoveBy(Vector3.up * hoverAmplitude, hoverHalfCycleDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo));
        hoverSequence.Join(
            transform.DOScale(baseScale * hoverScaleBreathRatio, hoverHalfCycleDuration * 0.7f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo));

        // 三段首尾相接的旋转构成绕 settle 朝向 ±hoverSwayAngle 的连续往返摇摆，避免起始相位瞬跳。
        Quaternion swayPeak = settleRotation * Quaternion.Euler(0f, 0f, hoverSwayAngle);
        Quaternion swayValley = settleRotation * Quaternion.Euler(0f, 0f, -hoverSwayAngle);
        swaySequence = DOTween.Sequence().SetLink(gameObject);
        swaySequence.Append(
            transform.DORotateQuaternion(swayPeak, hoverSwayHalfCycleDuration).SetEase(Ease.InOutSine));
        swaySequence.Append(
            transform.DORotateQuaternion(swayValley, hoverSwayHalfCycleDuration * 2f).SetEase(Ease.InOutSine));
        swaySequence.Append(
            transform.DORotateQuaternion(settleRotation, hoverSwayHalfCycleDuration).SetEase(Ease.InOutSine));
        swaySequence.SetLoops(-1, LoopType.Restart);
    }

    /// <summary>停止落定抖动与全部 idle 悬浮 Tween。</summary>
    private void KillHover()
    {
        arrivalShakeTweener?.Kill();
        arrivalShakeTweener = null;
        hoverSequence?.Kill();
        hoverSequence = null;
        swaySequence?.Kill();
        swaySequence = null;
    }

    private Vector3 GetPointerWorldPosition()
    {
        Vector3 screenPosition = Input.mousePosition;
        screenPosition.z = Mathf.Abs(gameplayCamera.transform.position.z - transform.position.z);
        return gameplayCamera.ScreenToWorldPoint(screenPosition);
    }

    /// <summary>释放后首次接触到场景表面视为落定，此后才重新计满可见时长；
    /// 武装命中时 Consume 已先清除 awaitingLanding 并销毁本音符，不会重复计时。</summary>
    private void MarkLanded()
    {
        if (!awaitingLanding || isDragging)
            return;

        awaitingLanding = false;
        StartLifetime();
    }

    private bool IsReleasedAboveRival()
    {
        if (rivalCollider == null)
            return false;

        Bounds bounds = rivalCollider.bounds;
        return transform.position.y >= bounds.max.y + minimumDropHeight &&
               transform.position.x >= bounds.min.x &&
               transform.position.x <= bounds.max.x;
    }

    private void TryReportRivalHit(Collider2D other)
    {
        if (!interactionEnabled || isDragging || !isHitArmed || other == null || rivalCollider == null)
            return;

        if (other != rivalCollider && !other.transform.IsChildOf(rivalCollider.transform))
            return;

        isHitArmed = false;
        gameplayCtrl?.HandleMusicNoteHit(this);
    }

    #endregion
}
