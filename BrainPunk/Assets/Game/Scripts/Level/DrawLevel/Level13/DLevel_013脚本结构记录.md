# DLevel_013 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame `DLevel_013`
- 记录日期：2026-08-03
- 当前目标：猫夹球首笔释放后，球以更紧凑的重力节奏下落；普通地面和玩家画线当前均按
  `0.98` 高弹参数试玩，球必须从上方向下完成进筐才能成功，并在确认进筐点播放常规成功门彩纸烟花。
- 关卡 Prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_013.prefab`
- 局部控制器：`Assets/Game/Scripts/Level/DrawLevel/Level13/DLevel13Ctrl.cs`
- 参考基线：`DLevel_002` 的猫夹球入场、首笔释放、重开完成态。

## 2. 脚本与组件树

```text
DLevel_013
├─ DrawLevelCtrl
│  └─ successExtraRendererGroup -> yellow_ball
├─ DrawLineFeature
│  └─ DrawGame_DrawLine Rigidbody2D
│     └─ DLevel013_LineDampedBounce
├─ DrawLevelCountdownCtrl              # 15s，首笔后开始
├─ DLevel13Ctrl : DLevel2Ctrl
│  ├─ 可见横向入场、首笔开夹放球、Retry
│  ├─ 篮筐上/下平面扫掠判定
│  └─ 成功点实例化共享 BasketSuccessFirework
├─ level13
│  └─ Alien / catch / yellow_ball
│     └─ Rigidbody2D + CircleCollider2D + Actor
├─ Sp_hx_lqk_01                         # 篮筐前景
│  ├─ left BoxCollider2D             # 实体篮沿
│  └─ right BoxCollider2D            # 实体篮沿
├─ Sp_hx_lqk_02                         # 篮筐背景
├─ Ground / Platform
└─ FallArea
```

## 3. 类职责与边界

| 类/组件 | 职责 | 不负责事项 |
| --- | --- | --- |
| `DLevel13Ctrl` | 复用猫夹球生命周期；根据篮沿 Bounds 跟踪球心从上向下进筐 | 不创建篮筐 Trigger，不直接写 DB/UI |
| `DLevel2Ctrl` | 入场、猫 `control`、夹子 `clip_open`、球 Dynamic、Retry 直接就位 | 不判断篮筐几何 |
| PhysicsMaterial2D | 由 Physics2D 接触求解器控制地面与画线弹性；本轮两者均约 `0.98` | 不使用碰撞回调强改速度 |
| `DrawLevelCtrl.TryTriggerSuccessWithoutDoor` | 校验当前 Actor，占用胜利，可延迟渐隐 Actor/球，不播放门动画 | 不判断进筐方向 |
| `DrawGame_BasketSuccessFirework` | 复用常规成功门的 Confetti 配置，以非循环、非自播方式播放一轮 | 不提交结果，不跟随已离开的球 |
| 两个篮沿 BoxCollider2D | 真实挡球、撞框和提供开口几何 | 不是 Trigger，不直接提交胜利 |

## 4. 生命周期与进筐判定

1. 首次进关沿用 DLevel002 的可见 Intro；失败、手动重来或 Replay 不重播 Intro。
2. 第一笔有效线完成后，夹子打开，球从 Kinematic 切为 Dynamic，15 秒倒计时开始。
3. 每个 `FixedUpdate` 读取球壳 CircleCollider2D 的世界 Bounds 中心，并按世界 X 对两个篮沿排序。
4. 开口为 `left.bounds.max.x -> right.bounds.min.x`；上平面为两篮沿顶部较高值，下平面为
   两篮沿底部较低值。
5. 球心首先以向下速度扫过上平面且 X 在开口内，才进入候选；之后继续向下扫过
   下平面才成功。从下往上、侧向移入、只碰到框或进入后离开开口均不成功。
6. 成功判定会立即占用结果锁，但不清速度、不切 Kinematic；球继续按 Dynamic 物理离开篮筐。
7. 只有结果锁占用成功后，才在球心穿越下平面的插值点实例化共享彩纸烟花并显式 `Play(true)`。
   等待 `0.35` 秒后，`DrawLevelCtrl` 才渐隐人物和蓝球，并在渐隐完成后提交胜利。

## 5. Prefab 与物理参数

| 对象 | 参数 | 当前值/作用 |
| --- | --- | --- |
| 球 Rigidbody2D | Mass / Gravity / Detection | `10 / 1.5 / Continuous`；本关局部提高重力，减少轻飘感 |
| 球 Rigidbody2D | Linear Damping | 本关局部 `0.03`，降低空中能量衰减 |
| 球材质 | Friction / Bounce | `0.05 / 0.98`，Bounce Combine `Multiply` |
| 画线材质 | Friction / Bounce | `0.35 / 0.98`，Bounce Combine `Minimum` |
| 普通地面 | 接触弹性 | 默认地面与球组合后约 `0.98`，接近等高回弹 |
| 玩家画线 | 接触弹性 | `Minimum(0.98, 0.98) = 0.98`，本轮试玩不再额外衰减 |
| 篮筐开口 | 世界宽度 | 约 `2.141`；球有效直径约 `1.526`，单边余量约 `0.307` |
| 进筐容差 | X / 最小下落速度 | `0.04 / 0.1` |
| 成功渐隐 | 延迟 / 时长 | `0.35 / 0.4` 秒；延迟期间球保持 Dynamic |

## 6. 数据、资源、UI 与事件边界

- 资源：新增两个 DLevel013 局部 PhysicsMaterial2D；烟花复用
  `Prefabs/DrawLevel/Shared/DrawGame_BasketSuccessFirework.prefab`；其 SceneLayer 根排序为 Order `220`，
  高于球和篮筐前景。不修改共享 `Bouncy`、共享猫夹球或共享画线 Prefab。
- DB/Luban/存档：不新增字段或迁移，仍走 Draw 通用结算。
- UI：沿用 HUD、15 秒倒计时和胜利界面，无新窗口。
- 事件：只复用 `DrawLevelCtrl` 局部结果链，不新增全局 `PlayEvents`。
- Analytics/SDK/平台：不新增打点、广告、IAP、RemoteConfig 或平台分支。

## 7. 验证与缺口

- Assembly-CSharp Roslyn 全量编译为 `0 error`。
- Prefab 静态检查：D013 `109` 个 YAML anchor 无重复、本地 fileID 无缺失；控制器、球、两篮沿、
  材质和烟花引用非空。Unity PrefabImporter 已重新导入 D013 与共享烟花。
- 旧 `OnTriggerSuccess` 仍固定播放成功门；新的无门入口只由 DLevel013 直接调用。
- `uncertain`：完整 PlayMode 的高弹节奏、擦框/空心/下方反弹、进球点烟花层级与尺寸、成功后球继续下落与延迟渐隐、
  15 秒失败、Reset/Replay 及 Android/iOS 横屏仍待验证。

## 8. 风险与回滚

- 篮筐已按当前美术调整扩大物理开口；Contact Offset、画线角度或球旋转仍可能造成擦框，优先
  PlayMode 调整两个 BoxCollider 几何，不先扩大代码容差。
- 高弹球可能长时间不休眠；全局 Bounce Threshold 保持 `1`，未改整个项目。
- 本轮根据试玩反馈将 Gravity Scale 从 `1` 调到 `1.5`、画线 Bounce 从 `0.65` 调到 `0.98`；
  若节奏过快或线条失去引导作用，可分别回调这两个本关局部参数。
- 球在成功后继续物理运动，`0.35` 秒内可能因高速擦框离开篮筐区域；这是当前要求的表现，
  如渐隐开始得过晚只调 `successFadeDelay`，不要恢复成功定格。
- 烟花只在成功锁占用后播放，Reset/Disable 会立即 Stop/Clear 并销毁运行时实例；若特效尺寸或
  Sorting 不合适，只调整共享烟花 Prefab，不直接改 Epic Toon FX 原始资源。
- `uncertain`：D013 既有球物理与 `FixedUpdate` 进筐检测未按 `DrawLevelCtrl.IsPaused` 冻结，复用的
  无门成功等待/渐隐和新烟花也未做独立暂停；暂停中球、判定及得分后演出均可能继续推进。该问题
  不是本轮烟花定位改动引入，但需 PlayMode 验证并在统一暂停治理时处理。
- 回滚时移除 `DLevel13Ctrl` 和两个局部材质覆盖，恢复原 DLevel15Ctrl 仅会恢复“可放球但无进筐结算”的旧状态。
- 无门成功 API 可独立回滚，但必须同时让 DLevel13 改用其它结算入口，否则会编译失败。
