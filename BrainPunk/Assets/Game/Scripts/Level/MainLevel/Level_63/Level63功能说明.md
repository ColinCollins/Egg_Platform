# Level63 功能说明

## 2026-07-22：投影 Pixel Glitch 真机长时静止修复

- 问题背景：Level63 在真机逐关运行较长时间后，投影 pixel glitch 可能只显示静态错位画面，不再继续变化；编辑器冷启动后直接进入 Level63 时不一定复现。
- 原因：shader 原先把持续增长的 `floor(_Time.y * _NoiseSpeed)` 直接传入 `Hash21`，哈希内部再乘以较大的常量并取小数。关卡由 `PlayCtrl.CreateLevelAsync(...)` 在同一玩法场景中实例化，不会为每个关卡重置 shader 全局时间，因此长时运行后浮点小数精度会逐步丢失，最终让时间哈希退化为固定值。
- 修复：shader 先把离散时间 tick 限制在 `0..255`，再参与像素块哈希；glitch 保持每秒按 `_NoiseSpeed` 更新，但不再随玩法场景累计运行时间增长而失去哈希小数精度。
- shader fallback 从项目中不存在的 `Universal Render Pipeline/2D/Sprite-Unlit` 修正为当前 URP 包实际提供的 `Universal Render Pipeline/2D/Sprite-Unlit-Default`。
- `Level_063.prefab` 中 `Sp_l63_img_bg_01` 与 `Sp_l63_img_bg_03` 的默认材质恢复为功能加入前使用的 `Sprite-Unlit-Default`；投影打开时仍由 `Level63Ctrl` 临时切换专属 glitch 材质，关闭、重开、disable、destroy 时可恢复正确的原材质。
- 本次不修改点击、投影 reveal/close 时序、材质参数、音效、UI、存档、配置、事件、分析、资源加载或平台 SDK 流程。
- 验证要求：除冷启动立即进入 Level63 外，还需在同一玩法场景累计运行至少 45 分钟后验证 glitch 仍持续变化，并验证开关、重开、退出时三个目标均能正确切换和恢复材质。

## 2026-06-10: SimpleAudioPlay local audio helper

- `SimpleAudioPlay` is a Level63-local MonoBehaviour helper for Inspector-driven SFX playback.
- Serialized controls: `clipName`, `loop`, `loopInterval`, `playOnStart`, and `stopSoundOnDisable`.
- `playOnStart` calls `PlayAudio()` from runtime `Start()` only.
- `PlayAudio()` stops previous playback owned by the component, then plays `clipName` through `Game.Scripts.Common.AudioManager.PlaySound(...)`.
- When `loop=false`, the component triggers one one-shot sound.
- When `loop=true` and `loopInterval <= 0`, the component uses the existing SoundsGood loop path through `AudioManager.PlaySound(clipName, loop:true)` and stores the returned `Sound` so it can be stopped.
- When `loop=true` and `loopInterval > 0`, the component starts a local coroutine that repeats one-shot playback every `loopInterval` seconds.
- `SimpleAudioPlay` stores the latest `Sound` returned by `AudioManager.PlaySound(...)`, including one-shot playback.
- `stopSoundOnDisable` defaults to true. When enabled, `OnDisable` calls `StopAudio()` to stop the current sound and the local interval coroutine.
- When `stopSoundOnDisable=false`, `OnDisable` only stops the local interval coroutine, allowing the current sound to continue after this component is disabled.
- `OnDestroy` always calls `StopAudio()` to prevent owned audio from surviving object destruction.
- Public methods available for UnityEvent binding: `PlayAudio()`, `StopAudio()`, and `RestartAudio()`.

## 2026-06-10: version 2.0.0 post-unlock Use button shutdown

- `Level63Ctrl` now subscribes to `OnTiggerItemEvent` and only reacts when `EventId == 1`.
- When id 1 is received, Level63 treats the existing unlock/completion flow as finished for the Use button path, unsubscribes the Use button click callback, sets the Use button non-interactable, plays the existing short scale-out feedback when active, and hides it.
- A runtime-only `_useButtonLocked` flag suppresses later `SwitchObjActiveEvent` show requests during the same active Level63 run, so `PasswordPopup_63_2` cannot be reopened after the id 1 trigger.
- `GameResetEvent`, `OnDisable`, and `OnDestroy` clear the runtime lock and clean up button tweens/subscriptions, so restart or re-entry can use the normal Use button flow again when level logic requests it.
- This change is Level63-local and does not add save fields, config entries, new unlock dispatch, analytics, shared Use button changes, or non-Level63 behavior changes.

## 2026-06-10: Projector size reveal and reverse close update

- `Level63Ctrl` changes projector reveal to size animation: curtain starts at width `0.1` and height `0`, grows height to the cached full height, then expands width from `0.1` to `10`.
- Player-driven projector off now hides projected text first, then plays the curtain animation in reverse: width contracts to `0.1`, then height contracts to `0`, then projection visuals are hidden.
- `Level63Ctrl` uses `Closing` state during reverse close; repeated projector clicks during `Closing` are ignored to avoid re-entrant close sequences.
- `Awake`, `GameResetEvent`, `OnDisable`, and `OnDestroy` still reset immediately without reverse playback so restart/exit/destroy paths do not leave delayed visual callbacks.

**日期**: 2026-06-05

## 功能目标

Level63 新增投影仪表现：玩家点击或触摸投影仪机身后，由现有 `ClickItemEventHandle` 触发 `Level63Ctrl.ClickProjector(...)`，`Level63Ctrl` 只负责投影仪开关、分阶段显示和清理。

该功能只作用于 Level63，不改共享点击检测、全局输入、角色移动、关卡生命周期、存档、解锁、打点、资源加载或其他关卡。

## 现有入口

- `Level_063.prefab` 根对象挂有 `Level63Ctrl`，`BaseLevelCtrl.gamePlayPanelName` 当前为 `GamePlayPanel_017`。
- `Projector/Sp_l63_img_syj_01` 上已有 `ClickItemEventHandle`，负责鼠标/触摸输入、2D raycast 命中自身、暂停态过滤和点击次数。
- `ClickItemEventHandle.OnClickEvent` 已绑定到根对象 `Level63Ctrl.ClickProjector(Transform, Vector2)`。
- `GamePlayPanel_017.prefab` 复用 `GamePanel_UseBtnCommonFeature`，该组件的 `Use_btn` 已绑定到面板内 Use button。
- `Use_btn` 字段在 `Level_063.prefab` 根对象上当前为空；运行时优先通过 `PlayCtrl.Instance.CurrentGamePlayPanel.GetComponent<GamePanel_UseBtnCommonFeature>().Use_btn` 获取 `GamePlayPanel_017` 的按钮，Inspector 字段只作为兜底引用。

## 投影仪状态

- `Off`：默认状态。投影光、幕布、文字隐藏；幕布尺寸重置为宽度 0.1、高度 0；glitch 材质恢复原材质。
- `Revealing`：点击投影仪从 Off 进入。投影光先显示，短暂停顿后幕布显示，先保持宽度 0.1 让高度从 0 增至完整高度 5，再让宽度从 0.1 增至配置宽度 10，最后文字显示。
- `On`：完整 reveal 完成后进入。光、幕布、文字保持显示，glitch 材质保持在目标投影 SpriteRenderer 上。
- `Closing`：玩家主动关闭时进入。文字先隐藏，幕布按打开动画反向播放一次：宽度收回 0.1，再高度收回 0，完成后回到 Off。

重复点击规则：

- Off 时点击：开始 reveal。
- Revealing 时点击：进入 Closing，已显示的幕布按当前尺寸反向关闭，且后续不会延迟显示文字或幕布阶段。
- On 时点击：进入 Closing 并反向播放一次；下一次点击重新从投影光开始 reveal。

## 投影视觉

- `Sp_l63_img_bg_02` 作为 projection light，第一阶段显示。
- `Sp_l63_img_bg_01` 作为 projection curtain，`SpriteRenderer.drawMode = Sliced`，通过 `SpriteRenderer.size` 做高度和宽度 reveal：宽度保持 0.1 时高度 0 到 5，然后高度保持 5 时宽度 0.1 到 10。
- `Sp_l63_img_bg_03` 作为 projected text，挂在幕布下，等幕布 reveal 完成后显示。
- 投影仪机身不会在 Off 状态隐藏，避免点击组件所在对象失去可点入口。

## Pixel Glitch

- 新增 shader：`Assets/Game/ArtSrc/Shader/Sprites/Level63ProjectorPixelGlitch.shader`。
- 新增材质：`Assets/Game/ArtSrc/Materials/Level63ProjectorPixelGlitch.mat`。
- `Level63Ctrl` 在 Awake 缓存目标 SpriteRenderer 的原始 `sharedMaterial`。
- 投影打开时只把 `Sp_l63_img_bg_02`、`Sp_l63_img_bg_01`、`Sp_l63_img_bg_03` 切到 Level63 专属 glitch 材质。
- 投影关闭、重开、disable、destroy 时恢复原始材质。

## Use Button / Password Popup

- `SwitchObjActiveEvent` 的 Use button show/hide scale feedback 保留原节奏。
- `Start()` / `OnEnable()` 会从 `CurrentGamePlayPanel` 获取 `GamePlayPanel_017` 上的 `GamePanel_UseBtnCommonFeature.Use_btn`，并订阅 `Use_btn.OnClick += OnShowPasswordPopup`，disable/destroy 时退订。
- `Use_btn` 获取失败时，show/hide、订阅、退订和 tween cleanup 都安全跳过。
- 点击可用 Use button 时仍调用 `PasswordPopup.Create("PasswordPopup_63_2")`；异常时只输出 warning，不让 Level63 崩溃。
- 使用规则：Level63 当前必须复用带 `GamePanel_UseBtnCommonFeature` 的 `GamePlayPanel_017`；如后续修改 `gamePlayPanelName`，需要先确认目标 GamePanel 提供同名组件或等价的 Use button provider，否则密码弹窗入口不会被稳定订阅。

## 生命周期清理

- `GameResetEvent`、`OnDisable`、`OnDestroy` 都会让投影状态回到 Off。
- 清理会 kill 当前 DOTween sequence，并用版本号阻止旧 reveal 回调在关闭后继续显示幕布或文字。
- `OnDestroy` 会重置事件订阅并清理 Use button tween。

## 相关文件

- `BrainPunk/Assets/Game/Scripts/Level/LevelItem/Level_63/Level63Ctrl.cs`
- `BrainPunk/Assets/Game/Prefabs/Level/Level_063.prefab`
- `BrainPunk/Assets/Game/ArtSrc/Shader/Sprites/Level63ProjectorPixelGlitch.shader`
- `BrainPunk/Assets/Game/ArtSrc/Materials/Level63ProjectorPixelGlitch.mat`
- `specs/005-level63-projector-presentation/`

## 验证重点

- 进入或重开 Level63 后投影光、幕布、文字隐藏，幕布宽度为 0.1、高度为 0。
- 点击投影仪后显示顺序为 light -> curtain height 0 到 5 -> curtain width 0.1 到 10 -> text。
- reveal 中再次点击会进入反向关闭，且后续不会延迟显示幕布或文字。
- On 状态再次点击反向关闭一次，下一次点击从 light 重新开始。
- 点击投影仪外的区域不改变投影状态。
- Use button 通过 `CurrentGamePlayPanel` 上的 `GamePlayPanel_017` / `GamePanel_UseBtnCommonFeature` 获取后仍打开 `PasswordPopup_63_2`，获取失败时不崩。
- Pixel glitch 只影响三个 Level63 投影 SpriteRenderer，关闭后恢复原材质。
- Android/iOS 横屏触控和性能仍需真机或模拟器验证。
