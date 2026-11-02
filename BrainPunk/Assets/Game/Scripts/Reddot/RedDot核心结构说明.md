# RedDot 核心结构说明

## 需求背景

本次新增一套可复用红点核心结构，目标是让 UI 上的红点 prefab 只通过 Inspector 选择 `RedDotKey`，业务侧通过统一的 `RedDotManager`、`IRedDotRegister` 和 `IRedDotChecker` 注册节点关系与显示判断。

初始版本只实现核心结构；2026-07-06 已追加 `RedDotKey.Postcard` 和对应 checker；2026-07-16 已追加 `NewSkin` 总节点、四个 `SkinPart` 子节点、统一查询入口和分支刷新能力，仍不修改任何现有 UI prefab。

## 影响文件与系统

- `Assets/Game/UI/Global/Reddot/RedDotKey.cs`
  - 定义红点节点枚举，目前包含核心 `None`、业务 `Postcard`、`NewSkin` 及四个 NewSkin 部位子节点。
- `Assets/Game/UI/Global/Reddot/IRedDotRegister.cs`
  - 定义业务注册器、业务判断器和注册入口接口。
- `Assets/Game/UI/Global/Reddot/PostcardRedDotRegister.cs`
  - 注册 `RedDotKey.Postcard` 节点和明信片业务 checker。
- `Assets/Game/UI/Global/Reddot/PostcardRedDotChecker.cs`
  - 读取配置表中最后一个未完成的普通明信片配置、`UnlockProgress` 和背包 Star 数量，判断是否存在可消除的明信片部件。
- `Assets/Game/UI/Global/Reddot/NewSkinRedDotChecker.cs`
  - 只读判断 `GameData.WaitingPreviewSkinId.Count` 是否大于 `0`。
- `Assets/Game/UI/Global/Reddot/NewSkinRedDotQuery.cs`
  - 集中提供全部新皮肤、指定 `SkinPart`、指定 `skinId` 的只读查询，以及 NewSkin 分支刷新入口。
- `Assets/Game/UI/Global/Reddot/NewSkinPartRedDotChecker.cs`
  - 根据皮肤配置的 `SkinPart` 判断对应部位是否仍有待预览皮肤。
- `Assets/Game/UI/Global/Reddot/NewSkinRedDotRegister.cs`
  - 注册 NewSkin 总节点、四个固定部位子节点及其 checker。
- `Assets/Game/UI/Global/Reddot/RedDotManager.cs`
  - 维护 `Dictionary<RedDotKey, bool>` 状态、父子关系、checker 和 UI 监听。
- `Assets/Game/UI/Global/Reddot/RedDotRegisterHub.cs`
  - 提供集中注册并刷新一组业务注册器的辅助入口。
- `Assets/Game/UI/Global/Reddot/RedDotView.cs`
  - 运行时 UI 组件，挂在红点 prefab 上，通过 Inspector 暴露 `RedDotKey` 和显示节点引用。

本次不新增存档字段或配置，不修改 RemoteConfig、资源加载、广告、IAP、analytics、平台 SDK、现有关卡或现有 prefab 绑定。待预览列表仍沿用既有写入与保存流程，仅在列表实际变化后同步刷新红点分支。

## 原有行为基线

该目录下没有已落地的可复用红点核心脚本。现有 UI 不会因为本次新增脚本自动出现、隐藏或刷新红点。

## 新行为

- 业务模块后续可实现 `IRedDotRegister`，通过 `RegisterNode(...)` 注册红点节点和父子关系。
- 业务模块可通过 `RegisterChecker(...)` 为某个节点注册 `IRedDotChecker` 或 `Func<bool>`。
- `RedDotManager.Refresh(key)` 会把节点自身 checker 与全部子节点当前状态按“或”聚合。
- `RedDotManager.RefreshBranch(root)` 会先刷新全部后代节点，再聚合根节点并同步祖先链。
- `RedDotManager.Set(key, bool)` 支持业务直接设置叶子节点状态，并自动刷新父节点聚合。
- `RedDotView` 只在 PlayMode 运行时订阅 `RedDotManager`，非 PlayMode 不修改显示节点 active 状态，避免污染 prefab 编辑状态。

## 数据、UI、事件与平台流

- 数据：核心状态只在内存中维护 `Dictionary<RedDotKey, bool>`，不新增 DB、PlayerPrefs 或 Save 字段；`PostcardRedDotChecker` 只读取 `DB.Postcard`、配置表和背包 Star 数量，`NewSkinRedDotChecker` 只读取现有 `GameData.WaitingPreviewSkinId`。
- UI：`RedDotView` 在 `OnEnable` 订阅，在 `OnDisable` 取消订阅；显示状态通过 `redDotRoot.SetActive(...)` 切换。
- 事件：本次不新增 `PlayEvents` 或 Bear 全局事件，使用 `RedDotManager` 内部 C# callback 处理 UI 订阅。
- 平台：纯 C# 和 Unity UI 组件逻辑，不包含 Android/iOS 平台分支。

## 使用边界

- `RedDotKey` 后续只追加枚举值，不复用或改动已上线 key 的数值。
- `IRedDotChecker.IsOn()` 只读取业务状态，不应发奖、写存档、打开 UI、请求广告/IAP 或加载资源。
- 业务注册应在运行时数据准备完成后调用；不要在 `OnValidate`、`Reset`、prefab 预览或编辑器窗口中触发。
- UI prefab 上使用 `RedDotView` 时，需要在 Inspector 选择具体 `RedDotKey`，并绑定 `redDotRoot` 或保证第一个子物体就是实际显示节点。

## 验证记录

- 已确认当前 `RedDotKey` 只保留核心 `None`，没有具体业务 key。
- 已执行 `dotnet build .\Assembly-CSharp.csproj -v:minimal /clp:ErrorsOnly`，结果 0 error / 98 warnings。
- 初次 `--no-restore` 编译因 `Temp/obj/Unity.Rider.Editor/project.assets.json` 缺失失败，允许 restore 后编译通过。
- 2026-07-06 追加 Postcard checker 后复跑 `dotnet build .\Assembly-CSharp.csproj -v:minimal /clp:ErrorsOnly`，结果 0 error / 97 warnings。
- 2026-07-06 `git diff --check -- Assets/Game/UI/Global/Reddot DevelopMD/已有功能设计与使用记录.md Assembly-CSharp.csproj` 已通过，仅提示 `DevelopMD/已有功能设计与使用记录.md` 后续会按 Git 设置进行 LF/CRLF 转换。

## 未验证风险

- 尚未在 Unity Editor 中把 `RedDotView` 挂到实际红点 prefab 验证 Inspector 展示和运行时显隐。
- 已接入 `PostcardRedDotRegister` / `PostcardRedDotChecker`，但尚未在 Unity PlayMode 验证真实注册时机、UI prefab 绑定和运行时刷新。
- 尚未执行 Android/iOS 横屏 smoke。

## 回滚路径

删除 `Assets/Game/UI/Global/Reddot/` 下本次新增的核心脚本、`PostcardRedDotRegister.cs`、`PostcardRedDotChecker.cs` 和说明文档，并移除后续 prefab 上可能新增的 `RedDotView` 组件引用，即可回到本次新增前状态。

## 2026-09-04 DailyGiftCoin 接入

- `RedDotKey` 追加 `DailyGiftCoin = 120`，表示 ShopPanel 每日免费金币当前可领取。
- `DailyGiftCoinRedDotRegister` 在 Loading 的红点初始化阶段注册独立叶子节点和 `DailyGiftCoinRedDotChecker`。
- checker 只读取已初始化的 `ConfigManager`、`GameSchedule.DailyFreeCoinLastClaimTime` 和 `GlobalConst.DailyFreeCoinCount`；资格口径为当前时间已经跨过上次领取所在设备当地自然日的下一天 00:00。
- ShopPanel prefab 中 DailyCoin 自带的 `RedDotView` 已绑定 key 120；领取成功及卡片运行时跨日恢复时，由独立控制器调用 `RedDotManager.Refresh(RedDotKey.DailyGiftCoin)`。
- checker 与卡片共用 `DailyGiftCoinClockGuard` 和 `GameSchedule.DailyFreeCoinLastObservedTime`：运行中忽略设备墙钟跳变，跨会话时间回拨时保持红点关闭，避免红点状态绕过领取资格。
- 本接入不新增 `PlayEvents` 或 Bear 全局事件，不在 checker 中写存档、发奖励、加载资源或创建运行时单例。
- 主程序集定向编译通过（0 error / 122 warning）；Unity PlayMode、设备时间回拨、Android/iOS 当地时区与横屏表现仍为 `uncertain`。

## 2026-07-06 Postcard Checker 接入

### 需求背景

`RedDotKey.Postcard` 表示当前明信片推进口径下存在可以消除的 `postcardItem`。判断入口不按 `DB.Postcard.CurrentId` 当前浏览页取配置，而是遍历 `TbPostcardConfig.DataList`，按 `Sort` 升序选择最近一个尚未写入 `DB.Postcard.CompletedId` 的普通明信片配置，并结合 `UnlockProgress` 和玩家当前 Star 数量决定红点显隐。

### 行为说明

- `PostcardRedDotRegister` 注册 `RedDotKey.Postcard` 节点，并绑定 `PostcardRedDotChecker`。
- `PostcardRedDotChecker.IsOn()` 仅在 PlayMode 返回真实判断结果；非 PlayMode 直接返回 false。
- 判断前会确认 `DBManager`、`ConfigManager` 和 `PlayCtrl` 已存在且初始化完成，避免红点刷新过早时创建新的运行时单例对象。
- 所有普通明信片配置都已存在于 `DB.Postcard.CompletedId` 时返回 false。
- `Postcard_unlock` 这类 `unlockProgress`、`rewards` 为空且 `rewardMultiple <= 0` 的占位配置会被跳过，不参与红点判断。
- `PostcardConfig.UnlockProgress` 中任一条进度满足以下条件时返回 true：
  - progress key 不在 `DB.Postcard.CurrentCollectedPuzzledId` 中。
  - 玩家当前 `GameProps.Star` 数量大于等于该 progress value。

### 数据与事件流

- 读取数据：`DB.Postcard.CompletedId`、`DB.Postcard.CurrentCollectedPuzzledId`、`ConfigManager.Tables.TbPostcardConfig.DataList`、`PlayCtrl.Instance.Bag.GetToolCount(GameProps.Star)`。
- 写入数据：无。
- 事件：无新增 `PlayEvents` 或 Bear 全局事件；红点状态仍由业务调用 `RedDotManager.Refresh(RedDotKey.Postcard)` 或 `RedDotRegisterHub.RegisterAndRefresh(...)` 刷新。
- 资源与平台：不加载资源，不接入 RemoteConfig、广告、IAP、analytics 或平台 SDK。

### 2026-07-09 口径调整

- 需求：优化 `PostcardRedDotChecker`，检查目标 config 改为当前仍未 complete 的最后一个普通明信片 config，避免用户浏览已完成页后 `CurrentId` 影响红点结果。
- 影响文件：`PostcardRedDotChecker.cs`；不修改 prefab、DB 字段、Luban 配置、资源路径或平台 SDK。
- 行为变化：`TryGetPostcardData(...)` 不再要求 `DB.Postcard.CurrentId` 非空；checker 会从配置表尾部向前找未完成且非占位的 config，再按原有 `UnlockProgress` / `CurrentCollectedPuzzledId` / Star 数量规则判断红点。
- 验证：已执行静态敏感扫描，未新增编辑器生命周期、每帧轮询、全局事件、资源加载、广告、IAP、analytics、PlayerPrefs 或写存档路径；`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，99 warnings / 0 errors；Unity PlayMode、Android/iOS 横屏仍待补验。
- 剩余风险：`CurrentCollectedPuzzledId` 仍是现有存档中的当前收集列表；若线上数据出现多个普通 config 同时未完成，需要 PlayMode/存档样本验证该列表与“最后未完成 config”的对应关系。
- 回滚：恢复 `PostcardRedDotChecker.IsOn()` 为按 `DB.Postcard.CurrentId` 取 `TbPostcardConfig.GetOrDefault(...)` 并判断当前配置即可，不需要数据迁移。

### 剩余风险

- 尚未在 PostcardPopup 或其它实际 UI 初始化流程中注册 `PostcardRedDotRegister`。
- 尚未验证消除 `postcardItem` 后刷新 `RedDotKey.Postcard` 的调用时机。
- Android/iOS 横屏和 Unity PlayMode 未验证。

### 2026-07-14 按 Sort 升序查找最近未完成页面

- 需求背景：原查找方法从 `TbPostcardConfig.DataList` 尾部返回首个未完成配置，`out` 结果受 JSON 原始行顺序影响，可能与 `PostcardPopup` 按 `Sort` 升序推进的页面顺序不一致。
- 影响文件与系统：仅修改 `PostcardRedDotChecker.cs` 的只读目标配置选择，并更新本说明；不修改 prefab、Luban 配置、DB 字段、存档写入、资源、事件、analytics 或平台 SDK。
- 原有行为：倒序遍历 `DataList`，遇到第一个非占位且 `Id` 不在 `CompletedId` 的配置就立即返回。
- 修复后行为：遍历全部普通配置，排除 `CompletedId` 已完成项，选择 `Sort` 最小的未完成配置；这里“最近”明确指按页面升序推进时最先遇到的未完成页。`Sort` 相同时按 `Id` 的字符串序选择靠前配置，避免结果依赖配置表行顺序。
- 数据流：继续只读 `DB.Postcard.CompletedId`、`DB.Postcard.CurrentCollectedPuzzledId`、`TbPostcardConfig.DataList` 和背包 Star 数量；`CurrentId` 仍只表示当前浏览页，不参与红点目标选择。
- 验证：已执行目标 diff 检查、C# 编译和静态敏感关键字扫描；Unity PlayMode、真实存档样本、Android/iOS 横屏仍需补验。
- 剩余风险：`CurrentCollectedPuzzledId` 是现有存档中的当前收集列表；异常存档若出现已完成页不连续，需要使用真实存档样本验证它与 `Sort` 最小未完成页面的对应关系。
- 回滚：恢复从 `DataList.Count - 1` 倒序并在首次命中时立即返回的实现，不需要数据迁移。

## 2026-07-16 NewSkin Checker 接入

### 需求背景

新增 `RedDotKey.NewSkin` 和 `NewSkinRedDotChecker`。最终确认的显示口径为：现有 `GameData.WaitingPreviewSkinId` 只要还有任意数据就显示红点，列表为空时隐藏。

### 影响文件与系统

- `Assets/Game/UI/Global/Reddot/RedDotKey.cs`：追加 `NewSkin = 102`，未复用或修改已有枚举值。
- `Assets/Game/UI/Global/Reddot/NewSkinRedDotChecker.cs` 及 `.meta`：新增按待预览列表数量判断的只读 checker。
- 只读取 Bear SaveModule 的 `GameData.WaitingPreviewSkinId`；未修改该字段、存档写入、皮肤授予、UI prefab、资源、事件、analytics 或平台 SDK。

### 原有行为基线

- `RedDotKey` 只有 `None = 0` 和 `Postcard = 101`。
- 红点模块没有检查 `WaitingPreviewSkinId` 是否非空的 checker。
- `RedDotManager` 的 checker 接口为无参 `IsOn()`，每个 key 只保存最后一次注册的 checker。
- 本次口径调整前，工作区中的初版 `NewSkinRedDotChecker` 需要构造函数传入 `skinId`，并使用 `Contains(skinId)` 判断单个皮肤。

### 新行为

- `NewSkinRedDotChecker` 不再保存或接收 `skinId`，可直接使用无参构造。
- `IsOn()` 仅在 PlayMode、场景中已有且已初始化的 `DBManager`、`GameData` 和待预览列表均有效时执行数量判断。
- `WaitingPreviewSkinId.Count > 0` 时显示红点；列表为 `null` 或数量为 `0` 时隐藏。
- 本次不新增 `NewSkinRedDotRegister`、初始化调用或 prefab 绑定，不会自动改变现有 UI 红点。

### 数据、资源、事件与平台流

- 数据流：`DBManager.Get<GameData>()` -> 只读 `WaitingPreviewSkinId.Count` -> 返回红点状态。
- 写入数据：无；不调用 `Save()`，也不移除待预览 ID。
- 资源与事件：不加载资源，不新增 `PlayEvents`、Bear 全局事件或每帧轮询。
- 平台：纯运行时 C# 判断，无 Android/iOS 分支。

### 验证记录

- `git diff --check` 已通过；仅有 Git 的 LF/CRLF 工作区转换提示。
- 口径调整后的临时隔离校验直接引用本次 `NewSkinRedDotChecker.cs`，6/6 场景通过：非运行态、无 DBManager、DBManager 未初始化、列表为 `null`、列表为空、列表非空。
- 主工程 `dotnet build .\Assembly-CSharp.csproj --no-restore ...` 被当前 `Temp/bin/Debug` 缺失 191 个 Unity/插件元数据 DLL 阻断，未进入本次业务代码编译诊断。
- 静态检查确认 checker 未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、写存档、资源加载、全局事件、广告、IAP 或 analytics 路径。

### 剩余风险与未验证项

- 尚未执行 Unity PlayMode 和 Android/iOS 横屏验证。
- 当前没有 NewSkin 注册器和 UI 绑定；接入方仍需在运行时数据初始化后注册并主动刷新。
- `NewSkin` 当前表示全局“存在任意待预览皮肤”状态；所有绑定该 key 的视图会共享同一结果，不提供单个皮肤条目级状态。

### 回滚路径

- 删除 `NewSkinRedDotChecker.cs` 及 `.meta`，并从 `RedDotKey` 删除未接入的 `NewSkin = 102`，即可恢复原红点枚举与判断器集合；不需要存档迁移。
- 如果只回滚本次口径调整，可恢复 `_skinId`、带参构造函数和 `WaitingPreviewSkinId.Contains(_skinId)` 判断。

## 2026-07-16 NewSkin 分支与统一刷新入口

### 需求背景

原有 NewSkin 只有一个总节点，能够表示“待预览列表是否非空”，但无法让 Skin 页按 `SkinPart` 订阅独立红点。新增分支结构后，业务只需调用 `NewSkinRedDotQuery.Refresh()`，即可同步 NewSkin 总节点和全部部位子节点。

### 影响文件与系统

- `RedDotKey.cs`：新增 `NewSkinBack = 110`、`NewSkinFace = 111`、`NewSkinBody = 112`、`NewSkinFullBody = 113`，未修改已有枚举值。
- `RedDotManager.cs`：新增 `RefreshBranch(...)`；节点计算改为“自身 checker 或任一子节点为 true”。
- `NewSkinRedDotQuery.cs`、`NewSkinPartRedDotChecker.cs`、`NewSkinRedDotRegister.cs`：集中查询待预览数据、按配置解析 SkinPart，并注册固定分支。
- `SimpleBag.cs`：新增或成功移除待预览皮肤 ID 后调用统一刷新入口。
- `SkinItem.cs`：单个列表项通过 `NewSkinRedDotQuery.Contains(skinId)` 判断自身红点。
- `ShopPanel_SkinPart.cs`：移除重复的总节点直接刷新，复用 `SimpleBag.RemoveFromNewSkinList(...)` 内部的统一刷新。

### 原有行为与新行为

- 原有 `RedDotManager.Refresh(key)` 在节点有 checker 时忽略子节点，节点无法同时承担自身业务判断和父级聚合职责；现在节点结果为自身 checker 与全部直接子节点状态的逻辑或。
- 原有业务需要分别刷新具体 key；现在 `RefreshBranch(NewSkin)` 使用后序遍历，先刷新四个部位子节点，再计算 NewSkin，总节点完成后继续同步可能存在的祖先节点。
- NewSkin 总节点仍严格按 `WaitingPreviewSkinId.Count > 0` 判断；部位子节点会把待预览 ID 映射到 `TbSkin` 配置后比较 `SkinPart`。
- 未能映射到配置的待预览 ID 不会点亮任何部位子节点，但仍会按列表非空规则点亮 NewSkin 总节点。

### 数据、存档、UI 与事件流

- 新增皮肤：`SimpleBag.GetNewSkin(...)` -> 去重写入 `OwnedSkin` 和 `WaitingPreviewSkinId` -> `NewSkinRedDotQuery.Refresh()` -> 子节点 checker -> NewSkin 聚合 -> 沿用既有保存与 analytics 流程。
- 预览皮肤：Skin item 点击 -> `SimpleBag.RemoveFromNewSkinList(skinId)` -> 成功移除并 `GameData.Save()` -> `NewSkinRedDotQuery.Refresh()` -> 当前 item 再读取 `Contains(skinId)` 隐藏自身红点。
- 查询入口只读取场景中已存在且已初始化的 `DBManager`、`ConfigManager`，不会触发单例创建、资源加载或额外存档写入。
- 未新增 `PlayEvents`、Bear 全局事件、每帧轮询、RemoteConfig 或平台分支；UI prefab 仍由接入方选择对应 `RedDotKey`。

### 验证记录

- CodeGraph 与生成代码核对确认 `Config.TbSkin.GetOrDefault(string)`、`Config.Skin.SkinPart` 的类型和访问方式一致。
- 新增三个脚本均已被当前 `Assembly-CSharp.csproj` 纳入编译项；新增 `.meta` GUID 唯一。
- `git diff --check` 通过；敏感关键字扫描未发现新增编辑器生命周期、全局事件、资源加载、PlayerPrefs 或每帧刷新入口。
- 主程序集 `dotnet build ... --no-restore` 在进入业务代码诊断前，被现有 `Temp/bin/Debug` 缺失 191 个 Unity/插件元数据 DLL 阻断；按要求未创建临时隔离工程或 Temp 校验文件。

### 剩余风险与回滚

- 尚未执行 Unity PlayMode、真实 Skin UI 节点绑定、异常待预览 ID、Android/iOS 横屏 smoke；这些仍属于未验证项。
- 若回滚本次扩展：移除四个子 key、`NewSkinRedDotQuery`、`NewSkinPartRedDotChecker` 和 `NewSkinRedDotRegister`，恢复 `RedDotManager` 原单节点刷新逻辑，并把 `SimpleBag`、`SkinItem` 调用点恢复为原实现。无需存档迁移或资源回滚。

## 2026-09-08 RedDotView 多条件或绑定

### 需求、基线与影响范围

- 需求：一个红点视图支持多个条件，任意条件满足即显示。
- 原基线：`RedDotView` 仅序列化一个 `redDotKey`，启用订阅、禁用取消，回调直接应用单节点状态。复用管理器现有状态查询和订阅接口即可，不需要新增业务 checker、全局事件或共享 UI 抽象。
- 实际代码位置为 `Assets/Game/Scripts/Reddot/RedDotView.cs`；本文早期条目的 `Assets/Game/UI/Global/Reddot/` 是历史路径。
- 现有引用分布于 GamePlayPanel、DrawGame_GamePlayPanel、StartPanel、ShopPanel、ChoiceLevelPanel、PostcardEnter 六个 prefab。保留原 `redDotKey`、`redDotRoot` 字段及脚本 GUID，不修改 prefab、管理器或生成绑定。旧配置附加列表为空时仍按单 key 显示。

### 新行为与使用

- Inspector 的 `Additional Red Dot Keys` 与原 `Red Dot Key` 一起按或聚合；`None` 和重复项忽略，无有效条件时隐藏。
- 运行时使用 `view.BindAny(RedDotKey.NewSkin, RedDotKey.DailyGiftCoin)` 替换全部条件；数组会复制，外部后续修改不会影响绑定。空参数或 null 等价于解除绑定。
- `Bind(key)` 替换为单条件并清除附加条件；`Unbind()` 清除全部条件。禁用时 Bind 只保存条件并隐藏，启用后恢复订阅。原代码在禁用时对相同 key 调用 Bind 会读取并显示状态，本次统一为禁用时不显示的明确行为。
- 使用订阅快照清理旧 key；批量订阅关闭立即回调，完成后统一刷新，避免逐个订阅过程中错误显隐。每次通知重新读取全部已订阅状态，任意为 true 即短路显示，避免最后一个回调覆盖或结果。
- 运行时 Inspector 修改条件需要重新启用组件，或通过 Bind/BindAny 生效。不新增 OnValidate 或编辑器数据链。
- 继续复用 RedDotManager 的内存状态和 callback，仅切换 redDotRoot；不新增存档、资源、配置、打点或平台调用，也不新增层级查找。显示节点应在 Inspector 显式绑定独立子物体。
- 无 Update 轮询；状态刷新按条件数量短路查询，不为每次刷新创建集合。Android/iOS 共用同一路径，无布局改动。

### 验证、风险与回滚

- 已核对管理器 Subscribe 的立即回调参数、IsOn 的未注册/None 返回 false 语义，以及六个 prefab 的现有序列化字段。git diff --check 通过。
- `dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false -v:quiet /clp:ErrorsOnly` 失败：Temp/bin/Debug 缺少 Apple.Core.dll 等依赖，共 200 个 CS0006 错误，尚未获得主程序集编译通过证据。下一步在 Unity 完成依赖导入后编译。
- CodeGraph CLI 不可用；规则指定的 ../specs/037-remote-resource-loading/plan.md 缺失，本次依据本地模块文档和脚本核对。
- uncertain：未执行 PlayMode 和 Android/iOS 横屏验证。待验证：双 false 隐藏、任一 true 显示、两者 true 后关闭其中之一仍显示、全部关闭隐藏、重复/None/空条件、运行时重绑、单条件覆盖多条件、禁用后启用、解绑后旧 key 不再驱动视图，以及上述六个 prefab 原单条件回归。
- 风险规避重点是旧 prefab 单 key 序列化兼容、回调覆盖和订阅残留；真实 UI 生命周期效果仍待补验。
- 回滚条件：出现旧单条件显隐回归或订阅残留时，撤回 RedDotView 本次变更；新增调用方需恢复 Bind(key)，清除新增附加列表配置。本次未修改 prefab 或业务存档，无数据迁移。
