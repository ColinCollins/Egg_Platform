# Level65 功能说明

## 玩家流程

- 进入或重开 Level65 后，`AlienBubble` 保持隐藏状态，`alienBubbleRoot.localScale = Vector3.zero`，不会自动播放气泡入场。
- 点击 Alien 后，`Level65Ctrl.OnClickAlien(Transform, Vector2)` 只负责揭示气泡，不打开 `LocalizationPopup`。
- 首次点击 Alien 时，`AlienBubble` 从 0 缩放到原始尺寸，使用 `Ease.OutBack` 表现弹出。
- 如果当前使用 TMP 文本作为初始 alien 内容，`alienContent` 通过 `TextMeshPro.maxVisibleCharacters` 渐显；如果 `alienContentImg` 存在，则沿用图片版初始外星语表现，TMP fallback 不会被强行显示。
- 气泡已显示或正在显示时，再次点击 Alien 不会重播气泡动画，也不会重置当前 alien 内容。
- `LocalizationPopup` 的打开仍走现有系统逻辑，例如设置入口调用 `LocalizationPopup.Create(...)`。
- Level65 激活期间监听 `LocalizationPopup.OnPopupCreated`，在系统正常创建的语言弹窗中追加一个 `Level65AlineLanguageItem` 临时项。
- 选择外星语言项后，当前弹窗选中表现被清空，`alienContent` 立即变为 `3315`，不会修改真实语言设置。
- 如果玩家在点击 Alien 前选择外星语言项，`AlienBubble` 会立即揭示并显示 `3315`。
- 普通语言切换触发 `SwitchLanguageEvent` 时，Level65 会停止当前文字/气泡 tween，把 `hasSwitchAlienLanguage` 重置为 `false`，并恢复 prefab 初始 alien 内容，不重播气泡入场动画。
- 玩家进入 `PasswordBoard` 触发区后，公共 Use 按钮出现；点击 Use 打开 `PasswordPopup_65_3`。
- `PasswordPopup_65_3` 解锁成功派发 `OnTiggerItemEvent` 且 `EventId == 1` 后，Level65 会隐藏 Use 按钮并把 `Use_btn.Interactable` 置为 `false`，本次关卡 session 内不再恢复交互。

## 实现边界

- Level65 玩法逻辑集中在 `Level65Ctrl.cs`。
- `Level_065.prefab` 的 Alien 物体持有 `BoxCollider2D` 与 `ClickItemEventHandle`，`OnClickEvent` 绑定到 `Level65Ctrl.OnClickAlien`。
- Alien 点击只揭示气泡，不负责打开语言弹窗。
- `LocalizationPopup` 现有 `OnPopupCreated`、`AddItem`、`CancelAllItem`、`RemoveCustomItem` 会话 API 由 Level65 监听和调用；本次不再扩大共享弹窗脚本改动。
- `LanguageItem.InitCustom` 只用于无配置表语言 key 的临时项。
- 普通语言项仍走原有 `OnClickLanguageItem` 保存路径。
- `hasSwitchAlienLanguage` 只负责控制 Level65 外星语言临时项的显示状态，不改写系统普通语言 key 与保存流程。
- `SwitchLanguageEvent` 只负责把 Level65 当前显示的 alien 内容重置回初始内容，不改写普通语言切换与保存流程。
- `OnTiggerItemEvent.EventId == 1` 只锁定 Level65 的 Use 按钮，不改通关、存档、配置或系统语言逻辑。
- 不新增存档字段、Luban 配置、ProjectSettings 或平台设置。

## Prefab 绑定

- `Level_065.prefab` 根对象的 `Level65Ctrl.alienLanguageItem` 指向 `Level65AlineLanguageItem.prefab`。
- `Level65Ctrl.alienContent` 指向 `alienContent` 的 TextMeshPro。
- `Level65Ctrl.alienContentImg` 指向 `level65_alienContent` 图片节点；该图片存在时作为默认外星语表现。
- `Level65Ctrl.alienBubbleRoot` 指向 `AlienBubble`。
- Alien 的 `ClickItemEventHandle.OnClickEvent` 指向根对象 `Level65Ctrl.OnClickAlien`。
- `BaseLevelCtrl.gamePlayPanelName` 为 `GamePlayPanel_017`，Use 按钮来自 `GamePanel_UseBtnCommonFeature.Use_btn`。

## 清理策略

- `GameResetEvent`、`SwitchGameStateEvent.SUCCESS`、`SwitchGameStateEvent.FAILED`、`OnDisable`、`OnDestroy` 会清理当前 Level65 popup session、停止 tween、隐藏气泡并恢复初始 alien 内容。
- `3315` 不跨重开、失败、通关、退出、禁用、销毁或重新进入保留。
- Level65 清理时只移除当前系统语言弹窗中的临时外星项引用，不销毁系统弹窗。
- Use 按钮订阅在禁用和销毁时解除；解锁成功后会隐藏并禁用交互，重开时重新解除锁定。
- 语言弹窗关闭或销毁后不会保留临时外星项引用。

## 验证记录

- 静态确认：`Level65Ctrl.Start`、`OnEnable`、`OnGameReset` 不再调用自动入场播放，改为隐藏复位。
- 静态确认：`Level65Ctrl.OnClickAlien` 不调用 `LocalizationPopup.Create`，只调用气泡揭示逻辑。
- 静态确认：`Level_065.prefab` Alien 点击事件绑定到 `Level65Ctrl.OnClickAlien`。
- 静态确认：`Level65Ctrl` 订阅 `OnTiggerItemEvent`，`EventId == 1` 后调用本地 Use 按钮锁定隐藏逻辑。
- 待验证：Unity PlayMode 仍需覆盖进入、重开、Alien 点击、重复点击、外星语言项选择前后、Use 密码弹窗和普通语言弹窗。
- 待验证：Android/iOS 横屏触控、弹窗交互和 60 fps 表现仍需真机或模拟器验证。

## 2026-07-14：外星语言项首位显示

- Level65 仍通过 `LocalizationPopup.AddItem(alienLanguageItem, OnClickAlienLanguageItem)` 追加外星语言临时项；该两参数调用现在使用 `isFirst = true` 默认值。
- 外星语言项创建后位于语言列表第一位，语言弹窗会强制刷新布局、停止滚动惯性并回到顶部，避免打开时首项处于可视区域外。
- 本次不修改外星语言点击后的 `3315` 表现、普通语言保存、Level65 生命周期清理、事件或 prefab 绑定。
- 静态代码与 prefab YAML 已确认 `LocalizationPopup.content` 同时是父级 `ScrollRect` 的内容节点；Unity PlayMode 和 Android/iOS 横屏滚动表现仍待验证。
- 回滚共享弹窗的 `isFirst` 排序与回顶逻辑即可恢复 Level65 外星语言项末尾追加的原表现。
