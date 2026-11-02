# Level124 风险评估

## 2026-08-18 行为等价精简风险复核

- **事实**：停用和销毁原本都重置事件并解绑 Actor 皮肤回调；停用专属门流程恢复和销毁专属 Tween 终止仍在原路径。
- **直接结论**：回调所有权、拖拽状态、皮肤切换、门时序和结算边界保持一致。
- **假设**：无新增运行时假设；共享方法只封装两个原有调用。
- **缺失证据**：尚未执行 Unity PlayMode 的反向拖拽、皮肤变更、停用/销毁、Reset/Replay 和 Android/iOS 回归，标记为 `uncertain`。
- **回滚**：将 `UnbindRuntimeCallbacks()` 内容内联回两个回调。

更新时间：2026-08-17

## 原有基线与受影响项

Level124 是 Level004 的独立 Prefab 副本。当前变更只影响 Level124 私有 Prefab 覆盖、私有控制器、关卡登记配置和本地记录；复用现有 Actor、出口门、出生门、TextMeshPro、DOTween 与 BaseLevelCtrl 成功链，不修改任何共享实现。

## 风险与处理

| 风险 | 处理与证据 | 状态 |
| --- | --- | --- |
| 横置无限符号被进门流程真实减数 | `isFiniteCounter` 将无限与有限状态完全分开；无限状态只播放 `-1` | 代码静态通过，PlayMode 待验收 |
| 转正后还能转回无限 | 接近正向阈值时立即吸附为 `0°` 并永久置位有限状态，后续输入入口直接返回 | 代码静态通过 |
| 旋转必须点到极细字形，操作困难 | 命中使用 TMP Renderer 世界边界并向四周扩展 `0.35` 世界单位 | 静态通过，手感待验收 |
| 移动端鼠标模拟和 Touch 双路触发 | 同一帧优先处理首个 Touch；存在 Touch 时不读取 Mouse | 代码静态通过，真机待验收 |
| 普通循环误触发成功彩带或销毁 Actor | 门事件只进入 Level124；普通流程不调用 `SuccessAnimCtrl` 或 `BaseLevelCtrl.OnActorTrigger2D` | Prefab/代码静态通过 |
| 显示 `1` 时又减为 `0`、弹 `-1` 并循环 | `remainingDoorCount <= 1` 的最终判断先于 `-1` 反馈，直接调用标准成功入口 | 代码静态通过 |
| Trigger 连续进入导致重叠异步流程 | 同时使用 `isDoorFlowPlaying`、flow version 和流程期间关闭 Trigger 三层门禁 | 代码/Prefab 静态通过 |
| 角色只隐藏主 Spine，皮肤或影子残留 | Alpha 同步覆盖 Actor Spine、动态 `SpriteRendererGroupAlpha` 和 Shadow | 引用/代码静态通过，动态皮肤待验收 |
| 关闭 Rigidbody 后初始门走出阶段无法移动 | 出口吸入 Tween 完成后先恢复初始 simulated、速度、缩放和旋转，再执行 Level124 私有初始门走出流程 | 代码静态通过 |
| Reset 后旧 await 回调继续解锁或改写新状态 | Reset 先清最终成功锁，再递增 flow version、Kill Tween、恢复角色状态和控制权；每个 await 后复核当前 flow | 代码静态通过，Reset 时序待验收 |
| 游戏暂停不改 timeScale，Tween 或 Spine 仍在后台播放 | 订阅 Pause/Resume，显式暂停角色吸入/走出 Tween、两扇门与角色 Spine、`-1` Tween；私有等待只在非暂停时累计，并在 Resume 后从当前阶段继续 | 控制边界静态通过，视觉衔接待验收 |
| 本关普通循环要求的 1.2 倍速只作用到部分环节 | `flowSpeedMultiplier` 同时控制出口门/初始门 TrackEntry、出口吸入阶段角色动画、吸入时长和门相关等待时长；Prefab 值为 `1.2`。初始门走出保留基线 `0.2s`，避免落点缩短 | 代码/Prefab 静态通过，实际节奏待验收 |
| 出口关门到入口开门停顿无法单独调节 | `exitCloseToEntranceOpenDelay` 已序列化暴露，Prefab 当前为 `0.22s`，按实际秒数等待且不再除以倍率 | 代码/Prefab 静态通过，实际节奏待验收 |
| 旧本地化 term 覆盖数字 | Prefab 已移除第二个 Tips 的 Localize；控制器运行时再次按类型禁用 | Prefab/代码静态通过 |
| 修改 124 时误伤 004 或共享门/角色 | 新脚本只由 Level124 Prefab 唯一引用；旧 KnockKnock GUID 在 124 为零引用，共享文件未修改 | Git/Prefab 静态通过 |
| Prefab 手工接线出现断引用 | `136/136` 个 YAML 定义唯一，所有无 GUID 的本地 fileID 引用均有定义；初始门根节点、Spine、控制器、门方法和类型各唯一 | 静态通过 |
| 配置覆盖工作区已有 Level123 等改动 | Artifact Tool 修改版与 JSON 仅追加 `10124` 和排序 `101`，不重排或回退既有记录；写回前确认源表长度和时间戳未变化，写回后重新导入验证 | 已写回并复核通过 |

## 验证证据

- Unity 6000.0.59f2 Roslyn 完整编译退出码 `0`，Level124 无诊断；编译输出只有工程既有 warning。
- Prefab 静态检查为 `136` 个唯一定义、`0` 个重复 fileID、`0` 个悬空本地引用。
- 初始门根 Transform 和同根 ActorSpineCtrl 绑定正确；Prefab 中流程倍率为 `1.2`，出口关门到入口开门的独立停顿为 `0.22s`。
- `Level124Ctrl` GUID、`OnActorEnterDoor`、`Level124Ctrl, Assembly-CSharp` 各唯一一次；旧 KnockKnock GUID 为零。
- 数字初始文本 `8`、位置 `(3.33, 2.29)`、角度 `88.161°` 和字号 `12.3` 保持原有 Prefab override。
- LevelData、LevelSort 已写回项目源表并重新用 Artifact Tool 导入；`10124 / Level_124`、`Level_999`、排序 `101 / 10124` 均唯一，目标行顺序正确，两份表的公式错误扫描均为 `0`。运行时 JSON 已解析并做唯一性检查。

## 未验证缺口

- 未运行前台 Unity PlayMode，不声明旋转手感、门 Trigger 实际距离、八次循环、动画和音频节奏已获策划验收。
- 未实测在出口渐隐、出口关门、门间停顿、初始门开门/走出/关门和最终成功动画各阶段执行 Pause、Reset、Replay 的画面衔接。
- 未验证手机触控、宽高比变化、低帧率以及角色换动态皮肤后的视觉完整性。
- Level124 结算/答案本地化键的实际文本资源为 `uncertain`。

## 回滚

优先通过 Level124 Inspector 调整吸附阈值、命中余量、`flowSpeedMultiplier`、`exitCloseToEntranceOpenDelay`、开门等待、角色入门时长、关门延迟和 `-1` 参数，不修改共享流程。完整撤回时删除 Level124 私有 Prefab、脚本、文档及配置中的唯一 `10124` 和排序 `101` 记录；Level004 和共享资源无需处理。

## 2026-08-26 增量：旧实例 teardown

- 原始基线在 Reset 和 OnDisable 中恢复角色与门状态，可能在 0.5 秒销毁等待窗口重新开放控制或改变画面。
- 当前 Reset/Replay 只停止流程并禁用 Trigger/输入；释放 GameplayPanel Block 属于跨实例共享 UI 清理，继续保留。
- Level123 的共享按钮恢复、Level128 的协程取消/提示隐藏经审计属于跨实例清理，不按无效复位删除；Level126 仅删除旧实例选中火柴视觉复原。
- 编译为 0 error；未验证临时 Disable/Enable Level124 根节点的非主线用法，标记为 `uncertain`。
- 回滚时恢复 `OnGameReset()` 的 `InitializeCounter()/CancelDoorFlow(true)` 与门 Idle 写入，并移除 Replay teardown 订阅。
