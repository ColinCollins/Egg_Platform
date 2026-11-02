using System;
using System.Collections.Generic;
using System.Globalization;
using Bear.EventSystem;
using DG.Tweening;
using Game.Events;
using Game.ItemEvent;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using I2.Loc;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 141 独立种植循环。手工南瓜只作成熟样板；不写共享 Actor、门、输入或存档。
/// 所有本关飞行/生长使用同一个可挂起时钟；弧线与 ItemJumpToListener 完全同式。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level141Ctrl : MonoBehaviour
{
    /// <summary>
    /// 一个可播种位置的编辑器绑定和运行时状态容器。
    /// 只有 <see cref="root"/>、<see cref="pumpkin"/> 和 <see cref="leaves"/> 需要在 Prefab 中绑定；
    /// 其余状态由本控制器在每次关卡实例生命周期内维护，不应由其它关卡脚本直接修改。
    /// </summary>
    [Serializable]
    public sealed class PlantingSite
    {
        /// <summary>当前种植位的根节点，用于排序和保持编辑器布局。</summary>
        public Transform root;

        /// <summary>该种植位的成熟南瓜样板 Renderer，同时作为运行时成熟视觉的复原基准。</summary>
        public SpriteRenderer pumpkin;

        /// <summary>该种植位的叶子样板 Renderer；成熟后由本关控制器独立渐隐。</summary>
        public SpriteRenderer leaves;

        // 以下状态只由 Level141Ctrl 在当前关卡实例内维护，不作为 Prefab 输入或跨关卡接口。
        [NonSerialized] public bool occupied;
        [NonSerialized] public bool mature;
        [NonSerialized] public bool collected;
        [NonSerialized] public bool throwing;
        [NonSerialized] public bool blockActorPickupUntilExit;
        [NonSerialized] public bool falling;
        [NonSerialized] public bool releasePending;
        [NonSerialized] public bool collectionVisualRaised;
        [NonSerialized] public Transform collectionParent;
        [NonSerialized] public int collectionSiblingIndex;
        [NonSerialized] public int collectionSortingLayer;
        [NonSerialized] public int collectionSortingOrder;
        [NonSerialized] public float growTime;
        [NonSerialized] public float fallSpeed;
        [NonSerialized] public int stage;
        [NonSerialized] public int growthStartedFrame;
        [NonSerialized] public Vector3 initialPosition;
        [NonSerialized] public Vector3 initialScale;
        [NonSerialized] public Vector3 initialLeafPosition;
        [NonSerialized] public Vector3 initialLeafScale;
        [NonSerialized] public Vector3 growthBase;
        [NonSerialized] public Vector3 leafGrowthBase;
        [NonSerialized] public Quaternion initialRotation;
        [NonSerialized] public Color initialColor;
        [NonSerialized] public Color leafColor;

        // 以下引用和缓存 Renderer 由控制器创建、更新和销毁，不应由外部组件持有或复用。
        [NonSerialized] public SpriteRenderer crop;
        [NonSerialized] public SpriteRenderer nextCrop;
        [NonSerialized] public Level141ProgressView progress;
    }

    [Header("Scene bindings (keep the authored layout)")]
    // 关卡根控制器；只读取共享主流程状态，不在本脚本中重建或替换共享关卡。
    [SerializeField] private BaseLevelCtrl level;
    // 本关实际使用的 GameCamera，负责读取视口并执行仅限 X 轴的本关跟随。
    [SerializeField] private Transform cameraRoot;
    // 右侧 FallArea 的 BoxCollider2D，用于限制本关相机右侧可见边界。
    [SerializeField] private Transform rightFallArea;
    // Actor 中心相对相机目标的横向偏移；141 的约定值为 0，不前置镜头。
    [SerializeField] private float cameraHorizontalOffset = 0f;
    // 本关相机横向 SmoothDamp 的时间参数；不改变相机纵向开局构图。
    [SerializeField, Min(0f)] private float cameraHorizontalSmoothTime = 0.12f;
    // 盒子交互入口的场景对象；实际触发仍由 Prefab 中的本关事件链负责。
    [SerializeField] private Transform seedBox;
    // 盒子的可弹跳视觉子节点；只移动视觉，不移动盒子碰撞体。
    [SerializeField] private Transform boxVisual;
    // 十个固定种植位，初始化时按成熟南瓜世界 X 从左到右排序。
    [SerializeField] private PlantingSite[] sites;
    // Farmer 的编辑器样板；运行时会复制为本关拥有的视觉实例。
    [SerializeField] private Transform farmer;
    // 本关出口门根节点；钥匙解锁后的标准门事件仍由共享组件处理。
    [SerializeField] private Transform door;
    // 停用的共享 Key/DoorLock 模板；运行时成组复制以隔离本关引用。
    [SerializeField] private Transform sharedKeyLockTemplate;
    // Farmer 需求气泡 Prefab；只在本关运行根下创建实例，不修改共享气泡资源。
    [SerializeField] private HintBubble bubblePrefab;
    // 相机下显示收集数量和超越百分比的世界空间 TMP 文本。
    [SerializeField] private TextMeshPro collectedText;
    // 与 collectedText 配套的 I2 Localize 组件，用于语言切换和参数刷新。
    [SerializeField] private Localize collectedLocalize;
    // Farmer 气泡数字使用的独立字体；避免随 HUD 字体设置连带改变。
    [SerializeField] private TMP_FontAsset worldNumberFont;

    [Header("141 sprites: seed / sprout / grass / flower")]
    // 按种子、芽、草、花顺序绑定的四张本关生长/种子 Sprite。
    [SerializeField] private Sprite[] growthSprites;
    // 南瓜摔碎后按左、右、前顺序生成的三张碎片 Sprite。
    [SerializeField] private Sprite[] fragmentSprites;
    // 生长条和盒子冷却条共用的灰色背景 Sprite。
    [SerializeField] private Sprite progressBackground;
    // 生长条和盒子冷却条共用的绿色填充 Sprite。
    [SerializeField] private Sprite progressFill;
    // 生长条和盒子冷却条共用的透明边框 Sprite。
    [SerializeField] private Sprite progressFrame;
    // 本关运行时创建 SpriteRenderer 使用的材质；不修改共享材质资产。
    [SerializeField] private Material spriteMaterial;

    [Header("Timing and feedback")]
    // 单个生长阶段的时长；芽、草、花各阶段均使用此值。
    [SerializeField, Min(0.1f)] private float stageSeconds = 5f;
    // 每次有效出种后的冷却上限；只增加本局等待时长，不跨局保存。
    [SerializeField, Min(1f)] private float boxCooldownSeconds = 60f;
    // 第一颗种子落地或入土后的等待时长；每个新关卡实例从此值开始。
    [SerializeField, Min(1f)] private float boxFirstCooldownSeconds = 10f;
    // 每次有效出种完成后，下次冷却增加的秒数；等待中误触不增加。
    [SerializeField, Min(0f)] private float boxCooldownStepSeconds = 5f;
    // 种子从盒子或碎瓜落点飞向目标的基础时长。
    [SerializeField, Min(0.1f)] private float seedFlightSeconds = 0.6f;
    // 南瓜飞向 Farmer 或 HUD 时使用的世界距离速度。
    [SerializeField, Min(0.1f)] private float collectSpeed = 15f;
    // 南瓜收集飞行抛物线的世界空间弧高。
    [SerializeField, Min(0f)] private float collectArcHeight = 2.5f;
    // 任意方向拖开南瓜被认定为有效摔碎操作所需的最小世界距离。
    [SerializeField, Min(0.1f)] private float dragThreshold = 0.65f;
    // 南瓜本体切换为碎片视觉的过渡时长。
    [SerializeField, Min(0.01f)] private float fragmentSplitSeconds = 0.24f;
    // 碎片落地时的轻微回弹高度，不改变碎片最终落点。
    [SerializeField, Min(0f)] private float fragmentBounceHeight = 0.1f;
    // 碎片保持完整可见后开始渐隐前的停留时长。
    [SerializeField, Min(0f)] private float fragmentHoldSeconds = 0.6f;
    // 碎片从完整可见到销毁的渐隐时长。
    [SerializeField, Min(0.1f)] private float fragmentFadeSeconds = 0.6f;
    // 碎瓜种子以碎裂点为中心向左右散开的相邻落点间距。
    [SerializeField, Min(0.1f)] private float shatteredSeedSpacing = 0.6f;
    // Farmer 收满后向左离场的移动速度。
    [SerializeField, Min(0.1f)] private float farmerWalkSpeed = 2f;
    // 射线寻找种子和碎片落点时允许作为地面的层。
    [SerializeField] private LayerMask groundLayers = (1 << 6) | (1 << 8);

    private sealed class Seed
    {
        public SpriteRenderer view;
        public bool landed;
        public bool picked;
        public bool assigned;
    }

    // No independent timers: pause, terminal state and disable stop every local motion together.
    private sealed class Motion
    {
        public float elapsed;
        public float duration;
        public Action<float> step;
        public Action complete;
    }

    private const string CollectedTextFallback =
        "已收集 <link=\"Level141CollectedNumber\">{0}</link> 个 <sprite index=0 tint=0>\n" +
        "已超越 <link=\"Level141PercentNumber\">{1}</link>% 的玩家";
    // 保持原共享气泡实例的跟随偏移；纯图标和数字不再借用本地化文字启动气泡。
    private static readonly Vector3 FarmerDemandOffset = new Vector3(-0.4f, 1f, 0f);
    private const int GrowthStageCount = 3; // Sprout / grass / flower; seed is only a travelling item.
    private const float GrowthTransitionSeconds = 0.25f;
    private const int CollectionSortingOrder = 320; // Above the demand bubble (300) and HUD (310).
    private const int SeedSortingOrder = 220; // Above all three fragments (215-217), below progress bars (260).
    private const float BoxBounceHeight = 0.18f;

    // Exact user-supplied count -> percentile table. Index 0 is the empty-session display.
    // Do not interpolate, fit a curve or substitute planting/delivery counts for collection.
    private static readonly int[] SurpassedPercentByCount =
    {
        0,
        10, 18, 26, 33, 40, 45, 49, 53, 57, 60, // 1-10
        63, 65, 67, 69, 70, 71, 72, 72, 73, 73, // 11-20
        73, 74, 74, 74, 75, 75, 75, 76, 76, 76, // 21-30
        76, 77, 77, 77, 77, 78, 78, 78, 78, 78, // 31-40
        79, 79, 79, 79, 79, 79, 80, 80, 80, 80, // 41-50
        80, 80, 81, 81, 81, 81, 81, 81, 82, 82, // 51-60
        82, 82, 82, 82, 83, 83, 83, 83, 83, 83, // 61-70
        84, 84, 84, 84, 84, 84, 85, 85, 85, 85, // 71-80
        85, 85, 86, 86, 86, 86, 86, 86, 87, 87, // 81-90
        87, 87, 87, 87, 88, 88, 88, 88, 88, 88, // 91-100
        89, 89, 89, 89, 89, 89, 90, 90, 90, 90, // 101-110
        90, 90, 91, 91, 91, 91, 91, 91, 92, 92, // 111-120
        92, 92, 92, 92, 93, 93, 93, 93, 93, 93, // 121-130
        94, 94, 94, 94, 94, 94, 95, 95, 95, 95, // 131-140
        95, 95, 96, 96, 96, 96, 96, 96, 97, 97, // 141-150
        97, 97, 97, 97, 98, 98, 98, 98, 98, 98, // 151-160
        99, 99, 99, 99, 99, 99                  // 161-166; all later counts stay at 99.
    };

    private sealed class CollectionFlight
    {
        public PlantingSite site;
        public bool giveToFarmer;
        public Vector3 startPosition;
        public Vector3 startScale;
        public Vector3 startWorldSize;
        public Vector3 destination;
        public Vector2 iconSize;
        public float elapsed;
        public float duration;
    }

    private readonly List<Seed> seeds = new List<Seed>();
    private readonly List<Motion> motions = new List<Motion>();
    private readonly List<CollectionFlight> collectionFlights = new List<CollectionFlight>();
    private readonly List<GameObject> ownedObjects = new List<GameObject>();
    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[32];
    private EventSubscriber subscriber;
    private Transform runtimeRoot;
    private Camera worldCamera;
    private BoxCollider2D cameraRightBoundary;
    private Vector3 cameraInitialPosition;
    private float cameraLeftEdge;
    private float cameraHorizontalVelocity;
    private bool cameraLeftEdgeCached;
    private Collider2D actorCollider;
    private BoxCollider2D doorTrigger;
    private Level141ProgressView cooldown;
    private HintBubble demandBubble;
    private TextMeshPro demandCount;
    private Level141HudNumberFeedback hudNumberFeedback;
    private bool collectedFormatWarningLogged;
    private Transform sharedKeyLock;
    private Transform rewardKey;
    private BoxCollider2D rewardKeyPickup;
    private MoveFloatHandle rewardKeyFloat;
    private SkeletonAnimation farmerSkeleton;
    private Transform authoredFarmer;
    private Vector3 boxProgressPosition;
    private Vector3 boxVisualStart;
    private float cooldownRemaining;
    private float cooldownDuration;
    private float nextCooldownSeconds;
    private float cooldownFeedback;
    private bool boxSeedInFlight;
    private int collected;
    private int delivered;
    private int deliveriesReserved;
    private bool farmerLeaving;
    private bool initialized;
    private bool retiring;
    private bool cleanupComplete;
    private bool paused;
    private bool terminal;
    private bool wasActive;
    private bool hudIconWarningLogged;
    private PlantingSite dragged;
    private int pointerId = int.MinValue;
    private Vector3 dragStartWorld;
    private Vector3 pumpkinStartWorld;
    private bool committedDrag;

    private bool CanPlay => initialized && !paused && !terminal && level != null &&
        level.Actor != null && !level.Actor.IsDied && !level.IsFail() && !level.IsSuccess() &&
        level.ActorCtrlable && (PlayCtrl.Instance == null || PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING));

    private void Awake()
    {
        // Prefab samples are editor layout references, never the runtime opening state.
        HideAuthoredPumpkins();
    }

    private void HideAuthoredPumpkins()
    {
        if (sites == null) return;
        foreach (var site in sites)
        {
            if (site == null) continue;
            if (site.pumpkin != null) site.pumpkin.enabled = false;
            if (site.leaves != null) site.leaves.enabled = false;
        }
    }

    private void OnEnable()
    {
        if (retiring) return;
        EventsUtils.ResetEvents(ref subscriber);
        subscriber.Subscribe<GamePauseEvent>(OnPause);
        subscriber.Subscribe<GameResumeEvent>(OnResume);
        subscriber.Subscribe<GameResetEvent>(OnReset);
        subscriber.Subscribe<SwitchGameStateEvent>(OnState);
        subscriber.Subscribe<OnTriggerTrapEvent>(OnTrap);
        subscriber.Subscribe<OnTriggerFailAreaEvent>(OnFallArea);
    }

    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (initialized || retiring) return;
        if (level == null || cameraRoot == null || seedBox == null || farmer == null || door == null ||
            sharedKeyLockTemplate == null || bubblePrefab == null || collectedText == null || collectedLocalize == null ||
            progressBackground == null || progressFill == null || progressFrame == null ||
            sites == null || sites.Length != 10 || growthSprites == null || growthSprites.Length != 4 ||
            fragmentSprites == null || fragmentSprites.Length != 3)
        {
            Debug.LogError("[Level141] Incomplete scene bindings; planting and door remain locked.", this);
            return;
        }
        foreach (var site in sites)
            if (site.root == null || site.pumpkin == null || site.leaves == null) return;
        foreach (var sprite in growthSprites) if (sprite == null) return;
        foreach (var sprite in fragmentSprites) if (sprite == null) return;
        Transform lockPoint = door.Find("LockPoint");
        if (lockPoint == null)
        {
            Debug.LogError("[Level141] The exit door must provide its standard LockPoint.", this);
            return;
        }
        Transform templateKey = sharedKeyLockTemplate.Find("Key");
        if (templateKey == null || sharedKeyLockTemplate.Find("DoorLock") == null ||
            templateKey.GetComponent<BoxCollider2D>() == null || templateKey.GetComponent<MoveFloatHandle>() == null)
        {
            Debug.LogError("[Level141] Assign the wired shared Key / DoorLock template.", this);
            return;
        }
        doorTrigger = door.GetComponent<BoxCollider2D>();
        if (doorTrigger == null)
        {
            Debug.LogError("[Level141] The exit door must provide its standard trigger collider.", this);
            return;
        }
        doorTrigger.enabled = false;

        paused = terminal = false;
        collected = delivered = deliveriesReserved = 0;
        hudIconWarningLogged = false;
        cooldownRemaining = cooldownFeedback = 0f;
        cooldownDuration = nextCooldownSeconds = Mathf.Min(boxFirstCooldownSeconds, boxCooldownSeconds);
        boxSeedInFlight = false;
        farmerLeaving = false;
        runtimeRoot = new GameObject("Level141Runtime").transform;
        runtimeRoot.SetParent(transform, false);
        worldCamera = cameraRoot.GetComponent<Camera>();
        cameraInitialPosition = cameraRoot.position;
        cameraLeftEdgeCached = false;
        cameraHorizontalVelocity = 0f;
        cameraRightBoundary = rightFallArea != null ? rightFallArea.GetComponent<BoxCollider2D>() : null;
        if (cameraRightBoundary == null)
            Debug.LogError("[Level141] Assign the right FallArea to enable bounded camera following.", this);
        actorCollider = level.Actor != null ? level.Actor.GetComponent<Collider2D>() : null;
        Array.Sort(sites, (a, b) => SiteCenter(a).x.CompareTo(SiteCenter(b).x));
        foreach (var site in sites)
        {
            site.initialPosition = site.pumpkin.transform.localPosition;
            site.initialScale = site.pumpkin.transform.localScale;
            site.initialLeafPosition = site.leaves.transform.localPosition;
            site.initialLeafScale = site.leaves.transform.localScale;
            site.initialRotation = site.pumpkin.transform.localRotation;
            site.initialColor = site.pumpkin.color;
            site.leafColor = site.leaves.color;
            site.occupied = site.mature = site.collected = site.falling = site.throwing = false;
            site.blockActorPickupUntilExit = false;
            site.releasePending = false;
            site.pumpkin.enabled = site.leaves.enabled = false;
            site.progress = new Level141ProgressView(runtimeRoot, progressBackground, progressFill, progressFrame,
                worldNumberFont != null ? worldNumberFont : collectedText.font, false, 1.65f);
            // Keep horizontal centring, but lift the bar clear of every growth stage.
            site.progress.Root.position = GrowthProgressPosition(site);
        }
        cooldown = new Level141ProgressView(runtimeRoot, progressBackground, progressFill, progressFrame,
            worldNumberFont != null ? worldNumberFont : collectedText.font, false, 1.65f);
        cooldown.Root.name = "SeedBoxCooldown";
        boxProgressPosition = seedBox.position + Vector3.up * 1.1f;
        cooldown.Root.position = boxProgressPosition;
        if (boxVisual != null) boxVisualStart = boxVisual.localPosition;

        // Clone the wired pair together so the Key's event references resolve to this runtime lock.
        // The door trigger is outside the pair and remains the same level-owned target.
        sharedKeyLockTemplate.gameObject.SetActive(false);
        sharedKeyLock = Instantiate(sharedKeyLockTemplate, runtimeRoot, false);
        sharedKeyLock.name = "Level141SharedKeyLock";
        ownedObjects.Add(sharedKeyLock.gameObject);
        Transform sharedLock = sharedKeyLock.Find("DoorLock");
        // Keep Key outside the door's SortingGroup, while the lock uses the standard door parent.
        sharedLock.SetParent(door, false);
        ownedObjects.Add(sharedLock.gameObject);
        sharedLock.localPosition = lockPoint.localPosition;
        sharedLock.localRotation = lockPoint.localRotation;
        rewardKey = sharedKeyLock.Find("Key");
        rewardKey.gameObject.SetActive(false);
        rewardKeyPickup = rewardKey.GetComponent<BoxCollider2D>();
        rewardKeyFloat = rewardKey.GetComponent<MoveFloatHandle>();
        sharedKeyLock.gameObject.SetActive(true);

        authoredFarmer = farmer;
        // The authored skin remains a preview template; the visual-only runtime copy can be destroyed.
        farmer = Instantiate(authoredFarmer, runtimeRoot, true);
        farmer.name = "Level141Farmer";
        var actorSorting = level.Actor.GetComponent<SortingGroup>();
        var farmerSorting = farmer.GetComponent<SortingGroup>();
        if (actorSorting != null && farmerSorting != null)
        {
            farmerSorting.sortingLayerID = actorSorting.sortingLayerID;
            farmerSorting.sortingOrder = actorSorting.sortingOrder - 1;
        }
        authoredFarmer.gameObject.SetActive(false);
        farmerSkeleton = farmer.GetComponentInChildren<SkeletonAnimation>(true);
        // Preserve 074's held-hoe appearance: idle hides the hoe's Spine slot.
        PlayFarmerAnimation("weeding");
        BuildDemandBubble();
        hudNumberFeedback = new Level141HudNumberFeedback(collectedText);
        initialized = true;
        RefreshCollectedText();
        SetVisualClock(false);
    }

    private void BuildDemandBubble()
    {
        demandBubble = Instantiate(bubblePrefab, runtimeRoot);
        demandBubble.name = "Level141FarmerDemand";
        // 只复用气泡外观和关闭方法，不调用 Show，也不为隐藏文字保留第二个 I2 key。
        // 禁用共享跟随 Update，由本关跟随 Farmer；CloseStraightly 仍可正常调用。
        demandBubble.enabled = false;
        demandBubble.transform.position = farmer.position + FarmerDemandOffset;
        // This is a persistent demand display, not a newly appearing hint. Skip only the intro.
        var bubbleAnimator = demandBubble.GetComponent<Animator>();
        if (bubbleAnimator != null && bubbleAnimator.runtimeAnimatorController != null)
        {
            bubbleAnimator.SetBool("show", true);
            bubbleAnimator.Play("Base Layer.idle", 0, 0f);
            bubbleAnimator.Update(0f);
        }
        demandBubble.transform.localScale = bubblePrefab.transform.localScale;
        var originalText = demandBubble.GetComponentInChildren<TextMeshPro>(true);
        originalText.gameObject.SetActive(false);
        var panel = demandBubble.transform.Find("GameObject/Bubble");
        // Keep icon and number under the shared animated content, including the close animation.
        var icon = CreateSprite("PumpkinDemandIcon", sites[0].pumpkin.sprite, panel, 2);
        icon.transform.localPosition = new Vector3(-0.75f, 0.08f, -0.02f);
        // Keep the previous 1.2 factor and enlarge it by another 1.2 (1.44 total), still as a
        // uniform XYZ multiplier on the sprite-width fit, never native scale 1 or absolute 1.44.
        icon.transform.localScale = Vector3.one * (0.65f * 1.5f * 1.2f * 1.2f / Mathf.Max(icon.sprite.bounds.size.x, 0.01f));
        demandCount = Instantiate(collectedText, panel);
        demandCount.name = "RemainingPumpkins";
        var localize = demandCount.GetComponent<Localize>();
        if (localize != null) { localize.enabled = false; Destroy(localize); }
        if (worldNumberFont != null)
        {
            demandCount.font = worldNumberFont;
            demandCount.fontSharedMaterial = worldNumberFont.material;
        }
        demandCount.color = new Color(0.33f, 0.38f, 0.44f, 1f);
        demandCount.transform.localRotation = Quaternion.identity;
        demandCount.transform.localScale = Vector3.one * 1.5f;
        demandCount.rectTransform.pivot = Vector2.one * 0.5f;
        demandCount.rectTransform.sizeDelta = new Vector2(1f, 0.8f);
        // Reserve a separate right-hand number column instead of inheriting the HUD's left alignment.
        demandCount.transform.localPosition = new Vector3(0.8f, 0.08f, -0.03f);
        demandCount.alignment = TextAlignmentOptions.Center;
        demandCount.margin = Vector4.zero;
        demandCount.fontSize = 5;
        demandCount.text = "10";
        demandCount.GetComponent<MeshRenderer>().sortingOrder = 3;
        var sorting = demandCount.GetComponent<SortingGroup>();
        if (sorting != null) sorting.sortingOrder = 3;
    }

    private void Update()
    {
        bool active = CanPlay;
        if (active != wasActive) SetVisualClock(active);
        if (!active)
        {
            CancelPointer();
            return;
        }
        float dt = Time.deltaTime;
        TickCooldown(dt);
        // Landing callbacks start a fresh 60s cooldown; do not subtract this frame twice.
        TickMotions(dt);
        TickSeeds();
        TickPlants(dt);
        TickPointer();
        TickActorHarvest();
        TickFarmer(dt);
    }

    private void LateUpdate()
    {
        if (demandBubble != null && farmer != null)
            demandBubble.transform.position = farmer.position + FarmerDemandOffset;
        if (!CanPlay)
        {
            cameraHorizontalVelocity = 0f;
            return;
        }
        UpdateCameraFollow();
        // HUD glyph world positions must be sampled after the camera has moved this frame.
        TickCollectionFlights(Time.deltaTime);
        hudNumberFeedback?.Tick(Time.deltaTime);
    }

    private void UpdateCameraFollow()
    {
        if (worldCamera == null || !worldCamera.orthographic ||
            cameraRightBoundary == null || !cameraRightBoundary.isActiveAndEnabled)
        {
            cameraHorizontalVelocity = 0f;
            return;
        }

        float halfWidth = worldCamera.orthographicSize * worldCamera.aspect;
        if (halfWidth <= 0f) return;
        if (!cameraLeftEdgeCached)
        {
            // All Start callbacks (including CameraResolutionAutoFit) have run before LateUpdate.
            // Preserve the original opening view's left edge rather than borrowing 125's map bounds.
            cameraLeftEdge = cameraInitialPosition.x - halfWidth;
            cameraLeftEdgeCached = true;
        }

        float rightEdge = cameraRightBoundary.bounds.min.x;
        float minCenterX = cameraLeftEdge + halfWidth;
        float maxCenterX = rightEdge - halfWidth;
        if (minCenterX > maxCenterX)
        {
            // Same narrow-range fallback as CameraFollowRange: the view cannot fit both edges.
            cameraHorizontalVelocity = 0f;
            worldCamera.transform.position = new Vector3(
                (cameraLeftEdge + rightEdge) * 0.5f, cameraInitialPosition.y, cameraInitialPosition.z);
            return;
        }

        // Continuous Actor-centred X follow, target clamp, independent SmoothDamp.
        float targetX = Mathf.Clamp(level.Actor.transform.position.x + cameraHorizontalOffset, minCenterX, maxCenterX);
        float nextX = cameraHorizontalSmoothTime > 0f
            ? Mathf.SmoothDamp(worldCamera.transform.position.x, targetX,
                ref cameraHorizontalVelocity, cameraHorizontalSmoothTime)
            : targetX;
        worldCamera.transform.position = new Vector3(
            Mathf.Clamp(nextX, minCenterX, maxCenterX), cameraInitialPosition.y, cameraInitialPosition.z);
    }

    /// <summary>
    /// 处理本关盒子被 Actor 顶击后的单次产种请求。
    /// 调用边界：只由 Prefab 中的 <see cref="Level141BoxHit"/> 事件出口调用；
    /// 本方法自行检查暂停、失败、出种中和冷却状态，不应由外部脚本绕过事件链直接触发。
    /// </summary>
    public void HitSeedBox()
    {
        if (!CanPlay) return;
        if (boxSeedInFlight || cooldownRemaining > 0f)
        {
            AudioManager.PlaySound("passwordError");
            if (cooldownRemaining > 0f) cooldownFeedback = 0.4f;
            return; // 等待中只反馈失败，不再出种或重置计时。
        }
        AudioManager.PlaySound("getCoin");
        boxSeedInFlight = true;
        // Decide and reserve at the hit, not after emergence. A full-field seed must be picked
        // even if a site becomes free during its flight or while it is waiting on the ground.
        PlantingSite targetSite = ReserveLeftmostSite();
        var boxRenderer = boxVisual != null ? boxVisual.GetComponent<SpriteRenderer>() : null;
        Vector3 origin = boxRenderer != null ? boxRenderer.bounds.center : seedBox.position;
        var seed = CreateSeed(origin);
        seed.assigned = targetSite != null;
        var boxSorting = seedBox.GetComponent<SortingGroup>();
        seed.view.sortingLayerID = boxSorting != null ? boxSorting.sortingLayerID : seed.view.sortingLayerID;
        seed.view.sortingOrder = (boxSorting != null ? boxSorting.sortingOrder : 50) - 1;
        Bounds boxBounds = boxRenderer != null ? boxRenderer.bounds : new Bounds(origin, Vector3.one * 1.2f);
        Vector3 end = targetSite != null
            ? PlantingPoint(targetSite)
            : SeedGroundPoint(seed, seedBox.position.x + 1.05f, boxBounds.max.y + 0.6f);
        // One continuous parabola replaces the vertical rise -> second jump corner.
        // For unequal start/end heights, derive Fly's arc coefficient from a low WORLD apex;
        // a fixed small coefficient could otherwise send a downhill seed straight down.
        float boxTop = boxBounds.max.y + BoxBounceHeight;
        float apexY = Mathf.Max(boxTop + seed.view.bounds.extents.y + 0.15f, end.y + 0.15f);
        float ascent = Mathf.Sqrt(Mathf.Max(0f, apexY - origin.y));
        float descent = Mathf.Sqrt(Mathf.Max(0f, apexY - end.y));
        float arcHeight = (ascent + descent) * (ascent + descent) * 0.25f;
        bool clearedBox = false;
        if (boxVisual != null)
            Animate(0.25f, t => boxVisual.localPosition = boxVisualStart + Vector3.up * (BoxBounceHeight * Mathf.Sin(t * Mathf.PI)));
        // Keep the former total launch time (0.35 + 0.6s), without a mid-air stop or restart.
        Fly(seed.view.transform, end, 0.35f + seedFlightSeconds, arcHeight, t =>
        {
            if (clearedBox) return;
            Bounds seedBounds = seed.view.bounds;
            if (seedBounds.min.y < boxTop && seedBounds.min.x < boxBounds.max.x && seedBounds.max.x > boxBounds.min.x)
                return;
            clearedBox = true;
            seed.view.sortingLayerName = "SceneLayer";
            seed.view.sortingOrder = SeedSortingOrder;
        }, () =>
        {
            if (targetSite != null) CompleteSeedPlanting(seed, targetSite);
            else seed.landed = true;
            StartBoxCooldown();
        });
    }

    private void StartBoxCooldown()
    {
        boxSeedInFlight = false;
        cooldownDuration = nextCooldownSeconds;
        cooldownRemaining = cooldownDuration;
        nextCooldownSeconds = Mathf.Min(boxCooldownSeconds, nextCooldownSeconds + boxCooldownStepSeconds);
        cooldownFeedback = 0f;
    }

    private void TickCooldown(float dt)
    {
        cooldownRemaining = Mathf.Max(0f, cooldownRemaining - dt);
        cooldownFeedback = Mathf.Max(0f, cooldownFeedback - dt);
        float alpha = Mathf.Min((cooldownDuration - cooldownRemaining) / 0.2f, cooldownRemaining / 0.2f);
        cooldown.Set(cooldownRemaining / cooldownDuration, alpha);
        cooldown.Root.position = boxProgressPosition + Vector3.right *
            (Mathf.Sin(cooldownFeedback * 65f) * 0.12f * cooldownFeedback / 0.4f);
    }

    private Seed CreateSeed(Vector3 origin)
    {
        var seed = new Seed { view = CreateSprite("DroppedSeed", growthSprites[0], runtimeRoot, SeedSortingOrder) };
        seed.view.transform.position = origin;
        seed.view.transform.localScale = Vector3.one * (0.75f * 1.2f);
        seeds.Add(seed);
        return seed;
    }

    private void SpawnSeed(Vector3 origin, float landingX)
    {
        // 碎瓜种子仍先落地再拾取；每颗独立取弧高/时长/旋转，避免齐刷刷升降。
        Seed seed = CreateSeed(origin);
        landingX += UnityEngine.Random.Range(-shatteredSeedSpacing * 0.3f, shatteredSeedSpacing * 0.3f);
        Vector3 end = SeedGroundPoint(seed, landingX, origin.y + 0.1f);
        float angle = UnityEngine.Random.Range(-40f, 40f);
        float spin = UnityEngine.Random.Range(180f, 420f) * (landingX < origin.x ? 1f : -1f);
        Fly(seed.view.transform, end, UnityEngine.Random.Range(0.7f, 1.05f),
            UnityEngine.Random.Range(1.55f, 2.5f),
            t => seed.view.transform.localRotation = Quaternion.Euler(0f, 0f, angle + spin * t), () =>
            {
                // 旋转后的最低点重新贴地，放大种子也不陷入地面。
                Vector3 position = seed.view.transform.position;
                position.y += GroundY(position.x, position.y + 1f, -3.3f) - seed.view.bounds.min.y;
                seed.view.transform.position = position;
                seed.landed = true;
            });
    }

    private Vector3 SeedGroundPoint(Seed seed, float landingX, float rayStartY)
    {
        Vector3 origin = seed.view.transform.position;
        float bottom = seed.view.bounds.extents.y;
        return new Vector3(landingX, GroundY(landingX, rayStartY, -3.3f) + bottom, origin.z);
    }

    private void FlySeedToGround(Seed seed, float landingX, Action complete = null)
    {
        Vector3 end = SeedGroundPoint(seed, landingX, seed.view.transform.position.y + 0.1f);
        Fly(seed.view.transform, end, seedFlightSeconds, 0.85f, null, () =>
        {
            seed.landed = true;
            complete?.Invoke();
        });
    }

    private PlantingSite ReserveLeftmostSite()
    {
        foreach (var site in sites)
        {
            if (site.occupied) continue;
            site.occupied = true; // Reserve BEFORE any emergence/flight; shared by both planting routes.
            return site;
        }
        return null;
    }

    private void FlySeedToSite(Seed seed, PlantingSite site, Action complete = null)
    {
        seed.assigned = true;
        seed.view.color = Color.white;
        Fly(seed.view.transform, PlantingPoint(site), seedFlightSeconds, 0.65f, null, () =>
        {
            CompleteSeedPlanting(seed, site);
            complete?.Invoke();
        });
    }

    private void CompleteSeedPlanting(Seed seed, PlantingSite site)
    {
        seed.view.enabled = false;
        Destroy(seed.view.gameObject);
        seed.view = null;
        BeginGrowth(site);
    }

    private void TickSeeds()
    {
        if (actorCollider == null && level.Actor != null) actorCollider = level.Actor.GetComponent<Collider2D>();
        foreach (var seed in seeds)
        {
            if (seed.assigned || !seed.landed || seed.view == null) continue;
            // Only physical player contact collects a landed seed. No screen auto-pickup.
            if (!seed.picked && ActorTouches(seed.view))
            {
                seed.picked = true;
                AudioManager.PlaySound("getCoin");
                seed.view.color = new Color(1f, 1f, 1f, 0.7f);
            }
            if (!seed.picked) continue;
            PlantingSite free = ReserveLeftmostSite();
            if (free == null) continue; // Picked surplus stays visible until a site is freed.
            FlySeedToSite(seed, free);
        }
        seeds.RemoveAll(seed => seed.assigned && seed.view == null);
    }

    private void BeginGrowth(PlantingSite site)
    {
        site.growTime = 0f;
        site.stage = 0;
        site.growthStartedFrame = Time.frameCount;
        site.collected = site.mature = site.throwing = false;
        site.blockActorPickupUntilExit = false;
        site.releasePending = false;
        // 成长素材沿用本关种植组210，保证苗/草/花和成熟叶子都挡在角色200之前。
        var plantSorting = site.root.GetComponent<SortingGroup>();
        int cropOrder = plantSorting != null ? plantSorting.sortingOrder : site.pumpkin.sortingOrder;
        site.crop = CreateSprite("GrowingPumpkin", growthSprites[1], runtimeRoot, cropOrder);
        site.crop.sortingLayerID = plantSorting != null ? plantSorting.sortingLayerID : site.pumpkin.sortingLayerID;
        site.nextCrop = CreateSprite("GrowingPumpkinNext", growthSprites[2], runtimeRoot, cropOrder);
        site.nextCrop.sortingLayerID = site.crop.sortingLayerID;
        site.growthBase = SpriteBottomWorld(site.pumpkin);
        site.leafGrowthBase = SpriteBottomWorld(site.leaves);
        site.progress.Root.position = GrowthProgressPosition(site);
        RefreshGrowthVisuals(site);
    }

    private void TickPlants(float dt)
    {
        foreach (var site in sites)
        {
            if (site.falling) TickFallingPumpkin(site, dt);
            if (site.crop == null || site.mature) continue;
            float duration = Mathf.Max(0.1f, stageSeconds) * GrowthStageCount;
            // Planting may have completed in TickMotions this frame; its elapsed time starts here.
            if (site.growthStartedFrame != Time.frameCount)
                site.growTime = Mathf.Min(duration, site.growTime + dt);
            RefreshGrowthVisuals(site);
            if (site.growTime < duration) continue;
            site.crop.enabled = site.nextCrop.enabled = false;
            Destroy(site.crop.gameObject);
            Destroy(site.nextCrop.gameObject);
            site.crop = null;
            site.nextCrop = null;
            site.mature = true;
            site.pumpkin.transform.localPosition = site.initialPosition;
            site.pumpkin.transform.localScale = site.initialScale;
            site.leaves.transform.localPosition = site.initialLeafPosition;
            site.leaves.transform.localScale = site.initialLeafScale;
            site.pumpkin.enabled = site.leaves.enabled = true;
            site.pumpkin.color = site.initialColor;
            site.leaves.color = site.leafColor;
            // Leaves fade from maturity, independently of whether the fruit has been picked.
            Animate(2f, t => site.leaves.color = WithAlpha(site.leafColor, 1f - t), () =>
            {
                site.leaves.enabled = false;
                if (site.releasePending) FinishReleasingSite(site);
            });
            Animate(0.2f, t =>
            {
                site.progress.Set(1f, 1f - t, 1f + 0.08f * Mathf.Sin(t * Mathf.PI));
            });
        }
    }

    private void RefreshGrowthVisuals(PlantingSite site)
    {
        float seconds = Mathf.Max(0.1f, stageSeconds);
        float transitionSeconds = Mathf.Min(GrowthTransitionSeconds, seconds * 0.5f);
        site.stage = Mathf.Min(GrowthStageCount - 1, (int)(site.growTime / seconds));
        float phase = site.growTime - site.stage * seconds;
        float enter = site.stage == 0 ? Mathf.Clamp01(phase / transitionSeconds) : 1f;
        float change = Mathf.Clamp01((phase - seconds + transitionSeconds) / transitionSeconds);
        float crossfade = Mathf.SmoothStep(0f, 1f, change);

        site.crop.sprite = growthSprites[site.stage + 1];
        SetGrowthVisual(site.crop, site.growthBase, Vector3.one * 0.9f, Color.white,
            GrowthScale(enter), Mathf.SmoothStep(0f, 1f, enter) * (1f - crossfade));
        if (site.stage < GrowthStageCount - 1)
        {
            site.nextCrop.sprite = growthSprites[site.stage + 2];
            SetGrowthVisual(site.nextCrop, site.growthBase, Vector3.one * 0.9f, Color.white,
                GrowthScale(change), crossfade);
        }
        else
        {
            site.nextCrop.enabled = false;
            SetGrowthVisual(site.pumpkin, site.growthBase, site.initialScale, site.initialColor,
                GrowthScale(change), crossfade);
            SetGrowthVisual(site.leaves, site.leafGrowthBase, site.initialLeafScale, site.leafColor,
                GrowthScale(change), crossfade);
        }
        float pulse = 1f + 0.09f * Mathf.Sin(Mathf.Clamp01((phase - seconds + 0.18f) / 0.18f) * Mathf.PI);
        // Linear timed fill, with the existing last-0.12s full-bar pulse, not an eased timer.
        site.progress.Set(phase / Mathf.Max(0.01f, seconds - 0.12f), Mathf.Clamp01(site.growTime / 0.2f), pulse);
    }

    private static float GrowthScale(float t)
    {
        // Gentle OutBack: uniform scale from small to full, with a slight overshoot.
        float x = Mathf.Clamp01(t) - 1f;
        return Mathf.LerpUnclamped(0.65f, 1f, 1f + 2.2f * x * x * x + 1.2f * x * x);
    }

    private static Vector3 SpriteBottomLocal(SpriteRenderer view)
    {
        Bounds bounds = view.sprite.bounds;
        return new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
    }

    private static Vector3 SpriteBottomWorld(SpriteRenderer view)
    {
        return view.transform.TransformPoint(SpriteBottomLocal(view));
    }

    private static void SetGrowthVisual(SpriteRenderer view, Vector3 bottom, Vector3 fullScale,
        Color color, float scale, float alpha)
    {
        view.enabled = alpha > 0f;
        view.transform.localScale = fullScale * scale;
        // Keep the original ground contact fixed even with a centre pivot and scaled parents.
        view.transform.position = bottom - view.transform.TransformVector(SpriteBottomLocal(view));
        view.color = WithAlpha(color, alpha);
    }

    private void TickPointer()
    {
        if (worldCamera == null) return;
        if (Input.touchCount > 0)
        {
            bool found = false;
            for (int i = 0; i < Input.touchCount; i++)
            {
                var touch = Input.GetTouch(i);
                if (pointerId == int.MinValue && touch.phase == TouchPhase.Began)
                    BeginPointer(touch.fingerId, touch.position);
                if (touch.fingerId != pointerId) continue;
                found = true;
                if (touch.phase == TouchPhase.Canceled) CancelPointer();
                else if (touch.phase == TouchPhase.Ended) EndPointer(touch.position);
                else MovePointer(touch.position);
            }
            if (pointerId != int.MinValue && !found) CancelPointer();
            return;
        }
        if (pointerId >= 0) { CancelPointer(); return; }
        if (Input.GetMouseButtonDown(0)) BeginPointer(-1, Input.mousePosition);
        if (pointerId != -1) return;
        if (Input.GetMouseButtonUp(0)) EndPointer(Input.mousePosition);
        else if (Input.GetMouseButton(0)) MovePointer(Input.mousePosition);
        else CancelPointer();
    }

    private void BeginPointer(int id, Vector2 screen)
    {
        if (pointerId != int.MinValue || InputUtils.IsPointerOverUI(screen)) return;
        Vector3 world = ScreenWorld(screen);
        foreach (var site in sites)
        {
            if (!site.mature || site.collected || site.throwing) continue;
            Bounds bounds = site.pumpkin.bounds;
            if (world.x < bounds.min.x || world.x > bounds.max.x || world.y < bounds.min.y || world.y > bounds.max.y) continue;
            dragged = site;
            pointerId = id;
            dragStartWorld = world;
            pumpkinStartWorld = site.pumpkin.transform.position;
            committedDrag = false;
            break;
        }
    }

    private void MovePointer(Vector2 screen)
    {
        if (dragged == null) return;
        Vector3 delta = ScreenWorld(screen) - dragStartWorld;
        // 可横向或斜向移走，松手后从新位置砸落；不把整瓜拖到原地面以下。
        delta.y = Mathf.Max(0f, delta.y);
        dragged.pumpkin.transform.position = pumpkinStartWorld + new Vector3(delta.x, delta.y, 0f);
        if (!committedDrag && delta.sqrMagnitude >= dragThreshold * dragThreshold)
        {
            committedDrag = true;
            dragged.throwing = true; // Throwing is not collection: no counter, delivery or getCoin.
        }
    }

    private void EndPointer(Vector2 screen)
    {
        if (dragged == null) return;
        MovePointer(screen);
        var site = dragged;
        bool drop = committedDrag;
        ClearPointer();
        if (drop) { site.falling = true; site.fallSpeed = 0f; }
        else ReturnUncommittedDrag(site);
    }

    private void CancelPointer()
    {
        if (dragged != null)
        {
            if (committedDrag) { dragged.falling = true; dragged.fallSpeed = 0f; }
            else ReturnUncommittedDrag(dragged);
        }
        ClearPointer();
    }

    private void ClearPointer()
    {
        dragged = null;
        pointerId = int.MinValue;
        committedDrag = false;
    }

    private void ReturnUncommittedDrag(PlantingSite site)
    {
        site.pumpkin.transform.localPosition = site.initialPosition;
        // A cancelled click/drag must not immediately turn into collection by an overlapping Actor.
        // That fruit can be auto-picked again after Actor leaves and comes back.
        site.blockActorPickupUntilExit = ActorTouches(site.pumpkin);
    }

    private void CountCollection(PlantingSite site)
    {
        if (site.collected || site.throwing) return;
        site.collected = true;
        AudioManager.PlaySound("getCoin");
        int previousPercent = GetSurpassedPercent(collected);
        collected++;
        RefreshCollectedText();
        hudNumberFeedback?.Play(true, GetSurpassedPercent(collected) != previousPercent);
    }

    private void TickActorHarvest()
    {
        foreach (var site in sites)
        {
            // Pointer handling runs first; held/thrown fruit belongs exclusively to the throw path.
            if (!site.mature || site.collected || site.throwing || site == dragged || !site.pumpkin.enabled) continue;
            bool touching = ActorTouches(site.pumpkin);
            if (site.blockActorPickupUntilExit)
            {
                if (!touching) site.blockActorPickupUntilExit = false;
                continue;
            }
            if (touching) CollectPumpkin(site);
        }
    }

    private void CollectPumpkin(PlantingSite site)
    {
        if (!site.mature || site.collected || site.throwing) return;
        if (!farmerLeaving && delivered + deliveriesReserved >= 10)
        {
            // Wait for the last reserved delivery to actually arrive before switching to the HUD.
            site.pumpkin.transform.localPosition = site.initialPosition;
            return;
        }
        bool giveToFarmer = !farmerLeaving && farmer != null;
        Vector3 iconPosition = Vector3.zero;
        Vector2 iconSize = Vector2.zero;
        if (!giveToFarmer && !TryGetHudPumpkinTarget(out iconPosition, out iconSize))
        {
            site.pumpkin.transform.localPosition = site.initialPosition;
            if (!hudIconWarningLogged)
            {
                Debug.LogWarning("[Level141] HUD pumpkin glyph is unavailable; fruit remains collectible.", this);
                hudIconWarningLogged = true;
            }
            return;
        }
        CountCollection(site);
        if (giveToFarmer) deliveriesReserved++;
        site.pumpkin.color = site.initialColor;
        RaiseCollectionVisual(site);
        collectionFlights.Add(new CollectionFlight
        {
            site = site,
            giveToFarmer = giveToFarmer,
            startPosition = SiteCenter(site),
            startScale = site.pumpkin.transform.localScale,
            startWorldSize = site.pumpkin.bounds.size,
            destination = giveToFarmer ? farmer.position + Vector3.up : iconPosition,
            iconSize = iconSize,
            // 钥匙同式线性时间抛物线；距离决定总时长，近距离也保留最短可辨识弧线。
            duration = Mathf.Max(0.35f, Vector3.Distance(SiteCenter(site),
                giveToFarmer ? farmer.position + Vector3.up : iconPosition) / Mathf.Max(0.1f, collectSpeed))
        });
    }

    private void RaiseCollectionVisual(PlantingSite site)
    {
        Transform fruit = site.pumpkin.transform;
        site.collectionParent = fruit.parent;
        site.collectionSiblingIndex = fruit.GetSiblingIndex();
        site.collectionSortingLayer = site.pumpkin.sortingLayerID;
        site.collectionSortingOrder = site.pumpkin.sortingOrder;
        // A child renderer's order alone cannot escape the planting group's SceneLayer/210.
        // Move only the fruit out; keep its world pose/size and leave the leaves in the field.
        fruit.SetParent(runtimeRoot, true);
        site.pumpkin.sortingLayerName = "SceneLayer";
        site.pumpkin.sortingOrder = CollectionSortingOrder;
        site.collectionVisualRaised = true;
        // CollectionFlight captures startScale AFTER reparenting, in the new parent's space.
    }

    private void RestoreCollectionVisual(PlantingSite site)
    {
        if (site == null || !site.collectionVisualRaised) return;
        site.collectionVisualRaised = false;
        if (site.pumpkin == null) return;
        site.pumpkin.enabled = false; // Never flash the authored fruit during arrival/teardown.
        site.pumpkin.sortingLayerID = site.collectionSortingLayer;
        site.pumpkin.sortingOrder = site.collectionSortingOrder;
        // During level destruction the original parent may already be gone. In that case,
        // keep the hidden fruit under runtimeRoot so Cleanup still destroys it with the level.
        if (site.collectionParent != null)
        {
            Transform fruit = site.pumpkin.transform;
            fruit.SetParent(site.collectionParent, false);
            fruit.SetSiblingIndex(site.collectionSiblingIndex);
            fruit.localPosition = site.initialPosition;
            fruit.localScale = site.initialScale;
            fruit.localRotation = site.initialRotation;
        }
        site.collectionParent = null;
    }

    private void TickCollectionFlights(float dt)
    {
        for (int i = collectionFlights.Count - 1; i >= 0; i--)
        {
            CollectionFlight flight = collectionFlights[i];
            PlantingSite site = flight.site;
            if (flight.giveToFarmer)
            {
                flight.destination = farmer.position + Vector3.up;
            }
            else if (TryGetHudPumpkinTarget(out Vector3 currentPosition, out Vector2 currentSize))
            {
                flight.destination = currentPosition;
                flight.iconSize = currentSize;
            }

            // 直接使用 ItemJumpToListener 的时间抛物线，取消折线路径按距离追赶。
            flight.elapsed += Mathf.Max(0f, dt);
            float t = Mathf.Clamp01(flight.elapsed / flight.duration);
            Vector3 centre = Vector3.Lerp(flight.startPosition, flight.destination, t) +
                Vector3.up * (4f * collectArcHeight * t * (1f - t));
            bool arrived = t >= 1f;
            if (!flight.giveToFarmer)
            {
                Vector3 iconScale = new Vector3(
                    flight.startScale.x * flight.iconSize.x / Mathf.Max(0.0001f, flight.startWorldSize.x),
                    flight.startScale.y * flight.iconSize.y / Mathf.Max(0.0001f, flight.startWorldSize.y),
                    flight.startScale.z);
                site.pumpkin.transform.localScale = Vector3.Lerp(flight.startScale, iconScale, t);
            }
            site.pumpkin.transform.position = centre - site.pumpkin.transform.TransformVector(site.pumpkin.sprite.bounds.center);
            if (!arrived) continue;

            collectionFlights.RemoveAt(i);
            site.pumpkin.enabled = false; // Consume only at arrival; neither route fades in flight.
            if (flight.giveToFarmer)
            {
                deliveriesReserved--;
                delivered++;
                if (demandCount != null) demandCount.text = Mathf.Max(0, 10 - delivered).ToString(CultureInfo.InvariantCulture);
                if (delivered == 10) CompleteDemand();
            }
            ReleaseSite(site);
        }
    }

    private bool TryGetHudPumpkinTarget(out Vector3 position, out Vector2 size)
    {
        position = Vector3.zero;
        size = Vector2.zero;
        if (collectedText == null || !collectedText.isActiveAndEnabled) return false;
        if (collectedText.havePropertiesChanged || collectedText.textInfo.characterCount == 0)
            collectedText.ForceMeshUpdate();

        TMP_TextInfo textInfo = collectedText.textInfo;
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo glyph = textInfo.characterInfo[i];
            if (glyph.elementType != TMP_TextElementType.Sprite || !glyph.isVisible) continue;
            Vector3 bottomLeft = collectedText.transform.TransformPoint(glyph.bottomLeft);
            Vector3 bottomRight = collectedText.transform.TransformPoint(glyph.bottomRight);
            Vector3 topLeft = collectedText.transform.TransformPoint(glyph.topLeft);
            Vector3 topRight = collectedText.transform.TransformPoint(glyph.topRight);
            position = (bottomLeft + topRight) * 0.5f;
            size = new Vector2(Vector3.Distance(bottomLeft, bottomRight), Vector3.Distance(bottomLeft, topLeft));
            return size.x > 0f && size.y > 0f;
        }
        return false;
    }

    private void TickFallingPumpkin(PlantingSite site, float dt)
    {
        site.fallSpeed += 18f * dt;
        Vector3 position = site.pumpkin.transform.position;
        float bottomOffset = position.y - site.pumpkin.bounds.min.y;
        float ground = GroundY(position.x, site.pumpkin.bounds.min.y + 0.08f, -3.3f);
        position.y -= site.fallSpeed * dt;
        if (position.y - bottomOffset <= ground)
        {
            position.y = ground + bottomOffset;
            site.pumpkin.transform.position = position;
            site.falling = false;
            Shatter(site, position);
        }
        else site.pumpkin.transform.position = position;
    }

    private void Shatter(PlantingSite site, Vector3 position)
    {
        AudioManager.PlaySound("crashBroke");
        float crackOffset = site.pumpkin.bounds.size.x * 0.35f;
        Color wholeColor = site.pumpkin.color;
        var fragments = new SpriteRenderer[fragmentSprites.Length];
        var splitStarts = new Vector3[fragmentSprites.Length];
        var restingPositions = new Vector3[fragmentSprites.Length];
        for (int i = 0; i < fragmentSprites.Length; i++)
        {
            // Keep the accepted left / right / front resting layout and scale exactly as before.
            var fragment = CreateSprite("PumpkinFragment", fragmentSprites[i], runtimeRoot, 215 + i);
            float side = i == 0 ? -1f : i == 1 ? 1f : 0f;
            fragment.transform.position = position + Vector3.right * (side * crackOffset);
            fragment.transform.localScale = Vector3.one * 0.9f;
            fragments[i] = fragment;
            restingPositions[i] = GetFragmentGroundPosition(fragment);
            splitStarts[i] = restingPositions[i];
            splitStarts[i].x = Mathf.Lerp(position.x, restingPositions[i].x, 0.55f);
            fragment.transform.position = splitStarts[i];
            fragment.color = new Color(1f, 1f, 1f, 0f);
        }

        Animate(fragmentSplitSeconds, t =>
        {
            float spread = 1f - (1f - t) * (1f - t);
            // Finish the crossfade quickly, then show the three solid pieces settling.
            float reveal = Mathf.Clamp01(t * 4f);
            site.pumpkin.color = WithAlpha(wholeColor, 1f - reveal);
            for (int i = 0; i < fragments.Length; i++)
            {
                if (fragments[i] == null) continue;
                Vector3 settlingPosition = Vector3.Lerp(splitStarts[i], restingPositions[i], spread);
                // A small contact rebound, not another launch. The front piece settles a little sooner.
                float bounceT = Mathf.Clamp01(t / (i == 2 ? 0.85f : 1f));
                float height = fragmentBounceHeight * (1f - i * 0.15f);
                settlingPosition.y += 4f * height * bounceT * (1f - bounceT);
                fragments[i].transform.position = settlingPosition;
                fragments[i].color = new Color(1f, 1f, 1f, reveal);
            }
        }, () =>
        {
            site.pumpkin.enabled = false;
            for (int i = 0; i < fragments.Length; i++)
            {
                if (fragments[i] == null) continue;
                fragments[i].transform.position = restingPositions[i];
                HoldAndFadeFragment(fragments[i]);
            }
            // Avoid reusing the plant while its old whole-fruit fade is still running.
            ReleaseSite(site);
        });
        for (int i = 0; i < 10; i++) SpawnSeed(position, position.x + (i - 4.5f) * shatteredSeedSpacing);
    }

    private static float FragmentBottomOffset(SpriteRenderer fragment)
    {
        float lowestOffset = float.PositiveInfinity;
        foreach (Vector2 vertex in fragment.sprite.vertices)
        {
            Vector3 offset = fragment.transform.TransformVector(new Vector3(vertex.x, vertex.y, 0f));
            lowestOffset = Mathf.Min(lowestOffset, offset.y);
        }
        return float.IsPositiveInfinity(lowestOffset)
            ? fragment.transform.position.y - fragment.bounds.min.y
            : -lowestOffset;
    }

    private Vector3 GetFragmentGroundPosition(SpriteRenderer fragment)
    {
        Vector3 position = fragment.transform.position;
        position.y = GroundY(position.x, fragment.bounds.max.y + 0.1f, -3.3f) + FragmentBottomOffset(fragment);
        return position;
    }

    private void HoldAndFadeFragment(SpriteRenderer fragment)
    {
        fragment.color = Color.white;
        Animate(fragmentHoldSeconds, null, () =>
        {
            if (fragment == null) return;
            Animate(fragmentFadeSeconds, t =>
            {
                if (fragment != null) fragment.color = new Color(1f, 1f, 1f, 1f - t);
            }, () =>
            {
                if (fragment != null) Destroy(fragment.gameObject);
            });
        });
    }

    private void ReleaseSite(PlantingSite site)
    {
        site.pumpkin.enabled = false;
        RestoreCollectionVisual(site);
        if (site.leaves.enabled)
        {
            // Do not restart the fade at full alpha or reuse the site while its old leaves are fading.
            site.releasePending = true;
            return;
        }
        FinishReleasingSite(site);
    }

    private void FinishReleasingSite(PlantingSite site)
    {
        site.leaves.enabled = false;
        site.pumpkin.transform.localPosition = site.initialPosition;
        site.pumpkin.transform.localScale = site.initialScale;
        site.pumpkin.transform.localRotation = site.initialRotation;
        site.pumpkin.color = site.initialColor;
        site.leaves.color = site.leafColor;
        site.mature = site.collected = site.throwing = site.occupied = site.releasePending = false;
        site.blockActorPickupUntilExit = false;
    }

    private void CompleteDemand()
    {
        if (farmerLeaving) return;
        farmerLeaving = true;
        demandBubble?.CloseStraightly();
        // 141 owns only the gift's drop. After landing, the shared Key owns every interaction.
        // Keep MoveFloatHandle disabled until landing so its Start caches the final resting position.
        rewardKeyPickup.enabled = false;
        rewardKeyFloat.enabled = false;
        rewardKey.position = farmer.position + Vector3.up;
        rewardKey.gameObject.SetActive(true);
        Vector3 keyRestPosition = new Vector3(farmer.position.x - 0.7f,
            GroundY(farmer.position.x - 0.7f, farmer.position.y + 1.5f, -3.3f) + 0.8f, 0f);
        Fly(rewardKey, keyRestPosition, 0.6f, 0.8f, null, () =>
        {
            if (rewardKey == null) return;
            rewardKeyFloat.enabled = true;
            rewardKeyPickup.enabled = true;
        });
        farmer.localScale = new Vector3(-Mathf.Abs(farmer.localScale.x), farmer.localScale.y, farmer.localScale.z);
        PlayFarmerAnimation("walk");
    }

    private void TickFarmer(float dt)
    {
        if (!farmerLeaving || farmer == null || !farmer.gameObject.activeSelf) return;
        farmer.position += Vector3.left * (farmerWalkSpeed * dt);
        float leftEdge = sites[0].root.position.x - 5f;
        if (worldCamera != null) leftEdge = Mathf.Min(leftEdge,
            worldCamera.transform.position.x - worldCamera.orthographicSize * worldCamera.aspect - 2f);
        if (farmer.position.x < leftEdge) Destroy(farmer.gameObject);
    }

    private static int GetSurpassedPercent(int count)
    {
        return SurpassedPercentByCount[Mathf.Clamp(count, 0, SurpassedPercentByCount.Length - 1)];
    }

    private void RefreshCollectedText()
    {
        collectedLocalize?.OnLocalize(true);
    }

    /// <summary>I2 LocalizeEvent 回调：{0} 为收集数，{1} 为超越百分比，格式化后交回 TMP 排版。</summary>
    public void FormatCollectedTranslation()
    {
        if (!Application.isPlaying) return;
        string template = string.IsNullOrEmpty(Localize.MainTranslation) ? CollectedTextFallback : Localize.MainTranslation;
        try
        {
            Localize.MainTranslation = string.Format(CultureInfo.InvariantCulture, template,
                collected, GetSurpassedPercent(collected));
        }
        catch (FormatException)
        {
            if (!collectedFormatWarningLogged)
            {
                Debug.LogWarning("[Level141] Collected translation requires {0}/{1} placeholders.", this);
                collectedFormatWarningLogged = true;
            }
            Localize.MainTranslation = string.Format(CultureInfo.InvariantCulture, CollectedTextFallback,
                collected, GetSurpassedPercent(collected));
        }
    }

    private Vector3 SiteCenter(PlantingSite site)
    {
        return site.pumpkin.transform.TransformPoint(site.pumpkin.sprite.bounds.center);
    }

    private Vector3 GrowthProgressPosition(PlantingSite site)
    {
        Bounds bounds = site.pumpkin.sprite.bounds;
        Vector3 top = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        Vector3 position = SiteCenter(site);
        position.y = site.pumpkin.transform.TransformPoint(top).y + 0.35f;
        return position;
    }

    private Vector3 PlantingPoint(PlantingSite site)
    {
        Vector3 centre = SiteCenter(site);
        centre.y = site.pumpkin.transform.TransformPoint(site.pumpkin.sprite.bounds.min).y + 0.15f;
        return centre;
    }

    private Vector3 ScreenWorld(Vector2 screen)
    {
        return worldCamera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -worldCamera.transform.position.z));
    }

    private bool ActorTouches(SpriteRenderer view)
    {
        if (actorCollider == null || !actorCollider.enabled) return false;
        Vector2 point = actorCollider.ClosestPoint(view.bounds.center);
        Bounds bounds = view.bounds;
        return point.x >= bounds.min.x - 0.08f && point.x <= bounds.max.x + 0.08f &&
            point.y >= bounds.min.y - 0.08f && point.y <= bounds.max.y + 0.08f;
    }

    private float GroundY(float x, float startY, float fallback)
    {
        int count = Physics2D.RaycastNonAlloc(new Vector2(x, startY), Vector2.down, groundHits, 50f, groundLayers);
        float highest = float.NegativeInfinity;
        for (int i = 0; i < count; i++)
        {
            var hit = groundHits[i];
            if (hit.collider == null || hit.collider.isTrigger || hit.normal.y < 0.5f ||
                hit.collider.transform.IsChildOf(seedBox) || hit.collider.transform.IsChildOf(level.Actor.transform)) continue;
            highest = Mathf.Max(highest, hit.point.y);
        }
        return float.IsNegativeInfinity(highest) ? fallback : highest;
    }

    private SpriteRenderer CreateSprite(string name, Sprite sprite, Transform parent, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var view = go.AddComponent<SpriteRenderer>();
        view.sprite = sprite;
        view.sharedMaterial = spriteMaterial;
        view.sortingLayerName = "SceneLayer";
        view.sortingOrder = order;
        return view;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a *= Mathf.Clamp01(alpha);
        return color;
    }

    private void Fly(Transform target, Vector3 end, float seconds, float height, Action<float> extra, Action complete)
    {
        Vector3 start = target.position;
        Animate(seconds, t =>
        {
            if (target == null) return;
            // Exact shared ItemJumpToListener equation, with a local pauseable clock.
            target.position = Vector3.Lerp(start, end, t) + Vector3.up * (4f * height * t * (1f - t));
            extra?.Invoke(t);
        }, complete);
    }

    private void Animate(float seconds, Action<float> step, Action complete = null)
    {
        var motion = new Motion { duration = Mathf.Max(0.001f, seconds), step = step, complete = complete };
        motions.Add(motion);
        step?.Invoke(0f);
    }

    private void TickMotions(float dt)
    {
        for (int i = motions.Count - 1; i >= 0; i--)
        {
            var motion = motions[i];
            motion.elapsed += dt;
            float t = Mathf.Clamp01(motion.elapsed / motion.duration);
            motion.step?.Invoke(t);
            if (t < 1f) continue;
            motions.RemoveAt(i);
            motion.complete?.Invoke();
        }
    }

    private void PlayFarmerAnimation(string animation)
    {
        if (farmerSkeleton == null) return;
        farmerSkeleton.Initialize(false);
        if (farmerSkeleton.Skeleton.Data.FindAnimation(animation) != null)
            farmerSkeleton.AnimationState.SetAnimation(0, animation, true);
    }

    private void SetVisualClock(bool active)
    {
        wasActive = active;
        if (farmerSkeleton != null) farmerSkeleton.timeScale = active ? 1f : 0f;
        if (demandBubble != null)
        {
            var animator = demandBubble.GetComponent<Animator>();
            if (animator != null) animator.speed = active ? 1f : 0f;
        }
    }

    private void OnPause(GamePauseEvent evt) { paused = true; SetVisualClock(false); CancelPointer(); }
    private void OnResume(GameResumeEvent evt) { paused = false; }
    private void OnReset(GameResetEvent evt)
    {
        if (retiring) return;
        retiring = true;
        terminal = true;
        SetVisualClock(false);
        ClearPointer();
        motions.Clear();
        collectionFlights.Clear();
        StopSharedKeyWork();
        // PlayCtrl owns the 0.5s transition, DestroyLevel and the next prefab instance.
        // Keep this instance's last visual pose; do not restore editor samples or respawn here.
    }
    private void OnTrap(OnTriggerTrapEvent evt) { terminal = true; SetVisualClock(false); CancelPointer(); }
    private void OnFallArea(OnTriggerFailAreaEvent evt) { terminal = true; SetVisualClock(false); CancelPointer(); }
    private void OnState(SwitchGameStateEvent evt)
    {
        if (!evt.NewState.Equals(GamePlayStateName.PLAYING)) SetVisualClock(false);
    }

    private void OnDisable() { EventsUtils.ResetEvents(ref subscriber); Cleanup(); }
    private void OnDestroy() { EventsUtils.ResetEvents(ref subscriber); Cleanup(); }

    private void StopSharedKeyWork()
    {
        if (doorTrigger != null) doorTrigger.enabled = false;
        foreach (var owned in ownedObjects)
        {
            if (owned == null) continue;
            foreach (var executor in owned.GetComponentsInChildren<BaseItemExecutor>(true)) executor.enabled = false;
            foreach (var handle in owned.GetComponentsInChildren<BaseItemEventHandle>(true)) handle.enabled = false;
            foreach (var collider in owned.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
            foreach (var jump in owned.GetComponentsInChildren<ItemJumpToListener>(true)) jump.Stop();
            foreach (var floating in owned.GetComponentsInChildren<MoveFloatHandle>(true))
            {
                floating.StopMove();
                floating.enabled = false;
            }
            foreach (var target in owned.GetComponentsInChildren<Transform>(true)) target.DOKill();
            foreach (var renderer in owned.GetComponentsInChildren<SpriteRenderer>(true)) DOTween.Kill(renderer);
            foreach (var skeleton in owned.GetComponentsInChildren<SkeletonAnimation>(true)) skeleton.timeScale = 0f;
        }
    }

    private void Cleanup()
    {
        if (cleanupComplete) return;
        cleanupComplete = true;
        retiring = true;
        terminal = true;
        initialized = false;
        ClearPointer();
        hudNumberFeedback?.Dispose();
        hudNumberFeedback = null;
        StopSharedKeyWork();
        foreach (var owned in ownedObjects) if (owned != null) owned.SetActive(false);
        cameraLeftEdgeCached = false;
        cameraHorizontalVelocity = 0f;
        motions.Clear();
        collectionFlights.Clear();
        seeds.Clear();
        // OnReset may already have cleared collectionFlights. Restore by site ownership,
        // before runtimeRoot is destroyed, rather than relying on the remaining flight list.
        if (sites != null)
            foreach (var site in sites) RestoreCollectionVisual(site);
        if (demandBubble != null)
        {
            foreach (var text in demandBubble.GetComponentsInChildren<TMP_Text>(true))
            { text.DOKill(); text.transform.DOKill(); }
        }
        foreach (var owned in ownedObjects) if (owned != null) Destroy(owned);
        ownedObjects.Clear();
        sharedKeyLock = null;
        rewardKey = null;
        rewardKeyPickup = null;
        rewardKeyFloat = null;
        if (sharedKeyLockTemplate != null) sharedKeyLockTemplate.gameObject.SetActive(false);
        HideAuthoredPumpkins();
        if (runtimeRoot != null)
        {
            runtimeRoot.gameObject.SetActive(false);
            Destroy(runtimeRoot.gameObject);
            runtimeRoot = null;
        }
        if (authoredFarmer != null) authoredFarmer.gameObject.SetActive(false);
        authoredFarmer = null;
    }
}
