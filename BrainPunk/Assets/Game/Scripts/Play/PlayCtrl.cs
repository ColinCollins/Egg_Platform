using System;
using Bear.EventSystem;
using Bear.Fsm;
using Bear.Logger;
using Bear.UI;
using Config;
using Config.Level;
using Cysharp.Threading.Tasks;
using Game;
using Game.Common;
using Game.Events;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using GameCommon;
using I2.Loc;
using UnityEngine;
using Game.ConfigModule;

/// <summary>
/// 关卡重置原因
/// </summary>
public enum GameResetType
{
    // 手动重置
    Manually,
    // 触发陷阱
    Failed,
}

/// <summary>
/// 
/// </summary>
public class PlayCtrl : Singleton<PlayCtrl>, IBearMachineOwner, IDebuger, IEventSender, IPlayController, IInterstitialAdPolicyHost, IGMFeature
{
    private StateMachine _machine;

    private EventSubscriber _subscriber;

    private GameTipsGuideFlow _gameTipsGuideFlow;

    public EventSubscriber Subscriber => _subscriber;

    /// <summary>
    /// 主线玩法使用的全局背包实例。
    /// 兼容既有主线调用；新跨玩法业务应优先通过 GameManager.Instance.Bag 访问。
    /// </summary>
    public SimpleBag Bag { get; private set; }

    public InterstitialAdPolicy InterstitialAdPolicy { get; private set; }

    int IInterstitialAdPolicyHost.InterstitialLevel => Level?.CurrentLevel ?? 0;

    IInterstitialAdPolicy IInterstitialAdPolicyHost.InterstitialAdPolicy =>
        InterstitialAdPolicy;

    #region Level

    private LevelCtrl _level;

    public LevelCtrl Level => _level;

    public GamePlayPanel @CurrentGamePlayPanel
    {
        private set;
        get;
    }

    public Transform SceneRoot;
    public BaseLevelCtrl LevelCtrl;
    public GameObject LevelPrefab;
    private const string LevelPath = "Assets/Game/Prefabs/Level/{0}.prefab";

    #endregion

    private bool isReady = false;
    private bool _isActive;

    /// <summary>
    /// 当前控制器负责主线玩法。
    /// </summary>
    public PlayModeType Mode => PlayModeType.Main;

    /// <summary>
    /// 主线控制器是否已完成一次性初始化。
    /// </summary>
    public bool IsInitialized => isReady;

    /// <summary>
    /// 主线控制器当前是否由 PlayControllerManager 激活。
    /// </summary>
    public bool IsActive => _isActive;

    #region Main Level Entry

    /// <summary>
    /// 进入序列最小的未通关主线关卡；全部通关时进入 Level_999。
    /// 调用边界：仅供 GameManager 在主线控制器已激活后调用，用于其它玩法返回可继续游玩的主线关卡。
    /// 注意事项：复用现有 EnterLevelEvent、加载、打点与失败回退链，不直接创建关卡实例。
    /// </summary>
    /// <param name="error">目标关卡或控制器状态无效时返回失败原因；成功时为空。</param>
    /// <returns>目标主线关卡请求已由活动的 PlayCtrl 接受时返回 true。</returns>
    public bool TryEnterLowestUnpassedOrLastLevel(out string error)
    {
        if (!_isActive)
        {
            error = "PlayCtrl is not active.";
            return false;
        }

        if (Level == null)
        {
            error = "Main LevelCtrl is unavailable.";
            return false;
        }

        if (!Level.TryGetLowestUnpassedOrLastLevel(
                out Config.LevelSort targetSort,
                out _,
                out error))
            return false;

        this.DispatchEvent(Witness<EnterLevelEvent>._, targetSort);
        error = null;
        return true;
    }

    #endregion

    #region IGMFeature

    /// <summary>
    /// 尝试让 GM 调试入口进入下一主线关卡。
    /// 注意事项：复用现有 SetCurrentLevel 与 EnterLevelEvent，不直接创建关卡实例。
    /// </summary>
    public bool TryEnterNextLevel()
    {
        if (Level == null)
            return false;

        int levelIndex = Level.CurrentLevel + 1;
        Level.SetCurrentLevel(levelIndex);
        this.DispatchEvent(Witness<EnterLevelEvent>._, Level.CurrentLevelSort);
        return true;
    }

    /// <summary>
    /// 尝试让 GM 调试入口进入上一主线关卡。
    /// 注意事项：复用现有 SetCurrentLevel 与 EnterLevelEvent，不直接创建关卡实例。
    /// </summary>
    public bool TryEnterPreviousLevel()
    {
        if (Level == null)
            return false;

        int levelIndex = Level.CurrentLevel - 1;
        Level.SetCurrentLevel(levelIndex);
        this.DispatchEvent(Witness<EnterLevelEvent>._, Level.CurrentLevelSort);
        return true;
    }

    /// <summary>
    /// 尝试让 GM 调试入口通过当前主线关卡。
    /// 注意事项：只在主线控制器活动时派发现有 SUCCESS 状态事件，保留原结算链。
    /// </summary>
    /// <param name="source">调试命令来源；主线现有事件链暂不消费该值。</param>
    public bool TryForceVictory(string source)
    {
        if (!_isActive)
            return false;

        this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.SUCCESS);
        return true;
    }

    #endregion

    /// <summary>
    /// 通过统一上下文初始化主线玩法控制器。
    /// 调用边界：仅由 PlayControllerManager 在首次激活前调用。
    /// </summary>
    /// <param name="context">包含 GameManager 共享背包的玩法上下文。</param>
    public void Initialize(PlayControllerContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        Init(context.Bag);
    }

    /// <summary>
    /// 初始化主线玩法控制器，并绑定由 GameManager 持有的全局背包。
    /// 兼容旧调用；新入口应由 PlayControllerManager 通过 Initialize 调用。
    /// 必须在 DB 与配置完成加载后调用；重复调用不会重新初始化或替换背包。
    /// </summary>
    /// <param name="bag">全玩法共享且已进入可用生命周期的背包实例。</param>
    public void Init(SimpleBag bag)
    {
        if (isReady)
            return;

        if (bag == null)
            throw new ArgumentNullException(nameof(bag));

        LevelCtrl initializedLevel = null;
        InterstitialAdPolicy initializedAdPolicy = null;
        StateMachine initializedMachine = null;

        try
        {
            initializedLevel = new LevelCtrl();
            initializedLevel.Init();

            initializedAdPolicy = new InterstitialAdPolicy();

            Bag = bag;
            _level = initializedLevel;
            InterstitialAdPolicy = initializedAdPolicy;

            initializedMachine = new StateMachine(this);
            _machine = initializedMachine;
            _machine.Inject(typeof(PlayCtrl_Start),
            typeof(PlayCtrl_Playing),
            typeof(PlayCtrl_Pause),
            typeof(PlayCtrl_Success),
            typeof(PlayCtrl_Failed));

            _machine.Apply(GetType());
            _machine.Enter(GamePlayStateName.START);

            if (StartPanel.Create() == null)
                throw new InvalidOperationException("[PlayCtrl] Failed to create StartPanel.");

            isReady = true;
        }
        catch
        {
            initializedMachine?.Dispose();
            initializedAdPolicy?.Dispose();

            isReady = false;
            _isActive = false;
            _machine = null;
            InterstitialAdPolicy = null;
            _level = null;
            Bag = null;
            throw;
        }
    }

    /// <summary>
    /// 激活主线控制器并恢复事件订阅。
    /// 调用边界：仅由 PlayControllerManager 在切换到主线玩法时调用。
    /// </summary>
    public void Activate()
    {
        if (!isReady)
            throw new InvalidOperationException("PlayCtrl must be initialized before activation.");

        if (_isActive)
            return;

        _isActive = true;
        AddListener();
    }

    /// <summary>
    /// 停用主线控制器、停止接收全局玩法事件并清理主线运行时现场。
    /// 注意事项：不会创建 StartPanel；切换目标控制器负责自己的界面。
    /// </summary>
    public void Deactivate()
    {
        if (!_isActive)
            return;

        _isActive = false;
        EventsUtils.ResetEvents(ref _subscriber);
        ResetRuntimeForControllerExit();
    }

    private void AddListener()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<SwitchGameStateEvent>(OnSwitchState);
        _subscriber.Subscribe<GameResetEvent>(OnGameResetEvent);

        _subscriber.Subscribe<GameReplayEvent>(OnGameReplayEvent);
        _subscriber.Subscribe<EnterLevelEvent>(OnGameEnter);
        _subscriber.Subscribe<EnterNextLevelEvent>(OnEnterNextLevel);

        _subscriber.Subscribe<UseTipsEvent>(OnTipsUsed);
        _subscriber.Subscribe<UpdatePropEvent>(OnPropsUpdate);
        _subscriber.Subscribe<GamePlayPanelSwitchTipsEvent>(OnTipsWarningVisibilityChanged);
    }

    /// <summary>
    /// 在提示入口气泡完全隐藏或被玩家主动关闭时结束本轮展示，
    /// 供 Playing 状态从该时刻开始下一轮配置化倒计时。
    /// </summary>
    private void OnTipsWarningVisibilityChanged(GamePlayPanelSwitchTipsEvent evt)
    {
        if (evt == null || evt.IsShow || Level?.CurrentLevelState == null)
            return;

        Level.CurrentLevelState.SwitchClickTips(false);
    }

    /// <summary>
    /// 用于红点数据刷新
    /// </summary>
    /// <param name="event"></param>
    /// <exception cref="NotImplementedException"></exception>
    private void OnPropsUpdate(UpdatePropEvent @event)
    {
        if (@event.Prop == Config.Game.GameProps.Star)
            RedDotManager.Refresh(RedDotKey.Postcard);
    }

    private void OnTipsUsed(UseTipsEvent evt)
    {
        Level.CurrentLevelState.SwitchTips(true);
    }

    private void OnGameEnter(EnterLevelEvent evt)
    {
        OneLinkManager.Instance.OnLevelEntryStarted(GameType.Default);
        var sortId = evt.Data.Id;
        Level.SetCurrentLevel(sortId);

        var data = Level.CurrentLevelData;
        Level.CurrentLevelState.StartLevel(evt.Data.Id, data.Path);

        if (sortId == 1 && !DB.GameData.HasComics)
        {
            ComicsPopup.Create(() =>
            {
                EnterGame(sortId);
            });

            return;
        }

        EnterGame(sortId);
    }

    private async UniTask ShowBeginerPopup()
    {
        if (!Application.isPlaying || DB.GameData.HasBegineTutorial || LevelCtrl == null)
            return;

        var sourceLevel = LevelCtrl;
        var cancellationToken = sourceLevel.GetCancellationTokenOnDestroy();
        try
        {
            await UniTask.WaitForSeconds(3f, cancellationToken: cancellationToken);

            // 暂停或其它弹窗期间等待；本轮关卡退出、结束或已展示时结束等待，不插入旧关卡弹窗。
            await UniTask.WaitUntil(() =>
                !Application.isPlaying || !_isActive || sourceLevel != LevelCtrl || DB.GameData.HasBegineTutorial ||
                (!CheckState(GamePlayStateName.PLAYING) && !CheckState(GamePlayStateName.PAUSE)) ||
                (Application.isFocused && CheckState(GamePlayStateName.PLAYING) && !UIManager.Instance.HasBlockingPopup),
                cancellationToken: cancellationToken);

            if (Application.isPlaying && _isActive && sourceLevel != null && sourceLevel == LevelCtrl &&
                !DB.GameData.HasBegineTutorial && CheckState(GamePlayStateName.PLAYING))
                BeginerPopup.Create();
        }
        catch (OperationCanceledException) { }
    }

    // 进入游戏
    private void EnterGame(int sortId)
    {
        EnterLevelLoading.Create(async () =>
        {
            DestroyLevel();
            var success = await CreateLevelAsync(Level.CurrentLevelData.Path);
            if (success)
            {
                _machine.Enter(GamePlayStateName.PLAYING);
            }
            else
            {
                ChoiceLevelPanel.Create();
            }
        }, OnEnterLevelLoadingEraseComplete);

        TrackLevelStart(sortId);
        // 打点需要刷新 levelId
        GameAttribute.Instance.SetLevelId(DB.GameData.CurrentLevel);
    }

    private void OnEnterNextLevel(EnterNextLevelEvent evt)
    {
        CancelGameTipsGuide();
        Shop2SkinGuideFlow.CancelCurrent();

        bool isDeepLinkEntry = OneLinkManager.Instance.IsCurrentEntry(GameType.Default);
        string deepLinkError = null;
        if (isDeepLinkEntry && TryEnterLowestUnpassedOrLastLevel(out deepLinkError))
        {
            OneLinkManager.Instance.EndEntry(GameType.Default);
            return;
        }

        if (isDeepLinkEntry)
        {
            this.LogError(
                $"[PlayCtrl] Enter lowest unpassed DeepLink level failed. error:{deepLinkError}");
            OneLinkManager.Instance.EndEntry(GameType.Default);
            ExitToLevelSelection();
            return;
        }

        OneLinkManager.Instance.EndEntry(GameType.Default);

        var data = Level.CurrentLevelData;
        if (data == null)
        {
            Level.CurrentLevelState.SwitchTips(false);
            DestroyLevel();
            // switch panel
            ClickTransformPanel.Create(() =>
            {
                ChoiceLevelPanel.Create();
            });
        }
        else
        {
            Level.CurrentLevelState.StartLevel(Level.CurrentLevel, data.Path);
            EnterLevelLoading.Create(async () =>
            {
                DestroyLevel();
                var success = await CreateLevelAsync(data.Path);
                if (success)
                {
                    _machine.Enter(GamePlayStateName.PLAYING);
                }
                else
                {
                    ChoiceLevelPanel.Create();
                }
            }, OnEnterLevelLoadingEraseComplete);
        }

        TrackLevelStart(Level.CurrentLevel);
        // 打点需要刷新 levelId
        GameAttribute.Instance.SetLevelId(DB.GameData.CurrentLevel);
    }

    public bool CheckState(string state)
    {
        return _machine.IsRunning(state);
    }

    private void OnSwitchState(SwitchGameStateEvent evt)
    {
        this.Log(evt.NewState);
        _machine.Enter(evt.NewState);
    }

    private void OnGameReplayEvent(GameReplayEvent @event)
    {
        CancelGameTipsGuide();
        Shop2SkinGuideFlow.CancelCurrent();

        if (Level == null || Level.CurrentLevelState == null)
        {
            this.LogWarning("[PlayCtrl] GameReplay ignored. Level runtime state is missing.");
            return;
        }

        if (Level.CurrentLevelState.IsAttemptTransitioning)
        {
            this.LogWarning("[PlayCtrl] GameReplay ignored. An attempt transition is already pending.");
            return;
        }

        int replayLevelId = GetReplayLevelId();
        if (replayLevelId <= 0)
        {
            this.LogWarning($"[PlayCtrl] GameReplay ignored. Invalid level id: {replayLevelId}");
            return;
        }

        var replaySort = Level.GetLevelSortById(replayLevelId);
        if (replaySort == null)
        {
            this.LogWarning($"[PlayCtrl] GameReplay ignored. LevelSort not found: {replayLevelId}");
            return;
        }

        var replayData = Level.GetLevelDataById(replaySort.Path);
        if (replayData == null)
        {
            this.LogWarning($"[PlayCtrl] GameReplay ignored. LevelData not found: {replaySort.Path}");
            return;
        }

        Level.SetCurrentLevel(replayLevelId);
        PrepareReplayRuntimeState(replayLevelId, replayData.Path);

        TrackLevelStart(replayLevelId);
        GameAttribute.Instance.SetLevelId(DB.GameData.CurrentLevel);

        ReplayGame(replayData.Path).Forget();
    }

    private int GetReplayLevelId()
    {
        int runtimeLevelId = Level?.CurrentLevelState?.LevelId ?? 0;
        if (runtimeLevelId > 0)
            return runtimeLevelId;

        return Level?.CurrentLevel ?? 0;
    }

    private void PrepareReplayRuntimeState(int replayLevelId, string replayLevelPath)
    {
        bool startedNewRuntimeContext = false;
        if (Level.CurrentLevelState.LevelId != replayLevelId
            || !string.Equals(Level.CurrentLevelState.LevelPath, replayLevelPath, StringComparison.Ordinal))
        {
            Level.CurrentLevelState.StartLevel(replayLevelId, replayLevelPath);
            startedNewRuntimeContext = true;
        }

        Level.CurrentLevelState.BeginAttemptTransition();
        if (!startedNewRuntimeContext)
            Level.CurrentLevelState.TryToAddPlayCount();
        Level.CurrentLevelState.SwitchTips(false);
        Level.CurrentLevelState.ResetData();
    }

    /// <summary>
    /// 游戏重置
    /// </summary>
    /// <param name="evt"></param>
    private void OnGameResetEvent(GameResetEvent evt)
    {
        if (Level == null || Level.CurrentLevelState == null)
        {
            this.LogWarning("[PlayCtrl] GameReset ignored. Level runtime state is missing.");
            return;
        }

        var runtimeState = Level.CurrentLevelState;
        if (runtimeState.IsAttemptTransitioning)
        {
            this.LogWarning("[PlayCtrl] GameReset ignored. An attempt transition is already pending.");
            return;
        }

        runtimeState.BeginAttemptTransition();
        CancelGameTipsGuide();
        Shop2SkinGuideFlow.CancelCurrent();
        InterstitialAdPolicy?.OnPlayerFailedOrRetried();

        if (evt.Type == GameResetType.Manually)
            runtimeState.RecordReset();
        else
            runtimeState.RecordFail();

        GameSDKService.Instance.LevelEndEvent(GameSDKService.LevelEndTypeFailed);
        runtimeState.ResetDataPreservingRevive();
        runtimeState.TryToAddPlayCount();
        TrackLevelStart(runtimeState.LevelId);

        ResetGame().Forget();
    }

    private async UniTask ResetGame()
    {
        UIManager.Instance.CloseAllUI(UILayer.Popup);
        InterstitialAdHelper.TryToShowInterstitial(Config.Game.InterstitialPlacement.InGameFailed.ToString(), OnInterstitialCallback);

        await UniTask.WaitForSeconds(0.5f, ignoreTimeScale: true);

        // Show Ask 
        DestroyLevel();
        await CreateLevelAsync(Level.CurrentLevelData.Path);
        this.DispatchEvent(Witness<GamePlayPanelFadeInEvent>._);
        // 重置一下点击 Tips 的状态
        Level.CurrentLevelState.SwitchClickTips(false);

        _machine.Enter(GamePlayStateName.PAUSE);

        await UniTask.WaitForSeconds(1f, ignoreTimeScale: true);

        _machine.Enter(GamePlayStateName.PLAYING);

        TryToShowNoAdPopup();
    }

    /// <summary>
    /// 重玩当前关卡
    /// </summary>
    /// <param name="levelPath"></param>
    /// <returns></returns>
    private async UniTask ReplayGame(string levelPath)
    {
        UIManager.Instance.CloseAllUI(UILayer.Popup);

        await UniTask.WaitForSeconds(0.5f, ignoreTimeScale: true);

        DestroyLevel();
        var success = await CreateLevelAsync(levelPath);
        if (!success)
        {
            ChoiceLevelPanel.Create();
            return;
        }

        this.DispatchEvent(Witness<GamePlayPanelFadeInEvent>._);
        Level.CurrentLevelState.SwitchClickTips(false);

        _machine.Enter(GamePlayStateName.PAUSE);

        await UniTask.WaitForSeconds(1f, ignoreTimeScale: true);

        _machine.Enter(GamePlayStateName.PLAYING);

        TryToShowNoAdPopup();
    }

    private void OnEnterLevelLoadingEraseComplete()
    {
        this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PLAYING);

        var levelCtrl = Level;
        if (levelCtrl != null && !levelCtrl.IsLastLevel)
            Announce.Create(levelCtrl.CurrentLevel, data: levelCtrl.CurrentLevelData);

        TryToShowNoAdPopup();
        TryToGetFreeSkin();
    }

    private void TryToShowNoAdPopup()
    {
        if (Level != null && Level.IsLastLevel)
            return;

        var NoAdPopupModule =
ScheduleSystem.Instance.GetModule<DailyScheduleModule>()?.GetRefreshable<InterstitialNOAdPopupAutoShowModule>();

        // 主动尝试弹出 noAdPopup
        var noAdModule = NoAdPopupModule;
        if (noAdModule != null && noAdModule.ShouldShowNoAdPopup() && InterstitialAdHelper.HasInsterstitial)
        {
            NoAdsPopup.Create();
            noAdModule.RecordShow();
            InterstitialAdHelper.HasInsterstitial = false;
        }
    }

    private void OnInterstitialCallback(string placement, bool isSuc)
    {

    }

    /// <summary>
    /// LevelStart 打点
    /// </summary>
    private void TrackLevelStart(int levelId)
    {
        bool hasPassedCurrentLevel =
            DB.GameData?.PassedLevels?.Contains(levelId) == true;
        int startType = hasPassedCurrentLevel
            ? GameSDKService.LevelStartTypeReplay
            : GameSDKService.LevelStartTypeNewGame;
        GameSDKService.Instance.LevelStartEvent(startType);
    }

    /// <summary>
    /// 清理當前關卡
    /// </summary>
    public void DestroyLevel()
    {
        CancelGameTipsGuide();
        Shop2SkinGuideFlow.CancelCurrent();

        if (LevelCtrl == null)
            return;

        LevelCtrl.DestroyLevel();
        LevelCtrl = null;

        AudioManager.StopAllSound();
        Announce.CloseStraightly();
        GameManager.Instance.OpenCamera();
    }

    public async UniTask<bool> CreateLevelAsync(string levelName)
    {
        try
        {
            if (LevelCtrl != null)
            {
                this.LogWarning("[PlayCtrl] CreateLevelAsync skipped: LevelCtrl already exists.");
                OneLinkManager.Instance.EndEntry(GameType.Default);
                return false;
            }

            GameManager.Instance.CloseCamera();

            var path = string.Format(LevelPath, levelName);

            if (!LevelPrefab || !LevelPrefab.name.Equals(LevelPath))
                LevelPrefab = await Bear.ResourceSystem.ResourceManager.Instance.LoadAsync<GameObject>(path);

            if (!LevelPrefab)
            {
                this.LogError($"Level lost: {levelName} (path: {path})");
                OneLinkManager.Instance.EndEntry(GameType.Default);
                return false;
            }

            LevelCtrl = GameObject.Instantiate(LevelPrefab, SceneRoot).GetComponent<BaseLevelCtrl>();
            RefreshGamePanel();

            // 重置状态不播动画
            if (Level.CurrentLevelState.ResetCount <= 0 && Level.CurrentLevelState.FailCount <= 0)
                LevelCtrl.TryToPlayEnterDoorAnim();

            ShowBeginerPopup().Forget();
            OneLinkManager.Instance.ConfirmLevelEntered(GameType.Default);
            return true;
        }
        catch
        {
            OneLinkManager.Instance.EndEntry(GameType.Default);
            throw;
        }
    }

    /// <summary>
    /// 因为需求会有多种不同的 gamePanel，所以我们需要针对变化，设置变体
    /// </summary>
    private void RefreshGamePanel()
    {
        if (CurrentGamePlayPanel != null)
        {
            UIManager.Instance.DestroyUI(CurrentGamePlayPanel);
        }

        CurrentGamePlayPanel = GamePlayPanel.Create(LevelCtrl.GamePlayPanelName);
        CurrentGamePlayPanel.SetData(Level.CurrentLevel);
        CurrentGamePlayPanel.PlayShowPanelAnim().Forget();

        // TBC 关卡播放音效
        if (Level.IsLastLevel)
        {
            AudioManager.PlaySound("TBC");
        }
    }

    /// <summary>
    /// 尝试获取免费皮肤
    /// </summary>
    private void TryToGetFreeSkin()
    {
        int level = ConfigManager.RemoteConfig.GetFreeSkinLevel();
        if (level < 0)
            return;
        if (Level.CurrentLevel != level)
            return;

        string skinId = ConfigManager.RemoteConfig.GetFreeSkinId();
        if (string.IsNullOrEmpty(skinId))
        {
            this.LogError($"SkinData is null: {skinId}");
            return;
        }

        // check 是否已经获取
        if (DB.GameData.OwnedSkin.Contains(skinId))
            return;

        var tables = ConfigManager.Instance?.Tables;
        var skinTable = tables?.TbSkin;
        if (skinTable == null)
        {
            return;
        }

        var data = skinTable.DataList.Find(d => d.Id.Equals(skinId));
        if (data == null)
        {
            this.LogError($"SkinData is null: {skinId}");
        }

        GetSkinPopup.Create(data);
    }


    public void Update()
    {
        _machine?.Update();

        // 处理手机返回键
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ShowExitPopup();
        }
    }

    /// <summary>
    /// 由 PlayControllerManager 驱动主线逐帧逻辑。
    /// 非激活状态不会更新状态机或处理手机返回键。
    /// </summary>
    public void Tick()
    {
        if (_isActive)
            Update();
    }

    /// <summary>
    /// 通过主线既有状态事件暂停玩法，保持 GameplayPanel 的暂停标记同步。
    /// </summary>
    public void Pause()
    {
        if (_isActive)
            this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PAUSE);
    }

    /// <summary>
    /// 通过主线既有状态事件恢复玩法，保持 GameplayPanel 的暂停标记同步。
    /// </summary>
    public void Resume()
    {
        if (_isActive)
            this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PLAYING);
    }

    /// <summary>
    /// 结束当前主线关卡并打开主线 ChoiceLevelPanel。
    /// 调用边界：供复用的 GameSettingPopup 操作当前激活控制器。
    /// 注意事项：离开当前关卡会话前清除已获得的提示资格，避免返回选关页后继续继承。
    /// </summary>
    public void ExitToLevelSelection()
    {
        if (!_isActive)
            return;

        OneLinkManager.Instance.EndEntry(GameType.Default);
        Level?.CurrentLevelState?.SwitchTips(false);
        this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.FAILED);
        ClickTransformPanel.Create(() => ChoiceLevelPanel.Create());
    }

    /// <summary>
    /// 恢复到应用首次打开 StartPanel 时的主线运行时状态。
    /// 调用边界：仅由 GameManager 在主线控制器重新成为活动控制器后调用。
    /// </summary>
    internal bool TryRestoreStartPanel(out string error)
    {
        ResetRuntimeForControllerExit();
        UIManager.Instance.CloseAllUI(UILayer.Normal);
        GameManager.Instance.OpenCamera();

        StartPanel panel = StartPanel.Create();
        if (panel == null)
        {
            error = "Failed to create StartPanel.";
            return false;
        }

        error = null;
        return true;
    }

    private void ResetRuntimeForControllerExit()
    {
        OneLinkManager.Instance.EndEntry(GameType.Default);
        bool hasActiveLevel = LevelCtrl != null;
        if (hasActiveLevel && _machine != null && !CheckState(GamePlayStateName.FAILED))
            _machine.Enter(GamePlayStateName.FAILED);

        DestroyLevel();

        if (CurrentGamePlayPanel != null)
        {
            UIManager.Instance.DestroyUI(CurrentGamePlayPanel);
            CurrentGamePlayPanel = null;
        }

        if (_machine != null && !CheckState(GamePlayStateName.START))
            _machine.Enter(GamePlayStateName.START);
    }

    /// <summary>
    /// 显示退出确认弹窗
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
            onNo: null
        );
    }

    /// <summary>
    /// 判断当前返回键是否可以打开退出确认弹窗。
    /// Guide Flow 和成功结算期间由各自流程独占交互；退出弹窗已打开时也不重复入栈。
    /// </summary>
    private bool CanShowExitPopup()
    {
        CommmonPopup exitPopup = UIManager.Instance?.GetUI<CommmonPopup>(nameof(CommmonPopup));
        if (exitPopup != null && exitPopup.IsOpen)
            return false;

        GuideFlowManager guideFlowManager = GuideFlowManager.Instance;
        if (guideFlowManager.HasActiveFlow || guideFlowManager.PendingCount > 0)
            return false;

        return _machine == null || !CheckState(GamePlayStateName.SUCCESS);
    }

    #region Game Tips Guide

    /// <summary>
    /// 用途：在首次提示剧情门槛满足后，从当前 GamePlayPanel 获取局部 UI 引用并启动引导。
    /// 调用边界：仅由 PlayCtrl_Playing 的既有更新节点在 Unity 运行态主线程调用。
    /// 注意事项：流程由 PlayCtrl 跨 Playing/Pause 状态持有；资格、界面或引用无效时不会创建流程。
    /// </summary>
    /// <param name="runtimeState">当前关卡运行时状态。</param>
    /// <param name="triggerSettings">当前 Playing 状态进入时读取的引导配置快照。</param>
    /// <returns>本次成功创建并提交引导流程时返回 true。</returns>
    internal bool TryStartGameTipsGuide(
        LevelRuntimeState runtimeState,
        GameTipsGuideTriggerSettings triggerSettings)
    {
        if (!Application.isPlaying ||
            !CheckState(GamePlayStateName.PLAYING) ||
            _gameTipsGuideFlow != null ||
            (LevelCtrl != null && LevelCtrl.IsActorEnterDoor) ||
            !GameTipsGuideFlow.CanStart(runtimeState))
        {
            return false;
        }

        GamePlayPanel gamePlayPanel = CurrentGamePlayPanel;
        if (gamePlayPanel == null ||
            !gamePlayPanel.TryGetGameTipsGuideTargets(
                out RectTransform tipsButtonTarget,
                out RectTransform guideRoot))
        {
            return false;
        }

        var flow = new GameTipsGuideFlow(
            runtimeState,
            triggerSettings,
            tipsButtonTarget,
            guideRoot,
            gamePlayPanel.OpenFirstGuideFreeTipsPopup);
        _gameTipsGuideFlow = flow;
        RunGameTipsGuideAsync(flow).Forget();
        return true;
    }

    /// <summary>
    /// 用途：当前 GamePlayPanel 关闭时取消仍引用该界面的首次提示剧情引导。
    /// 调用边界：仅由 GamePlayPanel.OnClose 在 Unity 主线程调用。
    /// 注意事项：非当前界面的关闭通知不会影响正在运行的流程。
    /// </summary>
    /// <param name="gamePlayPanel">正在关闭的 GamePlayPanel。</param>
    internal void CancelGameTipsGuideFor(GamePlayPanel gamePlayPanel)
    {
        if (!ReferenceEquals(CurrentGamePlayPanel, gamePlayPanel))
            return;

        CancelGameTipsGuide();
    }

    private void CancelGameTipsGuide()
    {
        _gameTipsGuideFlow?.Cancel();
    }

    private async UniTask RunGameTipsGuideAsync(GameTipsGuideFlow flow)
    {
        this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, true);

        try
        {
            GuideFlowResult result = await flow.RunAsync();
            if (result == GuideFlowResult.Failed)
                this.LogWarning("[GameTipsGuide] Flow stopped because a required guide dependency was unavailable.");
        }
        catch (Exception exception)
        {
            this.LogError($"[GameTipsGuide] Unexpected flow exception. error:{exception}");
        }
        finally
        {
            this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, false);

            if (ReferenceEquals(_gameTipsGuideFlow, flow))
                _gameTipsGuideFlow = null;
        }
    }

    #endregion

    public void OnDestroy()
    {
        Deactivate();
        CancelGameTipsGuide();
        Shop2SkinGuideFlow.CancelCurrent();
        _machine?.Dispose();
        InterstitialAdPolicy?.Dispose();
        InterstitialAdPolicy = null;
    }

    /// <summary>
    /// 释放主线控制器运行时资源。
    /// 调用边界：仅由 PlayControllerManager.Shutdown 统一调用。
    /// </summary>
    public void Shutdown()
    {
        OnDestroy();
    }
}
