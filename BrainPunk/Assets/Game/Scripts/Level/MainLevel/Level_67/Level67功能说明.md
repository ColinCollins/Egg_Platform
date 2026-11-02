# Level67 降落伞控制功能说明

## 需求背景

`Level67Ctrl` 原脚本只有注释、四个序列化引用和空 `Start` / `Update`。注释要求：关注 actor，当高度低于阈值点时打开 parachute，并把 actor 的 `Rigidbody2D.gravityScale` 变为 `0.2`；监听左右移动，让 actor 绕 Z 轴按反方向在范围内转动。

本次实现保留原需求注释，不修改注释文本，只在类体内补齐运行时逻辑。

## 影响文件和系统

- `Assets/Game/Scripts/Level/LevelItem/Level_67/Level67Ctrl.cs`
- `Assets/Game/Prefabs/Level/Level_067.prefab`
- 读取但未修改：`ActorCtrl.XInput`、`ActorCtrl.RB`
- 未修改：`ActorCtrl`、`BaseLevelCtrl`、移动事件、存档、配置、资源加载、UI、analytics、平台 SDK。

## 行为前后

- 修改前：Level67 脚本不执行开伞、重力变化或旋转。
- 修改后：actor 低于 `limitPoint` 后只部署一次降落伞，把刚体重力设为 `0.2`，并在部署后根据 `ActorCtrl.XInput` 反向倾斜 actor 根节点。
- 取消移动或输入为 0 时，actor 根节点按配置速度回到缓存中立旋转。
- 清理时停止开伞 tween、清除部署状态并恢复 actor 旋转；不主动恢复刚体重力，重开入口状态依赖 prefab 或关卡加载流程。

## 数据、资源和事件流

- 数据来源：Unity Inspector 序列化引用和本地配置字段。
- 输入来源：共享 `BaseLevelCtrl` 仍按原逻辑把左右/取消移动写入 `ActorCtrl.SetMoveInput()`；Level67 只读取 `ActorCtrl.XInput`。
- 表现：`parachute.DOScale(Vector3.one, openDuration).SetEase(Ease.OutBack)`。
- 物理：只在部署时写一次 `actorRigid.gravityScale = 0.2f`。
- 事件：本次不新增事件，不订阅共享事件，不改变移动事件语义。

## 预制体绑定状态

`Level_067.prefab` 已追加 `Level67Ctrl` 组件并绑定 `actor`。当前静态文本证据无法可靠识别 `limitPoint` 和 `parachute`，因此这两个引用仍需 Unity Inspector 明确绑定。脚本缺少关键引用时只输出一次 warning 并跳过控制，避免空引用崩溃。

## 验证记录

- 静态检查：脚本未引入编辑器生命周期、DB、Save、RemoteConfig、Instantiate。
- 静态检查：原 `Level30Ctrl` 组件保留，避免破坏现有关卡钥匙/翅膀拖拽链路。
- 编译：当前 worktree 没有 `Assembly-CSharp.csproj`，未能用 `dotnet build` 验证。
- PlayMode、Inspector 绑定、Android/iOS 横屏触控和 60 fps：未在当前环境执行，需要后续补测。

## 剩余风险

- `limitPoint` 和 `parachute` 需要人工绑定；绑定错误会导致不开伞或错误视觉对象缩放。
- actor root 旋转可能影响碰撞、门触发、失败区和完成判定，必须 PlayMode 验证。
- `Level30Ctrl` 与新增 `Level67Ctrl` 并存是最小风险选择，但仍需确认两者不会抢占同一对象表现。
- 重开后的 gravity 初始状态依赖 prefab/关卡入口流程，必须在 Unity 中验证。

## 回滚路径

删除 `Level_067.prefab` 上新增的 `Level67Ctrl` 组件，并回退 `Level67Ctrl.cs` 的本次实现代码，可恢复到只有注释和空方法的原始行为；共享系统无需回滚。

## 2026-07-09 OpenParachute 左右衰减晃动补充

### 需求背景

本次需求是在 `Level67Ctrl.OpenParachute(Transform target, Vector2 pos)` 触发开伞时补充一个 actor 左右来回旋转表现，默认过程 2 秒，晃动幅度逐渐减弱，并把角度和时长暴露到 Inspector。

### 影响文件和系统

- `Assets/Game/Scripts/Level/LevelItem/Level_67/Level67Ctrl.cs`
- 未修改 `Level_067.prefab`、`ActorCtrl`、`BaseLevelCtrl`、存档、配置、资源加载、UI、analytics、广告、IAP、RemoteConfig 或平台 SDK。

### 行为前后

- 修改前：当前工作区的 `OpenParachute()` 已调用空的 `TweenShakeRotation()`，开伞后没有额外 actor 晃动表现。
- 修改后：`OpenParachute()` 会先解析运行时引用并经过 `CanRun()` 保护；只有本次调用实际完成首次部署时，才执行 `TweenShakeRotation()`。
- `TweenShakeRotation()` 使用 DOTween Sequence 让 actor 以 `actorNeutralRotation` 为基准左右衰减旋转，默认 `shakeRotationAngle = 12f`、`shakeRotationDuration = 2f`。
- 晃动期间 `UpdateActorTilt()` 暂停输入倾斜，避免开伞晃动和持续输入旋转同时写 actor rotation；晃动结束后回到中立角度，再恢复原有输入倾斜逻辑。
- `OnDisable()` / `OnDestroy()` 清理时会停止 `actorShakeRotationTween` 并恢复 actor 缓存中立旋转。

### 数据、资源和事件流

- 数据来源：新增 Inspector 字段 `shakeRotationAngle` 和 `shakeRotationDuration`。
- 表现：DOTween 本地 Sequence，不新增资源、不实例化对象、不访问 DB/save。
- 事件：不新增 `PlayEvents`、全局事件、订阅或派发。
- 平台：运行时代码只依赖 Unity Transform 和 DOTween；Android/iOS 横屏实际触控手感仍需设备或 PlayMode 继续验证。

### 验证记录

- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_67/Level67Ctrl.cs` 通过；仅有工作区 LF/CRLF 提示。
- 敏感关键字静态扫描未发现目标脚本新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB/save、RemoteConfig、Instantiate、`PlayEvents` 或 `DispatchEvent` 链路。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 98 warnings / 0 errors。

### 剩余风险

- 未在 Unity PlayMode 实测 actor 开伞晃动、结束回正、持续按左右时晃动结束后的倾斜衔接。
- 未验证 `Level_067.prefab` 中新增 Inspector 字段的最终设计值，当前默认值只是代码兜底。
- 未做 Android/iOS 横屏真机 smoke，移动端手感、碰撞影响和帧率仍需补验。

### 回滚路径

回退 `Level67Ctrl.cs` 中 `shakeRotationAngle`、`shakeRotationDuration`、`actorShakeRotationTween`、`TweenShakeRotation()` 实现、`UpdateActorTilt()` 的晃动暂停判断以及清理逻辑，即可恢复到开伞无额外左右晃动的当前工作区行为。

## 2026-07-09 通关后释放 actor rotation 控制

### 需求背景

用户确认 Level67 触发通关后不应再由 `Level67Ctrl` 限制或写入 actor 的 rotation。当前代码证据显示，开伞后 `UpdateActorTilt()` 会持续按 `maxTiltAngle` 限制并写入 actor rotation，开伞晃动 tween 也会在播放期间写入 rotation，因此需要在通关后释放这两个写入源。

### 影响文件和系统

- `Assets/Game/Scripts/Level/LevelItem/Level_67/Level67Ctrl.cs`
- 读取但未修改：`Game.Level.BaseLevelCtrl.IsSuccess()`
- 未修改 `Level_067.prefab`、通关判定、通关 UI、存档、配置、资源加载、全局事件、analytics、广告、IAP、RemoteConfig 或平台 SDK。

### 行为前后

- 修改前：开伞后只要 `Level67Ctrl` 仍在 `Update()`，`UpdateActorTilt()` 会继续把 actor rotation 写到 `actorNeutralRotation + currentTiltAngle`，通关后仍可能覆盖通关表现。
- 修改后：`Level67Ctrl` 运行时解析同关卡 `BaseLevelCtrl`，当 `IsSuccess()` 为 true 时调用 `ReleaseActorRotationControlAfterSuccess()`。
- 释放逻辑会停止当前 `actorShakeRotationTween`，关闭 `isActorShakeRotating`，并阻止后续 `UpdateActorTilt()` 继续写 rotation。
- 释放时不会强制把 actor rotation 回正；后续 `OnDisable()` / `OnDestroy()` 清理也不会在通关释放后恢复缓存中立旋转，避免覆盖已经触发的通关表现或后续通关动画控制。

### 验证记录

- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 98 warnings / 0 errors。
- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_67/Level67Ctrl.cs` 通过；仅有工作区 LF/CRLF 提示。
- 敏感关键字静态扫描未发现新增编辑器生命周期、DB/save、RemoteConfig、Instantiate、`PlayEvents` 或 `DispatchEvent` 链路。

### 剩余风险

- 未在 Unity PlayMode 验证 actor 进入门触发通关后，通关动画是否完全接管 rotation。
- 未验证开伞晃动进行中触发通关时，停止 tween 后当前 actor 姿态是否符合美术预期。
- Android/iOS 横屏 smoke 仍待补验。

### 回滚路径

回退 `Level67Ctrl.cs` 中 `levelCtrl` 解析、`IsLevelSuccess()`、`ReleaseActorRotationControlAfterSuccess()` 以及相关通关判断，即可恢复到通关后仍由 Level67 开伞旋转逻辑控制 actor rotation 的旧行为。
