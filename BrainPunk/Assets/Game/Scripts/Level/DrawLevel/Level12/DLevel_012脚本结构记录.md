# DLevel_012 脚本结构记录

## 1. 基本信息

- 关卡：DLevel_012
- 记录日期：2026-08-03
- 需求：玩家与 `DoorEnemy` 的世界坐标 X 轴距离不大于 2 时，门敌人向右跳出；门敌人首次碰到
  Draw 层后停止水平移动。
- 参考基线：`DLevel001Ctrl` 的 Actor 逐帧驱动与 Draw 生命周期清理、DLevel_011 的第二 Actor
  局部驱动结构、现有 `LayerCollisionDetector` 碰撞过滤方式。
- 选择理由：本关已复用 `DLevel001Ctrl` 驱动玩家；`DoorEnemyCtrl` 继承 `ActorCtrl` 但没有独立
  `Update`，与 DLevel_011 的第二 Actor 一样需要关卡控制器调用公开移动 API 和 `OnUpdate()`。
- 配置：`LevelData.xlsx` 的 `draw_level` 表新增 `20012 / DLevel_012 / 跳水`，`LevelSort.xlsx` 的
  `drawLevelSort` 第 18 项切换为 `20012`；对应运行时 JSON 同步生成。

## 2. 最终脚本与组件树

```text
DLevel_012
├─ DrawLevelCtrl                         # 共享 Draw 关卡生命周期与结果
├─ DrawLineFeature                      # 共享画线功能
├─ DLevel001Ctrl                        # 第一笔有效画线后驱动玩家向右移动
├─ DLevel012Ctrl                        # 距离触发、DoorEnemy 驱动、暂停/结算/Reset 清理
├─ Actor
│  └─ ActorCtrl                         # 玩家，由 DLevel001Ctrl 驱动
└─ Level12/DoorEnemy
   ├─ DoorEnemyCtrl                     # 复用 Actor 跳跃、移动与动画
   ├─ Rigidbody2D + PolygonCollider2D   # 真实跳跃和碰撞
   ├─ LayerCollisionDetector            # 既有 Actor 层监听，保留原门业务链
   └─ LayerCollisionDetector            # 新增 Draw 层监听，仅通知 DLevel012Ctrl 停止水平移动
```

## 3. 类职责、接口与边界

| 类/组件 | 继承/接口 | 当前职责 | 不负责事项 |
| --- | --- | --- | --- |
| `DLevel012Ctrl` | `MonoBehaviour`、`IDrawStrokeCompletedHandler`、`IDebuger` | 计算 X 距离、驱动 DoorEnemy、订阅 Draw 碰撞、处理暂停/结算/Attempt Reset | 不判断成功、不修改玩家移动、不加载资源、不派发全局事件 |
| `DLevel001Ctrl` | 既有本关组件 | 第一笔完成后驱动玩家向右移动 | 不知道 DoorEnemy 跳跃和碰线停止 |
| `DoorEnemyCtrl` | `ActorCtrl` | 复用跳跃、水平移动、地面检测和 DoorEnemy 动画 | 不自行逐帧更新，不持有关卡状态 |
| Draw 层 `LayerCollisionDetector` | 共享碰撞组件 | 只把 Layer 14 的碰撞进入回调给本关控制器 | 不处理 Actor 碰门业务 |
| 既有 Actor 层 `LayerCollisionDetector` | 共享碰撞组件 | 保留 Layer 11 碰撞后执行原 `BaseItemExecutor` 链 | 不参与 Draw 停止逻辑 |

本次没有把单关行为写入共享 `ActorCtrl`、`DoorEnemyCtrl`、`DrawLevelCtrl` 或 `GamePlayPanel`，也没有
新增 `PlayEvents` 或 Bear 全局事件。

## 4. 关键生命周期与调用流

1. PlayMode 启用时，`DLevel012Ctrl` 校验四个显式引用，缓存 DoorEnemy 初始局部 Transform 和刚体速度，
   订阅新增 Draw 碰撞监听器与 `DrawLevelCtrl.Resolved`。
2. 距离触发前，控制器保持 DoorEnemy 的水平输入为 0，并逐帧调用 `DoorEnemyCtrl.OnUpdate()`；这一步
   用于刷新共享 Actor 的地面状态，确保后续 `TriggerJump()` 能识别当前地面。
3. 当 `abs(Player.x - DoorEnemy.x) <= 2` 时，只触发一次：设置向右输入 `1`，调用
   `TriggerJump()`，随后继续通过 `OnUpdate()` 写入向右速度和动画状态。
4. DoorEnemy 跳出后，控制器每帧保持向右输入；暂停、Actor 死亡或关卡已结算时不再推进。
5. DoorEnemy 首次碰到 Layer 14 Draw 时，新增 `LayerCollisionDetector` 回调控制器；控制器清除输入并把
   `Rigidbody2D.linearVelocityX` 设为 0，保留 Y 速度、重力和后续落地物理。
6. `OnDrawAttemptReset()` 恢复初始局部位置、旋转、线速度和角速度，并清除触发/停止/结算标记。
7. 禁用时解除两个局部订阅并清除 DoorEnemy 输入，避免失败重建、Replay 或退出后残留回调。

## 5. Prefab 序列化绑定

| 宿主 | 字段/配置 | 当前值 |
| --- | --- | --- |
| DLevel_012 根 | `DLevel012Ctrl.drawLevelCtrl` | 根 `DrawLevelCtrl` |
| DLevel_012 根 | `DLevel012Ctrl.actor` | Player 的 `ActorCtrl` |
| DLevel_012 根 | `DLevel012Ctrl.doorEnemy` | `Level12/DoorEnemy` 的 `DoorEnemyCtrl` |
| DLevel_012 根 | `DLevel012Ctrl.drawCollisionDetector` | DoorEnemy 上新增的第二个监听器 |
| DLevel_012 根 | `triggerHorizontalDistance` | `2` |
| DoorEnemy | 新监听器 `targetLayerMask` | `16384`，即 Layer 14 Draw |
| DoorEnemy | 既有监听器 `targetLayerMask` | `2048`，即 Layer 11，保持原业务事件不变 |

## 6. 数据、资源、UI、事件与平台流

- Luban/运行时配置：新增 Draw 关卡 `20012`，资源路径为 `DLevel_012`，墨水上限 15，评级阈值
  `0/2/5`；Draw 排序第 18 项指向 `20012`。本关脚本不直接读写 DB 或存档。
- 资源：只复用 Prefab 已有 Actor、刚体、碰撞体和动画引用；无加载、实例化或地址变化。
- UI：不修改 HUD、弹窗或 `GamePlayPanel`。
- 事件：只使用 `UnityEvent<Collision2D>` 局部监听和 `DrawLevelCtrl.Resolved` 局部 C# 事件。
- Analytics/SDK/原生覆盖层：不涉及。
- 平台：Android/iOS 共用 Unity Physics2D 与相同代码路径，没有平台分支。

## 7. 相对参考基线的差异与结构复盘

- DLevel001 只有一个 Actor；本关增加 DoorEnemy 的第二条逐帧驱动链，但仍通过 `ActorCtrl` 现有公开
  API，不修改共享 Actor。
- DoorEnemy 原有 `LayerCollisionDetector` 已拥有 Actor 碰门业务事件，不能扩大其 LayerMask；本关在
  同一宿主上追加一个只监听 Draw 的检测器，把两类碰撞所有权隔离。
- 结构复盘结论：当同一刚体已有稳定碰撞业务、单关又需要另一 Layer 语义时，可优先使用第二个局部
  检测器而不是改写原 UnityEvent。当前只有 DLevel_012 一关证据，保留为观察候选，不上提新共享抽象，
  不批量迁移其它关卡。

## 8. 验证证据与缺口

- Unity 6000.0.59f2 自动完整重编完成；`Assembly-CSharp.rsp` 已包含 `DLevel012Ctrl.cs`，日志未出现
  本脚本 `error CS`。
- `LevelData.xlsx` 和 `LevelSort.xlsx` 的 DLevel12 局部版本已检查并渲染；运行时 JSON 只包含
  局部暂存版本只包含 `20012` 数据行和排序第 18 项变化。
- `DLevel_012.prefab` 由 PrefabImporter 成功导入；脚本 GUID、四个显式引用和两个互不重叠的
  LayerMask 已静态核对。
- Prefab 当前 161 个 YAML 锚点均唯一；新增脚本 GUID 仅绑定一次，新增检测器引用完整。
- 静态扫描未发现 `OnValidate`、`Reset`、编辑器执行属性、DB/Save、Instantiate、全局事件或直接
  `Debug.Log`。
- `uncertain`：尚未执行 PlayMode；2 单位触发时机、向右跳跃弧线、碰到不同角度/动态 Draw 后的停靠、
  与玩家同物理步碰撞的业务顺序、Attempt Reset、失败重建、Replay 仍需实机观察。
- `uncertain`：Android/iOS 横屏低帧率下的 Physics2D 接触顺序和最终关卡可解性尚未验证。

## 9. 回滚路径

1. 从 DLevel_012 根移除 `DLevel012Ctrl` 组件。
2. 从 DoorEnemy 移除目标为 Layer 14 Draw 的第二个 `LayerCollisionDetector`，保留原 Layer 11 监听器。
3. 将 `DLevel012Ctrl.cs` 恢复为空壳或删除本关实现，并同步回滚本地及中央记录。

共享 Actor、DoorEnemy、Draw 系统、原门业务事件、存档和资源均无需迁移。配置回滚需同步删除
`LevelData` 的 `20012` 行并恢复 `LevelSort` 第 18 项及两份运行时 JSON，避免源表与生成物不一致。
