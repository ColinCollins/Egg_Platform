# Level099「坚持就是胜利」功能说明

## 2026-08-18 黄色范围脚本精简

- 背景：合并问号块反馈组件内重复的 Tween 序列停止逻辑，保持推柱、计时、金币和视觉复位不变。
- 影响文件：仅 `Level99QuestionBlockFeedback.cs`。`OnDisable()` / `OnDestroy()` 共用私有 `StopActiveSequence()`；视觉姿态恢复仍仅在停用且运行态路径执行。
- 前后差异：仍以 `Kill(false)` 终止同一序列并清空引用，没有修改金币资源、飞字、碰撞、计数、事件或结算。
- 验证：`Assembly-CSharp.csproj` 编译通过（0 错误），目标 diff 检查通过。
- 回滚：将 `StopActiveSequence()` 的两行逻辑重新内联到两个回调。

更新时间：2026-08-11

## 需求与背景

Level099 的正常解法是让玩家持续向右推动岩石柱。开始连续推动后的前 `5` 秒石柱完全不动，随后逐渐加速，直到被推出右侧平台并落入悬崖。场景左侧问号块保留金币反馈；右侧 `Number` 显示距离另一条完成条件还差多少枚金币，默认公开显示 `9999`。若玩家实际把计数归零，金币图标、数字和岩石柱需要用较慢的节奏同步渐隐。

## 影响范围

- `Assets/Game/Prefabs/Level/Level_099.prefab`
- `Assets/Game/Scripts/Level/MainLevel/Level_99/Level99Ctrl.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_99/Level99QuestionBlockFeedback.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_99/Level99QuestionBlockReward.cs`
- `Luban/Configs/Datas/LevelData.xlsx`
- `Luban/Configs/Datas/LevelSort.xlsx`
- `Assets/Game/Configs/tbleveldata.json`
- `Assets/Game/Configs/tblevelsort.json`

本关只新增 Level099 私有控制器、问号块动画节点和收尾节点，不修改 Level089、共享 Actor、`BaseLevelCtrl`、问号块执行器、门、金币存档、全局事件定义或音频资源。

## 原有行为基线

场景布局和岩石柱美术以 Level089 为基础；Level089 原本用 `Level89Ctrl` 与四个顺序按钮控制石柱纵向下降。复制到 Level099 后不再需要顺序按钮、倒计数切换或 Level089 控制器。问号块原有事件链负责金币音效和跳出反馈，但其奖励节点不适合本关的 `9999` 局部计数。

## 当前行为

- `Level99Ctrl` 在角色位于石柱左侧、与柱体 Y 范围重叠、持续按住右移且 `ActorCtrlable` 时，先要求 `ActorCtrl.IsPushStuff` 命中 Stuff 层，再用 Actor 既有前置 `pushCollider` 与本关 `stone` Collider 的目标特定距离确认实际推到的是石柱。这里不再用 Actor 根 PolygonCollider2D 间距作为门禁，也不会把同层问号块误当成石柱；短于 `0.1` 秒的检测抖动保留进度，真正中断后重新从零计时。
- 前 `5` 秒不移动。之后用 SmoothStep 从 `0.03` 平滑加速到 `0.45` 世界单位/秒，加速阶段为 `4` 秒。石柱等待期间使用 Kinematic `Rigidbody2D.MovePosition`，不会被角色直接推走。
- 石柱保持 Kinematic 和初始竖直角度，直到碰撞体左边缘完全越过 `Ground_long` 右边界并额外留出 `0.02` 间距。此时刚体才切为 Dynamic，质量 `8`、重力倍率 `2`，恢复初始角度后继续冻结旋转，同时把水平速度与角速度清零，使整根石柱从坑口正上方竖直下落，不会因柱脚仍压住平台而倒回左侧。
- 问号块执行顺序为金币音效、`Level99QuestionBlockFeedback`、`Level99QuestionBlockReward`。私有动画只移动 `Box_1/img` 显示节点，按“上顶、轻微下压、快速归位”在 `0.165s` 内回到基线；金币在约 `0.34s` 内从块后上浮并渐隐。金币的既有翻转/旋转继续完全由 `GoldCoin` 自带 Animator 播放，本关不再叠加 Transform Z 轴旋转。每次触发都会先中断旧 Tween、硬复位再重播，旧 MMF 和 `0.2s` Wait 保留在 Prefab 中作为未引用的回退数据，不再进入运行时执行链。
- Reward 只增加本关内存计数，不写入玩家持久金币；每次有效触发后 `Number` 显示 `requiredCoinCount - earnedCoinCount`，同时从 Number 位置播放一个上浮渐隐的 `-1`。飞字从首帧就是最终倍率，只改变本地 Y 和 Alpha，不做缩放或横向 Tween；4 槽 Level099 私有世界空间 TextMeshPro 池允许快速连续顶击重叠显示。
- `requiredCoinCount` 在 Inspector 暴露，代码设计默认值为 `9999`；当前 Prefab 按策划测试要求暂设为 `10`，运行时 Number 会显示该值。计数归零后立即关闭问号射线并把 `Box_1/img` 切为已有灰图 `img_lock`，最后一枚金币反馈仍会播完；随后停止石柱物理，石柱全部 Sprite、金币图标和数字以 `1.5` 秒 `Ease.InOutSine` 同步渐隐。渐隐完成后关闭石柱碰撞及三个显示对象，玩家不会穿过仍清晰可见的石柱。
- 标题静态显示“坚持就是胜利”，Number 静态初值为 `9999`；两个 Tips 的 I2 Localize 组件均在 Prefab 中禁用，控制器还会兜底禁用 Number 下的 Localize，避免旧 Level089 term 覆盖数字。
- 本关订阅既有 `GamePauseEvent`、`GameResumeEvent`、`GameResetEvent` 与 `SwitchGameStateEvent`。暂停、失败和重开会冻结石柱物理、完成渐隐、问号射线、私有问号 Tween 与 `-1` 飞字；恢复请求只有在 FSM 确认 `PLAYING` 后才重新启用。禁用后再启用也会按当前状态重新对齐。

## 数据、资源、UI 与事件流

推柱路线不读写存档：角色输入与碰撞 -> 连续推动计时 -> Kinematic 位移 -> 平台边缘切 Dynamic -> 原有出口门通关。

金币路线同样不读写存档：顶问号块 -> 既有 `getCoin` 音效、金币 Animator 和 Level099 私有位置/Alpha Tween -> 本关内存计数减一与只向上漂的 `-1` -> Number 刷新 -> 达到 `requiredCoinCount` 后问号切灰并执行本地渐隐。没有新增全局事件、资源加载、分析、广告、IAP、RemoteConfig 或平台 SDK 调用。

关卡配置已登记 `10099 / Level_099`，默认排序为 `97`；源 Excel 与运行时 JSON 中均为唯一记录。

## 验证记录

- 使用 Unity 6000.0.59f2 自带 Roslyn 和当前 Bee `Assembly-CSharp` 响应文件完整编译：退出码 `0`，Level099 无错误；输出只有工程既有 warning。
- 使用当前 Excel 在独立临时目录重新执行 Luban：退出码 `0`；新生成的 `tbleveldata.json`、`tblevelsort.json` 与工程运行时 JSON 结构完全一致，Level099 数据/排序各唯一一条，全表无重复 ID 或 path。
- Prefab 静态复扫：`169` 个 YAML 定义全部唯一，无重复 fileID、无悬空本地引用；新增脚本 GUID 与组件引用各唯一一次。
- 根节点只挂一个 `Level99Ctrl`；旧 `Level89Ctrl`、`Level89SequenceButton` 和三个倒计时相关组件引用均为 `0`。
- Level099 控制器的 Actor、stone Transform/Rigidbody2D/Collider、`Ground_long`、Number、金币图标和问号执行器引用均存在且类型正确。
- 问号事件链三个节点为 `PlaySfx -> Level99QuestionBlockFeedback -> Level99QuestionBlockReward`，顺序和引用完整；旧 MMF/Wait 组件不在 items 中。LevelData、LevelSort 的源表与运行时 JSON 均无重复 ID 或 path。
- 2026-08-11 本轮修复后再次使用同一 Unity Roslyn 完整响应文件编译，退出码 `0`；Level099 无 C# error，输出仍只有工程既有 warning。
- 本轮 Prefab 静态复扫仍为 `169` 个唯一定义、`0` 重复 anchor、`350` 个精确本地引用且 `0` 悬空；`requiredCoinCount=10`、`lockedBlockVisual -> img_lock` 和新反馈脚本 GUID 均各唯一一次。
- 静态扫描确认 Level099 已无 `coinRotationAngle`、`DOLocalRotate`、`minusOnePopDuration`、减一 `DOScale` 或旧根碰撞体 `pushContactTolerance`；推柱门禁组合使用 `actor.IsPushStuff` 与 `actorPushCollider.Distance(pillarCollider)`，没有修改共享 Actor 或 GoldCoin Prefab。
- 2026-08-11 竖直坠落修正后再次完整编译为 `0 error / 104` 条工程既有 warning；静态扫描确认释放判定使用 `pillarCollider.bounds.min.x`，坠落阶段为 `FreezeRotation` 且线速度/角速度归零，旧 `minimumFallHorizontalSpeed` 已从脚本和 Prefab 移除。

## 未验证与剩余风险

- 未操作用户前台 Unity；修复后的连续推动五秒门禁、石柱完全越界后的竖直坠落位置、坑宽余量、角色在边缘的站位、当前 `10` 次测试完成渐隐、暂停/恢复、Reset/Replay 和完整进门结算仍需 PlayMode 验收。
- 问号块 `0.165s` 回弹、原金币 Animator 配合 `0.34s` 上浮、只向上漂的 `-1` 以及达标灰图切换仍需 PlayMode 观察；当前位置、时长与倍率参数可在 Level099 Inspector 微调，不影响共享组件。
- 坑底 `FallArea_Pit` 比可见坑口左右各内缩约 `0.05` 至 `0.06` 世界单位；Actor 宽度足以覆盖正常下落，但极端贴边轨迹仍标记为待实测。
- 配置引用的 `S_congraktTips_Des_level_99` 与 `S_answer_Des_level_99` 当前未在本地化资源中找到。关卡加载和场景内标题/数字不依赖它们，但结算文案显示为 `uncertain`。
- Android/iOS 横屏触控、不同帧率下的物理接触稳定性与 60 fps 表现尚未真机验证。

## 回滚路径

只回滚本次竖直坠落修正时，可把释放判定恢复为 Collider 中心、坠落约束恢复为 `None`，并恢复最小水平速度字段；推柱计时、金币反馈、灰态和计数无需回滚。完整回滚 Level099 时，移除 `Level_099.prefab`、Level99 脚本目录及其 meta，并从 LevelData/LevelSort 源表和两份运行时 JSON 中移除唯一的 `10099 / Level_099`、排序 `97` 记录。
