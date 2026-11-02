# Level70 抓钩控制风险评估

## 2026-06-29 实现评估

### 原始行为基线

- `Level70Ctrl` 修改前只缓存 `catchAnim.transform` 并提供测试按钮切换 Animator bool `isOpen`，没有抓钩输入接管、抓取、释放、stage2 或 actor 恢复逻辑。
- `BaseLevelCtrl.SetCtrlable(false)` 静态代码只拦截左右和 cancel 输入；`BaseLevelCtrl.OnPlayerJump()` 仍会调用 `actor.TriggerJump()`。
- `ActorCtrl.TriggerJump()` 内部 `JumpButton()` 会在 `canMove=false` 时返回，但随后仍会执行跳跃 buffer 记录逻辑；Level70 actor 锁定可以阻止可见跳跃，仍需验证短时间恢复时是否有 buffer 残留。
- `Level_070.prefab` 静态文本显示根 `Level70Ctrl.actor` 和 `BaseLevelCtrl.actor` 未绑定，`controller` 触发器存在但持久化 UnityEvent 方法名为空；actor prefab override 中存在 `Level70ActorCtrl` 组件。
- `catchArea` 是 trigger collider，但静态文本中未发现绑定 `OnTrigger2DHandle` 或其它触发转发组件。

### 直接修改文件与影响链路

- `Level70Ctrl.cs`：直接影响 Level70 运行时输入事件、抓钩 tween、目标 Rigidbody2D、actor 锁定和清理路径。
- `Level70ActorCtrl.cs`：直接影响 Level70 actor 在抓钩控制、搬运和等待恢复期间的移动、跳跃和动画更新。
- `Level70BallItemCtrl.cs`：直接影响 Level70 ball item 的 close/open 视觉和 collider 开关。
- 新增文档：`Level70抓钩控制功能说明.md`、`Level70抓钩控制风险评估.md`、`DevelopMD/已有功能设计与使用记录.md` 追加记录。
- 未修改 prefab、共享触发器、共享输入事件、共享 actor 基类、保存/配置/资源/analytics/平台 SDK。

### 已使用的现有系统

- `Bear.EventSystem.EventSubscriber` 与 `EventsUtils.ResetEvents(ref ...)`：订阅和清理玩家输入、重开、游戏状态切换事件。
- `BaseLevelCtrl.SetCtrlable(...)`：在抓钩控制期间关闭共享左右/cancel actor 输入。
- `Level70ActorCtrl` / `ActorCtrl`：本地锁定 actor，阻止普通移动和可见跳跃副作用。
- `OnTrigger2DHandle`：当前 `controller` 触发器运行时注册 `OnEnter`，不修改共享脚本。
- DOTween：驱动抓钩下落、回收、上移和平移；清理路径 kill active sequence 和 catchRoot tweens。
- Unity 2D physics：使用 `Collider2D.OverlapCollider(...)` 检测 catchArea 当前重叠对象，使用 `Rigidbody2D` 暂停/恢复目标物理。

### 风险项

- **P0：stage2 和 actor 恢复入口未静态确认**。规格要求 stage2 只由 `tools` Trigger2D 进入、actor 只由恢复 Trigger2D 恢复；当前 prefab 静态文本未确认这两个对象和 UnityEvent 绑定，PlayMode 前不能视为 US3 完成。
- **P0：Unity 编译未验证**。当前 worktree 不存在 `BrainPunk/Assembly-CSharp.csproj`，无法执行 dotnet build；必须用 Unity 6000.0.59f2 打开工程确认 0 compile error。
- **P1：BaseLevelCtrl.actor 未绑定风险**。静态 prefab 显示根 `BaseLevelCtrl.actor` 为 `{fileID: 0}`；虽然 `BaseLevelCtrl.Awake()` 会 fallback `FindFirstObjectByType<ActorCtrl>()`，但正式验收应在 Unity Inspector 确认引用。
- **P1：Jump buffer 残留风险**。`BaseLevelCtrl.OnPlayerJump()` 仍会触发 `ActorCtrl.TriggerJump()`；Level70 actor lock 能阻止可见跳跃，但私有 buffer 字段是否会在快速恢复时造成一次延迟跳跃仍需 PlayMode 验证。
- **P1：物品 open/close 视觉未绑定风险**。当前 ball 可能没有挂 `Level70BallItemCtrl`；fallback 仍可搬运按名称识别的 ball，但释放后的 close/open 视觉和 collider 关闭无法保证。
- **P1：抓取对象物理恢复风险**。释放时会恢复抓取前 velocity；如果抓取前 velocity 已异常或目标被其它脚本同时控制，可能出现释放后速度不符合设计的问题。
- **P1：清理路径竞态风险**。失败、重开、成功、退出、disable/destroy 期间若 DOTween 回调与清理同时发生，可能导致目标 parent 或 actor lock 状态被旧回调回写；当前通过 kill sequence、状态重置和清理释放降低风险，仍需 PlayMode 验证。
- **P1：移动范围 fallback 风险**。range1/range2 未绑定时使用脚本 fallback x 值，能避免软锁，但范围可能不符合关卡设计；正式验收必须绑定或确认 fallback 数值。
- **P1：移动端输入风险**。左右长按、取消、多次快速 Catch、触摸丢失和横屏设备帧率尚未验证。
- **OBS：运行时触发器绑定可见性**。`controller` 的 UnityEvent 持久化方法名仍为空；当前由 `Level70Ctrl` 运行时注册，降低 prefab YAML 手改风险，但 Inspector 静态查看时不会看到该绑定。

### 已缓解风险

- 没有修改共享 `BaseLevelCtrl`、`ActorCtrl`、`OnTrigger2DHandle`、输入事件、保存/配置/资源/平台系统，降低非 Level70 回归面。
- `EnterHookControl`、`EnterStage2`、`RestoreActorControl` 都有重复触发/目标过滤保护，避免重复入口重置状态。
- Catch 下落期间重复 Jump 不会重复创建抓取流程，因为只有 `Idle` 状态接受 Catch。
- catchArea 检测不依赖新增共享 trigger stay 逻辑，使用固定长度数组 `catchOverlapResults`，避免每帧分配 List。
- 清理路径统一 kill 抓钩 sequence、释放当前目标、重置 actor 锁定和 `BaseLevelCtrl.SetCtrlable(true)`。
- `Level70Ctrl` 顶部 `version 1.0` 注释未改写，满足用户约束和仓库注释规则。

### 验证证据

- `git diff --check -- BrainPunk/Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs BrainPunk/Assets/Game/Scripts/Level/Actor/Level70ActorCtrl.cs BrainPunk/Assets/Game/Scripts/Level/LevelItem/Level_70/Level70BallItemCtrl.cs` 通过，仅有 LF/CRLF 工作区提示。
- 旧触发事件名扫描未在 Level70 脚本和当前 feature artifacts 中发现残留。
- 静态检查 `Level_070.prefab` 确认存在 `controller`、`catchArea`、`dropPoint`，actor prefab override 中存在 `Level70ActorCtrl` GUID。
- 静态检查 `PlayEvents.cs` 确认 `SwitchGameStateEvent.NewState`、`GameResetEvent`、`PlayerRightMoveEvent`、`PlayerLeftMoveEvent`、`PlayerMoveCancelEvent`、`PlayerJumpEvent` 类型存在。
- 静态检查 `BaseLevelCtrl` 确认左右/cancel 受 `ActorCtrlable` gate 控制，jump 不受该 gate 控制。
- Speckit prerequisites 脚本因分支命名规则失败，失败原因已记录；feature 目录依据 `.specify/feature.json` 确认为 `specs/031-level70-v1-control`。

### 未验证缺口

- 未运行 Unity Editor 编译。
- 未运行 Level70 PlayMode：5 次抓钩控制入口、5 次有效物品抓取、5 次 missed Catch、stage2、actor 搬运、恢复 Trigger2D、失败/重开/通关/退出/下一关均未验证。
- 未运行非 Level70 回归。
- 未运行 Android/iOS 横屏触摸 smoke。
- 未在 Unity Inspector 中保存 `Level_070.prefab` 的新增序列化字段、range1/range2、lowerBoundary、stage2 trigger、restore trigger 和 ball item 绑定。

### 回滚路径

- 回滚脚本：恢复 `Level70Ctrl.cs`、`Level70ActorCtrl.cs`、`Level70BallItemCtrl.cs` 修改前内容。
- 回滚文档：删除或回滚本文件、`Level70抓钩控制功能说明.md` 和 `DevelopMD/已有功能设计与使用记录.md` 中本次 Level70 抓钩控制记录。
- 如果后续 Unity 绑定 prefab 后发现问题，优先回滚 `Level_070.prefab` 的新增绑定，再回滚脚本，避免影响共享系统。

## 2026-06-30 v2 接触回收与 ball 初始化风险评估

### 原始行为基线

- `OnCatchAreaEnter(Collider2D)` 的 v1 基线是合法接触后进入 `CaptureTarget(...)`，随后暂停目标物理、设置 parent 到抓钩区域、移动到 `dropPoint` 并释放。
- `RecoverAfterMiss()` 是既有未命中回收路径，会将抓钩移动回初始高度并恢复 `Idle`。
- `Level70BallItemCtrl` 的 v1 状态 API 只包含 close/open 切换；没有从 `Level70Ctrl.itemSprites` 初始化内容的公开入口。
- `Level_070.prefab` 当前工作区已是脏状态；静态文本显示 Level70Ctrl 有 `catchArea`、`dropPoint`、range1/range2、`lowerBoundary`、`hookControlTrigger`、`balls` 和 `itemSprites` 引用，但这些引用未经过 Unity Inspector/PlayMode 复核。

### 变更入口到影响链路

- Git 入口：未提交 diff 触达 `Level70Ctrl.cs` 和 `Level70BallItemCtrl.cs`，并追加本地文档与 Speckit 任务记录；未编辑 shared controller、save/config/resource/SDK 文件。
- 运行时入口：玩家在 Level70 抓钩控制状态点击 Catch，`catchArea` 重叠检测或 UnityEvent 进入 `OnCatchAreaEnter(...)`。
- 状态链：有效接触从 `Lowering` 转入 `RecoverAfterContact()`，再进入 `Recovering -> Idle`；不设置 `currentTarget`，不进入 `Carrying/Retracting/MovingToDrop/Releasing`。
- 资源链：ball 内容来自 `itemSprites` 序列化 Sprite 引用；缺失 sprite 只隐藏内部 renderer，不禁用 ball 根节点或 collider。
- 数据链：不读写 DB、PlayerPrefs、RemoteConfig、Luban 配置或资源地址；不新增 analytics、广告、IAP 或平台 SDK 调用。

### 风险项

- **P0：Unity PlayMode 未验证 v2 接触是否可完成回收**。风险来自 `OnCatchAreaEnter(...)` 接触路径变更；若 tween kill、Animator 或 `catchRoot` 绑定异常，可能出现抓钩停留或 Level70 软锁。建议在 Level70 连续 5 次接触 ball 验证关闭、回收、继续操作。
- **P0：stage2/actor 恢复仍未完成手工回归**。v2 不应改变 `tools` stage2 和 actor 专用恢复入口，但当前只做静态代码检查，未证明实际 prefab 触发器和 UnityEvent 生效。
- **P1：空内容 ball 的视觉语义依赖 prefab close 外观**。`InitializeContent(null)` 会隐藏内部 sprite 并保留 closePart；如果某个 ball 的可见主体只依赖该 sprite，玩家可能看到不可见但 collider 有效的空 ball。需要在 `balls.Count > itemSprites.Count` 配置下手工确认“可见空内容”符合设计。
- **P1：ball 初始化会在清理重置路径恢复 close/collider**。这是规格要求的重开/失败后恢复，但如果通关动画期望保留 open 视觉，`SwitchGameStateEvent.SUCCESS` 清理可能提前重置，需要 PlayMode 观察通关表现。
- **P1：prefab 当前脏改和行尾空格未接管**。`git diff --check` 命中 `Level_070.prefab` 多处行尾空格，本次未清理，后续提交前需要由 prefab 变更负责人确认是否清理或保存重导。
- **P1：移动端快速触摸未验证**。快速点击 Catch、左右触摸取消、前后台切换可能暴露 tween 状态或输入状态残留；本次只有静态编译证据。
- **OBS：v1 搬运方法仍保留在文件内**。`CaptureTarget(...)`、`PauseTargetPhysics(...)`、`AttachTargetToCatch(...)` 仍存在，用于降低删除面和回滚成本；当前静态扫描确认 `OnCatchAreaEnter(...)` 不再调用它们。若后续没有新搬运需求，可在单独规格中清理。

### 已缓解风险

- v2 接触入口只在 `Application.isPlaying`、`hookState == Lowering`、无 `currentTarget` 时执行，避免编辑器态或非下落状态触发运行时链路。
- `RecoverAfterContact()` 不复制 target、不暂停 Rigidbody2D、不设置 parent、不移动目标到 `catchAreaPosition` 或 `dropPoint`，直接降低自动吸附/搬运风险。
- `UpdateCatchAreaOverlap()` 会在 `hookState` 改变后停止本次循环，避免一次下落处理多个有效接触。
- ball 初始化跳过 null ball，itemSprites 缺失或 null 时按空内容处理，多余 sprite 不会访问。
- 新增公共 API `InitializeContent(Sprite)` 已用中文注释说明用途、调用边界和注意事项。
- 本次未修改共享输入、共享 trigger、`ActorCtrl`、`BaseLevelCtrl`、存档、配置、资源加载、UI、analytics、广告、内购或平台 SDK。

### 验证证据

- Speckit prerequisites：当前分支 `feature/1.9.0_common` 不符合 Speckit 数字分支规则，前置脚本失败；feature 目录依据 `.specify/feature.json` 为 `specs/032-level70-v2-hook-balls`。
- 静态 prefab 扫描确认 `Level_070.prefab` 当前文本中存在 `catchArea`、`dropPoint`、range1/range2、`lowerBoundary`、`hookControlTrigger`、`balls` 和 `itemSprites` 字段引用。
- 静态代码扫描确认 `Level70Ctrl.cs` 顶部 `version 1.0` / `version 2.0` 注释仍存在；v2 接触入口进入 `RecoverAfterContact()`；`InitializeBallsFromSprites()` 调用 `Level70BallItemCtrl.InitializeContent(...)`。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，0 error / 96 warning。
- `git diff --check` 未通过，原因是已脏 `Level_070.prefab` 多处行尾空格和 `Level70Ctrl.cs` 顶部空注释行尾空格；本次未清理以避免改写注释或接管 prefab 脏改。

### 未验证缺口

- 未运行 Unity Editor Console 编译、资源导入和 prefab 序列化检查。
- 未运行 Level70 PlayMode：US1 5 次接触、US2 三种 ball/sprite 矩阵、US3 stage2/actor 恢复/失败/重开/通关/退出/下一关均未完成。
- 未运行非 Level70 已验收关卡回归。
- 未运行 Android/iOS 横屏触摸和 60 fps smoke。
- 未在 Unity Inspector 中逐项确认 `Level_070.prefab` 的 ball 可见主体、空内容表现、stage2 trigger、actor 恢复 trigger 和 range/lowerBoundary 绑定。

### 回滚路径

- 脚本回滚：恢复 `OnCatchAreaEnter(...)` 到 v1 `CaptureTarget(...)` 路径，删除 `RecoverAfterContact()`、`InitializeBallsFromSprites()` 调用和 `InitializeContent(...)`。
- 行为回滚：如果 v2 接触导致软锁或流程阻塞，优先只回滚 Level70 接触路径，保留其它共享系统不变。
- ball 初始化回滚：如果 ball 显示或 collider 矩阵不符合设计，先移除 `ResetHookRuntime(...)` 中的 `InitializeBallsFromSprites()` 调用，再由 Unity Inspector 检查 prefab 初始状态。
- prefab 回滚：本次未编辑 prefab；若后续保存 prefab 后出问题，优先回滚 `Level_070.prefab` 的 Level70 专用绑定。

## 2026-06-30 v2.1 Catch 按钮 UI 切换风险评估

### 原始行为基线

- `Level70Ctrl.cs` 顶部 `version 2.1` 注释要求 handle control 状态隐藏 `jumpBtn` 并使用 `catch_btn`。
- `Level_070.prefab` 文本显示 `BaseLevelCtrl.gamePlayPanelName` 为 `GamePlayPanel_025_70`；`GamePlayPanel_025_70.prefab` 文本显示存在 `m_Name: catch_btn`。
- 共享 `GamePlayPanel.cs` 修改前只订阅 `JumpBtn.OnClickDown += OnClickJump`，没有解析 `Root/catch_btn`；用户已明确要求 Level70 / `LevelCtrl` 局部 UI 逻辑不得直接修改共享 `GamePlayPanel.cs`，应使用子类或关卡专用面板组件。
- `Level70Ctrl` 修改前不会直接访问 `PlayCtrl.CurrentGamePlayPanel` 切换 GamePlayPanel UI 模式。

### 变更入口到影响链路

- Git 入口：未提交 diff 触达 `Level70Ctrl.cs`、`Level70GamePlayPanel.cs`、`GamePlayPanel_025_70.prefab` 和 Level70 本地文档；不新增 `PlayEvents`，不在共享 `GamePlayPanel.cs` 增加 Level70 专用逻辑。
- UI 入口：`GamePlayPanel_025_70.prefab` 绑定 `Level70GamePlayPanel : GamePlayPanel`；该子类在 `OnCreate()` / `OnOpen()` 解析 `Root/jump_btn` 与 `Root/catch_btn`，绑定 Catch 按钮，并默认隐藏 Catch。
- 关卡入口：`Level70Ctrl.EnterHookControl(...)` 设置抓钩控制后从 `PlayCtrl.Instance.CurrentGamePlayPanel` 获取 `Level70GamePlayPanel`，再调用 `SetLevel70CatchButtonVisible(true)`；`RestoreActorControl()` 和 `ResetHookRuntime(...)` 调用 false。
- 输入链路：`catch_btn` 点击仍派发既有 `PlayerJumpEvent`，继续由 `Level70Ctrl.OnPlayerJump(...)` 启动 Catch。
- 资源链路：依赖 `GamePlayPanel_025_70.prefab` 保持 `Level70GamePlayPanel` 组件、`Root/jump_btn`、`Root/catch_btn` 路径和 `CustomButton` 组件；没有新增资源加载路径。

### 风险项

- **P0：PlayMode 未验证按钮恢复可能造成软锁**。如果进入抓钩控制后 `catch_btn` 未显示或无法点击，玩家无法触发 Catch；如果恢复/重开后 `jumpBtn` 未恢复，非抓钩移动流程会受阻。需要覆盖进入抓钩、恢复 actor、失败重开、成功和退出关卡。
- **P1：`GamePlayPanel_025_70.prefab` 组件绑定风险**。如果 prefab 没有正确挂载 `Level70GamePlayPanel`，`Level70Ctrl` 会记录 warning 并跳过 UI 切换，玩家仍看到默认 Jump；需在 Unity Inspector / PlayMode 中确认组件导入和序列化字段继承正常。
- **P1：`catch_btn` 路径硬编码依赖 prefab 层级**。当前子类用 `Root/catch_btn` 文本路径解析，若 `GamePlayPanel_025_70.prefab` 后续改名或移动节点，Level70 会回退为只显示/隐藏 Jump，Catch 按钮不可用；需在 UI prefab 变更时同步复核。
- **P1：Catch 按钮使用 `OnClickDown` 可能受快速触摸影响**。它复用 Jump 按钮触发方式，理论一致，但移动端快速按下/取消/切后台仍未验证。
- **P1：直接依赖当前 GamePlayPanel 创建时序**。`Level70Ctrl` 通过 `PlayCtrl.Instance.CurrentGamePlayPanel.GetComponent<Level70GamePlayPanel>()` 调用 UI API；若进入抓钩控制时当前面板尚未创建或不是 Level70 子类，调用会跳过，UI 可能仍显示默认 Jump，需要 PlayMode 确认创建时序。
- **OBS：共享 `GamePlayPanel.cs` 仍有无关脏改**。当前工作区共享 `GamePlayPanel.cs` 存在 `starBlock` 等非本需求改动；本风险评估不接管这些变更，也不把它们作为 Level70 UI 方案的一部分。

### 已缓解风险

- Level70 UI 切换逻辑隔离在 `Level70GamePlayPanel` 子类中，避免把单关按钮逻辑写入共享 `GamePlayPanel.cs`。
- `Level70GamePlayPanel` 解析不到 `catch_btn` 时直接跳过，不会空引用崩溃。
- `catch_btn` 不新增输入事件，继续派发 `PlayerJumpEvent`，减少 Level70 抓钩输入链路改动面。
- `Level70GamePlayPanel.OnOpen()` 默认调用 `SetLevel70CatchButtonVisible(false)`，降低面板复用时残留 Catch 模式的风险；同时重新绑定 Catch 按钮，避免 `OnClose()` 解绑后实例复用时按钮失效。
- `Level70Ctrl.SetLevel70CatchButtonMode(...)` 有重复状态保护，避免同一 UI 模式反复调用当前面板。
- 本次未修改 `CustomButton`、`PlayEvents`、`BaseLevelCtrl`、`ActorCtrl`、存档、配置、资源加载、analytics、广告、IAP 或平台 SDK。

### 验证证据

- 静态扫描确认 `Level_070.prefab` 指向 `GamePlayPanel_025_70`，且 `GamePlayPanel_025_70.prefab` 存在 `catch_btn`，并添加了脚本 GUID `a8d5a5b6e2c84f45a3fd0b2f5b7c9e31` 的 `Level70GamePlayPanel` 组件。
- 静态扫描确认旧 Level70 UI 事件名已无残留；`SetLevel70CatchButtonVisible(...)` 只存在于 `Level70GamePlayPanel`、`Level70Ctrl` 和文档中。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，0 error / 352 warnings；warnings 属于当前工程输出，未逐项审计。
- `git diff --check -- AGENTS.md Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs Assets/Game/Scripts/Level/LevelItem/Level_70/Level70GamePlayPanel.cs Assets/Game/Scripts/Level/LevelItem/Level_70/Level70GamePlayPanel.cs.meta Assets/Game/UI/GamePanel/Resources/GamePlayPanel_025_70.prefab Assets/Game/UI/GamePanel/Resources/GamePlayPanel_025_70.prefab.meta Assets/Game/Scripts/Level/LevelItem/Level_70/Level70抓钩控制功能说明.md Assets/Game/Scripts/Level/LevelItem/Level_70/Level70抓钩控制风险评估.md DevelopMD/已有功能设计与使用记录.md` 通过，仅有 Git 工作区 LF/CRLF 转换提示。

### 未验证缺口

- 未运行 Unity Editor PlayMode 验证 Level70 handle control 下 Jump/Catch 按钮实际显隐与点击。
- 未验证重开、失败、成功、退出关卡和下一关后 UI 是否恢复默认 Jump。
- 未运行 Android/iOS 横屏触摸 smoke。
- 未运行非 Level70 GamePlayPanel 回归；本次没有新增共享 Level70 UI 逻辑，但共享 `GamePlayPanel.cs` 已存在无关脏改，仍需由对应改动补验默认 `jump_btn`、左右移动按钮、Reset/Pause/Tips/Shop。
- 未在 Unity Inspector 中确认 `catch_btn` 的 `CustomButton`、点击音效、图标、初始 active 状态和遮挡层级。

### 回滚路径

- 回滚 `Level70Ctrl.cs` 中 `isLevel70CatchButtonMode`、`SetLevel70CatchButtonMode(...)` 和相关调用；Level70 将恢复为通过默认 `jump_btn` 触发 Catch。
- 删除或停用 `Level70GamePlayPanel.cs`，并把 `GamePlayPanel_025_70.prefab` 恢复为共享 `GamePlayPanel` 组件。

## 2026-06-30 v2.2 Catch 统一回 dropPoint 与 isOpen 节奏风险评估

### 原始行为基线

- v2 接触路径只在 `TryResolveTarget(...)` 成功时收束；背景、边界或其它外部 collider 不会触发关闭回收。
- v2 接触和 miss 都回收到初始抓钩高度，然后回到 `Idle`；不会统一移动到 `dropPoint`。
- v2 没有 `dropPoint` 打开等待窗口；回收完成后立即允许继续左右移动和下一次 Catch。
- 默认/清理路径已经通过 `ResetHookRuntime(...)` 调用 `SetHookOpen(false)`，但下降后的最终等待关闭节奏不完整。

### 变更入口到影响链路

- Git 入口：未提交 diff 触达 `Level70Ctrl.cs` 和本地 Level70 文档；未修改 prefab、共享输入、UI prefab、save/config/resource/SDK 文件。
- 运行时入口：玩家在 Level70 抓钩控制态点击 Catch，抓钩进入 `Lowering`；`catchArea` 碰到外部 active collider 或下落 tween 到达最低点都会进入统一收束。
- 状态链：`Lowering(open=true)` -> 接触/最低点 `Retracting(open=false)` -> `MovingToDrop(open=false)` -> 到达 `dropPoint` 后 `Releasing(open=true, 2s)` -> `Idle(open=false)`。
- 输入链：`CanReceiveHookMoveInput()` 和 `TryStartCatch()` 都要求 `hookState == Idle`，因此回 `dropPoint` 和 2 秒等待阶段不会接收左右或 Catch。
- 资源/数据链：继续使用现有 `dropPoint` 引用和 Animator bool `isOpen`；不读写 DB、PlayerPrefs、RemoteConfig、Luban、Resources/YooAsset、analytics、广告、IAP 或平台 SDK。

### 风险项

- **P0：PlayMode 未验证统一回 dropPoint 是否会软锁**。如果 `dropPoint` 绑定、tween 回调或清理路径异常，抓钩可能停留在非 `Idle` 状态，玩家无法继续操作。需要覆盖接触、未接触到底、重开/失败/成功中断。
- **P1：接触范围放宽可能提前收束**。本次按“碰到物体”将接触判断放宽到任意外部 active collider；若关卡中存在不应触发的背景/边界 trigger，抓钩可能比 v2 更早关闭回 `dropPoint`。当前只排除 `catchArea` 自身和抓钩层级内 collider，仍需 PlayMode 确认场景层级。
- **P1：2 秒等待期间 UI 仍可点击但逻辑忽略**。按钮交互状态未禁用，`TryStartCatch()` 会因非 `Idle` 直接返回；如果设计要求按钮视觉不可点，需要另行补 UI interactable 或遮罩逻辑。
- **P1：dropPoint fallback 只防软锁不等于设计正确**。`dropPoint` 缺失时高度 fallback 到初始高度、x fallback 到当前 x，可避免空引用，但不满足用户要求的真实 `dropPoint` 节奏；必须在 Inspector 中确认绑定。
- **P1：旧 v1 搬运方法仍保留**。`CaptureTarget(...)`、`StartCarryToDropPoint(...)`、`ReleaseCurrentTargetForGameplay(...)` 仍在文件中，当前入口不调用；后续若误接回旧入口会绕过本次开合节奏。
- **OBS：`missRecoverDuration` 和 `Recovering` 状态暂时未被新收束路径使用**。保留它们可降低删除面和回滚成本，但后续清理需要单独规格和回归证据。

### 已缓解风险

- `ReturnHookToDropPointAfterCatch()` 外层检查 `Application.isPlaying`、`hookState == Lowering` 和 `catchRoot != null`，避免编辑器态、非下落状态或缺失根节点触发运行时链。
- 收束开始时会 kill 当前 sequence 和 `catchRoot` tweens，并清空输入与目标缓存，降低重复回调和残留目标风险。
- `IsHookContactCollider(...)` 排除 `catchArea` 自身、抓钩根节点和抓钩子节点，避免抓钩内部 collider 触发自己的收束。
- 2 秒等待期间状态是 `Releasing`，既有输入 gate 可直接阻止左右移动和重复 Catch。
- 本次未修改共享输入、共享 UI 事件、存档、配置、资源加载、analytics、广告、IAP 或平台 SDK，风险集中在 Level70 本地抓钩流程。

### 验证证据

- 静态扫描确认 `OnCatchAreaEnter(...)` 改为 `IsHookContactCollider(...)` 过滤后进入 `RecoverAfterContact()`，不再从接触入口调用 `TryResolveTarget(...)` 或 `CaptureTarget(...)`。
- 静态扫描确认 `RecoverAfterMiss()` 与 `RecoverAfterContact()` 均进入 `ReturnHookToDropPointAfterCatch()`，并在到达 `dropPoint` 后 `SetHookOpen(true)`、等待 `DropPointOpenWaitDuration`、完成后 `SetHookOpen(false)`。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过，0 error / 97 warnings。

### 未验证缺口

- 未运行 Unity Editor PlayMode：触碰 ball、触碰非 ball 外部 collider、未触碰到最低点、重复 Catch、左右长按、移动取消、重开、失败、成功、退出和下一关均未完成。
- 未运行 Android/iOS 横屏触摸和 60 fps smoke。
- 未在 Unity Inspector 中确认 `dropPoint`、`catchArea`、背景/边界 collider、range/lowerBoundary 和 `GamePlayPanel_025_70` Catch 按钮实际状态。
- 未运行非 Level70 回归；虽然本次只改 Level70 脚本，仍需至少一个已验收关卡验证默认 Jump/左右/成功/失败流程。

### 回滚路径

- 回滚 `Level70Ctrl.cs` 中 `ReturnHookToDropPointAfterCatch()`、`ReturnToIdleAfterDropPointOpen()`、`DropPointOpenWaitDuration` 和 `IsHookContactCollider(...)` 的接触放宽。
- 恢复 `RecoverAfterMiss()` 到初始高度回收路径，并按 v2 逻辑只在可解析目标接触时关闭回收。
- 若风险只来自接触范围放宽，可保留回 `dropPoint` 节奏，仅把 `OnCatchAreaEnter(...)` 过滤恢复为 `TryResolveTarget(...)`。

## 2026-06-30 v2.2 isOpen 即时切换风险评估补充

### 原始行为基线

- `Level70Ctrl.TryStartCatch()` 在下降开始时调用 `SetHookOpen(true)`，代码层面会把 Animator bool `isOpen` 设为 true。
- `catch.controller` 的 `isOpen=true` 转场从 `catch_idle` 到 `cactch_grap`，但原始资源启用了 `m_HasExitTime=1` 且 `m_TransitionDuration=0.25`，会延迟视觉切换。
- `isOpen=false` 转场已无 exit time，但也有 0.25 秒过渡。

### 风险项

- **P1：即时切换可能过硬**。把两条转场过渡时长改为 0 后，开合视觉与逻辑同步，但不再有原来的 0.25 秒混合，需要 PlayMode 观察是否符合表现预期。
- **P1：资源修改需要 Unity 导入验证**。YAML 静态修改可读，但最终 AnimatorController 导入和状态机显示仍需 Unity Editor 确认。
- **OBS：仍可能被接触同帧关闭**。如果下降起点立刻与外部 collider 重叠，`UpdateCatchAreaOverlap()` 会马上进入关闭收束；这属于接触逻辑触发，不是 Animator 转场延迟。

### 已缓解风险

- 本次只改 Level70 专用 `catch.controller`，不影响其它关卡 AnimatorController。
- 没有改 `Level70Ctrl` 的状态机、输入、dropPoint 回收、ball 初始化、UI、存档、配置、资源加载、analytics、广告、IAP 或平台 SDK。

### 验证证据

- 静态证据：`Level_070.prefab` 的 `catch` Animator 绑定 controller GUID `2db3b5e916d190a4dac7d7166834aab5`，对应 `Assets/Game/ArtSrc/Animations/Level70/catch.controller`。
- 静态证据：`catch.controller` 中 `isOpen=true` 和 `isOpen=false` 两条转场当前均为 `m_HasExitTime=0`、`m_TransitionDuration=0`。

### 未验证缺口与回滚

- 未运行 Unity Editor PlayMode 观察下降开始帧和接触关闭帧。
- 若美术表现需要平滑过渡，回滚方式是把对应转场 `m_TransitionDuration` 恢复为 0.25；不建议恢复 `isOpen=true` 的 exit time，否则会重新出现下降时看起来没生效的延迟。

## 2026-06-30 v2.2 OnTrigger2D API 与 BallItem 弹出风险评估

### 原始行为基线

- `Level70Ctrl.EnterStage2(Collider2D)` 的原有入口只接受 tag 为 `tools` 的 collider，进入 `Complete` 阶段并调用 `onStage2Entered`，不会恢复 Player 普通控制。
- `Level70Ctrl.RestoreActorControl(Collider2D)` 已能按 Player/actor collider 恢复抓钩控制态，但不负责进入 stage2。
- `Level70BallItemCtrl` 原有 open/close API 只切换 `closePart`、`openPart` 和 collider；未播放 `ItemJumpOutExecutor`。
- `Level_070.prefab` 静态文本显示多个 ball 及其子节点已有 `Level70BallItemCtrl` 和 `ItemJumpOutExecutor` GUID，但本次未通过 Unity Inspector/PlayMode 确认所有绑定完整。

### 变更入口到影响链路

- Git 入口：未提交 diff 触达 `Level70Ctrl.cs`、`Level70BallItemCtrl.cs` 和 Level70 本地文档；未修改 prefab、共享输入、共享 trigger、save/config/resource/SDK。
- OnTrigger2D 链路：`OnTrigger2DHandle.OnEnter(Collider2D)` 可绑定 `EnterStage2AndRestorePlayerControl(Collider2D)` 或 `OpenBallItemByTools(Collider2D)`，参数直接来自触发 collider。
- 状态链：Player API 会进入 `Complete` 并调用 `RestoreActorControl()`，从抓钩控制态恢复普通输入；tools API 会把对应 ball 置为 open 并关闭 collider。
- 资源链：ball 弹出依赖当前 ball 或子节点上的 `ItemJumpOutExecutor`；缺失时不会崩溃，但弹出表现缺失。

### 风险项

- **P0：Prefab 未绑定新 API 会导致 v2.2 行为不可见**。本次只补脚本入口，没有修改 `Level_070.prefab` 的 `OnTrigger2DHandle` 持久化事件；如果 Inspector 未绑定，Player 触发 stage2 恢复和 tools 打开 ball 都不会发生。
- **P1：Player 入口恢复控制时机风险**。`EnterStage2AndRestorePlayerControl(...)` 会在进入 `Complete` 后立即调用 `RestoreActorControl()`；如果关卡设计需要 stage2 动画或其他条件完成后再恢复，需要在 PlayMode 中确认时序。
- **P1：Ball item 弹出依赖子节点 active 状态**。`OpenAndPlayJumpOut()` 当前先 `SetOpenState()` 再播放 `jumpOut`；静态 prefab 显示 item 子节点不一定都在 `closePart` 下，但仍需 PlayMode 验证被隐藏节点不会影响协程启动。
- **P1：重复触发被忽略可能影响需要重复弹出的设计**。`hasOpened` 防止重复播放，适合一次性打开；如果设计需要每次 tools 触发都重新弹出，需要调整为显式重置或重新播放。
- **OBS：旧 `EnterStage2(Collider2D)` 仍保留 tools 语义**。本次新增 Player API 没有删除旧入口，便于 prefab 分别绑定；后续应避免把 Player 触发器误绑到旧 `EnterStage2`。

### 已缓解风险

- 两个新增 API 都在外层检查 `Application.isPlaying`，避免编辑器态触发运行时链路。
- `EnterStage2AndRestorePlayerControl(...)` 使用 `IsActorCollider(...)` 过滤，避免 tools 或其它 collider 恢复 Player 控制。
- `OpenBallItemByTools(...)` 使用 `tools` tag 过滤，并在找不到 `Level70BallItemCtrl` 时直接跳过，避免空引用。
- `Level70BallItemCtrl.ResolveReferences()` 会从子节点兜底查找 `ItemJumpOutExecutor`，降低新增序列化字段未绑定导致的表现缺失概率。
- `hasOpened` 阻止重复触发造成多条弹出协程叠加。

### 验证证据

- 静态扫描确认新增 API 存在：`EnterStage2AndRestorePlayerControl`、`OpenBallItemByTools`、`EnterCompletePhase`、`OpenAndPlayJumpOut`。
- 静态 prefab 扫描确认 `Level_070.prefab` 中存在 `Level70BallItemCtrl` GUID `19022ba3c91fa8e40b9437abe3b89e98` 和 `ItemJumpOutExecutor` GUID `41b793ef7e73be24e8bf1bce44134772`。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过，0 error / 98 warnings。

### 未验证缺口

- 未运行 Unity PlayMode：Player 触发 stage2 恢复、tools 打开 ball、item 弹出、重复触发、失败/重开/退出清理均未验证。
- 未运行 Android/iOS 横屏触摸 smoke。
- 未在 Unity Inspector 中确认新 API 的 `OnTrigger2DHandle` 绑定、targetTag、触发器尺寸和触发层。
- 未运行非 Level70 回归；本次未修改共享系统，但仍需至少一个已验收关卡补普通移动/跳跃/成功/失败验证。

### 回滚路径

- 脚本回滚：删除 `EnterStage2AndRestorePlayerControl(...)`、`OpenBallItemByTools(...)`、`EnterCompletePhase()` 的复用改动，以及 `OpenAndPlayJumpOut()`、`jumpOut`、`hasOpened`。
- 绑定回滚：若 Unity 中已绑定新 API 后出现异常，先从对应 `OnTrigger2DHandle` 移除新方法绑定，再保留脚本观察是否恢复旧流程。
- 表现回滚：若只是不需要 item 弹出，可保留 ball open 入口，仅移除或跳过 `jumpOut.Play()`。

## 2026-06-30 v2.2 DropTrigger/Player/tools 语义校正与 BallItem 排序风险评估

### 原始行为基线

- 上一版实现把 Player 入口误合并为 `EnterStage2AndRestorePlayerControl(Collider2D)`，会同时进入 `Complete` 并恢复普通移动；这与用户最新校正不一致。
- 上一版 `EnterStage2(Collider2D)` 按旧理解接受 tag 为 `tools` 的 collider；当前 `Level_070.prefab` 静态文本已经显示 stage2 绑定形态是 `targetTag: lock -> EnterStage2`。
- `Level70BallItemCtrl` 原有打开逻辑只切换 `closePart`、`openPart` 和 collider，并播放 `ItemJumpOutExecutor`；没有提升 `SortingGroup.sortingOrder`。

### 变更入口到影响链路

- Git 入口：未提交 diff 触达 `Level70Ctrl.cs`、`Level70BallItemCtrl.cs` 和 Level70 本地文档；未修改 prefab、共享 trigger、共享输入、save/config/resource/SDK。
- OnTrigger2D 链路：`lock` collider 通过 `OnTrigger2DHandle.OnEnter(Collider2D)` 调用 `EnterStage2(Collider2D)`；Player collider 调用 `RestoreActorControl(Collider2D)`；tools collider 调用 `OpenBallItemByTools(Collider2D)`。
- 状态链：`lock` 只切换 `phase=Complete` 并触发 `onStage2Entered`；Player 只关闭抓钩控制态并恢复 `BaseLevelCtrl.SetCtrlable(true)`；tools 只打开对应 ball。
- 资源链：ball 打开依赖当前 ball 自身或子节点上的 `SortingGroup` 与 `ItemJumpOutExecutor`；缺失时不会崩溃，但排序提升或弹出表现会缺失。

### 风险项

- **P0：Trigger 绑定误配会直接改变关卡阶段时机**。如果 Player 仍绑定到旧合并入口或 `EnterStage2`，Player 触发会错误进入 stage2；如果 `lock` 未绑定 `EnterStage2`，DropTrigger 命中 lock 后不会进入 stage2。
- **P1：SortingGroup 查找范围可能不符合实际层级**。当前脚本优先取当前对象，再取子节点；若需要提升的是父级 SortingGroup，运行时不会改到目标，需要 Inspector 绑定 `sortingGroup` 或调整解析策略。
- **P1：恢复排序依赖运行时缓存的默认 order**。`InitializeContent(...)` / `SetClosedState()` 会恢复 Awake/首次 Resolve 时缓存的 order；如果其它脚本在运行中也改同一个 SortingGroup，可能被本脚本恢复覆盖。
- **P1：Ball 打开后 collider 关闭与排序提升同时发生**。如果弹出表现依赖 collider 或 closePart 下的 active 状态，需要 PlayMode 验证 `SetOpenState()` 后 `ItemJumpOutExecutor.Play()` 仍能正常显示。
- **OBS：旧文档章节保留历史误解**。本文件前一节保留了上一版误解记录；最新结论以本节和功能说明最新章节为准。

### 已缓解风险

- `EnterStage2(Collider2D)` 使用 `LockTag` 过滤，`OpenBallItemByTools(Collider2D)` 使用 `ToolsTag` 过滤，Player 恢复继续使用 `IsActorCollider(...)` 过滤。
- 已移除误加的 `EnterStage2AndRestorePlayerControl(Collider2D)`，降低 Inspector 误绑到合并入口的概率。
- `Level70BallItemCtrl` 在 `InitializeContent(...)` 和 `SetClosedState()` 恢复默认排序，避免重开或重新初始化后 ball 长期停留在 180。
- 本次没有新增全局事件、保存、配置、资源加载、analytics、广告、IAP 或平台 SDK 链路。

### 验证证据

- 静态 prefab 扫描确认当前 `Level_070.prefab` 存在 `targetTag: Player -> RestoreActorControl`、`targetTag: tools -> OpenBallItemByTools`、`targetTag: lock -> EnterStage2`。
- 静态代码扫描确认 `Level70Ctrl.cs` 不再包含 `EnterStage2AndRestorePlayerControl`，并存在 `ToolsTag="tools"`、`LockTag="lock"`。
- 静态代码扫描确认 `Level70BallItemCtrl.cs` 存在 `SortingGroup`、`openSortingOrder=180`、`SetOpenSortingOrder()` 和 `RestoreSortingOrder()`。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，0 warning / 0 error。
- 风险词静态扫描未发现本次新增 Level70 脚本包含 DB、Save、RemoteConfig、PlayerPrefs、StreamingAssets、OnValidate、ExecuteAlways、ExecuteInEditMode、Instantiate、PlayEvents 或 Debug.Log 链路。
- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs Assets/Game/Scripts/Level/LevelItem/Level_70/Level70BallItemCtrl.cs Assets/Game/Scripts/Level/LevelItem/Level_70/Level70抓钩控制功能说明.md Assets/Game/Scripts/Level/LevelItem/Level_70/Level70抓钩控制风险评估.md DevelopMD/已有功能设计与使用记录.md` 未通过，仅命中 `Level70Ctrl.cs` 顶部 `version 2.2` 注释行尾空格；因用户要求不修改注释内容，未清理这些空格。

### 未验证缺口

- 未运行 Unity PlayMode：DropTrigger 命中 `lock`、Player 恢复普通移动、tools 打开 ball、item 弹出、排序层级提升和重开排序恢复均未验证。
- 未运行 Android/iOS 横屏触摸 smoke。
- 未在 Unity Inspector 中确认每个 ball 的 `SortingGroup` 是否位于当前脚本可解析层级，且 `openSortingOrder=180` 是否符合最终遮挡设计。
- 未运行非 Level70 回归；本次只改 Level70 脚本，仍需至少一个已验收关卡补普通移动/跳跃/成功/失败验证。

### 回滚路径

- 脚本回滚：恢复 `EnterStage2(Collider2D)` 的旧 tag 判断，或按 Inspector 绑定回滚到旧入口；删除 `Level70BallItemCtrl` 中 `SortingGroup`、`openSortingOrder`、默认 order 缓存、`SetOpenSortingOrder()` 和 `RestoreSortingOrder()`。
- 绑定回滚：如 Unity 中已改绑定，优先恢复 `Player -> RestoreActorControl`、`tools -> OpenBallItemByTools`、`lock -> EnterStage2` 的明确绑定矩阵。
- 表现回滚：若仅排序提升不符合视觉，可保留 ball 打开和 itemJumpOut，只把 `openSortingOrder` 调回 prefab 原始 order 或移除排序设置。

## 2026-07-01 v2.2 PlayWallCrushAnim API 风险评估

### 原始行为基线

- 修改前 `EnterCompletePhase()` 内直接调用私有 `PlayWallCrushAnim()`，调用链只来自 `EnterStage2(Collider2D)` 通过 `lock` 触发进入 `Complete` 阶段。
- 原 `PlayWallCrushAnim()` 会直接隐藏 `crushWall1`、显示 `crushWall2`，再创建未追踪的 DOTween sequence，使 `crushWall2` 在 0.5 秒内淡出并 localX 移动到 -1，完成后隐藏 `crushWall2`。
- 原方法没有空引用保护、重复播放保护、sequence 生命周期清理，也没有在重开、失败、成功或退出时恢复 `crushWall2` 的 alpha、位置和显隐。

### 变更入口到影响链路

- Git 入口：未提交 diff 触达 `Level70Ctrl.cs` 和 Level70 本地文档；未修改 `Level_070.prefab`、共享 `OnTrigger2DHandle`、共享输入、UI、save/config/resource/SDK 文件。
- 回调链：`EnterStage2(Collider2D)` 通过 `EnterCompletePhase()` 调用无参 `PlayWallCrushAnim()`；新增 `PlayWallCrushAnim(Collider2D)` 可供 `OnTrigger2DHandle.OnEnter(Collider2D)` 直接绑定，但当前未保存 prefab 绑定修改。
- 状态链：墙体表现使用独立 `wallCrushAnimPlayed` 防止同一轮重复播放；`ResetHookRuntime(...)` 在 PlayMode 清理时重置该标记和墙体初始状态。
- 资源链：表现依赖 `crushWall1` 与 `crushWall2` 两个 `SpriteRenderer` 序列化引用；缺失时跳过表现并用 `Bear.Logger` warning，不阻断 stage2。
- Tween 链：新增 `wallCrushSequence` 追踪 sequence，并在重置时 kill sequence、`crushWall2` renderer tween 和 transform tween，降低退出/重开后的回调残留风险。

### 风险项

- **P1：Prefab 未绑定墙体 SpriteRenderer 会导致表现缺失**。脚本会跳过动画并继续 stage2，不会软锁，但玩家看不到墙体破碎；需要 Inspector/PlayMode 确认 `crushWall1` 和 `crushWall2` 绑定。
- **P1：直接绑定 `PlayWallCrushAnim(Collider2D)` 的触发器语义风险**。该重载只接受 `lock` tag；若误绑到 Player/tools 触发器将被忽略。若设计希望非 lock 也触发，需要明确改 tag 策略。
- **P1：墙体初始状态缓存依赖首次运行时状态**。脚本缓存 `crushWall2` 初始 localPosition、color 和两个墙体 active 状态；如果其它脚本在 Awake 之后、stage2 之前改墙体状态，重置可能恢复到缓存状态而非最新状态。
- **P1：DOTween 清理路径需要 PlayMode 验证**。虽然新增了 `KillWallCrushAnim(false)`，但退出、重开、失败、成功中断动画时的最终帧仍需实际运行确认。
- **OBS：`PlayWallCrushAnim()` 公开后 Inspector 可见性提高**。公开 API 便于 UnityEvent 绑定，但也增加误绑概率；当前用 `Application.isPlaying`、重复播放标记和 lock tag 重载过滤降低影响。

### 已缓解风险

- 入口外层检查 `Application.isPlaying`，避免编辑器态 UnityEvent 或非运行时路径触发运行时表现。
- `TryPrepareWallCrushAnim()` 对 `crushWall1` / `crushWall2` 做空引用检查，缺失时只输出 warning 并返回。
- 播放前恢复 `crushWall2` 初始位置和颜色，避免重复进入或上次中断留下 alpha/位置残留后再播放。
- `ResetHookRuntime(...)` 只在 PlayMode 调用墙体重置，避免编辑器禁用对象时修改序列化对象显隐、颜色或位置。
- 本次未新增全局事件、保存、配置、资源加载、analytics、广告、IAP 或平台 SDK 链路。

### 验证证据

- 静态代码扫描确认 `Level70Ctrl.cs` 存在 `PlayWallCrushAnim()`、`PlayWallCrushAnim(Collider2D)`、`wallCrushSequence`、`ResetWallCrushAnim()`、`KillWallCrushAnim(...)` 和 `TryPrepareWallCrushAnim()`。
- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs` 通过，仅有工作区 LF/CRLF 转换提示。
- 风险词静态扫描未发现本次新增 Level70 墙体 API 包含 DB、Save、RemoteConfig、PlayerPrefs、StreamingAssets、OnValidate、ExecuteAlways、ExecuteInEditMode、Instantiate、PlayEvents 或 Debug.Log 链路。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 error / 254 warnings；warnings 属于当前工程输出，未逐项审计。

### 未验证缺口

- 未运行 Unity PlayMode：stage2 进入、墙体破碎播放、重复触发忽略、中断重置、重开/失败/成功/退出恢复均未验证。
- 未在 Inspector 中确认 `crushWall1` / `crushWall2` 引用、初始 active 状态、`crushWall2` 初始 alpha 与 localPosition。
- 未验证将 `PlayWallCrushAnim(Collider2D)` 直接绑定到 `OnTrigger2DHandle` 的 Inspector 可选性和 lock tag 过滤行为。
- 未运行 Android/iOS 横屏 smoke；本次无平台分支，但 Level70 stage2 流程仍需移动端验证。

### 回滚路径

- 脚本回滚：删除 `PlayWallCrushAnim(Collider2D)`、`wallCrushSequence`、墙体初始状态缓存字段、`ResetWallCrushAnim()`、`KillWallCrushAnim(...)` 和 `TryPrepareWallCrushAnim()`。
- 行为回滚：把 `PlayWallCrushAnim()` 恢复为原私有简单实现，并保留 `EnterCompletePhase()` 对它的调用。
- 绑定回滚：如果 Unity 中已额外绑定 `PlayWallCrushAnim(Collider2D)` 后出现异常，从对应 `OnTrigger2DHandle` 移除该绑定即可；`EnterStage2 -> EnterCompletePhase` 原路径仍可保留。

## 2026-07-01 v2.2 catchArea tag 白名单风险评估

### 原始行为基线

- 修改前 `IsHookContactCollider(Collider2D)` 排除 `catchArea` 自身和抓钩层级内 collider 后，会把任意外部 active collider 当作有效接触。
- `OnCatchAreaEnter(...)` 和 `UpdateCatchAreaOverlap()` 都会通过该方法判断接触；一旦返回 true，就调用 `RecoverAfterContact()`，中断下落并回 `dropPoint`。

### 变更入口到影响链路

- Git 入口：未提交 diff 触达 `Level70Ctrl.cs` 和 Level70 本地文档；未修改 prefab、共享 `OnTrigger2DHandle`、共享输入、UI、save/config/resource/SDK 文件。
- 运行时入口：Catch 下落阶段的 UnityEvent 接触和 overlap 轮询都会经过 `IsHookContactCollider(...)`。
- 状态链：非白名单 tag 不会触发 `RecoverAfterContact()`，抓钩会继续下落直到碰到白名单 collider 或到达下边界走 miss 收束。
- 资源链：依赖现有 collider tag 配置；当前静态 prefab 文本可见 `Player -> RestoreActorControl`、`tools -> OpenBallItemByTools`、`lock -> EnterStage2` 绑定，但未逐个确认实际参与 `catchArea` 接触的 collider tag。

### 风险项

- **P1：实际接触 collider tag 与预期不一致**。如果 ball、工具或 lock 的子碰撞体没有直接配置 `tools` / `lock` / `Player` tag，新的白名单会忽略接触，导致抓钩继续下落到下边界。
- **P1：背景/边界不再提前收束可能改变节奏**。这是本次目标行为，但如果关卡设计曾依赖某个非白名单 trigger 让抓钩提前回收，需要重新给该 collider 配置允许 tag 或新增明确入口。
- **OBS：`ItemTag = triggerItem` 仍只用于旧目标解析兜底**。当前 catchArea 收束不再接受 `triggerItem` tag；如果后续恢复 v1 抓取目标解析，需要重新评估该 tag 是否进入白名单。

### 已缓解风险

- 白名单逻辑集中在本地 `IsCatchAreaTriggerTag(...)`，不修改共享 `OnTrigger2DHandle` 或物理层配置。
- 仍保留原有自身/抓钩层级排除，避免抓钩内部 collider 自触发。
- 本次未新增全局事件、保存、配置、资源加载、analytics、广告、IAP 或平台 SDK 链路。

### 验证证据

- 静态代码扫描确认 `IsHookContactCollider(...)` 在层级排除前先调用 `IsCatchAreaTriggerTag(...)`，白名单为 `LockTag`、`ToolsTag`、`ActorTag`。
- 空白检查：`git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs Assets/Game/Scripts/Level/LevelItem/Level_70/Level70抓钩控制功能说明.md Assets/Game/Scripts/Level/LevelItem/Level_70/Level70抓钩控制风险评估.md DevelopMD/已有功能设计与使用记录.md` 通过，仅有工作区 LF/CRLF 转换提示。
- 风险词静态扫描未发现本次新增 Level70 逻辑包含 DB、Save、RemoteConfig、PlayerPrefs、StreamingAssets、OnValidate、ExecuteAlways、ExecuteInEditMode、Instantiate、PlayEvents 或 Debug.Log 链路。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 未通过，错误为 `Assets/Game/Scripts/Play/PlayEvents.cs(173,18): CS0101`，`Game.Events` 已包含 `PostcardGetStarEvent` 定义；静态扫描显示同文件第 123 行和第 173 行重复定义。

### 未验证缺口

- 未运行 Unity PlayMode：`lock`、`tools`、`Player` 能触发收束，其它 tag 不能触发，均待验证。
- 未在 Unity Inspector 中确认参与 `catchArea` 接触的具体 collider tag，尤其是 ball/tools 子碰撞体。
- 未运行 Android/iOS 横屏触摸 smoke。
- `DevelopMD/已有功能设计与使用记录.md` 当前为 Git `UU` 未合并状态；本次只追加本地事实记录，未处理该无关合并状态。

### 回滚路径

- 脚本回滚：删除 `IsCatchAreaTriggerTag(...)` 并移除 `IsHookContactCollider(...)` 中的 tag 白名单判断。
- 配置回滚：如果只是一两个 collider tag 缺失，优先在 Unity Inspector 中补 tag，而不是恢复任意 collider 触发。

## 2026-07-01 v2.3 Level70Ctrl 无效 API 清理风险评估

### 原始行为基线

- v2 之后，`OnCatchAreaEnter(...)` 不再调用旧的目标抓取、吸附、搬运和释放流程，只用接触 collider 做合法性过滤，然后执行抓钩自身回收。
- 旧目标搬运相关类型和方法仍残留在 `Level70Ctrl.cs` 中，但当前没有任何调用方会设置 `currentTarget` 或执行 `PauseTargetPhysics(...)` / `AttachTargetToCatch(...)`。
- `Level_070.prefab` 静态绑定只包含 `RestoreActorControl(Collider2D)`、`OpenBallItemByTools(Collider2D)`、`EnterStage2(Collider2D)`；没有绑定 `OnCatchAreaEnter`、无参入口或墙体破碎公开入口。

### 变更入口到影响链路

- Git 入口：修改 `Level70Ctrl.cs` 和 `Level_070.prefab`，并追加 Level70 本地文档；不修改共享脚本和全局系统。
- 运行时入口：Catch 下落仍由 `UpdateCatchAreaOverlap()` 轮询，命中后调用私有 `HandleCatchAreaContact(Collider2D)`；stage2、Player 恢复和 tools 打开 ball 的三个外部入口保持不变。
- 序列化链：`catchAreaPosition` 和 `releaseRecoverDelay` 字段从脚本移除后，同步删除 `Level_070.prefab` 中对应无效字段，降低 Unity Inspector 残留数据噪声。
- 状态链：`HookControlState` 只保留实际使用的 `Idle`、`Lowering`、`Recovering`；回收过程不再经过旧的 `Carrying` / `Retracting` / `MovingToDrop` / `Releasing` 名义状态。

### 风险项

- **P1：UnityEvent 误绑定旧方法会在 Inspector 中丢失目标**。当前静态扫描未发现 `Level_070.prefab` 绑定旧方法，但如果其它未扫描资源或运行时脚本反射调用旧公开入口，会因方法删除而失败；本次范围内未发现证据。
- **P1：Prefab 已有脏改扩大差异认知成本**。`Level_070.prefab` 本次前已存在层级、位置和参数差异；本次只新增删除两个无效字段，最终提交前需要区分本次清理与既有 prefab 变更。
- **P1：旧目标搬运功能恢复成本提高**。删除死代码后，如果设计重新要求抓取并搬运目标，需要重新实现并验证目标物理、parent、actor 锁定和 ball open/close 流程。
- **P2：墙体破碎入口收回为私有后不能直接绑定 UnityEvent**。当前 prefab 未绑定该入口，stage2 仍通过 `EnterCompletePhase()` 内部触发；若后续想从额外 trigger 直接播放墙体破碎，需要重新提供明确入口。

### 已缓解风险

- 清理前后均未修改共享 `OnTrigger2DHandle`、输入事件、`ActorCtrl`、`BaseLevelCtrl`、UI、存档、配置、资源加载或平台 SDK。
- 仍保留实际被 prefab 或运行时使用的入口：`EnterHookControl(Collider2D)`、`EnterStage2(Collider2D)`、`OpenBallItemByTools(Collider2D)`、`RestoreActorControl(Collider2D)`。
- catchArea 过滤、回收到 `dropPoint`、墙体破碎内部调用和 ball 初始化路径仍保留。
- 已删除脚本中与现行 v2 行为冲突的注释和字段，避免后续误以为接触后仍会吸附/搬运目标。

### 验证证据

- 静态绑定扫描确认 `Level_070.prefab` 当前只绑定 `RestoreActorControl`、`OpenBallItemByTools`、`EnterStage2` 三个 `Level70Ctrl` 方法。
- 静态符号扫描未发现旧目标搬运类型、缓存、方法和已移除公开入口残留。
- 风险词静态扫描未发现本次 Level70 脚本变更命中 DB、Save、RemoteConfig、PlayerPrefs、StreamingAssets、OnValidate、ExecuteAlways、ExecuteInEditMode、Instantiate、PlayEvents 或 Debug.Log。
- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs Assets/Game/Prefabs/Level/Level_070.prefab` 通过，仅有工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 首次重跑 124 秒超时，未返回编译错误；随后以 300 秒超时补跑通过，结果为 0 warning / 0 error。

### 未验证缺口

- 未运行 Unity Editor 编译、资源导入和 PlayMode。
- 未在 Inspector 中逐项确认所有 Level70 Trigger 的 UnityEvent 绑定仍为当前保留入口。
- 未验证 Android/iOS 横屏触摸 smoke。
- 未处理 `Level_070.prefab` 中本次前已存在的其它脏改。

### 回滚路径

- 脚本回滚：恢复旧 `HookTargetRuntime`、`HookTargetKind`、目标解析、物理暂停、parent 挂接、释放和清理方法，以及旧公开入口。
- Prefab 回滚：恢复 `catchAreaPosition` 和 `releaseRecoverDelay` 两个序列化字段。
- 行为回滚：如果只需要恢复直接 UnityEvent 绑定墙体表现，应优先重新新增受 tag 限定的公开入口，而不是整体恢复旧目标搬运死代码。

## 2026-07-01 ModifyActorCollider 最小完善风险评估

### 原始行为基线

- `RestoreActorControl(Collider2D)` 是 Level70 Player/actor 普通控制恢复入口，会先校验触发 collider，再调用私有 `RestoreActorControl()`。
- 修改前私有 `RestoreActorControl()` 不返回恢复结果；即使 `Level70ActorCtrl.RestoreLevel70NormalControl()` 因 `controlhandle` 状态返回失败，public 入口仍会继续执行 `ModifyActorCollider()`。
- 修改前 `ModifyActorCollider()` 直接调用 `actor.GetComponent<Collider2D>().excludeLayers = 1 << LayerMask.NameToLayer("Stuff")` 和 `actor.SwitchWhatIsStuff(default)`，没有 actor、Collider2D 或 `Stuff` layer 防护。

### 变更入口到影响链路

- Git 入口：修改 `Level70Ctrl.cs`、为 `ActorCtrl.SwitchWhatIsStuff(...)` 补注释，并追加本地文档；不修改 prefab 或共享运行时逻辑。
- 回调链：`OnTrigger2DHandle.OnEnter(Collider2D)` 绑定到 `RestoreActorControl(Collider2D)` 后，仍先走 actor collider 过滤；恢复成功才进入 `ModifyActorCollider()`。
- 状态链：当 actor 正在 `controlhandle` 且拒绝恢复时，Level70 不再修改 Actor collider 排除层，也不关闭 Actor 的推物检测层。
- 物理链：恢复成功后仍沿用既有目标，把 Actor 根 collider 排除 `Stuff` 层，并把 `whatIsStuff` 切为空 LayerMask。

### 风险项

- **P1：推物检测关闭时机依赖恢复成功**。如果关卡设计希望即使 `controlhandle` 未结束也提前关闭 `whatIsStuff`，本次会延后到恢复成功后；当前本地文档约束显示 controlhandle 期间应拒绝恢复，因此这是符合现有恢复边界的取舍。
- **P1：Actor collider 位于非根节点时会跳过修改**。当前实现仍用 `actor.GetComponent<Collider2D>()`，没有扩大查找范围；这是为了最小改动并保持原根节点 collider 语义，需 Inspector/PlayMode 确认 Level70 actor 根节点 collider 绑定。
- **P2：`SwitchWhatIsStuff(default)` 无自动恢复**。本次未新增恢复原始 `whatIsStuff` 的缓存；如果后续关卡需要恢复推物检测，需要另行设计生命周期和回滚点。

### 已缓解风险

- `ModifyActorCollider()` 对 actor、Actor collider 和 `Stuff` layer 缺失都只记录 Bear.Logger warning 并返回，避免恢复触发器造成空引用中断。
- 私有 `RestoreActorControl()` 现在返回 `bool`，调用方只在 actor 恢复成功后修改 collider，避免恢复被拒绝时仍改变物理配置。
- 本次没有新增全局事件、存档、配置、资源加载、analytics、广告、IAP、平台 SDK 或编辑器生命周期入口。

### 验证证据

- 静态 diff 确认 `Level70Ctrl.cs` 只新增恢复结果返回值判断和 `ModifyActorCollider()` 防护；`ActorCtrl.cs` 只补充 `SwitchWhatIsStuff(...)` 注释。
- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs` 已通过，仅有工作区 LF/CRLF 转换提示。
- 风险词静态扫描未发现本次 `Level70Ctrl.cs` 新增 `OnValidate`、`Reset` 运行时入口、`ExecuteAlways`、`ExecuteInEditMode`、`DB`、`Save`、`RemoteConfig`、`PlayerPrefs`、`StreamingAssets`、`Resources`、`Instantiate`、`PlayEvents` 或 `Debug.Log` 链路。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 首次 124 秒超时，已停止该孤立构建进程；随后以 300 秒超时补跑通过，结果为 0 error / 144 warnings。

### 未验证缺口

- 未运行 Unity PlayMode，仍需验证恢复触发器在 controlhandle 期间不会改 collider，动画结束后恢复成功才改 collider。
- 未在 Inspector 中确认 Level70 actor 的根节点 `Collider2D`、`Stuff` layer 和 Player 恢复触发器绑定。
- 未运行 Android/iOS 横屏触摸 smoke。

### 回滚路径

- 脚本回滚：把私有 `RestoreActorControl()` 改回 `void`，public 入口恢复为无条件 `RestoreActorControl(); ModifyActorCollider();`，并删除 `ModifyActorCollider()` 中 actor、Collider2D、`Stuff` layer 的防护分支。
- 注释回滚：如需完全回到修改前文本，可删除 `ActorCtrl.SwitchWhatIsStuff(...)` 的 XML 注释；删除注释不影响运行时行为。

## 2026-07-21 Level70BallItemCtrl 打开后冻结风险评估

### 原始行为基线

- `OpenAndPlayJumpOut()` 给 Dynamic `Rigidbody2D` 施加水平冲量后固定等待 `ballOpenDelay`；`Level_070.prefab` 的 9 个 ball 均配置为 2 秒，再调用 `SetOpenState()`。
- `SetOpenState()` 修改 close/open 节点和排序层，但没有清零刚体速度或限制物理运动；因此球打开后仍可能受残余速度、重力和碰撞影响。
- `InitializeContent()` 与 `SetClosedState()` 会恢复视觉、collider 和排序层，但修改前不管理刚体约束。

### 变更入口与影响链路

- Git 入口：修改 `Level70BallItemCtrl.cs`、`Level_070.prefab`，并更新 Level70 本地文档和共享设计记录；不修改共享关卡系统。
- 调用链：`Level70Ctrl.OpenBallItemByTools(Collider2D)` → `Level70BallItemCtrl.OpenAndPlayJumpOut()` → `GetRandomOpenDelay()` 取样 → 等待 → `SetOpenState()` → `FreezeOpenedRigidbody()`。
- 序列化链：脚本字段由固定 float 改为 `Vector2 ballOpenDelayRange`；`Level_070.prefab` 的 9 个实例显式写入 `(1.5, 3)`，不依赖未保存的脚本默认值。
- 物理链：打开时清零线速度/角速度并设置 `FreezeAll`；初始化或关闭时通过缓存值恢复原始 `Rigidbody2D.constraints`。
- 既有系统：继续使用 Unity `Rigidbody2D`、原 Item 弹出执行器、SortingGroup 和 Level70 本地触发入口；没有新增全局事件、存档、配置或资源加载依赖。

### 风险项

- **P1：打开态 collider 仍启用**。冻结会保证球自身不移动，但 collider 仍可能参与接触并影响其它动态物体；这是既有 collider 行为，本次未扩大到碰撞语义调整，需 PlayMode 验证关卡是否出现阻挡。
- **P1：重开/复用恢复依赖初始约束缓存**。实现缓存首次解析到的约束并在初始化/关闭时恢复；若其它运行时代码有意在首次打开前动态改写约束，恢复值不会跟随该后续改写。当前 CodeGraph 证据未发现其它 `Level70BallItemCtrl` 刚体约束写入方。
- **P1：等待期间重复触发仍可能并发**。`hasOpened` 仍在延迟结束进入 `SetOpenState()` 时才置为 true；如果同一个 ball 在等待期间收到多次打开请求，多个随机等待可能并发并重复触发 Item 弹出。该并发窗口是既有实现问题，随机延迟会让完成时刻更分散，需 PlayMode 验证 tools Trigger 是否只触发一次。
- **P2：打开前运动时长改为随机**。水平冲量保持不变，但球会运动 1.5～3 秒后才冻结；这是用户明确指定的节奏变化，需要确认最长 3 秒不会让球越过预期区域。
- **P2：使用 Unity 全局随机状态**。每次合法打开请求会调用一次 `UnityEngine.Random.Range`；没有新增实例化或持续循环，但会推进 Unity 的全局随机序列。当前没有证据表明 Level70 依赖固定随机序列。

### 已缓解风险

- 同时清零线速度和角速度，避免只加约束后保留内部速度状态。
- 使用实例原始约束作为恢复值，不把所有关闭态强制写死为 `None`，保留 prefab 后续配置空间。
- 冻结入口位于 `SetOpenState()`，其它合法打开调用也得到相同行为；编辑器非 PlayMode 下不会改写刚体约束。
- 延迟区间运行时会自动排序 X/Y，并把负值收敛到 0；当前 prefab 显式配置合法的 1.5～3 秒。
- 本次只修改 Level70 专用 prefab，不修改共享物理层、共享 UI、DB/save、配置、资源加载、analytics 或平台 SDK。

### 验证证据

- CodeGraph 复核确认 `FreezeOpenedRigidbody()` 只有 `SetOpenState()` 一个调用方，`RestoreRigidbodyConstraints()` 由 `InitializeContent()` 和 `SetClosedState()` 调用；`Level70BallItemCtrl` 的外部调用方仍局限于 `Level70Ctrl`。
- `Level_070.prefab` 静态扫描确认 9 个 ball 仍为 Dynamic `Rigidbody2D`、`constraints = 0`，并全部配置 `ballOpenDelayRange = (1.5, 3)`；Assets 内 prefab/scene 无旧 `ballOpenDelay:` 残留。
- `git diff --check` 通过；敏感生命周期、存档、资源、事件和直接 Unity 日志静态扫描无新增命中。
- 标准 `dotnet build .\Assembly-CSharp.csproj ...` 通过，结果为 0 error / 1117 warnings；首次 `--no-restore` 失败仅因临时 NuGet 资产文件缺失，标准 restore/build 已补齐验证。
- 随机延迟调整后的 `dotnet build .\Assembly-CSharp.csproj --no-restore ...` 再次通过，结果为 0 error / 1117 warnings。

### 未验证缺口与回滚路径

- 未运行 Unity Editor PlayMode，尚未直接测量多次打开等待是否都落在 1.5～3 秒，也未验证等待期重复触发、最长运动距离、打开冻结、Item 弹出和重开恢复。
- 未运行 Android/iOS 横屏 smoke；本次无平台条件分支，但移动端物理帧表现仍待验证。
- 待确认：用户所说“打开之后”按当前实现解释为 `SetOpenState()` 执行后；若期望触发方法起点即停止，需要把冻结入口前移到延迟之前。
- 随机延迟可单独回滚：恢复固定 float 字段与等待调用，并把 prefab 的 9 个区间字段恢复为 `ballOpenDelay: 2`；无需修改存档、配置或资源地址。

## 2026-07-21 dropArea SpriteRenderer Sorting Order 风险评估

### 原始行为基线与直接原因

- dropArea 触发后，球根 SortingGroup 只在随机等待结束、`SetOpenState()` 执行时才写 180；等待期间保持初始值。
- 内部 Item 的 50 是 `SpriteRenderer.sortingOrder`。旧实现查找 `jumpOut` 子级 SortingGroup 并写 200，但 Item 节点没有该组件，因此目标类型错误，无法改变 50，并存在空引用异常风险。

### 变更链路

- 入口链：tools/dropArea → `Level70Ctrl.OpenBallItemByTools(Collider2D)` → `Level70BallItemCtrl.OpenAndPlayJumpOut()`。
- 球壳链：入口开始 → `ResolveReferences()` → 缓存 close/open 子树 SpriteRenderer → `SetBallPartSortingOrder(180)` → 水平冲量与随机等待。
- Item 链：随机等待结束 → `SetOpenState()` → Item 改父节点 → 已解析 Item `SpriteRenderer.sortingOrder = 200` → `ItemJumpOutExecutor.Play()`。
- 恢复链：`InitializeContent()` / `SetClosedState()` 恢复球壳各自原始 order 与 Item 原始 order。

### 风险项与已缓解风险

- **P1：根 SortingGroup 仍可能覆盖组外整体排序**。需求明确修改 SpriteRenderer Sorting in Layer，本次没有删除或禁用 prefab 中实际 SortingGroup；如果目标是改变整个球相对其它组的层级，仅改子 Renderer 可能不足，必须通过 PlayMode 遮挡结果确认。
- **P1：3 个 ball 的 sprite 序列化引用为空**。已改为优先从 `jumpOut` 同节点/子节点解析 Item Renderer，再使用 ball 子树兜底；仍需 Inspector 验证这 3 个实例的 jumpOut 指向正确节点。
- **P2：球壳 Renderer 集合按首次解析缓存**。运行时若动态新增 close/open 子 Renderer，它不会自动加入缓存；当前 Level70 静态层级没有运行时新增球壳 Renderer 的证据。
- **已缓解：Item 目标类型明确**。直接写 `SpriteRenderer.sortingOrder`，不再运行时查找不存在的 SortingGroup；初始化/关闭会恢复 50，避免重开残留 200。
- **已缓解：无逐帧成本**。Renderer 数组只在首次解析时创建，排序只在离散状态入口遍历；当前 11 个 ball 的局部子 Renderer 数量有限。

### 验证证据与未验证缺口

- 静态扫描确认脚本已无 `UnityEngine.Rendering`、`SortingGroup` 字段、`GetComponentInChildren<SortingGroup>()` 或 prefab `sortingGroup:` 序列化残留；当前 11 个组件均配置 open order 180 和 1.5～3 秒延迟。
- 脚本 `git diff --check` 通过；prefab 整体检查因其它并行改动的空值行尾空格未通过，本次未清理无关差异。
- C# 构建通过，结果为 0 error / 99 warnings。
- 未运行 PlayMode、Unity Console 运行时异常检查或 Android/iOS 横屏 smoke；180/200 Inspector 值、实际遮挡、3 个空 sprite 兜底和重开恢复均待验证。

### 回滚路径

- 脚本回滚 SpriteRenderer 缓存、立即写 180、Item 直接写 200 和 order 恢复逻辑，并恢复旧 SortingGroup 字段/方法。
- prefab 只需恢复 `Level70BallItemCtrl.sortingGroup` 空字段；不涉及存档、配置、资源地址、事件或平台数据回滚。

## 2026-07-21 ball SortingGroup / Item SpriteRenderer 分层修正风险评估

### 原始行为基线与变更链路

- 修正前的临时实现通过遍历 ball 子树 SpriteRenderer 写 180，未修改 ball 根 SortingGroup；这不能满足最新明确的组件边界。
- 修正后链路为：tools/dropArea → `OpenAndPlayJumpOut()` → ball 根 `SortingGroup.sortingOrder = 180` → 随机等待 → `SetOpenState()` → Item 脱离 ball → Item `SpriteRenderer.sortingOrder = 200` → `ItemJumpOutExecutor.Play()`。
- 初始化/关闭入口会把两类组件各自恢复到首次解析到的初始 order，避免重开残留 180/200。

### 风险项与缓解

- **P1：Item 改父节点后才脱离 ball SortingGroup。** 当前顺序沿用既有流程，并在脱离后写 SpriteRenderer 200；需 PlayMode 确认改父节点目标始终在 ball 根 SortingGroup 之外。
- **P1：3 个 ball 的 `sprite` 序列化引用为空。** 脚本继续从各自 `jumpOut` 同节点或子节点解析 Item SpriteRenderer；需运行时确认解析目标。
- **P2：同层同 order 的遮挡不确定性已避免。** ball 根使用 180，Item 使用 200，Item 脱离后有明确更高 order。
- **已缓解：prefab 显式绑定。** 静态映射确认 11/11 ball 根节点存在 SortingGroup，且 `Level70BallItemCtrl.sortingGroup` 全部指向对应根组件，不依赖跨层级查找。
- 本次无逐帧新增分配、无全局事件、无存档/配置/资源加载、无编辑器生命周期入口。

### 验证证据、未验证缺口与回滚

- C# 构建通过（0 error / 99 warnings）；脚本差异格式检查通过；YAML 映射结果为 `BallCount=11`、`MissingRootSortingGroup=0`。
- 未运行 Unity PlayMode、Unity Console 异常检查或 Android/iOS 横屏 smoke；实际遮挡、运行时 180/200、空 sprite 兜底和重开恢复待验证。
- 回滚时恢复上一版子 SpriteRenderer 遍历，并移除 11 个组件上的显式 `sortingGroup` 与 `openItemSortingOrder`；冻结和 1.5～3 秒随机延迟可独立保留。

## 2026-07-21 catch 控制职责拆分风险评估

### 原始行为基线与影响范围

- 拆分前所有 catch 字段、运行时状态、Tween、Update 轮询和重叠检测均在 `Level70Ctrl`；同一脚本还负责 Bear 输入事件、角色/UI、阶段墙体和 ball 初始化。
- 本次只在 Level70 本关内新增 `Level70HookCtrl`，没有提取共享基类或跨关卡系统；prefab 原 catch 引用和参数原样迁移，玩家可见行为目标保持一致。
- 受影响文件为 `Level70Ctrl.cs`、新增 `Level70HookCtrl.cs/.meta`、`Level_070.prefab` 及本地说明文档；ball 排序/冻结代码不在本次拆分范围内。

### 风险项与缓解

- **P1：MonoBehaviour Awake/OnDisable 顺序变化。** 新组件和 `Level70Ctrl` 位于同一根对象，Unity 不保证同对象不同脚本的 Awake 顺序。两个入口都通过可重复的 `ResolveReferences()` / `CacheInitialState()` / `ResetRuntime()` 自保护；初始位置只缓存一次，重复清理不会重设缓存基线。
- **P1：组件缺失会导致角色被锁但 catch 不可用。** `EnterHookControl()` 现在在锁角色和切换 UI 前校验 `hookCtrl`；缺失时使用 Bear.Logger warning 并退出。prefab 已显式绑定，静态检查为 1/1。
- **P1：事件与机械状态跨组件时序。** Bear 事件仍只由 `Level70Ctrl` 订阅，catch 组件不新增订阅者；每个回调同步调用局部 API，避免重复订阅和解绑顺序风险。
- **P1：清理重复调用。** `Level70Ctrl` 与 `Level70HookCtrl.OnDisable/OnDestroy` 都可能调用 catch reset；实现先 Kill Tween、清空状态、关闭 Animator 并恢复同一初始位置，设计为幂等。仍需 PlayMode 验证卸载时 DOTween 无异常。
- **P2：Update 执行者变化。** 水平移动后再做 overlap 的顺序在新组件内部保持不变，但其相对其它 MonoBehaviour 的 Update 顺序未显式固定；当前旧实现也没有 Script Execution Order 约束，需实际关卡验证接触边界。
- **P2：旧 `missRecoverDuration` 仍未使用。** 为保持 prefab 序列化参数完整，该字段迁移到新组件但 v2 接触和 miss 继续共用 `retractDuration`；没有借拆分改变节奏。
- **已缓解：局部直接引用。** 未新增全局事件、单例、运行时 AddComponent、共享 GamePlayPanel 修改或跨关卡抽象，回归范围限制在 Level70。

### 验证证据与未验证缺口

- CodeGraph 在修改前确认 catch 私有方法调用方均位于 `Level70Ctrl`，没有外部脚本依赖；三个 prefab UnityEvent 入口继续指向 `Level70Ctrl`。
- C# 构建通过（0 error / 99 warnings）；静态扫描确认 `Level70Ctrl` 不再保留 catch 状态/Tween/范围字段，新组件/根列表/控制器引用各 1 个。
- 未运行 Unity PlayMode、Unity Console 异常检查或 Android/iOS 横屏 smoke；输入手感、边界接触、Tween 中断、stage2、恢复、重开和退出仍待验证。

### 回滚路径

- 删除 `Level70HookCtrl.cs/.meta` 和 prefab 根组件，把序列化字段及机械方法恢复到 `Level70Ctrl`，输入回调恢复直接读写旧状态。
- prefab 参数值可以从当前新组件块原样迁回；不需要处理存档、配置、资源地址、事件数据或平台数据。

## 2026-07-21 catch 加速移动与左侧破墙风险评估

### 原始行为基线、影响文件和现有系统

- 基线为固定速度水平移动，`Playing` 使用 range1，`lock -> EnterStage2` 进入 `Complete` 后使用 range2 并播放墙体破碎。
- 受影响文件为 `Level70HookCtrl.cs`、`Level70Ctrl.cs`、`Level_070.prefab` 及本关中文说明/风险文档。
- 继续使用既有 Bear 输入事件订阅、Level70 局部组件直接引用和 DOTween；不新增 `PlayEvents`、全局单例、存档、配置或共享 UI 修改。

### 风险项与缓解

- **P1：默认加速度导致端到端时长变化。** 当前行程 13.47、最大速度 3，按公式推导的加速度约 0.334/秒²，从静止端到另一端约 8.98 秒。这满足“另一端恰好达最大速度”的数学约束，但手感需 PlayMode 调参；改 `maxHorizontalSpeed` 会同步重算加速度。
- **P1：离散帧进对“恰好”的误差。** 连续运动公式可保证理论值，但 `Update` 的 `deltaTime` 会带来小量越界；代码在越界帧夹紧到边界，碰撞速度又被 `maxHorizontalSpeed` 上限约束，不会超过配置最大值。
- **P1：右边界语义。** 按“只有最左边可以破碎”实现为右侧无论速度都播放 `CrashEffect_Right` 且不破墙；如果策划期望右侧高速有另一种 crash 表现，需要另外明确资源与行为。
- **P1：去除 Complete 改变进程门槛。** hook 现在进入控制后即可使用旧 range2 完整行程，`lock` 不再切阶段也不再自动破墙；这是明确行为变更，需确认关卡节奏。
- **P2：边界持续输入重复播放。** 左右独立锁使同一次持续顶墙只判定一次，离开边界后才重置；避免每帧创建 DOTween Sequence。
- **P2：失败效果 Tween 重入。** 同侧再次触发前会 Kill 旧 sequence 和对应 Transform/Renderer tween，然后重置 scaleX=0、alpha=1；重开/失败/成功/退出时统一隐藏。
- **P2：左墙破碎后的后续左撞。** 本轮破墙后 `hasBrokenLeftWall` 防止再次破碎，后续同侧撞击不再播失败效果；关卡重置后墙体与标记恢复。
- **已缓解：Inspector 引用和旧 UnityEvent。** prefab 显式绑定左右 `SpriteRenderer`、完整行程和两个控制器；旧 `EnterStage2` 绑定已清空，避免 UnityEvent 指向已删除方法。

### 验证证据与未验证缺口

- CodeGraph 修改前证据显示原阶段、范围和墙体链都局限于 Level70；新实现仍保持本关局部边界。
- `dotnet build Assembly-CSharp.csproj --no-restore -t:Rebuild -v:minimal /clp:ErrorsOnly` 强制重建通过：0 error / 1117 warnings。静态扫描确认旧阶段 API/字段和 prefab 方法绑定已移除，新参数与引用已显式保存。
- 未运行 Unity PlayMode、Unity Console 异常检查、输入帧率对比或 Android/iOS 横屏 smoke。需验证实际加速手感、速度阈值两侧分支、失败效果遮挡/方向、DOTween 清理、墙体和重开恢复。

### 回滚路径与待确认项

- 回滚脚本时恢复 `Level70Phase`、`EnterStage2(...)`、`EnterCompletePhase()` 与 range1/range2 切换，并将水平移动恢复为 `hookMoveSpeed * input * deltaTime`。
- 回滚 prefab 时恢复 `lock -> EnterStage2` UnityEvent 及旧 range 字段；不需要回滚存档、远端配置或平台数据。
- 待 PlayMode 确认：右侧高速撞击是否确定只播失败效果，以及左墙破碎后后续左撞是否需要其它视觉反馈。

## 2026-07-21 Level70 循环音效持有与销毁策略风险评估

### 原始基线与影响范围

- 基线是进入抓钩控制时直接循环播放 `l70PlayGame`，不持有 `Sound`；Level70Ctrl 原本在 GameReset、SUCCESS、FAILED、OnDisable、OnDestroy 中统一复位本关状态，HookCtrl 在禁用和销毁时也会复位。
- 本次影响 `Level70Ctrl.cs`、`Level70HookCtrl.cs`、Level70 本地文档与共享记录；用户新增的三个 ogg、`SoundCollection.asset`、`SFX_Generated.cs` 只做静态核对，没有改写。

### 风险项与缓解

- **P1：SoundsGood 音源独立于 Level GameObject。** 仅销毁 Level70 不足以证明循环音源自动停止；`OnDestroy` 保留定向 `Sound.Stop(0f)`，没有调用全局 `StopAllSound`，不会误停其它系统声音。
- **P1：成功/失败到销毁之间仍播放。** 按用户确认移除 SUCCESS/FAILED 停止逻辑后，循环音效会持续到正常恢复入口或 Level70 `OnDestroy`。若关卡结果表现不会立即销毁 Level，可能出现短暂残留，需 PlayMode 验证实际销毁时序。
- **P1：Disable 不清理。** 组件仅 Disable 但未 Destroy 时，循环音效和 hook 状态都会保留；这是明确的新生命周期语义。若项目存在对象池式禁用而非销毁，需要重新增加专门停止入口。
- **P2：重复进入音效叠加。** `EnterHookControl` 已通过 `IsControlActive` 阻止重复入口，音效启动方法还会先停止旧句柄，双重避免本组件产生两个 `l70PlayGame` 循环。
- **P2：SFX 开关关闭。** 项目 `AudioManager.PlaySound` 会返回 null；停止方法使用 null/`Using` 检查，可安全清空。
- **P2：actor 恢复被 controlhandle 动画拒绝。** 恢复失败时不停止音效，因为抓钩控制尚未真正退出；恢复成功后才停止，语义与控制权一致。
- **已缓解：本地初始化与旧实例清理解耦。** Awake 保留新实例初始化；GameReset/SUCCESS/FAILED/Disable/Destroy 不再回写旧实例视觉和物理状态，Destroy 只释放跨对象生命周期的音源句柄。

### 验证、缺口与回滚

- C# 增量构建通过：0 error / 99 warnings；静态检查确认三条 tag/clip、句柄启动停止边界和旧重置订阅移除。
- 未验证 Unity 实际销毁时序、结果界面期间声音、Disable/Enable、SFX 开关切换、快速恢复以及移动端切后台；这些仍是 PlayMode/设备验证缺口。
- 回滚音效持有时删除局部 `Sound` 字段和启动/停止方法；回滚生命周期策略时恢复 GameReset/SwitchGameState 订阅、Level70Ctrl CleanupRuntime 与 HookCtrl 禁用/销毁复位。

## 2026-07-21 catch 最低起步速度风险评估

### 基线与影响范围

- 修改前 hook 从 0 加速到最大速度 3；修改后活动方向输入从最低速度 1 起步，并按完整行程重新计算加速度。
- 影响范围仅为 `Level70HookCtrl.cs`、`Level_070.prefab` 和 Level70 本地/共享文档；抓取、墙体、失败效果、音效、ball、UI、存档与事件链未改变。

### 风险项与缓解

- **P1：起步速度不连续。** 从静止输入时速度直接变为最低值 1，这是需求规定的玩家可见手感变化；PlayMode 需要确认触控按下时不会显得突兀。
- **P1：松开停止存在最低速度门槛。** 速度减到最低值附近后直接归零，避免以低于配置最低值缓慢滑行，但会产生一次停止跳变。
- **P1：最低速度影响到达破墙阈值的距离。** 当前阈值为 2.4；最低速度 1 时，从行程一端持续加速约经过 59.5% 行程即可达到阈值速度。若将最低速度配置到 2.4 以上，短距离撞左墙也可能成功破碎。
- **P2：Inspector 最低值超过最大值。** 运行时通过 `Mathf.Clamp(min,0,max)` 保护；不会产生负加速度，但 Inspector 显示值本身不会被编辑器态回写。
- **P2：最低值等于最大值。** 加速度为 0，逻辑退化为按下即最大速度、松开即停止的恒速模式，避免除零或卡死。
- **P2：离散帧误差。** 15～120 fps 模拟端点速度约为最大值的 99.984%；边界仍夹紧位置，速度不会超过最大值，80% 阈值判断不受该误差影响。
- 本次没有新增逐帧分配、Tween、全局事件、实例化、存档、配置或平台分支；移动更新仍只有常数级数学运算。

### 验证缺口与回滚

- C# 构建通过：0 error / 99 warnings；静态字段和公式检查通过。
- Unity PlayMode、真实触控、快速左右切换、不同帧率和 Android/iOS 横屏尚未验证。
- 回滚时恢复 0 起步和 `a=max²/(2s)`，删除 prefab 的 `minHorizontalSpeed`；无需回滚资源、音效、存档或配置。

## 2026-07-21 catch 无输入硬停止风险评估

### 基线、风险与验证

- 基线是零输入后按加速度减速到最低速度门槛再归零；修改后 Cancel/0 输入在同一调用内立即归零，属于明确的手感变化。
- **P1：停止依赖输入层正确派发 Cancel。** Level70HookCtrl 已同时提供 `SetMoveInput(0)` 即时清零和 Update 零输入兜底，但如果输入层在按钮抬起后仍保留 ±1，Hook 仍会继续移动；需 PlayMode 覆盖触摸抬起与滑出按钮。
- **P1：停止表现为速度突变。** 从任意当前速度直接变为 0，不再有惯性；这是本次需求目标，但需要确认动画和音效没有依赖减速阶段。
- **P2：左右同时输入的事件顺序。** 当前行为由最后到达的方向/Cancel 事件决定，本次没有修改共享输入系统；双指同时操作结果需设备验证。
- 方向反转、最低速度起步、完整行程加速、左右边界锁与 80% 阈值保持不变；没有新增逐帧分配、事件类型或跨关卡修改。
- C# 构建通过：0 error / 99 warnings；Unity PlayMode、Android/iOS 横屏触控与暂停恢复尚未验证。
- 回滚时只恢复 Level70HookCtrl 的零输入减速分支，无需修改 prefab、资源、存档、配置或音效。

## 2026-07-21 左墙破碎前 range1Min 限制风险评估

### 原始基线与影响范围

- 原始问题是移除 `Complete` 状态后直接启用 `range2Min`，玩家无需破墙即可进入 `range1Min` 左侧区域。
- 本次只影响 `Level70HookCtrl.cs`、`Level70Ctrl.cs`、`Level_070.prefab` 及 Level70 本地/共享文档；没有修改共享输入、GamePlayPanel、全局事件、存档、远端配置或资源加载。

### 风险项与缓解

- **P0 已修正：左墙未破碎时提前越界。** 活动左边界在 `hasBrokenLeftWall=false` 时固定取 `range1Min`，只有成功启动左墙破碎表现才切到 `range2Min`。
- **P1：破墙表现准备失败却开放范围。** `PlayLeftWallCrash()` 现在返回是否真正开始播放；非 PlayMode、重复调用或关键引用缺失均返回 false，因此 Hook 保持在 `range1Min`。
- **P1：切换范围会改变加速度。** 破墙前按 8.65 的活动距离计算加速度，破墙后按 13.47 计算。Hook 在 `range1Min` 破墙后继续向左时只走完新完整范围的一部分，因此到 `range2Min` 时不保证恰好达到最大速度；墙已破碎后不再依赖左端高速判定，但手感仍需 PlayMode 确认。
- **P2：prefab 引用误绑或缺失。** 三个 Transform 已显式绑定；脚本保留 -4.5、-6.5、4.5 的 fallback 防止空引用，但 fallback 与当前 prefab 实际坐标不完全一致，只能作为容错，不能替代正确绑定。
- **P2：边界锁切换。** 破墙瞬间旧左边界锁为 true；活动边界改为 `range2Min` 后，Hook 位于 -3.98 而非 -8.8，后续帧会解除边界锁，不会永久阻止继续向左。
- 本次没有恢复关卡 Complete 状态；只增加局部 `hasBrokenLeftWall` 运行时门槛，避免重新引入旧阶段链和 UnityEvent 依赖。

### 验证、缺口与回滚

- C# 构建通过：0 error / 99 warnings；静态 prefab 引用和唯一调用方检查通过，相关代码 diff check 无空白错误。
- Unity PlayMode、实际 DOTween 破墙完成度、引用缺失保护、连续顶墙、低帧率跨边界和 Android/iOS 横屏尚未验证。
- 回滚时恢复固定 `range2Min` 左边界并把破墙接口恢复为无返回值；若问题复现，可只回滚这三个文件，不涉及存档、资源或共享系统。

## 2026-07-21 ball item 兼容 jumpOut SortingGroup 风险评估

### 基线与影响范围

- 基线行为只把内部 Item 的 `SpriteRenderer.sortingOrder` 设置为 200；用户要求在打开时兼容 `jumpOut` 同对象的 SortingGroup。
- 本次只修改 `Level70BallItemCtrl.cs` 与 Level70 本地/共享文档，没有编辑 `Level_070.prefab`，不影响其它关卡、共享 UI、输入、事件、资源、存档或配置。

### 风险项与缓解

- **P1：误改其它 SortingGroup。** 只调用 `jumpOut.GetComponent<SortingGroup>()`，不使用 `GetComponentInChildren`，不会把子节点或 ball 根排序组误认为 Item 排序组。
- **P1：SortingGroup 改变整体排序语义。** 一旦 prefab 后续在 jumpOut 同对象挂入 SortingGroup，200 会作为组的 Order in Layer 参与组外排序，同时 SpriteRenderer 仍为 200；最终遮挡需结合 Sorting Layer 和父级组关系在 PlayMode 验证。
- **P2：组件缺失。** 当前 prefab 的 11 个 jumpOut 均没有该组件；空引用分支直接跳过，保持现有 SpriteRenderer 200 行为。
- **P2：关闭后排序残留。** 首次解析组件时缓存其原始值，`InitializeContent()` 和 `SetClosedState()` 会恢复；没有增加 GameReset、成功、失败、禁用或销毁复位链。
- `GetComponent<SortingGroup>()` 只发生在现有引用解析/打开生命周期入口，不在 Update 中执行，不新增逐帧分配或持续扫描。

### 验证、缺口与回滚

- C# 构建通过：0 error / 99 warnings；静态脚本检查和 prefab 组件计数完成。
- Unity PlayMode、实际新增 SortingGroup 的变体 prefab、父级 SortingGroup 嵌套表现及 Android/iOS 横屏尚未验证。
- 回滚只需移除 jumpOut SortingGroup 的缓存、赋值和恢复逻辑；无需修改 prefab 或回滚 Item SpriteRenderer 的既有 200 行为。

## 2026-07-21 左墙 crash 成功震屏 Feedback 风险评估

### 基线与影响范围

- 基线是左墙高速撞击成功时播放墙体 SpriteRenderer Tween 和 `l70Crash` 音效，没有震屏 Feedback。
- 本次只修改 `Level70Ctrl.cs` 与 Level70 本地/共享文档；未修改 prefab、共享相机、Feedback 包、事件、存档、配置或资源。

### 风险项与缓解

- **P1：prefab 未绑定导致无震屏。** 当前 Level70 prefab 静态检查没有 MMF_Player，代码使用空安全调用保护主流程；这是明确的配置缺口，绑定完成前只能确认接入点，不能宣称实际震屏已验证。
- **P1：Feedback 目标或强度配置错误。** MMF_Player 内部仍需指定有效相机 Shaker/目标；目标指向关卡物体、强度过大或持续时间过长都可能造成画面位移、眩晕或结束后位置异常，必须在 PlayMode 调参。
- **P1：成功语义顺序。** Feedback 在 `TryPrepareWallCrushAnim()` 成功且 `wallCrushAnimPlayed=true` 后播放，因此不会把引用缺失或失败撞墙误表现为成功。
- **P2：重复播放。** `wallCrushAnimPlayed` 在 Feedback 前置为 true，后续调用直接返回 false；单个 Level70 实例最多播放一次成功震屏。
- **P2：Feedback 组件禁用或自身条件拒绝播放。** MMF_Player 可能因 inactive、CanPlay、cooldown 或全局 Feedback 开关跳过；破墙逻辑不依赖其播放结果，避免玩法软锁。
- 调用只发生一次成功撞击，不在 Update 中执行，不新增持续 GC、相机轮询或移动端常驻开销。

### 验证、缺口与回滚

- C# 构建通过：0 error / 99 warnings；静态成功/失败分支与 prefab MMF_Player 缺失检查完成。
- Unity PlayMode、实际 Feedback 绑定、相机回位、暂停/时间缩放、低端 Android/iOS 画面稳定性尚未验证。
- 回滚只需删除 MMF_Player 字段、命名空间和成功播放调用；无需回滚墙体、音效、Hook 或资源。

## 2026-07-22 ball 打开朝上与球壳渐隐风险评估

### 基线与影响范围

- 基线是 ball 打开冻结时保留滚动后的随机角度，并让 openPart 一直保持可见；新行为是打开瞬间统一世界 Z 角并让 openPart 渐隐后关闭。
- 本次只修改 `Level70BallItemCtrl.cs` 和 Level70 本地/共享文档；没有编辑 `Level_070.prefab`、共享物理、相机、事件、存档、配置或资源。

### 风险项与缓解

- **P1：世界角度归一影响 Item。** Item 在 `SetOpenState()` 之后才脱离 ball，因此会先随 ball 转为统一朝向，再以 worldPositionStays 默认行为保留该世界姿态；这符合统一开口方向，但 Item 的实际弹出路径和朝向仍需 PlayMode 验证。
- **P1：渐隐误伤 Item。** Renderer 缓存只从 `openPart.GetComponentsInChildren<SpriteRenderer>(true)` 获取；当前 11 个 `jumpOut` 均在 openPart 子树之外，不会加入 Tween。若后续 prefab 改层级把 Item 放进 openPart，则会被一并淡出，需把该层级约束纳入 prefab 评审。
- **P1：美术基准变化。** 当前 0°对应开口朝上；如果后续替换球壳图片或调整子节点旋转，可通过 `openUpwardWorldEulerZ` 局部修正，不需要改代码。
- **P2：淡出结束后的对象状态。** Sequence 完成后将 openPart inactive；初始化/关闭入口会先 Kill Sequence、恢复初始颜色和初始旋转，避免同实例复用时保持透明。
- **P2：Tween 生命周期。** Sequence 通过 `SetLink(gameObject)` 绑定当前 ball，Level 销毁时自动 Kill；没有新增 OnDisable/OnDestroy 状态重置逻辑。
- 每个 ball 只在成功打开时创建一个短 Sequence，当前最多 11 个，不在 Update 中分配或扫描；Android/iOS 的同时打开峰值仍需设备验证。

### 验证、缺口与回滚

- C# 构建通过：0 error / 1117 warnings；静态层级、初始旋转、Tween 目标和脚本 diff check 已完成。
- Unity PlayMode、不同滚动角度、低帧率、时间缩放、全部 ball 同时淡出、Item 路径和 Android/iOS 横屏尚未验证。
- 回滚只需移除朝上旋转与 openPart 淡出/恢复逻辑；无需回滚排序、音效、Hook、资源或 prefab。

## 2026-07-22 failCrashEffect 整体缩放风险评估

### 基线、风险与验证

- 基线是失败效果只沿 X 轴从 0 展开到 1；新行为是三个轴同时从 0 放大到 1，属于明确的玩家可见表现调整。
- **P1：视觉重心变化。** `DOScale` 会围绕 Transform Pivot 整体放大，和原来的横向展开观感不同；左右 CrashEffect 的 Pivot 是否适合需 PlayMode 确认。
- **P2：最终 Scale 固定为 1。** 当前左右 prefab 初始 Scale 都是 `(1,1,1)`，因此没有尺寸变化；若未来美术把初始 Scale 改成非 1，需要同步调整结束值或增加初始 Scale 缓存。
- **P2：重复触发与残留。** 每次播放前仍 Kill 同侧旧 Tween，并把 Scale 重置为 zero、Alpha 重置为 1；初始化路径把 Scale/Alpha 重置为 zero/0，不会残留半缩放状态。
- 本次没有新增 Tween 数量、逐帧分配、事件、资源、存档或配置；只替换已有缩放 Tween 类型。
- C# 构建通过：0 error / 1117 warnings，静态确认 `DOScaleX` 已从 Level70HookCtrl 移除；Unity PlayMode 和 Android/iOS 横屏尚未验证。
- 回滚只需恢复失败效果的 X 轴初始化和 `DOScaleX`，不影响失败淡出、音效、边界判断或成功破墙。

## 2026-07-22 crash wall 移除平移与 l70PlayGame 单次播放风险评估

### 基线、影响范围与风险

- 原 crash wall 使用同一 Sequence 并行执行 SpriteRenderer 淡出和 Transform X 轴平移；新行为只保留原地淡出，属于明确的玩家可见表现调整。影响 `Level70Ctrl.cs`、`Level_070.prefab` 和 Level70 文档，不影响 Hook 的边界速度判定。
- **P1：破墙视觉完整性。** 移除位移后碎墙只会原地消失，若美术资源原本依赖向左飞出的动势，表现会变弱；这是本次明确需求，仍需 PlayMode 结合震屏确认最终观感。
- **P2：Tween 清理边界。** 代码仍持有并清理淡出 Sequence，但不再 Kill `crushWall2.transform`，避免 Level70 无平移所有权后误杀该 Transform 上的其它 Tween；SpriteRenderer 淡出 Tween 仍由 Sequence 和 Renderer 清理覆盖。
- `l70PlayGame` 原本是持有句柄的循环音效，新行为依赖 `AudioManager.PlaySound` 默认 `loop=false` 播放一次；移除 Level70 停止逻辑后，不会在 actor 恢复或 Level 销毁时截断该单次音效，但较长音频可能跨越关卡销毁边界继续播放到自然完成。
- **P2：重复播放。** `EnterHookControl` 仍先检查 `hookCtrl.IsControlActive`，同一控制阶段不会重复触发；新 Level 实例再次进入会正常再播放一次。
- 一次性音效仍登记到 `AudioManager` 活动音源表，并在 SoundsGood 完成回调中移除；没有新增常驻句柄、Update 开销或全局停止调用。

### 验证、缺口与回滚

- C# 构建通过：0 error / 1117 warnings；静态检查确认平移参数/调用和循环音效持有/停止链均已移除。
- Unity PlayMode、实际碎墙原地淡出观感、Feedback 同步、单次音频完整时长、快速销毁 Level 后的尾音及 Android/iOS 横屏尚未验证。
- crash wall 可通过恢复 X 目标参数与并行位移 Tween 回滚；音效可通过恢复 `Sound` 句柄、`loop:true` 及控制恢复/销毁停止点回滚，资源和生成枚举无需变更。

## 2026-07-22 Hook InQuad 速度曲线风险评估

### 基线与影响范围

- 基线为按活动完整行程计算恒定加速度，速度随持续输入时间线性增长；新行为改为 `t²` 的 InQuad 速度插值，最低/最高速度和边界阈值配置不变，但玩家可感知的提速节奏发生变化。
- 影响限定在 `Level70HookCtrl` 的 Idle 水平移动及 Level70 文档；Catch 下落/回收、actor 控制、ball、墙体表现、音效、Feedback、事件和存档链不变。

### 风险项与缓解

- **P1：前段速度偏慢。** 当前 1→7 的 InQuad 在归一化时间 50% 时速度仅为 2.5，原线性速度为 4；完整行程理论耗时也从 `行程/4` 增至 `行程/3`。这是本次试验的主要手感变化，必须通过 PlayMode 调整后再确认是否保留。
- **P1：破墙判定窗口后移。** 6.09 阈值在 InQuad 下约到 92.1% 曲线时间、82.8% 行程才达到；玩家需要更完整的助跑。15～120 fps 静态模拟的满行程撞击仍为 6.940～7.000，高于阈值，但短距离起步可能失败，符合当前按实际速度判定的规则。
- **P1：反向阶段混合两种模型。** 正向提速使用 InQuad，直接反向仍用原恒定减速度归零；这样避免速度符号瞬间翻转，但减速与重新起步衔接的主观节奏需实机验证。
- **P2：离散帧误差。** 理论端点速度为 7，实际帧步进可能在 6.94～7 之间越过边界；现有 Clamp 防止越界，当前 6.09 阈值留有余量。若后续把阈值调得接近 1，需重新评估端点判定或直接按曲线终点取最大速度。
- **P2：最低速度为 0。** 首帧会推进一个 deltaTime 后评估 InQuad，避免位置不动导致曲线无法启动；当前 prefab 最低速度为 1，不走该兜底分支。
- 曲线状态仅为四个 float/bool 字段，每帧不创建 Tween、AnimationCurve、集合或委托，不新增 GC；移动端成本与原数学更新同量级。

### 验证、缺口与回滚

- C# 构建通过：0 error / 99 warnings；15/30/60/120 fps 数值模拟与旧正向加速度引用扫描通过。
- Unity PlayMode、触控连续输入、快速反向、短距离助跑、墙破后范围切换、暂停/低帧率及 Android/iOS 横屏尚未验证。
- 回滚只需恢复正向 `GetHorizontalAcceleration()`/`Mathf.MoveTowards`，删除 InQuad 状态与辅助方法；不需要改 prefab、资源、存档或事件。

## 2026-07-29 catch_btn 局部 useBtn 与 Jump 隔离风险评估

### 基线与影响范围

- 修改前 Catch 点击会派发 `PlayerJumpEvent`，共享 `BaseLevelCtrl` 和 Level70 控制器都会收到；仅隐藏 `jump_btn` 不能隔离同一全局事件的 Jump 语义。
- 本次只修改 `Level70Ctrl` 与 Level70 专用 `Level70GamePlayPanel`，不修改共享输入、Actor、关卡基类、prefab、配置、存档或资源。

### 风险项与控制

- **P1：control 状态误跳。** Catch 已改为局部按钮回调，不再派发 `PlayerJumpEvent`；共享 Jump 按钮在 control 分支按不可交互、组件 disabled、GameObject inactive 三层关闭，禁用时 `CustomButton` 会复位既有指针状态。
- **P1：退出 control 后 Jump 未恢复。** 正常恢复入口会先解绑 `useBtn`，再激活 `jump_btn` 并恢复 enabled/Interactable；需在 PlayMode 覆盖 actor 恢复 Trigger2D 与关卡重开。
- **P1：面板或 catch_btn 引用缺失。** 当前仍依赖 `GamePlayPanel_025_70` 的固定 `Root/catch_btn` 路径；缺失时 control 会没有可用 Catch 输入。该绑定在现有 prefab 中有静态节点证据，但运行时组件解析仍待 Unity 验证。
- **P2：暂停边界。** `CustomButton.OnPointerDown` 本身不检查 Interactable，因此 Level70 面板在暂停时同时禁用 catch 按钮组件，控制器回调也复核 `isActiveAndEnabled` 与 `Interactable`。
- **P2：回调残留。** 退出 control、Level70Ctrl Disable 和 Destroy 都会解绑 `useBtn.OnClickDown`；未新增全局事件或常驻对象。
- 变更只发生在按钮切换或点击时，没有新增 Update、实例化、资源加载或持续 GC；Android/iOS 性能影响可忽略，但触摸取消和切后台仍需设备验证。

### 验证、缺口与回滚

- 静态扫描确认 Level70 Catch 链路不再包含 `PlayerJumpEvent` / `DispatchEvent`，共享 `GamePlayPanel.OnClickJump()` 未修改。
- `git diff --check` 通过，仅有行尾提示；全量 C# 首次构建在 124 秒超时且无诊断，清理该次残留构建进程后以单节点、禁用共享编译器重试通过：`0 warning / 0 error`。后续环境复跑因 Unity `Temp` 输出被清理及 `Packages` 第三方源码/元数据缺失出现 `CS0006` / `CS2001`，没有产生 Level70 代码诊断；最终 Level70 脚本与成功构建版本一致。
- 未验证 Unity PlayMode、快速连续触摸、按住 Jump 时进入 control、暂停/恢复、重开、退出关卡和 Android/iOS 横屏。
- 若局部绑定造成 Catch 不响应，可整体恢复旧 Catch→`PlayerJumpEvent` 路径；回滚不涉及 prefab、Hook 状态机、存档、配置或资源。

## 2026-09-09 松夹落球风险复核

- 原行为只有开夹等待 2 秒；本次修改 Level70HookCtrl.cs 与 Level_070.prefab，用本地 Physics2D.IgnoreCollision 配对和一次向下速度消除夹子托球风险，复用原物理抓取及球落地打开流程。
- P1：恢复过早会重新夹球。必须同时满足球顶部低于释放包围盒底部 0.05 且不与当前夹子包围盒相交；不按固定延时强行恢复。
- P1：碰撞忽略泄漏。记录原忽略值，球离开或停用、控制器禁用/销毁、ResetRuntime 时恢复；保留原本已经忽略的关系。暂停不按墙钟计时推进。
- P2：其它球、地面或其它障碍仍可阻挡；本次不穿透环境，不瞬移球。多球堆叠、夹子动画最终位置、低帧率及原生覆盖恢复仍需实测，状态 uncertain。
- P2：筛选使用夹子和 catchArea 的合并包围盒；应在 PlayMode 检查释放点附近无未被夹持的球误入范围。已冻结 Y 的球被排除，避免改写打开态。
- 验证：源码构建 0 错误/123 警告；11 球、3 夹子 Collider 引用及球动态/重力/约束静态检查通过；无运行态验收证据。未修改其它关卡、共享 Actor/UI、存档、资源加载或打点。
- 回滚条件：若掉球轨迹、落地开壳或连续抓取出现回归，按同目录功能说明撤回本次 Hook 与 Prefab 字段。待确认项：无需求歧义，仍待 PlayMode 和 Android/iOS 横屏验收。
