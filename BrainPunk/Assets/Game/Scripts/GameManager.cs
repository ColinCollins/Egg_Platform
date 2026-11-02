using Bear.Logger;
using Bear.UI;
using Game.Common;
using Game.Play;
using GameCommon;
using UnityEngine;

/// <summary>
/// 1.5.0 打算加入金币系统，原有的 tipsTool 按照比例切换成 coin
/// </summary>
public class GameManager : MonoSingleton<GameManager>, IDebuger
{
    [SerializeField] private Camera StartCamera;

    public PlayCtrl @PlayCtrl { get; private set; }

    /// <summary>
    /// 玩法控制器管理入口。
    /// 由 GameManager 持有，负责 Policy 解析和具体玩法控制器生命周期。
    /// </summary>
    public PlayControllerManager ControllerManager { get; private set; }
    private DrawPlayCtrl _drawPlayCtrl;
    private IWannerPlayCtrl _iwannerPlayCtrl;
    private DigPlayCtrl _digPlayCtrl;

    /// <summary>Dig 独立本地/远端排序与进度入口，在配置完成后初始化。</summary>
    public DigGameLevelCtrl DigGameLevel { get; private set; }

    /// <summary>
    /// DrawGame 关卡配置控制器；由 GameManager 持有，并由 LoadManager 在配置加载完成后初始化一次。
    /// </summary>
    public DrawGameLevelCtrl DrawGameLevel { get; private set; }

    /// <summary>Draw 广告、暂停或转场尚未结束时延后远端提交，避免切换旧回调的关卡映射。</summary>
    internal bool IsDrawRemoteConfigRefreshBlocked => _drawPlayCtrl?.IsRemoteConfigRefreshBlocked == true;

    /// <summary>仅供云控导出日志读取现有 Draw 门禁状态，不创建控制器或改变门禁。</summary>
    internal string DrawRemoteConfigRefreshState => _drawPlayCtrl?.RemoteConfigRefreshState ?? "controller=null";

    /// <summary>有效 Draw 配置提交后刷新已有界面；只使用已初始化 UI，不创建面板或重开当前关。</summary>
    internal void RefreshDrawRemoteConfigViews()
    {
        if (!Application.isPlaying) return;
        if (ControllerManager == null)
        {
            Guru.SDK.Framework.Utils.Log.Log.I("[DrawRemoteConfigUI] stage=dispatch_skipped, reason=controller_manager_not_ready.");
            return;
        }
        _drawPlayCtrl?.RefreshRemoteConfigViews();
        var startPanel = UIManager.Instance.GetUI<StartPanel>(nameof(StartPanel));
        Guru.SDK.Framework.Utils.Log.Log.I($"[DrawRemoteConfigUI] stage=dispatch, drawControllerPresent={_drawPlayCtrl != null}, startPanelPresent={startPanel != null}, generation={DrawGameLevel?.ConfigGeneration ?? 0}.");
        startPanel?.RefreshDrawRemoteConfig();
    }

    /// <summary>Dig 广告、暂停或转场尚未结束时延后远端提交，避免切换旧回调的关卡映射。</summary>
    internal bool IsDigRemoteConfigRefreshBlocked => _digPlayCtrl?.IsRemoteConfigRefreshBlocked == true;

    /// <summary>仅供云控导出日志读取现有 Dig 门禁状态，不创建控制器或改变门禁。</summary>
    internal string DigRemoteConfigRefreshState => _digPlayCtrl?.RemoteConfigRefreshState ?? "controller=null";

    /// <summary>有效 Dig 配置提交后刷新已有界面；只使用已初始化 UI，不创建面板或重开当前关。</summary>
    internal void RefreshDigRemoteConfigViews()
    {
        if (!Application.isPlaying) return;
        if (ControllerManager == null)
        {
            Guru.SDK.Framework.Utils.Log.Log.I("[DigRemoteConfigUI] stage=dispatch_skipped, reason=controller_manager_not_ready.");
            return;
        }
        _digPlayCtrl?.RefreshRemoteConfigViews();
        var startPanel = UIManager.Instance.GetUI<StartPanel>(nameof(StartPanel));
        Guru.SDK.Framework.Utils.Log.Log.I($"[DigRemoteConfigUI] stage=dispatch, digControllerPresent={_digPlayCtrl != null}, startPanelPresent={startPanel != null}, generation={DigGameLevel?.ConfigGeneration ?? 0}.");
        startPanel?.RefreshDigRemoteConfig();
    }

    /// <summary>
    /// IWanner 关卡配置解析控制器；由 GameManager 持有，并由 Loading 在配置加载完成后初始化。
    /// </summary>
    public IWannerGameLevelCtrl IWannerGameLevel { get; private set; }

    /// <summary>
    /// 全玩法共享的玩家背包服务。
    /// 仅在 DB 与配置加载完成后的 ReadyToPlay 阶段创建，玩法控制器不得自行替换实例。
    /// </summary>
    public SimpleBag Bag { get; private set; }

    public PurchaseManager Purchase { get; private set; }


    #region Activity
    // 控制中心
    public PostcardManager Postcard {get; private set;}

    /// <summary>
    /// 每日挑战数据中心；由 GameManager 持有，UI 只能通过其公开快照接口读取状态。
    /// </summary>
    public DailyChallengeManager DailyChallenge { get; private set; }

    #endregion


    // Start is called before the first frame update
    private void Awake()
    {
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        DontDestroyOnLoad(gameObject);
    }

    public void Init()
    {
        Purchase = new PurchaseManager();

        DrawGameLevel ??= new DrawGameLevelCtrl();
        IWannerGameLevel ??= new IWannerGameLevelCtrl();
        DigGameLevel ??= new DigGameLevelCtrl();

        Postcard = new PostcardManager();
        DailyChallenge = new DailyChallengeManager();
    }

    /// <summary>
    /// 初始化并进入默认主线玩法。
    /// 调用边界：DB、配置和 UI 系统完成初始化后由 Loading 流程调用。
    /// 返回 false 时调用方必须停止进入 Main 场景，避免使用半初始化控制器。
    /// </summary>
    /// <param name="error">失败原因；成功时为空。</param>
    /// <returns>主线控制器完成初始化并激活时返回 true。</returns>
    public bool ReadyToPlay(out string error)
    {
        error = null;

        try
        {
            Bag ??= new SimpleBag();
            var playController = PlayCtrl.Instance;

            if (ControllerManager == null)
            {
                var policyResolver = new PlayControllerPolicyResolver();
                policyResolver.Register(new MainPlayControllerPolicy());
                policyResolver.Register(new DrawPlayControllerPolicy());
                policyResolver.Register(new IWannerPlayControllerPolicy());
                policyResolver.Register(new DigPlayControllerPolicy());
                ControllerManager = new PlayControllerManager(
                    new PlayControllerContext(Bag),
                    policyResolver);
            }

            _drawPlayCtrl ??= new DrawPlayCtrl();
            _iwannerPlayCtrl ??= new IWannerPlayCtrl();
            _digPlayCtrl ??= new DigPlayCtrl();

            if (!ControllerManager.Register(playController, out string registerError))
            {
                error = $"Register main PlayCtrl failed. error:{registerError}";
                this.LogError($"[GameManager] {error}");
                return false;
            }

            if (!ControllerManager.Register(_drawPlayCtrl, out string drawRegisterError))
            {
                error = $"Register DrawPlayCtrl failed. error:{drawRegisterError}";
                this.LogError($"[GameManager] {error}");
                return false;
            }

            if (!ControllerManager.Register(_iwannerPlayCtrl, out string iwannerRegisterError))
            {
                error = $"Register IWannerPlayCtrl failed. error:{iwannerRegisterError}";
                this.LogError($"[GameManager] {error}");
                return false;
            }

            if (!ControllerManager.Register(_digPlayCtrl, out string digRegisterError))
            {
                error = $"Register DigPlayCtrl failed. error:{digRegisterError}";
                this.LogError($"[GameManager] {error}");
                return false;
            }

            var request = new PlayEntryRequest(PlayModeType.Main);
            if (!ControllerManager.TryEnter(request, out string enterError))
            {
                error = $"Enter main PlayCtrl failed. error:{enterError}";
                this.LogError($"[GameManager] {error}");
                return false;
            }

            @PlayCtrl = playController;
            return true;
        }
        catch (System.Exception exception)
        {
            error = $"ReadyToPlay exception: {exception.Message}";
            this.LogError($"[GameManager] {error}");
            return false;
        }
    }

    /// <summary>
    /// 请求进入画线玩法并打开 Draw 专属选关界面。
    /// 调用边界：供 StartPanel、Draw 关内退出恢复选关或 Level_999 关内入口调用。
    /// </summary>
    /// <param name="error">切换失败原因；成功时为空。</param>
    /// <returns>ControllerManager 已切换到 DrawPlayCtrl 时返回 true。</returns>
    public bool TryEnterDraw(out string error)
    {
        if (ControllerManager == null)
        {
            error = "PlayControllerManager is not ready.";
            this.LogError($"[GameManager] Enter DrawPlayCtrl failed. error:{error}");
            return false;
        }

        var request = new PlayEntryRequest(PlayModeType.Draw);
        if (ControllerManager.TryEnter(request, out error))
            return true;

        this.LogError($"[GameManager] Enter DrawPlayCtrl failed. error:{error}");
        return false;
    }

    /// <summary>
    /// 临时进入 IWanner 配置排序第一行 Group 的第一个小关。
    /// 调用边界：仅供 IWannerGameEnter 使用；未来选组界面接入后应由选组入口提供目标 Group。
    /// 注意事项：配置或同步请求失败时会恢复主线控制器，不保留半激活 IWanner 状态。
    /// </summary>
    /// <param name="error">切换或首个 Group 解析失败原因；成功时为空。</param>
    /// <returns>IWanner 控制器已接受首个 Group 加载请求时返回 true。</returns>
    public bool TryEnterIWannerFirstGroup(out string error)
    {
        if (ControllerManager == null)
        {
            error = "PlayControllerManager is not ready.";
            this.LogError($"[GameManager] Enter IWanner failed. error:{error}");
            return false;
        }

        if (IWannerGameLevel == null || !IWannerGameLevel.IsReady)
        {
            error = "IWannerGameLevelCtrl is not ready.";
            this.LogError($"[GameManager] Enter IWanner failed. error:{error}");
            return false;
        }

        var request = new PlayEntryRequest(PlayModeType.IWanner);
        if (!ControllerManager.TryEnter(request, out error))
        {
            this.LogError($"[GameManager] Enter IWannerPlayCtrl failed. error:{error}");
            return false;
        }

        if (_iwannerPlayCtrl != null && _iwannerPlayCtrl.TryEnterFirstGroup(out error))
            return true;

        string enterError = error ?? "IWannerPlayCtrl is unavailable.";
        var rollbackRequest = new PlayEntryRequest(PlayModeType.Main);
        if (!ControllerManager.TryEnter(rollbackRequest, out string rollbackError))
        {
            error = $"{enterError} Rollback to Main failed. error:{rollbackError}";
        }
        else
        {
            error = enterError;
        }

        this.LogError($"[GameManager] Enter first IWanner Group failed. error:{error}");
        return false;
    }

    /// <summary>
    /// 按 IWannerLevelSort.Id 进入指定 Group；默认沿用存档续关，也支持 OneLink 指定组内小关。
    /// 调用边界：供 IWanner 选组弹窗和首页 OneLink 入口提交已解析目标。
    /// 注意事项：配置或同步请求失败时会恢复主线控制器，不保留半激活 IWanner 状态。
    /// </summary>
    /// <param name="groupId">目标 IWanner Group 的 LevelSort Id。</param>
    /// <param name="error">切换或 Group 解析失败原因；成功时为空。</param>
    /// <param name="levelIndex">OneLink 指定的零起始组内索引；省略时保留普通选组的续关规则。</param>
    /// <returns>IWanner 控制器已接受目标 Group 加载请求时返回 true。</returns>
    public bool TryEnterIWannerGroup(int groupId, out string error, int? levelIndex = null)
    {
        if (groupId <= 0)
        {
            error = $"IWanner Group id is invalid: {groupId}.";
            this.LogError($"[GameManager] Enter IWanner failed. error:{error}");
            return false;
        }

        if (ControllerManager == null)
        {
            error = "PlayControllerManager is not ready.";
            this.LogError($"[GameManager] Enter IWanner failed. error:{error}");
            return false;
        }

        if (IWannerGameLevel == null || !IWannerGameLevel.IsReady)
        {
            error = "IWannerGameLevelCtrl is not ready.";
            this.LogError($"[GameManager] Enter IWanner failed. error:{error}");
            return false;
        }

        var request = new PlayEntryRequest(PlayModeType.IWanner);
        if (!ControllerManager.TryEnter(request, out error))
        {
            this.LogError($"[GameManager] Enter IWannerPlayCtrl failed. error:{error}");
            return false;
        }

        if (_iwannerPlayCtrl != null && _iwannerPlayCtrl.TryEnterGroup(groupId, out error, levelIndex))
            return true;

        string enterError = error ?? "IWannerPlayCtrl is unavailable.";
        var rollbackRequest = new PlayEntryRequest(PlayModeType.Main);
        if (!ControllerManager.TryEnter(rollbackRequest, out string rollbackError))
        {
            error = $"{enterError} Rollback to Main failed. error:{rollbackError}";
        }
        else
        {
            error = enterError;
        }

        this.LogError($"[GameManager] Enter IWanner Group failed. groupId:{groupId}, error:{error}");
        return false;
    }

    /// <summary>
    /// 进入 IWanner LevelSort 之外的 TBN 占位关 ILevel_999。
    /// 调用边界：供 IWanner 选组弹窗与首页 OneLink 使用。
    /// 注意事项：普通入口校验最后一组已通关；OneLink 可放行本次入口，失败时仍恢复主线控制器。
    /// </summary>
    /// <param name="error">切换、解锁校验或 TBN 加载请求失败原因；成功时为空。</param>
    /// <param name="allowLockedEntry">仅 OneLink 直达 ILevel_999 时传 true；不写入 Group 通关记录。</param>
    /// <returns>IWanner 控制器已接受 ILevel_999 加载请求时返回 true。</returns>
    public bool TryEnterIWannerTbn(out string error, bool allowLockedEntry = false)
    {
        if (ControllerManager == null)
        {
            error = "PlayControllerManager is not ready.";
            this.LogError($"[GameManager] Enter IWanner TBN failed. error:{error}");
            return false;
        }

        if (IWannerGameLevel == null || !IWannerGameLevel.IsReady)
        {
            error = "IWannerGameLevelCtrl is not ready.";
            this.LogError($"[GameManager] Enter IWanner TBN failed. error:{error}");
            return false;
        }

        var request = new PlayEntryRequest(PlayModeType.IWanner);
        if (!ControllerManager.TryEnter(request, out error))
        {
            this.LogError($"[GameManager] Enter IWannerPlayCtrl failed. error:{error}");
            return false;
        }

        if (_iwannerPlayCtrl != null && _iwannerPlayCtrl.TryEnterTbn(out error, allowLockedEntry))
            return true;

        string enterError = error ?? "IWannerPlayCtrl is unavailable.";
        var rollbackRequest = new PlayEntryRequest(PlayModeType.Main);
        if (!ControllerManager.TryEnter(rollbackRequest, out string rollbackError))
            error = $"{enterError} Rollback to Main failed. error:{rollbackError}";
        else
            error = enterError;

        this.LogError($"[GameManager] Enter IWanner TBN failed. error:{error}");
        return false;
    }

    /// <summary>
    /// 根据 DrawLevelSort 与 DrawLevelData 请求当前 DrawPlayCtrl 进入指定画线关卡。
    /// 调用边界：仅由 Draw 选关面板在 DrawPlayCtrl 已激活时调用。
    /// </summary>
    /// <param name="sortData">Draw 排序数据，Id 用于 HUD 与独立进度。</param>
    /// <param name="levelData">由 sortData.Path 索引得到的 Draw 关卡数据，Path 用于资源加载。</param>
    /// <param name="error">配置或切换失败原因；成功时为空。</param>
    /// <returns>目标关卡请求已由活动的 DrawPlayCtrl 接受时返回 true。</returns>
    public bool TryEnterDrawLevel(
        Config.DrawLevelSort sortData,
        Config.DrawLevelData levelData,
        out string error)
    {
        if (_drawPlayCtrl == null)
        {
            error = "DrawPlayCtrl is not registered.";
            this.LogError($"[GameManager] Enter selected Draw level failed. error:{error}");
            return false;
        }

        if (sortData == null || levelData == null)
        {
            error = "DrawLevelSort or DrawLevelData is null.";
            this.LogError($"[GameManager] Enter selected Draw level failed. error:{error}");
            return false;
        }

        if (sortData.Path != levelData.Id)
        {
            error =
                $"Draw config mapping mismatch. sortId:{sortData.Id}, sortPath:{sortData.Path}, dataId:{levelData.Id}.";
            this.LogError($"[GameManager] Enter selected Draw level failed. error:{error}");
            return false;
        }

        if (ControllerManager == null ||
            !ReferenceEquals(ControllerManager.ActiveController, _drawPlayCtrl))
        {
            error = "DrawPlayCtrl is not the active play controller.";
            this.LogError($"[GameManager] Enter selected Draw level failed. error:{error}");
            return false;
        }

        if (!_drawPlayCtrl.TryEnterLevel(sortData.Id, levelData.Path, out error))
        {
            this.LogError($"[GameManager] Enter selected Draw level failed. error:{error}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// 从其它玩法直接进入序列最小的未通关主线关卡；全部通关时进入 Level_999。
    /// 调用边界：仅供 DrawGame / IWannerGame 的 TBC 关内传送门调用。
    /// 注意事项：玩法切换会先停用并清理当前控制器，再复用主线既有进关、打点和加载链；
    /// 主线配置无效时恢复 StartPanel，避免停留在无可用界面的主线状态。
    /// </summary>
    /// <param name="error">玩法切换、主线配置或进关请求失败原因；成功时为空。</param>
    /// <returns>主线控制器已激活且目标主线关卡请求已被接受时返回 true。</returns>
    public bool TryEnterMainLevel(out string error)
    {
        if (ControllerManager == null)
        {
            error = "PlayControllerManager is not ready.";
            this.LogError($"[GameManager] Enter target main level failed. error:{error}");
            return false;
        }

        if (@PlayCtrl == null)
        {
            error = "PlayCtrl is not registered.";
            this.LogError($"[GameManager] Enter target main level failed. error:{error}");
            return false;
        }

        var request = new PlayEntryRequest(PlayModeType.Main);
        if (!ControllerManager.TryEnter(request, out error))
        {
            this.LogError($"[GameManager] Enter main PlayCtrl failed. error:{error}");
            return false;
        }

        if (@PlayCtrl.TryEnterLowestUnpassedOrLastLevel(out error))
            return true;

        string enterError = error ?? "Lowest unpassed or Level_999 main level request was rejected.";
        if (!@PlayCtrl.TryRestoreStartPanel(out string fallbackError))
            error = $"{enterError} Restore StartPanel failed. error:{fallbackError}";
        else
            error = enterError;

        this.LogError($"[GameManager] Enter target main level failed. error:{error}");
        return false;
    }

    /// <summary>
    /// 从保留 StartPanel 作为背景的 Draw 选关阶段取消返回主界面。
    /// 调用边界：仅供 DrawGame_ChoiceLevelPanel 的关闭按钮调用；关内 Menu、加载失败等完整退出仍使用 ReturnToStartPanel。
    /// 注意事项：只有当前活动控制器为 Draw 且既有 StartPanel 仍打开、可见并处于激活层级时才复用；否则回退完整恢复流程。
    /// </summary>
    /// <param name="error">控制器切换或 StartPanel 恢复失败原因；成功时为空。</param>
    /// <returns>主线控制器已激活且 StartPanel 可继续使用时返回 true。</returns>
    public bool TryExitDrawLevelSelection(out string error)
    {
        if (ControllerManager == null)
        {
            error = "PlayControllerManager is not ready.";
            this.LogError($"[GameManager] Exit Draw level selection failed. error:{error}");
            return false;
        }

        if (_drawPlayCtrl == null ||
            !ReferenceEquals(ControllerManager.ActiveController, _drawPlayCtrl))
        {
            error = "DrawPlayCtrl is not the active play controller.";
            this.LogError($"[GameManager] Exit Draw level selection failed. error:{error}");
            return false;
        }

        StartPanel retainedStartPanel = UIManager.Instance.GetUI<StartPanel>();
        bool canReuseStartPanel =
            retainedStartPanel != null &&
            retainedStartPanel.IsOpen &&
            retainedStartPanel.IsVisible &&
            retainedStartPanel.gameObject.activeInHierarchy;
        if (!canReuseStartPanel)
            return ReturnToStartPanel(out error);

        var request = new PlayEntryRequest(PlayModeType.Main);
        if (!ControllerManager.TryEnter(request, out error))
        {
            this.LogError($"[GameManager] Exit Draw level selection failed. error:{error}");
            return false;
        }

        OpenCamera();
        if (retainedStartPanel != null &&
            retainedStartPanel.IsOpen &&
            retainedStartPanel.IsVisible &&
            retainedStartPanel.gameObject.activeInHierarchy)
        {
            Game.OneLinkManager.Instance.NotifyHomeOpened();
            error = null;
            return true;
        }

        if (@PlayCtrl != null && @PlayCtrl.TryRestoreStartPanel(out error))
            return true;

        error ??= "Retained StartPanel became unavailable after switching to Main.";
        this.LogError($"[GameManager] Exit Draw level selection failed. error:{error}");
        return false;
    }

    /// <summary>
    /// 从保留 StartPanel 作为背景的 Dig 选关阶段取消返回主界面。
    /// 调用边界：仅供 DigGame_ChoiceLevelPanel 的关闭按钮调用；关内 Menu、加载失败等完整退出仍使用 ReturnToStartPanel。
    /// 注意事项：只有当前活动控制器为 Dig 且既有 StartPanel 仍打开、可见并处于激活层级时才复用；否则回退完整恢复流程。
    /// </summary>
    /// <param name="error">控制器切换或 StartPanel 恢复失败原因；成功时为空。</param>
    /// <returns>主线控制器已激活且 StartPanel 可继续使用时返回 true。</returns>
    public bool TryExitDigLevelSelection(out string error)
    {
        if (ControllerManager == null)
        {
            error = "PlayControllerManager is not ready.";
            this.LogError($"[GameManager] Exit Dig level selection failed. error:{error}");
            return false;
        }

        if (_digPlayCtrl == null ||
            !ReferenceEquals(ControllerManager.ActiveController, _digPlayCtrl))
        {
            error = "DigPlayCtrl is not the active play controller.";
            this.LogError($"[GameManager] Exit Dig level selection failed. error:{error}");
            return false;
        }

        StartPanel retainedStartPanel = UIManager.Instance.GetUI<StartPanel>();
        bool canReuseStartPanel =
            retainedStartPanel != null &&
            retainedStartPanel.IsOpen &&
            retainedStartPanel.IsVisible &&
            retainedStartPanel.gameObject.activeInHierarchy;
        if (!canReuseStartPanel)
            return ReturnToStartPanel(out error);

        var request = new PlayEntryRequest(PlayModeType.Main);
        if (!ControllerManager.TryEnter(request, out error))
        {
            this.LogError($"[GameManager] Exit Dig level selection failed. error:{error}");
            return false;
        }

        OpenCamera();
        if (retainedStartPanel != null &&
            retainedStartPanel.IsOpen &&
            retainedStartPanel.IsVisible &&
            retainedStartPanel.gameObject.activeInHierarchy)
        {
            Game.OneLinkManager.Instance.NotifyHomeOpened();
            error = null;
            return true;
        }

        if (@PlayCtrl != null && @PlayCtrl.TryRestoreStartPanel(out error))
            return true;

        error ??= "Retained StartPanel became unavailable after switching to Main.";
        this.LogError($"[GameManager] Exit Dig level selection failed. error:{error}");
        return false;
    }

    /// <summary>
    /// 退出当前玩法，重新激活主线控制器并恢复应用首次进入时的 StartPanel。
    /// 调用边界：由当前激活玩法的 Pause/Setting MenuBtn 调用。
    /// </summary>
    /// <param name="error">恢复失败原因；成功时为空。</param>
    /// <returns>主线控制器已激活且 StartPanel 已打开时返回 true。</returns>
    public bool ReturnToStartPanel(out string error)
    {
        if (ControllerManager == null)
        {
            error = "PlayControllerManager is not ready.";
            this.LogError($"[GameManager] Return to StartPanel failed. error:{error}");
            return false;
        }

        UIManager.Instance.CloseAllUI(UILayer.Popup);

        var request = new PlayEntryRequest(PlayModeType.Main);
        if (!ControllerManager.TryEnter(request, out error))
        {
            this.LogError($"[GameManager] Return to StartPanel failed. error:{error}");
            return false;
        }

        if (@PlayCtrl == null || !@PlayCtrl.TryRestoreStartPanel(out error))
        {
            this.LogError($"[GameManager] Return to StartPanel failed. error:{error}");
            return false;
        }

        return true;
    }

    /// <summary>首页进入Dig独立选关面板，沿用当前有效关卡／保存进关页定位；云控关闭时不受理。</summary>
    public bool TryEnterDig(out string error)
    {
        error = null;
        if (ControllerManager == null || DigGameLevel?.IsReady != true || !Game.ConfigModule.ConfigManager.RemoteConfig.GetDigGameIsOpen())
        { error = "Dig 未准备好或已关闭。"; return false; }
        if (!ControllerManager.TryEnter(new PlayEntryRequest(PlayModeType.Dig), out error)) return false;
        return true;
    }

    /// <summary>从当前 Dig 选关项进入，实际数据必须属于当前排序；运行时固定该数据身份。</summary>
    public bool TryEnterDigLevel(Config.DigLevelSort sortData, Config.DigLevelData levelData, out string error)
    {
        error = null;
        if (_digPlayCtrl == null || ControllerManager?.ActiveController != _digPlayCtrl ||
            !DigGameLevel.IsCurrentSelection(DigGameLevel.ConfigGeneration, sortData, levelData))
        { error = "Dig 选关状态或配置身份无效。"; return false; }
        return _digPlayCtrl.TryEnterLevel(sortData.Id, false, out error);
    }

    /// <summary>OneLink 使用有效排序关号激活面板，再复用其解锁、存档和正常进关链。</summary>
    /// <param name="levelId">有效排序关号，空值选择当前存档关。</param>
    /// <param name="error">同步受理失败原因。</param>
    /// <param name="allowLockedEntry">仅解析后的 OneLink 允许解锁指定目标。</param>
    public bool TryEnterDigLevel(int? levelId, out string error, bool allowLockedEntry = false)
    {
        if (!TryEnterDig(out error)) return false;
        var sort = DigGameLevel.GetLevelSortById(levelId ?? DigGameLevel.ResolveCurrentLevel());
        var data = DigGameLevel.GetLevelDataById(sort?.Path);
        var panel = UIManager.Instance.GetUI<DigGame_ChoiceLevelPanel>();
        if (sort != null && data != null && panel != null &&
            (allowLockedEntry || DB.DigGame.UnlockLevels.Contains(sort.Id)) &&
            panel.TryEnterOneLinkLevel(sort, data, out error)) return true;
        error ??= "Dig 指定选关无效或未解锁。";
        string failure = error;
        ReturnToStartPanel(out _);
        error = failure;
        return false;
    }

    public void CloseCamera()
    {
        StartCamera.gameObject.SetActive(false);
    }

    public void OpenCamera()
    {
        StartCamera.gameObject.SetActive(true);
    }

    void Update()
    {
        ScheduleSystem.Instance.Tick();
        ControllerManager?.Tick();
        SDK.Remote.RemoteConfigService.TryApplyPendingDrawConfig(this);
        SDK.Remote.RemoteConfigService.TryApplyPendingDigConfig(this);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            ScheduleSystem.Instance.CheckRuntimeBoundary();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus)
            ScheduleSystem.Instance.CheckRuntimeBoundary();
    }

    private void OnApplicationQuit()
    {
        ClearAll();
    }

    private void ClearAll()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.DestroyAllUI();
        }
    }

    protected override void OnDestroy()
    {
        Postcard?.Dispose();
        DailyChallenge?.Dispose();
        ControllerManager?.Shutdown();
        ControllerManager = null;
    }

#region New Version Process
    /// <summary>
    /// 包体更新的时候用于判断是否是新版
    /// - remoteconfig 是否清理
    /// - 是否展示更新公告
    /// </summary>
    public void RefreshIsNewVersion()
    {
        var gameData = DB.GameData;
        var gameSetting = DB.GameSetting;
        if (gameData == null || gameSetting == null)
        {
            this.LogWarning("[GameManager] RefreshIsNewVersion skipped: DB.GameData or DB.GameSetting is null.");
            return;
        }


        if(DB.GameSetting.IsNewVersion)
        {
            this.Log("[GameManager] RefreshIsNewVersion skipped: already marked as new version.");
            return;
        }   

        var currentVersion = Application.version;
        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            this.LogWarning("[GameManager] RefreshIsNewVersion skipped: Application.version is null or empty.");
            gameSetting.IsNewVersion = false;
            gameSetting.Save();
            return;
        }

        var lastAppVersion = gameData.CurrentAppVersion;
        var isNewVersion = !string.Equals(lastAppVersion, currentVersion, System.StringComparison.Ordinal);

        gameSetting.IsNewVersion = isNewVersion;
        gameSetting.Save();
    }

    public void NewVersionProcessFinished()
    {
        DB.GameSetting.IsNewVersion = false;
        DB.GameSetting.Save();
    }
    
#endregion
}
