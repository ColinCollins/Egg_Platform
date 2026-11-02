using System.Collections;
using Bear.EventSystem;
using DG.Tweening;
using Game.Events;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level107 入口门坠落杠杆：管理门的拖放、杠杆反馈以及角色成功或失败抛射。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level107DoorLever : MonoBehaviour
{
    private const string LeverImpactSfxTag = "l93GearRotate";
    private const string PlayerLaunchSfxTag = "l93PlayerLaunch";

    #region Inspector 配置

    [Header("引用")]
    /// <summary>
    /// 当前关卡基础控制器，用于取得角色并归口成功流程。
    /// </summary>
    [SerializeField] private BaseLevelCtrl levelCtrl;

    /// <summary>
    /// 入口门拖拽组件；Prefab 使用基于公共 DragableItem 的本关命中适配器。
    /// </summary>
    [SerializeField] private DragableItem dragableItem;

    /// <summary>
    /// 入口门专用刚体；拖拽时为 Kinematic，松手后切换为 Dynamic 下落。
    /// </summary>
    [SerializeField] private Rigidbody2D doorRigidbody;

    /// <summary>
    /// 入口门新增的实体碰撞体；公共 StartDoor 原 Trigger 继续保留。
    /// </summary>
    [SerializeField] private BoxCollider2D physicalCollider;

    /// <summary>
    /// 被门砸中后产生杠杆反馈的横向平台。
    /// </summary>
    [SerializeField] private Transform rotatingPlatform;

    /// <summary>
    /// 杠杆平台的实体碰撞体，用于站位、边缘和门落点判定。
    /// </summary>
    [SerializeField] private Collider2D platformCollider;

    /// <summary>
    /// 左侧边缘正确解法对应的成功飞行终点。
    /// </summary>
    [SerializeField] private Transform endPoint;

    /// <summary>
    /// 关卡现有的角色跟随区域；抛射期间暂时停用，结束后恢复普通跟随。
    /// </summary>
    [SerializeField] private CameraFollowArea cameraFollowArea;

    /// <summary>
    /// Level107 的玩法相机；抛射期间由本脚本直接同步角色的世界 X/Y。
    /// </summary>
    [SerializeField] private Camera launchCamera;

    [Header("门拖放")]
    /// <summary>
    /// 入口门允许拖拽的世界坐标区域中心。
    /// </summary>
    [SerializeField] private Vector2 dragAreaCenter = new Vector2(0f, 1.5f);

    /// <summary>
    /// 入口门允许拖拽的世界坐标区域大小。
    /// </summary>
    [SerializeField] private Vector2 dragAreaSize = new Vector2(19f, 10f);

    /// <summary>
    /// 入口门释放后的重力倍率。
    /// </summary>
    [SerializeField, Min(0f)] private float doorGravityScale = 1.8f;

    [Header("杠杆判定")]
    /// <summary>
    /// 角色碰撞体与平台顶面的最大站立距离。
    /// </summary>
    [SerializeField, Min(0.01f)] private float standingTolerance = 0.35f;

    /// <summary>
    /// 平台左右各多大比例视为边缘区；沿用 Level093 的 0.28。
    /// </summary>
    [SerializeField, Range(0.05f, 0.5f)] private float edgeRatio = 0.28f;

    /// <summary>
    /// 门砸下后杠杆向受力侧转动的角度；沿用 Level093 的四分之一圈投石机动作。
    /// </summary>
    [SerializeField, Min(0f)] private float leverKickDegrees = 90f;

    /// <summary>
    /// 杠杆从水平位置转到受力位置的时间。
    /// </summary>
    [SerializeField, Min(0.01f)] private float leverKickDuration = 0.2f;

    /// <summary>
    /// 平台转过本次目标角度的该比例后释放角色；沿用 Level093 的 0.48。
    /// </summary>
    [SerializeField, Range(0.1f, 1f)] private float releaseRotationRatio = 0.48f;

    [Header("角色抛射")]
    /// <summary>
    /// 正确解法从杠杆释放到 EndPoint 的飞行时长。
    /// </summary>
    [SerializeField, Min(0.01f)] private float endpointFlightDuration = 2.45f;

    /// <summary>
    /// 正确解法显式抛物线的弧高，沿用 Level093。
    /// </summary>
    [SerializeField, Min(0f)] private float endpointFlightJumpPower = 20f;

    /// <summary>
    /// 任一边缘条件不足时的小幅抛射力度，沿用 Level093。
    /// </summary>
    [SerializeField, Min(0f)] private float minorLaunchForce = 12f;

    /// <summary>
    /// 反向两端均满足时向左飞出画面的抛射力度。
    /// </summary>
    [SerializeField, Min(0f)] private float reverseFailureLaunchForce = 28f;

    /// <summary>
    /// 普通抛射方向模板；X 会按角色所在侧翻转，沿用 Level093 的较小横向分量。
    /// </summary>
    [SerializeField] private Vector2 minorLaunchDirection = new Vector2(0.35f, 1f);

    /// <summary>
    /// 反向错误解法的飞出方向模板；代码固定把 X 翻向左侧。
    /// </summary>
    [SerializeField] private Vector2 reverseFailureDirection = new Vector2(1f, 1f);

    /// <summary>
    /// 物理抛射后暂时禁止角色操作的最长时间；落地会提前恢复。
    /// </summary>
    [SerializeField, Min(0f)] private float controlLockSeconds = 1.2f;

    [Header("抛射相机")]
    /// <summary>
    /// 未抛射时沿用的关卡相机跟随区域中心。
    /// </summary>
    [SerializeField] private Vector2 restingFollowAreaCenter = new Vector2(-3.05f, 0f);

    /// <summary>
    /// 未抛射时沿用的关卡相机跟随区域大小。
    /// </summary>
    [SerializeField] private Vector2 restingFollowAreaSize = new Vector2(15.1f, 12f);

    /// <summary>
    /// GameCamera 引用缺失时，共享跟随器兜底使用的极小死区。
    /// </summary>
    [SerializeField, Min(0f)] private float launchFollowDeadZone = 0.1f;

    /// <summary>
    /// 本关二维抛射跟镜的平滑时间；与原 CameraFollowArea authored 值一致。
    /// </summary>
    [SerializeField, Min(0f)] private float launchCameraSmoothTime = 0.2f;

    #endregion

    #region 运行时状态

    private ActorCtrl actor;
    private Collider2D actorCollider;
    private Rigidbody2D actorBody;
    private EventSubscriber eventSubscriber;
    private Sequence leverSequence;
    private Tween endpointFlightTween;
    private Coroutine restoreControlCoroutine;
    private Coroutine resumeLockedActorCoroutine;
    private Vector3 leverRestingEulerAngles;
    private Vector3 carriedActorLocalPosition;
    private Quaternion carriedActorWorldRotation;
    private Vector2 cachedDoorVelocity;
    private Vector2 cachedActorVelocity;
    private Vector3 launchCameraVelocity;
    private bool gameplayStarted;
    private bool isGameplayActive = true;
    private bool isDragging;
    private bool isWaitingForLanding;
    private bool releaseWasAboveLever;
    private bool isLeverReacting;
    private bool isCarryingActor;
    private bool actorLockedByLever;
    private bool actorWasCtrlable;
    private bool actorBodyWasSimulated;
    private bool doorFallPaused;
    private bool actorLaunchPaused;
    private bool hasSucceeded;
    private bool hasLoggedMissingReferences;
    private bool restoreCameraAfterPhysicalLaunch;
    private bool isDirectLaunchCameraFollowing;
    private bool cameraFollowAreaWasEnabled;
    private LaunchOutcome pendingOutcome;

    private enum LaunchOutcome
    {
        None,
        Minor,
        EndpointSuccess,
        ReverseFailure
    }

    #endregion

    #region Unity 生命周期

    private void Awake()
    {
        ResolveReferences();
        leverRestingEulerAngles = rotatingPlatform != null
            ? rotatingPlatform.localEulerAngles
            : Vector3.zero;
        PrepareForIntro();
    }

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
        SetGameplayActive(PlayCtrl.Instance != null && PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING));
    }

    private void Update()
    {
        ResolveReferences();

        bool shouldEnableDrag = CanEnableGameplayDrag();
        if (shouldEnableDrag && !gameplayStarted)
        {
            gameplayStarted = true;
            // Intro 阶段会关闭门的物理 Collider；首次进入可操作状态时立即恢复，
            // 让玩家从门的任意可见区域开始拖拽都能稳定命中。
            HoldDoorInPlace();
        }

        if (dragableItem != null && !isLeverReacting && !hasSucceeded)
        {
            dragableItem.SetEnableDrag(shouldEnableDrag);
        }
    }

    private void LateUpdate()
    {
        ApplyCarriedActorPose();
        UpdateLaunchCameraFollow();

        if (isGameplayActive
            && !actorLaunchPaused
            && restoreControlCoroutine != null
            && actorBody != null
            && actorBody.simulated
            && actorBody.linearVelocity.sqrMagnitude > 0.0001f)
        {
            cachedActorVelocity = actorBody.linearVelocity;
        }
    }

    private void FixedUpdate()
    {
        // 某些拖放帧里碰撞回调的法线方向会因接触对顺序而反转。
        // 门已经从杠杆上方释放且物理上正在接触杠杆时，直接补偿进入同一落地入口。
        if (!isGameplayActive
            || !isWaitingForLanding
            || !releaseWasAboveLever
            || physicalCollider == null
            || platformCollider == null
            || !physicalCollider.enabled
            || !platformCollider.enabled
            || !physicalCollider.IsTouching(platformCollider))
        {
            return;
        }

        CompleteDoorLandingOnLever();
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

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryHandleDoorLanding(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryHandleDoorLanding(collision);
    }

    #endregion

    #region 门拖放

    /// <summary>
    /// 由本关 StartDoor 上的 Level107DoorDragItem 开始拖拽事件调用；仅切换门的本地物理状态。
    /// </summary>
    public void BeginDoorDrag()
    {
        if (!CanEnableGameplayDrag()
            || doorRigidbody == null
            || physicalCollider == null)
        {
            return;
        }

        isDragging = true;
        isWaitingForLanding = false;
        releaseWasAboveLever = false;
        doorFallPaused = false;
        physicalCollider.enabled = false;
        doorRigidbody.simulated = true;
        doorRigidbody.bodyType = RigidbodyType2D.Kinematic;
        doorRigidbody.linearVelocity = Vector2.zero;
        doorRigidbody.angularVelocity = 0f;
    }

    /// <summary>
    /// 由本关 StartDoor 上的 Level107DoorDragItem 结束拖拽事件调用；松手后让门按重力下落。
    /// </summary>
    public void EndDoorDrag()
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;
        if (!CanEnableGameplayDrag()
            || isLeverReacting
            || hasSucceeded
            || doorRigidbody == null
            || physicalCollider == null)
        {
            HoldDoorInPlace();
            return;
        }

        releaseWasAboveLever = WasDoorReleasedAboveLever();
        isWaitingForLanding = true;
        physicalCollider.enabled = true;
        doorRigidbody.simulated = true;
        doorRigidbody.bodyType = RigidbodyType2D.Dynamic;
        doorRigidbody.gravityScale = Mathf.Max(0f, doorGravityScale);
        doorRigidbody.linearVelocity = Vector2.zero;
        doorRigidbody.angularVelocity = 0f;
    }

    private bool WasDoorReleasedAboveLever()
    {
        if (physicalCollider == null || platformCollider == null)
        {
            return false;
        }

        Bounds doorBounds = physicalCollider.bounds;
        Bounds platformBounds = platformCollider.bounds;
        bool overlapsHorizontally = doorBounds.max.x >= platformBounds.min.x
                                    && doorBounds.min.x <= platformBounds.max.x;
        float topTolerance = Mathf.Max(0.05f, Physics2D.defaultContactOffset * 4f);
        return overlapsHorizontally
               && doorBounds.center.y >= platformBounds.max.y - topTolerance;
    }

    private void TryHandleDoorLanding(Collision2D collision)
    {
        if (!isGameplayActive || !isWaitingForLanding || collision == null)
        {
            return;
        }

        if (IsLeverCollision(collision))
        {
            CompleteDoorLandingOnLever();
            return;
        }

        if (!HasSupportingContact(collision))
        {
            return;
        }

        isWaitingForLanding = false;
        doorFallPaused = false;
        HoldDoorInPlace();
    }

    private void CompleteDoorLandingOnLever()
    {
        if (!isWaitingForLanding)
        {
            return;
        }

        isWaitingForLanding = false;
        doorFallPaused = false;
        ResolveReferences();

        if (actor == null || actor.IsDied || levelCtrl == null || levelCtrl.IsSuccess())
        {
            HoldDoorInPlace();
            return;
        }

        BeginLeverReaction();
    }

    private static bool HasSupportingContact(Collision2D collision)
    {
        for (int index = 0; index < collision.contactCount; index++)
        {
            if (Mathf.Abs(collision.GetContact(index).normal.y) > 0.25f)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsLeverCollision(Collision2D collision)
    {
        if (platformCollider == null || rotatingPlatform == null)
        {
            return false;
        }

        Collider2D other = collision.otherCollider;
        if (other == physicalCollider)
        {
            other = collision.collider;
        }

        return other == platformCollider
               || (other != null
                   && (other.transform == rotatingPlatform
                       || other.transform.IsChildOf(rotatingPlatform)));
    }

    #endregion

    #region 杠杆判定与反馈

    private void BeginLeverReaction()
    {
        if (isLeverReacting || rotatingPlatform == null)
        {
            HoldDoorInPlace();
            return;
        }

        isLeverReacting = true;
        dragableItem?.SetEnableDrag(false);
        KeepDoorDynamicAfterImpact();

        bool actorStanding = IsActorStandingOnLever();
        pendingOutcome = EvaluateOutcome(actorStanding);
        if (actorStanding)
        {
            LockActorToLever();
        }

        Bounds platformBounds = platformCollider.bounds;
        float doorSide = transform.position.x >= platformBounds.center.x ? 1f : -1f;
        float kickDuration = Mathf.Max(0.01f, leverKickDuration);
        float releaseTime = kickDuration * Mathf.Clamp(releaseRotationRatio, 0.1f, 1f);
        Vector3 localRotationDelta = new Vector3(0f, 0f, -doorSide * Mathf.Abs(leverKickDegrees));

        AudioManager.PlaySound(LeverImpactSfxTag);
        leverSequence?.Kill();
        leverSequence = DOTween.Sequence()
            .SetLink(gameObject)
            .Append(rotatingPlatform.DOLocalRotate(
                localRotationDelta,
                kickDuration,
                RotateMode.LocalAxisAdd).SetEase(Ease.Linear))
            .InsertCallback(releaseTime, ReleaseActorDuringLeverSwing)
            .OnComplete(CompleteLeverReaction);
    }

    private LaunchOutcome EvaluateOutcome(bool actorStanding)
    {
        if (!releaseWasAboveLever || !actorStanding)
        {
            return LaunchOutcome.None;
        }

        bool actorNearLeft = IsNearLeftEdge(actorCollider.bounds.center.x);
        bool actorNearRight = IsNearRightEdge(actorCollider.bounds.center.x);
        float doorCenterX = physicalCollider != null
            ? physicalCollider.bounds.center.x
            : transform.position.x;
        bool doorNearLeft = IsNearLeftEdge(doorCenterX);
        bool doorNearRight = IsNearRightEdge(doorCenterX);

        if (actorNearLeft && doorNearRight)
        {
            return LaunchOutcome.EndpointSuccess;
        }

        if (actorNearRight && doorNearLeft)
        {
            return LaunchOutcome.ReverseFailure;
        }

        return LaunchOutcome.Minor;
    }

    private bool IsActorStandingOnLever()
    {
        if (actor == null
            || actorCollider == null
            || actorBody == null
            || platformCollider == null
            || actor.IsDied
            || actor.IsKnocked
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
        return overlapsHorizontally
               && actorBounds.center.y >= platformBounds.center.y
               && verticalGap >= -standingTolerance
               && verticalGap <= standingTolerance;
    }

    private bool IsNearLeftEdge(float worldX)
    {
        Bounds bounds = platformCollider.bounds;
        float boundary = bounds.min.x + bounds.size.x * Mathf.Clamp(edgeRatio, 0.05f, 0.5f);
        return worldX <= boundary;
    }

    private bool IsNearRightEdge(float worldX)
    {
        Bounds bounds = platformCollider.bounds;
        float boundary = bounds.max.x - bounds.size.x * Mathf.Clamp(edgeRatio, 0.05f, 0.5f);
        return worldX >= boundary;
    }

    private void ReleaseActorDuringLeverSwing()
    {
        switch (pendingOutcome)
        {
            case LaunchOutcome.EndpointSuccess:
                BeginEndpointFlight();
                break;
            case LaunchOutcome.ReverseFailure:
                BeginPhysicalLaunch(true);
                break;
            case LaunchOutcome.Minor:
                BeginPhysicalLaunch(false);
                break;
            default:
                DetachActorFromLever(true);
                RestoreActorControl();
                break;
        }
    }

    private void CompleteLeverReaction()
    {
        leverSequence = null;
        isLeverReacting = false;
        if (!hasSucceeded && dragableItem != null)
        {
            dragableItem.SetEnableDrag(CanEnableGameplayDrag());
        }
    }

    #endregion

    #region 角色抛射

    private void LockActorToLever()
    {
        if (actorLockedByLever || actor == null || levelCtrl == null)
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

    private void BeginEndpointFlight()
    {
        if (actor == null || endPoint == null)
        {
            Debug.LogWarning("[Level107DoorLever] 缺少 Actor 或 EndPoint，正确边缘组合无法执行成功飞行。", this);
            DetachActorFromLever(true);
            RestoreActorControl();
            return;
        }

        DetachActorFromLever(false);
        hasSucceeded = true;
        dragableItem?.SetEnableDrag(false);
        BeginLaunchCameraFollow();

        Vector3 startPosition = actor.transform.position;
        Vector3 targetPosition = endPoint.position;
        targetPosition.z = actor.transform.position.z;
        float jumpHeight = Mathf.Max(0f, endpointFlightJumpPower);

        endpointFlightTween?.Kill();
        endpointFlightTween = DOVirtual.Float(
                0f,
                1f,
                Mathf.Max(0.01f, endpointFlightDuration),
                progress =>
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
    }

    private void CompleteEndpointFlight()
    {
        endpointFlightTween = null;
        if (actor != null && endPoint != null)
        {
            Vector3 targetPosition = endPoint.position;
            targetPosition.z = actor.transform.position.z;
            actor.transform.position = targetPosition;
        }

        levelCtrl?.SuccessStrightly();
    }

    private void BeginPhysicalLaunch(bool reverseFailure)
    {
        DetachActorFromLever(true);
        if (actor == null || actorBody == null || actor.IsDied)
        {
            RestoreActorControl();
            return;
        }

        float horizontalSign;
        if (reverseFailure)
        {
            horizontalSign = -1f;
        }
        else
        {
            float actorOffset = actorCollider.bounds.center.x - platformCollider.bounds.center.x;
            horizontalSign = actorOffset <= 0f ? 1f : -1f;
        }

        Vector2 directionTemplate = reverseFailure
            ? reverseFailureDirection
            : minorLaunchDirection;
        Vector2 direction = new Vector2(
            Mathf.Abs(directionTemplate.x) * horizontalSign,
            Mathf.Abs(directionTemplate.y));
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = new Vector2(horizontalSign, 1f);
        }

        float force = reverseFailure ? reverseFailureLaunchForce : minorLaunchForce;
        restoreCameraAfterPhysicalLaunch = !reverseFailure;
        BeginLaunchCameraFollow();
        actor.StopMoving();
        actorBody.linearVelocity = direction.normalized * Mathf.Max(0f, force);
        cachedActorVelocity = actorBody.linearVelocity;
        actorLaunchPaused = false;
        AudioManager.PlaySound(PlayerLaunchSfxTag);

        if (restoreControlCoroutine != null)
        {
            StopCoroutine(restoreControlCoroutine);
        }

        restoreControlCoroutine = StartCoroutine(RestoreControlAfterPhysicalLaunch());
    }

    private IEnumerator RestoreControlAfterPhysicalLaunch()
    {
        float elapsed = 0f;
        float timeout = Mathf.Max(0f, controlLockSeconds);
        bool becameAirborne = false;

        while (elapsed < timeout && actor != null && !actor.IsDied)
        {
            if (!isGameplayActive || actorLaunchPaused)
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
        if (restoreCameraAfterPhysicalLaunch && actor != null && !actor.IsDied)
        {
            RestoreCameraFollowArea();
        }

        restoreCameraAfterPhysicalLaunch = false;
    }

    #endregion

    #region 状态、引用与清理

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
            leverSequence?.Play();
            endpointFlightTween?.Play();
            ResumeDoorFall();
            if (actorLockedByLever && resumeLockedActorCoroutine == null)
            {
                resumeLockedActorCoroutine = StartCoroutine(ResumeLockedActorAfterGameResume());
            }
        }
        else
        {
            dragableItem?.SetEnableDrag(false);
            leverSequence?.Pause();
            endpointFlightTween?.Pause();
            PauseDoorFall();
            PausePhysicalActorLaunch();
        }
    }

    private void PauseDoorFall()
    {
        if (doorFallPaused
            || !isWaitingForLanding
            || doorRigidbody == null
            || !doorRigidbody.simulated)
        {
            return;
        }

        cachedDoorVelocity = doorRigidbody.linearVelocity;
        doorRigidbody.simulated = false;
        doorFallPaused = true;
    }

    private void ResumeDoorFall()
    {
        if (!doorFallPaused || !isWaitingForLanding || doorRigidbody == null)
        {
            return;
        }

        doorRigidbody.simulated = true;
        doorRigidbody.bodyType = RigidbodyType2D.Dynamic;
        doorRigidbody.linearVelocity = cachedDoorVelocity;
        doorFallPaused = false;
    }

    private void PausePhysicalActorLaunch()
    {
        if (actorLaunchPaused
            || restoreControlCoroutine == null
            || actorBody == null
            || !actorBody.simulated)
        {
            return;
        }

        cachedActorVelocity = actorBody.linearVelocity;
        actorBody.simulated = false;
        actorLaunchPaused = true;
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
        if (actorLaunchPaused && actorBody != null)
        {
            actorBody.simulated = actorBodyWasSimulated;
            if (!actor.IsDied && actorBodyWasSimulated)
            {
                actorBody.linearVelocity = cachedActorVelocity;
            }

            actorLaunchPaused = false;
        }
    }

    private bool CanEnableGameplayDrag()
    {
        if (!Application.isPlaying
            || !isGameplayActive
            || isLeverReacting
            || hasSucceeded
            || levelCtrl == null
            || actor == null
            || PlayCtrl.Instance == null)
        {
            return false;
        }

        LevelRuntimeState runtimeState = PlayCtrl.Instance.Level?.CurrentLevelState;
        return runtimeState != null
               && !runtimeState.IsAttemptTransitioning
               && !actor.IsDied
               && !levelCtrl.IsActorEnterDoor
               && levelCtrl.ActorCtrlable
               && !levelCtrl.IsSuccess()
               && PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING);
    }

    private bool ResolveReferences()
    {
        levelCtrl ??= GetComponentInParent<BaseLevelCtrl>();
        actor ??= levelCtrl != null ? levelCtrl.Actor : null;
        if (actor != null)
        {
            actorCollider ??= actor.GetComponent<Collider2D>();
            actorBody ??= actor.RB != null ? actor.RB : actor.GetComponent<Rigidbody2D>();
        }

        dragableItem ??= GetComponent<DragableItem>();
        cameraFollowArea ??= levelCtrl != null ? levelCtrl.GetComponent<CameraFollowArea>() : null;
        if (launchCamera == null && levelCtrl != null)
        {
            Transform cameraTransform = levelCtrl.transform.Find("GameCamera");
            launchCamera = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
        }

        launchCamera ??= Camera.main;
        doorRigidbody ??= GetComponent<Rigidbody2D>();
        if (physicalCollider == null)
        {
            BoxCollider2D[] colliders = GetComponents<BoxCollider2D>();
            for (int index = 0; index < colliders.Length; index++)
            {
                if (!colliders[index].isTrigger)
                {
                    physicalCollider = colliders[index];
                    break;
                }
            }
        }

        if (platformCollider == null && rotatingPlatform != null)
        {
            // Level107 的杠杆是嵌套 Prefab 实例；运行时根节点 GetComponent 在该实例上未能解析到
            // 源 Prefab 的 Collider，因此保留包含自身的子层级查找作为序列化引用之外的兜底。
            platformCollider = rotatingPlatform.GetComponentInChildren<Collider2D>(true);
        }

        bool isValid = levelCtrl != null
                       && actor != null
                       && actorCollider != null
                       && actorBody != null
                       && dragableItem != null
                       && doorRigidbody != null
                       && physicalCollider != null
                       && rotatingPlatform != null
                       && platformCollider != null
                       && endPoint != null;
        if (!isValid
            && Application.isPlaying
            && gameplayStarted
            && !hasLoggedMissingReferences)
        {
            hasLoggedMissingReferences = true;
            Debug.LogWarning(
                $"[Level107DoorLever] 引用不完整：Level={levelCtrl != null}, Actor={actor != null}, "
                + $"ActorCollider={actorCollider != null}, ActorBody={actorBody != null}, Drag={dragableItem != null}, "
                + $"DoorBody={doorRigidbody != null}, DoorCollider={physicalCollider != null}, "
                + $"Lever={rotatingPlatform != null}, LeverCollider={platformCollider != null}, EndPoint={endPoint != null}。",
                this);
        }

        return isValid;
    }

    private void PrepareForIntro()
    {
        gameplayStarted = false;
        isDragging = false;
        isWaitingForLanding = false;
        releaseWasAboveLever = false;
        dragableItem?.SetDragArea(dragAreaCenter, dragAreaSize);
        dragableItem?.SetEnableDrag(false);
        if (doorRigidbody != null)
        {
            doorRigidbody.bodyType = RigidbodyType2D.Kinematic;
            doorRigidbody.linearVelocity = Vector2.zero;
            doorRigidbody.angularVelocity = 0f;
            doorRigidbody.simulated = false;
        }

        if (physicalCollider != null)
        {
            physicalCollider.enabled = false;
        }
    }

    private void KeepDoorDynamicAfterImpact()
    {
        isDragging = false;
        isWaitingForLanding = false;
        doorFallPaused = false;
        if (doorRigidbody != null)
        {
            // 保留撞击瞬间的线速度，让门在平台倾斜后继续自然滑落。
            doorRigidbody.bodyType = RigidbodyType2D.Dynamic;
            doorRigidbody.gravityScale = Mathf.Max(0f, doorGravityScale);
            doorRigidbody.simulated = true;
        }

        if (physicalCollider != null)
        {
            physicalCollider.enabled = true;
        }
    }

    private void HoldDoorInPlace()
    {
        isDragging = false;
        isWaitingForLanding = false;
        releaseWasAboveLever = false;
        doorFallPaused = false;
        if (doorRigidbody != null)
        {
            doorRigidbody.bodyType = RigidbodyType2D.Kinematic;
            doorRigidbody.linearVelocity = Vector2.zero;
            doorRigidbody.angularVelocity = 0f;
            doorRigidbody.simulated = gameplayStarted && !hasSucceeded;
        }

        if (physicalCollider != null)
        {
            physicalCollider.enabled = gameplayStarted && !hasSucceeded;
        }
    }

    private void BeginLaunchCameraFollow()
    {
        if (actor == null)
        {
            return;
        }

        if (launchCamera != null)
        {
            if (!isDirectLaunchCameraFollowing)
            {
                cameraFollowAreaWasEnabled = cameraFollowArea != null && cameraFollowArea.enabled;
                if (cameraFollowArea != null)
                {
                    // 避免共享 LateUpdate 在同一帧把本关写入的 Y 位置覆盖回去。
                    cameraFollowArea.enabled = false;
                }

                launchCameraVelocity = Vector3.zero;
                isDirectLaunchCameraFollowing = true;
            }

            UpdateLaunchCameraFollow();
            return;
        }

        // 相机引用异常时保留旧跟随器兜底，不中断抛射和过关流程。
        if (cameraFollowArea == null)
        {
            return;
        }

        float deadZone = Mathf.Max(0f, launchFollowDeadZone);
        cameraFollowArea.SetFollowArea(actor.transform.position, new Vector2(deadZone, deadZone));
        cameraFollowArea.SetCanFollow(true);
    }

    private void UpdateLaunchCameraFollow()
    {
        if (!isDirectLaunchCameraFollowing || launchCamera == null || actor == null)
        {
            return;
        }

        Transform cameraTransform = launchCamera.transform;
        Vector3 targetPosition = new Vector3(
            actor.transform.position.x,
            actor.transform.position.y,
            cameraTransform.position.z);
        float smoothTime = Mathf.Max(0f, launchCameraSmoothTime);
        cameraTransform.position = smoothTime > 0f
            ? Vector3.SmoothDamp(
                cameraTransform.position,
                targetPosition,
                ref launchCameraVelocity,
                smoothTime)
            : targetPosition;
    }

    private void RestoreCameraFollowArea()
    {
        bool wasDirectFollowing = isDirectLaunchCameraFollowing;
        isDirectLaunchCameraFollowing = false;
        launchCameraVelocity = Vector3.zero;
        if (cameraFollowArea == null)
        {
            return;
        }

        cameraFollowArea.SetFollowArea(restingFollowAreaCenter, restingFollowAreaSize);
        if (wasDirectFollowing)
        {
            cameraFollowArea.enabled = cameraFollowAreaWasEnabled;
        }
    }

    private void ApplyCarriedActorPose()
    {
        if (isCarryingActor && actor != null && rotatingPlatform != null)
        {
            actor.transform.position = rotatingPlatform.TransformPoint(carriedActorLocalPosition);
            actor.transform.rotation = carriedActorWorldRotation;
        }
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

    private void RestoreActorControl(bool restoreOwnershipWhileInactive = false)
    {
        if (!actorLockedByLever || hasSucceeded)
        {
            return;
        }

        actorLockedByLever = false;
        actorLaunchPaused = false;
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
        leverSequence?.Kill();
        leverSequence = null;
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

        isLeverReacting = false;
        isWaitingForLanding = false;
        doorFallPaused = false;
        restoreCameraAfterPhysicalLaunch = false;
        pendingOutcome = LaunchOutcome.None;
        if (rotatingPlatform != null)
        {
            rotatingPlatform.localEulerAngles = leverRestingEulerAngles;
        }

        DetachActorFromLever(true);
        RestoreActorControl(true);
        RestoreCameraFollowArea();
        HoldDoorInPlace();
    }

    #endregion
}
