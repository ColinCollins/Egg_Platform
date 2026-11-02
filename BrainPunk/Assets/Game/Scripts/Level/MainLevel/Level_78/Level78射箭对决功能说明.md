# Level 078 射箭对决功能说明

## 背景与目标

`Level_078` 原先只有射箭素材与通用 `KnockKnock` 流程，无法表达“小恶魔先射、移动挡板解谜、Box 三箭结算”的玩法。本实现把关卡收口为 Level 078 本地拥有的三箭射箭对决，不修改共享 `GamePlayPanel`、`ActorCtrl`、Actor Prefab 或参考关卡。

正确解法是把 `Sp_l78_img_03` 从远距固定点拖到近靶满分点，再控制 Box 重新进入挡板前的射箭点。远距单箭最高 9 分、三箭最高 27 分，必输给小恶魔的 28/29 分；近靶每箭固定 10 分，以 30 分胜出。

## 组成与职责

- `Domain/Level78Duel.cs`：纯领域状态机，负责合法小恶魔组合、逐箭事务、双清理门禁、重新进入、挡板状态、远近计分、忙碌门禁和最终胜负。
- `Level78Ctrl.cs`：Level 078 唯一运行时编排器，负责场景引用、拖拽与吸附、角色边界、箭矢、计分文字、HUD、Tween、重开清理及既有胜负归口。
- `Level78ActorCtrl.cs`：替换本关玩家 Actor 的通用控制器；射箭时阻止 idle/walk 覆盖。Box 非循环播放 `draw_pull` 后以原动画 `1.0×` 速度循环 `draw_hold` 保持蓄力感，点击后直接播放 `draw_shoot`；小恶魔仍自动播放原速完整三段动作链。
- `Level78Ctrl.cs` 与 `GamePlayPanel_026_78.prefab`：面板继续使用共享 `GamePlayPanel`；控制器从当前面板取得 Jump/Aim，射箭开放后在同一槽位切换，并直接接收 Aim 按下，不派发 `PlayerJumpEvent`。
- `Tests/EditMode/Level78DuelTests.cs`：Unity Test Framework 下的领域行为测试；领域程序集不依赖 UnityEngine。

## 阶段流程

1. 初始化时领域模型等概率选择 `[10,10,8]`、`[10,9,9]` 或 `[10,10,9]`，再用 Fisher-Yates 打乱一次。运行时从玩家 Actor 实例创建独立 `box_bad` 小恶魔，使其开屏即显示在 `demonHomePosition`；克隆刚体清零并关闭物理模拟，碰撞体保持禁用，因此待机时不会受重力下坠或与玩家碰撞。
2. 小恶魔开场保持静止。玩家首次产生左移或右移事件时，领域门禁只接受一次触发，小恶魔才前往 `demonShootingPosition`；跳跃不触发，后续移动不重启。小恶魔到达射箭点后，三次消费已准备分数；每支箭命中后才提交单箭分和累计分。
3. 第三箭计分后停留 1 秒，同时启动箭组 0.5 秒渐隐和小恶魔归位。`NotifyDemonArrowFadeCompleted()` 与 `NotifyDemonReturnCompleted(...)` 两个信号都到达后才进入挡板调整。
4. 挡板只沿 X 轴移动，范围限定为 Prefab 原始远距点与 `nearBoardLocalX`。从远距点松手后用 0.2 秒 `Ease.OutQuad` 吸附到最近点；近似等距时回到远距点。一旦近靶满分点吸附完成，本局关闭拖拽且领域状态拒绝回退，重开才恢复远距点。
5. 射箭开放前，挡板是 Box 向右移动边界，向左移动和跳跃仍可用。若小恶魔归位时 Box 已占据射箭点，必须先离开再重新进入。
6. 新进入射箭点后立即锁定 Box 与挡板，Jump/Aim 同槽切换，弧形蓄力表移动到 Box 朝计分靶方向的前方并启动指针往返；指针单程使用 `Ease.InOutSine`，在两端减速、中段加速。Box 同时非循环播放 `draw_pull`，随后以原动画 `1.0×` 速度循环 `draw_hold`，首个持弓周期完成前射击输入不生效。
7. 有效 Aim 点击先锁定指针位置，再从持弓姿态直接播放 `draw_shoot`。本次 `TrackEntry.TrackTime` 到达 `releaseNormalizedTime` 时释放箭矢；箭到达落点后立即提交计分。前两箭等待“计分已提交 + 本次角色动作已结束”两个条件同时满足后重启指针，并恢复 `draw_hold` 等待下一次点击。
8. 第三支 Box 箭提交并完成本次靶面受击反馈后锁定终局。小恶魔先通过角色讲话气泡显示“你赢了”或“你输了”1.5 秒，再分别调用无右门的 `BaseLevelCtrl.SuccessStrightly()` 或派发既有 `GameResetEvent(GameResetType.Failed)`。

## 计分与视觉

- 远距绿色固定 9 分；黄色按与中心距离二等分为 8/7；红色三等分为 6/5/0。`greenHalfWidth` 与 `yellowOuterHalfWidth` 在 Level 078 Prefab 本地标定，左右规则对称。
- 近靶固定输出 10 分，不读取指针档位；挡板吸附到近靶满分点后，弧形蓄力表直接显示现成绿色 Sprite `Sp_l73_jdt_02.png`，不通过 Shader 或运行时材质自行填色；蓝色外框和指针保持原样。远距点仍显示 `Sp_l73_jdt_04.png` 原红/黄/绿分段。
- 场景原箭只作为隐藏模板；每次尝试为双方各创建三支独立落箭。素材中心红色圆面对应 10 分、紧邻外圈对应 9 分，5–10 分使用以真实靶心为基准的逐圈固定半径；落点只消费已确定分数，不反向改变计分。
- 三个固定箭序使用不同环上方位；箭尖沿释放点到落点的抛物线移动，箭身逐帧跟随弹道切线，从上扬自然过渡到下压插入。运行时按箭 Sprite 右端箭头反算 Transform 位置，因此实际扎入计分环的是箭头而不是箭身中心；10 分环上任意两支预设箭头间距大于 `0.4`，三支可清楚区分。0 分仍使用横向间隔超过 `0.9` 的三个靶前地板点，落地角度由同一弹道自然产生。
- 计分板底图 `Sp_l78_img_02` 不包含标题文字；常驻子节点 `ScoreBoardTitle` 使用世界空间 `TextMeshPro`，I2 `Localize` 绑定 `S_LevelTips_Des_level_78_1`，简体中文显示“计分板”（已登记飞书多语言表；本地 CSV/I2 与其他语言待后续同步），复用工程 `en SDF.asset`，字号 50、颜色 `#E2E8EF`。运行时在同一底图节点下创建上 Box、下 Demon 两行，每行三格单箭分与一格总分。总分格初始为空白，待三格单箭分记完后单次弹出直接揭晓；未射单箭为空白。8 格同样使用世界空间 `TextMeshPro`、字号 76，分别对齐素材两行各自的四个实际格心并强制双位数单行显示；六个单箭分为纯白无描边，Box 总分为 `#2F7EDF`、小恶魔总分为 `#E11B00`，两个总分均带 `#FFFFFF` 外描边。
- 小恶魔箭组渐隐不清除计分文字；小恶魔成绩持续到重开或退出。

## 输入与生命周期

- Editor 鼠标与 Android/iOS Touch 都通过 Level 078 本地输入入口命中挡板 Collider；拖拽只改 X，近靶满分点提交后入口不再接受新的拖拽。
- 开场触发复用现有 `PlayerLeftMoveEvent` 与 `PlayerRightMoveEvent`，不新增全局事件；`Level78Duel.TryStartDemonOpening()` 保证每次尝试只接受第一次左右移动。
- Aim 按钮保持可交互。忙碌点击仍由 `ClickScaleAnim` 提供按压缩放，但 `Level78Duel.TryStartBoxShot(...)` 拒绝行为，不发箭、不排队、不消耗次数，也不触发震动。
- Box 的待发姿态与射击动作均由本关 `Level78ActorCtrl` 局部切换；不修改 `Box.json`、共享 `ActorSpineCtrl` 或其他关卡角色控制器。小恶魔仍使用 `draw_pull → draw_hold → draw_shoot`。
- 每次异步回调携带 `sessionVersion`；Spine 链只保存一个活动 `TrackEntry`，逐段使用具名回调并在切段、重开、禁用、销毁或结算时退订。生命周期清理还会 Kill Tween、停止动作并解绑 Aim 按钮和当前面板引用；禁用/销毁路径直接销毁运行时小恶魔与箭矢，而 `GameResetEvent`/`GameReplayEvent` 路径只作逻辑清理，小恶魔与落箭随 PlayCtrl 约 0.5 秒后的整关销毁一并移除，避免 box-bad 先于关卡其余元素凭空消失。
- 不新增保存字段、配置、全局事件定义、资源加载协议、analytics、广告、IAP、RemoteConfig 或平台 SDK 链路。

## Prefab 接线

- `Level_078.prefab` 根节点保留 `BaseLevelCtrl`，旧 `KnockKnock` 组件替换为 `Level78Ctrl`。
- `BaseLevelCtrl.gamePlayPanelName = GamePlayPanel_026_78`。
- `GamePlayPanel_026_78.prefab` 根组件使用共享 `GamePlayPanel`，保留 `Root/jump_btn` 与 `Root/aim_btn`；两者的阶段显隐和 Aim 输入绑定由 `Level78Ctrl` 负责。
- 玩家 Actor prefab instance 移除源 `ActorCtrl`，新增 `Level78ActorCtrl`；`BaseLevelCtrl.actor` 与 `EnterDoorAnimCtrl.actor` 均指向新组件。
- 本关控制器显式绑定 `Sp_l78_img_03`、`Sp_l78_img_01`、`Sp_l78_img_02`、`Sp_l78_gj_02`、`Sp_l78_jian_01`。
- 蓄力表同时绑定 `ArrowRange_normal`（`Sp_l73_jdt_04.png`）与 `ArrowRange_green`（`Sp_l73_jdt_02.png`）；运行时只切换两张 Image 的显示状态，不生成填色材质。

## 当前标定值

- 近靶本地 X：`2.65`；结合挡板保持不变的本地 Y/Z，最终本地坐标为 `(2.65, -2.89, 0)`。
- 射箭点位于挡板左侧 `0.85`，进入容差 `0.12`。
- 弧形蓄力表位于 Box 朝计分靶方向前方 `1.6`、Box 上方 `0.76`，根缩放为 `0.09`；该位置随射箭站位更新，目标是保证表盘上端不被计分板遮挡、下端贴近地面。首轮 `0.85` 横向偏移、固定 Y/`0.11` 缩放及 `+0.94`/`0.10` 标定均已由运行截图证明不合适并弃用。
- 当前 Level 078 Prefab 有效指针范围：`60° → -50°`，单向 `1s`，完整往返 `2s`；缓动为 `Ease.InOutSine`，单程平均角速度仍为 `110°/s`。
- 绿色半宽 `0.1`，黄色外边界半宽 `0.3`。
- 松弦比例 `0.55`，箭飞行 `0.45s`。

这些数值是根据现有 Prefab 与素材形成的开发标定，仍需在依赖完整的 Unity Play Mode 中逐帧和截图复核。

## 2026-07-29 近靶蓄力表改为全绿

- 需求：用户明确要求近靶蓄力条颜色改为原蓄力条 9 分对应的绿色，取代 2026-07-20 的整段黄色要求；远距颜色计分档与近靶固定 10 分规则不变。
- 素材：使用仓库现有 `Sp_l73_jdt_02.png`（Prefab 节点 `ArrowRange_green`）作为完整绿色弧段；用户提供的 `/Users/greaterbay/Desktop/l73/Sp_l73_jdt_02.png` 与仓库文件 SHA-256 完全一致，无需复制或重建资源。
- 新行为：移除 `NearAimGaugeColor`、`SkeletonGraphicFill-Straight.mat` 模板和运行时材质克隆。近靶时隐藏 `ArrowRange_normal`、显示 `ArrowRange_green` 并把其 `fillAmount` 设为 `1`；远距、初始化和清理时反向恢复。
- 历史：`2026-07-20 近靶蓄力表全黄反馈` 一节为当时需求与验证的历史记录，其中黄色要求及运行时材质机制均已被本节取代。
- 验证：静态检查确认 Prefab 的 `ArrowRange_green` GUID `1d6938b798aadf742acf81e5b6ccba04` 指向 `Sp_l73_jdt_02.png`，且代码不再包含 `_FillColor`、`NearAimGaugeColor`、`nearAimGaugeFillTemplate` 或运行时材质名。Unity Play Mode 实际画面仍需按授权入口另行复验。
- 回滚：恢复材质模板字段与运行时材质切换逻辑即可；不需要回滚领域计分、箭矢落点或配置。

## 2026-07-20 近靶蓄力表全黄反馈

- 需求：挡板吸附到近靶满分点后，弧形蓄力表必须整段显示为黄色，直观表达“任意指针位置都必定命中 10 分中心区”；远距颜色计分档保持不变。
- 原行为：`Level78Duel.TryStartBoxShot()` 已在 `NearFullScore` 固定输出 10 分，但 `Level78Ctrl` 始终显示同一张红/黄/绿分段图，视觉与规则不一致。
- 新行为：`EndBoardDrag()` 只在领域成功提交近靶点后切换视觉；`EnterBoxShooting()` 再按领域挡板状态兜底同步。运行时克隆 `SkeletonGraphicFill-Straight.mat`，利用原 `ArrowRange_normal` Sprite Alpha 保留手绘弧段轮廓并填充 `#FFD84D`，独立蓝色外框与指针不改；初始化、远距和清理恢复原材质。
- 计分与落点：领域层既有回归继续覆盖指针归一化位置 `0/0.5/1` 均输出 10、三箭 30 分胜利；10 分箭继续落在素材中心红色 10 分区，并保留三支箭的可辨认错位，不把三支箭完全重叠到单一像素。
- 验证：Unity `6000.0.59f2` worktree 门禁通过，batchmode 与 GUI Initial Refresh 三类编译错误为 0；真实 `Level_078.prefab` Play Mode 断言材质 `fillPhase=1.000`、`fillColor=#FFD84DFF`，截图见 `docs/screenshot/20260720/20260720-level078-near-aim-gauge-yellow-after.png`。Android/iOS 真机 Shader Variant、完整三箭和重开视觉仍待 smoke。
- 回滚：移除 `ApplyAimGaugeVisual()`、Prefab 的两个近靶表盘引用和运行时材质字段即可；不需要回滚领域计分、箭矢落点、配置或玩家数据。

## 2026-07-21 差异化计分反馈

- 需求背景：总分原先通过 `DOCounter(...)` 在短时间内逐整数递增，最新需求要求替换为整串上下翻牌换数，并统一覆盖 Box 与小恶魔两行。
- 原有行为基线：`Level78Ctrl.WriteScore(...)` 在同一次提交中让单次得分缩放弹出，并让总分通过 `DOCounter(...) + Ease.OutQuad` 在约 `0.20s` 内从旧累计值滚到新累计值。
- 已确认行为：单次得分继续使用既有缩放弹出；总分把整串旧值加速向上淡出的同时，把整串新值从下方快速移入、淡入并平稳停住，不再出现中间整数或逐位翻动。脱靶时单次得分 `0` 仍缩放弹出，但未变化的总分不播放反馈。
- 已确认节奏：总分翻牌约 `0.20s`，垂直位移约数字字高 `35%`；旧值使用 `Ease.InCubic`，新值使用 `Ease.OutCubic`，不缩放、不旋转、不产生位置过冲。现有第三箭靶面受击序列约 `0.22s`，计分反馈不得额外延迟即时结算。
- 已确认打断规则：新计分会终止目标字段的旧 Tween，再从正确状态播放新反馈；第三箭结算、重开和退出会终止计分反馈，并把文字与缩放强制恢复到已提交的最终状态。结算不等待数字动画完成。
- 已确认单次得分曲线：文字写入后先使用原始尺寸的 `0.65×`，再以 `Ease.OutBack` 在约 `0.20s` 内恢复到 `1×`；允许缓动产生轻微自然过冲，不额外叠加位移、旋转或颜色变化。
- 已确认总分实现边界：正式总分文本承载新值，每行各有一个同样式的旧值副本；同一 DOTween `Sequence` 并行驱动旧值位移/淡出与新值位移/淡入，完成或打断后只保留正式文本并恢复已提交值、稳定位置与完整透明度。
- 2026-07-28 调整：总分不再逐箭即时叠加显示——总分格初始为空白，前两箭不更新；第三格单次得分弹出完成后，从第一箭累计分开始逐箭累加翻牌三步（0 分步也照常翻牌），最终停在最终总分。单分格弹出起点播放 `normalScore`，揭晓序列起点播放 `finalScore`，音效火后即忘、与动效时长脱钩。现术语以 `docs/contexts/levels/078/CONTEXT.md` 的 总分 / 逐箭即时计分 / 差异化计分反馈 / 脱靶 为准。
- 已确认应用范围：Box 与小恶魔两行应用相同整串翻牌规则；六个单箭分使用 `#FFFFFF` 且无描边，两个总分分别继承 Box 蓝色、小恶魔红色及白色外描边。
- 已确认反馈通道：本轮只改变计分数字本身，不新增或调整音效、粒子、震动和其他反馈；既有箭矢、靶面受击、角色动作与输入反馈保持原样。
- 实现范围：只调整 Level 078 本地 `Level78Ctrl.cs` 的计分表现与生命周期清理，并在 `Level78ManualProbe.cs` 增加专项验证入口；未修改共享 `GamePlayPanel`、全局事件、领域状态机、字体资源、Prefab 或其他关卡。
- 数据与系统边界：不修改存档、配置、资源加载、analytics、广告、IAP、RemoteConfig 或平台 SDK；当前没有数据迁移。
- 当前验证：Unity `6000.0.59f2` worktree 启动门禁通过，batchmode 与 GUI Initial Refresh 三类编译错误均为 0；真实 `Level_078.prefab` 的 `-level78ScoreFeedbackProbe` 正常退出，验证 Box 与小恶魔两行、`0 → 10` 和 `10 → 19` 整串交叉中间态、`0.238` 本地单位位移、透明度、无缩放/旋转、脱靶跳过与主动打断收口。图形模式进行中帧归档于 `docs/screenshot/20260721/level078-total-score-flip-in-progress.png`。
- 风险与未验证缺口：世界空间 TMP 旧值副本的纯白颜色、透明度和排序需要真实运行验证；Android/iOS 横屏、60fps、完整第三箭结算、重开与退出仍需发布前人工 smoke。
- 回滚路径：恢复 `PlayTotalScoreFeedback(...)` 的 `DOCounter(...) + Ease.OutQuad` 路径，并移除两个旧值副本及翻牌清理；领域计分、Prefab、资源、配置和玩家数据均无需迁移。

## 2026-07-16 手测修复：小恶魔开场与首次移动

- 现象：手测时小恶魔创建后掉出场景，玩家看不到其正常进场和射箭表现。
- 根因：小恶魔复制完整玩家 Actor 后禁用了 Collider2D，但仍保留动态 Rigidbody2D、重力和物理模拟；同时创建完成就立即执行进场 Tween，没有等待玩家首次移动。
- 修复：创建时将小恶魔刚体速度和重力清零并设为 `simulated = false`；小恶魔停留在开场位，首次左/右移动通过领域单次门禁启动进场，到达射箭位后沿用既有三箭流程。
- 自动验证：新增 `DemonOpening_WaitsForFirstPlayerMovement_AndStartsOnlyOnce`，正式领域测试为 30/30 通过；Unity Game View 的开场、首次移动、进场与第一箭视觉仍需修复版手测确认。
- 回滚：恢复 `CreateDemon()` 的即时 `DOLocalMove` 并移除开场门禁与刚体隔离即可回到修复前行为；该回滚会重新引入下坠和时序偏差，不建议用于发布。

## 2026-07-16 持弓待发修正

- 原行为：Box 到达挡板前射箭点后保持普通 idle；首次 Aim 点击才开始 `draw_pull → draw_hold → draw_shoot`，画面与目标参考不一致。
- 新行为：Box 到达射箭点立即非循环播放 `draw_pull`，随后以 `0.35×` 速度循环 `draw_hold` 保持蓄力感；首个低速持弓周期完成后 Aim 点击才进入非循环 `draw_shoot`，前两箭结束后重新执行完整准备流程，第三箭仍由既有结算清理收口。
- 视觉依据：`docs/screenshot/20260716/level078-before-box-idle-at-board.png` 与 `docs/screenshot/20260716/level078-reference-hold-bow-before-shoot.png`。
- 验证：静态调用链确认 Box 到点调用 `ShowArcheryReadyPose()`、Aim 点击调用 `PlayArcheryShotFromReadyPose()`，小恶魔仍调用 `PlayArcherySequence()`；共享 `ActorSpineCtrl`、Box Spine 资源与 Level 87 差异门禁为零，`git diff --check` 通过。
- 编译缺口：Unity 6000.0.59f2 batchmode 因仓库既有 `Guru`、`YooAsset`、`Cysharp/UniTask` 等依赖缺失退出；去重后 14 类 `CS0246`，本次 Level 078 两个脚本无 `error CS` 命中。
- 未验证项：仍需在依赖完整的 Unity Play Mode 中确认 `draw_pull → 低速循环 draw_hold` 衔接、持续蓄力感、循环接缝、准备期间输入门禁、点击切换、前两箭重新准备和第三箭结算画面，并补做 Android/iOS 横屏触控与 60fps smoke。
- 回滚：回退 `Level78Ctrl.EnterBoxShooting()` 的待发调用与 Box 专用射击入口，并恢复 `PlayArcherySequence()` 调用即可；小恶魔、Prefab、共享 Spine 资源和计分领域不需要回滚。

## 2026-07-17 小恶魔影子修复

- 现象：运行时小恶魔本体正常显示，但脚下没有玩家 Actor 同款动态影子。
- 原因：小恶魔由 `player.gameObject` 运行时克隆，`CreateDemon()` 只重置了刚体、碰撞体、皮肤和控制状态，没有对克隆体的 `ShadowController` 执行明确的角色跟踪重绑定。
- 修复：`CreateDemon()` 在小恶魔组件确认有效后，查找其子节点 `ShadowController`，启用影子根节点并调用 `SetCharacter(demon.transform)`。修复仅位于 Level 078 本地编排器，不修改共享 `Actor.prefab` 或 `ShadowController.cs`。
- 验证：Unity 6000.0.59f2 重编译未出现新增 `error CS`；`-level78ManualProbe` 使用真实 `Level_078.prefab` 通过等待、进场、归位和回家朝向四个检查点，等待画面确认小恶魔脚下影子可见。前后证据为 `docs/screenshot/20260717/level078-demon-shadow-before.png` 与 `docs/screenshot/20260717/level078-demon-shadow-after.png`。
- 回滚：删除 `CreateDemon()` 内的 `ShadowController` 重绑定代码即可，不影响小恶魔刚体、走位、射箭或计分逻辑。

## 2026-07-17 箭靶视觉圆心修正

- 现象：计分板显示 9 分时，朝下方位的箭头可能落入下一层红色 8 分环；不同箭序还会出现同分箭头视觉上偏内或偏外。
- 原因：各分值的环半径映射正确，但 `Level78ArrowLayout` 使用的箭靶圆心 Y 偏低。对 `Sp_l78_img_01.png` 的中央红色连通区测得视觉中心约为像素 `(118.43, 196.42)`；相对 283×479 Sprite 的中心 Pivot，世界偏移应约为 `(-0.23, 0.43)`，旧值 `(-0.18, 0.16)` 会把所有箭头整体向下偏移约 `0.27`。
- 修复：把 `TargetCenterOffsetX/Y` 标定为 `-0.23/0.43`，保留 10–5 分既有六档半径、三种方位和 0 分地板落点，因此只校正共同圆心，不改变计分规则。
- 验证：领域测试新增视觉圆心回归；自然弹道测试加入后共 43/43 通过。Unity 6000.0.59f2 完整箭环探针通过 10/9/8、7/6/5、重复 10、重复 9 与 0 分五组场景。重复 9 分证据 `docs/screenshot/20260717/level078-arrow-repeat-9-after.png` 中三支箭均位于白色 9 分环。
- 回滚：恢复 `TargetCenterOffsetX/Y` 旧值即可；无需改动计分区间、箭半径、Prefab 或素材。

## 2026-07-17 箭矢自然飞行修正

- 现象：旧实现先把箭设置成固定落地角度，再通过一次 `DOMove` 直线平移到目标；箭从离弦到命中全程姿态不变，看起来像贴图横向移动，而不像真实射箭。
- 修复：新增纯领域 `Level78ArrowTrajectory`。轨迹采用水平匀速的抛物线，弧高按飞行距离的 `8%` 计算并限制在 `0.45..0.9`；运行时每帧同时计算箭尖位置与弹道切线角度，使箭离弦时微微上扬、越过最高点后逐渐下压，并以当时切线角自然插入目标。经视觉评审，飞行时长由 `0.35s` 调整为 `0.45s`，让弹道变化可辨且保持利落；所有分值落点和计分规则保持不变。
- 精确落点：轨迹描述的是箭尖而不是 Transform 中心；每帧旋转后反算 Sprite 右端箭尖的世界偏移，所以抛物线末端仍精确等于原定计分环或 0 分地板点。
- 验证：两个轨迹回归测试覆盖起终点、弧高和连续角度变化，领域测试 43/43 通过。Unity 慢放探针记录角度由 `+11.2° → +1.5° → -9.4° → -13.9°` 连续下压，三张阶段截图与精简日志归档到 `docs/screenshot/20260717/`；恢复正常时长后，完整 5 场景箭环矩阵再次通过。
- 回滚：删除 `Level78ArrowTrajectory`，把 `LaunchArrow()` 恢复为固定旋转后 `DOMove` 即可；无需回退箭靶圆心、分值落点、角色动画、Prefab 或素材。

## 2026-07-20 总分颜色与描边调整（2026-07-22 曾回退，现已恢复）

- 需求：最右侧总分格需要与参与者视觉身份对应；Box 总分使用蓝色 `#2F7EDF`，小恶魔总分使用红色 `#E11B00`，两者增加白色 `#FFFFFF`、`4px` 描边。
- 原行为：8 个计分文字统一为纯白色且没有描边，Box 与小恶魔总分只能依赖所在行区分。
- 新行为：`Level78Ctrl.CreateScoreText()` 只在 `cellIndex=3` 时应用总分样式；上方 Box 总分为蓝色，下方小恶魔总分为红色。六个单箭分仍为纯白且无描边，计分初始化与逐箭累计逻辑不变。
- 描边标定：TMP `outlineWidth` 是归一化 SDF 参数，并不直接等于屏幕像素。旧公式仅使用采样字号、`GradientScale`、`ScaleRatioA` 与运行字号推导出 `0.5263158`，遗漏世界缩放、相机投影和最终屏幕采样，实测明显过粗。第一次校准只把 `outlineWidth` 减为 `0.2631579`，虽然白边总宽约 `4px`，但 TMP 默认把描边居中覆盖字形边界，彩色字芯仍被压细。最终保持字号 76 与 `outlineWidth=0.2631579`，并把独立材质的 `Face Dilate` 设为同值，使白边完整移到原字形外侧；不修改共享字体材质资产。
- 数据与系统边界：不修改 Prefab、计分规则、箭矢、存档、配置、事件、analytics、广告、IAP、RemoteConfig 或平台 SDK。
- 验证：Unity `6000.0.59f2` batchmode 与 GUI Initial Refresh 均通过，三类编译错误为 0；Level 078 Play Mode 已依次复现过粗值、居中 4px 白边压细字芯及外置 4px 白边三种状态。一次仅重载 Editor 探针的旧 Play 对象仍持有 `0.5263158/Face Dilate 0`，完整退出并通过标准入口重启后，未改写材质的只读探针确认正式运行值为 `fontSize=76`、`outlineWidth=0.2631579`、`Face Dilate=0.2631579`。最终 `1920x1080` 截图中彩色主体占彩色与纯白像素约 `66%`，效果图约 `63%`。六个单箭分仍保持白色；Android/iOS 真机显示仍未验证。
- 回滚：删除总分颜色常量、描边换算与 `ConfigureScoreTextStyle()` 调用，即恢复全部分数字段纯白无描边；无需回滚计分数据或 Prefab。

## 2026-07-22 计分板字体与颜色统一

- 需求：计分板标题与数字使用工程内其他关卡的字体效果；标题保持 `50px`、`#E2E8EF`，8 个分数字段保持 `76px`、`#FFFFFF`。
- 原行为：标题由仓库外系统字体 `Wawati SC` 烘焙；六个单箭分为纯白无描边，但 Box 与小恶魔总分分别使用蓝色、红色和白描边。
- 修复行为：标题改由工程 `en.ttf` 烘焙；8 个运行时分数字段继续绑定 Level 006 同款 `en SDF.asset`，并统一为纯白无描边。总分翻牌、计分数据、位置和生命周期不变。
- 影响边界：只修改 `Sp_l78_img_02.png` 的标题区域和 `Level78Ctrl.CreateScoreText()` 的总分视觉覆盖；不修改字体资源、Prefab 引用、计分规则、动画、输入、存档、配置、事件、analytics、广告、IAP、RemoteConfig 或平台 SDK。
- 验证：按两个垂直切片完成静态 RED→GREEN；标题资源与确定性 `en.ttf` 目标逐字节一致，分数样式断言确认字体 GUID、字号 76、基础白色和无总分覆盖均满足。未启动 Unity，Game View、Android/iOS 与设备像素密度仍未验证。
- 回滚：恢复本次 PNG 与 `Level78Ctrl.cs` 两个实现提交即可；无需迁移数据或回退字体资源。
- 后续状态：用户确认两个总分的参与者颜色与白色外描边不应消失；后续修复已精确恢复 `9900ea8d` 删除的总分专用样式，标题 TMP 与六个单箭分不受影响。
- 后续状态：同日后续修复已恢复原始无标题底图，并将“计分板”改为 Prefab 常驻世界空间 `TextMeshPro`；本节烘焙方案仅保留为历史记录，不再描述当前实现。

## 2026-07-20 Box、box_bad 与挡板渲染层级修复

- 需求：Box 玩家和 `box_bad` 小恶魔对手与射箭挡板重叠时，两名角色都必须显示在挡板上层，挡板显示在角色下层。
- 原行为：Box 与运行时克隆的 `box_bad` 根 `SortingGroup` 都是 `SceneLayer/200`；`Sp_l78_img_03` 挡板却是优先级更高的 `BaseLayer/0`，因此挡板会覆盖两名角色的下半身。
- 新行为：仅把 `Level_078.prefab` 内 `Sp_l78_img_03` 改为 `SceneLayer/199`。Box 保持 `SceneLayer/200`，`box_bad` 继续继承同一角色排序，因此两名角色都稳定高于挡板；共享 `Actor.prefab` 与其他关卡不受影响。
- 数据与系统边界：不修改挡板拖拽、双点吸附、角色移动、射箭、计分、配置、存档、事件、资源加载、analytics、广告、IAP、RemoteConfig 或平台 SDK 流程。
- 验证：Unity `6000.0.59f2` batchmode 与 GUI Initial Refresh 通过，编译错误模式为 0；运行时探针确认 `board=SceneLayer/199`、`box=SceneLayer/200`、`box_bad=SceneLayer/200`。修复前后证据分别为 `docs/screenshot/20260720/level078-board-layer-before.png` 与 `docs/screenshot/20260720/level078-board-layer-after.png`，修复后两名角色在挡板重叠区域均完整显示于挡板之上。
- 未验证：Android/iOS 真机不同分辨率、所有可选 Box 皮肤和完整通关流程尚未验证；本次没有改变渲染材质或角色皮肤资源，风险限定为 Level 078 挡板与场景内其他 `SceneLayer` 对象的相对顺序。
- 回滚：把 `Sp_l78_img_03` 恢复为 `BaseLayer/0` 即可回到旧排序；该回滚会重新引入挡板遮挡 Box 与 `box_bad` 的问题。

## 2026-07-20 挡板射箭点左移解锁修复

- 需求：Box 在小恶魔射箭期间可以提前走到远距挡板射箭点；小恶魔完成射箭和归位后，玩家必须能够立即向左离开，再重新进入射箭点取得射箭资格。向右顶住挡板的持续时间不得转化为左移等待时间。
- 原行为：进入 `BoardAdjustment` 后，只要 Box 仍在射箭点容差内，`UpdatePlayerBoundaryAndQualification()` 每帧都会把角色重新对齐到射箭点并调用 `SetMoveInput(0f)`。领域层同时要求提前占位的 Box 必须先离开再重新进入，导致左移输入尚未让角色跨出容差就被站位吸附清零。
- 修复：保留射箭点右侧边界和离开容差；当 Box 的当前输入明确指向左侧时，跳过射箭点对齐与输入清零，让角色先完成离开。角色位置跨出 `shootingPointTolerance` 后仍立即调用 `NotifyBoxExitedShootingPoint()`，后续重新右移进入与射箭锁定规则不变。
- 影响范围：只修改 `Level78Ctrl.UpdatePlayerBoundaryAndQualification()`，并新增 Editor-only `Level78BoundaryProbe`。不修改共享 `ActorCtrl`、`BaseLevelCtrl`、`GamePlayPanel`、Prefab、挡板拖拽、计分、箭矢、配置、存档、资源、全局事件、analytics、广告、IAP、RemoteConfig 或平台 SDK。
- TDD 证据：首轮仅验证小恶魔阶段中途的长按场景时旧代码已可在 `0.034s` 内左移，否定 Rigidbody2D 隐藏位移假设；把同一真实 Prefab 场景延长到小恶魔完整射箭和归位后，旧代码在 `0.300s` 内移动 `0.000`，专项探针退出码 `1`。最小修复后在 `0.040s` 内左移 `0.102`，退出码 `0`；重排离开登记顺序后复跑仍为 `0.040s/0.102`。
- 回归验证：Unity `6000.0.59f2` worktree 就绪门禁通过，batchmode 与 GUI Initial Refresh 的 `error CS`、`Compilation failed`、`Scripts have compiler errors` 均为 0；Level 078 EditMode 测试 43/43 通过；目标文件 `git diff --check` 通过。
- 未验证：Android/iOS 横屏真机触控、不同帧率下的主观移动手感、完整远距失败/近靶胜利流程仍需发布前人工抽查。该修复不增加 `Update()` 分配、资源加载或实例化，性能影响为单次浮点方向判断。
- 回滚：删除 `UpdatePlayerBoundaryAndQualification()` 中左移离开门禁，并移除 `Level78BoundaryProbe`，即可恢复旧行为；该回滚会重新引入挡板调整阶段无法立即左移的问题。

## 2026-07-20 Box 持弓循环速度调整

- 需求：Box 的 `draw_hold` 首轮从原动画的 `35%` 提高到 `80%`；视觉确认后，用户将最终目标调整为保持原动画 `100%` 速度。
- 原行为：`Level78ActorCtrl.BoxReadyHoldTimeScale=0.35f`，Box 的单次持弓循环约为原动画时长的 `2.86` 倍。
- 新行为：常量最终调整为 `1f`，`PlayBoxReadyHold()` 继续写入 `TrackEntry.TimeScale`，单次循环与原动画时长一致；`draw_pull`、`draw_shoot`、小恶魔原速动作链和首个持弓周期输入门禁均不变。
- 数据与系统边界：不修改 Spine 资源、Prefab、射箭次数、计分、箭矢、挡板、配置、存档、事件、analytics 或平台流程。
- 验证：静态检查确认只有 Box 待发 `TrackEntry` 使用 `1f`；Unity 编译与真实 `Level_078` PlayMode 视觉复验记录见对应 PR 审阅单。Android/iOS 真机帧率与触控仍未验证。
- 回滚：把 `BoxReadyHoldTimeScale` 恢复为 `0.35f`，无需回滚资源或数据。

## 2026-07-21 箭簇按插入点确定遮挡顺序

- 需求：多支命中箭在靶面交叠时，遮挡关系必须来自最终箭头插入点，而不是箭序或 Renderer 并列顺序。
- 原行为：`Level78Ctrl.LaunchArrow()` 从同一模板克隆箭，所有箭继承相同 SortingLayer 与 `sortingOrder`；命中后只挂到计分靶共同回弹，没有确定箭与箭之间的前后顺序。
- 新行为（比较规则已被 2026-07-29 取代为水平优先，见下一节）：纯领域 `Level78ArrowDepthOrder` 严格比较最终插入点的实际坐标，Y 越低越靠前；Y 完全相同时 X 越靠左越靠前，不设置近似同层容差。它把 5–10 分、三种箭序形成的 18 个合法命中点映射为唯一 `1..18` 排序偏移。
- 应用时机（飞行层级处理已被 2026-07-29 取代，见下一节）：箭飞行时保持模板原层级；仅在有效命中抵达时把排序偏移加到该箭现有 `sortingOrder`，随后挂到靶面。0 分脱靶不参与，已命中箭不进行逐帧或持续重排。
- 影响文件：`Domain/Level78Duel.cs`、`Level78Ctrl.cs`、`Tests/EditMode/Level78DuelTests.cs`。不修改 Prefab、计分、落点、角度、弹道、回弹、存档、配置、事件、analytics 或平台 SDK。
- 验证：领域测试覆盖最小可表示 Y 差异、同 Y 左侧优先、18 个合法点唯一偏移和脱靶拒绝；Unity 编译、真实 Level 078 视觉证据与当前测试环境复验结果记录在对应 PR 审阅单。
- 回滚：删除 `Level78ArrowDepthOrder`、四个回归测试和命中回调中的 `ApplyHitArrowDepthOrder()` 调用；无需迁移数据或资源，但会恢复箭簇并列排序的不自然表现。

## 2026-07-29 箭簇遮挡顺序改为水平优先，飞行箭临时抬高

- 需求：用户以运行截图确认最右侧命中箭必须覆盖其左侧相邻箭；此前“垂直位置优先、下方覆盖上方”的规则让插入点最低的箭压住了最右箭，观感错误。
- 新行为：`Level78ArrowDepthOrder.Compare` 改为严格比较最终插入点的实际水平坐标，X 越靠右越靠前；X 完全相同时 Y 越低越靠前，仍不设近似同层容差。5–10 分、三种箭序形成的 18 个合法命中点仍映射为唯一 `1..18` 排序偏移，仅前后顺序重排。
- 飞行箭层级：箭离弦时（含脱靶箭）把 `sortingOrder` 临时抬高 `MaxSortingOffset + 1`（=19），高于全部留靶箭；命中靶面时一次性落位为模板基线 + 最终插入点偏移，脱靶落地时落回模板基线。拖尾在抬高后创建，保持比本箭低一级；飞行过程中不做动态换层，已命中箭不进行逐帧或持续重排。
- 影响文件：`Domain/Level78Duel.cs`、`Level78Ctrl.cs`、`Tests/EditMode/Level78DuelTests.cs`。不修改 Prefab、计分、落点、角度、弹道、回弹、入靶遮挡、存档、配置、事件、analytics 或平台 SDK。
- 验证：领域测试改写为最小可表示 X 差异右侧优先、同 X 下方优先，保留 18 点唯一偏移与脱靶拒绝回归；编译与 PlayMode 截图探针证据归档到 `docs/screenshot/20260729/`，完整命令与结果见对应 PR 审阅单。
- 回滚：把 `Compare` 恢复为 Y 优先比较，并删除 `LaunchArrow()` 的飞行抬高与落地落位（恢复命中时 `+=` 偏移）；无需迁移数据或资源。

## 2026-07-21 箭矢入靶遮挡与新箭余振

- 需求：命中箭不能像完整 Sprite 粘贴在靶面上；箭头最前端需要有一小部分进入靶体后不可见，同时保持 9 分、10 分等既有插入点、插入角度和计分结果完全不变。
- 结构调整：每支运行时箭新增以逻辑箭尖为原点的根节点，原箭 Sprite 作为 `Visual` 子节点向后偏移。弹道和最终落点驱动根节点，因此根节点始终代表原有逻辑箭尖；视觉节点的拆分不改 `Level78ArrowLayout`、`Level78ArrowTrajectory` 或计分领域。
- 入靶遮挡：有效命中箭在原插入点创建并启用局部 `SpriteMask`，仅让本箭 `SpriteRenderer` 在圆形遮罩外显示。遮罩半径为 `0.15` 世界单位，对应当前 Game View 约 `14–16px` 的前端深度；5–10 分统一使用同一深度。0 分脱靶保留完整箭 Sprite，不启用遮挡。
- 新箭余振：刚命中的箭以逻辑箭尖根节点为固定支点，按 `+3° → -1.5° → +0.6° → 0°` 在 `0.22s` 内衰减归零，并与既有靶面 `0 → +0.06 → -0.015 → 0` 回弹并行。此前留靶箭只随靶面整体运动，不重新执行各自余振。
- 视觉边界：不增加木屑、闪光等粒子，不增加常驻压痕或裂纹，不移动命中点，也不复制计分靶局部颜色贴片。
- 验证：Unity `6000.0.59f2` 的真实 `Level_078.prefab` 专项探针确认 9 分逻辑箭尖最终误差为 `0.00000`，余振采样为约 `+3° / -1.5° / 0°`，遮罩仅在有效命中启用，0 分脱靶保持遮罩关闭；阶段截图归档到 `docs/screenshot/20260721/`。完整命令与结果见对应 PR 审阅单。
- 回滚：恢复 `LaunchArrow()` 的单节点箭结构，删除局部 `SpriteMask` 与箭尾余振序列，并移除 Prefab 四个参数即可；计分、落点表、弹道、箭簇排序和靶面回弹无需回退。

## 2026-07-20 靶面受击后退回弹

- 需求：有效得分箭抵达计分靶时，靶面与所有留靶箭作为同一整体，沿箭的水平飞行方向短促后退、反向越过基准位置，再精确归位；本轮只增加动效，不新增、复用或调整音效。
- 原行为：箭矢抵达后直接停在世界坐标并提交分数，计分靶没有命中响应。箭与靶之间缺少受力关系，且只移动独立靶面会造成留靶箭短暂悬空。
- 新行为：5–10 分命中箭抵达后先保持世界位置并挂到 `Sp_l78_img_01` 下，使该箭及此前所有留靶箭与靶面共同运动。三段本地 X 位移固定为 `0 → +0.06 → -0.015 → 0`，时长为 `0.06s + 0.06s + 0.10s = 0.22s`；正方向取箭的水平飞行符号，不采纳弹道末端的竖直分量。0 分脱靶不挂到靶面，也不触发靶面位移。
- 结算时序：箭到达后仍立即提交分数；若第三支 Box 箭导致胜负结算，结果展示或失败重置延后到本次 `0.22s` 受击动效结束，避免失败重置在首帧清掉回弹。重开、重试、退出或 Tween 中断会强制恢复记录的靶面基准 `localPosition`，不会累计残余偏移。
- 参数：`targetRecoilDistance=0.06`、`targetReboundDistance=0.015`、`targetRecoilDuration=0.06`、`targetReboundDuration=0.06`、`targetRecoveryDuration=0.10` 均保留在 `Level_078.prefab` 的 Inspector 中可调；没有新增 Animator、粒子、相机震动、缩放、闪白、材质或音频资源。
- 验证：Unity `6000.0.59f2` 依赖门禁成功，C# 编译错误为 0；EditMode `Game.Level078.Domain.Tests` 为 43/43 通过。真实 `Level_078.prefab` 的专项 Play Mode 探针记录命中最大偏移 `+0.0600`、反向回弹 `-0.0150`、最终偏移 `0.0000`、命中箭父节点为靶面；0 分脱靶期间靶面偏移始终为 `0.0000` 且脱靶箭未挂到靶面。回弹两阶段截图归档在 `docs/screenshot/20260720/`。
- 未验证：Android/iOS 真机帧率下的主观力度、连续完整六箭、成功/失败结果界面的人工观感仍待发布前 smoke；自动探针已经覆盖单次有效命中、脱靶排除、共同运动关系和最终归位。
- 回滚：移除 `PlayTargetImpactFeedback()`、相关序列/结算门禁与 Prefab 五个参数，并恢复命中箭不改父节点的旧行为即可；计分领域、落点、抛物线、角色动画、存档和共享资源无需回滚。
## 2026-07-20 挡板近靶终点标定调整

- 需求：把射箭挡板近靶满分点的最终本地坐标从 `(4.65, -2.89, 0)` 调整为 `(2.65, -2.89, 0)`。
- 原行为：`Level78Ctrl.nearBoardLocalX` 与 `Level_078.prefab` 的序列化值均为 `4.65`；挡板从原始远距点 `(-1.99, -2.89, 0)` 吸附至近靶时只改变 X。
- 新行为：脚本默认值与 Prefab 序列化值统一为 `2.65`；Y/Z 继续保持 `-2.89/0`，双点吸附、`0.2s Ease.OutQuad`、近靶不可回退和重开恢复远距规则不变。
- 数据与系统边界：只调整 Level 078 本地 Inspector 标定，不修改领域计分、挡板输入、HUD、箭矢、共享资源、存档、配置、事件、analytics、广告、IAP、RemoteConfig 或平台 SDK。
- 验证：静态核对脚本默认值、Prefab 序列化值、挡板原始 Transform 和运行时仅改 X 的吸附调用链；`git diff --check` 通过。Unity PlayMode、Android/iOS 横屏触控及近靶画面距离仍待人工复验。
- 回滚：把 `Level78Ctrl.nearBoardLocalX` 与 `Level_078.prefab` 的 `nearBoardLocalX` 同步恢复为 `4.65`，无需迁移玩家数据或资源。

## 2026-07-20 蓄力指针缓动调整

- 需求：保持 Level 078 当前 `60° → -50°`、单程 `1s` 的速度配置，把指针运动手感从恒速改为两端平缓、中段较快。
- 原有行为：`StartPointerLoop()` 使用 `Ease.Linear`，指针在整个单程内以恒定 `110°/s` 角速度移动。
- 新行为：同一 `DOLocalRotate` 和无限 `LoopType.Yoyo` 改用 `Ease.InOutSine`；角度范围、单程时长、往返周期、点击冻结位置、归一化计分输入和下一箭重启逻辑均不变。
- 影响边界：只修改 `Level78Ctrl.cs` 的指针 Tween 缓动；不修改 Level 078 Prefab、领域计分、箭矢、角色动画、HUD、共享组件、配置、存档、事件、analytics、广告、IAP、RemoteConfig 或平台 SDK。
- 验证：Unity `6000.0.59f2` batchmode 与 GUI Initial Refresh 均通过，三类编译错误为 0。真实 `Level_078.prefab` PlayMode 探针把单程 Tween 精确跳转到 `0%/25%/50%/75%/100%`，实测角度为 `60.000°/43.891°/5.000°/-33.891°/-50.000°`，与 `Ease.InOutSine` 理论值逐点误差 `0.0000°`；五张阶段图归档到 `docs/screenshot/20260720/level078-pointer-inoutsine-*.png`。完整三箭人工手感、Android/iOS 真机触控与 60fps 仍需复验。
- 回滚：把 `StartPointerLoop()` 的 `.SetEase(Ease.InOutSine)` 恢复为 `.SetEase(Ease.Linear)` 即可，无需迁移数据或资源。

## 2026-07-21 箭矢力量感与距离派生时长

- 需求与基线：远位和近位原先统一使用 `0.45s`，挡板改变射击点后实际水平距离接近两倍，但停留时间不变；箭矢因此在近位显得更慢，且飞行阶段没有速度残迹。
- 新行为：双方共用 `27.65625` 的恒定水平箭速。每箭根据释放点与实际落点的水平距离计算时长；Prefab 基准坐标下远位约 `0.32s`、近位约 `0.15s`。竖直抛物线、箭身切线旋转、计分和落点不变。
- 拖尾：`LaunchArrow()` 在箭尾创建 Level 078 本地 `TrailRenderer`，使用 `Level078ArrowTrail.mat` 暖白材质、`0.10s` 残留和 `0.10` 起始宽度；命中或脱靶时停止生成，残迹自然消散。双方走同一入口和参数。
- 数据与系统边界：只修改 Level 078 脚本、Prefab、本地材质、领域测试和 Editor 探针；不新增配置、存档、全局事件、analytics、广告、IAP、RemoteConfig、音频、镜头震动或平台 SDK 链路。
- 验证：Unity `6000.0.59f2` worktree 门禁最终通过，编译错误模式为 0；`Game.Level078.Domain.Tests` 为 `49/49` 通过；`-level78FlightForceProbe` 验证远位 `0.3210s`、近位 `0.1362s`、飞行中拖尾存在且抵达后停止，并归档 `docs/screenshot/20260721/level078-arrow-flight-force-trail.png`。用户随后确认人工手感通过；Android/iOS 真机仍未验证。
- 回滚：恢复固定 `arrowFlightDuration=0.45`，移除 `Level78ArrowFlightTiming`、拖尾参数、材质与创建逻辑即可；不需要迁移玩家数据，也不影响靶面受击回弹。

## 2026-07-21 行动交接与比分结果播报

- 需求与旧行为：小恶魔归位后原先直接开放 Box 玩家阶段；Box 第三箭完成后直接触发胜负，成功还依赖右侧 `door_0`。现改为复用工程 `HintBubble` 角色讲话窗口，并删除右侧通关门。
- 行动交接：小恶魔三箭结果停留、箭组渐隐和归位全部完成后显示“到你了”1.5 秒。该播报只提示阶段交接，不阻塞挡板调整、Box 移动或重新就位取得射箭资格。
- 比分结果：第三支 Box 箭确定最终比分并完成靶面受击反馈后，立即锁定操作；30 分显示“你赢了”，远距最高 27 分显示“你输了”。气泡持续 1.5 秒后才分别进入 `SuccessStrightly()` 无右门成功链或 `GameResetEvent(GameResetType.Failed)` 失败链。
- 分数不变量：小恶魔总分只能为 28/29，Box 远距最高 27、近距固定 30，因此不存在同分；未增加平局状态或文案，领域层既有相等即失败分支保留为不可达防御兜底。
- 窗口复用：运行时实例化 `Assets/Game/Resources/HintBubble.prefab`，继续使用其跟随、入场、文字揭示和关闭动画。气泡保留 Prefab 默认的 `Twinkly SDF` 主字体，中文由该字体已有的 fallback 表覆盖；Level 078 不再直接替换为 `korean SDF` 或其材质，字号上限仍为 `4`。气泡实例排序层对齐计分板并保留高顺序，保证三字完整显示在计分板前方。三句分别使用 `S_LevelTips_Des_level_78_2`、`S_LevelTips_Des_level_78_3`、`S_LevelTips_Des_level_78_4`；飞书多语言表当前只登记简体中文，不修改共享 `HintBubble` 或本地 `I2Languages.asset`。
- Prefab：删除右侧 `door_0` 完整实例，`BaseLevelCtrl.successAnim` 清空；左侧 `StartDoor` 与 `enterDoorAnim` 原引用保留，开场入场链不变。只保留 `demonSpeechDuration=1.5` 本地接线，不再保存关卡私有字体引用。
- 生命周期：播报关闭与胜负延迟使用不受 `Time.timeScale` 影响的 DOTween；结算回调继续校验 `sessionVersion` 和 `Settled`。重开、退出、禁用或销毁会 Kill 延迟、关闭气泡并清理本局状态，避免旧回调进入新局。
- 验证：Unity `6000.0.59f2` 首轮 GUI 启动停在 `Level_078` Edit Mode，实际 C# 编译错误为 0；同步集成基线后 `Game.Level078.Domain.Tests` 为 `49/49` 通过。真实 `Level_078.prefab` 的 `-level78OutcomeDialogueProbe` exit `0`，确认“到你了”不阻塞操作、胜利约 `1.413s` 帧容差后触发、失败约 `1.501s` 后触发、右门不存在，三张同步后可读截图归档于 `docs/screenshot/20260721/level078-*-dialogue.png`。2026-07-22 字体对齐修正通过 `-level78SpeechFontProbe` 和完整播报探针复验：三句运行时主字体均为共享 `Twinkly SDF`、各有 12 个文字顶点，新截图归档于 `docs/screenshot/20260722/`。
- 未验证：未执行 Android/iOS 真机、构建、不同语言或全流程人工点击；其他语言翻译及飞书表到本地 CSV/I2 的导入留给后续同步流程。
- 回滚：恢复右侧 `door_0` 与 `successAnim`，移除本地三句气泡、字号/时长处理及延迟触发，恢复第三箭后直接胜负；左侧入场门、计分领域、存档与配置无需迁移。若只回滚 2026-07-22 字体对齐，可恢复 `demonSpeechFont=korean SDF` 接线与运行时字体/材质覆盖。

## 2026-07-22 失败结算恢复普通待机

- 原行为：第三支 Box 箭确定落败并开始“你输了”播报时，结算会取消当前射箭动作并清空 Spine 轨道，但没有显式播放稳定动画；若箭矢先于 `draw_shoot` 收尾抵达，Box 可能停在持弓或射箭骨骼残姿。
- 新行为：只在失败结算分支终止射箭流程后循环播放普通 `idle`；Box 继续停在当前射箭点并保持输入锁定，1.5 秒播报结束后仍派发既有 `GameResetEvent(GameResetType.Failed)`。
- 隔离边界：胜利结算、小恶魔动作、前三箭准备与发射、计分、箭矢、Prefab、共享 `ActorCtrl` / `ActorSpineCtrl` 和 Spine 源资源均不修改。
- 验证结果：Unity `6000.0.59f2` worktree 门禁通过，batchmode/GUI 编译错误模式均为 0；增强后的 outcome dialogue Play Mode 探针 exit `0`，在“你输了”可见期间确认 `boxAnimation=idle`、`loop=true`、`ctrlable=false`，失败约 `1.501s` 后重置。Level 078 领域回归 `49/49` 通过，失败截图归档于 `docs/screenshot/20260722/level078-failure-dialogue.png`。首次 `-nographics` 运行因既有截图步骤触发原生渲染崩溃，一次图形重试又因既有胜利时序采样 `1.241s` 低于探针容差失败；原命令再次重试后胜利 `1.403s`、失败 `1.501s` 均通过，详细边界见本任务 PR 审阅单。
- 回滚：移除失败分支的 `ShowFailureSettlementIdlePose()` 调用并恢复统一 `CancelLevel78Action(false)`；无配置、存档、资源或数据迁移。

## 2026-07-22 双收集星接入

- 需求与原有基线：`Level_078.prefab` 原先没有关卡收集星。现复用工程既有 `Postcard_Star.prefab` 增加两颗常驻星，不新增收集脚本、事件、存档字段或阶段显隐逻辑。
- 空间语义：两颗星都固定在 Level 078 根节点和 Box 的水平行走路径上，不随挡板移动、无需跳跃。左星位于挡板初始位置左侧，本地坐标为 `(-5.8, -2.06, 0)`，开局即可收集；右星位于挡板初始位置右侧、本地坐标为 `(0, -2.06, 0)`，初始被挡板隔开，挡板移动后可在走向新射箭点时收集。
- 可达边界：挡板初始本地 X 为 `-1.99`、近靶位置为 `2.65`，移动后的射箭点约为 `1.80`；右星满足 `-1.99 < 0 < 1.80 < 2.65`，不会落到挡板移动终点右侧的不可达区域。“左侧/右侧”只描述水平空间与解谜门槛，不表示渲染图层前后关系。
- 收集编号与链路：左星沿用默认 `Postcard_StarNum.One`，右星显式设为 `Postcard_StarNum.Two`。触发后继续复用 `Postcard_Star → 当前关卡 ID + index → PostcardManager → 背包星星奖励 → PostcardGetStarEvent → HUD` 的既有链路，本任务未修改该共享实现。
- 验证：静态检查确认真实 Prefab 仅有两处 `Postcard_Star` 引用、根节点含两颗子实例且仅右星覆盖 `index=Two`。Unity `6000.0.59f2` 启动门禁通过；真实 `Level_078.prefab` 的 `-level78PostcardStarProbe` exit `0`，确认两颗触发器启用、编号为 `One/Two`、玩家 Collider 在当前行走高度均可重叠，并验证左星完整位于初始挡板左侧、右星完整位于初始挡板右侧且位于移动后挡板与射箭点左侧。运行截图见 `docs/screenshot/20260722/level078-two-stars-runtime-layout.png`。
- 验证边界：本次 `V2` 证明真实 Prefab 的运行时层级、触发器、编号、水平可达关系和视觉摆放；未从正式关卡入口人工走完两次收集后的存档、背包与 HUD 反馈，也未执行构建或 Android/iOS 真机验证。
- 回滚：删除 `Level_078.prefab` 新增的两个 `Postcard_Star` 实例及专项探针，并回退本节与对应 Context/风险说明；共享收集链、配置和既有玩家数据不需要迁移。

## 2026-07-22 人工场景布局调整合并

- 背景：用户在指定关卡 PlayMode 会话期间手动保存了 `Level_078.prefab`，并明确要求把这些修改合并。该版本基于较早的集成基线；同步后的新基线同时加入了计分板常驻 `ScoreBoardTitle`，因此使用三方合并保留两侧内容，不以旧 Prefab 整体覆盖新基线。
- 场景调整：计分板底图 `Sp_l78_img_02` 的本地 Y 从 `2.74` 调整为 `2.15`，并增加 `SceneLayer/0` 的 `SortingGroup`；计分靶 `Sp_l78_img_01` 增加 `SceneLayer/50` 的 `SortingGroup`。新基线的 `ScoreBoardTitle` 世界空间 TMP 对象、字体、颜色和排序继续保留。
- 其他序列化内容：保留人工版本中的 Actor Prefab 嵌套覆写、提示文本 `Well... / Show some respect.` 及其位置、`CommonBg` 本地位置覆写；两颗 `Postcard_Star` 的 `One/Two` 编号和 `(-5.8, -2.06, 0)` / `(0, -2.06, 0)` 空间语义保持不变。
- 数据与系统边界：只修改 `Assets/Game/Prefabs/Level/Level_078.prefab` 的场景层级、Transform、Renderer/SortingGroup 和既有 Prefab override；不修改 C#、配置表、存档结构、资源地址、全局事件、analytics、广告、IAP、RemoteConfig 或平台 SDK。
- 验证边界：三方合并后静态确认冲突标记清零、YAML 对象锚点无重复、本地 `fileID` 无悬空，并保留新基线 `ScoreBoardTitle` 与人工 SortingGroup。最终合并版通过 `./launch_unity_worktree.command --level Level_078`，batchmode/GUI 三类编译错误均为 0，`READY` 命中 `Loading.unity`、`GamePlayPanel_026_78` 和 `input=mouse-only`；同步基线后未再次进入 Play Mode 人工目视，也未执行 Android/iOS 真机或构建验证。
- 回滚：只回退本节对应的 `Level_078.prefab` 人工差异即可；新基线的计分板 TMP 标题与无标题底图不随本次回滚撤销，无数据迁移。

## 2026-07-24 Jump/Aim 面板控制收口

- 需求与原有基线：原实现新增 `Level78GamePlayPanel : GamePlayPanel`，由该子类查找 Jump/Aim、切换显隐、监听暂停并通过 `AimPressed` 把输入转交 `Level78Ctrl`。项目既有关卡已存在由关卡控制器取得当前玩法面板按钮并直接绑定的做法；用户确认本关可由 `Level78Ctrl` 直接隐藏 Jump，因此不再保留专用面板子类。
- 影响文件与资源：修改 `Level78Ctrl.cs` 和 `GamePlayPanel_026_78.prefab`；删除 `Level78GamePlayPanel.cs` 及其 `.meta`。Prefab 中已有的 `aim_btn`、Jump 引用、人工序列化调整和其他 UI 资源均保留，共享 `GamePlayPanel.cs` 不修改。
- 新行为：`Level78Ctrl` 首次取得 `PlayCtrl.CurrentGamePlayPanel` 后缓存 `Root/jump_btn` 与 `Root/aim_btn`，直接绑定 Aim 的 `OnClickDown`。`SetAimMode(...)` 同步控制 Jump/Aim 与蓄力表；退出射箭态、重开、禁用或销毁时恢复 Jump、关闭 Aim、停止指针并解绑按钮。
- 输入与事件流：Aim 输入现在直接进入 `Level78Ctrl.OnAimPressed(...)`，随后仍由 `Level78Duel.TryStartBoxShot(...)` 决定是否接受，不改变忙碌门禁、指针取值、震动、动画、箭矢和计分。暂停保护迁移到 `Level78Ctrl`，复用既有 `GameResumeEvent` 与 `SwitchGameStateEvent`；没有新增全局事件定义，也没有接入 `GamePanel_UseBtnCommonFeature` 的 `SwitchObjActiveEvent`。
- 数据与平台边界：不修改 DB/save、Luban/运行时配置、资源地址、analytics、广告、IAP、RemoteConfig 或平台 SDK；Android/iOS 横屏按钮位置与触控目标沿用原 Prefab。
- 静态验证：当前面板已绑定共享 `GamePlayPanel` 脚本 GUID，活动 `Assets/Game` 中旧 `Level78GamePlayPanel` 类名与脚本 GUID 引用清零；`aim_btn` 和共享 Jump 序列化引用仍存在。`Level78Ctrl` 只在面板引用为空时解析按钮，不再每帧 `GetComponent<Level78GamePlayPanel>()`。
- 验证缺口：首次 C# 构建因生成工程尚未适配目录重命名而出现旧路径/重复类型错误，因此不能作为本次代码的有效编译通过证据。尚未执行重命名完成后的 Unity Prefab 导入、Level 078 PlayMode、暂停期间 Aim、重开/退出、完整三箭或 Android/iOS 真机 smoke。工作区开始前 `GamePlayPanel_026_78.prefab` 已存在 Unity YAML 行尾空格，因此包含该 Prefab 的 `git diff --check` 仍会报告这些既有格式项。
- 回滚：恢复 `Level78GamePlayPanel.cs/.meta`，把 `GamePlayPanel_026_78.prefab` 根脚本 GUID 切回专用子类，并将 `Level78Ctrl` 恢复为订阅 `AimPressed`；不需要回滚射箭领域、场景 Prefab、配置或玩家数据。

## 2026-07-29 射箭动作音效接入与总分单次弹出揭晓

- 需求背景：l78 音效目录中 `drawBow`、`arrowWhoosh`、`targetHit` 三个音频早已注册进 Sounds Good 合集但代码从未播放；同时总分的逐箭累加翻牌动效与 `finalScore` 音效感觉不匹配，用户确认改为直接显示总分。
- 射箭动作音效：`Level78ActorCtrl` 在 `draw_pull` 拉弓动画启动时播放 `drawBow`。Box 玩家在就位拉弓与每箭结束后 `CompleteArchery` 重新执行完整准备流程的拉弓时各播一次，保证每次射箭之前都有一次拉弓动作与拉弓音效（第三箭结束后、结算前的归位拉弓也照常播放）；小恶魔每箭完整重播 `draw_pull`，同样每箭各播一次。`Level78Ctrl.LaunchArrow` 在箭矢离弦起飞时播放 `arrowWhoosh`、在箭到达靶面且非脱靶时播放 `targetHit`（脱靶不播）。双方规则一致，均火后即忘、与动画/飞行时长脱钩。
- 总分单次弹出揭晓：第三格单次得分弹出完成后，总分格以与单次得分完全相同的缩放弹出参数（`SingleScorePopStartScaleMultiplier` × `ScoreFeedbackDuration`、`Ease.OutBack`）一次性直接亮出最终总分（含三箭全脱靶的 0 分），并在弹出起点播放 `finalScore`；最终总分仍在第三箭提交时写入 `committedScoreTexts`，打断收口安全网不变。
- 翻牌机制移除：删除三步滚动翻牌（`PlayTotalScoreRevealSequence/Step`）、旧值淡出专用文本 `outgoingTotalScoreTexts`、`ResetTotalScoreFlipVisual`、`SetScoreTextAlpha`、`TotalScoreFlipTravelRatio` 常量与只被翻牌读取的 `shotScores` 数组；需要时从 git 历史找回。
- 术语同步：`docs/contexts/levels/078/CONTEXT.md` 新增 总分单次弹出揭晓 / 射箭动作音效 词条，并更新 总分 / 逐箭即时计分 / 差异化计分反馈 / 脱靶 四个词条。
- 边界：不修改 Prefab、场景、Spine 资源、领域计分逻辑、配置与玩家数据；不新增音效资源或 Sounds Good 注册项。
- 回滚：从 git 历史恢复 `Level78Ctrl.cs` 与 `Level78ActorCtrl.cs` 对应版本即可，无数据迁移。

## 2026-07-29 胜负结算 Box 手部残姿修复

- 问题：胜利与失败结算播报期间，Box 的手保持拉弓手型等射箭残姿，明显不是共享套件的正常姿态。
- 根因（静态证据，`Box.json` 关键帧对比）：射箭三动画 key 了 `Soft_arm_L/R`、`Soft_arm_move_L/R/R2`、`Soft_arm_Rot_L/R` 等手臂骨骼；失败分支播放的 `idle` 不 key `Soft_arm_Rot_L/R`，手臂旋转残留；胜利分支只清 Spine 轨道不播任何稳定动画，Box 定格在 `draw_shoot` 残姿。
- 修复：两个结算姿态统一改为「取消射箭动作 → `Skeleton.SetToSetupPose()` 还原骨骼与插槽 → 播放稳定动画」；失败继续循环 `idle`，胜利改为循环共享 `celebrate`（与 Level 083 胜利庆祝一致）。还原逻辑实现为 `Level78ActorCtrl` 私有方法 `ResetSpineToSetupPose()`（本组件本地访问 `SkeletonAnimation`），并新增 `ShowVictorySettlementCelebratePose()`；`Level78Ctrl.CompleteSettlement` 胜利分支改调新方法。
- 边界：不修改共享层（`ActorSpineCtrl`、共享 `ActorCtrl`）、共享 `Box.json` 等 Spine 源资源、Prefab、领域逻辑与其他关卡；全部改动收在 Level 078 本关文件内。
- 回滚：恢复 `Level78Ctrl.CompleteSettlement` 胜利分支为 `CancelLevel78Action(false)`，并移除 `ResetToSetupPose` 调用；无数据迁移。

## 2026-07-29 破空音跳过前导静音与射箭动作音效提音量

- 需求背景：用户在 preview 人工试听确认破空音与打靶命中音不可闻（拉弓音可闻）。实测响度包络：`arrowWhoosh.ogg` 自带约 0.54s 前导静音（发声段 0.54–0.74s），箭飞行仅 0.2–0.4s，破空音出声前箭已命中，被命中音与计分音掩盖；`targetHit` 无前导静音，但与近两倍响度的 `normalScore`（默认 0.4 音量）同帧播放被掩蔽。
- 修改边界：用户明确只允许改 Level 078 自身文件，不改 Sounds Good 插件与共享 `AudioManager`；音频资源与 Sounds Good 注册保持不变。
- 破空音起播偏移：`Level78Ctrl` 新增 `arrowWhooshClip` 序列化字段（`Level_078.prefab` 接线到既有 `l78/arrowWhoosh.ogg`），由本关自带 `AudioSource` 以 `time = 0.54s` 从发声点起播、音量 1.0，受 `DB.GameSetting.SfxOn` 开关控制；双方每箭各一次、火后即忘、与飞行时长脱钩的规则不变。不再经 `AudioManager.PlaySound("arrowWhoosh")`。
- 命中音提音量：`targetHit` 仍走共享 `AudioManager.PlaySound`，音量从默认 0.4 提至 1.0（使用该接口既有可选参数，无共享层改动）。
- 影响文件：`Level78Ctrl.cs`、`Level_078.prefab`。不改计分、落点、弹道、遮挡、回弹、存档、配置、事件、analytics 或平台 SDK。
- 验证：编译与 EditMode 域测试回归；音频实际听感由用户在 preview 人工确认（证据缺口记录在 PR 审阅单）。
- 回滚：恢复 `Level78Ctrl.cs` 与 `Level_078.prefab` 对应版本即可；无数据迁移，共享层与音频资源始终未改。

## 2026-08-05 运行时小恶魔禁止继承玩家 Skin

- 需求与原行为：Level78 的小恶魔由 `Level78Ctrl.CreateDemon()` 直接克隆当前玩家 Actor。玩家已经加载到 `back`、`face`、`body` 挂点下的 SkinPart 子物体会随克隆体一起复制，导致生成的小恶魔仍穿戴玩家部件 Skin；原逻辑只调用 `SetSkin("box_bad")`，只能重设 Spine 全身 Skin，不能清理这些独立子物体。
- 修复行为：创建小恶魔后立即禁用其子层级全部 `ActorSkinBinding`，清空三个带 `BoneFollowerAutoBinder` 或 `BoneFollower` 的 SkinPart 挂点子物体，再设置本关固定 `box_bad` 全身外观。敌人不再继承玩家当前或后续切换的 Skin。
- 影响范围：只修改 `Level78Ctrl.cs` 的运行时小恶魔初始化；不修改玩家 Actor、共享 `ActorSkinBinding`、`ActorCtrl`、Prefab、配置、存档、资源路径、事件、analytics 或平台 SDK，也不改变射箭、移动、计分与结算流程。
- 验证：`dotnet build Assembly-CSharp.csproj --no-restore --nologo --verbosity:minimal` 通过，结果为 `0` error、`1119` 个项目既有 warning；定向 `git diff --check` 通过，仅有工作区 LF/CRLF 提示。
- 未验证：尚未在 Unity PlayMode 分别装备 back、face、body 与全身 Skin 进入 Level78 观察生成帧，因此最终可见外观、重开和 Android/iOS 横屏表现仍为 `uncertain`。
- 回滚：恢复 `CreateDemon()` 直接调用 `demon.SpineCtrl?.SetSkin(DemonSkinName)`，移除三个本地清理方法和对应命名空间即可；无需迁移数据。
