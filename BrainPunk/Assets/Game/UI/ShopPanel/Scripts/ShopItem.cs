using System;
using Config;
using Game.ConfigModule;
using UnityEngine;

/// <summary>
/// Visual wrapper for one shop item entry.
/// Handles its own display fields and purchase button event forwarding.
/// </summary>
public partial class ShopItem : ShopItemBase
{
    public ShopItemType ItemType;
    [SerializeField] private string shopId;
    [SerializeField] ShopSoldOutAnim soldOut;
    [SerializeField] ShopItemAnim anim;
    [SerializeField] GameObject root;

    public CustomButton PurchaseButton => ItemButton;
    public string ShopId => shopId;
    public bool IsSoldOut { get; private set; }

    public Shop ShopData { get; set; }

    public event Action<ShopItem> PurchaseClicked;

    public override void Init()
    {
        base.Init();
        Refresh();
    }

    /// <summary>
    /// Refreshes the localized item name and description.
    /// </summary>
    public void Refresh()
    {
        string id = string.IsNullOrEmpty(shopId) ? ShopData?.Id : shopId;
        var shop = ConfigManager.Instance?.Tables?.TbShop?.GetOrDefault(id);
        if (shop == null || shop.Rewards == null || shop.Rewards.Count == 0)
        {
            SetDiscount(0);
            return;
        }

        SetDiscount(shop.Discount);

        if (ItemCountTxt != null)
        {
            int coinRewardCount = 0;
            bool hasCoinReward = shop.Rewards.TryGetTotal(Config.Game.GameProps.Coin, out coinRewardCount);

            ItemCountTxt.text = string.Format("X{0}", hasCoinReward && coinRewardCount > 0 ? coinRewardCount.ToString() : string.Empty);
        }
    }

    /// <summary>
    /// Updates the visible purchase price string.
    /// </summary>
    public void SetPrice(string priceText)
    {
        if (ItemPriceTxt != null)
        {
            ItemPriceTxt.text = priceText ?? string.Empty;
        }
    }

    /// <summary>
    /// Plays the entry animation when the item is available for display.
    /// </summary>
    public void PlayShowAnimation()
    {
        anim?.Play();
    }

    /// <summary>
    /// Controls the sold-out visual state and animation.
    /// </summary>
    public void SetSoldOut(bool isSoldOut)
    {
        IsSoldOut = isSoldOut;

        if (root != null)
        {
            root.SetActive(!isSoldOut);
        }

        if (soldOut != null)
        {
            soldOut.gameObject.SetActive(isSoldOut);
            if (isSoldOut)
            {
                soldOut.Play();
            }
        }

        SetItemInteractable(!isSoldOut);
    }

    protected override bool CanInvokeItemAction => !IsSoldOut && base.CanInvokeItemAction;

    protected override void OnItemActionClicked()
    {
        PurchaseClicked?.Invoke(this);
    }
}
