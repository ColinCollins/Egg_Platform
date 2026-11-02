# Level67 降落伞控制风险评估

## 原始行为基线

- 原 `Level67Ctrl.cs` 只有需求注释、`actor`、`limitPoint`、`parachute`、`actorRigid` 四个字段和空 `Start` / `Update`。
- `Level_067.prefab` 原根对象包含基础关卡控制组件和 `Level30Ctrl`，未静态发现 `Level67Ctrl` 组件。
- Actor prefab 源刚体初始 `gravityScale` 为 `4.5`。

## 受影响资源

- `Assets/Game/Scripts/Level/LevelItem/Level_67/Level67Ctrl.cs`
- `Assets/Game/Prefabs/Level/Level_067.prefab`
- 关联但未修改：Actor prefab、`ActorCtrl`、`BaseLevelCtrl`、Level30 相关拖拽/钥匙逻辑。

## 复用系统

- `ActorCtrl.XInput`：读取当前左右输入。
- `ActorCtrl.RB` / `Rigidbody2D`：解析主角刚体。
- DOTween：一次性开伞缩放。
- Bear.Logger：缺少引用时输出 warning。

## 风险项

- P1：`limitPoint` 未绑定或绑定错误会导致不开伞、过早开伞或高度判断错误。
- P1：`parachute` 未绑定或绑定错误会导致没有视觉反馈或缩放错误对象。
- P1：actor root 旋转可能改变碰撞体姿态，影响门、失败区、障碍碰撞和完成流程。
- P1：Level_067 原有 `Level30Ctrl` 仍存在，新增 `Level67Ctrl` 需要确认不会和原拖拽/钥匙逻辑冲突。
- P2：清理不恢复 gravity 是需求澄清结果，但重开是否回到初始 gravity 依赖关卡加载/prefab 状态，必须运行验证。
- P2：本环境未运行 Unity 编译，存在命名空间、序列化或 DOTween 运行时细节待 Editor 验证。

## 已完成验证

- 静态证据：`Level67Ctrl` 使用 `Start`、`Update`、`OnDisable`、`OnDestroy`，未使用编辑器生命周期入口。
- 静态证据：未修改共享 `ActorCtrl`、`BaseLevelCtrl`、移动事件、存档、配置、资源加载、UI 或平台 SDK。
- 静态证据：开伞逻辑通过 `isParachuteDeployed` 防重复。
- 静态证据：旋转角度由 `Mathf.Clamp(actor.XInput, -1, 1)` 和 `maxTiltAngle` 限制。

## 未验证缺口

- Unity Inspector 中 `limitPoint`、`parachute` 的真实绑定。
- Unity Editor 编译。
- Level67 PlayMode：10 次开伞、10 次重力、20 次左右旋转、10 次取消回正。
- 倾斜状态下碰撞、门、失败区、完成流程。
- restart/fail/exit/complete/disable/re-entry 后的 gravity、旋转和 tween 状态。
- Android/iOS 横屏触控与 60 fps 表现。
- 一个非 Level67 关卡移动/重力回归。

## 回滚路径

- 回滚 `Level67Ctrl.cs` 的新增实现，保留原需求注释。
- 删除 `Level_067.prefab` 根对象新增的 `Level67Ctrl` 组件引用。
- 若只发现旋转风险，可先把 `maxTiltAngle` 调小或临时禁用部署后的 `UpdateActorTilt()`，再复测。

## 待澄清

- `limitPoint` 在场景中的最终设计对象。
- `parachute` 视觉对象的最终设计对象和初始关闭方式。
- `maxTiltAngle`、`tiltSpeed`、`neutralReturnSpeed` 是否需要设计手感微调。

## 2026-07-09 OpenParachute 左右衰减晃动风险评估

### 原始行为基线

- 当前工作区中 `OpenParachute(Transform target, Vector2 pos)` 已作为外部触发开伞入口，内部调用 `DeployParachute()` 和空的 `TweenShakeRotation()`；本次补充后晃动只在首次部署成功时播放。
- `DeployParachute()` 已负责降落伞缩放、actor 下落速度、重力、移动速度和升力状态切换。
- `UpdateActorTilt()` 在 `isParachuteDeployed` 后每帧读取 `actor.XInput` 并写 actor 根节点 rotation。

### 受影响文件和资源

- 直接修改：`Assets/Game/Scripts/Level/LevelItem/Level_67/Level67Ctrl.cs`
- 本次未修改：`Assets/Game/Prefabs/Level/Level_067.prefab`、Actor prefab、`ActorCtrl`、`BaseLevelCtrl`、存档、配置、资源加载、全局事件、analytics、广告、IAP、RemoteConfig、平台 SDK。

### 已使用的现有系统

- DOTween Sequence：执行本地 actor rotation 左右衰减表现。
- `actorNeutralRotation`：继续作为 actor 根节点旋转的基准。
- `CanRun()` / Bear.Logger warning：`OpenParachute()` 入口现在先做运行时引用保护。

### 风险项

- P1：`TweenShakeRotation()` 和 `UpdateActorTilt()` 都会写 actor rotation；当前通过 `isActorShakeRotating` 在晃动期间暂停输入倾斜，仍需 PlayMode 验证结束衔接是否符合手感。
- P1：actor 根节点左右晃动可能影响碰撞体姿态、门触发、失败区和通关流程，需要关卡内完整跑通。
- P2：`shakeRotationAngle` 和 `shakeRotationDuration` 暴露到 Inspector 后，过大角度或过长时长可能影响移动控制反馈；需要设计确认推荐值。
- P2：本次未改 prefab，Unity Inspector 中新增字段会使用脚本默认值，是否需要美术/策划调参仍未验证。
- P2：Android/iOS 横屏触控下，开伞晃动期间暂停输入倾斜是否影响玩家预期，需要移动端 smoke。
- OBS：重复调用 `OpenParachute()` 时不会重复播放晃动；若后续设计要求每次调用都重新表现，需要重新调整触发边界。

### 已完成验证

- CodeGraph 证据：`TweenShakeRotation` 当前只有 `OpenParachute` 一个调用方，影响面集中在 `Level67Ctrl`。
- 静态 diff：新增字段和 tween 清理都在 `Level67Ctrl` 内，未新增 DB/save、资源加载、全局事件或平台 SDK 链路。
- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_67/Level67Ctrl.cs` 通过，仅有 LF/CRLF 提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 98 warnings / 0 errors。

### 未验证缺口

- Unity PlayMode：`OpenParachute()` 触发、2 秒左右衰减晃动、晃动结束回正、持续左右输入衔接。
- Level67 完整流程：开伞后碰撞、失败、重开、退出、通关。
- `Level_067.prefab` Inspector 中 `shakeRotationAngle` / `shakeRotationDuration` 的最终调参。
- Android/iOS 横屏真机或模拟器 smoke。

### 回滚路径

- 回退 `Level67Ctrl.cs` 中本次新增的晃动字段、`actorShakeRotationTween` 状态、`TweenShakeRotation()` 实现、`ApplyActorShakeRotation()`、`UpdateActorTilt()` 的暂停判断和清理逻辑。
- 如果只发现开伞初始晃动手感不合适，可先把 `shakeRotationDuration` 配为 `0` 跳过该表现，再继续保留其它开伞逻辑。

### 待澄清

- `shakeRotationAngle` 的策划推荐角度。
- `shakeRotationDuration` 是否固定 2 秒，或按不同关卡配置微调。

## 2026-07-09 通关后释放 actor rotation 控制风险评估

### 原始行为基线

- `BaseLevelCtrl.IsSuccess()` 返回 `IsActorEnterDoor`，`CheckFinished()` 在成功时进入 `FinishedLevel()` 和通关表现。
- `Level67Ctrl.UpdateActorTilt()` 在开伞后每帧读取 `actor.XInput`，通过 `maxTiltAngle`、`tiltSpeed` 和 `neutralReturnSpeed` 限制并写入 actor rotation。
- `TweenShakeRotation()` 播放期间也会通过 DOTween 持续写 actor rotation，并在非通关完成时回到 `actorNeutralRotation`。

### 风险项

- P1：若通关后仍写 actor rotation，可能覆盖通关动画、进门姿态或成功表现。
- P2：当前通过 `BaseLevelCtrl.IsSuccess()` 作为释放边界；如果 Level67 存在非门触发的成功路径但没有更新 `IsActorEnterDoor`，该路径仍需单独验证。
- P2：停止 tween 与后续清理都不强制回正，能避免覆盖通关表现，但也意味着触发瞬间会保留当前 actor rotation，是否符合最终视觉需要 PlayMode 确认。

### 已完成验证

- 静态证据：`Level67Ctrl` 只新增本地 `BaseLevelCtrl` 解析和 `IsSuccess()` 读取，不新增全局事件、DB/save、资源加载或平台 SDK 链路。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 98 warnings / 0 errors。
- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_67/Level67Ctrl.cs` 通过，仅有 LF/CRLF 提示。

### 未验证缺口

- Unity PlayMode：正常开伞后进门通关、晃动中进门通关、持续左右输入时进门通关。
- 通关动画是否完整接管 actor rotation。
- Android/iOS 横屏 smoke。

### 回滚路径

- 回退 `Level67Ctrl.cs` 中本次新增的通关判断和释放逻辑。
- 若只发现停止时姿态不符合预期，可保留通关判断，但在 PlayMode 证据明确后改为通关动画入口负责设置目标姿态。
