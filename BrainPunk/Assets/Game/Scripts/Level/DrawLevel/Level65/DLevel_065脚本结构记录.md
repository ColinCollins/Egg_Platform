# DLevel_065 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame 065（`20065 / DLevel_065`）。
- 记录日期：2026-08-18。
- 需求：保护两个角色；蜂巢为每只蜜蜂随机选择两个角色中的一个；两角色被蛰时播放 `swell`。其余 D053 蜂群流程保持不变。
- 参考基线：D053。D065 场景没有猫夹释放 Handler，因此角色按 prefab 原有物理/开场状态参与蜂群。
- 关卡 prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_065.prefab`。
- 状态：静态检查和 C# 编译已完成；Unity PlayMode、Android/iOS 横屏仍为 `uncertain`。

## 2. 脚本与组件树

```text
DLevel_065
├─ DrawLevelCtrl
├─ DrawLineFeature
├─ DelayAutoBeginDrawHandle
├─ DrawLevelCountdownCtrl (10s, success)
├─ DLevel53Ctrl
│  ├─ actor -> 第一角色
│  └─ additionalActors[0] -> 第二角色
├─ DLevel53Hive
└─ 场景、FallArea、水体与 D003 Knock_Explosion
```

## 3. 类职责与边界

| 类/组件 | 类型 | 职责 | 继承/接口 | 不负责事项 |
| --- | --- | --- | --- | --- |
| `DrawLevelCtrl` | 共享控制器 | 画线输入、Handler 分发、倒计时/结算/Reset | 既有 Draw 契约 | 不选择 Bee 目标 |
| `DLevel53Ctrl` | 共享蜂群结算控制器 | 管理两个 Actor 目标、命中角色、接触点 Knock、`swell` 和一次失败 | `IDrawStrokeCompletedHandler`、`IDebuger` | 不生成蜂群 |
| `DLevel53Hive` | 蜂巢控制器 | 首笔生成批次并为 Bee 绑定随机角色；负责暂停/清理 | `IDrawStrokeCompletedHandler`、`IDebuger` | 不直接调用标准失败 |
| `DLevel53Bee` | 蜜蜂实例 | 追踪目标、碰撞推力、反冲和命中回调 | `MonoBehaviour` | 不控制成功倒计时 |

## 4. 生命周期与关键调用

1. `DLevel53Ctrl` 初始化时登记根 `actor` 和 `additionalActors[0]`，过滤 Layer/Tag，形成两个有效目标。
2. 第一笔有效画线后，唯一 Hive 建立 D053 同样的生成计划；每次 Bee 实例化时从两个目标中随机选一个并缓存。
3. Bee 与任一角色真实 Collider 碰撞后，Ctrl 解析具体 Actor，移动 Knock 到接触点并播放音效；每个角色只调用一次 `Die()` + `swell`。首击启动 1.5 秒失败演出，后续角色仍可被命中但不重复提交失败。
4. 共享成功倒计时在失败演出期间冻结；结束后由 Draw 标准链重开。成功、Reset、Replay、退出继续沿既有生命周期。

## 5. Prefab、序列化与资源绑定

| 宿主 | 字段/组件 | 绑定对象或资源 | 必填/可选 | 缺失时行为 |
| --- | --- | --- | --- | --- |
| 根 | `DLevel53Ctrl.actor` | 第一 Actor stripped 引用 | 必填 | 无目标时 Hive 不启动 |
| 根 | `additionalActors[0]` | 第二 Actor stripped 引用 | 双角色必填 | 缺失会退化为单角色 |
| 根 | `hive` | 单 Hive stripped 组件 | 必填 | Ctrl/Hive 记录错误 |
| 根 | `knockEffect` | D003 `Knock_Explosion` | 可选 | 失败链保留但无粒子 |
| 两个 Actor | SkeletonData/材质 | `sp_box_025` 及匹配材质，含 `swell` | 必填 | 缺失动画时保留默认 `die` |

静态 YAML 证据：根 Ctrl 位于 `DLevel_065.prefab:167`，`additionalActors` 指向第二角色；两个角色均有 SkeletonData Override，且没有猫夹释放控制器。

## 6. 数据、UI、资源与事件流

- 配置：运行 JSON 已包含 `20065 -> DLevel_065`，Draw 排序为第 57 项；不新增表字段。
- 画线/结算：复用 DrawLineFeature、DrawLevelCtrl、DrawLevelCountdownCtrl 和共享失败占用；没有新事件或存档。
- 资源：蜂巢/Bee/Actor 骨骼/Knock/水体/FallArea 随现有 prefab 加载；无新 Resources 地址、Analytics、广告、SDK 或平台分支。
- UI：复用既有画线与倒计时 UI。

## 7. 相对参考基线的差异

| 差异 | 原因 | 玩家/运行时影响 | 验证范围 | 是否形成演进候选 |
| --- | --- | --- | --- | --- |
| 增加 `additionalActors` 目标 | 双人保护 | Bee 目标随机分配到两角色 | D065 prefab/代码静态检查 | 与 D062 共用现有实现 |
| 角色命中表现按 Actor 去重 | 失败后仍允许其它角色受击 | 两角色各自最多播放一次 `swell` | 代码路径和资源检查 | 继续观察 |
| 无猫夹 Handler | D065 当前复制结构没有入场装置 | 不改变角色现有开场与物理 | Handler GUID 扫描 | 否 |

## 8. 可复用结论与优化候选

- 稳定结构：共享 Ctrl/Hive/Bee 三层职责、Draw Handler、失败占用和倒计时均沿 D053。
- 本关验证的新结构：第二受保护角色可在不新增关卡脚本的情况下由 Inspector `additionalActors` 注入。
- 暂不共享：不把双人目标选择上提到 DrawLevelCtrl，也不修改共享 ActorCtrl。
- 候选：若后续双人关卡目标分布需要权重或区域约束，再在 Hive/关卡局部增加参数，不改当前随机语义。

## 9. 验证证据与缺口

- 静态检查：D065 prefab 135 个定义、0 重复 anchor、0 悬空引用；两个 Actor、`additionalActors`、单 Hive 和倒计时引用有效；无释放 Handler。
- 配置检查：Luban 生成成功；JSON 含 20065，排序为第 57 项。
- 编译：Unity Roslyn 定向编译退出码 0；仅有工程既有 warning。
- Unity PlayMode：`uncertain`，尚未实测两个角色的随机命中、`swell`、失败演出和重开。
- Android/iOS 横屏：`uncertain`。

## 10. 风险与回滚

- 主要风险：随机选择是均匀随机而非配额保证，短局内可能连续命中同一角色；两个角色的嵌套 Collider/Spine 导入和失败后继续运动仍需实测。
- 回滚路径：移除 D065 `additionalActors` 和第二角色可恢复单角色目标；共享代码回滚需确认 D062 后再进行。配置撤销需同步移除 20065 行并重新运行 Luban。
