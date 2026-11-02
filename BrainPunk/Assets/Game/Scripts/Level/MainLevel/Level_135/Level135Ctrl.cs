using System.Collections.Generic;
using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using Game.Events;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using Spine.Unity;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>135 可乐排队唯一编排器；服务、补位、摇晃与演出时钟均由本关拥有。</summary>
[DefaultExecutionOrder(200)]
public sealed partial class Level135Ctrl : MonoBehaviour
{
    #region 场景绑定与运行状态
    /// <summary>从135场景显式绑定主角、服务区、队列模板和已有演出，不扫描场景。</summary>
    [Header("Scene references")]
    [SerializeField] private BaseLevelCtrl levelCtrl;
    /// <summary>本关玩家，胜负仍交给原主线生命周期。</summary>
    [SerializeField] private ActorCtrl player;
    /// <summary>玩家主碰撞体，用于排队接触与喷发期间的临时关闭。</summary>
    [SerializeField] private Collider2D playerBody;
    /// <summary>本关拖瓶指针捕获器，暂停和重开时释放捕获。</summary>
    [SerializeField] private Level135ColaDragInput colaInput;
    /// <summary>三瓶库存和逐瓶售卖状态，由本主控推进。</summary>
    [SerializeField] private Level135ColaSupply supply;
    /// <summary>玩家现有骨架，用于右手挂瓶及暂停恢复。</summary>
    [SerializeField] private SkeletonAnimation playerSkeleton;
    /// <summary>玩家现有排序组，作为本关顾客前后关系的参照。</summary>
    [SerializeField] private SortingGroup playerSorting;
    /// <summary>复用击飞演出中的文字节点，只控制本关实例。</summary>
    [SerializeField] private GameObject knockWord;
    /// <summary>柜台队首站位区，沿用场景原布置。</summary>
    [SerializeField] private BoxCollider2D headZone;
    /// <summary>主角清场后走入的购买区。</summary>
    [SerializeField] private BoxCollider2D playerServiceZone;
    /// <summary>原地面碰撞，用于判断角色站位和脚底高度。</summary>
    [SerializeField] private Collider2D ground;
    /// <summary>左侧起点门，作为补位及喷发离屏范围的参考。</summary>
    [SerializeField] private Transform startDoor;
    /// <summary>右侧离场边界，普通买完的顾客到此回收。</summary>
    [SerializeField] private Transform rightFallArea;
    /// <summary>隔离玩家脚本的顾客模板，仅生成本关队列。</summary>
    [SerializeField] private Level135QueueActor queueTemplate;
    /// <summary>沿用138插队受罚的本地打斗演出实例。</summary>
    [SerializeField] private Animator fightAnimation;
    /// <summary>沿用原打斗后的击飞粒子实例。</summary>
    [SerializeField] private ParticleSystem knockExplosion;

    [Header("NPC appearance (local visuals only; no player inventory)")]
    /// <summary>顾客脸部装饰备选，只实例化视觉对象。</summary>
    [SerializeField] private GameObject[] faceAccessories;
    /// <summary>顾客衣服备选，同队服饰按原规则去重。</summary>
    [SerializeField] private GameObject[] bodyAccessories;
    /// <summary>顾客背部装饰备选，不读取玩家拥有状态。</summary>
    [SerializeField] private GameObject[] backAccessories;
    private readonly HashSet<int> occupiedClothes = new HashSet<int>();
    private readonly HashSet<int> occupiedExtras = new HashSet<int>();
    private const float OrderBubbleSeconds = 1.5f;
    // 顾客首次点单时从六句中等概率抽取，允许不同顾客重复；主角点单不使用此池。
    private static readonly string[] NpcOrderKeys =
    {
        "S_LevelTips_Des_level_135_02",
        "S_LevelTips_Des_level_135_03",
        "S_LevelTips_Des_level_135_04",
        "S_LevelTips_Des_level_135_05",
        "S_LevelTips_Des_level_135_06",
        "S_LevelTips_Des_level_135_07"
    };
    private const string FightWarningKey = "S_LevelTips_Des_level_138_17";
    private HintBubble orderBubbleTemplate;
    private HintBubble orderBubble;
    private HintBubble playerOrderBubble;
    private bool playerOrderStarted;
    private float playerOrderElapsed;
    private HintBubble warningBubble;
    private bool warningPending;
    private Level135QueueActor warningSpeaker;
    private Level135QueueActor fightOpponent;
    private readonly List<NpcSpeech> ownedSpeechBubbles = new List<NpcSpeech>();

    private struct NpcSpeech
    {
        public HintBubble Bubble;
        public Level135QueueActor Speaker;

        public NpcSpeech(HintBubble bubble, Level135QueueActor speaker)
        {
            Bubble = bubble;
            Speaker = speaker;
        }
    }

    [Header("Confirmed tuning")]
    /// <summary>相邻顾客中心的排队间距，单位为世界距离。</summary>
    [SerializeField] private float spacing = 1.2f;
    /// <summary>正式队列向柜台补位的每秒移动距离。</summary>
    [SerializeField] private float queueSpeed = 5.5f;
    /// <summary>普通顾客买完后向右离场的每秒移动距离。</summary>
    [SerializeField] private float leaveSpeed = 5.5f;
    /// <summary>新顾客从左侧进入队尾的每秒移动距离。</summary>
    [SerializeField] private float refillSpeed = 8f;
    /// <summary>主角普通购买的总服务时间，单位秒。</summary>
    [SerializeField] private float playerDuration = 1f;
    /// <summary>顾客普通购买的总服务时间，单位秒。</summary>
    [SerializeField] private float npcDuration = 3f;
    /// <summary>顾客购买过程中原瓶开始飞向右手的时刻，单位秒。</summary>
    [SerializeField] private float npcCupTime = 2f;
    /// <summary>原瓶从柜台飞到右手的持续时间，单位秒。</summary>
    [SerializeField] private float cupFlightDuration = 0.35f;
    /// <summary>瓶子交付飞行弧线的最高附加高度。</summary>
    [SerializeField] private float cupArcHeight = 0.35f;
    /// <summary>插队后打斗演出的持续时间，单位秒。</summary>
    [SerializeField] private float fightDuration = 0.8f;
    /// <summary>玩家从顾客头顶插队时的接触容差。</summary>
    [Tooltip("玩家进入NPC头顶上方此范围即开始打斗；演出期间保持物理下落，不等待落地。")]
    [SerializeField] private float headTouchHeight = 0.6f;
    /// <summary>打斗后击飞冲量的等待时间，单位秒。</summary>
    [SerializeField] private float hitDelay = 0.1f;
    /// <summary>原插队失败分支的击飞冲量，不用于可乐浪头位移。</summary>
    [SerializeField] private float knockForce = 50f;

    private enum Phase { Inactive, Playing, Fight, Knock, Flying, Won, Failed }
    private readonly List<Level135QueueActor> queue = new List<Level135QueueActor>();
    private readonly List<Level135QueueActor> leaving = new List<Level135QueueActor>();
    private readonly Dictionary<Collider2D, bool> queueCollisionSnapshot = new Dictionary<Collider2D, bool>();
    private Level135QueueActor inflight;
    private Level135QueueActor servedNpc;
    private bool servingPlayer;
    private float serviceElapsed;
    private bool refilling;
    private bool paused;
    private bool started;
    private Phase phase;
    private EventSubscriber subscriber;
    private Level135QueueActor.CupBinding playerCup;
    /// <summary>主角现有刚体，显式绑定以避免依赖Actor.Start的初始化顺序。</summary>
    [SerializeField] private Rigidbody2D playerRb;
    private float playerAnimationSpeed;
    private float phaseElapsed;
    private float previousFeetY;
    private Vector3 knockPoint;
    private Quaternion initialBodyRotation;
    private Vector2 pauseVelocity;
    private bool pauseSimulated;
    private bool ownsPhysicsPause;
    private bool failureHolding;
    private bool reapplyInputLock;
    private int session;
    private int presentationSession;
    private bool resetPending;

    /// <summary>
    /// 指示本关是否允许玩家进行拖瓶、排队和领餐交互。
    /// 仅在本关已启动、处于 Playing 阶段且未暂停、重开或禁用时为 true；失败演出阶段不会重新开放玩家交互。
    /// </summary>
    public bool CanInteract => started && isActiveAndEnabled && !resetPending && phase == Phase.Playing && !paused && colaPhase == ColaPhase.Idle;
    private int PlayerOrder => playerSorting.sortingOrder;
    private float HeadX => headZone.bounds.center.x;
    private float TailX => queue.Count > 0 ? queue[queue.Count - 1].BodyBounds.center.x : HeadX;

    private bool IsPlayerFailurePhase =>
        phase == Phase.Fight || phase == Phase.Knock || phase == Phase.Flying || phase == Phase.Failed;

    // 玩家失败不等于店铺暂停；整条NPC流程持续到真正暂停、胜利、重开或退出。
    private bool CanAdvanceQueue => started && isActiveAndEnabled && !paused && !resetPending &&
        (phase == Phase.Playing || IsPlayerFailurePhase);

    #endregion

    #region 启动与帧推进
    private void Awake()
    {
        EventsUtils.ResetEvents(ref subscriber);
        subscriber.Subscribe<GamePauseEvent>(_ => SetPaused(true));
        subscriber.Subscribe<GameResumeEvent>(_ => SetPaused(false));
        // 首次从 START 进 PLAYING 不经过 Pause.OnExit，因此也监听明确的状态切换。
        subscriber.Subscribe<SwitchGameStateEvent>(OnGameStateChanged);
        subscriber.Subscribe<GameResetEvent>(_ => StopForReset());
        subscriber.Subscribe<GameReplayEvent>(_ => StopForReset());
        subscriber.Subscribe<OnTriggerFailAreaEvent>(_ => StopForFailure());
        subscriber.Subscribe<OnTriggerTrapEvent>(_ => StopForFailure());
        // 不让加载页期间消耗 NPC 领餐时间。人工独立实例没有 PlayCtrl 时可直接开始。
        paused = PlayCtrl.Instance != null && !PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING);
    }

    private void Start()
    {
        if (levelCtrl == null || player == null || playerBody == null || colaInput == null || supply == null || playerSkeleton == null || playerSorting == null ||
            headZone == null || playerServiceZone == null || ground == null || startDoor == null || rightFallArea == null ||
            queueTemplate == null || jet == null || colaAlertTemplate == null ||
            fightAnimation == null || knockExplosion == null)
        {
            Debug.LogError("[Level135] Incomplete prefab bindings; gameplay not started.", this);
            enabled = false;
            return;
        }
        if (!ValidateAppearancePalette())
        {
            Debug.LogError("[Level135] NPC accessory palette missing, duplicated or too small; gameplay not started.", this);
            enabled = false;
            return;
        }
        // 与132/083一样复用项目HintBubble资源及其I2 key接口，不改共享气泡。
        orderBubbleTemplate = Resources.Load<HintBubble>("HintBubble");
        if (orderBubbleTemplate == null)
        {
            Debug.LogError("[Level135] HintBubble resource missing; gameplay not started.", this);
            enabled = false;
            return;
        }
        if (playerSkeleton != null)
        {
            playerAnimationSpeed = playerSkeleton.timeScale;
            initialBodyRotation = playerSkeleton.transform.localRotation;
        }
        playerCup = Level135QueueActor.CupBinding.Create(player.transform, playerSkeleton);
        if (playerRb == null || playerCup == null)
        {
            Debug.LogError("[Level135] Player body or hand binding missing; no remote/fallback victory.", this);
            enabled = false;
            return;
        }
        // 门入口在序列化层断开；这里也不允许基础成功链选择旧门演出。
        levelCtrl.SetSuccessAnim(null);
        HideEffects();
        supply.Initialize();
        jet.Hide();
        ++session;
        phase = Phase.Playing;
        started = true;
        SpawnInitialQueue();
        previousFeetY = playerBody.bounds.min.y;
        if (paused) ApplyPresentationPause(true);
    }

    private void FixedUpdate()
    {
        if (!CanAdvanceQueue) return;
        float dt = Time.fixedDeltaTime;
        if (colaPhase != ColaPhase.Idle) return;
        Collider2D blocker = CanInteract ? playerBody : null;
        for (int i = 0; i < queue.Count; ++i)
            queue[i].TickMove(HeadX - i * spacing, queueSpeed, dt, blocker);

        TickRefiller(dt, blocker);
        for (int i = leaving.Count - 1; i >= 0; --i)
        {
            Level135QueueActor actor = leaving[i];
            actor.TickMove(rightFallArea.position.x + 2f, leaveSpeed, dt, null);
            if (actor.transform.position.x >= rightFallArea.position.x)
            {
                actor.Recycle();
                leaving.RemoveAt(i);
            }
        }
    }

    private void TickRefiller(float dt, Collider2D blocker)
    {
        if (inflight == null) return;
        // 队列在失败期间也正常前移，继续使用同一队尾槽位规则。
        float target = HeadX - queue.Count * spacing;
        inflight.TickMove(target, refillSpeed, dt, blocker);
        if (Mathf.Abs(inflight.BodyBounds.center.x - target) >= 0.015f) return;

        Level135QueueActor arrived = inflight;
        inflight = null;
        arrived.JoinQueue();
        arrived.SetPaused(false);
        queue.Add(arrived);
        ReindexSorting();
        if (queue.Count >= 8) refilling = false;
        // 这里只完成在途者报到；后续是否继续补满由统一MaybeRefill调度。
    }

    private void LateUpdate()
    {
        // 气泡交接沿共享UI生命周期推进，不绑到NPC服务/暂停时钟。
        TryShowPendingFightWarning();
        if (reapplyInputLock && playerRb != null)
        {
            reapplyInputLock = false;
            Vector2 velocity = playerRb.linearVelocity;
            player.StopMoving();
            playerRb.linearVelocity = velocity;
        }
        if (!CanAdvanceQueue) return;
        if (player.IsDied) StopForFailure(); // 不返回：玩家死亡后仍须推进NPC服务。
        float dt = Time.deltaTime;
        supply.Tick(dt);
        playerCup?.Tick(dt, supply);
        foreach (Level135QueueActor npc in queue) npc.Cup?.Tick(dt, supply);
        foreach (Level135QueueActor npc in leaving) npc.Cup?.Tick(dt, supply);
        if (colaPhase != ColaPhase.Idle) { TickColaEffect(dt); return; }
        if (phase == Phase.Playing)
        {
            CheckDanger();
        }
        else if (phase == Phase.Fight || phase == Phase.Knock)
        {
            if (presentationSession != session) return;
            phaseElapsed += dt;
            if (phaseElapsed >= (phase == Phase.Fight ? fightDuration : hitDelay)) LaunchPlayer();
        }
        else if (phase == Phase.Flying)
        {
            playerSkeleton.transform.Rotate(0f, 0f, 720f * dt, Space.Self);
        }
        if (CanAdvanceQueue)
        {
            TickService(dt);
            MaybeRefill();
        }
        // 本关堵在队伍旁只播walk；失败玩家不恢复动画或输入控制。
        if (CanInteract && player.IsGround && player.XInput != 0f && IsBlockedByQueue())
            player.SpineCtrl.PlayAnimation("walk", true);
        previousFeetY = playerBody.bounds.min.y;
    }

    #endregion

    #region 队首服务与气泡
    private static bool InsideZone(BoxCollider2D zone, Vector3 center)
    {
        Vector2 local = zone.transform.InverseTransformPoint(center);
        local -= zone.offset;
        Vector2 half = zone.size * 0.5f;
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y;
    }

    private void TickService(float dt)
    {
        if (!CanAdvanceQueue || colaPhase != ColaPhase.Idle) return;
        Level135QueueActor candidate = queue.Count > 0 && InsideZone(headZone, queue[0].BodyBounds.center) ? queue[0] : null;
        bool eligiblePlayer = CanInteract && !player.IsDied && queue.Count == 0 && inflight == null &&
            InsideZone(playerServiceZone, playerBody.bounds.center);
        if (candidate != servedNpc || eligiblePlayer != servingPlayer)
        {
            CancelService(); servedNpc = candidate; servingPlayer = eligiblePlayer;
            if (servedNpc != null) ShowOrderBubble(servedNpc);
        }
        if (servedNpc == null && !servingPlayer) return;
        if (servingPlayer && (!player.IsGround || Mathf.Abs(playerRb.linearVelocityY) > 0.1f)) return;
        // 主角先完整点单，再进入原交付计时；离区、失败和重开会取消本次点单。
        if (servingPlayer && !TickPlayerOrder(dt)) return;
        float flightDuration = Mathf.Max(0.01f, cupFlightDuration);
        float flightStart = servingPlayer ? Mathf.Max(0f, playerDuration - flightDuration) : npcCupTime;
        Level135QueueActor.CupBinding cup = servingPlayer ? playerCup : servedNpc.Cup;
        serviceElapsed += dt;
        if (serviceElapsed >= flightStart && !cup.IsFlying && !cup.IsHeld)
        {
            if (!supply.TrySell(out SpriteRenderer bottle, out bool charged)) { serviceElapsed = flightStart; return; }
            cup.BeginFlight(bottle, charged, transform);
        }
        if (cup.IsFlying) cup.UpdateFlight((serviceElapsed - flightStart) / flightDuration, cupArcHeight);
        if (!cup.IsHeld) return;
        // 有气瓶到手后先感叹号再喷发，不能先进入普通离场或成功分支。
        if (cup.Charged) { BeginColaReaction(servingPlayer ? null : servedNpc); return; }
        if (serviceElapsed < (servingPlayer ? playerDuration : npcDuration)) return;
        if (servingPlayer) { CompletePlayerService(); return; }
        Level135QueueActor head = servedNpc;
        queue.RemoveAt(0); Depart(head); CancelService(); ReindexSorting();
    }

    private void CompletePlayerService()
    {
        phase = Phase.Won;
        colaInput.Clear();
        refilling = false;
        player.SetMoveInput(0f);
        player.StopMoving();
        levelCtrl.SetCtrlable(false);
        foreach (Level135QueueActor actor in leaving) actor.SetPaused(true);
        CancelService();
        levelCtrl.SuccessStrightly();
    }

    private void CancelService()
    {
        CloseOrderBubble();
        CancelPlayerOrder();
        if (servedNpc != null && !servedNpc.IsLeaving) servedNpc.HideCup();
        // 胜利后保留已到手的可乐；其余资格取消必须收回玩家在途杯，不能留下延迟胜利。
        if (servingPlayer && phase != Phase.Won) playerCup?.Hide();
        servedNpc = null;
        servingPlayer = false;
        serviceElapsed = 0f;
    }

    private void CancelPlayerService()
    {
        // 玩家失败不是NPC的服务资格失效；保留NPC计时、在途杯和已到手杯。
        if (servingPlayer) CancelService();
    }

    private void ShowOrderBubble(Level135QueueActor actor)
    {
        // 每个NPC首次真正开始服务时说一次；取消后重新获得资格也不重复点单。
        if (actor.HasOrdered) return;
        actor.MarkOrdered();
        // 同一NPC警告优先；跳过的点单不补播，但服务时钟照常继续。
        if ((warningPending || warningBubble != null) && warningSpeaker == actor) return;
        string key = NpcOrderKeys[Random.Range(0, NpcOrderKeys.Length)];
        orderBubble = CreateSpeechBubble(key, actor, "Level135_OrderBubble");
    }

    private bool TickPlayerOrder(float dt)
    {
        if (!playerOrderStarted)
        {
            playerOrderStarted = true;
            // 点单即锁定角色，清除按住的方向；StopMoving同时拦住共享跳跃入口。
            levelCtrl.SetCtrlable(false);
            player.SetMoveInput(0f);
            player.StopMoving();
            playerOrderBubble = Instantiate(orderBubbleTemplate, transform);
            playerOrderBubble.name = "Level135_PlayerOrderBubble";
            ownedSpeechBubbles.RemoveAll(speech => speech.Bubble == null);
            // 无NPC归属，仍交给本关气泡集合负责旧关销毁时的最终清理。
            ownedSpeechBubbles.Add(new NpcSpeech(playerOrderBubble, null));
            playerOrderBubble.Show("S_LevelTips_Des_level_135_08", player.transform, 0f);
            Animator animator = playerOrderBubble.GetComponent<Animator>();
            if (animator != null) animator.Update(0f);
        }
        if (playerOrderElapsed >= OrderBubbleSeconds) return true;
        playerOrderElapsed += dt;
        if (playerOrderElapsed < OrderBubbleSeconds) return false;
        if (playerOrderBubble != null) playerOrderBubble.CloseStraightly();
        return true;
    }

    private void CancelPlayerOrder()
    {
        bool releaseControls = playerOrderStarted && started && isActiveAndEnabled &&
            !resetPending && phase == Phase.Playing && !player.IsDied;
        if (playerOrderBubble != null)
        {
            // 取消时立即收掉旧点单，避免快速离区再进入后两句话重叠。
            playerOrderBubble.gameObject.SetActive(false);
            Destroy(playerOrderBubble.gameObject);
        }
        playerOrderBubble = null;
        playerOrderStarted = false;
        playerOrderElapsed = 0f;
        // 只有正常取消点单才恢复操作；通关、失败、重开与旧关销毁不解锁。
        if (releaseControls)
        {
            levelCtrl.SetCtrlable(true);
            if (!paused) player.ResumeMoving();
        }
    }

    private HintBubble CreateSpeechBubble(string key, Level135QueueActor speaker, string bubbleName)
    {
        HintBubble bubble = Instantiate(orderBubbleTemplate, transform);
        bubble.name = bubbleName;
        ownedSpeechBubbles.RemoveAll(speech => speech.Bubble == null);
        ownedSpeechBubbles.Add(new NpcSpeech(bubble, speaker));
        // 显示、计时、文字动画、收起与暂停行为全部交给共享HintBubble，不再接管内部Tween。
        bubble.Show(key, speaker.transform, OrderBubbleSeconds);
        // LateUpdate生成时立即采样共享动画首帧，避免先显示单位缩放、下一帧才从零弹出。
        // 不预写零缩放，否则Animator的Write Defaults可能把idle默认缩放也记成零。
        Animator animator = bubble.GetComponent<Animator>();
        if (animator != null) animator.Update(0f);
        return bubble;
    }

    private void ShowFightWarning()
    {
        Level135QueueActor speaker = fightOpponent;
        fightOpponent = null;
        // 允许已经离场但仍存在的对手说话；销毁/禁用或演出中断不补造替身。
        if (speaker == null || !speaker.isActiveAndEnabled) return;
        warningSpeaker = speaker;
        warningPending = true;
        TryShowPendingFightWarning();
    }

    private void TryShowPendingFightWarning()
    {
        if (!warningPending) return;
        if (!started || !isActiveAndEnabled || resetPending || !IsPlayerFailurePhase ||
            warningSpeaker == null || !warningSpeaker.isActiveAndEnabled)
        {
            CloseWarningBubble();
            return;
        }

        bool waitingForClose = false;
        foreach (NpcSpeech speech in ownedSpeechBubbles)
        {
            if (speech.Speaker != warningSpeaker || speech.Bubble == null) continue;
            waitingForClose = true;
            Animator animator = speech.Bubble.GetComponent<Animator>();
            // 共享show->idle转场要求show=true；尚在弹出时提前Close会阻断此转场。
            // 让已有弹出/转场先结束，再走共享收起，不硬切动画或改其内部计时。
            bool enteringOrTransitioning = animator != null && animator.isActiveAndEnabled &&
                animator.runtimeAnimatorController != null &&
                (animator.GetCurrentAnimatorStateInfo(0).IsName("show") || animator.IsInTransition(0));
            if (enteringOrTransitioning) continue;
            speech.Bubble.CloseStraightly();
            if (speech.Bubble == orderBubble) orderBubble = null;
        }
        // HintBubble完成收起并自行销毁后才创建警告；无旧气泡时本帧就能直接显示。
        if (waitingForClose) return;
        warningPending = false;
        warningBubble = CreateSpeechBubble(FightWarningKey, warningSpeaker, "Level135_FightWarning");
    }

    private void CloseWarningBubble()
    {
        warningPending = false;
        if (warningBubble != null) warningBubble.CloseStraightly();
        warningBubble = null;
        warningSpeaker = null;
    }

    private void CloseOrderBubble()
    {
        if (orderBubble != null) orderBubble.CloseStraightly();
        orderBubble = null;
    }

    private void DisposeSpeechBubbles()
    {
        // 仅关卡禁用/销毁时立即清理，包含已进入共享收起动画但尚未销毁的实例。
        foreach (NpcSpeech speech in ownedSpeechBubbles)
        {
            HintBubble bubble = speech.Bubble;
            if (bubble == null) continue;
            bubble.gameObject.SetActive(false);
            Destroy(bubble.gameObject);
        }
        ownedSpeechBubbles.Clear();
        orderBubble = null;
        warningBubble = null;
        warningPending = false;
        warningSpeaker = null;
        fightOpponent = null;
    }

    #endregion

    #region 队列成员与外观
    private void MaybeRefill()
    {
        if (!CanAdvanceQueue || crowdCleared || colaPhase != ColaPhase.Idle || inflight != null) return;
        if (!refilling && queue.Count == 2) refilling = true;
        if (refilling && queue.Count < 8)
        {
            inflight = SpawnActor(true, startDoor.position.x);
            if (inflight != null) inflight.SetSorting(110);
        }
    }

    private void SpawnInitialQueue()
    {
        for (int i = 0; i < 8; ++i)
        {
            Level135QueueActor actor = SpawnActor(false, HeadX - i * spacing);
            if (actor == null) return;
            queue.Add(actor);
        }
        ReindexSorting();
    }

    private bool ValidateAppearancePalette()
    {
        if (faceAccessories == null ||
            bodyAccessories == null || backAccessories == null) return false;
        var distinct = new HashSet<GameObject>();
        foreach (GameObject[] parts in new[] { faceAccessories, bodyAccessories, backAccessories })
            foreach (GameObject part in parts)
                if (part == null || !distinct.Add(part)) return false;
        return bodyAccessories.Length >= 8;
    }

    private void RecordOccupiedAppearance(Level135QueueActor actor)
    {
        if (actor == null) return;
        occupiedClothes.Add(actor.ClothingId);
        if (actor.ExtraAccessoryId >= 0) occupiedExtras.Add(actor.ExtraAccessoryId);
    }

    private bool TryChooseAppearance(out int clothingId, out int extraId)
    {
        // 衣服必选，脸饰/背饰额外随机0或1件。三类未回收成员共同占用各自的单件池。
        occupiedClothes.Clear();
        occupiedExtras.Clear();
        foreach (Level135QueueActor actor in queue) RecordOccupiedAppearance(actor);
        foreach (Level135QueueActor actor in leaving) RecordOccupiedAppearance(actor);
        RecordOccupiedAppearance(inflight);

        clothingId = -1;
        extraId = -1;
        int available = 0;
        for (int id = 0; id < bodyAccessories.Length; ++id)
            if (!occupiedClothes.Contains(id) && Random.Range(0, ++available) == 0)
                clothingId = id;
        // 衣服池用尽只能等回收，不能裸Box或重复衣服；额外装饰用尽不阻断生成。
        if (clothingId < 0) return false;
        available = 1; // -1表示不加额外装饰，也参与等概率选择。
        for (int id = 0; id < faceAccessories.Length + backAccessories.Length; ++id)
            if (!occupiedExtras.Contains(id) && Random.Range(0, ++available) == 0)
                extraId = id;
        return true;
    }

    private Level135QueueActor SpawnActor(bool refill, float centerX)
    {
        if (!TryChooseAppearance(out int clothingId, out int extraId)) return null;
        GameObject face = null;
        GameObject clothes = bodyAccessories[clothingId];
        GameObject back = null;
        if (extraId >= 0 && extraId < faceAccessories.Length)
            face = faceAccessories[extraId];
        else if (extraId >= faceAccessories.Length)
            back = backAccessories[extraId - faceAccessories.Length];
        Level135QueueActor actor = Instantiate(queueTemplate, transform);
        actor.name = refill ? "Level135_Refiller" : "Level135_QueueActor";
        if (!actor.Initialize(clothingId, extraId, face, clothes, back, refill))
        {
            actor.Recycle();
            Debug.LogError("[Level135] Queue template/accessory/hand binding invalid; gameplay stopped.", this);
            phase = Phase.Inactive;
            return null;
        }
        actor.gameObject.SetActive(true);
        // StartDoor 只给出生横向位置，脚底始终贴本关地面；身体中心而非 Spine 根对齐服务槽。
        Bounds bounds = actor.BodyBounds;
        actor.transform.position += new Vector3(centerX - bounds.center.x, ground.bounds.max.y - bounds.min.y, 0f);
        actor.SetPaused(paused);
        // 失败期间新补位者也不能被玩家尸体/击飞中的玩家阻挡；正常暂停不改变碰撞关系。
        if (IsPlayerFailurePhase) IgnoreQueueCollision(actor.BodyCollider);
        return actor;
    }

    private void ReindexSorting()
    {
        for (int i = 0; i < queue.Count; ++i) queue[i].SetSorting(110 - (queue.Count - 1 - i));
    }

    private void Depart(Level135QueueActor actor)
    {
        actor.Depart(PlayerOrder);
        leaving.Add(actor);
    }

    #endregion

    #region 沿用138的插队与补位失败
    private void CheckDanger()
    {
        if (colaPhase != ColaPhase.Idle || crowdCleared) return;
        Bounds playerBounds = playerBody.bounds;
        foreach (Level135QueueActor actor in queue)
        {
            if (IsQueueEntryFromAbove(actor, playerBounds)) { BeginFailure(true, HeadContact(actor, playerBounds), actor); return; }
        }
        if (inflight == null) return;
        if (IsQueueEntryFromAbove(inflight, playerBounds)) { BeginFailure(true, HeadContact(inflight, playerBounds), inflight); return; }
        if (!inflight.HasHit && playerBounds.center.x < TailX - 1f)
        {
            ColliderDistance2D contact = inflight.BodyCollider.Distance(playerBody);
            if (contact.isOverlapped || contact.distance <= 0.04f)
            {
                inflight.MarkHit();
                // 同时解除原生碰撞；后续移动也不再受玩家的边界限步阻挡。
                IgnoreQueueCollision(inflight.BodyCollider);
                BeginFailure(false, (contact.pointA + contact.pointB) * 0.5f);
            }
        }
    }

    private bool IsQueueEntryFromAbove(Level135QueueActor actor, Bounds playerBounds)
    {
        if (actor.IsLeaving || player.IsGround || playerRb.linearVelocityY > 0f) return false;
        Bounds bounds = actor.BodyBounds;
        bool horizontal = playerBounds.max.x > bounds.min.x && playerBounds.min.x < bounds.max.x;
        float feet = playerBounds.min.y;
        // 物理步可能已把落在NPC头顶的Y速度解算为0；仍需放行下落，不让NPC充当地板。
        bool descending = playerRb.linearVelocityY < 0f || previousFeetY > feet + 0.001f;
        bool touchingNpc = playerBody.IsTouching(actor.BodyCollider);
        return (descending || touchingNpc) && horizontal && feet <= bounds.max.y + headTouchHeight &&
            (feet >= bounds.max.y - 0.05f || previousFeetY >= bounds.max.y);
    }

    private static Vector3 HeadContact(Level135QueueActor actor, Bounds playerBounds)
    {
        Bounds bounds = actor.BodyBounds;
        return new Vector3(Mathf.Clamp(playerBounds.center.x, bounds.min.x, bounds.max.x), bounds.max.y,
            actor.transform.position.z);
    }

    private void IgnoreQueueCollisions()
    {
        foreach (Level135QueueActor actor in queue)
            if (actor != null && !actor.IsLeaving) IgnoreQueueCollision(actor.BodyCollider);
        if (inflight != null && !inflight.IsLeaving) IgnoreQueueCollision(inflight.BodyCollider);
    }

    private void IgnoreQueueCollision(Collider2D npcBody)
    {
        if (playerBody == null || npcBody == null) return;
        if (!queueCollisionSnapshot.ContainsKey(npcBody))
            queueCollisionSnapshot.Add(npcBody, Physics2D.GetIgnoreCollision(playerBody, npcBody));
        Physics2D.IgnoreCollision(playerBody, npcBody, true);
    }

    private void RestoreQueueCollisions()
    {
        foreach (KeyValuePair<Collider2D, bool> pair in queueCollisionSnapshot)
            if (playerBody != null && pair.Key != null)
                Physics2D.IgnoreCollision(playerBody, pair.Key, pair.Value);
        queueCollisionSnapshot.Clear();
    }

    private bool IsBlockedByQueue()
    {
        foreach (Level135QueueActor actor in queue)
        {
            ColliderDistance2D distance = actor.BodyCollider.Distance(playerBody);
            if (distance.isOverlapped || distance.distance < 0.05f) return true;
        }
        return false;
    }

    private void BeginFailure(bool fight, Vector3 point, Level135QueueActor opponent = null)
    {
        fightOpponent = fight ? opponent : null;
        phase = fight ? Phase.Fight : Phase.Knock;
        phaseElapsed = 0f;
        presentationSession = session;
        knockPoint = point;
        CancelPlayerService();
        colaInput.Clear();
        Vector2 fallingVelocity = playerRb.linearVelocity;
        player.SetMoveInput(0f);
        levelCtrl.SetCtrlable(false);
        player.StopMoving();
        IgnoreQueueCollisions();
        if (fight)
        {
            // 特效立即播放，但玩家不悬停：仅锁水平输入，恢复StopMoving清掉的下落速度。
            failureHolding = false;
            playerRb.linearVelocity = new Vector2(0f, Mathf.Min(0f, fallingVelocity.y));
        }
        else
        {
            // 补位敲击仍沿用原0.1秒停顿，不改变它的击飞时序。
            playerRb.simulated = false;
            failureHolding = true;
        }
        if (fight)
        {
            fightAnimation.transform.position = point;
            fightAnimation.gameObject.SetActive(true);
            fightAnimation.SetBool("isStop", false);
            fightAnimation.Play("Dog_Fight", 0, 0f);
            AudioManager.PlaySound("dogFight");
        }
    }

    private void LaunchPlayer()
    {
        bool fight = phase == Phase.Fight;
        HideEffects();
        if (fight) ShowFightWarning();
        if (!fight)
        {
            knockExplosion.transform.position = knockPoint;
            if (knockWord != null) knockWord.SetActive(false);
            knockExplosion.gameObject.SetActive(true);
            knockExplosion.Play(true);
        }
        AudioManager.PlaySound(fight ? "footballOut" : "trap");
        failureHolding = false;
        playerRb.simulated = true;
        // 边打斗边下落及后续击飞共用成对忽略，避免被NPC托住或弹回头顶。
        IgnoreQueueCollisions();
        // 左上45度：水平与竖直分量绝对值相同；力度50时初速度为(-25,25)。
        player.Knockback(fight ? new Vector2(-0.5f, 0.5f) : new Vector2(0.5f, 0.5f), knockForce).Forget();
        phase = Phase.Flying;
        // 不手动判负/移动到 FallArea，真实刚体飞入既有区域后走 BaseLevelCtrl 的失败链。
    }

    #endregion

    #region 暂停与旧关回收
    private void SetPaused(bool value)
    {
        if (paused == value) return;
        paused = value;
        colaInput?.Clear();
        if (started) ApplyPresentationPause(value);
    }

    private void OnGameStateChanged(SwitchGameStateEvent evt)
    {
        if (evt.NewState == GamePlayStateName.FAILED)
        {
            StopForFailure();
            SetPaused(false); // FAILED不是PAUSE，不把NPC流程误冻结。
            return;
        }
        SetPaused(evt.NewState != GamePlayStateName.PLAYING);
    }

    private void ApplyPresentationPause(bool value)
    {
        foreach (Level135QueueActor actor in queue) actor.SetPaused(!CanAdvanceQueue);
        foreach (Level135QueueActor actor in leaving) actor.SetPaused(!CanAdvanceQueue);
        inflight?.SetPaused(!CanAdvanceQueue);
        fightAnimation.speed = value ? 0f : 1f;
        if (value)
        {
            if (knockExplosion.isPlaying) knockExplosion.Pause(true);
            // 与003的共享Actor/出门流程对齐：普通出生、重置和暂停不接管玩家物理或Spine。
            // 仅本关已接管的失败演出需要完整暂停；守卫必须先于快照写入，避免恢复时回写旧速度。
            if (!IsPlayerFailurePhase || ownsPhysicsPause) return;
            playerSkeleton.timeScale = 0f;
            pauseVelocity = playerRb.linearVelocity;
            pauseSimulated = playerRb.simulated;
            ownsPhysicsPause = true;
            playerRb.simulated = false;
        }
        else
        {
            if (knockExplosion.isPaused) knockExplosion.Play(true);
            if (phase != Phase.Playing || playerOrderStarted)
            {
                player.StopMoving();
                // BaseLevelCtrl 的 Resume 监听也会开输入；下一 LateUpdate 只重锁，不清飞行速度。
                reapplyInputLock = true;
            }
            RestorePhysicsPause();
        }
    }

    private void RestorePhysicsPause()
    {
        if (!ownsPhysicsPause || playerRb == null) return;
        ownsPhysicsPause = false;
        if (playerSkeleton != null) playerSkeleton.timeScale = playerAnimationSpeed;
        playerRb.simulated = pauseSimulated && !failureHolding;
        playerRb.linearVelocity = pauseVelocity;
        // 恢复模拟后重申本次失败阶段的成对忽略，但不覆盖接管前快照。
        foreach (Collider2D npcBody in queueCollisionSnapshot.Keys)
            if (playerBody != null && npcBody != null)
                Physics2D.IgnoreCollision(playerBody, npcBody, true);
    }

    private void StopForFailure()
    {
        if (!started || resetPending || phase == Phase.Won || phase == Phase.Inactive || phase == Phase.Failed) return;
        phase = Phase.Failed;
        fightOpponent = null; // 已启动的警告仍走共享生命周期；未结束的打斗中断后不补播。
        CancelPlayerService();
        colaInput.Clear();
        Vector2 velocity = playerRb.linearVelocity;
        player.SetMoveInput(0f);
        levelCtrl.SetCtrlable(false);
        player.StopMoving();
        playerRb.linearVelocity = velocity;
        IgnoreQueueCollisions();
        HideEffects();
    }

    private void StopForReset()
    {
        if (!started || resetPending) return;
        resetPending = true;
        // 与共享ResetGame/ReplayGame对齐：只停止旧实例，初态由随后创建的新关卡负责。
        ++session;
        phase = Phase.Inactive;
        colaInput.Clear();
        levelCtrl.SetCtrlable(false);
        player.SetMoveInput(0f);
        player.StopMoving();
        HideEffects();
        CloseOrderBubble();
        CancelPlayerOrder();
        CloseWarningBubble();
        fightOpponent = null;
        refilling = false;
        if (!ownsPhysicsPause) ApplyPresentationPause(true);
        // 普通Reset只停旧实例，柜台和队列初态由下一实例负责。
    }



    private void HideEffects()
    {
        if (fightAnimation != null) fightAnimation.gameObject.SetActive(false);
        if (knockExplosion != null)
        {
            knockExplosion.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            knockExplosion.gameObject.SetActive(false);
        }
    }

    private void Cleanup()
    {
        ++session;
        phase = Phase.Inactive;
        refilling = false;
        started = false;
        EventsUtils.ResetEvents(ref subscriber);
        colaInput?.Clear();
        if (levelCtrl != null) levelCtrl.SetCtrlable(false);
        if (player != null)
        {
            player.SetMoveInput(0f);
            player.StopMoving();
        }
        HideEffects();
        DisposeSpeechBubbles();
        CancelService();
        StopColaEffect();
        playerCup?.Dispose();
        playerCup = null;
        RecycleQueue();
        RestorePhysicsPause();
        if (failureHolding && playerRb != null) playerRb.simulated = true;
        failureHolding = false;
        if (playerSkeleton != null)
        {
            playerSkeleton.transform.localRotation = initialBodyRotation;
        }
    }

    private void RecycleQueue()
    {
        // Reset、禁用和销毁共用此入口；按原值恢复，不能一律把外部原有ignore清为false。
        RestoreQueueCollisions();
        foreach (Level135QueueActor actor in queue) if (actor != null) actor.Recycle();
        foreach (Level135QueueActor actor in leaving) if (actor != null) actor.Recycle();
        queue.Clear();
        leaving.Clear();
        if (inflight != null) inflight.Recycle();
        inflight = null;
    }

    private void OnDisable() => Cleanup();
    private void OnDestroy() => Cleanup();
    #endregion
}
