# Level85 风险评估

## 原始行为基线

- `ClickAudience(Transform, Vector2)` 只显示一个复用气泡，不启动 payoff。
- `OnFindOutSuspect()` 是独立 payoff 入口；重复触发、非运行时、已开始、已完成或缺少 suspect 时不会重复进入主流程。
- 历史 v2.0 continuation 为：有效 audience 面向 suspect -> 等待 2 秒 -> fight smoke 激活 -> audience 依次跳向 payoff target -> 每个 audience 跳完隐藏 -> suspect knockback -> 延迟成功。
- 历史 v2.1 文档基线为：audience 和 suspect 默认 celebrate；发现 suspect 后停止 celebrate，audience 随机左右转动 2-3 次并最终面向 suspect，再等待 2 秒进入 v2.0 continuation。
- 当前代码事实已经不同于旧 v2.1 文档：audience 不再加入随机转向任务，suspect 使用自身作为随机转向序列 target，suspect knockback 使用固定 `(0.3, 0.5)` 方向。
- 本次按代码优先同步文档，未把旧 v2.1 描述作为待实现目标；若后续策划确认旧描述才是目标表现，需要另起代码变更和回归验证。

## 受影响文件与资源

- `BrainPunk/Assets/Game/Scripts/Level/LevelItem/Level_85/Level85Ctrl.cs`
- `BrainPunk/Assets/Game/Scripts/Level/Actor/Level85ActorCtrl.cs`
- `BrainPunk/Assets/Game/Scripts/Level/LevelItem/Level_85/Level85FeatureNotes.md`
- `BrainPunk/Assets/Game/Scripts/Level/LevelItem/Level_85/Level85RiskAssessment.md`
- `BrainPunk/DevelopMD/已有功能设计与使用记录.md`
- `BrainPunk/Assets/Game/Prefabs/Level/Level_085.prefab` 本次仅静态检查，未编辑。

## 现有系统使用

- `ClickItemEventHandle` 与 `Level_085.prefab` 既有 UnityEvent 点击绑定。
- `ActorCtrl.StopMoving()`、`ActorCtrl.SetFaceDir(bool)`、`ActorCtrl.RB`、`ActorCtrl.Body`、`ActorCtrl.StopKnock()`、`ActorCtrl.Knockback(...)`。
- `ActorSpineCtrl.PlayAnimation(...)` 和 Spine skeleton animation lookup。
- DOTween 用于 bubble scale、shirt fade、audience jump、body rotation、success delay。
- UniTask 用于 celebrate 随机延迟、随机转向间隔和 payoff 等待。
- `BaseLevelCtrl.SuccessStrightly()` 用于通关。

## 风险项

- **P1 代码事实与旧文档不一致**：旧文档描述 audience 随机转向，当前代码把 audience 随机转向调用注释掉，只让 suspect 执行随机转向。本次文档已按代码事实同步；若最新需求仍要求 audience 转向表现，需要另起代码改动。
- **P1 Suspect 自身 target 转向表现不确定**：当前 `suspect.PlayRandomFacingSequence(suspect.transform, ...)` 结束后会 `FaceTarget(suspect.transform)`，由于 target 与自身位置相同，最终朝向取决于 `target.position.x >= transform.position.x` 的等值分支，视觉是否符合设计需 PlayMode 确认。
- **P1 Celebrate 延迟覆盖风险**：`PlayDefaultCelebrate()` 等待 0.5-1 秒后没有再次校验 presentation session；若玩家/脚本很快触发 `OnFindOutSuspect()`，旧 celebrate 任务可能晚于 `StopCelebrateToIdle()` 执行动画播放。
- **P1 Shirt alpha 残留风险**：payoff 中 `shirt.DOFade(0, 0.2f)` 和复位中 `shirt.DOFade(1, 0.2f)` 未进入统一 tween 追踪列表；快速重开/退出时最终 alpha 需要 PlayMode 验证。
- **P1 Level85ActorCtrl 绑定缺口**：静态搜索不能完全证明 nested prefab/variant 上的 `Level85ActorCtrl` 绑定，Unity Inspector 仍需确认。
- **P1 Celebrate/idle 动画缺失**：`celebrate` 和 `idle` 是否存在于真实 actor Spine 资源未验证；代码会查找 skeleton 动画并 fallback，但真实显示效果需确认。
- **P1 重复 suspect-found 触发**：已由 `payoffStarted`、`payoffCompleted`、`sessionId` 缓解，但仍需 PlayMode 验证重复 UnityEvent/快速触发不会错过成功或残留状态。
- **P1 过期 async/tween 回调**：部分等待使用 destroy cancellation 和 session 检查，active tweens 会统一 kill；但 celebrate 初始延迟不带 cancellation token，需重点验证快速切换。
- **P1 重开/退出 actor 状态**：缓存恢复 transform/active/Rigidbody2D/body rotation 并调用 `StopKnock()`，但需要确认所有实际 actor 结构都能恢复。
- **P1 缺少 suspect**：当前直接返回且不会成功，这是安全兜底但会导致关卡不可完成；prefab 当前已有 suspect 引用。
- **P1 无有效 audience**：当前会跳过 audience 序列并进入 suspect knockback/success，保留可完成性。
- **P1 payoff 中点击**：`ClickAudience()` 在 `payoffStarted` 为 true 时返回，需验证快速点击不会生成残留 bubble tween。
- **OBS 移动端性能**：当前随机转向只作用于 suspect，audience jump 和 tween 数量有上界；Android/iOS 横屏设备烟测仍缺失。

## 验证证据

- 静态阅读确认当前 `StartSuspectFoundSequence()` 停止 celebrate、淡出 shirt、直接让 audience face suspect、只把 suspect 加入随机转向任务、等待 2 秒后进入 audience jump。
- 静态阅读确认当前 `KnockSuspectAway()` 使用固定归一化方向 `(0.3, 0.5)`，并触发 body 旋转、`ActorCtrl.Knockback(...)`、knock 粒子和延迟成功。
- 静态 prefab 扫描确认 `Level_085.prefab` 有 `bubble`、五个 `audiences`、`suspect`、`fightAnim`、`shirt`、`knock` 引用，且当前序列化 `audienceJumpInterval = 0.3`、`suspectKnockForce = 30`、`suspectKnockDuration = 3`、`successDelayAfterSuspectKnock = 2`、`randomTurnInterval = 1`。
- 静态 prefab 扫描确认六个 `ClickAudience` 绑定和一个 `OnFindOutSuspect` 绑定。
- 静态代码扫描当前 Level85 相关脚本，未发现 `OnValidate`、`Reset`、`[ExecuteAlways]`、`[ExecuteInEditMode]`、DB、Save、RemoteConfig、资源加载、平台 SDK、裸 `Debug.Log`、Level61 或 Level84 引用。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 已在当前代码无 diff 状态下通过，结果为 0 error / 96 warnings；warnings 属于当前工程既有编译输出，其中包含 Level85 当前 fire-and-forget celebrate 调用与 `shirtInitialActive` 未使用提示。
- 本次只同步文档，未改运行时代码；未执行 Unity PlayMode、Unity Inspector 或移动端设备验证。

## 未验证缺口

- Unity Editor PlayMode 验证默认 celebrate/fallback 仍待执行。
- Unity Editor PlayMode 验证 bubble feedback、快速点击、payoff 中点击仍待执行。
- Unity Editor PlayMode 验证“audience 直接面向 suspect、suspect 随机转向”的当前表现是否符合最新设计仍待执行。
- Unity Editor PlayMode 验证 shirt fade 在重开、失败、退出、重进时不残留透明状态仍待执行。
- Unity Editor PlayMode 验证 fight smoke、audience jump、逐个隐藏、固定方向 suspect knockback、2 秒成功延迟仍待执行。
- Unity Editor PlayMode 验证缺失 `fightAnim`、无有效 audience、缺失动画 fallback、重复触发、中断恢复仍待执行。
- Unity Inspector 验证 `Level85ActorCtrl` nested prefab/variant 绑定仍待执行。
- 一个未受影响普通 Actor 关卡回归仍待执行。
- Android 横屏和 iOS 横屏烟测仍待执行。
- 当前 Codex 环境没有 Unity Editor PlayMode 控制或 Android/iOS 设备/构建执行能力。

## 回滚路径

- 若当前“audience 不随机转向、suspect 自身随机转向”不符合最新需求，另起代码改动修改 `Level85Ctrl.cs` 中 `StartSuspectFoundSequence()` 的转向任务分配，并重新执行编译、PlayMode 和移动端验证。
- 若固定 `(0.3, 0.5)` suspect knockback 方向不符合视觉预期，回滚或修改 `KnockSuspectAway()` 的方向计算。
- 若 `Level85ActorCtrl` 绑定或动画 fallback 不可靠，回滚或移除专用 actor presentation 绑定。
- 本次只修改文档；文档回滚只需恢复 `Level85FeatureNotes.md`、`Level85RiskAssessment.md` 和 `DevelopMD/已有功能设计与使用记录.md` 的本次同步段落。

## 待确认

- 确认最新设计是否接受当前代码事实：“audience 不随机转向，只让 suspect 随机转向”。
- 确认 suspect 使用自身作为 random-facing target 的最终朝向是否符合表现预期。
- 确认真实 actor Spine 动画名，必要时把 `celebrateAnimName` 或 `idleAnimName` 改成 prefab 上验证过的名称。
- 确认 `Level85ActorCtrl` 在 Unity nested prefab/variant 链路中的实际绑定。
