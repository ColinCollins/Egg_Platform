using System;
using TMPro;
using UnityEngine;

/// <summary>
/// CoinGift 的单个可购买商品展示项。
/// 只负责数量、价格、原价、折扣与点击转发，商品选择和购买交付仍由 CoinGiftController 负责。
/// </summary>
public sealed class CoinGiftItem : ShopItemBase
{
    #region Inspector 配置

    [Tooltip("当前礼包金币数量文本。")]
    [SerializeField] private TextMeshProUGUI coinCountText;

    [Tooltip("平台返回的本地化现价文本。")]
    [SerializeField] private TextMeshProUGUI priceText;

    [Tooltip("按现价与折扣反推的本地化划线原价；不需要原价的商品可不绑定。")]
    [SerializeField] private TextMeshProUGUI originalPriceText;

    [Tooltip("首购商品的 24 小时自然日倒计时文本；常规商品不需要绑定。")]
    [SerializeField] private TextMeshProUGUI countdownText;

    #endregion

    #region 对外能力

    /// <summary>
    /// 商品项点击事件。
    /// 调用边界：CoinGiftPopup 负责订阅并把当前商品快照交给 CoinGiftController，不在 Item 内购买或发奖。
    /// </summary>
    public event Action<CoinGiftItem> PurchaseClicked;

    /// <summary>
    /// 刷新一档 CoinGift 商品展示。
    /// 调用边界：折扣直接使用当前 Shop.discount；原价为空时隐藏划线原价节点。
    /// </summary>
    public void Refresh(int coinCount, string localizedPrice, int discount, string originalPrice)
    {
        if (coinCountText != null)
            coinCountText.text = coinCount > 0 ? $"+{coinCount}" : string.Empty;

        if (priceText != null)
            priceText.text = localizedPrice ?? string.Empty;

        SetOriginalPrice(originalPrice);
        SetDiscount(discount);
    }

    /// <summary>
    /// 清空当前商品展示，避免配置失效或档位切换时残留旧数据。
    /// 调用边界：只清理显示内容和折扣状态，不改变 GameObject 激活状态。
    /// </summary>
    public void Clear()
    {
        if (coinCountText != null)
            coinCountText.text = string.Empty;

        if (priceText != null)
            priceText.text = string.Empty;

        SetOriginalPrice(string.Empty);
        SetDiscount(0);
    }

    /// <summary>返回当前 CoinGift 商品项的购买按钮，供 Popup 合并业务忙碌状态。</summary>
    public CustomButton PurchaseButton => ItemButton;

    /// <summary>
    /// 刷新首购自然日倒计时。
    /// 调用边界：仅由 CoinGiftPopup 在首购仍未购买时每秒调用；常规商品不绑定 countdownText。
    /// </summary>
    public void SetCountdown(TimeSpan remaining, bool visible)
    {
        if (countdownText == null)
            return;

        bool shouldShow = visible && remaining > TimeSpan.Zero;
        countdownText.gameObject.SetActive(shouldShow);
        countdownText.text = shouldShow
            ? $"{(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}"
            : string.Empty;
    }

    #endregion

    #region 内部展示与交互

    private void SetOriginalPrice(string originalPrice)
    {
        if (originalPriceText == null)
            return;

        bool visible = !string.IsNullOrWhiteSpace(originalPrice);
        originalPriceText.text = visible ? originalPrice : string.Empty;
        originalPriceText.gameObject.SetActive(visible);
    }

    protected override void OnItemActionClicked()
    {
        PurchaseClicked?.Invoke(this);
    }

    #endregion
}
