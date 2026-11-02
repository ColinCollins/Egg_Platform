# ShopPanel 与 ShopItem 设计说明

## 概述

`ShopPanel` 负责展示和管理商店中的所有商品项 `ShopItem`。

- `ShopItemBase` 统一商品按钮生命周期、交互开关和折扣展示。
- `ShopItem` 继承 `ShopItemBase`，负责普通商店单项展示、价格文本和售罄表现。
- `ShopPanel` 负责数据装配、购买流程、广告奖励流程、恢复购买流程、按钮状态控制，以及金币飞行动画。

当前商品类型分为三类：

- `Purchase`：内购商品
- `Ad`：激励广告商品
- `Other`：预留类型，当前仅记录日志

## 主要职责

| 功能 | 当前行为 |
| --- | --- |
| 商品展示 | 根据 `shopId` 从 `TbShop` 读取配置并刷新显示 |
| 内购购买 | 通过 `PurchaseManager.Purchase()` 发起 IAP |
| 广告奖励 | 通过 `RewardAdHelper.TryToShowRewardAd()` 发起激励广告 |
| 奖励发放 | 根据 `Shop.Rewards` 发放 `Coin / NoAds / UnlimitTips` 等奖励 |
| 售罄判断 | 根据购买次数上限和已拥有权益控制售罄 |
| iOS 恢复购买 | 手动调用 `Purchase.Restore()`，等待本次资产更新后执行 `SyncRestore()` |
| 金币动画 | 只有奖励中包含 `Coin` 时才播放 `CommonUIRewardAnimCtrl` |
| 并发保护 | 购买、广告、恢复购买期间统一锁按钮 |

## 结构

```text
ShopPanel : BaseUIView
|- rewardAnimCtrl : CommonUIRewardAnimCtrl
|- CloseBtn
|- RestoreBtn
`- items : List<ShopItem>
   `- ShopItem : ShopItemBase
      |- PurchaseButton
      |- discountPart / discountText
      |- ItemPriceTxt
      |- ItemCountTxt
      |- soldOut
      `- shopId
```

## 数据来源

### ShopItem

- `shopId -> TbShop.GetOrDefault(shopId)`
- 读取 `Rewards / BuyTimes / GoodsName / GoodsDes / Discount`

### Purchase 商品价格

- `ShopData.Id -> PurchaseManager.TryResolveUniqueProduct(Shop.Id)`；缺失或重复映射都返回失败，不静默选择第一条
- `product.Id -> PurchaseManager.GetPrice(product.Id)`

## 商品类型行为

### Purchase

- 点击后走 `TryToPurchase()`
- 会通过 `GetProductByShopItem()` 反查对应的 `TbProducts`
- 成功后先调用 `PurchaseManager.OnPurchaseSuccess(productName)`
- 由于当前公共购买逻辑没有统一发放 `Coin`，`ShopPanel.OnPurchaseCallback()` 会额外调用 `ApplyPurchaseCoinReward()`
- 如果奖励包含 `Coin`，会播放金币飞行动画
- 完成后执行 `RefreshItems()` 和 `RefreshButtonStates()`

### Ad

- 点击后走 `TryToWatchAd()`
- 广告成功后，在 `OnWatchAdResult()` 中重新从 `TbShop` 取一次最新 `Shop`
- 遍历 `shop.Rewards`，逐项调用 `Bag.AddTool(..., RewardType.Ad)`
- 只有奖励中包含 `GameProps.Coin` 时才调用 `PlayRewardAnimCtrl()`
- 不管是否有金币奖励，最后都会执行 `RefreshItems()`

### Other

- 点击后进入 `OnOtherItemClick()`
- 当前仅记录日志，没有正式业务处理

## 奖励发放规则

### 购买成功

购买成功后的奖励链路如下：

1. `PurchaseManager.OnPurchaseSuccess(productName)` 处理公共购买逻辑
2. `ShopPanel.ApplyPurchaseCoinReward()` 补发 `Coin`
3. `ShopPanel.PlayRewardAnimCtrl()` 按需播放金币动画
4. `ShopPanel.RefreshItems()` 刷新商品状态
5. `ShopPanel.RefreshButtonStates()` 恢复按钮交互

### 广告成功

广告成功后的奖励链路如下：

1. 从 `TbShop` 重新读取奖励配置
2. 遍历 `shop.Rewards` 发奖
3. 如果包含 `Coin`，播放金币动画
4. 执行 `RefreshItems()`
5. 通过 `RefreshButtonStates()` 恢复交互

## 金币飞行动画

`ShopPanel` 使用 `CommonUIRewardAnimCtrl` 播放金币飞行动画。

当前规则：

- 动画起点：当前点击商品的 `PurchaseButton.transform`
- 动画终点：`rewardAnimCtrl` 内部配置的默认 `endPoint`
- 只有奖励中包含 `Coin` 时才播放
- 动画数量不直接等于奖励金币值，而是通过区间映射

### Inspector 可配置字段

- `minCoinRewardAnimCount`
- `midCoinRewardAnimCount`
- `largeCoinRewardAnimCount`
- `hugeCoinRewardAnimCount`
- `maxCoinRewardAnimCount`

`OnValidate()` 会把这些值限制为至少 `1`。

### 数量映射区间

| 奖励金币数 | 动画数量配置 |
| --- | --- |
| `< 250` | `minCoinRewardAnimCount` |
| `>= 250` | `midCoinRewardAnimCount` |
| `>= 500` | `largeCoinRewardAnimCount` |
| `>= 1000` | `hugeCoinRewardAnimCount` |
| `>= 2000` | `maxCoinRewardAnimCount` |

## 按钮状态与并发保护

`ShopPanel` 当前通过 3 个忙碌态统一控制交互：

- `_isPurchasing`
- `_isRewardAdRequesting`
- `_isRestoring`

### 统一规则

- 任一状态为 `true` 时，`CloseBtn` 不可点击
- 任一状态为 `true` 时，`RestoreBtn` 不可点击
- 任一状态为 `true` 时，所有 `ShopItem.PurchaseButton` 不可点击
- `TryToPurchase()`、`TryToWatchAd()`、`TryToRestore()`、`Close()` 在忙碌态下直接忽略

### 刷新入口

`RefreshButtonStates()` 是唯一的按钮状态收口点：

- 根据 `IsAnyActionInProgress()` 计算是否可交互
- 只对当前激活中的按钮恢复可点击状态
- 不会覆盖 `ShopItem.IsSoldOut` 的售罄状态
- 避免在售罄场景下错误恢复商品按钮

### SoldOut 点击约束

- `ShopItem.SetSoldOut(true)` 会把 `IsSoldOut` 置为 `true`
- `RefreshButtonStates()` 不会重新打开 sold-out 的 `PurchaseButton`
- `ShopItemBase` 在按钮可交互时才转发点击，`ShopItem` 还会再次检查 `IsSoldOut`
- `CustomButton.OnPointerClick()` 也会在 `Interactable = false` 时直接拦截点击

## 售罄逻辑

### Purchase 类型

会在以下情况下标记为售罄：

- `BuyTimes` 达到购买上限
- 奖励包含 `Coin` 且当前已拥有 `UnlimitTips`
- 奖励包含 `NoAds` 且当前已拥有 `NoAds`

### Ad 类型

当前主要通过 `IsNoNeedByHints(shop)` 控制售罄：

- 当奖励包含 `Coin`
- 且当前已拥有 `UnlimitTips`

## 生命周期

| 时机 | 当前行为 |
| --- | --- |
| `OnCreate` | 绑定关闭按钮、恢复按钮、商品点击事件，初始化商品数据，创建自动提示模块 |
| `OnShow` | 刷新商品显示，刷新按钮状态 |
| `OnOpen` | 重置忙碌态，重新绑定事件，刷新商品，注册恢复购买监听，刷新按钮状态 |
| `OnClose` | 清理事件、重置忙碌态、清空待购买项、释放自动提示模块 |
| `OnPurchaseCallback(isSuc=true)` | 发放购买奖励、补发金币、播放金币动画、刷新商品、恢复交互 |
| `OnWatchAdResult(isSuc=true)` | 发放广告奖励、按需播放金币动画、刷新商品、恢复交互 |
| `OnRestoreSuccess` | 刷新商品并恢复交互 |

## 关键接口

### ShopPanel

```csharp
private void InitItems();
private void RefreshItems();
private Config.Products GetProductByShopItem(ShopItem shopItem);
private void TryToPurchase(ShopItem shopItem);
private void TryToWatchAd(ShopItem shopItem);
private void TryToRestore(CustomButton _);
private void OnPurchaseCallback(string productName, bool isSuc);
private void OnWatchAdResult(ShopItem shopItem, bool isSuc);
private void ApplyPurchaseCoinReward(ShopItem purchasedItem);
private void PlayRewardAnimCtrl(ShopItem purchasedItem);
private int ResolveCoinRewardAnimCount(int coinRewardAmount);
private void RefreshButtonStates();
```

### ShopItem

```csharp
public string ShopId { get; }
public Shop ShopData { get; set; }
public ShopItemType ItemType;
public event Action<ShopItem> PurchaseClicked;

public void Refresh();
public void SetPrice(string priceText);
public void PlayShowAnimation();
public void SetSoldOut(bool isSoldOut);
```

### ShopItemBase

```csharp
public void SetDiscount(int discount);
public void SetItemInteractable(bool interactable);
protected abstract void OnItemActionClicked();
```

## 已知限制

- `Other` 类型仍然只是占位逻辑
- `GetProductByShopItem()` 仍通过 `FirstOrDefault` 遍历 `TbProducts`
- `PurchaseManager.OnPurchaseSuccess()` 目前没有统一处理 `Coin`
- 因此 `ShopPanel` 仍需在购买回调中自行补发金币，后续如果公共购买逻辑下沉，需要同步调整，避免重复发奖

## 文件位置

- 主逻辑：[Assets/Game/UI/ShopPanel/Scripts/ShopPanel.cs](D:/GitStore/BrinPunk/BrainPunk/Assets/Game/UI/ShopPanel/Scripts/ShopPanel.cs)
- 商品项：[Assets/Game/UI/ShopPanel/Scripts/ShopItem.cs](D:/GitStore/BrinPunk/BrainPunk/Assets/Game/UI/ShopPanel/Scripts/ShopItem.cs)
- 商品展示基类：[Assets/Game/UI/ShopPanel/Scripts/ShopItemBase.cs](D:/GitStore/BrinPunk/BrainPunk/Assets/Game/UI/ShopPanel/Scripts/ShopItemBase.cs)
- 奖励提示弹窗：[Assets/Game/UI/ShopPanel/Scripts/ShopRewardTipPopup.cs](D:/GitStore/BrinPunk/BrainPunk/Assets/Game/UI/ShopPanel/Scripts/ShopRewardTipPopup.cs)
- 文档：[DevelopMD/ShopPanel_ShopItem_Design.md](D:/GitStore/BrinPunk/BrainPunk/DevelopMD/ShopPanel_ShopItem_Design.md)

## 2026-09-02 Shop 奖励调用链适配 ParamBase 结构

### 需求与原行为基线

- `ShopItem`、`ShopPanel`、`ShopRewardTipPopup` 和 `PurchaseManager` 原先按 `Dictionary<GameProps, int>` 读取或复制奖励。
- `#Shop.xlsx` 的 `rewards` 已调整为 `Dictionary<GameProps, ParamBase>`，数值奖励由 `IntParam.Value` 承载，Skin ID 由 `StringParam.Value` 承载。
- 旧的直接读取、复制和 `Bag.AddTool` 调用会产生 9 个 `CS1503` 与 1 个 `CS0029`，导致工程无法随新配置代码编译。

### 新行为与数据流

- `ShopItem` 仍只负责显示 `Coin` 数量，不消费或发放 `Skin` 奖励。
- `BaseParamUtils.TryGetIntParam()` 只接受 `IntParam`；数字形式的 `StringParam` 不会被误解为奖励数量。
- `ShopPanel` 的广告发奖、购买金币补发和金币动画数量只读取 `IntParam`；`ShopRewardTipPopup` 的数量显示与广告发奖执行相同过滤。
- `PurchaseManager` 把 Shop 奖励复制为旧购买流程所需的整数奖励字典时只保留 `IntParam`，随后仍由原有逻辑仅发放 `NoAds / UnlimitTips`。
- `StringParam` Skin ID 在这些既有通用路径中被明确跳过，尚未接入 Skin 所有权写入或礼盒专用恢复交付。
- 缺少 Coin、类型不是 `IntParam` 或数值不大于零时，继续沿用原有空数量/不播放金币动画表现；价格、点击转发、售罄状态、购买次数和原有奖励分工均未改变。

### 验证、风险与回滚

- 执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:q -consoleLoggerParameters:ErrorsOnly`，结果为 `0 error / 122 warning`；原 10 个 Shop 奖励结构错误全部消失。
- 已扫描全部 `TbShop` 调用方和旧式 `out int`、直接字典复制、直接传递 `reward.Value` 模式，未发现其它待适配的 Shop 奖励调用点。
- Unity PlayMode、真实 IAP/Restore、广告回调、Skin 奖励交付和 Android/iOS 横屏表现尚未验证，状态为 `uncertain`。
- 回滚时移除 `TryGetIntParam()` 并恢复四个调用方对整数奖励的直接读取；该回滚只适用于 Shop 表也同步恢复旧奖励结构的情况。

## 2026-09-03 ShopItemBase 与 Shop.discount 折扣展示

### 需求与原行为基线

- Shop 配置已将旧 `soldCount` 字段替换为整数 `discount`，但 `ShopItem` 没有读取该字段，Prefab 中现有的折扣节点仍保留静态示意文本。
- `ShopItem` 自己管理按钮绑定；CoinGiftPopup 直接管理两套购买按钮和数量/价格文本，两个模块缺少可复用但不夹带购买业务的 Item 边界。

### 当前结构与行为

- 新增 `ShopItemBase : BaseAutoUIBind`，只负责 `CustomButton` 的防重复绑定/解绑、统一交互开关和 `discountPart / discountText` 展示；不读取存档、不调用 IAP/广告、不发奖励。
- `ShopItem` 改为继承 `ShopItemBase`，原 `Btn` 序列化字段名保持不变，售罄、动画、ShopId、数量和价格职责仍在 `ShopItem`。
- `ShopItem.Refresh()` 读取 `Shop.Discount`。值为 0 时隐藏折扣区域并清空文本；值非 0 时显示，文本固定使用 `string.Format("{0} Off", discount)`。
- `ShopPanel.prefab` 中实际存在折扣节点且配置非 0 的四个 Item 已完成绑定：`20003=25`、`20004=38`、`20005=60`、`20006=16`。`20000/20001/20002` 配置为 0，保持不绑定折扣区域。
- CoinGift 使用同一基类的 `CoinGiftItem` 子类；它只增加金币数量、本地化现价和可选划线原价展示，购买快照、校验、请求防重和交付仍由 `CoinGiftController` 负责。

### 验证、风险与回滚

- 只读核对 `#Shop.xlsx` 与 `tbshop.json`，确认上述七个 ShopPanel Item 的折扣配置与 Prefab 绑定一致。
- Prefab 定向校验结果：四个非 0 ShopItem 的 `discountPart / discountText` 均非空，三个 0 值 Item 无折扣引用；运行时程序集 `0 error / 122 warning`，Editor 程序集 `0 error / 4 warning`。
- Unity PlayMode、不同语言字体宽度、Android/iOS 横屏和实际售罄切换后的视觉仍为 `uncertain`。
- 回滚时可让 `ShopItem` 恢复直接继承 `BaseAutoUIBind` 和本地按钮绑定，并移除四个折扣引用及 `ShopItemBase`；该操作不需要修改 Shop 配置或购买存档。
## 2026-06-15 Skin scroll responsibilities

- `InfiniteScrollView` is a ShopPanel-local bounded-slot ScrollRect adapter for skin browsing. It owns slot creation/reuse, scroll listener cleanup, finite/cyclic effective mode, content sizing, and beginning reset on full refresh.
- `ShopPanel_SkinPart` owns skin catalog preparation and filters. It reads `TbSkinShop` order when available, resolves rows from `TbSkin`, falls back to `TbSkin.DataList`, and maps filter toggles to all/back/face/body lists.
- `SkinItem` owns one visible slot's data binding. It clears stale visual and click state on each bind, then applies skin name, quality background, selection marker, unlock part, price/hint text, and current-entry click forwarding.
- Existing `ShopPanel` purchase, rewarded-ad, restore, sold-out, reward animation, and busy-state logic remains outside the skin scroll path and was not edited for this feature.
- Existing `PageView` remains unchanged because it is a finite page-snap component, not the virtualized/infinite skin list implementation target.

## 2026-06-15 Skin version2.0.0 unlock responsibilities

- `ShopPanel_SkinPart` owns skin ownership, unlock, and apply logic for `Config.Skin` rows. This path is separate from `ShopPanel` / `ShopItem` goods, which remain driven by `TbShop` rewards.
- Owned skin ids are stored in `DB.GameData.OwnedSkin`. Current applied parts are stored in `CurrentBackPart`, `CurrentFacePart`, `CurrentBodyPart`, and `CurrentSkinId`.
- `SkinUnlockType.Default` rows are added to `OwnedSkin` automatically at runtime so default entries behave like owned entries after refresh.
- Clicking an owned skin currently applies it immediately. Back, face, and body rows update their matching part field; other rows update `CurrentSkinId`. 2026-06-23 note: SkinPart v2.0 planning changes the target behavior to preview-first clicks, so this line describes the current baseline rather than the desired v2.0 end state.
- `SkinUnlockType.Ad` shows the skin ad action button, then uses `RewardAdHelper.TryToShowRewardAd(RewardPlacement.ShopReward.ToString())`; a successful callback grants and applies the captured skin.
- `SkinUnlockType.Coin` shows the coin action button, checks `PlayCtrl.Instance.Bag` coin count, shows `U_HurryTips_NeedCoin` when insufficient, and removes coins with `CostType.Other` before granting.
- `SkinUnlockType.Purchase` shows the purchase action button, resolves `Products.GetProductId(skin.ProductId)`, sends `IAP_CLK(productId.Sku)`, then calls `PurchaseManager.Purchase(skin.ProductId, "skin", callback)`.
- Skin purchase success grants the skin directly and intentionally does not call `PurchaseManager.OnPurchaseSuccess(...)`; the current skin flow does not use `TbShop.Rewards`, so the shared shop reward grant path would not apply.
- `UISpineCtrl.SetCombinedSkin(...)` is the current viewer path for part skins. It combines the base skin name plus current back/face/body names and resets slots to setup pose after applying valid Spine skins.
- Pending validation: production `SpineResource` values must be confirmed as Spine skin names, and `ShopPanel.prefab` button/text child lookup must be verified in PlayMode.

## 2026-06-15 Skin version3.0.0 scene preview responsibilities

- `ShopPanel_SkinPart` controls the Main scene root object named `Skin` while the Skin section is active. The scene object is serialized inactive, activated on SkinPart enable, and deactivated on SkinPart disable/destroy.
- `ShopPanel.OnClose()` calls `ShopPanel_SkinPart.HideSceneSkinPreview()` as a guard so the scene `Skin` object is hidden even when ShopPanel closes from a path where SkinPart is not active.
- The scene preview camera is `SkinRt_Camera`, and its target texture is `Assets/Game/UI/ShopPanel/boxer.renderTexture`. The ShopPanel-local `Boxer_Rt` RawImage displays that same RenderTexture.
- Boxer part preview mount points are the scene actor child objects `back`, `face`, and `body`. They are world-space `Transform` BoneFollower nodes, so SkinPart resolves them as `Transform`, not `RectTransform`.
- Part preview prefab loading uses `Config.Skin.PrefabResource` directly through the Bear resource system after YooAsset location validation. SkinPart does not build paths from folder names, skin names, or legacy `prefab1_*` naming conventions.
- Empty, missing, invalid, or unloadable `PrefabResource` clears only the failed part content and keeps the Skin list and ShopPanel controls usable.
- Pending validation: many current `#Skin.xlsx` / `tbskin.json` rows still contain legacy `prefab1_*`, `prefab2_*`, and `prefab3_*` values. Those rows need resource/config completion before all preview parts can load in PlayMode.

## 2026-06-16 Skin resource binding responsibilities

- `Config.Skin.TextureResources` is the SkinItem list icon resource key. UI list cells load this value as a `Sprite`, resolving current part folders under `Assets/Game/ArtSrc/Skins`.
- `Config.Skin.PrefabResource` is the boxer display preview resource key. Scene preview parts load this value as a `GameObject` prefab from `Assets/Game/Prefabs/Skins`.
- `AssetBundleCollectorSetting.asset` keeps both skin resource roots in the `Skin` group, but the collectors are type-specific: `Assets/Game/ArtSrc/Skins` uses `CollectSprite`, and `Assets/Game/Prefabs/Skins` uses `CollectPrefab`.
- The runtime save key for part skins remains the configured `PrefabResource` value. The full asset path is only a load-location resolution detail and is not written to save data.
- Current first-batch authored resource names intentionally overlap between sprites and prefabs, such as `Sp_zs_bak_img_001`. Runtime code resolves full paths by usage so sprite and prefab resources do not depend on a shared addressable bare-name namespace.
- Current first-batch sprite folders are not fully aligned with `SkinPart` names, so SkinItem validates candidate icon paths through `Bear.ResourceSystem.ResourceManager.CanLoad<Sprite>()` before loading. This avoids invoking the loader for missing legacy texture rows.
- Pending validation: generated manifests need to confirm the full resolved sprite and prefab paths are valid in the production `GamePackage`; PlayMode needs to confirm SkinItem images clear or update correctly during infinite-list slot reuse.

## 2026-06-16 Skin resource loading boundary

- `SkinItem` and `ShopPanel_SkinPart` now depend on `Bear.ResourceSystem.ResourceManager` for both resource load and loadability checks.
- Skin feature code no longer calls `YooAssets`, `TryGetPackage`, or `CheckLocationValid` directly. YooAsset-specific checks are isolated in the Bear resource-system adapter.
- `ResourceManager.CanLoad<Sprite>(location)` is used before SkinItem icon loading, so invalid sprite candidates are skipped without entering the async loader failure/retry branch.
- `ResourceManager.CanLoad<GameObject>(location)` is used before boxer preview prefab loading, so invalid prefab locations clear only the affected preview part.
- This keeps the resource backend replaceable at the ShopPanel Skin layer and preserves the existing missing-resource behavior.

## 2026-06-24 RestorePurchase WaitingPopup 关闭保护

### 需求背景

ShopPanel 的 iOS `RestorePurchase` 会在恢复购买流程进行中锁住关闭按钮、复购按钮和普通商品按钮。新增防护要求：如果 `WaitingPopup` 已经被用户、超时或外部流程关闭，ShopPanel 按钮交互需要立即恢复，避免 IAP SDK 长时间未返回时面板持续不可操作。

### 影响文件与系统

- `Assets/Game/UI/ShopPanel/Scripts/ShopPanel.cs`：新增 RestorePurchase 等待弹窗关闭回调，恢复 `_isRestoring` 锁和按钮状态。
- `Assets/Game/UI/Global/Scripts/WaitingPopup.cs`：复用既有 `SetOnCancel(...)` / `CloseStraightly()` 回调能力，未修改通用弹窗实现。
- `Assets/Game/Scripts/SDK/Services/Purchase/PurchaseManager.cs`：仍由 `Restore()` 调用 IAP SDK，未修改 SDK 恢复、补发或购买成功逻辑。

### 原行为基线

- `TryToRestore()` 设置 `_isRestoring = true` 后调用 `RefreshButtonStates()`，统一禁用 `CloseBtn`、`RestoreBtn` 和所有未售罄 `ShopItem.PurchaseButton`。
- `IosRestoreAsync()` 原本只在 `await GameManager.Instance.Purchase.Restore()` 返回后的 `finally` 中清理 `_isRestoring` 并恢复按钮。
- 如果 `WaitingPopup` 先于 SDK 返回被关闭，按钮仍可能持续锁定直到 SDK 返回。

### 新行为

- `IosRestoreAsync()` 创建 `WaitingPopup` 后注册本次 Restore 专用关闭回调。
- 当 `WaitingPopup` 被关闭时，回调会把 `_isRestoring` 置为 `false`，并在 ShopPanel 仍有效时调用 `RefreshButtonStates()`。
- 若 SDK 后续才返回，`finally` 会再次刷新按钮状态，但不会重复关闭已经关闭过的 WaitingPopup，避免误关后续其他等待弹窗。

### 流程边界

- 本次不改变 `PurchaseManager.Restore()`、`SyncRestore()`、`OnRestoreSuccessEvent`、奖励补发、购买缓存、金币动画、商品售罄、存档或 analytics 流程。
- 复购 SDK 仍可能继续等待或稍后返回；本次只解除 ShopPanel UI 按钮锁，不取消 SDK 恢复请求。
- `WaitingPopup.SetOnCancel(...)` 为静态单回调能力；当前防护依赖 RestorePurchase 期间 ShopPanel 自身按钮已被锁定，避免同面板内并发启动其他等待流程。

### 验证

- 已执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`，结果为 0 error / 96 warnings；warnings 属于当前工程既有范围。
- 已静态检查 `ShopPanel.cs` diff：改动只在 `IosRestoreAsync()` 与新增 `OnRestoreWaitingPopupClosed()`，未改 ShopItem、PurchaseManager、WaitingPopup 通用实现或 prefab。

### 未验证风险

- 未在 Unity PlayMode 或 iOS 真机验证手动关闭 `WaitingPopup` 后按钮立刻恢复。
- 未验证 App Store restore 弹窗、网络超时、用户取消等真实平台回调组合。

### 回滚路径

移除 `IosRestoreAsync()` 中的 `WaitingPopup.SetOnCancel(...)` 注册和 `waitingPopupClosed` 局部标记，删除 `OnRestoreWaitingPopupClosed()`，恢复为只在 `finally` 中关闭 WaitingPopup 并恢复按钮。

## 2026-07-09 RestorePurchase 转换弹窗与商品状态刷新

### 需求背景

ShopPanel 的 iOS RestorePurchase 完成后，需要根据恢复补发出的旧版 Tips / UnlimitTips 权益弹出 `TransformPopup`，并刷新 ShopPanel 商品项状态，避免恢复后商品仍显示可购买或转换入口没有出现。

### 影响文件与系统

- `Assets/Game/UI/ShopPanel/Scripts/ShopPanel.cs`：调整 restore 完成、恢复成功事件和商品状态刷新逻辑。
- `Assets/Game/Scripts/SDK/Services/Purchase/PurchaseManager.cs`：作为证据链，`OnRestored(...) -> SyncRestore(...)` 会补发购买奖励并派发 `OnRestoreSuccessEvent`；本次未修改。
- `Assets/Game/UI/CommonPopup/Scripts/TransformPopup/TransformPopup.cs`：作为证据链，`HasTransformableTips()` 根据 `Tips` 或 `UnlimitTips` 判断是否需要转换；本次未修改。

### 原行为基线

- `TryToRestore()` 设置 `_isRestoring = true` 后发起 `IosRestoreAsync()`。
- `IosRestoreAsync()` 原本在 `await GameManager.Instance.Purchase.Restore()` 返回后立即调用 `TransformPopup.HasTransformableTips()`，但恢复补发实际发生在 SDK 资产回调 `OnRestored(...)` 触发的 `SyncRestore()` 中。
- 如果 `Restore()` 返回早于 `OnRestored(...) -> SyncRestore()` 补发完成，ShopPanel 的转换判断会早于背包状态更新，因此不会弹出 `TransformPopup`。
- `OnRestoreSuccess(...)` 原本会刷新 items，但 `IosRestoreAsync()` 的 finally 路径只恢复按钮状态；没有覆盖“无成功事件或事件时序异常时仍刷新商品状态”的需求。
- `IsUnlimitHints()` 原本只检查 `UnlimitCoin`；恢复补发阶段先写入旧版 `UnlimitTips`，需要等待 `TransformPopup` 关闭时 `SimpleBag.SyncOldVersion()` 才转换为 `UnlimitCoin`。

### 新行为

- Restore 每次开始时重置本次 ShopPanel 生命周期内的转换弹窗弹出标记。
- `OnRestoreSuccess(...)` 在恢复补发成功事件后刷新 `RefreshItems()`，并检查 `TransformPopup.HasTransformableTips()`；命中时打开 `TransformPopup`。
- `IosRestoreAsync()` 的 finally 会先关闭本次 `WaitingPopup`，再在 ShopPanel 仍激活时执行 `RefreshItems()` 和转换弹窗兜底检查。
- 转换弹窗通过 `_hasShownRestoreTransformPopup` 防重，同一次 ShopPanel 生命周期内只会由 restore 路径打开一次。
- `IsUnlimitHints()` 同时检查 `UnlimitCoin` 和待转换的 `UnlimitTips`，使 restore 后商品状态能在转换弹窗关闭前也反映旧版无限提示权益。

### 数据、UI 与平台流程

- 本次不修改 `PurchaseManager.Restore()`、`OnRestored(...)`、`SyncRestore(...)`、奖励补发、购买缓存、SDK 回调或 analytics 流程。
- ShopPanel 继续通过现有 `OnRestoreSuccessEvent` 接收恢复成功通知，没有新增全局事件。
- `TransformPopup` 仍由自身在关闭时调用 `PlayCtrl.Instance.Bag.SyncOldVersion()` 完成旧 Tips / UnlimitTips 到 Coin / UnlimitCoin 的转换。
- iOS restore 按钮锁、等待弹窗关闭保护和商品按钮 `Interactable` 仍收口到 `_isRestoring` 与 `RefreshButtonStates()`。

### 验证

- 已静态检查 `ShopPanel.cs`：restore 后 `TransformPopup` 检查从 `Restore()` await 后移到 `OnRestoreSuccess(...)` 和 finally 兜底，`RefreshItems()` 会在恢复成功事件和 restore 完成路径执行。
- 已静态核对 `PurchaseManager.cs`：`OnRestored(...)` 会调用 `SyncRestore()`，成功补发后派发 `OnRestoreSuccessEvent`。
- 已静态核对 `TransformPopup.cs`：`HasTransformableTips()` 读取 `Tips` 与 `UnlimitTips`，关闭弹窗时仍调用 `SyncOldVersion()`。
- 已执行 `git diff --check -- Assets/Game/UI/ShopPanel/Scripts/ShopPanel.cs DevelopMD/ShopPanel_ShopItem_Design.md DevelopMD/已有功能设计与使用记录.md`，未发现空白错误，仅有工作区 LF/CRLF 转换提示。
- 已执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`，被当前 `Temp\bin\Debug` 元数据 DLL 缺失阻断，首个错误为 `Apple.Core.dll` 缺失，未获得业务脚本编译诊断。

### 未验证风险

- 尚未执行 Unity PlayMode；需要验证 iOS restore 成功后 `OnRestoreSuccessEvent` 时序、`TransformPopup` 可见层级、ShopPanel item 售罄状态和按钮交互恢复。
- 尚未执行 iOS 真机或沙盒 App Store restore；真实 SDK 回调顺序、用户取消、无可恢复商品、网络失败和 WaitingPopup 手动关闭组合仍待补验。
- Android 横屏 smoke 未覆盖；本次 restore 按钮仍受 `UNITY_IOS` 条件控制，Android 侧主要是确认 ShopPanel 普通购买和广告商品未受影响。

### 回滚路径

回滚 `ShopPanel.cs` 中 `_hasShownRestoreTransformPopup`、`TryShowRestoreTransformPopup()`、`IosRestoreAsync()` finally 的 `RefreshItems()` / 转换弹窗兜底、`OnRestoreSuccess(...)` 的转换弹窗检查，以及 `IsUnlimitHints()` 对 `UnlimitTips` 的兼容检查，即可恢复到只在 `Restore()` await 后立即判断转换弹窗的旧行为。

## 2026-07-27 Draw 玩法暂停宿主传递

- `ShopPanel.Create(...)` 新增尾部可选参数 `IPlayPauseHost pauseHost`。旧调用不传参数时继续派发 `SwitchGameStateEvent(PLAYING)`，主线行为不变。
- Draw HUD 打开商店前通过活动 `IPlayPauseHost` 暂停；商店关闭且没有后续奖励提示时，由 `ShopPanel` 直接恢复同一宿主。
- 若商店关闭后自动打开 `ShopRewardTipPopup`，暂停宿主会继续传入奖励提示，直到奖励提示关闭或领奖动画完成后才恢复。
- 商店或奖励提示创建失败时不会记录自动提示已展示；Draw 商店创建失败时由 HUD 立即恢复玩法。
- 本次不改变商品、广告、IAP、奖励、存档、配置、analytics 或平台 SDK 流程。PlayMode 与 Android/iOS 横屏仍待验证。
- Draw 入口的完整功能、风险和回滚见 `Assets/Game/UI/DrawGame/GamePlayPanel/Scripts/DrawGame_GamePlayPanel商店入口功能与风险评估.md`。

## 2026-08-07 Level71 免费钥匙动画关闭入口

- 原关闭按钮的忙碌门禁、`UIManager.DestroyUI(this)` 与 `IAP_CLOSE(lastUIName)` 已收口到 `internal TryClosePanel()`；关闭按钮自身只转调该入口，正常关闭顺序不变。
- `Level71ShopBtnCtrl` 在免费钥匙飞走渐隐动画完成后调用同一入口，避免绕过 `ShopPanel.OnClose()` 的引导清理、奖励提示判定与暂停恢复链。
- `TryClosePanel()` 在购买、广告或恢复购买忙碌时返回 false；局部调用方必须解除自己临时开启的输入遮罩。`UIManager` 不可用时同样返回 false，不发送 IAP 关闭打点。
- Level71 免费钥匙不加入 `items`，不读取 `TbShop` / `TbProducts`，也不改变商品发奖、售罄或按钮忙碌逻辑。完整行为与风险见 `Assets/Game/Scripts/Level/MainLevel/Level_71/Level71商店免费钥匙功能说明.md`。

## 2026-09-03 Shop/Product 唯一映射公共化

- 原 `ShopPanel.GetProductByShopItem()` 直接对 `TbProducts` 使用 `FirstOrDefault`，CoinGift 与 ChestGift 又各自维护同类反查，重复 RewardId 时会静默选中首条。
- `PurchaseManager.TryResolveUniqueProduct(shopId, out product, out error)` 现在是唯一公共入口：只负责 `Shop.Id -> Product.RewardId` 配置关系，不混入价格、SKU、限购或奖励规则。
- ShopPanel、CoinGiftController 与 ChestGiftController 均改为复用该入口；缺失或重复映射时记录明确原因并保持购买不可用。
- `PurchaseManager.TryGetProductId()` 同步提供公共平台映射入口，并在 Guru 生成列表未更新时从唯一 `TbProducts.Id` 兜底构造。ProductType 仅映射平台交易属性，`BuyTimes` 与 `PurchaseCache` 才是限购依据。
- 静态扫描确认上述三个调用方已无 RewardId 首条反查；运行时程序集构建为 `0 error / 122 warning`。真实平台取价和购买仍需 Android/iOS 真机验证。回滚时可恢复各调用方本地反查，但会重新引入重复配置静默命中的风险。

## 2026-09-08 discount 百分号与换行

- 当前展示契约更新为首行 `{num}%`、次行小写 `off`，替代此前 `{num} Off` 的历史格式；配置为 0 仍隐藏并清空文本。
- 本地证据：`ShopItemBase.SetDiscount` 是 `ShopItem` 与 `CoinGiftItem` 的共享展示入口；仅修改格式和与之冲突的说明，沿用 CoinGift 两个及 ShopPanel 四个折扣文本 Inspector 引用。
- 定向引用检查及 diff 空白检查通过；dotnet 编译被 200 个依赖 DLL 缺失错误阻断，Unity 与 Android/iOS 横屏显示仍为 `uncertain`。
- 完整需求、流程、验证与回滚见 `Assets/Game/UI/GiftPopup/Scripts/CoinGiftPopup功能与风险记录.md` 的同日章节。

## 2026-09-14 购买入口断网前置校验

- `ShopItem` 只转发购买点击；实际 UI 锁在 `ShopPanel.TryToPurchase()`。旧版没有前置断网判断，提示依赖购买失败回调；SDK 不回调时按钮锁会持续。
- 当前在设置 `_isPurchasing`、刷新按钮、发送 `IAP_CLK` 和调用 `Purchase()` 之前检查设备无网；无网直接复用 `OnPurchaseFailed()` 显示 `U_NoNetWork_Tips_01`，不创建等待弹窗。`ShopPanel_SkinPart` 的皮肤内购入口也在设置自身请求状态前执行相同校验。
- 商店入口先确认 PurchaseManager 存在，再加购买中状态；缺失时记录错误并保持按钮可操作，避免无请求却锁住面板。正常联网请求仍使用同一 PurchaseManager 实例、原商品映射和原结果回调。
- Shop 配置、发奖、广告、恢复购买、暂停宿主及平台交易逻辑不变。静态顺序和差异检查通过；iOS 真机与切网期间无回调仍待验证。完整范围、风险和回滚见 `DevelopMD/购买入口断网前置校验.md`。
