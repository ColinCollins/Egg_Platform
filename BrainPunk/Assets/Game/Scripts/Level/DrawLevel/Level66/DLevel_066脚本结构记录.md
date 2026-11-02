# DLevel_066 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame 066（`20066 / DLevel_066`）。
- 记录日期：2026-08-18。
- 需求：检查复制配置，没有新的玩法机制；保留 D053 蜂群和 D058 猫夹角色释放表现，并清理复制残留。
- 参考基线：D053（蜂群）和 D058（猫夹角色释放）。
- 关卡 prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_066.prefab`。
- 状态：静态检查和 C# 编译已完成；Unity PlayMode、Android/iOS 横屏仍为 `uncertain`。

## 2. 脚本与组件树

```text
DLevel_066
├─ DrawLevelCtrl
├─ DrawLineFeature
├─ DelayAutoBeginDrawHandle
├─ DrawLevelCountdownCtrl (10s, success)
├─ DLevel53Ctrl (单 Actor)
├─ DLevel53Hive (单蜂巢)
├─ DLevel58ActorReleaseCtrl -> 猫/夹子/角色
└─ 场景、FallArea、水体与 D003 Knock_Explosion
```

## 3. 类职责与边界

| 类/组件 | 类型 | 职责 | 继承/接口 | 不负责事项 |
| --- | --- | --- | --- | --- |
| `DrawLevelCtrl` | 共享控制器 | 画线输入、Intro/首笔/Reset 分发和标准结算 | 既有 Draw 契约 | 不直接播放蜂群或夹子动画 |
| `DLevel53Ctrl` | 共享蜂群结算控制器 | 单角色命中、接触点 Knock、`swell`、延迟失败 | `IDrawStrokeCompletedHandler`、`IDebuger` | 不负责猫夹入场 |
| `DLevel53Hive` | 蜂巢控制器 | 首笔生成 Bee，暂停/清理运行实例 | `IDrawStrokeCompletedHandler`、`IDebuger` | 不独立结算 |
| `DLevel58ActorReleaseCtrl` | 角色释放入口 | 复用 D002 的可见入场、夹持和首笔 Dynamic 释放 | `DLevel2Ctrl` | 不改蜂群/倒计时 |
| `DLevel53Bee` | 蜜蜂实例 | 目标追踪、碰撞施力和反冲 | `MonoBehaviour` | 不控制角色释放 |

## 4. 生命周期与关键调用

1. D058 释放 Handler 在转场后将猫夹从起点飞入，角色在夹持阶段保持 Kinematic；Hive 和 D053 Ctrl 完成普通初始化。
2. 第一笔有效画线由 DrawLevelCtrl 分发：释放 Handler 开夹并将角色切为 Dynamic，Hive 同时建立蜂群计划。
3. Bee 命中角色后，Ctrl 在接触点播放 Knock/音效，调用角色 `Die()` 并播放 `swell`；首击进入 1.5 秒失败演出，蜂群在演出期间继续运行。
4. 成功、失败、Reset、Replay 和退出复用现有 Draw/D053/D058 生命周期；D066 不再挂载复制残留的 `DLevel32SpikeTrapHandle`。

## 5. Prefab、序列化与资源绑定

| 宿主 | 字段/组件 | 绑定对象或资源 | 必填/可选 | 缺失时行为 |
| --- | --- | --- | --- | --- |
| 根 | `DLevel53Ctrl.actor` | 单 Actor stripped 引用 | 必填 | Hive 不启动 |
| 根 | `DLevel53Ctrl.hive` | 单 Hive stripped 组件 | 必填 | 记录错误并回退扫描 |
| 根 | `DLevel58ActorReleaseCtrl` | Alien、夹子和 Actor Rigidbody2D | 必填 | Intro/首笔释放降级并记录错误 |
| 根 | `knockEffect` | D003 Knock_Explosion | 可选 | 无粒子但失败链保留 |
| Actor | SkeletonData/材质 | 含 `swell` 的现有角色资源 | 必填 | 缺动画时保留默认 `die` |

静态 YAML 证据：根 D053 Ctrl 位于 `DLevel_066.prefab:169`，D058 释放组件位于 `:186`；根节点已无 `DLevel32SpikeTrapHandle` GUID `c91427aaadab4129bb3ec5982208042b`。

## 6. 数据、UI、资源与事件流

- 配置：运行 JSON 已包含 `20066 -> DLevel_066`，Draw 排序为第 58 项；未新增字段。
- 画线/结算：复用 DrawLineFeature、DrawLevelCtrl、DLevel53Hive、DLevel58ActorReleaseCtrl 和 DrawLevelCountdownCtrl。
- 资源：既有蜂巢/Bee、猫夹 Rig、Actor 骨骼、Knock、水体和 FallArea；清理的尖刺脚本不再参与加载或事件。
- UI/存档/Analytics/SDK/平台：无新增或修改。

## 7. 相对参考基线的差异

| 差异 | 原因 | 玩家/运行时影响 | 验证范围 | 是否形成演进候选 |
| --- | --- | --- | --- | --- |
| 清理 `DLevel32SpikeTrapHandle` | 复制残留且本关无尖刺 TriggerItem | 不会错误注册尖刺处理器或产生无效依赖 | GUID 扫描与 prefab 结构 | 否 |
| 保留 D058 薄子类释放入口 | 需要保持猫夹表现 | 首笔前入场/夹持，首笔后释放角色 | 继承关系与绑定静态检查 | 否 |
| 共享 Ctrl 可选多目标 | 兼容 D062/D065，D066 未填写数组 | 单目标行为不变 | 单目标字段检查 | 已记录共享边界 |

## 8. 可复用结论与优化候选

- 稳定结构：无新玩法脚本，继续复用 D053 和 D058 的职责分层。
- 本关验证的新结构：复制关卡清理无对应场景对象的旧局部脚本组件，减少错误 Handler。
- 暂不共享：不把残留清理扩展成批量自动迁移，避免触碰其它已验收关卡。

## 9. 验证证据与缺口

- 静态检查：D066 prefab 155 个定义、0 重复 anchor、0 悬空引用；单 Ctrl/Hive/释放组件和所有本地引用有效；尖刺脚本 GUID 为 0 次。
- 配置检查：Luban 生成成功；JSON 含 20066，排序为第 58 项。
- 编译：Unity Roslyn 定向编译退出码 0；仅有工程既有 warning。
- Unity PlayMode：`uncertain`，尚未验证猫飞入、开夹、角色 Dynamic、蜂群命中和尖刺残留确实消失。
- Android/iOS 横屏：`uncertain`。

## 10. 风险与回滚

- 主要风险：复制 prefab 的猫夹引用、蜂群交互和 `swell` 资源仍需运行时确认；删除尖刺脚本后若场景未来重新加入尖刺，需要显式恢复本关专用处理器。
- 回滚路径：恢复 D066 根 `DLevel32SpikeTrapHandle` 组件块和对应脚本 GUID 即可恢复旧复制状态，但不建议在无尖刺对象时回滚；其它代码、配置和共享资源无需回滚。
