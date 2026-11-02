using System;
using System.Collections.Generic;
using Bear.Logger;
using Bear.UI;
using Config.Game;
using DG.Tweening;
using Game.ConfigModule;
using GameCommon;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 礼盒促销弹窗。
/// Shop 与礼盒档位由 ChestGiftController 根据 TbShop.ChestPrefab 配置注入；Prefab 只负责视觉与组件绑定。
/// </summary>
public partial class ChestGiftPopup : GiftPopup, IDebuger
{
    #region 常量配置

    private const float RewardItemSpacing = 24f;
    private const float RewardItemShowDuration = 0.2f;
    private const float RewardContainerShowDuration = 0.3f;
    private const float StartupCloseDuration = 0.3f;
    private const int RewardItemShowDelayFrames = 10;
    private const string BoxerSceneRootName = "Skin";
    private const string BoxerFallbackSkinName = "box_original";

    private static readonly Config.Game.GameProps[] DisplayRewardOrder =
    {
        Config.Game.GameProps.Coin,
        Config.Game.GameProps.Skin,
    };

    #endregion

    #region Inspector 绑定

    [Header("奖励展示")]
    [Tooltip("奖励展示容器；只清理 ChestGiftPopup 在运行时创建的 ChestGiftItem，不修改容器内已有美术节点。")]
    [SerializeField] private RectTransform itemContainer;

    [Tooltip("奖励展示项 Prefab；运行时按当前 Shop 的 Coin 与 Skin 奖励动态创建。")]
    [SerializeField] private ChestGiftItem itemPrefab;

    /// <summary>
    /// 皮肤预览区域根节点；由 Prefab Inspector 显式绑定，无有效 Skin 或场景宿主未就绪时整体隐藏。
    /// </summary>
    [Header("皮肤预览")]
    [Tooltip("皮肤预览区域根节点；需在 ChestGiftPopup 母版绑定 skinPreview。")]
    [SerializeField] private GameObject skinPreviewRoot;

    /// <summary>
    /// Boxer 预览画面；由 Prefab Inspector 显式绑定，其 RenderTexture 交给共享预览控制器使用。
    /// </summary>
    [Tooltip("皮肤预览 RawImage；需在 ChestGiftPopup 母版绑定 Boxer_Rt。")]
    [SerializeField] private RawImage boxerPreview;

    /// <summary>
    /// 礼包主动关闭按钮；由 Prefab Inspector 显式绑定，购买与奖励动画期间由 ClickBlock 拦截输入，回调仍保留状态校验。
    /// </summary>
    [Header("交互")]
    [Tooltip("主动关闭按钮；需在 ChestGiftPopup 母版绑定 Close_Btn。")]
    [SerializeField] private CustomButton closeBtn;

    /// <summary>
    /// 购买请求及成功奖励动画期间的全屏点击阻挡；由 Prefab Inspector 显式绑定，只切换已有对象，不执行节点查找。
    /// </summary>
    [Tooltip("全屏点击阻挡；需在 ChestGiftPopup 母版绑定默认隐藏且位于内容最上层的 ClickBlock。")]
    [SerializeField] private GameObject clickBlock;

    #endregion

    #region 运行时状态

    private ChestGiftController.ChestGiftOffer _offer;
    private readonly List<ChestGiftItem> _rewardItems = new List<ChestGiftItem>();
    private Sequence _rewardItemShowSequence;
    private int _rewardItemShowStartFrame = -1;
    private bool _isRewardItemShowHidden;
    private ShopPanelBoxerPreviewController _boxerPreviewController;
    private float _nextCountdownRefreshTime;
    private int _viewVersion;
    private bool _isWaitingForRewardPopup;
    private RectTransform _startupCloseTarget;
    private Sequence _startupCloseSequence;
    private bool _useStartupCloseAnimation;
    private bool _isClosingToEntry;

    #endregion

    #region 类型与商品信息

    /// <summary>当前由 ChestGiftController 注入的 Shop Id；弹窗尚未配置时返回空字符串。</summary>
    public string ShopId => _offer.Shop?.Id ?? string.Empty;

    /// <summary>向 GiftManager 声明当前弹窗属于 ChestGift 类型。</summary>
    public override GiftPopupType PopupType => GiftPopupType.ChestGift;

    #endregion

    #region 生命周期

    public override void OnOpen()
    {
        base.OnOpen();
        ClearStartupCloseAnimation();
        _viewVersion++;
        _isWaitingForRewardPopup = false;
        _isRewardItemShowHidden = false;
        CloseSkinPreview();
        _offer = default;
        RefreshTitle();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SetClickBlocked(false);
        BindButtons();
        ClearRewardItems();
        SetOfferUnavailable();
        RefreshCountdown(force: true);
    }

    /// <summary>
    /// 用途：弹窗从上层 UI 遮挡中重新显示时恢复当前礼包的皮肤预览。
    /// 调用边界：由 UIManager 生命周期调用；只恢复临时预览，不授予皮肤或写入存档。
    /// </summary>
    public override void OnShow()
    {
        base.OnShow();
        _startupCloseSequence?.Play();
        RefreshTitle();
        _isRewardItemShowHidden = false;
        if (!_isClosingToEntry && _rewardItemShowStartFrame < 0)
            _rewardItemShowSequence?.Play();
        if (_offer.Shop != null)
            RefreshSkinPreview();
    }

    /// <summary>
    /// 用途：弹窗被其它 UI 遮挡时关闭场景预览并释放临时部件。
    /// 调用边界：由 UIManager 生命周期调用；礼包商品状态和奖励项保持不变。
    /// </summary>
    public override void OnHide()
    {
        _startupCloseSequence?.Pause();
        _isRewardItemShowHidden = true;
        _rewardItemShowSequence?.Pause();
        CloseSkinPreview();
        base.OnHide();
    }

    public override void OnClose()
    {
        ClearStartupCloseAnimation();
        _viewVersion++;
        _isWaitingForRewardPopup = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SetClickBlocked(false);
        UnbindButtons();
        ClearRewardItems();
        CloseSkinPreview();
        _offer = default;
        base.OnClose();
    }

    private void OnDestroy()
    {
        ClearStartupCloseAnimation();
        StopRewardItemShowAnimation();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        _boxerPreviewController?.Dispose();
        _boxerPreviewController = null;
    }

    private void Update()
    {
        if (IsOpen && !_isRewardItemShowHidden && _rewardItemShowStartFrame >= 0 &&
            Time.frameCount >= _rewardItemShowStartFrame)
        {
            _rewardItemShowStartFrame = -1;
            _rewardItemShowSequence?.Play();
        }

        if (!IsOpen || Time.unscaledTime < _nextCountdownRefreshTime)
            return;

        RefreshCountdown(force: false);
    }

    #endregion

    #region 商品展示

    /// <summary>
    /// 注入本次打开对应的 Shop 与轮换档位，并刷新商品展示。
    /// 调用边界：只允许 ChestGiftController 在 UIManager.OpenUI 返回后同步调用；不得由 Prefab Inspector 配置商品身份。
    /// </summary>
    internal void ConfigureOffer(ChestGiftController.ChestGiftOffer offer)
    {
        _offer = offer;
        RefreshOffer();
    }

    private void RefreshOffer()
    {
        ClearRewardItems();
        CloseSkinPreview();

        Config.Shop shop = _offer.Shop;
        RefreshTitle();
        if (shop == null)
        {
            this.LogWarning($"[ChestGiftPopup] Shop offer was not configured. giftIndex:{_offer.GiftIndex}");
            SetOfferUnavailable();
            return;
        }

        if (shop.BuyTimes != 1)
        {
            this.LogError($"[ChestGiftPopup] BuyTimes must be 1. shopId:{ShopId}, buyTimes:{shop.BuyTimes}");
            SetOfferUnavailable();
            return;
        }

        RefreshSkinPreview();
        RefreshRewardItems(shop);

        Config.Products product = _offer.Product;
        if (product == null)
        {
            this.LogError($"[ChestGiftPopup] Product mapping not found. shopId:{ShopId}, giftIndex:{_offer.GiftIndex}");
            SetOfferUnavailable();
            return;
        }

        if (!PurchaseManager.TryGetProductId(product.Id, out var platformProductId) ||
            PurchaseManager.IsPlaceholderSku(platformProductId.Sku))
        {
            this.LogError($"[ChestGiftPopup] Platform SKU is empty or a placeholder. product:{product.Id}");
            SetOfferUnavailable();
            return;
        }

        PurchaseManager purchaseManager = GameManager.Instance?.Purchase;
        string localizedPrice = purchaseManager?.GetPrice(product.Id, nameof(ChestGiftPopup)) ?? string.Empty;
        var productInfo = purchaseManager?.GetProductInfo(product.Id, nameof(ChestGiftPopup));
        bool offerAvailable = IsOfferAvailable();
        bool canShowOriginalPrice = purchaseManager?.IsInitialized == true &&
                                    offerAvailable &&
                                    productInfo != null &&
                                    !string.IsNullOrWhiteSpace(productInfo.Details.Price);
        string originalPrice = canShowOriginalPrice && GiftPriceFormatter.TryResolveOriginalPrice(
            purchaseManager,
            product.Id,
            shop.Discount,
            nameof(ChestGiftPopup),
            out string resolvedOriginalPrice)
            ? resolvedOriginalPrice
            : string.Empty;
        SetDiscount(shop.Discount);
        SetPriceText(localizedPrice, originalPrice);
        // 与 ShopItem 的价格缺失路径对齐：不以取价结果阻断点击，后续由公共购买流程决定失败或发起请求。
        SetPurchaseInteractable(purchaseManager != null && offerAvailable);
    }

    /// <summary>
    /// 使用当前 Shop.GoodsName 多语言键填充已绑定的 title_txt；无商品或空键时清空，避免残留上一档标题。
    /// 仅由运行时商品刷新和显示生命周期调用，不读取购买状态或改变商品配置。
    /// </summary>
    private void RefreshTitle()
    {
        if (UPopuppackTitle01Txt == null)
            return;

        string nameKey = _offer.Shop?.GoodsName;
        UPopuppackTitle01Txt.text = string.IsNullOrWhiteSpace(nameKey)
            ? string.Empty
            : I2.Loc.LocalizationManager.GetTranslation(nameKey) ?? string.Empty;
    }

    private void SetOfferUnavailable()
    {
        SetDiscount(0);
        SetPriceText(string.Empty);
        SetPurchaseInteractable(false);
    }

    private void SetDiscount(int discount)
    {
        bool visible = discount != 0;
        if (UIShop2Dazhe01Img != null)
            UIShop2Dazhe01Img.gameObject.SetActive(visible);

        if (UShopDes03Txt != null)
        {
            UShopDes03Txt.text = visible ? string.Format("{0}%\noff", discount) : string.Empty;
            UShopDes03Txt.gameObject.SetActive(visible);
        }
    }

    private void SetPriceText(string localizedPrice, string originalPrice = "")
    {
        if (UiLibao205Txt != null)
            UiLibao205Txt.text = localizedPrice ?? string.Empty;

        if (UiLibao204Txt != null)
        {
            bool showOriginalPrice = !string.IsNullOrWhiteSpace(originalPrice);
            UiLibao204Txt.text = showOriginalPrice ? originalPrice : string.Empty;
            UiLibao204Txt.gameObject.SetActive(showOriginalPrice);
        }
    }

    private void RefreshRewardItems(Config.Shop shop)
    {
        if (shop.Rewards == null || shop.Rewards.Count == 0)
            return;

        if (itemContainer == null || itemPrefab == null)
        {
            this.LogError($"[ChestGiftPopup] Reward item binding is missing. shopId:{ShopId}, giftIndex:{_offer.GiftIndex}");
            return;
        }

        foreach (Config.Game.GameProps prop in DisplayRewardOrder)
        {
            foreach (Config.ShopReward reward in shop.Rewards)
            {
                if (reward.Key != prop) continue;
                Config.ParamBase rewardValue = reward.Value;
                ChestGiftItem item = Instantiate(itemPrefab, itemContainer, false);
                if (!item.Refresh(prop, rewardValue))
                {
                    item.gameObject.SetActive(false);
                    Destroy(item.gameObject);
                    continue;
                }

                item.gameObject.SetActive(true);
                _rewardItems.Add(item);
            }
        }

        LayoutRewardItems();
        PlayRewardItemShowAnimation();
    }

    private void LayoutRewardItems()
    {
        if (_rewardItems.Count == 0)
            return;

        RectTransform prefabRect = itemPrefab.transform as RectTransform;
        float itemWidth = prefabRect != null && prefabRect.rect.width > 0f
            ? prefabRect.rect.width
            : 0f;
        float step = itemWidth + RewardItemSpacing;
        float startX = -step * (_rewardItems.Count - 1) * 0.5f;

        for (int index = 0; index < _rewardItems.Count; index++)
        {
            RectTransform itemRect = _rewardItems[index].transform as RectTransform;
            if (itemRect == null)
                continue;

            itemRect.anchorMin = new Vector2(0.5f, 0.5f);
            itemRect.anchorMax = new Vector2(0.5f, 0.5f);
            itemRect.pivot = new Vector2(0.5f, 0.5f);
            itemRect.anchoredPosition = new Vector2(startX + step * index, 0f);
        }
    }

    private void ClearRewardItems()
    {
        StopRewardItemShowAnimation();
        for (int index = 0; index < _rewardItems.Count; index++)
        {
            if (_rewardItems[index] != null)
            {
                _rewardItems[index].gameObject.SetActive(false);
                Destroy(_rewardItems[index].gameObject);
            }
        }

        _rewardItems.Clear();
    }

    /// <summary>
    /// 新建奖励项后等待 10 个 Unity 帧，再按布局顺序逐项缩放；容器同时以 0.3 秒 OutBack 展开 X 轴。
    /// 使用非缩放时间，允许玩法暂停时播放；不参与购买锁定或奖励交付。
    /// </summary>
    private void PlayRewardItemShowAnimation()
    {
        StopRewardItemShowAnimation();
        if (_rewardItems.Count == 0)
            return;

        _rewardItemShowStartFrame = Time.frameCount + RewardItemShowDelayFrames;
        _rewardItemShowSequence = DOTween.Sequence().SetUpdate(true).Pause();
        foreach (ChestGiftItem item in _rewardItems)
        {
            item.transform.localScale = Vector3.zero;
            _rewardItemShowSequence.Append(
                item.transform.DOScale(Vector3.one, RewardItemShowDuration).SetEase(Ease.Linear));
        }

        Vector3 containerScale = itemContainer.localScale;
        containerScale.x = 0f;
        itemContainer.localScale = containerScale;
        _rewardItemShowSequence.Insert(0f,
            itemContainer.DOScaleX(1f, RewardContainerShowDuration).SetEase(Ease.OutBack));
        _rewardItemShowSequence.OnComplete(() => _rewardItemShowSequence = null);
    }

    /// <summary>刷新、关闭或销毁弹窗前终止入场序列，不强制完成动画或触发业务回调。</summary>
    private void StopRewardItemShowAnimation()
    {
        bool restoreContainerScale = _rewardItemShowSequence != null;
        _rewardItemShowSequence?.Kill();
        _rewardItemShowSequence = null;
        _rewardItemShowStartFrame = -1;
        if (restoreContainerScale && itemContainer != null)
        {
            Vector3 containerScale = itemContainer.localScale;
            containerScale.x = 1f;
            itemContainer.localScale = containerScale;
        }
    }

    #endregion

    #region 皮肤换装预览

    private void RefreshSkinPreview()
    {
        if (!TryGetPreviewSkins(out List<Config.Skin> previewSkins))
        {
            CloseSkinPreview();
            return;
        }

        if (!IsSkinPreviewSceneLoaded())
        {
            SetSkinPreviewVisible(false);
            return;
        }

        // 礼包预览只展示奖励 Skin：非奖励部位传空，FullBody 为空时由控制器恢复基础 Boxer Skin。
        string backValue = string.Empty;
        string faceValue = string.Empty;
        string bodyValue = string.Empty;
        string fullBodyValue = string.Empty;

        // 不同部位同时预览；同部位采用配置中的第一件，所有奖励项仍独立展示和发放。
        foreach (Config.Skin previewSkin in previewSkins)
        {
            switch (previewSkin.SkinPart)
            {
                case SkinPart.Back:
                    if (string.IsNullOrEmpty(backValue)) backValue = previewSkin.Id;
                    break;
                case SkinPart.Face:
                    if (string.IsNullOrEmpty(faceValue)) faceValue = previewSkin.Id;
                    break;
                case SkinPart.Body:
                    if (string.IsNullOrEmpty(bodyValue)) bodyValue = previewSkin.Id;
                    break;
                case SkinPart.FullBody:
                    if (string.IsNullOrEmpty(fullBodyValue)) fullBodyValue = previewSkin.Id;
                    break;
                default:
                    CloseSkinPreview();
                    this.LogWarning($"[ChestGiftPopup] Unsupported preview SkinPart. skinId:{previewSkin.Id}, part:{previewSkin.SkinPart}");
                    return;
            }
        }

        if (!TryEnsureSkinPreviewController())
        {
            SetSkinPreviewVisible(false);
            return;
        }

        _boxerPreviewController.Open();
        _boxerPreviewController.Apply(
            backValue,
            faceValue,
            bodyValue,
            fullBodyValue,
            previewSkins[0].SkinPart);
        SetSkinPreviewVisible(true);
    }

    /// <summary>
    /// Main 场景在启动礼包之后完成加载时，重新绑定场景 Skin 预览宿主。
    /// 调用边界：仅处理当前仍打开且可见的 ChestGiftPopup；不重新配置商品、不写存档。
    /// </summary>
    private void OnSceneLoaded(Scene _, LoadSceneMode __)
    {
        if (IsOpen && IsVisible && _offer.Shop != null)
            RefreshSkinPreview();
    }

    /// <summary>
    /// 检查 ShopPanel 共用的 Boxer 场景预览根是否已经随 Main 场景加载完成。
    /// 调用边界：仅用于阻止启动礼盒在 Loading 场景阶段提前显示空白预览区，不创建或修改场景对象。
    /// </summary>
    private static bool IsSkinPreviewSceneLoaded()
    {
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                if (roots[rootIndex] != null &&
                    string.Equals(roots[rootIndex].name, BoxerSceneRootName, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryGetPreviewSkins(out List<Config.Skin> previewSkins)
    {
        previewSkins = new List<Config.Skin>();
        if (_offer.Shop?.Rewards == null) return false;
        foreach (Config.ShopReward reward in _offer.Shop.Rewards)
        {
            if (reward.Key == Config.Game.GameProps.Skin &&
                reward.Value is Config.StringParam skinParam &&
                SkinUtils.TryGetSkin(skinParam.Value, out Config.Skin skin))
                previewSkins.Add(skin);
        }
        return previewSkins.Count > 0;
    }

    private bool TryEnsureSkinPreviewController()
    {
        if (_boxerPreviewController != null)
            return true;

        if (skinPreviewRoot == null ||
            boxerPreview == null ||
            boxerPreview.texture is not RenderTexture renderTexture)
        {
            this.LogWarning($"[ChestGiftPopup] Skin preview binding is missing. shopId:{ShopId}");
            return false;
        }

        _boxerPreviewController = new ShopPanelBoxerPreviewController(
            transform,
            BoxerSceneRootName,
            BoxerFallbackSkinName,
            boxerPreview,
            renderTexture);
        return true;
    }

    private void CloseSkinPreview()
    {
        SetSkinPreviewVisible(false);
        _boxerPreviewController?.Dispose();
        _boxerPreviewController = null;
    }

    private void SetSkinPreviewVisible(bool visible)
    {
        if (skinPreviewRoot != null && skinPreviewRoot.activeSelf != visible)
            skinPreviewRoot.SetActive(visible);
    }

    #endregion

    #region 商品状态

    private bool IsOfferAvailable()
    {
        return GiftManager.Instance.ChestGift.IsOfferAvailable(_offer);
    }

    #endregion

    #region 启动礼包收回入口

    /// <summary>
    /// 仅由主页启动自动弹出路径注入本次关闭目标；普通入口不调用，购买成功不使用该动画。
    /// 目标来自 StartPanel Inspector 显式绑定的 DailyChestGiftEnter；目标不可用时仍在当前位置完成缩放再关闭。
    /// </summary>
    internal void ConfigureStartupCloseTarget(RectTransform target)
    {
        _useStartupCloseAnimation = true;
        _startupCloseTarget = target;
    }

    private bool TryCloseToStartupEntry()
    {
        if (!_useStartupCloseAnimation)
            return false;

        RectTransform popupRect = transform as RectTransform;
        if (popupRect == null)
        {
            this.LogWarning("[ChestGiftPopup] StartupClose skipped because the popup root is not a RectTransform.");
            return false;
        }

        Vector3 endPoint = popupRect.position;
        bool hasEntryTarget = TryResolveStartupCloseEndPoint(popupRect, out Vector3 resolvedEndPoint);
        if (hasEntryTarget)
            endPoint = resolvedEndPoint;
        else
            this.LogWarning("[ChestGiftPopup] StartupClose target unavailable; playing scale at the current center.");

        _useStartupCloseAnimation = false;
        _startupCloseTarget = null;
        _isClosingToEntry = true;
        _rewardItemShowSequence?.Pause();
        _rewardItemShowStartFrame = -1;

        SetPurchaseInteractable(false);
        if (closeBtn != null)
            closeBtn.Interactable = false;
        SetClickBlocked(true);

        int viewVersion = _viewVersion;
        _startupCloseSequence = DOTween.Sequence().SetUpdate(true);
        _startupCloseSequence.Append(popupRect.DOMove(endPoint, StartupCloseDuration).SetEase(Ease.InBack));
        _startupCloseSequence.Join(popupRect.DOScale(Vector3.zero, StartupCloseDuration).SetEase(Ease.InBack));
        _startupCloseSequence.OnComplete(() =>
        {
            _startupCloseSequence = null;
            if (this != null && IsOpen && viewVersion == _viewVersion)
                GiftManager.Instance.Close(this);
        });
        return true;
    }

    /// <summary>
    /// 将主页入口中心转换为弹窗父节点的世界坐标；失败只影响移动落点，不得跳过启动关闭缩放。
    /// </summary>
    private bool TryResolveStartupCloseEndPoint(RectTransform popupRect, out Vector3 endPoint)
    {
        endPoint = popupRect.position;
        if (_startupCloseTarget == null || !_startupCloseTarget.gameObject.activeInHierarchy ||
            !(popupRect.parent is RectTransform parentRect))
            return false;

        Canvas targetCanvas = _startupCloseTarget.GetComponentInParent<Canvas>();
        Canvas popupCanvas = GetComponentInParent<Canvas>();
        if (targetCanvas == null || popupCanvas == null)
            return false;

        targetCanvas = targetCanvas.rootCanvas;
        popupCanvas = popupCanvas.rootCanvas;
        Camera targetCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : targetCanvas.worldCamera;
        Camera popupCamera = popupCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : popupCanvas.worldCamera;
        Vector3 targetCenter = _startupCloseTarget.TransformPoint(_startupCloseTarget.rect.center);
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(targetCamera, targetCenter);
        return RectTransformUtility.ScreenPointToWorldPointInRectangle(
            parentRect,
            screenPoint,
            popupCamera,
            out endPoint);
    }

    private void ClearStartupCloseAnimation()
    {
        _startupCloseSequence?.Kill();
        _startupCloseSequence = null;
        _useStartupCloseAnimation = false;
        _startupCloseTarget = null;
        _isClosingToEntry = false;
    }

    #endregion

    #region 按钮与购买

    private void BindButtons()
    {
        UnbindButtons();

        if (UIZhuyeDa01Btn != null)
            UIZhuyeDa01Btn.OnClick += OnPurchaseClick;

        if (closeBtn != null)
            closeBtn.OnClick += OnCloseClick;
    }

    private void UnbindButtons()
    {
        if (UIZhuyeDa01Btn != null)
            UIZhuyeDa01Btn.OnClick -= OnPurchaseClick;

        if (closeBtn != null)
            closeBtn.OnClick -= OnCloseClick;
    }

    private void OnCloseClick(CustomButton _)
    {
        ChestGiftController controller = GiftManager.Instance.ChestGift;
        if (IsInteractionLocked(controller))
            return;

        if (!TryCloseToStartupEntry())
            GiftManager.Instance.Close(this);
    }

    private void OnPurchaseClick(CustomButton _)
    {
        ChestGiftController controller = GiftManager.Instance.ChestGift;
        if (IsInteractionLocked(controller))
            return;

        PurchaseManager purchaseManager = GameManager.Instance?.Purchase;
        if (purchaseManager == null)
        {
            this.LogError("[ChestGiftPopup] Purchase ignored. PurchaseManager is null.");
            return;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            purchaseManager.OnPurchaseFailed();
            return;
        }

        if (!IsOfferAvailable())
            return;

        int viewVersion = _viewVersion;
        SetClickBlocked(true);
        if (!controller.TryPurchase(
                _offer,
                success => OnPurchaseResult(viewVersion, success)))
        {
            RestorePurchaseInteraction();
        }
    }

    private void OnPurchaseResult(int viewVersion, bool success)
    {
        if (this == null || !IsOpen || viewVersion != _viewVersion)
            return;

        if (success)
        {
            ShowPurchaseRewardPopup(viewVersion);
            return;
        }

        RestorePurchaseInteraction();
    }

    /// <summary>
    /// 用途：在 ChestGift 奖励已经入包后打开通用奖励展示弹窗。
    /// 调用边界：只由本弹窗的购买成功回调调用；通用弹窗关闭前保持 ChestGiftPopup 输入锁定。
    /// 注意事项：使用预发奖模式避免重复入包；ChestGiftPopup 自身不再播放飞金币效果。
    /// </summary>
    private void ShowPurchaseRewardPopup(int viewVersion)
    {
        _isWaitingForRewardPopup = true;
        SetClickBlocked(true);

        List<Config.ShopReward> rewards = _offer.Shop?.Rewards;
        if (rewards == null || rewards.Count == 0)
        {
            this.LogError("[ChestGiftPopup] CommonGetRewardPopup skipped because the purchased Shop has no rewards.");
            CloseAfterRewardPopup(viewVersion);
            return;
        }

        CommonGetRewardPopup rewardPopup = CommonGetRewardPopup.Create(
            rewards,
            multiple: 1,
            callback: () => CloseAfterRewardPopup(viewVersion),
            grantNormalReward: false);
        if (rewardPopup == null)
        {
            this.LogError("[ChestGiftPopup] CommonGetRewardPopup creation failed after purchase success.");
            CloseAfterRewardPopup(viewVersion);
        }
    }

    private void CloseAfterRewardPopup(int viewVersion)
    {
        if (this == null || !IsOpen || viewVersion != _viewVersion)
            return;

        _isWaitingForRewardPopup = false;
        GiftManager.Instance.Close(this);
    }

    private bool IsInteractionLocked(ChestGiftController controller)
    {
        return controller.IsActionInProgress || _isWaitingForRewardPopup || _isClosingToEntry;
    }

    private void RestorePurchaseInteraction()
    {
        SetClickBlocked(false);
        SetPurchaseInteractable(GameManager.Instance?.Purchase != null && IsOfferAvailable());
    }

    private void SetPurchaseInteractable(bool interactable)
    {
        if (UIZhuyeDa01Btn != null)
            UIZhuyeDa01Btn.Interactable = interactable;
    }

    private void SetClickBlocked(bool isBlocked)
    {
        if (clickBlock != null && clickBlock.activeSelf != isBlocked)
            clickBlock.SetActive(isBlocked);
    }

    #endregion

    #region 本地倒计时

    private void RefreshCountdown(bool force)
    {
        if (!force && Time.unscaledTime < _nextCountdownRefreshTime)
            return;

        _nextCountdownRefreshTime = Time.unscaledTime + 1f;
        if (UiLibao201Txt == null)
            return;

        TimeSpan remaining = GiftManager.Instance.GetDailyCountdown(GiftPopupType.ChestGift);
        UiLibao201Txt.text = $"{(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}";
    }

    #endregion

    /// <summary>
    /// 打开当前 Daily Schedule 对应的礼盒变体。
    /// 调用边界：仅在 UI、Schedule 与本地配置表初始化完成后调用；资源名读取 Shop.ChestPrefab。
    /// </summary>
    public static ChestGiftPopup Create()
    {
        return GiftManager.Instance.Open(GiftPopupType.ChestGift) as ChestGiftPopup;
    }

    /// <summary>按指定 1～5 档打开对应 Resources 礼盒变体。</summary>
    public static ChestGiftPopup Create(int targetGiftIndex)
    {
        return GiftManager.Instance.Open(GiftPopupType.ChestGift, targetGiftIndex) as ChestGiftPopup;
    }
}
