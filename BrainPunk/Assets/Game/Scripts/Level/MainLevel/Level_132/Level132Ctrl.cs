using System;
using System.Collections;
using System.Collections.Generic;
using Bear.EventSystem;
using Bear.UI;
using DG.Tweening;
using Game.Events;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using I2.Loc;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>132 双路线：准备期拉绳弹射，或逐渐加速跑满 10000 米。只接管本关实例。</summary>
public sealed class Level132Ctrl : MonoBehaviour, IEventSender
{
    private enum Phase { Entrance, Approach, Preparation, Running, Flight, Platform, ExitBoarding, InitializationFailed, Finished }

    [Header("Scene references")]
    [SerializeField] private BaseLevelCtrl levelCtrl;
    [SerializeField] private Level132ActorCtrl actor;
    [SerializeField] private Transform gameplayLayout;
    [Tooltip("准备锁定点的横向位置；脚下高度从 Scene 中实际绳面读取，不使用本标记的 Y。")]
    [SerializeField] private Transform ropeAnchor;
    [Tooltip("可自由摆放的世界空间 TMP；正上方仅为初始摆放，只更新数字、显隐与缩放，不回写位置或限制父节点。")]
    [SerializeField] private TextMeshPro preparationCountdownText;
    [Tooltip("准备点所在的人工直绳实例；显式绑定，不依赖对象名称判断准备绳。")]
    [SerializeField] private Transform preparationRope;
    [Tooltip("柱子侧连接绳段；下拉与回弹以其原左端为固定端，不跟随准备点移动。")]
    [SerializeField] private Transform pullLeftRope;
    [SerializeField] private Material ropeCurveMaterial;
    [Tooltip("渐进同侧连块组合共用的原始素材；不翻转、不改 Scale。")]
    [SerializeField] private SpriteRenderer obstacleVisual;
    [Tooltip("复用通用 Knock_Explosion；仅在撞障碍时创建，整体绘制在命中障碍之上。")]
    [SerializeField] private ParticleSystem obstacleImpactPrefab;
    [Tooltip("玩家先正常跳上并站稳的起步平台；不自动搬移或抬升该平台。")]
    [SerializeField] private BoxCollider2D approachPlatform;
    [Tooltip("非玩家 Actor：自然物理落地，准备阶段托管锯绳演出，不接玩家输入或出入门。")]
    [SerializeField] private Level132BoxBadCtrl boxBad;
    [Tooltip("指定平台上的水平落点；实际脚底高度取平台 Collider 顶面，不以门作为落点。")]
    [SerializeField] private Transform landingPoint;
    [SerializeField] private Transform topPlatform;
    [SerializeField] private Transform highDoor;
    [SerializeField] private Transform mileageDoor;
    [SerializeField] private Transform finishGround;
    [SerializeField] private Transform cameraRoot;

    [Header("World Distance Text (Scene Editable)")]
    [SerializeField] private TextMeshPro distanceText;
    [SerializeField] private Localize distanceLocalize;

    [Header("Box-Bad invitation")]
    [SerializeField] private string invitationBubbleKey = "S_LevelTips_Des_level_132_4";
    private const float InvitationBubbleSeconds = 1.5f;

    [Header("Confirmed rules")]
    [SerializeField, Min(0.1f)] private float preparationSeconds = 5f;
    [Tooltip("跑绳初速度；只影响本关自动跑动，不修改入场移动。")]
    [SerializeField, Min(0.1f)] private float runSpeed = 3f;
    [Tooltip("跑绳最高速度；路线生成会同步预留对应速度下的翻身窗口。")]
    [SerializeField, Min(0.1f)] private float maximumRunSpeed = 7.5f;
    [Tooltip("从初速匀加速至最高速度所需的实际跑绳秒数；暂停、准备及弹射不累计。")]
    [SerializeField, Min(1f)] private float accelerationSeconds = 40f;
    [SerializeField, Min(1f)] private float targetMeters = 10000f;

    [Header("Rope and launch tuning (world units / seconds)")]
    [SerializeField, Min(0.1f)] private float minimumPull = 0.9f;
    [SerializeField, Min(0.1f)] private float maximumPull = 2.4f;
    [Tooltip("右侧最小跨度；实际按捕获的视野和左右跨度比例向屏外延长，不再在近处回平。")]
    [SerializeField, Min(0.1f)] private float pullSpan = 3.75f;
    [SerializeField, Min(2f)] private float pullRightSpanRatio = 4f;
    [Tooltip("右侧整个回平圆角留在捕获视野外的额外世界距离；一次手势及回弹期间不重新计算。")]
    [SerializeField, Min(0.5f)] private float pullRightViewMargin = 3f;
    [Tooltip("下拉/回弹的 V 底及右侧衔接圆角中心线半径；实际按绳宽和跨度限制，不影响下坠。")]
    [SerializeField, Min(0.01f)] private float pullCornerRadius = 0.35f;
    [SerializeField, Min(0.1f)] private float flightSeconds = 2f;
    [SerializeField, Min(0.1f)] private float apexAbovePlatform = 6f;
    [SerializeField, Min(1f)] private float chaseDistance = 6f;
    [SerializeField, Min(0.05f)] private float switchCooldown = 0.15f;
    [SerializeField, Min(0.05f)] private float switchTransitionSeconds = 0.2f;
    [SerializeField, Min(0f)] private float switchArcOffset = 0.18f;
    [SerializeField, Min(0.1f)] private float boardingSeconds = 0.35f;
    [SerializeField] private float cameraLookAhead = 3.5f;
    [Tooltip("相机视野最上沿的世界 Y 上限，不是相机中心高度；飞行时同样限制。")]
    [SerializeField] private float cameraTopLimitY = 75.455f;
    [Header("Forward loop coverage")]
    [SerializeField, Min(1f)] private float spawnAheadDistance = 24f;
    [SerializeField, Min(0f)] private float spawnViewMargin = 3f;

    private EventSubscriber _subscriber;
    private Phase _phase;
    private Camera _camera;
    private BoxCollider2D _platformCollider;
    private BoxCollider2D _highGate;
    private BoxCollider2D _mileageGate;
    private Transform _finishFallArea;
    private Level132Hud _hud;
    private Level132Track _track;
    private Level132RunCourse _runCourse;
    private double _runSeconds;
    private Level132Backdrop _backdrop;
    private Level132RealTipsPopup _preparationPopup;
    private bool _preparationPopupShown;
    private bool _waitingForPreparationPopup;
    private int _preparationResumeFrame = -1;
    private HintBubble _invitationBubble;
    private bool _invitationShown;
    private ParticleSystem _obstacleImpact;
    private Vector3 _initialCamera;
    private Vector3 _anchor;
    private Vector3 _stageFrom;
    private Vector3 _stageTo;
    private Vector3 _flightCameraStart;
    private Vector3 _flightVelocity;
    private float _flightGravity;
    private float _stageTime;
    private float _remaining;
    private Vector3 _countdownBaseScale = Vector3.one;
    private int _countdownSeconds = -1;
    private float _pull;
    private float _visualPull;
    private float _pullFixedLeftX;
    private float _pullViewSpan;
    private float _releasePull;
    private float _releaseTime = 1f;
    private float _standingTime;
    private bool _reachedApproachPlatform;
    private float _cooldown;
    private double _meters;
    private double _displayMeters;
    private double _flightDisplayFrom;
    private int _shownDistance = -1;
    private int _shownGoal = -1;
    private bool _distanceFormatWarningReported;
    private Vector2 _previousHitCenter;
    private Vector2 _previousHitHalfSize;
    private bool _switching;
    private bool _switchFromBelow;
    private float _switchElapsed;
    private float _ropeAngle;
    private float _exitFromAngle;
    private bool _below;
    private bool _initialized;
    private bool _boxBadInitialized;
    private bool _trackFailed;
    private bool _preparationBindingFailed;
    private bool _gamePaused;
    private bool _eventBlocked;
    private bool _applicationPaused;
    private bool _hasFocus = true;
    private bool _dragging;
    private bool _pulled;
    private int _fingerId = -1;
    private Vector2 _pressPosition;
    private readonly List<RaycastResult> _uiHits = new List<RaycastResult>();
    private readonly List<Collider2D> _playerColliders = new List<Collider2D>();
    private readonly List<Collider2D> _boxBadColliders = new List<Collider2D>();
    private readonly List<ActorCollisionPair> _actorCollisionPairs = new List<ActorCollisionPair>();

    private sealed class ActorCollisionPair
    {
        public readonly Collider2D Player;
        public readonly Collider2D BoxBad;
        public readonly bool OriginallyIgnored;

        public ActorCollisionPair(Collider2D player, Collider2D bad, bool originallyIgnored)
        {
            Player = player;
            BoxBad = bad;
            OriginallyIgnored = originallyIgnored;
        }
    }

    private bool Suspended => _waitingForPreparationPopup || Time.frameCount <= _preparationResumeFrame ||
        _gamePaused || _eventBlocked || _applicationPaused || !_hasFocus ||
        _hud == null || _hud.IsBlocked;
    private bool SetupFailed => _trackFailed || _preparationBindingFailed;

    private void Awake()
    {
        _phase = Phase.Entrance;
        if (distanceText != null) distanceText.gameObject.SetActive(true);
        if (preparationCountdownText != null)
            _countdownBaseScale = preparationCountdownText.transform.localScale;
        HidePreparationCountdown();
        _hasFocus = Application.isFocused;
        if (levelCtrl == null || actor == null || !actor.HasEntranceGround || gameplayLayout == null ||
            approachPlatform == null || boxBad == null || boxBad == actor ||
            landingPoint == null || topPlatform == null || highDoor == null || mileageDoor == null ||
            finishGround == null || cameraRoot == null)
        {
            Debug.LogError("[Level132] Required scene references are missing; gameplay was not started.", this);
            enabled = false;
            return;
        }
        _camera = cameraRoot.GetComponent<Camera>();
        _platformCollider = topPlatform.GetComponent<BoxCollider2D>();
        _highGate = highDoor.GetComponent<BoxCollider2D>();
        _mileageGate = mileageDoor.GetComponent<BoxCollider2D>();
        _finishFallArea = transform.Find("FallArea (1)");
        if (_camera == null || _platformCollider == null || _highGate == null || _mileageGate == null)
        {
            Debug.LogError("[Level132] Camera/platform/door components are missing.", this);
            enabled = false;
            return;
        }
        _highGate.enabled = _mileageGate.enabled = false;
        // 原门模板的 UnityEvent 可能未绑定或仍指向 BaseLevelCtrl，统一由本关阶段门禁提交。
        DisableGenericDoorTrigger(highDoor);
        DisableGenericDoorTrigger(mileageDoor);
        var oldPlaceholder = gameplayLayout.Find("L132HighShortcutDoorPlaceholder");
        if (oldPlaceholder != null) oldPlaceholder.gameObject.SetActive(false);
        levelCtrl.SetSuccessAnim(null);
    }

    private void OnValidate()
    {
        preparationSeconds = Mathf.Max(0.1f, preparationSeconds);
        runSpeed = Mathf.Max(0.1f, runSpeed);
        maximumRunSpeed = Mathf.Max(runSpeed, maximumRunSpeed);
        accelerationSeconds = Mathf.Max(1f, accelerationSeconds);
        targetMeters = Mathf.Max(1f, targetMeters);
        minimumPull = Mathf.Max(0.1f, minimumPull);
        maximumPull = Mathf.Max(minimumPull, maximumPull);
        pullSpan = Mathf.Max(1f, pullSpan);
        pullRightSpanRatio = Mathf.Max(2f, pullRightSpanRatio);
        pullRightViewMargin = Mathf.Max(0.5f, pullRightViewMargin);
        pullCornerRadius = Mathf.Max(0.01f, pullCornerRadius);
        flightSeconds = Mathf.Max(0.1f, flightSeconds);
        apexAbovePlatform = Mathf.Max(0.1f, apexAbovePlatform);
        chaseDistance = Mathf.Max(1f, chaseDistance);
        switchCooldown = Mathf.Max(0.05f, switchCooldown);
        switchTransitionSeconds = Mathf.Max(0.05f, switchTransitionSeconds);
        switchArcOffset = Mathf.Max(0f, switchArcOffset);
        boardingSeconds = Mathf.Max(0.1f, boardingSeconds);
        spawnAheadDistance = Mathf.Max(1f, spawnAheadDistance);
        spawnViewMargin = Mathf.Max(0f, spawnViewMargin);
    }

    private static void DisableGenericDoorTrigger(Transform door)
    {
        var handler = door.GetComponent<OnTrigger2DHandle>();
        if (handler != null) handler.enabled = false;
    }

    private void OnEnable()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<PlayerJumpEvent>(OnSwitchSide);
        _subscriber.Subscribe<PlayerLeftMoveEvent>(OnInvitationLeft);
        _subscriber.Subscribe<PlayerRightMoveEvent>(OnInvitationRight);
        _subscriber.Subscribe<GamePauseEvent>(OnPause);
        _subscriber.Subscribe<GameResumeEvent>(OnResume);
        _subscriber.Subscribe<SwitchGameStateEvent>(OnGameState);
        _subscriber.Subscribe<GamePlayPanelSwitchBlockEvent>(OnInputBlock);
        _subscriber.Subscribe<GameResetEvent>(OnReset);
        _subscriber.Subscribe<GameReplayEvent>(OnReplay);
        _subscriber.Subscribe<OnTriggerTrapEvent>(OnExternalTrap);
        _subscriber.Subscribe<OnTriggerFailAreaEvent>(OnExternalFall);
        RefreshActorCollisionIsolation();
    }

    private void FixedUpdate()
    {
        // 停用/重新启用 Collider 可能丢失 IgnoreCollision；恢复模拟后的物理步前重新检查。
        // 不受玩法阶段或暂停门禁影响，不在角色重叠时因换阶段重新打开碰撞。
        RefreshActorCollisionIsolation();
    }

    private static bool IsColliderActive(Collider2D collider)
    {
        return collider != null && collider.enabled && collider.gameObject.activeInHierarchy;
    }

    private void RefreshActorCollisionIsolation()
    {
        if (actor == null || boxBad == null || actor == boxBad) return;
        // 复用列表，并涵盖推物子碰撞体及运行时新增的角色子物体；不扫描地面或其它实例。
        actor.GetComponentsInChildren(true, _playerColliders);
        boxBad.GetComponentsInChildren(true, _boxBadColliders);
        for (int i = _actorCollisionPairs.Count - 1; i >= 0; i--)
            if (_actorCollisionPairs[i].Player == null || _actorCollisionPairs[i].BoxBad == null)
                _actorCollisionPairs.RemoveAt(i);

        foreach (Collider2D playerCollider in _playerColliders)
        {
            if (!IsColliderActive(playerCollider)) continue;
            foreach (Collider2D badCollider in _boxBadColliders)
            {
                if (!IsColliderActive(badCollider) || playerCollider == badCollider) continue;
                bool ignored = Physics2D.GetIgnoreCollision(playerCollider, badCollider);
                bool captured = false;
                foreach (ActorCollisionPair pair in _actorCollisionPairs)
                {
                    if (pair.Player != playerCollider || pair.BoxBad != badCollider) continue;
                    captured = true;
                    break;
                }
                if (!captured)
                    _actorCollisionPairs.Add(new ActorCollisionPair(playerCollider, badCollider, ignored));
                if (!ignored)
                    Physics2D.IgnoreCollision(playerCollider, badCollider, true);
            }
        }
    }

    private void RestoreActorCollisionIsolation()
    {
        foreach (ActorCollisionPair pair in _actorCollisionPairs)
        {
            // 非活动 Collider 的忽略状态已由物理系统移除，不为恢复而重新激活对象。
            if (!IsColliderActive(pair.Player) || !IsColliderActive(pair.BoxBad)) continue;
            Physics2D.IgnoreCollision(pair.Player, pair.BoxBad, pair.OriginallyIgnored);
        }
        _actorCollisionPairs.Clear();
        _playerColliders.Clear();
        _boxBadColliders.Clear();
    }

    private IEnumerator Start()
    {
        while (actor != null && !actor.Initialized)
            yield return null;
        if (!isActiveAndEnabled || actor == null || _phase == Phase.Finished)
            yield break;
        _initialCamera = _camera.transform.position;
        // 不在这里 StopMoving/切刚体/清输入；共享出门正在驱动普通 Actor。
        _platformCollider.enabled = true;
        _platformCollider.isTrigger = false;
        try
        {
            _track = new Level132Track(transform, gameplayLayout, ropeCurveMaterial, obstacleVisual);
            _backdrop = new Level132Backdrop(transform, _camera);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            _track?.Dispose();
            _backdrop?.Dispose();
            _track = null;
            _backdrop = null;
            _trackFailed = true;
            Debug.LogError("[Level132] Track initialization failed. Gameplay is unavailable; " +
                "the error HUD will remain visible after entrance. Fix the scene references and restart the level.", this);
        }
        if (_track != null)
        {
            try
            {
                if (!TryBindPreparationPoint(out string error))
                {
                    _preparationBindingFailed = true;
                    Debug.LogError("[Level132] Preparation binding failed: " + error, this);
                }
            }
            catch (Exception exception)
            {
                _preparationBindingFailed = true;
                Debug.LogException(exception, this);
            }
        }
        if (!isActiveAndEnabled) yield break;
        _remaining = preparationSeconds;
        _initialized = true;
    }

    // 准备位置属于上绳绑定，不再是循环器构造/模板缓存的前置条件。
    private bool TryBindPreparationPoint(out string error)
    {
        error = null;
        if (preparationCountdownText == null)
        {
            error = "Preparation countdown TMP reference is missing.";
            return false;
        }
        var sprite = preparationRope != null ? preparationRope.GetComponent<SpriteRenderer>() : null;
        if (ropeAnchor == null || sprite == null || !preparationRope.IsChildOf(gameplayLayout) ||
            !sprite.enabled || !sprite.gameObject.activeInHierarchy || sprite.sprite == null)
        {
            error = "A marker and a visible preparation rope inside GameplayLayout are required. The authored layout was retained.";
            return false;
        }
        Bounds bounds = sprite.sprite.bounds;
        Vector3 a = sprite.transform.TransformPoint(new Vector3(bounds.center.x, bounds.min.y, 0f));
        Vector3 b = sprite.transform.TransformPoint(new Vector3(bounds.center.x, bounds.max.y, 0f));
        float left = Mathf.Min(a.x, b.x);
        float right = Mathf.Max(a.x, b.x);
        Vector3 anchor = ropeAnchor.position;
        if (right - left < 0.1f || Mathf.Abs(a.y - b.y) > 0.02f || anchor.x < left || anchor.x > right)
        {
            error = $"RopeAnchor X={anchor.x:F3} must lie on horizontal rope '{sprite.name}' [{left:F3}, {right:F3}].";
            return false;
        }
        var leftSprite = pullLeftRope != null ? pullLeftRope.GetComponent<SpriteRenderer>() : null;
        if (leftSprite == null || !pullLeftRope.IsChildOf(gameplayLayout) || !leftSprite.enabled ||
            !leftSprite.gameObject.activeInHierarchy || leftSprite.sprite == null)
        {
            error = "A visible pillar-side rope inside GameplayLayout is required for the fixed pull endpoint.";
            return false;
        }
        Bounds leftBounds = leftSprite.sprite.bounds;
        Vector3 leftA = leftSprite.transform.TransformPoint(new Vector3(leftBounds.center.x, leftBounds.min.y, 0f));
        Vector3 leftB = leftSprite.transform.TransformPoint(new Vector3(leftBounds.center.x, leftBounds.max.y, 0f));
        float fixedLeftX = Mathf.Min(leftA.x, leftB.x);
        if (anchor.x - fixedLeftX < 0.1f)
        {
            error = "The preparation marker must remain to the right of the pillar-side fixed endpoint.";
            return false;
        }
        float t = Mathf.Clamp01((anchor.x - a.x) / (b.x - a.x));
        anchor.y = Mathf.Lerp(a.y, b.y, t) +
            Mathf.Abs(sprite.transform.TransformVector(Vector3.right * bounds.extents.x).y);
        _track.ConfigurePreparation(sprite, leftSprite, fixedLeftX);
        _runCourse = new Level132RunCourse(runSpeed, maximumRunSpeed, accelerationSeconds,
            Mathf.Max(switchCooldown, switchTransitionSeconds), actor.RopeSwitchEnvelopeWidth(switchArcOffset));
        _track.ConfigureObstacleCourse(anchor.x, targetMeters, _runCourse);
        _anchor = anchor;
        _pullFixedLeftX = fixedLeftX;
        CapturePullView();
        return true;
    }

    // 初始化及每次有效按下时冻结右侧衔接，不在拉动/回弹时逐帧追逐镜头边界。
    private void CapturePullView()
    {
        float leftSpan = Mathf.Max(0.1f, _anchor.x - _pullFixedLeftX);
        float cameraRight = _camera.transform.position.x + _camera.orthographicSize * _camera.aspect;
        // 右跨度至少为左侧两倍，Taut 的半径上限因此为 0.45*左跨度；再计入带宽，保护整个圆角而非仅 B 点。
        float cornerPadding = leftSpan * 0.45f + _track.MaximumRopeHalfWidth;
        float viewSpan = cameraRight + Mathf.Max(0.5f, pullRightViewMargin) + cornerPadding - _anchor.x;
        _pullViewSpan = Mathf.Max(Mathf.Max(1f, pullSpan),
            Mathf.Max(leftSpan * Mathf.Max(2f, pullRightSpanRatio), viewSpan));
        _track.ConfigurePullView(_anchor.x + _pullViewSpan);
    }

    private void Update()
    {
        if (!_initialized || actor == null)
            return;
        UpdateDistanceText();
        if (_phase == Phase.Finished) return;
        TickBoxBad();
        actor.AllowKeyboardInput = (_phase == Phase.Approach || _phase == Phase.Platform ||
            _phase == Phase.InitializationFailed) && !Suspended;
        actor.AllowNormalJump = (_phase == Phase.Approach || _phase == Phase.InitializationFailed) && !Suspended;
        if (_hud == null && PlayCtrl.Instance != null && PlayCtrl.Instance.LevelCtrl == levelCtrl &&
            PlayCtrl.Instance.CurrentGamePlayPanel != null)
        {
            try
            {
                _hud = new Level132Hud(PlayCtrl.Instance.CurrentGamePlayPanel);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                StopAttempt();
                enabled = false;
                return;
            }
            ApplyControls();
        }
        // 共享 BaseLevelCtrl 在 await 出门完整结束后才开放 ActorCtrlable。
        // 本关 Entrance 不写这个开关；失败重开跳过演出时保留其默认开放状态。
        if (_phase == Phase.Entrance)
        {
            actor.SetFrozen(_gamePaused || _applicationPaused || !_hasFocus);
            if (!Suspended && levelCtrl.ActorCtrlable)
            {
                actor.CompleteEntrance();
                _phase = SetupFailed ? Phase.InitializationFailed : Phase.Approach;
                ApplyControls();
            }
            return;
        }
        if (Suspended)
        {
            CancelDrag();
            _hud?.ClearMovement();
            actor.SetFrozen(true);
            return;
        }
        actor.SetFrozen(false);
        if (actor.IsDied)
        {
            StopAttempt();
            return;
        }
        float dt = Time.deltaTime;
        _cooldown = Mathf.Max(0f, _cooldown - dt);
        switch (_phase)
        {
            case Phase.InitializationFailed:
                // 显式配置错误态，不访问已清理的轨道、不开始倒计时/跑绳/弹射或开放成功门。
                _hud?.ShowSetupError();
                if (actor.transform.position.y < _initialCamera.y - _camera.orthographicSize - 2f)
                    Fail();
                return;
            case Phase.Approach:
                if (actor.transform.position.y < _initialCamera.y - _camera.orthographicSize - 2f)
                    Fail();
                else
                {
                    bool grounded = actor.IsGround && Mathf.Abs(actor.RB.linearVelocity.y) < 0.15f;
                    Bounds platform = approachPlatform.bounds;
                    float feet = actor.MainCollider.bounds.min.y;
                    bool onPlatform = grounded && approachPlatform.enabled && !approachPlatform.isTrigger &&
                        actor.transform.position.x >= platform.min.x && actor.transform.position.x <= platform.max.x &&
                        Mathf.Abs(feet - platform.max.y) < 0.18f;
                    bool standing = grounded && Mathf.Abs(feet - _track.SurfaceY(actor.transform.position.x)) < 0.18f;
                    if (!_reachedApproachPlatform)
                    {
                        _standingTime = onPlatform ? _standingTime + dt : 0f;
                        if (_standingTime >= 0.08f)
                        {
                            _reachedApproachPlatform = true;
                            _standingTime = 0f;
                            _track.SetApproachSupport(true);
                        }
                        break;
                    }
                    _standingTime = standing ? _standingTime + dt : 0f;
                    if (_standingTime >= 0.08f && Mathf.Abs(actor.XInput) > 0.01f &&
                        Mathf.Abs(actor.transform.position.x - _anchor.x) <= 0.5f)
                        BeginPreparation();
                }
                break;
            case Phase.Preparation:
                TickDrag();
                if (_phase != Phase.Preparation) break;
                if (!_pulled) _remaining = Mathf.Max(0f, _remaining - dt);
                if (_remaining <= 0f) BeginRun();
                break;
            case Phase.Running:
                TickRun(dt);
                break;
            case Phase.Flight:
                TickFlight(dt);
                break;
            case Phase.Platform:
                if (actor.transform.position.y < _platformCollider.bounds.min.y - 4f)
                    Fail();
                else if (actor.IsGround && Mathf.Abs(actor.transform.position.x - highDoor.position.x) <= 0.5f &&
                         OverlapsDoor(_highGate, actor.MainCollider.bounds))
                    EnterDoor(highDoor);
                break;
            case Phase.ExitBoarding:
                _stageTime = Mathf.Min(boardingSeconds, _stageTime + dt);
                float exitT = _stageTime / boardingSeconds;
                actor.SetPosition(Vector3.Lerp(_stageFrom, _stageTo, exitT));
                actor.SetRopePose(Mathf.Lerp(_exitFromAngle, 0f, Mathf.SmoothStep(0f, 1f, exitT)), 0f);
                if (_stageTime >= boardingSeconds && OverlapsDoor(_mileageGate, actor.MainCollider.bounds))
                {
                    actor.StandFacing(true);
                    EnterDoor(mileageDoor);
                }
                break;
        }
        TickRopeRebound(dt);
        if (_phase == Phase.Preparation)
            actor.SetPosition(StandingOnRope(_anchor.x, false) - Vector3.up * _visualPull);
        UpdateDistanceText();
        RefreshPreparationCountdown();
    }

    private void UpdateDistanceText()
    {
        if (distanceText == null || distanceLocalize == null) return;
        bool becameVisible = !distanceText.gameObject.activeSelf;
        distanceText.gameObject.SetActive(true);
        int goal = Mathf.RoundToInt(targetMeters);
        int current = Mathf.Clamp((int)Math.Floor(_displayMeters), 0, goal);
        if (!becameVisible && current == _shownDistance && goal == _shownGoal) return;
        _shownDistance = current;
        _shownGoal = goal;
        distanceLocalize.OnLocalize(true);
    }

    // 与 131 一致：I2 回调先填入实时里程，再由世界 TMP 目标处理语言与 RTL。
    public void FormatDistanceTranslation()
    {
        const string fallback = "移动距离\n{0}/{1}";
        string template = string.IsNullOrEmpty(Localize.MainTranslation) ? fallback : Localize.MainTranslation;
        int goal = Mathf.RoundToInt(targetMeters);
        int current = Mathf.Clamp((int)Math.Floor(_displayMeters), 0, goal);
        try
        {
            Localize.MainTranslation = string.Format(template, current, goal);
        }
        catch (FormatException)
        {
            if (!_distanceFormatWarningReported)
            {
                Debug.LogWarning("[Level132] Distance translation requires {0}/{1} placeholders.", this);
                _distanceFormatWarningReported = true;
            }
            Localize.MainTranslation = string.Format(fallback, current, goal);
        }
    }

    private void RefreshPreparationCountdown()
    {
        if (_phase != Phase.Preparation || _remaining <= 0f || _waitingForPreparationPopup ||
            Time.frameCount <= _preparationResumeFrame)
        {
            HidePreparationCountdown();
            return;
        }
        if (preparationCountdownText == null) return;
        int seconds = Mathf.CeilToInt(_remaining);
        if (_countdownSeconds == seconds && preparationCountdownText.gameObject.activeSelf) return;
        _countdownSeconds = seconds;
        Transform textTransform = preparationCountdownText.transform;
        textTransform.DOKill();
        textTransform.localScale = _countdownBaseScale;
        preparationCountdownText.text = seconds.ToString();
        preparationCountdownText.gameObject.SetActive(true);
        // 对齐 Draw HUD 的表现，不借用其事件、画线触发或超时结算。
        DOTween.Sequence()
            .SetTarget(textTransform)
            .SetUpdate(true)
            .SetLink(preparationCountdownText.gameObject)
            .Append(textTransform.DOScale(_countdownBaseScale * 1.35f, 0.12f).SetEase(Ease.OutQuad))
            .Append(textTransform.DOScale(_countdownBaseScale, 0.18f).SetEase(Ease.OutBack));
    }

    private void HidePreparationCountdown()
    {
        _countdownSeconds = -1;
        if (preparationCountdownText == null) return;
        preparationCountdownText.transform.DOKill();
        preparationCountdownText.transform.localScale = _countdownBaseScale;
        preparationCountdownText.gameObject.SetActive(false);
    }

    private Vector3 StandingOnRope(float x, bool below)
    {
        return new Vector3(x, _track.SurfaceY(x, below) + actor.FootOffset * (below ? -1f : 1f), _anchor.z);
    }

    private void BeginPreparation()
    {
        if (_phase != Phase.Approach) return;
        RemoveInvitationBubble();
        actor.SetScripted(true);
        actor.StandFacing(true);
        actor.SetPosition(StandingOnRope(_anchor.x, false));
        actor.SetRopeShadowHidden(true); // 弹窗暂停后 LateUpdate 不再负责本帧的绳上显隐。
        _track.SetApproachSupport(false);
        _phase = Phase.Preparation;
        actor.SpineCtrl.PlayAnimation("idle", true);
        ShowPreparationPopup();
    }

    private void ShowPreparationPopup()
    {
        if (_preparationPopupShown) return;
        _preparationPopupShown = true;
        _waitingForPreparationPopup = true; // 在标准 OnShow/PAUSE 事件前锁住倒计时和锯绳。
        ApplyControls();
        actor.SetFrozen(true);
        try
        {
            _preparationPopup = Level132RealTipsPopup.Create();
            if (_preparationPopup != null)
            {
                _preparationPopup.Closed += OnPreparationPopupClosed;
                return;
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }

        Debug.LogWarning("[Level132] Tips popup could not open; continuing with a full preparation countdown.", this);
        // OpenUI 可能在注册实例后抛错；清理该类型的半开实例，避免残留遮罩/输入锁。
        _preparationPopup = UIManager.Instance?.GetUI<Level132RealTipsPopup>(nameof(Level132RealTipsPopup));
        ClosePreparationPopup();
        if (_phase == Phase.Preparation && isActiveAndEnabled && _gamePaused)
            this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PLAYING);
        ResumePreparationAfterPopup();
    }

    private void OnPreparationPopupClosed(Level132RealTipsPopup popup)
    {
        if (popup != _preparationPopup) return;
        popup.Closed -= OnPreparationPopupClosed;
        _preparationPopup = null;
        _waitingForPreparationPopup = false;
        ResumePreparationAfterPopup();
    }

    private void ResumePreparationAfterPopup()
    {
        if (!isActiveAndEnabled || _phase != Phase.Preparation || actor == null || actor.IsDied) return;
        _remaining = preparationSeconds;
        _preparationResumeFrame = Time.frameCount; // 不把关闭叉号的同帧输入交给拖绳，也不扣掉这一帧。
        ApplyControls();
        actor.SetFrozen(true);
    }

    private void ClosePreparationPopup()
    {
        Level132RealTipsPopup popup = _preparationPopup;
        _preparationPopup = null;
        _waitingForPreparationPopup = false;
        if (popup == null) return;
        popup.Closed -= OnPreparationPopupClosed;
        popup.CloseFromOwner(); // 失败/重开/离场不派发 PLAYING，也不启动倒计时。
    }

    private void OnInvitationLeft(PlayerLeftMoveEvent _) { TryShowInvitationBubble(); }
    private void OnInvitationRight(PlayerRightMoveEvent _) { TryShowInvitationBubble(); }

    private void TryShowInvitationBubble()
    {
        // 出门自动移动、暂停或托管跑绳不消费首次操作机会；左右/跳跃共享一次门禁。
        if (_invitationShown || !_initialized || !isActiveAndEnabled || _phase != Phase.Approach ||
            Suspended || actor == null || actor.IsDied || boxBad == null) return;
        _invitationShown = true;
        HintBubble template = Resources.Load<HintBubble>("HintBubble");
        if (template == null)
        {
            Debug.LogWarning("[Level132] Invitation skipped: HintBubble resource is missing.", this);
            return;
        }
        _invitationBubble = Instantiate(template, transform);
        _invitationBubble.Show(invitationBubbleKey, boxBad.transform, InvitationBubbleSeconds);
    }

    private void RemoveInvitationBubble()
    {
        if (_invitationBubble == null) return;
        // 与 083 的即时清理相同：先不可见，再销毁，避免收起动画盖在锁绳弹窗上。
        _invitationBubble.gameObject.SetActive(false);
        Destroy(_invitationBubble.gameObject);
        _invitationBubble = null;
    }

    private void BeginRun()
    {
        CancelDrag();
        try
        {
            _track.BeginRun();
            EnsureTrackAhead(_anchor.x); // 第一小步移动前先补齐，不等跑到首摆边缘才开始准备。
        }
        catch (Exception exception)
        {
            HandleLoopFailure(exception);
            return;
        }
        _phase = Phase.Running;
        _meters = 0d;
        _runSeconds = 0d;
        _track.SetRunMeters(0d);
        _displayMeters = 0d;
        _below = false;
        _switching = false;
        _ropeAngle = 0f;
        actor.SetBelowRope(false);
        actor.SetPosition(StandingOnRope(_anchor.x, false));
        // 保留双手背后的冲刺姿态；真实位移由本关加速模型驱动。
        actor.SpineCtrl.PlayAnimation("walk01", true);
        _previousHitCenter = actor.CollisionCenter;
        _previousHitHalfSize = actor.CollisionHalfSize;
        // 右侧已摆好的门/承托作为终点组保留比例与相对位置，只在运行实例上放到万米终点。
        float shift = _anchor.x + targetMeters - mileageDoor.position.x;
        mileageDoor.position += Vector3.right * shift;
        finishGround.position += Vector3.right * shift;
        // 旧右边界在 x=78.35，若不同行搬迁，长路会在约二十秒时误触默认 FallArea。
        if (_finishFallArea != null) _finishFallArea.position += Vector3.right * shift;
        ApplyControls();
    }

    private void TickRun(float dt)
    {
        // 小步只用于扫掠/复用顺序；总时间不丢弃，里程用 double 累加真实世界距离。
        double timeLeft = dt;
        while (timeLeft > 0d && _phase == Phase.Running)
        {
            bool wasSwitching = _switching;
            // SmoothStep 最大角速度为平均值的 1.5 倍；duration/54 将单步转角限制在约 5° 内。
            double maxStep = _switching ? Math.Min(1d / 60d, Mathf.Max(0.05f, switchTransitionSeconds) / 54d) : 1d / 60d;
            double step = Math.Min(timeLeft, maxStep);
            timeLeft -= step;
            _runSeconds += step;
            double nextMeters = Math.Min(targetMeters, _runCourse.DistanceAtTime(_runSeconds));
            try
            {
                EnsureTrackAhead(_anchor.x + (float)nextMeters);
            }
            catch (Exception exception)
            {
                HandleLoopFailure(exception);
                return;
            }
            _meters = nextMeters;
            _track.SetRunMeters(_meters);
            _displayMeters = _meters; // 只有跑绳真实位移计入普通计数，不计入场行走或准备期。
            TickRunningPose((float)step, _anchor.x + (float)_meters);
            Vector2 center = actor.CollisionCenter;
            Vector2 halfSize = actor.CollisionHalfSize;
            Vector2 sweepHalfSize = Vector2.Max(_previousHitHalfSize, halfSize);
            if (wasSwitching) sweepHalfSize += Vector2.one * 0.01f;
            if (_track.HitsObstacle(_previousHitCenter, center, sweepHalfSize,
                out Vector2 impactPoint, out SpriteRenderer hitObstacle))
            {
                PlayObstacleImpact(impactPoint, hitObstacle);
                Fail();
                return;
            }
            _previousHitCenter = center;
            _previousHitHalfSize = halfSize;
            float fallFront = _anchor.x + (float)_meters - chaseDistance;
            if (center.x - halfSize.x <= fallFront)
            {
                Fail();
                return;
            }
            _track.RecycleBehind(_camera, actor.transform.position.x, fallFront);
            if (_meters >= targetMeters && !_switching) BeginExitBoarding();
        }
    }

    private void EnsureTrackAhead(float nextActorX)
    {
        float cameraRight = _camera.transform.position.x + _camera.orthographicSize * _camera.aspect;
        float requiredRight = Mathf.Max(nextActorX + Mathf.Max(1f, spawnAheadDistance),
            cameraRight + Mathf.Max(0f, spawnViewMargin));
        _track.EnsureAhead(requiredRight);
    }

    private void HandleLoopFailure(Exception exception)
    {
        Debug.LogException(exception, this);
        Debug.LogError("[Level132] Forward loop failed; fix the templates and restart. Gameplay has not been completed.", this);
        _trackFailed = true;
        _track?.Dispose();
        _track = null;
        CancelDrag();
        _switching = false;
        _ropeAngle = 0f;
        actor.SetRopeShadowHidden(false);
        actor.SetFrozen(false);
        actor.StandFacing(true);
        actor.SetScripted(false);
        _phase = Phase.InitializationFailed;
        UpdateDistanceText();
        ApplyControls();
    }

    private void OnSwitchSide(PlayerJumpEvent _)
    {
        TryShowInvitationBubble();
        if (!_initialized || _phase != Phase.Running || Suspended || _cooldown > 0f || _switching || _meters >= targetMeters)
            return;
        _switchFromBelow = _below;
        _switchElapsed = 0f;
        _switching = true;
        _cooldown = Mathf.Max(switchCooldown, switchTransitionSeconds);
        // 按下只登记过渡；位移、旋转、碰撞轮廓都由后续运动小步连续推进。
    }

    private void TickRunningPose(float dt, float x)
    {
        if (_switching)
        {
            float duration = Mathf.Max(0.05f, switchTransitionSeconds);
            _switchElapsed = Mathf.Min(duration, _switchElapsed + dt);
            float t = Mathf.SmoothStep(0f, 1f, _switchElapsed / duration);
            _ropeAngle = Mathf.Lerp(_switchFromBelow ? 180f : 0f, _switchFromBelow ? 0f : 180f, t);
            if (_switchElapsed >= duration)
            {
                _below = !_switchFromBelow;
                _switching = false;
            }
        }
        float radians = _ropeAngle * Mathf.Deg2Rad;
        float top = _track.SurfaceY(x);
        float bottom = _track.SurfaceY(x, true);
        float y = (top + bottom) * 0.5f + ((top - bottom) * 0.5f + actor.FootOffset) * Mathf.Cos(radians);
        float arc = _switching ? Mathf.Max(0f, switchArcOffset) * Mathf.Sin(radians) : 0f;
        actor.SetRopePose(_ropeAngle, arc);
        // 跑动根节点的 X 始终来自真实加速里程；短弧线偏移只在局部表现/碰撞轮廓上。
        actor.SetPosition(new Vector3(x, y, _anchor.z));
    }

    private void BeginExitBoarding()
    {
        _phase = Phase.ExitBoarding;
        _stageFrom = actor.transform.position;
        _exitFromAngle = _ropeAngle;
        actor.Face(true);
        actor.SpineCtrl.PlayAnimation("walk", true); // 离绳登上出口承托时恢复普通步行。
        _stageTo = new Vector3(mileageDoor.position.x, _anchor.y + actor.FootOffset, _anchor.z);
        var ground = finishGround.GetComponent<BoxCollider2D>();
        if (ground != null && ground.enabled)
            _stageTo.y = ground.bounds.max.y + actor.FootOffset + 0.02f;
        _stageTime = 0f;
        ApplyControls();
    }

    private void BeginFlight()
    {
        _dragging = _pulled = false;
        _fingerId = -1;
        _phase = Phase.Flight;
        _flightDisplayFrom = _displayMeters;
        actor.SetPosition(StandingOnRope(_anchor.x, false) - Vector3.up * _pull);
        actor.StandFacing(true);
        _stageFrom = actor.transform.position;
        Bounds platform = _platformCollider.bounds;
        float halfWidth = actor.CollisionHalfSize.x;
        float landingX = Mathf.Clamp(landingPoint.position.x, platform.min.x + halfWidth + 0.1f, platform.max.x - halfWidth - 0.1f);
        _stageTo = new Vector3(landingX, platform.max.y + actor.FootOffset + 0.015f, _anchor.z);
        float apex = Mathf.Max(_stageFrom.y, _stageTo.y) + apexAbovePlatform;
        float up = Mathf.Sqrt(apex - _stageFrom.y);
        float down = Mathf.Sqrt(apex - _stageTo.y);
        _flightGravity = 2f * (up + down) * (up + down) / (flightSeconds * flightSeconds);
        _flightVelocity = new Vector3((_stageTo.x - _stageFrom.x) / flightSeconds,
            Mathf.Sqrt(2f * _flightGravity * (apex - _stageFrom.y)), 0f);
        _flightCameraStart = _camera.transform.position;
        _stageTime = 0f;
        actor.SpineCtrl.PlayAnimation("jump_up", false);
        AudioManager.PlaySound("jump");
        ApplyControls();
    }

    private void TickFlight(float dt)
    {
        _stageTime = Mathf.Min(flightSeconds, _stageTime + dt);
        float t = _stageTime;
        // 只改变显示值；与飞行共用计时，因此暂停也冻结，绝不写入驱动跑绳坐标的 _meters。
        _displayMeters = _flightDisplayFrom + (targetMeters - _flightDisplayFrom) * Mathf.Clamp01(t / flightSeconds);
        actor.SetPosition(_stageFrom + _flightVelocity * t - Vector3.up * (0.5f * _flightGravity * t * t));
        if (_flightVelocity.y - _flightGravity * t < 0f)
            actor.SpineCtrl.PlayAnimation("jump_down", false);
        if (t < flightSeconds)
            return;
        actor.SetPosition(_stageTo);
        _displayMeters = targetMeters;
        // 保留落地前的朝向；这里只恢复移动，后续转向交给玩家的左右输入。
        actor.SetScripted(false);
        actor.SpineCtrl.PlayAnimation("idle", true);
        _phase = Phase.Platform;
        ApplyControls(); // 停止自动右跑，清除旧输入，再开放左右键。
    }

    private static bool OverlapsDoor(BoxCollider2D gate, Bounds actorBounds)
    {
        // 禁用的 MonoBehaviour 仍可能收到物理消息，因此门 Collider 始终关闭。
        // 使用同一序列化门框的几何范围，再叠加本关阶段/站稳/中心距离门禁。
        Vector3 scale = gate.transform.lossyScale;
        Vector3 size = new Vector3(gate.size.x * Mathf.Abs(scale.x), gate.size.y * Mathf.Abs(scale.y), 1f);
        return new Bounds(gate.transform.TransformPoint(gate.offset), size).Intersects(actorBounds);
    }

    private void EnterDoor(Transform door)
    {
        if (_phase != Phase.Platform && _phase != Phase.ExitBoarding)
            return;
        _phase = Phase.Finished;
        CancelDrag();
        actor.SetRopeShadowHidden(false);
        actor.SetScripted(true);
        _highGate.enabled = _mileageGate.enabled = false;
        ApplyControls();
        var success = door.GetComponent<SuccessAnimCtrl>();
        if (success == null)
        {
            Debug.LogError("[Level132] Exit door is missing its success animation.", door);
            return;
        }
        // 必须先选中实际进入的门，才交给标准成功/第三颗星/进门动画链。
        levelCtrl.SetSuccessAnim(success);
        levelCtrl.OnActorTrigger2D(actor.MainCollider);
    }

    private void PlayObstacleImpact(Vector2 position, SpriteRenderer obstacle)
    {
        if (obstacleImpactPrefab == null || obstacle == null)
        {
            Debug.LogWarning("[Level132] Obstacle impact effect is missing; failure still resolves.", this);
            return;
        }
        try
        {
            if (_obstacleImpact == null)
            {
                _obstacleImpact = Instantiate(obstacleImpactPrefab, transform);
                _obstacleImpact.name = "Level132ObstacleImpact";
            }
            _obstacleImpact.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _obstacleImpact.transform.SetPositionAndRotation(
                new Vector3(position.x, position.y, transform.position.z), Quaternion.identity);
            var sorting = _obstacleImpact.GetComponent<SortingGroup>();
            if (sorting == null) sorting = _obstacleImpact.gameObject.AddComponent<SortingGroup>();
            // 一并覆盖子粒子的整体层级，保留通用特效内部的相对排序。
            sorting.sortingLayerID = obstacle.sortingLayerID;
            sorting.sortingOrder = obstacle.sortingOrder + 1;
            _obstacleImpact.Play(true);
        }
        catch (Exception exception)
        {
            ClearObstacleImpact();
            Debug.LogWarning("[Level132] Obstacle impact could not play; failure still resolves. " + exception.Message, this);
        }
    }

    private void ClearObstacleImpact()
    {
        if (_obstacleImpact == null) return;
        _obstacleImpact.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        Destroy(_obstacleImpact.gameObject);
        _obstacleImpact = null;
    }

    private void Fail()
    {
        if (_phase == Phase.Finished)
            return;
        StopAttempt();
        AudioManager.PlaySound("trap");
        AudioManager.PlaySound("failed");
        this.DispatchEvent(Witness<OnTriggerTrapEvent>._);
    }

    private void StopAttempt()
    {
        _phase = Phase.Finished;
        RemoveInvitationBubble();
        ClosePreparationPopup();
        _switching = false;
        CancelDrag();
        if (actor != null && actor.Initialized)
        {
            actor.SetRopeShadowHidden(false);
            actor.SetFrozen(false);
            actor.SetScripted(true);
        }
        if (_highGate != null) _highGate.enabled = false;
        if (_mileageGate != null) _mileageGate.enabled = false;
        ApplyControls();
    }

    private void ApplyControls()
    {
        bool move = _phase == Phase.Approach || _phase == Phase.Platform || _phase == Phase.InitializationFailed;
        bool jump = _phase == Phase.Approach || _phase == Phase.InitializationFailed;
        if (actor != null)
        {
            actor.AllowKeyboardInput = move && !Suspended;
            actor.AllowNormalJump = jump && !Suspended;
        }
        if (_phase != Phase.Entrance) levelCtrl?.SetCtrlable(move);
        _hud?.SetControls(move, jump, _phase == Phase.Running);
        RefreshPreparationCountdown();
        RefreshBoxBadPresentation();
    }

    private void RefreshBoxBadPresentation()
    {
        if (boxBad == null) return;
        if (_phase == Phase.Finished || !isActiveAndEnabled)
        {
            boxBad.ClearSawPresentation();
            return;
        }
        // 真正下拉只冻结 _remaining，不冻结锯绳；游戏暂停/遮罩/失焦才冻结动画。
        boxBad.SetSawPresentation(_phase == Phase.Preparation && !_waitingForPreparationPopup, Suspended);
    }

    private void TickBoxBad()
    {
        // ActorCtrl.Start 完成后才调用通用 API，避免脚本 Start 顺序影响输入锁。
        if (boxBad == null || !boxBad.isActiveAndEnabled || boxBad.RB == null)
            return;
        if (!_boxBadInitialized)
        {
            boxBad.SetMoveInput(0f);
            // 只调用一次：关闭主动移动/Editor 输入，但不关闭动态刚体或重力。
            // OnUpdate 保留通用接地检测；派生组件仅在锯绳/暂停时拦截通用动画。
            boxBad.StopMoving();
            _boxBadInitialized = true;
        }
        RefreshBoxBadPresentation();
        if (!Suspended)
            boxBad.OnUpdate();
    }

    private void LateUpdate()
    {
        if (!_initialized || SetupFailed || actor == null || _phase == Phase.Finished || Suspended)
            return;
        // 普通行走按实际绳支撑接触判断，不等锁点或倒计时；托管时上下附绳均隐藏。
        // Flight / Platform / ExitBoarding 已离绳，恢复通用投影，不再跟随 SetScripted。
        bool onRope = _phase == Phase.Preparation || _phase == Phase.Running ||
            (_phase == Phase.Approach && actor.RB.linearVelocity.y <= 0.15f &&
             _track != null && _track.IsStandingOnApproachRope(actor.MainCollider));
        actor.SetRopeShadowHidden(onRope);
        Vector3 cameraPosition = _camera.transform.position;
        if (_phase == Phase.Running || _phase == Phase.ExitBoarding)
        {
            // 对齐 131 的 Actor 跟随分支：前瞻 3.5，每帧插值 18%，不保留旧起点 X 钳制。
            float desiredX = actor.transform.position.x + cameraLookAhead;
            cameraPosition.x = Mathf.Lerp(cameraPosition.x, desiredX, 0.18f);
            cameraPosition.y = _initialCamera.y;
        }
        else if (_phase == Phase.Flight)
        {
            // 高台接近两百单位高：飞行镜头直接保持起飞构图，避免平滑滞后把 Actor 丢出画面。
            cameraPosition = _flightCameraStart + actor.transform.position - _stageFrom;
        }
        else if (_phase == Phase.Platform)
        {
            float blend = 1f - Mathf.Exp(-Time.deltaTime / 0.15f);
            cameraPosition.x = Mathf.Lerp(cameraPosition.x, topPlatform.position.x, blend);
            cameraPosition.y = Mathf.Lerp(cameraPosition.y, _platformCollider.bounds.max.y + 2f, blend);
        }
        // 所有跟随分支完成后统一限制上沿，避免 Flight 或 Platform 再将镜头抬高。
        cameraPosition.y = Mathf.Min(cameraPosition.y, cameraTopLimitY - _camera.orthographicSize);
        cameraPosition.z = _initialCamera.z;
        _camera.transform.position = cameraPosition;
        try
        {
            _backdrop.Step();
            bool chasing = _phase == Phase.Running || _phase == Phase.ExitBoarding;
            _track.DrawRope(_anchor.x, _visualPull, chasing ? pullSpan : _pullViewSpan, chasing,
                _anchor.x + (float)_meters - chaseDistance, pullCornerRadius);
        }
        catch (Exception exception)
        {
            HandleLoopFailure(exception);
        }
    }

    private void TickDrag()
    {
        if (_dragging)
        {
            if (_fingerId >= 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.fingerId != _fingerId) continue;
                    if (touch.phase == TouchPhase.Canceled) { CancelDrag(); return; }
                    UpdatePull(touch.position);
                    if (touch.phase == TouchPhase.Ended) ReleaseDrag();
                    return;
                }
                CancelDrag(); // 触点丢失不是松手成功。
                return;
            }
            UpdatePull(Input.mousePosition);
            if (Input.GetMouseButtonUp(0)) ReleaseDrag();
            else if (!Input.GetMouseButton(0)) CancelDrag();
            return;
        }
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began && TryBeginDrag(touch.position, touch.fingerId)) break;
            }
        }
        else if (Input.GetMouseButtonDown(0)) TryBeginDrag(Input.mousePosition, -1);
    }

    private bool TryBeginDrag(Vector2 screen, int finger)
    {
        if (_phase != Phase.Preparation || _remaining <= 0f || Suspended || PointerHitsUI(screen))
            return false;
        Vector3 world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Abs(_camera.transform.position.z - _anchor.z)));
        if (Mathf.Abs(world.x - _anchor.x) > 1.5f || Mathf.Abs(world.y - _anchor.y) > 0.6f)
            return false;
        try
        {
            CapturePullView();
        }
        catch (Exception exception)
        {
            HandleLoopFailure(exception);
            return false;
        }
        _dragging = true;
        _pulled = false;
        _fingerId = finger;
        _pressPosition = screen;
        return true;
    }

    private bool PointerHitsUI(Vector2 position)
    {
        if (EventSystem.current == null) return false;
        _uiHits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, _uiHits);
        foreach (var hit in _uiHits)
            if (hit.module is GraphicRaycaster) return true;
        return false;
    }

    private void UpdatePull(Vector2 screen)
    {
        float down = (_pressPosition.y - screen.y) * (2f * _camera.orthographicSize / Mathf.Max(1, _camera.pixelHeight));
        _pull = Mathf.Clamp(down, 0f, maximumPull);
        if (_pull > 0.05f) _pulled = true; // 真正下拉后暂停准备倒计时，纯按住不冻结。
    }

    private void ReleaseDrag()
    {
        bool launch = _phase == Phase.Preparation && !Suspended && _remaining > 0f && _pull >= minimumPull;
        float releasedPull = _pull;
        _dragging = _pulled = false;
        _fingerId = -1;
        if (launch) BeginFlight();
        _pull = 0f;
        _releasePull = releasedPull;
        _releaseTime = 0f;
    }

    private void TickRopeRebound(float dt)
    {
        if (_dragging)
        {
            _visualPull = _pull;
            _releasePull = 0f;
            _releaseTime = 1f;
            return;
        }
        _releaseTime = Mathf.Min(1f, _releaseTime + dt);
        // 有限时长的衰减回弹；只有视觉和准备期脚下随动，不改弹射轨迹/跑绳判定。
        float envelope = 1f - Mathf.SmoothStep(0f, 1f, _releaseTime);
        _visualPull = _releasePull * Mathf.Exp(-5f * _releaseTime) *
            Mathf.Cos(12f * _releaseTime) * envelope;
    }

    private void CancelDrag()
    {
        _dragging = _pulled = false;
        _fingerId = -1;
        _pull = 0f;
        _visualPull = _releasePull = 0f;
        _releaseTime = 1f;
    }

    private void OnPause(GamePauseEvent _) { _gamePaused = true; CancelDrag(); RefreshBoxBadPresentation(); }
    private void OnResume(GameResumeEvent _) { _gamePaused = false; RefreshBoxBadPresentation(); }
    private void OnInputBlock(GamePlayPanelSwitchBlockEvent e) { _eventBlocked = e.IsShow; if (_eventBlocked) CancelDrag(); RefreshBoxBadPresentation(); }
    private void OnGameState(SwitchGameStateEvent e)
    {
        if (e.NewState == GamePlayStateName.PAUSE) { _gamePaused = true; CancelDrag(); }
        else if (e.NewState == GamePlayStateName.PLAYING) _gamePaused = false;
        else if (e.NewState == GamePlayStateName.SUCCESS || e.NewState == GamePlayStateName.FAILED) StopAttempt();
        RefreshBoxBadPresentation();
    }
    private void OnReset(GameResetEvent _) { ClearObstacleImpact(); StopAttempt(); }
    private void OnReplay(GameReplayEvent _) { ClearObstacleImpact(); StopAttempt(); }
    private void OnExternalTrap(OnTriggerTrapEvent _) { StopAttempt(); }
    private void OnExternalFall(OnTriggerFailAreaEvent _) { StopAttempt(); }
    private void OnApplicationPause(bool pause) { _applicationPaused = pause; if (pause) CancelDrag(); RefreshBoxBadPresentation(); }
    private void OnApplicationFocus(bool focus) { _hasFocus = focus; if (!focus) CancelDrag(); RefreshBoxBadPresentation(); }

    private void OnDisable()
    {
        RemoveInvitationBubble();
        ClosePreparationPopup();
        if (distanceText != null) distanceText.gameObject.SetActive(false);
        ClearObstacleImpact();
        actor?.SetRopeShadowHidden(false);
        boxBad?.ClearSawPresentation();
        HidePreparationCountdown();
        EventsUtils.ResetEvents(ref _subscriber);
        CancelDrag();
        RestoreActorCollisionIsolation();
        if (actor != null) actor.SetFrozen(!SetupFailed);
        _hud?.Dispose();
        _hud = null;
    }

    private void OnDestroy()
    {
        RemoveInvitationBubble();
        ClosePreparationPopup();
        ClearObstacleImpact();
        boxBad?.ClearSawPresentation();
        HidePreparationCountdown();
        RestoreActorCollisionIsolation();
        _track?.Dispose();
        _backdrop?.Dispose();
    }
}
