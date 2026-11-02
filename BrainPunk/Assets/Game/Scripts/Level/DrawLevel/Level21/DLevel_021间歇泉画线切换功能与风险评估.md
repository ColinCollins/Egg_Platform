# DLevel_021 间歇泉单周期功能与风险评估

## 需求与行为变化

- 原空脚本不控制两侧 WaterSplash；上一版工作区实现又把 short/long 拆成不同循环，并在接触变化时停止播放、重置计时。
- 当前按用户要求收敛为一个播放周期：待机 3 秒、播放阶段 3 秒，循环往复。
- 进入关卡后先等待第一笔有效画线完成，完成回调到达时才从 0 开始累计首轮 3 秒待机。
- 接触状态只在每轮播放开始时决定本轮内容，不直接触发 water_2，也不改变当前播放或周期计时。

## 影响范围

- 脚本：`Assets/Game/Scripts/Level/DrawLevel/Level21/DLevel021Ctrl.cs`。
- Prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_021.prefab`。
- 复用既有 WaterSplash Animator 与 Layer 14 Draw 接触；控制器不承担音效逻辑。
- 不涉及 DB、存档、Luban、UI、全局事件、Analytics、SDK 或平台分支。

## 风险

| 风险 | 当前控制 | 未验证缺口 |
| --- | --- | --- |
| TouchArea/Animator 引用错误 | PlayMode 入口校验并使用 Bear Logger 报错 | 后续 Prefab 改配 |
| 接触在周期边界附近变化 | 每轮只采样一次，周期内不再改选 | 边界一帧的玩法手感 |
| 首笔启动通知缺失或重复 | 复用 `IDrawStrokeCompletedHandler` 并检查 `IsFirstStroke` 与本地门禁 | PlayMode 实际通知链 |
| 双侧同时接触 | 本轮不播放但周期继续 | 是否符合最终关卡解法 |
| Animator 或 Layer 资源被改动 | 当前使用既有四个参数和 Layer 14 | 后续资源迁移 |
| 移动端物理与性能 | 常量查询，无场景搜索或分配 | Android/iOS 真机未测 |

## 验证与回滚

- 静态确认控制器仅有 `_cycleTimer` 一个周期计时字段；首笔完成前不更新，接触读取仅由周期开始调用。
- Assembly-CSharp 编译通过（`0 error`）；Prefab 本地引用和 YAML 检查通过。
- `uncertain`：Unity PlayMode、完整可解性、Reset/Replay、暂停恢复、音画同步和移动端表现。
- 回滚可恢复上一版 `DLevel021Ctrl.cs` 与 Prefab 序列化字段；共享系统、配置和存档无需迁移。
