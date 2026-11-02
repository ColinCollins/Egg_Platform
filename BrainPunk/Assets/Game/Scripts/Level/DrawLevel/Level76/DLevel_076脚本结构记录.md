# DLevel_076 脚本结构记录

## 需求、原行为与参考基线

2026-09-18：补充 DLevel76Ctrl，控制 CuteActor 向右移动；Actor、CuteActor 都能进门并隐藏，两个角色分别触发后才能通关。沿用原玩家首笔行动时机，CuteActor 也在第一笔有效画线完成后立即右移。

2026-09-20：同一轮尝试中门只播放一次 `door_open`。第二个角色在开门过程中进入时等待当前动画完成；在门已打开后进入时不重播动画，直接完成双人进门结算。

没有既有 Draw76 模块说明。当前 Prefab 第二角色起初命名 Rich，外观 box_pink、Tag tools，作为用户指定的 CuteActor 显式绑定并改名 CuteActor；保留其速度 5、碰撞、Layer、Tag 和美术引用。玩家继续由原 DLevel001Ctrl 移动。

原门过滤 Player，并调用 DrawLevelCtrl.OnTriggerSuccess；该共享入口只接受共享控制器缓存的单一玩家并启动胜利，无法表达双人分别进入条件。参考 Level75 的局部首笔移动、Level70 的动画完成后结算边界，以及共享 DrawLevelCtrl 的演出门禁。不改旧关，参考记录没有整关验收证据，不能称为已验收体验基线。

## 最终组件树与职责

```text
DLevel_076
├─ DrawLevelCtrl                初始化、画线生命周期、暂停与标准结算
├─ DrawLineFeature              原绘制逻辑
├─ DLevel001Ctrl                原玩家首笔后右移
├─ DelayAutoBeginDrawHandle     原开始绘制入口
├─ DrawLevelCountdownCtrl       保留原 10 秒超时失败
├─ DLevel76Ctrl                 CuteActor 右移、双角色进门计数、单次开门和胜利收口
└─ Level 场景
   ├─ Actor / ActorCtrl         玩家
   ├─ CuteActor / ActorCtrl     第二角色，原 Rich/box_pink
   └─ DrawGame_SuccessDoor
      ├─ OnTrigger2DHandle      动态 Collider2D → DLevel76Ctrl.OnDoorEnter
      └─ door / ActorSpineCtrl  共用 door_open
```

DLevel76Ctrl 为 MonoBehaviour，实现 IDrawStrokeCompletedHandler、IDebuger，随本关加载/销毁。只消费已绑定角色、门和 Draw 控制器，不承担配置、HUD、资源加载、存档或打点职责。不修改 DLevel001Ctrl、ActorCtrl、DrawLevelCtrl、倒计时、基础 Actor 或门源 Prefab。

## 生命周期与关键调用

1. 运行态启用订阅同关 Resolved；共享 DrawLevelCtrl 缓存 Handler 并分发有效首笔结果。
2. `OnDrawStrokeCompleted` 只接受 IsFirstStroke，关卡已初始化且未暂停/锁定、CuteActor 活跃未死亡时启动持续右移；Update 调用 SetMoveInput(1)、OnUpdate。
3. 暂停、结果门禁、CuteActor 已进门/禁用/死亡时清除本脚本施加的输入；未进门且未结算时恢复暂停可继续右移。
4. `OnDoorEnter(Collider2D)` 接收门的动态事件；按 attachedRigidbody 与两个显式绑定的 ActorCtrl.RB 比较。无关物体、重复角色、死亡角色、暂停/未初始化/已锁定结果均拒绝；两个字段误绑同一 Actor 也拒绝。
5. 第一个角色进门：检查 `door_open` 存在并播放一次非循环开门，SetMoveInput(0)、StopMoving 并隐藏该角色 GameObject；记录其进门状态，不占用成功门禁，另一个角色继续活动和绘制。
6. 第二个角色进门：先 TryBeginSuccessPerformance 占用胜利演出并记录/隐藏角色。若开门仍在进行，保留当前 TrackEntry，等待同一次动画完成；若门已经打开，不再播放动画，直接检查双人条件并结算。
7. 唯一开门轨道 Complete：记录本轮门已打开，解绑当前回调；双方均进门且成功门禁已占用时调用 CompleteSuccessPerformance(nameof(DLevel76Ctrl))，经共享 Resolved/DrawPlayCtrl 标准链结算一次。只有第一人进入时，动画完成只保留门已打开状态，不会胜利。
8. 外部替换动画或清轨：解绑当前回调，恢复触发这次开门的角色并撤回其进门记录；若第二个角色已在开门期间进入，则该角色保持已进门状态，成功门禁被取消。恢复的角色可再次进门并重新启动此前未完成的开门动画；旧回调不再提交胜利。
9. Attempt Reset：停止 CuteActor、取消未完成开门、恢复本脚本隐藏的两角色和移动能力、清除本轮门已打开状态，门恢复 `door_idle`。禁用同样解除回调/门禁、恢复角色并清状态。正常 Reset/Replay 仍整关重建，不扩展共享生命周期。

## 显式 Inspector 与 Prefab 绑定

| 字段/事件 | 本关绑定 | 边界 |
| --- | --- | --- |
| drawLevelCtrl | 同根 DrawLevelCtrl，900000000000000001 | 必填，无跨关引用 |
| actor | 玩家 ActorCtrl，1716601560298323527 | 玩家行走仍归原 D001 |
| cuteActor | 第二 Actor 实例的 ActorCtrl，900000000000000006 | 新 stripped 引用对应实例 8273351881633990398、源组件 496433471279924807 |
| doorSpine | 原成功门 ActorSpineCtrl，2984154252043883271 | 两角色共用同一门；每轮尝试最多播放一次完整开门 |
| 门 onEnter[0] | DLevel76Ctrl.OnDoorEnter，模式 0 | 动态 Collider2D 参数，关闭原单人直接胜利入口 |
| 门 targetTag | 空字符串 | 允许 Player/tools 进入；实际身份由两个绑定刚体限制 |

新脚本不增加名称查找、父子层级扫描或运行时引用兜底。门本次使用局部 door_open，不调用原单人成功入口的 Actor 渐隐、ribbon/彩纸/关门完整演出；角色直接隐藏，符合本次双人入门条件。

## 数据、资源、UI、事件流

画线 → DrawLevelCtrl 本地 Handler → CuteActor 既有移动 API。

门 Trigger → 本地动态 Collider2D API → 两个角色的独立进门标记/显隐 → 单次开门状态/当前 TrackEntry → 第二人成功演出门禁 → 已开门后共享标准胜利 Resolved。

保留现有角色与门 Spine 资源；没有新动画、运行时实例化、资源地址、Luban/远端字段、存档、SDK 或新全局事件。UI 锁定复用 DrawLevelCtrl 已有演出门禁，不改共享 GamePlayPanel。10 秒失败倒计时仍保留；仅第一个角色入门时倒计时继续，第二人演出占用后共享结果门禁拒绝竞争失败。

## 结构复盘与差异

- 双角色胜利必须累计不同身份，不能重复调用共享单人 OnTriggerSuccess，也不能第一人入门就锁死另一个人的入口。
- 玩家原 D001 保留；本关只驱动 CuteActor。入门对原 Actor 调用公开 StopMoving，避免 D001 继续手动 OnUpdate 时推进隐藏角色。
- 局部使用 Spine TrackEntry 的完成/中断边界，不新增 Actor 子类，因为没有关卡专用 Actor 动画；不开新全局事件，不迁移旧关。
- 单次开门状态仅保存在 DLevel76Ctrl 当前尝试内；进行中以 `_openEntry` 复用同一轨道，完成后以 `_doorOpened` 跳过重播，Reset/Disable 清空，不扩散到共享门组件。
- 双人分别进门仅此关形成稳定样本，共享多角色成功门封装留为候选，不实施共享升级。

## 验证、缺口与回滚

- 主工程 Unity 6000.0.59f2 自动编译/程序集重载完成；独立 Roslyn 编译新脚本 0 error。
- 120 个 Prefab YAML 锚点唯一、非零本地引用闭合；仅修改根组件表、门实例事件/过滤、第二角色名称三个原对象块，新增 D76 组件及第二角色 stripped 引用；115 个其它原块逐字保持。
- 原 door.json 确认 door_open、door_idle 存在；四个序列化绑定、动态事件目标与模式分别核对。
- 2026-09-18 的独立 Unity PlayMode 44 项使用当时生产 D76 脚本，覆盖真实 Rigidbody2D/Collider2D 身份绑定和 spine-csharp AnimationState/TrackEntry；DrawLevelCtrl/ActorCtrl/ActorSpineCtrl/Logger 为测试替身，动画使用合成 1 秒时间轴。该历史结果不覆盖 2026-09-20 新增的单次开门分支。
- 2026-09-20 使用 Unity 6000.0.59f2 当前 Bee `Assembly-CSharp.rsp` 执行 Roslyn 全运行时编译，退出码 0；静态检查确认 `door_open` 只有一个调用点、进行中复用 TrackEntry、完成后跳过动画、只在 Complete 标记开门、Reset/Disable 均清状态且胜利仍要求双角色，差异检查通过。
- 原脚本与资源十份保护哈希保持；证据在 C:/Users/admin/AppData/Local/Temp/brainpunk-dlevel76-20260918/，详见本目录风险评估。
- `uncertain`：2026-09-20 调整未重新执行 PlayMode；正式 D076 两种进门顺序、第二人于开门中/开门后到达、地图可解性、真实 CuteActor 行走/碰撞、工具层与门物理接触、真实动画可见效果及最终 Complete、10 秒临界竞争、HUD/存档打点/转场、整关 Reset/Replay、Android/iOS 横屏未实测。
- 回滚：移除 D76 组件、第二角色 stripped 引用和新脚本；将本关门 UnityEvent 恢复到 DrawLevelCtrl.OnTriggerSuccess/Player，CuteActor 名称按当前人工版本处理。旧单人自动成功逻辑会随之恢复；不覆盖地图或改源 Prefab。
