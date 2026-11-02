using System;
using Bear.EventSystem;
using Game.Events;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Level 116「勇往直前」本关专属编排器。
/// 负责挡板完整越线（仅无敌资格边界）、连续右移链、五区固定尖刺编排与本关暂停/中断冻结。
/// 尖刺默认停在各自方向的屏外行程起点；本次尝试内首次有效玩家移动输入后一次性启动。
/// 挡板只做无敌资格边界，不启动/重置/对齐尖刺。
/// 开局入场：与 Level_005 / 103 对齐——保留左侧 StartDoor 门体，但 enterDoorAnim 置空且不挂 EnterDoorAnimCtrl。
/// PlayCtrl 共享入场链找不到可播组件后直接跳过；Actor 使用 Prefab 站立位直接开局。
/// </summary>
public sealed class Level116Ctrl : MonoBehaviour, IEventSender
{
    /// <summary>实际尖刺渲染序：高于 Actor 根 SortingGroup=200，保证尖刺画在角色前方。</summary>
    private const int ObstacleSortingOrder = 210;
    /// <summary>统一尖刺缩放：现行合同 Scale=0.7。</summary>
    private const float ObstacleLocalScale = 0.7f;

    /// <summary>
    /// 单点位固定编排：世界 X、速度档、方向、区段颜色、可选世界速度覆盖。
    /// 全部点位从屏外行程起点（InitialDistance=0）出发；文档代理可从 ObstacleFormation 还原五区与空槽。
    /// UseGray 按区段红/灰交替，与速度档解耦；WorldSpeedOverride <= 0 用档位默认速度。
    /// </summary>
    private readonly struct ObstaclePointConfig
    {
        public readonly float WorldX;
        public readonly Level116ObstacleSpeedTier SpeedTier;
        public readonly Level116ObstacleDirection Direction;
        public readonly bool UseGray;
        public readonly float WorldSpeedOverride;

        public ObstaclePointConfig(
            float worldX,
            Level116ObstacleSpeedTier speedTier,
            Level116ObstacleDirection direction,
            bool useGray,
            float worldSpeedOverride = -1f)
        {
            WorldX = worldX;
            SpeedTier = speedTier;
            Direction = direction;
            UseGray = useGray;
            WorldSpeedOverride = worldSpeedOverride;
        }
    }

    /*
     * 站立间隔（Scale=0.7；视觉/触发宽约 0.833，半宽约 0.4165）：
     *   区内相邻中心距约 2.83 → 边到边净空约 2.0，相对旧 2.5/≈1.67 更易放下 Actor（碰撞宽约 1.05）；
     *   区段间安全带中心距约 4.17 → 边到边净空约 3.33（略收旧 5.0，换区内余量；首尾刺与门位不动）；
     *   末刺 X=46.75，右门 51.4，门前通行缓冲约 4.65；相机右界 52.5 内仍可见门。
     *
     * 五区 + 区段间站立安全带（空带≠检查点/无敌/相位重置）：
     *   Zone1 导读   X=1.75, 4.58, 7.42
     *   空带 A       7.42 → 11.58
     *   Zone2 同向   X=11.58, 14.42
     *   空带 B       14.42 → 18.58
     *   Zone3 中压   X=18.58, 21.42, 24.25
     *   空带 C       24.25 → 28.42
     *   Zone4 加压   X=28.42, 31.25, 34.08
     *   空带 D       34.08 → 38.25
     *   Zone5 终压   X=38.25, 41.08, 43.92, 46.75
     *   门前缓冲     46.75 → 51.4
     *
     * 速度档默认映射 S1..S6 = 3.5/7.5/10/21.2/25.2/40。
     * 区段颜色红/灰交替（与速度档解耦）：Z1红 / Z2灰 / Z3红 / Z4灰 / Z5红。
     * Zone3 中央点可对 S4 使用 WorldSpeedOverride=11，避免牵动 Zone4/5 的 S4。
     * 全部尖刺 InitialDistance=0：开局/重置停在屏外行程起点并静止；
     * 本次尝试首次有效玩家移动（左/右/跳）后同时启动，启动后门闩保持，松手不停。
     */
    private static readonly ObstaclePointConfig[] ObstacleFormation =
    {
        // Zone1 红 — 前易读：三支统一 S1=3.5，区内中心距约 2.83（边到边约 2.0）
        new ObstaclePointConfig(1.75f, Level116ObstacleSpeedTier.S1, Level116ObstacleDirection.Down, false),
        new ObstaclePointConfig(4.58f, Level116ObstacleSpeedTier.S1, Level116ObstacleDirection.Up, false),
        new ObstaclePointConfig(7.42f, Level116ObstacleSpeedTier.S1, Level116ObstacleDirection.Down, false),

        // Zone2 灰 — 同向向上墙：两支统一 S2=7.5
        new ObstaclePointConfig(11.58f, Level116ObstacleSpeedTier.S2, Level116ObstacleDirection.Up, true),
        new ObstaclePointConfig(14.42f, Level116ObstacleSpeedTier.S2, Level116ObstacleDirection.Up, true),

        // Zone3 红 — 两侧 S3=10，中央 S4 覆盖速度 11
        new ObstaclePointConfig(18.58f, Level116ObstacleSpeedTier.S3, Level116ObstacleDirection.Down, false),
        new ObstaclePointConfig(21.42f, Level116ObstacleSpeedTier.S4, Level116ObstacleDirection.Up, false, 11f),
        new ObstaclePointConfig(24.25f, Level116ObstacleSpeedTier.S3, Level116ObstacleDirection.Down, false),

        // Zone4 灰 — 同向向下加压：S4→14、S5→16（覆盖，不改全局档位默认）
        new ObstaclePointConfig(28.42f, Level116ObstacleSpeedTier.S4, Level116ObstacleDirection.Down, true, 14f),
        new ObstaclePointConfig(31.25f, Level116ObstacleSpeedTier.S5, Level116ObstacleDirection.Down, true, 16f),
        new ObstaclePointConfig(34.08f, Level116ObstacleSpeedTier.S4, Level116ObstacleDirection.Down, true, 14f),

        // Zone5 红 — 终压：S5→20、S6→24、S4→18（覆盖）
        new ObstaclePointConfig(38.25f, Level116ObstacleSpeedTier.S5, Level116ObstacleDirection.Up, false, 20f),
        new ObstaclePointConfig(41.08f, Level116ObstacleSpeedTier.S6, Level116ObstacleDirection.Down, false, 24f),
        new ObstaclePointConfig(43.92f, Level116ObstacleSpeedTier.S5, Level116ObstacleDirection.Up, false, 20f),
        new ObstaclePointConfig(46.75f, Level116ObstacleSpeedTier.S4, Level116ObstacleDirection.Down, false, 18f),
    };

    [Header("Level 116 References")]
    [SerializeField] private BaseLevelCtrl levelCtrl;
    [SerializeField] private SpriteRenderer startBarrier;
    [SerializeField] private Level116InputTracker inputTracker;
    [SerializeField] private SpriteRenderer redObstacleTemplate;
    [SerializeField] private SpriteRenderer grayObstacleTemplate;
    [SerializeField] private Level116Obstacle[] obstacles;
    [Header("Hit Feedback")]
    [SerializeField] private ParticleSystem obstacleHitEffect;
    [SerializeField] private string obstacleHitSfxTag = "trap";

    private readonly Level116ForwardRule _rule = new Level116ForwardRule();

    private EventSubscriber _subscriber;
    private ActorCtrl _actor;
    private PolygonCollider2D _actorMainCollider;
    private Rigidbody2D _actorBody;

    private bool _sharedRightHeld;
    private bool _sharedLeftHeld;
    private bool _gamePaused;
    private bool _applicationPaused;
    private bool _hasApplicationFocus = true;
    private bool _inputDeviceInterrupted;
    private bool _attemptEnding;
    private bool _failureCommitted;
    private bool _isSuspended;
    /// <summary>本次尝试内尖刺是否已由首次有效玩家移动一次性启动。</summary>
    private bool _obstaclesMotionStarted;

    private bool _actorFrozen;
    private bool _actorWasSimulated;
    private Vector2 _storedActorVelocity;
    private float _storedActorAngularVelocity;

    public bool IsTriangleObstacleInvulnerable => _rule.IsTriangleObstacleInvulnerable;

    private bool IsRightHeld =>
        _sharedRightHeld || (inputTracker != null && inputTracker.IsRightHeld);

    private bool IsLeftHeld =>
        _sharedLeftHeld || (inputTracker != null && inputTracker.IsLeftHeld);

    private bool ShouldSuspend =>
        _gamePaused ||
        _applicationPaused ||
        !_hasApplicationFocus ||
        _inputDeviceInterrupted ||
        _attemptEnding;

    private void Awake()
    {
        ResolveReferences();
        // 与 103/104 等先例一致：确保共享入场引用为空，PlayCtrl 走 TryToPlayEnterDoorAnim 时直接跳过。
        levelCtrl?.SetEnterDoorAnim(null);
        BuildObstacleFormationIfNeeded();
        InitializeObstacles();
        ResetAttemptState();
    }

    private void OnEnable()
    {
        ResolveReferences();
        levelCtrl?.SetEnterDoorAnim(null);
        BindEvents();
        BindInputTracker();
        InputSystem.onDeviceChange += OnInputDeviceChange;
    }

    private void OnDisable()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        UnbindInputTracker();
        InputSystem.onDeviceChange -= OnInputDeviceChange;
        StopObstacleHitEffect();
        RestoreActorSimulation(false);
    }

    private void OnDestroy()
    {
        StopObstacleHitEffect();
    }

    private void Update()
    {
        ResolveActorReferences();

        // 挡板越线只建立本次连续右移/无敌资格边界，不启动或对齐尖刺。
        if (!_attemptEnding && !_isSuspended && !_rule.HasEnteredGameplay && IsActorCompletelyPastBarrier())
            _rule.TryEnterGameplay(IsRightHeld, IsLeftHeld);

        // 障碍时间轴：仅在本次尝试已由玩家移动启动后推进；暂停/中断/尝试收口仍冻结。
        if (_attemptEnding || _isSuspended || !_obstaclesMotionStarted)
            return;

        float deltaTime = Time.deltaTime;
        for (int i = 0; i < obstacles.Length; i++)
            obstacles[i]?.Advance(deltaTime);
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (_applicationPaused == pauseStatus)
            return;

        _applicationPaused = pauseStatus;
        if (pauseStatus)
            HandleInputInterruption();

        RefreshSuspensionState();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (_hasApplicationFocus == hasFocus)
            return;

        _hasApplicationFocus = hasFocus;
        if (!hasFocus)
            HandleInputInterruption();

        RefreshSuspensionState();
    }

    internal void TryHandleObstacleEnter(Collider2D other, Collider2D obstacleCollider)
    {
        if (other == null || _attemptEnding || _failureCommitted || _isSuspended)
            return;

        ResolveActorReferences();
        if (_actorMainCollider == null || other != _actorMainCollider)
            return;

        if (other.gameObject.layer != LayerMask.NameToLayer("Actor"))
            return;

        // 挡板不再是障碍启动门；未建链的普通 Trigger Enter 不可因尚未越线而静默忽略。
        // 无敌资格仍只由 ForwardRule 在越线后建立。
        if (_rule.IsTriangleObstacleInvulnerable)
            return;

        _failureCommitted = true;

        Vector3 hitPosition = CalculateContactPoint(obstacleCollider, other);
        PlayObstacleHitFeedback(hitPosition);

        this.DispatchEvent(Witness<OnTriggerTrapEvent>._);
    }

    private static Vector3 CalculateContactPoint(Collider2D sourceCollider, Collider2D targetCollider)
    {
        Vector2 sourceCenter = sourceCollider != null
            ? sourceCollider.bounds.center
            : (Vector2)targetCollider.bounds.center;
        Vector2 targetPoint = targetCollider.ClosestPoint(sourceCenter);
        Vector2 sourcePoint = sourceCollider != null
            ? sourceCollider.ClosestPoint(targetPoint)
            : sourceCenter;
        Vector2 contactPoint = (sourcePoint + targetPoint) * 0.5f;
        float z = sourceCollider != null
            ? sourceCollider.transform.position.z
            : targetCollider.transform.position.z;
        return new Vector3(contactPoint.x, contactPoint.y, z);
    }

    private void PlayObstacleHitFeedback(Vector3 hitPosition)
    {
        if (obstacleHitEffect != null)
        {
            obstacleHitEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            obstacleHitEffect.transform.position = hitPosition;
            obstacleHitEffect.Play(true);
        }

        if (!string.IsNullOrEmpty(obstacleHitSfxTag))
            AudioManager.PlaySound(obstacleHitSfxTag);

        // BaseLevelCtrl.OnTriggerTrap -> WaitPlayDie does not play "failed".
        AudioManager.PlaySound("failed");
    }

    private void StopObstacleHitEffect()
    {
        if (obstacleHitEffect == null)
            return;

        obstacleHitEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void ResolveReferences()
    {
        if (levelCtrl == null)
            levelCtrl = GetComponent<BaseLevelCtrl>();

        if (startBarrier == null)
        {
            Transform barrierTransform = transform.Find("Sp_l78_img_03");
            if (barrierTransform != null)
                startBarrier = barrierTransform.GetComponent<SpriteRenderer>();
        }

        if (inputTracker == null)
            inputTracker = GetComponent<Level116InputTracker>();

        if (redObstacleTemplate == null)
        {
            Transform template = transform.Find(
                "L116ObstacleVisualTemplates/Sp_l116_jc_01_RedDownTemplate");
            if (template != null)
                redObstacleTemplate = template.GetComponent<SpriteRenderer>();
        }

        if (grayObstacleTemplate == null)
        {
            Transform template = transform.Find(
                "L116ObstacleVisualTemplates/Sp_l116_jc_02_GrayUpTemplate");
            if (template != null)
                grayObstacleTemplate = template.GetComponent<SpriteRenderer>();
        }

        if (obstacleHitEffect == null)
        {
            Transform hitEffectTransform = transform.Find("Level116ObstacleHitEffect");
            if (hitEffectTransform != null)
                obstacleHitEffect = hitEffectTransform.GetComponent<ParticleSystem>();
        }

        if (obstacles == null || obstacles.Length == 0)
        {
            obstacles = GetComponentsInChildren<Level116Obstacle>(true);
            Array.Sort(
                obstacles,
                (left, right) => left.transform.position.x.CompareTo(right.transform.position.x));
        }

        ResolveActorReferences();
    }

    private void ResolveActorReferences()
    {
        if (_actor == null && levelCtrl != null)
            _actor = levelCtrl.Actor;

        if (_actor == null)
            return;

        if (_actorMainCollider == null)
            _actorMainCollider = _actor.GetComponent<PolygonCollider2D>();

        if (_actorBody == null)
            _actorBody = _actor.GetComponent<Rigidbody2D>();
    }

    private void BuildObstacleFormationIfNeeded()
    {
        if (obstacles != null && obstacles.Length > 0)
            return;

        if (redObstacleTemplate == null || grayObstacleTemplate == null)
        {
            obstacles = Array.Empty<Level116Obstacle>();
            Debug.LogError("[Level116Ctrl] Obstacle visual templates are missing.", this);
            return;
        }

        if (ObstacleFormation == null || ObstacleFormation.Length == 0)
        {
            obstacles = Array.Empty<Level116Obstacle>();
            Debug.LogError("[Level116Ctrl] Fixed obstacle formation data is empty.", this);
            return;
        }

        GameObject obstacleRootObject = new GameObject("L116Obstacles");
        Transform obstacleRoot = obstacleRootObject.transform;
        obstacleRoot.SetParent(transform, false);

        int triggerLayer = LayerMask.NameToLayer("TriggerItem");
        int actorLayer = LayerMask.NameToLayer("Actor");
        int actorMask = actorLayer >= 0 ? 1 << actorLayer : 0;
        int expectedCount = ObstacleFormation.Length;
        obstacles = new Level116Obstacle[expectedCount];

        for (int i = 0; i < expectedCount; i++)
        {
            ObstaclePointConfig point = ObstacleFormation[i];
            Level116ObstacleSpeedTier speedTier = point.SpeedTier;
            Level116ObstacleDirection direction = point.Direction;
            // 颜色按区段 UseGray 交替，不再由 S6 档位唯一决定。
            bool useGrayTemplate = point.UseGray;
            SpriteRenderer sourceRenderer = useGrayTemplate
                ? grayObstacleTemplate
                : redObstacleTemplate;

            string colorName = useGrayTemplate ? "Gray" : "Red";
            string directionName = direction == Level116ObstacleDirection.Up ? "Up" : "Down";
            GameObject obstacleObject = new GameObject(
                $"L116Obstacle_{i + 1:00}_{speedTier}_{directionName}_{colorName}");
            if (triggerLayer >= 0)
                obstacleObject.layer = triggerLayer;

            // 生成时先放到各自方向的屏外行程起点，Bind/Reset 会再次对齐。
            float spawnY = direction == Level116ObstacleDirection.Up ? -7f : 7f;

            Transform obstacleTransform = obstacleObject.transform;
            obstacleTransform.SetParent(obstacleRoot, false);
            obstacleTransform.position = new Vector3(point.WorldX, spawnY, 0f);
            obstacleTransform.localScale = Vector3.one * ObstacleLocalScale;
            obstacleTransform.localRotation = direction == Level116ObstacleDirection.Up
                ? Quaternion.Euler(0f, 0f, 180f)
                : Quaternion.identity;

            SpriteRenderer renderer = obstacleObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sourceRenderer.sprite;
            renderer.sharedMaterial = sourceRenderer.sharedMaterial;
            renderer.color = Color.white;
            renderer.sortingLayerID = sourceRenderer.sortingLayerID;
            renderer.sortingOrder = ObstacleSortingOrder;

            BoxCollider2D trigger = obstacleObject.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(1.19f, 1.65f);
            trigger.offset = Vector2.zero;
            trigger.includeLayers = actorMask;
            trigger.excludeLayers = ~actorMask;

            Level116Obstacle obstacle = obstacleObject.AddComponent<Level116Obstacle>();
            obstacle.Configure(direction, speedTier, 0f, point.WorldSpeedOverride);
            obstacles[i] = obstacle;
        }
    }

    private void InitializeObstacles()
    {
        if (obstacles == null)
            obstacles = Array.Empty<Level116Obstacle>();

        int expectedCount = ObstacleFormation.Length;
        if (obstacles.Length != expectedCount)
        {
            Debug.LogError(
                $"[Level116Ctrl] Expected {expectedCount} obstacles, found {obstacles.Length}.",
                this);
        }

        for (int i = 0; i < obstacles.Length; i++)
            obstacles[i]?.Bind(this);
    }

    private void ResetAttemptState()
    {
        _rule.ResetAttempt();
        _sharedRightHeld = false;
        _sharedLeftHeld = false;
        _gamePaused = false;
        _applicationPaused = false;
        _hasApplicationFocus = true;
        _inputDeviceInterrupted = false;
        _attemptEnding = false;
        _failureCommitted = false;
        _isSuspended = false;
        _obstaclesMotionStarted = false;

        if (inputTracker != null)
            inputTracker.ClearHeldState();

        for (int i = 0; i < obstacles.Length; i++)
            obstacles[i]?.ResetToInitial();
    }

    /// <summary>
    /// 本次尝试内首次有效玩家移动输入（左/右/跳）一次性启动全部尖刺。
    /// 启动后门闩保持；松手、停步、返回挡板左侧都不会停止已启动的时间轴。
    /// 入场动画自动位移不算；暂停/中断/尝试收口期间不接受新启动。
    /// </summary>
    private void TryStartObstacleMotionFromPlayerInput()
    {
        if (_obstaclesMotionStarted || _attemptEnding || _failureCommitted || _isSuspended)
            return;

        _obstaclesMotionStarted = true;
    }

    private bool IsActorCompletelyPastBarrier()
    {
        if (_actorMainCollider == null || startBarrier == null)
            return false;

        return _actorMainCollider.bounds.min.x > startBarrier.bounds.max.x;
    }

    private void BindEvents()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<PlayerRightMoveEvent>(OnPlayerRightMove);
        _subscriber.Subscribe<PlayerLeftMoveEvent>(OnPlayerLeftMove);
        _subscriber.Subscribe<PlayerMoveCancelEvent>(OnPlayerMoveCancel);
        _subscriber.Subscribe<PlayerJumpEvent>(OnPlayerJump);
        _subscriber.Subscribe<GamePauseEvent>(OnGamePause);
        _subscriber.Subscribe<GameResumeEvent>(OnGameResume);
        _subscriber.Subscribe<GameResetEvent>(OnGameReset);
    }

    private void BindInputTracker()
    {
        if (inputTracker == null)
            return;

        inputTracker.StateChanged -= OnRawInputStateChanged;
        inputTracker.StateChanged += OnRawInputStateChanged;
    }

    private void UnbindInputTracker()
    {
        if (inputTracker != null)
            inputTracker.StateChanged -= OnRawInputStateChanged;
    }

    private void OnRawInputStateChanged()
    {
        EvaluateHeldInput();
    }

    private void OnPlayerRightMove(PlayerRightMoveEvent _)
    {
        _sharedRightHeld = true;
        _sharedLeftHeld = false;
        TryStartObstacleMotionFromPlayerInput();
        EvaluateHeldInput();
    }

    private void OnPlayerLeftMove(PlayerLeftMoveEvent _)
    {
        _sharedRightHeld = false;
        _sharedLeftHeld = true;
        TryStartObstacleMotionFromPlayerInput();
        EvaluateHeldInput();
    }

    private void OnPlayerMoveCancel(PlayerMoveCancelEvent _)
    {
        _sharedRightHeld = false;
        _sharedLeftHeld = false;
        EvaluateHeldInput();
    }

    private void OnPlayerJump(PlayerJumpEvent _)
    {
        if (_attemptEnding || _failureCommitted || _isSuspended)
            return;

        TryStartObstacleMotionFromPlayerInput();
        _rule.NotifyJump(IsRightHeld, IsLeftHeld);
    }

    private void EvaluateHeldInput()
    {
        if (_attemptEnding || _failureCommitted || _isSuspended)
            return;

        // 重开后若右/左键仍被持续按住，InputTracker 或共享事件恢复持有态时也算一次开始移动。
        if (IsRightHeld || IsLeftHeld)
            TryStartObstacleMotionFromPlayerInput();

        _rule.NotifyHeldInput(IsRightHeld, IsLeftHeld);
    }

    private void OnGamePause(GamePauseEvent _)
    {
        if (_gamePaused)
            return;

        _gamePaused = true;
        HandleInputInterruption();
        RefreshSuspensionState();
    }

    private void OnGameResume(GameResumeEvent _)
    {
        if (!_gamePaused)
            return;

        _gamePaused = false;
        RefreshSuspensionState();
    }

    private void OnGameReset(GameResetEvent _)
    {
        // 回到屏外行程起点并再次静止，等待本次尝试的首次玩家移动；不永久锁死同一实例。
        // 暂停/失焦/设备中断标志保留，由 RefreshSuspensionState 决定是否仍冻结。
        _attemptEnding = false;
        _failureCommitted = false;
        _obstaclesMotionStarted = false;
        _rule.ResetAttempt();

        // 只清共享事件镜像，不强制清空 InputTracker：
        // 若重开后右/左键仍被持续按住，应立即再次启动尖刺（推荐语义）。
        // 中断路径仍走 ClearTrackedInput。
        _sharedRightHeld = false;
        _sharedLeftHeld = false;
        StopObstacleHitEffect();

        for (int i = 0; i < obstacles.Length; i++)
            obstacles[i]?.ResetToInitial();

        RefreshSuspensionState();
        EvaluateHeldInput();
    }

    private void OnInputDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (!IsGameplayInputDevice(device))
            return;

        switch (change)
        {
            case InputDeviceChange.Disconnected:
            case InputDeviceChange.Removed:
            case InputDeviceChange.Disabled:
                _inputDeviceInterrupted = true;
                HandleInputInterruption();
                RefreshSuspensionState();
                break;

            case InputDeviceChange.Added:
            case InputDeviceChange.Reconnected:
            case InputDeviceChange.Enabled:
                if (_inputDeviceInterrupted)
                {
                    _inputDeviceInterrupted = false;
                    RefreshSuspensionState();
                }
                break;
        }
    }

    private static bool IsGameplayInputDevice(InputDevice device)
    {
        return device is Pointer || device is Keyboard || device is Gamepad;
    }

    private void HandleInputInterruption()
    {
        _rule.NotifyInterruption();
        ClearTrackedInput();
    }

    private void ClearTrackedInput()
    {
        _sharedRightHeld = false;
        _sharedLeftHeld = false;
        inputTracker?.ClearHeldState();
    }

    private void RefreshSuspensionState()
    {
        bool shouldSuspend = ShouldSuspend;
        if (_isSuspended == shouldSuspend)
            return;

        _isSuspended = shouldSuspend;
        if (_isSuspended)
            FreezeActor();
        else
            RestoreActorSimulation(true);
    }

    private void FreezeActor()
    {
        ResolveActorReferences();
        if (_actorFrozen)
            return;

        _actorFrozen = true;
        if (_actorBody != null)
        {
            _actorWasSimulated = _actorBody.simulated;
            _storedActorVelocity = _actorBody.linearVelocity;
            _storedActorAngularVelocity = _actorBody.angularVelocity;
        }

        if (_actor != null)
        {
            _actor.SetMoveInput(0f);
            _actor.StopMoving();
        }

        if (_actorBody != null)
            _actorBody.simulated = false;
    }

    private void RestoreActorSimulation(bool resumeMovement)
    {
        if (!_actorFrozen)
            return;

        if (_actorBody != null)
        {
            _actorBody.simulated = _actorWasSimulated;
            if (_actorWasSimulated)
            {
                _actorBody.linearVelocity = new Vector2(0f, _storedActorVelocity.y);
                _actorBody.angularVelocity = _storedActorAngularVelocity;
            }
        }

        _actorFrozen = false;
        if (_actor == null)
            return;

        _actor.SetMoveInput(0f);
        if (resumeMovement && !_attemptEnding && !_failureCommitted && !_actor.IsDied)
            _actor.ResumeMoving();
    }
}
