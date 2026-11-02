# DLevel_015 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame `DLevel_015`
- 记录日期：2026-08-02
- 当前状态：右侧镜像猫/夹子/蓝球装置、可见入场、首笔释放、15 秒倒计时、无影子和
  成功时球/人物同步渐隐已接入；完整 PlayMode 与 Android/iOS 横屏验收为 `uncertain`
- 参考基线：`DLevel_002` 的单套装置流程，以及 `DLevel_006` 的镜像释放边界
- 选择理由：DLevel_015 与 DLevel_002 的触发时机、动画、倒计时和成功链同构，只是
  入场方向与装置根镜像不同
- 本关脚本：`Assets/Game/Scripts/Level/DrawLevel/Level15/DLevel15Ctrl.cs`
- 复用脚本：`Level2/DLevel2Ctrl.cs`、`DrawLevelCtrl.cs`、`DrawLineFeature.cs`、
  `DrawLevelCountdownCtrl.cs`、`DrawAlienClipBallRigView.cs`、`DrawEntrancePathView.cs`
- 关卡 Prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_015.prefab`
- 共享装置：`Assets/Game/Prefabs/DrawLevel/Shared/DrawGame_AlienClipBallRig.prefab`

## 2. 脚本与组件树

```text
DLevel_015
├─ DrawLevelCtrl                         # 运行时、结果锁和 Handler 分发
├─ DrawLineFeature                       # 画线、Collider 和线条刚体
├─ DrawLevelCountdownCtrl                # 首笔后 15 秒失败倒计时
├─ DLevel15Ctrl : DLevel2Ctrl            # 本关身份；复用单套装置流程
└─ level15
   ├─ Alien                              # 共享 Rig 镜像实例；根位置就是 FINAL
   │  ├─ DrawAlienClipBallRigView
   │  ├─ DrawEntrancePathView
   │  ├─ entranceStartPoint              # 世界 START (16.39, 2.41)
   │  ├─ 猫 SkeletonAnimation / ActorSpineCtrl
   │  └─ catch / BoneFollower(bone2)
   │     └─ clipSwingPivot               # 静止补偿 -4.22°
   │        ├─ clipVisual                # clip_close / clip_open
   │        └─ yellow_ball               # localZ=180°
   │           ├─ Rigidbody2D / CircleCollider2D
   │           ├─ SpriteRendererGroupAlpha
   │           └─ payloadContentRoot
   │              └─ Actor.prefab
   │                 └─ Shadow           # 共享 override 为 inactive
   ├─ DrawGame_DrawLine.prefab
   ├─ DrawGame_SuccessDoor.prefab
   ├─ FallArea.prefab
   └─ GameCamera.prefab
```

`Alien` 根为 `FINAL=(5.41,2.41,0)`、`scale=(-0.9,0.9,0.9)`；起点为
`START=(16.39,2.41,0)`。Scene 圆标、虚线路径、箭头和起点占位只用于编辑预览。

## 3. 类职责与边界

| 类/组件 | 职责 | 继承/接口 | 不负责事项 |
| --- | --- | --- | --- |
| `DrawLevelCtrl` | 初始化 Draw、扫描 Handler、锁定胜负、同步渐隐 Actor 与球渲染组 | `IDrawLevelRuntime` 等 | 不保存 Level15 坐标和镜像参数 |
| `DrawLineFeature` | 画线输入、有效笔画、线 Collider 与 Dynamic 刚体 | MonoBehaviour | 不开夹、放球或倒计时 |
| `DrawLevelCountdownCtrl` | 第一笔有效线后启动一次 15 秒倒计时 | `IDrawStrokeCompletedHandler` | 不控制 Intro 和 Spine |
| `DLevel15Ctrl` | 以独立关卡类型复用单套镜像装置行为 | 继承 `DLevel2Ctrl` | 当前不覆写基类逻辑 |
| `DLevel2Ctrl` | START/FINAL、入场摆动、首笔开夹、镜像安全放球、Reset | `IDrawLevelVisibleIntroHandler`、`IDrawStrokeCompletedHandler` | 不处理存档、HUD 或全局事件 |
| `DrawAlienClipBallRigView` | 暴露猫、挂点、Pivot、架子、球、payload 和渐隐组 | MonoBehaviour | 不决定时序或胜负 |
| `DrawEntrancePathView` | 读取 FINAL/START 并绘制 Scene 轨迹 | MonoBehaviour | 不改 Transform、不播放 Tween |

## 4. 生命周期与关键调用

1. `Awake()` 解析共享 Rig、绑定 `BoneFollower` 到 `bone2`，一次性缓存世界 START/FINAL，
   再把 Alien 移到 START；同时缓存球、payload、Collider offset 和 Pivot 初态。
2. 球恢复 Kinematic 与零速度，猫播放 `idle01`，架子播放 `clip_close`。
3. 遮罩完全揭开后，Alien 在 `0.78s` 内从右向左 `DOMove` 到 FINAL，使用
   `Ease.OutBack(1.9)`。
4. Pivot 相对静止基线先保持 `+38°/0.18s`，再到
   `-32°/+15°/-7°/+3°/0°`，分段 `0.25/0.14/0.11/0.09/0.08s`；约 `0.85s`
   后回到缓存基线并开放画线。
5. 第一笔有效线完成时，线条物理已建立；倒计时启动一次，架子播放 `clip_open`，猫播放
   `control` 后回 `idle01`。
6. 球保持世界位姿脱离动画父级，把负缩放迁移到 `payloadContentRoot`，同步镜像
   CircleCollider2D offset，然后切 Dynamic 并 `WakeUp()`。
7. Actor 子碰撞体进入成功门后，人物与 `yellow_ball` 使用同一 alpha 同步渐隐；FallArea
   或倒计时归零继续走既有失败重建。
8. 失败、手动重来或胜利 Replay 会 Kill 未完成 Tween，但不再返回 START 或重播 Intro；
   新实例在黑幕下直接位于 FINAL，并恢复夹持父级、Kinematic、payload、Collider、静止补偿
   与两套 Spine 初态。

## 5. Prefab、序列化与资源绑定

| 宿主 | 字段/数值 | 当前绑定 | 缺失或错误时影响 |
| --- | --- | --- | --- |
| DLevel 根 | 控制器 | 唯一 `DLevel15Ctrl` | 不会运行 Intro/释放 |
| `Alien` 根 | FINAL / 镜像 / 整体尺寸 | `(5.41,2.41,0)` / `(-0.9,0.9,0.9)` | 落点、方向或整体尺寸错误 |
| `entranceStartPoint` | START | `(16.39,2.41,0)` | Intro 取消，避免错误目标 |
| 控制器 | Intro | `0s / 0.78s / OutBack(1.9)` | 曲线和门禁时长改变 |
| 控制器 | 惯性摆 | `+38 -> -32 -> +15 -> -7 -> +3 -> 0°` | 手感或最终姿态异常 |
| 共享 Pivot | 静止补偿 | `localZ=-4.22°` | `bone2 +4.22°` 使夹子/球倾斜 |
| 共享球 | 静止旋转 | `localZ=180°` | 球壳/角色方向失配 |
| 共享 Actor | Shadow | GameObject inactive | 球内角色重新出现影子 |
| `yellow_ball` | 物理 | 初始 Kinematic，释放后 Dynamic | 不能夹持或下落 |
| `yellow_ball` | 成功渐隐组 | `successExtraRendererGroup` | 人物淡出但球壳保留 |
| 倒计时 | `cdTime/isSuccess` | `15/false` | 首笔后不会限时失败 |
| DrawLineFeature | 画线口径 | `isConfig=false / maxInkLength=50` | 当前不读取 Luban 15 |

## 6. 数据、UI、资源与事件流

```text
遮罩揭开
  -> START (16.39,2.41) --0.78s/OutBack(1.9)--> FINAL (5.41,2.41)
  -> 夹子衰减摆结束 -> 开放画线

第一笔有效线
  -> 线条先进入物理
  -> 15s 倒计时
  -> clip_open + 猫 control
  -> 球脱父级 -> 镜像迁移到 payload -> Collider offset 镜像
  -> 球 Dynamic

Actor 进入成功门 -> Actor + yellow_ball 同步渐隐 -> 既有成功结算
```

- DB/存档：无新增读写，进度继续由 Draw 成功链处理。
- Luban：未改表；`isConfig=false`，实际使用 Prefab 墨水 50，而非表中 15。
- UI：复用现有 Draw HUD、倒计时和胜负界面；未新增 UI 接口。
- 事件/资源：复用局部 Handler 和 Prefab 资源引用；未新增全局事件或运行时地址加载。
- Analytics/广告/SDK/平台：无修改。

## 7. 相对基线的差异

| 差异 | 原因 | 影响 |
| --- | --- | --- |
| `DLevel15Ctrl : DLevel2Ctrl` | 保留关卡身份且避免复制同构流程 | 后续可独立扩展；基类修改需回归两关 |
| 根 `(-0.9,0.9,0.9)`、右侧 START | 本关从右入场且共享装置统一缩小为 90% | 入场方向镜像，START/FINAL、时长和幅度不变 |
| 释放前迁移负缩放 | Dynamic 刚体根不保留反射 | 球、Actor、Collider 不在释放瞬间翻转 |
| 共享 Shadow inactive | 所有球内角色不要影子 | DLevel02/06/15 生效；DLevel10 已本地关闭 |
| Pivot/球成对补偿 | 抵消 `idle01/bone2 +4.22°` | 入场结束后夹子与球回正 |
| Prefab 墨水 50 | `isConfig=false` | Luban 15 当前被忽略 |

## 8. 可复用结论

- 单套、首笔释放的关卡可用 `DLevel2Ctrl` 行为基类和关卡薄子类；双装置仍由
  `DLevel6Ctrl` 独立处理。
- 实体根表示 FINAL，`entranceStartPoint` 表示 START；移动前必须缓存两者世界坐标。
- Spine 跟随、惯性摆与物理球分层；Tween 相对缓存 Pivot 叠加并回到补偿基线。
- 镜像应留在 payload 视觉层，不留在 Dynamic Rigidbody2D 根；Collider offset 同步处理。
- 球位于 Actor 父层级，成功时用额外渲染组同步渐隐；Shadow 不改通用 Actor prefab。

## 9. 验证证据与缺口

- 静态确认唯一 DLevel15Ctrl、`level15` 子根、共享 Rig、START/FINAL、负 X 镜像、Intro/
  摆动参数和 15 秒倒计时。
- 静态确认 Pivot `-4.22°`、ball `180°`；共享 Shadow 覆盖 DLevel02/06/15，DLevel10
  保持本地 inactive。
- 静态确认镜像迁移发生在 Dynamic 前，payload/Collider/Reset 路径完整，球渐隐组已绑定。
- 静态确认 `isConfig=false / maxInkLength=50`。
- Unity PlayMode 为 `uncertain`：需验证最终回正、首笔放球无跳变、倒计时、成功渐隐、
  Reset/Replay 与 15 秒临界成功竞争。
- Android/iOS 为 `uncertain`：需验证横屏边缘、30/60 fps、触控、物理和切后台恢复。

## 10. 风险与回滚

- 风险：基类改动会同时影响 DLevel02/DLevel15；共享 Shadow/Pivot 会影响 DLevel02/06/15；
  猫 Spine 更新后 `4.22°` 可能失效；镜像迁移漏掉 payload 或 Collider 会产生视物错位；
  Prefab 50 与 Luban 15 存在认知差异；成功门演出期间倒计时竞态仍属既有风险。
- 回滚 DLevel15 时只移除本关控制器和绑定，不删除 DLevel02/DLevel06 仍使用的共享资源。
- 回滚镜像释放必须成组恢复球根、payload、Collider offset 与 Reset。
- 回滚静止补偿必须成对恢复 Pivot/ball；回滚无影子需恢复共享 Shadow 并另查 DLevel10。
- 回滚 FINAL/START 必须同时恢复根、起终点对象与缓存方向；只移动 Transform 会破坏 Intro。
- DB、Luban、UI、analytics、SDK 与平台代码无修改，不需要数据迁移。

## 11. 共享 Rig 0.9 整体缩放

- 当前 DLevel15 根缩放为 `(-0.9,0.9,0.9)`，猫、夹子、球、Actor 和它们的碰撞体统一缩小 `10%`。
- `entranceStartPoint.localX=-12.2`，经镜像根 `-0.9` 后世界偏移仍为 `+10.98`，
  因此 START 仍为 `(16.39,2.41,0)`，FINAL 仍为 `(5.41,2.41,0)`。
- 质量、重力、画线、倒计时、动画和结算不变。需 PlayMode 实测缩小后的落球碰撞、
  成功门进入和镜像释放瞬间；未实测部分为 `uncertain`。
- 回滚须同时恢复共享根 `(1,1,1)`、DLevel15 X `-1` 和起点 local X `-10.98`，
  不能只恢复其中一项。

## 12. 2026-08-02 镜像球 Actor 碰撞结构

- `payloadContentRoot/Actor` 保留 prefab 身份与 ActorCtrl；共享 RemovedComponents 当前包含
  Actor Rigidbody2D 和 PolygonCollider2D。
- 成功门仍由 ActorCtrl 身份结算，球壳仍由 successExtraRendererGroup 同步渐隐；落球碰撞只使用
  镜像迁移后的 yellow_ball CircleCollider2D，不再使用 Actor 轮廓。
