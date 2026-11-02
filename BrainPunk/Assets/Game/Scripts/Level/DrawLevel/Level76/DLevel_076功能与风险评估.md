# DLevel_076 功能与风险评估

## 需求、原行为与范围

2026-09-18：CuteActor 向右行走；Actor 和 CuteActor 分别进同一门并隐藏，两者都触发后通关。本次将 CuteActor 的行动时机对齐原玩家第一笔有效画线完成，双人进门且开门完成后再提交胜利。

2026-09-20：门完整打开后不再因第二个角色进入而重播开门动画；若第二个角色在开门过程中到达，则复用同一 TrackEntry，等待该次开门完成后结算。

原 Prefab 为新增未跟踪资源，玩家由 D001 首笔后右移；第二角色 Rich/box_pink 未挂独立移动控制；门过滤 Player 并调用共享单人成功入口；已有 10 秒超时失败。以任务开始快照为证据，不能把原配置直接当成原实际体验。

修改范围：DLevel76Ctrl.cs/.meta、本关 Prefab、模块结构/风险文档、中央演进与当日使用记录。只绑定和驱动本关第二角色，门条件改为双人；玩家原行走、地图/物理参数、计时器和资源保持。没有修改共享脚本、源 Prefab、旧关、Luban、SDK、Packages 或 Assets/GF。

## 复用系统与行为变化

- 首笔通知仍由 DrawLevelCtrl 的 IDrawStrokeCompletedHandler 分发；CuteActor 复用 ActorCtrl.SetMoveInput/OnUpdate。
- 原单人成功入口由本关 OnDoorEnter 替代，过滤空 Tag 但严格比较两个绑定刚体；防止 tools 标签的其它物体计数。
- 第一人只启动一次开门、停移动并隐藏自己，不锁定胜利；第二人开始时才占用标准成功门禁。第二人在动画期间到达时等待当前 Complete，动画已完成后到达时直接收口，均不再发起第二次 `door_open`，并继续走现有 Resolved、UI、进度与打点链。
- 本次门表现仅 door_open 与角色直接隐藏；不调用原单人完整 ribbon/彩纸/关门及 Actor 渐隐。无新动画资源、存档、UI 组件或全局事件。
- 10 秒失败逻辑保留；第一人入门后另一人仍可能因超时而失败，第二人演出阶段成功门禁阻止竞争结果。实际临界帧先后顺序未实测。

## 风险、处理与证据

| 风险 | 处理及已验证证据 | 具体缺口 |
| --- | --- | --- |
| 同一人多个 Collider 导致双计数 | 两个绑定 RB 身份和独立进门标记；重复入口检查通过 | 实际物理触发组合 |
| 第一人入门后另一人不能活动 | 仅第二人占用成功门禁；两种顺序及交叠/分时检查通过 | 正式地图移动与输入 |
| 第一人直接导致单人成功 | 本关门事件替换原 OnTriggerSuccess，动态参数与 Tag 检查通过 | 其它人工新增成功入口不在本次范围 |
| 进入角色隐藏后原移动脚本仍执行 | 入门调用 StopMoving 和 SetMoveInput(0)，CuteActor 不再推进；保留原 D001 | 主工程真实 Actor OnUpdate/动画 |
| 第二人进入导致开门重播或替换首个轨道 | `_openEntry` 存在时复用进行中的动画，`_doorOpened` 为真时跳过动画并直接收口；生产脚本只保留一个 `PlayAnimation("door_open")` 调用点 | 正式关卡中两人到门的快慢组合与实际视觉 |
| 外部中断导致残留成功门禁 | 取消当前演出并恢复对应角色；中断/重试/禁用/晚推进通过 | 主工程暂停、销毁和外部动画调用 |
| 缺绑定、缺动画或错误双引用 | 入口拒绝、缺动画不隐藏；隔离缺动画/误绑检查通过 | 实际资源初始化与可见性 |
| 重置保留隐藏角色或已开门状态 | 本地 Attempt Reset 恢复两角色、清除 `_doorOpened` 并让门回到待机；Disable 同样清状态 | 正式 Reset/Replay 的黑幕重建 |
| Layer/Tag 导致第二人无法到门 | 原 tools/Layer11 保留，门过滤空 Tag，API 限制 RB；静态引用正确 | Physics2D 碰撞矩阵和实际进门位置/地形 |
| 倒计时与最终开门竞争 | 第二人开始占用共享结果门禁，原倒计时保持 | 10 秒临界帧、HUD 倒计时及失败回调 |

## 验证记录

- Unity6000.0.59f2 主工程编译完成并重载 Assembly-CSharp；新脚本独立 Roslyn 检查 0 error。
- Prefab 静态通过：120 唯一锚点、本地引用闭合、四个显式绑定、门动态事件和两个 Actor 实例正确。3 个原块修改、2 个新块、0 原块删除、115 其它原块保持。
- 十份现有脚本/资源保护哈希保持，包含 D75、D001、D11、共享 Draw/Actor、倒计时及门/Actor 源 Prefab。
- 2026-09-18 的独立 Unity6000.0.59f2 PlayMode 44 项使用当时生产 D76 脚本，覆盖真实 2D 刚体/碰撞体和 spine-csharp 状态机。Draw 控制器、Actor 控制器、门包装和 Logger 为替身，动画为合成时间轴；该历史结果不覆盖 2026-09-20 新增的单次开门分支。
- 第一次独立项目因未启用内置 Physics2D 模块未编译；只补独立临时项目依赖后重跑通过，主工程未发生此编译错误。
- 证据目录：C:/Users/admin/AppData/Local/Temp/brainpunk-dlevel76-20260918/，含原 Prefab、static-validation.json、protected-hashes.json、probe-result.txt（CHECKS=44）、验证夹具及 Unity 日志。临时证据可能清理，仓内保留范围和结果。
- 2026-09-20 使用当前 Unity Bee `Assembly-CSharp.rsp` 执行全运行时 Roslyn 编译，退出码 0；6 项静态断言确认唯一 `door_open` 调用、开门中复用、已开门直结、Complete 单点置位、Reset/Disable 清状态和双角色胜利条件，`git diff --check` 通过。

## 待实测项与回滚

以下为 `uncertain`：2026-09-20 调整未重新执行 PlayMode；正式关卡有效首笔与 CuteActor 真实 walk/碰撞、Actor/CuteActor 分别从两种顺序进门、第二人在开门中与开门后两种时机、第一人隐藏后另一人仍能到门、原计时器最后一帧竞争、真实开门与 Complete、胜利 HUD/翻页/进度/打点、整关失败/Reset/Replay、Android/iOS 横屏和暂停恢复。至少在正式关卡按两种顺序与两种到达时机各跑一次成功，再跑只到一人后的超时失败与重开。

回滚只撤销本关新增控制组件、对应 stripped 引用和门事件/Tag override，并删除新脚本；恢复原单人成功条件会取消双人通关需求。保留人工地图编辑，不改旧关或共享文件。第二角色是否继续命名 CuteActor 按当时人工场景版本决定。
