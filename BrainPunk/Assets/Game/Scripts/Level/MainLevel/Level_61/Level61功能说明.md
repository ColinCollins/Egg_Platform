# Level61 功能说明

## 2026-07-09 Level61 默认气泡弹出修复

### 需求背景

用户反馈 `Level61Ctrl` 做到椅子上后默认要开始弹出气泡，但当前坐下后气泡没有显示出来。

### 问题原因

- `StartBubbleRevealDelay()` 会在坐下后把气泡透明度设为 `0`，并把气泡 scale 置为 `0`。
- 上一版移除了气泡显示完成后的默认食物切换，避免自动切食物和 Star 计数。
- 但旧的默认食物切换曾间接把气泡 scale 从 `0` 撑回原始大小；移除后只剩 alpha 淡入，scale 仍为 `0`，所以看起来气泡没有弹出。

### 修改后行为

- 坐下后仍调用 `StartBubbleRevealDelay()`，并等待 `bubbleRevealDelay`。
- 延迟结束后同时执行：
  - `FadeBubbleAsync(1f, bubbleFadeInDuration, ...)` 淡入透明度。
  - `PopBubbleScaleAsync(bubbleFadeInDuration, ...)` 将气泡从 `Vector3.zero` 弹回缓存的原始 scale。
- 默认气泡弹出只恢复气泡可见性，不触发食物切换、不增加 `switchFoodCount`、不触发 Star。
- 弹出完成且仍处于坐下状态时，才设置 `isBubbleReadyForClick = true`，后续点击仍走有效点击切换食物逻辑。

### 验证记录

- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过，结果为 98 warnings / 0 errors。
- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_61/Level61Ctrl.cs` 已通过，仅有工作区 LF/CRLF 转换提示。
- 目标脚本敏感链路扫描未命中新增编辑器生命周期、全局事件、资源加载、RemoteConfig、PlayerPrefs、广告、IAP、analytics 或实例化链路。

### 剩余风险与待验证

- Unity PlayMode 尚未运行，需要验证坐下后气泡延迟、淡入和缩放弹出的实际视觉节奏。
- Android/iOS 横屏触控 smoke 尚未运行，需要验证默认弹出后点击食物、快速离开椅子和重复坐下/离开没有残留 tween。

### 回滚路径

- 回退 `RevealBubbleVisualAsync()`、`PopBubbleScaleAsync()` 及 `RevealBubbleAfterDelayAsync()` 中的调用改动，可恢复到只做 alpha 淡入的版本。

## 2026-07-09 Level61 食物切换 Star 触发整理

### 需求背景

本次用户在 `Level61Ctrl` 中补充了食物切换后显示 Star 的逻辑，需要完善计数边界、空引用保护、已收集去重和既有点击链路一致性。

### 影响文件与系统

- `Assets/Game/Scripts/Level/LevelItem/Level_61/Level61Ctrl.cs`
- `Assets/Game/Prefabs/Level/Level_061.prefab`
  - 当前工作区 diff 显示 `Level61Ctrl.star` 已绑定新增的 `Postcard_Star (1)` 节点。
  - 当前工作区 diff 显示 `foodMaxSwitchTriggerCount` 序列化为 `5`。
- 相关系统：
  - Level61 坐下、气泡显示和 actor/BubbleClickArea 点击食物切换。
  - `Postcard_Star` 的存档去重、发奖和 `PostcardGetStarEvent` 派发链路。
  - `StopKey()` 调用的 `DirectMove` / `MoveFloatHandle` 表现切换链路。

### 修改前行为基线

- `OnActorFoodClick(...)` 在 `CanStartActorFoodSwitch()` 通过后触发一次食物切换和翻页表现。
- 新增 Star 逻辑在计数递增前判断 `switchFoodCount >= foodMaxSwitchTriggerCount`，配置为 `5` 时实际第 6 次有效点击才会显示。
- 新增 Star 逻辑直接访问 `star.activeSelf`，`star` 未绑定时会空引用。
- `StartBubbleRevealDelay()` 在气泡显示完成后会主动调用一次 `SwitchFoodByActorClickAsync(...)`，与既有文档中“食物切换由有效点击驱动”的行为说明冲突。
- `StopKey()` 直接访问 `keyDirect` 和同节点 `MoveFloatHandle`，缺失绑定时会空引用。

### 修改后行为

- `Level61Ctrl` 新增中文注释说明 `star` 和 `foodMaxSwitchTriggerCount` 的用途、调用边界和注意事项。
- 进入坐下状态时重置本轮食物切换计数；每次有效点击先递增计数，再判断是否达到阈值，因此 `foodMaxSwitchTriggerCount=5` 时第 5 次有效点击触发 Star。
- Star 只在本轮达到阈值后尝试显示一次；未绑定时记录 warning 并跳过，不再产生空引用。
- Star 显示后若同节点有 `Postcard_Star`，立即调用 `RefreshSavedCollectionState()` 复用既有已收集隐藏逻辑；`Level61Ctrl` 不直接发奖、不写存档、不派发 `PostcardGetStarEvent`。
- 气泡显示完成后不再自动切一次食物，食物切换重新只由 actor 点击、`BubbleClickArea` 点击或本地 raycast fallback 的有效点击触发。
- `StopKey()` 增加 `keyDirect` 和 `MoveFloatHandle` 空引用保护；缺失绑定时记录 warning 后返回。

### 数据、资源、事件与平台流

- 数据/存档：`Level61Ctrl` 本次不新增 DB 字段，不新增保存写入；Star 收集后的存档与背包奖励仍由 `Postcard_Star` 负责。
- 资源/prefab：本次代码不新增资源加载路径。`Level_061.prefab` 当前已有新增 Star 绑定变更，但本次未手工修改 prefab YAML。
- 事件：不新增全局事件，不新增 `PlayEvents`，继续复用 `Postcard_Star` 自身的收集事件链路。
- 平台：未触碰 Android/iOS 平台 SDK、广告、IAP、analytics、RemoteConfig 或 YooAsset。

### 验证记录

- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过，结果为 99 warnings / 0 errors。
- `git diff --check -- Assets/Game/Scripts/Level/LevelItem/Level_61/Level61Ctrl.cs` 已通过，仅有工作区 LF/CRLF 转换提示。
- 针对 `Level61Ctrl.cs` 的敏感链路静态扫描未命中新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`PlayEvents`、`DispatchEvent`、`Subscribe<`、`RemoteConfig`、`PlayerPrefs`、`Resources`、`YooAsset`、`Analytics`、`Advertisement`、`IAP` 或 `Instantiate`。

### 剩余风险与待验证

- 尚未运行 Unity PlayMode，需验证第 5 次有效食物点击时 Star 是否显示、已收集 Star 是否被 `Postcard_Star` 重新隐藏、连续快速点击是否仍只计有效切换。
- 尚未在 Unity Inspector 中确认 `Level61Ctrl.star`、新增 `Postcard_Star (1)`、Collider、`Postcard_Star.index` 和 `foodMaxSwitchTriggerCount` 最终保存状态。
- 尚未做 Android/iOS 横屏触控 smoke，actor 点击、`BubbleClickArea` 点击和本地 raycast fallback 的实际触控舒适度仍待验证。
- `Level_061.prefab` 当前还有用户新增 Star 节点和少量浮点/YAML diff，本次未确认这些 prefab diff 是否全部都应提交。

### 回滚路径

- 回退 `Level61Ctrl.cs` 中 `star` / `foodMaxSwitchTriggerCount` / `switchFoodCount` / `hasTriggeredFoodSwitchStar` 以及 `InitFoodSwitchStar()`、`ResetFoodSwitchStarCounter()`、`RecordFoodSwitchTrigger()`、`TryShowFoodSwitchStar()`。
- 如需恢复气泡显示后自动切一次食物，可把 `StartBubbleRevealDelay()` 末尾的 `SwitchFoodByActorClickAsync(sitCts.Token).Forget()` 加回。
- 如需恢复旧的 `StopKey()` 行为，可移除新增空引用保护，但不建议在缺失 prefab 绑定风险未排除前回退。

## 2026-06-09 Level61 user-adjusted final state

- `Level_061.prefab` 新增 `BubbleClickArea`，挂载 2x2 trigger `BoxCollider2D` 和 `ClickItemEventHandle`，`OnClickEvent` 绑定到 `Level61Ctrl.OnActorFoodClick`，作为坐下后气泡/食物切换的稳定点击区域。
- 现有 actor 点击绑定和 `Level61Ctrl.Update()` 内的 actor raycast fallback 保留；有效点击仍要求 actor 已坐下、thinkBubble 已显示完成、没有处于切换中、且当前 sit token 仍有效。
- `menuBookOffset` 已暴露为 `Level61Ctrl` 序列化参数，`TryPlayMenuBookJump()` 使用 `actorCtrl.transform.position + menuBookOffset` 作为书本跳向 actor 的目标点；当前 prefab 配置为 `{x: 0, y: 0.2, z: 0}`。
- `TryTriggerKeyDoorFlow()` 进入时先隐藏 `keyObject`，引用校验通过后等待 2 秒，再调用 `SetKeyActive(true)` 显示 key。脚本内仍不直接调用 `executor.Execute()`。
- 当前 prefab 同时调整了 key 下落表现：`keyDirect.accelerate` 从 `{x: 0, y: -15}` 改为 `{x: 0, y: -30}`。

## 2026-06-09 Level61 page-turn visibility fix

- `Level61ActorCtrl` now plays `sofa_idle` when the sit jump completes, then starts `turning_the_pages` once after a 1s seated delay if the actor is still sitting and alive.
- Click-triggered food switching still replays `turning_the_pages` once per valid click, and the non-looping animation remains on its final frame.
- `Level61Ctrl` now has a Level61-local actor click fallback in `Update()`: it checks mouse/touch input only when the actor is sitting, the thinkBubble is click-ready, no food switch is running, and the sit token is valid.
- The fallback uses `Physics2D.RaycastAll` and accepts any collider under the actor transform, so the sit trigger area can no longer steal the first raycast hit and block actor food switching.
- The shared `ClickItemEventHandle` remains unchanged; the existing prefab binding is kept as a compatibility path.

## 2026-06-09 Level61 click-food update

- `Level61Ctrl.OnActorFoodClick(Transform, Vector2)` is the bindable actor click API for the actor `ClickItemEventHandle`.
- Actor clicks are accepted only when the actor is sitting, the thinkBubble reveal has completed, the sit cancellation token is still valid, and no food switch is already running.
- Automatic `foodSwitchInterval` cycling is no longer started after bubble reveal; food switching is now driven by valid actor clicks.
- A valid click advances to the next valid `foodSprites` entry and wraps after the final configured sprite. Missing `foodRender`, missing list, or fewer than two valid sprites causes the click to be ignored.
- Each valid click calls `Level61ActorCtrl.PlayTurningThePageOnce()`, scales the thinkBubble transform to zero and back over `clickSwitchDuration`, switches the food at zero scale, and fades the food back to visible.
- `isFoodSwitching` rejects rapid taps during the switch. Leave-sit, disable, destroy, and cleanup kill bubble scale and food tweens, reset click readiness, and restore the bubble scale.
- `Level61ActorCtrl` no longer starts an automatic delayed page-turn loop after sitting. It plays `sofa_idle` after the sit jump, then click-triggered page-turn plays once and holds the final frame.
- `menuBook` original world position and active state are cached before sit movement. Leaving sit shows the menuBook and jumps it back to the original position using `menuBookReturnDuration` and `menuBookReturnJumpPower`.
- `Level_061.prefab` currently binds actor `ClickItemEventHandle.OnClickEvent` to `Level61Ctrl.OnActorFoodClick`, and serializes `clickSwitchDuration=0.5`, `menuBookReturnDuration=0.3`, and `menuBookReturnJumpPower=1`.
- `Level_061.prefab` also binds `BubbleClickArea.ClickItemEventHandle.OnClickEvent` to `Level61Ctrl.OnActorFoodClick`, and serializes `menuBookOffset={x:0,y:0.2,z:0}`.

## 2026-06-09 Verification status

- Static code/prefab inspection completed for the click API, click event binding, no automatic food-cycle start after reveal, click lockout, page-turn once path, bubble scale tween, food midpoint swap, and menuBook return path.
- Unity PlayMode verification is still required for actor tap hit detection, final page-turn frame, bubble scale timing, food fade visibility, menuBook return timing, key flow, restart/fail/exit/complete cleanup, and choice-panel key regression.
- Android/iOS landscape device smoke is still required for touch hit area, rapid taps, 60 fps behavior, and repeated sit/leave cleanup.

更新时间：2026-06-05

## 当前脚本与资源

- 关卡主控脚本：`Assets/Game/Scripts/Level/LevelItem/Level_61/Level61Ctrl.cs`
- 关卡 actor 脚本：`Assets/Game/Scripts/Level/Actor/Level61ActorCtrl.cs`
- 关卡 prefab：`Assets/Game/Prefabs/Level/Level_061.prefab`
- 选择面板钥匙脚本：`Assets/Game/Scripts/Level/LevelItem/Level_61/Level61ChoicePanelKeyCtrl.cs`

## 当前确认的 prefab / 数据状态

- `Level_061.prefab` 根节点挂有 `BaseLevelCtrl` 和 `Level61Ctrl`。
- `BaseLevelCtrl.gamePlayPanelName` 当前为 `GamePlayPanel_021_61`。
- `Level_061.prefab` 的 actor 使用 `Level61ActorCtrl`，`BaseLevelCtrl.actor`、`Level61Ctrl.actorCtrl` 和 `GameCameraCtrl.actor` 指向该组件。
- `Level61Ctrl` 当前序列化字段包括：
  - `actorCtrl`
  - `sitableChair`
  - `sitOffset`
  - `keyObject`
  - `keyExecutor`
  - `groupAlpha`
  - `bubbleRevealDelay`
  - `bubbleFadeInDuration`
  - `bubbleFadeOutDuration`
  - `foodRender`
  - `foodSwitchInterval`
  - `foodFadeDuration`
  - `clickSwitchDuration`
  - `foodSprites`
  - `menuBook`
  - `menuBookReturnDuration`
  - `menuBookReturnJumpPower`
  - `menuBookOffset`
  - `keyDirect`
- `foodSprites` 已在 `Level_061.prefab` 中配置，运行时按数组顺序点击切换；少于两个有效项时跳过食物切换。
- 当前工作区里，`Level_061.prefab` 的 `Level61Ctrl` 序列化段已绑定 `menuBook`，并序列化 `menuBookOffset` 控制坐下后书本跳向 actor 的落点。
- `Level_061.prefab` 新增 `BubbleClickArea` 点击区域，绑定 `ClickItemEventHandle.OnClickEvent -> Level61Ctrl.OnActorFoodClick`。
- `GameData` 已有 `HasEnterLevel61` 和 `HasGetLevel61Key` 两个持久状态，默认值为 `false`。
- `ChoiceLevelPanel.prefab` 中存在 `Level61Key` 节点，挂有 `Level61ChoicePanelKeyCtrl`，并已绑定子节点 `keyBtn`。
- `ChoiceLevelPanel.OnOpen()` 会调用 `Level61ChoicePanelKeyCtrl.TryShow()`；当前调用已做空引用保护。

## Level61 进入状态 / key 显示逻辑

`Level61Ctrl.Start()` 中会调用 `EvaluateLevel61State()`，当前代码逻辑如下：

1. 读取 `DB.GameData`。
2. 如果 `GameData == null`：
   - 记录 warning。
   - 调用 `SetKeyActive(false)`。
   - 结束，不处理后续状态。
3. 如果 `HasEnterLevel61 == false`：
   - 设置 `HasEnterLevel61 = true`。
   - 立即 `Save()`。
   - 调用 `SetKeyActive(false)`。
   - 结束，不处理后续状态。
4. 如果 `HasEnterLevel61 == true` 且 `HasGetLevel61Key == false`：
   - 调用 `SetKeyActive(false)`。
   - 结束，不处理后续状态。
5. 如果 `HasEnterLevel61 == true` 且 `HasGetLevel61Key == true`：
   - 调用 `TryTriggerKeyDoorFlow()`。
   - 只有 `TryTriggerKeyDoorFlow()` 返回 `true` 时，才会把 `HasEnterLevel61` 和 `HasGetLevel61Key` 一起重置为 `false` 并 `Save()`。

## 当前 `TryTriggerKeyDoorFlow()` 实际行为

`Level61Ctrl` 当前并不会在这里直接执行 `executor.Execute()`，实际代码行为如下：

1. 检查 `keyObject` 是否存在；不存在则 warning 并返回 `false`。
2. 取 `BaseItemExecutor`：
   - 优先使用显式绑定的 `keyExecutor`
   - 否则从 `keyObject.GetComponent<BaseItemExecutor>()` 获取
3. 如果 `BaseItemExecutor` 不存在，则 warning 并返回 `false`。
4. 如果 `keyObject` 子层级里没有 `BaseItemEventHandle`，则 warning 并返回 `false`。
5. 等待 2 秒。
6. 调用 `SetKeyActive(true)`。
7. 直接返回 `true`。

当前代码层可直接确认的事实只有：进入流程时会先隐藏 `keyObject`，引用检查通过并等待 2 秒后会激活 `keyObject`，并把两个存档状态重置。脚本内当前没有直接调用 `executor.Execute()`。

## 选择关卡面板钥匙逻辑

1. 打开 `ChoiceLevelPanel` 时调用 `Level61ChoicePanelKeyCtrl.TryShow()`。
2. `Level61ChoicePanelKeyCtrl` 当前只读取 `HasGetLevel61Key`：
   - `HasGetLevel61Key == true` 时隐藏 key，不允许重复领取。
   - `HasGetLevel61Key == false` 时进入延迟显示流程。
3. 显示流程：
   - 先隐藏 `keyBtn` 并禁用点击。
   - 等待 `displayDelay`。
   - 用 `fadeInDuration` 让 key 渐显。
   - 渐显完成后才允许点击。
4. 点击 key 后：
   - 立即设置 `HasGetLevel61Key = true` 并 `Save()`。
   - 禁用重复点击。
   - 播放 `keyBtn` 上跳后向下移出屏幕的反馈动画。
   - 动画结束后隐藏 `keyBtn`。
5. `Level61Key` 父节点保持 active；动画只作用在 `keyBtn` 子节点。
6. 面板禁用或销毁时，显示 tween 和领取 tween 都会停止，并重置点击状态。

## 坐椅 / 气泡 / 食物 / 书本逻辑

1. 玩家点击 Use 按钮后，`Level61Ctrl` 通过 `ToggleSit()` 切换坐下 / 离开。
2. `ToggleSit()` 在 0.8 秒内用 `isSitTransitioning` 防止重复触发。
3. 坐下时：
   - 检查 `actorCtrl` 和 `sitableChair`。
   - 调用 `Level61ActorCtrl.PlaySit(sitableChair, sitOffset)`。
   - 调用 `TryPlayMenuBookJump()`。
   - 启动气泡延迟显示。
4. `TryPlayMenuBookJump()` 当前行为：
   - `menuBook == null` 时只记 warning 并跳过。
   - 先 Kill 上一次 `menuBookTween`。
   - 对 `menuBook` 执行 `DOJump`：
     - 目标点：`actorCtrl.transform.position + menuBookOffset`
     - Jump Power：`1`
     - Num Jumps：`1`
     - Duration：`0.3s`
     - Delay：`1.3s`
   - 动画完成后把 `menuBook.gameObject.SetActive(false)`。
5. 气泡逻辑：
   - 坐下后等待 `bubbleRevealDelay`，默认 2 秒。
   - 如果仍处于坐下状态，则用 `bubbleFadeInDuration` 把 `groupAlpha` 从 0 淡到 1。
   - 离开椅子时，用 `bubbleFadeOutDuration` 淡回 0。
6. 食物逻辑：
   - 只有在气泡显示完成后才允许点击切换。
   - 要求 `foodRender != null`、`foodSprites` 有至少两个有效项。
   - 有效点击来自 actor 点击绑定、`BubbleClickArea` 点击绑定或本地 raycast fallback。
   - 每次有效点击播放一次 `turning_the_pages`，并把 thinkBubble 从 1 缩到 0、切换食物、再放大到 1。
7. 离开椅子、关卡失活或销毁时：
   - 取消坐下延迟 token。
   - 停止食物 tween。
   - Kill 气泡 tween、食物 tween 和 `menuBookTween`。
   - 恢复 actor 坐下状态。

## `StopKey()` 当前行为

- `Level61Ctrl` 暴露了一个公共方法 `StopKey()`。
- 当前实现会：
  - 输出 `Debug.Log("StopKey")`
  - 把 `keyDirect.enabled = false`
  - 把 `keyDirect.GetComponent<MoveFloatHandle>().enabled = true`
- 该方法当前没有空引用保护，依赖 `keyDirect` 和同物体上的 `MoveFloatHandle` 已正确挂载。

## Inspector 参数要求

- `bubbleRevealDelay` 默认 `2s`。
- `bubbleFadeInDuration` 默认 `0.5s`。
- `bubbleFadeOutDuration` 默认 `0.2s`。
- `foodSwitchInterval` 旧自动循环参数当前不再启动食物循环；当前切换由有效点击驱动。
- `foodFadeDuration` 默认 `0.2s`。
- `clickSwitchDuration` 默认 `0.5s`。
- `foodSprites` 由 Level61 prefab Inspector 手动配置，按数组顺序循环。
- `menuBookOffset` 当前 prefab 配置为 `{x: 0, y: 0.2, z: 0}`。
- `Level61ChoicePanelKeyCtrl.displayDelay` 默认 `2s`。
- `Level61ChoicePanelKeyCtrl.fadeInDuration` 默认 `1s`。
- `Level61ChoicePanelKeyCtrl.upwardJumpDistance` 默认 `80`。
- `Level61ChoicePanelKeyCtrl.upwardJumpDuration` 默认 `0.2s`。
- `Level61ChoicePanelKeyCtrl.downwardExitDuration` 默认 `0.45s`。

## 已实现

- Level61 进入状态的读取、写入和持久化。
- 选择关卡面板 Level61 key 的条件显示、延迟渐显、点击领取、立即保存、领取动画和中断清理。
- `ChoiceLevelPanel.OnOpen()` 对 `level61KeyCtrl` 的空引用保护。
- keyObject / keyExecutor / event handle 的引用防护。
- `Level61ActorCtrl` 的 Level61 专属坐下与翻页表现。
- 气泡延迟显示、淡入淡出和点击驱动的食物切换。
- `BubbleClickArea` 稳定点击区域和 actor 点击 fallback。
- `menuBook` 跳跃表现的空引用保护、延迟跳跃、序列化偏移和 tween 清理。
- keyObject 进入开门流程时先隐藏、等待 2 秒后再显示。
- `StopKey()` 公共入口。

## 待验证

- `TryTriggerKeyDoorFlow()` 当前隐藏 `keyObject`、等待 2 秒后激活 `keyObject`，没有在 `Level61Ctrl` 内直接调用 `executor.Execute()`；激活后整条开门链路的最终表现仍需 Unity PlayMode 验证。
- `StopKey()` 调用时 `keyDirect` 与 `MoveFloatHandle` 的实际挂载完整性仍需验证。
- `Level61ChoicePanelKeyCtrl.TryShow()` 的延迟显示、渐显、点击保存、动画移出和面板关闭中断仍需 Unity PlayMode 验证。
- Level61 actor 坐下时 `sofa_jump`、跳椅子、翻页循环、离开时恢复 idle 的完整表现仍需 Unity PlayMode 验证。
- `menuBook` 已在 `Level_061.prefab` 中绑定，仍需验证时序、`menuBookOffset` 落点和离开椅子回跳表现。
- `foodSprites` 当前虽已配置，但最终顺序和点击切换表现仍需 Unity PlayMode 验证。
