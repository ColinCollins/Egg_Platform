# Level102 箱子接触碎裂

## 需求背景

Level102 的箱子不能被拖动。玩家从顶部真实接触箱子时，箱体应隐藏并播放 Level007 使用的 `BoxBroken` 碎裂反馈，同时显示残骸；玩家再次接触残骸时，残骸也应播放碎裂反馈并隐藏。

## 影响范围

- `Level102BoxBreakOnActor.cs`：Level102 局部碰撞与表现组件；本关通过运行时 relay 让残骸可被玩家再次接触碎裂。
- `BoxBroken.prefab`：仅作为 Inspector 中的粒子模板引用，不修改资源。
- Level102 场景 prefab：组件加到完整箱子实例并绑定 `debrisVisual` 与 `brokenEffectPrefab`；本关 `Box_2` 初始仅含 Sprite，运行时由 `addDebrisColliderIfMissing=true`、`addDebrisRelayAtRuntime=true` 自动补齐非 Trigger Collider 和 relay。

## 行为基线与实现

Level007 的 `Box_2` 本身只有静态 Sprite 表现，碎裂粒子由事件链单独播放。本组件保留这一资源边界，把触发条件改为 Level102 的 Player/Actor 真实接触，并在初始化时关闭箱子上的 `DragableItem`、将完整箱子固定为 Static。当前 Level102 Prefab 的 `requireTopContact` 默认开启，完整箱子需从顶部踩到；首次碎裂时自动给 `Box_2` 补非 Trigger Collider 和 relay，角色再次碰到残骸会播放同一反馈并隐藏残骸。

### 2026-09-01 relay 重置修复

残骸 relay 只在首次由完整箱子接管时捕获自身的初始渲染/碰撞状态；Level102 开启运行时 relay 后，`ResetRuntimeState()` 会同步清除二次碎裂标记、恢复 relay 状态并隐藏残骸。该路径只影响同一实例复用/手动复位；标准 `PlayCtrl` 失败流程仍会销毁并重建关卡。

### 2026-09-02 残骸重新武装时序修复

首次踩碎箱子时记录触发角色的 `ActorCtrl`，残骸显示后先保持其 Collider 关闭。组件在 `FixedUpdate` 节奏下检查该角色所有启用的 `Collider2D` 与残骸世界 `Bounds` 的 XY 投影；连续达到 `debrisClearFixedFrames`（当前默认 2）帧均不重叠后，才将 relay 标记为 armed 并启用 Collider。这样角色仍站在原箱位置时不会在下一物理帧立即把残骸再次碎裂；角色离开后重新接触仍会触发第二次碎裂。二次碎裂确认后会停止任何残留武装协程并关闭 Collider。动态添加或预先禁用的 relay 已避免在启用后的延迟 `Start` 中重复初始化，以免误清离开检测协程。复位、禁用和销毁会停止等待协程并清理角色记录/armed 状态。

## 验证与剩余风险

已完成源码静态检查、括号计数和 `git diff --check`；本次 relay guard/Reset、角色离开后重新武装逻辑及 prefab 残骸自动补建开关已回读。当前环境没有 Unity 编辑器，碰撞层矩阵、物理帧时序、残骸显示/二次碎裂和粒子排序仍需在 Level102 prefab 集成后通过 PlayMode 验证。自动补建使用矩形 Collider，若后续美术轮廓变化需在 Inspector 显式绑定更精确 Collider；若角色始终卡在残骸边界，按设计残骸会保持未 armed，需先让角色离开。

## 回滚

移除 Level102 prefab 上的 `Level102BoxBreakOnActor` 组件及本目录新增脚本/文档即可；共享 Box 与 `BoxBroken` 资源无需回滚。

## 2026-09-02 当前需求覆盖

此前关于“残骸重新武装、再次接触后二次碎裂”的段落是历史实现记录，当前需求以 Prefab 的 `debrisInteractWithActor=false` 为准：残骸只用于显示，不创建/启用 Collider 或 relay，角色经过残骸不会再次触发爆炸，也不会被残骸阻挡。完整箱子仍关闭 `DragableItem` 输入，但 `Level_102.prefab` 的 `lockBodyUntilBreak=false` 使其保留 authored Dynamic 物理；角色从顶部接触后只触发一次箱体碎裂反馈。

当前静态已回读 `Level102BoxBreakOnActor.cs` 与 `Level_102.prefab` 字段；Unity PrefabImporter、角色实际碰撞层和箱子推动手感仍为 `uncertain`。回滚仅需恢复本组件与 Level102 Prefab 的局部字段，不修改共享 Box 或 `BoxBroken` 资源。

## 2026-09-02 残骸姿态补充

- `alignDebrisToBox=false` 是当前 Level102 的有效配置。`ActivateDebris()` 不再把残骸瞬移到完整箱子的世界位置，而是保留 `Box_2` 在场景中 authored 的初始位置和高度；`ResetRuntimeState()` 也会恢复该快照后再隐藏残骸。
- 已静态回读 `Level_102.prefab` 的 `Box_2` 初始位置与 `alignDebrisToBox: 0`。残骸显示位置、排序和碎裂特效的实际画面仍需 Unity PlayMode 验证。

## 2026-09-02 激活时恢复 authored 高度

当 `alignDebrisToBox=false` 时，`ActivateDebris()` 每次显示残骸都会恢复 `debrisInitialPosition`、`debrisInitialRotation` 和 `debrisInitialScale`。该快照来自场景中的 `Box_2` 初始摆放，避免运行时或前一次尝试留下的位移让残骸漂高；`alignDebrisToBox=true` 时仍保留对齐完整箱体的旧行为。源码静态已确认，最终高度和排序仍待 Unity PlayMode。

## 2026-09-02 横向跟随补充（覆盖上一段的位置表述）

当前 `alignDebrisToBox=false` 分支不是完全固定残骸位置：显示时以场景 `Box_2` 初始快照恢复 Y/Z、旋转和缩放，仅把 X 替换为完整箱子碎裂瞬间的世界 X。箱子被推到哪里，残骸就留在对应横向位置，但高度始终参考场景最初摆放值。`debrisInteractWithActor=false` 继续保证残骸没有 Collider 或二次爆炸。该结论已由源码和 Prefab 字段静态回读，实际视觉仍需 PlayMode 验证。
