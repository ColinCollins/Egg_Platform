# DLevel_008 武器启动冲量功能与风险评估

## 1. 需求与背景

- `DLevel8Ctrl` 原始脚本注释要求给场景中的 `weapon` 施加一个力。
- 力的方向需要在 Scene 视图通过 Gizmos 展示，方向与大小都应能在 Inspector 调整。
- 用户进一步明确：进入关卡时不能施力；第一笔有效画线完成后，先把 weapon 的
  `Rigidbody2D` 改为 Dynamic，再施加一次冲量。
- 用户后续反馈：当前角色与刀向右平移的挥刀感不足，希望首笔释放时带有小幅旋转。

## 2. 原行为基线

- 初始 `DLevel8Ctrl.cs` 只有需求注释、空 `Start()` 和空 `Update()`，没有运行时行为。
- 第一版实现曾在 `Start()` 直接施力；这与用户补充的首笔触发要求不一致，现已移除。
- 当前实现原本在首笔前后都将 `angularVelocity` 清零且未施加角冲量，因此角色与刀只会
  沿线性冲量方向整体平移；本次以此作为旋转增强前的行为基线。
- `DLevel_008.prefab` 已包含 `level8/weapon`；该节点已有 `PolygonCollider2D` 和
  `Rigidbody2D`，刚体质量为 30；本次将其运行前初态固定为 Kinematic。
- 任务开始时 `DLevel_008.prefab` 与 Level8 目录均为用户未提交的新文件，本次保留其既有
  美术、物理和 Draw 配置，只追加本关控制器及绑定。

## 3. 影响范围

- `Assets/Game/Scripts/Level/DrawLevel/Level8/DLevel8Ctrl.cs`
- `Assets/Game/Prefabs/DrawLevel/DLevel_008.prefab`
- Level8 本地功能、风险与脚本结构文档
- DrawGame 中央结构演进记录
- `DevelopMD/已有功能设计与使用记录.md`

未修改共享 `DrawLevelCtrl`、`DrawLineFeature`、GameplayPanel、Actor、DB、存档、Luban、
资源地址、全局事件、analytics、SDK 或平台分支。

## 4. 修改后行为

1. `DLevel8Ctrl` 实现 `IDrawStrokeCompletedHandler`，由同一 DLevel 根节点上的
   `DrawLevelCtrl` 在初始化时自动发现。
2. `Awake()` 只缓存 weapon 初始相对位姿，并恢复 Kinematic 与零速度，不施加冲量。
3. `OnDrawStrokeCompleted()` 只接受第一笔有效画线，并通过 `_hasLaunched` 防止重复发射。
4. 控制器优先使用 prefab 显式绑定的 `weaponRigidbody`；引用缺失时只按
   `level8/weapon` 固定层级做一次局部回退查找。
5. 首笔回调先把 weapon 刚体切换为 Dynamic，并清空线速度与角速度。
6. `forceDirection` 表示世界坐标方向，施力前会归一化；向量本身长度不影响冲量大小。
7. `forceMagnitude` 独立控制大小；DLevel_008 prefab 当前保存方向 `(1, -0.05)`、大小
   `500`，负值按 0 处理。
8. 线性冲量有效时，读取可调的 `rotationImpulse`，通过
   `Rigidbody2D.AddTorque(..., ForceMode2D.Impulse)` 一次性追加角冲量；正值逆时针、
   负值顺时针。当前 prefab 使用 `-60`，用于小幅顺时针挥刀。
9. 引用有效、方向非零且大小大于 0 时，通过
   `Rigidbody2D.AddForce(..., ForceMode2D.Impulse)` 一次性施力并唤醒刚体。
10. 选中控制器时，`OnDrawGizmosSelected()` 从 weapon 质心绘制黄色方向箭头；Gizmo
   只表达方向，不访问 DB、运行时管理器或存档。
11. `OnDrawAttemptReset()` 会恢复 weapon 的初始相对位姿、Kinematic、零速度和发射门禁；
    整关 Reset/Replay 重建也会得到同一初态。本脚本没有 `Update()`、协程或持续分配。

## 5. 数据、资源、UI 与事件流

```text
DLevel_008 实例创建
  -> DLevel8Ctrl.Awake：缓存初态，保持 weapon Kinematic
第一笔有效画线完成
  -> DrawLevelCtrl.OnStrokeCompleted
  -> DLevel8Ctrl.OnDrawStrokeCompleted
  -> weapon Rigidbody2D 切换为 Dynamic、清零速度
  -> 读取本地序列化方向/大小/旋转冲量
  -> Rigidbody2D.AddForce(Impulse)
  -> Rigidbody2D.AddTorque(Impulse)
  -> weapon 继续参与既有 2D 物理
Reset
  -> 恢复初始相对位姿、Kinematic 和零速度
```

- DB/存档：不直接访问。
- Luban/配置：不访问；参数只在 DLevel_008 prefab Inspector 中保存。
- UI：不直接操作。
- 事件：无新 `PlayEvents`、Bear 全局事件或 UnityEvent。
- 资源加载：无新增资源或地址。
- Analytics/SDK：无。
- 平台：Android/iOS 共用 Unity 2D 物理路径，无平台分支。

## 6. 风险项

| 风险 | 等级 | 控制与现状 |
| --- | --- | --- |
| weapon 引用缺失 | 中 | prefab 显式绑定；运行时保留固定层级回退，仍缺失时使用 Bear Logger 报错并停止 |
| 方向为零或大小不大于零 | 低 | 不施力并输出 Bear Logger 警告，不产生无效物理调用 |
| 当前线性冲量不符合关卡手感 | 中 | 方向与大小均可调；prefab 当前为 `(1, -0.05)` 与 `500`，PlayMode 尚未调校 |
| 旋转冲量方向/幅度不符合挥刀手感 | 中 | `rotationImpulse` 可调，当前为 `-60`；负值顺时针，但实际刀尖轨迹与飞行时长仍需 PlayMode 调校 |
| 旋转改变长刀碰撞扫过范围或命中时机 | 中 | 仍使用既有 PolygonCollider2D 和同一 Rigidbody2D；需验证目标碰撞、地面卡住与胜负链 |
| DLevel8Ctrl 未被 DrawLevelCtrl 发现 | 中 | 实现现有 `IDrawStrokeCompletedHandler`，并挂在 DLevel_008 根节点；仍需 PlayMode 确认回调 |
| 重置后残留上一轮位姿或速度 | 中 | Awake 缓存相对位姿；OnDrawAttemptReset 恢复 Kinematic、位置、旋转和零速度 |
| 不同设备物理表现差异 | 中 | 使用固定世界方向和单次 Impulse，无逐帧累计；Android/iOS 横屏仍需实机验证 |
| 编辑器回调触发运行时链 | 低 | Gizmo 只读本地序列化引用、方向与常量，不访问 DB/Save/Manager |

## 7. 验证证据

- `Assembly-CSharp.csproj` 已包含 `DLevel8Ctrl.cs`。
- 修改前的单进程 Assembly-CSharp 构建曾通过：`0 error / 103 warning`；本次增量构建因当前
  机器没有可用的 .NET SDK 或 Unity Editor 未能执行，历史 warning 未作为本次运行时结论。
- 静态确认 prefab 根节点新增 DLevel8Ctrl，脚本 GUID 与 `.meta` 一致。
- 静态确认 `weaponRigidbody` 指向 `level8/weapon` 的 Rigidbody2D。
- 静态确认 DLevel_008 prefab YAML 本地锚点无重复。
- 目标脚本与 prefab 通过 `git diff --check`。
- 静态确认 DLevel8Ctrl 已实现 `IDrawStrokeCompletedHandler`，首笔直接顺序为
  `OnDrawStrokeCompleted -> bodyType=Dynamic -> ApplyForce -> Rigidbody2D.AddForce`
  -> `ApplyRotationImpulse -> Rigidbody2D.AddTorque`。
- 静态确认 prefab 中 weapon 初始 `m_BodyType=1`（Kinematic）。
- 工作区已有 Unity 生成的 `Library/ScriptAssemblies/Assembly-CSharp.dll` 包含
  `rotationImpulse` 与 `ApplyRotationImpulse` 符号；由于当前没有可用 Unity Editor，
  本次无法重新触发增量编译，仍不把该产物当作 PlayMode 证据。

## 8. 剩余缺口

- `uncertain`：尚未进入 Unity PlayMode 观察默认方向、速度、旋转幅度、碰撞、成功/失败影响及
  Reset/Replay 后是否符合关卡设计手感。
- `uncertain`：尚未实际观察进入关卡时 weapon 保持静止、第一笔完成后才切换 Dynamic 并受力。
- Unity Editor 日志最近 500 行没有本次脚本编译刷新记录，因此不能把运行中 Editor 状态
  作为额外编译证据。
- 尚未验证 Android/iOS 横屏设备上的物理一致性、帧率和触控解法。
- 未收到 Level 8 的完整胜负与解法说明；本次只在原有武器启动冲量上追加局部旋转表现。

## 9. 回滚路径

1. 从 `DLevel_008.prefab` 根节点移除 DLevel8Ctrl 组件。
2. 将 `DLevel8Ctrl.cs` 恢复为任务前空骨架，或删除该脚本及 meta；若只回滚本次时机调整，
   则移除 Handler 与初态恢复逻辑并恢复原触发方案。
3. 删除本关新增文档记录；保留用户任务前已有的 Level8 prefab 美术、物理与 Draw 配置。
4. 若只回滚本次挥刀表现，移除 `rotationImpulse` 序列化字段、Prefab 的 `rotationImpulse`
   值和 `ApplyRotationImpulse()` 调用，线性首笔发射与 Reset 路径保持不变。
