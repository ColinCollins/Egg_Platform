# DLevel_011 小偷延迟移动与钥匙跟随功能及风险评估

## 1. 需求与背景

- 需求：第一笔有效画线完成后，小偷等待可配置时间再向右移动；当前 Prefab 配置为 `0.5` 秒。
- 需求：小偷触碰钥匙时，启用钥匙上的 `FollowTarget`，让钥匙跟随小偷离开。
- 用户补充边界：不得改写 Key 原有 Player Trigger 内容；应额外挂一个 Trigger 处理小偷。
- 参考基线：`DLevel_001` 的首笔完成后 Actor 自动向右移动结构。该关结构记录明确标注整关
  尚未验收，因此这里只把源码、Prefab 和生命周期作为本地证据，验收状态仍为 `uncertain`。

## 2. 修改前行为基线

- `DLevel_011.prefab` 根节点已有 `DrawLevelCtrl`、`DrawLineFeature`、`DLevel001Ctrl` 和
  `DelayAutoBeginDrawHandle`；`DLevel001Ctrl` 只绑定玩家 Actor，第一笔后不会驱动 enemy。
- `DLevel11Ctrl.cs` 只有空的 `Start()` 和 `Update()`，没有实现 Draw 生命周期接口。
- Key 实例已经有一个禁用的 `FollowTarget`，目标指向 enemy，偏移为 `(0, 0.3)`，平滑速度为 `10`，
  但没有任何运行时入口会启用它。
- Key 原 Prefab 的 `OnTrigger2DHandle` 只接受 `Player`，并调用原 `TriggerExecutor.OnTriggerExecute`；
  DLevel_011 没有改写该 Trigger。
- enemy Actor 实例把 Actor Prefab 继承的 `spineCtrl`、`pushCollider` 清空，并把
  `whatIsGround` 覆盖为 `0`；直接调用 `ActorCtrl.OnUpdate()` 会存在空引用并无法进入正常地面步行动画。

## 3. 修改后行为

1. `DLevel11Ctrl` 实现 `IDrawStrokeCompletedHandler`。第一笔有效线完成后只启动一次倒计时，
   `enemyMoveDelay` 当前为 `0.5` 秒；后续笔画不重复启动。
2. 倒计时和移动只在非暂停、未结算且 enemy 未死亡时推进；暂停会清除本帧移动输入，恢复后
   继续剩余倒计时或继续向右移动。
3. 移动复用 `ActorCtrl.SetMoveInput(1f)` 与 `ActorCtrl.OnUpdate()`，速度继续由 enemy 实例当前
   `moveSpeed=8` 决定，不在 Level11 脚本中复制物理、朝向或动画逻辑。
4. Key 实例额外挂第二个 `OnTrigger2DHandle`，只接受 `enemy`，并调用
   `DLevel11Ctrl.OnEnemyEnteredKeyTrigger(Collider2D)`；原 Player Trigger、过滤标签、UnityEvent
   和 `TriggerExecutor` 内容保持不变。
5. enemy 命中新 Trigger 后，先停止 Key 的悬浮 Tween，再把 `FollowTarget` 目标明确设为 enemy
   并启用跟随；幂等门禁阻止本关脚本重复处理。
6. enemy Actor 删除 `spineCtrl`、`pushCollider` 和 `whatIsGround` 三个错误 override，恢复继承
   `Actor.prefab` 的现有引用与地面检测。共享 Actor Prefab 未修改。
7. 关卡结算、组件禁用或当前尝试 Reset 时停止 enemy 输入。原地 Reset 还会恢复 Key 初始位置、
   跟随目标、禁用状态和悬浮 Tween；当前整关重建流程也会从 Prefab 初态重新开始。

## 4. 影响文件与系统

- `Assets/Game/Scripts/Level/DrawLevel/Level11/DLevel11Ctrl.cs`
- `Assets/Game/Prefabs/DrawLevel/DLevel_011.prefab`
- 复用系统：`DrawLevelCtrl`、`IDrawStrokeCompletedHandler`、`ActorCtrl`、`OnTrigger2DHandle`、
  `FollowTarget`、`MoveFloatHandle` 和 Key 原 `TriggerExecutor`。
- 未修改：共享 `Actor.prefab`、共享 `Key.prefab`、`DrawLevelCtrl`、`ActorCtrl`、
  `GamePlayPanel`、全局事件、Physics2D Layer Matrix。

## 5. 数据、资源、UI、事件与平台流

- DB/存档/Luban：无新增读取、写入或字段；既有 Draw 胜利进度链不变。
- 资源：仍随 `DLevel_011.prefab` 加载，没有运行时资源请求或新地址。
- UI：不直接操作 HUD、暂停、胜利或失败界面。
- 事件：只使用 DrawLevelCtrl 已有本地 Handler 分发和 Key 上的两个局部 UnityEvent；没有新增
  `PlayEvents`、Bear 全局事件或跨关订阅。
- Analytics/广告/IAP/SDK：无。
- Android/iOS：共用相同的 2D 物理和时间逻辑，没有平台分支；横屏实际触控与帧率表现未验证。

## 6. 风险项

| 风险 | 等级 | 当前控制 | 剩余缺口 |
| --- | --- | --- | --- |
| 修改 Key 原 Trigger 导致玩家取钥匙回归 | 高 | 原 Trigger 保持 `Player -> OnTriggerExecute`，enemy 使用新增第二 Trigger | 仍需 PlayMode 验证玩家完整取钥匙、开门和成功链 |
| enemy Actor 空引用导致首帧停止 | 高 | 删除三个错误实例 override，恢复 Actor Prefab 已有引用与地面检测 | 需 PlayMode 观察实际 walk、地面检测与障碍碰撞 |
| 暂停或结算后 enemy 继续移动 | 中 | 每帧检查 `IsPaused`、`IsOutcomeLocked`、`IsDied`，结算/禁用时清输入 | 需验证暂停恢复、失败等待、成功演出 |
| Key 悬浮 Tween 与 FollowTarget 同时写 Transform | 中 | enemy 命中时先 `MoveFloatHandle.StopMove()` | 需低帧率下观察命中首帧是否跳位 |
| 玩家与 enemy 同一物理步同时进入 Key | 中 | 两个 Trigger 过滤标签并分别执行，互不改写 | Unity 回调先后与最终归属仍为 `uncertain`，需构造同帧碰撞验证 |
| Reset 后 Key 状态残留 | 中 | 本地 Reset 恢复位置、旋转、跟随初态和悬浮；整关重建天然恢复 | 玩家原取钥匙链已开始后的原地 Reset 不作为本次新增行为保证 |
| 移动端性能 | 低 | 每帧仅驱动一个 enemy Actor；Key 只在命中后执行一个 LateUpdate，无扫描/实例化 | Android/iOS 60 fps 与后台恢复未验证 |

## 7. 验证证据

- Unity 6000.0.59f2 生成的 `Assembly-CSharp.rsp` 通过 Roslyn 全量编译：`0 error`；输出为项目
  既有 warning，没有 warning 指向 `DLevel11Ctrl.cs`。
- Unity Editor 已成功导入最终 `DLevel_011.prefab`，Editor.log 未出现该 Prefab 导入错误。
- Prefab 静态校验：Unity 最终规范化后的 140 个 YAML 锚点全部唯一；140 个非零本地引用全部能解析；脚本 GUID、
  enemy Actor、Key FollowTarget 与两个 Trigger 的 UnityEvent 目标均存在。
- Key 共享 Prefab 静态确认原 Trigger 仍为 `targetTag: Player` 且方法为 `OnTriggerExecute`；
  DLevel_011 对该原 Trigger 的 override 数量为 0，新增 enemy Trigger 数量为 1。
- 静态扫描未发现本关新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB、
  `PlayEvents`、全局事件或直接 `Debug.Log`。
- 未执行 Unity PlayMode、真实触控、Android/iOS 设备或完整关卡可解性验证。

## 8. 手工验收清单

- [ ] 首次进入后未画线时，玩家与 enemy 都保持当前初态，Key 继续原悬浮。
- [ ] 第一笔完成后玩家沿既有 DLevel001Ctrl 行为移动；enemy 等待约 `0.5` 秒再向右移动。
- [ ] 暂停发生在等待期间时不偷跑；恢复后继续剩余等待。
- [ ] enemy 触碰 Key 时，Key 不执行玩家取钥匙/开门/销毁链，只跟随 enemy 离场。
- [ ] 玩家先触碰 Key 时，原音效、缩放、淡出、跳向门、开锁和销毁链保持原行为。
- [ ] 构造玩家与 enemy 同一物理步触发 Key，确认策划期望的归属。
- [ ] 失败、手动 Reset、Replay 后 enemy 和 Key 都恢复初态，第一笔仍能重新触发。
- [ ] 成功/失败锁定后 enemy 不再被本关脚本驱动。
- [ ] Android 与 iOS 横屏下验证移动、Trigger、低帧率命中和切后台恢复。

## 9. 回滚路径

- 从 `DLevel_011.prefab` 根移除 `DLevel11Ctrl`，从 Key 实例移除新增的 enemy 专用
  `OnTrigger2DHandle`，并删除 `DLevel11Ctrl.cs`，即可恢复“小偷不移动、FollowTarget 不启用”的基线。
- 若必须完整恢复任务前 enemy 配置，可重新添加 `spineCtrl=null`、`pushCollider=null` 和
  `whatIsGround=0` 三个 override；这会重新引入 `ActorCtrl.OnUpdate()` 空引用风险，不建议作为
  可运行版本使用。
- 共享 Key、Actor、Draw 控制器、配置和存档均无需迁移或回滚。

## 10. 2026-08-03 多 TipsObject 兼容补充

- 原行为基线：共享 `DrawLineFeature` 只能控制一个 `tipsObject`，DLevel_011 的第二份提示对象不会
  随现有提示入口统一显隐或在 Attempt Reset 时恢复。
- 修改后：原 `tipsObject` 字段与引用完整保留；第二份现有提示对象配置到新增可选列表。Show/Hide
  会同时处理两者，Reset 则按对象恢复各自的初始 `activeSelf`，不会把主对象状态覆盖到附加对象。
- 影响文件为共享 `DrawLineFeature.cs` 与 `DLevel_011.prefab`；不修改 DLevel11Ctrl、Key 两个 Trigger、
  Actor、UI、DB/存档、Luban、资源加载、全局事件、SDK 或平台分支。
- 风险等级低：每次显隐或 Reset 仅遍历关卡配置的少量引用；空项与重复项可安全处理。剩余缺口是
  PlayMode 中两份提示的视觉时序、连续显隐、清线/失败 Reset、Replay 和移动端横屏尚未验证。
- 验证：Assembly-CSharp 全量编译 `0 error`；20 个既有 DLevel 主提示引用全部非空；DLevel_011
  仅有 1 个附加提示，164 个 YAML 锚点唯一且本地引用完整。
- 回滚：删除 DLevel_011 的附加列表项并回滚共享列表控制即可；原主提示引用不需要恢复。
