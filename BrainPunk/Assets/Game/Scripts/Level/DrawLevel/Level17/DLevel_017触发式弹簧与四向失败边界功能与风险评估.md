# DLevel_017 触发式弹簧与四向失败边界功能与风险评估

## 1. 需求与背景

- 玩家无法主动跳跃；走到弹簧范围后应直接触发弹射，不再要求从上方踩中实体按钮。
- Spring 感应区不能阻止玩家落笔或截断正在绘制的线；触发区域内也必须能够连续画线。
- 弹射期间不锁定角色移动，第一笔后的自动行走始终保持向右。
- 弹射不能只有向上分量；需要对玩家刚体施加真实的右上冲量，且右向冲量不能被自动行走在下一帧立即覆盖。
- 本关运行时画线需要很光滑：接触时使用零摩擦、零弹性的二维物理材质。
- 弹簧结构复制自常规关卡 Level032，但复制到 DrawGame 后存在组件或事件丢失的可能。
- 关卡使用固定镜头，不跟随被弹飞的角色；视野上下左右都要复用既有 `FallArea.prefab`
  形成失败边界，顶部也不能留下逃逸缺口。

## 2. 原行为基线

- 最初从 Level032 复制时，按钮带有非 Trigger `BoxCollider2D`、动态 `Rigidbody2D` 与
  `SpringJoint2D`，角色从侧面会被实体按钮挡住，无法走到可触发位置。
- 用户随后删除了按钮的上述三个物理组件；角色能够穿过，但旧版 `DLevel17Ctrl` 仍把
  `springJoint == null` 作为直接退出条件，所以根 Trigger 即使命中也不会发射。
- Actor 在本关还会启用一个 `Untagged` 的前置子碰撞体；`OnTrigger2DHandle(targetTag=Player)`
  可能先过滤掉这个最早进入区域的碰撞体，造成触发时机不稳定。
- Spring 根 Trigger 原位于 Layer 9。`DrawLineFeature` 的阻挡查询会包含全部非 Draw Layer，且
  `Physics2D.queriesHitTriggers=true` 时 Trigger 同样参与 `OverlapCircle/CircleCast`，因此该感应区会
  拒绝落笔或截断连续画线。
- 不能直接把 `Level32Ctrl` 挂到本关：它依赖 `BaseLevelCtrl`、常规关 UI `levelBlock`、
  纵向镜头流程和另一个停止弹跳触发器，并会修改全局 `Time.fixedDeltaTime`。
- 当前 GameCamera 位于 `(0,0,-10)`，正交尺寸 `5.9`；只有分辨率适配和 Scene Gizmo，
  没有 Follow/Cinemachine/TileMapInfinite 组件。
- 修改前本关没有任何 FallArea 实例。
- 共享 `DrawGame_DrawLine` 画线模板的 Rigidbody2D 未绑定物理材质，本关原先也没有局部材质 override。

## 3. 影响范围

- `Assets/Game/Prefabs/DrawLevel/DLevel_017.prefab`
- `Assets/Game/Scripts/Level/DrawLevel/Level17/DLevel17Ctrl.cs`
- `Assets/Game/Scripts/Level/Actor/ActorCtrl.cs`
- `Assets/Game/ArtSrc/Physics/DrawStrokeFree.physicsMaterial2D`（仅复用，资源本体未修改）
- Level17 本地功能、风险与脚本结构记录
- DrawGame 中央结构演进记录
- `DevelopMD/已有功能设计与使用记录.md`

本轮不修改 Level032、`Level32Ctrl`、`DrawLevelCtrl`、`DrawLineFeature`、`FallArea.prefab`、
GameCamera prefab、共享 `DrawGame_DrawLine.prefab`、`DrawStrokeFree` 资源本体、Physics2D Layer 矩阵、
DB、存档、Luban、UI、analytics、广告、SDK 或
平台分支。共享 `ActorCtrl` 只新增默认关闭的外部水平动量保留开关，未开启时既有
移动语义不变。`DLevel17Ctrl` 继续只读取 `DrawLevelCtrl.IsOutcomeLocked` 做发射前门禁。
`DLevel_017.prefab` 是用户当前未跟踪的新资源；已有场景、美术、成功门、DLevel001Ctrl 自动向右
移动与惰性的旧 TrapEventHandlle 均保留。

## 4. 修改后行为

1. DLevel_017 根节点新增局部 `DLevel17Ctrl`，不复用带常规关依赖的 `Level32Ctrl`。
2. `Spring` 根节点保留唯一的非阻挡 Trigger，当前 `offset=(0,-0.5)`、`size=(2.2,1.4)`，并从
   Layer 9 改到 Layer 14 `Draw`。`DrawLineFeature` 的 `_blockingLayerMask` 明确排除 Draw Layer，
   所以即使 `Physics2D.queriesHitTriggers=true`，该 Trigger 也不再拒绝落笔或截断连续画线。
   项目 Physics2D 碰撞矩阵当前全开，因此 Actor 进入 Trigger 的物理回调仍会发生；
   `DLevel17Ctrl` 继续严格过滤为本关 Actor。
3. `Sp_button_1_02` 不恢复 Collider2D，因此角色仍可直接穿过；仅恢复无接触用途的动态
   Rigidbody2D 与 SpringJoint2D，Joint 参数与 Level032 一致：`frequency=4`、
   `dampingRatio=0.1`、`distance=0.005`，连接既有 Static 刚体。
4. `OnTrigger2DHandle.targetTag` 在本关清空，让 Player 根碰撞体和 Untagged 子碰撞体都能转发；
   `DLevel17Ctrl` 再通过 `GetComponentInParent<ActorCtrl>() == actor` 做严格身份过滤，其它物体
   不会被发射。
5. 有效触发后先播放 `boardTrigger`，按钮按 Level032 节奏在 `0.1s` 内向下压缩 `0.2`；
   从进入开始 `0.2s` 时启用视觉 Joint、播放 `spring` 并发射玩家。`Level5TrackLine` 每帧
   跟随 Bottom 与按钮，Joint 的真实拉伸与震荡会同步改变弹簧线长度。
6. 玩家 Actor 的实际 `Rigidbody2D.mass=3`。Prefab 暴露 `rightwardImpulse=12`、
   `upwardImpulse=72` 与当前用户调整的 `launchStrength=3`；发射时调用
   `Rigidbody2D.AddForce(new Vector2(rightwardImpulse, upwardImpulse) * launchStrength,
   ForceMode2D.Impulse)` 并 `WakeUp()`。当前实际冲量为 `(36,216)`，按 `Δv=impulse/mass`
   计算为 `Δv=(12,72)`。本关 Actor 基础向右速度为 `3`，因此无碰撞时弹射后的
   短时 X 速度理论约为 `15`。飞行重力为 `4.5`。
7. 发射链不调用 `SetMoveInput(0)`、`StopMoving()` 或 `ResumeMoving()`，也没有移动锁、落地解锁和
   `airborneControlLockDuration`。DLevel001Ctrl 始终每帧输入向右。发射前 DLevel17Ctrl 临时调用
   `ActorCtrl.SetPreserveExternalHorizontalMomentum(true)`；`ActorCtrl.HandleMovement()` 仅在外部 X 速度
   与当前输入同向且快于基础移动时保留它，同时始终保留 Y 速度。该可选开关
   默认关闭，DLevel017 在 `0.35s`、Reset/Replay、`OnDisable` 或机关协程结束时关闭，
   不会锁住角色。Actor 重力独立在 `1.1s` 后恢复缓存值。`IsOutcomeLocked` 仅做实际发射前门禁。
8. 协程和冷却会过滤同一角色多碰撞体造成的重复进入。Reset/Replay 或对象禁用时停止协程，
   关闭外部水平动量保留，禁用视觉 Joint，清空按钮刚体速度，恢复按钮位置/初始零重力、
   Actor 原重力和触发冷却；
   不改 Actor 移动状态。暂停期间压缩、发射等待和发射后计时均冻结，恢复后继续。
9. 本关对嵌套 `DrawGame_DrawLine` 模板根 Rigidbody2D 局部绑定现有
   `DrawStrokeFree.physicsMaterial2D`：`friction=0`、`bounciness=0`、摩擦合并为 Minimum。
   `DrawLineFeature` 克隆整个模板，而运行时生成的 Box/Capsule Collider 共用该根刚体，
   因此每笔已完成画线都继承零摩擦材质；其它关卡不受此 Prefab override 影响。
10. 固定镜头保持不变；四个共享 FallArea 都位于最大横屏视野外：

| 方向 | LocalPosition | LocalScale | Collider Size |
| --- | --- | --- | --- |
| 左 | `(-18.23,0,0)` | `(1,1.5,1)` | `(10,10)` |
| 右 | `(18.1,0,0)` | `(1,1.5,1)` | `(10,10)` |
| 下 | `(-0.66,-10.39,0)` | `(2.99625,0.7875,1)` | `(10,10)` |
| 上 | `(-0.66,10.39,0)` | `(2.99625,0.7875,1)` | `(10,10)` |

四块内沿约为 `x=-13.23/+13.1`、`y=±6.45`，覆盖正交尺寸 5.9 在
2340:1080 下约 `x=±12.78` 的最大视野，并在四角相互重叠。

## 5. 关键流程

```text
玩家第一笔后自动向右移动
  -> 进入 Spring 根 Trigger
  -> Trigger 位于 Draw Layer：允许在区域内落笔并连续画线
  -> OnTrigger2DHandle：转发根/子 Collider
  -> DLevel17Ctrl.TriggerSpring：绑定 Actor 身份 + 冷却 + 关卡结算状态门禁
  -> 压缩按钮 0.2 / 0.1s
  -> 0.2s 时启用 Level032 参数的视觉 SpringJoint2D
  -> Level5TrackLine 跟随按钮产生真实拉伸/回弹
  -> 临时开启同向外部水平动量保留
  -> Actor Rigidbody2D.AddForce(new Vector2(12,72) * launchStrength, Impulse) + WakeUp
  -> 当前 launchStrength=3、mass=3，因此 Δv=(12,72)
  -> 基础右移速度 3，无碰撞时短时 X 理论约 15；0.35s 后恢复普通水平移动规则
  -> 固定镜头内向上弹射并持续向右移动

Actor 越过固定视野任一方向
  -> 共享 FailAreaTriggerHandle
  -> OnTriggerFailAreaEvent
  -> DrawLevelCtrl 既有 Failed / Reset 重建链
```

## 6. Inspector 参数

| 字段 | 当前值 | 作用 |
| --- | ---: | --- |
| `upwardImpulse` | `72` | 右上冲量的基础 Y 分量 |
| `rightwardImpulse` | `12` | 右上冲量的基础 X 分量 |
| `rightwardMomentumRetentionDuration` | `0.35` | 保留与右向输入同向且更快的外部 X 速度时长；不锁定输入 |
| `launchStrength` | `3` | 用户当前 Prefab 倍率；同时缩放 X/Y 冲量，实际冲量 `(36,216)` |
| `flightGravityScale` | `4.5` | 飞行阶段重力；结束和 Reset 恢复缓存值 |
| `gravityRestoreDelay` | `1.1` | 恢复角色原重力的延时 |
| `compressionDistance` | `0.2` | 按钮向下预压距离 |
| `compressionDuration` | `0.1` | 预压动画时长 |
| `springReleaseGravityScale` | `1` | Joint 释放后视觉按钮刚体重力，等同 Level032 |
| `launchDelay` | `0.2` | 从触发到发射的总延时，等同 Level032 节奏 |
| `retriggerCooldown` | `1.3` | 重复进入过滤时间 |

## 7. 风险项

| 风险 | 等级 | 控制与现状 |
| --- | --- | --- |
| 直接挂 Level32Ctrl 带入主线依赖与全局副作用 | 高 | 使用 DLevel17Ctrl，只复用弹簧视觉物理参数、美术与声音 |
| 恢复按钮实体 Collider 再次挡住角色 | 高 | 按钮只有 Transform/Sprite/Rigidbody/Joint，没有 Collider；唯一 Collider 是根 Trigger |
| 无 Collider 的动态按钮在触发前受重力掉落 | 高 | Prefab 初始 gravityScale=0；仅在压缩完成、Joint 启用时切到参考值 1 |
| 缺少 Level032 按钮接触耗能后振荡过久 | 中 | Joint 参数保持参考值；当前为单次机关且 Reset 完整，持续时间仍需 PlayMode 看手感 |
| 触发区过高或过窄，无法跳跃的角色侧面漏触发 | 中 | 改为 `2.2×1.4`、offsetY `-0.5`，仍需 PlayMode 核对实际脚底轨迹 |
| Spring Trigger 阻止落笔或截断连续画线 | 高 | Spring 根从 Layer 9 改为 Layer 14 Draw；画线阻挡 mask 排除 Draw，即使 queriesHitTriggers=true 也不命中该 Trigger |
| 改到 Draw Layer 后 Actor 不再触发机关 | 高 | 项目 Physics2D 矩阵当前全开，Actor/Draw 仍产生 Trigger 回调；DLevel17Ctrl 再严格校验本关 Actor |
| Untagged 子 Collider 先进入而被 Tag 过滤 | 高 | 本关 targetTag 留空，控制器按父级 ActorCtrl 精确过滤 |
| 多 Collider 同帧重复发射 | 高 | 控制器校验 Actor 身份，并用协程状态和冷却去重 |
| 当前右上冲量越过门或触顶 FallArea | 高 | 当前 `(12,72)×launchStrength(3)` 在 mass=3 时为 `Δv=(12,72)`；必须 PlayMode 验收高度和门落点 |
| Actor 质量变化导致弹道变化 | 高 | `Δv=impulse/mass`；若 mass 不再为 3，必须同时重算 `rightwardImpulse` 和 `upwardImpulse` |
| 自动向右在下一帧覆盖 X 冲量 | 高 | DLevel17 开启 0.35s 的可选动量保留；HandleMovement 仅保留同向且快于基础速度的 X，开关默认关闭 |
| 动量保留泄漏到 Reset 或其它流程 | 高 | 0.35s 到期、协程结束、Reset/Replay 和 OnDisable 均显式关闭；PlayMode 仍需验证失败中断 |
| 画线仍有明显摩擦或发生弹跳 | 中 | 本关模板根刚体局部使用 DrawStrokeFree（摩擦 0、弹性 0、Minimum）；仍需 PlayMode 验证 Actor/画线和画线/地面接触 |
| 压缩期间已进入成功/失败链仍被发射 | 高 | `DrawLevelCtrl.IsOutcomeLocked` 在触发入口和实际 AddForce 前门禁；不再用于任何移动恢复逻辑 |
| 暂停菜单期间弹簧仍继续计时并发射 | 高 | 压缩、发射等待和发射后计时均在 `IsPaused` 时冻结；真正发射前再次校验 Actor 与结算状态 |
| Reset 后 Joint、按钮速度或重力残留 | 高 | 统一停止协程、禁用 Joint、清零视觉刚体并恢复按钮/Actor 初态 |
| FallArea 使用共享默认 `100×10` 导致场内误触 | 高 | 四个实例都显式覆盖 Collider 为 `10×10` |
| 顶部/四角漏出固定视野 | 中 | 顶部镜像底部并与左右边界重叠；静态范围闭合，仍需 PlayMode 高速穿越验证 |
| 不同物理帧率产生轨迹差异 | 中 | 速度由 Rigidbody2D 统一驱动；未改全局 fixedDeltaTime，Android/iOS 待验证 |

## 8. 验证证据与缺口

- 本次最终 Assembly-CSharp 完整 C# 编译为 `0 error / 104 warning`；warning 为工程既有诊断。
- `DLevel_017.prefab` 当前 166 个 YAML 对象锚点全部唯一，本地 fileID 引用无缺失。
- 静态确认 `Sp_button_1_02` 为 Transform、SpriteRenderer、Rigidbody2D、SpringJoint2D 四组件，
  没有按钮 Collider2D；根节点仍只有一个 `isTrigger=true` 感应 Collider。
- 静态确认视觉刚体初始 gravityScale=0、约束为 5，Joint 初始禁用并连接 Static 刚体，参数
  `frequency=4`、`dampingRatio=0.1`；Level5TrackLine 的 Bottom/按钮引用完整。
- 静态确认 `springButton`、`springButtonBody`、`springJoint`、UnityEvent 目标/方法和脚本 GUID
  均已绑定，targetTag 留空后仍由 DLevel17Ctrl 严格过滤本关 Actor。
- 静态确认 Actor Rigidbody2D 从共享 Prefab 继承 `mass=3`，本关没有质量 override；Prefab 序列化
  `rightwardImpulse=12`、`upwardImpulse=72`、`rightwardMomentumRetentionDuration=0.35`、
  `launchStrength=3`，代码执行
  `AddForce(new Vector2(rightwardImpulse, upwardImpulse) * launchStrength, ForceMode2D.Impulse)` 与
  `WakeUp()`；没有 `Knockback` 或目标速度写入。
- 静态确认 DLevel17Ctrl 已不存在 `SetMoveInput(0)`、`StopMoving()`、`ResumeMoving()`、移动锁、
  落地解锁和 `airborneControlLockDuration`；DLevel001Ctrl 每帧向右。ActorCtrl 新开关默认关闭，
  开启时只保留同向且更快的外部 X 速度，始终保留 Y；DLevel17 在 0.35s/重置/禁用/
  协程结束时释放开关。重力恢复仍使用独立 `1.1s` 延时；暂停期间协程计时不推进，
  发射前再次校验 Actor 和 `IsOutcomeLocked`。
- 静态确认 Spring 根为 Layer 14 Draw，画线阻挡 mask 排除 Draw；Physics2D Layer 矩阵当前全开，
  Trigger 的 Actor 回调仍可发生。
- 静态确认 DLevel017 画线模板根 Rigidbody2D 的 `m_Material` 唯一 override 指向
  `DrawStrokeFree`（摩擦 0、弹性 0、Minimum）；共享画线 Prefab 仍为空材质。
- 静态确认 FallArea prefab GUID 恰有四个实例，名称、位置、缩放和 `10×10` Collider override
  均完整；GameCamera 未增加跟随组件。
- 目标 C# 与文档未发现新增空白错误；Prefab 是用户当前未跟踪的新资源，改用 YAML 锚点、
  本地引用、组件和序列化值断言校验。
- `uncertain`：尚未执行完整 PlayMode；需观察 Trigger 区域落笔/连续画线、侧面走入触发、
  压缩/回弹、当前 `launchStrength=3` 的右上弹道、0.35s 动量保留与门落点、
  Actor 在画线上的滑动手感、画线落地接触、顶部/四角失败、
  防重复触发以及 Reset/Replay。
- `uncertain`：Android/iOS 横屏分辨率和物理帧率一致性尚未验证。

## 9. 回滚

1. 从 DLevel_017 根移除 `DLevel17Ctrl` 组件及其脚本/meta。
2. 从 Spring 根移除 Trigger Collider，并把 OnTrigger2DHandle UnityEvent 恢复为任务前的
   空目标 Level32Ctrl 记录（该状态不会触发，仅用于完整版本回退）。
3. 从根层级和 Prefab 尾部移除四个 FallArea nested instances。
4. 若只回滚本次 Level032 视觉对齐，则移除按钮 Rigidbody2D/SpringJoint2D 及控制器对应字段，
   恢复上一版 Transform 回弹；始终不要恢复会挡路的按钮 Collider。LineRenderer、Actor、门、
   场景美术、固定 GameCamera、DLevel001Ctrl、旧 TrapEventHandlle、DB 与平台配置均保留。
5. 若只回滚本轮右向冲量，则删除 `rightwardImpulse`、`rightwardMomentumRetentionDuration`
   及 DLevel17Ctrl 的开关管理，删除 ActorCtrl 默认关闭的保留 API/分支，并恢复为纯向上
   `AddForce`；不要回滚 Spring 的 Draw Layer、Trigger/Joint、四向 FallArea 或固定镜头。
6. 若只回滚本轮光滑画线，删除 DLevel017 内嵌套 `DrawGame_DrawLine` 根 Rigidbody2D 的
   `m_Material -> DrawStrokeFree` override；不需修改或删除共享材质资源。
