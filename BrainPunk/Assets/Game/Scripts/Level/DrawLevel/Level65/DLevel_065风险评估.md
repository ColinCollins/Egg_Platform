# DLevel_065 风险评估

## 1. 需求与原行为基线

- 目标：D053 蜂群关改为保护两个角色，并在被蛰角色位置/角色本体播放 `swell`。
- 基线：单 Actor、单 Hive、首击失败占用和 D003 Knock；角色由场景原有结构提供，不新增猫夹。
- 本次实际变化：D065 根 `DLevel53Ctrl` 绑定 `additionalActors[0]`，共享脚本按 Bee/Actor 去重并支持第二目标。

## 2. 风险与控制

| 风险 | 等级 | 控制措施 | 当前证据 | 待补验证 |
| --- | --- | --- | --- | --- |
| 蜜蜂始终追踪同一角色 | 高 | 每只生成 Bee 调用 Ctrl 随机目标并缓存 | Hive 代码与 additionalActors 静态检查 | 多次重开记录分布 |
| 第二角色 Collider 解析失败 | 高 | 支持 Collider 根、父级和附着 Rigidbody2D 解析 | 第二 Actor stripped 引用有效 | 两角色身体不同位置命中 |
| 第二角色未播放 `swell` | 高 | 每个 Actor 独立 HashSet；两个 Actor 都覆盖含动画的 SkeletonData | prefab 2 条 SkeletonData Override | PlayMode Spine 轨道/材质 |
| 首击后重复失败 | 高 | `_isFailing` 与 Bee/Actor 去重，失败演出只提交一次 | 共享 Ctrl 静态审查 | 多蜂同帧/CollisionStay |
| 随机性看起来不平均 | 中 | 当前为均匀随机，不承诺短窗口配额 | 代码语义明确 | 产品是否需要不重复配额 |
| 角色物理状态与既有关卡不一致 | 中 | 未修改 ActorCtrl、刚体质量或物理材质 | Prefab 只增加目标绑定 | PlayMode 落地/碰撞 |

## 3. 数据、资源与回滚

- 不新增 DB、存档、全局事件、Analytics 或平台链路；20065 只复用既有 Draw 配置。
- 回滚优先移除 D065 第二 Actor 和 `additionalActors`，保留共享实现以支持 D062；若整体回滚共享实现，需同步检查 D062。

## 4. 验证证据与未决项

- 已完成：D065 135 个 YAML 定义、0 重复 anchor、0 悬空引用；两个角色/骨骼/单 Hive 引用有效；Luban 生成成功；Roslyn 编译退出码 0。
- `uncertain`：Unity PlayMode、不同角色随机分布、命中接触点、双 `swell`、失败重开、Android/iOS 横屏和低帧率碰撞。
- 验收重点：多次首笔确认目标不固定；先命中角色 A 再命中 B 时两个角色都播 `swell`，但只重开一次；成功倒计时不会抢占失败演出。
