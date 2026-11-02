# DLevel_030 脚本结构记录

## 1. 需求与参考基线

- 需求：`platform_Stone` 的中心点必须保持固定，不允许整体下坠或发生位置抖动；平台仍需响应
  Actor、Draw Line 等二维碰撞并绕中心转动；左侧较长的一段需要表现得更重。
- 参考基线：沿用 DrawGame 根节点局部控制器与 `IDrawStrokeCompletedHandler` 生命周期模式；物理结构参考
  DLevel_037 的中心平衡机关，但 DLevel_030 不使用 HingeJoint2D，而是使用位置约束和等效重心力矩。
- 隔离边界：本关不修改共享 `DrawLevelCtrl`、`DrawLineFeature`、`ActorCtrl`、`GamePlayPanel`、全局事件、
  Physics2D Layer Matrix、配置、存档或资源加载。

## 2. 最终脚本树与职责

```text
DLevel_030
├─ DrawLevelCtrl                 # Draw 关卡结果、暂停与尝试重置分发
├─ DrawLineFeature              # 画线生成和动态线段物理
├─ DrawLevelCountdownCtrl       # 10 秒倒计时
└─ DLevel30Ctrl                 # 本关偏重平台稳定物理与重置
   └─ platform_Stone/Rigidbody2D
```

`DLevel30Ctrl` 的职责：

1. 在运行时把 `platform_Stone` 配置为 Dynamic、原生 Gravity Scale 0、Freeze Position X/Y、旋转开放。
2. 把刚体本地质心固定在平台根节点原点，避免 Collider 自动质量导致内部质心偏离支点。
3. 在 `FixedUpdate()` 中按配置的局部等效重心偏移施加重力力矩，只改变角运动，不产生平移重力。
4. 暂停时停止主动施加力矩；结算时清零速度并 Freeze All，防止结果阶段继续抖动。
5. `OnDrawAttemptReset()` 恢复初始中心、初始角度、零速度和正常旋转约束。

## 3. 序列化绑定与物理参数

- `platformRigidbody`：`platform_Stone` 根节点上的 Rigidbody2D，prefab 本地 fileID
  `4477135293997542308`。
- `drawLevelCtrl`：DLevel_030 根节点 `DrawLevelCtrl`。
- `platformMass = 10`：用于碰撞响应和转动惯量。
- `longSideCenterOfMassOffset = (-1.25, 0)`：负 X 表示左侧长边更重。
- `effectiveGravityScale = 1`：只参与等效重力力矩计算，不作为刚体平移重力。
- `angularDamping = 1.5`：抑制碰撞后的高频角速度。
- prefab 中平台 Rigidbody2D 同步保存 `Use Auto Mass=false`、`Mass=10`、`Gravity Scale=0`、
  `Constraints=Freeze Position X/Y`、`Interpolate` 和 `Continuous`。

## 4. 关键调用与生命周期

1. `OnEnable()` 仅在 PlayMode 解析引用、缓存初始位姿、应用稳定物理配置并订阅 `Resolved`。
2. 每个 `FixedUpdate()` 先检查结算和暂停，再将局部偏重向量转换为世界向量，计算
   `offset × (mass × Physics2D.gravity × scale)` 并调用 `AddTorque()`。
3. `OnDrawStrokeCompleted()` 不释放或重配平台；平台从开场即参与物理。该接口用于接入统一 Attempt Reset。
4. `OnDrawAttemptReset()` 临时关闭模拟，恢复初始刚体位置与角度，再重新应用稳定配置并同步 Physics2D。
5. `Resolved` 后平台 Freeze All；下一次 Attempt Reset 恢复 Freeze Position X/Y 且重新开放旋转。
6. `OnDisable()` 解除局部结果事件订阅，不创建或派发全局事件。

## 5. 数据、资源、UI 与事件流

- 数据与存档：无新增或修改。
- 资源：继续使用 DLevel_030 当前 prefab、Sprite、Actor 和 Draw Line 资源，不新增运行时资源地址。
- UI：继续由共享 Draw HUD 与本关倒计时处理，`DLevel30Ctrl` 不访问 UI。
- 事件：仅订阅同根节点 `DrawLevelCtrl.Resolved`；Reset 通过现有
  `IDrawStrokeCompletedHandler.OnDrawAttemptReset()` 局部分发。

## 6. 相对参考关卡的差异与结构复盘

- DLevel_037 通过 HingeJoint2D 把 Dynamic 平台连接到 Static 齿轮；DLevel_030 的产品目标是中心位置
  绝对稳定，因此取消位置关节求解，改为 Rigidbody2D Freeze Position X/Y。
- 由于原生重力会被位置约束抵消且无法直接表现偏重，本关显式关闭原生重力，并仅注入旋转力矩。
- 该机制目前只服务 DLevel_030；在多个已验收关卡出现相同“固定中心、偏重旋转”需求前，不抽取共享组件。

## 7. 验证、风险与回滚

- 已完成 `Assembly-CSharp.csproj --no-restore` 全量编译：0 error；工程既有 warning 1119 个。
- 已静态确认 DLevel_030 根节点脚本、本地 fileID、平台引用、质量、重力和约束可解析。
- Unity PlayMode 中 Actor/Draw Line 实际碰撞手感、连续 Reset、暂停/恢复、结算、Replay、Android/iOS
  横屏设备帧率和物理一致性仍为 `uncertain`，需在编辑器及真机验证。
- 若左侧下压过强，先减小 `longSideCenterOfMassOffset.x` 的绝对值或 `effectiveGravityScale`；若碰撞后摆动过久，
  增大 `angularDamping`。不要重新同时启用 HingeJoint2D 和 Freeze Position X/Y。
- 完整回滚：移除根节点 `DLevel30Ctrl`，恢复平台 Rigidbody2D 的原物理配置；共享 Draw/Actor 系统无需迁移。
