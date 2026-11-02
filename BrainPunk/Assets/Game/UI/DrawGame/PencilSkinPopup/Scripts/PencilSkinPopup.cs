using System;
using System.Collections.Generic;
using Bear.Logger;
using Bear.SaveModule;
using Bear.UI;
using Config.Game;
using Game.ConfigModule;
using Game.Scripts.Common;
using GameCommon;
using I2.Loc;
using UnityEngine;

/// <summary>
/// DrawGame 笔刷皮肤商店。
/// 负责按 PencilSkinShop 顺序读取 PencilSkin、刷新 PencilSkinItem，并处理装备、金币和广告解锁。
/// </summary>
public partial class PencilSkinPopup : BaseUIView, IDebuger
{
    #region Constants

    private const string PencilSkinConfigResourceFolder = "PencilSkinConfig";
    private const string PencilSkinConfigAssetPrefix = "PencilSkin_";

    #endregion

    #region Inspector Configuration

    [Header("笔刷列表")]
    [Tooltip("复用 PencilSkinItem 槽位的虚拟滚动列表；由 Popup prefab 直接绑定。")]
    [SerializeField] private InfiniteScrollView skinScrollView;

    [Tooltip("PencilSkinShop 列表使用的 Item prefab；必须包含 PencilSkinItem 组件。")]
    [SerializeField] private PencilSkinItem pencilSkinItemPrefab;

    #endregion

    #region Runtime State

    private readonly List<Config.PencilSkin> _skinRows = new();
    private readonly Dictionary<string, DrawBrushStyleAsset> _brushStyleCache =
        new(StringComparer.Ordinal);

    private bool _isCloseButtonBound;
    private bool _isRewardAdRequesting;
    private CommmonPopup _coinPurchaseConfirmPopup;

    #endregion

    #region UI Lifecycle

    public override void OnCreate()
    {
        base.OnCreate();
        BindCloseButton();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        BindCloseButton();
        RefreshCatalog();
    }

    public override void OnClose()
    {
        _isRewardAdRequesting = false;
        CloseCoinPurchaseConfirmPopup();
        skinScrollView?.SetComponentData(0, null, false);
        base.OnClose();
    }

    public override void OnDestroyView()
    {
        CloseCoinPurchaseConfirmPopup();
        UnbindCloseButton();
        base.OnDestroyView();
    }

    #endregion

    #region Catalog

    private void RefreshCatalog()
    {
        RebuildCatalog();
        EnsureDefaultOwnedAndSelectedSkin();
        RefreshItems(true);
    }

    private void RebuildCatalog()
    {
        _skinRows.Clear();

        var tables = ConfigManager.Instance?.Tables;
        var skinTable = tables?.TbPencilSkin;
        if (skinTable == null)
        {
            this.LogWarning("[PencilSkinPopup] PencilSkin table is unavailable.");
            return;
        }

        var addedSkinIds = new HashSet<string>(StringComparer.Ordinal);
        var shopRows = tables.TbPencilSkinShop?.DataList;
        if (shopRows != null && shopRows.Count > 0)
        {
            for (int i = 0; i < shopRows.Count; i++)
            {
                Config.PencilSkinShop shopRow = shopRows[i];
                Config.PencilSkin skin = shopRow != null
                    ? skinTable.GetOrDefault(shopRow.SkinId)
                    : null;
                AddCatalogSkin(skin, addedSkinIds);
            }

            return;
        }

        this.LogWarning(
            "[PencilSkinPopup] PencilSkinShop table is empty; falling back to PencilSkin table order.");
        List<Config.PencilSkin> skinRows = skinTable.DataList;
        for (int i = 0; i < skinRows.Count; i++)
        {
            AddCatalogSkin(skinRows[i], addedSkinIds);
        }
    }

    private void AddCatalogSkin(Config.PencilSkin skin, HashSet<string> addedSkinIds)
    {
        if (skin == null ||
            string.IsNullOrEmpty(skin.Id) ||
            addedSkinIds == null ||
            !addedSkinIds.Add(skin.Id))
        {
            return;
        }

        _skinRows.Add(skin);
    }

    private void EnsureDefaultOwnedAndSelectedSkin()
    {
        Bear.SaveModule.DrawGame drawGame = DB.DrawGame;
        if (drawGame == null)
        {
            this.LogError("[PencilSkinPopup] DrawGame save data is unavailable.");
            return;
        }

        bool changed = drawGame.EnsureValidCollections();
        Config.PencilSkin firstDefaultSkin = null;
        for (int i = 0; i < _skinRows.Count; i++)
        {
            Config.PencilSkin skin = _skinRows[i];
            if (skin == null || skin.UnlockType != SkinUnlockType.Default)
            {
                continue;
            }

            firstDefaultSkin ??= skin;
            if (!drawGame.OwnedSkin.Contains(skin.Id))
            {
                drawGame.OwnedSkin.Add(skin.Id);
                changed = true;
            }
        }

        if (firstDefaultSkin != null &&
            (!ContainsCatalogSkin(drawGame.CurrentSkinId) ||
             !IsOwnedSkin(drawGame, GetCatalogSkin(drawGame.CurrentSkinId))))
        {
            drawGame.CurrentSkinId = firstDefaultSkin.Id;
            changed = true;
        }

        if (changed && !DB.SaveDrawGame())
        {
            this.LogWarning(
                "[PencilSkinPopup] Failed to save default owned or selected pencil skin.");
        }
    }

    private bool ContainsCatalogSkin(string skinId)
    {
        return GetCatalogSkin(skinId) != null;
    }

    private Config.PencilSkin GetCatalogSkin(string skinId)
    {
        if (string.IsNullOrEmpty(skinId))
        {
            return null;
        }

        for (int i = 0; i < _skinRows.Count; i++)
        {
            Config.PencilSkin skin = _skinRows[i];
            if (skin != null && string.Equals(skin.Id, skinId, StringComparison.Ordinal))
            {
                return skin;
            }
        }

        return null;
    }

    #endregion

    #region Item View

    private void RefreshItems(bool resetToBeginning = false)
    {
        if (skinScrollView == null || pencilSkinItemPrefab == null)
        {
            this.LogError(
                "[PencilSkinPopup] Cannot refresh items because skinScrollView or pencilSkinItemPrefab is missing.");
            return;
        }

        skinScrollView.SetComponentItemPrefab(
            pencilSkinItemPrefab,
            ClearPencilSkinItem);
        skinScrollView.SetComponentData(
            _skinRows.Count,
            BindSkinItem,
            resetToBeginning);
    }

    private void BindSkinItem(MonoBehaviour itemView, int itemIndex)
    {
        PencilSkinItem item = itemView as PencilSkinItem;
        if (item == null)
        {
            this.LogError(
                "[PencilSkinPopup] InfiniteScrollView bound an item without PencilSkinItem.");
            return;
        }

        if (itemIndex < 0 || itemIndex >= _skinRows.Count)
        {
            item.Clear();
            return;
        }

        Bear.SaveModule.DrawGame drawGame = DB.DrawGame;
        Config.PencilSkin skin = _skinRows[itemIndex];
        DrawBrushStyleAsset brushStyle = LoadBrushStyle(skin.Id);
        Sprite previewSprite = brushStyle != null ? brushStyle.PreviewSprite : null;
        if (previewSprite == null)
        {
            this.LogWarning(
                $"[PencilSkinPopup] Pencil skin preview is missing. skin:{skin.Id}");
        }

        PencilSkinItem.PencilSkinItemState state = ResolveItemState(drawGame, skin);
        bool interactable =
            !_isRewardAdRequesting && CanInteractWithSkin(drawGame, skin, state);
        item.name = $"PencilSkinItem_{skin.Id}";
        item.Bind(skin, previewSprite, state, OnSkinItemClicked, interactable);
    }

    private static void ClearPencilSkinItem(MonoBehaviour itemView)
    {
        (itemView as PencilSkinItem)?.Clear();
    }

    private DrawBrushStyleAsset LoadBrushStyle(string skinId)
    {
        if (string.IsNullOrEmpty(skinId))
        {
            return null;
        }

        if (_brushStyleCache.TryGetValue(skinId, out DrawBrushStyleAsset cachedStyle))
        {
            return cachedStyle;
        }

        string path = $"{PencilSkinConfigResourceFolder}/{PencilSkinConfigAssetPrefix}{skinId}";
        DrawBrushStyleAsset brushStyle =
            Bear.ResourceSystem.ResourceManager.Instance.Load<DrawBrushStyleAsset>(path);
        if (brushStyle == null)
        {
            this.LogWarning(
                $"[PencilSkinPopup] Pencil skin asset load failed. skin:{skinId}, path:{path}");
            return null;
        }

        if (!string.Equals(brushStyle.BrushId?.Trim(), skinId, StringComparison.Ordinal))
        {
            this.LogWarning(
                $"[PencilSkinPopup] Pencil skin asset ID mismatch. skin:{skinId}, asset:{brushStyle.BrushId}, path:{path}");
            return null;
        }

        _brushStyleCache[skinId] = brushStyle;
        return brushStyle;
    }

    private PencilSkinItem.PencilSkinItemState ResolveItemState(
        Bear.SaveModule.DrawGame drawGame,
        Config.PencilSkin skin)
    {
        if (skin == null)
        {
            return PencilSkinItem.PencilSkinItemState.None;
        }

        if (IsOwnedSkin(drawGame, skin))
        {
            return drawGame != null &&
                   string.Equals(drawGame.CurrentSkinId, skin.Id, StringComparison.Ordinal)
                ? PencilSkinItem.PencilSkinItemState.Selected
                : PencilSkinItem.PencilSkinItemState.Normal;
        }

        return skin.UnlockType switch
        {
            SkinUnlockType.Ad => PencilSkinItem.PencilSkinItemState.AdUnlock,
            SkinUnlockType.Coin => PencilSkinItem.PencilSkinItemState.CoinUnlock,
            _ => PencilSkinItem.PencilSkinItemState.Normal,
        };
    }

    private bool CanInteractWithSkin(
        Bear.SaveModule.DrawGame drawGame,
        Config.PencilSkin skin,
        PencilSkinItem.PencilSkinItemState state)
    {
        if (skin == null || state == PencilSkinItem.PencilSkinItemState.None)
        {
            return false;
        }

        if (IsOwnedSkin(drawGame, skin))
        {
            return state != PencilSkinItem.PencilSkinItemState.Selected;
        }

        return skin.UnlockType == SkinUnlockType.Coin ||
               skin.UnlockType == SkinUnlockType.Ad;
    }

    private static bool IsOwnedSkin(
        Bear.SaveModule.DrawGame drawGame,
        Config.PencilSkin skin)
    {
        if (skin == null || string.IsNullOrEmpty(skin.Id))
        {
            return false;
        }

        return skin.UnlockType == SkinUnlockType.Default ||
               (drawGame?.OwnedSkin?.Contains(skin.Id) ?? false);
    }

    #endregion

    #region Item Interaction

    private void OnSkinItemClicked(PencilSkinItem item)
    {
        if (item?.Data == null || _isRewardAdRequesting)
        {
            return;
        }

        switch (item.State)
        {
            case PencilSkinItem.PencilSkinItemState.Normal:
                SelectOwnedSkin(item.Data);
                break;
            case PencilSkinItem.PencilSkinItemState.CoinUnlock:
                UnlockWithCoin(item.Data);
                break;
            case PencilSkinItem.PencilSkinItemState.AdUnlock:
                UnlockWithRewardAd(item.Data);
                break;
        }
    }

    private void SelectOwnedSkin(Config.PencilSkin skin)
    {
        Bear.SaveModule.DrawGame drawGame = DB.DrawGame;
        if (!IsOwnedSkin(drawGame, skin))
        {
            return;
        }

        drawGame.CurrentSkinId = skin.Id;
        if (!DB.SaveDrawGame())
        {
            this.LogWarning(
                $"[PencilSkinPopup] Failed to save selected pencil skin. skin:{skin.Id}");
        }

        RefreshItems();
    }

    private void UnlockWithCoin(Config.PencilSkin skin)
    {
        if (skin == null || skin.UnlockType != SkinUnlockType.Coin)
        {
            return;
        }

        int price = Mathf.Max(0, skin.CoinPrice);
        if (_coinPurchaseConfirmPopup != null)
            return;

        _coinPurchaseConfirmPopup = CommmonPopup.CreateVariant2(
            LocalizationManager.GetTranslation("U_BrushSkin_Tips_01"),
            string.Format( LocalizationManager.GetTranslation("U_BrushSkin_Tips_Des_04"), price),
            onYes: () =>
            {
                _coinPurchaseConfirmPopup = null;
                ConfirmUnlockWithCoin(skin);
            },
            onNo: () => _coinPurchaseConfirmPopup = null);
    }

    private void ConfirmUnlockWithCoin(Config.PencilSkin skin)
    {
        if (this == null ||
            !IsOpen ||
            skin == null ||
            skin.UnlockType != SkinUnlockType.Coin)
        {
            return;
        }

        Bear.SaveModule.DrawGame drawGame = DB.DrawGame;
        if (IsOwnedSkin(drawGame, skin))
        {
            SelectOwnedSkin(skin);
            return;
        }

        var bag = GameManager.Instance?.Bag;
        if (bag == null)
        {
            this.LogError("[PencilSkinPopup] Coin unlock failed because Bag is unavailable.");
            return;
        }

        int price = Mathf.Max(0, skin.CoinPrice);
        bool hasUnlimitedCoin = bag.GetToolCount(GameProps.UnlimitCoin) > 0;
        if (!hasUnlimitedCoin && bag.GetToolCount(GameProps.Coin) < price)
        {
            ShopPanel.Create(false, nameof(PencilSkinPopup));
            SystemTips.Show(LocalizationManager.GetTranslation("U_HurryTips_NeedCoin"));
            return;
        }

        if (!hasUnlimitedCoin && price > 0)
        {
            bag.RemoveTool(GameProps.Coin, price, CostType.PencilSkin);
        }

        GrantAndSelectSkin(skin, GetSkinWayType.Purchase);
        SystemTips.Show(LocalizationManager.GetTranslation("U_HurryTips_Shop_succeed"));
    }

    private void CloseCoinPurchaseConfirmPopup()
    {
        if (_coinPurchaseConfirmPopup == null)
            return;

        CommmonPopup popup = _coinPurchaseConfirmPopup;
        _coinPurchaseConfirmPopup = null;
        UIManager.Instance?.DestroyUI(popup);
    }

    private void UnlockWithRewardAd(Config.PencilSkin skin)
    {
        if (skin == null ||
            skin.UnlockType != SkinUnlockType.Ad ||
            _isRewardAdRequesting)
        {
            return;
        }

        _isRewardAdRequesting = true;
        RefreshItems();
        RewardAdHelper.TryToShowRewardAd(
            RewardPlacement.ShopReward.ToString(),
            onResult: (_, isSuccess) => OnRewardAdResult(skin, isSuccess));
    }

    private void OnRewardAdResult(Config.PencilSkin skin, bool isSuccess)
    {
        _isRewardAdRequesting = false;
        if (this == null || !IsOpen)
        {
            return;
        }

        if (isSuccess)
        {
            GrantAndSelectSkin(skin, GetSkinWayType.Ad);
        }
        else
        {
            RefreshItems();
        }
    }

    private void GrantAndSelectSkin(Config.PencilSkin skin, GetSkinWayType getType)
    {
        if (skin == null || string.IsNullOrEmpty(skin.Id))
        {
            return;
        }

        Bear.SaveModule.DrawGame drawGame = DB.DrawGame;
        if (drawGame == null)
        {
            this.LogError("[PencilSkinPopup] Pencil skin grant failed because DrawGame save is unavailable.");
            return;
        }

        drawGame.EnsureValidCollections();
        bool isNewSkin = !drawGame.OwnedSkin.Contains(skin.Id);
        if (isNewSkin)
        {
            drawGame.OwnedSkin.Add(skin.Id);
        }

        drawGame.CurrentSkinId = skin.Id;
        if (!DB.SaveDrawGame())
        {
            this.LogWarning(
                $"[PencilSkinPopup] Failed to save granted pencil skin. skin:{skin.Id}");
        }

        if (isNewSkin)
        {
            GameSDKService.Instance.GetNewSkin(skin.Id, getType);
        }

        AudioManager.PlaySound("shopGetReward");
        RefreshItems();
    }

    #endregion

    #region Close Button Binding

    private void BindCloseButton()
    {
        if (_isCloseButtonBound || UiCommonX01Btn == null)
        {
            return;
        }

        UiCommonX01Btn.OnClick += OnCloseButtonClicked;
        _isCloseButtonBound = true;
    }

    private void UnbindCloseButton()
    {
        if (!_isCloseButtonBound)
        {
            return;
        }

        if (UiCommonX01Btn != null)
        {
            UiCommonX01Btn.OnClick -= OnCloseButtonClicked;
        }

        _isCloseButtonBound = false;
    }

    private void OnCloseButtonClicked(CustomButton _)
    {
        UIManager.Instance.DestroyUI(this);
    }

    #endregion

    /// <summary>
    /// 打开 PencilSkinPopup。
    /// 调用边界：仅在 Bear UIManager 已初始化的运行时 UI 流程中调用。
    /// </summary>
    /// <returns>成功打开时返回 Popup 实例。</returns>
    public static PencilSkinPopup Create()
    {
        return UIManager.Instance.OpenUI<PencilSkinPopup>(
            nameof(PencilSkinPopup),
            UILayer.Popup);
    }
}
