# CoinGiftPopup 功能与风险记录

## 需求背景

- CoinGift 是金币不足场景使用的补充金币弹窗，不是按日轮换礼包。
- 弹窗包含三个入口：激励广告按 Shop 配置获得金币、首购/常规金币商品二选一、进入普通商店；当前广告 Shop 配置值为 60。
- 首购使用 Shop `20013`：280 金币、限购一次、60% 折扣，并显示到设备当地次日 0 点的倒计时；首购完成后替换为 Shop `20003` 的 500 金币常规档，不再显示首购倒计时。
- CoinGift 需要与 ChestGift 共用 GiftManager 的类型路由和窗口互斥，但不能接入 Daily Schedule 的刷新计数。
- GMPanel 的 `dailyCoinGift_btn` 只是调试入口，不代表 CoinGift 是 Daily 类型。

## 原行为

- `CoinGiftPopup.cs` 只有空生命周期和直接调用 `UIManager.OpenUI()` 的骨架，没有广告、购买、倒计时、商店跳转或奖励交付。
- 早期补接曾通过 `Transform.Find()` 和固定层级路径在运行时查找按钮、文本与首购分组；该方式与同模块生成字段、Inspector 序列化绑定方式不一致，节点改名也会在运行时失效。
- `CoinGiftPopup.Generated.cs` 曾因重复图片节点生成同名字段而出现 `CS0102`，当前生成结果已使用唯一字段名。
- CoinGift 未注册到 GiftManager，无法与 ChestGift 共用跨类型弹窗互斥。

## 当前实现

### 类型路由与职责

- `GiftPopupType` 新增 `CoinGift`；`GiftManager` 注册唯一的 `CoinGiftController`，并继续使用
  `Dictionary<GiftPopupType, IGiftController>` 路由。
- `CoinGiftController` 是普通 C# 控制器，负责 Shop/Product/SKU 校验、首购切换、请求防重、广告金币发放、购买记录与购买金币交付。
- `CoinGiftPopup` 继承 `GiftPopup`，负责商品状态切换、当地倒计时、本地化价格格式和交互编排；两套购买项由 `CoinGiftItem : ShopItemBase` 负责局部展示与点击转发。
- `CoinGiftController` 没有实现 `IScheduleRefreshable`；`GiftManager.OnScheduleRefresh()` 只会分发给 ChestGiftController，CoinGift 不读取也不增加 `ChestGiftDailyRefreshCount`。

### Prefab 与序列化绑定

- `Resources/CoinGiftPopup.prefab` 根节点挂有 `CoinGiftPopup`，Inspector Shop Id 为：广告 `20000`、首购 `20013`、替换档 `20003`。
- 关闭、商店、广告按钮和两个商品分组通过 Generated 字段序列化绑定；首购与常规购买状态各挂一个 `CoinGiftItem`，序列化绑定各自按钮、数量、现价、划线原价和 `discountPart / discountText`，首购项额外绑定倒计时文本。
- 运行时代码不再调用 `Transform.Find()`、`GetComponentsInChildren()` 或节点名兜底解析。
- 两个 `CoinGiftItem` 及其 `discountPart / discountText` 由 Prefab Inspector 直接绑定，运行时不按节点名查找。既有编辑器菜单仍只维护原 Generated 字段与 Shop Id，不自动猜测或覆盖 Item 引用。

### 广告流程

- 广告点击前读取 `rewardAdShopId` 对应 Shop 的正数 Coin `IntParam`；缺失或非正数时按钮禁用并输出一次明确错误，不再把当前配置值 60 写成代码门禁。
- 广告使用 `RewardPlacement.InGameTips`，每次请求由递增版本号防止重复回调发奖。
- 成功回调由 CoinGiftController 唯一调用 `Bag.AddTool(Coin, 60, RewardType.Ad)`，并以前后数量确认实际增加 60；视图只接收交付结果，成功后在当前 CoinGift 内播放金币飞行动画，不主动关闭弹窗，也不自动消耗底层提示。
- 广告请求期间广告、购买、商店和关闭按钮全部锁定；失败或取消后恢复当前弹窗交互。

### 购买与奖励

- 首购是否完成读取首购 Product 在 `GameData.PurchaseCache` 中的次数；达到 Shop `BuyTimes=1` 后切换到常规档。
- 首购要求 `BuyTimes=1`，首购与常规档的折扣均直接读取各自 `Shop.Discount`，不要求代码内固定为 60/25；限购次数只由 `Shop.BuyTimes + PurchaseCache` 判断，不与平台 `ProductType` 强绑定。缺 Product 映射、占位 SKU、非正 Coin 或购买次数配置无效时保持展示骨架但禁用购买。
- 两个 `CoinGiftItem` 都直接读取当前 Shop 的 `Discount`：0 隐藏折扣区域，非 0 显示并写入 `{0} Off`；当前首购显示 `60 Off`，常规档显示 `25 Off`。
- 价格使用 PurchaseManager 返回的平台本地化现价。首购与常规档的划线原价都按 `RawPrice ÷ (1-折扣)` 反算，沿用现价的前后币种符号、小数位和点/逗号分隔；元数据解析失败时隐藏划线价，不影响现价。
- 购买请求期间由 CoinGiftController 持有版本号和购买前 `PurchaseCache` 计数。成功后先调用既有 `PurchaseManager.OnPurchaseSuccess(..., false)` 记录购买，再发放一次配置 Coin；重复成功回调不会重复发奖。
- 本地交付完成后显示商店购买成功 `SystemTips` 和 `shopGetReward` 音效；CoinGift 保持打开，由当前购买按钮位置播放金币飞行动画；首购完成时当前界面立即刷新为常规档，不再额外创建 `CommonGetRewardPopup`。

### 商店入口与 GM 调试入口

- 商店按钮先关闭 CoinGift，再调用 `ShopPanel.Create(false, nameof(CoinGiftPopup))`；不会主动恢复底层玩法或替用户消费提示。
- `GMPanel` 使用 `DailyCoinGiftBtn` Generated 字段，在 `OnCreate()`/`OnClose()` 与其它按钮一致地绑定/解绑；点击统一调用 `GiftManager.Open(CoinGift)`。

## 数据、资源与事件流

```text
金币不足入口 / GM 调试入口
  -> GiftManager.Open(CoinGift)
  -> CoinGiftController.Open
  -> Resources/CoinGiftPopup

广告按钮
  -> CoinGiftController.TryShowRewardAd(Shop 20000)
  -> RewardAdHelper(InGameTips)
  -> Bag.AddTool(Coin, 60, Ad)
  -> CoinGiftPopup 内播放金币飞行动画
  -> 保持 CoinGift 打开

购买按钮
  -> TbShop + TbProducts + Products SKU 校验
  -> PurchaseManager.Purchase
  -> PurchaseManager.OnPurchaseSuccess(false)
  -> PurchaseCache 增加
  -> Bag.AddTool(Coin, 配置数量, Purchase)
  -> CoinGiftPopup 内播放金币飞行动画
  -> 保持 CoinGift 打开；首购完成则刷新为常规档
```

- 本轮没有新增全局 `PlayEvents`，也没有修改 RemoteConfig、Loading、GF 子模块、免广告购买或恢复逻辑。

## 验证证据

- 静态回读确认 CoinGiftPopup Prefab 根脚本、三个 Shop Id、两个 `CoinGiftItem` 和两个商品分组引用均非空；两个 Item 的按钮、数量、价格、折扣根和折扣文本引用均非空。
- 静态扫描确认 `CoinGiftPopup.cs` 与 `GMPanel.cs` 不再包含本功能的 `Transform.Find`、`GetComponentsInChildren` 或节点名扫描。
- 执行 `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies`：`0 error / 122 warning`；执行 `dotnet build Assembly-CSharp-Editor.csproj --no-restore --no-dependencies`：`0 error / 4 warning`。警告来自工程既有代码，本次 CoinGift 文件未产生编译错误。
- `tbshop.json` 的 `20013` 仍为 280 金币、`Discount=60`、`BuyTimes=1`；最新 `tbproducts.json` 已把 `chest_pack_1` 改映射到 ChestGift Shop `20008`，当前没有 `RewardId=20013` 的 Product，因此 CoinGift 首购购买入口会安全禁用。
- Prefab 定向校验确认按钮引用类型为 `CustomButton`、文本引用类型为 `TextMeshProUGUI`、首购/常规分组为 GameObject，嵌套关闭按钮指向 `Close_Btn.prefab` 的 `CustomButton`。
- `git diff --check` 对本次已跟踪文件通过；CoinGift C# 与本文档的逐行尾空格、重复生成字段、危险编辑器生命周期及运行时节点扫描检查通过。Unity YAML/Meta 的标准空标量未作为源码尾空格错误处理。
- Unity PlayMode、真实广告、真实 IAP、后台切换、当地跨午夜、逗号小数币种以及 Android/iOS 横屏仍为 `uncertain`。

## 已知风险与交付门禁

- Shop `20013` 当前缺少 `TbProducts.RewardId` 唯一映射；在补回独立 Product 与正式 SKU 前，首购档只能展示骨架并保持购买不可用。
- Shop `20003` 当前 `Discount=25`；后续若改为 0 或其它值，商品仍按其它有效性条件正常解析，折扣区域按最新配置显示或隐藏。
- ChestGift 的 `chest_pack_1～5` 当前配置为 `Possessive` 并进入永久资产恢复链；该类型不替代 `Shop.BuyTimes + PurchaseCache` 的限购判断。CoinGift Shop `20013` 需要独立 Product，不能复用 ChestGift 的 Product 映射。
- PurchaseCache 与 Bag 仍分别经过既有保存调用，当前接口不能确认磁盘保存是否完全原子；崩溃点恢复与保存失败重试需要后续特殊交付状态支持。
- 主线和 Draw 的获取提示/直接通关金币不足分支已接入 CoinGift；弹窗本身不接管玩法暂停，由底层提示弹窗保持所有权。通用 `InsufficientCoinsContext` 和完整入口矩阵仍未实现，PlayMode/真机暂停与原生回调仍待验证。

## 回滚方式

- 删除 `GiftPopupType.CoinGift`、GiftManager 中的 CoinGiftController 注册及 `CoinGiftController.cs/.meta`，可独立回滚类型路由；ChestGift 和 Daily Schedule 不受影响。
- 将 `CoinGiftPopup` 恢复为独立 `BaseUIView` 骨架并移除 Prefab 根组件/绑定，可回滚弹窗业务；不会迁移或删除玩家存档。
- 删除 GMPanel 的 `DailyCoinGiftBtn` 绑定、解绑和点击方法，可单独回滚 GM 调试入口。
- 配置层保持首购 Product 缺失或占位 SKU 即可安全禁售，不影响普通商店、ChestGift 和免广告商品。

## 2026-09-03 CoinGiftItem 抽象与 discount 接入

### 变更内容

- 新增 `CoinGiftItem : ShopItemBase`，首购与常规两种状态不再由 Popup 分别直接绑定购买按钮；Item 只转发点击并刷新数量、现价、可选原价和折扣。
- `CoinGiftPopup` 仍持有当前 `CoinGiftOffer`，根据 `PurchaseCache` 选择首购/常规状态，并把当前配置值传给对应 Item；`CoinGiftController` 仍唯一负责商品校验、购买防重和金币交付。
- `CoinGiftController` 的折扣校验和首购原价反算由已删除的 `Shop.SoldCount` 切换为 `Shop.Discount`，避免配置代码重新生成后编译或运行语义脱节。
- 两个 Prefab 商品状态均绑定现有 `discount` 根节点与其 TMP 文本；值非 0 时显示 `{0} Off`，值为 0 时隐藏整个根节点。

### 验证与剩余风险

- 只读核对 `#Shop.xlsx`：`20013` 为 60，`20003` 为 25；运行时 JSON 与生成的 `Config.Shop.Discount` 一致。
- Prefab 静态校验确认恰有两个 `CoinGiftItem`，两者的按钮、数量、价格、折扣根和折扣文本引用全部有效，Popup 也分别引用两个 Item。
- 运行时程序集构建为 `0 error / 122 warning`，Editor 程序集为 `0 error / 4 warning`；警告均来自工程既有代码。
- PlayMode、真实平台价格元数据、划线原价、字体在 `60 Off / 25 Off` 下的视觉和 Android/iOS 横屏仍为 `uncertain`。

### 回滚

- 删除两个 `CoinGiftItem` 组件与 Popup 引用，把购买按钮/数量/价格刷新恢复到 `CoinGiftPopup`，并将 `ShopItem` 恢复原本的本地按钮生命周期即可回滚抽象；配置 `discount` 字段和购买存档无需回滚。

## 2026-09-03 首购 24 小时自然日倒计时

### 需求与原行为基线

- 首购商品在尚未购买时需要显示 `HH:mm:ss` 倒计时；每个设备当地自然日结束后，如果仍未购买，倒计时自动进入下一日周期。
- 原实现已经按 `DateTime.Now` 计算到当地次日 0 点的剩余时间，但 `CoinGiftPopup` 直接操作 Generated 文本字段，未归入首购 Item 的 Inspector 展示边界。

### 当前行为与边界

- `CoinGiftItem` 新增可选的 `countdownText` Inspector 字段；只在 `firstPurchase_state` 绑定现有 `ui_libao1_04_txt`，常规档保持空引用。
- `CoinGiftPopup` 在打开时立即刷新，首购可见期间每秒按 `now.Date.AddDays(1) - now` 更新剩余时间。
- 未购买时，当设备当地日期跨过 0 点，下一次刷新会自动重新显示接近 `24:00:00` 的新一日倒计时；关闭弹窗或重启游戏不会改变自然日锚点，因此没有新增存档字段。
- 首购购买成功后当前弹窗保持打开，并立即依据 `PurchaseCache` 切换到常规档；首购倒计时不再显示。
- 倒计时文本通过 Prefab Inspector 直接绑定，运行时不使用节点名、`Transform.Find` 或层级遍历。

### 验证、风险与回滚

- 静态回读确认首购 `CoinGiftItem.countdownText` 指向 `ui_libao1_04_txt` 的 TMP 组件，常规 `CoinGiftItem.countdownText` 为 null。
- 执行 `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies`，结果为 `0 error / 122 warning`；警告来自工程既有代码。
- Unity PlayMode 的逐秒显示、跨设备当地 0 点、手动修改系统时间、夏令时切换以及 Android/iOS 后台恢复仍为 `uncertain`。
- 回滚时移除 `CoinGiftItem.countdownText` 和 `SetCountdown()`，并把文本刷新恢复到 `CoinGiftPopup.UiLibao104Txt`；不涉及配置、购买记录或存档迁移。

## 2026-09-03 主线与 Draw 金币不足入口接入

- `GameTipsPopup` 的获取提示/直接通关、`DrawGame_GameTipsPopup` 的获取提示/直接通关，共四个金币不足分支改为调用 `CoinGiftPopup.Create()`。
- CoinGift 只作为上层补充金币窗口：不关闭底层提示弹窗、不恢复玩法、不自动重试提示消费。关闭或从商店返回后，玩家仍在原提示意图中。
- 创建 CoinGift 失败时，发起弹窗会记录明确错误并回退显示原金币不足提示；不扣币、不发提示、不通关。
- 运行时程序集首次构建为 `0 error / 122 warning`，最终复验为 `0 error / 0 warning`。Unity PlayMode、真实广告/IAP、商店返回和 Android/iOS 原生覆盖层仍为 `uncertain`。
- 回滚只需恢复四个发起分支，CoinGift 自身、GM 入口、配置和购买存档无需变更。

## 2026-09-03 奖励动画与关闭边界修正

### 需求与原行为基线

- 用户确认广告和购买成功都只能发奖并播放金币飞行动画，CoinGiftPopup 不得因成功回调主动关闭。
- 原实现的广告成功回调直接调用 `GiftManager.Close(this)`；购买成功回调同样关闭 CoinGift，随后由 Controller 创建 `CommonGetRewardPopup` 展示奖励，与确认后的交互边界冲突。
- “Check the shop” 按钮保持唯一的商店跳转路径：先关闭 CoinGift，再创建 `ShopPanel`。

### 当前行为、资源与 UI 流

- `CoinGiftPopup` 通过 Inspector 序列化引用 Prefab 内现有 `CommonUIRewardAnimCtrl`，不在运行时按节点名搜索组件。
- 广告成功从广告按钮位置播放飞金币；购买成功从当前 `CoinGiftItem.PurchaseButton` 位置播放飞金币。动画数量沿用 ShopPanel 的金币档位规则，并暴露为 Inspector 参数 `8/10/12/14/16`。
- Prefab 新增无视觉节点 `RewardFlyTarget`，作为 `CommonUIRewardAnimCtrl.endPoint` 的 Inspector 目标；动画控制器和终点引用均已写入 Prefab 序列化数据。
- 动画期间广告、购买、商店和关闭按钮暂时锁定，避免重复请求、叠加动画或中途销毁表现；动画结束后恢复交互。
- 广告、首购、常规购买成功都不关闭 CoinGift。首购成功后当前界面立即刷新为常规档；普通购买成功后保留当前常规档。
- `CoinGiftController` 不再创建 `CommonGetRewardPopup`，因此奖励只由 Controller 入包一次，当前弹窗只负责动画表现。
- 点击 “Check the shop” 仍执行 `GiftManager.Close(this)` 后调用 `ShopPanel.Create(false, nameof(CoinGiftPopup))`；独立关闭按钮仍只关闭 CoinGift。

### 验证、剩余风险与回滚

- 静态回读确认广告/购买成功分支已无主动关闭，Controller 已无 `CommonGetRewardPopup.Create`；仅商店按钮和独立关闭按钮保留 `GiftManager.Close(this)`。
- Prefab 静态回读确认 `rewardAnimCtrl`、`CommonUIRewardAnimCtrl.endPoint` 与 `RewardFlyTarget` 均为非空引用。
- 执行 `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies`，结果为 `0 error / 122 warning`；警告来自工程既有代码，本次 CoinGift 文件未产生编译错误。
- Unity Editor 日志确认修改后的 `CoinGiftPopup.cs` 已完成 Hot Reload，`CoinGiftPopup.prefab` 已由 PrefabImporter 成功导入，未记录该资源的 YAML 或 Prefab 导入错误。
- PlayMode 成功回调、真实广告/IAP、飞行动画终点视觉以及 Android/iOS 原生覆盖层回调仍为 `uncertain`。
- 回滚时恢复两个成功回调的关闭行为与 Controller 的 `CommonGetRewardPopup` 展示，并移除 CoinGiftPopup 的动画字段、Prefab 控制器引用和 `RewardFlyTarget`；奖励配置、购买记录和 Bag 数据无需迁移。

## 2026-09-03 Shop/Product 公共解析与购买类型修正

### 需求与原行为基线

- Shop、CoinGift、ChestGift 都需要执行同一种 `Shop.Id -> TbProducts.RewardId` 反查，但原实现分散在各模块并使用首条匹配，无法区分缺失和重复配置。
- CoinGift 原校验把首购限购错误地绑定到 `Possessive`，导致当前 `chest_pack_1=Consumeable` 即使 `BuyTimes=1` 仍被禁售。

### 当前行为与数据流

- `PurchaseManager.TryResolveUniqueProduct()` 作为公共只读入口统一反查 Product；无映射、配置表不可用或同一 RewardId 多条映射都会返回具体错误。
- ShopPanel、CoinGiftController、ChestGiftController 全部复用该入口，不再各自扫描 `TbProducts`。
- `PurchaseManager.TryGetProductId()` 优先使用 Guru 生成映射；生成映射缺失时，从唯一的 `TbProducts.Id` 配置构造平台 ProductId，因此新增 `chest_pack_1` 不再被旧 Guru 列表直接拦截。
- `ProductType` 只转换平台交易属性：`Consumeable -> Consumable`、`Possessive -> Asset`、`Subscriptions -> Subscriptions`。限购统一由 `Shop.BuyTimes + PurchaseCache` 判断。
- CoinGift 的平台成功回调额外核对回调商品名与请求快照一致；不一致时不记录购买、不发奖。

### 验证、风险与回滚

- 静态扫描确认 GiftManager 相关模块不存在旧的 ProductType 限购断言，也不存在分散的 RewardId 首条反查。
- 执行 `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies`，结果为 `0 error / 122 warning`；warning 属于当前工程既有范围。
- 本条描述的是修改前状态；最新配置已改为 `chest_pack_1～5 -> 20008～20012` 且均为 `Possessive`，同时 `20013` 失去 Product 映射。CoinGift 首购需补回独立映射后才能购买。

- Unity PlayMode、真实商店商品详情、支付成功/失败/重复回调以及 Android/iOS 原生覆盖层恢复仍为 `uncertain`。
- 回滚时恢复三个模块各自的 Product 反查与生成映射限定，并移除 `TryResolveUniqueProduct/TryGetProductId` 配置兜底；这会重新使新增但未写入 Guru 生成列表的商品不可购买。

## 2026-09-03 购买成功提示与常规档划线原价补齐

### 需求、原行为与当前行为

- 原行为在 CoinGift 购买成功时调用 `PurchaseManager.OnPurchaseSuccess(..., false)`，金币确认入包后没有补充成功提示和音效；常规档虽然绑定了 `originalPriceText` 且配置了非零折扣，但刷新时固定传入空原价，因此节点被主动隐藏。
- 当前保持通用购买提示关闭，只有在 `PurchaseCache` 已更新且配置金币确认完整入包后，才显示 `U_HurryTips_Shop_succeed` 并播放 `shopGetReward`，避免奖励失败时提前提示成功。
- 首购与常规档现在共用当前 Offer 的本地化原价反算结果；常规档按自身 `Shop.Discount` 计算并展示划线原价。平台元数据或格式解析失败时仍安全隐藏原价，不影响现价、折扣、购买或奖励交付。
- 弹窗成功后保持打开、金币飞行动画、首购切换常规档、购买记录、配置和 Inspector 绑定均未改变。

### 验证、风险与回滚

- 静态回读确认成功提示位于金币数量增量验证之后；常规档不再传入空原价，而是与首购共用 `TryResolveOriginalPrice()` 的结果。
- 执行 `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies`，结果为 `0 error / 122 warning`；warning 属于当前工程既有范围。
- Unity PlayMode、真实 IAP 成功提示层级、各地区货币格式和 Android/iOS 横屏视觉仍为 `uncertain`。
- 回滚时删除 CoinGift 成功后的 `SystemTips/AudioManager` 调用，并把常规档 `Refresh()` 的原价参数恢复为空字符串即可；不涉及 Prefab、配置或存档迁移。

## 2026-09-03 CoinGift 奖励与折扣改为配置驱动

### 需求、原行为与当前行为

- 原实现把广告奖励 60、首购折扣 60、常规折扣 25 声明为 Controller 公共常量，并把它们作为商品可用性门禁；配置调整后会导致合法商品被禁用，广告动画数量也与代码常量绑定。
- 当前广告奖励读取 Inspector `rewardAdShopId` 对应 Shop 的正数 Coin 配置；Controller 将本次实际交付数量回传给 Popup，金币飞行动画使用同一数量。
- 首购与常规折扣直接读取各自 `Shop.Discount`。0 隐藏折扣节点，非 0 按 `{0} Off` 显示；折扣值不再决定商品是否可购买。
- Resources 名称和单 Prefab 档位仍是 CoinGift 内部路由结构值，已收窄为私有常量，不再作为公共业务配置暴露。

### 数据流、验证、风险与回滚

- 数据流为 `CoinGiftPopup Inspector ShopId -> TbShop.Rewards/Discount -> Controller 校验与交付 -> Popup 数量、折扣、原价和动画展示`；未新增配置、存档、事件或运行时节点搜索。
- 静态扫描确认 `RewardAdCoinCount`、`FirstPurchaseDiscount`、`NormalPurchaseDiscount` 及精确值门禁均已移除。
- 执行 `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies`，结果为 `0 error / 122 warning`；warning 属于当前工程既有范围。
- Unity PlayMode、真实广告、配置热重载期间回调、真实平台价格与 Android/iOS 横屏视觉仍为 `uncertain`。
- 回滚时恢复三个常量和精确值校验，并把广告回调恢复为只返回布尔值；Prefab、Shop 配置与玩家存档无需迁移。

## 2026-09-04 CoinGift 首购倒计时配置接入

- 修改前 CoinGift 首购倒计时由 `CoinGiftPopup` 固定计算到设备当地次日 `00:00`；现在改由 `GiftManager.GetDailyCountdown(CoinGift)` 读取 `TbGlobalConst.DailyFirstPurchastGiftDuration`，单位为秒，小于等于 `0` 或配置尚未就绪时回退为 `86400` 秒。
- 倒计时仍以设备当地当天 `00:00` 为起点；当前配置为 `86400`，所以现有玩家可见刷新时间不变。该配置只影响首购状态倒计时显示，不改变 CoinGift 广告、普通礼包、购买、奖励动画、购买记录、存档、事件或平台 SDK。
- 运行时工程编译结果为 `0 error / 122 warning`。Duration 为 `0`、非 24 小时值、跨午夜与 Android/iOS 本地时间显示尚未完成 PlayMode/真机验证，状态为 `uncertain`；回滚时恢复 `CoinGiftPopup.RefreshCountdown()` 的当地次日计算即可。

## 2026-09-08 折扣百分号与换行统一

- 需求与基线：按本次用户要求，所有 discount 展示统一为首行 `{num}%`、次行小写 `off`；旧运行时文本为 `{num} Off`，会覆盖 CoinGift Prefab 已有的百分号和换行。此前文档中的旧格式描述为历史记录，当前格式以本节为准。
- 影响与实现：仅修改 `Assets/Game/UI/ShopPanel/Scripts/ShopItemBase.cs` 的格式字符串为 `"{0}%\noff"`，并同步原格式相关 Tooltip 与方法注释，避免注释与新实现冲突。覆盖 `CoinGiftItem` 首购/常规项和普通商店 `ShopItem`；零折扣仍隐藏根节点并清空文本。
- 复用与流程：`CoinGiftPopup.RefreshPurchaseOffer -> CoinGiftItem.Refresh -> SetDiscount` 与 `ShopItem.Refresh -> SetDiscount` 共用已有展示入口。沿用 Inspector 折扣绑定，不新增组件、事件、运行时查找、配置或存档；购买、奖励、原价反算和 UI 生命周期没有修改。
- 验证证据：静态检索确认运行时 discountText 赋值收口在该方法；CoinGift 两个、ShopPanel 四个非空 discountText 引用均能解析到 Prefab 组件。`git diff --check` 通过。
- 编译与缺口：执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:quiet /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false`，因 `Temp/bin/Debug/Apple.Core.dll` 等依赖缺失报 200 个 CS0006，未取得编译通过证据。CodeGraph CLI 不可用，规则引用的 `../specs/037-remote-resource-loading/plan.md` 不存在，采用本地脚本与 Prefab 证据。
- 未验证风险：Unity PlayMode、字体百分号字形、两行文本边界及 Android/iOS 横屏实际显示均为 `uncertain`；需打开 CoinGift 首购/常规和普通商店确认无遮挡、无裁切。
- 回滚路径：恢复 ShopItemBase 的旧格式字符串及对应说明即可，无需迁移资源或存档。

## 2026-09-14 金币礼包购买入口断网校验

- CoinGift 的购买按钮此前直接进入 `CoinGiftController.TryPurchase()`；无网且 SDK 无结果回调时，控制器忙碌状态会使购买与关闭按钮持续不可用。
- 当前在调用控制器之前检查设备无网，直接复用 `PurchaseManager.OnPurchaseFailed()` 显示网络提示；广告入口、金币交付、首购切换、动画和购买结果处理不变。
- 静态顺序与差异检查通过；iOS/Android 真机、已连 Wi-Fi 但商店不可达及点击后切网仍为 `uncertain`。回滚时删除 `CoinGiftPopup.OnPurchaseClick()` 的断网分支即可。
