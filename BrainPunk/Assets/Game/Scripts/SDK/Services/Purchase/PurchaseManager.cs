using System;
using System.Collections.Generic;
using Bear.EventSystem;
using Bear.Logger;
using Config.Game;
using Game.Events;
using GameCommon;
using Cysharp.Threading.Tasks;
using Guru.SDK.Framework.Core.Financial.Iap;
using Guru.SDK.Framework.Core.Financial.Product;
using Guru.SDK.Framework.Utils.Financial.Iap;
using Guru.SDK.Framework.Core.Financial.Asset;
using R3;
using Game.ConfigModule;
using System.Linq;
using I2.Loc;
using Game.Scripts.Common;

/// <summary>
/// 统一处理购买事件，通过 PurchaseEvent 发放奖励。
/// 由外部代码创建实例并保持生命周期。
/// 平台商品映射优先复用 Guru 生成代码 Products，缺失时读取唯一的 TbProducts 配置。
/// </summary>
public class PurchaseManager : IEventSender, IDebuger
{
    private const string DefaultIntentScene = "legacy";

    private EventSubscriber _subscriber;
    private readonly List<IDisposable> _iapSubscriptions = new();
    private readonly List<string> _restoredProductNames = new();
    private readonly HashSet<string> _pendingPurchaseProductNames = new();

    /// <summary>
    /// IAP 是否初始化成功。
    /// 用于外部快速判断购买/取价 API 是否可用。
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// 已经尝试过初始化，不管成不成功
    /// </summary>
    public bool HasTryInit { get; private set; }

    private Dictionary<ProductId, ProductDetails>? _productDetails;
    private AssetStore<Asset>? _assetStore;
    private int _assetStoreVersion;

    /// <summary>
    /// 商品 Id 表，关联一下奖励表。主要用于 guru_spec 的数据填充用的，依托 guru 自动成成代码
    /// </summary>
    private List<Config.Products> ProducstIDs { get; set; }

    /// <summary>
    /// 商店奖励内容，早期表
    /// </summary>
    public List<Config.Shop> Shops { get; private set; }
    public IReadOnlyList<string> RestoredProductNames => _restoredProductNames;

    private bool IsReadyToRestore = false;

    public PurchaseManager()
    {
        InitCallback();
    }

    /// <summary>
    /// 监听
    /// </summary>
    private void InitCallback()
    {
        TrySubscribeIap();
    }

    public void InitConfig()
    {
        ProducstIDs = ConfigManager.Instance.Tables.TbProducts.DataList.ToList();
        Shops = ConfigManager.Instance.Tables.TbShop.DataList.ToList();
        IsReadyToRestore = true;

        // AssetStore 可能早于 Luban 配置完成回调；配置就绪后重新解析一次，避免新增 Possessive 商品漏恢复。
        if (_assetStore?.Data != null && _assetStore.Data.Count > 0)
        {
            ResolveRestoredProductNames(_assetStore);
        }
    }

    private void TrySubscribeIap()
    {
        try
        {
            _iapSubscriptions.Add(IapManager.Instance.ObservableAvailable.Subscribe(OnIapInit));
            _iapSubscriptions.Add(IapManager.Instance.ObservableProductDetails.Subscribe(OnProductDetailsChanged));
            _iapSubscriptions.Add(IapManager.Instance.ObservableAssetStore.Subscribe(OnRestored));
        }
        catch (Exception e)
        {
            this.LogError($"Subscribe IAP callbacks failed: {e}");
        }
    }

    private void OnIapInit(bool success)
    {
        IsInitialized = success;
        HasTryInit = true;
        this.Log($"Iap init: {success}");
    }

    /// <summary>
    /// 发起平台恢复购买；iOS 需等本次资产列表更新后再由商店同步本地权益。
    /// </summary>
    /// <returns>iOS 需平台成功且本次资产列表已更新；其它平台返回 SDK 的恢复结果。</returns>
    public async UniTask<bool> Restore()
    {
        this.Log("Restore start.");

        int assetStoreVersionBeforeRestore = _assetStoreVersion;
        bool result = await IapManager.Instance.RestorePurchases();
        this.Log($"Restore sdk return. result:{result}");
        if (!result) this.LogWarning("Restore sdk returned false.");

#if UNITY_IOS
        if (!result)
            return false;

        // SDK 的 OnRestore 会异步写入 AssetStore；等待该次通知后，ShopPanel 才能补发新资产。
        bool didTimeout = await UniTask.WaitUntil(() => _assetStoreVersion != assetStoreVersionBeforeRestore)
            .TimeoutWithoutException(TimeSpan.FromSeconds(10), DelayType.UnscaledDeltaTime);
        if (didTimeout)
        {
            this.LogWarning("Restore finished without an AssetStore update.");
            return false;
        }
#endif
        return result;
    }

    private void OnProductDetailsChanged(Dictionary<ProductId, ProductDetails>? data)
    {
        if (data == null || data.Count == 0)
            return;

        _productDetails = data;
        // TODO: 有需要的话可在这里抛事件通知 UI 刷新价格信息
    }

    private void OnRestored(AssetStore<Asset>? assetStore)
    {
        if (assetStore == null)
        {
            this.LogWarning("OnRestored assetStore is null.");
            return;
        }

        _assetStore = assetStore;
        ResolveRestoredProductNames(assetStore);
#if UNITY_ANDROID
        SyncRestore();
#endif
        _assetStoreVersion++;
    }

    /// <summary>
    /// 解析并缓存平台返回的可恢复商品名。
    /// 仅接受 TbProducts 中标记为 Possessive 的商品；实际补发由 Bag 就绪后的 SyncRestore 执行。
    /// </summary>
    private void ResolveRestoredProductNames(AssetStore<Asset> assetStore)
    {
        var dataCount = assetStore.Data?.Count ?? 0;
        this.Log($"OnRestored enter. dataCount:{dataCount}");

        var productNames = new List<string>();
        var rawSkus = new List<string>();
        var unresolvedSkus = new List<string>();
        if (dataCount > 0)
        {
            foreach (var item in assetStore.Data)
            {
                var productId = item.Key;
                var sku = productId?.Sku ?? "null";
                rawSkus.Add(sku);
                if (TryResolveRestoredProductName(productId, out string productName))
                {
                    productNames.Add(productName);
                    this.Log($"OnRestored map success. sku:{sku}, productName:{productName}");
                }
                else
                {
                    unresolvedSkus.Add(sku);
                    this.LogWarning($"OnRestored map failed. sku:{sku}");
                }
            }
        }
        else
        {
            return;
        }

        this.Log(
            $"OnRestored parse done. rawCount:{rawSkus.Count}, resolvedCount:{productNames.Count}, unresolvedCount:{unresolvedSkus.Count}");

        _restoredProductNames.Clear();
        _restoredProductNames.AddRange(productNames.Distinct());

        // 先缓存解析结果；Android 由 OnRestored/Loading 同步，iOS 由 ShopPanel 手动恢复后同步。
        this.Log($"OnRestored saved productNames: [{string.Join(", ", _restoredProductNames)}]");

      /*   SystemTips.Show(LocalizationManager.GetTranslation("U_Shop_Restore_Tips_01"));
        AudioManager.PlaySound("shopGetReward");
        this.Log($"OnRestored finish. restoredCount:{productNames.Count}"); */
    }

    /// <summary>
    /// 手动执行恢复补发。基于最近一次 OnRestored 解析并保存的 productNames。
    /// </summary>
    /// <param name="showTips">是否展示成功提示。</param>
    /// <returns>本次是否实际补齐了购买记录或永久权益。</returns>
    public bool SyncRestore(bool showTips = true)
    {
        if (_restoredProductNames.Count == 0)
        {
            this.LogWarning("SyncRestore skipped. no saved restored product names.");
            return false;
        }

        if (!IsReadyToRestore)
        {
            this.LogError($"SyncRestore skipped. IsReadyToRestore: {IsReadyToRestore}");
            return false;
        }

        var changedCount = 0;
        foreach (var productName in _restoredProductNames)
        {
            try
            {
                if (_pendingPurchaseProductNames.Contains(productName))
                {
                    this.Log($"SyncRestore skipped active purchase. productName:{productName}");
                    continue;
                }

                this.Log($"SyncRestore apply reward. productName:{productName}");
                if (TryRestorePurchase(productName, out bool changed) && changed)
                    changedCount++;
            }
            catch (Exception e)
            {
                this.LogError($"SyncRestore apply reward failed. productName:{productName}, error:{e}");
            }
        }

        if (showTips && changedCount > 0)
        {
            SystemTips.Show(LocalizationManager.GetTranslation("U_Shop_Restore_Tips_01"));
            AudioManager.PlaySound("shopGetReward");
        }

        // 内购恢复刷新
        if(changedCount > 0)
            this.DispatchEvent(Witness<OnRestoreSuccessEvent>._);

        this.Log($"SyncRestore finish. changedCount:{changedCount}, savedCount:{_restoredProductNames.Count}");
        return changedCount > 0;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 仅供 Editor PlayMode 的 GM 工具模拟平台已返回 GiftPack1 商品名。
    /// 这里只补充待恢复商品名，不伪造 SDK 订单；调用方需在游戏初始化完成后执行 SyncRestore。
    /// </summary>
    public void DebugAddRestoredGiftPack1()
    {
        if (!UnityEngine.Application.isPlaying)
        {
            this.LogWarning("DebugAddRestoredGiftPack1 skipped outside PlayMode.");
            return;
        }

        string productName = Products.ProductName.GiftPack1;
        if (!_restoredProductNames.Contains(productName))
            _restoredProductNames.Add(productName);

        this.Log($"GM restore test added productName:{productName}");
    }
#endif

    /// <summary>
    /// 按平台返回的 ProductId 反查唯一的 Possessive 商品配置。
    /// 使用 Luban 配置兜底 Guru 生成表尚未包含的新商品，且拒绝 Consumeable/Subscriptions 进入恢复链。
    /// </summary>
    private bool TryResolveRestoredProductName(ProductId restoredProductId, out string productName)
    {
        productName = null;
        if (restoredProductId == null || ProducstIDs == null)
            return false;

        List<Config.Products> matches = ProducstIDs
            .Where(product =>
                product != null &&
                product.Type == Config.IAP.ProductType.Possessive &&
                TryGetProductId(product.Id, out ProductId configuredProductId) &&
                configuredProductId.Equals(restoredProductId))
            .Take(2)
            .ToList();
        if (matches.Count != 1)
        {
            if (matches.Count > 1)
            {
                this.LogError(
                    $"Restore product mapping is ambiguous. sku:{restoredProductId.Sku}, matches:{string.Join(", ", matches.Select(product => product.Id))}");
            }

            return false;
        }

        productName = matches[0].Id;
        return true;
    }

    /// <summary>
    /// 恢复一个 Possessive 商品的永久权益。
    /// Coin 属于消耗型数值，不参与 Restore；Skin、NoAds 与 UnlimitTips 按当前配置恢复并保持幂等。
    /// </summary>
    private bool TryRestorePurchase(string productName, out bool changed)
    {
        changed = false;
        Config.Products product = ProducstIDs?.Find(item =>
            item != null && string.Equals(item.Id, productName, StringComparison.Ordinal));
        if (product == null || product.Type != Config.IAP.ProductType.Possessive)
        {
            this.LogWarning($"SyncRestore skipped. product is not Possessive. productName:{productName}");
            return false;
        }

        Config.Shop shopData = GetRewardByProductName(productName);
        if (shopData?.Rewards == null || shopData.Rewards.Count == 0)
        {
            this.LogError($"SyncRestore failed. Shop rewards are missing. productName:{productName}");
            return false;
        }

        var bag = GameManager.Instance?.Bag;
        if (bag == null || DB.GameData?.PurchaseCache == null)
        {
            this.LogError($"SyncRestore failed. Bag or PurchaseCache is unavailable. productName:{productName}");
            return false;
        }

        bool hasRestorableReward = false;
        foreach (Config.ShopReward reward in shopData.Rewards)
        {
            if (reward.Key == GameProps.Coin)
            {
                this.Log($"SyncRestore skipped Coin reward. productName:{productName}");
                continue;
            }

            if (reward.Key == GameProps.Skin)
            {
                if (reward.Value is not Config.StringParam skinParam ||
                    string.IsNullOrWhiteSpace(skinParam.Value) ||
                    !SkinUtils.TryGetSkin(skinParam.Value, out _))
                {
                    this.LogError($"SyncRestore failed. Skin reward is invalid. productName:{productName}");
                    return false;
                }

                if (bag.GetNewSkin(skinParam.Value, GetSkinWayType.Purchase, false))
                    changed = true;
                if (!(DB.GameData.OwnedSkin?.Contains(skinParam.Value) ?? false))
                {
                    this.LogError(
                        $"SyncRestore failed. Skin ownership was not written. productName:{productName}, skinId:{skinParam.Value}");
                    return false;
                }

                hasRestorableReward = true;
                continue;
            }

            if (reward.Key != GameProps.NoAds && reward.Key != GameProps.UnlimitTips)
            {
                this.LogWarning($"SyncRestore skipped unsupported reward. productName:{productName}, reward:{reward.Key}");
                continue;
            }

            if (!BaseParamUtils.TryGetIntParam(reward.Value, out int rewardCount) || rewardCount <= 0)
            {
                this.LogError(
                    $"SyncRestore failed. Permanent reward count is invalid. productName:{productName}, reward:{reward.Key}");
                return false;
            }

            int ownedCount = bag.GetToolCount(reward.Key);
            if (ownedCount < rewardCount)
            {
                bag.AddTool(reward.Key, rewardCount - ownedCount, RewardType.Purchase, false);
                changed = true;
            }

            hasRestorableReward = true;
        }

        if (!hasRestorableReward)
        {
            this.LogWarning($"SyncRestore skipped. no restorable rewards. productName:{productName}");
            return false;
        }

        if (DB.GameData.PurchaseCache.TryAdd(productName, 1))
            changed = true;
        if (changed && !DB.SaveGameData())
        {
            this.LogWarning($"SyncRestore save failed. productName:{productName}");
        }

        return true;
    }

    /// <summary>
    /// 是否已购买（或已拥有）指定商品。
    /// </summary>
    /// <param name="productName">项目内部商品名（可通过配置/约定映射到 ProductId）。</param>
    public bool IsPurchased(string productName)
    {
        if (!TryGetProductId(productName, out var productId))
            return false;

        if (IapManager.Instance?.AssetStore == null)
        {
            this.LogWarning($"IsPurchased failed. IapManager.Instance or AssetStore is null. productName:{productName}");
            return false;
        }

        var result = IapManager.Instance.AssetStore.IsOwned(productId);
        this.Log($"IsPurchased productName:{productName}, result:{result}");
        return result;
    }

    /// <summary>
    /// 获取商品信息（价格、币种等）。
    /// </summary>
    /// <param name="productName">项目内部商品名。</param>
    /// <param name="intentScene">购买意图场景（用于埋点/归因），默认 "legacy"。</param>
    /// <returns>商品信息；若未找到映射或未拉到商品信息则返回 null。</returns>
    public IapProduct? GetProductInfo(string productName, string intentScene = DefaultIntentScene)
    {
        if (!TryGetProductId(productName, out var productId))
        {
            this.LogWarning($"GetProductInfo failed. productId not found. productName:{productName}");
            return null;
        }

        var product = GetProductByIntent(productId, intentScene);
        this.Log(
            $"GetProductInfo productName:{productName}, Price:{product?.Details.Price}, CurrencyCode:{product?.Details.CurrencyCode} sku:{product?.ProductId.Sku}");
        return product;
    }

    /// <summary>
    /// 获取商品价格展示字符串（例如 "$0.99"）。
    /// 优先使用已缓存的商品详情；若未缓存则尝试即时构建查询。
    /// </summary>
    /// <param name="productName">项目内部商品名。</param>
    /// <param name="intentScene">购买意图场景（用于埋点/归因），默认 "legacy"。</param>
    public string GetPrice(string productName, string intentScene = DefaultIntentScene)
    {
        if (!TryGetProductId(productName, out var productId))
            return LocalizationManager.GetTranslation("U_Shop_Unavailable");

        if (_productDetails != null && _productDetails.TryGetValue(productId, out var details) && !string.IsNullOrEmpty(details.Price))
            return details.Price;

        var priceString = GetProductInfo(productName, intentScene)?.Details.Price;
        if (string.IsNullOrEmpty(priceString))
        {
            this.Log($"GetPrice item:{productName}, price is empty.");
            return LocalizationManager.GetTranslation("U_Shop_Unavailable");
        }

        return priceString;
    }

    /// <summary>
    /// 发起购买请求。
    /// </summary>
    /// <param name="productName">项目内部商品名。</param>
    /// <param name="category">购买场景/来源（用于埋点/归因）。</param>
    /// <param name="purchaseCallback">
    /// 购买结果回调。回调会通过 <see cref="RunOnMainThread{T1,T2}"/> 投递到主线程执行。
    /// </param>
    public void Purchase(string productName, string category = "store", Action<string, bool>? purchaseCallback = null)
    {
        if (!TryGetProductId(productName, out var productId))
        {
            RunOnMainThread(purchaseCallback, productName, false);
            this.LogWarning($"Purchase productName:{productName} productId is null");
            return;
        }

        var product = GetProductByIntent(productId, category);
        if (product == null)
        {
            RunOnMainThread(purchaseCallback, productName, false);
            this.LogWarning($"Purchase productName:{productName} product is null");
            return;
        }

        this.Log($"Purchase productName:{productName},Price:{product.Details.Price} request purchase.");

        /// 编辑器模式：弹窗 + 1s 模拟等待，便于联调 UI 流程
#if UNITY_EDITOR
        SimulateEditorPurchase(productName, purchaseCallback).Forget();
        return;
#endif

        WaitingPopup.Create(false);
        _pendingPurchaseProductNames.Add(productName);

        try
        {
            IapManager.Instance.Request(product).ContinueWith(result =>
            {
                UniTask.Post(() =>
                {
                    try
                    {
                        _pendingPurchaseProductNames.Remove(productName);
                        WaitingPopup.CloseStraightly();
                        purchaseCallback?.Invoke(productName, result);
                        this.Log($"Purchase result: {result}, productName:{productName}");
                    }
                    catch (Exception e)
                    {
                        this.LogError($"Purchase callback failed. productName:{productName}, result:{result}, error:{e}");
                    }
                });
            }).Forget();
        }
        catch
        {
            _pendingPurchaseProductNames.Remove(productName);
            WaitingPopup.CloseStraightly();
            throw;
        }
    }

#if UNITY_EDITOR
    private async UniTaskVoid SimulateEditorPurchase(string productName, Action<string, bool>? purchaseCallback)
    {
        WaitingPopup.Create(false);
        try
        {
            await UniTask.Delay(1000, DelayType.UnscaledDeltaTime);
        }
        finally
        {
            WaitingPopup.CloseStraightly();
        }

        RunOnMainThread(purchaseCallback, productName, true);
        this.Log("Purchase result (editor simulate): True");
    }
#endif

    /// <summary>
    /// 按项目内部商品名解析平台 ProductId。
    /// 调用边界：运行时配置初始化后使用；优先复用 Guru 生成映射，缺失时从唯一 TbProducts 行构造。
    /// 注意事项：ProductType 只转换为平台交易属性，不参与 Shop.BuyTimes 限购判断。
    /// </summary>
    public static bool TryGetProductId(string productName, out ProductId productId)
    {
        productId = Products.GetProductId(productName);
        if (productId != null)
            return true;

        List<Config.Products> products = ConfigManager.Instance?.Tables?.TbProducts?.DataList;
        if (products == null || string.IsNullOrWhiteSpace(productName))
            return false;

        List<Config.Products> matches = products
            .Where(item => item != null && string.Equals(item.Id, productName, StringComparison.Ordinal))
            .Take(2)
            .ToList();
        if (matches.Count != 1)
            return false;

        Config.Products config = matches[0];
        if (string.IsNullOrWhiteSpace(config.AndroidProductId) &&
            string.IsNullOrWhiteSpace(config.IosProductId))
        {
            return false;
        }

        TransactionAttribute attribute;
        switch (config.Type)
        {
            case Config.IAP.ProductType.Consumeable:
                attribute = TransactionAttribute.Consumable;
                break;
            case Config.IAP.ProductType.Subscriptions:
                attribute = TransactionAttribute.Subscriptions;
                break;
            default:
                attribute = TransactionAttribute.Asset;
                break;
        }
        productId = new ProductId(
            android: config.AndroidProductId,
            ios: config.IosProductId,
            attr: attribute,
            points: false);
        return true;
    }

    /// <summary>
    /// 判断平台 SKU 是否为空或仍为占位配置。
    /// 调用边界：供购买入口统一执行本地配置门禁，不代表平台商品一定已上架。
    /// </summary>
    public static bool IsPlaceholderSku(string sku)
    {
        return string.IsNullOrWhiteSpace(sku) ||
               sku.IndexOf("placeholder", StringComparison.OrdinalIgnoreCase) >= 0 ||
               sku.IndexOf("todo", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static IapProduct? GetProductByIntent(ProductId productId, string intentScene)
    {
        var productStore = IapManager.Instance.BuildProducts(new HashSet<TransactionIntent>
        {
            productId.CreateIntent(intentScene)
        });

        return productStore.GetProduct(productId);
    }

    /// <summary>
    /// 将回调投递到 Unity 主线程执行，避免在后台线程触发 Unity API 调用。
    /// </summary>
    public static void RunOnMainThread<T1, T2>(Action<T1, T2>? callback, T1 param1, T2 param2)
    {
        UniTask.Post(() =>
        {
            callback?.Invoke(param1, param2);
        });
    }

    /// <summary>
    /// 释放事件订阅与 IAP 订阅，避免重复回调与内存泄漏。
    /// </summary>
    public void Dispose()
    {
        EventsUtils.ResetEvents(ref _subscriber);

        foreach (var d in _iapSubscriptions)
        {
            try
            {
                d.Dispose();
            }
            catch (Exception e)
            {
                this.LogError($"Dispose iap subscription failed: {e}");
            }
        }

        _iapSubscriptions.Clear();
    }

    /// <summary>
    /// 按 TbShop Id 查询唯一的 TbProducts 映射。
    /// 调用边界：只读取当前已加载的 Luban 配置，不检查平台 SKU、价格、限购次数或奖励合法性。
    /// 注意事项：同一 RewardId 配置多条 Product 时视为歧义并返回 false，调用方不得静默选择第一条。
    /// </summary>
    /// <param name="shopId">需要反查 Product 的 Shop.Id。</param>
    /// <param name="product">成功时返回唯一的 Product 配置。</param>
    /// <param name="error">失败时返回可直接用于日志的原因。</param>
    public static bool TryResolveUniqueProduct(
        string shopId,
        out Config.Products product,
        out string error)
    {
        product = null;
        error = null;
        if (string.IsNullOrWhiteSpace(shopId))
        {
            error = "Shop id is empty.";
            return false;
        }

        List<Config.Products> products = ConfigManager.Instance?.Tables?.TbProducts?.DataList;
        if (products == null)
        {
            error = "Products table is unavailable.";
            return false;
        }

        List<Config.Products> matches = products
            .Where(item => item != null && string.Equals(item.RewardId, shopId, StringComparison.Ordinal))
            .Take(2)
            .ToList();
        if (matches.Count == 0)
        {
            error = $"Product mapping is missing. expected RewardId={shopId}.";
            return false;
        }

        if (matches.Count > 1)
        {
            error = $"Product mapping is ambiguous. expected one RewardId={shopId}.";
            return false;
        }

        product = matches[0];
        return true;
    }

    /// <summary>
    /// 获取真实奖励对象通过 productName
    /// </summary>
    /// <param name="productName"></param>
    /// <returns></returns>
    public Config.Shop GetRewardByProductName(string productName)
    {
        if(ProducstIDs == null)
        {
            this.LogError("ProductIds Config is empty;");
            return null;
        }
        if (Shops == null)
        {
            this.LogError("Shops Config is empty;");
            return null;
        }
        var data = ProducstIDs.Find(p => p.Id.Equals(productName));
        if (data == null)
        {
            this.LogError($"Product lost: {productName}");
            return null;
        }

        var shop = Shops.Find(shop => shop.Id.Equals(data.RewardId));
        if (shop == null)
        {
            this.LogError($"Shop reward lost for product: {productName}, rewardId: {data.RewardId}");
        }
        return shop;
    }

    // 内购成功，通用方法
    public void OnPurchaseSuccess(string productName, bool showTips = true)
    {
        this.Log($"OnPurchaseSuccess enter. productName:{productName}, showTips:{showTips}");
        if (string.IsNullOrEmpty(productName))
        {
            this.LogError("OnPurchaseSuccess failed. productName is null or empty.");
            return;
        }

        // 通过事件交给 PurchaseManager 统一处理奖励发放
        var shopData = GetRewardByProductName(productName);
        if (shopData == null)
        {
            this.LogError($"OnPurchaseSuccess failed. ShopData lost: {productName}");
            return;
        }
        if (shopData.Rewards == null)
        {
            this.LogError($"OnPurchaseSuccess failed. shopData.Rewards is null. productName:{productName}");
            return;
        }

        this.Log($"OnPurchaseSuccess get reward. productName:{productName}, rewardCount:{shopData.Rewards.Count}");
        OnPurchase(productName, shopData.Rewards);
        if (showTips)
        {
            SystemTips.Show(LocalizationManager.GetTranslation("U_HurryTips_Shop_succeed"));
            AudioManager.PlaySound("shopGetReward");
        }
    }

    // 购买失败
    public void OnPurchaseFailed()
    {
        string tipsKey = UnityEngine.Application.internetReachability == UnityEngine.NetworkReachability.NotReachable
            ? "U_NoNetWork_Tips_01"
            : "U_HurryTips_Shop_failed";
        SystemTips.Show(LocalizationManager.GetTranslation(tipsKey));
    }

    private void OnPurchase(string productName, List<Config.ShopReward> rewards)
    {
        this.Log($"OnPurchase enter. productName:{productName}, rewardCount:{rewards?.Count ?? 0}");
        if (rewards == null || rewards.Count == 0)
        {
            this.LogWarning($"OnPurchase skipped. rewards is null or empty. productName:{productName}");
            return;
        }

        if (DB.GameData?.PurchaseCache == null)
        {
            this.LogError($"OnPurchase failed. PurchaseCache is null. productName:{productName}");
            return;
        }

        if (GameManager.Instance?.Bag == null)
        {
            this.LogError($"OnPurchase failed. GameManager.Instance or Bag is null. productName:{productName}");
            return;
        }

        this.Log($"OnPurchase apply rewards. productName:{productName}");

        // 用于记录购买次数，内购回调之前统一验证
        if (!DB.GameData.PurchaseCache.TryAdd(productName, 1))
            DB.GameData.PurchaseCache[productName]++;

        foreach (Config.ShopReward reward in rewards)
        {
            // 1.5.0 只针对免广告进行恢复.目前没有 UnlimitCoin 商品
            if ((reward.Key == GameProps.NoAds || reward.Key == GameProps.UnlimitTips) &&
                BaseParamUtils.TryGetIntParam(reward.Value, out int rewardCount))
                GameManager.Instance.Bag.AddTool(reward.Key, rewardCount, RewardType.Purchase);
        }
    }

}

