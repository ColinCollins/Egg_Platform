# DLevel_079 脚本结构记录

## 2026-09-20：进球 FIFA Goal 表现接线补齐

### 本轮需求与根因

进球后先展示 `FIFAGoal` 界面并播放 `whistle`，等待 `0.5s` 后再进入共享胜利结算。重新核对当前工作区与 Git 历史后确认：`DLevel79Ctrl` 和 `DLevel79GoalSuccessHandle` 中的实现仍然存在，并未被合并还原；实际缺失的是 `DLevel_079.prefab` 的资源接线。

修复开始时（HEAD `39d544c78`）Prefab 的可观察事实为：`DLevel79Ctrl.fifaGoal` 仍是空引用，`SuccessArea` 仍绑定旧的共享直接成功组件，根节点也没有 `FIFAGoal.prefab` 实例。按本关局部成功组件 GUID 和 FIFA Prefab GUID 追溯当时的 Git 历史，没有找到包含这些接线的已提交 DLevel79 Prefab。因此直接结论是：上次说明文档记录在实际 Prefab 落库之前，接线没有进入 Git；不是合并把一份已经提交的接线还原。

本轮处理中分支随后前进到 `5a72b56b6`（`DLevel79 bug fixed`），该提交已包含 FIFA 与 Trigger 接线，但 Unity 当时保存的 Inspector 覆盖值为 `goalVictoryDelay: 1`。当前工作区已按需求再次改为 `0.5`；这个最后的默认值修正尚未提交。

### 最终脚本树与调用链

```text
DLevel_079
├─ DrawLevelCtrl：共享输入、尝试与胜负状态
├─ DLevel79Ctrl：首笔动作、踢球、进球表现占用与延迟完成
├─ FIFAGoal（嵌套 Prefab，默认隐藏）
└─ SuccessArea
   └─ DLevel79GoalSuccessHandle：只把足球 Trigger 转发给 DLevel79Ctrl
```

实际进球链为：足球进入 `SuccessArea` → 本关 Handle 校验足球引用 → `TryBeginSuccessPerformance` 占用共享成功演出 → 显示 `FIFAGoal` 并播放 `whistle` → 使用受暂停控制的 `0.5s` 等待 → `CompleteSuccessPerformance` → 进入原有胜利结算。共享 `DrawLevelCtrl`、主线 Level87 和 FIFA 源 Prefab 均未修改。

### Prefab 显式绑定

- 根节点新增 `FIFAGoal.prefab` 嵌套实例，资源 GUID 为 `09908bc8571d79c41b7652073385df55`，默认 `SetActive(false)`；其布局取自主线 Level87 已接受的实例结构。
- `DLevel79Ctrl.fifaGoal` 显式绑定该实例，`goalVictoryDelay` 保持 `0.5f`。
- `SuccessArea` 改绑本关 `DLevel79GoalSuccessHandle`（GUID `045d9061cbcd4018908dadd763fd4860`），`levelCtrl` 显式回指根 `DLevel79Ctrl`，不再直接调用共享成功链。
- 本轮只修改 DLevel79 Prefab 接线，已有两个本关脚本不需要改动，也未增加全局事件或运行时名称扫描。

### 验证、风险与回滚

- YAML 静态检查：175 个锚点无重复，所有非零本地 `fileID` 引用闭合；FIFA 资源、局部 Handle 和 Ctrl 引用均存在，旧共享直接成功组件 GUID 已从本关 Prefab 移除。
- 资源检查：所有外部 GUID 均能在工程 `.meta` 中解析；FIFA 实例默认隐藏，避免开场误显示。
- Unity 6000.0.59f2 当前 Bee `Assembly-CSharp.rsp` 由 Roslyn 全量编译退出码为 0；FIFA 与 Trigger 接线版本曾由 Unity `PrefabImporter` 成功重新导入。最后把 Inspector 延迟从 `1` 改回 `0.5` 后，磁盘 YAML 已复核，但当前 Editor.log 尚未出现这次末次修改的新导入记录；该证据不等同于进球流程实测。
- `uncertain`：尚未在正式 DLevel79 PlayMode 中实际触发进球，因此 FIFA 画面层级、吹哨听感、暂停中的 0.5 秒冻结、胜利转场及 Android/iOS 横屏表现仍需运行验证。静态引用闭合不能替代运行时验收。
- 回滚仅限本关 Prefab：移除 FIFA 嵌套实例及根子节点引用、清空 `fifaGoal`，并把 `SuccessArea` 恢复为原共享直接成功组件及 `drawLevelCtrl` 引用；无需修改共享脚本或源 Prefab。

## 需求与参考基线

2026-09-18：第一笔有效画线完成后，Actor 向右行走指定 X 距离，停下朝左，播放蓄力和踢球，给足球一次可调方向、大小的冲量。本次交付范围为动作机制与 prefab 绑定，不新增进球胜利条件。

参考 DrawLevel 中央结构规范、D001 的首笔移动入口、D070 的 Actor 子类动画保护，以及主线 `Actor/Level84ActorCtrl.cs` 的足球动作。Level84 当前源码连续播放两次 `football_shoot_ready`，再播放 `football_shoot`；79 关保留这个顺序，不复用主线84的选项、enemy、拖球或结算流程。

## 原始状态与最终脚本树

任务开始时 DLevel_079 为用户新增的未跟踪 prefab，Actor 已局部替换为 DLevel11ActorCtrl，具有50度 Draw 层陡坡限制；根节点有 DrawLevelCtrl、DrawLineFeature、10秒失败倒计时，没有首笔行走控制。足球为 Kinematic，带启用的 DragableItem，两个复制的 Level84 UnityEvent 目标为0。

```text
DLevel_079
├─ DrawLevelCtrl：共享输入、尝试与胜负状态
├─ DrawLineFeature：有效笔画与物理画线
├─ DrawLevelCountdownCtrl：保留原10秒超时失败
├─ DLevel79Ctrl：首笔、距离、动作推进、足球施力
├─ Actor
│  └─ DLevel79ActorCtrl : ActorCtrl：本关朝向、足球动画及原陡坡限制
└─ football
   ├─ Rigidbody2D：等待时Kinematic，踢球时Dynamic
   └─ DragableItem：在本关禁用，避免与画线触摸争用
```

## 职责、接口与生命周期

- DLevel79Ctrl 实现既有 `IDrawStrokeCompletedHandler`。仅 Waiting 状态接受 IsFirstStroke；目标为首笔时的刚体世界X加 moveRightDistance，后续笔画和重复通知不改目标。
- Moving 使用 `SetMoveInput(1)` / `OnUpdate` 的真实物理行走。检测刚体到达目标后仅校正越过的终点X、保留高度，再清水平速度、朝左、启动动作；被障碍或陡坡阻挡时不会按时间穿越障碍。
- DLevel79ActorCtrl 直接继承 ActorCtrl；不修改共享 Actor 或原 D11/主线84。沿用当前 prefab 的坡度字段与接触法向判定，将足球动作隔离在79子类。`SetFacingRight` 使用父类受保护的幂等朝向入口。
- `PlayFootballShoot` 先核对两种动画；Complete 只记完成，由 `TickFootballShoot(paused)` 在未暂停时推进两次蓄力和一次踢球。仅开始踢球的这一帧返回 true，Ctrl 随后施力。动作期间保护共享 HandleAnimators，保留接触检测与垂直物理。
- 踢球开始时足球切 Dynamic，`AddForce(normalized direction * impulse, Impulse)` 一次。方向使用世界坐标；方向零或力度零不增加冲量。没有按距离探测足球或等待实际脚碰撞。
- 暂停停止角色水平推进并冻结本脚本拥有的 Spine 轨道；已完成但未消费的阶段在恢复后才推进，不在 Complete 回调直接施力。
- Reset 解绑并清除仍属本脚本的动画，恢复首次有效笔画时保存的角色位置/朝向和足球类型、位置、旋转、速度，等待新首笔。正常 Reset/Replay 仍为共享整关重建。
- Resolved、角色死亡/禁用、控制器禁用及动画外部替换取消剩余动作；清轨前解绑，不清除外部后来替换的动画。踢球后角色保持原地，不恢复自动行走。

## 序列化绑定与调参

| 所属组件 | 字段 | 最终绑定或初值 |
| --- | --- | --- |
| DLevel79Ctrl | drawLevelCtrl | 根组件900000000000000001 |
| DLevel79Ctrl | actor | 原局部Actor组件900000000000000011，替换为D79子类 |
| DLevel79Ctrl | football | 足球RB 5273557179364374802 |
| DLevel79Ctrl | moveRightDistance | 2，世界X距离；0原地动作 |
| DLevel79Ctrl | kickDirection | (-1,0)，向左；(-1,1)向左上 |
| DLevel79Ctrl | kickImpulse | 20，单次冲量 |
| DLevel79ActorCtrl | spineCtrl | 原Spine组件3586672669756989625 |
| DLevel79ActorCtrl | maxWalkableSlopeAngle / slopeCheckLayers | 50 / Draw层16384 |
| DLevel79ActorCtrl | IsEditor | 0，避免编辑器键盘输入覆盖自动行走 |

Actor 组件fileID不变，已有辅助组件的 actor 引用仍指向它；原基础 Actor prefab 与足球质量1、重力20、线性阻尼5、碰撞/Trail配置保留。新 Ctrl、Actor 脚本 GUID 分别为 `29d0ac53bacf08c49a0a6f00d30d4dcb`、`2b3d37f28a40c5c4d9fbdaf093b8700c`。

## 数据、资源、UI与事件边界

有效笔画 → 共享局部 Handler → D79移动 → D79 Actor动画完成标记 → D79未暂停推进 → 足球冲量。只订阅本关 Resolved，不新增 PlayEvents、UI、配置、资源加载、存档或打点调用。

当前默认 SkeletonData 指向 `Spines/sp_Box_001/Box_SkeletonData.asset`。原始 Box.json 有 ready（1秒）和 shoot（0.5秒），没有踢球事件帧；本实现施力时机为踢球动画开始。保留足球原触地空目标事件，不接回主线84，不声称当前篮筐已有进球成功条件。

## 验证证据与边界

- 主工程 Unity6000.0.59f2：Editor.log 1339169 编译 Assembly-CSharp，1339424复制产物，1339706完成域重载，1339738重新导入D079；本次编译窗口没有 error CS，编译响应包含两个新脚本。
- 最终 prefab 171个唯一锚点、337处本地引用闭合；3个原块修改、1个新块、167个原块逐字保持，meta GUID仍为232360d210578074b978c970dffa0a50。新增根组件、Actor替换、拖拽禁用为本次变化，未改共享/旧关资源。
- 独立 Unity PlayMode 42项通过，逐字运行生产D79脚本、真实 Physics2D 和 Spine AnimationState：首笔/重复、实际距离、左朝向、两次蓄力、单次冲量、方向归一化、零距离/零方向、暂停与已完成阶段、Reset/禁用/死亡/Resolved/外部中断、缺踢球动画。
- 第一轮夹具把所有动画清空，桩Actor随后播放idle失败；修正为只移除football_shoot后42项通过，保留第一次输出。不把夹具错误当生产故障。
- 证据：`C:/Users/admin/AppData/Local/Temp/brainpunk-dlevel79-20260918/` 下 before.prefab、main-compile-evidence.txt、prefab-verification.json、Probe.cs、editor-retry.log、probe-result.txt。
- uncertain：隔离测试的共享Actor/Draw为桩且动画无真实时间线；未运行主工程整关，实际坡面、角色/球接触、画线质量200、球重力/阻尼下弹道、可解性、10秒临界失败、实际动画视觉接触、转场/Replay/移动端横屏待实测。局部暂停未接管整关既有物理暂停策略。

## 结构复盘与回滚

本关沿用“局部Ctrl持共享Draw + Actor子类隔离单关动作”。主线84既有异步API没有踢球开始回调，直接复用无法在暂停/重置边界安全施力；因此仅引用其动画序列，采用本关持有轨道与受控推进，不修改已验收关。陡坡代码保留局部复制，暂无稳定共享抽象证据，候选留待后续多关复盘。

回滚仅移除根DLevel79Ctrl组件及引用，把原Actor局部组件脚本GUID恢复83b9575e896443bfa7dba5480f74493f、IsEditor恢复1，按需恢复本关DragableItem启用，再移除新增Level79脚本/文档/meta。不整份覆盖用户继续编辑的地图；外部before快照仅供逐块比较。
