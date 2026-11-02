using Bear.Logger;
using DG.Tweening;
using Game.Scripts.Common;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Level70 本关 catch 机械控制器，负责 InQuad 曲线移动、边界撞击、下落检测、回收和开合。
/// 角色控制权、GameplayPanel、墙体破碎表现及 Trigger/Input 订阅仍由 Level70Ctrl 编排。
/// </summary>
public class Level70HookCtrl : MonoBehaviour, IDebuger
{
    #region Types

    private enum HookControlState
    {
        Idle,
        Lowering,
        Recovering
    }

    #endregion

    #region Inspector

    // Level70 编排控制器；左侧高速撞击成功时只调用本关墙体破碎入口，不跨关卡派发事件。
    [SerializeField] private Level70Ctrl levelCtrl;

    // 抓钩 Animator；必须保留 isOpen 参数语义，用于开合抓钩表现。
    [SerializeField] private Animator catchAnim;

    // 抓钩根节点；为空时使用 catchAnim.transform，所有抓钩位移只作用于该节点。
    [SerializeField] private Transform catchRoot;

    // 抓取判定区域；为空时按 catchRoot 子节点 catchArea 自动查找，仅在下落期间轮询重叠检测。
    [SerializeField] private Collider2D catchArea;

    // 目标释放点；其 y 值作为接触或 miss 后的回收高度，x 值作为水平回收目标。
    [SerializeField] private Transform dropPoint;

    // 本关所有球的实体碰撞体；在 Prefab 显式绑定，仅释放夹子范围内尚未打开的动态球。
    [SerializeField] private Collider2D[] releaseBalls;

    // 夹子左右爪及顶部挡块的实体碰撞体；显式绑定，不包含 catchArea 触发器。
    [SerializeField] private Collider2D[] releaseHookColliders;

    // 松夹时球的最低向下速度；只设置一次，保留落地碰撞和球壳打开流程。
    [SerializeField, Min(0.1f)] private float releaseDownSpeed = 2f;

    // 左墙未破碎时的 catch 左边界；高速撞击只能在此 range1Min 位置判定。
    [SerializeField] private Transform preCrashMovementRangeMin;

    // 左墙破碎后的 catch 左边界；只有 PlayLeftWallCrash 成功后才使用 range2Min。
    [SerializeField] private Transform movementRangeMin;

    // catch 完整水平行程右边界；从一端持续移动到另一端时，按 InQuad 提升至 maxHorizontalSpeed。
    [SerializeField] private Transform movementRangeMax;

    // Catch 未命中时的下落边界；为空时使用初始抓钩高度减去 fallbackLowerDistance。
    [SerializeField] private Transform lowerBoundary;

    // catch 水平最大速度；InQuad 曲线到达目标边界时使用该上限。
    [SerializeField, Min(0f)] private float maxHorizontalSpeed = 3f;

    // catch 开始水平移动时的最低速度；运行时会限制在 0 至 maxHorizontalSpeed 之间。
    [SerializeField, Min(0f)] private float minHorizontalSpeed = 1f;

    // 左侧破墙速度阈值占最大速度的比例；只有左侧且实际速度严格超过该比例才会破碎墙体。
    [SerializeField, Range(0f, 1f)] private float crashSpeedRatio = 0.8f;

    // 左侧撞击未达到破墙条件时播放的失败 SpriteRenderer。
    [SerializeField] private SpriteRenderer crashEffectLeft;

    // 右侧撞击时播放的失败 SpriteRenderer；右侧无论速度高低都不会破碎墙体。
    [SerializeField] private SpriteRenderer crashEffectRight;

    // 失败效果 ScaleX 从 0 到 1 的耗时。
    [SerializeField, Min(0f)] private float failureScaleDuration = 0.3f;

    // 失败效果完成缩放后从当前 alpha 渐隐到 0 的耗时。
    [SerializeField, Min(0f)] private float failureFadeDuration = 0.5f;

    // 抓钩下落耗时；必须大于 0，避免 Catch 瞬移导致碰撞判定不稳定。
    [SerializeField, Min(0.01f)] private float lowerDuration = 1.1f;

    // 旧 miss 回收配置仅为兼容已有 prefab 序列化保留；当前接触与 miss 均走 retractDuration，不单独读取该值。
    [SerializeField, Min(0.01f)] private float missRecoverDuration = 0.45f;

    // 接触或 miss 后上移到 dropPoint 高度的耗时。
    [SerializeField, Min(0.01f)] private float retractDuration = 0.75f;

    // 接触或 miss 后水平移动到 dropPoint 的耗时。
    [SerializeField, Min(0.01f)] private float moveToDropDuration = 0.75f;

    // 未配置 lowerBoundary 时从初始抓钩高度向下的距离。
    [SerializeField, Min(0.1f)] private float fallbackLowerDistance = 3.5f;

    // 未配置破墙前左边界时使用的最小 x。
    [SerializeField] private float fallbackPreCrashMovementRangeMinX = -4.5f;

    // 未配置破墙后左边界时使用的最小 x。
    [SerializeField] private float fallbackMovementRangeMinX = -6.5f;

    // 未配置完整行程右边界时使用的最大 x。
    [SerializeField] private float fallbackMovementRangeMaxX = 6.5f;

    #endregion

    #region Runtime

    private const string AnimatorOpenBool = "isOpen";
    private const string ActorTag = "Player";
    private const string ToolsTag = "tools";
    private const string LockTag = "lock";
    private const float DropPointOpenWaitDuration = 2f;
    private const float BoundaryEpsilon = 0.001f;

    private readonly Collider2D[] catchOverlapResults = new Collider2D[8];
    private Sequence hookSequence;
    private Sequence leftFailureSequence;
    private Sequence rightFailureSequence;
    private ContactFilter2D catchContactFilter;
    private HookControlState hookState = HookControlState.Idle;
    private Vector3 initialHookPosition;
    private bool hasInitialHookPosition;
    private bool hasCatchContactFilter;
    private bool hookControlActive;
    private bool hasBrokenLeftWall;
    private bool leftBoundaryImpactHandled;
    private bool rightBoundaryImpactHandled;
    private bool isOpen;
    private float hookMoveInput;
    private float horizontalVelocity;
    private float horizontalSpeedCurveElapsed;
    private float horizontalSpeedCurveDuration;
    private float horizontalSpeedCurveDirection;
    private bool hasHorizontalSpeedCurve;
    private bool[] releasingBalls;
    private bool[,] releaseOriginalIgnores;
    private float releaseBottomY;

    /// <summary>
    /// 当前是否已进入 Level70 catch 控制；仅供 Level70Ctrl 判断输入和角色控制权路由。
    /// </summary>
    public bool IsControlActive => hookControlActive;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();
        CacheInitialState();
        ResetRuntime();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        UpdateHookMovement();
        UpdateCatchAreaOverlap();
    }

    private void FixedUpdate()
    {
        if (!Application.isPlaying || releasingBalls == null)
            return;

        for (int i = 0; i < releasingBalls.Length; i++)
        {
            if (!releasingBalls[i])
                continue;

            Collider2D ball = releaseBalls[i];
            if (ball == null || !ball.isActiveAndEnabled ||
                (ball.bounds.max.y < releaseBottomY - 0.05f && !TouchesReleaseHook(ball)))
                RestoreReleaseCollision(i);
        }
    }

    private void OnDisable()
    {
        RestoreReleaseCollisions();
    }

    private void OnDestroy()
    {
        RestoreReleaseCollisions();
    }

    #endregion

    #region Public Control API

    /// <summary>
    /// 缓存 catch 初始位置；可由 Level70Ctrl 重复调用，首次缓存后不会被运行时位置覆盖。
    /// </summary>
    public void CacheInitialState()
    {
        ResolveReferences();
        if (catchRoot == null || hasInitialHookPosition)
            return;

        initialHookPosition = catchRoot.position;
        hasInitialHookPosition = true;
    }

    /// <summary>
    /// 进入 catch 控制并恢复 Idle、关闭抓钩；角色和 UI 状态必须由 Level70Ctrl 处理。
    /// </summary>
    public void ActivateControl()
    {
        if (!Application.isPlaying || hookControlActive)
            return;

        ResolveReferences();
        CacheInitialState();
        hookControlActive = true;
        hookMoveInput = 0f;
        horizontalVelocity = 0f;
        ResetHorizontalSpeedCurve();
        hookState = HookControlState.Idle;
        SetHookOpen(false);
        ClampHookToCurrentRange();
    }

    /// <summary>
    /// 退出 catch 输入控制并清零水平速度，但不强制中断当前下落或回收 Tween；完整清理调用 ResetRuntime。
    /// </summary>
    public void DeactivateControl()
    {
        hookControlActive = false;
        hookMoveInput = 0f;
        horizontalVelocity = 0f;
        ResetHorizontalSpeedCurve();
    }

    /// <summary>
    /// 设置 catch 水平 InQuad 曲线移动方向；小于 0 向左，大于 0 向右，0 代表无左右输入并立即停止。
    /// </summary>
    /// <param name="direction">水平输入方向。</param>
    public void SetMoveInput(float direction)
    {
        if (!hookControlActive)
            return;

        if (Mathf.Approximately(direction, 0f))
        {
            hookMoveInput = 0f;
            horizontalVelocity = 0f;
            ResetHorizontalSpeedCurve();
            return;
        }

        hookMoveInput = Mathf.Sign(direction);
    }

    /// <summary>
    /// 开始一次 catch 下落；仅在控制激活且 Idle 时有效，重复输入不会叠加 Tween。
    /// </summary>
    public void TryStartCatch()
    {
        ResolveReferences();
        if (!Application.isPlaying || !hookControlActive || hookState != HookControlState.Idle || catchRoot == null)
            return;

        hookMoveInput = 0f;
        horizontalVelocity = 0f;
        ResetHorizontalSpeedCurve();
        hookState = HookControlState.Lowering;
        SetHookOpen(true);
        hookSequence?.Kill(false);
        catchRoot.DOKill();

        hookSequence = DOTween.Sequence().SetLink(gameObject);
        hookSequence.Append(catchRoot.DOMoveY(GetLowerBoundaryY(), Mathf.Max(0.01f, lowerDuration)).SetEase(Ease.Linear));
        hookSequence.OnComplete(OnLoweringReachedBoundary);
        hookSequence.OnKill(() => hookSequence = null);
    }

    /// <summary>
    /// 终止 catch Tween、速度与失败效果，并恢复完整行程内的初始位置；供关卡重置、成功、失败或卸载调用。
    /// </summary>
    public void ResetRuntime()
    {
        RestoreReleaseCollisions();
        ResolveReferences();
        CacheInitialState();
        hookSequence?.Kill(false);
        hookSequence = null;
        catchRoot?.DOKill();

        hookState = HookControlState.Idle;
        hookControlActive = false;
        hookMoveInput = 0f;
        horizontalVelocity = 0f;
        ResetHorizontalSpeedCurve();
        hasBrokenLeftWall = false;
        leftBoundaryImpactHandled = false;
        rightBoundaryImpactHandled = false;
        SetHookOpen(false);
        ResetFailureEffects();

        if (catchRoot != null && hasInitialHookPosition)
            catchRoot.position = initialHookPosition;
    }

    #endregion

    #region Horizontal Speed Curve & Boundary Impact

    private void UpdateHookMovement()
    {
        if (!CanReceiveHookMoveInput())
            return;

        float rangeMinX = GetRangeMinX();
        float rangeMaxX = GetRangeMaxX();
        Vector3 position = catchRoot.position;
        ReleaseBoundaryImpactLocks(position.x, rangeMinX, rangeMaxX);

        if (ShouldBlockInputAtBoundary(position.x, rangeMinX, rangeMaxX))
        {
            horizontalVelocity = 0f;
            ResetHorizontalSpeedCurve();
            return;
        }

        float travelDistance = Mathf.Max(0f, rangeMaxX - rangeMinX);
        float maxSpeed = Mathf.Max(0f, maxHorizontalSpeed);
        if (travelDistance <= Mathf.Epsilon || maxSpeed <= Mathf.Epsilon)
        {
            horizontalVelocity = 0f;
            ResetHorizontalSpeedCurve();
            return;
        }

        UpdateHorizontalVelocity(rangeMinX, rangeMaxX);

        if (Mathf.Approximately(horizontalVelocity, 0f))
            return;

        float nextX = position.x + horizontalVelocity * Time.deltaTime;
        if (horizontalVelocity < 0f && nextX <= rangeMinX)
        {
            position.x = rangeMinX;
            catchRoot.position = position;
            HandleBoundaryImpact(true, Mathf.Abs(horizontalVelocity));
            return;
        }

        if (horizontalVelocity > 0f && nextX >= rangeMaxX)
        {
            position.x = rangeMaxX;
            catchRoot.position = position;
            HandleBoundaryImpact(false, Mathf.Abs(horizontalVelocity));
            return;
        }

        position.x = Mathf.Clamp(nextX, rangeMinX, rangeMaxX);
        catchRoot.position = position;
    }

    private bool CanReceiveHookMoveInput()
    {
        return hookControlActive && hookState == HookControlState.Idle && catchRoot != null;
    }

    private float GetHorizontalReversalDeceleration(float rangeMinX, float rangeMaxX)
    {
        float travelDistance = Mathf.Max(0f, rangeMaxX - rangeMinX);
        float maxSpeed = Mathf.Max(0f, maxHorizontalSpeed);
        float minSpeed = Mathf.Clamp(minHorizontalSpeed, 0f, maxSpeed);
        if (travelDistance <= Mathf.Epsilon || maxSpeed <= Mathf.Epsilon)
            return 0f;

        return (maxSpeed * maxSpeed - minSpeed * minSpeed) / (2f * travelDistance);
    }

    private void UpdateHorizontalVelocity(float rangeMinX, float rangeMaxX)
    {
        float maxSpeed = Mathf.Max(0f, maxHorizontalSpeed);
        float minSpeed = Mathf.Clamp(minHorizontalSpeed, 0f, maxSpeed);

        if (Mathf.Approximately(hookMoveInput, 0f))
        {
            horizontalVelocity = 0f;
            ResetHorizontalSpeedCurve();
            return;
        }

        float inputDirection = Mathf.Sign(hookMoveInput);
        if (Mathf.Approximately(horizontalVelocity, 0f))
        {
            BeginHorizontalSpeedCurve(inputDirection, rangeMinX, rangeMaxX, minSpeed, maxSpeed);
            horizontalVelocity = inputDirection * minSpeed;
            if (minSpeed <= Mathf.Epsilon)
                horizontalVelocity = inputDirection * EvaluateHorizontalSpeedCurve(minSpeed, maxSpeed, Time.deltaTime);
            return;
        }

        if (Mathf.Sign(horizontalVelocity) != inputDirection)
        {
            float reversalDeceleration = GetHorizontalReversalDeceleration(rangeMinX, rangeMaxX);
            float decelerationStep = Mathf.Max(0f, reversalDeceleration) * Time.deltaTime;
            if (decelerationStep <= Mathf.Epsilon)
            {
                horizontalVelocity = 0f;
                ResetHorizontalSpeedCurve();
                return;
            }

            horizontalVelocity = Mathf.MoveTowards(horizontalVelocity, 0f, decelerationStep);
            if (Mathf.Abs(horizontalVelocity) < minSpeed)
            {
                horizontalVelocity = 0f;
                ResetHorizontalSpeedCurve();
            }
            return;
        }

        if (!hasHorizontalSpeedCurve || !Mathf.Approximately(horizontalSpeedCurveDirection, inputDirection))
            BeginHorizontalSpeedCurve(inputDirection, rangeMinX, rangeMaxX, minSpeed, maxSpeed);

        horizontalVelocity = inputDirection * EvaluateHorizontalSpeedCurve(minSpeed, maxSpeed, Time.deltaTime);
    }

    private void BeginHorizontalSpeedCurve(float direction, float rangeMinX, float rangeMaxX, float minSpeed, float maxSpeed)
    {
        float travelDistance = Mathf.Max(0f, rangeMaxX - rangeMinX);
        float inQuadAverageSpeed = (maxSpeed + 2f * minSpeed) / 3f;

        horizontalSpeedCurveElapsed = 0f;
        horizontalSpeedCurveDuration = inQuadAverageSpeed > Mathf.Epsilon
            ? travelDistance / inQuadAverageSpeed
            : 0f;
        horizontalSpeedCurveDirection = direction;
        hasHorizontalSpeedCurve = true;
    }

    private float EvaluateHorizontalSpeedCurve(float minSpeed, float maxSpeed, float deltaTime)
    {
        if (!hasHorizontalSpeedCurve || horizontalSpeedCurveDuration <= Mathf.Epsilon)
            return minSpeed;

        horizontalSpeedCurveElapsed = Mathf.Min(horizontalSpeedCurveElapsed + Mathf.Max(0f, deltaTime), horizontalSpeedCurveDuration);
        float normalizedTime = Mathf.Clamp01(horizontalSpeedCurveElapsed / horizontalSpeedCurveDuration);
        float inQuadProgress = normalizedTime * normalizedTime;
        return Mathf.Lerp(minSpeed, maxSpeed, inQuadProgress);
    }

    private void ResetHorizontalSpeedCurve()
    {
        horizontalSpeedCurveElapsed = 0f;
        horizontalSpeedCurveDuration = 0f;
        horizontalSpeedCurveDirection = 0f;
        hasHorizontalSpeedCurve = false;
    }

    private bool ShouldBlockInputAtBoundary(float currentX, float rangeMinX, float rangeMaxX)
    {
        if (leftBoundaryImpactHandled && currentX <= rangeMinX + BoundaryEpsilon && hookMoveInput <= 0f)
            return true;

        return rightBoundaryImpactHandled && currentX >= rangeMaxX - BoundaryEpsilon && hookMoveInput >= 0f;
    }

    private void ReleaseBoundaryImpactLocks(float currentX, float rangeMinX, float rangeMaxX)
    {
        if (currentX > rangeMinX + BoundaryEpsilon)
            leftBoundaryImpactHandled = false;

        if (currentX < rangeMaxX - BoundaryEpsilon)
            rightBoundaryImpactHandled = false;
    }

    private void HandleBoundaryImpact(bool isLeftBoundary, float impactSpeed)
    {
        horizontalVelocity = 0f;
        ResetHorizontalSpeedCurve();
        if (isLeftBoundary)
            leftBoundaryImpactHandled = true;
        else
            rightBoundaryImpactHandled = true;

        float maxSpeed = Mathf.Max(0f, maxHorizontalSpeed);
        float requiredSpeed = maxSpeed * Mathf.Clamp01(crashSpeedRatio);
        bool exceedsCrashSpeed = maxSpeed > Mathf.Epsilon && impactSpeed > requiredSpeed;

        if (isLeftBoundary && exceedsCrashSpeed && !hasBrokenLeftWall && levelCtrl != null && levelCtrl.PlayLeftWallCrash())
        {
            hasBrokenLeftWall = true;
            return;
        }

        if (!isLeftBoundary || !hasBrokenLeftWall)
            PlayFailureEffect(isLeftBoundary);
    }

    private void PlayFailureEffect(bool isLeftBoundary)
    {
        SpriteRenderer effectRenderer = isLeftBoundary ? crashEffectLeft : crashEffectRight;
        if (effectRenderer == null)
        {
            this.LogWarning($"[Level70HookCtrl] Missing {(isLeftBoundary ? "left" : "right")} crash failure effect.");
            return;
        }

        Sequence currentSequence = isLeftBoundary ? leftFailureSequence : rightFailureSequence;
        currentSequence?.Kill(false);
        effectRenderer.transform.DOKill();
        effectRenderer.DOKill();

        effectRenderer.transform.localScale = Vector3.zero;
        effectRenderer.color = SetAlpha(effectRenderer.color, 1f);

        AudioManager.PlaySound("l70FailCrash");

        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        sequence.Append(effectRenderer.transform.DOScale(Vector3.one, Mathf.Max(0f, failureScaleDuration)).SetEase(Ease.OutBack));
        sequence.Append(effectRenderer.DOFade(0f, Mathf.Max(0f, failureFadeDuration)).SetEase(Ease.OutQuad));
        sequence.OnKill(() => ClearFailureSequence(isLeftBoundary, sequence));
        sequence.OnComplete(() => ClearFailureSequence(isLeftBoundary, sequence));

        if (isLeftBoundary)
            leftFailureSequence = sequence;
        else
            rightFailureSequence = sequence;
    }

    private void ResetFailureEffects()
    {
        leftFailureSequence?.Kill(false);
        rightFailureSequence?.Kill(false);
        leftFailureSequence = null;
        rightFailureSequence = null;
        ResetFailureEffect(crashEffectLeft);
        ResetFailureEffect(crashEffectRight);
    }

    private void ResetFailureEffect(SpriteRenderer effectRenderer)
    {
        if (effectRenderer == null)
            return;

        effectRenderer.transform.DOKill();
        effectRenderer.DOKill();
        effectRenderer.transform.localScale = Vector3.zero;
        effectRenderer.color = SetAlpha(effectRenderer.color, 0f);
    }

    private void ClearFailureSequence(bool isLeftBoundary, Sequence sequence)
    {
        if (isLeftBoundary && leftFailureSequence == sequence)
            leftFailureSequence = null;
        else if (!isLeftBoundary && rightFailureSequence == sequence)
            rightFailureSequence = null;
    }

    private Color SetAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    #endregion

    #region Hook Flow

    private void OnLoweringReachedBoundary()
    {
        if (hookState != HookControlState.Lowering)
            return;

        RecoverAfterMiss();
    }

    private void UpdateCatchAreaOverlap()
    {
        if (hookState != HookControlState.Lowering || catchArea == null)
            return;

        EnsureCatchContactFilter();
        int count = catchArea.Overlap(catchContactFilter, catchOverlapResults);
        for (int i = 0; i < count; i++)
        {
            Collider2D candidate = catchOverlapResults[i];
            if (!IsHookContactCollider(candidate))
                continue;

            HandleCatchAreaContact(candidate);
            if (hookState != HookControlState.Lowering)
                break;
        }

        for (int i = 0; i < count; i++)
            catchOverlapResults[i] = null;
    }

    private void HandleCatchAreaContact(Collider2D other)
    {
        if (!Application.isPlaying || hookState != HookControlState.Lowering)
            return;

        ResolveReferences();
        if (!IsHookContactCollider(other))
            return;

        RecoverAfterContact();
    }

    private void RecoverAfterMiss()
    {
        ReturnHookToDropPointAfterCatch();
    }

    private void RecoverAfterContact()
    {
        ReturnHookToDropPointAfterCatch();
    }

    private void ReturnHookToDropPointAfterCatch()
    {
        if (!Application.isPlaying || hookState != HookControlState.Lowering || catchRoot == null)
            return;

        hookMoveInput = 0f;
        horizontalVelocity = 0f;
        ResetHorizontalSpeedCurve();
        hookSequence?.Kill(false);
        catchRoot.DOKill();
        SetHookOpen(false);

        hookState = HookControlState.Recovering;
        hookSequence = DOTween.Sequence().SetLink(gameObject);
        hookSequence.Append(catchRoot.DOMoveY(GetDropHeightY(), Mathf.Max(0.01f, retractDuration)).SetEase(Ease.InOutSine));
        hookSequence.Append(catchRoot.DOMoveX(GetDropX(), Mathf.Max(0.01f, moveToDropDuration)).SetEase(Ease.InOutSine));
        hookSequence.AppendCallback(OpenHookAndReleaseBalls);
        hookSequence.AppendInterval(DropPointOpenWaitDuration);
        hookSequence.OnComplete(ReturnToIdleAfterDropPointOpen);
        hookSequence.OnKill(() => hookSequence = null);
    }

    private void ReturnToIdleAfterDropPointOpen()
    {
        SetHookOpen(false);
        hookState = HookControlState.Idle;
        horizontalVelocity = 0f;
        ResetHorizontalSpeedCurve();
        ClampHookToCurrentRange();
    }

    private void ClampHookToCurrentRange()
    {
        if (catchRoot == null)
            return;

        Vector3 position = catchRoot.position;
        position.x = Mathf.Clamp(position.x, GetRangeMinX(), GetRangeMaxX());
        catchRoot.position = position;
    }

    private void SetHookOpen(bool open)
    {
        isOpen = open;

        if (catchAnim != null)
            catchAnim.SetBool(AnimatorOpenBool, isOpen);
    }

    #region 松夹落球

    private void OpenHookAndReleaseBalls()
    {
        SetHookOpen(true);
        if (!Application.isPlaying || catchArea == null || releaseBalls == null ||
            releaseHookColliders == null || releaseHookColliders.Length == 0)
            return;

        if (releasingBalls == null)
        {
            releasingBalls = new bool[releaseBalls.Length];
            releaseOriginalIgnores = new bool[releaseBalls.Length, releaseHookColliders.Length];
        }

        Physics2D.SyncTransforms();
        Bounds releaseBounds = catchArea.bounds;
        foreach (Collider2D claw in releaseHookColliders)
        {
            if (claw != null && claw.isActiveAndEnabled)
                releaseBounds.Encapsulate(claw.bounds);
        }
        releaseBottomY = releaseBounds.min.y;

        for (int i = 0; i < releaseBalls.Length; i++)
        {
            Collider2D ball = releaseBalls[i];
            if (releasingBalls[i] || ball == null || !ball.isActiveAndEnabled ||
                !releaseBounds.Intersects(ball.bounds))
                continue;

            Rigidbody2D body = ball.attachedRigidbody;
            if (body == null || !body.simulated || body.bodyType != RigidbodyType2D.Dynamic ||
                (body.constraints & RigidbodyConstraints2D.FreezePositionY) != 0)
                continue;

            releasingBalls[i] = true;
            for (int j = 0; j < releaseHookColliders.Length; j++)
            {
                Collider2D claw = releaseHookColliders[j];
                if (claw == null)
                    continue;
                releaseOriginalIgnores[i, j] = Physics2D.GetIgnoreCollision(ball, claw);
                Physics2D.IgnoreCollision(ball, claw, true);
            }
            body.WakeUp();
            body.linearVelocity = new Vector2(body.linearVelocity.x,
                Mathf.Min(body.linearVelocity.y, -Mathf.Max(0.1f, releaseDownSpeed)));
        }
    }

    private bool TouchesReleaseHook(Collider2D ball)
    {
        foreach (Collider2D claw in releaseHookColliders)
        {
            if (claw != null && claw.isActiveAndEnabled && claw.bounds.Intersects(ball.bounds))
                return true;
        }
        return false;
    }

    private void RestoreReleaseCollision(int ballIndex)
    {
        Collider2D ball = releaseBalls[ballIndex];
        for (int j = 0; j < releaseHookColliders.Length; j++)
        {
            Collider2D claw = releaseHookColliders[j];
            if (ball != null && claw != null)
                Physics2D.IgnoreCollision(ball, claw, releaseOriginalIgnores[ballIndex, j]);
        }
        releasingBalls[ballIndex] = false;
    }

    private void RestoreReleaseCollisions()
    {
        if (!Application.isPlaying || releasingBalls == null)
            return;

        for (int i = 0; i < releasingBalls.Length; i++)
        {
            if (releasingBalls[i])
                RestoreReleaseCollision(i);
        }
    }

    #endregion

    #endregion

    #region Catch Detection

    private bool IsHookContactCollider(Collider2D other)
    {
        if (other == null || other == catchArea || !other.gameObject.activeInHierarchy)
            return false;

        if (!IsCatchAreaTriggerTag(other))
            return false;

        if (catchRoot == null)
            return true;

        Transform otherTransform = other.transform;
        return otherTransform != catchRoot && !otherTransform.IsChildOf(catchRoot);
    }

    private bool IsCatchAreaTriggerTag(Collider2D other)
    {
        return HasTag(other, LockTag) || HasTag(other, ToolsTag) || HasTag(other, ActorTag);
    }

    #endregion

    #region Resolve & Utility

    private void ResolveReferences()
    {
        if (levelCtrl == null)
            levelCtrl = GetComponent<Level70Ctrl>();

        if (catchAnim == null)
            catchAnim = transform.Find("l70/catch")?.GetComponent<Animator>() ?? transform.Find("catch")?.GetComponent<Animator>();

        if (catchRoot == null && catchAnim != null)
            catchRoot = catchAnim.transform;

        if (catchArea == null && catchRoot != null)
            catchArea = catchRoot.Find("catchArea")?.GetComponent<Collider2D>();

        if (dropPoint == null)
            dropPoint = transform.Find("dropPoint") ?? transform.Find("l70/dropPoint");

        if (crashEffectLeft == null)
            crashEffectLeft = transform.Find("l70/CatchFailCrashEffect/CrashEffect_Left")?.GetComponent<SpriteRenderer>();

        if (crashEffectRight == null)
            crashEffectRight = transform.Find("l70/CatchFailCrashEffect/CrashEffect_Right")?.GetComponent<SpriteRenderer>();
    }

    private void EnsureCatchContactFilter()
    {
        if (hasCatchContactFilter)
            return;

        catchContactFilter = new ContactFilter2D
        {
            useTriggers = true,
            useLayerMask = false
        };
        hasCatchContactFilter = true;
    }

    private float GetRangeMinX()
    {
        float rawMin = GetActiveRangeMinX();
        float rawMax = movementRangeMax != null ? movementRangeMax.position.x : fallbackMovementRangeMaxX;
        return Mathf.Min(rawMin, rawMax);
    }

    private float GetRangeMaxX()
    {
        float rawMin = GetActiveRangeMinX();
        float rawMax = movementRangeMax != null ? movementRangeMax.position.x : fallbackMovementRangeMaxX;
        return Mathf.Max(rawMin, rawMax);
    }

    private float GetActiveRangeMinX()
    {
        if (!hasBrokenLeftWall)
            return preCrashMovementRangeMin != null ? preCrashMovementRangeMin.position.x : fallbackPreCrashMovementRangeMinX;

        return movementRangeMin != null ? movementRangeMin.position.x : fallbackMovementRangeMinX;
    }

    private float GetLowerBoundaryY()
    {
        if (lowerBoundary != null)
            return lowerBoundary.position.y;

        return hasInitialHookPosition ? initialHookPosition.y - fallbackLowerDistance : transform.position.y - fallbackLowerDistance;
    }

    private float GetDropHeightY()
    {
        return dropPoint != null ? dropPoint.position.y : GetInitialHookY();
    }

    private float GetDropX()
    {
        return dropPoint != null ? dropPoint.position.x : catchRoot.position.x;
    }

    private float GetInitialHookY()
    {
        if (hasInitialHookPosition)
            return initialHookPosition.y;

        return catchRoot != null ? catchRoot.position.y : transform.position.y;
    }

    private bool HasTag(Collider2D target, string tagName)
    {
        return target != null && target.gameObject != null && target.gameObject.tag == tagName;
    }

    #endregion

    #region Test Buttons

    [Button("测试抓钩开合")]
    private void SwitchAnimState()
    {
        SetHookOpen(!isOpen);
    }

    #endregion
}
