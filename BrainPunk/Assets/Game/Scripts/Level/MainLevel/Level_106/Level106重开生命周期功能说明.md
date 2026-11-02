# Level106 重开生命周期功能说明

## 需求与基线

- 2026-08-26 纠察新增主线关卡的 Reset/Replay 行为。
- 主线 `PlayCtrl` 在 Reset/失败重开时等待约 0.5 秒后销毁旧 Level，再实例化新 Prefab；旧 Level106 原先在 `GameResetEvent` 和 `OnDisable` 中完整恢复 UFO、Actor、箱子怪、碰撞、层数和控制权。

## 当前行为

- `GameResetEvent` 只进入 `BeginTeardown()`：关闭真实提示弹窗、解绑事件、停止 Sequence/计数 Tween/UFO Tween/Actor 旋转、禁用 UFO 碰撞和控制输入，并取消箱子怪异步击飞。
- 新实例初态由 `OnEnable -> InitializeRuntimeState()` 建立；箱子怪和 UFO 碰撞的内部 API 同步改名为 `InitializeRuntimeState()`，避免被误用于旧实例复位。
- 正常进关、弹窗、多语言、层数、成功、失败、存档、配置和打点路径未修改。

## 影响与验证

- 影响脚本：`Level106Ctrl.cs`、`Level106BoxBadCtrl.cs`、`Level106UfoCollision.cs`。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo`：0 error / 120 warning；warning 为工程既有诊断。
- Unity PlayMode 的手动 Reset、失败重开、胜利 Replay、箱子怪击飞中重开及 Android/iOS 横屏仍为 `uncertain`。

## 回滚

恢复旧 `ResetRuntimeState()` 命名和 Reset/OnDisable 完整复位调用；该回滚会重新引入旧实例销毁前恢复控制与表现的风险。
