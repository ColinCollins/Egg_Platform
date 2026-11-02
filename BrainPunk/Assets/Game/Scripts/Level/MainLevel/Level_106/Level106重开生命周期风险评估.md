# Level106 重开生命周期风险评估

## 原始行为与风险

- Reset 事件会把即将销毁的旧实例恢复为初态并重新 `SetCtrlable(true)`，存在黑幕等待窗口内旧输入复活、UFO/Actor 跳位和异步击飞继续运行风险。
- OnDisable 重复执行相同复位，职责与主线销毁重建机制冲突。

## 已控制风险

- teardown 只停止输入、Trigger、Tween、事件和箱子怪异步版本，不恢复作者初态。
- `Level106BoxBadCtrl.SuspendRuntime()` 明确只取消触发与异步表现；完整物理、父级、Collider 和动画初态仍由新实例初始化恢复。
- Level106 既有正常 Resume、完成层数后恢复控制等 `SetCtrlable(true)` 保留，未误删正常玩法逻辑。

## 证据、缺口与回滚

- C# 编译通过，0 error / 120 warning。
- 工作区已有 `Level106RealTipsPopup.prefab` 尾随空格导致全目录 `git diff --check` 报告；该 Prefab 是任务前改动，本轮未修改。
- PlayMode、连续快速重开、广告覆盖期间的旧实例停机和移动端触控未验证，均标记 `uncertain`。
- 回滚为恢复旧完整复位调用；不涉及 Prefab、配置、存档、打点或共享 `PlayCtrl`。
