using System;
using System.Collections.Generic;
using Bear.EventSystem;
using Bear.Logger;
using Bear.UI;
using Cysharp.Threading.Tasks;
using Game.Events;
using Game.Level;
using Game.Level105;
using Game.Play;
using Game.Scripts.Common;
using I2.Loc;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Level 105「不要操作」本地玩法编排器（组合形态，Level 103 先例）：
/// 通过序列化字段引用 Prefab 根节点的 <see cref="BaseLevelCtrl"/>；actor / successAnim / gamePlayPanelName
/// 继续走标准链，enterDoorAnim 刻意留空并由本关专属 openingEntrance 接管。纯规则收敛到零 Unity 依赖的
/// <see cref="Level105DontOperateRule"/>；本组件只适配本地起始门、Actor 物理、钥匙/宝箱表现、真实 UI、
/// 系统面板与标准胜负链。玩家输入和脚本驱动位移保持分离：任何非白名单输入进入统一判负入口，
/// 自动奔跑、递增的 5 次坑底跳与第 6 次脱坑跳均不经过玩家事件；这些脚本驱动的起跳绕过 ActorCtrl.Jump，
/// 统一在 LaunchPitJump 起跳瞬间补放与 ActorCtrl.Jump 同源的全项目通用跳跃音效 jump。
/// 落到安全坑底后 Box 先停住并弹出本关专属坑底重开假弹窗（<see cref="Level105PitRestartPopup"/>，
/// 与开箱确认弹窗同构但独立实现）：入场完成后完整展示 5 秒，零输入自动关闭且完全移除后才开始第 1 次
/// 坑底跳；「是」「否」按钮或空白处点击均按既有诱惑弹窗规格判负，不执行任何重开业务。
/// 第一扇门（FakeDoor）不再直接弹假胜利：先由 <see cref="Level105FakeDoorCtrl"/> 完整播放与 SuccessAnimCtrl
/// 对齐的进门成功表现，结束后才打开假胜利 Popup；Popup 展示期满关闭后同一假门重新开门，同一 Box 按门当前朝向
/// 向左走出，假门完整关门后 Domain 才进入奔向真门阶段。判负按 Box 可见进门 / 已隐藏收尾 / Popup 期间 / 出门期间
/// 分矩阵处理，最终共用同一标准死亡失败链。
/// 奔向真门途中新增两个诱惑点：Box 到达镜头即将切到第二屏的节点后原地踏步（保持走动画不位移）模拟卡顿，
/// 卡顿结束后弹出「当前网络未连接」断网假弹窗（<see cref="Level105NetworkPopup"/>，单「是」诱饵按钮）；
/// 弹窗移除后继续右行，中间路程节点停住后弹出仿系统来电的顶部横幅（<see cref="Level105CallPopup"/>，
/// 无遮罩，「接听」「挂断」双诱饵按钮，来电人名称绑定 S_LevelTips_Des_level_105_9「妈妈」）；
/// 横幅完全移除后再继续右行，距真门 5 个世界单位处再弹出「游戏更新」假弹窗（<see cref="Level105UpdatePopup"/>，
/// 「是」「否」双诱饵按钮）；三者均在入场完成后完整展示 5 秒并自动关闭，按钮或空白点击按既有规格判负。
/// 来电横幅从打开请求的同一流程节点起循环播放 zhendong 震动音，弹窗完全移除时统一停播，不依赖弹窗资源是否打开成功。
/// 所有死亡路径同帧播放死亡音效 passwordError（沿用 06 关密码输入错误音效），并同帧弹出 06 关「密码正确」
/// 同款 SystemTips 上浮提示（沿用原气泡 I2 key，简体中文「不可以点击任何地方哦！」）；
/// 落到安全坑底（「你死了」坑底重开弹窗同一流程节点）瞬间同样播放一次 passwordError，不依赖弹窗资源，也不弹上浮提示。
/// </summary>
public sealed class Level105Ctrl : MonoBehaviour, IDebuger, IEventSender
{
    #region Inspector

    /// <summary>承载标准输入开关、起始门、真门通关链与失败重开流程的关卡基础控制器。</summary>
    [SerializeField] private BaseLevelCtrl levelCtrl;

    /// <summary>从左侧起始门出场的主角 Box；为空时回退取 levelCtrl.Actor。</summary>
    [SerializeField] private ActorCtrl actor;

    /// <summary>StartDoor 上的 Level 105 专属可取消出场控制器。</summary>
    [SerializeField] private Level105EntranceCtrl openingEntrance;

    [Header("规则标定")]
    /// <summary>首次出场完成后及失败重开后的原地等待时长，固定为 5 秒。</summary>
    [SerializeField] private float openingIdleDuration = 5f;

    /// <summary>Box 到达关闭宝箱左侧并完全静止后的空场景等待时长，固定为 2 秒。</summary>
    [SerializeField] private float chestDelayDuration = 2f;

    /// <summary>开箱确认弹窗真实入场完成后的完整展示时长，固定为 5 秒。</summary>
    [SerializeField] private float chestConfirmShowingDuration = 5f;

    /// <summary>消费钥匙后的开箱演出时长，固定匹配 box-open 的 1.2 秒。</summary>
    [SerializeField] private float chestOpeningDuration = 1.2f;

    /// <summary>金币假弹窗与假胜利弹窗各自在真实入场完成后的完整展示时长，固定为 5 秒。</summary>
    [SerializeField] private float fakePopupTimeout = 5f;

    [Header("陷坑")]
    /// <summary>主角世界 X 达到该值后继续向右掉入固定坑洞；现有标定保持不变。</summary>
    [SerializeField] private float pitApproachActorX = -2.83f;

    /// <summary>固定坑口中心世界 X；与当前左右地面真实缺口中心对齐。</summary>
    [SerializeField] private float pitCenterX = -1f;

    /// <summary>固定坑口半宽；当前总宽 3，落坑与坑底闭环判定共用。</summary>
    [SerializeField] private float pitHalfWidth = 1.5f;

    /// <summary>坑底观察线；低于该值且落地时视为到达安全坑底。</summary>
    [SerializeField] private float pitEnterDetectY = -7.5f;

    /// <summary>第 6 次脱坑跳落上右侧地面的水平完成线。</summary>
    [SerializeField] private float pitExitRightX = 0.51f;

    /// <summary>
    /// 前五次坑底原地跳的竖直初速度；前四跳同高、每次顶点距钥匙约 0.78 均不触碰
    /// （按坑底承托面碰撞顶 -8.58 与钥匙碰撞底浮动区间 [-1.25, -1.05] 标定，触碰阈值约 23.43），
    /// 第五档 25 稳定接触拾取，并用于同高度的第六次脱坑跳。
    /// 具体值保持 Level 105 本地，运行时按已完成落地次数依次取值。
    /// </summary>
    [SerializeField] private float[] pitBounceVelocities = { 22f, 22f, 22f, 22f, 25f };

    [Header("钥匙与宝箱")]
    /// <summary>Level 105 本地钥匙跟随、宝箱 Spine 与 Shiny 的纯表现组件。</summary>
    [SerializeField] private Level105ChestFlowCtrl chestFlow;

    /// <summary>动态可见边界尚不可用时，Box 在宝箱左侧的安全停点；不得小于右侧坑沿落地点。</summary>
    [SerializeField] private float chestStopFallbackActorX = 2.51f;

    [Header("假弹窗与双门")]
    /// <summary>通过关卡专属 CommonGetRewardPopup Variant 展示 100 金币的适配器。</summary>
    [SerializeField] private Level105FakePopupCtrl coinPopup;

    /// <summary>通过关卡专属 GameVictoryPanel Variant 展示假胜利的适配器。</summary>
    [SerializeField] private Level105FakePopupCtrl fakeVictoryPopup;

    /// <summary>FakeDoor 上的 Level 105 专属可取消/可挂起真假门演出控制器；复用 door_0 自带资源。</summary>
    [SerializeField] private Level105FakeDoorCtrl fakeDoor;

    /// <summary>第二扇门的标准进入触发器；假胜利完整移除前保持关闭。</summary>
    [SerializeField] private Collider2D realDoorTrigger;

    /// <summary>假门停点世界 X，固定为 7.6（假门 8.1 左侧 0.5）；到达后先完整播放假门进门成功表现，不再直接弹假胜利。</summary>
    [SerializeField] private float fakeDoorStopActorX = 7.6f;

    [Header("第二屏断网卡顿与游戏更新弹窗")]
    /// <summary>Box 世界 X 达到该值后进入原地踏步卡顿；与 CameraFollowArea 死区右缘 10（中心 0.5 + 半宽 9.5）对齐，即镜头即将切到第二屏的节点。</summary>
    [SerializeField] private float networkStuckTriggerActorX = 10f;

    /// <summary>到达节点后的原地踏步卡顿时长，固定为 2 秒；卡顿结束后才弹出断网假弹窗。</summary>
    [SerializeField] private float networkStuckDuration = 4f;

    /// <summary>距真门多少个世界单位时弹出游戏更新假弹窗，固定为 5。</summary>
    [SerializeField] private float updatePopupDistanceBeforeRealDoor = 5f;

    /// <summary>真门触发器不可用时，游戏更新弹窗触发点的回退世界 X（真门 26.9 左侧 5）。</summary>
    [SerializeField] private float updatePopupTriggerFallbackActorX = 21.9f;

    /// <summary>断网弹窗移除后中间路程的来电横幅触发点世界 X，固定为 16（断网节点 10 与更新节点 21.9 之间）；到达后停住并弹出来电横幅。</summary>
    [SerializeField] private float callPopupTriggerActorX = 16f;

    /// <summary>来电横幅循环震动音 tag（Sounds Good 集合内注册）；挂在弹窗打开请求节点上，不依赖弹窗资源是否打开成功。</summary>
    private const string CallRingingSoundTag = "zhendong";

    [Header("死亡上浮提示")]
    // 死亡提示为 06 关「密码正确」同款 SystemTips 上浮提示链路：LocalizationManager.GetTranslation 解析 key 后
    // 弹出全局上浮 Toast，展示/淡出时长由共享 SystemTips 自身动画固定，关卡不再持有实例也无需收尾关闭。
    // key 沿用原死亡气泡词条 S_LevelTips_Des_level_105_1（多语言直接复用该现有 key，只需处理好简体中文；
    // 其他语言不设置，由 I2 OnMissingTranslation=Fallback 回退简体中文），Inspector 上可配。
    [SerializeField] private string deathTipKey = "S_LevelTips_Des_level_105_1";

    #endregion

    #region Runtime State

    private sealed class GameplayButtonBinding
    {
        public CustomButton Button;
        public bool PreviousIsBlockClick;
    }

    private readonly Level105DontOperateRule rule = new Level105DontOperateRule();
    private readonly List<GameplayButtonBinding> boundGameplayButtons = new List<GameplayButtonBinding>();

    private EventSubscriber subscriber;
    private bool deathStarted;
    private bool externalFlowSettled;

    private bool openingReadyPending;
    private bool pitRestartPopupReadyPending;
    private bool pitRestartPopupClosedPending;
    private bool chestConfirmReadyPending;
    private bool chestConfirmClosedPending;
    private bool coinPopupReadyPending;
    private bool fakeVictoryReadyPending;
    private bool pitRestartPopupRequested;
    private bool chestConfirmPopupRequested;
    private bool coinPopupRequested;
    private bool fakeVictoryPopupRequested;
    private bool networkPopupRequested;
    private bool callPopupRequested;
    private bool updatePopupRequested;
    private Level105PitRestartPopup pitRestartPopup;
    private Level105ChestConfirmPopup chestConfirmPopup;
    private Level105NetworkPopup networkPopup;
    private Level105CallPopup callPopup;
    private Level105UpdatePopup updatePopup;
    private int timedPhaseEnteredFrame = -1;
    private bool chestStopActorXResolved;
    private float chestStopActorX;

    private bool networkPopupReadyPending;
    private bool networkPopupClosedPending;
    private bool callPopupReadyPending;
    private bool callPopupClosedPending;
    private bool updatePopupReadyPending;
    private bool updatePopupClosedPending;
    private bool updatePopupTriggerResolved;
    private float updatePopupTriggerActorX;
    private MelenitasDev.SoundsGood.Sound callRingingSound;

    private bool fakeDoorEnterRequested;
    private bool fakeDoorEnterCompletedPending;
    private bool fakeDoorExitRequested;
    private bool fakeDoorExitCompletedPending;
    private bool pendingFakeDoorExitDeath;
    private bool fakeDoorExitDeathStarted;

    private bool pitBounceStarted;
    private bool pitBounceAirborne;
    private bool pitEscapeLaunched;
    private bool pitBounceAnchorCaptured;
    private float pitBounceAnchorX;

    private bool isSuspended;
    private bool resumePending;
    private int resumeAllowedFrame;
    private bool actorPhysicsSuspended;
    private bool actorWasSimulated;
    private bool deferPhaseAdvanceForPointerPress;
    private Vector2 lastObservedActorVelocity;
    private Vector2 suspendedActorVelocity;
    private float suspendedActorAngularVelocity;

    #endregion

    #region Unity Messages

    private void Awake()
    {
        // 规则与输入订阅在首个 Update 前建立，确保关卡出现第一帧即进入判负窗口。
        ResolveReferences();
        InjectRuleSettings();
        BindEvents();
        InitializeGameplay();
    }

    private void Start()
    {
        // PlayCtrl 在 Instantiate 后才创建 GameplayPanel；Start 时再绑定按钮，同时由本关启动本地出场。
        BindGameplayPanelButtons();
        StartOpeningFlowAsync().Forget();
    }

    private void Update()
    {
        UpdateScheduledResume();

        if (actor == null || externalFlowSettled)
            return;

        // 标准真门一旦已经写入 IsActorEnterDoor，必须先于本帧任何输入提交 Victory。
        TryCommitRealDoorVictory();
        if (rule.IsSettled)
            return;

        if (isSuspended)
        {
            EnsureActorPhysicsSuspended();
            return;
        }

        TryCommitPendingReadiness();

        // 屏幕输入先于本帧计时推进处理：空白/普通输入立即判负；弹窗按钮按下则保留当前阶段，
        // 等真实 CustomButton 在同帧锁定 Failure，避免恰逢 5 秒边界时先消费钥匙或关闭弹窗。
        deferPhaseAdvanceForPointerPress = false;
        DetectScreenPress();
        if (rule.IsSettled)
            return;
        if (deferPhaseAdvanceForPointerPress)
        {
            CacheActorVelocity();
            return;
        }

        Level105Phase phaseBeforeTick = rule.Phase;
        if (Time.frameCount != timedPhaseEnteredFrame)
            rule.Tick(Time.deltaTime);
        HandleTimedPhaseTransition(phaseBeforeTick, rule.Phase);

        TryCommitPendingReadiness();
        TryCommitRealDoorVictory();
        if (rule.IsSettled)
            return;

        UpdatePhaseTriggers(actor.transform.position.x);
        TryCommitPendingReadiness();
        TryCommitRealDoorVictory();
        if (rule.IsSettled)
            return;

        UpdateAutoRun();
        CacheActorVelocity();
    }

    private void OnDisable()
    {
        CleanupRuntime();
    }

    private void OnDestroy()
    {
        CleanupRuntime();
    }

    #endregion

    #region Initialization

    private void ResolveReferences()
    {
        levelCtrl ??= GetComponent<BaseLevelCtrl>();
        actor ??= levelCtrl != null ? levelCtrl.Actor : null;
        openingEntrance ??= transform.Find("StartDoor")?.GetComponent<Level105EntranceCtrl>();
        fakeDoor ??= transform.Find("FakeDoor")?.GetComponent<Level105FakeDoorCtrl>();
    }

    private void InjectRuleSettings()
    {
        rule.OpeningIdleDuration = openingIdleDuration;
        rule.ChestDelayDuration = chestDelayDuration;
        rule.ChestConfirmShowingDuration = chestConfirmShowingDuration;
        rule.ChestOpeningDuration = chestOpeningDuration;
        rule.PitRestartPopupShowingDuration = fakePopupTimeout;
        rule.CoinPopupShowingDuration = fakePopupTimeout;
        rule.FakeVictoryShowingDuration = fakePopupTimeout;
        rule.NetworkStuckDuration = networkStuckDuration;
        rule.NetworkPopupShowingDuration = fakePopupTimeout;
        rule.CallPopupShowingDuration = fakePopupTimeout;
        rule.UpdatePopupShowingDuration = fakePopupTimeout;
    }

    private void BindEvents()
    {
        EventsUtils.ResetEvents(ref subscriber);
        subscriber.Subscribe<PlayerRightMoveEvent>(OnPlayerRightMove);
        subscriber.Subscribe<PlayerLeftMoveEvent>(OnPlayerLeftMove);
        subscriber.Subscribe<PlayerJumpEvent>(OnPlayerJump);
        subscriber.Subscribe<GamePauseEvent>(OnGamePause);
        subscriber.Subscribe<GameResumeEvent>(OnGameResume);
        subscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
        subscriber.Subscribe<GameResetEvent>(OnGameReset);
    }

    private void BindGameplayPanelButtons()
    {
        UnbindGameplayPanelButtons();

        GamePlayPanel panel = PlayCtrl.Instance?.CurrentGamePlayPanel;
        if (panel == null)
            panel = FindFirstObjectByType<GamePlayPanel>();
        if (panel == null)
            return;

        CustomButton[] buttons = panel.GetComponentsInChildren<CustomButton>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            CustomButton button = buttons[i];
            if (button == null)
                continue;

            bool isSystemButton = TryResolveSystemButton(button.transform, out _);
            boundGameplayButtons.Add(new GameplayButtonBinding
            {
                Button = button,
                PreviousIsBlockClick = button.IsBlockClick
            });
            if (!isSystemButton)
                button.IsBlockClick = true;
            button.OnClickDown += OnGameplayButtonDown;
        }
    }

    private void UnbindGameplayPanelButtons()
    {
        for (int i = 0; i < boundGameplayButtons.Count; i++)
        {
            GameplayButtonBinding binding = boundGameplayButtons[i];
            CustomButton button = binding.Button;
            if (button == null)
                continue;

            button.OnClickDown -= OnGameplayButtonDown;
            button.IsBlockClick = binding.PreviousIsBlockClick;
        }

        boundGameplayButtons.Clear();
    }

    private void InitializeGameplay()
    {
        // 共享 Actor 默认开启 Editor 键盘轮询；Level 105 全程由脚本驱动，关闭该调试轮询。
        if (actor != null)
            actor.IsEditor = false;

        deathStarted = false;
        chestStopActorXResolved = false;
        pitRestartPopupRequested = false;
        pitRestartPopupReadyPending = false;
        pitRestartPopupClosedPending = false;
        chestConfirmPopupRequested = false;
        coinPopupRequested = false;
        fakeVictoryPopupRequested = false;
        networkPopupRequested = false;
        updatePopupRequested = false;
        chestConfirmReadyPending = false;
        chestConfirmClosedPending = false;
        coinPopupReadyPending = false;
        fakeVictoryReadyPending = false;
        networkPopupReadyPending = false;
        networkPopupClosedPending = false;
        callPopupRequested = false;
        callPopupReadyPending = false;
        callPopupClosedPending = false;
        updatePopupReadyPending = false;
        updatePopupClosedPending = false;
        updatePopupTriggerResolved = false;

        AbortPitRestartPopup();
        AbortChestConfirmPopup();
        AbortNetworkPopup();
        AbortCallPopup();
        AbortUpdatePopup();
        chestFlow?.ResetAttempt();
        // 金币假弹窗沿用既有“反馈后直接死亡”；假胜利按钮违规改为反馈后重新出门再死亡。
        coinPopup?.ConfigureFailureCallbacks(OnFakePopupButtonPressed, OnFakePopupButtonFeedbackCompleted);
        fakeVictoryPopup?.ConfigureFailureCallbacks(OnFakeVictoryButtonPressed, OnFakeVictoryButtonFeedbackCompleted);
        coinPopup?.ConfigurePresentationReadyCallback(OnCoinPopupPresentationReady);
        fakeVictoryPopup?.ConfigurePresentationReadyCallback(OnFakeVictoryPresentationReady);
        coinPopup?.AbortAndHide();
        fakeVictoryPopup?.AbortAndHide();
        SetRealDoorTriggerEnabled(false);

        PlayCtrl playCtrl = PlayCtrl.Instance;
        isSuspended = playCtrl != null && !playCtrl.CheckState(GamePlayStateName.PLAYING);
        rule.SetSuspended(isSuspended);
        chestFlow?.SetSuspended(isSuspended);
        openingEntrance?.SetSuspended(isSuspended);
        fakeDoor?.SetSuspended(isSuspended);
        if (isSuspended)
            EnsureActorPhysicsSuspended();
    }

    private async UniTaskVoid StartOpeningFlowAsync()
    {
        if (!ShouldPlayFirstOpening())
        {
            MarkOpeningReady();
            return;
        }

        if (openingEntrance == null)
        {
            MarkOpeningReady();
            return;
        }

        levelCtrl?.SetCtrlable(false);
        bool completed = await openingEntrance.Play();
        if (!completed ||
            rule.IsSettled ||
            externalFlowSettled ||
            actor == null ||
            actor.IsDied ||
            !isActiveAndEnabled)
        {
            return;
        }

        // 只允许完整出场且 Actor 仍存活的路径恢复控制；随后才开启可见 5 秒等待。
        actor.ResumeMoving();
        levelCtrl?.SetCtrlable(true);
        MarkOpeningReady();
    }

    private bool ShouldPlayFirstOpening()
    {
        var state = PlayCtrl.Instance?.Level?.CurrentLevelState;
        return state == null || (state.ResetCount <= 0 && state.FailCount <= 0);
    }

    #endregion

    #region Readiness And Timed Phase Transitions

    private void MarkOpeningReady()
    {
        openingReadyPending = true;
        TryCommitPendingReadiness();
    }

    private void OnPitRestartPopupPresentationReady()
    {
        pitRestartPopupReadyPending = true;
        TryCommitPendingReadiness();
    }

    private void OnPitRestartPopupClosed()
    {
        pitRestartPopup = null;
        pitRestartPopupClosedPending = true;
        TryCommitPendingReadiness();
    }

    private void OnChestConfirmPresentationReady()
    {
        chestConfirmReadyPending = true;
        TryCommitPendingReadiness();
    }

    private void OnChestConfirmPopupClosed()
    {
        chestConfirmPopup = null;
        chestConfirmClosedPending = true;
        TryCommitPendingReadiness();
    }

    private void OnCoinPopupPresentationReady()
    {
        coinPopupReadyPending = true;
        TryCommitPendingReadiness();
    }

    private void OnFakeVictoryPresentationReady()
    {
        fakeVictoryReadyPending = true;
        TryCommitPendingReadiness();
    }

    private void TryCommitPendingReadiness()
    {
        if (isSuspended || rule.IsSettled)
            return;

        if (openingReadyPending && rule.Phase == Level105Phase.WaitingForOpening)
        {
            if (rule.NotifyOpeningCompleted())
            {
                openingReadyPending = false;
                StopActorHorizontalMotion();
                MarkTimedPhaseEntered();
            }
        }

        if (pitRestartPopupReadyPending && rule.Phase == Level105Phase.PitRestartPopupEntering)
        {
            if (rule.NotifyPitRestartPopupEntered())
            {
                pitRestartPopupReadyPending = false;
                MarkTimedPhaseEntered();
            }
        }

        if (pitRestartPopupClosedPending && rule.Phase == Level105Phase.PitRestartPopupClosing)
        {
            if (rule.NotifyPitRestartPopupClosed())
            {
                pitRestartPopupClosedPending = false;
                MarkTimedPhaseEntered();
            }
        }

        if (chestConfirmReadyPending && rule.Phase == Level105Phase.ChestConfirmEntering)
        {
            if (rule.NotifyChestConfirmEntered())
            {
                chestConfirmReadyPending = false;
                MarkTimedPhaseEntered();
            }
        }

        if (chestConfirmClosedPending && rule.Phase == Level105Phase.ChestConfirmClosing)
        {
            if (rule.NotifyChestConfirmClosed())
            {
                chestConfirmClosedPending = false;
                chestFlow?.ConsumeKeyAndOpen();
                MarkTimedPhaseEntered();
            }
        }

        if (coinPopupReadyPending && rule.Phase == Level105Phase.CoinPopupEntering)
        {
            if (rule.NotifyCoinPopupEntered())
            {
                coinPopupReadyPending = false;
                MarkTimedPhaseEntered();
            }
        }

        if (fakeVictoryReadyPending && rule.Phase == Level105Phase.FakeVictoryEntering)
        {
            if (rule.NotifyFakeVictoryEntered())
            {
                fakeVictoryReadyPending = false;
                MarkTimedPhaseEntered();
            }
        }

        if (fakeDoorEnterCompletedPending && rule.Phase == Level105Phase.FakeDoorEntering)
        {
            if (rule.NotifyFakeDoorEnterCompleted())
            {
                fakeDoorEnterCompletedPending = false;
                OpenFakeVictoryPopup();
            }
        }

        if (fakeDoorExitCompletedPending && rule.Phase == Level105Phase.FakeDoorExiting)
        {
            if (rule.NotifyFakeDoorExitCompleted())
            {
                fakeDoorExitCompletedPending = false;
                SettleFakeDoorExitCompleted();
            }
        }

        if (networkPopupReadyPending && rule.Phase == Level105Phase.NetworkPopupEntering)
        {
            if (rule.NotifyNetworkPopupEntered())
            {
                networkPopupReadyPending = false;
                MarkTimedPhaseEntered();
            }
        }

        if (networkPopupClosedPending && rule.Phase == Level105Phase.NetworkPopupClosing)
        {
            if (rule.NotifyNetworkPopupClosed())
            {
                networkPopupClosedPending = false;
            }
        }

        if (callPopupReadyPending && rule.Phase == Level105Phase.CallPopupEntering)
        {
            if (rule.NotifyCallPopupEntered())
            {
                callPopupReadyPending = false;
                MarkTimedPhaseEntered();
            }
        }

        if (callPopupClosedPending && rule.Phase == Level105Phase.CallPopupClosing)
        {
            if (rule.NotifyCallPopupClosed())
            {
                callPopupClosedPending = false;
            }
        }

        if (updatePopupReadyPending && rule.Phase == Level105Phase.UpdatePopupEntering)
        {
            if (rule.NotifyUpdatePopupEntered())
            {
                updatePopupReadyPending = false;
                MarkTimedPhaseEntered();
            }
        }

        if (updatePopupClosedPending && rule.Phase == Level105Phase.UpdatePopupClosing)
        {
            if (rule.NotifyUpdatePopupClosed())
            {
                updatePopupClosedPending = false;
            }
        }
    }

    private void MarkTimedPhaseEntered()
    {
        // 避免把入场完成所在帧的整帧 deltaTime 计入刚开始的 5 秒展示。
        timedPhaseEnteredFrame = Time.frameCount;
    }

    private void HandleTimedPhaseTransition(Level105Phase before, Level105Phase after)
    {
        if (before == after)
            return;

        if (before == Level105Phase.PitRestartPopupShowing && after == Level105Phase.PitRestartPopupClosing)
        {
            ClosePitRestartPopupNaturally();
            return;
        }

        if (before == Level105Phase.ChestDelay && after == Level105Phase.ChestConfirmEntering)
        {
            OpenChestConfirmPopup();
            return;
        }

        if (before == Level105Phase.ChestConfirmShowing && after == Level105Phase.ChestConfirmClosing)
        {
            CloseChestConfirmPopupNaturally();
            return;
        }

        if (before == Level105Phase.ChestOpening && after == Level105Phase.CoinPopupEntering)
        {
            chestFlow?.TrySwitchChestToOpenIdle();
            OpenCoinPopup();
            return;
        }

        if (before == Level105Phase.CoinPopupShowing && after == Level105Phase.RunningToFakeDoor)
        {
            coinPopup?.PlayOut();
            return;
        }

        if (before == Level105Phase.FakeVictoryShowing && after == Level105Phase.FakeDoorExiting)
        {
            // 假胜利完整展示 5 秒后先移除 Popup，再由同一假门重新开门让同一 Box 向左出门。
            fakeVictoryPopup?.PlayOut();
            StartFakeDoorExitFlow();
        }

        if (before == Level105Phase.NetworkStuckWaiting && after == Level105Phase.NetworkPopupEntering)
        {
            // 原地踏步卡顿结束后恢复物理模拟，停住并弹出断网假弹窗。
            RestoreNetworkStuckPhysics();
            StopActorHorizontalMotion();
            OpenNetworkPopup();
            return;
        }

        if (before == Level105Phase.NetworkPopupShowing && after == Level105Phase.NetworkPopupClosing)
        {
            CloseNetworkPopupNaturally();
            return;
        }

        if (before == Level105Phase.CallPopupShowing && after == Level105Phase.CallPopupClosing)
        {
            CloseCallPopupNaturally();
            return;
        }

        if (before == Level105Phase.UpdatePopupShowing && after == Level105Phase.UpdatePopupClosing)
        {
            CloseUpdatePopupNaturally();
        }
    }

    private void OpenPitRestartPopup()
    {
        if (pitRestartPopupRequested || rule.Phase != Level105Phase.PitRestartPopupEntering)
            return;

        pitRestartPopupRequested = true;
        StopActorHorizontalMotion();
        pitRestartPopup = Level105PitRestartPopup.Open(
            OnPitRestartPopupButtonPressed,
            OnPitRestartPopupButtonFeedbackCompleted,
            OnPitRestartPopupPresentationReady,
            UILayer.Top);

        if (pitRestartPopup == null)
            OnPitRestartPopupPresentationReady();
    }

    private void ClosePitRestartPopupNaturally()
    {
        Level105PitRestartPopup popup = pitRestartPopup;
        if (popup == null)
        {
            OnPitRestartPopupClosed();
            return;
        }

        popup.PlayOut(OnPitRestartPopupClosed);
    }

    private void AbortPitRestartPopup()
    {
        Level105PitRestartPopup popup = pitRestartPopup;
        pitRestartPopup = null;
        if (popup != null)
            popup.Abort();
    }

    private void OpenChestConfirmPopup()
    {
        if (chestConfirmPopupRequested || rule.Phase != Level105Phase.ChestConfirmEntering)
            return;

        chestConfirmPopupRequested = true;
        StopActorHorizontalMotion();
        chestConfirmPopup = Level105ChestConfirmPopup.Open(
            OnChestConfirmButtonPressed,
            OnChestConfirmButtonFeedbackCompleted,
            OnChestConfirmPresentationReady,
            UILayer.Top);

        if (chestConfirmPopup == null)
            OnChestConfirmPresentationReady();
    }

    private void CloseChestConfirmPopupNaturally()
    {
        Level105ChestConfirmPopup popup = chestConfirmPopup;
        if (popup == null)
        {
            OnChestConfirmPopupClosed();
            return;
        }

        popup.PlayOut(OnChestConfirmPopupClosed);
    }

    private void AbortChestConfirmPopup()
    {
        Level105ChestConfirmPopup popup = chestConfirmPopup;
        chestConfirmPopup = null;
        if (popup != null)
            popup.Abort();
    }

    private void OnNetworkPopupPresentationReady()
    {
        networkPopupReadyPending = true;
        TryCommitPendingReadiness();
    }

    private void OnNetworkPopupClosed()
    {
        networkPopup = null;
        networkPopupClosedPending = true;
        TryCommitPendingReadiness();
    }

    private void OpenNetworkPopup()
    {
        if (networkPopupRequested || rule.Phase != Level105Phase.NetworkPopupEntering)
            return;

        networkPopupRequested = true;
        StopActorHorizontalMotion();
        networkPopup = Level105NetworkPopup.Open(
            OnNetworkPopupButtonPressed,
            OnNetworkPopupButtonFeedbackCompleted,
            OnNetworkPopupPresentationReady,
            UILayer.Top);

        if (networkPopup == null)
            OnNetworkPopupPresentationReady();
    }

    private void CloseNetworkPopupNaturally()
    {
        Level105NetworkPopup popup = networkPopup;
        if (popup == null)
        {
            OnNetworkPopupClosed();
            return;
        }

        popup.PlayOut(OnNetworkPopupClosed);
    }

    private void AbortNetworkPopup()
    {
        Level105NetworkPopup popup = networkPopup;
        networkPopup = null;
        if (popup != null)
            popup.Abort();
    }

    private void OnCallPopupPresentationReady()
    {
        callPopupReadyPending = true;
        TryCommitPendingReadiness();
    }

    private void OnCallPopupClosed()
    {
        callPopup = null;
        StopCallRinging();
        callPopupClosedPending = true;
        TryCommitPendingReadiness();
    }

    /// <summary>Box 在中间路程节点停住后打开来电横幅；震动音从本节点起循环播放，不依赖弹窗资源是否打开成功。</summary>
    private void OpenCallPopup()
    {
        if (callPopupRequested || rule.Phase != Level105Phase.CallPopupEntering)
            return;

        callPopupRequested = true;
        StopActorHorizontalMotion();
        StartCallRinging();
        callPopup = Level105CallPopup.Open(
            OnCallPopupButtonPressed,
            OnCallPopupButtonFeedbackCompleted,
            OnCallPopupPresentationReady,
            UILayer.Top);

        if (callPopup == null)
            OnCallPopupPresentationReady();
    }

    private void CloseCallPopupNaturally()
    {
        Level105CallPopup popup = callPopup;
        if (popup == null)
        {
            OnCallPopupClosed();
            return;
        }

        popup.PlayOut(OnCallPopupClosed);
    }

    private void AbortCallPopup()
    {
        StopCallRinging();
        Level105CallPopup popup = callPopup;
        callPopup = null;
        if (popup != null)
            popup.Abort();
    }

    private void StartCallRinging()
    {
        StopCallRinging();
        callRingingSound = AudioManager.PlaySound(CallRingingSoundTag, loop: true);
    }

    private void StopCallRinging()
    {
        if (callRingingSound == null)
            return;

        callRingingSound.Stop();
        callRingingSound = null;
    }

    private void OnUpdatePopupPresentationReady()
    {
        updatePopupReadyPending = true;
        TryCommitPendingReadiness();
    }

    private void OnUpdatePopupClosed()
    {
        updatePopup = null;
        updatePopupClosedPending = true;
        TryCommitPendingReadiness();
    }

    private void OpenUpdatePopup()
    {
        if (updatePopupRequested || rule.Phase != Level105Phase.UpdatePopupEntering)
            return;

        updatePopupRequested = true;
        StopActorHorizontalMotion();
        updatePopup = Level105UpdatePopup.Open(
            OnUpdatePopupButtonPressed,
            OnUpdatePopupButtonFeedbackCompleted,
            OnUpdatePopupPresentationReady,
            UILayer.Top);

        if (updatePopup == null)
            OnUpdatePopupPresentationReady();
    }

    private void CloseUpdatePopupNaturally()
    {
        Level105UpdatePopup popup = updatePopup;
        if (popup == null)
        {
            OnUpdatePopupClosed();
            return;
        }

        popup.PlayOut(OnUpdatePopupClosed);
    }

    private void AbortUpdatePopup()
    {
        Level105UpdatePopup popup = updatePopup;
        updatePopup = null;
        if (popup != null)
            popup.Abort();
    }

    private void OpenCoinPopup()
    {
        if (coinPopupRequested || rule.Phase != Level105Phase.CoinPopupEntering)
            return;

        coinPopupRequested = true;
        StopActorHorizontalMotion();
        if (coinPopup == null || !coinPopup.PlayIn())
            OnCoinPopupPresentationReady();
    }

    private void OpenFakeVictoryPopup()
    {
        if (fakeVictoryPopupRequested || rule.Phase != Level105Phase.FakeVictoryEntering)
            return;

        fakeVictoryPopupRequested = true;
        StopActorHorizontalMotion();
        if (fakeVictoryPopup == null || !fakeVictoryPopup.PlayIn())
            OnFakeVictoryPresentationReady();
    }

    #endregion

    #region Automatic Flow

    private void UpdateAutoRun()
    {
        if (actor == null)
            return;

        // 假门进门/出门表演期间 Actor 由 Level105FakeDoorCtrl 独占驱动，本组件不得覆盖其移动输入。
        if (rule.Phase == Level105Phase.FakeDoorEntering ||
            rule.Phase == Level105Phase.FakeDoorExiting)
        {
            return;
        }

        if (rule.Phase == Level105Phase.PitEscaping)
        {
            // 第 6 次跳只需越过右坑沿；到达既有完成线后立即取消水平推进，避免冲过宝箱左侧停点。
            bool shouldMoveRight = actor.transform.position.x < pitExitRightX;
            actor.SetMoveInput(shouldMoveRight ? 1f : 0f);
            if (!shouldMoveRight && actor.RB != null)
                actor.RB.linearVelocity = new Vector2(0f, actor.RB.linearVelocityY);
            return;
        }

        if (rule.Phase == Level105Phase.NetworkStuckWaiting)
        {
            // 断网卡顿：保持向右移动输入维持 walk 动画（HandleAnimators 只看 xInput 与落地射线），
            // 同时关闭物理模拟保证零位移零抖动；卡顿结束或判负时由 RestoreNetworkStuckPhysics 恢复。
            actor.SetMoveInput(1f);
            Rigidbody2D stuckBody = actor.RB;
            if (stuckBody != null && stuckBody.simulated)
                stuckBody.simulated = false;

            actor.UpdateAnimation();
            return;
        }

        actor.SetMoveInput(rule.IsAutoRunPhase ? 1f : 0f);
    }

    private void UpdatePhaseTriggers(float actorX)
    {
        switch (rule.Phase)
        {
            case Level105Phase.RunningToPit:
                if (actorX >= pitApproachActorX)
                    rule.NotifyPitApproached();
                break;

            case Level105Phase.FallingIntoPit:
            case Level105Phase.PitBouncing:
            case Level105Phase.PitEscaping:
                UpdatePitFlow();
                break;

            case Level105Phase.RunningToChest:
                float stopX = ResolveChestStopActorX();
                if (actorX >= stopX && rule.NotifyChestReached())
                    StopActorAtWorldX(stopX);
                break;

            case Level105Phase.RunningToFakeDoor:
                if (actorX >= fakeDoorStopActorX && rule.NotifyFakeDoorReached())
                {
                    StopActorHorizontalMotion();
                    StartFakeDoorEnterFlow();
                }
                break;

            case Level105Phase.RunningToRealDoor:
                if (actorX >= networkStuckTriggerActorX)
                    rule.NotifyNetworkNodeReached();
                TryCommitRealDoorVictory();
                break;

            case Level105Phase.RunningToCallNode:
                if (actorX >= callPopupTriggerActorX && rule.NotifyCallNodeReached())
                {
                    StopActorAtWorldX(callPopupTriggerActorX);
                    OpenCallPopup();
                }

                TryCommitRealDoorVictory();
                break;

            case Level105Phase.RunningToUpdateNode:
                float updateNodeX = ResolveUpdatePopupTriggerActorX();
                if (actorX >= updateNodeX && rule.NotifyUpdateNodeReached())
                {
                    StopActorAtWorldX(updateNodeX);
                    OpenUpdatePopup();
                }

                TryCommitRealDoorVictory();
                break;

            case Level105Phase.ApproachingRealDoor:
                TryCommitRealDoorVictory();
                break;
        }
    }

    /// <summary>断网卡顿结束或卡顿期间判负时恢复物理模拟；先清零存储速度再启用，避免恢复瞬间前冲。</summary>
    private void RestoreNetworkStuckPhysics()
    {
        Rigidbody2D body = actor != null ? actor.RB : null;
        if (body == null || body.simulated || actorPhysicsSuspended)
            return;

        body.linearVelocity = Vector2.zero;
        body.simulated = true;
    }

    /// <summary>假门之后奔向真门的各段右行（含断网/来电/更新弹窗之间的恢复段与最终逼近段）都允许真门判胜同帧优先。</summary>
    private bool IsRealDoorRunPhase =>
        rule.Phase == Level105Phase.RunningToRealDoor ||
        rule.Phase == Level105Phase.RunningToCallNode ||
        rule.Phase == Level105Phase.RunningToUpdateNode ||
        rule.Phase == Level105Phase.ApproachingRealDoor;

    private float ResolveUpdatePopupTriggerActorX()
    {
        if (updatePopupTriggerResolved)
            return updatePopupTriggerActorX;

        updatePopupTriggerActorX = updatePopupTriggerFallbackActorX;
        if (realDoorTrigger != null)
        {
            float realDoorX = realDoorTrigger.transform.position.x;
            updatePopupTriggerActorX = realDoorX - Mathf.Max(0f, updatePopupDistanceBeforeRealDoor);
        }

        updatePopupTriggerResolved = true;
        return updatePopupTriggerActorX;
    }

    private bool TryCommitRealDoorVictory()
    {
        if (rule.IsSettled ||
            !IsRealDoorRunPhase ||
            levelCtrl == null ||
            !levelCtrl.IsActorEnterDoor)
        {
            return false;
        }

        if (!rule.TryEnterRealDoor())
            return false;

        SetRealDoorTriggerEnabled(false);
        StopActorHorizontalMotion();
        return true;
    }

    private void SetRealDoorTriggerEnabled(bool enabled)
    {
        if (realDoorTrigger != null)
            realDoorTrigger.enabled = enabled;
    }

    private float ResolveChestStopActorX()
    {
        if (chestStopActorXResolved)
            return chestStopActorX;

        float fallback = Mathf.Max(pitExitRightX, chestStopFallbackActorX);
        chestStopActorX = fallback;
        if (chestFlow != null && chestFlow.TryGetActorStandXLeftOfChest(out float resolvedX))
            chestStopActorX = Mathf.Max(fallback, resolvedX);

        chestStopActorXResolved = true;
        return chestStopActorX;
    }

    private void StopActorAtWorldX(float worldX)
    {
        if (actor == null)
            return;

        Rigidbody2D body = actor.RB;
        if (body != null && body.simulated)
            body.position = new Vector2(worldX, body.position.y);
        else
        {
            Vector3 position = actor.transform.position;
            position.x = worldX;
            actor.transform.position = position;
        }

        StopActorHorizontalMotion();
    }

    #endregion

    #region Fake Door Enter / Exit Flow

    private void StartFakeDoorEnterFlow()
    {
        if (fakeDoorEnterRequested)
            return;

        fakeDoorEnterRequested = true;
        RunFakeDoorEnterAsync().Forget();
    }

    private async UniTaskVoid RunFakeDoorEnterAsync()
    {
        levelCtrl?.SetCtrlable(false);
        bool completed = fakeDoor == null || await fakeDoor.PlayEnter();
        if (!completed ||
            externalFlowSettled ||
            actor == null ||
            !isActiveAndEnabled)
        {
            return;
        }

        if (rule.Result == Level105Result.Failure)
        {
            // Box 已完全隐藏后的收尾期违规：成功表现已完整收尾，跳过假胜利，直接出门后死亡。
            StartFakeDoorExitThenDeath();
            return;
        }

        if (rule.IsSettled || actor.IsDied)
            return;

        // 成功表现全部完成后才允许打开假胜利 Popup；挂起时由就绪提交延后到恢复。
        fakeDoorEnterCompletedPending = true;
        TryCommitPendingReadiness();
    }

    private void StartFakeDoorExitFlow()
    {
        if (fakeDoorExitRequested)
            return;

        fakeDoorExitRequested = true;
        RunFakeDoorExitAsync().Forget();
    }

    private async UniTaskVoid RunFakeDoorExitAsync()
    {
        levelCtrl?.SetCtrlable(false);
        bool completed = fakeDoor == null || await fakeDoor.PlayExit();
        if (!completed ||
            externalFlowSettled ||
            actor == null ||
            !isActiveAndEnabled)
        {
            return;
        }

        if (pendingFakeDoorExitDeath || rule.Result == Level105Result.Failure)
        {
            // 收尾期 / Popup 期间 / 出门期间违规：假门完整关门后才死亡。
            PlayDeath();
            return;
        }

        if (rule.IsSettled || actor.IsDied)
            return;

        // 只有出门与完整关门全部结束后，Domain 才进入奔向真门阶段。
        fakeDoorExitCompletedPending = true;
        TryCommitPendingReadiness();
    }

    private void SettleFakeDoorExitCompleted()
    {
        // Box 停在门左侧保持朝左后，恢复标准控制并自动转右奔向真门；真门触发器此刻才打开。
        if (actor != null && !actor.IsDied)
            actor.ResumeMoving();
        levelCtrl?.SetCtrlable(true);
        SetRealDoorTriggerEnabled(true);
    }

    #endregion

    #region Pit 5+1 Jump

    private void UpdatePitFlow()
    {
        Rigidbody2D body = actor?.RB;
        if (body == null)
            return;

        Vector3 position = actor.transform.position;
        bool isInsidePitMouth = Mathf.Abs(position.x - pitCenterX) <= pitHalfWidth;
        bool isAtPitFloor = actor.IsGround && isInsidePitMouth && position.y <= pitEnterDetectY;

        if (rule.Phase == Level105Phase.FallingIntoPit)
        {
            if (!isAtPitFloor || !rule.NotifyPitFloorReached())
                return;

            // 落到安全坑底瞬间播放死亡音效 passwordError（与 PlayDeath 同一音效）：与「你死了」坑底重开弹窗
            // 同一流程节点，但不依赖弹窗资源是否成功打开。
            AudioManager.PlaySound("passwordError");

            // 落到安全坑底后先停住并弹出坑底重开弹窗；弹窗完全移除进入 PitBouncing 后才开始第 1 跳。
            pitBounceAnchorCaptured = true;
            pitBounceAnchorX = body.position.x;
            OpenPitRestartPopup();
            return;
        }

        if (rule.Phase == Level105Phase.PitBouncing)
        {
            LockPitBounceHorizontal(body);

            if (!pitBounceStarted)
            {
                if (isAtPitFloor)
                    LaunchPitJump(isEscapeJump: false);
                return;
            }

            if (!actor.IsGround)
            {
                pitBounceAirborne = true;
                TryCollectChestKeyDuringFifthBounce();
                return;
            }

            if (!pitBounceAirborne || !isAtPitFloor)
                return;

            pitBounceAirborne = false;
            if (!rule.NotifyPitBounceLanded())
                return;

            if (rule.Phase == Level105Phase.PitBouncing)
                LaunchPitJump(isEscapeJump: false);
            else if (rule.Phase == Level105Phase.PitEscaping)
                LaunchPitJump(isEscapeJump: true);

            return;
        }

        if (rule.Phase != Level105Phase.PitEscaping)
            return;

        if (!pitEscapeLaunched && isAtPitFloor)
            LaunchPitJump(isEscapeJump: true);

        bool hasReachedRightGround = actor.IsGround &&
                                     position.x >= pitExitRightX &&
                                     position.y > pitEnterDetectY;
        if (!hasReachedRightGround || !rule.NotifyPitEscapeLanded())
            return;

        pitBounceAnchorCaptured = false;
        pitBounceStarted = false;
        pitBounceAirborne = false;
        pitEscapeLaunched = false;
    }

    private void LockPitBounceHorizontal(Rigidbody2D body)
    {
        actor.SetMoveInput(0f);
        if (pitBounceAnchorCaptured)
            body.position = new Vector2(pitBounceAnchorX, body.position.y);
        body.linearVelocity = new Vector2(0f, body.linearVelocityY);
    }

    private void TryCollectChestKeyDuringFifthBounce()
    {
        if (rule.HasChestKey ||
            rule.CompletedPitBounceCount != rule.RequiredPitBounceCount - 1 ||
            chestFlow == null ||
            !chestFlow.IsActorTouchingKey())
        {
            return;
        }

        if (rule.NotifyChestKeyCollected())
            chestFlow.TryPickupKeyOnFifthJump();
    }

    private void LaunchPitJump(bool isEscapeJump)
    {
        Rigidbody2D body = actor?.RB;
        if (body == null)
            return;

        // 坑底 5 跳与第 6 次脱坑跳均绕过 ActorCtrl.Jump 直接写速度，统一在此补放全项目通用跳跃音效 jump（与 ActorCtrl.Jump 同源）。
        AudioManager.PlaySound("jump");

        float verticalVelocity = GetPitBounceVelocity(
            isEscapeJump ? rule.RequiredPitBounceCount - 1 : rule.CompletedPitBounceCount);

        if (isEscapeJump)
        {
            pitEscapeLaunched = true;
            pitBounceAnchorCaptured = false;
            actor.SetMoveInput(1f);
            body.linearVelocity = new Vector2(actor.MoveSpeed, verticalVelocity);
        }
        else
        {
            pitBounceStarted = true;
            pitBounceAirborne = false;
            actor.SetMoveInput(0f);
            body.linearVelocity = new Vector2(0f, verticalVelocity);
        }

        actor.UpdateAnimation();
    }

    private float GetPitBounceVelocity(int bounceIndex)
    {
        if (pitBounceVelocities == null || pitBounceVelocities.Length == 0)
            return 25f;

        int index = Mathf.Clamp(bounceIndex, 0, pitBounceVelocities.Length - 1);
        return Mathf.Max(0f, pitBounceVelocities[index]);
    }

    #endregion

    #region Local Suspension

    private void OnGamePause(GamePauseEvent evt)
    {
        SuspendLevelFlow();
    }

    private void OnGameResume(GameResumeEvent evt)
    {
        ScheduleResume();
    }

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        if (evt == null)
            return;

        if (evt.NewState == GamePlayStateName.PAUSE)
        {
            SuspendLevelFlow();
            return;
        }

        if (evt.NewState == GamePlayStateName.PLAYING)
        {
            ScheduleResume();
            return;
        }

        if (evt.NewState == GamePlayStateName.SUCCESS || evt.NewState == GamePlayStateName.FAILED)
            SetExternalFlowSettled();
    }

    private void OnGameReset(GameResetEvent evt)
    {
        SetExternalFlowSettled();
    }

    private void SuspendLevelFlow()
    {
        if (externalFlowSettled)
            return;

        resumePending = false;
        isSuspended = true;
        rule.SetSuspended(true);
        chestFlow?.SetSuspended(true);
        openingEntrance?.SetSuspended(true);
        fakeDoor?.SetSuspended(true);
        EnsureActorPhysicsSuspended();
    }

    private void ScheduleResume()
    {
        // 注意不能因 rule.IsSettled 拒绝恢复：判负后的延迟死亡窗口（假门收尾/出门）仍可被系统面板挂起，
        // 拒绝恢复会让该窗口永久冻结。已 Victory/进入外部流程时由 externalFlowSettled 拦截。
        if (externalFlowSettled)
            return;

        // 系统关闭按钮会先派发 PLAYING 再移除自身；延后一帧恢复，避免同一次关闭点击被误判为普通操作。
        resumePending = true;
        resumeAllowedFrame = Time.frameCount + 1;
    }

    private void UpdateScheduledResume()
    {
        if (!resumePending || Time.frameCount < resumeAllowedFrame)
            return;

        // 暂停/提示可继续打开语言、商店等奖励/设置子面板；只要真实 Popup/Top 遮罩仍可见就保持冻结。
        if (HasVisiblePopupMask())
        {
            resumeAllowedFrame = Time.frameCount + 1;
            return;
        }

        resumePending = false;
        isSuspended = false;
        rule.SetSuspended(false);
        chestFlow?.SetSuspended(false);
        RestoreActorPhysics();
        openingEntrance?.SetSuspended(false);
        fakeDoor?.SetSuspended(false);
        TryCommitPendingReadiness();
    }

    private static bool HasVisiblePopupMask()
    {
        UIMask[] masks = FindObjectsByType<UIMask>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < masks.Length; i++)
        {
            if (masks[i] != null && masks[i].IsVisible)
                return true;
        }

        return false;
    }

    private void EnsureActorPhysicsSuspended()
    {
        if (actor == null)
            return;

        actor.SetMoveInput(0f);
        Rigidbody2D body = actor.RB;
        if (body == null || actorPhysicsSuspended)
            return;

        actorPhysicsSuspended = true;
        actorWasSimulated = body.simulated;

        Vector2 currentVelocity = body.linearVelocity;
        suspendedActorVelocity = currentVelocity.sqrMagnitude > 0.0001f
            ? currentVelocity
            : lastObservedActorVelocity;
        suspendedActorAngularVelocity = body.angularVelocity;
        body.simulated = false;
    }

    private void RestoreActorPhysics()
    {
        if (!actorPhysicsSuspended)
            return;

        Rigidbody2D body = actor?.RB;
        actorPhysicsSuspended = false;
        if (body == null)
            return;

        body.simulated = actorWasSimulated;
        if (actorWasSimulated)
        {
            body.linearVelocity = suspendedActorVelocity;
            body.angularVelocity = suspendedActorAngularVelocity;
        }
    }

    private void CacheActorVelocity()
    {
        Rigidbody2D body = actor?.RB;
        if (body != null && body.simulated)
            lastObservedActorVelocity = body.linearVelocity;
    }

    private void SetExternalFlowSettled()
    {
        if (externalFlowSettled)
            return;

        externalFlowSettled = true;
        resumePending = false;
        isSuspended = true;
        rule.SetSuspended(true);
        StopActorHorizontalMotion();
        SetRealDoorTriggerEnabled(false);
        openingEntrance?.CancelForTeardown();
        AbortPitRestartPopup();
        AbortChestConfirmPopup();
        chestFlow?.Abort();
        fakeDoor?.CancelForTeardown();
        coinPopup?.AbortAndHide();
        fakeVictoryPopup?.AbortAndHide();
        AbortNetworkPopup();
        AbortCallPopup();
        AbortUpdatePopup();
    }

    #endregion

    #region Player Input And Failure

    private void OnPlayerRightMove(PlayerRightMoveEvent evt) => OnPlayerOperate();

    private void OnPlayerLeftMove(PlayerLeftMoveEvent evt) => OnPlayerOperate();

    private void OnPlayerJump(PlayerJumpEvent evt) => OnPlayerOperate();

    private void OnGameplayButtonDown(CustomButton button)
    {
        if (button == null || externalFlowSettled || rule.IsSettled)
            return;

        if (TryResolveSystemButton(button.transform, out Level105SystemButton systemButton))
        {
            rule.NotifySystemButtonInput(systemButton);
            return;
        }

        OnPlayerOperate();
    }

    private void DetectScreenPress()
    {
        if (Input.GetMouseButtonDown(0))
            HandleScreenPress(Input.mousePosition);

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Began)
                HandleScreenPress(touch.position);
        }
    }

    private void HandleScreenPress(Vector2 screenPos)
    {
        if (IsOverSystemButtonWhitelist(screenPos, out Level105SystemButton button))
        {
            deferPhaseAdvanceForPointerPress = true;
            rule.NotifySystemButtonInput(button);
            StopActorHorizontalMotion();
            return;
        }

        // Level 105 确认按钮及两个假弹窗按钮均由真实 CustomButton 按压反馈链处理，
        // 避免普通屏幕检测提前销毁 UI；弹窗空白区域仍会立即判负。
        if (IsOverActiveFakePopupButton(screenPos))
        {
            deferPhaseAdvanceForPointerPress = true;
            return;
        }

        OnPlayerOperate();
    }

    private bool IsOverSystemButtonWhitelist(Vector2 screenPos, out Level105SystemButton button)
    {
        button = default;
        if (EventSystem.current == null)
            return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current) { position = screenPos };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        for (int i = 0; i < results.Count; i++)
        {
            Transform node = results[i].gameObject != null ? results[i].gameObject.transform : null;
            if (TryResolveSystemButton(node, out button))
                return true;
        }

        return false;
    }

    private static bool TryResolveSystemButton(Transform node, out Level105SystemButton button)
    {
        button = default;
        while (node != null)
        {
            switch (node.name)
            {
                case "pause_btn":
                    button = Level105SystemButton.Pause;
                    return true;
                case "reset_btn":
                    button = Level105SystemButton.Restart;
                    return true;
                case "tips_btn":
                    button = Level105SystemButton.Hint;
                    return true;
            }

            node = node.parent;
        }

        return false;
    }

    private bool IsOverActiveFakePopupButton(Vector2 screenPos)
    {
        return (pitRestartPopup != null && pitRestartPopup.IsScreenPositionOverInteractiveButton(screenPos)) ||
               (chestConfirmPopup != null && chestConfirmPopup.IsScreenPositionOverInteractiveButton(screenPos)) ||
               (coinPopup != null && coinPopup.IsScreenPositionOverInteractiveButton(screenPos)) ||
               (fakeVictoryPopup != null && fakeVictoryPopup.IsScreenPositionOverInteractiveButton(screenPos)) ||
               (networkPopup != null && networkPopup.IsScreenPositionOverInteractiveButton(screenPos)) ||
               (callPopup != null && callPopup.IsScreenPositionOverInteractiveButton(screenPos)) ||
               (updatePopup != null && updatePopup.IsScreenPositionOverInteractiveButton(screenPos));
    }

    private void OnPlayerOperate()
    {
        if (TryCommitRealDoorVictory())
            return;

        // 同帧锁定 Failure；失败演出按违规发生时阶段分矩阵处理。
        Level105Phase phaseAtFailure = rule.Phase;
        if (!rule.NotifyPlayerOperateInput())
            return;

        // 出场期间同帧取消本地异步流程；其 finally 受门禁约束，不会恢复随后死亡的 Actor。
        openingEntrance?.CancelForFailure();
        RestoreNetworkStuckPhysics();
        AbortPitRestartPopup();
        AbortChestConfirmPopup();
        chestFlow?.Abort();
        coinPopup?.AbortAndHide();
        AbortNetworkPopup();
        AbortCallPopup();
        AbortUpdatePopup();
        HandleFakeDoorFailure(phaseAtFailure);
    }

    /// <summary>
    /// 假门相关阶段的判负演出矩阵：可见进门立即中断并死亡；已隐藏收尾 / Popup 期间 / 出门期间
    /// 都让当前表现完整走完，随后（或继续）重新开门出门，假门完整关门后才死亡。
    /// </summary>
    private void HandleFakeDoorFailure(Level105Phase phaseAtFailure)
    {
        switch (phaseAtFailure)
        {
            case Level105Phase.FakeDoorEntering:
                if (fakeDoor != null && fakeDoor.IsPlayingEnter && fakeDoor.IsActorHidden)
                {
                    // Box 已完全隐藏但成功表现仍在收尾：不中断收尾；进门完成回调会跳过假胜利并直接出门后死亡。
                    pendingFakeDoorExitDeath = true;
                    PrepareFailure();
                    return;
                }

                // Box 尚未完全进门隐藏：中断进门、恢复同一 Actor 正常尺寸/可见/物理并立即标准死亡；
                // 假门与死亡并行关门，不冻结半开。
                fakeDoor?.CancelEnterForFailure();
                fakeVictoryPopup?.AbortAndHide();
                PrepareFailure();
                PlayDeath();
                return;

            case Level105Phase.FakeVictoryEntering:
            case Level105Phase.FakeVictoryShowing:
                // 普通区域点击：立即关闭假胜利 Popup，随后开门、同一 Actor 左向出门、完整关门后死亡。
                fakeVictoryPopup?.AbortAndHide();
                pendingFakeDoorExitDeath = true;
                PrepareFailure();
                StartFakeDoorExitThenDeath();
                return;

            case Level105Phase.FakeDoorExiting:
                // 正常 Popup 超时后的出门期间：不中断出门/淡入/关门，门关后死亡。
                pendingFakeDoorExitDeath = true;
                PrepareFailure();
                return;

            default:
                fakeVictoryPopup?.AbortAndHide();
                PrepareFailure();
                PlayDeath();
                return;
        }
    }

    /// <summary>假胜利按钮按下：同帧锁定 Failure，保留约 0.2 秒原按压反馈；Popup 关闭由反馈完成回调衔接。</summary>
    private void OnFakeVictoryButtonPressed()
    {
        if (TryCommitRealDoorVictory())
            return;

        if (!rule.NotifyPlayerOperateInput())
            return;

        pendingFakeDoorExitDeath = true;
        PrepareFailure();
    }

    /// <summary>假胜利按钮反馈结束且 Popup 已销毁：随后开门、同一 Actor 左向出门、完整关门后死亡。</summary>
    private void OnFakeVictoryButtonFeedbackCompleted()
    {
        if (rule.Result != Level105Result.Failure)
            return;

        StartFakeDoorExitThenDeath();
    }

    private void StartFakeDoorExitThenDeath()
    {
        if (fakeDoorExitDeathStarted || deathStarted)
            return;

        fakeDoorExitDeathStarted = true;
        fakeVictoryPopup?.AbortAndHide();
        StartFakeDoorExitFlow();
    }

    /// <summary>坑底重开弹窗按钮按下：同帧锁定 Failure，保留约 0.2 秒原按压反馈；不执行任何重开业务。</summary>
    private void OnPitRestartPopupButtonPressed(Level105PitRestartPopup.Choice choice)
    {
        _ = choice;
        if (!rule.NotifyPlayerOperateInput())
            return;

        // 是/否任一按钮都同帧判负；弹窗自身保留约 0.2 秒原按压反馈后才关闭并回调播放死亡。
        PrepareFailure();
    }

    private void OnPitRestartPopupButtonFeedbackCompleted(Level105PitRestartPopup.Choice choice)
    {
        _ = choice;
        pitRestartPopup = null;
        if (rule.Result == Level105Result.Failure)
            PlayDeath();
    }

    private void OnChestConfirmButtonPressed(Level105ChestConfirmPopup.Choice choice)
    {
        _ = choice;
        if (!rule.NotifyPlayerOperateInput())
            return;

        // 是/否任一按钮都同帧判负；钥匙仍由 Domain 保持，宝箱不开启。
        // 弹窗自身保留约 0.2 秒原按压反馈后才关闭并回调播放死亡。
        PrepareFailure();
    }

    private void OnChestConfirmButtonFeedbackCompleted(Level105ChestConfirmPopup.Choice choice)
    {
        _ = choice;
        chestConfirmPopup = null;
        if (rule.Result == Level105Result.Failure)
        {
            chestFlow?.Abort();
            PlayDeath();
        }
    }

    /// <summary>断网假弹窗「是」按钮按下：同帧锁定 Failure，保留约 0.2 秒原按压反馈；不执行任何网络业务。</summary>
    private void OnNetworkPopupButtonPressed(Level105NetworkPopup.Choice choice)
    {
        _ = choice;
        if (!rule.NotifyPlayerOperateInput())
            return;

        PrepareFailure();
    }

    private void OnNetworkPopupButtonFeedbackCompleted(Level105NetworkPopup.Choice choice)
    {
        _ = choice;
        networkPopup = null;
        if (rule.Result == Level105Result.Failure)
            PlayDeath();
    }

    /// <summary>来电横幅「接听」「挂断」按钮按下：同帧锁定 Failure，保留约 0.2 秒原按压反馈；不执行任何电话业务。</summary>
    private void OnCallPopupButtonPressed(Level105CallPopup.Choice choice)
    {
        _ = choice;
        if (!rule.NotifyPlayerOperateInput())
            return;

        PrepareFailure();
    }

    private void OnCallPopupButtonFeedbackCompleted(Level105CallPopup.Choice choice)
    {
        _ = choice;
        callPopup = null;
        StopCallRinging();
        if (rule.Result == Level105Result.Failure)
            PlayDeath();
    }

    /// <summary>游戏更新假弹窗「是」「否」按钮按下：同帧锁定 Failure，保留约 0.2 秒原按压反馈；不执行任何更新业务。</summary>
    private void OnUpdatePopupButtonPressed(Level105UpdatePopup.Choice choice)
    {
        _ = choice;
        if (!rule.NotifyPlayerOperateInput())
            return;

        PrepareFailure();
    }

    private void OnUpdatePopupButtonFeedbackCompleted(Level105UpdatePopup.Choice choice)
    {
        _ = choice;
        updatePopup = null;
        if (rule.Result == Level105Result.Failure)
            PlayDeath();
    }

    private void OnFakePopupButtonPressed()
    {
        if (TryCommitRealDoorVictory())
            return;

        if (!rule.NotifyPlayerOperateInput())
            return;

        // 同帧锁定 Failure，但让真实按钮保留约 0.2 秒原按压反馈后再关闭 UI、播放死亡。
        PrepareFailure();
    }

    private void OnFakePopupButtonFeedbackCompleted()
    {
        if (rule.Result == Level105Result.Failure)
        {
            chestFlow?.Abort();
            PlayDeath();
        }
    }

    private void PrepareFailure()
    {
        SetRealDoorTriggerEnabled(false);
        StopActorHorizontalMotion();
    }

    private void StopActorHorizontalMotion()
    {
        if (actor == null)
            return;

        actor.SetMoveInput(0f);
        Rigidbody2D body = actor.RB;
        if (body != null && body.simulated)
            body.linearVelocity = new Vector2(0f, body.linearVelocityY);
        actor.UpdateAnimation();
    }

    private void PlayDeath()
    {
        if (deathStarted)
            return;

        deathStarted = true;
        if (actor != null)
        {
            // 死亡音效按需求沿用 06 关密码输入错误音效 passwordError，不使用框架判负音效 failed，并同帧弹出死亡上浮提示；
            // actor == null 兜底分支仍由 OnTriggerFailAreaEvent 链（BaseLevelCtrl.WaitingFail）自带 failed，
            // 不播放 passwordError 也不弹上浮提示，不在此重复。
            AudioManager.PlaySound("passwordError");
            actor.Die();
            ShowDeathTip();
            return;
        }

        this.DispatchEvent(Witness<OnTriggerFailAreaEvent>._);
    }

    /// <summary>
    /// 死亡同帧弹出 06 关「密码正确」同款 SystemTips 上浮提示（key 驱动，简体中文「不可以点击任何地方哦！」）；
    /// 与 Level 6 PasswordPopup.PlaySuccess 同一链路：LocalizationManager.GetTranslation 解析 key 后由共享
    /// SystemTips 走对象池弹出、按自身动画自动上浮淡出并回收，关卡侧不持有实例，重开与收尾均无需额外关闭。
    /// </summary>
    private void ShowDeathTip()
    {
        SystemTips.Show(LocalizationManager.GetTranslation(deathTipKey));
    }

    #endregion

    #region Cleanup

    private void CleanupRuntime()
    {
        EventsUtils.ResetEvents(ref subscriber);
        UnbindGameplayPanelButtons();
        openingEntrance?.CancelForTeardown();
        fakeDoor?.CancelForTeardown();
        RestoreActorPhysics();
        AbortPitRestartPopup();
        AbortChestConfirmPopup();
        chestFlow?.Cleanup();
        coinPopup?.AbortAndHide();
        fakeVictoryPopup?.AbortAndHide();
        AbortNetworkPopup();
        AbortCallPopup();
        AbortUpdatePopup();
    }

    #endregion
}
