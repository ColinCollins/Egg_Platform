# IWannerGame_SetActorMoveSpeed 功能与风险记录

## 需求与原有基线（2026-09-08）

提供可配置的 Actor 移动速度设置脚本。原有 `ActorCtrl` 的 `moveSpeed` 默认 5，已有公开 `SetMoveSpeed(float)`；`Actor_IWanner` 继承该接口，移动平台速度合成也读取 `MoveSpeed`。邻近 `IWannerGame_StopAllDOTween` 使用 `BaseItemEventHandle.Execute()` 接入执行器，本组件沿用该契约。

## 实现与接入

- 新增同目录 `IWannerGame_SetActorMoveSpeed.cs` 及元数据；不修改共享 Actor、已验收关卡脚本或 Prefab。
- 挂载组件，在 Inspector 将当前关卡的 `Actor_IWanner` 绑定到 `target`，填写 `moveSpeed`（绝对速度，默认 5，单位为世界单位/秒）。
- 将组件接入 TriggerExecutor/BaseItemExecutor，或通过 UnityEvent 调用 `Execute()`；挂载本身不会自动修改速度。
- 执行流程：PlayMode 门禁 → 校验显式引用和有限数值 → `target.SetMoveSpeed()` → `IsRunning=false / IsDone=true`。负值按 0，NaN/Infinity 跳过并警告；空目标同样警告并结束。
- 每次执行覆盖基础速度。继承的 `ResetState()` 只重置执行状态，不恢复旧速度；需要恢复时，用另一配置为原速度的组件执行。
- 无新增全局事件、UI、存档、配置、资源加载、SDK、Tween、逐帧更新或层级扫描。只改变绑定实例；移动平台及外部附加速度继续按原逻辑合成。

## 验证证据与缺口

- 已核对 `BaseItemEventHandle` 状态契约、`ActorCtrl.SetMoveSpeed()` 的公开签名及 `Actor_IWanner` 读取基础速度的位置；代码静态检查覆盖空引用、非法值、重复执行与编辑态入口。
- 编译通过临时 MSBuild targets 显式包含新脚本。首次 `dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false` 因缺少 `project.assets.json` 报 NETSDK1004；允许还原后重试仍因缺少 `Temp/bin/Debug` 下的依赖 DLL 报 CS0006（200 个错误）。因此不能声称编译通过。
- CodeGraph 命令在当前 shell 不可用；规则引用的 `../specs/037-remote-resource-loading/plan.md` 不存在，改用本地代码及邻近功能记录取证。
- `uncertain`：尚未挂载 Prefab，Unity 编译、PlayMode 和 Android/iOS 横屏实机未验证。补验：执行前后左右移动速度、0 速度、移动平台/弹簧叠加、重复触发、执行器推进、重建关卡后速度恢复。

## 风险与回滚

- 基础速度为 0 不代表冻结角色，跳跃、平台和机关仍可能使角色移动；高速参数需验证碰撞与手感。
- 多个组件写同一 Actor 时最后一次执行生效；同一实例执行状态重置不会回滚速度。
- 回滚：移除后续接入的组件和执行器/UnityEvent 引用，再删除新增脚本及元数据；本次无既有关卡行为迁移。
