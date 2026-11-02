# Level 083“动人的音乐”玩法实现记录

> 2026-07-22 退役声明：本文较早章节中的指定关卡路由、`READY`、`SAVE_SNAPSHOT`、
> `SAVE_RESTORED` 和鼠标专用会话均为历史验证事实；相关仓库能力与代码已删除，不是当前入口。

## 需求与基线

Level 083 原有 `Level_083.prefab` 只有已对齐的环境、入场角色和静态玩法素材，没有演奏台触发、乐器选择、谱面判定、实体音符或胜负收口。本次按 `docs/contexts/levels/083/CONTEXT.md` 已确认合同实现完整玩法，不接入 `LevelData`、`LevelSort`、本地化和正常关卡入口。

## 影响范围

- `Level_083.prefab`：绑定玩法编排器、演奏台 trigger、三个乐器选项、8 个预置谱面槽、3 种音符模板和 Level 083 独立 Box Bad。
- `Domain/Level83Performance.cs`：固定三份 8 步谱序，实现逐格判定、首错锁定与全对后等待重选门禁（TryEnterReselection/TryReselect）。
- `Level83GameplayCtrl.cs`：订阅既有左移、右移、跳跃事件，编排乐器、谱面、音符和胜负。
- `Level83MusicNote.cs`：每枚音符独立 `0.8s + 0.2s` 生命周期，拖拽期间暂停计时、每次释放后先下落（途中不消亡）、落定后重新计满，渐隐窗口内可拖拽救回（0.1s 恢复全亮与原尺寸），支持释放下落、落空后重新抓取和首次有效命中。
- `Level83RivalCtrl.cs`：本地承载 `idle/want/angry/walk01/die` 与冲撞撞飞，不依赖 `Level46EnemyCtrl`。
- `Tests/EditMode/Level83PerformanceTests.cs`：锁定谱序、错误门禁和轮次复位。

## 新行为与运行时流

1. 初始隐藏乐器气泡和谱面；主角进入演奏台显示三个选项，未选择离开时收回。
2. 选定后把乐器移到手部锚点，主角播放 `play_guitar` / `play_harp` / `play_sax`，Box Bad 播放 `want`，并锁定主角与乐器。
3. 每格正确立即变绿并生成一枚当前乐器对应音符；8 格全对后保留全绿反馈进入完成评价，评价气泡收起后解除演奏锁定、谱面槽全绿原样从右到左反向错峰退回、三个乐器气泡重现，玩家必须重新选定乐器（可含原乐器）后按新谱序开始下一轮。
4. 首次错误立即停止输入；Box Bad 完整播放 `angry` 后以 `moveSpeed=11` 使用 `walk01` 向左冲撞。直接碰到主角后等待 `0.1s`，播放 `Knock_Explosion` 和 `trap`，执行 `Knockback((-0.5, 0.5), 50)` 与 `360°/0.5s` 持续翻滚，最后由本关 `FallArea` 触发标准失败。
5. 音符在 Box Bad 头顶上方释放后命中，冻结谱面和所有音符交互，消耗命中音符，主角播放 `celebrate`，Box Bad 播放 `die`；`die` 完成后进入 `BaseLevelCtrl.SuccessStrightly()` 标准成功。

`BaseLevelCtrl` 仍会每帧调用角色控制器刷新移动动画；Level 083 现通过关卡专用 `Level83ActorCtrl.HandleAnimators()` 在演奏/庆祝期间隔离通用动画刷新，不再依赖玩法控制器逐帧补偿或共享动画托管 API。

## 验证

- Unity `6000.0.59f2` worktree 启动门禁通过，batchmode 和 GUI Initial Refresh 未出现 C# 编译错误。
- Prefab API 构建日志命中 `LEVEL83_GAMEPLAY_BUILD_SUCCESS`，确认 8 槽、3 选项、3 模板、必需引用、Missing Script 和 `Level46EnemyCtrl` 隔离。
- EditMode：`Level83PerformanceTests` 结果 `6/6 Passed`。
- 真实 `Level_083.prefab` PlayMode 探针命中 `LEVEL83_GAMEPLAY_PLAYMODE_SUCCESS`，覆盖初始显隐、选择动画、8 格正确与复白、8 枚音符、命中成功，以及错误后 `angry → 冲撞 → 击飞 → FallArea 失败`。
- 运行态截图 `docs/screenshot/20260720/level083-gameplay-guitar-progress.png` 为 1920×1080，人工检查确认吉他已到手部、前 5 格绿色与剩余白色谱面可见、音符可见且 Box Bad 处于 `want` 表现。首版截图同时暴露谱面槽位于 `Default` sorting layer 而被 `SceneLayer` 外框遮挡，已把 8 个槽位统一到 `SceneLayer/order 110` 并重拍通过。

## 剩余风险与回滚

正常入口、存档/解锁、本地化、教程、Android/iOS 真机宽高比、触控手感、音效和性能未验证。回滚时恢复 `Level_083.prefab`，删除 `Level_83` 下本次新增的运行时/测试文件及本地文档；本次无配置和玩家数据迁移。

## 2026-07-21 演奏台触发与视觉修正

### 需求与原行为

用户手动预览要求开局由玩家控制 Box 走到 `Sp_l83_tz_01.png` 演奏台，站上台后才出现三个乐器气泡；气泡需按策划示意图弹出，三种实体音符需保持一致的视觉大小。原演奏台 trigger 宽度为 `2.4`、中心为 `x=-4.97`，左侧边界覆盖了 Box 入场位置，导致开局已被判定在台上；候选也只做瞬时 `SetActive`，红/黄音符模板的可见高度小于蓝色音符。共享 HUD 移动按钮原使用 `OnClickEnter`，指针已在按钮内时再按不会重新触发；Unity Game View 首次获焦还可能只送达 `PointerUp`。

### 新行为与数据流

1. Box 初始保持 `BaseLevelCtrl` 的可移动合同；未站上演奏台时，候选气泡和谱面均隐藏。
2. 演奏台 trigger 调整为中心 `x=-4.7`、宽度 `1.6`，不再覆盖左门入场点；`Level83StageTrigger` 报告首次进入后，`Level83GameplayCtrl` 激活候选根节点，并以 `0.06s` 间隔依次调用三个 `Level83InstrumentOption.PlayShow()`。
3. 每个候选只缩放自身气泡与乐器子图，使用 `0.24s / Ease.OutBack` 从零回到 Prefab 原始大小；不缩放位于关卡原点的候选根节点，避免视觉位置漂移。
4. 未选择时离开演奏台会立即隐藏并复位。选择乐器后仍按原合同锁定 Box、把所选乐器移动到手部并进入谱面；隐藏候选时保留所选乐器的独立移动 Tween。
5. 蓝/红/黄音符模板高度分别约为 `1.89×0.48=0.907`、`1.89×0.48=0.907`、`1.80×0.50=0.900`，统一到约 `0.9` 世界高度。
6. 当时曾把 `GamePlayPanel` 左右移动改为 `CustomButton.OnClickDown` 并增加首次获焦脉冲；该共享改动后来确认破坏了其他关卡沿用的 `OnClickEnter` 输入合同，已由下述“共享 HUD 输入合同回归修正”替代。

### 影响文件、验证与回滚

- `Level83GameplayCtrl.cs`：平台状态去重、气泡错峰编排和选择隐藏保护。
- `Level83InstrumentOption.cs`：气泡弹出、立即复位与 Tween 生命周期。
- `Level_083.prefab`：演奏台 trigger 范围及红/黄实体音符模板缩放。
- `GamePlayPanel.cs`：该阶段曾调整共享 HUD 左右按键；最终行为以文末“共享 HUD 输入合同回归修正”为准。
- Unity `6000.0.59f2` 启动门禁通过，手动预览日志确认 `hudLevel=83`、`actorCtrlable=True`、`rightDownListeners=1`；1920×1080 初始状态证据为 `docs/screenshot/20260721/level083-stage-trigger-manual-ready.png`。EditMode `Level83PerformanceTests` 结果为 `6/6 Passed`。
- Android/iOS 真机触控、宽高比、完整谱面与拖拽命中仍需 smoke。回滚时恢复上述三份脚本与 Prefab 参数；不涉及配置、存档或玩家数据迁移。

## 2026-07-21 共享 HUD 输入合同回归修正

### 根因与最终合同

Level 083 手动验证发现左右按钮无响应，而跳跃和其他屏内按钮正常。提交历史与 Prefab 绑定确认：共享 HUD 原有合同是左右按钮通过 `CustomButton.OnClickEnter` 开始持续移动，并通过 `OnClickUp` 或 `OnClickExit` 停止；跳跃通过 `OnClickDown` 派发单次事件。此前为 Level 083 引入的 `OnClickDown + 0.1s` 脉冲改写了共享合同，是本次回归来源。

本次删除 Level 083 特化的按压状态、脉冲版本和焦点补偿，恢复所有复用 `GamePlayPanel` 的关卡共同使用的历史绑定：

- 右移：`OnClickEnter → PlayerRightMoveEvent`，`OnClickUp/OnClickExit → PlayerMoveCancelEvent`。
- 左移：`OnClickEnter → PlayerLeftMoveEvent`，`OnClickUp/OnClickExit → PlayerMoveCancelEvent`。
- 跳跃：继续保持 `OnClickDown → PlayerJumpEvent`，没有修改事件或 Actor 跳跃逻辑。
- Level 083 本地 `Level83GameplayCtrl`、`Level_083.prefab`、`ActorCtrl` 和 `BaseLevelCtrl` 均未修改。

### TDD 与验证

- 有效 RED：真实 `Level_083.prefab + GamePlayPanel_000` 中执行右移 `PointerEnter`，修复前 `ActorCtrl.XInput` 为 `0`，单测 `0/1 Passed`。
- GREEN：同一用例修复后 `1/1 Passed`；完整 `GamePlayPanelInputTests` 覆盖右移、左移、离开停止和跳跃按下，结果 `4/4 Passed`。
- 既有 `Level83PerformanceTests` 结果 `6/6 Passed`，确认共享输入修正未改变 83 关谱面领域规则。
- Unity `6000.0.59f2` worktree 启动门禁已确认 `Level_083`、`GamePlayPanel_000` 与 EditMode READY，C# 编译错误模式为 0。

该修正影响所有复用共享 `GamePlayPanel` 的关卡，但行为是恢复既有共同合同。Android/iOS 真机多点触控与代表关卡 smoke 仍需补验；回滚只需恢复 `GamePlayPanel.cs` 及本次输入测试/文档，但会重新引入已复现的 Level 083 左右无响应问题。

## 2026-07-21 指定关卡鼠标专用启动

### 需求、基线与新行为

通过 `./launch_unity_worktree.command --level Level_083` 准备人工预览时，目标是只使用共享 HUD 的鼠标/触控按钮。原指定关卡会话仍允许按 `Q` 打开 `ActorCtrl` 的 Editor 键盘轮询；开启后，A/D 未按下的下一帧会派发 `PlayerMoveCancelEvent`，从而可能覆盖 HUD 已派发的持续移动，Space 也会绕过鼠标入口派发跳跃。

指定关卡会话现在复用既有 `-brainpunkPlayModeLevel` 参数建立鼠标专用合同：`EditorPlayModeLevelOverride` 暴露当前会话状态，`ActorCtrl.HandleInput()` 在该状态下跳过 Q/A/D/Space Editor 键盘路径，`GamePlayPanel` 的鼠标/触控事件和正式玩法事件链保持不变。Editor 路由 READY 标记增加 `input=mouse-only`，启动脚本只有同时命中目标关卡和该输入模式才判定指定关卡就绪；普通 Unity 启动与非指定关卡运行不进入该分支。

入口在打开 GUI 前删除精确的临时 `Editor.log`，再等待当前进程重新生成 Initial Refresh 与 READY，防止重复启动时把上一会话的标记误判为本轮通过。

同步基线后的复验进一步暴露：第三方包刷新期间首次 `delayCall` 命中 `EditorApplication.isUpdating` 时，把回调重新挂回同一轮一次性队列可能丢失，表现为 Editor 健康但始终没有 READY。路由等待改为持续订阅 `EditorApplication.update`，编译/刷新结束后执行一次准备并立即解除订阅。

### 影响、验证与回滚

- 影响文件：`launch_unity_worktree.command`、`BrainPunkPlayModeLevelRouter.cs`、`EditorPlayModeLevelOverride.cs`、`ActorCtrl.cs`、`GamePlayPanelInputTests.cs`、启动 Skill 与共享 Context；未修改 `Level_083.prefab`、`GamePlayPanel.cs`、事件定义、正式配置、存档或平台 SDK。
- Unity `6000.0.59f2` 门禁恢复 53 个 embedded packages、Git package cache `4/4` 和 GF `635506c9...`；同步基线及修复竞态后的最终唯一入口退出码为 0，batchmode 与 GUI 编译错误模式为 0。新会话 GUI PID `76471` 重新生成 `Editor.log`，先完成 Initial Refresh，再输出 `READY target=Level_083 state=EditMode scene=Assets/Game/Scenes/Loading.unity input=mouse-only panel=GamePlayPanel_000`。
- `GamePlayPanelInputTests` 结果 `5/5 Passed`；新增用例把真实 Actor 的 `IsEditor` 置为开启，再执行 HUD 右移 `PointerEnter` 和下一帧 `ActorCtrl.OnUpdate()`，确认鼠标移动仍保持。`Level83PerformanceTests` 结果 `6/6 Passed`。
- 首轮 GUI 被 Unity 的 `Deprecated packages` 模态提示阻塞，启动脚本按等待窗口超时退出；对 PID `50230` 一次性 Dismiss 后，同一进程完成 `Loading.unity` 和 READY。该超时作为环境风险保留，不计为入口直接成功。
- 一轮复验中 Unity PID `61844` 发生原生 Mono 崩溃且没有 READY，入口按失败退出；清理自动噪音后重新执行，PID `64675` 正常取得当前会话 READY。崩溃轮次不计为通过证据。
- 同步基线后 PID `70416`、`74626` 在第三方模态/刷新后保持健康但未取得 READY，确认一次性 `delayCall` 重排竞态；改为 `EditorApplication.update` 后，PID `76471` 在一次性 Dismiss 同类第三方提示后立即取得 READY，最终入口退出码为 0。
- 同步后的首轮输入测试遗漏 `-brainpunkPlayModeLevel Level_083`，非路由会话结果为 `4/5`，该命令作废；补齐会话参数后 `GamePlayPanelInputTests` 为 `5/5 Passed`，`Level83PerformanceTests` 为 `6/6 Passed`。
- 修正日志会话边界前的一次重复启动曾读到旧 READY 并过早返回；该结果已作废，不计为最终门禁证据。最终结果只采用重置日志后的启动。
- 未执行人工 Play、完整谱面、Android/iOS 真机触控、构建或平台 smoke，不能据此声明实际游玩与真机输入已验收。回滚时恢复上述启动、Editor 和 Actor 输入改动及测试/文档；不需要迁移配置或玩家数据。
## 2026-07-22 指定关卡工作树启动能力退役

- 用户要求彻底删除 Unity worktree 自动启动、batchmode 编译门禁和指定关卡临时路由能力。
- 已删除原机械启动脚本、`BrainPunkPlayModeLevelRouter`、`EditorPlayModeLevelOverride` 及对应 `.meta`；`LevelCtrl` 不再注入会话级 `LevelData/LevelSort`，`ActorCtrl` 不再根据专用命令行参数跳过 Q/A/D/Space，路由专用输入测试同步移除。
- `GamePlayPanelInputTests` 保留与该能力无关的四项共享 HUD 合同测试：右移、左移、离开停止和跳跃按下。`GamePlayPanel.cs`、按钮 Prefab、事件定义与正式关卡配置未修改。
- 当前仓库不再提供指定关卡 Edit Mode/PlayMode 准备、临时关卡配置、鼠标专用会话或 SaveModule PlayerPrefs 快照恢复能力；历史验证结果只保留在既有 PR 中，不再代表当前入口。
- 本次使用静态符号/路径扫描、引入提交逆向 Diff 和 `git diff --check` 证明能力移除；仓库不再具备合规 Unity 入口，因此未执行 Unity 编译、PlayMode、Android/iOS 构建或真机验证。
- 回滚必须整体恢复脚本、Editor/运行时接入、测试和规则，并重新进行独立安全审查；不得只恢复其中一个调用点。

## 2026-07-21 演奏台判断区域位置同步

### 需求与原行为

`Sp_l83_tz_01.png` 对应的 `Platform` 已把水平坐标从 `x=-4.97` 调整为 `x=-1.97`，但负责触发乐器候选弹出的 `Level83StageTrigger` 仍位于 `x=-4.7`。两者位于零位移、单位缩放的父级链路中，使用同一世界坐标空间，因此玩家走到新演奏台位置时不会进入原判断区域。

### 修复行为与影响范围

- 将 `Level83StageTrigger` 的水平坐标同步为 `x=-1.97`，与 `Platform` 的最新水平中心一致。
- 保留触发器 `y=-1.35`、`z=0` 和 `BoxCollider2D` 尺寸 `1.6×2.8`，继续覆盖角色站立高度并维持既有判定宽度。
- `Level83StageTrigger` 仍只通过 `SetActorInsideStage(true/false)` 通知 `Level83GameplayCtrl`；乐器选择、谱面、音符、胜负、HUD、配置、存档、事件和平台流程均未修改。

### 验证、风险与回滚

- 静态检查确认 `Platform.x` 与 `Level83StageTrigger.x` 均为 `-1.97`，旧触发中心 `x=-4.7` 不再存在。
- Unity batchmode 编译与首次 GUI 的 `READY` 路由验证结果见本任务 PR 审阅单；该会话虽进入过 PlayMode，但未取得目标关卡 `ACTIVE` 和角色走入新判断区域的人工证据，因此触发行为、Android/iOS 真机触控与完整关卡流程仍需 smoke。
- 如需回滚，只需把 `Level83StageTrigger` 的水平坐标恢复为 `x=-4.7`；不涉及脚本、配置或玩家数据迁移。

## 2026-07-21 乐器 Spine 演奏映射修正

### 需求与原行为

用户确认最终表现不需要“持有乐器 idle”和“按键时演奏”两套状态：玩家选择吉他、竖琴或萨克斯后，应直接持续播放对应的 `play_guitar`、`play_harp` 或 `play_sax`，左右移动与跳跃输入只参与谱面判定，不决定角色动画。

原实现会把候选气泡中的 `Instrument` Sprite 移到主角手部，同时用 `Level83GameplayCtrl.LateUpdate()` 补偿通用 `ActorCtrl` 的 `idle/walk` 刷新。玩家可见结果依赖外置静态贴图，且逐帧补偿无法清晰表达“本关持续拥有角色动画”的职责。

### 修复行为与数据流

1. `Level83GameplayCtrl.SelectInstrument(...)` 选中后不再调用 `MoveInstrumentToHand(...)`；候选根节点隐藏时，外置乐器 Sprite 一并隐藏。
2. 三种乐器继续使用固定 Spine 映射：吉他 `play_guitar`、竖琴 `play_harp`、萨克斯 `play_sax`，选择后立即循环播放。
3. `Level83ActorCtrl` 是仅绑定 Level 083 的动画托管边界；启用后只跳过继承自 `ActorCtrl` 的通用移动动画刷新，物理、碰撞和角色更新仍正常执行，其他关卡不受影响。
4. 左、右、跳跃输入不切换或重播乐器动画，只继续进入既有 `Level83Performance` 谱面判定。
5. 错误输入进入失败链时释放动画托管，由通用角色状态重新接管撞飞表现；音符命中胜利时仍由 `celebrate` 接管。

### 影响文件、验证与回滚

- `Level83ActorCtrl.cs`：新增本关专用动画托管、三种乐器映射、庆祝与恢复逻辑；共享 `ActorCtrl.cs` 不修改。
- `Level83GameplayCtrl.cs`：隐藏外置乐器贴图、删除逐帧动画补偿，并在失败/reset/replay 时释放托管。
- `Level_083.prefab`：主角实例移除共享 `ActorCtrl` 组件并添加 `Level83ActorCtrl`，原序列化移动、碰撞和 Spine 引用保持一致。
- `Tests/PlayMode/Level83InstrumentAnimationTests.cs`：通过真实 `Level_083.prefab` 验证三种映射、轨道持续推进、外置贴图隐藏和失败接管。
- 真实 Prefab/Spine 测试 `4/4 Passed`，既有谱面测试 `6/6 Passed`，同步基线后的共享 HUD 输入测试 `5/5 Passed`；Unity `6000.0.59f2` batchmode-only 门禁编译错误模式为 0。
- 运行态截图：`docs/screenshot/20260721/level083-instrument-spine-guitar.png`，1920×1080；画面确认主角显示 Spine 吉他演奏形态，手部没有额外候选 Sprite。
- Android/iOS 真机触控、三种乐器完整视觉循环、音效同步和长时间性能仍未验证。
- 回滚时恢复 `Level83ActorCtrl.cs`、`Level83GameplayCtrl.cs`、`Level_083.prefab` 与本次测试，并删除本节和截图；不涉及配置、存档或玩家数据迁移。

## 2026-07-21 演奏台站立合同实现

### 需求、原始基线与实现行为

用户依据正式效果图确认：主角应使用既有跳跃操作站上 `Sp_l83_tz_01.png` 演奏台；只有脚底稳定落在顶面才显示三个乐器候选，侧面接触或腾空经过不触发，未选乐器离开顶面时收回候选。

实现前 `Platform` 只有视觉组件，不能承托主角；`Level83StageTrigger` 的 `1.6×2.8` Trigger 只按 Player 进入/离开调用 `SetActorInsideStage(...)`，无法证明顶面站立。

当前 `Platform` 已增加 Level 083 本地 `Ground` 层非 Trigger `BoxCollider2D`，碰撞体与视觉同属 `Stage`，舞台平移时保持同步。`Level83StageTrigger` 在 Player 进入/停留时检查主角碰撞体是否接触该台面以及脚底是否位于台面顶端容差内，再调用 `Level83GameplayCtrl.SetActorStandingOnStage(...)`；离开时报告 false。该实现复用共享 Actor 既有重力、跳跃和 `jump_up` / `jump_down`，没有自动吸附、脚本抬升或新 Tween。

### 影响边界、验证与回滚

- 有效 TDD RED 1：真实 Prefab 缺少 Stage 非 Trigger 承托面，目标测试 `0/1 Passed`；最小 GREEN 增加本地实体台面后通过。
- 有效 TDD RED 2：旧宽 Trigger 在主角腾空经过时提前显示气泡，目标测试 `0/1 Passed`；GREEN 将通知条件收紧为顶面接触后通过。侧碰与未选离台用例在新实现下直接通过，作为回归保护。
- REFACTOR 后完整 `Level83InstrumentAnimationTests 10/10 Passed`，同时覆盖三种演奏 Spine、舞台锚点同步、失败动画接管和选中后锁定。
- 1920×1080 运行态截图 `docs/screenshot/20260721/level083-stage-standing-after.png` 确认主角脚线位于台面上方且三个气泡完整显示；现有 sorting order 足够，本次未拆分舞台图层。
- 共享 `ActorCtrl`、HUD、配置、存档、事件、analytics、广告、IAP、RemoteConfig、热更和平台 SDK 均未修改。Android/iOS 真机触控、不同宽高比、正式入口跳跃手感和完整重开/退出仍需 smoke。
- 回滚时恢复 `Level_083.prefab` 的平台碰撞/层级字段、`Level83StageTrigger.cs`、`Level83GameplayCtrl.cs` 和对应测试，并删除截图及本节增量；无运行时状态或玩家数据需要迁移。

## 2026-07-21 乐器随演奏台布局同步

### 需求、基线与根因

演奏台视觉从旧水平中心 `x=-4.97` 调整到 `x=-1.97` 后，三个乐器候选仍显示在旧区域。`Level_083.prefab` 的直接证据表明：`InstrumentBubbles` 与 `Stage` 原本是同级节点，吉他、竖琴和萨克斯气泡继续使用约 `-7.11/-4.78/-2.50` 的旧世界坐标，因此舞台位移不会传播到候选布局。

### 修复行为与影响范围

- `Stage` 现在是演奏台布局的唯一位置锚点，当前水平坐标为 `-1.97`；`Platform` 保持原世界位置并改为锚点下的零水平局部偏移。
- `InstrumentBubbles` 改为 `Stage` 子节点，三个候选的气泡、乐器贴图和 CircleCollider 均换算为相对旧舞台中心的局部偏移。当前画面中三件乐器整体随舞台向右移动 `3` 个世界单位，候选间距、纵向位置、缩放和点击半径不变。
- 后续调整演奏台位置时应移动 `Stage` 锚点；平台视觉和三个候选会通过层级继承相同位移。
- 运行时代码、乐器选择、气泡 Tween、Spine 演奏、谱面、音符、胜负、HUD、事件、配置、存档、analytics 和平台 SDK 均未修改。

### TDD、视觉证据与回滚

- 有效 RED：真实 `Level_083.prefab` 中把 `Stage` 水平移动 `3` 个单位并通过 `SetActorInsideStage(true)` 显示候选，目标测试 `0/1 Passed`，首个失败为 `GuitarBubble` 未同步位移。
- GREEN：完成层级和局部坐标换算后同一测试 `1/1 Passed`；完整 `Level83InstrumentAnimationTests 5/5 Passed`，既有 `Level83PerformanceTests 6/6 Passed`。
- 修复前证据：`docs/screenshot/20260721/level083-instrument-position-before.png`；修复后真实 Prefab Camera 证据：`docs/screenshot/20260721/level083-instrument-position-after.png`（1920×1080）。人工检查确认三件乐器位于当前演奏台正上方。
- 指定关卡 GUI 门禁取得 `READY`，但手动 Play 被第三方 `Deprecated packages` 模态阻断在 `SAVE_SNAPSHOT`，未取得 `ACTIVE`，不计为人工玩法通过。Android/iOS 真机、完整进入/离开触发、选择与通关流程仍需 smoke。
- 回滚时恢复 `Level_083.prefab` 的原同级层级和旧世界坐标，并删除本次新增测试、截图与文档增量；不涉及配置、存档或玩家数据迁移。

## 2026-07-21 演奏台可见承托面修正

### 需求、原始基线与修正行为

用户复核 `level083-stage-standing-after.png` 后确认：主角脚底和动态阴影停在木台椭圆顶面之外，画面像由高于演奏台的透明方块承托，因此此前“脚线和台面关系可读”的视觉结论不再成立。

真实 Prefab RED 测得 `Platform` 的 Sprite 最高边为 `y=-1.866`，实体顶面为 `y=-1.860`，内缩量为 `-0.006`，即物理顶面反而略高于素材最高边。修正只把 `Platform` 的 `BoxCollider2D.m_Offset.y` 从 `0.1` 调整为 `0`；现有 `Platform` 缩放 `x=1.1/y=1.2`、碰撞尺寸 `1.75×0.75`、Sprite、Stage 锚点、主角和共享阴影逻辑均保持不变。修正后实体顶面相对 Sprite 最高边内缩约 `0.114` 世界单位，使脚底和阴影进入椭圆的可见承托面。

### 影响边界、验证与回滚

- 有效 RED：`ActorLandsOnStageTop_IsSupportedAndShowsInstrumentBubbles` 为 `0/1 Passed`，断言报告 `inset=-0.006`。
- 同一专项测试修正后为 `1/1 Passed`；完整 `Level83InstrumentAnimationTests` 为 `10/10 Passed`，继续覆盖稳定承托、侧碰、腾空、离台、选中锁定、三种演奏和失败接管。
- 1920×1080 Camera 证据为 `docs/screenshot/20260721/level083-stage-grounding-after.png`；画面确认主角双脚和动态阴影进入椭圆顶面，三个乐器气泡完整显示。临时截图钩子在生成证据后已删除。
- 指定关卡门禁通过并命中 `READY target=Level_083 state=EditMode scene=Assets/Game/Scenes/Loading.unity input=mouse-only panel=GamePlayPanel_000`；本次没有人工进入 Play Mode，不把 READY 计为人工玩法通过。
- 未修改共享 `ActorCtrl`、`ShadowController`、运行时代码、配置、存档、事件、analytics、资源加载或平台 SDK。Android/iOS 真机、不同宽高比、正式入口连续跳跃手感和完整胜负流程仍未验证。
- 回滚时只需恢复 `Level_083.prefab` 的旧碰撞偏移，回退本次回归断言、截图和文档增量；不涉及配置、存档或玩家数据迁移。

## 2026-07-21 Box Bad 完成评价与胜利气泡实现

### 原行为与实现

- 原实现只等待 `roundResetDelay=0.12s` 就统一复白，Box Bad 保持 `want`，命中胜利也没有角色文字气泡。
- `Level83GameplayCtrl` 现在从 `Resources/HintBubble` 实例化气泡并跟随 `Level83RivalCtrl.transform`；两句台词使用中文原文，显示时长统一为 `1.5s`，未修改共享 `HintBubble`。
- 每轮全对后调用新增公开 `Level83RivalCtrl.PlayIdle()`，保留八格绿色，并等待本轮评价气泡完成共享收起动画、对象实际销毁；随后在同一续体中统一调用八格 `SetIdle()` 并恢复 `PlayWant()`。Prefab 中旧 `roundResetDelay` 字段同步删除。
- 评价中命中先递增 `flowVersion`，同步隐藏并销毁旧评价气泡，再冻结交互、消费命中音符、播放主角 `celebrate`、显示“`真是动人的音乐啊`”并启动既有 `die`。旧评价续体因版本不一致不能复白或恢复 `want`。
- reset、replay、disable 和 destroy 都使版本失效并清理当前 Rival 气泡，避免气泡或异步状态迁移跨生命周期残留。

### 资源、事件与范围

- 复用资源：`BrainPunk/Assets/Game/Resources/HintBubble.prefab`；没有新气泡 Prefab、素材、动画或音频。
- 复用事件：既有输入、`GameResetEvent`、`GameReplayEvent`；没有新增全局事件。
- 未改本地化 key、语言表、Luban、存档、analytics、正常入口或平台 SDK。

### TDD、验证与回滚

- TDD 有效 RED 依次证明：Rival 实际仍为 `want`；0.25 秒时旧逻辑已提前复白；评价中命中仍显示旧台词；正式 reset 后气泡未清理。四个最小实现切片分别转绿。
- 真实 `Level_083.prefab` 新增专项 `6/6 Passed`：首帧全绿/`idle`/评价气泡、真实收起后复白/`want`、评价期拖拽与命中替换、旧续体取消、连续两轮、`die` 成功门禁及 reset 清理。
- 完整 `Game.Level083.Gameplay.Tests` 为 `20/21 Passed`；唯一失败是既有 `Level83DirectionStripVisualTests` 仍反射调用已不存在的 `SetActorInsideStage`，未进入乐器选择，与本次 Diff 无关。`Level83PerformanceTests` 为 `6/6 Passed`。
- 验证上限为 Editor `V2`；未覆盖 Android/iOS、正式入口、长时间性能和完整重开/退出。回滚需恢复两个 Level 083 控制器、Prefab 旧字段、新增测试与本节，无配置或玩家数据迁移。

## 2026-07-22 乐器入手、发声音符与砸击胜利修正

### 原因与最终行为

- Box Bad 根节点的 `Rigidbody2D` 在演奏期间被设为 `simulated=false`，连带使同一刚体上的目标 Trigger 退出物理世界；旧测试又直接调用命中方法，因而没有覆盖玩家拖高、释放、重力下落和真实 Collider 接触。本次让刚体持续参与模拟，玩家撞击仍由 `isCharging` 门禁，实体音符则可在演奏期间真实命中。
- 点击乐器后先锁定输入，候选 Sprite 保持原世界位置并用 `0.3s` 移到手部锚点；到手同帧隐藏候选 Sprite、启动对应 `play_guitar` / `play_harp` / `play_sax` Spine，随后才显示方向谱并接受输入。
- 正确输入从手部乐器锚点生成该乐器对应音符，以 `0.4s` 曲线路径向角色朝向的前上方飘出；三条固定偏移循环使用，发声阶段不可拖拽，到位后才进入既有生命周期与交互。
- 高处释放后的音符由动态 `Rigidbody2D` 重力下落；首次有效接触在接触点播放一次 `Level83KnockEffect`，立即消费音符并进入 `die + celebrate + 胜利气泡 + 标准成功`，不追加 `trap` 音效、震屏或等待。
- 错误输入立即冻结演奏，Box Bad 同时进入 `angry` 并显示“`你弹错了`”；气泡按 `1.5s` 展示及共享收起动画先于约 `2.55s` 的 `angry` 消失，冲撞仍只由 `angry` 完成回调启动。

### 验证、证据与回滚

- Unity `6000.0.59f2` 指定关卡门禁命中 `READY target=Level_083 state=EditMode ... input=mouse-only`，batchmode 与 GUI 编译错误模式为 0。
- 真实 Prefab `Game.Level083.Gameplay.Tests` 为 `24/24 Passed`，包含入手前后动画边界、三轨发声、真实拖拽/释放/物理命中、粒子、`die`、成功，以及错误气泡早于 `angry` 消失、完成后才冲撞；领域测试为 `6/6 Passed`。
- 1920×1080 证据 `docs/screenshot/20260722/level083-note-emission-three-lanes.png` 显示吉他演奏与三枚蓝色音符在乐器前方扇形错位。截图只证明运行态构图，物理胜利由上述 Collider 测试证明。
- 验证上限为 Editor `V2`；未覆盖 Android/iOS 真机触控、不同帧率/宽高比、正式入口、构建、长时间性能和完整重开/退出。回滚本节对应四个 Level 083 控制器、Prefab 绑定和新增/调整测试即可，无配置或玩家数据迁移。

## 2026-07-22 实体音符尺寸放大

### 需求与修正

- 用户确认把吉他蓝色音符和竖琴红色音符的模板缩放从 `0.48` 调整为 `0.72`，把萨克斯黄色音符从 `0.50` 调整为 `0.75`。
- 三张 Sprite、`100 PPU` 导入设置、模板位置与旋转、`CircleCollider2D` 本地半径 `0.75`、生命周期和乐器映射均保持不变。
- 旋转前视觉尺寸调整为：蓝色 `1.1304×1.3608`、红色 `0.9648×1.3608`、黄色 `1.2450×1.3500` 世界单位；三者高度差为 `0.0108`，继续保持约等高。

### 验证、风险与回滚

- `V0` 定向解析确认 `Level_083.prefab` 只有三个目标 `m_LocalScale` 改动，目标值和视觉尺寸计算均通过，`git diff --check` 无错误。
- 模板 Sprite 与 Collider 位于同一缩放节点，因此碰撞世界半径同步变为蓝/红 `0.54`、黄色 `0.5625`；本次未进入 Unity 或 Play Mode，不声明实际触控抓取、物理下落与 Box Bad 命中手感已验证。
- 配置、存档、analytics、资源加载、共享脚本和其他关卡均未修改。回滚时恢复蓝/红 `0.48`、黄色 `0.50`，不涉及数据迁移。

## 2026-07-22 按键谱状态动效实现

### 需求、基线与实现行为

- 原按键谱只切换白/绿/红 Sprite，没有当前格引导、确认动效或统一 Tween 清理。当前正确语义继续使用既有绿色素材，不改三种状态资源。
- `Level83BeatSlot.cs` 缓存 Prefab 原始位置、缩放与方向，独立拥有单格 Tween：当前待输入格以 `1.00 ↔ 1.20`、单程 `0.4s`、`Ease.InOutSine` 持续呼吸；正确格以 `1.00 → 1.20 → 0.96 → 1.00` 在 `0.22s` 内绿色弹跳；错误格在 `0.30s` 内水平往返三次并执行 `1.00 → 0.92 → 1.00`，结束后位置/缩放精确复原且保持红色。
- `Level83GameplayCtrl.cs` 在乐器到手时让 `DirectionStrip` 以 `0.92 → 1.04 → 1.00`、总计 `0.28s` 入场，同时立即开放第一格输入；正确判定同帧启动下一格呼吸，不等待上一格弹跳。评价气泡收起后八格瞬时复白，只重启第一格呼吸，不重播整条入场。
- 错误判定先停止根节点和其他格子的既有反馈，再播放当前红色错误反馈，随后同步进入原 `BeginFailure()`；胜利、reset、replay、disable 和 destroy 复用统一清理入口。没有新增 Prefab 字段、对象、资源、音频、粒子、发光、触觉、事件、配置、存档、analytics 或平台 SDK 行为。

### TDD、视觉证据、边界与回滚

- 八个纵向切片均执行有效 RED → 最小 GREEN：当前格呼吸、正确弹跳/下一格接棒、错误震动压缩、整体入场、轮次复白、reset 清理、胜利停止引导、快速先对后错清理。生命周期测试通过真实 `GameResetEvent`，流程测试通过真实玩家输入事件和实体音符命中入口。
- Unity `6000.0.59f2` 最终 `Game.Level083.Gameplay.Tests 32/32 Passed`；既有 `IncorrectInput_BubbleClosesBeforeAngryCompletesAndChargeStartsAfterward` 继续通过，证明错误视觉反馈未改变 angry、气泡和冲撞门禁。所有最终日志的三类编译错误模式均为 `0`。
- `1024×256` 真实 Prefab 证据：`docs/screenshot/20260722/level083-direction-strip-entrance.png`、`level083-direction-strip-awaiting.png`、`level083-direction-strip-correct.png`、`level083-direction-strip-incorrect.png`。截图只证明可见状态，幅度、时长、即时接棒和清理由持续采样断言证明。
- 验证等级与上限均为 Editor `V2`；未覆盖 Android/iOS 真机触控、正式入口、构建、不同帧率/宽高比、长期性能和人工舒适度。回滚需恢复 `Level83BeatSlot.cs`、`Level83GameplayCtrl.cs` 和方向谱视觉测试，删除四张截图及本节/风险增量；不涉及 Prefab、配置或玩家数据迁移。

## 2026-07-22 按 1.10 规范隔离主角动画扩展

### 需求与原始基线

- Level 083 玩法脚本曾调用共享 `ActorCtrl` 上并不存在的动画托管接口，导致当前分支无法完成 C# 编译；同时把单关动画需求放到共享角色控制器也不符合 1.10 的关卡隔离规则。
- 原玩家表现合同保持不变：选择乐器后持续播放对应演奏动画，正确/错误输入不切换演奏动作，音符命中后播放 `celebrate`，失败、重开和退出时恢复通用角色动画。

### 实现与数据流

- 新增 `Level83ActorCtrl : ActorCtrl`，参考 `Level66ActorCtrl` 及已有 Level70/84 扩展方式，只在 Level 083 内提供开始托管、三种演奏、庆祝和恢复 API。
- 派生控制器覆写 `HandleAnimators()`；托管期间仅阻断通用 `idle/walk` 刷新，`ActorCtrl.OnUpdate()` 的碰撞、物理和角色生命周期仍沿用共享实现。
- `Level83GameplayCtrl` 只调用 `Level83ActorCtrl` 的关卡专用 API；失败、reset、replay、disable 和 destroy 统一释放本关状态。恢复前清零本关残留移动输入，避免停用控制后旧方向重新触发通用行走动画。
- `Level_083.prefab` 只替换主角 Actor prefab 实例的控制器组件并更新本关引用；原移动参数、LayerMask、碰撞体、PushCollider 和 Spine 引用原值复制。Rival 保持原 `ActorCtrl`，共享 prefab 和其他关卡不变。

### 影响、验证与回滚

- 影响文件：`Level83ActorCtrl.cs/.meta`、`Level83GameplayCtrl.cs`、`Level_083.prefab` 和本关中文文档；未新增事件、配置、存档、资源加载、analytics 或平台 SDK 链路。
- `Game.Level083.Domain.csproj` 与 `Game.Level078.Domain.csproj` 重建均为 `0 warning / 0 error`；随后 `Assembly-CSharp.csproj` 独立复跑为 `0 error / 98 warnings`。首次独立编译只因 Unity `Temp/bin/Debug` 缺少这两个领域 DLL 失败，未产生源码错误。
- Prefab 静态断言确认新脚本 GUID 唯一、三处 Actor 引用全部指向派生组件、共享组件只移除一次、旧 fileID 完全消失、Spine/PushCollider 引用有效且 Rival 仍使用共享 `ActorCtrl`；目标文件 `git diff --check` 通过。
- Unity PlayMode、完整三乐器胜负流程、Android/iOS 横屏和真机性能仍需后续 smoke，本次验证上限为静态编译与 Prefab 文本引用检查。
- 回滚时删除 `Level83ActorCtrl.cs/.meta`，恢复 `Level83GameplayCtrl.cs` 与 `Level_083.prefab` 的主角组件/引用及本节即可；不涉及玩家数据迁移。

## 2026-07-27 右侧带锁通关门就位（阶段一）

### 需求与原始基线

- 依据 `docs/issues/level-083-locked-exit-door/PRD.md` 阶段一：场景右侧放置本关唯一通关出口门并挂共享门锁；上锁期间玩家走到门边不能通关。钥匙掉落与解锁接线属阶段二，本阶段不接。
- 原 `Level_083.prefab` 已有 inactive 的右侧 `door_0` 实例（摆放期定位 `(8.3, -1.25)`、缩放 `1.085`，根节点 OnTrigger2DHandle 已接线 `BaseLevelCtrl.OnActorTrigger2D`，SuccessAnimCtrl 已绑定 `LevelCtrl.successAnim`），但门不可见、无锁、触发未禁用；通关仍只靠音符砸中 Box Bad 后立即标准成功。

### 实现与数据流

- 激活右门：门实例 `m_IsActive` 覆盖由 `0` 改为 `1`。
- 锁死门触发器：门根 BoxCollider2D（源 fileID `6885174075320494341`）新增 `m_Enabled = 0` 覆盖，与 Level 025/052 同款；上锁期间玩家进门不触发 `OnActorTrigger2D` 通关链。
- 挂锁：新增共享 `DoorLock` 资产的 PrefabInstance，parent 为门根 Transform（Level 025 范式，门实例 `m_AddedGameObjects` 登记锁根 Transform），局部坐标取门内 LockPoint 锚点 `(-0.2, -1.514)`，旋转/缩放保持单位值；锁保持 prefab 默认 `lock_idle` 表现、`lock` tag、sortingOrder 101 与自带解锁执行链，不接任何触发。
- 不改任何脚本与现有胜利链：音符砸中 Box Bad 仍按原链立即通关；门和锁在本阶段是纯视觉与阻挡语义（进不了门），关卡依旧可通关。阶段一完成后、阶段二接入前锁为「死锁」，是 PRD 认可的中间状态。

### 影响、验证与回滚

- 影响文件：`Level_083.prefab`（三处改动）与本节文档；未新增/修改脚本、事件、配置、存档或本地化 key。`docs/contexts/levels/083/CONTEXT.md` 的「右侧带锁通关门」术语条目已在前序 docs 任务（T2607271921）同步改写，本次核对无需再改。
- 静态断言：门实例 `m_IsActive = 1`；碰撞体 `m_Enabled = 0` 覆盖存在；DoorLock 实例 parent 为门根 Transform、源 prefab guid `3b36192ccce786b48864a76e003e5993`、位置 `(-0.2, -1.514)`；新增 fileID 全文件唯一；`successAnim`/触发接线未被改动；`git diff --check` 通过。
- 仓库已退役 Unity 启动/编译入口，本阶段不执行 Unity 编译、PlayMode 或真机验证；门与锁的视觉、遮挡与手感以用户人工校验为主（固定 preview worktree 浏览），发现遮挡再按 Level 025 的 sortingOrder 105 微调。
- 回滚：还原 `Level_083.prefab` 三处改动与本节即可，无配置、存档或玩家数据迁移。

## 2026-07-27 钥匙爆出与解锁接线（阶段二）

### 需求与原始基线

- 依据 `docs/issues/level-083-locked-exit-door/PRD.md` 阶段二：音符砸中 Box Bad、`die` 完成后不再立即标准成功；预置钥匙从 Box Bad 身上抛物爆出落地，玩家捡拾钥匙解锁右侧门后进门通关。范式采用 Level 007 式纯 prefab 接线（仓库 10 关主流），不采用 Level 025 代码派。
- 阶段一已就位：右门 active、门触发碰撞体 `m_Enabled=0`、DoorLock 挂在门内 LockPoint；原 `CompleteVictory` 仍「置空 successAnim + `SuccessStrightly()` 立即通关」。

### 实现与数据流

- **命中胜利链改向**（阶段二唯一改动的既有代码）：`CompleteVictory` 重命名为 `HandleRivalDieCompleted`，不再调用 `SetSuccessAnim(null)` / `SuccessStrightly()`；die 完成回调改为「`ReleasePlayerControl()` 放行玩家 + `DropKeyFromRival()` 爆出钥匙」。`successAnim` 序列化绑定保留，通关改由门触发链（OnActorTrigger2D → IsActorEnterDoor → CheckFinished → PlaySuccessAnim）完成。
- **放行玩家**：`Level83ActorCtrl.RestoreDefaultAnimation()` 解除演奏锁定 + `BaseLevelCtrl.SetCtrlable(true)` 恢复移动输入。
- **爆出钥匙**（阶段二唯一新表现代码，本关 DOTween）：激活关卡内预置的共享 `Key` 实例（初始 `m_IsActive=0`、layer 9 TriggerItem），以其时 Box Bad 位置为起点、关卡内 `KeyLandingPoint` 序列化锚点 `(5.8, -1.3)` 为落点做抛物补间；时长/高度按 `ItemJumpOutExecutor` 对齐为范围随机（0.35–0.6s / 1.0–2.5），抛物公式同为 `4·h·t·(1−t)`。飞行期间禁用钥匙自带 `MoveFloatHandle`，落地吸附锚点后重新启用、由浮动循环接管。不跨关引用 Level_7 脚本。爆出飞行期间允许空中拾取（与 Level_007 一致，不加代码）。
- **拾取与解锁**（007 范式纯 prefab 接线，零新代码）：Key 实例的 `TriggerExecutor.items` 扩展为 8 段——getKey 音效 → 停浮动 → 缩放 0.5 → 淡出 → `ItemJumpToListener` 跳飞到门（endTarget=门根 Transform，duration 0.5，jumpPower 3）→ 执行 DoorLock 自带 `EventHandleListener`（内部绑定锁的 BaseItemExecutor.Execute：`unlock` 动画 + `unlock` 音效 + 0.1s 锁自毁）→ `SwitchComponentListener` Target=门触发碰撞体、isEnable=1 重新启用 → 0.1s 销毁钥匙。
- **通关**：玩家走进已解锁的门 → 既有门触发链播放门通关动画（门开 → 主角走入 → 门关 → SUCCESS）。砸中气泡保持“真是动人的音乐啊”，进门通关不追加气泡。
- **重开/重玩**：PlayCtrl 销毁并重新实例化关卡，序列化初始态（锁在、门触发禁用、钥匙 inactive）自动恢复；本控制器 OnDisable/OnDestroy 杀掉爆出补间，无残留。

### 影响、验证与回滚

- 影响文件：`Level83GameplayCtrl.cs`（胜利链改向 + 爆出钥匙）、`Level_083.prefab`（Key 预置实例 + 接线 + 落点锚点 + 控制器新字段绑定）、新增 `Level83LockedDoorVictoryTests.cs/.meta`（PlayMode 用例 a–e）、`Level83GameplayFlowTests.cs` 命中用例更新为新合同、CONTEXT.md 两条目改写与本节文档。
- 静态断言：8 段拾取链三个关键项（跳飞到门、DoorLock EventHandleListener、门碰撞体 SwitchComponentListener isEnable=1）逐点核对；Key 初始 inactive/layer 9；`SuccessStrightly` 调用从 Level83GameplayCtrl 消失；新增 fileID 全文件唯一；无 Level_7 `ItemJumpOutExecutor` 引用；`git diff --check` 通过。
- 仓库已退役 Unity 启动/编译入口，PlayMode 用例 a–e 与既有套件一样**已编写未执行**，待合规入口恢复后运行；未验证风险：C# 改动未经编译器检查、爆出抛物与解锁时序未经运行态确认，以用户人工校验（固定 preview worktree）为主。
- 回滚：还原 `Level83GameplayCtrl.cs`、`Level_083.prefab` 本增量、删除 `Level83LockedDoorVictoryTests.cs/.meta` 并还原文档即可；无配置、存档或玩家数据迁移。

## 2026-07-29 乐器抛物线飞手与谱面槽错峰弹出

### 需求与原始基线

- 用户要求点击乐器后，乐器以小的抛物线移动到玩家手中，并逐渐缩小至与实际游戏内 Spine 动画素材大小一致，使入手效果更自然；谱面槽的出现逻辑相应调整为逐格错峰弹出。
- 原实现：选中后整个气泡（气泡底 + 乐器）保持原缩放、`0.3s` 直线（InOutSine）移到手部锚点，选项根节点脱离候选容器；到手后整条 `directionStrip` 做一次 `92% → 104% → 100%` 缩放弹跳，8 格同时出现并立即开放输入。

### 实现与数据流

- **选中气泡底**：点击瞬间原地 `InBack` 缩小消失（`0.3s`），不跟随飞行，早于落选气泡的反向错峰收回；选项根节点留在候选容器内随容器一并撤下。
- **乐器抛物线飞手**：候选乐器 Sprite 脱离气泡挂到手部锚点下，`DOJump` 单段上凸弧（`flightJumpPower=0.5`、`0.45s`、Linear）飞手，同时从候选缩放线性缩小到原缩放 × 每乐器 `handScaleFactor`；到手同帧隐藏候选 Sprite、启动对应 `play_*` Spine。系数对齐 Spine 演奏素材世界高度，由探针量测写入 Prefab：吉他 `0.5739`（play_guitar pose 精确量测）、竖琴 `1.1519`、萨克斯 `0.6706`（后两者在吉他 pose 下近似量测，仅作初值）。
- **谱面槽错峰弹出**：`Level83BeatSlot.PlayShow(delay)` 从缩放 0 以 `OutBack` 弹出（单格 `0.3s`、从左到右错峰 `0.08s`），替代整条谱带缩放弹跳；全部弹出完成前 `isInstrumentReady` 保持 false（输入闸门），完成后才置 true 并启动首格待输入引导。谱面复白不重新弹出。
- **探针取证**：新增 `level083-instrument-flight` 场景驱动探针（`Assets/Editor/Level83InstrumentFlightProbe.cs`），经唯一授权入口 `verify_compile.command --probe-playmode` 运行：断言飞行中段高于直线路径、到手隐藏 + `play_guitar`、到手缩放与量测值偏差 ≤15%、错峰中段末格未弹出、弹出期输入被忽略、弹出完成后输入判定为绿并生成音符；并按固定时刻截 5 张 1920×1080 图。

### 影响、验证与回滚

- 影响文件：`Level83InstrumentOption.cs`、`Level83GameplayCtrl.cs`、`Level83BeatSlot.cs`、`Level_083.prefab`（三选项 `moveToHandDuration=0.45`、`flightJumpPower=0.5`、`handScaleFactor`）、三份 PlayMode 测试时序/断言对齐、新增探针与 `verify_compile.command` 场景白名单、CONTEXT.md 四个词条、风险评估一行。
- 验证：`verify_compile.command` 编译 0 error + EditMode 域测试通过；探针首轮量测（baked=1 预期失败并输出 MEASURE）→ 写入 Prefab → 次轮全部断言 PASS，截图归档 `docs/screenshot/20260729/`（飞行弧、到手、错峰中段、弹出完成）。
- 未覆盖：PlayMode 断言套件（已对齐新时序但本任务无授权入口执行，探针覆盖同等关键断言）；竖琴/萨克斯系数为近似量测初值，手感以人工预览微调；真机触控、不同宽高比未验证。
- 回滚：还原三个 Level 083 脚本、Prefab 本增量、探针与脚本白名单、测试与文档即可；无配置、存档或玩家数据迁移。

## 2026-07-29 命中即爆出钥匙与庆祝可打断（胜利收口提速）

### 需求与原始基线

- 用户确认两点行为调整：音符砸中 Box Bad 后钥匙必须**同帧立即爆出**，不再挂在 `die` 完成回调上（原链路让玩家干等约 5s）；命中后主角 `celebrate` 庆祝循环**允许被玩家移动/跳跃输入打断**——庆祝瞬间按下移动键即打断庆祝、恢复正常移动，可直接跑去捡钥匙。玩家始终无输入时庆祝循环到 `die` 完成后兜底恢复默认动画。

### 实现与数据流

- **命中即爆出**：`HandleMusicNoteHit` 内 `DropKeyFromRival()` 从 `die` 完成回调前移到命中同帧，与 `die` 并行；抛物参数、落点锚点与浮动接管不变。`HandleRivalDieCompleted` 不再爆出钥匙。
- **庆祝可打断**：命中同帧调用 `ReleaseMovementForCelebrationInterrupt()`（`ResumeMoving` + `SetCtrlable(true)`）恢复移动输入，庆祝循环继续播放；新增 `isCelebrating` 状态与 `BreakCelebrationIfActive()`，左移/右移/跳跃三个输入事件入口先行调用，首个输入即 `RestoreDefaultAnimation()` 恢复通用动画，移动本身由 `BaseLevelCtrl` 同事件链既有处理驱动。
- **兜底与幂等**：玩家始终无输入时 `HandleRivalDieCompleted` 结束庆祝并 `ReleasePlayerControl()`（幂等）；已通关（`IsSuccess()`）时直接跳过，避免在门触发通关链期间重开输入开关。
- **冻结拆分**：原 `FreezeGameplayInteraction()` 拆出 `DisableNoteAndOptionInteraction()`（只关乐器候选与场上音符交互），命中链不再冻结移动；失败链（angry→冲撞）冻结行为不变。
- **重开/重玩**：`OnGameReset`/`OnGameReplay` 同步清 `isCelebrating`。

### 影响、验证与回滚

- 影响文件：`Level83GameplayCtrl.cs`（本节目全部代码改动）、`Level83LockedDoorVictoryTests.cs`（用例 b 改写为「立即爆出 + 移动打断庆祝」）、`Level83GameplayFlowTests.cs` 与 `Level83RivalDialogueTests.cs`（命中用例断言文案同步新合同）、CONTEXT.md「爆出钥匙」「胜利收口」两条目改写、本节文档。
- 验证：`verify_compile.command` 编译 + EditMode 域测试回归；用户明确授权的 `--probe-playmode` 场景探针（`level083-key-drop-celebration`）运行态断言「die 播放中钥匙已激活、庆祝可被移动打断并真实移动」并截图归档。
- 回滚：还原 `Level83GameplayCtrl.cs` 本增量与对应测试、文档即可；prefab 与序列化接线无改动。

## 2026-08-05：销毁谱面格导致全局重开事件中断修复

### 日志、原始基线与根因

- 用户在 Level091 失败重开时收到 `Level83BeatSlot has been destroyed`。091 的失败流程已正常派发 `OnTriggerTrapEvent`，`BaseLevelCtrl.TryToRestart()` 也已到达 `GameResetEvent`；091 不持有任何 Level083 引用。
- `Level83GameplayCtrl` 原来在 `OnDisable()` / `OnDestroy()` 的视觉清理结束后才 `ResetEvents`。销毁层级中子级 `Level83BeatSlot` 可能先成为 Unity 假空对象，而 `StopDirectionStripFeedback(...)` 使用 C# `slot?.`，不会调用 `UnityEngine.Object == null`。继续访问 `transform` 会抛 `MissingReferenceException`，从而跳过后面的事件解绑。
- 残留 delegate 再次收到 `GameResetEvent` 时会重现同一异常。共享 `EventDispatcher` 用一个 `try/catch` 包围整个监听列表，单个异常会跳过其后的监听，因此注册顺序满足时 `PlayCtrl` 收不到重开事件，关卡停在失败状态。

### 修复与事件边界

- `OnDisable()` 与 `OnDestroy()` 现在先调用 `EventsUtils.ResetEvents(ref subscriber)`，再杀 Tween、恢复 Actor、清谱面格和气泡；即使后续表现清理异常，全局输入、Reset 与 Replay 监听也已解除。
- 两个生命周期入口以及 Reset/Replay 回调恢复 Actor 时均改用 Unity 对象安全的 `level83Actor != null`；`StopDirectionStripFeedback(...)` 增加数组空保护，并对每个 slot 使用 `if (slot == null) continue`，不再对这些清理路径中的 Unity 对象使用 `?.`。这样即使当前进程已经持有旧 delegate，回调也会跳过已销毁的 Actor 与谱面格。
- 本次只修改 `Level83GameplayCtrl.cs` 和 083 文档；没有修改 `Level83BeatSlot`、Level091、共享 `EventDispatcher`、事件定义、Prefab、存档、配置或平台链路。

### 验证、缺口与回滚

- 静态断言确认两个生命周期入口的 `ResetEvents` 都早于谱面格清理，危险的 `slot?.RestoreOriginalTransform()` / `slot?.StopFeedback()` 已不存在；调用链与用户日志类型唯一对应。
- 使用 Unity 6000.0.59f2 生成的 `Assembly-CSharp.rsp` 进行独立 Roslyn 编译，结果 `0 error`，仅有工程既有 warning。
- 未运行 083 退出后再进入 091 的 PlayMode 复现，也未验证 083 自身失败、Replay、Android/iOS；这些仍需运行态回归。
- 回滚时仅恢复 `Level83GameplayCtrl` 的生命周期解绑顺序与 `StopDirectionStripFeedback(...)` 空判断，并移除本节；无需回滚 091、事件系统或 Prefab。

## 2026-09-09 Reset 访问已销毁谱面格修复补齐

- 用户再次提供 GameResetEvent 中 Level83BeatSlot 已销毁异常。当前工作区实际代码仍是 OnDisable/OnDestroy 先清理后解绑，且 StopDirectionStripFeedback 使用 slot?.；与前文已有修复记录不一致。本次以实际源码为基线补齐，不推断历史改动丢失的原因。
- 影响文件：仅 Level83GameplayCtrl.cs 及本关文档、DevelopMD 记录。生命周期先 ResetEvents，再清理 Tween/Actor/谱面格；清理数组和槽位用 Unity == null 判断，Actor 恢复同样显式判空。Reset/Replay 回调对已销毁或非活动控制器直接返回，防止旧监听/当前派发快照访问旧场景。
- 数据/事件流：旧逻辑中谱面格 transform 访问异常可能打断解绑；共享 EventDispatcher 的 catch 包围整个监听循环，异常会跳过后续监听。本次在本关消除这条已证实的危险访问路径，不修改共享事件系统、BaseLevelCtrl、Reset/Replay 打点、重建延迟或正常谱面表现。Reset 仍保留方向，只复位位置和缩放。
- 验证：dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies --verbosity:quiet 通过，0 错误/123 警告，日志 Temp/CodexLogAnalysis/level83-reset-build.txt；目标差异检查通过。静态核对两处解绑前置、数组与 Unity 对象保护、Reset/Replay 入口保护。
- 未验证（uncertain）：没有本次完整原始异常调用栈/运行态复现，无法证明每次报错都经过相同销毁顺序；Unity PlayMode、先退出 083 再在其它关重置、083 失败/Replay、Android/iOS 尚未实测。修改脚本后需停止并重新进入 PlayMode，以清除当前进程可能已经残留的旧实例监听；热重载不能视为干净回归。
- 回滚：撤回本次 Level83GameplayCtrl 的解绑顺序、清理判空及回调入口保护和对应记录。回滚会重新暴露原风险，不涉及 Prefab/GUID/玩家数据。

### 2026-09-09 后续：共享派发器异常隔离

用户继续报告手动 Reset 错误；Editor.log 同样记录 Level83BeatSlot 已销毁。进一步修改 Bear EventDispatcher：直接绑定到已销毁 Unity 对象的监听跳过并延迟注销，每个独立监听异常单独捕获并输出目标方法与完整异常栈，后续监听继续执行。此前“本次不改共享派发器”的说明只代表上一阶段；最终交付包含本次共享异常路径修复，正常重置及打点合同不变。

8 项隔离回归通过，BearEventSystem 编译 0 错误/0 警告，Assembly-CSharp 0 错误/123 警告。测试替身不是 Unity 运行态验证；当前 PlayMode 和移动端仍 uncertain。共享影响范围、回滚及证据见 Assets/BearGame/com.bear.package.eventsystem@92707994e9ad/事件监听异常隔离修复记录.md。
