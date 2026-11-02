using System.Collections.Generic;
using System.Globalization;
using Bear.EventSystem;
using Game.Events;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using I2.Loc;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

/// <summary>
/// Level 134 双路线编排器：有限池化的 10000 层 Z 字诱导路线，以及拖动掉落台阶接载 Actor
/// 进入原门的短路线；登顶由独立天降门接走。全部行为限定在本关。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level134Ctrl : MonoBehaviour, IEventSender
{
    private const int TotalLayers = 10000;
    private const int StageSize = 10;
    private const int FinalStageFirstLayer = 9991;
    private const int PreparedStageCount = 3;
    private const string IdleAnimation = "idle";
    private const string JumpUpAnimation = "jump_up";
    private const string JumpDownAnimation = "jump_down";

    private static readonly int[] FirstStageDirections =
        { 1, 1, 1, -1, -1, 1, 1, -1, -1 };

    private static readonly int[][] BuiltInStageTemplates =
    {
        new[] { 1, 1, 1, -1, -1, 1, -1, 1, -1, -1 },
        new[] { 1, 1, -1, 1, -1, -1, 1, 1, -1, -1 },
        new[] { 1, -1, 1, 1, -1, 1, -1, -1, 1, -1 },
        new[] { -1, 1, 1, -1, 1, 1, -1, -1, 1, -1 },
        new[] { 1, -1, -1, 1, 1, -1, 1, 1, -1, -1 },
        new[] { -1, 1, -1, 1, 1, 1, -1, -1, -1, 1 },
    };

    private static readonly int[] SafeFallbackTemplate =
        { 1, -1, 1, -1, 1, -1, 1, -1, 1, -1 };

    private static readonly int[] FinalStageDirections =
        { 1, 1, -1, 1, -1, -1, 1, -1, -1, -1 };

    private enum GameplayMode
    {
        Ground,
        Climbing,
        Carrier,
        GroundRouteClosed,
        FinalDoorDrop,
        Failure,
        Finished,
    }

    private enum PointerOwnerKind
    {
        Other,
        Left,
        Right,
        Step,
    }

    private struct PointerOwner
    {
        public PointerOwnerKind Kind;
        public Level134Step Step;
    }

    [Header("关卡引用")]
    [SerializeField] private BaseLevelCtrl levelCtrl;
    [SerializeField] private ActorCtrl actor;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Transform realGround;
    [SerializeField] private Transform commonBackground;
    [SerializeField] private Transform successDoor;
    [SerializeField] private Level134CameraFollow cameraFollow;

    [Header("台阶与路线")]
    [SerializeField] private Sprite stepSprite;
    [SerializeField] private Vector3 stepScale = Vector3.one * 1.2f;
    [SerializeField] private Vector3 firstStepPosition = new Vector3(-1.41f, -1.41f, 0f);
    [SerializeField, Min(0.1f)] private float horizontalStepDistance = 1.536f;
    [SerializeField, Min(0.1f)] private float verticalStepHeight = 0.684f;
    [SerializeField, Min(0.1f)] private float smallJumpArcHeight = 1.464f;
    [SerializeField, Min(0.01f)] private float firstStepLandingTolerance = 0.54f;
    [SerializeField, Min(0.1f)] private float routeHorizontalSafeHalfWidth = 5f;
    [SerializeField] private string stepSortingLayerName = "SceneLayer";
    [SerializeField] private int stepSortingOrder = 60;
    [SerializeField] private int draggedStepSortingOrder = 90;
    [SerializeField, Min(0f)] private float stepGravityScale = 0.28125f;

    [Header("主动接载")]
    [SerializeField, Min(0.01f)] private float carrierHorizontalTolerance = 0.78f;
    [SerializeField, Min(0.01f)] private float carrierVerticalTolerance = 0.432f;
    [SerializeField, Min(0f)] private float carrierTopPenetrationTolerance = 0.05f;
    [SerializeField, Min(0f)] private float groundSupportTolerance = 0.16f;
    [SerializeField, Min(0f)] private float offscreenRecycleMargin = 0.8f;
    [SerializeField, Min(0f)] private float dragScreenMargin = 0.08f;

    [Header("相机固定信息（样式和位置在文字节点编辑）")]
    [SerializeField, FormerlySerializedAs("rewardText")] private TMP_Text rewardProgressText;
    // Inspector 绑定同一文字节点的 I2 组件；数字变化时刷新，语言切换沿用 I2 回调。
    [SerializeField] private Localize rewardProgressLocalize;
    [SerializeField, Min(0.1f)] private float cameraLayoutReferenceAspect = 1.7777778f;
    [SerializeField, Min(0.1f)] private float cameraLayoutReferenceOrthographicSize = 5.9f;

    [Header("深空循环（过渡完成后留出缓冲）")]
    [SerializeField, Min(1f)] private float deepSpaceBufferDistance = 12f;
    [SerializeField, Min(1f)] private float deepSpaceRebaseDistance = 12f;
    [SerializeField, Min(0.1f)] private float sceneryOffscreenMargin = 1f;

    [Header("最终高空与落门")]
    [SerializeField] private Transform endingRoot;
    [SerializeField] private Transform summitScenery;
    [SerializeField, Min(0f)] private float summitTopInset = 1f;
    [SerializeField] private Transform finalDoor;
    [SerializeField] private BoxCollider2D finalDoorTrigger;
    [SerializeField] private SuccessAnimCtrl finalDoorSuccessAnim;
    [SerializeField, Min(0.05f)] private float finalDoorFallDuration = 0.8f;
    [SerializeField, Min(0.1f)] private float finalDoorOffscreenMargin = 1f;

    private EventSubscriber eventSubscriber;
    private GameplayMode mode = GameplayMode.Ground;
    private readonly List<Vector3> routePositions = new List<Vector3>();
    private readonly List<int> routeDirections = new List<int>();
    private readonly List<int[]> validTemplates = new List<int[]>();
    private readonly Dictionary<int, Level134Step> climbSteps = new Dictionary<int, Level134Step>();
    private readonly List<Level134Step> allSteps = new List<Level134Step>();
    private readonly Stack<Level134Step> stepPool = new Stack<Level134Step>();
    private readonly Dictionary<int, PointerOwner> pointerOwners = new Dictionary<int, PointerOwner>();
    private readonly List<int> endedPointerIds = new List<int>();
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

    private Transform stepRoot;
    private Transform runtimeRoot;
    private Vector3 finalDoorDropStart;
    private Vector3 finalDoorDropTarget;
    private float finalDoorDropElapsed;
    private bool finalDoorDropActive;
    private bool finalDoorReady;
    private Transform backgroundCache;
    private float backgroundInitialY;
    private Transform movingWorldRoot;
    private double worldOffsetY;
    private float summitCameraY;
    private float summitVisualBottomY;
    private bool summitLayoutReady;
    private bool finalApproach;
    private readonly List<BackgroundGradient> backgroundGradients = new List<BackgroundGradient>();
    private readonly List<BoxCollider2D> sideFallAreas = new List<BoxCollider2D>();
    private static readonly int StartHeightId = Shader.PropertyToID("_StartHeight");
    private static readonly int EndHeightId = Shader.PropertyToID("_EndHeight");

    private sealed class BackgroundGradient
    {
        public SpriteRenderer Renderer;
        public MaterialPropertyBlock Properties;
        public float StartHeight;
        public float EndHeight;
    }
    private BoxCollider2D cameraBottomFallAreaCollider;
    private RectTransform leftMoveButtonRect;
    private RectTransform rightMoveButtonRect;
    private Collider2D realGroundCollider;
    private Collider2D actorMainCollider;
    private Collider2D actorPushCollider;
    private bool actorPushColliderInitiallyEnabled;
    private GameObject actorShadowObject;
    private bool actorShadowActiveBeforeSteps;
    private bool actorShadowSuppressed;
    private Collider2D[] actorColliders;
    private Rigidbody2D actorBody;
    private Transform actorInitialParent;
    private RigidbodyType2D actorInitialBodyType;
    private RigidbodyConstraints2D actorInitialConstraints;
    private float actorInitialGravityScale;
    private bool actorInitialSimulated;
    private bool actorStateCaptured;
    private bool actorPauseStateCaptured;
    private bool actorSimulatedBeforePause;
    private Vector2 actorVelocityBeforePause;
    private float actorAngularVelocityBeforePause;
    private float actorFootOffset;
    private Level134Step currentStep;
    private Level134Step carrierStep;
    private Level134Step landedCarrierStep;
    private Level134Step activeDraggedStep;
    private int currentLayer;
    private int lastTemplateIndex = -1;
    private int activeDragPointerId = int.MinValue;
    private int stepLayer;
    private int groundLayer;
    private int fallAreaLayer;
    private bool initialized;
    private bool gameplayActive = true;
    private bool directionInputArmed = true;
    private bool directionChordBlocked;
    private bool touchLeftHeld;
    private bool touchRightHeld;
    private bool smallJumpPending;
    private bool smallJumpActive;
    private bool smallJumpApexReached;
    private int smallJumpDirection;
    private float smallJumpElapsed;
    private float smallJumpExpectedApexTime;
    private float smallJumpExpectedLandingTime;
    private Level134Step smallJumpTarget;
    private bool routePermanentlyClosed;
    private bool doorHandoffStarted;
    private Level134RealTipsPopup realTipsPopup;
    private bool realTipsPopupTriggered;
    private bool dragStartedUnderActor;
    private bool cameraLayoutCaptured;
    private Vector3 successDoorAuthoredLocalPosition;
    private Vector3 rewardProgressTextAuthoredLocalPosition;
    private float lastCameraLayoutAspect = -1f;
    private float lastCameraLayoutOrthographicSize = -1f;

    public Transform StepRoot => stepRoot;
    public float StepGravityScale => stepGravityScale;
    public int DraggedStepSortingOrder => draggedStepSortingOrder;
    public int StepLayer => stepLayer;
    public int GroundLayer => groundLayer;

    private void Awake()
    {
        ResolveReferences();
        CaptureActorState();
    }

    private void Start()
    {
        InitializeRuntime();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        EventsUtils.ResetEvents(ref eventSubscriber);
        eventSubscriber.Subscribe<PlayerRightMoveEvent>(OnPlayerRightMove);
        eventSubscriber.Subscribe<PlayerLeftMoveEvent>(OnPlayerLeftMove);
        eventSubscriber.Subscribe<PlayerMoveCancelEvent>(OnPlayerMoveCancel);
        eventSubscriber.Subscribe<PlayerJumpEvent>(OnPlayerJump);
        eventSubscriber.Subscribe<GameResetEvent>(OnGameReset);
        eventSubscriber.Subscribe<OnTriggerTrapEvent>(OnFailureStarted);
        eventSubscriber.Subscribe<OnTriggerFailAreaEvent>(OnFailureStarted);
        eventSubscriber.Subscribe<GamePauseEvent>(OnGamePause);
        eventSubscriber.Subscribe<GameResumeEvent>(OnGameResume);
        eventSubscriber.Subscribe<SwitchGameStateEvent>(OnGameStateChanged);
    }

    private void Update()
    {
        if (!initialized)
        {
            InitializeRuntime();
        }

        if (!initialized || actor == null)
        {
            return;
        }

        if (levelCtrl != null && levelCtrl.IsActorEnterDoor)
        {
            BeginDoorHandoff();
        }

        if (!gameplayActive || mode == GameplayMode.Failure || mode == GameplayMode.Finished)
        {
            return;
        }

        ResolveMoveButtons();
        UpdateCameraFixedLayout();
        if (mode == GameplayMode.FinalDoorDrop)
        {
            UpdateFinalDoorDrop();
            return;
        }
        // Only rebase between physical jumps, outside collision callbacks and before input.
        UpdateDeepSpaceLoop();
        ProcessPointerInput();
        UpdateSmallJump();
        UpdateCarrierActor();
        UpdateStepLifecycle();
        UpdateBackgroundLoop();

        if (mode == GameplayMode.Ground)
        {
            TryEnterFirstStep();
        }
        else if (mode == GameplayMode.Climbing && !smallJumpActive)
        {
            LockActorForClimb();
        }
    }

    private void LateUpdate()
    {
        if (smallJumpPending && gameplayActive && mode == GameplayMode.Climbing)
        {
            BeginPhysicalSmallJump();
        }

        if (smallJumpActive)
        {
            UpdateSmallJumpAnimation();
        }

        if (carrierStep != null && mode == GameplayMode.Carrier)
        {
            PositionActorOnStep(carrierStep);
            actor?.SpineCtrl?.PlayAnimation(IdleAnimation, true, true);
        }
    }

    private void ResolveReferences()
    {
        levelCtrl ??= GetComponent<BaseLevelCtrl>();
        actor ??= levelCtrl != null ? levelCtrl.Actor : FindFirstObjectByType<ActorCtrl>();
        targetCamera ??= Camera.main;
        if (targetCamera == null)
        {
            targetCamera = GetComponentInChildren<Camera>(true);
        }

        realGround ??= transform.Find("Ground_short") ?? transform.Find("Ground_long");
        commonBackground ??= transform.Find("CommonBg");
        successDoor ??= targetCamera != null ? targetCamera.transform.Find("door_0") : null;
        successDoor ??= transform.Find("door_0");
        cameraFollow ??= GetComponent<Level134CameraFollow>();
        if (cameraFollow == null)
        {
            cameraFollow = gameObject.AddComponent<Level134CameraFollow>();
        }
    }

    private void CaptureActorState()
    {
        if (actorStateCaptured || actor == null)
        {
            return;
        }

        actorBody = actor.GetComponent<Rigidbody2D>();
        actorColliders = actor.GetComponentsInChildren<Collider2D>(true);
        actorMainCollider = ResolveActorMainCollider(actorColliders);
        actorPushCollider = actor.transform.Find("pushCollider")?.GetComponent<Collider2D>();
        actorPushColliderInitiallyEnabled = actorPushCollider != null && actorPushCollider.enabled;
        actorInitialParent = actor.transform.parent;
        if (actorBody != null)
        {
            actorInitialBodyType = actorBody.bodyType;
            actorInitialConstraints = actorBody.constraints;
            actorInitialGravityScale = actorBody.gravityScale;
            actorInitialSimulated = actorBody.simulated;
        }

        actorFootOffset = actorMainCollider != null
            ? actor.transform.position.y - actorMainCollider.bounds.min.y
            : 0.55f;
        actorStateCaptured = actorBody != null;
    }

    private void InitializeRuntime()
    {
        if (initialized || !Application.isPlaying)
        {
            return;
        }

        ResolveReferences();
        CaptureActorState();
        if (actor == null || actorBody == null || targetCamera == null || stepSprite == null)
        {
            Debug.LogError("[Level134] Missing Actor, Rigidbody2D, Camera, or step Sprite reference.", this);
            return;
        }

        stepLayer = Mathf.Max(0, LayerMask.NameToLayer("Stuff"));
        groundLayer = Mathf.Max(0, LayerMask.NameToLayer("Ground"));
        fallAreaLayer = Mathf.Max(0, LayerMask.NameToLayer("FallArea"));
        realGroundCollider = realGround != null ? realGround.GetComponent<Collider2D>() : null;

        runtimeRoot = new GameObject("Level134Runtime").transform;
        runtimeRoot.SetParent(transform, false);
        stepRoot = new GameObject("Level134StepPool").transform;
        stepRoot.SetParent(runtimeRoot, false);

        cameraFollow.Configure(actor.transform, targetCamera);
        cameraFollow.Freeze();
        InitializeAuthoredEnding();
        DisableCompetingCameraFollowers();
        BuildTemplatePool();
        InitializeRoute();
        EnsureRouteAndStepsThrough(StageSize * PreparedStageCount);
        InitializeCameraHud();
        CreateCameraBottomFallArea();
        PrepareBackgroundLoop();
        InitializeMovingWorld();
        UpdateProgressText();
        initialized = true;
    }

    private void DisableCompetingCameraFollowers()
    {
        CameraFollowArea[] sharedFollowers = GetComponentsInChildren<CameraFollowArea>(true);
        for (int i = 0; i < sharedFollowers.Length; i++)
        {
            sharedFollowers[i].enabled = false;
        }

    }

    private void BuildTemplatePool()
    {
        validTemplates.Clear();
        for (int i = 0; i < BuiltInStageTemplates.Length; i++)
        {
            int[] template = BuiltInStageTemplates[i];
            if (ValidateTemplate(template))
            {
                validTemplates.Add(template);
            }
            else
            {
                Debug.LogError($"[Level134] Stage template {i} violates the universal splice contract.", this);
            }
        }

        if (validTemplates.Count == 0)
        {
            validTemplates.Add(SafeFallbackTemplate);
            Debug.LogError("[Level134] No valid random template; using the built-in safe fallback.", this);
        }
    }

    private bool ValidateTemplate(int[] template)
    {
        if (template == null || template.Length != StageSize)
        {
            return false;
        }

        int left = 0;
        int right = 0;
        int prefix = 0;
        float safeHalfWidth = ResolveRouteSafeHalfWidth();
        float firstStageX = firstStepPosition.x;
        if (Mathf.Abs(firstStageX) > safeHalfWidth)
        {
            return false;
        }

        for (int i = 0; i < FirstStageDirections.Length; i++)
        {
            firstStageX += FirstStageDirections[i] * horizontalStepDistance;
            if (Mathf.Abs(firstStageX) > safeHalfWidth)
            {
                return false;
            }
        }

        float x = firstStageX;
        for (int i = 0; i < template.Length; i++)
        {
            if (template[i] == -1)
            {
                left++;
            }
            else if (template[i] == 1)
            {
                right++;
            }
            else
            {
                return false;
            }

            prefix += template[i];
            x += template[i] * horizontalStepDistance;
            if (prefix < -1 || prefix > 3)
            {
                return false;
            }

            if (Mathf.Abs(x) > safeHalfWidth)
            {
                return false;
            }
        }

        return left == 5 && right == 5 && prefix == 0;
    }

    private float ResolveRouteSafeHalfWidth()
    {
        float configuredHalfWidth = Mathf.Max(0.1f, routeHorizontalSafeHalfWidth);
        if (targetCamera == null)
        {
            return configuredHalfWidth;
        }

        float cameraHalfWidth = targetCamera.orthographicSize
            * Mathf.Max(0.1f, targetCamera.aspect);
        float stepHalfWidth = stepSprite != null
            ? stepSprite.bounds.extents.x * Mathf.Abs(stepScale.x)
            : 0.65f;
        return Mathf.Min(
            configuredHalfWidth,
            Mathf.Max(0.1f, cameraHalfWidth - stepHalfWidth - dragScreenMargin));
    }

    private void InitializeRoute()
    {
        routePositions.Clear();
        routeDirections.Clear();
        routePositions.Add(Vector3.zero);
        routeDirections.Add(0);
        routePositions.Add(firstStepPosition);

        Vector3 cursor = firstStepPosition;
        for (int i = 0; i < FirstStageDirections.Length; i++)
        {
            int direction = FirstStageDirections[i];
            routeDirections.Add(direction);
            cursor += new Vector3(
                direction * horizontalStepDistance,
                verticalStepHeight,
                0f);
            routePositions.Add(cursor);
        }
    }

    private void EnsureRouteAndStepsThrough(int requestedLayer)
    {
        if (routePermanentlyClosed)
        {
            return;
        }

        requestedLayer = Mathf.Clamp(requestedLayer, 1, TotalLayers);
        while (routePositions.Count - 1 < requestedLayer)
        {
            int lastPlannedLayer = routePositions.Count - 1;
            if (lastPlannedLayer < 10)
            {
                break;
            }

            if (lastPlannedLayer < FinalStageFirstLayer - 1)
            {
                AppendRandomStage();
            }
            else
            {
                AppendDirections(FinalStageDirections);
            }
        }

        int maxLayer = Mathf.Min(requestedLayer, routePositions.Count - 1);
        for (int layer = Mathf.Max(1, currentLayer + 1); layer <= maxLayer; layer++)
        {
            if (layer <= currentLayer || climbSteps.ContainsKey(layer))
            {
                continue;
            }

            Level134Step step = AcquireStep();
            step.name = $"Level134_ClimbStep_{layer:00000}";
            step.ConfigureClimbable(ResolveRouteWorldPosition(layer), layer);
            climbSteps[layer] = step;
        }

        RefreshClimbableColliderWindow();
    }

    private void AppendRandomStage()
    {
        int selected = 0;
        if (validTemplates.Count > 1)
        {
            do
            {
                selected = UnityEngine.Random.Range(0, validTemplates.Count);
            }
            while (selected == lastTemplateIndex);
        }

        lastTemplateIndex = selected;
        AppendDirections(validTemplates[selected]);
    }

    private Vector3 ResolveRouteWorldPosition(int layer)
    {
        // X follows the selected templates; Y is accumulated in double precision, independently
        // of the presentation offset. Rebase never changes a layer or a route direction.
        return new Vector3(routePositions[layer].x,
            (float)(firstStepPosition.y + (layer - 1) * (double)verticalStepHeight + worldOffsetY),
            routePositions[layer].z);
    }

    private void AppendDirections(int[] directions)
    {
        Vector3 cursor = routePositions[routePositions.Count - 1];
        for (int i = 0; i < directions.Length && routePositions.Count - 1 < TotalLayers; i++)
        {
            int direction = directions[i];
            routeDirections.Add(direction);
            cursor += new Vector3(
                direction * horizontalStepDistance,
                verticalStepHeight,
                0f);
            routePositions.Add(cursor);
        }
    }

    private Level134Step AcquireStep()
    {
        Level134Step step;
        if (stepPool.Count > 0)
        {
            step = stepPool.Pop();
        }
        else
        {
            GameObject stepObject = new GameObject("Level134Step");
            stepObject.transform.SetParent(stepRoot, false);
            step = stepObject.AddComponent<Level134Step>();
            step.Initialize(
                this,
                stepSprite,
                stepScale,
                stepSortingLayerName,
                stepSortingOrder);

            for (int i = 0; i < allSteps.Count; i++)
            {
                if (allSteps[i] != null && allSteps[i].Collider != null)
                {
                    Physics2D.IgnoreCollision(step.Collider, allSteps[i].Collider, true);
                }
            }

            allSteps.Add(step);
        }

        return step;
    }

    private void RecycleStep(Level134Step step)
    {
        if (step == null
            || step == carrierStep
            || (step == landedCarrierStep && step.State == Level134Step.StepState.Resting)
            || step.State == Level134Step.StepState.Pooled)
        {
            return;
        }

        if (activeDraggedStep == step)
        {
            step.CancelDrag();
        }

        climbSteps.Remove(step.LayerNumber);
        SetStepActorCollisionIgnored(step, true);
        step.Recycle();
        stepPool.Push(step);
    }

    private void TryEnterFirstStep()
    {
        if (!climbSteps.TryGetValue(1, out Level134Step firstStep)
            || actorBody.linearVelocityY > 0.05f
            || !IsActorLandingOn(firstStep))
        {
            return;
        }

        mode = GameplayMode.Climbing;
        currentLayer = 1;
        currentStep = firstStep;
        LockActorForClimb();
        PositionActorOnStep(firstStep);
        RefreshClimbableColliderWindow();
        cameraFollow.BeginClimb();
        UpdateProgressText();
        directionInputArmed = true;
        // Open in the same Update as the snap, before rendering. Pause must capture
        // the locked, zero-velocity state rather than the incoming jump velocity.
        ShowFirstStepTipsPopup();
    }

    private void ShowFirstStepTipsPopup()
    {
        if (realTipsPopupTriggered || realTipsPopup != null || !isActiveAndEnabled)
        {
            return;
        }

        realTipsPopup = Level134RealTipsPopup.Create();
        realTipsPopupTriggered = realTipsPopup != null;
    }

    private void CloseRealTipsPopup()
    {
        if (realTipsPopup != null)
        {
            realTipsPopup.CloseFromOwner();
            realTipsPopup = null;
        }

        realTipsPopupTriggered = false;
    }

    private bool IsActorLandingOn(Level134Step step)
    {
        if (step == null || actorMainCollider == null || step.Collider == null)
        {
            return false;
        }

        Bounds actorBounds = actorMainCollider.bounds;
        Bounds stepBounds = step.Collider.bounds;
        float verticalDelta = actorBounds.min.y - stepBounds.max.y;
        float horizontalTolerance = stepBounds.extents.x + actorBounds.extents.x * 0.25f;
        return verticalDelta <= firstStepLandingTolerance
            && verticalDelta >= -firstStepLandingTolerance * 0.2f
            && actorBounds.center.y > stepBounds.center.y
            && Mathf.Abs(actorBounds.center.x - stepBounds.center.x) <= horizontalTolerance;
    }

    private void OnPlayerRightMove(PlayerRightMoveEvent evt)
    {
        if (mode != GameplayMode.Climbing || Input.touchCount > 0 || activeDragPointerId == -1)
        {
            return;
        }

        TryStartSmallJump(1);
    }

    private void OnPlayerLeftMove(PlayerLeftMoveEvent evt)
    {
        if (mode != GameplayMode.Climbing || Input.touchCount > 0 || activeDragPointerId == -1)
        {
            return;
        }

        TryStartSmallJump(-1);
    }

    private void OnPlayerMoveCancel(PlayerMoveCancelEvent evt)
    {
        if (Input.touchCount == 0)
        {
            directionInputArmed = true;
        }
    }

    private void OnPlayerJump(PlayerJumpEvent evt)
    {
        if (mode == GameplayMode.Climbing || mode == GameplayMode.Carrier
            || mode == GameplayMode.FinalDoorDrop)
        {
            actor?.StopMoving();
        }
    }

    private void TryStartSmallJump(int direction)
    {
        if (!directionInputArmed
            || smallJumpPending
            || smallJumpActive
            || mode != GameplayMode.Climbing
            || currentLayer <= 0
            || currentLayer >= TotalLayers
            || currentLayer >= routeDirections.Count)
        {
            return;
        }

        if (touchLeftHeld && touchRightHeld)
        {
            return;
        }

        directionInputArmed = false;
        smallJumpDirection = direction;
        smallJumpTarget = null;

        if (routeDirections[currentLayer] == direction)
        {
            int targetLayer = currentLayer + 1;
            EnsureRouteAndStepsThrough(Mathf.Min(TotalLayers, targetLayer + (PreparedStageCount - 1) * StageSize));
            climbSteps.TryGetValue(targetLayer, out smallJumpTarget);
        }

        smallJumpPending = true;
    }

    private void BeginPhysicalSmallJump()
    {
        smallJumpPending = false;
        // HUD events may arm the next jump before Update. Keep the same layer-boundary
        // rebase available here, before any world-space launch position is captured.
        UpdateDeepSpaceLoop();
        Vector3 start = actor.transform.position;
        Vector3 expectedLanding = smallJumpTarget != null
            ? ResolveActorPositionOnStep(smallJumpTarget)
            : start + new Vector3(
                smallJumpDirection * horizontalStepDistance,
                verticalStepHeight,
                0f);
        float gravityAcceleration = Mathf.Abs(Physics2D.gravity.y * actorInitialGravityScale);
        gravityAcceleration = Mathf.Max(0.01f, gravityAcceleration);
        float landingDeltaY = expectedLanding.y - start.y;
        float apexHeight = Mathf.Max(
            smallJumpArcHeight,
            landingDeltaY + 0.01f);
        float verticalVelocity = Mathf.Sqrt(2f * gravityAcceleration * apexHeight);
        smallJumpExpectedApexTime = verticalVelocity / gravityAcceleration;
        float expectedDescentTime = Mathf.Sqrt(
            2f * Mathf.Max(0.01f, apexHeight - landingDeltaY) / gravityAcceleration);
        smallJumpExpectedLandingTime = smallJumpExpectedApexTime + expectedDescentTime;
        float horizontalVelocity = (expectedLanding.x - start.x)
            / Mathf.Max(0.01f, smallJumpExpectedLandingTime);

        levelCtrl?.SetCtrlable(false);
        SetClimbPushCollisionSuppressed(true);
        actor.SetMoveInput(0f);
        actor.StopMoving();
        actor.transform.SetParent(actorInitialParent, true);
        actorBody.simulated = true;
        actorBody.bodyType = RigidbodyType2D.Dynamic;
        actorBody.gravityScale = actorInitialGravityScale;
        actorBody.linearVelocity = new Vector2(horizontalVelocity, verticalVelocity);
        actorBody.angularVelocity = 0f;

        FaceActorTowardSmallJump();
        smallJumpElapsed = 0f;
        smallJumpApexReached = false;
        smallJumpActive = true;
        SetStepShadowSuppressed(true);
        RefreshClimbableColliderWindow();
        PlaySmallJumpAnimation(JumpUpAnimation, smallJumpExpectedApexTime);
        AudioManager.PlaySound("jump");
    }

    private void FaceActorTowardSmallJump()
    {
        bool shouldFaceRight = smallJumpDirection > 0;
        if (actor.FaingRight != shouldFaceRight)
        {
            actor.SetFaceDir(shouldFaceRight);
        }
    }

    private void UpdateSmallJump()
    {
        if (!smallJumpActive || actor == null || actorBody == null)
        {
            return;
        }

        smallJumpElapsed += Time.deltaTime;
        UpdateSmallJumpAnimation();
    }

    private void UpdateSmallJumpAnimation()
    {
        if (!smallJumpActive || actorBody == null || actor.SpineCtrl == null)
        {
            return;
        }

        if (!smallJumpApexReached && actorBody.linearVelocityY <= 0f)
        {
            smallJumpApexReached = true;
            PlaySmallJumpAnimation(
                JumpDownAnimation,
                Mathf.Max(0.01f, smallJumpExpectedLandingTime - smallJumpElapsed));
        }
        else if (!smallJumpApexReached
            && !actor.SpineCtrl.IsPlayingAnimation(JumpUpAnimation))
        {
            PlaySmallJumpAnimation(
                JumpUpAnimation,
                Mathf.Max(0.01f, smallJumpExpectedApexTime - smallJumpElapsed));
        }
        else if (smallJumpApexReached
            && !actor.SpineCtrl.IsPlayingAnimation(JumpDownAnimation))
        {
            PlaySmallJumpAnimation(
                JumpDownAnimation,
                Mathf.Max(0.01f, smallJumpExpectedLandingTime - smallJumpElapsed));
        }
    }

    private void PlaySmallJumpAnimation(string animationName, float phaseDuration)
    {
        Spine.TrackEntry entry = actor.SpineCtrl?.PlayAnimation(animationName, false, false);
        if (entry == null)
        {
            return;
        }

        float animationDuration = Mathf.Max(0.0001f, entry.AnimationEnd - entry.AnimationStart);
        entry.TimeScale = animationDuration / Mathf.Max(0.01f, phaseDuration);
    }

    internal void NotifyActorTouchedClimbable(Level134Step step)
    {
        if (!smallJumpActive
            || step == null
            || step != smallJumpTarget
            || actorMainCollider == null
            || actorBody == null
            || actorBody.linearVelocityY > 0.05f
            || !step.Collider.IsTouching(actorMainCollider))
        {
            return;
        }

        Bounds actorBounds = actorMainCollider.bounds;
        Bounds stepBounds = step.Collider.bounds;
        if (actorBounds.center.y <= stepBounds.center.y
            || Mathf.Abs(actorBounds.min.y - stepBounds.max.y) > firstStepLandingTolerance)
        {
            return;
        }

        CompleteSuccessfulStep(step);
    }

    private void CompleteSuccessfulStep(Level134Step targetStep)
    {
        smallJumpPending = false;
        smallJumpActive = false;
        LockActorForClimb();
        Level134Step previousStep = currentStep;
        currentStep = targetStep;
        currentLayer = targetStep.LayerNumber;
        PositionActorOnStep(targetStep);
        actor.SpineCtrl?.PlayAnimation(IdleAnimation, true, true);

        if (previousStep != null && previousStep != targetStep)
        {
            climbSteps.Remove(previousStep.LayerNumber);
            previousStep.BeginFalling();
        }

        RefreshClimbableColliderWindow();
        UpdateProgressText();
        if (currentLayer >= TotalLayers)
        {
            EnterFinalDoorDropState();
            return;
        }

        EnsureRouteAndStepsThrough(Mathf.Min(
            TotalLayers,
            currentLayer + (PreparedStageCount - 1) * StageSize));
    }

    private void LockActorForClimb()
    {
        SetStepShadowSuppressed(true);
        SetClimbPushCollisionSuppressed(true);
        levelCtrl?.SetCtrlable(false);
        actor.SetMoveInput(0f);
        actor.StopMoving();
        actorBody.simulated = true;
        actorBody.bodyType = RigidbodyType2D.Kinematic;
        actorBody.gravityScale = 0f;
        actorBody.linearVelocity = Vector2.zero;
        actorBody.angularVelocity = 0f;
    }

    private void SetStepShadowSuppressed(bool suppressed)
    {
        if (suppressed)
        {
            if (actorShadowSuppressed || actor == null)
            {
                return;
            }

            ShadowController shadow = actor.GetComponentInChildren<ShadowController>(true);
            if (shadow == null)
            {
                return;
            }

            // ShadowController creates child renderers; hide the whole shadow hierarchy.
            actorShadowObject = shadow.gameObject;
            actorShadowActiveBeforeSteps = actorShadowObject.activeSelf;
            actorShadowSuppressed = true;
            actorShadowObject.SetActive(false);
        }
        else if (actorShadowSuppressed)
        {
            if (actorShadowObject != null)
            {
                actorShadowObject.SetActive(actorShadowActiveBeforeSteps);
            }
            actorShadowSuppressed = false;
            actorShadowObject = null;
        }
    }

    private void SetClimbPushCollisionSuppressed(bool suppressed)
    {
        if (actorPushCollider != null)
        {
            // ActorCtrl toggles the child GameObject during collision checks; keep the component disabled.
            actorPushCollider.enabled = !suppressed && actorPushColliderInitiallyEnabled;
        }
    }

    private void EnterFinalDoorDropState()
    {
        mode = GameplayMode.FinalDoorDrop;
        routePermanentlyClosed = true;
        cameraFollow.FreezeForFinalStage();
        currentStep?.MakeStaticSupport();
        LockActorForClimb();
        // Keep the actor at the real final landing, with a dynamic, zero-gravity
        // body so the newly enabled static door trigger can generate a contact.
        actorBody.bodyType = RigidbodyType2D.Dynamic;
        actorBody.constraints = RigidbodyConstraints2D.FreezeAll;
        SetActorCollisionForCurrentStates();
        BeginFinalDoorDrop();
    }

    private void BeginFinalDoorDrop()
    {
        if (finalDoor == null || finalDoorTrigger == null || finalDoorSuccessAnim == null)
        {
            Debug.LogError("[Level134] Missing final drop door references.", this);
            return;
        }

        finalDoorReady = false;
        finalDoorDropElapsed = 0f;
        finalDoorTrigger.enabled = false;
        finalDoor.gameObject.SetActive(true);

        Vector2 offset = finalDoorTrigger.offset;
        Vector3 center = finalDoorTrigger.transform.TransformPoint(offset);
        Vector3 bottom = finalDoorTrigger.transform.TransformPoint(
            new Vector3(offset.x, offset.y - finalDoorTrigger.size.y * 0.5f, 0f));
        float triggerBottomOffset = bottom.y - finalDoor.position.y;
        finalDoorDropTarget = new Vector3(
            actorMainCollider.bounds.center.x - (center.x - finalDoor.position.x),
            currentStep.TopY - triggerBottomOffset,
            actor.transform.position.z);

        float visibleBottomOffset = triggerBottomOffset;
        foreach (Renderer renderer in finalDoor.GetComponentsInChildren<Renderer>())
        {
            if (renderer.enabled && renderer.bounds.size.sqrMagnitude > 0.0001f)
            {
                visibleBottomOffset = Mathf.Min(visibleBottomOffset,
                    renderer.bounds.min.y - finalDoor.position.y);
            }
        }

        float screenTop = targetCamera.transform.position.y + targetCamera.orthographicSize;
        finalDoorDropStart = finalDoorDropTarget;
        finalDoorDropStart.y = Mathf.Max(finalDoorDropTarget.y,
            screenTop + finalDoorOffscreenMargin - visibleBottomOffset);
        finalDoor.position = finalDoorDropStart;
        finalDoorDropActive = true;
    }

    private void UpdateFinalDoorDrop()
    {
        // Update is gated by gameplayActive: pause/resume also pauses this timer.
        if (!finalDoorDropActive || finalDoor == null || finalDoorTrigger == null)
        {
            return;
        }

        finalDoorDropElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(finalDoorDropElapsed / Mathf.Max(0.05f, finalDoorFallDuration));
        finalDoor.position = Vector3.Lerp(finalDoorDropStart, finalDoorDropTarget, progress * progress);
        if (progress < 1f)
        {
            return;
        }

        finalDoorDropActive = false;
        finalDoorReady = true;
        finalDoorTrigger.enabled = true;
        // Do not synthesize a collision or success event: the real trigger
        // overlap with the waiting actor enters the existing success pipeline.
    }

    private void CancelFinalDoorDrop()
    {
        finalDoorDropActive = false;
        finalDoorReady = false;
        finalDoorDropElapsed = 0f;
        if (finalDoorTrigger != null) finalDoorTrigger.enabled = false;
        if (finalDoor != null) finalDoor.gameObject.SetActive(false);
    }

    private void RestoreActorFreeControl()
    {
        SetStepShadowSuppressed(false);
        SetClimbPushCollisionSuppressed(false);
        actor.transform.SetParent(actorInitialParent, true);
        actorBody.simulated = actorInitialSimulated;
        actorBody.bodyType = actorInitialBodyType;
        actorBody.constraints = actorInitialConstraints;
        actorBody.gravityScale = actorInitialGravityScale;
        actorBody.linearVelocity = Vector2.zero;
        actorBody.angularVelocity = 0f;
        actor.SetMoveInput(0f);
        actor.ResumeMoving();
        actor.UpdateAnimation();
    }

    private void ProcessPointerInput()
    {
        if (Input.touchCount > 0)
        {
            ProcessTouches();
        }
        else
        {
            ProcessMouseDrag();
        }
    }

    private void ProcessTouches()
    {
        endedPointerIds.Clear();
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            int pointerId = touch.fingerId;
            if (touch.phase == TouchPhase.Began)
            {
                pointerOwners[pointerId] = ResolvePointerOwner(pointerId, touch.position);
            }

            if (!pointerOwners.TryGetValue(pointerId, out PointerOwner owner))
            {
                continue;
            }

            if (owner.Kind == PointerOwnerKind.Step && owner.Step != null
                && (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary))
            {
                owner.Step.MoveDrag(ScreenToWorld(touch.position));
            }

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                ReleasePointerOwner(pointerId, owner);
                endedPointerIds.Add(pointerId);
            }
        }

        if (mode == GameplayMode.Climbing
            && directionInputArmed
            && !directionChordBlocked
            && touchLeftHeld != touchRightHeld)
        {
            TryStartSmallJump(touchLeftHeld ? -1 : 1);
        }

        if (touchLeftHeld && touchRightHeld)
        {
            directionChordBlocked = true;
        }

        for (int i = 0; i < endedPointerIds.Count; i++)
        {
            pointerOwners.Remove(endedPointerIds[i]);
        }
    }

    private PointerOwner ResolvePointerOwner(int pointerId, Vector2 screenPosition)
    {
        if (mode == GameplayMode.Climbing)
        {
            int direction = ResolveMoveButtonDirection(screenPosition);
            if (direction != 0)
            {
                if (direction < 0)
                {
                    touchLeftHeld = true;
                }
                else
                {
                    touchRightHeld = true;
                }

                return new PointerOwner
                {
                    Kind = direction < 0 ? PointerOwnerKind.Left : PointerOwnerKind.Right,
                };
            }
        }

        if (IsPointerOverUi(screenPosition))
        {
            return new PointerOwner { Kind = PointerOwnerKind.Other };
        }

        Level134Step step = ResolveStepAtScreenPosition(screenPosition);
        if (step != null && step.BeginDrag(pointerId, ScreenToWorld(screenPosition)))
        {
            return new PointerOwner { Kind = PointerOwnerKind.Step, Step = step };
        }

        return new PointerOwner { Kind = PointerOwnerKind.Other };
    }

    private void ReleasePointerOwner(int pointerId, PointerOwner owner)
    {
        switch (owner.Kind)
        {
            case PointerOwnerKind.Left:
                touchLeftHeld = false;
                break;
            case PointerOwnerKind.Right:
                touchRightHeld = false;
                break;
            case PointerOwnerKind.Step:
                owner.Step?.EndDrag(pointerId);
                break;
        }

        if (!touchLeftHeld && !touchRightHeld)
        {
            directionChordBlocked = false;
            directionInputArmed = true;
        }
    }

    private void ProcessMouseDrag()
    {
        const int mousePointerId = -1;
        if (Input.GetMouseButtonDown(0)
            && !IsPointerOverUi(Input.mousePosition))
        {
            Level134Step step = ResolveStepAtScreenPosition(Input.mousePosition);
            step?.BeginDrag(mousePointerId, ScreenToWorld(Input.mousePosition));
        }

        if (Input.GetMouseButton(0)
            && activeDraggedStep != null
            && activeDragPointerId == mousePointerId)
        {
            activeDraggedStep.MoveDrag(ScreenToWorld(Input.mousePosition));
        }

        if (Input.GetMouseButtonUp(0)
            && activeDraggedStep != null
            && activeDragPointerId == mousePointerId)
        {
            activeDraggedStep.EndDrag(mousePointerId);
        }
    }

    public bool CanBeginStepDrag(Level134Step step, int pointerId)
    {
        if (!gameplayActive
            || step == null
            || activeDraggedStep != null
            || !IsStepVisible(step))
        {
            return false;
        }

        if (carrierStep != null && carrierStep != step)
        {
            return false;
        }

        return mode != GameplayMode.Failure && mode != GameplayMode.Finished
            && mode != GameplayMode.FinalDoorDrop;
    }

    public void NotifyStepDragStarted(Level134Step step)
    {
        activeDraggedStep = step;
        activeDragPointerId = step.DragPointerId;
        if (landedCarrierStep == step)
        {
            landedCarrierStep = null;
        }

        dragStartedUnderActor = IsActorStandingOnStep(step);
        if (carrierStep == null && dragStartedUnderActor)
        {
            TryAttachActorToDraggedStep(step);
        }
    }

    public void NotifyDraggedStepMoved(Level134Step step)
    {
        if (step == null || step != activeDraggedStep)
        {
            return;
        }

        if (!IsStepVisible(step))
        {
            step.CancelDrag();
            return;
        }

        if (carrierStep == null)
        {
            TryAttachActorToDraggedStep(step);
        }
    }

    public void NotifyStepDragEnded(Level134Step step, int pointerId)
    {
        if (activeDraggedStep == step)
        {
            activeDraggedStep = null;
            activeDragPointerId = int.MinValue;
            dragStartedUnderActor = false;
        }

        if (step == carrierStep && IsStepSupportedByGround(step))
        {
            ReleaseCarrierOnGround();
        }
    }

    public Vector2 ClampDraggedStepPosition(Level134Step step, Vector2 desiredPosition)
    {
        if (step == null)
        {
            return desiredPosition;
        }

        Vector2 clampedPosition = desiredPosition;
        if (targetCamera != null)
        {
            float halfHeight = targetCamera.orthographicSize;
            float halfWidth = halfHeight * Mathf.Max(0.1f, targetCamera.aspect);
            float left = targetCamera.transform.position.x - halfWidth + dragScreenMargin;
            float right = targetCamera.transform.position.x + halfWidth - dragScreenMargin;
            float top = targetCamera.transform.position.y + halfHeight - dragScreenMargin;

            Bounds visualBounds = step.VisualBounds;
            Vector2 visualOffset = (Vector2)visualBounds.center - (Vector2)step.transform.position;
            float minX = left - visualOffset.x + visualBounds.extents.x;
            float maxX = right - visualOffset.x - visualBounds.extents.x;
            if (minX <= maxX)
            {
                clampedPosition.x = Mathf.Clamp(clampedPosition.x, minX, maxX);
            }

            clampedPosition.y = Mathf.Min(
                clampedPosition.y,
                top - visualOffset.y - visualBounds.extents.y);

            if (step == carrierStep && actorMainCollider != null)
            {
                Bounds actorBounds = actorMainCollider.bounds;
                Vector2 actorOffset = (Vector2)actorBounds.center - (Vector2)step.transform.position;
                float actorMinX = left - actorOffset.x + actorBounds.extents.x;
                float actorMaxX = right - actorOffset.x - actorBounds.extents.x;
                if (actorMinX <= actorMaxX)
                {
                    clampedPosition.x = Mathf.Clamp(clampedPosition.x, actorMinX, actorMaxX);
                }

                clampedPosition.y = Mathf.Min(
                    clampedPosition.y,
                    top - actorOffset.y - actorBounds.extents.y);
            }
        }

        if (realGroundCollider != null && step.Collider != null)
        {
            Bounds groundBounds = realGroundCollider.bounds;
            Bounds stepBounds = step.Collider.bounds;
            float halfWidth = stepBounds.extents.x;
            bool overlapsGroundX = clampedPosition.x + halfWidth >= groundBounds.min.x
                && clampedPosition.x - halfWidth <= groundBounds.max.x;
            float bottomOffset = stepBounds.min.y - step.transform.position.y;
            if (overlapsGroundX && clampedPosition.y + bottomOffset < groundBounds.max.y)
            {
                clampedPosition.y = groundBounds.max.y - bottomOffset;
            }
        }

        return clampedPosition;
    }

    private void TryAttachActorToDraggedStep(Level134Step step)
    {
        if (step == null || !step.IsDragging || !CanActorBePickedUp(step))
        {
            return;
        }

        Bounds actorBounds = actorMainCollider.bounds;
        Bounds stepBounds = step.Collider.bounds;
        float horizontalDelta = Mathf.Abs(actorBounds.center.x - stepBounds.center.x);
        float actorFootY = actorBounds.min.y;
        float stepTopY = stepBounds.max.y;
        float verticalDelta = actorFootY - stepTopY;
        float previousStepTopY = stepTopY - step.LastDragDelta.y;
        bool topNearActorFoot = verticalDelta >= -carrierVerticalTolerance
            && verticalDelta <= carrierTopPenetrationTolerance;
        bool arrivingFromBelow = step.LastDragDelta.y > 0.0001f
            && previousStepTopY < actorFootY
            && topNearActorFoot;

        if (horizontalDelta > carrierHorizontalTolerance
            || (!dragStartedUnderActor && !arrivingFromBelow))
        {
            return;
        }

        carrierStep = step;
        routePermanentlyClosed = true;
        mode = GameplayMode.Carrier;
        step.AttachCarrier();
        cameraFollow.Freeze();
        levelCtrl?.SetCtrlable(false);
        LockActorForClimb();
        SetAllActorStepCollisionIgnored(true);
        PositionActorOnStep(step);
        actor.SpineCtrl?.PlayAnimation(IdleAnimation, true, true);
    }

    private bool IsActorStandingOnStep(Level134Step step)
    {
        if (step == null
            || step.Collider == null
            || actorMainCollider == null
            || actorBody == null
            || actorBody.linearVelocityY > 0.25f)
        {
            return false;
        }

        Bounds actorBounds = actorMainCollider.bounds;
        Bounds stepBounds = step.Collider.bounds;
        float verticalDelta = actorBounds.min.y - stepBounds.max.y;
        return verticalDelta >= -carrierTopPenetrationTolerance
            && verticalDelta <= carrierVerticalTolerance
            && Mathf.Abs(actorBounds.center.x - stepBounds.center.x) <= carrierHorizontalTolerance;
    }

    private bool CanActorBePickedUp(Level134Step candidateStep)
    {
        if (actorMainCollider == null || actorBody == null || smallJumpActive || actor.IsDied)
        {
            return false;
        }

        if (mode == GameplayMode.Climbing)
        {
            return currentStep != null;
        }

        if (mode == GameplayMode.GroundRouteClosed)
        {
            bool stableOnGround = actor.IsGround || IsActorStandingOnStep(candidateStep);
            return stableOnGround && Mathf.Abs(actorBody.linearVelocityY) <= 0.25f;
        }

        return false;
    }

    private void UpdateCarrierActor()
    {
        if (carrierStep == null || mode != GameplayMode.Carrier)
        {
            return;
        }

        PositionActorOnStep(carrierStep);
        actorBody.linearVelocity = Vector2.zero;
        actorBody.angularVelocity = 0f;
        actor.SpineCtrl?.PlayAnimation(IdleAnimation, true, true);
    }

    public void NotifyStepTouchedGround(Level134Step step)
    {
        if (step == null)
        {
            return;
        }

        if (step == carrierStep)
        {
            if (!step.IsDragging)
            {
                ReleaseCarrierOnGround();
            }
        }
        else if (!step.IsDragging)
        {
            step.MarkResting();
        }
    }

    private void ReleaseCarrierOnGround()
    {
        if (carrierStep == null)
        {
            return;
        }

        Level134Step landedCarrier = carrierStep;
        carrierStep = null;
        landedCarrierStep = landedCarrier;
        routePermanentlyClosed = true;
        currentLayer = 0;
        currentStep = null;
        smallJumpActive = false;
        mode = GameplayMode.GroundRouteClosed;
        landedCarrier.MarkResting();

        foreach (KeyValuePair<int, Level134Step> pair in climbSteps)
        {
            pair.Value?.MakeStaticSupport();
        }

        RestoreActorFreeControl();
        SetActorCollisionForCurrentStates();
        PositionActorOnStep(landedCarrier);
        cameraFollow.ResumeAfterGroundLanding();
        levelCtrl?.SetCtrlable(true);
        UpdateProgressText();
    }

    public bool IsRealGround(Collider2D candidate)
    {
        if (candidate == null || realGround == null)
        {
            return false;
        }

        return candidate.transform == realGround || candidate.transform.IsChildOf(realGround);
    }

    private bool IsStepSupportedByGround(Level134Step step)
    {
        if (step == null || step.Collider == null || realGroundCollider == null)
        {
            return false;
        }

        Bounds stepBounds = step.Collider.bounds;
        Bounds groundBounds = realGroundCollider.bounds;
        bool overlapsX = stepBounds.max.x >= groundBounds.min.x
            && stepBounds.min.x <= groundBounds.max.x;
        return overlapsX
            && Mathf.Abs(stepBounds.min.y - groundBounds.max.y) <= groundSupportTolerance;
    }

    private void UpdateStepLifecycle()
    {
        if (targetCamera == null)
        {
            return;
        }

        float cameraBottom = targetCamera.transform.position.y - targetCamera.orthographicSize;
        for (int i = 0; i < allSteps.Count; i++)
        {
            Level134Step step = allSteps[i];
            if (step == null || step.State == Level134Step.StepState.Pooled)
            {
                continue;
            }

            if (step.IsDragging && !IsStepVisible(step))
            {
                step.CancelDrag();
            }

            bool recyclableState = step.State == Level134Step.StepState.Falling
                || step.State == Level134Step.StepState.Resting;
            if (recyclableState
                && step != carrierStep
                && step.VisualBounds.max.y < cameraBottom - offscreenRecycleMargin)
            {
                RecycleStep(step);
            }
        }
    }

    private bool IsStepVisible(Level134Step step)
    {
        if (step == null || targetCamera == null)
        {
            return false;
        }

        Bounds bounds = step.VisualBounds;
        float halfHeight = targetCamera.orthographicSize;
        float halfWidth = halfHeight * targetCamera.aspect;
        Vector3 cameraPosition = targetCamera.transform.position;
        return bounds.max.x >= cameraPosition.x - halfWidth
            && bounds.min.x <= cameraPosition.x + halfWidth
            && bounds.max.y >= cameraPosition.y - halfHeight
            && bounds.min.y <= cameraPosition.y + halfHeight;
    }

    public void SetStepActorCollisionIgnored(Level134Step step, bool ignored)
    {
        if (step == null || step.Collider == null || actorColliders == null)
        {
            return;
        }

        for (int i = 0; i < actorColliders.Length; i++)
        {
            if (actorColliders[i] != null)
            {
                Physics2D.IgnoreCollision(step.Collider, actorColliders[i], ignored);
            }
        }
    }

    internal bool ShouldIgnoreActorCollision(Level134Step step)
    {
        if (step == null)
        {
            return false;
        }

        return !gameplayActive
            || mode == GameplayMode.Failure
            || mode == GameplayMode.Finished
            || mode == GameplayMode.Carrier
            || step.State == Level134Step.StepState.Falling
            || step.State == Level134Step.StepState.Carried
            || step.State == Level134Step.StepState.Pooled
            || (mode == GameplayMode.Climbing && smallJumpActive && step == currentStep)
            || (step.State == Level134Step.StepState.Climbable
                && !IsClimbableColliderInActiveWindow(step))
            || (step.State == Level134Step.StepState.ExitPlatform && mode != GameplayMode.FinalDoorDrop);
    }

    private bool IsClimbableColliderInActiveWindow(Level134Step step)
    {
        if (step == null || step.State != Level134Step.StepState.Climbable)
        {
            return false;
        }

        if (mode == GameplayMode.Ground)
        {
            return step.LayerNumber == 1;
        }

        return mode == GameplayMode.Climbing
            && (step.LayerNumber == currentLayer || step.LayerNumber == currentLayer + 1);
    }

    private void RefreshClimbableColliderWindow()
    {
        foreach (KeyValuePair<int, Level134Step> pair in climbSteps)
        {
            Level134Step step = pair.Value;
            if (step != null && step.State == Level134Step.StepState.Climbable)
            {
                step.SetClimbableColliderEnabled(IsClimbableColliderInActiveWindow(step));
            }
        }
    }

    private void SetAllActorStepCollisionIgnored(bool ignored)
    {
        for (int i = 0; i < allSteps.Count; i++)
        {
            SetStepActorCollisionIgnored(allSteps[i], ignored);
        }
    }

    private void SetActorCollisionForCurrentStates()
    {
        RefreshClimbableColliderWindow();
        for (int i = 0; i < allSteps.Count; i++)
        {
            Level134Step step = allSteps[i];
            if (step == null)
            {
                continue;
            }

            SetStepActorCollisionIgnored(step, ShouldIgnoreActorCollision(step));
        }
    }

    private void PositionActorOnStep(Level134Step step)
    {
        if (step == null || actor == null)
        {
            return;
        }

        Vector3 position = ResolveActorPositionOnStep(step);
        actor.transform.position = position;
        if (actorBody != null)
        {
            actorBody.position = position;
        }
    }

    private Vector3 ResolveActorPositionOnStep(Level134Step step)
    {
        return new Vector3(
            step.Collider.bounds.center.x,
            step.TopY + actorFootOffset,
            actor.transform.position.z);
    }

    private void ResolveMoveButtons()
    {
        if (leftMoveButtonRect != null && rightMoveButtonRect != null)
        {
            return;
        }

        GamePlayPanel panel = FindFirstObjectByType<GamePlayPanel>();
        if (panel == null)
        {
            return;
        }

        RectTransform[] rects = panel.GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            if (rects[i].name == "leftMove_btn")
            {
                leftMoveButtonRect = rects[i];
            }
            else if (rects[i].name == "rightMove_btn")
            {
                rightMoveButtonRect = rects[i];
            }
        }
    }

    private int ResolveMoveButtonDirection(Vector2 screenPosition)
    {
        if (RectangleContainsScreenPoint(leftMoveButtonRect, screenPosition))
        {
            return -1;
        }

        return RectangleContainsScreenPoint(rightMoveButtonRect, screenPosition) ? 1 : 0;
    }

    private static bool RectangleContainsScreenPoint(RectTransform rect, Vector2 screenPosition)
    {
        if (rect == null)
        {
            return false;
        }

        Canvas canvas = rect.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPosition, eventCamera);
    }

    private bool IsPointerOverUi(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        PointerEventData data = new PointerEventData(EventSystem.current) { position = screenPosition };
        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(data, uiRaycastResults);
        return uiRaycastResults.Count > 0;
    }

    private Level134Step ResolveStepAtScreenPosition(Vector2 screenPosition)
    {
        Vector2 worldPosition = ScreenToWorld(screenPosition);
        Level134Step selected = null;
        int bestOrder = int.MinValue;
        for (int i = 0; i < allSteps.Count; i++)
        {
            Level134Step step = allSteps[i];
            if (step == null || !step.CanBeDragged || !IsStepVisible(step)
                || step.Renderer == null || !step.Renderer.enabled || step.Renderer.sprite == null)
            {
                continue;
            }

            // Pick the whole visible step in XY, independently of its thin support collider.
            Bounds bounds = step.VisualBounds;
            if (worldPosition.x < bounds.min.x || worldPosition.x > bounds.max.x
                || worldPosition.y < bounds.min.y || worldPosition.y > bounds.max.y)
            {
                continue;
            }

            int order = step.Renderer.sortingOrder;
            if (selected == null || order > bestOrder)
            {
                selected = step;
                bestOrder = order;
            }
        }

        return selected;
    }

    private Vector2 ScreenToWorld(Vector2 screenPosition)
    {
        Vector3 world = targetCamera.ScreenToWorldPoint(new Vector3(
            screenPosition.x,
            screenPosition.y,
            -targetCamera.transform.position.z));
        return world;
    }

    private void InitializeCameraHud()
    {
        if (rewardProgressText == null)
        {
            Debug.LogError("[Level134] Assign the authored camera HUD text reference in the level Prefab.", this);
        }

        if (rewardProgressLocalize == null)
        {
            Debug.LogError("[Level134] Assign the camera HUD Localize reference in the level Prefab.", this);
        }

        CaptureCameraFixedLayout();
        UpdateCameraFixedLayout(true);
    }

    #region 进度文本多语言

    private void UpdateProgressText()
    {
        rewardProgressLocalize?.OnLocalize(true);
    }

    /// <summary>
    /// 供本关文字的 I2 LocalizeEvent 调用，在排版前填入当前层数和总层数；仅运行态执行。
    /// </summary>
    public void FormatProgressTranslation()
    {
        if (!Application.isPlaying || string.IsNullOrEmpty(Localize.MainTranslation))
        {
            return;
        }

        Localize.MainTranslation = string.Format(
            CultureInfo.InvariantCulture,
            Localize.MainTranslation,
            Mathf.Clamp(currentLayer, 0, TotalLayers),
            TotalLayers);
    }

    #endregion
    private void CreateCameraBottomFallArea()
    {
        GameObject fallArea = new GameObject(
            "Level134CameraBottomFallArea",
            typeof(BoxCollider2D),
            typeof(FailAreaTriggerHandle));
        fallArea.layer = fallAreaLayer;
        fallArea.transform.SetParent(targetCamera.transform, false);
        fallArea.transform.localPosition = new Vector3(
            0f,
            -targetCamera.orthographicSize - 1f,
            -targetCamera.transform.position.z);
        cameraBottomFallAreaCollider = fallArea.GetComponent<BoxCollider2D>();
        cameraBottomFallAreaCollider.isTrigger = true;
        cameraBottomFallAreaCollider.size = new Vector2(
            targetCamera.orthographicSize * targetCamera.aspect * 2f + 4f,
            2f);
    }

    private void CaptureCameraFixedLayout()
    {
        if (cameraLayoutCaptured)
        {
            return;
        }

        if (successDoor != null)
        {
            successDoorAuthoredLocalPosition = successDoor.localPosition;
        }

        if (rewardProgressText != null)
        {
            rewardProgressTextAuthoredLocalPosition = rewardProgressText.transform.localPosition;
        }

        cameraLayoutCaptured = true;
    }

    private void UpdateCameraFixedLayout(bool force = false)
    {
        if (targetCamera == null)
        {
            return;
        }

        CaptureCameraFixedLayout();
        float aspect = targetCamera.aspect;
        float orthographicSize = targetCamera.orthographicSize;
        if (!force
            && Mathf.Approximately(lastCameraLayoutAspect, aspect)
            && Mathf.Approximately(lastCameraLayoutOrthographicSize, orthographicSize))
        {
            return;
        }

        lastCameraLayoutAspect = aspect;
        lastCameraLayoutOrthographicSize = orthographicSize;
        if (successDoor != null)
        {
            successDoor.localPosition = ResolveCameraAnchoredLocalPosition(successDoorAuthoredLocalPosition);
        }

        if (rewardProgressText != null)
        {
            rewardProgressText.transform.localPosition = ResolveCameraAnchoredLocalPosition(rewardProgressTextAuthoredLocalPosition);
        }

        if (cameraBottomFallAreaCollider != null)
        {
            cameraBottomFallAreaCollider.transform.localPosition = new Vector3(
                0f,
                -orthographicSize - 1f,
                -targetCamera.transform.position.z);
            cameraBottomFallAreaCollider.size = new Vector2(
                orthographicSize * aspect * 2f + 4f,
                2f);
        }
    }

    private Vector3 ResolveCameraAnchoredLocalPosition(Vector3 authoredLocalPosition)
    {
        float referenceAspect = Mathf.Max(0.1f, cameraLayoutReferenceAspect);
        float referenceSize = Mathf.Max(0.1f, cameraLayoutReferenceOrthographicSize);
        float verticalScale = targetCamera.orthographicSize / referenceSize;
        float horizontalScale = verticalScale * targetCamera.aspect / referenceAspect;
        return new Vector3(
            authoredLocalPosition.x * horizontalScale,
            authoredLocalPosition.y * verticalScale,
            authoredLocalPosition.z);
    }

    private void PrepareBackgroundLoop()
    {
        if (commonBackground == null)
        {
            return;
        }

        backgroundInitialY = commonBackground.position.y;
        backgroundCache = Instantiate(commonBackground.gameObject, transform).transform;
        backgroundCache.name = "CommonBg_Level134Cache";
        // Clone the authored SmoothColorByHeight component along with the tile.
        // As in Level 106, its runtime Start switches the ordinary editor
        // material to the gradient, and Update supplies the world-Y parameters.
        UpdateBackgroundLoop();
    }

    private void UpdateBackgroundLoop()
    {
        if (commonBackground == null || backgroundCache == null || targetCamera == null)
        {
            return;
        }

        const float tileSize = 50f;
        float cameraY = targetCamera.transform.position.y;
        double originY = backgroundInitialY + worldOffsetY;
        float lowerTileY = (float)(originY
            + System.Math.Floor((cameraY - originY + tileSize * 0.5f) / tileSize) * tileSize);
        SetWorldY(commonBackground, lowerTileY);
        SetWorldY(backgroundCache, lowerTileY + (cameraY >= lowerTileY ? tileSize : -tileSize));
    }

    private static void SetWorldY(Transform target, float y)
    {
        Vector3 position = target.position;
        position.y = y;
        target.position = position;
    }

    private void InitializeMovingWorld()
    {
        // One translated parent preserves local animation origins for the near-ground scenery,
        // stars and spawn door. The summit, both background tiles and camera stay outside it.
        movingWorldRoot = new GameObject("Level134MovingWorld").transform;
        movingWorldRoot.SetParent(transform, false);
        List<Transform> children = new List<Transform>();
        foreach (Transform child in transform)
        {
            if (child != movingWorldRoot && child != commonBackground && child != backgroundCache
                && child != targetCamera.transform && child != endingRoot && child != summitScenery
                && child != finalDoor && child != successDoor)
            {
                children.Add(child);
            }
        }
        foreach (Transform child in children)
        {
            if (child.name == "FallArea" || child.name == "FallArea (1)")
                sideFallAreas.AddRange(child.GetComponentsInChildren<BoxCollider2D>(true));
            child.SetParent(movingWorldRoot, true);
        }
        actorInitialParent = actor.transform.parent;
    }

    private bool CaptureBackgroundGradients()
    {
        if (backgroundGradients.Count > 0) return true;
        // SmoothColorByHeight.Start must have installed the gradient material first.
        foreach (Transform tile in new[] { commonBackground, backgroundCache })
        {
            if (tile == null) continue;
            foreach (SpriteRenderer renderer in tile.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Material material = renderer.sharedMaterial;
                if (material == null || !material.HasProperty(StartHeightId)
                    || !material.HasProperty(EndHeightId)) continue;
                backgroundGradients.Add(new BackgroundGradient
                {
                    Renderer = renderer,
                    Properties = new MaterialPropertyBlock(),
                    StartHeight = material.GetFloat(StartHeightId),
                    EndHeight = material.GetFloat(EndHeightId),
                });
            }
        }
        if (backgroundGradients.Count >= 2) return true;
        backgroundGradients.Clear();
        return false;
    }

    private void UpdateDeepSpaceLoop()
    {
        if (mode != GameplayMode.Climbing || routePermanentlyClosed || finalApproach
            || !summitLayoutReady || currentStep == null || smallJumpActive || smallJumpPending
            || activeDraggedStep != null || !CaptureBackgroundGradients()) return;

        float darkHeight = float.NegativeInfinity;
        foreach (BackgroundGradient gradient in backgroundGradients)
            darkHeight = Mathf.Max(darkHeight, gradient.EndHeight);
        // The entry stays in the fixed summit coordinate system. The ground/gradient move away
        // below it, never back into the viewport. Budget includes the unmodified remaining steps.
        float entryBottom = darkHeight + Mathf.Max(1f, deepSpaceBufferDistance);
        float cameraBottom = targetCamera.transform.position.y - targetCamera.orthographicSize;
        if (cameraBottom <= entryBottom) return;

        float predictedFinalCameraY = cameraFollow.ResolveCameraY((float)(
            firstStepPosition.y + (TotalLayers - 1) * (double)verticalStepHeight + worldOffsetY)
            + (currentStep.TopY - currentStep.transform.position.y) + actorFootOffset);
        float remainingShift = predictedFinalCameraY - summitCameraY;
        if (remainingShift <= 0.001f)
        {
            BeginFinalApproach();
            return;
        }

        float margin = Mathf.Max(0.1f, sceneryOffscreenMargin);
        float safeUpperBottom = summitVisualBottomY - 2f * targetCamera.orthographicSize
            - smallJumpArcHeight - verticalStepHeight - margin;
        float rebaseAtBottom = Mathf.Min(entryBottom + deepSpaceRebaseDistance + margin, safeUpperBottom);
        if (cameraBottom < rebaseAtBottom) return;

        float distance = Mathf.Min(Mathf.Max(1f, deepSpaceRebaseDistance), remainingShift,
            cameraBottom - entryBottom - margin);
        if (distance <= 0.001f) return;
        ApplyClimbWorldShift(-distance);
        if (remainingShift - distance <= 0.001f) BeginFinalApproach();
    }

    private void ApplyClimbWorldShift(float deltaY)
    {
        Rigidbody2D[] bodies = movingWorldRoot.GetComponentsInChildren<Rigidbody2D>(true);
        Vector2[] positions = new Vector2[bodies.Length];
        for (int i = 0; i < bodies.Length; i++) positions[i] = bodies[i].position;
        movingWorldRoot.position += Vector3.up * deltaY;
        for (int i = 0; i < bodies.Length; i++)
        {
            RigidbodyInterpolation2D interpolation = bodies[i].interpolation;
            bodies[i].interpolation = RigidbodyInterpolation2D.None;
            bodies[i].position = positions[i] + Vector2.up * deltaY;
            bodies[i].interpolation = interpolation;
        }
        worldOffsetY += deltaY;
        cameraFollow.ApplyWorldShift(deltaY);
        // Keep each authored side boundary's top fixed at the summit while extending its
        // bottom with the ground. The camera-bottom failure trigger moves with the camera.
        foreach (BoxCollider2D area in sideFallAreas)
        {
            float localDistance = -deltaY / Mathf.Max(0.001f, Mathf.Abs(area.transform.lossyScale.y));
            area.offset += Vector2.up * (localDistance * 0.5f);
            area.size += Vector2.up * localDistance;
        }
        foreach (BackgroundGradient gradient in backgroundGradients)
        {
            gradient.Renderer.GetPropertyBlock(gradient.Properties);
            gradient.Properties.SetFloat(StartHeightId, (float)(gradient.StartHeight + worldOffsetY));
            gradient.Properties.SetFloat(EndHeightId, (float)(gradient.EndHeight + worldOffsetY));
            gradient.Renderer.SetPropertyBlock(gradient.Properties);
        }
        UpdateBackgroundLoop();
        Physics2D.SyncTransforms();
    }

    private void BeginFinalApproach()
    {
        finalApproach = true;
        // The authored summit has never moved. Restore it while its lowest renderer is still
        // above the viewport; the remaining real jumps carry the camera through the whole layout.
        summitScenery.gameObject.SetActive(true);
    }

    private void InitializeAuthoredEnding()
    {
        CancelFinalDoorDrop();
        if (endingRoot == null)
        {
            Debug.LogError("[Level134] Assign the authored ending in the level prefab.", this);
            return;
        }

        if (successDoor != null)
        {
            // The prize door is authored under GameCamera. Preserve that layout; converting
            // it through the empty former ending group would move it offscreen a second time.
            if (successDoor.parent != targetCamera.transform)
                successDoor.SetParent(targetCamera.transform, true);
        }
        else
        {
            Debug.LogError("[Level134] Missing independent prize door_0 reference.", this);
        }

        endingRoot.gameObject.SetActive(false);

        if (summitScenery == null)
        {
            Debug.LogError("[Level134] Assign the authored Scene sky group as Summit Scenery.", this);
            return;
        }
        summitVisualBottomY = float.PositiveInfinity;
        float topY = float.NegativeInfinity;
        foreach (Renderer renderer in summitScenery.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            summitVisualBottomY = Mathf.Min(summitVisualBottomY, renderer.bounds.min.y);
            topY = Mathf.Max(topY, renderer.bounds.max.y);
        }
        summitLayoutReady = !float.IsInfinity(summitVisualBottomY) && !float.IsInfinity(topY);
        summitCameraY = topY - targetCamera.orthographicSize + summitTopInset;
        summitScenery.gameObject.SetActive(false);
        if (!summitLayoutReady)
            Debug.LogError("[Level134] Summit Scenery has no enabled authored renderers.", this);
    }

    /// <summary>
    /// 本关门 Trigger 的同步入口：沿用标准成功判定，并在本次碰撞回调内交出 Actor 的物理控制。
    /// </summary>
    public void OnActorEnterSuccessDoor(Collider2D collider)
    {
        if (mode == GameplayMode.FinalDoorDrop) return;
        TryEnterDoor(collider, null);
    }

    public void OnActorEnterFinalDoor(Collider2D collider)
    {
        if (mode != GameplayMode.FinalDoorDrop || !finalDoorReady || !gameplayActive
            || finalDoorSuccessAnim == null)
        {
            return;
        }
        TryEnterDoor(collider, finalDoorSuccessAnim);
    }

    private void TryEnterDoor(Collider2D collider, SuccessAnimCtrl successAnimation)
    {
        if (doorHandoffStarted
            || mode == GameplayMode.Failure
            || collider == null
            || collider != actorMainCollider
            || levelCtrl == null
            || actor == null
            || actorBody == null
            || actor.IsDied)
        {
            return;
        }

        if (successAnimation != null)
        {
            levelCtrl.SetSuccessAnim(successAnimation);
        }
        levelCtrl.OnActorTrigger2D(collider);
        if (levelCtrl.IsActorEnterDoor)
        {
            BeginDoorHandoff();
        }
    }

    private void BeginDoorHandoff()
    {
        if (doorHandoffStarted)
        {
            return;
        }

        doorHandoffStarted = true;
        finalDoorDropActive = false;
        finalDoorReady = false;
        mode = GameplayMode.Finished;
        SetStepShadowSuppressed(false);
        SetClimbPushCollisionSuppressed(false);
        gameplayActive = false;

        // SuccessAnimCtrl 先开门再由 ActorDestroyAnimHandle 移动 Transform。
        // 在取消载运之前停止物理；整个交接期间既不恢复重力，也不继续写 Actor 位置。
        levelCtrl?.SetCtrlable(false);
        actor.SetMoveInput(0f);
        actor.StopMoving();
        actorBody.linearVelocity = Vector2.zero;
        actorBody.angularVelocity = 0f;
        actorBody.gravityScale = 0f;
        actorBody.simulated = false;
        actorPauseStateCaptured = false;
        actor.transform.SetParent(actorInitialParent, true);

        if (carrierStep != null)
        {
            carrierStep.FreezeForDoor();
            carrierStep = null;
        }

        activeDraggedStep?.InterruptDrag();
        activeDraggedStep = null;
        activeDragPointerId = int.MinValue;
        dragStartedUnderActor = false;
        pointerOwners.Clear();
        touchLeftHeld = false;
        touchRightHeld = false;
        directionChordBlocked = false;
        directionInputArmed = true;
        smallJumpPending = false;
        smallJumpActive = false;
        cameraFollow.Freeze();
        for (int i = 0; i < allSteps.Count; i++)
        {
            allSteps[i]?.SetSuspended(true);
        }
        SetActorCollisionForCurrentStates();
        // 进门吸入期间保留距离文字，与气泡随关卡一同退场，避免文字突然消失。
    }

    private void OnFailureStarted(OnTriggerTrapEvent evt)
    {
        EnterFailureState();
    }

    private void OnFailureStarted(OnTriggerFailAreaEvent evt)
    {
        EnterFailureState();
    }

    private void EnterFailureState()
    {
        if (doorHandoffStarted)
        {
            return;
        }

        mode = GameplayMode.Failure;
        SetStepShadowSuppressed(false);
        CancelFinalDoorDrop();
        CloseRealTipsPopup();
        SetClimbPushCollisionSuppressed(false);
        SetGameplayActive(false);
        smallJumpPending = false;
        smallJumpActive = false;
        smallJumpTarget = null;
        cameraFollow.Freeze();
        for (int i = 0; i < allSteps.Count; i++)
        {
            allSteps[i]?.SetSuspended(true);
        }

        if (actor != null && actorBody != null)
        {
            actor.transform.SetParent(actorInitialParent, true);
            actorBody.simulated = actorInitialSimulated;
            actorBody.bodyType = RigidbodyType2D.Dynamic;
            actorBody.constraints = actorInitialConstraints;
            actorBody.gravityScale = actorInitialGravityScale;
            actorBody.linearVelocity = Vector2.zero;
            actorBody.angularVelocity = 0f;
            actor.SetMoveInput(0f);
            actor.StopMoving();
        }

        actorPauseStateCaptured = false;
    }

    private void OnGameReset(GameResetEvent evt)
    {
        CancelFinalDoorDrop();
        CloseRealTipsPopup();
        EnterFailureState();
    }

    private void OnGamePause(GamePauseEvent evt)
    {
        SetGameplayActive(false);
    }

    private void OnGameResume(GameResumeEvent evt)
    {
        if (mode != GameplayMode.Failure && mode != GameplayMode.Finished)
        {
            SetGameplayActive(true);
        }
    }

    private void OnGameStateChanged(SwitchGameStateEvent evt)
    {
        if (evt == null)
        {
            return;
        }

        bool shouldPlay = evt.NewState == GamePlayStateName.PLAYING
            && mode != GameplayMode.Failure
            && mode != GameplayMode.Finished;
        SetGameplayActive(shouldPlay);
    }

    private void SetGameplayActive(bool active)
    {
        if (doorHandoffStarted)
        {
            return;
        }

        if (!active && gameplayActive)
        {
            CaptureActorPauseState();
            smallJumpPending = false;
            activeDraggedStep?.InterruptDrag();
            activeDraggedStep = null;
            activeDragPointerId = int.MinValue;
            dragStartedUnderActor = false;
            pointerOwners.Clear();
            endedPointerIds.Clear();
            touchLeftHeld = false;
            touchRightHeld = false;
            directionChordBlocked = false;
            directionInputArmed = true;
        }

        gameplayActive = active;
        if (active)
        {
            RestoreActorPauseState();
        }

        if (cameraFollow != null)
        {
            cameraFollow.enabled = active;
        }

        for (int i = 0; i < allSteps.Count; i++)
        {
            allSteps[i]?.SetSuspended(!active);
        }
    }

    private void CaptureActorPauseState()
    {
        // Before the first step, the shared entrance owns movement and visibility. Loading
        // PAUSE does not pause its fade-in timer, so keep physics running to settle the actor
        // while hidden. Do not capture/restore a stale velocity or unlock entrance input here.
        // GroundRouteClosed is deliberately excluded: carrier landing keeps its pause contract.
        if (mode == GameplayMode.Ground)
        {
            return;
        }

        if (actorPauseStateCaptured || actorBody == null)
        {
            return;
        }

        actorPauseStateCaptured = true;
        actorSimulatedBeforePause = actorBody.simulated;
        actorVelocityBeforePause = actorBody.linearVelocity;
        actorAngularVelocityBeforePause = actorBody.angularVelocity;
        actorBody.simulated = false;
        actor.SetMoveInput(0f);
        actor.StopMoving();
    }

    private void RestoreActorPauseState()
    {
        if (!actorPauseStateCaptured || actorBody == null)
        {
            return;
        }

        actorBody.simulated = actorSimulatedBeforePause;
        if (actorSimulatedBeforePause)
        {
            actorBody.linearVelocity = actorVelocityBeforePause;
            actorBody.angularVelocity = actorAngularVelocityBeforePause;
        }

        actorPauseStateCaptured = false;
        if (mode == GameplayMode.Climbing || mode == GameplayMode.Carrier
            || mode == GameplayMode.FinalDoorDrop)
        {
            actor.StopMoving();
        }
        else if (mode != GameplayMode.Failure && mode != GameplayMode.Finished)
        {
            actor.ResumeMoving();
        }
    }

    private static Collider2D ResolveActorMainCollider(Collider2D[] colliders)
    {
        if (colliders == null)
        {
            return null;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider != null
                && collider.enabled
                && !collider.isTrigger
                && collider.transform.GetComponent<ActorCtrl>() != null)
            {
                return collider;
            }
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider != null && collider.enabled && !collider.isTrigger)
            {
                return collider;
            }
        }

        return null;
    }

    private void OnDisable()
    {
        EventsUtils.ResetEvents(ref eventSubscriber);
        SetStepShadowSuppressed(false);
        if (!Application.isPlaying)
        {
            return;
        }

        if (!doorHandoffStarted)
        {
            CancelFinalDoorDrop();
        }
        CloseRealTipsPopup();
        SetClimbPushCollisionSuppressed(false);
        if (actor != null && actorBody != null && mode != GameplayMode.Finished)
        {
            SetAllActorStepCollisionIgnored(false);
            actor.transform.SetParent(actorInitialParent, true);
            actorBody.bodyType = actorInitialBodyType;
            actorBody.constraints = actorInitialConstraints;
            actorBody.gravityScale = actorInitialGravityScale;
            actorBody.simulated = actorInitialSimulated;
            actor.SetMoveInput(0f);
        }
    }
}
