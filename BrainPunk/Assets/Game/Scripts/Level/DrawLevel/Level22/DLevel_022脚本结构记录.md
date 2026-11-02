# DLevel_022 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame 第 22 关
- 记录日期：2026-08-03
- 当前状态：首笔车身定位已与 Actor/车轮校验拆分，双轮加速滚动与车辆同步前进已实现；
  静态检查和 Assembly-CSharp 独立全量编译通过，PlayMode 与移动设备验收未完成
- 需求依据：`DLevel022Ctrl.cs` 原始类注释，以及
  `Assets/Game/Prefabs/DrawLevel/PixPin_2026-08-03_17-00-17.png` 关卡参考图
- 参考基线：DLevel_007 的“取得并定位已完成运行时线段”边界，以及 DLevel_021 的
  “关卡局部 FixedUpdate 驱动、暂停/Disable 清理”边界
- 主要脚本：`DLevel022Ctrl.cs`
- 关卡 prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_022.prefab`

## 2. 最终组件结构

```text
DLevel_022
├─ DrawLevelCtrl
├─ DrawLineFeature
├─ DelayAutoBeginDrawHandle
├─ DLevel022Ctrl : IDrawStrokeCompletedHandler, IDebuger
├─ Actor（nested prefab，Player Tag，Rigidbody2D 初始 Kinematic）
├─ Scene
│  ├─ startPoint
│  └─ wheels
│     ├─ Sp_hx_mtlz_01 + Dynamic Rigidbody2D + CircleCollider2D
│     └─ Sp_hx_mtlz_01 (1) + Dynamic Rigidbody2D + CircleCollider2D
└─ 运行时 RuntimeStroke_1
   ├─ LineRenderer
   ├─ Rigidbody2D（完成后由本关保持 Dynamic）
   └─ DrawLineFeature 生成的子 Collider2D
```

## 3. 类职责与局部边界

| 类/组件 | 职责 | 不负责事项 |
| --- | --- | --- |
| `DrawLevelCtrl` | 初始化本关、分发 StrokeCompleted/Reset、管理暂停和一次性结算 | 不处理车辆位置或速度 |
| `DrawLineFeature` | 创建、居中、登记并最终销毁运行时线段 | 不决定线段的车辆用途 |
| `DLevel022Ctrl` | 接管第一笔、定位车身、释放 Actor 动态物理、计算加速度、同步移动并旋转车轮、处理暂停/结算/Reset 清理 | 不改 ActorCtrl、成功门、UI、存档或共享画线实现 |

- 本关不新增 `PlayEvents` 或其它全局事件。
- 本关通过 `DrawLevelCtrl.Resolved` 的局部 C# 事件停止推进；Reset 继续使用
  `IDrawStrokeCompletedHandler.OnDrawAttemptReset()`。
- 运行时线段仍归 `DrawLineFeature` 所有；控制器只在当前尝试中持有引用，不销毁线段。

## 4. 生命周期与关键调用

1. `Awake/OnEnable` 解析根节点 `DrawLineFeature`、`DrawLevelCtrl`、Actor Rigidbody2D、
   `Scene/startPoint` 与 `Scene/wheels`，缓存轮组局部位姿、两轮局部旋转和 CircleCollider2D
   世界半径；Awake 把 Actor 明确保持为 Kinematic 并清零速度。
2. 第一笔完成后，`DrawLevelCtrl` 调用
   `DLevel022Ctrl.OnDrawStrokeCompleted(DrawStrokeResult)`。
3. 控制器先只校验 `DrawLineFeature` 和 `startPoint`，再通过
   `TryGetCompletedStrokeObject(1)` 取得当前运行时线段及 Rigidbody2D；该定位阶段不依赖 Actor
   或车轮引用。
4. 参考 DLevel7，线段先暂停 Physics2D 模拟、清零速度并切为 Dynamic/Interpolate，再把运行时
   线段 Transform 和 Rigidbody2D 中心同步设置到 `startPoint.position`，最后恢复模拟。DrawLineFeature
   已在完成回调前把线段包围盒中心归到 Transform 原点，因此不重复改写 LineRenderer 或子 Collider。
5. 车身完成定位后，Actor 和车轮分别校验：Actor 存在时清零速度、切为 Dynamic 并 WakeUp；缺失时
   只记录错误，不撤销线段定位。车轮完整时才启动持续行驶；缺失时车身仍保留在 startPoint。
6. 每个 `FixedUpdate` 以 `acceleration` 从 0 线性增加当前速度，达到
   `maxForwardSpeed` 后保持匀速；车身与两只 Dynamic 轮体只覆盖各自 `linearVelocity.x`，保留
   Y 速度、重力和碰撞响应，不再移动 wheelsRoot Transform。
7. 单轮 `angularVelocity` 由 `-speed / radius * Rad2Deg` 计算；向右行驶对应顺时针旋转，半径取
   各轮 CircleCollider2D 半径和 lossyScale 的最大轴。不再直接调用轮子 Transform.Rotate。
8. `DrawLevelCtrl.IsPaused` 时不推进。结算、Disable 或 Attempt Reset 时停止并清零本地速度；
   Disable/Attempt Reset 把 Actor 恢复为 Kinematic；Stop 清零车身和两轮线/角速度，Attempt Reset
   暂停两轮模拟后还原轮组及两轮局部位置/旋转，再恢复原 simulated 状态。线段由 DrawLineFeature
   先行统一销毁。

## 5. Prefab 序列化契约

| 字段 | 当前绑定/默认值 | 注意事项 |
| --- | --- | --- |
| `drawLineFeature` | 根 `DrawLineFeature`（fileID `900000000000000002`） | 必填 |
| `drawLevelCtrl` | 根 `DrawLevelCtrl`（fileID `900000000000000001`） | 推荐必填；缺失时不能读取暂停或结算事件 |
| `actorRigidbody` | Actor Rigidbody2D（fileID `1138963634036635986`） | 已显式绑定；缺失仍不阻止车身定位，但 Actor 不会切 Dynamic |
| `startPoint` | `Scene/startPoint`（fileID `6716555359068394885`） | 必填；世界位置是车身中心出生点 |
| `wheelsRoot` | `Scene/wheels`（fileID `5932873882439646080`） | 必填；只作为层级解析和 Reset 基准，运行时不直接移动 |
| `wheels[0..1]` | 两个 `Sp_hx_mtlz_01` Transform | 必须恰好两项，且均带 Dynamic Rigidbody2D 与 CircleCollider2D |
| `maxForwardSpeed` | `4` 世界单位/秒 | 用户可在 Inspector 调整；达到后匀速 |
| `acceleration` | `1` 世界单位/秒² | `0` 表示首个物理帧直接使用最大速度 |

## 6. 数据、资源、UI 与事件流

- 数据/存档：脚本不访问 DB、Luban、Save 或 PlayerPrefs。
- 资源：只使用 DLevel_022 已加载的 prefab、运行时线段和两只车轮；无新增资源加载或实例化。
- UI：无直接访问；画线输入和结算 UI 继续由共享 DrawGame 流程管理。
- Actor：不改 ActorCtrl，也不直接移动或改 Parent；只控制本关 Actor 实例 Rigidbody2D 的
  Kinematic/Dynamic 边界。Actor 和 Dynamic 车身能否稳定落到车轮并共同前进，依赖现有 Physics2D
  接触、Draw 层 Ground 配置和玩家画出的车身形状。
- 成功门：不改成功条件；Actor 进入现有成功门后仍由 DrawLevelCtrl 处理。
- Analytics/SDK/平台：无直接调用或配置修改。

## 7. 相对基线的差异与结构复盘

- 相对 DLevel_007，本关复用“暂停线段物理后直接写运行时线段 Transform 到 startPoint，再恢复模拟”的
  定位方式；但在 StrokeCompleted 同步回调内立即完成，不等待 Actor 动画、不隐藏/渐显、不施加冲量，
  随后把线段作为受重力和碰撞影响的 Dynamic 车身，只控制其横向目标速度。
- 定位依赖已缩小为 DrawLineFeature、startPoint 和线段 Rigidbody2D；Actor 释放与车轮驱动是后续独立
  步骤，避免无关引用缺失导致玩家画好的车身仍留在画框。
- 相对 DLevel_021，本关 FixedUpdate 不做物理查询，只计算一次目标速度并写入一个 Dynamic 车身和
  两个 Dynamic 轮体；轮体缓存数组在 Awake 建立，循环无集合分配。
- 结构决策：当前只有 DLevel_022 需要“玩家画出的车身 + 场景双轮同步行驶”，继续保留关卡局部
  控制器，不扩展 `DrawStrokeResult`、`DrawLineFeature` 或共享 Actor。
- 候选观察：当前车身和两轮是三个独立 Dynamic Rigidbody2D，没有 Joint2D。若 PlayMode 证实碰撞时
  分离，再在本关评估局部 HingeJoint2D/WheelJoint2D；当前不上提共享车辆抽象。

## 8. 验证证据与缺口

- Unity 6000.0.59f2 自带 Roslyn、当前 Assembly-CSharp rsp 全量编译：`compile_exit=0`；输出只有
  项目既有 104 个 warnings，本次脚本无编译错误。
- 检查时 `Library/ScriptAssemblies/Assembly-CSharp.dll` 时间晚于本次脚本修改，当前 Unity 会话已完成
  最新脚本编译；轮体碰撞和整关行为仍需重新进入 PlayMode 验证。
- Unity Editor 日志确认 `DLevel022Ctrl.cs` 与 `DLevel_022.prefab` 已导入，PrefabImporter 成功。
- Prefab 静态检查：169 个 YAML anchor 唯一，本地非零 fileID 引用缺失数为 0；脚本 GUID、Actor、
  startPoint、wheelsRoot 和两轮引用存在，两轮 Dynamic Rigidbody2D 均为质量 30、Continuous。
- 未执行 PlayMode，因此短/长/高低车身形状的定位、Dynamic 车身下落与承载、车轮/车身视觉同步、
  Actor 首笔前 Kinematic 保持与首笔后 Dynamic 下落、经过地面起伏、碰撞稳定性、到门成功、
  失败/Reset/Replay、暂停恢复仍为 `uncertain`。
- Android/iOS 横屏、低帧率和真机性能未验证，状态为 `uncertain`。

## 9. 回滚路径

- 恢复 `DLevel022Ctrl.cs` 的空实现或移除根节点上的该组件，并删除 prefab 中新增的引用、Actor
  stripped Rigidbody2D 与速度字段，
  即可回到“画线正常生成但不定位、不动车”的原始基线。
- 无共享脚本、配置、存档、资源地址或数据迁移需要回滚；用户已有 DLevel_022 美术布局可保留。
