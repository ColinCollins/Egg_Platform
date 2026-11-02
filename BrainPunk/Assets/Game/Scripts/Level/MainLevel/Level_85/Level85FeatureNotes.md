# Level85 功能记录

## 需求与背景

本文同步截至 2026-06-23 本地代码中的 `Level85Ctrl` 运行时逻辑。Level85 仍只负责本关观众气泡、发现嫌疑人后的表现流程、战斗 payoff 和成功结算；非 Level85 关卡、共享 `ActorCtrl`、存档、配置、资源加载、打点、平台 SDK 未在本次文档同步中改动。

本次按“代码优先”口径同步：当前运行时行为以 `Level85Ctrl.cs` 和 `Level85ActorCtrl.cs` 为事实源，不把旧 v2.1 注释或旧文档描述视为本次待实现目标。若后续确认需要恢复 audience 随机转向，应另起实现改动并重新同步文档。

历史 v2.1 文档曾记录“audience 随机左右转动 2-3 次后再面向 suspect”。当前 `Level85Ctrl` 实际代码已经调整：`audiences` 在嫌疑人流程中直接 `FaceTarget(suspect.transform)`，不再加入随机转向任务；只有 `suspect` 会执行 2-3 次随机转向序列，且该序列的 target 传入 `suspect.transform` 自身。

## 受影响文件与资源

- `Assets/Game/Scripts/Level/LevelItem/Level_85/Level85Ctrl.cs`
- `Assets/Game/Scripts/Level/Actor/Level85ActorCtrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_85/Level85FeatureNotes.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_85/Level85RiskAssessment.md`
- `DevelopMD/已有功能设计与使用记录.md`
- `Assets/Game/Prefabs/Level/Level_085.prefab` 已做静态绑定检查，本次未编辑 prefab。

## 已确认的本地证据

- `Level85Ctrl.ClickAudience(Transform, Vector2)` 仍是气泡反馈入口：仅在运行时、未进入 payoff、且 `bubble` 存在时显示一个复用气泡，不启动成功流程。
- `Level85Ctrl.OnFindOutSuspect()` 仍是嫌疑人 payoff 入口：非运行时、已开始、已完成或 `suspect` 为空时直接返回；接受后隐藏气泡、递增 session，并启动异步流程。
- `StartSuspectFoundSequence()` 当前会让 `suspect` 和有效 `audiences` 先 `StopCelebrateToIdle()`，随后把 `shirt` alpha 在 0.2 秒内淡到 0。
- 当前 audience 转向逻辑是直接面向 suspect：循环内调用 `actor.FaceTarget(suspect.transform)`，原本把 audience 加入 `PlayRandomFacingSequence(...)` 的代码处于注释状态。
- 当前随机转向任务只添加了 `suspect.PlayRandomFacingSequence(suspect.transform, turnCount, randomTurnInterval, currentSession, IsCurrentSession)`；`turnCount` 来自 `randomTurnMinCount` 和 `randomTurnMaxCount` 的 2-3 次范围。
- 随机转向完成后会重新获取有效 audience，再次让所有有效 audience 面向 suspect，然后等待 `suspectDelayAfterAudience`；`Level_085.prefab` 当前序列化为 2 秒。
- audience 跳跃目标仍优先取 `fightAnim.transform.position`；缺失 `fightAnim` 时使用 suspect 位置，若 suspect 也缺失则使用当前控制器位置。
- suspect knockback 当前使用固定归一化方向 `(0.3, 0.5)`，不再根据 `fightAnim` 计算远离方向；`fightAnim` 只影响 audience 跳跃目标和战斗特效激活。
- `Level_085.prefab` 当前序列化了 `audienceJumpInterval = 0.3`、`suspectKnockForce = 30`、`suspectKnockDuration = 3`、`successDelayAfterSuspectKnock = 2`、`randomTurnInterval = 1`。
- `Level_085.prefab` 静态扫描确认有六个 `ClickAudience` UnityEvent 绑定，以及一个 `OnFindOutSuspect` UnityEvent 绑定。

## 旧文档基线

- 旧记录描述 audience 和 suspect 默认进入 celebrate。
- 旧记录描述触发 `OnFindOutSuspect()` 后，audience 停止 celebrate、进入 idle、随机左右转动 2-3 次、最终面向 suspect，等待 2 秒后进入 v2.0 跳跃/knockback/success 流程。
- 旧记录描述 suspect payoff 会在 knockback 开始 1 秒后成功，并且缺失 `fightAnim` 时 suspect knockback 右向兜底。

## 当前代码行为

- `OnEnable()` 会缓存初始状态并执行清理复位，随后对有效 audience 和 suspect 调用 `PlayDefaultCelebrate()`；该方法内部先等待 0.5-1 秒随机延迟，再尝试播放 `celebrate`，缺动画时回退到 `idle`。
- 点击 audience 只显示气泡；payoff 开始后再次点击 audience 不再显示气泡。
- 接受 `OnFindOutSuspect()` 后，bubble 立即隐藏，suspect 和 audience 停止 celebrate 并进入 idle/readable 状态，shirt 淡出。
- audience 当前不执行随机左右转向，只会在随机阶段前后直接面向 suspect。
- suspect 当前执行 2-3 次随机左右转向，间隔取 `randomTurnInterval`；当前 prefab 为 1 秒。序列结束后 suspect 会调用 `FaceTarget(suspect.transform)` 收尾，实际朝向表现需要 PlayMode 视觉确认。
- 随机阶段结束后等待 `suspectDelayAfterAudience`；当前 prefab 为 2 秒。等待结束后激活 `fightAnim`，再按 `audienceJumpInterval` 依次让 audience 跳向 payoff target。
- 每个 audience 跳跃完成后隐藏自身；所有有效 audience 完成后，或者没有有效 audience 时，进入 suspect knockback。
- suspect knockback 使用固定 `(0.3, 0.5)` 方向、body 旋转 720 度、`ActorCtrl.Knockback(...)`、`knock` 粒子和延迟成功；当前 prefab 成功延迟为 2 秒。
- 清理流程会递增 session、kill 气泡和 payoff tween、取消 actor presentation、恢复 actor transform/active/Rigidbody2D/body rotation、恢复 bubble/fight 状态、将 shirt alpha 淡回 1，并停止 knock 粒子。

## 数据、资源、UI、存档、事件、平台流

- 本次同步只修改文档，没有修改 Level85 运行时代码、prefab、DB 字段、PlayerPrefs、RemoteConfig、Luban 配置、资源包、热更配置、分析打点、广告/IAP/平台 SDK。
- UI 入口仍是 `Level_085.prefab` 中已有的点击 UnityEvent、`bubble`、`fightAnim`、`shirt`、`knock` 和 actor 序列化引用。
- 成功结算仍复用 `BaseLevelCtrl.SuccessStrightly()`。
- 没有修改共享 `ActorCtrl`、Level61、Level84 或其他关卡脚本。

## 已执行验证

- 静态阅读 `Level85Ctrl.cs` 和 `Level85ActorCtrl.cs`，同步了当前 audience/suspect 转向、shirt 淡出、固定 knockback 方向、prefab 序列化时间参数和清理逻辑。
- 静态扫描 `Level_085.prefab`，确认当前 `bubble`、五个 `audiences`、`suspect`、`fightAnim`、`shirt`、`knock`、时间参数和 UnityEvent 绑定仍存在。
- 静态扫描当前 Level85 相关脚本，未发现 `OnValidate`、`Reset`、`[ExecuteAlways]`、`[ExecuteInEditMode]`、DB、Save、RemoteConfig、资源加载、平台 SDK、裸 `Debug.Log`、Level61 或 Level84 引用。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 已在当前代码无 diff 状态下通过，结果为 0 error / 96 warnings；warnings 属于当前工程既有编译输出，其中包含 Level85 当前 fire-and-forget celebrate 调用与 `shirtInitialActive` 未使用提示。
- 未执行 Unity PlayMode、Unity Inspector、Android 或 iOS 设备验证。

## 待补验证

- Unity PlayMode 进入 Level85，确认 audience 和 suspect 的默认 celebrate/idle fallback 表现。
- 快速触发 `OnFindOutSuspect()`，确认 0.5-1 秒 celebrate 延迟不会覆盖后续 idle/随机转向表现。
- 验证当前“audience 直接面向 suspect、只有 suspect 随机转向”的视觉表现是否符合最新设计。
- 验证 shirt 淡出/重开/退出后 alpha 恢复，不残留透明状态。
- 验证 fight smoke、audience 跳跃、逐个隐藏、固定方向 suspect knockback、2 秒成功延迟。
- 验证缺失 `fightAnim`、没有有效 audience、缺失 celebrate/idle 动画、重复触发、payoff 中点击、失败/重开/退出中断和一个未受影响 Actor 关卡回归。
- Android/iOS 横屏真机或模拟器仍需验证触控输入和帧稳定性。

## 剩余风险

- 当前代码与脚本顶部历史 v2.1 注释、旧文档描述不完全一致：audience 不再随机转向，suspect 使用自身作为随机转向序列 target。本文已按代码事实同步；该表现是否符合最终设计仍需 PlayMode 视觉确认。
- `PlayDefaultCelebrate()` 的初始随机延迟没有在等待后复核 presentation session；如果进入关卡 0.5-1 秒内立刻触发 suspect 流程，旧的 celebrate 延迟任务可能晚于 `StopCelebrateToIdle()` 播放动画，需实测确认。
- shirt fade tween 没有纳入 `activeTweens` 统一追踪；当前清理会再淡回 alpha 1，但重开/退出时的最终 alpha 仍需 PlayMode 确认。
- 真实 actor Spine 中是否存在 `celebrate` 动画仍未通过 Unity Inspector 确认。
- `Level85ActorCtrl` 绑定仍需 Unity Inspector 确认 nested prefab/variant 链路；静态 GUID 搜索不能完全证明运行时绑定。

## 回滚路径

- 若当前 Level85 逻辑不符合最新设计，回滚或调整 `Assets/Game/Scripts/Level/LevelItem/Level_85/Level85Ctrl.cs` 中的 suspect-found 序列。
- 若专用 actor presentation 不适合当前 prefab，回滚或移除 `Assets/Game/Scripts/Level/Actor/Level85ActorCtrl.cs` 的绑定。
- 本次只做文档同步；如需回滚本文档，恢复 `Level85FeatureNotes.md`、`Level85RiskAssessment.md` 和 `DevelopMD/已有功能设计与使用记录.md` 对应段落即可。如需改变 Level85 表现，应另行修改 `Level85Ctrl.cs` 或 `Level85ActorCtrl.cs` 并重新验证。
