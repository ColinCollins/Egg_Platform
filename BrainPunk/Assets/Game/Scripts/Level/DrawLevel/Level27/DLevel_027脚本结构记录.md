# DLevel_027 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame `DLevel_027`
- 记录日期：2026-08-03
- 当前状态：已建立独立控制器并清理跨关卡残留绑定；Prefab 与 C# 静态验证完成，完整 PlayMode 与移动端验收待补
- 参考基线：`DLevel_002`
- 选择理由：两关都使用单套共享猫夹球装置，首次可见时横向入场，第一笔有效画线后开夹放球，重开时直接应用完成态
- 主要脚本：
  - `Assets/Game/Scripts/Level/DrawLevel/Level27/DLevel27Ctrl.cs`
  - `Assets/Game/Scripts/Level/DrawLevel/Level2/DLevel2Ctrl.cs`
  - `Assets/Game/Scripts/Level/DrawLevel/DrawLevelCtrl.cs`
  - `Assets/Game/Scripts/Level/DrawLevel/DrawLineFeature.cs`
  - `Assets/Game/Scripts/Level/DrawLevel/DrawLevelCountdownCtrl.cs`
- 关卡 Prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_027.prefab`
- 共享装置：`Assets/Game/Prefabs/DrawLevel/Shared/DrawGame_AlienClipBallRig.prefab`

## 2. 脚本与组件树

```text
DLevel_027
├─ DrawLevelCtrl
├─ DrawLineFeature
├─ DrawLevelCountdownCtrl                  # 第一笔后启动 20 秒失败倒计时
├─ DLevel27Ctrl : DLevel2Ctrl              # 入场、首笔开夹放球、Retry 完成态
├─ Alien                                   # 共享猫夹球 Rig，Transform 摆在最终位置
│  ├─ DrawEntrancePathView / entranceStartPoint
│  ├─ 猫 SkeletonAnimation / ActorSpineCtrl
│  └─ catch / clipSwingPivot / yellow_ball
│     └─ Rigidbody2D + CircleCollider2D + 子 Actor
├─ platform_Stone                          # 静态斜坡，仅 Transform/Sprite/BoxCollider2D
├─ DrawGame_DrawLine
├─ DrawGame_SuccessDoor                    # 唯一启用的成功门
├─ DrawGame_SuccessDoor_DisabledLegacy     # 禁用旧门，不参与运行时
├─ FallArea_Left / Right / Bottom / Top
└─ GameCamera
```

## 3. 类职责与边界

| 类/组件 | 职责 | 继承/接口 | 不负责事项 |
| --- | --- | --- | --- |
| `DLevel27Ctrl` | 为 D027 建立独立脚本身份；直接复用单套装置 Intro、放球和 Retry | `DLevel2Ctrl` | 不控制静态斜坡、门、FallArea、倒计时或存档 |
| `DLevel2Ctrl` | 解析共享 Rig；保持夹子闭合和球 Kinematic；首笔后播放 `clip_open/control` 并让球脱离后进入 Dynamic | `IDrawLevelVisibleIntroHandler`、`IDrawStrokeCompletedHandler` | 不判断本关地形和胜负 |
| `DrawLevelCtrl` | 串行 Intro 门禁、分发 StrokeCompleted、成功/失败结算、角色与附属球壳渐隐 | Draw 共享控制器 | 不写本关物理参数 |
| `DrawLevelCountdownCtrl` | 第一笔有效线后开始 20 秒倒计时；归零时提交失败 | Draw 共享倒计时 | 不直接提交成功 |
| `platform_Stone` | 以实体 BoxCollider2D 提供固定斜坡碰撞 | 静态 Prefab 实例 | 不旋转、不释放、不需要 Rigidbody2D/HingeJoint2D 或关卡控制器 |

## 4. 生命周期与关键调用

1. 首次实例化时，`DLevel27Ctrl` 继承 `DLevel2Ctrl` 的引用解析与缓存流程。Alien 在 Prefab 中配置的是最终位置 `(-5.93, 5.76)`；运行时由共享 `DrawEntrancePathView` 提供起点。
2. 转场完全揭开后，关卡只等待 `DLevel27Ctrl` 的可见 Intro。Alien 以 `0.78s / OutBack(1.9)` 横向飞到最终位置，夹子从反向惯性姿态逐步回摆，球保持 Kinematic 并随夹子移动。
3. Intro 完成后开放画线；本关不叠加额外 Delay 或强制超时，门禁时长只由共享入场 Sequence 决定。
4. 第一笔有效线完成后，线本身先进入 Dynamic 物理；随后 `DLevel27Ctrl` 只消费一次 StrokeCompleted：夹子播放 `clip_open`，球保持世界姿态脱离 Spine 层级并切为 Dynamic，猫播放 `control` 后回 `idle01`。同帧启动 20 秒失败倒计时。
5. 球和线由 Physics2D 与静态斜坡、地面共同求解。继续画线不会重复放球，静态斜坡也不会切换状态。
6. 球内 Actor 进入启用的成功门后，沿用 `DrawLevelCtrl.OnTriggerSuccess`；门打开后 Actor 与 `successExtraRendererGroup` 绑定的球壳同步渐隐并提交成功。
7. 球复合刚体进入任一 FallArea，或 20 秒倒计时归零，沿用共享失败重建流程。失败、手动重开与 Replay 不重播入场，而是在黑幕下直接恢复最终位置、闭夹持球状态。

## 5. Prefab、序列化与关键参数

| 对象 | 当前值 | 作用 |
| --- | --- | --- |
| Alien FINAL | `(-5.93, 5.76)`，Scale `0.9` | 编辑器中直接展示最终落点；起点由共享路径视图配置 |
| DLevel27Ctrl Intro | delay `0`、duration `0.78`、OutBack `1.9` | 直接沿用共享可见横向入场 |
| yellow_ball | mass `5`、gravity scale `1.5`、linear damping `0.03` | 初始 Kinematic，首笔后 Dynamic |
| platform_Stone | 位置 `(-5.39,-0.14)`、角度 `34.695°`、Scale `1.3120143` | 固定斜坡；Box `4.024662 × 0.46279836`，无刚体/铰链 |
| DrawLineFeature | width `0.2`、ink `50`、多笔、stroke mass `32`、minimum ratio `0.25` | 生成可下落、可碰撞的 Dynamic 画线 |
| DrawLevelCountdownCtrl | `20s / isSuccess=false` | 只处理超时失败 |
| 成功门 | `(8.36,0.55)`，`OnTriggerSuccess` | 唯一启用的成功入口 |
| FallArea | 左/右 `X=-18.23/+18.1`，下/上 `Y=-10.39/+10.39` | 四向越界失败 |

## 6. 数据、UI、资源与事件流

- DB/Luban：继续使用既有 `20027 -> DLevel_027` 资源路径，未新增字段、表或迁移。
- 存档：本关控制器不直接读写；进度与胜负提交沿用 Draw 共享链。
- UI：只复用 Draw HUD、墨水、20 秒倒计时和结算界面。
- 资源：复用共享猫夹球、成功门、FallArea、画线与石台 Prefab；未复制共享美术或 Spine 资源。
- 事件：只使用局部 `DrawLineFeature -> DrawLevelCtrl -> IDrawStrokeCompletedHandler` 与门/FallArea UnityEvent；未新增全局事件。
- Analytics/SDK/平台：未新增打点、广告、IAP、RemoteConfig 或平台分支。

## 7. 相对 DLevel_002 的差异与结构复盘

| 差异 | 原因 | 影响 | 结论 |
| --- | --- | --- | --- |
| D027 使用独立 `DLevel27Ctrl` | 避免关卡 Prefab 继续误绑 `DLevel26Ctrl` | 日志、组件身份和后续扩展都归属本关 | 必要的局部薄封装 |
| 不复制 D026 的 2 秒 Intro 补丁 | D027 没有对应卡死复现，015/020 等成熟基线也直接继承共享实现 | 避免未来合法延长动画时被固定阈值提前截断 | 遵循最小单关差异 |
| 根节点不再挂 `DelayAutoBeginDrawHandle` | 该组件与可见 Intro 重复，且 delay 为 0 | 首次进关只剩一条画线门禁链 | 删除冗余组件 |
| D027 只有静态 `platform_Stone` | 当前玩法没有旋转平台或首笔释放平台 | 不需要 D026 双平台控制器 | 删除空绑定跨关卡组件，保留地形原样 |

当前没有足够证据为 D027 新增 Intro 超时或把静态斜坡规则提升为共享能力；后续应先取得本关运行时复现或另一关同类需求，再评估局部补丁或抽象。

## 8. 验证证据与缺口

- 静态确认根节点只保留 `DrawLevelCtrl`、`DrawLineFeature`、`DrawLevelCountdownCtrl` 与 `DLevel27Ctrl`；旧 D26 控制器 GUID、重复 Delay 和空平台组件均已移除。
- 共享 Rig 的 Alien、球刚体、Spine、BoneFollower、摆动 Pivot、夹子 Spine 与球附属渐隐组引用均可解析。
- 唯一启用门回调为 `DrawLevelCtrl.OnTriggerSuccess`；旧门已明确禁用；四个共享 FallArea 均启用。
- Prefab YAML 共 139 个唯一锚点、139 个唯一非零本地 fileID 引用，重复/缺失均为 0；D026 与 D027 的 Prefab/meta GUID 不冲突。
- 使用 Unity 6.0.59f2 当前 Assembly-CSharp 响应文件执行全量 Roslyn 编译：退出码 0、`0 error / 104 warning`；warning 均来自当前工程既有代码，输出未出现 DLevel27。
- Unity Editor.log 最近 8000 行未见 C#、Prefab 导入、断言或序列化错误；该证据不能替代运行时试玩。
- `uncertain`：尚未在 Unity PlayMode 实测首次入场、第一笔释放手感、斜坡弹道、进门、四向越界、20 秒失败、暂停、连续重开与 Replay。
- `uncertain`：尚未完成 Android/iOS 横屏、不同固定帧率与触控画线回归。

## 9. 风险与回滚

- 本关直接等待共享 Intro 完成；若后续实际复现 Tween 永久不返回，应先定位共享链或取得单关证据，再决定是否增加局部超时。
- `DLevel27Ctrl` 依赖共享 Rig 的路径视图、`bone2`、`clip_close/clip_open`、`idle01/control` 与球释放层级；共享资源改名或改层级需回归本关。
- 静态斜坡没有刚体和关节；若后续策划改为可旋转平台，应新增 D027 局部物理设计，不得重新挂回空绑定的 D26 双平台控制器。
- 回滚时可从根移除 `DLevel27Ctrl` 并恢复原控制器，但会重新引入 D26 身份误绑；恢复 `DelayAutoBeginDrawHandle` 或 D26 平台组件会重新产生双 Intro 或空引用风险，不建议局部回滚这两项。
