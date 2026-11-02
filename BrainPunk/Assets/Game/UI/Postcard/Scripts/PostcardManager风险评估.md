# PostcardManager 临时星星计数风险评估

## 评估范围

- 评估日期：2026-07-01
- 变更入口：`PostcardManager` 注释驱动实现
- 涉及文件：`PostcardManager.cs`、`GameVictoryPanel.cs`、`GameManager.cs`、`Postcard_Star.cs`、`Postcard_VictoryStarGroupCtrl.cs`、`PlayCtrl_Success.cs`
- 涉及系统：Bear 事件订阅、Postcard 存档数据、胜利面板创建流程

## 原始行为基线

- `PostcardManager` 空实现，不监听事件，不集中处理 `DB.Postcard.LevelCollectedStar` 或 `DB.Postcard.LevelWaitingAnimStar`。
- `PostcardGetStarEvent` 已存在，星星收集完成后由 `Postcard_Star` 派发，并由 `GamePlayPanel` 用于 HUD 星星块刷新。
- `GameVictoryPanel.Create(LevelData data, ...)` 负责打开普通或奖励版胜利面板，并持有本关 `LevelData`。
- `LevelCollectedStar` 已存在于 Postcard 存档结构，作为按关卡保存的永久星 id 列表。
- `LevelWaitingAnimStar` 已存在于 Postcard 存档结构，作为按关卡保存的胜利面板等待动画计数。

## 变更后链路

1. `GameManager.Init()` 创建 `PostcardManager`。
2. `PostcardManager` 构造时订阅 `PostcardGetStarEvent`。
3. 关卡内星星触发时，`Postcard_Star` 调用 `TryCollectLevelStar(...)` 写入 `LevelCollectedStar[levelId]` 并保存。
4. `PostcardGetStarEvent` 触发后，管理器读取当前关卡 id，并累加 `DB.Postcard.LevelWaitingAnimStar[levelId]`。
5. 首次通关默认星由 `PlayCtrl_Success` 调用 `TryGrantVictoryStars(...)`，管理器写入 `LevelCollectedStar` 并累加 `LevelWaitingAnimStar`。
6. 胜利面板创建时，`Postcard_VictoryStarGroupCtrl` 通过 `GetLevelCollectedStarIds(...)` 读取永久星 id，通过 `ConsumeCollectedStarCount(...)` 消费等待动画计数。
7. 消费 API 返回本轮计数并清空该关卡临时数据。
8. 失败、重开或退出关卡不会清空临时计数，计数保留到后续胜利消费或显式清理。
9. `GameManager.OnDestroy()` 释放 `PostcardManager` 事件订阅。

## 已缓解风险

- 重复订阅：`PostcardManager` 内部使用 `EventsUtils.ResetEvents(ref subscriber)` 订阅前清理旧订阅。
- 空关卡 id：所有公开 API 和事件处理都会拒绝空白 `levelId`，不会写入空 key。
- 空存档字典：`TryGetLevelCollectedStar(...)` 会在字典为 null 时补建新字典。
- 永久记录误删：清理等待动画计数的 API 只作用于 `LevelWaitingAnimStar`，不修改 `LevelCollectedStar`、`CompletedId`、`CurrentIndex` 或 `CurrentCollectedPuzzledId`。
- 失败/重开/退出丢计数：规格已明确这些路径不清空临时计数，避免永久收集已完成但胜利表现计数提前丢失。
- 胜利数据串关：胜利面板按 `GameVictoryPanel.Create(...)` 传入的 `LevelData.Id` 消费对应关卡，消费后清空该关卡临时条目。
- 生命周期残留：`GameManager.OnDestroy()` 释放 `PostcardManager` 事件订阅。
- 业务层直连 DB 扩散：`Postcard_Star`、`Postcard_VictoryStarGroupCtrl`、`PlayCtrl_Success` 已改为只调用 `PostcardManager` 公开 API，直接 `DB.Postcard` / `DB.SavePostcard()` 访问集中在 `PostcardManager`。
- 永久星保存失败：`TryCollectLevelStar(...)` 和 `TryGrantVictoryStars(...)` 保存失败时会回滚本次新增 id；`TryGrantVictoryStars(...)` 还会回滚本次新增等待动画计数。

## 风险项

### P1-1 `CacheCollectedStarCount(...)` 保存失败后的临时计数内存态可能残留

- 问题：`CacheCollectedStarCount(...)` 写入 `LevelWaitingAnimStar` 后调用 `DB.SavePostcard()`；若保存失败，当前实现记录 warning 并返回 false，但不回滚内存字典。
- 影响：本次进程内胜利面板仍可能消费该计数；重启后该计数可能丢失。
- 当前处理：事件处理会在保存失败时输出 warning，避免静默失败。
- 后续验证：需要 PlayMode 注入或断点模拟 `DB.SavePostcard()` 返回 false，确认等待动画计数是否需要与永久星写入一样做回滚。

### P1-2 事件语义依赖 `PostcardGetStarEvent` 继续只代表一次星星收集完成

- 问题：如果后续其它系统复用 `PostcardGetStarEvent` 但不是星星收集完成语义，`PostcardManager` 会误累加。
- 影响：胜利面板可能得到错误的本轮新增星星数。
- 当前处理：本次不新增事件，按现有 `Postcard_Star` 派发语义接入。
- 后续验证：修改 `PostcardGetStarEvent` 派发点时需要同步检查 `PostcardManager`。

### P1-3 PostcardManager 初始化时序仍需 PlayMode 验证

- 问题：`Postcard_Star.Awake()` 现在通过 `GameManager.Instance?.Postcard` 判断已收集状态；静态链路显示 `LoadManager.EnterGame()` 先调用 `GameManager.Instance.Init()`，`OnPostProcess()` 后续才 `ReadyToPlay()` 并创建关卡，但 Unity 场景特殊进入或调试直开关卡仍需验证。
- 影响：如果星星 Awake 时 manager 缺失，已收集星可能短暂保持可用状态。
- 当前处理：缺 manager 时安全返回未收集，不直接访问 DB 或初始化 manager。
- 后续验证：需要 Unity PlayMode 覆盖正常启动进入关卡，以及开发者直开玩法场景的调试路径。

### P2-1 胜利面板已消费但 UI 表现仍依赖 prefab 绑定

- 问题：`Postcard_VictoryStarGroupCtrl.Refresh(...)` 会刷新 toggle 并播放等待动画，但真实表现依赖胜利面板 prefab 是否绑定 `postcard_starGroup` 或能在子节点找到该组件。
- 影响：缺失组件时 `GameVictoryPanel.RefreshPostcardVictoryStars(...)` 只消费等待动画计数，玩家可能看不到胜利星 UI。
- 当前处理：缺组件时仍消费等待动画计数，避免临时计数长期残留；真实 UI 绑定需要 PlayMode/Inspector 验证。
- 后续验证：验证普通胜利面板和奖励胜利面板中 `Postcard_VictoryStarGroupCtrl` 的绑定和动画表现。

## 验证证据

- 静态证据：`PostcardManager.cs` 的原类注释未被改写，新增公开 API 已补中文 XML 注释。
- 静态证据：`Postcard_Partial.Init()` 初始化 `LevelCollectedStar` 与 `LevelWaitingAnimStar`，`DB.SavePostcard()` 入口存在于 `DBManager_Generated.cs`。
- 静态证据：`GameVictoryPanel.Create(LevelData data, ...)` 在面板创建时有 `data.Id` 可用于消费对应关卡数据。
- 静态证据：`LoadManager.EnterGame()` 先调用 `GameManager.Instance.Init()` 和 `DBManager.Instance.Initialize()`，`OnPostProcess()` 后续才调用 `GameManager.Instance.ReadyToPlay()`；`PlayCtrl.CreateLevelAsync(...)` 负责实例化关卡 prefab。
- 静态扫描：业务脚本中直接 `DB.Postcard` / `DB.SavePostcard()` 访问已集中到 `PostcardManager.cs`，`Postcard_Partial.cs` 仍为存档结构定义。
- 静态生命周期扫描：本次新增逻辑未引入编辑器生命周期入口、资源加载、广告、IAP、analytics 或每帧轮询。
- `git diff --check` 已通过，仅有工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过，0 error / 1115 warnings；warning 属于当前工程输出，未逐项审计。

## 未覆盖验证

- Unity Editor PlayMode：首次收集、连续收集、失败/重开/退出后保留计数、胜利消费、重复打开胜利面板。
- 保存失败注入：`DB.SavePostcard()` 返回 false 后，确认内存态计数是否符合预期。
- 开发者直开玩法场景：确认 `Postcard_Star.Awake()` 依赖 `GameManager.Instance.Postcard` 的路径不会让已收集星保持可触发。
- Android/iOS 横屏 smoke：真实设备触摸收集、胜利弹出、保存耗时和 UI 流畅度。

## 回滚路径

- 回退 `PostcardManager.cs` 新增事件订阅和 API 实现。
- 回退 `Postcard_Star.cs`、`Postcard_VictoryStarGroupCtrl.cs`、`PlayCtrl_Success.cs` 中对 `PostcardManager` 新 API 的调用。
- 回退 `GameVictoryPanel.cs` 中的胜利星组刷新和兜底消费调用。
- 回退 `GameManager.cs` 中的 `Postcard?.Dispose()` 调用。
- 删除 `PostcardManager功能说明.md`、`PostcardManager风险评估.md` 及对应 `.meta`。

## 2026-07-10：当前关卡可收集星配置门禁增量评估

### 基线、范围与现有系统

- 基线：`BaseLevelCtrl.TryToCollectStar(...)` 原本不读取 `LevelSumStarCount`，会在共享成功流程中直接进入第 3 颗星去重和收集链路。
- 范围：新增只读 static API `CanCurrentLevelCollectStar()`，并由 `BaseLevelCtrl` 在延迟前后调用；API 会读取永久收集记录，但未修改配置值、存档结构、资源、事件定义或平台流程。
- 复用：当前关卡仍由 `PlayCtrl.Instance.Level.CurrentLevelData` 提供，已收集数量复用 `GetLevelCollectedStarIds(...)` 的去重和有效 id 过滤结果，实际收集继续复用 `IsLevelStarCollected(...)`、`TryCollectLevelStar(...)`、背包和 `PostcardGetStarEvent`。

### 风险项与控制

- P1：延迟期间 `CurrentLevelData` 可能切换。当前在延迟前后双重检查 `LevelSumStarCount > 0`，实际保存前的同步调用栈内会再次基于当前关卡判定。
- P1：永久已收集数量可能已达到 `LevelSumStarCount`。当前门禁先按有效星 id 1 到 3 将配置上限归一化到 0 到 3，再要求有效永久星数量严格小于上限；达到或超过有效上限时不再进入新增星链路。
- P1：`LevelSumStarCount == 0` 仅禁止 `BaseLevelCtrl` 的自动第 3 星收集，不禁止 `PlayCtrl_Success` 按 `LevelVictoryStarCount` 发放首次通关默认星；验收不能只观察胜利面板是否出现星。
- P2：PlayMode 外、当前关卡数据无效、`PostcardManager` 或 `DB.Postcard` 缺失时 API 返回 `false`，采取不收集的安全降级；正常判定只读 `LevelCollectedStar`，不调用保存。

### 验证与未验证项

- `git diff --check` 对两个目标脚本通过，仅有 LF/CRLF 转换提示；敏感关键字静态扫描未发现新增编辑器生命周期、资源加载、RemoteConfig、PlayerPrefs、Instantiate 或全局事件。
- `Assembly-CSharp.csproj` 独立编译通过，0 error / 98 warnings，warnings 未逐项审计。
- 补充已收集数量上限比较后复跑同一编译命令通过，0 warning / 0 error。
- 未验证 Unity PlayMode、零值/正值/大于 3 的异常配置、DB 缺失、已收集数量低于/等于/高于有效上限时的实际 Bag 与事件结果、延迟切关，以及 Android/iOS 横屏流程。

### 回滚路径

- 移除 static API 和 `BaseLevelCtrl` 中两处调用即可；无需回滚配置、DB 数据、prefab 或资源。

## 2026-07-13：历史已通关关卡胜利星补录增量评估

### 基线、范围与现有系统

- 基线：历史通关胜利星补录原本由 `PostcardGuidePopup.FullNeededStarCount()` 调用弹窗内私有方法，只有进入对应引导阶段时才执行。
- 范围：补录主体迁入公开 API `PostcardManager.TryGetAllCompleteLevelStar()`，由 `LoadManager` 在 `ReadyToPlay()` 之后调用；`PostcardGuidePopup` 不再负责遍历历史关卡。
- 复用：关卡映射继续读取 `DB.GameData.PassedLevels`、`LevelSort.Path` 与 `LevelData`，星星写入继续复用 `TryGrantVictoryStars(...)`、`LevelCollectedStar`、`LevelWaitingAnimStar` 和 `DB.SavePostcard()`。

### 风险项与控制

- P1 加载时序：补录依赖 `PlayCtrl.Instance.Level` 与 `GameManager.Instance.Postcard` 已就绪。当前静态顺序是先执行 `ReadyToPlay()`，后调用补录；缺少任一对象时安全返回并记录 warning，但 Unity 特殊启动路径仍需验证。
- P1 加载耗时：每个首次新增星记录的历史关卡会独立调用一次保存。已有星不重复写入，`PassedLevels` 中重复 sortId 会先去重，但大量历史关卡首次补录的移动端耗时尚未测量。
- P1 等待动画语义：补录不仅写永久星，还会累加历史关卡的 `LevelWaitingAnimStar`；后续重玩并打开对应胜利面板时可能播放补录星动画，产品预期仍待确认。
- P2 重复调用：永久星 id 去重使成功保存后的再次调用不会重复新增或保存；保存失败由 `TryGrantVictoryStars(...)` 回滚本次新增。重复加载、进程中断与部分关卡保存成功的组合尚未做 PlayMode 验证。
- P2 诊断标签：迁入管理器后的 warning 文本仍使用 `[PostcardGuidePopup]` 前缀，功能不受影响，但日志归属可能造成排查混淆；是否调整代码日志标签待后续确认。

### 验证、未覆盖项与回滚

- 静态 diff 确认 `PostcardManager` 包含完整遍历与补录逻辑，`LoadManager` 存在唯一未注释调用，原 `PostcardGuidePopup` 调用已注释且私有实现已移除。
- 静态检查确认未新增全局事件、配置、资源、prefab、编辑器生命周期或每帧逻辑，也未按历史通关数直接增加背包 Star。
- 本次文档更新未重新执行 C# 编译；Unity PlayMode、历史存档迁移、重复加载、无效 sort/path、保存失败、部分成功、加载耗时、等待动画和 Android/iOS 横屏均未验证。
- 回滚时移除 `LoadManager` 调用与 `PostcardManager.TryGetAllCompleteLevelStar()`；若恢复原触发点，则还原 `PostcardGuidePopup` 的私有实现和 `FullNeededStarCount()` 调用。DB 字段与既有已补录数据无需迁移。

## 2026-07-17：历史补录总星数门禁增量评估

### 基线、影响链路与控制

- 基线：`TryGetAllCompleteLevelStar()` 原先只要求历史关卡能解析到有效 `LevelData`，随后直接按 `LevelVictoryStarCount` 补录；`LevelSumStarCount <= 0` 不能阻止历史通关补星。
- 影响链路：加载后处理 → `DB.GameData.PassedLevels` → `LevelSort.Path` → `LevelData` → `TryGrantVictoryStars(...)` → `LevelCollectedStar`、`LevelWaitingAnimStar` 与 Postcard 保存。
- 当前控制：在调用 `TryGrantVictoryStars(...)` 前增加 `LevelSumStarCount <= 0` 门禁。被门禁关卡不进入任何 Postcard 写入；正数关卡仍由 `TryGrantVictoryStars(...)` 原子式补充永久星和等待动画计数，并在保存失败时回滚。
- 重复补录控制保持不变：`LevelWaitingAnimStar` 只按本次新增星 id 数量累加，已有永久星或重复加载不会重复增加动画数据。

### 风险、验证缺口与回滚

- P1 配置组合：本次只增加零值门禁，没有把 `LevelVictoryStarCount` 上限进一步限制为 `LevelSumStarCount`。若出现总星数为 1、胜利星为 3 等异常组合，仍会沿用现有最多补 3 星的行为；是否需要截断尚未得到需求证据。
- P1 存档表现：历史补录新增星会进入 `LevelWaitingAnimStar`，后续对应胜利面板可能播放补录星动画；这是现有补录链路的既有语义，本次未改变，仍需 PlayMode 确认产品表现。
- 静态 diff 与敏感项检查通过；没有新增事件、资源、编辑器生命周期或每帧逻辑。
- C# 工程构建被 `Assembly-CSharp.csproj` 中 25 个不存在源文件的 `CS2001` 阻断，阻断文件不属于本次改动；Unity PlayMode、保存失败注入、重复加载及 Android/iOS 横屏仍未验证。
- 回滚只需删除 `TryGetAllCompleteLevelStar()` 中的总星数门禁，不涉及配置、DB schema、资源或 prefab 回滚。

## 2026-07-17：胜利星同步进入 Bag 增量评估

### 基线与控制

- 基线：`TryGrantVictoryStars(...)` 原先只更新 Postcard 永久星与等待动画数据；首次通关和历史补录都不会增加可消费的 Bag Star。
- 当前链路：计算本次新增星 id → 更新 Postcard 数据 → `DB.SavePostcard()` 成功 → 按新增数量调用 `Bag.AddTool(Star, count, Other)`。
- 去重控制继续使用 `LevelCollectedStar`：已有星 id 不发放 Bag Star；历史补录与正常首次通关复用同一入口。
- Bag 或 `DB.GameData` 缺失时提前返回，避免只记录已收集星却未发放真实道具。

### 风险、验证缺口与回滚

- P1：Postcard 与 GameData/Bag 是两份独立存档；当前按项目既有 `Bag.AddTool()` 方式保存，没有新增跨存档事务。进程在两次同步保存之间异常退出的极端场景未验证。
- P1：过去已经写入 `LevelCollectedStar`、但尚未进入 Bag 的存档不会被本次增量规则追溯补发；若产品要求修复这类既有存档，需要单独的迁移需求和去重依据。
- P2：历史首次补录可能连续触发多次 Bag 保存与道具更新事件，移动端启动耗时和表现仍需 PlayMode/设备验证。
- `git diff --check` 通过；`Assembly-CSharp.csproj` 独立编译通过，0 error / 99 warnings，warnings 未逐项审计。
- 需验证新增 1 至 3 星、部分已有、全部已有、重复启动、Bag 缺失及 Postcard 保存失败；未验证 Android/iOS。
- 回滚时移除 Bag/GameData 就绪检查和保存成功分支中的 `Bag.AddTool(...)`，无需修改 DB schema、配置或资源。

## 2026-07-20：胜利默认星总数约束增量评估

### 基线、影响链路与控制

- 基线：首次通关未检测 `LevelSumStarCount`；历史补录只在调用前做零值门禁；`TryGrantVictoryStars(string, int)` 本身只能把胜利星限制到 0 至 3。
- 当前链路：首次通关或历史补录 → 完整 `LevelData` → `TryGrantVictoryStars(LevelData)` → 同时归一化 `LevelSumStarCount` 与 `LevelVictoryStarCount` → 永久星、等待动画、Postcard 保存与 Bag Star。
- 当前控制：实际发放数量为 `0~min(LevelVictoryStarCount, LevelSumStarCount, 3)`；总星数为 0 时在任何存档或 Bag 访问前返回，两个现有调用点共用同一保护。
- 去重、保存失败回滚和按实际新增数量发奖逻辑不变；未新增全局事件、资源加载、编辑器生命周期或每帧逻辑。

### 风险、验证缺口与回滚

- P1 旧存档：本次只阻止后续错误发放，不清理已经存在于 `LevelCollectedStar` 或 Bag 中的历史星，避免没有迁移依据时破坏玩家数据。
- P2 配置：负数和超过 3 的配置会被安全限制；配置异常本身不会被自动修正或上报到配置系统。
- C# 工程构建通过，0 error / 99 warnings；调用点与差异格式已做静态检查。Unity PlayMode、保存失败注入、Android/iOS 横屏和已有异常存档未验证。
- 回滚为恢复旧 API 签名和两个调用点，并恢复历史补录外部门禁；不涉及 DB schema、Luban 配置、prefab 或资源回滚。
