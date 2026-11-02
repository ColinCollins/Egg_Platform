using Bear.EventSystem;
using Game.Events;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using Spine.Unity;
using UnityEngine;

/// <summary>149 双转盘：左台手动充电，右盘每次点击自动跑三分之一；共享钥匙/门锁负责最终解锁。</summary>
public sealed class Level149Ctrl : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private BaseLevelCtrl level;
    [SerializeField] private Level149ActorCtrl actor;
    [SerializeField] private SpriteRenderer leftWheel;
    [SerializeField] private SpriteRenderer rightWheel;
    [SerializeField] private SpriteRenderer leftBattery;
    [SerializeField] private SpriteRenderer rightBattery;
    [SerializeField] private Transform leftFill;
    [SerializeField] private Transform rightFill;
    [SerializeField] private Transform leftFillBackground;
    [SerializeField] private Transform rightFillBackground;
    [SerializeField] private GameObject cageFront;
    /// <summary>笼子后层及线缆的人工Sprite；只与前栏一起炸飞，不移动钥匙或电池。</summary>
    [SerializeField] private Transform cageBack;
    /// <summary>只读复用50关火箭的GrenadeExplosionFire；仅实例化粒子，不执行伤害/死亡链。</summary>
    [SerializeField] private ParticleSystem cageExplosionPrefab;
    [SerializeField] private Collider2D keyCollider;
    [SerializeField] private Sprite chargeIcon;
    [SerializeField] private Sprite electricFrameA;
    [SerializeField] private Sprite electricFrameB;
    [Tooltip("人工摆放的未激活 A 帧节点；只采用位置、旋转和缩放，不采用其图层或顺序。")]
    [SerializeField] private Transform electricPlacement;
    [Tooltip("原图左上角坐标系中的黑色栏杆中心；两帧不能直接共用图片中心。")]
    [SerializeField] private Vector2 electricCageCenterPixelsA = new Vector2(324.5f, 256f);
    [SerializeField] private Vector2 electricCageCenterPixelsB = new Vector2(364.5f, 304f);
    [SerializeField] private SkeletonDataAsset boxPinkData;

    [Header("Charge tuning")]
    [SerializeField, Range(0f, 1f)] private float initialLeftCharge = 0.2f;
    /// <summary>开场左电量由0涨至原20%的时长；仅有效玩法时间推进，不循环赠送。</summary>
    [SerializeField, Min(0.01f)] private float initialChargeSeconds = 2f;
    [SerializeField, Min(0f)] private float chargePerSecond = 0.25f;
    [Tooltip("每次点击右侧闪电按钮的自动跑动最多增加的右电量；当前为三分之一。")]
    [SerializeField, Range(0.01f, 1f)] private float autoChargeAmount = 1f / 3f;
    [Tooltip("右电量停止有效自动跑动后的宽限时间；超过后才开始连续衰减。")]
    [SerializeField, Min(0f)] private float chargeDecayDelaySeconds = 2f;
    [Tooltip("右电量超过宽限时间后的线性衰减速度，单位为每秒归一化电量。")]
    [SerializeField, Min(0f)] private float chargeDecayPerSecond = 0.25f;
    [SerializeField, Min(0.01f)] private float maxGaitCyclesPerSecond = 2f;
    [SerializeField, Min(0.01f)] private float leftSlowGaitCyclesPerSecond = 1.2f;
    [Tooltip("按住方向立即 walk01；最高步频倍率，不再使用渐加速曲线。")]
    [SerializeField, Min(1f)] private float fastRunVisualMultiplier = 1.25f;
    [Tooltip("玩家驱动盘面的最终最高角速度，360 表示每秒一圈；高速表现倍率不再突破此上限。")]
    [SerializeField, Min(0f)] private float maxWheelDegreesPerSecond = 480f;
    [SerializeField, Min(0f)] private float pinkWalkDegreesPerSecond = 40f;
    [Tooltip("超过此角速度首次触发 BoxPink 失衡；本局触发后保持 die，不再恢复 walk/idle。")]
    [SerializeField, Min(0.01f)] private float pinkMaxFollowDegreesPerSecond = 160f;
    [Tooltip("转盘累计转过多少度完成一次底部身体翻滚；盘速越快，翻滚周期越短。")]
    [SerializeField, Min(1f)] private float pinkTumbleCycleDegrees = 480f;
    [Tooltip("翻滚中心在底部的横向摆幅，世界单位；不沿内壁爬高。")]
    [SerializeField, Min(0f)] private float pinkTumbleSway = 0.08f;
    [Tooltip("底部翻滚的轻微起伏上限，世界单位；不编排带起再落回的阶段。")]
    [SerializeField, Min(0f)] private float pinkTumbleLift = 0.03f;
    [SerializeField, Min(0.01f)] private float pinkTumbleBlendSeconds = 0.15f;
    [SerializeField, Min(0f)] private float boardDistance = 2.2f;
    [SerializeField, Min(0.01f)] private float boardSeconds = 0.2f;
    [Header("Left wheel capture (no standing platform)")]
    [SerializeField, Range(0.01f, 1f)] private float leftCaptureWidthRatio = 0.5f;
    [Tooltip("脚底接近盘顶的上下半高，世界单位；只检测，不承托。")]
    [SerializeField, Min(0.01f)] private float leftCaptureHalfHeight = 0.3f;
    [Tooltip("左台吸附时长下限，秒；实际时长按锁点距离推导后再夹在上下限之间。")]
    [SerializeField, Min(0.01f)] private float leftBoardMinSeconds = 0.22f;
    [SerializeField, Min(0.01f)] private float leftBoardMaxSeconds = 0.32f;
    [Tooltip("推导吸附时长的参考速度，世界单位／秒；距离越远吸附越久。")]
    [SerializeField, Min(0.01f)] private float leftBoardReferenceSpeed = 5f;
    [Tooltip("吸附前段占比：先按入场速度自然飘行并减速到零，之后用平滑曲线收进锁点。")]
    [SerializeField, Range(0.05f, 0.9f)] private float leftBoardLeadPortion = 0.45f;
    [Tooltip("入场速度允许消耗的锁点距离比例；超出即衰减，保证任何一轴都不越过锁点。")]
    [SerializeField, Range(0f, 0.9f)] private float leftBoardLeadRatio = 0.5f;
    [Tooltip("到位落地拍时长，秒；保持跳跃收势、盘速从 0 起转，之后才进入原地跑动。")]
    [SerializeField, Min(0f)] private float leftBoardSettleSeconds = 0.09f;
    [SerializeField, Min(0.01f)] private float electricFrameSeconds = 0.1f;
    /// <summary>自动上下台为本关小弧线，离台完成前保持接管，避免共享18力度大跳。</summary>
    [SerializeField, Min(0.01f)] private float dismountSeconds = 0.32f;
    /// <summary>右盘上台的附加弧高；离台只用0.18的小幅离地。</summary>
    [SerializeField, Min(0f)] private float boardingArcHeight = 0.35f;
    /// <summary>双满后笼子被炸飞的时长，结束后才开放钥匙；暂停由本关统一冻结。</summary>
    [SerializeField, Min(0.1f)] private float cageBlastSeconds = 0.8f;

    private enum Station { None, LeftTop, RightInside, Leaving }
    private Station _station;
    private EventSubscriber _events;
    private Level149Hud _hud;
    private GameObject _runtime;
    private SkeletonAnimation _pink;
    private Transform _pinkPivot;
    private SpriteRenderer _electric;
    private SpriteRenderer _cageRenderer;
    private Bounds _leftBounds;
    private Bounds _rightBounds;
    private readonly BoxCollider2D[] _batteryPlatforms = new BoxCollider2D[2];
    private float _leftCharge;
    private float _rightCharge;
    private float _rightChargeIdleSeconds;
    private float _runSpeed;
    private float _runnerFacing = -1f;
    private float _electricTime;
    private float _openTime;
    private float _boardTime;
    private bool _batteryJumpArmed;
    private bool _leftEnteredFull;
    private bool _hasPreviousLeftFoot;
    private Vector3 _previousLeftFoot;
    private float _pinkTumbleTravel;
    private float _pinkTumbleBlend;
    private Vector3 _pinkTumbleStartPosition;
    private Quaternion _pinkTumbleStartRotation;
    private float _autoRunRemaining;
    private float _autoRunTargetCharge;
    private bool _autoRunning;
    private bool _leaveWheelAfterFrame;
    private float _rightDegreesThisFrame;
    private Vector3 _boardStart;
    private Vector3 _anchor;
    private float _leftBoardDuration;
    private float _leftBoardLead;
    private Vector3 _leftBoardLeadVelocity;
    private float _leftBoardSettleRemaining;
    private float _wheelSpeedFactor;
    private Quaternion _leftRotation;
    private Quaternion _rightRotation;
    private bool _opening;
    private bool _opened;
    private bool _paused;
    private bool _terminal;
    private bool _focused = true;
    private bool _applicationPaused;
    private bool _ready;
    private bool _hudError;
    private bool _pinkHasTumbled;
    private float _initialChargeRemaining;
    private Vector3 _leftTakeoffPosition, _dismountStart, _dismountEnd;
    private float _dismountTime;
    private Vector3 _blastCenter, _blastFrontOffset, _blastBackOffset;
    private Quaternion _blastFrontRotation, _blastBackRotation;
    private ParticleSystem _explosion;
    private float _explosionAge, _explosionLife;
    private readonly Spine.Bone[] _pinkEyeBones = new Spine.Bone[2];
    private readonly float[] _pinkEyeAngles = new float[2];
    private readonly float[] _pinkEyeSpeeds = new float[2];

    // 无玩家驱动时只有尚未失衡的 BoxPink 能带动左盘；die 后不能继续自动慢转。
    private float UnattendedLeftAngularSpeed => _pinkHasTumbled ? 0f : -pinkWalkDegreesPerSecond;

    /// <summary>满电后重新登左台可用跳跃键小跳离台；右盘仍完整跑完一段。</summary>
    public void RequestDismount()
    {
        if (CanOperate && _station == Station.LeftTop && _leftEnteredFull) BeginDismount();
    }

    /// <summary>当前关卡是否允许处理玩家输入和本关充电操作。</summary>
    /// <remarks>终态、暂停、失焦、Actor 未初始化或 HUD 不可用时均返回 false。</remarks>
    public bool CanOperate => _ready && !_terminal && !_paused && _focused && !_applicationPaused &&
        level != null && level.ActorCtrlable && !level.IsActorEnterDoor && actor != null &&
        actor.Initialized && !actor.IsDied && _hud != null && !_hud.Blocked;

    private void Awake()
    {
        if (level == null || actor == null || leftWheel == null || rightWheel == null ||
            leftBattery == null || rightBattery == null ||
            leftFill == null || rightFill == null || leftFillBackground == null || rightFillBackground == null ||
            cageFront == null || cageBack == null || cageExplosionPrefab == null || keyCollider == null || chargeIcon == null || electricFrameA == null ||
            electricFrameB == null || electricPlacement == null || boxPinkData == null)
        {
            Debug.LogError("[Level149] Missing gameplay references; charging remains disabled.", this);
            enabled = false;
            return;
        }
        actor.Gameplay = this;
        keyCollider.enabled = false;
        _leftRotation = leftWheel.transform.localRotation;
        _rightRotation = rightWheel.transform.localRotation;
        _leftBounds = leftWheel.bounds;
        _rightBounds = rightWheel.bounds;
        _leftCharge = 0f;
        _initialChargeRemaining = Mathf.Clamp01(initialLeftCharge);
        _rightCharge = 0f;
        RefreshBattery(leftFill, leftFillBackground, _leftCharge);
        RefreshBattery(rightFill, rightFillBackground, _rightCharge);
        CreateLocalVisuals();
        _ready = true;
    }

    private void OnEnable()
    {
        EventsUtils.ResetEvents(ref _events);
        _events.Subscribe<GamePauseEvent>(_ => Pause(true));
        _events.Subscribe<GameResumeEvent>(_ => Pause(false));
        _events.Subscribe<SwitchGameStateEvent>(e =>
        {
            if (e.NewState == GamePlayStateName.PAUSE) Pause(true);
            else if (e.NewState == GamePlayStateName.PLAYING) Pause(false);
            else if (e.NewState == GamePlayStateName.SUCCESS) StopAttempt();
            else if (e.NewState == GamePlayStateName.FAILED) StopAttempt();
        });
        _events.Subscribe<GameResetEvent>(_ => StopAttempt());
        _events.Subscribe<GameReplayEvent>(_ => StopAttempt());
        _events.Subscribe<OnTriggerTrapEvent>(_ => StopAttempt());
        _events.Subscribe<OnTriggerFailAreaEvent>(_ => StopAttempt());
    }

    private void CreateLocalVisuals()
    {
        _runtime = new GameObject("Level149Runtime");
        _runtime.transform.SetParent(leftWheel.transform.parent, false);
        _batteryPlatforms[0] = CreateBatteryPlatform(leftBattery, "LeftBatteryTop");
        _batteryPlatforms[1] = CreateBatteryPlatform(rightBattery, "RightBatteryTop");

        _pinkPivot = new GameObject("BoxPinkRollPivot").transform;
        _pinkPivot.SetParent(_runtime.transform, false);
        var pinkObject = new GameObject("BoxPink", typeof(MeshFilter), typeof(MeshRenderer));
        // 先在 inactive 对象上赋资源，避免 SkeletonAnimation.Awake 在无数据时抢先初始化。
        pinkObject.SetActive(false);
        pinkObject.transform.SetParent(_pinkPivot, false);
        pinkObject.transform.localScale = Vector3.one * 0.4f;
        pinkObject.transform.localPosition = Vector3.down * 0.48f;
        _pink = pinkObject.AddComponent<SkeletonAnimation>();
        _pink.skeletonDataAsset = boxPinkData;
        _pink.initialSkinName = "box_pink";
        _pink.Initialize(true);
        BindPinkEyeSpin();
        var renderer = pinkObject.GetComponent<MeshRenderer>();
        renderer.sortingLayerID = leftWheel.sortingLayerID;
        renderer.sortingOrder = leftWheel.sortingOrder + 1;
        pinkObject.SetActive(true);
        _pink.AnimationState.SetAnimation(0, "walk", true);
        UpdatePink(0f, UnattendedLeftAngularSpeed, false);

        var electricObject = new GameObject("ChargingElectricity", typeof(SpriteRenderer));
        // 保留 lz 的 SortingGroup；不挂在未激活的人工基准下。
        electricObject.transform.SetParent(_runtime.transform, false);
        _electric = electricObject.GetComponent<SpriteRenderer>();
        _cageRenderer = cageFront.GetComponent<SpriteRenderer>();
        _electric.sharedMaterial = _cageRenderer.sharedMaterial;
        _electric.sortingLayerID = _cageRenderer.sortingLayerID;
        _electric.sortingOrder = _cageRenderer.sortingOrder + 1;
        _electric.enabled = false;
        SetElectricFrame(false);
    }

    private BoxCollider2D CreateBatteryPlatform(SpriteRenderer battery, string objectName)
    {
        var platform = new GameObject(objectName, typeof(BoxCollider2D), typeof(PlatformEffector2D));
        platform.transform.SetParent(_runtime.transform, false);
        platform.layer = LayerMask.NameToLayer("Ground");
        Bounds spriteBounds = battery.sprite.bounds;
        Matrix4x4 toRuntime = _runtime.transform.worldToLocalMatrix * battery.transform.localToWorldMatrix;
        platform.transform.localPosition = toRuntime.MultiplyPoint3x4(
            new Vector3(spriteBounds.center.x, spriteBounds.max.y - 0.06f, 0f));
        platform.transform.localRotation = toRuntime.rotation;
        platform.transform.localScale = toRuntime.lossyScale;
        var collider = platform.GetComponent<BoxCollider2D>();
        collider.size = new Vector2(spriteBounds.size.x, 0.12f);
        collider.usedByEffector = true;
        var effector = platform.GetComponent<PlatformEffector2D>();
        effector.useOneWay = true;
        effector.useOneWayGrouping = true;
        effector.useSideFriction = false;
        effector.useSideBounce = false;
        return collider;
    }

    /// <summary>判断 Actor 当前是否站在本关运行时创建的电池承托平台上。</summary>
    /// <remarks>只在关卡已准备、Actor 已初始化且处于可判定的落地状态时返回 true。</remarks>
    public bool IsActorOnBattery()
    {
        if (!_ready || _terminal || actor == null || !actor.Initialized || actor.MainCollider == null ||
            !actor.IsGround || actor.RB.linearVelocityY > 0.1f) return false;
        foreach (var platform in _batteryPlatforms)
            if (platform != null && platform.enabled && platform.IsTouching(actor.MainCollider)) return true;
        return false;
    }

    /// <summary>登记一次地面起跳是否来自左侧电池平台，以便本关判定左台充电资格。</summary>
    /// <param name="jumpedFromBattery">共享 Actor 在起跳前确认的电池承托结果。</param>
    /// <remarks>仅由本关 Actor 控制器在实际地面起跳时调用，不作为外部强制充电入口。</remarks>
    public void NotifyGroundJump(bool jumpedFromBattery)
    {
        // 每次地面起跳重新核对：只有电池承托上真正产生向上速度才授予本跳资格。
        _batteryJumpArmed = jumpedFromBattery && CanOperate && _station == Station.None;
        if (_batteryJumpArmed) _leftTakeoffPosition = actor.transform.position;
        _hasPreviousLeftFoot = false;
    }

    private void BindPinkEyeSpin()
    {
        // 只读原 die 的眼睛旋转轨道，不修改共享动画或循环重播身体。
        string[] names = { "eye_control_L2", "eye_control_R2" };
        Spine.Animation die = _pink.Skeleton.Data.FindAnimation("die");
        for (int i = 0; i < names.Length; i++)
        {
            Spine.Bone bone = _pink.Skeleton.FindBone(names[i]);
            if (bone != null && die != null)
            {
                foreach (Spine.Timeline timeline in die.Timelines)
                {
                    if (!(timeline is Spine.RotateTimeline rotation) || rotation.BoneIndex != bone.Data.Index ||
                        rotation.Duration <= 0f) continue;
                    float start = rotation.GetCurveValue(0f);
                    _pinkEyeBones[i] = bone;
                    _pinkEyeAngles[i] = start;
                    _pinkEyeSpeeds[i] = (rotation.GetCurveValue(rotation.Duration) - start) / rotation.Duration;
                    break;
                }
            }
            if (_pinkEyeBones[i] == null)
                Debug.LogWarning("[Level149] Missing die eye rotation: " + names[i], this);
        }
        _pink.UpdateLocal += ApplyPinkEyeSpin;
    }

    private void ApplyPinkEyeSpin(ISkeletonAnimation animated)
    {
        if (!_pinkHasTumbled) return;
        // 动画先应用，随后仅覆盖两只眼睛的本地旋转；暂停时重复应用同一角度，不回退到末帧。
        for (int i = 0; i < _pinkEyeBones.Length; i++)
            if (_pinkEyeBones[i] != null)
                _pinkEyeBones[i].Rotation = _pinkEyeBones[i].Data.Rotation + _pinkEyeAngles[i];
    }

    private void LateUpdate()
    {
        if (!_ready || _terminal) return;
        if (actor == null || actor.IsDied)
        {
            StopAttempt();
            return;
        }
        if (level.IsActorEnterDoor)
        {
            // 成功入门期间保持笼子已经炸飞的状态；旧实例退出时只做 teardown，不回写 Prefab 初态。
            StopAttempt();
            return;
        }
        BindHud();
        bool active = CanOperate;
        if (_hud != null) _hud.Refresh(active && IsNearRightStation(), active && CanBoardRight());
        // 共享暂停不保证 timeScale=0；本关物理/动画/计时显式停住。
        bool freeze = _paused || !_focused || _applicationPaused ||
            (_hud != null && _hud.Blocked && level.ActorCtrlable);
        actor.SetPaused(freeze && !_terminal && !actor.IsDied);
        if (_pink != null) _pink.timeScale = active ? 1f : 0f;
        if (!active)
        {
            _hasPreviousLeftFoot = false;
            _electric.enabled = false;
            return;
        }
        float dt = Time.deltaTime;
        TickInitialCharge(dt);
        TickExplosion(dt);
        _rightDegreesThisFrame = 0f;
        TickRightChargeDecay(dt);
        if (_station == Station.None) TryCaptureLeft();
        if (_station != Station.None)
        {
#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.Q)) actor.IsEditor = !actor.IsEditor;
            if (actor.IsEditor && Input.GetKeyDown(KeyCode.Space)) actor.TriggerJump();
#endif
            if (_station != Station.None) TickRunner(dt);
        }
        // 左台按住立即最高速、松手慢走；右台只采用本次自动跑累计出的角位移。
        float leftAngularSpeed = UnattendedLeftAngularSpeed;
        if (_station == Station.LeftTop)
            leftAngularSpeed = _boardTime < Mathf.Max(0.01f, _leftBoardDuration) ? 0f :
                (_runSpeed != 0f ? _runSpeed * maxWheelDegreesPerSecond : _runnerFacing * pinkWalkDegreesPerSecond) *
                _wheelSpeedFactor;
        float leftDegrees = leftAngularSpeed * dt;
        float rightDegrees = _rightDegreesThisFrame;
        leftWheel.transform.Rotate(0f, 0f, leftDegrees, Space.Self);
        rightWheel.transform.Rotate(0f, 0f, rightDegrees, Space.Self);
        UpdatePink(leftDegrees, leftAngularSpeed, _station == Station.LeftTop, dt);
        RefreshBattery(leftFill, leftFillBackground, _leftCharge);
        RefreshBattery(rightFill, rightFillBackground, _rightCharge);
        if (!_opening && !_opened && _leftCharge >= 1f && _rightCharge >= 1f) BeginOpenCage();
        if (_opening) TickCageBlast(dt);
        // 完成后保持接管走小弧线，落点到达才释放物理；不再调用共享Jump。
        if ((_station == Station.LeftTop && !_leftEnteredFull && _leftCharge >= 1f) ||
            (_leaveWheelAfterFrame && _station == Station.RightInside)) BeginDismount();
    }

    private void BindHud()
    {
        var panel = PlayCtrl.Instance != null ? PlayCtrl.Instance.CurrentGamePlayPanel : null;
        if (_hud != null && _hud.Panel != panel) { _hud.Dispose(); _hud = null; }
        if (_hud != null || panel == null || _hudError || _terminal) return;
        try { _hud = new Level149Hud(panel, chargeIcon, BoardRight); }
        catch (System.Exception e)
        {
            _hudError = true;
            Debug.LogError("[Level149] Cannot bind charging HUD: " + e.Message, this);
        }
    }

    private bool IsNearRightStation()
    {
        // 右台随时可进入；先充满左侧再连续跑三次只是推荐解法，不是入口门禁。
        if (_station != Station.None || !actor.Initialized) return false;
        Vector3 position = actor.transform.position;
        return Mathf.Abs(position.x - _rightBounds.center.x) <= boardDistance &&
            position.y < _rightBounds.center.y + 0.5f && position.y > _rightBounds.min.y - 1f;
    }

    private bool CanBoardRight() => IsNearRightStation() && actor.IsGround;

    private void BoardRight()
    {
        if (!CanOperate || !CanBoardRight()) return;
        float footOffset = actor.transform.position.y - actor.MainCollider.bounds.min.y;
        _anchor = new Vector3(_rightBounds.center.x, _rightBounds.min.y + 0.18f + footOffset, actor.transform.position.z);
        _rightChargeIdleSeconds = 0f;
        EnterStation(Station.RightInside);
    }

    private void TryCaptureLeft()
    {
        if (!_batteryJumpArmed)
        {
            _hasPreviousLeftFoot = false;
            return;
        }
        // 电池起跳资格仅限当前一跳；落回任何承托面即失效，不因走下电池获得资格。
        if (actor.IsGround && actor.RB.linearVelocityY <= 0.1f)
        {
            _batteryJumpArmed = false;
            _hasPreviousLeftFoot = false;
            return;
        }
        if (!actor.TryGetWheelFoot(out Vector3 foot, out float footOffset)) return;
        foot.z = 0f;
        Bounds capture = new Bounds(new Vector3(_leftBounds.center.x, _leftBounds.max.y, 0f),
            new Vector3(_leftBounds.size.x * leftCaptureWidthRatio, leftCaptureHalfHeight * 2f, 1f));
        bool inside = capture.Contains(foot);
        Vector3 travel = foot - _previousLeftFoot;
        bool crossed = _hasPreviousLeftFoot && travel.sqrMagnitude > 0.000001f &&
            capture.IntersectRay(new Ray(_previousLeftFoot, travel.normalized), out float distance) &&
            distance <= travel.magnitude;
        _previousLeftFoot = foot;
        _hasPreviousLeftFoot = true;
        // 只有从电池起跳的上升/下降可捕获，不再允许地面直跳吸入左台。
        if (actor.IsGround || (!inside && !crossed)) return;
        _anchor = new Vector3(_leftBounds.center.x, _leftBounds.max.y + footOffset - 0.09f,
            actor.transform.position.z);
        EnterStation(Station.LeftTop);
    }

    private void EnterStation(Station station)
    {
        _station = station;
        _leftEnteredFull = station == Station.LeftTop && _leftCharge >= 1f;
        _runSpeed = 0f;
        // 入台瞬间先保持 Actor 当前朝向；左台到位转左，右台开始自动跑时转右。
        _runnerFacing = actor.FaingRight ? 1f : -1f;
        _batteryJumpArmed = false;
        _boardTime = 0f;
        _boardStart = actor.transform.position;
        _leftBoardSettleRemaining = 0f;
        _wheelSpeedFactor = 1f; // 右台自动跑直接使用目标速度；左台在下方改为从零起转。
        if (station == Station.LeftTop)
        {
            // 吸附时长按锁点距离推导；前段用入场速度飘行，末段才平滑收敛，不再固定 0.2 秒匀速。
            float duration = Mathf.Clamp((_anchor - _boardStart).magnitude / leftBoardReferenceSpeed,
                leftBoardMinSeconds, Mathf.Max(leftBoardMinSeconds, leftBoardMaxSeconds));
            _leftBoardDuration = duration;
            // 序列化值可能来自旧 Prefab；前段占比必须为正，避免除零得到 NaN 位置。
            _leftBoardLead = duration * Mathf.Clamp(leftBoardLeadPortion, 0.05f, 0.9f);
            _wheelSpeedFactor = 0f; // 落地拍内盘面与步频从零起转，不再瞬间跳到目标档位。
            _leftBoardLeadVelocity = actor.RB != null ? actor.RB.linearVelocity : Vector2.zero;
            // 限制入场速度最多消耗多少锁点距离，保证前段任意一轴都单调趋近锁点。
            float leadBudget = Mathf.Max(0f, leftBoardLeadRatio);
            for (int i = 0; i < 3; i++)
            {
                float limit = leadBudget * Mathf.Abs(_anchor[i] - _boardStart[i]);
                float travel = Mathf.Abs(_leftBoardLeadVelocity[i]) * _leftBoardLead * 0.5f;
                if (travel > limit && travel > 0.0001f) _leftBoardLeadVelocity[i] *= limit / travel;
            }
        }
        _hasPreviousLeftFoot = false;
        actor.LockToWheel();
    }

    private float ReadDirection()
    {
        float direction = _hud.Direction;
#if UNITY_EDITOR
        if (actor.IsEditor)
        {
            bool left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
            bool right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
            if (left || right) direction = left == right ? 0f : right ? 1f : -1f;
        }
#endif
        return direction;
    }

    private void TickRunner(float dt)
    {
        if (_station == Station.Leaving)
        {
            _dismountTime += dt;
            float t = Mathf.Clamp01(_dismountTime / Mathf.Max(0.01f, dismountSeconds));
            actor.Pin(Vector3.Lerp(_dismountStart, _dismountEnd, t) + Vector3.up * (4f * 0.18f * t * (1f - t)));
            actor.ShowTransferPose();
            if (t >= 1f) LeaveWheel();
            return;
        }
        if (_station == Station.LeftTop)
        {
            float duration = Mathf.Max(0.01f, _leftBoardDuration);
            if (_boardTime < duration)
            {
                float transitTime = Mathf.Min(dt, duration - _boardTime);
                _boardTime = Mathf.Min(duration, _boardTime + transitTime);
                float t = _boardTime / duration;
                float leadPortion = _leftBoardLead / duration;
                Vector3 position;
                if (t < leadPortion)
                {
                    // 前段保留捕获瞬间的跳跃速度并线性衰减到零，让入台衔接自然而不是被瞬间吸住。
                    float u = t / leadPortion;
                    position = _boardStart + _leftBoardLeadVelocity * (_leftBoardLead * u * (1f - 0.5f * u));
                }
                else
                {
                    // 末段从飘行终点平滑收进锁点：两端速度都为零，收尾不再急停。
                    Vector3 leadEnd = _boardStart + _leftBoardLeadVelocity * (_leftBoardLead * 0.5f);
                    float v = (t - leadPortion) / (1f - leadPortion);
                    position = Vector3.Lerp(leadEnd, _anchor, Mathf.SmoothStep(0f, 1f, v));
                }
                float climb = position.y - actor.transform.position.y;
                actor.Pin(position);
                // 动作只看本帧是否仍在上升，不再用固定拱高的解析斜率驱动。
                actor.ShowBoarding(climb);
                _electric.enabled = false;
                if (_boardTime < duration) return;
                // 到位帧仅剩余时间可用于充电，不把整段吸附时间计入跑速或电量。
                dt -= transitTime;
                _leftBoardSettleRemaining = Mathf.Max(0f, leftBoardSettleSeconds);
                _runnerFacing = -1f; // 到位帧朝左慢走；玩家已按住方向时由下方读取立即覆盖。
                if (dt <= 0f)
                {
                    ShowRunnerPose();
                    return;
                }
            }
            actor.Pin(_anchor);
            float direction = ReadDirection();
            AdvanceLeftSettle(dt);
            _runSpeed = direction;
            if (direction != 0f) _runnerFacing = Mathf.Sign(direction);
            ShowRunnerPose();
            actor.TickWheelSteps(dt, direction != 0f);
            float before = _leftCharge;
            // 左台落地拍内盘面还没到目标档位，充电也按同一比例缓起。
            float after = Mathf.Min(1f, before + Mathf.Abs(_runSpeed) * chargePerSecond * _wheelSpeedFactor * dt);
            _leftCharge = after;
            bool charging = _runSpeed != 0f && _wheelSpeedFactor > 0f && !_opening && !_opened;
            _electric.enabled = charging;
            if (charging)
            {
                _electricTime += dt;
                SetElectricFrame(((int)(_electricTime / Mathf.Max(0.01f, electricFrameSeconds)) & 1) != 0);
            }
            // 左台满电由 LateUpdate 在盘面/BoxPink 更新后统一自动跳离。
            return;
        }

        TickRightRunner(dt);
    }

    private void TickRightRunner(float dt)
    {
        // 右盘先短距离就位，途中不跑步、不充电。
        float duration = Mathf.Max(0.01f, boardSeconds);
        if (_boardTime < duration)
        {
            float transitTime = Mathf.Min(dt, duration - _boardTime);
            _boardTime = Mathf.Min(duration, _boardTime + transitTime);
            float t = _boardTime / duration;
            actor.Pin(Vector3.Lerp(_boardStart, _anchor, t) + Vector3.up * (4f * boardingArcHeight * t * (1f - t)));
            _runSpeed = 0f;
            actor.ShowTransferPose();
            _electric.enabled = false;
            if (_boardTime < duration) return;
            dt -= transitTime;
            if (dt <= 0f) return;
        }

        actor.Pin(_anchor);
        if (!_autoRunning) BeginRightAutoRun();
        if (!_autoRunning) return;

        _runSpeed = 1f;
        _runnerFacing = 1f;
        float runTime = Mathf.Min(dt, _autoRunRemaining);
        _rightDegreesThisFrame = -maxWheelDegreesPerSecond * runTime;
        ShowRunnerPose();
        actor.TickWheelSteps(runTime, true);
        float before = _rightCharge;
        _rightCharge = Mathf.Min(1f, before + chargePerSecond * runTime);
        bool charging = runTime > 0f && !_opening && !_opened;
        _electric.enabled = charging;
        if (charging)
        {
            _electricTime += runTime;
            SetElectricFrame(((int)(_electricTime / Mathf.Max(0.01f, electricFrameSeconds)) & 1) != 0);
        }
        _autoRunRemaining = Mathf.Max(0f, _autoRunRemaining - runTime);
        if (_autoRunRemaining <= 0.0001f)
        {
            // 结束时对齐本段目标，避免浮点累计略低于1导致三次跑完仍不开笼。
            _rightCharge = _autoRunTargetCharge;
            _autoRunRemaining = 0f;
            _autoRunning = false;
            _runSpeed = 0f;
            _leaveWheelAfterFrame = true;
        }
    }

    private void BeginRightAutoRun()
    {
        // 满电重入仍跑完整一段；电量保持封顶，不能因补电量为零立即离台。
        float amount = _rightCharge >= 1f ? Mathf.Clamp01(autoChargeAmount) :
            Mathf.Min(Mathf.Clamp01(autoChargeAmount), Mathf.Max(0f, 1f - _rightCharge));
        if (amount <= 0f || chargePerSecond <= 0f)
        {
            _autoRunning = false;
            _leaveWheelAfterFrame = true;
            return;
        }
        _autoRunTargetCharge = Mathf.Min(1f, _rightCharge + amount);
        _autoRunRemaining = amount / chargePerSecond;
        _autoRunning = true;
        _rightChargeIdleSeconds = 0f;
        _electricTime = 0f;
    }

    private void TickRightChargeDecay(float dt)
    {
        if (_rightCharge >= 1f || _autoRunning || _station == Station.RightInside)
        {
            _rightChargeIdleSeconds = 0f;
            return;
        }
        float before = _rightChargeIdleSeconds;
        _rightChargeIdleSeconds += dt;
        float previousDrainTime = Mathf.Max(0f, before - chargeDecayDelaySeconds);
        float currentDrainTime = Mathf.Max(0f, _rightChargeIdleSeconds - chargeDecayDelaySeconds);
        float drainTime = currentDrainTime - previousDrainTime;
        if (drainTime > 0f && chargeDecayPerSecond > 0f)
            _rightCharge = Mathf.Max(0f, _rightCharge - chargeDecayPerSecond * drainTime);
    }

    private void ShowRunnerPose()
    {
        bool sprint = _runSpeed != 0f;
        bool slowWalk = _station == Station.LeftTop && !sprint;
        actor.ShowWheelMotion(_runnerFacing, sprint ? "walk01" : slowWalk ? "walk" : "idle",
            (sprint ? maxGaitCyclesPerSecond * fastRunVisualMultiplier : slowWalk ? leftSlowGaitCyclesPerSecond : 0f) *
            _wheelSpeedFactor);
    }

    /// <summary>左台到位后的短落地拍：盘面与步频从零平滑升到目标档位，只作用于左台。</summary>
    private void AdvanceLeftSettle(float dt)
    {
        float duration = Mathf.Max(0.01f, leftBoardSettleSeconds);
        if (_leftBoardSettleRemaining <= 0f)
        {
            _wheelSpeedFactor = 1f;
            return;
        }
        _leftBoardSettleRemaining = Mathf.Max(0f, _leftBoardSettleRemaining - dt);
        _wheelSpeedFactor = Mathf.SmoothStep(0f, 1f, 1f - _leftBoardSettleRemaining / duration);
    }

    /// <summary>结束当前转盘接管并释放 Actor、输入和电光表现。</summary>
    /// <remarks>仅供本关 Actor 或本关终态清理调用；不会恢复关卡初态，初态由新关卡实例负责建立。</remarks>
    public void LeaveWheel()
    {
        if (_station == Station.None) return;
        _station = Station.None;
        _runSpeed = 0f;
        _autoRunning = false;
        _autoRunRemaining = 0f;
        _leaveWheelAfterFrame = false;
        _rightDegreesThisFrame = 0f;
        _batteryJumpArmed = false;
        _hasPreviousLeftFoot = false;
        _wheelSpeedFactor = 1f;
        _leftBoardSettleRemaining = 0f;
        if (_electric != null) _electric.enabled = false;
        if (actor != null) actor.ReleaseWheel();
        // 左台满电和右台自动跑结束共用清理，避免 HUD 按住状态带回普通移动。
        _hud?.ClearHeld();
        UpdatePink(0f, UnattendedLeftAngularSpeed, false);
    }

    private void SetElectricFrame(bool second)
    {
        Sprite sprite = second ? electricFrameB : electricFrameA;
        Vector2 center = second ? electricCageCenterPixelsB : electricCageCenterPixelsA;
        Vector3 cageCenterFromPivot = new Vector3(center.x - sprite.pivot.x,
            sprite.rect.height - center.y - sprite.pivot.y, 0f) / sprite.pixelsPerUnit;
        Vector3 referenceCenterFromPivot = new Vector3(electricCageCenterPixelsA.x - electricFrameA.pivot.x,
            electricFrameA.rect.height - electricCageCenterPixelsA.y - electricFrameA.pivot.y, 0f) /
            electricFrameA.pixelsPerUnit;
        // 两帧栏杆像素尺寸相同，只补偿中心和 PPU，不再按原牢笼重新缩放。
        float pixelScale = sprite.pixelsPerUnit / electricFrameA.pixelsPerUnit;
        Vector3 offset = referenceCenterFromPivot - cageCenterFromPivot * pixelScale;
        _electric.sprite = sprite;
        // 将人工基准换算到运行时父级，避免把关卡根坐标误作 lz 局部坐标。
        Matrix4x4 placementToRuntime = _runtime.transform.worldToLocalMatrix * electricPlacement.localToWorldMatrix;
        _electric.transform.localScale = Vector3.Scale(placementToRuntime.lossyScale,
            new Vector3(pixelScale, pixelScale, 1f));
        _electric.transform.localRotation = placementToRuntime.rotation;
        _electric.transform.localPosition = placementToRuntime.MultiplyPoint3x4(offset);
    }

    private void UpdatePink(float wheelDegrees, float angularSpeed, bool playerOnTop, float deltaTime = 0f)
    {
        if (_pink == null) return;
        float speed = Mathf.Abs(angularSpeed);
        float followLimit = Mathf.Max(0.01f, pinkMaxFollowDegreesPerSecond);
        // 仅首次超过阈值触发；本局锁存 die，减速、停转、离盘都不恢复 walk/idle。
        if (!_pinkHasTumbled && playerOnTop && speed > followLimit)
        {
            _pinkHasTumbled = true;
            _pinkTumbleTravel = 0f;
            _pinkTumbleBlend = 0f;
            _pinkTumbleStartPosition = _pinkPivot.position;
            _pinkTumbleStartRotation = _pinkPivot.rotation;
        }
        string animation = _pinkHasTumbled ? "die" : speed > 0.01f ? "walk" : "idle";
        var entry = _pink.AnimationState.GetCurrent(0);
        if (entry == null || entry.Animation == null || entry.Animation.Name != animation)
            entry = _pink.AnimationState.SetAnimation(0, animation, !_pinkHasTumbled);
        if (entry != null)
        {
            entry.Loop = !_pinkHasTumbled; // 身体恢复单次 die，眼睛由独立旋转继续表现。
            entry.TimeScale = _pinkHasTumbled ? (speed > 0.01f ? 1f : 0f) : speed > 0.01f
                ? speed / Mathf.Max(0.01f, pinkWalkDegreesPerSecond) : 1f;
        }
        if (_pinkHasTumbled)
        {
            // UpdatePink 的有效 dt 已受本关暂停/焦点/终态门禁控制；与盘速、身体停帧解耦。
            for (int i = 0; i < _pinkEyeAngles.Length; i++)
                _pinkEyeAngles[i] = Mathf.Repeat(_pinkEyeAngles[i] + _pinkEyeSpeeds[i] * deltaTime, 360f);
        }
        if (!_pinkHasTumbled && speed > 0.01f)
        {
            // 盘内脚步对抗盘底运动：正转时向左走，反转时向右走。
            Vector3 scale = _pink.transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (angularSpeed > 0f ? -1f : 1f);
            _pink.transform.localScale = scale;
        }
        Vector3 center = _leftBounds.center;
        float radius = Mathf.Max(0f, _leftBounds.extents.y - 0.7f);
        Vector3 restPosition = center + Vector3.down * radius;
        if (_pinkHasTumbled)
        {
            if (wheelDegrees == 0f) return; // 停盘保留当前翻倒姿态，不插值归正。
            float cycleDegrees = Mathf.Max(1f, pinkTumbleCycleDegrees);
            // 使用有符号角位移；暂停不推进，方向跟随盘面，不在每圈重置姿态。
            _pinkTumbleTravel = Mathf.Repeat(_pinkTumbleTravel + wheelDegrees, cycleDegrees);
            float phase = _pinkTumbleTravel / cycleDegrees;
            float radians = phase * Mathf.PI * 2f;
            // 身体连续翻倒为主，中心仅轻微摆动；不绕盘公转，也不固定点纯自旋。
            Vector3 tumblePosition = restPosition + new Vector3(
                Mathf.Sin(radians) * Mathf.Min(pinkTumbleSway, radius * 0.15f),
                (1f - Mathf.Cos(radians * 2f)) * 0.5f * Mathf.Min(pinkTumbleLift, radius * 0.05f), 0f);
            Quaternion tumbleRotation = Quaternion.Euler(0f, 0f, phase * 360f + 12f * Mathf.Sin(radians));
            _pinkTumbleBlend = Mathf.Min(1f, _pinkTumbleBlend + deltaTime / Mathf.Max(0.01f, pinkTumbleBlendSeconds));
            float blend = Mathf.SmoothStep(0f, 1f, _pinkTumbleBlend);
            _pinkPivot.position = Vector3.Lerp(_pinkTumbleStartPosition, tumblePosition, blend);
            _pinkPivot.rotation = Quaternion.Slerp(_pinkTumbleStartRotation, tumbleRotation, blend);
        }
        else
        {
            _pinkPivot.position = restPosition;
            _pinkPivot.rotation = Quaternion.identity;
        }
    }

    private static void RefreshBattery(Transform fill, Transform background, float charge)
    {
        Vector3 scale = background.localScale;
        Vector3 position = background.localPosition;
        float height = scale.y * Mathf.Clamp01(charge);
        position.y += (height - scale.y) * 0.5f;
        scale.y = height;
        fill.localPosition = position;
        fill.localScale = scale;
    }

    #region 开场电量、自动小跳与开笼爆炸
    private void TickInitialCharge(float dt)
    {
        float amount = Mathf.Min(_initialChargeRemaining,
            Mathf.Clamp01(initialLeftCharge) * dt / Mathf.Max(0.01f, initialChargeSeconds));
        _initialChargeRemaining -= amount;
        _leftCharge = Mathf.Min(1f, _leftCharge + amount);
    }

    private void BeginDismount()
    {
        _dismountStart = actor.transform.position;
        _dismountEnd = _station == Station.LeftTop ? _leftTakeoffPosition : _boardStart;
        _dismountTime = 0f;
        _station = Station.Leaving;
        _runSpeed = 0f;
        _autoRunning = _leaveWheelAfterFrame = false;
        _electric.enabled = false;
        _hud?.ClearHeld();
        actor.ShowTransferPose();
    }

    private void BeginOpenCage()
    {
        if (_opening || _opened) return;
        _opening = true;
        _openTime = 0f;
        _electric.enabled = false;
        _blastCenter = _cageRenderer.bounds.center;
        _blastFrontOffset = cageFront.transform.position - _blastCenter;
        _blastBackOffset = cageBack.position - _blastCenter;
        _blastFrontRotation = cageFront.transform.rotation;
        _blastBackRotation = cageBack.rotation;
        _explosion = Instantiate(cageExplosionPrefab, _blastCenter, cageExplosionPrefab.transform.rotation, transform);
        _explosion.name = "Level149CageExplosion";
        _explosion.transform.localScale = Vector3.one * 0.7f;
        foreach (ParticleSystem particle in _explosion.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particle.main;
            main.loop = false;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.None;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            _explosionLife = Mathf.Max(_explosionLife, main.duration + main.startDelay.constantMax + main.startLifetime.constantMax);
        }
        foreach (var renderer in _explosion.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            renderer.sortingLayerName = "SceneLayer";
            renderer.sortingOrder = 250;
        }
        // 只由有效玩法时间手动模拟，暂停不会偷偷播完，终态立即销毁本关实例。
        _explosion.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _explosion.Simulate(0f, true, true, false);
        _explosionAge = 0f;
        AudioManager.PlaySound("flyRocketExplosion");
    }

    private void TickCageBlast(float dt)
    {
        _openTime += dt;
        float t = Mathf.Min(_openTime, cageBlastSeconds);
        Vector3 center = _blastCenter + new Vector3(6f * t, 22f * t - 7f * t * t, 0f);
        Quaternion turn = Quaternion.Euler(0f, 0f, -330f * t);
        cageFront.transform.SetPositionAndRotation(center + turn * _blastFrontOffset, turn * _blastFrontRotation);
        cageBack.SetPositionAndRotation(center + turn * _blastBackOffset, turn * _blastBackRotation);
        if (_openTime < cageBlastSeconds) return;
        cageFront.SetActive(false);
        cageBack.gameObject.SetActive(false);
        keyCollider.enabled = true;
        _opening = false;
        _opened = true;
    }

    private void TickExplosion(float dt)
    {
        if (_explosion == null) return;
        _explosion.Simulate(dt, true, false, false);
        _explosionAge += dt;
        if (_explosionAge >= _explosionLife + 0.2f) Destroy(_explosion.gameObject);
    }
    #endregion

    private void Pause(bool paused)
    {
        _paused = paused;
        _hud?.ClearHeld();
        if (actor != null && actor.Initialized) actor.SetMoveInput(0f);
    }

    private void OnApplicationFocus(bool focus) { _focused = focus; _hud?.ClearHeld(); }
    private void OnApplicationPause(bool paused) { _applicationPaused = paused; _hud?.ClearHeld(); }

    /// <summary>结束当前关卡实例并释放本关接管的运行时资源。</summary>
    /// <remarks>Reset、Replay、失败和通关都只做旧实例 teardown；电量、笼子和钥匙初态由新 Prefab 实例在 Awake 中建立。</remarks>
    private void StopAttempt()
    {
        if (_terminal) return;
        _terminal = true;
        if (_explosion != null) Destroy(_explosion.gameObject);
        LeaveWheel();
        if (actor != null) actor.SetPaused(false);
        if (_pink != null) _pink.timeScale = 0f;
        _hud?.Dispose();
        _hud = null;
        if (_electric != null) _electric.enabled = false;
    }

    private void OnDisable()
    {
        EventsUtils.ResetEvents(ref _events);
        // 旧关卡实例只解除接管并销毁运行时对象，不在禁用阶段回写电量、门或钥匙初态。
        StopAttempt();
        if (_pink != null) _pink.UpdateLocal -= ApplyPinkEyeSpin;
        if (actor != null) actor.Gameplay = null;
        if (_ready && leftWheel != null) leftWheel.transform.localRotation = _leftRotation;
        if (_ready && rightWheel != null) rightWheel.transform.localRotation = _rightRotation;
        if (_runtime != null) Destroy(_runtime);
    }
}
