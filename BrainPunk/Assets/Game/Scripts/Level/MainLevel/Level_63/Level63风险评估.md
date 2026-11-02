# Level63 风险评估

## 2026-07-22：投影 Pixel Glitch 真机长时静止修复风险更新

### 原有行为基线

- `Level63ProjectorPixelGlitch.shader` 使用 `floor(_Time.y * _NoiseSpeed)` 作为离散动画时间，并直接把持续增长的值传入 `Hash21`。
- `PlayCtrl.CreateLevelAsync(...)` 在现有玩法场景中实例化关卡 prefab；逐关切换不会因创建 Level63 prefab 而重新加载场景或重置 shader 全局时间。
- 功能加入前，`Sp_l63_img_bg_02`、`Sp_l63_img_bg_01`、`Sp_l63_img_bg_03` 均使用 URP `Sprite-Unlit-Default` 材质。当前修复前 prefab 中幕布和文字被保存为专属 glitch 材质，导致 `Level63Ctrl.Awake()` 把 glitch 材质误缓存为这两个对象的原材质。
- Unity 导入日志持续报告 `Universal Render Pipeline/2D/Sprite-Unlit` fallback 不存在；当前 URP 包实际 shader 名称为 `Universal Render Pipeline/2D/Sprite-Unlit-Default`。

### 影响文件 / 资源

- `Assets/Game/ArtSrc/Shader/Sprites/Level63ProjectorPixelGlitch.shader`
- `Assets/Game/Prefabs/Level/Level_063.prefab`
- `Assets/Game/Scripts/Level/LevelItem/Level_63/Level63功能说明.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_63/Level63风险评估.md`
- `DevelopMD/已有功能设计与使用记录.md`

### 修复后行为

- 离散时间 tick 在进入 `Hash21` 前通过 `fmod(..., 256.0)` 限制为 `0..255`，避免玩法场景长时运行后哈希乘法丢失小数精度。
- glitch 仍按现有 `_NoiseSpeed` 离散更新；256 个时间状态循环只影响随机序列周期，不改变三个目标、材质参数、投影开关或 reveal/close 时序。
- fallback 指向当前 URP 包存在的 `Sprite-Unlit-Default`。
- 幕布和文字 prefab 默认材质恢复为修复前历史基线中的 `Sprite-Unlit-Default`；运行时开投影临时换成 glitch，关闭和生命周期清理恢复默认材质。

### 风险项

- 256 个离散时间状态会按 `_NoiseSpeed` 周期重复；当前材质速度为 7，理论随机序列约 36.6 秒循环。该重复周期不改变功能开关，但最终观感仍需美术或真机目视确认。
- fallback 修正只能保证主 SubShader 不兼容时能找到现有 URP 兜底，不替代 Android OpenGLES3 与 iOS Metal 的主 shader 真机验证。
- prefab 材质恢复依赖历史版本和当前 URP 包 GUID 证据；若后续替换 URP 默认材质，需要同步核对三个 `glitchTargets` 的缓存与恢复基线。
- 本次未调整 `_ChannelOffset`、`_ShiftStrength` 等现有材质参数，真机错位强度与文字可读性保持原配置风险。

### 验证证据

- Git 历史静态核对确认 glitch 功能加入前三个目标的 `m_Materials` 均为 GUID `9dfc825aed78fcd4ba02077103263b40`，对应当前 URP 包 `Sprite-Unlit-Default.mat`。
- 静态核对确认 `Level63Ctrl` 仍只在投影打开时把三个目标切换到专属 glitch 材质，并在关闭或生命周期清理时恢复 Awake 缓存的原材质。
- 数值边界核对确认传入 `Hash21` 的时间 tick 现在最大为 255，不再随同一玩法场景的累计时间无限增长。
- 按 32 位浮点逐步舍入进行数值模拟：旧表达式在采样的 30、45、60 分钟时间点已出现时间哈希输入退化为 `0`；新表达式在 60 分钟、24 小时和 7 天采样点仍能从当前 tick 递增到下一 tick。
- 静态核对确认 fallback 名称与当前 URP 包内 `Sprite-Unlit-Default.shader` 声明一致。
- 已执行 `dotnet build Assembly-CSharp.csproj --no-restore`，结果为 0 个错误、1117 个项目既有 warning。

### 未验证缺口

- 尚未在 Unity Editor PlayMode 中完成冷启动、模拟长时 `_Time`、连续开关、重开、退出和材质恢复目视验证。
- 尚未在同一玩法场景累计运行至少 45 分钟后验证 glitch 仍持续变化。
- 尚未完成 Android OpenGLES3 与 iOS Metal 横屏真机验证，具体 GPU/驱动差异仍属未验证风险。
- 尚未验证 36.6 秒随机序列循环是否可被玩家明显感知，以及当前文字可读性是否满足美术要求。

### 回滚路径

- 回退 shader 的 bounded time tick 与 fallback 名称修改，可恢复原时间哈希和原警告状态。
- 把 `Sp_l63_img_bg_01`、`Sp_l63_img_bg_03` 的 prefab 材质重新设为专属 glitch 材质，可恢复修复前 prefab 状态；该状态会再次破坏原材质缓存语义，不建议作为长期方案。
- 本次不涉及存档、配置、事件、分析、资源地址或平台 SDK，无需玩家数据迁移。

## 2026-06-10: SimpleAudioPlay local audio helper risk update

### Original behavior baseline

- Existing Level63 audio playback was hard-coded in `Level63Ctrl.ClickProjector(Transform, Vector2)` through `AudioManager.PlaySound("level63projector")`.
- Existing project audio SFX entry point is `Game.Scripts.Common.AudioManager.PlaySound(string tag, ..., bool loop = false)`.

### Affected files/resources

- `Assets/Game/Scripts/Level/LevelItem/Level_63/SimpleAudioPlay.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_63/SimpleAudioPlay.cs.meta`
- `Assets/Game/Scripts/Level/LevelItem/Level_63/Level63功能说明.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_63/Level63风险评估.md`
- `DevelopMD/已有功能设计与使用记录.md`

### Existing systems used

- Existing `Game.Scripts.Common.AudioManager.PlaySound(...)` path for SoundsGood SFX playback.
- Existing SoundsGood `Sound` instance returned by `AudioManager.PlaySound(...)` for stoppable continuous loops.
- Unity runtime `Start`, `OnDisable`, `OnDestroy`, and coroutine lifecycle.
- Serialized `stopSoundOnDisable` local switch for disable-time audio cleanup.

### Risk items

- Invalid or empty `clipName` would cause unwanted audio lookup or warning noise; mitigation: `SimpleAudioPlay.PlayAudio()` returns early for empty `clipName`.
- Interval loops can create repeated one-shot playback and may overlap if `loopInterval` is shorter than the actual clip duration; mitigation: behavior is explicit through the serialized interval and remains local to the component.
- Continuous loop sounds could survive object disable/destroy if not stopped; mitigation: `stopSoundOnDisable` defaults true so `OnDisable` calls `StopAudio()`, and `OnDestroy` always calls `StopAudio()`.
- When `stopSoundOnDisable=false`, the current one-shot or continuous loop can continue after disable by design; mitigation: the interval coroutine is still stopped on disable so a disabled component does not keep scheduling repeated one-shot playback.
- Shared audio behavior could change if `AudioManager` were modified; mitigation: this implementation only adds a Level63-local caller and does not modify `AudioManager` or shared sound settings.

### Verification evidence

- Static code review confirmed `SimpleAudioPlay` has no `OnValidate`, `Reset`, `[ExecuteAlways]`, `[ExecuteInEditMode]`, editor GUI, DB, save, resource loading, or runtime singleton initialization path beyond the existing `AudioManager.PlaySound(...)` call.
- Static code review confirmed `Start()` only plays when `playOnStart` is true.
- Static code review confirmed `SimpleAudioPlay` stores the latest returned `Sound`, including one-shot playback.
- Static code review confirmed `StopAudio()` stops both the interval coroutine and the owned current `Sound`.
- Static code review confirmed `OnDisable` only stops current sound when `stopSoundOnDisable` is true, while always stopping the local interval coroutine when the switch is false.
- C# compile check passed again in `BrainPunk/` after adding `stopSoundOnDisable`: `dotnet build Assembly-CSharp.csproj --no-restore`, 0 errors / 89 existing warnings.
- C# compile check passed in `BrainPunk/` with `dotnet build Assembly-CSharp.csproj --no-restore`: 0 errors / 89 existing warnings.

### Unverified gaps

- Unity Editor PlayMode still needs to verify the intended Level63 prefab object can bind `SimpleAudioPlay`, configure the target `clipName`, and hear playback through the current SFX settings.
- Unity Editor PlayMode still needs to verify interval loop cadence, continuous loop stop behavior, disable/destroy cleanup, and restart behavior.
- Android/iOS landscape smoke still needs to verify repeated interval playback does not introduce audible overlap or performance spikes for the configured clip.

### Rollback path

- Remove `SimpleAudioPlay.cs` and its Unity `.meta` file.
- Remove the SimpleAudioPlay sections from `Level63功能说明.md`, `Level63风险评估.md`, and `DevelopMD/已有功能设计与使用记录.md`.
- No save/config/resource/platform migration is required because the helper only calls the existing audio runtime API.

## 2026-06-10: version 2.0.0 post-unlock Use button shutdown risk update

### Original behavior baseline

- Before v2.0.0, `Level63Ctrl` preserved the existing `SwitchObjActiveEvent` show/hide flow and `PasswordPopup_63_2` Use button entry.
- The script had an `OnTiggerItem(OnTiggerItemEvent)` method from the new comment, but it was not subscribed in `Awake()` and did not enforce a persistent non-interactable state after id 1.

### Affected files/resources

- `Assets/Game/Scripts/Level/LevelItem/Level_63/Level63Ctrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_63/Level63功能说明.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_63/Level63风险评估.md`
- `DevelopMD/已有功能设计与使用记录.md`
- `specs/005-level63-projector-presentation/`

### Existing systems used

- Existing `OnTiggerItemEvent` dispatch; Level63 only subscribes and reacts to `EventId == 1`.
- Existing `CustomButton.Interactable` to disable click/raycast/collider interaction.
- Existing `SwitchObjActiveEvent` and `GamePanel_UseBtnCommonFeature.Use_btn` runtime button resolution.
- Existing DOTween short scale feedback for Use button hiding.

### Risk items

- If id 1 is dispatched earlier than expected, the Use button will hide and stay locked for the current active run; mitigation: only `EventId == 1` triggers the lock, and `GameResetEvent` / re-entry clears the runtime flag.
- Later `SwitchObjActiveEvent` show requests could otherwise re-show the button; mitigation: `_useButtonLocked` forces hide and suppresses registration/show while locked.
- Hidden but still-interactable button could reopen `PasswordPopup_63_2`; mitigation: id 1 path unsubscribes click, sets `Interactable=false`, and hides/deactivates the button.
- Shared systems could be affected if the lock were implemented globally; mitigation: change is local to `Level63Ctrl` and does not modify shared Use button scripts, save/unlock, analytics, config, or non-Level63 levels.

### Verification evidence

- Static code review confirmed `Level63Ctrl.Awake()` subscribes to `OnTiggerItemEvent`.
- Static code review confirmed `OnTiggerItem` ignores null events and non-id 1 events.
- Static code review confirmed id 1 sets `_useButtonLocked=true`, unsubscribes Use button click, sets `Use_btn.Interactable=false`, kills active button tweens, scales the button out, and deactivates it when active.
- Static code review confirmed `OnSwitchObj` and `RegisterUseButton` keep the Use button hidden/non-interactable while locked.
- Static code review confirmed `GameResetEvent`, `OnDisable`, and `OnDestroy` clear the runtime lock and clean up subscriptions/tweens.
- C# compile check passed in `BrainPunk/` with `dotnet build Assembly-CSharp.csproj --no-restore`: 0 errors / 1108 existing warnings.

### Unverified gaps

- Unity Editor PlayMode still needs to verify the actual Level63 unlock/completion path dispatches `OnTiggerItemEvent` id 1 at the intended time.
- Unity Editor PlayMode still needs to verify id 1 hides the Use button, prevents `PasswordPopup_63_2` reopening, suppresses later show requests, and resets correctly on restart/re-entry.
- Android/iOS landscape smoke still needs to verify touch behavior after id 1 and no visible input stall or stuck hidden button.

### Rollback path

- Revert the v2.0.0 event subscription, `_useButtonLocked` state, and `HideUseButton()` changes in `Level63Ctrl.cs`.
- Remove the v2.0.0 sections from `Level63功能说明.md`, `Level63风险评估.md`, `DevelopMD/已有功能设计与使用记录.md`, and `specs/005-level63-projector-presentation/` if the feature is rolled back.
- No save/config/player-data migration is required because this change is runtime-only.

## 2026-06-10: Projector size reveal and reverse close risk update

### Original behavior baseline

- Before this change, projector reveal showed light, then expanded curtain width from 0 to 10 while preserving height, then showed projected text.
- The previous close update faded visible projection sprites by alpha before hiding them.
- Lifecycle cleanup paths (`GameResetEvent`, `OnDisable`, `OnDestroy`) reset immediately without waiting for visual playback.

### Affected files/resources

- `Assets/Game/Scripts/Level/LevelItem/Level_63/Level63Ctrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_63/Level63功能说明.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_63/Level63风险评估.md`
- `DevelopMD/已有功能设计与使用记录.md`

### Existing systems used

- DOTween `Sequence` and `DOTween.To(...)` for Level63-local `SpriteRenderer.size` changes.
- Existing `_projectorVersion` and sequence kill flow still prevent stale reveal/close callbacks from changing visuals after reset.
- Existing `SpriteRenderer.size` control is extended from width-only reveal to width and height reveal/close.

### Risk items

- Height animation can distort non-sliced sprites if the prefab setup changes; current risk is limited to the existing serialized `projectionCurtain` SpriteRenderer and requires PlayMode visual validation.
- Delayed close can conflict with restart/exit/destroy cleanup; mitigation: only `TurnProjectorOff()` passes `playClose=true`, while `GameResetEvent`, `OnDisable`, `OnDestroy`, and `Awake` keep immediate cleanup.
- Repeated clicks during reverse close could re-enter close/open state; mitigation: `Closing` clicks are ignored.
- Pixel glitch material remains active during reverse close so the closing visuals keep the current projector style; it is restored only after close completion or immediate cleanup.

### Verification evidence

- Static code review confirmed reveal now sets curtain size to `(0.1, 0)`, animates height to full height, then animates width to `fullCurtainWidth`.
- Static code review confirmed player click close now calls `ResetProjectorVisuals(true)` and reverse-closes the curtain size, while reset/disable/destroy call `ResetProjectorVisuals()` without reverse playback.
- Static code review confirmed changes are limited to `projectionCurtain` size and local projector state, with no shared input, save, config, resource loading, platform SDK, or non-Level63 code change.

### Unverified gaps

- Unity Editor PlayMode still needs to verify reveal timing: width stays at 0.1 while height grows 0 to full height, then width grows 0.1 to 10.
- Unity Editor PlayMode still needs to verify On-state click reverse playback: text hides, width contracts 10 to 0.1, height contracts to 0, then visuals fully hide.
- Unity Editor PlayMode still needs to verify mid-reveal close behavior and no delayed curtain/text callbacks after close.
- Android/iOS landscape device or simulator smoke still needs to verify rapid projector taps, restart during close, and 60 fps behavior.

### Rollback path

- Revert `Level63Ctrl.cs` reveal/close size-sequence additions back to the previous width-only reveal and immediate or fade-out close behavior as required.
- Remove this size reveal/reverse close update section from `Level63功能说明.md`, `Level63风险评估.md`, and the matching `DevelopMD/已有功能设计与使用记录.md` entry if the feature is rolled back.

更新时间：2026-06-05

## 原有行为基线

- `Level63Ctrl.cs` 原脚本订阅 `SwitchObjActiveEvent`，按事件显示/隐藏 `Use_btn` 并播放 0.1 秒缩放反馈。
- `Level63Ctrl.cs` 原脚本点击 Use button 时调用 `PasswordPopup.Create("PasswordPopup_63_2")`。
- `Level_063.prefab` 根对象挂有 `Level63Ctrl`，`BaseLevelCtrl.gamePlayPanelName` 当前为 `GamePlayPanel_017`。
- `Level_063.prefab` 根对象的 `Use_btn` 字段当前为 `{fileID: 0}`；Level63Ctrl 运行时需要从 `GamePlayPanel_017` 的 `GamePanel_UseBtnCommonFeature.Use_btn` 获取 Use button。
- `Level_063.prefab` 已有 `Projector`、`Sp_l63_img_bg_02`、`Sp_l63_img_bg_01`、`Sp_l63_img_bg_03`。
- `Projector/Sp_l63_img_syj_01` 上已有 `ClickItemEventHandle`，但 `OnClickEvent` 原先为空。
- 共享 `ClickItemEventHandle` 负责鼠标/触摸检测、2D raycast、暂停态过滤和 UnityEvent 分发，本次没有修改该脚本。

## 影响文件 / 资源

- `Assets/Game/Scripts/Level/LevelItem/Level_63/Level63Ctrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_63/Level63功能说明.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_63/Level63风险评估.md`
- `Assets/Game/Prefabs/Level/Level_063.prefab`
- `Assets/Game/ArtSrc/Shader/Sprites/Level63ProjectorPixelGlitch.shader`
- `Assets/Game/ArtSrc/Materials/Level63ProjectorPixelGlitch.mat`
- `DevelopMD/已有功能设计与使用记录.md`
- `specs/005-level63-projector-presentation/`

## 使用的既有系统

- `ClickItemEventHandle`：保留既有点击/触摸检测，不新增 Level63 本地 raycast。
- `UnityEvent<Transform, Vector2>`：prefab 事件绑定到 `Level63Ctrl.ClickProjector(Transform, Vector2)`。
- DOTween `Sequence` / `DOTween.To`：控制投影 staged reveal 和 sliced curtain 宽度。
- `SpriteRenderer.size`：控制 `Sp_l63_img_bg_01` 宽度 reveal，保留高度。
- `SwitchObjActiveEvent`：保留 Use button show/hide 入口。
- `PlayCtrl.Instance.CurrentGamePlayPanel.GetComponent<GamePanel_UseBtnCommonFeature>().Use_btn`：Level63Ctrl 优先通过当前 `GamePlayPanel_017` 获取 Use button。
- `GameResetEvent`：重开时清理投影状态。
- `PasswordPopup.Create("PasswordPopup_63_2")`：保留 Level63 password popup 入口。

## 已规避风险

- 投影仪行为保留在 `Level63Ctrl` 和 `Level_063.prefab` 内，未修改共享 click handler、全局输入、存档、配置表或 ProjectSettings。
- Level63Ctrl 在 `Start()` / `OnEnable()` 优先从 `CurrentGamePlayPanel` 获取 `GamePlayPanel_017` 上的 `GamePanel_UseBtnCommonFeature.Use_btn`；获取失败时 show/hide、订阅、退订、tween cleanup 都直接跳过，避免原脚本空引用崩溃。
- 使用规则已限定为：Level63 当前复用 `GamePlayPanel_017`，并依赖该 prefab 上的 `GamePanel_UseBtnCommonFeature` 提供 Use button；如后续更换 GamePanel，必须同步验证目标面板具备同名组件或等价 Use button provider。
- 投影视觉引用缺失时只跳过对应 visual 操作，不阻断 Level63 正常生命周期。
- reveal 通过 `_projectorVersion` 版本号和 sequence kill 阻止关闭后旧回调继续显示幕布或文字。
- 幕布 reveal 改 `SpriteRenderer.size.x`，不改 transform scale，降低 sliced sprite 拉伸变形风险。
- Pixel glitch 使用 Level63 专属 shader/material，并只绑定三个 Level63 投影 SpriteRenderer，避免改共享默认 sprite 材质。
- 投影仪机身不在 Off 状态隐藏，避免点击组件所在对象失去 raycast 入口。
- `GameResetEvent`、`OnDisable`、`OnDestroy` 都调用统一 reset，降低重开/退出后视觉残留风险。

## 验证证据

- Checklist gate：`specs/005-level63-projector-presentation/checklists/requirements.md` 为 16/16 complete。
- Prerequisite 脚本已执行，但当前分支 `feature/1.7.0_common` 不符合 Spec Kit numeric branch 规则，脚本在 branch check 处失败；本次按 `.specify/feature.json` 指向的 `specs/005-level63-projector-presentation` 继续。
- 已文本检查 `Level_063.prefab` 根对象 `Level63Ctrl` 字段绑定：projector parent、projection light、projection curtain、projected text、glitch targets、glitch material 均已写入。
- 已文本检查 `Level_063.prefab` 根对象 `BaseLevelCtrl.gamePlayPanelName` 为 `GamePlayPanel_017`。
- 已文本检查 `GamePlayPanel_017.prefab` 挂有 `GamePanel_UseBtnCommonFeature`，且该组件 `Use_btn` 已绑定到面板内 Use button。
- 已文本检查 `ClickItemEventHandle.OnClickEvent` 从空列表改为调用 `Level63Ctrl.ClickProjector`。
- 已执行 C# 编译检查：在 `BrainPunk/` 下运行 `dotnet build Assembly-CSharp.csproj --no-restore`，最后一次结果为 0 个错误、90 个项目既有 warning。

## 未验证 / 剩余风险

- 尚未在 Unity Editor PlayMode 中验证 Level63 进入、重开、失败、退出、通关后的投影视觉清理。
- 尚未在 Unity Editor PlayMode 中验证投影 reveal 的实际显示顺序、时间节奏、幕布高度稳定和文字可读性。
- 尚未在 Unity Editor PlayMode 中验证 `ClickItemEventHandle` 的 prefab UnityEvent 在运行时确实命中 `ClickProjector(Transform, Vector2)`。
- 尚未在 Unity Editor PlayMode 中验证点击投影仪外区域不会改变投影状态。
- 尚未在 Unity Editor PlayMode 中验证 `GamePlayPanel_017` 作为 `CurrentGamePlayPanel` 创建时序下，Level63Ctrl 是否稳定获取到 `GamePanel_UseBtnCommonFeature.Use_btn`。
- 尚未在 Unity Editor PlayMode 中验证 `PasswordPopup_63_2` 打开后与投影状态同时存在时的 UI/视觉协调。
- 尚未在 Unity Editor 或移动设备中验证 Level63 pixel glitch shader 的最终渲染效果、文字可读性和关闭后材质恢复。
- 尚未在 Android / iOS 真机或模拟器上验证横屏触控响应、连续 10 次 toggle、mid-reveal reverse close 和 60 fps 目标。
- 尚未验证至少一个非 Level63 已验收关卡的 shared click handler / Use button / password popup 回归。

## 回滚路径

- 回滚 `Level63Ctrl.cs` 到原始 Use button/password popup 脚本或停用 projector toggle 相关字段和方法。
- 回滚 `Level_063.prefab` 中 `Level63Ctrl` projector/glitch 引用和 `ClickItemEventHandle.OnClickEvent` 绑定。
- 删除或保留未引用的 `Level63ProjectorPixelGlitch.shader` 与 `Level63ProjectorPixelGlitch.mat`，以实际资源引用状态为准。
- 本功能不新增存档字段、配置表、全局输入或 ProjectSettings；回滚不需要迁移玩家数据。

## 后续验证清单

- Compile：`dotnet build Assembly-CSharp.csproj --no-restore` 保持 0 个错误。
- Prefab：确认 `Level_063.prefab` 的 `BaseLevelCtrl.gamePlayPanelName` 保持为 `GamePlayPanel_017`。
- GamePanel：确认 `GamePlayPanel_017.prefab` 保持挂载 `GamePanel_UseBtnCommonFeature`，且 `Use_btn` 引用非空。
- Prefab：打开 `Level_063.prefab`，确认根对象 Level63Ctrl 的 projector/light/curtain/text/glitch 字段完整。
- Prefab：确认 `Projector/Sp_l63_img_syj_01` 保留 `ClickItemEventHandle`，且 OnClickEvent 调用 Level63Ctrl projector toggle API。
- Entry/restart：进入或重开后投影光、幕布、文字隐藏，幕布宽度为 0.1、高度为 0。
- Reveal：点击投影仪 10 次，均按 light -> curtain height 0 到 5 -> curtain width 0.1 到 10 -> text 顺序显示。
- Mid reveal close：在 light 和 curtain 阶段分别再次点击，反向关闭一次且没有延迟文字/幕布出现。
- On close replay：完整打开后再次点击反向关闭，第三次点击从 light 重新 reveal。
- Outside click：点击非投影仪对象和空白区域，不改变投影状态。
- Use button：在投影前、投影 On、投影 Off 后触发 Use button 并打开 `PasswordPopup_63_2`。
- Use button：确认 Level63Ctrl 的 `Use_btn` 运行时来自 `CurrentGamePlayPanel` 上 `GamePlayPanel_017` 的 `GamePanel_UseBtnCommonFeature.Use_btn`，而不是依赖 `Level_063.prefab` 根对象空引用。
- Pixel glitch：确认效果只影响三个 Level63 投影 SpriteRenderer，文字可读，关闭后恢复原材质。
- Lifecycle：fail/restart/exit/completion 后重新进入，不残留投影视觉或 glitch 材质。
- Mobile：Android / iOS 横屏下连续 toggle、mid-reveal reverse close、Use button popup 和 glitch 表现无明显卡顿。
