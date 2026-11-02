# Level61 风险评估

## 2026-07-09 默认气泡弹出修复风险评估

### 原有行为基线

- Level61 坐下后调用 `StartBubbleRevealDelay()`，先隐藏气泡透明度并将气泡 scale 置为 `0`。
- `RevealBubbleAfterDelayAsync(...)` 等待 `bubbleRevealDelay` 后负责让气泡可点击。
- 食物切换应由有效点击触发，不应由默认气泡弹出路径自动触发。

### 变更入口与影响文件

- `Assets/Game/Scripts/Level/LevelItem/Level_61/Level61Ctrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_61/Level61功能说明.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_61/Level61风险评估.md`
- `DevelopMD/已有功能设计与使用记录.md`

### 已规避风险

- 气泡不可见风险：新增默认弹出时的 scale tween，将气泡从 `Vector3.zero` 恢复到缓存原始 scale，避免 alpha 已到 1 但 scale 仍为 0。
- 自动切食物风险：默认弹出路径只处理透明度和 scale，不调用 `SwitchFoodByActorClickAsync(...)`，不修改 `switchFoodCount`，不触发 Star。
- 中断残留风险：弹出 scale tween 复用现有 `bubbleScaleTween`，离开椅子、禁用或销毁时继续由 `StopFoodSwitch(true)` / `CleanupRuntimeState()` 清理。
- 可点击时机风险：只有视觉弹出流程完成且仍处于坐下状态时，才设置 `isBubbleReadyForClick = true`。

### 验证证据

- 源码检查确认 `RevealBubbleAfterDelayAsync(...)` 现在调用 `RevealBubbleVisualAsync(...)`，后者并行执行 alpha 淡入和 scale 弹出。
- 源码检查确认 `RevealBubbleVisualAsync(...)` 不调用食物切换、不访问 Star、不写 DB、不派发事件。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过，结果为 98 warnings / 0 errors。
- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_61/Level61Ctrl.cs` 已通过，仅有工作区 LF/CRLF 转换提示。
- 目标脚本敏感链路扫描未发现新增编辑器生命周期、全局事件、资源加载、RemoteConfig、PlayerPrefs、广告、IAP、analytics 或实例化。

### 剩余风险 / 未验证缺口

- Unity PlayMode 未运行，坐下后气泡弹出节奏、离开椅子中断、重复坐下/离开后的 scale 恢复仍待验证。
- Android/iOS 横屏 smoke 未运行，触控时序和快速点击表现仍待验证。
- 当前工作区 `Level_061.prefab` 仍有用户新增 Star 相关 diff 和 Unity YAML 空字段行尾空白，本次未手工修改 prefab。

### 回滚路径

- 回退 `RevealBubbleVisualAsync()`、`PopBubbleScaleAsync()` 和 `RevealBubbleAfterDelayAsync(...)` 的调用改动，即可恢复到只淡入 alpha 的气泡显示路径。

## 2026-07-09 食物切换 Star 触发整理风险评估

### 原有行为基线

- Level61 食物切换入口是 `Level61Ctrl.OnActorFoodClick(...)`，有效点击要求 actor 已坐下、气泡显示完成、未处于切换中、`sitCts` 有效且至少有两个有效食物 sprite。
- 食物切换表现仍由 `SwitchFoodByActorClickAsync(...)`、`Level61ActorCtrl.PlayTurningThePageOnce()` 和气泡缩放/食物淡入处理。
- `Postcard_Star` 是明信片 Star 的收集权威：负责已收集去重、背包发奖、保存和 `PostcardGetStarEvent` 派发。
- `StopKey()` 是 Level61 key 事件链调用的公共入口，原实现依赖 `keyDirect` 和同节点 `MoveFloatHandle` 均已绑定。

### 变更入口与影响文件

- 直接修改文件：
  - `Assets/Game/Scripts/Level/LevelItem/Level_61/Level61Ctrl.cs`
  - `Assets/Game/Scripts/Level/LevelItem/Level_61/Level61功能说明.md`
  - `Assets/Game/Scripts/Level/LevelItem/Level_61/Level61风险评估.md`
  - `DevelopMD/已有功能设计与使用记录.md`
- 相关未手工修改资源：
  - `Assets/Game/Prefabs/Level/Level_061.prefab` 当前工作区已有用户新增 `Postcard_Star (1)`、`Level61Ctrl.star` 绑定和 `foodMaxSwitchTriggerCount: 5` 的 diff。

### 已规避风险

- 触发次数偏移：原新增逻辑在 `switchFoodCount++` 前判断阈值，`5` 会变成第 6 次触发；当前改为有效点击后先递增，再判断阈值。
- Star 空引用：`star` 未绑定时现在只记录 warning 并停止本轮触发尝试，不再访问 `star.activeSelf` 导致空引用。
- 重复触发：新增 `hasTriggeredFoodSwitchStar`，一轮坐下达到阈值后只尝试显示一次，避免已显示或已收集隐藏后反复触发。
- 已收集重复显示：Star 激活后调用 `Postcard_Star.RefreshSavedCollectionState()`，复用既有存档去重隐藏；`Level61Ctrl` 不直接写 `DB.Postcard`、不发奖、不派发事件。
- 文档/行为冲突：移除气泡显示完成后的自动 `SwitchFoodByActorClickAsync(...)`，使食物切换重新只由有效点击触发。
- StopKey 空引用：`StopKey()` 增加 `keyDirect` 与 `MoveFloatHandle` 检查，缺失时 warning 后返回。
- 生命周期边界：本次未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode` 或编辑器态 DB 链路。

### 验证证据

- 源码检查确认 `RecordFoodSwitchTrigger()` 会先 `switchFoodCount++`，再用 `Mathf.Max(1, foodMaxSwitchTriggerCount)` 判断阈值。
- 源码检查确认 `TryShowFoodSwitchStar()` 对 `star == null` 做保护，并通过 `Postcard_Star.RefreshSavedCollectionState()` 只读刷新已收集状态。
- 源码检查确认 `StartBubbleRevealDelay()` 不再在气泡显示完成后自动调用 `SwitchFoodByActorClickAsync(...)`。
- 源码检查确认 `StopKey()` 在访问 `keyDirect.enabled` 和 `MoveFloatHandle.enabled` 前都有空引用保护。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过，结果为 99 warnings / 0 errors。
- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_61/Level61Ctrl.cs` 已通过，仅有工作区 LF/CRLF 转换提示。
- 敏感链路静态扫描未发现目标脚本新增全局事件、资源加载、编辑器生命周期、RemoteConfig、PlayerPrefs、广告、IAP、analytics 或对象实例化链路。

### 剩余风险 / 未验证缺口

- Unity PlayMode 未运行，尚未验证第 5 次有效点击显示 Star、连续快速点击只计一次、离开椅子后 Star 保持可收集或已收集隐藏的真实表现。
- Android/iOS 横屏 smoke 未运行，触控命中、快速点击和性能表现仍待验证。
- `Level_061.prefab` 的新增 Star 节点、`Postcard_Star.index`、Collider、`MoveFloatHandle` / `ItemJumpOutExecutor` 等组件最终 Inspector 状态仍待 Unity 中确认。
- 当前工作区存在多处无关改动和 `Level_061.prefab` YAML diff；本次只确认与 `Level61Ctrl.star`、`foodMaxSwitchTriggerCount` 直接相关的静态 diff，其他 prefab 变更归属仍需用户确认。

### 回滚路径

- 回退 `Level61Ctrl.cs` 中 Star 计数、显示、已收集刷新和 `StopKey()` 空引用保护相关改动。
- 如果产品需要恢复“气泡显示后自动切一次食物”，可重新添加 `StartBubbleRevealDelay()` 末尾的自动切换调用，并同步更新功能说明和风险评估。
- 若后续确认 `Level_061.prefab` 中新增 Star 不应提交，可在 Unity 中移除 `Postcard_Star (1)` 和 `Level61Ctrl.star` / `foodMaxSwitchTriggerCount` 序列化变更。

## 2026-06-09 User-adjusted final state

### Original behavior baseline

- Level61 click-food behavior remains local to `Level61Ctrl`, `Level61ActorCtrl`, and `Level_061.prefab`.
- The latest user-adjusted expected behavior is: thinkBubble reveal completes before clicks are accepted, valid clicks switch food while shrinking/re-expanding the bubble, seated page-turn plays once after sitting and once per valid click, and key display is delayed after returning with the choice-panel key.

### Affected files/resources

- `Assets/Game/Scripts/Level/LevelItem/Level_61/Level61Ctrl.cs`
- `Assets/Game/Prefabs/Level/Level_061.prefab`
- `Assets/Game/Scripts/Level/LevelItem/Level_61/Level61功能说明.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_61/Level61风险评估.md`
- `DevelopMD/已有功能设计与使用记录.md`

### Risk controls implemented

- `BubbleClickArea` adds a prefab-local 2x2 trigger click surface bound to `Level61Ctrl.OnActorFoodClick`, reducing dependence on actor collider overlap and raycast ordering.
- Existing actor click binding and local raycast fallback remain available, while `isFoodSwitching` and bubble-ready gates still reject premature or rapid taps.
- `menuBookOffset` is serialized on `Level61Ctrl`; current `Level_061.prefab` uses `{x: 0, y: 0.2, z: 0}` so the book jump target can be tuned per Level61 without shared code changes.
- `TryTriggerKeyDoorFlow()` now hides `keyObject` before validation/wait, then waits 2 seconds before `SetKeyActive(true)`, preserving the delayed reveal timing and avoiding key visibility during the delay.

### Verification evidence

- Static inspection confirmed `Level_061.prefab` contains `BubbleClickArea` with trigger `BoxCollider2D`, `ClickItemEventHandle`, and `OnClickEvent -> Level61Ctrl.OnActorFoodClick`.
- Static inspection confirmed `Level_061.prefab` serializes `menuBookOffset={x:0,y:0.2,z:0}`.
- Static inspection confirmed `Level61Ctrl.TryTriggerKeyDoorFlow()` calls `SetKeyActive(false)`, awaits `UniTask.WaitForSeconds(2f)`, then calls `SetKeyActive(true)`.

### Remaining risks

- Unity PlayMode has not been run in this environment, so the user-confirmed visual result is documented from workspace state but not independently reproduced here.
- Android/iOS landscape touch smoke has not been run; touch target comfort, rapid taps, and frame stability remain unverified.
- `TryTriggerKeyDoorFlow()` still does not directly call `executor.Execute()`, so key activation to full door-flow behavior still depends on prefab/runtime event chain validation.

### Rollback path

- Revert the `BubbleClickArea` addition and `menuBookOffset` prefab/script changes.
- Revert the delayed `SetKeyActive(false)` / `WaitForSeconds(2f)` / `SetKeyActive(true)` flow if the key should return to immediate activation.

## 2026-06-09 Page-turn visibility fix

### Original behavior baseline

- Level61 click-food behavior is local to `Level61Ctrl`, `Level61ActorCtrl`, and `Level_061.prefab`.
- `turning_the_pages` is a valid actor Spine animation and remains the configured page-turn animation.
- Existing shared `ClickItemEventHandle` behavior remains unchanged for other levels.

### Affected files/resources

- `Assets/Game/Scripts/Level/Actor/Level61ActorCtrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_61/Level61Ctrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_61/Level61功能说明.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_61/Level61风险评估.md`
- `DevelopMD/已有功能设计与使用记录.md`

### Risk controls implemented

- Sit completion now enters `sofa_idle`, then attempts one `turning_the_pages` playback after a 1s seated delay if the actor is still sitting and alive.
- Actor click fallback is Level61-local and only active when sit, bubble-ready, not switching, and the sit token is valid.
- The fallback uses `Physics2D.RaycastAll` and filters colliders by `actorCtrl.transform`, avoiding the local sit trigger area stealing the first raycast hit.
- Existing `isFoodSwitching` gate prevents duplicate switching if both the prefab `ClickItemEventHandle` and the fallback detect the same click.

### Verification evidence

- Static inspection confirmed `Level_061.prefab` keeps actor click binding to `Level61Ctrl.OnActorFoodClick`.
- Static inspection confirmed actor root has an enabled `PolygonCollider2D` and the sit area has a 2x2 trigger collider that may overlap the seated actor.
- Static inspection confirmed `Box.json` contains `turning_the_pages` with non-empty keyframes.

### Remaining risks

- Unity PlayMode has not been run in this environment, so seated auto page-turn, click hit fallback, final-frame hold, and duplicate-click suppression still require runtime validation.
- Android/iOS landscape touch smoke has not been run.

### Rollback path

- Revert the `PlaySofaIdleAfterSit()` change in `Level61ActorCtrl`.
- Remove the Level61-local `Update()` click fallback and helper methods from `Level61Ctrl`.

## 2026-06-09 Level61 click-food risk update

### Original behavior baseline

- Existing Level61 entry/key state remains owned by `EvaluateLevel61State()`, `TryTriggerKeyDoorFlow()`, and existing `DB.GameData` fields.
- Existing choice-panel key behavior remains owned by `Level61ChoicePanelKeyCtrl` and `ChoiceLevelPanel.OnOpen()`.
- The intentional behavior change is limited to sit presentation: automatic food interval switching is replaced by actor-click-driven switching after thinkBubble reveal.

### Affected files/resources

- `Assets/Game/Scripts/Level/LevelItem/Level_61/Level61Ctrl.cs`
- `Assets/Game/Scripts/Level/Actor/Level61ActorCtrl.cs`
- `Assets/Game/Prefabs/Level/Level_061.prefab`
- `Assets/Game/Scripts/Level/LevelItem/Level_61/Level61功能说明.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_61/Level61风险评估.md`
- `DevelopMD/已有功能设计与使用记录.md`
- `specs/011-level61-click-food/quickstart.md`

### Risk controls implemented

- Actor clicks are ignored unless Level61 is sitting, bubble reveal is complete, the sit token is valid, valid food data exists, and no switch is running.
- The actor click event remains local to `Level_061.prefab`; shared `ClickItemEventHandle` code is not changed.
- Bubble scale, food fade, and menuBook tweens are killed during leave-sit and cleanup to avoid delayed callbacks.
- MenuBook original position is cached before movement and restored on leave-sit to avoid drift.
- `Level61ActorCtrl` click page-turn plays once with `loop=false`; leave-sit still restores idle through existing `StopSit()`.

### Remaining risks

- Unity PlayMode has not been run in this environment, so actor tap hit detection, Spine final-frame hold, exact bubble scale timing, food fade appearance, and menuBook return path remain unverified.
- Android/iOS landscape device smoke has not been run, so touch hit area, rapid-tap behavior, and 60 fps target remain unverified.
- Existing key-door flow still has the prior known risk: `TryTriggerKeyDoorFlow()` activates `keyObject` after a 2 second delay but does not directly call `executor.Execute()`.

### Rollback path

- Revert `Level61Ctrl.cs`, `Level61ActorCtrl.cs`, and `Level_061.prefab`.
- Remove the actor `ClickItemEventHandle.OnClickEvent -> Level61Ctrl.OnActorFoodClick` binding.
- Restore automatic food switching by returning the post-reveal path to `StartFoodCycle(cancellationToken)`.

更新时间：2026-06-05

## 原有行为基线

- `Level61Ctrl.cs` 原本负责：
  - 进入关卡时检查 `HasEnterLevel61` / `HasGetLevel61Key`
  - 绑定 Use 按钮
  - 控制坐下、气泡和食物循环
- `Level61ChoicePanelKeyCtrl.cs` 负责选择关卡面板中的 Level61 key 显示和领取。
- `Level_061.prefab` 中存在 inactive 的 `Key` 对象，并挂有与 key 相关的事件链组件。

## 当前代码事实

- `EvaluateLevel61State()` 只有在 `HasEnterLevel61 == true` 且 `HasGetLevel61Key == true` 时才进入 `TryTriggerKeyDoorFlow()`。
- `TryTriggerKeyDoorFlow()` 当前先隐藏 `keyObject`，再做引用检查、确认 `BaseItemEventHandle` 存在，等待 2 秒后激活 `keyObject`，然后返回 `true`。
- `TryTriggerKeyDoorFlow()` 当前没有执行 `executor.Execute()`，对应代码已被注释掉。
- `EvaluateLevel61State()` 会在 `TryTriggerKeyDoorFlow()` 返回 `true` 后，立即把 `HasEnterLevel61` 和 `HasGetLevel61Key` 重置为 `false` 并保存。
- `Level61ChoicePanelKeyCtrl` 当前只依赖 `HasGetLevel61Key`：
  - `HasGetLevel61Key == false` 时允许延迟显示
  - `HasGetLevel61Key == true` 时隐藏
- `EnterSit()` 会调用 `TryPlayMenuBookJump()`；该方法在 `menuBook == null` 时只记 warning，不阻断坐下逻辑。
- `TryPlayMenuBookJump()` 当前会延迟 `1.3s` 后执行 `0.3s` 的 `DOJump`，目标点为 `actorCtrl.transform.position + menuBookOffset`，结束后隐藏 `menuBook`。
- `Level61Ctrl` 暴露了 `StopKey()`，当前直接依赖 `keyDirect` 和 `MoveFloatHandle`，没有空引用保护。

## 影响文件 / 资源

- `Assets/Game/Scripts/Level/LevelItem/Level_61/Level61Ctrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_61/Level61ChoicePanelKeyCtrl.cs`
- `Assets/Game/Scripts/Level/Actor/Level61ActorCtrl.cs`
- `Assets/Game/Prefabs/Level/Level_061.prefab`
- `Assets/Game/UI/LevelChoicePanel/Scripts/ChoiceLevelPanel.cs`
- `Assets/Game/UI/LevelChoicePanel/Resources/ChoiceLevelPanel.prefab`
- `Assets/Game/Scripts/Level/LevelItem/Level_61/Level61功能说明.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_61/Level61风险评估.md`
- `DevelopMD/已有功能设计与使用记录.md`

## 已规避风险

- `ChoiceLevelPanel.OnOpen()` 对 `level61KeyCtrl` 做了空引用保护；面板引用缺失时不阻断既有入口。
- `Level61ChoicePanelKeyCtrl` 找不到 `keyBtn`、`RectTransform` 或 `Graphic` 时会隐藏 key 并停止流程。
- `Level61ChoicePanelKeyCtrl` 只操作 `keyBtn` 子节点，不移动 `Level61Key` 父节点，降低 UI 布局错位风险。
- 选择面板 key 现在只依赖 `HasGetLevel61Key`，避免已经领取后的重复显示。
- 点击选择面板 key 后会立即保存 `HasGetLevel61Key=true`，动画中断也不会丢失领取状态。
- `TryTriggerKeyDoorFlow()` 在 `keyObject`、`BaseItemExecutor` 或 `BaseItemEventHandle` 缺失时返回 `false`，不会提前重置两个存档状态。
- `TryPlayMenuBookJump()` 对 `menuBook` 做了空引用保护，并在重复触发前 Kill 上一次 tween。
- `BubbleClickArea` 是 Level61 prefab 局部点击面，不改共享 `ClickItemEventHandle`。
- `menuBookOffset` 是 Level61 序列化字段，不改其他关卡书本表现。
- `CleanupRuntimeState()` 会清理 Use 按钮订阅、气泡 tween、食物 tween 和 `menuBookTween`，降低残留状态风险。

## 当前主要风险

- `TryTriggerKeyDoorFlow()` 返回 `true` 的条件并不包含“门已打开”或“executor 已执行”；当前只要 keyObject 延迟激活成功且必要引用存在，就会把两个存档状态重置。
- `BaseItemExecutor.Execute()` 当前未从 `Level61Ctrl` 调用；如果后续门逻辑仍依赖该调用，现状可能造成“状态已消耗，但开门链路未真正执行”。
- `StopKey()` 没有对 `keyDirect` 或 `MoveFloatHandle` 做空引用保护；若挂载缺失，运行时存在空引用风险。
- `menuBook` 跳跃完成后会直接 `SetActive(false)`；如果后续重新进入该表现链路仍依赖该对象，需要额外验证是否有重新激活入口。
- `Level61ChoicePanelKeyCtrl` 已不依赖 `HasEnterLevel61`；如果设计上仍希望“只有进过 Level61 才能在选择面板看到 key”，当前实现已不再满足该前提。

## 验证证据

- 已读取 `Level61Ctrl.cs` 当前代码，确认 `executor.Execute()` 分支被注释，`TryTriggerKeyDoorFlow()` 当前先隐藏 key，等待 2 秒后激活 `keyObject` 并返回 `true`。
- 已读取 `Level61Ctrl.cs` 当前代码，确认新增字段 `menuBook`、`keyDirect` 和公共方法 `StopKey()` 存在。
- 已读取 `Level61Ctrl.cs` 当前代码，确认 `TryPlayMenuBookJump()` 当前使用：
  - 目标点 `actorCtrl.transform.position + menuBookOffset`
  - Jump Power `1`
  - Duration `0.3s`
  - Delay `1.3s`
- 已读取 `Level61ChoicePanelKeyCtrl.cs` 当前代码，确认显示判断只依赖 `HasGetLevel61Key`。
- 已检查当前仓库，仍未找到可同步的 `specs/**/tasks.md` 或其他 Level61 任务文件；本次仅更新现有功能说明、风险评估和设计记录。

## 未验证 / 剩余风险

- 尚未在 Unity PlayMode 中验证：`SetKeyActive(true)` 之后，门链路是否会在不调用 `executor.Execute()` 的情况下按预期继续执行。
- 尚未在 Unity PlayMode 中验证：状态重置是否应延后到真正开门完成之后。
- 尚未在 Unity PlayMode 中验证：`StopKey()` 的实际调用时机、`keyDirect` / `MoveFloatHandle` 挂载完整性，以及关闭直线移动后 key 的后续漂浮表现。
- 尚未在 Unity PlayMode 中验证：`menuBook` 延迟 1.3 秒后按 `menuBookOffset` 跳向 actor 并隐藏的完整时序是否符合表现预期。
- 尚未在 Unity PlayMode 中验证：选择面板 key 现在不依赖 `HasEnterLevel61` 是否符合当前设计。
- 尚未在 Android / iOS 真机验证横屏触控和性能表现。

## 回滚路径

- 回滚 `Level61ChoicePanelKeyCtrl.cs` 到此前的领取条件版本。
- 回滚 `Level61Ctrl.cs` 中 `menuBook` / `keyDirect` / `StopKey()` 相关改动。
- 如需恢复由 `Level61Ctrl` 主动触发 executor，回滚 `TryTriggerKeyDoorFlow()` 中被注释的执行路径。
- 回滚本地文档到修改前版本。

## 后续验证清单

- Fresh save：`HasEnterLevel61=false`、`HasGetLevel61Key=false` 时进入 Level61，只记录进入且不显示 key。
- No key return：`HasEnterLevel61=true`、`HasGetLevel61Key=false` 时进入 Level61，不激活 key。
- With key return：`HasEnterLevel61=true`、`HasGetLevel61Key=true` 时进入 Level61，`keyObject` 先隐藏、约 2 秒后被激活，随后观察门链路是否继续执行，以及状态是否被正确重置。
- Missing key/executor/handle：临时断开 `keyObject`、`BaseItemExecutor` 或 `BaseItemEventHandle` 时，不重置两个存档状态。
- ChoicePanel hidden：`HasGetLevel61Key=true` 时打开选择面板，Level61 key 不显示且不可点击。
- ChoicePanel show：`HasGetLevel61Key=false` 时打开选择面板，key 延迟后渐显，父节点不移动。
- ChoicePanel claim：key 渐显完成后点击一次，立即保存 `HasGetLevel61Key=true`，播放上跳和下落移出动画，重复点击无额外效果。
- ChoicePanel interruption：显示或领取动画期间关闭面板，不出现关闭后回调重新显示 key。
- MenuBook missing：`menuBook` 断绑时坐下流程不报错，仅缺少书本表现。
- MenuBook bound：当前已绑定 `menuBook`，验证延迟 1.3 秒跳跃、`menuBookOffset` 落点和结束隐藏。
- StopKey path：调用 `StopKey()` 后，`keyDirect` 被禁用且 `MoveFloatHandle` 被启用，无空引用异常。
- Food click：配置至少两个 `foodSprites` 后，气泡显示完成后通过 actor 或 `BubbleClickArea` 点击按顺序切换食物，气泡按缩放表现切换且不接受切换过程中的重复点击。
