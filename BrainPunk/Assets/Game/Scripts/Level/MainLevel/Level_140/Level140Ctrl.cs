using System;
using System.Collections.Generic;
using Bear.EventSystem;
using Game.Common;
using Game.Events;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using Spine.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>Level140 的局部用餐编排。所有位移/计时由本关可暂停时钟推进，不写存档或全局时间。</summary>
[DisallowMultipleComponent]
public sealed class Level140Ctrl : MonoBehaviour
{
    [Header("人工摆放引用")]
    /// <summary>四个用餐席位的共同父节点；仅在初始化时读取直接子节点并按世界 X 排序，必须恰好提供四个带 SpriteRenderer 的席位。</summary>
    [SerializeField] private Transform chairs;
    /// <summary>转盘带面的共同父节点；初始化和运行时只处理名称符合本关带面约定的直接子节点，至少需要两片等间距带面。</summary>
    [SerializeField] private Transform conveyor;
    /// <summary>出餐机器的人工摆放根节点；初始化时读取机器本体、故障部件和进度外框，节点结构变化时必须同步 Prefab 接线约定。</summary>
    [SerializeField] private Transform machine;
    /// <summary>左侧三层餐点样例；只作为盘子、食物层级和默认落点模板，运行时会隐藏模板但不会改写其 Transform。</summary>
    [SerializeField] private Transform leftMealTemplate;
    /// <summary>右侧餐盘落点样例；仅提供反向出餐落点，运行时会隐藏模板且不从中复制食物外观。</summary>
    [SerializeField] private Transform rightMealTemplate;
    /// <summary>玩家面部 BoneFollower 下的可调进食子点；玩家和动态顾客共用其标定，必须绑定子点而不是会被骨骼覆盖的父挂点。</summary>
    [Tooltip("Actor/spine/FoodMouthMount/FoodMouthPoint；子点可在 Prefab 中调整，挂点跟随 Black 面部骨骼。")]
    [SerializeField] private Transform playerMouthPoint;
    [Header("本关音效（项目已注册的 Sound tag，留空关闭）")]
    /// <summary>角色开始膨胀时播放一次的共享 Sound tag；只由本关控制器消费，留空可关闭且不得填入未注册 tag。</summary>
    [Tooltip("顾客开始 expand 时播放一次，expand_idle 不循环播放音效。")]
    [SerializeField] private string expandSfxTag = "spring";
    [Header("本关共享气泡")]
    /// <summary>顾客等待时可随机使用的 I2 key；仅供共享 HintBubble 显示，数组应只包含已登记且允许缺译回退的本关词条。</summary>
    [SerializeField] private string[] customerWaitingBubbleKeys =
    {
        "S_LevelTips_Des_level_140_2",
        "S_LevelTips_Des_level_140_3",
        "S_LevelTips_Des_level_140_4",
        "S_LevelTips_Des_level_140_5"
    };
    [Header("本关素材")]
    /// <summary>每盘随机使用的七种食物 Sprite；初始化要求数组长度恰为七且没有空引用，同盘实例只选其中一种。</summary>
    [SerializeField] private Sprite[] foodSprites;
    /// <summary>顾客身体服饰候选；Inspector显式绑定，初始三人及每次补位各随机一件，不使用玩家存档外观。</summary>
    [SerializeField] private GameObject[] customerClothes;
    /// <summary>机器满条待点击时使用的绿色进度 Sprite；只由运行时进度节点显示，必须与外框使用相同标定基准。</summary>
    [SerializeField] private Sprite readySprite;
    /// <summary>机器加载期间使用的红色进度 Sprite；只由运行时进度节点显示，不能为空且不应包含额外状态逻辑。</summary>
    [SerializeField] private Sprite loadingSprite;
    /// <summary>编辑器中手摆的烟雾出生姿态模板；运行时只读取外观和世界姿态并临时隐藏 Renderer，不移动或销毁模板。</summary>
    [Tooltip("编辑器中手摆的烟雾参考节点；实际烟团按它的出生姿态生成，不覆盖参考节点。")]
    [SerializeField] private SpriteRenderer smokeTemplate;
    [Header("本关视觉：手摆开关，不做两态切图")]
    /// <summary>墙面开关底座 Renderer；仅用于点击范围和可见性基准，运行时不得改写其 Sprite、位置或缩放。</summary>
    [Tooltip("Prefab 中手摆的开关底座；位置/缩放由编辑器摆放，运行时不覆盖，也不改写它的 Sprite。")]
    [SerializeField] private SpriteRenderer lightSwitchRenderer;
    /// <summary>墙面开关唯一拨杆 Renderer；沿146式正负Y翻面并移动，销毁时必须恢复手摆 OFF 姿态。</summary>
    [Tooltip("Prefab 中手摆的蓝色 kg 拨杆；OFF 取编辑器姿态，ON 移到下方并翻转Y缩放，尺寸绝对值不变。")]
    [SerializeField] private SpriteRenderer lightSwitchLever;
    /// <summary>开灯时拨杆使用的本地位置；关闭和销毁时恢复 Prefab 原位置，修改父层级后需要重新标定。</summary>
    [Tooltip("开灯时蓝色拨杆的本地位置（用户摆好的翻转后位置）；关灯回到编辑器位置。")]
    [SerializeField] private Vector3 lightSwitchOnLocalPosition = new Vector3(4.802097f, 2.5f, 0f);
    /// <summary>四席聚光幕罩使用的本关 Shader；只传给运行时材质实例，不能为空且不得直接修改共享材质。</summary>
    [SerializeField] private Shader seatLightsShader;
    /// <summary>仅本关四席灯光之外的黑色遮罩透明度；不读取或修改全局弹窗遮罩，0为全透明。</summary>
    [SerializeField, Range(0f, 1f)] private float seatShadeAlpha = 0.4f;
    /// <summary>膨胀退场角色的台面裁切 Shader；只用于每个角色的材质副本，不能为空且不得覆盖共享 Box 材质。</summary>
    [SerializeField] private Shader customerClipShader;
    [Header("标定（世界单位 / 秒）")]
    /// <summary>转盘带面和落地餐盘的水平移动速度；仅在玩法时钟运行时生效，单位为世界单位/秒且必须为正。</summary>
    [SerializeField, Min(0.1f)] private float beltSpeed = 2.4f;
    /// <summary>前七次出餐后的机器加载时长；由本关玩法时钟推进，单位为秒且暂停时不累计。</summary>
    [SerializeField, Min(0.1f)] private float loadSeconds = 2.2f;
    /// <summary>进度图片相对外框的额外内缩；当前素材自带透明边应保持零，单位为局部世界单位且不得缩成非正尺寸。</summary>
    [Tooltip("进度相对外框的额外内缩（局部世界单位）；当前 jd_02/jd_03 已自带透明留边，保持 0 以按原图对齐。")]
    [SerializeField, Min(0f)] private float progressFrameInset = 0f;
    /// <summary>运行时整盘相对样例的统一视觉倍率；同时作用于盘子、食物和层间距，不改变左右落点。</summary>
    [Tooltip("整盘视觉倍率：盘子、食物和层间距一起缩放，不改变落点。")]
    [SerializeField, Min(0.1f)] private float mealVisualScale = 1.2f;
    /// <summary>一次手势触发永久反转所需的向右净位移；仅统计从转盘开始的当前手势，单位为世界单位。</summary>
    [SerializeField, Min(0.1f)] private float reverseDragDistance = 3f;
    /// <summary>四个等待点相对各自椅子中心的统一水平偏移；只在初始化缓存等待点时使用，单位为世界单位。</summary>
    [SerializeField] private float seatHorizontalOffset = 0.2f;
    /// <summary>等待姿态脚底高于椅子下方站立地面的距离；玩家和顾客共用，单位为世界单位且不得为负。</summary>
    [Tooltip("等待时脚底距离该椅子下方站立地面的世界高度，不是相对椅子中心的偏移。")]
    [SerializeField, Min(0f)] private float waitingHeightAboveGround = 0.1f;
    /// <summary>玩家自动锁定到席位所需的最大水平距离；只在落地且竖直速度稳定时判断，不代表二维吸附半径。</summary>
    [Tooltip("玩家落稳且进入自己椅子的水平距离后自动锁定；只判定水平距离，不再判定垂直距离。")]
    [SerializeField, Min(0.05f)] private float sitReach = 0.2f;
    /// <summary>餐盘从机器飞到固定落点的时长；由本关玩法时钟推进，单位为秒且中途反转不改变目标。</summary>
    [SerializeField, Min(0.1f)] private float launchSeconds = 0.75f;
    /// <summary>餐盘抛物线相对直线路径增加的顶点高度；只影响飞行表现，单位为世界单位。</summary>
    [SerializeField] private float launchHeight = 2.2f;
    /// <summary>首次送玩家的 Alien 进场时长；仅用于首次尝试，单位为秒且重开不重复该运输流程。</summary>
    [SerializeField, Min(0.1f)] private float alienEnterSeconds = 1.2f;
    /// <summary>补位顾客 Alien 的进场时长；只影响补客演出，不改变首次玩家入场或席位恢复规则。</summary>
    [Tooltip("仅补客 Alien 的进场时长；不影响开局送玩家。")]
    [SerializeField, Min(0.1f)] private float replacementEnterSeconds = 0.8f;
    /// <summary>首次和补位 Alien 松爪后的退场时长；单位为秒，必须与顾客落位计时并行而非阻塞席位状态。</summary>
    [SerializeField, Min(0.1f)] private float alienExitSeconds = 0.8f;
    /// <summary>吃满顾客向右滚动退场的平移速度；仅供顾客使用，单位为世界单位/秒且只在退场阶段生效。</summary>
    [SerializeField, Min(0.1f)] private float departingSpeed = 2.5f;
    /// <summary>吃满顾客退场时的顺时针角速度；仅供顾客使用，单位为度/秒且不影响转盘旋转方向。</summary>
    [Tooltip("膨胀后退场的顺时针自转角速度（度/秒）；不影响转盘输送或顾客右移速度。")]
    [SerializeField, Min(0f)] private float departingRotationSpeed = 100f;

    private enum MachineState { Intro, Loading, Ready, Broken, Finished }
    private enum PlayerMealState { Dining, Expanding, WaitingForSuccess }
    private enum CustomerState { Seated, Eating, Expanding, Arriving }
    private sealed class DepartingCustomer
    {
        public Transform Root;
    }
    private sealed class Customer
    {
        public Transform Root;
        public SkeletonAnimation Spine;
        public Transform Seat;
        public Vector3 WaitingPosition;
        public CustomerState State;
        public int Count;
        public int PendingBites;
        public Transform MouthPoint;
        public BoneFollower MouthFollower;
        public SpriteRenderer[] ClothingRenderers;
        public float Time;
        public float EatDuration;
        public float WaitingTime;
        public DrawAlienClipBallRigView Carrier;
        public Vector3 CarrierStart, CarrierEnd;
        public bool Dropped;
    }
    private sealed class Plate
    {
        public Transform Root;
        public SpriteRenderer Dish;
        public readonly List<SpriteRenderer> Foods = new List<SpriteRenderer>();
        public Vector3 Start, Landing;
        public float Flight;
        public bool Landed;
    }
    private sealed class Bite
    {
        public Transform Food, Target;
        public Vector3 Start, Scale;
        public float Time;
        public Customer Customer;
        public BoneFollower MouthFollower;
    }

    private BaseLevelCtrl level;
    private Level140ActorCtrl actor;
    private Camera gameCamera;
    private Transform runtimeRoot;
    private Transform playerSeat;
    private DrawAlienClipBallRigView alien;
    private DrawAlienClipBallRigView carrierTemplate;
    private Transform carrierTemplateRoot;
    private Vector3 alienStart, alienEnd, gripPosition, gripScale;
    private Quaternion gripRotation;
    private readonly List<Customer> customers = new List<Customer>();
    private readonly List<DepartingCustomer> departingCustomers = new List<DepartingCustomer>();
    private readonly Dictionary<Transform, Vector3> waitingPositions = new Dictionary<Transform, Vector3>();
    private readonly List<Plate> plates = new List<Plate>();
    private readonly List<Bite> bites = new List<Bite>();
    private readonly List<Transform> beltTiles = new List<Transform>();
    private readonly List<float> tileStarts = new List<float>();
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    private readonly Dictionary<SkeletonAnimation, float> pausedAnimations = new Dictionary<SkeletonAnimation, float>();
    private EventSubscriber events;
    private Level140CustomerOcclusion playerOcclusion;
    private SkeletonAnimation playerSkeleton;
    private HintBubble mealBubbleTemplate, customerWaitingBubble;
    private Customer customerBubbleOwner;
    private bool mealBubbleTemplateResolved, customerBubbleClosing;
    private float customerBubbleTime, customerBubbleCooldown;
    private int lastCustomerBubbleLine = -1;
    private SpriteRenderer progress;
    private SpriteRenderer faultReadyOverlay;
    private Color faultPartColor, faultReadyColor;
    private Level140SmokeView smoke;
    private Level140LightingView lighting;
    private SpriteRenderer machineBody;
    private SpriteRenderer brokenPart;
    private SpriteRenderer dishTemplate;
    private SpriteRenderer[] foodTemplates;
    private Vector3 leftLanding, rightLanding, progressPosition, progressScale;
    private MachineState state;
    private PlayerMealState playerMealState;
    private float timer, introTime, beltOffset, beltMin, beltLength, tileWidth, surfaceY;
    private float playerEatTime, playerEatDuration;
    private Vector3 machineBaseScale;
    private float machinePulseTime = -1f;
    private int playerMealCount, playerPendingBites;
    // 当前由玩家逐个领取的餐盘；餐盘仍输送，连吃期间其他顾客不抢取该盘。
    private Plate playerMealPlate;
    private int servings;
    private int plateSortingOrder;
    private bool initialized, introReleased, introDone, reverse, beltStarted, stopped, suspended;
    private bool retryAtCreation;
    private bool reportedMealForecastGap;
    private BoneFollower playerMouthFollower;
    private bool dragging, machinePress, lightPress;
    private int finger = -1;
    private Vector2 pointerStart;
    private const int SceneLayer = 1752186269;
    private const float BiteSeconds = 0.3f;
    private const int PlayerMealsRequired = 3;
    private const float CustomerBubbleDisplaySeconds = 1.5f;
    private const float CustomerWaitBeforeBubble = 3f;
    private const float CustomerBubbleMinInterval = 5f;
    private const float CustomerBubbleMaxInterval = 8f;
    private const float FaultTransitionSeconds = 0.4f;
    private const float FaultSmokeDelay = 0.15f;
    private const float MealForecastMargin = 0.15f;

    /// <summary>表示本关初始化完成、尚未收口且共享玩法处于可推进状态；仅供本关 Actor 门禁读取，不代表关卡已可结算。</summary>
    public bool IsRunning => initialized && !stopped && IsPlaying();
    private bool PlayerFinishing => playerMealState != PlayerMealState.Dining || playerMealCount + playerPendingBites >= PlayerMealsRequired;
    /// <summary>表示玩家当前可接受移动、跳跃和机关输入；仅供本关 Actor 与输入分支读取，进食或最终退场期间必须保持 false。</summary>
    public bool CanOperate => IsRunning && introDone && state != MachineState.Finished && !PlayerFinishing && !actor.Eating && !actor.IsDied;
    private bool CanAutoSeat => CanOperate && !actor.IsSit && actor.IsGround &&
        Mathf.Abs(actor.RB.linearVelocity.y) < 0.35f &&
        Mathf.Abs(actor.transform.position.x - playerSeat.position.x) <= sitReach;

    private void Awake()
    {
        level = GetComponent<BaseLevelCtrl>();
        if (!(level is Level140BaseLevelCtrl))
        { ConfigurationError("需要 Level140BaseLevelCtrl 处理本关入场与单次结算"); return; }
        actor = level.Actor as Level140ActorCtrl;
        level.SetEnterDoorAnimationEnabled(false);
        level.SetCtrlable(false);
        if (actor == null) { ConfigurationError("缺少 Level140ActorCtrl"); return; }
        actor.Owner = this;
        gameCamera = GetComponentInChildren<Camera>(true);
        alien = transform.Find("Alien")?.GetComponent<DrawAlienClipBallRigView>();
        var path = alien != null ? alien.GetComponent<DrawEntrancePathView>() : null;
        if (path == null || !path.TryGetWorldPositions(out alienStart, out alienEnd))
        { ConfigurationError("Alien 缺少原有入场起终点"); return; }
        alien.ResolveReferences();
        gripPosition = actor.transform.localPosition;
        gripRotation = actor.transform.localRotation;
        gripScale = actor.transform.localScale;
        if (alien.ClipSwingPivot == null || alien.ClipSkeletonAnimation == null || gameCamera == null)
        { ConfigurationError("Alien 夹子或关卡相机引用缺失"); return; }
        // 与共享 CreateLevelAsync 使用相同的首次/重开判定。重开不能先把玩家搬到场外等 PLAYING。
        var attempt = CurrentPlay?.Level?.CurrentLevelState;
        retryAtCreation = attempt != null && (attempt.ResetCount > 0 || attempt.FailCount > 0);
        if (!retryAtCreation) alien.transform.position = alienStart;
    }

    private void OnEnable()
    {
        events = new EventSubscriber();
        events.Subscribe<GameResetEvent>(_ => StopRuntime());
        events.Subscribe<GameReplayEvent>(_ => StopRuntime());
        events.Subscribe<SwitchGameStateEvent>(OnStateChanged);
        events.Subscribe<GamePauseEvent>(_ => SetSuspended(true));
        events.Subscribe<OnTriggerFailAreaEvent>(_ => StopRuntime());
        events.Subscribe<OnTriggerTrapEvent>(_ => StopRuntime());
    }

    private void Start()
    {
        if (stopped) return;
        if (chairs == null || conveyor == null || machine == null || leftMealTemplate == null || rightMealTemplate == null ||
            playerMouthPoint == null || playerMouthPoint.parent == null ||
            readySprite == null || loadingSprite == null || smokeTemplate == null || smokeTemplate.sprite == null || foodSprites == null || foodSprites.Length != 7 ||
            lightSwitchRenderer == null || lightSwitchLever == null || seatLightsShader == null || customerClipShader == null ||
            Array.Exists(foodSprites, sprite => sprite == null) || customerClothes == null || customerClothes.Length == 0 ||
            Array.Exists(customerClothes, clothes => clothes == null))
        { ConfigurationError("椅子、转盘、机器、落点或素材未接齐"); return; }
        actor.EnsureInitialized();
        playerSkeleton = actor.GetComponentInChildren<SkeletonAnimation>(true);
        if (playerSkeleton == null)
        { ConfigurationError("玩家缺少膨胀退场所需的 SkeletonAnimation/Renderer"); return; }
        playerMouthFollower = playerMouthPoint.parent.GetComponent<BoneFollower>();
        if (playerMouthFollower == null)
        { ConfigurationError("进食点缺少面部 BoneFollower 挂点"); return; }
        playerMouthFollower.Initialize();
        if (!playerMouthFollower.valid || playerMouthFollower.bone == null)
        { ConfigurationError("进食点的面部骨骼未绑定成功"); return; }
        runtimeRoot = new GameObject("Level140_Runtime").transform;
        runtimeRoot.SetParent(transform, false);
        var seats = new List<Transform>();
        foreach (Transform child in chairs) if (child.GetComponent<SpriteRenderer>() != null) seats.Add(child);
        seats.Sort((a, b) => a.position.x.CompareTo(b.position.x));
        if (seats.Count != 4) { ConfigurationError("需要四把人工摆放的椅子"); return; }
        playerSeat = seats[0];
        if (!CacheWaitingPositions(seats)) return;
        dishTemplate = leftMealTemplate.Find("Sp_l140_pz_01")?.GetComponent<SpriteRenderer>();
        var foods = new List<SpriteRenderer>();
        foreach (var renderer in leftMealTemplate.GetComponentsInChildren<SpriteRenderer>(true))
            if (renderer.name.StartsWith("Sp_l140_sw_", StringComparison.Ordinal)) foods.Add(renderer);
        foods.Sort((a, b) => a.transform.position.y.CompareTo(b.transform.position.y));
        foodTemplates = foods.ToArray();
        var rightDish = rightMealTemplate.Find("Sp_l140_pz_01");
        if (dishTemplate == null || foodTemplates.Length != 3 || rightDish == null)
        { ConfigurationError("sw1 需要一个盘子、三层食物和右侧盘子落点"); return; }
        leftLanding = dishTemplate.transform.position;
        rightLanding = rightDish.position;
        leftMealTemplate.gameObject.SetActive(false);
        rightMealTemplate.gameObject.SetActive(false);

        foreach (Transform child in conveyor)
            if (child.name.StartsWith("Sp_l140_zm_02", StringComparison.Ordinal)) beltTiles.Add(child);
        beltTiles.Sort((a, b) => a.localPosition.x.CompareTo(b.localPosition.x));
        if (beltTiles.Count < 2) { ConfigurationError("转盘至少需要两片 zm_02"); return; }
        tileWidth = beltTiles[1].localPosition.x - beltTiles[0].localPosition.x;
        beltMin = beltTiles[0].localPosition.x;
        beltLength = tileWidth * beltTiles.Count;
        surfaceY = float.NegativeInfinity;
        foreach (Transform tile in beltTiles)
        {
            tileStarts.Add(tile.localPosition.x);
            surfaceY = Mathf.Max(surfaceY, tile.GetComponent<SpriteRenderer>().bounds.max.y);
        }
        // 整盘归入转台排序组，在组内高于台体/带面；不越过机器其他部件的排序组。
        plateSortingOrder = 0;
        foreach (var renderer in conveyor.GetComponentsInChildren<SpriteRenderer>(true))
            plateSortingOrder = Mathf.Max(plateSortingOrder, renderer.sortingOrder + 1);

        machineBody = machine.Find("Sp_l140_jq_01")?.GetComponent<SpriteRenderer>();
        brokenPart = machine.Find("Sp_l140_jq_03")?.GetComponent<SpriteRenderer>();
        var frame = machine.Find("Sp_l140_jd_01")?.GetComponent<SpriteRenderer>();
        if (machineBody == null || brokenPart == null || frame == null || frame.sprite == null)
        { ConfigurationError("机器 jq_01/jq_03/jd_01 缺失"); return; }
        // jd_01 中间透明；机器 < 进度 < 外框，进度缩在框内，不能覆盖框的描边。
        int progressOrder = Mathf.Max(frame.sortingOrder, machineBody.sortingOrder + 1);
        frame.sortingOrder = progressOrder + 1;
        progress = NewSprite("Level140_Progress", frame.transform, readySprite, 0);
        progress.sortingLayerID = frame.sortingLayerID;
        // 进度内部允许绿/红两层交叠过渡，整体仍处于机器本体与外框之间。
        var progressSorting = progress.gameObject.AddComponent<SortingGroup>();
        progressSorting.sortingLayerID = frame.sortingLayerID;
        progressSorting.sortingOrder = progressOrder;
        Vector3 frameSize = frame.sprite.bounds.size;
        Vector3 fillSize = readySprite.bounds.size;
        progressPosition = frame.sprite.bounds.center;
        progressScale = new Vector3(
            Mathf.Max(0.01f, frameSize.x - 2f * progressFrameInset) / fillSize.x,
            Mathf.Max(0.01f, frameSize.y - 2f * progressFrameInset) / fillSize.y, 1f);
        SetProgress(readySprite, 1f);
        smoke = new GameObject("Level140_FaultSmoke").AddComponent<Level140SmokeView>();
        smoke.transform.SetParent(machine, false);
        smoke.Configure(smokeTemplate, frame.sortingOrder + 2);

        lighting = new GameObject("Level140_SeatLights").AddComponent<Level140LightingView>();
        lighting.transform.SetParent(runtimeRoot, false);
        lighting.Configure(gameCamera, new Vector4(seats[0].position.x, seats[1].position.x, seats[2].position.x, seats[3].position.x),
            surfaceY, seatLightsShader, lightSwitchRenderer, lightSwitchLever, lightSwitchOnLocalPosition, seatShadeAlpha);
        for (int i = 1; i < seats.Count; i++) customers.Add(CreateCustomer(seats[i]));
        InitializeRig(alien);
        initialized = true;
        state = MachineState.Intro;
        // Reset 的黑幕先退去，PLAYING 后恢复；视觉初态必须在此时就准备好，不能等 Update。
        if (retryAtCreation) RestoreRetryState();
        if (!stopped) SetSuspended(!IsPlaying());
    }

    private void Update()
    {
        if (!initialized || stopped || !actor.Initialized) return;
        bool playing = IsPlaying();
        SetSuspended(!playing);
        if (!playing) { ClearPointer(); return; }
        if (actor.IsDied) { StopRuntime(); return; }
        float dt = Time.deltaTime;
        if (!introDone) { TickIntro(dt); return; }
        TickPlayerMeal(dt);
        if (stopped) return;
        TryAutoSeat();
        actor.LockWaitingPose();
        level.SetCtrlable(CanOperate && !actor.IsSit);
        TickInput();
        TickCustomers(dt);
        TickPlates(dt);
        TickBelt(dt);
        TickMachine(dt);
        TickMachinePulse(dt);
        lighting.Tick(dt);
        TickCustomerWaitingBubble(dt);
    }

    private void TickPlayerMeal(float dt)
    {
        if (playerMealState == PlayerMealState.WaitingForSuccess)
        {
            playerEatTime += dt;
            if (playerEatTime >= 0.5f) Win();
            return;
        }
        if (playerMealState == PlayerMealState.Expanding)
        {
            playerEatTime += dt;
            if (playerEatTime < playerEatDuration) return;
            actor.HoldExpandedPose();
            playerEatTime = 0f;
            playerMealState = PlayerMealState.WaitingForSuccess;
            return;
        }
        if (!actor.Eating) return;
        playerEatTime += dt;
        if (playerPendingBites > 0 || playerEatTime < playerEatDuration) return;
        if (playerMealCount < PlayerMealsRequired)
        {
            // 当前一份入嘴且吃完，立即从同盘取下一份，不等待餐盘绕回。
            if (playerMealPlate != null && playerMealPlate.Root != null && playerMealPlate.Foods.Count > 0)
            {
                TakeFood(playerMealPlate, null);
                return;
            }
            playerMealPlate = null;
            actor.EndEating();
            return;
        }

        playerMealPlate = null;
        // 三份均真正入嘴且 eat 完成才变胖；不是取到第一份就成功。
        CloseCustomerWaitingBubble();
        ClearPointer();
        level.SetCtrlable(false);
        playerOcclusion = actor.gameObject.AddComponent<Level140CustomerOcclusion>();
        playerOcclusion.Apply(playerSkeleton, customerClipShader, surfaceY);
        playerEatTime = 0f;
        playerEatDuration = actor.BeginMealExpansion();
        playerMealState = PlayerMealState.Expanding;
        if (!string.IsNullOrWhiteSpace(expandSfxTag)) AudioManager.PlaySound(expandSfxTag);
    }

    private void LateUpdate()
    {
        if (!IsRunning || !introDone || !actor.Initialized) return;
        // 面部动画在 Update 后已经更新，再取嘴部位置，避免食物追着前一帧的根节点偏移。
        TickBites(Time.deltaTime);
    }

    private void TickIntro(float dt)
    {
        introTime += dt;
        if (!introReleased)
        {
            float t = Mathf.Clamp01(introTime / alienEnterSeconds);
            alien.transform.position = Vector3.Lerp(alienStart, alienEnd, Mathf.SmoothStep(0f, 1f, t));
            if (t < 1f) return;
            alien.ClipBoneFollower?.LateUpdate();
            PlayClip(alien, "clip_open");
            actor.Release(transform);
            introReleased = true;
            CreateCarrierTemplate();
            introTime = 0f;
        }
        float exit = Mathf.Clamp01(introTime / alienExitSeconds);
        alien.transform.position = Vector3.Lerp(alienEnd, alienStart, Mathf.SmoothStep(0f, 1f, exit));
        if (exit >= 1f) alien.gameObject.SetActive(false);
        if (exit >= 1f && actor.IsGround && Mathf.Abs(actor.RB.linearVelocity.y) < 0.35f) CompleteIntro();
        else if (introTime > 8f) ConfigurationError("玩家松爪后未落稳，请检查当前地面和 Actor 碰撞体");
    }

    private void RestoreRetryState()
    {
        // 在暂停/遮罩期间同步恢复位置、idle 和阴影，物理继续冻结直到游戏恢复。
        actor.SetFrozen(true);
        alien.transform.position = alienEnd;
        alien.ClipBoneFollower?.LateUpdate();
        actor.Release(transform, standing: true);
        if (!PlaceAtLanding()) return;
        introReleased = true;
        CreateCarrierTemplate();
        alien.gameObject.SetActive(false);
        CompleteIntro();
    }

    private bool PlaceAtLanding()
    {
        if (!TryGetStandingFootOffset(out float foot)) return false;
        Physics2D.SyncTransforms();
        RaycastHit2D best = default;
        foreach (RaycastHit2D hit in Physics2D.RaycastAll(actor.transform.position, Vector2.down, 30f, LayerMask.GetMask("Ground")))
            if (!hit.collider.isTrigger && !hit.transform.IsChildOf(actor.transform) && (best.collider == null || hit.distance < best.distance)) best = hit;
        if (best.collider == null) { ConfigurationError("Reset 落点下方找不到 Ground"); return false; }
        Vector3 position = new Vector3(actor.transform.position.x, best.point.y + foot + 0.01f, actor.transform.position.z);
        actor.RB.position = position;
        actor.transform.position = position;
        actor.RB.linearVelocity = Vector2.zero;
        Physics2D.SyncTransforms();
        return true;
    }

    private void CompleteIntro()
    {
        if (stopped) return;
        introDone = true;
        actor.EnableWalking();
        level.SetCtrlable(true);
        // 开局/Reset 直接满条，第一次出餐不用先等冷却。
        timer = 0f;
        state = MachineState.Ready;
        SetProgress(readySprite, 1f);
    }

    private void CreateCarrierTemplate()
    {
        carrierTemplateRoot = new GameObject("Level140_CarrierTemplate").transform;
        carrierTemplateRoot.SetParent(runtimeRoot, false);
        carrierTemplateRoot.gameObject.SetActive(false);
        // 玩家已脱离，复制的是当前人工调整的夹子，不复制任何玩家或共享 ActorCtrl。
        carrierTemplate = Instantiate(alien, carrierTemplateRoot);
        carrierTemplate.gameObject.SetActive(true);
    }

    private bool TryGetStandingFootOffset(out float standingFootOffset)
    {
        // 夹持或暂停时不依赖 Collider.world bounds；读取正向、单位根缩放下的同一脚底几何。
        standingFootOffset = 0f;
        var shape = actor.GetComponent<PolygonCollider2D>();
        if (shape == null || shape.points.Length == 0)
        { ConfigurationError("缺少用于计算正常站立脚底的 Actor PolygonCollider2D"); return false; }
        float bottom = float.PositiveInfinity;
        foreach (Vector2 point in shape.points) bottom = Mathf.Min(bottom, point.y + shape.offset.y);
        standingFootOffset = -bottom * Mathf.Abs(transform.lossyScale.y);
        return true;
    }

    private bool CacheWaitingPositions(List<Transform> seats)
    {
        if (!TryGetStandingFootOffset(out float standingFootOffset)) return false;
        Physics2D.SyncTransforms();
        foreach (Transform seat in seats)
        {
            Vector3 position = seat.position + Vector3.right * seatHorizontalOffset;
            Vector2 rayStart = new Vector2(position.x, seat.position.y + 8f);
            RaycastHit2D best = default;
            foreach (var hit in Physics2D.RaycastAll(rayStart, Vector2.down, 30f, LayerMask.GetMask("Ground")))
            {
                if (hit.collider.isTrigger || !hit.transform.IsChildOf(transform)) continue;
                if (best.collider == null || hit.distance < best.distance) best = hit;
            }
            if (best.collider == null)
            { ConfigurationError("椅子下方找不到站立地面：" + seat.name); return false; }
            position.y = best.point.y + standingFootOffset + waitingHeightAboveGround;
            waitingPositions.Add(seat, position);
        }
        return true;
    }

    private Customer CreateCustomer(Transform seat)
    {
        var root = new GameObject("Level140_Customer").transform;
        root.SetParent(runtimeRoot, false);
        root.position = waitingPositions[seat];
        var skeleton = SkeletonAnimation.NewSkeletonAnimationGameObject(actor.GetComponentInChildren<SkeletonAnimation>(true).skeletonDataAsset);
        skeleton.name = "Box";
        skeleton.transform.SetParent(root, false);
        skeleton.transform.localPosition = actor.transform.Find("spine").localPosition;
        skeleton.transform.localScale = actor.transform.Find("spine").localScale;
        skeleton.Skeleton.SetSkin("box_original");
        skeleton.Skeleton.SetSlotsToSetupPose();
        // 复用共享Actor的前后身分层，衣服夹在身体0与脸部5之间，避免盖住表情。
        skeleton.separatorSlots.Add(skeleton.Skeleton.FindSlot("front-b2"));
        SkeletonRenderSeparator.AddToSkeletonRenderer(skeleton);
        var sorting = root.gameObject.AddComponent<SortingGroup>();
        sorting.sortingLayerID = SceneLayer;
        sorting.sortingOrder = 199; // 初始和补位顾客统一，低于保留原排序 200 的玩家。
        SpriteRenderer[] clothingRenderers = AttachCustomerClothes(skeleton);
        // 不克隆已激活的玩家 BoneFollower，避免其 Awake 先订阅玩家 Skeleton 的重建事件。
        var mouthMount = new GameObject("FoodMouthMount").transform;
        mouthMount.SetParent(skeleton.transform, false);
        var mouthFollower = mouthMount.gameObject.AddComponent<BoneFollower>();
        mouthFollower.boneName = playerMouthFollower.boneName;
        mouthFollower.followXYPosition = playerMouthFollower.followXYPosition;
        mouthFollower.followZPosition = playerMouthFollower.followZPosition;
        mouthFollower.followBoneRotation = playerMouthFollower.followBoneRotation;
        mouthFollower.followSkeletonFlip = playerMouthFollower.followSkeletonFlip;
        mouthFollower.followLocalScale = playerMouthFollower.followLocalScale;
        mouthFollower.followParentWorldScale = playerMouthFollower.followParentWorldScale;
        mouthFollower.maintainedAxisOrientation = playerMouthFollower.maintainedAxisOrientation;
        mouthFollower.SkeletonRenderer = skeleton;
        mouthFollower.Initialize();
        var mouthPoint = Instantiate(playerMouthPoint, mouthMount, false);
        mouthPoint.name = "FoodMouthPoint";
        Play(skeleton, "idle", true);
        return new Customer { Root = root, Spine = skeleton, Seat = seat,
            MouthPoint = mouthPoint, MouthFollower = mouthFollower,
            ClothingRenderers = clothingRenderers,
            WaitingPosition = waitingPositions[seat], State = CustomerState.Seated };
    }

    private SpriteRenderer[] AttachCustomerClothes(SkeletonAnimation skeleton)
    {
        var mount = new GameObject("CustomerClothes");
        mount.transform.SetParent(skeleton.transform, false);
        var follower = mount.AddComponent<BoneFollower>();
        follower.boneName = "point02";
        follower.followLocalScale = true;
        follower.followParentWorldScale = true;
        follower.SkeletonRenderer = skeleton;
        follower.LateUpdate();
        // 与共享Actor的身体挂点相同，衣服Default/2在身体之上、面部之下。
        var sorting = mount.AddComponent<SortingGroup>();
        sorting.sortingOrder = 2;
        var clothes = Instantiate(customerClothes[UnityEngine.Random.Range(0, customerClothes.Length)], mount.transform, false);
        clothes.transform.localPosition = Vector3.zero;
        clothes.transform.localRotation = Quaternion.identity;
        clothes.transform.localScale = Vector3.one;
        clothes.SetActive(true);
        return clothes.GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void TickCustomers(float dt)
    {
        // 旧顾客的表现与席位补客分开推进，不能等其走出屏幕后才派 Alien。
        for (int i = departingCustomers.Count - 1; i >= 0; i--)
        {
            DepartingCustomer departing = departingCustomers[i];
            departing.Root.position += Vector3.right * (departingSpeed * dt);
            departing.Root.Rotate(0f, 0f, -departingRotationSpeed * dt);
            if (departing.Root.position.x <= CameraRight() + 3f) continue;
            Destroy(departing.Root.gameObject);
            departingCustomers.RemoveAt(i);
        }
        foreach (var customer in customers)
        {
            customer.Time += dt;
            customer.WaitingTime = customer.State == CustomerState.Seated ? customer.WaitingTime + dt : 0f;
            if (customer.State == CustomerState.Seated || customer.State == CustomerState.Eating ||
                customer.State == CustomerState.Expanding)
            {
                customer.Root.position = customer.WaitingPosition;
                customer.Root.rotation = Quaternion.identity;
            }
            switch (customer.State)
            {
                case CustomerState.Eating:
                    if (customer.PendingBites > 0 || customer.Time < customer.EatDuration) break;
                    customer.Time = 0f;
                    if (customer.Count >= Level140MealPlanner.MealsPerCustomer)
                    {
                        customer.Root.gameObject.AddComponent<Level140CustomerOcclusion>().Apply(customer.Spine, customerClipShader, surfaceY,
                            customer.ClothingRenderers);
                        customer.EatDuration = Play(customer.Spine, "expand", false);
                        if (!string.IsNullOrWhiteSpace(expandSfxTag)) AudioManager.PlaySound(expandSfxTag);
                        customer.State = CustomerState.Expanding;
                    }
                    else { Play(customer.Spine, "idle", true); customer.State = CustomerState.Seated; }
                    break;
                case CustomerState.Expanding:
                    if (customer.Time < customer.EatDuration) break;
                    Play(customer.Spine, "expand_idle", true);
                    // 膨胀结束、开始滚离座位的同帧送客，不提前到膨胀开始，也不等出屏。
                    customer.Root.position += Vector3.right * (departingSpeed * dt);
                    customer.Root.Rotate(0f, 0f, -departingRotationSpeed * dt);
                    departingCustomers.Add(new DepartingCustomer { Root = customer.Root });
                    StartReplacement(customer);
                    break;
                case CustomerState.Arriving:
                    TickReplacement(customer, dt);
                    break;
            }
        }
    }

    private void StartReplacement(Customer customer)
    {
        // 旧 Root 已交给 departingCustomers；这里只替换席位上的新顾客引用。
        Customer fresh = CreateCustomer(customer.Seat);
        customer.Root = fresh.Root;
        customer.Spine = fresh.Spine;
        customer.MouthPoint = fresh.MouthPoint;
        customer.MouthFollower = fresh.MouthFollower;
        customer.ClothingRenderers = fresh.ClothingRenderers;
        customer.WaitingPosition = fresh.WaitingPosition;
        customer.Carrier = Instantiate(carrierTemplate, runtimeRoot);
        customer.Carrier.name = "Level140_ReplacementAlien";
        customer.Carrier.gameObject.SetActive(true);
        InitializeRig(customer.Carrier);
        customer.Root.SetParent(customer.Carrier.ClipSwingPivot, false);
        customer.Root.localPosition = gripPosition;
        customer.Root.localRotation = gripRotation;
        customer.Root.localScale = gripScale;
        Play(customer.Spine, "hang", true);
        customer.Carrier.transform.position = alienEnd;
        customer.Carrier.ClipBoneFollower?.LateUpdate();
        // 在座位上方松爪，再将视觉角色落到座位；无 NPC 物理或玩家事件副作用。
        Vector3 release = customer.WaitingPosition + Vector3.up * 1.5f;
        customer.CarrierEnd = alienEnd + release - customer.Root.position;
        customer.CarrierStart = customer.CarrierEnd + (alienStart - alienEnd);
        customer.Carrier.transform.position = customer.CarrierStart;
        customer.Time = 0f;
        customer.Dropped = false;
        customer.State = CustomerState.Arriving;
    }

    private void TickReplacement(Customer customer, float dt)
    {
        float t = Mathf.Clamp01(customer.Time / replacementEnterSeconds);
        if (!customer.Dropped)
        {
            customer.Carrier.transform.position = Vector3.Lerp(customer.CarrierStart, customer.CarrierEnd, Mathf.SmoothStep(0f, 1f, t));
            if (t < 1f) return;
            customer.Carrier.ClipBoneFollower?.LateUpdate();
            PlayClip(customer.Carrier, "clip_open");
            customer.Root.SetParent(runtimeRoot, true);
            customer.Root.rotation = Quaternion.identity;
            customer.Root.localScale = Vector3.one;
            Play(customer.Spine, "jump_down", false);
            customer.Dropped = true;
            customer.Time = 0f;
        }
        float drop = Mathf.Clamp01(customer.Time / 0.5f);
        customer.Root.position = customer.WaitingPosition + Vector3.up * (1.5f * (1f - drop * drop));
        float exit = Mathf.Clamp01(customer.Time / alienExitSeconds);
        customer.Carrier.transform.position = Vector3.Lerp(customer.CarrierEnd, customer.CarrierStart, exit);
        if (drop >= 1f) Play(customer.Spine, "idle", true, true);
        if (drop < 1f || exit < 1f) return;
        Destroy(customer.Carrier.gameObject);
        customer.Carrier = null;
        customer.Count = 0;
        customer.PendingBites = 0;
        customer.State = CustomerState.Seated;
    }

    private void BeginLoading()
    {
        state = MachineState.Loading;
        timer = 0f;
        SetProgress(loadingSprite, 0f);
    }

    private void TickMachine(float dt)
    {
        if (state == MachineState.Loading)
        {
            timer += dt;
            SetProgress(loadingSprite, Mathf.Clamp01(timer / loadSeconds));
            if (timer >= loadSeconds)
            {
                state = MachineState.Ready;
                SetProgress(readySprite, 1f); // 这里只进入待点击态，绝不自动调用 Serve。
            }
        }
        else if (state == MachineState.Broken)
        {
            TickFaultTransition(dt);
            smoke.Tick(dt);
            // 故障只停止出餐。食物耗尽后继续留在现场，等玩家自己 Reset。
        }
    }

    // 有效出餐才启动：轻放大后衰减回弹，使用本关可暂停时钟并精确恢复人工缩放。
    private void TickMachinePulse(float dt)
    {
        if (machinePulseTime < 0f) return;
        machinePulseTime += dt;
        if (machinePulseTime >= 0.36f)
        {
            machine.localScale = machineBaseScale;
            machinePulseTime = -1f;
            return;
        }
        float offset;
        if (machinePulseTime < 0.08f)
        {
            float t = machinePulseTime / 0.08f;
            offset = 0.055f * (1f - (1f - t) * (1f - t));
        }
        else
        {
            float t = (machinePulseTime - 0.08f) / 0.28f;
            offset = 0.055f * (1f - t) * (1f - t) * Mathf.Cos(2f * Mathf.PI * t);
        }
        machine.localScale = machineBaseScale * (1f + offset);
    }

    private void Serve()
    {
        if (!CanOperate || state != MachineState.Ready) return;
        int count = ChooseMealPortions();
        var plate = new Plate();
        plate.Root = new GameObject("Level140_Plate_" + (servings + 1)).transform;
        plate.Root.SetParent(runtimeRoot, false);
        // 以盘子中心为缩放中心；左右手摆样例仅作为落点和原始堆叠模板，不能缩放其根导致落点偏移。
        plate.Root.localScale = Vector3.one * mealVisualScale;
        var group = plate.Root.gameObject.AddComponent<SortingGroup>();
        group.sortingLayerID = SceneLayer;
        group.sortingOrder = plateSortingOrder;
        plate.Dish = CopySprite(dishTemplate, plate.Root, "Dish");
        plate.Dish.transform.localPosition = Vector3.zero;
        plate.Dish.sortingOrder = 0;
        Sprite chosen = foodSprites[UnityEngine.Random.Range(0, foodSprites.Length)];
        for (int i = 0; i < count; i++)
        {
            var food = CopySprite(foodTemplates[i], plate.Root, "Food_" + i);
            food.sprite = chosen;
            Vector3 offset = foodTemplates[i].transform.position - dishTemplate.transform.position;
            offset.x = 0f; // 每层都居中；保留 sw1 的层高，不保留手摆的微小 X 偏差。
            food.transform.localPosition = offset;
            food.sortingOrder = i + 1;
            plate.Foods.Add(food);
        }
        plate.Start = new Vector3(machineBody.bounds.center.x, machineBody.bounds.max.y + 0.2f, leftLanding.z);
        plate.Landing = reverse ? rightLanding : leftLanding;
        plate.Root.position = plate.Start;
        // 保留已算好的世界姿态/尺寸，仅继承 zt 的外层排序：jq_02 < 整盘 < 其余机器。
        plate.Root.SetParent(conveyor, true);
        plates.Add(plate);
        AudioManager.PlaySound("throwItem");
        if (machinePulseTime < 0f) machineBaseScale = machine.localScale;
        machinePulseTime = 0f;
        beltStarted = true;
        servings++;
        if (servings == 8)
        {
            state = MachineState.Broken;
            BeginFaultTransition();
        }
        else
        {
            // 只有有效出餐触发下一次 2.2 秒加载；食物清空和 Alien 补位不控制或重置计时。
            BeginLoading();
        }
    }

    private void BeginFaultTransition()
    {
        timer = 0f;
        faultPartColor = brokenPart.color;
        // 红条保持在底层，绿条在上层渐隐，避免两层同时淡出造成中途露空。
        faultReadyOverlay = CopySprite(progress, progress.transform, "Level140_FaultReady");
        faultReadyOverlay.transform.localPosition = Vector3.zero;
        faultReadyOverlay.transform.localScale = Vector3.one;
        faultReadyOverlay.sortingLayerID = progress.sortingLayerID;
        faultReadyOverlay.sortingOrder = 1;
        faultReadyColor = faultReadyOverlay.color;
        SetProgress(loadingSprite, 1f);
    }

    private void TickFaultTransition(float dt)
    {
        if (timer >= FaultTransitionSeconds) return;
        float previous = timer;
        timer = Mathf.Min(FaultTransitionSeconds, timer + dt);
        float t = Mathf.SmoothStep(0f, 1f, timer / FaultTransitionSeconds);
        Color partColor = faultPartColor;
        partColor.a *= 1f - t;
        brokenPart.color = partColor;
        Color readyColor = faultReadyColor;
        readyColor.a *= 1f - t;
        if (faultReadyOverlay != null) faultReadyOverlay.color = readyColor;
        // 与抛盘声错开；阈值跨越只触发一次，大步长也不会漏掉。
        if (previous < FaultSmokeDelay && timer >= FaultSmokeDelay)
        {
            smoke.Begin();
            AudioManager.PlaySound("passwordError");
        }
        if (timer < FaultTransitionSeconds) return;
        brokenPart.enabled = false;
        if (faultReadyOverlay != null) Destroy(faultReadyOverlay.gameObject);
        faultReadyOverlay = null;
    }

    private int ChooseMealPortions()
    {
        float replacement = replacementEnterSeconds + Mathf.Max(0.5f, alienExitSeconds);
        var diners = new Level140MealPlanner.Diner[customers.Count];
        for (int i = 0; i < customers.Count; i++)
        {
            Customer c = customers[i];
            float eat = Mathf.Max(BiteSeconds, c.Spine.Skeleton.Data.FindAnimation("eat").Duration);
            float expand = c.Spine.Skeleton.Data.FindAnimation("expand").Duration;
            var diner = new Level140MealPlanner.Diner
            {
                Count = c.Count + c.PendingBites,
                ReplacementAfterThirdMeal = eat + expand + replacement + MealForecastMargin
            };
            switch (c.State)
            {
                case CustomerState.Eating:
                    if (diner.Count < Level140MealPlanner.MealsPerCustomer) break;
                    float remainingEat = Mathf.Max(0f, c.EatDuration - c.Time);
                    foreach (var bite in bites)
                        if (bite.Customer == c) remainingEat = Mathf.Max(remainingEat, BiteSeconds - bite.Time);
                    diner.Returning = true;
                    diner.ReadyAt = remainingEat + expand + replacement + MealForecastMargin;
                    break;
                case CustomerState.Expanding:
                    diner.Returning = true;
                    diner.ReadyAt = Mathf.Max(0f, c.EatDuration - c.Time) + replacement + MealForecastMargin;
                    break;
                case CustomerState.Arriving:
                    diner.Returning = true;
                    diner.ReadyAt = c.Dropped
                        ? Mathf.Max(0f, Mathf.Max(0.5f, alienExitSeconds) - c.Time) + MealForecastMargin
                        : Mathf.Max(0f, replacementEnterSeconds - c.Time) + Mathf.Max(0.5f, alienExitSeconds) + MealForecastMargin;
                    break;
            }
            diners[i] = diner;
        }

        var active = new List<Level140MealPlanner.Meal>();
        foreach (Plate plate in plates)
        {
            if (plate.Foods.Count == 0) continue;
            var meal = new Level140MealPlanner.Meal { Remaining = plate.Foods.Count };
            float x = plate.Landed ? plate.Root.position.x : plate.Landing.x;
            float delay = plate.Landed ? 0f : Mathf.Max(0f, launchSeconds - plate.Flight);
            for (int seat = 0; seat < customers.Count; seat++) meal.Arrivals[seat] = MealArrival(x, customers[seat].Seat.position.x, delay);
            active.Add(meal);
        }
        float landing = reverse ? rightLanding.x : leftLanding.x;
        var arrivals = new float[3];
        for (int i = 0; i < arrivals.Length; i++) arrivals[i] = MealArrival(landing, customers[i].Seat.position.x, launchSeconds);
        int portions = Level140MealPlanner.Choose(diners, active, arrivals, Mathf.Max(0, 7 - servings), loadSeconds, out bool covered);
        if (!covered && !reportedMealForecastGap)
        {
            reportedMealForecastGap = true;
            Debug.LogWarning("[Level140] 接餐预测存在未覆盖窗口，按单份继续出餐；没有禁止玩家进食，请人工核对当前输送/补客节奏。", this);
        }
        return portions;
    }

    private float MealArrival(float fromX, float seatX, float flightDelay)
    {
        float distance;
        if (!reverse)
        {
            // 默认路线越过顾客就是漏接，预测不能借下一圈掩盖经过玩家前的剩余餐点。
            distance = fromX - seatX;
            if (distance < -0.001f) return float.PositiveInfinity;
        }
        else
        {
            distance = seatX - fromX;
            if (distance < 0f)
            {
                float left = Mathf.Min(CameraLeft() - 2f, conveyor.TransformPoint(new Vector3(beltMin, 0f, 0f)).x);
                float right = Mathf.Max(CameraRight() + 2f, conveyor.TransformPoint(new Vector3(beltMin + beltLength, 0f, 0f)).x);
                distance = right - fromX + seatX - left;
            }
        }
        return flightDelay + Mathf.Max(0f, distance) / beltSpeed;
    }

    private void TickPlates(float dt)
    {
        float displacement = (reverse ? 1f : -1f) * beltSpeed * dt;
        for (int i = plates.Count - 1; i >= 0; i--)
        {
            Plate plate = plates[i];
            if (!plate.Landed)
            {
                plate.Flight = Mathf.Min(launchSeconds, plate.Flight + dt);
                float t = plate.Flight / launchSeconds;
                plate.Root.position = Vector3.Lerp(plate.Start, plate.Landing, t) + Vector3.up * (4f * launchHeight * t * (1f - t));
                plate.Landed = t >= 1f;
                continue;
            }
            float before = plate.Root.position.x;
            plate.Root.position += Vector3.right * displacement;
            // 扫过座位而非使用距离阈值，避免低帧率越过进食触发点。
            if (displacement < 0f)
            {
                for (int seat = 3; seat >= 0; seat--) TryTake(plate, seat, before, plate.Root.position.x);
            }
            else
            {
                for (int seat = 0; seat < 4; seat++) TryTake(plate, seat, before, plate.Root.position.x);
            }
            if (plate.Foods.Count == 0 && !IsVisible(plate.Dish.bounds))
            {
                Destroy(plate.Root.gameObject);
                plates.RemoveAt(i);
                continue;
            }
            float left = Mathf.Min(CameraLeft() - 2f, conveyor.TransformPoint(new Vector3(beltMin, 0f, 0f)).x);
            float right = Mathf.Max(CameraRight() + 2f, conveyor.TransformPoint(new Vector3(beltMin + beltLength, 0f, 0f)).x);
            if (plate.Foods.Count > 0 && (plate.Root.position.x < left || plate.Root.position.x > right))
            {
                Vector3 p = plate.Root.position;
                p.x = p.x < left ? right : left;
                plate.Root.position = p;
            }
        }
    }

    private void TryTake(Plate plate, int seat, float from, float to)
    {
        if (plate.Foods.Count == 0) return;
        float x = seat == 0 ? playerSeat.position.x : customers[seat - 1].Seat.position.x;
        if (x < Mathf.Min(from, to) || x > Mathf.Max(from, to)) return;
        Customer customer = seat == 0 ? null : customers[seat - 1];
        // 玩家资格不读取转向：坐好且有餐经过就真实进食。
        if (seat == 0 && (!actor.Seated || actor.Eating || playerPendingBites > 0 ||
            playerMealState != PlayerMealState.Dining || playerMealCount >= PlayerMealsRequired)) return;
        if (customer != null && plate == playerMealPlate) return;
        if (customer != null)
        {
            // 同盘反转或绕回再次经过时仍可取食；保留原有食量和顾客阶段限制。
            if (customer.Count + customer.PendingBites >= Level140MealPlanner.MealsPerCustomer ||
                (customer.State != CustomerState.Seated && customer.State != CustomerState.Eating)) return;
        }
        if (seat == 0) playerMealPlate = plate;
        TakeFood(plate, customer);
    }

    // 共用逐份飞嘴表现；玩家只在上一份进食结束后继续领取，顾客沿用原接餐规则。
    private void TakeFood(Plate plate, Customer customer)
    {
        int top = plate.Foods.Count - 1;
        var food = plate.Foods[top];
        plate.Foods.RemoveAt(top);
        food.transform.SetParent(runtimeRoot, true);
        food.sortingLayerID = SceneLayer;
        food.sortingOrder = 205;
        var bite = new Bite { Food = food.transform, Target = customer == null ? playerMouthPoint : customer.MouthPoint,
            MouthFollower = customer == null ? playerMouthFollower : customer.MouthFollower,
            Start = food.transform.position, Scale = food.transform.localScale, Customer = customer };
        bites.Add(bite);
        if (customer != null)
        {
            if (customerBubbleOwner == customer) CloseCustomerWaitingBubble();
            customer.WaitingTime = 0f;
            customer.State = CustomerState.Eating;
            customer.Time = 0f;
            customer.PendingBites++;
            customer.EatDuration = Play(customer.Spine, "eat", false);
        }
        else
        {
            CloseCustomerWaitingBubble(); // 玩家开始累计进食后不再生成顾客气泡。
            playerPendingBites++;
            playerEatDuration = Mathf.Max(BiteSeconds, actor.BeginEating());
            playerEatTime = 0f;
            level.SetCtrlable(false);
        }
    }

    private void TickBites(float dt)
    {
        for (int i = bites.Count - 1; i >= 0; i--)
        {
            Bite bite = bites[i];
            bite.Time += dt;
            float t = Mathf.Clamp01(bite.Time / BiteSeconds);
            bite.MouthFollower.LateUpdate();
            Vector3 mouth = bite.Target.position;
            bite.Food.position = Vector3.Lerp(bite.Start, mouth, Mathf.SmoothStep(0f, 1f, t));
            float shrink = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, t));
            bite.Food.localScale = bite.Scale * (1f - shrink);
            if (t < 1f) continue;
            AudioManager.PlaySound("eat");
            if (bite.Customer != null) { bite.Customer.Count++; bite.Customer.PendingBites--; }
            else
            {
                playerMealCount++;
                playerPendingBites--;
            }
            Destroy(bite.Food.gameObject);
            bites.RemoveAt(i);
        }
    }

    private HintBubble GetMealBubbleTemplate()
    {
        if (!mealBubbleTemplateResolved)
        {
            mealBubbleTemplateResolved = true;
            mealBubbleTemplate = Resources.Load<HintBubble>("HintBubble");
            if (mealBubbleTemplate == null)
                Debug.LogWarning("[Level140] Meal bubbles skipped: HintBubble resource is missing.", this);
        }
        return mealBubbleTemplate;
    }

    private void TickCustomerWaitingBubble(float dt)
    {
        if (playerMealCount + playerPendingBites > 0)
        {
            CloseCustomerWaitingBubble();
            return;
        }
        if (customerWaitingBubble != null)
        {
            // 关闭期间仍占用唯一顾客气泡槽，等共享组件完成动画并自行销毁。
            if (customerBubbleClosing) return;
            if (customerBubbleOwner == null || customerBubbleOwner.State != CustomerState.Seated)
            {
                CloseCustomerWaitingBubble();
                return;
            }
            customerBubbleTime += dt;
            if (customerBubbleTime >= CustomerBubbleDisplaySeconds) CloseCustomerWaitingBubble();
            return;
        }
        if (customerBubbleClosing || customerBubbleOwner != null)
        {
            customerBubbleClosing = false;
            customerBubbleOwner = null;
            customerBubbleCooldown = UnityEngine.Random.Range(CustomerBubbleMinInterval, CustomerBubbleMaxInterval);
            return;
        }
        customerBubbleCooldown = Mathf.Max(0f, customerBubbleCooldown - dt);
        if (customerBubbleCooldown > 0f || customerWaitingBubbleKeys == null || customerWaitingBubbleKeys.Length == 0) return;

        Customer selected = null;
        int eligible = 0;
        foreach (Customer customer in customers)
        {
            if (customer.State != CustomerState.Seated || customer.WaitingTime < CustomerWaitBeforeBubble) continue;
            // 等概率选择一位符合条件的顾客，不为每位顾客各开一个气泡。
            if (UnityEngine.Random.Range(0, ++eligible) == 0) selected = customer;
        }
        if (selected == null) return;
        HintBubble template = GetMealBubbleTemplate();
        if (template == null) return;

        int count = customerWaitingBubbleKeys.Length;
        bool excludeLast = count > 1 && lastCustomerBubbleLine >= 0 && lastCustomerBubbleLine < count;
        int line = UnityEngine.Random.Range(0, excludeLast ? count - 1 : count);
        if (excludeLast && line >= lastCustomerBubbleLine) line++;
        string key = customerWaitingBubbleKeys[line];
        if (string.IsNullOrWhiteSpace(key)) return;

        customerWaitingBubble = Instantiate(template, runtimeRoot);
        // 共享 show 动画从零缩放开始；先设初态，避免 Animator 首次求值前露出完整气泡。
        customerWaitingBubble.transform.localScale = Vector3.zero;
        customerBubbleOwner = selected;
        customerBubbleTime = 0f;
        lastCustomerBubbleLine = line;
        // 顾客保留本关 1.5 秒显示计时；气泡表现、关闭动画和销毁仍由共享组件处理。
        customerWaitingBubble.Show(key, selected.Root, 0f);
    }

    private void CloseCustomerWaitingBubble()
    {
        if (customerWaitingBubble == null || customerBubbleClosing) return;
        customerBubbleClosing = true;
        customerWaitingBubble.CloseStraightly();
    }

    private void TickBelt(float dt)
    {
        if (!beltStarted) return;
        beltOffset += (reverse ? 1f : -1f) * beltSpeed * dt / Mathf.Abs(conveyor.lossyScale.x);
        for (int i = 0; i < beltTiles.Count; i++)
        {
            Vector3 p = beltTiles[i].localPosition;
            p.x = beltMin + Mathf.Repeat(tileStarts[i] - beltMin + beltOffset, beltLength);
            beltTiles[i].localPosition = p;
        }
    }

    private void TryAutoSeat()
    {
        if (!CanAutoSeat) return;
        level.SetCtrlable(false);
        actor.PlaySit(playerSeat, waitingPositions[playerSeat] - playerSeat.position);
    }

    private void TickInput()
    {
        if (!CanOperate) { ClearPointer(); return; }
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Began && finger == -1 && !dragging && !machinePress && !lightPress) BeginPointer(touch.position, touch.fingerId);
                if (touch.fingerId != finger) continue;
                if (touch.phase == TouchPhase.Canceled) ClearPointer();
                else if (touch.phase == TouchPhase.Ended) EndPointer(touch.position);
                else MovePointer(touch.position);
            }
            return;
        }
        if (finger >= 0) { ClearPointer(); return; }
        if (Input.GetMouseButtonDown(0)) BeginPointer(Input.mousePosition, -1);
        if (Input.GetMouseButton(0)) MovePointer(Input.mousePosition);
        if (Input.GetMouseButtonUp(0)) EndPointer(Input.mousePosition);
    }

    private void BeginPointer(Vector2 screen, int id)
    {
        if (OverUI(screen)) return;
        Vector2 world = ToWorld(screen);
        lightPress = lighting.HitSwitch(world);
        bool hitMachine = !lightPress && Contains2D(machineBody.bounds, world);
        // 加载期间按下后一直按住到 Ready 也不算新点击，不积压出餐请求。
        machinePress = hitMachine && state == MachineState.Ready;
        dragging = !lightPress && !hitMachine && !reverse && beltStarted && InBelt(world);
        if (!machinePress && !dragging && !lightPress) return;
        pointerStart = world;
        finger = id;
    }

    private void MovePointer(Vector2 screen)
    {
        if (dragging && ToWorld(screen).x - pointerStart.x >= reverseDragDistance)
        {
            reverse = true;
            dragging = false; // 一次向右净拖动达标；不累计多次手势，不重复翻转。
        }
    }

    private void EndPointer(Vector2 screen)
    {
        Vector2 world = ToWorld(screen);
        bool tap = !OverUI(screen) && Vector2.Distance(pointerStart, world) < 0.35f;
        bool toggleLight = lightPress && tap && lighting.HitSwitch(world);
        bool click = machinePress && tap && Contains2D(machineBody.bounds, world);
        ClearPointer();
        if (toggleLight)
        {
            lighting.Toggle();
            AudioManager.PlaySound("fanSwitch");
        }
        else if (click) Serve();
    }

    private bool OverUI(Vector2 screen)
    {
        if (EventSystem.current == null) return false;
        uiHits.Clear();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, uiHits);
        foreach (var hit in uiHits) if (hit.module is GraphicRaycaster) return true;
        return false;
    }

    private bool InBelt(Vector2 world)
    {
        foreach (var tile in beltTiles)
        {
            Bounds bounds = tile.GetComponent<SpriteRenderer>().bounds;
            bounds.Expand(new Vector3(0f, 0.65f, 0f));
            if (Contains2D(bounds, world)) return true;
        }
        return false;
    }

    private void SetProgress(Sprite sprite, float amount)
    {
        progress.sprite = sprite;
        progress.transform.localScale = new Vector3(progressScale.x * amount, progressScale.y, progressScale.z);
        progress.transform.localPosition = progressPosition + Vector3.left * (sprite.bounds.size.x * progressScale.x * (1f - amount) * 0.5f);
    }

    private void Win()
    {
        if (stopped || state == MachineState.Finished || playerMealState != PlayerMealState.WaitingForSuccess ||
            playerMealCount < PlayerMealsRequired || playerPendingBites != 0 || playerEatTime < 0.5f) return;
        state = MachineState.Finished;
        StopRuntime();
        level.SuccessStrightly();
    }

    private void OnStateChanged(SwitchGameStateEvent evt)
    {
        if (evt.NewState == GamePlayStateName.SUCCESS || evt.NewState == GamePlayStateName.FAILED) StopRuntime();
        else if (evt.NewState == GamePlayStateName.PAUSE) SetSuspended(true);
    }

    private static PlayCtrl CurrentPlay => Singleton<PlayCtrl>.IsInitialized ? Singleton<PlayCtrl>.Instance : null;

    private static bool IsPlaying()
    {
        var play = CurrentPlay;
        // 与邻近关卡一致：人工直接打开场景时不主动创建全局 PlayCtrl。
        return play == null || !play.IsInitialized || (play.IsActive && play.CheckState(GamePlayStateName.PLAYING));
    }

    private void SetSuspended(bool value)
    {
        actor?.SetFrozen(value);
        if (value)
        {
            foreach (var skeleton in GetComponentsInChildren<SkeletonAnimation>(true))
            {
                if (pausedAnimations.ContainsKey(skeleton)) continue;
                pausedAnimations[skeleton] = skeleton.timeScale;
                skeleton.timeScale = 0f;
            }
        }
        else if (suspended)
        {
            foreach (var pair in pausedAnimations) if (pair.Key != null) pair.Key.timeScale = pair.Value;
            pausedAnimations.Clear();
        }
        suspended = value;
    }

    private void StopRuntime()
    {
        stopped = true;
        playerMealPlate = null;
        if (machinePulseTime >= 0f && machine != null) machine.localScale = machineBaseScale;
        machinePulseTime = -1f;
        CloseCustomerWaitingBubble();
        lighting?.Hide();
        ClearPointer();
        level?.SetCtrlable(false);
        SetSuspended(true);
    }

    private void OnDisable()
    {
        StopRuntime();
        EventsUtils.ResetEvents(ref events);
        // 餐盘为排序归入手摆转台，不再是 runtimeRoot 子级，需要单独回收。
        foreach (Plate plate in plates)
            if (plate.Root != null) Destroy(plate.Root.gameObject);
        plates.Clear();
        if (playerOcclusion != null) Destroy(playerOcclusion);
        if (runtimeRoot != null) Destroy(runtimeRoot.gameObject);
        if (progress != null) Destroy(progress.gameObject);
        if (smoke != null) Destroy(smoke.gameObject);
    }

    private void OnDestroy()
    {
        EventsUtils.ResetEvents(ref events);
    }

    private void ConfigurationError(string message)
    {
        Debug.LogError("[Level140] " + message, this);
        StopRuntime();
    }

    private void ClearPointer() { finger = -1; dragging = false; machinePress = false; lightPress = false; }
    private Vector2 ToWorld(Vector2 screen) => gameCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -gameCamera.transform.position.z));
    private float CameraLeft() => gameCamera.ViewportToWorldPoint(new Vector3(0f, 0.5f, -gameCamera.transform.position.z)).x;
    private float CameraRight() => gameCamera.ViewportToWorldPoint(new Vector3(1f, 0.5f, -gameCamera.transform.position.z)).x;
    private static bool Contains2D(Bounds b, Vector2 p) => p.x >= b.min.x && p.x <= b.max.x && p.y >= b.min.y && p.y <= b.max.y;
    private bool IsVisible(Bounds bounds)
    {
        Vector3 min = gameCamera.WorldToViewportPoint(bounds.min);
        Vector3 max = gameCamera.WorldToViewportPoint(bounds.max);
        return max.x >= 0f && min.x <= 1f && max.y >= 0f && min.y <= 1f;
    }

    private static SpriteRenderer NewSprite(string name, Transform parent, Sprite sprite, int order)
    {
        var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
        renderer.transform.SetParent(parent, false);
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return renderer;
    }

    private static SpriteRenderer CopySprite(SpriteRenderer source, Transform parent, string name)
    {
        var copy = NewSprite(name, parent, source.sprite, source.sortingOrder);
        copy.sharedMaterial = source.sharedMaterial;
        copy.color = source.color;
        copy.transform.localScale = source.transform.lossyScale;
        return copy;
    }

    private static float Play(SkeletonAnimation skeleton, string animation, bool loop, bool keepCurrent = false)
    {
        if (keepCurrent && skeleton.AnimationState.GetCurrent(0)?.Animation.Name == animation) return 0f;
        return skeleton.AnimationState.SetAnimation(0, animation, loop).Animation.Duration;
    }

    private static void PlayClip(DrawAlienClipBallRigView rig, string animation)
    {
        rig.ClipSkeletonAnimation.Initialize(false);
        rig.ClipSkeletonAnimation.AnimationState.SetAnimation(0, animation, false);
    }

    private static void InitializeRig(DrawAlienClipBallRigView rig)
    {
        rig.ResolveReferences();
        rig.AlienSkeletonAnimation.Initialize(false);
        if (rig.ClipBoneFollower != null)
        {
            rig.ClipBoneFollower.SkeletonRenderer = rig.AlienSkeletonAnimation;
            rig.ClipBoneFollower.Initialize();
            rig.ClipBoneFollower.LateUpdate();
        }
        PlayClip(rig, "clip_close");
    }
}
