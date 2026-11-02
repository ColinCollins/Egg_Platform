# Level62 风险评估

更新时间: 2026-06-10

## 原有行为基线

- `Level62Ctrl.cs` 和 `Level62BlockItemCtrl.cs` 原有脚本主要保留注释和空生命周期方法；注释描述了单点选择、框选和 selectBox/fragile 平台联动目标。
- `Level_062.prefab` 当前工作区中已包含 Level62 controller、多个 block item、选择框图、Collider2D 和 `SpriteRenderFragilePlatform` 组件。
- `SpriteRenderFragilePlatform` 原有职责是碰撞后关闭 collider，并通过 `SpriteRendererGroupAlpha` 渐隐；旧的 `autoBreak=false` 曾用于阻止自动破碎。
- 用户确认新的 Level62 v2.0.0 语义：未选中时 actor 无法触碰 block；选中时 collider 打开，actor 可以触碰 block；触碰后先等待可配置的 `contactBreakDelaySeconds`，等待结束后 collider 关闭并进入 `fadeOutDuration` 渐隐，渐隐完成后恢复为可见未选中状态，不销毁对象。
- 用户确认 Level62 blockItem 选中表现：选中时 `SpriteRendererGroupAlpha` alpha = 1，非选中时 alpha = 0.7。

## 影响文件 / 资源

- `Assets/Game/Scripts/Level/LevelItem/Level_62/Level62Ctrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_62/Level62BlockItemCtrl.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_62/SpriteRenderFragilePlatform.cs`
- `Assets/Game/Scripts/Level/LevelItem/Level_62/Level62功能说明.md`
- `Assets/Game/Scripts/Level/LevelItem/Level_62/Level62风险评估.md`
- `Assets/Game/Prefabs/Level/Level_062.prefab`
- `DevelopMD/已有功能设计与使用记录.md`
- `specs/004-level62-block-select/`

## 使用的既有系统

- Unity touch input / Editor mouse fallback。
- Unity 2D physics `Collider2D`、`OnCollisionEnter2D`、`OnTriggerEnter2D`。
- `SpriteRenderer` 用于 selectBox 和 Selector 显示。
- `SpriteRenderer.drawMode = Sliced` 与 `SpriteRenderer.size` 用于 Selector 框选大小控制。
- `SpriteRendererGroupAlpha.SetAlpha(float)` 用于 blockItem 选中/非选中 alpha 表现，也用于易碎平台渐隐。
- `DOTween` 用于触碰后的延迟消失调度与 alpha 渐隐。
- `EventsUtils.ResetEvents(ref _subscriber)` / `EventSubscriber` 监听游戏状态切换和重开事件。
- `GamePlayStateName.PLAYING` 用于非游玩态暂停 Level62 输入处理。
- `InputUtils.IsPointerOverUI(...)` 用于忽略 UI 上的点击。

## 已规避风险

- Level62 选择逻辑保留在 `Level62Ctrl`、`Level62BlockItemCtrl` 和 Level62 本地 fragile 平台脚本内，不新增全局输入、存档或配置表。
- `Level62Ctrl` 只处理第一个 active touch，降低多点触控导致选区和单点选择互相覆盖的风险。
- Selector 在 release/cancel/disable/destroy/restart/非 PLAYING 状态时隐藏并清理 active touch，降低残留框选视觉风险。
- Selector 使用 sliced size 控制，降低框选框视觉变形风险。
- 进入或重开会对所有配置 block 调用 ResetSelection；完整 block 会重置为未选中并关闭 actor 接触，降低 stale selected state 泄漏到下一轮的风险。
- direct tap 只保留被点击 block 选中，框选会追加选中，符合注释中两种选择规则。
- `Level62BlockItemCtrl` 在引用缺失时尝试从自身和子节点查找 `SpriteRenderFragilePlatform` / selectBox，降低 prefab 引用漏绑导致功能完全失效的风险。
- 未选中 block 会关闭 `SpriteRenderFragilePlatform` 的 collider，避免 actor 继续被未选中 block 支撑或碰撞。
- 点击命中改用 block 可见 `SpriteRenderer.bounds`，避免 collider 关闭后玩家无法点击未选中 block。
- `SpriteRenderFragilePlatform` 使用 `BreakState.Intact/Breaking` 区分完整与延迟/渐隐流程，避免选择状态覆盖正在渐隐的平台。
- 选择状态变化不会取消已开始的 contact delay / fade tween，不再出现选中状态切换后意外取消或恢复破碎流程的问题。
- `FormerlySerializedAs("autoBreak")` 用于将 prefab 旧字段迁移到 `actorContactEnabled`；`FormerlySerializedAs("destroyAfterSeconds")` 用于将旧延迟字段迁移到 `contactBreakDelaySeconds`，降低 Unity 序列化字段改名风险。
- 当前脚本在 Breaking 渐隐完成后恢复为可见未选中状态；如果 Level62 重开流程发生在 Breaking 过程中，则需要 PlayMode 验证该行为是否符合关卡重开预期。

## 验证证据

- 静态证据：`Level_062.prefab` 中四个配置 block 均有 `Level62BlockItemCtrl`、`SpriteRendererGroupAlpha`、`SpriteRenderFragilePlatform`、selectBox 和 Collider2D 组件引用。
- 静态证据：仓库内 `SpriteRenderFragilePlatform` 仅在 Level62 脚本和 Level62 文档/规格中出现，未发现其他关卡脚本引用该类。
- 已同步 Level62 功能说明、Spec Kit spec、plan、data-model、contract、tasks 和全局设计记录，验收口径更新为 selected actor contact fades then restores visible unselected.
- 已执行 C# 编译检查：在 `BrainPunk/` 下使用 `dotnet build Assembly-CSharp.csproj --no-restore`，最后一次结果为 0 个错误、1108 个项目既有 warning。
- Unity PlayMode 与 Android/iOS 真机验证仍列为未验证风险。

## 未验证 / 剩余风险

- 尚未在 Unity Editor PlayMode 中验证 block list、selectBox、selectorBox、Collider2D 和 fragilePlatform 的运行时引用解析。
- 当前工作区 `Level_062.prefab` 中部分 `SpriteRenderer` / `SpriteRendererGroupAlpha` authored alpha 为 0.4；运行时进入/重开会通过 `Level62BlockItemCtrl.unselectedAlpha = 0.7` 重设未选中 block，authored alpha 0.4 的最终视觉意图仍需 Unity PlayMode 确认。
- 尚未在 Unity Editor PlayMode 中验证 `SpriteRendererGroupAlpha` 覆盖范围是否正好对应 blockItem 预期视觉，不误改不该变透明的子 SpriteRenderer。
- 尚未在 Unity Editor PlayMode 中验证未选中 block 关闭 collider 后，actor 确实无法触碰或被支撑。
- 尚未在 Unity Editor PlayMode 中验证 collider 关闭后，玩家仍可通过可见 bounds 点击未选中 block。
- 尚未在 Unity Editor PlayMode 中验证 actor 触碰 selected block 后会继续碰撞 `contactBreakDelaySeconds` 秒，随后关闭 collider、按 `fadeOutDuration` 渐隐，并恢复为可见未选中状态。
- 尚未在 Unity Editor PlayMode 中验证已进入 Breaking 的 block 后续选择/取消选择不会提前恢复 alpha、打开 collider 或取消恢复流程。
- 尚未在 Unity Editor PlayMode 中确认 Level62 restart/re-entry 在 Breaking 过程中是否应等待 fade 完成恢复，或由关卡重建恢复初始状态。
- 尚未在 Unity Editor PlayMode 中验证单点选择所有 block、框选四个方向、空框选、追加选择和快速连续选择。
- 尚未在 Unity Editor PlayMode 中验证 `SP_L62_xzk_img_02` sprite 的 border/slice 设置是否完整；如果资源没有设置 border，`Sliced` size 控制可能仍无法达到预期九宫格效果。
- 尚未在 Unity Editor PlayMode 中验证 fail/restart/exit/completion 后 Selector、active touch 和 selected state 不残留。
- 尚未在 Android / iOS 真机或模拟器上验证横屏触控、框选拖拽、多点触控忽略和性能。

## 回滚路径

- 回滚 `Level62Ctrl.cs` 的可见 bounds 点击命中和选择控制逻辑。
- 回滚 `Level62BlockItemCtrl.cs` 的 selected/unselected 状态 API 和 actor contact 映射。
- 回滚 `SpriteRenderFragilePlatform.cs` 的 actor contact / Breaking 状态机，恢复旧碰撞后直接破碎行为或上一个已验收版本。
- 回滚 `Level_062.prefab` 中 Level62 controller、block item、selectBox、selectorBox 和 fragilePlatform 相关引用调整。
- 回滚或保留本风险评估、功能说明和 Spec Kit 文档时，以实际代码回滚范围为准。
- 本功能不新增存档字段或全局配置，回滚不需要迁移玩家数据。

## 后续验证清单

- Compile：`dotnet build Assembly-CSharp.csproj --no-restore` 保持 0 个错误。
- Prefab：确认 `Level_062.prefab` 根节点有 `Level62Ctrl`，block list、selectorBox、selectBox、fragilePlatform 和 Collider2D 引用完整。
- Entry/restart：进入和重开后完整 block 未选中、selectBox 隐藏、alpha 0.7、actor contact 关闭、Selector 隐藏；同时确认 Breaking 中平台是否等待 fade 完成恢复或通过关卡重建恢复。
- Tap：逐个点击每个可选 block，只保留当前点击 block 选中且 alpha 为 1，其他 block 取消选中且 alpha 为 0.7。
- Tap unselected collider-off：未选中 block collider 关闭时，仍能通过可见 bounds 被点击选中。
- Area：从空白区域向右上、右下、左上、左下框选，Selector 使用 sliced size 改变大小，中心点在框内的 block 被追加选中且 alpha 为 1。
- Empty area：框选不包含任何 block 中心点时，不取消已有选中。
- Multi-touch：第一个触摸未结束时，额外触摸不改变选中结果、Selector 大小或清理流程。
- Collision unselected：未选中 block 不支撑 actor，actor 不应触碰该 block。
- Collision selected：已选中 block 被 actor 触碰时，先保持 collider 开启 `contactBreakDelaySeconds` 秒，随后关闭 collider、按 `fadeOutDuration` 渐隐，并在渐隐完成后恢复为可见未选中状态。
- Already breaking：Breaking 中的平台后续被选择或取消选择，不应提前恢复 alpha、打开 collider 或取消恢复流程。
- Lifecycle：fail/restart/exit/completion 后重新进入，不残留 Selector、active touch 或 stale selected state；如果关卡对象不重建，需要额外确认破碎平台状态是否按设计保留或恢复。
- Mobile：Android / iOS 横屏中 tap、drag、multi-touch 和选择表现保持响应，无明显输入卡顿。
