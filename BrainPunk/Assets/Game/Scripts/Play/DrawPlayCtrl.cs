using System;
using System.Collections.Generic;
using System.Threading;
using Bear.EventSystem;
using Bear.Logger;
using Bear.ResourceSystem;
using Bear.SaveModule;
using Bear.UI;
using Config;
using Config.Level;
using Cysharp.Threading.Tasks;
using Game;
using Game.Events;
using Game.ConfigModule;
using Game.Play;
using UnityEngine;
using Game.Scripts.Common;
using I2.Loc;

/// <summary>
/// 画线单关玩法控制器。
/// 负责当前选中的 DLevel、DrawGame_GamePlayPanel、暂停入口和局部玩法生命周期；
/// 具体机关演出和胜负条件由关卡内组件通过局部接口承接。
/// </summary>
public sealed class DrawPlayCtrl : IPlayController, IDebuger, IEventSender, IInterstitialAdPolicyHost, IGMFeature
{
    #region Constants

    private const string DrawLevelDirectory = "Assets/Game/Prefabs/DrawLevel/";
    private const string TbcLevelResourcePath = DrawLevelDirectory + "DLevel_999.prefab";
    private const string TipsHiddenLevelResourcePath = DrawLevelDirectory + "DLevel_001.prefab";
    private const float ManualResetFadeOutDelaySeconds = 0.5f;
    private const float ResetFadeInInputDelaySeconds = .5f;

    #endregion

    #region Runtime State

    private PlayControllerContext _context;
    private DrawGame_ChoiceLevelPanel _choiceLevelPanel;
    private GameObject _levelInstance;
    private DrawGame_GamePlayPanel _gamePlayPanel;
    private DrawGame_GameGuidePopup _gameGuidePopup;
    private IDrawLevelRuntime _drawLevelRuntime;
    private EventSubscriber _subscriber;
    private DrawPlayPhase _phase = DrawPlayPhase.Intro;
    private bool _isInitialized;
    private bool _isActive;
    private bool _isPaused;
    private bool _isOpeningLevel;
    private bool _isAttemptTransitionPending;
    private bool _hasGrantedHint;
    private bool _shouldShowHintForCurrentAttempt;
    private bool _hasShownTipsWarning;
    private bool _isTipsButtonVisible = true;
    private bool _isRewardInkGranted;
    private float _tipsAttemptTimeSeconds;
    private float _showTipsTimeLimit;
    private int _showTipsFailCount;
    private int _activationVersion;
    private string _drawLevelPath;
    private int _drawLevelDisplayId;
    // 进入请求通过时固定真实内容；运行时重排、Reset 与 Replay 不重新按显示序号推算。
    private DrawLevelData _currentLevelData;
    private int _currentLevelConfigGeneration;
    private DrawInterstitialAdPolicy _interstitialAdPolicy;
    private CancellationTokenSource _victoryProcessCancellationSource;
    private string _analyticsLevelDataId;
    private string _analyticsLevelPath;
    private float _analyticsCurrentLevelTimeSeconds;
    private int _analyticsReviveCount;
    private bool _isAnalyticsAttemptActive;

    #endregion

    #region IPlayController

    /// <summary>
    /// 当前控制器负责画线玩法。
    /// </summary>
    public PlayModeType Mode => PlayModeType.Draw;

    int IInterstitialAdPolicyHost.InterstitialLevel => _drawLevelDisplayId;

    IInterstitialAdPolicy IInterstitialAdPolicyHost.InterstitialAdPolicy =>
        _interstitialAdPolicy;

    /// <summary>
    /// 画线控制器是否已注入共享运行时依赖。
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// 画线控制器当前是否为活动玩法。
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// 当前画线单关阶段。
    /// 注意事项：该状态只由 DrawPlayCtrl 推进，关卡本地演出不得直接改写。
    /// </summary>
    public DrawPlayPhase Phase => _phase;

    /// <summary>
    /// 当前 Draw 关卡会话是否已经通过金币或广告获得提示资格。
    /// 注意事项：同关 Reset、失败重载和 Replay 不会清除此状态；进入新选关请求、返回选关页或退出 Draw 时重置。
    /// </summary>
    public bool HasGrantedHint => _hasGrantedHint;

    /// <summary>
    /// 当前 Draw 关卡进入周期内是否已经领取过奖励墨水。
    /// 调用边界：仅供 Draw HUD 在发起广告前和同关重建时同步显示；进入新关、返回选关页或退出 Draw 玩法会重置。
    /// </summary>
    internal bool IsRewardInkGranted => _isRewardInkGranted;

    /// <summary>供 Draw 配置提交检查当前广告、暂停及转场边界；当前画线本身不阻止列表更新。</summary>
    internal bool IsRemoteConfigRefreshBlocked => _isActive &&
        (_isOpeningLevel || _isAttemptTransitionPending || _isPaused ||
         _choiceLevelPanel?.IsRewardAdPending == true || _gamePlayPanel?.IsRewardAdRequesting == true);

    /// <summary>仅在云控提交/延期时输出已有状态，不参与门禁判断，也不在每帧生成日志。</summary>
    internal string RemoteConfigRefreshState =>
        $"active={_isActive}, opening={_isOpeningLevel}, attemptTransition={_isAttemptTransitionPending}, paused={_isPaused}, choiceAd={_choiceLevelPanel?.IsRewardAdPending == true}, inkAd={_gamePlayPanel?.IsRewardAdRequesting == true}";

    /// <summary>配置提交后仅刷新当前持有的选关界面；关卡、HUD、墨水与结算上下文保持当前会话。</summary>
    internal void RefreshRemoteConfigViews()
    {
        Guru.SDK.Framework.Utils.Log.Log.I($"[DrawRemoteConfigUI] stage=choice_dispatch, panelPresent={_choiceLevelPanel != null}; current level is retained.");
        _choiceLevelPanel?.RefreshRemoteConfig();
    }

    /// <summary>
    /// 当前 Draw 关卡是否允许 HUD 发起提示交互。
    /// 调用边界：仅供 Draw 专用提示弹窗工厂校验活动关卡运行时，不用于推进关卡阶段。
    /// </summary>
    internal bool IsHintInteractionReady =>
        _isActive &&
        !_isOpeningLevel &&
        _drawLevelRuntime != null &&
        _drawLevelRuntime.IsRuntimeReady;

    /// <summary>
    /// Draw 当前没有 GM 下一关语义；调用方不得回退为修改主线关卡。
    /// </summary>
    public bool TryEnterNextLevel()
    {
        return false;
    }

    /// <summary>
    /// Draw 当前没有 GM 上一关语义；调用方不得回退为修改主线关卡。
    /// </summary>
    public bool TryEnterPreviousLevel()
    {
        return false;
    }

    /// <summary>
    /// 直接提交当前 DrawGame 关卡胜利，用于 GM 调试入口。
    /// 调用边界：仅在活动控制器处于 Drawing 阶段且关卡运行时就绪时接受；
    /// 通过 IDrawLevelRuntime.TryResolve 进入原有结算、广告、统计、存档和胜利面板链。
    /// </summary>
    /// <param name="source">触发来源，用于结算日志。</param>
    /// <returns>本次胜利被当前关卡运行时首次接受时返回 true。</returns>
    public bool TryForceVictory(string source)
    {
        if (!_isInitialized ||
            !_isActive ||
            _isPaused ||
            _isOpeningLevel ||
            _isAttemptTransitionPending ||
            _phase != DrawPlayPhase.Drawing ||
            _drawLevelRuntime == null ||
            !_drawLevelRuntime.IsRuntimeReady)
        {
            return false;
        }

        string resolvedSource = string.IsNullOrWhiteSpace(source)
            ? nameof(TryForceVictory)
            : source;
        return _drawLevelRuntime.TryResolve(new DrawLevelResult(
            DrawLevelResultType.Success,
            resolvedSource));
    }

    /// <summary>
    /// 按当前 DrawInkState.MaxInk 为本次尝试增加一整份基础最大墨水容量。
    /// 调用边界：仅供当前 Draw HUD 在激励视频成功回调中调用；控制器或关卡未就绪时拒绝发放。
    /// 注意事项：单次门禁由 DrawPlayCtrl 持有，Reset/Replay 会为同关新运行时恢复真实容量；
    /// 进入新关、返回选关页或退出 Draw 玩法后重置。
    /// </summary>
    internal bool TryGrantRewardInk(out DrawInkState inkState)
    {
        inkState = default;
        if (!_isActive ||
            _isOpeningLevel ||
            _isRewardInkGranted ||
            _drawLevelRuntime == null ||
            !_drawLevelRuntime.IsRuntimeReady)
        {
            return false;
        }

        DrawInkState currentInkState = _drawLevelRuntime.InkState;
        float addedInk = _drawLevelRuntime.AddInk(currentInkState.MaxInk);
        if (addedInk <= 0f)
            return false;

        inkState = _drawLevelRuntime.InkState;
        _isRewardInkGranted = true;
        return true;
    }

    /// <summary>
    /// 请求当前已激活的 DrawPlayCtrl 从选关界面进入指定关卡。
    /// 调用边界：仅允许由 DrawGame_ChoiceLevelPanel 经 GameManager 转发调用。
    /// 注意事项：控制器未激活、关卡正在加载或已有活动关卡时会拒绝重复进入；
    /// 选关确认后复用主线 EnterLevelLoading 过场，擦除完成前不会开放画线输入。
    /// </summary>
    /// <param name="displayId">DrawLevelSort.Id，用于 HUD 显示。</param>
    /// <param name="levelPath">DrawLevelData.Path，可传资源名或 Assets 下完整 prefab 路径。</param>
    /// <param name="error">请求失败原因；成功时为空。</param>
    /// <returns>已接受关卡请求并开始异步加载时返回 true。</returns>
    public bool TryEnterLevel(int displayId, string levelPath, out string error)
    {
        return TryBeginLevelTransition(
            displayId,
            levelPath,
            keepCurrentLevelUntilCovered: false,
            out error);
    }

    /// <summary>
    /// 校验并启动 Draw 进关转场；正常选关要求当前无关卡，胜利下一关可把旧关保留到翻页完全遮住。
    /// </summary>
    private bool TryBeginLevelTransition(
        int displayId,
        string levelPath,
        bool keepCurrentLevelUntilCovered,
        out string error)
    {
        if (!_isActive)
        {
            error = "DrawPlayCtrl is not active.";
            return false;
        }

        if (_isOpeningLevel)
        {
            error = "A Draw level is already opening.";
            return false;
        }

        if (keepCurrentLevelUntilCovered)
        {
            if (_phase != DrawPlayPhase.Resolved ||
                _drawLevelRuntime == null ||
                _levelInstance == null)
            {
                error = "A resolved Draw level is required before entering the next level.";
                return false;
            }
        }
        else if (_drawLevelRuntime != null || _levelInstance != null)
        {
            error = "A Draw level is already running.";
            return false;
        }

        if (displayId <= 0)
        {
            error = $"Invalid Draw level display id: {displayId}.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(levelPath))
        {
            error = "Draw level path is null or empty.";
            return false;
        }

        DrawGameLevelCtrl levelController = GameManager.Instance?.DrawGameLevel;
        DrawLevelSort selectedSort = levelController?.GetLevelSortById(displayId);
        DrawLevelData selectedData = selectedSort == null ? null : levelController.GetLevelDataById(selectedSort.Path);
        if (selectedData == null || string.IsNullOrWhiteSpace(selectedData.Path) || !string.Equals(
                ResolveLevelResourcePath(selectedData.Path), ResolveLevelResourcePath(levelPath), StringComparison.Ordinal))
        {
            error = "Draw selection no longer matches the current level sequence.";
            return false;
        }

        if (keepCurrentLevelUntilCovered)
        {
            // 胜利结算后排序可能再次变化；接受下一关时补齐新目标进度，不把刷新当作通关。
            DrawGame nextProgress = DB.DrawGame;
            if (nextProgress != null)
            {
                bool progressChanged = nextProgress.CurrentLevel != displayId;
                nextProgress.CurrentLevel = displayId;
                if (!nextProgress.UnlockLevels.Contains(displayId))
                {
                    nextProgress.UnlockLevels.Add(displayId);
                    progressChanged = true;
                }
                if (progressChanged && !DB.SaveDrawGame())
                    this.LogWarning($"[DrawPlayCtrl] Failed to save accepted next level. sortId:{displayId}");
            }
        }

        OneLinkManager.Instance.OnLevelEntryStarted(GameType.Draw);
        _currentLevelData = selectedData;
        _currentLevelConfigGeneration = levelController.ConfigGeneration;
        this.Log($"[DrawPlayCtrl] Accepted Draw content. generation:{_currentLevelConfigGeneration}, sortId:{displayId}, dataId:{selectedData.Id}");
        _drawLevelDisplayId = displayId;
        _drawLevelPath = ResolveLevelResourcePath(levelPath);
        _isTipsButtonVisible = !string.Equals(
            _drawLevelPath,
            TipsHiddenLevelResourcePath,
            StringComparison.OrdinalIgnoreCase);
        _isAttemptTransitionPending = false;
        ResetHintGrantSession();
        PrepareTipsWarningForAttempt();
        ResetRewardInkSession();
        _isOpeningLevel = true;
        int activationVersion = ++_activationVersion;

        if (keepCurrentLevelUntilCovered)
            DestroyGamePlayPanel();

        StartNewLevelAnalytics();
        CloseLevelSelection();
        OpenDrawLevelTransitionAsync(activationVersion, keepCurrentLevelUntilCovered).Forget();

        error = null;
        return true;
    }

    /// <summary>
    /// 注入 GameManager 持有的共享上下文。
    /// 调用边界：仅由 PlayControllerManager 在首次进入 Draw 前调用。
    /// </summary>
    public void Initialize(PlayControllerContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _interstitialAdPolicy = new DrawInterstitialAdPolicy();
        _isInitialized = true;
    }

    /// <summary>
    /// 激活画线玩法并打开 Draw 专属选关界面。
    /// 调用边界：仅由 PlayControllerManager 切换玩法时调用。
    /// 注意事项：激活和选关阶段保留现有 Normal 层；只有确认进入具体 Draw 关卡后才关闭。
    /// </summary>
    public void Activate()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("DrawPlayCtrl must be initialized before activation.");

        if (_isActive)
            return;

        _isActive = true;
        _isPaused = false;
        _isAttemptTransitionPending = false;
        _activationVersion++;
        AddListener();

        if (!OpenLevelSelection(out string error))
            throw new InvalidOperationException(error);
    }

    /// <summary>
    /// 停用画线玩法并清理关卡实例、HUD 和未完成加载结果。
    /// 调用边界：由 PlayControllerManager 在切换到其他玩法前调用。
    /// </summary>
    public void Deactivate()
    {
        OneLinkManager.Instance.EndEntry(GameType.Draw);
        ReportLevelEndAnalytics(GameSDKService.LevelEndTypeExit);
        _isActive = false;
        _isPaused = false;
        _isAttemptTransitionPending = false;
        ResetRewardInkSession();
        _activationVersion++;
        CancelVictoryProcess();
        EventsUtils.ResetEvents(ref _subscriber);
        CleanupRuntime();
    }

    /// <summary>
    /// 驱动画线玩法逐帧逻辑，并处理当前活动 Draw 模式的系统返回键。
    /// 仅活动控制器响应返回键；暂停时停止关卡 Tick，但仍允许 Exit App 确认入口。
    /// </summary>
    public void Tick()
    {
        if (!_isActive)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
            ShowExitPopup();

        if (_isPaused)
            return;

        TickLevelAnalytics(Time.deltaTime);
        UpdateTipsWarning(Time.deltaTime);
        _drawLevelRuntime?.Tick();
    }

    /// <summary>
    /// 释放画线控制器持有的运行时状态。
    /// 调用边界：仅由 PlayControllerManager.Shutdown 调用。
    /// </summary>
    public void Shutdown()
    {
        Deactivate();
        _interstitialAdPolicy?.Dispose();
        _interstitialAdPolicy = null;
        _context = null;
        _isInitialized = false;
    }

    #endregion

    #region Exit App

    /// <summary>
    /// 显示 Draw 模式的退出应用确认弹窗。
    /// 调用边界：仅由当前活动 DrawPlayCtrl 在收到 Escape/Android Back 的离散输入时调用。
    /// 注意事项：加载、重置/重玩过渡、胜利结算和引导期间不会创建弹窗。
    /// </summary>
    private void ShowExitPopup()
    {
        if (!CanShowExitPopup())
            return;

        CommmonPopup.Create(
            title: LocalizationManager.GetTranslation("U_Quit_Title_01"),
            content: LocalizationManager.GetTranslation("U_Quit_Des_01"),
            onYes: () =>
            {
                Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            },
            onNo: null);
    }

    /// <summary>
    /// 判断 Draw 当前状态是否允许退出确认弹窗进入 Popup 栈。
    /// </summary>
    private bool CanShowExitPopup()
    {
        CommmonPopup exitPopup = UIManager.Instance?.GetUI<CommmonPopup>(nameof(CommmonPopup));
        if (exitPopup != null && exitPopup.IsOpen)
            return false;

        GuideFlowManager guideFlowManager = GuideFlowManager.Instance;
        if (guideFlowManager.HasActiveFlow || guideFlowManager.PendingCount > 0)
            return false;

        return !_isOpeningLevel &&
               !_isAttemptTransitionPending &&
               _phase != DrawPlayPhase.Resolved;
    }

    #endregion

    #region Pause Host

    /// <summary>
    /// 暂停当前画线玩法并关闭输入。
    /// 注意事项：未完成的一笔由关卡运行时取消并返还墨水。
    /// </summary>
    public void Pause()
    {
        if (_isActive)
        {
            _isPaused = true;
            _drawLevelRuntime?.SetPaused(true);
        }
    }

    /// <summary>
    /// 恢复当前画线玩法。
    /// </summary>
    public void Resume()
    {
        if (_isActive)
        {
            _isPaused = false;
            _drawLevelRuntime?.SetPaused(false);

            // 入场已完成且没有转场时补开画线，结果演出仍由关卡运行时门禁拦截。
            if (_phase == DrawPlayPhase.Drawing &&
                !_isOpeningLevel &&
                !_isAttemptTransitionPending &&
                _drawLevelRuntime?.IsRuntimeReady == true)
            {
                _drawLevelRuntime.SetInputEnabled(true);
            }
        }
    }

    /// <summary>
    /// 结束当前画线关卡，先恢复 StartPanel，再重新打开 DrawGame_ChoiceLevelPanel。
    /// 调用边界：供复用的 GameSettingPopup 操作当前激活的 Draw 控制器。
    /// 注意事项：退出清理与 level_end(exit) 统一由玩法切换触发的 Deactivate 执行，避免重复上报。
    /// </summary>
    public void ExitToLevelSelection()
    {
        if (!_isActive)
            return;

        bool shouldRestoreLevelSelection =
            _isOpeningLevel ||
            _drawLevelRuntime != null ||
            _levelInstance != null ||
            _gamePlayPanel != null;

        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            this.LogError("[DrawPlayCtrl] Return to StartPanel failed. GameManager is unavailable.");
            return;
        }

        if (!gameManager.ReturnToStartPanel(out string error))
        {
            this.LogError($"[DrawPlayCtrl] Return to StartPanel failed. error:{error}");
            return;
        }

        if (!shouldRestoreLevelSelection)
            return;

        if (!gameManager.TryEnterDraw(out error))
        {
            this.LogError($"[DrawPlayCtrl] Restore Draw level selection failed. error:{error}");
            return;
        }
    }

    #endregion

    #region Runtime Lifecycle

    /// <summary>
    /// 仅在 DrawPlayCtrl 激活期间监听 Draw 专属重置事件。
    /// 注意事项：不得订阅主线 GameResetEvent，避免触发或接收主线广告与统计链。
    /// </summary>
    private void AddListener()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<DrawGameResetEvent>(OnGameResetEvent);
        _subscriber.Subscribe<DrawGameHintEvent>(OnGameHintEvent);
        _subscriber.Subscribe<GameReplayEvent>(OnGameReplayEvent);
        _subscriber.Subscribe<EnterNextLevelEvent>(OnEnterNextLevel);
    }

    private void OnGameResetEvent(DrawGameResetEvent evt)
    {
        if (!_isActive ||
            _drawLevelRuntime == null ||
            _isOpeningLevel ||
            _isAttemptTransitionPending)
        {
            return;
        }

        _isAttemptTransitionPending = true;
        // Reset/Fail 是新尝试边界；即使暂停弹窗仍打开，新实例也必须恢复为可玩状态。
        _isPaused = false;
        CancelVictoryProcess();
        _gamePlayPanel?.BeginResetTransition();

        RecordLevelRetryAnalytics();
        ReportLevelEndAnalytics(GameSDKService.LevelEndTypeFailed);
        PrepareHintVisibilityForSameLevelAttempt();
        PrepareTipsWarningForAttempt();
        StartResetAnalytics();
        _interstitialAdPolicy?.OnPlayerFailedOrRetried();
        _drawLevelRuntime.SetInputEnabled(false);
        ResetGameAsync(
            evt.Type.ToString(),
            _activationVersion,
            showFailureInterstitial: true).Forget();
    }

    private void OnGameHintEvent(DrawGameHintEvent evt)
    {
        if (!_isActive)
            return;

        _hasGrantedHint = true;
        _shouldShowHintForCurrentAttempt = true;
        this.DispatchEvent(Witness<GamePlayPanelSwitchTipsEvent>._, false);
        _drawLevelRuntime?.ShowHint(true);
    }

    private bool OpenLevelSelection(out string error)
    {
        if (!_isActive)
        {
            error = "Cannot open Draw level selection while DrawPlayCtrl is inactive.";
            return false;
        }

        ResetHintGrantSession();

        GameManager.Instance.OpenCamera();

        if (_choiceLevelPanel != null)
        {
            ResetRewardInkSession();
            error = null;
            return true;
        }

        _choiceLevelPanel = DrawGame_ChoiceLevelPanel.Create();
        if (_choiceLevelPanel == null)
        {
            error = "Failed to create DrawGame_ChoiceLevelPanel.";
            return false;
        }

        ResetRewardInkSession();
        error = null;
        return true;
    }

    private void CloseLevelSelection()
    {
        if (_choiceLevelPanel == null)
            return;

        UIManager.Instance.DestroyUI(_choiceLevelPanel);
        _choiceLevelPanel = null;
    }

    /// <summary>
    /// 等待 Popup 层共享 Mask 隐藏后创建主线同款进关转场。
    /// 调用边界：首次选关直接加载；胜利下一关在翻页完全覆盖后才销毁旧 DLevel 并加载新关。
    /// 注意事项：必须等待一帧，避免无 Mask 的 EnterLevelLoading 继承前一个 Popup 的遮罩。
    /// </summary>
    private async UniTask OpenDrawLevelTransitionAsync(
        int activationVersion,
        bool releaseCurrentLevelAfterCover)
    {
        await UniTask.NextFrame();
        if (!IsCurrentActivation(activationVersion) || !_isOpeningLevel)
            return;

        try
        {
            EnterLevelLoading.Create(
                () => OpenDrawLevelAfterCoverAsync(
                    activationVersion,
                    releaseCurrentLevelAfterCover),
                () => OnEnterLevelLoadingEraseComplete(activationVersion));
        }
        catch (Exception exception)
        {
            HandleOpenFailure(
                activationVersion,
                $"Failed to create Draw level transition. exception:{exception.Message}");
        }
    }

    /// <summary>
    /// 在 EnterLevelLoading 翻页完全覆盖后释放旧关，再创建新 HUD 与 DLevel。
    /// </summary>
    private async UniTask OpenDrawLevelAfterCoverAsync(
        int activationVersion,
        bool releaseCurrentLevelAfterCover)
    {
        if (!IsCurrentActivation(activationVersion) || !_isOpeningLevel)
            return;

        if (releaseCurrentLevelAfterCover)
        {
            DetachLevelRuntime();
            DestroyLevelInstance();
        }

        await OpenDrawLevelAsync(activationVersion, false, false);
    }

    private async UniTask OpenDrawLevelAsync(
        int activationVersion,
        bool showResetMask,
        bool isRetry)
    {
        try
        {
            UIManager.Instance.CloseAllUI(UILayer.Normal);

            CloseLevelSelection();

            if (!TryCreateGamePlayPanel(activationVersion, showResetMask))
                return;

            GameObject prefab = await Bear.ResourceSystem.ResourceManager.Instance.LoadAsync<GameObject>(_drawLevelPath);
            if (!IsCurrentActivation(activationVersion))
                return;

            if (prefab == null)
            {
                HandleOpenFailure(activationVersion, $"Draw level prefab not found: {_drawLevelPath}");
                return;
            }

            GameObject sceneRootObject = GameObject.Find("Scene");
            if (sceneRootObject == null)
            {
                HandleOpenFailure(activationVersion, "Main scene root 'Scene' was not found.");
                return;
            }

            GameManager.Instance.CloseCamera();
            _levelInstance = UnityEngine.Object.Instantiate(prefab, sceneRootObject.transform);
            _drawLevelRuntime = ResolveDrawLevelRuntime(_levelInstance);
            if (_drawLevelRuntime == null)
            {
                HandleOpenFailure(
                    activationVersion,
                    $"Draw level does not contain {nameof(IDrawLevelRuntime)}: {_drawLevelPath}");
                return;
            }

            _drawLevelRuntime.Resolved += OnLevelResolved;
            if (TryGetCurrentLevelData(out DrawLevelData currentLevelData))
            {
                _drawLevelRuntime.ConfigureInk(
                    currentLevelData.MaxInkLength,
                    currentLevelData.InkStarStage1,
                    currentLevelData.InkStarStage2,
                    currentLevelData.InkStarStage3);
            }
            _drawLevelRuntime.InitializeRuntime();
            if (!_drawLevelRuntime.IsRuntimeReady)
            {
                HandleOpenFailure(
                    activationVersion,
                    $"Draw level runtime initialization failed: {_drawLevelPath}");
                return;
            }

            if (_gamePlayPanel == null)
            {
                HandleOpenFailure(activationVersion, "Draw GameplayPanel closed while opening level.");
                return;
            }

            RestoreRewardInkForNewRuntime();
            _drawLevelRuntime.ShowHint(_shouldShowHintForCurrentAttempt);

            _drawLevelRuntime.SetPaused(_isPaused);
            _gamePlayPanel.SetInkState(_drawLevelRuntime.InkState);

            _phase = DrawPlayPhase.Intro;
            if (!isRetry && !_drawLevelRuntime.PlaysIntroAfterTransition)
                await _drawLevelRuntime.PlayIntroAsync();
            if (!IsCurrentActivation(activationVersion))
                return;
        }
        catch (Exception exception)
        {
            HandleOpenFailure(activationVersion, exception.ToString());
        }
    }

    private bool TryCreateGamePlayPanel(int activationVersion, bool showResetMask)
    {
        DestroyGamePlayPanel();

        _gamePlayPanel = DrawGame_GamePlayPanel.Create();
        if (_gamePlayPanel == null)
        {
            HandleOpenFailure(activationVersion, "Failed to create DrawGame_GamePlayPanel.");
            return false;
        }

        _gamePlayPanel.SetLevel(_drawLevelDisplayId);
        _gamePlayPanel.SetTipsButtonVisible(_isTipsButtonVisible);
        _gamePlayPanel.SetRewardInkGranted(_isRewardInkGranted);

        // TBC 关卡播放音效
        if (IsTbcLevel())
        {
            AudioManager.PlaySound("TBC");
        }

        if (showResetMask)
            _gamePlayPanel.ShowBlackMaskImmediately();
        return true;
    }

    /// <summary>
    /// 为 Reset/Replay 后刚初始化完成的同关 DLevel 恢复已经领取的真实奖励容量。
    /// 调用边界：同关每个新运行时只调用一次；进入新关会提前清除门禁，不会继承上一关容量。
    /// 恢复失败时同步撤销领取表现，避免 HUD 与实际容量不一致。
    /// </summary>
    private void RestoreRewardInkForNewRuntime()
    {
        if (!_isRewardInkGranted ||
            _drawLevelRuntime == null ||
            !_drawLevelRuntime.IsRuntimeReady)
        {
            return;
        }

        DrawInkState currentInkState = _drawLevelRuntime.InkState;
        float addedInk = _drawLevelRuntime.AddInk(currentInkState.MaxInk);
        if (addedInk > 0f)
            return;

        _isRewardInkGranted = false;
        _gamePlayPanel?.SetRewardInkGranted(false);
        this.LogWarning("[DrawPlayCtrl] Failed to restore rewarded ink for the new runtime.");
    }

    /// <summary>
    /// 清理当前 Draw 会话的奖励墨水领取门禁。
    /// 调用边界：进入新关、进入选关页或退出 Draw 玩法时调用；关内 Reset/Replay 不得调用。
    /// </summary>
    private void ResetRewardInkSession()
    {
        _isRewardInkGranted = false;
    }

    /// <summary>
    /// 清理当前 Draw 关卡会话已经获得的提示资格与本次尝试显示状态。
    /// 调用边界：只在进入新的选关请求、返回选关页或退出 Draw 玩法时调用；同关 Reset、失败重试或 Replay 不得调用。
    /// </summary>
    private void ResetHintGrantSession()
    {
        _hasGrantedHint = false;
        _shouldShowHintForCurrentAttempt = false;
    }

    /// <summary>
    /// 为同一 DLevel 的新尝试重新派生提示显示状态。
    /// 注意事项：只读取当前关已经获得的提示资格，不产生消费、广告请求、存档或事件。
    /// </summary>
    private void PrepareHintVisibilityForSameLevelAttempt()
    {
        _shouldShowHintForCurrentAttempt = _hasGrantedHint;
    }

    /// <summary>
    /// 为当前 Draw 尝试重置 Tips warning 的一次性状态，并刷新 Draw 专属远端阈值。
    /// 调用边界：只在进入新关、Reset 或 Victory Replay 开启新尝试时调用。
    /// </summary>
    private void PrepareTipsWarningForAttempt()
    {
        _hasShownTipsWarning = false;
        _tipsAttemptTimeSeconds = 0f;
        _showTipsTimeLimit = ConfigManager.RemoteConfig.GetDrawGameShowTipsTimeLimit();
        _showTipsFailCount = ConfigManager.RemoteConfig.GetDrawGameShowTipsFailCount();
    }

    /// <summary>
    /// 按主线提示 warning 语义，在 Draw 可操作阶段累计等待时间并检查本关累计重试次数。
    /// 注意事项：达到任一阈值后单次尝试只派发一次，点击 Tips 后不会在同次尝试重复出现。
    /// </summary>
    private void UpdateTipsWarning(float deltaSeconds)
    {
        if (_hasGrantedHint ||
            _hasShownTipsWarning ||
            !_isTipsButtonVisible ||
            _gamePlayPanel == null ||
            _isOpeningLevel ||
            _isAttemptTransitionPending ||
            _phase != DrawPlayPhase.Drawing)
        {
            return;
        }

        if (deltaSeconds > 0f)
            _tipsAttemptTimeSeconds += deltaSeconds;

        bool playTimeReached = _tipsAttemptTimeSeconds > _showTipsTimeLimit;
        bool failCountReached = _analyticsReviveCount >= _showTipsFailCount;
        if (!playTimeReached && !failCountReached)
            return;

        _hasShownTipsWarning = true;
        this.DispatchEvent(Witness<GamePlayPanelSwitchTipsEvent>._, true);
        this.Log(
            $"[DrawPlayCtrl] Show Tips warning. attemptSeconds:{_tipsAttemptTimeSeconds:F1}, revive:{_analyticsReviveCount}");
    }

    /// <summary>
    /// 主线同款进关擦除过场完成后开放 Draw 输入。
    /// 调用边界：仅由本次 EnterLevelLoading 的完成回调调用；失效或加载失败的请求不得推进阶段。
    /// </summary>
    private void OnEnterLevelLoadingEraseComplete(int activationVersion)
    {
        if (!IsCurrentActivation(activationVersion) ||
            _drawLevelRuntime == null ||
            !_drawLevelRuntime.IsRuntimeReady)
        {
            return;
        }

        if (_drawLevelRuntime.PlaysIntroAfterTransition)
        {
            PlayVisibleIntroAndBeginDrawingAsync(activationVersion).Forget();
            return;
        }

        CompleteIntroAndBeginDrawing();
    }

    /// <summary>
    /// 转场遮罩已经关闭后播放当前关卡的可见 Intro，完成前继续保持画线输入关闭。
    /// 调用边界：只允许由当前有效进关请求的擦除完成回调触发。
    /// </summary>
    private async UniTask PlayVisibleIntroAndBeginDrawingAsync(int activationVersion)
    {
        await _drawLevelRuntime.PlayIntroAsync();
        if (!IsCurrentActivation(activationVersion) ||
            _drawLevelRuntime == null ||
            !_drawLevelRuntime.IsRuntimeReady)
        {
            return;
        }

        CompleteIntroAndBeginDrawing();
    }

    private void CompleteIntroAndBeginDrawing()
    {
        OneLinkManager.Instance.ConfirmLevelEntered(GameType.Draw);
        BeginDrawing();
        _isOpeningLevel = false;
        _isAttemptTransitionPending = false;
        if (!TryShowGameGuidePopup())
            TryToShowNoAdPopup();
    }

    /// <summary>
    /// 首次实际进关完成后展示 Draw 操作引导，关闭前暂停关卡并保持 HUD 点击锁。
    /// 关闭回调只恢复原激活会话、原关卡；免广告弹窗延后到引导关闭，避免覆盖操作说明。
    /// </summary>
    private bool TryShowGameGuidePopup()
    {
        if (!Application.isPlaying ||
            DB.GameData.HasDrawGameGuidePopup ||
            DB.DrawGame?.PassedLevels?.Count > 0)
            return false;

        int activationVersion = _activationVersion;
        IDrawLevelRuntime levelRuntime = _drawLevelRuntime;
        _gameGuidePopup = DrawGame_GameGuidePopup.Create(() =>
        {
            _gameGuidePopup = null;
            if (!IsCurrentActivation(activationVersion) ||
                !ReferenceEquals(_drawLevelRuntime, levelRuntime) ||
                _isOpeningLevel ||
                _isAttemptTransitionPending ||
                _phase != DrawPlayPhase.Drawing)
            {
                return;
            }

            Resume();
            this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, false);
            TryToShowNoAdPopup();
        });
        if (_gameGuidePopup == null)
            return false;

        Pause();
        this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, true);
        return true;
    }

    /// <summary>
    /// 在 Draw 进入可操作状态后尝试消费成功插屏标记并展示免广告弹窗。
    /// 调用边界：仅由当前有效关卡的进关、Reset 或 Replay 完成点调用；最后一关不自动弹出。
    /// 注意事项：使用 Draw 专属每日模块、RemoteConfig 和存档计数，不得消费主线插屏标记。
    /// </summary>
    private void TryToShowNoAdPopup()
    {
        if (IsLastDrawLevel())
            return;

        var noAdModule = ScheduleSystem.Instance
            .GetModule<DailyScheduleModule>()
            ?.GetRefreshable<InterstitialNOAdPopupAutoShowModule>();
        if (noAdModule == null ||
            !noAdModule.ShouldShowDrawNoAdPopup() ||
            !InterstitialAdHelper.HasDrawInterstitial)
        {
            return;
        }

        NoAdsPopup.Create();
        noAdModule.RecordDrawShow();
        InterstitialAdHelper.HasDrawInterstitial = false;
    }

    /// <summary>
    /// 判断当前 Draw 关卡是否为最终有效排序项；排序不可用时按最后一关处理以避免误弹窗。
    /// </summary>
    private bool IsLastDrawLevel()
    {
        DrawGameLevelCtrl levelCtrl = GameManager.Instance?.DrawGameLevel;
        IReadOnlyList<DrawLevelSort> levelSorts = levelCtrl?.LevelSorts;
        if (levelCtrl == null ||
            !levelCtrl.IsReady ||
            levelSorts == null ||
            levelSorts.Count == 0)
        {
            return true;
        }

        for (int i = levelSorts.Count - 1; i >= 0; i--)
        {
            DrawLevelSort levelSort = levelSorts[i];
            if (levelSort != null)
                return levelSort.Id == _drawLevelDisplayId;
        }

        return true;
    }

    private bool IsCurrentActivation(int activationVersion)
    {
        return _isActive && activationVersion == _activationVersion;
    }

    private void HandleOpenFailure(int activationVersion, string error)
    {
        if (!IsCurrentActivation(activationVersion))
            return;

        OneLinkManager.Instance.EndEntry(GameType.Draw);
        _isAttemptTransitionPending = false;
        this.LogError($"[DrawPlayCtrl] Open draw level failed. error:{error}");
        CleanupLevelRuntime();

        if (!OpenLevelSelection(out string selectionError) &&
            !GameManager.Instance.ReturnToStartPanel(out string returnError))
        {
            this.LogError(
                $"[DrawPlayCtrl] Failure fallback failed. selectionError:{selectionError}, returnError:{returnError}");
        }
    }

    private void CleanupRuntime()
    {
        CloseLevelSelection();
        CleanupLevelRuntime();
        ResetHintGrantSession();
        _currentLevelData = null;
    }

    private void CleanupLevelRuntime()
    {
        _isOpeningLevel = false;
        DetachLevelRuntime();
        if (_gameGuidePopup != null)
        {
            UIManager.Instance.DestroyUI(_gameGuidePopup);
            _gameGuidePopup = null;
        }
        DestroyGamePlayPanel();
        DestroyLevelInstance();
    }

    private void DetachLevelRuntime()
    {
        if (_drawLevelRuntime != null)
        {
            _drawLevelRuntime.SetInputEnabled(false);
            _drawLevelRuntime.Resolved -= OnLevelResolved;
            _drawLevelRuntime = null;
        }

        _phase = DrawPlayPhase.Intro;
    }

    private void DestroyGamePlayPanel()
    {
        if (_gamePlayPanel != null)
        {
            UIManager.Instance.DestroyUI(_gamePlayPanel);
            _gamePlayPanel = null;
        }
    }

    private void DestroyLevelInstance()
    {
        if (_levelInstance != null)
        {
            // Destroy 延迟到帧末；先立即失活，避免同帧重建时旧关卡 Collider/Tween 继续参与新关卡探测。
            _levelInstance.SetActive(false);
            UnityEngine.Object.Destroy(_levelInstance);
            _levelInstance = null;
        }
    }

    /// <summary>
    /// 进入 Drawing 阶段，并成对恢复画线输入与 GameplayPanel 交互。
    /// 注意事项：任何新尝试入口都必须通过这里开放输入，确保 GameplayPanel 交互门禁恢复。
    /// </summary>
    private void BeginDrawing()
    {
        if (!_isActive || _drawLevelRuntime == null)
            return;

        if (!AudioManager.IsCurrentMusicTag("musicInGame"))
        {
            AudioManager.PlayMusic("musicInGame");
        }

        _phase = DrawPlayPhase.Drawing;
        _drawLevelRuntime.SetPaused(_isPaused);
        _drawLevelRuntime.SetInputEnabled(!_isPaused);
        this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, false);
    }

    private void OnLevelResolved(DrawLevelResult result)
    {
        if (!_isActive ||
            _drawLevelRuntime == null ||
            _isAttemptTransitionPending ||
            _phase == DrawPlayPhase.Resolved)
        {
            return;
        }

        _phase = DrawPlayPhase.Resolved;
        _drawLevelRuntime.SetInputEnabled(false);
        this.Log(
            $"[DrawPlayCtrl] Draw level resolved. type:{result.Type}, source:{result.Source}");

        if (result.Type == DrawLevelResultType.Success)
        {
            _interstitialAdPolicy?.OnLevelSucceeded();
            InterstitialAdHelper.TryToShowInterstitial(
                Config.Game.InterstitialPlacement.InGameVictory.ToString());
            StartVictoryProcess();
        }
    }

    /// <summary>
    /// 启动画线关卡胜利结算；每日挑战只记录已解锁功能的进度，不负责主动解锁。
    /// </summary>
    private void StartVictoryProcess()
    {
        CancelVictoryProcess();
        _victoryProcessCancellationSource = new CancellationTokenSource();
        RunVictoryProcessAsync(_victoryProcessCancellationSource.Token).Forget();
    }

    private async UniTask RunVictoryProcessAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            DailyChallengeSettlement dailyChallengeSettlement = null;
            RecordLevelSuccessAnalytics();
            if (TryGetCurrentLevelData(out DrawLevelData currentLevelData) &&
                int.TryParse(currentLevelData.Id, out int dailyChallengeLevelId) &&
                dailyChallengeLevelId > 0)
            {
                GameManager.Instance?.DailyChallenge?.TryRecordVictory(
                    dailyChallengeLevelId,
                    out dailyChallengeSettlement);
            }
            else
            {
                this.LogWarning(
                    $"[DrawPlayCtrl] Daily challenge victory skipped because DrawLevelData.Id is invalid. sortId:{_drawLevelDisplayId}");
            }

            ReportLevelEndAnalytics(GameSDKService.LevelEndTypeSuccess);
            ResetCompletedLevelAnalyticsCounters();
            _shouldShowHintForCurrentAttempt = false;
            if (dailyChallengeSettlement != null)
            {
                await DailyChallengePopup.ShowAndWaitAsync(
                    dailyChallengeSettlement,
                    cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!_isActive ||
                _drawLevelRuntime == null ||
                _isAttemptTransitionPending ||
                _phase != DrawPlayPhase.Resolved)
            {
                return;
            }

            ShowVictoryPanel();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            ReleaseVictoryProcess(cancellationToken);
        }
    }

    private void CancelVictoryProcess()
    {
        CancellationTokenSource cancellationSource = _victoryProcessCancellationSource;
        _victoryProcessCancellationSource = null;
        if (cancellationSource == null)
            return;

        if (!cancellationSource.IsCancellationRequested)
            cancellationSource.Cancel();

        cancellationSource.Dispose();
    }

    private void ReleaseVictoryProcess(CancellationToken cancellationToken)
    {
        CancellationTokenSource cancellationSource = _victoryProcessCancellationSource;
        if (cancellationSource == null || cancellationSource.Token != cancellationToken)
            return;

        _victoryProcessCancellationSource = null;
        cancellationSource.Dispose();
    }

    private void ShowVictoryPanel()
    {
        if (!TryGetCurrentLevelData(out DrawLevelData currentLevelData))
        {
            this.LogError(
                $"[DrawPlayCtrl] Current DrawLevelData is missing. sortId:{_drawLevelDisplayId}");
            return;
        }

        bool isFirstVictory = IsFirstVictory();
        int victoryStarCount = Mathf.Clamp(
            _drawLevelRuntime?.InkState.StarCount ?? 0,
            0,
            3);
        TryRecordVictoryStars(currentLevelData.Id, victoryStarCount);
        bool isDeepLinkEntry = OneLinkManager.Instance.IsCurrentEntry(GameType.Draw);
        bool isPanelCreated = isDeepLinkEntry
            ? GameVictoryPanel_DeepLink.Create(currentLevelData) != null
            : isFirstVictory
                ? DrawGame_GameVictoryPanel_Reward.Create(currentLevelData) != null
                : DrawGame_GameVictoryPanel.Create(currentLevelData) != null;
        RecordVictoryProgress();

        if (isPanelCreated)
            return;

        this.LogError(
            $"[DrawPlayCtrl] Failed to create Draw victory panel. firstVictory:{isFirstVictory}");
        CleanupLevelRuntime();
        if (!OpenLevelSelection(out string error))
            this.LogError($"[DrawPlayCtrl] Victory fallback failed. error:{error}");
    }

    private bool IsFirstVictory()
    {
        DrawGame saveData = DB.DrawGame;
        if (saveData?.PassedLevels == null)
            return false;

        return !saveData.PassedLevels.Contains(_drawLevelDisplayId);
    }

    /// <summary>
    /// 将本次 Draw 星级按历史最高值写入永久记录，并累加 VictoryPanel 待播放的新星数量。
    /// 调用边界：仅在成功结算且创建胜利面板前调用；保存失败时回滚本次星级变更。
    /// </summary>
    private bool TryRecordVictoryStars(string levelDataId, int victoryStarCount)
    {
        DrawGame saveData = DB.DrawGame;
        if (saveData == null || string.IsNullOrEmpty(levelDataId))
        {
            this.LogWarning(
                $"[DrawPlayCtrl] Cannot record Draw stars. levelId:{levelDataId}, sortId:{_drawLevelDisplayId}");
            return false;
        }

        bool repairedCollections = saveData.EnsureValidCollections();
        int normalizedStarCount = Mathf.Clamp(victoryStarCount, 0, 3);
        bool hadCollectedCount = saveData.LevelCollectedStar.TryGetValue(
            levelDataId,
            out int originalCollectedCount);
        int collectedStarCount = Mathf.Clamp(originalCollectedCount, 0, 3);
        int addedStarCount = Mathf.Max(0, normalizedStarCount - collectedStarCount);
        if (addedStarCount <= 0)
        {
            if (!repairedCollections || DB.SaveDrawGame())
                return true;

            this.LogWarning(
                $"[DrawPlayCtrl] Failed to save repaired Draw star collections. levelId:{levelDataId}");
            return false;
        }

        bool hadWaitingCount = saveData.LevelWaitingAnimStar.TryGetValue(
            levelDataId,
            out int originalWaitingCount);
        saveData.LevelCollectedStar[levelDataId] = normalizedStarCount;
        saveData.LevelWaitingAnimStar[levelDataId] =
            Mathf.Max(0, originalWaitingCount) + addedStarCount;

        if (DB.SaveDrawGame())
            return true;

        if (hadCollectedCount)
            saveData.LevelCollectedStar[levelDataId] = originalCollectedCount;
        else
            saveData.LevelCollectedStar.Remove(levelDataId);

        if (hadWaitingCount)
            saveData.LevelWaitingAnimStar[levelDataId] = originalWaitingCount;
        else
            saveData.LevelWaitingAnimStar.Remove(levelDataId);

        this.LogWarning(
            $"[DrawPlayCtrl] Failed to save Draw stars. levelId:{levelDataId}, starCount:{normalizedStarCount}");
        return false;
    }

    private void RecordVictoryProgress()
    {
        DrawGame saveData = DB.DrawGame;
        if (saveData == null)
        {
            this.LogError("[DrawPlayCtrl] Cannot record victory because DB.DrawGame is unavailable.");
            return;
        }

        bool changed = saveData.EnsureValidCollections();
        int nextLevel = _drawLevelDisplayId + 1;
        if (!saveData.PassedLevels.Contains(_drawLevelDisplayId))
        {
            saveData.PassedLevels.Add(_drawLevelDisplayId);
            saveData.MaxLevel = Math.Max(saveData.MaxLevel, nextLevel);
            changed = true;
        }

        if (TryGetNextLevel(out DrawLevelSort nextSort, out _))
        {
            if (!saveData.UnlockLevels.Contains(nextSort.Id))
            {
                saveData.UnlockLevels.Add(nextSort.Id);
                changed = true;
            }

            if (saveData.CurrentLevel != nextSort.Id)
            {
                saveData.CurrentLevel = nextSort.Id;
                changed = true;
            }
        }

        if (changed && !DB.SaveDrawGame())
        {
            this.LogWarning(
                $"[DrawPlayCtrl] Failed to save Draw victory progress. sortId:{_drawLevelDisplayId}");
        }
    }

    private void OnEnterNextLevel(EnterNextLevelEvent evt)
    {
        if (!_isActive || _phase != DrawPlayPhase.Resolved)
            return;

        bool isDeepLinkEntry = OneLinkManager.Instance.IsCurrentEntry(GameType.Draw);
        bool hasNextLevel = isDeepLinkEntry
            ? TryGetLowestUnpassedLevel(out DrawLevelSort nextSort, out DrawLevelData nextLevelData)
            : TryGetNextLevel(out nextSort, out nextLevelData);

        string enterError = null;
        if (hasNextLevel &&
            TryBeginLevelTransition(
                nextSort.Id,
                nextLevelData.Path,
                keepCurrentLevelUntilCovered: true,
                out enterError))
        {
            if (isDeepLinkEntry)
                OneLinkManager.Instance.EndEntry(GameType.Draw);
            return;
        }

        if (isDeepLinkEntry)
            OneLinkManager.Instance.EndEntry(GameType.Draw);

        if (hasNextLevel)
            this.LogError($"[DrawPlayCtrl] Enter next Draw level failed. error:{enterError}");

        CleanupLevelRuntime();
        if (!OpenLevelSelection(out string selectionError))
        {
            this.LogError(
                $"[DrawPlayCtrl] Open Draw level selection after victory failed. error:{selectionError}");
        }
    }

    private void OnGameReplayEvent(GameReplayEvent evt)
    {
        if (!_isActive ||
            _isAttemptTransitionPending ||
            _phase != DrawPlayPhase.Resolved)
        {
            return;
        }

        _isAttemptTransitionPending = true;
        CancelVictoryProcess();
        _gamePlayPanel?.BeginResetTransition();
        PrepareHintVisibilityForSameLevelAttempt();
        PrepareTipsWarningForAttempt();

        DrawGame saveData = DB.DrawGame;
        if (saveData != null && saveData.CurrentLevel != _drawLevelDisplayId)
        {
            saveData.CurrentLevel = _drawLevelDisplayId;
            if (!DB.SaveDrawGame())
            {
                this.LogWarning(
                    $"[DrawPlayCtrl] Failed to save replay level. sortId:{_drawLevelDisplayId}");
            }
        }

        StartReplayAnalytics();
        ResetGameAsync(
            "VictoryReplay",
            _activationVersion,
            showFailureInterstitial: false).Forget();
    }

    #region Level Analytics

    private void StartNewLevelAnalytics()
    {
        if (_isAnalyticsAttemptActive)
            ReportLevelEndAnalytics(GameSDKService.LevelEndTypeExit);

        ResetLevelAnalyticsContext();
        if (!TryGetCurrentLevelData(out DrawLevelData currentLevelData) ||
            string.IsNullOrEmpty(currentLevelData.Id))
        {
            this.LogWarning(
                $"[DrawPlayCtrl] Draw level_start skipped because DrawLevelData is missing. sortId:{_drawLevelDisplayId}");
            return;
        }

        _analyticsLevelDataId = currentLevelData.Id;
        _analyticsLevelPath = currentLevelData.Path;
        _isAnalyticsAttemptActive = true;

        DrawGame saveData = DB.DrawGame;
        int playCount = 0;
        if (saveData != null)
        {
            saveData.EnsureValidCollections();
            saveData.LevelPlayCount.TryGetValue(_analyticsLevelDataId, out playCount);
            saveData.LevelPlayCount[_analyticsLevelDataId] = ++playCount;
            SaveDrawAnalytics("level start");
        }
        else
        {
            this.LogWarning("[DrawPlayCtrl] DrawGame save data is missing during level_start.");
        }

        GameSDKService.Instance.DrawLevelStartEvent(
            _analyticsLevelPath,
            playCount,
            GetCurrentLevelStartType(saveData));
    }

    private void StartResetAnalytics()
    {
        if (string.IsNullOrEmpty(_analyticsLevelDataId))
        {
            this.LogWarning(
                $"[DrawPlayCtrl] Draw reset level_start skipped because analytics context is missing. sortId:{_drawLevelDisplayId}");
            return;
        }

        _analyticsCurrentLevelTimeSeconds = 0f;
        _isAnalyticsAttemptActive = true;

        DrawGame saveData = DB.DrawGame;
        int playCount = 0;
        if (saveData != null)
        {
            saveData.EnsureValidCollections();
            saveData.LevelPlayCount.TryGetValue(_analyticsLevelDataId, out playCount);
            saveData.LevelPlayCount[_analyticsLevelDataId] = ++playCount;
            SaveDrawAnalytics("level reset");
        }

        GameSDKService.Instance.DrawLevelStartEvent(
            _analyticsLevelPath,
            playCount,
            GetCurrentLevelStartType(saveData));
    }

    private void StartReplayAnalytics()
    {
        if (string.IsNullOrEmpty(_analyticsLevelDataId))
        {
            this.LogWarning(
                $"[DrawPlayCtrl] Draw replay level_start skipped because analytics context is missing. sortId:{_drawLevelDisplayId}");
            return;
        }

        _analyticsCurrentLevelTimeSeconds = 0f;
        _analyticsReviveCount = 0;
        _isAnalyticsAttemptActive = true;

        DrawGame saveData = DB.DrawGame;
        int playCount = 0;
        if (saveData != null)
        {
            saveData.EnsureValidCollections();
            saveData.LevelPlayCount.TryGetValue(_analyticsLevelDataId, out playCount);
            saveData.LevelPlayCount[_analyticsLevelDataId] = ++playCount;
            SaveDrawAnalytics("level replay");
        }

        GameSDKService.Instance.DrawLevelStartEvent(
            _analyticsLevelPath,
            playCount,
            GetCurrentLevelStartType(saveData));
    }

    private int GetCurrentLevelStartType(DrawGame saveData)
    {
        return saveData?.PassedLevels?.Contains(_drawLevelDisplayId) == true
            ? GameSDKService.LevelStartTypeReplay
            : GameSDKService.LevelStartTypeNewGame;
    }

    private void TickLevelAnalytics(float deltaSeconds)
    {
        if (!_isAnalyticsAttemptActive ||
            _drawLevelRuntime == null ||
            _phase == DrawPlayPhase.Resolved ||
            deltaSeconds <= 0f)
        {
            return;
        }

        _analyticsCurrentLevelTimeSeconds += deltaSeconds;
    }

    private void RecordLevelRetryAnalytics()
    {
        _analyticsReviveCount++;

        DrawGame saveData = DB.DrawGame;
        if (saveData == null || string.IsNullOrEmpty(_analyticsLevelDataId))
            return;

        saveData.EnsureValidCollections();
        saveData.LevelReviveCount.TryGetValue(_analyticsLevelDataId, out int reviveCount);
        saveData.LevelReviveCount[_analyticsLevelDataId] = reviveCount + 1;
        SaveDrawAnalytics("level retry");
    }

    private void RecordLevelSuccessAnalytics()
    {
        DrawGame saveData = DB.DrawGame;
        if (saveData == null || string.IsNullOrEmpty(_analyticsLevelDataId))
            return;

        saveData.EnsureValidCollections();
        saveData.LevelSuccessCount.TryGetValue(_analyticsLevelDataId, out int successCount);
        saveData.LevelSuccessCount[_analyticsLevelDataId] = successCount + 1;
        SaveDrawAnalytics("level success");
    }

    private void ReportLevelEndAnalytics(int levelEndType)
    {
        if (!_isAnalyticsAttemptActive || string.IsNullOrEmpty(_analyticsLevelDataId))
            return;

        long levelDuration = ToAnalyticsMilliseconds(_analyticsCurrentLevelTimeSeconds);
        long totalDuration = RecordLevelDurationAnalytics(levelDuration);
        int reviveCount = _analyticsReviveCount;

        GameSDKService.Instance.DrawLevelEndEvent(
            _analyticsLevelDataId,
            _analyticsLevelPath,
            levelDuration,
            totalDuration,
            reviveCount,
            levelEndType);
        _isAnalyticsAttemptActive = false;
    }

    private long RecordLevelDurationAnalytics(long levelDuration)
    {
        DrawGame saveData = DB.DrawGame;
        if (saveData == null || string.IsNullOrEmpty(_analyticsLevelPath))
        {
            this.LogWarning(
                $"[DrawPlayCtrl] Draw level duration save skipped because analytics context is missing. levelDataId:{_analyticsLevelDataId}");
            return levelDuration;
        }

        saveData.EnsureValidCollections();
        saveData.LevelPlayDuration.TryAdd(_analyticsLevelPath, 0L);
        saveData.LevelPlayDuration[_analyticsLevelPath] += levelDuration;
        SaveDrawAnalytics("level duration");
        return saveData.LevelPlayDuration[_analyticsLevelPath];
    }

    private void ResetCompletedLevelAnalyticsCounters()
    {
        _analyticsCurrentLevelTimeSeconds = 0f;
        _analyticsReviveCount = 0;
    }

    private void ResetLevelAnalyticsContext()
    {
        _analyticsLevelDataId = null;
        _analyticsLevelPath = null;
        _analyticsCurrentLevelTimeSeconds = 0f;
        _analyticsReviveCount = 0;
        _isAnalyticsAttemptActive = false;
    }

    private void SaveDrawAnalytics(string operation)
    {
        if (!DB.SaveDrawGame())
        {
            this.LogWarning(
                $"[DrawPlayCtrl] Failed to save Draw analytics. operation:{operation}, levelDataId:{_analyticsLevelDataId}");
        }
    }

    private static long ToAnalyticsMilliseconds(float seconds)
    {
        return seconds <= 0f ? 0L : (long)(seconds * 1000f);
    }

    #endregion

    private bool TryGetCurrentLevelData(out DrawLevelData currentLevelData)
    {
        currentLevelData = _currentLevelData;
        return currentLevelData != null;
    }

    /// <summary>
    /// 当前是否为 Draw 的 TBC 占位关 DLevel_999。
    /// </summary>
    private bool IsTbcLevel()
    {
        return string.Equals(
            _drawLevelPath,
            TbcLevelResourcePath,
            StringComparison.OrdinalIgnoreCase);
    }

    private bool TryGetNextLevel(
        out DrawLevelSort nextSort,
        out DrawLevelData nextLevelData)
    {
        nextSort = null;
        nextLevelData = null;

        if (IsTbcLevel()) return false;

        DrawGameLevelCtrl levelCtrl = GameManager.Instance?.DrawGameLevel;
        IReadOnlyList<DrawLevelSort> sortData = levelCtrl?.LevelSorts;
        if (levelCtrl == null || !levelCtrl.IsReady || sortData == null)
            return false;

        int currentIndex = -1;
        for (int i = 0; i < sortData.Count; i++)
        {
            if (sortData[i] == null || sortData[i].Id != _drawLevelDisplayId)
                continue;

            currentIndex = i;
            break;
        }

        if (currentIndex < 0 || currentIndex + 1 >= sortData.Count)
            return false;

        nextSort = sortData[currentIndex + 1];
        nextLevelData = nextSort == null
            ? null
            : levelCtrl.GetLevelDataById(nextSort.Path);
        if (nextSort != null && nextLevelData == null)
        {
            this.LogError(
                $"[DrawPlayCtrl] Next DrawLevelData is missing. sortId:{nextSort.Id}, dataId:{nextSort.Path}");
        }

        return nextSort != null && nextLevelData != null;
    }

    /// <summary>
    /// 解析当前有效序列中最靠前且尚未通关的 Draw 关卡。
    /// DeepLink 胜利下一关只选择目标，实际解锁、存档、翻页遮罩和资源重建仍由 TryBeginLevelTransition 统一处理。
    /// </summary>
    private bool TryGetLowestUnpassedLevel(
        out DrawLevelSort targetSort,
        out DrawLevelData targetLevelData)
    {
        targetSort = null;
        targetLevelData = null;

        DrawGameLevelCtrl levelCtrl = GameManager.Instance?.DrawGameLevel;
        DrawGame saveData = DB.DrawGame;
        IReadOnlyList<DrawLevelSort> sortData = levelCtrl?.LevelSorts;
        if (levelCtrl == null || !levelCtrl.IsReady || saveData == null || sortData == null)
            return false;

        saveData.EnsureValidCollections();
        for (int i = 0; i < sortData.Count; i++)
        {
            DrawLevelSort candidate = sortData[i];
            if (candidate == null || candidate.Id <= 0 || saveData.PassedLevels.Contains(candidate.Id))
                continue;

            DrawLevelData candidateData = levelCtrl.GetLevelDataById(candidate.Path);
            if (candidateData == null || string.IsNullOrWhiteSpace(candidateData.Path))
                continue;

            targetSort = candidate;
            targetLevelData = candidateData;
            return true;
        }

        return false;
    }

    private async UniTask ResetGameAsync(
        string source,
        int activationVersion,
        bool showFailureInterstitial)
    {
        UIManager.Instance.CloseAllUI(UILayer.Popup);
        await UniTask.WaitForSeconds(ManualResetFadeOutDelaySeconds, ignoreTimeScale: true);
        if (!IsCurrentActivation(activationVersion))
            return;

        await ReloadCurrentLevelAsync(source, showFailureInterstitial);
    }

    /// <summary>
    /// 按主线 Reset 语义销毁并重新创建当前 DLevel，确保 Actor、机关、线条和 HUD 全部回到初始状态。
    /// 调用边界：仅由 Draw HUD 的 Reset 或当前关卡首次失败后的自动重试调用。
    /// 注意事项：立即提升激活版本，使并发点击和旧的失败延迟回调不能重复创建关卡。
    /// </summary>
    private async UniTask ReloadCurrentLevelAsync(
        string source,
        bool showFailureInterstitial)
    {
        if (!_isActive ||
            _isOpeningLevel ||
            string.IsNullOrWhiteSpace(_drawLevelPath))
        {
            return;
        }

        CleanupLevelRuntime();
        _isOpeningLevel = true;
        int activationVersion = ++_activationVersion;
        this.Log($"[DrawPlayCtrl] Reload draw level. source:{source}, path:{_drawLevelPath}");

        await OpenDrawLevelAsync(activationVersion, true, true);
        if (!IsCurrentActivation(activationVersion) ||
            _drawLevelRuntime == null ||
            !_drawLevelRuntime.IsRuntimeReady)
        {
            return;
        }

        if (_gamePlayPanel == null)
        {
            HandleOpenFailure(activationVersion, "Draw GameplayPanel is missing during reset.");
            return;
        }

        // 新实例仍处于全黑遮罩时先落到 Intro 完成态，避免淡入首帧闪出场外起点。
        await _drawLevelRuntime.PlayResetIntroAsync();
        if (!IsCurrentActivation(activationVersion))
            return;

        if (showFailureInterstitial)
        {
            await WaitForFailureInterstitialAsync();
            if (!IsCurrentActivation(activationVersion))
                return;
        }

        this.DispatchEvent(Witness<GamePlayPanelFadeInEvent>._);
        await UniTask.WaitForSeconds(
            ResetFadeInInputDelaySeconds,
            ignoreTimeScale: true);
        if (!IsCurrentActivation(activationVersion))
            return;

        CompleteIntroAndBeginDrawing();
    }

    /// <summary>
    /// 在新 DLevel 与存活 Actor 已完成重建、HUD 仍保持全黑时请求失败/重试插屏。
    /// 调用边界：仅由 Reset 重建链调用；策略拒绝、SDK 未就绪或广告关闭都会通过既有结果回调结束等待。
    /// 注意事项：本方法不负责淡出黑幕或恢复输入，调用方必须在活动版本校验通过后继续统一恢复流程。
    /// </summary>
    private static async UniTask WaitForFailureInterstitialAsync()
    {
        var completionSource = new UniTaskCompletionSource();
        InterstitialAdHelper.TryToShowInterstitial(
            Config.Game.InterstitialPlacement.InGameFailed.ToString(),
            (_, _) => completionSource.TrySetResult());
        await completionSource.Task;
    }

    private static IDrawLevelRuntime ResolveDrawLevelRuntime(GameObject levelInstance)
    {
        if (levelInstance == null)
            return null;

        MonoBehaviour[] behaviours = levelInstance.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IDrawLevelRuntime runtime)
                return runtime;
        }

        return null;
    }

    private static string ResolveLevelResourcePath(string levelPath)
    {
        string normalizedPath = levelPath.Trim().Replace('\\', '/');
        if (normalizedPath.StartsWith("Assets/", StringComparison.Ordinal))
            return normalizedPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                ? normalizedPath
                : $"{normalizedPath}.prefab";

        string fileName = normalizedPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
            ? normalizedPath
            : $"{normalizedPath}.prefab";
        return $"{DrawLevelDirectory}{fileName}";
    }

    #endregion
}
