# DLevel_047 上方进场与首笔放球功能及风险评估

## 1. 需求与背景

- 用户要求 DLevel047 复用当前 DLevel_009 的上方进场效果，并在进场完成、开放画线后，等第一笔画完再松开球。
- 用户明确要求不得修改原有关卡脚本，并要求长期约束：已完成关卡脚本除非指定，否则不允许擅自修改。
- D009 当前使用的 `DLevel10Ctrl` 会在 Intro 内自动开夹放球、退场并播放落地双弹，不能直接满足“第一笔后放球”。
- 现有 `DLevel2Ctrl` 已提供“入场保持夹球 + 第一笔有效线完成后开夹放球 + Retry 恢复夹球态”的完整契约，因此 D047 直接复用该脚本。

## 2. 影响范围

- `Assets/Game/Prefabs/DrawLevel/DLevel_047.prefab`
- `Assets/Game/Scripts/Level/DrawLevel/Level47/` 本地中文记录
- DrawGame 中央结构演进记录、`DevelopMD/已有功能设计与使用记录.md`
- `AGENTS.md` 与 `DevelopMD/AI规则提升队列.md`：固化用户指定的已完成关卡脚本保护规则

未修改 `DLevel2Ctrl`、`DLevel10Ctrl`、D009 prefab、共享 Rig prefab、`DrawLevelCtrl`、`DrawLineFeature`、Key、DoorLock、DB、存档、Luban、UI、资源地址、全局事件、analytics、SDK 或平台配置。

## 3. 行为前后

### 最初状态

- 转场后只执行零秒等待并开放画线。
- 共享 Rig 保持编辑器摆位，没有可见入场，也没有按首笔放球。

### 中间方案（已撤回）

- 曾在 D047 prefab 上绑定 `DLevel10Ctrl` 复用 D009 当前完整 Intro。
- 该方案会在开放画线前自动放球、退场和双弹，与用户后续明确的“第一笔画完再松开”冲突，因此已从 D047 绑定中撤回；未修改 `DLevel10Ctrl` 内容。

### 当前行为

- 根节点只绑定一个现有 `DLevel2Ctrl`，替换原零秒 Delay Handler。
- D047 通过 prefab override 把共享 Rig 的 START 点改到上方，按 `1.2s / OutBack(1.9)` 入场并播放夹子衰减摆动。
- Intro 完成后装置停在 FINAL，球仍被闭合夹子持有；随后开放画线。
- 第一笔有效线段完成时开夹，球改挂到 `Scene` 并切换为 Dynamic；后续笔画不重复触发。
- 失败、手动重来和 Replay 直接恢复 FINAL 夹球态，等待新尝试的第一笔，不重播完整进场。
- 当前关卡内容调优将画线质量设为 `250`，释放球使用本关 PhysicsMaterial2D、gravity scale `4`、linear damping `0`，并调整多处平台、提示对象与 Rig 子节点摆位。

## 4. 数据、资源、UI 与事件流

```text
DrawLevelCtrl 可见 Intro
  -> DLevel2Ctrl 读取 Rig + DrawEntrancePathView
  -> 闭夹装置从上方入场并完成摆动
  -> 保持夹球，Intro 返回
  -> DrawLevelCtrl 开放画线
  -> 第一笔有效线段完成
  -> clip_open + 球改挂 Scene + Rigidbody2D 切 Dynamic
```

- DB/存档/Luban：无变化。
- 资源：只复用 D047 已有共享 Rig 和现有脚本，无新增地址或实例化资源。
- UI：仅把首次画线门禁保持到入场完成；放球由画线完成接口驱动。
- 事件：不新增全局或局部事件。
- 移动端：Android/iOS 共用 DOTween 与 Physics2D 路径，无平台分支。

## 5. 风险项

| 风险 | 等级 | 控制与现状 |
| --- | --- | --- |
| 误改已完成关卡脚本 | 高 | 本次现有脚本和 D009 diff 为空；保护规则已固化到 `AGENTS.md` 并登记规则队列 |
| Rig 或路径引用缺失 | 高 | `alien` 与 `Scene` 显式绑定；Rig View 自动解析内部引用；START override 和路径组件静态可见 |
| 同一 Rig 被多个 Intro Handler 驱动 | 高 | 根节点只保留 `DLevel2Ctrl`，不与 Delay 或 `DLevel10Ctrl` 并存 |
| 第一笔完成回调与球物理切换同帧碰撞 | 中 | 沿用现有 `DLevel2Ctrl` 的先脱离动画层级再切 Dynamic 顺序；D047 实际碰撞仍需 PlayMode |
| 上方路径与夹子摆动组合观感异常 | 中 | 仅在 D047 覆写 START，参数沿用既有值；设备观感仍为 `uncertain` |
| Retry 后首笔状态未正确复位 | 中 | 现有 `ApplyDrawRetryIntroState()` 恢复 FINAL、闭夹和 Kinematic 夹球；需 PlayMode 验证 |
| 调整后的质量、球物理和场景摆位影响可解性 | 中 | 参数与坐标均隔离在 D047 prefab；完整落球轨迹、Key/DoorLock 联动和容错仍需 PlayMode |
| Key/DoorLock 与球的物理联动不符合设计 | 中 | 本次未改既有组件；完整玩法为 `uncertain` |
| 暂停或低帧率下 Tween/物理节奏变化 | 中 | 沿用共享实现，需 PlayMode 和设备验证 |

## 6. 验证证据

- `DLevel2Ctrl.PlayDrawIntroAsync()` 在完整移动和夹子衰减摆动结束后调用夹持态模拟，不执行开夹放球。
- `DLevel2Ctrl.OnDrawStrokeCompleted()` 由 `_hasReleasedBall` 保证每次尝试只消费第一次有效线段完成回调，并依次开夹、放球、播放 Alien control。
- `DLevel2Ctrl.ApplyDrawRetryIntroState()` 把装置放回 FINAL，并恢复闭夹与夹球状态。
- D047 prefab 中 `DLevel2Ctrl` 脚本 GUID 唯一，`DLevel10Ctrl` 与旧 Delay Handler GUID 均不存在。
- `alien` 指向 D047 的共享 Rig 根，`ballReleaseParent` 指向 D047 的 `Scene` Transform。
- 共享 `entranceStartPoint` 在 D047 实例上覆写为 `(0, 12.022222, 0)`，`DrawEntrancePathView` 未被移除。
- D047 当前 `strokeMass=250`；球的序列化物理参数为 gravity scale `4`、linear damping `0` 并绑定 PhysicsMaterial2D。`DLevel2Ctrl` 仍在夹持阶段强制 Kinematic、首笔完成后切 Dynamic。
- 多处场景对象与共享 Rig 子节点坐标变化均表现为 D047 prefab override，没有修改来源 prefab。
- 本次未改 C#；`DLevel2Ctrl`、`DLevel10Ctrl`、D009 prefab 及其它已完成关卡目标 diff 为空。
- Prefab 静态检查为 134 个唯一 YAML anchor、134 个唯一非零本地引用且无缺失；Unity 6000.0.59f2 已通过 `PrefabImporter` 导入当前 DLevel_047，目标导入段无反序列化、断言或编译错误。
- PlayMode、完整胜负、Reset/Replay、暂停和 Android/iOS 验证仍为 `uncertain`。

## 7. 回滚路径

1. 若只撤回本次关卡内容调优，恢复 D047 prefab 中画线质量、球物理参数与场景坐标的上一版值，不修改任何脚本或来源 prefab。
2. 若只撤回“第一笔后放球”，可将 D047 根组件恢复为此前 D009 同型的 `DLevel10Ctrl` 绑定，并移除 START 点的本关覆写；会恢复 Intro 内自动放球、退场和双弹。
3. 若完全回滚到本次需求前，将根组件恢复为 `DelayAutoBeginDrawHandle(drawLevelCtrl + delay=0)`，移除 D047 的可见 Intro 绑定。
4. 不需要回滚任何已完成关卡脚本、共享 Rig prefab、D009 prefab、Key、DoorLock、DB、配置或平台数据。
