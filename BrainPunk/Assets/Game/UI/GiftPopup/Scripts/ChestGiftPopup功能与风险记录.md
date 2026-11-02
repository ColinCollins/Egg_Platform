# ChestGiftPopup 功能与风险记录

## 2026-09-09 Enter 按档位强制映射原 icon

- 用户明确要求在 Enter 创建 Dictionary 强制对应颜色，同时保留原 icon 与原结构；并进一步纠正 Dictionary 应直接对应档位和原 GameObject，不应绕到资源名与数组索引。
- 根因已由本地配置确认：`tbshop.json` 中五档 `ChestPrefab` 的实际顺序是 `green、blue、purple、orange、red`，而 `chestIconsByVariant` 的 Inspector 顺序是 `red、blue、green、purple、orange`。旧实现直接使用 `giftIndex - 1` 访问数组，除蓝色外会显示错误色系。
- 本次只修改 `DailyChestGiftEnter.cs`。新增运行时 `Dictionary<int, GameObject>`，将当前五档直接强制对应为 `1→绿、2→蓝、3→紫、4→橙、5→红`。入口继续使用既有 `TryResolveCurrentIndex()`，再直接激活 Dictionary 对应的原 icon，不再创建 Resolver 或二次解析资源名。
- `DailyChestGiftEnter.prefab`、五张原 PNG、Transform、层级和序列化数组绑定均保持恢复后的原始内容；`ChestGiftPopup.cs` 也保持恢复后的原始内容。没有重新着色、替换素材、创建分层节点或读取 Popup Image 颜色。
- 数据与 UI 边界：Dictionary 在首次刷新时从 Inspector 已绑定的原数组建立，字段为空时也会惰性初始化，避免已有 PlayMode 实例空引用。不写配置或存档，不新增事件、资源实例、逐帧查找或移动端分支。档位不在 Dictionary 中时隐藏全部 icon 并记录 Bear Logger 告警，避免继续显示错误颜色。
- 验证：静态回读确认当前 Shop 顺序为 `green、blue、purple、orange、red`，Dictionary 五个档位全部覆盖；值依次引用原数组对象 `绿[2]、蓝[1]、紫[3]、橙[4]、红[0]`。`dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -v:q` 为 0 错误 / 123 警告；目标脚本 `git diff --check` 通过。
- `uncertain`：尚未在重新创建主页实例后的 Unity PlayMode 和 Android/iOS 横屏逐档点击验证。由于脚本结构有变化，不应使用 Hot Reload 前已经存在的 `DailyChestGiftEnter` 实例作为结果依据。
- 回滚：移除档位 Dictionary 和建立方法，并把 `RefreshChestIcon()` 恢复为 `giftIndex - 1` 数组索引；Prefab、图片、配置和存档均无需回滚。

## 2026-09-09 撤回 Enter 分层替换，恢复原 icon

- 用户明确指出本轮擅自改变 Enter 图标，要求找回原 icon。此前“对”的回复不能作为替换既有美术、构图及层级的明确授权；原始需求是颜色对齐。
- 已将 `DailyChestGiftEnter.prefab`、`DailyChestGiftEnter.cs` 和 `ChestGiftPopup.cs` 精确恢复为 `b27da4f46` 之前版本。恢复前逐文件确认工作区与该提交一致，没有覆盖后续独立修改；本次不撤销 IWanner 自动翻页。
- 五张原始红、蓝、绿、紫、橙 PNG 始终保留，现在恢复其原有 Image、Transform、五档绑定与切换逻辑，移除误加的分层节点和 `TryGetEntryColors()`。
- 以下“Enter 分层图标同步弹窗配色”章节仅保留为已撤回方案的历史记录，不再代表当前实现。颜色不一致的具体原因仍为 uncertain，本轮没有继续改色或更换图片。
- 验证：三个恢复文件与上述父提交逐字节归一换行比较一致；尚未在 Unity PlayMode 验证已存在实例的刷新。旧实例需要重新创建才能加载恢复后的 Prefab。
- 当前任务边界：保留原 icon 素材、构图和 Enter 层级，只排查颜色/档位对应；没有明确授权不得再次用弹窗分层素材替换 icon。需要撤销这次恢复时，先取得用户对替换视觉的明确选择，不自动恢复已否决方案。

## 2026-09-09 Enter 分层图标同步弹窗配色

### 需求、原行为与本次范围

- 用户反馈调整 ChestGiftPopup 颜色后 Enter 图标没有同步，并确认按自动同步方向处理。
- 原入口使用红、蓝、绿、紫、橙五张已经烘焙颜色的 PNG，`RefreshChestIcon()` 仅按最终可售档位切换 GameObject，不读取实际弹窗颜色。对整张 PNG 乘色会连金币、宝石和装饰一起染色。
- 修改文件仅为 `DailyChestGiftEnter.cs`、`ChestGiftPopup.cs`、`Prefabs/DailyChestGiftEnter.prefab` 及本地记录。五个 Resources Variant、Popup 母版、原 PNG、Shop 配置、购买、奖励、存档和 Schedule 均未修改。

### 实现与颜色来源

- 入口改为一个 `ChestIcon` 分层图标，直接复用 Popup 的十片宝箱素材、一张金币素材、三片星光与箱盖/箱体两组静态 Transform；现有入口根节点、light、倒计时和 clickArea 保留。
- 分层图标采用弹窗美术构图，因此金币数量、边缘与原烘焙入口图细节有所不同；本次没有重新绘制或覆盖原始图片。图标缩放为 0.2，箱体约 162×143，保持在原 227×213 入口区域内。
- `chestIconRoot`、`chestBodyImages`、`chestLiningImages`、`chestRimImages` 均已在 Prefab 显式绑定。每组两个 Image 对应左右镜像片，无运行时名称查找或层级扫描。
- `ChestGiftPopup.TryGetEntryColors()` 只读已有生成绑定 `UILibao2Baoxiang201Imgs[0]`、`UILibao2Baoxiang301Imgs[0]`、`UILibao2Baoxiang501Imgs[0]` 的 Image.color，分别作为箱体、内衬和盖沿颜色；不触发 Popup 生命周期或业务初始化，不改写源 Image。
- `DailyChestGiftEnter.RefreshChestIcon()` 沿用 `TryResolveCurrentIndex()` 取得跳过不可售档位后的最终档位，再通过既有 `ChestGiftOfferResolver.TryGetResourceName()` 读取对应 `Shop.ChestPrefab`。`Resources.Load` 只取得已有 Prefab 资源，`GetComponent<ChestGiftPopup>()` 直接读取其配色，不 Instantiate、不打开弹窗。
- 三组颜色只写入入口六片 Image。金币、宝石、箱框及星光不参与乘色。当前左右片共享组首颜色；如未来需要左右不对称配色，应另行扩展绑定，不隐式假定已经支持。
- 刷新时机仍为入口启用、父页面刷新及原有 AvailabilityChanged 通知；保存颜色 Variant 后重新进入/刷新主页即可同步。不会逐帧加载资源或轮询颜色，不提供编辑器态实时预览回调。Prefab 静态预览保存为当前绿色配色。
- 当前没有可售档位或源配色绑定缺失时隐藏图标，后者记录告警；原按钮、资格、当地午夜倒计时、启动收回目标和通知订阅边界保留。
- 原 `chestIconsByVariant` 字段及说明随过期的五图切换实现一起替换；其原注释已经不符合新分层结构，其它已有注释未调整。

### 验证、剩余风险与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -v:q`：0 错误、123 警告。构建日志位于本机临时目录 `ChestGiftIconSync-20260909/build.log`。
- Prefab 静态检查通过：fileID 唯一、本地引用可解析、父子关系互相匹配、六个颜色目标均绑定 Image。扫描 `Assets/Game` 内其它 Prefab，没有指向被删除旧入口节点的引用；入口根节点、按钮、倒计时及其组件序列化内容未改动。
- 根据最终 Prefab 的 Transform、Sprite、颜色数据生成浏览器 Canvas 静态组合预览，已逐一目视检查红、蓝、绿、紫、橙五档，宝箱分层正常，金币和装饰不被染色。该预览只验证美术组合，不替代 Unity 渲染或 PlayMode。
- 仅存在一组图标，包含 14 个静态 Image；没有新增材质、贴图、Animator、Mask、Tween、全局事件或逐帧配色工作。相较原单张 Image 增加 UI 几何，移动端合批与具体绘制开销仍需实测。
- `uncertain`：本轮尚未进行 Unity PrefabImporter、PlayMode 和 Android/iOS 横屏实测。需要重新创建主页实例，验证五档/跳档颜色、保存 Variant 后重进主页、跨日换色、点击打开、购买后隐藏、启动关闭动画落点。新增序列化字段不能用 Hot Reload 后的旧实例作为绑定验证依据。
- 回滚：恢复本轮入口 Prefab 与 `DailyChestGiftEnter.cs` 的五图切换，移除 `ChestGiftPopup.TryGetEntryColors()`；原 PNG 保留，无配置、存档或购买数据迁移。

## 需求与本阶段范围

- 需求来源为飞书《付费相关优化》及用户 2026-09-02 的补充要求。
- 本阶段已完成礼盒弹窗母版、五个 Resources 颜色变体、`Shop.chestPrefab` 资源配置、通用礼物弹窗管理器、当地 Daily Schedule 计数和主页 `DailyChestGiftEnter` 入口。
- Shop `20008～20012` 当前分别配置 `ChestGiftPopup_red/blue/green/purple/orange`；Prefab 不再保存或决定 Shop Id。正式 SKU、Skin ID 和外观数量仍以用户配置为准。
- 启动自动弹出已纳入“更新公告 -> 旧数据转换 -> ChestGift”一次性序列；主页以外的入口矩阵和专用成功页尚未接入。ChestGift Skin 已接入通用 Possessive Restore，但启动序列与真机恢复仍未验证，不能据此报告付费优化整体完成。

## 原行为基线

- 用户提供的 `ChestGiftPopup` 脚本只有空生命周期和直接按母版名称打开的方法。
- `ChestGiftPopup.prefab` 包含五组箱体美术和购买按钮，但根节点没有 `ChestGiftPopup` 运行时组件，也没有五个可分别配置的变体。
- `DailyScheduleModule` 已使用 `TimeZoneInfo.Local` 按设备当地自然日判断刷新，但没有 ChestGift 业务计数。
- 控制器拆分前，`GiftManager` 同时持有 ChestGift 的会话状态、五档资源表、配置校验、轮换、购买日隐藏和通用弹窗互斥，新增其它礼物类型会继续扩大单类职责。

## 当前实现

### 弹窗与商品绑定

- `ChestGiftController` 按 `TbShop.DataList` 中 `ChestPrefab` 非空行的顺序建立 1～5 档映射；同一条 Shop 配置同时提供商品 Id、奖励和 Resources Prefab 名称。
- `ChestGiftPopup` 不再通过 Inspector `shopId/giftIndex` 判断商品身份；控制器打开配置指定的 Resources Prefab 后，同步注入当前 `Shop` 与档位，再刷新价格、奖励和购买状态。
- `ChestGiftPopup` 绑定 `itemContainer` 与 `ChestGiftItem itemPrefab`；母版引用 `Prefabs/ChestGiftItem.prefab`，五个 Resources Variant 通过继承母版共享该引用。
- Shop 解析成功后，弹窗按固定 `Coin -> Skin` 顺序创建奖励项。只销毁本弹窗运行时创建的 `ChestGiftItem`，不会遍历或删除 `Container` 内已有背景节点；1 个奖励居中，2 个奖励按固定间距水平居中。
- `ChestGiftItem` 的 Coin 只读取 `IntParam` 并显示固定金币图标；Skin 只读取 `StringParam.Value` 作为 Skin ID，经 `TbSkin` 与 `SkinUtils` 异步加载真实图标。Skin 图标参考 `CommonRewardItem.RefreshSkin()` 按原始尺寸等比限制在现有 icon 框内，不改 `localScale` 或角度；异步回写使用版本号防止已关闭/复用 Item 被旧请求覆盖。
- 打开时先从 Shop 配置取得 `ChestPrefab` 并加载 Resources，再按 `TbProducts.RewardId == Shop.Id` 找到商品；价格使用 `PurchaseManager.GetPrice()` 返回的平台本地化价格。
- `ChestPrefab` 为空、对应 Resources 不存在、Prefab 缺少 `ChestGiftPopup`、Shop/Products 映射无效时，该档位不可用并记录明确日志。
- `ChestGiftPopup` 与 `DailyChestGiftEnter` 的倒计时都使用 `DateTime.Now` 计算到设备当地次日 00:00 的剩余时间，分别在弹窗打开和入口显示期间每秒刷新。
- 购买按钮复用现有 `PurchaseManager.Purchase()` 与平台点击埋点；购买请求和回调由 `ChestGiftController` 持有，即使遮罩关闭 Popup，成功回调仍会记录 `PurchaseCache` 并完成奖励交付。请求中禁用重复点击，重复成功回调按购买前计数拦截。
- Coin 严格读取 `IntParam` 并通过 `SimpleBag.AddTool(..., Purchase)` 入库；Skin 严格读取 `StringParam` 并通过 `SimpleBag.GetNewSkin(..., Purchase)` 入库。全部配置奖励验证完成后才允许发起购买，全部交付成功后才通知 GiftManager。
- 母版没有独立关闭图标；弹窗通过现有 `UIManager` Popup 遮罩关闭，避免把整张弹窗根节点变成误触关闭区。

### 五档 Prefab Variant

- `Resources/ChestGiftPopup_red/blue/green/purple/orange.prefab` 均继承 `Prefabs/ChestGiftPopup.prefab`，具体资源由对应 Shop 的 `ChestPrefab` 字段指定。
- 颜色变体自身负责宝箱视觉、Transform 与节点激活状态；`ChestGiftPopup` 不再按档位修改箱体节点显隐，也不修改缩放或角度。
- 新变体 YAML 中仍可存在旧 `shopId/giftIndex` override，但对应序列化字段已移除，运行时不会读取；后续由 Unity 正常序列化保存时可清理这些遗留 override，不需要手改 prefab YAML。
- `ChestGiftPopupPrefabBuilder` 菜单调整为 `Tools/BrainPunk/Chest Gift/Bind Base Popup`，只补齐母版组件引用，不再生成、重建或覆盖 Resources 颜色变体；工具不使用 `OnValidate`、`Reset` 或运行时 DB。

### GiftPopup、GiftManager、ChestGiftController 与 Schedule

- `GiftPopup` 是礼物弹窗公共基类，只负责向 `GiftManager` 上报打开与关闭生命周期；具体商品、购买、奖励和倒计时继续由子类负责。
- `IGiftController` 定义单类礼物的类型、入口资格、档位打开和交付完成契约；新增类型必须实现独立控制器并注册，不允许未实现类型静默回退到 ChestGift。
- `ChestGiftPopup` 继承 `GiftPopup`，作为 `ChestGift` 类型继续承载五档商品逻辑。
- `GiftManager` 使用 `Dictionary<GiftPopupType, IGiftController>` 做离散类型路由，只保留活动弹窗互斥、打开/关闭生命周期、入口通知聚合和 Schedule 分发。
- `ChestGiftController` 是由 `GiftManager` 唯一持有的普通 C# 对象，不是新的全局单例或 `MonoBehaviour`；它独立持有 ChestGift 的会话资格、购买日、配置/SKU 校验、档位选择和弹窗创建逻辑。
- 五档仍保留固定的 `1～5` 轮换语义，但档位内容改为按 `TbShop.DataList` 中 `ChestPrefab` 非空行的表顺序解析；不再维护脚本内 Shop Id 或 Resources 名称数组。
- `GiftManager` 仍作为唯一 `IScheduleRefreshable` 注册到 `DailyScheduleModule`，Daily 回调再分发给实现 `IScheduleRefreshable` 的礼物控制器；当前只会推进 ChestGift 计数。
- `GiftData.chestGiftDailyRefreshCount` 保存当地自然日累计刷新次数；首次主页状态和最近购买当地日期也集中在 `GiftData`。`GameData/GameSchedule` 的历史 Gift 字段仅保留作旧存档迁移源，新业务不再写入。
- 当前档位映射为 `((刷新次数 - 1) % 5) + 1`；未产生首次 Daily 刷新记录时预览第 1 档。
- 该字段是“自然日刷新次数”，不是打开次数；同一天反复打开不会推进档位。

### DailyChestGiftEnter 与展示资格

- 用户创建的 `Prefabs/DailyChestGiftEnter.prefab` 保留原视觉、层级和按钮效果，根节点 `DailyChestGiftEnter` 组件通过 Inspector 绑定 `clickArea` 上的 `CustomButton` 与 `countdown_txt` 的 TMP 文本。
- `StartPanel` 每次打开时用包含 inactive 子节点的方式找到入口，并调用 `RefreshForHome()`；入口自身不承担自动弹窗逻辑。
- 新存档第一次成功到达主页时，`ChestGiftController` 写入 `HasReachedGiftHome`，但本启动会话继续隐藏入口。后续启动到达主页后才允许显示。
- `GiftData.dataVersion` 控制一次性迁移：旧 `GameData` 缺少 `GiftEntryStateVersion` 时按历史上已到达主页处理，不会在版本升级后被误判为新用户。
- 入口展示前从当前 Daily 档位开始循环检查五条 `ChestPrefab` 配置，要求 Resources Prefab、`ChestGiftPopup` 组件、本地 `TbShop/TbProducts`、`BuyTimes=1` 和正式平台 SKU 均有效；限购次数只由 `Shop.BuyTimes + PurchaseCache` 判断，不与平台 `ProductType` 强绑定。配置不完整的档位跳过并只记录一次明确错误。
- 已购档通过 `PurchaseCache` 永久跳过；五档全部已购或全部配置无效时入口隐藏。点击入口只调用 `GiftManager.Open(GiftPopupType.ChestGift)`；Manager 先保证跨礼物类型弹窗互斥，再路由到唯一的 `ChestGiftController`，不同入口不会形成独立档位状态。
- `ChestGiftController.Open()` 在最终创建 Prefab 前额外检查五档购买记录；五档全部达到各自 `BuyTimes` 后，即使未来入口使用指定档位重载，也会直接拒绝创建弹窗。该终止条件读取现有 `PurchaseCache`，不新增重复存档标记。
- ChestGift 购买回调确认购买计数增加且金币已交付后，记录 `LastChestGiftPurchaseLocalDay`；该当地自然日内全部 ChestGift 入口隐藏，次日仍需重新启动并到达主页后才可能展示。
- `AvailabilityChanged` 是 `GiftManager` 的局部 C# 事件，只通知 Gift 入口刷新，没有新增全局 `PlayEvents`。

## 数据、资源与事件流

```text
ScheduleSystem.CheckAllModules
  -> DailyScheduleModule（设备当地日期）
  -> GiftManager.OnScheduleRefresh（按已注册类型分发）
  -> ChestGiftController.OnScheduleRefresh
  -> GiftData.chestGiftDailyRefreshCount + 1
  -> DB.SaveGiftData

业务入口
  -> StartPanel.OnOpen
  -> DailyChestGiftEnter.RefreshForHome
  -> ChestGiftController（首次主页/购买日/配置/已购档检查）
  -> GiftManager.Open(GiftPopupType.ChestGift)
  -> Dictionary<GiftPopupType, IGiftController>
  -> ChestGiftController.Open
  -> TbShop 中 ChestPrefab 非空行（表顺序映射 1～5 档）
  -> Shop.ChestPrefab
  -> Resources/ChestGiftPopup_red～orange
  -> ChestGiftPopup.ConfigureOffer(Shop, giftIndex)
  -> TbProducts
  -> Shop.Rewards
  -> ChestGiftItem（Coin IntParam / Skin StringParam ID）
  -> SkinUtils（Skin 图标异步加载）
  -> PurchaseManager / IAP
  -> PurchaseCache + SimpleBag(Coin / Skin)
  -> GiftData.lastChestGiftPurchaseLocalDay
  -> AvailabilityChanged（入口立即隐藏）
```

- 本阶段没有新增 `PlayEvents` 或其它全局事件。
- 本阶段没有修改 `Assets/GF`、RemoteConfig、Loading 等待、提示价格读取或免广告购买结构。

## 验证证据

- 本次职责拆分后执行 `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies`：`0 warning / 0 error`；执行 `dotnet build Assembly-CSharp-Editor.csproj --no-restore --no-dependencies`：`4 warning / 0 error`，Editor 警告仍来自既有代码。
- 静态扫描确认 ChestGift 会话字段、资源定义、档位解析和商品资格检查只位于 `ChestGiftController`；`GiftManager` 中只保留控制器注册、字典路由、Schedule 分发、入口通知和活动弹窗引用。
- 本次重构没有修改 prefab、Shop/Products 配置、存档字段、奖励发放、PurchaseManager、`Assets/GF`、RemoteConfig 或 Loading；`git diff --check` 通过。
- 在合并目标工作区执行 `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies`：`0 warning / 0 error`；执行 `dotnet build Assembly-CSharp-Editor.csproj --no-restore --no-dependencies`：`4 warning / 0 error`，警告来自既有 `BuildToolV2.cs`。
- 2026-09-03 重新执行运行时与 Editor 程序集构建：运行时 `0 error / 122 warning`，Editor `0 error / 4 warning`；warning 为当前工程既有范围。
- `tbshop.json` 静态回读确认五档顺序为 `20008/red、20009/blue、20010/green、20011/purple、20012/orange`，五个 Resources 文件均存在。
- 五个颜色变体均引用母版 GUID `b5c8b99873562e74aa6cc0483a934c07`，母版包含 `ChestGiftPopup` 组件；代码扫描确认运行时不再读取 prefab `shopId/giftIndex`，也不再修改箱体节点显隐、缩放或角度。
- `DailyChestGiftEnter` 静态回读确认脚本 GUID、`clickArea/CustomButton`、`countdown_txt/TMP_Text` fileID、入口 prefab GUID 和 `StartPanel` 嵌套引用全部匹配。
- Unity Editor 日志回读未发现本次相关 `error CS`、重复 GUID 或脚本编译失败；`git diff --check` 通过。
- 五档全购门禁静态检查覆盖普通打开与指定档位打开共用的 `IGiftController.Open()`；复用 Unity 当前 Bee 响应文件执行 Roslyn 编译通过，结果为 `0 error`，输出均为工程既有 warning。
- 常规 `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies` 因当前 Unity 生成工程缺失 `Temp/bin/Debug` 下的依赖程序集而产生 `CS0006`，不能作为本次代码失败证据；五档 Shop Id 尚未配置，因此真实五档购买后的 PlayMode 行为仍为 `uncertain`。
- `ChestGiftItem.prefab` 静态回读确认脚本、icon、countText 与 coinSprite 引用完整；`ChestGiftPopup.prefab` 静态回读确认 itemContainer 指向现有 `Container`，itemPrefab 指向独立 Prefab 上的 `ChestGiftItem` 组件。
- Unity 当前 Bee 响应文件已包含 `ChestGiftItem.cs`；`CoinGiftPopup.Generated.cs` 的重复字段已消除，复用当前 Bee 响应文件执行运行时 Roslyn 编译为 `0 error`。Prefab 实际布局与 Skin 异步图标仍为 `uncertain`。
- Inspector 实际配置、首次/旧存档迁移、入口点击、IAP、PlayMode 和 Android/iOS 横屏仍为 `uncertain`。

## 已知风险与后续门禁

- 当前 Coin 与单个 `StringParam` Skin 均已纳入控制器交付校验；已拥有 Skin 由现有 `SimpleBag.GetNewSkin()` 幂等处理。多 Skin ID 列表仍不在当前 ParamBase 配置契约内。
- PurchaseCache 与 Coin/Skin 保存仍不是单事务，应用在记录购买后异常退出时缺少专用补发状态；真实支付前仍需验证崩溃恢复策略和后台回调。
- 当前 Daily 计数驱动的是“每个当地自然日推进一档”。它是本阶段用户补充要求的实现；若最终仍需早期方案中的“每次冷启动推进”，必须在继续入口接入前统一轮换口径。
- `GiftPopupType` 当前包含 `ChestGift` 与 `CoinGift`；两类分别由 `ChestGiftController` 和 `CoinGiftController` 显式实现，没有默认回退分支。
- CoinGift 未实现 `IScheduleRefreshable`，因此 GiftManager 的 Daily 分发仍只推进 ChestGift；多类型路由已完成静态与编译验证，但跨类型互斥的 PlayMode 行为仍为 `uncertain`。
- `ChestPrefab` 的档位顺序依赖 Luban Shop 表中非空行的行序；若未来插入新的非空行，会改变轮换档位，配置维护时必须保持五条礼盒记录的稳定顺序。当前没有独立 `giftIndex` 配置列。
- 当前五条 `ChestPrefab` 与 Resources 文件已对应，但正式售卖仍需验证 Products、平台 SKU、限购次数、奖励和美术对应关系。
- 当前 `tbshop.json` 的 Shop `20010/20011/20012` 金币为 `1000/2800/4000`，与付费优化已锁定需求的 `1500/4000/10000` 不一致；本次按“具体配置由用户处理”的边界没有修改表值，正式验收前必须校正。
- 当前已接入主页入口与启动自动弹出；选关、暂停、提示和胜利页入口仍未实现，不能把当前完成状态视为入口矩阵完成。
- 系统当地时间可被玩家修改；该口径适合营销展示与本地日刷，但不能作为防篡改付费凭据。

## 回滚方式

- 若只回滚奖励项展示，移除 `ChestGiftPopup` 的 itemContainer/itemPrefab、动态生成方法和 Prefab 绑定，并删除 `ChestGiftItem.cs/.meta` 及 `ChestGiftItem.prefab` 根组件即可；Shop、购买记录与奖励交付数据无需迁移。
- 若只回滚“五档全购后拒绝指定档位打开”，删除 `ChestGiftController.Open()` 顶部的 `AreAllVariantsPurchased()` 门禁及两个私有辅助方法即可；不需要修改存档或 Prefab。
- 从 `DailyScheduleModule` 移除 `GiftManager` 注册，并停止读写 `GiftData` 的 ChestGift 计数字段，可独立回滚日刷轮换；若只回滚本次职责拆分，可把 `ChestGiftController` 逻辑内联回 `GiftManager`。兼容期内不要先删除 `GameData/GameSchedule` 的旧迁移字段。
- 从 `StartPanel` 移除 `DailyChestGiftEnter` 刷新、删除主页嵌套实例与入口脚本组件，可独立回滚主页入口；同时删除 `GameData` 三个 Gift 字段和 `GiftManager` 展示资格逻辑，可回滚首次主页、旧存档迁移和购买当日隐藏。
- 若只回滚本次配置驱动加载，可恢复 `ChestGiftController` 的固定资源数组和 `ChestGiftPopup` Inspector 字段，并恢复旧构建工具；无需迁移存档与购买记录。
- 清空 Shop 的 `ChestPrefab` 可即时关闭对应档位；删除 `GiftManager`、`IGiftController`、`ChestGiftController`、`GiftPopup`、`GiftPopupType` 路由、五个颜色 Variant 和母版上的 `ChestGiftPopup` 组件，可回滚本阶段弹窗能力。

## 2026-09-03 购买映射与回调所有权修正

### 原行为与问题

- ChestGift 曾在 Popup 内持有 IAP 回调；由于遮罩允许关闭界面，平台回调到达时 Popup 可能已销毁，存在已支付但本地奖励链不再执行的风险。
- 商品反查与 CoinGift、ShopPanel 重复实现，并错误要求一次性礼包必须使用 `Possessive` ProductType。

### 当前行为

- `ChestGiftController` 持有不可变 Offer 快照、购买请求版本和平台回调。Popup 只负责发起与仍存活时刷新 UI，关闭界面不会取消已提交的购买交付。
- 成功回调依次核对请求商品、重复回调和购买计数，调用公共购买记录后交付全部 Coin/Skin；任一配置奖励无法识别时在发起支付前禁用该档。
- Shop/Product 关系统一调用 `PurchaseManager.TryResolveUniqueProduct()`；平台 ProductId 统一调用 `PurchaseManager.TryGetProductId()`。
- `ProductType` 只决定平台 `Consumable / Asset / Subscriptions` 属性；`BuyTimes=1` 是 ChestGift 当前业务配置要求，两者互不推导。

### 验证、风险与回滚

- 静态扫描确认购买回调和奖励交付位于 Controller，Popup 销毁判断只影响界面刷新；未发现 ProductType 限购判断。
- 执行 `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies`，结果为 `0 error / 122 warning`；warning 属于当前工程既有范围。
- 本条描述的是修改前状态；最新生成 `TbProducts` 已包含 `chest_pack_1～5 -> 20008～20012`，并全部标记为 `Possessive`。当前映射和 Restore 行为以文末“Possessive 恢复购买”章节为准。
- PurchaseCache 与 Coin/Skin 落盘不是单事务，真实支付、杀进程恢复、重复 SDK 回调和 Android/iOS 原生覆盖层仍为 `uncertain`。
- 回滚可把购买回调移回 Popup 并恢复本地映射，但会重新暴露遮罩关闭后丢失交付与映射规则分叉的问题；无需迁移现有存档字段。

## 2026-09-03 ChestGiftController 职责精简

### 背景与原行为基线

- `ChestGiftController` 在上一轮稳定性修正后达到 635 行，同时承担会话、Daily 轮换、商品目录解析、资源校验、SKU/奖励校验、Popup 创建和支付交付。
- 按档位选择、Popup 快照刷新和购买点击分别重复执行 Shop/Product/SKU/奖励判断；继续把配置细节留在 Controller 会增加后续修改分叉风险。

### 调整后的职责与流程

- 新增内部 `ChestGiftOfferResolver`，集中处理档位到 Shop、唯一 Product、Resources Prefab、平台 SKU、Coin/Skin 奖励和 `BuyTimes + PurchaseCache` 校验。
- `ChestGiftController` 缩减为 454 行，只保留会话资格、Daily 轮换、当前档选择、弹窗创建、购买回调所有权和奖励交付。
- 普通入口统一走 `TryResolveAvailable()` 获得已经校验的 Offer；Popup 刷新和购买前复核统一走 `IsAvailable()`，不再复制一套条件。
- GM 指定档位仍走宽松 `TryCreatePreview()`：只要该档 Shop/Prefab 配置存在即可打开；Product 无效时 Popup 保持禁用态，原预览行为不变。
- 占位 SKU 判断从 CoinGift/ChestGift 私有实现收口到 `PurchaseManager.IsPlaceholderSku()`，两个礼物控制器和 ChestGiftPopup 共用同一口径。
- 为保持职责说明与代码事实一致，更新了 `ChestGiftController` 原类注释；其它既有注释未做无关改写。

### 影响、验证与回滚

- 影响文件：`ChestGiftController.cs`、新增 `ChestGiftOfferResolver.cs/.meta`、`PurchaseManager.cs`、`CoinGiftController.cs`、`ChestGiftPopup.cs`；未修改 Prefab、Shop/Products、存档字段、事件或平台调用顺序。
- 行为保持：首次主页抑制、当地 Daily 五档轮换、已购跳过、五档全购终止、遮罩关闭后回调继续、Coin/Skin 交付和成功提示均未改变。
- 执行 `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies`，最终结果 `0 error / 122 warning`；warning 属于当前工程既有范围。
- `git diff --check` 与重复校验静态扫描通过。Unity PlayMode、真实支付回调及 Android/iOS 原生覆盖层仍为 `uncertain`。
- 回滚时删除 `ChestGiftOfferResolver.cs/.meta`，把目录解析方法恢复到 Controller，并把占位 SKU 判断恢复到原调用方即可；不涉及配置或存档迁移。

## 2026-09-03 ChestGift Possessive 恢复购买

- 最新 `TbProducts` 已将 `chest_pack_1～5` 分别映射到 Shop `20008～20012`，类型均为 `Possessive`；五档 Shop 均包含 Coin 与一个 Skin。
- `PurchaseManager` 现在按 Luban ProductId 反查 Possessive 商品，Restore 时明确跳过 Coin，只通过 `SimpleBag.GetNewSkin()` 恢复有效 Skin；既有 NoAds/UnlimitTips 永久权益仍可恢复。
- 重复 Restore 通过 Skin 拥有列表和永久道具最低数量保持幂等，`PurchaseCache` 只保证至少记录一次，不再因已有缓存直接跳过缺失权益检查。
- 运行时程序集构建为 `0 error / 122 warning`；当前五个 Skin ID 均能在 `tbskin.json` 找到。PlayMode、真实商店 Restore、重装恢复和移动端原生回调仍为 `uncertain`。
- `chest_pack_1～4` 当前平台 SKU 与 Coin Pack 重复但 ProductType 不同，发布前必须核对平台后台商品属性；完整恢复边界与回滚见 `Assets/Game/Scripts/SDK/Services/Purchase/PurchaseManager恢复购买功能记录.md`。

## 2026-09-03 DailyChestGiftEnter 当地自然日倒计时

- 需求是在主页 Daily ChestGift 入口补充 `countdown_txt`，显示当前设备当地时间到次日 00:00 的剩余时间；修改前入口只管理显隐与点击，文本仍保留 Prefab 静态占位内容。
- `DailyChestGiftEnter.cs` 新增 Inspector 序列化的 `TMP_Text countdownText`，Prefab 直接绑定现有 `countdown_txt` 组件；运行时不使用节点名、`Transform.Find` 或层级遍历查找倒计时文本。
- 入口可见时立即刷新一次，并使用 `Time.unscaledTime` 每秒驱动显示 `HH:mm:ss`；跨过当地 00:00 后下一次刷新自然进入新一天的倒计时。入口隐藏时组件不执行 `Update`，不产生持续刷新开销。
- 本次不修改 ChestGift 展示资格、五档轮换、购买日隐藏、`GameSchedule`、资源加载、IAP、奖励、全局事件或 analytics；Prefab 中已有的节点位置、字体材质和其它美术改动保持不变，只追加序列化引用。
- 静态检查确认 `countdown_txt`、TMP 组件 fileID `7646588154131298347` 与 `countdownText` 引用一致；`dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies` 结果为 `0 error / 122 warning`，warning 属于当前工程既有范围。
- `uncertain`：尚未在 Unity PlayMode 验证主页首次显示、跨当地 00:00、系统时间修改、应用前后台切换及 Android/iOS 横屏视觉。
- 回滚时移除 `DailyChestGiftEnter` 的 `countdownText`、每秒刷新状态和方法，并删除 Prefab 根组件上的 `countdownText` 引用；不需要迁移或清理存档。

## 2026-09-03 GiftData 默认 Asset

- 在 `Assets/Game/DB/` 新增 `GiftData.asset/.meta`，资源直接引用 `GiftData.cs` 的脚本 GUID，并显式保存 `dataVersion=0`、`hasReachedGiftHome=false`、`chestGiftDailyRefreshCount=0`、`lastChestGiftPurchaseLocalDay=0` 四个全新数据默认值。
- `Assets/Game/Settings/DBSetting.asset` 已登记 `GiftData`，文件路径为 `Assets/Game/DB/GiftData.cs`，存储类型为 `PlayerPrefs`（枚举值 0）且 `isValid=1`；`DB.GiftData.cs` 提供与 `DB.DrawGame.cs/DB.IWannerGame.cs` 一致的静态获取和保存入口。
- `DBManager.Initialize()` 当前仍按类型扫描创建运行时实例，并依据 `GiftData.StorageType=PlayerPrefs` 加载玩家数据；`DBSetting` 同时作为 SaveModule 编辑器工具、访问代码生成和定向清理 PlayerPrefs/Json 数据的统一登记表，不能因运行时存在反射扫描而省略。
- 静态检查确认 Asset 脚本 GUID `f18687c259999a74b86e41aa6477da28` 与 `GiftData.cs.meta` 一致，新 Asset GUID 在 `Assets/` 内唯一，`DBSetting` 中 GiftData 条目唯一且路径/存储类型正确；运行时程序集构建结果为 `0 error / 122 warning`。
- `uncertain`：尚未在 Unity Inspector 打开该 Asset 验证导入后的字段显示，也未执行真实 PlayerPrefs 新建、旧数据迁移和重启读取测试。
- 完整回滚时删除 `GiftData.asset/.meta`、`DB.GiftData.cs/.meta` 和 `DBSetting.asset` 中的 GiftData 条目，并撤回 GiftData 数据类及迁移调用；仅删除 Asset 不会自动删除 PlayerPrefs 中已有的 `SaveData_GiftData`。

## 2026-09-03 ChestGift 启动序列自动弹出

- `LoadManager.OnPostProcess()` 在 `StartPanel` 已确认首次主页资格、内购恢复同步和其它收尾初始化完成后，启动一次性 `StartupPopupSequence`。ChestGift 固定排在更新公告和旧 Tips 转金币弹窗之后。
- 前置弹窗不满足资格时立即跳过；真实打开时必须等待 `OnClose`，并跨一帧避免重入 UIManager 栈清理后，才会评估 ChestGift 资格。
- ChestGift 仍通过 `IsEntryAvailable()` 完整检查首次主页抑制、购买当日、已购档、全档完成、Shop/Product/SKU/Skin 和 Resources；序列不复制这套业务规则。
- `GiftManager.OpenStartupOnce()` 在弹窗真实创建成功后才记录当前进程会话已自动展示 ChestGift；失败或无资格不记录，普通入口仍可继续调用 `Open()`。没有新增存档字段或改动 Daily 计数。
- 运行时程序集构建通过：`0 error / 122 warning`。Unity PlayMode 的首次/第二次启动、公告/转换组合、购买当日、五档全购、外部强制关闭和 Android/iOS 真机仍为 `uncertain`。
- 回滚可以只移除 Loading 中的序列组装与 `GiftManager.OpenStartupOnce()`，恢复公告/转换弹窗的直接创建；ChestGift 配置、购买记录、Daily 计数和入口 Prefab 不需迁移。

## 2026-09-03 ChestGiftItem 奖励数量格式

- 修改前 `ChestGiftItem` 对 Coin 和 Skin 都显示纯数字数量，因此金币缺少 `x` 前缀，Skin 会显示没有业务意义的数量 `1`。
- 当前 Coin 奖励显示为 `x{num}`，数量继续来自 Shop `Rewards` 中的 `IntParam`；Skin 奖励只显示配置 Skin ID 对应的异步图标，数量文本保持隐藏。
- 影响范围仅为 `ChestGiftItem.cs` 的展示刷新；未修改 `ChestGiftItem.prefab` 绑定、Shop 配置、奖励交付、购买记录、存档、事件或资源加载路径。
- 静态核对确认 Coin 与 Skin 分别进入 `RefreshCoin()` 和 `RefreshSkin()`，两类数量显示不会互相覆盖；运行时程序集构建结果为 `0 error / 122 warning`。Unity PlayMode、不同字体下的文本宽度及 Android/iOS 横屏视觉仍为 `uncertain`。
- 回滚时把 `ApplyCount()` 恢复为纯数字，并让 Skin 的两次视觉刷新重新传入数量 `1` 即可；不涉及数据迁移。

## 2026-09-03 Boxer 礼包皮肤换装预览

- 用户在 `ChestGiftPopup.prefab` 新增 `Boxer_Rt`，并将其 `RawImage.texture` 指向 ShopPanel 现有 `boxer.renderTexture`；修改前 ChestGift 只生成 Coin/Skin 奖励图标，没有启用场景 Boxer 换装预览。
- `ChestGiftPopup` 当前复用 `ShopPanelBoxerPreviewController`：打开有效礼包后读取玩家当前 Back、Face、Body、FullBody 穿搭，再用 Shop `Rewards` 中配置的 Skin ID 覆盖对应 `SkinPart`，在 `Boxer_Rt` 展示同一套部件 prefab、Spine Skin 和切换动画。
- 该流程只读当前穿搭和 Skin 配置，不调用 `GameData.Save()`、`Bag.GetNewSkin()` 或皮肤选择事件；礼包 Skin 仍只在购买成功交付链中授予。
- Popup 关闭、重新打开或 Skin 配置无效时会释放预览控制器、使未完成的异步部件加载失效、清理临时实例并关闭场景 `Skin` 根节点。未修改用户提供的 `ChestGiftPopup.prefab`、五个 Resources Variant、`boxer.renderTexture` 或 Shop/Skin 配置。
- 静态核对确认当前五档 Skin `203014～203018` 均为 Body，配置的部件 prefab 均存在；运行时程序集构建结果为 `0 error / 122 warning`。
- 待验证：Unity PlayMode 中五档 Body Skin 的尺寸、层级、动画和透明背景；从 ShopPanel Skin 页叠加打开 ChestGift 后的共享场景预览所有权，以及 Android/iOS RenderTexture 兼容性仍为 `uncertain`。
- 回滚时移除 `ChestGiftPopup` 的 Boxer 预览字段与方法调用即可；Prefab、配置、购买记录、奖励交付和存档均无需迁移。

## 2026-09-03 ChestGift 现价与划线原价

- `ChestGiftPopup.prefab` 当前的 `price_txt` 与 `originPrice_txt` 分别继续由既有生成字段 `UiLibao205Txt`、`UiLibao204Txt` 引用；修改前现价读取平台本地化价格，但原价始终被清空。
- 新增 `GiftPriceFormatter`，由 CoinGift 与 ChestGift 共用同一条规则：现价直接使用平台 `Details.Price`；原价使用 `Details.RawPrice ÷ (1 - Shop.discount)` 反算，按本地化现价的小数位数四舍五入，并保留价格字符串的币种前后缀和点/逗号小数分隔符。
- 有效原价使用 TMP `<s>` 标签显示删除线。折扣不在 1～99、商品元数据缺失、RawPrice 无效或本地化价格无法解析时，`originPrice_txt` 清空并隐藏，`price_txt` 和真实购买价格不受影响。
- 本次只新增 Gift UI 价格格式工具并修改 CoinGift/ChestGift 的显示调用；未修改用户的 ChestGift prefab、生成绑定、Shop/Products 配置、平台 SKU、PurchaseManager、购买请求或奖励交付。
- 静态回读确认 `originPrice_txt` 与 `price_txt` 的 TMP fileID 仍分别绑定到 `UiLibao204Txt` 与 `UiLibao205Txt`，且原价文本启用了 Rich Text；运行时程序集构建结果为 `0 error / 122 warning`。
- 待验证：美元、逗号小数币种、零小数币种和长币种后缀的 PlayMode 排版，以及 Android/iOS 商店真实回传价格仍为 `uncertain`。
- 回滚时让 CoinGift 恢复本地格式方法，并让 ChestGift 再次固定清空原价；无需修改配置、Prefab、SKU 或存档。

## 2026-09-03 无 Skin 奖励时隐藏预览区

- 修改前 `ChestGiftPopup` 在 Shop 没有有效 Skin 奖励时会清理场景 Boxer，但 prefab 中包含背景与 `Boxer_Rt` 的 `skinPreview` 根节点仍保持显示。
- 当前 `RefreshSkinPreview()` 只有在 Shop `Rewards` 存在可解析的 Skin ID、SkinPart 有效且 `Boxer_Rt` 已绑定 RenderTexture 时才显示整个 `skinPreview`；其余情况隐藏该根节点并释放场景预览控制器。
- 弹窗刚打开、被其它 UI 遮挡、关闭或重新配置商品时也先隐藏预览区，避免上一档或 prefab 静态画面闪现；有效 Skin 配置完成应用后再显示。
- 本次只修改 `ChestGiftPopup.cs` 的 UI 显隐与校验，没有修改用户提供的 prefab、Shop/Skin 配置、购买、奖励、存档、事件或 RenderTexture。
- 静态回读确认母版中仅有一个 `skinPreview`、一个 `Boxer_Rt`，且 RawImage 仍引用既有 `boxer.renderTexture`；运行时程序集构建结果为 `0 error / 122 warning`。
- 待验证：Unity PlayMode 中无 Skin、有 Skin、无效 Skin 和缺失 RenderTexture 四种状态的布局切换，以及 Android/iOS 横屏视觉仍为 `uncertain`。
- 回滚时移除 `skinPreview` 根节点缓存和显隐调用即可；无需迁移数据或资源。

## 2026-09-03 GiftData 结构修正、皮肤预览场景重试与不可购买原价显隐

### 需求与原行为

- Gift 的首次主页、ChestGift Daily 轮换次数和最近购买当地日期曾分别存放在 `GameData` 与 `GameSchedule`，同一业务的数据所有权分散。
- ChestGift 只要取得 `Boxer_Rt` 的 RenderTexture 就会显示 `skinPreview`；当启动礼包早于 Main 场景加载、场景 `Skin` 根节点、Camera 或 Actor 尚未就绪时，会留下只有背景、没有角色的空预览。
- 原划线价只依赖折扣和价格字符串计算，没有把支付初始化、商品元数据和当前礼包购买资格共同作为显示门禁。

### 当前实现

- 独立 `GiftData` 集中保存 `dataVersion`、`hasReachedGiftHome`、`chestGiftDailyRefreshCount` 和 `lastChestGiftPurchaseLocalDay`。结构遵循现有 SaveModule：`GiftData.cs` 只定义序列化字段，`GiftData_Partial.cs` 提供 `Init()` 和属性，`DB.GiftData.cs` 提供与 `DB.DrawGame/DB.IWannerGame` 一致的访问与保存入口。
- `GiftManager` 恢复为只负责类型路由、窗口互斥、入口通知和 Schedule 分发；首次读取与历史 `GameData/GameSchedule` 的一次性迁移收口在 `ChestGiftController`，没有覆盖 `GameData.FromJson()`，也没有新增额外迁移 Partial。
- 旧字段至少保留一个兼容版本且新业务不再写入。缺少旧版本字段的升级用户按已经到达过主页迁移，避免升级后错误进入“首个会话全部隐藏”；共享 `PurchaseCache` 仍留在 `GameData`，因为它同时服务普通 Shop、NoAds 和全部 IAP，不属于 Gift 独占数据。
- 日志证据确认启动队列可能在 Main 场景前打开 ChestGift，此时共享预览控制器找不到场景 `Skin` 根。修复只在 `ChestGiftPopup` 内检查该场景根：未加载时先隐藏预览区，并监听 `SceneManager.sceneLoaded`；Main 场景加载完成且弹窗仍打开时重新建立预览、应用当前礼包 Skin 后显示。共享 `ShopPanelBoxerPreviewController` 保持原结构和行为。
- ChestGift 只有在支付已初始化、当前 Offer 仍可购买、商品元数据存在且本地化现价有效时才展示反算划线原价；该版本在任一条件失败时清空并隐藏 `originPrice_txt`，同时禁用购买按钮。2026-09-14 的无网提示需求仅调整无网时的按钮交互例外，划线原价门禁保持不变。公共 `GiftPriceFormatter` 也拒绝在支付未初始化时生成原价，CoinGift 同步获得相同保护。
- Popup 正常关闭和异常销毁都会解除场景加载订阅并释放临时皮肤预览，不写入皮肤选择或 Gift 存档。

### 验证、剩余风险与回滚

- 最终运行时程序集构建通过：`0 error / 122 warning`；Editor 程序集构建为 `0 error / 4 warning`，warning 属于当前工程既有范围。静态扫描确认 ChestGift 新业务只写 `GiftData`，旧字段只有初始化与迁移读取；`GameData.GiftMigration.cs` 已移除。
- Unity Editor 已成功导入 `GiftData.cs`、`DB.GiftData.cs`、ChestGift 和预览脚本，最近日志未发现相关 `error CS`。
- 只读核对 `#Shop.xlsx` 与 `Skin.xlsx`：五档 Skin `203014～203018` 均为 Body，`PrefabResource` 对应资源均存在。配置数值仍由用户维护，其中 Shop `20010/20011/20012` 金币、普通礼包 4 折扣和 NoAds Bundle 折扣与付费文档不一致，正式验收前需校正。
- `uncertain`：尚未完成真实 PlayerPrefs 的全新用户/升级用户/迁移后重启验证，也未在 PlayMode 和 Android/iOS 真机确认五档 Skin 的最终 RenderTexture 画面。若 Main 加载后的重试仍为空，需要继续从 Camera culling、Actor Spine 材质和设备 RenderTexture 支持取证。
- 回滚集中存档时应先恢复 Controller 对旧字段的读写，再移除 `GiftData`；直接删除旧字段会破坏尚未迁移的历史用户。场景重试可独立回滚，不影响 Shop 配置、购买和奖励交付；原价门禁可独立回滚到仅按格式解析，但会重新暴露不可购买商品显示原价的问题。

## 2026-09-04 ChestGift 主动关闭按钮与 ClickBlock 接入

### 需求与原行为基线

- `ChestGiftPopup.prefab` 已包含名为 `Close_Btn` 的公共关闭按钮 Prefab 实例，但 `ChestGiftPopup` 没有序列化引用或点击监听，玩家点击该按钮不会主动关闭礼包。
- ChestGift 购买由 `ChestGiftController` 持有原生支付请求和回调；弹窗只负责发起请求与刷新仍存活的视图。第一版接入曾通过分别禁用购买按钮和关闭按钮阻挡输入，用户随后明确要求改用项目公共 `ClickBlock`。

### 当前行为与影响范围

- `ChestGiftPopup.cs` 新增私有序列化字段 `closeBtn` 与 `clickBlock`，二者均由母版 Prefab Inspector 显式绑定；运行时只使用序列化引用，不按节点名或层级搜索。`closeBtn` 在 `OnOpen/OnClose` 对应的 `BindButtons/UnbindButtons` 中成对注册和注销监听。
- 点击关闭按钮且没有 ChestGift 购买请求时，通过 `GiftManager.Close(this)` 销毁当前弹窗，使 `GiftPopup.OnClose()` 继续清理 GiftManager 的活动弹窗引用。
- `ChestGiftController.Open()` 调用共享 UIManager 时显式使用 `isCloseByMask: false`，关闭点击公共空白遮罩退出的能力；`closeBtn` 是当前界面唯一主动关闭入口。
- `ChestGiftPopup.prefab` 在内容根节点末尾复用公共 `Assets/Game/UI/Global/Prefabs/ClickBlock.prefab`，保持全屏拉伸、默认 inactive，并位于全部礼包内容之后。开始购买前先激活 ClickBlock；发起失败或购买回调完成时关闭，避免同步回调后遮罩被错误重新打开。
- 购买期间不再分别切换关闭按钮和购买按钮的 `Interactable`；购买按钮的交互状态只表达商品是否可购，实际异步输入统一由 ClickBlock 覆盖。按钮回调继续检查 `IsActionInProgress`，作为异常层级或外部调用下的防御。
- `ChestGiftPopupPrefabBuilder` 不负责查找或绑定新增的 `closeBtn/clickBlock`，两项引用归 Prefab Inspector 所有；五个 Resources 颜色 Variant 继续继承母版，不新增各档重复绑定。
- 本轮没有修改 Shop/Product、奖励交付、存档、Daily 轮换、全局事件、analytics 或资源路径。Prefab 中同时存在的 Container 布局、图片尺寸与位置调整属于已有美术工作区改动，本次仅补齐关闭按钮、ClickBlock 及其序列化绑定，未覆盖这些调整。

### 验证、剩余风险与回滚

- 最终执行 `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies` 与 `dotnet build Assembly-CSharp-Editor.csproj --no-restore --no-dependencies` 均返回 `exit 0 / 0 warning / 0 error`。中间一次编译曾发现私有方法重命名漏改导致 `CS0103`，补齐调用点后已重新编译通过。
- 本次代码和中文文档目标执行 `git diff --check` 通过；Prefab diff 仍包含 Unity 为当前美术新增布局组件生成的空值行尾空格，本次未通过格式化覆盖该并行美术改动。静态核对确认 `closeBtn` 引用指向当前 `Close_Btn` 实例的 `CustomButton`，五个颜色 Variant 均引用 ChestGift 母版 GUID。
- Unity Editor 日志确认母版及五个颜色 Variant 均由 PrefabImporter 成功重新导入，未发现本次相关 `error CS`、YAML 或 Prefab 导入错误。
- `uncertain`：尚未在 PlayMode 实际点击验证普通关闭、重复打开、购买发起失败及原生 IAP 回调恢复；Android/iOS 横屏触控和安全区视觉也未做真机验证。
- 回滚时将 `ChestGiftController.Open()` 的 `isCloseByMask` 恢复为 `true`，移除 `ChestGiftPopup.cs` 的 `closeBtn/clickBlock` 字段、监听、关闭回调和 ClickBlock 状态同步，并移除母版中的 ClickBlock 实例及两项序列化引用即可；无需迁移配置、存档或购买记录，也不应回滚当前美术布局调整。

## 2026-09-04 ChestGiftPopup 局部结构优化

### 需求与原行为基线

- 本次要求仅整理 `ChestGiftPopup` 代码结构，不改变玩家可见结果。优化前弹窗仍保持：控制器注入 Offer、购买期间由 ClickBlock 阻挡、`closeBtn` 主动关闭、公共空白遮罩不可关闭、场景加载后重试 Skin 预览。
- 优化前常量、Inspector 字段和运行时状态混排；打开与关闭生命周期重复清空 Offer 字段；四条无效商品分支重复清空价格并禁用购买；购买发起失败与异步失败重复恢复输入。
- `skinPreview` 与 `Boxer_Rt` 已由用户放在母版 Prefab 中，但代码仍通过 `GetComponentsInChildren + FirstOrDefault` 按名称扫描；此外 `TryGetCoinReward()` 没有任何调用方，空 `OnCreate()` 只转调基类。

### 当前结构与影响范围

- `ChestGiftPopup.cs` 现按常量配置、Inspector 绑定、运行时状态、类型与商品信息、生命周期、商品展示、皮肤预览、购买交互和倒计时分区；本次没有拆分新脚本或增加公共抽象。
- `ResetOfferState()` 统一负责 Offer、Shop、Product 与档位清理；`SetOfferUnavailable()` 统一处理不可购买时的价格和按钮状态；`RestorePurchaseInteraction()` 统一处理购买未发起或失败后的 ClickBlock 与购买按钮恢复。
- 删除无行为的空 `OnCreate()`、无调用的 `TryGetCoinReward()` 以及不再需要的 LINQ/节点名常量。
- 新增 Inspector 字段 `skinPreviewRoot` 与 `boxerPreview`，母版分别绑定现有 `skinPreview/GameObject` 和 `Boxer_Rt/RawImage`；运行时不再查找这两个 Prefab 对象。创建共享 `ShopPanelBoxerPreviewController` 前会同时校验两项绑定和 RenderTexture。
- 影响文件仅为 `ChestGiftPopup.cs`、`ChestGiftPopup.prefab` 及本地记录。五个 Resources Variant 继续继承母版；`ChestGiftController`、GiftManager、Shop/Product、购买、奖励、存档、Daily、事件、analytics 和资源路径没有因本次结构整理而改变。

### 验证、剩余风险与回滚

- 首次完整编译中，`dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -v:q` 返回 `0 error / 122` 条工程既有 warning，Editor 工程返回 `0 error / 4` 条工程既有 warning；最终增量复核两份工程均返回 `0 error / 0 warning`。
- 静态核对确认 `ChestGiftPopup.cs` 不再包含 `GetComponentsInChildren`、`GameObject.Find`、`Transform.Find` 或 LINQ `FirstOrDefault`；母版的 `skinPreviewRoot`、`boxerPreview`、`closeBtn`、`clickBlock` 均为非空序列化引用，ClickBlock 仍默认 inactive 且位于内容根最后，五个颜色 Variant 仍继承母版 GUID。
- Unity 当前运行会话的 Hot Reload 日志保留过一次中间保存版本的编译错误；磁盘最终代码已经由两份工程独立编译通过，但退出 PlayMode 后的 Unity 完整程序集重载、Prefab Inspector 显示、Skin 实际画面、购买成功/失败、关闭按钮和 Android/iOS 横屏触控仍为 `uncertain`。
- 普通回滚可内联三个局部收口方法并恢复空方法/未使用代码，但应保留 `skinPreviewRoot/boxerPreview` 的 Inspector 引用以遵守 Prefab 绑定规则；只有用户明确撤销该规则时，才可完整恢复运行时层级扫描。所有回滚均不需要迁移配置、存档或购买记录。

## 2026-09-04 ChestGiftItem Skin 原生尺寸与缩放调整

### 需求与原行为基线

- Skin 奖励图标加载完成后已经调用 `Image.SetNativeSize()`，但随后会按调用前的 icon 框尺寸重新计算宽高，实际没有采用指定的 Transform Scale。
- Coin 奖励使用固定金币 Sprite 和原有 Item 尺寸，不应继承 Skin 图标的缩放状态。

### 当前行为与影响范围

- `ChestGiftItem.ApplySkinVisual()` 在设置 Skin Sprite 后先调用 `SetNativeSize()`，再将 icon 的 `localScale` 统一设置为 `0.7`；删除旧的按框等比限制与辅助尺寸计算。
- 普通 `ApplyVisual()` 路径会把 icon 的 `localScale` 恢复为 `Vector3.one`，因此 Coin、清空以及同一 Item 后续被刷新时不会残留 Skin 的 `0.7` 缩放。
- 本次只修改 `ChestGiftItem.cs` 的 UI 展示尺寸逻辑；没有修改 Prefab、Shop/Skin 配置、Skin 图标解析与异步加载、奖励交付、存档、事件、打点或平台流程。

### 验证、剩余风险与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies` 执行成功，结果为 `0 error / 122 warning`；warning 属于工程现有范围。目标脚本执行 `git diff --check` 通过。
- `uncertain`：尚未在 Unity PlayMode 目视确认不同原始尺寸 Skin 图标在 `0.7` 缩放下的最终位置、裁切和与 Coin 并排时的观感；Android/iOS 横屏分辨率也未做真机验证。
- 回滚时恢复 `ApplySkinVisual()` 的框内等比尺寸计算，移除 `SkinIconScale` 以及普通展示路径的 Scale 重置即可；无需改动资源、配置或存档。

## 2026-09-04 Gift 每日倒计时配置与 ChestGift 开关

### 需求与原行为基线

- ChestGift 弹窗与主页入口、CoinGift 首购区域原本各自在视图中固定计算到设备当地次日 `00:00` 的剩余时间，无法通过礼包配置调整倒计时窗口。
- ChestGift 原本只有商品、购买日和会话资格校验，没有独立总开关；入口隐藏并不能作为所有打开路径的统一门禁。

### 当前行为与影响范围

- `GiftManager` 统一读取 `TbGlobalConst` 的 `DailyChestGiftDuration`、`DailyFirstPurchastGiftDuration` 和 `IsOpenDailyChest`。两个 Duration 单位为秒，配置小于等于 `0` 或 GlobalConst 尚未就绪时均回退为 `86400` 秒。
- ChestGift 弹窗和 `DailyChestGiftEnter` 共用 `DailyChestGiftDuration`，CoinGift 首购倒计时使用 `DailyFirstPurchastGiftDuration`；倒计时仍以设备当地当天 `00:00` 为起点计算配置周期终点。当前两项均为 `86400`，因此玩家可见行为仍是倒数到当地次日 `00:00`。
- `IsOpenDailyChest=false` 时，`GiftManager.IsEntryAvailable(ChestGift)` 返回 false，且 `OpenInternal()` 会拒绝启动自动弹出、普通打开和显式档位打开；CoinGift 不受该开关影响。配置尚未初始化时保持旧的开启门禁行为，后续商品解析仍会按原逻辑拒绝无效配置。
- 开关只控制 ChestGift 的入口与打开，不停止 `DailyScheduleModule` 对 ChestGift 轮换计数的自然日推进，不修改购买记录、奖励交付、存档结构、Prefab、资源路径、事件或打点。

### 验证、剩余风险与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies --verbosity:minimal` 执行成功，结果为 `0 error / 122 warning`；warning 属于工程现有范围。
- 静态回读确认三个倒计时显示点均改为调用 `GiftManager.GetDailyCountdown()`，ChestGift 的入口资格与打开路径均有开关门禁；用户新增的 Luban 生成字段和值保持不变。
- `uncertain`：尚未在 Unity PlayMode 验证 Duration 为 `0`、非 24 小时值与 `IsOpenDailyChest=false/true` 的动态表现；Android/iOS 本地时间、跨午夜、修改系统时间和配置重载也未做真机验证。Duration 非 `86400` 时只改变 UI 倒计时，不改变自然日 Schedule 刷新时机。
- 回滚时移除 `GiftManager` 的三项配置访问、倒计时方法和 ChestGift 开关门禁，并把三个视图恢复为直接计算当地次日 `00:00`；无需迁移 GiftData、购买记录或奖励数据。

## 2026-09-04 ChestGiftPopup 二次代码整理

### 原行为基线与整理目标

- 整理前 Popup 同时保存完整 `_offer` 以及可由它直接取得的 `_shop`、`_product`、`_giftIndex`，`ConfigureOffer()` 和关闭流程必须同步维护四份状态；公开 `GiftIndex` 属性在当前工程没有调用方。
- 本次目标只消除重复状态与无效 API，保持商品校验、奖励创建顺序、皮肤预览、购买回调、ClickBlock、倒计时和关闭生命周期的玩家可见行为不变。

### 当前结构与影响范围

- `ChestGiftPopup` 现在只持有 Controller 注入的 `_offer` 快照；Shop、Product 和档位均从该快照即时读取，删除三个重复字段、无调用的 `GiftIndex` 属性以及只负责同步清空这些字段的 `ResetOfferState()`。
- `RefreshOffer()` 使用局部 `shop/product` 变量按原顺序完成 Shop、BuyTimes、奖励/预览、Product、SKU、商店元数据和购买资格检查；`RefreshRewardItems()` 显式接收当前 Shop，减少对分散成员状态的依赖。
- 皮肤预览 RenderTexture 绑定检查改为单一模式匹配分支，错误日志与失败显隐保持不变。普通打开由 Resolver 严格校验，但 GM 指定档位预览仍允许不完整 Product，因此 Popup 侧的 BuyTimes、Product 与 SKU 禁用态校验继续保留。
- 修改仅涉及 `ChestGiftPopup.cs` 与中文记录；没有修改 Prefab/生成绑定、Controller、Resolver、Shop/Product、奖励交付、购买记录、存档、Daily、事件、打点或资源路径。

### 验证、剩余风险与回滚

- 静态扫描确认已不存在 `_shop/_product/_giftIndex/ResetOfferState` 残留引用，六个手工序列化字段仍均有运行时用途，母版 Prefab 绑定未修改。
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies --verbosity:minimal` 执行成功，结果为 `0 error / 122 warning`；warning 属于工程现有范围。
- `uncertain`：尚未在 Unity PlayMode 回归普通打开、GM 档位预览、场景预览重试、购买失败恢复、购买成功刷新与主动关闭；Android/iOS 原生支付覆盖层也未做真机验证。
- 回滚时恢复 `_shop/_product/_giftIndex`、`GiftIndex` 与 `ResetOfferState()`，并让各流程重新读取这些字段即可；无需回滚 Prefab、配置或玩家数据。

## 2026-09-04 ChestGiftPopup 移除动态 SkinPreview 并保留静态 Image

### 需求与原行为基线

- 用户已在母版 Prefab 中删除 `Boxer_Rt/RawImage`，并在原 `skinPreview` 节点下新增 `Icon/Image`；该 Image 当前没有 Sprite，后续资源来源、命名与赋值时机尚未确定。
- 修改前 `ChestGiftPopup` 仍会读取当前装备 Skin、等待 Boxer 场景对象、创建 RenderTexture 预览控制器并监听场景加载；这套动态换装预览已经不符合本次静态图片占位要求。

### 当前行为与影响范围

- `ChestGiftPopup` 只保留 `[SerializeField] Image skinImage`，由母版 Prefab Inspector 显式绑定 `skinPreview/Icon/Image`；没有新增节点查找、层级扫描或资源加载兜底。
- 打开弹窗时仅刷新占位状态：有 Sprite 时启用 Image 渲染，没有 Sprite 时关闭 Image 组件，避免空 Sprite 显示白色块；节点、RectTransform 和绑定继续保留，后续资源进入后可直接在 Inspector 或明确的业务入口赋值。
- 已删除 ChestGiftPopup 对 Boxer 场景对象、当前装备 Skin、`SkinUtils`、RenderTexture、`ShopPanelBoxerPreviewController` 和 `SceneManager.sceneLoaded` 的依赖，以及配套的 Show/Hide/Destroy 预览生命周期。共享的 ShopPanel Boxer 预览控制器和 ShopPanel 行为没有修改。
- 新 Image 的 `raycastTarget` 设为 false，不额外拦截弹窗点击。关闭按钮、ClickBlock、空白区域不可关闭、商品/价格/购买、奖励交付、存档、Daily、事件和资源路径均保持原职责。
- `ChestGiftItem` 中 Skin 奖励小图的现有加载和展示不受影响；本次仅移除弹窗中央区域的动态角色换装预览。五个颜色 Variant 继续继承母版绑定。

### 验证、剩余风险与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -v:q` 返回 `0 error / 122 warning`；`dotnet build Assembly-CSharp-Editor.csproj --no-restore --no-dependencies -v:q` 返回 `0 error / 4 warning`，warning 均来自工程既有范围。
- Unity Hot Reload 已识别 `ChestGiftPopup.cs` 改动，PrefabImporter 已成功导入母版和五个颜色 Variant；静态回读确认 `skinImage` 指向新增 Image、Sprite 当前为空、`raycastTarget=false`，旧 `skinPreviewRoot/boxerPreview` 绑定与动态预览代码均无残留。
- `uncertain`：静态大图的最终资源来源、赋值时机和适配规则尚未提供，因此当前只保留空占位；尚未在 PlayMode 目视确认弹窗布局、关闭按钮、ClickBlock、购买流程及 Android/iOS 横屏触控。
- 若恢复动态预览，可回滚本节对应脚本与 Prefab 绑定并恢复 RawImage/RenderTexture 链路；若仅接入静态资源，应保留 `skinImage` Inspector 绑定，在需求明确后增加单一资源赋值入口，无需迁移配置、存档或购买记录。

## 2026-09-04 ChestGiftPopup 恢复动态 SkinPreview

### 需求与恢复基线

- 用户撤回上一节的静态 Image 占位要求，要求恢复 ChestGiftPopup 原有动态 SkinPreview。
- 恢复前弹窗只保留空 `Icon/Image`，不再读取礼包 Skin、当前装备外观或场景 Boxer 预览宿主，因此中央区域无法展示礼包皮肤的动态试穿效果。

### 恢复后行为与影响范围

- `ChestGiftPopup.cs` 已恢复 `skinPreviewRoot` 与 `boxerPreview` 两项 `[SerializeField]`，母版 Inspector 分别绑定现有 `skinPreview` 根节点和 `Boxer_Rt/RawImage`；空 `Icon/Image` 占位及 `skinImage` 字段已移除。
- Prefab 已恢复 `Boxer_Rt`、RawImage 和原 `boxer.renderTexture` 引用。Prefab 内对象继续全部使用 Inspector 显式绑定，没有新增 `GameObject.Find`、`Transform.Find`、`GetComponentsInChildren` 或按 Prefab 节点名扫描的兜底。
- 弹窗会从当前 Shop 奖励解析 Skin，在玩家现有 Back、Face、Body、FullBody 外观上替换礼包对应部位，并交给共享 `ShopPanelBoxerPreviewController` 渲染。该过程只读取 `DB.GameData`，不授予 Skin、不修改装备、不写存档。
- `OnOpen` 恢复 Main 场景加载监听，`OnShow` 恢复预览，`OnHide/OnClose/OnDestroy` 关闭或释放预览控制器；场景 `Skin` 根尚未加载时隐藏预览区，场景加载完成后重试。场景宿主不是 Prefab 内绑定对象，仍沿用原场景根枚举与共享控制器解析流程。
- `closeBtn`、ClickBlock、点击空白区域不可关闭、购买/奖励/存档/Daily/事件和资源路径均保持不变。五个颜色 Variant 继续继承母版；用户现有的其它 Prefab 美术布局调整没有被覆盖。

### 验证、剩余风险与回滚

- 恢复后的 `ChestGiftPopup.cs` 与恢复前已验证的动态预览版本无代码差异；静态核对确认 `skinImage` 不再存在，`skinPreviewRoot`、`boxerPreview`、`closeBtn`、`clickBlock` 均为非空序列化引用，RawImage 仍引用原 RenderTexture。
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -v:q` 返回 `0 error / 122 warning`；`dotnet build Assembly-CSharp-Editor.csproj --no-restore --no-dependencies -v:q` 返回 `0 error / 4 warning`，warning 均属于工程既有范围。
- Unity Editor 日志确认母版与五个颜色 Variant 已由 PrefabImporter 成功重新导入，未观察到本次相关 `error CS` 或 Prefab 导入失败。
- `uncertain`：尚未在 PlayMode 实际确认五档礼包的 Boxer 画面、场景晚加载重试、遮挡后恢复、关闭清理与快速重复打开；Android/iOS 横屏 RenderTexture、触控和原生支付覆盖层也未做真机验证。
- 若再次改为静态图片，可按上一节回滚动态生命周期与 RawImage，并在资源来源明确后恢复 Inspector Image 绑定；本次恢复不涉及配置、存档、奖励或购买数据迁移。

## 2026-09-04 ChestGift SkinPreview 仅展示礼包奖励皮肤

### 需求与原行为基线

- 原 ChestGift 预览会先读取玩家正在穿戴的 Back、Face、Body、FullBody，再只替换礼包奖励所属的 SkinPart；因此预览画面会混入非礼包奖励的当前穿戴部件。
- 本次要求只改变 ChestGiftPopup 的临时 Boxer 画面：仅展示当前 Chest Reward 中的 Skin，玩家当前穿戴皮肤在该预览期间全部移除，但实际装备与存档不得改变。

### 当前行为与影响范围

- `RefreshSkinPreview()` 现在以四个空字符串初始化 Back、Face、Body、FullBody，只在解析到奖励 Skin 后给对应 SkinPart 写入奖励 Id，再调用现有 `ShopPanelBoxerPreviewController.Apply(...)`。
- 共享控制器对空 Back/Face/Body 会清理对应预览挂点，对空 FullBody 会恢复 `box_original` 基础 Boxer Skin。因此 Back、Face 或 Body 奖励显示为基础身体加单个奖励部件；FullBody 奖励显示该奖励全身皮肤且不叠加其它部件。
- ChestGiftPopup 不再为预览读取 `DB.GameData`，不会把正在穿戴的其它部位带入画面。预览仍只操作场景 Skin/RenderTexture 与临时实例，不修改当前装备字段、不调用 Save、不授予奖励、不派发 Skin 选择事件。
- 本次没有修改共享 `ShopPanelBoxerPreviewController`，因此 ShopPanel 的穿戴、试穿与恢复逻辑不受影响；Prefab Inspector 绑定、场景加载重试、关闭清理、`closeBtn`、ClickBlock、禁止空白关闭、购买与奖励交付均保持不变。

### 验证、剩余风险与回滚

- 静态核对确认 ChestGiftPopup 的四项 Apply 参数只可能为空或当前礼包奖励 Id；共享控制器的空部位分支会执行 `ClearPart()`，空 FullBody 分支会使用 `box_original`。
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -v:q` 返回 `0 error / 122 warning`；`dotnet build Assembly-CSharp-Editor.csproj --no-restore --no-dependencies -v:q` 返回 `0 error / 4 warning`，warning 均属于工程既有范围。
- Unity Editor 日志显示已对 `ChestGiftPopup.RefreshSkinPreview()` 完成 Hot Reload，未观察到本次相关 `error CS`。
- `uncertain`：尚未在 Unity PlayMode 逐档验证 Back、Face、Body、FullBody 奖励是否都只显示奖励内容，也未验证快速开关、场景晚加载及 Android/iOS 横屏 RenderTexture 表现。
- 回滚时恢复 `RefreshSkinPreview()` 对 `DB.GameData` 四个当前穿戴字段的读取，并保留按奖励 SkinPart 覆盖目标部位的逻辑即可；无需迁移 Prefab、配置、存档或购买数据。

## 2026-09-07 DailyChestGiftEnter 五档宝箱图标联动

### 需求与原行为基线

- `DailyChestGiftEnter` 的新 Prefab 视觉已包含红、蓝、绿、紫、橙五个宝箱图标，但脚本原本只刷新入口显隐、点击和倒计时，没有根据当前 ChestGift 档位切换图标。
- 当前业务档位继续由 `TbShop.DataList` 中 `ChestPrefab` 非空项的表顺序决定：1=`20008/ChestGiftPopup_red`、2=`20009/ChestGiftPopup_blue`、3=`20010/ChestGiftPopup_green`、4=`20011/ChestGiftPopup_purple`、5=`20012/ChestGiftPopup_orange`。

### 当前行为与影响范围

- `DailyChestGiftEnter` 新增 `chestIconsByVariant` 序列化数组，Prefab 按红、蓝、绿、紫、橙顺序显式绑定已有五个图标对象；运行时没有名称查找或层级扫描。
- 入口每次实际显示与刷新时调用 `ChestGiftController.TryResolveCurrentIndex()`，只激活最终可售档位对应的一个图标并关闭其余四个。这里使用的是跳过已购、配置无效档后的最终档位，因此入口图标与点击后打开的 ChestGift 保持一致。
- 图标只在入口刷新时更新，不加入每帧倒计时路径；`AvailabilityChanged` 和 Daily Schedule 原有通知继续驱动购买状态或自然日轮换后的入口刷新。
- 本次不修改档位计算、`TbShop/TbProducts`、购买与恢复、奖励、存档、倒计时、弹窗资源、全局事件或原生覆盖层流程。新增运行时工作量仅为入口刷新时遍历五个本地引用，对 Android/iOS 横屏性能无持续影响。

### 验证、剩余风险与回滚

- 静态校验确认 Prefab 数组的五个 fileID 依次指向红、蓝、绿、紫、橙图标；当前 `tbshop.json` 的五条有效 `ChestPrefab` 顺序与数组一致；`git diff --check` 通过。
- 执行 `dotnet build Assembly-CSharp.csproj --no-restore`，结果为 `0 error / 1140 warning`；warning 属于工程现有范围。首次 `--no-dependencies` 构建因 `Temp/bin/Debug` 缺少 Unity 项目依赖而失败，随后由包含项目依赖的构建补齐并验证通过。
- `uncertain`：尚未在 Unity PlayMode 逐档确认入口图标、购买后跳档、跨当地自然日切换，以及 Android/iOS 横屏显示；Prefab 当前保存的默认激活态为绿色，但运行时入口首次有效刷新会按最终档位覆盖该状态。
- 回滚时删除 `DailyChestGiftEnter.chestIconsByVariant`、`RefreshChestIcon()` 和 `Refresh()` 中的调用，再移除 Prefab 根组件上的五项数组绑定；不需要迁移配置或存档。

## 2026-09-07 ChestGift 购买成功飞金币并自动关闭

### 需求与原行为基线

- ChestGift 购买完成且奖励成功入包后，需要播放金币飞行动画；动画期间禁止操作，整批动画结束后自动关闭弹窗。
- 修改前，购买按钮在发起平台支付前打开已有 `ClickBlock`，失败时恢复交互；成功回调会立即关闭 `ClickBlock` 并刷新下一档商品，既没有奖励动画，也不会自动关闭。
- `ChestGiftController` 的成功回调发生在 `PurchaseCache` 更新、Coin 与 Skin 全部交付、成功提示和音效之后，因此动画属于本地展示层，不能再次修改金币或 Skin。

### 当前行为与数据、资源、输入流

- `ChestGiftPopup` 在购买前缓存当前视图版本、购买按钮 Transform 和当前 Shop 的 Coin 数量。平台 IAP 覆盖层显示及返回期间继续沿用购买前开启的 `ClickBlock`，不依赖 Unity 帧等待在原生覆盖层期间推进。
- 成功回调只启动 `CommonUIRewardAnimCtrl`：起点为购买按钮，终点为弹窗现有金币图，金币实例数沿用 CoinGift 的 `8/10/12/14/16` 数量分档。母版中已有 `CommonUIReward` 通过 Inspector 序列化绑定，五个 Resources 颜色 Variant 继续继承母版，不做名称或层级查找。
- 动画播放期间 `_isRewardAnimating` 与 `ClickBlock` 同时阻止再次购买和主动关闭；只有 `CommonUIRewardAnimCtrl.Play(...)` 返回后才调用 `GiftManager.Close(this)`。弹窗被外部关闭或复用版本变化后，旧异步回调不会再关闭新视图。
- 购买失败仍执行原 `RestorePurchaseInteraction()`，立即关闭 ClickBlock 并按当前 Offer 恢复购买按钮。动画绑定、起点、终点或 Coin 数量异常时记录错误并关闭成功弹窗，避免奖励已经到账但界面永久锁死。
- 本次不修改平台 SKU、购买记录、奖励交付顺序、Gift Daily 轮换、存档字段、成功提示、音效、全局事件或 Skin 预览。

### 验证、剩余风险与回滚

- 静态核对确认母版 `rewardAnimCtrl` 指向已有 `CommonUIRewardAnimCtrl`，五个颜色 Variant 均继承母版；ClickBlock 默认隐藏，购买成功路径在动画结束前没有解锁或刷新商品。
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -v:q` 通过：`0 error / 122 warning`，warning 属于工程既有范围。
- `uncertain`：尚未在 Unity PlayMode 和 Android/iOS 真机验证成功/取消/失败支付、原生覆盖层返回、飞行轨迹、金币数量档位、动画中触摸、外部强制关闭及动画结束自动关闭。
- 回滚时移除 `ChestGiftPopup` 的奖励动画字段、视图版本与成功异步收口，恢复成功回调中的 `SetClickBlocked(false) + RefreshOffer()`，并移除母版的 `rewardAnimCtrl` 与数量分档绑定；不需要回滚配置或迁移玩家数据。

## 2026-09-07 ChestGift 新皮肤红点补充

### 原因与需求

- ChestGift 购买成功后，Skin 已通过 `SimpleBag.GetNewSkin()` 写入 `OwnedSkin`，但调用来源为 `GetSkinWayType.Purchase`。
- 现有通用规则默认只让 `Reward` 和 `Free` 来源进入 `WaitingPreviewSkinId`；普通 SkinPart 内购买和广告解锁会直接展示或应用皮肤，因此 `Purchase/Ad` 被刻意排除。ChestGift 没有进入 SkinPart 预览流程，沿用默认 `Purchase` 规则会导致礼包中的新皮肤没有红点。

### 当前行为与边界

- `SimpleBag.GetNewSkin()` 新增默认关闭的 `markForPreview` 参数。ChestGift 发放 Skin 时仍传 `GetSkinWayType.Purchase`，同时传 `markForPreview: true`。
- 只有本次确实新增到 `OwnedSkin` 的礼包皮肤才会加入 `WaitingPreviewSkinId`，并通过 `NewSkinRedDotQuery.Refresh()` 更新 NewSkin 总节点、对应 SkinPart 子节点和 SkinItem 红点；重复获得已拥有皮肤不会重新制造待预览状态。
- 普通 ShopPanel 金币/内购购买、广告解锁及其它现有调用方未传该参数，原“不显示新皮肤红点”行为保持不变；`getNewSkin` 打点仍记录 `Purchase`，没有改变 IAP、购买次数、奖励交付、穿戴或资源加载。
- 红点状态在 `GameData.Save()` 中与新皮肤所有权一起持久化；ChestGift 成功后的金币动画和自动关闭顺序不变，关闭后即可在既有入口看到红点。

### 验证、风险与回滚

- 运行时程序集定向构建通过：`0 error / 121 warning`；`git diff --check` 通过，warning 为工程既有范围。
- 静态确认只有 ChestGift 使用 `markForPreview: true`，全部既有调用保持默认参数；新增状态仍由现有 `WaitingPreviewSkinId` 与 NewSkin 红点查询管理。
- `uncertain`：真实 IAP 后的总红点、部位红点、SkinItem 红点、进入皮肤页后的清除以及 Android/iOS 横屏流程尚未实测。
- 回滚时删除 `markForPreview` 参数和 ChestGift 命名参数，恢复 `GetNewSkin()` 仅按来源决定待预览状态；已进入 `WaitingPreviewSkinId` 的测试数据需进入皮肤页预览或清档后清除。

## 2026-09-07 ChestGift 飞金币终点修正

### 原因与修复行为

- 母版 `CommonUIReward` 的 `endPoint` 已由 Prefab Inspector 绑定到现有 `PropBlock`，但 ChestGift 成功路径调用了显式接收终点的 `Play(...)`，并传入 `UILibao2Jinbi101Img.transform`，运行时因此覆盖了 Prefab 中指定的终点。
- ChestGift 现在与 CoinGift 一样调用 `CommonUIRewardAnimCtrl.PlayFrom(...)`；起点仍为购买按钮，终点由 `CommonUIReward.endPoint` 读取，因此金币飞向母版已配置的 `PropBlock`。
- 本次没有修改 Prefab、奖励发放、购买记录、ClickBlock、动画数量分档或自动关闭时序，也没有新增节点查找和全局事件。

### 验证、风险与回滚

- 静态回读确认 `ChestGiftPopup.prefab` 的 `CommonUIReward.endPoint` 指向 `PropBlock` 的 RectTransform，ChestGift 代码不再把 `UILibao2Jinbi101Img` 作为显式终点。
- 运行时程序集定向构建通过：`0 error / 121 warning`；`git diff --check` 通过，warning 为工程既有范围。
- `uncertain`：仍需在 Unity PlayMode 和 Android/iOS 横屏下目视确认五档礼包的最终飞行落点、路径与自动关闭时序。
- 回滚时把 `PlayFrom(rewardSource, rewardAnimCount)` 恢复为显式 `Play(..., UILibao2Jinbi101Img.transform, ...)` 即可；不涉及 Prefab 或数据迁移。

## 2026-09-08 奖励项从第 10 帧开始依次缩放

- 需求与基线：原 `RefreshRewardItems` 按 Coin、Skin 顺序创建有效项，`LayoutRewardItems` 横向居中后直接显示。本次要求每项 Scale 从 0 到 1，耗时 0.5 秒，上一项完成才开始下一项，并从第 10 帧起播。
- 实现与范围：仅修改 `ChestGiftPopup.cs`，沿用 `_rewardItems` 顺序及现有 Inspector itemContainer/itemPrefab 引用；新建项布局后立即置零，以创建当帧为基准等待 10 个 Unity 帧，再播放暂停创建的 DOTween Sequence。每项使用 Append + DOScale(Vector3.one, 0.5f) + Linear；没有重叠或额外间隔。帧数按 Unity 实际帧计，不是固定秒数或 Animator 时间轴采样帧。
- 生命周期与流程：起播检查位于 Update 的秒级倒计时门禁之前；SetUpdate(true) 允许 timeScale=0 时播放。OnHide 暂停且禁止等待中的序列起播，OnShow 恢复已开始序列，未到起播帧则继续等待；刷新、关闭、销毁前 Kill 并清空待起播帧，避免访问旧 item。原生覆盖导致 PlayerLoop 停止时动画也停止，返回后继续，不承担支付完成或输入恢复职责。
- 复用边界：动画仅由本弹窗持有，不修改 ChestGiftItem 的异步图标加载、共享组件、布局、Prefab、购买、奖励交付、配置、存档或全局事件。有效奖励为空时不创建序列，原无效项清理路径不变。
- 验证：静态核对 item Prefab 根 localScale 为 (1,1,1)，串行 Append、十帧门禁、隐藏恢复及清理路径；定向 git diff --check 通过。初次定向 dotnet 构建因 Temp/bin/Debug 下 Apple.Core.dll 等依赖缺失报 200 个 CS0006，未取得编译通过证据；日志位于 Temp/ChestGiftItemScale-build.log。
- 未验证风险（uncertain）：尚未运行 Unity PlayMode 或 Android/iOS 真机。需验证逐帧起播、每项 0.5 秒、timeScale=0、等待/播放中关闭重开、遮挡恢复及 Skin 图标异步返回；慢速图标加载仍可能晚于缩放结束，此次未改变加载时序。
- 回滚：移除本次 DOTween 引用、常量、序列状态、播放/清理方法及生命周期调用，即恢复原直接显示；无需数据迁移。

## 2026-09-08 奖励容器横向弹性展开

- 需求与原基线：新增 itemContainer 的 ScaleX 表现，0.3 秒 OutBack。修改前只有 item 顺序缩放；本轮读取到工作区 RewardItemShowDuration 已为 0.2 秒，保留该值，前节所述 0.5 秒为历史基线。同步修正方法注释，避免与当前代码不符。
- 实现与边界：ChestGiftPopup 复用现有 itemContainer Inspector 引用与入场 Sequence，在全部 item Tween Append 后使用 Insert(0) 加入 DOScaleX(1, 0.3).SetEase(OutBack)，保证不推迟各 item 的既有起播位置。容器初始 X=0，Y/Z 保持原值；第 10 个 Unity 帧与第一个 item 一起开始。OutBack 会短暂超过 1 再回到 1。
- 生命周期与流程：容器和 item 共用暂停、恢复及非缩放时间；刷新、关闭、销毁时终止整个序列并把仍在播放或等待的容器 X 恢复为 1。未修改 Prefab、奖励图标加载、购买、发奖、输入锁、存档或事件。
- 验证：静态核对 Insert 位于所有 Append 之后、插入时间为 0、目标仅 X 轴、时长 0.3 秒和 OutBack；定向 diff 空白检查通过。此前编译已被依赖 DLL 缺失阻断，本轮未重复同一阻断命令，未取得编译成功证据。
- 未验证风险（uncertain）：Unity PlayMode 与 Android/iOS 横屏未实测，需目视确认弹性超出时无裁切，并覆盖播放中关闭、重开、遮挡恢复及单奖励项。容器缩放会同时影响其子项，属于本次指定表现。
- 回滚：移除容器时长常量、置零与 Insert 动画、终止时 X 恢复代码，保留 item 现有逐项动画；无需资源或数据迁移。

## 2026-09-08 Shop 有序奖励列表与多皮肤

### 需求、原行为与修改边界

用户明确要求同一个 Shop 奖励允许重复 Skin 类型，保留 `Coin;IntParam,600|Skin;StringParam,202032|Skin;StringParam,203029` 写法。原 H2 使用 map，Luban 在 H14 报 Skin 键重复；原礼包列表与 Boxer 预览也只取一个 Skin。

本次仅将 Shop.rewards 改为有序 `List<ShopReward>`。Postcard、每日挑战及通用奖励弹窗继续使用各自既有结构；当前 ChestGift 成功路径是控制器入包后在原 Popup 播放金币动画并关闭，不调用 CommonGetRewardPopup，故不扩展共享弹窗。此次为用户明确批准的奖励结构变更，原单类型一项的商品内容与顺序保持。

### 影响文件和流程

- `Luban/Configs/Datas/#Shop.xlsx`：只修改 H2 为 `(list#sep=|),ShopReward`，保留用户已修改的所有商品行。独立 OOXML 比对确认其它值、类型与公式不变。
- `Luban/Configs/Defines/shop_reward.xml`：新增 ShopReward，key 为 GameProps，value 为 ParamBase；Bean 用分号，参数用逗号，列表用竖线。
- `Assets/Configs/Code/Shop.cs`、`ShopReward.cs`、`Assets/Game/Configs/tbshop.json`：从 Luban 临时输出同步本次相关生成产物，其它生成文件未覆盖。
- `ShopRewardUtils.cs`：只读列表类型查询与 IntParam 合计，参数错误/溢出返回失败，禁止把 Skin ID 当数量合并。
- `ShopItem.cs`、`ShopPanel.cs`、`CoinGiftController.cs`：金币显示/入包/动画数量沿用既有入口，改为同类型合计；拥有状态查询改用列表查询。
- `ChestGiftOfferResolver.cs`、`ChestGiftController.cs`：逐项校验与逐项交付，Skin 每项单独调用 GetNewSkin；购买请求、购买计数和重复成功回调保护不变。
- `PurchaseManager.cs`：恢复购买遍历完整列表，保留 Coin 不恢复、逐个 Skin 恢复的规则；普通成功记录不再把列表压回 Dictionary，NoAds/UnlimitTips 仍由原入口发放，ChestGift 的 Coin/Skin 仍由其控制器发放，避免双发。
- `ChestGiftPopup.cs`：Coin -> Skin 分组顺序保留，同组内按配置顺序创建全部奖励项；不同部位皮肤同时传给既有 Boxer 预览控制器。同部位存在多件时预览第一件，其余仍显示独立图标并发放。关闭时沿用现有实例与异步加载清理。

资源流为配置文本 → Luban 列表 → ShopReward → 礼包校验/列表展示/购买回调 → Bag 与原存档链。无新增全局事件、Inspector 引用、Find 兜底、OnValidate 或平台支付调用。原生支付覆盖期间的状态与动画恢复时序未改。

### 验证证据

- Luban `client + cs-simple-json + json` 完整解析、验证和仓库外生成退出码 0。Shop 20008 包含 600 Coin、202032、203029 三项；20009～20012 的用户新增多皮肤和金币数也被保留。
- 原有运行时 JSON 与新 JSON 去除格式差异后，对照发现数据差异仅为用户表内已有的 20008～20012 奖励及 20013 名称/描述变更；没有按旧 Git 数据覆盖用户配置。
- 常规 dotnet build 被 Temp/bin/Debug 缺失的 200 个项目依赖 DLL 阻断。改用仓库外临时 csproj 引用当前 Library/ScriptAssemblies 缓存依赖、包含本次两个新增脚本，编译实际全部 Assembly-CSharp 源码通过：0 error / 125 warning。未修改 Unity 生成的项目文件，也未改 GF。
- 临时控制台引用本次编译程序集与实际 Luban.Runtime，执行实际 Shop 反序列化及 ShopRewardUtils：14 条商品全部读取成功；20008 的两件皮肤及顺序、600 金币、60+40 重复金币合计、缺失类型、Skin 非数量参数和 int 溢出检查均通过。
- 工作簿修改前后预览检查完成；新增类型文件和调用方通过定向 diff 检查。

### 剩余风险与验收清单

- `uncertain`：尚未执行 Unity PlayMode、Android/iOS 横屏、实际 IAP 与恢复购买。缓存依赖编译不替代 Unity 重新导入或真机验证。
- 重点验收 20008 的三项、其它档位四项奖励布局不越界；每件图标正确；脸部/背部/衣服同时预览；同部位多件只预览首件但均发奖；关闭/重新打开后无旧异步图标回写。
- 支付成功、失败、重复成功回调、支付期间关闭 Popup、Possessive Restore 重复调用均需验证购买次数和所有皮肤所有权；恢复购买仍不补发金币。
- 普通 Shop/CoinGift、广告商品、NoAds/UnlimitTips 与已有单皮肤礼包需回归。CommonGetRewardPopup 仍不支持直接接收 Shop 奖励列表，不得在未来调用处转成 Dictionary 规避类型检查。
- Shop JSON 从键值对数组变为对象列表，代码和 tbshop.json 必须成套更新；旧客户端不能读取新格式，不得单独热更该数据给旧代码。

### 回滚

成套撤回 Shop H2、shop_reward.xml、生成 Shop/ShopReward/tbshop.json、本次列表查询和 UI/购买适配。不撤销用户原有商品内容；回退 map 前须先与用户确认如何处理重复 Skin，否则旧类型仍会导表失败。文档保留此次风险与验证事实供复核。

## 2026-09-09 标题使用 Shop 商品名多语言

- 需求与基线：title_txt 的 text 通过当前 Shop 商品名称键取得多语言。当前生成字段实际为 GoodsName（JSON goodsName），不存在 Name 属性；母版已由用户把 u_popuppack_title_01_txt 改名 title_txt 并移除固定 Localize 组件，脚本此前未按商品填标题。
- 影响与实现：仅修改 ChestGiftPopup.cs，复用 Generated.cs 中既有 UPopuppackTitle01Txt 序列化引用。OnOpen 清空旧 Offer 后清空标题，RefreshOffer 读取当前 Offer.Shop.GoodsName 并调用 I2.Loc.LocalizationManager.GetTranslation，OnShow 重新读取当前语言；无商品、空键或翻译返回 null 时为空字符串。
- 绑定与流程：静态解析母版确认 UPopuppackTitle01Txt 的 TMP fileID 784991795605114595 仍属于 title_txt。保留用户已有 Prefab 改动，不新增字段、查找节点、配置、存档或全局事件。商品注入→商品名键→I2 当前语言→标题文本；购买、奖励、倒计时与入场动画保持原路径。
- 验证：定向绑定解析及 git diff --check 通过。dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -v:q /clp:ErrorsOnly 被 NETSDK1004 阻断，缺少 Temp/obj/Assembly-CSharp/project.assets.json，未取得编译通过证据，日志 Temp/ChestGiftTitle-build.log。
- 未验证风险（uncertain）：Unity PlayMode、各档位语言资源完整性及 Android/iOS 横屏长标题未实测；弹窗保持显示期间切语言不会即时订阅刷新，再次 OnShow 或刷新 Offer 时更新。仍使用既有生成字段名，后续重新生成绑定时需同步检查字段名称。
- 回滚：移除 RefreshTitle 方法及三个调用，保留用户已完成的 Prefab 重命名与组件调整，无需数据迁移。

## 2026-09-09 主页启动礼包关闭收回入口

### 需求、原行为与本次边界

- 用户确认主页启动默认打开的这次 ChestGiftPopup，主动关闭时缩向 StartPanel 的 ChestGiftEnter；普通入口打开保持原关闭，购买成功仍按原飞金币完成后直接关闭。
- 原路径为 LoadManager.TryToShowStartupChestGiftAsync → GiftManager.OpenStartupOnce → ChestGiftController.Open → ConfigureOffer；OnCloseClick 直接 GiftManager.Close → UIManager.DestroyUI。
- 本次仅给自动打开返回的实例注入目标，不新增会话存档或全局事件，也不改变 OpenStartupOnce 原有一次性资格、Loading 等待/超时、RemoteConfig、IAP 或奖励顺序。

### 影响文件、绑定与表现

- StartPanel.cs 新增 Inspector RectTransform chestGiftReturnTarget 与只读属性；StartPanel.prefab 绑定现有入口 RectTransform 7866297924960958713（DailyChestGiftEnter 嵌套实例，来源 GUID 58699936f8ce8f444b988bb52e202041）。原入口资格刷新逻辑未修改。
- LoadManager.cs 仅在 OpenStartupOnce 成功返回 ChestGiftPopup 后调用 ConfigureStartupCloseTarget；仓库中该方法只有这一处调用。
- ChestGiftPopup.cs 与母版 ChestGiftPopup.prefab 新增启动关闭内容 Root 和 Animator 的显式引用，分别为 7998628872607552264、4709850938803846306。资源原有修改保留，五个颜色 Variant 继续继承母版。
- 关闭时把入口 RectTransform 中心经实际 rootCanvas 的相机转换到弹窗父 RectTransform 世界坐标，避免直接混用 anchoredPosition。Root 在 0.3 秒内 DOMove 到入口中心并 DOScale 到零，均为 InBack、非缩放时间；Root 现有 pivot 为 (0.5,0.5)。入口缺失、失活、Canvas 或必要绑定缺失时直接原关闭。
- 开始前停用 Root Animator 并暂停奖励项入场，避免同一 Transform 被动画重复驱动。已绑定 ClickBlock 从 Root 移至弹窗全屏根节点，重新铺满并激活，保持全屏输入阻挡；只操作已有引用，不创建节点或按名称扫描。

### 生命周期、购买和遮罩

- _isClosingToEntry 阻止重复关闭和购买；仅主动 OnCloseClick 进入收回动画，购买成功仍走原 PlayPurchaseRewardAnimationAsync → GiftManager.Close。
- OnHide 暂停收回序列，OnShow 恢复；收回期间不恢复已暂停的 item 入场。OnClose/OnDestroy Kill 序列、清空本次目标与状态；完成回调核对 IsOpen 和视图版本后才销毁。原生覆盖导致 PlayerLoop 停止时 Tween 不推进，但该路径不发起平台支付，已有购买中门禁仍生效。
- UIManager 的黑幕是 Popup/Top 共享的私有遮罩，没有本弹窗独立淡出入口。实现阶段已说明：保留原遮罩释放流程，收回结束销毁 Popup 后由 UIManager 执行原淡出；未实现原方案中“同步淡出”，避免为局部表现修改共享遮罩所有权。全屏 ClickBlock 一直保留到销毁。

### 验证与剩余风险

- 三个 Prefab 引用定向校验通过；启动目标注入仅有一处调用，购买成功分支没有调用收回方法。定向 git diff --check 通过，未修改 GF、共享 UIManager 或生成绑定脚本。
- 常规工程此前缺少 Temp 构建依赖，本轮在系统临时目录 ChestGiftStartupClose-20260909 创建独立验证工程，包含原 Assembly-CSharp 的 1070 个源文件，沿用原编译常量/框架设置并引用 Library/ScriptAssemblies 的项目依赖。最终构建 0 error / 2014 warning；此为缓存依赖静态编译，不替代 Unity 导入和运行验证。初次验证工程脚本的空 ItemGroup 节点处理及隐式框架引用配置有误，修正后重新编译，早期空工程成功不计作证据。
- uncertain：未运行 Unity PlayMode 或 Android/iOS 真机。待验证自动弹出关闭落点、16:9/宽屏比例、点击过早时 Root Animator 中断、普通入口关闭、购买成功关闭、动画期间点击/上层弹窗遮挡/外部销毁。InBack 有短暂反向运动，视觉幅度和入口中心是否需要美术微调待实测；黑幕在收回后淡出而非同步。
- 修改前记录 git status，保留用户已有 StartPanel/ChestGiftPopup Prefab 与脚本、字体及其它面板修改。本次无需新增运行时资源或改变存档。

### 回滚

只移除 LoadManager 的启动目标注入、StartPanel 的目标字段/属性/单项绑定、ChestGiftPopup 的收回区域与对应生命周期/按钮门禁/两个母版绑定，恢复 OnCloseClick 的直接关闭。保留本次之前的标题、item 动画、多奖励配置及用户 Prefab 改动；不需要数据迁移。

## 2026-09-09 收回动画不可见排查（待运行证据）

- 用户反馈未看到缩放。facts：本地母版 Root/Animator 与 StartPanel 目标绑定仍存在；五个颜色 Variant 继承同一母版且未移除组件。Editor.log 同时包含 LoadManager.OpenStartupOnce 的自动打开栈与 DailyChestGiftEnter.OnEnterClick 的手动打开栈，不能仅凭后者认定用户操作了错误入口。
- direct conclusions：普通入口不注入收回目标，按当前约定走直接关闭；原代码所有动画前置条件失败均静默返回 false，旧日志不足以区分是哪条分支。
- 本轮仅在 ChestGiftPopup 的启动目标注入、各前置失败、动画开始/完成补充 Bear.Logger 日志，前缀 StartupClose；不改变入口范围、条件、时长、缩放或购买行为。hypotheses：未注入目标、运行态绑定缺失/目标失活或坐标转换失败仍待运行证据，未宣布已修复。
- 验证：缓存依赖源码编译 0 error / 2014 warning；Editor.log 确认 ConfigureStartupCloseTarget、TryCloseToStartupEntry 和完成回调已 Hot Reload。missing evidence：用户反馈对应的关闭分支与运行时缩放采样，需下一次自动弹出关闭日志；Unity 视觉及真机仍为 uncertain。
- 回滚：移除本节新增 StartupClose 日志即可，不影响原行为；先前动画实现不在本轮回滚范围。

## 2026-09-09 收回动画不可见修复

- 反馈后的运行日志没有出现 `StartupClose` 分支记录；日志证据不足以确认旧分支的具体失败条件。旧实现的直接风险是依赖本轮新加到 StartPanel 和 ChestGiftPopup Prefab 的三个序列化引用，当前 PlayMode 已存在的实例无法获得后来补入的 Prefab 引用，因此可能直接回退关闭。
- 当前实现移除 `StartPanel.chestGiftReturnTarget`、`ChestGiftPopup.startupCloseRoot/startupCloseAnimator` 及对应 Prefab 绑定。StartPanel 改为从 `OnOpen` 已取得的 `DailyChestGiftEnter` 返回其 RectTransform；弹窗直接移动并缩放自身根 RectTransform，不再依赖新序列化字段或颜色 Variant 的继承刷新。
- 启动自动打开仍是唯一调用 `ConfigureStartupCloseTarget` 的路径。主动关闭时把入口中心转换到弹窗父 RectTransform 的世界坐标，弹窗根以 0.3 秒 `InBack` 同时移动和缩放到零；普通入口打开和购买成功关闭不进入该分支。
- 关闭开始后暂停奖励项入场、禁用购买按钮和关闭按钮，并激活既有 ClickBlock。UIManager 的共享遮罩保持在 Popup 层，负责缩放期间继续阻挡主页输入；动画完成后才执行原 `GiftManager.Close`。
- 验证：独立验证工程包含 Assembly-CSharp 当前 1070 个源码文件并引用 Library/ScriptAssemblies 缓存依赖，构建结果 `0 error / 2011 warning`；`git diff --check` 通过。Editor.log 确认 `ConfigureStartupCloseTarget`、`TryCloseToStartupEntry` 和 `StartPanel.ChestGiftReturnTarget` 已 Hot Reload。
- `uncertain`：修改后的自动弹出关闭尚未获得新一轮目视确认；需重新产生一次启动自动弹出实例后关闭。当前已经关闭的旧 Popup 不会重新执行启动目标注入。Android/iOS 横屏落点与缩放仍待真机验证。
- 回滚：恢复三个 Inspector 引用及两份 Prefab 绑定，并把 Tween 目标改回内容 Root；数据、商品配置、购买和存档均无需迁移。

## 2026-09-09 启动关闭强制等待 Tween

- 用户复验后确认点击 Close 仍直接关闭，未等待动画。现有实现把“启动弹窗身份”和“入口目标有效”合并判断；目标为空、失活、Canvas 或坐标转换失败时 `TryCloseToStartupEntry()` 返回 false，`OnCloseClick()` 随即直接调用 `GiftManager.Close()`。
- 当前新增 `_useStartupCloseAnimation`，由启动自动打开路径的 `ConfigureStartupCloseTarget()` 无条件置为 true。只要属于本次启动弹窗，Close 就必须创建并等待 0.3 秒非缩放时间 Sequence 完成，完成回调之后才调用 `GiftManager.Close()`。
- 入口中心解析拆分到 `TryResolveStartupCloseEndPoint()`。解析成功时移动并缩放到 ChestGiftEnter；解析失败时保留当前位置并仍然缩放到零，不再退回直接关闭。普通入口没有启动标记，继续直接关闭；购买成功仍沿用飞金币完成后关闭。
- 动画开始前关闭购买和 Close 交互、激活既有 ClickBlock、暂停 item 入场；关闭期间不新增存档、配置、奖励或事件操作。失败仅影响视觉落点，不影响关闭完成。
- 验证：当前 1070 个 Assembly-CSharp 源文件配合 Library/ScriptAssemblies 缓存依赖构建 `0 error / 2011 warning`；定向 `git diff --check` 通过；Editor.log 确认相关方法已 Hot Reload。修改后的启动新实例目视结果仍为 `uncertain`，Android/iOS 横屏未验证。
- 回滚：移除 `_useStartupCloseAnimation` 和解析失败原地缩放分支，恢复目标无效时返回 false 的旧逻辑；无需迁移数据。

## 2026-09-09 启动关闭动画 PlayMode 复验

- 用户重新生成运行时实例后确认 Close 动画正常：启动自动弹出的 ChestGiftPopup 会等待缩放动画，界面不再直接消失。
- 早期失败发生在连续 Hot Reload 调整字段、Prefab 绑定与运行时状态期间；方法热更新成功并不等同于已存在 MonoBehaviour 实例完成重新序列化。此次结果与 Hot Reload 状态不完整一致，但旧实例当时的字段快照未留存，因此具体内部状态仍标记为 `uncertain`。
- 最终实现不依赖本次新增 Prefab 字段：StartPanel 复用已初始化的 DailyChestGiftEnter，ChestGiftPopup 使用自身 RectTransform，并以启动布尔标记保证 0.3 秒 Tween 完成后才关闭。该结构降低同类 Hot Reload 验证误差。
- 已移除“配置、开始、完成”三条临时普通诊断日志；保留根 RectTransform 缺失和入口坐标不可用的异常告警。缓存依赖编译与此前一致为 0 error；本次用户 PlayMode 复验只覆盖当前编辑器分辨率，Android/iOS 横屏仍未验证。
- 回滚路径保持上一节说明；若只撤销日志清理，可恢复三条 Bear.Logger 调试输出，不影响动画行为。

## 2026-09-09 购买成功接入通用奖励弹窗并移除自身飞金币

### 需求与原行为

- 用户要求 ChestGiftPopup 购买成功后弹出 CommonGetRewardPopup，并进一步明确移除 ChestGiftPopup 自身的飞金币效果。
- 修改前，ChestGiftController 已在购买成功回调中更新购买记录并逐项发放 Shop 奖励；ChestGiftPopup 随后使用自身 CommonUIRewardAnimCtrl 播放金币飞行动画，动画完成后直接关闭。
- Shop 奖励已经是有序 `List<ShopReward>`，当前礼包包含一项 Coin 和两到三项 Skin。不能转成 Dictionary 后只保留一个 Skin。

### 新流程与影响范围

- `ChestGiftPopup.cs` 移除自身 `rewardAnimCtrl`、金币动画数量阈值、`PlayPurchaseRewardAnimationAsync`、金币数量解析及动画数量解析。
- `ChestGiftPopup.prefab` 移除上述已废弃序列化字段；用户已从母版移除的 `CommonUIReward` 子 Prefab 保持移除状态，五个颜色 Variant 继续继承母版。
- 购买成功回调现在保持 `ClickBlock` 和购买门禁，使用当前 `Shop.Rewards` 打开 `CommonGetRewardPopup`，固定 `multiple=1` 且 `grantNormalReward=false`。
- `grantNormalReward=false` 表示奖励继续只由 ChestGiftController 原购买链入包；CommonGetRewardPopup 只展示已经发放的奖励，避免 Coin 和 Skin 重复入包。
- CommonGetRewardPopup 关闭回调再关闭 ChestGiftPopup。通用弹窗创建失败或 Shop 奖励列表为空时记录错误并直接关闭 ChestGiftPopup，不恢复或播放 ChestGiftPopup 飞金币。
- CommonGetRewardPopup 的关闭动画仍只检查 Coin；所有 Skin 只创建展示项，不参与飞行动画。

### 数据、UI 与平台边界

1. PurchaseManager 返回购买成功。
2. ChestGiftController 更新 PurchaseCache，并逐项把 Coin/Skin 发到 Bag 后通知 GiftManager。
3. ChestGiftPopup 打开 Top 层 CommonGetRewardPopup，传入原有有序 ShopReward 列表和预发奖标记。
4. 通用弹窗展示 Coin 与全部 Skin；关闭时只有 Coin 进入其既有飞行动画。
5. 通用弹窗 OnClose 回调关闭仍被输入锁定的 ChestGiftPopup。

未新增全局事件、存档字段、配置字段、运行时节点查找或原生支付调用。原生支付覆盖层结束并回到购买成功回调后才进入本 UI 流程；未改变购买期间的等待遮罩和回调线程边界。

### 验证、风险与回滚

- 使用缓存依赖的独立 Assembly-CSharp 验证工程编译当前 1070 个源码文件，结果 `0 error / 2010 warning`。
- 定向 `git diff --check` 通过；静态扫描确认 ChestGiftPopup 不再引用 CommonUIRewardAnimCtrl、动画阈值或 PlayPurchaseRewardAnimationAsync。
- 常规 `Assembly-CSharp.csproj` 构建仍被 `Temp/bin/Debug` 中约 200 个依赖 DLL 缺失阻断，该失败没有进入本次源码编译阶段。
- Unity Editor.log 显示 CommonGetRewardPopup 新重载与字段结构无法通过 Hot Reload 完整应用；本次必须退出 Play Mode 触发 Unity 全量脚本重编译后再做运行验证。
- `uncertain`：尚未在 Unity PlayMode 及 Android/iOS 横屏真机完成真实/编辑器模拟购买，需验证通用弹窗层级、全部皮肤图标、关闭回调、Coin 单次飞行动画、无 Coin 礼包直接关闭，以及外部销毁通用弹窗后的收口。
- 回滚时恢复 ChestGiftPopup 自身动画字段和购买成功动画方法，移除 CommonGetRewardPopup 调用及关闭回调；CommonGetRewardPopup 的 Shop 列表/多 Skin 支持可独立回滚。奖励与购买记录无数据迁移。

## 2026-09-09 ChestGiftPopup 与 StartPanel 定向整理

### 需求、基线与修改范围

- 用户要求继续优化 ChestGiftPopup，并包含 StartPanel 脚本。此次以不改变已确认的购买、奖励展示和启动收回表现为边界，只处理重复状态判断、失真命名与主页子入口运行时扫描。
- 修改前，ChestGiftPopup 的 `_isRewardAnimating` 实际表示“已打开并等待 CommonGetRewardPopup 关闭”，名称与职责不符；关闭和购买分别重复判断购买请求、奖励弹窗等待和启动收回三个互斥状态。启动收回时长 `0.3f` 在两个 Tween 中重复书写。
- 修改前，StartPanel 每次 `OnOpen()` 都用四次 `GetComponentInChildren(..., true)` 查找 DrawGame、IWannerGame、Postcard 与 DailyChestGift 入口。四个入口均已存在于 StartPanel.prefab，按项目约束应由 Inspector 显式绑定。

### 修改后职责与流程

- ChestGiftPopup 将状态改名为 `_isWaitingForRewardPopup`，并由 `IsInteractionLocked()` 统一判断购买请求、通用奖励弹窗等待与启动收回。关闭按钮仍先尝试启动收回，普通入口仍直接关闭；购买成功仍以 `grantNormalReward=false` 打开 CommonGetRewardPopup，关闭通用弹窗后再关闭 ChestGiftPopup。
- 启动收回时长提取为 `StartupCloseDuration=0.3f`，移动与缩放仍同时使用非缩放时间和 `Ease.InBack`。目标不可用时继续在当前位置完成缩放后关闭，未改变用户已确认的等待行为。
- StartPanel 新增四个私有序列化入口字段，StartPanel.prefab 分别绑定现有四个嵌套 Prefab 组件。`OnOpen()` 改为调用 `RefreshHomeEntries()` 集中刷新，不再做运行时层级扫描。
- `ChestGiftReturnTarget` 现在直接读取 Inspector 绑定的 DailyChestGiftEnter。自动启动链仍由 LoadManager 在打开成功后注入目标；普通入口、礼包资格、配置、存档、支付、奖励发放与全局事件均未修改。

### 验证、风险与回滚

- 静态校验确认四个字段引用、四个 stripped MonoBehaviour、来源组件 fileID 与各自 PrefabInstance 全部匹配；Unity Editor 已成功导入 StartPanel.prefab，未报告 YAML 或反序列化错误。
- 系统临时目录的完整 Assembly-CSharp 验证工程编译当前 1070 个源码文件，结果 `0 error / 2014 warning`；警告为项目既有依赖版本与未赋值字段告警。定向 `git diff --check` 通过。
- Editor.log 曾在字段名先改、引用后改的中间步骤记录 6 条瞬时 Hot Reload 编译错误，随后引用改名与方法热更新完成；最终源码以独立完整构建 0 错误为准。新增序列化字段不能依赖旧 PlayMode 实例验证，必须退出 PlayMode并完成全量脚本重编译后重新创建 StartPanel。
- `uncertain`：尚未在全量重编译后的 PlayMode 复验四个入口刷新、启动自动弹出关闭落点和购买成功奖励弹窗；Android/iOS 横屏仍未验证。
- 回滚时恢复 StartPanel.OnOpen 的四次运行时查找并移除四个字段及 Prefab 绑定；ChestGiftPopup 恢复原状态名、分散判断与两个 `0.3f` 字面量。无需迁移配置、存档或资源。

## 2026-09-09 ChestGift 跨日自动刷新

### 需求、基线与修改范围

- 用户确认 ChestGift 以设备当地自然日 `00:00` 作为唯一换日边界；弹窗未打开时，倒计时结束后应推进到新一天礼包，应用后台跨日返回后也应补刷新。
- 修改前 `ScheduleSystem.CheckAllModules()` 只在进入游戏和 GM 调试时调用。持续在线跨过当地午夜时，倒计时显示会进入新一天，但 `GiftData.ChestGiftDailyRefreshCount` 不会同步推进，下一次打开可能仍解析旧档位。
- 本次修改 `ScheduleSystem.cs`、`GameManager.cs`、`GiftManager.cs` 与 `DailyChestGiftEnter.cs`；未修改 Prefab、Luban 生成配置、商品、奖励、IAP、埋点或存档结构。

### 新行为与数据、UI 流

1. `GameManager.Update()` 调用 `ScheduleSystem.Tick()`；系统预约最近的设备当地午夜或 UTC 午夜，普通帧只比较 `Time.unscaledTime` 截止值。
2. 到预约日界线、60 秒校准点或应用恢复前台时才读取系统日期。日期未变化时不遍历 Schedule 模块、不读写存档；跨日时执行既有 `CheckAllModules()`。
3. 当地 Daily 通过原有 `GiftManager -> ChestGiftController.OnScheduleRefresh()` 增加并保存 `ChestGiftDailyRefreshCount`，随后发送局部 `AvailabilityChanged` 通知；已激活的入口据此刷新可售档位图标，下次创建 ChestGiftPopup 时重新解析当前档位。
4. ChestGift 倒计时固定计算到设备当地次日 `00:00`，与 Daily 换日共用同一边界；`DailyChestGiftDuration` 生成字段保留兼容但不再参与 ChestGift 倒计时。CoinGift 的 `DailyFirstPurchastGiftDuration` 行为不变。
5. `DailyChestGiftEnter` 因当日不可用而隐藏自身后继续保留局部资格通知订阅，在下一次 Daily 通知中可以重新激活并显示新档图标；组件销毁时统一退订，父 UI 隐藏期间不会丢失跨日状态。

### 验证、风险与回滚

- 已完成定向源码与调用链静态检查；普通帧只比较两个浮点截止值，系统时间读取限于日界线、60 秒校准与恢复前台，跨日仍复用原 Schedule 存档与异常隔离流程。使用 `Library/ScriptAssemblies` 缓存依赖补齐 Unity 生成项目的缺失输出后，`dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies` 结果为 `0 error / 123 warning`，warning 属于工程既有范围。
- `uncertain`：尚未在 Unity PlayMode 实测前台跨午夜、后台跨午夜返回、系统时区或日期手动前后调整，以及 Android/iOS 真机行为。已打开 ChestGiftPopup 在跨日瞬间继续持有打开时的 Offer 快照，本需求只确认弹窗未打开时刷新，打开态展示策略仍沿用现状。
- 若只回滚本轮优化，恢复 ScheduleSystem 每秒读取日期的门禁并移除 GameManager 的 Focus/Pause 校准回调。若整体回滚跨日功能，再移除 `GameManager.Update()` 的 Schedule Tick 与日期观察状态，把 ChestGift 倒计时恢复为读取 `DailyChestGiftDuration`，并恢复 `DailyChestGiftEnter.OnDisable()` 退订；无需迁移或回退 `GiftData`、`GameSchedule` 及购买记录。

## 2026-09-10 ChestGift 折扣角标接入 Shop 配置

### 需求、原行为与影响范围

- 用户发现 ChestGiftPopup 的折扣角标显示 `25% off`，要求接入当前 Shop 配置。
- 修改前母版 `ChestGiftPopup.prefab` 的 `u_shop_des_03_txt` 保留静态 `25%\noff`；`RefreshOffer()` 虽把 `Shop.Discount` 用于划线原价反算，却没有刷新角标文本。当前紫色档对应 Shop `20010`，运行时 `tbshop.json` 的 Discount 为 `75`，因此截图与配置不一致。
- 本次仅修改 `ChestGiftPopup.cs`，复用生成字段 `UShopDes03Txt` 与 `UIShop2Dazhe01Img` 的既有 Inspector 绑定；不修改 Prefab、Shop/Products 配置、支付价格、购买、奖励、Daily、存档、事件或平台 SDK。

### 修改后行为与数据/UI 流

- Offer 完整通过 Shop、Product 与 SKU 校验后，`RefreshOffer()` 调用 `SetDiscount(shop.Discount)`；非 0 值按普通 Shop/CoinGift 的既有格式显示为 `{discount}%\noff`。
- Discount 为 0 时隐藏折扣图片和文本并清空文字；`OnOpen()` 初始化或任意不可用 Offer 分支通过 `SetOfferUnavailable()` 同样清空并隐藏，避免 Prefab 静态文案或上一档状态残留。
- 划线原价继续由 `GiftPriceFormatter` 使用同一个 `Shop.Discount` 反算，现价继续来自平台商品元数据；新增逻辑不改变真实售价或扣款。

### 验证、剩余风险与回滚

- 静态检查确认母版两项生成绑定均非空，五个 Chest Shop 当前 Discount 均为 `75` 且在 1～99 范围；紫色资源通过 `ChestPrefab=ChestGiftPopup_purple` 对应 Shop `20010`。
- 使用 Unity Bee 响应文件执行 Unity Roslyn 运行时程序集编译，结果为退出码 0、0 error；目标脚本与文档差异检查通过。
- `uncertain`：尚未在 Unity PlayMode 和 Android/iOS 横屏实测五个颜色档位、Discount=0 的隐藏状态及长折扣文本排版。
- 回滚时移除 `SetDiscount()` 及 `RefreshOffer/SetOfferUnavailable` 两个调用点，即恢复 Prefab 静态角标；无需迁移配置、存档或购买记录。

## 2026-09-11 ChestGift 购买反馈对齐 Shop

### 需求、原行为与影响范围

- 用户确认平台购买成功回调即为成功反馈边界，要求 ChestGift 与 Shop 复用相同购买成功入口，并避免重复调用提示。
- 修改前 `ChestGiftController` 调用 `PurchaseManager.OnPurchaseSuccess(productName, showTips: false)` 更新购买记录，
  在同步交付 Coin/Skin 后又自行调用 `SystemTips.Show(U_HurryTips_Shop_succeed)` 和 `AudioManager.PlaySound(shopGetReward)`。
- 本次只修改 `ChestGiftController.cs`；不修改 Popup、Prefab、Shop/Products 配置、PurchaseManager、存档结构、奖励内容、
  Daily、打点或平台 SDK。

### 修改后行为与购买、奖励、UI 流

- ChestGift 成功回调改为只调用一次 `PurchaseManager.OnPurchaseSuccess(productName)`，由 PurchaseManager 与 Shop 共用的
  默认成功反馈显示 `U_HurryTips_Shop_succeed` 并播放 `shopGetReward`。
- 删除 ChestGiftController 后续手写的成功提示和音效，不新增反馈 API，也不会在同一次成功回调中重复显示或播放。
- `OnPurchaseSuccess` 仍先更新 `PurchaseCache`；ChestGift 随后在同一主线程回调、同一帧内同步执行原有 Coin/Skin 交付校验，
  再通知 GiftManager 并打开原通用奖励展示弹窗。没有新增异步等待、延迟或额外保存。
- 支付失败继续统一调用 `PurchaseManager.OnPurchaseFailed()`，断网与普通失败的现有提示分流不变。

### 验证、剩余风险与回滚

- 静态检查确认 ChestGiftController 仅剩一处 `OnPurchaseSuccess(productName)`，已无 `SystemTips.Show` 或
  `shopGetReward` 直接调用；购买记录、Coin/Skin 发放和 GiftManager 通知仍各保留一处。
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly
  /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，
  结果为 `0 error / 123 warning`；未建立本次修改前 warning 数量基线，不将 warning 归因于本次修改。
- 目标脚本 `git diff --check` 通过，仅有工作区 LF/CRLF 转换提示。
- `uncertain`：尚未在 Unity PlayMode、Android/iOS 沙盒验证成功提示、成功音效、Coin/Skin 单次到账、通用奖励弹窗、
  失败/取消和重复 SDK 回调；`SystemTips` 对象池耗尽或 Top 根节点缺失时仍可能不显示，需要运行日志确认。
- 回滚时把调用恢复为 `OnPurchaseSuccess(productName, showTips: false)`，并在奖励交付成功后恢复 ChestGiftController 的
  `SystemTips.Show` 与 `AudioManager.PlaySound` 两行；无需迁移配置、资源或存档。

## 2026-09-14 购买点击断网前置校验

- 旧版 `OnPurchaseClick()` 在断网时也先打开 ClickBlock，再等待控制器和 SDK 的购买结果；平台购买界面未拉起且无回调时，手动关闭 WaitingPopup 不能释放该遮罩，也不会显示网络提示。
- 当前在开启 ClickBlock 之前检查 `Application.internetReachability`；已报告无网络时直接调用既有 `PurchaseManager.OnPurchaseFailed()` 显示 `U_NoNetWork_Tips_01`，不调用 `ChestGiftController.TryPurchase()`，控制器 `_isPurchasing` 保持未开始。
- 复核发现冷启动无网且 SDK 价格尚未取得时，旧按钮门禁会先禁用购买按钮，使上述提示分支无法到达。现在配置、SKU、限购均有效且 PurchaseManager 存在时，无网状态允许点击以显示提示；联网状态仍必须支付已初始化、商品元数据及本地化现价有效才可点击。划线原价仍按原门禁显示，不会凭空生成价格。
- 礼包配置、奖励、动画、存档、事件和平台购买回调没有修改。静态顺序检查、Unity 运行时程序集编译与 `git diff --check` 通过；iOS 26 真机断网提示与点击后切网仍待验证。回滚时删除 `OnPurchaseClick()` 的断网分支及 `RefreshOffer()` 的离线按钮例外即可。

## 2026-09-14 礼包皮肤恢复复核

- 当前五档 `gift_pack_1～5` 是 Possessive 商品，Shop `20008～20012` 每档均含 Coin 与多个 Skin；配置中的 14 个 Skin ID 在 `tbskin.json` 中均能唯一匹配。
- 购买成功时 ChestGiftController 在购买记录更新后同步发放 Coin 与全部 Skin；平台资产恢复时 PurchaseManager 依据商品名找到同一 Shop，跳过 Coin，按 Skin ID 幂等补回 OwnedSkin 并保存。关闭 ChestGiftPopup 不承担发奖；本次未修改其购买或恢复回调。
- 恢复成立的前提是商店后台以永久商品返回对应平台资产，且本地配置仍保留 SKU、商品名与有效 Skin ID。Android/iOS 沙盒购买、重装后恢复和多次恢复尚未验证；回滚本次 NoAds 商品切换无需修改 ChestGift。

## 2026-09-14 真机点击无反馈：购买按钮门禁与 Shop 对齐

- 用户反馈同一真机上 ChestGiftPopup 购买按钮点击没有反馈，而 ShopItem 点击能显示 `SystemTips` 购买失败。此前只核对 `OnPurchaseClick()` 的断网分支，漏查回调前的按钮可交互条件。
- 原 `ChestGiftPopup.RefreshOffer()` 只有 SDK 已初始化且拿到有效商品详情与价格时才启用按钮，例外仅覆盖 `Application.internetReachability == NotReachable`。设备虽无法取价但系统仍报告 Wi-Fi/蜂窝可达时，按钮被禁用，`OnPurchaseClick()` 完全不会执行。相同情况下 ShopPanel 的商品按钮只按售罄和忙碌态控制，不以价格详情禁用。
- 现在 ChestGift 的配置、SKU、奖励及限购校验通过且 PurchaseManager 存在时即允许点击；有效原价仍仅在 SDK 价格就绪时显示。点击后的已知无网分支继续在 ClickBlock 前调用 `OnPurchaseFailed()`；公共 `Purchase()` 找不到商品对象时会通过失败回调进入控制器的 `OnPurchaseFailed()`，Popup 随后释放 ClickBlock。若商品对象存在但价格为空，公共流程仍可能发起平台请求，不能承诺立即提示；该路径与 ShopItem 一样依赖平台结果回调。
- 影响文件为 `ChestGiftPopup.cs`；Prefab 的购买按钮与 ClickBlock 已在母版显式绑定，五个颜色档位复用母版。未修改 ShopItem、PurchaseManager、SDK、商品表、存档或奖励。
- 同场景链路复核：

| 场景 | ShopItem / ShopPanel | ChestGiftPopup / 控制器 | 结论 |
| --- | --- | --- | --- |
| 本地商品有效、设备报告无网 | 按钮可点，`TryToPurchase()` 在加锁前调用 `OnPurchaseFailed()` | 本次改动后按钮可点，`OnPurchaseClick()` 在 ClickBlock 前调用同一提示 | 静态路径已对齐；真机未验证 |
| 设备报告有网、SDK 商品对象不存在 | 不因价格禁用，公共 `Purchase()` 找不到商品时回调失败并显示提示 | 本次改动后按钮可点，经同一公共失败回调提示并恢复 ClickBlock | 静态路径已对齐；真机未验证 |
| 商品对象存在但价格为空 | ShopItem 不以价格为空禁用按钮，公共流程可能发起平台请求 | 本次改动后 ChestGift 同样可点并进入公共流程 | 静态点击门禁已对齐；平台是否回调仍为 `uncertain` |
| 商品详情有效、购买结果失败 | Shop 回调清 `_isPurchasing` 并提示 | 控制器回调清 `_isPurchasing`、提示，Popup 关闭 ClickBlock | 代码路径已核对；平台回调时序未验证 |
| WaitingPopup 被关闭且 SDK 始终不回调 | 面板仍保持购买锁，关闭重开会重置其局部锁 | 控制器仍保持购买锁，关闭重开 Popup 也不会重置控制器 | 仍不一致且均可能无法继续点击；本次不把弹窗关闭当成交易失败 |
| PurchaseManager 缺失或本地商品配置无效 | 管理器缺失时取价路径未做空值保护；无效映射可能保留原按钮状态，点击只记录错误 | 管理器缺失或 Offer 无效时按钮禁用 | 启动/配置异常路径仍不一致，不属于已验证的有效商品场景 |

- 使用当前 Unity `NetCoreRuntime/dotnet.exe` 与 `DotNetSdkRoslyn/csc.dll` 重新编译运行时程序集，0 error、110 warning；源码顺序与 Prefab 绑定静态检查、`git diff --check` 通过。iOS 26 真机的按钮点击、不可取价提示、价格随后恢复、支付成功及 WaitingPopup 取消后 SDK 不回调仍未验证，均为 `uncertain`。此修复不处理“已有商品详情但平台请求挂起”的取消解锁问题。
- 回滚时将 `RefreshOffer()` 按钮条件恢复为 `canPurchase || canShowOfflineTip`，并将 `RestorePurchaseInteraction()` 恢复为仅校验 Offer；这会重新出现取价失败且系统仍报告网络可达时的无反馈。

## 2026-09-14 Daily Chest 开关云控补链

- 需求与原行为：用户先前要求将 Luban `isOpenDailyChest` 补入 Guru 配置；原字段只存在于 Luban 本地配置，`GiftManager.IsOpenDailyChest` 直接读取 `GlobalConst`，远端 `level_config` 无法控制主页入口、启动自动弹出或显式打开。此前的本地开关功能仍成立，但云控接入未完成。
- 本次影响 `LubanGenerator.cs`、`guru_spec.yaml`、`GuruAllSpecs.G.cs`、`ConfigManager.RemoteConfig.cs` 与 `GiftManager.cs`；不改 Prefab、ChestGift 商品/奖励、Daily Schedule、存档和事件。`level_config.isOpenDailyChest` 使用布尔值，运行时优先远端，再回退 Luban；远端缺失、解析失败或类型不符时回退，Luban 未就绪时维持开启。远端 `false` 统一隐藏入口并拒绝打开，远端 `true` 可覆盖本地 `false`。
- 数据流：Luban Global JSON → 生成器白名单 → Guru YAML/生成默认值 → 既有 RemoteConfigService 的 `level_config` 缓存 → `ConfigManager.RemoteConfig.GetIsOpenDailyChest()` → `GiftManager.IsEntryAvailable()` / `OpenInternal()`。此次未修改 RemoteConfigService 的拉取、超时或缓存逻辑。
- 验证：静态检查三处配置键及类型一致、YAML 与生成 C# 的完整 `level_config` 默认文本一致、消费入口统一；项目缓存的 Newtonsoft.Json DLL 对布尔/整数/字符串/缺字段进行了类型转换检查，只有布尔值通过类型门禁。`git diff --check` 通过。整程序集 `dotnet build` 因缺少 `Temp/bin/Debug` Unity DLL 报 `CS0006`；Unity Editor 菜单再生成、PlayMode 与 Android/iOS 远端 `true/false`、缺字段和错误类型实测均为 `uncertain`。
- 回滚：移除 Guru 默认键和生成器白名单、RemoteConfig Getter，恢复 `GiftManager` 的本地读取；不需迁移存档或资源。

## 2026-09-14 恢复购买后跳过对应 ChestGift 档位（需求与实现前现状）

- 用户要求：iOS 在 ShopPanel 手动恢复、Android 自动恢复 `gift_pack_N` 后，对应的 ChestGift `giftIndex=N` 应按已购档永久跳过。后续从当天 Daily 档位开始选礼盒时继续寻找下一档未购商品；五档都已购时不再展示普通 ChestGift 入口。恢复不应增加 Daily 刷新计数、标记“今天购买过”，也不重复补发礼包金币。
- 当前代码已具备下一次选档的跳过条件：`PurchaseManager.TryRestorePurchase()` 在有效 Skin 权益处理后写入 `DB.GameData.PurchaseCache[productName]`；`ChestGiftOfferResolver.HasRemainingPurchase()` 用该商品名的购买次数与 `Shop.BuyTimes` 比较；`ChestGiftController.TryResolveCurrentOffer()` 从 Daily 档位起循环寻找仍可购买的档位。当前五档 `gift_pack_1～5` 对应 Shop `20008～20012`，各档 `BuyTimes=1`。因此，只有礼包资产确实映射并成功走到 `PurchaseCache` 写入后，后续重新选档才会跳过对应 index；平台未返回礼包资产、补发校验失败或落盘失败时不能宣称永久跳过。
- 跳档结论仅覆盖普通入口与启动自动弹窗使用的无指定档位选档。`GiftManager.Open(ChestGift, variantIndex)` 的显式档位路径调用 `TryCreatePreview()`，可在未全档已购时打开指定的已购档；当前调用点在 GM 预览/跨天测试。若产品要求显式档位入口也一律跳过，当前代码不满足，须另行处理该预览例外。
- 实现前的即时 UI 缺口：`SyncRestore()` 只派发 `OnRestoreSuccessEvent`，当时 `StartPanel` 的监听只刷新 NoAds；`DailyChestGiftEnter` 只响应 `GiftManager.AvailabilityChanged` 或显式 `Refresh()`。恢复后若主页入口仍在屏上，图标/显隐可能保持旧状态，直到页面或入口再次刷新；普通入口点击会重新调用 `GiftManager.Open()` 选档。

## 2026-09-14 Android/iOS 恢复后刷新 ChestGift 入口

- 本次只修改 `Assets/Game/UI/StartPanel/Scripts/StartPanel.cs`：现有 `OnRestoreSuccessEvent` 监听在刷新 NoAds 后，调用 Inspector 已绑定的 `dailyChestGiftEnter.Refresh()`。没有新增事件、单例提前初始化、Prefab 绑定、平台分支、配置或存档字段。Android 的 `OnRestored()` / Loading 和 iOS 的 ShopPanel 手动恢复均复用 `PurchaseManager.SyncRestore()`；只有实际新增 Skin/永久权益或 `PurchaseCache` 时，该方法派发一次恢复成功事件。
- `DailyChestGiftEnter.Refresh()` 重新查询 `GiftManager.IsEntryAvailable()` 与 `ChestGiftController.TryResolveCurrentIndex()`。若仍有未购档，入口图标改为下一可售 gift index；若五档都已购，`TryResolveCurrentOffer()` 返回 false，入口 `SetActive(false)`。Android 启动恢复早于主页事件订阅时，`StartPanel.OnOpen()` 原有 `RefreshHomeEntries()` 仍会以最新 `PurchaseCache` 刷新。恢复不推进 `GiftData.ChestGiftDailyRefreshCount` 或 `LastChestGiftPurchaseLocalDay`，也不补发 Coin。
- 静态复核当前 `gift_pack_1～5` 分别唯一映射 Shop `20008～20012`，五档 `BuyTimes=1` 且 ChestPrefab 非空；`StartPanel.prefab` 的 `dailyChestGiftEnter` 序列化引用非空，事件订阅与解除位置未变，`git diff --check` 通过。`dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies` 因 `Temp/bin/Debug` 中大量 Unity 元数据 DLL 不存在而失败；当前 Bee `Assembly-CSharp.rsp` 又引用已移除的源码，故本次未取得可靠的整程序集编译结果。
- `uncertain`：Android/iOS 真机平台资产是否返回五档礼包、实际 Skin 与 `PurchaseCache` 是否落盘、恢复后当场图标切档、五档全购隐藏及重启持久性均未做设备验证。若恢复未产生任何权益变化，`SyncRestore()` 不派发成功事件；主页每次 `OnOpen()` 仍会重新计算入口。回滚时从 StartPanel 恢复事件回调移除 `dailyChestGiftEnter?.Refresh()` 并恢复原回调命名即可；礼包选档和恢复发奖不需回滚。

## 2026-09-14 ChestGift 跨日购买归属与实际档位轮换修复

- 需求：跨当地零点仍打开的弹窗点击购买，购买弹窗原来显示的礼包；新一天入口按当天分配的礼包及其商品名购买记录判断。存在两档以上可售礼包时，连续两个当地自然日不得因已购跳档而展示同一档；仅剩一档未购时允许持续展示该档直至购买。同一天重复打开不推进档位；当天分配礼包正常购买完成后隐藏当天普通入口，恢复购买只跳过对应已购档，不计作当天购买。
- 修改前：`ChestGiftController.NotifyDelivered()` 将支付回调当天写入 `LastChestGiftPurchaseLocalDay` 并以此全局隐藏入口；`ResolveIndex(DailyRefreshCount)` 每天生成固定起点后才跳过已购档，可能连续两天落在同一可售档。弹窗商品快照没有所属日期，旧弹窗在跨日后仍可发起旧档支付。
- 修改范围：`GiftData.cs`、`GiftData_Partial.cs` 保存当天分配日、实际档位、商品名、分配时 Daily 次数及已完成的商品名；`ChestGiftController.cs` 负责分配、轮换及入口资格；`GMDailyScheduleTestController.cs` 的跨日调试回退分配日并走普通礼包入口。先前自行添加的跨天弹窗关闭与“购买归属旧日”逻辑已由文末同日回归修正撤销。未修改 Shop/Products、Prefab、支付 SDK、奖励配置、ScheduleSystem、远端配置、CoinGift 或 GF。
- 新状态流：`GiftManager` 的 Daily 回调仍只增加 `ChestGiftDailyRefreshCount` 并发局部资格通知。正常入口首次查询当天商品时，从昨天**实际分配档位的下一档**循环查找配置有效且 `PurchaseCache` 未达 `BuyTimes` 的礼包，保存当天档位与商品名；同日继续使用该记录。云控关闭导致多个 Daily 回调未查询入口时，以分配时记录的 Daily 次数补足轮换步数。当天恢复购买使已分配商品售罄时，同日从其后一档重选；普通购买完成后不重选。仅一档可售时自然重复，零档时隐藏。
- 支付流：Offer 只携带弹窗显示的 Shop/Product 商品快照。成功后仍由 `PurchaseManager` 按商品名写 `PurchaseCache`，奖励交付顺序不变；仅当完成的商品名等于**今天已分配的商品名**时，写入完成商品名并隐藏入口。两者不同时不隐藏新礼包；未查询入口前跨日完成旧礼包时，下次选档按购买缓存跳过已购商品。恢复购买不写完成商品名；GM 购买若恰好是当天分配商品，也按同一商品名规则处理。
- 存档兼容：`GiftData.DataVersion` 仍为 2；既有 v1 存档的新字段默认为空，首次选档由原 Daily 计数作为起点，之后依据实际分配档位轮换。旧 `LastChestGiftPurchaseLocalDay` 仅为历史迁移字段，不再控制入口；上一提交保存的 `chestGiftCompletedOfferLocalDay` 从序列化结构移除，旧存档该字段不再参与业务判断。升级首日仍由当前 `PurchaseCache` 与 Daily 起点建立分配记录，无法还原旧版本未保存的历史分配商品，此点为 `uncertain`。
- 验证：静态核对普通入口、GM、恢复购买、支付成功/失败及跨日迟到回调链。用当前 `GuruAllSpecs.G.cs` 在临时目录先编译 Guru 生成程序集，再用当前 `Assembly-CSharp.csproj` 源文件列表和 Unity Roslyn 引用编译完整运行时程序集，两个编译均为 0 error；目标文件 `git diff --check` 通过。直接使用旧 Bee 缓存 Guru 引用会出现四处与本次改动无关的 `Products.ProductName.NewnoAds` 缺失错误，不能把旧缓存结果当作本次回归。
- `uncertain`：尚未在 Unity PlayMode 和 Android/iOS 真机验证零点前后成功、失败/取消、后台跨日、恢复购买、单档/全购、重启持久性与旧存档升级。保存失败后重启仍可能重建当天分配记录；现有 `PurchaseCache` 先于全部奖励写入的失败补发风险未在本次逻辑修复中处理。回滚时恢复控制器原计数起点与回调日门禁、Popup 原点击路径及 GM 跨日入口，移除 `GiftData` 新字段或将其作为未使用兼容字段保留；保留既有永久购买记录与支付奖励数据。

## 2026-09-14 跨日旧弹窗点击误关闭回归修正

- 用户实测：跨过当地零点后，在仍打开的 ChestGiftPopup 点击购买，界面被直接关闭，没有进入购买。源码直接原因是上一版在 `ChestGiftPopup.OnPurchaseClick()` 发现 Offer 不是今天分配档位后调用 `GiftManager.Close(this)`；即使去掉关闭，`ChestGiftController.IsOfferAvailable()` 的同一日门禁仍会阻断平台请求。用户随后明确确认：此时应购买弹窗原来显示的礼包。
- 本次移除上述关闭和当前日门禁；`IsOfferAvailable()` 继续用 `ChestGiftOfferResolver` 校验弹窗商品的配置、SKU、奖励与 `PurchaseCache/BuyTimes`。同时撤销自行添加的 Offer 打开日及完成展示日记录：入口只在当天分配商品名与已完成商品名一致且购买缓存有记录时隐藏。旧礼包成功后若当天已分配的是另一商品，不会把新入口隐藏；若当天尚未分配，后续选档从 `PurchaseCache` 跳过已购商品。原有离线检查、点击锁、平台请求、成功/失败回调仍在原链路。
- 静态场景核对：①零点前打开 A、零点后未支付才点购买，Popup 向购买管理器提交仍显示的 A，点击路径没有日期关窗；②A 支付期间换日且当天已分配 B，A 完成只写 A 的购买缓存，不把 B 标记完成；③当天分配 B 并购买 B，完成商品名与分配商品名一致且缓存为已购，当天入口隐藏；④当天 B 被恢复购买占满次数但没有正常购买完成记录，解析器从 B 后重选；⑤支付失败或取消不写完成商品名，弹窗恢复点击状态。这些是源码路径核对，不是 PlayMode 或真机实测。
- 影响范围：`GiftData.cs`、`GiftData_Partial.cs`、`GMDailyScheduleTestController.cs`、`ChestGiftPopup.cs`、`ChestGiftController.cs`；无新 Prefab、事件、支付 SDK、奖励配置或云控修改。静态复核点击前不再调用 `GiftManager.Close(this)`，`TryPurchase()` 不再以日期拒绝。`uncertain`：Unity PlayMode 和 Android/iOS 真机跨零点点击、支付成功/失败、旧弹窗关闭后新入口点击及旧存档升级仍待验证。回滚应以提交前版本作整体回滚，避免只恢复日期门禁。

## 2026-09-15 ChoiceLevelPopup 恢复购买后刷新 ChestGift 入口

- 需求与原行为：主线、DrawGame、IWanner 的选关界面都嵌套共享 `DailyChestGiftEnter.prefab`，但共享入口只监听 `GiftManager.AvailabilityChanged`。`PurchaseManager.SyncRestore()` 在实际补齐权益后派发的是既有 `OnRestoreSuccessEvent`，不会触发该局部资格通知；因此选关界面保持打开时，恢复后的 ChestGift 图标或全档已购显隐不会即时更新。主页 `StartPanel` 已有单独刷新，不代表其他宿主也已覆盖。
- 本次只修改 `DailyChestGiftEnter.cs`：运行时首次启用时订阅既有 `OnRestoreSuccessEvent`，收到事件后调用原 `Refresh()`，重新读取 `PurchaseCache` 驱动的可售档位。仍有档位时切换为下一可售图标，五档均达到 `Shop.BuyTimes` 时隐藏入口。入口可能因资格判断隐藏自身，因此与既有 Daily 资格监听一致，不在 `OnDisable()` 解除恢复监听；只在 `OnDestroy()` 统一释放，重复启用不会重复订阅。
- 事件与状态边界：iOS 手动恢复和 Android 自动恢复仍由 `PurchaseManager.SyncRestore()` 决定是否派发事件；只有 `changedCount > 0` 的实际权益变化会即时刷新。没有变化的恢复不会派发事件，页面后续启用时仍由 `OnEnable()` 的原刷新读取最新状态。没有新增事件、存档字段、配置、资源或 Prefab 改动，也不推进 Daily 计数、不标记当天正常购买、不补发金币。
- 兼容与回滚：主页原有 `StartPanel` 显式刷新暂时保留，因此主页在有效恢复时可能执行两次幂等 `Refresh()`；选档和存档结果不变，避免为本修复扩大已上线主页改动范围。回滚时移除 `DailyChestGiftEnter` 的恢复事件订阅、回调和销毁释放即可，主页原刷新仍可独立工作。
- 验证：静态核对共享入口生命周期、`changedCount > 0` 的恢复事件派发条件，以及母版被 11 个宿主复用且覆盖三个 ChoiceLevel 的 Prefab 关系；使用 Unity 6000.0.59f2 Roslyn 对目标脚本定向编译为 0 error、2 个 Inspector 序列化字段未在源码赋值的预期 warning，目标差异 `git diff --check` 通过。`uncertain`：Unity PlayMode、iOS 手动恢复、Android 自动恢复、入口打开期间切档、五档全购隐藏、父面板关闭期间收到事件及重启持久性尚未做真机验证。

## 2026-09-20 新玩家首次游玩主线胜利自动弹出

- 需求：`autoShowChestGiftLevel` 当前本地默认值为 `7`；新玩家首次游玩时首次通过该主线排序关卡，在胜利界面之上自动打开 `ChestGiftPopup`。关卡值同时支持 Luban 本地默认值与 `level_config` 云控覆盖。
- 门禁修正：原实现使用 `DB.GameData.PlayDays == 1`，会覆盖第一个当地自然日内的多次启动，不等于“首次游玩”。`LoadManager` 每次启动都会先递增并保存 `PlayCount`，工程既有首次启动逻辑也使用 `PlayCount == 1`；当前入口因此改为 `DB.GameData.PlayCount == 1`。同日第二次启动不会触发，首次启动跨当地零点仍保持首次游玩资格。
- 动画复播根因：胜利页和自动 ChestGift 原来都位于 Bear UI 的 `Popup` 层。打开同层 ChestGift 时，`UIManager` 会对胜利页调用 `OnHide()`；销毁 ChestGift 后再对同层栈顶胜利页调用 `OnShow()`。`BaseUIView.OnShow()` 会重置并播放 `IUIAnimation`，奖励版胜利页的 `OnShowExtra()` 还会再次播放分享区和按钮组进场，因此玩家会看到胜利动画复播。
- 当前 UI 流：只有首次游玩胜利专用入口使用 `UILayer.Top` 打开 ChestGift；主页、选关和 Loading 等普通入口继续使用 `UILayer.Popup`。Bear UI 各层使用独立栈，`Top` 不会隐藏 `Popup` 层胜利页；现有共享遮罩由 `Top` 优先持有，关闭后归还 `Popup`，不需要修改胜利页动画或 Bear UI 源码。
- 运行时流程：`PlayCtrl_Success` 在推进关卡前保存本次完成序号，创建胜利面板并完成通关存档后调用 `GiftManager.TryOpenFirstPlayVictoryChestGift()`。其余门禁继续要求本关首次通关、完成关卡等于云控值、`isOpenDailyChest=true`、当前没有其它 GiftPopup，并保留当天档位、全档已购、商品/SKU、限购和 Resources 校验。
- 影响范围：`PlayCtrl_Success.cs`、`GiftManager.cs`、`ChestGiftController.cs`。未新增存档字段、全局事件、Prefab、资源、奖励、购买、广告、RemoteConfig 拉取或平台代码；普通 ChestGift 入口、胜利页实现、Daily 轮换及购买交付不变。
- 验证：静态场景核对覆盖首次启动目标关首次通关、同日二次启动、首次启动跨日、重复通关、非目标关和普通 ChestGift 入口。使用当前 Unity Bee `Assembly-CSharp.rsp` 与 Unity 6000.0.59f2 自带 Roslyn 编译完整运行时程序集到临时目录，结果为 `0 error`；输出 warning 均为工程既有诊断。标准 `dotnet build` 仍因 `Temp/bin/Debug` 缺少 Unity 元数据 DLL 报 `CS0006`，不是本次源码诊断。
- 未验证风险：尚未用干净新存档完成 PlayMode 首次启动第 7 关、同日重启、跨零点、关闭 ChestGift 后胜利页动画和 Android/iOS 横屏真机验证，运行时视觉结果仍为 `uncertain`。胜利页会在 Top 弹窗遮挡期间继续推进原动画，关闭礼包后应处于动画已推进的状态，而不是重新播放。
- 回滚：把首次游玩门禁恢复为旧条件、专用 ChestGift 层级恢复为 `Popup`，并恢复旧方法名即可回到本次修正前行为；配置字段、普通入口和存档无需回滚。若要整体移除功能，再同步删除主线 SUCCESS 入口、RemoteConfig Getter、LubanGenerator 白名单及默认载荷。

## 2026-09-20 首次胜利弹窗成功后解锁普通入口

- 需求：新玩家首次游玩通过云控指定关卡（当前默认第 7 关）并成功打开 ChestGift 后，立即解锁完整 ChestGift 普通入口逻辑，效果等同 ChestGift 已进入第二次启动；只影响 ChestGift，不得把全局 `PlayCount` 改成 2。
- 原行为：首次到达主页时，`ConfirmHomeReached()` 已把 `GiftData.HasReachedGiftHome` 写为 `true`，但同时以 `_suppressThisSession=true` 隐藏整个首次会话的普通入口。胜利专用 `OpenForFirstPlayVictory()` 只绕过该门禁打开一次 Top 弹窗，没有解除当前会话抑制，因此关闭弹窗后主页和选关入口仍保持隐藏，直到玩家真正第二次启动。
- 当前行为：只有 `OpenOffer(..., UILayer.Top)` 返回真实弹窗后，控制器才调用局部解锁逻辑；将既有 `HasReachedGiftHome` 置为 `true`、把本会话标记为已确认主页并清除 `_suppressThisSession`，随后通过原 `AvailabilityChanged` 通知所有 `DailyChestGiftEnter` 刷新。首次胜利弹窗打开失败、全档已购、商品无效、Daily Chest 关闭或不满足首次通关门禁时均不会解锁。
- UI 与存档流：首次胜利弹窗仍在 Top 层，不改变胜利页动画边界；入口可在其后面的主页/选关中恢复显示，弹窗关闭后可走普通 Popup 层再次打开。跨启动资格复用现有 `GiftData.HasReachedGiftHome`，仅字段原为 `false` 时补存一次；不新增存档版本、字段、全局事件、资源、配置、购买或奖励逻辑，不修改 `GameData.PlayCount` 及其它首次启动功能。
- 影响文件仅为 `ChestGiftController.cs` 与本记录、当日共享记录。Unity 6000.0.59f2 当前 Bee `Assembly-CSharp.rsp` 全量编译退出码为 0；源码静态检查确认解锁调用位于弹窗非空分支，设置三项会话/持久资格后走既有局部入口通知。
- `uncertain`：尚未用干净新存档完成正式 PlayMode 的“首次启动 → 首次通过第 7 关 → 弹窗出现 → 关闭后主页/三类选关入口显示 → 同日重启自动入口”全链，也未验证保存失败注入、Android/iOS 横屏与真机购买。回滚只需移除 `UnlockEntryAfterFirstPlayVictoryOpen()` 及其成功打开后的调用，即恢复“本会话继续隐藏、第二次启动才解锁”的原行为。

## 2026-09-23 OneLink 自动跳转跳过启动 ChestGift

- 需求：应用由 OneLink 唤醒并存在待处理的自动跳转时，不显示 Loading 启动序列中的 `ChestGiftPopup`，避免礼包阻塞目标关卡跳转。
- 原行为：启动序列依次处理公告、旧数据转换、ChestGift 和 OneLink 派发；只要 ChestGift 具备资格就会先打开，OneLink 必须等待礼包关闭后才能继续。
- 当前行为：`LoadManager.TryToShowStartupChestGiftAsync()` 仍等待 StartPanel 首次进场完成并确认页面开启；随后检查 `OneLinkManager.HasPendingRequest`。存在已解析请求、待解析系统 URL 或自动解析中的请求时，记录跳过日志并结束 ChestGift 步骤，序列立即进入 OneLink 派发步骤。
- 边界：只跳过本次启动自动弹窗，不调用 `OpenStartupOnce()`，因此不消耗其会话内自动展示记录；主页入口、GM/显式打开、首次胜利礼包、Daily 轮换、购买、奖励、存档和云控资格均不改变。公告与旧数据转换仍按原顺序处理，也不会主动关闭已经打开的 ChestGift。
- 验证：恢复 Unity 临时 NuGet 资产后执行完整 `Assembly-CSharp.csproj` 编译，结果为 `0 error / 1030 warning`；warning 未建立本次修改前基线，不归因于本次门禁。静态调用链确认跳过后下一步仍执行 `NotifyStartupPopupsDispatched()`。
- `uncertain`：尚未在 Android/iOS 真机验证 OneLink 冷启动、UDL 回调晚到、普通启动仍自动展示礼包及跳转完成。若链接在 ChestGift 已经打开后才到达，本次逻辑不会强制关闭弹窗，OneLink 继续沿现有阻塞门禁等待。
- 回滚：删除 `TryToShowStartupChestGiftAsync()` 中的 `HasPendingRequest` 分支即可恢复原启动礼包顺序；无需迁移 Prefab、配置或存档。
