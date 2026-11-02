using System.Collections.Generic;
using Bear.EventSystem;
using DG.Tweening;
using Game.Events;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using I2.Loc;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Level 131「火箭加速前进」控制器。
/// 初始场景允许 Actor 拾取火箭背包进行长距离飞行，或把火箭拖到方块背后进行快速推进。
/// 首轮五对刺柱保留原布局，两批完整内容在离屏后向前循环复用，每批按五对洗牌。
/// 终点阶段只启用独立的终点方块与右门。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level131Ctrl : MonoBehaviour, IEventSender
{
    private enum Route
    {
        None = 0,
        Backpack = 1,
        Platform = 2,
    }

    private enum Phase
    {
        Initial = 0,
        Ready = 1,
        Running = 2,
        Terminal = 3,
        Failed = 4,
    }

    private const float GoalDistance = 10000f;
    private const float MileagePerWorldUnit = 10f;
    private const int InitialObstacleGroupCount = 5;
    private const float TerminalEntryGap = 0.5f;
    private const float PlatformTerminalGap = 1f;
    private const float PlatformTerminalPositionTolerance = 0.001f;
    private const float TerminalViewRightMargin = 1f;
    private const float ObstacleSpawnLeadSeconds = 0.5f;

    [Header("Serialized Scene Layout")]
    [SerializeField] private Transform serializedLayoutRoot;
    [SerializeField] private Level131RocketInteraction rocketInteraction;
    [SerializeField] private GameObject rocketObject;

    [Header("Obstacle Groups")]
    [Tooltip("循环批次内各对刺柱包络之间的水平净空；不改变首轮布局或组内错位。")]
    [SerializeField, Min(0f)] private float obstacleGroupGap = 2.5f;
    [SerializeField, Min(1f)] private float obstacleSpawnAheadDistance = 24f;

    [Header("Shared References")]
    [SerializeField] private BaseLevelCtrl levelCtrl;
    [SerializeField] private ParticleSystem debrisBurstPrefab;
    [SerializeField] private ParticleSystem commonHitEffectPrefab;
    [SerializeField] private Texture2D platformFlameAtlas;
    [SerializeField] private Sprite rocketControlIcon;

    [Header("World Distance Text (Scene Editable)")]
    [SerializeField] private TextMeshPro distanceText;
    [SerializeField] private Localize distanceLocalize;

    [Header("Backpack Flight")]
    [SerializeField, Min(0.01f)] private float backpackWorldSpeed = 4f;
    [SerializeField, Min(0.01f)] private float backpackThrustSpeed = 4.8f;
    [SerializeField, Min(0f)] private float backpackGravityScale = 4.5f;

    [Header("Platform Acceleration")]
    [SerializeField, Min(0.01f)] private float platformInitialWorldSpeed = 5f;
    [SerializeField, Min(0.01f)] private float platformAccelerationPerPress = 11.875f;
    [SerializeField, Min(0.01f)] private float platformMaximumWorldSpeed = 100f;

    [Header("Terminal Preview")]
    [Tooltip("终点预告时至少放在镜头右侧外多少屏；已生成障碍更远时继续留出安全净空。")]
    [SerializeField, Min(0.25f)] private float terminalPreviewScreens = 1f;

    [Header("World Layout")]
    [SerializeField] private float waterSurfaceY = -5.75f;
    [SerializeField, Min(0.5f)] private float waterHeight = 3.6f;
    [SerializeField, Min(0.5f)] private float fallAreaDepth = 2.2f;
    [SerializeField, Min(0f)] private float fallAreaBelowWater = 0.35f;
    [SerializeField] private float cameraFollowOffsetX = 3.5f;
    [SerializeField, Min(1f)] private float rebaseTriggerDistance = 18f;
    [SerializeField, Min(1f)] private float rebaseAmount = 12f;

    private readonly List<Level131ObstacleColumn> obstacleColumns = new();
    private readonly List<ObstacleGroup> obstacleGroups = new();
    // 固定两批，每批包含五对刺柱；仅初始化第二批，推进中只移动和恢复已有实例。
    private readonly List<ObstacleBatch> obstacleBatches = new(2);
    private readonly List<int> obstacleShuffleBag = new();
    private readonly List<RaycastHit2D> obstacleSweepHits = new(64);
    private readonly ContactFilter2D obstacleSweepFilter = new() { useTriggers = true };
    private int obstacleShuffleCursor;
    private float nextObstacleGroupLeftX;
    // 最后一批若会进入终点预留画面，则整批留在原处，后续不再尝试前移。
    private bool obstacleLoopCompleted;

    private sealed class ObstacleBatch
    {
        public readonly List<ObstacleGroup> Groups = new(InitialObstacleGroupCount);
        public float RightX;
    }

    private sealed class ObstacleGroup
    {
        public Level131ObstacleColumn First;
        public Level131ObstacleColumn Second;
        public float LeftX;
        public float RightX;
    }

    private EventSubscriber eventSubscriber;
    private Route route;
    private Phase phase;
    private ActorCtrl actor;
    private Level131ActorCtrl levelActor;
    private Rigidbody2D actorBody;
    private Collider2D actorCollider;
    private Camera worldCamera;

    private Transform obstacleRoot;
    private Transform terminalRoot;
    private GameObject platformObject;
    private GameObject terminalPlatformObject;
    private BoxCollider2D terminalPlatformCollider;
    private bool terminalPlatformColliderInitiallyEnabled;
    private Level131PlatformMover platformMover;
    private Level131PlatformSpike platformSpike;
    private Level131RocketFlame rocketFlame;
    private SpriteRenderer platformRocketRenderer;
    private Renderer rocketSpineRenderer;
    private Level131Water water;
    private BoxCollider2D fallAreaCollider;
    private Level131BackgroundLoop backgroundLoop;
    private Level131DebrisBurst debrisBursts;
    private ParticleSystem playerHitEffect;
    private bool playerHitEffectPlayed;

    private Transform doorRoot;
    private Renderer terminalDoorBodyRenderer;
    private Vector3 terminalDoorRightLocalPoint;
    private bool terminalDoorRightCaptured;
    private bool terminalCameraWarningReported;
    private Transform startDoorRoot;
    private Vector3 startDoorPlatformOffset;
    private bool startDoorFollowsPlatform;
    private Collider2D doorTrigger;
    private OnTrigger2DHandle doorTriggerHandle;

    private GamePlayPanel boundPanel;
    private CustomButton jumpButton;
    private Image jumpIcon;
    private Sprite originalJumpIcon;
    private Quaternion originalJumpIconRotation;
    private Vector3 originalJumpIconScale;
    private bool originalJumpIconPreserveAspect;
    private bool originalJumpIconRaycastTarget;
    private bool rocketIconApplied;
    private Transform leftMoveButton;
    private Transform rightMoveButton;
    private bool distanceFormatWarningReported;

    private float initialActorGravityScale;
    private float nextRebaseWorldX;
    private float distance;
    private float platformSpeed;
    private float cameraInitialY;
    private float cameraInitialZ;
    private float platformCameraOffsetX;
    private bool platformCameraAnchored;
    private bool thrustHeld;
    private bool gamePaused;
    private bool failureStarted;
    private bool terminalStarted;
    private bool terminalPreviewStarted;
    private Vector3 previousActorLocalPosition;
    private Vector3 previousPlatformLocalPosition;
    private bool routeMotionSampleValid;
    // 背包待启动时仍会受重力运动，因此必须保留平台碰撞与死亡判定。
    private bool IsBackpackReady => route == Route.Backpack && phase == Phase.Ready;
    private int lastShownDistance;
    private Vector3 rocketDragOffset;
    private Vector3 rocketDragOrigin;
    private Transform rocketDragOriginParent;
    private Vector3 rocketDragOriginLocalPosition;

    public ActorCtrl Actor => actor;

    public float Distance => distance;

    public bool CanBeginRocketDrag(Level131RocketInteraction interaction)
    {
        return interaction != null && route == Route.None && phase == Phase.Initial &&
               rocketInteraction == interaction && !failureStarted;
    }

    private void Awake()
    {
        ResolveReferences();
        ResolveSerializedLayout();
        CacheDoorState();
        lastShownDistance = -1;
        // 开场即显示初始里程，不等待拾取或安装火箭。
        UpdateDistanceText();
    }

    private void Start()
    {
        ResolveReferences();
        PrepareSerializedLayout();
        PrepareDebrisBursts();
        backgroundLoop = new Level131BackgroundLoop(transform, worldCamera);
        BindEvents();
        ResolvePanelBinding();
        SetInitialControls();
        nextRebaseWorldX = actor != null
            ? actor.transform.position.x + rebaseTriggerDistance
            : float.PositiveInfinity;
    }

    private void Update()
    {
        debrisBursts?.Tick();
        ResolvePanelBinding();

        if ((phase == Phase.Running || phase == Phase.Terminal || IsBackpackReady) && actor != null && actor.IsDied)
        {
            StopRunWithoutDispatch();
        }

        UpdateDistanceText();
    }

    private void FixedUpdate()
    {
        if (IsBackpackReady && !failureStarted && !gamePaused)
            CheckActorObstacleCollisions();

        if (phase != Phase.Running || failureStarted || terminalStarted || gamePaused)
            return;

        float deltaTime = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        AccumulateActualDistance();
        if (route == Route.Backpack)
        {
            TickBackpackFlight(deltaTime);
            SweepTravelledObstacles(actorCollider, previousActorLocalPosition, false);
            CheckActorObstacleCollisions();
        }
        else if (route == Route.Platform)
        {
            rocketFlame?.SetPropulsion(true, GetPlatformSpeedFactor());
            SweepTravelledObstacles(platformSpike != null ? platformSpike.HitCollider : null,
                previousPlatformLocalPosition, true);
            CheckPlatformObstacleCollisions();
            SweepTravelledObstacles(actorCollider, previousActorLocalPosition, false);
            CheckActorObstacleCollisions();
        }

        if (phase != Phase.Running || failureStarted)
            return;

        TryPrepareTerminalPreview();
        if (route == Route.Platform)
        {
            if (!terminalPreviewStarted && distance >= GoalDistance)
                TryPrepareTerminalPreview(true);
            if (TryGetPlatformTerminalDistance(out float remainingToGround) &&
                remainingToGround <= PlatformTerminalPositionTolerance)
            {
                BeginTerminal();
                return;
            }
        }
        else if (distance >= GoalDistance)
        {
            BeginTerminal();
            return;
        }

        CaptureRouteMotionSamples();
        MaybeRebaseWorld();
        RecycleObstacleBatches();
        // 唯一移动调度：末段按实际地面边缘限位，不因里程封顶而提前停在离地面更远的位置。
        if (route == Route.Platform)
        {
            float travelLimit = GetRemainingWorldDistance();
            if (terminalPreviewStarted)
                travelLimit = TryGetPlatformTerminalDistance(out float remainingToGround) ? remainingToGround : 0f;
            platformMover?.MoveStep(travelLimit);
        }
    }

    private void LateUpdate()
    {
        FollowPlatformStartDoor();
        if (worldCamera == null)
            return;

        if (phase == Phase.Running || phase == Phase.Terminal)
        {
            Transform target = phase == Phase.Running && route == Route.Platform && platformObject != null
                ? platformObject.transform
                : actor != null ? actor.transform : null;
            if (target != null)
            {
                Vector3 cameraPosition = worldCamera.transform.position;
                if (phase == Phase.Running && route == Route.Platform && platformObject != null)
                {
                    // 和 StartDoor 一样读取平台已插值的显示 Transform，不读取离散物理步位置。
                    // 首次保留当前构图，此后固定偏移；不再叠一层逐帧 18% 的滞后追赶。
                    if (!platformCameraAnchored)
                        AnchorPlatformCamera();
                    cameraPosition.x = target.position.x + platformCameraOffsetX;
                }
                else
                {
                    float desiredX = target.position.x + cameraFollowOffsetX;
                    cameraPosition.x = Mathf.Lerp(cameraPosition.x, desiredX, 0.18f);
                }
                cameraPosition.y = cameraInitialY;
                cameraPosition.z = cameraInitialZ;
                worldCamera.transform.position = cameraPosition;
            }
        }

        ClampTerminalCameraRight();
        backgroundLoop?.Tick();

        if (fallAreaCollider != null)
        {
            float anchorX = actor != null ? actor.transform.position.x : transform.position.x;
            fallAreaCollider.transform.position = new Vector3(
                anchorX,
                waterSurfaceY - fallAreaBelowWater - fallAreaDepth * 0.5f,
                fallAreaCollider.transform.position.z);
        }

        if (water != null)
        {
            float anchorX = actor != null ? actor.transform.position.x : transform.position.x;
            water.transform.position = new Vector3(
                anchorX,
                waterSurfaceY - waterHeight * 0.5f,
                water.transform.position.z);
        }
    }

    #region Initialization

    private void ClampTerminalCameraRight()
    {
        if (!terminalPreviewStarted || doorRoot == null || worldCamera == null)
            return;

        if (!worldCamera.orthographic)
        {
            ReportTerminalCameraWarning("终点右边界需要本关的正交相机，当前相机未应用该限制。");
            return;
        }

        if (!terminalDoorRightCaptured)
        {
            // 只读取 door_0/door 门体，不把 effect、ribbon、粒子或门触发盒算作门图形。
            if (terminalDoorBodyRenderer == null)
            {
                ReportTerminalCameraWarning("缺少 door_0/door 门体 Renderer，终点右边界尚未建立。");
                return;
            }
            if (!terminalDoorBodyRenderer.enabled || !terminalDoorBodyRenderer.gameObject.activeInHierarchy)
                return;

            Bounds bounds = terminalDoorBodyRenderer.bounds;
            float rightX = bounds.max.x;
            // Spine 首次激活可能尚未生成网格；等待有效边界，不能拿门中心或空盒替代。
            if (bounds.size.x <= 0.0001f || float.IsNaN(rightX) || float.IsInfinity(rightX))
                return;

            Vector3 rightPoint = new(rightX, bounds.center.y, bounds.center.z);
            terminalDoorRightLocalPoint = doorRoot.InverseTransformPoint(rightPoint);
            terminalDoorRightCaptured = true;
        }

        // 边界随门根/整关重定位，但不随开门和彩带动画扩大；1 单位留白始终用世界单位。
        float viewRight = doorRoot.TransformPoint(terminalDoorRightLocalPoint).x + TerminalViewRightMargin;
        float halfViewWidth = worldCamera.orthographicSize * worldCamera.aspect;
        Vector3 cameraPosition = worldCamera.transform.position;
        cameraPosition.x = Mathf.Min(cameraPosition.x, viewRight - halfViewWidth);
        worldCamera.transform.position = cameraPosition;
    }

    private void ReportTerminalCameraWarning(string message)
    {
        if (terminalCameraWarningReported)
            return;
        terminalCameraWarningReported = true;
        Debug.LogWarning($"Level 131：{message}", this);
    }

    private void AnchorPlatformCamera()
    {
        if (worldCamera == null || platformObject == null)
            return;

        platformCameraOffsetX = worldCamera.transform.position.x - platformObject.transform.position.x;
        platformCameraAnchored = true;
    }

    private void BindPlatformStartDoor()
    {
        if (startDoorRoot == null || platformObject == null)
        {
            Debug.LogWarning("Level 131 方块路线缺少 StartDoor 或平台，无法绑定起点门随行。", this);
            return;
        }

        // 保留原父层级、SortingGroup 和 BaseLevelCtrl 入场引用；只缓存相对平台的位置。
        startDoorPlatformOffset = platformObject.transform.InverseTransformPoint(startDoorRoot.position);
        startDoorFollowsPlatform = true;
    }

    private void FollowPlatformStartDoor()
    {
        if (!startDoorFollowsPlatform || route != Route.Platform || startDoorRoot == null || platformObject == null)
            return;

        // Ready、推进、暂停、失败和终点均保持同一关系；门不跟随 Actor 去终点，也不自行积分速度。
        startDoorRoot.position = platformObject.transform.TransformPoint(startDoorPlatformOffset);
    }

    private void ResolveReferences()
    {
        if (levelCtrl == null)
            levelCtrl = GetComponent<BaseLevelCtrl>();

        if (actor == null)
            actor = levelCtrl != null ? levelCtrl.Actor : GetComponentInChildren<ActorCtrl>(true);

        if (actor != null)
        {
            actorBody = actor.RB != null ? actor.RB : actor.GetComponent<Rigidbody2D>();
            actorCollider = actor.GetComponent<Collider2D>();
        }
        BindLevelActor();

        if (worldCamera == null)
        {
            worldCamera = GetComponentInChildren<Camera>(true);
            worldCamera ??= Camera.main;
            worldCamera ??= FindFirstObjectByType<Camera>();
            if (worldCamera != null)
            {
                cameraInitialY = worldCamera.transform.position.y;
                cameraInitialZ = worldCamera.transform.position.z;
            }
        }
    }

    private void CacheDoorState()
    {
        if (doorRoot == null)
            return;

        doorTrigger = doorRoot.GetComponentInChildren<Collider2D>(true);
        if (doorTrigger != null)
            doorTrigger.enabled = false;

        doorTriggerHandle = doorRoot.GetComponent<OnTrigger2DHandle>();
        doorTriggerHandle?.OnEnter.AddListener(OnTerminalDoorEnter);

        doorRoot.gameObject.SetActive(false);
        SuccessAnimCtrl successAnim = doorRoot.GetComponent<SuccessAnimCtrl>();
        if (successAnim != null)
            levelCtrl?.SetSuccessAnim(successAnim);
    }

    private void BindLevelActor()
    {
        Level131ActorCtrl resolved = actor as Level131ActorCtrl;
        if (levelActor == resolved)
            return;
        if (levelActor != null)
        {
            levelActor.Dying -= OnActorDying;
            levelActor.RestoreDefaultAnimation();
        }
        levelActor = resolved;
        if (levelActor != null)
            levelActor.Dying += OnActorDying;
    }

    private void OnActorDying()
    {
        TryPlayMissingPlayerHitEffect();
    }

    private void OnTerminalDoorEnter(Collider2D collider)
    {
        if (phase == Phase.Terminal && !failureStarted && actor != null && !actor.IsDied)
            levelCtrl?.OnActorTrigger2D(collider);
    }

    private void ResolveSerializedLayout()
    {
        if (startDoorRoot == null)
            startDoorRoot = transform.Find("StartDoor");
        if (serializedLayoutRoot == null)
            serializedLayoutRoot = FindChildRecursive(transform, "Level131SceneLayout");

        platformMover = serializedLayoutRoot != null
            ? serializedLayoutRoot.GetComponentInChildren<Level131PlatformMover>(true)
            : null;
        platformSpike = serializedLayoutRoot != null
            ? serializedLayoutRoot.GetComponentInChildren<Level131PlatformSpike>(true)
            : null;
        if (rocketInteraction == null)
            rocketInteraction = FindChildRecursive(transform, "Item_rocket")?.GetComponent<Level131RocketInteraction>();
        if (rocketObject == null && serializedLayoutRoot != null)
            rocketObject = FindChildRecursive(serializedLayoutRoot, "Level131Rocket")?.gameObject;
        platformRocketRenderer = serializedLayoutRoot != null
            ? FindChildRecursive(serializedLayoutRoot, "Level131PlatformRocketSprite")?.GetComponent<SpriteRenderer>()
            : null;
        rocketFlame = serializedLayoutRoot != null
            ? FindChildRecursive(serializedLayoutRoot, "Level131RocketFlame")?.GetComponent<Level131RocketFlame>()
            : null;
        water = serializedLayoutRoot != null
            ? FindChildRecursive(serializedLayoutRoot, "Level131Water")?.GetComponent<Level131Water>()
            : null;
        fallAreaCollider = serializedLayoutRoot != null
            ? FindChildRecursive(serializedLayoutRoot, "Level131FallArea")?.GetComponent<BoxCollider2D>()
            : null;
        obstacleRoot = serializedLayoutRoot != null
            ? FindChildRecursive(serializedLayoutRoot, "Level131ObstacleLoop")
            : null;
        terminalRoot = serializedLayoutRoot != null
            ? FindChildRecursive(serializedLayoutRoot, "Level131TerminalArea")
            : null;
        terminalPlatformObject = terminalRoot != null
            ? FindChildRecursive(terminalRoot, "Level131TerminalPlatform")?.gameObject
            : null;
        doorRoot = terminalRoot != null
            ? FindChildRecursive(terminalRoot, "door_0")
            : FindChildRecursive(transform, "door_0");
        terminalDoorBodyRenderer = doorRoot != null
            ? doorRoot.Find("door")?.GetComponent<Renderer>()
            : null;

    }

    private void PrepareSerializedLayout()
    {
        ResolveSerializedLayout();
        if (levelActor == null)
            Debug.LogError("Level 131 Actor 必须使用 Level131ActorCtrl；不再用关卡 LateUpdate 代写动画。", this);
        if (doorTriggerHandle == null)
            CacheDoorState();

        platformObject = platformMover != null ? platformMover.gameObject : null;
        platformMover?.Configure(actor);
        platformMover?.SetCarryActor(false);

        terminalPlatformCollider = terminalPlatformObject != null
            ? terminalPlatformObject.GetComponentInChildren<BoxCollider2D>(true)
            : null;
        terminalPlatformColliderInitiallyEnabled = terminalPlatformCollider != null && terminalPlatformCollider.enabled;
        if (terminalRoot != null)
            terminalRoot.gameObject.SetActive(false);
        if (!terminalPlatformColliderInitiallyEnabled)
            Debug.LogError("Level 131 终点地面缺少启用的 BoxCollider2D，无法开放终点承托。", this);

        platformSpike?.InitializeSerialized();
        platformSpike?.gameObject.SetActive(false);

        rocketInteraction?.Initialize(this);
        rocketFlame?.Configure(platformFlameAtlas, platformRocketRenderer);
        rocketSpineRenderer = FindMeshRenderer(rocketObject);
        if (rocketSpineRenderer != null)
            rocketSpineRenderer.enabled = false;
        if (platformRocketRenderer != null)
            platformRocketRenderer.enabled = false;
        if (rocketObject != null)
            rocketObject.SetActive(false);

        if (fallAreaCollider != null)
            fallAreaCollider.enabled = true;

        initialActorGravityScale = actorBody != null ? actorBody.gravityScale : backpackGravityScale;
        obstacleColumns.Clear();
        if (obstacleRoot != null)
        {
            foreach (Level131ObstacleColumn column in obstacleRoot.GetComponentsInChildren<Level131ObstacleColumn>(true))
            {
                if (column == null)
                    continue;

                column.InitializeSerialized(this);
                obstacleColumns.Add(column);
            }
        }

        PrepareObstacleBatches();
    }

    private void PrepareObstacleBatches()
    {
        obstacleGroups.Clear();
        obstacleBatches.Clear();
        obstacleShuffleBag.Clear();
        obstacleShuffleCursor = 0;
        obstacleLoopCompleted = false;
        if (obstacleRoot == null || obstacleColumns.Count != InitialObstacleGroupCount * 2)
        {
            Debug.LogError("Level 131 需要首轮十根刺柱，从左到右两根一组；停止循环，保留现场布局。", this);
            return;
        }

        // 配对只读取初始 X；首轮位置和两根柱之间的错位保持原值。
        obstacleColumns.Sort((left, right) => left.transform.localPosition.x.CompareTo(right.transform.localPosition.x));
        Transform firstRoot = new GameObject("Level131ObstacleLoop_A").transform;
        Transform secondRoot = new GameObject("Level131ObstacleLoop_B").transform;
        firstRoot.SetParent(obstacleRoot, false);
        secondRoot.SetParent(obstacleRoot, false);
        ObstacleBatch firstBatch = new();
        ObstacleBatch secondBatch = new();
        int initialColumnCount = obstacleColumns.Count;
        float lastRightX = float.NegativeInfinity;
        for (int index = 0; index < initialColumnCount; index += 2)
        {
            Level131ObstacleColumn first = obstacleColumns[index];
            Level131ObstacleColumn second = obstacleColumns[index + 1];
            Bounds bounds = first.GetLayoutBounds(obstacleRoot);
            bounds.Encapsulate(second.GetLayoutBounds(obstacleRoot));

            // 首轮保持原世界布局；第二批只在初始化时复制一次，之后不再创建刺柱。
            first.transform.SetParent(firstRoot, true);
            second.transform.SetParent(firstRoot, true);
            ObstacleGroup firstGroup = new()
            {
                First = first,
                Second = second,
                LeftX = bounds.min.x,
                RightX = bounds.max.x,
            };
            firstBatch.Groups.Add(firstGroup);
            obstacleGroups.Add(firstGroup);
            Level131ObstacleColumn firstCopy = Instantiate(first, secondRoot, false);
            Level131ObstacleColumn secondCopy = Instantiate(second, secondRoot, false);
            firstCopy.InitializeSerialized(this);
            secondCopy.InitializeSerialized(this);
            obstacleColumns.Add(firstCopy);
            obstacleColumns.Add(secondCopy);
            secondBatch.Groups.Add(new ObstacleGroup
            {
                First = firstCopy,
                Second = secondCopy,
                LeftX = bounds.min.x,
                RightX = bounds.max.x,
            });
            lastRightX = Mathf.Max(lastRightX, bounds.max.x);
        }

        nextObstacleGroupLeftX = lastRightX + Mathf.Max(0f, obstacleGroupGap);
        firstBatch.RightX = lastRightX;
        obstacleBatches.Add(firstBatch);
        obstacleBatches.Add(secondBatch);
        PlaceObstacleBatch(secondBatch);
    }

    #endregion

    #region Route Selection And Input

    private void BindEvents()
    {
        EventsUtils.ResetEvents(ref eventSubscriber);
        eventSubscriber.Subscribe<OnTriggerTrapEvent>(OnTriggerTrap);
        eventSubscriber.Subscribe<OnTriggerFailAreaEvent>(OnTriggerFailArea);
        eventSubscriber.Subscribe<GamePauseEvent>(OnGamePause);
        eventSubscriber.Subscribe<GameResumeEvent>(OnGameResume);
    }

    private void ResolvePanelBinding()
    {
        GamePlayPanel currentPanel = PlayCtrl.Instance != null
            ? PlayCtrl.Instance.CurrentGamePlayPanel
            : null;
        if (currentPanel == boundPanel)
            return;

        UnbindPanel();
        boundPanel = currentPanel;
        if (boundPanel == null)
            return;

        jumpButton = ResolveButton(boundPanel.transform, "jump_btn");
        leftMoveButton = ResolveTransform(boundPanel.transform, "leftMove_btn");
        rightMoveButton = ResolveTransform(boundPanel.transform, "rightMove_btn");
        if (jumpButton != null)
        {
            jumpIcon = boundPanel.JumpButtonIcon;
            if (jumpIcon != null)
            {
                originalJumpIcon = jumpIcon.sprite;
                originalJumpIconRotation = jumpIcon.rectTransform.localRotation;
                originalJumpIconScale = jumpIcon.rectTransform.localScale;
                originalJumpIconPreserveAspect = jumpIcon.preserveAspect;
                originalJumpIconRaycastTarget = jumpIcon.raycastTarget;
            }
            jumpButton.OnClickDown += OnJumpButtonDown;
            jumpButton.OnClickUp += OnJumpButtonUp;
        }

        ApplyControlsForCurrentPhase();
    }

    private void UnbindPanel()
    {
        SetRocketControlIcon(false);
        rocketIconApplied = false;
        jumpIcon = null;
        originalJumpIcon = null;
        if (jumpButton != null)
        {
            jumpButton.OnClickDown -= OnJumpButtonDown;
            jumpButton.OnClickUp -= OnJumpButtonUp;
        }

        jumpButton = null;
        leftMoveButton = null;
        rightMoveButton = null;
        boundPanel = null;
    }

    private void OnJumpButtonDown(CustomButton _)
    {
        if (failureStarted || gamePaused || phase == Phase.Terminal)
            return;

        if (route == Route.Backpack)
        {
            if (phase == Phase.Ready)
                StartBackpackFlight();

            if (phase == Phase.Running)
            {
                thrustHeld = true;
                // 短按即使在两次物理步之间松开，也先产生一次上冲；长按仍由 FixedUpdate 持续维持。
                // 写入同一上冲速度，不叠加额外冲量或新增点按飞行模式。
                if (actorBody != null)
                    actorBody.linearVelocity = new Vector2(backpackWorldSpeed, backpackThrustSpeed);
            }
        }
        else if (route == Route.Platform)
        {
            if (phase == Phase.Ready)
            {
                StartPlatformRoute();
                return;
            }

            if (phase == Phase.Running)
                IncreasePlatformSpeed();
        }
    }

    private void OnJumpButtonUp(CustomButton _)
    {
        if (route == Route.Backpack)
            thrustHeld = false;
    }

    public void TryEquipBackpack(Level131RocketInteraction source, ActorCtrl targetActor)
    {
        if (source == null || targetActor == null || rocketObject == null || route != Route.None || phase != Phase.Initial ||
            source != rocketInteraction || failureStarted)
        {
            return;
        }

        actor = targetActor;
        BindLevelActor();
        actorBody = actor.RB != null ? actor.RB : actor.GetComponent<Rigidbody2D>();
        actorCollider = actor.GetComponent<Collider2D>();
        route = Route.Backpack;
        phase = Phase.Ready;
        ConsumeRocketPickup(source);

        rocketObject.transform.SetParent(actor.transform, false);
        rocketObject.transform.localPosition = new Vector3(-0.24f, -0.22f, 0f);
        rocketObject.transform.localScale = Vector3.one * 0.3f;
        rocketObject.transform.localRotation = Quaternion.identity;
        if (platformRocketRenderer != null)
            platformRocketRenderer.enabled = false;
        if (rocketSpineRenderer != null)
            rocketSpineRenderer.enabled = true;
        rocketFlame?.SetPropulsion(false, 0f);

        LockActorForRoute();
        // 待启动时保持 Dynamic 和正常重力，不冻结拾取位置；首次点击直接进入飞行。
        ShowDistanceText();
        ApplyControlsForCurrentPhase();
    }

    public void BeginRocketDrag(
        Level131RocketInteraction interaction,
        DragEventsListener dragListener)
    {
        if (!CanBeginRocketDrag(interaction) || dragListener == null)
            return;

        rocketDragOriginParent = interaction.transform.parent;
        // 浮动中的当前位置只用于抓取偏移；错误释放回到 Scene 保存的待机基准。
        rocketDragOriginLocalPosition = interaction.RestLocalPosition;
        rocketDragOrigin = rocketDragOriginParent != null
            ? rocketDragOriginParent.TransformPoint(rocketDragOriginLocalPosition)
            : rocketDragOriginLocalPosition;
        Vector3 pointerWorld = ScreenToWorldPoint(dragListener.BeginScreenPosition, interaction.transform.position.z);
        rocketDragOffset = interaction.transform.position - pointerWorld;
    }

    public void UpdateRocketDrag(
        Level131RocketInteraction interaction,
        DragEventsListener dragListener)
    {
        if (interaction == null || dragListener == null || route != Route.None || phase != Phase.Initial)
            return;

        Vector3 pointerWorld = ScreenToWorldPoint(dragListener.CurrentScreenPosition, interaction.transform.position.z);
        interaction.transform.position = pointerWorld + rocketDragOffset;
    }

    /// <summary>
    /// 绑定到安装区域 Trigger 的进入事件；仅正在拖拽的本关火箭进入时锁定方块路线并完成安装。
    /// 调用边界：由 OnTrigger2DHandle.OnEnter 传入实际进入的 Collider2D，不接受 Actor 或其它物体。
    /// </summary>
    public void OnRocketInstallTriggerEnter(Collider2D other)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || other == null ||
            rocketInteraction == null || !rocketInteraction.IsDragging ||
            route != Route.None || phase != Phase.Initial || failureStarted)
        {
            return;
        }

        Transform rocketRoot = rocketInteraction.transform;
        if (other.transform != rocketRoot && !other.transform.IsChildOf(rocketRoot))
            return;

        InstallRocketOnPlatform(rocketInteraction);
    }

    public void EndRocketDrag(Level131RocketInteraction interaction)
    {
        if (interaction == null || route != Route.None || phase != Phase.Initial)
            return;

        interaction.BeginReturn();
        interaction.transform.SetParent(rocketDragOriginParent, true);
        interaction.transform.DOKill();
        interaction.transform
            .DOMove(rocketDragOrigin, 0.2f)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                if (interaction == null)
                    return;
                interaction.transform.localPosition = rocketDragOriginLocalPosition;
                interaction.RestoreForRetry();
            });
    }

    private Vector3 GetRocketInstallPoint()
    {
        if (platformRocketRenderer != null)
            return platformRocketRenderer.transform.position;

        Bounds platformBounds = GetColliderBounds(platformObject);
        return new Vector3(
            platformBounds.min.x - 0.1f,
            platformBounds.max.y + 0.42f,
            platformBounds.center.z - 0.05f);
    }

    private void InstallRocketOnPlatform(Level131RocketInteraction interaction)
    {
        if (rocketObject == null || platformObject == null)
            return;

        route = Route.Platform;
        phase = Phase.Ready;
        BindPlatformStartDoor();
        ConsumeRocketPickup(interaction);
        // 方块使用人工摆放的专属 Sprite；背包 Spine 不搬到方块或重设尺寸。
        if (platformRocketRenderer == null)
        {
            rocketObject.transform.SetParent(platformObject.transform, true);
            rocketObject.transform.position = GetRocketInstallPoint();
            rocketObject.transform.localScale = Vector3.one * 0.3f;
            rocketObject.transform.localRotation = Quaternion.identity;
        }
        platformMover?.SetActorCollisionIgnored(false);
        if (platformRocketRenderer != null)
            platformRocketRenderer.enabled = true;
        if (rocketSpineRenderer != null)
            rocketSpineRenderer.enabled = platformRocketRenderer == null;

        platformSpike?.Reveal();
        LockActorForRoute();
        ShowDistanceText();
        ApplyControlsForCurrentPhase();
    }

    private void ConsumeRocketPickup(Level131RocketInteraction pickup)
    {
        pickup.transform.DOKill();
        pickup.Consume();
        pickup.gameObject.SetActive(false);
        rocketObject.SetActive(true);
        AudioManager.PlaySound("levelup");
    }

    private void StartBackpackFlight()
    {
        if (route != Route.Backpack || phase != Phase.Ready || actorBody == null)
            return;

        phase = Phase.Running;
        thrustHeld = true;
        // 首次起飞后才沿用飞行路线的穿平台规则；领取后的等待阶段仍可落在平台上。
        platformMover?.SetActorCollisionIgnored(true);
        CaptureRouteMotionSamples();
        actorBody.bodyType = RigidbodyType2D.Dynamic;
        actorBody.gravityScale = backpackGravityScale;
        actorBody.linearVelocity = new Vector2(backpackWorldSpeed, backpackThrustSpeed);
        actor.SetFaceDir(true);
        PlayRocketAnimation("rocket01_fly");
        levelActor?.BeginBackpackFlightAnimation();
        ApplyControlsForCurrentPhase();
    }

    private void StartPlatformRoute()
    {
        if (route != Route.Platform || phase != Phase.Ready || platformMover == null)
            return;
        if (platformSpike != null && !platformSpike.IsExtended)
            return;

        AnchorPlatformCamera();
        phase = Phase.Running;
        platformSpeed = Mathf.Min(platformInitialWorldSpeed, platformMaximumWorldSpeed);
        CaptureRouteMotionSamples();
        platformMover.SetCarryActor(true);
        platformMover.SetSpeed(platformSpeed);
        rocketFlame?.SetPropulsion(true, 0f);
        ApplyControlsForCurrentPhase();
    }

    private void IncreasePlatformSpeed()
    {
        platformSpeed = Mathf.Min(
            platformMaximumWorldSpeed,
            platformSpeed + Mathf.Max(0.01f, platformAccelerationPerPress));
        platformMover?.SetSpeed(platformSpeed);
        rocketFlame?.SetPropulsion(true, GetPlatformSpeedFactor());
    }

    private void LockActorForRoute()
    {
        levelCtrl?.SetCtrlable(false);
        actor?.SetMoveInput(0f);
        actor?.StopMoving();
        if (actorBody != null)
        {
            actorBody.bodyType = RigidbodyType2D.Dynamic;
            actorBody.gravityScale = initialActorGravityScale > 0f
                ? initialActorGravityScale
                : backpackGravityScale;
        }
    }

    #endregion

    #region Running And Obstacles

    private void TickBackpackFlight(float deltaTime)
    {
        if (actorBody == null)
            return;

        float verticalVelocity = thrustHeld ? backpackThrustSpeed : actorBody.linearVelocityY;
        float remaining = GetRemainingWorldDistance();
        actorBody.linearVelocity = new Vector2(Mathf.Min(backpackWorldSpeed, remaining / deltaTime), verticalVelocity);
    }

    private Vector3 GetBodyLocalPosition(Rigidbody2D body)
    {
        return body != null
            ? transform.InverseTransformPoint(new Vector3(body.position.x, body.position.y, transform.position.z))
            : Vector3.zero;
    }

    private void CaptureRouteMotionSamples()
    {
        previousActorLocalPosition = GetBodyLocalPosition(actorBody);
        previousPlatformLocalPosition = GetBodyLocalPosition(platformMover != null ? platformMover.Body : null);
        routeMotionSampleValid = true;
    }

    private void AccumulateActualDistance()
    {
        if (!routeMotionSampleValid)
        {
            CaptureRouteMotionSamples();
            return;
        }

        Rigidbody2D body = route == Route.Platform ? platformMover?.Body : actorBody;
        if (body == null)
            return;
        Vector3 previous = route == Route.Platform ? previousPlatformLocalPosition : previousActorLocalPosition;
        Vector3 worldDelta = transform.TransformVector(GetBodyLocalPosition(body) - previous);
        // 只计已完成的向右物理位移，1 世界单位 = 10 里程；不计上下飞行、等待或整关坐标平移。
        distance = Mathf.Min(GoalDistance, distance + Mathf.Max(0f, worldDelta.x) * MileagePerWorldUnit);
    }

    private float GetRemainingWorldDistance()
    {
        return Mathf.Max(0f, GoalDistance - distance) / MileagePerWorldUnit;
    }

    private void SweepTravelledObstacles(Collider2D source, Vector3 previousBodyLocalPosition, bool breakColumns)
    {
        if (!routeMotionSampleValid || source == null || !source.enabled || !source.gameObject.activeInHierarchy ||
            source.attachedRigidbody == null || phase != Phase.Running || failureStarted || gamePaused)
            return;

        Rigidbody2D body = source.attachedRigidbody;
        Vector2 origin = transform.TransformPoint(previousBodyLocalPosition);
        Vector2 travel = body.position - origin;
        if (travel.sqrMagnitude <= 0.00000001f)
            return;

        // 从上一物理位置扫到当前实际位置，不向尚未走到的前方提前破柱；命中仍是原 Polygon 轮廓。
        obstacleSweepHits.Clear();
        int count = source.Cast(origin, body.rotation, travel.normalized, obstacleSweepFilter,
            obstacleSweepHits, travel.magnitude, true);
        for (int index = 0; index < count; index++)
        {
            RaycastHit2D hit = obstacleSweepHits[index];
            Level131ObstacleColumn column = hit.collider != null
                ? hit.collider.GetComponent<Level131ObstacleColumn>() : null;
            if (column == null || !column.IsActive || !column.IsCollisionShape(hit.collider) ||
                !column.transform.IsChildOf(obstacleRoot))
                continue;

            if (breakColumns)
                TryBreakPlatformColumn(column, true);
            else
            {
                TriggerFatalFailure(true, new Vector3(hit.point.x, hit.point.y, source.transform.position.z));
                return;
            }
        }
    }

    private float GetPlatformSpeedFactor()
    {
        return Mathf.InverseLerp(
            Mathf.Max(0.01f, platformInitialWorldSpeed),
            Mathf.Max(platformInitialWorldSpeed + 0.01f, platformMaximumWorldSpeed),
            platformSpeed);
    }

    private void CheckActorObstacleCollisions()
    {
        if (actorCollider == null || actor == null ||
            (phase != Phase.Running && !IsBackpackReady) || failureStarted || gamePaused)
            return;

        for (int index = 0; index < obstacleColumns.Count; index++)
        {
            Level131ObstacleColumn column = obstacleColumns[index];
            if (column != null && column.Overlaps(actorCollider))
            {
                NotifyObstacleHit(column);
                return;
            }
        }
    }

    private void CheckPlatformObstacleCollisions()
    {
        if (platformSpike == null || platformSpike.HitCollider == null || route != Route.Platform)
            return;

        for (int index = 0; index < obstacleColumns.Count; index++)
        {
            Level131ObstacleColumn column = obstacleColumns[index];
            if (column == null || !column.IsActive || !column.IsBottom)
                continue;

            TryBreakPlatformColumn(column);
        }
    }

    public void NotifyObstacleHit(Level131ObstacleColumn column)
    {
        if (column == null || failureStarted || gamePaused ||
            (phase != Phase.Running && !IsBackpackReady) ||
            !column.TryGetHitPoint(actorCollider, out Vector3 hitPosition))
            return;

        TriggerFatalFailure(true, hitPosition);
    }

    public void NotifyPlatformSpikeHit(Level131ObstacleColumn column)
    {
        TryBreakPlatformColumn(column);
    }

    private bool TryBreakPlatformColumn(Level131ObstacleColumn column, bool sweptContact = false)
    {
        if (column == null || !column.IsBottom || route != Route.Platform || phase != Phase.Running ||
            failureStarted || terminalStarted || gamePaused || !column.IsActive || platformSpike == null ||
            (!sweptContact && !column.Overlaps(platformSpike.HitCollider)))
        {
            return false;
        }

        // 仅将碎片发射点沿世界 Y 上移 2，不改变柱体或碰撞位置。
        Vector3 impactPosition = column.Center + Vector3.up * 2f;
        column.ResolveDebrisSorting(out int debrisSortingLayerId, out int debrisSortingOrder);
        if (!column.TryMarkBroken())
            return false;

        float speedFactor = GetPlatformSpeedFactor();
        debrisBursts?.Play(impactPosition, speedFactor, debrisSortingLayerId, debrisSortingOrder);
        AudioManager.PlaySound("crashBroke");
        return true;
    }

    private void PrepareDebrisBursts()
    {
        // 首轮布局不强套后续净空规则，直接留出全部十柱容量；后续按每组最多两柱保守估计。
        // 2 秒最高速窗口 + 两组边界余量；重定位只平移，不影响净空换算。
        float gapWorld = obstacleRoot != null
            ? Mathf.Abs(obstacleRoot.TransformVector(Vector3.right * obstacleGroupGap).x)
            : obstacleGroupGap;
        float travelWindow = Mathf.Max(platformInitialWorldSpeed, platformMaximumWorldSpeed) *
                             Level131DebrisBurst.CapacityWindowSeconds;
        int capacity = InitialObstacleGroupCount * 2 +
                       (Mathf.CeilToInt(travelWindow / Mathf.Max(0.1f, gapWorld)) + 2) * 2;
        debrisBursts = new Level131DebrisBurst(debrisBurstPrefab, this, capacity);
    }

    private float GetForwardX()
    {
        return route == Route.Platform && platformMover != null && platformMover.Body != null
            ? platformMover.Body.position.x
            : actorBody != null ? actorBody.position.x : actor != null ? actor.transform.position.x : transform.position.x;
    }

    private float GetMaximumRouteWorldSpeed()
    {
        return Mathf.Min(route == Route.Platform ? platformMaximumWorldSpeed : backpackWorldSpeed,
            Mathf.Max(0.01f, Physics2D.maxTranslationSpeed));
    }

    private float GetObstacleSpawnLead()
    {
        float speed = GetMaximumRouteWorldSpeed();
        // 提前覆盖最高速半秒，并为物理步和插值镜头留两步余量；低速仍保留原 24 单位下限。
        return Mathf.Max(obstacleSpawnAheadDistance, speed * ObstacleSpawnLeadSeconds) +
               speed * Time.fixedDeltaTime * 2f;
    }

    /// <summary>
    /// 两批刺柱交替向前复用；整批离开镜头及所有运动主体后才允许恢复被破坏的内容。
    /// 终点前预留一屏净空，候选整批侵入该区域时保留原位置；终点预告后也不再前移。
    /// </summary>
    private void RecycleObstacleBatches()
    {
        if (obstacleRoot == null || obstacleBatches.Count != 2 || obstacleLoopCompleted ||
            phase != Phase.Running || failureStarted || terminalStarted || terminalPreviewStarted || gamePaused)
        {
            return;
        }

        float safeBehindX = actorCollider != null ? actorCollider.bounds.min.x : GetForwardX();
        if (worldCamera != null && worldCamera.orthographic)
        {
            float cameraLeftX = worldCamera.transform.position.x - worldCamera.orthographicSize * worldCamera.aspect;
            safeBehindX = Mathf.Min(safeBehindX, cameraLeftX);
        }
        if (route == Route.Platform && platformObject != null)
            safeBehindX = Mathf.Min(safeBehindX, platformObject.transform.position.x);
        if (route == Route.Platform && platformSpike != null && platformSpike.HitCollider != null)
            safeBehindX = Mathf.Min(safeBehindX, platformSpike.HitCollider.bounds.min.x);

        // 留出两步最高速移动余量，避免回收仍可能被本轮扫掠覆盖的旧碰撞体。
        safeBehindX -= Mathf.Max(1f, GetMaximumRouteWorldSpeed() * Time.fixedDeltaTime * 2f);
        float safeBehindLocalX = obstacleRoot.InverseTransformPoint(
            new Vector3(safeBehindX, obstacleRoot.position.y, obstacleRoot.position.z)).x;
        ObstacleBatch trailing = obstacleBatches[0].RightX < obstacleBatches[1].RightX
            ? obstacleBatches[0]
            : obstacleBatches[1];
        if (trailing.RightX >= safeBehindLocalX)
            return;

        float gap = Mathf.Max(0f, obstacleGroupGap);
        float batchWidth = gap * (InitialObstacleGroupCount - 1);
        foreach (ObstacleGroup group in trailing.Groups)
            batchWidth += group.RightX - group.LeftX;
        float nextBatchRightWorldX = obstacleRoot.TransformPoint(
            new Vector3(nextObstacleGroupLeftX + batchWidth + gap, 0f, 0f)).x;
        float screenWidth = worldCamera != null && worldCamera.orthographic
            ? worldCamera.orthographicSize * 2f * worldCamera.aspect
            : Mathf.Max(1f, obstacleSpawnAheadDistance);
        // 必须在前移之前判断，不能等终点显示后再处理已经放进停车区域的上柱。
        // 当前前进位置 + 剩余实际路程是停车位置；整关重定位不会改变两者的相对关系。
        float terminalClearLeftWorldX = GetForwardX() + GetRemainingWorldDistance() - screenWidth;
        if (nextBatchRightWorldX > terminalClearLeftWorldX)
        {
            obstacleLoopCompleted = true;
            return;
        }

        PlaceObstacleBatch(trailing);
    }

    private void PlaceObstacleBatch(ObstacleBatch batch)
    {
        float gap = Mathf.Max(0f, obstacleGroupGap);
        for (int index = 0; index < InitialObstacleGroupCount; index++)
        {
            ObstacleGroup group = batch.Groups[TakeNextObstacleGroup()];
            float offsetX = nextObstacleGroupLeftX - group.LeftX;
            // 两个循环根均保持单位变换；只改柱体 X，不改组内错位、Y、缩放或物理轮廓。
            group.First.transform.localPosition += Vector3.right * offsetX;
            group.Second.transform.localPosition += Vector3.right * offsetX;
            group.LeftX += offsetX;
            group.RightX += offsetX;
            group.First.RestoreForLoop();
            group.Second.RestoreForLoop();
            nextObstacleGroupLeftX = group.RightX + gap;
            batch.RightX = group.RightX;
        }
    }

    private int TakeNextObstacleGroup()
    {
        if (obstacleShuffleCursor >= obstacleShuffleBag.Count)
        {
            obstacleShuffleBag.Clear();
            for (int index = 0; index < obstacleGroups.Count; index++)
                obstacleShuffleBag.Add(index);

            // 每一轮恰好包含 A-E 各一次；不同轮之间独立洗牌。
            for (int index = obstacleShuffleBag.Count - 1; index > 0; index--)
            {
                int other = Random.Range(0, index + 1);
                int value = obstacleShuffleBag[index];
                obstacleShuffleBag[index] = obstacleShuffleBag[other];
                obstacleShuffleBag[other] = value;
            }
            obstacleShuffleCursor = 0;
        }

        return obstacleShuffleBag[obstacleShuffleCursor++];
    }

    private void MaybeRebaseWorld()
    {
        // 保留背包重定位；计数/扫掠采样存关卡局部坐标，整关平移不增加里程。
        // 方块真实走完 1000 世界单位，仍不平移其刚体祖先，避免重引 MovePosition / 插值争写。
        // 该有限长度的远端浮点精度与循环视觉仍需人工验证，不把关卡原点平移当作移动。
        if (route != Route.Backpack || phase != Phase.Running || actor == null || rebaseAmount <= 0f)
            return;

        if (actor.transform.position.x < nextRebaseWorldX)
            return;

        transform.position -= new Vector3(rebaseAmount, 0f, 0f);
        nextRebaseWorldX = actor.transform.position.x + rebaseTriggerDistance;
    }

    private void PlayRocketAnimation(string animationName)
    {
        if (rocketObject == null || string.IsNullOrEmpty(animationName))
            return;

        ActorSpineCtrl spine = rocketObject.GetComponentInChildren<ActorSpineCtrl>(true);
        spine?.PlayAnimation(animationName, true);
    }

    #endregion

    #region Failure And Terminal

    private void TriggerFatalFailure(bool playTrapSound, Vector3 hitPosition)
    {
        if (failureStarted || phase == Phase.Terminal)
            return;

        failureStarted = true;
        phase = Phase.Failed;
        thrustHeld = false;
        platformMover?.Stop();
        rocketFlame?.SetPropulsion(false, 0f);
        ApplyControlsForCurrentPhase();
        // 先提交失败门禁再播放，扫描与 Trigger 同帧命中不会重复生成。
        PlayCommonHitEffect(hitPosition);

        if (playTrapSound)
        {
            AudioManager.PlaySound("trap");
            AudioManager.PlaySound("failed");
            this.DispatchEvent(Witness<OnTriggerTrapEvent>._);
        }
    }

    private void PlayCommonHitEffect(Vector3 hitPosition)
    {
        if (playerHitEffectPlayed)
            return;
        playerHitEffectPlayed = true;
        if (commonHitEffectPrefab == null)
        {
            Debug.LogError("Level 131 缺少 Actor 通用碰撞特效引用。", this);
            return;
        }

        int sortingLayerId = SortingLayer.NameToID("SceneLayer");
        int sortingOrder = 200;
        // 不能只提高粒子 Renderer：父 SortingGroup 也会限制最终遮挡顺序。
        void IncludeSorting(int layerId, int order)
        {
            int candidate = SortingLayer.GetLayerValueFromID(layerId);
            int current = SortingLayer.GetLayerValueFromID(sortingLayerId);
            if (candidate > current)
            {
                sortingLayerId = layerId;
                sortingOrder = order;
            }
            else if (candidate == current)
                sortingOrder = Mathf.Max(sortingOrder, order);
        }

        // 碰撞时直接创建并播放。把背景遮罩、地面、门等也纳入排序，避免只高于 Actor/刺柱。
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            IncludeSorting(renderer.sortingLayerID, renderer.sortingOrder);
        foreach (SortingGroup group in GetComponentsInChildren<SortingGroup>(true))
            if (group.enabled)
                IncludeSorting(group.sortingLayerID, group.sortingOrder);
        for (Transform parent = transform.parent; parent != null; parent = parent.parent)
        {
            SortingGroup group = parent.GetComponent<SortingGroup>();
            if (group != null && group.enabled)
                IncludeSorting(group.sortingLayerID, group.sortingOrder);
        }

        ParticleSystem instance = Instantiate(commonHitEffectPrefab, hitPosition,
            commonHitEffectPrefab.transform.rotation, transform);
        playerHitEffect = instance;
        instance.name = "Level131PlayerHitEffect";
        // 与 dev129 相同：使用无 Knock 字样的通用打击表现，保持源尺寸和内部相对排序。
        Transform knockText = instance.transform.Find("Knock 1");
        if (knockText != null)
            knockText.gameObject.SetActive(false);
        // Knock_Explosion 的根节点没有可安全运行时追加的 SortingGroup；在该 prefab
        // 上动态 AddComponent 会在实际碰撞链中抛 MissingComponentException，导致
        // 后续粒子播放代码被跳过。沿用共享特效的 Renderer/子 SortingGroup 排序，
        // 不改变 prefab 资产，也不引入新的运行时组件。
        foreach (SortingGroup group in instance.GetComponentsInChildren<SortingGroup>(true))
        {
            group.enabled = true;
            group.sortingLayerID = sortingLayerId;
            group.sortingOrder = sortingOrder + 1;
        }
        foreach (ParticleSystemRenderer renderer in instance.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder + 1;
        }

        instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = instance.main;
        main.loop = false;
        main.playOnAwake = false;
        main.stopAction = ParticleSystemStopAction.None;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        // 原共享资源的无字根效果恰好在 t=0 爆发 1 粒子；改为命中时显式 Emit，避免等自动发射。
        // 只改本局实例，保留源材质、尺寸、寿命及形状，不再叠加自动 Burst。
        ParticleSystem.EmissionModule emission = instance.emission;
        emission.rateOverTime = 0f;
        emission.rateOverDistance = 0f;
        emission.burstCount = 0;
        instance.gameObject.SetActive(true);
        instance.Play(false);
        instance.Emit(1);
        Destroy(instance.gameObject, 3f);
    }

    private void TryPlayMissingPlayerHitEffect()
    {
        if (playerHitEffectPlayed || actorCollider == null)
            return;

        // 专属 Actor 的死亡前通知/外部陷阱通知，只在真实刺柱接触时播放，不将任意死亡当作撞刺。
        // 不用前进扫描游标：手动终点阶段可能折返碰到身后仍存在的柱体。
        foreach (Level131ObstacleColumn column in obstacleColumns)
        {
            if (column == null || !column.TryGetHitPoint(actorCollider, out Vector3 hitPosition))
                continue;

            PlayCommonHitEffect(hitPosition);
            return;
        }
    }

    private void StopRunWithoutDispatch()
    {
        levelActor?.RestoreDefaultAnimation();
        phase = Phase.Failed;
        failureStarted = true;
        thrustHeld = false;
        platformMover?.Stop();
        rocketFlame?.SetPropulsion(false, 0f);
        ApplyControlsForCurrentPhase();
    }

    private void OnTriggerTrap(OnTriggerTrapEvent _)
    {
        if (phase == Phase.Running || phase == Phase.Terminal || IsBackpackReady)
        {
            // 外部陷阱通知可能先令 Actor.Die；仅补漏掉的特效，不重复派发死亡或重播来源音效。
            TryPlayMissingPlayerHitEffect();
            StopRunWithoutDispatch();
        }
    }

    private void OnTriggerFailArea(OnTriggerFailAreaEvent _)
    {
        if (phase == Phase.Running || phase == Phase.Terminal || IsBackpackReady)
            StopRunWithoutDispatch();
    }

    private void OnGamePause(GamePauseEvent _)
    {
        gamePaused = true;
        debrisBursts?.SetPaused(true);
        rocketInteraction?.SetPaused(true);
        platformSpike?.SetPaused(true);
        thrustHeld = false;
        if (phase == Phase.Running && route == Route.Platform)
            platformMover?.Stop();
    }

    private void OnGameResume(GameResumeEvent _)
    {
        gamePaused = false;
        debrisBursts?.SetPaused(false);
        rocketInteraction?.SetPaused(false);
        platformSpike?.SetPaused(false);

        // BaseLevelCtrl 恢复时会 ResumeMoving；路线期间（含 Ready 待启动阶段）
        // 乘员保持锁定，只有终点段恢复普通控制。
        if (route != Route.None && phase != Phase.Terminal)
            actor?.StopMoving();

        if (phase == Phase.Running)
            CaptureRouteMotionSamples(); // 不把暂停期间的外部位移算作推进或跨柱路径。
        if (phase == Phase.Running && route == Route.Platform)
            platformMover?.SetSpeed(platformSpeed);
    }

    private bool TryPrepareTerminalPreview(bool force = false)
    {
        if (terminalPreviewStarted)
            return true;
        if (terminalRoot == null || doorRoot == null || actor == null ||
            terminalPlatformCollider == null || !terminalPlatformColliderInitiallyEnabled ||
            phase != Phase.Running || failureStarted || gamePaused)
            return false;

        float remainingWorldDistance = GetRemainingWorldDistance();
        float screenWidth = worldCamera != null && worldCamera.orthographic
            ? worldCamera.orthographicSize * 2f * worldCamera.aspect
            : Mathf.Max(1f, obstacleSpawnAheadDistance);
        float cameraRight = worldCamera != null
            ? worldCamera.transform.position.x + screenWidth * 0.5f
            : GetForwardX() + screenWidth * 0.5f;
        float screenBuffer = screenWidth * Mathf.Max(0.25f, terminalPreviewScreens);
        float maxGroupWidth = 0f;
        foreach (ObstacleGroup group in obstacleGroups)
            maxGroupWidth = Mathf.Max(maxGroupWidth, group.RightX - group.LeftX);
        if (obstacleRoot != null)
            maxGroupWidth *= Mathf.Abs(obstacleRoot.lossyScale.x);

        // 比当前障碍预生成范围再提前一屏，保证不会把终点插到已有刺柱中间。
        float previewLead = Mathf.Max(cameraRight - GetForwardX() + screenBuffer,
            GetObstacleSpawnLead() + maxGroupWidth + obstacleGroupGap + screenBuffer);
        if (!force && remainingWorldDistance > previewLead)
            return false;

        float leadingX = actorCollider != null ? actorCollider.bounds.max.x : actor.transform.position.x;
        // 背包保留原有余量；方块以本体实体右缘为准，侧刺 Trigger/火箭 Renderer 不参与。
        float entryGap = Mathf.Max(TerminalEntryGap, backpackWorldSpeed * Time.fixedDeltaTime * 2f);
        if (route == Route.Platform)
        {
            if (platformMover == null || !platformMover.TryGetSolidRightEdge(out leadingX))
                return false;
            entryGap = PlatformTerminalGap;
        }
        float targetLeft = leadingX + remainingWorldDistance + entryGap;
        targetLeft = Mathf.Max(targetLeft, cameraRight + screenBuffer);
        if (obstacleRoot != null && obstacleGroups.Count > 0)
        {
            // 游标包含当前前批的右缘及净空；终点放在已摆放内容之外，预告后不再前移障碍。
            float clearLeft = obstacleRoot.TransformPoint(new Vector3(nextObstacleGroupLeftX, 0f, 0f)).x;
            targetLeft = Mathf.Max(targetLeft, clearLeft);
        }

        Bounds platformBounds = GetTerminalPlatformBounds();
        Vector3 terminalPosition = terminalRoot.position;
        terminalPosition.x += targetLeft - platformBounds.min.x;
        terminalRoot.position = terminalPosition; // 仅预告时放置一次，后续不追着镜头或里程重排。

        terminalPreviewStarted = true;

        // 先只展示整段，避免尚未到达 10000 就撞上终点地面或提前进门。
        terminalPlatformCollider.enabled = false;
        if (doorTrigger != null)
            doorTrigger.enabled = false;
        doorRoot.gameObject.SetActive(true);
        terminalRoot.gameObject.SetActive(true);
        return true;
    }

    private bool TryGetPlatformTerminalDistance(out float remainingWorldDistance)
    {
        remainingWorldDistance = 0f;
        if (route != Route.Platform || !terminalPreviewStarted || terminalPlatformCollider == null ||
            platformMover == null || !platformMover.TryGetSolidRightEdge(out float platformRight))
            return false;

        // 终点承托暂时禁用时也从其序列化形状取左缘，不能改用门中心或 Renderer 包络。
        remainingWorldDistance = Mathf.Max(0f, GetTerminalPlatformBounds().min.x - platformRight - PlatformTerminalGap);
        return true;
    }

    private Bounds GetTerminalPlatformBounds()
    {
        // 终点未激活或处于只展示阶段时，Collider.bounds 为空；直接变换序列化盒子四角。
        Vector2 center = terminalPlatformCollider.offset;
        Vector2 half = terminalPlatformCollider.size * 0.5f;
        Transform colliderTransform = terminalPlatformCollider.transform;
        Bounds bounds = new(colliderTransform.TransformPoint(center), Vector3.zero);
        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
                bounds.Encapsulate(colliderTransform.TransformPoint(center + new Vector2(half.x * x, half.y * y)));
        }
        return bounds;
    }

    private void BeginTerminal()
    {
        if (terminalStarted || failureStarted || gamePaused || phase != Phase.Running || actor == null || actor.IsDied)
            return;
        if (!TryPrepareTerminalPreview(true))
            return;
        if (route == Route.Platform && (!TryGetPlatformTerminalDistance(out float remainingToGround) ||
            remainingToGround > PlatformTerminalPositionTolerance))
            return;

        terminalStarted = true;
        phase = Phase.Terminal;
        thrustHeld = false;
        platformMover?.Stop();
        platformMover?.SetCarryActor(false);
        platformMover?.SetActorCollisionIgnored(false);
        // 到站保留侧刺伸出外观，只关闭破障 Trigger；背包路线不主动显示侧刺。
        if (platformSpike != null && platformSpike.HitCollider != null)
            platformSpike.HitCollider.enabled = false;
        rocketFlame?.SetPropulsion(false, 0f);

        // 保留 Actor 当前所在位置及原平台承托，不插值送往终点、不先抬高再落台。
        // 终点只开放地面和门，由玩家用普通左右/跳跃操作自行抵达。
        terminalPlatformCollider.enabled = terminalPlatformColliderInitiallyEnabled;
        if (rocketObject != null)
            rocketObject.SetActive(false);

        if (actorBody != null)
        {
            actorBody.bodyType = RigidbodyType2D.Dynamic;
            actorBody.gravityScale = initialActorGravityScale > 0f
                ? initialActorGravityScale
                : backpackGravityScale;
            // 只撤销自动推进的水平速度，保留竖向物理状态；不写 Transform/Rigidbody 位置。
            actorBody.linearVelocity = new Vector2(0f, actorBody.linearVelocityY);
        }

        PrepareTerminalDoor();
        actor?.ResumeMoving();
        actor?.SetMoveInput(0f);
        levelActor?.RestoreDefaultAnimation();
        levelCtrl?.SetCtrlable(true);
        ApplyControlsForCurrentPhase();
    }

    private void PrepareTerminalDoor()
    {
        if (doorRoot == null)
            return;

        doorRoot.gameObject.SetActive(true);
        if (doorTrigger != null)
            doorTrigger.enabled = true;

        SuccessAnimCtrl successAnim = doorRoot.GetComponent<SuccessAnimCtrl>();
        if (successAnim != null)
            levelCtrl?.SetSuccessAnim(successAnim);
    }

    #endregion

    #region HUD

    private void SetRocketControlIcon(bool useRocketIcon)
    {
        useRocketIcon &= rocketControlIcon != null;
        if (jumpIcon == null || rocketIconApplied == useRocketIcon)
            return;

        rocketIconApplied = useRocketIcon;
        jumpIcon.sprite = useRocketIcon ? rocketControlIcon : originalJumpIcon;
        jumpIcon.rectTransform.localRotation = useRocketIcon
            ? Quaternion.Euler(0f, 0f, -45f)
            : originalJumpIconRotation;
        jumpIcon.rectTransform.localScale = useRocketIcon
            ? originalJumpIconScale * 1.2f
            : originalJumpIconScale;
        jumpIcon.preserveAspect = useRocketIcon || originalJumpIconPreserveAspect;
        // 点击仍由未旋转的按钮底板接收；旋转后的装饰图标不扩大命中区域。
        jumpIcon.raycastTarget = !useRocketIcon && originalJumpIconRaycastTarget;
    }

    private void SetInitialControls()
    {
        SetRocketControlIcon(false);
        SetButtonActive(leftMoveButton, true);
        SetButtonActive(rightMoveButton, true);
        SetButtonActive(jumpButton != null ? jumpButton.transform : null, true);
        if (distanceText != null)
            distanceText.gameObject.SetActive(false);
    }

    private void ApplyControlsForCurrentPhase()
    {
        if (boundPanel == null)
            return;

        SetRocketControlIcon((route == Route.Backpack || route == Route.Platform)
            && (phase == Phase.Ready || phase == Phase.Running));

        if (phase == Phase.Terminal)
        {
            SetButtonActive(leftMoveButton, true);
            SetButtonActive(rightMoveButton, true);
            SetButtonActive(jumpButton != null ? jumpButton.transform : null, true);
            return;
        }

        if (phase == Phase.Failed)
        {
            SetButtonActive(leftMoveButton, false);
            SetButtonActive(rightMoveButton, false);
            SetButtonActive(jumpButton != null ? jumpButton.transform : null, false);
            return;
        }

        if (route == Route.Backpack || route == Route.Platform)
        {
            SetButtonActive(leftMoveButton, false);
            SetButtonActive(rightMoveButton, false);
            SetButtonActive(jumpButton != null ? jumpButton.transform : null, true);
            return;
        }

        SetInitialControls();
    }

    private void ShowDistanceText()
    {
        if (distanceText != null)
            distanceText.gameObject.SetActive(true);
    }

    private void UpdateDistanceText()
    {
        if (distanceText == null || distanceLocalize == null)
            return;

        // 世界空间 TMP 归关卡持有，不依赖 UI 面板，也不切换到屏幕坐标。
        bool visible = !gamePaused;
        if (distanceText.gameObject.activeSelf != visible)
            distanceText.gameObject.SetActive(visible);

        int shownDistance = Mathf.Clamp(Mathf.FloorToInt(distance), 0, Mathf.FloorToInt(GoalDistance));
        if (shownDistance == lastShownDistance)
            return;

        lastShownDistance = shownDistance;
        distanceLocalize.OnLocalize(true);
    }

    // I2 LocalizeEvent 的序列化回调；语言切换也走同一路径，在 RTL 排版前填入实时数字。
    public void FormatDistanceTranslation()
    {
        const string fallback = "前进距离\n{0}/{1}";
        string template = string.IsNullOrEmpty(Localize.MainTranslation)
            ? fallback : Localize.MainTranslation;
        int current = Mathf.Clamp(Mathf.FloorToInt(distance), 0, Mathf.FloorToInt(GoalDistance));
        int goal = Mathf.FloorToInt(GoalDistance);
        try
        {
            Localize.MainTranslation = string.Format(template, current, goal);
        }
        catch (System.FormatException)
        {
            if (!distanceFormatWarningReported)
            {
                Debug.LogWarning("[Level131] Distance translation requires {0}/{1} placeholders.", this);
                distanceFormatWarningReported = true;
            }
            Localize.MainTranslation = string.Format(fallback, current, goal);
        }
    }

    private static void SetButtonActive(Transform button, bool active)
    {
        if (button != null)
            button.gameObject.SetActive(active);
    }

    #endregion

    #region Helpers And Cleanup

    private Vector3 ScreenToWorldPoint(Vector2 screenPosition, float worldZ)
    {
        if (worldCamera == null)
            return Vector3.zero;

        float zDistance = Mathf.Abs(worldCamera.transform.position.z - worldZ);
        Vector3 world = worldCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, zDistance));
        world.z = worldZ;
        return world;
    }

    private static CustomButton ResolveButton(Transform root, string path)
    {
        Transform button = ResolveTransform(root, path);
        return button != null ? button.GetComponent<CustomButton>() : null;
    }

    private static Transform ResolveTransform(Transform root, string path)
    {
        if (root == null)
            return null;

        Transform result = root.Find(path);
        if (result != null)
            return result;

        string leaf = path.Substring(path.LastIndexOf('/') + 1);
        return FindChildRecursive(root, leaf);
    }

    private static Renderer FindMeshRenderer(GameObject root)
    {
        if (root == null)
            return null;

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is MeshRenderer)
                return renderer;
        }

        return null;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
            return null;
        if (root.name == childName)
            return root;

        for (int index = 0; index < root.childCount; index++)
        {
            Transform found = FindChildRecursive(root.GetChild(index), childName);
            if (found != null)
                return found;
        }

        return null;
    }

    private static Bounds GetColliderBounds(GameObject target)
    {
        Bounds bounds = new(target != null ? target.transform.position : Vector3.zero, Vector3.zero);
        bool hasBounds = false;
        if (target == null)
            return bounds;

        foreach (Collider2D collider in target.GetComponentsInChildren<Collider2D>(true))
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                continue;
            if (hasBounds)
                bounds.Encapsulate(collider.bounds);
            else
            {
                bounds = collider.bounds;
                hasBounds = true;
            }
        }

        if (hasBounds)
            return bounds;

        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;
            if (hasBounds)
                bounds.Encapsulate(renderer.bounds);
            else
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
        }

        return bounds;
    }

    private void OnDestroy()
    {
        if (levelActor != null)
        {
            levelActor.Dying -= OnActorDying;
            levelActor.RestoreDefaultAnimation();
        }
        levelActor = null;
        if (playerHitEffect != null)
            Destroy(playerHitEffect.gameObject);
        playerHitEffect = null;
        debrisBursts?.Dispose();
        debrisBursts = null;
        // 两批刺柱归本关 Hierarchy 所有，随关卡销毁，不转移到全局对象池。
        backgroundLoop?.Dispose();
        backgroundLoop = null;
        EventsUtils.ResetEvents(ref eventSubscriber);
        doorTriggerHandle?.OnEnter.RemoveListener(OnTerminalDoorEnter);
        UnbindPanel();
        DOTween.Kill(gameObject);
        actor?.transform.DOKill();
        platformObject?.transform.DOKill();
        rocketObject?.transform.DOKill();
        rocketInteraction?.transform.DOKill();
        platformMover?.Stop();
    }

    #endregion
}
