using System;
using System.Collections.Generic;
using Bear.Logger;
using Bear.SaveModule;
using Bear.UI;
using Game.ConfigModule;
using Game.Scripts.Common;
using GameCommon;

/// <summary>
/// ChestGift 专属业务控制器。
/// 负责当地 Daily 轮换、主页资格、档位选择、弹窗创建和购买交付；商品配置校验由 ChestGiftOfferResolver 收口。
/// </summary>
public sealed class ChestGiftController : IGiftController, IScheduleRefreshable, IDebuger
{
    private const int CurrentGiftDataVersion = 2;

    #region 档位定义

    /// <summary>
    /// ChestGift 业务固定档位数量。
    /// 每一档按 TbShop 表顺序对应一个 ChestPrefab 非空的 Shop 配置。
    /// </summary>
    public const int VariantCount = 5;

    /// <summary>
    /// ChestGift 一次展示与购买使用的配置快照。
    /// 调用边界：只由 ChestGiftController 创建并交给当前 ChestGiftPopup；不得跨配置重载长期缓存。
    /// </summary>
    public readonly struct ChestGiftOffer
    {
        internal ChestGiftOffer(int giftIndex, Config.Shop shop, Config.Products product)
        {
            GiftIndex = giftIndex;
            Shop = shop;
            Product = product;
        }

        /// <summary>当前礼盒在 Daily 轮换中的 1～5 档索引。</summary>
        public int GiftIndex { get; }

        /// <summary>当前礼盒对应的 TbShop 配置快照。</summary>
        public Config.Shop Shop { get; }

        /// <summary>当前礼盒对应的唯一 TbProducts 映射；预览无效配置时可能为空。</summary>
        public Config.Products Product { get; }
    }

    #endregion

    #region 会话状态

    private readonly Action<GiftPopupType> _notifyAvailabilityChanged;
    private readonly HashSet<string> _reportedInvalidOffers = new HashSet<string>();
    private readonly ChestGiftOfferResolver _offerResolver = new ChestGiftOfferResolver();
    private bool _hasConfirmedHomeThisSession;
    private bool _suppressThisSession;
    private bool _isPurchasing;
    private int _purchaseRequestVersion;

    /// <summary>当前控制器对应 ChestGift 类型。</summary>
    public GiftPopupType PopupType => GiftPopupType.ChestGift;

    /// <summary>Schedule 子控制器日志标识。</summary>
    public string RefreshKey => "ChestGift";

    /// <summary>是否存在尚未结束的 ChestGift 购买请求。</summary>
    public bool IsActionInProgress => _isPurchasing;

    /// <summary>
    /// 创建 ChestGift 控制器。
    /// 调用边界：只由 GiftManager 构造并传入局部入口刷新通知。
    /// </summary>
    internal ChestGiftController(Action<GiftPopupType> notifyAvailabilityChanged)
    {
        _notifyAvailabilityChanged = notifyAvailabilityChanged;
    }

    #endregion

    #region Daily Schedule

    /// <summary>
    /// ChestGift 当前本地 Daily 刷新次数。首次建立 GameSchedule 时由 Daily 调度更新为 1。
    /// 注意：这是自然日刷新计数，不是弹窗展示次数。
    /// </summary>
    public int DailyRefreshCount => GetGiftData()?.ChestGiftDailyRefreshCount ?? 0;

    /// <summary>最近一次实际分配的 ChestGift 档位；当前日期尚未选档或无可售礼包时可能为 0。</summary>
    public int CurrentIndex => GetGiftData()?.ChestGiftOfferIndex ?? 0;

    /// <summary>
    /// 将本地自然日累计刷新次数映射为 ChestGift 的 1～5 档；存档尚未刷新时预览第 1 档。
    /// 此方法不读写存档，可供调试和测试复用。
    /// </summary>
    public static int ResolveIndex(int dailyRefreshCount)
    {
        if (dailyRefreshCount <= 0)
            return 1;

        return ((dailyRefreshCount - 1) % VariantCount) + 1;
    }

    /// <summary>
    /// 本地自然日刷新回调：累计 ChestGift 更新次数，供五档礼盒轮换使用。
    /// 调用边界：由 GiftManager 接收 DailyScheduleModule 回调后分发；弹窗展示或关闭不得直接调用。
    /// </summary>
    public void OnScheduleRefresh()
    {
        GiftData giftData = GetGiftData();
        if (giftData == null)
        {
            this.LogError("[ChestGiftController] Daily refresh failed. GiftData is null.");
            return;
        }

        giftData.ChestGiftDailyRefreshCount++;
        if (!DB.SaveGiftData())
            this.LogWarning("[ChestGiftController] Failed to save ChestGift daily refresh count.");
        NotifyAvailabilityChanged();
        this.Log(
            $"[ChestGiftController] Local daily refresh updated. Count={giftData.ChestGiftDailyRefreshCount}, LastAssignedGiftIndex={CurrentIndex}");
    }

    #endregion

    #region 入口可用性

    /// <summary>
    /// 在本次启动成功到达主页时确认 ChestGift 入口资格。
    /// 新存档首次到达主页时会落盘但本会话继续隐藏；历史字段由 GiftData 一次性迁移，避免升级用户被误判为新用户。
    /// </summary>
    /// <returns>本次启动是否允许展示 ChestGift 入口。</returns>
    public bool ConfirmHomeReached()
    {
        if (_hasConfirmedHomeThisSession)
            return !_suppressThisSession;

        GiftData giftData = GetGiftData();
        if (giftData == null)
        {
            this.LogError("[ChestGiftController] Cannot confirm home because GiftData is null.");
            return false;
        }

        _hasConfirmedHomeThisSession = true;
        bool isFirstHome = !giftData.HasReachedGiftHome;

        giftData.HasReachedGiftHome = true;
        _suppressThisSession = isFirstHome;

        if (!DB.SaveGiftData())
            this.LogWarning("[ChestGiftController] Failed to save home state; current-session visibility remains valid.");

        NotifyAvailabilityChanged();
        return !_suppressThisSession;
    }

    bool IGiftController.IsEntryAvailable()
    {
        return IsSessionAvailable() && TryResolveCurrentOffer(out _);
    }

    bool IGiftController.IsVariantDefined(int variantIndex)
    {
        return _offerResolver.TryGetResourceName(variantIndex, out _);
    }

    /// <summary>
    /// 返回当天实际分配的档位；换日从前一天实际档位的下一档继续寻找。
    /// 恢复购买导致当天档位已购时，同一天重选下一可售档；正常购买完成后不重选。
    /// </summary>
    public bool TryResolveCurrentIndex(out int giftIndex)
    {
        if (TryResolveCurrentOffer(out ChestGiftOffer offer))
        {
            giftIndex = offer.GiftIndex;
            return true;
        }

        giftIndex = 0;
        return false;
    }

    private bool TryResolveCurrentOffer(out ChestGiftOffer offer)
    {
        offer = default;
        GiftData giftData = GetGiftData();
        if (giftData == null)
            return false;

        if (ConfigManager.Instance == null || !ConfigManager.Instance.IsInitialized ||
            DB.GameData?.PurchaseCache == null)
            return false;

        long today = DailyScheduleModule.GetTodayStartTimestamp();
        long assignedDay = giftData.ChestGiftOfferLocalDay;
        if (assignedDay > today)
            return false;

        if (assignedDay == today &&
            !string.IsNullOrEmpty(giftData.ChestGiftOfferProductId) &&
            string.Equals(giftData.ChestGiftCompletedOfferProductId,
                giftData.ChestGiftOfferProductId, StringComparison.Ordinal) &&
            DB.GameData.PurchaseCache.TryGetValue(giftData.ChestGiftOfferProductId, out int completedPurchaseCount) &&
            completedPurchaseCount > 0)
            return false;

        int previousIndex = giftData.ChestGiftOfferIndex;
        if (assignedDay == today && previousIndex > 0 &&
            _offerResolver.TryResolveAvailable(previousIndex, out ChestGiftOffer assignedOffer, out _) &&
            string.Equals(assignedOffer.Product.Id, giftData.ChestGiftOfferProductId, StringComparison.Ordinal))
        {
            if (giftData.ChestGiftOfferRefreshCount != DailyRefreshCount)
                SaveDailyOffer(giftData, today, assignedOffer);
            offer = assignedOffer;
            return true;
        }

        bool hasPreviousOffer = assignedDay > 0L && previousIndex > 0;
        int startIndex = hasPreviousOffer
            ? (previousIndex % VariantCount) + 1
            : ResolveIndex(DailyRefreshCount);
        long advanceCount = assignedDay < today && hasPreviousOffer
            ? Math.Max(1L, (long)DailyRefreshCount - giftData.ChestGiftOfferRefreshCount)
            : 1L;
        if (TryResolveAvailableFrom(startIndex, advanceCount, out ChestGiftOffer selectedOffer))
        {
            SaveDailyOffer(giftData, today, selectedOffer);
            offer = selectedOffer;
            return true;
        }

        SaveDailyOffer(giftData, today, default);
        return false;
    }

    private bool TryResolveAvailableFrom(int startIndex, long advanceCount, out ChestGiftOffer offer)
    {
        List<ChestGiftOffer> availableOffers = new List<ChestGiftOffer>(VariantCount);
        for (int offset = 0; offset < VariantCount; offset++)
        {
            int candidateIndex = ((startIndex - 1 + offset) % VariantCount) + 1;
            if (_offerResolver.TryResolveAvailable(candidateIndex, out ChestGiftOffer candidate, out string error))
            {
                availableOffers.Add(candidate);
                continue;
            }

            if (!string.IsNullOrEmpty(error))
                LogInvalidOfferOnce(candidateIndex, error);
        }

        if (availableOffers.Count == 0)
        {
            offer = default;
            return false;
        }

        offer = availableOffers[(int)((advanceCount - 1L) % availableOffers.Count)];
        return true;
    }

    private void SaveDailyOffer(GiftData giftData, long today, ChestGiftOffer offer)
    {
        int giftIndex = offer.GiftIndex;
        string productId = offer.Product?.Id;
        if (giftData.ChestGiftOfferLocalDay == today &&
            giftData.ChestGiftOfferIndex == giftIndex &&
            string.Equals(giftData.ChestGiftOfferProductId, productId, StringComparison.Ordinal) &&
            giftData.ChestGiftOfferRefreshCount == DailyRefreshCount)
            return;

        giftData.ChestGiftOfferLocalDay = today;
        giftData.ChestGiftOfferIndex = giftIndex;
        giftData.ChestGiftOfferProductId = productId;
        giftData.ChestGiftOfferRefreshCount = DailyRefreshCount;
        if (!DB.SaveGiftData())
            this.LogWarning("[ChestGiftController] Failed to save assigned ChestGift offer.");
    }

    void IGiftController.NotifyDelivered()
    {
        NotifyAvailabilityChanged();
    }

    private bool IsSessionAvailable()
    {
        GiftData giftData = GetGiftData();
        if (!_hasConfirmedHomeThisSession || _suppressThisSession || giftData == null)
            return false;

        return true;
    }

    private bool AreAllVariantsPurchased()
    {
        if (DB.GameData?.PurchaseCache == null)
            return false;

        for (int giftIndex = 1; giftIndex <= VariantCount; giftIndex++)
        {
            if (!_offerResolver.TryCreatePreview(giftIndex, out ChestGiftOffer offer) ||
                offer.Product == null ||
                offer.Shop.BuyTimes <= 0 ||
                _offerResolver.HasRemainingPurchase(offer))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 获取 Gift 独立存档，并在首次读取时迁移历史 GameData/GameSchedule 字段。
    /// 调用边界：仅在 DBManager 完成初始化后的 ChestGift 运行时路径调用。
    /// </summary>
    private GiftData GetGiftData()
    {
        if (!DBManager.Instance.IsInitialized)
            return null;

        GiftData giftData = DB.GiftData;
        if (giftData == null)
            return null;

        if (giftData.DataVersion == 1)
        {
            giftData.DataVersion = CurrentGiftDataVersion;
            if (!DB.SaveGiftData())
                this.LogWarning("[ChestGiftController] Failed to save GiftData daily-offer version upgrade.");
        }

        if (giftData.DataVersion > 0)
            return giftData;

        return TryMigrateLegacyData(giftData) ? giftData : null;
    }

    /// <summary>
    /// 将历史散落字段一次性复制到 GiftData；迁移完成后新业务只读写 GiftData。
    /// 注意事项：旧字段至少保留一个兼容版本，迁移不会回写或清空旧存档。
    /// </summary>
    private bool TryMigrateLegacyData(GiftData giftData)
    {
        var gameData = DB.GameData;
        var gameSchedule = DB.GameSchedule;
        if (gameData == null || gameSchedule == null)
        {
            this.LogError("[ChestGiftController] GiftData migration failed because legacy data is unavailable.");
            return false;
        }

        bool isLegacySave = gameData.GiftEntryStateVersion <= 0;
        giftData.HasReachedGiftHome = isLegacySave || gameData.HasReachedGiftHome;
        giftData.ChestGiftDailyRefreshCount = Math.Max(0, gameSchedule.ChestGiftDailyRefreshCount);
        giftData.LastChestGiftPurchaseLocalDay = Math.Max(0L, gameData.LastChestGiftPurchaseLocalDay);
        giftData.DataVersion = CurrentGiftDataVersion;

        if (!DB.SaveGiftData())
            this.LogWarning("[ChestGiftController] GiftData migration could not be persisted; this cold start continues with the in-memory data.");

        return true;
    }

    /// <summary>
    /// 复核指定 ChestGift 快照的配置与限购条件；不以当前日期替换弹窗商品。
    /// 调用边界：供 Popup 刷新按钮和点击前共同调用；不会发起平台请求或修改存档。
    /// </summary>
    public bool IsOfferAvailable(ChestGiftOffer offer)
    {
        return _offerResolver.IsAvailable(offer);
    }

    private void NotifyAvailabilityChanged()
    {
        _notifyAvailabilityChanged?.Invoke(PopupType);
    }

    private void LogInvalidOfferOnce(int giftIndex, string reason)
    {
        string key = $"{giftIndex}:{reason}";
        if (_reportedInvalidOffers.Add(key))
            this.LogError($"[ChestGiftController] Variant is disabled. giftIndex={giftIndex}, reason={reason}");
    }

    #endregion

    #region 弹窗与资源

    /// <summary>
    /// 为新玩家首次游玩会话的主线胜利流程打开当天 ChestGift。
    /// 调用边界：仅由 GiftManager 在完成首次游玩会话、首次通关与云控关卡门禁后调用。
    /// 注意事项：只绕过新存档首次主页会话抑制，仍保留全档已购、商品、限购与资源校验；
    /// 弹窗使用 Top 层覆盖胜利页，避免同层入栈触发胜利页 OnHide/OnShow 并复播进场动画。
    /// </summary>
    internal ChestGiftPopup OpenForFirstPlayVictory()
    {
        if (AreAllVariantsPurchased())
        {
            this.LogWarning("[ChestGiftController] First-play victory open ignored because all ChestGift variants have already been purchased.");
            return null;
        }

        if (!TryResolveCurrentOffer(out ChestGiftOffer offer))
        {
            this.LogWarning("[ChestGiftController] First-play victory open ignored because no available offer could be resolved.");
            return null;
        }

        ChestGiftPopup popup = OpenOffer(offer, UILayer.Top);
        if (popup != null)
            UnlockEntryAfterFirstPlayVictoryOpen();
        return popup;
    }

    /// <summary>
    /// 首次游玩胜利礼包真实打开后，把 ChestGift 普通入口提升为“已完成首次主页会话”的状态。
    /// 调用边界：只能由首次游玩胜利专用打开流程调用；弹窗创建失败时不得解锁入口。
    /// 注意事项：只复用 GiftData 既有主页资格，不修改全局 PlayCount 或其它首启业务。
    /// </summary>
    private void UnlockEntryAfterFirstPlayVictoryOpen()
    {
        GiftData giftData = GetGiftData();
        if (giftData == null)
        {
            this.LogError("[ChestGiftController] Entry unlock failed because GiftData is unavailable.");
            return;
        }

        bool shouldSave = !giftData.HasReachedGiftHome;
        giftData.HasReachedGiftHome = true;
        _hasConfirmedHomeThisSession = true;
        _suppressThisSession = false;

        if (shouldSave && !DB.SaveGiftData())
            this.LogWarning("[ChestGiftController] Failed to persist first-play victory entry unlock; current-session access remains enabled.");

        NotifyAvailabilityChanged();
    }

    GiftPopup IGiftController.Open(int? variantIndex)
    {
        if (AreAllVariantsPurchased())
        {
            this.LogWarning("[ChestGiftController] Open ignored because all ChestGift variants have already been purchased.");
            return null;
        }

        ChestGiftOffer offer;
        if (variantIndex.HasValue)
        {
            if (!_offerResolver.TryCreatePreview(variantIndex.Value, out offer))
            {
                this.LogError($"[ChestGiftController] Shop config is missing for variant index: {variantIndex.Value}.");
                return null;
            }
        }
        else if (!IsSessionAvailable() || !TryResolveCurrentOffer(out offer))
        {
            this.LogWarning("[ChestGiftController] Open ignored because no entry-eligible offer is available.");
            return null;
        }

        return OpenOffer(offer, UILayer.Popup);
    }

    private ChestGiftPopup OpenOffer(ChestGiftOffer offer, UILayer layer)
    {
        string resourceName = offer.Shop.ChestPrefab.Trim();
        ChestGiftPopup popup = UIManager.Instance?.OpenUI<ChestGiftPopup>(
            resourceName,
            layer,
            isShowMask: true,
            isCloseByMask: false);
        if (popup == null)
        {
            this.LogError($"[ChestGiftController] Failed to open Resources prefab. resource={resourceName}.");
            return null;
        }

        popup.ConfigureOffer(offer);

        return popup;
    }

    #endregion

    #region 购买与奖励交付

    /// <summary>
    /// 发起指定 ChestGift 的购买，并在 Popup 已关闭时仍完成购买记录与本地奖励交付。
    /// 调用边界：调用前必须使用当前控制器生成的快照；回调只用于刷新视图，不承担发奖。
    /// </summary>
    public bool TryPurchase(ChestGiftOffer offer, Action<bool> onCompleted)
    {
        if (IsActionInProgress || !IsOfferAvailable(offer))
            return false;

        PurchaseManager purchaseManager = GameManager.Instance?.Purchase;
        bool hasProductId = PurchaseManager.TryGetProductId(offer.Product.Id, out var productId);
        if (purchaseManager == null || !hasProductId)
        {
            this.LogError($"[ChestGiftController] Purchase unavailable. shopId={offer.Shop.Id}, giftIndex={offer.GiftIndex}.");
            return false;
        }

        DB.GameData.PurchaseCache.TryGetValue(offer.Product.Id, out int purchaseCountBeforeRequest);
        _isPurchasing = true;
        int requestVersion = ++_purchaseRequestVersion;

        try
        {
            GameSDKService.Instance?.IAP_CLK(productId.Sku);
            purchaseManager.Purchase(
                offer.Product.Id,
                nameof(ChestGiftPopup),
                (productName, success) => OnPurchaseResult(
                    requestVersion,
                    offer,
                    purchaseCountBeforeRequest,
                    productName,
                    success,
                    onCompleted));
            return true;
        }
        catch (Exception exception)
        {
            _isPurchasing = false;
            this.LogError(
                $"[ChestGiftController] Purchase request failed. product={offer.Product.Id}, exception={exception}");
            purchaseManager.OnPurchaseFailed();
            onCompleted?.Invoke(false);
            return false;
        }
    }

    private void OnPurchaseResult(
        int requestVersion,
        ChestGiftOffer offer,
        int purchaseCountBeforeRequest,
        string productName,
        bool success,
        Action<bool> onCompleted)
    {
        if (!_isPurchasing || requestVersion != _purchaseRequestVersion)
            return;

        _isPurchasing = false;
        PurchaseManager purchaseManager = GameManager.Instance?.Purchase;
        if (purchaseManager == null)
        {
            this.LogError($"[ChestGiftController] Purchase callback lost PurchaseManager. product={productName}");
            onCompleted?.Invoke(false);
            return;
        }

        if (!success)
        {
            purchaseManager.OnPurchaseFailed();
            onCompleted?.Invoke(false);
            return;
        }

        if (!string.Equals(productName, offer.Product.Id, StringComparison.Ordinal))
        {
            this.LogError(
                $"[ChestGiftController] Purchase callback product mismatch. expected={offer.Product.Id}, actual={productName}");
            onCompleted?.Invoke(false);
            return;
        }

        int currentPurchaseCount = 0;
        DB.GameData?.PurchaseCache?.TryGetValue(productName, out currentPurchaseCount);
        if (currentPurchaseCount > purchaseCountBeforeRequest)
        {
            this.LogWarning($"[ChestGiftController] Duplicate purchase success ignored. product={productName}");
            onCompleted?.Invoke(false);
            return;
        }

        if (GameManager.Instance?.Bag == null)
        {
            this.LogError($"[ChestGiftController] Local delivery failed because Bag is unavailable. product={productName}");
            onCompleted?.Invoke(false);
            return;
        }

        purchaseManager.OnPurchaseSuccess(productName);
        int deliveredPurchaseCount = 0;
        DB.GameData?.PurchaseCache?.TryGetValue(productName, out deliveredPurchaseCount);
        if (deliveredPurchaseCount <= purchaseCountBeforeRequest)
        {
            this.LogError($"[ChestGiftController] Purchase record was not updated. product={productName}");
            onCompleted?.Invoke(false);
            return;
        }

        if (!TryDeliverRewards(offer.Shop))
        {
            this.LogError($"[ChestGiftController] Local reward delivery was incomplete. product={productName}");
            onCompleted?.Invoke(false);
            return;
        }

        RecordDeliveredOffer(offer);
        GiftManager.Instance.NotifyGiftDelivered(PopupType);
        onCompleted?.Invoke(true);
    }

    private void RecordDeliveredOffer(ChestGiftOffer offer)
    {
        if (offer.Product == null)
            return;

        GiftData giftData = GetGiftData();
        if (giftData == null ||
            giftData.ChestGiftOfferLocalDay != DailyScheduleModule.GetTodayStartTimestamp() ||
            !string.Equals(giftData.ChestGiftOfferProductId, offer.Product.Id, StringComparison.Ordinal))
            return;

        giftData.ChestGiftCompletedOfferProductId = offer.Product.Id;
        if (!DB.SaveGiftData())
            this.LogWarning("[ChestGiftController] Failed to save delivered ChestGift offer.");
    }

    private bool TryDeliverRewards(Config.Shop shop)
    {
        var bag = GameManager.Instance.Bag;
        foreach (Config.ShopReward reward in shop.Rewards)
        {
            if (reward.Key == Config.Game.GameProps.Coin)
            {
                BaseParamUtils.TryGetIntParam(reward.Value, out int coinCount);
                int before = bag.GetToolCount(Config.Game.GameProps.Coin);
                int after = bag.AddTool(Config.Game.GameProps.Coin, coinCount, RewardType.Purchase);
                if (after < before + coinCount)
                    return false;
                continue;
            }

            var skinParam = (Config.StringParam)reward.Value;
            bag.GetNewSkin(
                skinParam.Value,
                GetSkinWayType.Purchase,
                markForPreview: true);
            if (!(DB.GameData?.OwnedSkin?.Contains(skinParam.Value) ?? false))
                return false;
        }

        return true;
    }

    #endregion
}
