# DLevel_020 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame `DLevel_020`
- 记录日期：2026-08-03
- 当前状态：首笔放球与水体浮力已实现，Unity Prefab 导入和 C# 编译通过；完整 PlayMode、
  可解性与 Android/iOS 横屏验收待补
- 参考基线：`DLevel_002`
- 选择理由：两关均为共享猫夹球横向可见入场，并在第一笔有效画线完成后开夹放球；本关只在
  释放后的场景物理中增加局部水体浮力，不修改共享装置或既有关卡
- 主要脚本：
  - `Assets/Game/Scripts/Level/DrawLevel/Level20/DLevel20Ctrl.cs`
  - `Assets/Game/Scripts/Level/DrawLevel/Level2/DLevel2Ctrl.cs`
  - `Assets/Game/Scripts/Level/DrawLevel/DrawLevelCtrl.cs`
  - `Assets/Game/Scripts/Level/DrawLevel/DrawLineFeature.cs`
- 关卡 Prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_020.prefab`
- 共享装置：`Assets/Game/Prefabs/DrawLevel/Shared/DrawGame_AlienClipBallRig.prefab`

## 2. 脚本与组件树

```text
DLevel_020
├─ DrawLevelCtrl
├─ DrawLineFeature
├─ DrawLevelCountdownCtrl                  # 第一笔后启动 20 秒失败倒计时
├─ DLevel20Ctrl : DLevel2Ctrl              # 横向 Intro、首笔开夹、放球、Retry
├─ Alien                                   # 共享猫夹球 Rig，根节点摆在最终位置
│  ├─ DrawEntrancePathView / entranceStartPoint
│  ├─ 猫 SkeletonAnimation / ActorSpineCtrl
│  └─ catch / clipSwingPivot / yellow_ball
│     └─ Rigidbody2D + CircleCollider2D + 子 Actor
├─ Water
│  ├─ InteractableWater                    # 水面网格与受撞波纹
│  ├─ WaterTriggerHandler                  # 球入水时把竖直速度映射到水面波纹
│  └─ Buoyancy
│     ├─ BoxCollider2D (Trigger, Used By Effector)
│     └─ BuoyancyEffector2D                # 仅对 Layer 11 蓝球施加浮力/阻力
├─ DrawGame_DrawLine                       # 完成后生成 Dynamic 重线
├─ DrawGame_SuccessDoor
├─ FallArea
└─ GameCamera
```

## 3. 类职责与边界

| 类/组件 | 职责 | 继承/接口 | 不负责事项 |
| --- | --- | --- | --- |
| `DLevel20Ctrl` | 给本关建立独立脚本身份，并复用 DLevel002 的 Intro、首笔释放和 Retry 契约 | `DLevel2Ctrl` | 不实现水力、胜负、UI 或存档 |
| `DLevel2Ctrl` | 缓存 START/FINAL，闭夹持球入场，消费第一笔，播放 `clip_open/control` 并把球切 Dynamic | `IDrawLevelVisibleIntroHandler`、`IDrawStrokeCompletedHandler` | 不处理水面和画线物理 |
| `BuoyancyEffector2D` | 按浸没面积、流体密度和阻力持续求解真实 2D 浮力 | Unity Physics2D | 不控制动画、计时或结果 |
| `InteractableWater` | 生成水面 Mesh，并在球穿过水面时产生波纹 | MonoBehaviour | 不给球施加浮力 |
| `DrawLineFeature` | 生成可碰撞 Dynamic 线段；线本身不受本关浮力 | 共享 Draw 组件 | 不直接放球或判断成功 |

## 4. 生命周期与关键调用

1. 首次实例化时，`DLevel20Ctrl` 通过共享 Rig View 解析球、猫和夹子，缓存 Alien 最终位置
   `(-6.79, 2.93)` 及 `entranceStartPoint`；运行时先把整套装置移到左侧 START。
2. 转场完全揭开后，Alien 以 `0.78s / OutBack(1.9)` 横向进入；夹子保持 `clip_close`，球为
   Kinematic 并随装置，甩动参数与 DLevel002 一致。Intro 返回后才开放画线。
3. 第一笔有效线完成后，`DrawLevelCtrl` 同步调用 `DLevel20Ctrl.OnDrawStrokeCompleted()`：夹子播放
   `clip_open`，球保持世界姿态脱离 Spine 层级后变为 Dynamic，猫播放 `control` 后回 `idle01`；
   同帧倒计时开始。
4. 球从静止释放后受重力加速，穿过世界水面 `Y=-2.59` 时保留向下动量；浸没部分开始受到
   `BuoyancyEffector2D` 的浮力与线性阻力，先继续下冲，速度反转后回到水面附近并产生物理摆动。
5. 玩家可继续画线。线条位于 Layer 8、重力倍率 5，且不在浮力 Mask 内；线与 Layer 11 球保持
   正常碰撞，因此可在水面上方落下并用自身重量把球压入水下。
6. 球进入成功门走既有 Draw 成功链，人物与蓝球同步渐隐；进入 FallArea 或 20 秒归零走既有
   失败重建。失败、手动重来和 Replay 不重播 Intro，而是在黑幕下直接恢复 FINAL 闭夹持球态。

## 5. Prefab 与关键参数

| 对象 | 参数 | 当前值与作用 |
| --- | --- | --- |
| Alien | FINAL / START | FINAL `(-6.79,2.93)`；起点局部 X `-12.2`，共享缩放 0.9 后世界 START 约 `(-17.77,2.93)` |
| DLevel20Ctrl | Intro | `delay 0 / duration 0.78 / OutBack 1.9`，甩动沿用 DLevel002 参数 |
| yellow_ball | Rigidbody2D | 初始 Kinematic，首笔后 Dynamic；本关覆盖质量 12、Gravity Scale 1、Linear Damping 0.2 |
| Water | 视觉范围 | 根 Y `-15.09`，Width 30、Height 25，可见液面世界 Y `-2.59` |
| Buoyancy | 区域 | Box `30×25`，Trigger 且 Used By Effector；与视觉水体范围一致 |
| BuoyancyEffector2D | 浮力 | Enabled；Surface Level `12.5`、Density `8`、Linear Drag `1`、Collider Mask `2048`（Layer 11）；保留斜坡产生的水平惯性 |
| DrawLineFeature | 线物理 | 允许多笔、最大墨水 20、满长质量 30、最小质量比 0.25 |
| Draw line Rigidbody2D | 下压力 | Layer 8、Gravity Scale 5；质量约为 `7.5 + 1.125 × 当前线长`，不受水浮力 |
| DrawLevelCountdownCtrl | 失败计时 | `20s / isSuccess=false` |

## 6. 数据、资源、UI 与事件流

- DB/存档：本关局部控制器与水体不直接读写；通关进度仍由既有 Draw 结算链处理。
- Luban/配置：沿用已有 `DLevel_020` 路径，不新增字段或表。
- 资源：复用共享猫、Spine 夹子、蓝球和 Level16 的水面表现组件；无运行时地址加载。
- UI：只复用 Draw HUD、倒计时和结果界面。
- 事件：复用 `DrawLineFeature -> DrawLevelCtrl -> IDrawStrokeCompletedHandler` 局部链；未新增
  `PlayEvents` 或全局事件。
- Analytics/SDK/平台：没有新增打点、广告、IAP、RemoteConfig 或平台分支。

## 7. 相对 DLevel002 的差异与结构复盘

| 差异 | 原因 | 影响 | 结论 |
| --- | --- | --- | --- |
| 本关新增局部水体 Effector | 解法需要球下冲后浮起并由线压下 | 释放后轨迹由重力、浮力、阻力和碰撞共同求解 | 本关必要差异，不上提共享控制器 |
| 浮力只作用于球，不作用于线 | 线需要保持重量才能压球 | 球会浮、重线会沉并施加接触力 | 明确的玩法 Layer 边界 |
| `DLevel20Ctrl` 继承 DLevel2Ctrl | 首笔释放生命周期完全一致 | 避免复制 Spine/镜像/Retry 代码 | 局部安全复用 |
| 成功额外渐隐组绑定蓝球 | 延续所有球关卡的成功表现要求 | 球与人物同步消失 | 沿用共享可选边界 |

本关没有形成新的共享脚本候选：Unity 内置浮力参数和水面范围属于 DLevel020 玩法配置，不能
在缺少其它已验收水球关证据时批量迁移。

## 8. 验证证据与缺口

- Unity 已识别 `DLevel20Ctrl.cs` 并写入 Assembly-CSharp 工程/编译响应文件。
- DLevel020 PrefabImporter 成功；127 个 YAML anchor 无重复，本地 fileID 引用无缺失。
- Assembly-CSharp Roslyn 全量编译：`0 error`。
- 静态确认 DLevel10Ctrl GUID 已从本关根控制组件移除，DLevel20Ctrl 实现链包含
  `IDrawStrokeCompletedHandler`；浮力 Mask 只包含 Layer 11，Physics2D Layer Matrix 允许
  Layer 8 线与 Layer 11 球碰撞。
- `uncertain`：尚未完成 Unity PlayMode 中实际下冲深度、反转时间、液面停留、线条压球手感、
  成功门、FallArea、连续重开和暂停验证。
- Android/iOS 横屏 60 fps、触控画线和不同固定帧率下的浮力手感仍未验证。

## 9. 风险与回滚

- 浮力手感受球入水速度、浸没面积、Density、Linear Drag 和 Fixed Timestep 共同影响；静态参数
  只能证明力链成立，不能替代 PlayMode 调参。
- 首轮运行反馈确认 Linear Drag `5` 使球入水后过早停止，因此本关局部降为 `1`；
  Density `8`、Flow `0` 与共享球 Linear Damping `0.2` 不变，避免同时改变上浮能力和方向性。
- 线质量明显高于球，长线可能把球持续压到底部；这是当前解法能力，但需要验证墨水与 20 秒
  倒计时是否仍能形成可控难度。
- 回滚夹子修复：把根控制器恢复旧 DLevel10Ctrl 会重新造成首笔不响应，不建议单独回滚；正确
  回滚应移除 DLevel20Ctrl 并重新明确本关释放时机。
- 回滚水体：禁用 BuoyancyEffector2D 即恢复纯重力落水；Surface Level、Mask 和其它物理参数
  不涉及数据迁移。删除本关脚本/文档时必须保留共享 DLevel2Ctrl 和共享 Rig，避免影响旧关。
