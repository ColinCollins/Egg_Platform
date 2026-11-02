# Level70 Actor 控制手柄风险评估

## 2026-06-29 脚本重写

### 原始行为基线

- 修改前 `Level70ActorCtrl` 已存在 `controlhandle` 动画常量和坐下后延迟播放逻辑，但内部状态、方法和日志仍大量沿用 Level61 翻页语义。
- `PlaySit()` 会设置坐下状态、停止移动/击退、将 actor 设为 Kinematic，并通过 DOTween 跳到目标点。
- `StopSit()` 会恢复父节点、将 Rigidbody2D 设回 Dynamic，并播放 idle。
- 控制状态期间 `OnUpdate()` 不走基础移动逻辑，只更新空中状态、停止速度并处理碰撞。

### 影响文件与资源

- `Assets/Game/Scripts/Level/Actor/Level70ActorCtrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_70/Level70ActorCtrl功能说明.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_70/Level70ActorCtrl风险评估.md`
- `DevelopMD/已有功能设计与使用记录.md`
- 关联但未修改：`Assets/Game/Prefabs/Level/Level_070.prefab`、`Assets/Game/Scripts/Level/Actor/ActorCtrl.cs`、`Assets/Game/Scripts/Level/ActorSpineCtrl.cs`

### 已使用的现有系统

- `ActorCtrl`：基础移动、坐下状态、Rigidbody2D、碰撞检测和动画更新。
- `ActorSpineCtrl`：播放 `sofa_jump`、`sofa_idle`、`idle` 和 `controlhandle`。
- DOTween：坐下跳跃和 `transform.DOKill()` 清理。
- UniTask：坐下后 1 秒延迟触发控制手柄动画。
- Bear.Logger：Level70 本地 warning 输出。

### 风险项

- **P1：根 actor 引用未绑定风险**。2026-06-29 补充静态复核已在 `Level_070.prefab` 的 actor prefab override 中找到 `Level70ActorCtrl` GUID `0c20f997e9eaf734a8b7a3fe62d397b7`；但根 `BaseLevelCtrl.actor` 仍为 `{fileID: 0}`，运行时依赖 `BaseLevelCtrl.Awake()` fallback 查找，正式验收仍需 Unity Inspector 和 PlayMode 确认。
- **P1：动画资源缺失风险**。如果 actor Spine 数据没有 `controlhandle` 动画，`HasControlHandleAnimation()` 会返回 false 并跳过控制手柄表现；不会阻塞编译，但会缺少关卡表现。
- **P1：坐下/离开状态竞态风险**。坐下跳跃完成后的延迟任务可能与离开坐下、重开或对象禁用交错；本次通过 `controlHandleFlowVersion` 和销毁取消 token 降低旧任务回写风险。
- **P1：物理锁定风险**。控制手柄状态会清零移动输入和速度；如果后续 Level70 需要在控制手柄动画期间保留坠落或其他物理表现，需要重新评估。
- **P1：旧调用断开风险**。本次已按要求移除 TurnPage 兼容入口；静态扫描未发现 Level70 prefab 或 Level70 脚本引用这些入口，但如果 Unity 动画事件、未文本化资源或外部未提交脚本仍调用旧方法，需要在 Unity PlayMode 中补验。

### 验证证据

- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 warning / 0 error。
- `git diff --check -- Assets/Game/Scripts/Level/Actor/Level70ActorCtrl.cs` 通过，无空白错误。
- `rg` 静态扫描确认 `Level70ActorCtrl.cs` 不再包含 `Level61ActorCtrl`、`Debug.LogWarning`、`isTurningThePage`、`ControlHandleAniamName`、`TurningThePage`、`TurnPage` 残留。
- `rg` 静态检查确认 `Assembly-CSharp.csproj` 已包含 `Level70ActorCtrl.cs` 编译项。
- 2026-06-29 补充静态检查确认 `Level_070.prefab` 出现 `Level70ActorCtrl.cs.meta` 的 GUID `0c20f997e9eaf734a8b7a3fe62d397b7`，位置在 actor prefab override 的新增组件段。

### 未验证缺口

- 未运行 Unity PlayMode；未验证 Level70 进入、坐下、控制手柄动画、离开坐下、失败、重开、通关、退出和下一关。
- 未验证 `controlhandle` Spine 动画是否存在于 Level70 实际使用的 actor skeleton 数据中。
- 未验证 Android/iOS 横屏触摸路径和性能表现。
- 未验证当前 `Level_070.prefab` 未提交修改与本脚本的最终绑定关系。

### 回滚路径

- 脚本层回滚：恢复 `Level70ActorCtrl.cs` 修改前内容。
- 绑定层回滚：如果后续将 prefab actor 绑定到 `Level70ActorCtrl` 后出现异常，可在 Unity 中恢复为通用 `ActorCtrl`，并重新验证 `BaseLevelCtrl.actor`、`EnterDoorAnimCtrl.actor` 和相机 actor 引用。
- 流程层回滚：如果延迟控制手柄播放影响关卡节奏，可移除坐下完成后的 `EnterControlHandleAfterSit(...)` 调用。

## 2026-07-01 控制手柄恢复保护

### 原始行为基线

- `Level70Ctrl.RestoreActorControl()` 是 Player/actor 专用恢复入口，修改前会调用 `Level70ActorCtrl.RestoreLevel70NormalControl()` 并无条件恢复 Catch UI 与 `BaseLevelCtrl` 普通输入。
- `Level70ActorCtrl.RestoreLevel70NormalControl()` 修改前返回 `void`，即使 actor 正在播放 `controlhandle`，也会清理 `isControlHandle` 并调用 `ResumeMoving()`。
- `MarkLevel70CarriedByHook()`、`MarkLevel70AwaitingRestoreTrigger()`、`IsLevel70HookLocked` 和 `isAwaitingLevel70RestoreTrigger` 当前没有代码或 `Level_070.prefab` 文本调用依据。

### 影响文件与资源

- `Assets/Game/Scripts/Level/Actor/Level70ActorCtrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_70/Level70ActorCtrl功能说明.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_70/Level70ActorCtrl风险评估.md`
- `DevelopMD/已有功能设计与使用记录.md`
- 关联但未修改：`Assets/Game/Prefabs/Level/Level_070.prefab`、`Assets/Game/Scripts/Level/Actor/ActorCtrl.cs`、`Assets/Game/Scripts/Level/ActorSpineCtrl.cs`

### 已使用的现有系统

- `ActorCtrl.StopMoving()`、`ResumeMoving()`、`SetMoveInput(0f)`：维持 actor 移动锁定与恢复。
- `ActorSpineCtrl.PlayAnimation(...)` 与 Spine `TrackEntry.Complete`：监听 `controlhandle` 动画完成并结束控制手柄状态。
- `Level70Ctrl.RestoreActorControl()`：根据 actor 恢复结果决定是否恢复 Level70 Catch UI 和 `BaseLevelCtrl` 输入。

### 风险项

- **P1：恢复入口延迟风险**。`controlhandle` 播放期间触发 Player 恢复入口时，现在会拒绝恢复，可能导致玩家必须等动画完成后再次触发恢复入口；如果关卡空间只允许一次触发，需要 PlayMode 确认触发器可重复进入。
- **P1：动画完成回调风险**。恢复保护依赖 `TrackEntry.Complete` 结束 `isControlHandle`；如果 Spine 动画被中断、对象禁用或动画资源缺失，状态会由 `StopControlHandle()` 或 `ResetLevel70HookStateForCleanup()` 兜底，但实际恢复时机仍需 PlayMode 验证。
- **P1：旧无效 API 删除风险**。静态扫描未发现 `MarkLevel70CarriedByHook()`、`MarkLevel70AwaitingRestoreTrigger()`、`IsLevel70HookLocked` 被调用；若未文本化 UnityEvent 或外部未提交脚本引用这些成员，需在 Unity PlayMode 或 Inspector 中补验。
- **OBS：编译警告存量**。本次 `dotnet build` 通过但仍有 97 个工程警告，未逐项归因；本次变更未新增编译错误。

### 验证证据

- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 error、97 warning。
- `git diff --check -- Assets/Game/Scripts/Level/Actor/Level70ActorCtrl.cs Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs` 通过，仅有 Git LF/CRLF 工作区提示。
- 静态扫描确认 `RestoreLevel70NormalControl()` 当前只有 `Level70Ctrl.RestoreActorControl()` 调用，并已检查返回值。
- 静态扫描确认代码与 `Level_070.prefab` 文本未发现 `MarkLevel70CarriedByHook`、`MarkLevel70AwaitingRestoreTrigger` 调用。
- 静态风险词扫描本次两个脚本未新增 `OnValidate`、`Reset(`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、RemoteConfig、PlayerPrefs、StreamingAssets、Resources、Instantiate、PlayEvents 或 `Debug.Log` 链路。

### 未验证缺口

- 未运行 Unity PlayMode；尚未验证 `controlhandle` 期间触发恢复入口时 UI 和普通移动确实不恢复，以及动画完成后恢复入口可再次成功。
- 未验证失败、重开、退出、通关、下一关对 `ResetLevel70HookStateForCleanup()` 的兜底清理。
- 未验证 Android/iOS 横屏触摸路径。
- 未验证当前 `Level_070.prefab` 未提交修改与最终 Unity Inspector 绑定关系。

### 回滚路径

- 脚本层回滚：恢复 `Level70ActorCtrl.RestoreLevel70NormalControl()` 为 `void` 并移除 `controlhandle` 期间返回 `false` 的保护。
- 调用方回滚：恢复 `Level70Ctrl.RestoreActorControl()` 为无条件关闭 Catch UI、清理 `hookControlActive` 并调用 `BaseLevelCtrl.SetCtrlable(true)`。
- 无效 API 回滚：如发现外部非文本化引用，可恢复 `MarkLevel70CarriedByHook()`、`MarkLevel70AwaitingRestoreTrigger()` 和 `IsLevel70HookLocked`，再重新评估真实调用链。

## 2026-07-01 移除本地 Sit 覆盖逻辑

### 原始行为基线

- `Level70ActorCtrl` 修改前覆盖 `PlaySit()` / `StopSit()`，坐下时使用 DOTween 跳到目标点，并在坐下完成后通过 UniTask 延迟触发 `controlhandle`。
- `UpdateControlHandleState()` 修改前会在 `IsSit == true` 时走 `StopAllMotion()`，否则走 `StopHorizontalMotion()`。
- 静态扫描显示当前 `Level_070.prefab` 没有 `PlaySit` / `StopSit` 持久绑定，Level70 目录也没有 sitable/sofa 专用调用。

### 影响文件与资源

- `Assets/Game/Scripts/Level/Actor/Level70ActorCtrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_70/Level70ActorCtrl功能说明.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_70/Level70ActorCtrl风险评估.md`
- `DevelopMD/已有功能设计与使用记录.md`
- 关联但未修改：`Assets/Game/Scripts/Level/Actor/ActorCtrl.cs`、`Assets/Game/Prefabs/Level/Level_070.prefab`

### 已使用的现有系统

- `ActorCtrl`：保留共享默认 `PlaySit()` / `StopSit()` 能力，本次不修改共享脚本。
- `ActorSpineCtrl`：`Level70ActorCtrl` 仍只用于播放和检查 `controlhandle`。
- Bear.Logger：继续用于 Level70 本地 warning 输出。

### 风险项

- **P1：隐藏绑定风险**。文本化 prefab 与代码扫描未发现 Level70 绑定 `PlaySit` / `StopSit`，但如果有未文本化动画事件或运行时外部脚本调用本地 override，删除后会回落到 `ActorCtrl` 默认坐下行为，不再自动播放 `controlhandle`。
- **P1：行为节奏变化风险**。删除坐下后自动 `controlhandle` 后，`controlhandle` 只由抓钩控制入口触发；如果设计仍依赖坐下完成自动进入控制手柄，需要在 Unity PlayMode 中重新确认。
- **OBS：共享 Sit 仍存在**。本次没有删除 `ActorCtrl.PlaySit()` / `StopSit()`，其它关卡 Level37/45/61 的坐下调用不在本次变更范围内。

### 验证证据

- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 error、97 warning。
- `git diff --check -- Assets/Game/Scripts/Level/Actor/Level70ActorCtrl.cs` 通过，仅有 Git LF/CRLF 工作区提示。
- 静态扫描 `Level70ActorCtrl.cs` 未发现 `PlaySit`、`StopSit`、`IsSit`、`sofa`、`ControlHandleDelayAfterSit`、`EnterControlHandleAfterSit`、`PlaySofaIdleAfterSit`、`DOKill`、`DOJump`、`UniTask`、`DG.Tweening` 或 `Cysharp` 残留。
- 静态扫描 `Level_070.prefab` 与 Level70 目录未发现 `PlaySit` / `StopSit` 绑定或 sitable/sofa 专用调用。

### 未验证缺口

- 未运行 Unity PlayMode；未验证 Level70 进入抓钩控制、`controlhandle` 播放完成、恢复入口、失败、重开、退出、通关和下一关。
- 未验证 Android/iOS 横屏触摸路径。
- 未验证 Unity Inspector 中是否存在文本扫描覆盖不到的动画事件或临时运行时引用。

### 回滚路径

- 恢复 `Level70ActorCtrl` 的 `PlaySit()` / `StopSit()` override、DOTween/UniTask using、坐下完成后延迟触发 `controlhandle` 的方法，以及 `UpdateControlHandleState()` 的 `IsSit` 分支。

## 2026-07-01 controlhandle canMove 锁定修正

### 原始行为基线

- `ActorCtrl.canMove` 为私有字段，`SetMoveInput(0f)` 不会写入该字段；它只在 `canMove == false` 时把 `xInput` 清零，或在可移动时写入输入值。
- `ActorCtrl.StopMoving()` 会设置 `canMove=false` 并清空 Rigidbody2D 速度；`ResumeMoving()` 会设置 `canMove=true`。
- `Level70ActorCtrl` 修改前在 `controlhandle`/抓钩锁定的更新中只调用 `StopHorizontalMotion()` 或 `StopAllMotion()`，无法持续抵消共享流程中的 `ResumeMoving()`。
- `BaseLevelCtrl.GameResume()` 和 `TryToPlayEnterDoorAnim()` 等共享路径存在直接 `actor.ResumeMoving()` 调用；这些路径不感知 Level70 本地 `isControlHandle` 或 `isLevel70HookLocked`。

### 影响文件与资源

- `Assets/Game/Scripts/Level/Actor/Level70ActorCtrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_70/Level70ActorCtrl功能说明.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_70/Level70ActorCtrl风险评估.md`
- `DevelopMD/已有功能设计与使用记录.md`
- 关联但未修改：`Assets/Game/Scripts/Level/Actor/ActorCtrl.cs`、`Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs`、`Assets/Game/Scripts/Level/BaseLevelCtrl.cs`、`Assets/Game/Prefabs/Level/Level_070.prefab`

### 已使用的现有系统

- `ActorCtrl.StopMoving()`：使用既有公开入口在 Level70 局部把 `canMove` 重新设为 `false`，不为单关需求修改共享基类。
- `Level70ActorCtrl` 本地状态：`isControlHandle`、`isLevel70HookLocked`、`RestoreLevel70NormalControl()` 和两个锁定更新方法。
- `Level70Ctrl` 本地恢复返回值：继续依赖 `RestoreLevel70NormalControl()` 返回 `false` 时不恢复 UI 和普通输入。

### 风险项

- **P1：锁定帧与物理表现风险**。本次通过既有 `StopMoving()` 持续锁 `canMove`，会清空 Rigidbody2D 速度；如果 controlhandle 期间需要保留垂直速度，需要另行设计 Level70 局部方案并补 PlayMode 验证。
- **OBS：日志噪声风险**。`StopMoving()` 内部会输出日志，controlhandle/抓钩锁定更新持续调用时可能产生日志噪声；本次为避免修改共享 `ActorCtrl` 接受该局部代价，需 PlayMode 观察。
- **P1：外部 ResumeMoving 竞态风险已缓解但未实机验证**。暂停恢复、进门动画结束或生命周期顺序把 `canMove` 设回 `true` 后，Level70 锁定更新会再次写回 `false`；但同帧显示与实际输入仍需 Unity PlayMode 观察。
- **OBS：编译警告存量**。上次构建存在工程既有 warning，本次需重新执行构建确认无新增错误。

### 验证证据

- 已执行静态扫描，确认 `ActorCtrl.canMove` 直接写入点为 `SetSitState(bool)`、`Start()`、`ResumeMoving()` 和 `StopMoving()`；`SetMoveInput(0f)` 不写入 `canMove`。
- 已静态确认 `ActorCtrl.cs` 无本次工作区改动；本次没有新增共享基类 API。
- 已执行 `rg -n "LockMoveInput|LockLevel70MoveInput" Assets/Game/Scripts -g "*.cs"`，确认不存在 `ActorCtrl.LockMoveInput()`，`LockLevel70MoveInput()` 仅位于 `Level70ActorCtrl`。
- 已执行 `rg` 静态扫描，`Level70ActorCtrl.cs` 未发现 Sit、DOTween 或 UniTask 相关残留。
- 已执行风险词静态扫描，本次两个脚本未新增 `OnValidate`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、RemoteConfig、PlayerPrefs、StreamingAssets、Resources、Instantiate、PlayEvents 或 `Debug.Log` 链路。
- 已执行 `git diff --check -- Assets/Game/Scripts/Level/Actor/Level70ActorCtrl.cs Assets/Game/Scripts/Level/LevelItem/Level_70/Level70ActorCtrl功能说明.md Assets/Game/Scripts/Level/LevelItem/Level_70/Level70ActorCtrl风险评估.md DevelopMD/已有功能设计与使用记录.md`，无空白错误，仅有 Git LF/CRLF 工作区提示。
- 已执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`，结果为 0 error、97 warning。

### 未验证缺口

- 未运行 Unity PlayMode；尚未验证 `controlhandle` 期间 `canMove` 在 Inspector 或断点中持续为 `false`。
- 未验证暂停/恢复、恢复触发器、动画结束、失败、重开、退出、通关和下一关路径。
- 未验证 Android/iOS 横屏触摸路径。

### 回滚路径

- 回滚 `Level70ActorCtrl` 中 `LockLevel70MoveInput()` 以及进入、拒绝恢复、锁定更新中的调用。
- 回滚后如果再次遇到 `canMove=true`，需要改用更大范围的共享移动状态协议或在所有外部 `ResumeMoving()` 调用方加 Level70 特判。
