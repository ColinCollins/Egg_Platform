using System;
using Bear.EventSystem;
using Bear.Logger;
using Bear.ResourceSystem;
using Bear.UI;
using Config;
using Cysharp.Threading.Tasks;
using Game.Dig;
using Game;
using Game.ConfigModule;
using Game.Events;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>Dig 模式加载、HUD、转场与完整重建；关卡失败不写通关，统计按四玩法生命周期约定上报。</summary>
public sealed partial class DigPlayCtrl : IPlayController, IInterstitialAdPolicyHost, IEventSender, IDebuger, IGMFeature
{
    #region 状态与生命周期
    private EventSubscriber _subscriber;
    private GameObject _instance;
    private IDigLevelRuntime _runtime;
    private DigGame_ChoiceLevelPanel _choice;
    private DigInterstitialAdPolicy _interstitialAdPolicy;
    private bool _tipsWarningShown;
    private float _showTipsTimeLimit;
    private int _showTipsFailCount;
    /// <summary>Dig 插屏只使用独立策略与当前进入时的排序关号。</summary>
    public IInterstitialAdPolicy InterstitialAdPolicy => _interstitialAdPolicy;
    /// <inheritdoc />
    public int InterstitialLevel => _levelId;
    /// <summary>暂停、原生广告和转场期间延迟提交 Dig 配置；正在玩的内容不重建。</summary>
    internal bool IsRemoteConfigRefreshBlocked => IsActive && (_transitioning || _paused || _choice?.IsRewardAdPending == true || _hud?.IsRewardAdRequesting == true);
    /// <summary>云控提交日志的当前门禁快照。</summary>
    internal string RemoteConfigRefreshState => $"active={IsActive}, transition={_transitioning}, paused={_paused}, choiceAd={_choice?.IsRewardAdPending == true}, inkAd={_hud?.IsRewardAdRequesting == true}";
    /// <summary>配置提交后只刷新现存选关界面，运行中的数据与统计上下文保留。</summary>
    internal void RefreshRemoteConfigViews() => _choice?.RefreshRemoteConfig();
    private bool IsTbc => _data?.Id == DigGameLevelCtrl.TbcDataId;
    private DigGame_GamePlayPanel _hud;
    private BaseUIView _victory;
    private DigLevelData _data;
    private int _levelId, _version;
    private string _resourcePath;
    private bool _transitioning, _paused, _resolved;
    private bool _rewardInkGranted, _hintGranted;
    /// <summary>同关广告领取资格；Reset/Replay 保留，切关或退出清除。</summary>
    internal bool IsRewardInkGranted => _rewardInkGranted;
    /// <summary>同关提示获取资格；用于提示弹窗选择提示或跳关资源。</summary>
    internal bool HasGrantedHint => _hintGranted;
    /// <summary>只有普通关有本地提示且结果未锁定时才开放提示弹窗；弹窗暂停期间仍可完成操作。</summary>
    internal bool IsHintInteractionReady => IsActive && !_transitioning && !_resolved && _runtime?.IsRuntimeReady == true && !_runtime.IsResultLocked && _runtime.HasHint;
    /// <summary>当前关卡版本；异步广告与提示回调必须同时匹配控制器和版本。</summary>
    internal int LevelVersion => _version;
    /// <summary>供当前 HUD 读取奖励后的状态；TBC 返回空快照。</summary>
    internal DrawInkState CurrentInkState => _runtime?.InkState ?? default;
    /// <inheritdoc />
    public PlayModeType Mode => PlayModeType.Dig;
    /// <inheritdoc />
    public bool IsInitialized { get; private set; }
    /// <inheritdoc />
    public bool IsActive { get; private set; }
    /// <inheritdoc />
    public void Initialize(PlayControllerContext context)
    {
        IsInitialized = context != null;
        _interstitialAdPolicy = new DigInterstitialAdPolicy();
    }
    /// <inheritdoc />
    public void Activate()
    {
        if (!IsInitialized) throw new InvalidOperationException("Dig 控制器未初始化。");
        if (IsActive) return;
        IsActive = true;
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<DigGameResetEvent>(OnReset);
        _subscriber.Subscribe<GameReplayEvent>(OnReplay);
        _subscriber.Subscribe<EnterNextLevelEvent>(OnNext);
        _choice = DigGame_ChoiceLevelPanel.Create();
        if (!_choice) throw new InvalidOperationException("Dig 独立选关面板加载失败。");
    }
    /// <inheritdoc />
    public void Deactivate()
    {
        Game.OneLinkManager.Instance.EndEntry(Config.Level.GameType.Dig);
        ReportLevelEndAnalytics(GameSDKService.LevelEndTypeExit);
        ResetLevelAnalyticsContext();
        IsActive = false; _version++;
        if (_choice) UIManager.Instance.DestroyUI(_choice);
        _choice = null;
        EventsUtils.ResetEvents(ref _subscriber);
        DestroyRuntime();
        DestroyHud();
        CloseVictory();
        if (!string.IsNullOrEmpty(_resourcePath)) ResourceManager.Instance.Release(_resourcePath);
        _resourcePath = null;
        _transitioning = _paused = _resolved = false;
        _rewardInkGranted = _hintGranted = false;
    }
    /// <inheritdoc />
    public void Shutdown() { Deactivate(); _interstitialAdPolicy?.Dispose(); _interstitialAdPolicy = null; IsInitialized = false; }
    /// <inheritdoc />
    public void Tick()
    {
        // FixedUpdate 可以先于 GameManager.Update 锁定死亡，本帧立即同步 HUD 门禁。
        _hud?.SetDigInteraction(CanInteract());
        if (!IsActive) return;
        if (Input.GetKeyDown(KeyCode.Escape)) ShowExitPopup();
        if (_paused || _transitioning || _resolved || _runtime?.IsRuntimeReady != true) return;
        TickLevelAnalytics(Time.deltaTime);
        if (!IsTbc && !_hintGranted && _runtime.HasHint && !_tipsWarningShown &&
            (_analyticsReviveCount >= _showTipsFailCount ||
             _analyticsCurrentLevelTimeSeconds > _showTipsTimeLimit))
        {
            _tipsWarningShown = true;
            this.DispatchEvent(Witness<GamePlayPanelSwitchTipsEvent>._, true);
        }
    }
    private bool CanInteract() => IsActive && !_transitioning && !_paused && _runtime?.IsRuntimeReady == true && !_runtime.IsResultLocked;
    #endregion

    #region GM 关卡调试
    /// <summary>GM 按当前有效排序进入下一关；末尾拒绝，不把当前关记为通关。</summary>
    public bool TryEnterNextLevel() => TryEnterAdjacentLevel(1);

    /// <summary>GM 按当前有效排序进入上一关；首关拒绝，不修改其它玩法的进度。</summary>
    public bool TryEnterPreviousLevel() => TryEnterAdjacentLevel(-1);

    /// <summary>GM 通过普通关的统一胜利入口结算，保留延迟、统计、广告及胜利界面；TBC 拒绝。</summary>
    /// <param name="source">调试命令来源，仅用于日志，不改变关卡统计口径。</param>
    public bool TryForceVictory(string source)
    {
        if (!CanExecuteGMCommand() || IsTbc || _runtime is not DigLevelCtrl level || !level.TryForceVictory())
            return false;
        this.Log($"[Dig] GM 胜利请求已接受。level:{_levelId}, source:{source}");
        return true;
    }

    private bool CanExecuteGMCommand() => IsInitialized && CanInteract() && !_resolved &&
        !IsRemoteConfigRefreshBlocked && (_runtime is not DigLevelCtrl level || !level.IsPaused);

    private bool TryEnterAdjacentLevel(int direction)
    {
        var levels = GameManager.Instance?.DigGameLevel;
        if (!CanExecuteGMCommand() || _data == null || levels?.IsReady != true || DB.DigGame == null)
            return false;
        int currentIndex = -1;
        for (int i = 0; i < levels.LevelSorts.Count; i++)
            if (levels.LevelSorts[i]?.Path == _data.Id) { currentIndex = i; break; }
        int targetIndex = currentIndex + direction;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= levels.LevelSorts.Count)
            return false;
        DigLevelSort target = levels.LevelSorts[targetIndex];
        if (target == null || !levels.TryGetLevel(target.Id, out _)) return false;
        if (!TryEnterLevel(target.Id, true, out string error))
        {
            this.LogWarning($"[Dig] GM 切关请求被拒绝。level:{target.Id}, error:{error}");
            return false;
        }
        DB.DigGame.CurrentLevel = target.Id;
        if (!DB.SaveDigGame()) this.LogWarning($"[Dig] GM 当前关卡保存失败。level:{target.Id}");
        OneLinkManager.Instance.EndEntry(Config.Level.GameType.Dig);
        return true;
    }
    #endregion

    #region 退出应用
    /// <summary>活动 Dig 收到 Escape/Android Back 时打开默认退出确认；取消保留原暂停状态，不进入菜单选关流程。</summary>
    private void ShowExitPopup()
    {
        if (!CanShowExitPopup()) return;

        CommmonPopup.Create(
            title: I2.Loc.LocalizationManager.GetTranslation("U_Quit_Title_01"),
            content: I2.Loc.LocalizationManager.GetTranslation("U_Quit_Des_01"),
            onYes: () =>
            {
                Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            },
            onNo: null);
    }

    /// <summary>防止退出弹窗重复或插入引导、广告、转场和结果处理；普通选关及非转场暂停仍允许确认退出。</summary>
    private bool CanShowExitPopup()
    {
        CommmonPopup exitPopup = UIManager.Instance?.GetUI<CommmonPopup>(nameof(CommmonPopup));
        if (exitPopup != null && exitPopup.IsOpen) return false;

        GuideFlowManager guideFlowManager = GuideFlowManager.Instance;
        if (guideFlowManager.HasActiveFlow || guideFlowManager.PendingCount > 0) return false;

        return !_transitioning && !_resolved && _runtime?.IsResultLocked != true &&
               _choice?.IsRewardAdPending != true && _hud?.IsRewardAdRequesting != true;
    }
    #endregion

    #region 加载与转场
    /// <summary>首页切换 Controller 后调用；接受当前有效排序关的进关请求。</summary>
    public bool TryEnterCurrentLevel(out string error)
        => TryEnterLevel(GameManager.Instance.DigGameLevel.ResolveCurrentLevel(), false, out error);

    /// <summary>普通入口按解锁记录进入；OneLink 与 GM 切关可解锁当前已配置目标，不能加载任意路径。</summary>
    internal bool TryEnterLevel(int levelId, bool allowLockedEntry, out string error)
    {
        error = null;
        var levels = GameManager.Instance.DigGameLevel;
        if (!IsActive || _transitioning || levels?.IsReady != true) { error = "Dig 入口状态不可用。"; return false; }
        if (!levels.TryGetLevel(levelId, out var data)) { error = "Dig 目标配置无效。"; return false; }
        if (!DB.DigGame.UnlockLevels.Contains(levelId))
        {
            if (!allowLockedEntry) { error = "Dig 目标未解锁。"; return false; }
            DB.DigGame.UnlockLevels.Add(levelId);
            if (!DB.SaveDigGame()) this.LogError("[Dig] 链接目标解锁保存失败。");
        }
        ReportLevelEndAnalytics(GameSDKService.LevelEndTypeExit);
        _levelId = levelId; _data = data;
        StartNewLevelAnalytics();
        Game.OneLinkManager.Instance.OnLevelEntryStarted(Config.Level.GameType.Dig);
        if (_choice) UIManager.Instance.CloseUI(_choice);
        _rewardInkGranted = _hintGranted = false;
        BeginTransition();
        return true;
    }

    private bool IsCurrent(int version) => IsActive && version == _version;
    private void BeginTransition()
    {
        _transitioning = true; _resolved = false; _paused = false;
        _runtime?.SetInputEnabled(false); _runtime?.SetPaused(true);
        _hud?.SetDigInteraction(false);
        int version = ++_version;
        BeginTransitionAsync(version).Forget();
    }

    private async UniTask BeginTransitionAsync(int version)
    {
        // 与 Draw 一致，避免本次按钮/弹窗关闭的调用栈影响新 Loading。
        await UniTask.NextFrame();
        if (!IsCurrent(version)) return;
        try
        {
            EnterLevelLoading.Create(async () =>
            {
                if (!IsCurrent(version)) return;
                await BuildLevelAsync(version);
                if (IsCurrent(version)) _hud.SetDigBlackMask(false);
            }, () => OpenInput(version));
        }
        catch (Exception ex) { HandleFailure(version, ex); }
    }

    private async UniTask BuildLevelAsync(int version)
    {
        try
        {
            // 只在翻页遮挡完成或 Reset 全黑后执行；先失活再销毁旧实例。
            DestroyRuntime(); DestroyHud(); CloseVictory();
            UIManager.Instance.CloseAllUI(UILayer.Normal);
            _hud = DigGame_GamePlayPanel.Create();
            if (!_hud) throw new InvalidOperationException("Dig 独立 HUD 加载失败。");
            _hud.ConfigureForDig(_levelId, CanInteract); _hud.SetDigBlackMask(true);
            string path = $"Assets/Game/Prefabs/DigLevel/{_data.Path}.prefab";
            if (!string.IsNullOrEmpty(_resourcePath) && _resourcePath != path) ResourceManager.Instance.Release(_resourcePath);
#if UNITY_EDITOR
            // 编辑器模拟模式下，同关重建重新读取已保存的 Prefab；打包资源仍沿用原缓存策略。
            if (_resourcePath == path && Game.HotReload.HotReloadCtrl.IsInitialized
                && Game.HotReload.HotReloadCtrl.Instance.PlayMode == YooAsset.EPlayMode.EditorSimulateMode)
                ResourceManager.Instance.Release(path);
#endif
            _resourcePath = path;
            GameObject prefab = await ResourceManager.Instance.LoadAsync<GameObject>(path);
            if (!IsCurrent(version)) return;
            if (!prefab) throw new InvalidOperationException($"Dig 关卡资源缺失：{path}");
            GameManager.Instance.CloseCamera();
            _instance = UnityEngine.Object.Instantiate(prefab);
            _runtime = IsTbc
                ? (IDigLevelRuntime)_instance.GetComponent<DigLevel999Ctrl>()
                : _instance.GetComponent<DigLevelCtrl>();
            if (_runtime == null) throw new InvalidOperationException("DigLevel 根节点缺少运行时组件。");
            _runtime.Resolved += OnResolved;
            if (_runtime is DigLevelCtrl digRuntime)
                digRuntime.ConfigureInk(_data.MaxInkLength, new Vector3(_data.InkStarStage1, _data.InkStarStage2, _data.InkStarStage3));
            _runtime.InitializeRuntime();
            _runtime.SetInputEnabled(false); _runtime.SetPaused(true);
            if (_rewardInkGranted) _runtime.AddRewardInk();
            _runtime.ShowHint(_hintGranted);
            _runtime.InkChanged += OnInkChanged;
            _runtime.CountdownChanged += OnCountdownChanged;
            _hud.BindRuntime(_runtime.InkState, _rewardInkGranted, _runtime.HasHint, !IsTbc);
            _hud.SetCountdownSeconds(_runtime.CountdownSeconds);
            if (IsTbc) AudioManager.PlaySound("TBC");
            DB.DigGame.CurrentLevel = _levelId;
            if (!DB.SaveDigGame()) this.LogError("[Dig] 当前关保存失败。");
        }
        catch (Exception ex) { HandleFailure(version, ex); }
    }

    private void OpenInput(int version)
    {
        if (!IsCurrent(version) || _runtime?.IsRuntimeReady != true) return;
        _transitioning = false; _paused = false; _resolved = false;
        // 复用现有状态事件恢复 SceneSpriteBrushClear 的外部擦除门禁。
        this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PLAYING);
        _runtime.SetPaused(false); _runtime.SetInputEnabled(true);
        _hud.SetDigInteraction(true);
        _tipsWarningShown = false;
        _showTipsFailCount = ConfigManager.RemoteConfig.GetDigGameShowTipsFailCount();
        _showTipsTimeLimit = ConfigManager.RemoteConfig.GetDigGameShowTipsTimeLimit();
        this.DispatchEvent(Witness<GamePlayPanelSwitchTipsEvent>._, false);
        TryToShowNoAdPopup();
        Game.OneLinkManager.Instance.ConfirmLevelEntered(Config.Level.GameType.Dig);
    }

    private void HandleFailure(int version, Exception exception)
    {
        if (!IsCurrent(version)) return;
        this.LogError($"[Dig] 加载/重建失败，返回首页：{exception}");
        ExitToLevelSelection();
    }
    #endregion

    #region 结果与完整重建
    private void OnResolved(DigLevelResult result)
    {
        if (!IsActive || _transitioning || _resolved || _runtime?.IsRuntimeReady != true || !_runtime.IsResultLocked) return;
        _resolved = true;
        _runtime.SetInputEnabled(false);
        _runtime.SetPaused(true);
        _hud?.SetDigInteraction(false);
        this.Log($"[Dig] 关卡结算。level:{_levelId}, result:{result}");
        if (result == DigLevelResult.Failed)
        {
            this.DispatchEvent(Witness<DigGameResetEvent>._, GameResetType.Failed);
            return;
        }
        if (IsTbc) return;
        RecordLevelSuccessAnalytics();
        ReportLevelEndAnalytics(GameSDKService.LevelEndTypeSuccess);
        ResetCompletedLevelAnalyticsCounters();
        _interstitialAdPolicy.OnLevelSucceeded();
        int version = _version;
        InterstitialAdHelper.TryToShowInterstitial(Config.Game.InterstitialPlacement.InGameVictory.ToString(),
            (_, _) => { if (IsCurrent(version) && _resolved) ShowVictoryPanel(); });
    }

    private void ShowVictoryPanel()
    {
        try
        {
            var levels = GameManager.Instance.DigGameLevel;
            bool first = levels.IsFirstVictory(_levelId);
            int stars = Mathf.Clamp(_runtime.InkState.StarCount, 0, 3);
            TryRecordVictoryStars(_data.Id, stars);
            if (OneLinkManager.Instance.IsCurrentEntry(Config.Level.GameType.Dig))
            {
                _victory = GameVictoryPanel_DeepLink.CreateForDig(_data);
            }
            else
            {
                _victory = first ? DigGame_GameVictoryPanel_Reward.Create(_data) : DigGame_GameVictoryPanel.Create(_data);
            }
            if (!_victory) throw new InvalidOperationException("Dig 胜利面板加载失败。");
            // 与 Draw 的职责顺序一致：控制器创建胜利面板，再记录独立模式进度。
            levels.RecordVictory(_levelId, _data);
            this.Log($"[Dig] 胜利面板已创建。level:{_levelId}, panel:{_victory.GetType().Name}");
        }
        catch (Exception ex) { HandleFailure(_version, ex); }
    }

    /// <summary>
    /// 在胜利面板创建前保存 Dig 最高星与待播正增量，规则沿用 Draw。
    /// 调用边界：仅用于普通关成功结算；保存失败恢复原星级条目，TBC 不写入。
    /// </summary>
    private bool TryRecordVictoryStars(string levelDataId, int victoryStarCount)
    {
        var saveData = DB.DigGame;
        if (saveData == null || string.IsNullOrEmpty(levelDataId) || levelDataId == DigGameLevelCtrl.TbcDataId)
        {
            this.LogWarning($"[DigPlayCtrl] 无法记录 Dig 星级。levelId:{levelDataId}, sortId:{_levelId}");
            return false;
        }

        bool repairedCollections = saveData.EnsureValidCollections();
        int normalizedStarCount = Mathf.Clamp(victoryStarCount, 0, 3);
        bool hadCollectedCount = saveData.LevelCollectedStar.TryGetValue(levelDataId, out int originalCollectedCount);
        int collectedStarCount = Mathf.Clamp(originalCollectedCount, 0, 3);
        int addedStarCount = Mathf.Max(0, normalizedStarCount - collectedStarCount);
        if (addedStarCount <= 0)
        {
            if (!repairedCollections || DB.SaveDigGame())
                return true;

            this.LogWarning($"[DigPlayCtrl] 修复后的星级集合保存失败。levelId:{levelDataId}");
            return false;
        }

        bool hadWaitingCount = saveData.LevelWaitingAnimStar.TryGetValue(levelDataId, out int originalWaitingCount);
        saveData.LevelCollectedStar[levelDataId] = normalizedStarCount;
        saveData.LevelWaitingAnimStar[levelDataId] = Mathf.Max(0, originalWaitingCount) + addedStarCount;
        if (DB.SaveDigGame())
            return true;

        if (hadCollectedCount)
            saveData.LevelCollectedStar[levelDataId] = originalCollectedCount;
        else
            saveData.LevelCollectedStar.Remove(levelDataId);

        if (hadWaitingCount)
            saveData.LevelWaitingAnimStar[levelDataId] = originalWaitingCount;
        else
            saveData.LevelWaitingAnimStar.Remove(levelDataId);

        this.LogWarning($"[DigPlayCtrl] 星级保存失败。levelId:{levelDataId}, starCount:{normalizedStarCount}");
        return false;
    }

    private void OnReset(DigGameResetEvent evt)
    {
        if (!IsActive || _transitioning || _runtime == null) return;
        if (evt.Type == GameResetType.Manually && (_paused || _runtime.IsResultLocked)) return;
        if (evt.Type == GameResetType.Failed && (!_resolved || !_runtime.IsResultLocked)) return;
        RecordLevelRetryAnalytics();
        ReportLevelEndAnalytics(GameSDKService.LevelEndTypeFailed);
        StartResetAnalytics();
        _interstitialAdPolicy.OnPlayerFailedOrRetried();
        BeginRebuild(true);
    }
    private void OnReplay(GameReplayEvent evt)
    {
        if (IsActive && _resolved && !_transitioning)
        {
            StartReplayAnalytics();
            BeginRebuild(false);
        }
    }
    private void BeginRebuild(bool requestFailedInterstitial)
    {
        _transitioning = true; _paused = false;
        _hud.SetCountdownSeconds(0);
        _runtime.SetInputEnabled(false); _runtime.SetPaused(true);
        _hud.SetDigInteraction(false);
        CloseVictory();
        UIManager.Instance.CloseAllUI(UILayer.Popup);
        RebuildAsync(++_version, requestFailedInterstitial).Forget();
    }
    private async UniTask RebuildAsync(int version, bool requestFailedInterstitial)
    {
        try
        {
            await _hud.FadeDigMaskAsync(true);
            if (!IsCurrent(version)) return;
            await BuildLevelAsync(version);
            if (!IsCurrent(version)) return;
            if (requestFailedInterstitial)
            {
                var completion = new UniTaskCompletionSource();
                InterstitialAdHelper.TryToShowInterstitial(Config.Game.InterstitialPlacement.InGameFailed.ToString(),
                    (_, _) => completion.TrySetResult());
                await completion.Task;
                if (!IsCurrent(version)) return;
            }
            await _hud.FadeDigMaskAsync(false);
            OpenInput(version);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { HandleFailure(version, ex); }
    }
    private void OnNext(EnterNextLevelEvent evt)
    {
        if (!IsActive || !_resolved || _transitioning) return;
        var levels = GameManager.Instance.DigGameLevel;
        bool isDeepLinkEntry = OneLinkManager.Instance.IsCurrentEntry(Config.Level.GameType.Dig);
        DigLevelSort sort = null;
        if (isDeepLinkEntry)
        {
            // 专属面板的 Next 回到当前有效序列最靠前未通过关；普通全通时沿原查询进入 TBC。
            sort = levels.GetLevelSortById(levels.ResolveLowestUnpassed());
        }
        else
        {
            // 远端允许正在玩时更新列表；下一关须按进入时的内容在新列表中定位。
            int index = -1;
            for (int i = 0; i < levels.LevelSorts.Count; i++)
                if (levels.LevelSorts[i].Path == _data.Id) { index = i; break; }
            if (index >= 0 && index + 1 < levels.LevelSorts.Count) sort = levels.LevelSorts[index + 1];
        }
        var data = levels.GetLevelDataById(sort?.Path);
        if (data == null)
        {
            if (isDeepLinkEntry) OneLinkManager.Instance.EndEntry(Config.Level.GameType.Dig);
            ExitToLevelSelection();
            return;
        }
        _levelId = sort.Id; _data = data;
        if (!DB.DigGame.UnlockLevels.Contains(_levelId)) DB.DigGame.UnlockLevels.Add(_levelId);
        StartNewLevelAnalytics();
        _rewardInkGranted = _hintGranted = false;
        // 保留已结算旧关直到翻页完全遮挡。
        BeginTransition();
        if (isDeepLinkEntry) OneLinkManager.Instance.EndEntry(Config.Level.GameType.Dig);
    }
    #endregion

    #region 墨水与提示会话
    private void OnInkChanged(DrawInkState state) => _hud?.SetInkState(state);

    /// <summary>只转发当前已绑定关卡的局部倒计时；旧关销毁前解除订阅，不接收其它玩法事件。</summary>
    private void OnCountdownChanged(int seconds) => _hud?.SetCountdownSeconds(seconds);

    /// <summary>广告开始前在仍可操作的普通关暂停物理并结束笔画；同一关只允许领取一次。</summary>
    internal bool TryBeginRewardInk()
    {
        if (!CanInteract() || _rewardInkGranted || IsTbc || _runtime.InkState.UsedInk > 0f) return false;
        Pause();
        return true;
    }

    /// <summary>当前有效广告回调成功后增加基础容量；暂停中的奖励不恢复物理，由 HUD 成对 Resume。</summary>
    internal bool TryGrantRewardInk()
    {
        if (!IsActive || _transitioning || _resolved || _rewardInkGranted || _runtime?.IsRuntimeReady != true || _runtime.IsResultLocked || IsTbc) return false;
        _rewardInkGranted = true;
        _runtime.AddRewardInk();
        return true;
    }

    /// <summary>提示购买完成后仅显示本关提示；同关重建从该资格恢复，不写全局事件或永久资格。</summary>
    internal bool TryGrantHint()
    {
        if (_hintGranted || !IsHintInteractionReady) return false;
        _hintGranted = true;
        _runtime.ShowHint(true);
        this.DispatchEvent(Witness<GamePlayPanelSwitchTipsEvent>._, false);
        return true;
    }

    /// <summary>已获得提示后的金币跳关；须先关闭弹窗并恢复当前宿主，不绕过 TBC 或结果门禁。</summary>
    internal bool TryCompleteByHint() => _hintGranted && CanInteract() && _runtime.TryCompleteByHint();
    #endregion

    #region 插屏后免广告提示
    private void TryToShowNoAdPopup()
    {
        if (IsTbc) return;
        var module = ScheduleSystem.Instance.GetModule<DailyScheduleModule>()?.GetRefreshable<InterstitialNOAdPopupAutoShowModule>();
        if (module == null || !module.ShouldShowDigNoAdPopup() || !InterstitialAdHelper.HasDigInterstitial) return;
        NoAdsPopup.Create();
        module.RecordDigShow();
        InterstitialAdHelper.HasDigInterstitial = false;
    }
    #endregion

    #region 暂停与清理
    /// <inheritdoc />
    public void Pause()
    {
        if (!IsActive || _transitioning || _runtime == null || _runtime.IsResultLocked) return;
        _paused = true; _runtime.SetPaused(true); _runtime.SetInputEnabled(false); _hud?.SetDigInteraction(false);
    }
    /// <inheritdoc />
    public void Resume()
    {
        if (!IsActive || !_paused || _transitioning || _runtime == null || _runtime.IsResultLocked) return;
        _paused = false; _runtime.SetPaused(false); _runtime.SetInputEnabled(true); _hud?.SetDigInteraction(true);
    }
    /// <inheritdoc />
    public void ExitToLevelSelection()
    {
        if (!IsActive) return;
        bool restoreChoice = _runtime != null || _instance || _hud || _transitioning;
        if (!GameManager.Instance.ReturnToStartPanel(out string error)) { this.LogError($"[Dig] 返回首页失败：{error}"); return; }
        if (restoreChoice) GameManager.Instance.TryEnterDig(out error);
    }
    private void DestroyRuntime()
    {
        if (_runtime != null) { _runtime.Resolved -= OnResolved; _runtime.InkChanged -= OnInkChanged; _runtime.CountdownChanged -= OnCountdownChanged; _runtime.SetInputEnabled(false); _runtime.SetPaused(true); }
        _hud?.SetCountdownSeconds(0);
        _runtime = null;
        if (_instance) { _instance.SetActive(false); UnityEngine.Object.Destroy(_instance); }
        _instance = null;
    }
    private void DestroyHud() { if (_hud) UIManager.Instance.DestroyUI(_hud); _hud = null; }
    private void CloseVictory() { if (_victory) UIManager.Instance.DestroyUI(_victory); _victory = null; }
    #endregion
}
