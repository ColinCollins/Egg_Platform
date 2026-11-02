# IWannerGame 入口相机生命周期功能与风险记录

## 需求与背景

- 需求：进入 IWanner Game 模式时立即关闭 `GameManager` 控制的入口相机，不再等到具体小关实例化前才关闭。
- 修改前 `IWannerPlayCtrl.OpenCurrentLevelAsync()` 要先创建 HUD、解析并异步加载关卡资源、查找 `Scene` 根节点，随后才调用 `GameManager.CloseCamera()`；上述等待期间入口相机仍可能保持启用。

## 影响文件与系统

- 修改 `Assets/Game/Scripts/Play/IWanner/IWannerPlayCtrl.cs`。
- 使用既有 `GameManager.CloseCamera()/OpenCamera()`，不修改 `GameManager`、`StartCamera` 序列化字段、场景资源或 Camera 参数。
- 不新增配置、存档、资源路径、全局事件、分析打点、广告、支付或平台 SDK 调用。

## 修改后行为与调用流

1. `PlayControllerManager.TryEnter()` 先停用旧玩法控制器，再调用 `IWannerPlayCtrl.Activate()`。
2. IWanner 激活状态与异步版本号建立后，`Activate()` 立即调用 `GameManager.Instance.CloseCamera()`，随后注册玩法事件。
3. `OpenCurrentLevelAsync()` 继续创建 HUD、加载并实例化 IWanner 小关，但不再重复关闭入口相机。
4. IWanner 退出、入口失败回滚、控制器激活异常回滚或 Shutdown 仍通过 `Deactivate() -> CleanupRuntime() -> GameManager.OpenCamera()` 恢复入口相机。
5. 同一个已激活 IWanner 控制器内切 Group、小关、Reset 或 Replay 不重复切换入口相机。

## 本地证据

- `PlayControllerManager.TryEnter()` 的顺序为旧控制器 `Deactivate()`、新控制器 `Activate()`、再设置 `ActiveController`；因此主线退出清理若先打开相机，IWanner 激活会在其后重新关闭。
- `Assets/Game/Scenes/Loading.unity` 的 `GameManager.StartCamera` 已静态绑定到有效 fileID；本次没有改动该场景。
- `IWannerPlayCtrl` 最终仅保留一个 `CloseCamera()` 调用，位置在 `Activate()`；`CleanupRuntime()` 保留一个 `OpenCamera()` 恢复调用。

## 验证

- CodeGraph 与静态调用点检查确认关闭动作从关卡实例化边界移至控制器激活边界，退出恢复链未变。
- Unity Editor 已对 `IWannerPlayCtrl.Activate/OpenCurrentLevelAsync` 完成 Hot Reload，日志未出现新增 `error CS` 或 `Compilation failed`。
- `dotnet build Assembly-CSharp.csproj --no-restore -m:1 /nodeReuse:false` 完整构建通过：`0 warning / 0 error`。
- `git diff --check` 在最终收尾执行。

## 剩余风险与未验证项

- `uncertain`：尚未在 Hot Reload 后重新执行一次 StartPanel/选组 Popup → IWanner 的完整进入流程，需确认点击 Group 后、资源加载期间入口相机立即关闭。
- `uncertain`：需验证无效 Group、关卡资源缺失、场景根节点缺失、返回 StartPanel、连续切 Group 和 Shutdown 后入口相机均恢复。
- Android/iOS 横屏真机的切换帧、黑幕覆盖和相机音频监听器状态尚未验证。
- `GameManager.CloseCamera()` 仍沿用既有非空引用前提；若 Loading 场景绑定被破坏，现在会在 IWanner 激活时更早暴露错误。

## 回滚路径

- 从 `IWannerPlayCtrl.Activate()` 移除 `GameManager.Instance.CloseCamera()`，并在 `OpenCurrentLevelAsync()` 实例化关卡前恢复原调用。
- 不需要回滚存档、配置、资源、场景绑定或事件订阅。
