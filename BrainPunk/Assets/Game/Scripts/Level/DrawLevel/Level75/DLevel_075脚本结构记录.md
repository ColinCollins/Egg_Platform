# DLevel_075 脚本结构记录

## 需求与参考基线

- 日期：2026-09-18。
- 需求：提供 `DLevel75Ctrl.PlayFailedDoorAndFail()`，播放 FailedDoor 开门动画、隐藏 enemy 并发送失败事件；第一笔有效画线完成后 enemy 立即开始向右移动。
- 未找到既有 Draw75 模块说明；基于当前 `DLevel_075.prefab`、Level11 敌人行走结构、Level70 局部演出门禁与共享 DrawLevelCtrl 实现。旧关结构记录仍包含未验收项，不能视为整关验收基线。
- 状态：Unity 编译和 Prefab 静态检查通过；整关 PlayMode 与移动端表现为 `uncertain`。

## 脚本树、职责与边界

```text
DLevel_075
├─ DrawLevelCtrl                 初始化、画线通知、暂停、结果门禁与标准结算
├─ DrawLineFeature               绘制与有效笔画结果
├─ DelayAutoBeginDrawHandle      原有入场绘制入口
├─ DLevel001Ctrl                 原有玩家首笔右移
├─ DLevel75Ctrl                  enemy 首笔右移与 FailedDoor 失败演出
└─ Level 场景
   ├─ Actor / ActorCtrl          玩家，原控制与引用保持
   ├─ Enemy / ActorCtrl          enemy，保留原 Actor Prefab、速度和碰撞参数
   ├─ DrawGame_SuccessDoor       原 DrawLevelCtrl.OnTriggerSuccess
   └─ DrawGame_FailedDoor
      ├─ OnTrigger2DHandle      enemy 标签 → PlayFailedDoorAndFail（无参数）
      └─ door / ActorSpineCtrl  door_open
```

- DLevel75Ctrl：`MonoBehaviour`，实现 `IDrawStrokeCompletedHandler`、`IDebuger`，随本关加载和销毁。
- 不再挂 DLevel11Ctrl，避免两份脚本同时推进 enemy；不修改 Level11、ActorCtrl、共享 DrawLevelCtrl 或成功门源 Prefab。
- 不持有玩家/UI/存档/配置/打点职责，不新增全局事件或运行时对象查找。

## 生命周期与调用链

1. 本组件运行态启用时订阅同关 `DrawLevelCtrl.Resolved`；共享控制器缓存并分发画线 Handler。
2. `OnDrawStrokeCompleted` 只接受 `IsFirstStroke`；确认已初始化、未暂停、未锁定结果、enemy 可用且未死亡后开启右移状态，无旧 Level11 的 0.5 秒延迟。
3. 每帧通过 `enemy.SetMoveInput(1)`、`enemy.OnUpdate()` 推进原 Actor 行走；暂停、结果锁定、enemy 禁用或死亡时清除已施加的移动输入。
4. FailedDoor 的 enemy Trigger 或外部调用 `PlayFailedDoorAndFail()`。入口只在运行态、关卡可用且未暂停时有效；检查门 Spine 有 `door_open` 后占用现有 `TryBeginFailurePerformance()` 门禁。
5. 非循环播放 `door_open`，订阅该 TrackEntry 的 Complete/Interrupt/End；停止 enemy 输入并隐藏 `enemy.gameObject`。演出期间结果门禁防止重复触发和竞争结算。
6. 开门 Complete：解除回调，调用 `CompleteFailurePerformance(nameof(DLevel75Ctrl))` → `TryFailAndReset` → `DrawGameResetEvent(GameResetType.Failed)`。先完成开门再提交事件，避免标准失败重建立即打断动画。
7. 动画被替换或清轨：解除回调、恢复被隐藏 enemy 的触发前显隐状态，取消本次失败演出门禁，不发送失败。首笔移动状态保留，可在下一帧恢复右移。
8. Attempt Reset / 禁用：停止右移并取消尚未完成的演出；正常失败、Reset、Replay 继续使用共享流程整关重建，不新增重建或打点逻辑。

## Inspector 与 Prefab 绑定

| 字段/入口 | 显式绑定 | 缺失行为 |
| --- | --- | --- |
| `drawLevelCtrl` | 同根 DrawLevelCtrl，fileID 900000000000000001 | 不移动、不触发失败 |
| `enemy` | Enemy 实例 ActorCtrl，fileID 8373166078072422585 | 不移动、不触发失败 |
| `failedDoorSpine` | FailedDoor 实例门 ActorSpineCtrl，fileID 900000000000000006 | 不触发失败 |
| FailedDoor `onEnter[0]` | DLevel75Ctrl.PlayFailedDoorAndFail，Void 模式 1，过滤 enemy | 自动进门失败入口失效；公开 API 仍可手动绑定 |
| SuccessDoor `onEnter[0]` | 原 DrawLevelCtrl.OnTriggerSuccess | 本次保持原绑定 |

FailedDoor 是 `DrawGame_SuccessDoor.prefab` 的嵌套实例（实例 fileID 8498727679795086425）。门 Spine 引用对应源组件 5287133353375514517，不能误绑实例 6932855938480942226 的成功门 Spine。两个门分别核对事件，不能仅按相同源 GUID 判断归属。

## 数据、资源、UI 与事件流

- 资源：使用现有 Enemy 嵌套 Actor、门 Spine 与 `sp_door_001/door.json` 的 `door_open`，不新增动画资源、加载接口或实例化。
- 画线：共享 DrawLevelCtrl 本地 Handler 分发，不新增第一笔全局事件。
- UI：只复用 DrawLevelCtrl 现有失败演出输入锁和阻挡事件，不访问或修改 GamePlayPanel。
- 失败：只由 DrawLevelCtrl 标准入口发送既有 DrawGameResetEvent；不用主线 GameResetEvent，不重复发送事件。
- 存档、Luban、远端配置、SDK、Analytics：没有直接新增或修改。

## 结构复盘

- 相对 Level11：enemy 无延迟右移；删除本关不使用的 keyFollower 职责，合并到具有真实失败门职责的 DLevel75Ctrl。
- 相对 Level70：使用相同局部演出门禁与完成后标准结算边界，门动画直接由本关组件管理，无须新增 Actor 动画子类。
- 当前仅新增本关必要脚本，不升级共享接口或迁移已完成关卡；继续观察多个局部演出入口重复出现后是否需要共享，证据不足时保留候选。

## 验证、风险与回滚

- Unity 6000.0.59f2 主工程自动编译：Editor.log 中 Assembly-CSharp 的 Csc、ILPostProcess、CopyFiles 与程序集重载完成；新脚本也用现有引用独立 Roslyn 编译，0 error。
- Prefab 静态检查：126 个 YAML 对象锚点唯一，非零本地引用闭合，三个序列化引用均显式绑定；FailedDoor 为 enemy→D75 无参数 API，SuccessDoor 仍为原成功入口。
- 原始 door.json 确认 `door_open` 存在；编译与静态检查不等于实测动画 Complete、物理进门或事件回调。
- 生命周期扫描：新脚本无编辑器回调、DB/存档/远端访问、运行时实例化或对象扫描。
- `uncertain`：实际 first stroke / enemy 行走及碰撞、暂停恢复、两门竞争、开门视觉与真实完成回调、完整失败黑幕重建、Reset/Replay、Android/iOS 横屏未实测。完整验证清单见本目录功能与风险评估。
- 同实例在失败已提交后直接 Attempt Reset 的 enemy 显隐恢复未扩展；实际产品流程依赖标准整关重建，不能声称覆盖任意手动复用实例场景。
- 回滚：移除本关 DLevel75Ctrl 组件与文件，移除 FailedDoor API UnityEvent override 和仅供本脚本使用的 stripped 引用；恢复人工确认的失败入口。若需旧 enemy 延迟行为，重新在本关绑定旧 DLevel11Ctrl；不改旧关或共享源文件。
