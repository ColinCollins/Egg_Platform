# DLevel_022 车辆画线行驶功能与风险评估

## 1. 需求与原始行为基线

- 需求：有效画线完成后，线段出现在 `startPoint`；场景两只 `Sp_hx_mtlz_01` 车轮从 0 开始
  向前加速，到可在 Inspector 调整的最大速度后匀速滚动。
- 追加需求：控制本关 Actor，第一笔完成之前保持 Rigidbody2D 为 Kinematic，第一笔完成之后才
  切换为 Dynamic。
- 追加需求：生成的完成线段 Rigidbody2D 使用 Dynamic，不作为 Kinematic 移动平台。
- 追加需求：两只轮子的速度必须由各自 Rigidbody2D 控制，使其与 stone 的碰撞由 Physics2D 解算，
  不再通过 wheelsRoot Transform 强制移动。
- 参考图表达的是“在白框内画出车辆主体，主体放到双轮上，承载 Actor 向右到达成功门”。
- 修改前 `DLevel022Ctrl` 只有空 `Start/Update`，prefab 虽已挂脚本、startPoint、wheels 和两只
  CircleCollider2D 车轮，但画线后没有任何定位、移动或轮子旋转逻辑。

## 2. 影响范围

- 修改：`Assets/Game/Scripts/Level/DrawLevel/Level22/DLevel022Ctrl.cs`
- 修改：`Assets/Game/Prefabs/DrawLevel/DLevel_022.prefab`
- 新增/更新：Level22 本地结构、风险文档，以及 DrawGame 中央演进/已有设计记录
- 未修改：`DrawLineFeature`、`DrawLevelCtrl`、`ActorCtrl`、成功门 prefab、Physics2D 全局配置、
  Luban/运行时配置、存档、UI、analytics、SDK 与平台工程文件

## 3. 事实

- `DrawLineFeature.CompleteActiveStroke()` 在分发 `StrokeCompleted` 前已把有效线段几何中心归到
  运行时线段 Transform 原点、生成碰撞体并启用 Rigidbody2D。
- `DrawLevelCtrl` 会缓存子层级中的 `IDrawStrokeCompletedHandler`，在每笔完成后同步调用，并在
  Attempt Reset 时先调用 `DrawLineFeature.ResetAttempt()` 销毁线段，再通知本地 Handler。
- DLevel_022 当前 `allowMultipleStrokes=0`，只允许第一笔有效画线。
- startPoint 当前局部位置为 `(-7.28, 0.4, 0)`；两轮当前局部位置分别为
  `(-8.86, -1.77, 0)` 和 `(-5.68, -1.77, 0)`，CircleCollider2D 半径均为 `0.76`。
- 两轮当前各有 Dynamic Rigidbody2D，质量 `30`、Gravity Scale `1`、Continuous Collision Detection、
  无 Constraints；stone/stone (1) 是非 Trigger 静态 Collider，Default 与 Default 层碰撞矩阵开放。
- Actor nested prefab 当前世界布局位于车辆区域上方，Actor Ground LayerMask 含 Draw Layer；
  运行时线段根和其生成的子 Collider 使用 Draw Layer。
- 共享 Actor prefab 的 Rigidbody2D 默认 `m_BodyType=0`（Dynamic）；DLevel_022 实例已有
  `m_BodyType=1`（Kinematic）override。当前 DLevel022Ctrl 已显式绑定 Actor Rigidbody2D，脚本仍保留
  子层级 `ActorCtrl` 回退解析作为容错。
- 新实现每个 FixedUpdate 只更新当前速度、一个 Dynamic 车身和两个 Dynamic 轮体的 X 速度及轮体
  角速度；不移动 wheelsRoot、不直接旋转 Transform，无逐帧集合分配。

## 4. 直接结论

- 第一笔完成后，控制器先暂停线段 Physics2D 模拟，参考 DLevel7 直接把运行时线段 Transform 移到
  startPoint，并同步 Rigidbody2D 位置，再恢复为 Dynamic/Interpolate 模拟。
- 线段定位只依赖 DrawLineFeature、startPoint 和已完成线段 Rigidbody2D，不再被 Actor 或车轮引用校验
  阻断。Actor 缺失时只记录错误；车轮缺失时不启动行驶，但已定位车身保留在 startPoint。
- Actor 进入或 Attempt Reset 时保持 Kinematic 且速度清零；引用可解析时，第一笔车身定位完成后再切为
  Dynamic 并 WakeUp。Disable 时恢复 Kinematic，避免禁用后的局部物理继续推进。
- 车身和两轮每个物理帧只覆盖 `linearVelocity.x`，保留 Y 速度、重力和碰撞响应；轮体
  `angularVelocity` 按目标速度和半径换算。stone 接触可以参与位置/速度解算，不再被下一帧父节点
  Transform 位移直接覆盖。
- 最大速度字段 `maxForwardSpeed` 已序列化到 prefab，当前为 `4`；`acceleration` 当前为 `1`。
- 暂停时不推进，结算/Disable/Attempt Reset 停止推进；Attempt Reset 恢复车轮初始位姿。
- 改动完全位于 Level22，本次不会改变已验收 DrawGame 关卡的运行时脚本或 prefab。

## 5. 假设与风险

### P1：任意车身形状的承载稳定性

- 假设：玩家画出的形状能在 startPoint 附近形成足够平整的 Actor 支撑面。
- 风险链：任意折线形状 -> Dynamic 车身受重力落到车轮 -> Actor 与子 BoxCollider2D 接触 -> 质量、
  摩擦、接触法线和旋转共同作用，
  可能使 Actor 滑落、卡入轮子或无法随车稳定前进。
- 当前缓解：车身只写横向速度，不逐帧改 Transform，也不覆盖纵向速度和角速度；Actor Ground mask
  已含 Draw Layer；不强制改 Actor Parent 或共享移动逻辑。
- 状态：`uncertain`，必须 PlayMode 覆盖短线、长线、V 形、陡边和近似平顶车身。

### P1：Rigidbody2D 轮速驱动与 stone 接触

- 事实：两轮为 Dynamic/Continuous Rigidbody2D，stone 为 Static Collider；脚本现在只写轮体速度。
- 风险链：每个 FixedUpdate 重设目标 X 速度 -> 轮子持续向 stone 施加运动趋势 -> 接触处可能爬坡、
  打滑、旋转或抖动，但不会再因 wheelsRoot Transform 强推而绕过接触解算。
- 当前决策：保持局部速度电机式控制，不新增力矩 PID 或共享车辆系统；先通过 PlayMode 调整质量、
  摩擦、加速度和最大速度。
- 状态：`uncertain`。

### P1：Dynamic 车身与独立轮组可能不同步

- 事实：车身和两只轮子是三个独立 Dynamic Rigidbody2D，没有 Joint2D；脚本分别写相同目标 X 速度。
- 风险链：任一刚体遇到不同碰撞约束 -> 三者实际位移不同 -> 车身和轮子发生视觉或碰撞分离。
- 当前决策：按本次明确要求保留 Dynamic 车身，不增加关节、父子关系或共享车辆系统。必须先通过
  PlayMode 观察正常路径和受阻路径，再决定是否需要局部 Joint2D 或以车身实际位移驱动车轮。
- 状态：`uncertain`。

### OBS：最大速度和加速度手感

- 当前 `4 / 1` 表示约 4 秒到达最大速度；是否能在倒计时、场景距离和成功门范围内形成合理节奏，
  尚无 PlayMode 时间证据。
- 调参只需修改 DLevel022Ctrl prefab 字段，不需要代码或数据迁移。

## 6. 缺失证据

- 未执行 DLevel_022 PlayMode：首笔前 Actor 静止、首笔后 Dynamic 下落、车身首次出现位置、Actor
  落座、双轮同步、到门成功和失败链未知。
- 未验证暂停恢复、手动 Reset、失败重建、Replay、退出关卡时的帧边界。
- 未验证 Dynamic/Continuous 轮体与 stone、地面、Actor 和车身的碰撞稳定性。
- 未验证 Android/iOS 横屏、30/60 fps、不同 Fixed Timestep 或低端设备表现。
- 当前运行时 `tbdrawleveldata.json` 和 `tbdrawlevelsort.json` 已包含 `20022 / DLevel_022`；配置表生成链
  与正式包入口仍未在本次验证。

## 7. 验证证据

- Unity 6000.0.59f2 自带 Roslyn，基于当前 Assembly-CSharp rsp 的独立全量编译返回
  `compile_exit=0`；输出只有项目既有 104 个 warnings。
- 当前 `Library/ScriptAssemblies/Assembly-CSharp.dll` 时间晚于本次脚本修改，Unity 会话已完成最新脚本
  编译；必须重新进入 PlayMode 后再做轮体与 stone 的行为验收。
- Unity Editor 日志确认目标脚本和 prefab 导入，DLevel_022 PrefabImporter 成功。
- Prefab YAML：169 个 anchor、重复数 0、本地非零引用缺失数 0；DLevel022Ctrl 脚本 GUID、Actor、
  startPoint、wheelsRoot、两个 wheel 引用和两项参数存在；两轮 Dynamic Rigidbody2D 均为质量 30、
  Continuous Collision Detection。
- 代码静态检查确认没有 `OnValidate/Reset/ExecuteAlways`、DB/Save/资源加载、全局事件或运行时实例化。

## 8. 手工验证清单

1. 首次进入：画线前确认 Actor 保持原位且 Rigidbody2D 为 Kinematic；画完第一笔后确认同帧切为
   Dynamic 并开始受重力影响。
2. 车身定位：画不同长度的近似平顶车身，确认完成帧中心出现在 startPoint，Rigidbody2D 为 Dynamic，
   随后受重力下落且不闪回原画框位置。
3. 加速：从静止开始记录约 4 秒内三个刚体的 X 速度变化，确认之后匀速；两轮 angularVelocity 应为
   负值并顺时针滚动，wheelsRoot 世界位置不应由脚本持续累加。
4. 承载：Dynamic 车身应落到双轮区域，Actor 应稳定落到车身并随车到门；覆盖 V 形、轻微斜面、
   较短和较长画法。
5. 接触：确认轮子撞到 stone 后由碰撞阻挡或沿表面运动，不再直接穿过；观察地面、Actor、车身接触
   是否穿透、抖动或把 Actor 异常弹飞。
6. 结果：验证成功门、失败、倒计时（若配置启用）、Reset、Replay 和退出重进。
7. 生命周期：暂停期间车辆完全停止，恢复后不跳步；Reset 后 Actor 应重新为 Kinematic、轮子回到
   初始位姿；再次画第一笔后 Actor 应再次切为 Dynamic。
8. 移动端：Android/iOS 横屏各验证至少一次，观察 30/60 fps 下同步和 GC/物理性能。

## 9. 回滚路径

- 完整回滚：恢复 DLevel022Ctrl 空实现，并删除 DLevel_022 prefab 该组件下新增的引用、Actor stripped
  Rigidbody2D 与速度字段；Actor 实例原有 `m_BodyType=1` override 可按关卡旧布局保留。
- 局部调参回滚：只恢复 `maxForwardSpeed` 和 `acceleration`，不影响脚本结构或序列化引用。
- Dynamic 行为回滚：把完成线段恢复为 Kinematic，并把横向 `linearVelocity` 驱动恢复为
  `Rigidbody2D.MovePosition`；无需修改共享 DrawLineFeature 或数据。
- 轮速控制回滚：恢复 wheelsRoot Transform 平移与 Transform.Rotate，并移除轮体缓存/速度/Reset 分支；
  该回滚会重新引入碰撞响应被强制 Transform 位移覆盖的问题，仅用于快速定位差异。
- 无配置、存档、资源地址、共享组件或数据迁移回滚。
