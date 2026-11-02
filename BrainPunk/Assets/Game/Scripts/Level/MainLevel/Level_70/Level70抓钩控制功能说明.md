# Level70 抓钩控制功能说明

## 2026-06-29 实现记录

### 需求与背景

- 本次需求来自 `Level70Ctrl.cs` 顶部 `version 1.0` 注释：Level70 需要在 actor 普通控制和抓钩控制之间切换，左右输入控制抓钩，Jump 在抓钩控制状态下作为 Catch，抓钩下落后抓取物品或 stage2 的 actor，上移到 `dropPoint.position.y` 后平移到 `dropPoint` 并打开释放。
- 用户明确要求不改写原注释内容；本次实现保留 `Level70Ctrl.cs` 顶部 `version 1.0` 注释文本，包括原有拼写和换行。
- 已按规格澄清保留 `Key tag=tools` 作为 stage2 入口，actor 普通控制只通过专用恢复 Trigger2D 入口恢复；未重新引入已移除的旧触发事件名。

### 影响文件与系统

- `Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs`：新增 Level70 本地抓钩状态机、输入接管、抓取/释放、运行时触发器绑定、清理逻辑。
- `Assets/Game/Scripts/Level/Actor/Level70ActorCtrl.cs`：新增 Level70 抓钩锁定、搬运、等待恢复 Trigger2D 和清理 API，避免修改共享 `ActorCtrl`。
- `Assets/Game/Scripts/Level/LevelItem/Level_70/Level70BallItemCtrl.cs`：新增 close/open 状态切换，释放后可隐藏 close 部分并关闭 collider。
- 关联但未修改：`Assets/Game/Prefabs/Level/Level_070.prefab`、`BaseLevelCtrl`、`ActorCtrl`、`OnTrigger2DHandle`、`PlayEvents`。
- 本次未修改 DB/save、RemoteConfig、Luban 配置、YooAsset/Resources、analytics、广告、内购、SDK 或其它关卡脚本。

### 修改前行为基线

- `Level70Ctrl` 原本只有 `actor`、`catchAnim` 字段、`Start()` 缓存 `catchAnim.transform`，以及 Odin 测试按钮切换 Animator bool `isOpen`。
- `Level70ActorCtrl` 已有控制手柄动画状态，`ActorCtrl.TriggerJump()` 仍会被 `BaseLevelCtrl.OnPlayerJump()` 调用；`BaseLevelCtrl.SetCtrlable(false)` 只拦截左右和 cancel，不拦截 jump。
- 静态 prefab 证据显示 `Level_070.prefab` 根节点有 `Level70Ctrl`，存在 `controller` 触发器、`catch`、`catchArea`、`dropPoint`，actor prefab override 中存在 `Level70ActorCtrl` 组件 GUID `0c20f997e9eaf734a8b7a3fe62d397b7`。
- 静态 prefab 证据也显示根 `BaseLevelCtrl.actor` 和旧 `Level70Ctrl.actor` 字段为 `{fileID: 0}`，`controller` 上的 `OnTrigger2DHandle` targetTag 为 `Player`，但持久化 `m_MethodName` 为空。

### 修改后行为

- `Level70Ctrl` 使用本关局部枚举管理 `Playing/Complete` 阶段和 `Idle/Lowering/Carrying/Retracting/MovingToDrop/Releasing/Recovering` 抓钩状态。
- 抓钩控制入口 `EnterHookControl(Collider2D)` 会锁定 actor、关闭共享左右输入、清空抓钩移动输入，并把抓钩限制在当前阶段范围内；重复入口会被忽略。
- `OnEnable()` 会在运行时查找 `controller` 的 `OnTrigger2DHandle`，把 `OnEnter` 注册到 `EnterHookControl`，用于补齐当前 prefab 中 `m_MethodName` 为空的入口问题；禁用和销毁时会解绑。
- 抓钩控制激活后，`PlayerRightMoveEvent` / `PlayerLeftMoveEvent` 驱动抓钩水平移动，`PlayerMoveCancelEvent` 停止抓钩水平输入，`PlayerJumpEvent` 触发 Catch。
- Catch 下落使用 DOTween 移动 `catchRoot` 到下落边界；如果未抓到目标，会自动回收到初始高度并回到 `Idle`，不触发失败。
- 下落阶段会通过 `catchArea.OverlapCollider(...)` 轮询重叠目标，并保留 `OnCatchAreaEnter(Collider2D)` 作为 UnityEvent 可绑定入口；因此当前不要求修改共享 `OnTrigger2DHandle`。
- 合法物品会被临时挂到 `catchAreaPosition`，暂停 Rigidbody2D 重力和速度，跟随抓钩上移到 `dropPoint` 高度，再平移到 `dropPoint.x`，打开抓钩释放并恢复物理。
- `EnterStage2(Collider2D)` 只接受 tag 为 `tools` 的 collider，进入后抓钩范围切换到 stage2/range2；重复触发不会重置状态。
- complete/stage2 下允许抓取 actor；释放 actor 后只进入等待恢复状态，不会自动恢复普通移动。
- `RestoreActorControl(Collider2D)` / `RestoreActorControl()` 是 actor 恢复入口，会清理 Level70 抓钩锁定并恢复 `BaseLevelCtrl.SetCtrlable(true)`。
- `GameResetEvent`、`SwitchGameStateEvent.SUCCESS/FAILED`、`OnDisable()` 和 `OnDestroy()` 会 kill 抓钩 tween、释放当前目标、重置抓钩、恢复 actor 本地锁定状态。

### 数据、资源、事件与平台流

- 输入事件：只订阅既有 `PlayerRightMoveEvent`、`PlayerLeftMoveEvent`、`PlayerMoveCancelEvent`、`PlayerJumpEvent`、`GameResetEvent`、`SwitchGameStateEvent`，未新增共享事件。
- 物理：抓取期间临时保存并恢复目标 `Rigidbody2D.bodyType`、`gravityScale`、`linearVelocity` 和 `angularVelocity`；清理路径使用零速度恢复，降低重开残留风险。
- 资源：不新增 prefab、Sprite、AnimatorController、Spine、Resources 或 YooAsset 地址；仍依赖当前 prefab 中的 `catch` Animator 和 `dropPoint`。
- UI/按钮：不修改 GamePlayPanel；Jump/Catch 语义通过 Level70 本地监听 `PlayerJumpEvent` 实现，实际按钮文案/图标切换未在本次脚本中处理，需 Unity UI 验证确认。
- 保存/配置/平台：无持久化字段、配置表、analytics、广告、IAP、SDK 或平台构建变更。

### 验证记录

- `git diff --check -- BrainPunk/Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs BrainPunk/Assets/Game/Scripts/Level/Actor/Level70ActorCtrl.cs BrainPunk/Assets/Game/Scripts/Level/LevelItem/Level_70/Level70BallItemCtrl.cs` 通过，仅有 Git LF/CRLF 工作区提示。
- 旧触发事件名扫描未在 Level70 脚本和当前 feature artifacts 中发现残留。
- 静态检查确认 `Level70Ctrl.cs` 顶部 `version 1.0` 注释仍存在，diff 显示注释文本未被改写。
- `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` 因当前分支名 `codex/level70-v1-logic` 不符合 Speckit 数字分支规则而失败；当前 feature 目录依据 `.specify/feature.json` 为 `specs/031-level70-v1-control`。
- 当前 worktree 中不存在 `BrainPunk/Assembly-CSharp.csproj`，无法执行 `dotnet build`；需要打开 Unity 生成工程文件或使用 Unity Console 编译结果补验。

### 剩余风险与未验证项

- 未运行 Unity Editor 编译，无法确认 `ContactFilter2D` / `Collider2D.OverlapCollider(...)` 在当前 Unity 6000.0.59f2 工程中的最终编译结果。
- 未运行 PlayMode；抓钩入口、左右移动、Catch、抓取、释放、miss 回收、stage2、actor 恢复、失败、重开、通关、退出和下一关都需要实机流程验证。
- `Level_070.prefab` 的 stage2 `tools` 入口、actor 恢复 Trigger2D、range1/range2/lowerBoundary 绑定未在静态文本中确认；当前脚本有 fallback，但不能替代正式 Inspector 验收。
- 当前 ball prefab 未静态确认挂有 `Level70BallItemCtrl`；脚本可按名称 fallback 抓取 `red_ball`、`blue_ball`、`yellow_ball`，但 open/close 视觉切换需要 Unity 绑定验证。
- 未做 Android/iOS 横屏触摸 smoke；快速点击、触摸取消、多次 Catch、长按左右移动和帧稳定性仍未验证。
- 未跑非 Level70 回归；共享输入未被修改，但仍需至少一个已验收关卡验证普通移动、跳跃、成功和失败流程。

### 回滚路径

- 脚本回滚：恢复 `Level70Ctrl.cs`、`Level70ActorCtrl.cs`、`Level70BallItemCtrl.cs` 到修改前版本。
- 绑定回滚：如果后续修改 `Level_070.prefab` 绑定后出现异常，回滚该 prefab 的 Level70 专用触发器、范围点和对象引用绑定。
- 流程回滚：若抓钩控制造成 actor 软锁或非 Level70 回归，优先禁用 `Level70Ctrl` 新抓钩入口，保留共享 `BaseLevelCtrl`、`ActorCtrl` 和通用输入事件不变。

## 2026-06-30 v2 接触回收与 ball 初始化补充

### 需求与背景

- 本次需求来自 `Level70Ctrl.cs` 顶部 `version 2.0 补充逻辑` 注释：`catchArea` 去除接触吸附逻辑，改为触碰物体时尝试将抓钩 Animator `isOpen` 改为 `false`；同时增加 `balls` 根据 `itemSprites` 填充内容的初始化逻辑。
- 已确认用户约束为不改写原注释内容；本次实现未修改 `Level70Ctrl.cs` 顶部既有 v1/v2 注释文本。
- 规格澄清结果：有效接触后本次 Catch 立即停止并回收到可继续操作流程；无对应 sprite 的 ball 保持可见空内容并继续参与物理/触发。

### 影响文件与系统

- `Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs`：`OnCatchAreaEnter(Collider2D)` 在验证有效目标后进入本地 `RecoverAfterContact()`，不再从接触入口调用 `CaptureTarget(...)`；`ResetHookRuntime(...)` 增加 `InitializeBallsFromSprites()`。
- `Assets/Game/Scripts/Level/LevelItem/Level_70/Level70BallItemCtrl.cs`：新增 `InitializeContent(Sprite contentSprite)`，用于关卡进入、重开或清理重置时刷新 ball 内容、close/open 显示和 collider。
- `Assets/Game/Prefabs/Level/Level_070.prefab`：本次未编辑。静态扫描确认当前脏 prefab 已存在 `catchArea`、`dropPoint`、range1/range2、`lowerBoundary`、`hookControlTrigger`、`balls` 和 `itemSprites` 字段引用，但正式 Inspector/PlayMode 绑定仍需补验。
- 本次未修改 `ActorCtrl`、`BaseLevelCtrl`、`OnTrigger2DHandle`、共享输入事件、DB/save、RemoteConfig、Luban、Resources/YooAsset、UI、analytics、广告、内购或平台 SDK。

### 修改前行为基线

- v1 接触入口基线：`OnCatchAreaEnter(Collider2D)` 调用 `TryResolveTarget(...)` 后直接进入 `CaptureTarget(...)`，该路径会暂停目标 Rigidbody2D、设置目标 parent 到 `catchAreaPosition`、上移到 `dropPoint.position.y`，再平移到 `dropPoint.x` 并释放。
- v1 miss 基线：未命中目标并到达下边界时调用 `RecoverAfterMiss()` 回到初始高度，进入 `Idle`。
- ball 基线：`Level70BallItemCtrl` 只有 `SetClosedState()` / `SetOpenState()`；`Level70Ctrl` 中虽已有 `balls` 和 `itemSprites` 序列化字段，但没有按索引初始化 ball 内容的运行时代码。

### 修改后行为

- 有效接触路径现在只把接触 collider 用作合法性过滤；通过后调用 `RecoverAfterContact()`，该方法停止当前下落 tween、清空当前目标缓存、请求 `SetHookOpen(false)`，再复用 `RecoverAfterMiss()` 回收到初始高度。
- v2 接触路径不调用 `CaptureTarget(...)`，因此不会因接触本身执行 `PauseTargetPhysics(...)`、`AttachTargetToCatch(...)`、移动到 `dropPoint` 或设置 `currentTarget`。
- `UpdateCatchAreaOverlap()` 在 `RecoverAfterContact()` 后会因 `hookState` 不再是 `Lowering` 而停止本次重叠循环，避免一次下落处理多个有效接触。
- `InitializeBallsFromSprites()` 只在 `Application.isPlaying` 且 `ResetHookRuntime(...)` 执行时运行；覆盖 `Awake()` 初始重置、`GameResetEvent`、失败/成功状态清理、禁用/销毁清理等既有重置路径，不在每帧或每次 Catch 前刷新。
- ball 初始化按索引读取 `itemSprites`：同序号有 sprite 时显示对应 sprite；缺失或 null sprite 时将内部 `SpriteRenderer.sprite` 置空并隐藏 renderer，同时保持 close 外观 active、open 外观 inactive、`itemCollider.enabled=true`；多余 sprite 被忽略，null ball 条目跳过。

### 数据、资源、事件与平台流

- 数据流：只读取 Unity 序列化的 `balls` 和 `itemSprites` 列表，不新增持久化字段或配置表字段。
- 资源流：不新增 Sprite 资源地址、Resources/YooAsset 路径或热更规则；运行时只把已序列化 sprite 引用赋给对应 ball。
- 事件流：继续使用既有玩家输入事件、`GameResetEvent` 和 `SwitchGameStateEvent`；v2 接触回收不派发新事件，不自动恢复 actor 普通移动，不改变 `tools` stage2 入口。
- 物理流：v2 接触路径不暂停目标 Rigidbody2D、不改 parent、不移动目标；空内容 ball 的 collider 保持启用，仍参与本关物理/触发。
- 性能流：ball 初始化是 O(n) 生命周期操作；接触检测仍使用固定 `Collider2D[] catchOverlapResults`，本次未新增持续实例化或全局查找循环。

### 验证记录

- Speckit 前置脚本：`.\.specify\scripts\powershell\check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` 因当前分支 `feature/1.9.0_common` 不符合数字 feature 分支规则失败；本次按 `.specify/feature.json` 指向的 `specs/032-level70-v2-hook-balls` 继续。
- 需求 checklist：`specs/032-level70-v2-hook-balls/checklists/requirements.md` 当前 16/16 已完成。
- 静态扫描：`rg -n "version 1.0|version 2.0|去除吸附|balls 根据 itemSprites|CaptureTarget\(|AttachTargetToCatch\(|PauseTargetPhysics\(|RecoverAfterContact|InitializeBallsFromSprites|InitializeContent" ...` 确认顶部 v1/v2 注释仍存在，v2 接触入口命中 `RecoverAfterContact()`，ball 初始化入口存在；`CaptureTarget(...)` 等 v1 搬运方法仍保留但不再由 `OnCatchAreaEnter(...)` 调用。
- 编译：`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果 0 error / 96 warning；warning 属于当前工程输出，未逐项审计。
- 空白检查：`git diff --check -- BrainPunk/Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs BrainPunk/Assets/Game/Scripts/Level/LevelItem/Level_70/Level70BallItemCtrl.cs BrainPunk/Assets/Game/Prefabs/Level/Level_070.prefab` 未通过，命中已脏 `Level_070.prefab` 多处空值行尾空格，以及 `Level70Ctrl.cs` 顶部空注释行尾空格。因用户要求不要改写注释内容且本次不接管 prefab 脏改，未清理这些行尾空格。

### 手工验证清单

- US1：进入 Level70 后触发抓钩控制，让 `catchArea` 接触有 sprite 的 ball 5 次；每次应关闭抓钩并回收，ball 不改 parent、不被移到 `catchAreaPosition` 或 `dropPoint`，Rigidbody2D 不因接触路径被暂停。
- US2：分别验证 `balls.Count == itemSprites.Count`、`balls.Count > itemSprites.Count`、`balls.Count < itemSprites.Count`；进入和重开后同序号 sprite 正确，缺失 sprite 的 ball 可见为空且 collider 仍启用，多余 sprite 被忽略。
- US3：验证 range1/range2、`tools` stage2 入口、actor 专用恢复入口、失败、重开、通关、退出和下一关流程；v2 接触关闭不得自动恢复 actor。
- 非 Level70 回归：至少选择一个已验收非 Level70 关卡，验证左右移动、跳跃、成功和失败流程。
- 移动端 smoke：Android/iOS 或等效横屏触摸环境验证 Level70 进入、重开、左右触摸移动、Catch 接触回收、快速点击 Catch、前后台切换后继续操作。

### 剩余风险与未验证项

- 未运行 Unity Editor Console 编译；`dotnet build` 只能覆盖 C# 工程静态编译，不替代 Unity 资源导入、Animator、Prefab 和 Console 验证。
- 未运行 Level70 PlayMode；US1 的 5 次接触、US2 的三种 ball/sprite 矩阵、US3 的 stage2/actor 恢复/失败/重开/通关/退出仍待补验。
- 未运行 Android/iOS 横屏触摸和帧率 smoke，60 fps 目标、快速点击 Catch、触摸取消和前后台切换仍为未验证风险。
- 未运行非 Level70 回归；虽然本次未修改共享系统，仍需一个已验收关卡补移动、跳跃、成功、失败验证。
- `Level_070.prefab` 已有大量未提交脏改；本次只做静态 grep 级别绑定确认，没有保存或清理 prefab。

### 回滚路径

- 脚本回滚：撤销 `Level70Ctrl.cs` 中 `OnCatchAreaEnter(...)` 的 v2 接触回收、`RecoverAfterContact()`、`InitializeBallsFromSprites()` 和 `ResetHookRuntime(...)` 初始化调用；撤销 `Level70BallItemCtrl.cs` 的 `InitializeContent(...)`。
- 资源回滚：本次未编辑 `Level_070.prefab`；如果后续 Unity 绑定补验中保存 prefab 出现异常，优先回滚 prefab 的 Level70 专用 `balls/itemSprites`、range、trigger 引用。
- 流程回滚：若 Level70 出现软锁、无法完成 stage2/actor 恢复、ball 显示错乱或非 Level70 回归，恢复 v1 接触搬运路径并保留共享系统不变。

## 2026-06-30 v2.1 Catch 按钮 UI 切换补充

### 需求与背景

- 本次需求来自 `Level70Ctrl.cs` 顶部 `version 2.1 补充` 注释：`GameplayPanel` 获取 `catch_btn`，并在 handle control 状态时隐藏 `jumpBtn`。
- 本地证据显示 `Level_070.prefab` 的 `BaseLevelCtrl.gamePlayPanelName` 当前为 `GamePlayPanel_025_70`，该 UI prefab 文本中存在 `Root/catch_btn`；共享 `GamePlayPanel` 脚本此前只绑定 `JumpBtn`，未解析 `catch_btn`。
- 用户已明确要求：不得为了 Level70 / `LevelCtrl` 局部逻辑直接修改共享 `GamePlayPanel.cs`，也不得随意新增 `PlayEvents`；如需扩展 UI 行为，使用 `GamePlayPanel` 子类或关卡专用面板组件。
- 实现目标是只改变 Level70 抓钩控制态下的 UI 表现，不改变 `PlayerJumpEvent` 输入语义和抓钩 Catch 监听入口。

### 影响文件与系统

- `Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs`：在进入抓钩控制时从 `PlayCtrl.Instance.CurrentGamePlayPanel` 上获取 `Level70GamePlayPanel` 组件并切换 Catch UI 模式；恢复、重置、失败/成功清理、禁用/销毁时切回 Jump UI 模式。
- `Assets/Game/Scripts/Level/LevelItem/Level_70/Level70GamePlayPanel.cs`：继承共享 `GamePlayPanel`，只在 Level70 专用面板中解析 `Root/jump_btn` 和 `Root/catch_btn`；`catch_btn` 点击派发既有 `PlayerJumpEvent`。
- `Assets/Game/UI/GamePanel/Resources/GamePlayPanel_025_70.prefab`：当前工作区版本移除继承来源上的共享 `GamePlayPanel` 组件，并新增脚本 GUID 为 `a8d5a5b6e2c84f45a3fd0b2f5b7c9e31` 的 `Level70GamePlayPanel` 组件。
- `Assets/Game/Scripts/Play/PlayEvents.cs`：当前不承载 Level70 Catch/Jump 按钮显隐事件；本 UI 切换不新增 `PlayEvents`。
- 本次 Level70 UI 切换不在共享 `GamePlayPanel.cs` 中新增 Level70 API 或 `catch_btn` 解析逻辑；也未修改 `CustomButton`、`BaseLevelCtrl`、`ActorCtrl`、存档、配置、资源加载、analytics、广告、IAP 或平台 SDK。

### 修改前行为基线

- Level70 抓钩控制激活后，玩家仍通过默认 `jump_btn` 触发 `PlayerJumpEvent`，按钮外观未切换为 Catch。
- `GamePlayPanel` 自动生成字段中只有 `JumpBtn`，没有 `catch_btn` 字段；`GamePlayPanel_025_70.prefab` 虽有 `catch_btn`，但运行时没有统一订阅。
- Level70 抓钩逻辑已经监听 `PlayerJumpEvent` 作为 Catch 输入，因此不需要新增新的 Catch 输入事件。

### 修改后行为

- `Level70GamePlayPanel.OnCreate()` / `OnOpen()` 会解析并绑定 `Root/catch_btn`，默认执行 `SetLevel70CatchButtonVisible(false)`，确保 UI 面板打开或复用时先显示 Jump、隐藏 Catch。
- `catch_btn` 解析成功后会订阅 `OnClickDown += OnClickCatch`；`OnClickCatch(...)` 不新增输入事件，仍派发既有 `PlayerJumpEvent`。
- `Level70Ctrl.EnterHookControl(...)` 在 actor 进入抓钩控制后通过当前面板的 `Level70GamePlayPanel.SetLevel70CatchButtonVisible(true)` 隐藏 `jump_btn` 并显示 `catch_btn`。
- `RestoreActorControl()`、`ResetHookRuntime(...)` 和清理路径会通过 `Level70GamePlayPanel.SetLevel70CatchButtonVisible(false)` 隐藏 `catch_btn` 并恢复 `jump_btn`。
- 如果当前面板为空或不是 `Level70GamePlayPanel`，`Level70Ctrl` 会记录 warning 并跳过 UI 切换；该情况不会新增全局事件兜底，需通过绑定 `GamePlayPanel_025_70.prefab` 解决。

### 数据、资源、UI 与事件流

- UI 流：`Level70Ctrl` 不直接查找 UI 节点，只从 `PlayCtrl.Instance.CurrentGamePlayPanel` 获取 Level70 专用面板组件并调用公开 API；`Level70GamePlayPanel` 负责解析当前面板内 `Root/jump_btn` / `Root/catch_btn` 并切换显示。
- 事件流：Catch 按钮点击仍走既有 `PlayerJumpEvent`，不新增抓钩输入事件，也不新增 Level70 专用 `PlayEvents`。
- 资源流：`Level_070.prefab` 指向 `GamePlayPanel_025_70`；`GamePlayPanel_025_70.prefab` 必须保持 `Root/jump_btn`、`Root/catch_btn` 路径和 `Level70GamePlayPanel` 组件绑定。
- 平台流：没有新增平台 SDK、广告、IAP、analytics 或配置链路；移动端触摸仍依赖 `CustomButton.OnClickDown`。

### 验证记录

- 静态扫描：确认旧 Level70 UI 切换事件名无残留；确认 `SetLevel70CatchButtonVisible(...)` 只存在于 `Level70GamePlayPanel`、`Level70Ctrl` 和文档中。
- UI prefab 静态扫描：确认 `Level_070.prefab` 指向 `GamePlayPanel_025_70`，且该面板 prefab 文本中存在 `catch_btn`，并添加了 `Level70GamePlayPanel` 组件 GUID。
- 编译：`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，0 error / 352 warnings；warnings 属于当前工程输出，未逐项审计。
- 空白检查：`git diff --check -- AGENTS.md Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs Assets/Game/Scripts/Level/LevelItem/Level_70/Level70GamePlayPanel.cs Assets/Game/Scripts/Level/LevelItem/Level_70/Level70GamePlayPanel.cs.meta Assets/Game/UI/GamePanel/Resources/GamePlayPanel_025_70.prefab Assets/Game/UI/GamePanel/Resources/GamePlayPanel_025_70.prefab.meta Assets/Game/Scripts/Level/LevelItem/Level_70/Level70抓钩控制功能说明.md Assets/Game/Scripts/Level/LevelItem/Level_70/Level70抓钩控制风险评估.md DevelopMD/已有功能设计与使用记录.md` 通过，仅有 Git 工作区 LF/CRLF 转换提示。

### 剩余风险与未验证项

- 未运行 Unity Editor PlayMode；需要确认进入 Level70 后按钮实际从 Jump 切到 Catch，退出抓钩控制、重开、失败、成功、退出关卡后能恢复 Jump。
- 未在移动端横屏触摸环境验证 `catch_btn.OnClickDown` 与 `jump_btn.OnClickDown` 手感一致。
- `GamePlayPanel_025_70.prefab` 的 `catch_btn` 节点路径只做文本确认，未在 Unity Inspector 中确认组件引用、层级激活状态和点击音效表现。
- 当前 `GamePlayPanel.cs` 已有其它未提交修改；本次不接管这些共享 UI 变更，也不以它们作为 Level70 Catch 按钮方案的一部分。

### 回滚路径

- 脚本回滚：撤销 `Level70Ctrl.SetLevel70CatchButtonMode(...)` 及其调用，删除或停用 `Level70GamePlayPanel.cs`。
- UI 回滚：把 `GamePlayPanel_025_70.prefab` 恢复为使用共享 `GamePlayPanel` 组件；如只需临时止血，可先禁用 `SetLevel70CatchButtonMode(...)` 调用恢复默认 Jump 按钮。

## 2026-06-30 v2.2 Catch 统一回 dropPoint 与 isOpen 节奏调整

### 需求与背景

- 本次需求要求重写 Catch 收束逻辑：无论抓钩下降中是否碰到物体，都必须回到 `dropPoint` 并执行 `isOpen` 状态节奏。
- 默认 `isOpen=false`；Catch 下降过程中 `isOpen=true`；下降期间碰到物体或到达最低点后立即 `isOpen=false`；抓钩移动回 `dropPoint` 后再 `isOpen=true` 等待 2 秒，等待期间不能操作；等待结束后恢复默认关闭并回到可操作状态。
- 本次是在 v2 去吸附逻辑基础上调整收束位置和开合节奏，不恢复 v1 的目标吸附、挂载或搬运。

### 影响文件与系统

- `Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs`：统一 `RecoverAfterMiss()` 与 `RecoverAfterContact()` 到 `ReturnHookToDropPointAfterCatch()`，新增回到 `dropPoint` 后 2 秒打开等待，再关闭并回到 `Idle`。
- 本次未修改 `Level70BallItemCtrl.cs`、`PlayEvents.cs`、`GamePlayPanel.cs`、`Level_070.prefab`、共享输入、保存、配置、资源加载、analytics、广告、IAP 或平台 SDK。

### 修改前行为基线

- v2 接触路径：`OnCatchAreaEnter(...)` 只在解析到有效目标后调用 `RecoverAfterContact()`，随后关闭抓钩并复用 miss 回收到初始高度。
- v2 miss 路径：下降到最低点时回收到初始高度并回到 `Idle`，不会移动到 `dropPoint`。
- v2 抓钩开合：Catch 开始后会打开；接触后关闭；回收完成后直接可操作，没有 `dropPoint` 打开 2 秒等待。

### 修改后行为

- `TryStartCatch()` 保持 Catch 开始时 `SetHookOpen(true)`，表示下降阶段打开。
- `OnCatchAreaEnter(...)` 和 `UpdateCatchAreaOverlap()` 现在只要求接触对象是外部 active collider，排除 `catchArea` 自身和抓钩层级内 collider；触碰后不再要求解析成可抓目标。
- 接触物体或到达最低点都会进入 `ReturnHookToDropPointAfterCatch()`：停止当前下落 tween，清空目标缓存，立即 `SetHookOpen(false)`，然后先移动到 `dropPoint.position.y`，再移动到 `dropPoint.position.x`。
- 移动到 `dropPoint` 后进入 `Releasing` 状态并 `SetHookOpen(true)`，等待固定 2 秒；等待期间 `hookState` 不是 `Idle`，左右移动和 Catch 都不会被接收。
- 2 秒等待结束后执行 `ReturnToIdleAfterDropPointOpen()`：`SetHookOpen(false)`，清空目标缓存，`hookState=Idle`，并按当前阶段范围 clamp 抓钩位置。

### 数据、资源、事件与平台流

- 事件流：继续使用既有 `PlayerJumpEvent` 触发 Catch，不新增事件；v2.1 Catch/Jump 按钮显隐通过 `PlayCtrl.CurrentGamePlayPanel` 获取 `Level70GamePlayPanel` 后调用其本地 API。
- 物理流：接触路径仍不暂停目标 Rigidbody2D、不改 parent、不移动目标；只把接触作为抓钩自身收束信号。
- 资源流：不新增资源、prefab 绑定、Animator 参数或 YooAsset/Resources 路径；继续使用既有 `isOpen` Animator bool。
- 输入流：锁输入依赖本地 `HookControlState`，只有 `Idle` 接收左右移动和下一次 Catch；`Retracting`、`MovingToDrop`、`Releasing` 期间均不可操作。

### 验证记录

- 静态扫描：`rg -n "IsHookContactCollider|ReturnHookToDropPointAfterCatch|ReturnToIdleAfterDropPointOpen|DropPointOpenWaitDuration|SetHookOpen" Assets\Game\Scripts\Level\LevelItem\Level_70\Level70Ctrl.cs` 确认接触过滤、统一回 `dropPoint`、2 秒等待和开合入口存在。
- 编译：`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，0 error / 144 warnings；warnings 属于当前工程输出，未逐项审计。
- 空白检查：`git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs Assets/Game/Scripts/Level/LevelItem/Level_70/Level70抓钩控制功能说明.md Assets/Game/Scripts/Level/LevelItem/Level_70/Level70抓钩控制风险评估.md DevelopMD/已有功能设计与使用记录.md` 通过，仅有 Git 工作区 LF/CRLF 转换提示。

### 剩余风险与未验证项

- 未运行 Unity Editor PlayMode；需要验证触碰 ball、触碰其它外部 collider、未触碰到最低点三条路径都能关闭、回 `dropPoint`、打开 2 秒、再关闭并恢复操作。
- 未验证 2 秒等待期间快速点击 Catch、左右长按、移动取消、重开、失败、成功、退出关卡是否都不会产生状态残留。
- 未运行 Android/iOS 横屏触摸 smoke；移动端快速触摸和前后台切换仍为未验证风险。
- `dropPoint` 绑定只依赖已有 prefab 字段；若缺失会 fallback 到初始高度和当前 x，仍需 Unity Inspector 确认实际绑定符合关卡设计。

### 回滚路径

- 脚本回滚：恢复 `RecoverAfterMiss()` 回初始高度路径，恢复 `OnCatchAreaEnter(...)` 只对可解析目标生效，删除 `ReturnHookToDropPointAfterCatch()`、`ReturnToIdleAfterDropPointOpen()` 和 `DropPointOpenWaitDuration`。
- 行为回滚：如果统一回 `dropPoint` 造成 Level70 节奏或 stage2 软锁，优先只回滚本次收束路径，保留 v2 去吸附和 v2.1 UI 按钮切换。

## 2026-06-30 v2.2 isOpen 下降表现即时切换补充

### 需求与背景

- 现象：Catch 下落开始时脚本已经调用 `SetHookOpen(true)`，但实际视觉看起来 `isOpen` 没有立刻生效。
- 本地证据显示 `Level_070.prefab` 的 `catch` Animator 绑定 `Assets/Game/ArtSrc/Animations/Level70/catch.controller`；该 controller 的 `isOpen=true` 转场从 `catch_idle` 到 `cactch_grap`，原先启用了 `Has Exit Time` 并有 0.25 秒过渡，因此 bool 变化不会立刻切状态。

### 影响文件与系统

- `Assets/Game/ArtSrc/Animations/Level70/catch.controller`：关闭 `catch_idle -> cactch_grap` 的 exit time，并把开/关两条转场过渡时长设为 0，保证 `isOpen` bool 与视觉状态同步。
- 本次未修改 `Level70Ctrl.cs` 的开合调用顺序，仍由 `TryStartCatch()` 设置下降打开，由接触/最低点收束设置关闭。

### 修改后行为

- Catch 下降开始设置 `isOpen=true` 后，Animator 会立即从 `catch_idle` 切到 `cactch_grap`。
- 接触物体、到达最低点或等待结束设置 `isOpen=false` 后，Animator 会立即切回 `catch_idle`。
- 这个修改只影响 Level70 抓钩 AnimatorController，不改变目标接触、dropPoint 回收、输入锁定、UI 按钮或 ball 初始化逻辑。

### 验证记录

- 静态检查：`rg -n "m_ConditionMode|m_ConditionEvent|m_TransitionDuration|m_HasExitTime" Assets\Game\ArtSrc\Animations\Level70\catch.controller` 确认 `isOpen` 两条转场均无 exit time，过渡时长为 0。
- 编译仍需最终命令复核；AnimatorController 资源修改不替代 Unity PlayMode 视觉验证。

### 剩余风险与回滚

- 未运行 Unity PlayMode；仍需观察下降起始帧、接触关闭帧、回到 `dropPoint` 后打开 2 秒和等待结束关闭是否符合美术预期。
- 如即时切换过硬，可只恢复 `m_TransitionDuration` 为 0.25，保留 `m_HasExitTime=0`，避免再次出现等待 idle exit time 的延迟。

## 2026-06-30 v2.2 OnTrigger2D API 与 BallItem 弹出补充

### 需求与背景

- 本次补充来自 `Level70Ctrl.cs` 顶部 `version 2.2` 注释：需要提供两个可给 `OnTrigger2DHandle` 绑定的 API。
- Player 触发入口需要使 Level70 进入 stage2，并恢复 Player 普通操作。
- tools 触发入口需要把对应 `Level70BallItem` 打开，并触发内部 sprite item 的 `ItemJumpOutExecutor.Play()`。
- 本次未改写 `Level70Ctrl.cs` 顶部既有注释内容，只按注释补齐运行时代码。

### 影响文件与系统

- `Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs`：新增 `EnterStage2AndRestorePlayerControl(Collider2D)`、`OpenBallItemByTools(Collider2D)`，并提取 `EnterCompletePhase()` 复用 stage2 状态切换。
- `Assets/Game/Scripts/Level/LevelItem/Level_70/Level70BallItemCtrl.cs`：新增 `OpenAndPlayJumpOut()`，运行时从子节点兜底查找 `ItemJumpOutExecutor`，并用 `hasOpened` 防止重复打开时重复播放弹出。
- 本次未修改 `Level_070.prefab`、`OnTrigger2DHandle`、共享输入事件、`ActorCtrl`、`BaseLevelCtrl`、GamePlayPanel、存档、配置、资源加载、analytics、广告、IAP 或平台 SDK。

### 修改前行为基线

- `EnterStage2(Collider2D)` 只接受 tag 为 `tools` 的 collider 并切换到 `Complete`，不会同时恢复 actor 普通控制。
- `RestoreActorControl(Collider2D)` 已可恢复 actor 普通控制，但没有和 stage2 Player 触发入口合并。
- `Level70BallItemCtrl` 已有 `InitializeContent(Sprite)`、`SetClosedState()`、`SetOpenState()`，但未把打开逻辑和 `ItemJumpOutExecutor.Play()` 串起来；`jumpOut` 字段已存在但缺少使用逻辑和兜底解析。

### 修改后行为

- `EnterStage2AndRestorePlayerControl(Collider2D)` 只接受 Player/actor collider；通过后进入 `Complete` 阶段并调用现有 `RestoreActorControl()`，因此会关闭 Level70 Catch UI、清理抓钩控制态并恢复 `BaseLevelCtrl.SetCtrlable(true)`。
- `OpenBallItemByTools(Collider2D)` 只接受 tag 为 `tools` 的 collider；从 collider 父级或子级解析 `Level70BallItemCtrl` 后调用 `OpenAndPlayJumpOut()`。
- `OpenAndPlayJumpOut()` 仅在 PlayMode 且当前 ball 未打开时生效；它会切到 open 状态、关闭 collider，并播放内部 `ItemJumpOutExecutor`。重复触发会被 `hasOpened` 忽略。
- `InitializeContent(Sprite)` 和 `SetClosedState()` 会重置 `hasOpened=false`，确保重开或清理后 ball 可重新打开和弹出。

### 数据、资源、事件与平台流

- 事件流：继续使用 Unity `OnTrigger2DHandle.OnEnter(Collider2D)` 传参，不新增 `PlayEvents` 或全局事件。
- 资源流：不新增资源路径；`ItemJumpOutExecutor` 来自当前 ball 子节点的现有组件，缺失时只跳过弹出表现。
- 数据流：不读写 DB、PlayerPrefs、RemoteConfig、Luban 或存档字段。
- 平台流：无 Android/iOS 专用分支；仍需横屏触摸环境补验快速触发和重开恢复。

### 验证记录

- 静态扫描确认 `EnterStage2AndRestorePlayerControl`、`OpenBallItemByTools`、`OpenAndPlayJumpOut` 已存在，且 `Level70BallItemCtrl` 会从子节点兜底查找 `ItemJumpOutExecutor`。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过，结果为 0 error / 98 warnings；warning 属于当前工程输出，未逐项审计。
- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs Assets/Game/Scripts/Level/LevelItem/Level_70/Level70BallItemCtrl.cs` 未通过，命中 `Level70Ctrl.cs` 顶部 `version 2.2` 注释行尾空格；因用户要求不要修改注释内容，本次未清理该注释空格。

### 剩余风险与回滚

- 未运行 Unity PlayMode；仍需验证 Player 触发 stage2 后能恢复普通移动，tools 触发 ball 后能打开并弹出 item，重复触发不会重复弹出。
- 未在 Unity Inspector 中确认两个新 API 是否已绑定到正确的 `OnTrigger2DHandle`；本次只补脚本入口，不保存 prefab。
- 若 Player 入口造成 stage2 时机错误，回滚 `EnterStage2AndRestorePlayerControl(...)` 绑定即可恢复旧的分离入口。
- 若 ball 打开/弹出表现异常，回滚 `OpenBallItemByTools(...)` 绑定或 `OpenAndPlayJumpOut()` 调用，保留已有 `SetOpenState()` / `SetClosedState()`。

## 2026-06-30 v2.2 DropTrigger/Player/tools 语义校正与 BallItem 排序补充

### 需求与背景

- 用户校正了 v2.2 触发语义：DropTrigger 触发到 `lock` 时变为 stage2；触发 `Player` 时恢复 Player 运动；触发 `tools` 时触发 `Level70BallItemCtrl`。
- 用户补充要求：`Level70BallItemCtrl` 打开时，`SortingGroup` 的 Order in Layer 变为 `180`。
- 本次未改写 `Level70Ctrl.cs` 顶部既有注释内容；实现只校正运行时代码和追加本地记录。

### 影响文件与系统

- `Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs`：`EnterStage2(Collider2D)` 改为只接受 `lock`；移除误加的 Player+stage2 合并入口；`OpenBallItemByTools(Collider2D)` 继续只接受 `tools`。
- `Assets/Game/Scripts/Level/LevelItem/Level_70/Level70BallItemCtrl.cs`：新增 `SortingGroup` 缓存、默认 order 记录和打开时 `openSortingOrder=180`；初始化或关闭时恢复默认 order。
- 本次不修改 `Level_070.prefab`、`OnTrigger2DHandle`、共享输入事件、`ActorCtrl`、`BaseLevelCtrl`、GamePlayPanel、存档、配置、资源加载、analytics、广告、IAP 或平台 SDK。

### 修改前行为基线

- 上一版实现把 Player 入口误合并为“进入 stage2 并恢复普通操作”，与用户最新校正不一致。
- 上一版 `EnterStage2(Collider2D)` 仍按旧理解接受 `tools`，但当前 prefab 静态文本已经存在 `targetTag: lock -> EnterStage2` 的绑定形态。
- `Level70BallItemCtrl.SetOpenState()` 只切换 open/close 视觉和 collider，不调整 ball 自身的 `SortingGroup` 排序层级。

### 修改后行为

- `EnterStage2(Collider2D)` 现在只接受 tag 为 `lock` 的 collider；通过后进入 `Complete` 阶段并触发 `onStage2Entered`。
- `RestoreActorControl(Collider2D)` 继续作为 Player 恢复运动入口；它不会进入 stage2。
- `OpenBallItemByTools(Collider2D)` 继续只接受 tag 为 `tools` 的 collider；解析到 `Level70BallItemCtrl` 后调用 `OpenAndPlayJumpOut()`。
- `OpenAndPlayJumpOut()` 调用 `SetOpenState()` 后会把 ball 自身或子节点 `SortingGroup.sortingOrder` 设置为 `180`，再播放 `ItemJumpOutExecutor.Play()`。
- `InitializeContent(Sprite)` 和 `SetClosedState()` 会把 `hasOpened=false`，并恢复进入运行时后缓存到的原始 `SortingGroup.sortingOrder`。

### 数据、资源、事件与平台流

- 事件流：继续使用 `OnTrigger2DHandle.OnEnter(Collider2D)`，不新增 `PlayEvents` 或全局事件。
- 资源流：不新增资源路径；排序只修改当前 ball 上现有 `SortingGroup` 的运行时 order。
- 数据流：不读写 DB、PlayerPrefs、RemoteConfig、Luban 或存档字段。
- 平台流：无 Android/iOS 专用分支；移动端仍需验证打开、弹出和排序遮挡表现。

### 验证记录

- 静态 prefab 扫描确认当前 `Level_070.prefab` 中存在 `targetTag: Player -> RestoreActorControl`、`targetTag: tools -> OpenBallItemByTools`、`targetTag: lock -> EnterStage2` 的绑定文本。
- 静态扫描确认 `Level70Ctrl.cs` 不再包含 `EnterStage2AndRestorePlayerControl`，并存在 `ToolsTag="tools"`、`LockTag="lock"`。
- 静态扫描确认 `Level70BallItemCtrl.cs` 存在 `SortingGroup`、`openSortingOrder=180`、`SetOpenSortingOrder()` 和 `RestoreSortingOrder()`。
- 编译：`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，0 warning / 0 error。
- 风险词静态扫描：本次新增 Level70 脚本未命中 DB、Save、RemoteConfig、PlayerPrefs、StreamingAssets、OnValidate、ExecuteAlways、ExecuteInEditMode、Instantiate、PlayEvents 或 Debug.Log。
- 空白检查：`git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs Assets/Game/Scripts/Level/LevelItem/Level_70/Level70BallItemCtrl.cs Assets/Game/Scripts/Level/LevelItem/Level_70/Level70抓钩控制功能说明.md Assets/Game/Scripts/Level/LevelItem/Level_70/Level70抓钩控制风险评估.md DevelopMD/已有功能设计与使用记录.md` 未通过，仅命中 `Level70Ctrl.cs` 顶部 `version 2.2` 注释行尾空格；因用户要求不修改注释内容，未清理这些空格。

### 剩余风险与回滚

- 未运行 Unity PlayMode；仍需验证 DropTrigger 命中 `lock` 后进入 stage2、Player 触发只恢复运动、tools 打开 ball 并弹出 item、打开 ball 的显示层级提升到 180。
- 未在 Unity Inspector 中逐项确认 `OnTrigger2DHandle` 的触发尺寸、层级、targetTag 和 UnityEvent 绑定。
- 如果 `SortingGroup` 实际挂在非当前 ball 层级，运行时兜底可能找不到目标，需要在 Inspector 中补序列化引用或调整解析范围。
- 回滚脚本时，可恢复 `EnterStage2(Collider2D)` 的旧 tag 判断，删除 `Level70BallItemCtrl` 中排序字段和 `SetOpenSortingOrder()` / `RestoreSortingOrder()`；绑定回滚时恢复对应 `OnTrigger2DHandle` 的旧方法配置。

## 2026-07-01 v2.2 PlayWallCrushAnim API 补充

### 需求与背景

- 用户要求完善 `Level70Ctrl.PlayWallCrushAnim` API 功能，使 stage2 墙体破碎表现可以作为明确的运行时入口使用。
- 修改前 `EnterCompletePhase()` 会直接调用私有 `PlayWallCrushAnim()`；该方法缺少 `Application.isPlaying` 外层保护、空引用兜底、重复触发保护、tween 生命周期清理和重置恢复。
- 本次不改写 `Level70Ctrl.cs` 顶部既有需求注释，不修改 `Level_070.prefab`、共享 `OnTrigger2DHandle`、输入、UI、存档、配置、资源加载、analytics、广告、IAP 或平台 SDK。

### 影响文件与系统

- `Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs`：将墙体破碎表现改为公开 `PlayWallCrushAnim()`，并新增 `PlayWallCrushAnim(Collider2D)` 以兼容 `OnTrigger2DHandle.OnEnter(Collider2D)` 绑定。
- `crushWall1` / `crushWall2`：作为 Level70 本地墙体破碎表现引用，前者在播放时隐藏，后者在播放时显示、淡出并移动。
- DOTween：新增独立 `wallCrushSequence` 追踪墙体表现，清理时 kill 该 sequence 与 `crushWall2` 相关 tween。

### 修改后行为

- `EnterCompletePhase()` 保留原有 stage2 调用路径，会在首次进入 `Complete` 时调用 `PlayWallCrushAnim()`，然后继续触发 `onStage2Entered`。
- `PlayWallCrushAnim()` 仅在 PlayMode 且本轮尚未播放过墙体破碎时生效；重复调用会被忽略，直到关卡重置、失败、成功或退出触发运行时清理。
- `PlayWallCrushAnim(Collider2D other)` 只接受空参数或 tag 为 `lock` 的 collider；用于避免 Player/tools 误绑定后触发墙体表现。
- 播放前会缓存并恢复 `crushWall2` 初始 localPosition 与 color，然后隐藏 `crushWall1`、显示 `crushWall2`、在 0.5 秒内将 `crushWall2` 淡出并移动到 localX = -1，完成后隐藏 `crushWall2`。
- `ResetHookRuntime(...)` 在 PlayMode 清理时调用 `ResetWallCrushAnim()`，恢复墙体初始显隐、颜色和位置；编辑器态不会执行墙体重置，避免非运行时改动序列化对象状态。
- 如果 `crushWall1` 或 `crushWall2` 未绑定，API 会用 `Bear.Logger` 输出 warning 并跳过墙体表现，不阻塞 stage2 状态切换。

### 验证记录

- 静态扫描确认 `PlayWallCrushAnim()`、`PlayWallCrushAnim(Collider2D)`、`wallCrushSequence`、`ResetWallCrushAnim()` 和 `TryPrepareWallCrushAnim()` 已存在于 `Level70Ctrl.cs`。
- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs` 通过，仅有工作区 LF/CRLF 转换提示。
- 风险词静态扫描未发现本次新增 Level70 墙体 API 命中 `DB`、`Save`、`RemoteConfig`、`PlayerPrefs`、`StreamingAssets`、`OnValidate`、`ExecuteAlways`、`ExecuteInEditMode`、`Instantiate`、`PlayEvents` 或 `Debug.Log`。
- 编译：`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 error / 254 warnings；warnings 属于当前工程输出，未逐项审计。

### 剩余风险与回滚

- 未运行 Unity PlayMode，仍需验证进入 stage2 时墙体切换、淡出、移动、隐藏，以及重开、失败、成功、退出后的墙体恢复。
- 未在 Inspector 中确认 `crushWall1` / `crushWall2` 引用是否完整绑定；缺引用时脚本会跳过表现但 stage2 仍继续。
- 未运行 Android/iOS 横屏触摸 smoke；本次无移动端分支，但仍需验证 Level70 stage2 全流程无卡死。
- 回滚时删除 `PlayWallCrushAnim(Collider2D)`、`wallCrushSequence`、墙体初始状态缓存、`ResetWallCrushAnim()`、`KillWallCrushAnim(...)` 和 `TryPrepareWallCrushAnim()`，并把 `PlayWallCrushAnim()` 恢复为原私有简单表现即可。

## 2026-07-01 v2.2 catchArea tag 白名单修正

### 需求与背景

- 用户要求修正 `catchArea` 的检测模式：除 `lock`、`tools`、`Player` 之外，其它碰撞体都不能触发下落过程收束。
- 修改前 `IsHookContactCollider(...)` 在排除 `catchArea` 自身和抓钩层级后，会接受任意外部 active collider；背景、边界或其它 trigger 可能提前触发抓钩回 `dropPoint`。

### 影响文件与系统

- `Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs`：新增本地 `IsCatchAreaTriggerTag(Collider2D)`，并在 `IsHookContactCollider(...)` 中要求 tag 必须为 `lock`、`tools` 或 `Player`。
- 本次不修改 `Level_070.prefab`、共享 `OnTrigger2DHandle`、输入事件、UI、存档、配置、资源加载、analytics、广告、IAP 或平台 SDK。

### 修改后行为

- Catch 下落阶段 `OnCatchAreaEnter(...)` 与 `UpdateCatchAreaOverlap()` 仍共用 `IsHookContactCollider(...)`。
- 只有 tag 为 `lock`、`tools` 或 `Player` 的外部 active collider 会触发 `RecoverAfterContact()`，并进入现有回 `dropPoint` 的收束流程。
- 其它 tag 的 collider 会被忽略，不会关闭抓钩、不会中断下落、不会触发墙体/ball/Player 相关 API。

### 验证记录

- 静态代码扫描确认 `IsHookContactCollider(...)` 已调用 `IsCatchAreaTriggerTag(...)`，白名单只包含 `LockTag`、`ToolsTag`、`ActorTag`。
- 空白检查：`git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs Assets/Game/Scripts/Level/LevelItem/Level_70/Level70抓钩控制功能说明.md Assets/Game/Scripts/Level/LevelItem/Level_70/Level70抓钩控制风险评估.md DevelopMD/已有功能设计与使用记录.md` 通过，仅有工作区 LF/CRLF 转换提示。
- 风险词静态扫描未发现本次新增 Level70 逻辑命中 `DB`、`Save`、`RemoteConfig`、`PlayerPrefs`、`StreamingAssets`、`OnValidate`、`ExecuteAlways`、`ExecuteInEditMode`、`Instantiate`、`PlayEvents` 或 `Debug.Log`。
- 编译：`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 未通过，错误为 `Assets/Game/Scripts/Play/PlayEvents.cs` 中 `Game.Events.PostcardGetStarEvent` 重复定义；该错误不在本次 Level70 修改文件内。

### 剩余风险与回滚

- 未运行 Unity PlayMode，仍需验证 `lock`、`tools`、`Player` 三类 tag 能触发收束，其它 tag 不能触发。
- 如果某些 ball 子碰撞体实际没有 `tools` tag，而依赖父级或名字识别，新的白名单会使它不再触发 `catchArea` 收束；需要在 Inspector 中确认参与接触的 collider tag。
- 当前 `DevelopMD/已有功能设计与使用记录.md` 在 Git 状态中为 `UU` 未合并状态；本次只追加 Level70 事实记录，未解析该文件已有合并状态。
- 回滚时删除 `IsCatchAreaTriggerTag(...)` 调用，恢复 `IsHookContactCollider(...)` 对任意外部 active collider 生效的旧逻辑。

## 2026-07-01 v2.3 Level70Ctrl 无效 API 清理

### 需求与背景

- 用户要求清理 `Level70Ctrl` 脚本，移除当前已经无效的 API。
- 静态引用检查确认当前 `Level_070.prefab` 只绑定 `RestoreActorControl(Collider2D)`、`OpenBallItemByTools(Collider2D)`、`EnterStage2(Collider2D)`；运行时只通过 `hookControlTrigger.OnEnter` 注册 `EnterHookControl(Collider2D)`。
- v2 触发语义已经取消 v1 的“抓取目标、挂到 catchArea、搬运到 dropPoint、释放目标”流程，相关私有方法和目标缓存结构继续保留会误导后续维护。

### 影响文件与系统

- `Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs`：移除旧目标搬运缓存和死代码，收窄未绑定公开入口。
- `Assets/Game/Prefabs/Level/Level_070.prefab`：删除脚本已移除字段 `catchAreaPosition` 与 `releaseRecoverDelay` 的序列化残留。
- 本次不修改共享 `OnTrigger2DHandle`、输入事件、`ActorCtrl`、`BaseLevelCtrl`、`GamePlayPanel`、存档、配置、资源加载、analytics、广告、IAP 或平台 SDK。

### 修改后行为

- `catchArea` 仍由 `UpdateCatchAreaOverlap()` 轮询；命中白名单 tag 后调用私有 `HandleCatchAreaContact(Collider2D)`，再走 `RecoverAfterContact()` 收束。
- `OnCatchAreaEnter(Collider2D)`、无参 `EnterHookControl()`、公开 `PlayWallCrushAnim()`、公开 `PlayWallCrushAnim(Collider2D)`、公开无参 `RestoreActorControl()` 已移除或收回为私有实现，不再作为外部 API。
- 旧 v1 目标搬运路径已移除：`HookTargetRuntime`、`HookTargetKind`、`TryResolveTarget(...)`、`ResolveFallbackItemRoot(...)`、`CreateTarget(...)`、`PauseTargetPhysics(...)`、`AttachTargetToCatch(...)`、`ReleaseCurrentTarget(...)`、`ClearCurrentTarget(...)` 和 `CopyTargetRuntime(...)` 不再存在。
- `HookControlState` 只保留 `Idle`、`Lowering`、`Recovering` 三个实际状态；接触或 miss 后仍关闭抓钩，回到 `dropPoint` 高度和 x 坐标，再短暂打开后恢复 `Idle`。
- `PlayWallCrushAnim()` 仍由 `EnterCompletePhase()` 内部调用，墙体破碎表现不再暴露给 Inspector 额外绑定。

### 验证记录

- 静态绑定扫描确认 `Level_070.prefab` 当前只引用 `RestoreActorControl`、`OpenBallItemByTools`、`EnterStage2` 三个 `Level70Ctrl` 方法。
- 静态符号扫描未发现 `HookTargetRuntime`、`HookTargetKind`、`currentTarget`、`OnCatchAreaEnter`、`catchAreaPosition`、`releaseRecoverDelay`、公开 `PlayWallCrushAnim` 或旧搬运方法残留。
- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs Assets/Game/Prefabs/Level/Level_070.prefab` 通过，仅有工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 首次重跑 124 秒超时，未返回编译错误；随后以 300 秒超时补跑通过，结果为 0 warning / 0 error。

### 剩余风险与回滚

- 未运行 Unity PlayMode；仍需验证 Catch 接触、miss 回收、stage2、Player 恢复、tools 打开 ball、墙体破碎、重开/失败/成功/退出流程。
- `Level_070.prefab` 在本次前已有其它未提交脏改；本次只删除两个无效序列化字段，未接管其它层级、位置或参数差异。
- 如果后续重新需要 v1 自动搬运目标，需要重新按新需求恢复目标缓存和物理恢复流程，而不是依赖本次已删除的旧死代码。
- 回滚时恢复 `Level70Ctrl.cs` 中旧目标搬运结构和公开入口，并在 `Level_070.prefab` 恢复 `catchAreaPosition` / `releaseRecoverDelay` 字段即可；但这会重新暴露当前 v2 已废弃的抓取搬运路径。

## 2026-07-01 ModifyActorCollider 最小完善

### 需求与背景

- 用户要求最小化完善 `Level70Ctrl.ModifyActorCollider()`。
- 本地代码证据显示 `RestoreActorControl(Collider2D)` 会先调用私有 `RestoreActorControl()`，随后无条件调用 `ModifyActorCollider()`；而私有恢复方法会在 `Level70ActorCtrl.RestoreLevel70NormalControl()` 返回失败时只清理 `hookMoveInput` 并提前返回。
- 修改前 `ModifyActorCollider()` 直接访问 `actor.GetComponent<Collider2D>()`，当 `actor`、Actor 根节点 `Collider2D` 或 `Stuff` 层缺失时存在空引用或无效 layer mask 风险。

### 影响文件与系统

- `Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs`：让私有 `RestoreActorControl()` 返回恢复结果，并只在恢复成功后调用 `ModifyActorCollider()`；为 `ModifyActorCollider()` 增加 actor、Collider2D、`Stuff` layer 防护。
- `Assets/Game/Scripts/Level/Actor/ActorCtrl.cs`：只为既有公共方法 `SwitchWhatIsStuff(LayerMask)` 补充中文 XML 注释，说明用途、调用边界和注意事项；未改变方法实现。
- 本次不修改 `Level_070.prefab`、共享输入、共享 Trigger、UI、存档、配置、资源加载、analytics、广告、IAP 或平台 SDK。

### 修改后行为

- Player/actor 恢复入口仍由 `RestoreActorControl(Collider2D)` 过滤触发 collider，非 actor collider 仍被忽略。
- 当 `Level70ActorCtrl.RestoreLevel70NormalControl()` 拒绝恢复时，Level70 只清理 `hookMoveInput`，不会继续修改 Actor collider 的 `excludeLayers` 或 `whatIsStuff`。
- 当 actor 恢复成功且 Actor 根节点存在 `Collider2D`、项目存在 `Stuff` layer 时，继续把 Actor collider 排除 `Stuff` 层，并通过 `SwitchWhatIsStuff(default)` 关闭 Actor 推物检测。
- 当必要引用缺失时，`ModifyActorCollider()` 使用 Bear.Logger warning 跳过，不抛出运行时异常。

### 验证记录

- 静态 diff 确认本次只改变恢复入口返回值处理、`ModifyActorCollider()` 防护和 `SwitchWhatIsStuff(...)` 注释。
- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs` 已通过，仅有工作区 LF/CRLF 转换提示。
- 风险词静态扫描未发现本次 `Level70Ctrl.cs` 新增 `OnValidate`、`ExecuteAlways`、`ExecuteInEditMode`、`DB`、`Save`、`RemoteConfig`、`PlayerPrefs`、`StreamingAssets`、`Resources`、`Instantiate`、`PlayEvents` 或 `Debug.Log` 链路。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 首次 124 秒超时，已停止该孤立构建进程；随后以 300 秒超时补跑通过，结果为 0 error / 144 warnings。

### 剩余风险与回滚

- 未运行 Unity PlayMode；仍需验证 Player 恢复触发器、controlhandle 期间拒绝恢复、恢复成功后的 Actor 与 Stuff 碰撞关系、失败/重开/退出路径。
- 未运行 Android/iOS 横屏触摸 smoke；本次无平台分支，但 Level70 恢复链路仍需设备补验。
- `ActorCtrl.SwitchWhatIsStuff(default)` 会把推物检测层清空；这是当前 `ModifyActorCollider()` 既有目标行为，本次只限制其执行时机和异常防护。
- 回滚时把私有 `RestoreActorControl()` 改回 `void`，恢复 public 入口无条件调用 `ModifyActorCollider()`，并移除 `ModifyActorCollider()` 的三段防护即可；`ActorCtrl` 注释可保留，也可随回滚删除。

## 2026-07-21 Level70BallItemCtrl 打开后冻结

### 需求与背景

- 用户要求 `Level70BallItemCtrl` 打开之后不再移动。
- 用户进一步指定打开等待时间不再固定，改为每次在 `1.5～3` 秒区间内随机取值。
- 修改前 `OpenAndPlayJumpOut()` 会先给球施加水平冲量，等待 `ballOpenDelay` 后调用 `SetOpenState()`；打开态没有停止刚体，残余速度、重力或后续碰撞仍可能继续改变球的位置和旋转。

### 影响文件与系统

- `Assets/Game/Scripts/Level/LevelItem/Level_70/Level70BallItemCtrl.cs`：缓存球初始 `Rigidbody2D.constraints`，打开时清零速度并冻结；把固定 `ballOpenDelay` 改为 `ballOpenDelayRange`，每次打开请求随机一次等待时间。
- `Assets/Game/Prefabs/Level/Level_070.prefab`：9 个 `Level70BallItemCtrl` 实例均从 `ballOpenDelay = 2` 改为 `ballOpenDelayRange = (1.5, 3)`。
- 本次未修改 `Level70Ctrl`、碰撞层配置、Item 弹出执行器、UI、存档、配置、资源加载、事件、analytics、广告、IAP 或平台 SDK。

### 修改后行为

- 打开前既有水平冲量保持不变；等待时长由 `GetRandomOpenDelay()` 在 `1.5～3` 秒内随机一次，等待结束进入 `SetOpenState()` 时停止球运动。
- `ballOpenDelayRange.x/y` 在运行时自动按较小值和较大值排序，负数按 0 秒处理，避免 Inspector 反向填写或负值导致无效等待。
- 打开态会把 `linearVelocity`、`angularVelocity` 清零并冻结全部刚体约束，避免残余速度、重力和碰撞继续推动或旋转球。
- `InitializeContent()` 与 `SetClosedState()` 会恢复该实例首次解析刚体时记录的原始约束，使关卡初始化、重开或关闭态仍按原有物理配置运行。
- 打开态 collider 的既有启用状态及 `excludeLayers` 逻辑保持不变；本次只收束球的运动，不改变 Item 弹出、排序层和内容 Sprite 流程。
- 因为 `SetOpenState()` 现在会改变刚体约束，原注释中“只影响视觉和 collider”的表述已与代码事实冲突，本次仅把该句补充为“视觉、collider 和球刚体约束”。

### 验证记录

- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_70/Level70BallItemCtrl.cs Assets/Game/Prefabs/Level/Level_070.prefab` 通过，仅有 Git 的 LF/CRLF 工作区提示。
- 静态资源扫描确认 `Level70BallItemCtrl` 组件 GUID 只出现在 `Level_070.prefab`；该 prefab 的 9 个实例全部为 `(1.5, 3)`，Assets 内 prefab/scene 已无旧 `ballOpenDelay:` 字段。
- 风险词静态扫描未发现新增 `OnValidate`、`Reset()`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、RemoteConfig、PlayerPrefs、资源加载、实例化、`PlayEvents` 或 `Debug.Log` 链路。
- `dotnet build .\Assembly-CSharp.csproj --no-restore ...` 首次因 `Temp/obj/Assembly-CSharp/project.assets.json` 不存在而未进入编译；随后执行标准 `dotnet build .\Assembly-CSharp.csproj ...` 恢复并构建通过，结果为 0 error / 1117 warnings。
- 随机区间调整后再次执行 `dotnet build .\Assembly-CSharp.csproj --no-restore ...`，结果为 0 error / 1117 warnings。

### 剩余风险与回滚

- 未运行 Unity PlayMode；需多次触发确认实际等待均落在 1.5～3 秒、不同球可独立随机，且打开瞬间停止、Item 弹出动画正常。
- 未验证关卡重开后球约束恢复、再次打开仍能冻结；未运行 Android/iOS 横屏设备 smoke。
- 打开态 collider 仍保持既有行为，静止球是否会阻挡其它物体需在 Level70 实际布局中确认。
- 只回滚随机延迟时，把脚本恢复为固定 `ballOpenDelay`，并把 prefab 的 9 个 `ballOpenDelayRange` 恢复为 `ballOpenDelay: 2`；冻结逻辑可独立保留。
- 整体回滚时再删除初始约束缓存、`FreezeOpenedRigidbody()`、`RestoreRigidbodyConstraints()` 及三个调用点，并恢复 `SetOpenState()` 原注释。

## 2026-07-21 dropArea SpriteRenderer Sorting Order 修正

### 需求与问题原因

- 用户要求 ball 通过 dropArea 后立即把球壳 `SpriteRenderer` 的 Sorting Order in Layer 改为 180，内部 Item 打开弹出时把自身 Sorting Order 从 50 改为 200；明确不使用 `SortingGroup` 字段完成该需求。
- 本地 prefab 证据显示内部 Item 节点挂的是 `SpriteRenderer` 与 `ItemJumpOutExecutor`，没有子 `SortingGroup`；原代码调用 `jumpOut.GetComponentInChildren<SortingGroup>().sortingOrder = 200`，因此不会写到 Item 的 `SpriteRenderer.sortingOrder = 50`，并存在空引用异常风险。

### 影响文件与系统

- `Assets/Game/Scripts/Level/LevelItem/Level_70/Level70BallItemCtrl.cs`：移除 `SortingGroup` 引用与序列化字段，缓存球壳 close/open 子树的 SpriteRenderer 及原始 order；dropArea 触发入口立即写 180，Item 弹出前直接写已绑定 SpriteRenderer 为 200。
- `Assets/Game/Prefabs/Level/Level_070.prefab`：移除 `Level70BallItemCtrl.sortingGroup` 的空序列化字段；不删除关卡层级中既有的实际 `SortingGroup` 组件。
- 当前静态扫描发现 prefab 已有 11 个 `Level70BallItemCtrl`，全部配置 `openSortingOrder = 180` 和 `ballOpenDelayRange = (1.5, 3)`；其中 3 个 `sprite` 字段为空，脚本会优先从对应 `jumpOut` 节点解析内部 Item SpriteRenderer。

### 修改后行为与数据流

- `OpenBallItemByTools()` 调用 `OpenAndPlayJumpOut()` 后，脚本在水平冲量和随机等待之前立即遍历已缓存的球壳 SpriteRenderer，将各自 `sortingOrder` 写为 180。
- 随机等待结束后，Item 先脱离 ball 父节点，再把内部 Item 的 `SpriteRenderer.sortingOrder` 写为 200，随后调用 `ItemJumpOutExecutor.Play()`。
- `InitializeContent()` 和 `SetClosedState()` 会恢复球壳各 Renderer 的原始 order，并把内部 Item 恢复到首次解析时记录的 order（当前 prefab 静态值为 50）。
- Item Renderer 解析顺序为：已序列化 `sprite` → `jumpOut` 同节点 SpriteRenderer → `jumpOut` 子节点 SpriteRenderer → ball 子树兜底，降低未绑定实例改错球壳 Renderer 的风险。
- 本次不新增全局事件、存档、配置、资源加载、UI、analytics、广告、IAP 或平台 SDK 链路；排序写入只发生在初始化、关闭、dropArea 触发和打开这些离散时点。

### 验证记录

- CodeGraph 与 prefab YAML 共同确认旧 Item 节点没有 SortingGroup，50 来自 SpriteRenderer；旧 `GetComponentInChildren<SortingGroup>()` 调用已无残留。
- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_70/Level70BallItemCtrl.cs` 通过，仅有 LF/CRLF 工作区提示。
- `Level_070.prefab` 整体 `git diff --check` 被当前并行 prefab 改动中的空值行尾空格阻断，命中 11533、11670、11671、11716、11717 行；这些不是本次排序字段修改，未擅自清理。
- 最终 `dotnet build .\Assembly-CSharp.csproj --no-restore ...` 通过，结果为 0 error / 99 warnings。

### 剩余风险与回滚

- 未运行 Unity PlayMode；需在 Inspector 运行时确认通过 dropArea 后球壳 SpriteRenderer 立即为 180，等待结束后内部 Item 从 50 变为 200，并验证 3 个未绑定 sprite 的球是否解析到正确 Item。
- ball 根节点仍存在实际 SortingGroup 组件；子 SpriteRenderer 的 Inspector order 会按需求变化，但相对组外对象的最终整体排序仍可能受根 SortingGroup order 控制，需结合实际遮挡结果验证。
- 回滚时恢复 SortingGroup 字段与旧排序方法、删除球壳 Renderer 缓存/恢复逻辑，并把 Item 写入恢复为旧查找代码；prefab 可恢复空 `sortingGroup` 序列化字段，但该字段原本没有有效绑定。

## 2026-07-21 dropArea SortingGroup 与 Item SpriteRenderer 分层修正

### 需求与背景

- 用户进一步明确两个 `Order in Layer` 的目标组件不同：ball 通过 dropArea 后修改 ball 根节点 `SortingGroup.sortingOrder = 180`；内部 Item 打开弹出时修改 Item 自身 `SpriteRenderer.sortingOrder = 200`。
- 上一版把 ball 的 close/open 子树 `SpriteRenderer` 逐个写为 180，目标组件与最新需求不一致；本节修正并覆盖上一节关于 ball 排序目标的描述。

### 影响文件与修改后行为

- `Level70BallItemCtrl.cs` 恢复并显式区分 `sortingGroup` 与 `sprite`：`OpenAndPlayJumpOut()` 进入后立即写 ball 根 `SortingGroup` 为 180；随机等待结束、Item 改父节点后写内部 Item `SpriteRenderer` 为 200。
- `Level_070.prefab` 当前 11 个 `Level70BallItemCtrl` 都显式绑定各自根节点 `SortingGroup`，并显式保存 `openSortingOrder = 180`、`openItemSortingOrder = 200`、`ballOpenDelayRange = (1.5, 3)`。
- `InitializeContent()` 与 `SetClosedState()` 分别恢复 ball 根 SortingGroup 和内部 Item SpriteRenderer 的初始 order；不再扫描或改写球壳子 Renderer。
- 现有冻结、随机延迟、Item 弹出、碰撞层、存档、配置、资源和事件链保持不变。

### 验证与回滚

- 静态 YAML 映射确认 11 个 ball 根对象均存在 SortingGroup，缺失数为 0；11 个脚本组件均已显式绑定对应组件。
- `dotnet build Assembly-CSharp.csproj --no-restore` 通过，结果为 0 error / 99 warnings；脚本 `git diff --check` 通过。
- 未运行 Unity PlayMode；仍需确认通过 dropArea 后 ball 根 SortingGroup 立即为 180、Item 打开时 SpriteRenderer 从 50 变为 200，以及实际遮挡效果。
- 回滚时可恢复上一版子 SpriteRenderer 缓存与遍历，并移除 prefab 的 `sortingGroup` / `openItemSortingOrder` 显式字段；不涉及存档或配置回滚。

## 2026-07-21 catch 控制职责拆分

### 需求与背景

- 用户要求把 `Level70Ctrl` 中钩子/catch 相关控制独立成单独脚本，降低关卡编排与机械运动混在同一控制器内的维护成本。
- 拆分前 `Level70Ctrl` 同时负责全局输入事件订阅、角色控制权、GameplayPanel 切换、阶段/墙体/ball 流程，以及 catch 的状态机、Tween、重叠检测、范围和 Animator 开合。
- 本次是职责拆分，不计划改变玩家可见的 catch 移动、下落、接触回收、miss 回收、dropPoint 打开等待或 stage2 范围行为。

### 影响文件、资源与职责边界

- 新增 `Level70HookCtrl.cs/.meta`：仅用于 Level70，负责 `Idle/Lowering/Recovering` 状态、水平移动、catchArea 重叠检测、下落和回收 Tween、`isOpen` Animator 参数、playing/stage2 范围与初始位置恢复。
- `Level70Ctrl.cs`：保留 `PlayerRightMoveEvent`、`PlayerLeftMoveEvent`、`PlayerMoveCancelEvent`、`PlayerJumpEvent`、重置和游戏状态事件订阅；把输入值和阶段切换直接委托给 `Level70HookCtrl`。角色锁定/恢复、Level70GamePlayPanel、入口 Trigger、墙体、ball 初始化和 UnityEvent 仍由它负责。
- `Level_070.prefab`：在 Level70 根节点新增一个 `Level70HookCtrl`，`Level70Ctrl.hookCtrl` 显式绑定该组件；原 catch 引用与数值从 `Level70Ctrl` 原样迁移到新组件。
- 没有新增 `PlayEvents`、全局事件、共享 `GamePlayPanel` 修改、实例化、存档、配置、资源加载、analytics、广告、IAP 或平台 SDK 链路。

### 行为与数据流

- 输入链保持为：Bear `EventSubscriber` → `Level70Ctrl` 输入回调 → `Level70HookCtrl.SetMoveInput(...)` / `TryStartCatch()`；新 catch 组件不自行订阅全局事件。
- 进入链保持为：`OnTrigger2DHandle` → `Level70Ctrl.EnterHookControl(...)` → 校验 actor → 关闭入口 Trigger、锁定角色、切换 Catch 按钮 → `Level70HookCtrl.ActivateControl()`。
- stage2 链保持为：`EnterStage2(...)` → `Level70Ctrl.EnterCompletePhase()` → `Level70HookCtrl.EnterCompletePhase()` 夹紧范围 → 墙体破碎 → `onStage2Entered`。
- 清理链保持为：GameReset / SUCCESS / FAILED / OnDisable / OnDestroy → `Level70Ctrl.ResetHookRuntime(...)` → `Level70HookCtrl.ResetRuntime()`，随后恢复墙体、角色控制权和 ball 内容。
- prefab 参数保持为：`hookMoveSpeed=3`、`lowerDuration=2`、`missRecoverDuration=0.45`（当前 v2 仍未使用）、`retractDuration=3`、`moveToDropDuration=1`、`fallbackLowerDistance=5`，范围 Transform 和 fallback 数值不变。

### 验证、剩余风险与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly` 通过，结果为 0 error / 99 warnings。
- 静态 prefab 检查确认新脚本组件、根组件列表引用和 `Level70Ctrl.hookCtrl` 均各 1 个；`Level70Ctrl` 序列化块已不再持有 catch 机械字段。
- 静态代码检查确认 catch 状态/Tween/检测/范围方法只位于 `Level70HookCtrl`；事件订阅和三个既有 UnityEvent 入口仍位于 `Level70Ctrl`。
- 未运行 Unity PlayMode 与 Android/iOS 横屏 smoke；仍需验证 Awake/OnDisable 顺序、左右输入、Catch、接触/miss 回收、2 秒打开等待、stage2 范围、Player 恢复、重开/失败/成功/退出。
- 回滚时删除根节点 `Level70HookCtrl` 组件与脚本，把字段、状态和方法移回 `Level70Ctrl`，恢复输入回调直接操作旧字段即可；不涉及存档、配置、资源地址或平台数据回滚。

## 2026-07-21 catch 加速移动、边界撞击与去 Complete 状态

### 需求与原始行为

- 原实现通过 `Playing/Complete` 两个状态切换 range1/range2，水平移动是固定速度，`lock -> EnterStage2` 负责进入 `Complete` 并直接播放墙体破碎。
- 新需求不再要求 `Complete` 状态；catch 需要在完整行程内加速/减速，到边界时按实际速度判定，只有左边界高速撞击能破墙。

### 影响文件与修改后行为

- `Level70HookCtrl.cs`：水平速度使用 `Mathf.MoveTowards` 向输入目标速度加速，松开时按同一加速度减速到 0，反向时先减速再反向加速。
- 加速度按 `a = maxHorizontalSpeed² / (2 * 完整行程距离)` 运行时计算，使 catch 从一端静止出发并持续向另一端移动时，理论边界速度恰好等于 `maxHorizontalSpeed`。当前 prefab 行程为 x=-8.8 至 x=4.67，`maxHorizontalSpeed=3`，对应理论加速度约 0.334/秒²。
- 左/右边界都在实际触边帧捕获撞击速度。左边界严格超过 `maxHorizontalSpeed * crashSpeedRatio` 时调用 `Level70Ctrl.PlayLeftWallCrash()`；当前 `crashSpeedRatio=0.8`。右边界无论速度高低都不会破墙。
- 左侧速度未超过阈值时播放 `CrashEffect_Left`，右侧碰撞播放 `CrashEffect_Right`。效果先 `DOScaleX(0 -> 1)`，再 `DOFade(alpha -> 0)`；prefab 默认缩放时间 0.3 秒、淡出时间 0.5 秒，两个参数都暴露在组件上。
- 每一侧边界使用碰撞锁，持续按向墙体时不会每帧重复播放；离开边界后解锁。左墙破碎在本轮只触发一次，重置关卡后恢复。
- `Level70Ctrl.cs` 删除 `Level70Phase`、`EnterStage2(...)`、`EnterCompletePhase()` 和 `onStage2Entered`；墙体表现改为只由局部 API `PlayLeftWallCrash()` 触发。
- `Level_070.prefab` 的 hook 直接使用旧 range2 两端作为完整行程；旧 `lock -> EnterStage2` UnityEvent 清空，旧 range1/range2 Transform 节点保留但不再由状态切换。
- 没有新增全局事件、共享 `GamePlayPanel` 改动、存档、配置、资源加载、analytics 或平台 SDK 链路。

### 验证、未验证缺口与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore -t:Rebuild -v:minimal /clp:ErrorsOnly` 强制重建通过，结果为 0 error / 1117 warnings；本次 Level70 新增警告仅为兼容旧序列化保留但未读取的 `missRecoverDuration`。
- 静态扫描确认 C# 和 prefab 无 `Level70Phase` / `EnterStage2` / `EnterCompletePhase` / `onStage2Entered` 残留，左右失败 SpriteRenderer、完整行程和参数已显式绑定。
- 未运行 Unity PlayMode、Unity Console 检查或 Android/iOS 横屏 smoke；需实机确认端到端手感、80% 阈值、左侧破墙、右侧始终失败、效果方向/层级、持续按键去重和重开恢复。
- 回滚时可恢复 `Level70Phase` 与 `EnterStage2` 链，prefab 重绑 `lock -> EnterStage2`，再将 hook 水平逻辑恢复为固定速度及 range1/range2 切换；不需要回滚存档或配置数据。

## 2026-07-21 Level70 音效持有与销毁生命周期

### 需求与原始行为

- Level70 已补充 `l70Crash`、`l70FailCrash`、`l70PlayGame` 三个 SoundsGood SFX；左墙破碎和边界失败分别播放前两个一次性音效，进入抓钩控制时循环播放 `l70PlayGame`。
- 修改前 `l70PlayGame` 直接调用 `AudioManager.PlaySound(..., loop: true)`，没有保存返回的 `Sound`，因此无法定向停止。
- 用户确认重开会直接销毁 Level 实例，不需要在重开、成功、失败、禁用或销毁过程中恢复抓钩、墙体、actor、ball 等本地状态。

### 修改后行为与资源流

- `Level70Ctrl` 保存 `l70PlayGameSound` 句柄；每次启动前先停止旧句柄，再通过现有项目包装层 `Game.Scripts.Common.AudioManager` 播放 `SFX.l70PlayGame`，继续遵循现有 SFX 开关和活动音源管理。
- Player 成功通过独立恢复入口退出抓钩控制时立即停止循环音效；Level70 实例 `OnDestroy` 时再次幂等停止并清空句柄，防止全局 SoundsGood 音源在关卡对象销毁后残留。
- `OnDisable` 只解绑 Level70 的 Trigger/Bear 事件，不停止循环音效，也不复位本关运行状态；`OnDestroy` 除解绑外只承担外部循环音源释放。
- `Level70Ctrl` 不再订阅 `GameResetEvent` 与 `SwitchGameStateEvent`，移除重开、SUCCESS、FAILED 对本关本地状态的复位链。`Level70HookCtrl` 也不再在 `OnDisable/OnDestroy` 调用 `ResetRuntime()`。
- Awake 仍执行一次 `InitializeRuntime()`，用于新实例初始隐藏失败效果、恢复墙体初始外观并按 sprite 列表初始化 ball；这属于新 Level 实例初始化，不是旧实例清理。
- `l70Crash` 仍在左墙确认破碎后播放一次；`l70FailCrash` 仍在左右失败效果开始时播放一次。本次不修改它们的播放时机或资源。
- 资源静态证据：`SoundCollection.asset` 三个 tag 均绑定一个非空 AudioClip GUID，`SFX_Generated.cs` 已生成三个强类型 `SFX`；对应三个 ogg 文件均存在。

### 验证与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly` 通过：0 error / 99 warnings。
- 静态扫描确认 `Level70Ctrl` 不再包含 GameReset/SwitchGameState 清理订阅，`Level70HookCtrl` 不再包含禁用/销毁复位入口；循环音效只有一个持有字段，并在正常恢复与销毁两个边界停止。
- 未运行 Unity PlayMode、Unity Console 或 Android/iOS 横屏 smoke；需验证 SFX 开关关闭时空句柄、进入抓钩控制只启动一个循环、正常恢复立即停止、Disable 不停止、Destroy 停止、成功/失败到对象销毁之间是否存在可接受的继续播放窗口。
- 回滚时删除 `l70PlayGameSound` 与两个局部音效方法，把进入入口恢复为直接循环播放；如需恢复旧本地复位策略，重新订阅 GameReset/SwitchGameState 并恢复 Level70Ctrl/HookCtrl 的禁用销毁复位调用。音频资源和 SoundsGood tag 可独立保留。

## 2026-07-21 catch 最低起步速度

### 需求与行为变化

- 原加速模型从速度 0 开始，按 `a=maxHorizontalSpeed²/(2*行程)` 加速；用户要求增加最低速度，使 hook 按下方向后从最低值开始，并在完整行程另一端达到最大值。
- `Level70HookCtrl` 新增序列化字段 `minHorizontalSpeed`，prefab 默认值为 1，`maxHorizontalSpeed` 保持 3。运行时最低速度会限制在 0～最大速度之间，避免 Inspector 误填导致反向加速度。
- 新公式为 `a=(maxHorizontalSpeed²-minHorizontalSpeed²)/(2*完整行程)`。当前行程 13.47、最低速度 1、最大速度 3，对应加速度约 0.296956/秒²，理论端到端时间约 6.735 秒。
- 静止时按下方向会立即使用最低速度；持续同方向时逐步加速到最大速度。松开时按同一加速度减速，降到最低速度附近后直接归零；反向时先减速归零，下一帧从最低速度向新方向启动。
- 当最低速度为 0 时兼容上一版从 0 加速的行为；当最低速度等于或超过最大速度时，运行时按最大速度恒速起步，松开立即停止。
- 左墙破碎阈值仍是 `maxHorizontalSpeed * crashSpeedRatio`，当前为 3×0.8=2.4；最低速度不改变阈值定义。

### 验证与回滚

- 15/30/60/120 fps 数值模拟的端点速度均约为 2.999505，即最大速度的 99.984%；离散帧进产生微小误差，理论连续模型端点为 3。
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly` 通过：0 error / 99 warnings。
- 静态 prefab 检查确认 `minHorizontalSpeed=1`、`maxHorizontalSpeed=3`、`crashSpeedRatio=0.8` 已保存；没有新增事件、存档、配置、资源加载或跨关卡改动。
- 未运行 Unity PlayMode 和 Android/iOS 横屏 smoke；仍需确认起步跳变、松手停止、快速反向、低帧率边界速度和 80% 破墙手感。
- 回滚时删除 `minHorizontalSpeed` 和最低速度更新分支，把加速度恢复为 `max²/(2*行程)`，并移除 prefab 对应字段即可。

## 2026-07-21 catch 无左右输入立即停止

### 需求与修改后行为

- 上一版最低速度模型在松开左右输入后仍按加速度减速；用户要求没有左右移动触发时，hook 速度直接变为 0。
- `PlayerMoveCancelEvent` 仍由 `Level70Ctrl.OnPlayerMoveCancel` 转发为 `Level70HookCtrl.SetMoveInput(0)`；`SetMoveInput` 现在在收到 0 时同步清空 `hookMoveInput` 与 `horizontalVelocity`，不等待下一帧减速。
- `UpdateHorizontalVelocity` 对零输入也直接把水平速度设为 0，作为非事件入口或状态残留的逐帧兜底。
- 左右方向直接反转时仍属于有方向输入，继续沿用“先减速到 0，再从最低速度向新方向起步”；最低/最大速度、边界判定、破墙阈值、Catch、ball 和音效流程不变。

### 验证与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly` 通过：0 error / 99 warnings；静态调用链确认 Cancel 事件直接进入新的清零分支。
- 未运行 Unity PlayMode 和移动端触控验证；仍需确认左右按钮抬起、滑出按钮、双指左右同时操作和暂停恢复时是否都会正确派发 Cancel。
- 回滚时恢复 `SetMoveInput(0)` 只写输入值，并恢复零输入分支的 `Mathf.MoveTowards` 减速即可；不涉及 prefab、资源、存档或配置。

## 2026-07-21 左墙破碎前的 range1Min 限制

### 问题与修正后行为

- 去掉 `Complete` 阶段后，上一版抓钩进入控制即使用 `range2Min`，导致左墙尚未破碎时 Hook 可以越过 `range1Min`；这与“先撞击左墙，成功破墙后才开放左侧区域”的关卡语义不一致。
- `Level70HookCtrl` 现在分别持有破墙前左边界 `preCrashMovementRangeMin` 和破墙后左边界 `movementRangeMin`。当前 prefab 显式绑定为：`range1Min.x=-3.98`、`range2Min.x=-8.8`、右边界 `x=4.67`。
- 左墙未破碎时，水平位置只能到 `range1Min`。到达该点后，仅当向左速度严格大于 `maxHorizontalSpeed * crashSpeedRatio`，且 `Level70Ctrl.PlayLeftWallCrash()` 确实开始播放破墙表现时，才记录 `hasBrokenLeftWall=true` 并把活动左边界切换为 `range2Min`。
- 速度不足时仍在 `range1Min` 播放左侧失败效果，Hook 不会进入左墙外区域；右侧边界和失败表现不变。
- `PlayLeftWallCrash()` 改为返回布尔结果。非 PlayMode、已经破墙或破墙引用缺失时返回 false，Hook 不会仅因调用过接口就提前开放 `range2Min`。
- 本次没有恢复 `Complete` 状态，也没有增加全局事件；边界开放只由本关局部运行时标记 `hasBrokenLeftWall` 控制。

### 速度与范围关系

- 最低速度 1、最大速度 3 时，加速度仍使用活动范围计算：`a=(max²-min²)/(2s)`。
- 破墙前活动范围为 `-3.98～4.67`，距离 8.65，加速度约 0.46243/秒²，理论端到端约 4.325 秒；破墙后范围为 `-8.8～4.67`，距离 13.47，加速度约 0.296956/秒²。
- 破墙成功后，Hook 当时位于 `-3.98`，新的左边界变为 `-8.8`，原边界锁会因 Hook 已不在新边界而在后续帧释放，可以继续向左移动。

### 验证与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly` 通过：0 error / 99 warnings。
- prefab 静态检查确认破墙前、破墙后和右侧三个边界引用分别绑定到 `range1Min`、`range2Min` 和右侧范围点；静态调用扫描确认破墙接口只有 Hook 使用。
- 未运行 Unity PlayMode、Unity Console 或 Android/iOS 横屏验证；仍需验证慢速左撞停在 `range1Min`、高速左撞破墙后可越过该点、破墙表现引用缺失时不开放范围，以及右侧行为不变。
- 回滚时移除 `preCrashMovementRangeMin`、`fallbackPreCrashMovementRangeMinX` 和 `hasBrokenLeftWall` 对活动范围的选择，把左边界恢复为固定 `movementRangeMin`；同时可把 `PlayLeftWallCrash()` 恢复为无返回值。无需回滚存档、配置、资源或事件。

## 2026-07-21 ball item 打开时兼容 jumpOut SortingGroup

### 需求与修改后行为

- 在既有“内部 Item 的 `SpriteRenderer.sortingOrder=200`”基础上，打开 ball 时额外检查 `jumpOut` 执行器所在 GameObject 是否挂有 `SortingGroup`；存在时同步把其 `sortingOrder` 设置为 `openItemSortingOrder`，当前配置值为 200。
- 检查目标限定为 `jumpOut.GetComponent<SortingGroup>()`，不向子树扫描，避免误改 ball 根 `SortingGroup` 或其它装饰节点的排序组。
- `SpriteRenderer` 的 200 逻辑继续保留；`jumpOut` 没有 `SortingGroup` 时只跳过该组件，不抛空引用，也不影响弹出动画。
- 解析到 `jumpOut SortingGroup` 时会缓存初始 Order in Layer；现有 `InitializeContent()` / `SetClosedState()` 恢复 Item 排序时会一并恢复该初始值。
- ball 根 `SortingGroup=180`、随机打开延迟 1.5～3 秒、打开后冻结、Item 改父节点和 `jumpOut.Play()` 时序不变。本次没有修改 prefab、资源、事件、存档或配置。

### 本地证据、验证与回滚

- 当前 `Level_070.prefab` 静态扫描到 11 个 `jumpOut` 引用，其执行器所在 GameObject 均未挂 `SortingGroup`；因此本次是面向后续/变体配置的空安全兼容，当前 prefab 仍由 Item `SpriteRenderer=200` 生效。
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly` 通过：0 error / 99 warnings；脚本 diff check 清理后通过。
- 未运行 Unity PlayMode；需在某个 `jumpOut` 同对象挂入 SortingGroup 后验证打开时 Group 与 SpriteRenderer 均为 200、关闭恢复原值，并确认实际遮挡符合预期。
- 回滚时删除 `jumpOutSortingGroup` 的解析、初始值缓存、打开赋值和恢复分支即可；原有 SpriteRenderer 200 与 ball 根 SortingGroup 180 可独立保留。

## 2026-07-21 左墙 crash 成功震屏 Feedback 接入

### 需求与修改后行为

- `Level70Ctrl` 新增序列化 `MMF_Player crashSuccessFeedback`，供 `Level_070.prefab` 绑定可在 Feedback Inspector 中调整的震屏配置。
- `PlayLeftWallCrash()` 只有通过 PlayMode、重复触发和墙体引用校验，并把本次撞击确认为成功后，才调用 `crashSuccessFeedback.PlayFeedbacks()`。
- 播放时点与 `l70Crash` 成功音效一致，发生在墙体淡出/左移动 Tween 启动前；慢速撞左墙、撞右墙、重复成功调用和破墙准备失败均不会触发该 Feedback。
- Feedback 引用为空时安全跳过，不会阻断墙体破碎、`hasBrokenLeftWall` 范围开放或音效流程。
- 本次不新增全局事件、运行时实例化、存档、配置、资源路径或共享相机脚本修改。

### 绑定、验证与回滚

- 当前 `Level_070.prefab` 静态检查未发现 MMF_Player 组件，因此 `crashSuccessFeedback` 尚未绑定；需在 Unity Inspector 中创建/复用一个已配置震屏目标的 MMF_Player 并绑定后，实际震屏才会生效。
- 项目既有 Level29、Level34、Level45、Level52 等关卡均使用序列化 MMF_Player + `PlayFeedbacks()` 的局部调用模式，本次保持相同接入边界。
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly` 通过：0 error / 99 warnings；静态检查确认调用只位于破墙成功分支。
- 未运行 Unity PlayMode；绑定后需验证成功破墙只震一次、失败不震、震屏目标是 GameCamera、强度/时长合适，并执行 Android/iOS 横屏 smoke。
- 回滚时移除 `MoreMountains.Feedbacks` 引用、`crashSuccessFeedback` 字段和成功分支的一次播放调用即可；墙体、音效、Hook 范围与 prefab 其它内容不受影响。

## 2026-07-22 ball 打开朝上与球壳渐隐

### 需求与修改后行为

- 修改前 ball 在随机延迟期间会受冲量和物理旋转影响；打开后虽然冻结 Rigidbody2D，但会保留当时角度，导致不同 ball 的开口方向不一致，openPart 球壳也会持续显示。
- `Level70BallItemCtrl` 新增 `openUpwardWorldEulerZ`，默认 0°。`SetOpenState()` 在清零速度并冻结刚体后，把 ball 的世界 Z 角设置为该值，使当前美术基准下的开口统一朝向世界上方。
- 新增 `openPartFadeDuration`，默认 0.5 秒。ball 打开后立即对 `openPart` 子树的 SpriteRenderer 同步执行 `DOFade(0)`，完成后关闭 openPart；小于等于 0 时直接隐藏。
- 渐隐范围严格限定在 openPart。当前 prefab 的 11 个 `jumpOut` 均位于 openPart 子树之外，Item 随后脱离 ball 并播放 `jumpOut`，因此 Item 的 SpriteRenderer、SortingGroup=200 和弹出动画不参与球壳渐隐。
- `InitializeContent()` / `SetClosedState()` 会终止未完成的球壳渐隐，恢复 openPart 各 SpriteRenderer 的初始颜色和 ball 初始本地旋转；没有增加 GameReset、成功、失败、禁用或销毁复位链。
- ball 根 SortingGroup=180、Item order=200、1.5～3 秒随机打开延迟、打开冻结和 collider 行为保持不变。本次未修改 prefab、资源、事件、存档或配置。

### 本地证据、验证与回滚

- `Level_070.prefab` 静态证据显示 11 个 ball 美术初始旋转均为 0°；每个 openPart 都有两个子球壳，11 个 `jumpOut` 均不在 openPart 子树中。
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly` 通过：0 error / 1117 warnings；脚本 diff check 通过。
- 未运行 Unity PlayMode；需验证滚动到任意角度后打开均朝上、0.5 秒球壳渐隐、Item 不淡出、渐隐结束球壳 inactive，以及连续打开 11 个 ball 时的视觉和移动端性能。
- 回滚时删除 DOTween 引用、两个序列化参数、旋转归一、openPart Renderer 缓存/颜色恢复和渐隐 Sequence；原有打开冻结、排序与 Item 弹出逻辑可独立保留。

## 2026-07-22 failCrashEffect 改为整体缩放

### 行为变化

- 修改前左右 `CrashEffect_` 播放失败表现时只把 X 轴设为 0，再通过 `DOScaleX(1)` 横向展开；Y/Z 轴保持不变。
- 修改后效果初始 Scale 统一设为 `Vector3.zero`，通过 `DOScale(Vector3.one)` 让 X/Y/Z 同时从 0 放大到 1，再执行原有 SpriteRenderer 渐隐。
- 左右失败效果共用同一 `PlayFailureEffect()`，因此慢速撞左墙和撞右边界都使用新的整体缩放；破墙成功分支不会播放失败效果。
- 当前 prefab 的左右效果初始 Scale 均为 `(1,1,1)`，`failureScaleDuration=0.2`、`failureFadeDuration=0.3`，本次未修改这些参数或 prefab。
- 失败音效 `l70FailCrash`、边界触发去重、淡出 Ease、速度阈值和左墙破碎流程保持不变。

### 验证与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly` 通过：0 error / 1117 warnings；静态扫描确认失败效果路径不再包含 `DOScaleX`。
- 未运行 Unity PlayMode；需确认整体缩放中心、左右效果视觉尺寸、0.2 秒出现与 0.3 秒淡出衔接，以及移动端低帧率表现。
- 回滚时把初始 Scale 恢复为仅 X=0，并把 `DOScale(Vector3.one)` 恢复为 `DOScaleX(1f)`；无需修改 prefab、音效、Hook 速度或墙体流程。

## 2026-07-22 crash wall 原地淡出与操作音效单次播放

### 需求与修改后行为

- crash wall 成功表现修改前会让 `crushWall2` 在 `WallCrushAnimDuration` 内同时淡出并平移到 localX=-1；修改后只在当前位置淡出，完成后仍关闭 `crushWall2`。
- 删除脚本序列化参数 `WallCrushTargetLocalX` 及 `Level_070.prefab` 中对应值，同时移除仅为平移动作服务的初始 localPosition 缓存、恢复和 Transform Tween 清理；墙体显隐切换、`l70Crash`、成功 Feedback（震屏）和破墙范围开放逻辑不变。
- `l70PlayGame` 修改前以 loop 模式播放并由 `Level70Ctrl` 持有 `Sound`，在恢复控制和销毁时定向停止；修改后进入抓钩控制时直接调用 `AudioManager.PlaySound("l70PlayGame")`，使用其默认 `loop=false` 单次播放语义。
- 删除 `l70PlayGame` 的持有字段、启动/停止包装方法及恢复控制、销毁处的停止调用。音效仍遵循现有 SFX 开关及 `AudioManager` 的一次性音源完成回收，不再由 Level70 管理生命周期。
- 本次没有新增事件、存档、配置、运行时实例化或共享玩法脚本修改。

### 验证与回滚

- 静态检查确认 `Level70Ctrl` 和 `Level_070.prefab` 已无 `WallCrushTargetLocalX`、`DOLocalMoveX`、`l70PlayGameSound`、`loop:true` 及对应停止方法；`l70PlayGame` 只在抓钩控制成功进入后调用一次。
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly` 通过：0 error / 1117 warnings。
- 未运行 Unity PlayMode；需验证破墙碎片原地淡出、震屏与音效时序，以及进入抓钩控制后 `l70PlayGame` 播放一次且恢复/销毁不会出现额外停止或重复播放。
- 回滚 crash wall 时恢复目标 X 参数、prefab 配置和 Sequence 的 `DOLocalMoveX`；回滚音效时恢复 `Sound` 句柄、loop 播放和两个生命周期停止点。无需回滚音频资源、事件或存档。

## 2026-07-22 Hook 水平速度改为 InQuad 曲线

### 需求、基线与修改后行为

- 修改前速度采用恒定加速度：`a=(max²-min²)/(2×活动行程)`，持续同方向时速度随时间线性增长；最低速度 1、最高速度 7 时，归一化速度为 `1+6t`。
- 修改后每次从静止开始持续输入时启动一段本地 InQuad 速度曲线：`speed=Lerp(minSpeed,maxSpeed,t²)`。曲线从最低速度开始，前段增长较慢、后段明显加快，到完整活动行程的理论结束时间达到最大速度。
- 曲线时长按当前活动完整行程和 InQuad 平均速度计算：`duration=行程/((max+2×min)/3)`。当前最低 1、最高 7 的平均速度为 3；当前工作区 prefab 破墙前行程 8.04，对应约 2.68 秒，破墙后完整行程 13.06，对应约 4.353 秒。
- 松开左右输入仍立即把速度和曲线进度清零；再次按下会从最低速度重新开始。直接反向仍先使用原恒定减速度减到 0，再从新方向启动 InQuad，避免高速状态直接翻转为反向速度。
- 左墙破碎前/后活动范围、边界锁、右墙永不破碎、`crashSpeedRatio` 和失败/成功效果链不变。当前最大速度 7、比例 0.87，破墙仍要求实际撞击速度严格大于 6.09。
- 本次只修改 `Level70HookCtrl.cs` 和 Level70 本地/共享文档；没有修改 prefab 参数、全局事件、UI、资源、存档、配置、分析或平台链路。

### 数值验证、缺口与回滚

- InQuad 达到速度 6.09 时的归一化时间约为 92.1%，对应约 82.8% 的完整行程；相比原恒定加速度，破墙所需高速区间更靠近目标边界，属于明确的手感变化。
- 对当前工作区 8.04/13.06 行程执行 15、30、60、120 fps 数值模拟，完整端到端撞击速度约为 6.940～7.000，均严格超过当前破墙阈值 6.09；离散帧下边界位置会被现有逻辑 Clamp。
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly` 通过：0 error / 99 warnings；脚本静态检查已无旧的正向恒定加速度入口。
- 未运行 Unity PlayMode 或 Android/iOS 横屏验证；需重点确认前段是否过慢、满行程破墙是否稳定、短距离起步是否应允许破墙、快速反向的减速手感及低帧率撞墙表现。
- 回滚时删除 InQuad 的 elapsed/duration/direction 状态和计算方法，恢复 `GetHorizontalAcceleration()` 与原 `Mathf.MoveTowards` 正向提速分支；prefab、音效、墙体和资源无需回滚。

## 2026-07-29 catch_btn 作为局部 useBtn

### 需求与行为基线

- 用户要求整理 Level70 输入代码：`catch_btn` 作为 `useBtn` 交给 `Level70Ctrl` 控制，并确保 control 状态不会触发 `JumpBtn` 功能。
- 修改前 `Level70GamePlayPanel.OnClickCatch()` 会派发全局 `PlayerJumpEvent`；同一事件既由 `Level70Ctrl` 用于 `TryStartCatch()`，也由共享 `BaseLevelCtrl.OnPlayerJump()` 用于 `ActorCtrl.TriggerJump()`。因此 Catch 与 Jump 共用事件语义，无法从输入链上隔离。
- 修改前 control 状态只隐藏 `jump_btn`，没有同时关闭 `CustomButton.enabled` 和 `Interactable`。

### 修改后行为与职责

- `Level70GamePlayPanel.SetLevel70CatchButtonVisible(true)` 会先将共享 `jump_btn` 设为不可交互、禁用组件并隐藏，再显示 `catch_btn`，把它作为返回值交给 `Level70Ctrl`。
- `Level70Ctrl` 只在 control 状态绑定该局部 `useBtn.OnClickDown`，点击后直接调用 `Level70HookCtrl.TryStartCatch()`；不再订阅 `PlayerJumpEvent`，Catch 不再进入共享 Jump 事件链。
- 退出 control 时先解绑 `useBtn`，再恢复 `jump_btn` 并停用、隐藏 `catch_btn`；`OnDisable` / `OnDestroy` 也会清理局部按钮回调。
- 暂停或非 `PLAYING` 状态下，Level70 面板同时关闭 `catch_btn.Interactable` 和组件 enabled；恢复游戏后仅在 Catch 模式重新开放。

### 影响范围与数据流

- 修改文件：`Level70Ctrl.cs`、`Level70GamePlayPanel.cs` 及本地功能/风险文档。
- UI 输入流变为 `catch_btn.OnClickDown -> Level70Ctrl.OnClickUse -> Level70HookCtrl.TryStartCatch()`；普通状态仍沿用 `jump_btn -> PlayerJumpEvent -> BaseLevelCtrl/ActorCtrl`。
- 未修改共享 `GamePlayPanel`、`BaseLevelCtrl`、`ActorCtrl`、`CustomButton`、Level70 prefab、其它关卡、DB/save、配置、资源、analytics 或平台 SDK。

### 验证、缺口与回滚

- 静态扫描确认 Level70 两个修改脚本已无 Catch 派发或订阅 `PlayerJumpEvent` 的代码，且 control 分支同时执行 Jump 按钮不可交互、组件禁用和隐藏。
- `git diff --check` 通过，仅有工作区 LF/CRLF 提示。全量 C# 首次构建在 124 秒超时且没有返回编译诊断；清理该次残留构建进程后，使用单节点且禁用共享编译器重试通过：`0 warning / 0 error`。后续环境复跑时 Unity `Temp` 输出已被清理，并暴露当前 `Packages` 中缺失的第三方源码/元数据文件，失败点均为 `CS0006` / `CS2001` 外部依赖，不是本次 Level70 脚本诊断；最终脚本保持为前述成功构建时的版本。
- 未运行 Unity PlayMode 与 Android/iOS 横屏触摸验证；需手工验证普通 Jump、进入 control、连续 Catch、暂停/恢复、退出 control、重开和退出关卡。
- 回滚时恢复 Level70 面板的 Catch→`PlayerJumpEvent` 派发及 `Level70Ctrl` 的对应订阅，并移除局部 `useBtn` 绑定；无需回滚 prefab、共享输入、存档或资源。

## 2026-09-09 松夹落球兜底

- 需求：用户确认第 70 关到释放点松夹时，临时忽略球与夹子的碰撞并给予向下速度，避免球仍被爪子托住。这是有意的本关释放行为调整。
- 基线：Level70HookCtrl 到 dropPoint 后只打开 Animator，等待 2 秒后闭合；没有落球检查。
- 修改：Level70HookCtrl.cs 新增 releaseBalls、releaseHookColliders、releaseDownSpeed；Level_070.prefab 显式绑定 11 个球的 CircleCollider2D 和左右爪、顶部 block 共 3 个实体 Collider2D，速度默认 2。无运行时节点扫描。
- 流程：到达释放点 → 打开夹子 → 同步物理 Transform → 用 catchArea 与夹子实体的合并包围盒筛选球 → 对可模拟、未冻结 Y 轴的动态球保存原碰撞忽略值并临时忽略夹子碰撞 → 唤醒、设置一次最低向下速度。球顶点落到释放范围下方且与当前夹子包围盒不交叠后恢复原碰撞关系。球停用/销毁、控制器 Disable/Destroy、ResetRuntime 也回收本功能持有的忽略状态。
- 复用边界：原下落/抓取、2 秒开夹、回收、球落地后 OpenAndPlayJumpOut 与打开冻结流程保留；不改 Actor、共享 UI、事件、存档、配置、SDK、广告和资源加载。已冻结的打开态球不重新激活。
- 验证：dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies --verbosity:quiet 通过，0 错误、123 警告；日志 Temp/CodexLogAnalysis/level70-build.txt。Prefab 引用检查确认 11 个唯一球 Collider 与 3 个唯一非 Trigger 夹子 Collider 均有效，球初始均为 Dynamic、simulated=1、gravityScale=1、constraints=0。目标文件 git diff --check 通过。
- 未验证（uncertain）：Unity PlayMode 与 Android/iOS 真机尚未验证。必须覆盖单球、偏夹、多球、空夹、暂停恢复、连续抓取、退出重开、落地开壳。当前防止夹子托球，不屏蔽地面或其它球；不能把静态检查表述为任意物理场景必落的实测保证。
- 性能：首次释放分配 11 个状态及 33 个碰撞备份，固定步仅检查绑定数组，不进行全场景搜索。
- 回滚：撤回 Hook 的松夹辅助字段/方法与新清理调用，将回调恢复 SetHookOpen(true)，移除 Prefab 新增三个字段；不改 GUID 或玩家数据。
