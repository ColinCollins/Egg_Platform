using System;
using System.Collections.Generic;
using System.Linq;
using Bear.Logger;
using Bear.UI;
using Config;
using Game.ConfigModule;
using Game.Scripts.Common;
using GameCommon;
using UnityEngine;

/// <summary>
/// CoinGift 专属业务控制器。
/// 负责金币不足弹窗的商品选择、广告奖励、购买防重与本地奖励交付；视图只负责展示和交互转发。
/// </summary>
public sealed class CoinGiftController : IGiftController, IDebuger
{
    #region 常量与商品快照

    /// <summary>CoinGift 唯一 Resources 资源名，只用于本类型内部路由。</summary>
    private const string ResourceName = nameof(CoinGiftPopup);

    /// <summary>单 Prefab 类型的内部档位索引，只用于 GiftManager 显式档位校验。</summary>
    private const int DefaultVariantIndex = 0;

    /// <summary>
    /// 当前一次 CoinGift 商品展示所使用的不可变配置快照。
    /// 调用边界：只由 CoinGiftController 创建并交给当前 CoinGiftPopup 使用，不得跨配置重载长期缓存。
    /// </summary>
    public readonly struct CoinGiftOffer
    {
        internal CoinGiftOffer(
            string shopId,
            Config.Shop shop,
            Config.Products product,
            int coinCount,
            bool isFirstPurchase)
        {
            ShopId = shopId;
            Shop = shop;
            Product = product;
            CoinCount = coinCount;
            IsFirstPurchase = isFirstPurchase;
        }

        /// <summary>当前商品对应的 TbShop Id。</summary>
        public string ShopId { get; }

        /// <summary>当前商品的 TbShop 配置快照。</summary>
        public Config.Shop Shop { get; }

        /// <summary>当前商品的 TbProducts 映射快照。</summary>
        public Config.Products Product { get; }

        /// <summary>当前商品配置声明的金币奖励数。</summary>
        public int CoinCount { get; }

        /// <summary>当前是否为限购一次的首购档。</summary>
        public bool IsFirstPurchase { get; }
    }

    #endregion

    #region 状态

    private readonly Action<GiftPopupType> _notifyAvailabilityChanged;
    private readonly HashSet<string> _reportedInvalidOffers = new HashSet<string>();
    private bool _isRewardAdRequesting;
    private bool _isPurchasing;
    private int _rewardAdRequestVersion;
    private int _purchaseRequestVersion;

    /// <summary>当前控制器对应 CoinGift 类型。</summary>
    public GiftPopupType PopupType => GiftPopupType.CoinGift;

    /// <summary>是否存在尚未结束的广告或购买请求，用于弹窗统一锁定输入。</summary>
    public bool IsActionInProgress => _isRewardAdRequesting || _isPurchasing;

    /// <summary>
    /// 创建 CoinGift 控制器。
    /// 调用边界：只由 GiftManager 构造并传入局部入口刷新通知。
    /// </summary>
    internal CoinGiftController(Action<GiftPopupType> notifyAvailabilityChanged)
    {
        _notifyAvailabilityChanged = notifyAvailabilityChanged;
    }

    #endregion

    #region 商品选择与校验

    /// <summary>
    /// 根据首购购买记录选择首购档或常规礼包，并生成本次展示所用商品快照。
    /// 首购配置缺失时保持首购视觉并禁用购买，不会静默回退到常规礼包。
    /// </summary>
    public bool TryResolveCurrentOffer(
        string firstPurchaseShopId,
        string normalPurchaseShopId,
        out CoinGiftOffer offer,
        out bool showFirstPurchase)
    {
        showFirstPurchase = !IsFirstPurchaseConsumed(firstPurchaseShopId);
        string targetShopId = showFirstPurchase ? firstPurchaseShopId : normalPurchaseShopId;
        return TryBuildOffer(targetShopId, showFirstPurchase, out offer);
    }

    /// <summary>返回平台提供的本地化价格；商品信息尚未就绪时返回现有不可用文案。</summary>
    public string GetLocalizedPrice(CoinGiftOffer offer)
    {
        return GameManager.Instance?.Purchase?.GetPrice(offer.Product?.Id, nameof(CoinGiftPopup)) ?? string.Empty;
    }

    /// <summary>
    /// 读取指定 Shop 的金币奖励，仅供 CoinGiftPopup 填充奖励数量。
    /// 调用边界：只解析本地 Luban 配置，不判断该商品是否可购买或是否存在平台 SKU。
    /// </summary>
    public bool TryGetConfiguredCoinReward(string shopId, out int coinCount)
    {
        Config.Shop shop = ConfigManager.Instance?.Tables?.TbShop?.GetOrDefault(shopId);
        return TryGetCoinReward(shop, out coinCount);
    }

    /// <summary>
    /// 读取指定 Shop 的折扣配置，仅供商品展示项刷新 discount part。
    /// 调用边界：只返回本地 Luban Shop.discount，不判断商品是否可购买；配置不存在时返回 false 和 0。
    /// </summary>
    public bool TryGetConfiguredDiscount(string shopId, out int discount)
    {
        Config.Shop shop = ConfigManager.Instance?.Tables?.TbShop?.GetOrDefault(shopId);
        discount = shop?.Discount ?? 0;
        return shop != null;
    }

    /// <summary>复核商品仍未达到限购次数，供展示刷新和点击前共同使用。</summary>
    public bool IsOfferAvailable(CoinGiftOffer offer)
    {
        if (offer.Shop == null || offer.Product == null ||
            DB.GameData?.PurchaseCache == null || GameManager.Instance?.Bag == null)
        {
            return false;
        }

        if (!PurchaseManager.TryGetProductId(offer.Product.Id, out var productId) || PurchaseManager.IsPlaceholderSku(productId.Sku))
            return false;

        DB.GameData.PurchaseCache.TryGetValue(offer.Product.Id, out int purchaseCount);
        return offer.Shop.BuyTimes < 0 || purchaseCount < offer.Shop.BuyTimes;
    }

    private bool IsFirstPurchaseConsumed(string shopId)
    {
        if (!TryResolveShopAndProduct(shopId, out Config.Shop shop, out Config.Products product))
            return false;

        if (shop.BuyTimes != 1 || DB.GameData?.PurchaseCache == null)
            return false;

        DB.GameData.PurchaseCache.TryGetValue(product.Id, out int purchaseCount);
        return purchaseCount >= shop.BuyTimes;
    }

    private bool TryBuildOffer(string shopId, bool isFirstPurchase, out CoinGiftOffer offer)
    {
        offer = default;

        Config.Shop shop = ConfigManager.Instance?.Tables?.TbShop?.GetOrDefault(shopId);
        if (shop == null)
        {
            LogInvalidOfferOnce(shopId, "Shop config is missing.");
            return false;
        }

        if (!PurchaseManager.TryResolveUniqueProduct(shop.Id, out Config.Products product, out string productError))
        {
            LogInvalidOfferOnce(shopId, productError);
            return false;
        }

        if (isFirstPurchase && shop.BuyTimes != 1)
        {
            LogInvalidOfferOnce(shopId, $"First-purchase BuyTimes must be 1. buyTimes={shop.BuyTimes}");
            return false;
        }

        if (shop.BuyTimes == 0)
        {
            LogInvalidOfferOnce(shopId, "BuyTimes is 0.");
            return false;
        }

        if (!PurchaseManager.TryGetProductId(product.Id, out var productId) || PurchaseManager.IsPlaceholderSku(productId.Sku))
        {
            LogInvalidOfferOnce(shopId, $"Platform SKU is empty or a placeholder. product={product.Id}");
            return false;
        }

        if (!TryGetCoinReward(shop, out int coinCount))
        {
            LogInvalidOfferOnce(shopId, "Positive Coin reward is missing.");
            return false;
        }

        offer = new CoinGiftOffer(shopId, shop, product, coinCount, isFirstPurchase);
        return true;
    }

    private static bool TryResolveShopAndProduct(
        string shopId,
        out Config.Shop shop,
        out Config.Products product)
    {
        shop = null;
        product = null;
        if (string.IsNullOrWhiteSpace(shopId))
            return false;

        shop = ConfigManager.Instance?.Tables?.TbShop?.GetOrDefault(shopId);
        if (shop == null)
            return false;

        return PurchaseManager.TryResolveUniqueProduct(shop.Id, out product, out _);
    }

    private static bool TryGetCoinReward(Config.Shop shop, out int coinCount)
    {
        coinCount = 0;
        return shop?.Rewards != null &&
               shop.Rewards.TryGetTotal(Config.Game.GameProps.Coin, out coinCount) &&
               coinCount > 0;
    }

    private void LogInvalidOfferOnce(string shopId, string reason)
    {
        string key = $"{shopId}:{reason}";
        if (_reportedInvalidOffers.Add(key))
            this.LogError($"[CoinGiftController] Offer is disabled. shopId={shopId}, reason={reason}");
    }

    #endregion

    #region 激励广告

    /// <summary>
    /// 请求金币不足弹窗激励广告；成功时由控制器按指定 Shop 的 Coin 配置只发放一次奖励。
    /// 调用边界：回调返回是否交付成功及本次实际交付数量，只用于刷新当前视图，不承担奖励发放。
    /// </summary>
    public bool TryShowRewardAd(string rewardShopId, Action<bool, int> onCompleted)
    {
        if (IsActionInProgress || GameManager.Instance?.Bag == null)
            return false;

        if (!TryGetConfiguredCoinReward(rewardShopId, out int coinCount))
        {
            LogInvalidOfferOnce(rewardShopId, "Positive reward-ad Coin is missing.");
            return false;
        }

        _isRewardAdRequesting = true;
        int requestVersion = ++_rewardAdRequestVersion;

        try
        {
            RewardAdHelper.TryToShowRewardAd(
                Config.Game.RewardPlacement.InGameTips.ToString(),
                onResult: (_, success) => OnRewardAdResult(requestVersion, coinCount, success, onCompleted));
            return true;
        }
        catch (Exception exception)
        {
            _isRewardAdRequesting = false;
            this.LogError($"[CoinGiftController] Reward ad request failed. exception={exception}");
            onCompleted?.Invoke(false, 0);
            return false;
        }
    }

    private void OnRewardAdResult(
        int requestVersion,
        int coinCount,
        bool success,
        Action<bool, int> onCompleted)
    {
        if (!_isRewardAdRequesting || requestVersion != _rewardAdRequestVersion)
            return;

        _isRewardAdRequesting = false;
        bool delivered = false;
        var bag = GameManager.Instance?.Bag;
        if (success && bag != null)
        {
            int coinCountBeforeDelivery = bag.GetToolCount(Config.Game.GameProps.Coin);
            int coinCountAfterDelivery = bag.AddTool(
                Config.Game.GameProps.Coin,
                coinCount,
                RewardType.Ad);
            delivered = coinCountAfterDelivery >= coinCountBeforeDelivery + coinCount;
            if (!delivered)
                this.LogError("[CoinGiftController] Reward-ad Coin delivery was incomplete.");
        }

        onCompleted?.Invoke(delivered, delivered ? coinCount : 0);
    }

    #endregion

    #region 购买与奖励交付

    /// <summary>
    /// 发起当前 CoinGift 商品购买；控制器持有回调并负责防重、购买记录、金币交付和奖励反馈。
    /// 调用边界：视图关闭不会取消已经交给平台的购买请求，回调仍会完成本地交付。
    /// </summary>
    public bool TryPurchase(CoinGiftOffer offer, Action<bool> onCompleted)
    {
        if (IsActionInProgress || !IsOfferAvailable(offer))
            return false;

        PurchaseManager purchaseManager = GameManager.Instance?.Purchase;
        bool hasProductId = PurchaseManager.TryGetProductId(offer.Product.Id, out var productId);
        if (purchaseManager == null || !hasProductId)
        {
            this.LogError($"[CoinGiftController] Purchase unavailable. shopId={offer.ShopId}");
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
                nameof(CoinGiftPopup),
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
            this.LogError($"[CoinGiftController] Purchase request failed. product={offer.Product.Id}, exception={exception}");
            purchaseManager.OnPurchaseFailed();
            onCompleted?.Invoke(false);
            return false;
        }
    }

    private void OnPurchaseResult(
        int requestVersion,
        CoinGiftOffer offer,
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
            this.LogError($"[CoinGiftController] Purchase callback lost PurchaseManager. product={productName}");
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
                $"[CoinGiftController] Purchase callback product mismatch. expected={offer.Product.Id}, actual={productName}");
            onCompleted?.Invoke(false);
            return;
        }

        int currentPurchaseCount = 0;
        DB.GameData?.PurchaseCache?.TryGetValue(productName, out currentPurchaseCount);
        if (currentPurchaseCount > purchaseCountBeforeRequest)
        {
            this.LogWarning($"[CoinGiftController] Duplicate purchase success ignored. product={productName}");
            onCompleted?.Invoke(false);
            return;
        }

        var bag = GameManager.Instance?.Bag;
        if (bag == null)
        {
            this.LogError($"[CoinGiftController] Local delivery failed because Bag is unavailable. product={productName}");
            onCompleted?.Invoke(false);
            return;
        }

        purchaseManager.OnPurchaseSuccess(productName, showTips: false);

        int deliveredPurchaseCount = 0;
        DB.GameData?.PurchaseCache?.TryGetValue(productName, out deliveredPurchaseCount);
        if (deliveredPurchaseCount <= purchaseCountBeforeRequest)
        {
            this.LogError($"[CoinGiftController] Purchase record was not updated. product={productName}");
            onCompleted?.Invoke(false);
            return;
        }

        int coinCountBeforeDelivery = bag.GetToolCount(Config.Game.GameProps.Coin);
        int coinCountAfterDelivery = bag.AddTool(
            Config.Game.GameProps.Coin,
            offer.CoinCount,
            RewardType.Purchase);
        if (coinCountAfterDelivery < coinCountBeforeDelivery + offer.CoinCount)
        {
            this.LogError($"[CoinGiftController] Coin delivery incomplete. product={productName}");
            onCompleted?.Invoke(false);
            return;
        }

        SystemTips.Show(I2.Loc.LocalizationManager.GetTranslation("U_HurryTips_Shop_succeed"));
        AudioManager.PlaySound("shopGetReward");
        GiftManager.Instance.NotifyGiftDelivered(PopupType);
        onCompleted?.Invoke(true);
    }

    #endregion

    #region IGiftController

    bool IGiftController.IsEntryAvailable()
    {
        GameObject prefab = Resources.Load<GameObject>(ResourceName);
        return prefab != null && prefab.GetComponent<CoinGiftPopup>() != null;
    }

    bool IGiftController.IsVariantDefined(int variantIndex)
    {
        return variantIndex == DefaultVariantIndex;
    }

    GiftPopup IGiftController.Open(int? variantIndex)
    {
        CoinGiftPopup popup = UIManager.Instance?.OpenUI<CoinGiftPopup>(
            ResourceName,
            UILayer.Popup,
            isShowMask: true,
            isCloseByMask: false);
        if (popup == null)
            this.LogError($"[CoinGiftController] Failed to open Resources prefab. resource={ResourceName}");

        return popup;
    }

    void IGiftController.NotifyDelivered()
    {
        _notifyAvailabilityChanged?.Invoke(PopupType);
    }

    #endregion
}
