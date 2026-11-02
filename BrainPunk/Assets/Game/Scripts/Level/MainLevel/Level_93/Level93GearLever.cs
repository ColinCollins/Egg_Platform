using System.Collections;
using Bear.EventSystem;
using DG.Tweening;
using Game.Events;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level093 齿轮杠杆控制器：点击后顺时针旋转平台，并按站位决定终点飞行或普通抛射。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level93GearLever : MonoBehaviour
{
    private const string GearRotateSfxTag = "l93GearRotate";
    private const string PlayerLaunchSfxTag = "l93PlayerLaunch";

    #region Inspector 配置

    /// <summary>
    /// 点击齿轮时整体旋转的平台根节点，必须包含可站立的 Collider2D。
    /// </summary>
    [SerializeField] private Transform rotatingPlatform;

    /// <summary>
    /// 成功抛射的目标点；满足横向平台左侧边缘条件后角色会准确到达此点。
    /// </summary>
    [SerializeField] private Transform endPoint;

    /// <summary>
    /// 齿轮点击命中区域；为空时读取当前物体上的 Collider2D。
    /// </summary>
    [SerializeField] private Collider2D gearHitArea;

    /// <summary>
    /// 平台可站立碰撞体；为空时从 rotatingPlatform 根节点读取。
    /// </summary>
    [SerializeField] private Collider2D platformCollider;

    /// <summary>
    /// 每次点击的顺时针旋转角度，按绝对值使用。
    /// </summary>
    [SerializeField, Min(0f)] private float clockwiseRotationDegrees = 90f;

    /// <summary>
    /// 杠杆完成一次旋转所需时间，单位为秒。
    /// </summary>
    [SerializeField, Min(0.01f)] private float rotationDuration = 0.2f;

    /// <summary>
    /// 杠杆旋转缓动，仅影响旋转表现。
    /// </summary>
    [SerializeField] private Ease rotationEase = Ease.Linear;

    /// <summary>
    /// 杠杆转过本次目标角度的该比例后释放角色，让旋转和抛射保持连续。
    /// </summary>
    [SerializeField, Range(0.1f, 1f)] private float releaseRotationRatio = 0.48f;

    /// <summary>
    /// 判断平台为横向时允许的角度误差。
    /// </summary>
    [SerializeField, Range(0f, 45f)] private float horizontalAngleTolerance = 12f;

    /// <summary>
    /// 角色碰撞体与平台顶面的最大站立距离。
    /// </summary>
    [SerializeField, Min(0.01f)] private float standingTolerance = 0.35f;

    /// <summary>
    /// 平台左侧多大比例算作成功边缘区，0.28 表示左侧 28%。
    /// </summary>
    [SerializeField, Range(0.05f, 0.5f)] private float leftEdgeRatio = 0.28f;

    /// <summary>
    /// 成功时从杠杆释放到 EndPoint 的飞行时长，单位为秒。
    /// </summary>
    [SerializeField, Min(0.01f)] private float endpointFlightDuration = 2.45f;

    /// <summary>
    /// 成功飞行的抛物线高度，数值越大飞行最高点越高。
    /// </summary>
    [SerializeField, Min(0f)] private float endpointFlightJumpPower = 20f;

    /// <summary>
    /// 未达到边缘条件时的小幅抛射力度。
    /// </summary>
    [SerializeField, Min(0f)] private float minorLaunchForce = 12f;

    /// <summary>
    /// 普通抛射方向模板；X 会根据角色位于支点哪一侧自动翻转，Y 建议保持正数。
    /// </summary>
    [SerializeField] private Vector2 minorLaunchDirection = new Vector2(0.35f, 1f);

    /// <summary>
    /// 普通抛射时，杠杆切线方向向上抛模板混合的比例，用于避免释放角度过低。
    /// </summary>
    [SerializeField, Range(0f, 1f)] private float minorLaunchDirectionBlend = 0.4f;

    /// <summary>
    /// 普通抛射后暂时禁止角色操作的时间，单位为秒。
    /// </summary>
    [SerializeField, Min(0f)] private float minorControlLockSeconds = 0.8f;

    #endregion

    #region 运行时状态

    private BaseLevelCtrl levelCtrl;
    private ActorCtrl actor;
    private Collider2D actorCollider;
    private Rigidbody2D actorBody;
    private Camera inputCamera;
    private Vector3 carriedActorLocalPosition;
    private Quaternion carriedActorWorldRotation;
    private Tween rotationTween;
    private Tween endpointFlightTween;
    private Coroutine restoreControlCoroutine;
    private Coroutine resumeLockedActorCoroutine;
    private EventSubscriber eventSubscriber;
    private Vector2 cachedMinorLaunchVelocity;
    private bool actorWasCtrlable;
    private bool actorBodyWasSimulated;
    private bool minorLaunchWasSimulated;
    private bool isCarryingActor;
    private bool actorLockedByLever;
    private bool minorLaunchPaused;
    private bool isRotating;
    private bool isEndpointFlight;
    private bool hasSucceeded;
    private bool pendingActorStanding;
    private bool pendingEndpointFlight;
    private bool actorReleasedFromLever;
    private float rotationStartZ;
    private bool hasLoggedMissingReferences;
    private bool isGameplayActive = true;

    #endregion

    #region Unity 生命周期

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        EventsUtils.ResetEvents(ref eventSubscriber);
        eventSubscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
        eventSubscriber.Subscribe<GamePauseEvent>(OnGamePause);
        eventSubscriber.Subscribe<GameResumeEvent>(OnGameResume);
        SetGameplayActive(PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING));
    }

    private void Start()
    {
        ResolveRuntimeReferences();
    }

    private void Update()
    {
        if (isRotating
            || isEndpointFlight
            || actorLockedByLever
            || hasSucceeded
            || !isGameplayActive
            || Time.timeScale <= 0f
            || !ResolveRuntimeReferences()
            || actor.IsDied
            || levelCtrl.IsSuccess())
        {
            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Began && TryHandleClick(touch.position))
            {
                return;
            }
        }

        if (Input.touchCount == 0 && Input.GetMouseButtonDown(0))
        {
            TryHandleClick(Input.mousePosition);
        }
    }

    private void LateUpdate()
    {
        ApplyCarriedActorPose();

        if (isGameplayActive
            && !minorLaunchPaused
            && restoreControlCoroutine != null
            && actorBody != null
            && actorBody.simulated
            && actorBody.linearVelocity.sqrMagnitude > 0.0001f)
        {
            cachedMinorLaunchVelocity = actorBody.linearVelocity;
        }
    }

    private void OnDisable()
    {
        EventsUtils.ResetEvents(ref eventSubscriber);
        CleanupInterruptedSequence();
    }

    private void OnDestroy()
    {
        EventsUtils.ResetEvents(ref eventSubscriber);
        CleanupInterruptedSequence();
    }

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        SetGameplayActive(evt.NewState.Equals(GamePlayStateName.PLAYING));
    }

    private void OnGamePause(GamePauseEvent evt)
    {
        SetGameplayActive(false);
    }

    private void OnGameResume(GameResumeEvent evt)
    {
        SetGameplayActive(true);
    }

    private void SetGameplayActive(bool isActive)
    {
        isGameplayActive = isActive;

        if (isGameplayActive)
        {
            rotationTween?.Play();
            endpointFlightTween?.Play();

            if (actorLockedByLever && resumeLockedActorCoroutine == null)
            {
                resumeLockedActorCoroutine = StartCoroutine(ResumeLockedActorAfterGameResume());
            }
        }
        else
        {
            rotationTween?.Pause();
            endpointFlightTween?.Pause();
            PauseMinorLaunch();
        }
    }

    private void PauseMinorLaunch()
    {
        if (minorLaunchPaused || restoreControlCoroutine == null || actorBody == null)
        {
            return;
        }

        minorLaunchWasSimulated = actorBody.simulated;
        if (actorBody.linearVelocity.sqrMagnitude > 0.0001f)
        {
            cachedMinorLaunchVelocity = actorBody.linearVelocity;
        }

        actorBody.simulated = false;
        minorLaunchPaused = true;
    }

    private IEnumerator ResumeLockedActorAfterGameResume()
    {
        yield return null;
        resumeLockedActorCoroutine = null;

        if (!isGameplayActive || !actorLockedByLever || actor == null)
        {
            yield break;
        }

        actor.StopMoving();
        if (!minorLaunchPaused || actorBody == null)
        {
            yield break;
        }

        actorBody.simulated = minorLaunchWasSimulated;
        if (!actor.IsDied && minorLaunchWasSimulated)
        {
            actorBody.linearVelocity = cachedMinorLaunchVelocity;
        }

        minorLaunchPaused = false;
    }

    #endregion

    #region 点击与旋转

    private bool TryHandleClick(Vector2 screenPosition)
    {
        if (!TryGetWorldPoint(screenPosition, out Vector2 worldPoint)
            || gearHitArea == null
            || !gearHitArea.OverlapPoint(worldPoint))
        {
            return false;
        }

        pendingActorStanding = IsActorStandingOnPlatform();
        pendingEndpointFlight = pendingActorStanding && IsPlatformHorizontal() && IsActorNearLeftEdge();
        actorReleasedFromLever = false;

        if (pendingActorStanding)
        {
            LockActorToLever();
        }

        isRotating = true;
        AudioManager.PlaySound(GearRotateSfxTag);
        rotationStartZ = rotatingPlatform.localEulerAngles.z;
        rotationTween?.Kill();
        rotationTween = rotatingPlatform
            .DOLocalRotate(
                new Vector3(0f, 0f, -Mathf.Abs(clockwiseRotationDegrees)),
                Mathf.Max(0.01f, rotationDuration),
                RotateMode.LocalAxisAdd)
            .SetEase(rotationEase)
            .SetLink(gameObject)
            .OnUpdate(TryReleaseActorDuringRotation)
            .OnComplete(CompleteLeverRotation);

        return true;
    }

    private void TryReleaseActorDuringRotation()
    {
        if (actorReleasedFromLever || rotatingPlatform == null)
        {
            return;
        }

        float rotatedDegrees = Mathf.Abs(Mathf.DeltaAngle(
            rotationStartZ,
            rotatingPlatform.localEulerAngles.z));
        float releaseDegrees = Mathf.Abs(clockwiseRotationDegrees)
                               * Mathf.Clamp(releaseRotationRatio, 0.1f, 1f);
        if (rotatedDegrees + 0.01f < releaseDegrees)
        {
            return;
        }

        ReleaseActorFromLever();
    }

    private void ReleaseActorFromLever()
    {
        if (actorReleasedFromLever)
        {
            return;
        }

        actorReleasedFromLever = true;
        ApplyCarriedActorPose();

        if (!pendingActorStanding)
        {
            return;
        }

        if (pendingEndpointFlight && BeginEndpointFlight())
        {
            return;
        }

        BeginMinorLaunch();
    }

    private void CompleteLeverRotation()
    {
        ReleaseActorFromLever();
        rotationTween = null;
        isRotating = false;
    }

    #endregion

    #region 站位判定

    private bool IsPlatformHorizontal()
    {
        Vector2 platformRight = rotatingPlatform.right;
        float horizontalDot = Mathf.Abs(Vector2.Dot(platformRight.normalized, Vector2.right));
        return horizontalDot >= Mathf.Cos(horizontalAngleTolerance * Mathf.Deg2Rad);
    }

    private bool IsActorStandingOnPlatform()
    {
        if (actor == null
            || actorCollider == null
            || actorBody == null
            || platformCollider == null
            || !actor.IsGround
            || !actorBody.simulated
            || actorBody.linearVelocity.y > 0.1f
            || !actorCollider.enabled
            || !platformCollider.enabled)
        {
            return false;
        }

        Bounds actorBounds = actorCollider.bounds;
        Bounds platformBounds = platformCollider.bounds;
        bool overlapsHorizontally = actorBounds.max.x >= platformBounds.min.x
                                    && actorBounds.min.x <= platformBounds.max.x;
        float verticalGap = actorBounds.min.y - platformBounds.max.y;
        bool actorIsAbovePlatform = actorBounds.center.y >= platformBounds.center.y;
        return overlapsHorizontally
               && actorIsAbovePlatform
               && verticalGap >= -standingTolerance
               && verticalGap <= standingTolerance;
    }

    private bool IsActorNearLeftEdge()
    {
        Bounds platformBounds = platformCollider.bounds;
        float edgeBoundary = platformBounds.min.x + platformBounds.size.x * Mathf.Clamp(leftEdgeRatio, 0.05f, 0.5f);
        return actorCollider.bounds.center.x <= edgeBoundary;
    }

    #endregion

    #region 抛射流程

    private void LockActorToLever()
    {
        if (actorLockedByLever)
        {
            return;
        }

        actorLockedByLever = true;
        actorWasCtrlable = levelCtrl.ActorCtrlable;
        levelCtrl.SetCtrlable(false);
        actor.SetMoveInput(0f);
        actor.StopMoving();

        carriedActorLocalPosition = rotatingPlatform.InverseTransformPoint(actor.transform.position);
        carriedActorWorldRotation = actor.transform.rotation;
        isCarryingActor = true;

        if (actorBody != null)
        {
            actorBodyWasSimulated = actorBody.simulated;
            actorBody.linearVelocity = Vector2.zero;
            actorBody.simulated = false;
        }
    }

    private bool BeginEndpointFlight()
    {
        if (endPoint == null || actor == null)
        {
            Debug.LogWarning("[Level93GearLever] EndPoint or Actor is missing; using the minor launch instead.", this);
            return false;
        }

        DetachActorFromLever(false);
        isEndpointFlight = true;
        Vector3 startPosition = actor.transform.position;
        Vector3 targetPosition = endPoint.position;
        targetPosition.z = actor.transform.position.z;
        float jumpHeight = Mathf.Max(0f, endpointFlightJumpPower);

        endpointFlightTween?.Kill();
        endpointFlightTween = DOVirtual
            .Float(0f, 1f, Mathf.Max(0.01f, endpointFlightDuration), progress =>
            {
                if (actor == null)
                {
                    return;
                }

                Vector3 position = Vector3.LerpUnclamped(startPosition, targetPosition, progress);
                position += Vector3.up * (4f * jumpHeight * progress * (1f - progress));
                actor.transform.position = position;
            })
            .SetEase(Ease.Linear)
            .SetLink(gameObject)
            .OnComplete(CompleteEndpointFlight);

        AudioManager.PlaySound(PlayerLaunchSfxTag);
        return true;
    }

    private void CompleteEndpointFlight()
    {
        endpointFlightTween = null;
        isEndpointFlight = false;
        hasSucceeded = true;

        if (actor != null && endPoint != null)
        {
            Vector3 targetPosition = endPoint.position;
            targetPosition.z = actor.transform.position.z;
            actor.transform.position = targetPosition;
        }

        levelCtrl?.SuccessStrightly();
    }

    private void BeginMinorLaunch()
    {
        DetachActorFromLever(true);

        if (actor == null || actorBody == null || actor.IsDied)
        {
            RestoreActorControl();
            return;
        }

        Vector2 radial = actor.transform.position - rotatingPlatform.position;
        Vector2 direction = new Vector2(radial.y, -radial.x);
        float side = Mathf.Sign(direction.x);
        if (Mathf.Approximately(side, 0f))
        {
            side = -1f;
        }

        Vector2 upwardDirection = new Vector2(
            Mathf.Abs(minorLaunchDirection.x) * side,
            Mathf.Abs(minorLaunchDirection.y));
        if (direction.sqrMagnitude <= 0.0001f || direction.y <= 0.05f)
        {
            direction = upwardDirection;
        }
        else if (upwardDirection.sqrMagnitude > 0.0001f)
        {
            direction = Vector2.Lerp(
                direction.normalized,
                upwardDirection.normalized,
                Mathf.Clamp01(minorLaunchDirectionBlend));
        }

        actor.StopMoving();
        actorBody.linearVelocity = direction.normalized * Mathf.Max(0f, minorLaunchForce);
        cachedMinorLaunchVelocity = actorBody.linearVelocity;
        minorLaunchPaused = false;
        AudioManager.PlaySound(PlayerLaunchSfxTag);

        if (restoreControlCoroutine != null)
        {
            StopCoroutine(restoreControlCoroutine);
        }

        restoreControlCoroutine = StartCoroutine(RestoreControlAfterMinorLaunch());
    }

    private IEnumerator RestoreControlAfterMinorLaunch()
    {
        float elapsed = 0f;
        float timeout = Mathf.Max(0f, minorControlLockSeconds);
        bool becameAirborne = false;

        while (elapsed < timeout && actor != null && !actor.IsDied)
        {
            if (!isGameplayActive || minorLaunchPaused)
            {
                yield return null;
                continue;
            }

            if (!actor.IsGround || (actorBody != null && actorBody.linearVelocity.y > 0.1f))
            {
                becameAirborne = true;
            }
            else if (becameAirborne)
            {
                break;
            }

            yield return null;
            elapsed += Time.deltaTime;
        }

        restoreControlCoroutine = null;
        RestoreActorControl();
    }

    #endregion

    #region 引用与清理

    private bool ResolveRuntimeReferences()
    {
        if (levelCtrl == null)
        {
            levelCtrl = GetComponentInParent<BaseLevelCtrl>();
        }

        if (actor == null && levelCtrl != null)
        {
            actor = levelCtrl.Actor;
        }

        if (actor != null)
        {
            actorCollider ??= actor.GetComponent<Collider2D>();
            actorBody ??= actor.RB != null ? actor.RB : actor.GetComponent<Rigidbody2D>();
        }

        if (gearHitArea == null)
        {
            gearHitArea = GetComponent<Collider2D>();
        }

        if (platformCollider == null && rotatingPlatform != null)
        {
            platformCollider = rotatingPlatform.GetComponent<Collider2D>();
        }

        if (inputCamera == null)
        {
            inputCamera = Camera.main;
            if (inputCamera == null)
            {
                inputCamera = FindFirstObjectByType<Camera>();
            }
        }

        bool isValid = levelCtrl != null
                       && actor != null
                       && actorCollider != null
                       && actorBody != null
                       && rotatingPlatform != null
                       && platformCollider != null
                       && gearHitArea != null
                       && inputCamera != null;

        if (!isValid && !hasLoggedMissingReferences)
        {
            hasLoggedMissingReferences = true;
            Debug.LogWarning("[Level93GearLever] Missing level, actor, collider, platform, gear hit area, or camera reference.", this);
        }

        return isValid;
    }

    private bool TryGetWorldPoint(Vector2 screenPosition, out Vector2 worldPoint)
    {
        if (inputCamera == null)
        {
            worldPoint = default;
            return false;
        }

        Ray ray = inputCamera.ScreenPointToRay(screenPosition);
        Plane clickPlane = new Plane(Vector3.forward, transform.position);
        if (!clickPlane.Raycast(ray, out float distance))
        {
            worldPoint = default;
            return false;
        }

        worldPoint = ray.GetPoint(distance);
        return true;
    }

    private void DetachActorFromLever(bool restoreSimulation)
    {
        ApplyCarriedActorPose();
        isCarryingActor = false;

        if (restoreSimulation && actorLockedByLever && actorBody != null)
        {
            actorBody.simulated = actorBodyWasSimulated;
            actorBody.linearVelocity = Vector2.zero;
        }
    }

    private void ApplyCarriedActorPose()
    {
        if (!isCarryingActor || actor == null || rotatingPlatform == null)
        {
            return;
        }

        actor.transform.position = rotatingPlatform.TransformPoint(carriedActorLocalPosition);
        actor.transform.rotation = carriedActorWorldRotation;
    }

    private void RestoreActorControl(bool restoreOwnershipWhileInactive = false)
    {
        if (!actorLockedByLever || hasSucceeded)
        {
            return;
        }

        actorLockedByLever = false;
        minorLaunchPaused = false;
        bool shouldRestoreControl = (isGameplayActive || restoreOwnershipWhileInactive)
                                    && levelCtrl != null
                                    && actor != null
                                    && !actor.IsDied
                                    && !levelCtrl.IsSuccess();
        if (!shouldRestoreControl)
        {
            return;
        }

        levelCtrl.SetCtrlable(actorWasCtrlable);

        if (isGameplayActive && actorWasCtrlable)
        {
            actor.SetMoveInput(0f);
            actor.ResumeMoving();
        }
    }

    private void CleanupInterruptedSequence()
    {
        rotationTween?.Kill();
        rotationTween = null;
        endpointFlightTween?.Kill();
        endpointFlightTween = null;

        if (restoreControlCoroutine != null)
        {
            StopCoroutine(restoreControlCoroutine);
            restoreControlCoroutine = null;
        }

        if (resumeLockedActorCoroutine != null)
        {
            StopCoroutine(resumeLockedActorCoroutine);
            resumeLockedActorCoroutine = null;
        }

        isRotating = false;
        isEndpointFlight = false;
        minorLaunchPaused = false;
        pendingActorStanding = false;
        pendingEndpointFlight = false;
        actorReleasedFromLever = false;
        DetachActorFromLever(true);
        RestoreActorControl(true);
    }

    #endregion
}
