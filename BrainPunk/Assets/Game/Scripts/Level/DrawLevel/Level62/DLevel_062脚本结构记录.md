# DLevel_062 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame 062（`20062 / DLevel_062`）。
- 记录日期：2026-08-18。
- 需求：保护两个角色；每只蜜蜂在两个角色中随机选择一个固定追踪目标。猫和夹子沿既有关卡表现入场，第一笔画线后分别释放角色；任一角色被蜜蜂命中时在真实接触点播放受击特效，并播放该角色的 `swell`。
- 参考基线：D053 蜂群生存、D058 猫夹角色释放。选择理由是蜂群、失败倒计时和角色释放链分别与这两关相同。
- 主要脚本：共享 `DLevel53Ctrl`、`DLevel53Hive`、`DLevel53Bee`；共享 `DLevel2Ctrl` 的薄子类 `DLevel58ActorReleaseCtrl`。
- 关卡 prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_062.prefab`。
- 状态：静态检查和 C# 编译已完成；Unity PlayMode、Android/iOS 横屏仍为 `uncertain`。

## 2. 脚本与组件树

```text
DLevel_062
├─ DrawLevelCtrl
├─ DrawLineFeature
├─ DelayAutoBeginDrawHandle
├─ DrawLevelCountdownCtrl (10s, success)
├─ DLevel53Ctrl
│  ├─ actor -> 第一角色
│  └─ additionalActors[0] -> 第二角色
├─ DLevel53Hive
├─ DLevel58ActorReleaseCtrl -> 第一猫/夹子/角色
├─ DLevel58ActorReleaseCtrl -> 第二猫/夹子/角色
└─ 场景、FallArea、水体与 D003 Knock_Explosion
```

运行时生成的蜜蜂来自 `DLevel53Hive` 内嵌的 `BeeTemplate_AdjustHere`；蜂巢 prefab 和蜜蜂 prefab 均沿用 D053 资源。

## 3. 类职责与边界

| 类/组件 | 类型 | 职责 | 继承/接口 | 不负责事项 |
| --- | --- | --- | --- | --- |
| `DrawLevelCtrl` | 共享控制器 | 扫描 Intro/首笔/Reset Handler，管理输入、倒计时和标准结算 | 既有 Draw 契约 | 不选择蜜蜂目标、不播放 `swell` |
| `DLevel53Ctrl` | 共享蜂群结算控制器 | 登记两个受保护 Actor，解析命中角色，去重受击、`swell` 和延迟失败 | `IDrawStrokeCompletedHandler`、`IDebuger` | 不生成蜜蜂、不驱动猫夹入场 |
| `DLevel53Hive` | 蜂巢控制器 | 首笔后建立批次、分散路线并为每只 Bee 绑定随机角色；Reset/结算时清理或暂停 | `IDrawStrokeCompletedHandler`、`IDebuger` | 不直接结算失败、不播放角色动画 |
| `DLevel53Bee` | 蜜蜂实例 | 两帧动画、朝目标追踪、碰撞施力、反冲和碰撞回调 | `MonoBehaviour` | 不决定关卡胜负 |
| `DLevel58ActorReleaseCtrl` | 本关入口薄子类 | 复用 D002 的可见入场、夹持和首笔释放逻辑 | `DLevel2Ctrl` | 不改蜂群、倒计时或存档逻辑 |

## 4. 生命周期与关键调用

1. 运行时初始化时 `DLevel53Ctrl` 解析首个 Actor 和 `additionalActors`，只登记 Layer 为 `Actor` 且 Tag 为 `Player` 的对象。
2. 转场后 `DrawLevelCtrl.PlayIntroAsync()` 按当前层级扫描顺序逐个等待两个 `DLevel58ActorReleaseCtrl` 的 Intro。每套猫夹从 `entranceStartPoint` 飞入，夹持角色保持 Kinematic；两套 Intro 当前不是并行播放，见风险章节。
3. 第一笔有效画线由 `DrawLevelCtrl` 分发给两个释放 Handler 和 Hive。每个释放 Handler 开夹并将对应角色脱离动画层级、切为 Dynamic；Hive 建立本轮批次和攻击角度。
4. Hive 每次实例化 Bee 时调用 `DLevel53Ctrl.GetRandomTargetActor()`，目标在该 Bee 生命周期内固定，不会因角色移动或其它 Bee 命中而改换。
5. Bee 与两个角色的非 Trigger Collider 碰撞后，`DLevel53Ctrl` 用实际 Collider 解析被命中的角色，在接触点播放 Knock/音效，并只对该角色调用一次 `Die()` + `swell`。首次命中占用标准失败演出并等待 `failureDelay=1.5s`；等待期间其它 Bee 继续生成、飞行和命中，其它角色也可播放一次 `swell`，但不重复提交失败。
6. 成功倒计时由既有 `DrawLevelCountdownCtrl` 复用；失败演出期间由共享 Draw 失败占用冻结倒计时，结束后提交标准失败重建。Resolved、Reset、Replay、退出会停止/清理全部 Hive 子节点。

## 5. Prefab、序列化与资源绑定

| 宿主 | 字段/组件 | 绑定对象或资源 | 必填/可选 | 缺失时行为 |
| --- | --- | --- | --- | --- |
| 根 | `DLevel53Ctrl.actor` | 第一角色 stripped `ActorCtrl` | 必填 | 无有效目标时 Hive 不启动 |
| 根 | `DLevel53Ctrl.additionalActors[0]` | 第二角色 stripped `ActorCtrl` | 双角色必填 | 缺失则退化为单角色目标列表 |
| 根 | `DLevel53Ctrl.hive` | 第一 `DLevel53Hive` 实例 | 兼容字段 | Hive 自身会回退扫描父层级 |
| 根 | `DLevel53Ctrl.knockEffect` | D003 `Knock_Explosion` | 可选 | 仍执行失败和 `swell`，但无粒子 |
| 根 | 两个 `DLevel58ActorReleaseCtrl` | 两套 Alien、夹子和角色 Rigidbody2D | 必填 | 对应套装记录错误并跳过缺失动作 |
| Hive | `beeTemplate` | `DLevel53Bee` 内嵌模板 | 必填 | 不生成运行时 Bee |
| 角色 | SkeletonData/材质 Override | 含 `swell` 的 `sp_box_025` 资源 | 必填 | 运行时缺动画时保留标准 `die` 并记录错误 |

静态 YAML 证据：根控制器位于 `DLevel_062.prefab:167`，两个释放组件位于 `:186`、`:221`；`additionalActors` 绑定第二角色。该 prefab 有两个角色 SkeletonData Override、两个释放控制器和一个 Hive。

## 6. 数据、UI、资源与事件流

- 配置：Luban 源表已有 `20062`，运行映射为 `20062 -> DLevel_062`；Draw 排序为第 54 项。没有新增存档字段或玩法配置表。
- 画线/结算：沿用 `DrawLineFeature -> DrawLevelCtrl -> IDrawStrokeCompletedHandler`、`DrawLevelCountdownCtrl` 和 Draw 标准成功/失败生命周期。
- 资源：蜜蜂、蜂巢、猫夹、Actor 骨骼和 D003 Knock 随 prefab/既有资源加载；无新地址、全局事件、Analytics、广告、SDK 或平台分支。
- UI：仅复用现有 Draw 画线与倒计时 UI。

## 7. 相对参考基线的差异

| 差异 | 原因 | 玩家/运行时影响 | 验证范围 | 是否形成演进候选 |
| --- | --- | --- | --- | --- |
| `DLevel53Ctrl` 增加 `additionalActors` | 两个角色都要成为蜂群目标 | 每只 Bee 可随机追踪两个角色，单角色关卡空数组保持兼容 | D062/D065 静态绑定、隔离编译 | 暂不再抽象 |
| 命中表现按 Actor 去重 | 失败演出期间仍允许另一角色被命中 | 每个角色最多播放一次 `swell`，失败只提交一次 | 代码路径和资源静态检查 | 继续观察多蜂同帧 |
| 两个 D058 Handler 挂在同一根节点 | 复用既有猫夹流程 | 当前按 Draw Handler 扫描顺序串行入场，而非同步入场 | YAML 组件顺序；PlayMode 未验证 | 保留为风险候选 |

## 8. 可复用结论与优化候选

- 稳定结构：蜂群职责继续拆在 Ctrl/Hive/Bee 三层；角色释放继续由 D002 基类承担。
- 本关验证的新结构：一个蜂群控制器管理多个受保护 Actor，Hive 不保存单一目标。
- 暂不共享：双套猫夹并行编排没有上提到共享 `DrawLevelCtrl`，因为其它关卡的 Intro 时序尚未覆盖。
- 后续候选：若产品确认两套装置必须同时飞入，可增加 D062 局部聚合 Intro Handler，避免修改共享扫描语义。

## 9. 验证证据与缺口

- 静态检查：D062 prefab 131 个定义、重复 anchor 0、悬空本地引用 0；脚本 GUID、两个 Actor、两个释放组件和一个 Hive 均可解析。
- 配置检查：`tbdrawleveldata.json` 和 `tbdrawlevelsort.json` 已生成并包含 20062；Luban 生成命令成功。
- 编译：Unity Roslyn `Assembly-CSharp.rsp` 定向编译退出码 0；仅有工程既有 warning。
- Unity PlayMode：`uncertain`，尚未实测双目标随机性、两套猫夹入场/释放、两角色分别 `swell`、失败重开。
- Android/iOS 横屏：`uncertain`，尚未执行设备压力和触控验证。

## 10. 风险与回滚

- 主要风险：两个可见 Intro 当前由 `DrawLevelCtrl` 顺序等待，可能出现先后入场；嵌套角色 Collider 与 SkeletonData 导入仍需运行时确认；失败 1.5 秒内的多目标碰撞需验证视觉叠加。
- 回滚路径：移除 `DLevel_062.prefab` 的 `additionalActors`、第二角色和第二 `DLevel58ActorReleaseCtrl` 可回到单角色/单装置；若回滚共享多目标能力，需先确认 D065，再移除 `DLevel53Ctrl` 的列表与按角色命中逻辑，并恢复单目标命中链。
