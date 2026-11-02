# Level74 功能说明

## 证据边界

- 规格来源：`D:/GitStore/BrinPunk/specs/009-level74-design-analysis/spec.md`、`plan.md`、`data-model.md`、`quickstart.md`。
- 静态证据：`Level_074.prefab`、`GamePlayPanel_022_74.prefab`、`Level74Ctrl.cs`、`Level74GrassCtrl.cs`、`Level74GamePlayPanelCtrl.cs`、`Level74ActorCtrl.cs`。
- 未验证：Unity Editor PlayMode、Android/iOS 横屏触摸、Spine 动画实际存在性、Prefab Inspector 运行时序。

## 当前静态事实

- `Level_074.prefab` 根对象挂有 `BaseLevelCtrl`，`gamePlayPanelName` 为 `GamePlayPanel_022_74`。
- `Level_074.prefab` 根对象挂有 `Level74Ctrl`，当前已绑定 `doorCollider`、`doorSpineCtrl`、`actorCtrl`、`handBone`。
- `Level_074.prefab` 中存在 `Sickle`、`Hoe`、`Eradicator`、`Farmer`、五个 `Grass` 对象。
- `Sickle`、`Eradicator`、`Farmer` 已有 `ClickItemEventHandle`，但静态检查时 `OnClickEvent` 为空。
- `Hoe` 当前初始 inactive，符合“Farmer 释放 Hoe 后可用”的设计方向。
- 五个 Grass 当前挂有 `OnTrigger2DHandle`，targetTag 为 `Player`，但事件仍指向旧的 `SwitchGamePanelUseBtnBoard` 目标类型且 target 为空。
- `Level74GrassCtrl.cs` 当前只有 leaf/root 注释字段和空 `Start` / `Update`。
- `Level74GamePlayPanelCtrl.cs` 当前只有空 `Start` / `Update`。
- `GamePlayPanel_022_74.prefab` 挂有 `Level74GamePlayPanelCtrl`，但当前只有一个新增 `use_btn`，未静态确认独立 cut/dig 两个按钮。
- `Level74ActorCtrl.cs` 当前仍是 Level61 翻书/坐下逻辑拷贝，和 Level74 除草工具状态不匹配。

## 直接结论

- Level74 可以局部实现，不需要修改共享 `ActorCtrl`、`ClickItemEventHandle`、`OnTrigger2DHandle`、`BaseLevelCtrl` 或通用 GamePlayPanel 脚本。
- `Level74ActorCtrl` 必须替换为 Level74 工具状态实现，避免旧翻书逻辑继续影响 actor。
- Grass 必须新增并绑定 `Level74GrassCtrl`，每个 Grass 显式指定 leaf/root，不依赖运行时按名称查找。
- `Level74Ctrl` 需要统一负责工具点击、Farmer 释放、Grass 触发路由、UI 目标、动画锁和开门。
- `GamePlayPanel_022_74` 需要提供 cut/dig 操作入口。当前实现可先用两个 `CustomButton` 字段支持显式绑定，运行时缺少 dig 时可复用 cut 按钮但需要在风险文档记录 UI 待验证。

## 目标行为

- Actor 同一时间只有一个 active tool。
- 点击不同可用工具时，新工具进入 actor 手部，旧工具回到场景初始位置。
- 点击当前 active tool 是 no-op，不重播切换动画。
- Tool 选择和 Grass 操作要求 actor alive 且 grounded。
- Eradicator 状态允许走路，禁止跳跃，场景 Eradicator 对象隐藏，接触 Grass 自动切 leaf，不显示 UI，不计完成。
- Mow 状态接触 Grass 后显示 cut 按钮，点击后 actor 播放 `weeding_mowing`，leaf 被临时隐藏并等待 regrow。
- Farmer 初始可点击，点击后释放 Hoe，Farmer 不再可点击。
- Hoe 状态接触 Grass 后显示 dig 按钮，点击后 actor 播放 `weeding_hoeing`，root 永久移除。
- Hoe 可在 leaf hidden/regrowth-wait 且 active cut lock 结束后挖 root。
- Grass root 全部移除后，`Level74Ctrl.OpenDoor()` 打开门；最终通关仍交给现有门/关卡系统。
- Restart 会销毁并重建当前关卡运行时状态，因此本地运行时状态不写入存档或配置。

## Prefab 绑定清单

- `Level74Ctrl`
  - `doorCollider`
  - `doorSpineCtrl`
  - `actorCtrl`
  - `handBone`
  - `tools`
  - `farmer`
  - `farmerClick`
  - `farmerSpine`
  - `grasses`
- 每个 `Level74GrassCtrl`
  - `leave`
  - `root`
- `GamePlayPanel_022_74`
  - `Level74GamePlayPanelCtrl`
  - `cutButton`
  - `digButton`

## 假设

- `Sickle` 是设计中的 Mow 工具。原因：prefab 内未发现名为 `Mow` 的对象，`Sickle` 有工具外观、trigger collider 和 click handle。
- `Farmer` Spine 存在 `idle` 或可接受缺省 idle 表现；实际动画名仍需 Unity PlayMode/Spine 数据确认。
- `weeding_car`、`weeding_mowing`、`weeding_hoeing` 是 actor Spine 可播放动画；实际存在性仍需 Unity 编译/PlayMode 确认。

## 缺失证据

- 尚未在 Unity Editor Inspector 视觉确认每个 Grass 的 leaf/root 绑定是否为策划预期。
- 尚未 PlayMode 验证 `ClickItemEventHandle.OnClickEvent` 手工 YAML 绑定是否正确调用 `Level74Ctrl`。
- 尚未 PlayMode 验证 `OnTrigger2DHandle` 路由到 `Level74GrassCtrl.OnEnterTrigger/OnExitTrigger`，再转发到 `Level74Ctrl.OnGrassEnter/OnGrassExit`。
- 尚未 PlayMode 验证 `GamePlayPanel_022_74` 的 cut/dig 按钮位置、可触摸性和显隐动画；当前 cut/dig 均绑定到现有 `use_btn` fallback，独立视觉仍需 UI prefab 补充或确认。
- 尚未移动端验证 Android/iOS 横屏触摸、快速点击、动画锁和 60 fps 表现。

## 当前实现状态

- `Level74GrassCtrl` 已实现 leaf cut、regrow、root dig、isDead、animation lock 和 tween cleanup。
- `Level74GamePlayPanelCtrl` 已实现 cut/dig 操作显示、按钮回调绑定、interactable 控制和 cleanup。
- `Level74ActorCtrl` 已替换为 Level74 工具状态，Eradicator 禁跳，Mow/Hoe 可执行一次性工具动画。
- `Level74Ctrl` 进入 Eradicator 状态时隐藏 Eradicator 场景对象；切换到其它工具时由 return 流程恢复显示。
- `Level74Ctrl` 已实现 single active tool、same-tool no-op、Hoe release、tool return/equip、Grass trigger routing、UI target、all roots removed door opening。
- `Level_074.prefab` 已绑定 Level74Ctrl 工具/Farmer/Grass 引用和事件。
- `GamePlayPanel_022_74.prefab` 已绑定 `Level74GamePlayPanelCtrl` 的按钮 fallback。
## 2026-06-11 Level74 v2.0.0 Fire Burn 实现记录

### 已实现行为

- `Level74FireCtrl` 负责三次有效 drag/rub flash 后激活火源；短距离移动不计数，触发一次 flash 后需要重新回到 re-arm 距离内或重新 BeginDrag 后才能再次计数。
- `Level74FireCtrl.ResetRuntimeState()` 会清理 flash 粒子、火源视觉、计数和 re-arm 状态；`Level74Ctrl.ResetRuntimeState()` 会恢复 pre-fire collider、关闭 active fire collider。
- `Level74Ctrl.OnGrassEnter(...)` 现在优先识别 collider 名称为 `fire` 的 active-fire 路径，火接触不再被 actor grounded/alive/tool eligibility 提前挡住。
- `Level74Ctrl` 在 fire contact 时隐藏 cut/dig UI、清理 `selectedGrass`，再由目标草自行判断是否可 burn。
- `Level74GrassCtrl.TryBurn(...)` 只允许 leaf visible、root 未移除、非 Cutting/Digging/Burning/Fading/RegrowWaiting/Regrowing 的草进入 fire burn。
- fire burn 时会从 Level74 当前 `fire` 对象或显式 `grassFireVisualTemplate` 复制视觉实例，禁用实例上的 `Collider2D`、`Rigidbody2D.simulated` 和 `DragEventsListener`，避免草局部火效再次触发 active-fire contact。
- burn 时序为：草局部火效约 `fireDuration` 1s -> 火效按 `fireEffectFadeDuration` 渐隐并销毁 -> leaf 变黑 -> `fireScorchedHoldDuration` 停留 -> `fireFadeDuration` fade -> 复用现有 `regrowDelay` 和 `regrowDuration` 回到 Visible。
- fire burn 不修改 `RootState`，不设置 `IsDead`，不调用 `CheckDoorOpen()`，因此不会替代 Hoe root-removal completion。
- `GameResetEvent` 会重置 Level74 本地 fire/grass/tool runtime state；`SwitchGameStateEvent.SUCCESS/FAILED` 会清理 fire state、临时火效、草 burn sequence 和 UI selection。

### Prefab 绑定要求

- `Level74Ctrl.flashCollider` 指向 pre-fire CampFire collider，初始 enabled。
- `Level74Ctrl.fireCollider` 指向 active fire 对象上的 collider，初始 disabled，且对象名为 `fire`。
- `Level74FireCtrl.OnFireCallback` 绑定到 `Level74Ctrl.OnFireCallback()`。
- `Level74FireCtrl.fire` 指向 Level74 当前火源 `fire` 视觉对象；草局部火效默认复用 `fireCollider.gameObject`，也可以通过 `grassFireVisualTemplate` 显式覆盖。
- 每个 `Level74GrassCtrl` 需要显式绑定 leaf/root；fire burn 只处理 leaf 视觉，不处理 root。

### 待验证

- Unity PlayMode 尚未执行，仍需验证 drag/rub 三次点火、草 burn/regrow 时序、UI 清理、root-only door completion、restart/fail/exit/completion 清理。
- Android/iOS landscape touch smoke 尚未执行，仍需验证触摸拖拽、多指输入、快速拖过多棵草、工具点击和门完成流程。
- `grassFireVisualTemplate` 新增序列化字段当前可为空，代码会回退使用 `fireCollider.gameObject`；Inspector 最终绑定仍需人工确认。

## 2026-06-11 Level74 Fire Burn Optimization Notes

- This optimization only整理 Level74 local scripts. It does not introduce shared abstractions or touch save/config/analytics/resource loading/platform SDK/hot reload.
- `Level74FireCtrl` now centralizes drag preconditions in `CanProcessDrag()`, removes high-frequency drag logs, and keeps the renamed `maxFlashCount` compatible with the old serialized `MaxFlashCount` field through `FormerlySerializedAs`.
- `Level74FireCtrl` keeps the no-touch fallback through `DragEventsListener.CurrentScreenPosition`; when a drag callback target is null, flash positioning falls back to the fire controller transform.
- `Level74Ctrl` groups fire helper methods in a local `Fire` region. Active-fire contact still runs before actor eligibility checks.
- `Level74GrassCtrl` splits leaf/fire busy-state checks into named helpers. Burn timing, root-only completion, and cleanup boundaries are unchanged.
- Static compile check passed after the refactor: `dotnet build Assembly-CSharp.csproj --no-restore` returned 0 errors / 90 warnings. Unity PlayMode and Android/iOS landscape touch smoke remain unverified.

## 2026-06-23 Level74 Grass Regrow 地下生长表现

### 需求背景

- 需求：`Level74GrassCtrl` 在 regrow 阶段增加草从地下长出来的过程；Fire grass 变黑后先等待，再渐隐；叶子的临时火苗消失时也增加渐隐。
- 范围：只调整 Level74 草叶 `leaf` 的运行时视觉表现，不修改工具选择、Grass trigger、root 挖除、Door completion、save/config/resource/event/analytics/platform 流程。

### 已实现行为

- `Level74GrassCtrl` 新增 `regrowStartOffsetY` 序列化参数，表示 regrow 开始时 leaf 相对初始本地坐标向下偏移的距离，默认 `0.45f`。
- `Level74GrassCtrl` 新增 `fireScorchedHoldDuration` 序列化参数，表示 Fire grass 变黑后进入渐隐前的停留时间，默认 `0.5f`。
- `Level74GrassCtrl` 新增 `fireEffectFadeDuration` 序列化参数，表示叶子临时火苗正常结束时的淡出时长，默认 `0.25f`。
- Mow cut 后的 regrow 和 fire burn 后的 regrow 都复用同一套 leaf 重生流程：先将 leaf 放到初始位置下方、alpha 为 0、scale 为 0，再用 `regrowDuration` 移动回初始本地坐标，同时恢复 scale 和 alpha。
- Fire burn 的渐隐现在使用独立 Append 段，顺序保持为火效持续、火效渐隐/销毁、leaf 变黑、黑色停留、leaf 渐隐、等待、地下生长，避免停留时间和渐隐同时发生。
- 火效渐隐只在正常 burn sequence 中执行；reset、失败、退出、`OnDisable`、`OnDestroy` 等清理路径仍立即销毁临时火效，避免跨状态残留。
- `CompleteRegrow()` 仍把 leaf 状态恢复为 `Visible` 并调用 `ResetLeafVisual(true, 1f)`，确保最终位置、旋转、缩放和颜色回到初始缓存值。
- fire burn 仍只处理 leaf 临时视觉，不修改 `RootState`，不设置 `IsDead`，不触发 `CheckDoorOpen()`。

### 验证与待补

- 静态编译：已执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`，结果 0 error / 100 warnings。
- Unity PlayMode 尚未执行，仍需验证 Mow cut regrow、fire burn 火效渐隐、fire burn 变黑停留、fire burn 地下生长的起点、移动距离、透明度、scale、root-only completion、restart/fail/exit cleanup。
- Android/iOS 横屏触摸与性能尚未执行，仍需验证快速 cut/fire 多棵草时没有视觉残留或交互卡顿。

## 2026-06-23 Level74FireCtrl 点火稳定性完善

### 需求背景

- 需求：完善 `Level74FireCtrl` 当前脚本逻辑，重点处理拖拽点火计数、摩擦音效、粒子/火源清理和空引用边界。
- 范围：只修改 Level74 本地火源控制脚本和 Level74 本地文档；不修改 prefab、Grass、Door、save/config/resource/event/analytics/platform SDK 或共享脚本。

### 原有行为基线

- `Level74FireCtrl` 原先用 `count >= maxFlashCount` 直接作为 `IsActiveFire`，如果 `fire` 视觉对象缺失，`PlayFire()` 返回后内部计数仍可能让外部认为火源已 active。
- `Update()` 原先直接访问 `flash.isPlaying`，当 `flash` 未绑定时存在空引用风险。
- `OnDisable()` 原先只解绑拖拽事件，不主动停止 `flash` 粒子、摩擦循环音效或火源 tween。
- 触发一次 flash 后，锁定状态中每帧都会更新 `segmentStartScreen`，容易让 re-arm 判断在下一帧被快速满足。

### 已实现行为

- `IsActiveFire` 现在只在 `PlayFire()` 成功启用 `fire` 视觉后变为 true；`fire` 缺失时会保留为非 active，并把计数压回未完成状态。
- `maxFlashCount` 通过本地 `RequiredFlashCount` 限制为至少 1，避免配置为 0 或负数后直接进入 active-fire 状态。
- `Awake()` 会在序列化字段为空时尝试从当前对象或子对象解析 `DragEventsListener`；已有 prefab 显式绑定仍优先使用。
- `OnEnable()` / `OnDisable()` / `OnDestroy()` / `Update()` 增加 `Application.isPlaying` 外层保护，避免非运行态入口触发 runtime 音效、粒子或事件链。
- 拖拽事件订阅增加 `isSubscribed` 标记，避免重复订阅；禁用和销毁时统一解绑并停止摩擦循环音效。
- `UpdateFlashPosition(...)` 在 `flash` 缺失时安全返回，不再尝试播放摩擦音效；`Update()` 也会在粒子停止或缺失时清理循环音效。
- `PlayFire()` 现在先 `DOKill()` 旧火源缩放 tween，再播放 active fire 缩放，并使用 `SetLink(gameObject)` 绑定生命周期。
- `ResetRuntimeState()`、`OnDisable()`、`OnDestroy()` 统一走 `StopFlashFeedback(true)` 清理 flash 粒子和摩擦音效。
- re-arm 逻辑现在在锁定状态下只在当前指针回到 `minRearmDistance` 内时更新段落起点并解锁；否则保持锁定，直到玩家回到 re-arm 距离内或重新 BeginDrag。

### 数据与事件流

- 运行时入口仍为 `DragEventsListener.BeginDragHandlers` / `DragHandlers`。
- 点火链路保持为：拖拽达标 -> `PlayFlash()` 计数 -> 达到 `RequiredFlashCount` -> `PlayFire()` -> `OnFireCallback` -> `Level74Ctrl.OnFireCallback()` 启用 active fire collider。
- 本次不新增 save/config/resource loading/analytics/platform SDK 链路，不新增实例化，也不修改 `Level74Ctrl`、`Level74GrassCtrl` 或 Door completion。

### 验证与待补

- 静态禁用调用扫描：`Level74FireCtrl.cs` 未命中 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、RemoteConfig、Resources、YooAsset、PlayerPrefs、Analytics 或 Instantiate。
- 静态编译：已执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`，结果 0 error / 96 warnings；warnings 属于当前工程既有范围。
- Unity PlayMode 尚未执行，仍需验证三次点火、回拉 re-arm 手感、摩擦音效停止、reset/fail/success/exit 后没有粒子或声音残留。
- Android/iOS 横屏触摸尚未执行，仍需验证单指/多指 drag、快速摩擦和快速重开时的点火稳定性。

### 回滚路径

- 恢复 `Level74FireCtrl.cs` 中 `isActiveFire`、`RequiredFlashCount`、订阅保护、运行态保护、音效清理、fire tween lifecycle 和 re-arm 判断相关改动。
- 文档记录不影响 runtime，可随脚本回滚同步恢复或作为历史证据保留。

## 2026-07-13 Star regrow 仅生成一次

- 需求：索引 `2` 的 Grass regrow 随机命中并显示 star 后，同一 Level74 控制器生命周期内不允许再次生成。
- 原有基线：每次目标 Grass 进入 `OnGrassRegrown(...)` 都会重新执行概率判定；star 被其它组件隐藏或收集后，后续 regrow 仍可能再次调用 `SetActive(true)`。
- 当前行为：`Level74Ctrl` 使用本地布尔状态 `hasGeneratedStar` 记录首次成功；随机命中时先置为 `true`，再激活 star。后续回调在随机判定前返回，不会重复显示。
- 生命周期：`ResetRuntimeState()` 不清除 `hasGeneratedStar`，因此同一控制器实例内重开关卡也不会再次生成；销毁并重新创建 Level74 控制器后状态重新从 `false` 开始。不新增存档字段或全局状态。
- 日志：`OnGrassRegrown` 的 DEBUG 日志增加 `hasGeneratedStar`，用于确认一次性拦截状态。
- 影响文件仅为 `Level74Ctrl.cs` 及本地中文文档；不修改 prefab、Grass tween、star 收集组件、DB/save、配置、资源、事件或平台 SDK。
- 验证：`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 通过，0 error / 1116 warning；warning 属于工程既有范围。
- 未验证：Unity PlayMode 尚未实测首次命中、收集/隐藏后再次 regrow、GameResetEvent 重开和控制器销毁重建；Android/iOS 横屏尚未验证。
- 回滚：移除 `hasGeneratedStar` 字段、guard、命中赋值和日志字段，即恢复每次 regrow 都可重新抽取的行为。

## 2026-07-13 Grass 索引 2 重生随机显示星星

### 需求背景

- 需求：`Level74Ctrl.grasses` 中零基索引为 `2` 的草开始 regrow 时，以简单随机方式按 20% 概率把 `star` 设为 active。
- 范围：只增加 Level74 本地草重生到星星显隐的调用，不修改割草、火烧、挖根、开门、存档、配置、资源加载、事件、打点或平台 SDK 流程。

### 原有行为基线

- `Level74GrassCtrl.BeginRegrow()` 是 Mow cut 与 Fire burn 共用的草叶重生入口，原先只切换 `LeafState.Regrowing` 并准备地下生长视觉。
- `Level74Ctrl` 已有按 Inspector 顺序保存的 `grasses` 列表；本次修改前没有草重生回调，也没有随机显示星星的逻辑。
- 工作区中已有未提交的 `star` 序列化字段和禁用的 `Postcard_Star (1)` prefab 实例，但 `Level74Ctrl` 的 prefab 序列化数据尚未绑定该对象。

### 已实现行为

- `BeginRegrow()` 在草叶进入 `Regrowing` 且准备好重生视觉后调用 `levelCtrl?.OnGrassRegrown(this)`；因此 Mow cut 和 Fire burn 的正常重生都会进入同一判定。
- `Level74Ctrl.OnGrassRegrown(...)` 只接受 `grasses[2]` 对应的实例；列表不足 3 项、实例不匹配或 `star` 未绑定时直接返回。
- 目标草每次开始重生时执行 `Random.value < generateProbability`；命中后调用 `star.SetActive(true)`。初始需求为 20%，当前源码默认值已由后续工作区调整为 `0.5f`，本次日志需求不修改该值。
- `Level_074.prefab` 已把 `Level74Ctrl.star` 绑定到当前初始禁用的 `Postcard_Star (1)` GameObject。

### 数据、资源与事件流

- 调用链为 `Level74GrassCtrl.BeginRegrow()` → `Level74Ctrl.OnGrassRegrown(grass)` → `UnityEngine.Random.value` → `star.SetActive(true)`。
- 不新增全局事件、运行时实例化、DB/save、配置、资源地址、RemoteConfig、PlayerPrefs、analytics、广告、IAP 或平台 SDK 调用。
- 每次判定为常数时间，不新增 `Update`、轮询或集合遍历；对 Android/iOS 横屏运行时没有额外平台分支。

### 验证与待补

- CodeGraph 静态证据确认 `BeginRegrow()` 有两个调用入口，分别来自 Mow cut 重生和 Fire burn 重生；目标控制器没有现成测试覆盖。
- 静态 prefab 复核确认 `grasses` 第 3 项为 fileID `684921366059065066`，`star` 绑定到初始禁用的 fileID `2760197287872847478`。
- 已执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`，结果 0 error / 1116 warning；warning 属于当前工程既有范围。
- 已执行目标文件 `git diff --check` 和禁用链路扫描；最终结果见本关风险评估对应条目。
- Unity PlayMode 尚未执行：仍需验证索引 2 的草在 Mow/Fire 重生时可按随机结果显示星星、其它草不会触发、缺失绑定不报错，以及星星显示后的收集/重开行为。
- Android/iOS 横屏与当前配置概率的长期统计分布尚未验证。

### 回滚路径

- 移除 `Level74GrassCtrl.BeginRegrow()` 中的 `OnGrassRegrown(...)` 调用、`Level74Ctrl.OnGrassRegrown(...)` 和 `star` 字段，并移除 `Level_074.prefab` 的 `star` 绑定，即可恢复仅播放草重生视觉的原行为。
- `Postcard_Star (1)` prefab 实例及其其它未提交调整属于任务开始前已有工作，不在本次回滚范围内。

## 2026-06-23 Level74 Grass 烧焦变黑过程参数

### 需求背景

- 需求：`Level74GrassCtrl` 中 Fire grass 叶子烧焦变黑不能瞬间完成，需要提供一个 duration 让策划可控制变黑过程。
- 范围：只调整 Level74 草叶 `leaf` 的运行时颜色过渡表现，不修改工具选择、Grass trigger、root 挖除、Door completion、save/config/resource/event/analytics/platform 流程。

### 原有行为基线

- Fire burn 原有时序在火效持续和火效渐隐后调用 `EnterScorchedFade()`，该方法直接把 `leaf` 的 `SpriteRenderer.color` 设置为黑色，因此烧焦变黑是瞬时表现。
- `fireScorchedHoldDuration` 只控制变黑后进入渐隐前的停留时间，`fireFadeDuration` 只控制黑色 leaf 渐隐时间，二者都不是变黑过程时长。

### 已实现行为

- `Level74GrassCtrl` 新增 `fireScorchedDuration` 序列化参数，默认 `0.25f`，用于控制 Fire grass 从原始 leaf 颜色过渡到焦黑色的时间。
- Fire burn 时序调整为：火效持续、火效渐隐/销毁、leaf 按 `fireScorchedDuration` 逐步变黑、黑色按 `fireScorchedHoldDuration` 停留、leaf 按 `fireFadeDuration` 渐隐、等待、地下生长恢复。
- `EnterScorchedFade()` 现在只进入 scorched fading 状态并确保 leaf 可见，不再瞬时设置黑色；颜色变化统一由 `AppendLeafScorchedAlpha(..., 1f, fireScorchedDuration)` 驱动。
- 本次不修改 `RootState`、`IsDead`、`CheckDoorOpen()`、`Level74Ctrl`、prefab 序列化值或共享系统。

### 验证与待补

- 静态禁用调用扫描：`Level74GrassCtrl.cs` 未命中 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、RemoteConfig、Resources、YooAsset、PlayerPrefs、Analytics、IAP 或 Advertisement。
- 静态编译：已执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`，结果 0 error / 1114 warnings；warnings 属于当前工程既有范围。
- `git diff --check -- Assets\Game\Scripts\Level\LevelItem\Level_74\Level74GrassCtrl.cs` 已通过，仅提示 Git 工作区 LF/CRLF 转换。
- Unity PlayMode 尚未执行，仍需目视验证变黑过程时长、黑色停留、黑色渐隐、fire burn 后地下生长、restart/fail/exit/completion 中断恢复。
- Android/iOS 横屏触摸与性能尚未执行，仍需验证快速拖火经过多棵草时无视觉残留或卡顿。

### 回滚路径

- 恢复 `Level74GrassCtrl.cs` 中 `fireScorchedDuration` 字段、burn sequence 中变黑 tween 段，以及 `EnterScorchedFade()` 不再直接设置黑色的改动，即可回到瞬时变黑表现。
- 文档记录不影响 runtime，可随脚本回滚同步恢复或作为历史证据保留。

## 2026-07-13 OnGrassRegrown 执行日志

- 需求：在 `OnGrassRegrown(...)` 增加日志，以确认 Grass regrow 回调是否实际进入控制器。
- 原有基线：方法在任何输出前执行目标索引与 star 绑定检查，提前返回时无法从 Console 区分“没有回调”和“回调后被过滤”。
- 当前行为：方法入口先计算 `isTargetGrass`，再通过 Bear.Logger 的 `this.Log(...)` 输出 grass 名称、是否为 `grasses[2]`、star 是否绑定和当前 `generateProbability`；随后继续原有过滤与随机激活流程。
- 数据与事件：只增加 DEBUG 日志，不新增事件、存档、配置、资源加载、实例化或平台 SDK 调用，也不改变随机判定次数。
- 验证：`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 通过，0 error / 98 warning；warning 属于工程既有范围。
- 未验证：尚未在 Unity PlayMode 观察实际 Console 输出；Bear.Logger 的普通日志受 `DEBUG_MODE` 编译符号控制，关闭时不会显示。
- 回滚：移除 `isTargetGrass` 日志行并恢复等价的目标草条件判断即可；不需要回滚 regrow 或 star 功能。

## 2026-06-24 Level74 Tool 切换 SortingOrder

### 需求背景

- 需求：`Level74Ctrl` 切换 Tool 时，当前在手上的 tool `sortingOrder` 变为 `300`，卸下回到场景位置时变为 `150`。
- 范围：只调整 Level74 本地工具对象的运行时渲染排序；不修改工具选择条件、Grass 操作、Door completion、save/config/resource/event/analytics/platform 流程。

### 原有行为基线

- `Level74Ctrl.AppendEquipTool(...)` 原先只把 Mow/Hoe 工具挂到 `handBone` 并播放位置、旋转、缩放 tween，没有统一调整工具 `sortingOrder`。
- `AppendReturnTool(...)` 和 `RestoreTool(...)` 原先只恢复 parent/local transform/active，没有统一把工具卸下后的 `sortingOrder` 设回固定值。
- `Level_074.prefab` 静态文本显示 `Sickle`、`Eradicator` 的初始 `m_SortingOrder` 为 `1`，`Hoe` 当前为 `300`，运行时切换依赖 prefab 初始值不稳定。

### 已实现行为

- `Level74Ctrl` 新增本地常量：手持工具 `ToolEquippedSortingOrder = 300`，卸下工具 `ToolUnequippedSortingOrder = 150`。
- `SetToolSortingOrder(...)` 会遍历当前 tool 子级的 `SpriteRenderer` 和 `SortingGroup`，统一设置 `sortingOrder`。
- `AppendEquipTool(...)` 在 Mow/Hoe 工具挂到 `handBone` 后设置为 `300`。
- `AppendReturnTool(...)` 在旧工具回到初始父节点后设置为 `150`。
- `RestoreTool(...)` 在重置/恢复工具初始状态时设置为 `150`。
- Eradicator 当前沿用原有选择后隐藏场景对象的表现；隐藏前同步设置为卸下排序 `150`，未新增手持 Eradicator 场景对象。

### 数据与事件流

- 入口仍是原有 `ClickTool` / `ClickMow` / `ClickHoe` / `ClickEradicator` / Farmer 释放 Hoe 后的 `SelectTool(...)`。
- 本次只改变工具对象下 `SpriteRenderer.sortingOrder` 和 `SortingGroup.sortingOrder`，不新增事件订阅、存档写入、配置读取、资源加载、实例化、打点或平台 SDK 调用。

### 验证与待补

- 格式检查：已执行 `git diff --check -- Assets\Game\Scripts\Level\LevelItem\Level_74\Level74Ctrl.cs`，无错误，仅提示 Git 工作区 LF/CRLF 转换。
- 静态风险扫描：`Level74Ctrl.cs` 只命中旧文件顶部注释中的 `Instantiate` 与已有 `OnGameReset` 方法名；本次新增排序代码未引入这些链路。
- 静态编译：已执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`，结果 0 error / 1114 warnings；warnings 属于当前工程既有范围。
- Unity PlayMode 尚未执行，仍需验证 Sickle/Hoe 切换时手持为 300、旧工具回场景为 150、Eradicator 选择/切回后排序和显隐符合预期。
- Android/iOS 横屏触摸尚未执行，仍需验证快速切换工具时视觉层级无闪烁或遮挡异常。

### 回滚路径

- 恢复 `Level74Ctrl.cs` 中 `UnityEngine.Rendering` using、`ToolEquippedSortingOrder` / `ToolUnequippedSortingOrder` 常量、`SetToolSortingOrder(...)` 方法，以及 `RestoreTool(...)`、`AppendReturnTool(...)`、`AppendEquipTool(...)` 中的排序调用，即可回到依赖 prefab 初始排序的旧行为。
- 文档记录不影响 runtime，可随脚本回滚同步恢复或作为历史证据保留。
