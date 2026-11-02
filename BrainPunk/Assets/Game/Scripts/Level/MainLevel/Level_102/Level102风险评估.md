# Level102 风险评估

更新时间：2026-09-01

## 评估边界

本关从 Level101 复制，当前评估对象为 `Level_102.prefab` 及其目录下的五个局部脚本。附图属于用户的表现反馈，不作为可执行配置。当前结论区分“源码/Prefab 静态证据”和尚未在 Unity 前台执行的运行验证。

## 风险矩阵

| 等级 | 风险 | 已有处理或证据 | 状态 |
| --- | --- | --- | --- |
| P1 | Level101 与 Level102 两套控制器同时改写锁、箱子或门 | `Level102Ctrl.disableLegacyLevel101Controller` 默认为 `true`，运行时查找并禁用同根 `Level101Ctrl`；Prefab 已序列化该值 | 静态已处理；最终运行时 `uncertain` |
| P1 | 箱子仍可被拖动或被复制的 Level101 箱子逻辑接管 | 箱子脚本初始化时关闭所有同物体 `DragableItem`、固定完整箱子为 Static；控制器同时禁用旧 `Level101BoxDragItem` | 静态已处理；输入优先级待 PlayMode |
| P1 | `Level102Ctrl.boxBreak` 接线或脚本失效，导致箱子接触路径不可达 | 当前 Prefab 已将 `boxBreak` 指向 Box 根上的 `Level102BoxBreakOnActor`（GUID `a438381bb9b34b7aba9e605511463a39`）；仍需 Unity 导入后确认无 Missing Script、引用保持有效 | 静态已接线；PlayMode `uncertain` |
| P1 | Level007 `Box_2` 残骸携带复制执行器，激活后自动跳走 | Level102 使用本关内嵌 `Box_2` 残骸，初始 inactive；序列化对象仅保留 Sprite，运行时只按本关开关补齐 Collider/relay，不含 `ItemJumpOutExecutor` 行为 | 静态已处理；PrefabImporter/运行时 `uncertain` |
| P1 | 角色接触完整箱体或残骸没有收到碰撞 | 脚本实现 `OnCollisionEnter2D/Stay2D` 和 `OnTriggerEnter2D`，按 `ActorCtrl`、Actor 根节点或实际碰撞子节点的 Player 标签和 Actor 层识别；完整箱体首次接触后启用残骸非 Trigger Collider，残骸再次接触进入二次碎裂 | 回调、标签和接管链已静态修复；层矩阵和实际接触 `uncertain` |
| P2 | 残骸 relay 配置导致二次碎裂状态残留 | Level102 prefab 明确开启 `addDebrisColliderIfMissing=true`、`addDebrisRelayAtRuntime=true`；relay 只绑定本关 owner，Reset 同步清除标记并隐藏残骸，避免跨尝试残留 | 当前配置静态通过；PrefabImporter/运行时 `uncertain` |
| P1 | 假 Key/默认触发链绕过本关真钥匙逻辑 | `Level102KeyTrigger` 只接受玩家，控制器默认关闭 Key 子树既有 `OnTrigger2DHandle`，收集后关闭所有 Key Collider 并单次通知 | 静态接线已见；实际拾取时序 `uncertain` |
| P1 | 收集 Key 后门未解锁、解锁动画名不匹配或提前通关 | `unlockAnimationName` 默认 `unlock`；启动前校验 `ActorSpineCtrl`/动画，只有确认播放或销毁链完成后才 `SetDoorGate(true)`；`allowUnlockTimeoutFallback` 默认关闭，异常/超时 fail-closed | 源码和 Prefab 字段静态通过；Spine 资源/Trigger 时序 `uncertain` |
| P1 | 移走锁后不再按 Level101 逻辑重生 | `Level102LockDragItem` 只转发 DragableItem 回调；`Level102Ctrl` 复用渐隐、`lock`、`idle`、原始姿态恢复和流程版本号 | 静态已见；视觉和暂停边界 `uncertain` |
| P1 | 重开时错误弹出提示气泡，或普通气泡与物理气泡重叠 | 控制器监听既有 `GamePlayPanelSwitchTipsEvent`，按全局提示门槛生成物理气泡；事件下一帧清理普通子气泡，Reset/Teardown 销毁活动气泡 | 源码静态已处理；事件订阅顺序和 PlayMode `uncertain` |
| P1 | 气泡被拖动后自动消失或继续生成新气泡 | 首次拖动设置 `bubbleDragged=true` 并切换 PhysicsMode；后续提示事件入口直接返回 | 源码静态通过；实际拖动、碰撞和暂停 `uncertain` |
| P1 | 气泡碰撞体偏移过大，玩家看似未碰到却被挡住/无法垫脚 | Inspector 暴露 `hintBubbleColliderOffset/Size`、视觉自适应开关、等待时长/根缩放门槛和 padding；默认创建非 Trigger BoxCollider2D；实例关闭 Animator 根运动，等 show 动画完成后适配，超时或退化边界保留固定尺寸 | 参数可调；不同分辨率和视觉边界 `uncertain` |
| P1 | 门 Collider/Trigger 在钥匙收集前后状态错误 | `SetDoorGate(false)` 初始关闭 Trigger，解锁完成前保持实体；仅 `DoorUnlocked` 切换为 Trigger，并关闭/重开 Collider 以刷新接触对 | 静态路径已见；角色门碰撞和成功链 `uncertain` |
| P2 | Spine Complete、Tween 或协程在暂停/Reset 后回调旧流程 | `flowVersion`、Complete 解绑、协程停止、Tween Kill、事件重置集中在 `BeginTeardown`；暂停时关闭局部输入 | 静态已见；Unity 事件顺序 `uncertain` |
| P2 | 共享资源改动造成其它关卡回归 | 本轮脚本仅位于 Level102；共享 BoxBroken/Key/DoorLock/HintBubble 只作引用，回滚可删除局部接线 | 当前工作区仍有其它用户改动，不能据此宣称全仓库无回归 |
| P2 | 移动端触控、低帧率或异形屏下拖拽/碰撞手感变化 | 复用现有 `DragableItem` 与 `BaseLevelCtrl` 输入链，没有新增平台分支 | Android/iOS 真机未验证 |
| P1 | Level102 未登记到本地关卡数据或排序，导致正常主线无法加载 | OOXML 静态检查显示 `Luban/Configs/Datas/LevelData.xlsx` 没有 `10102`/`Level_102`，`LevelSort.xlsx` 也没有 `10102` 路径；本轮未猜测 ID、排序、提示文案并未修改共享配置 | 配置归属/产品确认 pending；运行入口 `uncertain` |

## 重点检查清单

1. 在 Unity 重新导入 `Level_102.prefab`，确认无 Missing Script、`Level102Ctrl` 引用不为空，且旧 `Level101Ctrl` 不会在同一帧重新启用。
2. 确认独立残骸对象没有 `ItemJumpOutExecutor`；测试角色从顶部接触完整箱体后残骸 Collider/relay 被启用，再次接触残骸只触发一次二次碎裂并隐藏残骸，随后角色可通过。
3. 观察 Key 收集链：Key 只消失一次，DoorLock 播放完整 `unlock`，门在动画完成前不能结算，完成后标准门 Trigger 只结算一次。
4. 拖走锁、暂停、Reset、Replay，确认锁只按一次流程重生且无残留 Tween/Spine 回调。
5. 等待全局 `GamePlayPanelSwitchTipsEvent` 达到远端时间/失败次数门槛后确认物理气泡出现；重开前后不主动弹泡；拖动气泡后持续存在、可与角色碰撞且不生成第二个泡。
6. 在代表性 Android/iOS 横屏和低帧率下重复上述流程。

## 验证证据与缺口

- 已执行 `git diff --check`，未发现本轮新增尾随空白。
- 已完成 Level102 局部脚本静态读取、Prefab 关键字段回读（包括 `boxBreak` 本关脚本 GUID、内嵌 `Box_2` 状态和两个残骸自动补齐开关）以及 `Level102BoxBreakOnActor` relay 状态修复后的语法/括号检查。
- 已通过 OOXML 只读检查确认本地 `LevelData.xlsx`/`LevelSort.xlsx` 暂无 Level102 登记；远程配置内容未读取，不能据此判断线上入口状态。
- 先前 Unity Roslyn 后台编译为 `exit 0`；该结果只能证明源码语法/类型链可编译，不能证明 PrefabImporter、物理层矩阵、动画资源加载、视觉排序或完整通关。
- 2026-09-01 使用 Unity 6000.0.59f2 自带 Roslyn 对 Level102 五个脚本做增量编译探测，未出现 Level102 诊断；全量响应文件仍报告其它脚本的既有 `CS0172/CS0173`，所以不能宣称全项目编译通过。
- 当前没有最终 Level102 PlayMode、移动端、线上配置或性能日志，因此这些项均为 `uncertain`，不应在发布说明中写成已验收。

## 回滚路径

优先回滚本关 Prefab 上的 Level102 局部组件和序列化引用，再删除 `Assets/Game/Scripts/Level/MainLevel/Level_102/` 下本关新增脚本与文档；恢复 Level101 复制物的原始启用状态。不要回滚共享 `BaseLevelCtrl`、`DragableItem`、HintBubble、Key/DoorLock 预制体或 Level007 碎箱资源。若 Unity 已生成对应 `.meta` 或 Library 缓存，应由 Unity 重新导入，不能用缓存状态替代 Prefab 回滚确认。

## 2026-09-02 当前需求修正

- 当前有效配置覆盖早期记录：`Level102BoxBreakOnActor.debrisInteractWithActor=false`。完整箱子保留 Prefab 的 Dynamic 物理并关闭 `DragableItem`，角色首次从顶部接触时播放碎裂反馈；残骸仅显示，不创建/启用 Collider 或 relay，也不会再次触发爆炸。
- 当前有效提示对象是 `GamePlayPanel` 的 `warningBubble`，不是角色头顶的通用 `HintBubble`。`Level102Ctrl` 在运行态关闭后者，并通过 `GamePlayPanelSwitchTipsEvent` 复用全局 `showTipsTimeLimit` / `showTipsFailCount` 门槛；重开不主动弹出，未拖动时按 `DisplayDuration` 自动隐藏，首次拖动后保留。
- `warningBubble` 的实体 Collider 默认按根节点及所有 UI 子节点矩形拟合，`fitWarningBubbleColliderToVisual` 与 `warningBubbleColliderPadding` 可调；拟合失败时回退到根 RectTransform 尺寸。实例层设置为 `Stuff` 以匹配角色地面检测。
- 以上结论基于 `BaseLevelCtrl.cs`、`GamePlayPanelTipsSwitcher.cs`、`Level102Ctrl.cs`、`Level102BoxBreakOnActor.cs` 和 `Level_102.prefab` 静态回读。由于当前环境没有 Unity Editor/PlayMode，PrefabImporter、WorldSpace UI 尺寸、角色实际站立/拖动、箱子推动和移动端触控均为 `uncertain`。
- 本修正的回滚边界：恢复上述局部脚本和 `Level_102.prefab` 的 warningBubble 拟合字段/接线；保留共享 `warningBubble` 资源和其它关卡行为不变。AGENTS 指定的 `specs/037-remote-resource-loading/plan.md` 当前缺失，相关计划依据标记为 `uncertain`。

## 2026-09-02 证据补充

### 已确认（静态）

- `Level102Ctrl` 的 DoorLock 解锁路径已拆分 Spine 动画完成与执行器生命周期：先取得并监听实际 `unlock` `TrackEntry`，再按配置调用 `BaseItemExecutor.Execute()`；门仅在动画完成且锁对象生命周期收尾后开放。
- watchdog 时长按 `TrackEntry.TimeScale * AnimationState.TimeScale` 修正，并附加 `0.25s` 执行器缓冲；Prefab 中 `invokeDoorLockExecutor=0`、`allowUnlockTimeoutFallback=0`，默认异常路径不会绕过真实钥匙校验。
- 提示事件暂停竞态已静态修复：Start 前事件进入 pending，只有局部输入开放后才消费；暂停期间即使收到事件也会安排下一帧清理 BaseLevelCtrl 的普通气泡，不会因 `CanAcceptGameplayInput` 直接返回而残留重复实例。Teardown 清空 pending、停止协程并销毁全部本关范围内的提示气泡，已拖动气泡则保留其物理实例并拒绝后续替换。
- 已回读 `Level_102.prefab` 的局部 GUID/fileID 接线并执行 Level102 源码结构检查与 `git diff --check`；未发现本轮新增尾随空白。

### 未确认（`uncertain`）

- 当前机器没有 Unity Editor/PlayMode；PrefabImporter、Missing Script、Spine 资源实际完成回调、Key 拾取后门 Trigger 时序、锁重生和气泡碰撞/拖动仍需在 Unity 中验证。
- 提示事件的实际订阅顺序、暂停面板期间 Unity 帧推进方式及 BaseLevelCtrl 在目标构建中的气泡销毁时序尚未在 PlayMode 复现，仍标记为 `uncertain`；静态竞态保护不能替代该验证。
- 之前的 Roslyn 增量探测未报告 Level102 诊断，但全量工程仍有工作区其它脚本的既有 `CS0172/CS0173`，不能据此宣称全项目编译通过。

### 回滚边界

若验证发现问题，优先撤销 `Level102Ctrl.cs` 与 `Level_102.prefab` 的本关局部改动；保留共享 DoorLock、Key、HintBubble、BoxBroken 和 Level101 资源不变。

## 2026-09-02 本次需求风险更新

| 等级 | 风险 | 处理与证据 | 状态 |
| --- | --- | --- | --- |
| P1 | 残骸跟随箱子完整姿态，导致高度随推动位置漂移 | `alignDebrisToBox=false` 时只覆盖残骸位置 X，Y/Z、旋转、缩放取首次场景快照；Prefab 已保持关闭 | 源码/Prefab 静态已处理；实际画面 `uncertain` |
| P1 | Key 收集后原地消失，未复用项目标准飞锁表现 | Prefab 将 `useLocalKeyPickupFallback` 设为 `0`；控制器在执行标准 `TriggerExecutor` 前把 `ItemJumpToListener` 目标绑定到 `door_0/LockPoint` | 静态已处理；标准链运行时 `uncertain` |
| P1 | warningBubble 位置漂移或视觉可见处无法点中 | 共享面板对 RectTransform 只恢复 anchored XY；Level102 拖拽适配器按根及可见 UI 子矩形命中，并用 World Space Canvas 平面换算 | 源码静态已处理；EventSystem/多分辨率拖动 `uncertain` |
| P1 | 气泡自动隐藏突然消失，或重试时 Tween 残留 | 新增独立回缩 Tween；显示/隐藏 Tween 分离，Teardown/Reset 立即 Kill 并隐藏 | 源码静态已处理；动画时序 `uncertain` |
| P1 | 全局提示事件只在首次控制器实例生效 | 角色头顶气泡在多生命周期入口清理；Teardown 清回 `hasStartedPlayLifecycle`，pending 只在绑定面板成功后消费，下一次尝试仍复用全局门槛 | 源码静态已处理；实际重试事件顺序 `uncertain` |
| P1 | DoorLock SortingGroup 覆盖整扇门，演出像出现前置门 | 新增 `doorLockSortingOrder`，默认/Prefab 为 101，仅高于门体 100 一层 | 静态配置已处理；最终视觉遮挡 `uncertain` |

### 本次静态验证

- 回读 `Level_102.prefab`：`useLocalKeyPickupFallback=0`、`doorLockSortingOrder=101`、SortingGroup order 为 101、`warningBubbleCollapseDuration=0.2`、`warningBubbleCollapseEase=26`、`alignDebrisToBox=0`。
- 回读 Key 预制体确认标准 `TriggerExecutor` 与 `ItemJumpToListener` 存在，且 `ItemJumpToListener.SetEndTarget(Transform)` 可用；回读门预制体确认 `LockPoint` 节点存在。
- 对 Level102 控制器、箱子、气泡拖拽适配器、共享拖拽基类和 Tips 切换器执行源码结构检查，并执行 `git diff --check`。当前没有 Unity Editor/PlayMode，PrefabImporter、Spine/Key 真实播放、UI 拖动与多次重试仍为 `uncertain`。

### 回滚

恢复上述五个脚本与 `Level_102.prefab` 的本次字段/逻辑即可；不回滚共享 Key、DoorLock、warningBubble 资源。若只回滚钥匙表现，将 `useLocalKeyPickupFallback` 恢复为 `1` 并移除 `ConfigureKeyPickupTarget()` 调用即可。

## 2026-09-02 物理层与触发竞态补充

| 等级 | 风险 | 处理与证据 | 状态 |
| --- | --- | --- | --- |
| P1 | 物理气泡 Collider 留在 Default，角色 `whatIsGround` 无法把它当作垫脚物 | `Level102Ctrl` 新增可编辑 `hintBubblePhysicsLayer`，默认 bit 256（`Stuff`）；运行时同步根节点及所有子 Collider 层，零值自动回退 `Stuff`；共享 `HintBubble.prefab` 保持不变 | 源码/Prefab 静态已处理；Unity 物理接触 `uncertain` |
| P1 | 角色在关卡初始化时已与 Key 重叠，只有 `OnTriggerEnter2D` 时可能错过收集 | `Level102KeyTrigger.OnTriggerStay2D` 与 Enter 共用 `TryHandlePlayerContact`，仅在 `Level102Ctrl.CanAcceptGameplayInput` 开放后锁存 | 源码静态已处理；实际物理回调时序 `uncertain` |
| P1 | 全局提示事件在暂停/结算竞态帧丢失物理气泡或留下重复实例 | `GamePlayPanelSwitchTipsEvent` 的 `true` 分支在不可交互窗口缓存 pending，恢复后消费；`false` 分支清理未拖动实例，已拖动实例保留 | 源码静态已处理；事件订阅顺序和 PlayMode `uncertain` |

### 本轮验证与缺口

- 已回读 `Level_102.prefab` 中 `hintBubblePhysicsLayer.m_Bits=256` 及新增方法引用，并执行目标脚本结构/括号检查。
- 当前机器未运行 Unity PrefabImporter/PlayMode；气泡碰撞、角色地面检测、Key 重叠触发、移动端触控仍标记为 `uncertain`。
- 回滚时只移除 `Level102Ctrl` 的气泡层字段/方法及 `Level102KeyTrigger` 的 `OnTriggerStay2D`，不修改共享 `HintBubble` 或 Key 预制体。

## 2026-09-02 全局提示配置接管

- 物理气泡现在复用 `PlayCtrl_Playing` 已实现的 `showTipsTimeLimit` / `showTipsFailCount` 触发门槛，通过 `GamePlayPanelSwitchTipsEvent` 接收结果；不读取或改写远端配置，也不依赖 `LevelData.isGiveHints`。
- Prefab 新增可调的 `globalTipsBubbleDescription` 和 `globalTipsBubbleDisappearTime`，默认分别为 `U_Tips_Pop_Des_01` 与 `3s`，用于对齐顶部提示的文案和展示时长。
- 顶部 UI 提示与物理气泡是否需要互斥、远端配置下发值和实际 Unity PlayMode 触发时序尚未验证，均标记为 `uncertain`。

## 2026-09-02 重试姿态与拖拽输入修正

| 等级 | 风险 | 处理与证据 | 状态 |
| --- | --- | --- | --- |
| P1 | GamePlayPanel 跨重试复用时沿用上次拖动后的气泡位置/旋转 | `GamePlayPanelTipsSwitcher` 持久化 authored local pose；Level102 重新接管前调用 `ResetTargetPose()`，随后才捕获本关基线 | 源码静态已处理；Unity 重试视觉仍 `uncertain` |
| P1 | 拖动物理气泡释放时触发父级 Tips 按钮，气泡被隐藏或无法继续使用 | `Level102HintBubbleDragItem` 在有效拖动期间设置父 `CustomButton.IsBlockClick=true`，下一帧恢复原值；组件禁用时也恢复 | 源码静态已处理；UI EventSystem 顺序和实际拖动仍 `uncertain` |

本次修正未改变全局 Tips 门槛、共享气泡资源或其它关卡。由于没有 Unity Editor/PlayMode，首次展开动画、拖动命中、父按钮点击抑制和多次重试仍需运行验证。

## 2026-09-02 首次接管与 authored 姿态补充

| 等级 | 风险 | 处理与证据 | 状态 |
| --- | --- | --- | --- |
| P1 | GamePlayPanel 默认 Tips 流程先于 Level102 接管，首次气泡无本关展开动画或未绑定拖拽 | `Level102Ctrl.Start()` 提前调用 `TryBindWarningBubble()`，先启用 external control，再由全局提示事件进入 `ShowWarningBubble()` | 源码静态已处理；Unity 首次显示时序仍 `uncertain` |
| P1 | `DragableItem` 通过世界坐标拖动后，重试残留 warningBubble 的本地位置/Z 或旋转 | `GamePlayPanelTipsSwitcher` 持久化 authored pose；`ResetTargetPose()` 同时恢复 RectTransform 的 anchored/local position、旋转和缩放 | 源码静态已处理；跨重试视觉仍 `uncertain` |
| P1 | 面板入场时 warningBubble 子 Image 尚未展开，Collider 与可视区域错位导致无法点中 | Level102 在气泡展开完成后最多等待 `1s`，重试按所有 UI 子 RectTransform 的世界边界拟合；失败时保留根节点固定尺寸 | 源码静态已处理；UI 渲染尺寸和拖拽命中仍 `uncertain` |
| P1 | 残骸激活时沿用上次运行时位置，导致高度偏离场景初始摆放 | `Level102BoxBreakOnActor.ActivateDebris()` 在 `alignDebrisToBox=false` 分支每次恢复 `debrisInitialPosition`、旋转和缩放；Prefab 保持 `alignDebrisToBox: 0` | 源码/Prefab 静态已处理；残骸最终画面仍 `uncertain` |

本轮未改全局 Tips 门槛、共享 warningBubble 资源或其它关卡。当前环境没有 Unity Editor/PlayMode，首次展开、拖拽命中、重试姿态和残骸排序仍需手动验证。

## 2026-09-02 面板异步创建补偿

| 等级 | 风险 | 处理与证据 | 状态 |
| --- | --- | --- | --- |
| P1 | 全局 Tips 事件先于 GamePlayPanel/warningBubble 就绪，唯一一次事件被消费后气泡永不出现 | `ConsumePendingHintBubble()` 先验证 `TryBindWarningBubble()`，绑定失败保留 pending；`ReplaceHintBubbleNextFrame()` 失败时重新缓存文案和展示时长，后续 Update 在面板就绪后补偿显示 | 源码静态已处理；异步创建时序、首次动画和拖拽命中仍 `uncertain` |

本轮验证：Level102 四个脚本括号结构均平衡，65 个序列化字段均能在 `Level_102.prefab` 找到，关键气泡/残骸配置和 `git diff --check` 已回读。未安装 Unity Editor，不能宣称 PrefabImporter、UI EventSystem 或 PlayMode 通过。回滚边界仅为 `Level102Ctrl.cs` pending 分支，保留共享提示面板和资源不变。

## 2026-09-02 标准 Key Executor 目标补全

| 等级 | 风险 | 处理与证据 | 状态 |
| --- | --- | --- | --- |
| P1 | Key 标准执行器停在空目标 `SwitchComponentListener`，后续 `ItemJumpToListener` 不执行，钥匙看起来像原地消失 | `SwitchComponentListener` 新增 `HasTarget/SetTarget`；`Level102Ctrl.ConfigureKeyPickupTarget()` 在执行标准链前把空目标步骤绑定到当前门 Collider，并绑定 `LockPoint` 飞行终点 | 源码静态已处理；Unity 实际执行器时序 `uncertain` |

本次未修改共享 Key 预制体中的序列化目标，避免影响其它关卡；Level102 运行时接线与其它关卡 Prefab 覆写结果保持一致。若运行时仍无飞行表现，应优先检查该关卡实例是否仍引用标准 `Key.prefab` 及 Unity 导入后的 `TriggerExecutor.items`。

## 2026-09-02 当前需求修正（覆盖同日旧表述）

| 等级 | 风险 | 处理与证据 | 状态 |
| --- | --- | --- | --- |
| P1 | 残骸高度随箱子推动位置漂移 | `ActivateDebris()` 在 `alignDebrisToBox=false` 时只覆盖当前箱子世界 X，Y/Z、旋转和缩放恢复残骸初始快照；默认关闭残骸与角色交互 | 源码/Prefab 静态通过；最终画面 `uncertain` |
| P1 | DoorLock 排序组把门体覆盖成额外前置门 | 已移除 Level102 实例额外添加的 SortingGroup 和对应控制器字段，恢复标准 DoorLock 资源排序 | 静态通过；最终遮挡 `uncertain` |
| P1 | warningBubble 视觉位置与命中区错位、无法拖动 | 新增 `warningBubbleInteractionRect`，运行时绑定真实 `Image` 子节点；`Level102HintBubbleDragItem` 只按该矩形命中，并将屏幕点投影到 Canvas 平面；Collider 按同一组矩形拟合 | 源码静态通过；World Space/UI EventSystem/多分辨率拖动 `uncertain` |
| P1 | 提示气泡只出现一次或自动隐藏无动画 | 保留 `GamePlayPanelSwitchTipsEvent` 全局门槛；pending 在面板未就绪时不消费，Teardown 清理尝试状态；未拖动实例使用独立回缩 Tween，重试前恢复 authored 姿态 | 源码静态通过；首次提示、重试事件顺序 `uncertain` |
| P1 | Key 拾取后原地消失，未复用飞锁表现 | 默认执行当前 Key 实例标准 `TriggerExecutor`，绑定空目标 Switch 到门 Collider、Jump 终点到 LockPoint；执行器缺失只报警并保留可见钥匙 | 源码/标准 Prefab 静态通过；真实飞行/销毁/解锁时序 `uncertain` |

本轮只修改 Level102 局部控制器、拖拽适配器和关卡 Prefab 接线，未修改共享 Key、DoorLock、warningBubble 资源。已执行 `git diff --check` 与源码结构检查；没有 Unity Editor/PlayMode，因此 PrefabImporter、Spine/Key 实际演出、World Space UI 坐标、拖拽和多次重试仍不得视为已验收。AGENTS 指定的 `specs/037-remote-resource-loading/plan.md` 当前不存在，计划依据为 `uncertain`。

回滚时恢复 `Level102Ctrl.cs`、`Level102HintBubbleDragItem.cs` 与 `Level_102.prefab` 的本次局部字段/逻辑即可；保留共享资源和其它关卡改动不变。

## 2026-09-02 最终时序与持久面板风险补充

| 等级 | 风险 | 处理与证据 | 状态 |
| --- | --- | --- | --- |
| P1 | `Unlocking` 阶段同时显示原门、标准门和 DoorLock，造成前景多出一扇门 | `ApplyDoorVisualState()` 使用三态互斥：锁定门仅用于 `LockedIdle/Relocking`，标准门仅用于 `DoorUnlocked`，DoorLock 仅由解锁链激活 | 源码静态通过；最终渲染 `uncertain` |
| P1 | 钥匙执行器启动后原地渐隐，或尚未飞到锁就开始解锁 | Prefab 显式绑定 `ItemJumpToListener.endTarget=door_0/LockPoint`，控制器只在 `IsDone` 后调用 `BeginUnlockSequence()` | 源码/Prefab 静态通过；实际轨迹 `uncertain` |
| P1 | GamePlayPanel 跨重试保留，旧控制器销毁气泡动态组件后，新控制器在帧末失去拖拽/物理能力 | Teardown 只禁用并复用组件；下一次绑定先写入新控制器，再启用 Collider/Rigidbody/拖拽，并恢复 authored pose | 源码静态通过；连续重试 `uncertain` |
| P1 | warningBubble 只在第一次尝试出现 | 继续使用公共 `GamePlayPanelSwitchTipsEvent`；`ResetDataPreservingRevive()` 每次重试重置 `HasClickTips` 和尝试计时，Level102 不另设一次性门禁 | 公共状态机静态通过；远端门槛与 PlayMode `uncertain` |
| P2 | 文档旧记录把残骸描述为完全固定位置 | 当前实现只固定 authored Y/Z/旋转/缩放，X 使用箱子碎裂时位置；`debrisInteractWithActor=false` | 源码/Prefab 静态通过；最终画面 `uncertain` |

验证边界：已回读相关脚本、`Level_102.prefab` 的 Key/LockPoint/门视觉引用及公共重试状态重置路径，并执行定向 `git diff --check`。AGENTS 指定的 `specs/037-remote-resource-loading/plan.md` 在工作区不存在，因此相关计划依据保持 `uncertain`。回滚时仅恢复上述 Level102 局部逻辑和共享 Tips 切换器的 external-control 扩展，不修改共享资源 Prefab。

Unity 自带 Roslyn 的当前响应文件探测未报告 Level102 或 `GamePlayPanelTipsSwitcher` 错误；命令整体因其它模块缺失 `IGMFeature`、`IWannerGameLevelCtrl`、`TbIWannerLevelData` 等类型返回失败。该结果只能证明本轮文件没有出现在当前诊断中，不能替代 Unity 正常导入和 PlayMode 验收。

## 2026-09-03 提示循环兼容风险

| 等级 | 风险 | 处理与证据 | 状态 |
| --- | --- | --- | --- |
| P1 | Level102 接管共享 warningBubble 后，普通切换器不会执行自动隐藏回调，导致主线循环状态永久停在展示中 | 本关未拖动气泡完成回缩并 `SetActive(false)` 后显式调用 `NotifyExternalDisplayEnded()`；PlayCtrl 才从该时刻记录下一轮等待基线 | Roslyn 编译与静态调用链通过；PlayMode `uncertain` |
| P1 | 已达到重试门槛后，结束通知使次数条件仍成立并在下一帧立即重弹 | 共享 Playing 逻辑只在当前尝试首次展示前检查 `showTipsFailCount`，后续仅检查从消失时刻累计的 `showTipsTimeLimit` | 源码静态通过；多次重试实机时序 `uncertain` |
| P2 | 已拖动的物理气泡又启动下一轮，生成重复平台 | 拖动会取消自动隐藏；只有自动隐藏路径传入 `notifyDisplayEnded=true`，Teardown、显式清理与保留拖动实例路径不通知 | 源码静态通过；拖动竞态 `uncertain` |

- 验证：Unity 6000.0.59f2 自带 Roslyn 按当前完整 `Assembly-CSharp` 响应文件编译为 `exit 0`、`0 error`；`git diff --check` 通过。普通 solution MSBuild 因既有 Firebase 引用缺失失败，未指向本次脚本。
- 未验证：Unity PlayMode 的第三次尝试立即显示、自动消失后多轮等待、回缩/拖动同帧竞争、暂停恢复、Android/iOS 横屏触控。
- 回滚：删除 Level102 自动隐藏分支传递的 `notifyDisplayEnded` 与共享切换器外部结束调用；不改关卡 Prefab、物理参数、配置或存档。

## 2026-09-03 warningBubble 世界碰撞与重试位置风险

| 等级 | 风险 | 处理与证据 | 状态 |
| --- | --- | --- | --- |
| P1 | 屏幕 UI 上的 Collider 使用 Canvas 坐标，角色在视觉气泡位置没有可站立的平台 | UI 只负责显示和拖拽；`Level102Ctrl` 新建 Stuff 层 Kinematic 世界代理，把气泡可视矩形按“UI 角点 → 屏幕 → GameCamera/角色 Z 平面”投影后写入 BoxCollider2D | 定向 Roslyn 编译通过；PlayMode 接触 `uncertain` |
| P1 | 第三次重试达到失败次数门槛后立即显示，气泡继承 `tips_btn` 入场中的零/中间缩放并发生位置或尺寸偏移 | 显示协程等待父 `CustomButton` 激活且 XY 缩放达到 `0.98`，最长 `2s`，再刷新 Canvas 和展开气泡 | 公共时序与源码静态通过；第三次重试画面 `uncertain` |
| P1 | 拖动、CanvasScaler、安全区或相机移动后 UI 与世界代理错位 | 气泡开放交互期间在 `LateUpdate` 和 `FixedUpdate` 重新投影；隐藏/重置先停用 Rigidbody 模拟和 Collider | 源码静态通过；多分辨率/移动端 `uncertain` |
| P1 | 独立代理跨重试残留，生成重复平台 | Hide/Reset 停用代理，Teardown 销毁根级代理并清空运行时引用；不把代理挂到持久化 GamePlayPanel | 源码清理链静态通过；连续重试 `uncertain` |
| P2 | UI 可视矩形无法投影或世界相机解析失败，代理尺寸无效 | 解析顺序为关卡 `GameCamera`、关卡子 Camera、`Camera.main`；投影失败时保持碰撞关闭，避免在错误坐标生成隐形平台 | 防御分支已实现；异常场景日志/体验 `uncertain` |

- 影响范围：行为代码仅为 `Level102Ctrl.cs`；本轮不修改 Prefab、共享 UI 脚本、物理层矩阵、远端配置和存档结构。
- 验证：Unity 6000.0.59f2 Roslyn 定向编译 `exit 0`、`0 error`，仅有既有 `hintBubblePrefab` 未使用警告；结构检查和差异格式检查通过。前台 Unity/PlayMode、角色落脚、拖拽跟随、第三次重试、横竖安全区和 Android/iOS 仍未验证，统一标记为 `uncertain`。
- 计划缺口：AGENTS 指定的 `specs/037-remote-resource-loading/plan.md` 当前不存在，无法用该计划补充风险约束，状态为 `uncertain`。
- 回滚：恢复 `Level102Ctrl.cs` 中本轮父按钮等待及世界代理逻辑，并删除本节文档；共享 `GamePlayPanel`、warningBubble 资源、`Level_102.prefab` 和其它关卡不需要回滚。

## 2026-09-03 红框碰撞、开锁门视觉与重试清理风险（覆盖同日旧门/碰撞表述）

| 等级 | 风险 | 处理与证据 | 状态 |
| --- | --- | --- | --- |
| P1 | 按整张气泡设置碰撞会让角色踩在红框外的轮廓/尾巴上 | 优先使用 `msg/title` 的 `300×80` RectTransform，仅向上下各扩 20 UI 单位，不扩展左右；Prefab 回读为 `(0,20)` | 静态通过；PlayMode 手感 `uncertain` |
| P1 | 影子被永久关闭，或从侧面/下方接触气泡时误隐藏 | 只在 Collider 接触、水平重叠且脚底贴近气泡顶面时暂时关闭 `Shadow`；记录原 `activeSelf` 并在离开/Hide/Reset 恢复 | 源码链通过；实际物理接触 `uncertain` |
| P1 | `Unlocking` 同时隐藏两扇门，只剩 DoorLock 锁在空中播放 | 状态表改为 `Unlocking/DoorUnlocked` 显示正常门，`LockedIdle/Relocking` 显示锁定门；DoorLock 继续作为前景锁演出 | 静态通过；层叠观感 `uncertain` |
| P1 | Spine 默认混合让锁的 `unlock` 动作与上一动作交叉 | 取得真实 `TrackEntry` 后立即设置 `MixDuration=0` | Roslyn 编译通过；实际 Spine 表现 `uncertain` |
| P1 | 已 Destroy 的 Key 在 Teardown 中抛异常，旧 Level102 事件未退订，后续重试报错并截断黑屏监听 | `StopLocalKeyPickup` 改用 Unity `!= null`；`BeginTeardown` 第一步退订事件。Editor.log 旧栈明确指向该方法 | 根因与源码修正已确认；连续重试 `uncertain` |
| P2 | 为修 Level102 而改动共享事件、UI、DoorLock 或影子系统，影响其它关 | 本轮行为只修改 `Level102Ctrl.cs` 与 `Level_102.prefab`，共享资源保持不变 | 影响边界已收紧 |

- 验证：Unity 6000.0.59f2 Roslyn 定向编译 `exit 0`、`0 error`，仅有既有未使用字段警告；前台 Unity 脚本域已进入 `ReloadAssembly`，最新日志无新增 `error CS`。关键字段和调用链已回读。
- 未验证：前台 PlayMode 的红框精确物理范围、站立/跳离影子、完整开锁画面、第三次及更多次 Reset 的黑屏和 Console，均为 `uncertain`。
- 计划缺口：`specs/037-remote-resource-loading/plan.md` 当前不存在，无法根据该计划补充风险约束，保持 `uncertain`。
- 回滚：仅恢复 `Level102Ctrl.cs` 中红框/影子/门状态/Spine 零混合/Teardown 改动和 `Level_102.prefab` 对应字段；不回滚共享系统。

## 2026-09-03 钥匙收集门替换顺序与对齐风险（覆盖前节开锁可见性表述）

| 等级 | 风险 | 处理与证据 | 状态 |
| --- | --- | --- | --- |
| P1 | 假门先隐藏、正常门/锁后激活，中间出现空门或浮空锁 | 钥匙收集当帧先显示正常门 + DoorLock，保留假门/假锁一个渲染帧，下一帧才退役假表现 | 调用顺序静态通过；PlayMode `uncertain` |
| P1 | 两门 Transform 值相同但 Spine 网格原点/尺寸不同，切换时视觉跳位 | 复用 Level101 的网格边界校准：position `(0.1058,-1.5815,0)`，scale `(1.0283,1.0165,1)` | 源码、Level101/102 Prefab 证据已回读；视觉 `uncertain` |
| P1 | 重叠帧中正常门或正常锁仍在假门下方 | 保留既有正常门内部 order 100 > body 0、DoorLock group 105 > 门根 group 100；运行时再比较 SortingLayer/order，不满足时提升到下层 `order+1` | 静态通过；多相机/特殊材质 `uncertain` |
| P1 | DoorLock 提前显示后自带 Trigger 抢先启动解锁 | 激活后立即重新解析并关闭整个 DoorLock 子树的 `OnTrigger2DHandle`；真实解锁仍只由钥匙到达状态机启动 | 调用链通过；Physics2D 时序 `uncertain` |
| P1 | 重试再激活 DoorLock 时保留上次 `unlock` 末帧或发生动作混合 | 每次出现时显式播放 `lock_idle` 且 `MixDuration=0`；真实 `unlock` 同样为零混合 | Roslyn 编译通过；连续重试 Spine 表现 `uncertain` |
| P2 | 局部拾取兜底在重叠帧内就进入 Unlocking，隐藏假锁时 Kill 渐隐 Tween 使解锁链中断 | 退役协程在 Kill 后检查 `Unlocking && !unlockActivationStarted`，显式接续 `ActivateDoorLock` | 防御分支已实现 |

- 影响边界：仅 `Level102Ctrl.cs`、`Level_102.prefab` 及 Level102/项目记录文档；共享 Level101、door_0、DoorLock、Key、Spine 和事件系统未改动。
- 验证：Unity 6000.0.59f2 当前 `Assembly-CSharp.rsp` 全量 Roslyn 编译 `exit 0`、`0 error`、107 条既有 warning；对齐字段、门/锁排序值、钥匙到门调用链与 Prefab 序列化已静态回读。
- 未验证：PlayMode 实际重叠帧、轮廓像素级对齐、正常锁相对门的位置、钥匙落点、连续重试和移动端。计划文件缺失仍为 `uncertain`。
- 回滚：恢复 `ApplyDoorVisualState` 单状态互斥逻辑，删除替换协程/排序校验/对齐字段与 Prefab 值；共享资源不回滚。

## 2026-09-03 钥匙飞行遮挡与提示次数歧义

| 等级 | 风险 | 处理与证据 | 状态 |
| --- | --- | --- | --- |
| P1 | Key 实例 order 100 低于 DoorLock Renderer order 101，飞行末段被锁遮挡 | 收集后读取当前锁的最高有效 SortingLayer/order，把 Key 根 SortingGroup 临时设置为 `lockOrder+1` | Roslyn 编译通过；逐帧视觉 `uncertain` |
| P1 | 直接修改共享 Key Prefab 影响其它关卡 | 仅修改 Level102 运行时实例；不改 `Key.prefab` / `DoorLock.prefab` | 影响边界已收紧 |
| P2 | 重试后钥匙仍保留飞行高层级 | 首次初始化保存实例原排序，`ResetKeyPickupVisualState()` 恢复 authored SortingLayer/order | 调用链静态通过；连续重试 `uncertain` |
| P1 | 将“第三次进入”误报成“第三次重试”，导致验收预期错一轮 | 明确公共计数是已发生的 Fail + Reset；默认阈值 2 对应第二次重试立即显示 | 源码与本地配置已回读 |
| P1 | 其它发布环境远端阈值为 1 时，第一次重试立即显示，与本地默认不同 | `GetShowTipsFailCount()` 远端优先；当前 Editor.log 已确认值为 2，其它环境通过 `showTipsFailCount: N` 日志核对，不在本关写死 | 当前编辑器已确认；其它环境 `uncertain` |

- 本轮没有调整 `showTipsFailCount` / `showTipsTimeLimit`，也没有启用 LevelData 的普通 HintBubble；只修复 Key 飞行排序并澄清当前计数语义。
- 完整 Assembly-CSharp Roslyn 编译为 `exit 0`、`0 error`、107 条既有 warning。PlayMode 遮挡、多次重试和远端配置仍待验收。
- `specs/037-remote-resource-loading/plan.md` 缺失，计划约束无法核对，标记为 `uncertain`。回滚范围仅为 Level102 Key 运行时排序代码与本节文档。

## 2026-09-03 钥匙渐隐与缩小被提前销毁

| 等级 | 风险 | 处理与证据 | 状态 |
| --- | --- | --- | --- |
| P1 | Level102 飞行 0.5s + 销毁延迟 0.1s，早于缩小 0.8s/渐隐 0.9s 完成，表现被截断 | 删除本关 0.5s 覆写，继承共享 Key 的 1s 飞行；销毁约在飞行后再延迟 0.1s | 静态时序通过；PlayMode `uncertain` |
| P1 | 为修复表现复制一套 Level102 Tween，和共享 Key 参数漂移 | 继续使用共享 TriggerExecutor 及原 Scale/Fade/Jump/Destroy 监听器，只修正 Prefab 覆写 | 影响边界已收紧 |
| P2 | 延长飞行导致开锁比旧版本晚约 0.5s | `WaitForKeyArrival()` 明确等待标准 Jump 完成后解锁；延长属于恢复共享完整拾取演出 | 设计时序一致；手感 `uncertain` |
| P2 | 恢复飞行时间后钥匙在锁下方的时间更长 | 上一轮 `EnsureFlyingKeyAboveDoorLock()` 仍在飞行前提层，重试恢复 authored 排序 | Roslyn 编译通过；视觉 `uncertain` |

- 修改只涉及 `Assets/Game/Prefabs/Level/Level_102.prefab` 和文档；共享 `Key.prefab`、监听器源码及其它关卡未改。
- 验证确认 Level102 已无 Jump duration 覆写，实际继承 `1s`；完整 Assembly-CSharp 编译 `exit 0`、`0 error`。修复后的逐帧表现仍需 PlayMode 验收。
- 计划文件仍缺失，标记为 `uncertain`。回滚时恢复 Level102 Key 的 `duration=0.5` Prefab 覆写即可。

## 2026-09-03 Level002 参数对齐风险（覆盖上一节 1 秒方案）

| 等级 | 风险 | 处理与证据 | 状态 |
| --- | --- | --- | --- |
| P1 | 单纯把 Level102 飞行延长到 1 秒，与 Level002 的 0.5 秒节奏不同，观感拖沓 | 恢复 0.5 秒；缩小 0.8 秒和渐隐 0.4+0.5 秒继续使用共享参数 | 参数已静态回读；PlayMode `uncertain` |
| P1 | 恢复 0.5 秒后再次在约 0.6 秒提前销毁，截断 Tween | 仿照 Level002 在 Destroy 前增加阻塞项；等待本关 `IsDoorUnlocked` 后才推进 Destroy | 执行器索引与组件引用已回读 |
| P1 | 直接把 Level002 DoorLock Executor 接入 Level102，造成控制器和执行器重复解锁 | 使用 Level102 私有只读等待节点，不主动播放动画；DoorLock 仍只由 `Level102Ctrl` 驱动 | 职责边界明确 |
| P2 | DoorLock 动画异常不完成时，Key 根对象不销毁 | Key 的缩小/渐隐仍会独立完成，视觉会消失；等待不伪造解锁成功，关卡退出时随 Level 根统一销毁 | 失败安全优先；异常路径 `uncertain` |
| P2 | 新增 Prefab 组件 fileID/GUID 接线错误导致导入失败 | 175 个定义全部唯一；等待节点、Destroy stripped 引用、数组索引和脚本 GUID 均通过静态断言 | 静态通过；PrefabImporter `uncertain` |

- 行为边界只涉及 Level102 Prefab、私有等待节点和文档。完整源码 Roslyn 编译 `exit 0`、`0 error`；未修改 Level002 或共享 Key/DoorLock。
- PlayMode 的最终观感、Spine 完成回调和移动端帧率仍需验收，计划文件缺失仍为 `uncertain`。
- 回滚为删除私有等待节点及 Prefab 的 duration/数组/新增组件覆写，恢复继承共享 1 秒 Jump 的上一状态。

## 2026-09-03 正常门漩涡缺失与对齐风险

| 等级 | 风险 | 处理与证据 | 状态 |
| --- | --- | --- | --- |
| P1 | 正常门打开后门框内部为空，缺少标准过关门的漩涡 | 移除 Level102 对 `door_0/effect` 的永久隐藏；与正常 `door` 共用显示条件 | 共享 Prefab、Level002 和 Level101 基线已回读；PlayMode `uncertain` |
| P1 | 只开启 effect，但仍使用其源局部姿态，导致漩涡与已校准的门框错位 | `ApplyNormalDoorAlignment()` 同时对 `door`、`effect` 应用同一位置和缩放 | 源码静态通过；像素级观感 `uncertain` |
| P2 | 重试后漩涡残留在假门中 | 初始/Reset 将 `replacementDoorVisible=false`、状态还原为 LockedIdle，再由同一显示函数同时隐藏正常门和 effect | 重置调用链已回读；连续重试 `uncertain` |
| P2 | 修改共享 `door_0.prefab` 影响其它关卡 | 不修改共享资源，只调整 Level102 私有控制器对现有引用的状态管理 | 影响范围已收紧 |

- 当前完整 Assembly-CSharp Roslyn 编译为 `exit 0`、`0 error`、107 条既有 warning；未运行前台 Unity PlayMode。
- `specs/037-remote-resource-loading/plan.md` 仍缺失，计划约束核对为 `uncertain`。回滚只涉及 Level102 私有控制器的 effect 显示和对齐两处逻辑。

## 2026-09-09：已获提示后的气泡可用性

- 原行为基线与影响范围：公共 HasTips 门禁会停止 Level102 物理气泡；本次仅修改 Level102Ctrl.cs，复用既有配置读取、运行态时间、pending 显示、物理代理和隐藏通知。
- P1：已获提示后无法取得垫脚物。增加本关补偿，保留答案资格，继续按原时间/重试门槛弹出。
- P2：重复弹出或回缩中抢先显示。通过 bubbleDragged、warningBubbleWasShown、pending、协程和回缩 Tween 门禁阻止；首次拖动后继续保留气泡。
- P2：暂停或重建期间误弹。复用 CanAcceptGameplayInput 和 IsAttemptTransitioning 门禁；不新增计时器或事件订阅，仍使用既有生命周期清理。
- 验证证据：当前完整 Assembly-CSharp.rsp 的 Unity Roslyn 编译退出码 0、0 error；公共时间/重试条件与隐藏完成计时链静态核对通过。
- 未验证缺口（uncertain）：PlayMode 下已获提示后多轮弹出、重试、暂停、拖动物理接触和 Android/iOS 横屏体验；CodeGraph 命令不可用及引用 plan 缺失。
- 回滚路径：仅移除 Level102Ctrl 新增的补偿方法、调用与配置缓存；无资源、存档迁移。无待确认产品参数，门槛沿用现有配置。
