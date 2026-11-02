# Level129 拉弓动画时长调整与风险记录

## 需求与基线

- 2026-09-08 用户要求将当前关卡的拉弓动画总时长缩短为原来的一半。
- 修改前，`Level129ActorCtrl` 依次完整播放 `draw_pull`、`draw_hold`，随后进入 `draw_shoot`；三段 `TrackEntry` 均使用默认播放速度。

## 修改范围与行为

- 仅修改 `Level129ActorCtrl.cs`，在本关角色子类中把 `draw_pull` 与 `draw_hold` 的 `TrackEntry.TimeScale` 设为 `2`。
- 两段动画各自的实际播放时长均变为原来的二分之一，因此进入 `draw_shoot` 前的拉弓总等待也变为原来的二分之一。
- `draw_shoot` 的播放速度、`arrowReleaseNormalizedTime` 放箭帧、`arrowHorizontalSpeed` 箭飞行速度和 `shotRecoveryDuration` 射后恢复时间均保持不变。
- 不修改共享 `ActorCtrl`、Level78 轨迹代码、Prefab、UI、资源、存档、事件或关卡统计链路。

## 验证与风险

- 使用 Unity 6000.0.59f2 当前 `Assembly-CSharp.rsp` 进行完整 Roslyn 定向编译；最终结果记录为 `exit 0 / 0 error`。
- 静态核对确认倍速只应用于 `draw_pull`、`draw_hold`，没有应用于 `draw_shoot`。
- PlayMode、不同皮肤、暂停/恢复、Reset/Replay 与 Android/iOS 真机尚未验证，状态为 `uncertain`。
- 拉弓音效仍在 `draw_pull` 开始时播放；音频本身不会随 Spine 动画倍速压缩，需试玩确认听感是否匹配。

## 回滚

- 删除 `BowDrawAnimationTimeScale` 常量及 `pullEntry.TimeScale`、`holdEntry.TimeScale` 两处赋值即可恢复原时长。

## 2026-09-08 弓箭拖尾发射边界修正

- 用户要求弓箭发射前不显示拖尾，只在实际飞行过程中显示。
- 修改前，`CreateArrowTrail` 创建拖尾后立即开启发射，而箭根节点随后才由 `ApplyArrowTrajectory` 移到真实发射点；初始化位置变化可能被拖尾采样。
- 修改后，拖尾创建时保持 `emitting=false`。箭根节点定位到轨迹起点后，再次清空历史位置并设置 `emitting=true`，从这一刻开始记录飞行轨迹；命中时仍由原流程关闭发射。
- 修改只影响 `Level129Ctrl.cs` 的飞行箭拖尾时序；弓箭 Spine 待发附件、拉弓动画倍速、箭飞行速度、轨迹、伤害和命中停留保持不变。
- 使用 Unity 6000.0.59f2 当前 `Assembly-CSharp.rsp` 完整 Roslyn 定向编译，最终结果为 `exit 0 / 0 error`；静态检查确认开启拖尾发生在初始轨迹定位之后。
- PlayMode、连续射击、命中停留、Reset/Replay 及 Android/iOS 真机显示仍未验证，状态为 `uncertain`。
- 回滚时将 `CreateArrowTrail` 恢复为创建后直接开启，并删除 `ApplyArrowTrajectory(..., 0f)` 后的清空与开启代码；这会恢复初始化位移可能形成拖尾的旧行为。

## 2026-09-08 Mission Complete 技能清理

- 用户要求出现 `Mission Complete` 时销毁场景内仍存活的 Boss 技能。
- 修改前，Boss 败北后技能循环不再开始下一轮，但当前地刺/预警、火球、扇形刺、滚石或流星对象仍按各自协程和 Tween 生命周期继续存在；成功展示只隐藏准星，并在进门时关闭伤害碰撞。
- 修改后，上述五类技能及地刺预警的运行时根对象同时登记到本关 `activeSkillObjects`。Boss 淡出完成后、调用 `ShowDefeatedBanner` 前，停止当前 `bossSkillLoop`，关闭所有技能伤害碰撞，立即隐藏并销毁仍登记的技能对象，再显示 `Mission Complete` 和落下出口门。
- 玩家飞行箭、命中短暂停留箭、命中特效和血条碎片仍属于普通 `runtimeObjects`，不会被本次技能清理误删；关卡退出或销毁时继续由原 `CleanupRuntime` 统一清理。
- 本修改不调整 `BaseLevelCtrl` 成功判定、进门流程、`level_end`/`level_end_success_N` 事件顺序和运行统计，符合 `DevelopMD/关卡打点生命周期约定.md` 的成功结算约定。
- 使用 Unity 6000.0.59f2 当前 `Assembly-CSharp.rsp` 完整 Roslyn 定向编译，最终结果为 `exit 0 / 0 error`；静态核对确认所有技能对象入口均使用 `RegisterSkillObject`。
- PlayMode 尚未验证 Boss 在每一种技能过程中败北、同帧伤害竞争、流星子协程退出、门落下和移动端表现，状态为 `uncertain`。
- 回滚时把六处 `RegisterSkillObject` 恢复为 `RegisterRuntimeObject`，移除 `activeSkillObjects`、`ClearActiveSkillObjects` 及 Boss 败北流程中的调用；不要回退本关其它用户修改。

## 2026-09-09 射箭被取消移动打断后卡死修复

### 需求、基线与证据

- 用户反馈 Actor 可能卡死、不再进入下一轮射箭，并授权修复。
- 事实：`BaseLevelCtrl.OnPlayerMoveCancel -> StopMove -> ActorCtrl.UpdateAnimation -> HandleAnimators` 会直接刷新普通动画。本关此前只在 `OnUpdate` 内用 `archeryActive` 拦截，未覆盖此调用路径。
- 直接结论：射箭期间取消移动可覆盖射箭轨道；原流程只监听 `Complete`，轨道在完成前被移除时可能一直保留 `archeryActive=true`，阻止移动与后续射箭。
- 现场归因仍为 `uncertain`：本轮未取得卡死现场的轨道状态或完成 PlayMode 复现。

### 范围与修复后行为

- 仅修改本关 `Level129ActorCtrl.cs`，并更新本文与 `DevelopMD/已有功能设计与使用记录.md`；共享 Actor、BaseLevelCtrl、其它关卡、Prefab、GF 均未修改。
- 覆写 `HandleAnimators`：射箭流程持有轨道期间拦截普通动画刷新，覆盖共享取消移动入口。
- 拉弓、持弓和射箭条目订阅 `Interrupt/End`。异常时立即解绑条目、标记中断，在下一次非暂停的角色更新中复用 `CompleteArchery` 停止协程、隐藏待发箭、恢复移动与弓显示。
- 正常阶段切换先解绑，再播放下一动画，不触发异常收尾。骨骼重建也标记当前射箭中断。
- 放箭协程遇到中断不再读取旧条目的时间，也不补发箭；已发射的箭仍归 Level129Ctrl 管理，不被回收或重复发射。
- UI/资源流复用已有 `activeOnComplete -> CompleteBowRecovery`，恢复携弓显示；不新增全局事件或改变存档、配置、打点、资源加载与平台 SDK。
- 保留 pull/hold 两倍速、shoot 原速、放箭帧与正常恢复时长。异常收尾有意取消本轮未完成射击，允许下一次重新射击。

### 风险与验证

- 已执行：使用 Unity 6000.0.59f2 的 NetCoreRuntime/dotnet 与 DotNetSdkRoslyn/csc，读取当前 `Library/Bee/artifacts/1300b0aEDbg.dag/Assembly-CSharp.rsp`，只把 out/refout 重定向至临时目录进行完整编译；exit 0，0 error，保留项目已有警告。
- 编译日志：`%TEMP%/BrainPunk-Level129-Check/compile.log`。未覆盖 Unity/Bee 原有程序集。
- 静态复核：正常完成先解绑；中断后不补发；暂停中不提前恢复移动；死亡、停用和清理仍先取消事务并解绑，不走异常完成回调；本次目标文件 diff 空白检查通过。
- 全仓 diff 检查发现任务前已有 `en SDF.asset` 空白问题，未调整该字体资源。
- PlayMode、Android/iOS 横屏、实际音画与多指事件时序均为 `uncertain`，不得视为已验证。
- 待试玩：持方向开始射箭，在 pull/hold/shoot 三阶段松手，确认动作完整且可继续移动、重复射击；每阶段强制替换/清空轨道，确认未发箭不补发、已发箭不重复、弓恢复；正常连续射击只发一箭；暂停/恢复、射箭中死亡、Reset/Replay 与退出重进无残留回调；重建骨骼后不永久锁定。
- 当前兜底处理轨道中断与重建，不以超时强制结束合法暂停；无中断事件的外部永久冻结仍需现场证据另查。

### 回滚

- 仅撤销本轮 `HandleAnimators` 覆写、中断标记、Interrupt/End 监听、重建中断标记与放箭协程的中断检查，恢复前一版射箭流程；保留 9 月 8 日倍速、拖尾与技能清理修改。
- 若正常动画衔接被误取消、箭重复发射或暂停期间提前恢复移动，停止验收并按上述范围回滚。无待确认的需求冲突；规则引用的 `../specs/037-remote-resource-loading/plan.md` 当前不存在，已按本关源码及现存文档检查。
