# Level124 功能说明

## 2026-08-18 黄色范围脚本精简

- 背景：合并停用和销毁阶段重复的事件/皮肤回调解绑，保持拖拽、换装、门流程、Tween 和结算行为不变。
- 影响文件：仅 `Level124Ctrl.cs`。新增私有 `UnbindRuntimeCallbacks()`；`OnDisable()` 仍继续清除拖拽并恢复门流程，`OnDestroy()` 仍继续终止 Tween。
- 前后差异：事件重置与 Actor 皮肤回调解绑顺序不变，停用/销毁各自专属清理未合并或移动。
- 数据/资源：未修改皮肤资源、Prefab、配置、存档、UI、全局事件、打点或共享脚本。
- 验证：`Assembly-CSharp.csproj` 编译通过（0 错误），目标 diff 检查通过；回滚时内联私有方法即可。

更新时间：2026-08-17

## 需求与背景

Level124 以 Level004 的场景与门结构为基础。画面中的横置数字 `8` 表示无限：玩家在未处理该符号时进入出口门，只会在符号旁看到一个上浮渐隐的 `-1`，实际状态不变，并从初始门重新出现。玩家将横置 `8` 旋转为正向 `8` 后，符号永久锁定，后续每次进门才真正减少一次；数字显示为 `1` 时再次进门，关卡正常结束。

## 影响范围

- `Assets/Game/Prefabs/Level/Level_124.prefab`
- `Assets/Game/Scripts/Level/MainLevel/Level_124/Level124Ctrl.cs`
- `Luban/Configs/Datas/LevelData.xlsx`
- `Luban/Configs/Datas/LevelSort.xlsx`
- `Assets/Game/Configs/tbleveldata.json`
- `Assets/Game/Configs/tblevelsort.json`

本次新增 Level124 私有控制器并只修改 Level124 Prefab 的本地覆盖，不修改 Level004、`BaseLevelCtrl`、`ActorCtrl`、门 Prefab、Tips Prefab、共享旋转组件或通用 UI。

## 原有行为基线

复制后的 Level124 仍挂载 Level004 的 `KnockKnock`：连续点击门后才启用出口 Trigger，随后直接调用 `BaseLevelCtrl.OnActorTrigger2D` 进入标准通关流程。场景中已有策划摆放的标题和第二个 Tips，第二个 Tips 文本为 `8`，位置为 `(3.33, 2.29)`、Z 旋转约 `88.161°`，视觉上表现为无限符号。

## 当前行为

- 旧 `KnockKnock` 组件已在 Level124 根节点替换为 `Level124Ctrl`；`knockArea` 仅保留为无逻辑场景对象，不再响应敲门。
- 出口门 Trigger 从关卡开始即启用，玩家靠近门后自动触发本关流程。UnityEvent 只绑定 `Level124Ctrl.OnActorEnterDoor`，不会在普通循环中提前调用标准成功逻辑。
- 横置 `8` 使用 Level124 私有的鼠标/单指旋转状态机。点击命中范围来自现有 TextMeshPro 的世界渲染边界，并额外保留 `0.35` 世界单位操作余量；接近 `0°` 或 `180°` 的 `7.5°` 范围时吸附为 `0°`，切换为有限模式且本次尝试内不可转回。
- 无限模式下每次普通循环返回时，会在角色真正从初始门开始渐显走出的同一时刻播放一个正向、上浮并渐隐的 `-1`，但不修改数字和内部次数。
- 有限模式从 `8` 开始。每次普通循环也在角色从初始门开始走出时按 `8 -> 7 -> ... -> 1` 扣减并刷新数字；显示 `1` 时下一次进入不再播放 `-1`、归零或返回，而是直接交给 `BaseLevelCtrl.OnActorTrigger2D` 执行原有通关、星星、存档和解锁流程。
- 普通进门流程不调用 `SuccessAnimCtrl.Play()`，因此不会播放彩带、烟花、成功震动，也不会使用公共 `ActorDestroyAnimHandle` 缩旋或销毁角色。
- 普通进门时锁定玩家输入和 Gameplay UI，播放出口门打开；角色保持走路动作，向门内移动并渐隐。角色完全隐藏后出口门关门，从关门开始等待 Inspector 暴露的 `exitCloseToEntranceOpenDelay`（Prefab 当前为 `0.22s`），再由 Level124 私有流程打开初始门、让角色渐显走出并关闭初始门；普通循环不再调用共享 `TryToPlayEnterDoorAnim()`。
- `flowSpeedMultiplier` 在 Inspector 暴露且 Prefab 当前为 `1.2`：普通循环中出口门与初始门的开/关门 Spine、角色吸入时长和吸入阶段角色 Spine 速度均统一提升 20%；初始门走出仍沿用共享出生表现的 `0.2s`，避免缩短落点。门衔接停顿参数按实际秒数独立调节，不被倍率再次缩放。
- 角色主 Spine、动态皮肤 `SpriteRendererGroupAlpha` 和影子同步处理 Alpha，避免只隐藏主 Spine 后仍残留皮肤或影子。
- 普通流程用 flow version、Trigger 临时关闭和运行门禁阻止重复触发。暂停会停止本关旋转输入、角色吸入/走出 Tween、门与角色 Spine、`-1` Tween 和私有等待计时；Resume 后从当前阶段继续。Reset 会先清理最终成功锁，再使旧异步流程失效、将出口门与初始门恢复为 idle、恢复角色初始状态和控制权；Disable 和 Destroy 会清理 Tween。
- 运行时会再次禁用数字节点下的本地化组件，避免静态数字被旧 Level004 term 覆盖。策划现有数字位置、字号、文本与初始旋转均保留。

## 数据、UI、事件与存档流

无限/有限状态和剩余次数只存在于当前关卡实例内，不新增存档字段、全局状态或分析事件。普通循环只派发既有 `GamePlayPanelSwitchBlockEvent` 并执行 Level124 私有门循环；最终一次才进入 `BaseLevelCtrl` 的标准成功链。

LevelData 使用 `10124 / Level_124`，结算和答案键分别为 `S_congraktTips_Des_level_124`、`S_answer_Des_level_124`；LevelSort 使用排序 ID `101` 指向 `10124`。

## 验证记录

- 使用 Unity 6000.0.59f2 自带 Roslyn和当前 Bee `Assembly-CSharp` 完整响应文件编译：退出码 `0`；Level124 无编译错误，输出仅有工程既有 warning。
- Level124 Prefab 静态扫描为 `136` 个唯一 YAML 定义，无重复 fileID、无悬空本地引用。
- `startDoorRoot` 与 `startDoorSpine` 分别绑定 StartDoor 根 Transform 和同根 ActorSpineCtrl；Prefab 中 `flowSpeedMultiplier = 1.2`、`exitCloseToEntranceOpenDelay = 0.22`。
- 新脚本 GUID、出口门 UnityEvent 方法和目标类型均唯一；旧 `KnockKnock` 脚本 GUID 在 Level124 Prefab 中为零引用。
- 出口 Trigger 已启用，目标为本关控制器；数字位置 `(3.33, 2.29)`、字号 `12.3` 和初始角度 `88.161°` 各保留一处。
- LevelData、LevelSort 已用 Artifact Tool 完成全工作表渲染、目标尾部值检查和公式错误扫描，并写回项目源表。写回后重新导入源表复核：LevelData 的 `10124 / Level_124` 位于第 107 行且唯一，`Level_999` 位于第 108 行且仍为唯一末行；LevelSort 的 `101 / 10124` 位于第 106 行且唯一；两份表的公式错误扫描均为 `0`。

## 未验证与剩余风险

- 按用户要求未操作 Unity 前台，也未运行 PlayMode；旋转命中手感、`7.5°` 吸附阈值、角色吸入与初始门走出节奏、`0.22s` 门衔接停顿、`-1` 出现位置和完整八次循环仍需策划在 Unity 中验收。
- 暂停面板在普通进门期间由 Gameplay UI Block 阻止点击；异常外部暂停落在出口渐隐、两扇门动画或初始门走出阶段时的最终视觉衔接仍待 PlayMode 验证。
- Android/iOS 单指旋转、不同分辨率和动态皮肤加载时序尚未真机验证。
- `S_congraktTips_Des_level_124` 与 `S_answer_Des_level_124` 仅登记配置键；实际本地化文案是否存在仍需本地化流程确认。

## 回滚路径

完整回滚时移除 Level124 Prefab、`Level_124` 私有脚本目录及其 meta，并从 LevelData、LevelSort 源表和两份运行时 JSON 中删除唯一的 `10124 / Level_124` 与排序 `101` 记录。Level004 和所有共享组件无需回滚。

## 2026-08-26：Reset/Replay 销毁边界清理

- 原 Reset 回调会恢复 Actor、重置八次计数器、重播两扇门 Idle 并重新启用出口 Trigger；这些写入作用于即将销毁的旧实例。
- 现在 Reset 和 Replay 统一调用 `BeginTeardown()`：使流程失效、杀死 Tween、禁用出口 Trigger与输入、解绑事件和角色皮肤回调，并释放共享 GameplayPanel Block；不恢复门、Actor 或计数器初态。
- 新实例仍由 `Awake()` 缓存作者状态并执行 `InitializeCounter()`，正常 Pause/Resume 和门流程不变。
- 已通过 C# 编译（0 error / 120 warning）；Reset/Replay 发生在各门动画阶段时的画面仍需 PlayMode 验证。
