# Level126 重开生命周期风险评估

## 基线与改动

- 原 teardown 已停止 DOTween、摩擦反馈、火焰、提示和事件，但额外恢复一次选中火柴视觉。
- 当前删除这次旧实例视觉复位；运行时对象销毁、反馈停止和事件解绑不变。

## 风险与证据

- 风险较低：被删除写入只发生于已进入 teardown 的旧实例，正常取消选择仍保留视觉恢复。
- C# 编译通过，0 error / 120 warning；静态确认 Reset/Replay 继续共用同一个幂等 `BeginTeardown()`。
- 未执行 Unity PlayMode、连续重开和 Android/iOS 横屏测试，标记为 `uncertain`。

## 回滚

恢复 teardown 中的选中火柴视觉还原两行；无需修改 Prefab、配置、存档、事件或共享脚本。
