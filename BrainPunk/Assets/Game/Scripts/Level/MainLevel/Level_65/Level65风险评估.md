# Level65 风险评估

## 原始行为基线

- 早期 `Level65Ctrl.cs` 只有 `alienLanguageItem` 与 `alienContent` 两个序列化字段，运行逻辑为空，注释描述气泡缩放、文字渐显、外星语言项、`3315` 线索和 `PasswordPopup_65_3`。
- 2026-06-09 前一版实现会在 `Start`、`OnEnable`、`OnGameReset` 中自动播放 `PlayAlienIntroduction()`，进入或重开 Level65 后气泡会自动从 0 缩放到原始尺寸。
- 最新规格要求：进入、重开、失败、通关、退出、禁用、销毁或重新进入后，AlienBubble 必须隐藏；只有点击 Alien 或选择外星语言项才揭示。
- `Level_065.prefab` 根对象持有 `Level65Ctrl`，已配置 `alienLanguageItem`、`alienContentImg`、`alienContent`、`alienBubbleRoot`；Alien 物体已有 `BoxCollider2D` 与 `ClickItemEventHandle`，本次只补齐 `OnClickEvent` 绑定。
- 普通 `LocalizationPopup.Create(Action<string>)` 会重建配置语言项；普通语言项点击会更新 `LocalizationManager.CurrentLanguageCode`、回调、`DB.GameSetting.CurrentLanguageKeyCode` 并保存。
- `LocalizationPopup` 与 `LanguageItem` 当前已有 `OnPopupCreated`、`AddItem`、`CancelAllItem`、`RemoveCustomItem`、`InitCustom`，本次不再修改共享弹窗脚本。

## 本次接入范围

- `Level65Ctrl` 负责隐藏初始气泡、Alien 点击揭示气泡、重复点击抑制、外星语言项选择揭示 `3315`、生命周期清理和 Use 按钮打开 `PasswordPopup_65_3`。
- `Level_065.prefab` 的 Alien `ClickItemEventHandle.OnClickEvent` 绑定到 `Level65Ctrl.OnClickAlien(Transform, Vector2)`。
- `GameResetEvent`、`SwitchGameStateEvent.SUCCESS`、`SwitchGameStateEvent.FAILED`、`OnDisable`、`OnDestroy` 会恢复隐藏气泡与初始 alien 内容。
- `SwitchLanguageEvent` 会停止当前 tween、收束正在揭示的气泡状态、重置外星语言显示标记并恢复初始 alien 内容。
- 外星语言项选择会调用 `LocalizationPopup.CancelAllItem()` 清除表现，但不会进入普通语言保存路径。
- `OnTiggerItemEvent.EventId == 1` 作为 `PasswordPopup_65_3` 解锁成功后的本地信号；Level65 收到后隐藏 Use 按钮并禁用交互，避免解锁后重复打开密码弹窗。
- 不修改其他关卡、不新增存档字段、不改 Luban 配置、不改 RemoteConfig、不改 ProjectSettings、不改平台 SDK。

## 影响文件与资源

- `Assets/Game/Scripts/Level/LevelItem/Level_65/Level65Ctrl.cs`
- `Assets/Game/Prefabs/Level/Level_065.prefab`
- `Assets/Game/Scripts/Level/LevelItem/Level_65/Level65功能说明.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_65/Level65风险评估.md`
- `DevelopMD/已有功能设计与使用记录.md`
- `specs/006-level65-ctrl-analysis/tasks.md`

## 已使用的现有系统

- `ClickItemEventHandle` 检测 Alien 的 2D 点击并通过 `UnityEvent<Transform, Vector2>` 派发。
- `LocalizationPopup.OnPopupCreated` / `AddItem(...)` / `CancelAllItem()` / `RemoveCustomItem()` 负责 Level65 的外星语言临时项会话扩展。
- `LanguageItem.InitCustom(...)` 用于不依赖配置表的临时项点击，不设置语言 key。
- `GameSettingPopup.OpenLocalizationPopup(...)` 负责通过 `LocalizationPopup.Create(...)` 打开系统语言弹窗，并在普通语言切换后派发 `SwitchLanguageEvent`。
- `GamePanel_UseBtnCommonFeature.Use_btn` 作为 Level65 的公共 Use 按钮入口。
- `OnTiggerItemEvent` 使用 `EventId == 1` 表示密码/解锁流程完成，Level65 只消费该事件来锁定 Use 按钮显示与交互。
- `DOTween` 负责 AlienBubble 缩放表现；TMP fallback 文本使用 `maxVisibleCharacters` 渐显。

## 变更入口到影响链路

- `Level65Ctrl.Start/OnEnable/OnGameReset` 改为隐藏复位 -> Level65 入场和重开不会自动显示气泡 -> 玩家必须点击 Alien 或选择外星语言项才能看到内容。
- Alien `ClickItemEventHandle.OnClickEvent` 绑定 `OnClickAlien` -> 玩家点击 Alien -> `RevealAlienBubble(false)` -> 气泡缩放，TMP fallback 逐字显示或图片初始内容可见。
- 外星语言临时项点击 -> `OnClickAlienLanguageItem` -> `SetAlienContent(true)` + `RevealAlienBubble(true)` -> 隐藏气泡也会揭示并显示 `3315`，但不保存真实语言。
- `SwitchGameStateEvent.SUCCESS/FAILED` 或 `GameResetEvent` -> `ResetAlienPresentationState` -> 停止 tween、隐藏气泡、恢复初始内容，避免 `3315` 跨 session 残留。
- `OnTiggerItemEvent.EventId == 1` -> `Level65Ctrl.OnTiggerItem` -> `_useButtonLocked = true` -> `Use_btn.Interactable = false` 并隐藏 Use 按钮；后续触发区显示事件不会恢复本 session 的 Use 交互。

## 风险项

- P1：如果 `ClickItemEventHandle` 的 Alien 命中盒尺寸不符合美术实际范围，移动端点击可能难以触发；需要 PlayMode 和设备触控验证。
- P1：如果 UnityEvent 签名或 prefab fileID 绑定在 Unity 导入后失效，Alien 点击不会调用 `OnClickAlien`；需要在 Unity Inspector 或 PlayMode 中确认。
- P1：如果普通语言切换发生在气泡 tween 中，状态可能中断；当前已通过 `CompleteInterruptedBubbleReveal()` 收束到已显示状态，但仍需运行验证。
- P1：如果 `alienContentImg` 与 TMP fallback 的显示预期被策划调整，当前“图片存在则显示图片、TMP 不逐字播放”的实现可能与“文字逐字显示”验收口径不一致；需要以 prefab 美术和策划验收确认。
- P1：如果 `LocalizationPopup.OnPopupCreated` 时序早于 Level65 active 或晚于 popup 初始化，本次 Level65 追加外星项路径仍需 PlayMode 验证。
- P1：如果 Use 按钮在 `GamePlayPanel_017` 创建时序中晚于 Level65 `Start`，`ResolveUseButton()` 可能需要依赖后续 `SwitchObjActiveEvent` 重试；当前保留了原有重试逻辑，仍需验证。
- P1：`GamePanel_UseBtnCommonFeature` 也会响应 `SwitchObjActiveEvent` 显示 Use 按钮；Level65 已在锁定后排队再次隐藏按钮以保证最终状态，但事件时序仍需 PlayMode 验证。

## 手工验证清单

- 进入或重开 Level65 10 次：AlienBubble 初始保持隐藏，不自动播放气泡入场。
- 点击 Alien 10 次：首次点击揭示气泡；点击不打开 `LocalizationPopup`。
- 气泡显示后重复点击 Alien 10 次：不重播气泡动画，不重置当前消息，包括 `3315`。
- 未点击 Alien 时打开系统语言弹窗并选择外星语言项 10 次：气泡立即揭示并显示 `3315`，保存语言 key 不变化。
- 已点击 Alien 后选择外星语言项 10 次：当前内容变为 `3315`，气泡不重播，保存语言 key 不变化。
- 重开、失败、退出、通关、禁用、销毁或重新进入后：气泡隐藏，初始 alien 内容恢复，`3315` 不残留。
- 通过现有系统入口打开 `LocalizationPopup` 10 次：普通语言项存在，Level65 active 时每次只有一个外星语言项。
- 普通设置语言弹窗：Level65 inactive 时不显示外星语言项；普通语言选择仍会更新并保存。
- 进入 `PasswordBoard` 触发区前后：Use 按钮按现有表现显示/隐藏；点击 Use 打开 `PasswordPopup_65_3`。
- `PasswordPopup_65_3` 解锁成功后：确认 `OnTiggerItemEvent id = 1` 会让 Use 按钮隐藏且不可交互；离开/重新进入触发区不再显示可点 Use；重开后 Use 入口恢复初始可用规则。
- Android/iOS 横屏设备或模拟器：Alien 点击、气泡、系统语言弹窗、外星项、Use 密码弹窗和普通设置弹窗保持 60 fps 目标体验。

## 已验证证据

- 2026-06-10：静态确认 `Level65Ctrl.Start`、`OnEnable`、`OnGameReset` 已改为 `ResetAlienPresentationState()`，不再自动揭示气泡。
- 2026-06-10：静态确认 `Level65Ctrl.OnClickAlien(Transform, Vector2)` 不调用 `LocalizationPopup.Create`，只进入 `RevealAlienBubble(false)`。
- 2026-06-10：静态确认 `OnClickAlienLanguageItem(...)` 会设置 `hasSwitchAlienLanguage = true`、显示 `3315`，并调用 `RevealAlienBubble(true)` 支持未点击 Alien 前选择外星语言项。
- 2026-06-10：静态确认 `Level_065.prefab` Alien 的 `OnClickEvent` 绑定到根对象 `Level65Ctrl.OnClickAlien`。
- 2026-06-10：静态确认 `LocalizationPopup.cs`、`LanguageItem.cs` 已有本需求所需 session API，本次未继续修改共享弹窗脚本。
- 2026-06-10：静态确认 `Level65Ctrl` 已订阅 `OnTiggerItemEvent`，`EventId == 1` 后会锁定并隐藏 Use 按钮，`GameResetEvent` 会解除本地锁定。

## 未验证缺口

- 未在 Unity Editor PlayMode 实际进入 Level65，因此 Alien 点击命中、气泡缩放、图片/TMP 显示节奏、重复点击抑制仍需运行确认。
- 未在 PlayMode 验证系统正常打开 `LocalizationPopup` 时，Level65 active 状态下外星项追加且每次只有一个。
- 未在 PlayMode 验证外星语言项选择前后 saved language 不变。
- 未在 PlayMode 验证 `GamePlayPanel_017` 创建时序下 Use 按钮稳定获取且不会重复订阅。
- 未在 PlayMode 验证 `PasswordPopup_65_3` 在外星线索揭示前后均能打开。
- 未在 PlayMode 验证 `OnTiggerItemEvent id = 1` 后 Use 按钮最终隐藏且不可交互，以及重开后是否按初始规则恢复。
- 未在 Android/iOS 设备或模拟器验证触控和帧率。

## 回滚路径

- 回滚 `Level65Ctrl.cs` 中 `OnClickAlien`、`RevealAlienBubble`、隐藏入场复位、`SwitchGameStateEvent` 清理、`OnTiggerItemEvent` Use 锁定隐藏和外星语言项前置揭示逻辑。
- 回滚 `Level_065.prefab` Alien 的 `ClickItemEventHandle.OnClickEvent` 绑定；如需要完全回退，也移除 Alien 上的 `BoxCollider2D` 与 `ClickItemEventHandle`。
- 保留或回滚 `Level65功能说明.md`、本风险评估和 `DevelopMD/已有功能设计与使用记录.md` 中对应记录。
- 本次未改存档、配置表、ProjectSettings、RemoteConfig 或平台设置，因此无需数据迁移回滚。

## 待确认

- 策划是否要求图片版初始 alien 内容也必须转换成逐字 TMP 显示；当前依据 2026-06-09 本地记录保留“图片存在时显示图片”的行为。
- `SwitchLanguageEvent` 后，当前实现恢复初始 alien 内容但不重播气泡入场；是否需要在普通语言切换后重新隐藏气泡，待 PlayMode/策划验收确认。

## 2026-07-14：外星语言项首位与回顶风险补充

### 行为基线与变更

- 原有 `LocalizationPopup.AddItem(...)` 将 Level65 外星语言项追加到普通语言列表末尾，且不改变 `ScrollRect` 的当前滚动位置。
- 当前两参数调用默认使用 `isFirst = true`：外星语言项移动到列表首位，布局重建后停止滚动惯性并把垂直归一化位置设为顶部。
- 影响文件为 `LocalizationPopup.cs` 及本地文档；不修改 Level65 脚本、prefab、存档、配置、资源、事件或平台流程。

### 风险、验证与回滚

- P1：仍需在 Unity PlayMode 验证弹窗首次打开、滚动后重新打开、连续重建及普通语言切换后，外星语言项始终处于首位且顶部位置稳定。
- P1：仍需在 Android/iOS 横屏验证强制布局刷新不会产生可感知卡顿，触控拖动不会被残余惯性影响。
- 静态 YAML 已确认 `LocalizationPopup.prefab` 的 `ScrollRect.m_Content` 与脚本 `content` 指向同一节点；缺少父级 `ScrollRect` 时实现会保留新增项并输出 warning。
- C# 工程构建已通过，结果为 0 个错误、1116 个警告；Unity PlayMode 与设备验证尚未执行。
- 回滚 `LocalizationPopup.AddItem(...)` 的 `isFirst`、首位排序和回顶调用，即可恢复外星语言项末尾追加的基线行为。
