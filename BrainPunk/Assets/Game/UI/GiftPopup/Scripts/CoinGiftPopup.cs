using System;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;

/// <summary>
/// 金币不足弹窗视图。
/// 展示配置驱动的金币广告入口、首购/常规礼包二选一商品和商店入口；业务状态与奖励交付由 CoinGiftController 负责。
/// </summary>
public partial class CoinGiftPopup : GiftPopup, IDebuger
{
    #region Inspector 配置

    [Header("Coin Gift")]
    [Tooltip("激励广告金币奖励对应的 Luban TbShop Id；奖励数量读取该 Shop 的 Coin 配置。")]
    [SerializeField] private string rewardAdShopId = "20000";

    [Tooltip("超值首购对应的 Luban TbShop Id；必须配置为限购一次的 280 金币商品。")]
    [SerializeField] private string firstPurchaseShopId = "20013";

    [Tooltip("首购完成后替换显示的常规礼包 TbShop Id；当前需求对应现有礼包 2。")]
    [SerializeField] private string normalPurchaseShopId = "20003";

    [Tooltip("首购状态的商品展示项；负责按钮、数量、价格、原价和折扣显示。")]
    [SerializeField] private CoinGiftItem firstPurchaseItem;

    [Tooltip("常规礼包状态的商品展示项；负责按钮、数量、价格、原价和折扣显示。")]
    [SerializeField] private CoinGiftItem normalPurchaseItem;

    [Header("Reward Anim")]
    [Tooltip("广告或购买成功后播放金币飞行动画的公共控制器；由 CoinGiftPopup Prefab Inspector 直接绑定。")]
    [SerializeField] private CommonUIRewardAnimCtrl rewardAnimCtrl;

    [Tooltip("少于 250 金币时生成的飞行动画金币数量。")]
    [SerializeField] private int minCoinRewardAnimCount = 8;

    [Tooltip("250 至 499 金币时生成的飞行动画金币数量。")]
    [SerializeField] private int midCoinRewardAnimCount = 10;

    [Tooltip("500 至 999 金币时生成的飞行动画金币数量。")]
    [SerializeField] private int largeCoinRewardAnimCount = 12;

    [Tooltip("1000 至 1999 金币时生成的飞行动画金币数量。")]
    [SerializeField] private int hugeCoinRewardAnimCount = 14;

    [Tooltip("2000 金币及以上时生成的飞行动画金币数量。")]
    [SerializeField] private int maxCoinRewardAnimCount = 16;

    /// <summary>
    /// 超值首购对应的 TbShop Id。
    /// 调用边界：供 CoinGiftController 从 Resources Prefab 核对配置，不应在运行时改写。
    /// </summary>
    public string FirstPurchaseShopId => firstPurchaseShopId;

    /// <summary>
    /// 首购完成后展示的常规礼包 TbShop Id。
    /// 调用边界：供 CoinGiftController 从 Resources Prefab 核对配置，不应在运行时改写。
    /// </summary>
    public string NormalPurchaseShopId => normalPurchaseShopId;

    /// <summary>向 GiftManager 声明当前弹窗属于 CoinGift 类型。</summary>
    public override GiftPopupType PopupType => GiftPopupType.CoinGift;

    #endregion

    #region 运行时状态

    private CoinGiftController.CoinGiftOffer _currentOffer;
    private bool _hasOffer;
    private bool _showFirstPurchase;
    private bool _isRewardAnimating;
    private float _nextCountdownRefreshTime;
    private int _viewVersion;

    #endregion

    #region 生命周期

    public override void OnCreate()
    {
        base.OnCreate();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        _viewVersion++;
        BindButtons();
        RefreshAdOffer();
        RefreshPurchaseOffer();
        RefreshCountdown(force: true);
        RefreshButtonStates();
    }

    public override void OnClose()
    {
        _viewVersion++;
        UnbindButtons();
        _hasOffer = false;
        _isRewardAnimating = false;
        base.OnClose();
    }

    private void Update()
    {
        if (!IsOpen || !_showFirstPurchase || Time.unscaledTime < _nextCountdownRefreshTime)
            return;

        RefreshCountdown(force: false);
    }

    #endregion

    #region 展示刷新

    private void RefreshAdOffer()
    {
        if (UiLibao101Txt != null)
        {
            UiLibao101Txt.text = GiftManager.Instance.CoinGift.TryGetConfiguredCoinReward(
                rewardAdShopId,
                out int coinCount)
                ? $"+{coinCount}"
                : string.Empty;
        }
    }

    private void RefreshPurchaseOffer()
    {
        CoinGiftController controller = GiftManager.Instance.CoinGift;
        _hasOffer = controller.TryResolveCurrentOffer(
            firstPurchaseShopId,
            normalPurchaseShopId,
            out _currentOffer,
            out _showFirstPurchase);

        if (FirstPurchaseState != null)
            FirstPurchaseState.SetActive(_showFirstPurchase);

        if (NormalPurchaseState != null)
            NormalPurchaseState.SetActive(!_showFirstPurchase);

        string localizedPrice = _hasOffer
            ? controller.GetLocalizedPrice(_currentOffer)
            : string.Empty;
        string originalPrice = TryResolveOriginalPrice(out string resolvedOriginalPrice)
            ? resolvedOriginalPrice
            : string.Empty;

        if (_showFirstPurchase)
        {
            normalPurchaseItem?.Clear();
            controller.TryGetConfiguredCoinReward(firstPurchaseShopId, out int coinCount);
            controller.TryGetConfiguredDiscount(firstPurchaseShopId, out int discount);
            firstPurchaseItem?.Refresh(
                coinCount,
                localizedPrice,
                discount,
                originalPrice);
        }
        else
        {
            firstPurchaseItem?.Clear();
            controller.TryGetConfiguredCoinReward(normalPurchaseShopId, out int coinCount);
            controller.TryGetConfiguredDiscount(normalPurchaseShopId, out int discount);
            normalPurchaseItem?.Refresh(coinCount, localizedPrice, discount, originalPrice);
        }
    }

    private void RefreshButtonStates()
    {
        CoinGiftController controller = GiftManager.Instance.CoinGift;
        bool idle = !controller.IsActionInProgress && !_isRewardAnimating;
        bool offerAvailable = _hasOffer && controller.IsOfferAvailable(_currentOffer);
        bool rewardAdAvailable = controller.TryGetConfiguredCoinReward(rewardAdShopId, out _);

        if (UILibao1Libao101Btn != null)
            UILibao1Libao101Btn.Interactable = idle && rewardAdAvailable;

        firstPurchaseItem?.SetItemInteractable(idle && _showFirstPurchase && offerAvailable);
        normalPurchaseItem?.SetItemInteractable(idle && !_showFirstPurchase && offerAvailable);

        if (UTopupcoins01Btn != null)
            UTopupcoins01Btn.Interactable = idle;

        if (CloseBtn != null)
            CloseBtn.Interactable = idle;
    }

    #endregion

    #region 按钮交互

    private void BindButtons()
    {
        UnbindButtons();

        firstPurchaseItem?.Init();
        normalPurchaseItem?.Init();

        if (UILibao1Libao101Btn != null)
            UILibao1Libao101Btn.OnClick += OnRewardAdClick;

        if (firstPurchaseItem != null)
            firstPurchaseItem.PurchaseClicked += OnPurchaseClick;

        if (normalPurchaseItem != null)
            normalPurchaseItem.PurchaseClicked += OnPurchaseClick;

        if (UTopupcoins01Btn != null)
            UTopupcoins01Btn.OnClick += OnShopClick;

        if (CloseBtn != null)
            CloseBtn.OnClick += OnCloseClick;
    }

    private void UnbindButtons()
    {
        if (UILibao1Libao101Btn != null)
            UILibao1Libao101Btn.OnClick -= OnRewardAdClick;

        if (firstPurchaseItem != null)
            firstPurchaseItem.PurchaseClicked -= OnPurchaseClick;

        if (normalPurchaseItem != null)
            normalPurchaseItem.PurchaseClicked -= OnPurchaseClick;

        if (UTopupcoins01Btn != null)
            UTopupcoins01Btn.OnClick -= OnShopClick;

        if (CloseBtn != null)
            CloseBtn.OnClick -= OnCloseClick;
    }

    private void OnRewardAdClick(CustomButton _)
    {
        if (GiftManager.Instance.CoinGift.IsActionInProgress)
            return;

        int viewVersion = _viewVersion;
        Transform rewardSource = UILibao1Libao101Btn != null
            ? UILibao1Libao101Btn.transform
            : transform;
        if (!GiftManager.Instance.CoinGift.TryShowRewardAd(
                rewardAdShopId,
                (success, coinCount) => OnRewardAdCompleted(viewVersion, rewardSource, success, coinCount)))
        {
            RefreshButtonStates();
            return;
        }

        RefreshButtonStates();
    }

    private void OnRewardAdCompleted(int viewVersion, Transform rewardSource, bool success, int coinCount)
    {
        if (this == null || !IsOpen || viewVersion != _viewVersion)
            return;

        if (success)
        {
            PlayRewardAnimationAsync(
                viewVersion,
                rewardSource,
                coinCount,
                "RewardAd").Forget();
            return;
        }

        if (!success)
            this.LogWarning("[CoinGiftPopup] Reward ad did not deliver Coin.");

        RefreshButtonStates();
    }

    private void OnPurchaseClick(CoinGiftItem item)
    {
        if (GiftManager.Instance.CoinGift.IsActionInProgress)
            return;

        PurchaseManager purchaseManager = GameManager.Instance?.Purchase;
        if (purchaseManager == null)
        {
            this.LogError("[CoinGiftPopup] Purchase ignored. PurchaseManager is null.");
            return;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            purchaseManager.OnPurchaseFailed();
            return;
        }

        if (!_hasOffer)
            return;

        int viewVersion = _viewVersion;
        int coinCount = _currentOffer.CoinCount;
        Transform rewardSource = item?.PurchaseButton != null
            ? item.PurchaseButton.transform
            : transform;
        if (!GiftManager.Instance.CoinGift.TryPurchase(
                _currentOffer,
                success => OnPurchaseCompleted(viewVersion, rewardSource, coinCount, success)))
        {
            RefreshPurchaseOffer();
            RefreshButtonStates();
            return;
        }

        RefreshButtonStates();
    }

    private void OnPurchaseCompleted(
        int viewVersion,
        Transform rewardSource,
        int coinCount,
        bool success)
    {
        if (this == null || !IsOpen || viewVersion != _viewVersion)
            return;

        if (success)
        {
            PlayRewardAnimationAsync(viewVersion, rewardSource, coinCount, "Purchase").Forget();
            RefreshPurchaseOffer();
            return;
        }

        RefreshPurchaseOffer();
        RefreshButtonStates();
    }

    /// <summary>
    /// 播放广告或购买成功后的金币飞行动画，并在动画期间锁定当前弹窗交互。
    /// 调用边界：奖励已经由 CoinGiftController 入包，本方法只负责表现，不重复发奖也不关闭弹窗。
    /// </summary>
    private async UniTask PlayRewardAnimationAsync(
        int viewVersion,
        Transform rewardSource,
        int coinCount,
        string rewardSourceName)
    {
        _isRewardAnimating = true;
        RefreshButtonStates();

        if (rewardAnimCtrl == null)
        {
            this.LogWarning(
                $"[CoinGiftPopup] Reward animation skipped. controller is null. source={rewardSourceName}");
        }
        else
        {
            int rewardAnimCount = ResolveCoinRewardAnimCount(coinCount);
            CommonUIRewardPlayResult result = await rewardAnimCtrl.PlayFrom(rewardSource, rewardAnimCount);
            if (result != CommonUIRewardPlayResult.Completed)
            {
                this.LogWarning(
                    $"[CoinGiftPopup] Reward animation did not complete. source={rewardSourceName}, result={result}, count={rewardAnimCount}");
            }
        }

        if (this == null || !IsOpen || viewVersion != _viewVersion)
            return;

        _isRewardAnimating = false;
        RefreshButtonStates();
    }

    private int ResolveCoinRewardAnimCount(int coinCount)
    {
        if (coinCount >= 2000)
            return maxCoinRewardAnimCount;

        if (coinCount >= 1000)
            return hugeCoinRewardAnimCount;

        if (coinCount >= 500)
            return largeCoinRewardAnimCount;

        if (coinCount >= 250)
            return midCoinRewardAnimCount;

        return minCoinRewardAnimCount;
    }

    private void OnShopClick(CustomButton _)
    {
        if (GiftManager.Instance.CoinGift.IsActionInProgress)
            return;

        GiftManager.Instance.Close(this);
        ShopPanel.Create(false, nameof(CoinGiftPopup));
    }

    private void OnCloseClick(CustomButton _)
    {
        if (!GiftManager.Instance.CoinGift.IsActionInProgress)
            GiftManager.Instance.Close(this);
    }

    #endregion

    #region 本地倒计时

    private void RefreshCountdown(bool force)
    {
        if (!force && Time.unscaledTime < _nextCountdownRefreshTime)
            return;

        _nextCountdownRefreshTime = Time.unscaledTime + 1f;
        if (!_showFirstPurchase)
        {
            firstPurchaseItem?.SetCountdown(TimeSpan.Zero, false);
            return;
        }

        TimeSpan remaining = GiftManager.Instance.GetDailyCountdown(GiftPopupType.CoinGift);
        firstPurchaseItem?.SetCountdown(remaining, true);
    }

    private bool TryResolveOriginalPrice(out string originalPrice)
    {
        originalPrice = string.Empty;
        if (!_hasOffer || _currentOffer.Shop == null || _currentOffer.Product == null)
            return false;

        return GiftPriceFormatter.TryResolveOriginalPrice(
            GameManager.Instance?.Purchase,
            _currentOffer.Product.Id,
            _currentOffer.Shop.Discount,
            nameof(CoinGiftPopup),
            out originalPrice);
    }

    #endregion

    /// <summary>
    /// 通过 GiftManager 打开金币不足弹窗。
    /// 调用边界：只在运行时 UI、配置、存档和 SDK 初始化完成后调用；商品配置缺失时购买入口会安全禁用。
    /// </summary>
    public static CoinGiftPopup Create()
    {
        return GiftManager.Instance.Open(GiftPopupType.CoinGift) as CoinGiftPopup;
    }
}
