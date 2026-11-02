# DLevel_008 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame 第 8 关
- 记录日期：2026-07-31
- 交付/验收状态：weapon 启动冲量与可调旋转冲量已实现；PlayMode 与设备验收未完成
- 参考基线关卡：DLevel_005
- 选择基线理由：DLevel5 已使用 `Rigidbody2D.AddForce(..., ForceMode2D.Impulse)` 驱动物理物体，
  可作为 2D 冲量 API 与世界方向的本地代码证据
- 基线证据限制：DLevel5 的触发时机与对象所有权不同，本关只参考施力方式；其完整设备验收
  状态为 `uncertain`
- 主要脚本：`DLevel8Ctrl.cs`
- 关卡 prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_008.prefab`

## 2. 脚本与组件树

```text
DLevel_008
├─ DrawLevelCtrl
├─ DrawLineFeature
├─ DelayAutoBeginDrawHandle
├─ DrawLevelCountdownCtrl
├─ DLevel8Ctrl
├─ DrawLineArea
└─ level8
   └─ weapon
      ├─ SpriteRenderer
      ├─ PolygonCollider2D
      ├─ Sp_hx_bbox_01 角色子节点
      │  └─ BoxCollider2D
      └─ Rigidbody2D
```

## 3. 类职责与边界

| 类/组件 | 类型 | 职责 | 继承/接口 | 创建与销毁方 | 不负责事项 |
| --- | --- | --- | --- | --- | --- |
| DrawLevelCtrl | 共享主控制器 | Draw 运行时初始化与结算 | `IDrawLevelRuntime` | DrawPlayCtrl | 不设置 Level8 启动冲量 |
| DrawLineFeature | 共享功能 | 画线输入、线段与墨水状态 | MonoBehaviour | DLevel prefab | 不操作 weapon |
| DelayAutoBeginDrawHandle | 共享局部 Handler | 按现有 delay 开放画线 | `IDrawLevelIntroHandler` | DLevel prefab | 不施力 |
| DrawLevelCountdownCtrl | 共享局部 Handler | 按当前 prefab 配置处理倒计时 | `IDrawStrokeCompletedHandler` | DLevel prefab | 不施力 |
| DLevel8Ctrl | 关卡局部控制器 | 第一笔完成后切换 weapon 为 Dynamic、施加世界方向冲量与旋转冲量、处理本地重置并绘制 Gizmo | `IDrawStrokeCompletedHandler`、`IDebuger` | DLevel prefab | 不处理画线输入、UI、结算、存档 |
| weapon | 本关物理对象 | 接收冲量并参与 2D 碰撞/重力 | Rigidbody2D | DLevel prefab | 不持有控制器状态 |

## 4. 生命周期与关键调用

1. DrawPlayCtrl 加载并实例化 DLevel_008。
2. DLevel8Ctrl 的 `Awake()` 解析刚体、缓存初始相对位姿，并保持 weapon Kinematic。
3. DrawLevelCtrl 初始化时发现 DLevel8Ctrl 的 `IDrawStrokeCompletedHandler`。
4. 第一笔有效画线完成后，DrawLevelCtrl 同步调用 DLevel8Ctrl。
5. DLevel8Ctrl 把 weapon 切换为 Dynamic、清零速度，再按归一化世界方向写入一次线性
   Impulse，并按 `rotationImpulse` 写入一次旋转 Impulse。
6. weapon 后续平移、旋转与碰撞由 Unity 2D 物理负责；DLevel8Ctrl 不再逐帧写入。
7. Reset 时恢复初始相对位姿、Kinematic、零速度与发射门禁；整关重建也恢复 prefab 初态。
8. 选中控制器时仅绘制方向 Gizmo，不触发运行时施力。

## 5. Prefab、序列化与资源绑定

| 宿主 | 字段/组件 | 绑定对象或资源 | 必填/可选 | 缺失时行为 |
| --- | --- | --- | --- | --- |
| DLevel_008 根 | DLevel8Ctrl | 本关启动冲量控制器 | 必填 | weapon 不会收到本关启动冲量 |
| DLevel8Ctrl | weaponRigidbody | level8/weapon Rigidbody2D | 必填 | 固定层级回退；仍缺失则报错并停止 |
| DLevel8Ctrl | forceDirection | `(1, -0.05)` 世界方向 | 可调 | 零向量时警告且不施力 |
| DLevel8Ctrl | forceMagnitude | `500` Impulse | 可调 | 不大于零时警告且不施力 |
| DLevel8Ctrl | rotationImpulse | `-60` 旋转 Impulse；负值顺时针 | 可调 | 0 时保持原有无旋转冲量行为 |
| weapon | Rigidbody2D | 初始 Kinematic，mass=30；首笔后切 Dynamic | 必填 | 无法参与预期 2D 动力学 |

## 6. 数据、UI、资源与事件流

- DB/存档：本关脚本不访问；完整胜利后的既有 Draw 进度链不在本次变更范围。
- Luban/配置：无新增或读取。
- UI：无直接操作。
- 局部引用：DLevel8Ctrl 只持有本关 weapon Rigidbody2D。
- 事件：无新全局事件、局部事件或 UnityEvent。
- 资源加载：随 DLevel_008 prefab 加载，无新增地址。
- Analytics/SDK：无。

## 7. 相对参考基线的差异

| 差异 | 原因 | 玩家/运行时影响 | 验证范围 | 是否形成演进候选 |
| --- | --- | --- | --- | --- |
| DLevel5 首笔后连续生成箭，Level8 首笔后只发射既有 weapon 一次 | 本关只有一个预置 weapon | 进入关卡保持静止，落笔完成后才运动 | 进入、首笔、重复回调、Reset、Replay | 否，本关必要差异 |
| 方向与大小拆分序列化 | 用户要求两者分别可调 | 调整方向不会隐式改变大小 | Inspector 与实际轨迹 | 观察中 |
| 首笔追加一次旋转 Impulse | 用户希望挥刀而非整体平移 | 角色与刀随 weapon 顺时针小幅转动，仍参与同一物理碰撞 | 首笔轨迹、刀尖朝向、目标命中 | 观察中 |
| 只在选中时画固定长度 Gizmo 箭头 | 需求只要求展示方向 | Scene 视图可读，不把大冲量映射成超长线 | 不同方向与零向量 | 否，编辑器局部表现 |
| 实现 IDrawStrokeCompletedHandler 并恢复局部初态 | 用户明确要求首笔后发射 | 施力与当前尝试画线生命周期一致 | Handler 发现、首笔门禁、Reset | 延续既有稳定模式 |

## 8. 可复用结论与优化候选

- 本关继续沿用的稳定结构：一个共享 DrawLevelCtrl 加一个关卡局部 MonoBehaviour。
- 本关验证的新结构：在既有首笔线性 Impulse 后追加一次局部旋转 Impulse；无需共享接口或逐帧动画。
- 暂不共享的局部实现：weapon 层级、启动时机、世界方向、线性/旋转冲量默认值和 Gizmo。
- 后续关卡需继续观察的候选：若多关都需要可视化一次性冲量，再评估局部基类或编辑器工具；
  当前不修改共享 Draw 接口。

## 9. 验证证据与缺口

- 静态检查：脚本 GUID、根组件、weapon Rigidbody2D 引用、方向/大小/旋转冲量字段存在；
  首笔调用顺序包含 `AddForce` 后的 `AddTorque`；YAML 锚点无重复。
- 编译：历史基线的 Assembly-CSharp 单进程构建通过（`0 error / 103 warning`）；本次增量
  编译未能执行，当前机器没有可用的 .NET SDK 或 Unity Editor。
- Unity PlayMode：未执行。
- Android/iOS：未执行。
- 未验证项：默认线性/旋转冲量手感、weapon 轨迹/转动/碰撞、暂停、胜负影响、Reset/Replay 复现、
  完整关卡解法和移动端物理一致性。

## 10. 风险与回滚

- 主要风险：当前 `(1, -0.05)`、`500` 线性冲量和 `-60` 旋转冲量可能与最终关卡设计不一致；
  旋转会改变长刀 PolygonCollider2D 的扫过范围与命中时机；Handler 触发和 Reset
  仍未 PlayMode 验证；
  当前完整胜负需求缺失；
  PlayMode 和移动端物理表现未验证。
- 回滚路径：移除 `rotationImpulse` 序列化值与 `ApplyRotationImpulse()` 调用即可恢复
  纯线性发射；若需整体回滚，再从 DLevel_008 根节点移除 DLevel8Ctrl，且不修改用户已有的
  Level8 美术、碰撞体、刚体和 Draw 配置。
