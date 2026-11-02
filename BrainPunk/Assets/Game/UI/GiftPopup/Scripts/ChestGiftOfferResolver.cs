using System;
using System.Collections.Generic;
using Game.ConfigModule;
using UnityEngine;

/// <summary>
/// ChestGift 商品目录解析器，统一处理档位、Shop/Product、资源、SKU、奖励和限购校验。
/// 调用边界：只读取当前运行时配置与购买存档，不发起支付、不修改存档、不创建界面。
/// </summary>
internal sealed class ChestGiftOfferResolver
{
    /// <summary>解析配置完整且仍可购买的指定档位；已售罄时失败但不返回配置错误。</summary>
    internal bool TryResolveAvailable(
        int giftIndex,
        out ChestGiftController.ChestGiftOffer offer,
        out string error)
    {
        if (!TryBuildSellableOffer(giftIndex, out offer, out error))
            return false;

        if (HasRemainingPurchase(offer))
            return true;

        error = null;
        return false;
    }

    /// <summary>复核控制器创建的 Offer 快照是否仍满足配置和限购条件。</summary>
    internal bool IsAvailable(ChestGiftController.ChestGiftOffer offer)
    {
        return TryValidateOffer(offer, out _) && HasRemainingPurchase(offer);
    }

    /// <summary>查询指定 Offer 是否仍有剩余购买次数。</summary>
    internal bool HasRemainingPurchase(ChestGiftController.ChestGiftOffer offer)
    {
        if (offer.Shop == null || offer.Product == null || DB.GameData?.PurchaseCache == null)
            return false;

        DB.GameData.PurchaseCache.TryGetValue(offer.Product.Id, out int purchaseCount);
        return purchaseCount < offer.Shop.BuyTimes;
    }

    /// <summary>
    /// 创建用于 GM 指定档位预览的宽松快照。
    /// Product 映射无效时仍返回 Shop 与资源信息，由 Popup 展示禁用态。
    /// </summary>
    internal bool TryCreatePreview(int giftIndex, out ChestGiftController.ChestGiftOffer offer)
    {
        offer = default;
        if (!TryGetConfiguredShop(giftIndex, out Config.Shop shop))
            return false;

        PurchaseManager.TryResolveUniqueProduct(shop.Id, out Config.Products product, out _);
        offer = new ChestGiftController.ChestGiftOffer(giftIndex, shop, product);
        return true;
    }

    /// <summary>按 ChestPrefab 非空配置的表顺序取得指定档位 Resources 名称。</summary>
    internal bool TryGetResourceName(int giftIndex, out string resourceName)
    {
        if (TryGetConfiguredShop(giftIndex, out Config.Shop shop))
        {
            resourceName = shop.ChestPrefab.Trim();
            return true;
        }

        resourceName = null;
        return false;
    }

    private static bool TryBuildSellableOffer(
        int giftIndex,
        out ChestGiftController.ChestGiftOffer offer,
        out string error)
    {
        offer = default;
        error = null;
        if (!TryGetConfiguredShop(giftIndex, out Config.Shop shop))
        {
            error = "Shop config with ChestPrefab is missing for this ordered variant.";
            return false;
        }

        string resourceName = shop.ChestPrefab.Trim();
        GameObject popupPrefab = Resources.Load<GameObject>(resourceName);
        if (popupPrefab == null || popupPrefab.GetComponent<ChestGiftPopup>() == null)
        {
            error = $"Resources prefab or ChestGiftPopup component is missing. resource={resourceName}";
            return false;
        }

        if (!PurchaseManager.TryResolveUniqueProduct(shop.Id, out Config.Products product, out string productError))
        {
            error = $"{productError} shopId={shop.Id}";
            return false;
        }

        offer = new ChestGiftController.ChestGiftOffer(giftIndex, shop, product);
        return TryValidateOffer(offer, out error);
    }

    private static bool TryValidateOffer(ChestGiftController.ChestGiftOffer offer, out string error)
    {
        error = null;
        if (offer.Shop == null || offer.Product == null)
        {
            error = "Shop or Product mapping is missing.";
            return false;
        }

        if (!string.Equals(offer.Product.RewardId, offer.Shop.Id, StringComparison.Ordinal))
        {
            error = $"Product RewardId mismatch. shopId={offer.Shop.Id}, product={offer.Product.Id}.";
            return false;
        }

        if (offer.Shop.BuyTimes != 1)
        {
            error = $"BuyTimes must be 1. shopId={offer.Shop.Id}, buyTimes={offer.Shop.BuyTimes}";
            return false;
        }

        if (!PurchaseManager.TryGetProductId(offer.Product.Id, out var productId) ||
            PurchaseManager.IsPlaceholderSku(productId.Sku))
        {
            error = $"Platform SKU is empty or a placeholder. product={offer.Product.Id}";
            return false;
        }

        if (TryValidateRewards(offer.Shop, out string rewardError))
            return true;

        error = $"{rewardError} shopId={offer.Shop.Id}";
        return false;
    }

    private static bool TryValidateRewards(Config.Shop shop, out string error)
    {
        error = null;
        if (shop?.Rewards == null || shop.Rewards.Count == 0)
        {
            error = "Rewards are missing.";
            return false;
        }

        foreach (Config.ShopReward reward in shop.Rewards)
        {
            switch (reward.Key)
            {
                case Config.Game.GameProps.Coin:
                    if (!BaseParamUtils.TryGetIntParam(reward.Value, out int coinCount) || coinCount <= 0)
                    {
                        error = "Coin reward must be a positive IntParam.";
                        return false;
                    }
                    break;
                case Config.Game.GameProps.Skin:
                    if (reward.Value is not Config.StringParam skinParam ||
                        string.IsNullOrWhiteSpace(skinParam.Value) ||
                        !SkinUtils.TryGetSkin(skinParam.Value, out _))
                    {
                        error = "Skin reward must contain a valid StringParam Skin ID.";
                        return false;
                    }
                    break;
                default:
                    error = $"Unsupported reward type: {reward.Key}.";
                    return false;
            }
        }

        return true;
    }

    private static bool TryGetConfiguredShop(int giftIndex, out Config.Shop shop)
    {
        shop = null;
        if (giftIndex < 1 || giftIndex > ChestGiftController.VariantCount)
            return false;

        List<Config.Shop> shops = ConfigManager.Instance?.Tables?.TbShop?.DataList;
        if (shops == null)
            return false;

        int currentIndex = 0;
        foreach (Config.Shop candidate in shops)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.ChestPrefab))
                continue;

            if (++currentIndex != giftIndex)
                continue;

            shop = candidate;
            return true;
        }

        return false;
    }
}
