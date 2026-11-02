# ILevel027Ctrl 功能与风险记录

## 需求与背景

- 2026-08-13：实现第 27 小关局部控制器，使 Inspector 配置的一组 GameObject 通过各自的
  `Rigidbody2D.linearVelocity` 沿世界 X 轴从 `startPoint` 向 `endPoint` 移动。
- 每个对象独立到达终点；到达后只把自身世界 X 坐标重置到起点，再继续下一轮移动。
- 2026-08-13 补充：提供无参公开 API `ReverseVelocity()`，供 Trigger 的 UnityEvent 反转当前循环方向。
- 2026-08-13 补充：监听既有 `PlayerRightMoveEvent` 与 `PlayerLeftMoveEvent`，对象保持静止，直到当前关卡首次收到玩家移动事件才开始循环。
- 2026-08-13 补充：`ReverseVelocity()` 触发后同时把有效移动对象的 X 缩放设置为可配置倍率，默认缩小为初始宽度的 `0.5`。
- 2026-08-13 补充：缩小不再瞬时完成；新增 `triggeredScaleDuration` 控制过渡时间，当前 prefab 默认 `0.5` 秒。
- 2026-08-13 修复：共享 `ActorCtrl.HandleMovement()` 在无输入时持续把 Actor 的 X 速度写成 0，仅依赖 Collider/物理材质摩擦
  不能保证 Actor 跟随平台；现改由 `Actor_IWanner` 检测脚下 Ground 的 `Rigidbody2D` 并合成其水平速度。
- 当前 `ILevel_027.prefab` 已绑定脚本、5 个移动对象、起止点和 5 个 Kinematic `Rigidbody2D`；Actor 继续使用既有
  `Actor_IWanner` prefab 实例，不在本关控制器重复绑定。

## 修改前行为基线

- `ILevel027Ctrl` 只有空的 `Start()` 和 `Update()`，不读取配置、不驱动刚体，也不执行循环重置。
- Actor 跟随修复前，控制器只给 `Ground (4)`～`Ground (8)` 的刚体写平台速度，不读取 Actor，也不判断 Actor 是否站在移动平台上。
- IWanner 现有输入、成功、失败、Reset、存档、资源加载和统计链不依赖本脚本。

## 影响文件与系统

- 运行时代码：`Assets/Game/Scripts/Level/IWannerLevel/Levels/ILevel027Ctrl.cs`。
- Prefab：`Assets/Game/Prefabs/WannerLevel/ILevel_027.prefab`，保持 5 个移动平台与范围点绑定，不再序列化本控制器的乘客字段。
- 本地记录：本文及 `DevelopMD/已有功能设计与使用记录.md`。
- 使用系统：Unity 2D 物理步进、`Rigidbody2D`、DOTween、Bear.Logger、Bear.EventSystem，以及既有
  `PlayerRightMoveEvent` / `PlayerLeftMoveEvent`。
- 关联修改：`Assets/Game/Scripts/Level/Actor/Actor_IWanner.cs` 增加移动 Ground 刚体速度合成；共享 `ActorCtrl` 保持不变。
- 未修改：共享 `IWannerLevelCtrl`、Actor prefab、UI、事件、配置、存档、资源地址、Analytics 与平台 SDK。

## 修改后行为与物理流

1. `OnEnable()` 只在 PlayMode 缓存 `movingObjects` 上的有效 `Rigidbody2D`；空引用、缺少刚体和 Static 刚体会记录一次
   Bear.Logger 警告并被忽略，不会在 `FixedUpdate()` 中反复执行 `GetComponent`。初始化同时把所有有效刚体的 X 速度清零。
2. 配置有效时通过 `EventSubscriber` 监听既有 `PlayerRightMoveEvent` 与 `PlayerLeftMoveEvent`。任一事件首次到达时锁存
   `_hasMovementStarted = true` 并立即写入配置速度；HUD 按住期间的后续逐帧移动事件不会重复初始化。
3. `PlayerMoveCancelEvent` 不参与本控制器，因此玩家松开方向键后平台继续循环。控制器禁用时统一解除事件订阅并重置启动门禁。
4. 初始移动方向由 `endPoint.position.x - startPoint.position.x` 的符号决定，Inspector 的 `horizontalVelocity` 作为非负速度大小；
   因此起点在终点左侧或右侧都可工作。每次重新启用控制器会恢复初始方向。
5. 启动后每次物理步进只覆盖刚体 X 速度，保留当前 Y 速度。速度配置为 0 时会主动把 X 速度保持为 0。
6. 正向时，单个刚体到达或越过 `endPoint` 后将 X 重置为 `startPoint`；反向时到达或越过 `startPoint` 后将 X 重置为
   `endPoint`。两种方向都保留该刚体的 Y 坐标和 Y 速度，列表内其它对象不受影响。
7. 控制器禁用时会清零已缓存刚体的 X 速度并保留 Y 速度；运行时丢失范围引用或范围退化为同一 X 时同样停止并关闭后续推进。
8. 没有新增全局事件类型、协程、资源加载、存档或逐帧托管分配；平台移动的单次物理步进复杂度为 O(n)，n 为有效移动刚体数量。
9. `ReverseVelocity()` 是公开无参 UnityEvent API：每调用一次切换方向；平台已启动时立即按原配置速度大小写入所有有效刚体，
   尚未启动时只记录未来方向，不会绕过移动事件门禁。连续调用两次恢复初始方向。编辑态、未完成运行时初始化或范围无效时只记录
   Bear.Logger 警告，不修改刚体。
10. `triggeredScaleXMultiplier` 暴露为 `0～1` 的 Inspector 参数，默认 `0.5`；`triggeredScaleDuration` 控制到达目标所需时间，
    当前 prefab 为 `0.5` 秒。Trigger API 让所有有效对象同时用 `Ease.InOutSine`、物理步进 Tween 过渡到“首次缓存的初始 X 缩放 × 参数”；
    Y/Z 不变。时长为 0 时立即完成；重复调用会终止旧 Tween，并从当前宽度重新过渡到相同目标，不累计缩小。
11. 027 不再自行判断乘客。`Actor_IWanner.OnUpdate()` 在共享移动计算后，用预分配接触数组查询 Ground 层接触；支撑法线朝上且
    Ground Collider 的 `attachedRigidbody` 为非 Static 刚体时，把 Actor X 速度写成“支撑刚体速度 + Actor 输入相对速度”，Y 速度不变。
12. 静态 Ground 没有可移动刚体时继续使用共享移动结果；Actor 离地、上升、死亡、击退或坐下时不合成支撑速度。该能力会作用于所有
    使用 `Actor_IWanner` 的关卡，不依赖物理材质传递速度。

## 风险评估

- **Prefab 绑定**：YAML 已确认脚本、5 个移动对象、`startPoint/endPoint` 和 5 个 Kinematic `Rigidbody2D` 均已绑定；修改发生在既有
  PlayMode 实例创建后时，必须重新进入 PlayMode 或重建关卡实例，旧 Clone 不会自动获得新组件。
- **刚体类型**：对象必须使用 Kinematic 或 Dynamic `Rigidbody2D`。Static 刚体会被安全忽略；Dynamic 刚体仍受重力、碰撞和约束影响，本脚本只持续校正 X 速度。
- **循环瞬移**：到达终点后刚体在一个物理步进内回到起点。若玩家当时站在平台上，接触体在瞬移帧的表现取决于最终 prefab 的平台刚体、碰撞器和物理材质，需 PlayMode 实测。
- **乘客判定**：当前以 Physics2D 已建立的实际接触和朝上支撑法线为准，不使用距离容差；Actor 初始脚底与最右平台顶部约有
  `0.065` 静态间隙，因此必须等待重力建立接触后才开始合成。跨平台缝隙、运行时缩放边缘及高速接触仍需 PlayMode 实测。
- **高速越界**：终点判断允许越过后再回绕，不会遗漏循环；但速度过高可能造成碰撞穿透或视觉跳变，应由关卡配置与 PlayMode 手感验证约束。
- **范围语义**：只使用起止 Transform 的世界 X 坐标，Y 坐标不会限制或重置对象。若关卡实际需要二维轨迹，当前实现不覆盖该需求。
- **Trigger 重复触发**：API 没有一次性门禁，每个有效 Trigger 回调都会切换一次方向；若同一 Trigger 配置了重复 Listener、多个
  Collider 同帧进入或触发器允许连续进入，最终方向取决于实际调用次数，需在 prefab 绑定时检查。
- **启动事件范围**：监听的是共享玩家左右移动事件，但本组件只随 `ILevel_027` 实例存在并在禁用时解除订阅。首次有效左右输入会永久
  开启本实例的平台循环，松手不会停止；若产品预期“按住才移动”，则当前一次性启动语义需要重新确认。
- **运行时缩放碰撞**：缩放与 BoxCollider2D 位于同一对象层级，X 缩放会同步改变平台可碰撞宽度。触发瞬间若 Actor 正站在边缘，可能发生
  接触面逐步收缩、悬空或下落，需要 PlayMode 验证；倍率 0 会把平台 X 缩放逐步压到 0，应谨慎配置。
- **Tween 生命周期**：每个有效平台最多持有一个缩放 Tween；重复 Trigger 先 Kill 旧 Tween再从当前比例重启，控制器禁用时统一 Kill。
  若同一关卡实例在缩放中途仅禁用后又重新启用，平台会保留中断时比例，直到下次 Trigger；正常 Reset/重建会重新读取 prefab 初始比例。
- **暂停与移动端**：逻辑依赖 Unity `FixedUpdate()`，`timeScale = 0` 时不会推进，恢复后继续；没有 Android/iOS 平台分支，但横屏真机性能与触控联动尚未验证。

## 验证记录

- `dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false -v:minimal`：通过，
  `0 error / 101 warning`；warning 属于工程现有范围，目标脚本没有新增编译诊断，且已由 `Assembly-CSharp.csproj` 收录。
- 静态检查确认移动使用 `FixedUpdate()` 与 `Rigidbody2D.linearVelocity`，只写 X 分量；终点判断同时覆盖正向和反向范围。
- 静态检查确认 `ReverseVelocity()` 为公开无参方法，可由 UnityEvent 选择；启动后调用会立即反转速度，启动前调用只更新未来方向，
  后续物理循环使用对应的反向目标与重置点。
- 静态检查确认只订阅 `PlayerRightMoveEvent` 与 `PlayerLeftMoveEvent`，不订阅 `PlayerMoveCancelEvent`；首次输入锁存后
  `FixedUpdate()` 才开始推进，`OnDisable()` 会解除订阅、停止 X 速度并重置门禁。
- 静态检查确认缩放使用首次缓存的初始 X 值计算，重复 Trigger 不累计；`DOScaleX` 使用 `UpdateType.Fixed`、`Ease.InOutSine`，
  每个平台的旧 Tween 在重触发和 `OnDisable()` 时被清理，Y/Z 不变。
- 静态确认 `ILevel_027.prefab` 已序列化 `triggeredScaleDuration: 0.5`。
- 当前 prefab 静态确认 5 个移动对象均具有 Kinematic `Rigidbody2D`，`Simulated=1`、`Constraints=0`。
- 静态检查确认 Actor 使用 `Actor_IWanner`；5 个平台均位于 Ground Layer 并带 Kinematic `Rigidbody2D`。Actor 初始位置与最右平台
  水平范围重叠，脚底和平台顶部静态间隙约 `0.065`，需由运行时重力建立接触。
- 静态检查确认 `Actor_IWanner` 使用预分配 `ContactPoint2D[8]` 和 Ground Layer ContactFilter2D，只对朝上支撑面的非 Static
  `attachedRigidbody` 合成 X 速度并保留 Y；共享 `ActorCtrl` 与 Actor prefab 未修改。
- 静态检查确认没有 `Update()`、协程、全局事件、DB/Save、资源加载、`OnValidate`、`Reset`、`ExecuteAlways` 或编辑态实例创建。
- `uncertain`：尚未在重新创建的 Unity PlayMode 实例验证多对象错位循环、首次左/右输入启动、松手持续、单次与连续反转、
  Trigger 后 0.5 秒 X 缩放过程、重复触发中断、Actor 站台/跨缝/跳跃/触墙、平台回绕瞬移、暂停恢复、Reset/重建与成功清理。
- `uncertain`：尚未静态确认实际 Trigger 的 UnityEvent 已绑定 `ReverseVelocity()`，也尚未完成 Android/iOS 横屏真机验证。

## 回滚路径

- 将 `ILevel027Ctrl.cs` 恢复为空控制器即可撤回运行时能力；当前 prefab、配置、存档、事件、资源和共享控制器无需回滚。
- 若只回滚 Trigger 反转能力，删除 `ReverseVelocity()`、`_movementDirectionMultiplier` 与反向目标/重置点分支，恢复固定的
  `startPoint -> endPoint` 循环即可。
- 若只回滚移动事件启动门禁，删除 `EventSubscriber`、两个左右移动回调与 `_hasMovementStarted` 判断，并在 `OnEnable()` 配置有效后
  直接写入初始速度即可。
- 若只回滚缩放时间过程，把 `triggeredScaleDuration` 设为 0 即恢复瞬时缩放；完整删除 Trigger 缩放时，再移除倍率/时长字段、初始
  X 缩放缓存、Tween 字典及 `AnimateTriggeredScaleX()` 调用，速度反转和循环边界不受影响。
- 若只回滚 Actor 跟随修复，删除 `Actor_IWanner.OnUpdate()` 与 `Moving Ground` 区域；027 平台循环、缩放、反转和 prefab 绑定可保留。
- 若后续已在 prefab 完成绑定，回滚脚本前应先移除对应组件或恢复与旧脚本字段兼容的版本，避免 Missing Script 或序列化字段残留。
