# DLevel_019 中点铰接物理杠杆功能与风险评估

## 0. 2026-08-03 最终物理参数（当前生效）

- Unity 6 探针确认本连接方向下顺时针为正 jointAngle；Beam limits 当前为 `[-2°,78°]`，
  `beamAngularDamping=1`。
- Beam/Bowl 继续碰撞 Ground。为给 78° 行程留出横向空间，`ganggan` 当前为
  `(-4.03,-2.6858387)`，Alien final 当前为 `(-7.15,2.23)`；两者相对原始 X 均右移 0.80，
  机关内部相对结构不变。
- Beam Box 从 `size=(0.27,6.55), offset=(0,0)` 收短并偏移为
  `size=(0.27,5.9), offset=(0,-0.35)`，保留右侧画线受压区，同时避开左端 Bowl/球夹点。
- Beam Collider 继续使用 `DrawStrokeStable`。Bowl Edge 首尾点当前为 `(-0.96,0.32)`、
  `(0.96,0.05)`，中间五点不变。
- 球 Prefab override 质量当前为 10；控制器不改质量，只在可玩态把球设为
  `gravityScale=2.2`、`linearDamping=0.05`。画线 `strokeMass=100`、Beam mass 25 保持。

## 1. 需求与背景

- 本关需要一套真实的二维杠杆装置：中间支点固定，长杆能够绕支点旋转，左侧小碗随杆端移动但不能旋转。
- 既有共享装置释放的球落入小碗；玩家画出的物理线撞击长杆右侧后，冲量经杠杆传到左侧并把球弹出。
- 本次只负责物理组件、碰撞传力和 Reset，不实现球进门、成功条件或门表现。

## 2. 原行为基线

- `DLevel_019/ganggan` 原来只有 `SortingGroup`；三个子物体
  `Sp_hx_gg_01`、`Sp_hx_bz_01`、`Sp_hx_w_01` 均只有 `Transform + SpriteRenderer`。
- 视觉支点、长杆和小碗没有 Rigidbody2D、Collider2D 或 Joint2D，因此画线无法撞击杆体，
  小碗也不能托球或把杠杆运动传给球。
- 本关复用猫夹球共享 Rig 和 DLevel10Ctrl；球在本关 Prefab 的质量 override 为 10，
  DLevel19LeverPhysics 不写球质量。

## 3. 影响范围

- `Assets/Game/Prefabs/DrawLevel/DLevel_019.prefab`
- `Assets/Game/Scripts/Level/DrawLevel/Level19/DLevel19LeverPhysics.cs`
- `Assets/Game/ArtSrc/Physics/DrawStrokeStable.physicsMaterial2D`（只复用，资源本体未修改）
- Level19 本地功能、风险与结构记录
- DrawGame 中央结构演进记录
- `DevelopMD/已有功能设计与使用记录.md`

本次不修改共享猫夹球 Rig、DLevel10Ctrl、DrawLineFeature、DrawLevelCtrl、成功门、倒计时、
FallArea、Physics2D Layer Matrix、Luban/DB、存档、UI、全局事件、analytics、SDK 或平台分支。

## 4. 修改后行为

1. `ganggan` 挂载 `DLevel19LeverPhysics`。组件在 Awake 中校验三项杠杆绑定并持有共享 Rig，
   缓存初始世界位姿，再为已有美术节点补齐或复用物理组件；缺少任一杠杆绑定时记录错误并禁用自身。
2. 固定支点绑定 `Sp_hx_gg_01`：运行时为 Static、Simulated 的 Rigidbody2D，不添加 Collider。
   它只提供不会漂移的铰链锚点，支座美术本身不参与实体碰撞。
3. 长杆绑定 `Sp_hx_bz_01`：Prefab 初始为 Kinematic，运行时应用质量 25、线性阻尼 0.15、角阻尼 1、
   重力倍率 1、Interpolate、Continuous；使用 `0.27×5.9`、offset `(0,-0.35)` 的非 Trigger
   BoxCollider2D 和 `DrawStrokeStable`。Awake 建好组件后继续保持锁定。
4. 长杆 HingeJoint2D 显式连接固定支点，双方不碰撞；长杆本地锚点为 `(0,0)`，支点连接锚点
   由当前层级几何计算为约 `(0.04,0.59)`。电机关闭，角度限制为 `[-2°,78°]`：正角允许
   右端下压、左端抬升，负角只保留 `2°` 反向余量。
5. 小碗绑定 `Sp_hx_w_01`：Prefab 初始为 Kinematic，质量 1.5、线性阻尼 0.2、重力倍率 1、
   Interpolate、Continuous，并使用 FreezeRotation；Awake 后与 Beam 一起保持锁定。
6. 小碗使用顶部敞开的 U 形 EdgeCollider2D，圆角半径 0.055；七个局部点依次为
   `(-0.96,0.32)`、`(-0.72,-0.08)`、`(-0.42,-0.24)`、`(0,-0.3)`、
   `(0.42,-0.24)`、`(0.72,-0.08)`、`(0.96,0.05)`。Collider 复用
   `DrawStrokeStable`（friction 0.8、bounciness 0），用于稳住碗内球且不主动弹球。
   Beam Box 与 Bowl Edge 均不排除 Ground；radius 保持不变。
7. 小碗 HingeJoint2D 显式连接长杆，双方不碰撞；小碗本地锚点为 `(0,0)`，长杆连接锚点
   由当前层级几何计算为约 `(0.073385,3.329732)`。Joint 不设角度限制；碗的 FreezeRotation
   负责保持水平，Joint 只传递位置约束和受力。
8. DrawLevel 串行执行 Handler：根 DLevel10Ctrl 先完整完成装置入场、放球落碗和双弹；随后子
   DLevel19LeverPhysics.PlayDrawIntroAsync 才应用球的可玩态重力/阻尼，并把 Beam/Bowl 切为 Dynamic。
9. OnDrawStrokeCompleted 不主动 AddForce/AddTorque，只负责异常误锁兜底；正常玩法仍由画线真实碰撞传力。
10. Retry 与 AttemptReset 统一执行 `Hold 杠杆 → 球 Kinematic 落座 → 应用球可玩态参数并让杠杆 Dynamic
    → 球 Dynamic/清速/Sleep`。
11. Static Pivot 复位时不再写速度，避免 Unity 对 Static Rigidbody2D 写速度的警告。
12. OnDisable 会把球的 gravityScale/linearDamping 恢复为进入本组件前捕获的共享初值，避免局部
    可玩态参数污染后续实例。
13. 为 78° 行程留出空间，ganggan 与 Alien final X 相对原始位置同量右移 0.80；Y 和内部结构不变。

## 5. 关键物理流程

```text
共享猫夹球 Rig 入场并释放当前 Prefab 球
  -> 球落入小碗 U 形 EdgeCollider2D
  -> DLevel10Ctrl 放球/双弹演出结束
  -> 应用球 gravityScale 2.2 / linearDamping 0.05，并解锁 Beam/Bowl
  -> 玩家在长杆右侧完成画线
  -> 画线刚体与长杆 BoxCollider2D 真实碰撞
  -> 长杆 Dynamic Rigidbody2D 围绕 Static Pivot 的 HingeJoint2D 转动
  -> Bowl HingeJoint2D 把杆端位移传给小碗
  -> Bowl FreezeRotation 保持世界水平
  -> 小碗碰撞面向球传递真实冲量，球脱离碗口
```

没有“画线完成即施力”、手工抛物线、Tween 位移或目标点反算。

```text
AttemptReset / Retry / Replay
  -> Hold：Beam/Bowl Kinematic + 恢复初态
  -> 球 Kinematic 并定位到 Bowl.TransformPoint(0, 0.62)
  -> 应用球可玩态重力/阻尼，Beam/Bowl Dynamic
  -> 球 Dynamic、清零并 Sleep
```

## 6. Inspector 与物理参数

| 对象/字段 | 当前值 | 作用 |
| --- | ---: | --- |
| Pivot / `Sp_hx_gg_01` | Static Rigidbody2D，无 Collider | 固定铰链中心，不参与实体碰撞 |
| Beam / `beamMass` | 25 | 长杆惯性；画线必须通过碰撞推动它 |
| Beam / `beamLinearDamping` | 0.15 | 抑制长杆平移残余 |
| Beam / `beamAngularDamping` | 1 | 保留更充分的旋转响应，同时抑制无限摆动 |
| Beam / `beamGravityScale` | 1 | 长杆接受真实重力 |
| Beam / `beamColliderSize` | `(0.27,5.9)` | 收短左端夹点，同时保留右侧受压区 |
| Beam / `beamColliderOffset` | `(0,-0.35)` | 沿杆局部负 Y 偏移，避开 Bowl/球 |
| Beam Box / material | DrawStrokeStable | 摩擦 0.8、弹性 0，减少重线滑回支点 |
| Beam Hinge / anchors | `(0,0)` / `(0.04,0.59)` | 长杆中心连接固定支点 |
| Beam Hinge / limits | `[-2°,78°]` | 正角为右端下压方向；关闭电机 |
| Bowl / `bowlMass` | 1.5 | 小碗及球对左端形成真实载荷 |
| Bowl / `bowlLinearDamping` | 0.2 | 抑制关节位置震荡 |
| Bowl / `bowlGravityScale` | 1 | 小碗接受真实重力 |
| Bowl / constraints | FreezeRotation | 始终保持世界水平 |
| Bowl Edge / radius | 0.055 | U 形边缘厚度 |
| Bowl Edge / points | 首尾 `(-0.96,0.32)` / `(0.96,0.05)` | 中间五点保持原配置，顶部仍敞开 |
| Bowl Edge / material | DrawStrokeStable | 摩擦 0.8、弹性 0 |
| Beam/Bowl Prefab body type | Kinematic | DLevel10Ctrl 放球/双弹完成前保持锁定 |
| `ganggan` / local position | `(-4.03,-2.6858387)` | 相对原始 X 右移 0.80，Y 保持当前值 |
| Alien final position | `(-7.15,2.23)` | X 与 ganggan 相对原始位置同量右移 |
| Beam/Bowl Collider / exclude layers | Nothing (`0`) | 保留与 Ground、球和画线的真实碰撞 |
| Bowl Hinge / anchors | `(0,0)` / `(0.073385,3.329732)` | 小碗中心连接长杆左端 |
| Bowl Hinge / limits | 关闭 | 允许杆相对水平碗自由转动 |
| 共享球质量 | 10（本关 Prefab override） | 控制器不写质量 |
| 球 / `playableBallGravityScale` | 2.2 | 只在 DLevel10 首次放球演出完成后的可玩态应用 |
| 球 / `playableBallLinearDamping` | 0.05 | 只在可玩态应用，以保留水平速度；OnDisable 恢复共享初值 |
| `rig` | 本关共享 DrawAlienClipBallRigView | 普通清线重置与 Retry 时取得既有球 Rigidbody2D |
| `retryBallSeatLocalOffset` | `(0,0.62)` | 两类重置时球刚体原点相对 Bowl 的落座位置 |
| DrawLineFeature / `strokeMass` | 100（用户当前值） | 本轮不覆盖 |
| DrawLine template / `gravityScale` | 5（未改） | 保留既有画线下落手感 |

## 7. 风险项

| 风险 | 等级 | 控制与现状 |
| --- | --- | --- |
| 把小碗作为普通杆体子节点会随杆旋转 | 高 | 小碗拥有独立 Dynamic Rigidbody2D、HingeJoint2D 和 FreezeRotation；运行时仍需观察求解稳定性 |
| Pivot 漂移导致整套装置移动 | 高 | Pivot 为 Static Rigidbody2D；无 Collider，避免与杆/线发生额外接触 |
| 杆绕错位置或层级调整后锚点漂移 | 高 | 两个 connectedAnchor 都由当前 Transform 几何计算；美术节点移动后必须重新核对 Scene 中的实际关节点 |
| 杆在 78° 时碰 Ground_short | 高 | ganggan 与 Alien final X 相对原始位置同量右移 0.80；正式触地范围仍需 PlayMode 验收 |
| Intro 期间杠杆先动导致球落不进碗 | 高 | Beam/Bowl Prefab Kinematic，Awake 后 Hold；根 DLevel10Ctrl 完成放球/双弹后子 Handler 才解锁 |
| Beam 左端与 Bowl/球形成夹点 | 高 | Box 长度由 6.55 收为 5.9，并 offsetY=-0.35；左端净空和各种杆角仍需 PlayMode 验证 |
| 高摩擦 Beam 把画线咬得过紧 | 中 | DrawStrokeStable 为 friction 0.8 / bounce 0；不同线长和撞击角度需确认不会阻碍期望压杆 |
| 碗口封闭或唇口点位阻碍弹出 | 高 | 使用顶部敞开的 U 形 EdgeCollider2D；首尾改为 `(-0.96,0.32)` / `(0.96,0.05)`，实际离碗方向待 PlayMode 验证 |
| 球从细 Edge 高速穿透 | 高 | Bowl 和 Beam 使用 Continuous；共享球自身的检测模式、实际峰值速度与移动端帧率仍需 PlayMode 核对 |
| 可玩态球参数污染共享 Rig | 高 | 只改 gravityScale/linearDamping，不改 Prefab mass 10；OnDisable 恢复捕获的共享初值 |
| FreezeRotation 与 Bowl Hinge 产生抖动 | 中 | Bowl Hinge 不启用角限位且关闭连接体碰撞，减少过约束；低帧率稳定性待验证 |
| 连接件互相碰撞导致物理解体 | 高 | 两个 Hinge 都 `enableCollision=false`，breakForce/breakTorque 为 Infinity |
| 运行时补组件不便于编辑态观察 | 中 | 所有手感参数集中暴露在 DLevel19LeverPhysics；需进入 PlayMode 查看最终 Rigidbody/Joint 状态 |
| DLevel10Ctrl Retry 落点过滤忽略 Dynamic Bowl | 高 | 本组件实现 Retry 完成态，在 DLevel10Ctrl 后按显式 Bowl 偏移 `(0,0.62)` 重新落座共享球 |
| 普通清线后球仍留在已弹飞位置 | 高 | OnDrawAttemptReset 在复位三件杠杆刚体后调用同一回球方法，立即恢复可玩初态 |
| Retry 球定位后首帧滚落或弹开 | 中 | 定位阶段临时 Kinematic，恢复 Dynamic 后再次清零并 Sleep；揭幕后唤醒与实际接触待 PlayMode 验证 |
| 共享 Rig 或球引用缺失 | 中 | Retry 落座安全退出，不影响杠杆；但球不会回碗，应在 Prefab 静态校验中视为必填 |
| 本次意外改变成功门或胜利条件 | 高 | 新脚本不引用门、不调用 TryResolve；门和成功逻辑明确不在范围内 |

## 8. 验证证据与缺口

- 当前磁盘参数为 ganggan `(-4.03,-2.6858387)`、Alien final `(-7.15,2.23)`、Beam
  `angularDamping=1 / limits=[-2°,78°]`、球 Prefab mass 10、可玩态球
  `gravityScale=2.2 / linearDamping=0.05`，Bowl Edge 首尾点为
  `(-0.96,0.32)` / `(0.96,0.05)`。
- 代码路径保持纯物理：`OnDrawStrokeCompleted` 只做异常误锁兜底解锁，没有 AddForce/AddTorque；
  球的弹出冲量仍只来自画线、Beam、Bowl 的 Physics2D 碰撞链。
- Unity 6 独立最小探针确认：Beam 从世界 Z `108.555°` 顺时针转 `10°` 时，jointAngle 为
  `+10°`，为本连接方向的限位符号提供运行时证据。
- Handler 顺序设计为：DLevel10Ctrl 完成首次放球演出后，DLevel19 PlayDrawIntroAsync 才应用
  可玩态球参数并 Release；Retry/AttemptReset 在球恢复 Dynamic 前完成同一参数应用。
- Unity 自动重编与独立 Unity Roslyn 全量 Assembly-CSharp 编译均成功，仅有既有 warnings；
  本轮没有编译错误。
- 关键字段/时序静态断言通过，Prefab 133 个 YAML 锚点唯一；`git diff --check` 通过，只有工作区既有的 LF/CRLF 提示。
- `uncertain`：尚未执行 Unity PlayMode。需验证放球入碗、静止稳定、不同位置/角度画线撞击、
  串行解锁、78° 触地、画线命中、球实际切线/速度、命中门、穿透与抖动。
- `uncertain`：普通清线重置后的即时回球，以及暂停/恢复、失败重建、手动 Reset、Replay 和连续
  尝试中 DLevel10Ctrl 与本组件的 Retry 执行顺序、球在 Dynamic Bowl 内的首帧稳定和后续唤醒
  尚未验证。
- `uncertain`：Android/iOS 横屏下的 Physics2D 求解、帧率差异和高速碰撞尚未验证。

## 9. 回滚

1. 若只回滚最终调参：恢复修改前的 Upper、Beam 角阻尼、ganggan/Alien X、Bowl Edge 首尾点，
   并移除可玩态球 gravityScale/linearDamping 覆盖及 OnDisable 恢复链；具体旧值应从对应提交恢复，
   不应只回滚其中一项。
2. 若完整回滚杠杆：从 `ganggan` 移除 DLevel19LeverPhysics，并删除 Pivot 的 Rigidbody2D、Beam 的
   Rigidbody2D/BoxCollider2D/HingeJoint2D、Bowl 的 Rigidbody2D/EdgeCollider2D/HingeJoint2D，随后
   删除本地脚本及 meta。当前这些物理组件已经序列化在 Prefab，不能只删脚本。
3. 保留原 `ganggan`、`Sp_hx_gg_01`、`Sp_hx_bz_01`、`Sp_hx_w_01` 美术和 SortingGroup。
4. 共享猫夹球 Rig、DLevel10Ctrl、球 Prefab mass 10、画线质量、门、倒计时、FallArea、画线系统和结算链均无需回滚；
   回滚后会同时失去普通清线重置与 Retry/Replay 的 Dynamic Bowl 显式回球修复。
