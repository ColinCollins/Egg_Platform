# PostcardManager 临时星星计数功能说明

## 需求背景

`PostcardManager` 原脚本注释要求作为 postcard 数据中心，监听 `PostcardGetStarEvent`，用 `DB.Postcard.LevelCollectedStar` 记录对应关卡本轮新获得的 star 数量，并在 Victory 弹出时清空对应关卡数据。本次按该注释补齐运行时实现，同时保持原注释内容不改。

## 影响范围

- 修改脚本：`Assets/Game/UI/Postcard/Scripts/PostcardManager.cs`
- 接入脚本：`Assets/Game/UI/GameVictory/Scripts/GameVictoryPanel.cs`
- 生命周期释放：`Assets/Game/Scripts/GameManager.cs`
- 数据：`DB.Postcard.LevelCollectedStar`
- 数据：`DB.Postcard.LevelWaitingAnimStar`
- 事件：复用已有 `PostcardGetStarEvent`
- 接入脚本：`Assets/Game/Scripts/Postcard/Postcard_Star.cs`
- 接入脚本：`Assets/Game/UI/Postcard/Scripts/Postcard_VictoryStarGroupCtrl.cs`
- 接入脚本：`Assets/Game/Scripts/Play/PlayCtrl_Nodes/PlayCtrl_Success.cs`
- 未修改 `PlayEvents.cs`、背包奖励数量、prefab、Luban 配置、RemoteConfig、资源加载、analytics、广告、IAP 或平台 SDK。

## 原有行为基线

- `PostcardManager.cs` 只有类注释和空类体，没有事件订阅、读取、缓存、清理或胜利消费逻辑。
- `GameManager.Init()` 已创建 `Postcard = new PostcardManager()`，但此前该对象没有实际运行时行为。
- `DB.Postcard.LevelCollectedStar` 已在 `Postcard.Init()` 中初始化为 `Dictionary<string, List<int>>`，用于按关卡保存永久收集的星星 id。
- `DB.Postcard.LevelWaitingAnimStar` 已在 `Postcard.Init()` 中初始化为 `Dictionary<string, int>`，用于按关卡保存胜利面板等待播放的新增星星动画计数。
- `Postcard_Star` 在星星飞行动画完成后派发 `PostcardGetStarEvent`，但事件此前只被 `GamePlayPanel` 用于 HUD 星星块提示。

## 当前行为

- `PostcardManager` 构造时通过 `EventSubscriber` 订阅 `PostcardGetStarEvent`。
- 收到事件后，管理器读取 `PlayCtrl.Instance.Level.CurrentLevelData.Id`，将该关卡在 `DB.Postcard.LevelWaitingAnimStar` 中的等待动画计数加 1，并调用 `DB.SavePostcard()` 保存。
- 新增 `GetCollectedStarCount(string levelId)`、`CacheCollectedStarCount(string levelId, int starCount)`、`ClearCollectedStarCount(string levelId)`、`ClearAllCollectedStarCounts()`、`ConsumeCollectedStarCount(string levelId)`，用于管理 `LevelWaitingAnimStar` 等待动画计数。
- 新增 `GetLevelCollectedStarIds(string levelId)`、`IsLevelStarCollected(string levelId, int starId)`、`TryCollectLevelStar(string levelId, int starId, out bool isNewCollection)`，用于集中读取和写入 `LevelCollectedStar` 永久收集记录。
- 新增 `TryGrantVictoryStars(LevelData levelData)`，用于首次通关时统一读取胜利星数量与关卡总星数上限，写入胜利默认星，并把本次新增数量累加到 `LevelWaitingAnimStar`。
- `Postcard_Star` 现在通过 `PostcardManager.TryCollectLevelStar(...)` 和 `IsLevelStarCollected(...)` 完成永久收集去重与保存，不再直接访问 `DB.Postcard` 或调用 `DB.SavePostcard()`。
- `Postcard_VictoryStarGroupCtrl` 通过 `PostcardManager.GetLevelCollectedStarIds(...)` 读取永久星 id 列表，并通过 `ConsumeCollectedStarCount(...)` 消费等待动画计数。
- 已澄清生命周期规则：失败、重开或退出关卡不会清空 `LevelWaitingAnimStar` 临时计数；只有 Victory 消费或显式 `Clear` / `ClearAll` API 会清空。
- `GameManager.OnDestroy()` 调用 `Postcard?.Dispose()`，释放事件订阅，避免全局事件回调残留。

## 数据、事件与 UI 流

1. `Postcard_Star` 完成收集反馈后派发 `PostcardGetStarEvent`。
2. `PostcardManager.OnPostcardGetStar(...)` 读取当前关卡 id。
3. `PostcardManager` 确保 `DB.Postcard.LevelWaitingAnimStar` 非空，按关卡 id 累加本轮等待动画数量。
4. `PostcardManager` 调用 `DB.SavePostcard()` 保存等待动画计数。
5. 失败、重开或退出关卡时不触发临时计数清理，计数会保留到后续胜利消费或显式清理。
6. 胜利面板通过 `GameVictoryPanel.Create(data, hasReward)` 打开时，按 `data.Id` 调用 `ConsumeCollectedStarCount(...)`。
7. `ConsumeCollectedStarCount(...)` 返回本轮计数并清空该关卡临时记录；后续再次读取同关卡会得到 0，直到新星星事件再次写入。
8. 首次通关默认星由 `PlayCtrl_Success` 调用 `TryGrantVictoryStars(...)` 写入；关卡内手动收集星由 `Postcard_Star` 调用 `TryCollectLevelStar(...)` 写入。

## 使用边界

- `LevelCollectedStar` 是永久收集的星星 id 字典；`LevelWaitingAnimStar` 是胜利表现用的临时计数字典。
- `ConsumeCollectedStarCount(...)` 适合胜利面板打开时调用；如果仅预览或调试，不希望清空数据，应使用 `GetCollectedStarCount(...)`。
- `CacheCollectedStarCount(...)` 的 `starCount <= 0` 会清除对应关卡条目。
- `TryCollectLevelStar(...)` 只负责永久收集记录和保存，不负责背包奖励、HUD 事件或 UI 表现。
- `TryGrantVictoryStars(...)` 会把默认星数量限制为 `0~min(LevelVictoryStarCount, LevelSumStarCount, 3)`；保存失败时回滚本次新增永久星和等待动画计数。
- 失败、重开或退出前已经收集的临时计数应保留，避免玩家稍后胜利时丢失本次进入以来的新收集表现数据。
- levelId 为空时所有公开 API 都会安全返回，不创建空 key。
- 当前实现不新增全局事件；胜利流程通过已有 `GameVictoryPanel.Create(LevelData data, ...)` 的 `data.Id` 消费数据。

## 验证记录

- 静态检查确认 `PostcardManager.cs` 保留原类注释文本，新增公开 API 均有中文 XML 注释。
- 静态生命周期扫描：目标新增逻辑未引入 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`Instantiate`、`RemoteConfig`、`PlayerPrefs`、`Resources`、`YooAsset`、`Analytics`、`IAP` 或 `Advertisement`。
- 静态链路证据：`LoadManager.EnterGame()` 先调用 `GameManager.Instance.Init()` 创建 `PostcardManager` 并初始化 DB，`OnPostProcess()` 后续才调用 `GameManager.Instance.ReadyToPlay()`；`PlayCtrl.CreateLevelAsync(...)` 在进入关卡时实例化关卡 prefab。
- 静态链路证据：`Postcard_Star` 通过 `PostcardManager` 读写 `LevelCollectedStar`；`Postcard_VictoryStarGroupCtrl` 通过 `PostcardManager` 读取永久星 id 并消费 `LevelWaitingAnimStar`；`PlayCtrl_Success` 通过 `TryGrantVictoryStars(...)` 发放默认胜利星。
- 静态扫描确认业务脚本中直接 `DB.Postcard` / `DB.SavePostcard()` 访问已集中到 `PostcardManager.cs`；`Postcard_Partial.cs` 仍是存档结构定义。
- `git diff --check -- .specify/feature.json specs/033-postcard-manager/spec.md specs/033-postcard-manager/checklists/requirements.md BrainPunk/Assets/Game/UI/Postcard/Scripts/PostcardManager.cs BrainPunk/Assets/Game/UI/GameVictory/Scripts/GameVictoryPanel.cs BrainPunk/Assets/Game/Scripts/GameManager.cs BrainPunk/Assets/Game/UI/Postcard/Scripts/PostcardManager功能说明.md BrainPunk/Assets/Game/UI/Postcard/Scripts/PostcardManager功能说明.md.meta BrainPunk/Assets/Game/UI/Postcard/Scripts/PostcardManager风险评估.md BrainPunk/Assets/Game/UI/Postcard/Scripts/PostcardManager风险评估.md.meta BrainPunk/DevelopMD/已有功能设计与使用记录.md`：通过，仅有工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，0 error / 1115 warnings；warning 属于当前工程输出，未逐项审计。

## 剩余风险

- 尚未运行 Unity Editor PlayMode 验证真实星星收集、胜利面板打开、计数消费和二次读取清零。
- 尚未在 Android/iOS 横屏设备验证胜利流程中新增保存是否有可见卡顿；当前实现仅事件触发时常量时间字典写入，无每帧逻辑。
- `CacheCollectedStarCount(...)` 保存失败时会记录 warning 并返回失败，但当前没有回滚 `LevelWaitingAnimStar` 的内存态覆盖；`TryCollectLevelStar(...)` 和 `TryGrantVictoryStars(...)` 已对本次新增永久星做内存回滚。
- 胜利星 UI 表现依赖胜利面板 prefab 中存在 `Postcard_VictoryStarGroupCtrl`；缺失时只会走兜底消费，真实表现仍需 PlayMode 验证。

## 回滚方式

- 回退 `PostcardManager.cs` 新增 API 和集中 DB 访问实现。
- 将 `Postcard_Star.cs`、`Postcard_VictoryStarGroupCtrl.cs`、`PlayCtrl_Success.cs` 恢复为改动前的调用边界。
- 移除 `GameManager.OnDestroy()` 中的 `Postcard?.Dispose()` 调用。
- 删除本说明文档及对应 `.meta`。

## 2026-07-10：当前关卡可收集星配置门禁

### 需求与影响范围

- 新增 `PostcardManager.CanCurrentLevelCollectStar()` static API，供 `BaseLevelCtrl.TryToCollectStar(...)` 判断当前 `LevelData.LevelSumStarCount` 归一化到 0 到 3 后是否大于 0，且当前有效永久收集星数量是否仍小于该上限。
- 修改文件为 `PostcardManager.cs` 与 `BaseLevelCtrl.cs`；未修改 Luban 生成代码、`tbleveldata.json`、DB 字段、prefab、资源、全局事件、广告、IAP、analytics、RemoteConfig 或平台 SDK。

### 原有行为与当前行为

- 原有 `TryToCollectStar(...)` 不检查 `LevelSumStarCount`，所有进入共享成功流程的关卡都可能尝试写入第 3 颗明信片星、增加背包星并派发 `PostcardGetStarEvent`。
- `CanCurrentLevelCollectStar()` 仅在 PlayMode 读取 `PlayCtrl.Instance.Level.CurrentLevelData`，把 `LevelSumStarCount` 限制为当前有效星 id 范围 0 到 3，并通过当前 `PostcardManager.GetLevelCollectedStarIds(levelId)` 读取去重、过滤后的永久星 id 数量；关卡/存档无效、管理器缺失、有效上限为 0 或已收集数量达到上限时返回 `false`。该方法不写 DB/save，也不创建运行时对象。
- `TryToCollectStar(...)` 在可选延迟前和延迟完成后分别调用门禁：第一次避免零星关卡启动延迟任务，第二次避免延迟期间当前关卡切换后按错误关卡写入。
- 门禁返回 `true` 后仍沿用既有永久星去重、保存、背包发奖和事件派发链路；`PlayCtrl_Success.TryGrantVictoryStars(...)` 的首次通关默认星链路独立按 `LevelVictoryStarCount` 执行，不受本门禁改变。

### 验证、风险与回滚

- 静态 diff 确认新增 API 具有中文调用边界说明，且没有新增 DB/save、编辑器生命周期、资源加载或全局事件链路。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，0 error / 98 warnings，warnings 未逐项审计。
- 补充已收集数量上限比较后复跑同一编译命令通过，0 warning / 0 error。
- Unity PlayMode 尚需验证 `Level_001/002` 的零星门禁、`Level_003` 的未满/已满上限、DB 缺失降级、异常 `LevelSumStarCount > 3` 的上限归一化、重复通关去重和延迟期间关卡切换；Android/iOS 横屏 smoke 未验证。
- 回滚时移除 `CanCurrentLevelCollectStar()`，并移除 `BaseLevelCtrl.TryToCollectStar(...)` 中两处门禁调用即可恢复本次变更前行为。

## 2026-07-13：历史已通关关卡胜利星补录职责迁移

### 需求与影响范围

- 历史通关胜利星补录由 `PostcardGuidePopup` 的私有逻辑迁入 `PostcardManager.TryGetAllCompleteLevelStar()`，补录职责与 `TryGrantVictoryStars(...)` 的永久星写入、等待动画计数及保存逻辑集中在同一管理器。
- 运行入口改为 `LoadManager` 的游戏加载后处理流程：`GameManager.Instance.ReadyToPlay()` 完成后、`InitGameReward()` 与 `NewVersionProcessFinished()` 前调用。
- 涉及脚本为 `PostcardManager.cs`、`LoadManager.cs` 和 `PostcardGuidePopup.cs`；未新增 DB 字段、配置、prefab、资源、全局事件、背包奖励、analytics、广告、IAP、RemoteConfig 或平台 SDK 链路。

### 原有行为基线与当前行为

- 原实现只在 `PostcardGuidePopup.FullNeededStarCount()` 完成背包 Star 保底后调用弹窗内私有方法，因此补录依赖首阶段明信片引导入口。
- 当前 `TryGetAllCompleteLevelStar()` 在运行态读取 `DB.GameData.PassedLevels`，对关卡 sortId 去重，再通过 `LevelCtrl.GetLevelSortById(sortId)` → `LevelSort.Path` → `LevelCtrl.GetLevelDataById(path)` 获得关卡配置。
- 每个有效关卡按 `LevelData.LevelVictoryStarCount` 复用 `TryGrantVictoryStars(...)`：已有星 id 不重复写入，数量限制为 0 至 3；发生新增时写入 `DB.Postcard.LevelCollectedStar`，累加 `DB.Postcard.LevelWaitingAnimStar` 并调用 `DB.SavePostcard()`。
- 非运行态、通关列表为空时直接返回；管理器或关卡控制器缺失时停止补录；单个 sort/path/LevelData 无效或单关保存失败时记录 warning，并继续处理后续关卡。
- 本方法不按历史通关数增加背包 Star；`PostcardGuidePopup.FullNeededStarCount()` 原有背包 Star 至少补到 9 的逻辑保持独立。

### 验证、剩余风险与回滚

- 静态 diff 已确认方法主体从 `PostcardGuidePopup` 迁入 `PostcardManager`，加载流程存在唯一未注释调用；未发现其他运行时调用点。
- 静态源码确认调用位于 `ReadyToPlay()` 之后，且复用既有 `TryGrantVictoryStars(...)`，没有新增编辑器生命周期、每帧轮询或直接背包发奖逻辑。
- 本次仅调整文档，未重新执行 C# 编译或 Unity PlayMode；历史存档补录、重复加载、无效映射、保存失败、大量历史关卡加载耗时、等待动画消费以及 Android/iOS 横屏流程仍待验证。
- `TryGrantVictoryStars(...)` 会为历史关卡写入 `LevelWaitingAnimStar`，后续进入对应关卡胜利面板时可能播放这些补录星的等待动画，产品表现仍需确认。
- 每个存在新增星的历史关卡会独立保存一次；历史关卡较多时的移动端加载耗时尚无实测证据。
- 回滚时移除 `LoadManager` 对 `TryGetAllCompleteLevelStar()` 的调用和 `PostcardManager` 中该公开方法；若需要恢复旧触发时机，再恢复 `PostcardGuidePopup.FullNeededStarCount()` 末尾的私有补录调用。无需迁移或清理既有 Postcard 存档结构。

## 2026-07-17：历史已通关星补录增加关卡总星数门禁

### 需求、范围与原有行为

- 需求：`PostcardManager.TryGetAllCompleteLevelStar()` 在处理历史 `PassedLevels` 时增加 `LevelData.LevelSumStarCount` 判断；总星数小于等于 0 的关卡即使已经通关，也不得补录明信片星。同时，符合条件且确有新增星的关卡需要补充 `LevelWaitingAnimStar` 数据。
- 本次只修改 `Assets/Game/UI/Postcard/Scripts/PostcardManager.cs` 的历史补录入口及本地说明文档；不修改 Luban 生成配置、`PassedLevels`、DB 字段、背包奖励、prefab、资源、事件、analytics、广告、IAP、RemoteConfig 或平台 SDK。
- 原有行为：历史补录在解析出有效 `LevelData` 后直接按 `LevelVictoryStarCount` 调用 `TryGrantVictoryStars(...)`，未检查 `LevelSumStarCount`；因此存在总星数配置为 0、但胜利星配置为正数时仍补录星星的可能。
- 原有 `TryGrantVictoryStars(...)` 已同时负责永久星 id 与等待动画计数：只要本次新增了星 id，就会把新增数量累加到 `LevelWaitingAnimStar[levelId]`，保存失败时同时回滚两类数据。

### 当前行为与数据流

1. `LoadManager` 在加载后处理阶段调用 `TryGetAllCompleteLevelStar()`，方法读取并去重 `DB.GameData.PassedLevels`。
2. 每个历史关卡继续通过 `LevelSort.Path` 解析 `LevelData`；配置或 id 无效时沿用现有安全跳过和 warning。
3. `LevelData.LevelSumStarCount <= 0` 时直接跳过，不调用 `TryGrantVictoryStars(...)`，因此不会写入 `LevelCollectedStar`、`LevelWaitingAnimStar` 或触发 Postcard 保存。
4. `LevelSumStarCount > 0` 时继续按 `LevelVictoryStarCount` 调用 `TryGrantVictoryStars(...)`。仅本次实际新增的星 id 会同步增加 `LevelWaitingAnimStar[levelId]`；重复加载或已有星不会重复增加等待动画计数。
5. 保存仍统一由 `TryGrantVictoryStars(...)` 执行；保存失败会回滚本次新增永久星和对应等待动画计数。

### 验证、剩余风险与回滚

- `git diff --check -- BrainPunk/Assets/Game/UI/Postcard/Scripts/PostcardManager.cs` 已通过，仅有仓库既有 LF/CRLF 提示。
- 静态源码检查确认门禁位于 `LevelData` 有效性校验之后、`TryGrantVictoryStars(...)` 之前；等待动画数据继续复用既有统一写入和回滚链路，未新增编辑器生命周期、资源加载、全局事件或每帧逻辑。
- `dotnet build .\Assembly-CSharp.csproj --no-restore ...` 被工程文件中 25 个当前不存在的源文件引用阻断，错误为 `CS2001`；错误路径包括 Reddot、Guide、Level72/80/92 等未改模块，未获得本次脚本的完整编译结论。
- 尚未运行 Unity PlayMode 与 Android/iOS 横屏验证。需验证总星数为 0、总星数为正、已有部分/全部星、重复加载和保存失败回滚场景。
- 当前门禁只实现需求指定的“总星数小于等于 0 不补录”；`LevelSumStarCount > 0` 时仍沿用 `LevelVictoryStarCount` 的 0 至 3 归一化规则，不额外按总星数截断，异常配置组合仍需产品或配置侧确认。
- 回滚时移除 `TryGetAllCompleteLevelStar()` 中的 `LevelSumStarCount <= 0` 分支及本节文档即可；无需迁移或清理既有 Postcard 存档。

## 2026-07-17：胜利星同步发放到 Bag

### 需求、范围与行为

- 需求：首次通关或 `TryGetAllCompleteLevelStar()` 历史补录实际新增胜利星时，需要把同等数量的 Star 道具增加到 `PlayCtrl.Instance.Bag`。
- 修改范围仅为 `PostcardManager.TryGrantVictoryStars(...)` 及本地说明/风险文档；未新增 DB 字段、配置、prefab、资源、事件或平台 SDK 接口。
- 原行为只把新增星 id 写入 `DB.Postcard.LevelCollectedStar`，并累加 `LevelWaitingAnimStar`，不会增加 Bag Star。
- 当前行为在 Postcard 保存成功后按 `addedStarIds.Count` 调用 `Bag.AddTool(GameProps.Star, ..., RewardType.Other)`；已有星 id 不进入 `addedStarIds`，因此重复调用不会重复发放。
- Bag 或 `DB.GameData` 未就绪时，本次胜利星写入与发放均停止并记录 warning；Postcard 保存失败时沿用原有回滚，不发放 Bag Star。

### 验证、风险与回滚

- 静态检查确认 Bag 发放位于 Postcard 保存成功分支，数量来自本次新增星 id，而不是配置原始数量；`git diff --check` 通过。
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，0 error / 99 warnings，warnings 未逐项审计。
- 仍需 Unity PlayMode 验证首次通关、历史补录、部分已有星、全部已有星、Bag 缺失与 Postcard 保存失败场景；Android/iOS 未验证。
- 已经存在于 `LevelCollectedStar` 的旧记录不会再次发放到 Bag；本次不增加历史迁移标记或追溯补偿逻辑。
- 回滚时移除 `TryGrantVictoryStars(...)` 中的 Bag/GameData 就绪检查和 `Bag.AddTool(...)` 调用，并恢复该 API 的原说明即可。

## 2026-07-20：胜利默认星统一受关卡总星数限制

### 需求、范围与原有行为

- 需求：没有配置可收集星星的关卡在 Victory 时不得获得明信片星或 Bag Star；胜利默认星也不得超过该关卡总星数。
- 影响文件为 `PlayCtrl_Success.cs`、`PostcardManager.cs` 及现有本地文档；未修改 Luban 配置、DB 字段、prefab、资源、事件、analytics、广告、IAP、RemoteConfig 或平台 SDK。
- 原有首次通关入口只把 `LevelData.Id` 与 `LevelVictoryStarCount` 传给管理器，没有检查 `LevelSumStarCount`；历史补录虽有外部门禁，但统一发放 API 本身无法保证总星数约束。

### 当前行为与数据流

1. 首次通关与历史补录均把完整 `LevelData` 传给 `TryGrantVictoryStars(LevelData)`，不再重复拆分配置参数。
2. 统一入口先校验 `LevelData` 与关卡 id，再把总星数限制到 0 至 3，并把胜利星数量限制到 0 至该总星数。
3. `LevelSumStarCount <= 0` 或 `LevelVictoryStarCount <= 0` 时直接成功返回，不读取 Postcard 存档、不写等待动画、不保存，也不增加 Bag Star。
4. 正数发放仍沿用永久星 id 去重、Postcard 保存、保存失败回滚和按实际新增数量增加 Bag Star 的既有链路。

### 验证、剩余风险与回滚

- 调用点扫描确认业务代码只有首次通关与历史补录两个调用点，均已传入完整 `LevelData`。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，0 error / 99 warnings；warning 未逐项审计。
- 尚未运行 Unity PlayMode 与 Android/iOS 横屏验证；仍需验证总星数为 0、胜利星为 0、胜利星大于总星数、部分已有星、全部已有星和保存失败场景。
- 本次不会清理旧存档中已经错误发放的星；若需要历史纠错，必须另行定义迁移和补偿规则。
- 回滚时恢复 `TryGrantVictoryStars(string levelId, int starCount)`，恢复两个旧调用点，并恢复历史补录的外部零值门禁；无需修改 DB schema、配置或资源。
