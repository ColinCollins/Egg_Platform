# Level84 足球选择流程功能记录

## 需求背景

Level84 原本只有足球选择流程相关的序列化引用和注释，运行时没有实际流程。2026-06-18 需求澄清：两个射门选择只是表现不同，任何一个选择都不能触发成功；两个选择都必须被敌人阻挡，然后进入已有失败重置流程。后续澄清补充：配置好的敌人阻挡表现是 Level84 验收条件，缺失敌人的 fallback 只能作为防崩溃保护，不能视为验收通过。

## 影响文件和系统

- `Assets/Game/Scripts/Level/LevelItem/Level_84/Level84Ctrl.cs`：Level84 本地足球进入、选择、射门表现、敌人阻挡和失败事件派发。
- `Assets/Game/Prefabs/Level/Level_084.prefab`：把本地 `Enemy` 角色实例绑定到 `Level84Ctrl.enemy`，并保留已有的 UI、足球、触发区引用。
- 使用的既有系统：`BaseLevelCtrl.SetCtrlable(...)`、`GameResetEvent(GameResetType.Failed)`、`CustomButton.OnClick`、`TriggerAreaHandle.GetBoundaryRect()`、DOTween、UniTask。

## 原行为基线

- `Level84Ctrl.cs` 只有序列化字段和注释，`Start()` / `Update()` 为空。
- `Level_084.prefab` 已存在 `EnterTrigger` 和 `SuccessArea`，二者都使用 `TriggerAreaHandle`。`Level84Ctrl` 组件已有足球、选择面板和两个按钮引用。早期 prefab 文本证据显示 `enemy` 未绑定；当前 prefab 文本中 `enemy` 已绑定到配置好的 `Enemy` 角色实例。

## 新行为

- Actor 进入 Level84 的 `EnterTrigger` 后，只启动一次受保护的足球流程。
- 足球重置到初始位置后做下落/弹跳表现，随后显示本地选择面板。
- 足球选择和射门处理期间锁住 actor 移动输入。
- 直射按钮会隐藏面板，播放 actor 射门表现，足球走直射表现，然后进入敌人阻挡和失败重置。
- 高射按钮会隐藏面板，播放 actor 射门表现，足球走高射表现，然后进入敌人阻挡和失败重置。
- 最终验收要求配置好的敌人必须可见地阻挡两个选择。prefab 文本已绑定敌人引用，但仍需要 PlayMode 确认敌人确实出现并阻挡两个射门。缺失敌人的 fallback 只用于避免崩溃，不满足视觉验收要求。
- 两个射门选择都不会调用 `BaseLevelCtrl.SuccessStrightly()` 或其他成功路径。
- Disable / Destroy 路径会停止本地 tween、解绑按钮监听，并重置本地表现状态。
- Actor 和敌人表现通过 `Level84Ctrl` 本地 helper 使用既有 `ActorCtrl.SpineCtrl`，没有修改共享 `ActorCtrl` API，也没有替换 actor prefab 脚本类型。

## 数据、资源、UI、存档、事件和平台流

- 数据 / 配置 / 存档：未新增 DB、PlayerPrefs、Luban、RemoteConfig、资源包或存档结构行为。
- UI：复用 Level84 本地 `Canvas`、`highBtn`、`strightBtn` 引用。
- 资源：未新增运行时资源加载。
- 事件：两个选择都通过 `GameResetEvent(GameResetType.Failed)` 进入失败重置。Level84 足球选择流程不会触发成功事件。
- 平台：仅为 Unity 运行时流程；未修改 Android/iOS SDK 或构建设置。

## 验证记录

- 已静态检查 Level84 本地脚本和既有 `BaseLevelCtrl` 失败流程。
- prefab 文本检查确认 `EnterTrigger`、`SuccessArea`、`football`、`Canvas`、`highBtn`、`strightBtn`、`actor`、`enemy` 引用存在于 `Level_084.prefab`。`SuccessArea` 和 `EnterTrigger` 的 UnityEvent 调用列表在 YAML 证据中为空。
- 静态扫描 `Level84Ctrl.cs` 未发现 `Instantiate`、运行时资源加载、DB/save、RemoteConfig、PlayerPrefs、`OnValidate`、`Reset`、`[ExecuteAlways]`、`[ExecuteInEditMode]`。
- 在当前 worktree 中执行 `dotnet build Assembly-CSharp.csproj --no-restore` 失败，因为没有生成的 `.csproj` / `.sln` 文件；需要先由 Unity 生成项目文件，或使用已有生成项目文件后再运行。
- 已尝试使用 Unity 6000.0.59f2 进行 batchmode 编译，但项目未能达到干净编译：`Assets/Guru/Gen/GuruAllSpecs.G.cs` 和 `Assets/BearGame/com.bear.ui@919f05cf0418/Runtime/Animation/UIScaleAnimation.cs` 仍有无关依赖错误（缺少 `Guru` / spec 类型，以及 `DG.Tweening` / `Tween` 命名空间或类型）。提取到的编译错误行未包含 Level84 文件路径。
- 当前环境未执行完整 Unity PlayMode 验证。

## 剩余风险和未验证项

- `football_shoot_ready`、`football_shoot`、`football_save` 这三个 Spine 动画名需要在 Unity PlayMode 中结合 Level84 actor/enemy 资源确认。缺失动画时脚本会降级为短等待并输出 warning。
- `enemy` 已在 prefab 文本中绑定，但视觉阻挡仍需 PlayMode 验证配置好的 Enemy 实例和动画可用性。
- 移动端横屏触控、重复重开/退出流程仍需设备或 Editor PlayMode 验证。

## 回滚路径

如果 Level84 足球流程不被接受，回滚 `Level84Ctrl.cs`、`Level_084.prefab` 中 Level84 本地绑定/表现改动，并删除本功能记录。

## 2026-06-18 v2.0 补充记录

### 需求背景

本次继续处理 `specs/023-level84-v2-logic`：在不改变两个射门选择失败结果的前提下，补充 Level84 v2.0 表现和可维护性要求。用户额外要求：额外留存文档必须使用中文；实现脚本中的既有注释不得随意改写；生成或新增脚本 API 时需要中文注释和 `#region` 分区。

### 本次影响文件

- `Assets/Game/Scripts/Level/LevelItem/Level_84/Level84Ctrl.cs`
- `specs/023-level84-v2-logic/quickstart.md`
- 本功能记录和同目录风险评估
- `DevelopMD/已有功能设计与使用记录.md`

本次没有继续修改 `Level_084.prefab`，因为 prefab 文本已显示 `Level84Ctrl` 绑定了 `Canvas`、`highBtn`、`strightBtn`、`actor`、`enemy` 和 `football`。新增的 `enemyBlockVisibleDuration` 使用脚本默认值即可，避免无必要 prefab 序列化 churn。

### 行为补充

- `Level84Ctrl` 新增 `enemyBlockVisibleDuration` 序列化字段，enemy 完成进入和扑救表现后会额外保持短暂可见，再进入失败等待和失败重置，目的是让两个选择的配置 enemy 阻挡更容易被玩家确认。
- `Level84Ctrl` 新增中文 `Tooltip`、`Enter()` 中文 XML 注释，以及 `#region` / `#endregion` 分区；原有类注释和 Unity 默认 `Start` / `Update` 注释文本未被改写。
- `quickstart.md` 补充 3 秒触发记录、enemy 阻挡可见判定，以及移动端 fps/卡顿记录方式。

### 数据、资源、UI、存档、事件和平台流

- 数据 / 配置 / 存档：未新增 DB、PlayerPrefs、Luban、RemoteConfig 或存档结构行为。
- UI：仍复用 Level84 本地选择面板和两个按钮引用。
- 资源：未新增运行时资源加载。
- 事件：两个选择仍只通过 `GameResetEvent(GameResetType.Failed)` 进入失败重置，未新增 success、通关、解锁或成功存档路径。
- 平台：未修改 Android/iOS SDK、ProjectSettings 或构建设置。

### 验证记录

- 静态扫描 `Level84Ctrl.cs`：未发现 `SuccessStrightly`、`GameResetType.Success`、通关、解锁、成功存档调用。
- 静态扫描 `Level84Ctrl.cs`：未发现 DB、Save、RemoteConfig、PlayerPrefs、Resources、StreamingAssets、YooAsset、Instantiate、`OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode` 新增链路。
- `#region` / `#endregion` 数量检查结果为 12 / 12。
- `git diff --check` 针对 `Level84Ctrl.cs` 和 `quickstart.md` 未发现 whitespace error。
- Unity 6000.0.59f2 batchmode 已尝试执行，日志路径为 `BrainPunk/Logs/Level84V2_Batchmode.log`。编译仍被无关依赖错误阻断，主要是 `Assets/Guru/Gen/GuruAllSpecs.G.cs` 缺少 Guru/spec 类型，以及 `Assets/BearGame/com.bear.ui@919f05cf0418/Runtime/Animation/UIScaleAnimation.cs` 缺少 `DG.Tweening` / `Tween`。日志未显示 `Level84Ctrl.cs` 编译错误。
- 当前 worktree 没有生成的 `.csproj` 文件，无法执行 `dotnet build BrainPunk/Assembly-CSharp.csproj --no-restore`。

### 剩余风险和未验证项

- PlayMode 未执行，仍需在 Unity 中验证两个射门选择都清晰显示配置好的 enemy 阻挡、都失败且都无 success。
- `football_shoot_ready`、`football_shoot`、`football_save` 动画名仍需结合 Level84 actor/enemy 资源验证。
- Android/iOS 横屏触控、fps/可见卡顿和重复重开/退出流程仍需设备或 Editor PlayMode 补验。

### 回滚路径

如果本次 v2.0 补充不被接受，回滚 `Level84Ctrl.cs` 中新增的中文 Tooltip、`#region` 分区和 `enemyBlockVisibleDuration` 保持逻辑，并回滚 `specs/023-level84-v2-logic/quickstart.md` 及本次文档追加内容。

## 2026-06-22 足球刚体入场补全记录

> 当前状态校准（2026-06-23）：本节记录 2026-06-22 当时的刚体入场补全过程。当前 `Level84Ctrl.cs` 已改为 DOTween `DOMove(footballInitialPosition + footballDropOffset)` 入场，脚本中已没有 `footballBouncePower` 字段，也不会在入场阶段切到 Dynamic 做刚体下落。当前行为以“2026-06-23 Level84 音效、UI 展示和状态恢复补全”章节为准。

### 需求/背景

- 当前工作区中的 `Level84Ctrl.cs` 已把足球入场逻辑从 DOTween 下落/弹跳改到 `Rigidbody2D` 方向，但只保留了 `fbRigidbody` 字段和 `SwitchFootballState(...)` 空壳，`EnterSoccerFlow()` 的足球入场实现被挖空。
- `Level_084.prefab` 文本证据显示，`Level84Ctrl` 上仍保留 `footballDropOffset`、`footballDropDuration`、`footballBouncePower` 三个序列化值，足球物体本身也挂有 `Rigidbody2D`，因此本次按当前工作区意图补齐“刚体入场、射门前锁回 Kinematic、失败重置归位”的闭环。

### 本次影响文件和系统

- `Assets/Game/Scripts/Level/LevelItem/Level_84/Level84Ctrl.cs`
- 使用的既有系统仍为 `BaseLevelCtrl.SetCtrlable(...)`、`GameResetEvent(GameResetType.Failed)`、`TriggerAreaHandle.GetBoundaryRect()`、DOTween、UniTask 和 Unity `Rigidbody2D`
- 本次没有修改 `Level_084.prefab`、共享 `ActorCtrl`、共享触发器、存档、资源加载、平台 SDK 或构建配置

### 变更前基线

- `Level84Ctrl.cs` 中 `footballDropOffset`、`footballDropDuration`、`footballBouncePower` 字段已从脚本删除，但 prefab 仍有这三项序列化数据。
- `SwitchFootballState(...)` 只有 `bodyType` 切换和一条未完成注释，未处理 `constraints`、`gravityScale`、速度清零或回到 Kinematic。
- `EnterSoccerFlow()` 中 `if (football != null)` 代码块为空，进入足球流程后不会执行任何入场表现。

### 当时变更后行为

- 恢复 `footballDropOffset`、`footballDropDuration`、`footballBouncePower` 三个足球入场序列化字段，使脚本重新承接 `Level_084.prefab` 中现有配置。
- `ResolveReferences()` 会在 `football` 已绑定且 `fbRigidbody` 为空时自动解析足球的 `Rigidbody2D`，降低缺失手工拖拽导致的脚本空引用风险。
- `CacheInitialState()` 记录足球刚体的初始 `constraints` 和 `gravityScale`，供入场阶段和失败重置阶段切换使用。
- 当时 `EnterSoccerFlow()` 会先把足球放到 `footballInitialPosition + footballDropOffset`，切为 `Dynamic`，按 `footballDropDuration` 和初始重力计算入场速度，并叠加 `footballBouncePower` 形成一次向上的入场弹跳；该实现已被当前 DOTween 入场实现覆盖。
- `ResolveShot()` 在开始射门前会显式调用 `SwitchFootballState(false)`，确保后续 `DOMove` / `DOJump` 射门表现不与动态刚体持续模拟冲突。
- `ResetPresentationState()` 会统一停止 tween、切回 Kinematic、清空速度并归位足球，确保关卡失败重置、Disable/Destroy 清理路径和下一次进入尝试保持一致。
- 如果足球刚体不存在，入场阶段仍保留 DOTween `DOJump(...)` fallback，避免当前场景/引用异常时直接中断流程。

### 数据/资源/UI/事件流

- 数据 / 存档 / 配置：未新增 DB、PlayerPrefs、Luban、RemoteConfig、存档结构或资源包加载链路
- UI：仍只在入场表现结束后显示 `Canvas` 选择面板
- 事件：仍只派发 `GameResetEvent(GameResetType.Failed)`，没有新增 success、通关、解锁或成功存档
- 平台：未改 Android/iOS 平台代码；移动端表现仍需 PlayMode/设备补验

### 验证记录

- 本地 diff 证据确认：当前工作区此前已删除脚本中的 `footballDropOffset` / `footballDropDuration` / `footballBouncePower` 和 DOTween 入场实现，但 `Level_084.prefab` 第 91-93 行仍保留三项配置，说明脚本与 prefab 一度不一致。
- 本地 prefab 文本证据确认：`Level_084.prefab` 第 357-382 行的足球 `Rigidbody2D` 当前为 `Dynamic`、`GravityScale=1`、`Constraints=FreezeRotation`。
- 静态检查 `Level84Ctrl.cs`：未新增 `SuccessStrightly`、`GameResetType.Success`、DB、Save、RemoteConfig、PlayerPrefs、Resources、StreamingAssets、YooAsset、`Instantiate`、`OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode` 链路。
- 仅执行了脚本级静态验证，未执行 Unity PlayMode 或设备冒烟。

### 剩余风险和未验证项

- 当时物理入场速度是按本地 `Rigidbody2D` 重力和 `footballDropDuration` 计算得到，真实视觉节奏仍需在 Unity PlayMode 中确认是否符合 Level84 预期。
- 当时脚本在入场阶段结束后强制把足球位置拉回 `footballInitialPosition` 再切回 Kinematic；如果场景中依赖入场末帧的自然物理停点，仍需 PlayMode 核对是否有肉眼可见跳变。
- `football_shoot_ready`、`football_shoot`、`football_save` 三个动画名，以及 enemy 阻挡时机、失败前停留时长，仍需结合 Level84 prefab / asset 验证。

### 回滚路径

- 回滚 `Level84Ctrl.cs` 中本次恢复的足球入场序列化字段、刚体状态切换、入场速度计算、射门前锁回 Kinematic 和刚体 fallback 逻辑。
- 同步删除本节记录，并恢复到当前工作区补全前的半成品状态。

## 2026-06-22 SuccessArea 胜利路径主线合并记录

> 当前状态校准（2026-06-23）：本节中“主线已有刚体入场”的描述为当时合并背景；当前入场实现已改为 DOTween DOMove，成功路径仍会调用 `SwitchFootballState(false)` 锁定足球刚体。

### 需求/背景

- `specs/023-level84-v2-logic` 已澄清：两个射门按钮默认仍走 enemy 阻挡失败；但 football 在最终失败派发前任意时刻进入 `SuccessArea` 时，必须优先等待 2 秒并走既有成功结算。
- 主线工作树已有足球刚体入场补全改动；本次合并在保留刚体入场、射门前锁回 Kinematic 和失败重置归位逻辑的基础上，叠加 `SuccessArea` 成功优先路径。

### 本次影响文件和系统

- `Assets/Game/Scripts/Level/LevelItem/Level_84/Level84Ctrl.cs`
- `specs/023-level84-v2-logic/quickstart.md`
- `specs/023-level84-v2-logic/tasks.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_84/Level84SoccerFlow_Feature.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_84/Level84RiskAssessment.md`
- `DevelopMD/已有功能设计与使用记录.md`

### 变更后行为

- `Level84Ctrl` 新增本地 `successArea` 引用和 `successDelay` 配置；为空时按子物体名 `SuccessArea` 自动解析，不需要额外 prefab 接线。
- `Update()` 会在未结算状态下优先检查 football 是否进入 `SuccessArea`；命中后启动成功路径并阻止后续进入失败流程。
- 失败路径在射门表现后、enemy 阻挡后、失败等待后都补充 `TryStartSuccessFlow()` 抢占检查，避免同一次尝试出现成功和失败双结算。
- 成功路径启动后会隐藏选择面板、停止 football/enemy tween、停止 enemy 足球动作，并把足球刚体切回静止 Kinematic，避免与主线已有刚体入场逻辑继续物理模拟。
- 成功路径等待 `successDelay` 默认 2 秒后调用既有 `BaseLevelCtrl.SuccessStrightly()`；未进入 `SuccessArea` 时仍按原按钮逻辑派发 `GameResetEvent(GameResetType.Failed)`。
- `OnEnable()` 会绑定按钮监听，`OnDisable()` 会解绑按钮监听；`AddListeners()` 内部先移除再添加，因此与保留的 `Start()` 绑定入口不会形成重复订阅。

### 数据/资源/UI/事件流

- 数据 / 存档 / 配置：未新增 DB、PlayerPrefs、Luban、RemoteConfig、存档结构或资源包加载链路。
- UI：仍只使用 Level84 本地选择面板和两个按钮；成功路径仅负责提前隐藏面板。
- 事件：未进入 `SuccessArea` 时仍派发 `GameResetEvent(GameResetType.Failed)`；进入 `SuccessArea` 时改为等待后调用 `BaseLevelCtrl.SuccessStrightly()`。
- 平台：未改 Android/iOS SDK、ProjectSettings 或构建配置；移动端表现仍需补验。

### 验证记录

- 静态代码检查确认：`Level84Ctrl.cs` 已新增 `successArea`、`successDelay`、`TryStartSuccessFlow()`、`StartSuccessFlow()`、`CompleteSuccess()` 和 `IsFootballInsideSuccessArea()`。
- 静态代码检查确认：成功路径只复用 `BaseLevelCtrl.SuccessStrightly()`，未新增 `GameResetType.Success`、共享事件、解锁、成功存档或资源加载逻辑。
- 静态合并检查确认：主线已有 `SwitchFootballState(...)`、足球刚体入场和射门前锁回逻辑被保留；成功路径额外调用 `SwitchFootballState(false)`，避免成功等待期间刚体继续运动。
- `quickstart.md` 已补充成功后的解锁、存档、关键打点观察项，以及移动端至少 60 秒 fps/卡顿观察口径。

### 剩余风险和未验证项

- 未在 Unity PlayMode 中验证 football 在入场、直射、高射、enemy 阻挡、失败等待各阶段进入 `SuccessArea` 时，都能稳定抢占失败并且只成功一次。
- 未验证成功后既有解锁、存档和关键打点实际结果；当前只是复用 `BaseLevelCtrl.SuccessStrightly()`，仍需 PlayMode、日志、存档或打点证据确认。
- 未验证 Android/iOS 横屏触控、连续 60 秒帧率/卡顿、重复重开/退出，以及 disable 期间旧等待取消。

### 回滚路径

- 回滚 `Level84Ctrl.cs` 中 `successArea` / `successDelay`、成功等待、成功抢占失败、`SuccessArea` 检测、成功路径刚体锁定和 `OnDisable()` 按钮解绑补充。
- 同步回滚 `quickstart.md`、`tasks.md`、本功能记录、风险评估和 `DevelopMD/已有功能设计与使用记录.md` 中本次追加内容。

## 2026-06-22 Actor 足球表现改用 Level84ActorCtrl

### 需求/背景

- 本次需求要求结合当前代码结构，调整 `Level84Ctrl` 中 Actor 踢球动画相关代码，使用 `Level84ActorCtrl` 的表现功能。
- 本地代码证据显示 `Level84ActorCtrl` 已经拥有 `PlayFootballShoot()`、`PlayGoalkeeperSave()`、`StopSoccerAction()` 和 `IsSoccerActionPlaying`，并在内部管理 `football_shoot_ready`、`football_shoot`、`football_save` 动画播放与缺失动画降级。
- 因此 `Level84Ctrl` 不再直接通过 `ActorCtrl.SpineCtrl` 播放足球相关 Spine 动画，只保留关卡流程、足球路径、UI、胜负结算和缺失组件防崩溃降级。

### 影响文件和系统

- `Assets/Game/Scripts/Level/LevelItem/Level_84/Level84Ctrl.cs`
- `Assets/Game/Scripts/Level/Actor/Level84ActorCtrl.cs`：本次只复用既有 API，未修改该文件。
- `Assets/Game/Prefabs/Level/Level_084.prefab`：本次未修改 prefab；静态文本证据显示当前 actor/enemy 组件脚本 GUID 指向 `Level84ActorCtrl`。
- 使用的既有系统仍为 `BaseLevelCtrl.SetCtrlable(...)`、`GameResetEvent(GameResetType.Failed)`、`TriggerAreaHandle.GetBoundaryRect()`、DOTween、UniTask 和 Bear Logger。

### 变更前基线

- `Level84Ctrl` 之前保留本地足球动画 helper：`PlayActorSoccerAction(...)`、`PlayActorAnimationOrDelay(...)`、`HasAnimation(...)`、`StopActorMotion(...)`、`StopActorSoccerAction(...)`。
- 该 helper 会直接访问 `ActorCtrl.SpineCtrl`，并在 `Level84Ctrl` 中维护 `football_shoot_ready`、`football_shoot`、`football_save` 相关播放顺序。
- 旧文档中“Level84 actor/enemy 表现由 Level84Ctrl 通过 ActorCtrl.SpineCtrl 播放”的记录仍作为历史基线保留，本节为新的覆盖记录。

### 变更后行为

- `Level84Ctrl.ResolveShot(...)` 现在调用 `PlayActorFootballShoot()`，由解析到的 `actorSoccerCtrl.PlayFootballShoot()` 播放玩家射门准备和射门动画。
- `Level84Ctrl.PlayEnemyBlock(...)` 现在调用 `PlayEnemyGoalkeeperSave()`，由解析到的 `enemySoccerCtrl.PlayGoalkeeperSave()` 播放敌人扑救动画。
- `Level84Ctrl.ResolveReferences()` 会在 `actorSoccerCtrl` / `enemySoccerCtrl` 未手动绑定时，从已有 `actor` / `enemy` 引用自动解析同物体上的 `Level84ActorCtrl`；如果 `actor` / `enemy` 为空但新表现引用存在，也会回填基础 Actor 引用。
- 成功路径、失败重置和 Disable 清理改为优先调用 `Level84ActorCtrl.StopSoccerAction()`，同时保留原有的移动输入清零和非播放态动画恢复语义。
- 如果 actor/enemy 缺少 `Level84ActorCtrl`，脚本会输出 warning，并只做短等待与移动清理作为防崩溃 fallback；该路径不能视为 Level84 踢球/扑救表现验收通过。

### 数据/资源/UI/事件/平台流

- 数据/存档/配置：未新增 DB、PlayerPrefs、Luban、RemoteConfig、存档结构或资源包加载链路。
- UI：仍复用 Level84 本地 `Canvas`、`highBtn`、`strightBtn`，未新增 UI 资源。
- 事件：未进入 `SuccessArea` 时仍走 `GameResetEvent(GameResetType.Failed)`；进入 `SuccessArea` 时仍复用 `BaseLevelCtrl.SuccessStrightly()`。
- 资源：未新增运行时资源加载；足球动画名仍由 `Level84ActorCtrl` 内部既有常量管理。
- 平台：未修改 Android/iOS SDK、ProjectSettings 或构建配置。

### 验证记录

- 静态代码检查确认：`Level84Ctrl.cs` 已不再包含 `PlayActorSoccerAction`、`PlayActorAnimationOrDelay`、`HasAnimation`、`ActorSpineCtrl` 或足球动画名常量引用。
- 静态代码检查确认：`Level84Ctrl.cs` 新增 `actorSoccerCtrl` / `enemySoccerCtrl` 解析，并在射门、扑救、成功清理、失败重置路径调用 `Level84ActorCtrl` 相关 API。
- 静态 prefab 文本证据：`Level_084.prefab` 当前 actor/enemy 挂载脚本 GUID 为 `Level84ActorCtrl.cs.meta` 中的 `f8c368c64087fd548a915a5e12ea9cbd`。
- 已执行 `git diff --check` 和关键字扫描，结果见本次最终说明。

### 剩余风险和未验证项

- 未在 Unity PlayMode 中验证 `actorSoccerCtrl.PlayFootballShoot()` 和 `enemySoccerCtrl.PlayGoalkeeperSave()` 的实际 Spine 动画节奏、朝向、移动锁定和结束恢复。
- 未验证重复进入、快速连点、成功抢占、失败重置、Disable/退出期间足球动作协程取消是否在 PlayMode 中完全符合预期。
- 未验证 Android/iOS 横屏触控、连续 60 秒帧率/卡顿和一个未受影响 Actor 关卡冒烟。

### 回滚路径

- 回滚 `Level84Ctrl.cs` 中新增的 `actorSoccerCtrl` / `enemySoccerCtrl` 字段、解析逻辑、`Level84ActorCtrl` 调用和缺失组件 fallback。
- 如回滚到旧实现，需要同步恢复 `Level84Ctrl` 本地 `ActorCtrl.SpineCtrl` 动画 helper，并删除本节文档、风险评估和 `DevelopMD/已有功能设计与使用记录.md` 中对应追加内容。
## 2026-06-22 enemy 扑救后状态恢复

> 当前状态校准（2026-06-23）：当前 `PlayEnemyBlock(...)` 末尾的 `RestoreEnemyPresentationState()` 调用已被注释，enemy 不会在 `enemyBlockVisibleDuration` 后立即恢复；`RestoreEnemyPresentationState()` 仍存在，并由重置/禁用等路径调用。

### 需求/背景

- 本次需求要求 `PlayEnemyGoalkeeperSave()` 播放完成后恢复 enemy 的状态，避免 enemy 在扑救表现结束后持续停留在阻挡位置、阻挡动作或临时激活状态。
- 现有代码已经在 `CacheInitialState()` 中记录 `enemyInitialPosition`、`enemyInitialRotation` 和 `enemyInitialActive`，并在失败重置路径中恢复这些状态；本次将该恢复逻辑抽为本地私有方法并复用到 enemy 阻挡表现结束后。

### 影响文件和系统

- `Assets/Game/Scripts/Level/LevelItem/Level_84/Level84Ctrl.cs`
- 影响范围仅限 Level84 enemy 阻挡/扑救表现、失败前停留节奏和关卡表现状态恢复。
- 未修改 prefab、Spine 资源、Luban/DB/存档、热更资源、Android/iOS 配置、广告、IAP 或打点逻辑。

### 变更前行为

- `PlayEnemyBlock(...)` 会把 enemy 临时激活、移动到扑救位置，调用 `PlayEnemyGoalkeeperSave()` 播放扑救表现，然后等待 `enemyBlockVisibleDuration`。
- enemy 的位置、旋转、激活状态主要依赖后续 `ResetPresentationState()` 恢复；在失败事件派发前的等待窗口中，enemy 可能继续停留在临时阻挡位置。

### 当时变更后行为

- `Level84Ctrl` 新增私有 `RestoreEnemyPresentationState()`，内部会停止 enemy tween、停止 `Level84ActorCtrl` 足球动作或 fallback 动画、恢复 enemy 初始位置、初始旋转和初始激活状态。
- `ResetPresentationState()` 改为复用 `RestoreEnemyPresentationState()`，避免 enemy 恢复逻辑出现两份实现。
- 当时 `PlayEnemyBlock(...)` 在 `PlayEnemyGoalkeeperSave()` 完成并保留既有 `enemyBlockVisibleDuration` 可见停留后，如果当前 session 仍有效，会立即恢复 enemy 状态；当前代码已注释该恢复调用。

### 数据/资源/UI/事件/平台流

- 数据/存档/配置：未新增 DB、PlayerPrefs、RemoteConfig、Luban 或存档结构访问。
- 资源：未新增 Resources、StreamingAssets、YooAsset 或运行时资源加载路径。
- UI：不改变 `Canvas`、`highBtn`、`strightBtn` 的显示与监听逻辑。
- 事件：失败路径仍由后续 `CompleteFailure()` 派发 `GameResetEvent(GameResetType.Failed)`；本次仅提前恢复 enemy 表现状态，不改变成功/失败事件类型。
- 平台：未修改 Android/iOS SDK 或构建配置；移动端表现仍需 PlayMode/真机补验。

### 验证记录

- 静态代码检查确认：enemy 恢复逻辑只写入 `Level84Ctrl.cs`，未新增 `DB`、`Save`、`RemoteConfig`、`PlayerPrefs`、`Resources`、`StreamingAssets`、`YooAsset`、`Instantiate`、`OnValidate`、`ExecuteAlways` 或 `ExecuteInEditMode` 入口。
- `git diff --check` 和 C# 编译验证结果见本次最终说明。

### 当时剩余风险和未验证项

- 尚未在 Unity PlayMode 中目测确认：扑救动作结束、额外可见停留、enemy 恢复初始激活状态之间的节奏是否完全符合策划预期。
- 如果 prefab 中 enemy 初始状态为 inactive，当时恢复后 enemy 会在失败事件派发前隐藏；当前代码不再在扑救后立即执行该恢复。
- Android/iOS 横屏触控、连续 60 秒帧率/卡顿、快速连点、成功抢占和退出/disable 场景尚未补验。

### 回滚路径

- 回滚 `Level84Ctrl.cs` 中 `PlayEnemyBlock(...)` 末尾的 `RestoreEnemyPresentationState()` 调用，以及私有 `RestoreEnemyPresentationState()` 的抽取；`ResetPresentationState()` 可恢复为原本内联 enemy 状态恢复代码。
- 同步删除本节功能记录、风险评估记录和 `DevelopMD/已有功能设计与使用记录.md` 中对应条目。

## 2026-06-23 Level84 音效、UI 展示和状态恢复补全

### 需求/背景

- 本次用户反馈 Level84 功能已有修改，需要先完善代码，再同步文档。
- 当前工作区新增了 Level84 足球流程音效、选择 UI Root 缩放展示、足球触地音效触发器、prefab UI 层级和提示文案调整；代码侧需要补齐空引用保护、状态恢复和异步表现边界，避免只停留在文档更新。
- 2026-06-23 追加校准：本节已按当前 `Level84Ctrl.cs` 重新核对，仅修正文档，不修改代码；早前“刚体入场”和“扑救后立即恢复 enemy”的记录只作为历史变更背景，当前行为以下列代码事实为准。

### 影响文件和系统

- `Assets/Game/Scripts/Level/LevelItem/Level_84/Level84Ctrl.cs`
- `Assets/Game/Prefabs/Level/Level_084.prefab`
- 使用既有系统：`AudioManager.PlaySound(...)`、DOTween、UniTask、`LayerTriggerDetector`、`TriggerAreaHandle`、`BaseLevelCtrl.SuccessStrightly()`、`GameResetEvent(GameResetType.Failed)`、`Level84ActorCtrl`
- 未修改共享 `AudioManager`、`LayerTriggerDetector`、`ActorCtrl`、`Level84ActorCtrl`、存档、配置、资源加载、analytics、广告/IAP 或平台 SDK。

### 变更前基线

- `Level84Ctrl` 已有足球流程、按钮选择、enemy 阻挡、成功抢占和失败重置逻辑。
- 本次工作区脚本差异已加入 `AudioManager` 调用、`uiRoot` 字段、`PlayFootballTouchGround()` 入口和若干 fire-and-forget 表现调用。
- 本次工作区 prefab 差异显示 `Level_084.prefab` 已绑定 `uiRoot`，新增足球 `LayerTriggerDetector`，并把触地回调绑定到 `Level84Ctrl.PlayFootballTouchGround()`；同时调整了足球初始位置、`footballDropOffset`、`blockForce`、进入区大小、选择 UI Root/按钮层级、按钮点击音效和提示文案。

### 变更后行为

- 足球流程进入时播放 `whistle`，先把 football 复位到 `footballInitialPosition`，保持 `Kinematic` 并通过 DOTween `DOMove(footballInitialPosition + footballDropOffset)` 播放入场表现；当前脚本没有 `footballBouncePower` 字段，也没有在入场阶段切到 `Dynamic` 做刚体下落。
- 足球入场 DOTween 结束并额外等待 0.5 秒后播放 `grassCut`，再显示选择面板并对 `uiRoot` 播放 0.2 秒缩放展示；`uiRoot` 为空时只显示 Canvas，不中断流程。
- `PlayFootballTouchGround()` 作为 prefab 触发器回调，只播放 `footballTouchGround`，不改变胜负、存档或物理状态。
- 射门开始时播放 `kickFootball`；enemy 阻挡足球时会先停止足球 DOTween，再切到动态刚体并施加 `blockForceDir * blockForce`，同时播放 `footballOut` 和 `trap`。
- `PlayEnemyBlock(...)` 当前在 `enemyBlockVisibleDuration` 后没有立即恢复 enemy，末尾 `RestoreEnemyPresentationState()` 调用处于注释状态；enemy 初始位置、旋转和 active 的恢复发生在 `ResetPresentationState()`、`OnDisable()`、`Awake()` / `OnEnable()` 重置路径。
- 成功路径会直接激活 `successUI`，播放 `footballAudienceCelebrate`，仍等待 `successDelay` 后复用 `BaseLevelCtrl.SuccessStrightly()`；当前代码未对 `successUI` 做空引用保护，依赖 prefab 已绑定。
- `Level84Ctrl` 现在缓存并恢复 `footballInitialPosition`、足球旋转/缩放、足球初始重力、enemy 初始位置/旋转/active 状态；重开、Disable 和下一轮进入时不再依赖上一轮末位置。
- `SwitchFootballState(false)` 会清速度、清角速度、切回 Kinematic 并把 `gravityScale` 置 0；切到 Dynamic 时恢复足球初始重力。
- `shotEndPoint` 缺失时，脚本会使用当前足球位置或本关物体位置作为防崩溃 fallback，并输出 Bear Logger warning；该路径不能视为玩法验收通过。
- enemy 手动 `OnUpdate()` 只在 `enemySoccerCtrl` 仍 active in hierarchy 时执行，降低 enemy 被重置为 inactive 后继续跑表现更新的风险。

### 数据/资源/UI/事件/平台流

- 数据/存档/配置：未新增 DB、PlayerPrefs、RemoteConfig、Luban 或存档结构访问。
- 资源：未新增 Resources、StreamingAssets、YooAsset 或运行时资源加载路径；新增音效均通过既有 `AudioManager.PlaySound(string)` tag 路径调用。
- UI：本次只影响 Level84 本地选择面板 Root、两个按钮位置/点击音效和提示文本；未改共享 UI 栈。
- 事件：失败路径仍派发 `GameResetEvent(GameResetType.Failed)`；成功路径仍调用 `BaseLevelCtrl.SuccessStrightly()`；触地音效回调不派发胜负事件。
- 成功表现：`successUI` 为 Level84 prefab 本地引用，当前代码只在成功路径 `SetActive(true)`，`ResetPresentationState()` 未显式隐藏它，需 PlayMode 确认 prefab 初始状态和成功后流程是否符合预期。
- 平台：未修改 Android/iOS SDK、ProjectSettings 或构建配置；移动端表现仍需真机或模拟器补验。

### 验证记录

- 已执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`：0 error / 1114 warnings。warning 为当前工程范围既有警告，未阻断 Level84 编译。
- 已执行 `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_84/Level84Ctrl.cs`：通过；仅提示该文件未来 Git 触碰时 LF 会替换为 CRLF。
- 静态扫描 `Level84Ctrl.cs` 未发现 `OnValidate`、`Reset(...)`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、RemoteConfig、PlayerPrefs、Resources、StreamingAssets、YooAsset、Instantiate、`GameResetType.Success` 或空字符串音效调用。扫描到的 `SuccessStrightly()` 仅位于既有成功路径。
- `#region` / `#endregion` 数量检查为 11 / 11。

### 剩余风险和未验证项

- 尚未在 Unity PlayMode 目测验证新增音效 tag 是否全部存在、播放时机是否符合策划预期、触地触发器是否只在预期地面层触发。
- 尚未 PlayMode 验证 `uiRoot` 缩放、按钮位置、提示文本、足球初始位置和 `footballDropOffset=-9.5` 作为 DOTween 终点偏移后的入场表现。
- 尚未 PlayMode 验证挡球前停止 DOTween 后，直射/高射的物理反弹、enemy 阻挡后不立即恢复、失败重置节奏和下一轮重置是否符合预期。
- 尚未 PlayMode 验证 `successUI` 绑定、显示、成功后状态清理，以及 prefab 初始 inactive 状态是否稳定。
- 尚未验证 Android/iOS 横屏触控、快速连点、重开/退出/Disable 清理和 60 秒帧稳定性。

### 回滚路径

- 回滚 `Level84Ctrl.cs` 中新增的音效调用、`uiRoot` 展示 helper、`PlayFootballTouchGround()`、足球/enemy 初始状态缓存与重置、`shotEndPoint` fallback、挡球前停止足球 tween、刚体 gravityScale 切换和 `successUI` 成功显示。
- 回滚 `Level_084.prefab` 中 `uiRoot` 绑定、选择 UI 层级/位置、按钮音效组件、足球触地触发器、提示文本、本地化 key、足球/触发区/力度参数和新增手柄显示对象。
- 同步删除本节功能记录、同日风险评估和 `DevelopMD/已有功能设计与使用记录.md` 对应条目。
