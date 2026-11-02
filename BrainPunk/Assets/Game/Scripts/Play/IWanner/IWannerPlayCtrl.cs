using System;
using System.Collections.Generic;
using Bear.EventSystem;
using Bear.Logger;
using Bear.ResourceSystem;
using Bear.UI;
using Config;
using Config.Level;
using Config.Game;
using Cysharp.Threading.Tasks;
using Game;
using Game.ConfigModule;
using Game.Events;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// IWanner 玩法控制器。
/// 当前阶段负责首个 Group 进入、关卡实例/HUD 所有权、输入、暂停、Reset、组内成功推进与退出清理。
/// </summary>
public sealed class IWannerPlayCtrl : IPlayController, IDebuger, IEventSender, IInterstitialAdPolicyHost, IGMFeature
{
    #region Constants

    private const string IWannerLevelDirectory = "Assets/Game/Prefabs/WannerLevel/";
    private const string TbnLevelPath = "ILevel_999";
    private const string InGameMusicTag = "musicInGame";
    private const string OutGameMusicTag = "musicOutGame";
    private const float OutGameMusicFadeInSeconds = 8f;
    #endregion

    #region Runtime State

    private PlayControllerContext _context;
    private EventSubscriber _subscriber;
    private GameObject _levelInstance;
    private IWannerLevelCtrl _levelRuntime;
    private IWannerGame_GamePlayPanel _gamePlayPanel;
    private IWannarGame_GameGuidePopup _gameGuidePopup;
    private IWannerLevelSort _currentGroup;
    private IWannerLevelData _currentLevelData;
    private IWannerInterstitialAdPolicy _interstitialAdPolicy;
    private readonly IWannerLevelRuntimeStatistics _levelStatistics =
        new IWannerLevelRuntimeStatistics();
    private int _currentLevelIndex;
    private int _currentLevelFailureCount;
    private int _activationVersion;
    private bool _isInitialized;
    private bool _isActive;
    private bool _isPaused;
    private bool _isOpeningLevel;
    private bool _isAwaitingFailureReset;
    private bool _isSkipRewardAdRequesting;
    private bool _isGroupResolved;
    private bool _isTbnLevel;

#if UNITY_EDITOR || DEBUG_MODE
    private const float InterstitialDebugRefreshInterval = 0.25f;
    private float _nextInterstitialDebugRefreshTime;
    private InterstitialPlacement? _debugPendingInterstitialPlacement;
    private string _debugLastInterstitialRequestResult;
#endif

    #endregion

    #region IPlayController

    /// <summary>
    /// 用途：标识该控制器负责 IWanner 玩法。
    /// 调用边界：供 PlayControllerManager 注册和切换时读取。
    /// </summary>
    public PlayModeType Mode => PlayModeType.IWanner;

    /// <summary>
    /// 用途：向共享插屏辅助器提供当前 Group 在配置中的一基序号。
    /// 调用边界：仅在 IWanner 为活动玩法且发起插屏资格判断时读取。
    /// 注意事项：返回配置顺序而不是 GroupId；无法定位当前 Group 时返回 0 并阻止最小 Group 门槛通过。
    /// </summary>
    int IInterstitialAdPolicyHost.InterstitialLevel => ResolveCurrentGroupOrder();

    /// <summary>
    /// 用途：向共享插屏辅助器提供 IWanner 独立广告策略。
    /// 调用边界：策略由 Initialize 创建、Shutdown 释放，不与主线或 Draw 共用状态。
    /// </summary>
    IInterstitialAdPolicy IInterstitialAdPolicyHost.InterstitialAdPolicy =>
        _interstitialAdPolicy;

    /// <summary>
    /// 用途：标识共享依赖是否已经注入。
    /// 调用边界：由 PlayControllerManager 在首次激活前读取。
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// 用途：标识 IWanner 是否为当前活动玩法。
    /// 调用边界：入口、HUD 和异步回调用它拒绝旧会话操作。
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// 用途：注入 GameManager 统一创建的跨玩法共享依赖。
    /// 调用边界：仅由 PlayControllerManager 首次进入 IWanner 时调用一次。
    /// </summary>
    public void Initialize(PlayControllerContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _interstitialAdPolicy?.Dispose();
        _interstitialAdPolicy = new IWannerInterstitialAdPolicy();
        _isInitialized = true;
    }

    /// <summary>
    /// 用途：激活 IWanner 控制器并建立新的异步会话令牌。
    /// 调用边界：仅由 PlayControllerManager 调用；实际首个 Group 由入口随后显式请求。
    /// </summary>
    public void Activate()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("IWannerPlayCtrl must be initialized before activation.");

        if (_isActive)
            return;

        _isActive = true;
        _isPaused = false;
        _isOpeningLevel = false;
        _isGroupResolved = false;
        _activationVersion++;
        GameManager.Instance.CloseCamera();
        AddListener();
    }

    /// <summary>
    /// 用途：停止 IWanner 并清理它持有的关卡实例和 HUD。
    /// 调用边界：玩法切换时由 PlayControllerManager 调用，可重复执行。
    /// </summary>
    public void Deactivate()
    {
        OneLinkManager.Instance.EndEntry(GameType.IWanner);
        bool wasActive = _isActive;
        _levelStatistics.CompleteAttempt(isSuccess: false, isRetry: false);
        bool shouldCancelRewardAd = _isSkipRewardAdRequesting;
        _isSkipRewardAdRequesting = false;
        _isActive = false;
        _isPaused = false;
        _isOpeningLevel = false;
        _isGroupResolved = false;
        _activationVersion++;
        if (shouldCancelRewardAd)
            RewardAdHelper.CancelCurrent();

        EventsUtils.ResetEvents(ref _subscriber);
        CleanupRuntime();
        if (wasActive)
            PlayOutGameMusic();
    }

    /// <summary>
    /// 用途：驱动 IWanner 控制器的离散运行时检查，并处理当前活动玩法的系统返回键。
    /// 调用边界：仅由 GameManager.Update 经 PlayControllerManager 调用。
    /// 注意事项：角色逐帧逻辑由当前 IWannerLevelCtrl 自己驱动；暂停时仍允许打开退出应用确认弹窗。
    /// </summary>
    public void Tick()
    {
        if (!_isActive)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
            ShowExitPopup();

        if (!_isPaused &&
            !_isOpeningLevel &&
            !_isGroupResolved &&
            _levelRuntime != null)
        {
            _levelStatistics.Tick(Time.deltaTime);
        }

#if UNITY_EDITOR || DEBUG_MODE
        RefreshInterstitialDebugData();
#endif
    }

    /// <summary>
    /// 用途：释放控制器持有的全部运行时引用。
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
    /// 用途：显示 IWanner 模式的退出应用确认弹窗。
    /// 调用边界：仅由当前活动 IWannerPlayCtrl 在收到 Escape/Android Back 的离散输入时调用。
    /// 注意事项：加载、失败等待、激励广告请求、Group 结算和引导期间不会创建弹窗。
    /// </summary>
    private void ShowExitPopup()
    {
        if (!CanShowExitPopup())
            return;

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

    /// <summary>
    /// 用途：判断 IWanner 当前状态是否允许退出确认弹窗进入 Popup 栈。
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
               !_isAwaitingFailureReset &&
               !_isSkipRewardAdRequesting &&
               !_isGroupResolved;
    }

    #endregion

    #region Runtime Events

    /// <summary>
    /// 用途：只在 IWanner 活动期间接收玩法专属 Reset、共享 Victory Replay 与 Victory 下一步事件。
    /// 调用边界：由 Activate 建立、Deactivate 清理；不得订阅主线 GameResetEvent。
    /// </summary>
    private void AddListener()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<IWannerGameResetEvent>(OnGameResetEvent);
        _subscriber.Subscribe<GameReplayEvent>(OnGameReplayEvent);
        _subscriber.Subscribe<EnterNextLevelEvent>(OnEnterNextLevelEvent);
    }

    private void OnGameResetEvent(IWannerGameResetEvent evt)
    {
        TryResetCurrentLevel();
    }

    private void OnGameReplayEvent(GameReplayEvent evt)
    {
        TryReplayCurrentGroup();
    }

    private void OnEnterNextLevelEvent(EnterNextLevelEvent evt)
    {
        if (!_isActive || !_isGroupResolved)
            return;

        if (OneLinkManager.Instance.IsCurrentEntry(GameType.IWanner))
        {
            if (TryEnterFirstGroupAfterDeepLinkVictory(out string error))
            {
                OneLinkManager.Instance.EndEntry(GameType.IWanner);
                return;
            }

            OneLinkManager.Instance.EndEntry(GameType.IWanner);
            this.LogError(
                $"[IWannerPlayCtrl] Enter first Group after DeepLink victory failed. error:{error}");
        }

        ExitToLevelSelection();
    }

    #endregion

    #region Entry

    /// <summary>
    /// 用途：从配置排序第一行的 Group 进入 IWanner；若存档仍指向该 Group，则恢复已保存的小关索引。
    /// 调用边界：仅由 GameManager.TryEnterIWannerFirstGroup 在控制器激活后调用。
    /// 注意事项：存档指向其它 Group 或索引无效时从索引 0 开始，并记录本次有效入口。
    /// </summary>
    public bool TryEnterFirstGroup(out string error)
    {
        if (!_isActive)
        {
            error = "IWannerPlayCtrl is not active.";
            return false;
        }

        if (_isOpeningLevel || _levelRuntime != null || _levelInstance != null)
        {
            error = "An IWanner level is already opening or running.";
            return false;
        }

        IWannerGameLevelCtrl levelCtrl = GameManager.Instance?.IWannerGameLevel;
        if (levelCtrl == null || !levelCtrl.IsReady)
        {
            error = "IWannerGameLevelCtrl is not ready.";
            return false;
        }

        if (!levelCtrl.TryGetFirstGroup(out IWannerLevelSort group, out error))
            return false;

        int entryLevelIndex = ResolveEntryLevelIndex(group);
        if (!levelCtrl.TryGetLevelData(group.Id, entryLevelIndex, out IWannerLevelData levelData, out error))
            return false;

        OneLinkManager.Instance.OnLevelEntryStarted(GameType.IWanner);
        _currentGroup = group;
        _isTbnLevel = false;
        _currentLevelIndex = entryLevelIndex;
        _currentLevelFailureCount = 0;
        _currentLevelData = levelData;
        _isPaused = false;
        _isAwaitingFailureReset = false;
        _isGroupResolved = false;
        _isOpeningLevel = true;
        SaveCurrentLevelProgress(group.Id, entryLevelIndex);
        int activationVersion = ++_activationVersion;
        OpenCurrentLevelAsync(activationVersion, closeNormalLayerBeforeCreate: true).Forget();

        error = null;
        return true;
    }

    /// <summary>
    /// 用途：从指定 IWannerLevelSort.Id 对应 Group 进入 IWanner。
    /// 调用边界：仅由 GameManager.TryEnterIWannerGroup 在控制器激活后调用。
    /// 注意事项：未指定索引时保留同组续关；OneLink 小关目标传实际索引，未命中时传入传送门回退索引。
    /// </summary>
    /// <param name="groupId">已经解析出的目标 Group Id。</param>
    /// <param name="error">配置、解锁或运行状态的校验失败原因。</param>
    /// <param name="levelIndex">OneLink 的零起始组内索引；仅改变此次入口，后续推进、存档和打点沿用原流程。</param>
    public bool TryEnterGroup(int groupId, out string error, int? levelIndex = null)
    {
        if (!_isActive)
        {
            error = "IWannerPlayCtrl is not active.";
            return false;
        }

        if (_isOpeningLevel || _levelRuntime != null || _levelInstance != null)
        {
            error = "An IWanner level is already opening or running.";
            return false;
        }

        IWannerGameLevelCtrl levelCtrl = GameManager.Instance?.IWannerGameLevel;
        if (levelCtrl == null || !levelCtrl.IsReady)
        {
            error = "IWannerGameLevelCtrl is not ready.";
            return false;
        }

        if (!levelCtrl.TryGetGroup(groupId, out IWannerLevelSort group, out error))
            return false;

        if (!CanEnterGroup(levelCtrl, group, out error))
            return false;

        int entryLevelIndex = levelIndex ?? ResolveEntryLevelIndex(group);
        if (!levelCtrl.TryGetLevelData(group.Id, entryLevelIndex, out IWannerLevelData levelData, out error))
            return false;

        OneLinkManager.Instance.OnLevelEntryStarted(GameType.IWanner);
        _currentGroup = group;
        _isTbnLevel = false;
        _currentLevelIndex = entryLevelIndex;
        _currentLevelFailureCount = 0;
        _currentLevelData = levelData;
        _isPaused = false;
        _isAwaitingFailureReset = false;
        _isGroupResolved = false;
        _isOpeningLevel = true;
        SaveCurrentLevelProgress(group.Id, entryLevelIndex);
        int activationVersion = ++_activationVersion;
        OpenCurrentLevelAsync(activationVersion, closeNormalLayerBeforeCreate: true).Forget();

        error = null;
        return true;
    }

    /// <summary>
    /// 用途：进入 LevelSort 之外的 IWanner TBN 占位关 ILevel_999。
    /// 调用边界：仅由 GameManager.TryEnterIWannerTbn 在控制器激活后调用。
    /// 注意事项：普通入口由最后一组通关解锁；OneLink 允许本次直达，不改通关记录，也不发起广告。
    /// </summary>
    /// <param name="error">活动状态、解锁校验或运行时请求失败原因；成功时为空。</param>
    /// <param name="allowLockedEntry">仅由 GameManager 转交 OneLink 的本次直接进入许可。</param>
    /// <returns>控制器已接受 ILevel_999 加载请求时返回 true。</returns>
    public bool TryEnterTbn(out string error, bool allowLockedEntry = false)
    {
        if (!_isActive)
        {
            error = "IWannerPlayCtrl is not active.";
            return false;
        }

        if (_isOpeningLevel || _levelRuntime != null || _levelInstance != null)
        {
            error = "An IWanner level is already opening or running.";
            return false;
        }

        IWannerGameLevelCtrl levelCtrl = GameManager.Instance?.IWannerGameLevel;
        if (levelCtrl == null || !levelCtrl.IsReady)
        {
            error = "IWannerGameLevelCtrl is not ready.";
            return false;
        }

        if (!allowLockedEntry && !levelCtrl.IsTbnUnlocked(DB.IWannerGame?.PassedLevels))
        {
            error = "IWanner TBN is locked because the last Group has not been passed.";
            return false;
        }

        OneLinkManager.Instance.OnLevelEntryStarted(GameType.IWanner);
        _currentGroup = null;
        _currentLevelData = null;
        _currentLevelIndex = 0;
        _currentLevelFailureCount = 0;
        _isPaused = false;
        _isAwaitingFailureReset = false;
        _isGroupResolved = false;
        _isTbnLevel = true;
        _isOpeningLevel = true;
        int activationVersion = ++_activationVersion;
        OpenCurrentLevelAsync(activationVersion, closeNormalLayerBeforeCreate: true).Forget();

        error = null;
        return true;
    }

    /// <summary>
    /// 用途：解析进入 Group 时应加载的组内索引。
    /// 调用边界：仅在目标 Group 已由 IWannerGameLevelCtrl 校验后调用。
    /// 注意事项：只有存档 GroupId 与目标一致且索引仍在当前配置范围内才续关，否则回退索引 0。
    /// </summary>
    private int ResolveEntryLevelIndex(IWannerLevelSort group)
    {
        if (group == null)
            return 0;

        var saveData = DB.IWannerGame;
        if (saveData == null || saveData.CurrentGroupID != group.Id)
            return 0;

        int levelCount = group.Path?.Length ?? 0;
        if (saveData.CurrentLevel >= 0 && saveData.CurrentLevel < levelCount)
            return saveData.CurrentLevel;

        this.LogWarning(
            $"[IWannerPlayCtrl] Saved level index is invalid and will reset to zero. groupId:{group.Id}, savedIndex:{saveData.CurrentLevel}, levelCount:{levelCount}");
        return 0;
    }

    /// <summary>
    /// 校验 Group 入口前补齐旧档漏记的通关后继解锁，避免 UI 未打开或最高进度跳组时丢失资格。
    /// 仅新增解锁时保存；保存失败保留内存结果并告警，原配置、通关和当前进度门禁继续生效。
    /// </summary>
    private static bool CanEnterGroup(
        IWannerGameLevelCtrl levelCtrl,
        IWannerLevelSort group,
        out string error)
    {
        var saveData = DB.IWannerGame;
        if (saveData == null)
        {
            error = "IWanner save data is unavailable.";
            return false;
        }

        if (levelCtrl.RestorePassedGroupUnlocks(saveData) && !DB.SaveIWannerGame())
        {
            levelCtrl.LogWarning("[IWannerGameLevelCtrl] Failed to save restored Group unlocks.");
        }

        int currentGroupId = levelCtrl.ResolveCurrentGroupId(
            saveData.MaxGroupId,
            saveData.PassedLevels);
        bool canEnter = saveData.PassedLevels?.Contains(group.Id) == true ||
                        currentGroupId == group.Id ||
                        group.LockType == Config.Level.LevelLockType.Unlock ||
                        saveData.UnlockGroups?.Contains(group.Id) == true;
        if (canEnter)
        {
            error = null;
            return true;
        }

        error = $"IWanner Group is locked. groupId:{group.Id}, currentGroupId:{currentGroupId}.";
        return false;
    }

    /// <summary>
    /// 用途：保存当前 Group 与组内索引，供退出或重启后恢复。
    /// 调用边界：进入 Group、成功/Skip 推进以及 Replay 重置进度时调用；普通失败 Reset 不调用。
    /// 注意事项：仅在值变化时落盘；保存失败只记录告警，不中断当前关卡流程。
    /// </summary>
    private void SaveCurrentLevelProgress(int groupId, int levelIndex)
    {
        var saveData = DB.IWannerGame;
        if (saveData == null)
        {
            this.LogWarning(
                $"[IWannerPlayCtrl] Cannot save current IWanner progress. groupId:{groupId}, levelIndex:{levelIndex}");
            return;
        }

        int normalizedLevelIndex = Mathf.Max(0, levelIndex);
        if (saveData.CurrentGroupID == groupId && saveData.CurrentLevel == normalizedLevelIndex)
            return;

        saveData.CurrentGroupID = groupId;
        saveData.CurrentLevel = normalizedLevelIndex;
        if (!DB.SaveIWannerGame())
        {
            this.LogWarning(
                $"[IWannerPlayCtrl] Failed to save current IWanner progress. groupId:{groupId}, levelIndex:{normalizedLevelIndex}");
        }
    }

    private async UniTask OpenCurrentLevelAsync(
        int activationVersion,
        bool closeNormalLayerBeforeCreate,
        InterstitialPlacement? pendingInterstitialPlacement = null,
        bool recordAttempt = true)
    {
        try
        {
            if (closeNormalLayerBeforeCreate)
            {
                UIManager.Instance.CloseAllUI(UILayer.Normal);
                await UniTask.NextFrame();
                if (!IsCurrentActivation(activationVersion))
                    return;
            }

            DestroyGameplayPanel();
            _gamePlayPanel = IWannerGame_GamePlayPanel.Create(
                _currentLevelIndex,
                _currentGroup?.Path?.Length ?? 0,
                TrySkipCurrentLevel);
            if (_gamePlayPanel == null)
            {
                HandleOpenFailure(activationVersion, "Failed to create IWanner GamePlayPanel.");
                return;
            }

            this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, true);
            _gamePlayPanel.SetInputEnabled(false);
            // 仅在新 GameplayPanel 创建阶段按当前小关累计失败次数决定显隐，不热更新旧面板。
            _gamePlayPanel.SetSkipVisible(ShouldShowSkip());

            string currentLevelPath = _isTbnLevel ? TbnLevelPath : _currentLevelData?.Path;
            string resourcePath = ResolveLevelResourcePath(currentLevelPath);
            if (string.IsNullOrEmpty(resourcePath))
            {
                HandleOpenFailure(activationVersion, "Current IWanner level resource path is empty.");
                return;
            }

            GameObject prefab = await ResourceManager.Instance.LoadAsync<GameObject>(resourcePath);
            if (!IsCurrentActivation(activationVersion))
                return;

            if (prefab == null)
            {
                HandleOpenFailure(
                    activationVersion,
                    $"IWanner level prefab was not found: {resourcePath}");
                return;
            }

            GameObject sceneRootObject = GameObject.Find("Scene");
            if (sceneRootObject == null)
            {
                HandleOpenFailure(activationVersion, "Main scene root 'Scene' was not found.");
                return;
            }

            _levelInstance = UnityEngine.Object.Instantiate(prefab, sceneRootObject.transform);
            InstallLevelCameraSizeFit();
            _levelRuntime = _levelInstance.GetComponentInChildren<IWannerLevelCtrl>(true);
            if (_levelRuntime == null)
            {
                HandleOpenFailure(
                    activationVersion,
                    $"IWanner level does not contain a ready {nameof(IWannerLevelCtrl)}: {resourcePath}");
                return;
            }

            _levelRuntime.Resolved -= OnLevelResolved;
            _levelRuntime.Resolved += OnLevelResolved;
            if (!_levelRuntime.InitializeRuntime())
            {
                HandleOpenFailure(
                    activationVersion,
                    $"IWanner level does not contain a ready {nameof(IWannerLevelCtrl)}: {resourcePath}");
                return;
            }

            if (_levelRuntime.TryGetGameplayPanelColors(
                    out Color gameplayPanelColor,
                    out Color skipTextColor,
                    out Color groundColor))
            {
                _gamePlayPanel.ApplyGameplayColors(
                    gameplayPanelColor,
                    skipTextColor,
                    groundColor);
            }

            _levelRuntime.SetInputEnabled(false);
            _levelRuntime.SetPaused(_isPaused || pendingInterstitialPlacement.HasValue);

            if (pendingInterstitialPlacement.HasValue)
            {
                await WaitForInterstitialAsync(pendingInterstitialPlacement.Value);
                if (!IsCurrentActivation(activationVersion) || _levelRuntime == null)
                    return;

                _levelRuntime.SetPaused(_isPaused);
            }

            this.DispatchEvent(Witness<GamePlayPanelFadeInEvent>._);
            float fadeFromBlackDuration = _gamePlayPanel?.FadeFromBlackDuration ?? 0f;
            if (fadeFromBlackDuration > 0f)
            {
                await UniTask.WaitForSeconds(
                    fadeFromBlackDuration,
                    ignoreTimeScale: true);
                if (!IsCurrentActivation(activationVersion))
                    return;
            }

            if (recordAttempt)
            {
                string currentLevelDataId = _isTbnLevel
                    ? null
                    : _currentLevelData?.Id;
                _levelStatistics.BeginAttempt(currentLevelDataId, currentLevelPath);
            }
            else
                _levelStatistics.DiscardAttempt();

            OneLinkManager.Instance.ConfirmLevelEntered(GameType.IWanner);
            _levelRuntime.SetInputEnabled(!_isPaused);
            _levelRuntime.SetPaused(_isPaused);
            _gamePlayPanel?.SetInputEnabled(!_isPaused);
            PlayInGameMusic();
            _isOpeningLevel = false;
            this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, false);
            if (!TryShowGameGuidePopup())
                TryToShowNoAdPopup();
        }
        catch (Exception exception)
        {
            HandleOpenFailure(activationVersion, exception.ToString());
        }
    }

    /// <summary>
    /// 首次实际进关完成后展示 IWanner 操作引导，关闭前使用当前玩法的暂停与输入门禁。
    /// 普通 Group、指定小关与 TBN 共用此入口；过期回调不得恢复新关卡，免广告弹窗延后展示。
    /// </summary>
    private bool TryShowGameGuidePopup()
    {
        if (!Application.isPlaying ||
            DB.GameData.HasIWannerGameGuidePopup ||
            DB.IWannerGame?.PassedLevels?.Count > 0)
            return false;

        int activationVersion = _activationVersion;
        IWannerLevelCtrl levelRuntime = _levelRuntime;
        _gameGuidePopup = IWannarGame_GameGuidePopup.Create(() =>
        {
            _gameGuidePopup = null;
            if (!IsCurrentActivation(activationVersion) ||
                !ReferenceEquals(_levelRuntime, levelRuntime) ||
                _isOpeningLevel ||
                _isGroupResolved)
            {
                return;
            }

            Resume();
            TryToShowNoAdPopup();
        });
        if (_gameGuidePopup == null)
            return false;

        Pause();
        return true;
    }

    /// <summary>
    /// 用途：为当前 IWanner 小关唯一的正交 Camera 安装 1920×1080 宽度完整显示适配。
    /// 调用边界：只在小关 prefab 完成运行时实例化后调用；保留各关自行配置的原始 Camera Size。
    /// 注意事项：缺少 Camera 或使用透视 Camera 时只记录警告，不阻断既有关卡加载流程。
    /// </summary>
    private void InstallLevelCameraSizeFit()
    {
        if (!Application.isPlaying || _levelInstance == null)
            return;

        Camera levelCamera = _levelInstance.GetComponentInChildren<Camera>(true);
        if (levelCamera == null)
        {
            this.LogWarning("[IWannerPlayCtrl] Camera Size fit skipped. Level Camera is missing.");
            return;
        }

        if (!levelCamera.orthographic)
        {
            this.LogWarning(
                $"[IWannerPlayCtrl] Camera Size fit skipped. Camera is not orthographic. camera:{levelCamera.name}");
            return;
        }

        CameraOrthographicSizeFit sizeFit =
            levelCamera.GetComponent<CameraOrthographicSizeFit>();
        if (sizeFit == null)
            sizeFit = levelCamera.gameObject.AddComponent<CameraOrthographicSizeFit>();

        sizeFit.ApplyFit();
    }

    private void HandleOpenFailure(int activationVersion, string error)
    {
        if (!IsCurrentActivation(activationVersion))
            return;

        OneLinkManager.Instance.EndEntry(GameType.IWanner);
        this.LogError($"[IWannerPlayCtrl] Open level failed. error:{error}");
        _isOpeningLevel = false;
        CleanupRuntime();

        if (!GameManager.Instance.ReturnToStartPanel(out string returnError))
        {
            this.LogError(
                $"[IWannerPlayCtrl] Return to StartPanel after open failure failed. error:{returnError}");
        }
    }

    #endregion

    #region HUD Commands

    /// <summary>
    /// 用途：重建当前 Group 当前索引对应的小关。
    /// 调用边界：供 IWanner HUD Reset 按钮与死亡后的 ResetClickArea 调用；加载期间和无活动关卡时拒绝重复请求。
    /// 注意事项：手动 Reset 计入一次失败；死亡结果已计数，点击失败重置区不会重复累计。
    /// </summary>
    public bool TryResetCurrentLevel()
    {
        if (!_isActive ||
            _isOpeningLevel ||
            _isSkipRewardAdRequesting ||
            _levelRuntime == null ||
            (!_isTbnLevel && _currentLevelData == null))
        {
            return false;
        }

        if (!_isAwaitingFailureReset)
        {
            _currentLevelFailureCount++;
            _levelStatistics.CompleteAttempt(isSuccess: false, isRetry: true);
            _interstitialAdPolicy?.OnPlayerFailedOrRetried();
        }

        _isAwaitingFailureReset = false;
        _isOpeningLevel = true;
        _isPaused = false;
        _gamePlayPanel?.SetResetClickAreaVisible(false);
        _gamePlayPanel?.SetInputEnabled(false);
        _levelRuntime.SetInputEnabled(false);
        this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, true);

        int activationVersion = ++_activationVersion;
        ReloadCurrentLevelAsync(
            activationVersion,
            pendingInterstitialPlacement: InterstitialPlacement.InGameFailed).Forget();
        return true;
    }

    /// <summary>
    /// 用途：从当前 Group 的第一个小关重新开始整组 Replay。
    /// 调用边界：供 IWanner VictoryPanel 的 Replay 操作调用；HUD Reset 不得调用本方法。
    /// 注意事项：保持当前 Group 不变，把运行时和存档中的 currentLevelIndex 一并重置为 0；当前阶段不包含 Replay 打点。
    /// </summary>
    public bool TryReplayCurrentGroup()
    {
        if (!_isActive ||
            !_isGroupResolved ||
            _isOpeningLevel ||
            _isSkipRewardAdRequesting ||
            _currentGroup == null)
        {
            return false;
        }

        IWannerGameLevelCtrl levelCtrl = GameManager.Instance?.IWannerGameLevel;
        IWannerLevelData firstLevelData = null;
        string error = levelCtrl == null ? "IWannerGameLevelCtrl is unavailable." : null;
        if (levelCtrl == null ||
            !levelCtrl.TryGetLevelData(
                _currentGroup.Id,
                0,
                out firstLevelData,
                out error))
        {
            this.LogError(
                $"[IWannerPlayCtrl] Replay current Group failed. groupId:{_currentGroup.Id}, error:{error}");
            return false;
        }

        _isOpeningLevel = true;
        _isPaused = false;
        _currentLevelIndex = 0;
        _currentLevelFailureCount = 0;
        _isAwaitingFailureReset = false;
        _isGroupResolved = false;
        _currentLevelData = firstLevelData;
        SaveCurrentLevelProgress(_currentGroup.Id, _currentLevelIndex);
        _gamePlayPanel?.SetResetClickAreaVisible(false);
        _gamePlayPanel?.SetInputEnabled(false);
        _levelRuntime?.SetInputEnabled(false);
        this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, true);

        int activationVersion = ++_activationVersion;
        ReloadCurrentLevelAsync(activationVersion).Forget();
        return true;
    }

    /// <summary>
    /// 用途：OneLink 胜利后固定进入配置第一行 Group 的第一个小关。
    /// 调用边界：仅由 DeepLink 专用胜利面板的 Next 事件在完整 Group 已结算后调用。
    /// 注意事项：不恢复组内进度、不筛除已通关 Group，复用现有 IWanner 黑幕重建链。
    /// </summary>
    private bool TryEnterFirstGroupAfterDeepLinkVictory(out string error)
    {
        if (!_isActive || !_isGroupResolved || _isOpeningLevel || _isSkipRewardAdRequesting)
        {
            error = "IWanner is not ready to enter another level after victory.";
            return false;
        }

        IWannerGameLevelCtrl levelCtrl = GameManager.Instance?.IWannerGameLevel;
        var saveData = DB.IWannerGame;
        if (levelCtrl == null || !levelCtrl.IsReady || saveData == null)
        {
            error = "IWanner level configuration or save data is unavailable.";
            return false;
        }

        if (!levelCtrl.TryGetFirstGroup(out IWannerLevelSort targetGroup, out error) ||
            !levelCtrl.TryGetLevelData(targetGroup.Id, 0, out IWannerLevelData targetLevelData, out error))
            return false;

        if (string.IsNullOrWhiteSpace(targetLevelData.Path))
        {
            error = "The first IWanner level resource path is empty.";
            return false;
        }

        _isOpeningLevel = true;
        _isPaused = false;
        _currentGroup = targetGroup;
        _isTbnLevel = false;
        _currentLevelIndex = 0;
        _currentLevelFailureCount = 0;
        _isAwaitingFailureReset = false;
        _isGroupResolved = false;
        _currentLevelData = targetLevelData;
        SaveCurrentLevelProgress(targetGroup.Id, _currentLevelIndex);
        _gamePlayPanel?.SetResetClickAreaVisible(false);
        _gamePlayPanel?.SetInputEnabled(false);
        _levelRuntime?.SetInputEnabled(false);
        this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, true);

        int activationVersion = ++_activationVersion;
        ReloadCurrentLevelAsync(activationVersion).Forget();
        error = null;
        return true;
    }

    private async UniTask ReloadCurrentLevelAsync(
        int activationVersion,
        InterstitialPlacement? pendingInterstitialPlacement = null,
        bool recordAttempt = true)
    {
        try
        {
            if (_gamePlayPanel != null)
                await _gamePlayPanel.FadeToBlackAsync();

            if (!IsCurrentActivation(activationVersion))
                return;

            CleanupCurrentLevelRuntime();
            await OpenCurrentLevelAsync(
                activationVersion,
                closeNormalLayerBeforeCreate: false,
                pendingInterstitialPlacement: pendingInterstitialPlacement,
                recordAttempt: recordAttempt);
        }
        catch (Exception exception)
        {
            HandleOpenFailure(activationVersion, exception.ToString());
        }
    }

    /// <summary>
    /// 用途：等待一次 IWanner 插屏资格判断和 SDK 结果回调完成。
    /// 调用边界：只在关卡输入已锁定且运行时处于稳定暂停状态后调用。
    /// 注意事项：策略拒绝、SDK 未就绪或调用异常都按未展示降级，不得阻断 Reset 或 Victory 后续流程。
    /// </summary>
    private async UniTask WaitForInterstitialAsync(InterstitialPlacement placement)
    {
        BeginInterstitialDebugRequest(placement);

        try
        {
            var completionSource = new UniTaskCompletionSource<bool>();
            try
            {
                InterstitialAdHelper.TryToShowInterstitial(
                    placement.ToString(),
                    (_, success) => completionSource.TrySetResult(success));
            }
            catch (Exception exception)
            {
                RecordInterstitialDebugException(placement, exception);
                this.LogWarning(
                    $"[IWannerPlayCtrl] Interstitial request failed open. placement:{placement}, error:{exception}");
                return;
            }

            bool success = await completionSource.Task;
            RecordInterstitialDebugResult(placement, success);
        }
        finally
        {
            EndInterstitialDebugRequest();
        }
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEBUG_MODE")]
    private void BeginInterstitialDebugRequest(InterstitialPlacement placement)
    {
#if UNITY_EDITOR || DEBUG_MODE
        _debugPendingInterstitialPlacement = placement;
        _debugLastInterstitialRequestResult = $"{placement}: 请求中";
        RefreshInterstitialDebugData(force: true);
#endif
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEBUG_MODE")]
    private void RecordInterstitialDebugResult(
        InterstitialPlacement placement,
        bool success)
    {
#if UNITY_EDITOR || DEBUG_MODE
        _debugLastInterstitialRequestResult =
            $"{placement}: Helper callback success={success}";
#endif
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEBUG_MODE")]
    private void RecordInterstitialDebugException(
        InterstitialPlacement placement,
        Exception exception)
    {
#if UNITY_EDITOR || DEBUG_MODE
        _debugLastInterstitialRequestResult =
            $"{placement}: 请求异常 {exception.GetType().Name}";
#endif
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEBUG_MODE")]
    private void EndInterstitialDebugRequest()
    {
#if UNITY_EDITOR || DEBUG_MODE
        _debugPendingInterstitialPlacement = null;
        RefreshInterstitialDebugData(force: true);
#endif
    }

    /// <summary>
    /// 用途：在 IWanner 重新进入可操作状态后消费专属成功插屏标记并尝试展示免广告弹窗。
    /// 调用边界：仅由有效 GameplayPanel 完成渐隐、输入恢复后调用；Group 结算期间不直接弹出。
    /// 注意事项：创建失败时保留标记，允许下一次 IWanner Gameplay 状态重试。
    /// </summary>
    private void TryToShowNoAdPopup()
    {
        if (!_isActive || _isGroupResolved || !InterstitialAdHelper.HasIWannerInterstitial)
            return;

        var noAdModule = ScheduleSystem.Instance
            .GetModule<DailyScheduleModule>()
            ?.GetRefreshable<InterstitialNOAdPopupAutoShowModule>();
        if (noAdModule == null || !noAdModule.ShouldShowIWannerNoAdPopup())
            return;

        try
        {
            NoAdsPopup popup = NoAdsPopup.Create(nameof(IWannerGame_GamePlayPanel));
            if (popup == null)
                return;

            noAdModule.RecordIWannerShow();
            InterstitialAdHelper.HasIWannerInterstitial = false;
        }
        catch (Exception exception)
        {
            this.LogWarning(
                $"[IWannerPlayCtrl] Failed to create NoAdsPopup after interstitial. error:{exception}");
        }
    }

    /// <summary>
    /// 用途：打开通用游戏内设置弹窗并注入当前 IWanner 暂停能力。
    /// 调用边界：供需要从控制器侧打开暂停设置的运行时入口调用；系统返回键使用退出应用确认弹窗。
    /// </summary>
    public void OpenPausePopup()
    {
        if (!_isActive ||
            _isOpeningLevel ||
            _isAwaitingFailureReset ||
            _isSkipRewardAdRequesting ||
            _isGroupResolved ||
            _gamePlayPanel == null)
        {
            return;
        }

        Pause();
        GameSettingPopup.Create(true, this);
    }

    #endregion

    #region GM Commands

    /// <summary>
    /// IWanner 当前不提供跳过当前小关的 GM 下一关语义；请使用 TryForceVictory() 通过当前小关。
    /// </summary>
    public bool TryEnterNextLevel()
    {
        return false;
    }

    /// <summary>
    /// 用途：让 GM 调试入口回到当前 Group 的上一小关。
    /// 调用边界：仅供活动 IWanner 玩法的 GMPanel 调用；第一小关、加载中或无有效运行时时拒绝。
    /// 注意事项：只切换运行时小关，不写入通关、失败、Replay、存档或分析数据。
    /// </summary>
    /// <returns>已接受上一小关切换请求时返回 true，否则返回 false。</returns>
    public bool TryEnterPreviousLevel()
    {
        if (!_isActive ||
            _isOpeningLevel ||
            _levelRuntime == null ||
            _currentGroup == null ||
            _currentLevelData == null ||
            _isSkipRewardAdRequesting ||
            _currentLevelIndex <= 0)
        {
            return false;
        }

        int previousLevelIndex = _currentLevelIndex - 1;
        IWannerGameLevelCtrl levelCtrl = GameManager.Instance?.IWannerGameLevel;
        string error = levelCtrl == null ? "IWannerGameLevelCtrl is unavailable." : null;
        if (levelCtrl == null ||
            !levelCtrl.TryGetLevelData(
                _currentGroup.Id,
                previousLevelIndex,
                out IWannerLevelData previousLevelData,
                out error))
        {
            this.LogError(
                $"[IWannerPlayCtrl] Enter previous level failed. groupId:{_currentGroup.Id}, index:{previousLevelIndex}, error:{error}");
            return false;
        }

        _isOpeningLevel = true;
        _isPaused = false;
        _levelStatistics.DiscardAttempt();
        _currentLevelIndex = previousLevelIndex;
        _currentLevelFailureCount = 0;
        _isAwaitingFailureReset = false;
        _currentLevelData = previousLevelData;
        _gamePlayPanel?.SetResetClickAreaVisible(false);
        _gamePlayPanel?.SetInputEnabled(false);
        _levelRuntime.SetInputEnabled(false);
        this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, true);

        int activationVersion = ++_activationVersion;
        ReloadCurrentLevelAsync(activationVersion, recordAttempt: false).Forget();
        return true;
    }

    /// <summary>
    /// 用途：让 GM 调试入口按真实成功条件提交当前 IWanner 小关。
    /// 调用边界：仅供活动 IWanner 玩法的 GMPanel 调用；暂停、加载、死亡或已结算状态由现有成功门禁拒绝。
    /// 注意事项：复用 IWannerLevelCtrl.TrySuccess()，不得绕过进门表现和组内推进链。
    /// </summary>
    /// <param name="source">调试命令来源；当前 IWanner 成功链暂不消费该值。</param>
    /// <returns>当前小关成功请求被接受时返回 true，否则返回 false。</returns>
    public bool TryForceVictory(string source)
    {
        if (!_isActive || _isOpeningLevel || _isSkipRewardAdRequesting || _levelRuntime == null)
            return false;

        return _levelRuntime.TrySuccess();
    }

    #endregion

    #region Level Result

    /// <summary>
    /// 用途：在当前小关累计失败次数达到远端阈值后，请求观看激励广告并在奖励成功后推进到下一小关。
    /// 调用边界：仅由当前 IWanner GameplayPanel 注入的局部 Skip 回调调用；加载、暂停、广告请求中或未达阈值时拒绝。
    /// 注意事项：广告覆盖期间会暂停当前关卡并锁定输入；失败或取消恢复当前关卡，只有成功回调允许跳关。
    /// </summary>
    /// <returns>Skip 请求被接受时返回 true，否则返回 false。</returns>
    public bool TrySkipCurrentLevel()
    {
        if (!_isActive ||
            _isOpeningLevel ||
            _isPaused ||
            _levelRuntime == null ||
            _gamePlayPanel == null ||
            _currentGroup == null ||
            _currentLevelData == null ||
            _isAwaitingFailureReset ||
            _isSkipRewardAdRequesting ||
            !ShouldShowSkip())
        {
            return false;
        }

        int requestActivationVersion = _activationVersion;
        IWannerLevelCtrl sourceLevel = _levelRuntime;
        IWannerGame_GamePlayPanel sourcePanel = _gamePlayPanel;

        _isSkipRewardAdRequesting = true;
        _isPaused = true;
        sourcePanel.SetInputEnabled(false);
        sourceLevel.SetInputEnabled(false);
        sourceLevel.SetPaused(true);
        this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, true);

        try
        {
            RewardAdHelper.TryToShowRewardAd(
                RewardPlacement.InGamePassLevel.ToString(),
                success: "U_HurryTips_RewardVideo_succeed",
                failed: "U_HurryTips_RewardVideo_failed",
                onResult: (placement, isSuccess) => OnSkipRewardAdResult(
                    requestActivationVersion,
                    sourceLevel,
                    sourcePanel,
                    placement,
                    isSuccess));
        }
        catch (Exception exception)
        {
            RestoreAfterSkipRewardAdFailure(
                requestActivationVersion,
                sourceLevel,
                sourcePanel,
                $"Reward ad request threw an exception. error:{exception}");
        }

        return true;
    }

    private void OnSkipRewardAdResult(
        int requestActivationVersion,
        IWannerLevelCtrl sourceLevel,
        IWannerGame_GamePlayPanel sourcePanel,
        string placement,
        bool isSuccess)
    {
        if (!_isSkipRewardAdRequesting ||
            !IsCurrentActivation(requestActivationVersion) ||
            sourceLevel != _levelRuntime ||
            sourcePanel != _gamePlayPanel)
        {
            return;
        }

        _isSkipRewardAdRequesting = false;
        if (!isSuccess)
        {
            RestoreAfterSkipRewardAdFailure(
                requestActivationVersion,
                sourceLevel,
                sourcePanel,
                $"Reward ad did not grant a reward. placement:{placement}");
            return;
        }

        _levelStatistics.CompleteAttempt(isSuccess: true, isRetry: false);
        _isPaused = false;
        _isOpeningLevel = true;
        sourceLevel.SetPaused(false);
        sourceLevel.SetInputEnabled(false);
        sourcePanel.SetInputEnabled(false);
        this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, true);

        int activationVersion = ++_activationVersion;
        AdvanceToNextLevelAsync(activationVersion).Forget();
    }

    private void RestoreAfterSkipRewardAdFailure(
        int requestActivationVersion,
        IWannerLevelCtrl sourceLevel,
        IWannerGame_GamePlayPanel sourcePanel,
        string reason)
    {
        if (!IsCurrentActivation(requestActivationVersion) ||
            sourceLevel != _levelRuntime ||
            sourcePanel != _gamePlayPanel)
        {
            return;
        }

        _isSkipRewardAdRequesting = false;
        _isPaused = false;
        sourceLevel.SetInputEnabled(true);
        sourceLevel.SetPaused(false);
        sourcePanel.SetInputEnabled(true);
        this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, false);
        this.LogWarning($"[IWannerPlayCtrl] Skip was not granted. {reason}");
    }

    private void OnLevelResolved(IWannerLevelResult result)
    {
        if (!_isActive || _isOpeningLevel || _levelRuntime == null)
            return;

        IWannerLevelCtrl sourceLevel = _levelRuntime;
        try
        {
            this.Log(
                $"[IWannerPlayCtrl] IWanner level resolved. type:{result.Type}, source:{result.Source}");

            switch (result.Type)
            {
                case IWannerLevelResultType.Success:
                    _levelStatistics.CompleteAttempt(isSuccess: true, isRetry: false);
                    BeginLevelResultTransition(sourceLevel);
                    int activationVersion = ++_activationVersion;
                    ResolveCurrentLevelSuccessAsync(sourceLevel, activationVersion).Forget();
                    break;
                case IWannerLevelResultType.Failed:
                    ShowFailureResetArea(sourceLevel);
                    break;
                default:
                    BeginLevelResultTransition(sourceLevel);
                    HandleOpenFailure(
                        ++_activationVersion,
                        $"Unsupported IWanner level result type: {result.Type}");
                    break;
            }
        }
        catch (Exception exception)
        {
            HandleSynchronousLevelResultException(sourceLevel, result, exception);
        }
    }

    private void HandleSynchronousLevelResultException(
        IWannerLevelCtrl sourceLevel,
        IWannerLevelResult result,
        Exception exception)
    {
        string error =
            $"IWanner synchronous {result.Type} result handling failed. source:{result.Source}, error:{exception}";
        if (!_isActive || sourceLevel != _levelRuntime)
        {
            this.LogError($"[IWannerPlayCtrl] {error}");
            return;
        }

        int recoveryVersion = ++_activationVersion;
        HandleOpenFailure(recoveryVersion, error);
    }

    private void BeginLevelResultTransition(IWannerLevelCtrl sourceLevel)
    {
        _isAwaitingFailureReset = false;
        _isOpeningLevel = true;
        _isPaused = false;
        _gamePlayPanel?.SetResetClickAreaVisible(false);
        _gamePlayPanel?.SetInputEnabled(false);
        sourceLevel.SetInputEnabled(false);
        this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, true);
    }

    private void ShowFailureResetArea(IWannerLevelCtrl sourceLevel)
    {
        _currentLevelFailureCount++;
        _levelStatistics.CompleteAttempt(isSuccess: false, isRetry: true);
        _interstitialAdPolicy?.OnPlayerFailedOrRetried();
        _isAwaitingFailureReset = true;
        _isOpeningLevel = false;
        _isPaused = false;
        _gamePlayPanel?.SetInputEnabled(false);
        _gamePlayPanel?.SetResetClickAreaVisible(true);
        sourceLevel.SetInputEnabled(false);
        this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, false);
    }

    private async UniTask ResolveCurrentLevelSuccessAsync(
        IWannerLevelCtrl sourceLevel,
        int activationVersion)
    {
        try
        {
            bool doorAnimationCompleted = await sourceLevel.PlayEnterDoorAnimationAsync(
                sourceLevel.GetCancellationTokenOnDestroy());
            if (!IsCurrentActivation(activationVersion) || sourceLevel != _levelRuntime)
                return;

            if (!doorAnimationCompleted)
            {
                this.LogWarning(
                    "[IWannerPlayCtrl] Enter door animation did not complete; continuing success transition to avoid blocking the level.");
            }

            await AdvanceToNextLevelAsync(activationVersion);
        }
        catch (Exception exception)
        {
            HandleLevelResultException(
                sourceLevel,
                activationVersion,
                IWannerLevelResultType.Success,
                exception);
        }
    }

    private void HandleLevelResultException(
        IWannerLevelCtrl sourceLevel,
        int activationVersion,
        IWannerLevelResultType resultType,
        Exception exception)
    {
        if (!IsCurrentActivation(activationVersion) || sourceLevel != _levelRuntime)
            return;

        HandleOpenFailure(
            activationVersion,
            $"IWanner {resultType} flow failed. error:{exception}");
    }

    private async UniTask AdvanceToNextLevelAsync(int activationVersion)
    {
        if (_isTbnLevel)
        {
            _isOpeningLevel = false;
            ExitToLevelSelection();
            return;
        }

        int nextLevelIndex = _currentLevelIndex + 1;
        int levelCount = _currentGroup?.Path?.Length ?? 0;
        if (nextLevelIndex >= levelCount)
        {
            _currentLevelFailureCount = 0;
            await HandleGroupCompletionAsync(activationVersion);
            return;
        }

        IWannerGameLevelCtrl levelCtrl = GameManager.Instance?.IWannerGameLevel;
        string error = levelCtrl == null ? "IWannerGameLevelCtrl is unavailable." : null;
        if (levelCtrl == null ||
            !levelCtrl.TryGetLevelData(
                _currentGroup.Id,
                nextLevelIndex,
                out IWannerLevelData nextLevelData,
                out error))
        {
            HandleOpenFailure(
                activationVersion,
                $"Resolve next IWanner level failed. groupId:{_currentGroup?.Id}, index:{nextLevelIndex}, error:{error}");
            return;
        }

        _currentLevelIndex = nextLevelIndex;
        _currentLevelFailureCount = 0;
        _isAwaitingFailureReset = false;
        _currentLevelData = nextLevelData;
        SaveCurrentLevelProgress(_currentGroup.Id, _currentLevelIndex);
        await ReloadCurrentLevelAsync(activationVersion);
    }

    private bool ShouldShowSkip()
    {
        int failureThreshold = ConfigManager.RemoteConfig.GetIWannerGameShowSkipFailCount();
        return !_isTbnLevel &&
               failureThreshold > 0 &&
               _currentLevelFailureCount >= failureThreshold;
    }

#if UNITY_EDITOR || DEBUG_MODE
    private void RefreshInterstitialDebugData(bool force = false)
    {
        if (_gamePlayPanel == null || !_gamePlayPanel.WantsInterstitialDebugData)
        {
            _nextInterstitialDebugRefreshTime = 0f;
            return;
        }

        float nowRealtime = Time.realtimeSinceStartup;
        if (!force && nowRealtime < _nextInterstitialDebugRefreshTime)
            return;

        _nextInterstitialDebugRefreshTime = nowRealtime + InterstitialDebugRefreshInterval;
        _gamePlayPanel.SetInterstitialDebugData(BuildInterstitialDebugData());
    }

    private IWannerGame_GamePlayPanel.InterstitialDebugData BuildInterstitialDebugData()
    {
        var saveData = DB.IWannerGame;
        int groupOrder = ResolveCurrentGroupOrder();
        int minGroup = ConfigManager.RemoteConfig.GetIWannerGameInterstitialShowMinGroup();
        int victoryInterval = ConfigManager.RemoteConfig.GetIWannerGameInterstitialShowIntervalCount(
            groupOrder);
        int passedLevelThreshold =
            ConfigManager.RemoteConfig.GetIWannerGameInterstitialShowPassedLevelCount();
        int failureThreshold =
            ConfigManager.RemoteConfig.GetIWannerGameInterstitialShowFailedCount(groupOrder);
        int interstitialCooldown = ConfigManager.RemoteConfig.GetIWannerGameInterstitialCD();
        int rewardCooldown = ConfigManager.RemoteConfig.GetIWannerGameRewardResetInterstitialCD();

        int victoryCount = saveData?.InterstitialLevelSuccessCount ?? 0;
        int passedLevelCount = saveData?.InterstitialPassedLevelCount ?? 0;
        int failureCount = saveData?.InterstitialFailCount ?? 0;
        long lastInterstitialDate = saveData?.LastInterstitialDate ?? 0L;
        long lastRewardAdDate = saveData?.LastRewardAdDate ?? 0L;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int interstitialRemaining = GetDebugRemainingSeconds(
            now,
            lastInterstitialDate,
            interstitialCooldown);
        int rewardRemaining = GetDebugRemainingSeconds(now, lastRewardAdDate, rewardCooldown);
        int effectiveRemaining = Math.Max(interstitialRemaining, rewardRemaining);

        bool victoryIntervalTrigger =
            groupOrder >= minGroup &&
            victoryInterval > 0 &&
            victoryCount >= victoryInterval;
        bool passedLevelTrigger =
            groupOrder >= minGroup &&
            passedLevelThreshold >= 0 &&
            passedLevelCount >= passedLevelThreshold;
        bool victoryTrigger = victoryIntervalTrigger || passedLevelTrigger;
        bool failureTrigger = failureThreshold > 0 && failureCount >= failureThreshold;
        GameManager gameManager = GameManager.Instance;
        bool activeController = ReferenceEquals(
            gameManager?.ControllerManager?.ActiveController,
            this);
        bool policyAvailable = _interstitialAdPolicy != null;
        bool bagAvailable = gameManager?.Bag != null;
        bool hasNoAds = bagAvailable && gameManager.Bag.HasTool(GameProps.NoAds);
        bool isNoAdForTest = InterstitialAdPolicy.isNoADForTest;
        bool sharedGatePassed =
            activeController &&
            policyAvailable &&
            saveData != null &&
            bagAvailable &&
            !hasNoAds &&
            !isNoAdForTest &&
            effectiveRemaining <= 0;

        string BuildFailureBlockReasons()
        {
            var reasons = new System.Collections.Generic.List<string>();
            if (!activeController)
                reasons.Add("IWanner 不是当前活动控制器");
            if (!policyAvailable)
                reasons.Add("IWanner 插屏策略不可用");
            if (saveData == null)
                reasons.Add("IWanner 广告存档不可用");
            if (!bagAvailable)
                reasons.Add("Bag 未就绪");
            if (isNoAdForTest)
                reasons.Add("测试免广告开关已开启");
            if (hasNoAds)
                reasons.Add("玩家已持有免广告");
            if (interstitialRemaining > 0)
                reasons.Add($"插屏冷却剩余 {interstitialRemaining}s");
            if (rewardRemaining > 0)
                reasons.Add($"激励后冷却剩余 {rewardRemaining}s");

            if (failureThreshold <= 0)
                reasons.Add("失败触发已关闭(Threshold<=0)");
            else if (!failureTrigger)
                reasons.Add($"失败次数不足({failureCount}/{failureThreshold})");

            if (reasons.Count > 0)
                return string.Join("；", reasons);

            return "无阻断；Reset 或 Failed 后重建时可触发";
        }

        string BuildVictoryBlockReasons()
        {
            var reasons = new System.Collections.Generic.List<string>();
            if (!activeController)
                reasons.Add("IWanner 不是当前活动控制器");
            if (!policyAvailable)
                reasons.Add("IWanner 插屏策略不可用");
            if (saveData == null)
                reasons.Add("IWanner 广告存档不可用");
            if (!bagAvailable)
                reasons.Add("Bag 未就绪");
            if (isNoAdForTest)
                reasons.Add("测试免广告开关已开启");
            if (hasNoAds)
                reasons.Add("玩家已持有免广告");
            if (interstitialRemaining > 0)
                reasons.Add($"插屏冷却剩余 {interstitialRemaining}s");
            if (rewardRemaining > 0)
                reasons.Add($"激励后冷却剩余 {rewardRemaining}s");
            if (groupOrder < minGroup)
                reasons.Add($"当前 Group 未达最低门槛({groupOrder}/{minGroup})");

            if (!victoryTrigger)
            {
                if (victoryInterval <= 0)
                    reasons.Add("完整 Group 次数触发已关闭(ADRangeLevelCount<=0)");
                else
                    reasons.Add($"完整 Group 次数不足({victoryCount}/{victoryInterval})");

                if (passedLevelThreshold >= 0)
                    reasons.Add($"兼容小关次数不足({passedLevelCount}/{passedLevelThreshold})");
            }

            if (reasons.Count > 0)
                return string.Join("；", reasons);

            return "无阻断；完整 Group 通关结算时可触发";
        }

        return new IWannerGame_GamePlayPanel.InterstitialDebugData
        {
            StateAvailable = saveData != null,
            ActiveController = activeController,
            PolicyAvailable = policyAvailable,
            BagAvailable = bagAvailable,
            IsNoAdForTest = isNoAdForTest,
            HasNoAds = hasNoAds,
            PendingPlacement = _debugPendingInterstitialPlacement?.ToString() ?? "None",
            RequestState = _debugPendingInterstitialPlacement.HasValue
                ? $"正在处理 {_debugPendingInterstitialPlacement.Value}"
                : "当前未发起；Reset、Failed 后重建或完整 Group 通关时检查",
            LastRequestResult = _debugLastInterstitialRequestResult ?? "尚无请求",
            GroupOrder = groupOrder,
            MinGroup = minGroup,
            VictoryCount = victoryCount,
            VictoryInterval = victoryInterval,
            VictoryIntervalTrigger = victoryIntervalTrigger,
            PassedLevelCount = passedLevelCount,
            PassedLevelThreshold = passedLevelThreshold,
            PassedLevelTrigger = passedLevelTrigger,
            VictoryTrigger = victoryTrigger,
            FailureCount = failureCount,
            FailureThreshold = failureThreshold,
            FailureTrigger = failureTrigger,
            LastInterstitialDate = lastInterstitialDate,
            LastRewardAdDate = lastRewardAdDate,
            InterstitialCooldown = interstitialCooldown,
            RewardCooldown = rewardCooldown,
            InterstitialRemaining = interstitialRemaining,
            RewardRemaining = rewardRemaining,
            EffectiveRemaining = effectiveRemaining,
            VictoryPolicyReady = sharedGatePassed && victoryTrigger,
            VictoryBlockReasons = BuildVictoryBlockReasons(),
            FailurePolicyReady = sharedGatePassed && failureTrigger,
            FailureBlockReasons = BuildFailureBlockReasons()
        };
    }

    private static int GetDebugRemainingSeconds(long now, long lastTimestamp, int cooldown)
    {
        if (cooldown <= 0 || lastTimestamp <= 0)
            return 0;

        long elapsed = now - lastTimestamp;
        return elapsed < cooldown
            ? (int)(cooldown - elapsed)
            : 0;
    }
#endif

    /// <summary>
    /// 用途：解析当前 Group 在 IWanner 最终配置顺序中的一基序号。
    /// 调用边界：仅供 IInterstitialAdPolicyHost 的最低 Group 门槛判断读取。
    /// 注意事项：GroupId 不是顺序值；控制器或 Group 不可用时返回 0，使广告资格安全失败。
    /// </summary>
    private int ResolveCurrentGroupOrder()
    {
        if (_currentGroup == null)
            return 0;

        var levelGroups = GameManager.Instance?.IWannerGameLevel?.LevelGroups;
        if (levelGroups == null)
            return 0;

        for (int i = 0; i < levelGroups.Count; i++)
        {
            if (levelGroups[i]?.Id == _currentGroup.Id)
                return i + 1;
        }

        return 0;
    }

    /// <summary>
    /// 用途：提交完整 Group 胜利、等待胜利插屏结果并创建首通或复通 VictoryPanel。
    /// 调用边界：仅由当前 Group 最后一小关完成门动画后调用一次。
    /// 注意事项：完整 Group 胜利才累计胜利次数并参与 ADRangeLevelCount 门槛；普通小关不累计。广告等待期间保持 Level 暂停、HUD/Actor 输入锁定和 ClickBlock 显示。
    /// </summary>
    private async UniTask HandleGroupCompletionAsync(int activationVersion)
    {
        _currentLevelFailureCount = 0;
        _isAwaitingFailureReset = false;
        _isOpeningLevel = true;
        _isPaused = false;
        _isSkipRewardAdRequesting = false;
        _isGroupResolved = true;
        _gamePlayPanel?.SetResetClickAreaVisible(false);
        _gamePlayPanel?.SetInputEnabled(false);
        _levelRuntime?.SetInputEnabled(false);
        _levelRuntime?.SetPaused(true);
        this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, true);
        PlayOutGameMusic();

        bool isFirstVictory = IsFirstGroupVictory();
        RecordGroupVictoryProgress();
        _interstitialAdPolicy?.OnLevelSucceeded();
        await WaitForInterstitialAsync(InterstitialPlacement.InGameVictory);

        if (!IsCurrentActivation(activationVersion) ||
            !_isActive ||
            !_isGroupResolved ||
            _currentGroup == null)
        {
            return;
        }

        _isOpeningLevel = false;
        int groupId = _currentGroup?.Id ?? 0;
        string currentLevelPath = _currentLevelData?.Path;
        string congraktTips = _currentGroup?.CongraktTips;
        bool isDeepLinkEntry = OneLinkManager.Instance.IsCurrentEntry(GameType.IWanner);
        bool isPanelCreated = isDeepLinkEntry
            ? GameVictoryPanel_DeepLink.CreateForIWanner() != null
            : isFirstVictory
                ? IWannerGame_GameVictoryPanel_Reward.Create(groupId, currentLevelPath, congraktTips) != null
                : IWannerGame_GameVictoryPanel.Create(groupId, currentLevelPath, congraktTips) != null;
        if (isPanelCreated)
            return;

        this.LogError(
            $"[IWannerPlayCtrl] Failed to create IWanner victory panel. groupId:{_currentGroup?.Id}, firstVictory:{isFirstVictory}");
        ExitToLevelSelection();
    }

    private bool IsFirstGroupVictory()
    {
        int groupId = _currentGroup?.Id ?? 0;
        var saveData = DB.IWannerGame;
        if (groupId <= 0 || saveData == null)
        {
            this.LogWarning(
                $"[IWannerPlayCtrl] Cannot resolve first Group victory. groupId:{groupId}, saveAvailable:{saveData != null}");
            return false;
        }

        saveData.EnsureValidCollections();
        return !saveData.PassedLevels.Contains(groupId);
    }

    private void RecordGroupVictoryProgress()
    {
        int groupId = _currentGroup?.Id ?? 0;
        var saveData = DB.IWannerGame;
        if (groupId <= 0 || saveData == null)
        {
            this.LogError(
                $"[IWannerPlayCtrl] Cannot record Group victory. groupId:{groupId}, saveAvailable:{saveData != null}");
            return;
        }

        bool changed = saveData.EnsureValidCollections();
        if (saveData.CurrentGroupID != groupId)
        {
            saveData.CurrentGroupID = groupId;
            changed = true;
        }

        if (saveData.CurrentLevel != 0)
        {
            saveData.CurrentLevel = 0;
            changed = true;
        }

        // OneLink 只记录当前组位置；新增 Group 通关记录也会在选组/进关时推导后继解锁。
        if (!OneLinkManager.Instance.IsCurrentEntry(GameType.IWanner))
        {
            if (!saveData.PassedLevels.Contains(groupId))
            {
                saveData.PassedLevels.Add(groupId);
                changed = true;
            }

            IWannerGameLevelCtrl levelCtrl = GameManager.Instance?.IWannerGameLevel;
            if (levelCtrl != null && levelCtrl.IsReady)
            {
                changed |= levelCtrl.RestorePassedGroupUnlocks(saveData);
                int currentGroupId = levelCtrl.ResolveCurrentGroupId(
                    saveData.MaxGroupId,
                    saveData.PassedLevels);
                if (currentGroupId > 0 && saveData.MaxGroupId != currentGroupId)
                {
                    saveData.MaxGroupId = currentGroupId;
                    changed = true;
                }
            }
            else
            {
                this.LogWarning(
                    $"[IWannerPlayCtrl] Cannot advance current Group because level config is unavailable. groupId:{groupId}");
            }
        }

        if (changed && !DB.SaveIWannerGame())
        {
            this.LogWarning(
                $"[IWannerPlayCtrl] Failed to save IWanner Group victory progress. groupId:{groupId}");
        }
    }

    private bool TryReturnToStartPanelAndOpenLevelSelection(out string error)
    {
        if (!_isActive)
        {
            error = "Cannot leave IWanner while IWannerPlayCtrl is inactive.";
            return false;
        }

        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            error = "GameManager is unavailable.";
            return false;
        }

        if (!gameManager.ReturnToStartPanel(out error))
            return false;

        if (IWannerGame_ChoiceLevelPopup.Create() == null)
        {
            error = "Returned to StartPanel, but failed to create IWannerGame_ChoiceLevelPopup.";
            return false;
        }

        error = null;
        return true;
    }

    #endregion

    #region Pause Host

    /// <summary>
    /// 用途：暂停当前 IWanner 小关与 HUD 输入。
    /// 调用边界：由通用暂停弹窗或 IWanner HUD 调用。
    /// </summary>
    public void Pause()
    {
        if (!_isActive)
            return;

        _isPaused = true;
        _gamePlayPanel?.SetInputEnabled(false);
        _levelRuntime?.SetPaused(true);
    }

    /// <summary>
    /// 用途：恢复当前 IWanner 小关与 HUD 输入。
    /// 调用边界：由通用暂停弹窗关闭流程调用。
    /// </summary>
    public void Resume()
    {
        if (!_isActive || _isOpeningLevel || _isSkipRewardAdRequesting || _isGroupResolved)
            return;

        _isPaused = false;
        _levelRuntime?.SetPaused(false);
        _levelRuntime?.SetInputEnabled(true);
        _gamePlayPanel?.SetInputEnabled(true);
    }

    /// <summary>
    /// 用途：退出当前 IWanner、恢复 StartPanel，并在其上打开 IWanner 选组弹窗。
    /// 调用边界：由通用设置弹窗 MenuBtn 或 VictoryPanel 下一步事件调用。
    /// 注意事项：返回 StartPanel 会切回主线并停用当前控制器；选组创建失败时保留 StartPanel 作为安全落点。
    /// </summary>
    public void ExitToLevelSelection()
    {
        if (!_isActive)
            return;

        if (TryReturnToStartPanelAndOpenLevelSelection(out string error))
            return;

        this.LogError($"[IWannerPlayCtrl] Return to StartPanel and open level selection failed. error:{error}");
    }

    #endregion

    #region Music

    /// <summary>
    /// IWanner 小关完成加载并恢复可操作状态时切换到游戏内音乐；同曲目已播放时不重新开始。
    /// </summary>
    private static void PlayInGameMusic()
    {
        if (!AudioManager.IsCurrentMusicTag(InGameMusicTag))
            AudioManager.PlayMusic(InGameMusicTag);
    }

    /// <summary>
    /// IWanner 整组结算或退出玩法时切换到游戏外音乐；沿用主线退出状态的 8 秒淡入参数。
    /// </summary>
    private static void PlayOutGameMusic()
    {
        if (!AudioManager.IsCurrentMusicTag(OutGameMusicTag))
            AudioManager.PlayMusic(OutGameMusicTag, fadeInSeconds: OutGameMusicFadeInSeconds);
    }

    #endregion

    #region Cleanup

    private bool IsCurrentActivation(int activationVersion)
    {
        return _isActive && activationVersion == _activationVersion;
    }

    private void CleanupRuntime()
    {
        CleanupCurrentLevelRuntime();
        _levelStatistics.DiscardAttempt();

        _currentGroup = null;
        _currentLevelData = null;
        _isTbnLevel = false;
        _currentLevelIndex = 0;
        _currentLevelFailureCount = 0;
        _isAwaitingFailureReset = false;
        _isSkipRewardAdRequesting = false;
        _isGroupResolved = false;
#if UNITY_EDITOR || DEBUG_MODE
        _debugPendingInterstitialPlacement = null;
        _debugLastInterstitialRequestResult = null;
        _nextInterstitialDebugRefreshTime = 0f;
#endif
        GameManager.Instance?.OpenCamera();
    }

    private void CleanupLevelRuntime()
    {
        if (_levelRuntime != null)
        {
            _levelRuntime.Resolved -= OnLevelResolved;
            _levelRuntime.ShutdownRuntime();
            _levelRuntime = null;
        }

        if (_levelInstance != null)
        {
            _levelInstance.SetActive(false);
            UnityEngine.Object.Destroy(_levelInstance);
            _levelInstance = null;
        }
    }

    private void CleanupCurrentLevelRuntime()
    {
        CleanupLevelRuntime();
        if (_gameGuidePopup != null)
        {
            UIManager.Instance.DestroyUI(_gameGuidePopup);
            _gameGuidePopup = null;
        }
        DestroyGameplayPanel();
    }

    private void DestroyGameplayPanel()
    {
        if (_gamePlayPanel == null)
            return;

        UIManager.Instance.DestroyUI(_gamePlayPanel);
        _gamePlayPanel = null;
    }

    private static string ResolveLevelResourcePath(string levelPath)
    {
        if (string.IsNullOrWhiteSpace(levelPath))
            return null;

        string normalizedPath = levelPath.Trim().Replace('\\', '/');
        if (normalizedPath.StartsWith("Assets/", StringComparison.Ordinal))
        {
            return normalizedPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                ? normalizedPath
                : $"{normalizedPath}.prefab";
        }

        string fileName = normalizedPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
            ? normalizedPath
            : $"{normalizedPath}.prefab";
        return $"{IWannerLevelDirectory}{fileName}";
    }

    #endregion
}
