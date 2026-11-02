# ILevel058Ctrl 功能与风险记录

日期：2026-08-24

## 需求与原始基线

- 需求：调用 `EnableRightwardJump()` 后只进入等待跳跃阶段，不立即修改 Force Angle；继续监听
  `PlayerJumpEvent`，收到有效事件后才把 `AreaEffector2D` 的 Force Angle 设为 `0`、应用 Inspector 可配置的
  Force Magnitude 并开启区域力。Actor 在 API 触发后仍可移动，直到跳跃监听真正触发后才禁止主动移动；
  每次有效事件仍让 Actor 绕本地 Z 轴正向旋转 90 度。
- 调整前基线：最初实现会在首次有效 Jump 后通过 `FixedUpdate` 持续写入纯世界右向速度；上一轮中间实现错误地在
  `EnableRightwardJump()` 内立即设置角度和启用 Effector，没有保留“API 后等待 Jump”的时序。

## 影响文件与系统

- `Assets/Game/Scripts/Level/IWannerLevel/Levels/ILevel058Ctrl.cs`
- `Assets/Game/Scripts/Level/IWannerLevel/IWannerGame_GravityCtrl.cs`（新增局部调用所需的 Force Magnitude API）
- `Assets/Game/Scripts/Level/IWannerLevel/IWannerGame_GravityCtrl功能与风险记录.md`
- `Assets/Game/Prefabs/WannerLevel/ILevel_058.prefab`（以 `forceMagnitude` 替换已废弃速度字段）
- `DevelopMD/已有功能设计与使用记录.md`
- 复用现有 `PlayerJumpEvent`、Bear `EventSubscriber`、`Actor_IWanner` 与 `IWannerGame_GravityCtrl`。
- 当前静态 YAML 已存在 058 控制器、Actor、GravityCtrl 与 AreaEffector2D 引用，Trigger UnityEvent 继续绑定
  `EnableRightwardJump()`；不新增组件或 UnityEvent。
- 不涉及 DB/Save、配置、资源加载、UI、打点、广告、平台 SDK 或新全局事件。

## 修改后行为与事件流

本关 Trigger/其它局部逻辑 → `EnableRightwardJump()` → `SetGravityEnabled(false)`
→ 锁定当前 Ground 检测方向并把 Actor `gravityScale` 设为 0 → 开启 058 Jump 门禁
→ 后续 `PlayerJumpEvent` → Actor 本地 Z 轴 `+90°` 并登记一次待应用切换
→ 当帧或下一帧 `LateUpdate` → `Actor.SetMoveInput(0)` → `Actor.StopMoving()`
→ `SetForceMagnitude(forceMagnitude)` → `SetForceAngle(0)`
→ `SetGravityEnabled(true)` 并唤醒刚体。

- `ILevel058Ctrl` 启用时只负责订阅 Jump；公开 API 调用前，Jump 事件不会触发本控制器的旋转逻辑。
- API 可重复调用。调用当刻关闭 AreaEffector2D、锁定最后 Ground 检测方向并把 Actor 的 `gravityScale` 设为 0；
  Actor 仍可主动移动，不修改 Force Angle、Force Magnitude、刚体水平速度、`localScale` 或旋转。
- Jump 的同步回调结束后才调用 `SetMoveInput(0)` 与 `StopMoving()`，此时清零输入和刚体速度并关闭 `canMove`；
  后续不调用 `ResumeMoving()`，使 `ActorCtrl.OnUpdate()` 跳过 `HandleMovement()`，避免共享水平移动每帧覆盖
  AreaEffector2D 产生的速度。当前 Jump 的共享处理可先收到事件，但其速度会在 Jump 后移动锁定时清零。
- Jump 回调不直接写区域力，先登记待应用状态，再由 `LateUpdate` 写入参数。这样无论
  `ILevel058Ctrl` 与 `IWannerGame_GravityCtrl` 的事件订阅顺序如何，同一 Jump 的所有同步回调结束后最终 Force Angle 都是 0。
- 首次 Jump 前 GravityCtrl 已关闭，因此其 Jump 反转门禁会忽略该事件；后续 Jump 若触发 GravityCtrl 的 180 度反转，
  058 的延迟应用仍会在事件后把最终角度恢复为 0，但 GravityCtrl 对 `localScale.y` 与落地等待状态的既有修改不会回滚。
- 每次有效 Jump 使用 `Space.Self` 绕本地 Z 轴正向旋转 90 度；X/Y 轴旋转不主动修改。
- 本控制器不再包含 `rightwardSpeed`、持续移动状态或 `FixedUpdate` 赋速；速度、加速度与方向切换结果由
  `AreaEffector2D` 的 Force Magnitude、Drag、Force Target 和 `IWannerGame_GravityCtrl` 决定。
- `forceMagnitude` 暴露在 `ILevel058Ctrl` Inspector，默认及当前 prefab 配置均为 `150`；保留 Unity 原始语义，
  负值会使 Force Angle 0 的实际施力方向反转。
- Actor 死亡、刚体缺失、未模拟或为 Static 时忽略旋转；引用未配置时从当前关卡根节点及子层级解析。

## 验证

- CodeGraph 与源码已确认 `PlayerJumpEvent`、`EventSubscriber`、`Actor_IWanner.RB`、Ground 检测方向锁定和
  `IWannerGame_GravityCtrl.SetForceAngle/SetGravityEnabled` 的现有接口与调用影响；GravityCtrl 与 058 确实同时订阅 Jump。
- 静态 YAML 确认 `ILevel_058.prefab` 根节点已绑定 `ILevel058Ctrl`、Actor 与 GravityCtrl；GravityCtrl 指向
  `AreaEffector2D` stripped component，区域 prefab 默认 Force Angle 为 90，058 override 的 Force Magnitude 为 150，
  运行时控制器配置角度为 270。API 调用当刻不改角度；下一次有效 Jump 后才把运行时角度覆盖为 0。
- `Assembly-CSharp.csproj` 已包含 `ILevel058Ctrl.cs`；执行
  `dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false` 结果为
  `0 error / 106 warning`，warning 均来自工程现有诊断范围。
- 定向静态扫描确认运行时代码已无 `rightwardSpeed`、`linearVelocity`、`FixedUpdate`、编辑器生命周期、DB/Save、实例化或
  Unity `Debug.Log` 路径；prefab Trigger 仍绑定原 API，`forceMagnitude` 已序列化，定向空白检查通过。
- Unity PlayMode 未执行，本次编译与静态检查不得视为关卡手感验证通过。

## 剩余风险与未验证缺口

- 区域力在 Jump 同步回调结束后的 `LateUpdate` 才启用，最多存在一个渲染帧内的阶段延迟；首帧合力与视觉表现需
  Unity PlayMode 验证。
- `uncertain`：Force Magnitude 150、Drag 0 的水平区域力会形成持续加速而非原来的恒定速度 8；是否需要限速、阻力或
  Force Target 调整必须以实际关卡手感为准，当前不扩大修改范围。
- `uncertain`：尚未验证 Force Angle 0/180 下的左右墙 Ground 检测、共享正 Y 跳跃冲量、连续/空中 Jump、旋转后的
  Collider、相机、门、死亡、Reset、关卡退出、Android/iOS 横屏触控和 60 fps 表现。
- `uncertain`：后续 Jump 中 GravityCtrl 可能先执行 180 度反转及 `localScale.y` 翻转，再由 058 把角度恢复为 0；
  角度结果已由时序保证，但缩放翻转与落地等待状态是否符合 058 设计仍需 PlayMode 确认。
- `uncertain`：Actor 在首次有效 Jump 后保持 `canMove=false`，Reset/关卡重建会创建新实例恢复初始移动状态；若存在不重建 Actor 的
  局部回退路径，则需要额外确认是否应显式调用 `ResumeMoving()`。
- 若 GravityCtrl、AreaEffector2D 或 Trigger 绑定缺失，API 仍会开启本控制器的旋转门禁，但 Jump 后不会获得预期水平区域力。

## 回滚路径

- 从 `ILevel_058.prefab` 移除 `ILevel058Ctrl` 组件及其 Trigger UnityEvent 绑定，并删除本脚本及 meta，
  即可回到仅由 `IWannerGame_GravityCtrl` 处理 Jump 的关卡行为。
- 若只需临时停用阶段切换，可移除 Trigger 对 `EnableRightwardJump()` 的调用；GravityCtrl 将保持 prefab 的初始配置。
- 若只需撤销禁止移动，可移除 Jump 后应用阶段中的 `SetMoveInput(0)` 与 `StopMoving()`；不影响区域力切换。
- 若需恢复调整前恒定右移方案，可恢复 `rightwardSpeed`、首次 Jump 门禁与 `FixedUpdate` 赋速，移除
  `forceMagnitude` 和 Jump 后区域力应用，并恢复 prefab 的速度序列化值。
