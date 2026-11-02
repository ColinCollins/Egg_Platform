# DLevel_070 功能风险评估

2026-09-18。需求：fruit CollisionEnter 触发当前 Actor 的 eat，完整播放结束后走标准 Draw 胜利结算。

## 基线、修改范围与复用

修改前 prefab 已由用户配置 DLevel4Ctrl 首笔释放 fruit、weight；没有专用 eat 胜利入口。新增 `DLevel70Ctrl.cs`、`DLevel70ActorCtrl.cs` 及 meta，在 `Assets/Game/Prefabs/DrawLevel/DLevel_070.prefab` 显式绑定 API、碰撞事件与 Actor 子类。继承与迁移只作用本关嵌套 Actor override，原物理、图像、布局、首笔列表与 prefab GUID 保留。

复用 ActorSpineCtrl、LayerCollisionDetector、DLevel4Ctrl 首笔释放、DrawLevelCtrl 成功演出门禁及 Resolved 标准结算；不改共享脚本、旧关、Package 或 GF。详细职责、组件树及数据/资源/UI/事件流见 `DLevel_070脚本结构记录.md`。

## 风险项与证据边界

| 风险 | 处理与已有证据 | 尚待验证 |
| --- | --- | --- |
| 非玩家碰撞误胜利 | Detector 筛选 Actor 层，Ctrl 再比对显式 Actor.RB | PlayMode 下 Draw 线、weight、其它 Collider 的实际碰撞 |
| 重复接触重播/重复成功 | 本关 eat 状态与共享成功演出/结果门禁；完成后解绑 | 真实同时/连续接触与一次性结算 |
| 待机或移动动画覆盖 eat | 本关子类 eat 期间停止通用更新/动画刷新；TrackEntry 中断取消 | 场景其它组件的轨道竞争 |
| 动画未播放就结算 | 预检 eat，失败取消演出；只接收当前 Complete | 实际 Spine 初始化、可见播放速度、皮肤切换 |
| Reset/禁用/晚到回调错误结算 | 解绑 Complete/Interrupt/End，取消成功占用；隔离 Spine 中断与旧回调检查通过 | 完整 Draw Reset/退出和对象销毁顺序 |
| eat 演出与倒计时、失败竞争 | 复用 TryBeginSuccessPerformance，已有失败/成功占用优先 | eat 期间到期、陷阱/落区、暂停与失焦 |
| 替换 Actor 丢失引用或配置 | Unity 序列化迁移，并重绑原 actor 引用；Prefab 往返与关键字段检查通过 | 实际 Actor 皮肤及其它运行时消费者 |
| 成功后的存档、打点、面板、转场偏差 | 本关只调用 CompleteSuccessPerformance，保留共享责任边界 | VictoryPanel/Replay/下一关与 Android/iOS，均 uncertain |

## 验证、缺口与回滚

Unity 编译与 21 项本地 Prefab/真实 Spine 时间推进检查通过；不属于整关 PlayMode 验收。dotnet no-restore 由于 NETSDK1004 未能启动，保留失败记录。检查输出和任务前副本位于 `C:/Users/admin/AppData/Local/Temp/brainpunk-dlevel70-20260918/`。

`uncertain`：完整关卡物理接触、UI、存档、打点、暂停/重开、移动端均未实测。验收时按表逐项检查；若首笔释放或 Actor 行为与基线不同，先停止本关接入并核对当前 prefab 差异。

回滚只恢复本关新增组件、Actor override 和对应引用；用任务前副本审查具体修改，保留用户并行改动及 fruit/weight 原释放列表。删新脚本前先移除 prefab 引用。不修改共享系统、不清档，不直接覆盖整个当前 prefab。
