# PurchaseManager 恢复购买功能记录

## 2026-09-03 Possessive 商品恢复规则

### 需求与原行为基线

- ChestGift 的 `chest_pack_1～5` 已在 `TbProducts` 中配置为 `Possessive`，表示这些商品可以进入平台 Restore Purchase 资产恢复链。
- 原 `OnRestored()` 只通过 Guru 生成的 `Products.GetProductName()` 反查商品；新 ChestGift 尚未进入该生成表时无法反查。
- 原 `SyncRestore()` 调用普通购买成功路径。该路径只复制 `IntParam`，不会恢复 `StringParam` Skin；Coin 虽不会由公共路径发放，但缺少明确的 Restore 奖励边界。

### 当前行为

- 平台恢复结果只接受 `TbProducts.Type == Possessive` 的唯一商品；`Consumeable` 与 `Subscriptions` 不进入本次永久资产补发。
- ProductId 反查读取当前 Luban `TbProducts`，因此 Guru 生成列表尚未包含 ChestGift 时仍可识别；同一平台 ProductId 对应多条 Possessive 配置时拒绝恢复并记录错误。
- Restore 遍历对应 `TbShop.Rewards` 时明确跳过 `Coin`，不会补发礼包中的金币。
- `Skin` 必须是有效 `StringParam` Skin ID，随后通过 `SimpleBag.GetNewSkin(..., Purchase, false)` 幂等写入拥有列表。
- 既有 `NoAds / UnlimitTips` 永久权益继续恢复，但只补足到配置数量，不重复累加。
- 存在至少一项可恢复永久权益后，将 `PurchaseCache` 至少记录为 1，并统一调用 `DB.SaveGameData()`；随后沿用 `OnRestoreSuccessEvent` 刷新 ShopPanel。
- `InitConfig()` 发生在 Bag 创建之前，因此它只重新解析早到的 AssetStore；实际补发仍由 `OnRestored()` 或 Loading 在 `ReadyToPlay()` 后调用 `SyncRestore()`。
- `Purchase()` 会记录尚未完成的 productName；如果平台在正常购买回调前先推送 AssetStore，`SyncRestore()` 会跳过该商品，避免提前写入 `PurchaseCache` 导致礼包购买回调误判重复并漏发 Coin。

### 影响范围与数据流

- 修改文件：`Assets/Game/Scripts/SDK/Services/Purchase/PurchaseManager.cs`。
- 配置链：`AssetStore ProductId -> TbProducts(Possessive) -> RewardId -> TbShop.Rewards`。
- 发放链：`Coin -> 跳过`；`Skin -> OwnedSkin`；`NoAds / UnlimitTips -> GameTools`；成功后更新 `PurchaseCache` 并保存 `GameData`。
- 正常购买的奖励顺序、CoinGift/ChestGift 首次发奖、价格、广告、Prefab、Daily Schedule 和平台 Restore 请求入口均未修改；仅增加正常购买与 Restore 资产回调之间的竞态隔离。

### 验证

- 当前 JSON 静态矩阵确认 `chest_pack_1～5` 分别映射 `20008～20012`，类型均为 `Possessive`，奖励均包含 Coin 与 Skin；Skin ID `203014～203018` 均能在 `tbskin.json` 唯一找到。
- 执行 `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies`，结果为 `0 error / 122 warning`；warning 属于工程既有范围。

### 剩余风险与未验证项

- Unity PlayMode、iOS 沙盒 Restore、Android 资产回调、应用重装后的 Skin 补发、重复 Restore 幂等性及原生覆盖层时序仍为 `uncertain`。
- 当前 `chest_pack_1～4` 的平台 SKU 与既有 Coin Pack SKU 重复但 ProductType 不同；平台后台是否已将这些 SKU 改为永久商品尚未验证。同一商店 SKU 通常不能同时表达 Consumable 与 Asset，发布前必须核对 Google Play / App Store 商品定义。
- 当前 `CoinGiftPopup` 首购仍指向 Shop `20013`，但最新 `TbProducts` 已没有 `RewardId=20013` 的 Product；该首购档会保持禁用，需另行补充唯一商品映射。

### 回滚路径

- 删除 `ResolveRestoredProductNames()` 的 Luban Possessive 反查并恢复 Guru `Products.GetProductName()`。
- 将 `SyncRestore()` 恢复为调用 `OnPurchaseSuccess(productName, false)`，并删除 `TryRestorePurchase()`；这会重新导致 ChestGift Skin 无法恢复。


## 2026-09-07 guru_spec 内购信息补齐

- 需求与依据：按用户提供的 `C:/Users/admin/Desktop/IAP及Subscription _ My Site.html`（26.4.0，IAP 配置）补齐 `Assets/Guru/guru_spec.yaml`。文档提供字段格式，实际商品名称、SKU 与类型取当前 `Assets/Game/Configs/tbproducts.json`，未执行附件中的后台操作步骤。
- 原行为基线：YAML 仅有 7 个商品，当前 JSON 有 13 个；本次新增 `gift_pack_1～5` 和 `first_pack`，保留原有 7 个商品及其余配置。任务开始时 YAML、Products/Shop Excel、商品 JSON 等已有工作区状态，未覆盖这些已有内容。
- 新增配置：五个礼包使用 `attr: asset`，首充包使用 `attr: consumable`；均为 `method: iap`。Android/iOS SKU 分别沿用 JSON 的 `brpk.a.iap.giftpack1～5` / `brpk.i.iap.giftpack1～5` 及 `brpk.a.iap.firstpack` / `brpk.i.iap.firstpack`。本次 manifest 分类选择 `gift_pack`（礼包）和 `coin`（首充包），该分类是本次配置选择，并非附件指定的礼包分类。
- 兼容性事实：`Packages/com.guru.sdk.framework.guruspec/Editor/GuruSpecGenerator/Code/GuruSpecDoc.cs` 的 `GetTransactionAttribute()` 将 `asset` 与 `possessive` 均映射为 `TransactionAttribute.Asset`。原有商品不需要为拼写统一而迁移；没有新增订阅商品。
- 数据流与边界：`guru_spec.yaml -> Guru Spec 生成器 -> GuruAllSpecs.G.cs -> Products / ProductProfile`。本次仅补 YAML，未修改购买、恢复、奖励、存档、UI、事件或平台调用实现。历史记录中的 `chest_pack_*` 名称与 SKU 冲突描述属于当时快照，当前 JSON 已使用 `gift_pack_*`，13 个商品各平台 SKU 均唯一。
- 验证：Python/PyYAML 解析成功；13 个商品名称集合、双平台 SKU、类型与当前 JSON 一致；双平台 SKU 各自唯一；原有 7 个商品及所有非 products 节点与修改前完全一致。
- 未验证：当前没有可调用的 Unity 编辑器执行工具，本次未运行 Guru 菜单生成，现有 `Assets/Guru/Gen/GuruAllSpecs.G.cs` 仍只有旧商品。需在 Unity 执行 `Guru/Guru Spec/Auto-Gen <GuruAllSpecs.G.cs>` 后确认生成列表含 13 个商品，再验证编译、Android/iOS 商品查询、沙盒购买和恢复。平台后台 SKU/类型、Tangle 配置与 manifest 分类的统计口径均未验证，状态为 `uncertain`。
- 环境缺口：规则引用的 `../specs/037-remote-resource-loading/plan.md` 不存在；CodeGraph CLI 不可用且没有相应 MCP 工具，类型和生成入口改用直接源码读取核对；知识库没有 guru_spec 专项匹配。
- 回滚：只删除本次新增的 6 个 products 节点；保留任务开始前的工作区内容。若后续生成运行时文件，需同步重新生成。

## 2026-09-11 内购失败提示区分当前网络状态

### 需求与原行为基线

- 原 `PurchaseManager.OnPurchaseFailed()` 对所有购买失败统一显示 `U_HurryTips_Shop_failed`，没有区分当前设备是否无网络。
- 本次要求在购买失败回调执行时检测当前网络状态；确认无网络时改用 `U_NoNetWork_Tips_01`。

### 当前行为与影响范围

- `OnPurchaseFailed()` 读取 `UnityEngine.Application.internetReachability`；值为 `NetworkReachability.NotReachable` 时显示 `U_NoNetWork_Tips_01`，其余状态继续显示 `U_HurryTips_Shop_failed`。
- 修改文件仅为 `Assets/Game/Scripts/SDK/Services/Purchase/PurchaseManager.cs`；所有调用该公共失败入口的 Shop、NoAds、Victory、Skin 与 Gift UI 自动复用相同判断。
- 不修改 IAP 请求、原生覆盖层、回调时序、等待弹窗、成功发奖、存档、配置、恢复购买或 analytics 流程。

### 验证与剩余风险

- 已执行定向源码检查与差异空白检查，确认只替换失败提示词条选择逻辑。
- 当前 Unity Editor 日志已记录 `Hot Reloading OnPurchaseFailed in PurchaseManager.cs - Assembly-CSharp`，未在该次热重载后的日志片段发现新增 C# 错误；独立 `dotnet build` 因 `Temp/bin/Debug` 缺失 200 个 Unity、Guru 与 Bear 元数据 DLL 被环境阻断，未形成完整程序集编译结论。
- 当前 `Assets/Resources/I2Languages.asset` 未检索到 `U_NoNetWork_Tips_01`，本地语言资源是否会从其它生成源补入仍为 `uncertain`；若发布资源中同样缺失，I2 可能回显 Key，需要在语言表中补齐并重新生成。
- `Application.internetReachability` 只反映设备当前网络可达类型，不保证互联网或商店服务真实可用；有 Wi-Fi/蜂窝连接但商店服务不可达时仍会显示普通购买失败。
- Unity PlayMode、Android/iOS 断网购买、飞行模式和真机商店失败回调尚未验证。

### 回滚路径

- 将 `OnPurchaseFailed()` 恢复为固定显示 `U_HurryTips_Shop_failed`，即可撤销本次提示分流；无需修改购买、奖励或存档数据。

## 2026-09-11 旧 NoAds Bundle 恢复与新 Bundle 购买配置同步

### 需求与原行为基线

- 需求是保留旧玩家 `noad_bundle` 的永久权益恢复，同时让商店当前 `newnoad_bundle` 可以正常查询价格并发起购买。
- `#Products.xlsx` 与 `tbproducts.json` 已把旧商品 `noad_bundle` 保留为 `Possessive`，沿用旧 Android/iOS SKU，并改为关联仅用于恢复的 Shop `20014`；新商品 `newnoad_bundle` 关联当前 Shop `20006`。
- `#Shop.xlsx` 与 `tbshop.json` 的 `20006`、`20014` 均配置 `NoAds ×1 + Coin ×500`；ShopPanel prefab 只展示 `20006`。
- 原 `guru_spec.yaml` 与 `GuruAllSpecs.G.cs` 未包含 `newnoad_bundle`。Guru SDK 初始化和商品详情查询只遍历 `ProductProfile.IapIds`，因此 Luban 兜底虽然能构造新 ProductId，但无法获得商品详情，Shop 会显示不可购买并且不能发起交易。

### 当前行为与影响范围

- `guru_spec.yaml` 保留旧 `noad_bundle`，新增 `newnoad_bundle`，两者均使用 `possessive + iap + no_ads`，并标记 `capabilities: noAds`、`ignore_sales: true`。
- `GuruAllSpecs.G.cs` 同步加入新 ProductId、ManifestBuilder、全局商品名映射、IAP 商品集合和 NoAds 能力集合；旧商品也进入 NoAds 能力集合。
- 启动后 Guru IAP 会同时查询旧、新两个 SKU。旧平台资产仍能通过 `ProductProfile.Find -> PurchaseManager.ResolveRestoredProductNames -> Shop 20014` 恢复 NoAds；Coin 按既有恢复规则不重复补发。
- ShopPanel 的 `shopId 20006 -> newnoad_bundle -> 新 SKU` 映射唯一。购买成功后沿用公共 NoAds 发放和 ShopPanel Coin 补发，PurchaseCache 以新商品名记录一次购买。
- 未修改 Excel、Luban JSON、ShopPanel prefab、购买代码、恢复代码、存档结构、价格、奖励数量或 analytics 事件调用。

### 配置审计与验证

- `#Products.xlsx / tbproducts.json` 均为 14 个商品且 ID 集合一致；`#Shop.xlsx / tbshop.json` 均为 15 条且 ID 集合一致。商品 ID、Shop ID、Android SKU、iOS SKU 均无重复。
- 旧/新商品均为 `Possessive`；旧 SKU 为 `brpk.a.iap.noadsbundle / brpk.i.iap.noadsbundle`，新 SKU 为 `brpk.a.iap.newnoadsbundle / brpk.i.iap.newnoadsbundle`。
- 静态调用链确认 Guru SDK 的 `RefreshProducts()` 只查询 `ProductProfile.IapIds`，`BuildProducts()` 只接受已加载详情；补齐后新商品已进入生成的 `oneOffChargeIapIds` 与 `_productMap`。
- YAML 解析确认 `products` 共 14 项，旧/新礼包字段均为 `possessive + noAds + iap + no_ads + ignore_sales`；生成文件定向检查确认两者均在 IAP 集合、NoAds 能力集合、ManifestBuilder 和商品名映射中。
- 当前 Unity Editor 已记录 `Hot Reloading 6 methods in GuruAllSpecs.G.cs - Guru.SDK.Gen`，该热重载后的日志片段未发现新增 C# 编译错误；`git diff --check` 通过。
- Shop `20006` 和 `20014` 的 `goodsName/goodsDes` 当前仍引用无限灯泡词条，与 NoAds + 500 Coin 奖励语义不一致。当前 ShopItem 只从该配置读取折扣与 Coin 数量，标题/描述由 prefab 美术承担，因此未阻断本次购买；没有现成的 NoAds Bundle 专用多语言词条，本次不擅自改文案 Key。

### 剩余风险与未验证项

- Google Play Console / App Store Connect 是否已经创建并启用新 SKU、旧 SKU 是否仍可返回历史资产，当前本地工作区无法验证，状态为 `uncertain`。
- 尚未执行 Android/iOS 沙盒真机的旧账号恢复、新账号购买、价格拉取、断网失败、重复恢复和广告抑制验证。
- `GuruAllSpecs.G.cs` 为生成文件；本次按当前 Guru Spec 生成器结构同步，后续必须通过 Unity 的 Guru Spec 菜单重新生成并确认 diff 为空，避免生成结果漂移。

### 回滚路径

- 删除 `guru_spec.yaml` 的 `newnoad_bundle` 和旧/新 `capabilities: noAds`，再重新生成 `GuruAllSpecs.G.cs`，即可恢复原运行时商品集合；该回滚会重新导致新 SKU 无法取价和购买。
- 不应删除旧 `noad_bundle` 的 Excel、JSON、Guru ProductProfile 或 Shop `20014` 映射，否则旧玩家平台资产将无法反查到 NoAds 恢复奖励。

## 2026-09-14 新免广告商品与旧权益恢复复核

### 需求、配置与原行为

- 当前工作区的 `#Products.xlsx` 与 `tbproducts.json` 新增 `newno_ads`，Android/iOS SKU 为 `brpk.a.iap.newnoads` / `brpk.i.iap.newnoads`，类型 `Possessive`，关联 Shop `20001`。旧 `no_ads` 保留原 SKU，改为关联 Shop `20015`；两个 Shop 均只授予 `NoAds ×1`。旧 `noad_bundle` 与当前 `newnoad_bundle` 的 SKU 和奖励映射未变。
- 原 `guru_spec.yaml` 与 `GuruAllSpecs.G.cs` 只有旧 `no_ads`，四处独立 NoAd 购买入口也继续购买旧 SKU；只更新 Luban 商品表不能让 Guru 商品详情查询新 SKU。

### 本次改动与恢复链

- `guru_spec.yaml` 增加 `newno_ads`，使用 `possessive + noAds + iap`，manifest 为 `no_ads` 且 `ignore_sales: true`；生成表同步增加 ProductId、商品名映射、ManifestBuilder、IAP 查询集合与免广告能力集合。旧 `no_ads`、`noad_bundle` 和 `newnoad_bundle` 均保留。
- NoAdsPopup、主线/Draw/IWanner 胜利面板购买改用 `newno_ads`；点击前的已拥有判断同时识别新旧独立免广告商品、新旧免广告礼包平台资产与 Bag 中的 NoAds 道具，避免旧玩家重复购买。购买回调仍按商品名读取当前 `TbProducts.RewardId` 与 `TbShop.Rewards` 发奖。
- `PurchaseManager` 恢复代码没有改动：平台资产中的旧 SKU 仍由 `TbProducts` 唯一反查为旧商品名；`no_ads -> 20015` 与 `noad_bundle -> 20014` 均恢复 NoAds。恢复逻辑跳过礼包 Coin，永久权益只补足数量，PurchaseCache 保留旧商品名；未迁移或覆盖老用户存档。
- ChestGift 五档 `gift_pack_1～5` 仍为 Possessive，Shop `20008～20012` 含 Coin 与有效 Skin ID。正常购买由 ChestGiftController 发 Coin 与全部 Skin；以后平台恢复若返回对应礼包资产，`SyncRestore()` 会按当前 Shop 配置幂等补回 Skin，跳过 Coin。平台未返回资产、商店后台商品类型与本地不一致时，无法仅凭本地代码保证恢复。

### 验证、风险与回滚

- 当前 15 条 Product 的双平台 SKU、类型与 YAML/JSON/Excel 逐项核对一致，SKU 无重复；新旧 NoAds 奖励均为 `NoAds ×1`，五档礼包的 14 个 Skin ID 均在当前 `tbskin.json` 中唯一存在。独立编译 Guru.SDK.Gen 与 Assembly-CSharp，结果均为 0 error；后者有 110 warning，未建立警告变化基线。`git diff --check` 通过。
- 未执行 Unity 菜单重新生成后的 diff 比对，也未验证 Google Play / App Store 后台 SKU 状态、旧账号恢复、新账号购买、重复恢复、iOS/Android 真机与配置打包。上述结论为静态代码和当前工作区配置结论；发布必须确认旧 SKU 仍可返回历史资产，并将用户已更新的 Excel/JSON 配置一同交付。
- 回滚本次改动时，四处独立 NoAd 入口恢复旧商品名和原已拥有判断，并从 YAML/生成表删除 `newno_ads`；不回退用户更新的 Product/Shop 配置，也不删除旧 SKU。若配置继续指向新商品，回滚后当前 NoAd 入口会再次购买旧 SKU，需先核对发行目标。

## 2026-09-14 ChestGift 恢复复核与重复提示修正

### 需求、影响范围和原行为

- 用户反馈 ChestGift 已购皮肤未恢复，且每次重启都会出现恢复购买成功提示。影响 `PurchaseManager.cs` 的平台资产同步、存档奖励与提示，以及 `guru_spec.yaml` 中五档礼包定义；不改变购买发起、原生弹窗、礼包正常购买交付或 Coin 数量。
- 原 `SyncRestore()` 把每个配置有效的已拥有永久商品都计入成功，即使 `OwnedSkin`、NoAds、UnlimitTips 和 `PurchaseCache` 均没有变化。Loading 启动调用以及 SDK `OnRestored` 均可触发它，故同一平台资产反复出现时会重复弹提示。
- 五档礼包在当前 Luban Product 表均为 `Possessive`，Guru YAML 原为 `attr: asset`。GuruSpecDoc.GetTransactionAttribute() 将 `asset` 和 `possessive` 都映射为 `TransactionAttribute.Asset`，所以此字段命名不一致，但不能把皮肤未恢复直接归因于该差异。

### 当前行为和数据流

- `guru_spec.yaml` 的 `gift_pack_1～5` 统一改为 `attr: possessive`。生成文件中对应 `TransactionAttribute.Asset` 不需变化；双平台 SKU、manifest、Shop 奖励和 Coin/Skin 映射不变。
- `SyncRestore()` 仍只消费平台 AssetStore 已解析的商品。`TryRestorePurchase()` 记录 Skin、永久道具或 PurchaseCache 是否真的新增；只有实际新增时才保存、发 `OnRestoreSuccessEvent` 并显示恢复提示。已拥有权益不再在每次启动时弹成功提示。Coin 继续跳过。
- iOS 的 Guru SDK 不在初始化时自动调用平台 `RestorePurchases()`；商店恢复按钮才显式调用。SDK 本地 `ReloadOrders()` 可在启动时把本地交易写入 AssetStore。跨安装且本地没有交易时，必须依赖 iOS 平台恢复返回对应 `giftpack` SKU，且 SDK `ProcessRestored()` 能取得商品详情并写入 AssetStore；这些设备端事实当前均为 `uncertain`。
- 当前 `PurchaseManager` 已有 `OnRestored map success/failed`、`saved productNames`、`SyncRestore apply reward` 和 Skin 校验失败日志。若设备返回的恢复列表没有礼包 SKU，本地逻辑不会凭其它商品恢复礼包皮肤；若映射成功但皮肤仍不显示，需核对设备端 `OwnedSkin` 与 Skin UI 读取路径。现阶段不引入凭本地 `PurchaseCache` 推断平台恢复的逻辑。

### 验证、缺口与回滚

- 静态核对当前 `tbproducts.json` 中五档礼包均为类型 0、iOS SKU `brpk.i.iap.giftpack1～5`、Shop `20008～20012`，奖励包含有效 Skin ID；Guru 生成器的两个 attr 别名都映射 Asset。Unity Roslyn 使用现有 Assembly-CSharp.rsp 编译为 0 error / 110 warning，`git diff --check` 通过。
- 未取得问题设备的 `OnRestore` / `OnRestored` SKU 日志，也未执行 iOS 真机恢复、重启和重装验证。皮肤未恢复的实际断点仍为 `uncertain`；不能把 YAML 文字修正或静态编译当作恢复成功证据。
- 回滚时把五档 YAML attr 改回 `asset`，并将 `SyncRestore` 恢复为按可处理商品计数、每次保存和提示；后者会重现重复提示问题，不建议在无回归证据时单独回退。

## 2026-09-14 Android 自动同步与 iOS 商店手动恢复边界

### 需求与原行为

- 用户明确要求 Android 可自动触发 `SyncRestore()`，iOS 不在 `LoadManager` 自动触发，而应由 ShopPanel 的恢复购买按钮手动触发。
- 原 `LoadManager.OnPostProcess()` 和 `PurchaseManager.OnRestored()` 都不区分平台调用 `SyncRestore()`；iOS 即使未点商店恢复按钮，启动时 SDK 重载本地交易也可能自动补发并弹提示。
- SDK 的 iOS `RestorePurchases()` 返回与 `ProcessRestored()` 写入 AssetStore 不构成同步完成边界：V5 先调用 `OnRestore`，随后异步写库并广播资产；V4 在通知 `OnRestore` 前即可结束 SDK 等待。因此 ShopPanel 不能在 SDK 返回后立即读取旧资产列表。

### 当前行为和影响范围

- `LoadManager.cs` 的 Bag 就绪后 `SyncRestore()` 仅在 `UNITY_ANDROID` 编译；`PurchaseManager.OnRestored()` 仍在所有平台缓存并解析 AssetStore，但只在 Android 自动同步。
- `PurchaseManager.Restore()` 在 iOS 等待本次 `OnRestored` 资产更新通知，限时 10 秒；SDK 返回失败或等不到资产更新时返回 false。ShopPanel 的手动按钮在 `Restore()` 返回 true 后调用 `SyncRestore()`，并沿用原 `finally` 关闭等待弹窗、刷新商店。普通购买回调与 ChestGift 正常交付不变。
- Android 保留启动加载和资产变化两条幂等同步入口。iOS 启动可缓存本地资产，但不会因此自动同步永久权益；玩家需要在 ShopPanel 手动恢复。恢复成功后的本地 Skin/NoAds 等仍沿用 `TryRestorePurchase()` 幂等补发，Coin 继续跳过。

### 验证、缺口与回滚

- 静态核对三处调用入口及 Guru V4/V5 回调时序；Unity Roslyn 使用当前 Editor 响应文件编译运行时程序集成功，0 error。`git diff --check` 通过。
- 在 Windows Editor 响应文件上附加 `UNITY_IOS` 的整程序集模拟编译被既有 `ChoiceLevelPanel.cs`、`StartPanel.cs` 对 `Guru.UniKit.Apple` 的平台专用引用阻断，不能据此断定本次 iOS 分支通过或失败。iOS 真机恢复、多次点击、无可恢复订单、10 秒后才广播资产及关闭等待弹窗仍未验证；这些情况为 `uncertain`。
- 回滚时移除 `LoadManager` 与 `OnRestored` 的 Android 平台门禁，并让 ShopPanel 恢复为仅等待 `Purchase.Restore()`；这会重新允许 iOS 在启动时自动同步。

## 2026-09-14 GiftPack 恢复与 ChestGift 档位联动（需求与实现前现状）

- 用户要求 iOS 在 ShopPanel 恢复、Android 自动恢复 `gift_pack_N` 时，同步将对应 ChestGift `giftIndex=N` 视为已购并跳过。当前 `SyncRestore()` 仅在平台资产成功反查为 `gift_pack_N`、Shop Skin 奖励校验通过后，才把商品名补入 `PurchaseCache`；ChestGift 选档已读取同一缓存并按 `BuyTimes=1` 跳过已购档。恢复过程继续只补 Skin、不补 Coin，不推进 ChestGift Daily 轮换计数或当日购买标记。
- 实现前缺少恢复后的即时入口刷新：`OnRestoreSuccessEvent` 的 StartPanel 监听只刷新 NoAds。若首页入口在恢复前已显示，其图标/显隐可能暂时落后于最新 `PurchaseCache`，但下一次普通 `GiftManager.Open()` 会重新选档。

### 本次联动、验证与边界

- StartPanel 的现有恢复成功监听现在额外调用 `DailyChestGiftEnter.Refresh()`；两平台共用 `SyncRestore()` 的一次实际权益变更事件。Android 启动阶段若事件早于 StartPanel 订阅，主页 `OnOpen()` 原有刷新会读取已更新的 `PurchaseCache`。全档已购时入口由现有 `IsEntryAvailable()` 返回 false 而隐藏；未改 SDK、购买、发奖、配置、Daily 或存档结构。
- 静态核对五档商品与 Shop 一一对应，`BuyTimes=1`，主页 Prefab 的入口引用非空；`git diff --check` 通过。整程序集编译受本机过期的 Unity 项目引用与 Bee 响应文件阻断，Android/iOS 真机恢复、即时图标、全档隐藏和持久性仍为 `uncertain`。本次联动不能修复平台未返回礼包资产的原始皮肤漏恢复问题；回滚仅需移除 StartPanel 事件回调中的入口刷新。

## 2026-09-15 GiftPack iOS SKU 补零同步

### 需求、来源与原行为

- 用户已调整 `Luban/Configs/Datas/#Products.xlsx` 的商品 ID，并要求同步 `Assets/Guru/guru_spec.yaml`。当前工作区 Excel 与 Git `HEAD` 的逐单元格比较只发现 `D13:D17` 五处变化：`gift_pack_1～5` 的 iOS SKU 从 `brpk.i.iap.giftpack1～5` 改为 `brpk.i.iap.giftpack01～05`。
- 修改前，Guru YAML、`Assets/Guru/Gen/GuruAllSpecs.G.cs` 与 `Assets/Game/Configs/tbproducts.json` 仍使用未补零的旧 iOS SKU；Android SKU、商品内部名、`Possessive` 类型和 Shop `20008～20012` 映射均未变化。

### 本次配置与数据流

- 仅把 `guru_spec.yaml` 中 `gift_pack_1～5.ios` 更新为 `brpk.i.iap.giftpack01～05`，保留 Android SKU、`attr: possessive`、`method: iap` 和 `manifest.category: gift_pack`。
- 配置生效链仍为 `guru_spec.yaml -> Guru Spec 生成器 -> GuruAllSpecs.G.cs -> ProductProfile.IapIds -> PurchaseManager`。本次按用户点名范围只修改 YAML，没有手工改写生成文件，也没有运行 Luban 或 Guru 生成菜单。
- 因此当前 YAML 已与 Excel 的五个新 iOS SKU 对齐，但 `GuruAllSpecs.G.cs` 和 `tbproducts.json` 仍是旧值；在重新生成前，现有运行时代码仍可能查询旧 SKU，不能把本次源配置修改表述为真机已生效。

### 验证、风险与回滚

- 已执行 YAML 解析和 Excel/YAML 定向映射核对；五个目标商品的新 iOS SKU 一一对应，Android SKU保持不变，当前平台 SKU 集合无重复；`git diff --check` 通过。
- 未运行 Unity Guru Spec 自动生成、Luban JSON 生成、程序集编译、iOS 商品详情查询、购买、恢复或 App Store Connect 后台核对，上述运行时和平台结果均为 `uncertain`。
- 后续应先生成 `tbproducts.json`，再在 Unity 执行 `Guru/Guru Spec/Auto-Gen <GuruAllSpecs.G.cs>`，确认两份生成产物与 Excel/YAML 一致，并进行 iOS 沙盒验证。
- 回滚只需把五个 YAML iOS SKU 恢复为未补零的 `giftpack1～5`；若之后已经生成代码或 JSON，必须同步重新生成，避免源配置与运行产物漂移。
