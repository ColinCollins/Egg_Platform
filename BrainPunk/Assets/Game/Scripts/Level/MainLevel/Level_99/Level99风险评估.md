# Level099 风险评估

## 2026-08-18 行为等价精简风险复核

- **事实**：停用与销毁原本重复执行同一 `activeSequence.Kill(false)` 和引用清空；姿态恢复条件未改变。
- **直接结论**：Tween 终止方式、停用视觉复位和销毁边界保持一致，不影响计数、金币反馈、失败/成功或配置。
- **假设**：无新增运行时假设；公共方法不包含额外状态写入。
- **缺失证据**：尚未执行 Unity PlayMode 的连续撞击、停用/销毁、暂停/重开及真机反馈回归，标记为 `uncertain`。
- **回滚**：重新内联两行序列停止逻辑。

更新时间：2026-08-11

## 原有基线与受影响项

Level099 从 Level089 场景结构发展而来，但玩法不再使用四按钮顺序和石柱纵向下降。当前变更只影响 `Level_099.prefab`、Level99 三个私有脚本、关卡登记配置与本地记录；复用 `ActorCtrl`、`BaseLevelCtrl`、`Raycast2DExecutor`、DOTween、出口门和现有金币音效。MMFeedbacks 数据仍保留在本关 Prefab 便于回退，但已从执行链移除。

## 风险与处理

| 风险 | 处理与证据 | 状态 |
| --- | --- | --- |
| 角色只靠近石柱或从右侧接触也累计五秒 | 同时要求右移输入、角色中心在柱左侧、Y 范围重叠、`ActorCtrlable`、Actor 既有 Stuff 推物射线命中，并由前置 `pushCollider.Distance(pillarCollider)` 精确确认目标是本关石柱 | 静态通过，PlayMode 待验收 |
| Actor 根 Collider 与石柱间距门禁导致永远无法完成 | Actor 前置 `pushCollider` 会先挡住石柱，使根 PolygonCollider2D 稳定保留约 `0.267` 间距；本关已移除原 `0.12` 根 ColliderDistance 门禁，改为对前置 `pushCollider` 与目标石柱使用 `0.02` 数值容差 | 根因与代码静态确认，PlayMode 待验收 |
| 命中其它 Stuff 时远程累计推柱 | `IsPushStuff` 只作 Actor 原生推物前置条件，最终还要求启用中的 `pushCollider` 与序列化 `pillarCollider` 重叠或距离不超过 `0.02`，问号块等同层对象不能通过目标校验 | 静态通过，PlayMode 待验收 |
| 推物检测逐帧抖动导致连续时间被清零 | 提供 `0.1s` 检测宽限；超过宽限才清零连续计时 | 静态通过，低帧率待验收 |
| 前五秒被角色刚体直接推动 | 石柱初始化为 Kinematic，脚本在五秒后才用 `MovePosition` 推进 | Prefab/代码静态通过 |
| 石柱中心过界后柱脚仍压平台，切 Dynamic 时整根倾倒 | 释放判定改为石柱 Collider 左边缘完全越过平台右界加 `0.02`；释放前保持 Kinematic，释放时恢复初始角度、冻结旋转并清零线速度/角速度 | 代码静态通过，竖直坠落位置待验收 |
| 等到整根离台导致角色先掉坑 | 标准 Actor 前置推物接触点约在角色根右侧 `0.76`，石柱左边缘越界 `0.02` 时角色根仍约在平台边缘内侧 `0.74`，理论上可安全完成推出 | 几何静态核对，PlayMode 待验收 |
| 暂停退出顺序早于 FSM 真正进入 PLAYING | Resume 只登记待恢复；`SwitchGameStateEvent` 或后续 FixedUpdate 再核对 `PlayCtrl.CheckState(PLAYING)` | 代码与事件顺序静态通过 |
| 暂停、失败或重开后石柱/渐隐/飞字继续运行 | 停问号执行器，暂停私有问号 Tween、飞字 Tween 和完成渐隐，缓存并关闭 Rigidbody2D 模拟；恢复时还原 Dynamic 速度 | 编译通过，PlayMode 待验收 |
| 连续顶击碰到旧动画未完成时卡住或漏计数 | 新反馈节点立即 `IsDone`，每次播放先 Kill(false)、硬复位并重播；items 删除旧 MMF 与 `0.2s` Wait，触发链不等待表现结束 | 代码/Prefab 静态通过，连续手感待验收 |
| `-1` 飞字快速触发时互相覆盖或串到其它关卡 | 使用 4 槽 Level099 私有 TextMeshPro 池；全忙时轮转最旧槽，不注册共享对象池 | 代码静态通过，位置与密度待验收 |
| `-1` 出现时先横向位移 | 没有 X Tween；已移除围绕宽文本中心播放的 65% -> 100% OutBack 缩放，从首帧固定最终倍率，只做本地 Y 上浮和 Alpha 渐隐 | 静态通过，视觉待验收 |
| 本关额外旋转导致金币观感异常 | 删除 Level099 的 Transform Z 轴 `DOLocalRotate`，保留 GoldCoin Prefab 原有 Animator 作为唯一翻转/旋转来源 | 资源/代码静态通过，视觉待验收 |
| 重新启用关卡对象后永久停住 | `OnEnable` 重新订阅并按当前 FSM 对齐运行对象 | 静态通过 |
| 最后一枚金币重复计数或继续触发 | 本地 `coinRouteComplete` 门禁先置位并立刻关闭问号执行器 | 静态通过 |
| 达标后问号仍显示可顶状态 | 复用本关已有 `Box_1/img_lock` 灰图；达标立即切换 `img -> img_lock`，Reset 恢复正常图，最后一枚金币序列不被中断 | 代码/Prefab 静态通过，视觉待验收 |
| 渐隐开始就关闭碰撞，角色穿过可见石柱 | 完整 `1.5s` 渐隐结束后才关闭 Collider 和对象 | 静态通过，观感待验收 |
| Number 被旧 Level089 本地化覆盖 | Prefab 两个 Localize override 均禁用，运行时再次兜底禁用 Number Localize | Prefab/代码静态通过 |
| 顶块误增玩家真实金币 | 自定义 Reward 只更新 `Level99Ctrl` 内存字段，不访问 DB/save | 代码静态通过 |
| 新逻辑影响已验收关卡 | 两个新脚本只绑定 Level099；旧 Level089 脚本和共享组件均未修改 | 静态通过 |

## 验证证据

- Unity 自带 Roslyn 完整编译退出码 `0`，Level099 无诊断。
- 当前 Excel 独立运行 Luban 成功；生成的 LevelData/LevelSort JSON 与工程运行时 JSON 结构一致，Level099 各唯一一条且全表无重复 ID/path。
- Prefab `169/169` 个唯一定义、`0` 重复 fileID、`0` 悬空本地引用；新反馈脚本 GUID 与组件引用各唯一一次。
- 问号链为 `PlaySfx -> Level99QuestionBlockFeedback -> Level99QuestionBlockReward`，无空节点；旧 MMF/Wait 不在 items 中，旧三次限制逻辑为零引用。
- `stone` 为 Stuff 层，Prefab 刚体配置为 Kinematic、质量 `8`、插值和连续碰撞开启、初始冻结旋转；运行时离地参数与需求一致。
- `10099 / Level_099` 在 LevelData 与 LevelSort 的 Excel/JSON 中均唯一。
- 本轮修复后 Unity 6000.0.59f2 Roslyn 完整响应文件再次编译通过，结果为 `0 error`；没有新增 Level099 诊断。
- Prefab 仍为 `169` 个唯一 anchor、`0` 重复、`350` 个精确本地引用且 `0` 悬空；当前策划测试值 `requiredCoinCount=10` 保持不变，`lockedBlockVisual` 唯一绑定现有 `img_lock`。
- 静态扫描确认额外金币旋转、减一缩放与旧根 ColliderDistance 容差字段均已移除；当前只对 Actor 前置 `pushCollider` 和本关 `pillarCollider` 做目标特定距离核对，共享 `ActorCtrl`、Actor Prefab 和 GoldCoin Prefab 未因本轮修复改动。
- 竖直坠落修正后完整 Roslyn 编译为 `0 error / 104` 条工程既有 warning；释放判定唯一使用石柱左边缘，Dynamic 阶段仍为 `FreezeRotation`，水平/角速度均归零，脚本与 Prefab 中旧 `minimumFallHorizontalSpeed` 为零引用。

## 未验证缺口

- 未进行前台 PlayMode，不声明修复后的推柱时长、加速曲线、竖直坠落落点、坑底碰撞、当前 10 次测试、灰图切换、1.5 秒渐隐节奏或出口通路已获策划验收。
- 未实测暂停落在问号回弹、金币上浮或 `-1` 飞字中间时的衔接；代码会显式 Pause/Resume，但最终观感仍需 PlayMode 验收。
- 结算本地化键 `S_congraktTips_Des_level_99`、`S_answer_Des_level_99` 缺失，需策划/本地化确认文本后补齐。
- Reset/Replay、失败区贴边、Android/iOS 横屏触摸和代表性移动设备性能仍为 `uncertain`。

## 回滚

优先通过 Level099 Inspector 调整推柱、重力、计数、完成渐隐、问号回弹/金币上浮及 `-1` 飞字参数，不改共享物理和问号系统。只撤回本次竖直坠落修正时恢复中心释放、解除旋转约束和最小水平速度；完整撤回时删除 Level099 Prefab、私有脚本及配置中的唯一 10099/排序 97 条目。
