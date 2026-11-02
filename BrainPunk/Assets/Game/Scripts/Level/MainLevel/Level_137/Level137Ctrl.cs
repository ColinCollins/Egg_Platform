using System.Collections.Generic;
using Bear.EventSystem;
using DG.Tweening;
using Game.Events;
using Game.ItemEvent;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Sound = MelenitasDev.SoundsGood.Sound;

/// <summary>
/// 137 车载钩取：所有地下命中由本关的扫掠白名单负责，与 Physics2D/地面分离。
/// 玩家仍是基础关卡绑定的真实 Actor；星星与入门/失败使用既有标准业务链。
/// </summary>
[DefaultExecutionOrder(100)]
public sealed class Level137Ctrl : MonoBehaviour
{
    private const float MountBindingRevealProgress = 0.8f;
    // 对齐Level018钥匙弹出的零端点切线时间曲线，用于玩家挂钩和狗上车。
    private static readonly AnimationCurve ArcTimeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    // 货物抛起按实际时间走抛物线：快速起跳、顶部减速、下落加速，不在落地前缓停。
    private static readonly AnimationCurve CargoTossTimeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    private enum Phase { Walking, Boarding, Swinging, Mounting, Extending, Retracting, Tossing, DogExit, DogToCar, Fighting, DefeatFlying, Finished }
    private enum ExitMotion { None, Swinging, Extending, Retracting }
    private enum Kind { Prop, Board, Lock, Key, Star, Door, Dog }
    private enum ItemState { Available, Carried, Stored, FlyingKey, Unlocking, Absorbing, Consumed }

    private sealed class Item
    {
        // 物件的运行时根节点；货物挂载和吸入时由总控移动该根节点。
        public Transform Root;
        // 可直接读取视觉包围盒的 SpriteRenderer；Spine 物件使用 FixedFrame。
        public SpriteRenderer Sprite;
        // 普通 Collider 的静态几何来源；不参与本关 Physics2D 命中。
        public BoxCollider2D Box;
        // 标准星星实例；只有 Kind.Star 时使用共享收集入口。
        public Postcard_Star Star;
        // 物件类型，用于区分普通物件、牌、门锁、钥匙、星星、门和狗。
        public Kind Kind;
        // 本关扫掠和生命周期使用的物件状态。
        public ItemState State;
        // 告示牌在 boards 数组中的顺序索引；非告示牌保持 -1。
        public int BoardIndex = -1;
        // 记录命中时的来源侧；左侧来源投放到车辆右侧。
        public bool DropOnRight;
        // Spine 物件捕获的固定视觉坐标参考。
        public Transform FixedFrame;
        // FixedFrame 对应的局部包围盒快照。
        public Bounds FixedLocalBounds;
        // 返回当前物件进行几何计算时使用的坐标参考。
        public Transform Frame => FixedFrame != null ? FixedFrame : Sprite != null ? Sprite.transform : Box.transform;
        // 返回当前物件用于扫掠和吸入的局部包围盒。
        public Bounds LocalBounds => FixedFrame != null ? FixedLocalBounds : Sprite != null ?
            Level137Geometry.SpriteLocalBounds(Sprite) : new Bounds(Box.offset, Box.size);
        // 返回当前物件的世界轴对齐包围盒。
        public Bounds Bounds => Level137Geometry.TransformBounds(Frame, LocalBounds);
    }

    private sealed class Absorption
    {
        // 正在被洞吸入的物件记录。
        public Item Item;
        // 吸入开始时物件中心的世界位置。
        public Vector3 Center;
        // 吸入开始时物件根节点的局部缩放。
        public Vector3 Scale;
        // 吸入开始时物件根节点的世界旋转。
        public Quaternion Rotation;
        // 驱动该物件位移、旋转和缩放的本关进度对象。
        public EasedMotion Motion;
    }

    // Tween只由本关活动帧Goto推进，不加入自动播放，保留原状态机的暂停/结束门禁与更新顺序。
    private sealed class EasedMotion
    {
        private readonly Tween tween;
        private readonly float duration;
        private float elapsed;
        // 当前已经经过时间曲线处理的进度，供位置和表现计算读取。
        public float Progress { get; private set; }
        // 未经过时间曲线处理的归一化时间进度。
        public float TimeProgress => Mathf.Clamp01(elapsed / duration);
        // 表示该进度对象是否已经到达配置时长。
        public bool Completed => elapsed >= duration;

        // 创建暂停状态的非自动播放 Tween；推进由本关活动帧显式调用 Advance。
        public EasedMotion(float seconds, AnimationCurve timeCurve = null)
        {
            duration = Mathf.Max(0.1f, seconds);
            tween = DOTween.To(() => Progress, value => Progress = value, 1f, duration)
                .SetAutoKill(false).Pause();
            if (timeCurve != null) tween.SetEase(timeCurve);
            else tween.SetEase(Ease.OutQuad);
        }

        // 推进有限时间并把结果写入 Tween 进度；不会修改 Unity 全局时间。
        public float Advance(float dt)
        {
            elapsed = Mathf.Min(duration, elapsed + dt);
            tween.Goto(elapsed, false);
            return Progress;
        }

        // 结束并释放当前进度 Tween，供完成、退场和销毁路径调用。
        public void Kill() => tween.Kill();
    }

    [Header("Scene bindings — preserve the authored layout")]
    // 共享关卡主控；只用于关闭普通控制和转发标准成功/失败入口，不在本关复制其结算逻辑。
    [SerializeField] private BaseLevelCtrl level;
    // Level 137 的唯一真实玩家；上车前仍由共享 Actor 链更新。
    [SerializeField] private Level137ActorCtrl player;
    // 用于屏幕坐标转换和 16:9 参考视野边界计算的世界相机。
    [SerializeField] private Camera worldCamera;
    // authored 车辆碰撞框；用于靠近自动上车、来源侧判定和车辆中心镜像。
    [SerializeField] private BoxCollider2D carCollider;
    // 车辆中央的玩家锁定点；自动上车弧线在此落定。
    [SerializeField] private Transform carSeatPoint;
    // authored 地面碰撞框；货物落地高度以其顶面为基准。
    [SerializeField] private BoxCollider2D groundCollider;
    // 钩绳和玩家挂载共同使用的旋转根节点。
    [SerializeField] private Transform swingRoot;
    // 主绳 SpriteRenderer；出钩、回收和狗入区期间由本关调整长度与姿态。
    [SerializeField] private SpriteRenderer rope;
    // 钩爪 SpriteRenderer；钩尖坐标由 hookTipLocal 解释。
    [SerializeField] private SpriteRenderer hook;
    // 玩家挂载姿态参考点；只读取其初始位置和旋转，不作为第二个运行时玩家。
    [SerializeField] private Transform hangingPose;
    // 玩家身上的捆绑绳模板；挂载后克隆到真实玩家下方并按阶段显隐。
    [SerializeField] private SpriteRenderer hangingRope;
    // 右侧货物落点；左侧落点通过车辆中心镜像得到。
    [Tooltip("右侧投放水平锚点，左侧按Car碰撞框中心镜像；落地高度仍按地面和货物包围盒计算，不使用此点的Y。")]
    [SerializeField] private Transform cargoPoint;
    // authored 出口门根节点；既用于视觉，也作为门物件的生命周期根。
    [SerializeField] private Transform door;
    // 正式门入口区域；载人入门和钩狗使用其中心、旋转和尺寸比例。
    [SerializeField] private BoxCollider2D doorTrigger;
    // 关闭/打开门的 Spine 控制器；总控只驱动本实例的门动画。
    [SerializeField] private ActorSpineCtrl doorSpine;
    // 门锁根节点；普通钩取和共享钥匙两条路径都以此实例为目标。
    [SerializeField] private Transform doorLock;
    // 门锁视觉 Spine 控制器；用于捕获关闭状态包围盒并保持初始视觉。
    [SerializeField] private ActorSpineCtrl lockSpine;
    // 场景中的有效钥匙 Sprite；是否有效由门解锁状态决定。
    [SerializeField] private SpriteRenderer key;
    // 共享钥匙事件执行器；只在钩取确认或本关回调允许时启动。
    [SerializeField] private TriggerExecutor keyExecutor;
    // 共享钥匙飞行监听器；本关仅适配其 Tween 归属和到达门锁的位置。
    [SerializeField] private ItemJumpToListener keyJump;
    // authored 洞口 Sprite；用于露洞判定和吸入中心参考。
    [SerializeField] private SpriteRenderer hole;
    // 可被普通钩取的普通物件列表，顺序只影响登记，不代表命中优先级。
    [SerializeField] private SpriteRenderer[] ordinaryProps;
    // 三层告示牌，必须按数组顺序逐层命中。
    [SerializeField] private Transform[] boards;
    // 与星位一一对应的银币替代物；位置在初始化时跟随对应星星。
    [SerializeField] private SpriteRenderer[] silverCoins;
    // 标准场景星数组；本关要求恰好两个，并由共享星星链负责存档与奖励。
    [SerializeField] private Postcard_Star[] stars;

    [Header("Local presentations")]
    // 狗门内显现和挂钩回收模板；运行时克隆后由本关总控管理生命周期。
    [SerializeField] private Level137DogExit dogPrefab;
    // 狗夺车打斗表现模板；只承载本关局部 Animator 和 Sprite 表现。
    [SerializeField] private GameObject fightPrefab;
    [Tooltip("旧旋流仅保留停止/清理接线，不再启动或推进。")]
    // 历史旋流组件，仅保留停止和卸载清理，不参与当前洞口旋涡表现。
    [SerializeField] private Level137Vortex vortex;
    // 出钩阶段替换共享 Jump 图标的本关按钮图片。
    [SerializeField] private Sprite hookButtonIcon;

    [Header("Confirmed swing / reference frame")]
    // 钩爪相对水平端点的摆角；由 TickSwing 在本关活动帧推进。
    [SerializeField, Range(1f, 89f)] private float swingAngle = 75f;
    // 左右端点之间的单程摆动时间，不包含端点停留。
    [SerializeField, Min(0.1f)] private float sideToSideSeconds = 2f;
    // 到达任一摆动端点后的停留时间。
    [SerializeField, Min(0f)] private float endpointPauseSeconds = 0.1f;
    [Tooltip("钩头素材局部坐标中的钩尖；当前 58×109、100 PPU 素材的左侧尖端。")]
    [SerializeField] private Vector2 hookTipLocal = new Vector2(-0.20f, -0.27f);
    // 钩尖命中目标后沿原方向继续深入的钩头长度比例。
    [SerializeField, Range(0f, 1f)] private float penetrationLengthRatio = 0.25f;
    // 出钩和退场运动距参考视野边缘保留的安全边距。
    [SerializeField, Min(0f)] private float viewportMargin = 0.04f;

    [Header("Adjustable initial motion")]
    // 玩家身体与车身横向间隙小于此值时自动上车；只在正常可操作的步行阶段检查。
    [SerializeField, Min(0f)] private float boardingDistance = 0.5f;
    // 自动上车允许脚底高出车顶的距离，避免角色远在空中或地下时被吸到车上。
    [SerializeField, Min(0f)] private float boardingHeightAllowance = 0.75f;
    // 从靠近车身的位置跳到座位的时长，使用本关可暂停的弧线进度。
    [SerializeField, Min(0.1f)] private float boardingSeconds = 0.3f;
    // 自动上车在起终点连线之上抬高的弧度；不改变座位与车的摆放。
    [SerializeField, Min(0f)] private float boardingArcHeight = 0.6f;
    // 普通钩取和载人出钩的直线伸出速度，单位为世界单位/秒。
    [SerializeField, Min(0.1f)] private float extendSpeed = 6f;
    // 普通钩取、载人和狗回收的直线回收速度，单位为世界单位/秒。
    [SerializeField, Min(0.1f)] private float retractSpeed = 9f;
    // 点击车上玩家后移动到钩头的挂载时长。
    [SerializeField, Min(0.1f)] private float mountSeconds = 0.3f;
    // 载人挂载小弧线的世界高度；不用于狗失败击飞。
    [SerializeField, Min(0f)] private float mountArcHeight = 0.65f;
    [Tooltip("普通货物抛落和狗上车共用时长。")]
    [SerializeField, Min(0.1f)] private float tossSeconds = 0.7f;
    [Tooltip("普通货物抛落和狗上车共用弧高。")]
    [SerializeField, Min(0f)] private float tossArcHeight = 2f;
    // 同一来源侧已存货物在水平方向的向外间距，不改变其落地高度。
    [SerializeField, Min(0f)] private float cargoHorizontalSpacing = 0.22f;
    // 洞吸入物件的持续时间；彩带显隐跟随该吸入列表而非独立计时。
    [SerializeField, Min(0.1f)] private float suctionSeconds = 1.6f;
    // 洞吸入开始时播放的一次性共享音效名称。
    [SerializeField] private string suctionSfx = "enterDoor";
    [Header("Shared door entry / dog reveal")]
    [UnityEngine.Serialization.FormerlySerializedAs("dogEntrySizeRatio")]
    // 门入口区域相对正式 doorTrigger 的宽高缩放比例；不修改 Collider 本体。
    [SerializeField, Range(0.1f, 1f)] private float doorEntrySizeRatio = 0.5f;
    // 钩狗进入门区域后隐藏钩爪并等待回收的停留时间；载人入门不使用该等待。
    [SerializeField, Min(0f)] private float dogEntryHoldSeconds = 0.3f;
    // 狗上车后的打斗表现时长；结束后进入真实物理击飞。
    [SerializeField, Min(0.1f)] private float fightSeconds = 1.2f;
    // 狗夺车后传给共享 Knockback 的击飞强度，不直接决定失败时刻。
    [SerializeField, Min(0.1f)] private float defeatKnockForce = 50f;

    private readonly List<Item> items = new List<Item>();
    private readonly List<Absorption> absorptions = new List<Absorption>();
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    private readonly Dictionary<Spine.AnimationState, float> pausedSpines = new Dictionary<Spine.AnimationState, float>();
    private readonly Dictionary<Animator, float> pausedAnimators = new Dictionary<Animator, float>();
    private readonly Dictionary<Behaviour, bool> pausedSharedBehaviours = new Dictionary<Behaviour, bool>();
    private readonly List<Tween> pausedSharedTweens = new List<Tween>();
    private EventSubscriber subscriber;
    private Phase phase;
    private bool initialized;
    private bool paused;
    private bool inputBlocked;
    private bool stopped;
    private bool exiting;
    private ExitMotion exitMotion;
    private bool exitCarryPlayer;
    private bool exitResumeSwing;
    private float exitExtensionLimit;
    private bool mounted;
    private bool doorUnlocked;
    private bool doorGone;
    private bool holeStarted;
    private int nextBoard;
    private int storedCount;
    private int storedLeftCount;
    private int storedRightCount;
    private float angle;
    private float swingTarget;
    private float endpointRemaining;
    private float extension;
    private float phaseTime;
    private EasedMotion motion;
    private float initialRopeLength;
    private Vector3 initialRopePosition;
    private Quaternion initialRopeRotation;
    private Vector3 initialHookPosition;
    private bool dogPullStarted;
    private float dogEntryHoldRemaining;
    private Vector3 hangingLocalPosition;
    private Quaternion hangingLocalRotation;
    private Quaternion mountFromRotation;
    private Vector3 seatPosition;
    private Vector3 motionFrom;
    private Vector3 motionTo;
    private Quaternion lockTossFromRotation;
    private Vector3 carryOffset;
    private Item carried;
    private Item pendingHit;
    private float penetrationRemaining;
    private SortingGroup playerSorting;
    private bool addedPlayerSorting;
    private int originalPlayerLayer;
    private int originalPlayerOrder;
    private Item lockItem;
    private Item doorItem;
    private Item lastBoard;
    private Item keyItem;
    private bool keyUnlockStarted;
    private GameObject playerBinding;
    private Level137DogExit dog;
    private GameObject fight;
    private Animator fightAnimator;
    private Sound suctionSound;
    // 只由本关持有的伸绳循环声；回收、退场和销毁时停止，暂停时随运动暂停。
    private Sound extensionSound;
    private Level137HoleVortex holePresentation;
    private Sound fightSound;
    private GamePlayPanel boundPanel;
    private Image jumpIcon;
    private Sprite originalIcon;
    private Quaternion originalIconRotation;
    private Vector3 originalIconScale;
    private bool originalPreserveAspect;
    private bool originalIconRaycast;
    private Transform jumpButton;
    private GameObject leftMoveButton;
    private GameObject rightMoveButton;
    private GameObject panelClickBlock;
    private bool originalLeftActive;
    private bool originalRightActive;
    private bool hookControlsApplied;
    private bool hookRevealPending;
    private bool jumpScaleCaptured;
    private Vector3 originalJumpScale;
    private SequentialScaleAnim hookButtonReveal;
    private CustomButton jumpInput;
    private bool jumpInputWasEnabled;
    private bool jumpInteractionSuspended;
    private ClickScaleAnim[] jumpPressAnimations;
    private bool[] jumpPressWasEnabled;

    private bool GameplayInputBlocked => inputBlocked || boundPanel == null ||
        !boundPanel.isActiveAndEnabled || jumpIcon == null ||
        (panelClickBlock != null && panelClickBlock.activeInHierarchy);

    private void Awake()
    {
        vortex?.Stop();
        if (hole != null) holePresentation = hole.GetComponent<Level137HoleVortex>();
        // 这个 Actor 只是人工挂载姿态示意，绝不作为第二个运行角色或收集物。
        if (hangingPose != null) hangingPose.gameObject.SetActive(false);
        if (doorTrigger != null) doorTrigger.enabled = false;
        var handler = door != null ? door.GetComponent<OnTrigger2DHandle>() : null;
        if (handler != null) handler.enabled = false;
        // Awake 即订阅，避免加载方在本关 Start 之前派发出门/HUD 阻挡事件而漏接。
        EventsUtils.ResetEvents(ref subscriber);
        subscriber.Subscribe<GamePauseEvent>(OnPause);
        subscriber.Subscribe<GameResumeEvent>(OnResume);
        subscriber.Subscribe<GamePlayPanelSwitchBlockEvent>(OnInputBlock);
        subscriber.Subscribe<GameResetEvent>(OnReset);
        subscriber.Subscribe<GameReplayEvent>(OnReplay);
    }

    private bool Initialize()
    {
        if (player == null || !player.Initialized) return false;
        if (level == null || worldCamera == null || carCollider == null || carSeatPoint == null || groundCollider == null ||
            swingRoot == null || rope == null || hook == null || hangingPose == null || hangingRope == null ||
            cargoPoint == null || door == null || doorTrigger == null || doorSpine == null || doorLock == null ||
            lockSpine == null || key == null || keyExecutor == null || keyJump == null ||
            hole == null || dogPrefab == null || fightPrefab == null ||
            hookButtonIcon == null || ordinaryProps == null || boards == null || boards.Length != 3 || stars == null || stars.Length != 2 ||
            silverCoins == null || silverCoins.Length != 2 || System.Array.Exists(boards, item => item == null) ||
            System.Array.Exists(stars, item => item == null) || System.Array.Exists(silverCoins, item => item == null) ||
            System.Array.Exists(ordinaryProps, item => item == null) ||
            doorLock.GetComponent<BaseItemExecutor>() == null || doorLock.GetComponent<EventHandleListener>() == null)
        {
            Debug.LogError("[Level137] Missing authored gameplay bindings; level left unstarted.", this);
            stopped = true;
            return false;
        }
        initialHookPosition = hook.transform.localPosition;
        initialRopeLength = rope.size.y;
        initialRopePosition = rope.transform.localPosition;
        initialRopeRotation = rope.transform.localRotation;
        hangingLocalPosition = swingRoot.InverseTransformPoint(hangingPose.position);
        hangingLocalRotation = Quaternion.Inverse(swingRoot.rotation) * hangingPose.rotation;
        playerBinding = Instantiate(hangingRope.gameObject, player.transform, false);
        playerBinding.name = "Level137PlayerBinding";
        playerBinding.SetActive(false);
        swingTarget = -swingAngle;

        foreach (SpriteRenderer prop in ordinaryProps) AddSprite(prop, Kind.Prop);
        for (int i = 0; i < boards.Length; i++)
        {
            Item board = AddSprite(boards[i].GetComponentInChildren<SpriteRenderer>(true), Kind.Board, boards[i]);
            board.BoardIndex = i;
        }
        keyItem = AddSprite(key, Kind.Key, keyExecutor.transform);
        lockItem = new Item { Root = doorLock, Box = doorLock.GetComponent<BoxCollider2D>(), Kind = Kind.Lock };
        doorItem = new Item { Root = door, Box = doorTrigger, Kind = Kind.Door };
        items.Add(lockItem);
        items.Add(doorItem);
        // 在所有 Awake 完成后钉住带锁初态，不依赖源 Spine 与 ActorSpineCtrl 的 Awake 先后。
        doorSpine.PlayAnimation("door_idle", false, false);
        lockSpine.PlayAnimation("lock_idle", false, false);
        if (!CaptureShape(doorItem, doorSpine) || !CaptureShape(lockItem, lockSpine))
        {
            Debug.LogError("[Level137] Missing closed door/lock visual bounds; level left unstarted.", this);
            stopped = true;
            return false;
        }
        // 保留标准 DoorLock 视觉/挂点，只关闭本实例的自动触发和销毁执行器。
        foreach (MonoBehaviour component in doorLock.GetComponents<MonoBehaviour>())
            if (!(component is ActorSpineCtrl) && !(component is Spine.Unity.SkeletonAnimation)) component.enabled = false;
        foreach (Collider2D collider in doorLock.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;

        for (int i = 0; i < stars.Length; i++)
        {
            // 星位是位置真源；每次重建先原位对齐替代币，再按永久收集记录选择显示对象。
            // 使用世界坐标，避免星星和银币父层级不同导致偏移；不改银币的缩放和旋转。
            silverCoins[i].transform.position = stars[i].transform.position;
            bool available = stars[i].RefreshSavedCollectionState();
            silverCoins[i].gameObject.SetActive(!available);
            Item coin = AddSprite(silverCoins[i], Kind.Prop);
            if (available) coin.State = ItemState.Consumed; // 本局永远不补币，只在下次初始化读存档。
            Item star = AddSprite(stars[i].GetComponentInChildren<SpriteRenderer>(true), Kind.Star, stars[i].transform);
            star.Star = stars[i];
            if (!available) star.State = ItemState.Consumed;
            foreach (Collider2D collider in stars[i].GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
        }
        dog = Instantiate(dogPrefab, transform);
        dog.gameObject.SetActive(false);
        fight = Instantiate(fightPrefab, transform);
        fight.SetActive(false);
        fightAnimator = fight.GetComponent<Animator>();
        initialized = true;
        return true;
    }

    private Item AddSprite(SpriteRenderer sprite, Kind kind, Transform root = null)
    {
        var item = new Item { Root = root != null ? root : sprite.transform, Sprite = sprite, Kind = kind };
        items.Add(item);
        return item;
    }

    private void Update()
    {
        if (stopped) return;
        if (!initialized && !Initialize()) return;
        // 退场仅保留钩爪视觉运动；不依赖已交给成功动画或已销毁的Actor。
        if (exiting)
        {
            if (!paused) TickExitMotion(Time.deltaTime);
            return;
        }
        BindPanel();
        TickHookButtonReveal();
        if (paused) return;
        if (phase == Phase.Finished || player == null)
        {
            StopExtensionSound();
            holePresentation?.StopAbsorptionRibbon();
            return;
        }
        if (player.IsDied)
        {
            phase = Phase.Finished;
            StopExtensionSound();
            holePresentation?.StopAbsorptionRibbon();
            return;
        }
        float dt = Time.deltaTime;
        TickSharedUnlock();
        TickAbsorptions(dt);
        switch (phase)
        {
            case Phase.Walking: TickBoarding(); break;
            case Phase.Boarding: TickBoardingJump(dt); break;
            case Phase.Swinging:
                TickSwing(dt);
                CheckMountInput();
                break;
            case Phase.Mounting: TickMount(dt); break;
            case Phase.Extending:
                TickExtension(dt);
                if (phase != Phase.Extending) StopExtensionSound();
                break;
            case Phase.Retracting: TickRetraction(dt); break;
            case Phase.Tossing: TickToss(dt); break;
            case Phase.DogExit: TickDogExit(dt); break;
            case Phase.DogToCar: TickDogToCar(dt); break;
            case Phase.Fighting: TickFight(dt); break;
            case Phase.DefeatFlying: TickDefeatFlying(dt); break;
        }
        CheckLockDeparture();
        CheckHoleExposure();
#if UNITY_EDITOR
        if (phase == Phase.Swinging && !GameplayInputBlocked && Input.GetKeyDown(KeyCode.Space)) RequestShot();
#endif
    }

    private void LateUpdate()
    {
        if (!initialized || stopped) return;
        if (paused) FreezeAnimations();
        if (phase == Phase.Fighting) SetPlayerAlpha(0f); // 同步可能晚到的换装附件。
    }

    private void TickBoarding()
    {
        if (!level.ActorCtrlable || GameplayInputBlocked) return;
        Bounds body = player.BodyBounds;
        Bounds car = carCollider.bounds;
        float gap = Mathf.Max(car.min.x - body.max.x, body.min.x - car.max.x);
        if (gap > boardingDistance || body.min.y < groundCollider.bounds.max.y - 0.25f ||
            body.min.y > car.max.y + boardingHeightAllowance) return;
        level.SetCtrlable(false);
        player.TakeScriptedControl();
        seatPosition = carSeatPoint.position;
        motionFrom = player.transform.position;
        motionTo = seatPosition;
        BeginMotion(boardingSeconds, ArcTimeCurve);
        phase = Phase.Boarding;
        player.SpineCtrl.PlayAnimation("jump_up", false);
    }

    // 上车期间不接受出钩或挂钩点击；完成后才切换原钩取HUD与摆动阶段。
    private void TickBoardingJump(float dt)
    {
        float previousTime = motion.TimeProgress;
        float t = motion.Advance(dt);
        player.SetPosition(Level137Geometry.ArcAtProgress(motionFrom, motionTo, t, boardingArcHeight));
        if (previousTime < 0.5f && motion.TimeProgress >= 0.5f)
            player.SpineCtrl.PlayAnimation("jump_down", false);
        if (!motion.Completed) return;
        EndMotion();
        player.SetPosition(seatPosition);
        player.transform.rotation = carSeatPoint.rotation;
        SetPlayerSorting(true);
        player.SpineCtrl.PlayAnimation("idle", true);
        phase = Phase.Swinging;
        AudioManager.PlaySound("level64Block");
        ApplyHookControls();
    }

    private void TickSwing(float dt)
    {
        float speed = 2f * swingAngle / Mathf.Max(0.1f, sideToSideSeconds);
        // 保留抵达端点那一帧的剩余时间，使单程与端点停留分别遵守配置。
        while (dt > 0f && speed > 0f)
        {
            if (endpointRemaining > 0f)
            {
                float wait = Mathf.Min(endpointRemaining, dt);
                endpointRemaining -= wait;
                dt -= wait;
                if (dt <= 0f) break;
            }
            float travel = Mathf.Min(dt, Mathf.Abs(swingTarget - angle) / speed);
            angle = Mathf.MoveTowards(angle, swingTarget, speed * travel);
            dt -= travel;
            if (Mathf.Approximately(angle, swingTarget))
            {
                endpointRemaining = endpointPauseSeconds;
                swingTarget = -swingTarget;
            }
        }
        UpdateRig();
    }

    private void UpdateRig()
    {
        swingRoot.localRotation = Quaternion.Euler(0f, 0f, angle);
        hook.transform.localPosition = initialHookPosition + Vector3.down * extension;
        Vector2 size = rope.size;
        if (phase == Phase.DogExit && !dogPullStarted)
        {
            // 钩爪刚进入旋涡时只留下绳子；绳尾补到触点，不留下隐藏钩爪占据的空隙。
            Vector3 start = initialRopePosition + initialRopeRotation * (Vector3.up * initialRopeLength * 0.5f);
            Vector3 end = rope.transform.parent.InverseTransformPoint(HookTip);
            Vector3 span = end - start;
            size.y = span.magnitude;
            rope.transform.localPosition = (start + end) * 0.5f;
            rope.transform.localRotation = span.sqrMagnitude > 0.000001f
                ? Quaternion.FromToRotation(Vector3.down, span) : initialRopeRotation;
        }
        else
        {
            size.y = initialRopeLength + extension;
            Vector3 position = initialRopePosition;
            position.y = -size.y * 0.5f;
            rope.transform.localPosition = position;
            rope.transform.localRotation = initialRopeRotation;
        }
        rope.size = size;
        if (mounted && phase != Phase.Mounting && phase != Phase.Finished)
        {
            player.SetPosition(swingRoot.TransformPoint(hangingLocalPosition + Vector3.down * extension));
            player.transform.rotation = swingRoot.rotation * hangingLocalRotation;
        }
        if (dog != null && dog.IsAttached) dog.FollowHook();
        if (carried != null && carried.Kind != Kind.Dog)
            carried.Root.position = hook.transform.position + carryOffset;
    }

    private void CheckMountInput()
    {
        if (mounted || GameplayInputBlocked) return;
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began && TryMountAt(touch.position)) break;
            }
        }
        else if (Input.GetMouseButtonDown(0)) TryMountAt(Input.mousePosition);
    }

    private bool TryMountAt(Vector2 screen)
    {
        if (EventSystem.current != null)
        {
            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, uiHits);
            foreach (RaycastResult hit in uiHits) if (hit.module is GraphicRaycaster) return false;
        }
        Vector3 position = worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y,
            player.transform.position.z - worldCamera.transform.position.z));
        Bounds bounds = player.BodyBounds;
        if (position.x < bounds.min.x || position.x > bounds.max.x || position.y < bounds.min.y || position.y > bounds.max.y)
            return false;
        mounted = true; // 唯一方向：之后再点击玩家不会返回车上。
        SetPlayerSorting(false);
        motionFrom = player.transform.position;
        motionTo = swingRoot.TransformPoint(hangingLocalPosition);
        mountFromRotation = player.transform.rotation;
        BeginMotion(mountSeconds, ArcTimeCurve);
        phase = Phase.Mounting;
        playerBinding.SetActive(false);
        return true;
    }

    private void TickMount(float dt)
    {
        float t = motion.Advance(dt);
        // Mounting 只用于点击玩家后的挂钩弧线，车中央锁定不经过此阶段。
        player.SetPosition(Level137Geometry.ArcAtProgress(motionFrom, motionTo, t, mountArcHeight));
        Quaternion target = swingRoot.rotation * hangingLocalRotation;
        // 位移与旋转只使用同一份DOTween进度，不再叠加SmoothStep。
        player.transform.rotation = Quaternion.Slerp(mountFromRotation, target, t);
        // 玩家先靠近钩头，再显示身上的捆绑绳；跨帧直接到终点也会显示。
        if (motion.TimeProgress >= MountBindingRevealProgress && !playerBinding.activeSelf) playerBinding.SetActive(true);
        if (!motion.Completed) return;
        EndMotion();
        hook.enabled = !mounted;
        phase = Phase.Swinging;
        UpdateRig();
    }

    /// <summary>
    /// 请求沿当前摆动方向出钩。只能在已初始化、未暂停、未退场且 Phase.Swinging 时生效。
    /// </summary>
    public void RequestShot()
    {
        if (!initialized || stopped || paused || GameplayInputBlocked || phase != Phase.Swinging) return;
        phase = Phase.Extending;
        AudioManager.PlaySound("boardTrigger");
        extensionSound = AudioManager.PlaySound("findItems", loop: true);
    }

    // 立即结束本次伸绳循环并释放引用，避免回收或关卡重建后继续响。
    private void StopExtensionSound()
    {
        if (extensionSound != null && extensionSound.Using) extensionSound.Stop(0f);
        extensionSound = null;
    }

    private Bounds HeadBounds => mounted ? player.BodyBounds : Level137Geometry.SpriteBounds(hook);
    private Vector3 HookTip => hook.transform.TransformPoint(hookTipLocal);

    private static bool CaptureShape(Item item, ActorSpineCtrl visual)
    {
        var animation = visual.GetComponentInChildren<Spine.Unity.SkeletonAnimation>(true);
        if (animation == null || animation.Skeleton == null || animation.AnimationState == null) return false;
        // 不让源 Prefab 的 door_open 初态或默认混合时长混入关闭姿态快照。
        animation.Skeleton.SetToSetupPose();
        Spine.TrackEntry track = animation.AnimationState.GetCurrent(0);
        if (track == null) return false;
        track.MixDuration = 0f;
        animation.AnimationState.Apply(animation.Skeleton);
        item.FixedFrame = animation.transform;
        item.FixedLocalBounds = Level137Geometry.SkeletonLocalBounds(animation, item.Kind == Kind.Door ? "door_" : null);
        Vector3 size = item.FixedLocalBounds.size;
        return size.x > 0f && size.y > 0f && !float.IsInfinity(size.x) && !float.IsInfinity(size.y);
    }

    // 坐稳后置于车身后方；离座挂钩或被击飞时提到场景最前，只缓存一次原始层级供退场恢复。
    private void SetPlayerSorting(bool seated)
    {
        if (playerSorting == null)
        {
            playerSorting = player.GetComponent<SortingGroup>();
            addedPlayerSorting = playerSorting == null;
            if (addedPlayerSorting) playerSorting = player.gameObject.AddComponent<SortingGroup>();
            originalPlayerLayer = playerSorting.sortingLayerID;
            originalPlayerOrder = playerSorting.sortingOrder;
        }
        playerSorting.sortingLayerID = SortingLayer.NameToID("SceneLayer");
        if (seated)
        {
            SortingGroup carSorting = carCollider.GetComponentInParent<SortingGroup>();
            if (carSorting != null) playerSorting.sortingLayerID = carSorting.sortingLayerID;
            playerSorting.sortingOrder = carSorting != null ? carSorting.sortingOrder - 1 : 210;
        }
        else playerSorting.sortingOrder = Mathf.Max(originalPlayerOrder, 500);
    }

    private void TickExtension(float dt)
    {
        float extensionAtFrameStart = extension;
        Vector2 direction = swingRoot.TransformDirection(Vector3.down);
        Bounds from = HeadBounds;
        Bounds visible = mounted ? player.TravelBounds() : from;
        float available = Level137Geometry.TravelInside(Level137Geometry.ReferenceView(worldCamera), visible, direction, viewportMargin);
        float distance = Mathf.Min(extendSpeed * dt, available); // 匀速直线伸出，不使用缓动或加速曲线。
        Vector2 origin = mounted ? (Vector2)from.center : (Vector2)HookTip;
        Vector2 halfSize = mounted ? (Vector2)from.extents : Vector2.zero;
        if (pendingHit == null && distance > 0f)
        {
            Item target = FindFirstHit(origin, origin + direction * distance, halfSize,
                out float fraction, out float exit);
            if (target != null)
            {
                float contactTravel = distance * fraction;
                extension += contactTravel;
                distance -= contactTravel;
                available -= contactTravel;
                if (target.Kind == Kind.Door)
                {
                    UpdateRig();
                    if (mounted) EnterDoor();
                    else Hit(target);
                    return;
                }
                pendingHit = target;
                float hookLength = hook.transform.TransformVector(Vector3.up *
                    Level137Geometry.SpriteLocalBounds(hook).size.y).magnitude;
                penetrationRemaining = Mathf.Min(hookLength * penetrationLengthRatio,
                        (exit - fraction) * (contactTravel + distance) * 0.5f);
            }
        }
        if (pendingHit != null)
        {
            float step = Mathf.Min(distance, penetrationRemaining);
            // 深入星星/钥匙期间也不能穿过有效出口而漏掉入门接触。
            if (mounted && doorUnlocked && !doorGone && doorItem.State == ItemState.Available &&
                door.gameObject.activeInHierarchy)
            {
                float travel = extension + step - extensionAtFrameStart;
                Vector2 end = (Vector2)from.center + direction * travel;
                if (SweepDoorTrigger(from.center, end, from.extents, out float fraction, out _))
                {
                    extension = extensionAtFrameStart + travel * fraction;
                    pendingHit = null;
                    UpdateRig();
                    EnterDoor();
                    return;
                }
            }
            extension += step;
            penetrationRemaining -= step;
            UpdateRig();
            if (penetrationRemaining <= 0.0001f || step >= available - 0.0001f)
            {
                Item target = pendingHit;
                pendingHit = null;
                if (target.State == ItemState.Available && target.Root != null && target.Root.gameObject.activeInHierarchy) Hit(target);
                else phase = Phase.Retracting;
            }
            return;
        }
        extension += distance;
        UpdateRig();
        if (distance >= available - 0.0001f) phase = Phase.Retracting;
    }

    private Item FindFirstHit(Vector2 from, Vector2 to, Vector2 halfSize, out float fraction, out float exit)
    {
        fraction = 1f;
        exit = 1f;
        Item first = null;
        foreach (Item item in items)
        {
            if (item.State != ItemState.Available || item.Root == null || !item.Root.gameObject.activeInHierarchy) continue;
            if (item.Kind == Kind.Board && item.BoardIndex != nextBoard) continue;
            if (item.Kind == Kind.Door && (!doorUnlocked || doorGone)) continue;
            if (mounted && item.Kind != Kind.Star && item.Kind != Kind.Door &&
                !(item.Kind == Kind.Key && !doorUnlocked && !doorGone)) continue;
            // 载人入门与钩狗共用同一区域，只保留身体矩形/钩尖点的探头差异。
            float t;
            float end;
            bool hit = item.Kind == Kind.Door
                ? SweepDoorTrigger(from, to, halfSize, out t, out end)
                : Level137Geometry.SweepShape(from, to, halfSize, item.Frame, item.LocalBounds, out t, out end);
            if (hit &&
                (first == null || t < fraction))
            {
                first = item;
                fraction = t;
                exit = end;
            }
        }
        return first;
    }

    private bool SweepDoorTrigger(Vector2 from, Vector2 to, Vector2 halfSize, out float fraction, out float exit)
    {
        // 唯一入口区域：中心/旋转沿用门Collider，宽高共同缩为配置比例。
        // 载人伸出、深入和回程，以及普通钩狗，全部走这里；不吸附到中心。
        Bounds region = new Bounds(doorTrigger.offset,
            doorTrigger.size * Mathf.Clamp(doorEntrySizeRatio, 0.1f, 1f));
        return Level137Geometry.SweepShape(from, to, halfSize, doorTrigger.transform,
            region, out fraction, out exit);
    }

    private void Hit(Item item)
    {
        phase = Phase.Retracting;
        switch (item.Kind)
        {
            case Kind.Star:
                // 共享 Star 没有公开 Collect API。这里仅转发已由本关扫掠确认的收集接触，
                // 使用真实玩家 Collider 进入标准 Player 判定、去重、保存、发奖、飞星和音效。
                // 不改共享 Star，不伪造 Player tag，也不在本关复制存档/奖励逻辑。
                item.Star.SendMessage("OnTriggerEnter2D", player.CollectorCollider, SendMessageOptions.RequireReceiver);
                if (!item.Star.gameObject.activeSelf) item.State = ItemState.Consumed;
                break;
            case Kind.Key:
                if (!doorUnlocked && !doorGone)
                {
                    item.State = ItemState.FlyingKey;
                    keyJump.SetEndTarget(null);
                    keyJump.SetEndPosition(lockItem.Bounds.center);
                    // 标准 Key 的 PlaySfxListener 已播放 getKey，本关不再叠加一次。
                    keyExecutor.Execute();
                }
                else
                {
                    TakeCargo(item);
                    AudioManager.PlaySound("getKey");
                }
                break;
            case Kind.Door:
                if (mounted) EnterDoor();
                else
                {
                    phase = Phase.DogExit;
                    dogPullStarted = false;
                    dogEntryHoldRemaining = Mathf.Max(0f, dogEntryHoldSeconds);
                    hook.enabled = false;
                    rope.enabled = true;
                    UpdateRig();
                    AudioManager.PlaySound("bark");
                }
                break;
            default:
                TakeCargo(item);
                // 普通物件、当前告示牌和门锁只在钩取确认时反馈，运输途中不重播。
                AudioManager.PlaySound("getCoin");
                if (item.Kind == Kind.Board)
                {
                    nextBoard++;
                    if (nextBoard == boards.Length) lastBoard = item;
                }
                break;
        }
    }

    private void TakeCargo(Item item)
    {
        carried = item;
        // 在货物尚未随钩移动时锁定来源侧；左侧来源投到右侧，正中按左侧处理。
        item.DropOnRight = item.Bounds.center.x <= carCollider.bounds.center.x;
        item.State = ItemState.Carried;
        // 保持真实命中点的相对偏移，不在挂上那一帧把物件中心瞬移到钩头。
        carryOffset = item.Root.position - hook.transform.position;
        item.Root.SetParent(transform, true);
        var sorting = item.Root.GetComponent<SortingGroup>();
        if (sorting == null) sorting = item.Root.gameObject.AddComponent<SortingGroup>();
        sorting.sortingLayerID = SortingLayer.NameToID("SceneLayer");
        sorting.sortingOrder = 220 + storedCount;
        foreach (Collider2D collider in item.Root.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
    }

    private void TickRetraction(float dt)
    {
        Bounds from = HeadBounds;
        float distance = Mathf.Min(extension, retractSpeed * dt);
        if (mounted && doorUnlocked && !doorGone && doorItem.State == ItemState.Available)
        {
            Vector2 to = (Vector2)from.center + (Vector2)swingRoot.TransformDirection(Vector3.up) * distance;
            if (SweepDoorTrigger(from.center, to, from.extents, out float fraction, out _))
            {
                extension -= distance * fraction;
                UpdateRig();
                EnterDoor();
                return;
            }
        }
        extension -= distance;
        UpdateRig();
        if (extension > 0.0001f) return;
        extension = 0f;
        if (carried == null) { phase = Phase.Swinging; return; }
        if (carried.Kind == Kind.Dog)
        {
            dog.Release(); // 回收到车边才松开挂点，随后保留原来的上车小弧线。
            motionFrom = dog.transform.position;
            motionTo = new Vector3(seatPosition.x, Level137Geometry.BoxBounds(carCollider).max.y, seatPosition.z);
            BeginMotion(tossSeconds, ArcTimeCurve);
            phase = Phase.DogToCar;
            return;
        }
        motionFrom = carried.Root.position;
        Bounds cargoBounds = carried.Bounds;
        Vector3 centerOffset = carried.Root.position - cargoBounds.center;
        Vector3 landingCenter = cargoPoint.position;
        if (!carried.DropOnRight)
            landingCenter.x = 2f * carCollider.bounds.center.x - landingCenter.x;
        int sideIndex = carried.DropOnRight ? storedRightCount : storedLeftCount;
        landingCenter.x += (carried.DropOnRight ? 1f : -1f) * cargoHorizontalSpacing * sideIndex;
        // 每件货物的底边落在地面上，不能把中心固定在空中或按件数累加高度。
        landingCenter.y = Level137Geometry.BoxBounds(groundCollider).max.y +
            (carried.Kind == Kind.Lock ? GetUprightHalfHeight(carried) : cargoBounds.extents.y);
        motionTo = landingCenter + centerOffset;
        if (carried.Kind == Kind.Lock)
        {
            // 锁离开钩子开始投放才回正；用视觉中心走弧线，避免偏置pivot旋转时甩动。
            lockTossFromRotation = carried.Root.rotation;
            motionFrom = cargoBounds.center;
            motionTo = landingCenter;
        }
        BeginMotion(tossSeconds, CargoTossTimeCurve);
        phase = Phase.Tossing;
    }

    private static float GetUprightHalfHeight(Item item)
    {
        // 将素材的真实局部轴转到直立姿态，不能拿已经扩大的世界AABB再旋转。
        Quaternion straighten = Quaternion.Inverse(item.Root.rotation);
        Bounds local = item.LocalBounds;
        Vector3 x = straighten * item.Frame.TransformVector(Vector3.right * local.extents.x);
        Vector3 y = straighten * item.Frame.TransformVector(Vector3.up * local.extents.y);
        return Mathf.Abs(x.y) + Mathf.Abs(y.y);
    }

    private void TickToss(float dt)
    {
        float t = motion.Advance(dt);
        Vector3 position = Level137Geometry.ArcAtProgress(motionFrom, motionTo, t, tossArcHeight);
        if (carried.Kind == Kind.Lock)
        {
            carried.Root.rotation = Quaternion.Slerp(lockTossFromRotation, Quaternion.identity, t);
            carried.Root.position += position - carried.Bounds.center;
        }
        else carried.Root.position = position;
        if (!motion.Completed) return;
        EndMotion();
        carried.State = ItemState.Stored;
        LowerStoredCargoSorting(carried);
        if (carried.DropOnRight) storedRightCount++;
        else storedLeftCount++;
        carried = null;
        storedCount++;
        phase = Phase.Swinging;
    }

    private void LowerStoredCargoSorting(Item item)
    {
        var actorSorting = player.GetComponent<SortingGroup>();
        var cargoSorting = item.Root.GetComponent<SortingGroup>();
        if (actorSorting == null || cargoSorting == null)
        {
            Debug.LogError("[Level137] Missing Actor/cargo SortingGroup; cannot lower landed cargo behind Actor.", this);
            return;
        }
        cargoSorting.sortingLayerID = actorSorting.sortingLayerID;
        // 保留落地货物原来的 199 层级，不随本关主角整体提层而盖住车和其它货物。
        cargoSorting.sortingOrder = 199;
    }

    private void CheckLockDeparture()
    {
        if (doorUnlocked || doorGone || (lockItem.State != ItemState.Carried && lockItem.State != ItemState.Stored)) return;
        if (!Level137Geometry.Overlaps(lockItem.Bounds, doorItem.Bounds)) OpenDoor();
    }

    private void OpenDoor()
    {
        if (exiting || doorUnlocked || doorGone) return;
        doorUnlocked = true;
        AudioManager.PlaySound("openDoor");
        doorSpine.PlayAnimation("door_open", false);
    }

    // 由共享 Key 的事件列表在飞行结束后调用；钥匙自己的渐隐/销毁不等待锁动画。
    /// <summary>
    /// 接收共享钥匙到达门锁后的回调，负责把钥匙状态转交给本关的 DoorLock 解锁链。
    /// 共享事件链可能在回调前被取消，因此入口必须重复检查本关生命周期门禁。
    /// </summary>
    public void OnSharedKeyArrived()
    {
        if (stopped || exiting || !initialized || keyItem == null || keyItem.State != ItemState.FlyingKey) return;
        keyItem.State = ItemState.Consumed;
        if (doorGone || doorUnlocked || doorLock == null) return;
        keyUnlockStarted = true;
        lockItem.State = ItemState.Unlocking;
        foreach (BaseItemEventHandle handle in doorLock.GetComponents<BaseItemEventHandle>()) handle.enabled = true;
        doorLock.GetComponent<BaseItemExecutor>().enabled = true;
        doorLock.GetComponent<EventHandleListener>().Execute();
    }

    private void TickSharedUnlock()
    {
        if (!keyUnlockStarted || doorGone || doorLock != null) return;
        keyUnlockStarted = false;
        lockItem.State = ItemState.Consumed;
        OpenDoor();
    }

    private void EnterDoor()
    {
        if (phase == Phase.Finished || !mounted || !doorUnlocked || doorGone) return;
        phase = Phase.Finished;
        hook.enabled = true;
        rope.enabled = true;
        playerBinding.SetActive(false);
        BeginExitMotion(ExitMotion.Retracting, false, true);
        // Actor交接标准吸入，空钩独立回收；不再写Actor位置或重新检测门。
        level.OnActorTrigger2D(player.CollectorCollider);
        level.CheckFinished();
    }

    private void BeginResetExit()
    {
        if (stopped || exiting) return;
        if (!initialized) { StopOwnedWork(); return; }
        ExitMotion remaining = phase == Phase.Swinging ? ExitMotion.Swinging :
            phase == Phase.Extending ? ExitMotion.Extending :
            phase == Phase.Retracting || (phase == Phase.DogExit && dogPullStarted)
                ? ExitMotion.Retracting : ExitMotion.None;
        BeginExitMotion(remaining, mounted && phase != Phase.Mounting && phase != Phase.Finished);
    }

    private void BeginExitMotion(ExitMotion remaining, bool carryPlayer, bool resumeSwing = false)
    {
        StopExtensionSound();
        exiting = true;
        exitMotion = remaining;
        exitCarryPlayer = carryPlayer;
        exitResumeSwing = resumeSwing;
        exitExtensionLimit = extension;
        if (remaining == ExitMotion.Extending)
        {
            Bounds visible = carryPlayer && player != null ? player.TravelBounds() : Level137Geometry.SpriteBounds(hook);
            exitExtensionLimit += Level137Geometry.TravelInside(Level137Geometry.ReferenceView(worldCamera),
                visible, swingRoot.TransformDirection(Vector3.down), viewportMargin);
        }
        phase = Phase.Finished;
        pendingHit = null;
        penetrationRemaining = 0f;
        keyUnlockStarted = false;
        player?.StopDefeatFlight();
        EndMotion();
        holePresentation?.StopAbsorptionRibbon();
        foreach (Absorption absorption in absorptions) absorption.Motion.Kill();
        absorptions.Clear();
        // 禁用共享事件后再Kill，避免取消回调继续开门；保留暂停订阅直到真正卸载。
        CancelSharedEvents(keyExecutor != null ? keyExecutor.transform : null);
        CancelSharedEvents(doorLock);
        pausedSharedBehaviours.Clear();
        pausedSharedTweens.Clear();
        player?.StopMoving();
        if (suctionSound != null && suctionSound.Using) suctionSound.Stop(0f);
        if (fightSound != null && fightSound.Using) fightSound.Stop(0f);
    }

    private void TickExitMotion(float dt)
    {
        if (exitMotion == ExitMotion.None) return;
        if (exitMotion == ExitMotion.Swinging) TickSwing(dt);
        else
        {
            if (exitMotion == ExitMotion.Extending)
                extension = Mathf.MoveTowards(extension, exitExtensionLimit, extendSpeed * dt);
            else
                extension = Mathf.MoveTowards(extension, 0f, retractSpeed * dt);
            UpdateRig();
            if (exitMotion == ExitMotion.Retracting && extension <= 0f)
                exitMotion = exitResumeSwing ? ExitMotion.Swinging : ExitMotion.None;
            else if (exitMotion == ExitMotion.Extending && extension >= exitExtensionLimit)
                exitMotion = ExitMotion.None;
        }
        // 重启退场保留原挂载关系；成功入门永不再牵动Actor。
        if (exitCarryPlayer && player != null)
        {
            player.SetPosition(swingRoot.TransformPoint(hangingLocalPosition + Vector3.down * extension));
            player.transform.rotation = swingRoot.rotation * hangingLocalRotation;
        }
    }

    private void CheckHoleExposure()
    {
        if (holeStarted || lastBoard == null || nextBoard != boards.Length) return;
        if (Level137Geometry.Overlaps(lastBoard.Bounds, Level137Geometry.SpriteBounds(hole))) return;
        holeStarted = true;
        doorGone = true; // 从吸入开始即撤销门交互，不在视觉退场期间又判通关。
        if (keyItem.State == ItemState.FlyingKey && keyItem.Root != null)
        {
            CancelSharedEvents(keyItem.Root);
            keyItem.State = ItemState.Available;
        }
        if (keyUnlockStarted && doorLock != null)
        {
            CancelSharedEvents(doorLock);
            keyUnlockStarted = false;
            lockItem.State = ItemState.Available;
        }
        foreach (Item item in items)
        {
            if (item.State != ItemState.Available || !item.Root.gameObject.activeInHierarchy) continue;
            // 仍挂在门上的锁随门整体吸入，不能父子分别做两次位移。
            if (item.Kind == Kind.Lock && item.Root.IsChildOf(door))
            {
                item.State = ItemState.Absorbing;
                continue;
            }
            item.State = ItemState.Absorbing;
            absorptions.Add(new Absorption { Item = item, Center = item.Bounds.center,
                Scale = item.Root.localScale, Rotation = item.Root.rotation, Motion = new EasedMotion(suctionSeconds) });
        }
        suctionSound = AudioManager.PlaySound(suctionSfx);
        // 正常吸入开始才播放；防止切退场的同帧末尾检查把彩带重新开启。
        if (!stopped && !exiting && phase != Phase.Finished && absorptions.Count > 0)
            holePresentation?.PlayAbsorptionRibbon();
        // 没有 Fail/Reset 调用：门消失后由玩家主动重来。
    }

    private void TickAbsorptions(float dt)
    {
        Vector3 center = Level137Geometry.SpriteBounds(hole).center;
        for (int i = absorptions.Count - 1; i >= 0; i--)
        {
            Absorption absorption = absorptions[i];
            float eased = absorption.Motion.Advance(dt);
            Item item = absorption.Item;
            Vector3 desired = Vector3.Lerp(absorption.Center, center, eased);
            // 从吸入开始持续缩小，与位移/自转共用进度，到洞心时缩至零。
            item.Root.localScale = absorption.Scale * (1f - eased);
            item.Root.rotation = absorption.Rotation * Quaternion.Euler(0f, 0f, -720f * eased);
            // 先旋转/缩放，再补偿中心偏移：自身中心沿直线移动，不绕洞或偏置根节点公转。
            item.Root.position += desired - item.Bounds.center;
            if (!absorption.Motion.Completed) continue;
            absorption.Motion.Kill();
            item.State = ItemState.Consumed;
            item.Root.gameObject.SetActive(false);
            absorptions.RemoveAt(i);
        }
        if (absorptions.Count == 0) holePresentation?.StopAbsorptionRibbon();
    }

    private void TickDogExit(float dt)
    {
        if (!dogPullStarted)
        {
            // 先让钩爪消失的状态停留，狗仍隐藏；停留结束后才开始回收。
            float wait = Mathf.Min(dt, dogEntryHoldRemaining);
            dogEntryHoldRemaining = Mathf.Max(0f, dogEntryHoldRemaining - wait);
            dt -= wait;
            if (dogEntryHoldRemaining > 0f || dt <= 0f) return;
            // 回收第一帧立即恢复钩爪与原绳形，不等钩爪离开旋涡或狗完全出来。
            dogPullStarted = true;
            hook.enabled = true;
            UpdateRig();
            if (!dog.Begin(doorTrigger.transform, new Bounds(doorTrigger.offset, doorTrigger.size),
                hook.transform, hookTipLocal, swingRoot.TransformDirection(Vector3.up),
                swingRoot.TransformVector(Vector3.up * extension).magnitude))
            {
                StopOwnedWork();
                return;
            }
        }
        float distance = Mathf.Min(extension, retractSpeed * dt);
        float used = distance / Mathf.Max(0.1f, retractSpeed);
        extension = Mathf.Max(0f, extension - distance);
        UpdateRig();
        if (!dog.Completed) return;
        carried = new Item { Root = dog.transform, Kind = Kind.Dog, State = ItemState.Carried };
        phase = Phase.Retracting;
        TickRetraction(Mathf.Max(0f, dt - used));
    }

    private void BeginFight()
    {
        carried = null;
        SetPlayerAlpha(0f);
        dog.Spine.SetAlpha(0f);
        fight.transform.position = seatPosition;
        fight.SetActive(true);
        fightAnimator.SetBool("isStop", false);
        fightSound = AudioManager.PlaySound("dogFight");
        phaseTime = 0f;
        phase = Phase.Fighting;
    }

    private void TickDogToCar(float dt)
    {
        float t = motion.Advance(dt);
        dog.transform.position = Level137Geometry.ArcAtProgress(motionFrom, motionTo, t, tossArcHeight);
        // 短绳末端低于车身：先把狗自然送到玩家旁边，再用打斗云遮住双方，不能在云外突然隐藏。
        if (!motion.Completed) return;
        EndMotion();
        BeginFight();
    }

    private void BeginMotion(float seconds, AnimationCurve timeCurve = null)
    {
        EndMotion();
        motion = new EasedMotion(seconds, timeCurve);
    }

    private void EndMotion()
    {
        motion?.Kill();
        motion = null;
    }

    private void TickFight(float dt)
    {
        phaseTime += dt;
        if (phaseTime < fightSeconds) return;
        fightAnimator.SetBool("isStop", true);
        dog.transform.position = new Vector3(seatPosition.x, Level137Geometry.BoxBounds(carCollider).max.y, seatPosition.z);
        dog.Spine.SetAlpha(1f);
        dog.Spine.PlayAnimation("idle", true);
        // 先切die再显现，但不置IsDied；共享FallArea会拒绝已经死亡的Actor。
        var deathTrack = player.SpineCtrl.PlayAnimation("die", false, false);
        if (deathTrack != null) deathTrack.MixDuration = 0f;
        SetPlayerSorting(false);
        SetPlayerAlpha(1f);
        fight.SetActive(false);
        AudioManager.PlaySound("footballOut");
        player.BeginDefeatFlight(defeatKnockForce);
        phase = Phase.DefeatFlying;
    }

    private void TickDefeatFlying(float dt)
    {
        player.TickDefeatFlight(dt);
        // 真实刚体进入既有FallArea后由共享WaitingFail/TryToRestart结算；本关不按时间或位置伪造失败。
    }

    private void SetPlayerAlpha(float alpha)
    {
        if (player == null) return;
        player.SpineCtrl.SetAlpha(alpha);
        var group = player.GetComponent<SpriteRendererGroupAlpha>();
        if (group != null) { group.CollectRenderers(); group.SetAlpha(alpha); }
    }

    private void BindPanel()
    {
        GamePlayPanel panel = PlayCtrl.Instance != null ? PlayCtrl.Instance.CurrentGamePlayPanel : null;
        if (panel == boundPanel) return;
        RestorePanel();
        boundPanel = panel;
        if (panel == null) return;
        jumpButton = panel.transform.Find("Root/jump_btn");
        leftMoveButton = panel.transform.Find("Root/leftMove_btn")?.gameObject;
        rightMoveButton = panel.transform.Find("Root/rightMove_btn")?.gameObject;
        foreach (Transform child in panel.GetComponentsInChildren<Transform>(true))
            if (child.name.StartsWith("ClickBlock", System.StringComparison.Ordinal)) panelClickBlock = child.gameObject;
        jumpIcon = panel.JumpButtonIcon;
        if (jumpIcon == null || jumpButton == null || leftMoveButton == null || rightMoveButton == null)
        {
            Debug.LogError("[Level137] Gameplay buttons are missing; boarding input remains blocked.", this);
            jumpIcon = null;
            return;
        }
        originalLeftActive = leftMoveButton.activeSelf;
        originalRightActive = rightMoveButton.activeSelf;
        originalIcon = jumpIcon.sprite;
        originalIconRotation = jumpIcon.rectTransform.localRotation;
        originalIconScale = jumpIcon.rectTransform.localScale;
        originalPreserveAspect = jumpIcon.preserveAspect;
        originalIconRaycast = jumpIcon.raycastTarget;
        if (player != null && player.IsScripted) ApplyHookControls();
    }

    private void ApplyHookControls()
    {
        if (jumpIcon == null || boundPanel == null || hookControlsApplied) return;
        hookControlsApplied = true;
        // 清除面板长按状态，并关闭左右按钮的显示及输入，保留原 Jump 事件入口。
        boundPanel.SwitchMoveBtns(false);
        jumpIcon.sprite = hookButtonIcon;
        jumpIcon.preserveAspect = true;
        jumpIcon.raycastTarget = false;
        jumpIcon.rectTransform.localRotation = Quaternion.identity;
        jumpIcon.rectTransform.localScale = Vector3.one;
        hookRevealPending = true;
        TickHookButtonReveal();
    }

    private void TickHookButtonReveal()
    {
        if (!hookRevealPending || paused || GameplayInputBlocked || jumpButton == null) return;
        // 不抢占共享 HUD 入场动画的 targets/Completed，也不让两段缩放同时写同一按钮。
        var panelReveal = boundPanel.GetComponent<SequentialScaleAnim>();
        if (panelReveal != null && panelReveal.IsPlaying) return;
        originalJumpScale = jumpButton.localScale;
        jumpScaleCaptured = true;
        // 原按钮的按压缩放也会写根节点；出现期间暂停它和指针入口，完成后恢复。
        jumpInput = jumpButton.GetComponent<CustomButton>();
        if (jumpInput != null)
        {
            jumpInputWasEnabled = jumpInput.enabled;
            jumpInput.enabled = false;
        }
        jumpPressAnimations = jumpButton.GetComponentsInChildren<ClickScaleAnim>(true);
        jumpPressWasEnabled = new bool[jumpPressAnimations.Length];
        for (int i = 0; i < jumpPressAnimations.Length; i++)
        {
            jumpPressWasEnabled[i] = jumpPressAnimations[i].enabled;
            jumpPressAnimations[i].enabled = false;
        }
        jumpInteractionSuspended = true;
        hookButtonReveal = gameObject.AddComponent<SequentialScaleAnim>();
        hookButtonReveal.Duration = panelReveal != null ? panelReveal.Duration : 0.2f;
        hookButtonReveal.Completed += RestoreJumpInteraction;
        hookButtonReveal.SetTargetsAndPlay(jumpButton);
        hookRevealPending = false;
    }

    private void RestoreJumpInteraction()
    {
        if (!jumpInteractionSuspended) return;
        jumpInteractionSuspended = false;
        for (int i = 0; i < jumpPressAnimations.Length; i++)
            if (jumpPressAnimations[i] != null) jumpPressAnimations[i].enabled = jumpPressWasEnabled[i];
        if (jumpInput != null) jumpInput.enabled = jumpInputWasEnabled;
        jumpInput = null;
        jumpPressAnimations = null;
        jumpPressWasEnabled = null;
    }

    private void RestorePanel()
    {
        if (hookButtonReveal != null)
        {
            hookButtonReveal.enabled = false;
            hookButtonReveal.Completed -= RestoreJumpInteraction;
            hookButtonReveal.ClearTargets();
            Destroy(hookButtonReveal);
            hookButtonReveal = null;
        }
        RestoreJumpInteraction();
        if (hookControlsApplied)
        {
            if (boundPanel != null) boundPanel.ResetState();
            if (leftMoveButton != null) leftMoveButton.SetActive(originalLeftActive);
            if (rightMoveButton != null) rightMoveButton.SetActive(originalRightActive);
            if (jumpScaleCaptured && jumpButton != null) jumpButton.localScale = originalJumpScale;
        }
        if (jumpIcon != null)
        {
            jumpIcon.sprite = originalIcon;
            jumpIcon.preserveAspect = originalPreserveAspect;
            jumpIcon.raycastTarget = originalIconRaycast;
            jumpIcon.rectTransform.localRotation = originalIconRotation;
            jumpIcon.rectTransform.localScale = originalIconScale;
        }
        jumpIcon = null;
        boundPanel = null;
        jumpButton = null;
        leftMoveButton = rightMoveButton = panelClickBlock = null;
        hookControlsApplied = hookRevealPending = jumpScaleCaptured = false;
    }

    private void OnInputBlock(GamePlayPanelSwitchBlockEvent evt) { inputBlocked = evt.IsShow; }
    private void OnPause(GamePauseEvent _)
    {
        paused = true;
        // 本关在Awake订阅，早于BaseLevelCtrl.Start注册的StopMoving，先保存真实击飞速度。
        player?.SetDefeatFlightPaused(true);
        FreezeAnimations();
        if (suctionSound != null && suctionSound.Playing) suctionSound.Pause();
        if (fightSound != null && fightSound.Playing) fightSound.Pause();
        if (extensionSound != null && extensionSound.Playing) extensionSound.Pause();
    }

    private void OnResume(GameResumeEvent _)
    {
        paused = false;
        player?.SetDefeatFlightPaused(false);
        RestoreAnimations();
        foreach (var pair in pausedSharedBehaviours) if (pair.Key != null) pair.Key.enabled = pair.Value;
        pausedSharedBehaviours.Clear();
        foreach (Tween tween in pausedSharedTweens) if (tween.IsActive()) tween.Play();
        pausedSharedTweens.Clear();
        if (suctionSound != null && suctionSound.Paused) suctionSound.Resume();
        if (fightSound != null && fightSound.Paused) fightSound.Resume();
        if (extensionSound != null && extensionSound.Paused) extensionSound.Resume();
    }
    private void OnReset(GameResetEvent _) { BeginResetExit(); }
    private void OnReplay(GameReplayEvent _) { BeginResetExit(); }

    private void FreezeAnimations()
    {
        FreezeSharedEvents(keyExecutor != null ? keyExecutor.transform : null);
        FreezeSharedEvents(doorLock);
        foreach (ActorSpineCtrl spine in GetComponentsInChildren<ActorSpineCtrl>(true))
        {
            // 普通出生/行走的真实 Actor 保持共享暂停行为；只有锁定后才冻结其动画。
            if (player != null && !player.IsScripted && spine.transform.IsChildOf(player.transform)) continue;
            var state = spine.AnimationState;
            if (state == null || pausedSpines.ContainsKey(state)) continue;
            pausedSpines.Add(state, state.TimeScale);
            state.TimeScale = 0f;
        }
        foreach (Animator animator in GetComponentsInChildren<Animator>(true))
        {
            if (player != null && !player.IsScripted && animator.transform.IsChildOf(player.transform)) continue;
            if (pausedAnimators.ContainsKey(animator)) continue;
            pausedAnimators.Add(animator, animator.speed);
            animator.speed = 0f;
        }
    }

    private void RestoreAnimations()
    {
        foreach (var pair in pausedSpines) pair.Key.TimeScale = pair.Value;
        pausedSpines.Clear();
        foreach (var pair in pausedAnimators) if (pair.Key != null) pair.Key.speed = pair.Value;
        pausedAnimators.Clear();
    }

    private void FreezeSharedEvents(Transform root)
    {
        if (root == null) return;
        foreach (Behaviour component in root.GetComponents<Behaviour>())
        {
            if (!(component is BaseItemExecutor) && !(component is BaseItemEventHandle)) continue;
            if (pausedSharedBehaviours.ContainsKey(component)) continue;
            pausedSharedBehaviours.Add(component, component.enabled);
            component.enabled = false;
        }
        foreach (Transform target in root.GetComponentsInChildren<Transform>(true)) PauseTargetTweens(target);
        foreach (SpriteRenderer target in root.GetComponentsInChildren<SpriteRenderer>(true)) PauseTargetTweens(target);
    }

    private void PauseTargetTweens(Object target)
    {
        List<Tween> tweens = DOTween.TweensByTarget(target, true, null);
        if (tweens == null) return;
        foreach (Tween tween in tweens)
        {
            tween.Pause();
            pausedSharedTweens.Add(tween);
        }
    }

    private void CancelSharedEvents(Transform root)
    {
        if (root == null) return;
        // 先停执行器/等待监听器，再Kill，避免共享跳跃OnKill完成标记推动下一步解锁。
        foreach (Behaviour component in root.GetComponents<Behaviour>())
            if (component is BaseItemExecutor || component is BaseItemEventHandle) component.enabled = false;
        foreach (Transform target in root.GetComponentsInChildren<Transform>(true)) target.DOKill();
        foreach (SpriteRenderer target in root.GetComponentsInChildren<SpriteRenderer>(true)) target.DOKill();
    }

    private void StopOwnedWork()
    {
        stopped = true;
        StopExtensionSound();
        player?.StopDefeatFlight();
        holePresentation?.StopAbsorptionRibbon();
        exiting = false;
        exitMotion = ExitMotion.None;
        exitCarryPlayer = false;
        exitResumeSwing = false;
        EndMotion();
        foreach (Absorption absorption in absorptions) absorption.Motion.Kill();
        absorptions.Clear();
        dogEntryHoldRemaining = 0f;
        if (phase == Phase.DogExit && !dogPullStarted)
        {
            dogPullStarted = true;
            hook.enabled = true;
            UpdateRig();
        }
        dog?.Release();
        pendingHit = null;
        penetrationRemaining = 0f;
        pausedSharedBehaviours.Clear();
        pausedSharedTweens.Clear();
        CancelSharedEvents(keyExecutor != null ? keyExecutor.transform : null);
        CancelSharedEvents(doorLock);
        if (playerSorting != null)
        {
            if (addedPlayerSorting) Destroy(playerSorting);
            else
            {
                playerSorting.sortingLayerID = originalPlayerLayer;
                playerSorting.sortingOrder = originalPlayerOrder;
            }
            playerSorting = null;
        }
        RestorePanel();
        RestoreAnimations();
        player?.StopMoving();
        vortex?.Stop();
        if (suctionSound != null && suctionSound.Using) suctionSound.Stop(0f);
        if (fightSound != null && fightSound.Using) fightSound.Stop(0f);
        EventsUtils.ResetEvents(ref subscriber);
    }

    private void OnDisable() { StopOwnedWork(); }
    private void OnDestroy() { StopOwnedWork(); }
}
