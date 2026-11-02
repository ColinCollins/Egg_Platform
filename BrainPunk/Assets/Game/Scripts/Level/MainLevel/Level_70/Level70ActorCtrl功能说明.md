# Level70 Actor 控制手柄功能说明

## 2026-06-29 脚本重写记录

### 需求与背景

- 本次需求为重写 `Level70ActorCtrl` 脚本，并保持脚本顶部原有注释不变。
- 本地证据显示 `Assets/Game/Scripts/Level/Actor/Level70ActorCtrl.cs` 顶部注释描述的是 `controlhandle` 控制手柄动画；修改前脚本内部仍沿用 Level61 翻页状态命名、`Level61ActorCtrl` 日志标签和 `ControlHandleAniamName` 拼写。

### 影响文件与系统

- 修改脚本：`Assets/Game/Scripts/Level/Actor/Level70ActorCtrl.cs`
- 相关关卡主控：`Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs`
- 相关 prefab：`Assets/Game/Prefabs/Level/Level_070.prefab`
- 依赖系统：`ActorCtrl` 基础移动/坐下流程、`ActorSpineCtrl.PlayAnimation(...)`、DOTween 坐下跳跃、UniTask 延迟、Bear.Logger 日志。
- 本次未修改存档、配置、资源加载、热更、广告、内购、analytics 或平台 SDK。

### 修改前行为基线

- `Level70ActorCtrl` 继承 `ActorCtrl`，坐下后播放 `sofa_jump`，完成后播放 `sofa_idle`，延迟 1 秒尝试播放 `controlhandle`。
- 控制状态期间脚本会停止水平移动；坐下状态下停止全部速度；离开坐下时恢复父节点和 Rigidbody2D Dynamic。
- 修改前状态变量和公开入口沿用 `isTurningThePage`、`PlayTurningThePageOnce()` 等 Level61 翻页命名，但实际动画名已经改为 `controlhandle`。
- 修改前日志标签仍写为 `[Level61ActorCtrl]`，排查 Level70 动画缺失或引用缺失时容易误判来源。

### 修改后行为

- 内部状态改为 `isControlHandle`，状态更新方法改为 `UpdateControlHandleState()`，动画检查方法改为 `HasControlHandleAnimation()`。
- `ControlHandleAniamName` 拼写修正为 `ControlHandleAnimName`，统一指向 `controlhandle` 动画。
- 已移除 `IsTurningThePage`、`PlayTurningThePage()`、`PlayTurningThePageOnce()`、`TriggerTurningThePage()`、`StopTurningThePage()` 等 TurnPage 兼容层，Level70 脚本不再暴露翻页语义。
- 坐下后延迟播放控制手柄动画时增加 `controlHandleFlowVersion` 校验；离开坐下、禁用对象或重新进入坐下流程后，旧延迟任务不会再播放控制手柄动画。
- 延迟等待使用 `GetCancellationTokenOnDestroy()` 并抑制销毁取消异常，避免对象销毁后继续回调。
- 日志标签改为 `[Level70ActorCtrl]`，并改用 Bear.Logger 的 `this.LogWarning(...)`。

### 数据、资源与事件流

- 数据：无新增持久化字段，无 DB / Save / RemoteConfig / Luban 配置变更。
- 资源：脚本仍依赖 actor Spine 数据中存在 `controlhandle` 动画；缺失时只记录 warning 并跳过。
- 事件：本次未新增 Bear.EventSystem 事件；脚本不再提供 TurnPage 兼容入口，现有 UnityEvent、动画事件或其他脚本如仍引用旧方法需重新绑定。
- 物理：控制手柄状态期间仍通过 `SetMoveInput(0f)` 和 `Rigidbody2D.linearVelocity` 停止移动，不新增碰撞层或 Rigidbody 配置。

### 验证记录

- 已执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`，结果为 0 warning / 0 error。
- 已执行 `git diff --check -- Assets/Game/Scripts/Level/Actor/Level70ActorCtrl.cs`，无输出，说明本次脚本文件未发现空白错误。
- 已静态扫描 `Level70ActorCtrl.cs`，未发现 `Level61ActorCtrl`、`Debug.LogWarning`、`isTurningThePage`、`ControlHandleAniamName`、`TurningThePage`、`TurnPage` 等旧残留。
- 已静态确认 `Assembly-CSharp.csproj` 包含 `Assets\Game\Scripts\Level\Actor\Level70ActorCtrl.cs`，上述构建覆盖本脚本。

### 剩余风险与未验证项

- 2026-06-29 补充静态复核：`Level_070.prefab` 的 actor prefab override 中已能找到 `Level70ActorCtrl.cs.meta` 的 GUID `0c20f997e9eaf734a8b7a3fe62d397b7`；但根 `BaseLevelCtrl.actor` 仍为 `{fileID: 0}`，仍需 Unity Inspector 确认运行时 actor 引用和组件绑定。
- 未运行 Unity PlayMode，尚未验证坐下、延迟 `controlhandle` 播放、离开坐下、失败/重开/退出后的实际表现。
- 未进行 Android/iOS 横屏触摸 smoke；控制手柄动画触发、移动锁定和帧率表现仍需设备验证。
- `Level70Ctrl.cs` 当前工作区存在未提交修改；2026-06-29 抓钩控制实现复核中 `git diff --check` 对 Level70 三个脚本通过，仅有 LF/CRLF 工作区提示。

### 回滚路径

- 回滚 `Assets/Game/Scripts/Level/Actor/Level70ActorCtrl.cs` 到修改前版本即可恢复原 Level61 风格实现。
- 如果后续确认 prefab 绑定需要回滚，应在 Unity 中恢复 Level70 actor 使用通用 `ActorCtrl` 或原绑定脚本，并重新验证 Level70 进入、重开、失败、通关和下一关链路。

## 2026-07-01 控制手柄恢复保护与无效代码整理

### 需求与背景

- 本次需求为整理 `Level70ActorCtrl` 无效代码，并确保 actor 处于 `controlhandle` 动画状态时不能被恢复为普通移动。
- 本地证据显示 `Level70Ctrl.RestoreActorControl()` 会调用 `Level70ActorCtrl.RestoreLevel70NormalControl()`，修改前该入口会无条件停止 `controlhandle` 并调用 `ResumeMoving()`。

### 影响文件与系统

- 修改脚本：`Assets/Game/Scripts/Level/Actor/Level70ActorCtrl.cs`
- 同步调用方：`Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs`
- 文档：`Assets/Game/Scripts/Level/LevelItem/Level_70/Level70ActorCtrl功能说明.md`、`Assets/Game/Scripts/Level/LevelItem/Level_70/Level70ActorCtrl风险评估.md`、`DevelopMD/已有功能设计与使用记录.md`
- 依赖系统仍为 `ActorCtrl` 移动锁定、`ActorSpineCtrl.PlayAnimation(...)`、Spine `TrackEntry.Complete` 回调、Bear.Logger 日志；未新增全局事件、存档、配置、资源加载、analytics、广告、内购或平台 SDK 链路。

### 修改前行为基线

- `RestoreLevel70NormalControl()` 返回 `void`，即使 `isControlHandle == true` 也会清理状态并调用 `ResumeMoving()`。
- `MarkLevel70CarriedByHook()`、`MarkLevel70AwaitingRestoreTrigger()`、`IsLevel70HookLocked` 以及 `isAwaitingLevel70RestoreTrigger` 已无代码或 prefab 文本调用。
- `StopAllMotion()` 只保留了一个空的 `Rigidbody2D` 判断分支，没有运行时效果。

### 修改后行为

- `RestoreLevel70NormalControl()` 改为返回 `bool`：`isControlHandle == true` 时只清零水平输入并返回 `false`，不调用 `ResumeMoving()`。
- `Level70Ctrl.RestoreActorControl()` 只在 actor 返回恢复成功后，才关闭 Catch UI、清理 `hookControlActive` 并调用 `BaseLevelCtrl.SetCtrlable(true)`。
- `controlhandle` 动画播放时记录本地流程版本号，并在 `TrackEntry.Complete` 中按版本校验结束 `isControlHandle`，避免旧动画回调改写新状态。
- 删除未使用的 `MarkLevel70CarriedByHook()`、`MarkLevel70AwaitingRestoreTrigger()`、`IsLevel70HookLocked`、`isAwaitingLevel70RestoreTrigger` 和 `StopAllMotion()` 空分支。
- `ResetLevel70HookStateForCleanup()` 仍作为失败、重开、退出或禁用时的兜底清理入口，可强制清掉 Level70 锁定并恢复 actor，避免残留锁死。

### 验证记录

- 已执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`，结果为 0 error、97 warning。
- 已执行 `git diff --check -- Assets/Game/Scripts/Level/Actor/Level70ActorCtrl.cs Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs`，无空白错误，仅有 Git LF/CRLF 工作区提示。
- 已静态扫描 `RestoreLevel70NormalControl()` 调用点，当前代码中仅 `Level70Ctrl.RestoreActorControl()` 调用并检查返回值。
- 已静态扫描 `MarkLevel70CarriedByHook`、`MarkLevel70AwaitingRestoreTrigger`，代码与 `Level_070.prefab` 文本未发现调用。
- 已静态风险词扫描本次两个脚本，未新增 `OnValidate`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、RemoteConfig、PlayerPrefs、StreamingAssets、Resources、Instantiate、PlayEvents 或 `Debug.Log` 链路。

### 剩余风险与未验证项

- 未运行 Unity PlayMode；Level70 进入抓钩控制、`controlhandle` 期间触发恢复入口、动画结束后再次恢复、失败、重开、退出和下一关仍需补验。
- 未验证 Android/iOS 横屏触摸路径和真机性能。
- 当前 `Level70Ctrl.cs`、`Level_070.prefab` 以及多个非本需求文件在工作区已有未提交修改，本记录只覆盖本次脚本逻辑边界。

### 回滚路径

- 回滚 `Assets/Game/Scripts/Level/Actor/Level70ActorCtrl.cs` 中 `RestoreLevel70NormalControl()` 返回值、`TrackEntry.Complete` 回调和无效成员删除，即恢复旧恢复行为。
- 回滚 `Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs` 中对 `RestoreLevel70NormalControl()` 返回值的判断，即恢复调用方无条件恢复 UI 与 `BaseLevelCtrl` 输入的旧行为。

## 2026-07-01 移除本地 Sit 覆盖逻辑

### 需求与背景

- 本次需求为删除 `Level70ActorCtrl` 中 Sit 相关代码。
- 本地证据显示当前 `Level_070.prefab` 文本未绑定 `PlaySit()` 或 `StopSit()`，Level70 目录代码也没有 sitable/sofa 入口；坐下能力仍由共享 `ActorCtrl` 和其它关卡脚本使用。

### 影响文件与系统

- 修改脚本：`Assets/Game/Scripts/Level/Actor/Level70ActorCtrl.cs`
- 文档：`Assets/Game/Scripts/Level/LevelItem/Level_70/Level70ActorCtrl功能说明.md`、`Assets/Game/Scripts/Level/LevelItem/Level_70/Level70ActorCtrl风险评估.md`、`DevelopMD/已有功能设计与使用记录.md`
- 未修改 `ActorCtrl`、`Level70Ctrl`、`Level_070.prefab`、存档、配置、资源加载、UI、analytics、广告、内购或平台 SDK。

### 修改前行为基线

- `Level70ActorCtrl` 自己覆盖 `PlaySit()` / `StopSit()`，坐下时使用 DOTween `DOJump`，并在坐下完成后通过 UniTask 延迟 1 秒尝试播放 `controlhandle`。
- `UpdateControlHandleState()` 仍按 `IsSit` 分支决定停止全部移动或只停止水平移动。
- 文件顶部依赖 `Cysharp.Threading.Tasks` 和 `DG.Tweening`。

### 修改后行为

- 删除 `Level70ActorCtrl.PlaySit()`、`StopSit()`、`PlaySofaIdleAfterSit()`、`EnterControlHandleAfterSit(...)`、`SitJumpPower`、`SitJumpDuration`、`ControlHandleDelayAfterSit`。
- 删除 `Cysharp.Threading.Tasks` 与 `DG.Tweening` using，`Level70ActorCtrl` 不再直接依赖 Sit 延迟或 DOTween 坐下跳跃。
- `UpdateControlHandleState()` 固定停止水平移动，不再检查 `IsSit`。
- `controlhandle` 只由 `EnterLevel70HookControl()` 进入抓钩控制时触发；共享 `ActorCtrl.PlaySit()` / `StopSit()` 未改动。

### 验证记录

- 已执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`，结果为 0 error、97 warning。
- 已执行 `git diff --check -- Assets/Game/Scripts/Level/Actor/Level70ActorCtrl.cs`，无空白错误，仅有 Git LF/CRLF 工作区提示。
- 已静态扫描 `Level70ActorCtrl.cs`，未发现 `PlaySit`、`StopSit`、`IsSit`、`sofa`、`ControlHandleDelayAfterSit`、`EnterControlHandleAfterSit`、`PlaySofaIdleAfterSit`、`DOKill`、`DOJump`、`UniTask`、`DG.Tweening` 或 `Cysharp` 残留。
- 已静态扫描 Level70 prefab 与 Level70 目录，未发现 `Level_070.prefab` 绑定 `PlaySit` / `StopSit`，也未发现 Level70 专用 sitable/sofa 调用。

### 剩余风险与未验证项

- 未运行 Unity PlayMode；仍需验证 Level70 抓钩入口、`controlhandle` 播放、恢复入口、失败/重开/退出/通关/下一关。
- 未验证 Android/iOS 横屏触摸路径。
- 如果未来 Level70 重新需要本地 Sit 表现，应重新设计专用入口和验证矩阵，而不是依赖已删除的坐下后自动 `controlhandle` 逻辑。

### 回滚路径

- 回滚 `Assets/Game/Scripts/Level/Actor/Level70ActorCtrl.cs` 中删除的 Sit override、DOTween/UniTask using、坐下后延迟触发 `controlhandle` 方法和 `UpdateControlHandleState()` 的 `IsSit` 分支即可恢复本地 Sit 覆盖逻辑。

## 2026-07-01 controlhandle 状态 canMove 锁定修正

### 需求与背景

- 本次问题为排查并修复 actor 处于 handlecontrol/controlhandle 状态时，`ActorCtrl.canMove` 仍可能显示为 `true` 的情况。
- 本地证据显示 `ActorCtrl.canMove` 是私有字段，只有 `Start()`、`ResumeMoving()`、`StopMoving()` 和 `SetSitState(bool)` 直接写入；`SetMoveInput(0f)` 只清理 `xInput`，不会把 `canMove` 改为 `false`。
- 修改前 `Level70ActorCtrl.UpdateControlHandleState()` 与 `UpdateLevel70HookLockedState()` 主要通过 `StopHorizontalMotion()` / `StopAllMotion()` 清输入，不能在上层 `ResumeMoving()` 或 `ActorCtrl.Start()` 把 `canMove` 改回 `true` 后持续纠正。

### 影响文件与系统

- 修改脚本：`Assets/Game/Scripts/Level/Actor/Level70ActorCtrl.cs`
- 文档：`Assets/Game/Scripts/Level/LevelItem/Level_70/Level70ActorCtrl功能说明.md`、`Assets/Game/Scripts/Level/LevelItem/Level_70/Level70ActorCtrl风险评估.md`、`DevelopMD/已有功能设计与使用记录.md`
- 关联调用方：`Assets/Game/Scripts/Level/LevelItem/Level_70/Level70Ctrl.cs` 仍通过 `EnterLevel70HookControl()` 和 `RestoreLevel70NormalControl()` 控制 actor 锁定与恢复。
- 本次未修改 `ActorCtrl`、`GamePlayPanel`、prefab、UI、存档、配置、资源加载、analytics、广告、内购或平台 SDK。

### 修改前行为基线

- `EnterLevel70HookControl()` 会调用 `StopMoving()`，进入抓钩控制时通常会把 `canMove` 设为 `false`。
- `RestoreLevel70NormalControl()` 在 `isControlHandle == true` 时返回 `false`，但只调用 `StopHorizontalMotion()`，不会显式重置 `canMove`。
- `controlhandle` 或抓钩锁定帧内只清理输入和速度，没有持续写入 `canMove=false`；如果暂停恢复、进门动画结束、生命周期 `Start()` 或其它共享流程调用过 `ResumeMoving()`，`canMove` 可能重新变为 `true`。

### 修改后行为

- 不修改共享 `ActorCtrl`；`Level70ActorCtrl` 本地新增 `LockLevel70MoveInput()`，内部只调用已有 `StopMoving()` 来把 `canMove` 重新压为 `false`。
- `Level70ActorCtrl` 在进入 `controlhandle` 后立即调用本地 `LockLevel70MoveInput()`，确保动画状态自身拥有移动权限锁。
- `RestoreLevel70NormalControl()` 在 `isControlHandle == true` 拒绝恢复时，也会重新锁定移动输入权限，避免恢复触发器把状态留在 `canMove=true`。
- `UpdateControlHandleState()` 与 `UpdateLevel70HookLockedState()` 每帧先调用 `LockLevel70MoveInput()`，持续抵消外部 `ResumeMoving()` 或 `Start()` 对 `canMove` 的恢复。
- 由于只能通过既有 `StopMoving()` 写入 `ActorCtrl.canMove=false`，`controlhandle`/抓钩锁定更新会清空 Rigidbody2D 速度；这是为避免改共享基类而保留在 Level70 局部的取舍。

### 数据、资源与事件流

- 数据：无新增持久化数据、DB、Save、RemoteConfig 或 Luban 配置。
- 资源：不新增动画、prefab、材质或热更资源。
- 事件：不新增 PlayEvents 或 Bear.EventSystem 事件；仍沿用 `Level70Ctrl` 本地直接引用 actor。
- 物理：本地锁定通过既有 `StopMoving()` 改写 `canMove=false` 并清空 Rigidbody2D 速度，随后继续执行原有 `StopHorizontalMotion()` / `StopAllMotion()` 清输入流程。

### 验证记录

- 已静态确认 `ActorCtrl.cs` 无本次工作区改动；本次没有新增共享基类 API。
- 已执行 `rg -n "LockMoveInput|LockLevel70MoveInput" Assets/Game/Scripts -g "*.cs"`，确认不存在 `ActorCtrl.LockMoveInput()`，`LockLevel70MoveInput()` 仅位于 `Level70ActorCtrl`。
- 已执行 `rg` 静态扫描，`Level70ActorCtrl.cs` 未发现 `PlaySit`、`StopSit`、`IsSit`、`sofa`、`DOKill`、`DOJump`、`UniTask`、`DG.Tweening` 或 `Cysharp` 残留。
- 已执行风险词静态扫描，本次两个脚本未新增 `OnValidate`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、RemoteConfig、PlayerPrefs、StreamingAssets、Resources、Instantiate、PlayEvents 或 `Debug.Log` 链路。
- 已执行 `git diff --check -- Assets/Game/Scripts/Level/Actor/Level70ActorCtrl.cs Assets/Game/Scripts/Level/LevelItem/Level_70/Level70ActorCtrl功能说明.md Assets/Game/Scripts/Level/LevelItem/Level_70/Level70ActorCtrl风险评估.md DevelopMD/已有功能设计与使用记录.md`，无空白错误，仅有 Git LF/CRLF 工作区提示。
- 已执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`，结果为 0 error、97 warning。
- 仍需 Unity PlayMode 验证 handlecontrol/controlhandle 期间 Inspector 中 `canMove` 保持 `false`，暂停恢复、恢复触发器、失败、重开、退出后状态符合预期。

### 剩余风险与未验证项

- 未运行 Unity PlayMode；无法确认 Inspector 显示与实际输入在所有时间点完全一致。
- 未验证 Android/iOS 横屏触摸路径。
- `LockLevel70MoveInput()` 复用既有 `StopMoving()`，会产生清空速度和日志输出；这是当前不改共享 `ActorCtrl` 前提下持续保证 `canMove=false` 的剩余代价，需 PlayMode 观察是否有日志噪声或表现副作用。

### 回滚路径

- 回滚 `Level70ActorCtrl.LockLevel70MoveInput()` 及其进入、拒绝恢复、锁定更新中的调用，即恢复旧的只清输入/速度、不持续写入 `canMove=false` 行为。
