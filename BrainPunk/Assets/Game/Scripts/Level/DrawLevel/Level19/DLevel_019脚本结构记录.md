# DLevel_019 脚本结构记录

## 0. 2026-08-03 最终物理参数（当前生效）

- Beam limits 当前为 `[-2°,78°]`，`beamAngularDamping=1`，Ground 碰撞保持；ganggan/Alien
  final 当前为 `(-4.03,-2.6858387)` / `(-7.15,2.23)`。
- Beam/Bowl Prefab 初始 Kinematic，DLevel10Ctrl 放球/双弹完成后才由子 Handler 解锁。
  球 Prefab override mass 10；可玩态应用 gravityScale 2.2、linearDamping 0.05，OnDisable 恢复共享初值。
- Bowl Edge 首尾点为 `(-0.96,0.32)` / `(0.96,0.05)`，中间五点保持原配置；strokeMass 当前为 100。

## 1. 基本信息

- 关卡：DrawGame 第 19 关
- 记录日期：2026-08-03
- 交付/验收状态：中点铰接杠杆、水平小碗与真实画线碰撞传力已配置；静态检查和 Unity 自动重编通过，PlayMode 与设备验收待完成
- 关卡目标边界：本记录只覆盖杠杆物理，不实现或修改球进门与成功判定
- 主要新增脚本：`DLevel19LeverPhysics.cs`
- 既有协作者：`DrawLevelCtrl.cs`、`DrawLineFeature.cs`、`DLevel10Ctrl.cs`、`DrawLevelCountdownCtrl.cs`
- 关卡 Prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_019.prefab`

## 2. 脚本与组件树

```text
DLevel_019
├─ DrawLevelCtrl                         # 既有 Draw 生命周期与结算；本次未修改
├─ DrawLineFeature                       # 生成带刚体/Collider 的画线，碰撞真实传力
├─ DLevel10Ctrl                          # 既有共享猫夹球入场、开夹和放球
├─ DrawLevelCountdownCtrl                # 既有 20 秒失败倒计时；本次未修改
├─ Alien / DrawGame_AlienClipBallRig     # 既有共享装置
│  └─ yellow_ball                        # 本关 Prefab mass 10；可玩态重力/阻尼由 Level19 局部覆盖
├─ ganggan / SortingGroup
│  ├─ DLevel19LeverPhysics               # 构建物理拓扑、杠杆 Reset、清线/Retry 球回碗
│  ├─ Sp_hx_gg_01                        # Pivot：Static Rigidbody2D，无 Collider
│  ├─ Sp_hx_bz_01                        # Beam：Prefab Kinematic，Intro 后 Dynamic
│  └─ Sp_hx_w_01                         # Bowl：Prefab Kinematic，Intro 后 Dynamic
└─ DrawGame_SuccessDoor                  # 既有门；本次不引用、不修改
```

Rigidbody2D、Collider2D 和 HingeJoint2D 已序列化在 Prefab；DLevel19LeverPhysics 在 Awake 中
通过 GetOrAdd 兼容缺失组件，并把 Inspector 参数重新应用到现有组件。

## 3. 类与组件职责边界

| 类/组件 | 职责 | 不负责 |
| --- | --- | --- |
| `DLevel19LeverPhysics` | 解析 Pivot/Beam/Bowl/Rig、配置二维物理；放球前锁住 Beam/Bowl，DLevel10Ctrl Intro 完成后应用球的可玩态重力/阻尼并解锁；普通 Draw Attempt Reset 和 Retry 都把共享球放回小碗；OnDisable 恢复球的共享初值 | 不改球质量、不手工 AddForce/AddTorque、不判断胜负 |
| `DrawLineFeature` | 创建运行时物理画线；画线 Collider 与 Beam 发生正常 Collision2D | 不知道杠杆几何，不调用 Level19 专用 API |
| `DLevel10Ctrl` | 驱动既有共享 Rig 入场、开夹、放球和 Retry 完成态 | 不配置杠杆，不拥有 Bowl/Beam |
| Pivot Rigidbody2D | 提供固定且显式的 Joint connectedBody | 不承担碰撞，不阻挡球或画线 |
| Beam Rigidbody2D + BoxCollider2D | 接收画线冲量，并围绕 Pivot 转动 | 不直接控制球 |
| Beam HingeJoint2D | 固定杆中心并限制相对转角 `[-2°,78°]`；正角为右端下压 | 不使用电机，不与 Pivot 碰撞 |
| Bowl Rigidbody2D + EdgeCollider2D | 跟随杆端、保持水平并托住/弹出球 | 不封闭碗口，不旋转 |
| Bowl HingeJoint2D | 把 Bowl 位置约束在 Beam 左端并传递受力 | 不限制相对角度，不与 Beam 碰撞 |
| `DrawLevelCtrl` / SuccessDoor | 保留既有结果链 | 不由 DLevel19LeverPhysics 调用或改写 |

## 4. 生命周期与关键调用

1. DLevel_019 实例化后，DLevel19LeverPhysics.Awake 解析显式绑定；字段为空时才按三个固定子节点名回退查找。
2. 控制器缓存 Pivot、Beam、Bowl 的初始世界位置和 Z 旋转，配置 Pivot Static、Beam/Bowl 的
   Dynamic 参数、Collider 与 Hinge；随后立即把 Beam/Bowl 切回 Kinematic 并恢复初始位姿。
3. Prefab 也把 Beam/Bowl 序列化为 Kinematic，因此从实例化到球完成落座前都不会自行解算。
4. `DrawLevelCtrl` 按层级串行等待 Intro Handler：根节点 `DLevel10Ctrl` 先完成入场、开夹、球落碗、
   双弹与装置退场；随后子节点 `DLevel19LeverPhysics.PlayDrawIntroAsync()` 才把球应用为
   `gravityScale=2.2 / linearDamping=0.05`，并把 Beam/Bowl 切为 Dynamic。
5. 玩家完成画线时，`OnDrawStrokeCompleted` 只做幂等解锁兜底，不施加力；画线必须实际撞到
   Beam BoxCollider2D 才能产生冲量。Beam 围绕 Pivot 转动，保持水平的 Bowl 再把真实冲量传给球。
6. Retry 与普通 Draw Attempt Reset 都执行同一原子顺序：Hold 杠杆 → 球临时 Kinematic 并定位到
   `bowl.TransformPoint(0,0.62)` → 应用球可玩态重力/阻尼并令 Beam/Bowl Dynamic → 球 Dynamic、
   清零并 Sleep。球一定在恢复 Dynamic 前取得可玩态参数。
7. OnDisable 会把杠杆恢复并锁为 Kinematic，同时恢复捕获的球初始 gravityScale/linearDamping；同一
   实例重新 OnEnable 时根据可玩态标记重新解锁并重应用局部参数。倒计时、门和结算仍由既有控制器处理。

## 5. Prefab、序列化与物理绑定

| 宿主 | 字段/组件 | 当前绑定或值 | 缺失/异常时行为 |
| --- | --- | --- | --- |
| `ganggan` | `pivot` | `Sp_hx_gg_01` | 回退按名称查找；仍为空则禁用控制器 |
| `ganggan` | `beam` | `Sp_hx_bz_01` | 同上 |
| `ganggan` | `bowl` | `Sp_hx_w_01` | 同上 |
| `ganggan` | `rig` | 本关 `DrawAlienClipBallRigView` | 缺失时杠杆仍工作，但普通清线重置和 Retry 都不会把球放回碗内 |
| `ganggan` | `retryBallSeatLocalOffset` | `(0,0.62)` | 控制两类重置时球刚体原点相对 Bowl 的落座点 |
| `ganggan` | `playableBallGravityScale` | 2.2 | DLevel10 首次放球演出完成后及 Retry 球恢复 Dynamic 前应用 |
| `ganggan` | `playableBallLinearDamping` | 0.05 | 只作用于可玩态；OnDisable 恢复捕获的共享初值 |
| `ganggan` Transform | local position `(-4.03,-2.6858387)` | 相对原始 X 右移 0.80；Y 与子节点/Joint 相对结构不变 |
| Alien Transform | final position `(-7.15,2.23)` | X 与 ganggan 相对原始位置同量右移，保持放球点和 Bowl 的原相对对齐 |
| Pivot | Rigidbody2D | Static、Simulated | 运行时 GetOrAdd 后统一覆盖；复位时不向 Static body 写速度 |
| Beam | Rigidbody2D | mass 25、linear 0.15、angular 1、gravity 1、Interpolate、Continuous | 参数由控制器 Inspector 在 Awake 集中应用 |
| Beam | BoxCollider2D | size `(0.27,5.9)`、offset `(0,-0.35)`、非 Trigger | 收短左端并避开 Bowl/球夹点 |
| Beam Box | sharedMaterial | `DrawStrokeStable`（friction 0.8 / bounce 0） | 缺失时重线可能沿斜杆滑回支点 |
| Beam Box | excludeLayers | Nothing (`0`) | 保留与 Ground 的真实碰撞 |
| Beam Hinge | connectedBody | Pivot；anchor `(0,0)`、connectedAnchor 约 `(0.04,0.59)` | 禁用自动锚点；层级位置变化会改变计算结果 |
| Beam Hinge | limits | `[-2°,78°]`，motor off，connected collision off | Unity 6 探针确认顺时针为正角；更晚释放让切线更偏右 |
| Bowl | Rigidbody2D | mass 1.5、linear 0.2、gravity 1、Interpolate、Continuous、FreezeRotation | 参数由控制器 Inspector 集中覆盖 |
| Bowl | EdgeCollider2D | 七点 U 形、首尾 `(-0.96,0.32)` / `(0.96,0.05)`、中间五点不变、radius 0.055、非 Trigger | 少于两点时回退代码默认同组 U 形点 |
| Bowl Edge | sharedMaterial | `DrawStrokeStable` | 空引用时无显式碗材质，稳定性需重新验证 |
| Bowl Edge | excludeLayers | Nothing (`0`) | 保留与 Ground 的真实碰撞 |
| Bowl Hinge | connectedBody | Beam；anchor `(0,0)`、connectedAnchor 约 `(0.073385,3.329732)` | 不使用角限位；connected collision off |
| Shared Rig Ball | Rigidbody2D mass | 10（本关 Prefab override） | 本控制器不覆盖质量 |
| DrawLineFeature | `strokeMass` | 100（用户当前值） | 本轮不覆盖 |
| DrawLine template | Rigidbody2D gravityScale | 5（未改） | 保留既有画线下落参数 |

## 6. 数据、资源、UI 与事件

- DB/存档/Luban：无新增或修改。
- 资源：Beam Box 与 Bowl Edge 都复用 `DrawStrokeStable.physicsMaterial2D`；无新图片、动画、音频
  或运行时加载地址，材质资源本体未修改。
- UI/倒计时：既有 HUD 与 20 秒失败倒计时保持不变。
- 事件/生命周期：不新增全局事件；OnDrawStrokeCompleted 只做幂等解锁兜底，故意不施力。
  首次 Intro 在 DLevel10Ctrl 完整放球演出之后应用球可玩态参数并解锁物理；普通清线与 Retry
  同步应用同一回碗终态；OnDisable 恢复共享球初始重力与阻尼。
- 结果：DLevel19LeverPhysics 不引用 SuccessDoor，不调用 TryResolve，不改变进门判定或结算。
- Analytics/SDK/平台配置：无新增或修改。

## 7. 相对原结构的差异

| 差异 | 原因 | 运行时影响 | 共享边界 |
| --- | --- | --- | --- |
| 三个纯 Sprite 节点改为运行时物理拓扑 | 原装置只有视觉，无法传力 | 画线、杆、碗、球进入同一 Physics2D 力链 | 只存在于 DLevel019 |
| Pivot 使用 Static RB 且无 Collider | 需要固定铰链，但不需要支座挡球/挡线 | 关节中心不漂移，视觉支架不产生额外碰撞 | 不修改 Layer Matrix |
| Beam 使用限位 Hinge | 杆必须绕中点转动且不能整圈翻转 | 右侧受击转化为左端抬升 | 参数保持关卡局部 |
| 限位最终调为 `[-2°,78°]`，Beam 角阻尼为 1 | 需要更偏横向的离碗方向与更充分的旋转响应 | 更晚释放且减小转动衰减；实际弹道待 PlayMode 验收 | 以实际 Joint 读数为准，不按视觉正负猜测 |
| ganggan 与 Alien 相对原始位置同量右移 0.80 且保留 Ground | 78° 行程需要避让 Ground_short | 保持真实 Ground 碰撞，并给长杆完整正向行程 | 不改子节点/Joint 相对结构或 Layer Matrix |
| Beam Box 收短并偏移 | 原 6.55 长盒延伸到 Bowl/球夹点 | 保留右侧受压区，释放左端净空 | 只改 Level19 杆体几何 |
| Beam 复用 DrawStrokeStable | 重画线会沿斜杆滑回支点 | 高摩擦、零弹性提高压杆有效性 | 复用资源，不改材质本体 |
| Bowl 使用独立刚体、Hinge 与 FreezeRotation | 普通 Transform 子节点会随 Beam 旋转 | Bowl 沿圆弧移动但保持水平 | 不新增共享关节组件 |
| Bowl 使用开放 U Edge + Stable 材质，首尾点调整 | 既要托球又要允许球沿目标侧离开 | 首尾为 `(-0.96,0.32)` / `(0.96,0.05)`，中间五点不变；摩擦稳球、零弹性不主动弹球 | 复用资源，不改资源本体 |
| 放球前 Beam/Bowl 保持 Kinematic | 机关在球落座前不应受重力或关节求解而移动 | DLevel10Ctrl 完整 Intro 后才进入 Dynamic 可玩态 | 依赖当前根 Handler 先于 ganggan 子 Handler 的层级顺序 |
| StrokeCompleted 不施力 | 用户要求纯物理 | 必须实际画到右侧并发生碰撞 | 不扩展 DrawStrokeResult |
| 本关球 Prefab mass 10，可玩态局部覆盖重力/阻尼 | 已有放球装置满足输入物来源，但入场与弹射需要不同物理阶段 | 入场节奏不变；放球演出后使用 2.2 / 0.05，Disable 时恢复共享初值 | 不修改共享 Rig 资源，不写球质量 |
| 清线与 Retry 显式把球放回 Bowl | 普通 Reset 必须收回已弹走的球；DLevel10Ctrl 通用 Cast 又会忽略 Dynamic Bowl | 两类重置都直接得到杠杆初态与碗内球 | 复用同一 Level19 局部落座方法，Retry 接既有接口 |

## 8. 验证证据与缺口

- 当前磁盘最终值记录为 Beam `angular=1 / limits=[-2°,78°]`、ganggan
  `(-4.03,-2.6858387)`、Alien final `(-7.15,2.23)`、球 Prefab mass 10、可玩态球
  `gravityScale=2.2 / linearDamping=0.05`，Bowl Edge 首尾 `(-0.96,0.32)` / `(0.96,0.05)`。
- Unity 6 既有最小探针实测本连接下顺时针转动对应正 jointAngle，继续作为限位符号依据。
- Unity 自动重编与独立 Unity Roslyn 全量 Assembly-CSharp 编译均成功，仅有既有 warnings；
  本轮没有编译错误。
- 关键字段/时序静态断言通过，Prefab 133 个 YAML 锚点唯一；`git diff --check` 通过，只有工作区既有的 LF/CRLF 提示。
- `uncertain`：未执行 Unity PlayMode；放球落点、碗内稳定、画线命中手感、杆转角、球弹道、
  78° Ground 接触范围、Edge 穿透、左端净空、高摩擦线接触、Joint 抖动和限位冲击待验收。
- `uncertain`：普通清线重置即时回球，以及失败重建/Replay 时 DLevel10Ctrl 先应用完成态、本组件
  后落座球的实际分发顺序、球在 Dynamic Bowl 内首帧保持休眠和接触唤醒待验收。
- `uncertain`：暂停、低帧率、Android/iOS 横屏 Physics2D 一致性和性能待验证。

## 9. 风险与回滚

- 主要风险是 Beam 质量 25 相对本关运行时画线质量后的冲量手感、质量 10 的球对 Bowl/Beam 的
  静态载荷、FreezeRotation + Hinge 的求解稳定性，以及高速球对细 EdgeCollider2D 的穿透。
- 当前保留画线 mass 100、球 Prefab mass 10、模板 gravity 5 和 Beam mass 25；若正式关卡仍
  手感异常，应先区分几何接触、
  材质摩擦和质量比例，不应无证据同时改动全部变量。
- Beam/Bowl 保留 Ground 碰撞；右移后的 78° 完整行程是否仍在动态接触余量内待 PlayMode 验收。
- 可玩态球 gravityScale/linearDamping 是本关临时覆盖；任何新增 Disable/销毁路径都必须继续保证
  共享初值恢复，且 Retry 必须在球切回 Dynamic 前完成覆盖。
- Retry 依赖本关组件顺序在 DLevel10Ctrl 完成态之后执行；若后续改变 Handler 收集/排序，必须重新
  验证球不会被通用落点逻辑覆盖。Rig 缺失时当前安全退出，但会留下没有球的可玩终态。
- 若美术层级或三个节点位置变化，两个 connectedAnchor 会随运行时 Transform 重新计算；必须重验
  杆中心和 Bowl 左端连接点，不应把当前数值当作与几何无关的常量。
- 只回滚最终调参时，应成组恢复修改前的 Upper、Beam 角阻尼、ganggan/Alien X、Bowl Edge 首尾点，
  并移除球可玩态重力/阻尼覆盖及恢复链；不要只回滚其中一项。完整回滚需同时移除 ganggan 组件和 Prefab 已序列化的
  Pivot/Beam/Bowl Rigidbody、Collider、Joint；
  不能只删除脚本。共享 Rig、DLevel10Ctrl、球、门、倒计时和 Draw 系统无需回滚。
