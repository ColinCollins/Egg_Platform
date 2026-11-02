# Level 083“动人的音乐”玩法风险评估

> 2026-07-22 退役声明：本文较早章节中的指定关卡路由、`READY`、`SAVE_SNAPSHOT`、
> `SAVE_RESTORED` 和鼠标专用会话均为历史风险证据；相关仓库能力与代码已删除。

## 范围

原始玩法实现从静态摆放扩展为可运行玩法，只修改 `Level_083.prefab` 和 Level 083 本地代码/测试/文档，未修改共享 `ActorCtrl`、`BaseLevelCtrl`、`GamePlayPanel`、Level 046、Luban、存档、analytics 或平台 SDK。2026-07-21 修正增量额外调整共享 `GamePlayPanel` 左右移动按键，具体跨关卡风险与回滚见文末增量章节。

## 风险与控制

| 风险 | 控制与证据 |
|---|---|
| 谱序与已确认文档偏离 | 纯 C# `Level83Performance` 集中保存三份固定 8 步谱序，EditMode 测试逐项比对，`6/6 Passed` |
| 连续移动事件重复判定同一格 | 用 `PlayerMoveCancelEvent` 释放左/右输入锁存；跳跃仍是单次事件 |
| 共享角色刷新把演奏或庆祝覆盖为 `idle` | Level 083 主角绑定 `Level83ActorCtrl`，其覆写 `HandleAnimators()` 仅在本关托管期间阻断通用动画刷新 |
| 八格循环频繁创建 UI | Prefab 预置 8 个 `Level83BeatSlot`，运行时只替换 Sprite 和旋转 |
| 谱面状态正确但被外框遮挡 | 外框固定使用 `SceneLayer/order 109`，8 个槽位固定使用 `SceneLayer/order 110`；真实 Prefab 渲染测试覆盖连续两个正确输入后的 `2 绿 + 6 白` 可见性 |
| 新音符替换或回收旧音符 | 每次正确都独立 Instantiate，每枚保存自己的 DOTween 生命周期；PlayMode 确认一轮同时存在 8 枚 |
| 拖拽后计时被意外恢复 | `OnMouseDown` 首次直接 Kill 生命序列，后续释放不再创建 |
| 落空误触发胜利 | 只有在 Box Bad 水平范围且高于 collider 顶部时释放才布尔武装，首次碰到指定 collider 才上报 |
| 冲撞直接复用 Level 046 形成跨关联动 | `Level83RivalCtrl` 本地复制初始参数，Prefab 不存在 `Level46EnemyCtrl`，Diff 不触及 Level 046 |
| `angry` 未完成就冲撞 | 只在 Spine `TrackEntry.Complete` 后启用 rigidbody/collider 移动；PlayMode 等待 `IsCharging` 断言 |
| 撞到后立即失败而缺少击飞 | 碰撞只触发击飞序列，不直接失败；PlayMode 先断言左上速度，再等待本关 `FallArea` |
| `die` 动画资源较长造成提前成功 | 胜利回调绑定 `die` 完成事件；资源时长约 `5.0667s`，PlayMode 验证窗口覆盖真实完成点 |
| 胜负后仍可输入或重复命中 | `isResolving` 和状态机门禁立即冻结谱面、乐器选项与所有已登记音符 |
| 移动端宽高比下出屏或拖拽手感不稳定 | 当前只验证 Editor PlayMode；Android/iOS 真机仍是必须 smoke 项 |

## 验证边界与回滚

Unity 编译、Prefab API 绑定、EditMode 状态机与 Editor PlayMode 成功/失败完整链路已验证。正常入口、重开/退出、存档解锁、星星、教程、真机触控、宽高比、性能和音效尚未验证。回滚只需恢复 Level 083 Prefab 并删除本关新增脚本/测试，不需要玩家数据迁移。

## 2026-07-21 修正增量风险

| 风险 | 控制与验证要求 |
|---|---|
| 多碰撞体重复进入导致气泡 Tween 重播 | `isActorInsideStage` 对相同进入状态去重；PlayMode 连续调用进入时确认只保留一组表现 |
| 缩放候选根节点导致气泡围绕关卡原点位移 | 只缩放各候选的 `Bubble` 与 `Instrument` 子图，根节点和碰撞偏移不变 |
| 选择时隐藏根节点杀掉乐器移到手部的 Tween | 选择前终止旧弹出序列；隐藏时对已选乐器使用保留标记，PlayMode 检查最终手部位置 |
| 红/黄音符放大后命中范围变化 | 仅做约 `0.9` 世界高度的小幅等比对齐；PlayMode 重新验证拖拽、释放和首次有效命中 |
| 原演奏台 trigger 覆盖入场点 | 中心由 `-4.97` 调整为 `-4.7`、宽度由 `2.4` 收窄为 `1.6`；1920×1080 初始状态证据确认候选未提前出现 |
| 共享 HUD 移动按键影响其他关卡 | 当时的 `OnClickDown + 0.1s` 脉冲方案后续确认破坏共享输入合同；已由文末回归修正恢复为 `OnClickEnter` 启动、`OnClickUp/OnClickExit` 停止，并以真实 Level 083/HUD 输入测试覆盖 |
| 移动端点击气泡时仍在弹出 | 当前候选激活即允许点击，点击会安全终止弹出并转入手部移动；仍需真机验证触控手感 |

本增量回滚需同时恢复 `Level83GameplayCtrl.cs`、`Level83InstrumentOption.cs`、`Level_083.prefab` 和 `GamePlayPanel.cs`；本地文档与视觉证据随提交回退，不需要数据迁移。

## 2026-07-21 共享 HUD 输入回归风险

| 风险 | 控制与验证要求 |
|---|---|
| 只修 Level 083 导致其他关卡输入分叉 | 不新增关卡判断或专用 Prefab override，直接恢复共享 `GamePlayPanel` 的历史事件绑定 |
| 左右方向只覆盖一侧 | 真实 Prefab 测试分别执行 `rightMove_btn` 与 `leftMove_btn` 的 `PointerEnter`，断言 Actor 正/负水平输入 |
| 松开后残留移动 | 测试执行 `PointerEnter → PointerExit`，断言 Actor 水平输入恢复为 `0`；运行时代码同时保留 `OnClickUp` 取消路径 |
| 修复左右时破坏跳跃 | 跳跃仍绑定 `OnClickDown`；真实 `jump_btn` 测试断言 Actor 获得向上速度 |
| 共享改动影响代表关卡 | 自动化覆盖真实 Level 083 与共享 `GamePlayPanel_000`，领域状态机 `6/6 Passed`；Android/iOS 真机多点触控及其他 HUD 变体仍需 smoke |

本次回滚只涉及 `GamePlayPanel.cs`、输入回归测试和文档，不涉及 Prefab、事件定义、Level 083 本地控制器、存档或配置；回滚会恢复已确认的左右无响应回归。

## 2026-07-21 指定关卡鼠标专用启动风险

| 风险 | 控制与验证要求 |
|---|---|
| 只在 Skill 文案中要求鼠标操作，运行时仍接受键盘 | 指定关卡参数由 `EditorPlayModeLevelOverride` 转换为可查询会话状态，`ActorCtrl.HandleInput()` 在该状态下直接返回；READY 必须包含 `input=mouse-only` |
| A/D 未按下的帧覆盖 HUD 持续移动 | 自动化把真实 Actor 的 Editor 输入设为开启，执行 HUD `PointerEnter` 后调用下一帧 `OnUpdate()`；`XInput` 必须仍为正 |
| 鼠标专用分支破坏普通启动或移动端 | 分支只在 Unity Editor 且命令行存在有效指定关卡参数时生效；Player 构建与无 `--level` 会话沿用原行为，但尚未执行构建/真机回归 |
| 为禁用键盘误改共享 HUD 输入合同 | `GamePlayPanel.cs`、按钮 Prefab和事件定义均未修改；左右 `OnClickEnter`、取消和跳跃 `OnClickDown` 继续由既有测试覆盖 |
| 第三方模态提示导致 READY 超时 | 本轮 `Deprecated packages` 提示使入口超时；对目标 PID 一次性 Dismiss 后同一进程取得 READY，未修改全局偏好，首轮超时仍按失败留痕 |
| 刷新期间重排一次性回调导致 READY 永久缺失 | 路由使用持续 `EditorApplication.update` 等待编译/刷新结束，准备前解除订阅；避免在 `delayCall` 内重新挂回同一轮队列时丢失回调 |
| Unity GUI 偶发原生崩溃 | PID `61844` 在未输出 READY 前发生 Mono 原生崩溃，入口按失败处理；清理自动噪音并重跑后 PID `64675` 由唯一入口以退出码 0 取得当前会话 READY，失败轮次不计为通过证据 |
| 重复启动误读旧 READY | 打开 GUI 前删除精确的临时 `Editor.log`，当前进程必须重新写出 Initial Refresh 和 `input=mouse-only` READY；旧会话结果不能进入本轮门禁 |
| 仅凭 READY 声称玩法通过 | READY 只证明 Edit Mode 路由、HUD 和输入模式准备完成；人工 Play、完整关卡、Android/iOS 触控与构建继续列为未验证 |

回滚时恢复 `launch_unity_worktree.command`、`BrainPunkPlayModeLevelRouter.cs`、`EditorPlayModeLevelOverride.cs`、`ActorCtrl.cs`、输入测试与对应文档。该变更不修改 Prefab、正式配置或玩家数据，无数据迁移。
## 2026-07-22 指定关卡工作树启动能力退役风险

| 风险 | 控制与验证要求 |
|---|---|
| 只删除入口而遗留运行时分支 | 同时删除 Editor 路由、会话覆盖类、`LevelCtrl` / `ActorCtrl` 接入和专用输入测试；执行全仓精确符号扫描 |
| 误删共享 HUD 输入回归测试 | 只移除路由专用第 5 个用例，保留右移、左移、离开停止和跳跃按下四项共享合同测试 |
| 文档继续诱导调用已删除入口 | SOP、验证策略、README、Context 和仓库规则统一声明能力已退役，禁止临时恢复或直接调用 Unity 替代 |
| 删除 C# 后未取得 Unity 编译证据 | 当前仓库已无合规 Unity 入口；只声明静态接入清零，编译、PlayMode、构建和真机均列为未验证风险 |
| 历史 PR 仍出现旧命令 | 历史 PR 作为当时证据保留，不属于现行操作入口；当前规则和模块文档优先 |

回滚必须整体恢复脚本、两个专用类型及 `.meta`、共享代码接入、测试与规则，并重新完成独立安全审查；不得局部恢复。

## 2026-07-21 演奏台判断区域位置同步风险

| 风险 | 控制与验证要求 |
|---|---|
| 只移动演奏台视觉而遗漏独立判断区域 | `Platform` 与 `Level83StageTrigger` 的水平坐标统一为 `-1.97`，静态检查同时读取两个具名节点 |
| 直接复制完整 Transform 导致角色站立高度判定变化 | 只同步水平坐标；保留触发器 `y=-1.35`、`z=0` 与 `1.6×2.8` Collider 参数 |
| 判断区域移到新位置后开局再次提前弹出 | 新中心相比旧入场侧位置右移约 `2.73`；仍需在真实 PlayMode 中确认初始隐藏、走到演奏台后弹出、未选中离开后收回 |
| 局部 Prefab 改动影响其他关卡或数据链 | 修改仅位于 `Level_083.prefab`，不改共享 Prefab、脚本、配置、存档、事件、analytics 或平台 SDK |

回滚只需恢复 `Level83StageTrigger.m_LocalPosition.x=-4.7`。Unity batchmode 编译与 Prefab 路由已验证；角色实际走入新判断区域、离开收回、Android/iOS 真机触控和完整成功/失败流程仍属于未验证风险，并在 PR 审阅单中明确记录。

## 2026-07-21 乐器 Spine 演奏映射修正风险

| 风险 | 控制与验证要求 |
|---|---|
| 外置 Sprite 与 Spine 内乐器重复显示 | 选中后不再把候选 `Instrument` 移到手部；真实 Prefab 测试断言候选 Sprite 在层级中不可见，截图人工确认手部只保留 Spine 表现 |
| 通用角色逻辑每帧把乐器动画覆盖为 `idle/walk` | Level 083 主角替换为 `Level83ActorCtrl`，在本关托管期间覆写并阻断通用 `HandleAnimators()`；三种演奏和庆祝仍走本地明确 API |
| 动画托管扩散影响其他关卡 | 不修改共享 `ActorCtrl`；派生组件只绑定 `Level_083.prefab` 的主角实例，Rival 与其他关卡继续使用原控制器 |
| 错误输入后仍持续演奏，破坏撞飞表现 | `BeginFailure()` 释放动画托管；TDD 有效 RED 为期望 `idle`、实际 `play_guitar`，GREEN 后失败链恢复通用角色动画 |
| reset/replay 后残留本关动画所有权 | `OnGameReset` 与 `OnGameReplay` 显式释放托管；场景重建之外也不会继续阻断通用动画刷新 |
| 输入被误用于切换演奏/idle | 最终合同不包含乐器 idle；输入路径未调用动画映射，只保留既有谱面判定，领域测试 `6/6 Passed` |
| 真机 Spine 性能或三种完整循环存在差异 | 当前覆盖 Editor 真实 Prefab、轨道推进与吉他截图；Android/iOS、竖琴/萨克斯完整视觉循环和长时间性能仍需 smoke |

本次只修改 `Level_083.prefab` 的主角控制器绑定，不修改配置、存档、analytics 或平台 SDK。回滚需同时恢复 `Level83ActorCtrl.cs`、`Level83GameplayCtrl.cs`、`Level_083.prefab`、新增测试、功能记录与截图；其他关卡无需数据迁移。

## 2026-07-21 Box Bad 冲撞朝向同步风险

用户在真实 Level 083 PlayMode 中确认：Box Bad 完整播放 `angry` 后虽然收到向左冲撞输入，但可见角色朝右，形成背对主角的倒退冲撞。`Level_083.prefab` 的 `Rival` 以约 `180°` Y 旋转保持初始左向，而共享 `ActorCtrl.Start()` 固定把内部 `facingRight` 初始化为 `true`；首次处理 `SetMoveInput(-1f)` 时再次执行 `Flip()`，使内部方向变为左但可见 Transform 转回右向。

本次只修改 `Level83RivalCtrl.cs`：在 `Awake()` 缓存 Rival 的 Prefab 原始旋转；进入冲撞时，若 Actor 内部仍为朝右则通过既有公开接口同步为朝左，并立即恢复缓存的左向可见旋转。`Level_083.prefab`、共享 `ActorCtrl`、移动速度、`walk01`、Collider、击飞参数、事件、配置、存档、analytics 和平台 SDK 均未修改。

TDD 有效 RED 通过真实 `Level_083.prefab` 完整等待 `angry` 与 `IsCharging`，确认左向输入已经成立，但 `rival.transform.right.x` 期望 `< -0.9`、实际为 `1.0`；最小修复后专项测试 `1/1 Passed`。包含三种乐器 Spine、错误输入失败入口和本朝向测试的真实 Prefab 回归为 `5/5 Passed`，谱面领域测试为 `6/6 Passed`，Unity batchmode 编译错误模式为 0。用户失败截图归档于 `docs/screenshot/20260721/level083-rival-backward-charge-before.png`；修复后的指定关卡人工视觉复验仍需单独记录。

剩余风险是 Android/iOS 真机渲染、完整撞击出屏和失败重开未在本任务覆盖。回滚时恢复 `Level83RivalCtrl.cs`，删除 `Level83RivalFacingTests.cs` 及本节即可；无 Prefab、配置或玩家数据迁移。
## 2026-07-21 按键谱状态遮挡修正风险

### 回归基线与修复

八格按键谱放大后，三个不透明外框与八个谱面格仍共同使用 `SceneLayer/order 110`。玩家完成第一个正确输入时，绿色左箭头只剩伸出外框的尖端；第二个绿色向上箭头位于底条内部并完全不可见。状态逻辑没有隐藏或移动谱面格，回归发生在 Sprite 切换后的渲染合成阶段。

本次只把 `Level_083.prefab` 中 `FrameLeft`、`FrameCenter`、`FrameRight` 的顺序降为 `SceneLayer/order 109`，八个 `BeatSlot` 继续使用 `SceneLayer/order 110`。素材、Transform、谱序、输入事件、实体音符、胜负、存档、analytics 与平台流程均未修改。

### 验证与剩余风险

- TDD RED：真实 `Level_083.prefab` 通过公开乐器选择入口与正式输入事件完成前两个正确输入；两个槽位均已切换为 `Sp_l83_ui_04`，但渲染断言报告第一个槽位绿色像素为 `0`，结果 `0/1 Failed`。
- TDD GREEN：相同测试在外框改为 `order 109` 后结果 `1/1 Passed`；重复执行仍为 `1/1 Passed`，证据图完整显示 `2 绿 + 6 白`。
- Unity `6000.0.59f2` 就绪门禁通过；53 个 embedded packages、Git package cache `4/4`、GF `635506c9...` 已准备，batchmode 与 GUI 编译错误模式为 0。
- 未覆盖红色错误态、八格完成复白、Android/iOS 真机 GPU/宽高比和完整触控流程；这些范围不能由本次两个正确输入的渲染测试外推为已通过。

回滚只需把三个外框 SpriteRenderer 恢复为 `sortingOrder 110`，并随实现删除本次渲染回归测试；不涉及配置、存档或玩家数据迁移。

## 2026-07-21 演奏台站立合同实现风险

| 风险 | 控制与验证结果 |
|---|---|
| 只调整 sorting order，角色仍停在地面高度 | `Platform` 增加 Level 083 本地实体碰撞并随 `Stage` 移动；真实 Prefab 落地测试和截图证明主角由台面承托，未用排序替代高度 |
| 宽 Trigger 把侧面接触或腾空经过误判为站上演奏台 | Trigger 只在 `stageSurface.IsTouching(actorCollider)` 且脚底位于顶面容差内时报告站立；侧碰、腾空和离台测试均通过 |
| 自动吸附或脚本抬升造成角色瞬移 | 未增加位移、吸附或 Tween；继续复用玩家既有物理与 `jump_up` / `jump_down` |
| 新增平台碰撞造成侧边卡住、落地抖动或破坏地面跳跃 | 自动化覆盖从上方落地和侧面接触；正式入口连续跳跃手感、Android/iOS 不同帧率仍需人工 smoke |
| 站立判定切换破坏候选收回或演奏锁定 | 未选离台收回、选中后离台通知不解除锁定，完整真实 Prefab 测试 `10/10 Passed` |
| 单一 Sprite 在真实落地后仍缺少纵深 | 1920×1080 截图确认当前脚线和台面关系可读，保持 `Platform order 120 / Actor order 200`；未扩大为前后层拆分 |

本次执行 `V2` 真实 Prefab 自动化与 Camera 渲染证据；Unity 就绪门禁编译错误模式为 0。未覆盖 Android/iOS 真机触控、不同宽高比、正式入口连续跳跃和完整重开/退出。回滚需同时恢复 Prefab、两个 Level 083 本地控制脚本、测试、截图和本节，不涉及配置或玩家数据。

## 2026-07-21 乐器随演奏台布局同步风险

| 风险 | 控制与验证要求 |
|---|---|
| 舞台视觉移动但乐器仍停留在旧世界坐标 | `InstrumentBubbles` 归入 `Stage` 锚点；真实 Prefab 测试移动 `Stage` 后断言三件可见气泡同步相同位移，RED `0/1`、GREEN `1/1` |
| 层级换算改变演奏台当前构图 | `Stage.x=-1.97`、`Platform.localPosition.x=0`，平台世界中心仍为 `-1.97`；修复后 1920×1080 Camera 截图人工确认舞台位置未漂移 |
| 只移动气泡图形而遗漏乐器贴图或点击范围 | 三组 `Bubble`、`Instrument` 与 CircleCollider 水平偏移使用同一舞台基准换算；缩放、纵向坐标和 Collider 半径未改 |
| 调整布局破坏现有乐器选择与谱面 | 完整真实 Prefab 乐器测试 `5/5 Passed`，领域谱面测试 `6/6 Passed`；运行时代码和事件流无 Diff |
| 后续继续直接移动 `Platform` 子节点导致层级合同失效 | `Stage` 是正式位置锚点，后续舞台平移必须调整 `Stage`；本地实现记录明确该边界，测试也以 `Stage` 公开 Transform 行为锁定 |
| GUI READY 被误当成人工玩法通过 | 当前 GUI 只取得 `READY ... state=EditMode`；第三方模态使手动 Play 停在 `SAVE_SNAPSHOT` 且没有 `ACTIVE`，因此只采用自动化世界坐标断言和真实 Prefab Camera 截图，不扩张结论 |
| 真机宽高比或触控仍有差异 | Android/iOS 横屏、完整进入/离开触发、点击选择和通关流程未验证，继续作为后续 smoke 项 |

回滚只需恢复 `Level_083.prefab` 的原父子层级和旧坐标，并删除本次测试、截图及文档增量；不需要迁移配置、存档或玩家数据。

## 2026-07-21 乐器透明混合与演奏锁定残留输入风险

| 风险 | 控制与验证结果 |
|---|---|
| `Sp_l83_yq_01` / `Sp_l83_yq_02` 的透明区 RGB 被绘制成棕色或白色矩形 | 两张源图属于 Straight Alpha，但 `sp_box_025/Box_Material.mat` 原先按 PMA 输入混合；启用 `_STRAIGHT_ALPHA_INPUT`，真实 atlas attachment Camera 测试逐像素验证输出满足 PMA 不变量，吉他 RED 为 `84781` 个泄漏像素、竖琴 RED 为 `177045` 个，修复后均为 `0` 并通过 |
| 只检查 PNG Alpha 或附件名，漏掉最终混合阶段的 RGB 泄漏 | 测试先走真实 `SelectInstrument(...)`，再使用 attachment 的真实 UV、三角形和 atlas Material 做 Camera 渲染；同时要求有效 Alpha 像素超过 `100`，空帧不能冒充透明背景通过 |
| 修改共享 `Box_Material` 影响同 atlas 的其他 Box attachment | 不改 atlas 像素、region、SkeletonData 或动画；Straight Alpha keyword 与纹理实际编码一致。回归组覆盖三件乐器动画推进、吉他/竖琴渲染、错误输入释放动画等 `13/13 Passed`；其他使用该 atlas 的长动画与 Android/iOS GPU 仍未逐项目视 |
| 选择前残留的左移输入在通用恢复流程后复活 | `ActorCtrl.StopMoving()` 现在同时清零 `xInput` 和 Rigidbody 速度；真实落台测试保存旧 `-1` 输入并模拟延迟 `ResumeMoving()`，RED 从 `X=-1.970` 移到 `X=-7.970`，GREEN 后保持原位、水平速度为零且持续接触台面 |
| 清零共享输入改变其他关卡暂停/停止语义 | 变更只收紧 `StopMoving()` 的既有“停止”合同，不改变 `ResumeMoving()`、事件定义、速度、跳跃、碰撞或动画 API；需要保留方向的流程必须在恢复后重新调用 `SetMoveInput(...)`，不再依赖停止前的隐式残留 |
| 用 Editor 结果外推设备表现 | 本次验证上限为 `V2`：Unity `6000.0.59f2`、真实 Prefab、PlayMode 物理与 Camera 渲染；未执行构建、Android/iOS 真机、设备 GPU、完整成功/失败/重开/退出和长时间性能 |

本次回滚需同时恢复 `sp_box_025/Box_Material.mat`、`ActorCtrl.StopMoving()`、Level 083 回归测试及本节；无需修改 `Box.png`、atlas region、Prefab、配置、存档、analytics 或玩家数据。

## 2026-07-21 Box Bad 评价气泡实现风险

| 风险 | 控制与验证结果 |
|---|---|
| 第八格正确后仍由 `roundResetDelay=0.12s` 立即复白 | 已删除字段并等待评价气泡实际销毁；0.25 秒全绿及连续两轮测试通过 |
| 气泡关闭依赖固定猜测时长，动画变化后提前复白 | 只把 `1.5s` 交给 `HintBubble.Show` 作为显示期；复白等待对象完成真实收起并销毁 |
| 评价期间禁用实体音符，破坏已确认交互 | 评价期生成音符通过 Unity 鼠标消息进入拖拽态，随后公开命中入口有效 |
| 命中后旧评价任务恢复 `want`，覆盖 `die` | `flowVersion` 在命中/reset/replay/disable/destroy 时失效；命中 2.2 秒后仍未复白或恢复 `want` |
| 旧评价气泡与胜利气泡同时可见 | 先同步隐藏/销毁旧气泡，再创建胜利气泡；测试只观察到一个“`真是动人的音乐啊`”气泡 |
| 胜利气泡收起改变既有成功时点 | 默认气泡按 `1.5s` 关闭；测试延长到 10 秒后，真实 `die=5.0667s` 完成时关卡仍成功 |
| 复用共享气泡扩大其他关卡回归面 | 未修改 `HintBubble.cs` 或 Prefab，仅由 Level 083 本地创建和清理；完整 Gameplay 回归其余 20 项通过 |
| 未确认的其他语言被擅自填写或本地词条状态被误判 | 三句气泡使用 `S_LevelTips_Des_level_83_1`、`S_LevelTips_Des_level_83_2`、`S_LevelTips_Des_level_83_3`；飞书表只填写简体中文，不修改 Luban、本地 CSV/I2、配置或正常入口 |

本次验证上限为 `V2`：新增真实 Prefab 专项 `6/6 Passed`，领域谱面 `6/6 Passed`；完整 Gameplay `20/21 Passed` 的唯一失败来自既有过期测试入口，已在 PR 审阅单记录。未覆盖 Android/iOS 真机、正式入口、长时间性能和完整重开/退出，不能外推为平台验收。

回滚需恢复 `Level83GameplayCtrl.cs`、`Level83RivalCtrl.cs`、`Level_083.prefab` 的旧字段、新增专项测试和本节；不涉及配置、存档或玩家数据迁移。

## 2026-07-22 实体音符与演奏时序修正风险

| 风险 | 控制与验证结果 |
|---|---|
| Box Bad 命中 Trigger 因刚体停用而丢失 | 演奏期间保持 Rival `Rigidbody2D.simulated=true`；真实拖拽、释放和重力接触测试进入 `die` 并最终成功 |
| 恢复刚体后提前撞击玩家 | 玩家撞击入口仍要求 `isCharging`，演奏期只允许实体音符通过自身武装门禁上报 |
| 乐器 Sprite 与 Spine 重叠或过早接收输入 | `0.45s` 抛物线入手完成前谱面隐藏且输入无效；到手回调同帧隐藏 Sprite、启动 Spine；谱面槽再经 `0.86s` 错峰弹出，全部弹出完成前输入闸门保持关闭，完成后才开放谱面 |
| 发声 Tween 与拖拽争夺 Transform | `0.4s` 发声阶段强制不可交互；完成后才启用拖拽，胜负冻结可阻止延迟重新启用 |
| 多枚音符仍像固定位置贴图 | 生成点改为当前乐器手部锚点，终点使用三轨循环偏移；运行态截图和三点距离断言通过 |
| 错误气泡阻塞或早于斥责开始 | 气泡与 `angry` 同时开始，真实销毁时 Rival 仍为 `angry`；`IsCharging` 只在动画完成后变为 true |
| 命中效果误复用失败撞击音效 | 实体音符只调用本地 `Level83KnockEffect`，不进入失败链的 `trap`、击飞或旋转 |

验证上限为 Editor `V2`：Gameplay `24/24 Passed`、Domain `6/6 Passed`，并归档 1920×1080 三轨音符证据。未覆盖 Android/iOS 真机、正式入口、构建、不同宽高比/帧率、长时间性能和完整重开/退出，不能外推为平台验收。

回滚需恢复 `Level83GameplayCtrl.cs`、`Level83InstrumentOption.cs`、`Level83MusicNote.cs`、`Level83RivalCtrl.cs`、`Level_083.prefab`、本次测试和文档增量；不涉及配置、存档或玩家数据迁移。

## 2026-07-22 按键谱状态动效实现风险结论

| 风险 | 控制与验证结果 |
|---|---|
| 多格同时循环造成错误引导 | 循环 Tween 只归属当前待输入格；持续 `0.9s` 采样确认未来格偏差低于原始缩放 `0.5%` |
| 快速输入使视觉进度落后或 Tween 叠加 | 正确判定同帧启动下一格；快速先对后错会先统一 Kill/复原旧反馈，再启动当前红色反馈 |
| 正确/错误结束后产生累计漂移 | 所有效果从缓存的 Prefab Transform 基线播放；正确、错误、重置测试均断言最终位置/缩放复原 |
| 错误震动延后失败链 | `SetIncorrect()` 与 `BeginFailure()` 之间没有等待；既有 angry/气泡/冲撞测试继续通过 |
| reset、replay、胜利、disable 或 destroy 残留 Tween | 统一清理入口恢复根缩放和格子变换；reset、胜利有真实 Prefab 专项，其他路径复用同一方法 |
| 动效越界扩散到共享系统 | 只修改两个 Level 083 本地控制器与本地测试；Prefab、共享 HUD/Actor、资源、音频、材质、配置、存档、事件、analytics 和平台 SDK 均未修改 |

最终 `Game.Level083.Gameplay.Tests 32/32 Passed`，四张 `1024×256` 截图证明白/绿/红状态与整条构图，动态时序由采样断言证明。验证上限为 Editor `V2`；Android/iOS、正式入口、构建、不同帧率/宽高比、长期性能和人工舒适度仍需后续验证。回滚范围限于 `Level83BeatSlot.cs`、`Level83GameplayCtrl.cs`、方向谱视觉测试、四张截图与本节，不涉及 Prefab 或玩家数据迁移。

## 2026-07-22 主角动画扩展隔离风险结论

### 原始行为基线与影响边界

原始玩家可见合同为选中后持续演奏、命中后庆祝、失败与重开恢复通用动画。本次不改谱面、胜负、物理参数、资源、存档或事件，只把无法解析的共享动画托管调用迁移到 Level 083 派生控制器，并修改本关主角 prefab 实例绑定。

| 风险 | 控制与验证要求 |
|---|---|
| 单关动作继续污染共享 `ActorCtrl` | 新逻辑全部位于 `Level83ActorCtrl`；共享 `ActorCtrl.cs` 和 Actor prefab 不修改 |
| 派生组件替换后丢失 Actor 序列化参数 | 新组件逐项保留主角实例的移动、跳跃、检测距离、LayerMask、PushCollider、Spine 和 Editor 输入字段；Prefab 引用检查必须确认不再指向已移除组件 |
| 演奏仍被通用 `idle/walk` 覆盖 | 托管期间由派生类覆写 `HandleAnimators()`；演奏和庆祝通过本关明确 API 播放，不使用玩法脚本逐帧重播 |
| 失败、重开或退出后残留托管状态 | failure、reset、replay、disable、destroy 均调用幂等恢复入口；`OnDisable()` 另有本地状态兜底 |
| 残留方向输入在恢复后触发行走动画 | 本关开始托管与恢复时均调用既有 `SetMoveInput(0)`，不修改共享 `StopMoving()` 合同 |
| Rival 被误替换为主角派生组件 | Prefab 只移除 Actor prefab 实例的共享组件；本地 `Rival` GameObject 继续保留原 `ActorCtrl` |
| 设备表现被 Editor 静态结果外推 | 当前结论上限以本次实际编译和静态引用检查为准；Unity PlayMode、Android/iOS、完整胜负/重开/退出仍列为未验证 |

回滚范围为 `Level83ActorCtrl.cs/.meta`、`Level83GameplayCtrl.cs`、`Level_083.prefab` 和本节；删除派生组件后须同时把三处 Actor 引用恢复到原 prefab 实例组件。无需配置或玩家数据迁移。

验证证据：两个缺失的领域程序集分别重建为 `0 warning / 0 error`，`Assembly-CSharp.csproj` 最终为 `0 error / 98 warnings`；Prefab 八项静态引用断言和 meta GUID 匹配全部通过，目标差异检查无错误。Unity PlayMode、完整失败/成功/重开/退出、Android/iOS 和真机性能未验证，不能外推为设备验收。

## 2026-09-09 已销毁对象与全局监听风险

- 基线：当前源码与旧文档不一致，仍在视觉清理后解绑，slot?. 不能保护 Unity 假空对象；用户日志确认 GameResetEvent 派发中访问已销毁 Level83BeatSlot。
- P1：旧关卡监听残留会导致后续关卡 Reset 派发提前中断。改为生命周期先解绑，清理槽位/Actor 显式 Unity 判空，Reset/Replay 入口拒绝已销毁或停用控制器。
- P2：热重载期间旧委托可能已存在；必须退出再进入 PlayMode 回归，不能仅看编译通过。原始异常精确调用路径和销毁顺序仍为 uncertain。
- 范围：只修改 Level83GameplayCtrl；复用 EventsUtils.ResetEvents，保留原正常重置谱面方向和共享打点/重建时序，无新增 API、字段、Prefab 绑定、事件、存档或资源。
- 验证：C# 编译 0 错误/123 警告；静态确认解绑优先和假空保护。待测：先销毁子槽再销毁父控制器、083 退出到其它关连续 Reset、083 自身失败/Replay、移动端横屏。回滚仅撤回本次控制器增量及文档。

### 2026-09-09 后续：共享派发器异常隔离

用户继续报告手动 Reset 错误；Editor.log 同样记录 Level83BeatSlot 已销毁。进一步修改 Bear EventDispatcher：直接绑定到已销毁 Unity 对象的监听跳过并延迟注销，每个独立监听异常单独捕获并输出目标方法与完整异常栈，后续监听继续执行。此前“本次不改共享派发器”的说明只代表上一阶段；最终交付包含本次共享异常路径修复，正常重置及打点合同不变。

8 项隔离回归通过，BearEventSystem 编译 0 错误/0 警告，Assembly-CSharp 0 错误/123 警告。测试替身不是 Unity 运行态验证；当前 PlayMode 和移动端仍 uncertain。共享影响范围、回滚及证据见 Assets/BearGame/com.bear.package.eventsystem@92707994e9ad/事件监听异常隔离修复记录.md。
