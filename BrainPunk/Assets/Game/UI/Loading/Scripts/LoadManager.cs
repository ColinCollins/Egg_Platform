using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Bear.EventSystem;
using Bear.Logger;
using Bear.SaveModule;
using Bear.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game;
using Game.Common;
using Game.ConfigModule;
using Game.HotReload;
using Game.Scripts.Common;
using GF;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SDK.Remote;
using Bear.ResourceSystem;

// 当前场景，用状态机，处理一些资源加载或者配置初始化的功能设计
public class LoadManager : MonoBehaviour, IDebuger
{
    public Booster booster;
    [SerializeField] private Image ProcessFill;
    [SerializeField] private TextMeshProUGUI processTxt;

    [SerializeField] private UISpineCtrl spineCtrl;

    private Queue<Func<IEnumerator<float>>> functionQueue = new Queue<Func<IEnumerator<float>>>();
    private Coroutine currentCoroutine;
    private EventSubscriber _hotReloadSubscriber;
    private Tweener _processFillTweener;
    private bool _hasCriticalInitializationFailure;

    // private bool isPlayCompleted = false;

    public void Awake()
    {
        EnqueueFunction(EnterGame);

        EnqueueFunction(HotReload);

        // 加载配置表
        EnqueueFunction(LoadConfigAsync);

        // On Finshed
        EnqueueFunction(OnPostProcess);

        PlayLoadingSpine();
    }

    private void OnDestroy()
    {
        _processFillTweener?.Kill();
        EventsUtils.ResetEvents(ref _hotReloadSubscriber);
    }

    private void PlayLoadingSpine()
    {
        var entry = spineCtrl.PlayAnimation("loading", false);
        entry.Complete += (track) =>
        {
            spineCtrl.PlayAnimation("loading01", true);
        };
    }

    private IEnumerator<float> EnterGame()
    {
        // 这里需要注意一下 iap 的初始化进度是不是很快
        booster.Init();
        GameManager.Instance.Init();
        DBManager.Instance.Initialize();
        LocalizatioinUtils.InitializeStartupLanguage();

        DB.GameData.PlayCount++;
        DB.GameData.Save();

        GameManager.Instance.RefreshIsNewVersion();

        // 时间周期调度中心
        ScheduleSystem.Instance.RegisterModule(new DailyScheduleModule());
        ScheduleSystem.Instance.RegisterModule(new DailyUTCScheduleModule());
        ScheduleSystem.Instance.CheckAllModules();

        yield return 1;
    }

    private IEnumerator<float> HotReload()
    {
        var packageName = GameSettingData.Setting.defaultPackageName;
        BuiltinPackageElement packageElement = GameSettingData.Setting.GetDefaultPackageElement(packageName);

        // 初始化事件订阅
        EventsUtils.ResetEvents(ref _hotReloadSubscriber);
        _hotReloadSubscriber.Subscribe<HotReloadEvents.PatchStepsChange>(OnPatchStepsChange);
        _hotReloadSubscriber.Subscribe<HotReloadEvents.DownloadUpdate>(OnDownloadUpdate);
        _hotReloadSubscriber.Subscribe<HotReloadEvents.FoundUpdateFiles>(OnFoundUpdateFiles);
        _hotReloadSubscriber.Subscribe<HotReloadEvents.InitializeFailed>(OnInitializeFailed);
        _hotReloadSubscriber.Subscribe<HotReloadEvents.PackageVersionRequestFailed>(OnPackageVersionRequestFailed);
        _hotReloadSubscriber.Subscribe<HotReloadEvents.PackageManifestUpdateFailed>(OnPackageManifestUpdateFailed);
        _hotReloadSubscriber.Subscribe<HotReloadEvents.WebFileDownloadFailed>(OnWebFileDownloadFailed);

        // 初始化热更新流程
        HotReloadCtrl.Instance.Init(packageName, packageElement.playMode);

        // 等待热更新完成
        float progress = 0f;
        float hotReloadStartTime = Time.time;
        const float HOT_RELOAD_TIMEOUT = 30f;
        while ((!HotReloadCtrl.Instance.IsFinish || !booster.IsReady) && !HotReloadCtrl.Instance.IsFailed)
        {
            // 总超时保护：避免任何异常导致无限等待
            if (Time.time - hotReloadStartTime > HOT_RELOAD_TIMEOUT)
            {
                this.LogError("[LoadManager] HotReload timeout (60s), force continue.");
                break;
            }

            // 可以根据需要更新进度
            yield return progress;
            progress = Mathf.Min(progress + 0.01f, 0.99f);
        }

        if (HotReloadCtrl.Instance.IsFailed)
        {
            this.LogError("[LoadManager] HotReload failed, continue with built-in resources.");
        }

        // 热更新完成或失败，重置事件订阅
        EventsUtils.ResetEvents(ref _hotReloadSubscriber);
        yield return 1f;
    }

    #region HotReload Event Handlers
    private void OnPatchStepsChange(HotReloadEvents.PatchStepsChange evt)
    {
        Debug.Log($"[LoadManager] HotReload Step: {evt.Tips}");
        processTxt.text = evt.Tips;
    }

    private void OnDownloadUpdate(HotReloadEvents.DownloadUpdate evt)
    {
        if (evt.TotalDownloadCount > 0)
        {
            float progress = (float)evt.CurrentDownloadCount / evt.TotalDownloadCount;
            processTxt.text = $"[LoadManager] Download Progress: {evt.CurrentDownloadCount}/{evt.TotalDownloadCount} ({progress * 100:F1}%)";
        }
    }

    private void OnFoundUpdateFiles(HotReloadEvents.FoundUpdateFiles evt)
    {
        processTxt.text = $"[LoadManager] Found {evt.TotalCount} files to download, total size: {evt.TotalSizeBytes / 1024 / 1024} MB";
    }

    private void OnInitializeFailed(HotReloadEvents.InitializeFailed evt)
    {
        processTxt.color = Color.red;
        processTxt.text = "[LoadManager] HotReload Initialize Failed!";
        this.LogError("[LoadManager] HotReload Initialize Failed!");
    }

    private void OnPackageVersionRequestFailed(HotReloadEvents.PackageVersionRequestFailed evt)
    {
        processTxt.color = Color.red;
        processTxt.text = "[LoadManager] HotReload Initialize Failed!";
        this.LogError("[LoadManager] HotReload Package Version Request Failed!");
    }

    private void OnPackageManifestUpdateFailed(HotReloadEvents.PackageManifestUpdateFailed evt)
    {
        processTxt.color = Color.red;
        processTxt.text = "[LoadManager] HotReload Initialize Failed!";
        this.LogError("[LoadManager] HotReload Package Manifest Update Failed!");
    }

    private void OnWebFileDownloadFailed(HotReloadEvents.WebFileDownloadFailed evt)
    {
        processTxt.color = Color.red;
        processTxt.text = "[LoadManager] HotReload Initialize Failed!";
        this.LogError($"[LoadManager] HotReload Download Failed: {evt.FileName}, Error: {evt.Error}");
    }
    #endregion

    /// <summary>
    /// 异步加载配置表
    /// </summary>
    private IEnumerator<float> LoadConfigAsync()
    {
        // 是否需要清理缓存
        RemoteConfigService.TryToClearRemoteCache();
        // 先按 LoadManager 风格执行 RemoteConfig 的加载协程
        var remoteEnumerator = RemoteConfigService.UpdateRemoteConfigsForLoadManager();
        while (remoteEnumerator.MoveNext())
        {
            yield return remoteEnumerator.Current;
        }

        // 确保 ConfigManager 实例存在
        if (ConfigManager.Instance == null)
        {
            Debug.LogError("[LoadManager] ConfigManager instance is null!");
            LocalizatioinUtils.ValidateAndApplyStartupLanguage();
            yield return 1f;
            yield break;
        }

        // 如果已经初始化，直接返回
        if (ConfigManager.Instance.IsInitialized)
        {
            Debug.Log("[LoadManager] Config tables already initialized.");
            InitializeDrawGameLevelCtrl();
            InitializeIWannerGameLevelCtrl();
            InitializeDigGameLevelCtrl();
            LocalizatioinUtils.ValidateAndApplyStartupLanguage();
            yield return 1f;
            yield break;
        }

        bool isLoading = true;
        bool loadSuccess = false;

        // 启动异步加载
        ConfigManager.Instance.InitializeAsync(() =>
        {
            isLoading = false;
            loadSuccess = ConfigManager.Instance.IsInitialized;
        });

        // 等待加载完成，并报告进度
        while (isLoading)
        {
            float progress = ConfigManager.Instance.LoadProgress;
            yield return progress;
        }

        // 加载完成
        if (loadSuccess)
        {
            Debug.Log("[LoadManager] Config tables loaded successfully.");
            InitializeDrawGameLevelCtrl();
            InitializeIWannerGameLevelCtrl();
            InitializeDigGameLevelCtrl();
            yield return 1f;
        }
        else
        {
            Debug.LogError("[LoadManager] Failed to load config tables!");
            yield return 1f;
        }

        LocalizatioinUtils.ValidateAndApplyStartupLanguage();
    }

    /// <summary>
    /// 在 RemoteConfig 与 Luban 表均可用后初始化一次 DrawGame 关卡序列。
    /// 初始化失败不阻断主线启动；Draw 入口打开时会通过控制器 IsReady 拒绝使用半初始化数据。
    /// </summary>
    private void InitializeDrawGameLevelCtrl()
    {
        DrawGameLevelCtrl levelCtrl = GameManager.Instance?.DrawGameLevel;
        if (levelCtrl == null)
        {
            this.LogError("[LoadManager] DrawGameLevelCtrl is unavailable.");
            return;
        }

        if (!levelCtrl.Init(out string error))
            this.LogError($"[LoadManager] DrawGameLevelCtrl initialization failed. error:{error}");
    }

    /// <summary>
    /// 在 RemoteConfig 与 Luban 表均可用后初始化一次 IWanner Group/小关查询数据。
    /// 初始化失败不阻断 Main 或 Draw 启动；IWannerGameEnter 会通过 IsReady 拒绝半初始化入口。
    /// </summary>
    private void InitializeIWannerGameLevelCtrl()
    {
        IWannerGameLevelCtrl levelCtrl = GameManager.Instance?.IWannerGameLevel;
        if (levelCtrl == null)
        {
            this.LogError("[LoadManager] IWannerGameLevelCtrl is unavailable.");
            return;
        }

        if (!levelCtrl.Init(out string error))
        {
            this.LogError(
                $"[LoadManager] IWannerGameLevelCtrl initialization failed. error:{error}");
        }
    }

    /// <summary>本地 Luban 初始化后接入 Dig；不改变既有 RemoteConfig/Draw 初始化顺序。</summary>
    private void InitializeDigGameLevelCtrl()
    {
        var levelCtrl = GameManager.Instance?.DigGameLevel;
        if (levelCtrl == null) { this.LogError("[LoadManager] DigGameLevelCtrl is unavailable."); return; }
        if (!levelCtrl.Init(out string error)) this.LogError($"[LoadManager] Dig 初始化失败：{error}");
    }

    // 最后处理模块
    private IEnumerator<float> OnPostProcess()
    {
        InitUIManager();

        // 初始化游戏设置
        // GameManager.Instance.Init();
        AudioManager.Init();

        // 内购配置初始化
        GameManager.Instance.Purchase.InitConfig();

        // 打点占位
        GameSDKService.Instance.UpdateBPlay();
        GameSDKService.Instance.UpdateBLevel();

        // 进入游戏
        if (!GameManager.Instance.ReadyToPlay(out string readyError))
        {
            StopForCriticalInitializationFailure(readyError);
            yield break;
        }

        // Android 启动时同步平台资产；iOS 仅在商店手动恢复后同步。
#if UNITY_ANDROID
        GameManager.Instance.Purchase.SyncRestore();
#endif

        // 成就系统检测
        GameAchievementUtils.Init();

        // 在新版本标记落盘前缓存公告资格，弹窗序列在其它收尾逻辑完成后再启动
        bool shouldShowVersionBoard = ShouldShowVersionBoard();

        // 补录历史已通关关卡的胜利星与对应 Bag Star
        GameManager.Instance?.Postcard.TryGetAllCompleteLevelStar();

        InitGameReward();
        // 修改记录
        GameManager.Instance.NewVersionProcessFinished();
        // 初始化 红点系统
        InitReddot();

        // 更新公告、旧数据转换和启动礼盒按关闭边界串行展示
        StartStartupPopupSequence(shouldShowVersionBoard);

        yield return 1f;
    }

    private void StopForCriticalInitializationFailure(string error)
    {
        _hasCriticalInitializationFailure = true;
        this.LogError($"[LoadManager] Game initialization failed. error:{error}");

        if (processTxt == null)
            return;

        processTxt.color = Color.red;
        processTxt.text = "[LoadManager] Game initialization failed!";
    }

    /// <summary>
    /// 初始化 红点系统
    /// </summary>
    private void InitReddot()
    {
        RedDotRegisterHub.RegisterAndRefresh(new PostcardRedDotRegister());
        RedDotRegisterHub.RegisterAndRefresh(new NewSkinRedDotRegister());
        RedDotRegisterHub.RegisterAndRefresh(new DailyGiftCoinRedDotRegister());
    }

    #region Startup Popup Sequence

    /// <summary>
    /// 组装并启动一次性启动弹窗序列。
    /// 注意：序列会跨越 Loading -> Main 场景切换，因此取消令牌必须绑定常驻 UIManager。
    /// </summary>
    private static void StartStartupPopupSequence(bool shouldShowVersionBoard)
    {
        var sequence = new StartupPopupSequence();
        sequence.EnqueuePopup(
            nameof(BulletinBoardPopup),
            onClosed => TryToShowVersionBoard(shouldShowVersionBoard, onClosed));
        sequence.EnqueuePopup(nameof(TransformPopup), TryToShowTransformPopup);
        sequence.Enqueue(nameof(ChestGiftPopup), TryToShowStartupChestGiftAsync);
        sequence.Enqueue(nameof(OneLinkManager), cancellationToken =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            OneLinkManager.Instance.NotifyStartupPopupsDispatched();
            return UniTask.CompletedTask;
        });

        CancellationToken cancellationToken = UIManager.Instance.GetCancellationTokenOnDestroy();
        sequence.RunAsync(cancellationToken).SuppressCancellationThrow().Forget();
    }

    /// <summary>在新版本标记落盘前读取当前启动是否需要展示更新公告。</summary>
    private static bool ShouldShowVersionBoard()
    {
        return ConfigManager.RemoteConfig.GetVersionBoardEnabled() && DB.GameSetting.IsNewVersion;
    }

    /// <summary>在序列轮到该步骤时，使用收尾前缓存的资格尝试展示更新公告。</summary>
    private static bool TryToShowVersionBoard(bool shouldShow, Action onClosed)
    {
        if (!shouldShow)
            return false;

        return BulletinBoardPopup.Create(onClosed) != null;
    }

    /// <summary>在公告关闭或跳过后，尝试展示旧 Tips 转金币弹窗。</summary>
    private static bool TryToShowTransformPopup(Action onClosed)
    {
        return TransformPopup.HasTransformableTips() &&
               TransformPopup.Create(onClosed) != null;
    }

    /// <summary>
    /// 在前两个启动弹窗完成后等待 StartPanel 首次进场动画；OneLink 有待处理跳转时跳过 ChestGift，
    /// 否则按 GiftManager 当前资格尝试打开。
    /// </summary>
    private static async UniTask TryToShowStartupChestGiftAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        StartPanel startPanel = UIManager.Instance.GetUI<StartPanel>();
        if (startPanel == null)
        {
            BearLogger.LogWarning("[LoadManager] Startup ChestGift skipped because StartPanel is unavailable.");
            return;
        }

        await startPanel.WaitForEnterAnimationAsync(cancellationToken);
        if (startPanel == null || !startPanel.IsOpen)
            return;

        if (OneLinkManager.Instance.HasPendingRequest)
        {
            BearLogger.Log(
                "[LoadManager] Startup ChestGift skipped because OneLink navigation is pending.",
                typeof(LoadManager));
            return;
        }

        GiftManager giftManager = GiftManager.Instance;
        if (giftManager.IsEntryAvailable(GiftPopupType.ChestGift))
        {
            if (giftManager.OpenStartupOnce(GiftPopupType.ChestGift) is ChestGiftPopup popup)
                popup.ConfigureStartupCloseTarget(startPanel.ChestGiftReturnTarget);
        }
    }

    #endregion

    /// <summary>
    /// 初次登陆游戏时候的奖励
    /// </summary>
    private void InitGameReward()
    {
        if (DB.GameData.PlayCount == 1)
        {
            // 只会执行一次
            if (GameManager.Instance.Bag.GetToolCount(Config.Game.GameProps.Coin) < 0)
                GameManager.Instance.Bag.SetToolCount(Config.Game.GameProps.Coin, ConfigManager.RemoteConfig.GetInitGameCoinReward());
        }
        else
        {
            // 老玩家不给金币，但是要保证数据是对的
            if (GameManager.Instance.Bag.GetToolCount(Config.Game.GameProps.Coin) < 0)
                GameManager.Instance.Bag.SetToolCount(Config.Game.GameProps.Coin, 0);
        }
    }

    /// <summary>
    /// 尝试自动登录游戏账号（带异常捕获和 15s 超时保护）
    /// </summary>
    private IEnumerator<float> TryAutoLoginGameAccount()
    {
        Debug.Log("[LoadManager] Try auto login with game...");
        bool loginDone = false;
        bool loginResult = false;
        Exception loginException = null;

        async UniTask LoginAsync()
        {
            try
            {
                loginResult = await GameAccountUtils.TryAutoLoginGame();
                // 登陆成功尝试获取成就
                // if (loginResult)
                //     await GameAchievementUtils.LoadAchievementCacheAsync();
            }
            catch (Exception ex)
            {
                loginException = ex;
                Debug.LogException(ex);
            }
            finally
            {
                loginDone = true;
            }
        }
        LoginAsync().Forget();

        // 增加 15 秒超时保护，避免异常或 SDK 挂起导致无限等待
        float loginWaitTimer = 0f;
        while (!loginDone && loginWaitTimer < 15f)
        {
            loginWaitTimer += Time.deltaTime;
            yield return 1f; // 保持进度条在 100%，等待登录完成
        }

        if (!loginDone)
        {
            Debug.LogError("[LoadManager] Auto login timeout (15s), skip to next step.");
        }
        else if (loginException != null)
        {
            Debug.LogError($"[LoadManager] Auto login exception, skip to next step. Exception: {loginException.Message}");
        }
        else
        {
            Debug.Log($"[LoadManager] Auto login result: {loginResult}");
        }
    }

    private void InitUIManager()
    {
        UIManager.Instance.Initialize();
        // YooAssetUILoader yooAssetLoader = new YooAssetUILoader();
        ResourcesUILoader newLoader = new ResourcesUILoader("");
        // UIManager.Instance.RegisterLoader(yooAssetLoader, 4);
        UIManager.Instance.RegisterLoader(newLoader, 5);
        ResourceSystemInitializer.Initialize();

        ObjectPoolManager.Instance.RegisterPool<SystemTips>(SystemTips.Create, 2, 5);
    }

    // 添加函数到队列
    public void EnqueueFunction(Func<IEnumerator<float>> function)
    {
        functionQueue.Enqueue(function);

        // 如果当前没有在执行，开始执行队列
        if (currentCoroutine == null)
        {
            currentCoroutine = StartCoroutine(ProcessQueue());
        }
    }

    // 处理队列中的函数
    private IEnumerator ProcessQueue()
    {
        while (functionQueue.Count > 0)
        {
            Func<IEnumerator<float>> function = functionQueue.Dequeue();
            IEnumerator<float> coroutine = function();
            ProcessFill.fillAmount = 0;

            // 执行协程并获取返回值
            while (coroutine.MoveNext())
            {
                float value = coroutine.Current;

                // 更新进度条
                if (ProcessFill != null)
                {
                    _processFillTweener?.Kill();
                    if (ProcessFill.fillAmount < value)
                    {
                        _processFillTweener = DOTween.To(() => ProcessFill.fillAmount, d =>
                        {
                            if (ProcessFill != null)
                                ProcessFill.fillAmount = d;
                        }, value, Mathf.Max(0.02f, value - ProcessFill.fillAmount)).SetTarget(ProcessFill);
                    }
                    else
                    {
                        ProcessFill.fillAmount = value;
                        _processFillTweener = null;
                    }
                }

                yield return null;
            }

            if (_hasCriticalInitializationFailure)
            {
                functionQueue.Clear();
                break;
            }
        }

        currentCoroutine = null;
        if (_hasCriticalInitializationFailure)
        {
            this.LogError("[LoadManager] Main scene loading aborted because game initialization failed.");
            yield break;
        }

        LoadSceneAsync("Main");
    }

    // 异步加载场景（推荐）
    public void LoadSceneAsync(string sceneName)
    {
        StartCoroutine(LoadSceneCoroutine(sceneName));
    }

    private IEnumerator LoadSceneCoroutine(string sceneName)
    {
        /*         while(!isPlayCompleted)
                    yield return null;
                 */
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
        asyncLoad.allowSceneActivation = false;

        // 等待场景加载完成
        while (!asyncLoad.isDone)
        {
            // 获取加载进度值（0-0.9 映射到 0-1）
            float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);

            // 更新进度条
            if (ProcessFill != null)
            {
                ProcessFill.fillAmount = progress;
            }

            // 当进度达到 0.9 时，允许场景激活
            if (asyncLoad.progress >= 0.9f)
            {
                asyncLoad.allowSceneActivation = true;
            }

            yield return null;
        }
    }
}
