# DLevel_075 功能与风险评估

## 原行为、需求与范围

2026-09-18 开始时，本关为新增未跟踪 Prefab；包含 DLevel001Ctrl 驱动玩家首笔右移、DLevel11Ctrl 驱动 enemy 首笔后延迟 0.5 秒右移。FailedDoor 已过滤 enemy，但 UnityEvent 仍指向 DrawLevelCtrl.OnTriggerSuccess；这只能证明序列化入口配置，不证明实际进门表现。

本次按用户要求新增 DLevel75Ctrl，enemy 首笔完成后无延迟右移，并公开 PlayFailedDoorAndFail。调用时播放现有非循环 door_open、隐藏 enemy，动画完成后通过标准 Draw 失败入口发送失败事件。先占用共享失败演出门禁，避免重建打断开门或重复结算。

涉及文件：本目录 DLevel75Ctrl.cs/.meta、DLevel_075.prefab、本目录结构记录和本评估、中央结构演进记录及当日使用记录。保留地图、Actor 参数与成功门；不改 Level11、共享脚本、配置表、Packages 或 Assets/GF。任务期间工作区出现其它关卡 Prefab 编辑，这些不属于本次写入。

## 复用系统与流向

画线完成 → DrawLevelCtrl 的 IDrawStrokeCompletedHandler → 本关 enemy 的 ActorCtrl.SetMoveInput/OnUpdate。

FailedDoor enemy Trigger / 公开 API → TryBeginFailurePerformance → ActorSpineCtrl.PlayAnimation → 隐藏 enemy → Spine TrackEntry.Complete → CompleteFailurePerformance → TryFailAndReset → DrawGameResetEvent(Failed) → 原失败重建与打点链。

无新增存档、配置、资源加载、共享 HUD 或全局事件类型。动画中断/组件禁用时解除未完成回调并取消演出；不自行发送失败。

## 风险与证据

| 风险项 | 当前处理/证据 | 未验证范围 |
| --- | --- | --- |
| 重复 API、多个 Collider 或两门竞争 | DrawLevelCtrl 既有失败门禁及本地 TrackEntry 持有状态阻挡重入；静态核对 | 真实同帧物理回调顺序 |
| 两份脚本同时驱动 enemy | 本关不再挂 DLevel11Ctrl，仅 D75 控制 enemy；Prefab 静态通过 | 完整玩家/敌人移动与可解性 |
| Spine 动画丢失或引用误绑 | 必填 Inspector 引用，检查 AnimationState 和 door_open；原始 JSON 含该动画 | 实际导入后的初始化、Complete 和可见帧 |
| 开门立即被失败重建打断 | Complete 后才提交标准失败，演出阶段占用结果与输入门禁 | 实际开门节奏、HUD 阻挡和失败转场 |
| 动画中断后卡住 | Complete/Interrupt/End 回调成对解除；中断恢复触发前 enemy 显隐并取消门禁 | 真实动画替换、清轨、禁用与晚到回调 |
| 暂停/死亡期间持续移动 | Update 读取暂停、结果锁、enemy 活跃和死亡状态，清除本组件输入 | 移动端暂停及物理速度表现 |
| 人工复用已结算实例 | 本次按产品标准整关重建边界实现 | 已结算后直接手动 Attempt Reset 的显隐/门姿态，uncertain |
| 嵌套门引用或编辑覆盖 | 最终检查 sourceObject、PrefabInstance、事件目标和模式，并逐门核对 | 若之后 Inspector 保存旧快照，应再次确认绑定 |

## 验证记录与验收清单

- Unity 6000.0.59f2 主工程 Assembly-CSharp 自动编译完成，并完成程序集重载。
- 独立 Roslyn 检查新脚本：0 error；重复类型与 Inspector 字段未赋值 warning 是独立编译上下文产生，不能用其判断 Prefab 绑定。
- Prefab：126 个 YAML 锚点唯一、非零本地引用闭合；D75 单一组件、三个引用、enemy 过滤和 Void 事件模式、成功门事件均核对。
- door.json：door_open 动画存在；本关无新增动画、存档、DB、全局事件类型或编辑态入口。
- 原始检查产物：`C:/Users/admin/AppData/Local/Temp/brainpunk-dlevel75-20260918/`（临时产物可能被系统清理，仓内保留上述结果）。

尚未执行 PlayMode 或真机，以下均为 `uncertain`：首笔前 enemy 静止；有效首笔后右移而后续笔画不重新启动；暂停、死亡与结算停止；enemy 入 FailedDoor 开门并立即消失；开门期间重复调用和玩家进成功门不重复结算；完整开门完成仅发送一次失败；动画中断不失败；Reset/Replay 重建恢复初态；标准失败 UI、打点、Android/iOS 横屏实际表现。

## 回滚与待确认

只撤销本关新增组件、失败门事件 override、新脚本和对应文档主题，不恢复或覆盖其它地图编辑。没有预先指定的 Draw75 需求文档；本次时序按“开门完成后失败”实施，若产品要开门开始即发送失败，需要同步评估标准转场是否遮住动画，再调整本关时序。
