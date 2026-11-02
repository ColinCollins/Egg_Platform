# Level62 功能说明

**日期**: 2026-06-10

## 功能目标

Level62 的核心机制是 block 选择。玩家可以点击单个可选 block，或从空白区域拖拽框选多个 block，改变这些 block 的选中状态、透明度表现和对应易碎平台的 actor 接触状态。

该功能只作用于 Level62，不修改全局输入、角色移动、关卡生命周期、存档、解锁、打点、资源加载或其他关卡。

## 证据来源

- `Level62Ctrl.cs` 顶部注释要求监听 touch down，先判断是否命中 TriggerItem；命中时触发单个 `Level62BlockItemCtrl` 选中逻辑，未命中时进入框选逻辑。
- `Level62Ctrl.cs` 顶部注释要求框选时 Selector 根据 startPosition 和 currentPosition 调整 size，EndTouch 时框选范围内的 blockItem 被选中且不互相取消。
- `Level62BlockItemCtrl.cs` 当前通过 `SpriteRendererGroupAlpha` 控制 blockItem 透明度：选中 alpha 为 1，未选中 alpha 为 0.7。
- `SpriteRenderFragilePlatform.cs` 当前使用 `actorContactEnabled` 控制 actor 是否可以触碰 block；触碰有效 actor 后先等待 `contactBreakDelaySeconds`，等待结束后关闭 collider，按 `fadeOutDuration` 渐隐，渐隐完成后恢复为可见未选中状态，不销毁对象。
- `Level_062.prefab` 已包含 Level62 controller、多组 block item、selectBox、Collider2D、`SpriteRendererGroupAlpha` 和 `SpriteRenderFragilePlatform` 组件。

## 设计规则

### 初始状态

- 每次进入或重开 Level62 时，所有配置 block 都强制重置为未选中。
- 未选中 block 隐藏 selection frame/selectBox。
- 未破碎且未处于破碎中的 block 会将 `SpriteRendererGroupAlpha` alpha 设为 0.7。
- 未破碎且未处于破碎中的 block 会关闭 actor 接触，actor 不能被该 block 支撑或碰撞。
- Selector 初始隐藏。
- 上一次触摸、框选或选中产生的临时状态不能泄漏到下一次进入或重开。
- 当前脚本会在 Breaking 渐隐完成后恢复为可见未选中状态；如果重开流程发生在 Breaking 过程中，ResetSelection 不会中断该延迟/渐隐流程。

### 单点选择

- Level62 同一时间只处理第一个有效触摸。
- touch down 命中可选 block 时，执行单点选择。
- 单点选择会选中被点击的 block，并取消其他配置 block 的选中状态。
- 被选中的完整 block 显示 selection frame/selectBox，`SpriteRendererGroupAlpha` alpha 设为 1，并打开 actor 接触。
- 被取消选中的完整 block 隐藏 selection frame/selectBox，`SpriteRendererGroupAlpha` alpha 设为 0.7，并关闭 actor 接触。
- Breaking 中的 block 后续选择不会提前恢复 selectBox、alpha 或 collider。

### 易碎平台接触状态

- 选中 block 时，`SpriteRenderFragilePlatform.actorContactEnabled` 为 true，actor 可以触碰 block。
- actor 触碰已选中 block 后，平台进入 Breaking：保持 collider 开启并继续允许 actor 碰撞，等待 `contactBreakDelaySeconds` 秒后关闭 collider。
- collider 关闭后按 `fadeOutDuration` 渐隐，渐隐完成后恢复为可见未选中状态：selectBox 隐藏、alpha 0.7、actor 接触关闭。
- `contactBreakDelaySeconds <= 0` 时会立即关闭 collider 并开始渐隐。
- 取消选中不会取消已经开始的延迟/渐隐流程。
- 如果 block 已经开始 Breaking，后续选择/取消选择不能提前恢复 alpha、重新打开 collider 或取消恢复流程。
- `SpriteRenderFragilePlatform.SetAutoBreak(...)` 仅保留为兼容入口，实际映射到 actor 接触开关。

### 点击命中与 actor 接触解耦

- 未选中 block 的平台 collider 会关闭，以阻止 actor 接触。
- 因为 Level62 仍需要允许玩家点击未选中 block，`Level62Ctrl` 的点击命中不再依赖平台 collider，而是通过配置 block 的可见 SpriteRenderer bounds 判断。
- `selectableLayers` 仍用于过滤 block item 所在层，避免非配置层参与点击选择。

### 框选

- touch down 没有命中可选 block 时，从该位置开始框选。
- 框选期间 Selector 根据 touch startPosition 和 currentPosition 显示完整矩形，拖拽方向可以向左、右、上、下。
- Selector 使用 `SpriteRenderer.drawMode = Sliced`，并通过 `SpriteRenderer.size` 控制框选区域大小，避免整体 scale 拉伸导致边框变形。
- EndTouch 时，以 block item 的中心点是否在 Selector 区域内作为命中规则。
- 中心点在 Selector 内的 block 会被选中。
- 框选是追加选择，不会因为其他 block 不在框内而取消它们已有的选中状态。
- 若框内没有任何 block 中心点，已有选中状态保持不变，仅清理 Selector。

### 多点触控

- 有一个触摸正在驱动点击或框选时，额外触摸会被忽略。
- 直到第一个有效触摸结束，Level62 才能开始下一次选择交互。

### 生命周期清理

- Touch release/cancel 后，Selector 必须隐藏。
- Level62 disable/destroy/restart/fail/exit/completion 时，Selector 和 active touch 临时状态必须清理。
- 重开或重新进入时，Level62Ctrl 会清理 Selector 和 active touch，并对所有配置 block 调用 ResetSelection；完整 block 回到未选中且 actor 接触关闭，Breaking 中 block 不会被 ResetSelection 提前恢复。

## 相关文件

- `BrainPunk/Assets/Game/Scripts/Level/LevelItem/Level_62/Level62Ctrl.cs`
- `BrainPunk/Assets/Game/Scripts/Level/LevelItem/Level_62/Level62BlockItemCtrl.cs`
- `BrainPunk/Assets/Game/Scripts/Level/LevelItem/Level_62/SpriteRenderFragilePlatform.cs`
- `BrainPunk/Assets/Game/Prefabs/Level/Level_062.prefab`
- `specs/004-level62-block-select/spec.md`
- `specs/004-level62-block-select/plan.md`
- `specs/004-level62-block-select/quickstart.md`

## 验证重点

- 进入和重开 Level62 后，完整 block 未选中、selectBox 隐藏、alpha 0.7，actor 接触关闭；Breaking block 不应被选择状态提前恢复。
- 点击任意可选 block，只保留该 block 选中。
- 完整 block 选中 alpha 为 1，未选中 alpha 为 0.7。
- 未选中 block 的 actor 接触关闭，但仍可通过可见 bounds 被玩家点击选中。
- 从空白区域拖拽框选多个 block，中心点在框内的 block 被追加选中。
- 框选 Selector 使用 sliced size 调整大小，边框表现不应随选区宽高整体变形。
- 框选空区域不会取消已有选择。
- 多点触控时，额外触摸不改变第一个触摸驱动的选择结果。
- 已选中 block 被 actor 触碰后，会保持 collider 开启 `contactBreakDelaySeconds` 秒，随后关闭 collider、按 `fadeOutDuration` 渐隐，并在渐隐完成后恢复为可见未选中状态。
- Breaking 中 block 后续被选择/取消选择，不应提前恢复 alpha、打开 collider 或取消恢复流程。
- fail/restart/exit/completion 后没有残留 Selector 或 stale selected state。

## 回滚方式

若 Level62 选择行为导致卡关、输入冲突或平台状态异常，可回滚 `Level62Ctrl.cs`、`Level62BlockItemCtrl.cs`、`SpriteRenderFragilePlatform.cs` 和 `Level_062.prefab` 的 Level62 选择相关改动。该功能不新增存档字段或全局配置，回滚不需要迁移玩家数据。
