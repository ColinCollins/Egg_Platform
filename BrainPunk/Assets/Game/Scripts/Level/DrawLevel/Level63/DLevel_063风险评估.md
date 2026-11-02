# DLevel_063 风险评估

## 1. 需求与原行为基线

- 目标：D053 蜂群玩法增加第二个蜂巢，其它规则保持不变。
- 基线：一个 `DLevel53Ctrl`、一个 `DLevel53Hive`、一个成功倒计时和一个 Actor；成功倒计时基线为 10 秒。
- 本次实际变化：D063 prefab 保留首个 Hive，并新增第二个 `DLevel53Hive` prefab instance；共享 Ctrl 的结算暂停改为扫描全部子 Hive。

## 2. 风险与控制

| 风险 | 等级 | 控制措施 | 当前证据 | 待补验证 |
| --- | --- | --- | --- | --- |
| 第二 Hive 没有收到首笔/Reset | 高 | 复用 `CacheLocalHandlers()` 扫描所有 MonoBehaviour Handler | 两个 Hive 实例 anchor 和源 GUID 均存在 | PlayMode 首笔、失败重开 |
| 成功后只暂停首 Hive，第二巢残留 | 高 | `OnLevelResolved()` 扫描全部子节点 Hive | 代码静态检查 | 成功/失败后观察运行实例 |
| 两巢同帧生成造成峰值 | 中 | 各 Hive 仍沿用 3 秒窗口、1~3 批次；未改变数量参数 | prefab Hive 参数可解析 | 移动端压力测试 |
| 双巢共享失败状态重复提交 | 中 | 共享 Draw failure performance 和 Ctrl 的 Bee/Actor 去重 | 调用链静态检查 | 两巢同帧命中 |
| 两个 Hive 引用同一运行父节点导致层级混淆 | 低 | 每个 Hive 优先使用自身 runtimeBeeRoot；运行实例列表按 Hive 隔离 | prefab YAML 静态检查 | Unity 层级和 Reset |

## 3. 数据、资源与回滚

- 不新增 DB、存档、全局事件或平台分支；只复用既有 Hive/Bee 资源。
- `20063` 已存在于 Luban 数据和 Draw 排序；D063 当前成功倒计时为 10 秒，与 D053 基线一致。删除本关时同步移除 prefab、排序/数据行并重新生成 JSON。
- 局部回滚仅删除第二 Hive instance；共享代码回滚需先确认 D062/D065 不再依赖全量 Hive 暂停扫描。

## 4. 验证证据与未决项

- 已完成：D063 144 个 YAML 定义、0 重复 anchor、0 悬空引用；两个 Hive 源 GUID、Ctrl、倒计时和资源引用静态有效；Luban 生成成功；Roslyn 编译退出码 0。
- `uncertain`：双巢实际生成节奏、同帧物理开销、失败演出期间残蜂、重开/退出清理以及 Android/iOS 横屏。
- 验收重点：第一笔后两个出生点均出现 Bee；任一巢命中都只触发一次失败；成功或失败后两个巢都停止且不残留运行对象。
