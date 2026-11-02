using System;
using System.Collections.Generic;
using Bear.EventSystem;
using Bear.Logger;
using Bear.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Play;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// IWanner 玩法 HUD。
/// 输入、Reset 和暂停沿用主线/DrawGame 的事件与活动控制器边界；本类只保留 IWanner 组内进度显示差异。
/// </summary>
public partial class IWannerGame_GamePlayPanel : BaseUIView, IDebuger, IEventSender
{
    #region Inspector References

    /// <summary>
    /// IWanner 进入、Reset 和 Replay 使用的全屏黑幕。
    /// 调用边界：由当前 GameplayPanel prefab 绑定，不负责关卡内容或 Victory 遮罩。
    /// </summary>
    [Header("黑屏过场")]
    [SerializeField] private Image blackMask;

    /// <summary>
    /// 黑屏与关卡加载期间阻止 GameplayPanel 输入的全屏节点。
    /// 调用边界：只响应当前玩法控制器派发的 GamePlayPanelSwitchBlockEvent。
    /// </summary>
    [SerializeField] private GameObject clickBlock;

    /// <summary>
    /// 黑幕完全显示时的目标透明度。
    /// 注意事项：建议保持 1；降低后会在关卡重建期间透出场景内容。
    /// </summary>
    [SerializeField, Range(0f, 1f)] private float blackMaskVisibleAlpha = 1f;

    /// <summary>
    /// 从透明渐变到黑幕目标透明度的时长，单位秒。
    /// </summary>
    [SerializeField, Min(0f)] private float fadeToBlackDuration = 0.4f;

    /// <summary>
    /// 从黑幕目标透明度渐变到透明的时长，单位秒。
    /// </summary>
    [SerializeField, Min(0f)] private float fadeFromBlackDuration = 0.4f;

    /// <summary>
    /// 组内进度 Toggle 的横向容器。
    /// 调用边界：只读取其直接子节点，并按兄弟索引从左到右播放入场动画。
    /// 注意事项：直接子节点需要挂载不可交互的 Toggle，节点顺序必须与 Group 小关索引一致。
    /// </summary>
    [Header("组内进度入场")]
    [SerializeField] private RectTransform progressToggleGroup;

    /// <summary>
    /// 单个进度 Toggle 的上跃高度，单位为 UI 像素。
    /// 注意事项：动画会在黑幕完全淡出后播放，并在落地时恢复初始位置。
    /// </summary>
    [SerializeField, Min(0f)] private float progressToggleJumpHeight = 28f;

    /// <summary>
    /// 相邻进度 Toggle 的起跳间隔，单位秒。
    /// 注意事项：使用不受 timeScale 影响的 UI 时间。
    /// </summary>
    [SerializeField, Min(0f)] private float progressToggleStaggerInterval = 0.08f;

    /// <summary>
    /// 角色因失败区域死亡后显示的全屏重置点击区。
    /// 调用边界：仅由当前 IWannerPlayCtrl 切换显隐；点击后派发玩法专属失败重置事件。
    /// 注意事项：prefab 中必须默认隐藏，正常操作与加载期间不得拦截 HUD 输入。
    /// </summary>
    [Header("失败重置")]
    [SerializeField] private CustomButton resetClickArea;

    /// <summary>
    /// 使用当前关卡 GroundColor 的 HUD 图片列表。
    /// 调用边界：仅在关卡配色初始化时批量写入颜色；列表项为空时跳过，不参与输入或进度状态。
    /// </summary>
    [Header("Ground 配色")]
    [SerializeField] private List<Image> groundColorImages = new List<Image>();

    #endregion

    #region Runtime State

    private EventSubscriber _subscriber;
    private Func<bool> _skipHandler;
    private bool _inputEnabled;
    private bool _skipVisible;
    private bool _resetClickAreaVisible;
    private bool _isLeftEntered;
    private bool _isRightEntered;
    private bool _entryFadeCompleted;
    private bool _groupProgressInitialized;
    private bool _groupProgressAnimationStarted;
    private int _groupProgressLevelCount;
    private int _groupProgressCurrentIndex;
    private Sequence _groupProgressSequence;
    private readonly List<Toggle> _groupProgressToggles = new List<Toggle>();
    private readonly List<RectTransform> _animatedProgressRects = new List<RectTransform>();
    private readonly List<Vector2> _animatedProgressBasePositions = new List<Vector2>();
    private readonly List<Vector3> _animatedProgressBaseScales = new List<Vector3>();

    #endregion

    #region UI Lifecycle

    public override void OnCreate()
    {
        base.OnCreate();
        InitButtons();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        StopGroupProgressAnimation(true);
        _entryFadeCompleted = false;
        _groupProgressInitialized = false;
        _groupProgressAnimationStarted = false;
        ResetMoveInput(false);
        SetSkipVisible(false);
        SetResetClickAreaVisible(false);
        SetInputEnabled(false);
        ShowBlackMaskImmediately();
        SetClickBlockVisible(true);
        AddListener();
    }

    public override void OnClose()
    {
        SetInputEnabled(false);
        SetResetClickAreaVisible(false);
        EventsUtils.ResetEvents(ref _subscriber);
        StopGroupProgressAnimation(true);
        blackMask?.DOKill();
        HideBlackMaskImmediately();
        SetClickBlockVisible(false);
#if UNITY_EDITOR || DEBUG_MODE
        _interstitialDebugData = null;
#endif
        base.OnClose();
    }

    private void OnDestroy()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        StopGroupProgressAnimation(true);
        blackMask?.DOKill();
        ResetMoveInput(true);
        _skipHandler = null;
#if UNITY_EDITOR || DEBUG_MODE
        _interstitialDebugData = null;
#endif
    }

    #endregion

    #region Runtime Debug Overlay

#if UNITY_EDITOR || DEBUG_MODE
    /// <summary>
    /// IWanner 插屏资格调试快照；只保存控制器在正常运行态采集的数据，IMGUI 不读取 DB 或配置。
    /// </summary>
    internal sealed class InterstitialDebugData
    {
        internal bool StateAvailable;
        internal bool ActiveController;
        internal bool PolicyAvailable;
        internal bool BagAvailable;
        internal bool IsNoAdForTest;
        internal bool HasNoAds;
        internal string PendingPlacement;
        internal string RequestState;
        internal string LastRequestResult;
        internal int GroupOrder;
        internal int MinGroup;
        internal int VictoryCount;
        internal int VictoryInterval;
        internal bool VictoryIntervalTrigger;
        internal int PassedLevelCount;
        internal int PassedLevelThreshold;
        internal bool PassedLevelTrigger;
        internal bool VictoryTrigger;
        internal int FailureCount;
        internal int FailureThreshold;
        internal bool FailureTrigger;
        internal long LastInterstitialDate;
        internal long LastRewardAdDate;
        internal int InterstitialCooldown;
        internal int RewardCooldown;
        internal int InterstitialRemaining;
        internal int RewardRemaining;
        internal int EffectiveRemaining;
        internal bool VictoryPolicyReady;
        internal string VictoryBlockReasons;
        internal bool FailurePolicyReady;
        internal string FailureBlockReasons;
    }

    private const float DebugMargin = 20f;
    private const float DebugButtonWidth = 420f;
    private const float DebugButtonHeight = 60f;
    private const float DebugPanelWidth = 1000f;
    private const float DebugPanelHeight = 650f;
    private static bool s_isInterstitialDebugVisible;
    private static GUIStyle s_interstitialDebugGuiStyle;
    private static GUIStyle s_interstitialDebugButtonStyle;
    private InterstitialDebugData _interstitialDebugData;

    /// <summary>
    /// 用途：告诉 IWannerPlayCtrl 当前是否需要刷新插屏调试快照。
    /// 调用边界：仅用于 Editor PlayMode 或 DEBUG_MODE 的低频调试刷新。
    /// </summary>
    internal bool WantsInterstitialDebugData =>
        GMPanel.IsRuntimeDebugHostOpen && s_isInterstitialDebugVisible;

    /// <summary>
    /// 用途：接收控制器在正常运行态生成的插屏资格快照。
    /// 调用边界：仅由活动 IWannerPlayCtrl 调用；本方法不读取 DB、配置或运行时单例。
    /// </summary>
    internal void SetInterstitialDebugData(InterstitialDebugData debugData)
    {
        _interstitialDebugData = debugData;
    }

    /// <summary>
    /// 用途：在 GMPanel 已打开时提供 IWanner 插屏资格数据的 IMGUI 显隐按钮和只读展示。
    /// 调用边界：仅在 Editor PlayMode 或 DEBUG_MODE、当前玩法面板激活且 GMPanel 已打开时绘制。
    /// 注意事项：本入口只读取控制器快照，不访问 DB、配置或运行时单例，正式非调试构建不会编译。
    /// </summary>
    private void OnGUI()
    {
        if (!Application.isPlaying ||
            !isActiveAndEnabled ||
            !GMPanel.IsRuntimeDebugHostOpen)
        {
            return;
        }

        float buttonX = Mathf.Max(DebugMargin, Screen.width - DebugButtonWidth - DebugMargin);
        Rect buttonRect = new Rect(
            buttonX,
            DebugMargin,
            DebugButtonWidth,
            DebugButtonHeight);
        string buttonText = s_isInterstitialDebugVisible
            ? "隐藏 IWanner 插屏数据"
            : "显示 IWanner 插屏数据";
        if (GUI.Button(buttonRect, buttonText, GetInterstitialDebugButtonStyle()))
            s_isInterstitialDebugVisible = !s_isInterstitialDebugVisible;

        if (!s_isInterstitialDebugVisible)
            return;

        float panelWidth = Mathf.Min(DebugPanelWidth, Screen.width - DebugMargin * 2f);
        if (panelWidth <= 0f)
            return;

        Rect panelRect = new Rect(
            Screen.width - panelWidth - DebugMargin,
            DebugMargin + DebugButtonHeight + 10f,
            panelWidth,
            DebugPanelHeight);
        GUI.Box(panelRect, GUIContent.none);

        Rect contentRect = new Rect(
            panelRect.x + 16f,
            panelRect.y + 10f,
            panelRect.width - 32f,
            panelRect.height - 20f);

        if (_interstitialDebugData == null)
        {
            GUI.Label(
                contentRect,
                "IWanner 插屏触发数据\n等待运行态快照...",
                GetInterstitialDebugGuiStyle());
            return;
        }

        InterstitialDebugData data = _interstitialDebugData;
        GUI.Label(
            contentRect,
            $"IWanner 插屏触发数据  Placement:{data.PendingPlacement}\n" +
            $"当前请求  {data.RequestState}\n" +
            $"最近 Helper 请求  {data.LastRequestResult}\n" +
            $"失败位来源  Reset / Failed（Failed 后进入重建时请求）\n" +
            $"胜利位来源  完整 Group 通关（普通小关不计数、不请求）\n" +
            $"基础门禁  ActiveCtrl:{data.ActiveController}  Policy:{data.PolicyAvailable}  " +
            $"State:{data.StateAvailable}  Bag:{data.BagAvailable}\n" +
            $"免广告门禁  NoAdTest:{data.IsNoAdForTest}  HasNoAds:{data.HasNoAds}\n" +
            $"胜利触发  Group:{data.GroupOrder} / Min:{data.MinGroup}  " +
            $"Count:{data.VictoryCount} / ADRangeLevelCount:{data.VictoryInterval}  " +
            $"RangeTrigger:{data.VictoryIntervalTrigger}\n" +
            $"兼容小关触发  Count:{data.PassedLevelCount} / Threshold:{data.PassedLevelThreshold}  " +
            $"Trigger:{data.PassedLevelTrigger}  VictoryTrigger:{data.VictoryTrigger}  " +
            $"PolicyReady:{data.VictoryPolicyReady}\n" +
            $"失败触发  Count:{data.FailureCount} / Threshold:{data.FailureThreshold}  " +
            $"Trigger:{data.FailureTrigger}  PolicyReady:{data.FailurePolicyReady}\n" +
            $"插屏冷却  Remain:{data.InterstitialRemaining} / Config:{data.InterstitialCooldown}  " +
            $"Last:{data.LastInterstitialDate}\n" +
            $"激励冷却  Remain:{data.RewardRemaining} / Config:{data.RewardCooldown}  " +
            $"Last:{data.LastRewardAdDate}  Effective:{data.EffectiveRemaining}\n" +
            $"胜利位策略阻断原因  {data.VictoryBlockReasons}\n" +
            $"失败位策略阻断原因  {data.FailureBlockReasons}",
            GetInterstitialDebugGuiStyle());
    }

    private static GUIStyle GetInterstitialDebugGuiStyle()
    {
        if (s_interstitialDebugGuiStyle != null)
            return s_interstitialDebugGuiStyle;

        s_interstitialDebugGuiStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperLeft,
            fontSize = 24,
            wordWrap = true,
            normal =
            {
                textColor = Color.white
            }
        };
        return s_interstitialDebugGuiStyle;
    }

    private static GUIStyle GetInterstitialDebugButtonStyle()
    {
        if (s_interstitialDebugButtonStyle != null)
            return s_interstitialDebugButtonStyle;

        s_interstitialDebugButtonStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 24
        };
        return s_interstitialDebugButtonStyle;
    }
#endif

    #endregion

    #region Player Input

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        // 失败重置独立于常规 HUD 输入；空格与全屏点击区共用入口，按住不会连续触发。
        if (_resetClickAreaVisible && Input.GetKeyDown(KeyCode.Space))
        {
            OnClickFailureReset(resetClickArea);
            return;
        }

        if (!_inputEnabled)
            return;

        if (_isRightEntered)
            this.DispatchEvent(Witness<PlayerRightMoveEvent>._);
        else if (_isLeftEntered)
            this.DispatchEvent(Witness<PlayerLeftMoveEvent>._);
    }

    private void OnMoveEnterLeft(CustomButton btn)
    {
        if (!_inputEnabled)
            return;

        _isLeftEntered = true;
        _isRightEntered = false;
    }

    private void OnMoveExitLeft(CustomButton btn)
    {
        if (!_isLeftEntered)
            return;

        _isLeftEntered = false;
        DispatchMoveCancelIfIdleAndEnabled();
    }

    private void OnMoveEnterRight(CustomButton btn)
    {
        if (!_inputEnabled)
            return;

        _isRightEntered = true;
        _isLeftEntered = false;
    }

    private void OnMoveExitRight(CustomButton btn)
    {
        if (!_isRightEntered)
            return;

        _isRightEntered = false;
        DispatchMoveCancelIfIdleAndEnabled();
    }

    private void OnClickJump(CustomButton btn)
    {
        if (_inputEnabled)
            this.DispatchEvent(Witness<PlayerJumpEvent>._);
    }

    private void DispatchMoveCancelIfIdleAndEnabled()
    {
        if (_inputEnabled && !_isLeftEntered && !_isRightEntered)
            this.DispatchEvent(Witness<PlayerMoveCancelEvent>._);
    }

    private void ResetMoveInput(bool dispatchCancel)
    {
        bool wasMoving = _isLeftEntered || _isRightEntered;
        _isLeftEntered = false;
        _isRightEntered = false;

        if (dispatchCancel && wasMoving)
            this.DispatchEvent(Witness<PlayerMoveCancelEvent>._);
    }

    #endregion

    #region Transition Events

    private void AddListener()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<GamePlayPanelFadeInEvent>(OnGamePlayFadeIn);
        _subscriber.Subscribe<GamePlayPanelSwitchBlockEvent>(OnSwitchBlock);
    }

    private void OnSwitchBlock(GamePlayPanelSwitchBlockEvent evt)
    {
        SetClickBlockVisible(evt.IsShow);
    }

    private void OnGamePlayFadeIn(GamePlayPanelFadeInEvent evt)
    {
        if (blackMask == null)
        {
            CompleteEntryFadeIn();
            return;
        }

        blackMask.DOKill();
        blackMask.gameObject.SetActive(true);
        SetBlackMaskAlpha(blackMaskVisibleAlpha);

        float duration = Mathf.Max(0f, fadeFromBlackDuration);
        if (duration <= 0f)
        {
            CompleteEntryFadeIn();
            return;
        }

        blackMask
            .DOFade(0f, duration)
            .SetEase(Ease.Linear)
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(CompleteEntryFadeIn);
    }

    private void CompleteEntryFadeIn()
    {
        HideBlackMaskImmediately();
        _entryFadeCompleted = true;
        TryStartGroupProgressAnimation();
    }

    /// <summary>
    /// 用途：在当前小关销毁前等待黑幕完全显示。
    /// 调用边界：仅由活动 IWannerPlayCtrl 的 Reset、Replay 和后续切关流程调用。
    /// 注意事项：使用不受 timeScale 影响的 UI 时间；缺少黑幕引用时立即返回，由 ClickBlock 继续保护输入。
    /// </summary>
    internal async UniTask FadeToBlackAsync()
    {
        if (blackMask == null)
            return;

        blackMask.DOKill();
        if (!blackMask.gameObject.activeSelf)
        {
            blackMask.gameObject.SetActive(true);
            SetBlackMaskAlpha(0f);
        }

        float duration = Mathf.Max(0f, fadeToBlackDuration);
        if (duration <= 0f)
        {
            SetBlackMaskAlpha(blackMaskVisibleAlpha);
            return;
        }

        Tween fadeTween = blackMask
            .DOFade(Mathf.Clamp01(blackMaskVisibleAlpha), duration)
            .SetEase(Ease.Linear)
            .SetUpdate(true)
            .SetLink(gameObject);
        await fadeTween.AsyncWaitForCompletion();
    }

    /// <summary>
    /// 用途：向 IWannerPlayCtrl 提供当前黑幕渐隐等待时长。
    /// 调用边界：控制器派发 GamePlayPanelFadeInEvent 后读取，用于在渐隐完成前保持关卡输入关闭。
    /// </summary>
    internal float FadeFromBlackDuration => Mathf.Max(0f, fadeFromBlackDuration);

    private void ShowBlackMaskImmediately()
    {
        if (blackMask == null)
            return;

        blackMask.DOKill();
        blackMask.gameObject.SetActive(true);
        SetBlackMaskAlpha(blackMaskVisibleAlpha);
    }

    private void HideBlackMaskImmediately()
    {
        if (blackMask == null)
            return;

        blackMask.DOKill();
        SetBlackMaskAlpha(0f);
        blackMask.gameObject.SetActive(false);
    }

    private void SetBlackMaskAlpha(float alpha)
    {
        if (blackMask == null)
            return;

        Color color = blackMask.color;
        color.a = Mathf.Clamp01(alpha);
        blackMask.color = color;
    }

    private void SetClickBlockVisible(bool visible)
    {
        if (clickBlock != null)
            clickBlock.SetActive(visible);
    }

    #endregion

    #region Button Handlers

    private void InitButtons()
    {
        if (LeftBtn != null)
        {
            LeftBtn.OnClickEnter += OnMoveEnterLeft;
            LeftBtn.OnClickExit += OnMoveExitLeft;
        }

        if (RightBtn != null)
        {
            RightBtn.OnClickEnter += OnMoveEnterRight;
            RightBtn.OnClickExit += OnMoveExitRight;
        }

        if (JumpBtn != null)
            JumpBtn.OnClickDown += OnClickJump;

        if (ResetBtn != null)
            ResetBtn.OnClick += OnClickReset;

        if (resetClickArea != null)
            resetClickArea.OnClick += OnClickFailureReset;

        if (SkipBtn != null)
            SkipBtn.OnClick += OnClickSkip;

        if (PauseBtn != null)
            PauseBtn.OnClick += OnClickPause;
    }

    private void OnClickReset(CustomButton btn)
    {
        if (_inputEnabled)
            this.DispatchEvent(Witness<IWannerGameResetEvent>._, GameResetType.Manually);
    }

    private void OnClickFailureReset(CustomButton btn)
    {
        if (!_resetClickAreaVisible)
            return;

        this.DispatchEvent(Witness<IWannerGameResetEvent>._, GameResetType.Failed);
    }

    private void OnClickSkip(CustomButton btn)
    {
        if (!_inputEnabled || !_skipVisible)
            return;

        if (_skipHandler == null || !_skipHandler())
            this.LogWarning("[IWannerGame_GamePlayPanel] Skip request was rejected by the active IWanner controller.");
    }

    private void OnClickPause(CustomButton btn)
    {
        if (!_inputEnabled)
            return;

        IPlayPauseHost pauseHost = GameManager.Instance?.ControllerManager?.ActivePauseHost;
        if (pauseHost == null)
        {
            this.LogWarning("[IWannerGame_GamePlayPanel] Active pause host is missing.");
            return;
        }

        pauseHost.Pause();
        GameSettingPopup.Create(true, pauseHost);
    }

    #endregion

    #region View API

    /// <summary>
    /// 用途：应用当前 IWanner 小关配置的 GameplayPanel 图片、Skip 文本与 Ground HUD 图片颜色。
    /// 调用边界：仅由活动 IWannerPlayCtrl 在关卡初始化成功、黑幕淡出前调用。
    /// 注意事项：Ground 颜色只作用于 groundColorImages，文字颜色只作用于 u_level_devil_btn_skip_txt。
    /// </summary>
    internal void ApplyGameplayColors(Color imageColor, Color skipTextColor, Color groundColor)
    {
        ApplyImageColor(UIYouxizhong3Chonglai01Img, imageColor);
        ApplyImageColor(UIYouxizhong3Shang01Img, imageColor);
        ApplyImageColor(UIYouxizhong3Tiaoguo01Img, imageColor);
        ApplyImageColor(UIYouxizhong3You01Img, imageColor);
        ApplyImageColor(UIYouxizhong3Zanting01Img, imageColor);
        ApplyImageColor(UIYouxizhong3Zuo01Img, imageColor);
        ApplyImageColors(UIYouxizhong3Weiguoguan01Imgs, imageColor);
        ApplyImageColors(UIYouxizhong3Yiguoguan01Imgs, imageColor);
        ApplyImageColors(groundColorImages, groundColor);

        if (ULevelDevilSkipTxtBtn != null)
            ULevelDevilSkipTxtBtn.color = skipTextColor;
    }

    /// <summary>
    /// 用途：统一开启或关闭当前 IWanner HUD 输入。
    /// 调用边界：仅由活动 IWannerPlayCtrl 在加载、暂停、恢复、Reset、Replay 和退出边界调用。
    /// 注意事项：关闭时立即清空方向状态并派发一次移动取消，Skip 可见性独立管理。
    /// </summary>
    internal void SetInputEnabled(bool enabledInput)
    {
        if (!enabledInput)
            ResetMoveInput(true);

        _inputEnabled = enabledInput;
        SetButtonInteractable(LeftBtn, enabledInput);
        SetButtonInteractable(RightBtn, enabledInput);
        SetButtonInteractable(JumpBtn, enabledInput);
        SetButtonInteractable(ResetBtn, enabledInput);
        SetButtonInteractable(PauseBtn, enabledInput);
        RefreshSkipButtonState();
    }

    /// <summary>
    /// 用途：切换角色死亡后的全屏重置点击区。
    /// 调用边界：仅由活动 IWannerPlayCtrl 在失败结果与重建边界调用。
    /// 注意事项：该点击区独立于常规 HUD 输入；活动控制器接受事件后必须先隐藏再开始重建。
    /// </summary>
    internal void SetResetClickAreaVisible(bool visible)
    {
        _resetClickAreaVisible = visible;
        if (resetClickArea == null)
            return;

        resetClickArea.Interactable = visible;
        resetClickArea.gameObject.SetActive(visible);
    }

    /// <summary>
    /// 用途：为本次新建的 GameplayPanel 缓存组内进度状态，并准备从左到右的 Q 弹入场动画。
    /// 调用边界：每个面板实例只在 Create 后初始化一次；currentLevelIndex 从 0 开始。
    /// 注意事项：状态会在全部 Toggle 动画完成后统一刷新；配置数量超出绑定容量时不会动态创建 UI。
    /// </summary>
    private void InitializeGroupProgress(int currentLevelIndex, int levelCount)
    {
        StopGroupProgressAnimation(true);
        _groupProgressLevelCount = Mathf.Max(0, levelCount);
        _groupProgressCurrentIndex = _groupProgressLevelCount > 0
            ? Mathf.Clamp(currentLevelIndex, 0, _groupProgressLevelCount - 1)
            : 0;
        _groupProgressInitialized = true;
        _groupProgressAnimationStarted = false;

        if (!CacheGroupProgressToggles())
        {
            this.LogWarning(
                "[IWannerGame_GamePlayPanel] Progress Toggle group is missing; fallback to generated Image bindings.");
            RefreshProgressImages(
                UIYouxizhong3Yiguoguan01Imgs,
                _groupProgressLevelCount,
                _groupProgressCurrentIndex,
                true);
            RefreshProgressImages(
                UIYouxizhong3Weiguoguan01Imgs,
                _groupProgressLevelCount,
                _groupProgressCurrentIndex,
                false);
            return;
        }

        if (_groupProgressLevelCount > _groupProgressToggles.Count)
        {
            this.LogWarning(
                $"[IWannerGame_GamePlayPanel] Configured level count exceeds progress Toggle capacity. " +
                $"levelCount:{_groupProgressLevelCount}, capacity:{_groupProgressToggles.Count}.");
        }

        PrepareGroupProgressToggles();
        TryStartGroupProgressAnimation();
    }

    private bool CacheGroupProgressToggles()
    {
        _groupProgressToggles.Clear();
        if (progressToggleGroup == null)
            return false;

        for (int i = 0; i < progressToggleGroup.childCount; i++)
        {
            Transform child = progressToggleGroup.GetChild(i);
            Toggle toggle = child.GetComponent<Toggle>();
            if (toggle != null)
                _groupProgressToggles.Add(toggle);
        }

        return _groupProgressToggles.Count > 0;
    }

    private void PrepareGroupProgressToggles()
    {
        for (int i = 0; i < _groupProgressToggles.Count; i++)
        {
            Toggle toggle = _groupProgressToggles[i];
            if (toggle == null)
                continue;

            bool isConfiguredSlot = i < _groupProgressLevelCount;
            toggle.gameObject.SetActive(isConfiguredSlot);
            if (isConfiguredSlot)
                toggle.SetIsOnWithoutNotify(false);
        }
    }

    private void TryStartGroupProgressAnimation()
    {
        if (!_entryFadeCompleted
            || !_groupProgressInitialized
            || _groupProgressAnimationStarted
            || _groupProgressToggles.Count == 0)
        {
            return;
        }

        _groupProgressAnimationStarted = true;
        int animatedCount = Mathf.Min(_groupProgressLevelCount, _groupProgressToggles.Count);
        if (animatedCount <= 0)
        {
            RefreshGroupProgressToggleState();
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(progressToggleGroup);
        CacheGroupProgressTransforms(animatedCount);
        int animatedRectCount = _animatedProgressRects.Count;
        if (animatedRectCount <= 0)
        {
            RefreshGroupProgressToggleState();
            return;
        }

        float jumpHeight = Mathf.Max(0f, progressToggleJumpHeight);
        float staggerInterval = Mathf.Max(0f, progressToggleStaggerInterval);
        _groupProgressSequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject);

        for (int i = 0; i < animatedRectCount; i++)
        {
            RectTransform rectTransform = _animatedProgressRects[i];
            Vector2 basePosition = _animatedProgressBasePositions[i];
            Vector3 baseScale = _animatedProgressBaseScales[i];
            Sequence itemSequence = CreateGroupProgressJumpSequence(
                rectTransform,
                basePosition,
                baseScale,
                jumpHeight);
            _groupProgressSequence.Insert(i * staggerInterval, itemSequence);
        }

        _groupProgressSequence.OnComplete(OnGroupProgressAnimationComplete);
    }

    private void CacheGroupProgressTransforms(int animatedCount)
    {
        _animatedProgressRects.Clear();
        _animatedProgressBasePositions.Clear();
        _animatedProgressBaseScales.Clear();

        for (int i = 0; i < animatedCount; i++)
        {
            RectTransform rectTransform = _groupProgressToggles[i].transform as RectTransform;
            if (rectTransform == null)
                continue;

            rectTransform.DOKill();
            _animatedProgressRects.Add(rectTransform);
            _animatedProgressBasePositions.Add(rectTransform.anchoredPosition);
            _animatedProgressBaseScales.Add(rectTransform.localScale);
        }
    }

    private static Sequence CreateGroupProgressJumpSequence(
        RectTransform rectTransform,
        Vector2 basePosition,
        Vector3 baseScale,
        float jumpHeight)
    {
        Vector3 squashScale = new Vector3(baseScale.x * 1.12f, baseScale.y * 0.86f, baseScale.z);
        Vector3 stretchScale = new Vector3(baseScale.x * 0.92f, baseScale.y * 1.14f, baseScale.z);
        Vector3 landScale = new Vector3(baseScale.x * 1.1f, baseScale.y * 0.88f, baseScale.z);
        Vector3 reboundScale = new Vector3(baseScale.x * 0.97f, baseScale.y * 1.05f, baseScale.z);
        float reboundHeight = jumpHeight * 0.14f;

        return DOTween.Sequence()
            .Append(rectTransform.DOScale(squashScale, 0.06f).SetEase(Ease.OutQuad))
            .Append(rectTransform.DOAnchorPosY(basePosition.y + jumpHeight, 0.14f).SetEase(Ease.OutQuad))
            .Join(rectTransform.DOScale(stretchScale, 0.14f).SetEase(Ease.OutQuad))
            .Append(rectTransform.DOAnchorPosY(basePosition.y, 0.14f).SetEase(Ease.InQuad))
            .Join(rectTransform.DOScale(landScale, 0.14f).SetEase(Ease.InQuad))
            .Append(rectTransform.DOAnchorPosY(basePosition.y + reboundHeight, 0.07f).SetEase(Ease.OutQuad))
            .Join(rectTransform.DOScale(reboundScale, 0.07f).SetEase(Ease.OutQuad))
            .Append(rectTransform.DOAnchorPosY(basePosition.y, 0.1f).SetEase(Ease.InQuad))
            .Join(rectTransform.DOScale(baseScale, 0.1f).SetEase(Ease.OutBack, 2.2f));
    }

    private void OnGroupProgressAnimationComplete()
    {
        _groupProgressSequence = null;
        RestoreGroupProgressTransforms();
        RefreshGroupProgressToggleState();
    }

    private void RefreshGroupProgressToggleState()
    {
        for (int i = 0; i < _groupProgressToggles.Count; i++)
        {
            Toggle toggle = _groupProgressToggles[i];
            if (toggle == null)
                continue;

            bool isConfiguredSlot = i < _groupProgressLevelCount;
            toggle.gameObject.SetActive(isConfiguredSlot);
            if (isConfiguredSlot)
                toggle.SetIsOnWithoutNotify(i <= _groupProgressCurrentIndex);
        }
    }

    private void StopGroupProgressAnimation(bool restoreTransforms)
    {
        if (_groupProgressSequence != null && _groupProgressSequence.IsActive())
            _groupProgressSequence.Kill();

        _groupProgressSequence = null;
        if (restoreTransforms)
            RestoreGroupProgressTransforms();
    }

    private void RestoreGroupProgressTransforms()
    {
        int restoreCount = Mathf.Min(
            _animatedProgressRects.Count,
            Mathf.Min(_animatedProgressBasePositions.Count, _animatedProgressBaseScales.Count));
        for (int i = 0; i < restoreCount; i++)
        {
            RectTransform rectTransform = _animatedProgressRects[i];
            if (rectTransform == null)
                continue;

            rectTransform.anchoredPosition = _animatedProgressBasePositions[i];
            rectTransform.localScale = _animatedProgressBaseScales[i];
        }

        _animatedProgressRects.Clear();
        _animatedProgressBasePositions.Clear();
        _animatedProgressBaseScales.Clear();
    }

    /// <summary>
    /// 用途：切换 Skip 按钮整体可见性。
    /// 调用边界：由 IWannerPlayCtrl 按当前关卡类型与失败阈值调用；普通小关未达阈值及 TBN 关默认传 false。
    /// 注意事项：显示不代表立即可点击，只有 HUD 输入同时开放时才允许交互。
    /// </summary>
    internal void SetSkipVisible(bool visible)
    {
        _skipVisible = visible;
        if (SkipBtn != null)
            SkipBtn.gameObject.SetActive(visible);

        RefreshSkipButtonState();
    }

    private void RefreshSkipButtonState()
    {
        if (SkipBtn != null)
            SkipBtn.Interactable = _skipVisible && _inputEnabled;
    }

    private static void SetButtonInteractable(CustomButton button, bool interactable)
    {
        if (button != null)
            button.Interactable = interactable;
    }

    private static void ApplyImageColors(List<Image> images, Color color)
    {
        if (images == null)
            return;

        for (int i = 0; i < images.Count; i++)
            ApplyImageColor(images[i], color);
    }

    private static void ApplyImageColor(Image image, Color color)
    {
        if (image != null)
            image.color = color;
    }

    private static void RefreshProgressImages(
        List<Image> images,
        int levelCount,
        int currentLevelIndex,
        bool showPassed)
    {
        if (images == null)
            return;

        for (int i = 0; i < images.Count; i++)
        {
            Image image = images[i];
            if (image == null)
                continue;

            bool isConfiguredSlot = i < levelCount;
            bool isPassedOrCurrent = i <= currentLevelIndex;
            image.gameObject.SetActive(
                isConfiguredSlot && (showPassed ? isPassedOrCurrent : !isPassedOrCurrent));
        }
    }

    #endregion

    #region Factory

    /// <summary>
    /// 用途：通过 Bear.UI 为当前 IWanner 小关创建全新的玩法 HUD，并完成一次性进度与局部 Skip 回调初始化。
    /// 调用边界：由 IWannerPlayCtrl 在每次进入、Reset、Replay 或后续切换小关时调用；旧实例必须先销毁。
    /// 注意事项：Skip 只回调当前控制器，不持有控制器引用、不派发全局跳关事件；本方法不复用既有实例。
    /// </summary>
    /// <param name="currentLevelIndex">当前 Group 内的小关索引。</param>
    /// <param name="levelCount">当前 Group 的小关总数。</param>
    /// <param name="skipHandler">当前小关满足显示条件后提交 Skip 的局部回调。</param>
    public static IWannerGame_GamePlayPanel Create(
        int currentLevelIndex,
        int levelCount,
        Func<bool> skipHandler)
    {
        IWannerGame_GamePlayPanel panel = UIManager.Instance.OpenUI<IWannerGame_GamePlayPanel>(
            nameof(IWannerGame_GamePlayPanel),
            UILayer.Normal);
        if (panel != null)
            panel._skipHandler = skipHandler;

        panel?.InitializeGroupProgress(currentLevelIndex, levelCount);
        return panel;
    }

    #endregion
}
