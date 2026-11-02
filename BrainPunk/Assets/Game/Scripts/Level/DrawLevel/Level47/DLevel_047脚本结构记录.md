# DLevel_047 脚本结构记录

## 1. 基本信息

- 关卡：DLevel_047
- 记录日期：2026-08-04
- 当前状态：已完成上方进场与首笔完成后放球的 Prefab 绑定；完整 PlayMode 和移动端验收未完成
- 用户指定表现参考：当前 `DLevel_009.prefab` 的上方猫夹球进场方向与节奏
- 行为复用参考：DLevel_002、DLevel_027 对 `DLevel2Ctrl` 的夹球入场与首笔放球契约
- 主要运行时脚本：直接复用 `Level2/DLevel2Ctrl.cs`，本关不新增 C# 类
- 关卡 Prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_047.prefab`

## 2. 脚本与组件树

```text
DLevel_047
├─ DrawLevelCtrl
├─ DrawLineFeature
├─ DLevel2Ctrl
└─ Scene
   ├─ DrawGame_AlienClipBallRig
   │  ├─ DrawAlienClipBallRigView
   │  └─ DrawEntrancePathView
   ├─ Key
   ├─ DoorLock
   └─ 本关既有平台、门与场景对象
```

## 3. 类职责与边界

| 类/组件 | 职责 | 不负责事项 |
| --- | --- | --- |
| `DrawLevelCtrl` | 收集并串行执行可见 Intro，Intro 完成后开放画线，沿用既有结算链 | 不保存 D047 的入场参数或首笔状态 |
| `DLevel2Ctrl` | 闭夹持球入场、入场完成时继续夹球、首笔有效线完成后开夹放球、Retry 恢复就位夹球态 | 不处理 Key、DoorLock、倒计时、存档或 UI |
| `DrawAlienClipBallRigView` | 提供共享 Rig 内猫、夹子、球与 Spine 表现引用 | 不启动 Tween 或判断胜负 |
| `DrawEntrancePathView` | 提供共享 Rig 的 START/FINAL 世界位置 | 不在编辑态改写 Transform，不驱动物理 |
| `DrawLineFeature` | 沿用本关画线输入，并在有效线段完成后通知首笔处理器 | 不管理进场装置 |

## 4. 生命周期与关键调用

1. 首次进入时，`DrawLevelCtrl` 在转场揭开后调用根节点唯一的 `DLevel2Ctrl.PlayDrawIntroAsync()`。
2. `DLevel2Ctrl` 通过显式 `alien` 引用取得共享 Rig 根，并由 `DrawAlienClipBallRigView` 自动解析球、夹子与 Spine 引用。
3. D047 仅在本关 prefab 上把 `entranceStartPoint` 覆写为局部坐标 `(0, 12.022222, 0)`；装置由上方以 `1.2s / OutBack(1.9)` 入场并播放夹子惯性摆动。
4. Intro 完成后球仍保持在夹子层级中，刚体保持 Kinematic；`DrawLevelCtrl` 此时开放画线。
5. 第一笔有效线段完成时，`OnDrawStrokeCompleted()` 首次消费回调，播放 `clip_open`，把球改挂到 `Scene` 并切换为 Dynamic，同时播放 Alien `control`。
6. 同一尝试中的后续线段不会重复放球。
7. 失败、手动重来或 Replay 不重播完整 Intro；`ApplyDrawRetryIntroState()` 把装置放在 FINAL，并恢复闭夹、夹球状态，等待新尝试的第一笔。
8. Disable 时取消本组件持有的 Tween，避免旧实例异步回调继续推进。

## 5. Prefab 与序列化绑定

| 字段/组件 | 当前绑定或值 | 必要性 |
| --- | --- | --- |
| 根 `DLevel2Ctrl.drawLevelCtrl` | 同根 `DrawLevelCtrl` | 必填；接入当前关卡生命周期 |
| `alien` | `Scene/DrawGame_AlienClipBallRig` 根 Transform | 必填；同时作为共享 Rig View 的解析入口 |
| `ballReleaseParent` | `Scene` Transform | 必填；确保放球后不再跟随夹子层级 |
| 入场参数 | delay `0`、duration `1.2`、overshoot `1.9` | 复用当前 DLevel_009 的上方入场节奏 |
| 夹子惯性参数 | `38 / 0.18 / 32 / 0.25 / 15 / 0.14 / 7 / 0.11 / 3 / 0.09 / 0.08` | 沿用 `DLevel2Ctrl` 的现有衰减摆动配置 |
| Rig 内部引用 | 序列化为空，由 `alien` 上的 `DrawAlienClipBallRigView` 在运行时解析 | 现有控制器支持的最小绑定方式；不新增跨关引用 |
| `entranceStartPoint` | 本关 prefab override：`x=0`、`y=12.022222` | 把共享 Rig 默认左侧起点改为 D047 的上方起点 |
| `DrawEntrancePathView` | 继承共享 Rig 组件，未移除 | `DLevel2Ctrl.CacheInitialState()` 的路径数据来源 |
| `DrawLineFeature.strokeMass` | `250` | 本关画线刚体质量调优，不影响共享画线脚本 |
| 释放球物理参数 | PhysicsMaterial2D `c3a457...`、gravity scale `4`、linear damping `0` | Intro/Retry 时 `DLevel2Ctrl` 仍强制 Kinematic；第一笔完成后切 Dynamic 并使用这些本关参数 |
| 场景摆位 | 多个平台、提示对象、Key/DoorLock 相关实例与 Rig 子节点采用本关 prefab override | 只调整 D047 内容布局，不修改来源 prefab |

## 6. 数据、UI、资源与事件流

- DB/存档、Luban/配置：本次不读取、不新增、不修改。
- UI：只沿用 `DrawLevelCtrl` 的 Intro 画线门禁，不直接操作 HUD。
- 资源：继续复用 D047 已有共享 Rig、Key、DoorLock、场景和画线资源，无新增加载地址。
- 事件：无新 `PlayEvents`、Bear 全局事件或 UnityEvent；首笔完成沿用现有局部接口回调。
- Analytics/SDK/平台：无修改或平台分支。

## 7. 相对参考基线的差异

| 差异 | 原因 | 影响 |
| --- | --- | --- |
| 不复用 D009 当前绑定的 `DLevel10Ctrl` | D009 会在 Intro 内自动开夹、退场和落地双弹，不满足“第一笔画完再松开” | D047 入场完成即开放画线，但球继续被夹住 |
| 直接复用 `DLevel2Ctrl` | 该现有脚本已具备首笔完成放球与 Retry 夹球态 | 无需修改任何已完成关卡脚本，也无需新增包装脚本 |
| 把共享起点覆写到上方 | `DLevel2Ctrl` 读取实际 START/FINAL，不使用 D009 的方向枚举 | 保留 D009 的上方入场观感，同时隔离在 D047 prefab 内 |
| 释放父节点绑定 `Scene` | D047 的玩法对象集中在 Scene 子根 | 放球后留在 D047 场景坐标系内 |

## 8. 结构复盘

- `DLevel2Ctrl` 已完整覆盖当前需求，直接绑定比新增 `DLevel47Ctrl` 更小且没有重复代码。
- 本次只改 D047 prefab 与本关/流程文档；`DLevel2Ctrl`、`DLevel10Ctrl`、D009 及其它已完成关卡脚本均未修改。
- 根节点已移除 `DelayAutoBeginDrawHandle(delay=0)`，避免同一关存在重复 Intro 门禁。
- “已完成或已验收关卡脚本默认只读复用”已固化到仓库 `AGENTS.md`，并登记到 `DevelopMD/AI规则提升队列.md`。

## 9. 验证证据与缺口

- 代码证据：`DLevel2Ctrl.PlayDrawIntroAsync()` 在入场结束时保持夹球模拟；`OnDrawStrokeCompleted()` 只在每次尝试第一次有效回调时开夹放球；`ApplyDrawRetryIntroState()` 恢复 FINAL 夹球态。
- 静态检查：D047 根节点只绑定一个 `DLevel2Ctrl`，不存在 `DLevel10Ctrl` 或 `DelayAutoBeginDrawHandle` 绑定。
- 静态检查：`alien`、`Scene` 释放父节点、上方 `entranceStartPoint` override 和 `DrawEntrancePathView` 可解析；134 个 YAML anchor 唯一，134 个唯一非零本地引用无缺失。
- Unity 6000.0.59f2 已通过 `PrefabImporter` 导入当前 DLevel_047，目标导入段未出现反序列化、断言或编译错误。
- C#：本次未修改运行时代码；现有已完成关卡脚本 diff 为空。
- 关卡内容调优：画线质量已改为 `250`；释放球序列化为 gravity scale `4`、linear damping `0` 并绑定本关物理材质；多处场景与 Rig 子节点坐标已在 D047 prefab 内调整。
- 未验证：首次上方入场观感、入场完成后的夹持稳定性、第一笔结束瞬间开夹放球、Retry、暂停、胜负、Key/DoorLock 联动以及 Android/iOS 横屏表现，均为 `uncertain`。

## 10. 风险与回滚

- 主要风险：上方路径与夹子摆动组合的视觉节奏、第一笔线段刚体生成与球切 Dynamic 的同帧碰撞，以及质量、重力、物理材质与新摆位共同作用后的可解性仍需 PlayMode 验证。
- 内容调优回滚：仅恢复 D047 prefab 中本次质量、球物理参数和场景坐标的上一版值，不需要修改或回滚任何复用脚本。
- 回滚当前“首笔放球”调整：可把 D047 根组件恢复为此前绑定的 `DLevel10Ctrl`，并移除本关对上方 START 点的坐标覆写；这会恢复 Intro 内自动放球、退场和双弹行为。
- 完全回滚到本次需求前：把根组件恢复为 `DelayAutoBeginDrawHandle(delay=0)`，移除本关 Intro 绑定。共享脚本、共享 Rig prefab、D009、Key、DoorLock 和场景摆位无需回滚。
