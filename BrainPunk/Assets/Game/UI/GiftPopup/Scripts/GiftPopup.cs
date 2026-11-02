using System;
using System.Globalization;
using Bear.UI;

/// <summary>
/// GiftManager 支持的礼物弹窗类型。
/// 新增类型时必须创建对应 IGiftController，并在 GiftManager 中注册类型路由。
/// </summary>
public enum GiftPopupType
{
    /// <summary>按当地 Daily 计数轮换的 1～5 档 ChestGift 礼盒。</summary>
    ChestGift = 0,

    /// <summary>金币不足时提供广告、首购/常规金币礼包与商店入口的补充金币弹窗。</summary>
    CoinGift = 1,
}

/// <summary>
/// 礼物弹窗公共基类。
/// 负责向 GiftManager 报告通用打开/关闭生命周期；商品、奖励和交互逻辑由具体类型实现。
/// </summary>
public abstract class GiftPopup : BaseUIView
{
    #region 类型信息

    /// <summary>当前弹窗在 GiftManager 中的路由类型。</summary>
    public abstract GiftPopupType PopupType { get; }

    #endregion

    #region 生命周期

    public override void OnOpen()
    {
        base.OnOpen();
        GiftManager.Instance.NotifyPopupOpened(this);
    }

    public override void OnClose()
    {
        GiftManager.Instance.NotifyPopupClosed(this);
        base.OnClose();
    }

    #endregion
}

/// <summary>
/// Gift 商品本地化划线原价计算工具。
/// 只读取平台商品价格元数据并格式化展示字符串，不改变真实售价、商品配置或购买流程。
/// </summary>
internal static class GiftPriceFormatter
{
    #region 原价计算

    /// <summary>
    /// 用途：按“现价 ÷ (1 - 折扣)”反算本地化划线原价。
    /// 调用边界：仅用于 Gift UI 展示；失败时返回 false，调用方应隐藏原价但保留平台现价。
    /// 注意事项：折扣必须在 1～99，且平台必须同时返回有效 RawPrice 和本地化 Price。
    /// </summary>
    internal static bool TryResolveOriginalPrice(
        PurchaseManager purchaseManager,
        string productName,
        int discount,
        string intentScene,
        out string originalPrice)
    {
        originalPrice = string.Empty;
        if (purchaseManager == null ||
            !purchaseManager.IsInitialized ||
            string.IsNullOrWhiteSpace(productName) ||
            discount <= 0 ||
            discount >= 100)
        {
            return false;
        }

        var productInfo = purchaseManager.GetProductInfo(productName, intentScene);
        if (productInfo == null || string.IsNullOrWhiteSpace(productInfo.Details.Price))
            return false;

        double currentPrice;
        try
        {
            currentPrice = Convert.ToDouble(productInfo.Details.RawPrice, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return false;
        }

        if (currentPrice <= 0d || double.IsNaN(currentPrice) || double.IsInfinity(currentPrice))
            return false;

        double originalValue = currentPrice / (1d - discount / 100d);
        if (!TryApplyLocalizedPriceFormat(
                productInfo.Details.Price,
                originalValue,
                out string localizedOriginalPrice))
        {
            return false;
        }

        originalPrice = $"<s>{localizedOriginalPrice}</s>";
        return true;
    }

    #endregion

    #region 本地化格式

    private static bool TryApplyLocalizedPriceFormat(
        string localizedPrice,
        double value,
        out string formattedPrice)
    {
        formattedPrice = string.Empty;
        if (string.IsNullOrWhiteSpace(localizedPrice) || double.IsNaN(value) || double.IsInfinity(value))
            return false;

        int firstDigitIndex = -1;
        int lastDigitIndex = -1;
        for (int index = 0; index < localizedPrice.Length; index++)
        {
            if (!char.IsDigit(localizedPrice[index]))
                continue;

            if (firstDigitIndex < 0)
                firstDigitIndex = index;
            lastDigitIndex = index;
        }

        if (firstDigitIndex < 0 || lastDigitIndex < firstDigitIndex)
            return false;

        int decimalDigits = 0;
        char decimalSeparator = '.';
        int separatorIndex = Math.Max(
            localizedPrice.LastIndexOf('.', lastDigitIndex),
            localizedPrice.LastIndexOf(',', lastDigitIndex));
        if (separatorIndex >= firstDigitIndex && separatorIndex < lastDigitIndex)
        {
            decimalSeparator = localizedPrice[separatorIndex];
            for (int index = separatorIndex + 1; index <= lastDigitIndex; index++)
            {
                if (char.IsDigit(localizedPrice[index]))
                    decimalDigits++;
            }
        }

        string number = Math.Round(value, decimalDigits, MidpointRounding.AwayFromZero)
            .ToString($"F{decimalDigits}", CultureInfo.InvariantCulture);
        if (decimalSeparator == ',')
            number = number.Replace('.', ',');

        string prefix = localizedPrice.Substring(0, firstDigitIndex);
        string suffix = localizedPrice.Substring(lastDigitIndex + 1);
        formattedPrice = prefix + number + suffix;
        return true;
    }

    #endregion
}
