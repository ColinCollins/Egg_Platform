# IWannerGame_AccelerateMoveTriggerListener 功能与风险记录

日期：2026-08-11（初版）；2026-08-18（Kinematic Rigidbody2D 物理移动补丁、ILevel_014 速度驱动隔离补丁）

## 需求 / 背景

- 用户要求新增 `IWannerGame_AccelerateMoveTriggerListener`：方向参数与既有
  `IWannerGame_MoveTriggerListener` 一致，同时可配置初始速度和加速度，并在移动到指定位置后停下。
- 本次采用既有 `target + Vector2 offset` 世界坐标契约；速度和加速度为沿 offset 方向的非负标量，避免速度向量与
  终点方向互相冲突。

## 影响文件、资源与系统

- 新增脚本：`Assets/Game/Scripts/Level/IWannerLevel/IWannerGame_AccelerateMoveTriggerListener.cs`。
- 新增 Unity 元数据：`Assets/Game/Scripts/Level/IWannerLevel/IWannerGame_AccelerateMoveTriggerListener.cs.meta`。
- 继承既有 `Game.ItemEvent.BaseItemEventHandle`，可由 `TriggerExecutor` / `BaseItemExecutor` 调用。
- 本次未修改或绑定任何 prefab、场景、配置、存档、资源地址、UI、全局事件、Analytics 或平台 SDK。

### 2026-08-18 补丁范围

- `ILevel_013` 的 `Ground_Move` 由本 Listener 以 `initialSpeed = 20`、`acceleration = 0` 执行横向移动；旧实现始终在
  `Update()` 直接写 `target.position`，即使目标后来新增 `Rigidbody2D`，Listener 也不会调用该刚体的物理移动 API。
- 本次只调整 Listener：目标存在已启用的 `Kinematic Rigidbody2D` 时改走 `FixedUpdate() + MovePosition()`；没有刚体、
  刚体未启用或 Body Type 不是 Kinematic 时，继续走原 `Update() + Transform` 路径。
- 未修改 `ILevel_013.prefab`、Actor、碰撞层、Physics 2D 全局设置、速度参数或其它 Move Listener。
- 当时静态核对另一个引用该 Listener 的 `ILevel_015`：其 `Ground (1)` 目标没有 `Rigidbody2D`，因此仍走旧路径。

### 2026-08-18 ILevel_014 速度驱动隔离补丁范围

- 运行期 Bear Logger 证据确认：`Actor_IWanner` 与 `ILevel_014/Ground_Move` 有两个有效接触点，支撑法线为 `(0, 1)`，
  支撑刚体为 `Simulated = true` 的 Kinematic；但 Actor 跟随计算读取到的平台 `linearVelocity = (0, 0)`，随后角色失去
  Ground 接触并下落。
- 资源 GUID 全量扫描确认该 Listener 当前只被 `ILevel_013.prefab`、`ILevel_014.prefab`、`ILevel_015.prefab` 三处引用。
- 为避免共享修改改变其它关卡，新增实例级 `KinematicMovementMode`：默认 `MovePosition` 保持既有行为；只有
  `ILevel_014` 的 AccelerateMove 实例序列化为 `LinearVelocity`。
- `ILevel_013` 的目标是 Kinematic `Ground_Move`，未写入新字段，继续使用原 `MovePosition` 路径；`ILevel_015` 的目标
  `Ground (1)` 没有 `Rigidbody2D`，继续使用原 `Update() + Transform` 路径。
- 本次没有修改 `ILevel_013.prefab`、`ILevel_015.prefab`、`Actor_IWanner`、全局 Physics 2D、存档、配置、事件、UI、
  Analytics 或平台 SDK。

## 既有行为基线

- `IWannerGame_MoveTriggerListener` 使用 `target` 和 `Vector2 offset`，按固定时长通过 DOTween 线性移动；终点是
  触发瞬间的世界坐标加 offset，多次触发会累积位移。
- 项目已有 `DirectMove` 可按 `Vector2` 初速度和加速度持续直接移动 Transform，但它不接受触发执行契约，也没有
  固定终点和到点停止边界。
- 新脚本加入前，没有同时满足“Trigger Listener、加速移动、固定相对终点、到点停止”的局部组件。

## 新行为

1. `Execute()` 以目标当前世界坐标加 `offset` 计算本次终点，只改变 X/Y，Z 保持触发时坐标。
2. 当前速度每次触发时重置为 `initialSpeed`，之后按 `acceleration` 与当前路径的 delta time 增长；两者都按非负值处理。
3. 目标是已启用的 Kinematic `Rigidbody2D` 时，在 `FixedUpdate()` 使用 `Time.fixedDeltaTime` 与前后速度平均值推进；
   实例默认使用 `Rigidbody2D.MovePosition()`，配置为 `LinearVelocity` 时显式写入本物理步的刚体速度。
4. 其它目标继续在 `Update()` 使用 `Time.deltaTime`、前后速度平均值与 `Vector3.MoveTowards` 直接写 Transform，保持旧关卡行为。
5. 所有路径都会将单步位移钳制在终点；`LinearVelocity` 的最后一步按剩余距离换算速度，并在下一次 FixedUpdate
   精确归位、清零速度后设置 `IsRunning = false`、`IsDone = true`，避免完成标志早于实际物理移动。
6. `offset` 为零时立即完成；初速度和加速度同时为零时也立即完成且不移动，避免顺序执行器永久等待。
7. 重复调用会先清零本 Listener 正在驱动的刚体速度，再从当时位置重新计算终点；`ResetState()` 会停止本次推进并重置
   执行标志，但不会把 Transform 还原。
8. 物理移动期间若刚体被销毁、停止模拟或改成非 Kinematic，会停止物理路径并在后续 Update 回退原 Transform 路径；
   组件被禁用或目标被销毁时仍会中止并完成执行状态。
9. `OnDrawGizmos()` 以青色方向线和线框球预览当前配置的相对终点，仅使用本地序列化数据。

## 数据、资源、事件与平台流

- Kinematic 物理路径按实例配置调用 `Rigidbody2D.MovePosition()` 或写入 `Rigidbody2D.linearVelocity`；兼容路径只修改
  目标 Transform 世界坐标。所有路径都不访问 DB、Save、RemoteConfig、资源系统或 UI。
- 不新增 Bear 全局事件。`IsRunning` / `IsDone` 继续使用 `BaseItemEventHandle` 的局部执行契约。
- 兼容路径使用缩放时间 `Time.deltaTime`，物理路径使用 `Time.fixedDeltaTime`；`timeScale = 0` 时保持暂停，恢复后继续。
- 算法每个运行中实例每个更新步只做常量次浮点运算和一次位置写入，不创建集合或运行时对象；Android/iOS 横屏没有专用分支。

## 验证

- CodeGraph 核对了 `IWannerGame_MoveTriggerListener`、`BaseItemEventHandle` 和 `DirectMove` 的现有源码与依赖边界。
- `git diff --check` 对新增脚本和 `.meta` 通过。
- 静态扫描未发现 `OnValidate`、编辑态执行属性、DB/Save/RemoteConfig、实例化或全局事件调用。
- 定向执行：
  `dotnet build .\Assembly-CSharp.csproj --no-restore --nologo -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`；
  初版结果为 `0 error / 101 warning`；2026-08-18 补丁结果为 `0 error / 109 warning`，warning 为工程现有输出范围。
- 静态核对 `ILevel_013` 的 AccelerateMove 参数为 `offset = (8.5, 0)`、`initialSpeed = 20`、`acceleration = 0`；
  磁盘上的 `Ground_Move` prefab YAML 当前未包含 `Rigidbody2D` 组件。本次按批准范围不写 prefab。
- 静态核对 `ILevel_015` AccelerateMove 目标组件为 Transform、SpriteRenderer、BoxCollider2D 和禁用的本地 MonoBehaviour，
  不会进入新增 Kinematic 路径。
- ILevel_014 隔离补丁再次定向构建 `Assembly-CSharp.csproj`，结果为 `0 error / 109 warning`。
- GUID 资源扫描只发现 013、014、015 三个引用；静态断言只有 `ILevel_014.prefab` 包含
  `kinematicMovementMode: 1`，013 与 015 未写该字段，使用枚举默认值 `MovePosition`。
- `git diff --check` 对 Listener 与 `ILevel_014.prefab` 通过；未修改 013、015 的资源内容。

## 剩余风险 / 未验证缺口

- `uncertain`：尚未在 Unity PlayMode 把组件挂入 TriggerExecutor，验证 Sequence / Parallel、重复触发、Reset、禁用和
  目标销毁时的实际执行表现。
- `uncertain`：尚未目测极短 offset、高初速度/高加速度、零参数和低帧率下的到点停靠表现。
- `ILevel_013/Ground_Move` 当前已保存同物体、`Simulated = true`、Body Type 为 `Kinematic` 的 `Rigidbody2D`，并继续使用
  默认 `MovePosition`；其高速推动 Actor 的既有未验收风险不由本次 ILevel_014 隔离补丁扩大或处理。
- `uncertain`：尚未在 `ILevel_013` PlayMode 实测速度 20 时 Ground 推动 Actor、碰撞回调、终点停靠及多次 Replay；
  `MovePosition` 能否满足最终手感仍需以实际物理接触结果验收。
- `uncertain`：尚未在修改后的 `ILevel_014` PlayMode 复测角色全程跟随、终点精确停靠、Sequence 后续项、Reset、Replay、
  禁用与重复触发；需要以 Inspector/Console 中平台移动期 `linearVelocityX = 3` 和角色未离地为验收依据。
- `uncertain`：尚未在 Android/iOS 真机验证暂停恢复、帧率波动和最终手感。

## 回滚路径

- 若尚未绑定资源，删除新增 `.cs` 与 `.meta` 即可完整回滚。
- 若后续已挂接 prefab 或场景，先从对应 TriggerExecutor 的 items 中移除组件引用，再删除脚本；本次没有存档、配置或
  资源迁移需要回滚。
- 仅回滚 2026-08-18 物理补丁时，删除 `_targetRigidbody` / `_useRigidbodyMovement` 分支和 `FixedUpdate()`，恢复
  `Update()` 直接调用原 Transform 推进即可；无需迁移序列化数据。
- 仅回滚 ILevel_014 速度驱动隔离补丁时，从 `ILevel_014.prefab` 删除 `kinematicMovementMode: 1`，该实例即恢复默认
  `MovePosition`；随后可删除 Listener 的 `LinearVelocity` 分支和对应运行时状态，不需要修改 013、015、存档或配置。
