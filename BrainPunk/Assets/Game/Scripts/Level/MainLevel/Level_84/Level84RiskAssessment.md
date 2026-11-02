# Level84 足球选择流程风险评估

## 原行为基线

- `Level84Ctrl` 除序列化引用和注释外没有运行时行为。
- 现有 actor/enemy prefab 实例使用共享 `ActorCtrl` 组件。
- 关卡失败/重置已由既有 Play 流程负责；本次改动继续使用失败重置路径，并明确两个射门选择都不触发成功。

## 影响文件和资源

- `Level84Ctrl.cs`
- `Level_084.prefab` 中 Level84 本地引用和配置好的 Enemy 绑定。

## 使用的既有系统

- `TriggerAreaHandle`：读取既有进入/成功区域的边界数据。
- `CustomButton.OnClick`：处理本地选择 UI。
- `BaseLevelCtrl.SetCtrlable(...)`：选择和射门流程期间锁住 actor 输入。
- `GameResetEvent(GameResetType.Failed)`：进入既有失败重置。
- DOTween / UniTask：本地表现时序。
- 既有 `ActorCtrl` 和 `ActorSpineCtrl`：播放可选的 actor 射门和敌人扑救动画。

## 风险项

- P1：Level84 actor/enemy 的 Spine 动画缺失会导致表现不完整。脚本会输出缺失动画 warning，并降级为短等待。
- P1：prefab 中 `enemy` 已在 YAML 中绑定到配置好的 `Enemy` 角色实例，但仍需要 PlayMode 确认视觉进入和扑救表现。缺失敌人的 fallback 仅作为防崩溃保护。
- P1：重复进入触发区或快速连点两个按钮可能造成重复成功/失败事件。当前实现通过 guard 状态和 session id 降低该风险。
- P1：如果 prefab 中仍有 UnityEvent 或外部绑定调用成功路径，会违背澄清后的规则。当前代码中没有 Level84 足球选择成功路径，prefab 绑定仍应在 Unity 中复核。
- 观察项：`TriggerAreaHandle` 本身未修改，内部仍有原有 Unity `Debug.Log`；本次 Level84 改动只读取它的边界。
- 观察项：足球拖拽行为未在代码中明确定位；当前实现没有新增拖拽拦截。
- 观察项：本次有意不新增 `Level84ActorCtrl` 子类，因为当前 actor/enemy prefab 实例使用 `ActorCtrl`。足球相关可选动画由 `Level84Ctrl` 通过 `ActorCtrl.SpineCtrl` 本地播放。

## 验证证据

- 代码检查：`BaseLevelCtrl` 的失败路径会在 `TryToRestart()` 后派发 `GameResetEvent`，本地失败派发使用同一事件类型。
- prefab 检查：`EnterTrigger` 指向 actor transform，`SuccessArea` 指向 football transform；二者都使用 `TriggerAreaHandle`，边界大小为 `{x: 2, y: 3}`。YAML 证据中二者 UnityEvent 调用列表为空。
- prefab 检查：`Level84Ctrl.enemy` 已绑定到配置好的 `Enemy` 角色实例 stripped `ActorCtrl` 引用。
- 静态扫描：`Level84Ctrl.cs` 不包含 `Instantiate`、运行时资源加载、DB/save、RemoteConfig、PlayerPrefs、`OnValidate`、`Reset`、`[ExecuteAlways]`、`[ExecuteInEditMode]`。
- `dotnet build Assembly-CSharp.csproj --no-restore` 无法运行，因为当前 worktree 没有生成的 `.csproj` / `.sln` 文件。
- 已尝试 Unity 6000.0.59f2 batchmode 编译，但在干净项目编译前被无关依赖错误阻断：`Assets/Guru/Gen/GuruAllSpecs.G.cs` 和 `Assets/BearGame/com.bear.ui@919f05cf0418/Runtime/Animation/UIScaleAnimation.cs`。提取到的编译错误行未包含 `Level84Ctrl.cs`。
- 未执行：PlayMode、Android/iOS 横屏冒烟验证。

## 未验证项

- 视觉时序、足球路径是否合适、敌人阻挡时机和最终玩家体验需要 PlayMode 验证。
- 敌人动画可用性需要在 Unity 中结合 prefab / asset 验证。两个射门选择都显示配置好的敌人阻挡前，Level84 不应视为验收完成。
- 由于本次使用既有失败重置流程且不触发成功，存档/解锁/analytics 下游行为预期不变；但这些路径尚未实际运行验证。

## 回滚路径

如果新 Level84 流程造成回归，回滚 `Level84Ctrl.cs`、`Level_084.prefab` 的 Level84 本地绑定/表现改动，并删除 `Level84SoccerFlow_Feature.md` 和本风险评估。

## 2026-06-18 v2.0 补充风险评估

### 原行为基线

- 当前 Level84 足球流程基线保持不变：直射和高射只是表现不同，都会被配置好的 enemy 阻挡，都会派发 `GameResetEvent(GameResetType.Failed)`，不会触发 success。
- 本次补充前，enemy 阻挡完成后直接进入既有失败等待；本次补充增加了 `enemyBlockVisibleDuration`，用于在失败重置前保留一段可见阻挡时间。

### 新增或变化的风险项

- P1：`enemyBlockVisibleDuration` 过短可能仍不足以让玩家确认阻挡；过长可能让失败节奏变慢。当前默认值为 0.35 秒，仍需 PlayMode 目测验证。
- P1：新增中文 Tooltip 和 `#region` 分区不应改变运行时行为，但需要确认没有误改原有类注释和 Unity 默认 `Start` / `Update` 注释。当前 diff 检查确认原注释文本保留。
- P1：新增序列化字段未写入 prefab YAML，Unity 会使用脚本默认值。该选择避免 prefab 序列化 churn，但仍需在 Unity Inspector/PlayMode 中确认默认值生效。
- P2：batchmode 编译被无关 Guru/Bear UI 依赖错误阻断，无法获得干净 Unity 编译结果；当前只能证明错误日志未指向 `Level84Ctrl.cs`。

### 验证证据

- 静态扫描未发现 success、通关、解锁、成功存档调用。
- 静态扫描未发现 DB、Save、RemoteConfig、PlayerPrefs、Resources、StreamingAssets、YooAsset、Instantiate、`OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode` 新增链路。
- `Level84Ctrl.cs` 的 `#region` 和 `#endregion` 数量均为 12。
- `git diff --check -- Level84Ctrl.cs quickstart.md` 未报告 whitespace error。
- Unity batchmode 日志 `BrainPunk/Logs/Level84V2_Batchmode.log` 显示阻断点仍为既有无关依赖：`GuruAllSpecs.G.cs` 缺少 Guru/spec 类型，`UIScaleAnimation.cs` 缺少 `DG.Tweening` / `Tween`。日志未显示 `Level84Ctrl.cs` 错误。

### 未验证项

- 未执行 Unity PlayMode，因此两个选择的真实视觉节奏、enemy 位置、动画可用性、失败重置前可见时长仍待验证。
- 未执行 Android/iOS 横屏触控和 fps/卡顿验证。
- 未执行未受影响 Actor 关卡 PlayMode 冒烟验证；当前仅通过 diff 范围和静态扫描确认没有改 Level85、bubble 或共享系统。

### 回滚路径

回滚 `Level84Ctrl.cs` 中新增的 `enemyBlockVisibleDuration` 字段、相关等待逻辑、Tooltip 和 region 分区；同时回滚 `specs/023-level84-v2-logic/quickstart.md` 中本次补充的 3 秒、enemy 可见和性能记录项。

## 2026-06-22 足球刚体入场补全风险评估

> 当前状态校准（2026-06-23）：本节评估 2026-06-22 当时的刚体入场方案。当前 `Level84Ctrl.cs` 已改为 DOTween `DOMove(footballInitialPosition + footballDropOffset)` 入场，脚本中已没有 `footballBouncePower` 字段，也不会在入场阶段切到 Dynamic 做刚体下落。当前风险以“2026-06-23 Level84 音效、UI 展示和状态恢复风险评估”为准。

### 原行为基线

- 当前工作区补全前，`Level84Ctrl.cs` 的足球入场逻辑处于未完成状态：`fbRigidbody` 已加入脚本，但 `EnterSoccerFlow()` 的足球入场代码块为空，`SwitchFootballState(...)` 只改 `bodyType`，没有完成刚体状态管理。
- `Level_084.prefab` 仍保留 `footballDropOffset`、`footballDropDuration`、`footballBouncePower` 配置，以及足球 `Rigidbody2D` 的 `Dynamic + GravityScale=1 + FreezeRotation` 基线。

### 本次新增/变化的风险项

- P1：足球入场现在依赖 `Physics2D.gravity`、`footballInitialGravityScale` 和 `footballDropDuration` 计算纵向初速度。若场景重力或 prefab 刚体重力后续被改动，入场视觉会随之变化。
- P1：脚本在入场阶段结束后会把足球强制归到 `footballInitialPosition` 再切回 `Kinematic`。如果物理模拟终点与初始点偏差较大，可能出现轻微的末帧吸附感，需 PlayMode 目测确认。
- P1：`ResolveShot()` 新增 `SwitchFootballState(false)`，是为避免 DOTween 射门表现和动态刚体持续模拟打架；如果未来 Level84 希望保留射门后的真实物理滚动，这里需要重新设计，不宜直接复用当前锁回方案。
- P1：当 `fbRigidbody` 丢失时脚本会走 DOTween `DOJump(...)` fallback，流程能继续但不代表 Level84 足球刚体方案已验证通过。
- P2：本次没有改 prefab，因此 `fbRigidbody` 仍主要依赖手工序列化引用或 `ResolveReferences()` 自动解析；如果未来改名/拆层级导致 `football` 引用缺失，刚体解析会跟着失效。

### 验证证据

- 本地 diff 证据：工作区此前已经删除脚本内的 `footballDropOffset` / `footballDropDuration` / `footballBouncePower` 和 DOTween 入场实现，但 prefab 文本仍保留第 91-93 行配置。
- prefab 文本证据：`Level_084.prefab` 第 357-382 行显示足球 `Rigidbody2D` 当前为 `Dynamic`、`GravityScale=1`、`Constraints=FreezeRotation`。
- 静态检查 `Level84Ctrl.cs`：未新增 success、通关、解锁、成功存档、DB、Save、RemoteConfig、PlayerPrefs、Resources、StreamingAssets、YooAsset、`Instantiate`、`OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode` 链路。
- 本次未执行 Unity PlayMode、Android/iOS 横屏触控或设备性能验证。

### 未验证项

- 入场物理下落/反弹的节奏、球体末帧归位观感、按钮显示时机和 enemy 阻挡节奏仍需 Unity PlayMode 验证。
- 射门前切回 Kinematic 后，直射/高射 DOTween 表现是否与现有 prefab 碰撞体交互保持稳定，仍需 PlayMode 验证。
- Android/iOS 横屏触控、fps/卡顿、失败重开和退出清理仍未补验。

### 回滚路径

- 回滚 `Level84Ctrl.cs` 中本次恢复的足球入场参数、刚体状态切换、速度计算、射门前锁定和 fallback 逻辑。
- 同步删除本节风险评估，并恢复到当前工作区补全前状态。

## 2026-06-22 SuccessArea 胜利路径主线合并风险评估

> 当前状态校准（2026-06-23）：本节中“主线已有足球刚体入场补全”为当时背景；当前入场实现已改为 DOTween DOMove，成功路径仍会调用 `SwitchFootballState(false)` 锁定 football。

### 原行为基线

- 主线合并前，当前工作树已经包含 Level84 足球刚体入场补全：football 入场阶段可切到 Dynamic，入场结束后切回 Kinematic，射门前也会锁回 Kinematic。
- 主线合并前，按钮路径仍以 enemy 阻挡后失败为主；`Level_084.prefab` 虽存在 `SuccessArea`，但脚本未消费该成功区。

### 本次新增/变化的风险项

- P1：成功路径改为轮询 football 是否进入 `SuccessArea`。若 football 在边界附近高速穿过，是否能稳定命中仍需 PlayMode 验证。
- P1：成功路径启动时会停止 football/enemy tween，并把足球刚体切回静止 Kinematic。该处理用于避免成功等待期间物理继续模拟，但可能在命中瞬间产生一帧视觉中断，需 PlayMode 目测确认。
- P1：成功路径复用 `BaseLevelCtrl.SuccessStrightly()`；解锁、存档和关键打点预期沿用既有成功归口，但当前环境尚未运行到 Level84 成功结算，不能视为已验证。
- P1：`OnEnable()` 与 `Start()` 都会进入按钮监听绑定；当前依赖 `AddListeners()` 先移除再添加来避免重复订阅，仍需反复 enable/disable PlayMode 验证按钮只触发一次。
- P2：本次仍未修改 prefab，`successArea` 依赖按名查找 `SuccessArea`。如果后续 prefab 改名或层级拆分，成功路径会失效。

### 验证证据

- 静态源码证据：`Level84Ctrl.cs` 新增 `TryStartSuccessFlow()`、`StartSuccessFlow()`、`CompleteSuccess()`、`IsFootballInsideSuccessArea()`，并在 `Update()` 与失败协程关键节点调用成功抢占检查。
- 静态源码证据：成功路径只调用 `BaseLevelCtrl.SuccessStrightly()`；未新增 DB、Save、RemoteConfig、PlayerPrefs、Resources、StreamingAssets、YooAsset、Instantiate、OnValidate、Reset、ExecuteAlways、ExecuteInEditMode。
- 静态源码证据：成功路径中已调用 `SwitchFootballState(false)`，用于兼容当前主线已有足球刚体入场实现。
- prefab 文本证据：`Level_084.prefab` 中 `SuccessArea` 的 `TriggerAreaHandle` 仍序列化为 `boundarySize: {x: 2, y: 3}`、`targetObject: football`、UnityEvent 列表为空。

### 未验证项

- 未验证 football 在入场、直射、高射、enemy 阻挡、失败等待期间进入 `SuccessArea` 时，是否都能稳定优先成功且只结算一次。
- 未验证重开、退出、Disable 发生在成功等待 2 秒期间时，是否一定不会晚到调用 `SuccessStrightly()`。
- 未验证成功后的解锁、存档和关键打点实际结果。
- 未验证 Android/iOS 横屏触控、连续 60 秒帧率/卡顿，以及一个未受影响 Actor 关卡冒烟。

### 回滚路径

- 回滚 `Level84Ctrl.cs` 中 `successArea` / `successDelay`、成功等待、成功抢占失败、`SuccessArea` 检测、成功路径刚体锁定和 `OnDisable()` 监听解绑补充。
- 同步删除本节风险评估，以及功能记录和 `DevelopMD/已有功能设计与使用记录.md` 中对应追加内容。

## 2026-06-22 Actor 足球表现改用 Level84ActorCtrl 风险评估

### 原行为基线

- 变更前 `Level84Ctrl` 直接通过 `ActorCtrl.SpineCtrl` 播放 `football_shoot_ready`、`football_shoot` 和 `football_save`，并由本地 helper 负责缺失动画 warning、短等待 fallback、移动停止和动画恢复。
- `Level84ActorCtrl` 本地代码已经存在足球表现 API：`PlayFootballShoot()`、`PlayGoalkeeperSave()`、`StopSoccerAction()` 和 `IsSoccerActionPlaying`。
- `Level_084.prefab` 当前文本证据显示 actor/enemy 追加组件脚本 GUID 指向 `Level84ActorCtrl.cs.meta` 的 GUID，因此本次代码路径与当前 prefab 结构匹配；仍需 Unity PlayMode 验证实际实例解析和动画表现。

### 影响文件/资源

- `Assets/Game/Scripts/Level/LevelItem/Level_84/Level84Ctrl.cs`
- `Assets/Game/Scripts/Level/Actor/Level84ActorCtrl.cs`：只作为既有表现 API 使用，未修改。
- `Assets/Game/Prefabs/Level/Level_084.prefab`：未修改；依赖 actor/enemy 继续具备 `Level84ActorCtrl`。

### 使用的既有系统

- `Level84ActorCtrl`：足球射门、守门员扑救、动作锁定和动作停止。
- `ActorCtrl`：保留基础 actor/enemy 引用、移动输入清零、缺失表现组件 fallback。
- DOTween / UniTask：保留足球路径、敌人入场和等待时序。
- Bear Logger：缺失组件或缺失 enemy 时输出 warning。

### 新增/变化的风险项

- P1：如果后续 prefab 或变体把 actor/enemy 上的 `Level84ActorCtrl` 移除或替换为普通 `ActorCtrl`，`Level84Ctrl` 会进入防崩溃 fallback，流程不会中断，但不会播放真实踢球/扑救动画，不能视为验收通过。
- P1：`Level84ActorCtrl.PlayFootballShoot()` / `PlayGoalkeeperSave()` 内部会管理 `isSoccerActionPlaying` 并调用 `StopAllMotion()`；实际移动锁定、朝向、动画恢复节奏必须在 PlayMode 验证，不能仅凭静态代码判定表现正确。
- P1：成功抢占、失败重置或 Disable 可能发生在 `Level84ActorCtrl` 足球动作播放期间。当前 `Level84Ctrl` 已调用 `StopSoccerAction()` 并保留输入清零/动画恢复，但仍需 PlayMode 验证不会晚到恢复错误动画或影响下一次尝试。
- P1：本次从 `Level84Ctrl` 移除直接动画名检查后，缺失动画 warning 由 `Level84ActorCtrl` 输出。日志来源变化可能影响排查习惯，但不改变业务流程。
- P2：新增 `actorSoccerCtrl` / `enemySoccerCtrl` 序列化字段未写入 prefab YAML；当前依赖运行时从 `actor` / `enemy` 自动解析，若后续层级或引用异常，Inspector 上不会直接显示已绑定值。

### 验证证据

- 静态源码证据：`Level84Ctrl.cs` 中不再存在 `PlayActorSoccerAction`、`PlayActorAnimationOrDelay`、`HasAnimation`、`ActorSpineCtrl` 或足球动画名常量。
- 静态源码证据：`ResolveShot(...)` 调用 `PlayActorFootballShoot()`，enemy 阻挡路径调用 `PlayEnemyGoalkeeperSave()`，二者优先转发到 `Level84ActorCtrl`。
- 静态源码证据：清理路径调用 `StopLevel84SoccerAction(...)`，优先使用 `Level84ActorCtrl.StopSoccerAction()`，同时保留移动输入清零和动画恢复。
- prefab 文本证据：`Level84ActorCtrl.cs.meta` GUID 为 `f8c368c64087fd548a915a5e12ea9cbd`，`Level_084.prefab` 中 actor/enemy 追加组件脚本使用同一 GUID。
- 静态扫描确认：本次未新增 DB、Save、RemoteConfig、PlayerPrefs、Resources、StreamingAssets、YooAsset、Instantiate、OnValidate、Reset、ExecuteAlways、ExecuteInEditMode 链路。

### 未验证项

- 未执行 Unity PlayMode 验证 actor 踢球、enemy 扑救、动画缺失降级、重复连点、成功抢占、失败重置和 Disable/退出清理。
- 未验证 Android/iOS 横屏触控、连续 60 秒帧率/卡顿，以及一个未受影响 Actor 关卡冒烟。
- 未验证新增序列化字段在 Unity Inspector 重新加载后的显示和自动解析是否符合策划维护预期。

### 回滚路径

- 回滚 `Level84Ctrl.cs` 中 `actorSoccerCtrl` / `enemySoccerCtrl`、`PlayActorFootballShoot()`、`PlayEnemyGoalkeeperSave()`、`StopLevel84SoccerAction()` 和 `ResolveLevel84ActorCtrl(...)`。
- 如需恢复旧逻辑，同步恢复 `Level84Ctrl` 内直接使用 `ActorCtrl.SpineCtrl` 的本地动画 helper，并删除本节风险评估、功能记录和 `DevelopMD/已有功能设计与使用记录.md` 的对应追加内容。
## 2026-06-22 enemy 扑救后状态恢复风险评估

> 当前状态校准（2026-06-23）：当前 `PlayEnemyBlock(...)` 末尾的 `RestoreEnemyPresentationState()` 调用处于注释状态，enemy 不会在 `enemyBlockVisibleDuration` 后立即恢复；`RestoreEnemyPresentationState()` 仍由重置路径调用。当前风险以 2026-06-23 追加风险评估为准。

### 原行为基线

- `PlayEnemyBlock(...)` 会临时激活 enemy，把 enemy 从初始位置右侧移动到 `GetEnemySavePosition()`，再调用 `PlayEnemyGoalkeeperSave()` 播放扑救表现。
- `PlayEnemyGoalkeeperSave()` 优先转发到 `enemySoccerCtrl.PlayGoalkeeperSave()`；缺少 `Level84ActorCtrl` 时走 warning、短等待和移动清理 fallback。
- 变更前 enemy 的位置、旋转、激活状态主要由 `ResetPresentationState()` 在重开、disable、失败 reset 等路径恢复；扑救表现结束到失败事件派发之间，enemy 可能继续停在阻挡位置。

### 影响文件/资源

- `Assets/Game/Scripts/Level/LevelItem/Level_84/Level84Ctrl.cs`
- 未修改 `Level_084.prefab`、Spine 动画资源、Luban/DB/存档、热更资源、Android/iOS 构建配置、广告、IAP 或打点系统。

### 使用的既有系统

- `Level84ActorCtrl`：enemy 足球扑救动作播放与动作停止。
- `ActorCtrl`：fallback 动画恢复、移动输入清理和死亡状态判断。
- DOTween / UniTask：enemy 入场移动、异步等待和 session 防晚到恢复。
- Bear Logger：缺少表现控制器时仍沿用现有 warning 路径。

### 新增/变化的风险项

- P1：如果 enemy 初始状态为 inactive，`RestoreEnemyPresentationState()` 会在扑救表现和既有可见停留结束后立即把 enemy 隐藏；这符合“恢复 enemy 状态”，但失败事件派发前的剩余等待窗口中玩家不再看到 enemy，节奏需要 PlayMode 目测确认。
- P1：恢复方法会调用 `StopLevel84SoccerAction(...)`，如果 `Level84ActorCtrl.PlayGoalkeeperSave()` 的内部动作结束恢复与外部恢复时机重叠，实际动画回 idle 的帧序需要 PlayMode 验证。
- P1：成功抢占、重开或 disable 可能发生在扑救动作或可见停留期间；当前通过 `IsCurrentSession(currentSession)` 阻止晚到恢复，但仍需 PlayMode 验证不会把后续新一轮状态错误拉回旧缓存。
- P2：`ResetPresentationState()` 和 `PlayEnemyBlock(...)` 现在复用同一个私有恢复方法；代码重复减少，但如果未来某一路径需要只停动作不恢复 active，需要拆出更细粒度方法。

### 验证证据

- 静态源代码证据：`Level84Ctrl.cs` 新增 `RestoreEnemyPresentationState()`，并在 `PlayEnemyBlock(...)` 的 `PlayEnemyGoalkeeperSave()` 与 `enemyBlockVisibleDuration` 后按 session 有效性调用。
- 静态源代码证据：`ResetPresentationState()` 改为复用同一恢复方法，保留原本 enemy DOKill、停止足球动作、恢复初始位置/旋转/激活状态的语义。
- 静态扫描确认：本次未新增 DB、Save、RemoteConfig、PlayerPrefs、Resources、StreamingAssets、YooAsset、Instantiate、OnValidate、ExecuteAlways 或 ExecuteInEditMode 入口。
- `git diff --check` 和 C# 编译验证结果见本次最终说明。

### 未验证项

- 未执行 Unity PlayMode：需要验证直射/高射后的 enemy 入场、扑救、停留、恢复、失败事件派发和下一次重开状态。
- 未验证成功抢占：football 在 enemy 阻挡或失败等待期间进入 `SuccessArea` 时，enemy 恢复不会干扰成功结算。
- 未验证 Android/iOS 横屏触控、连续 60 秒帧率/卡顿、快速连点、退出/disable 和一轮失败后重新进入。

### 回滚路径

- 回滚 `Level84Ctrl.cs` 中 `RestoreEnemyPresentationState()` 的新增方法、`PlayEnemyBlock(...)` 末尾调用，以及 `ResetPresentationState()` 的复用改动。
- 同步删除本风险评估条目、`Level84SoccerFlow_Feature.md` 的本次功能记录和 `DevelopMD/已有功能设计与使用记录.md` 的对应记录。

## 2026-06-23 Level84 音效、UI 展示和状态恢复风险评估

### 原行为基线

- 现有 Level84 主流程已由 `Level84Ctrl` 驱动：进入足球流程、显示两个射门选择、直射/高射表现、enemy 阻挡、失败重置，以及 football 进入 `SuccessArea` 后成功抢占。
- 变更前脚本没有缓存 `footballInitialPosition`，`ResetPresentationState()` 只恢复 football 旋转/缩放和 active，未显式恢复足球位置。
- 变更前 enemy 恢复方法只停止 tween 和足球动作，未在当前代码中实际恢复 enemy 初始位置、旋转和 active 状态。
- 本次工作区 prefab 已新增触地触发器、UI Root、按钮音效组件和多处位置/参数调整，因此风险入口不只在脚本，也包括 `Level_084.prefab` 的资源链和 UnityEvent 绑定链。
- 2026-06-23 追加校准：当前 `EnterSoccerFlow()` 使用 DOTween `DOMove(footballInitialPosition + footballDropOffset)` 做入场表现，不再是旧记录中的刚体下落/弹跳；当前 `PlayEnemyBlock(...)` 末尾的 `RestoreEnemyPresentationState()` 调用被注释，enemy 不会在扑救可见停留后立即恢复。

### 影响文件/资源

- `Assets/Game/Scripts/Level/LevelItem/Level_84/Level84Ctrl.cs`
- `Assets/Game/Prefabs/Level/Level_084.prefab`
- 关联但未修改的既有系统：`AudioManager`、`LayerTriggerDetector`、`TriggerAreaHandle`、DOTween、UniTask、`Level84ActorCtrl`、`BaseLevelCtrl`、`GameResetEvent`

### 使用的既有系统

- `AudioManager.PlaySound(string)`：播放 `whistle`、`grassCut`、`kickFootball`、`footballTouchGround`、`footballOut`、`trap`、`footballAudienceCelebrate`。
- `LayerTriggerDetector`：prefab 足球触地检测进入事件，当前绑定 `PlayFootballTouchGround()`。
- DOTween / UniTask：足球入场 DOMove、射门表现、UI Root 缩放、enemy 入场和等待时序。
- `TriggerAreaHandle`：Actor 进入区和 football 成功区边界检测。
- `Level84ActorCtrl`：actor 射门和 enemy 扑救表现。

### 新增/变化的风险项

- P1：`Level_084.prefab` 的足球 `CircleCollider2D` 改为 trigger，并新增 `LayerTriggerDetector`。如果足球原本依赖非 trigger 碰撞参与物理阻挡或接地，PlayMode 中可能改变足球与地面/其它碰撞体的交互。
- P1：触地音效依赖 `LayerTriggerDetector.targetLayerMask = 64` 和目标层设置。若地面层不匹配，`footballTouchGround` 不播放；若层过宽，可能重复触发。
- P1：新增多个音效 tag 只通过静态代码确认调用，尚未确认 Sounds Good 配置中 tag 全部存在；缺失 tag 可能导致运行时无声或音频系统 warning。
- P1：`PlayBlockedShot(...)` 中射门表现以 fire-and-forget 启动，`shotDuration / 2` 后进入 enemy 阻挡。本次已在挡球前 `football.DOKill()`，但真实直射/高射半程被打断的视觉节奏仍需 PlayMode 目测。
- P1：`SwitchFootballState(false)` 现在会把 `gravityScale` 置 0。该处理能避免 Kinematic 状态残留重力，但如果未来希望 Kinematic 后保留原重力值，需要重新评估。
- P1：当前 `PlayEnemyBlock(...)` 不会在 `enemyBlockVisibleDuration` 后立即恢复 enemy；如果 enemy 在扑救位置继续停留到失败事件或成功抢占，可能与策划预期的“扑救后回位/隐藏”不一致，需要 PlayMode 目测。
- P1：`successUI.gameObject.SetActive(true)` 当前没有空引用保护，且 `ResetPresentationState()` 未显式隐藏 `successUI`。当前 prefab 已绑定该引用，但如果后续变体丢失绑定或成功后状态由外部流程复用，存在空引用或 UI 残留风险。
- P1：`uiRoot` 缩放展示依赖 prefab 绑定和 UI 层级调整。若 Root 下按钮/文本锚点与横屏适配不匹配，可能造成按钮位置或提示文字不符合预期。
- P2：`shotEndPoint` 缺失 fallback 会避免崩溃，但会让射门目标退化为当前足球位置或关卡物体位置；该路径只能算防崩溃，不能算玩法验收通过。
- P2：`footballDropOffset=-9.5` 在当前代码中是 `DOMove` 终点偏移，不是旧刚体方案的下落起点。若 prefab 参数仍按旧语义配置，足球入场终点可能偏离预期初始点。
- OBS：`git diff --check` 针对 prefab 仍可能报告 Unity YAML 空字段尾随空格；这是 Unity 序列化格式噪声，不等同运行时失败，但提交前可按团队格式要求处理。

### 验证证据

- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 通过，结果 0 error / 1114 warnings。
- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_84/Level84Ctrl.cs` 通过。
- 静态扫描 `Level84Ctrl.cs` 未发现 `OnValidate`、`Reset(...)`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、RemoteConfig、PlayerPrefs、Resources、StreamingAssets、YooAsset、Instantiate、`GameResetType.Success` 或空字符串音效调用。
- 静态源码证据：`CacheInitialState()` 记录 football 初始位置/旋转/缩放、初始 gravityScale，以及 enemy 初始位置/旋转/active；`ResetPresentationState()` 会将 football 复位到 `footballInitialPosition`，`EnterSoccerFlow()` 会先复位再 `DOMove(footballInitialPosition + footballDropOffset)`。
- 静态源码证据：`PlayEnemyBlock(...)` 在切换足球为 Dynamic 并施加阻挡力前会 `football.DOKill()`；`RestoreEnemyPresentationState()` 会恢复 enemy 初始位置、旋转和 active，但当前只在 `ResetPresentationState()` 等重置路径实际调用，`PlayEnemyBlock(...)` 末尾调用处于注释状态。
- 静态 prefab 证据：`Level_084.prefab` 已绑定 `uiRoot`，足球新增 `LayerTriggerDetector` 并把 onEnter 绑定到 `Level84Ctrl.PlayFootballTouchGround()`，两个射门按钮新增 `ClickAudio` 且 `AudioName: button`。
- 静态 prefab 证据：`Level_084.prefab` 已绑定 `successUI`、`footballDropOffset=-9.5`、`footballDropDuration=1.2`、`blockForce=400`。

### 未验证项

- 未执行 Unity PlayMode 验证 Level84 入场、触地音效、UI Root 缩放、两个按钮音效、直射/高射、enemy 阻挡、失败重置和 SuccessArea 成功抢占。
- 未验证 Sounds Good 中 `whistle`、`kickFootball`、`footballTouchGround` 等 tag 的实际存在和音量/重叠表现。
- 未验证足球 collider 改 trigger 后是否影响关卡物理交互、成功区进入、地面触发和挡球反弹。
- 未验证 enemy 在 `PlayEnemyBlock(...)` 后不立即恢复时的停留观感、成功抢占交互和失败重置前后状态。
- 未验证 `successUI` 在成功路径中的显示、成功后清理和缺失绑定时的容错。
- 未验证 Android/iOS 横屏触控、快速连点、退出/disable、重开后状态恢复和 60 秒帧稳定性。

### 回滚路径

- 回滚 `Level84Ctrl.cs` 中本次新增的音效调用、UI Root 缩放、触地入口、足球/enemy 状态缓存与重置、`shotEndPoint` fallback、挡球前停止 tween、刚体 gravityScale 切换和 `successUI` 成功显示。
- 回滚 `Level_084.prefab` 中本次 Level84 UI/音效/触发器/参数调整。
- 同步删除本风险评估条目、功能记录和 `DevelopMD/已有功能设计与使用记录.md` 中对应记录。
