# DLevel_040 脚本结构记录

## 1. 基本信息

- 关卡：`DLevel_040`
- 记录日期：2026-08-04
- 交付/验收状态：代码与 prefab 静态实现完成；Unity PlayMode、Android/iOS 待验收
- 参考基线关卡：`DLevel_008`、`DLevel_035`
- 选择基线理由：DLevel_008 已采用“第一笔有效线后 Kinematic→Dynamic”的局部刚体入口；
  DLevel_035 已覆盖结算和 Attempt Reset 的局部生命周期边界。本关把持续动力交给 Unity AreaEffector2D，
  不复用两关的直接力或逐帧推进实现。
- 主要脚本：`DLevel040Ctrl.cs`
- 关卡 prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_040.prefab`

## 2. 脚本与组件树

```text
DLevel_040
├─ DrawLevelCtrl                   # Draw 生命周期、结算和 Attempt Reset 分发
├─ DrawLineFeature                 # 有效笔画生成与 StrokeCompleted 通知
├─ DrawLevelCountdownCtrl          # 第一笔后 10 秒失败倒计时
├─ DLevel040Ctrl                   # Actor 首笔释放与 WindArea 开关、结算和复位
└─ Scene
   ├─ Actor (nested prefab)        # Player 标签；根 Rigidbody2D 初始 Kinematic
   └─ Fan
      └─ WindArea                  # 初始 inactive；BoxCollider2D + AreaEffector2D
```

本关不新增 Actor 子类。`DLevel040Ctrl` 不直接调用 `Rigidbody2D.AddForce`，也不向共享
`ActorCtrl`、`DrawLevelCtrl` 或 `DrawLineFeature` 添加单关 API。

## 3. 类职责与边界

| 类/组件 | 类型 | 职责 | 继承/接口 | 创建与销毁方 | 不负责事项 |
| --- | --- | --- | --- | --- | --- |
| `DLevel040Ctrl` | 本关局部控制器 | 首笔切 Dynamic；启停 WindArea；结算、Disable 与 Attempt Reset 时关闭风区并复位 Actor | `MonoBehaviour`、`IDrawStrokeCompletedHandler`、`IDebuger` | 随本关 prefab 创建和销毁 | 计算或施加风力、轨迹、胜负、UI、存档 |
| `AreaEffector2D` | Unity 2D 物理组件 | 对进入 WindArea 的 Actor 层碰撞体施加配置风力 | `Effector2D` | prefab 预置 | 首笔门禁和 Actor 状态切换 |
| `DrawLevelCtrl` | 共享关卡控制器 | 分发笔画完成/Attempt Reset 并提交结算 | `IDrawLevelRuntime` 等 | Draw 主流程 | 本关风区参数 |
| Actor `Rigidbody2D` | 嵌套 prefab 物理组件 | 接收 AreaEffector2D、重力和碰撞 | Unity `Rigidbody2D` | Actor prefab | 风区启停决策 |

## 4. 生命周期与关键调用

1. `OnEnable` 仅在 PlayMode 解析 Actor、同根 `DrawLevelCtrl` 与 `Scene/Fan/WindArea`，缓存 Actor 初始
   世界位置、角度和刚体类型，订阅 `Resolved`，并强制关闭 WindArea。
2. `DrawLineFeature` 完成第一笔有效线后，经 `DrawLevelCtrl` 同步调用
   `DLevel040Ctrl.OnDrawStrokeCompleted()`。
3. 控制器只响应第一笔一次：Actor 切为 Dynamic、速度清零并唤醒；随后激活 WindArea 并启用
   AreaEffector2D。后续动力完全由 Physics2D 求解。
4. 后续笔画不重复切换 Actor 或风区。脚本不存在 `Update`、`FixedUpdate` 或 `AddForce`。
5. `Resolved` 与 `OnDisable` 关闭 WindArea；`OnDrawAttemptReset` 关闭风区并恢复 Actor 初始刚体类型、
   世界位姿和零速度，允许下一次尝试重新响应第一笔。

## 5. Prefab、序列化与资源绑定

| 宿主 | 字段/组件 | 当前绑定或参数 | 必填/可选 | 缺失时行为 |
| --- | --- | --- | --- | --- |
| `DLevel_040` | `DLevel040Ctrl.actorRigidbody` | `Scene/Actor` 根 `Rigidbody2D` | 必填 | 按 Player 标签局部回退；仍缺失则 Bear 错误日志并停用流程 |
| `DLevel_040` | `DLevel040Ctrl.drawLevelCtrl` | 同根 `DrawLevelCtrl` | 必填 | 同根回退；缺失时无法订阅结算 |
| `DLevel_040` | `DLevel040Ctrl.windAreaEffector` | `Scene/Fan/WindArea` 的 `AreaEffector2D` | 必填 | 按固定本关层级回退；仍缺失则 Bear 错误日志并停用流程 |
| `WindArea` | GameObject 初态 | inactive | 必填 | `OnEnable` 仍会强制关闭，第一笔后开启 |
| `WindArea` | AreaEffector2D | Angle `180`、Magnitude `40`、Variation `0`、Mask `2048` | 可调 | 参数直接决定风向、强度和作用层 |
| `WindArea` | BoxCollider2D | Trigger、Used By Effector、Size `15×5` | 必填 | 缺少任一配置时 AreaEffector2D 不能按预期形成风区 |
| Actor | `Rigidbody2D.bodyType` | 初始 Kinematic | 必填 | 首笔改为 Dynamic；Attempt Reset 恢复初值 |

## 6. 数据、UI、资源与事件流

- DB/存档：本关控制器不读写；通关进度继续由既有 Draw 结算链处理。
- Luban/配置：不新增字段或表；风区参数只保存在 DLevel_040 prefab。
- UI：沿用 Draw HUD、10 秒失败倒计时和结果界面。
- 事件：只复用 `DrawLineFeature -> DrawLevelCtrl -> IDrawStrokeCompletedHandler` 与局部 `Resolved`；
  未新增 `PlayEvents` 或全局事件。
- 资源加载/Analytics/SDK：无新增资源地址、打点、广告、IAP、RemoteConfig 或平台分支。

## 7. 相对原实现的差异与结构复盘

| 差异 | 原因 | 玩家/运行时影响 | 验证范围 | 是否形成演进候选 |
| --- | --- | --- | --- | --- |
| 移除脚本 `FixedUpdate + AddForce` | 用户要求纯 AreaEffector2D 动力 | 风力由 WindArea 的形状、方向、力值和 Layer Mask 决定 | 首笔前后、进出风区、轨道手感 | 本关必要差异 |
| 移除 `leftwardForce/stopWorldX` | AreaEffector2D 已拥有动力参数；风区边界替代代码 X 阈值 | Actor 离开 WindArea 后自然停止获得风力 | 风区范围、惯性、门可达性 | 不形成共享 API |
| 结算/Reset 关闭整个 WindArea | 避免重试前或结果画面继续施力 | 每次尝试都从相同无风初态开始 | 失败、重试、Replay、结算 | 沿用局部生命周期边界 |

结构复盘结论：当前机制完全由 DLevel_040 prefab 的局部 AreaEffector2D 承担，没有理由修改共享 Draw、
Actor 或全局物理配置；后续多个已验收关卡出现相同首笔风区门禁前不抽取共享组件。

## 8. 验证证据与缺口

- 代码静态扫描：`DLevel040Ctrl` 不含 `Update`、`FixedUpdate`、`AddForce`、`OnValidate`、`Reset`、DB/Save、
  全局事件或资源加载。
- Prefab YAML 静态检查：149 个 anchor 无重复，149 个唯一非零本地引用无缺失；
  `DLevel040Ctrl` 脚本 GUID 与 `windAreaEffector` 显式绑定各一处。
- Assembly-CSharp 独立全量编译通过：`0 error / 101 warning`；警告来自工程其它既有脚本，
  未出现 DLevel040Ctrl 编译警告。
- `uncertain`：Unity PlayMode 的首笔前无风、第一笔后生效、风区边界、Magnitude 40 手感、轨道绕行、
  暂停/恢复、清线、失败重试、Replay 和门可达性尚未人工验收。
- `uncertain`：Android/iOS 横屏触控、不同固定帧率下 AreaEffector2D 手感与 60 fps 表现未验证。

## 9. 风险与回滚

- 主要风险：AreaEffector2D 的范围 `15×5` 与力值 `40` 尚未 PlayMode 调参；过宽会持续推压轨道，过窄或
  过弱可能不能提供足够惯性。
- 完整回滚：恢复 DLevel040Ctrl 的直接力版本并恢复旧序列化字段；共享系统和数据无需迁移。
- 快速停用：禁用或移除 WindArea，Actor 仍会在第一笔后切为 Dynamic，但不会获得主动风力。
