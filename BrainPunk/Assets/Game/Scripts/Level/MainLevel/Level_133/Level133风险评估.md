# Level133 风险评估

## 2026-09-08：死亡重建前重复复位清理

### 原始行为基线

Actor 死亡后，`BaseLevelCtrl.CheckFinished()` 等待 1 秒并派发 `GameResetEvent(Failed)`。`PlayCtrl.OnGameResetEvent()` 完成失败计数、`level_end(fail)`、下一次 `level_start` 等既有流程，再等待 0.5 秒销毁并重建关卡。Level133 原先在该事件到达时立即复位机关，销毁触发 `OnDisable` 时再次复位。

### 风险与控制

- **死亡现场表现**：移除局部复位后，旧实例在销毁前不再把柱子、按钮和倒计时跳回初始位置。旧实例仍可能在剩余窗口继续当前机关运动；实际死亡镜头停留效果需 PlayMode 确认。
- **新实例初始化**：`OnEnable → ResetMechanism()` 保留，新建关卡仍从缓存的柱子/按钮初始位置和隐藏倒计时开始。
- **事件与打点**：未修改 `BaseLevelCtrl`、`PlayCtrl` 或 `GameResetEvent` 定义；失败次数、Reset 次数、revive、`level_end(fail)`、新 `level_start` 及关卡重建顺序继续遵循 `DevelopMD/关卡打点生命周期约定.md` 第 2.2 节。
- **资源与状态**：Prefab、资源加载、存档、成功条件、机关周期参数和碰撞均未修改。
- **移动端**：本次只减少两次旧实例状态写入，没有新增帧循环或分配；Android/iOS 的死亡停留画面仍需实际验证。

### 验证证据

- 源码检查确认 Level133 不再监听 `GameResetEvent`，`OnDisable` 无机关位置写入。
- `git diff --check` 通过。
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo` 通过：0 错误、123 个既有警告。
- 未执行 Unity PlayMode、真机、插屏覆盖期间重建和连续死亡回归，上述运行时表现为 `uncertain`。

### 回滚路径

只恢复 Level133 的事件订阅、`OnGameReset` 和 `OnDisable` 局部复位；共享失败重建与打点流程不参与回滚。
