# Level133 功能说明

## 2026-09-08：移除死亡重建前的重复机关复位

### 需求与原行为

Level133 原先同时依赖主线重建流程和本关局部复位：`BaseLevelCtrl` 在 Actor 死亡后派发 `GameResetEvent(Failed)`，`PlayCtrl` 负责记录失败并销毁、重建当前关卡；`Level133Ctrl` 又监听同一事件并调用 `ResetMechanism()`，旧关卡进入 `OnDisable` 时还会再调用一次。表现上，死亡现场的柱子、按钮和倒计时会在旧关卡销毁前恢复初始状态。

### 修改后行为

- `Level133Ctrl` 不再订阅 `GameResetEvent`，删除本关 `OnGameReset` 回调。
- `OnDisable` 仍退订事件、停止倒计时 Tween、取消角色入场并解除本关输入阻塞，但不再复位柱子、按钮和倒计时。
- `OnEnable` 首次初始化或组件重新启用时仍调用 `ResetMechanism()`，保留机关初始状态建立职责。
- 死亡、手动 Reset、失败计数、打点、插屏和整关销毁/重建继续由 `BaseLevelCtrl` 与 `PlayCtrl` 处理。

### 影响范围

- 修改脚本：`Level133Ctrl.cs`。
- 未修改 `Level_133.prefab`、Actor、机关参数、碰撞、资源、存档或共享控制器。
- 事件流从“Level133 局部复位 + PlayCtrl 重建”收敛为“PlayCtrl 重建”。

### 验证

- 定向引用检查确认 `Level133Ctrl` 已无 `GameResetEvent` 和 `OnGameReset`，`ResetMechanism()` 只剩 `OnEnable` 初始化调用。
- `git diff --check` 通过。
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo` 通过：0 错误、123 个既有警告。
- Unity PlayMode 中的死亡停留画面、手动 Reset、连续失败、暂停恢复以及 Android/iOS 横屏表现尚未验证，标记为 `uncertain`。

### 回滚

仅恢复 `Level133Ctrl.OnEnable` 的 `GameResetEvent` 订阅、`OnGameReset` 回调和 `OnDisable` 中的 `ResetMechanism()` 调用；不要回滚 Prefab 或共享主线重建流程。
