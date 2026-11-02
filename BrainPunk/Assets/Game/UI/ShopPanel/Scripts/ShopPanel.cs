using System.Collections.Generic;
using System.Linq;
using Bear.EventSystem;
using Bear.Logger;
using Bear.UI;
using Config;
using Config.Game;
using Cysharp.Threading.Tasks;
using Game.ConfigModule;
using Game.Events;
using Game.Play;
using Game.Scripts.Common;
using GameCommon;

using UnityEngine;


/// <summary>
/// 商品类型
/// </summary>
public enum ShopItemType
{
    Purchase,
    Ad,
    Other
}

/// <summary>
/// 商店面板。
/// 管理所有商品项的初始化、刷新、购买/广告点击事件以及售罄状态。
/// </summary>
public partial class ShopPanel : BaseUIView, IDebuger, IEventSender
{
    [SerializeField] private CommonUIRewardAnimCtrl rewardAnimCtrl;
    [Header("Reward Anim")]
    [SerializeField] private int minCoinRewardAnimCount = 8;
    [SerializeField] private int midCoinRewardAnimCount = 10;
    [SerializeField] private int largeCoinRewardAnimCount = 12;
    [SerializeField] private int hugeCoinRewardAnimCount = 14;
    [SerializeField] private int maxCoinRewardAnimCount = 16;
    [SerializeField] private List<ShopItem> items;
    [SerializeField] private ShopPanelSwitchGroup switchGroup;

    /// <summary>
    /// 用途：按业务需要拦截 ShopPanel 原有交互，可用于引导、过渡动画或其它异步操作期间的输入隔离。
    /// 调用边界：由持有当前 ShopPanel 的业务流程成对开启和关闭，ShopPanel 生命周期负责复位。
    /// 注意事项：Prefab 应提供默认 inactive 的根节点 ClickBlock；未序列化绑定时会按直接子节点名称兜底查找。
    /// </summary>
    [SerializeField] private GameObject clickBlock;

    private bool needResumeGame;
    private string lastUIName = string.Empty;
    private EventSubscriber _subscriber;

    private bool hasRefresh;
    private ShopItem _pendingPurchaseItem;
    private bool _isPurchasing;
    private bool _isRewardAdRequesting;
    private bool _isRestoring;
    private IPlayPauseHost _pauseHost;
    private bool _skipRewardTipAutoShowOnClose;
    private bool _hasShownRestoreTransformPopup;

    #region Lifecycle

    public override void OnCreate()
    {
        base.OnCreate();
        if (CloseBtn != null) CloseBtn.OnClick += Close;
        BindItemEvents();
        ResolveSwitchGroup();

#if UNITY_IOS
        if (RestoreBtn != null)
        {
            RestoreBtn.gameObject.SetActive(true);
            RestoreBtn.OnClick += TryToRestore;
        }
#else
        RestoreBtn?.gameObject.SetActive(false);
#endif

        InitItems();
    }

    public override void OnShow()
    {
        base.OnShow();
        RefreshItems();
        RefreshButtonStates();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        SetClickBlocked(false);
        _isPurchasing = false;
        _isRewardAdRequesting = false;
        _isRestoring = false;
        _skipRewardTipAutoShowOnClose = false;
        _hasShownRestoreTransformPopup = false;
        BindItemEvents();
        RefreshItems();
        AddListener();
        RefreshButtonStates();
    }

    public override void OnClose()
    {
        IPlayPauseHost pauseHost = _pauseHost;
        _pauseHost = null;

        Shop2SkinGuideFlow.CancelFor(this);
        SetClickBlocked(false);
        GetComponentInChildren<ShopPanel_SkinPart>(true)?.ClosePreviewSession();
        base.OnClose();
        ShopPanel_SkinPart.HideSceneSkinPreview();
        EventsUtils.ResetEvents(ref _subscriber);
        UnbindItemEvents();
        _pendingPurchaseItem = null;
        _isPurchasing = false;
        _isRewardAdRequesting = false;
        _isRestoring = false;

        bool willShowRewardTip = false;
        var rewardTipModule = ScheduleSystem.Instance.GetModule<DailyScheduleModule>()?.GetRefreshable<ShopAdRewardTipAutoShowModule>();
        if (rewardTipModule != null)
        {
            if (!_skipRewardTipAutoShowOnClose && rewardTipModule.ShouldShowRewardTip())
            {
                ShopRewardTipPopup rewardTipPopup =
                    ShopRewardTipPopup.Create("20000", needResumeGame, pauseHost);
                willShowRewardTip = rewardTipPopup != null;
                if (willShowRewardTip)
                    rewardTipModule.RecordShow();
            }
        }

        if (needResumeGame && !willShowRewardTip)
        {
            if (pauseHost != null)
                pauseHost.Resume();
            else
                this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PLAYING);
        }
    }

    private void OnValidate()
    {
        minCoinRewardAnimCount = Mathf.Max(1, minCoinRewardAnimCount);
        midCoinRewardAnimCount = Mathf.Max(1, midCoinRewardAnimCount);
        largeCoinRewardAnimCount = Mathf.Max(1, largeCoinRewardAnimCount);
        hugeCoinRewardAnimCount = Mathf.Max(1, hugeCoinRewardAnimCount);
        maxCoinRewardAnimCount = Mathf.Max(1, maxCoinRewardAnimCount);
    }

    #endregion

    #region Factory

    /// <summary>
    /// 创建并打开商店面板。
    /// 游戏内调用可注入当前玩法暂停能力，确保商店及其后续奖励弹窗关闭后恢复正确的玩法控制器。
    /// </summary>
    /// <param name="resumeGame">关闭完整商店弹窗链后是否恢复玩法。</param>
    /// <param name="lastUIName">用于商店 SDK 打点的来源界面名称。</param>
    /// <param name="pauseHost">当前玩法的暂停恢复能力；为空时保持原主线事件恢复方式。</param>
    public static ShopPanel Create(
        bool resumeGame = false,
        string lastUIName = "",
        IPlayPauseHost pauseHost = null)
    {
        var panel = UIManager.Instance?.OpenUI<ShopPanel>($"{typeof(ShopPanel).Name}", UILayer.Popup);
        if (panel != null)
        {
            panel.needResumeGame = resumeGame;
            panel.lastUIName = lastUIName;
            panel._pauseHost = resumeGame ? pauseHost : null;
        }

        GameSDKService.Instance?.IAP_IMP(lastUIName);
        return panel;
    }

    public static ShopPanel CreateSkin(bool resumeGame = false, string lastUIName = "")
    {
        var panel = Create(resumeGame, lastUIName);
        panel?.SelectSkinPart();
        return panel;
    }

    /// <summary>
    /// 创建并打开商店 Skin 页，同时切换到指定 SkinPart 子筛选；只控制入口定位，不改变皮肤授予、试穿或存档。
    /// </summary>
    public static ShopPanel CreateSkin(SkinPart targetSkinPart, bool resumeGame = false, string lastUIName = "")
    {
        var panel = Create(resumeGame, lastUIName);
        panel?.SelectSkinPart(targetSkinPart);
        return panel;
    }

    #endregion

    #region Query

    /// <summary>
    /// 根据 ShopItem 的 ShopData.Id 反向查找本地 Products 配置。
    /// </summary>
    private Config.Products GetProductByShopItem(ShopItem shopItem, out string error)
    {
        error = "Shop item or ShopData is missing.";
        if (shopItem?.ShopData == null)
        {
            return null;
        }

        return PurchaseManager.TryResolveUniqueProduct(shopItem.ShopData.Id, out Config.Products product, out error)
            ? product
            : null;
    }

    /// <summary>
    /// 判断商品是否还可购买（未达到购买上限）。
    /// </summary>
    private bool IsAvailable(string productName, Config.Shop shop)
    {
        if (DB.GameData?.PurchaseCache == null)
        {
            return shop.BuyTimes < 0;
        }
        DB.GameData.PurchaseCache.TryGetValue(productName, out int count);
        return (shop.BuyTimes >= 0 && count < shop.BuyTimes) || shop.BuyTimes < 0;
    }

    /// <summary>
    /// 判断商品是否已经不需要（shop 奖励包含 Coin 且已购买 Hint99）。
    /// </summary>
    private bool IsNoNeedByHints(Shop shop)
    {
        if (shop == null || shop.Rewards == null)
        {
            return false;
        }
        return shop.Rewards.HasReward(Config.Game.GameProps.Coin) && IsUnlimitHints();
    }

    private bool IsNoNeedByAd(Shop shop)
    {
        if (shop == null || shop.Rewards == null)
        {
            return false;
        }
        return shop.Rewards.HasReward(Config.Game.GameProps.NoAds) && IsNoAd();
    }

    /// <summary>
    /// 是否已购买无限提示（Hint99）。
    /// </summary>
    private bool IsUnlimitHints()
    {
        var bag = GameManager.Instance?.Bag;
        if (bag == null) return false;
        var count = bag.GetToolCount(Config.Game.GameProps.UnlimitCoin) +
                    bag.GetToolCount(Config.Game.GameProps.UnlimitTips);
        return count > 0;
    }

    private bool IsNoAd()
    {
        var bag = GameManager.Instance?.Bag;
        if (bag == null) return false;
        var count = bag.GetToolCount(Config.Game.GameProps.NoAds);
        return count > 0;
    }

    #endregion

    #region Item Management

    /// <summary>
    /// 首次初始化所有商品项：查配置、设数据、播动画、设价格。
    /// </summary>
    private void InitItems()
    {
        for (int i = 0; i < items.Count; i++)
        {
            RefreshSingleItem(items[i], i, isInit: true);
        }
        this.Log("Init items");
    }

    /// <summary>
    /// 刷新所有商品项：重新查最新配置、更新售罄状态、刷新价格和动画。
    /// </summary>
    private void RefreshItems()
    {
        for (int i = 0; i < items.Count; i++)
        {
            RefreshSingleItem(items[i], i, isInit: false);
        }

        hasRefresh = true;
        RefreshButtonStates();
        this.Log("Refresh items");
    }

    /// <summary>
    /// 刷新单个商品项的核心逻辑。
    /// </summary>
    /// <param name="item">商品项</param>
    /// <param name="index">列表索引</param>
    /// <param name="isInit">是否为首次初始化（控制是否输出 Warning）</param>
    private void RefreshSingleItem(ShopItem item, int index, bool isInit)
    {
        if (item == null)
        {
            return;
        }

        // 1. 查 TbShop 获取商品配置
        var shop = ConfigManager.Instance?.Tables?.TbShop?.GetOrDefault(item.ShopId);
        if (shop == null)
        {
            if (isInit)
            {
                this.LogWarning($"ShopPanel shop not found for shopId: {item.ShopId}, index:{index}");
            }
            return;
        }

        item.ShopData = shop;

        // 2. 按 ItemType 分别处理
        if (item.ItemType == ShopItemType.Purchase)
        {
            // 反向查 Products 获取价格与购买状态
            var product = GetProductByShopItem(item, out string productError);
            if (product == null)
            {
                if (isInit)
                {
                    this.LogWarning($"ShopPanel product mapping invalid for shopId: {item.ShopId}, index:{index}, reason:{productError}");
                }
                return;
            }

            // 售罄判断：达到上限 或 已买 Hint99 导致
            if (!IsAvailable(product.Id, shop) || IsNoNeedByHints(shop) || IsNoNeedByAd(shop))
            {
                item.SetSoldOut(true);
                return;
            }

            item.SetSoldOut(false);
            item.Refresh();
            if (!hasRefresh)
                item.PlayShowAnimation();
            item.SetPrice(GameManager.Instance.Purchase.GetPrice(product.Id));

            return;
        }
        else if (item.ItemType == ShopItemType.Ad)
        {
            // 售罄判断：达到上限 或 已买 Hint99 导致
            if (IsNoNeedByHints(shop))
            {
                item.SetSoldOut(true);
                return;
            }

            item.SetSoldOut(false);
            item.Refresh();
            if (!hasRefresh)
                item.PlayShowAnimation();
            return;
        }

        // Other 类型：直接显示，刷新内容并播放动画
        item.SetSoldOut(false);
        item.Refresh();
        if (!hasRefresh)
            item.PlayShowAnimation();
    }

    #endregion

    #region Click Handlers

    /// <summary>
    /// Purchase 类型商品点击：走 IAP 购买流程。
    /// </summary>
    private void TryToPurchase(ShopItem shopItem)
    {
        if (IsAnyActionInProgress())
        {
            this.LogWarning("[ShopPanel] TryToPurchase ignored. action already in progress.");
            return;
        }

        if (shopItem == null || shopItem.ShopData == null)
        {
            this.LogWarning("[ShopPanel] TryToPurchase ignored. shop item or shop data is null.");
            return;
        }

        PurchaseManager purchaseManager = GameManager.Instance?.Purchase;
        if (purchaseManager == null)
        {
            this.LogError("[ShopPanel] TryToPurchase failed. PurchaseManager is null.");
            return;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            purchaseManager.OnPurchaseFailed();
            return;
        }

        var product = GetProductByShopItem(shopItem, out string productError);
        if (product == null)
        {
            this.LogError($"[ShopPanel] TryToPurchase failed. shopId:{shopItem.ShopData.Id}, reason:{productError}");
            return;
        }

        if (!PurchaseManager.TryGetProductId(product.Id, out var productId))
        {
            this.LogError($"[ShopPanel] TryToPurchase failed. ProductId not found for product: {product.Id}");
            return;
        }

        _pendingPurchaseItem = shopItem;
        _isPurchasing = true;
        RefreshButtonStates();
        GameSDKService.Instance?.IAP_CLK(productId.Sku);
        purchaseManager.Purchase(product.Id, purchaseCallback: OnPurchaseCallback);
    }

    /// <summary>
    /// Ad 类型商品点击：播放激励视频，成功后发奖励。
    /// </summary>
    private void TryToWatchAd(ShopItem shopItem)
    {
        if (IsAnyActionInProgress())
        {
            this.LogWarning("[ShopPanel] TryToWatchAd ignored. action already in progress.");
            return;
        }

        if (shopItem == null)
        {
            this.LogWarning("[ShopPanel] TryToWatchAd ignored. shop item is null.");
            return;
        }

        _isRewardAdRequesting = true;
        RefreshButtonStates();
        RewardAdHelper.TryToShowRewardAd(
            Config.Game.RewardPlacement.ShopReward.ToString(),
            onResult: (placement, isSuc) => OnWatchAdResult(shopItem, isSuc));
    }

    /// <summary>
    /// 激励视频播放结果回调：成功则通过 TbShop 查奖励并发放到背包。
    /// </summary>
    private void OnWatchAdResult(ShopItem shopItem, bool isSuc)
    {
        _isRewardAdRequesting = false;

        if (this == null || !isActiveAndEnabled)
        {
            return;
        }

        if (!isSuc || shopItem == null)
        {
            RefreshButtonStates();
            return;
        }

        string id = shopItem.ShopData?.Id ?? shopItem.ShopId;
        var shop = ConfigManager.Instance?.Tables?.TbShop?.GetOrDefault(id);
        if (shop == null)
        {
            RefreshButtonStates();
            return;
        }

        shopItem.ShopData = shop;

        var bag = GameManager.Instance?.Bag;
        if (bag == null)
        {
            RefreshButtonStates();
            return;
        }
        bool hasGrantedReward = false;
        foreach (var reward in shop.Rewards)
        {
            if (!BaseParamUtils.TryGetIntParam(reward.Value, out int rewardCount))
            {
                continue;
            }

            bag.AddTool(reward.Key, rewardCount, RewardType.Ad);
            hasGrantedReward = true;
        }

        if (hasGrantedReward)
        {
            _skipRewardTipAutoShowOnClose = true;
        }

        if (TryGetCoinRewardAmount(shop, out _))
        {
            PlayRewardAnimCtrlAsync(shopItem, "AdReward").Forget();
        }
        AudioManager.PlaySound("shopGetReward");
        RefreshItems();
    }

    /// <summary>
    /// Other 类型商品点击：预留逻辑。
    /// </summary>
    private void OnOtherItemClick(ShopItem shopItem)
    {
        if (shopItem == null)
        {
            this.LogWarning("[ShopPanel] OnOtherItemClick ignored. shop item is null.");
            return;
        }

        // TODO: implement other item click logic
        this.Log($"[ShopPanel] OnOtherItemClick: {shopItem.ShopId}");
    }

    #endregion

    #region Purchase Callback & Restore

    /// <summary>
    /// IAP 购买结果回调。
    /// </summary>
    private void OnPurchaseCallback(string productName, bool isSuc)
    {
        ShopItem purchasedItem = _pendingPurchaseItem;
        _pendingPurchaseItem = null;
        _isPurchasing = false;

        if (isSuc)
        {
            GameManager.Instance?.Purchase?.OnPurchaseSuccess(productName);
            ApplyPurchaseCoinReward(purchasedItem);
            PlayRewardAnimCtrlAsync(purchasedItem, "Purchase").Forget();
            RefreshItems();
        }
        else
        {
            GameManager.Instance?.Purchase?.OnPurchaseFailed();
        }

        RefreshButtonStates();
    }

    /// <summary>
    /// 播放金币奖励动画
    /// </summary>
    private async UniTask PlayRewardAnimCtrlAsync(ShopItem purchasedItem, string rewardSource)
    {
        if (rewardAnimCtrl == null)
        {
            this.LogWarning($"[ShopPanel] Reward animation skipped. controller is null. source:{rewardSource}");
            return;
        }

        if (purchasedItem?.PurchaseButton == null || purchasedItem.ShopData?.Rewards == null)
        {
            this.LogWarning("[ShopPanel] PlayRewardAnimCtrl skipped. purchase source or rewards are missing.");
            return;
        }

        if (!TryGetCoinRewardAmount(purchasedItem.ShopData, out int coinRewardAmount))
        {
            return;
        }

        int rewardAnimCount = ResolveCoinRewardAnimCount(coinRewardAmount);
        CommonUIRewardPlayResult result = await rewardAnimCtrl.PlayFrom(
            purchasedItem.PurchaseButton.transform,
            rewardAnimCount);
        if (result == CommonUIRewardPlayResult.Completed)
        {
            this.Log($"[ShopPanel] Reward animation completed. source:{rewardSource}, count:{rewardAnimCount}");
        }
        else
        {
            this.LogWarning(
                $"[ShopPanel] Reward animation did not complete. source:{rewardSource}, result:{result}, count:{rewardAnimCount}");
        }
    }

    private static bool TryGetCoinRewardAmount(Shop shop, out int coinRewardAmount)
    {
        coinRewardAmount = 0;
        if (shop?.Rewards == null)
        {
            return false;
        }

        return shop.Rewards.TryGetTotal(Config.Game.GameProps.Coin, out coinRewardAmount)
               && coinRewardAmount > 0;
    }

    private void ApplyPurchaseCoinReward(ShopItem purchasedItem)
    {
        if (purchasedItem?.ShopData == null)
        {
            this.LogWarning("[ShopPanel] ApplyPurchaseCoinReward skipped. purchased item or shop data is null.");
            return;
        }

        if (!TryGetCoinRewardAmount(purchasedItem.ShopData, out int coinRewardAmount))
        {
            return;
        }

        var bag = GameManager.Instance?.Bag;
        if (bag == null)
        {
            this.LogError("[ShopPanel] ApplyPurchaseCoinReward failed. Bag is null.");
            return;
        }

        bag.AddTool(Config.Game.GameProps.Coin, coinRewardAmount, RewardType.Purchase);
    }

    private int ResolveCoinRewardAnimCount(int coinRewardAmount)
    {
        if (coinRewardAmount >= 2000)
        {
            return maxCoinRewardAnimCount;
        }

        if (coinRewardAmount >= 1000)
        {
            return hugeCoinRewardAnimCount;
        }

        if (coinRewardAmount >= 500)
        {
            return largeCoinRewardAnimCount;
        }

        if (coinRewardAmount >= 250)
        {
            return midCoinRewardAnimCount;
        }

        return minCoinRewardAnimCount;
    }

    /// <summary>
    /// iOS 恢复购买按钮点击。
    /// </summary>
    private void TryToRestore(CustomButton _)
    {
        if (IsAnyActionInProgress())
        {
            this.LogWarning("[ShopPanel] TryToRestore ignored. action already in progress.");
            return;
        }

        _isRestoring = true;
        _hasShownRestoreTransformPopup = false;
        RefreshButtonStates();
        IosRestoreAsync().Forget();
    }

    /// <summary>
    /// iOS 异步恢复购买流程。
    /// </summary>
    private async UniTask IosRestoreAsync()
    {
        bool waitingPopupClosed = false;
        WaitingPopup.Create(false);
        WaitingPopup.SetOnCancel(() =>
        {
            waitingPopupClosed = true;
            OnRestoreWaitingPopupClosed();
        });
        try
        {
            if (GameManager.Instance?.Purchase != null)
            {
                PurchaseManager purchaseManager = GameManager.Instance.Purchase;
                if (await purchaseManager.Restore())
                {
                    purchaseManager.SyncRestore();
                }
            }
        }
        finally
        {
            _isRestoring = false;
            if (!waitingPopupClosed)
            {
                WaitingPopup.CloseStraightly();
            }

            if (this != null && isActiveAndEnabled)
            {
                RefreshItems();
                TryShowRestoreTransformPopup();
            }
        }
    }

    /// <summary>
    /// WaitingPopup 被手动、超时或外部流程关闭时，释放 RestorePurchase 的按钮锁，避免 SDK 长时间未返回导致 ShopPanel 交互悬挂。
    /// </summary>
    private void OnRestoreWaitingPopupClosed()
    {
        if (!_isRestoring)
        {
            return;
        }

        _isRestoring = false;
        if (this != null && isActiveAndEnabled)
        {
            RefreshButtonStates();
        }
    }

    #endregion

    #region Events

    /// <summary>
    /// 注册全局事件监听。
    /// </summary>
    private void AddListener()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<OnRestoreSuccessEvent>(OnRestoreSuccess);
    }

    /// <summary>
    /// 恢复购买成功后刷新商品状态。
    /// </summary>
    private void OnRestoreSuccess(OnRestoreSuccessEvent _)
    {
        _isRestoring = false;
        RefreshItems();
        TryShowRestoreTransformPopup();
    }

    /// <summary>
    /// 恢复购买补发后检查旧 Tips 权益是否需要弹出转换界面；只在本次 ShopPanel 生命周期内弹出一次。
    /// </summary>
    private void TryShowRestoreTransformPopup()
    {
        if (_hasShownRestoreTransformPopup)
        {
            return;
        }

        if (!TransformPopup.HasTransformableTips())
        {
            return;
        }

        if (TransformPopup.Create() != null)
        {
            _hasShownRestoreTransformPopup = true;
        }
    }

    #endregion

    #region UI Actions

    /// <summary>
    /// 关闭商店面板。
    /// </summary>
    private void Close(CustomButton _)
    {
        TryClosePanel();
    }

    /// <summary>
    /// 用途：仅在当前 ShopPanel 生命周期内禁止关闭后自动弹出广告奖励提示。
    /// 调用边界：由已经提供独立奖励反馈的局部领取流程在领取成立后调用；不会消耗奖励提示的每日展示次数。
    /// 注意事项：该状态会在下一次 OnOpen 时复位，普通关闭、其它关卡及后续商店实例保持原行为。
    /// </summary>
    internal void SuppressRewardTipAutoShowOnClose()
    {
        _skipRewardTipAutoShowOnClose = true;
    }

    /// <summary>
    /// 用途：通过 ShopPanel 统一关闭链关闭当前面板，保持暂停恢复、引导清理和 IAP 关闭打点一致。
    /// 调用边界：供 ShopPanel 自身关闭按钮及面板内已完成前置动画的局部功能调用。
    /// 注意事项：购买、广告或恢复购买进行中会拒绝关闭并返回 false，调用方需自行恢复临时输入遮罩。
    /// </summary>
    /// <returns>已提交面板关闭时返回 true；当前忙碌或 UIManager 不可用时返回 false。</returns>
    internal bool TryClosePanel()
    {
        if (IsAnyActionInProgress())
        {
            this.LogWarning("[ShopPanel] Close ignored. action already in progress.");
            return false;
        }

        if (UIManager.Instance == null)
        {
            this.LogWarning("[ShopPanel] Close ignored. UIManager is unavailable.");
            return false;
        }

        UIManager.Instance.DestroyUI(this);
        GameSDKService.Instance?.IAP_CLOSE(lastUIName);
        return true;
    }

    #endregion

    #region Button Binding

    /// <summary>
    /// 为所有商品项绑定点击事件。
    /// </summary>
    private void BindItemEvents()
    {
        for (int i = 0; i < items.Count; i++)
        {
            ShopItem item = items[i];
            if (item == null)
            {
                continue;
            }

            UnbindSingleItem(item);
            BindSingleItem(item);
        }
    }

    /// <summary>
    /// 解绑所有商品项的点击事件。
    /// </summary>
    private void UnbindItemEvents()
    {
        for (int i = 0; i < items.Count; i++)
        {
            ShopItem item = items[i];
            if (item == null)
            {
                continue;
            }

            UnbindSingleItem(item);
        }
    }

    /// <summary>
    /// 根据 ItemType 为单个商品项绑定对应的事件处理函数。
    /// </summary>
    private void BindSingleItem(ShopItem item)
    {
        switch (item.ItemType)
        {
            case ShopItemType.Purchase:
                item.PurchaseClicked += TryToPurchase;
                break;
            case ShopItemType.Ad:
                item.PurchaseClicked += TryToWatchAd;
                break;
            case ShopItemType.Other:
                item.PurchaseClicked += OnOtherItemClick;
                break;
        }
    }

    /// <summary>
    /// 解绑单个商品项的所有事件处理函数。
    /// </summary>
    private void UnbindSingleItem(ShopItem item)
    {
        item.PurchaseClicked -= TryToPurchase;
        item.PurchaseClicked -= TryToWatchAd;
        item.PurchaseClicked -= OnOtherItemClick;
    }

    private bool IsAnyActionInProgress()
    {
        return _isPurchasing || _isRewardAdRequesting || _isRestoring;
    }

    private void RefreshButtonStates()
    {
        bool interactable = !IsAnyActionInProgress();

        if (CloseBtn != null)
        {
            CloseBtn.Interactable = interactable;
        }

        if (RestoreBtn != null && RestoreBtn.gameObject.activeInHierarchy)
        {
            RestoreBtn.Interactable = interactable;
        }

        if (items == null)
        {
            return;
        }

        for (int i = 0; i < items.Count; i++)
        {
            ShopItem item = items[i];
            if (item?.PurchaseButton == null)
            {
                continue;
            }

            item.PurchaseButton.Interactable = interactable
                && !item.IsSoldOut;
            // 只在进入的时候刷新
            // && item.PurchaseButton.gameObject.activeInHierarchy;
        }
    }

    #endregion

    #region Tab Selection

    /// <summary>
    /// 用途：按调用方要求切换 ShopPanel 的全屏点击拦截。
    /// 调用边界：由持有当前 ShopPanel 的业务流程及本 Panel 生命周期在运行态调用，不绑定具体业务类型。
    /// 注意事项：调用方负责成对开启和关闭；缺少根节点 ClickBlock 时返回 false，不创建运行时替代对象。
    /// </summary>
    /// <param name="isBlocked">true 开启拦截；false 恢复 ShopPanel 普通交互。</param>
    /// <returns>找到 ClickBlock 并完成目标状态同步时返回 true。</returns>
    public bool SetClickBlocked(bool isBlocked)
    {
        if (!Application.isPlaying)
            return false;

        if (clickBlock == null)
        {
            Transform clickBlockTransform = transform.Find("ClickBlock");
            clickBlock = clickBlockTransform != null ? clickBlockTransform.gameObject : null;
        }

        if (clickBlock == null)
            return false;

        if (clickBlock.activeSelf != isBlocked)
            clickBlock.SetActive(isBlocked);

        return true;
    }

    /// <summary>
    /// 用途：获取 ShopPanel 内 Skin Toggle 与 GuideFinger 根节点。
    /// 调用边界：仅由 Shop2SkinGuideFlow 在 ShopPanel 创建完成后于运行态调用。
    /// 注意事项：Prefab 变体缺少切页组件或 RectTransform 时返回 false，不修改当前页签。
    /// </summary>
    internal bool TryGetSkinGuideContext(
        out RectTransform skinToggleTarget,
        out RectTransform guideRoot)
    {
        ResolveSwitchGroup();
        skinToggleTarget = switchGroup != null ? switchGroup.SkinGuideTarget : null;
        guideRoot = transform as RectTransform;
        return skinToggleTarget != null && guideRoot != null;
    }

    /// <summary>
    /// 用途：在 Skin Toggle 引导点击完成后只切换到 Skin 大页，并返回页内 SkinPart 控制器。
    /// 调用边界：仅由 Shop2SkinGuideFlow 调用，复用现有页签与 SkinPart 刷新逻辑。
    /// 注意事项：缺少切页组件或 SkinPart 控制器时返回 false；部位筛选等待下一枚 GuideFinger 完成后再切换。
    /// </summary>
    internal bool TrySelectSkinPageForGuide(out ShopPanel_SkinPart skinPart)
    {
        ResolveSwitchGroup();
        skinPart = null;
        if (switchGroup == null)
            return false;

        switchGroup.SelectSkin();
        skinPart = GetComponentInChildren<ShopPanel_SkinPart>(true);
        if (skinPart == null)
            return false;

        return true;
    }

    private void SelectSkinPart()
    {
        ResolveSwitchGroup();
        switchGroup?.SelectSkin();
    }

    private void SelectSkinPart(SkinPart targetSkinPart)
    {
        SelectSkinPart();
        var skinPart = GetComponentInChildren<ShopPanel_SkinPart>(true);
        skinPart?.SelectSkinPart(targetSkinPart);
    }

    private void ResolveSwitchGroup()
    {
        if (switchGroup != null)
        {
            return;
        }

        switchGroup = GetComponentInChildren<ShopPanelSwitchGroup>(true);
    }

    public bool IsSkinPartSelected()
    {
        ResolveSwitchGroup();
        return switchGroup != null && switchGroup.IsSkinSelected;
    }

    #endregion
}
