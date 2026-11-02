# Level116「勇往直前」风险评估

## 原始行为基线与影响范围

原实现把每个障碍的距离通过 `Mathf.Repeat(distance, TravelDistance)` 归一化到固定 14 单位行程；其前置保护判断的是同一个编译期常量是否小于等于零。影响文件仅为 `Level116Obstacle.cs`，其它控制器、规则、输入、Prefab、资源和流程未改。

## 风险复核

- **事实**：`BottomWorldY=-7`、`TopWorldY=7`、`TravelDistance=14`；编译器对旧保护分支报告 `CS0162` 不可达代码。
- **直接结论**：删除分支后，所有可达输入仍执行同一个 `Mathf.Repeat(distance, 14)`；障碍位置、方向、速度、初始相位和碰撞回调不变。
- **假设**：无运行时可修改该 `const` 值的路径；如果未来改变行程定义，应同时重新评估归一化合同。
- **缺失证据**：未执行 Unity PlayMode 的多帧相位对比、碰撞、暂停/恢复、Reset/Replay 和 Android/iOS 真机验证，标记为 `uncertain`。

## 既有系统与回滚

本关继续使用现有 Actor、LevelCtrl、GamePlayPanel 输入、Bear 事件清理、Unity InputSystem 和本关局部规则/障碍组件；未新增全局事件或共享脚本。回滚只需恢复 `NormalizeDistance()` 的三行不可达保护；无需回滚 Prefab、配置、资源或其它关卡。
