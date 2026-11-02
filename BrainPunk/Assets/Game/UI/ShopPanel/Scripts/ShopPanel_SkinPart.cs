using System;
using System.Collections.Generic;
using Bear.EventSystem;
using Bear.Logger;
using Bear.SaveModule;
using Config;
using Config.Game;
using DG.Tweening;
using Game.ConfigModule;
using Game.Events;
using Game.Scripts.Common;
using GameCommon;
using I2.Loc;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ShopPanel skin page controller.
///
/// Responsibilities:
/// - Build the current Config.Skin / Config.SkinShop catalog and feed it to InfiniteScrollView.
/// - Refresh SkinItem ownership and selected state from DB.GameData.
/// - Handle skin unlock actions for ad, coin, and purchase rows.
/// - Control the scene Skin preview root, RenderTexture camera, and back/face/body preview prefabs.
///
/// Non-responsibilities: normal ShopPanel goods, shop reward grants, restore purchase, sold-out state,
/// and coin reward animation stay owned by ShopPanel / ShopItem.
///
/// version 2.0
/// 1. RefreshItem 的时候默认添加一个 SkinItem_Revert, 默认排在第一位，State 为 Restore，只负责清理当前的 SkinPart 数据
/// 2. 增加服装预览功能。点击对应皮肤后，直接再 Boxer 试穿对应皮肤，但是不改变 GameData 数据，其他购买逻辑不变
///
/// version 3.0
/// 服装预览功能逻辑完善。记录每个 filterPart 当前正选中的skinItem，切换对应的 filterPart 的时候 actionButton 需要切换对应的数据。
/// </summary>
public class ShopPanel_SkinPart : MonoBehaviour, IDebuger, IEventSender
{
    #region Serialized References

    [Header("皮肤滚动")]
    [Tooltip("用于展示过滤后皮肤目录的有限槽位滚动组件。")]
    [SerializeField] private InfiniteScrollView skinScrollView;

    [Tooltip("皮肤列表使用的 ScrollRect。必要时用于自动查找 InfiniteScrollView。")]
    [SerializeField] private ScrollRect skinScrollRect;

    [Tooltip("InfiniteScrollView 创建可复用可见皮肤槽位时使用的预制体。")]
    [SerializeField] private SkinItem skinItemPrefab;

    [Tooltip("Restore/Revert 视觉预制体引用；Restore 行会优先使用该 prefab 创建独立槽位，未配置时回退到普通 skinItemPrefab 的 Restore 状态绑定。")]
    [SerializeField] private SkinItem skinItemRestorePrefab;

    [Tooltip("ShopPanel 皮肤列表期望使用的滚动模式。小目录下 Cyclic 会自动退化为 Finite。")]
    [SerializeField] private InfiniteScrollMode scrollMode = InfiniteScrollMode.Finite;

    [Header("Filter")]
    [Tooltip("SkinPart only listens to the selected result. Exclusive toggle state is owned by this group.")]
    [SerializeField] private GameObjectToggleGroup filterToggleGroup;

    // 是否拥有对应皮肤
    [SerializeField] private Toggle ownedToggle;

    [Header("Unlock Buttons")]
    [SerializeField] private GameObject adButtonRoot;
    [SerializeField] private GameObject coinButtonRoot;
    [SerializeField] private GameObject purchaseButtonRoot;
    [SerializeField] private CustomButton adButton;
    [SerializeField] private CustomButton coinButton;
    [SerializeField] private CustomButton purchaseButton;

    [SerializeField] private TextMeshProUGUI adBtnTxt;

    [SerializeField] private TextMeshProUGUI coinBtnTxt;

    [SerializeField] private TextMeshProUGUI purchaseBtnTxt;

    [Header("Boxer Preview")]
    [Tooltip("试穿提示文本对象；仅根据 ShopPanel 临时试穿状态显示或隐藏，不参与存档、解锁或 Actor 刷新。")]
    [SerializeField] private GameObject tryUsingTxt;
    [SerializeField] private string fallbackBaseSkinName = "box_original";
    [SerializeField] private string sceneSkinRootName = "Skin";
    [SerializeField] private RawImage boxerPreview;
    [SerializeField] private RenderTexture boxerRenderTexture;

    #endregion

    #region Runtime State

    private readonly List<SkinCatalogRow> _skinRows = new();
    private readonly Dictionary<SkinPart, Config.Skin> _transientPreviewSkins = new();
    private readonly Dictionary<SkinPart, Config.Skin> _selectedSkinsByPart = new();
    private ShopPanelBoxerPreviewController _boxerPreviewController;
    private SkinPart _skinPart = SkinPart.Back;
    private Config.Skin _selectedSkin;
    private Config.Skin _pendingPurchaseSkin;
    private bool _isFilterGroupBound;
    private bool _isOwnedToggleBound;
    private bool _isRefreshing;
    private bool _isSkinRewardAdRequesting;
    private bool _isSkinPurchaseRequesting;
    private bool _animateNextVisibleSlotRefresh;
    private bool _hasPreviewSession;
    private static int _skinSelectionSignalVersion;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();
        SyncTryUsingText();
        _boxerPreviewController?.Close();
        HideActionButtons();
    }

    private void OnEnable()
    {
        ResolveReferences();
        BindActionButtons();
        BindFilterToggleGroup();
        BindOwnedToggle();
        EnsureDefaultOwnedSkins();
        OpenOrResumeBoxerPreview();
        ApplyFilterForCurrentToggleGroupSelection();
    }

    private void OnDisable()
    {
        UnbindActionButtons();
        UnbindFilterToggleGroup();
        UnbindOwnedToggle();
        _pendingPurchaseSkin = null;
        _isSkinRewardAdRequesting = false;
        _isSkinPurchaseRequesting = false;
        if (_hasPreviewSession)
        {
            _boxerPreviewController?.Suspend();
        }

        HideActionButtons();
        if (skinScrollView != null)
        {
            skinScrollView.SetData(0, null, true);
        }
    }

    private void OnDestroy()
    {
        UnbindActionButtons();
        UnbindFilterToggleGroup();
        UnbindOwnedToggle();
        ClosePreviewSession();
        _boxerPreviewController?.Dispose();
        _boxerPreviewController = null;
    }

    #endregion

    #region Public UI Entry Points

    /// <summary>
    /// Close-path guard used by ShopPanel. The Skin tab may already be inactive, so this
    /// hides the scene preview without relying on an enabled ShopPanel_SkinPart instance.
    /// </summary>
    public static void HideSceneSkinPreview()
    {
        ShopPanelBoxerPreviewController.HideScenePreview("Skin");
    }

    /// <summary>
    /// 结束当前 ShopPanel 的皮肤预览会话并清理临时试穿内容。
    /// 调用边界：仅由 ShopPanel 关闭流程或当前组件销毁流程调用，页签 Disable 不得调用。
    /// </summary>
    internal void ClosePreviewSession()
    {
        _selectedSkin = null;
        _pendingPurchaseSkin = null;
        ClearAllRememberedSelections();
        ClearAllTransientPreviews();
        _isSkinRewardAdRequesting = false;
        _isSkinPurchaseRequesting = false;
        _hasPreviewSession = false;
        _boxerPreviewController?.Close();
        HideActionButtons();
    }

    /// <summary>
    /// 供 ShopPanel 外部入口切换 Skin 页内的部位筛选；只同步筛选按钮和列表数据，不授予皮肤、不试穿、不写入存档。
    /// 调用方应先确保 ShopPanel 已切到 Skin 页；缺少筛选按钮组时会退化为直接刷新对应部位列表。
    /// </summary>
    public void SelectSkinPart(SkinPart skinPart)
    {
        ResolveReferences();

        int filterIndex = GetFilterIndex(skinPart);
        if (filterToggleGroup != null && filterIndex >= 0)
        {
            filterToggleGroup.SelectIndex(filterIndex);
            if (_skinPart == skinPart)
            {
                return;
            }
        }

        SetFilter(skinPart);
    }

    /// <summary>
    /// 用途：获取指定 SkinPart 对应的筛选 Toggle 引导目标。
    /// 调用边界：仅由 Shop2SkinGuideFlow 在 Skin 大页激活后于 Unity 主线程调用。
    /// 注意事项：依赖 filterToggleGroup 的序列化顺序与 GetFilterIndex(...) 映射一致，引用缺失时返回 false。
    /// </summary>
    internal bool TryGetSkinPartGuideTarget(SkinPart skinPart, out RectTransform target)
    {
        ResolveReferences();
        int filterIndex = GetFilterIndex(skinPart);
        GameObjectSwitchToggle targetToggle = filterToggleGroup != null
            ? filterToggleGroup.GetToggleAtIndex(filterIndex)
            : null;
        target = targetToggle != null ? targetToggle.transform as RectTransform : null;
        return target != null;
    }

    /// <summary>
    /// Rebuilds the filtered skin catalog and resets the virtualized list to the beginning.
    /// </summary>
    public void RefreshItem()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        try
        {
            ResolveReferences();
            RebuildCatalog();
            EnsureDefaultOwnedSkins();
            RefreshSelectedSkinForActiveFilter();

            if (skinScrollView != null)
            {
                skinScrollView.SetItemPrefab(skinItemPrefab);
                skinScrollView.SetItemPrefabSelector(SelectSkinItemPrefab);
                skinScrollView.Mode = scrollMode;
                skinScrollView.SetData(_skinRows.Count, BindSkinItem, true);
            }
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    /// <summary>
    /// 用途：刷新当前部位目录，按 skinId 定位已拥有的目标皮肤并返回可见引导目标。
    /// 调用边界：仅由 Shop2SkinGuideFlow 在 Skin 大页和部位筛选完成后调用。
    /// 注意事项：目标槽位来自虚拟列表，只保证本次定位后的即时有效性，不应长期缓存。
    /// </summary>
    internal bool TryPrepareGuideSkin(string skinId, out RectTransform target)
    {
        target = null;
        if (string.IsNullOrEmpty(skinId))
            return false;

        RefreshItem();
        int rowIndex = FindSkinRowIndex(skinId);
        if (rowIndex < 0 || !IsOwnedSkin(_skinRows[rowIndex].Skin) || skinScrollView == null)
            return false;

        if (!skinScrollView.TryFocusItem(rowIndex, out SkinItem item) ||
            item?.Data == null ||
            !string.Equals(item.Data.Id, skinId, StringComparison.Ordinal))
        {
            return false;
        }

        target = item.transform as RectTransform;
        return target != null;
    }

    /// <summary>
    /// 用途：消费皮肤引导的目标点击，并复用现有已拥有皮肤穿戴与红点清理路径。
    /// 调用边界：仅由 Shop2SkinGuideFlow 在目标 GuideFinger 点击完成后立即调用。
    /// 注意事项：会重新按 skinId 校验虚拟槽位，目标已换绑、不可见或未拥有时返回 false。
    /// </summary>
    internal bool TrySelectGuideSkin(string skinId)
    {
        int rowIndex = FindSkinRowIndex(skinId);
        if (rowIndex < 0 || !IsOwnedSkin(_skinRows[rowIndex].Skin) || skinScrollView == null)
            return false;

        if (!skinScrollView.TryGetBoundItem(rowIndex, out SkinItem item) ||
            item?.Data == null ||
            !string.Equals(item.Data.Id, skinId, StringComparison.Ordinal))
        {
            return false;
        }

        OnSkinItemClicked(item);
        return true;
    }

    private void SetFilter(SkinPart skinPart)
    {
        _skinPart = skinPart;
        RefreshItem();
    }

    #endregion

    #region Event Binding

    private void BindActionButtons()
    {
        UnbindActionButtons();

        if (adButton != null)
        {
            adButton.OnClick += OnAdButtonClicked;
        }

        if (coinButton != null)
        {
            coinButton.OnClick += OnCoinButtonClicked;
        }

        if (purchaseButton != null)
        {
            purchaseButton.OnClick += OnPurchaseButtonClicked;
        }
    }

    private void UnbindActionButtons()
    {
        if (adButton != null)
        {
            adButton.OnClick -= OnAdButtonClicked;
        }

        if (coinButton != null)
        {
            coinButton.OnClick -= OnCoinButtonClicked;
        }

        if (purchaseButton != null)
        {
            purchaseButton.OnClick -= OnPurchaseButtonClicked;
        }
    }

    private void BindFilterToggleGroup()
    {
        if (_isFilterGroupBound || filterToggleGroup == null)
        {
            return;
        }

        filterToggleGroup.OnSelectionChanged += OnFilterSelectionChanged;
        _isFilterGroupBound = true;
    }

    private void UnbindFilterToggleGroup()
    {
        if (!_isFilterGroupBound)
        {
            return;
        }

        if (filterToggleGroup != null)
        {
            filterToggleGroup.OnSelectionChanged -= OnFilterSelectionChanged;
        }

        _isFilterGroupBound = false;
    }

    private void BindOwnedToggle()
    {
        if (_isOwnedToggleBound || ownedToggle == null)
        {
            return;
        }

        ownedToggle.onValueChanged.AddListener(OnOwnedToggleChanged);
        _isOwnedToggleBound = true;
    }

    private void UnbindOwnedToggle()
    {
        if (!_isOwnedToggleBound)
        {
            return;
        }

        if (ownedToggle != null)
        {
            ownedToggle.onValueChanged.RemoveListener(OnOwnedToggleChanged);
        }

        _isOwnedToggleBound = false;
    }

    private void OnFilterSelectionChanged(GameObjectSwitchToggle _)
    {
        ApplyFilterForCurrentToggleGroupSelection();
    }

    private void OnOwnedToggleChanged(bool _)
    {
        RefreshItem();
    }

    private void ApplyFilterForCurrentToggleGroupSelection()
    {
        // 默认 seletcIndex = -1 
        ApplyFilterByIndex(filterToggleGroup != null ? filterToggleGroup.SelectedIndex : -1);
    }

    #endregion

    #region Catalog And Item Binding

    /// <summary>
    /// 这里注意
    /// </summary>
    /// <param name="index"></param>
    private void ApplyFilterByIndex(int index)
    {
        SetFilter(GetFilterPartByIndex(index));
    }

    private static int GetFilterIndex(SkinPart skinPart)
    {
        return skinPart switch
        {
            SkinPart.Face => 1,
            SkinPart.Body => 2,
            SkinPart.FullBody => 3,
            _ => 0
        };
    }

    private static SkinPart GetFilterPartByIndex(int index)
    {
        return index switch
        {
            1 => SkinPart.Face,
            2 => SkinPart.Body,
            3 => SkinPart.FullBody,
            _ => SkinPart.Back
        };
    }

    private void RebuildCatalog()
    {
        _skinRows.Clear();
        _skinRows.Add(SkinCatalogRow.Restore(_skinPart));
        var entries = new List<SkinCatalogEntry>();

        var tables = ConfigManager.Instance?.Tables;
        var skinTable = tables?.TbSkin;
        if (skinTable == null)
        {
            return;
        }

        var shopList = tables.TbSkinShop?.DataList;
        if (shopList != null && shopList.Count > 0)
        {
            for (int i = 0; i < shopList.Count; i++)
            {
                var shop = shopList[i];
                var skin = skinTable.GetOrDefault(shop.SkinId);
                AddSkinIfMatches(entries, skin, i);
            }
        }
        else
        {
            var skinList = skinTable.DataList;
            for (int i = 0; i < skinList.Count; i++)
            {
                AddSkinIfMatches(entries, skinList[i], i);
            }
        }

        entries.Sort(CompareSkinCatalogEntries);
        for (int i = 0; i < entries.Count; i++)
        {
            _skinRows.Add(SkinCatalogRow.FromSkin(entries[i].Skin, _skinPart));
        }
    }

    private int FindSkinRowIndex(string skinId)
    {
        if (string.IsNullOrEmpty(skinId))
            return -1;

        for (int i = 0; i < _skinRows.Count; i++)
        {
            SkinCatalogRow row = _skinRows[i];
            if (row.RowType == SkinCatalogRowType.Skin &&
                row.Skin != null &&
                string.Equals(row.Skin.Id, skinId, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private void EnsureDefaultOwnedSkins()
    {
        var gameData = DB.GameData;
        var skinList = ConfigManager.Instance?.Tables?.TbSkin?.DataList;
        if (gameData == null || skinList == null)
        {
            return;
        }

        var bag = GameManager.Instance?.Bag;
        if (bag == null)
        {
            this.LogError("[ShopPanel_SkinPart] EnsureDefaultOwnedSkins failed. Bag is null.");
            return;
        }

        bool changed = false;
        foreach (var skin in skinList)
        {
            if (skin == null || skin.UnlockType != SkinUnlockType.Default || string.IsNullOrEmpty(skin.Id))
            {
                continue;
            }
            // 默认皮肤
            if (bag.GetNewSkin(skin.Id, GetSkinWayType.Free, false))
            {
                changed = true;
            }
        }

        if (changed)
        {
            gameData.Save();
        }
    }

    private void AddSkinIfMatches(List<SkinCatalogEntry> entries, Config.Skin skin, int sourceOrderIndex)
    {
        if (entries == null || skin == null)
        {
            return;
        }

        if (skin.SkinPart != _skinPart)
        {
            return;
        }

        if (IsOwnedOnlyEnabled() && !IsOwnedSkin(skin))
        {
            return;
        }

        entries.Add(new SkinCatalogEntry(skin, sourceOrderIndex));
    }

    private int CompareSkinCatalogEntries(SkinCatalogEntry left, SkinCatalogEntry right)
    {
        bool leftOwned = IsOwnedSkin(left.Skin);
        bool rightOwned = IsOwnedSkin(right.Skin);
        if (leftOwned != rightOwned)
        {
            return leftOwned ? -1 : 1;
        }

        int qualityComparison = right.Skin.Quality.CompareTo(left.Skin.Quality);
        if (qualityComparison != 0)
        {
            return qualityComparison;
        }

        return left.SourceOrderIndex.CompareTo(right.SourceOrderIndex);
    }

    private void BindSkinItem(SkinItem item, int index)
    {
        if (item == null || index < 0 || index >= _skinRows.Count)
        {
            item?.Clear();
            return;
        }

        var row = _skinRows[index];
        if (row.RowType == SkinCatalogRowType.Restore)
        {
            item.BindRestore(OnSkinItemClicked, _animateNextVisibleSlotRefresh);
            return;
        }

        var skin = row.Skin;
        bool owned = IsOwnedSkin(skin);
        var state = ResolveSkinItemState(skin);
        item.Bind(skin, state, owned, OnSkinItemClicked, _animateNextVisibleSlotRefresh);
    }

    private SkinItem SelectSkinItemPrefab(int index)
    {
        if (index >= 0 &&
            index < _skinRows.Count &&
            _skinRows[index].RowType == SkinCatalogRowType.Restore &&
            skinItemRestorePrefab != null)
        {
            return skinItemRestorePrefab;
        }

        return skinItemPrefab;
    }

    #endregion

    #region Skin Item Selection

    private void OnSkinItemClicked(SkinItem item)
    {
        if (item == null)
        {
            return;
        }

        if (item.State == SkinItem.SkinItemState.Restore)
        {
            ClearCurrentSkinPart(_skinPart);
            return;
        }

        if (item.Data == null)
        {
            return;
        }

        AudioManager.PlaySound("button");

        _selectedSkin = item.Data;
        RememberSelectedSkin(_selectedSkin);
        if (IsOwnedSkin(_selectedSkin))
        {
            ApplyOwnedSkin(_selectedSkin, true);
            TryToClearRedDot(_selectedSkin);
            item.Refresh();
            return;
        }

        if (CanPreviewUnownedSkin(_selectedSkin))
        {
            SetTransientPreview(_selectedSkin);
        }

        ShowActionButtonFor(_selectedSkin);
        RefreshVisibleSlots(true);
    }

    private void TryToClearRedDot(Skin _selectedSkin)
    {
        var bag = GameManager.Instance?.Bag;
        bag?.RemoveFromNewSkinList(_selectedSkin.Id);
    }

    #endregion

    #region Unlock Actions

    private void OnAdButtonClicked(CustomButton _)
    {
        if (_isSkinRewardAdRequesting || _selectedSkin == null || _selectedSkin.UnlockType != SkinUnlockType.Ad)
        {
            return;
        }

        _isSkinRewardAdRequesting = true;
        RefreshActionButtonInteractable();

        var rewardSkin = _selectedSkin;
        RewardAdHelper.TryToShowRewardAd(
            RewardPlacement.ShopReward.ToString(),
            onResult: (_, isSuc) => OnRewardAdResult(rewardSkin, isSuc));
    }

    private void OnRewardAdResult(Config.Skin skin, bool isSuc)
    {
        _isSkinRewardAdRequesting = false;

        if (this == null || !isActiveAndEnabled)
        {
            return;
        }

        if (isSuc && skin != null)
        {
            GrantSkin(skin, RewardType.Ad);
        }

        RefreshActionButtonInteractable();
    }

    private void OnCoinButtonClicked(CustomButton _)
    {
        if (_selectedSkin == null || _selectedSkin.UnlockType != SkinUnlockType.Coin)
        {
            return;
        }

        var bag = GameManager.Instance?.Bag;
        if (bag == null)
        {
            this.LogError("[ShopPanel_SkinPart] Coin unlock failed. Bag is null.");
            return;
        }

        int price = Mathf.Max(0, _selectedSkin.CoinPrice);
        bool hasUnlimitCoin = bag.GetToolCount(GameProps.UnlimitCoin) > 0;
        if (!hasUnlimitCoin && bag.GetToolCount(GameProps.Coin) < price)
        {
            SystemTips.Show(LocalizationManager.GetTranslation("U_HurryTips_NeedCoin"));
            return;
        }

        if (!hasUnlimitCoin && price > 0)
        {
            bag.RemoveTool(GameProps.Coin, price, CostType.Skin);
        }

        GrantSkin(_selectedSkin, RewardType.Purchase);
        SystemTips.Show(LocalizationManager.GetTranslation("U_HurryTips_Shop_succeed"));
    }

    private void OnPurchaseButtonClicked(CustomButton _)
    {
        if (_isSkinPurchaseRequesting || _selectedSkin == null || _selectedSkin.UnlockType != SkinUnlockType.Purchase)
        {
            return;
        }

        if (string.IsNullOrEmpty(_selectedSkin.ProductId))
        {
            this.LogWarning($"[ShopPanel_SkinPart] Purchase skin skipped. ProductId is empty. skin:{_selectedSkin.Id}");
            return;
        }

        PurchaseManager purchaseManager = GameManager.Instance?.Purchase;
        if (purchaseManager == null)
        {
            this.LogError("[ShopPanel_SkinPart] Purchase skin skipped. PurchaseManager is null.");
            return;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            purchaseManager.OnPurchaseFailed();
            return;
        }

        var productId = Products.GetProductId(_selectedSkin.ProductId);
        if (productId == null)
        {
            this.LogWarning($"[ShopPanel_SkinPart] Purchase skin skipped. Product not found. skin:{_selectedSkin.Id}, product:{_selectedSkin.ProductId}");
            return;
        }

        _pendingPurchaseSkin = _selectedSkin;
        _isSkinPurchaseRequesting = true;
        RefreshActionButtonInteractable();
        GameSDKService.Instance?.IAP_CLK(productId.Sku);
        purchaseManager.Purchase(_selectedSkin.ProductId, "skin", OnSkinPurchaseCallback);
    }

    private void OnSkinPurchaseCallback(string productName, bool isSuc)
    {
        var purchasedSkin = _pendingPurchaseSkin;
        _pendingPurchaseSkin = null;
        _isSkinPurchaseRequesting = false;

        if (isSuc && purchasedSkin != null && string.Equals(purchasedSkin.ProductId, productName, StringComparison.Ordinal))
        {
            GrantSkin(purchasedSkin, RewardType.Purchase);
            SystemTips.Show(LocalizationManager.GetTranslation("U_HurryTips_Shop_succeed"));
        }
        else if (!isSuc)
        {
            GameManager.Instance?.Purchase?.OnPurchaseFailed();
        }

        RefreshActionButtonInteractable();
    }

    #endregion

    #region Ownership And Save State

    private void GrantSkin(Config.Skin skin, RewardType rewardType)
    {
        if (skin == null || string.IsNullOrEmpty(skin.Id))
        {
            return;
        }

        var bag = GameManager.Instance?.Bag;
        if (bag == null)
        {
            this.LogError("[ShopPanel_SkinPart] GrantSkin failed. Bag is null.");
            return;
        }

        bag.GetNewSkin(skin.Id, rewardType == RewardType.Ad ? GetSkinWayType.Ad : GetSkinWayType.Purchase, false);

        ApplyOwnedSkin(skin, true);
        RefreshItem();

        if (rewardType == RewardType.Ad || rewardType == RewardType.Purchase)
        {
            AudioManager.PlaySound("shopGetReward");
        }
    }

    private void ApplyOwnedSkin(Config.Skin skin, bool isSave = false)
    {
        if (skin == null)
        {
            return;
        }

        var gameData = DB.GameData;
        if (gameData == null)
        {
            this.LogError("[ShopPanel_SkinPart] ApplyOwnedSkin failed. GameData is null.");
            return;
        }

        if (isSave)
        {
            switch (skin.SkinPart)
            {
                case SkinPart.Back:
                    gameData.CurrentBackPart = ResolvePartSkinKey(skin);
                    break;
                case SkinPart.Face:
                    gameData.CurrentFacePart = ResolvePartSkinKey(skin);
                    break;
                case SkinPart.Body:
                    gameData.CurrentBodyPart = ResolvePartSkinKey(skin);
                    break;
                default:
                    gameData.CurrentSkinId = ResolvePartSkinKey(skin);
                    break;
            }

            gameData.Save();
        }

        ClearTransientPreview(skin.SkinPart);
        DispatchSkinSelectionChanged(skin.SkinPart, ActorSkinSelectionChangeReason.Applied);
        _selectedSkin = skin;
        RememberSelectedSkin(skin);
        ApplyCurrentSkinToViewer(true);
        HideActionButtons();
        RefreshVisibleSlots(true);
    }

    /// <summary>
    /// 清理指定皮肤部件的保存值和同部位临时试穿；仅由 Restore 入口或同部位清理逻辑调用。
    /// </summary>
    /// <param name="part">需要清理的 SkinPart；FullBody 对应 CurrentSkinId，其他部位对应各自保存字段。</param>
    public void ClearCurrentSkinPart(SkinPart part)
    {
        var gameData = DB.GameData;
        if (gameData == null)
        {
            this.LogError("[ShopPanel_SkinPart] ClearCurrentSkinPart failed. GameData is null.");
            return;
        }

        bool hadTransientPreview = HasTransientPreviewForPart(part);
        ClearTransientPreview(part);
        ClearRememberedSelection(part);
        bool changed = ClearSavedSkinValue(gameData, part);
        if (changed)
        {
            gameData.Save();
            DispatchSkinSelectionChanged(part, ActorSkinSelectionChangeReason.Cleared);
        }

        if (part == _skinPart)
        {
            _selectedSkin = ResolveSelectedSkinForActiveFilter();
        }

        ApplyCurrentSkinToViewer(hadTransientPreview || changed);
        SyncActionButtonsForActiveSelection();
        RefreshVisibleSlots(true);
    }

    private void DispatchSkinSelectionChanged(SkinPart changedPart, ActorSkinSelectionChangeReason reason)
    {
        this.DispatchEvent(Witness<ActorSkinSelectionChangedEvent>._, changedPart, reason, ++_skinSelectionSignalVersion);
    }

    #endregion

    #region Viewer Refresh And Skin State Query

    private void ApplyCurrentSkinToViewer(bool playBoxerSwitchAnimation = false)
    {
        var gameData = DB.GameData;
        if (gameData == null)
        {
            return;
        }

        string effectiveBack = GetEffectiveSkinValue(gameData, SkinPart.Back);
        string effectiveFace = GetEffectiveSkinValue(gameData, SkinPart.Face);
        string effectiveBody = GetEffectiveSkinValue(gameData, SkinPart.Body);
        string effectiveFullBody = GetEffectiveSkinValue(gameData, SkinPart.FullBody);
        _boxerPreviewController?.Apply(
            effectiveBack,
            effectiveFace,
            effectiveBody,
            effectiveFullBody,
            playBoxerSwitchAnimation ? _skinPart : (SkinPart?)null);
    }

    private void OpenOrResumeBoxerPreview()
    {
        if (_hasPreviewSession)
        {
            _boxerPreviewController?.Resume();
            SyncTryUsingText();
            return;
        }

        ClearAllTransientPreviews();
        _boxerPreviewController?.Open();
        ApplyCurrentSkinToViewer();
        _hasPreviewSession = true;
    }

    private bool IsOwnedSkin(Config.Skin skin)
    {
        if (skin == null || string.IsNullOrEmpty(skin.Id))
        {
            return false;
        }

        return skin.UnlockType == SkinUnlockType.Default || (DB.GameData?.OwnedSkin?.Contains(skin.Id) ?? false);
    }

    private bool IsOwnedOnlyEnabled()
    {
        return ownedToggle != null && ownedToggle.isOn;
    }

    private SkinItem.SkinItemState ResolveSkinItemState(Config.Skin skin)
    {
        if (skin == null)
        {
            return SkinItem.SkinItemState.None;
        }

        if (IsTransientPreviewSkin(skin))
        {
            return SkinItem.SkinItemState.Preview;
        }

        if (HasTransientPreviewForPart(skin.SkinPart))
        {
            return SkinItem.SkinItemState.Normal;
        }

        if (IsCurrentSkin(skin))
        {
            return SkinItem.SkinItemState.Selected;
        }

        return SkinItem.SkinItemState.Normal;
    }

    private bool IsCurrentSkin(Config.Skin skin)
    {
        if (skin == null)
        {
            return false;
        }

        var gameData = DB.GameData;
        if (gameData == null)
        {
            return _selectedSkin != null && _selectedSkin.Id == skin.Id;
        }

        return skin.SkinPart switch
        {
            SkinPart.Back => SkinUtils.SavedValueMatchesSkin(gameData.CurrentBackPart, skin),
            SkinPart.Face => SkinUtils.SavedValueMatchesSkin(gameData.CurrentFacePart, skin),
            SkinPart.Body => SkinUtils.SavedValueMatchesSkin(gameData.CurrentBodyPart, skin),
            _ => SkinUtils.SavedValueMatchesSkin(gameData.CurrentSkinId, skin)
        };
    }

    private bool IsTransientPreviewSkin(Config.Skin skin)
    {
        return skin != null &&
               _transientPreviewSkins.TryGetValue(skin.SkinPart, out var previewSkin) &&
               ReferenceEquals(previewSkin, skin);
    }

    private bool HasTransientPreviewForPart(SkinPart part)
    {
        return _transientPreviewSkins.ContainsKey(part);
    }

    private static bool CanPreviewUnownedSkin(Config.Skin skin)
    {
        if (skin == null)
        {
            return false;
        }

        return skin.UnlockType == SkinUnlockType.Ad ||
               skin.UnlockType == SkinUnlockType.Coin ||
               skin.UnlockType == SkinUnlockType.Purchase ||
               skin.UnlockType == SkinUnlockType.Special;
    }

    private void SetTransientPreview(Config.Skin skin)
    {
        if (skin == null || !CanPreviewUnownedSkin(skin))
        {
            return;
        }

        bool previewChanged =
            !_transientPreviewSkins.TryGetValue(skin.SkinPart, out var currentPreviewSkin) ||
            !ReferenceEquals(currentPreviewSkin, skin);

        _transientPreviewSkins[skin.SkinPart] = skin;
        RememberSelectedSkin(skin);
        if (skin.SkinPart == _skinPart)
        {
            _selectedSkin = skin;
        }

        SyncTryUsingText();
        ApplyCurrentSkinToViewer(previewChanged);
    }

    private void ClearTransientPreview(SkinPart part)
    {
        if (_transientPreviewSkins.Remove(part))
        {
            SyncTryUsingText();
        }
    }

    private void ClearAllTransientPreviews()
    {
        if (_transientPreviewSkins.Count <= 0)
        {
            SyncTryUsingText();
            return;
        }

        _transientPreviewSkins.Clear();
        SyncTryUsingText();
    }

    private void SyncTryUsingText()
    {
        if (tryUsingTxt != null)
        {
            tryUsingTxt.SetActive(_transientPreviewSkins.Count > 0);
        }
    }

    private string GetEffectiveSkinValue(GameData gameData, SkinPart part)
    {
        if (_transientPreviewSkins.TryGetValue(part, out var previewSkin))
        {
            return ResolvePartSkinKey(previewSkin);
        }

        return GetSavedSkinValue(gameData, part);
    }

    private static string ResolvePartSkinKey(Config.Skin skin)
    {
        return skin != null ? skin.Id : string.Empty;
    }

    private static string GetSavedSkinValue(GameData gameData, SkinPart part)
    {
        if (gameData == null)
        {
            return string.Empty;
        }

        return part switch
        {
            SkinPart.Back => gameData.CurrentBackPart,
            SkinPart.Face => gameData.CurrentFacePart,
            SkinPart.Body => gameData.CurrentBodyPart,
            _ => gameData.CurrentSkinId
        };
    }

    private static bool ClearSavedSkinValue(GameData gameData, SkinPart part)
    {
        if (gameData == null || string.IsNullOrEmpty(GetSavedSkinValue(gameData, part)))
        {
            return false;
        }

        switch (part)
        {
            case SkinPart.Back:
                gameData.CurrentBackPart = string.Empty;
                break;
            case SkinPart.Face:
                gameData.CurrentFacePart = string.Empty;
                break;
            case SkinPart.Body:
                gameData.CurrentBodyPart = string.Empty;
                break;
            default:
                gameData.CurrentSkinId = string.Empty;
                break;
        }

        return true;
    }

    private void RefreshSelectedSkinForActiveFilter()
    {
        _selectedSkin = ResolveSelectedSkinForActiveFilter();
        SyncActionButtonsForActiveSelection();
    }

    private Config.Skin ResolveSelectedSkinForActiveFilter()
    {
        if (_selectedSkinsByPart.TryGetValue(_skinPart, out var rememberedSkin) &&
            IsSkinVisibleInCurrentRows(rememberedSkin))
        {
            return rememberedSkin;
        }

        if (_transientPreviewSkins.TryGetValue(_skinPart, out var previewSkin) &&
            IsSkinVisibleInCurrentRows(previewSkin))
        {
            return previewSkin;
        }

        for (int i = 0; i < _skinRows.Count; i++)
        {
            var row = _skinRows[i];
            if (row.RowType == SkinCatalogRowType.Skin && IsCurrentSkin(row.Skin))
            {
                return row.Skin;
            }
        }

        return null;
    }

    private bool IsSkinVisibleInCurrentRows(Config.Skin skin)
    {
        if (skin == null || skin.SkinPart != _skinPart)
        {
            return false;
        }

        for (int i = 0; i < _skinRows.Count; i++)
        {
            var row = _skinRows[i];
            if (row.RowType == SkinCatalogRowType.Skin && IsSameSkin(row.Skin, skin))
            {
                return true;
            }
        }

        return false;
    }

    private void RememberSelectedSkin(Config.Skin skin)
    {
        if (skin == null)
        {
            return;
        }

        _selectedSkinsByPart[skin.SkinPart] = skin;
    }

    private void ClearRememberedSelection(SkinPart part)
    {
        _selectedSkinsByPart.Remove(part);
    }

    private void ClearAllRememberedSelections()
    {
        _selectedSkinsByPart.Clear();
    }

    private static bool IsSameSkin(Config.Skin left, Config.Skin right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        return left != null &&
               right != null &&
               !string.IsNullOrEmpty(left.Id) &&
               string.Equals(left.Id, right.Id, StringComparison.Ordinal);
    }

    #endregion

    #region Action Button View

    private void ShowActionButtonFor(Config.Skin skin, bool allowImmediateAction = true)
    {
        HideActionButtons();
        if (skin == null)
        {
            return;
        }

        switch (skin.UnlockType)
        {
            case SkinUnlockType.Ad:
                SetActionButtonVisible(adButtonRoot, true);
                SetText(adBtnTxt, string.IsNullOrEmpty(skin.UnlockHint) ? LocalizationManager.GetTranslation("U_Skins_Btn_01") : skin.UnlockHint);
                break;
            case SkinUnlockType.Coin:
                SetActionButtonVisible(coinButtonRoot, true);
                SetText(coinBtnTxt, skin.CoinPrice > 0 ? skin.CoinPrice.ToString() : string.Empty);
                break;
            case SkinUnlockType.Purchase:
                SetActionButtonVisible(purchaseButtonRoot, true);
                SetText(purchaseBtnTxt, ResolvePurchasePrice(skin));
                break;
            case SkinUnlockType.Default:
                if (allowImmediateAction)
                {
                    GrantSkin(skin, RewardType.Other);
                }
                break;
            case SkinUnlockType.Unavailable:
                /* if (allowImmediateAction && !string.IsNullOrEmpty(skin.UnlockHint))
                {
                    SystemTips.Show(string.IsNullOrEmpty(skin.UnlockHint) ? LocalizationManager.GetTranslation("U_Skins_Btn_01") : skin.UnlockHint);
                }
                break;
 */
            case SkinUnlockType.Special:
            default:
                break;
        }

        RefreshActionButtonInteractable();
    }

    private void SyncActionButtonsForActiveSelection()
    {
        if (_selectedSkin == null || _selectedSkin.SkinPart != _skinPart || IsOwnedSkin(_selectedSkin))
        {
            HideActionButtons();
            RefreshActionButtonInteractable();
            return;
        }

        ShowActionButtonFor(_selectedSkin, false);
    }

    private string ResolvePurchasePrice(Config.Skin skin)
    {
        if (skin == null || string.IsNullOrEmpty(skin.ProductId))
        {
            return string.Empty;
        }

        return GameManager.Instance?.Purchase?.GetPrice(skin.ProductId) ?? skin.ProductId;
    }

    private void HideActionButtons()
    {
        SetActionButtonVisible(adButtonRoot, false);
        SetActionButtonVisible(coinButtonRoot, false);
        SetActionButtonVisible(purchaseButtonRoot, false);
    }

    private void RefreshActionButtonInteractable()
    {
        bool interactable = !_isSkinRewardAdRequesting && !_isSkinPurchaseRequesting;
        if (adButton != null)
        {
            adButton.Interactable = interactable;
        }

        if (coinButton != null)
        {
            coinButton.Interactable = interactable;
        }

        if (purchaseButton != null)
        {
            purchaseButton.Interactable = interactable;
        }
    }

    private void RefreshVisibleSlots(bool animateState = false)
    {
        if (skinScrollView != null)
        {
            _animateNextVisibleSlotRefresh = animateState;
            try
            {
                skinScrollView.Refresh(false);
            }
            finally
            {
                _animateNextVisibleSlotRefresh = false;
            }
        }
    }

    #endregion

    #region Reference Resolution

    private void ResolveReferences()
    {
        skinScrollView ??= GetComponentInChildren<InfiniteScrollView>(true);
        skinScrollRect ??= GetComponentInChildren<ScrollRect>(true);
        filterToggleGroup ??= GetComponentInChildren<GameObjectToggleGroup>(true);
        ownedToggle ??= FindOwnedToggle();

        if (skinScrollView == null && skinScrollRect != null)
        {
            skinScrollView = skinScrollRect.GetComponent<InfiniteScrollView>();
        }

        skinItemPrefab ??= GetComponentInChildren<SkinItem>(true);
        adButtonRoot ??= FindChildGameObjectRecursive(transform, "skin_free_btn");
        coinButtonRoot ??= FindChildGameObjectRecursive(transform, "skin_coin_btn");
        purchaseButtonRoot ??= FindChildGameObjectRecursive(transform, "skin_purchase_btn");
        tryUsingTxt ??= FindChildGameObjectRecursive(transform, "u_skins_des_06_txt");
        adButton ??= adButtonRoot != null ? adButtonRoot.GetComponentInChildren<CustomButton>(true) : null;
        coinButton ??= coinButtonRoot != null ? coinButtonRoot.GetComponentInChildren<CustomButton>(true) : null;
        purchaseButton ??= purchaseButtonRoot != null ? purchaseButtonRoot.GetComponentInChildren<CustomButton>(true) : null;
        adBtnTxt ??= adButtonRoot != null ? adButtonRoot.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        coinBtnTxt ??= coinButtonRoot != null ? coinButtonRoot.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        purchaseBtnTxt ??= purchaseButtonRoot != null ? purchaseButtonRoot.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        EnsureBoxerPreviewController();
    }

    private void EnsureBoxerPreviewController()
    {
        _boxerPreviewController ??= new ShopPanelBoxerPreviewController(
            transform,
            sceneSkinRootName,
            fallbackBaseSkinName,
            boxerPreview,
            boxerRenderTexture);
    }

    private Toggle FindOwnedToggle()
    {
        var ownedToggleRoot =
            FindChildGameObjectRecursive(transform, "UI_pifu_toggle_owned_01") ??
            FindChildGameObjectRecursive(transform, "OwnedTogglew");

        return ownedToggleRoot != null ? ownedToggleRoot.GetComponent<Toggle>() : null;
    }

    #endregion

    #region Utility

    private static void SetActionButtonVisible(GameObject target, bool visible)
    {
        if (target != null && target.activeSelf != visible)
        {
            target.SetActive(visible);

            if (visible)
            {
                target.transform.DOKill();
                target.transform.localScale = Vector3.zero;
                target.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack);
            }
            else
            {
                target.transform.DOKill();
            }
        }
    }

    private static void SetText(TextMeshProUGUI text, string value)
    {
        if (text != null)
        {
            text.text = value ?? string.Empty;
        }
    }

    private static GameObject FindChildGameObjectRecursive(Transform root, string childName)
    {
        var child = FindChildRecursive(root, childName);
        return child != null ? child.gameObject : null;
    }

    private static Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            var result = FindChildRecursive(child, childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private readonly struct SkinCatalogEntry
    {
        public readonly Config.Skin Skin;
        public readonly int SourceOrderIndex;

        public SkinCatalogEntry(Config.Skin skin, int sourceOrderIndex)
        {
            Skin = skin;
            SourceOrderIndex = sourceOrderIndex;
        }
    }

    private enum SkinCatalogRowType
    {
        Restore,
        Skin
    }

    private readonly struct SkinCatalogRow
    {
        public readonly SkinCatalogRowType RowType;
        public readonly Config.Skin Skin;
        public readonly SkinPart Part;

        private SkinCatalogRow(SkinCatalogRowType rowType, Config.Skin skin, SkinPart part)
        {
            RowType = rowType;
            Skin = skin;
            Part = part;
        }

        public static SkinCatalogRow Restore(SkinPart part)
        {
            return new SkinCatalogRow(SkinCatalogRowType.Restore, null, part);
        }

        public static SkinCatalogRow FromSkin(Config.Skin skin, SkinPart part)
        {
            return new SkinCatalogRow(SkinCatalogRowType.Skin, skin, part);
        }
    }

    #endregion
}
