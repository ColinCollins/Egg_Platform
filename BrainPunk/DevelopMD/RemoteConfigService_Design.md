# RemoteConfigService 设计说明

## 当前有效补充：2026-10-12 Dig 独立配置

本次事后复核覆盖 Global→LubanGenerator→Guru默认值→RemoteConfig拉取/缓存→getter和消费者，确认 Dig 第二轮代码已接入，修正本说明旧的三key文字；既有章节继续约束 ForceFetch/Loading/Draw，本节补充同样边界下的 Dig。

`diggame_config` 已注册到启动及已有SDK回调的统一 key 列表。初次 Luban/Dig 配置初始化前沿现有链提交缓存，随后 DigGameLevelCtrl.Init 读取；运行时新增候选独立验证，当 Dig 广告、暂停或转场尚未完成时保留最新 pendingDigConfig，不换当前缓存/排序。由既有 GameManager.Update 在安全边界调用 TryApplyPendingDigConfig；保存成功才同步 ApplyRemoteConfig 和刷新已有 Dig 选关/首页，同排序保留版本，活动关卡数据/墨水/统计现场不重建。拒绝候选或保存失败保留旧缓存/序列，不影响其它 key 独立保存。

14个字段及level_sort的生成器入口、常量/API和消费者已对应闭合，详见 LubanGenerator功能说明的当前章节；远端参数缺字段/解析失败回退对应Dig本地值。Dig与Draw/IWanner一样不要求Main的currentRemoteConfigVersion字段；JSON根、排序ID/类型/重复/非空/真实映射另行校验。

授权为用户此前确认的游戏侧读取/缓存/延迟提交范围：ForceFetchMaxAttempts=1、自动等待5秒、后台条件强制10秒、原启动顺序与只读SDK边界不变。本轮复核没有修改RemoteConfigService运行时代码，补充27项生成器纯方法验证并复查51项缓存/getter/统计隔离断言，均不证明Firebase服务端已发布或设备拿到最新值；这些平台环节仍为uncertain。

证据与逐字段清单：`DevelopMD/验证记录/DigGame选关云控_2026-10-12/RemoteConfig与LubanGenerator闭环复核.md`。回滚复核仅恢复本次说明/提示增量，不能删掉已接入的Dig运行消费者或改变启动ForceFetch。

## 1. 文档目的

本文档描述 `RemoteConfigService` 在 BrainPunk 项目中的当前职责、启动链路、回退策略、包体升级策略、缓存边界与已知风险。

本文档只描述当前仓库内代码已经体现出来的行为，不把理想设计写成现状事实。

当前相关文件：

- `Assets/Game/Scripts/SDK/Services/Remote/RemoteConfigService.cs`
- `Assets/Game/Scripts/SDK/Services/Core/GameSDKService.cs`
- `Assets/Game/Scripts/SDK/SDKImps/GameRoot.cs`
- `Assets/Game/Scripts/Config/ConfigManager.RemoteConfig.cs`
- `Assets/Game/UI/Loading/Scripts/LoadManager.cs`
- GuruSDK 提供的 `RemoteConfigManager` 依赖实现（当前仓库不再维护 `Packages` 源码覆盖）
- `Assets/Game/DB/GameData_Partial.cs`

## 2. 模块职责

`RemoteConfigService` 是游戏侧对 Guru SDK / Firebase Remote Config 的业务层封装，当前职责有五类：

### 2.1 管理启动期需要拉取的 remote key

当前统一维护在：

- `RemoteConfigService.RemoteConfigKeys`

当前正式 key 包含：

- `level_config`
- `drawgame_config`
- `iwannergame_config`
- `diggame_config`

### 2.2 提供统一加载入口

当前对外主要入口：

- `UpdateRemoteConfigsAsync()`
- `UpdateRemoteConfigsAsync(IReadOnlyList<string> keys)`
- `UpdateCustomConfigAsync()`
- `UpdateRemoteConfigsForLoadManager()`

其中：

- `UpdateRemoteConfigsAsync()` 是默认入口，直接使用 `RemoteConfigKeys`。
- `UpdateRemoteConfigsAsync(keys)` 是核心实现。
- `UpdateCustomConfigAsync()` 当前只是兼容旧入口，内部仍复用 `level_config`。
- `UpdateRemoteConfigsForLoadManager()` 只是适配 `LoadManager` 的 `IEnumerator<float>` 风格。

### 2.3 统一处理回退与本地落盘

当远端拉取失败、超时、SDK 状态不可用，或远端 JSON 未通过当前版本校验时，`RemoteConfigService` 负责从：

1. `DB.GameData.RemoteConfigCache`
2. `GuruSdk.Instance.RemoteConfigSpec.DefaultValues`

中选择可用值，并统一写回：

- `ConfigManager.RemoteConfig.TrySetRawBatch(...)`（检查保存结果）

### 2.4 向业务层屏蔽底层 SDK 细节

业务层不应直接依赖：

- `RemoteConfigManager.Instance.GetString(...)`
- `DB.GameData.RemoteConfigCache`

业务侧应统一通过：

- `ConfigManager.RemoteConfig.TryGetRaw(...)`
- `ConfigManager.RemoteConfig.TryGetValue<T>(...)`
- `ConfigManager.RemoteConfig.GetXXX()`

来读取配置。

### 2.5 持续接收 SDK 已有配置通知

GameSDKService 在 GuruSdk.Initialize 前调用 RegisterRemoteConfigCallback，复用 SDK 已有 ObserveConfigGetter。
跳过订阅首条回放，后续仅处理 IsActive 且 LastFetchStatus=Success 的 getter；读取四个 key，检查 JSON 对象根和主线版本约束后提交缓存。
启动清缓存前先暂存有效值，清理和默认值写入后再提交；Loading 超时后仍保持订阅，应用退出时释放。
已有通知也可能携带 Prepare 激活的历史缓存，不能把收到通知直接称作本次强制请求访问服务端成功。详见第 23 节。
## 3. 启动链路与前置条件

### 3.1 启动时序

当前主启动链路里，`LoadManager` 的顺序是：

1. `EnterGame()`
2. `HotReload()`
3. `LoadConfigAsync()`
4. `OnPostProcess()`

其中与 RemoteConfig 直接相关的关键前置条件有两个：

- `EnterGame()` 先调用 `booster.Init()` 启动 SDK 异步初始化，随后执行 `DBManager.Instance.Initialize()`；不能仅凭调用顺序保证 SDK 回调时 DB 已就绪。服务在 Loading 清缓存阶段前暂存有效结果，该阶段正常主链的 DB 已可用。
- `HotReload()` 会等待 `booster.IsReady`；而 `booster.IsReady` 来自 `GameSDKService.InitService(...)` 完成回调，因此 `GuruSdk` / `RemoteConfigSpec` 在正常启动链上通常也已经准备好。

### 3.2 RemoteConfig 阶段

`LoadManager.LoadConfigAsync()` 当前顺序是：

1. `RemoteConfigService.TryToClearRemoteCache()`
2. `RemoteConfigService.UpdateRemoteConfigsForLoadManager()`
3. `ConfigManager.Instance.InitializeAsync(...)`

设计含义：

- 先决定是否清理旧版本残留缓存。
- 再尝试拿到本次启动可用的 remote 配置来源。
- 最后初始化配置表和业务层读取入口。

### 3.3 前置条件边界

`RemoteConfigService` 当前不是无条件安全入口，以下前置条件成立时行为才可靠：

- `DB.GameData` 已初始化。
- `DB.GameData.RemoteConfigCache` 可用，或允许由 `ConfigManager.RemoteConfig.SetRaw(...)` / `SetRawBatch(...)` 延迟创建。
- `GuruSdk.Instance.RemoteConfigSpec` 已可用，才有机会使用 spec 默认值 fallback。

在当前主启动链上，这些前提基本成立；在主流程外单独复用时，需要自行确认。

## 4. 拉取流程与成功判定

### 4.1 SDK 正常拉取与业务强制拉取

`GuruSdk.Initialize(...)` 的异步初始化阶段会调用：

- `RemoteConfigManager.Instance.Initialize()`
- `FetchAndActivate()`

`RemoteConfigService` 当前将两阶段分开：Loading 只等待 SDK 自动结果最多 5 秒，失败或超时先采用缓存/默认值；自动结果返回后，后台仅在失败或 Success 沿用历史 FetchTime 时强制一次，单独等待最多 10 秒。自动新鲜成功不强制。自动永不返回就不启动强制；自动晚于 Loading 返回仍可启动后台阶段。GM 手动入口保留额外强制及自身 5 秒预算。

当前仓库不再跟踪 `Packages/com.guru.sdk.framework.core` 下的 `RemoteConfigManager.cs` 补丁覆盖；本地嵌入包已恢复为 GuruSDK 原版文件。
原版的普通拉取和强制拉取共用 `isFetchAndActivating`：已有请求运行时，新请求直接记录 `skipped: already loading` 并返回，不进入排队。
初始化使用 30 秒原生超时和 Deployment 最小缓存间隔；`ForceFetch()` 临时设置为 20 秒超时、最小间隔 0，并调用同一拉取入口；恢复联网入口仍会触发普通拉取。
第 21 节记录的是曾纳入仓库的排队补丁及其历史验证，第 36 节记录该补丁的移除。源码编译只能证明当前工作区可编译，发布包仍需用实际构建与真机日志核对。

游戏侧保留公开 ForceFetch、最多一次强制尝试、临时活动 getter 等待和 ForceFetch 的 FetchTime 新鲜度检查。
长期缓存订阅不依赖 Loading 是否仍在等待，不新增 SDK 事件或结果 API；当前 5 秒自动等待与 10 秒后台强制设计、验证边界见第 30 节，第 29 节为第一轮历史实现。

### 4.2 并发等待与 Loading 边界

- 每次业务更新入口建立一份 5 秒 `Task.Delay` 预算，覆盖游戏侧业务门排队与 SDK 自动完成跟踪；普通 Loading 不等待后台启动强制 Task。
- 每轮 SDK 初始化登记一次后台协调任务，自动结果失败或没有新鲜 FetchTime 时才创建独立 10 秒预算，ForceFetch Task 与临时活动 getter 等待共用该预算；不因失败/超时重试，不重新开始 Loading。
- 排队超时只取消尚未获得业务门的等待；临时回调订阅在结果或预算到期后释放。ForceFetch 的 FetchTime 墙钟检查保留，超时不取消原生任务或清 SDK 队列。
- 长期配置订阅独立于 Loading 等待；晚到有效值仍保存，不重跑 Loading。已初始化的 Draw 按第 31 节校验、提交排序并刷新已有界面；已打开的 GM 文本不自动刷新。
- 业务任务结束后 LoadManager 才继续 Luban 和首次 Draw 初始化；适配器仍不表达 FullRemote/保存失败等细分结果。计时不依赖 Unity PlayerLoop，但暂停时 Loading UI 无法推进，恢复后才处理到期结果；同步解析/落盘及其它启动阶段不属于 5 秒等待预算。

### 4.3 请求、通知、内容与保存分别判断

- 原 ForceFetch 新鲜度判断仍检查 LastFetchStatus=Success 和 LastFetchTime 不早于尝试开始时间，可能受设备时间/时间精度影响。
- 长期订阅只表示 SDK 已激活配置可读；Inactive/Failure 不处理。Prepare 的历史 Success 或激活失败后仍为 Success 的原生状态不能证明本次 Fetch/Activate 全部成功。
- 按 key 检查 JSON 对象根；主线仍要求 currentRemoteConfigVersion 等于 Application.version，Draw/IWanner 不新增版本必填要求。
- TrySetRawBatch 检查保存结果，相同内容不保存；保存 false/异常恢复旧缓存引用并记录失败。Draw 初始化后的提交额外校验序列并同步更新列表和已有界面，详见第 31 节；这不等于其它业务消费者均已安全热更新。
### 4.4 LoadManager 适配层语义

`UpdateRemoteConfigsForLoadManager()` 当前只表达：

- “RemoteConfig 阶段结束”

它不额外表达：

- 本次是否真的拿到了远端最新值
- 本次是否只是 fallback 成功
- 本次是否发生了内部异常后以结束态退出

因此对 `LoadManager` 来说，这个枚举器的语义是“阶段完成”，不是“阶段成功”。

## 5. 回退与取值优先级

### 5.1 raw JSON 来源优先级

当前运行时某个 remote key 的 raw JSON 来源优先级为：

1. 本次远端成功拉取且通过当前 key 可用性校验的结果
2. `DB.GameData.RemoteConfigCache` 中的上次有效值
3. `GuruSdk.Instance.RemoteConfigSpec.DefaultValues`

其中当前唯一已落地的“key 可用性校验”是：

- `level_config.currentRemoteConfigVersion == Application.version`

无论结果来自哪一层，最终都会走：

- `ConfigManager.RemoteConfig.SetRawBatch(...)`

### 5.2 业务字段读取优先级

业务字段不是直接读 raw JSON，而是通常经过：

1. `ConfigManager.RemoteConfig.TryGetValue<T>(...)`
2. `TbGlobalConst` / `GlobalConst`
3. 代码内固定兜底值

这两层要区分理解：

- 第 1 层解决“这个 remote key 的 JSON 从哪里来”
- 第 2 层解决“业务字段最终怎么兜底”

### 5.3 编辑器态边界

当前项目约束明确禁止编辑器非运行态通过：

- `RemoteConfig -> DB.GameData.RemoteConfigCache`

间接触发运行时 DB 链路。

当前 `ConfigManager.RemoteConfig.GetVictoryHorizantalRewards()` 已对编辑器非运行态做了 `Application.isPlaying` 外层保护，这与 `DevelopMD/Editor态开发约束.md` 保持一致。

## 6. 包体升级策略

### 6.1 当前策略

当 `Application.version` 与 `DB.GameData.CurrentAppVersion` 不一致时，`TryToClearRemoteCache()` 会：

1. 清空 `DB.GameData.RemoteConfigCache`
2. 更新 `DB.GameData.CurrentAppVersion`
3. 立即保存
4. 再把当前包体的 `RemoteConfigSpec.DefaultValues` 写回本地缓存

### 6.2 设计意图

这个策略的核心目标是：

- 包体升级后优先回归当前版本内置默认值
- 不继续沿用旧版本遗留的 remote JSON
- 确保新版本参数时效性，避免旧缓存污染新逻辑

### 6.3 认知边界

这里应被理解为：

- 当前代码的有意升级回退策略

而不是：

- 逻辑错误
- 风险点

如果未来业务希望升级后仍尽量沿用“上一次远端成功值”，那属于策略变更，不属于当前代码 bug。

## 7. 本地缓存设计与边界

### 7.1 当前缓存位置

RemoteConfig 原始 JSON 当前持久化在：

- `DB.GameData.RemoteConfigCache`

数据结构：

- `Dictionary<string, string>`

### 7.2 当前写入方式

缓存当前通过 `ConfigManager.RemoteConfig.SetRaw(...)` 与 `SetRawBatch(...)` 写入。

批量入口委托 `TrySetRawBatch()`：复制完整旧字典形成候选，保留非本次 key，发生变化才替换引用并保存一次。
保存成功返回 true；保存 false 或异常恢复旧配置引用，不回滚其它 GameData 字段。
单 key `SetRaw()` 与升级整表 `ClearRemoteCache()` 仍保留原来的写入方式，不由批量回滚验证推断其崩溃安全。

当前行为：

- `remoteKey` 为空则直接返回
- `json` 为空则移除该 key
- 否则写入 / 覆盖该 key
- 每次变更后调用 `DB.GameData.Save()`

### 7.3 当前边界问题

`RemoteConfigCache` 当前已经不是“只存 remote JSON”的纯净缓存表。

已确认的额外使用者：

- `SimpleBag.CoinMigrationVersion`

这意味着：

- `ClearRemoteCache()` 的整表 `Clear()` 会清掉所有复用此字典的数据
- GM 面板把这张表当作纯 remote 缓存展示时，也会混入非 remote 条目

当前这还不一定已经造成严重线上问题，但它说明存储职责已经发生混用。

## 8. 当前已知风险与限制

本节只记录当前代码里已经能直接看到的结构性问题和风险，不再把升级回退策略算作风险。

### 8.1 ForceFetch 新鲜度与排队重试风险

现状：

- `ObserveConfigGetter()` 的底层是 `BehaviorSubject`，订阅捕获当前 getter，但 SDK 的 `ObserveOnMainThread()` 将实际处理安排到 Unity Update；不能把派发返回后的首个回调直接认作自动结束。
- 服务复用 SDK 已有 getter 通知，跳过初次订阅回放，后续活动 Success 中的有效值同步缓存；可能包含已激活历史缓存。
- `GameRoot.Initialize()` 在 SDK Prepare 之后、自动请求派发之前捕获重放 getter 身份，自动跟踪只接受不同身份的活动 getter。启动强制最多一次，ForceFetch 和临时 getter 共用独立 10 秒预算，Loading 的自动等待与业务排队只使用 5 秒；长期缓存订阅独立于两份预算。

风险：

- 墙钟/原生时间精度仍可能误判本次新鲜度；业务预算不能保证原生任务终止或服务端返回最新发布值。缓存更新回调不依赖该新鲜度判断。
- 弱网或 SDK 不发布完成回调时，本轮会耗尽约 5 秒等待预算；SDK 前置初始化、热更、配置表、同步保存和主线程阻塞仍可能延长整页 Loading。
- 本轮未获可接受结果时使用业务缓存或默认值，DrawGame 可能使用旧缓存或 Guru 默认排序；晚到结果不会自动重建已经初始化的排序。

### 8.2 LoadManager 阶段语义不够精细

现状：

- `UpdateRemoteConfigsForLoadManager()` 只表达阶段结束。

风险：

- 启动链日志更容易表达“RemoteConfig 结束”，而不是“远端成功 / fallback / fault”三者的具体区别。
- 排查问题时，配置来源判定成本更高。

### 8.3 多 key 扩展时的落盘与一致性注意事项

现状：

- 当前有三个正式 key：`level_config`、`drawgame_config`、`iwannergame_config`。
- `RemoteConfigService` 当前已改成“先收集本轮可用结果，再统一调用一次 `SetRawBatch(...)`”。
- 因此在当前启动链里，同一轮 RemoteConfig 更新只会集中落盘一次。

当前影响：

- 批量提交后，启动期多次保存的问题已经明显收敛。
- 三个 key 允许分别回退，因此仍可能出现“部分 key 来自本次远端、部分 key 来自缓存或 Guru 默认值”的混合来源。

扩展后的问题：

- 如果后续 `RemoteConfigKeys` 扩成多项，当前实现至少能保证“本轮已解析出的可用结果”一次性提交。
- 但只要仍允许“部分 key 有值、部分 key 无值”，业务语义上依然可能出现“某些 key 更新了，某些 key 继续沿用旧值”的状态。

可优化方向：

- 如果未来业务要求更强一致性，可以继续把策略从“批量提交可用结果”提升为“按整组 key 做全量成功判定后再提交”。
- 是否需要继续往事务化方向收敛，取决于后续 remote key 之间是否存在强耦合关系。

### 8.4 ClearRemoteCache 不是通用安全 API

现状：

- `ClearRemoteCache()` / `ShouldClearCache()` 直接访问 `DB.GameData` 和 `DB.GameData.RemoteConfigCache`。
- 它们依赖主启动链里 `DBManager.Instance.Initialize()` 已经先执行。

风险：

- 在主流程外复用时，如果 `DB.GameData` 尚未可用，会直接空引用。

### 8.5 当前限制

当前实现还存在几个明确限制：

- 只能清理 `DB.GameData.RemoteConfigCache`，不能清 Firebase SDK 原生本地缓存。
- 只保存 raw JSON，不做 schema 版本管理。
- 检查 JSON 对象根类型和 `level_config.currentRemoteConfigVersion`，尚未完成通用字段 schema、Draw 排序资源映射及运行时刷新协调。
- `currentRemoteConfigVersion` 校验当前只覆盖 `level_config`，还不是所有 remote key 的统一约束。

## 9. 维护约束

- 新增 remote key 时，先补 `RemoteConfigService.RemoteConfigKeys`。
- 同时补 `ConfigManager.RemoteConfig` 对应读取方法，不要只加拉取不加读取入口。
- 业务层不要直接使用 `RemoteConfigManager.Instance.GetString(...)`。
- 业务层不要把 `DB.GameData.RemoteConfigCache` 当成字段读取入口。
- 涉及 editor / preview / `OnValidate` 路径时，必须遵守 `DevelopMD/Editor态开发约束.md`，不要在编辑器非运行态经由 RemoteConfig 间接触发 DB 初始化链路。
- 修改 `ForceFetch`、最小拉取间隔、Loading 等待/回退或 DrawGame 远端排序初始化前，必须遵守 `AGENTS.md` 的修改前警告与用户确认门禁。

## 10. 一句话总结

当前 `RemoteConfigService` 的定位更准确地说是：

- 在启动阶段主动尝试拿到本轮新鲜远端配置，并在远端不可用时回退到当前可用来源

而不是：

- 在断网、Firebase 失败、节流或系统时间异常时无条件保证拿到最新远端配置

其中，包体升级后的默认值优先策略是当前有意设计；真正需要关注的风险主要在于：

- ForceFetch 与 SDK 正常拉取的并发及本轮新鲜度判断
- 弱网场景下 Loading 等待时间增加
- 启动适配层语义过粗
- `RemoteConfigCache` 的存储边界混用

## 11. 2026-06-24 iOS 首装初始金币缺陷原因记录

本节保留当时的缺陷基线；其中业务侧 `ForceFetch()` 启动链已由第 18 节所述修复替代。

### 11.1 问题背景

用户反馈 iOS 上 RemoteConfig 更新不及时，首次安装时初始金币数量不符合预期；Android 当前表现正常。当前仅记录缺陷原因和证据边界，不在本节记录已落地修复方案。

### 11.2 本地证据

- `LoadManager.LoadConfigAsync()` 在配置表初始化前依次调用 `RemoteConfigService.TryToClearRemoteCache()` 和 `RemoteConfigService.UpdateRemoteConfigsForLoadManager()`。
- `LoadManager.OnPostProcess()` 中的 `InitGameReward()` 会在 `DB.GameData.PlayCount == 1` 且 `Coin < 0` 时调用 `ConfigManager.RemoteConfig.GetInitGameCoinReward()`，并将结果写入金币存档。
- `GameData.CreateDefaultGameTools()` 使用 `{ "Coin", -999 }` 作为新档金币尚未初始化的哨兵。
- `ConfigManager.RemoteConfig.GetInitGameCoinReward()` 的读取优先级是：`level_config.initGameCoinReward`，然后 `GlobalConst.InitGameCoinReward`，最后 `0`。
- 当前包体内本地默认配置里的 `level_config.initGameCoinReward` 为 `0`。
- Guru SDK 初始化阶段会 fire-and-forget 调用 `RemoteConfigManager.Instance.Initialize()`；该初始化内部会执行 `FetchAndActivate()`。
- `RemoteConfigManager.ForceFetch()` 也会调用 `FetchAndActivate()`；SDK 内部存在 `isFetchAndActivating` 并发保护，已有 fetch 运行时新的 fetch 会被跳过并快速返回。

### 11.3 直接结论

- 当前 `UpdateRemoteConfigsForLoadManager()` 表达的是“RemoteConfig 阶段结束”，不是“本次远端值已经成功激活并可证明来自远端”。
- `InitGameReward()` 是一次性写入路径；一旦把缓存值、本地默认值或字段兜底值写入金币，`Coin` 不再小于 0，后续 RemoteConfig 即使更新到正确值也不会自动修正首装金币。
- 因此当前缺陷的核心不是单纯等待时间不足，而是首装金币写入前没有区分 `initGameCoinReward` 的数据来源：本次远端、历史缓存、本地默认和业务兜底在写入点被视为同一种可用值。

### 11.4 假设与待验证项

- iOS 更容易外显的原因推测为 SDK 初始化 fetch 与 LoadManager 业务强制 fetch 的启动期竞态；该推测有本地代码链路支持，但尚未通过 iOS 真机日志确认。
- Android 正常不能证明链路没有风险，只能说明当前 Android 测试条件下没有复现同样时序。
- 尚未验证弱网、无网、Firebase throttled、Firebase 本地缓存命中、`currentRemoteConfigVersion` 不匹配、远端字段缺失等场景下的实际日志和落值。

### 11.5 风险影响

- 影响范围集中在首次安装且金币仍为 `-999` 哨兵的启动路径。
- 风险结果是初始金币可能被错误地写成旧缓存、本地默认或兜底值；写入后不会自动二次修正。
- 当前缺陷不直接阻断启动、进关、广告、IAP 或资源加载，因此按当前认知不是致命缺陷，但属于一次性经济数据准确性风险。

### 11.6 后续设计方向记录

- 更理想的功能设计应避免让 `Coin < 0` 同时承担“余额”和“初始化状态”两个职责。
- 首装金币应有独立的一次性结算状态，例如 `None / Pending / Applied / Skipped`，并由专门的初始化奖励流程管理。
- RemoteConfig 层应能对业务暴露来源语义，例如“本次远端成功”“缓存 fallback”“默认 fallback”，首装金币这类一次性写入逻辑只应接受可证明的数据来源。
- 如果产品要求首次进入前必须拿到正确金币，应等待的目标是 `level_config` 的可信远端值，而不是整个 SDK 初始化完成。

## 12. 2026-07-16 GameTipsGuide 业务读取 API

### 12.1 需求与影响范围

- `GlobalConst` 已包含 `GameTipsGuideLevel`、`GameTipsGuideFailedCount`、`GameTipsGuideWaitingTime`，首次提示剧情引导需要通过统一 RemoteConfig 入口读取，不能继续使用流程内临时常量或复用普通提示警示参数。
- 修改 `Assets/Game/Scripts/Config/ConfigManager.RemoteConfig.cs`，并由 `GameTipsGuideFlow`、`PlayCtrl_Playing`、`GamePlayPanel` 串联配置快照；未修改 RemoteConfig 拉取 key、SDK、缓存结构、存档字段或配置生成文件。

### 12.2 当前行为与数据流

- 新增 `GetGameTipsGuideLevel()`、`GetGameTipsGuideFailedCount()`、`GetGameTipsGuideWaitingTime()`。
- 每个 API 的优先级均为 `level_config` 同名字段 → `GlobalConst` 同名字段 → 数值 0。
- `GameTipsGuideFlow.LoadTriggerSettings()` 在每次进入 Playing 时被调用一次，把三项配置组成单关快照；更新阶段和流程开始前二次校验复用该快照，不会每帧重复执行 `JObject.Parse`。
- 普通提示警示仍使用 `GetShowTipsTimeLimit()` / `GetShowTipsFailCount()`，与首次剧情引导参数保持独立。

### 12.3 验证、风险与回滚

- 静态扫描确认三个 key、三个读取 API、快照加载、门槛判断和流程构造调用链完整，旧 `MinimumTriggerLevel` / `StayTriggerSeconds` 常量及旧重试参数未残留。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 通过，结果为 0 个错误、1116 个项目/依赖既有警告。
- 尚未执行 Unity PlayMode、远端覆盖、字段缺失和 Android/iOS 真机验证；配置全部不可用时数值 0 可能导致时间门槛立即满足。
- 回滚方式：删除三个字段 key 与读取 API，移除 `GameTipsGuideTriggerSettings` 快照和调用链，并恢复流程内已确认的固定门槛；无需迁移存档或资源。

## 13. 2026-07-23 DailyChallenge 奖励倍率读取 API

### 13.1 需求与影响范围

- `GlobalConst` 新增整数配置 `dailyChallengeRewardMultiple`，DailyChallenge 阶段奖励倍率需要从配置读取，替代 Manager 内固定的 `2`。
- 新增 `DailyChallengeRewardMultipleKey` 与 `GetDailyChallengeRewardMultiple()`；业务层仍只通过 `ConfigManager.RemoteConfig.GetXXX()` 读取，不直接访问 RemoteConfig SDK、缓存或 GlobalConst 表实例。
- Luban 正式生成更新 `GlobalConst.cs` 与 `lubanconfig_tbglobalconst.json`；`LubanGenerator.GlobalConstLevelConfigKeys` 同步加入该字段，并将默认值写入 Guru spec 的既有 `level_config` JSON，不新增 RemoteConfig 顶层 key。

### 13.2 当前行为与数据流

- 读取优先级为 `level_config.dailyChallengeRewardMultiple` → `GlobalConst.DailyChallengeRewardMultiple` → 固定兜底 `2`。
- 远端或本地值小于 `1` 时按 `1` 返回；这样可以避免负倍率，同时允许配置 `1` 关闭广告追加奖励。
- `DailyChallengeManager.TryCreateRewardData(...)` 在每个待领奖阶段创建数据时读取一次，不新增逐帧解析或缓存写入。

### 13.3 验证、风险与回滚

- 源表经 Luban 临时生成完成 schema 加载、数据校验和 JSON/C# 产物检查，确认字段类型为 `int`、当前值为 `2`；正式生成仅覆盖两个对应产物，Guru `level_config` 同名字段也已完成 JSON 反序列化校验。
- C# 工程构建通过，结果为 0 个错误、99 个项目既有警告。
- 尚未验证真实远端字段覆盖、本地值 1/大于 2/异常负值、配置热更新和 Android/iOS 真机广告流程。
- 回滚方式：删除 key 与读取 API，让 DailyChallengeManager 恢复固定倍率；同时回退源表字段、两个 Luban 产物、LubanGenerator 白名单和 Guru `level_config` 同名字段。

## 14. 2026-08-04 DrawGame 独立 RemoteConfig

### 配置边界

- 启动拉取 key 从仅 `level_config` 扩展为 `level_config` 与 `drawgame_config`。
- Draw 的功能开关、提示和插屏参数统一从 `drawgame_config` 读取，字段缺失时回退 `GlobalConst`；不再从主线 `level_config` 读取同名 Draw 字段。
- `drawgame_config.level_sort` 由 `DrawGameLevelCtrl` 在 Loading 阶段读取一次，`enabled=false` 时保留本地 `TbDrawLevelSort`。
- 当前版本字段校验仍只适用于 `level_config`；`drawgame_config` 不包含 `currentRemoteConfigVersion`，继续使用 RemoteConfigService 现有远端/缓存/Guru 默认值来源优先级。

### 验证与风险

- `RemoteConfigKeys` 已包含两个 key，包体升级回填默认值时会遍历并批量写入二者。
- `ConfigManager.RemoteConfig` 已补充 `drawGameIsOpen` 和全部现有 Draw GlobalConst getter，读取 key 与 Guru 默认 JSON 一致。
- 真实 Firebase 双 key 拉取、部分 key 缺失、缓存混合来源和非 Editor 远端排序仍为 `uncertain`。

## 15. 2026-08-05 DrawGame TipsPopup 金币配置

### 配置与读取边界

- `drawgame_config` 新增 `drawGame_gameTipsPopupCoinCost` 与
  `drawGame_gameTipsPopupPassLevelCoinCost`，分别管理 Draw 获取场景提示和提示后跳关的金币价格。
- `ConfigManager.RemoteConfig` 新增两个 Draw 专用 getter，读取优先级为
  `drawgame_config` 同名字段 → `GlobalConst.DrawGameGameTipsPopup*` → `0`；主线
  `GetGameTipsPopupCoinCost()` 与 `GetGameTipsPopupPassLevelCoinCost()` 仍只读取 `level_config`。
- `RemoteConfigService.RemoteConfigKeys` 已包含 `drawgame_config`，因此无需新增顶层 key；启动拉取、缓存、
  Guru 默认值回退和批量落盘继续复用现有链路。
- `LubanGenerator.GlobalConstDrawGameConfigKeys`、`guru_spec.yaml` 与 `GuruAllSpecs.G.cs` 已同步两个字段，
  当前默认值为提示 `60`、跳关 `40`。

### 验证、风险与回滚

- 静态 JSON 解析确认 GlobalConst 产物与 Guru 默认值一致；运行时与 Editor C# 工程均为 0 error。
- 真实 Firebase 覆盖、远端字段缺失、异常负数和缓存旧版本不含新字段时的运行态取值仍为 `uncertain`；
  缺字段会按既有规则回退 GlobalConst。
- 回滚时删除两个 getter、字段常量、Guru 同步白名单和默认 JSON 字段；不涉及 RemoteConfig 缓存迁移。

## 16. 2026-08-10 IWannerGame 独立 RemoteConfig

### 配置与读取边界

- 启动拉取列表新增 `iwannergame_config`，当前正式 key 总数为三个；缓存选择、Guru 默认值回退和批量落盘
  继续复用现有 RemoteConfigService，不新增缓存结构。
- `ConfigManager.RemoteConfig` 新增 9 个 IWanner 专用 getter；运行态读取优先级为
  `iwannergame_config` 同名字段 → `GlobalConst.IWannerGame*` → 类型默认值。
- Getter 在 `Application.isPlaying` 为 false 时不读取 `DB.GameData.RemoteConfigCache`，避免编辑器预览路径进入
  运行时 DB 链；当前没有新增 `OnValidate`、`Reset`、`ExecuteAlways` 或 Editor GUI 消费入口。
- `iwannergame_config` 不包含 `enabled`、`level_sort` 或 `currentRemoteConfigVersion`；本次只同步用户明确提供的
  9 个字段，不改变 IWanner 本地 Group 顺序或跨 key 版本策略。
- GM RemoteConfig 分页已加入新 key；业务入口、Skip、提示和广告消费仍为后续任务。

### 验证、风险与回滚

- Guru YAML、Guru 生成 C#、GlobalConst JSON、字段常量和 LubanGenerator 白名单完成名称/值一致性检查；
  两套 C# 定向构建均为 0 error。
- 真实 Firebase、多来源混合回退、旧缓存、Unity PlayMode、Android/iOS 和实际 Editor 菜单执行仍为
  `uncertain`。
- 回滚时删除启动 key、getter/字段常量、GM 展示、Guru 默认行和 LubanGenerator 同步入口；无需迁移存档。

## 17. 2026-08-21 启动拉取收敛为 SDK 正常回调

> 历史记录：本节描述的“移除启动 ForceFetch”方案已被第 18 节替代，不能再作为当前实现说明。

### 17.1 需求与原行为基线

- 覆盖安装后，`drawgame_config.level_sort` 可能在 Loading 已按旧缓存初始化后才由 Firebase 更新。
- 原业务链会在 Guru SDK 已 fire-and-forget 启动正常 `FetchAndActivate()` 后再次调用 `ForceFetch()`。
- `RemoteConfigManager` 的 `isFetchAndActivating` 会让并发的第二次请求快速返回，因此业务层可能把“强制拉取方法结束”
  误当成“正常网络拉取已经回调”。

### 17.2 新行为与数据流

- `RemoteConfigService` 不再调用 `ForceFetch()`；启动阶段只保留 Guru SDK `Initialize()` 发起的一次正常拉取。
- 服务订阅 `ObserveConfigGetter()`，跳过 `BehaviorSubject` 的首条历史回放；若正常拉取已在订阅前完成，则通过
  当前进程启动时间与 `LastFetchTime` 识别结果。
- 10 秒内收到回调后，继续校验 `LastFetchStatus` 和各 key 内容，批量写入
  `DB.GameData.RemoteConfigCache`，随后 `LoadManager` 才初始化 Luban 与 `DrawGameLevelCtrl`。
- 10 秒超时仍使用旧缓存或 Guru 默认值，避免弱网无限阻塞 Loading；迟到成功结果最多观察 30 秒并写入缓存，
  但不直接调用 `DrawGameLevelCtrl.SyncRemoteConfig()`，也不重建已初始化的 `LevelSort`。
- 未新增存档字段、RemoteConfig key、全局事件、平台分支、资源或 UI 修改；Android/iOS 共用同一逻辑。

### 17.3 验证、剩余风险与回滚

- 静态扫描确认 `RemoteConfigService.cs` 不再包含 `ForceFetch` 调用；`git diff --check` 通过。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 通过，结果为 0 个错误、1124 个既有警告。
- `uncertain`：尚未执行 Unity PlayMode、覆盖安装、弱网/断网、正常回调早于/晚于订阅、系统时间跳变及
  Android/iOS 真机验证。
- 回滚时恢复 `WaitForForceFetchAsync()` 与业务侧 `remote.ForceFetch()` 调用，删除 R3 回调等待和迟到结果处理；
  不涉及缓存结构或存档迁移。

## 18. 2026-08-24 恢复启动 ForceFetch 与 DrawGame 新鲜配置门禁

### 18.1 需求与问题基线

- 用户反馈 Firebase RemoteConfig 发布后没有更新，并要求恢复 `ForceFetch`，确保 DrawLevel 关卡顺序能够正确更新。
- 2026-08-21 的实现移除了业务侧 `ForceFetch()`，只等待 SDK 正常 `FetchAndActivate()` 回调。
- 当前 Guru Deployment 没有显式传入 `minimumFetchRemoteConfigInterval`，因此使用 SDK 默认两小时；Firebase 在间隔内可以直接返回旧缓存，
  但 SDK 仍会推送活动 getter，Loading 因此可能把“回调完成”误当成“本轮拿到最新远端值”。
- `DrawGameLevelCtrl` 只初始化一次；如果 `drawgame_config.level_sort` 在它初始化后才更新，当前序列不会自动重建。
- `DrawGameLevelCtrl.SyncRemoteConfig()` 原先在 `UNITY_EDITOR` 下无条件返回，导致 Unity PlayMode 即使成功拉取远端值也只能看到本地排序。

### 18.2 影响文件与系统

- `Assets/Game/Scripts/SDK/Services/Remote/RemoteConfigService.cs`
- `Assets/Game/Scripts/Play/DrawGameLevelCtrl.cs`
- `Assets/Game/UI/Loading/Scripts/LoadManager.cs`：未修改，继续作为启动顺序证据。
- `Packages/com.guru.sdk.framework.core/Runtime/Firebase/RemoteConfig/RemoteConfigManager.cs`：只读取并核对，Package 被 Git 忽略，本次最终未修改。
- `AGENTS.md`
- `DevelopMD/AI规则提升队列.md`
- `DevelopMD/RemoteConfigService_Design.md`

不涉及 prefab、资源地址、Luban 源表、存档字段、RemoteConfig 顶层 key、分析事件、广告、IAP 或账号流程修改。

### 18.3 修复后启动与数据流

1. Guru SDK 仍可在初始化阶段 fire-and-forget 启动正常 `FetchAndActivate()`。
2. Loading 进入 `RemoteConfigService.UpdateRemoteConfigsAsync(...)` 后，项目侧预先订阅下一次活动 getter，再调用 `ForceFetch()`。
3. 如果 `ForceFetch()` 实际完成，并且 `LastFetchStatus == Success`、`LastFetchTime` 不早于本次尝试开始时间，则本轮远端结果有效。
4. 如果第一次调用因 SDK `isFetchAndActivating` 被正常 fetch 占用而快速跳过，则等待该活动回调；若它只是旧缓存，下一帧重试一次 `ForceFetch()`。
5. 两次尝试仍没有新鲜成功结果时，按既有优先级使用 `DB.GameData.RemoteConfigCache` 或 Guru spec 默认值。
6. 三个 key 解析完成后统一 `SetRawBatch(...)` 落盘，`UpdateRemoteConfigsForLoadManager()` 才结束。
7. `LoadManager` 随后初始化 Luban，再调用 `DrawGameLevelCtrl.Init(...)`；控制器从已经落盘的 `drawgame_config.enabled` 与
   `drawgame_config.level_sort` 构建一次性最终序列。
8. `DrawGameLevelCtrl.SyncRemoteConfig()` 改为只在 `Application.isPlaying == false` 时返回，因此 Android、iOS 与 Unity PlayMode
   均可消费远端排序；prefab 预览和编辑器非运行态仍不会进入 RemoteConfig/DB 链路。

### 18.4 规则固化

- `AGENTS.md` 新增 RemoteConfig 高风险修改门禁：修改 ForceFetch、最小拉取间隔、Loading 等待/回退或 DrawGame 远端排序前，
  必须先阅读本文档，向用户警告远端不更新、Loading 延长/卡住、Firebase 节流和旧 `level_sort` 风险，并取得明确确认。
- `DevelopMD/AI规则提升队列.md` 新增 `BP-AI-026`，状态为 `已固化`。
- 未经用户明确批准，不得再次移除或弱化启动 ForceFetch，也不得把 DrawGame 关卡序列初始化提前到 RemoteConfig 解析落盘之前。

### 18.5 验证证据

- 首次 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 因缺少
  `Temp/obj/Assembly-CSharp/project.assets.json` 未进入编译；执行 `dotnet restore .\Assembly-CSharp.csproj -v:minimal` 后重试。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`：通过，`0` 个错误、`1124` 个项目既有警告。
- 静态启动顺序检查通过：确认 `ForceFetch` 与新鲜度校验存在、`drawgame_config` 位于启动 key 列表、Loading 等待 RemoteConfig
  结束后才初始化 Luban 与 `DrawGameLevelCtrl`、控制器在 `IsReady=true` 前调用 `SyncRemoteConfig()`。
- 静态检查确认 `DrawGameLevelCtrl` 通过 `Application.isPlaying` 阻止编辑器非运行态读取 RemoteConfig。
- `git diff --check` 在最终变更集上通过，未发现空白错误；仅有工作区换行符提示。

### 18.6 剩余风险与未验证缺口

- `uncertain`：尚未在真实 Firebase 项目发布新值后执行 Unity PlayMode、Android 或 iOS 真机拉取验证。
- `uncertain`：尚未覆盖首次安装、覆盖安装、两小时缓存命中、弱网、断网、Firebase throttled、正常 fetch 与 ForceFetch 并发、
  系统时间跳变及远端 JSON 非法场景。
- Loading 现在优先等待本轮强制拉取及必要的一次并发重试；弱网下等待可能明显长于旧 10 秒策略。
- DrawGame 会过滤找不到本地 `DrawLevelData` 的远端路径；如果远端全部路径无效，仍保留本地排序并输出错误日志。
- `level_config` 继续要求 `currentRemoteConfigVersion == Application.version`；`drawgame_config` 当前没有同类版本字段校验。

### 18.7 回滚路径

- 运行时回滚：恢复第 17 节的正常回调等待实现并移除项目侧 ForceFetch 重试，但该操作受 `AGENTS.md` 门禁约束，必须先警告用户并取得明确批准。
- DrawGame Editor 回滚：恢复 `UNITY_EDITOR` 无条件跳过远端排序；Android/iOS 行为不受该单项回滚影响。
- 不需要迁移存档、清理 RemoteConfigCache、修改 Luban 表或回退 prefab。

## 19. 2026-09-11 `dailyFreeCoinCount` 运行时云控闭环

- 修改前 `dailyFreeCoinCount` 已进入 `level_config` 默认载荷，但 Shop 每日金币规则直接读取 GlobalConst，远端值没有运行时消费者。
- 当前由 `ConfigManager.RemoteConfig.GetDailyFreeCoinCount()` 统一按“`level_config` 远端/缓存值 → GlobalConst → 0”读取；
  `DailyGiftCoinRules` 是卡片显示、非正数隐藏、领取发奖与红点资格的唯一数量入口。
- 本次不修改 `RemoteConfigService`、`ForceFetch`、版本门禁、缓存回退或 Loading 顺序；`level_config` 仍要求
  `currentRemoteConfigVersion == Application.version` 才接受本轮远端值。
- 字段名、类型、Getter、消费者与本地兜底静态检查通过；完整项目依赖编译为 `0 error / 1141 warning`。
  真实 Firebase 不同值、缺字段、错误类型和 Android/iOS 发奖仍为 `uncertain`。
- 回滚时删除字段常量和 Getter，并恢复业务对 GlobalConst 的直接读取；不涉及存档迁移。

## 20. 2026-09-16 Loading 拉取与 DrawGame 运行时刷新设计（历史设计；当前等待见第 30 节，Draw/UI 刷新实现与验证边界见第 31 节）

本节保留制定设计时的基线与完整验收目标，不把第 21、23 节的局部实现扩大为整套方案交付。

### 20.1 用户目标、已确认取舍与边界

- 目标不是“某个 Firebase 方法返回”，而是 Loading 能使用本轮成功获取、通过业务校验并完成提交的配置。
- 用户已选择：Loading 超时允许使用本地配置进入游戏；也已明确接受关卡顺序变化，允许配置更新后刷新关卡数据。
- 因此采用“Loading 优先获取新配置，失败有明确本地回退，后续有效结果可以在本次运行中生效”。不再把“只能下次重启生效”作为产品限制。
- 本节运行时关卡刷新先按前文讨论的 DrawGame 排序、选关列表设计；不运行时重载 Luban 全表、下载任意新关卡或重建全部 Prefab，也不静默扩展成主线/IWanner 排序热重建。
- 当前关卡保持已经接受的真实关卡 ID、资源和打点上下文；刷新数据列表不等于销毁当前正在玩的关卡。下一关可使用新排序。
- “云控更新”在本方案中指本次或后续拉取成功回调，不代表 Firebase 控制台发布后客户端必定立即收到推送。实时 Remote Config 监听属于额外接入，不能只增加一个回调就宣称已经具备。
- 外部网络禁止访问、Firebase 不可用、服务端节流和无效云控内容无法由客户端设计消除。稳定性的验收是分支受控、结果真实、数据一致、可以恢复，而不是无条件保证远端成功或零缺陷。
- 本节不覆盖现状说明：第 18 节仍是当前已实现启动行为。本次仅增加设计与验收条件，未修改运行时代码或 SDK。

### 20.2 已核对事实、直接结论与未知项

| 分类 | 本地证据与结论边界 |
| --- | --- |
| 事实 | `Packages/com.guru.sdk.framework.core/Runtime/Firebase/RemoteConfig/RemoteConfigManager.cs:79` 在 fetch 被占用时跳过请求；`:178`、`:195` 的 ForceFetch 设置修改在该保护之外，Initialize 也在 `:145` 修改设置。初始化、Loading、网络恢复、Websocket 调试入口共享默认 Firebase 实例。 |
| 事实 | `Assets/Game/Scripts/SDK/Services/Remote/RemoteConfigService.cs:134` 接受任何活动 getter 作为等待结束信号，随后另行检查 Success 与时间；`:380` 的 Loading 枚举器仅等待任务结束，没有向 Loading 返回远端/回退/异常结果。服务只在 `:63` 一次写入解析结果，未持续接收后续成功值写回业务配置。 |
| 事实 | `Assets/Game/Scripts/Config/ConfigManager.RemoteConfig.cs:192` 在 DB 缺失时静默返回；`:229` 没有检查 `DB.GameData.Save()` 的布尔结果。`Assets/BearGame/com.bear.savemodule@fa7a4686436c/Runtime/Core/SaveManager.cs:104` 确实返回底层保存结果。现有提交 API 不能证明保存成功。 |
| 事实 | `Assets/Game/Scripts/Play/DrawGameLevelCtrl.cs:49` 对已经 Ready 的实例直接返回，不能通过再次 Init 刷新。选关面板 `Assets/Game/UI/LevelChoicePanel/Scripts/DrawGame/DrawGame_ChoiceLevelPanel.cs:130` 又复制了一份排序，单改控制器列表不会更新面板副本。 |
| 事实 | `Assets/Game/Scripts/Play/DrawPlayCtrl.cs:1572` 的当前关数据查询仍按本地 `TbDrawLevelSort` 的显示序号查找，而下一关 `:1603` 使用控制器有效排序。两条数据来源不同，远端顺序不同于本地时可能查到不是实际加载内容的配置。 |
| 事实 | `DrawPlayCtrl.cs:1264` 的通关与解锁按显示排序 ID 记录；真实关卡配置查询、资源路径、星星和运行统计还有独立身份。运行时排序刷新必须明确这两种身份，不能顺带擅自做存档迁移。 |
| 日志事实 | 用户 ZIP `com.brain.tricky.puzzle.games.test.quest_BP-YYGFX_debug_log(4).zip` 内 `gurusdk/com.brain.tricky.puzzle.games.test.quest 2026-09-16--15-15-59-588.log:265/285/305` 出现 `NSURLErrorDomain Code=-1009` 与 Wi-Fi 路径 Denied；`:1408/1409` 在 15:16:43 记录拉取激活 True / Success。不能把该日志概括成一直拉取失败。 |
| 直接结论 | 请求、激活、业务校验、缓存提交、关卡列表刷新是不同成功边界。设计必须逐级传递明确结果；业务侧增加一个短期监听不能解决 SDK 所有入口的请求所有权。 |
| 假设 | 旧日志的 cancelled 可能与请求期间设置重建网络会话有关，源码存在这条风险链；但日志没有每次设置与原生请求对应记录，不能认定它就是所有 cancelled 的根因。 |
| 缺失证据 | 最新日志未记录业务 JSON、版本拒绝、Loading 回退时刻或缓存写入；其晚到成功是否发生在业务回退之后仍为 `uncertain`。仓库依赖声明也不等于已安装测试包的实际 SDK/原生依赖证明。 |

CodeGraph 索引目录存在，但本机 CLI 与当前工具未提供可用查询入口，以上代码证据采用当前文件只读核对。AGENTS 引用的 remote-resource-loading feature plan 当前路径未找到，不以不存在的计划补全事实。

### 20.3 SDK 层：所有入口共用请求控制器

1. Prepare、设置、Fetch、Activate 的所有权统一收口到 Guru RemoteConfigManager；游戏侧不再另外直接操作默认 Firebase 实例。
2. 设置只在本次 SDK 初始化、任何 fetch 开始前配置一次。不在 ForceFetch、网络恢复、重试或 finally 中反复设置/恢复最小间隔或超时，也不为诊断在请求期间读取可能重建会话的原生 settings。Prepare 中 SetDefaults 与缓存 Activate 必须分别真正 await 完成；不能沿用 `RemoteConfigManager.cs:62–64` 的未展开嵌套 Task 链就宣布准备结束。
3. ForceFetch 的语义保留并加强：在同一串行控制器中执行 `await FetchAsync(TimeSpan.Zero)`，成功后 `await ActivateAsync()`，再读取本次不可变配置快照。普通拉取仍保留其缓存策略，不把全局间隔永久设为零。
4. SDK 初始化拉取、Loading ForceFetch、网络恢复、Websocket force 调试入口都必须走该控制器。正在执行时不能“跳过并返回成功”：同一启动会话的强制请求可以共享同一未完成任务；正在执行的普通缓存请求不能冒充强制结果，必要时排队执行一次真正的强制请求。
5. 共享必须限定同一启动会话、相同请求要求和未完成任务，不能把上次已完成任务长期缓存为每次 ForceFetch 的结果。共享使用可被多个调用者等待的 `Task<结果>`，不要把同一个一次性 UniTask 分给多个并发等待者；本地 UniTaskCompletionSource.cs:291 存在重复注册 continuation 的拒绝分支。排队任务按需合并、有数量上限，不积累无限请求队列。
6. 请求结果携带会话 ID、请求 ID、触发来源、是否强制、异常/节流信息和配置快照；SDK 给广告等既有消费者的通知仍保留，但业务 Loading 直接等待该请求结果，不从 BehaviorSubject 的任意回放猜测完成情况。
7. 冷启动可让 SDK 初始化与 Loading 共享已登记的启动强制请求，避免正常 fetch 加强制 fetch 无意义重复；如果初始化已经发起普通请求，则排队而不是重置原生会话。这是调度合并，不是移除或弱化启动 ForceFetch。
8. 成功依据是本次强制 fetch 任务成功、原生状态与业务内容通过检查，不以系统墙钟比较作为唯一新鲜度证明；FetchTime 保留作诊断。`ActivateAsync()==false` 可能只是已经激活，不能单独判定失败；内容 hash 不变也不能单独判定没有访问服务端。
9. SDK 超时和 Loading 预算分开。Loading 不再等待不明来源的下一个 getter；它退出等待不意味着原生请求被取消。原生请求未结束时保持占用，不清锁重发、不销毁 Firebase 实例、不重建网络会话来制造“超时”。

接口依据：Firebase [Unity API](https://firebase.google.com/docs/reference/unity/class/firebase/remote-config/firebase-remote-config) 说明每次 fetch 可传零缓存有效期；[C++ 12.8.0 iOS 桥接](https://github.com/firebase/firebase-cpp-sdk/blob/v12.8.0/remote_config/src/ios/remote_config_ios.mm#L304) 和 [Android 桥接](https://github.com/firebase/firebase-cpp-sdk/blob/v12.8.0/remote_config/src/android/remote_config_android.cc#L786) 都把请求级有效期传给原生 fetch。零有效期绕过客户端缓存门槛，不绕过服务端节流或网络访问限制。

声明依赖为 Unity Firebase 12.10.0 / iOS 11.14.0 / Android Config 22.1.0；Unity 包 README 声明使用 C++ 12.8.0。iOS 11.14.0 的 [settings setter](https://github.com/firebase/firebase-ios-sdk/blob/11.14.0/FirebaseRemoteConfig/Sources/FIRRemoteConfig.m#L716) 重建会话，而 [会话重建](https://github.com/firebase/firebase-ios-sdk/blob/11.14.0/FirebaseRemoteConfig/Sources/RCNConfigFetch.m#L116) 调用 invalidateAndCancel。这支持避开该风险路径，不证明历史包根因。Loading 超时和不在交互中途破坏界面的策略亦与 [Firebase 加载策略](https://firebase.google.com/docs/remote-config/loading) 一致；本项目加入的业务校验、持久化和 Draw 会话保护仍需自行验证。

### 20.4 游戏层：Loading 释放条件与有限重试

1. DB 先就绪。SDK 必须有可等待的准备结果，不能把 `booster.IsReady` 或 HotReload 超时 break 当成 Firebase 拉取成功。
2. 热更新/本地资源前置完成后，Loading 启动统一强制请求；本地 Luban 表可先加载为 JSON、关卡 ID 与资源映射的校验依据，不能提前执行 Draw Init、ReadyToPlay 或首装发奖。`ConfigManager.cs:115–167` 当前表加载负责本地表构建，不以远端 JSON 替换这些表。
3. 拉取、激活、读取三项 key 后，先构建候选数据，不直接覆盖业务缓存。每个 key 明确记录 Remote / Cached / Default / Invalid 来源及校验结果。
4. 保留 `level_config.currentRemoteConfigVersion == Application.version` 的现有约束；不凭空要求 Draw/IWanner 存在这个字段。JSON 根类型、已有业务字段类型与合法边界、Draw 开关及排序都要检查；可选字段缺失仍走既有 Getter 兜底，不要求全量字段突然变成必填。
5. Draw 新排序先在临时集合验证：已有本地关卡 ID、可解析资源位置、重复项与空项、有效非空序列、等待关 `20999` 只在末尾一次。失败保留上一份有效排序；不能清空旧列表以后才发现新列表不可用。
6. 不把互不依赖的 key 强制绑定为全组失败。某 key 无效按该 key 回退并明示 PartialFallback；Draw JSON 与派生排序必须同一候选版本提交，不能 raw 已新、排序仍旧却报告 Draw 已更新。
7. 新提交 API 返回明确结果并检查 Save 布尔值；缓存改用完整候选字典替换，保存失败恢复旧配置引用，不回滚或覆盖金币等无关 GameData 字段。相同内容不重复保存。只有完成提交后才标记 AppliedRemote，随后初始化 Draw 和游戏业务。
8. 启动结果至少区分 FullRemote、PartialFallback、LocalFallback、InitializationError。原生失败、解析失败与持久化失败不能作为“远端成功”释放 Loading；用户允许本地回退，所以它们可以转为可说明原因的 LocalFallback。
9. 若本地关键表/配置也不可用，应进入可见初始化错误，不进入空数据游戏。允许 Firebase 回退不是允许任何基础系统失败后继续。

首轮建议参数（待运行证据验收后调整，不是已经生效的常量）：

- RemoteConfig 阶段总等待预算 60 秒，包括等待 SDK/请求排队，不为每次重试重新计时；这是优先获取配置的弱网等待上限，不代表正常启动固定等待 60 秒，也不是全 Loading 的总上限。
- 原生 fetch 超时初始建议 20 秒，仅初始化时设置一次。该值不作为 Firebase Installations、原生内部重试或任务绝不挂起的保证。
- 首次实际强制请求失败后最多自动重试一次，间隔 2 秒；只有可重试网络/服务错误才重试。明确无网时等待恢复信号而不连续空发；版本不匹配、非法配置不重复请求来掩盖发布错误。
- 网络恢复可以唤醒/合并一次补拉，仍受总预算、请求占用与 SDK 节流限制；已用本地进入游戏后，本启动会话额外后台补拉最多一次。网络抖动重复信号合并，设最小冷却，不能每次信号额外叠加重试次数。
- 收到 throttled 后记录 SDK 给出的恢复时间，不在恢复时间之前 force 重试。Wi-Fi/可下载资源只作触发提示，不当成 Firebase endpoint 可访问的证明。
- 等待时钟使用单调、非 timeScale 依赖的计时；应用暂停/系统权限覆盖期间不把 PlayerLoop 不推进当成请求已经失败。建议累计可运行的前台等待时间，恢复后先处理已到结果再判断预算；因此墙钟等待可能超过 60 秒，用户停留系统界面不计为游戏卡死。

超过预算时，Loading 固定一次本地回退决定并继续初始化；持续接收请求结果的所有者属于应用生命周期，不绑定即将销毁的 LoadManager。晚到成功仍可触发第 20.5 节更新，不会因 Loading 取消等待而被丢掉。

### 20.5 DrawGame：有效配置到达后刷新数据，不重建当前关

这是用户本次确认的行为变化：替代“已经 Init 就不能再应用新排序”的限制，但保留初次 Draw Init 必须在 Loading 配置解析提交之后的门禁。

1. 通过业务校验的 fetch 结果进入一个应用级更新入口；失败通知不刷新，内容相同不刷新，也不重新跑 LoadManager、ReadyToPlay、首装金币或 SDK 广告初始化。
2. 每次更新生成候选配置和候选排序，设置本地递增 generation。完成保存后，在主线程、无异步让出的提交段内替换活动缓存和 Draw 排序，随后通知现有持有者刷新。异步 UI 动画完成不是数据提交成功条件。
3. 当前已经进入的关卡保存独立会话上下文：进入时显示序号、真实 `DrawLevelData.Id`、资源路径、配置 generation 及单关需要固定的配置。当前数据查询、星星结算、实际 ID 打点都从这份真实身份读取，不再用刷新后的序号或原始本地排序重新推算。
4. 正在画线或展示胜利面板时可以更新全局待选列表；当前 DLevel/HUD、墨水、提示资格、当前关打点不因更新被重建或重复结算。Reset/失败重试/Replay 继续本关真实内容，遵守 `DevelopMD/关卡打点生命周期约定.md`。
5. 下一关按新列表中的“当前显示序号之后的一项”选择，这是保持当前按序号推进的规则，允许内容随排序改变；不能悄悄改成按真实 ID 重新定位后的下一项。新列表缩短、当前序号不在列表或已为等待关时，回到有效选关界面，不越界或强行加载不存在的关卡。
6. 新旧序列切换发生在胜利下一关已经预选目标之后时，已接受目标使用捕获的真实内容完成进关，不在翻页回调内重新选一次。旧关仍保留到 EnterLevelLoading 翻页完全遮住，保持现有转场销毁时序。
7. 选关面板必须重新取得新排序副本、修正页数和当前页、重新绑定可见 item、锁定状态和星星；主页 Draw 入口刷新总星数与开关。不仅刷新 `DrawGameLevelCtrl.LevelSorts`。
8. 存档继续保持现有按“第 N 关”记录 CurrentLevel / MaxLevel / UnlockLevels / PassedLevels 的规则，不清档、不把排序刷新当成通关、不重新发奖。真实关卡星星与统计记录不搬到别的 ID；列表缩短时保留历史记录，只修正需要显示/进入的有效落点。若产品希望通关/解锁跟着真实内容移动，需要另行确认存档语义与迁移，不属于本稿默认变化。
9. 选关奖励广告、当前关提示/墨水广告或 Reset/进关转场尚未完成时，暂缓提交排序变化，保存最新候选即可；回调/转场完整结束后再提交。不能丢弃已成功广告奖励，也不能让旧 item 回调把新的第 N 关当成它原来请求的内容。
10. 所有点击/进关请求携带或校验 generation 与真实内容身份；主线程短暂切换期间拒绝重复进入，旧 UI 回调不能按新列表偷偷替换内容。开放输入后再刷新外观不是门禁方案，数据版本必须在进关被接受之前校验。
11. 更新通过现有 GameManager/DrawPlayCtrl/面板可达引用和局部 API 协调，不新增 PlayEvents 或名称扫描。新增持有的 Prefab 对象必须 SerializeField 显式绑定；新 API、字段按项目规则补中文注释和职责分区。

上述即时刷新只对已核对的 Draw 调度提出方案。主线/IWanner 当前关、经济、商店与广告等消费者的热更新边界要在实现时逐项列出；不能由 Draw 列表刷新推断所有配置消费者已经安全同步。

### 20.6 影响文件、交付条件与七维风险检查

拟实施文件/系统：Guru RemoteConfigManager 与相关接口/通知、游戏 RemoteConfigService、ConfigManager.RemoteConfig 提交 API、LoadManager 等待结果、DrawGameLevelCtrl 刷新入口、DrawPlayCtrl 当前真实关身份与进入门禁、Draw 选关/主页刷新。是否需要保存候选与元数据的新字段，应优先复用现有 SaveModule；不得把临时排队标志写进混用的 RemoteConfigCache。

| 风险维度 | 设计约束与验收重点 |
| --- | --- |
| 初始化/依赖 | 准备任务有明确结果，初次 Draw Init 不早于解析提交；SDK/本地表失败不能依赖 HotReload break 假装就绪。 |
| 状态/数据一致性 | 请求结果、业务快照、排序、UI generation 对齐；当前真实关上下文固定，序号进度和真实内容身份不混用。 |
| 外部依赖/平台 | 每请求零有效期，不反复设置；尊重 Firebase 节流，真机网络限制与实际 SDK 交付单独核实。 |
| 业务/生命周期 | 更新不重建当前关、不改变 Reset/Replay/打点契约、不清进度、不重复奖励；广告与转场阻止危险提交。 |
| 资源/性能/泄漏 | 单请求、有界排队与重试；列表构建按更新发生而非每帧；应用级订阅在退出时释放，LoadManager 销毁不误取消原生任务。 |
| 异常/回退 | Fetch、Activate、JSON、资源映射、保存分别报告失败；保存失败保留旧快照，本地关键数据失败进入错误态。 |
| 可观测/验证 | 同一请求/配置版本贯穿日志，区分原生成功、业务采用、UI 刷新及本地回退，真机和故障注入作为交付门禁。 |

SDK Package 当前被 Git 忽略，仅修改本机 `Packages` 不构成可靠交付。落地必须取得可维护 SDK 源码/发布包或经确认的受版本控制依赖方式，并在构建中固定版本、检查改动确实进入 APK/IPA；不修改只读 `Assets/GF`，不擅自改 Guru 远端仓库或发布 SDK。

日志最小契约：启动/请求 ID、触发入口、是否强制、尝试次数、耗时与超时阶段；原生异常类型与可取得的 domain/code、节流恢复时间；每个 key 的来源/版本/hash、拒绝原因；提交保存结果、配置 generation、Draw 排序 hash/条数、UI 刷新和延后原因。不得输出完整带 SDK 凭据的 JSON、token 或设备敏感信息。仅 DEBUG_MODE 的 Bear 日志不能充当生产包诊断保证，必须确认已有可交付脱敏日志通道。

### 20.7 必须完成的验收矩阵与当前验证边界

以下为实施后验收要求，不是本次已经执行的测试：

| 场景 | 验收结果 |
| --- | --- |
| 首装/同版本重启/1.11 覆盖升级到 1.14 | 有效新值在 Loading 提交后初始化；记录本次真实来源，升级默认值策略不被静默更改。 |
| 两小时缓存内发布不同云控值/服务器返回相同值 | 强制请求不受客户端缓存有效期阻止；相同值不是失败，旧普通缓存请求不是本轮远端成功。 |
| 初始化、Loading、网络恢复、调试 force 同时触发 | 无并发原生 fetch、无 settings 重建打断、无 skipped 假成功、无无限队列；真正强制请求不丢。 |
| Wi-Fi 但 Firebase 不可达/网络初始禁止约 40 秒后恢复 | 有界等待/重试；预算内成功立即采用，预算外使用本地且后续成功能刷新；不以能下载资源推断 endpoint 正常。 |
| 请求超时/cancelled/throttled/原生任务不回调 | 正确错误分类；Loading 有本地退出路径、不清锁重复发；节流期不重试；底层永久挂起作为独立原生故障报告而非声称已解决。 |
| 系统时间跳变/暂停权限弹窗/切后台后恢复 | 不靠墙钟比较误拒新结果；恢复先处理已到结果，取消 Loading 等待不打断原生任务。 |
| key 缺失/非法 JSON/类型错误/主线版本不匹配/Draw 排序重复或全无效 | 按 key 明示拒绝和回退；旧有效排序不被清空；可选字段仍按现有业务规则兜底。 |
| 保存返回 false/磁盘异常 | 不报告已落盘或已应用远端，不留下半新配置/排序；其余存档不被配置回滚覆盖。 |
| 主页/选关已打开/画线中/胜利面板收到新排序 | UI 副本、页码、星星与列表对齐；当前真实内容不变，下一次新关选择使用新规则。 |
| 新排序缩短/删除当前内容/增删等待关 | 有效落点和返回选择可恢复，无越界；20999 唯一末项，历史通关与星星记录保留。 |
| 奖励广告、Reset、Replay、下一关转场期间多次更新 | 成功奖励不丢；同关重玩不变关；最新候选有界；已接受进关不换目标；旧关销毁仍在完全遮盖后。 |
| 单关连续 Reset 两次→成功→Replay，期间更新 | 实际关卡 ID/资源上下文正确，level_start/end 配对、revive 与提示资格符合权威打点约定，配置更新不新增一次尝试或发奖。 |

- 本次已做：当前调用链、缓存写入/读取、Save 布尔结果、Draw 数据查询与 UI 副本、依赖声明、相关设计和打点约束的静态核对；最新 ZIP 的失败/后续成功记录复核；Firebase 官方接口和声明版本原生源码核对。
- 本次未做：运行时代码修改、Unity 编译、模拟/故障注入、PlayMode、连接设备上的操作、Firebase 控制台条件核对、APK/IPA 依赖锁确认或 Android/iOS 实测。上述稳定性验收仍为 `uncertain`，不能用本设计稿代替实测证明。
- 只读本机反射检查 Firebase DLL 未成功，提示程序集架构与当前 PowerShell 进程不兼容；未执行程序集初始化。拟用接口已经由官方 API 核实，本地编译与 IL2CPP 可用性仍须实施阶段验证。
- 现有 JsonSaveProvider 直接写文件（`:35`），Save=true 不证明突然断电/杀进程时存档具有崩溃原子性。若验收要求配置写入不可破坏原存档，需要独立评估并授权持久化边界加强，不能在本任务中顺带全局改 SaveModule 后承诺零风险。

### 20.8 回滚与实施前门禁

- 本次设计稿回滚只撤销第 20 节及中央使用记录的对应入口，不涉及运行时、存档或 SDK。
- 实施后应保留可回到“Loading 强制拉取 + 本地回退”的开关/最小回滚路径；停用运行时刷新不等于移除启动 ForceFetch。不得在客户端首次启动依赖远端开关才能进行自身回滚。
- 刷新出现错误时保留上一份有效配置/排序，不清档、不重发初始金币、不热重建正在玩的关卡，也不修改 GF。
- 开始实现前，需对用户警告远端值不更新、Loading 延长/卡住、Firebase 节流和 Draw 使用旧缓存等风险，并取得高风险实现确认；本次“允许顺序变化/数据刷新”是产品取舍确认，不自动授权 SDK 外部发布或全局存档迁移。

## 21. 2026-09-16 保留两路拉取并串行执行

### 21.1 本次需求与边界

- 用户先讨论仅保留游戏 ForceFetch，随后明确选择“现在要两边都留”；本次保留 SDK 初始化拉取、网络恢复拉取，以及 Loading 强制拉取。
- 已在本次会话说明弱网排队增加等待、Firebase 节流、Loading 卡住和 Draw 使用旧缓存风险；用户随后选择两路共存。本次实现 SDK 请求调度与 Loading 独立等待预算，不把第 20 节的完整请求结果、业务快照和 Draw/UI 运行时刷新当作已交付。
- 本次开始前，本文第 20 节和中央使用记录已有未提交设计改动；只调整与当前调度冲突的现状说明并追加本节，不撤销已有设计。

### 21.2 原行为与新行为

- 原 SDK 在 FetchAndActivate 内部加锁，遇到并发请求直接返回；ForceFetch 在锁外反复 SetConfigSettingsAsync，finally 仍写零间隔。设置与请求存在交错窗口，不能由源码单独认定它是历史真机失败的全部根因。
- 新 SDK 用共享 Task 登记请求，并等待前序请求真正结束；普通与强制各保留一个未完成任务，重复调用合并等待，已完成任务不作为下次拉取结果。一次调度最多保留两类未完成请求，不把正在运行的普通请求冒充强制结果。
- Prepare 共享一次准备任务，分别真正等待 SetDefaultsAsync 和缓存 ActivateAsync，再设置 Firebase；所有 fetch 都等待 Prepare 结束。设置只执行一次，普通请求使用 Deployment 有效期，ForceFetch 使用每次请求的 TimeSpan.Zero。
- 拉取与激活分别等待；ActivateAsync 返回 false 不单独算失败。getter 通知仍保留，完成通知后释放队列；失败请求结束后仍可继续后序请求。对外 Prepare、ForceFetch 和 SDK 初始化的签名不变，返回前恢复主线程。
- 原普通拉取为 30 秒超时，强制拉取反复配置为 20 秒；现在统一使用准备阶段的 30 秒设置。避免请求期间改设置的代价是强制请求的配置超时从 20 秒变为 30 秒；该设置不是原生任务绝不挂起的保证。
- RemoteConfigService 新增本轮共用的 60 秒等待预算，覆盖 SDK 排队、原生等待与兼容回调等待；重试不重新计时。到期只结束业务等待，不取消底层任务，同帧已完成请求优先采用。以不受 timeScale 影响的 PlayerLoop 延时计时，应用暂停或帧不推进时不是墙钟 60 秒保证。
- 未修改 LoadManager、ConfigManager、DrawGameLevelCtrl、业务 JSON/版本校验、缓存落盘、Prefab、存档结构、资源路径、分析事件或 GF。Loading 继续等待服务解析落盘（远端或 fallback），再初始化关卡序列。

### 21.3 文件与交付方式

- `Packages/com.guru.sdk.framework.core/Runtime/Firebase/RemoteConfig/RemoteConfigManager.cs`：SDK 实际实现；Package 目录原受 Git 忽略规则覆盖，本次只将这一份实际源码直接纳入版本控制，不修改忽略规则或其它 SDK 文件。
- `Assets/Game/Scripts/SDK/Services/Remote/RemoteConfigService.cs`：独立业务等待预算，不解除原生请求所有权。
- 本文与 `DevelopMD/已有功能设计与使用记录.md`：记录范围、现状与验证边界。

用户要求移除整套辅助文件，原 tools/SDKPatches 下的补丁、清单、手动应用脚本和三份模拟测试文件均已删除。SDK 修改以实际源码交付，没有自动构建检查。本次尚未制作 APK/IPA。

### 21.4 验证证据

- SDK Core 定向 dotnet 编译通过：0 error / 69 warning。首次检查缺少 NuGet assets，restore 后又发现 Temp/bin/Debug 的 30 项依赖缺失；只从已有 Library/ScriptAssemblies 补齐缺失的派生 DLL，再编译实际 SDK 源码，未写入 Library 或 GF。
- 游戏 Assembly-CSharp 定向编译通过：0 error / 125 warning，引用本次编译后的 SDK Core；不等同于 Unity 全量重新导入或 IL2CPP 构建。
- 历史验证（辅助测试文件现已删除）：9 个实际调度/等待源码模拟用例通过。覆盖准备与缓存激活等待、普通后强制、20 个公开 ForceFetch 调用者合并、强制后普通、每类有界排队、完成后新拉取、fetch/Activate 失败后的推进、通知同步重入、SDK 初始化与网络恢复、原生未完成保持占用、设置失败不重复改设置。替身 Activate 默认返回 false，仍可正确结束请求。另验证业务预算到期仅退出等待、原生请求仍占用队列并可晚到完成、同帧结果与到期优先采用已完成请求。
- 历史验证（辅助工具现已删除）：LF 与 CRLF 临时仓库均通过重放、回滚、重复执行、未知源码拒绝且不改写检查；删除前本机 SDK 内容匹配当时的清单。
- 历史验证：移除前，编辑器 Assembly-CSharp-Editor 定向编译包含当时新增的构建检查脚本，通过：0 error / 6 warning；临时 MSBuild targets 补充脚本引用，未改生成的 csproj。该脚本及临时 targets 现已移除，这项结果不是删除后的 Unity 导入或打包验证。SDK 差异及任务文档 git diff --check 通过，新文件无尾随空白。

### 21.5 剩余风险与回滚

- `uncertain`：真实 Firebase 发布、Android/iOS、首装/覆盖安装、弱网/断网/节流、系统时间变化、Unity PlayMode、SDK 广告等 getter 消费者回归，以及最终 APK/IPA 包内补丁尚未实测。本次模拟不执行原生网络，也不证明历史 cancelled 根因已修复。
- RemoteConfigService 的墙钟新鲜度比较、BehaviorSubject 完成猜测、35 秒兼容等待、最多两次重试仍保留；SDK 内部结果未改为游戏侧的不可变请求快照。激活失败、晚到结果或系统时间变化下的业务采用边界仍需后续任务核实。
- 原生任务永久不回调时，SDK 排队请求继续等待，不清锁重发；PlayerLoop 正常推进时，游戏本轮等待预算到期可回退继续启动。原生永久挂起本身仍未修复，晚到结果的业务应用、第 20 节完整故障处理仍待实现；PlayerLoop 暂停/系统覆盖层期间的预算计时也需真机验证。
- SDK getter 晚到成功仍不自动刷新已初始化 Draw/UI 副本；第 20 节对应设计继续待实现，不能因本次排队修改宣称运行时刷新完成。
- SDK 重装或升级可能覆盖这份已纳入版本控制的源码，需要核对当前拉取队列实现；本次没有执行 SDK 重装、全新检出后的依赖安装或平台打包，此边界为 `uncertain`。整体回滚需恢复改动前的 SDK 源码并回退 RemoteConfigService 本任务等待预算；不再提供辅助回滚脚本。回滚受 AGENTS.md 的云控高风险确认门禁约束。
- 回滚不迁移存档、不清远端缓存、不改已验收关卡。若新 SDK 编译失败、调度无法推进或真机启动等待不可接受，应停止交付并按以上边界回滚，不能通过移除游戏 ForceFetch 或提前 Draw Init 掩盖问题。

### 21.6 用户要求移除额外的打包检查

- 背景：用户指出自动打包检查不在“两边拉取都保留”的要求中，并明确要求去掉。
- 移除 `Assets/Editor/GuruRemoteConfigPatchBuildGuard.cs`、对应 `.meta`，以及仅用于此前定向编译的 `Temp/Validation/GuruRemoteConfigBuildGuard.targets`；同步本文和中央使用记录。
- 行为变化：移除前，Unity 构建回调按整个 SDK 源码哈希拦截打包；移除后，该回调不存在。当时保留的手动补丁、清单和模拟工具随后也按用户要求移除，见第 21.7 节。SDK 拉取队列、ForceFetch、Loading 等待预算、配置解析与落盘不作修改。
- 验证：检查删除目标不存在、项目代码与工具没有该检查类型或临时 targets 的引用；对比删除前后 SDK 与游戏云控服务的 SHA256，并检查文档 diff。未执行 Unity 重新导入和 APK/IPA 构建，相关平台结果仍为 `uncertain`。
- 回滚边界：本次不恢复该自动检查；若用户以后明确要求恢复，应单独评审构建拦截条件。SDK 以第 21.3 节记录的实际源码交付。

### 21.7 用户要求移除整套辅助文件

- 背景：用户进一步明确要求去掉截图中的补丁、清单、应用脚本及三份模拟测试文件。
- 删除原 tools/SDKPatches 下六个文件；同步本文和中央使用记录。SDK 实际 RemoteConfigManager.cs 直接纳入 Git，保留两路串行拉取的实现，不新增替代工具、不修改 GF。
- 原行为为实际 SDK 源码留在被忽略的 Package 目录、通过辅助补丁交付；现在直接跟踪这一份实际源码，辅助应用、检查、回滚和测试文件均不保留。游戏配置、存档、资源、事件及运行流程没有本次代码修改。
- 验证：核对六个文件和此前打包检查已不存在；对比删除前后 SDK 与 RemoteConfigService 的 SHA256；核对暂存文件清单，只包含这六个删除项、SDK 实际源码及两份现有说明。未重新执行编译或平台测试，之前的编译和模拟结果仅对应相同的运行时代码。
- 剩余风险与回滚：SDK 新装/升级与 APK/IPA 验证仍为 `uncertain`。本次不恢复用户要求移除的辅助文件；若需要恢复，须由用户重新明确要求，运行时代码回滚遵循第 21.5 节。

## 22. 2026-09-16 debug_log(5) 日志分析

### 22.1 输入与核查范围

- 用户要求分析 `com.brain.tricky.puzzle.games.test.quest_BP-YYGFX_debug_log(5).zip`，当前重点为 Firebase 拉取、Loading 采用配置与广告配置回退。
- 原文件位于用户提供的 WXWork 本地缓存目录；SHA256 为 `3A988CD951527A4E84D5DC043297FF3DCC893FEDB88A36ACCDFF60D59E6F1979`。ZIP 只有 Guru SDK 日志与 FusionAds 原生日志，分别为 2966 行、51 行；不是完整 Unity Player 日志。
- 只读取 ZIP 内这两份日志，按固定文件名保存到 `C:/Users/admin/AppData/Local/Temp/brainpunk-log5-20260916-357b2a8a/sdk.log` 和 `fusion-ads.log`，以下行号对应这两份原文。
- 同时只读核对当前 RemoteConfigManager、FirebaseRemoteConfigGetter、广告云控提供者、AdsManager、业务缓存服务与本机 SDK 编译产物。知识库“日志分析”未匹配；CodeGraph 目录存在，但本机 CLI 和当前 MCP 未提供可用查询，使用直接日志与源码检索。

### 22.2 可观察事实

| 启动与版本 | Firebase 记录 | 广告记录与边界 |
| --- | --- | --- |
| 15:20:55，1.13.0-26082851；SDK 日志 1–1319 行 | 265、285、305 行共 3 次 installations token 获取失败，原生错误 `NSURLErrorDomain Code=-1009`、`unsatisfied (Denied over Wi-Fi interface)`；654–655 行在 15:21:10 出现激活 True、状态 Success，不能说这一轮一直失败。 | 664–669 行的远端及广告本地 profile 都为空，插屏/激励改用默认配置；FusionAds 14、16 行开始加载，17、18 行分别记载激励/插屏 loaded。远端拉取成功不等于广告 profile 有有效内容。 |
| 16:13:37，升级到 1.14.0-26091664；SDK 日志 1320–2200 行 | 1361 行为版本变化；1636、1650 行两次忙时跳过；1638、1652 行两次 FirebaseException“已取消”；1649、1663 行为 Failure/Error，最后成功时间仍是前一轮的值。 | 2067–2072 行：插屏/激励各等待 15 秒后超时，无广告本地 profile，改用默认配置；2178 行插屏已 loaded。 |
| 16:14:21，再启动 1.14.0-26091664；SDK 日志 2201–2966 行 | 2242 行显示版本未变；2527、2541 行两次忙时跳过；2529、2543 行两次“已取消”；2540、2554 行仍为 Failure/Error，没有新的成功时间。 | 2904–2909 行：插屏/激励各 15 秒超时、无广告本地 profile、回退默认配置。日志到 16:14:41 截止，只有本轮启动后约 20 秒，不能判断之后是否加载成功。 |

- 最新两轮合计 4 条忙时跳过、4 次 Firebase 取消异常、4 条广告云控 15 秒超时；前一轮的网络拒绝不能直接作为后两轮取消异常的原因，后两轮没有同样的 token 网络错误。
- `RemoteConfig UpdateInfo Success!` 不能独立作为拉取成功依据：`Packages/com.guru.sdk.framework.core/Runtime/Firebase/RemoteConfig/FirebaseRemoteConfigGetter.cs:85` 在读完信息后无条件打印该前缀，真实状态在后面的 LastFetchStatus。日志 1649、2540 行虽有此前缀，仍明确为 Failure/Error。
- 日志 1496 行的 analytics “Remote config fetched successfully”发生在新拉取失败前，Prepare 的 1371 行已载入旧 Success 与旧成功时间。`GuruAnalytics.cs:69`、`LegacySDKProxy.cs:188` 的监听依据状态打印，不能由该提示证明 16:13:38 发起的网络拉取成功。
- 广告字段为 `fusion_ads_engine_profiles`，来源见 `Packages/com.guru.sdk.framework.ads.fusionads/Runtime/Remote/FusionAdsRemoteConstants.cs:5`；它与业务服务保存的三个 JSON key 不同，广告 profile 本地缓存也不能等同于 `DB.GameData.RemoteConfigCache`。
- SDK 的广告配置等待使用独立 15 秒 CancellationTokenSource，见 `FusionAdsRemote.Firebase.cs:57–58`；状态非 Success 继续等待（80–84 行），到期读取广告本地缓存（129–141 行）。因此这几条超时不是 RemoteConfigService 的 60 秒等待预算日志。

### 22.3 源码与实际运行路径的差异

- 日志的 `FetchAndActivate skipped: already loading.` 仍表示忙时跳过。当前 `RemoteConfigManager.cs:109` 是 QueueFetch；`:148` 在真正执行时打印 `FetchAndActivate start: force=...`；当前源码没有上述跳过文本，日志未见新执行标记。更正首次分析的一处检索错误：日志实际有 PrepareTask、SetDefaultsAsync2 和 Prepare SetConfigSettingsAsync 标记（例如 2250、2253、2315 行），旧实现也有这些标记（原始快照 57、67、144 行），不能用它们判断新旧实现。
- 当前源码 SHA256 仍为 `FD6EED59E52B100F844C28A48DE7C0C66A292C4AA49F4C565810FF69E5FE8C75`，与两路排队实现及提交 `0cf392d7c` 对应；两份云控脚本工作区没有代码差异。
- 本机只读扫描程序集 UTF-16 字符串：`Library/ScriptAssemblies/Guru.SDK.Framework.Core.dll` 含旧跳过标记、不含新执行标记，修改时间 2026-09-02 19:06:10；此前定向编译产物 `Temp/bin/Debug/Guru.SDK.Framework.Core.dll` 不含旧跳过标记、含新执行标记，修改时间 2026-09-16 15:44:42。
- 直接结论：这份日志记录的 SDK 拉取路径与当前两路排队源码不一致，不能用它证明排队实现已进入测试包。此前定向 dotnet 编译只验证 Temp 编译产物，不等于 Unity 或 IPA 已采用它。
- `uncertain`：没有测试包的程序集、构建提交号、打包工作区和 SDK 安装记录，无法从日志区分旧包、另一份 SDK 程序集、源码被安装工具覆盖或构建没有采用修改等具体来源；本机 Library 标记不证明测试 IPA 一定由这份 DLL 制作。

### 22.4 其它错误与推导边界

- 三轮各出现一次 AdsManager 初始化线程上下文异常，见 SDK 日志 149、1523、2400 行；2401–2404 行栈指向 `TaskScheduler.FromCurrentSynchronizationContext` → `AsUniTask` → `AdsManager.Initialize`。当前 `AdsManager.cs:526–529` 的登录任务转换在 `:536` 的主线程切换之前。应核对这处调用线程；不能只把“User is null”解释为账号接口失败，也不能直接把它认定为 Firebase 取消的原因。
- 1.13.0 轮有 5 次 IAP NoProductsAvailable（257、326、328、332、594 行），1024 行后续 isAvailable 成功；两轮 1.14.0 没有相同初始化错误，1665、2505 行也记录 available。这里只说明已观察结果，未验证实际购买或恢复。
- `GuruSdk.DispatchInitializeAsync: Calling _guruRoot.DispatchAsyncInitialize` 被记录成 E，是 `GuruSdk.cs:299` 的主动 Log.E，不包含异常，不作为初始化失败证据。
- 多条 Timer cancelled 为计时器取消记录，不能与带 RemoteConfig 异常栈的 4 次 FirebaseException“已取消”混计。
- 后两轮取消异常没有请求 ID、设置变更或原生取消调用信息，触发原因仍为 `uncertain`。时间上同时出现忙时跳过与取消，只能确认事件共存，不能直接证明前者造成后者。
- 用户补充已杀进程。旧实现的 isFetchAndActivating 是 RemoteConfigManager 实例内存字段（原始快照 41 行），开始拉取时从 0 设为 1（79 行），finally 清为 0（104 行），不写存档；终止进程不会保留这个忙状态。重启后 SDK 初始化又会发起拉取（旧实现 161 行，当前 GuruSdk.Initializer.cs:41），新的重叠调用仍会触发旧分支。日志 2246 行重新创建 SDK，2388 行启动 SDK 拉取，2527、2541 行在同一轮启动打印跳过；不能解释为上一进程的锁遗留。具体被跳过的是 ForceFetch、网络恢复还是其它入口，没有调用关联记录，仍为 `uncertain`。
- 用户追问新版本是否仍有问题：这份日志的 1.14.0-26091664 两次启动都有上述跳过和取消，包版本变化不等于 SDK 拉取实现已换为当前 QueueFetch。能确认该测试包的日志仍有此现象；不能据此断言现有排队源码已经或尚未解决同一真机问题，须先核对源码是否进入实际包。

### 22.5 缺失证据与后续验证

- ZIP 没有 RemoteConfigService、LoadManager、业务 JSON、主线版本拒绝、DB 配置提交或 Draw 排序替换记录。SDK 的 LastFetchStatus 不能代替游戏实际采用配置的证据；三组业务缓存是否更新、Loading 是否回退、Draw 是否使用新排序均为 `uncertain`。
- 优先核对实际测试包对应的源码、SDK 程序集和构建提交。下一轮验收应在已有新执行日志中看到普通与强制请求分别执行，并核对它们是否按顺序完成；不能只核对 Git 已提交或编译通过。
- 对 Loading 另外采集完整 Unity Player 日志，核对 ForceFetch 成功/回退、三个 JSON 的采用来源、版本检查、配置提交完成与 Draw 初始化先后。最新轮只覆盖约 20 秒，未覆盖本轮 60 秒预算及应用暂停后行为。
- 本次仅进行日志与现有源码/程序集只读检查，更新两份既有说明；没有修改运行时代码，没有新建辅助脚本，没有编译、重装 SDK、重新出包或操作设备。无运行时回滚事项；撤销本节与中央同日分析记录即可回滚本次文档改动。

## 23. 2026-09-16 仅用游戏侧现有接口补齐缓存回调

### 需求、基线与修改

- 用户已要求接缓存更新，随后再次明确 Packages 不许改、不要新增无意义脚本和代码，并要求写进 AGENTS。已固化 Packages 只读和最小实现约束。
- 刚才在 Package 新增的 FetchSucceeded、ForceFetchWithResult 及通知派发代码已撤销，Packages 相对本次修改前 HEAD 无差异；此前第 21 节已提交的基线保留，不继续改 Package。
- 当前游戏侧 GameSDKService 在 GuruSdk.Initialize 前注册一次现有 ObserveConfigGetter；RemoteConfigService 跳过首次回放，活动 Success 通知中逐 key 校验 JSON 对象根及现有主线版本，再调用 ConfigManager.RemoteConfig.TrySetRawBatch。
- 启动版本清理前按 key 保留最新有效值，清理/默认值后提交；Loading 超时后仍保存晚到通知。订阅在退出/SubsystemRegistration 释放，编辑态不注册。原 ForceFetch、重试、新鲜度判断、等待预算与 Loading→首次 Draw 顺序保留。
- TrySetRawBatch 构造完整候选字典，仅改本次 key；相同内容不保存，保存 false/异常恢复旧配置引用，不回滚金币等其它字段。不新增仓库脚本、SDK API、打包守卫或全局事件。

### 验证与证据边界

- 撤销 Package 新增内容后，SDK 只读定向编译 0 error/69 warning；游戏 Assembly-CSharp 定向编译 0 error/125 warning。两者使用既有 csproj，输出在 Temp，不在 Packages 生成源码。
- 系统临时目录中的实际方法体模拟通过：原 9 组 SDK 调度/预算；现有通知接入、早到值跨升级清理、ForceFetch 通知保存、重复订阅/相同值去重、非法 JSON/数组根/主线版本拒绝、Failure 通知不更新、保存失败回滚、Loading 退出后的晚到保存及退出/编辑态，共 7 组游戏侧断言。
- 入口仍为 `C:/Users/admin/AppData/Local/Temp/brainpunk-remote-callback-tests-20260916/CallbackTests.csproj`，命令 `dotnet run --project <该路径> -v:quiet`，退出码 0。使用当前 SDK 调度与完整游戏服务/提交方法体，Firebase/Unity/存档及 R3 调度为替身，不能扩大为平台实测。
- 静态证据：GameSDKService 中注册早于 GuruSdk.Initialize；RemoteConfigService 订阅已有 ObserveConfigGetter 并调用原 ForceFetch；Package 原 CompleteFetchAsync 已在请求结束后发布 getter，原 Prepare 也发布 getter。因此缓存订阅无须新增 Package 接口。

### 未验证项与复查/回滚

- `uncertain`：真机 Firebase、Android/iOS/IL2CPP、实际入包、暂停覆盖、杀进程保存安全尚未验证。模拟激活失败只证明替身未产生新的活动值；SDK 原 getter 的 Success 不能独立证明原生 Fetch 与 Activate 都成功。
- Prepare 可以通知历史 Success，激活异常后原生 LastFetchStatus 也可能仍为 Success；这里只把可读且通过当前检查的活动配置同步缓存，不承诺该通知来自本次网络请求或服务器最新发布。强制请求新鲜度仍由原流程判断，其墙钟/精度风险未解决。
- JSON 对象/主线版本检查不是完整 schema/Draw 排序资源映射校验；缓存已写不表示已初始化 Draw 排序、选关/主页或已打开 GM 文本即时刷新。第 20 节完整运行时联合提交设计仍未交付。
- 批量保存失败恢复不保证原升级整表 Clear、单 key 保存或断电/杀进程原子性；现有 Loading 适配器仍不区分保存失败等完整结果。
- 下一包采集 Unity Player 日志，检查 `RemoteConfig callback registered`、`cacheSaved=True` 或早到暂存后的 `Startup retained config values`，再重新打开 GM/切 key 或经业务 API 核对实际值。当前本机源码/模拟不能保证旧测试包已有这条回调。
- 回滚只撤销三份游戏侧既有脚本本次改动及对应说明，不改 Packages、不清玩家存档、不移除原启动 ForceFetch，不整文件覆盖带已有内容的文档。

## 24. 2026-09-16 拉取失败诊断日志

### 需求与原行为

- 原流程只在部分失败分支输出 `LastFetchStatus`、`LastFetchFailureReason` 与 `ThrottledEndTime`，缺少同一轮请求关联、请求前后状态、网络可达性、回调状态、共享预算超时阶段和最终采用来源。
- 因此出现“ForceFetch 没有新结果”时，日志难以区分断网、Firebase throttled、Pending、SDK Error、无回调、FetchTime 缺失或旧时间被新鲜度检查拒绝。

### 新日志与判定边界

- 每次 `UpdateRemoteConfigsAsync()` 使用递增 operation id 串联开始、每次 attempt、回调和结束日志，并记录 keys、app version、`Application.internetReachability`、LastFetchStatus、failure reason、LastFetchTime 与 ThrottledEndTime。
- ForceFetch task 超出 60 秒共享预算、35 秒内没有活动回调、task 抛出异常、状态非 Success、Success 但 FetchTime 缺失，以及 FetchTime 早于本次请求都会输出独立原因。
- failure reason 映射会明确标记 `Throttled` 截止时间、`Invalid`、`Error`、`Pending` 和 Unity 报告无网络；每个 key 记录 `sdkActiveValue`、`validatedCallbackValue` 或具体 fallback 来源，最终日志记录 fetch mode、已解析 key 数和业务缓存提交结果。
- 长期 `ObserveConfigGetter()` 收到 null、inactive 或非 Success getter 时不再静默忽略，会输出 getter 完整状态。
- GuruSDK 的 `ForceFetch()` 当前会在内部捕获部分 Firebase 异常，游戏侧不一定能得到原始 exception；当 failure reason 为 `Error` 时，日志会提示结合相邻 Guru/Firebase exception log 判断底层原因。此诊断不修改 SDK，也不宣称能从游戏侧恢复被 SDK 隐藏的异常文本。

### 影响、验证与回滚

- 拉取次数、两次尝试、35 秒回调等待、60 秒共享预算、版本校验、fallback 顺序和 `TrySetRawBatch()` 行为保持不变；只增加日志和 GM 手动入口。
- `Assembly-CSharp.csproj` 最终构建通过：0 error / 122 warning；`RemoteConfigService.cs` 与相关 GM 文件定向 `git diff --check` 通过。
- `uncertain`：尚未连接真实 Firebase 验证各失败枚举、限流时间和原生异常日志的实际组合；Unity 的网络可达性只是一项诊断信号，不等于服务端一定可达。
- 回滚时删除 operation id、状态格式化/原因映射和新增日志参数，恢复原方法签名即可；不修改或清理现有缓存和玩家存档。

## 25. 2026-09-16 ForceFetch 等待 SDK 自动拉取

### 需求与运行证据

- 新需求要求业务 `ForceFetch` 先等待 SDK 自动拉取结束，再发起强制拉取，避免 SDK 自动请求与业务强制请求重叠。
- 现有实体机日志中，SDK 首轮 `FetchAndActiveTask result:False` 后 getter 仍为 `Pending/Invalid`，随后两次业务 ForceFetch 均在无网络状态下立即失败；约 13 秒后的 Success 更可能是网络恢复触发的新请求，不能证明原首轮请求一直运行到成功。
- GuruSDK 没有公开“请求正在执行”状态；游戏侧可用信号只有 getter 的 `Pending/Success/Failure` 和通知，因此无法精确区分自动请求、网络恢复请求及其它 SDK 内部入口。

### 新行为

- `ShouldUseFallbackValuesAsync()` 创建原 60 秒共享预算后，先检查 SDK 当前状态。`Success/Failure` 已是终态时直接进入 ForceFetch；`Pending` 或未知状态时订阅下一次活动 getter，等待其进入 `Success/Failure`。
- 自动拉取阶段最多等待 35 秒，并计入原 60 秒共享预算。观察到终态后仍执行原有最多两次 ForceFetch；35 秒后仍为 Pending 时记录日志并用剩余预算继续 ForceFetch；共享预算已耗尽则直接使用缓存或默认值。
- 状态检查和订阅之间增加二次终态检查，避免请求恰好完成时漏掉通知。GMPanel 手动按钮复用同一入口，因此仅在 SDK 仍为 Pending 时执行相同等待。
- GuruSDK、Packages、缓存结构、三个业务 key、版本校验、ForceFetch 新鲜度检查和 fallback 优先级均未修改。

### 验证、风险与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore` 通过：0 error / 1008 warning；`RemoteConfigService.cs` 定向 `git diff --check` 通过。
- `uncertain`：尚未在真机验证 Pending 自动转 Success、Pending 自动转 Failure、永久 Pending 后 35 秒继续 ForceFetch，以及手动 GM 拉取四条路径。若 SDK 在请求运行期间保留历史 Success 而不发布 Pending，游戏侧会认为状态已是终态并立即 ForceFetch，这是现有公开接口无法消除的边界。
- 回滚时删除 `SdkAutomaticFetchMaxWaitSeconds`、`WaitForSdkAutomaticFetchCompletionAsync()`、`WaitForNextTerminalSdkFetchCallbackAsync()`、`IsTerminalFetchState()` 及强制拉取循环前的等待调用即可；无需修改 GuruSDK 或清理玩家缓存。

## 26. 2026-09-17 收敛启动拉取次数

### 需求与新行为

- 用户确认重复调用偏多，同意采用“自动拉取新鲜成功则直接使用，否则只 ForceFetch 一次；GM 手动入口仍额外强制一次”的策略。
- 回调注册时间记录为本次运行的新鲜度基线。SDK 状态为 Success 且 `LastFetchTime >= callbackRegisteredAtUtc` 时，启动 `UpdateRemoteConfigsAsync()` 接受该结果并跳过 ForceFetch；历史缓存的旧 Success 不满足该条件。
- 自动拉取为 Failure、旧 Success、缺少有效 FetchTime，或 Pending 等待 35 秒仍未结束时，启动流程仅执行一次 ForceFetch。原 60 秒共享预算不变，足以覆盖最长 35 秒自动等待及一次 20 秒 SDK 强制请求，并留出约 5 秒调度余量。
- 新增 `ForceUpdateRemoteConfigsAsync()` 给 GMPanel 手动按钮使用。它同样先等待当前 Pending 结束，但即使已观察到本次运行的新鲜 Success，也仍执行一次 ForceFetch，保证“手动拉取”不会被启动结果短路。
- 启动阶段因此最多由本服务增加一次强制请求；后续网络恢复重拉继续由 SDK 自身处理，长期回调仍负责校验并更新业务缓存。

### 影响、验证与回滚

- 修改仅涉及游戏侧 `RemoteConfigService.cs`、GMPanel 调用点和本地说明；GuruSDK、Packages、key、缓存结构、版本校验和 fallback 顺序未修改。
- `dotnet build Assembly-CSharp.csproj --no-restore` 通过：0 error / 122 warning；相关代码与文档 `git diff --check` 通过。
- `uncertain`：真机尚未验证自动新鲜 Success 跳过 ForceFetch、旧 Success 执行一次 ForceFetch、永久 Pending 和 GM 手动强制四条路径。SDK 没有公开请求来源或 in-flight 标识，回调注册后的网络恢复 Success 也会被视为本次运行的新鲜 SDK 结果。
- 回滚时恢复两次 ForceFetch 循环，移除回调注册时间判定和 `ForceUpdateRemoteConfigsAsync()`，并把 GMPanel 调用恢复为 `UpdateRemoteConfigsAsync()`；无需清理存档。

## 27. 2026-09-17 游戏侧确认自动拉取完成后再 ForceFetch

### 问题与代码证据

- 实体日志对应的 `1.14.0-26091678` 构建早于第 26 节源码修改，因此日志中的两次失败不能用于验证当前“一次 ForceFetch”实现。
- 当前安装的 GuruSDK `v26.4.4` 仍由 `isFetchAndActivating` 控制 `FetchAndActivate()`；并发调用会打印 `FetchAndActivate skipped: already loading.` 后直接返回。它与第 21 节记录的排队 SDK 实现不一致。GuruSDK 是指向 `~/.guru/` 的只读软链接，本次未修改或切换它。
- `GuruSdk.Initialize()` 通过 `RemoteConfigManager.Initialize().Forget()` 派发自动拉取后返回。其返回值不代表自动拉取已经结束；`RemoteConfigManager.Prepare()` 又会先发布一次包含历史 `LastFetchStatus` 的缓存激活 getter。因此用当前 `Success/Failure/Pending` 判断是否仍在拉取，会把历史终态误当成当前空闲状态。
- GMPanel 的实例字段只能阻止同一面板重复点击，不能串行化 Loading、其它业务入口和 SDK 自动请求。

### 新行为

- `GameSDKService.InitService()` 在调用 `GuruSdk.Initialize()` 前启动 Remote Config 初始化跟踪，在该调用返回后标记“自动初始化已派发”。
- 持久 getter 订阅同时记录初始化阶段活动回调。正常路径中，第一个活动回调来自 `Prepare()` 缓存激活，第二个来自 SDK 自动拉取结束；若 `Prepare()` 未发布回调，则初始化派发返回后的首个新回调也可确认自动拉取结束。判断不再使用历史 `LastFetchStatus` 或墙钟新鲜度推断 SDK 是否空闲。
- 启动业务拉取最多等待 35 秒获取上述完成证据。确认自动拉取成功时直接采用；确认自动拉取失败时才执行一次 ForceFetch；无法确认完成时立即选择缓存或默认值，不在 SDK 所有权未知时强制拉取。原 60 秒共享预算继续覆盖“自动拉取完成后的一次强制请求”。
- `RemoteConfigService` 使用全局 `SemaphoreSlim` 串行化 Loading、GM 和其它游戏侧更新入口；获得异步门和等待标准 Task 后显式返回 Unity 主线程。完成源使用异步续接，避免在 SDK getter 回调栈中重入配置解析或落盘。
- 原三个 key、JSON/版本校验、批量缓存提交、晚到成功长期回调、Draw/IWanner 初始化顺序和 GM 手动额外强制一次的语义保持不变。

### 影响文件与验证

- `Assets/Game/Scripts/SDK/Services/Core/GameSDKService.cs`：增加初始化跟踪的开始和派发返回通知。
- `Assets/Game/Scripts/SDK/Services/Remote/RemoteConfigService.cs`：增加回调序号协调器、未知所有权回退和游戏侧全局异步门；移除基于历史状态判断自动请求结束的逻辑。
- `dotnet build Assembly-CSharp.csproj --no-restore` 通过：0 error / 122 个项目既有 warning。代码定向 `git diff --check` 通过。

### 剩余风险与回滚

- `uncertain`：尚未在 iOS 真机验证正常自动成功、自动失败后一次强制、自动请求无回调、热更提前失败、GM 与 Loading 同时触发五条路径；也未执行 IL2CPP 打包。
- 游戏侧异步门不能串行化 GuruSDK 内部的网络恢复请求或 SDK 其它调用者。启动路径已通过自动拉取回调协调；启动后 GM 强制拉取仍可能与极少数 SDK 内部请求重叠。彻底解决需要 GuruSDK 维护方交付公开的请求排队或 in-flight API。
- 回滚时移除 `GameSDKService` 两个跟踪调用、初始化回调协调字段/方法和全局异步门，恢复第 26 节的状态/时间判定；无需修改 GuruSDK、清理 RemoteConfig 缓存或迁移存档。

## 28. 2026-09-18 Android Loading 长等待实机排查

### 范围与可观察事实

- 用户反馈已连接 Android 手机，Loading 等待很长。本轮只读采集日志和核对源码，没有调整拉取、等待、缓存、SDK 或手机网络设置，也没有重新启动或安装游戏。
- ADB 识别到小米 14 Pro（型号 `23116PN5BC`）；安装包 `com.brain.tricky.puzzle.games.test.quest` 的 `versionName=1.14.0`、`versionCode=26091857`，更新时间为 `2026-09-18 15:22:01`。安装日期不能证明该包使用当前工作区源码。
- 当前工作区 `ProjectSettings/ProjectSettings.asset:146` 为 `bundleVersion: 1.15.0`，`Assets/StreamingAssets/build_info` 记录本地构建时间 `2026/9/18 15:03:04`。手机包与工作区不能仅凭当天构建时间视为同一实现。
- 成功采集的 Unity 缓冲日志保存于 `C:/Users/admin/AppData/Local/Temp/brainpunk-loading-android-20260918/logcat-unity-buffer.txt`，共 3006 行。包信息保存于同目录 `package-metadata.txt`。原始日志包含账户及设备数据，仅本地留存；下表仅摘录排查所需的非账户信息。

| 手机本地时间 | 观察事实 | 原始日志行号 |
| --- | --- | --- |
| 15:22:42.860 | 第一轮起始时间，由后续日志中的 `forceFetchStartedAtUtc=2026-09-18T07:22:42.8603220Z` 换算 | 1413 |
| 15:23:17.877 | 第一轮提示 35 秒内未观察到活动拉取回调 | 1408 |
| 15:23:17.880 | 开始第二轮；第一轮没有得到新鲜成功结果 | 1419、1440 |
| 15:23:17.883 | SDK 打印 `FetchAndActivate skipped: already loading.`，跳过重叠请求 | 1426 |
| 15:23:52.260 | `OnApplicationPause: True` | 2143 |
| 15:24:29.908 | `OnApplicationPause: False` | 2270 |
| 15:24:30.709 | 第二轮提示 35 秒内未观察到活动拉取回调 | 2293 |
| 15:24:30.711 | 两轮未获新鲜成功结果，决定使用缓存或默认值 | 2304 |
| 15:24:30.723–.725 | 三个业务 key 选择缓存回退 | 2311、2325、2340 |
| 15:24:31.062–.063 | 配置表初始化成功，LoadManager 确认加载完成 | 2480、2485 |

### 直接结论与因果边界

- 本次观察到的主要等待位于 Remote Config 阶段。`LoadManager.cs:196–200` 会等待 RemoteConfig 协程结束后再继续配置表初始化；原始日志中缓存回退决策到配置表加载完成约 0.352 秒，不支持把本次约两分钟归因于配置表本身。
- 第一轮等待约 35.017 秒；第二轮墙钟耗时约 72.828 秒，其中应用暂停约 37.648 秒。扣除暂停后第二轮约 35.180 秒。第一轮开始到缓存回退约 107.851 秒，到配置表加载完成约 108.203 秒。此统计不包含第一轮之前的 SDK、热更耗时，也不等于完整 Loading 页面展示时间。
- `ios/1.14.0`（提交 `1abdce7c6`）的游戏侧源码 `RemoteConfigService.cs:27、79–97、104–119、142–149` 为两次 ForceFetch、每轮等待 35 秒，且超时使用默认 `UniTask.Delay`。手机打印的日志和堆栈符合这一旧流程；本次 Android 包的实际源码提交仍未确认，不能仅以 iOS 标签认定其构建来源。
- 当前游戏侧源码 `RemoteConfigService.cs:30–34、323–348、389–405、415–423` 为最多一次强制请求、初始化自动拉取协调及 60 秒共享等待预算，日志包含 `Fetch operation`。当前只读 SDK 源码 `RemoteConfigManager.cs:109–179、260–265` 使用排队请求。手机日志中的两轮重试与忙时跳过，与上述当前实现不同；本轮日志不能验证第 27 节当前实现的真机效果。
- 超时日志显示 `LastFetchTime` 仍为 `2026-09-16T10:37:58.8510000Z`，因此这两次检查没有拿到符合本轮起始时间的新鲜成功结果。忙时跳过说明 SDK 当时认为已有请求在进行，不能由此直接断言 DNS、网络、防火墙或 Firebase 服务故障。

### 假设、缺失证据与下一步验证

- `uncertain`：尚无完整启动前段和 Firebase 原生请求结果，无法判定在途请求为何没有及时发布完成回调；也没有应用暂停触发者的明确记录。暂停与第二轮延长在时间上吻合，但本轮没有断言暂停由 USB 授权、锁屏或用户切后台中的哪一个动作导致。
- `uncertain`：手机包可能来自不同分支或构建目录，也可能存在其它运行时代码来源。未取得该 Android 包的构建提交、SDK 依赖清单或程序集来源，因此只确认运行行为不同，不断言具体打包错误。
- 后续应先确认该 APK 的构建提交和实际 SDK，再用对应包在持续前台条件下完整采集启动及 Firebase 原生日志，对比自动成功、失败/无回调和暂停恢复路径。仅把当前包的日志用于评估当前工作区修复会混淆证据。
- 当前 60 秒预算由 Unity PlayerLoop 驱动，`ignoreTimeScale` 不能证明应用暂停期间仍继续推进；本轮未验证新版流程的后台墙钟上限，也未验证新版 Android/iOS 包。
- 本轮没有运行时变更，回滚仅需删除本节及当日记录中的对应排查主题。若后续要修改拉取、ForceFetch、Loading 等待或缓存策略，仍须遵守 `AGENTS.md` 的风险提示和用户确认规则；Packages/GuruSDK 保持只读。

### 用户追问：更早版本为什么没有明显的额外等待

- Git 标签 `android/1.10.0`、`ios/1.11.0`、`android/1.12.0` 的游戏侧服务已经进行 Firebase ForceFetch，并设置 10 秒任务等待超时，并非完全没有等待。对应只读源码快照保存于上述本地采集目录；以 `android/1.12.0` 为例，`RemoteConfigService.cs:26、107–154、277–295` 为 10 秒预算、等待 ForceFetch Task 和仅检查 `LastFetchStatus=Success` 的路径。
- 提交 `cce8cf27c`（2026-08-21，当时工程版本号仍为 1.12.0）改为等待 SDK 正常拉取的新回调，前台等待仍为 10 秒；另有 30 秒晚到观察，不阻塞已结束的 Loading。不能把这次提交称作两轮 35 秒等待的引入点。
- 提交 `93fbe02c4`（2026-08-24）才恢复业务 ForceFetch，并加入最多两次、每轮 35 秒回调等待及 `LastFetchTime >= 本轮起始时间` 的新鲜度验证。目的在于避免 Task 返回或历史 Success 被误当成当次新鲜成功，但未获回调时玩家可观察的等待明显延长。
- `feature/1.13.0_common` 与 `ios/1.14.0` 的该文件内容完全相同，限定文件的 `git diff --quiet` 返回 0。因此若用户比较的是这两个源码版本，两轮等待并非 1.14 才新增；若比较的是上述早期标签，等待上限和成功判定确实不同。相同版本号内部构建仍需核对具体提交。
- 静态推导：在 SDK 忙时直接返回且仍保留历史 Success 的条件下，早期 Task/状态判定可能很快继续；两轮新鲜度判定则不能接受旧时间，可能等待新回调至超时。另一条正常路径是请求本身及时成功，新流程也会提前结束，无需固定等满 35 秒。历史设备日志缺失，用户此前启动快的具体分支仍为 `uncertain`，不能认定必然由缓存、网络或 SDK 版本中的某一个因素导致。

### 用户限定区间：1.13 发布位置至 1.14/develop

- 本地 `git tag --list '*13*'` 和只读远端查询 `git ls-remote --tags origin '*1.13.0*'` 均无结果。因此没有把发布分支静默称作 1.13 tag；当前可核对的 1.13 发布分支是 `feature/1.13.0_common`，提交 `c1626e25c8487d12e97b7e079d0f8d26f168c60f`。若用户另有未在该远端的 tag，需要其实际提交才能严格替换比较边界。
- 本地 reflog 记录 `feature/1.14.0_common` 于 `2026-08-27 11:01:02` 从 `feature/1.13.0_common` 的 `c1626e25c` 创建，随后 `cc17217cc`（8 月 28 日）合并 develop。不能把当前 develop 位置直接称作当时的分支创建点。
- 核对三个终点：`feature/1.14.0_common=7993a2a3940fac2ded7e0c70579378578d8c2075`、`ios/1.14.0^{commit}=1abdce7c6b417a86cbf3b2e3f857132d5aab5d2c`、`develop=44e04849db0aa1ca8034aa3ff01c91b358a00ad3`。它们与上述 1.13 发布位置的 `RemoteConfigService.cs` blob 全部为 `057bff4bc0fa182ff3a25afd350a857021d33e9d`；`GameSDKService.cs` blob 全部为 `99211c5c11fcd26339a87b145a2c0b7635050252`。
- 限定上述两个文件的 `git diff --quiet feature/1.13.0_common develop` 和 `git diff --quiet feature/1.13.0_common feature/1.14.0_common` 均退出 0。对应区间的文件历史没有修改；`LoadManager` diff 改动为后处理的平台资产恢复、红点和启动弹窗序列，没有更改 Remote Config 等待阶段。
- 直接结论仅针对可核对的游戏侧区间：请求等待规则没有改变，1.13 发布分支已经包含两次尝试、每轮 35 秒及 FetchTime 新鲜度验证。9 月 16–17 日的 Remote 修改不属于上述 1.14/develop 终点历史，当前包含它们的是 1.15 分支。
- `Packages` 在这个区间没有 Git 文件差异，但历史实际安装的外部 SDK 内容不能由未跟踪目录或声明文件推定；以上源码结论不证明两个已发布 APK 的 SDK 二进制和真机表现完全一致。本轮只读 Git 核对，没有运行时变更。

### 当前最新版是否仍可能长等待

- 本轮以当前 1.15 工作区源码（服务最近提交 `df1ae6559`）为对象，未把手机 1.14 日志当成新版实测。
- `RemoteConfigService.cs:323–367、370–406`：自动完成跟踪已经确认 Success 时直接接受；自动请求无完成回调时，等待约 35 秒后选择缓存/默认值，不再 ForceFetch；确认完成但没有 Success 时最多强制一次。
- `RemoteConfigService.cs:423–476`：该次强制 Task 和活动回调均与本轮 60 秒预算竞争，不为强制调用或回调等待另开一轮总预算。因此静态上已移除旧版连续两轮 35 秒失败重试，但启动仍会因自动请求无回调等待约 35 秒，也可能在自动失败后强制等待消耗约 60 秒预算。成功可提前结束，数字不是固定延迟。
- 上述时间是在主线程/PlayerLoop 正常推进且本轮已进入等待阶段时的设计预算，不是整页墙钟上限。`updateRemoteConfigGate.WaitAsync()` 位于预算建立前（源码 265）；初始化、缓存解析/落盘、配置表和其他 Loading 阶段也不在该预算内。`LoadManager.cs:233` 的配置表等待另无超时。本轮没有观察到新版这些边界导致的实际卡住，不将代码边界写成已发生故障。
- 应用暂停时 `UniTask.Delay(..., ignoreTimeScale: true)` 仍依赖 PlayerLoop，不能保证后台时间计入等待预算。持续前台和暂停恢复的新版 Android/iOS 验证仍为 `uncertain`。
- 当前只读 SDK 配置 `RemoteConfigManager.cs:94` 为 30 秒原生 fetch timeout；第 26 节的 20 秒属于历史记录，不能用于估算当前包实际请求时长。游戏侧预算只结束业务等待，不取消原生请求或清 SDK 队列。
- 结论：最新版有回退和收敛重试的设计，仍保留可被玩家感知的 35–60 秒远端等待路径；不能宣称 Loading 长等待已在真机解决。本轮只更新分析记录，没有修改等待策略。

## 29. 2026-09-18 单轮 5 秒业务等待预算

本节记录第一轮已完成验证的历史实现；用户后续明确四步要求后，当前行为以第 30 节及第 4 节为准。

### 需求、基线与实施边界

- 用户明确表示不能接受 35 秒等待，最多接受一次 5 秒；已告知短预算更容易使用旧缓存/默认值和旧 Draw `level_sort`。本节记录当前修改目标，历史第 26–28 节保留为当时实现及排查证据。
- 修改前：自动完成回调最多等 35 秒；确认失败后最多 ForceFetch 一次，业务请求及回调共用 60 秒 PlayerLoop 预算；全局业务门排队不在预算内，临时回调订阅另有 35 秒计时。
- 实现行为：进入业务更新入口时建立一份 5 秒计时任务，业务门排队、自动完成跟踪、最多一次 ForceFetch 和临时回调等待均使用它；不重置预算，不新增失败重试。排队超时取消尚未获得门的等待，避免超时后悄悄获得门且无法释放。取消和获得门同时发生时，以等待 Task 的实际结果决定门所有权，已获得门仍在 finally 释放。
- 预算使用标准 `Task.Delay`，不依赖 Unity 时间缩放和 PlayerLoop 计时。应用暂停时无法推进 Loading UI，恢复后应直接处理已到期预算；Android/iOS 系统挂起及恢复行为仍需真机验证，不能承诺后台继续加载页面。
- 保留自动成功直接使用、未确认自动完成不重叠强制请求、确认自动完成但没有成功结果且预算未到期时强制一次，以及长期晚到回调保存。业务超时不取消 Firebase 原生请求、不清 SDK 请求队列、不修改最小拉取间隔，不增加因超时而重发请求的节流风险。
- 保留先选择来源、解析并提交缓存，再加载 Luban 并初始化 Draw/IWanner 的顺序。业务门排队超时不覆盖当前缓存；Loading 入口已经调用 `TryToClearRemoteCache()` 准备缓存/默认值。
- 影响文件：游戏侧 `Assets/Game/Scripts/SDK/Services/Remote/RemoteConfigService.cs`、本设计说明与当日使用记录。GM 手动入口也使用同一单轮 5 秒预算，晚到的实际结果仍可保存。SDK、Packages、GF、Prefab 和手机安装包不在修改范围。
- 5 秒仅限制本服务的异步业务等待；主线程调度、同步 JSON 解析/落盘、SDK 前置初始化、热更、配置表及其他 Loading 阶段不是页面 5 秒上限。
- 实施前状态：对应 feature plan 引用路径 `../specs/037-remote-resource-loading/plan.md` 不存在；已基于当前规则、模块设计和源码核对。本地 CodeGraph 索引存在但 CLI 不可用，当前未提供可调用 MCP，因此回退直接读取源代码和 Git 历史。

### 验证证据、剩余风险与回滚条件

- 定向 Roslyn 编译使用实际 Unity 引用、现有游戏 DLL 与项目依赖，编译当前完整 `RemoteConfigService.cs`：退出 0，0 错误、0 警告。此证据覆盖本文件接口与语法，不等于全项目 Unity 导入、构建或 IL2CPP 验证。
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo -p:BuildProjectReferences=false` 被缺失 `Temp/obj/Assembly-CSharp/project.assets.json` 的 `NETSDK1004` 阻断。本轮未为此还原、构建或改写 Packages/GF。
- 本地 .NET 计时程序通过反射调用上述实际编译服务的 `WaitForRequestWithinBudgetAsync()`，10 项断言通过：已完成请求提前返回；持续未完成请求约 5.002 秒结束业务等待、底层 Task 仍 Pending；该 Task 随后仍可成功；先等待约 3 秒的阶段后，第二阶段沿用同一截止任务，总计约 5.010 秒；预算已到期时不再等待。计时包含运行时调度误差，不承诺精确 5.000 秒。测试首次因临时目录定位错误失败，纠正验证程序路径后通过，生产源码未因该失败调整。
- 12 项源码静态断言通过：仅有一个计时任务，移除独立 35 秒计时，预算从入口穿过排队/自动/强制/回调，排队可取消，预算到期不派发强制，长期保存保留。限定三个本轮文件的 `git diff --check` 通过。
- 验证产物：`C:/Users/admin/AppData/Local/Temp/brainpunk-remote-five-second-20260918/` 下的 `compile.rsp`、`compile.log`、`build.log`、`budget-check.log`、`Program.cs`、`static-check.log`、`runtime-change.diff`。计时验证仅覆盖实际等待辅助方法，未运行完整 SDK、队列取消分支、配置落盘或 Unity 场景。
- 真机清单仍待执行：自动已成功直接继续；自动始终无回调约 5 秒回退且 ForceFetch 次数为 0；自动在第 4 秒失败后强制长期不结束，总等待仍约 5 秒、强制次数最多 1；并发入口第 5 秒撤销排队，原调用释放后新调用仍可获得门；暂停超过 5 秒后恢复不重开预算；晚到结果保存；首次 Draw/IWanner 初始化保持解析/落盘之后的顺序。
- 自动完成跟踪依赖现有 SDK getter 回调计数/派发边界，并非 SDK 公开 in-flight 状态；游戏侧业务门不能控制 SDK 内部网络恢复和其它调用者。当前只读 SDK 队列静态上会等待前序请求完成，但历史手机包的忙时跳过实现不能用来验证新版。仍不能宣称所有自动/强制并发场景已经真机无冲突。
- `uncertain`：尚未使用本轮修改生成 Android/iOS 包或做新版实机计时，现有手机 1.14 日志只作为问题基线。
- 如缓存缺失导致业务初始化失败、晚到结果覆盖错误、强制请求被重复派发或队列门不能释放，应回滚本轮游戏侧差异及本节当前行为说明；不回滚其他任务文件、不修改 SDK 或清玩家存档。

## 30. 2026-09-18 自动等待 5 秒、条件后台强制 10 秒

### 用户要求、原行为与实施目标

- 用户最终明确四步：Loading 只等自动更新最多 5 秒；自动返回失败或在客户端 CD 时转入强制；强制独立最多等 10 秒，不阻塞游戏；成功经回调更新游戏配置。此前已提示旧缓存/Draw 排序、Loading、节流风险，用户给出明确策略作为本次变更依据。
- 第 29 节将自动、强制全部合并在前台 5 秒内，自动晚到后不继续安排强制；中间未完成的调整曾无条件强制并让 Loading 等启动 Task。最终实现替换上述策略，自动新鲜成功不强制，普通 Loading 不等待强制任务。
- 每轮 SDK 初始化开始跟踪时登记一个后台协调 Task；等待自动结果，再回主线程并让出一帧退出 SDK 回调栈。仅 `Failure` 或 `Success` 缺失/无效/早于本轮起始时间的 FetchTime 进入一次 ForceFetch，独立 10 秒覆盖实际任务及临时回调等待。`Pending` 不当成失败或 CD。SDK 没有公开 CD 标志，历史 FetchTime 是“没有新鲜结果”的游戏侧判定，不能精确证明具体 CD 原因。
- Loading 使用从业务入口开始的 5 秒墙钟计时，覆盖门排队与自动结果；自动成功可提前继续，失败直接回退，超时按缓存/默认值继续。后台 Task 不因该超时取消，自动稍后返回失败/CD 时仍可开始；永远没有自动完成证据就不强制。
- 不因 Loading 重开、后台 getter 再次发布或业务超时重复安排启动强制。GM 手动入口在自身剩余 5 秒内等启动业务 Task 后，仍保留额外一次手动强制；本次未把 GM 改成 10 秒。
- 结果沿原 `OnRemoteConfigChanged → JSON/主线版本校验 → latestValidConfigs → PersistResolvedConfigs → ConfigManager.RemoteConfig.TrySetRawBatch` 流保存。缓存尚未准备时先暂存，清理/默认值提交后再保存；失败或无效内容保留旧值。
- LoadManager 先完成本轮来源解析与缓存提交，再初始化 Luban 与首次 Draw/IWanner。本节等待策略完成时，后台成功只保存缓存，尚未刷新已经初始化的 Draw；该缺口已由第 31 节补齐游戏侧实现。按需读取 RemoteConfig API 的其它消费者后续能读取更新值，不能据此推断所有既有业务已安全即时刷新。
- 应用退出或关闭 Domain Reload 的重新运行会使旧跟踪失效；尚未派发的旧任务不得再强制，已进入原生请求的任务不能取消，返回后不得参与新一轮业务判断。
- 修改文件为游戏侧 RemoteConfigService、GameRoot、本设计及当日记录；GameRoot 保留成就注册，只覆写既有同步初始化入口采集快照。不修改 SDK/Packages/GF、最小拉取间隔、SDK 原生 30 秒设置、Prefab 或手机安装包。

### 自动完成证据与取消边界

- 只读 SDK `GuruSdk.DispatchInitializeSync()` 先等待 `RemoteConfigManager.Prepare()`，后调用 `_guruRoot.DispatchSyncInitialize()`；自动拉取在随后的 `DispatchInitializeAsync()` 才派发。游戏侧在 `GameRoot.Initialize()` 通过 `.Take(1)` 捕获此时的重放 getter，不额外等待初始化或发请求。
- SDK Prepare 和请求结束分别创建新 `FirebaseRemoteConfigGetter`；原回调计数/派发返回判断可能把延到 Unity 下一帧的 Prepare 回调认作自动完成。现在缓存快照未捕获或 latest getter 与快照相同都不完成自动跟踪，不同活动 getter 才完成；Prepare 未发布时也可用 Inactive 快照识别之后的活动失败结果。
- 标准 `Task.Delay` 不依赖 timeScale 或 PlayerLoop 计时，但处理、UI 和落盘仍要 Unity 主线程推进。5 秒仅限制本服务等待，SDK 前置初始化、热更、配置表、主线程阻塞和同步保存不是整页 5 秒承诺。
- 10 秒“取消”仅结束游戏侧等待并解除临时订阅；只读 SDK 的公开 ForceFetch 没有 CancellationToken/Abort，底层请求及 SDK 队列会继续。长期订阅保留，晚到有效 Success 仍保存，不重发。若必须 10 秒真正中断原生网络请求，需要 GuruSDK 维护方交付可取消 API，游戏侧无法保证。
- ForceFetch 绕过客户端缓存有效期，不绕过 Firebase 服务端节流。自动失败/Throttled 也只强制一次，不循环重试；成功新鲜度受设备时钟和 Firebase FetchTime 精度影响。

### 验证与回滚

- 两个实际修改脚本（RemoteConfigService、GameRoot）使用本地真实 Unity/游戏/SDK 引用进行定向 Roslyn 编译：退出 0、0 错误、1 条 CS0436 警告。该警告来自验证同时引用旧 Assembly-CSharp.dll 和当前服务源码，编译器明确选择当前源码；此证据不是完整 Unity 导入/IL2CPP 构建。
- 本地 .NET 验证通过反射调用本轮编译服务中的真实 `WaitForRequestWithinBudgetAsync`、`ShouldForceFetchAfterAutomaticResult`、`TryCompleteSdkAutomaticFetchTracking`，24 项断言通过。5 秒未完成任务约 5.001 秒结束业务等待，连续阶段共用预算约 5.008 秒；独立强制任务约 10.010 秒结束等待，底层 Task 仍可晚到完成。条件覆盖新鲜成功不强制、历史 Success/失败/缺失或无效 FetchTime 强制、Pending 不推断；身份覆盖缓存快照尚未捕获、同一 Prepare getter 不完成、不同活动 getter 完成及 Inactive Prepare 后活动失败完成。模拟 getter 只用于调用实际条件/身份方法，没有运行 Firebase 或 R3 主线程投递。
- 16 项源码核对通过：独立 5/10 秒预算、移除 35 秒计时、先自动后条件强制、Loading 在后台 join 前返回、游戏侧同步快照入口、按身份完成、可取消排队和释放门、临时订阅释放、长期回调保存、原启动顺序。定向四文件 `git diff --check` 通过。
- 验证产物保存在 `C:/Users/admin/AppData/Local/Temp/brainpunk-remote-five-second-20260918/`：`compile.rsp`、`compile.log`、`budget-check.log`、`Program.cs`、`StaticCheck.ps1`、`static-check.log`、`runtime-change.diff`；全项目构建仍受第 29 节 `NETSDK1004` 阻断，本轮未执行依赖还原或改写只读目录。
- 关键源码复查：`RemoteConfigService.cs:126` 的同步快照入口、`:221` 的身份判定、`:246` 的后台协调、`:308` 的强制条件、`:373` 的 Loading 预算、`:469` 的正常入口提前返回、`:614` 的等待辅助方法；`GameRoot.cs:30` 复用 SDK 同步 Initialize。保存链复用 `ConfigManager.RemoteConfig.cs:194` 的批量事务入口。
- 真机验收矩阵：自动新鲜成功（无强制，提前继续）；客户端 CD（一次后台强制，Loading 不等）；自动失败（立即回退且后台一次）；自动晚于 5 秒失败（先进入游戏，随后后台一次）；自动永无回调（5 秒回退且无强制）；强制无结果（10 秒结束业务等待且不重试）；强制晚到成功（长期回调保存）；缓存回调延到派发后（不提前强制）；退出/Domain Reload 关闭重启；并发 GM/Loading；暂停超过预算后恢复；首次 Draw/IWanner 在本轮配置提交之后初始化。
- `uncertain`：上述真实 SDK/Unity 时序、缓存落盘、原生取消和 Android/iOS/IL2CPP 尚未用本轮新版包验证。SDK 没有请求来源 ID，网络恢复或其它 SDK 调用者的活动 getter 也可能进入跟踪；游戏侧门不控制这些 SDK 内部调用。Fetch/Activate 原生异常可能只暴露历史 Success，不能把 LastFetchStatus 单独当作原生全部步骤成功证据。
- 回滚条件为重复强制、缓存覆盖错误、门无法释放、初始化识别不成立或业务读取异常。只回滚本轮游戏侧差异和当前说明，保留其它任务文件与记录；若恢复第 29 节还需同步恢复对应自动跟踪策略，不改 SDK、不清玩家存档。

## 31. 2026-09-18 补齐回调后的 Draw 运行时刷新

### 已确认需求与实施边界

- 用户指出第 20.5 节已经要求配置更新后刷新 Draw，随后明确要求“先补充回调更新 Draw，然后评估风险”。第 30 节的“不自动重排”是当时实际缺口，不能当作覆盖原需求的新产品约束；此前完整交付结论已撤回。
- 本轮按第 20.5 节补齐：有效 Draw 候选先验证，在同一主线程同步段保存成功后切换排序版本，刷新现有选关副本与主页 Draw 开关/星数；内容相同不重复保存或刷新。初次初始化仍由 Loading 在配置落盘和 Luban 就绪之后执行。
- 当前关进入时固定真实 DrawLevelData 与排序版本，Reset/Replay/星星/打点不再通过可变化的序号或本地排序倒查真实内容。下一关继续按新列表中的显示序号推进，列表缩短则返回有效选关页；不迁移历史进度。
- 选关广告、Draw HUD 墨水广告、暂停和进关/重置转场未完成时暂存最新 Draw JSON，既有 GameManager Update 在安全边界尝试提交。其它顶层 key 可独立提交。选关请求验证版本及真实关身份；广告回调捕获发起时的对象并在完成前保护排序。
- 复用现有 GameManager、DrawPlayCtrl、UIManager 的已持有引用和局部 API；不新增全局 PlayEvents、Prefab 扫描或资源下载，不修改只读 Packages/GuruSDK/GF。
- 与《关卡打点生命周期约定》一致：修正真实内容身份来源，不改变事件序列、revive、play_count、Reset/Replay 分类或成功/退出清理契约。旧源码中的“一次性序列查询”注释将因新增刷新与事实冲突而仅做对应调整。

### 验证与风险评估

- 实现入口为 `RemoteConfigService.PersistResolvedConfigs`：Draw 已初始化后先构建完整候选，拒绝非布尔 enabled、空/非字符串序列、重复或未知 ID、空资源路径、仅等待关。有效序列保证 `20999` 唯一且位于末尾；enabled 关闭或缺失时采用本地排序。无效候选不写入 Draw 缓存，并恢复内存中上次有效值；不把局部过滤后的半份远端排序当作成功。
- 交互中只保留最新有效 Draw 候选，批量保存排除 Draw key，其它 key 可更新；同一成功回调重新返回当前缓存可撤销旧候选，GM 超时的旧缓存回退不能撤销候选。保存失败保留旧缓存/排序，不自动循环重试磁盘；后续有效回调或手动入口可重试。初次 Loading/Draw Init 沿用原逻辑，本次严格候选检查只覆盖初始化后的更新。
- 当前真实内容由 `DrawPlayCtrl.TryBeginLevelTransition` 在接受进关时固定；`TryGetCurrentLevelData` 供初始化墨水、提示、星星结算及实际 ID 打点读取。Reset/Replay 清理关卡实例时不清内容身份，完整 Deactivate 才清理。正常下一关仍按当前显示序号在新列表中取后一项；正在运行的 TBC 不因新列表增长进入普通关。若结算时列表没有后项、胜利页随后扩展列表，受理下一关时补齐新目标 CurrentLevel/UnlockLevels，仅发生变化才保存，不增加通关/星星/奖励。
- 选关刷新沿现有副本/分页/item 绑定完成，主页复用已有 Draw 入口引用，不重开首页或播放入场动画。选关奖励回调捕获排序、真实数据和 generation，结束前阻止分页/关闭及排序提交；HUD 暴露已有广告等待状态供控制器判断。新 UI API 均有运行态入口保护，不新增编辑器 DB/单例初始化路径。
- 8 个游戏脚本发生本轮变更：RemoteConfigService、GameManager、DrawGameLevelCtrl、DrawPlayCtrl、DrawGame_ChoiceLevelPanel、DrawGame_LevelChoiceItem、DrawGame_GamePlayPanel、StartPanel。第 30 节 GameRoot 与 5/10 秒等待策略保留；不修改 Packages/GuruSDK/GF、Prefab 或配置表。
- 使用项目生成的源文件清单、实际 Unity/项目引用执行 Roslyn 编译：0 错误、1791 警告，其中 CS0649 为 1687 条；没有建立同条件基线，不能把全部警告认定为既有。另编译去掉 DEBUG_MODE 的同源验证程序集以避免 .NET 测试调用 Unity 原生日志；两次均退出 0。此证据不等于 Unity 导入、Player 编译、IL2CPP 或真机验证。
- 反射调用实际编译代码的 31 项断言通过：候选不改活动序列、重排/同序列 generation、等待关、无效输入拒绝、旧版本及旧身份拒绝、当前内容在全局重排/移除后保持、TBC 不前进，以及进关/重试/暂停门禁。当前内容通过注入进入时字段验证查询边界，未在 Unity 实际运行 Reset/Replay 或结算。另 17 项源码断言覆盖保存前校验、保存后切换、无异步让出、延期排除 Draw key、候选恢复、下一关受理时进度同步及 UI/广告调用链；这些属于静态证据。
- 资源只读检查发现本地表 101 行、默认排序 100 项，排序 ID 无重复且均有数据行；`20070–20083、20100` 共 15 个 ID 在当前工作区找不到对应 `DLevel_*.prefab`。用户已确认这些是正在开发的关卡，明确本次先不处理，不作为本轮 Draw 刷新的阻断项；不删除配置、不补造资源。新增解析器只验证映射/非空路径，不代表发布资源验收。
- 验证产物：`C:/Users/admin/AppData/Local/Temp/brainpunk-draw-remote-refresh-20260918/` 中的 `compile-filtered.rsp`、`compile.log`、`compile-nodebug.rsp`、`compile-nodebug.log`、`Program.cs`、`draw-check.log`、`StaticCheck.ps1`、`static-check.log`、`resource-audit.json`、`runtime-change.diff`。测试/构建输出只写临时目录，没有恢复或构建只读依赖。
- 完整七维评估、分场景证据和回滚见 `Assets/Game/Scripts/Level/DrawLevel/DrawGameLevelCtrl功能与风险评估.md` 第 12 节；选关 UI 记录见同名面板功能文档的 2026-09-18 小节。
- `uncertain`：真实保存 false/磁盘异常、选关页与星星可见效果、广告成功/失败/取消/无回调、两次 Reset 后成功再 Replay、已接受下一关转场期间更新、退出重启，以及新版 Android/iOS 横屏、暂停恢复、资源包与 IL2CPP 尚未实测。保存失败的回滚仅指内存缓存引用；现有 JsonSaveProvider 直接写文件，不能宣称杀进程/断电具有崩溃原子性。
- 已知保留风险：广告/暂停/转场门禁若始终不释放，候选保持延后；云控排序变化沿用按显示序号的进度，已通关位置可能换成另一实际关，这是第 20.5 节确认的产品规则；经济价格、其它广告参数、主线/IWanner 不由本次 Draw UI 刷新保证同步。
- 回滚触发：当前真实关或星星归属错误、广告奖励丢失/错发、保存失败后缓存与排序不一致、选关刷新后不能进入、旧转场目标改变。只撤销本节对应的 8 个游戏脚本差异，保留第 30 节的 5/10 秒与 GameRoot 自动跟踪，不整文件覆盖其他任务改动；不清玩家存档、不操作 SDK/GF。

## 33. 2026-09-18 iOS 1.14.1：17:40 后断网与恢复联网日志核对

### 范围、来源与行为边界

- 用户反馈 iPhone 包远程拉取成功后 Draw 未更新，要求仅分析报告中 17:40 之后的测试，并确认游戏内恢复联网是否有重新拉取及应用结果的逻辑。
- 原始文件：`C:/Users/admin/Documents/WXWork/1688857075821642/Cache/File/2026-09/com.brain.tricky.puzzle.games.test.quest_BP-YYGFX_debug_log(11).zip`。ZIP 仅含 `guru_app/fusion-ads.log` 与 `gurusdk/com.brain.tricky.puzzle.games.test.quest 2026-09-18--16-59-23-214.log`；以下行号均指后者原始文件。
- 第 2477、2481 行等记录版本 `1.14.1`、时区 `Asia/Shanghai`。筛选使用外层日志时间；FetchTime 字段比对应外层时间小 8 小时，按 UTC 解释与本地时间吻合。报告没有确认 IPA 构建号、源码提交或实际 SDK 源码版本。
- 本轮仅分析日志与只读核对源码，追加说明；未改变 5/10 秒策略、回调、Draw、SDK、资源、存档或手机包。当前工作区为 `feature/1.15.0_common`；与 `feature/1.14.1_common` 对比，RemoteConfigService、GameSDKService、DrawGameLevelCtrl 三个相关脚本没有差异。这不能证明手机包包含相同源码。

### 可观察事实

| 本地时间 | 网络及拉取记录 | 原始日志行 |
| --- | --- | --- |
| 17:41:32 | Wi-Fi 切换为 None | 2496、2509 |
| 17:42:35 | 断网启动；先读取历史 09:02:12 Success，随后明确抛出离线异常，最终 Failure/Error | 2552、2567、2789–2790、2864–2876 |
| 17:44:18 | 断网再次启动；历史 FetchTime 未推进，最终离线异常及 Failure/Error | 3754、3769、3973–3974、4032–4044 |
| 17:45:13 → 17:45:14 | None → Wi-Fi；约 1 秒后 result:True、状态 Success、FetchTime 推进到 09:45:14 | 4901、4906、4925–4926 |
| 17:45:53 → 17:45:57 | 再次断网并启动；历史 09:45:14 被读取，随后离线异常及 Failure/Error | 5266、5271、5367、5414、5611–5612、5723–5735 |
| 17:46:57 | 断网再次启动；最终离线异常及 Failure/Error，FetchTime 仍为 09:45:14 | 6598、6634、6649、6853–6854、6934–6946 |
| 17:48:13 → 17:48:14 | None → Wi-Fi；约 1 秒后 result:True、状态 Success、FetchTime 推进到 09:48:14 | 7801、7806、7825–7826 |

- 四次离线启动的明确异常均为 `Firebase.FirebaseException: The Internet connection appears to be offline.`。每轮可见两个任务结果打印，但没有调用来源标识，不能仅凭先后顺序认定哪个属于普通请求或游戏侧 ForceFetch。
- `RemoteConfig UpdateInfo Success!` 前缀不是请求成功判据：第 4044、5735、6946 行同时含此前缀与 `Failure Error`；第 3973、6853 行的 `result:True` 后也仍是历史 Failure/Error。须联合查看状态及 FetchTime，不能用日志单词 Success/True 判断最新云控已应用。
- 两个 ZIP 条目全文件检索中，`RemoteConfigService`、`DrawGameLevelCtrl`、`Draw update`、`LoadManager`、`Applied remote sort`、`drawgame_config`、`ForceFetch` 的匹配数全部为 0。未取得业务 JSON、保存结果、刷新版本或 Loading 阶段耗时。

### 直接结论、源码对应与未知项

- 可以确认 17:45:14、17:48:14 两次恢复联网后 SDK 记录了成功且 FetchTime 前进；这不是一直没有远程结果的情况。每次约 1 秒是“恢复网络记录到成功记录”的间隔，不是完整 Loading 耗时，也不是每个原生请求的精确耗时。
- 当前只读 SDK `RemoteConfigManager.cs:275–282` 有恢复联网入口：None 转有网且距本进程上次该入口派发至少一小时才调用 `FetchAndActivate()`；`:238–252` 在初始化拉取结束后订阅网络变化；`:163、403–404` 发布 getter 并投递主线程观察者。这说明源码存在网络恢复拉取和通知机制，不能据此断言该 IPA 的每条通知均已交付。
- 当前游戏侧 `RemoteConfigService.cs:72–80、159–205` 在 SDK 初始化前注册长期订阅，Loading 超时不取消该订阅；有效结果经 JSON 校验进入 `PersistResolvedConfigs`。Draw 已就绪时在 `:838–886` 比较原 JSON、验证候选、检查广告/暂停/转场门禁，保存成功后应用排序并刷新界面；延期候选由 `:895–903` 及 GameManager.Update 继续处理。
- Draw 更新仍要求符合配置语义：`enabled` 未开启时沿用本地排序；无效、重复或未知 ID 等候选被拒绝；相同 JSON/排序可能没有可见变化；当前正在玩的真实关身份按进入时固定，不因远端排序变化重建。见 `DrawGameLevelCtrl.cs:170–278`、`DrawPlayCtrl.cs:115–122、304–307`。
- `uncertain`：两次成功是否命中期望的 Firebase 条件、返回了什么 Draw JSON、游戏侧是否收到通知、是否被拒绝/延期/保存失败、手机是否包含迁移提交 `e2e81de32`。ZIP 缺少这些日志不能当作回调未发生的证据，也不能确认 Draw 已刷新。
- 手机日志的 Prepare getter 在线程池输出，异常栈显示 `FetchAndActivate`；当前 SDK 源码对应路径回主线程且执行体为 `CompleteFetchAsync`。这是应核对实际 SDK 版本的差异线索，不据此判定具体版本、缺少游戏提交或 SDK 故障。

### 验证、后续证据与回滚

- 通过只读 ZIP API 读取两条日志，按 17:40 筛选，检查上述结果、原始行号与标记计数；未安装或运行新包，未做新的 iOS 复现。
- 留存包含 ZIP SHA256、原始行号和相关记录的摘录：`C:/Users/admin/AppData/Local/Temp/brainpunk-ios-remote-20260918-1740/远程拉取证据摘录.txt`。摘录不保留账户认证/推送记录。
- 下一次应采集同一轮的 iOS Unity Player 日志，关联 `RemoteConfig callback: validKeys=..., cacheSaved=...`、`Draw update rejected/deferred`、`Applied remote sort`，以及实际收到的 Draw `enabled`、`level_sort` 和 IPA 构建提交；先定位缺失环节，再决定是否修改。
- 本轮无运行时变更；回滚仅移除此节及当日对应记录，不回滚代码、不清缓存、不改 SDK。

## 34. 2026-09-18 补齐手机导出报告中的云控与 Draw 诊断

### 需求、范围与日志通道

- 用户要求对应更新时补充日志，并明确下次要在报告中看到。本轮修改当前 `feature/1.15.0_common`；询问是否同时同步 1.14.1 后，用户回复“暂时不用”，因此没有迁移分支、提交或推送。手机必须使用包含本次改动的新构建；当前 1.14.1 IPA 不会自动获得这些日志。
- 原问题：RemoteConfigService 使用 Unity Debug，Draw 排序使用 Bear.Logger，提供的 SDK/广告 ZIP 没有这些业务记录。当前只读 `Packages/com.guru.sdk.framework.utils/Runtime/Log/Log.cs` 中，`Log.I/W/E → LogInternal → persistentLog` 对符合持久化级别的记录写入 `gurusdk` 日志；日志初始化前暂存队列，初始化时写出。
- 将 RemoteConfigService 既有输出接入 Guru 日志，新增 Draw 专项诊断也调用现有 Guru 日志公开接口。这是针对手机导出通道的局部选择；不修改 SDK/Packages/GF、不改日志全局级别、不改其它玩法日志、不安装新导出组件。
- 影响脚本：RemoteConfigService、DrawGameLevelCtrl、DrawPlayCtrl、GameManager、DrawGame_ChoiceLevelPanel、StartPanel。只增加诊断字段/输出；5 秒 Loading、10 秒后台强制、请求次数、配置校验/保存/延期、排序和 UI 刷新调用顺序保持原实现。

### 可检索记录与业务流

- 启动标记：`[RemoteConfigService] diagnostics=draw-refresh-v1`，同时记录应用版本与平台，用于检查测试包是否包含本次诊断。
- 每次长期通知递增 `callback` 编号，记录 `stage=received`、网络、缓存就绪与完整 Fetch 状态。空 getter、非活动/失败通知、JSON/版本拒绝、启动前暂存均明确记录。收到成功 getter 时记录实际 `drawgame_config` 原文与字符数，换行转义，不额外采集账号或设备凭据。
- 提交流程记录 `source`（callback / startup_retained / loading_or_manual / deferred）、当前回调编号和最近观察的 Draw 回调编号。相同 JSON、候选拒绝、Draw 尚未初始化、缓存保存成功/失败、延期与恢复都输出独立 `stage`；`allConfigsCommitted=false` 可能表示 Draw 被拒绝/延期，不能单独等同于磁盘失败，应看 `cache_commit`。
- 延期输出已有的 active、opening、attemptTransition、paused、choiceAd、inkAd 状态；仅在登记/恢复候选时输出，不在 Update 等待期间每帧输出。
- `[DrawRemoteConfig]` 记录首次序列、相同序列或实际切换，包括 generation、数量和真实 ID 顺序；服务记录 `draw_applied` 的前后 generation。相同排序可能只刷新其它 Draw 配置，不增加 generation。
- `[DrawRemoteConfigUI]` 记录面板是否存在、选关是否就绪/打开/等待广告、首页是否关闭，以及刷新后 generation、数量、页码与首页入口状态。`view_dispatch_complete` 只证明调用返回，须结合面板的 refreshed/skipped 记录；日志不等于实际屏幕渲染验收。
- 回调内异常以 `callback_exception` 和同一编号写入 SDK 错误日志，再原样抛出，保留原失败语义。当前运行关卡身份仍保留，不因诊断代码重建关卡。

### 验证、验收与风险

- 使用原项目源文件清单及真实 Unity/SDK 引用编译当前代码，常规与去 DEBUG_MODE 的两次 Roslyn 编译均 0 错误；常规编译 1791 条警告。本次不恢复或构建只读依赖。
- 复用已有 31 项 Draw 方法回归，新增 4 项实际 SDK 持久化队列验证，共 35 项通过：空 getter 回调带编号进入持久化队列、实际 ApplyRemoteConfig 进入同一队列、包含实际生效 ID 顺序、相同序列有独立日志。验证只关闭测试进程中的 Console 输出，使用真实 SDK Log 类的初始化前持久化队列，没有把模拟 logger 的结果称作实际 SDK 结果。
- 验证产物：`C:/Users/admin/AppData/Local/Temp/brainpunk-remote-export-log-20260918/` 下的 compile.log、compile-nodebug.log、draw-log-check.log、persistent-queue-sample.log、Program.cs 及响应文件。限定六个脚本 `git diff --check` 通过；原工作区资源改动保留。
- 下一包必须验收：导出 ZIP 中能检索到 `diagnostics=draw-refresh-v1`；断网后恢复联网，能按时间及 callback 编号串起 received、draw_payload、cache_commit、draw_applied/明确拒绝或延期原因；再检查 DrawRemoteConfig 的实际序列与 DrawRemoteConfigUI 的结果。未完成该检查不得称“手机导出日志已验证”。
- `uncertain`：尚未生成并运行新 iOS/Android 包，未实际读取本轮新增日志的手机 ZIP；原生文件写入、归档及轮转仍依赖 SDK 现有持久化设置和导出流程。SDK 关闭日志或提高级别时，Info 记录可能被过滤。本轮没有为了诊断强制改动这些设置。
- 日志包含 Draw 业务配置和排序，更新次数较多时会增加日志体积；本轮无逐帧打印。回滚只撤销诊断输出、关联字段及诊断状态属性，恢复原日志调用；不撤销既有云控/Draw 刷新实现，不清存档或 SDK 缓存。

## 35. 2026-09-20 `autoShowChestGiftLevel` 运行时云控闭环

- 新字段属于既有 `level_config` 内部业务字段，不增加 RemoteConfig 顶层 key，也不修改 `RemoteConfigService`、`ForceFetch`、缓存、版本门禁、Loading 等待或 DrawGame 刷新链。
- `ConfigManager.RemoteConfig.GetAutoShowChestGiftLevel()` 在运行态只接受远端 JSON Integer；缺字段、解析失败或类型不符时回退 Luban `GlobalConst.AutoShowChestGiftLevel`。编辑态直接使用本地配置，不进入运行时 DB/RemoteConfig 链；消费者把非正数视为关闭自动弹出。
- 运行时消费者只位于 `GiftManager.TryOpenFirstDayVictoryChestGift()`：首个当地游玩日、首次通关、完成关卡等于配置、Daily Chest 总开关开启且胜利面板创建成功时，才请求 ChestGiftController 打开当天有效礼包。
- 验证确认本地 JSON、Guru YAML 与 Guru 生成 C# 的字段名、整数类型和值一致；使用当前 Unity Bee 响应文件与同版本 Roslyn定向编译运行时程序集为 `0 error / 113 warning`。真实 Firebase 不同值、缺字段、错误类型与 Android/iOS 设备行为仍为 `uncertain`。回滚时必须同时移除 Getter、字段常量、Guru 默认值、生成器白名单和消费者。

## 36. 2026-09-20 移除仓库内 GuruSDK RemoteConfig 源码覆盖

### 需求与影响范围

- 用户要求检查并还原 `Packages` 中的修改，同时说明被还原功能。本次合并相对第一父提交只在 `Packages` 下新增了 `com.guru.sdk.framework.core/Runtime/Firebase/RemoteConfig/RemoteConfigManager.cs`；没有发现其它 Package 文件差异。
- 该文件是完整的 GuruSDK `RemoteConfigManager` 游戏仓库覆盖实现，不是游戏侧业务脚本。此次将它从 Git 索引移除，同时使用历史可逆补丁恢复 SDK 原版实体文件；`Packages` 的版本控制状态和运行所需文件都回到合并前边界。没有修改 `Assets/Game/Scripts/SDK/Services/Remote/RemoteConfigService.cs`、GuruSDK 其它文件、GF、配置表或玩家存档。

### 还原前后行为与调用流

- 还原前的覆盖实现将普通拉取和强制拉取统一进入 `QueueFetch(bool force)`：请求串行执行，同类型未完成请求合并；强制请求使用 `FetchAsync(TimeSpan.Zero)`，Firebase 原生超时为 30 秒，并在激活后发布新的配置 getter。网络从离线恢复时也可触发普通拉取。
- 还原后，当前本地 GuruSDK 原版使用单个 `isFetchAndActivating` 门禁：任一拉取占用时，后来的普通或强制请求直接跳过，不排队；初始化配置 30 秒超时和 Deployment 最小缓存间隔，`ForceFetch()` 配置 20 秒超时及最小间隔 0。恢复联网入口仍存在。这些是当前原版实体源码的直接事实；最终 Player 是否打入同一版本仍须由构建和真机日志确认。
- 游戏侧链路保持：`RemoteConfigService` 仍等待 SDK 自动结果最多 5 秒，并在既有条件下启动最多 10 秒的后台强制等待；有效通知仍经 JSON/版本校验、缓存保存和业务消费者应用。删除 SDK 覆盖可能改变这些等待背后的底层请求时序，但没有直接移除游戏侧回退、长期订阅或 Draw 刷新代码。

### 验证、风险与回滚

- 静态核对：历史补丁清单记录的原文件规范化 SHA-256 为 `b46286610b8d6e2b3678b8039f0f2445669193b14f4ad70af0bd9ad7db665924`，恢复后的实体文件哈希完全一致；该文件已由现有 `.gitignore` 接管。`git diff HEAD^1 -- BrainPunk/Packages` 为空，证明相对本次合并前第一父提交没有 Package 版本控制差异；`git diff --cached HEAD -- BrainPunk/Packages` 仅表现为移除上述一个强制跟踪文件。现有 `Skin.xlsx`、`guru_gf_settings.txt` 等工作区改动未触碰。
- 已使用 Unity 当前 Bee 响应文件分别编译 `Guru.SDK.Framework.Core` 与 `Assembly-CSharp`，两者均为 0 error；编译输出仍有项目既有警告。尚未运行 Unity 导入、Player/IL2CPP 构建或 Android/iOS 真机拉取，因此忙时跳过、实际网络超时及恢复联网在发布包中的表现仍为 `uncertain`。发布前应以构建所解析的 GuruSDK 版本和带诊断日志的真机测试为准。
- 若后续确需恢复这份游戏仓库覆盖，可从本次合并提交单独恢复已打补丁版本；由于这会重新让游戏仓库直接维护 GuruSDK 源码，应先由 SDK 维护方确认版本、升级冲突和回归范围，不应连带回滚游戏侧 RemoteConfigService。

## 37. 2026-09-22 Draw 排序初始化与运行时校验统一

- Android 诊断日志确认新鲜 99 项 `drawgame_config.level_sort` 在 Draw 初始化后到达时，因数组为整数而被旧运行时校验拒绝；
  iOS 同配置在 Draw 初始化前到达并通过旧 `List<string>` 转换生效。两端收到的关卡数组内容和类型相同，差异是配置到达时点。
- `DrawGameLevelCtrl` 现将本地初始排序、初始化阶段远端排序和初始化后候选统一交给同一构建器。合法 ID 可为 JSON Integer
  或 String，统一转换成字符串后执行重复、关卡映射、资源路径、普通关非空及 `20999` 唯一末项校验；其它 JSON 类型仍拒绝。
- 初始化候选无效时保留已验证的本地排序；运行时候选无效时继续由 `PersistResolvedConfigs()` 排除 Draw key 并保留旧缓存/序列。
  本次未修改 `RemoteConfigService`、ForceFetch、5 秒/10 秒预算、缓存提交顺序或 UI 刷新门禁。
- 实际源码两种编译定义均为 0 错误；20 项候选断言覆盖线上 99 项整数、等价字符串、混合 ID 和非法类型/重复/未知/空序列。
  Unity PlayMode、IL2CPP、覆盖安装及 Android/iOS 真机回调仍为 `uncertain`。完整行为、风险和回滚见
  `Assets/Game/Scripts/Level/DrawLevel/DrawGameLevelCtrl功能与风险评估.md` 第 13 节。
