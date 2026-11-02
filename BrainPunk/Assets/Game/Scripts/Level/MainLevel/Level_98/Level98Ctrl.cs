using Bear.EventSystem;
using Game.Events;
using Game.Level;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level098 私有关卡控制器：落到第五坑石像后升起通路；
/// 跳过第五坑并落到右岸时，改为把出口门拉离可通关位置。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level98Ctrl : MonoBehaviour
{
    private const string StatueRiseSfxTag = "l89StonePillarDrop";
    private const string DoorLiftSfxTag = "findItems";

    #region Inspector

    [Header("场景引用")]
    [Tooltip("本关现有角色控制器；只读取角色状态并在石像承托阶段调整竖直速度。")]
    [SerializeField] private ActorCtrl actor;
    [Tooltip("本关 GameCamera 的 Transform；运行时只修改 X，Y/Z 始终保持初始值。")]
    [SerializeField] private Transform cameraRoot;
    [Tooltip("第五坑右侧地面；用于判定跳过五坑并计算石像最终顶面高度。")]
    [SerializeField] private Transform rightGround;
    [Tooltip("第五坑底部石像；运行时会补充仅本关使用的 Kinematic Rigidbody2D。")]
    [SerializeField] private Transform statue;
    [Tooltip("本关出口门根节点；门触发器会在正确路线完成前保持禁用。")]
    [SerializeField] private Transform exitDoor;
    [Tooltip("连接出口门的绳子；失败路线中与门同步上移。")]
    [SerializeField] private Transform rope;

    [Header("仅水平镜头跟随")]
    [Tooltip("角色到达该视口横向比例后才开始跟随；初始阶段不移动镜头。")]
    [SerializeField, Range(0f, 1f)] private float cameraFollowStartViewportX = 0.6666667f;
    [Tooltip("镜头 X 轴平滑时间；设为 0 时直接跟随。")]
    [SerializeField, Min(0f)] private float cameraFollowSmoothTime = 0.2f;
    [Tooltip("策划黄色跟随区中心；运行时只使用其 X 范围，故意忽略角色 Y。")]
    [SerializeField] private Vector2 cameraFollowAreaCenter = new(29.66058f, -0.25f);
    [Tooltip("策划黄色跟随区尺寸；Y 仅供场景视图显示，不参与运行时跟随判定。")]
    [SerializeField] private Vector2 cameraFollowAreaSize = new(51.32116f, 14f);

    [Header("石像正确路线")]
    [Tooltip("角色稳定落到石像后，等待多久开始上升。")]
    [SerializeField, Min(0f)] private float statueRiseDelay = 0.8f;
    [Tooltip("石像从坑底升到右侧地面顶面的时长。")]
    [SerializeField, Min(0.01f)] private float statueRiseDuration = 1.5f;
    [Tooltip("石像升起终点相对右侧地面碰撞体顶面的高度偏移。")]
    [SerializeField] private float statueTopOffset;
    [Tooltip("判定角色正站在石像或右侧地面顶面的竖直容差。")]
    [SerializeField, Min(0.01f)] private float standingTopTolerance = 0.12f;
    [Tooltip("石像上升时允许继续承托角色的最大顶面距离。")]
    [SerializeField, Min(0.01f)] private float riderReleaseTolerance = 0.65f;
    [Tooltip("玩家主动跳跃后，暂时停止石像承托速度同步的时间。")]
    [SerializeField, Min(0f)] private float riderJumpSuppressDuration = 0.18f;

    [Header("跳过第五坑失败路线")]
    [Tooltip("门和绳子被拉到相机顶边所需的时间。")]
    [SerializeField, Min(0.01f)] private float doorLiftDuration = 1f;
    [Tooltip("上拉结束后，允许留在相机顶边下方的门体可见高度。")]
    [SerializeField, Min(0f)] private float doorVisibleHeightAtTop = 0.7f;

    #endregion

    #region Runtime State

    private enum RouteState
    {
        AwaitingChoice,
        WaitingForStatue,
        RaisingStatue,
        StatueRaised,
        PullingDoor,
        DoorPulled
    }

    private EventSubscriber subscriber;
    private Camera levelCamera;
    private Collider2D actorCollider;
    private Collider2D rightGroundCollider;
    private Collider2D statueCollider;
    private Collider2D doorTrigger;
    private Rigidbody2D actorBody;
    private Rigidbody2D statueBody;
    private MelenitasDev.SoundsGood.Sound doorLiftSound;

    private RouteState routeState;
    private Vector3 cameraInitialPosition;
    private Vector2 statueRiseStartPosition;
    private Vector2 statueRiseTargetPosition;
    private Vector3 doorLiftStartPosition;
    private Vector3 ropeLiftStartPosition;
    private float cameraFollowVelocityX;
    private float phaseElapsed;
    private float doorLiftDistance;
    private float riderSuppressedUntil;
    private bool cameraFollowStarted;
    private bool doorTriggerInitiallyEnabled;
    private bool riderSettlePending;
    private bool runtimeReady;
    private bool isPaused;
    private bool isResetting;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        InitializeRuntime();
    }

    private void OnEnable()
    {
        BindEvents();
    }

    private void FixedUpdate()
    {
        if (!runtimeReady || isPaused || isResetting)
            return;

        switch (routeState)
        {
            case RouteState.AwaitingChoice:
                EvaluateRouteChoice();
                break;
            case RouteState.WaitingForStatue:
                UpdateStatueDelay();
                break;
            case RouteState.RaisingStatue:
                UpdateStatueRise();
                break;
            case RouteState.StatueRaised:
                SettleRiderAfterLift();
                break;
            case RouteState.PullingDoor:
                UpdateDoorLift();
                break;
        }
    }

    private void LateUpdate()
    {
        UpdateHorizontalCameraFollow();
    }

    private void OnDisable()
    {
        CleanupRuntimeState();
        isPaused = true;
    }

    private void OnDestroy() => CleanupRuntimeState();

    private void CleanupRuntimeState()
    {
        StopDoorLiftSound();
        EventsUtils.ResetEvents(ref subscriber);
    }

    #endregion

    #region Initialization and Events

    private void InitializeRuntime()
    {
        BaseLevelCtrl baseLevelCtrl = GetComponent<BaseLevelCtrl>();
        actor ??= baseLevelCtrl != null ? baseLevelCtrl.Actor : null;
        rightGround ??= transform.Find("Ground_long (1)");
        statue ??= transform.Find("Sp_l89_02");
        exitDoor ??= transform.Find("door_0");
        rope ??= transform.Find("shengzi");

        levelCamera = cameraRoot != null ? cameraRoot.GetComponent<Camera>() : null;
        levelCamera ??= GetComponentInChildren<Camera>(true);
        levelCamera ??= Camera.main;
        if (cameraRoot == null && levelCamera != null)
            cameraRoot = levelCamera.transform;

        actorCollider = actor != null ? actor.GetComponent<Collider2D>() : null;
        actorBody = actor != null ? actor.GetComponent<Rigidbody2D>() : null;
        rightGroundCollider = rightGround != null ? rightGround.GetComponent<Collider2D>() : null;
        statueCollider = statue != null ? statue.GetComponent<Collider2D>() : null;
        doorTrigger = exitDoor != null ? exitDoor.GetComponentInChildren<Collider2D>(true) : null;

        if (actor == null || actorCollider == null || actorBody == null ||
            levelCamera == null || cameraRoot == null ||
            rightGroundCollider == null || statue == null || statueCollider == null ||
            exitDoor == null || doorTrigger == null || rope == null)
        {
            Debug.LogWarning("[Level98Ctrl] Required Level098 references are incomplete; private gameplay is disabled.", this);
            return;
        }

        statueBody = statue.GetComponent<Rigidbody2D>();
        if (statueBody == null)
            statueBody = statue.gameObject.AddComponent<Rigidbody2D>();

        statueBody.bodyType = RigidbodyType2D.Kinematic;
        statueBody.gravityScale = 0f;
        statueBody.constraints = RigidbodyConstraints2D.FreezeRotation;
        statueBody.interpolation = RigidbodyInterpolation2D.Interpolate;
        statueBody.linearVelocity = Vector2.zero;
        statueBody.angularVelocity = 0f;

        cameraInitialPosition = cameraRoot.position;
        doorTriggerInitiallyEnabled = doorTrigger.enabled;
        doorTrigger.enabled = false;
        routeState = RouteState.AwaitingChoice;
        runtimeReady = true;
    }

    private void BindEvents()
    {
        EventsUtils.ResetEvents(ref subscriber);
        subscriber.Subscribe<PlayerJumpEvent>(OnPlayerJump);
        subscriber.Subscribe<GamePauseEvent>(OnGamePause);
        subscriber.Subscribe<GameResumeEvent>(OnGameResume);
        subscriber.Subscribe<GameResetEvent>(OnGameReset);
    }

    private void OnPlayerJump(PlayerJumpEvent evt)
    {
        riderSuppressedUntil = Time.time + Mathf.Max(0f, riderJumpSuppressDuration);
    }

    private void OnGamePause(GamePauseEvent evt)
    {
        isPaused = true;
        cameraFollowVelocityX = 0f;
        StopDoorLiftSound();
    }

    private void OnGameResume(GameResumeEvent evt)
    {
        if (isResetting)
            return;

        isPaused = false;
        if (routeState == RouteState.PullingDoor)
            StartDoorLiftSound();
    }

    private void OnGameReset(GameResetEvent evt)
    {
        isResetting = true;
        isPaused = true;
        cameraFollowVelocityX = 0f;
        riderSettlePending = false;
        StopDoorLiftSound();

        if (statueBody != null)
        {
            statueBody.linearVelocity = Vector2.zero;
            statueBody.angularVelocity = 0f;
        }
    }

    #endregion

    #region Route Choice

    private void EvaluateRouteChoice()
    {
        // 同一物理帧优先认定石像接触，避免石像靠近右岸后把正确路线改判为失败。
        if (IsActorStandingOn(statueCollider, standingTopTolerance))
        {
            routeState = RouteState.WaitingForStatue;
            phaseElapsed = 0f;
            return;
        }

        if (IsActorStandingOn(rightGroundCollider, standingTopTolerance))
            BeginDoorFailure();
    }

    private bool IsActorStandingOn(Collider2D platformCollider, float tolerance)
    {
        if (actorCollider == null || platformCollider == null ||
            !actorCollider.enabled || !platformCollider.enabled ||
            !actorCollider.gameObject.activeInHierarchy ||
            !platformCollider.gameObject.activeInHierarchy)
        {
            return false;
        }

        Bounds actorBounds = actorCollider.bounds;
        Bounds platformBounds = platformCollider.bounds;
        bool overlapsHorizontally = actorBounds.max.x >= platformBounds.min.x &&
                                    actorBounds.min.x <= platformBounds.max.x;
        if (!overlapsHorizontally || actorBounds.center.y < platformBounds.center.y)
            return false;

        float allowedGap = Mathf.Max(0.01f, tolerance);
        float verticalGap = actorBounds.min.y - platformBounds.max.y;
        if (verticalGap < -allowedGap || verticalGap > allowedGap)
            return false;

        if (actorCollider.IsTouching(platformCollider))
            return true;

        ColliderDistance2D distance = actorCollider.Distance(platformCollider);
        float contactDistance = Mathf.Max(0.02f, Physics2D.defaultContactOffset * 2f);
        return distance.isValid && distance.distance <= contactDistance;
    }

    #endregion

    #region Statue Route

    private void UpdateStatueDelay()
    {
        phaseElapsed += Time.fixedDeltaTime;
        if (phaseElapsed < Mathf.Max(0f, statueRiseDelay))
            return;

        statueRiseStartPosition = statueBody.position;
        float riseDistance = Mathf.Max(
            0f,
            rightGroundCollider.bounds.max.y + statueTopOffset - statueCollider.bounds.max.y);
        statueRiseTargetPosition = statueRiseStartPosition + Vector2.up * riseDistance;
        phaseElapsed = 0f;

        if (riseDistance <= 0.0001f)
        {
            CompleteStatueRise();
            return;
        }

        routeState = RouteState.RaisingStatue;
        AudioManager.PlaySound(StatueRiseSfxTag);
    }

    private void UpdateStatueRise()
    {
        float duration = Mathf.Max(0.01f, statueRiseDuration);
        phaseElapsed += Time.fixedDeltaTime;
        float progress = Mathf.Clamp01(phaseElapsed / duration);
        float easedProgress = SmoothStep01(progress);
        Vector2 nextPosition = Vector2.LerpUnclamped(
            statueRiseStartPosition,
            statueRiseTargetPosition,
            easedProgress);
        Vector2 movementDelta = nextPosition - statueBody.position;

        CarryActorWithStatue(movementDelta);
        statueBody.MovePosition(nextPosition);

        if (progress >= 1f)
            CompleteStatueRise();
    }

    private void CarryActorWithStatue(Vector2 movementDelta)
    {
        if (Time.time < riderSuppressedUntil || !CanCarryActorWithStatue(movementDelta))
            return;

        float fixedDeltaTime = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        Vector2 actorVelocity = actorBody.linearVelocity;
        actorVelocity.y = movementDelta.y / fixedDeltaTime;
        actorBody.linearVelocity = actorVelocity;
        riderSettlePending = true;
    }

    private bool CanCarryActorWithStatue(Vector2 movementDelta)
    {
        if (actor.IsDied || actor.IsKnocked || !actorBody.simulated ||
            actorCollider == null || !actorCollider.enabled ||
            statueCollider == null || !statueCollider.enabled)
        {
            return false;
        }

        Bounds actorBounds = actorCollider.bounds;
        Bounds statueBounds = statueCollider.bounds;
        bool overlapsHorizontally = actorBounds.max.x >= statueBounds.min.x &&
                                    actorBounds.min.x <= statueBounds.max.x;
        if (!overlapsHorizontally || actorBounds.center.y < statueBounds.center.y)
            return false;

        float allowedGap = Mathf.Max(
            Mathf.Max(0.01f, riderReleaseTolerance),
            Mathf.Abs(movementDelta.y) + standingTopTolerance);
        float verticalGap = actorBounds.min.y - statueBounds.max.y;
        return verticalGap >= -allowedGap && verticalGap <= allowedGap;
    }

    private void CompleteStatueRise()
    {
        statueBody.MovePosition(statueRiseTargetPosition);
        routeState = RouteState.StatueRaised;
        riderSettlePending = true;
        if (doorTrigger != null)
            doorTrigger.enabled = doorTriggerInitiallyEnabled;
    }

    private void SettleRiderAfterLift()
    {
        if (!riderSettlePending || Time.time < riderSuppressedUntil)
            return;

        riderSettlePending = false;
        if (!CanCarryActorWithStatue(Vector2.zero))
            return;

        Vector2 actorVelocity = actorBody.linearVelocity;
        if (actorVelocity.y <= 2f)
        {
            actorVelocity.y = 0f;
            actorBody.linearVelocity = actorVelocity;
        }
    }

    #endregion

    #region Door Failure

    private void BeginDoorFailure()
    {
        routeState = RouteState.PullingDoor;
        phaseElapsed = 0f;
        riderSettlePending = false;

        if (doorTrigger != null)
            doorTrigger.enabled = false;
        if (statue != null)
            statue.gameObject.SetActive(false);

        doorLiftStartPosition = exitDoor.position;
        ropeLiftStartPosition = rope.position;
        doorLiftDistance = CalculateDoorLiftDistance();

        if (doorLiftDistance <= 0.0001f)
        {
            routeState = RouteState.DoorPulled;
            return;
        }

        StartDoorLiftSound();
    }

    private void UpdateDoorLift()
    {
        float duration = Mathf.Max(0.01f, doorLiftDuration);
        phaseElapsed += Time.fixedDeltaTime;
        float progress = Mathf.Clamp01(phaseElapsed / duration);
        float easedProgress = SmoothStep01(progress);
        Vector3 liftOffset = Vector3.up * (doorLiftDistance * easedProgress);

        exitDoor.position = doorLiftStartPosition + liftOffset;
        rope.position = ropeLiftStartPosition + liftOffset;

        if (progress >= 1f)
        {
            routeState = RouteState.DoorPulled;
            StopDoorLiftSound();
        }
    }

    private void StartDoorLiftSound()
    {
        doorLiftSound ??= AudioManager.PlaySound(DoorLiftSfxTag, loop: true);
    }

    private void StopDoorLiftSound()
    {
        doorLiftSound?.Stop();
        doorLiftSound = null;
    }

    private float CalculateDoorLiftDistance()
    {
        float depth = Mathf.Abs(exitDoor.position.z - levelCamera.transform.position.z);
        float cameraTopY = levelCamera.ViewportToWorldPoint(new Vector3(0.5f, 1f, depth)).y;
        float doorBottomY = TryGetDoorVisibleBounds(out Bounds visibleBounds)
            ? visibleBounds.min.y
            : doorTrigger.bounds.min.y;
        float targetBottomY = cameraTopY - Mathf.Max(0f, doorVisibleHeightAtTop);
        return Mathf.Max(0f, targetBottomY - doorBottomY);
    }

    private bool TryGetDoorVisibleBounds(out Bounds combinedBounds)
    {
        combinedBounds = default;
        bool hasBounds = false;
        Renderer[] renderers = exitDoor.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer currentRenderer = renderers[i];
            if (currentRenderer == null || !currentRenderer.enabled ||
                !currentRenderer.gameObject.activeInHierarchy ||
                currentRenderer.bounds.size.y <= 0.0001f)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = currentRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(currentRenderer.bounds);
            }
        }

        return hasBounds;
    }

    #endregion

    #region Camera

    private void UpdateHorizontalCameraFollow()
    {
        if (!runtimeReady || cameraRoot == null || levelCamera == null || actor == null)
            return;

        Vector3 cameraPosition = cameraRoot.position;
        if (!isPaused && !isResetting && !actor.IsDied)
        {
            bool actorInFollowArea = IsActorInsideHorizontalFollowArea();
            if (!cameraFollowStarted)
            {
                Vector3 actorViewportPosition = levelCamera.WorldToViewportPoint(actor.transform.position);
                if (actorViewportPosition.z > 0f &&
                    actorViewportPosition.x >= Mathf.Clamp01(cameraFollowStartViewportX) &&
                    actorInFollowArea)
                {
                    cameraFollowStarted = true;
                    cameraFollowVelocityX = 0f;
                }
            }

            if (cameraFollowStarted)
            {
                float actorTargetX = actor.transform.position.x;
                float followAreaHalfWidth = Mathf.Abs(cameraFollowAreaSize.x) * 0.5f;
                if (followAreaHalfWidth > 0.0001f)
                {
                    actorTargetX = Mathf.Clamp(
                        actorTargetX,
                        cameraFollowAreaCenter.x - followAreaHalfWidth,
                        cameraFollowAreaCenter.x + followAreaHalfWidth);
                }

                float targetX = Mathf.Clamp(
                    actorTargetX,
                    cameraInitialPosition.x,
                    GetMaximumCameraX());
                cameraPosition.x = cameraFollowSmoothTime > 0f
                    ? Mathf.SmoothDamp(
                        cameraPosition.x,
                        targetX,
                        ref cameraFollowVelocityX,
                        cameraFollowSmoothTime)
                    : targetX;
                cameraPosition.x = Mathf.Clamp(
                    cameraPosition.x,
                    cameraInitialPosition.x,
                    GetMaximumCameraX());
            }
        }

        // 第五坑需要让角色掉出画面；即使暂停，也绝不继承角色的竖直位移。
        cameraPosition.y = cameraInitialPosition.y;
        cameraPosition.z = cameraInitialPosition.z;
        cameraRoot.position = cameraPosition;
    }

    private float GetMaximumCameraX()
    {
        float cameraHalfWidth = levelCamera.orthographic
            ? levelCamera.orthographicSize * levelCamera.aspect
            : 0f;
        float rightBoundary = rightGroundCollider.bounds.max.x;
        return Mathf.Max(cameraInitialPosition.x, rightBoundary - cameraHalfWidth);
    }

    private bool IsActorInsideHorizontalFollowArea()
    {
        float halfWidth = Mathf.Abs(cameraFollowAreaSize.x) * 0.5f;
        if (halfWidth <= 0.0001f)
            return true;

        float minX = cameraFollowAreaCenter.x - halfWidth;
        float maxX = cameraFollowAreaCenter.x + halfWidth;
        float actorX = actor.transform.position.x;
        return actorX >= minX && actorX <= maxX;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(
            new Vector3(cameraFollowAreaCenter.x, cameraFollowAreaCenter.y, 0f),
            new Vector3(
                Mathf.Abs(cameraFollowAreaSize.x),
                Mathf.Abs(cameraFollowAreaSize.y),
                0.1f));
    }

    #endregion

    private static float SmoothStep01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }
}
