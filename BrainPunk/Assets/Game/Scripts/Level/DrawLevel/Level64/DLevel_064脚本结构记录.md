# DLevel_064 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame 064（`20064 / DLevel_064`）。
- 记录日期：2026-08-18。
- 需求：无新玩法，检查复制配置并沿用 D053 蜂群生存关。
- 参考基线：D053。D064 保持单 Actor、单 Hive、单倒计时和同一失败链。
- 关卡 prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_064.prefab`。
- 状态：静态检查和 C# 编译已完成；Unity PlayMode、Android/iOS 横屏仍为 `uncertain`。

## 2. 脚本与组件树

```text
DLevel_064
├─ DrawLevelCtrl
├─ DrawLineFeature
├─ DelayAutoBeginDrawHandle
├─ DrawLevelCountdownCtrl (10s, success)
├─ DLevel53Ctrl (单 Actor)
├─ DLevel53Hive (单蜂巢)
└─ 场景、FallArea、水体与 D003 Knock_Explosion
```

## 3. 类职责与边界

| 类/组件 | 类型 | 职责 | 继承/接口 | 不负责事项 |
| --- | --- | --- | --- | --- |
| `DrawLevelCtrl` | 共享控制器 | 画线输入、Handler 分发、倒计时/胜负/Reset | 既有 Draw 契约 | 不改变蜂群参数 |
| `DLevel53Ctrl` | 共享蜂群结算控制器 | 单角色命中、接触点 Knock、`swell`、延迟失败 | `IDrawStrokeCompletedHandler`、`IDebuger` | 不生成 Bee |
| `DLevel53Hive` | 蜂巢控制器 | 建立批次并生成 Bee，暂停/清理运行实例 | `IDrawStrokeCompletedHandler`、`IDebuger` | 不独立结算 |
| `DLevel53Bee` | 蜜蜂实例 | 追踪、碰撞施力和反冲 | `MonoBehaviour` | 不管理 UI/存档 |

## 4. 生命周期与关键调用

1. `DrawLevelCtrl` 初始化并缓存本关局部 Handler；单 Hive 在第一笔有效画线后建立 D053 同样的生成计划。
2. Hive 为每只 Bee 绑定本关唯一 Actor，Bee 在 3 秒窗口内按批次出现；蜜蜂间碰撞保持忽略。
3. Bee 命中角色后，Ctrl 在接触点播放 Knock/音效，执行角色 `Die()` 后播放 `swell`；首击等待 1.5 秒并提交标准失败，期间共享成功倒计时冻结。
4. 成功、失败、Reset、Replay 和退出继续沿 Draw 标准生命周期；本关没有额外 Intro、猫夹或机关脚本。

## 5. Prefab、序列化与资源绑定

| 宿主 | 字段/组件 | 绑定对象或资源 | 必填/可选 | 缺失时行为 |
| --- | --- | --- | --- | --- |
| 根 | `DLevel53Ctrl.actor` | 单 Actor stripped 引用 | 必填 | Hive 无目标时不启动 |
| 根 | `DLevel53Ctrl.hive` | 单 Hive stripped 组件 | 必填 | Ctrl 仍尝试子节点回退扫描 |
| 根 | `knockEffect` | D003 Knock_Explosion | 可选 | 无粒子但失败链保留 |
| Hive | `beeTemplate` | D053 Bee 模板 | 必填 | 不生成 Bee |
| Actor | SkeletonData | `sp_box_025`（含 `swell`） | 必填 | 缺失时保留默认 `die` 并记录错误 |

## 6. 数据、UI、资源与事件流

- 配置：Luban 运行 JSON 已包含 `20064 -> DLevel_064`，Draw 排序为第 56 项；未增加字段。
- 画线/结算：沿用 DrawLineFeature、DrawLevelCtrl、DrawLevelCountdownCtrl 和共享失败演出占用。
- 资源：蜂巢、Bee、Actor 骨骼、Knock、水体和 FallArea 均为现有资源；无新增 DB、存档、全局事件、Analytics、SDK 或平台分支。
- UI：复用现有 Draw 画线、倒计时和成功/失败 UI。

## 7. 相对参考基线的差异

| 差异 | 原因 | 玩家/运行时影响 | 验证范围 | 是否形成演进候选 |
| --- | --- | --- | --- | --- |
| 仅更换关卡 prefab 内容 | 用户确认无新机制 | 玩法流程与 D053 相同 | 配置、Prefab、GUID 静态检查 | 否 |
| 共享 Ctrl 支持可选多目标 | 为 D062/D065 兼容，D064 保持空数组 | 单角色行为通过首个 `actor` 保持不变 | 单角色字段静态检查、Roslyn | 已记录兼容边界 |

## 8. 可复用结论与优化候选

- 稳定结构：无新脚本；继续使用 D053 的 Ctrl/Hive/Bee 职责拆分和 Draw Handler 生命周期。
- 本关验证的新结构：没有新的关卡结构；只验证共享多目标改动对空 `additionalActors` 的兼容。
- 暂不共享：不为复制关卡新增包装控制器或配置抽象。

## 9. 验证证据与缺口

- 静态检查：D064 prefab 130 个定义、0 重复 anchor、0 悬空本地引用；单 Ctrl、单 Hive、倒计时和 Actor 引用有效，无 D062/D063 专用组件。
- 配置检查：Luban 生成成功，JSON 含 20064，排序为第 56 项。
- 编译：Unity Roslyn 定向编译退出码 0；仅有工程既有 warning。
- Unity PlayMode：`uncertain`，尚未执行进入、画线、命中、失败重开和通关体验。
- Android/iOS 横屏：`uncertain`。

## 10. 风险与回滚

- 主要风险：复制 prefab 的资源导入、蜂群力度和 `swell` 视觉仍需运行时确认；静态检查不能替代实际碰撞/倒计时体验。
- 回滚路径：D064 无新增脚本或共享配置；回滚只需恢复/删除本关 prefab 与对应 `20064` 配置行，不能回滚共享多目标代码以免影响 D062/D065。
