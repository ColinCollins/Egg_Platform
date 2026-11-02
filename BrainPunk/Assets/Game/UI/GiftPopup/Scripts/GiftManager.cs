using System;
using System.Collections.Generic;
using Bear.Logger;
using Bear.UI;
using Config.LubanConfig;
using Game.Common;
using Game.ConfigModule;

/// <summary>
/// 礼物弹窗统一管理器。
/// 仅负责礼物类型路由、活动弹窗互斥、入口通知和 Schedule 刷新分发；各类礼物业务由独立控制器实现。
/// </summary>
public sealed class GiftManager : Singleton<GiftManager>, IScheduleRefreshable, IDebuger
{
    private const int DefaultDailyDurationSeconds = 24 * 60 * 60;

    #region 控制器与状态

    private readonly Dictionary<GiftPopupType, IGiftController> _controllers;
    private readonly HashSet<GiftPopupType> _startupOpenedTypes = new();
    private GiftPopup _activePopup;

    /// <summary>
    /// ChestGift 业务控制器。
    /// 调用边界：外部仅调用其 ChestGift 专属状态入口，弹窗打开仍应经由 GiftManager 以保持窗口互斥。
    /// </summary>
    public ChestGiftController ChestGift { get; }

    /// <summary>
    /// CoinGift 业务控制器。
    /// 调用边界：外部只调用其广告与购买入口，弹窗打开仍应经由 GiftManager 以保持窗口互斥。
    /// </summary>
    public CoinGiftController CoinGift { get; }

    /// <summary>
    /// 指定礼物类型的入口可用性发生变化时触发。
    /// 调用边界：仅用于 GiftPopup 局部入口刷新，不得转发为全局 PlayEvents。
    /// </summary>
    public event Action<GiftPopupType> AvailabilityChanged;

    /// <summary>
    /// 创建统一礼物管理器并注册各类型控制器。
    /// 该构造函数仅供 Singleton 创建实例，业务代码不得自行持有另一份 GiftManager。
    /// </summary>
    public GiftManager()
    {
        ChestGift = new ChestGiftController(NotifyAvailabilityChanged);
        CoinGift = new CoinGiftController(NotifyAvailabilityChanged);
        _controllers = new Dictionary<GiftPopupType, IGiftController>
        {
            [ChestGift.PopupType] = ChestGift,
            [CoinGift.PopupType] = CoinGift,
        };
    }

    #endregion

    #region 礼包配置

    /// <summary>
    /// CoinGift 首购每日倒计时配置，单位为秒。
    /// 调用边界：只供 CoinGift 首购状态显示倒计时；配置小于等于 0 时回退为 24 小时。
    /// </summary>
    public int DailyFirstPurchastGiftDuration => TryGetGlobalConst(out GlobalConst config)
        ? ResolveDailyDuration(config.DailyFirstPurchastGiftDuration)
        : DefaultDailyDurationSeconds;

    /// <summary>
    /// 是否开启 Daily ChestGift。
    /// 调用边界：同时控制入口资格、启动自动弹出与显式打开；配置尚未就绪时保持原有开启行为。
    /// </summary>
    public bool IsOpenDailyChest => ConfigManager.RemoteConfig.GetIsOpenDailyChest();

    /// <summary>
    /// 获取指定 Gift 类型当前周期内的剩余时间。
    /// ChestGift 固定计算到设备当地次日 00:00，确保显示归零与 DailyScheduleModule 换日一致；
    /// CoinGift 继续使用首购倒计时配置。本方法只负责 UI 显示，不直接推进刷新或写入存档。
    /// </summary>
    public TimeSpan GetDailyCountdown(GiftPopupType popupType)
    {
        DateTime now = DateTime.Now;
        DateTime cycleEnd = popupType == GiftPopupType.CoinGift
            ? now.Date.AddSeconds(DailyFirstPurchastGiftDuration)
            : now.Date.AddDays(1);
        TimeSpan remaining = cycleEnd - now;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    private static int ResolveDailyDuration(int configuredDuration)
    {
        return configuredDuration > 0 ? configuredDuration : DefaultDailyDurationSeconds;
    }

    private static bool TryGetGlobalConst(out GlobalConst config)
    {
        ConfigManager manager = ConfigManager.Instance;
        List<GlobalConst> values = manager != null && manager.IsInitialized
            ? manager.Tables?.TbGlobalConst?.DataList
            : null;
        if (values == null || values.Count == 0)
        {
            config = null;
            return false;
        }

        config = values[0];
        return config != null;
    }

    #endregion

    #region Daily Schedule

    /// <summary>Schedule 日志标识；由 DailyScheduleModule 注册。</summary>
    public string RefreshKey => "Gift";

    /// <summary>
    /// 将当地自然日刷新分发给所有需要 Daily 刷新的礼物控制器。
    /// 调用边界：只允许由 DailyScheduleModule 触发，不得由 GiftPopup 展示或关闭直接调用。
    /// </summary>
    public void OnScheduleRefresh()
    {
        foreach (IGiftController controller in _controllers.Values)
        {
            if (controller is not IScheduleRefreshable refreshable)
                continue;

            try
            {
                refreshable.OnScheduleRefresh();
            }
            catch (Exception exception)
            {
                this.LogError(
                    $"[GiftManager] Gift schedule refresh failed. type={controller.PopupType}, key={refreshable.RefreshKey}, exception={exception}");
            }
        }
    }

    #endregion

    #region 类型路由

    /// <summary>查询指定礼物入口当前是否可展示。</summary>
    public bool IsEntryAvailable(GiftPopupType popupType)
    {
        if (popupType == GiftPopupType.ChestGift && !IsOpenDailyChest)
            return false;

        return _controllers.TryGetValue(popupType, out IGiftController controller) &&
               controller.IsEntryAvailable();
    }

    /// <summary>
    /// 指定礼物奖励完整交付后通知对应控制器更新业务状态。
    /// 调用边界：只允许在购买记录与全部本地奖励均已更新后调用。
    /// </summary>
    public void NotifyGiftDelivered(GiftPopupType popupType)
    {
        if (_controllers.TryGetValue(popupType, out IGiftController controller))
        {
            controller.NotifyDelivered();
            return;
        }

        this.LogError($"[GiftManager] Unsupported delivered gift type: {popupType}.");
    }

    private void NotifyAvailabilityChanged(GiftPopupType popupType)
    {
        Delegate[] callbacks = AvailabilityChanged?.GetInvocationList();
        if (callbacks == null)
            return;

        foreach (Delegate callback in callbacks)
        {
            try
            {
                ((Action<GiftPopupType>)callback).Invoke(popupType);
            }
            catch (Exception exception)
            {
                this.LogError($"[GiftManager] Entry refresh callback failed. type={popupType}, exception={exception}");
            }
        }
    }

    #endregion

    #region 弹窗控制

    /// <summary>按礼物类型打开由对应控制器选出的当前档位弹窗。</summary>
    public GiftPopup Open(GiftPopupType popupType)
    {
        return OpenInternal(popupType, null);
    }

    /// <summary>
    /// 按礼物类型和类型内档位打开对应弹窗。
    /// 同一时刻只允许一个 GiftPopup；非法类型或档位直接拒绝。
    /// </summary>
    public GiftPopup Open(GiftPopupType popupType, int variantIndex)
    {
        return OpenInternal(popupType, variantIndex);
    }

    /// <summary>
    /// 在当前应用会话内自动打开指定类型的礼物弹窗一次。
    /// 调用边界：只供 Loading 启动弹窗序列使用；普通界面入口必须继续调用 Open。
    /// 注意事项：只在弹窗真实创建成功后记录本会话已自动展示；资格不足或打开失败不消耗机会。
    /// </summary>
    /// <param name="popupType">需要由启动序列自动打开的礼物类型。</param>
    /// <returns>真实打开的礼物弹窗；已自动展示、资格不足或打开失败时返回 null。</returns>
    public GiftPopup OpenStartupOnce(GiftPopupType popupType)
    {
        if (_startupOpenedTypes.Contains(popupType))
        {
            this.LogWarning($"[GiftManager] Startup auto-open ignored because type was already shown. type={popupType}.");
            return null;
        }

        GiftPopup popup = OpenInternal(popupType, null);
        if (popup != null)
            _startupOpenedTypes.Add(popupType);

        return popup;
    }

    /// <summary>
    /// 新玩家首次游玩会话内首次通关云控指定主线关卡时，在已创建的胜利界面上打开 ChestGift。
    /// 调用边界：仅供主线 SUCCESS 流程调用；普通入口与启动序列继续使用 Open/OpenStartupOnce。
    /// 注意事项：非首次通关、非首次游玩、配置关闭或已有 GiftPopup 时均不会打开。
    /// </summary>
    /// <param name="completedLevel">本次完成的主线排序关卡序号。</param>
    /// <param name="isFirstVictory">本关是否首次通关。</param>
    /// <returns>真实打开的 ChestGiftPopup；任一门禁不满足或创建失败时返回 null。</returns>
    public ChestGiftPopup TryOpenFirstPlayVictoryChestGift(int completedLevel, bool isFirstVictory)
    {
        int autoShowLevel = ConfigManager.RemoteConfig.GetAutoShowChestGiftLevel();
        if (!isFirstVictory || autoShowLevel <= 0 || completedLevel != autoShowLevel)
            return null;

        if (DB.GameData == null || DB.GameData.PlayCount != 1)
            return null;

        if (!IsOpenDailyChest)
        {
            this.LogWarning("[GiftManager] First-play victory ChestGift ignored because IsOpenDailyChest is disabled.");
            return null;
        }

        if (_activePopup != null && _activePopup.IsOpen)
        {
            this.LogWarning(
                $"[GiftManager] First-play victory ChestGift ignored because another GiftPopup is open. activeType={_activePopup.PopupType}.");
            return null;
        }

        ChestGiftPopup popup = ChestGift.OpenForFirstPlayVictory();
        if (popup != null)
            _activePopup = popup;

        return popup;
    }

    private GiftPopup OpenInternal(GiftPopupType popupType, int? variantIndex)
    {
        if (popupType == GiftPopupType.ChestGift && !IsOpenDailyChest)
        {
            this.LogWarning("[GiftManager] ChestGift open ignored because IsOpenDailyChest is disabled.");
            return null;
        }

        if (!_controllers.TryGetValue(popupType, out IGiftController controller))
        {
            this.LogError($"[GiftManager] Unsupported gift popup type: {popupType}.");
            return null;
        }

        if (variantIndex.HasValue && !controller.IsVariantDefined(variantIndex.Value))
        {
            this.LogError(
                $"[GiftManager] Invalid gift popup request. type={popupType}, variantIndex={variantIndex.Value}.");
            return null;
        }

        if (_activePopup != null && _activePopup.IsOpen)
        {
            this.LogWarning(
                $"[GiftManager] Open ignored. Active type={_activePopup.PopupType}, requested type={popupType}, variantIndex={variantIndex}.");
            return null;
        }

        GiftPopup popup = controller.Open(variantIndex);
        if (popup == null)
            return null;

        _activePopup = popup;
        return popup;
    }

    /// <summary>
    /// 关闭指定礼物弹窗。
    /// 调用边界：购买进行中等业务拦截由具体 GiftPopup 类型负责。
    /// </summary>
    public void Close(GiftPopup popup)
    {
        if (popup == null)
            return;

        UIManager.Instance?.DestroyUI(popup);
    }

    /// <summary>由 GiftPopup.OnOpen 回报真实打开实例。</summary>
    public void NotifyPopupOpened(GiftPopup popup)
    {
        if (popup != null)
            _activePopup = popup;
    }

    /// <summary>由 GiftPopup.OnClose 清理活动实例，避免残留引用阻止后续打开。</summary>
    public void NotifyPopupClosed(GiftPopup popup)
    {
        if (ReferenceEquals(_activePopup, popup))
            _activePopup = null;
    }

    #endregion
}
