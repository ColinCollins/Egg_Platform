# Level60 梦境切换功能说明

## 2026-06-09 DreamActor bubble alpha update

- `Level60Ctrl.SetBubbleAlpha(float)` now also applies the current bubble alpha to `DreamActor` through `dreamActor.SpineCtrl.SetAlpha(...)`.
- `CacheBubbleVisuals()` still excludes `ActorCtrl` child visuals from the cached bubble renderer arrays, so bubble decoration renderers and DreamActor spine alpha are controlled separately.
- Initial hide, interrupted fade-out, fade-in, and completed switch all pass through `SetBubbleAlpha`, so DreamActor starts hidden, fades with `dreamBubble`, and remains visible when the dream switch completes.
- Unity PlayMode still needs to verify DreamActor spine alpha, shadow/auxiliary actor visuals, restart/fail/exit cleanup, and Android/iOS touch reset behavior.

更新时间：2026-06-04

## 现状说明

- 当前关卡脚本为 `Level60Ctrl.cs`。
- 当前关卡 prefab 为 `Assets/Game/Prefabs/Level/Level_060.prefab`。
- 本关卡行为来自 `Level60Ctrl.cs` 顶部注释：玩家停止操作一段时间后显示梦境气泡，气泡显示完成后从 `FakeActor` 切换到 `DreamActor`。
- 当前实现保持关卡局部逻辑，不修改 `BaseLevelCtrl`、`ActorCtrl` 或全局输入系统。

## 脚本职责

### Level60Ctrl

- 解析并缓存 `BaseLevelCtrl`、`FakeActor`、`DreamActor` 和 `dreamBubble`。
- 初始化时把 `BaseLevelCtrl` 当前 actor 设置为 `FakeActor`。
- 初始化时恢复 `FakeActor` 移动，停止 `DreamActor` 移动。
- 监听 `PlayerRightMoveEvent`、`PlayerLeftMoveEvent`、`PlayerJumpEvent` 和屏幕触摸。
- 在梦境切换完成前，用玩家输入或触摸重置等待计时。
- 等待时间达到 `idleWaitDuration` 后，让 `dreamBubble` 视觉淡入。
- 淡入期间如果玩家再次操作，快速淡出 `dreamBubble` 并重新等待。
- 淡入完成后停止 `FakeActor`，恢复 `DreamActor`，并调用 `BaseLevelCtrl.SetActor(dreamActor)`。
- 禁用或销毁时清理事件订阅和 tween。

## 当前流程

1. `Awake()` 解析引用，缓存气泡视觉组件，并把气泡透明度置为 0。
2. `OnEnable()` 订阅移动和跳跃事件。
3. `Start()` 应用初始状态，并在下一帧再次应用一次，覆盖 `BaseLevelCtrl.Start()` 的恢复移动影响。
4. `Update()` 在切换完成前累计无操作等待时间。
5. 等待时间达到 5 秒后启动 2 秒淡入。
6. 淡入完成前如果收到左右移动、跳跃事件或屏幕触摸：
   - 等待时间归零。
   - 当前气泡快速淡出。
   - 仍保持 `FakeActor` 可控。
7. 淡入完成后：
   - `FakeActor` 清输入并停止移动。
   - `DreamActor` 恢复移动并清输入。
   - `BaseLevelCtrl` 当前 actor 切换为 `DreamActor`。
8. 切换完成后，后续玩家输入不再重启梦境气泡流程。

## Inspector 配置

- `baseLevelCtrl`：当前关卡的 `BaseLevelCtrl`。
- `dreamActor`：梦境切换完成后受控的 actor。
- `fakeActor`：关卡初始受控 actor。
- `dreamBubble`：梦境气泡对象。
- `idleWaitDuration`：无操作等待时间，当前为 5 秒。
- `bubbleFadeInDuration`：气泡淡入时间，当前为 2 秒。
- `bubbleFadeOutDuration`：中断后的快速淡出时间，当前为 0.15 秒。

## 引用兜底规则

- 如果 Inspector 引用为空，`Level60Ctrl` 会在关卡根节点下按名字查找 `FakeActor`、`DreamActor` 和 `dreamBubble`。
- 按名字查找失败时，才会使用 `BaseLevelCtrl.Actor` 作为兜底。
- 正式 prefab 仍建议显式绑定引用，名字查找只作为防御性兜底。

## 视觉控制规则

- 当前实现不会整体关闭 `dreamBubble` 对象，只控制已缓存视觉组件透明度。
- 当前实现会缓存 `dreamBubble` 下的 `SpriteRenderer`、`SkeletonRenderer` 和 `SkeletonGraphic`。
- 当前实现会排除所有属于 `ActorCtrl` 子层级的视觉组件，避免 `DreamActor` 被误隐藏或误透明。

## 触摸重置规则

- `Level60Ctrl` 使用 `Input.touchCount > 0` 判断是否存在屏幕触摸。
- 只要触摸持续存在，等待计时就会被压回 0，不会进入气泡淡入。
- 触摸刚出现时，如果气泡已经部分显示或正在淡入，会触发一次快速淡出。
- 持续触摸期间不会每帧重复创建淡出 tween。

## 待确认

- `dreamBubble` 的“包含其子对象渐隐渐显”是否应该包含 `DreamActor` 视觉。当前代码选择排除 Actor 视觉以降低误伤风险；如果确认要包含 `DreamActor`，需要调整视觉缓存过滤规则，并补充 Unity 运行验证。
