# Level102 功能说明

更新时间：2026-09-01

## 需求背景

Level102 从 Level101 复制场景和大部分误导点，但玩法改为“接触碎箱、收集真实钥匙、解锁门”。用户提供的截图仅作为视觉参考；本记录以当前工作区源码和 `Level_102.prefab` 的序列化接线为准。当前场景资源已建立，但主线配置是否已登记仍需单独确认。

## 影响范围

| 范围 | 文件/资源 | 作用 |
| --- | --- | --- |
| 本关控制器 | `Level102Ctrl.cs` | 锁重生、钥匙收集、DoorLock Spine 解锁、提示气泡接管、重置与生命周期 |
| 本关输入适配 | `Level102LockDragItem.cs`、`Level102HintBubbleDragItem.cs`、`Level102KeyTrigger.cs` | 分别转发锁/气泡拖拽和真实钥匙碰撞 |
| 本关物理 | `Level102BoxBreakOnActor.cs` | 禁止箱子拖动，角色踩到完整箱体时碎裂并显示只读残骸 |
| 场景 | `Assets/Game/Prefabs/Level/Level_102.prefab` | Level101 复制基线、局部组件、对象引用和初始状态 |
| 只复用资源 | Level007 `BoxBroken`/`crashBroke`、Key 预制体、DoorLock Spine、既有 `HintBubble`、`BaseLevelCtrl`/`DragableItem` | 碎裂反馈、标准门通关与拖拽链；Key 的局部拾取兜底避免复制实例中空目标执行器阻塞 |

本轮局部实现不要求修改共享 `BaseLevelCtrl`、`DragableItem`、门预制体、Key 预制体或 Level101 脚本。Prefab 已序列化 `disableLegacyLevel101Controller=1`，运行时会禁用复制残留的 `Level101Ctrl`，避免两套状态机同时写入状态。

## Level101 基线与本关变化

Level101 的锁拖走后会渐隐、播放门体 `lock` Spine 动画并恢复锁；Level102 保留该逻辑。Level102 不再依赖 Level101 的箱子拖拽/砸门通关链，箱子改为角色接触碎裂，钥匙成为真实过关前置，门最终仍交给既有 `BaseLevelCtrl` 的 Trigger 成功链。Key 预制体的默认触发器在本关由 `Level102KeyTrigger` 接管；局部拾取演出默认绕过可能为空目标的共享执行器链。

## 玩法流程

1. **初始态**：门锁可拖；门入口保持未解锁状态；真实 Key 可碰撞；箱子为不可拖动的静态实体；提示气泡不由重开流程主动弹出。
2. **箱子接触**：`Level102BoxBreakOnActor` 识别 `ActorCtrl`/`Player` 后隐藏完整箱体，播放 Level007 `BoxBroken` 粒子和 `crashBroke` 音效，并显示 `Box_2` 残骸。角色识别同时兼容 `Player` 标签位于 `ActorCtrl` 根节点或实际碰撞子节点的 Prefab 结构，并继续校验 Actor 层和存活状态。完整箱子要求角色从顶部踩到（`requireTopContact=true`）；当前 Prefab 的 `debrisInteractWithActor=false`，残骸只作为视觉残留，运行时不会补齐或启用角色碰撞/relay，因此角色经过残骸不会再次爆炸，也不会被残骸阻挡。
3. **锁误导**：玩家拖走可见锁后复用 Level101 的渐隐 + `lock` Spine + `idle` 恢复流程；移动走的锁会再次回到初始姿态。钥匙收集后不再开放该图片锁拖拽。
4. **钥匙与解锁**：角色进入真实 Key 的本关触发器后只计数一次，关闭 Key 碰撞并播放局部拾取演出（可在 Inspector 关闭兜底以调用共享执行器）。随后激活 `DoorLock`，先验证 `ActorSpineCtrl` 和 `unlock` 动画存在，再由其执行器播放 Spine 动画；关闭执行器时则由本关直接播放指定动画。只有确认动画/锁对象生命周期完成后才将门 Collider 切为 Trigger；超时强制开放由 `allowUnlockTimeoutFallback` 显式控制，Prefab 默认关闭，异常配置保持门锁定。
5. **提示气泡**：控制器监听 `GamePlayPanelSwitchTipsEvent`，因此物理气泡与 `PlayCtrl_Playing` 使用同一组全局 `showTipsTimeLimit` / `showTipsFailCount` 门槛，不受关卡数据 `isGiveHints` 控制。显示时默认使用 `U_Tips_Pop_Des_01`，未拖动时按 `globalTipsBubbleDisappearTime=3s` 自动关闭。事件到达后销毁 BaseLevelCtrl 同帧创建的普通气泡，生成一个带非 Trigger Collider、Kinematic Rigidbody2D 和 `Level102HintBubbleDragItem` 的气泡。Level102 实例关闭气泡 Animator 根运动，先使用 Inspector 固定碰撞体，等待 `show` 动画完成且根缩放达到 `0.98`（默认最多等待 `1s`）后再按可视边界适配；适配失败时继续保留固定尺寸。气泡可与玩家碰撞并可拖动；玩家首次开始拖动后锁存 `bubbleDragged`，气泡不再自动消失，后续提示事件也不再生成新气泡。

## 局部状态与边界

| 状态/标记 | 关键规则 |
| --- | --- |
| `LockedIdle` | 图片锁可拖；Key 尚未收集；门入口关闭 |
| `Relocking` | 锁输入关闭，等待 Spine `lock` 完成或兜底时长后恢复初态 |
| `Unlocking` | Key 已收集，图片锁渐隐并播放 DoorLock 解锁；门尚未开放 |
| `DoorUnlocked` | 门 Collider 为 Trigger，交由标准成功链结算 |
| `bubbleDragged` | 气泡已被玩家接管；自动消失和新气泡生成永久停用，直到本次尝试 Reset/重建 |
| 箱子 `Intact/Debris` | 完整箱子顶部接触后进入 `Debris`；残骸只显示、不参与角色碰撞或二次碎裂；owner reset 恢复完整箱体并隐藏残骸 |

## 生命周期、暂停与重开

`Level102Ctrl` 订阅既有状态、暂停、Reset、Replay 和提示事件。非 Playing 时关闭局部输入；重锁/解锁协程、Spine Complete 回调、Tween 和提示气泡实例在 Teardown 时停止/解绑。标准 `PlayCtrl.ResetGame()` 会销毁并重建关卡；同实例 `ResetRuntimeState()` 作为箱子/Key 的兜底复位路径。没有新增存档字段、全局事件 ID 或 analytics 数据。

## 当前验证证据

- 已静态读取 Level101 功能记录、Level102 五个局部脚本和 `Level_102.prefab`，确认局部引用字段、旧控制器禁用开关、Key/DoorLock/气泡字段及 `BoxBroken_Level007_Template` 场景模板存在。
- 当前 Prefab 的 `Level102Ctrl.boxBreak` 已指向 Box 根上的 `Level102BoxBreakOnActor`（脚本 GUID `a438381bb9b34b7aba9e605511463a39`）；`Box_2` 残骸为本关内嵌对象，初始 inactive，`debrisInteractWithActor=false` 时只在首次碎裂后显示，不创建或启用残骸 Collider/relay。该静态接线仍需 Unity 导入和 PlayMode 确认。
- `Level102BoxBreakOnActor` 的 relay guard/reset 修复后，源码括号结构检查通过；残骸 relay 只在首次接管时捕获初始渲染/碰撞状态，owner reset 会同步清除二次碎裂标记并重新隐藏残骸。
- `Level102BoxBreakOnActor.IsPlayerActor` 现在同时检查 Actor 根节点和触发碰撞子节点的 `Player` 标签；这与 Actor 预制体可能把标签放在子碰撞体的结构兼容，且不放宽 `ActorCtrl.IsDied`/Actor 层过滤。
- 本地 `LevelData.xlsx`/`LevelSort.xlsx` 静态 OOXML 检查未发现 `10102`/`Level_102` 行；因此正常主线、远程排序和提示文案登记仍为待确认项，本轮未擅自修改共享配置。
- `git diff --check` 已执行；输出仅包含工作区既有文件的行尾转换警告，没有新增尾随空白诊断。
- 先前的 Unity Roslyn 静态编译检查为 `exit 0`；该证据不能替代最终 PrefabImporter、PlayMode 或真机验证。

### 2026-09-01 运行时审计补丁

- `Level102Ctrl` 新增 `allowUnlockTimeoutFallback`（Prefab 为 `0`），并在启动 DoorLock 前校验 `unlock` Spine；缺失引用、无效动画、执行器异常和未确认完成的超时都会走 fail-closed 收尾，不调用 `SetDoorGate(true)`。
- `Level102Ctrl` 新增 `disableHintBubbleRootMotion`（Prefab 为 `1`）；物理气泡先使用固定碰撞体，等待 show 动画完成/根缩放达到 `0.98` 后再适配可视边界，最长等待 `1s`，退化边界不会覆盖安全回退尺寸。
- 使用 Unity 6000.0.59f2 自带 Roslyn 对 Level102 五个脚本进行增量编译探测；输出未出现 Level102 诊断，但全量响应文件仍有工作区其它脚本的既有 `CS0172/CS0173` 错误，因此不能宣称全项目编译通过。

## 未验证项

以下项目当前必须标记为 `uncertain`：Unity 6000.0.59f2 最终导入后的 Missing Script/空引用、角色接触箱体与残骸的碰撞层矩阵、BoxBroken 排序、Key 拾取与 DoorLock Spine 实际动画时序、锁重生与门 Trigger 通关、提示事件和气泡拖动/碰撞手感、连续 Reset/Replay/暂停恢复，以及 Android/iOS 横屏触控和性能。

## 回滚路径

只回滚 Level102 局部玩法时，移除 `Level_102.prefab` 上的 `Level102Ctrl`、`Level102BoxBreakOnActor`、`Level102KeyTrigger`、`Level102LockDragItem`、`Level102HintBubbleDragItem` 接线，并恢复 Level101 复制基线；不删除或修改共享 Key、DoorLock、HintBubble、`BoxBroken`、`BaseLevelCtrl`、`DragableItem` 或 Level007 资源。随后删除本目录 Level102 局部脚本和本说明/风险文档即可。

### 2026-09-02 DoorLock 执行器时序审计

- 解锁流程现在区分 Spine `unlock` 动画完成和 DoorLock 执行器后续的 SFX/销毁生命周期：启用执行器的兼容路径会先监听实际 `TrackEntry`，再等待执行器完成；只有两者都完成才把门切换为 Trigger。
- DoorLock watchdog 会按 `TrackEntry.TimeScale * AnimationState.TimeScale` 修正动画时长，并额外保留 `0.25s` 执行器生命周期缓冲；`allowUnlockTimeoutFallback` 在本关 Prefab 中为关闭，异常或超时默认保持门锁定（fail-closed）。当前 Prefab 的 `invokeDoorLockExecutor` 也为关闭，因此默认路径直接播放已校验的 Spine 动画和 `unlock` 音效。
- 提示事件的暂停/初始化竞态已收敛：首次 `Start` 前到达的全局 `GamePlayPanelSwitchTipsEvent(true)` 先缓存，待 `CanAcceptGameplayInput` 打开后消费；暂停期间到达的事件也会保留 pending，恢复后再生成物理泡，避免因输入门禁提前返回而丢失全局门槛结果。Reset/Replay/Teardown 会清空 pending 数据、停止替换协程并销毁活动实例；玩家已拖动气泡后仍保留物理实例且不再生成新泡。
- 已完成本关源码结构检查、关键 Prefab 字段/GUID 回读和 `git diff --check`；之前的增量 Roslyn 探测未报告 Level102 诊断。
- Unity PrefabImporter、Spine 实际播放/销毁时序、Key 拾取、门 Trigger 通关、锁重生、提示气泡物理拖拽及移动端 PlayMode 仍为 `uncertain`，不能由上述静态证据替代。
- 回滚范围仍限于本关控制器和 `Level_102.prefab` 的局部接线；不回滚共享 DoorLock、Key、HintBubble 或 Level101 资源。

### 2026-09-02 当前需求修正

- 顶部角色跟随气泡不是本关玩法对象。`Level102Ctrl.Awake/OnEnable` 通过 `BaseLevelCtrl.SetHintBubbleEnabled(false)` 关闭该共享实例；本关只接管 `GamePlayPanel` 中的 `warningBubble`。
- 物理气泡继续复用 `GamePlayPanelSwitchTipsEvent`，因此遵循既有的全局 `showTipsTimeLimit` / `showTipsFailCount` 配置，而不是在重开时主动生成。未拖动时按 `GamePlayPanelTipsSwitcher.DisplayDuration` 自动隐藏，首次拖动后锁存并保留到本次尝试结束。
- `warningBubble` 的 Collider 默认按自身及 UI 子节点的实际矩形边界拟合，`warningBubbleColliderPadding` 可调；拟合失败时回退到根 RectTransform 尺寸。Collider 为实体（非 Trigger），刚体为 Kinematic，拖拽仍由 `Level102HintBubbleDragItem` 转发。
- 完整箱子仍可参与实体物理，但不响应鼠标/触摸拖动；踩碎后的残骸默认 `debrisInteractWithActor=false`，不创建/启用 Collider 或 relay，角色后续接触不会再次爆炸。

本节基于 `BaseLevelCtrl.cs`、`GamePlayPanelTipsSwitcher.cs`、`Level102Ctrl.cs`、`Level102BoxBreakOnActor.cs` 和 `Level_102.prefab` 的静态回读。Unity PrefabImporter、warningBubble 的 WorldSpace 尺寸、角色站立/拖动手感、箱子真实推动和移动端触控仍为 `uncertain`；AGENTS 指定的 `specs/037-remote-resource-loading/plan.md` 当前缺失，相关计划依据也为 `uncertain`。回滚本修正时，恢复上述四个脚本的局部改动并移除 `Level_102.prefab` 的 `fitWarningBubbleColliderToVisual` 字段即可；不修改共享 `warningBubble` 资源或其它关卡。

### 2026-09-02 物理气泡层与钥匙重叠补偿

- 物理气泡由 `Level102Ctrl` 在运行时配置为 `Stuff` 层，且同步设置气泡根节点和所有实际 `Collider2D` 所在节点。该层与 `ActorCtrl.whatIsGround` 的项目约定一致，保证气泡可作为角色脚下的平台；Inspector 的 `hintBubblePhysicsLayer` 可调整，空值或旧 Prefab 缺字段时回退到 `Stuff`。共享 `Resources/HintBubble.prefab` 未修改。
- `Level102KeyTrigger` 增加 `OnTriggerStay2D` 补偿入口，与 `OnTriggerEnter2D` 共用原子收集流程。角色在关卡初始化时已经与钥匙重叠时，只要后续进入可交互状态仍会被收集，不会因 Awake/Start 顺序丢失。
- 全局提示事件若在暂停/结算竞态帧到达，控制器会缓存 `true` 结果并在恢复后消费；`false` 事件会清理未拖动实例，已被玩家拖动的活动物理泡会保留。共享 `BaseLevelCtrl`/`HintBubble` 仍未被修改。
- 静态验证：回读 `Level_102.prefab` 的 `hintBubblePhysicsLayer.m_Bits=256`、脚本字段和 GUID；源码结构检查确认气泡层设置发生在运行时实例生成后，且不可交互分支会清理普通泡，未改共享气泡资源。Unity PrefabImporter、物理层矩阵、钥匙实际重叠触发和 PlayMode 仍待验证。

### 2026-09-02 全局提示配置接管

- Level102 物理气泡改为监听 `GamePlayPanelSwitchTipsEvent`，与 `PlayCtrl_Playing` 的全局 `showTipsTimeLimit` / `showTipsFailCount` 门槛保持一致；不再依赖 `LevelData.isGiveHints` 或 `ShowHintBubbleEvent` 才能出现。
- Prefab 显式记录 `globalTipsBubbleDescription=U_Tips_Pop_Des_01` 和 `globalTipsBubbleDisappearTime=3`。事件的 `false` 分支清理未被拖动的物理气泡；玩家拖动后保留实例，直到 Reset/Replay/Teardown。
- 静态验证已回读控制器订阅、事件分支和 Prefab 字段；全局远端配置实际下发值、顶部 UI 气泡与物理气泡是否需要同时显示，以及 Unity PlayMode 的出现/自动关闭时序仍为 `uncertain`。

### 2026-09-02 重试姿态与拖拽输入修正

- `GamePlayPanelTipsSwitcher` 在面板实例生命周期内保存 `warningBubble` 的初始本地位置、旋转和缩放，并提供 `ResetTargetPose()`；Level102 每次重新接管前先调用它。GamePlayPanel 跨关卡重试复用时，气泡不会把上一次拖动后的姿态当成新尝试的初始姿态。
- `Level102HintBubbleDragItem` 成功开始拖动时临时设置父级 Tips 按钮的 `IsBlockClick`，释放后延迟一帧恢复，避免拖动物理气泡被父级按钮误判为打开 Tips 面板；拖动失败或组件禁用不会锁住父按钮。
- `Level102Ctrl.ShowWarningBubble()` 仍从零缩放展开，展开完成后开放拖拽；气泡未拖动时继续按全局展示时长自动隐藏，拖动后永久保留到本次尝试结束。

本次修正基于三个局部脚本的静态回读；共享面板持久化复用、UI 事件顺序、warningBubble 的实际拖拽命中和重试视觉效果仍需 Unity PlayMode 验证，状态为 `uncertain`。

### 2026-09-02 首次接管与 authored 姿态补充

- `Level102Ctrl.Start()` 在关卡生命周期进入可交互状态前就调用 `TryBindWarningBubble()`，先关闭共享 `GamePlayPanelTipsSwitcher` 的默认显示流程，再等待全局提示事件触发本关的展开动画。这样首次提示不会被面板默认动画抢先显示，拖拽组件也会在提示出现前完成绑定。
- `GamePlayPanelTipsSwitcher.ResetTargetPose()` 对 RectTransform 同时恢复 `anchoredPosition` 与 `localPosition`，覆盖 `DragableItem` 通过世界坐标移动后遗留的本地位置/Z 值；旋转和缩放仍按面板首次加载时的 authored 快照恢复。
- Level102 释放 external control 时共享切换器也会先恢复该快照，面板即使未立即销毁也不会把拖动后的姿态带入下一次普通提示。
- warningBubble 展开完成后会在最多 `1s` 内重试按 UI 子节点边界拟合 Collider，覆盖面板入场阶段子 Image 暂时为零缩放的情况；如果仍无法取得有效边界，则保留根节点固定尺寸回退。
- `Level102BoxBreakOnActor.ActivateDebris()` 在 `alignDebrisToBox=false` 时每次激活都恢复 `Box_2` 的 authored 世界位置、旋转和缩放，不继承前一次尝试的运行时姿态，因此残骸高度以场景初始摆放为准。

本节为源码和 Prefab 静态修正记录；首次提示展开、UI 拖拽命中、跨重试姿态和残骸最终画面仍需 Unity PlayMode 验证，状态为 `uncertain`。

### 2026-09-02 Tips 面板异步接管补偿

- `ConsumePendingHintBubble()` 现在只有在 `TryBindWarningBubble()` 成功后才清除 pending。若全局 `GamePlayPanelSwitchTipsEvent(true)` 先于面板实例或 warningBubble 子节点创建到达，提示资格会保留到后续 Update，不会因唯一一次事件已消费而永久丢失。
- `ReplaceHintBubbleNextFrame()` 在延迟帧仍找不到 warningBubble 时会重新写入 pending，并沿用原始文案与展示时长；面板就绪后继续执行本关的展开动画、Collider 拟合和拖拽启用。
- 该补偿不改变全局 `showTipsTimeLimit` / `showTipsFailCount` 门槛，也不重新启用角色头顶通用气泡；Reset/Replay 的 Teardown 仍会清空 pending。

本次改动已完成源码括号检查、Prefab 字段回读和 `git diff --check`；没有 Unity Editor/PlayMode，异步面板创建、首次展开、实际拖拽命中和多次重试仍为 `uncertain`。回滚时恢复 `Level102Ctrl.cs` 中 pending 消费/绑定失败分支即可，不需要修改共享 GamePlayPanel 或 warningBubble 资源。

### 2026-09-02 本次需求修正

- 残骸位置采用“横向跟随、纵向固定”：`alignDebrisToBox=false` 时，`Box_2` 每次显示只读取完整箱子的当前世界 X；Y/Z、旋转和缩放继续使用残骸首次捕获的场景初始值，不再复制箱子被推后的整套世界姿态。
- 收集钥匙默认关闭 `useLocalKeyPickupFallback`，恢复 Key 预制体的标准 `TriggerExecutor` 链。`Level102Ctrl` 在执行前把 `ItemJumpToListener.endTarget` 绑定到既有 `door_0/LockPoint`（找不到时回退门根），因此钥匙会按项目标准表现飞向锁，而不是原地渐隐缩放。
- warningBubble 的自动关闭新增独立回缩 Tween（默认 `0.2s`、`Ease.InBack`），Teardown/重置仍立即隐藏；World Space UI 复位以 `RectTransform.anchoredPosition` 为唯一 XY 基准，只恢复 local Z，避免 anchored/localPosition 双写造成位置漂移。
- `Level102HintBubbleDragItem` 优先按 warningBubble 根节点及其可见 UI 子矩形进行屏幕命中，并使用 Canvas 平面坐标转换拖动；无 UI 矩形时仍回退到共享 Collider2D/半径检测。
- Level102 在 Awake/Start/OnEnable/绑定 warningBubble 时关闭角色头顶通用 HintBubble；`hasStartedPlayLifecycle` 在 Teardown 清回，下一次尝试仍按 `PlayCtrl_Playing` 的全局 `showTipsTimeLimit` / `showTipsFailCount` 事件门槛重新接收提示。
- DoorLock 实例的 SortingGroup 统一为可调 `doorLockSortingOrder=101`（Prefab 同步为 101），仅高于门体基础层级一层，避免解锁演出期间锁视觉覆盖成一扇额外前置门。

### 2026-09-02 标准钥匙飞锁链补全

- 标准 `Key.prefab` 的 `TriggerExecutor` 链包含一个未绑定目标的 `SwitchComponentListener`。其它关卡会在 Prefab 覆写该目标为出口门 Collider；Level102 现在在执行器启动前将所有空目标的同类监听器绑定到当前 `door_0` Collider，并继续把 `ItemJumpToListener` 的终点绑定到 `LockPoint`。
- 这样保留项目既有的 getKey、缩放/渐隐、钥匙飞向锁和销毁顺序；不会退回 Level102 的局部“原地渐隐”兜底，除非显式打开 `useLocalKeyPickupFallback`。

### 本次验证与未确认项

- 已静态回读 `Level_102.prefab` 的 `useLocalKeyPickupFallback=0`、`doorLockSortingOrder=101`、`warningBubbleCollapseDuration=0.2`、`warningBubbleCollapseEase=26` 和残骸 `alignDebrisToBox=0`；已检查标准 Key/LockPoint 的 GUID 与 `ItemJumpToListener.SetEndTarget` API。
- 已完成 Level102 相关 C# 大括号/关键方法引用检查，并执行 `git diff --check`。当前环境没有 Unity Editor/PlayMode，钥匙实际飞行轨迹、DoorLock 最终遮挡、World Space 气泡命中/拖动、重试多次提示事件仍标记为 `uncertain`。
- 回滚边界：恢复 `Level102Ctrl.cs`、`Level102BoxBreakOnActor.cs`、`Level102HintBubbleDragItem.cs`、`DragableItem.cs`、`GamePlayPanelTipsSwitcher.cs` 及 `Level_102.prefab` 的本次字段即可；不修改共享 Key、DoorLock、warningBubble 资源。

本次新增运行时绑定使用 `SwitchComponentListener.HasTarget/SetTarget`，仅影响 Level102 的 Key 实例；源码/Prefab 静态检查通过，标准执行器在 Unity 中的实际飞行、销毁和解锁时序仍为 `uncertain`。回滚时恢复 `Level102Ctrl.ConfigureKeyPickupTarget()` 与 `SwitchComponentListener` 新增 API 即可。

### 2026-09-02 当前需求修正（覆盖同日旧表述）

- 箱子残骸继续采用“横向跟随、纵向固定”：残骸的 X 使用箱子碎裂时的当前世界 X，Y/Z、旋转和缩放使用场景初始快照；`debrisInteractWithActor=false` 时残骸不创建或启用 Collider/relay，不会二次爆炸。
- DoorLock 不再由 Level102 额外添加 SortingGroup；使用标准 `DoorLock.prefab` 自带的渲染排序，避免解锁演出期间出现整扇前置门的视觉覆盖。
- warningBubble 的拖拽命中只绑定 `Image` 子节点及其文字子节点，根节点仅负责锚点；拖拽坐标投影到同一 World Space Canvas 平面，物理 Collider 也按该可视矩形拟合。这样点击视觉区域即可拖动，且不会命中根锚点的空白区域。
- warningBubble 自动消失仍播放可配置的回缩 Tween；每次新关卡实例重新接管前恢复面板 authored 姿态，重试时继续沿用 `GamePlayPanelSwitchTipsEvent` 的全局门槛，不主动弹泡。
- 钥匙默认强制执行标准 `Key.prefab` 的 `TriggerExecutor`，运行前绑定空的门 Collider 步骤和 `door_0/LockPoint`；执行器缺失时保留钥匙并记录错误，不再静默原地隐藏。局部拾取兜底只有显式打开 `useLocalKeyPickupFallback` 才会使用。

本节已完成源码、Prefab 字段和标准 Key 链的静态回读，并执行 `git diff --check`。当前无 Unity Editor/PrefabImporter/PlayMode，warningBubble 的 World Space 实际坐标、首次提示动画、拖拽命中、多次重试以及钥匙飞锁时序仍为 `uncertain`；`specs/037-remote-resource-loading/plan.md` 仍缺失，相关计划依据同样为 `uncertain`。

### 2026-09-02 最终时序与重试复用补充

- 门视觉按状态互斥：`LockedIdle/Relocking` 只显示原锁定门，`Unlocking` 隐藏原锁定门和标准门、只显示 `DoorLock` Spine，`DoorUnlocked` 关闭 `DoorLock` 并只显示标准门。解锁动画期间不会再叠出第二扇门。
- Key 继续执行共享 `Key.prefab` 的标准 `TriggerExecutor`。当前 Level102 Prefab 已把 `ItemJumpToListener.endTarget` 显式指向 `door_0/LockPoint`，并将对应的门 Collider 开关步骤接到本关门 Collider；控制器等待 `ItemJumpToListener.IsDone` 后才启动 DoorLock 解锁演出。
- `GamePlayPanel` 会跨重试保留。Level102 释放 `warningBubble` 时不再销毁运行时补齐的拖拽、Collider 和 Rigidbody 组件，只关闭其运行能力；下一次尝试重新绑定新控制器、恢复 authored `anchoredPosition`、重新启用物理和拖拽。这样避免同帧延迟销毁让新实例随后失去拖拽能力。
- `warningBubble` 每次仍由 `PlayCtrl_Playing` 的公共 `GamePlayPanelSwitchTipsEvent` 触发。`LevelRuntimeState.ResetDataPreservingRevive()` 会重置 `HasClickTips` 和本次计时，因此每次重试都会重新等待同一远端时间/失败次数门槛；未拖动时展开后计时并执行 `0.2s` 回缩动画，拖动后保留到本次尝试结束。
- 残骸只继承箱子碎裂时的世界 X；Y/Z、旋转和缩放均恢复场景中 `Box_2` 的初始快照。残骸保持无 Collider、无二次爆炸。

本节基于当前源码、Prefab YAML、共享 Key/Level007 表现链和公共 Tips 状态机的静态证据。Unity 当前前台实例中的实际动画、UI 射线、物理站立和连续重试仍需 PlayMode 验证；若验证失败，回滚范围为 `Level102Ctrl.cs`、`Level102HintBubbleDragItem.cs`、`GamePlayPanelTipsSwitcher.cs` 与 `Level_102.prefab` 的本轮局部改动，共享 Key、DoorLock 和 warningBubble 资源无需回滚。

验证补充：使用 Unity 6000.0.59f2 自带 Roslyn 和当前 `Assembly-CSharp` 响应文件执行编译探测，未出现 Level102 或 `GamePlayPanelTipsSwitcher` 诊断；全量探测仍被工作区其它模块缺失的 `IGMFeature`、`IWannerGameLevelCtrl`、`TbIWannerLevelData` 等类型阻断，因此不能声明全工程编译通过。

### 2026-09-03 全局提示循环结束通知

- 主线共享提示逻辑现要求 warningBubble 完全消失后才开始下一轮 `showTipsTimeLimit` 倒计时。Level102 因接管了共享切换器的显示生命周期，会在未拖动气泡完成本关回缩并隐藏后调用 `GamePlayPanelTipsSwitcher.NotifyExternalDisplayEnded()`，复用同一个 `GamePlayPanelSwitchTipsEvent(false)` 结束反馈。
- 达到 `showTipsFailCount` 的新尝试仍首次进入即显示；此后累计重试次数不再重复参与，等待远端/本地回退配置的 `showTipsTimeLimit` 后再次显示。`globalTipsBubbleDisappearTime` 及切换器 `DisplayDuration` 仍决定展示期，没有写死 3 秒。
- 玩家首次拖动后气泡作为物理垫脚物持续存在，不发送“消失”通知，也不生成下一轮；Reset/Replay/Teardown 仍按既有路径清理。
- 本轮 Level102 通知调用链已完成静态复核；Unity 6000.0.59f2 Roslyn 使用当前完整响应文件编译 `Assembly-CSharp` 为 `exit 0`、`0 error`。实际多轮、拖动、暂停和移动端触控仍为 `uncertain`。回滚时删除 `HideWarningBubbleAfterDelay()` 的外部结束通知参数与调用，不修改 Prefab 或配置。

### 2026-09-03 warningBubble 世界碰撞与第三次重试位置修正（覆盖旧碰撞表述）

#### 需求与根因

- 玩家需要把提示按钮里的 `warningBubble` 当作垫脚解谜对象，但该对象位于屏幕 UI Canvas。旧实现把 `Collider2D` / `Rigidbody2D` 直接挂在 UI 节点上，UI 坐标没有转换为关卡世界坐标，因此角色无法在视觉气泡处发生有效的 Physics2D 接触。
- 达到失败次数门槛后，`PlayCtrl_Playing` 会在新尝试进入 `PLAYING` 的首帧立即派发显示事件；此时 `GamePlayPanel.PlayShowPanelAnim()` 仍在依次恢复按钮缩放。`warningBubble` 是 `tips_btn` 的子节点，第三次重试会继承父按钮的零值或中间缩放，造成位置/尺寸看起来偏移。首次等待约 10 秒后显示时，面板动画已经结束，所以位置正确。
- `specs/037-remote-resource-loading/plan.md` 在当前工作区仍缺失，无法核对该计划；计划相关依据标记为 `uncertain`。本轮以用户反馈、当前源码、Prefab 层级和公共提示状态机为行为依据。

#### 新行为与流程

- `warningBubble` 继续留在 `GamePlayPanel` 内负责显示、UI 射线命中和拖拽；`Level102Ctrl` 在场景根层级创建独立的 `Level102_WarningBubblePhysicsProxy`，配置非 Trigger `BoxCollider2D`、Kinematic `Rigidbody2D` 和 `Stuff` 层。
- 每次同步时读取气泡 `Image`/文字的 UI 世界角点，先换算为屏幕矩形，再通过关卡 `GameCamera` 投影到角色所在 Z 平面；由该世界矩形设置代理位置和碰撞体尺寸。`LateUpdate` 与 `FixedUpdate` 会在气泡可交互期间刷新代理，使拖拽、CanvasScaler、安全区和相机变化不会让显示与碰撞分离。
- 气泡隐藏、重置、Replay 或 Teardown 时立即停止代理模拟；退出本关时销毁代理。UI 面板上的拖拽组件仍按既有复用规则处理，不向共享 Prefab 写入物理组件。
- 失败次数触发显示后，协程最多等待 `2s`，直到父级 `CustomButton` 已激活且本地 XY 缩放至少恢复到 `0.98`；随后强制刷新 Canvas、配置世界代理并播放气泡展开。正常的 10 秒计时触发同样走该流程，但通常无需等待。

#### 影响、验证与回滚

- 行为代码只修改 `Level102Ctrl.cs`；本轮没有修改 `Level_102.prefab`、共享 `GamePlayPanel`、`warningBubble` Prefab、物理层矩阵或远端提示配置。文档同步更新本文件、`Level102风险评估.md` 和项目功能记录。
- 使用 Unity 6000.0.59f2 自带 Roslyn，对当前 `Level102Ctrl.cs` 与 `Level102HintBubbleDragItem.cs` 进行定向编译，结果为 `exit 0`、`0 error`；仅保留既有的 `hintBubblePrefab` 未使用警告。源码大括号/region 数量、关键旧字段残留和差异格式检查通过。
- 尚未运行前台 Unity PlayMode。角色真实落脚、从下方/侧面接触、拖动时代理跟随、第三次重试最终视觉位置、不同分辨率与移动端触控仍为 `uncertain`，需要关卡内验收。
- 回滚仅需移除 `Level102Ctrl.cs` 的父按钮就绪等待和 `Level102_WarningBubblePhysicsProxy` 创建/投影/同步逻辑，并恢复本节对应文档；不回滚共享 UI、Prefab、配置或其它 Level102 玩法。

### 2026-09-03 红框碰撞、开锁门视觉与重试清理修正（覆盖同日旧门/碰撞表述）

#### 需求与新行为

- 策划截图的红框要求碰撞范围只覆盖气泡中间的文字平台，不包含外侧气泡轮廓和尾巴。`Level102Ctrl` 绑定 `GamePlayPanel` 实例时优先查找 `msg`（兼容源 Prefab 名称 `title`）的 `300×80` RectTransform，再以 `warningBubbleColliderPadding=(0,20)` 向上下各扩展 20 UI 单位；结果为约 `300×120` 的红框范围。找不到文字节点时才回退到背景 `Image`。
- 角色只在自身 Collider 正与气泡世界代理接触、水平有重叠且脚底位于代理顶面容差内时，暂时隐藏 Actor 直属 `Shadow`。离开平台、气泡隐藏或 Reset/Replay/Teardown 时恢复进入前的影子激活状态，不改共享 `ShadowController`。
- 开锁状态进入 `Unlocking` 后，原锁定门立即关闭，同时显示正常过关门；`DoorLock` 只在正常门上播放锁的解锁演出，不再出现“没有门的锁浮在空中”。`unlock` 返回的 Spine `TrackEntry.MixDuration` 强制为 `0`，取消解锁动画的动作混合。

#### 重试报错根因与修正

- Unity `Editor.log` 的完整栈指向 `Level102Ctrl.StopLocalKeyPickup()`：已 Destroy 的 `keyObject` 仍是非 CLR null 的 Unity 托管引用，旧代码的 `keyObject?.transform.DOKill()` 会继续访问 `transform` 并抛 `MissingReferenceException`。
- 该异常使 `BeginTeardown()` 在旧的事件退订之前中断，已销毁的 `Level102Ctrl` 留在全局监听列表。下次 `GamePlayPanelSwitchTipsEvent` 派发时先命中旧对象并再次抛异常；由于当前 `EventDispatcher` 在整个监听遍历外捕获，后续面板遮罩监听可能被跳过，因而出现重试黑屏几乎不可见。
- `StopLocalKeyPickup()` 已改用 Unity 重载的 `!= null` 判断再访问 GameObject/Transform/Renderer；`BeginTeardown()` 把 `EventsUtils.ResetEvents()` 提到第一步，即使后续某项资源清理再异常，旧关卡实例也不会继续拦截重试事件。同一清理链中的其它 Unity 对象访问也改为安全空值判断。

#### 影响、验证与回滚

- 行为文件仅为 `Assets/Game/Scripts/Level/MainLevel/Level_102/Level102Ctrl.cs` 和 `Assets/Game/Prefabs/Level/Level_102.prefab`；未修改共享 `EventDispatcher`、`GamePlayPanel`、`warningBubble.prefab`、`DoorLock.prefab`、`ShadowController` 或 Spine 公共封装。
- Unity 6000.0.59f2 Roslyn 定向编译当前 `Level102Ctrl.cs` 和 `Level102HintBubbleDragItem.cs` 为 `exit 0`、`0 error`，仅有既有 `hintBubblePrefab` 未使用警告；前台 Unity 本轮脚本编译已进入 `ReloadAssembly`，其后最新日志未出现 `error CS` 或 `Scripts have compiler errors`。Prefab 序列化字段已回读。
- 尚未完成前台 PlayMode 行为验收；红框实际落脚手感、影子切换、门/锁层叠观感、第三次与更多次重试的黑屏和 Console 均为 `uncertain`。`specs/037-remote-resource-loading/plan.md` 在当前工作区仍不存在，计划依据也标记为 `uncertain`。
- 回滚时恢复上述两个 Level102 私有文件中的红框、影子、门状态、Spine 零混合和 Teardown 顺序改动即可；共享系统无需回滚。

### 2026-09-03 钥匙收集时的门替换顺序与对齐（覆盖前节开锁可见性表述）

#### 需求、基线与新流程

- 当前有效需求是：角色吃到钥匙的当帧，先让“正常过关门 + 正常 DoorLock”在假门上层出现，然后再隐藏假门；钥匙到锁后才播放 `unlock`。该顺序覆盖前节“进入 Unlocking 才同帧互斥切门”的旧表述。
- 旧实现用单一 `LockState` 同帧互斥显示 `body` 和 `door`，并在钥匙飞到 `LockPoint` 后才激活 DoorLock；因此无法表达“新门已在上层，旧门尚未退场”的短重叠阶段。
- `NotifyKeyCollected()` 现在先执行 `PrepareDoorReplacementVisuals()`：对齐并显示 `door_0/door`，激活 DoorLock，关闭 DoorLock 自带的触发转发器，播放零混合 `lock_idle`，同时保留假 `body` 和假图片锁。协程 `yield return null` 保留一个完整渲染帧，下一帧才隐藏假门/假锁。
- 钥匙仍复用标准 `TriggerExecutor -> ItemJumpToListener -> door_0/LockPoint` 链；到达后 `BeginUnlockSequence()` 直接在已显示的 DoorLock 上播放零混合 `unlock`，动画完成后再开放门 Trigger。资源、存档、远程配置和事件类型均未改变。

#### 位置与渲染对齐

- Level102 的 `body` 和源 `door` Transform 原本都是局部 `(0,-1.524,0)`，但两套 Spine 网格的主轮廓原点/尺寸不同，Transform 数值相同仍会视觉偏移。Level101 已有基于两套 idle 网格边界得出的校准值：正常门局部位置 `(0.1058,-1.5815,0)`、局部缩放 `(1.0283,1.0165,1)`。Level102 现在复用这组值，并在每次初始化/替换前重新应用。
- 渲染顺序仍优先使用 Prefab 既有设置：正常门 Renderer 的内部 order 高于假 `body`，DoorLock `SortingGroup` 为 105，门根 `SortingGroup` 为 100。运行时还会比较 SortingLayer 实际值和 order；只在正常门/锁不在上层时，局部提升到对应假表现的 `order+1`。

#### 影响、验证与回滚

- 行为范围仍仅为 `Assets/Game/Scripts/Level/MainLevel/Level_102/Level102Ctrl.cs` 和 `Assets/Game/Prefabs/Level/Level_102.prefab`；未修改 Level101、共享 `door_0.prefab`、`DoorLock.prefab`、Key 执行器、Spine 封装或共享事件。
- 已对比 Level101 对齐源码/Prefab、Level102 两套门 Transform、Renderer/SortingGroup 值和钥匙调用链；Unity 6000.0.59f2 当前 `Assembly-CSharp.rsp` 全量 Roslyn 编译结果为 `exit 0`、`0 error`、107 条既有 warning。Prefab 串行化字段已回读。
- 前台 PlayMode 的实际重叠帧、两门轮廓对齐、锁在门上的位置、钥匙飞行终点和连续重试仍为 `uncertain`。`specs/037-remote-resource-loading/plan.md` 仍不存在，计划依据同样为 `uncertain`。
- 回滚时删除 `PrepareDoorReplacementVisuals`/退役协程/排序校验，恢复 `ApplyDoorVisualState` 的单状态互斥判断，并移除 Prefab 中的正常门校准和 `doorLockIdleAnimationName` 字段即可；共享资源无需回滚。

### 2026-09-03 钥匙飞行层级与提示次数复核

#### 钥匙飞行层级

- `Level_102.prefab` 将本关 Key 实例的 `SortingGroup.sortingOrder` 覆写为 `100`，标准 `DoorLock.prefab` 的锁渲染为同层 `101`，因此钥匙飞到 `LockPoint` 附近时会被锁遮住。
- `Level102Ctrl.NotifyKeyCollected()` 现在先建立正常门/锁替换表现，再调用 `EnsureFlyingKeyAboveDoorLock()`：优先读取 DoorLock 根 SortingGroup；没有时遍历锁子树 Renderer 取得最高有效排序，然后把 Key 整体 SortingGroup 设置为同 SortingLayer、`lockOrder + 1`。
- 调整只发生在钥匙飞行阶段，不改共享 `Key.prefab` 或 `DoorLock.prefab`。`CaptureKeyPickupPose()` 保存本关 Key 的 authored 排序，`ResetKeyPickupVisualState()` 在重试初始化时恢复原 SortingLayer/order。

#### warningBubble 出现次数语义

- Level102 的解谜气泡只复用 `PlayCtrl_Playing` 派发的 `GamePlayPanelSwitchTipsEvent`；本关 `LevelData.isGiveHints=false`，不会同时走角色头顶的 `ShowHintBubbleEvent`。
- 公共条件为 `FailCount + ResetCount >= showTipsFailCount`。当前本地 Luban/内置配置的 `showTipsFailCount=2`、`showTipsTimeLimit=10`，因此：首次进入累计次数 0，等待约 10 秒；第一次局内重试累计 1，仍等待约 10 秒；第二次局内重试累计 2，新尝试进入 PLAYING 后立即显示。换成“进入次数”说法，就是第三次进入本关时立即显示，不是第三次重试。
- `showTipsFailCount` 优先取远端配置。当前 `Editor.log` 最近一次远端原文和 Playing 运行日志都确认生效值为 `2`。若其它环境下发值为 1，第一次局内重试才会立即显示；可继续用现有 `showTipsFailCount: N` 日志核对。本轮没有修改公共阈值或远端配置。

#### 验证与边界

- 使用 Unity 6000.0.59f2 自带 Roslyn 及当前完整 `Assembly-CSharp.rsp` 编译：`exit 0`、`0 error`、107 条既有 warning，Level102 无诊断。
- 尚未运行修复后的 PlayMode，钥匙跨过假门/正常门/锁时的逐帧遮挡仍为 `uncertain`；当前编辑器会话的提示远端值已确认是 2，其它发布环境仍需各自日志确认。`specs/037-remote-resource-loading/plan.md` 在工作区缺失，计划核对同样为 `uncertain`。
- 回滚只需移除 `Level102Ctrl.cs` 的 Key SortingGroup 快照、飞行阶段提层和重试恢复逻辑；共享 Key、DoorLock、提示配置及 GamePlayPanel 不回滚。

### 2026-09-03 钥匙飞行渐隐与缩小恢复

#### 根因与修复

- 共享 `Key.prefab` 的标准顺序为：启动缩放到 `0.5`（`0.8s`，不阻塞）、Sprite 延迟 `0.4s` 后渐隐 `0.5s`（不阻塞）、飞向门锁（标准 `1s`，阻塞），飞行完成后才推进门 Collider 和延迟 `0.1s` 销毁。
- Level102 Prefab 曾单独把 `ItemJumpToListener.duration` 覆写为 `0.5s`。执行器因此约在 `0.6s` 就销毁 Key，早于缩小的 `0.8s` 和渐隐的 `0.9s` 完成时刻，视觉上表现为钥匙只飞走、没有完整渐隐和变小。
- 本轮删除 `Level_102.prefab` 的 `duration=0.5` 实例覆写，使其重新继承共享 Key 的 `1s` 飞行时长。缩小与渐隐仍由共享 Key 原有监听器执行，不复制代码、不修改共享资源；钥匙飞行期高于 DoorLock 的运行时排序修复继续生效。

#### 影响、验证与回滚

- 运行时流程仍为 `Level102Ctrl -> Key TriggerExecutor -> ItemScaleListener / SpriteRenderFadeListener / ItemJumpToListener / DestroyListener`，钥匙飞完后才开始 DoorLock 的真实解锁动画；存档、配置、事件和其它关卡不变。
- 已静态断言 Level102 不再包含 Key 跳跃时长覆写，生效飞行时长为 `1s`、缩小 `0.8s`、渐隐 `0.4+0.5s`；Unity 6000.0.59f2 完整 Assembly-CSharp Roslyn 编译为 `exit 0`、`0 error`、107 条既有 warning。
- 尚未运行修复后的 PlayMode，飞行轨迹、缩小/渐隐观感、落锁时机和移动端帧率下的表现仍为 `uncertain`。`specs/037-remote-resource-loading/plan.md` 缺失，计划核对同样为 `uncertain`。
- 回滚只需在 `Level_102.prefab` 的 Key 实例重新增加 `ItemJumpToListener.duration=0.5` 覆写；不回滚共享 Key、DoorLock 或 Level102 控制器。

### 2026-09-03 按 Level002 对齐钥匙参数与销毁顺序（覆盖上一节 1 秒方案）

#### Level002 基准

- Level002 的 Key 沿用共享参数：缩小目标 `(0.5,0.5,0.5)`、时长 `0.8s`；Sprite 从 `0.4s` 开始渐隐、持续 `0.5s`、结束 Alpha 为 0；飞行使用 `ItemFlyPathListener`，时长 `0.5s`。
- Level002 的 Key `TriggerExecutor` 扩为 8 项：飞行结束后先启用门、执行并等待 DoorLock 解锁，最后才执行 Key 的 `DestroyListener`（额外延迟 `0.1s`）。所以即使飞行只需 0.5 秒，缩小和渐隐仍有时间完整播放。

#### Level102 新行为

- Level102 恢复 Key 的 `ItemJumpToListener.duration=0.5s`，保留本关原有 `jumpPower=3` 和 `door_0/LockPoint` 终点；不再用上一节临时采用的 1 秒飞行。
- 新增 `Level102KeyUnlockWait` 并把本关 Key 执行器同样扩为 8 项：索引 6 等待 `Level102Ctrl.IsDoorUnlocked`，索引 7 才执行共享 Key 原有的 `DestroyListener`。本关 DoorLock 仍由 `Level102Ctrl` 的零混合解锁状态机驱动，不会像 Level002 那样重复调用 DoorLock Executor。
- 实际时序为：0 秒同时启动缩小/渐隐准备；钥匙在 0.5 秒飞到锁并启动本关解锁；缩小约 0.8 秒完成、渐隐约 0.9 秒完成；门锁解锁完成后再销毁 Key。飞行期间 Key SortingGroup 仍临时高于锁。

#### 影响、验证与回滚

- 修改文件为 `Level_102.prefab`、新增 `Level102KeyUnlockWait.cs/.meta` 及 Level102/项目记录文档。共享 Key、DoorLock、Level002、事件、配置、存档和其它关卡未改。
- Prefab 静态检查：175 个 YAML 定义、175 个唯一 fileID、0 重复；Key 飞行 0.5 秒、执行器 size 8、等待节点索引 6、Destroy 索引 7 和脚本 GUID 均已回读。Unity 6000.0.59f2 当前完整源码加新增脚本 Roslyn 编译为 `exit 0`、`0 error`、107 条既有 warning。
- 尚未运行修复后的 PlayMode，0.5 秒轨迹、渐隐/缩小观感、DoorLock 完成事件及连续重试仍为 `uncertain`。`specs/037-remote-resource-loading/plan.md` 缺失，计划核对同样为 `uncertain`。
- 回滚时删除 `Level102KeyUnlockWait.cs/.meta`，移除 Key 实例新增组件及执行器 size/index 覆写，并删除 `duration=0.5` 覆写即可恢复上一节方案；共享资源不回滚。

### 2026-09-03 正常门内漩涡恢复

#### 基线、根因与新行为

- Level002 使用的共享 `door_0.prefab` 中，正常门 `door` 与门内 `effect` 是一组表现：`effect` 默认启用，以 `effects` 动画循环播放；Level101 切换到正常门时同样会同时启用 `door` 和 `effect`，并为二者应用一致的位置、缩放补偿。
- Level102 虽然已经引用同一 `door_0/effect`，但 `ApplyDoorVisualState()` 过去在所有状态下都强制关闭它，所以开门后只剩门框和门帘，门内为空。
- Level102 现在让 `effect` 与 `standardDoorVisual` 使用同一个 `showStandardDoor` 条件：初始/重试的假门阶段二者都隐藏；吃到钥匙、正常门开始替换假门时二者同时显示；解锁和最终可通关状态继续显示。正常门与漩涡不会分离闪现。
- `ApplyNormalDoorAlignment()` 现在同时校准 `door` 与 `effect`，两者都使用 `localPosition=(0.1058,-1.5815,0)`、`localScale=(1.0283,1.0165,1)`，与 Level101 的既有校准基线一致，避免漩涡相对门框偏移。

#### 影响、验证与回滚

- 行为修改仅涉及 `Assets/Game/Scripts/Level/MainLevel/Level_102/Level102Ctrl.cs`；共享 `door_0.prefab`、Level002、Level101、DoorLock、Key、Spine 资源和其它关卡未修改。
- 已静态回读共享门的 `effect/effects` 循环配置、Level002 的同 Prefab 实例和 Level101 的同步显示/对齐实现；Unity 6000.0.59f2 当前完整 `Assembly-CSharp.rsp` Roslyn 编译结果为 `exit 0`、`0 error`、107 条既有 warning。
- 尚未运行修复后的 PlayMode；漩涡循环、开门逐帧层叠、门框内实际位置、连续重试恢复仍为 `uncertain`。`specs/037-remote-resource-loading/plan.md` 在当前工作区缺失，计划核对也保持 `uncertain`。
- 回滚只需把 `legacyDoorEffectVisual` 恢复为始终隐藏，并从 `ApplyNormalDoorAlignment()` 移除其 Transform；共享门资源无需回滚。

## 2026-09-09：已获得提示后保留物理气泡入口

- 需求：Level102 必须保持玩家取得通关垫脚气泡的机会，特殊逻辑放在本关 Ctrl。
- 修改前基线：PlayCtrl_Playing.OnUpdate 在 HasTips=true 时直接返回；本关只监听公共显示事件，因此玩家获得答案后（含保留提示资格的重试）无法再通过该入口弹出物理气泡。
- 修改范围：仅 Level102Ctrl.cs 增加 UpdatePhysicalBubbleAvailability；不修改共享 PlayCtrl、GamePlayPanel、Prefab、其它关卡或提示资格。
- 修改后：未获得提示仍使用公共事件；已获得提示时，本关读取相同 showTipsTimeLimit/showTipsFailCount 门槛，以 CurrentAttemptTimeSeconds、TipsWarningHiddenAtSeconds 和累计 FailCount+ResetCount 判断。配置首次补偿时缓存，避免逐帧读取。首次可走重试门槛，隐藏后只按等待时间循环。
- 数据与 UI 流：满足门槛 → SwitchClickTips(true) 标记展示轮次 → 本关 pending → 原 ConsumePendingHintBubble/ReplaceHintBubbleNextFrame → 原 warningBubble 展开与物理代理。完全隐藏仍通过既有 NotifyExternalDisplayEnded 记录下一轮起点；不派发新的全局显示事件，不改 HasTips、存档、计数或打点。
- 边界：暂停、尝试切换、已拖动、已有气泡、pending、替换协程或回缩动画期间不发起补弹；沿用面板就绪等待与本关清理。新关卡实例重新缓存配置。
- 验证：Unity 6000.0.59f2 自带 Roslyn 使用当前 Assembly-CSharp.rsp 编译到 Temp/Level102BubbleCheck.dll，退出码 0，0 error；静态核对公共门槛、SwitchClickTips 的隐藏计时闭环及拖动锁存路径。
- 未验证（uncertain）：尚未运行 PlayMode，需覆盖已获提示后首次弹出、未拖动多轮、获得答案后 Reset、气泡拖动保留、暂停/恢复、退出和 Android/iOS 横屏触控。CodeGraph CLI 不可用，指定 specs/037-remote-resource-loading/plan.md 缺失，使用本地源码与文档核对。
- 回滚：删除本次局部方法、Update 调用、三个配置缓存字段及 using Game.ConfigModule；不改共享脚本、Prefab 或存档。
