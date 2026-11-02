# ILevel037Ctrl 功能与风险记录

日期：2026-08-14

## 需求与原始基线

- 用户要求在 `Assets/Game/Scripts/Level/IWannerLevel/Levels` 提供第 037 关 Portal 控制 API：第一次触发时，
  同一个指定 Portal 按配置速度持续向右平移；当前传送完整结束后再等待 1 秒切换 `endPoint`；第二次触发时停止移动。
- 修改前 `ILevel037Ctrl.cs` 只有 Unity 自动生成的空 `Start()`、空 `Update()` 和占位注释，没有序列化引用、
  触发计数、Portal 移动或终点切换行为。
- `ILevel_037.prefab` 已挂载并配置 `ILevel037Ctrl`，目标 Trigger 的 `onEnter` 调用
  `TriggerPortalMovement()`；需求是 Actor 离开后再次进入同一个 Trigger 时，由同一个 API 停止 Portal。
- 原实现通过 `OnEnable()` 重置调用计数。该生命周期重置不属于需求，并可能破坏同一控制器实例对首次调用状态的保留。

## 影响文件、资源与系统

- 修改：`Assets/Game/Scripts/Level/IWannerLevel/Levels/ILevel037Ctrl.cs`。
- 新增：本功能与风险记录及对应 `.md.meta`。
- 复用：`IWannerGame_Portal.SetEndPointAfterTeleport(Transform, float)`、Unity `Update()`、`Time.deltaTime`、UniTask
  游戏时间延迟和 Bear Logger。
- 不修改共享 `ActorCtrl`、Portal prefab、全局事件、存档、配置、资源加载、UI、Analytics、
  广告、SDK 或平台分支。

## 修改后行为与 API

1. Inspector 配置同一个 `portal`、`newEndPoint` 和 `portalMoveSpeed`；速度单位为世界单位/秒，
   默认 `1f`。
2. Trigger/UnityEvent 调用公开方法 `TriggerPortalMovement()`。
3. 第一次有效调用会对同一个对象执行 `portal.SetEndPointAfterTeleport(newEndPoint, 1f)` 预约终点切换，并立即允许
   该 `portal` 每帧按 `Vector3.right * portalMoveSpeed * Time.deltaTime` 向世界坐标右侧移动。当前传送仍使用旧终点；
   Actor 到达、完成 OutBack 出现并恢复移动后，Portal 再等待 1 秒应用 `newEndPoint`。
4. 同一个 Trigger 再次调用该 API 时只关闭移动门禁，Portal 保持在停止瞬间的位置；不会调用终点切换 API，
   不覆盖、不恢复也不重新预约 `endPoint`。之后的重复调用仍保持停止。
5. 第一次调用时若任一引用缺失或速度不大于 0，会用 Bear Logger 警告并忽略该次调用，不记录为已经启动。
6. 不再通过 `OnEnable()` 重置首次调用状态。组件禁用时只停止移动；重新启用不会把后续调用误判为首次启动。

## 数据、资源与性能流

- 运行时只修改同一个指定 Portal 的 Transform 世界坐标和内存态 `endPoint` 引用，不产生存档、配置、
  资源加载、UI 或全局事件流。
- 移动期间每帧执行一次 Transform 位移，没有实例化、集合遍历或显式托管分配；Android/iOS 横屏性能影响预计很低，
  但尚未进行真机验证。

## 验证记录

- 使用 CodeGraph 核对相邻 IWanner 关卡控制器公开 API、PlayMode 保护、Bear Logger 和移动生命周期写法。
- 静态核对 `ILevel_037.prefab`：目标 Trigger 的 `onEnter` 指向同一 `ILevel037Ctrl` 实例的
  `TriggerPortalMovement()`；其它 Trigger 与本次 API 状态修复无关。
- 执行 `dotnet build Assembly-CSharp.csproj --no-restore --nologo --verbosity:minimal`，结果为
  `0 error / 101` 个工程既有 warning；传送完成后延迟 1 秒切换终点的最终实现已包含在该结果中，构建输出没有指向
  `ILevel037Ctrl` 或 `IWannerGame_Portal` 的 warning。
- Unity PlayMode 尚未执行。

## 风险与未验证缺口

- `uncertain`：尚未在 Unity PlayMode 验证第一次触发的移动方向/速度、动态终点跟随、第二次停止位置、Reset/Replay、
  暂停恢复、Collider2D 触发稳定性以及 Android/iOS 横屏表现。
- 传送完成后的 1 秒使用游戏时间，并继续保持 Portal 重复触发门禁；全局暂停会延后终点切换。若传送被取消，预约不会
  提前应用，而会保留到下一次成功传送。
- `newEndPoint` 若需要跟随 Portal 移动，应放在该 `portal` 的子层级；若绑定静止的独立 Transform，终点不会随 Portal
  一起移动。
- 代码直接移动 Portal Transform。若后续给 Portal 根节点增加动态 `Rigidbody2D` 或其它移动 Tween，应改为单一移动所有者，
  避免物理位置与 Transform/Tween 竞争。
- `OnDisable()` 只停止移动，不回滚位置和终点；Reset/Replay 若复用同一实例并要求恢复初态，需要关卡外层重建实例，
  或后续明确增加本关 Reset API。

## 回滚路径

- 将 `ILevel037Ctrl.cs` 恢复为空占位控制器，并删除本记录及 `.md.meta`，即可回滚本次控制逻辑。
- 若只回滚本次停止修复，恢复 `_validTriggerCount` 和 `OnEnable()` 重置逻辑即可；Prefab 无需调整。
