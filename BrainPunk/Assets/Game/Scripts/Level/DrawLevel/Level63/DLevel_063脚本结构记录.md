# DLevel_063 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame 063（`20063 / DLevel_063`）。
- 记录日期：2026-08-18。
- 需求：沿用 D053 蜂群生存玩法，但场景放置两个蜂巢；其它规则、失败演出和成功倒计时保持原配置。
- 参考基线：D053。两个蜂巢均为现有 `DLevel53Hive` prefab 实例，不新增关卡脚本。
- 关卡 prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_063.prefab`。
- 状态：静态检查和 C# 编译已完成；Unity PlayMode、Android/iOS 横屏仍为 `uncertain`。

## 2. 脚本与组件树

```text
DLevel_063
├─ DrawLevelCtrl
├─ DrawLineFeature
├─ DelayAutoBeginDrawHandle
├─ DrawLevelCountdownCtrl (10s, success; 与 D053 基线一致)
├─ DLevel53Ctrl (单 Actor)
├─ DLevel53Hive instance A
├─ DLevel53Hive instance B
└─ 场景、FallArea、水体与 D003 Knock_Explosion
```

两个 Hive 源 prefab GUID 均为 `fe79017bd82a4df3bf390715a913b93e`，实例 anchor 为 `930000000000000001` 和 `7142391443523601624`。

## 3. 类职责与边界

| 类/组件 | 类型 | 职责 | 继承/接口 | 不负责事项 |
| --- | --- | --- | --- | --- |
| `DrawLevelCtrl` | 共享控制器 | 扫描并分发两个 Hive 的首笔/Reset，管理输入和标准结算 | 既有 Draw 契约 | 不合并两个 Hive 的生成计划 |
| `DLevel53Ctrl` | 共享蜂群结算控制器 | 处理任一 Hive 生成的 Bee 命中、Knock、`swell` 和一次失败 | `IDrawStrokeCompletedHandler`、`IDebuger` | 不直接实例化 Bee |
| `DLevel53Hive` | 蜂巢实例 | 各自生成 8 只 Bee、保存本地批次/攻击槽并清理运行实例 | `IDrawStrokeCompletedHandler`、`IDebuger` | 不独立提交成功/失败 |
| `DLevel53Bee` | 运行时 Bee | 追踪 Actor、碰撞施力和反冲 | `MonoBehaviour` | 不控制倒计时 |

## 4. 生命周期与关键调用

1. 初始化时 `DrawLevelCtrl.CacheLocalHandlers()` 扫描当前层级；两个 Hive 实例都实现 `IDrawStrokeCompletedHandler`，因此都会进入 Handler 列表。
2. 第一笔有效画线完成后，DrawLevelCtrl 依次调用两个 Hive。每个 Hive 都建立独立的批次计划和攻击角度，按自己的 SpawnPoint 生成蜜蜂；两组 Bee 共享同一个 `DLevel53Ctrl` 和 Draw 失败占用。
3. 每个 Hive 的 Bee 都固定追踪本关唯一 Actor。任一 Bee 命中时，Ctrl 在真实接触点播放 Knock/音效并播放角色 `swell`；首次命中启动 1.5 秒失败演出，期间另一 Hive 的 Bee 不会被整体冻结。
4. `DLevel53Ctrl.OnLevelResolved()` 不再只操作序列化的首个 `hive`，而是扫描全部子节点 `DLevel53Hive` 并暂停；Reset/Disable 由各 Hive 清理自己的运行实例。
5. 成功倒计时仍只有 prefab 中的一个 `DrawLevelCountdownCtrl`，不会因双 Hive 重复计时或重复结算。

## 5. Prefab、序列化与资源绑定

| 宿主 | 字段/组件 | 绑定对象或资源 | 必填/可选 | 缺失时行为 |
| --- | --- | --- | --- | --- |
| 根 | `DLevel53Ctrl.actor` | 当前 Actor stripped 引用 | 必填 | 两个 Hive 均拒绝启动 |
| 根 | `DLevel53Ctrl.hive` | Hive A 的 stripped 组件 | 兼容首 Hive 引用 | Ctrl 结算时仍扫描全部 Hive |
| 根 | Hive A/B | `DLevel53Hive.prefab` | 两个都必填 | 缺失实例不生成蜂群 |
| 每个 Hive | `beeTemplate`、`spawnPoint` | 本地模板/出生点 | 必填 | 该 Hive 记录错误并跳过 |
| 根 | `knockEffect` | D003 `Knock_Explosion` | 可选 | 失败链仍执行，无粒子 |

## 6. 数据、UI、资源与事件流

- 配置：运行 JSON 已包含 `20063 -> DLevel_063`，Draw 排序为第 55 项；源表没有新增字段。
- 画线/结算：两个 Hive 复用 `DrawLineFeature -> DrawLevelCtrl -> IDrawStrokeCompletedHandler`；倒计时、成功/失败和 Reset 仍由共享 Draw 控制器管理。
- 资源：两个 Hive 各自引用同一蜂巢 prefab、Bee 模板和既有粒子；无新存档、全局事件、Analytics、SDK 或平台链路。
- UI：复用现有画线和倒计时 UI。

## 7. 相对参考基线的差异

| 差异 | 原因 | 玩家/运行时影响 | 验证范围 | 是否形成演进候选 |
| --- | --- | --- | --- | --- |
| Hive 数量由 1 增为 2 | 用户要求双蜂巢 | 两个出生点独立产生蜂群，单个倒计时仍保持 | prefab 实例和 Handler 静态检查 | 局部必要差异 |
| Resolved 扫描所有 Hive | 避免成功/失败后第二 Hive 残留 | 所有子 Hive 一起暂停 | 代码静态检查 | 可复用于其它多源生成关 |
| 生成计划不合并 | 保持 Hive 自包含职责 | 每个 Hive 各自有 8 只和本地随机顺序，可能同时产生峰值 | YAML/代码审查 | 观察中，不上提共享 |

## 8. 可复用结论与优化候选

- 稳定结构：多个同类局部 Handler 可通过现有 DrawLevelCtrl 扫描接入，无需新增全局事件。
- 本关验证的新结构：共享 Ctrl 对 `GetComponentsInChildren<DLevel53Hive>(true)` 做结算暂停，兼容多 Hive。
- 暂不共享：跨 Hive 的统一生成调度、全局数量上限和随机种子不在本需求内。
- 候选：若移动端出现同帧实例化峰值，再评估局部总量协调，不改变已验收单 Hive 关卡。

## 9. 验证证据与缺口

- 静态检查：D063 prefab 144 个定义、0 重复 anchor、0 悬空引用；两个 Hive prefab 实例均可解析，根 Ctrl/倒计时引用有效。
- 配置检查：Luban 生成命令成功；Draw JSON 含 20063，排序路径为第 55 项。
- 编译：Unity Roslyn 定向编译退出码 0；仅有工程既有 warning。
- Unity PlayMode：`uncertain`，尚未验证两巢同时首笔启动、蜂群总量/帧率、成功/失败后双巢清理。
- Android/iOS 横屏：`uncertain`。

## 10. 风险与回滚

- 主要风险：两个 Hive 的批次时间可能重叠，造成短时物理/实例化峰值；第二 Hive 的 Reset/Resolved 生命周期需在真实运行中确认；同一个 Knock 粒子可能被连续命中重启。
- 回滚路径：从 D063 根移除第二个 Hive prefab instance 即可恢复 D053 单巢行为；共享 Ctrl 的全量 Hive 暂停扫描应保留给 D062/D065 兼容，不要随单关回滚一并删除。
