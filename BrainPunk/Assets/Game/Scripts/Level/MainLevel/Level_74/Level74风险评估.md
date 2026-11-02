# Level74 风险评估

## 原始行为基线

- `Level_074.prefab` 已存在 Level74 场景、工具、Farmer、Grass、Door、Actor 美术和碰撞对象。
- `BaseLevelCtrl.gamePlayPanelName` 已配置为 `GamePlayPanel_022_74`。
- `Level74Ctrl` 已绑定 door、actor、hand bone，但当前运行时逻辑只包含 `OpenDoor()`。
- `Level74ActorCtrl` 当前是旧 Level61 翻书逻辑，不是可接受的 Level74 完成态。
- Grass 当前只有触发组件，没有 `Level74GrassCtrl` 状态组件。

## 允许变更范围

- `Assets/Game/Scripts/Level/LevelItem/Level_74/Level74Ctrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_74/Level74GrassCtrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_74/Level74GamePlayPanelCtrl.cs`
- `Assets/Game/Scripts/Level/Actor/Level74ActorCtrl.cs`
- `Assets/Game/Prefabs/Level/Level_074.prefab`
- `Assets/Game/UI/GamePanel/Resources/GamePlayPanel_022_74.prefab`
- Level74 本地文档和 `DevelopMD/已有功能设计与使用记录.md`

## 禁止变更范围

- 共享脚本、共享 prefab、save/config、analytics/resource loading、ProjectSettings、平台 SDK、hot reload。
- 如必须触碰共享系统，需要先更新 `specs/009-level74-design-analysis/plan.md` 并追加非 Level74 回归范围。

## 现有系统使用

- `ActorCtrl`：移动、跳跃、alive/grounded 状态和 actor Spine 引用。
- `ActorSpineCtrl`：播放 actor、door、Farmer Spine 动画。
- `ClickItemEventHandle`：工具和 Farmer 点击/触摸入口。
- `OnTrigger2DHandle`：Grass 与 Player 的 enter/exit 触发入口。
- `CustomButton`：GamePlayPanel cut/dig UI 点击。
- DOTween：工具移动、Grass 渐隐/恢复、UI 显隐。
- `BaseLevelCtrl`/现有门系统：Door 打开后的通关交接。

## 风险项

- P1：Prefab 手工 YAML 绑定错误会导致工具、Farmer、Grass 或 UI 不触发。需要 Unity Inspector 和 PlayMode 验证。
- P1：如果 Grass leaf/root 绑定不符合美术层级，cut/dig 表现会隐藏错误对象。需要视觉验证。
- P1：`GamePlayPanel_022_74` 当前只有一个按钮，若仅复用同一按钮表示 cut/dig，视觉语义可能不满足最终 UI。需要 UI prefab 补充或策划确认。
- P1：Spine 动画名 `weeding_car`、`weeding_mowing`、`weeding_hoeing`、Farmer `idle` 未通过运行时确认，可能播放失败后退回 idle。
- P1：动画锁如果未在异常路径清理，可能造成 softlock。脚本需要 `OnDisable` / `OnDestroy` kill tween 并隐藏 UI。
- OBS：未改共享系统时非 Level74 回归不是阻塞项，但仍需记录未覆盖范围。

## 回滚路径

- 恢复四个 Level74 脚本到实现前版本。
- 恢复 `Level_074.prefab` 和 `GamePlayPanel_022_74.prefab`。
- 移除或保留本地文档均不影响运行时；若回滚交付文档，可删除本文件和 `Level74功能说明.md`。

## 手动验证清单

- 静态：`Level_074.prefab` 仍使用 `GamePlayPanel_022_74`。
- 静态：所有 Grass 都有 `Level74GrassCtrl`，leaf/root 为非空显式引用。
- 静态：工具和 Farmer 点击事件绑定到 `Level74Ctrl`；Grass 触发事件绑定到 `Level74GrassCtrl` 并转发到 `Level74Ctrl`。
- 静态：GamePlayPanel 可提供 cut/dig 操作按钮。
- PlayMode：无工具接触 Grass 无变化、无 UI。
- PlayMode：Eradicator 可选择后场景对象隐藏、同工具点击 no-op、可走路不可跳、接触 Grass 自动 cut；切换到其它工具后 Eradicator 回到初始位置并恢复显示。
- PlayMode：Mow 接触 Grass 显示 cut，离开隐藏，点击 cut 后 leaf 隐藏并 regrow。
- PlayMode：Farmer 点击后 Hoe 出现，Farmer 不再可点击。
- PlayMode：Hoe 接触 Grass 显示 dig，点击 dig 后 root 永久移除。
- PlayMode：cut lock 结束后，Hoe 可在 leaf hidden/regrowth-wait 阶段 dig root。
- PlayMode：最后一个 root 移除后 Door 立即打开，actor 进入后由现有系统完成通关。
- PlayMode：tool jump、grass animation、UI 显示、door opening 中 restart/fail/exit 不留下 stale UI/tween/selected grass。
- 移动端：Android/iOS 横屏触摸可完成工具点击、Farmer、Grass、cut/dig、Door entry。

## 验证证据

- 静态证据：已读取 `spec.md`、`plan.md`、`tasks.md`、`quickstart.md`、`Level_074.prefab`、`GamePlayPanel_022_74.prefab` 和 Level74 相关脚本。
- 静态绑定：`Level_074.prefab` 已绑定 `Level74Ctrl` 的 tool/farmer/grass 字段；五个 Grass 已挂 `Level74GrassCtrl` 并显式绑定 leaf/root；Grass `OnTrigger2DHandle` 已路由到 `Level74GrassCtrl.OnEnterTrigger/OnExitTrigger`，再由 Grass 控制器转发到 `Level74Ctrl.OnGrassEnter/OnGrassExit`；Sickle/Eradicator/Hoe/Farmer 点击已路由到 `Level74Ctrl`。
- UI 绑定：`GamePlayPanel_022_74.prefab` 已绑定 `Level74GamePlayPanelCtrl.cutButton/digButton`，当前两个字段均指向现有 `use_btn` 作为 fallback。独立 cut/dig 视觉仍需后续 UI prefab 验证或补充。
- 编译：已执行 `dotnet build Assembly-CSharp.csproj --no-restore`，结果 0 error。存在项目既有 warning；Level74 新增序列化字段 warning 已在 prefab 绑定后再次编译确认不产生 error。
- PlayMode：未执行。
- Android/iOS：未执行。
- 非 Level74 回归：当前 diff 限定为 Level74 本地脚本、Level74 prefab/UI、Level74 文档和 learned-facts 文档；未修改共享脚本或共享 prefab，因此按当前 scope 不要求非 Level74 回归。

## 未验证缺口

- Unity Inspector prefab 绑定视觉确认。
- Unity Editor PlayMode。
- Android/iOS 真机或模拟器横屏触摸。
- Spine 动画实际可播放性。
## 2026-06-11 Level74 Fire Burn 风险评估补充

### 原始行为基线

- Level74 原有完成目标仍是 Hoe 永久移除全部 root 后由 `Level74Ctrl.CheckDoorOpen()` 调用 `OpenDoor()`。
- Eradicator/Mow/Hoe/Farmer/tool panel/door handoff 逻辑保留在 Level74 本地脚本内，未修改共享 `ActorCtrl`、`ClickItemEventHandle`、`OnTrigger2DHandle`、`BaseLevelCtrl`。
- fire burn 是 leaf 临时视觉路径，不应改变 root、save/config、analytics、resource loading、ProjectSettings、platform SDK 或 hot reload。

### 本次影响文件

- `Assets/Game/Scripts/Level/LevelItem/Level_74/Level74Ctrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_74/Level74FireCtrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_74/Level74GrassCtrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_74/Level74功能说明.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_74/Level74风险评估.md`
- `DevelopMD/已有功能设计与使用记录.md`
- `specs/014-level74-fire-burn/tasks.md`

### 已规避风险

- P1: active fire 被 actor grounded/alive/tool eligibility 误挡。处理：`OnGrassEnter` 先识别 collider name `fire`，再进入 fire burn path。
- P1: burn 误开门或误完成。处理：`TryBurn` 不修改 `RootState`，不设置 `IsDead`，不调用 `CheckDoorOpen()`。
- P1: grass-local fire 复制 active fire collider 后递归触发。处理：复制实例后禁用 `Collider2D`、`Rigidbody2D.simulated` 和 `DragEventsListener`。
- P1: 同一草重复触发导致时序重叠。处理：`CanBurn()` 只允许 leaf visible 且非所有动画/等待状态。
- P1: reset/fail/success 后残留火效或延迟回调。处理：`GameResetEvent` 走 `ResetRuntimeState()`，`SUCCESS/FAILED/OnDisable/OnDestroy` 走 cleanup，草侧 kill sequence 并 destroy fire effect。
- P1: Editor/鼠标路径 `Input.GetTouch(0)` 崩溃。处理：无 touch 时使用 `DragEventsListener.CurrentScreenPosition`，有 touch 时才读取 touches。

### 验证证据

- 静态读取：已检查 `specs/014-level74-fire-burn/spec.md`、`plan.md`、`data-model.md`、`research.md`、`quickstart.md`、`tasks.md`。
- Prefab 静态证据：`Level_074.prefab` 中存在 `CampFire`、`fire`、`firewood`；`Level74FireCtrl.OnFireCallback` 已绑定到 `Level74Ctrl.OnFireCallback()`；`flashCollider` / `fireCollider` 已序列化绑定；active fire 对象名为 `fire` 且初始 inactive / collider disabled。
- 编译：已执行 `dotnet build Assembly-CSharp.csproj --no-restore`，结果 0 error / 90 warnings。warnings 为项目既有 warning 类型，未发现 Level74 fire burn 新增 error。

### 2026-06-11 优化补充

- 本次优化保持 Level74 fire burn 的玩家可见行为不变，只整理 Level74 本地脚本，不抽取共享逻辑，不修改共享系统或共享 prefab。
- `Level74FireCtrl` 移除拖拽过程高频日志，统一拖拽前置判断，保留 `FormerlySerializedAs("MaxFlashCount")` 以兼容旧序列化字段，并补充 drag target 为空时的 flash 定位回退。
- `Level74GrassCtrl` 将 leaf/fire busy state 判断拆成命名 helper，燃烧时序、root-only completion、fire effect cleanup 语义不变。
- `Level74Ctrl` 将 fire helper 集中到 `Fire` region；active-fire contact 仍优先于 actor eligibility。
- 优化后静态编译已通过：0 error / 90 warnings。Unity PlayMode、Inspector 目视确认、Android/iOS landscape touch smoke 仍未执行。

### 未验证缺口

- Unity Editor PlayMode 未执行：三次 flash 点火、短拖拽不计数、active fire persistence、grass burn 1s + 0.5s fade + regrow、UI 清理、root-only completion 仍需实测。
- Android/iOS landscape touch 未执行：多指、快速拖拽、工具点击互不干扰和性能仍需真机或模拟器 smoke。
- Inspector 视觉确认未执行：`grassFireVisualTemplate` 可为空并回退 `fireCollider.gameObject`，但最终 Inspector 绑定和火效视觉位置仍需人工确认。
- Restart/exit/completion 的实际销毁顺序未 PlayMode 覆盖；当前代码提供 cleanup 路径，但仍需场景生命周期验证。

### 回滚路径

- 回滚本次 Level74 runtime 改动：恢复 `Level74Ctrl.cs`、`Level74FireCtrl.cs`、`Level74GrassCtrl.cs` 到修改前版本。
- 如 prefab 后续绑定发生问题，本次脚本支持 `grassFireVisualTemplate` 为空回退；必要时恢复 `Level_074.prefab` 的 fire 相关字段。
- 文档和 `tasks.md` 为交付证据，不影响 runtime，可按规格历史保留或随代码回滚同步恢复。

## 2026-06-23 Grass Regrow 地下生长风险评估补充

### 原始行为基线

- Level74 草叶 regrow 原有路径由 `Level74GrassCtrl` 本地 DOTween sequence 驱动，Mow cut 和 fire burn 都会在 `regrowDelay` 后恢复 leaf 到可见状态。
- Level74 完成目标仍是 Hoe 永久移除全部 root 后由 `Level74Ctrl.CheckDoorOpen()` 打开 Door；leaf regrow 不应影响 root 状态或 Door completion。
- `GameResetEvent`、`SwitchGameStateEvent.SUCCESS/FAILED`、`OnDisable`、`OnDestroy` 已有 cleanup 路径会 kill grass tween 并清理 fire effect。

### 本次影响文件

- `Assets/Game/Scripts/Level/LevelItem/Level_74/Level74GrassCtrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_74/Level74功能说明.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_74/Level74风险评估.md`
- `DevelopMD/已有功能设计与使用记录.md`

### 风险项

- P1: `regrowStartOffsetY` 默认值与实际美术地面线不匹配，可能表现为草从过深或过浅位置出现。需要 Unity Inspector/PlayMode 目视调参。
- P1: `fireEffectFadeDuration` 默认值与当前火效模板不匹配时，可能表现为火苗消失过快、拖慢 leaf 变黑节奏，或模板没有 `SpriteRenderer` 时只能等待时长后销毁。需要 PlayMode 目视确认。
- P1: `fireScorchedHoldDuration` 默认值与关卡节奏不匹配时，Fire grass 变黑停留可能显得过短或拖沓。需要 PlayMode 目视确认。
- P1: Mow cut 和 fire burn 共享地下生长 helper 后，任一路径的时序如果被中途 reset/fail/exit 打断，可能出现 leaf 位置残留。当前 `KillActiveSequence()`、`ResetLeafVisual(true, 1f)` 和 `CompleteRegrow()` 会恢复初始坐标，但仍需 PlayMode 验证。
- OBS: 本次未修改 prefab 序列化值，新增字段使用脚本默认 `0.45f`；若策划需要逐棵草差异，需要后续在 `Level_074.prefab` 或 `Grass.prefab` 中显式调参。

### 已规避风险

- 不修改 `RootState`、`IsDead`、`CheckDoorOpen()`、`Level74Ctrl` 或 shared systems，避免 leaf 视觉变化误影响通关条件。
- 不新增 runtime 实例、不接入 save/config/resource/analytics/platform SDK，避免扩散到非 Level74 系统。
- 火效渐隐只在正常 burn sequence 中执行；`ClearFireEffect()`、`OnDisable()`、`OnDestroy()` 等清理路径仍立即 kill tween 并销毁火效，避免 reset/fail/exit 残留。
- 新增序列化字段带中文注释说明用途、边界和注意事项；Fire grass 黑色停留和火苗渐隐仅影响 leaf/fire 视觉节奏。

### 验证证据

- 静态复核：`Level74GrassCtrl` 的 Mow cut regrow 和 fire burn regrow 都调用 `BeginRegrow()` 与 `AppendLeafRegrowTween(...)`；helper 只操作 `leave` 的 localPosition、scale、alpha。Fire burn 会在 `fireDuration` 后先调用 `AppendFireEffectFade(...)` 处理火效渐隐，再进入 leaf 变黑、`fireScorchedHoldDuration` 等待和 `AppendLeafScorchedAlpha(...)` 渐隐。
- 静态复核：`AppendFireEffectFade(...)` 会停止子级 `ParticleSystem` 发射，淡出子级 `SpriteRenderer`，没有 SpriteRenderer 时按 `fireEffectFadeDuration` 等待后销毁；`ClearFireEffect()` 仍走 `DestroyFireEffectInstance(...)` 立即清理。
- 编译：已执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`，结果 0 error / 100 warnings。warnings 为当前工程既有类型，未发现本次 regrow / black hold / fire effect fade 改动引入编译错误。

### 未验证缺口

- Unity Editor PlayMode 未执行：Mow cut 后草从地下长出、fire burn 火效渐隐、fire burn 变黑停留、fire burn 后草从地下长出、restart/fail/exit/completion 中断恢复、Hoe root 挖除和 Door completion 仍需实测。
- Android/iOS landscape touch 未执行：快速工具点击、fire 拖过多棵草、移动端帧稳定性仍需真机或模拟器 smoke。
- Inspector 视觉确认未执行：`regrowStartOffsetY = 0.45f` 是否适合五棵草的地面线、`fireEffectFadeDuration = 0.25f` 和 `fireScorchedHoldDuration = 0.5f` 是否符合节奏仍需人工确认。

### 回滚路径

- 恢复 `Level74GrassCtrl.cs` 中 `regrowStartOffsetY`、`fireEffectFadeDuration`、`fireScorchedHoldDuration`、`PrepareLeafRegrowVisual()`、`AppendLeafRegrowTween(...)`、`AppendFireEffectFade(...)`、`StopFireEffectParticles(...)`、`CompleteFireEffectFade(...)`、`DestroyFireEffectInstance(...)`、`AppendLeafScorchedAlpha(...)` 相关改动，并把 Mow cut/fire burn regrow 恢复为原本只恢复 scale/alpha 或固定 Y 的逻辑。
- 文档记录不影响 runtime，可随代码回滚同步恢复或作为历史证据保留。

## 2026-06-23 Level74FireCtrl 点火稳定性风险评估补充

### 原始行为基线

- `Level74FireCtrl` 是 Level74 本地火源控制器，负责从 `DragEventsListener` 接收 drag/rub 输入，累计有效 flash 后触发 active fire。
- Level74 active fire 接触草叶后仍由 `Level74Ctrl.OnGrassEnter(...)` 和 `Level74GrassCtrl.TryBurn(...)` 处理；通关目标仍是 Hoe 永久移除全部 root 后打开 Door。
- 本次修改前，`IsActiveFire` 直接由 `count >= maxFlashCount` 推导，`fire` 缺失时也可能被计数状态误认为 active；`Update()` 对 `flash` 无空值保护；禁用/销毁路径没有统一停止摩擦循环音效。

### 本次影响文件

- `Assets/Game/Scripts/Level/LevelItem/Level_74/Level74FireCtrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_74/Level74功能说明.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_74/Level74风险评估.md`
- `DevelopMD/已有功能设计与使用记录.md`

### 风险项

- P1: re-arm 判断收紧后，玩家需要明显回拉到 `minRearmDistance` 内或重新 BeginDrag 才能再次计数；若当前 `distance = 1`、`minRearmDistance = 1` 与实际触摸手感不匹配，可能感觉点火节奏变慢。需要 PlayMode 和移动端触摸验证。
- P1: 摩擦音效改为循环音并由粒子/生命周期清理控制，若 SoundsGood 的 stop 行为和预期不同，可能出现音效提前停或残留。需要 PlayMode 听感验证。
- P1: `Application.isPlaying` 保护避免编辑器非运行态触发 runtime 链路，但如果策划曾依赖 Inspector 手动调用 `PlayFire()` 预览，非运行态不会通过生命周期自动订阅 drag；该预览行为当前未作为需求证据确认。
- OBS: `Awake()` 自动解析 `DragEventsListener` 只作为序列化绑定缺失兜底；当前 `Level_074.prefab` 已静态显示 `dragListener` 有绑定，仍需 Inspector 目视确认最终对象符合预期。

### 已规避风险

- `IsActiveFire` 改为只在 `fire` 视觉成功启用后为 true，避免 `fire` 缺失时误启用后续 grass burn contact。
- `RequiredFlashCount` 将有效点火次数限制为至少 1，避免错误配置让火源跳过拖拽直接 active。
- `OnEnable()` / `OnDisable()` / `OnDestroy()` / `Update()` 使用 `Application.isPlaying` 外层保护，避免非运行态进入音效、粒子、事件订阅和 tween 清理链。
- `isSubscribed` 防止重复订阅；禁用、销毁和 reset 都会停止 `flash` 粒子与摩擦循环音效，降低失败/退出/重开后残留风险。
- `fire.transform.DOKill()` 后再播放 active fire 缩放，且 active tween 使用 `SetLink(gameObject)`，降低重复触发或对象销毁后的延迟 tween 风险。
- 本次未修改 `Level74Ctrl`、`Level74GrassCtrl`、prefab、save/config/resource/analytics/platform SDK 或共享脚本，影响范围保持在 Level74 火源输入链。

### 验证证据

- 静态源码复核：`Level74FireCtrl.cs` 新增 `isActiveFire`、`RequiredFlashCount`、`BindDragEvents()`、`UnbindDragEvents()`、`StopFlashFeedback(...)` 和 `StopFlashSound()`；外部 `IsActiveFire` 不再直接读取 `count`。
- 静态 prefab 证据：`Assets/Game/Prefabs/Level/Level_074.prefab` 中 `Level74FireCtrl` 已序列化 `dragListener`、`distance: 1`、`minRearmDistance: 1`、`maxFlashCount: 3` 和 `OnFireCallback -> Level74Ctrl.OnFireCallback`。
- 静态禁用调用扫描：`Level74FireCtrl.cs` 未命中 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、RemoteConfig、Resources、YooAsset、PlayerPrefs、Analytics 或 Instantiate。
- 编译：已执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`，结果 0 error / 96 warnings；warnings 属于当前工程既有范围，未发现本次 `Level74FireCtrl` 改动引入编译错误。

### 未验证缺口

- Unity Editor PlayMode 未执行：三次有效拖拽点火、短距离不计数、回拉 re-arm、`fire` active collider 启用、reset/fail/success/exit 清理和声音停止仍需实测。
- Android/iOS 横屏触摸未执行：单指/多指、快速摩擦、快速重开、快速拖过多棵草和帧稳定性仍需真机或模拟器 smoke。
- Inspector 视觉确认未执行：`flash`、`fire`、`dragListener`、`OnFireCallback` 和 active fire collider 最终绑定仍需在 Unity 内目视确认。

### 回滚路径

- 恢复 `Level74FireCtrl.cs` 中 `IsActiveFire` 直接由 count 推导、原始 OnEnable/OnDisable 订阅、原始 flash 播放和原始 re-arm 判断逻辑。
- 本次未修改 prefab 或共享系统；如运行时手感不符合预期，可只回滚 `Level74FireCtrl.cs`，文档记录可同步恢复或保留为历史证据。

## 2026-06-23 Grass 烧焦变黑过程风险评估补充

### 原有行为基线

- Fire burn 原有 leaf 颜色链路为：火效持续和火效淡出后，`EnterScorchedFade()` 直接调用 `SetLeafScorched(1f)`，把 leaf 颜色瞬时设为黑色。
- Level74 完成目标仍是 Hoe 永久移除全部 root 后由 `Level74Ctrl.CheckDoorOpen()` 打开 Door；Fire burn 是 leaf 临时视觉路径，不应改变 root 状态或开门条件。
- `GameResetEvent`、`SwitchGameStateEvent.SUCCESS/FAILED`、`OnDisable`、`OnDestroy` 已有 cleanup 路径会 kill grass tween 并清理 fire effect。

### 本次影响文件

- `Assets/Game/Scripts/Level/LevelItem/Level_74/Level74GrassCtrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_74/Level74功能说明.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_74/Level74风险评估.md`
- `DevelopMD/已有功能设计与使用记录.md`

### 风险项

- P1: `fireScorchedDuration` 默认 `0.25f` 若与当前火效节奏不匹配，可能让 leaf 变黑显得过慢或过快。需要 Unity PlayMode 目视调参。
- P1: Fire burn 时序新增一个颜色 tween 段，会把后续黑色停留、渐隐和 regrow 整体顺延 `fireScorchedDuration`，需要确认关卡节奏仍符合策划预期。
- P1: 多个 `SpriteRenderer` 同时变色时，如果初始 leaf 颜色差异明显，统一 tween 到黑色可能产生局部观感差异。当前代码使用已有 `AppendLeafScorchedAlpha(...)` 统一处理，仍需实机目视验证。
- OBS: 本次未修改 prefab 序列化值，新字段使用脚本默认 `0.25f`；如果需要逐棵草差异，需要后续在 `Grass.prefab` 或 `Level_074.prefab` 中显式调参。

### 已规避风险

- 不修改 `RootState`、`IsDead`、`CheckDoorOpen()`、`Level74Ctrl` 或共享系统，避免 leaf 视觉变化误影响通关条件。
- 不新增 save/config/resource loading/analytics/platform SDK 链路，不新增运行时实例化，影响范围保持在 Level74 本地 leaf 颜色 tween。
- 清理路径仍由 `KillActiveSequence()`、`ClearFireEffect()`、`ResetLeafVisual(true, 1f)` 和已有 disable/destroy cleanup 覆盖，reset/fail/exit 不等待变黑过程完成。
- 新增序列化字段带中文注释，说明用途、调用边界和注意事项。

### 验证证据

- 静态源码复核：`BuildBurnSequence(...)` 现在在 `EnterScorchedFade` 后追加 `AppendLeafScorchedAlpha(activeSequence, 1f, fireScorchedDuration)`，再进入 `fireScorchedHoldDuration` 停留和 `fireFadeDuration` 渐隐。
- 静态源码复核：`EnterScorchedFade()` 不再调用 `SetLeafScorched(1f)`，因此 leaf 变黑不再瞬时完成。
- 静态禁用调用扫描：`Level74GrassCtrl.cs` 未命中 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、RemoteConfig、Resources、YooAsset、PlayerPrefs、Analytics、IAP 或 Advertisement。
- 编译：已执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`，结果 0 error / 1114 warnings；warnings 属于当前工程既有范围。
- 格式检查：`git diff --check -- Assets\Game\Scripts\Level\LevelItem\Level_74\Level74GrassCtrl.cs` 已通过，仅有 Git 工作区 LF/CRLF 转换提示。

### 未验证缺口

- Unity Editor PlayMode 未执行：Fire grass 烧焦变黑过程、黑色停留、黑色渐隐、地下生长、restart/fail/exit/completion 中断恢复仍需实测。
- Android/iOS 横屏触摸未执行：快速拖火经过多棵草、快速重开和帧稳定性仍需真机或模拟器 smoke。
- Inspector 视觉确认未执行：`fireScorchedDuration = 0.25f` 是否符合五棵草最终视觉节奏仍需人工确认。

### 回滚路径

- 恢复 `Level74GrassCtrl.cs` 中 `fireScorchedDuration` 字段、`BuildBurnSequence(...)` 新增变黑 tween 段，以及 `EnterScorchedFade()` 的瞬时 `SetLeafScorched(1f)` 调用。
- 文档记录不影响 runtime，可随脚本回滚同步恢复或作为历史证据保留。

## 2026-06-24 Tool 切换 SortingOrder 风险评估补充

### 原有行为基线

- `Level74Ctrl` 原有 Tool 切换只处理 parent、localPosition、localRotation、localScale、active 和 Actor tool state。
- `AppendEquipTool(...)` 把 Mow/Hoe 挂到 `handBone`，但没有保证手持工具渲染在目标层级。
- `AppendReturnTool(...)` 和 `RestoreTool(...)` 没有保证卸下工具回到场景后排序一致。
- `Level_074.prefab` 静态文本显示 `Sickle` / `Eradicator` 初始 `m_SortingOrder` 为 `1`，`Hoe` 为 `300`，不同工具初始排序不一致。

### 本次影响文件

- `Assets/Game/Scripts/Level/LevelItem/Level_74/Level74Ctrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_74/Level74功能说明.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_74/Level74风险评估.md`
- `DevelopMD/已有功能设计与使用记录.md`

### 风险项

- P1: 工具子级如果存在多个 `SpriteRenderer` 或 `SortingGroup`，统一设置为同一个 order 可能覆盖局部美术层级差异。当前 prefab 静态证据中 Sickle/Hoe/Eradicator 主要命中根节点 `SpriteRenderer`，但仍需 Unity Inspector/PlayMode 目视确认最终层级。
- P1: 手持状态设置为 `300` 后可能遮挡 actor 手部或其他前景美术；这是需求指定行为，但仍需 PlayMode 验证观感。
- P1: 卸下状态统一为 `150` 后可能改变 Sickle/Eradicator 原本 prefab `1` 的场景层级。该变化符合本次“卸下为 150”的需求，但需确认不会被草、门或 Farmer 错误遮挡。
- OBS: Eradicator 现有逻辑选择后隐藏场景对象，不进入 `handBone` 手持显示；本次仅在隐藏前设置卸下排序 `150`，未改变 Eradicator 显隐设计。

### 已规避风险

- 排序 helper 只作用于传入的当前 tool Transform 及其子级，不遍历整关卡对象，避免影响 Grass、Door、Actor、Farmer、Fire 或 UI。
- 本次没有修改 Tool 选择条件、Actor tool state、Grass cut/dig/burn、Door completion、GameReset/SUCCESS/FAILED 事件流。
- 本次不新增 save/config/resource loading/analytics/platform SDK、广告/IAP、PlayerPrefs 或 RemoteConfig 链路。
- 重置路径 `RestoreTool(...)` 会把工具排序恢复为卸下值 `150`，降低 restart/fail/exit 后手持排序残留风险。

### 验证证据

- 静态源码复核：`Level74Ctrl` 中 `AppendEquipTool(...)` 对 Mow/Hoe 设置 `ToolEquippedSortingOrder = 300`；`AppendReturnTool(...)`、`RestoreTool(...)` 和 Eradicator 隐藏路径设置 `ToolUnequippedSortingOrder = 150`。
- 静态源码复核：`SetToolSortingOrder(...)` 只写当前 tool 子级 `SpriteRenderer.sortingOrder` 和 `SortingGroup.sortingOrder`。
- 静态 prefab 证据：`Assets/Game/Prefabs/Level/Level_074.prefab` 中 `Sickle` / `Eradicator` 根节点 `m_SortingOrder` 为 `1`，`Hoe` 根节点 `m_SortingOrder` 为 `300`；本次改为运行时统一管理。
- 格式检查：`git diff --check -- Assets\Game\Scripts\Level\LevelItem\Level_74\Level74Ctrl.cs` 已通过，仅提示 Git 工作区 LF/CRLF 转换。
- 静态禁用调用扫描：`Level74Ctrl.cs` 未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、RemoteConfig、Resources、YooAsset、PlayerPrefs、Analytics、IAP 或 Advertisement；扫描命中来自旧注释和已有 `OnGameReset` 方法名。
- 编译：已执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`，结果 0 error / 1114 warnings；warnings 属于当前工程既有范围。

### 未验证缺口

- Unity Editor PlayMode 未执行：Sickle/Hoe 快速切换、Farmer 释放 Hoe 后自动手持、旧工具回场景、重开/失败/成功/退出后的排序恢复仍需实测。
- Android/iOS 横屏触摸未执行：快速点击工具、点击 Farmer、移动中切换工具时层级稳定性仍需真机或模拟器 smoke。
- Unity Inspector 视觉确认未执行：手持 `300` 和卸下 `150` 与当前 actor、Grass、Door、Farmer、Fire 的排序关系仍需目视确认。

### 回滚路径

- 恢复 `Level74Ctrl.cs` 中本次新增的 `UnityEngine.Rendering` using、两个排序常量、`SetToolSortingOrder(...)` 以及三个调用点，即可回到原有 parent/transform-only 切换行为。
- 本次未修改 prefab 或共享系统；文档记录可随代码回滚同步恢复或保留为历史证据。

## 2026-07-13 Grass 索引 2 重生随机显示星星风险评估

### 原有行为基线

- `Level74GrassCtrl.BeginRegrow()` 原先只负责草叶状态和视觉恢复，Mow cut 与 Fire burn 共用该入口。
- `Level74Ctrl.grasses` 的第三项即零基索引 `2` 是本次指定目标；修改前没有 regrow 到 star 的回调或概率判定。
- 任务开始前工作区已存在 `Level74Ctrl.star` 字段、禁用的 `Postcard_Star (1)` prefab 实例及同 prefab 的其它未提交改动；本次必须避免覆盖这些既有改动。

### 本次影响文件与系统

- `Assets/Game/Scripts/Level/LevelItem/Level_74/Level74Ctrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_74/Level74GrassCtrl.cs`
- `Assets/Game/Prefabs/Level/Level_074.prefab`
- `Assets/Game/Scripts/Level/LevelItem/Level_74/Level74功能说明.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_74/Level74风险评估.md`
- `DevelopMD/已有功能设计与使用记录.md`
- 只影响 Level74 本地 Grass regrow 回调、随机判定和 star 显隐；不触碰共享 gameplay panel、全局事件、存档、配置、资源加载或平台 SDK。

### 风险项

- P1：`Random.value` 为非确定性结果，少量手工尝试可能连续不出现星星；必须把“未命中”与绑定/调用故障区分，当前配置概率的长期分布尚未做统计验证。
- P1：Mow cut 与 Fire burn 都调用 `BeginRegrow()`，因此两种重生都会按 `generateProbability` 重新抽取；若策划只期望某一种工具路径，需要后续明确并收窄入口。
- P1：逻辑只执行 `star.SetActive(true)`，不负责重开时关闭或星星收集后的生命周期；当前是否应在 `GameResetEvent` 中恢复 inactive 缺少需求证据，标记为待 PlayMode 验证。
- P1：目标身份依赖 `Level_074.prefab` 中 `grasses` 列表顺序；如果后续 Inspector 重排列表，索引 `2` 会指向不同草。
- OBS：列表不足、目标实例不匹配或 star 缺失时采用静默返回，能避免异常但会表现为永不出星，需要 Inspector 绑定检查定位。

### 已规避风险

- 判定仅比较 `grasses[2]` 与回调实例，不遍历场景或新增全局事件，避免扩散到其它关卡和共享脚本。
- `BeginRegrow()` 在 `IsDead`、`leave == null` 防护之后才通知控制器，未真正进入重生的草不会抽取概率。
- `OnGrassRegrown(...)` 对列表长度、目标引用和 star 引用做空值/边界保护；随机命中后只调用本地 GameObject 显隐。
- 未修改 root、`IsDead`、`CheckDoorOpen()`、工具选择、草重生 tween 时长、存档、配置、资源、打点、广告、IAP 或 Android/iOS 平台分支。

### 验证证据

- CodeGraph：`BeginRegrow()` 有两个调用点，分别位于 `TryCut(...)` 和 `BuildBurnSequence(...)`；新增回调位于其原有死亡/空引用防护之后。
- 静态源码：`OnGrassRegrown(...)` 仅在 `isTargetGrass && star != null` 时执行 `Random.value < generateProbability`，命中后 `SetActive(true)`；当前源码默认概率为 `0.5f`。
- 静态 prefab：控制器 `grasses` 第 3 项为 fileID `684921366059065066`；新增 `star` 绑定指向初始禁用的 `Postcard_Star (1)` fileID `2760197287872847478`。
- 编译：`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` 通过，0 error / 1116 warning；warning 属于当前工程既有范围。
- 静态禁用链路扫描：本次新增代码未引入 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、RemoteConfig、PlayerPrefs、Analytics、Advertisement 或 IAP。
- 格式检查：目标文件 `git diff --check` 最终通过，仅保留 Git 的 LF/CRLF 转换提示。

### 未验证缺口

- Unity Editor PlayMode 未执行：索引 2 的 Mow/Fire 重生触发、其它四棵草不触发、连续重生重新抽取、star 显示/收集、失败/重开/退出后的 inactive 状态仍需实测。
- 未使用固定随机种子或统计测试验证长期出现比例接近当前 `generateProbability` 配置；当前只静态确认阈值实现为 `Random.value < generateProbability`。
- Android/iOS 横屏真机未执行；虽然只有一次常数时间随机判定和显隐，仍未验证触摸流程与星星表现。
- 任务开始前 `Level_074.prefab` 已有其它未提交修改；本次只确认新增字段绑定，没有替这些既有改动提供回归结论。

### 回滚路径

- 删除 `Level74GrassCtrl.BeginRegrow()` 中的回调、`Level74Ctrl.OnGrassRegrown(...)` 与 `star` 字段，并删除 `Level_074.prefab` 控制器上的 `star` 序列化绑定。
- 不回滚任务开始前已存在的 `Postcard_Star (1)` 实例、JumpBezier 组件、位置/旋转和其它 prefab 调整；如需回滚这些内容，应由其原任务单独处理。

## 2026-07-13 OnGrassRegrown 执行日志风险补充

- 入口日志位于目标草与 star 过滤之前，可确认所有 Grass regrow 回调是否进入；输出只包含本地对象名、布尔绑定状态和概率值，不包含存档或用户数据。
- Bear.Logger 的 `this.Log(...)` 带 `[Conditional("DEBUG_MODE")]`，未开启 `DEBUG_MODE` 时调用会被编译移除；Console 没有日志不能单独证明回调未执行。
- 每次正常 regrow 最多新增一条日志，可能在连续割草/火烧调试时增加 Console 噪声；正式关闭 DEBUG_MODE 后没有运行时日志开销。
- 编译已通过（0 error / 98 warning），`git diff --check` 结果见最终检查；Unity PlayMode 实际日志仍待用户验证。
- 回滚只需移除 `this.Log(...)` 及为日志复用的局部布尔变量，并恢复等价 guard，不影响随机显示星星功能。

## 2026-07-13 Star regrow 仅生成一次风险补充

- 原有风险：目标 Grass 每次 regrow 都会重新抽取，star 收集或隐藏后存在二次、三次生成的可能。
- 当前控制：首次概率命中时先写入 `hasGeneratedStar = true`，再执行 `star.SetActive(true)`；后续回调在生成状态为 true 时直接返回，避免激活回调重入或后续 regrow 重复生成。
- 生命周期边界：该标记不进入 `ResetRuntimeState()`，所以同一 `Level74Ctrl` 实例内重开仍保持“已经生成”；它不是存档字段，控制器销毁并重建后会恢复为未生成。
- P1 未验证：需要 PlayMode 确认 Level74 重开是否复用同一控制器实例，以及 star 收集组件是否仅隐藏对象而不会替换控制器；若产品期望跨控制器/跨进程永久只出现一次，则当前本地布尔状态不足，需要明确存档需求。
- 日志会输出 `hasGeneratedStar`，但仍受 `DEBUG_MODE` 控制；未开启时不能依赖 Console 验证。
- 编译已通过（0 error / 1116 warning）；`git diff --check` 结果见最终检查，Unity PlayMode 与 Android/iOS 真机仍待补验。
- 回滚只需删除 `hasGeneratedStar` 字段、guard、命中赋值和日志字段；不会影响原有概率、Grass regrow 或 star prefab 绑定。
