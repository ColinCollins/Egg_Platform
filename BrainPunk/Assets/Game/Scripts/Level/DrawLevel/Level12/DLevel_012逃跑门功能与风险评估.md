# DLevel_012 逃跑门功能与风险评估

## 1. 需求与原行为基线

- 需求：玩家与 DoorEnemy 的 X 轴距离不大于 2 时，DoorEnemy 向右跳出；DoorEnemy 碰到 Draw 后停止。
- 原 Prefab 已有玩家、`DLevel001Ctrl`、DoorEnemy 的 `DoorEnemyCtrl + Rigidbody2D`，以及 DoorEnemy
  碰到 Actor 后执行原门业务的 Layer 11 监听器。
- 原 `DLevel012Ctrl` 只有空 `Start/Update`，未绑定 Prefab；`DoorEnemyCtrl` 没有独立 Update，因此
  距离、跳跃、持续向右移动和 Draw 碰撞停止均没有运行时调用链。

## 2. 当前行为与影响范围

- DLevel_012 根新增并显式绑定 `DLevel012Ctrl`，距离按两个 Actor 世界坐标 X 的绝对差计算，阈值为 2。
- 触发前由控制器刷新 DoorEnemy 地面状态；触发后复用 `SetMoveInput(1)`、`TriggerJump()` 和
  `OnUpdate()`，不直接写跳跃高度或复制共享移动公式。
- DoorEnemy 新增第二个 `LayerCollisionDetector`，只监听 Draw 层。首次命中后清除输入并仅清零 X
  速度，不清零 Y 速度、不禁用刚体、不切 Kinematic。
- 原 Layer 11 监听器、`BaseItemExecutor` 及其 UnityEvent 保持不变；玩家碰门的原业务链不受本关
  Draw 停止逻辑调用。
- 影响文件：`DLevel012Ctrl.cs`、`DLevel_012.prefab`、LevelData/LevelSort 源表与运行时 JSON、
  本地结构/风险文档、中央演进记录和已有功能记录。
- 配置新增 `20012 / DLevel_012 / 跳水`，墨水上限 15、评级阈值 `0/2/5`，并把 Draw 排序第 18 项
  指向 `20012`；未修改共享 `ActorCtrl`、`DoorEnemyCtrl`、`LayerCollisionDetector`、
  `DrawLevelCtrl`、`GamePlayPanel`、存档、全局事件、analytics、SDK 和平台配置。

## 3. 风险项

| 风险 | 等级 | 当前控制 | 未验证缺口 |
| --- | --- | --- | --- |
| 触发前 DoorEnemy 地面状态未刷新，跳跃被忽略 | P1 | 距离判断前逐帧调用 DoorEnemy `OnUpdate()` 更新 Ground | PlayMode 首帧和低帧率顺序未验证 |
| 扩大原碰撞 Mask 导致 Draw 误执行门业务 | P1 | 保留原 Layer 11 监听器，新增独立 Layer 14 监听器 | 后续 Inspector 误改 Mask 仍需检查 |
| 碰 Draw 后仍横向滑动 | P1 | 清除 Actor 输入并立即把刚体 X 速度设为 0 | 动态/斜向 Draw 的接触冲量可能在后续物理步再次改变速度 |
| 停止时误冻结下落 | P1 | 只清零 X，保留 Y、重力和 Dynamic 刚体 | 不同线形上的最终停靠姿态未验证 |
| 暂停或结算后继续被控制器推进 | P1 | Update 检查 `IsPaused`、死亡和 Resolved；Disable 解除订阅 | 暂停恢复与成功渐隐窗口未做 PlayMode |
| Attempt Reset 残留门位置或速度 | P1 | 缓存并恢复局部 Transform、线速度、角速度和运行标记 | 物理步内 Reset、连续 Reset 未验证 |
| 两单位阈值手感或可解性不合适 | 中 | 阈值为 Prefab 序列化字段，可单关调整 | 玩家速度、跳跃弧线与玩家画线解法未试玩 |
| 移动端物理顺序差异 | OBS | 无平台分支、无每帧分配和资源加载 | Android/iOS 横屏真机未验证 |

## 4. 验证证据

- Unity 6000.0.59f2 自动完整编译完成；脚本已进入 `Assembly-CSharp.rsp`，日志未发现本脚本编译错误。
- DLevel_012 PrefabImporter 成功；161 个 YAML 锚点唯一，新增脚本和检测器引用静态完整。
- 两个 Luban 源表的 DLevel12 局部版本与两份运行时 JSON 已核对：仅新增 `20012` 并修改排序第 18 项；
  表格渲染通过且没有公式错误。
- 根控制器四个引用均非空；触发距离为 2；新增碰撞 Mask 为 16384（Draw），原 Mask 仍为 2048。
- 静态生命周期与风险关键字扫描通过：没有编辑器运行入口、DB/Save、实例化、直接 `Debug.Log`、
  全局事件或共享脚本修改。
- 未执行 Unity PlayMode、自动化物理场景、设备构建或 Android/iOS 真机验证。

## 5. 手工验收清单

- [ ] 玩家与 DoorEnemy 的 X 距离大于 2 时，DoorEnemy 保持原位和待机动画。
- [ ] 距离首次进入 2 以内时只触发一次跳跃，并持续向世界坐标右侧移动。
- [ ] 跳跃前没有 Draw 接触时，不会提前停止。
- [ ] DoorEnemy 首次碰到水平、斜向和动态 Draw 后立即停止横向驱动，Y 方向仍按物理运动。
- [ ] 玩家碰到 DoorEnemy 时，原门业务链仍只执行一次，不被 Draw 监听器抢占。
- [ ] 暂停/恢复、清线 Attempt Reset、失败重建、Replay 后状态和订阅无残留。
- [ ] 低帧率以及 Android/iOS 横屏设备验证触发距离、碰撞顺序和完整可解性。

## 6. 回滚路径

- 移除根节点 `DLevel012Ctrl` 和 DoorEnemy 上新增的 Draw 层 `LayerCollisionDetector`，保留原 Layer 11
  监听器及其 UnityEvent。
- 恢复 `DLevel012Ctrl.cs` 的空壳基线并回滚相关中文记录。
- 配置回滚需同步移除 `LevelData` 的 `20012` 行、恢复 `LevelSort` 第 18 项，并重新生成或回滚两份
  JSON；存档、资源地址、UI、analytics 和平台数据无需迁移。
