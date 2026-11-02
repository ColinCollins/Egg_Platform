# DLevel_029 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame `DLevel_029`
- 记录日期：2026-08-04
- 当前状态：已修正为共享自动投放控制器，并完成 D029 局部纯物理击球参数调整；PrefabImporter、YAML 与完整 PlayMode/移动端验收状态见第 7 节
- 参考基线：`DLevel_009`、`DLevel_031`
- 选择理由：本关属于已有“进场即放球”类型，首次进入横向飞入，到位后开夹、退场并等待球落地双弹完成；玩家第一笔只启动倒计时，不负责释放球；圆点、画线和轻球之间只需要现有 Physics2D
- 主要文件：
  - `Assets/Game/Prefabs/DrawLevel/DLevel_029.prefab`
  - `Assets/Game/Scripts/Level/DrawLevel/Level9/DLevel10Ctrl.cs`
  - `Assets/Game/Scripts/Level/DrawLevel/Level29/DLevel029_GolfBall.physicsMaterial2D`

## 2. 脚本与组件树

```text
DLevel_029
├─ DrawLevelCtrl
├─ DrawLineFeature
├─ DrawLevelCountdownCtrl                  # 第一笔后启动 20 秒失败倒计时
├─ DLevel10Ctrl                             # 入场投放、退场、球落地双弹、Retry 落点
├─ Alien / DrawGame_AlienClipBallRig       # Prefab 中摆的是最终位置
│  ├─ DrawEntrancePathView
│  ├─ 猫 SkeletonAnimation / ActorSpineCtrl
│  └─ clipSwingPivot / 夹子 Spine / yellow_ball
│     └─ Rigidbody2D + CircleCollider2D + 子 Actor
├─ Circle (4)                              # 无刚体的实体圆形支点
├─ platform_Stone / Ground_long            # 静态地形
├─ DrawGame_TipsObjectSubObject            # 虚线解法提示
├─ DrawGame_SuccessDoor
├─ FallArea_Right / Bottom / Top
└─ GameCamera
```

## 3. 类职责与边界

| 类/组件 | 职责 | 不负责事项 |
| --- | --- | --- |
| `DLevel10Ctrl` | 按 FromLeft 轨迹让共享 Rig 入场；到位开夹放球；猫夹原路退场；等待球落稳并播放双弹；Retry 直接安置落点 | 不消费笔画，不驱动画线旋转，不 AddForce/AddTorque，不判断圆点命中 |
| `DrawLineFeature` | 生成玩家画出的 Dynamic 线、Collider 和质量；维护提示对象与墨水 | 不知道 Circle，也不创建铰链或马达 |
| `Circle (4)` | 以静态场景 Collider 和零摩擦材质作为画线可接触的物理支点 | 不带 Rigidbody2D、Joint2D 或运行时脚本 |
| `DrawLevelCtrl` | 成功门、失败、Intro 门禁和球壳随 Actor 渐隐 | 不修改球或线的物理轨迹 |

## 4. 运行流程

1. 首次进入时，Alien 按 `FromLeft` 从 `entranceStartPoint` 飞到 Prefab 中配置的最终位置；飞行期间夹子闭合，球保持 Kinematic 并随装置移动。
2. 装置到位后立即播放 `clip_open` 并让球脱离动画层级、切为 Dynamic；猫夹同时沿原路径退到场外，球按真实重力落到场景碰撞面。
3. 控制器等待球落稳后播放两次衰减弹跳；猫夹退场和球表现均完成后才结束 Intro 并开放画线。玩家第一笔只让线进入物理并启动 20 秒失败倒计时，不触发放球。
4. 之后只由 Physics2D 求解：画线以本关 `gravityScale=1.15` 下落，与零摩擦实体 Circle 接触后可形成支点效果，再与低摩擦、适度回弹的球碰撞；最终轨迹仍取决于玩家线形、长度和接触位置。
5. 球内 Actor 进入启用的成功门后，沿用共享开门流程；Actor 与蓝色球壳同步渐隐并提交成功。
6. 球进入 Right、Bottom 或 Top FallArea，或倒计时归零时失败。失败、手动重开和 Replay 不重播入场；球直接放到夹点下方最近的本关静态落点并休眠，猫夹保持在退场后的场外位置。

## 5. Prefab 关键参数

| 对象 | 当前值 | 说明 |
| --- | --- | --- |
| Alien 最终位置 | `(-5.42, 0.95)`，整体 Scale `0.9` | 编辑器内直接展示最终位置 |
| 入场/退场 | FromLeft、duration `0.78s`、exit `0.8s`、OutBack `1.9`、起点 local `(-14.37, 0.04)` | 到位自动开夹，随后原路退场 |
| 落球表现 | timeout `8s`、首弹 `0.55 / 0.34s`、第二弹为高度 `55%` / 时长 `80%` | 落稳后播放两次衰减弹跳，完成前保持 Intro 门禁 |
| Retry 落点 | 向下搜索 `20` 世界单位 | 直接安置到最近本关 Static 碰撞面并 Sleep |
| yellow_ball | mass `1`、gravity `1`、linear damping `0.08`、angular damping `0.2`、Continuous | 本关绑定 `DLevel029_GolfBall`：friction `0.08`、bounce `0.25`、Minimum/Maximum Combine |
| Circle (4) | 位置 `(-5.09, 1.81)`、radius `0.3888085`、非 Trigger | 实体支点；复用 `DrawStrokeFree` 零摩擦材质，无 Rigidbody2D/Joint2D |
| DrawLineFeature | width `0.2`、ink `50`、多笔、collider radius `0.05`、stroke mass `120`、min ratio `0.25` | 线质量约为 `30 + 1.8 × 线长`，模板 `gravityScale=1.15` |
| Countdown | `20s / isSuccess=false` | 第一笔后只处理超时失败 |
| 成功门 | `(7.49, -1.49)` | 回调 `DrawLevelCtrl.OnTriggerSuccess` |

## 6. 本次修正与影响范围

- 根节点不再使用“首笔放球”的 `DLevel29Ctrl : DLevel2Ctrl`，改为与其它自动投放关卡相同的共享 `DLevel10Ctrl`。
- 控制器显式绑定共享 Rig 的 `DrawAlienClipBallRigView`，入场方向为 FromLeft；DLevel29 薄包装脚本及 meta 已移除，避免留下重复且不完整的投放流程。
- 共享 Rig 自身的 `DrawAlienClipBallRigView` 继续封装猫、球刚体、Spine 与 BoneFollower 引用；D029 控制器只显式绑定该 RigView，不再维护一组重复字段。
- `successExtraRendererGroup` 已绑定共享球壳渲染组，保证成功时球与内部角色一起渐隐。
- 现有虚线提示已加入 `DrawLineFeature.tipsObjects`，提示按钮和 Reset 可以正常控制它。
- Circle 上无 SpringJoint、`trackLine` 也为空的主线 `Level5Anchor` 残留已移除；实体 CircleCollider2D 和用户布局保持。
- 保留用户当前球质量 `1`、画线 `strokeMass=120`、多笔设置、地形、门、三个 FallArea、相机和美术位置；未增加自动转轴、马达、脚本冲量或自定义过关判定。Intro、退场与双弹只复用 `DLevel10Ctrl` 既有 Tween，不介入玩家高尔夫物理解法。
- 击球距离不足的原因不是质量：当前约 10 单位的线仍有约 `48` 质量，对质量 `1` 的球已接近重锤传能上限；继续加质量不会提高重力摆速。本关通过局部球材质、阻尼、零摩擦支点和线重力控制真实接触速度与击出后的续航。
- 首轮 `friction=0.03 / bounce=0.55`、球阻尼 `0.03 / 0.1`、线重力 `1.5` 的组合被试玩确认过易，任何普通线条都可能把球送入门。本轮回调为 `0.08 / 0.25`、`0.08 / 0.2` 与 `1.15`，保留正确绕点摆锤的低摩擦支点优势，同时削弱随机碰撞的爆发和滑行距离。
- 新材质只绑定 D029 嵌套球 Rigidbody2D；支点只复用现有 `DrawStrokeFree`。共享球 Rig、共享画线 Prefab、其它关卡和 Physics2D 全局设置均不修改。
- 配置已存在：`20029 -> DLevel_029`，Draw 排序表当前第 26 项指向 `20029`；本次未修改 Luban/JSON。
- 未修改共享 `DLevel10Ctrl`、Draw/Actor/猫夹球 Prefab、Physics2D Matrix、存档、UI、analytics、SDK 或平台分支。

## 7. 验证、风险与回滚

- 使用当前 Bee 响应文件并排除已删除源文件的隔离 Unity Roslyn 全量编译：退出码 0、`0 error / 104 warning`；warning 均为工程既有项。
- Unity 已成功以 NativeFormatImporter 导入 `DLevel029_GolfBall`，并以 PrefabImporter 重新导入当前 `DLevel_029.prefab`；最终 artifact 生成成功，未见反序列化、Missing Script 或材质引用错误。
- Prefab 当前 147 个 YAML anchor 全部唯一，本地 fileID 缺失 0；球材质、零摩擦支点材质、画线模板和共享球 Rig GUID 均可解析，旧 DLevel29 GUID 为 0。
- `uncertain`：尚未在 PlayMode 验证首次到位开夹、猫夹退场、轻球真实落地与双弹、Retry/Replay 静态落点、玩家画线绕 Circle 的实际旋转方向/力度、进门、三个 FallArea、倒计时、暂停和连续重开。
- `uncertain`：平衡参数仍需在 PlayMode 对比普通短线与绕 Circle 的完整摆锤线；目标是普通碰撞不能直接完成全程，而正确画法仍能越过约 12.5 单位的缓上坡进入门。
- `uncertain`：当前没有 Left FallArea；球若被击向最左侧，可能只能等待 20 秒超时。是否补 Left 必须结合入场路径和试玩确认，不能在未验收时盲加。
- `uncertain`：Android/iOS 固定帧率、触控精度与 Physics2D 接触稳定性尚未验证。
- 只回滚本次类型更正时，把根控制器恢复为原 DLevel29 GUID 与 DLevel2 字段，并恢复 DLevel29 脚本/meta；Prefab 的提示、球壳渐隐和 Circle 清理应继续保留。
