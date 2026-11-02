# DLevel_036 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame 第 36 关
- 记录日期：2026-08-03
- 当前状态：pingtai 整体匀速/浮动、platform_Stone 与 Actor 等位移 Kinematic 等待、首次 Draw 接触后
  两者同时 Dynamic 释放，并在下一物理步把平台乘坐点切向速度传给 Actor 形成投石机效果；暂停/结算停止和
  Attempt Reset 已实现；静态检查与 `Assembly-CSharp` 编译通过，PlayMode 和移动设备
  验收未完成。
- 需求依据：`DLevel036Ctrl.cs` 类注释。
- 参考基线：DLevel_037 的 Static 铰接点、Kinematic 等待态、Dynamic 释放和 SyncTransforms Reset；
  DLevel_022 的关卡局部 `FixedUpdate`、`DrawLevelCtrl.Resolved` 与暂停/结算边界。

## 2. 最终脚本树

```text
DLevel_036
├─ DrawLevelCtrl
├─ DrawLineFeature
├─ DelayAutoBeginDrawHandle
├─ DLevel036Ctrl : IDrawStrokeCompletedHandler
└─ Scene
   ├─ Actor（nested prefab）
   │  └─ Rigidbody2D（等待阶段 Kinematic 同步乘船；首次 Draw 接触后 Dynamic）
   ├─ pingtai（motionRoot，接触前整体移动）
   │  ├─ Sp_l93_cl_01
   │  │  └─ Static Rigidbody2D（Hinge connected body）
   │  └─ platform_Stone（nested prefab）
   │     ├─ Rigidbody2D（初始 Kinematic；首次 Draw 接触后 Dynamic）
   │     ├─ HingeJoint2D（连接 Sp_l93_cl_01）
   │     ├─ 现有平台 Collider2D
   │     └─ Ship（SpriteRenderer 视觉子节点，无独立物理组件）
   └─ Water（现有水面表现；不负责 Ship 浮动）
```

## 3. 职责与边界

| 类/组件 | 职责 | 不负责事项 |
| --- | --- | --- |
| `DLevel036Ctrl` | 缓存 pingtai、平台与 Actor 初态；等待态整体移动 pingtai 并用 MovePosition 同步 Actor；检测平台 Draw 接触并同时释放平台/Actor Dynamic；下一物理步传递平台乘坐点速度；处理暂停、结算、Disable 与 Attempt Reset | 不重建铰链，不判断胜负，不修改共享 ActorCtrl，不读写配置或存档 |
| `DrawLevelCtrl` | 管理关卡暂停、结果锁，并向本关控制器分发结算与重置 | 不计算船的路径 |
| pingtai Transform | 接触前同步移动静态铰接点、平台和 Ship 视觉 | Dynamic 释放后不继续移动 |
| platform_Stone Rigidbody2D + HingeJoint2D | 接触前 Kinematic；接触后 Dynamic 并绕静态铰接点响应 Line、重力和其它碰撞 | 不使用水体 Layer 11 浮力 |
| Actor Rigidbody2D | 接触前 Kinematic 并跟随船的世界位移；接触后 Dynamic 并恢复自身重力 | 不改变共享 Actor prefab 或 ActorCtrl 行为 |

- 控制器保留 `OnDrawStrokeCompleted` 空回调，只为接入现有 `IDrawStrokeCompletedHandler` 的 Attempt Reset
  契约；船的启动不依赖画线。
- 不新增全局事件、共享 Actor API、共享 Draw 组件修改或 GamePlayPanel 修改。

## 4. 生命周期与调用链

1. `Awake/OnEnable` 解析根 `DrawLevelCtrl`、`Scene/pingtai`、platform_Stone 与 Actor Rigidbody2D，缓存
   pingtai/平台/Actor 初态，并把平台和 Actor 保持为 Kinematic/Interpolate；平台等待阶段零重力，Actor
   保留原 Gravity Scale 配置供释放时继续使用。
2. 每个未暂停、未结算的 `FixedUpdate` 先用缓存的 8 项 ContactPoint2D 数组查询平台与 Draw Layer 的
   非 Trigger 接触；没有接触时按 X 线性/Y 正弦目标设置 pingtai 世界坐标，并以同一偏移 MovePosition Actor。
3. 首次 Draw 接触后把 platform_Stone 与 Actor 同时切为 Dynamic，并写入当前横向速度与正弦曲线瞬时 Y
   速度；平台应用重力倍率 `1`，Actor 保留 prefab Gravity Scale `2`，pingtai 停止移动。
4. 下一次 `FixedUpdate` 读取平台在释放时 Actor 乘坐点的 `GetPointVelocity`，按可调倍率一次性写给 Actor；
   船头命中产生的铰链角速度因此转化为角色切向抛射速度。
5. `DrawLevelCtrl.Resolved` 到达后把平台和 Actor 固定为 Kinematic 并清零刚体线速度/角速度。
6. `OnDrawAttemptReset` 恢复 pingtai、平台和 Actor 初态后同步 Physics2D；`OnDisable` 固定两刚体并解除局部事件订阅。

## 5. Prefab 序列化契约

| 字段 | 当前值/绑定 | 注意事项 |
| --- | --- | --- |
| `motionRoot` | `Scene/pingtai` Transform | 必填；接触前整体移动静态铰接点和平台 |
| `platformRigidbody` | `pingtai/platform_Stone` Rigidbody2D | 必填；Draw 接触检测和 Dynamic 释放对象 |
| `actorRigidbody` | `Scene/Actor` Rigidbody2D | 必填；等待跟随、同步释放和 Reset 对象 |
| `moveSpeed` | prefab 当前 `1`；脚本默认 `2` | 世界单位/秒；`0` 表示只浮动 |
| `verticalAmplitude` | prefab 当前 `0.05`；脚本默认 `0.08` | 相对 pingtai 初始 Y 的最大偏移；`0` 关闭浮动 |
| `verticalFrequency` | `0.5` | 每秒周期数；`0` 时保持初始 Y |
| `catapultVelocityMultiplier` | prefab 当前 `1.25` | 仅首次释放后的下一物理步使用；`0` 关闭额外点速度继承 |

- Line 判定固定使用项目 `Draw` Layer 名，Dynamic 重力倍率固定为 `1`；两者不是 prefab 调参入口，避免
  热重载或误配置让当前关卡失去状态切换。

## 6. 结构复盘、验证与回滚

- 本关是“移动整体在接触后转为局部铰接物理”的组合机制，局部控制器已经覆盖需求；当前不抽取共享移动平台组件。
- `Assembly-CSharp` 编译为 `0 error`；Prefab motionRoot/platformRigidbody/actorRigidbody 绑定、138 个 YAML anchor 唯一与
  本地引用完整。
- `uncertain`：PlayMode 的 Actor 等位移跟随、父 Transform 移动与铰链同步、首次接触双刚体切换时机、
  船头/支点不同命中位置的抛射速度、切换瞬间冲量、Dynamic 摆动、承载稳定性、镜头边界、
  暂停/Reset/Replay、完整胜负流程和 Android/iOS 尚未验证。
- 完整回滚见 `DLevel_036船移动功能说明.md` 与 `DLevel_036风险评估.md`。

## 2026-08-03 当前结构更正

> 本节是当前有效结构，替代上文中的 `pingtai/platform_Stone/HingeJoint2D` 记录。

```text
DLevel_036
├─ DLevel036Ctrl : IDrawStrokeCompletedHandler
└─ Scene
   ├─ ShipPivot（Kinematic Rigidbody2D，无 Collider）
   ├─ Ship（Rigidbody2D + PolygonCollider2D + HingeJoint2D）
   └─ Actor（ActorCtrl + Rigidbody2D）
```

- `DLevel036Ctrl` 在 `OnEnable` 缓存 Ship/ShipPivot/Actor 初态并进入 Kinematic 等待；三者一次性获得相同水平
  `linearVelocity`，之后由 Physics2D 推进，不再逐帧 MovePosition。
- `FixedUpdate` 用预分配 `ContactPoint2D[8]` 查询 Ship 与 Draw Layer 非 Trigger 接触。首次接触时
  Ship/Actor 同时切 Dynamic；Ship 的转动由碰撞和铰链求解产生，Actor 接受按 Line 相对位置和下落高度计算的抛射冲量。
- `OnDrawStrokeCompleted` 从 `DrawLineFeature` 取得对应 Line Rigidbody2D 并记录质心初始 Y；
  `CalculateCatapultForce` 使用 Line 与 Actor 的 X 相对位置决定左右，Y 固定向上，再按下落高度放大冲量。
- `CalculateCatapultForce` 最后将 Y 冲量提升到至少 `minimumActorUpwardImpulse`，避免 Line 贴近船体命中时
  下落高度接近零、基准冲量不足以对抗 Actor 重力。
- Ship HingeJoint2D 与 limits 完全由 prefab 配置，连接 ShipPivot 且当前为 `-20° ~ +20°`；控制器不再持有
  `shipHingeJoint`、`maxShipRotationAngle` 或重复写入铰链配置。
- `ReleaseBodiesToDynamic` 仅释放 Ship 和 Actor，ShipPivot 保持 Kinematic 与初始水平速度；
  铰链锚点约束取代旧 `FreezePositionY` 和代码角度夹取。
- Dynamic 阶段 `ApplyShipSpring` 根据 Ship 相对初始角度施加回正扭矩，并按当前角速度施加阻尼；
  当前 prefab 的 `shipSpringStrength=10`、`shipSpringDamping=2`。
- 当前序列化绑定：`platformRigidbody=Scene/Ship Rigidbody2D`、
  `shipPivotRigidbody=Scene/ShipPivot Rigidbody2D`、`actorRigidbody=Scene/Actor Rigidbody2D`、
  `initialHorizontalVelocity=1`（脚本默认 `2`）、`baseCatapultForce=(0.1,0.8)`、
  `forcePerDropUnit=500`（脚本默认 `1`）、`minimumActorUpwardImpulse=8`、
  `shipSpringStrength=10`、`shipSpringDamping=2`。
- 暂停只停止脚本推进；结算/Disable 固定两刚体；Attempt Reset 恢复 Ship/Actor 初始位姿、
  ShipPivot 初始位姿和三者 Kinematic 状态。不新增全局事件、共享 Actor API、资源、配置、存档或 UI 流。
- 结构复盘：相对位置/下落高度抛射与移动铰链是 Level36 局部机制，不抽取共享投石机组件。
- 无效代码清理后，不再保留旧平台局部位姿、motionRoot、正弦浮动、逐帧 Hot Reload 修复、
  持续 MovePosition、Transform 同步链、未使用约束缓存、人工船体角速度或重复铰链配置。
- 验证：Assembly-CSharp `0 error`；Prefab 133 anchors 唯一、本地引用无缺失。PlayMode 与移动端为 `uncertain`。
