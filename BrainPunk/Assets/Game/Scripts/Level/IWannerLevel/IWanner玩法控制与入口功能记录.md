# IWanner 玩法控制与入口功能记录

## 需求与背景

- 2026-08-10：先完成独立玩法控制器、Group/小关配置解析、单关运行时和
  `IWannerGameEnter` 入口逻辑；后续按用户补充要求接入 GameplayPanel 的 `BlackMask` 与 `ClickBlock`。
- 当前入口按 `TbIWannerLevelSort.DataList` 顺序显示全部 Group 的 `Id`；点击当前存档 Group 时恢复已保存的
  组内索引，点击其它 Group 时从第 0 小关开始。正式解锁与选组状态仍未接入。
- 本次“数据分析管理”指对 Luban Group/LevelData 的运行时解析、校验和查询；不包含 SDK 数据分析事件。

## 修改前基线

- `IWannerLevelCtrl` 是挂在测试场景/关卡 prefab 上的临时角色驱动，只订阅既有移动/跳跃事件并调用
  `ActorCtrl.OnUpdate()`，没有正式初始化、暂停、输入门禁或清理 API。
- `GameManager` 只注册 Main 与 Draw 两种 `IPlayController`；Loading 只初始化 `DrawGameLevelCtrl`。
- `IWannerGame_GamePlayPanel` 只有生成字段和空生命周期，按钮没有运行时行为。
- 没有 `IWannerGameEnter` 或“首个 Group 直入”调用链。

## 影响文件与系统

- 玩法生命周期：`Assets/Game/Scripts/Play/IWanner/IWannerPlayCtrl.cs`
- 配置解析：`Assets/Game/Scripts/Play/IWanner/IWannerGameLevelCtrl.cs`
- 单关运行时：`Assets/Game/Scripts/Level/IWannerLevel/IWannerLevelCtrl.cs`
- HUD 逻辑：`Assets/Game/UI/IWannerGame/GamePlayPanel/Scripts/IWannerGame_GamePlayPanel.cs`
- 入口逻辑：`Assets/Game/UI/StartPanel/Scripts/IWannerGameEnter.cs`
- 控制器注册：`Assets/Game/Scripts/GameManager.cs`、`Assets/Game/Scripts/Play/Controller/PlayModeType.cs`、
  `Assets/Game/Scripts/Play/Controller/IPlayControllerPolicy.cs`
- Loading 初始化：`Assets/Game/UI/Loading/Scripts/LoadManager.cs`
- 资源读取：继续使用当前目录 `Assets/Game/Prefabs/WannerLevel/`，未移动或重命名 prefab。
- YooAsset：`Assets/BPGame/AssetBundleCollectorSetting.asset` 的 `GamePackage/Level` 组新增
  `Assets/Game/Prefabs/WannerLevel` 目录收集器。

## 修改后行为

### 配置解析

- `IWannerGameLevelCtrl` 在 Loading 的 Luban 初始化完成后读取两张 IWanner 表，不修改源数据。
- 当前以 `IWannerLevelSort.Id` 作为唯一 `groupId`；`IWannerLevelSort.Group` 只作为配置分类字段保留。
- Group 在使用前校验：ID 大于 0、小关数量为 1～5、引用非空且不重复、每个 ID 都能解析到
  `IWannerLevelData`。首行无效时明确失败，不跳过到后续 Group。
- `currentLevelIndex` 采用从 0 开始的组内位置，并与 `DB.IWannerGame.CurrentLevel` 使用同一口径。

### 玩法进入与加载

1. `IWannerGameEnter` 点击创建 `IWannerGame_ChoiceLevelPopup`。
2. 弹窗按 `IWannerGameLevelCtrl.LevelGroups` 顺序创建 Item，文本显示 `IWannerLevelSort.Id`。
3. Item 点击调用 `GameManager.TryEnterIWannerGroup(groupId)`，再由 `PlayControllerManager` 通过
   `IWannerPlayControllerPolicy` 激活 `IWannerPlayCtrl`。
4. 控制器按 `groupId` 校验目标 Group；若与存档 `CurrentGroupID` 相同则读取有效的 `CurrentLevel`，否则使用
   第 0 小关。越界存档索引会回退到 0，并在确认目标小关有效后修正存档。
5. 关闭 Normal 层旧 UI，创建 `IWannerGame_GamePlayPanel`，从
   `Assets/Game/Prefabs/WannerLevel/{IWannerLevelData.Path}.prefab` 异步加载关卡。
6. 关卡实例挂到 Main 场景 `Scene` 节点，解析并初始化其 `IWannerLevelCtrl` 后才开放 HUD 和角色输入。
7. 同步校验失败时保持选组弹窗并回滚 Main；异步加载失败时清理 IWanner 实例/HUD并返回 StartPanel。

### YooAsset 关卡收集

- WannerLevel 与主线/Draw 关卡使用同一 `Level` 收集组，规则为 `AddressByFileName`、
  `PackSeparately`、`CollectPrefab`。
- 收集目录固定为 `Assets/Game/Prefabs/WannerLevel`，CollectorGUID 使用该目录 `.meta` 中的
  `39d30675343e18c4d9f8a0a36db874bf`。
- 运行时代码仍用完整工程路径 `Assets/Game/Prefabs/WannerLevel/{Path}.prefab` 请求资源；本次未移动、
  重命名或编辑目录内 prefab/图片。

### 输入、暂停与 Reset

- 左/右按钮只使用 `CustomButton.OnClickEnter/OnClickExit` 驱动移动区域进入与离开；进入一侧立即切换到该方向，
  离开当前方向且没有另一方向处于激活状态时归零。
- 上按钮只在一次按下时触发一次跳跃。
- HUD 的方向/跳跃复用主线玩家输入事件；Reset 新增 IWanner 玩法专属事件并只由活动控制器订阅，
  Pause 使用当前 `ActivePauseHost`，避免进入主线或 Draw 的 Reset 业务链。
- 暂停复用 `GameSettingPopup.Create(true, this)` 与 `IPlayPauseHost`，暂停/关闭/退出都会先归零移动输入。
- HUD Reset 销毁旧关卡与旧 GameplayPanel，再创建当前 Group 当前索引的小关和全新 GameplayPanel；
  `currentGroupId`、`currentLevelIndex` 与已保存进度保持不变，并使用激活版本过滤旧异步结果；Reset 不改进度存档。
- Group Replay 与 HUD Reset 是两个独立行为：`TryReplayCurrentGroup()` 保持当前 Group，先把
  `currentLevelIndex` 和存档 `CurrentLevel` 重置为 0，再加载该 Group 的第一关；HUD Reset 不得调用它。
- Skip 默认隐藏且不可交互；达到失败阈值并完成激励广告后，按成功推进规则保存下一小关索引。

### HUD 重写边界

- `IWannerGame_GamePlayPanel` 已按主线/DrawGame 模式改为 `BaseUIView + IEventSender`，不再持有
  `IWannerPlayCtrl`。方向和跳跃复用 `PlayerRightMoveEvent`、`PlayerLeftMoveEvent`、
  `PlayerMoveCancelEvent`、`PlayerJumpEvent`。
- Reset 使用 IWanner 专属 `IWannerGameResetEvent`，只由活动 `IWannerPlayCtrl` 订阅；Victory Replay
  复用共享 `GameReplayEvent`，但在 IWanner 中保持“当前 Group 从第 0 小关开始”的业务语义。
- 暂停与 DrawGame 一致，通过 `ControllerManager.ActivePauseHost` 调用 Pause，再创建
  `GameSettingPopup`；面板关闭或输入禁用时释放方向状态。
- 左右区域激活后由面板逐帧派发；进入新方向会清除旧方向，旧区域随后到达的 Exit 不会误取消新方向，
  只有左右都未激活时才派发移动取消。
- GameplayPanel 采用小关级生命周期，不跨小关复用。每个新实例只在 `Create(currentLevelIndex, levelCount)`
  后初始化一次组内进度；不提供刷新旧面板进度的入口，超出 prefab 绑定容量时不动态创建节点。
- Skip 的可见性与可交互性分离，默认不可见；当前阶段未接入 Skip 点击后的业务行为。

### 黑屏过场与点击拦截

- GameplayPanel prefab 已绑定 `BlackMask` 和 `ClickBlock`；面板打开时立即显示黑幕并开启点击拦截。
- 首次进入或关卡重建加载完成后，控制器派发主线/DrawGame 共用的 `GamePlayPanelFadeInEvent`，黑幕从目标
  透明度渐隐到 0；渐隐完成前关卡和 HUD 输入保持关闭，随后关闭 `ClickBlock` 并恢复输入。
- Reset 与 Group Replay 先关闭关卡/HUD 输入并开启旧面板 `ClickBlock`，等待旧面板黑幕渐显完成后，
  同时销毁旧关卡和旧 GameplayPanel。控制器随即创建全新 GameplayPanel；新实例从全黑状态覆盖加载过程，
  目标小关加载完成后再渐隐，避免两个 UI 实例之间出现透明空窗。
- Inspector 暴露 `blackMaskVisibleAlpha`、`fadeToBlackDuration`、`fadeFromBlackDuration`，默认分别为
  `1`、`0.4` 秒、`0.4` 秒；Tween 使用不受 `timeScale` 影响的 UI 时间。
- 六个按钮字段已在 prefab 完成绑定。两组进度列表均已按左到右顺序绑定 5 个独立 Image；进度 Toggle
  容器也已显式绑定，运行时按其直接子节点兄弟顺序对应 Group 小关索引。

## 小关成功与组内推进（2026-08-10）

- `IWannerLevelCtrl` 新增 `Success(Collider2D)` UnityEvent 适配 API 和幂等 `TrySuccess()`：只接受 Player，
  每个小关实例只提交一次；接受成功后立即关闭角色输入、清零刚体速度，并把角色瞬移到成功门根节点的世界坐标
  （保留角色原 Z），随后更新为停止动画状态，不派发主线 `SUCCESS` 全局事件。
- 角色到门过程不使用行走输入、路径移动或 Tween；瞬移完成后成功锁持续生效，方向键、Jump、Pause Resume 的
  输入恢复都不能重新开放角色控制。若门引用缺失则记录警告并跳过瞬移，但仍继续门表现/下一关降级流程。
- `IWannerPlayCtrl` 在 `InitializeRuntime()` 前订阅当前关卡实例的 `Resolved` 事件；`IWannerLevelCtrl` 只同步
  上报 `Success/Failed` 结果，不持有玩法控制器的异步委托。玩法控制器收到成功后立即关闭 HUD 输入和
  `ClickBlock` 穿透，再调用 `PlayEnterDoorAnimationAsync()`；只有
  `IWannerGame_EnterDoorAnimCtrl.PlayAsync()` 完成关门与 effect 渐隐，才解析 `currentLevelIndex + 1`。
- 下一关数据解析成功后更新 `_currentLevelIndex/_currentLevelData`，复用既有“小关级 GameplayPanel 销毁重建”
  与黑幕流程：旧面板先渐显到黑，销毁旧关卡和旧面板，新建下一关及新面板，加载完成后渐隐并恢复输入。
- 成功过程使用关卡实例 `_isResultSubmitted`、玩法 `_isOpeningLevel`、当前关卡引用和 activation version 四层门禁，
  拒绝同一门的多 Collider 重复提交、加载期间 Reset/Replay，以及旧关卡异步结果推进新会话。
- `IWannerLevelCtrl.AutoBind` 为仅 Editor 编译的 Odin 按钮：自动绑定 Actor、名为 `IWannerGame_Door` 的
  `OnTrigger2DHandle` 和 `IWannerGame_EnterDoorAnimCtrl`，并把 `Success(Collider2D)` 持久化到门的 `OnEnter`。
  按钮支持 Undo 和 Prefab Instance Override，只访问当前 prefab 层级。
- 运行时保留本地兜底：若 prefab 尚未持久化 Success Listener，初始化时会在同一门的 `OnEnter` 添加临时 Listener，
  Shutdown 时移除；若已通过 AutoBind 持久化，则不会重复注册。
- 当前 Group 最后一关完成后进入 IWanner VictoryPanel；首通记录 `PassedLevels` 并打开 Reward 表现版，复通打开
  普通版。整组结算同时把该 Group 的保存索引重置为 0，不会越界加载下一关或误用主线 Victory。

## 当前未实现范围

- 正式选组解锁/通关状态、TBN、SDK 关卡打点和广告真机验收仍未完成。
- 本次没有修改 `GameSDKService`、`DB` 或《关卡打点生命周期约定》，因此没有建立 IWanner 标准 attempt。

## 入口门关闭动画（2026-08-10）

- 新增 `IWannerGame_EnterDoorAnimCtrl`，作为 IWanner 局部 `BaseItemEventHandle` 交给
  `TriggerExecutor/BaseItemExecutor` 调用；未新增全局事件，也不负责触发条件、玩家判定或小关结算。
- 触发时启用 `door_close` 节点，并强制从头播放该 Spine 资源中的 `close` 动画；随后把 `effect` 的
  Spine Alpha 从 1 线性渐隐到 0。
- Inspector 暴露 `effectFadeDelay` 与 `effectFadeDuration`，分别控制关门动画开始后到特效渐隐之间的等待时间、
  以及实际渐隐时长；默认值为 0 秒和 0.4 秒。
- 句柄在“延迟 + 渐隐”结束后设置 `IsDone`，保证顺序型执行器不会在表现完成前推进。关门 Spine 动画本身
  不额外阻塞执行器，表现对齐由 `effectFadeDelay` 调节。
- `ResetState()` 会停止旧 Tween、隐藏 `door_close` 并把 `effect` 恢复到 Alpha 1，因此当前小关 Reset 或
  同一执行器再次触发时可从初始状态重播。
- `IWannerGame_Door.prefab` 已挂载并绑定控制器的两个 Spine 引用。组件仍可作为独立
  `BaseItemEventHandle` 加入 TriggerExecutor；标准成功链则由 `IWannerLevelCtrl` 直接异步执行并等待它完成，
  避免 TriggerExecutor 与成功推进之间缺少完成确认。
- 控制器提供仅在 `UNITY_EDITOR` 编译的 Odin `AutoBind` 按钮：优先查找直接子节点 `door_close/effect`，
  再在当前门层级递归查找同名 `ActorSpineCtrl`。按钮只写本组件序列化引用，支持 Undo 和 Prefab Instance
  Override，不访问 DB、配置、资源管理器或其它运行时链路。
- 修改前门 prefab 的 `door_close` 会按自身默认动画配置在加载时播放，且没有特效渐隐句柄；修改后由新控制器
  在 Start 隐藏关闭态节点，实际触发时才显示并重播。

## 验证记录

- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo --verbosity:minimal`：
  `0 error / 101 warning`；warning 属于工程既有范围。
- `git diff --check`：本次已跟踪代码修改通过；只有仓库现有 LF/CRLF 转换提示。
- 静态确认新增脚本已进入 `Assembly-CSharp.csproj`。
- 静态确认 YooAsset collector 的目录路径与文件夹 GUID 一致，当前 `ILevel_001.prefab`～
  `ILevel_005.prefab` 均存在且未发现同名 prefab 地址冲突。
- 静态确认控制链未新增 `PlayEvents`，未修改配置、关卡 prefab、共享 `GamePlayPanel`、共享 `ActorCtrl`。
- `uncertain`：尚未执行 Unity PlayMode、Android/iOS 横屏触控和异步切玩法验证。
- 静态确认 GameplayPanel 的六个按钮、`BlackMask` 和 `ClickBlock` 已绑定，过场参数已序列化为默认值。
- 静态确认 Reset/Replay 重建路径调用 `CleanupCurrentLevelRuntime()`，同时销毁关卡实例和 GameplayPanel，
  随后的 `OpenCurrentLevelAsync()` 必定创建新 GameplayPanel，不再复用旧实例。
- 静态确认两组进度图片列表各有 5 个不同引用，进度容器的 5 个直接子节点均挂 Toggle，顺序为
  `IWanner_PointItem` 到 `IWanner_PointItem (4)`。
- `uncertain`：尚未在 Unity PlayMode 验证逐格状态与 Q 弹动画的实际视觉节奏。
- `uncertain`：尚未在 Unity PlayMode 验证首次进入、Reset、Replay 的黑幕时序与 ClickBlock 输入拦截。
- 静态确认门资源中 `door_close` Spine 仅包含 `close` 动画，`effect` 为独立 Spine 节点；prefab 序列化引用
  已指向对应的两个 `ActorSpineCtrl`。
- 静态确认 `IWannerLevelCtrl.Success(Collider2D) -> Resolved(Success) -> IWannerPlayCtrl.OnLevelResolved() ->
  IWannerGame_EnterDoorAnimCtrl.PlayAsync() -> ReloadCurrentLevelAsync()` 调用链成立，下一关索引在门表现完成后更新。
- `uncertain`：尚未在 Unity PlayMode 点击 LevelCtrl AutoBind 并保存 prefab，也未验证门触发、渐隐时序、
  连续进入第二关、Reset 中断与旧实例门禁。
- 当前整体进度与后续优先级见同目录 `IWannerGame玩法制作进度Checklist.md`。
- `uncertain`：`IWannerGameEnter` 尚未绑定到入口 prefab；逻辑脚本已提供，需资源侧挂载并指定/自动查找按钮。
- `uncertain`：尚未在 Unity YooAsset Collector 窗口执行收集检查，也未生成本地/远端包验证运行时清单。

## 回滚路径

- 删除 `IWannerPlayCtrl`、`IWannerGameLevelCtrl`、`IWannerGameEnter` 及其 `.meta`。
- 从 `PlayModeType`、`IPlayControllerPolicy`、`GameManager` 和 `LoadManager` 移除 IWanner 注册/初始化入口。
- 将 `IWannerLevelCtrl` 恢复为原临时事件转发版本，将 HUD 脚本恢复为空生命周期即可。
- 回滚不需要修改用户维护的 Excel、JSON、图片或生成配置代码；GameplayPanel prefab 需同时移除本次新增的
  `BlackMask`、`ClickBlock` 与三个过场参数序列化引用。
- 若只回滚 YooAsset 接入，从 `GamePackage/Level` 删除 WannerLevel collector 即可。
- 若只回滚入口门表现，移除 `IWannerGame_EnterDoorAnimCtrl.cs` 及其 `.meta`，并从
  `IWannerGame_Door.prefab` 删除对应 MonoBehaviour；无需修改配置、存档、玩法控制器或共享 Actor 代码。

## IWanner 临时 Group 选组入口（2026-08-11）

- `IWannerGameEnter` 不再固定进入首行 Group，改为创建 Popup 层的临时选组弹窗。
- 弹窗通过 `IWannerGameLevelCtrl.LevelGroups` 读取 Loading 已冻结的 LevelSort 顺序，每行创建一个 Item；Item
  只显示配置 `Id`，点击后把目标 `Id` 沿 `GameManager -> IWannerPlayCtrl -> IWannerGameLevelCtrl` 既有校验链
  提交，并始终从组内索引 0 开始。
- prefab 根组件、Container、失活 Item 模板、TMP 文本、Item `CustomButton` 和关闭按钮已静态绑定；没有新增
  DB/Save、全局事件、Analytics 或配置写入。
- 定向构建为 `0 error / 101 warning`；Unity PlayMode 的七项布局、逐组进入、重复打开、失败回滚与
  Android/iOS 横屏触控仍为 `uncertain`。完整基线、风险和回滚见 ChoiceLevelPopup 目录本地记录。

## IWanner Group 小关进度存档（2026-08-11）

### 需求、基线与影响范围

- 原存档已声明 `CurrentGroupID` 与 `CurrentLevel`，但 `IWannerPlayCtrl` 没有读写这两个字段；首 Group 入口和
  指定 Group 入口均硬编码索引 0，成功/Skip 只更新内存索引，因此退出或重启后必然回到第一小关。
- 本次只修改 `IWannerPlayCtrl.cs`、`IWannerGame.cs`、`IWannerGame_Partial.cs` 与 IWanner 本地记录，不修改用户
  维护的配置数据、关卡 prefab、UI prefab、资源地址、全局事件、广告策略或 Analytics。

### 修改后数据与运行流

1. 新存档的 `CurrentLevel` 默认值改为零基索引 0；`CurrentGroupID` 仍以 -1 表示尚未选择 Group。
2. 进入 Group 时，只有目标 Id 与存档 `CurrentGroupID` 相同且 `CurrentLevel` 在当前 Group Path 范围内才续关；
   选择其它 Group 或遇到越界旧档时从索引 0 开始，并在目标小关数据校验成功后保存有效入口。
3. 小关成功或 Skip 推进到下一小关时，先把下一索引写入 `DB.IWannerGame` 再走既有黑幕销毁重建链；因此退出、
   重启应用或重新经过 Loading 后，再次选择相同 Group 会恢复该索引。
4. HUD Reset 只重建当前小关，不修改保存索引；Victory Replay 将当前 Group 索引重置并保存为 0；完整 Group
   通关在同一次 `SaveIWannerGame()` 中记录 `PassedLevels` 并把当前索引重置为 0。
5. 存档仅在 Group/索引值变化时写入；写入失败记录 warning 但不打断当前游戏。GM 上一小关仍是临时调试行为，
   不倒退正式进度存档。

### 验证、剩余风险与回滚

- 已静态核对 `DBManager` 在无旧档时调用 `Init()` 并立即保存、有旧档时反序列化现有字段；因此默认索引与重启
  恢复均通过现有 PlayerPrefs SaveModule 链，不新增初始化入口。
- 已覆盖进入同组、切换异组、成功/Skip、Reset、Replay、完整 Group 结算及越界索引回退的代码路径。
- `Assembly-CSharp.csproj` 定向构建通过：`0 error / 101 warning`；warning 为工程既有范围，目标文件差异检查通过。
- `uncertain`：尚未在 Unity PlayMode 实测杀进程重启、PlayerPrefs 写入失败、配置缩短 Group 后的旧档修正，
  以及 Android/iOS 进程重启恢复。
- 回滚时移除 `ResolveEntryLevelIndex()`、`SaveCurrentLevelProgress()` 及四处进度写入，把两个进入入口恢复为索引 0，
  并把 `IWannerGame.cs` 与 `IWannerGame_Partial.Init()` 的 `currentLevel` 恢复为 1；已有存档字段可保留，无需删除
  玩家数据。

## IWanner 独立 RemoteConfig 接入（2026-08-10）

### 需求、基线与影响范围

- Luban `GlobalConst` 与运行时 JSON 已存在 9 个 `iWannerGame_*` 字段，但修改前启动流程不拉取 IWanner
  独立 remote key，业务层也没有对应 getter；这些字段只能作为生成数据存在。
- 本次新增顶层 `iwannergame_config`，影响 `ConfigManager.RemoteConfig`、`RemoteConfigService`、GM 缓存展示、
  Guru 默认配置、`LubanGenerator` 和本目录 Checklist；不修改 IWanner 关卡 prefab、存档、事件、Analytics
  或平台 SDK 调用顺序。

### 修改后配置与数据流

1. 启动阶段与 `level_config`、`drawgame_config` 一并拉取 `iwannergame_config`，解析后的 raw JSON 继续由
   `SetRawBatch(...)` 批量写入 `DB.GameData.RemoteConfigCache`。
2. `ConfigManager.RemoteConfig` 提供入口开关、Skip 失败次数、提示等待时间、五项插屏/免广告参数共 9 个
   专用 getter；只在 `Application.isPlaying` 时读取缓存，字段缺失时回退生成的 `GlobalConst`，全部不可用时
   按类型返回 `false`、`0` 或 `0f`。
3. `guru_spec.yaml` 与 `GuruAllSpecs.G.cs` 已写入默认值：`true / 2 / 20 / 30 / 30 / 6 / 5 / 30 / 5`。
4. `LubanGenerator` 新增独立菜单，从 `lubanconfig_tbglobalconst.json` 同步精确字段名；已有配置存在时保留未知
   扩展字段，只覆盖白名单字段，成功或内容一致后继续调用 Guru 官方生成菜单。
5. IWanner 入口现已消费 `GetIWannerGameIsOpen()`；失败/Skip、提示与广告流程仍未消费对应 getter，不把其余
   未实现业务误标为已接入。

### 验证、未验证项与回滚

- PowerShell 字段级校验确认 Guru YAML、Guru 生成 C#、GlobalConst JSON、运行时常量与生成器白名单的 9 个
  名称和值一致；YAML 内 JSON 可反序列化且类型分别为 bool、int、float 语义数值。
- `Assembly-CSharp.csproj` 与 `Assembly-CSharp-Editor.csproj` 定向构建均为 0 error；warning 分别为 101 和 4，
  属于工程既有范围。`git diff --check` 通过。
- `uncertain`：尚未在 Unity Editor 实际点击新菜单，也未验证真实 Firebase、旧缓存无新 key、离线 fallback、
  Unity PlayMode 或 Android/iOS 启动流程。
- 回滚时从启动拉取列表、GM 展示和 `ConfigManager.RemoteConfig` 删除 IWanner key/getter；删除 Guru YAML 与
  生成 C# 的默认行，并移除 `LubanGenerator` 的菜单、白名单和合并逻辑。无需迁移存档；旧缓存中的未知 key
  不再被读取，可在版本升级或 GM 清缓存流程中清除。

## IWanner Actor 死亡震屏（2026-08-10）

### 需求与行为

- `ILevel_001/Scene/Feedback` 已存在 `MMF_Player`，其 Feedback 列表配置为 `MMF_CameraShake`；本次不修改
  该震屏参数或 prefab 数据，只在 `IWannerLevelCtrl` 建立统一调用边界。
- `IWannerLevelCtrl` 新增 `TryActorDeath()`：仅在当前小关运行时已初始化、未暂停、未成功结算且 Actor 尚未
  死亡时接受一次请求；接受后立即关闭输入、调用共享 `ActorCtrl.Die()`，再以 Actor 世界坐标播放死亡 Feedback。
- 新增 `ActorDeath(Collider2D)` 作为 `OnTrigger2DHandle.OnEnter` 的 UnityEvent 适配入口，只接受 Player Tag；
  其它关卡脚本可直接调用 `TryActorDeath()`。
- 死亡状态与成功状态互斥；Actor 死亡后 `TrySuccess()` 和重新开放输入均会被拒绝。当前接口只保留死亡现场，
  不派发全局事件，也不负责失败次数、延时重建、Skip 或广告。
- 序列化引用为空时，运行时会在当前关卡层级查找名为 `Feedback` 的子物体并获取 `MMF_Player`；Odin
  `AutoBind` 同样会持久化该引用。找不到 Feedback 时会记录 warning，但不阻断 Actor 死亡。

### 影响、验证与回滚

- 影响文件仅为 `IWannerLevelCtrl.cs` 及 IWanner 本地记录；未修改共享 `ActorCtrl`、主线/Draw 关卡、配置、
  存档、UI、YooAsset 或平台 SDK。
- 静态确认 `ILevel_001` 的 Feedback 组件类型为 `MMF_Player`，内部 Camera Shake 参数当前为
  `Duration=0.15 / Amplitude=0.04 / Frequency=10`。
- `Assembly-CSharp.csproj` 定向构建通过：`0 error / 101 warning`；warning 属于工程既有范围。
- `uncertain`：尚未在 Unity PlayMode 验证实际镜头 Shaker、重复碰撞、死亡后接触成功门、Reset 重建以及
  Android/iOS 横屏真机表现。
- 若需回滚，仅移除 `deathFeedback` 引用、`ActorDeath/TryActorDeath` API、死亡门禁和 AutoBind 对应逻辑；
  `ILevel_001` 的既有 Feedback 对象无需修改。

## IWanner 组内进度 Q 弹入场（2026-08-10）

### 需求、修改前基线与影响文件

- 用户要求每次重新进入 `IWannerGame_GamePlayPanel` 时，组内进度 Toggle 从左到右依次以 Q 弹方式跳起，
  全部动画完成后才刷新已过关/未过关状态。
- 修改前 `Create(...)` 会立即按两组 Image 列表刷新状态，动画不存在；同时 prefab 两组列表各自重复引用第一格，
  无法独立显示五格状态。若直接在 `Create(...)` 播放动画，还会被 `OnOpen()` 的全黑遮罩覆盖。
- 影响文件为 `IWannerGame_GamePlayPanel.cs`、`IWannerGame_GamePlayPanel.prefab`、本目录 Checklist/功能记录/
  风险评估和 `DevelopMD/已有功能设计与使用记录.md`；未修改共享 `GamePlayPanel`、全局事件、存档、配置或广告链。

### 修改后行为与 UI 数据流

1. `Create(currentLevelIndex, levelCount)` 只缓存规范化后的状态，把有效 Toggle 统一预置为未过关，并按配置数量
   隐藏多余槽位；不在黑幕遮挡期间提前播放。
2. `GamePlayPanelFadeInEvent` 的黑幕完全淡出后启动进度动画；黑幕缺失或渐隐时长为 0 时走同一完成入口。
3. 运行时从显式绑定的横向容器读取直接子 Toggle，兄弟索引即从左到右顺序。每格执行压缩蓄力、上跃拉伸、
   落地挤压和轻微回弹，默认上跃 `28` UI 像素，相邻起跳间隔 `0.08s`，整体使用 unscaled time。
4. 最后一格落稳后统一调用 `SetIsOnWithoutNotify(i <= currentLevelIndex)`；已完成格与当前所在格为 on，后续格
   保持 off。面板关闭或销毁时 Kill 整体 Sequence，并恢复所有参与节点的初始位置与缩放。
5. prefab 的已过关/未过关两组生成 Image 列表也同步修正为各 5 个独立节点，作为进度容器缺失时的无动画降级。

### 验证、剩余风险与回滚

- `Assembly-CSharp.csproj` 定向构建通过：`0 error / 101 warning`；warning 为工程既有项。
- PowerShell 静态校验确认容器直接子节点顺序为五个 `IWanner_PointItem`，对应五个 Toggle；两组 Image 列表
  各有 5 个不同 fileID；`progressToggleGroup` 指向同一横向容器。`git diff --check` 通过。
- `uncertain`：尚未执行 Unity PlayMode 和 Android/iOS 横屏验证；实际 Q 弹高度、错峰节奏、横向布局回写、
  Reset/Replay 中断以及状态切换的视觉帧序仍需实机确认。
- 仅回滚本表现时，恢复 `InitializeGroupProgress` 的即时 Image 刷新，移除进度 Sequence、容器与两项动画参数，
  并从 prefab 删除对应序列化字段；若仍需保留正确逐格显示，可继续保留两组独立 Image 引用。

## GMPanel 上一小关与当前小关成功（2026-08-10）

### 需求、基线与影响范围

- 原 GMPanel 的上一关只修改主线 `LevelCtrl`，直接胜利也没有 IWanner 分支；在 IWanner 活动期间两者都无法作用于
  当前 Group 小关。
- `IWannerPlayCtrl` 实现统一 `IGMFeature`：`TryEnterPreviousLevel()` 与 `TryForceVictory(source)` 提供本玩法
  调试能力，`TryEnterNextLevel()` 明确返回 false；GMPanel 的玩法按钮逻辑迁入独立 partial 且不再感知具体类型。
- 未修改关卡 prefab、GameplayPanel prefab、Luban/RemoteConfig、DB、资源收集、广告、analytics、共享
  `ActorCtrl` 或全局事件定义。

### 修改后运行流

- 上一小关只在活动、非加载、当前运行时有效且索引大于 0 时接受。控制器先解析上一索引的 LevelData，成功后
  更新当前索引/数据，锁关卡与 HUD 输入，并复用 `ReloadCurrentLevelAsync()` 黑幕重建链；不写失败、Replay、
  通关或存档数据。
- GM 直接成功只调用当前 `IWannerLevelCtrl.TrySuccess()`；暂停、死亡、已提交成功或运行时未就绪时由现有门禁
  返回 false。接受后仍执行角色停靠成功门、进门动画、当前小关推进和末关待 Victory 降级逻辑。
- Main/Draw 的现有 GM 胜利链保持不变；本次没有新增 IWanner Group VictoryPanel 或末关奖励。

### 验证、剩余风险与回滚

- `Assembly-CSharp.csproj` 定向构建通过：`0 error / 101 warning`；warning 为工程既有基线。
- 静态确认上一小关在配置解析成功前不改索引，接受后使用 activation version 拦截旧异步结果；GM 成功没有绕过
  `TrySuccess()` 或直接派发主线 SUCCESS。
- `uncertain`：尚未在 Unity PlayMode 验证第 1 小关拒绝、2～5 小关连续回退、黑幕/输入恢复、加载失败、
  暂停/死亡/重复胜利与末关行为；Android/iOS 横屏调试触控也未验证。
- 回滚时从 IWanner 移除 `IGMFeature` 实现和三个 GM 方法，并恢复 GMPanel 的具体玩法分流；无需迁移存档或资源。

## 2026-08-10：ILevel_001 关卡配色资产

### 需求与实现范围

- 新增 `IWannerLevelColorSettings : ScriptableObject`，集中记录 Ground、Camera Solid Color 和
  GameplayPanel 三类颜色。
- 按用户确认，本阶段只创建一份 `ColorSettings/ILevel_001_ColorSettings.asset`，并且只绑定到
  `Assets/Game/Prefabs/WannerLevel/ILevel_001.prefab` 的 `IWannerLevelCtrl.colorSettings`；
  `ILevel_002`～`ILevel_005` 暂不绑定，不创建额外颜色资产。
- 资产初值来自 `ILevel_001` 修改前的现有视觉基线：Ground 为
  `(0.25490198, 0.3529412, 0.36862746, 1)`，Camera 背景为
  `(0.69803923, 0.76470596, 0.7294118, 0)`，GameplayPanel 图片保持白色。

### 运行时行为与数据流

- `IWannerPlayCtrl` 创建 GameplayPanel 后加载关卡 prefab；`IWannerLevelCtrl.InitializeRuntime()` 在开放输入前
  读取本关绑定资产，把 Camera 背景色写入当前关卡子层级的 Camera，并把 Ground 颜色写入名称以
  `Ground` 开头的全部 `SpriteRenderer`。
- 关卡初始化成功后，`IWannerPlayCtrl` 从 `IWannerLevelCtrl` 取得 GameplayPanel 颜色，并在黑幕淡出前调用
  `IWannerGame_GamePlayPanel.ApplyGameplayColor()`；该接口只修改六个操作按钮图标和两组共十个进度图片，
  不修改 BlackMask、ClickBlock 或文字颜色。
- 未绑定 `colorSettings` 时保留 prefab/HUD 原始颜色，不阻断进入、Reset、Replay 或组内切关。每次重建小关和
  GameplayPanel 都重新走同一应用链，因此没有跨关卡缓存颜色。
- 本次没有新增 DB、存档、配置表、全局事件、资源加载 API、打点、广告或平台 SDK 链路。

### 验证记录

- 定向 `Assembly-CSharp.csproj` 构建在显式收录新增脚本及当前工作区 partial 脚本后通过：
  `0 error / 102 warning`；warning 为工程现有项。
- 静态确认只有 `ILevel_001.prefab` 引用了 `ILevel_001_ColorSettings.asset`，资产脚本 GUID、asset GUID 和 prefab
  引用一致；`ILevel_002`～`ILevel_005` 无 `colorSettings` 序列化引用。
- 静态确认应用入口位于运行时 `InitializeRuntime()`，未新增 `OnValidate`、`Reset`、`ExecuteAlways` 或编辑器
  数据访问链。
- `uncertain`：尚未在 Unity PlayMode 目测 Ground、Camera 与 HUD 同帧切色，也未在 Android/iOS 横屏验证。

### 回滚路径

- 从 `ILevel_001.prefab` 移除 `colorSettings` 引用，删除配色 asset，即可让该关恢复使用 prefab/HUD 原始颜色。
- 若需要完全回滚能力，再移除 `IWannerLevelColorSettings.cs`、`IWannerLevelCtrl` 的配色应用与查询接口、
  `IWannerPlayCtrl` 的 HUD 调用以及 `IWannerGame_GamePlayPanel.ApplyGameplayColor()`；不涉及存档迁移。

### 2026-08-11：Trap SpriteRenderer 配色补充

- 原配色入口只按 `Ground*` 节点名匹配 SpriteRenderer，新建的
  `Assets/Game/Prefabs/IWannerLevel/IWannerGame_Trap.prefab` 根节点挂有 `OnTriggerTrapHandle`，其视觉不属于
  Ground 命名范围，因此不会应用本关 `GroundColor`。
- `IWannerLevelCtrl.ApplySceneColorSettings()` 现把“名称以 `Ground` 开头”或“位于
  `OnTriggerTrapHandle` 组件层级内”的 SpriteRenderer 统一写为同一 `GroundColor`；包括 Trap 根节点及任意深度子节点，
  不依赖 Trap GameObject 名称，也不增加新的 ColorSettings 字段。
- 本次只修改运行时一次性初始化扫描与文档，不修改 Trap/Ground prefab、配置、存档、事件、资源加载、Analytics
  或平台 SDK。Runtime 构建为 `0 error / 101 warning`，Editor 构建为 `0 error / 4 warning`。
- `uncertain`：尚未在 Unity PlayMode 目测 Trap 与 Ground 色值一致，也未验证 Trap 动画过程中其它脚本是否会再次
  覆盖 SpriteRenderer.color；Android/iOS 横屏显示尚未验证。
- 回滚时移除 `OnTriggerTrapHandle` 父层级判断，即恢复仅按 `Ground*` 名称着色；无需回滚配色资产或 prefab。

### 2026-08-11：GameplayPanel GroundColor 图片列表

- 修改前 GameplayPanel 只把 `GameplayPanelColor` 应用于六个操作按钮与两组进度图片，并把 `SkipTextColor`
  应用于 Skip 文本；prefab 中新增的六个 `colorBg` Image 没有面板序列化列表或运行时配色入口。
- `IWannerGame_GamePlayPanel` 新增序列化 `groundColorImages`，当前 prefab 已绑定六个 `colorBg` Image。
  `IWannerLevelCtrl.TryGetGameplayPanelColors()` 复用现有 `IWannerLevelColorSettings.GroundColor` 作为第三个输出，
  `IWannerPlayCtrl` 在关卡初始化成功、黑幕淡出前把它传给 `ApplyGameplayColors()` 批量写入列表。
- 颜色只在现有小关初始化链执行一次；列表为空或包含空引用时安全跳过。未绑定 `colorSettings` 的关卡仍不会调用
  配色接口并保留 HUD prefab 原色。本次未新增配色字段、DB/Save、全局事件、逐帧刷新、资源加载或平台分支。
- `Assembly-CSharp.csproj` 定向构建通过：`0 error / 101 warning`，warning 为工程既有项；静态确认唯一调用点已传递
  `GroundColor`，列表中的六个 fileID 均指向 Image 组件。`uncertain`：尚未执行 Unity PlayMode 目测切色、
  Reset/Replay/组内切关与 Android/iOS 横屏对比度验证。
- 单独回滚本增量时，移除 `groundColorImages` 字段及 prefab 列表绑定，把 LevelCtrl/PlayCtrl/GameplayPanel 的第三个
  Ground 参数恢复为原两色接口即可；现有场景 Ground/Trap 配色、配色资产、存档和资源路径不受影响。

## IWanner 入口 IsOpen 控制（2026-08-10）

### 需求、基线与修改后行为

- 修改前 `ConfigManager.RemoteConfig.GetIWannerGameIsOpen()` 已具备远端缓存优先、GlobalConst 回退能力，但
  `IWannerGameEnter` 未读取该值，`WannerGameEnter` 始终显示且点击始终尝试进入首个 Group。
- `IWannerGameEnter.OnEnable()` 和公开 `Refresh()` 现在统一读取入口开关。关闭时停用组件所在的
  `WannerGameEnter` 根节点，因此按钮、入口图片和提示气泡一并隐藏；开启时解析并绑定原 `enterButton`。
- 点击回调会再次读取同一 getter。即使外部代码或陈旧事件尝试直接调用，开关关闭时也只刷新入口为隐藏，
  不调用 `GameManager.TryEnterIWannerFirstGroup()`。
- `Refresh()` 只消费 Loading 后已经确定的业务配置，不拉取远端、不写缓存或存档；项目内没有游戏中实时更新
  RemoteConfig 的流程，新值仍在下一次 Loading 后生效。

### 影响、验证与回滚

- 影响文件为 `IWannerGameEnter.cs`、IWanner 本地记录和 Checklist；未修改入口 prefab、StartPanel、配置值、
  RemoteConfig 拉取、存档、资源加载、analytics、广告或平台 SDK。
- 静态确认 `IWannerGameEnter` 挂在 `WannerGameEnter.prefab` 根节点，`enterButton` 仍指向子级 `clickBtn`，因此
  根节点显隐覆盖完整入口且不需要新增序列化引用。
- `Assembly-CSharp.csproj` 定向构建通过：`0 error / 101 warning`；warning 属于工程既有范围。
- `uncertain`：尚未在 Unity PlayMode 验证远端 true/false、缓存缺字段时的 GlobalConst 回退、返回 StartPanel
  后的入口状态，以及 Android/iOS 横屏显示。
- 回滚时移除 `IWannerGameEnter` 对 `GetIWannerGameIsOpen()` 的显隐和点击校验，恢复原按钮绑定流程即可；
  不需要修改 prefab、配置或迁移存档。

## IWanner 普通失败计数与 RewardAd Skip 接入（2026-08-11）

### 需求、基线与影响范围

- 修改前 GameplayPanel 每次打开都会把 Skip 设为隐藏，工程内没有任何 `SetSkipVisible(true)` 调用；
  `GetIWannerGameShowSkipFailCount()` 虽已存在，但玩法未消费。Actor 死亡只锁输入、播放死亡动画和震屏，
  不会报告失败、累计次数或重建关卡。
- 失败闭环首次接入后，Skip 达到阈值会直接推进下一小关，不调用广告。当前需求明确改为必须观看
  RewardAd 且实际获得奖励后才允许跳关。
- 本次影响 `IWannerLevelCtrl.cs`、`IWannerPlayCtrl.cs`、`IWannerGame_GamePlayPanel.cs`、
  `IWannerGame_GamePlayPanel.prefab` 以及 IWanner 本地记录。
  其中 RewardAd 增量只修改 `IWannerPlayCtrl.cs` 和文档，复用既有 `RewardAdHelper`、
  `RewardPlacement.InGamePassLevel`、等待弹窗与本地化提示，不修改 GameplayPanel/prefab、广告 SDK、
  已有 `ILevel_001.prefab`、`IWannerGame_Door.prefab`、共享 `ActorCtrl`、主线/Draw 关卡、配置值、存档或 Analytics。

### 修改后行为与数据流

1. `IWannerLevelCtrl` 运行时订阅现有 `OnTriggerFailAreaEvent`。失败区域触发后统一调用幂等
   `TryActorDeath()`；若关卡已暂停、成功、死亡或未初始化则拒绝重复处理。
2. 死亡被接受后立即锁角色输入、调用 `ActorCtrl.Die()` 并播放既有 `deathFeedback`，随后通过当前关卡实例的
   `Resolved(Failed)` 事件报告给 `IWannerPlayCtrl`。没有新增全局失败事件。
3. `IWannerPlayCtrl` 在内存中维护当前小关失败次数。死亡结果与 HUD 手动 Reset 都只加一；死亡结果进入
   `_isAwaitingFailureReset` 状态，锁住 Actor 和常规 HUD 输入、解除 `ClickBlock`，并显示全屏
   `ResetClickArea`，不再按固定延时自动重建。
4. `ResetClickArea` 在 prefab 中默认隐藏并绑定到 GameplayPanel。用户点击后派发
   `IWannerGameResetEvent(GameResetType.Failed)`；活动 `IWannerPlayCtrl` 接收后隐藏点击区并复用既有黑幕
   销毁/重建链。失败次数已在死亡结果时记录，点击不会重复累计；普通 Reset 按钮仍派发 `Manually` 并计数一次。
5. Reset 完成黑幕重建后，只在新 GameplayPanel 创建阶段读取
   `ConfigManager.RemoteConfig.GetIWannerGameShowSkipFailCount()`：配置为 `n > 0` 时，累计次数
   `>= n` 即显示，因此第 `n` 次失败后的新面板出现；旧面板不会在失败现场中途显示。配置小于等于 0 时关闭 Skip。
6. Skip 按钮仍只调用当前 GameplayPanel 创建时注入的局部 `Func<bool>`。活动 `IWannerPlayCtrl` 校验加载、
   暂停、阈值、当前实例及重复广告请求后，先关闭 GameplayPanel/Actor 输入、调用
   `IWannerLevelCtrl.SetPaused(true)` 停住角色，再显示 ClickBlock 并调用 `RewardAdHelper`；没有新增全局跳关事件。
7. 广告位固定使用 `RewardPlacement.InGamePassLevel`，成功/失败提示复用
   `U_HurryTips_RewardVideo_succeed/failed`。只有 `onResult(..., true)` 会进入既有
   `AdvanceToNextLevelAsync()`；失败、关闭或等待取消返回 `false` 时恢复当前关卡输入、解除暂停和 ClickBlock，
   保留失败次数与 Skip 显示，玩家可继续游戏或再次点击。
8. 广告请求期间 Reset、Replay、暂停弹窗、Resume、GM 上一关和 GM 强制成功均被本地状态门禁拒绝。
   `Deactivate()` 会先失效请求状态并调用 `RewardAdHelper.CancelCurrent()`；回调同时校验 activation version、
   LevelCtrl 和 GameplayPanel 实例，退出或重建后的旧回调不会推进新会话。
9. 原生覆盖层期间不依赖 `ignoreTimeScale`、Tween 或 PlayerLoop 定时推进，恢复与跳关只从广告结果回调开始。
   成功和 Skip 推进、Replay、GM 回退、退出/清理都会清空当前失败次数；普通失败、手动 Reset 和广告失败不会清空。
   Group 最后一关仍进入既有的未完成 Victory 占位流程。

### 验证、剩余风险与回滚

- RewardAd 增量完成后，`Assembly-CSharp.csproj` 定向构建通过：`0 error / 101 warning`；warning 为工程既有范围。
  失败闭环首次接入时的 `Assembly-CSharp-Editor.csproj` 定向构建为 `0 error / 4 warning`。
- 静态确认 prefab 的 `ResetClickArea` 默认 `m_IsActive: 0`、`resetClickArea` 字段绑定到同节点 `CustomButton`，
  点击派发 `GameResetType.Failed`；同时确认 `SetSkipVisible(ShouldShowSkip())`、远端失败阈值 getter、Skip 局部回调和
  `RewardAdHelper -> onResult(bool) -> 成功推进/失败恢复`、
  `OnTriggerFailAreaEvent -> TryActorDeath() -> Resolved(Failed) -> OnLevelResolved()` 调用链均已建立；本次没有
  新增 `OnValidate`、`Reset`、`ExecuteAlways`、编辑态数据访问链或全局事件。
- `uncertain`：尚未在 Unity PlayMode 验证死亡后全屏点击区显隐/层级/触控、连续点击只重建一次、默认阈值 2
  时第二次失败重建后显示 Skip、手动 Reset 累计、奖励成功/失败/关闭/等待取消、退后台或退出玩法后的迟到回调、
  末关占位行为，以及 Android/iOS 横屏真实广告覆盖层和输入恢复。
- 只回滚 RewardAd 增量时，可移除 `_isSkipRewardAdRequesting`、广告回调/恢复方法及相关门禁，并把
  `TrySkipCurrentLevel()` 恢复为直接 `AdvanceToNextLevelAsync()`；完整回滚失败闭环时，再移除 LevelCtrl 失败上报、
  GameplayPanel 的 ResetClickArea/Skip 回调和 prefab 绑定。两种回滚均不涉及存档迁移。

## IWanner LevelCtrl 与 PlayCtrl 实例事件解耦（2026-08-11）

### 修改前后与职责边界

- 修改前 `IWannerPlayCtrl` 在 `InitializeRuntime(...)` 时把成功、失败两个异步委托注入关卡，
  `IWannerLevelCtrl` 保存委托、启动异步流程并捕获外围推进异常，导致单关控制器同时承担玩法流程协调职责。
- 修改后新增 `IWannerLevelResultType`、`IWannerLevelResult` 与实例级 `Resolved` 事件。
  `IWannerLevelCtrl` 只负责本地幂等、输入锁、成功瞬移、死亡与 Feedback，并同步上报一次结果。
- `IWannerPlayCtrl` 在初始化关卡前订阅、在 Shutdown/销毁前退订；成功门动画、下一小关推进、失败计数、
  死亡后的 ResetClickArea 等待状态、黑幕重建和异步异常降级全部由玩法控制器持有。
- 该事件只存在于当前关卡实例，不接入 Bear 全局事件总线；Reset、Replay 和退出销毁旧实例后不会保留订阅。
- `OnLevelResolved()` 的日志、输入/UI 锁定、结果分流与同步 Bear 事件派发现已统一纳入异常收口。同步阶段异常时，
  PlayCtrl 会递增 activation version 淘汰可能已启动的旧异步流程，并通过既有 `HandleOpenFailure()` 清理运行时；
  若异常来源已经不是当前关卡，只记录错误，不触碰新实例。

### 验证、剩余风险与回滚

- 静态确认旧 `_successHandler/_failureHandler`、运行会话字段及 LevelCtrl 内成功/失败异步包装已移除；
  PlayCtrl 的订阅早于 `InitializeRuntime()`，退订早于 `ShutdownRuntime()`。
- `Assembly-CSharp.csproj` 定向构建通过：`0 error / 101 warning`；warning 为工程既有项。
- `uncertain`：Unity PlayMode 尚未覆盖成功、死亡、Reset/退出打断异步流程以及连续重建后的重复订阅情况。
- `uncertain`：尚未主动注入同步 UI/事件订阅者异常，验证 StartPanel 降级画面和输入恢复结果。
- 回滚时可恢复 `InitializeRuntime(successHandler, failureHandler)` 与两条局部异步包装，并移除结果类型、事件及
  PlayCtrl 的订阅/退订；不涉及 prefab、配置、存档或资源迁移。

## IWanner Skip 文本独立配色（2026-08-11）

### 需求、基线与影响范围

- 修改前 `IWannerLevelColorSettings` 只提供 GameplayPanel 图片色；`ApplyGameplayColor()` 明确排除文字，
  因此 `u_level_devil_btn_skip_txt` 始终使用 GameplayPanel prefab 自身颜色。
- 现有生成字段 `ULevelDevilSkipTxtBtn` 已绑定到该 TextMeshProUGUI，prefab 基线文字色为
  `(0.18431373, 0.35686275, 0.36862746, 1)`，无需修改 prefab 或自动生成脚本。
- 影响文件为 `IWannerLevelColorSettings.cs`、`ILevel_001_ColorSettings.asset`、`IWannerLevelCtrl.cs`、
  `IWannerPlayCtrl.cs`、`IWannerGame_GamePlayPanel.cs` 及本地文档。

### 修改后行为与数据流

- 配色资产新增 `skipTextColor` 与只读 `SkipTextColor`；`ILevel_001_ColorSettings.asset` 使用上述 prefab 基线色
  作为新字段初值，保留现有 Ground、Camera 和 GameplayPanel 图片色不变。
- `IWannerLevelCtrl.TryGetGameplayPanelColors()` 同时返回图片色与 Skip 文本色；`IWannerPlayCtrl` 在关卡初始化
  成功、黑幕淡出前把两种颜色交给 GameplayPanel。
- `IWannerGame_GamePlayPanel.ApplyGameplayColors()` 继续着色原六个操作按钮图标和两组进度图片，并且只对
  `ULevelDevilSkipTxtBtn.color` 应用 Skip 文本色，不修改其它文字、BlackMask 或 ClickBlock。
- 未绑定配色资产时仍保留 GameplayPanel prefab 原始颜色；本次不增加 DB、事件、资源加载、打点、广告或 SDK。

### 验证、剩余风险与回滚

- Runtime 定向构建通过：`0 error / 0 warning`；Editor 定向构建通过：`0 error / 4 warning`，Editor warning
  来自既有 `BuildToolV2.cs`。
- 静态确认生成字段与 prefab fileID `4815849973215290273` 一致，配色 asset 已序列化 `skipTextColor`，旧单色
  API 引用已全部替换；相关脚本/asset/文档的 `git diff --check` 在收尾检查中执行。
- `uncertain`：尚未在 Unity PlayMode 目测 Skip 文本与按钮背景的对比度，也未在 Android/iOS 横屏验证。
- 回滚时移除 `skipTextColor/SkipTextColor`、恢复单色查询/应用方法，并从 ILevel_001 asset 删除对应序列化字段；
  无需修改 prefab、存档或资源地址。

## IWanner 进门成功动画节奏延迟（2026-08-11）

### 需求、基线与影响范围

- 修改前 Actor 被成功门接受并瞬移到门根节点后，`IWannerGame_EnterDoorAnimCtrl.PlayAsync()` 会立即
  `ResetState()`、播放关门动画并启动 effect fade；fade 完成即返回，`IWannerPlayCtrl` 随后立即重建下一小关。
- 本次只修改 `IWannerGame_EnterDoorAnimCtrl.cs` 和共享 `IWannerGame_Door.prefab` 的节奏参数；
  `IWannerLevelCtrl.PlayEnterDoorAnimationAsync()`、`IWannerPlayCtrl.ResolveCurrentLevelSuccessAsync()`、黑幕、
  资源加载、失败/Reset、存档、配置、事件和 Analytics 链均保持原入口。

### 修改后行为与时序

1. `PlayAsync()` 完成视觉重置并确认组件可用后，先等待
   `doorCloseDelayAfterActorEntered`，再调用既有 `Execute()` 播放关门动画与 effect fade。
2. 既有 `effectFadeDelay` 和 `effectFadeDuration` 语义不变；只有 fade 把 `IsDone` 置为 true 后，才开始等待
   `nextLevelDelayAfterFade`。
3. 第二段等待结束后 `PlayAsync()` 才返回；现有 PlayCtrl 因此仍在同一成功门禁内延后调用
   `AdvanceToNextLevelAsync()`，期间 Actor 与 HUD 输入继续保持锁定，未增加全局事件或新的切关入口。
4. 两个新字段均为 Inspector 可调的非负秒数，共享门 prefab 初值均为 `0.3` 秒；等待使用与既有门表现一致的
   Unity 缩放时间，并继续使用当前小关销毁令牌处理中途退出或切换。

### 验证、剩余风险与回滚

- `Assembly-CSharp.csproj` 定向构建通过：`0 error / 101 warning`；
  `Assembly-CSharp-Editor.csproj` 定向构建通过：`0 error / 4 warning`，warning 均为工程既有范围。
- 静态确认 prefab 已序列化两段 `0.3` 秒初值，成功调用链仍为
  `Resolved(Success) -> PlayEnterDoorAnimationAsync() -> PlayAsync() -> AdvanceToNextLevelAsync()`；未新增
  `OnValidate`、`Reset`、`ExecuteAlways`、DB/Save、资源加载、全局事件、Analytics 或平台 SDK 访问。
- `uncertain`：尚未在 Unity PlayMode 目测两段停顿、调节为 `0`/较大值、fade 缺失降级、中途 Reset/退出，
  也未在 Android/iOS 横屏验证最终节奏。
- 回滚时移除两个序列化字段及 `PlayAsync()` 的两段等待，并从共享门 prefab 删除对应 YAML 字段；既有
  `effectFadeDelay/effectFadeDuration`、LevelCtrl/PlayCtrl 和关卡资源无需回滚。

## IWanner 进门震屏与中等手机震动（2026-08-11）

### 需求、基线与影响范围

- 修改前 `TrySuccess()` 在锁定输入并把 Actor 同步到成功门后直接上报 `Resolved(Success)`，进门本身没有震屏
  或手机震动；已有 `deathFeedback` 只在 Actor 死亡时播放。
- 本次只修改 `IWannerLevelCtrl.cs`，复用五个现有 IWanner 关卡已经绑定的同类
  `Feedback/MMF_Player/MMF_CameraShake` 与项目现有 `VibrationManager`；不修改 prefab、共享 Actor、
  PlayCtrl、门动画时序、配置、存档、资源地址、事件或 Analytics。

### 修改后行为与调用顺序

1. `TrySuccess()` 首次接受成功后继续先锁输入、停止 Actor，并调用 `SnapActorToSuccessDoor()` 完成位置与物理同步。
2. 位置同步完成后调用本地 `PlayEnterDoorImpactFeedback()`：在 Actor 当前门位置播放一次既有
   `MMF_Player` 震屏，同时调用 `VibrationManager.Instance.Vibrate(VibrationIntensity.Medium)`。
3. 震屏或震动调用完成后才同步上报 `Resolved(Success)`；后续关门延迟、effect fade、切关延迟和黑幕重建
   保持原顺序。`_isResultSubmitted` 在表现前已锁定，因此同一关卡实例的重复 Collider 或 GM 成功不会重复触发。
4. `deathFeedback` 字段名为保持五个已绑定 prefab 的序列化兼容而保留；其旧注释因职责已经从“仅死亡”扩展为
   “死亡与成功进门共用局部震屏”而同步修正，没有改写其它原注释。
5. `VibrationManager` 会读取并遵守现有 `GameSetting.VibrationOn`。Android 的 Medium 为 100ms、振幅 128；
   iOS 继续使用该管理器既有的系统振动实现。

### 验证、剩余风险与回滚

- `Assembly-CSharp.csproj` 定向构建通过：`0 error / 101 warning`；
  `Assembly-CSharp-Editor.csproj` 定向构建通过：`0 error / 4 warning`，warning 均为工程既有范围。
- 静态确认 `ILevel_001`～`ILevel_005` 都存在 `Feedback` 节点并已序列化 `deathFeedback` 引用；新表现位于
  `SnapActorToSuccessDoor()` 之后、`Resolved(Success)` 之前，且没有新增编辑器生命周期、全局事件或资源加载。
- `uncertain`：尚未在 Unity PlayMode 目测震屏幅度，也未在 Android/iOS 真机验证中等震感、玩家关闭震动后的
  静默行为，以及构建后的 Android 合并 Manifest 权限结果。
- 回滚时移除 `PlayEnterDoorImpactFeedback()`、成功路径调用和 `Game.Common` 引用，并把 `deathFeedback` 注释恢复
  为仅死亡用途；prefab、存档、配置和门动画控制器无需回滚。

## IWanner Group VictoryPanel 闭环（2026-08-11）

### 需求、基线与影响范围

- 原 `HandlePendingGroupCompletion()` 在最后一小关完成后只恢复 HUD 并记录 Victory 未实现 warning；两个新建的
  IWanner Victory prefab 根节点仍绑定 DrawGame 脚本，不能由 IWanner 类型工厂打开。
- 本次新增 IWanner 普通/Reward 胜利脚本、`DB.IWannerGame` 静态入口，并修改 `IWannerPlayCtrl` 的末关结算、
  Replay 和返回选组编排；不修改 DrawGame/主线 Victory、关卡 prefab、配置、广告、SDK 打点或共享 GameplayPanel。

### 修改后运行流

1. 末关门动画结束后清空失败次数，锁定 HUD/Actor 输入并暂停当前 LevelCtrl；Group Id 不在
   `DB.IWannerGame.PassedLevels` 时创建 Reward 版，否则创建普通版。
2. Reward 版只播放 `1_appear01 -> 1_idle`、转盘预览和按钮组入场；奖励广告按钮不可交互，不请求广告或写 Bag。
   普通版播放 `2_appear01 -> 2_idle`；两版都隐藏复制资源中的 Draw 星级、分享和免广告入口。
3. 首次完成的 Group Id 写入 `PassedLevels` 并调用 `DB.SaveIWannerGame()`；本次不同时接入 currentGroup/currentLevel、
   解锁、续关、Reward、插屏或 analytics。
4. Replay 复用 `GameReplayEvent`，仅在 Group 已结算时由活动 IWanner 控制器接受，从当前 Group 索引 0 重建；
   next 复用 `EnterNextLevelEvent`，并与 SettingPopup Menu 共用退出入口：切回主线、恢复 StartPanel，再打开 IWanner 临时选组弹窗。

### 验证、剩余风险与回滚

- 定向 C# 构建通过：`0 error / 25 warning`，warning 为工程既有范围；两个 prefab 根组件 GUID 与必需引用已静态核对。
- `uncertain`：Unity PlayMode 的首通/复通、Spine/转盘表现、Replay/next 快速连点、存档重启、Resources 加载与
  Android/iOS 横屏触控尚未验证；正式 attempt 时长、解锁和 analytics 仍未实现。
- 完整功能、风险、验收与回滚见
  `Assets/Game/UI/IWannerGame/VictoryPanel/IWannerGame_VictoryPanel功能与风险记录.md`。

## IWanner 陷阱事件失败适配（2026-08-11）

### 需求、基线与影响范围

- 主线 `OnTriggerTrapHandle` 命中玩家后先播放陷阱表现并调用 `ActorCtrl.Die()`，延迟 0.5 秒派发既有
  `OnTriggerTrapEvent`。原 `IWannerLevelCtrl` 只订阅 `OnTriggerFailAreaEvent`，因此即使 Actor 已死亡，也不会
  上报实例级 `Resolved(Failed)`，IWanner 的失败计数、ResetClickArea 和后续重建均不会进入。
- 本次只修改 `IWannerLevelCtrl` 及 IWanner 本地记录；不修改主线 `OnTriggerTrapHandle`、事件定义、关卡 prefab、
  配置、存档、广告、Analytics 或平台 SDK。

### 修改后事件流

1. `IWannerLevelCtrl.OnEnable()` 同时订阅 `OnTriggerTrapEvent` 与 `OnTriggerFailAreaEvent`，`OnDisable()` 继续通过
   既有 `EventSubscriber` 统一退订。
2. 陷阱事件进入 `OnTriggerTrap()` 后复用 `TryActorDeath()`；实例级 `_isResultSubmitted` 继续保证成功、死亡和重复
   陷阱事件之间只接受一次结果。
3. 如果 `OnTriggerTrapHandle` 已先将 Actor 置为死亡，`TryActorDeath()` 不重复调用 `ActorCtrl.Die()`，但仍锁定输入、
   播放关卡 Feedback 并上报 `Resolved(Failed)`；随后由 `IWannerPlayCtrl` 进入既有失败计数和 ResetClickArea 流程。

### 验证、剩余风险与回滚

- Runtime 定向构建通过：`0 error / 101 warning`；Editor 定向构建通过：`0 error / 4 warning`。静态确认没有新增
  全局事件类型、存档或配置访问。
- `uncertain`：尚未在 Unity PlayMode 验证陷阱表现、0.5 秒延迟、一次失败计数和 ResetClickArea；
  `ILevel_006.prefab` 当前 `OnTriggerTrapHandle.knock` 仍为空，实际触发会在事件派发前被空引用阻断，需由 prefab
  数据绑定完成后再验收。
- 回滚时移除 `OnTriggerTrapEvent` 订阅与 `OnTriggerTrap()`，并恢复 `TryActorDeath()` 无条件调用 `actor.Die()`；
  主线脚本、事件定义与 prefab 无需回滚。

## IWanner Menu 与 Victory Next 返回 StartPanel 后选组（2026-08-11）

### 需求、基线与影响范围

- 前一版 IWanner 自定义 `ExitToLevelSelection()` 会保持控制器激活，清理关卡后直接打开临时选组弹窗；Victory next
  也使用同一直接选组 helper，因此 StartPanel 不会恢复。
- 用户确认 Menu 与 Victory next 都应执行正常退出语义：先恢复 StartPanel，再在其上弹出 IWanner 选组界面。本次只调整
  `IWannerPlayCtrl` 的共享退出入口；
  不修改通用 `GameSettingPopup`、共享 GameplayPanel、关卡 prefab、配置、存档、全局事件、广告或 Analytics。

### 修改后调用与状态流

1. `GameSettingPopup.MenuBtn -> IPlayPauseHost.ExitToLevelSelection()` 仍沿用通用协议。
2. Victory next 继续派发 `EnterNextLevelEvent`，活动 IWanner 控制器在 Group 已结算门禁通过后直接调用同一
   `ExitToLevelSelection()`，不复制另一套清理和导航逻辑。
3. 共享入口先调用 `GameManager.ReturnToStartPanel()`；玩法控制器切换会停用 IWanner、取消旧异步会话、销毁当前
   `IWannerLevelCtrl` 与 GameplayPanel，并由主线恢复 StartPanel。
4. StartPanel 恢复成功后创建 `IWannerGame_ChoiceLevelPopup`；Item 点击继续走现有 `TryEnterIWannerGroup()`。
   弹窗创建失败时保留 StartPanel，避免玩家落在无可用界面的状态。

### 验证、剩余风险与回滚

- 静态确认 Menu 与 Victory next 共用 `TryReturnToStartPanelAndOpenLevelSelection()`，并由已有玩法切换清理 IWanner；
  本次没有新增全局事件、配置或存档写入。`Assembly-CSharp.csproj` 构建通过：`0 error / 1119 warning`，warning 为工程既有范围。
- `uncertain`：尚未在 Unity PlayMode 覆盖暂停后 Menu、Victory next 快速连点、StartPanel 与 Popup 层级、旧关卡异步
  回调失效、同组断点续关、弹窗创建失败以及 Android/iOS 横屏触控。
- 回滚仅需恢复 `OnEnterNextLevelEvent()` 与 `ExitToLevelSelection()` 直接打开选组的旧实现；其它 IWanner 入口、
  Victory、弹窗、存档和关卡资源不受影响。

## IWanner Reward Victory 真实广告金币（2026-08-11）

### 需求替换与行为

- 用户确认将原“Reward 版只展示、不发真实奖励”约定替换为：
  `IWannerGame_GameVictoryPanel_Reward` 的奖励广告、金币发放和领奖表现与
  `DrawGame_GameVictoryPanel_Reward` 保持一致；权威 Checklist 已先更新再修改实现。
- Reward 入场完成后启动转盘；点击广告按钮先锁定 next/replay/广告按钮并立即停转盘，再通过
  `RewardAdHelper` 请求 `RewardPlacement.GameVictory`，不等待原生覆盖期间的 Unity Tween 或 PlayerLoop。
- 失败、取消或加载未成功时不写 Bag，重新启动转盘并恢复交互。成功回调以单面板完成标记阻止重复领取，
  读取 `HorizontalRandomAdRewardCtrl.GetCurrentReward()`，通过
  `Bag.AddTool(GameProps.Coin, coins, RewardType.Ad)` 自动保存并沿用既有奖励打点。
- 金币写入后从转盘指针播放 `CommonUIRewardAnimCtrl`，完成后刷新 `PropBlock`；动画缺失只降级表现，不回滚金币。
  `AfterAdNextBtn` 与 Draw 当前实现一致，打开时保持隐藏；IWanner 的 Replay/next/选组语义不改变。

### 覆盖层边界、验证与回滚

- Victory 创建前当前 Level 已暂停，Actor/HUD 输入与 GameplayPanel Block 已锁定；广告请求和领奖动画期间面板的
  `HasExtraBusyState()` 继续锁定导航，回调完成后由 Reward 面板恢复按钮状态。
- `Assembly-CSharp.csproj` 构建通过：`0 error / 1119 warning`；静态确认 Reward prefab 的广告按钮、转盘、
  奖励动画和 PropBlock 引用非空。Unity PlayMode、真实 SDK 成功/失败/取消/超时、退后台及 Android/iOS 横屏仍为
  `uncertain`。
- 回滚只需恢复 IWanner Reward 脚本的无奖励预览逻辑；已发放 Coin 不自动追回，普通 Victory、Group 进度、
  插屏和选组流程无需回滚。

## IWanner QBox 进门关闭碰撞（2026-08-12）

### 需求、基线与影响范围

- 用户要求 QBox 通过成功门后停止参与碰撞，避免进门表现期间继续与 Ground、Door、Trap 或其它 2D Collider
  发生物理接触。
- 修改前 `IWannerLevelCtrl.TrySuccess()` 只锁定输入、调用 `ActorCtrl.StopMoving()`、清零刚体速度并把 Actor
  同步到成功门位置；`Actor_IWanner` 根 `Rigidbody2D` 与根/`pushCollider` 的 `BoxCollider2D` 仍保留物理模拟。
- 本次只修改 `Assets/Game/Scripts/Level/IWannerLevel/IWannerLevelCtrl.cs` 及 IWanner 本地中文记录；不修改共享
  `ActorCtrl`、Actor/门/关卡 prefab、碰撞矩阵、配置、资源地址、存档、UI、全局事件、Analytics 或平台 SDK。

### 修改后行为与生命周期

1. 成功门首次接受 Player 后，现有链路仍先锁定结果与输入、停止 Actor，并把 QBox 同步到 Door 位置。
2. 同步完成后，`DisableActorCollisionAfterDoorEntry()` 获取当前 Actor 层级内全部 `Collider2D`，包含失活的
   `pushCollider`，逐一关闭 `enabled`；随后再次清零根 `Rigidbody2D` 速度并设置 `simulated = false`。
3. 碰撞关闭发生在震屏、手机震动和 `Resolved(Success)` 上报之前，因此门动画等待与 Group 推进期间 QBox 不再
   参与 2D 物理接触。现有成功结果、门动画、黑幕、下一小关和 Group Victory 流程不变。
4. 当前成功小关会被既有流程销毁并创建新关卡实例，新 Actor 从 prefab 默认碰撞状态初始化；旧成功实例不恢复碰撞。

### 验证、剩余风险与回滚

- CodeGraph 已重新索引并确认唯一调用链为
  `TrySuccess() -> SnapActorToSuccessDoor() -> DisableActorCollisionAfterDoorEntry() -> Resolved(Success)`，没有修改
  失败、Reset、Replay 或共享 Actor 调用链。
- 静态确认 `Actor_IWanner.prefab` 当前只有一个根 `Rigidbody2D`，并有根与失活 `pushCollider` 两个
  `BoxCollider2D`；实现使用 `GetComponentsInChildren<Collider2D>(true)` 覆盖二者。
- Unity Editor 日志已检测脚本变更并执行 Hot Reload，未观察到本次代码的编译错误；独立
  `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies` 因 `Temp/bin/Debug` 缺少约 200 个 Unity/Package
  元数据 DLL 而返回 `CS0006`，不能作为完整编译通过证据。
- `uncertain`：尚未在 Unity PlayMode 目测 QBox 进门后不再顶住门/地面、连续五关推进、末关 Victory、成功后退出，
  以及 Android/iOS 横屏表现。
- 回滚时从 `TrySuccess()` 移除 `DisableActorCollisionAfterDoorEntry()` 调用并删除该私有 helper，即恢复原有进门物理行为；
  prefab、配置、存档与事件无需回滚。

## IWanner 左右移动反向映射（2026-08-12）

### 需求、基线与影响范围

- 用户要求 `IWannerLevelCtrl` 提供可由外部设置的 `IsReverse` 状态；状态为 true 时，右移与左移事件应交换触发方向。
- 修改前 `OnPlayerRightMove()` 固定向 `SetMoveInput()` 传入 `1f`，`OnPlayerLeftMove()` 固定传入 `-1f`，外部无法按小关运行状态切换映射。
- 本次修改 `IWannerLevelCtrl.cs`，并新增本关局部的 `IWannerGame_SetReverseListener.cs` 供现有
  `TriggerExecutor` / `BaseItemExecutor` 配置调用；不修改共享 `ActorCtrl`、HUD、关卡 prefab、全局移动事件、
  配置、存档、资源、UI、Analytics 或平台 SDK。

### 修改后行为与事件流

1. `IWannerLevelCtrl.IsReverse` 公开当前反向状态，默认 false；外部通过公开 `SetReverse(bool)` 设置，可在运行时初始化前或运行期间调用。
2. false 时保持现有映射：`PlayerRightMoveEvent -> SetMoveInput(1f)`，`PlayerLeftMoveEvent -> SetMoveInput(-1f)`。
3. true 时交换映射：`PlayerRightMoveEvent -> SetMoveInput(-1f)`，`PlayerLeftMoveEvent -> SetMoveInput(1f)`；
   `PlayerMoveCancelEvent` 仍传入 `0f`，跳跃、暂停、输入锁、死亡和成功流程不变。
4. `SetMoveInput(float)` 保持“传入角色物理方向”的既有语义，不在内部再次反转，避免未来直接调用者遭受双重反转。
5. IWanner HUD 按住方向按钮时会逐帧派发对应移动事件，因此运行期间切换状态后，下一帧事件按新映射更新角色方向。
6. `IWannerGame_SetReverseListener` 继承 `BaseItemEventHandle`，Inspector 可指定 `IWannerLevelCtrl` 和目标布尔值；
   未指定控制器时仅在执行阶段从父级查找。触发后同步调用 `SetReverse(isReverse)` 并立即完成，目标缺失时使用
   `Bear.Logger` 警告且仍结束执行，避免阻塞顺序执行器。
7. Listener 的 `ResetState()` 沿用基类，只重置执行状态，不自动恢复 `IsReverse`；需要退出反向区域时应再配置一个
   `isReverse = false` 的 Listener，确保设计意图显式可见。

### 验证、剩余风险与回滚

- 静态确认左右事件只在 `IWannerLevelCtrl` 内映射到 `SetMoveInput()`；Listener 只在触发时查找一次父级并同步写值，
  不新增 Update、全局事件、持续 GC 分配、资源或存档访问。
- `git diff --check` 对本次相关文件返回 0；Unity 生成的 `Assembly-CSharp.csproj` 已收录
  `IWannerGame_SetReverseListener.cs`，Editor 导入后完成一次 `Finished compiling in 20s` 的 C# 编译，导入点之后未出现
  新的 `error CS`。Unity PlayMode 中的触发器绑定、动态切换、关卡 Reset/重建及 Android/iOS 横屏触控仍为
  `uncertain`。
- 回滚时删除 `IWannerGame_SetReverseListener.cs` 及其 meta，再删除 `IsReverse`、`SetReverse(bool)` 并把两个事件入口
  恢复为固定 `1f/-1f`；无需修改 prefab、配置、存档或共享输入系统。

## IWanner Actor 跳跃开关与目标刚体跳跃监听（2026-08-12）

### 需求、基线与影响范围

- 用户要求 `IWannerLevelCtrl` 提供控制 Actor 跳跃是否生效的外部 API；关闭后 Actor 不再响应跳跃，同时新增一个
  Listener，让指定 `Rigidbody2D` 独立监听玩家跳跃事件并执行跳跃。
- 修改前 `PlayerJumpEvent` 在输入门禁通过后必定进入 `Actor_IWanner.TriggerGroundedJump()` 或共享
  `ActorCtrl.TriggerJump()`，LevelCtrl 没有实例级跳跃开关，也没有 IWanner 局部的目标刚体跟跳组件。
- 本次只修改 `IWannerLevelCtrl.cs`，新增 `IWannerGame_SetJumpEnabledListener.cs`、
  `IWannerGame_JumpListener.cs` 及各自 meta，并更新本地中文记录；不修改共享 `ActorCtrl`、HUD、
  `PlayerJumpEvent`、prefab、配置、存档、资源、Analytics 或平台 SDK。

### 修改后行为与事件流

1. `IWannerLevelCtrl.IsJumpEnabled` 公开当前 Actor 跳跃响应状态，默认 true；外部使用
   `SetJumpEnabled(bool)` 在初始化前或运行期间设置。
2. `TriggerJump()` 在原有输入、暂停和结算门禁之前增加 `IsJumpEnabled` 判断。false 时只拒绝后续 Actor 跳跃请求，
   不清除已有 Y 速度、不修改 HUD，也不阻止其它局部组件接收同一 `PlayerJumpEvent`。
3. `IWannerGame_SetJumpEnabledListener : BaseItemEventHandle` 专门负责控制开关：Inspector 配置目标 LevelCtrl 与
   `isJumpEnabled`，由 `TriggerExecutor` / `BaseItemExecutor` 执行后调用 `SetJumpEnabled(bool)` 并同步完成；缺少目标时
   使用 `Bear.Logger` 警告并结束，避免阻塞执行链。它不订阅 `PlayerJumpEvent`，也不操作 Rigidbody2D。
4. `IWannerGame_JumpListener` 专门负责目标刚体跟跳：在 PlayMode 的 `OnEnable()` 中通过 `EventSubscriber` 订阅
   `PlayerJumpEvent`，`OnDisable()` 解除订阅；它不调用 LevelCtrl 的跳跃开关 API，编辑态不会创建事件订阅实例。
5. Jump Listener 的 `target` 可显式绑定；留空时只回退获取当前 GameObject 上的 `Rigidbody2D`。每次收到跳跃事件时保留
   `linearVelocityX`，把 Y 速度写为非负 `jumpVelocity`；目标缺失、未模拟、Static 或速度小于等于 0 时不执行。
6. Jump Listener 不做接地检测，因此每个有效的离散跳跃事件都可重新写入目标 Y 速度；是否允许空中重复响应由关卡设计通过
   组件启用状态、输入节奏或其它局部门禁控制。

### 验证、剩余风险与回滚

- 精确搜索确认 IWanner HUD 仅在 `_inputEnabled` 为 true 时派发 `PlayerJumpEvent`，现有 Actor 跳跃同样通过
  `Rigidbody2D.linearVelocity` 写入 Y 速度；Setter Listener 与 Jump Listener 没有交叉职责，均未新增 Update、协程、
  全局事件类型、资源或存档访问。
- `git diff --check` 对控制器与两个 Listener 返回 0。Unity 生成的 `Assembly-CSharp.csproj` 与
  `Assembly-CSharp.rsp` 已分别收录两个脚本；使用 Unity 同版本 Roslyn 按该 rsp 编译完整 Assembly-CSharp，返回
  `CSC_EXIT=0`，只有工程既有 warning。
- `uncertain`：尚未在具体 prefab 绑定 Listener，也未完成 PlayMode 中的 Actor 开关、目标刚体单次/连续跳跃、暂停、
  Reset/重建以及 Android/iOS 横屏触控验证。
- 回滚时删除两个 Jump Listener 脚本及其 meta，删除 `IsJumpEnabled`、`SetJumpEnabled(bool)`，并把
  `TriggerJump()` 恢复为只检查 `CanAcceptInput()`；共享 Actor、HUD、事件、prefab、配置和存档无需回滚。

## IWanner 第 5 组地图拼接（2026-08-12）

### 需求、基线与影响范围

- 用户要求按 `Assets/Game/Prefabs/WannerLevel/5-1.png`～`5-5.png` 的地图轮廓继续拼接当前 `ILevel_` 结构。
- 配置静态证据确认第 5 组 Path 为 `30021`～`30025`，对应资源名为 `ILevel_021`～`ILevel_025`；本次没有修改
  `tbiwannerlevelsort.json`、`tbiwannerleveldata.json` 或运行时代码。
- `ILevel_021.prefab` 已有 Actor、Door、Camera、Feedback、FallArea、单块 Ground 和 5-1 对照图；本次以该结构为模板新增
  `ILevel_022.prefab`～`ILevel_025.prefab`，并保留各自独立 meta GUID。

### 拼接结果与资源流

- `ILevel_021 / 5-1`：保留中央单平台、左侧 Actor 与右侧 Door。
- `ILevel_022 / 5-2`：一块长平台、4 组向上陷阱，Door 在左、Actor 在右。
- `ILevel_023 / 5-3`：一块长平台、3 组向上陷阱，Actor 位于平台中左、Door 在右。
- `ILevel_024 / 5-4`：左/右边界、顶部、左右底台共 5 块 Ground；底部 2 组向上陷阱、顶部 4 组反向陷阱；Actor 在左底台，Door 在右底台。
- `ILevel_025 / 5-5`：5 段独立纵向平台，Actor 与 Door 分别位于最左、最右平台。
- Ground 继续使用既有 Ground SpriteRenderer 与 BoxCollider2D，命名保持 `Ground*`，可被 `IWannerLevelCtrl` 配色逻辑识别；
  陷阱复用 `IWannerGame_Trap.prefab`、当前 Actor 引用和关卡内共享 `Knock_Explosion` 粒子，继续派发既有陷阱失败事件。
- 5-1～5-5 图片节点保留在各 prefab 中作为编辑对照，但默认 `m_IsActive = 0`，避免网页截图和参考色在运行时显示。

### 验证、未覆盖与回滚

- 文件级校验确认 5 个 prefab 的所有本地 fileID 均有定义且无重复；Ground 数量依次为 `1/1/1/5/5`，陷阱实例数量为
  `0/4/3/6/0`，根资源名、图片 GUID、Actor 与 Door 引用均匹配预期。
- Unity Editor 日志确认 `ILevel_021`～`ILevel_025` 均由 PrefabImporter 成功导入，未出现本批资源的 YAML、反序列化或
  Prefab 导入错误。
- `uncertain`：尚未在 PlayMode 实际跑通 5 个小关，角色出生落地、陷阱触发范围、5-4 顶部反向尖刺、窄坑掉落、门触发、
  连续切关和 Android/iOS 横屏手感仍需人工验收；当前工程完整编译仍被工作区既有 Level 71 `CS1061` 阻断。
- 回滚时删除 `ILevel_022`～`ILevel_025` 及对应 meta，并把 `ILevel_021` 恢复到拼接前版本；配置未改，不需要回滚配置、
  存档、事件或共享 Actor/Trap 资源。

## IWanner 小关历史重试存档补齐（2026-08-12）

### 需求、基线与影响范围

- `DB.IWannerGame.LevelReviveCount` 已有存档字段，选组 Popup 也会汇总其 Value，但原 `IWannerPlayCtrl` 的死亡和
  手动 Reset 只更新 `_currentLevelFailureCount` 与插屏策略，没有写入该字典，导致显示长期为 `0`。
- 本次修改 `IWannerPlayCtrl.cs` 与 `IWannerGame.cs` 的字段注释，沿用 Bear SaveModule 的
  `DB.SaveIWannerGame()`；不新增存档字段、全局事件、配置、资源、Analytics 或平台分支。

### 修改后行为与数据流

1. 手动 Reset 仅在 `_isAwaitingFailureReset == false` 的原计数分支调用 `RecordCurrentLevelReviveCount()`；死亡后
   点击 Reset 只重建关卡，不会重复累计。
2. 死亡结果进入 `ShowFailureResetArea()` 时记录一次，与既有 `_currentLevelFailureCount` 和插屏失败计数同源。
3. 写入方法以当前 `IWannerLevelData.Path` 为 Key，修复旧档空集合后执行
   `LevelReviveCount[levelPath] + 1` 并立即保存；保存失败记录 Bear Logger warning，不中断失败/Reset 重建。
4. Group Replay、成功推进与 Skip 不调用该方法；选组 Popup 重新打开后汇总全部小关 Value，显示 IWanner 历史
   “手动 Reset + 死亡失败”总数。

### 验证、剩余风险与回滚

- 静态确认写入方法只有手动 Reset 非死亡分支和死亡结果分支两个调用点；既有 `_isAwaitingFailureReset` 门禁保持不变。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:q '-clp:ErrorsOnly;Summary'` 通过，结果为
  `0 error / 101 warning`；warning 属于工程现有范围。
- `uncertain`：尚未在 Unity PlayMode 验证手动 Reset、死亡后 Reset 去重、跨小关 Key、Popup 重开刷新、保存失败、
  杀进程重启及 Android/iOS 横屏流程。
- 回滚时移除两个调用点和 `RecordCurrentLevelReviveCount()`，并恢复 `IWannerGame.cs` 的字段注释；已有玩家
  `LevelReviveCount` 数据可保留，不会影响进度、通关或广告字段。

## IWanner 小关运行统计与 Group 正式广告解锁（2026-08-12）

> 本节替代上方“历史重试存档补齐”中的临时直写方案；上方内容仅保留演进记录，最终实现以本节和当前代码为准。

### 需求、参考基线与影响范围

- 用户要求把 `DB.IWannerGame` 中的 `LevelPlayCount`、`LevelSuccessCount`、`LevelReviveCount`、
  `LevelPlayDuration` 全部接入，并把统计职责从 `IWannerPlayCtrl` 抽成独立脚本；同时要求
  `UnlockGroups` 具备正式广告解锁写入，而不是只读占位。
- 主线参考基线为独立 `LevelRuntimeState`：四项历史数据均以 `LevelPath` 为 Key；DrawGame 参考基线为
  `DrawPlayCtrl` 内的 `Level Analytics` 区域：每次尝试开始、重试、成功和结束均有明确门禁，但计数使用
  `DrawLevelData.Id`、时长使用 Path。IWanner 最终采用主线的职责拆分和统一 Path 键，同时保留 DrawGame 的
  尝试幂等结算边界。
- 新增 `IWannerLevelRuntimeStatistics.cs` 及 meta；修改 `IWannerPlayCtrl.cs`、`IWannerGame.cs`、
  `IWannerGame_ChoiceLevelPopup.cs`、`IWannerGame_ChoiceLevelItem.cs` 和本地记录。没有新增存档字段、Luban 配置、
  资源地址、全局事件、SDK Analytics 事件或移动端平台分支。

### 运行统计生命周期与数据流

1. 小关资源和运行时创建完成、即将开放输入前，`BeginAttempt(Path)` 增加一次 `LevelPlayCount[Path]` 并保存；
   加载失败、广告等待和 GM 无统计切关不会提前增加游玩次数。
2. 控制器仅在玩法活动、未暂停、未加载、Group 未结算且关卡实例存在时，把 `Time.deltaTime` 交给统计对象；
   Reset、Fail、成功、Skip 成功或正式退出时，`CompleteAttempt(...)` 把本次毫秒时长累加到
   `LevelPlayDuration[Path]`。
3. 手动 Reset 的非死亡分支和死亡结果分支以 `isRetry=true` 结算，各增加一次
   `LevelReviveCount[Path]`；死亡后点击 Reset 仍由 `_isAwaitingFailureReset` 去重，不重复累计。
4. 真实过关与 Skip 激励视频成功均以 `isSuccess=true` 结算，增加一次 `LevelSuccessCount[Path]`；广告失败、
   取消及普通退出不增加成功次数。每次尝试先清空活动标志再写存档，同一结果重复回调不会重复结算。
5. `Deactivate()` 先按非成功、非重试结束活动尝试并保存时长；最终清理和 GM 上一关使用
   `DiscardAttempt()`，其中 GM 重载显式传入 `recordAttempt=false`，保持无统计语义。
6. 四项字典全部以 `IWannerLevelData.Path` 为 Key；`revive_txt` 继续汇总
   `LevelReviveCount.Values`，只负责展示历史重试总数。

### UnlockGroups 正式解锁流程

1. `IWannerGame_ChoiceLevelItem` 的 Lock 状态恢复局部点击绑定，但只调用锁定回调；普通状态继续调用进入回调，
   Item 不直接读写广告或存档。
2. Popup 收到锁定点击后，`LockType == Ad` 使用 `RewardPlacement.OutGameUnlockLevel` 请求激励视频，并在请求期间
   禁用关闭按钮及重复点击；非 Ad 锁定项显示既有 `U_HurryTips_TBC` 提示。
3. 只有广告成功回调会把 Group Id 幂等加入 `DB.IWannerGame.UnlockGroups`、调用 `DB.SaveIWannerGame()`，随后复用
   `TryEnterIWannerGroup()` 的权威校验进入该组。失败或取消不写入；关闭 Popup 会取消其持有的当前广告请求并清理回调状态。
4. `IWannerPlayCtrl.CanEnterGroup()` 继续在资源加载前二次校验 `UnlockGroups`，因此显示层回调不能绕过权限。
   当前配置中的普通锁定组为 `LockType.Ad`；未来若出现 `Level` 锁定，现阶段只提示待开放，不静默写入。

### 验证、未覆盖与回滚

- 静态检查确认 `IWannerPlayCtrl` 不再直接读写四项历史字典；统计对象的开始调用 1 处、逐帧调用 1 处，
  Reset/Fail/成功/Skip/退出均落入统一幂等结算入口。Popup 的 `UnlockGroups.Add` 只位于激励视频成功分支。
- Unity 生成的 `Assembly-CSharp.csproj` 已收录新脚本；执行
  `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:q '-clp:ErrorsOnly;Summary'` 通过，结果为
  `0 error / 101 warning`。warning 属于工程现有范围。
- `uncertain`：尚未在 Unity PlayMode 和 Android/iOS 真机验证首次进入、连续 Reset、死亡后 Reset 去重、Skip
  成功/失败/取消、退出时长、暂停时长、杀进程重启、真实广告覆盖层、存档失败及解锁后重进。
- 回滚统计时删除 `IWannerLevelRuntimeStatistics.cs` 及 meta，并移除控制器的开始、Tick、结算、丢弃和
  `recordAttempt` 参数；回滚解锁时移除 Popup 激励视频流程及 Item 的锁定回调。已有四项历史数据与
  `UnlockGroups` 可安全保留，不影响旧版读取；若产品要求数据级撤回，需另做存档迁移，不能只还原代码。

## IWanner 第 6 组地图拼接与可复用 Skill（2026-08-12）

### 需求、原始基线与影响范围

- 用户要求继续按 `Assets/Game/Prefabs/WannerLevel/6-2.png`～`6-6.png` 拼接第 6 组小关；配置静态证据确认
  Group 6 的 `30026`～`30030` 已对应 `ILevel_026`～`ILevel_030`，本次不修改配置、运行时代码或存档。
- 以最近完成的 `ILevel_025.prefab` 为只读模板，保留 `IWannerLevelCtrl`、Actor、Door、Camera、Feedback、
  FallArea、颜色资源、进门动画控制与成功回调。模板中已有左、右、顶三块相机边界 Ground，本批每关继续保留。
- 新增仓库级 Skill `../.agents/skills/brainpunk-stitch-iwanner-level-prefab/`，把 100 PPU 测量、规格化生成、
  本地 fileID/GUID 校验及交付记录要求固化；Skill 不属于 Unity `Assets`，不进入运行时资源流。

### 拼接结果与资源流

- `ILevel_026 / 6-2`：左右两端平台，Actor 在左、Door 在右；2 块可见平台加 3 块固定边界，共 5 块 Ground。
- `ILevel_027 / 6-3`：同高度五段平台，Door 在左、Actor 在右；5 块可见平台加固定边界，共 8 块 Ground。
- `ILevel_028 / 6-4`：左右高台加中央下层台，Actor 与 Door 分别位于左右高台；3 块可见平台加固定边界，共 6 块 Ground。
- `ILevel_029 / 6-5`：左右高台之间配置 5 个窄踏板；7 块可见平台加固定边界，共 10 块 Ground。
- `ILevel_030 / 6-6`：左下起点、7 级上升踏板与右上门台；9 块可见平台加固定边界，共 12 块 Ground。
- 全部 Ground 继续使用模板 SpriteRenderer、BoxCollider2D、Layer 6 与 `Ground (N)` 命名；参考图节点分别绑定
  6-2～6-6 的图片 GUID 并保持 `m_IsActive = 0`。没有新增 Trap、事件、配置、存档、Analytics 或平台分支。

### 验证、未覆盖与回滚

- Skill `quick_validate.py` 在 `PYTHONUTF8=1` 下通过；两个 Python 脚本通过语法检查，测量脚本以 6-6 完成一次真实输入冒烟。
- 生成器 `--dry-run` 与生成后 `--validate-only` 均通过；5 个 prefab 的根名、参考图名/GUID/失活状态、Ground 数量与
  完整几何、Actor/Door 坐标、关键 LevelCtrl 绑定、本地 fileID 完整性和重复 fileID 检查全部为 true。5 个 prefab
  meta GUID 互不重复。
- Unity Project Browser 已显示 `ILevel_026`～`ILevel_030`，Editor.log 记录了目标 prefab/meta 的 Created/Changed；
  用户选择自行检查并停止 UI 自动化，因此本批没有取得 PrefabImporter 成功行，也没有执行 PlayMode。
- `uncertain`：出生落地、6-3 连续跳台、6-4 中央低台、6-5 窄踏板、6-6 阶梯可达性、FallArea、进门、连续切关及
  Android/iOS 横屏手感均待用户在 Unity 内验收。
- 回滚时只删除 `ILevel_026`～`ILevel_030` 及其 meta；若不再需要自动化流程，可独立删除上述 Skill。模板、配置、
  共享 Actor/Door、运行时代码、事件与存档不需要回滚。

## ILevel_023 陷阱动态跳跃控制（2026-08-12）

### 需求、原始基线与影响范围

- 用户要求为第 23 小关补充 `ILevel023Ctrl`，提供外部 API，将 `IWannerGame_Trap(1)` 的 `Rigidbody2D`
  切换为 Dynamic，并开启该陷阱已有的跳跃事件监听。
- 修改前该陷阱在 `ILevel_023.prefab` 中显式覆盖为 Kinematic（`m_BodyType = 1`），其
  `IWannerGame_JumpListener` 已绑定同一 `Rigidbody2D`，但组件初始关闭；关卡根节点没有局部控制 API。
- 本次新增 `ILevel023Ctrl.cs` 及 meta，并将组件与两个既有引用绑定到 `ILevel_023.prefab` 根节点；不修改
  `IWannerLevelCtrl`、共享 Actor/Trap prefab、事件定义、配置、存档、Analytics 或平台 SDK。

### 修改后行为与调用流

1. 外部运行时逻辑或本关 `TriggerExecutor` 的 UnityEvent 可调用无参 `EnableTrapJump()`；API 使用 `void`，可被
   UnityEvent 直接选择，并可重复调用。
2. API 先把绑定刚体设为 `RigidbodyType2D.Dynamic`，调用 `WakeUp()`，再启用绑定的
   `IWannerGame_JumpListener`；监听器在 PlayMode 的 `OnEnable()` 中订阅既有 `PlayerJumpEvent`，后续跳跃输入为陷阱
   写入配置的 Y 速度。
3. API 只在 PlayMode 生效；编辑态调用会记录 `Bear.Logger` 警告并返回，避免把运行时物理状态写回 prefab。
   刚体或 Listener 引用缺失时同样警告并返回，不修改其它对象。
4. 本控制器不修改质量、重力、约束、当前速度、`simulated`、Actor 的 `IsJumpEnabled` 或其它陷阱；也不自动绑定触发时机。
   具体何时开放陷阱跳跃由外部调用方决定。

### 验证、未覆盖与回滚

- 静态校验确认 `ILevel023Ctrl` 的脚本 GUID 与 meta 一致，根组件引用分别指向
  `IWannerGame_Trap(1)` 的 stripped `Rigidbody2D` 和已添加的 Jump Listener；Prefab 共 71 个本地锚点且无重复 fileID。
- 检查确认目标初始状态仍为 Kinematic、Listener 仍为关闭，只有调用 API 后才发生状态切换；使用 Unity 同版本 Roslyn
  按现有 `Assembly-CSharp.rsp` 加入新脚本编译，返回 `CSC_EXIT=0`，只有工程既有 warning。Unity Editor 随后完成脚本编译，
  生成项目与 `Assembly-CSharp.dll` 均包含 `ILevel023Ctrl`；脚本可用后的最终 PrefabImporter 导入未再出现 Missing Script 或
  `error CS`。
- `uncertain`：尚未在 Unity PlayMode 实际调用 API，陷阱动态落体、连续跳跃、失败/Reset/重建，以及 Android/iOS
  横屏触控表现仍需人工验证；当前 prefab 也未自动新增 UnityEvent 调用方，Inspector 引用显示尚未人工目检。
- 回滚时从 `ILevel_023.prefab` 根节点移除 `ILevel023Ctrl` 组件并删除脚本/meta；陷阱原有 Kinematic 覆盖和关闭的
  Jump Listener 均可保留，共享控制器、事件、Actor、Trap prefab、配置和存档无需回滚。

## IWanner 运行态 OnGUI 调试展示（2026-08-13）

### 需求、原始基线与影响范围

- 用户最终要求 `IWannerGame_GamePlayPanel.OnGUI` 只展示影响 IWanner 插屏触发的数据，并提供 IMGUI 显隐按钮；按钮和数据区
  由 IWanner GameplayPanel 自己绘制，GMPanel 只作为“已经打开”的展示许可，GM 内部 View1/View2 是否展开不影响该 GUI。
- 修改前 HUD 的调试区显示 Path、尝试时长和活动状态，这些值不参与插屏资格判断；GMPanel 也没有向其它调试 IMGUI
  提供无单例查询的打开状态。
- 旁路实测发现 IWanner 比 DrawGame 多了一层 `GameSDKService.IsAdsReady` 前置判断；该属性依赖从未写为 `true` 的
  `GameSDKService.IsInitialized`，导致失败次数已超过阈值时仍在进入 `InterstitialAdHelper` 前固定返回。
- 本次最终修改 `GMPanel.cs`、`IWannerPlayCtrl.cs` 与 `IWannerGame_GamePlayPanel.cs`；不修改 prefab、RemoteConfig 键或值、
  DB 结构、统计/存档写入时机、插屏资格算法、资源、事件、Analytics 或 SDK 接口。

### 修改后行为与数据流

1. `GMPanel.OnOpen()` / `OnClose()` 同步维护只读的 GMPanel 打开状态；IWanner HUD 不通过 `UIManager.Instance` 反查面板，
   `SetView1Visible(...)` 只控制 GM 内部内容，不再影响 IWanner 调试 GUI。
2. GMPanel 已打开时，IWanner HUD 右上角显示“显示/隐藏 IWanner 插屏数据”按钮；按钮和数据仍归
   `IWannerGame_GamePlayPanel.OnGUI` 所有。按钮状态在 HUD 重建间保留，折叠时不刷新快照。
3. 展开后 `IWannerPlayCtrl.Tick()` 以 0.25 秒间隔在正常运行态采集只读快照，HUD 的 `OnGUI` 只消费快照。快照覆盖：
   当前 Placement/请求状态、最近一次 Helper 回调结果、活动控制器、策略、IWanner 广告存档、Bag/免广告门禁、失败计数与阈值、
   两类时间戳/配置 CD/剩余 CD，以及失败位当前资格结果与逐项无法触发原因。
   失败位明确标注来源为手动 `Reset` 和关卡 `Failed`；`Failed` 先记录失败并显示重置区，随后在进入关卡重建时发起请求。
4. IWanner 的失败等待点与 DrawGame 一样直接调用 `InterstitialAdHelper.TryToShowInterstitial(...)` 并等待回调，不再用
   `GameSDKService.IsAdsReady` 或 `IsInterstitialAdReady` 提前判断；策略拒绝和 SDK 未展示都由 Helper 回调 `false` 安全结束等待。
   为避免 Unity Hot Reload 破坏 UniTask 异步状态机，Helper 回调仅向 `UniTaskCompletionSource<bool>` 写入结果，调试文本统一在
   `await` 返回后更新，不在回调闭包内部放置条件编译分支。
5. 普通小关成功和完整 Group 通关不累计胜利插屏计数，也不请求 `InGameVictory`；GUI 不再展示 Group/小关胜利资格。
6. Path、游玩时长、普通关卡统计和其它不参与插屏判断的数据不再展示。`OnGUI` 没有 DB、RemoteConfig、Manager/Instance、
   Save 或实例创建调用；采集和绘制代码只在 `UNITY_EDITOR || DEBUG_MODE` 下编译，正式非调试构建无运行开销。

### 验证、未覆盖与回滚

- `git diff --check` 对三份代码文件通过；定向扫描确认 HUD `OnGUI` 没有 DB、RemoteConfig、Manager/Instance、Save 或
  Instantiate 路径，并确认调试代码的预处理块配对。
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo` 通过，结果为
  `0 error / 101 warning`；warning 属于工程既有范围。
- `uncertain`：尚未在 Unity PlayMode 实测 GMPanel 开关、GM 内部 View 显隐不影响 HUD、按钮切换、0.25 秒刷新、失败/胜利/冷却变化和实际插屏结果，也未验证
  不同分辨率、Android/iOS Development Build 或与其它 IMGUI 调试区的遮挡。
- 回滚调试展示时删除 GMPanel 打开状态、控制器调试快照区域和 HUD 调试区域即可；若单独回滚调用对齐，则恢复 IWanner 的
  `IsAdsReady` 提前返回，但会重新阻断当前 Editor 模拟插屏。无需迁移存档、修改 prefab 或配置。

## IWanner Spring 场景配色补充（2026-08-13）

### 需求、基线与影响范围

- 用户要求参考 `IWannerLevelCtrl` 已有 Trap 改色逻辑，把 Spring 视觉也纳入当前小关配色。
- 修改前 `ApplySceneColorSettings()` 只处理名称以 `Ground` 开头的 `SpriteRenderer` 和符合现有
  `OnTriggerTrapHandle` 层级判定的 Trap 视觉；`IWannerGame_Spring.prefab` 的两个视觉子节名为
  `Square` / `Square (1)`，不会命中 `Ground*` 规则。
- 本次只修改 `IWannerLevelCtrl.cs`、`IWannerLevelColorSettings.cs` 与本地记录；不修改 Spring、Trap、
  Ground 或已验收关卡 prefab，不新增配色字段、事件、资源加载、存档、打点或平台链路。

### 修改后行为与数据流

- 小关运行时初始化扫描到某个 `SpriteRenderer` 时，若其父层级存在
  `IWannerGame_SpingTriggerCtrl`，就把该 Renderer 颜色写为当前 `IWannerLevelColorSettings.GroundColor`。
- 识别边界使用 Spring 功能组件，不依赖 `Square` 子节名称或固定层级深度；当前静态证据中
  `ILevel_031.prefab` 已在 Spring 实例根上绑定该组件。
- `IWannerLevelColorSettings.groundColor` 与 `GroundColor` 属性的旧注释因新行为后只写 Ground 已不完整，
  因此最小更正为 Ground、Trap 与 Spring 共享场景颜色；字段名和序列化数据未变。
- Trap 分支保持现行代码不变。旁路核对发现 2026-08-11 旧记录中“不依赖 Trap GameObject 名称”
  的描述与当前源码不一致：现行 Trap 逻辑在组件判断外仍检查直接父节点名称；本需求未扩大为 Trap 修复。

### 验证、未覆盖与回滚

- 目标代码 `git diff --check` 通过，只输出工作区 LF/CRLF 提示；静态扫描未新增 `OnValidate`、
  `Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB/Save、实例化或全局事件链。
- `dotnet build Assembly-CSharp.csproj` 通过：`0 error / 101 warning`。
- `uncertain`：尚未在 Unity PlayMode 目测 `ILevel_031` 的两个 Spring Renderer 与 Ground/Trap 同色，
  也未验证 Reset/切关、后续动态生成 Spring 和 Android/iOS 横屏显示。
- 单独回滚本增量时，移除 `IWannerLevelCtrl` 的 `IWannerGame_SpingTriggerCtrl` 父层级判断，并还原
  `IWannerLevelColorSettings` 两处说明即可；配色资产、prefab 和存档无需迁移。

## IWanner 小关正式打点接入（2026-08-13）

### 需求、基线与影响范围

- 用户要求 IWanner 参考主线与 DrawGame 的现行关卡打点，在小关维度保持相同的
  `level_start`、`level_end`、`level_end_success_N`、`b_play`、`start_type`、`play_count`、
  revive 和累计时长口径。
- 原 `IWannerLevelRuntimeStatistics` 已统一维护以小关 Path 为 Key 的游玩次数、成功次数、重试次数和累计时长，
  但没有向 `GameSDKService` 发送正式关卡事件；跨玩法累计值也只包含主线与 DrawGame。
- 原权威约定的跨玩法公式与本需求冲突。用户已明确确认把公式扩展为“主线 + DrawGame + IWannerGame”，
  因此先更新 `DevelopMD/关卡打点生命周期约定.md`，再同步运行时实现。
- 修改范围为 `IWannerLevelRuntimeStatistics.cs`、`IWannerPlayCtrl.cs`、
  `GameSDKService.Game.cs`、`GameSDKService.UserProperty.cs` 和相关本地文档；不修改 prefab、Luban、
  RemoteConfig、广告策略、Group 存档字段或已验收主线/Draw 关卡控制器。

### 修改后行为与数据流

1. 可玩小关完成资源与运行时初始化、即将开放输入时，`IWannerPlayCtrl` 把实际
   `IWannerLevelData.Id` 与 Path 交给 `IWannerLevelRuntimeStatistics.BeginAttempt(...)`。统计对象先增加并保存
   `LevelPlayCount[Path]`，再发送 `level_start`。
2. `level_start` 使用 `item_category=iwanner`、`level_name=Path`、当前小关 `play_count`；`start_type`
   只由该 Path 的历史 `LevelSuccessCount` 判断，已经成功过为 `replay`，否则为 `play`，不依赖进入按钮或 Group 通关状态。
3. 死亡或手动 Reset 各增加一次当前小关运行态 revive 和历史 `LevelReviveCount[Path]`，先保存本次时长，
   再发送 `level_end(result=fail)`。死亡后的 Reset 点击只负责重建，不重复增加 revive；新实例开放输入前发送新的
   `level_start`，运行态 revive 保留到成功或退出。
4. 正常成功和激励 Skip 成功共用一次结算：先增加并保存 `LevelSuccessCount[Path]` 与累计时长，再发送
   `level_end(result=success)`，随后更新 `b_play` 并发送 `level_end_success_N`。事件 `level` 使用实际
   `IWannerLevelData.Id`；整个 Group 完成只进入 VictoryPanel，不重复发送小关成功事件。
5. 活动小关退出玩法或被其它正常关卡切换时发送 `level_end(result=exit)`；成功或退出会清理当前小关 revive 上下文。
   GM 上一关重载继续沿用无统计的 `DiscardAttempt()`，不会制造正式关卡事件。
6. `level_start.level`、`level_end.level`、`level_end_success_N` 的 `N` 与 `b_play` 统一读取主线、DrawGame、
   IWannerGame 三者历史成功次数之和。同一 attempt 的开始与失败/退出结束保持同一序号；成功结束使用已写入本次成功后的累计值。
7. `ILevel_999` TBN 可发送开始、失败、Reset 和退出事件，但没有实际 `IWannerLevelData.Id`；产品要求其不可通关。
   若异常产生成功结果，普通 `level_end(success)` 仍安全完成，但派生成功事件会跳过并记录告警，不伪造 ID 或 GroupId。
8. IWanner `level_start` 同步刷新 SDK 的最近关卡 Path，因此之后的虚拟货币事件会与主线、DrawGame 一样使用最近进入的关卡上下文。

### 验证、未覆盖与回滚

- 静态检查确认开始入口只有一处；Reset、死亡、成功、Skip 和退出继续收敛到同一个幂等 `CompleteAttempt(...)`，
  Group 完成没有额外成功结算调用。主线与 DrawGame 的事件调用顺序未改，只扩展了共享累计公式。
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false
  /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 `0 error / 101 warning`；
  warning 属于工程既有范围。
- `uncertain`：尚未在 Unity PlayMode、真实 SDK 后台与 Android/iOS 验证首次进入、连续死亡/Reset、Skip、
  正常成功、Group Victory/Replay、退出、TBN 以及主线/Draw/IWanner 相互切换后的事件顺序和字段值。
- 回滚时移除统计对象对两个 IWanner SDK API 的调用与 `IWannerPlayCtrl` 传入的小关 Id，删除
  `GameSDKService` 的两个 IWanner 关卡事件方法，并从 `GetCombinedLevelPassedCount()` 移除 IWanner 成功次数；
  已有 IWanner 四项历史统计保留，无需存档迁移。若回滚累计公式，必须同步恢复权威生命周期约定。

## IWanner Group 8 的 ILevel036～040 静态地图拼接（2026-08-14）

### 需求、基线与影响范围

- 用户要求按 `8-1.png`～`8-5.png` 拼接 `ILevel_036`～`ILevel_040`，并明确允许覆盖未纳入 Git 的旧 `ILevel_036.prefab`。
- 修改前 Group 配置只到 7、普通小关映射只到 `30035 -> ILevel_035`；`ILevel_037`～`ILevel_040` 不存在。
- 本批只读复用最近完成的 `ILevel_035.prefab`，不修改 `ILevel_031`～`ILevel_035` 或 IWanner 运行时代码。

### 修改后行为与资源流

1. `LevelSort.xlsx/iwannerLevelSort` 与 `tbiwannerlevelsort.json` 新增 Group 8，顺序引用 `30036`～`30040`；
   `LevelData.xlsx/iwanner_level` 与 `tbiwannerleveldata.json` 增加 `ILevel_036`～`ILevel_040` Path。
2. 五关保留模板的 Actor、Door、Camera、Feedback、FallArea、颜色和成功回调，只重建 `Scene` 下的 Ground；
   可见 Ground 分别为 `5/6/12/1/4`，加三个固定边界后的总数为 `8/9/15/4/7`。
3. 五张参考图节点保存于对应 prefab 并保持失活。Actor 与 Door 按 100 PPU 像素测量和模板脚底/门根偏移定位。
4. 参考图里的方框机关、尖刺、弹簧、传送等机制未实例化；旧 `ILevel_036` 中的 Trigger、Trap、Spring
   已按用户授权被静态规格覆盖，但原 `.meta` GUID 保留。

### 验证、未覆盖与回滚

- Luban 源表经导入、定向检查、公式错误扫描与前后渲染，新增行样式和 Group/ID/Path 顺序正确；两个运行时 JSON 与源表一致。
- 生成器 dry-run 与最终生成输出一致，五关 `--validate-only` 均为 `ok: true`；Ground 几何、参考图 GUID、
  Actor/Door、Scene 子节点、LevelCtrl 绑定、本地 fileID 均通过。
- Luban CLI 临时生成的两个 IWanner JSON 与仓库运行时 JSON 结构化比对一致；Unity 日志已记录两个 JSON 与
  `ILevel_036`～`ILevel_040` 全部成功导入。Scene 叠图、PlayMode、机关闭环、YooAsset 和 Android/iOS 横屏仍为 `uncertain`。
- 回滚时同步删除 Group 8 映射和新增 prefab；旧 `ILevel_036` 未纳入 Git，无法从 Git 恢复。完整记录见
  `Assets/Game/Prefabs/WannerLevel/ILevel036-040地图拼接功能记录.md` 与同目录风险评估。

## IWanner HUD 左右移动改用 Enter/Exit（2026-08-19）

### 需求、基线与影响范围

- 用户反馈左右移动操作不流畅，要求控制只由 `Enter/Exit` 触发，不再使用按下/抬起事件。
- 修改前面板同时订阅 `OnClickDown/OnClickUp` 与 `OnClickEnter/OnClickExit`，但 prefab 左右按钮的
  `isListenEnter/isListenExit` 均为 false，因此有效控制实际仍来自 Down/Up。
- 本次仅修改 IWanner HUD 脚本和对应 GameplayPanel prefab，不修改共享 `CustomButton`、`GamePlayPanel`、
  `IWannerLevelCtrl`、Actor、配置、存档、资源加载、打点或平台 SDK。

### 修改后行为与事件流

1. 左右按钮只订阅 `OnClickEnter/OnClickExit`；prefab 中对应两个 `CustomButton` 实例同时开启 Enter 与 Exit 监听。
2. Enter 当前方向后，HUD 在每帧继续派发既有左/右移动事件；进入另一方向时直接清除旧方向，不插入取消事件。
3. Exit 仅结束自己仍持有的方向；旧区域的延后 Exit 不会取消已进入的新方向，左右都未激活时才派发移动取消。
4. 输入关闭、暂停、Reset、Replay 和面板销毁仍通过 `ResetMoveInput` 清空方向并按既有边界派发取消；跳跃继续使用
   `OnClickDown`，不在本次左右控制范围内。

### 验证、未覆盖与回滚

- CodeGraph 确认 `CustomButton.OnPointerEnter/OnPointerExit` 分别受 `isListenEnter/isListenExit` 门禁并派发
  `OnClickEnter/OnClickExit`；静态 diff 确认左右移动已无 Down/Up 订阅，且仅两个目标按钮的门禁改为 true。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo` 已执行，但被未修改的
  `IWannerPlayCtrl.cs:553` 中 `I2.Loc.ResourceManager` 与 `Bear.ResourceSystem.ResourceManager` 歧义阻断，结果为
  `1 error / 1124 warnings`；本次目标文件没有出现在错误列表中，但完整编译仍记为未通过。
- `uncertain`：Unity PlayMode 中的进入、移出、左右连续滑动、触点释放、暂停/恢复、Reset，以及 Android/iOS 横屏
  触控尚未验证；桌面鼠标会按悬停 Enter/Exit 语义控制，也需在 Editor 冒烟时确认是否只作为调试差异接受。
- 回滚时恢复左右按钮的 Down/Up 订阅及原方向字段，并把目标 prefab 两个 Enter/Exit 门禁恢复为 false；事件、配置、
  存档与关卡资源无需回滚。详细模块记录见
  `Assets/Game/UI/IWannerGame/GamePlayPanel/IWannerGame_GamePlayPanel左右移动EnterExit功能与风险记录.md`。

## IWanner 系统返回键补齐退出应用确认（2026-08-19）

### 需求、原有基线与影响范围

- 需求：IWannerGame 模式收到 Unity Editor Escape 或 Android Back 时，参考 DrawGame 打开 Exit App 确认弹窗。
- 修改前 `IWannerPlayCtrl.Tick()` 把返回键转发到 `OpenPausePopup()`，因此打开的是 `GameSettingPopup`，没有进入主线/Draw
  已有的默认 `CommmonPopup` 退出确认链路。
- 本次运行时仅修改 `Assets/Game/Scripts/Play/IWanner/IWannerPlayCtrl.cs`；不修改 HUD 暂停按钮、
  `GameSettingPopup`、`CommmonPopup`、UI prefab、资源、配置、存档、打点、广告策略或平台 SDK。

### 修改后行为与 UI 流

1. 活动 IWanner 控制器在 `Tick()` 收到 `KeyCode.Escape` 后调用本地 `ShowExitPopup()`，使用
   `U_Quit_Title_01` / `U_Quit_Des_01` 本地化文案创建默认 `CommmonPopup`。
2. 玩家确认后调用 `Application.Quit()`；Unity Editor 同时退出 PlayMode。取消沿用 `CommmonPopup` 既有关闭逻辑。
3. 已打开默认退出弹窗、Guide Flow 正在执行或排队、关卡加载、失败后等待 Reset、Skip 激励广告请求或 Group 已结算时，
   返回键不会再创建退出弹窗。
4. 暂停状态仍可读取返回键，与 DrawGame 一致；原暂停按钮继续通过
   `IWannerGame_GamePlayPanel.OnClickPause()` 打开 `GameSettingPopup`，两条入口职责分离。
5. 该链路不切换玩法、不销毁关卡，也不主动发送 `level_end(exit)`；真正应用退出后的 SDK 收尾能力不在本次改动范围内。

### 验证、未覆盖与回滚

- 静态检查确认 IWanner 仅保留一个 `KeyCode.Escape` 入口，调用链为
  `PlayControllerManager.Tick -> IWannerPlayCtrl.Tick -> ShowExitPopup -> CommmonPopup.Create`，并覆盖重复弹窗、引导和四个
  IWanner 过渡/结算状态门禁。
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo -v:minimal
  /p:UseSharedCompilation=false /nr:false /clp:ErrorsOnly` 通过：`0 error / 106 warning`；warning 为工程既有范围。
- `uncertain`：尚未在 Unity PlayMode 验证运行中、暂停中、失败等待、加载、Skip 广告请求、Group Victory 与 Guide Flow
  各阶段的 Escape 行为；尚未在 Android 横屏真机验证系统返回键和取消后的输入恢复。iOS 没有新增返回键平台分支，也未回归。
- 回滚时把 `Tick()` 的 Escape 分支恢复为 `OpenPausePopup()`，删除本地 `Exit App` 区域即可；无需迁移资源、配置或存档。

## IWanner 首个 Group 默认解锁（2026-08-21）

### 需求、修改前基线与影响范围

- 需求：IWanner 选组列表的第一个 Group 默认可进入，不要求玩家先观看解锁广告。
- 修改前 `Assets/Game/Configs/tbiwannerlevelsort.json` 的 Group 1 与后续普通 Group 一样配置为
  `lockType=1 (LevelLockType.Ad)`；新存档的 `UnlockGroups` 为空，因此 Item 显示 Lock，真实入口也会拒绝进入。
- 本次只修改运行时加载的 `tbiwannerlevelsort.json` 第一行配置；不修改 UI、玩法控制器、DB/Save、广告回调、
  Analytics、关卡 prefab、资源地址或平台分支。

### 修改后行为与数据流

1. Group 1 配置改为 `lockType=0 (LevelLockType.Unlock)`；Group 2～11 继续保持 `Ad` 锁定。
2. `IWannerGame_ChoiceLevelItem.ResolveState()` 通过既有 `LockType.Unlock` 分支把 Group 1 显示为普通可进入状态。
3. `IWannerPlayCtrl.CanEnterGroup()` 在资源加载前使用相同配置二次放行，因此 UI 显示与权威入口一致。
4. 默认解锁不写入 `DB.IWannerGame.UnlockGroups`，不请求广告，也不改变 `CurrentGroupID`、`CurrentLevel`、
   `MaxGroupId` 或 `PassedLevels`；玩家实际进入后仍走既有进度保存流程。

### 验证、剩余风险与回滚

- 静态解析确认排序配置共 11 个唯一 Group：首项 `id=1/lockType=0`，其余 `id=2～11/lockType=1`；所有
  Path 仍能在 `tbiwannerleveldata.json` 中唯一解析。
- 定向静态检查确认 Item 与 `IWannerPlayCtrl` 均复用 `LevelLockType.Unlock`，没有新增第一组 ID 特判或存档写入。
- 外部 `dotnet build Assembly-CSharp.csproj` 已尝试：首次因缺少 `project.assets.json` 停止；允许 restore 后仍因当前
  Unity `Temp/bin/Debug` 缺少 Apple、Bear、GF、Unity Package 等 200 个依赖 DLL 而失败。该结果属于工程临时编译产物
  不完整，不能作为本次配置的通过或失败证据；需由 Unity 重新生成依赖后补验。
- `uncertain`：Unity PlayMode 清档打开选组页、点击 Group 1、Group 2 广告锁定、Android/iOS 横屏触控尚未验证。
- 当前会话提供的表格运行时缺少必需的 `@oai/artifact-tool`，因此未修改/验证 Luban Excel 源表；后续重新生成
  配置可能覆盖本次 JSON。补验条件是修复表格运行时后，将首 Group 源行同步为 `Unlock` 并重新生成比对。
- 回滚只需把 `tbiwannerlevelsort.json` 首项 `lockType` 恢复为 `1`；无存档数据需要迁移。

## IWanner 主界面入口展示 Group 进度（2026-08-25）

### 需求、基线与影响范围

- 需求：`WannerGameEnter.prefab` 的 `ui_zhuye_05_txt` 展示当前已通过 Group 数，
  `ui_zhuye_06_txt` 展示 `/{当前配置 Group 总数}`。
- 修改前 `IWannerGameEnter` 只负责 IsOpen 显隐、按钮绑定与打开选组 Popup；两个文本保留 prefab 占位值，
  `StartPanel` 复用打开时也不会主动刷新入口进度。
- 本次只修改 `IWannerGameEnter.cs`、`WannerGameEnter.prefab` 和 `StartPanel.cs`；未修改 Group 配置、
  IWanner 存档结构、选组/进关权限、资源、事件、广告、打点或平台 SDK。

### 修改后行为与数据流

1. `IWannerGameEnter.RefreshEntryState()` 在入口开关开启后读取 `GameManager.IWannerGameLevel.LevelGroups`，
   去重并统计正数 Group Id，写入 `ui_zhuye_06_txt` 的 `/{总数}`。
2. `ui_zhuye_05_txt` 只统计这些当前配置 Group Id 中同时存在于 `DB.IWannerGame.PassedLevels` 的数量；
   旧配置残留或重复记录不计入显示，读取过程不调用 `EnsureValidCollections()`，也不保存存档。
3. 配置控制器未就绪时显示 `0` 与 `/0` 并记录 Bear Logger 警告；现有入口显隐和按钮去重绑定顺序保持不变。
4. `StartPanel.OnOpen()` 包含 inactive 子节点查找并缓存 `IWannerGameEnter`，每次复用打开调用其既有 `Refresh()`，
   因此从 IWanner 返回首页后可读取最新 Group 通关进度。

### 验证、未覆盖与回滚

- 当前 `tbiwannerlevelsort.json` 静态解析得到 14 个唯一有效 Group；两个 TMP fileID 与根节点脚本序列化字段绑定检查通过。
- 完整 `Assembly-CSharp.csproj` 外部构建通过：`0 error / 118 warning`；CodeGraph 确认
  `StartPanel.OnOpen() -> IWannerGameEnter.Refresh() -> RefreshGroupProgress()` 调用关系。
- `uncertain`：尚未在 Unity PlayMode 验证清档 `0/14`、部分通过、全部通过、返回首页复用打开、IsOpen 切换，
  以及 Android/iOS 横屏文本布局。
- 回滚时删除 `IWannerGameEnter` 的两个 TMP 字段及 Group 统计方法，移除 prefab 两个字段绑定，
  并删除 `StartPanel` 中对应缓存与刷新调用；Group 配置与存档无需迁移。

## 失败等待时按空格重置（2026-08-31）

### 需求、基线与范围

- 用户补充：IWanner 失败后，按空格也能重置当前小关。
- 修改前：失败时控制器关闭常规 HUD 输入，显示 `ResetClickArea`；HUD 的 `Update()` 在输入关闭时直接返回，没有键盘失败重置入口。
- 只修改 `Assets/Game/UI/IWannerGame/GamePlayPanel/Scripts/IWannerGame_GamePlayPanel.cs` 的 `Update()`；不改共享 GamePlayPanel、Actor、玩法控制器、关卡 prefab、配置、存档或 SDK。

### 修改后行为与事件流

- `Update()` 先检查 `Application.isPlaying`，再在 `_resetClickAreaVisible` 为 true 时检测 `Input.GetKeyDown(KeyCode.Space)`，该分支位于常规 HUD 输入门禁之前。
- 空格调用既有 `OnClickFailureReset(resetClickArea)`，与点击全屏区域共用 `IWannerGameResetEvent(GameResetType.Failed)` → `IWannerPlayCtrl.TryResetCurrentLevel()` 链路。
- 控制器接受后同步隐藏重置区并标记正在重建，仍按当前 Group 当前小关执行既有黑幕、插屏与重建流程；死亡已累计的失败次数不会因这次重置再次增加。
- 按住空格不重复触发；正常游玩、正常暂停、成功及加载时没有失败重置区，不触发该新增分支。原全屏点击、移动与跳跃入口保留，不新增全局事件或每帧分配。

### 验证、缺口与回滚

- 静态检查确认失败分支先于 `_inputEnabled` 返回，且使用既有失败回调与控制器同步去重门禁。
- 外部构建命令：`dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false -v:quiet --nologo`；结果为 91 个 `CS0006` 错误，原因是 `Temp/bin/Debug` 缺少依赖 DLL，不能据此宣称编译通过。日志：`%TEMP%/brainpunk-iwanner-space-reset-build.log`。
- 全局 `git diff --check` 发现任务前已有 `en SDF.asset` 的尾随空白，未修改该资源；本次四个文件的定向检查已通过。
- `uncertain`：未运行 Unity PlayMode 或 Android/iOS 横屏验证。验收需覆盖失败后按空格重置当前小关、按住不重复、空格与鼠标同时操作只重建一次、失败计数不重复，以及正常操作/暂停/成功/加载时不误重置。按键与死亡同帧、广告覆盖和移动端原触控路径仍待实测。
- 回滚只移除本次 `Update()` 新增的运行态检查与空格分支；原点击重置、控制器、资源与存档不需恢复或迁移。

## 2026-09-07：动态配置成功门 API

- 需求：为第 47 关左右分支提供动态配置成功门触发器和进入动画的 API，左侧选择对应右门，右侧选择对应左门。
- 基线：successDoorTrigger / enterDoorAnimCtrl 原为私有序列化字段；运行时初始化只绑定当时的门，直接换字段无法解除旧门的 Success 回调。
- 当前新增 `IWannerLevelCtrl.SetSuccessDoorTrigger(OnTrigger2DHandle)` 与 `SetEnterDoorAnimCtrl(IWannerGame_EnterDoorAnimCtrl)`，均仅 PlayMode、未结算、非空参数时接受设置。未调用 API 的关卡维持既有路径。
- 触发器 API 先解除旧门运行时监听，关闭旧门中目标为当前控制器且方法为 Success 的持久化回调，再切换字段、启用新门同类持久化回调；运行时就绪则沿用 BindDoorTriggerAtRuntime，没有持久化绑定时添加运行时监听。其它事件回调不动，不写回 Prefab。
- 动画 API 仅切换引用，不播放或重置；后续角色门前定位及 PlayEnterDoorAnimationAsync 使用该引用。结算后拒绝切门，避免成功定位与播放门不一致。
- 第 47 关 Inspector 配置：On Trigger Left 依次绑定右门动画的 SetEnterDoorAnimCtrl、右门触发器的 SetSuccessDoorTrigger、原 Executor；On Trigger Right 对称绑定左门。两个 API 的事件目标均为当前关卡 IWannerLevelCtrl，参数分别拖入对应门组件。
- 本次修改 IWannerLevelCtrl.cs 及相关中文记录，未修改 Prefab、ILevel047Ctrl、共享门动画、配置、资源、存档、UI 或打点。两个 API 通过现有 UnityEvent 组合，不新增单关门引用字段或全局事件。
- 验证：`dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -v:q` 通过，0 错误、121 警告。静态核对旧门解绑、新门绑定、重复设置、结算门禁与未调用路径；PlayMode、Android/iOS 横屏实际表现为 uncertain。
- 回滚：删除两个 API 及 SetDoorSuccessListenerState，并移除新接入的 UnityEvent 调用；还原对应文档段落，保留其它工作区改动。

## 2026-09-22：OneLink 指定组内小关入口

- `GameManager.TryEnterIWannerGroup` 和 `IWannerPlayCtrl.TryEnterGroup` 增加可选的零起始 `levelIndex`；默认省略时保留原同组续关、跨组从 0 开始的规则。OneLink 从 `IWannerLevelData.Path` 反查组和索引，只解锁该组后传入明确索引。
- 明确指定 `ILevel_999` 时，通过可选 `allowLockedEntry` 放行本次 TBN 进入，不写最后一组通关；普通选组 TBN 的原检查保留。未命中路径时的 TBN 回退不传放行标志。
- 原保存、加载、统计、胜利与失败恢复流程继续执行；跳到组内后段后完成末关，仍可能依原流程记整组完成。OneLink 不创建地图弹窗，也不改 LastMapIndex。
- 两层入口编译和提取原方法的模拟验证通过；实际移动端加载与统计为 `uncertain`。详情及回滚见 `Assets/Game/Scripts/SDK/Services/Core/OneLink功能与测试记录.md`。

## 2026-10-09：OneLink Group／小关 sub2 协议分离

- Group 入口改用 `IGroup_{IWannerLevelSort.Id}`；同组使用既有存档索引续关，切换到其它 Group 时由 `ResolveEntryLevelIndex` 回到索引 0。小关入口改用配置中的精确资源 Path `ILevel_XXX`，并把命中的真实组内索引传给 `TryEnterIWannerGroup`，不再统一进入所属组首关。
- 裸数字、配置 level ID、错误大小写和未命中目标不解释为 Group 或小关，仍进入既有最低未通关回退；`ILevel_999` 继续使用 TBN 专用入口。未新增存档字段、事件、UI、资源或平台 SDK 写入。
- 隔离路由 harness 在五组平台宏下各通过 647 项断言，Editor/Android 定向编译 0 error；Unity PlayMode、Google Play 安装、Facebook 延迟回调、真实资源加载和移动端画面仍为 `uncertain`。完整证据和回滚见 `Assets/Game/Scripts/SDK/Services/Core/OneLink功能与测试记录.md` 顶部同日章节。
