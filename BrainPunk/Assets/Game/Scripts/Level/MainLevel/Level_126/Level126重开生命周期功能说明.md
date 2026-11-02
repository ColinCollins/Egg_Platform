# Level126 重开生命周期功能说明

## 审计结论

- Level126 已正确让 Reset 与 Replay 共用 `BeginTeardown()`，没有完整恢复关卡初态。
- 本轮只删除 teardown 中把当前选中火柴恢复缩放和排序的旧实例视觉复位；输入状态清理、火焰/摩擦反馈停止、提示隐藏、事件解绑和运行时对象销毁继续保留。

## 影响与验证

- 影响文件仅 `Level126Ctrl.cs` 和本地中文记录。
- 正常取消选中仍会调用 `RestoreMatchSelectionVisual()`，玩家操作行为不变；只改变关卡即将销毁时的无意义回写。
- C# 编译通过：0 error / 120 warning。
- 未执行选中火柴状态下 Reset/Replay 的黑幕画面与移动端验证，标记为 `uncertain`。

## 回滚

在 `BeginTeardown()` 末尾恢复对当前 `selectedMatchIndex` 的 `RestoreMatchSelectionVisual()` 调用即可。
