# DLevel_058 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame 058（`20058 / DLevel_058`）。
- 记录日期：2026-08-18。
- 需求：保留当前猫、钩爪、蜂巢和场景配置；猫和夹持角色先从画面外可见飞入，玩家完成第一笔有效画线后夹子张开，角色脱离动画父级并由 `Rigidbody2D` Dynamic 物理接管。
- 实现基线：`DLevel58ActorReleaseCtrl` 是 `DLevel2Ctrl` 的薄子类，复用 DLevel002 已有的可见 Intro、闭夹持有、首笔开夹释放和 Retry 完成态；没有复制第二套入场/夹子逻辑。
- 说明：随需求提供的截图只用于确认编辑器布局；截图内的辅助线、箭头和文字不是运行时配置。

## 2. Prefab 组件树

```text
DLevel_058
├─ DrawLevelCtrl
├─ DrawLineFeature
├─ DelayAutoBeginDrawHandle
├─ DrawLevelCountdownCtrl (10s, success)
├─ DLevel53Ctrl / DLevel53Hive             # 058 已有蜂群与失败流程
├─ DLevel58ActorReleaseCtrl : DLevel2Ctrl # 本关薄子类入口
└─ DrawGame_AlienClipBallRig (Alien)
   ├─ entranceStartPoint                   # 画面外入场起点
   └─ catch / BoneFollower (bone2)
      └─ clipSwingPivot
         ├─ clipVisual                     # clip_close / clip_open
         └─ Actor.prefab
            └─ Rigidbody2D                # 持有时 Kinematic，首笔后 Dynamic
```

## 3. 职责边界

| 组件 | 职责 | 不负责 |
| --- | --- | --- |
| `DrawLevelCtrl` | 扫描 Intro/Stroke Handler，顺序等待可见 Intro，分发有效笔画和 Attempt Reset | 直接播放猫/夹子动画或改写角色刚体 |
| `DLevel58ActorReleaseCtrl` | 为 D058 提供独立脚本身份，继承 `DLevel2Ctrl` 的可见 Intro、首笔释放和 Retry 契约 | 重写蜂群、结算、UI、存档或共享装置逻辑 |
| `DLevel2Ctrl` | 解析共享 Rig，绑定 `bone2`，驱动 START -> FINAL 入场和夹子惯性摆动；首笔回调中开夹、释放夹持刚体并播放猫 `control` | D058 的蜂群、胜负、倒计时和角色移动参数 |
| `DrawAlienClipBallRigView` | 暴露 Alien Spine、BoneFollower、`clipSwingPivot` 和夹子 Spine 引用 | 决定 D058 的入场/释放时机 |
| `DLevel53Ctrl` / `DLevel53Hive` | 继续使用 058 已有的蜂群生成、命中和失败流程 | 猫夹角色的可见 Intro 和首笔释放 |

## 4. 生命周期与调用链

1. 关卡实例化后，继承的 `DLevel2Ctrl.Awake()` 解析 Alien/Rig/夹子引用，将 BoneFollower 绑到猫 Spine 的 `bone2`，并同时缓存 `entranceStartPoint` 世界坐标、Prefab 摆放的 FINAL 坐标与 Actor 的持有位姿。
2. 初始恢复会播放夹子 `clip_close` 和猫 `idle01`，把 Alien 放到 `entranceStartPoint`，并令 Actor 根刚体 `simulated=false` + Kinematic，避免画面外入场时提前发生物理、FallArea 或蜂群交互。
3. 转场遮罩完全揭开后，`DrawLevelCtrl` 通过 `IDrawLevelVisibleIntroHandler.PlayDrawIntroAsync()` 调用继承逻辑。Alien 使用 `Ease.OutBack` 从 START 移到 FINAL，`clipSwingPivot` 同时执行衰减摆动；Intro 返回前画线输入保持关闭。
4. 入场结束后 Actor 仍保持 Kinematic，仅把 `simulated` 恢复为 `true`，继续挂在 `clipSwingPivot` 下等待画线。
5. 第一笔有效画线完成时，`DrawLineFeature -> DrawLevelCtrl -> IDrawStrokeCompletedHandler` 在同一回调中触发继承实现：夹子播放 `clip_open`，Actor 以保留世界位姿的方式脱离 `clipSwingPivot/catch`、清零速度并切为 Dynamic/WakeUp，猫播放一次 `control` 后恢复循环 `idle01`。
6. `_hasReleasedBall` 保证同一 Attempt 只释放一次；基类不施加额外力，Actor 后续运动由自身刚体、控制器与场景碰撞决定。
7. 失败、手动重试或 Replay 重建时，`IDrawLevelRetryIntroStateHandler.ApplyDrawRetryIntroState()` 直接应用 Intro 完成态：Alien 在 FINAL，夹子闭合，Actor 回到持有位姿并保持 Kinematic。Retry 不重播画面外飞入。

## 5. 序列化绑定

| 继承字段 | DLevel_058 fileID | 绑定/参数 |
| --- | ---: | --- |
| 根 `DLevel58ActorReleaseCtrl` | `900000000000000007` | 脚本 GUID `98e78cc6aaf249b183132cd4d46d4d2c`；运行时类继承 `DLevel2Ctrl` |
| `drawLevelCtrl` | `900000000000000001` | 同一 DLevel 根的 `DrawLevelCtrl` |
| `alien` | `4059550188848516894` | 共享 Alien Rig 根 Transform |
| `entranceDelay / entranceDuration / entranceBackOvershoot` | - | `0 / 0.78 / 1.9` |
| 夹子摆动参数 | - | `38, 0.18, 32, 0.25, 15, 0.14, 7, 0.11, 3, 0.09, 0.08` |
| `yellowBallRigidbody` | `1138963634036635986` | 字段名源于共享基类；D058 实际绑定 Actor Prefab 根 Rigidbody2D（源 fileID `2233608387543695698`） |
| `ballReleaseParent` | `8549164348132234925` | DLevel_058 根 Transform，脱离动画层级后的稳定父级 |
| `clipSwingPivot` | `3729883368532928849` | 共享 Rig 中的摆动/夹持 Transform |
| Spine/BoneFollower 字段 | `0` | 未显式跨 Prefab 绑定；由 `DLevel2Ctrl.ResolveReferences()` 在当前 Alien Rig 内解析 |
| Actor 初始刚体 | nested override | `m_BodyType: 1`（Kinematic）；不在 Prefab 中预置为 Dynamic |

## 6. 数据、资源和事件流

- 受影响文件：`DLevel58ActorReleaseCtrl.cs/.meta`、`DLevel_058.prefab` 以及 Level58 局部/共享记录文档。`DLevel2Ctrl.cs` 本身未修改。
- 配置：`tbdrawleveldata.json` 已有 `20058 -> DLevel_058`，本次不改 Excel、JSON 或 Luban 生成流。
- 入场流：`DrawLevelCtrl -> IDrawLevelVisibleIntroHandler -> DLevel2Ctrl.PlayDrawIntroAsync()`。
- 画线流：`DrawLineFeature -> DrawLevelCtrl -> IDrawStrokeCompletedHandler -> DLevel2Ctrl.OnDrawStrokeCompleted()`。
- 资源/UI/存档/Analytics/SDK/平台：无新增或修改。
- 蜂巢、蜂群、倒计时和已有场景物理参数本次保持不变。

## 7. 验证记录

- 已静态核对：`DLevel58ActorReleaseCtrl` 当前仅继承 `DLevel2Ctrl`；Prefab 根只挂这一个猫夹释放 Handler，没有与旧独立释放组件并存。
- 已静态核对：`alien`、Actor Rigidbody2D、释放父级和 `clipSwingPivot` 的本地 fileID 均存在；Actor 初态仍为 Kinematic；Rig 的 `entranceStartPoint` 仍保留画面外起点。
- 20055/20056/20057 的配置映射、排序、Prefab anchor、脚本 GUID 和本地引用静态复核结论不变：没有必须修正项。
- 当前 `DLevel58ActorReleaseCtrl : DLevel2Ctrl` 薄子类已使用 Unity 自带 Roslyn 与项目程序集做隔离编译，结果为 `CSC_EXIT=0`、无错误。该结果只证明当前 C# 类型可编译，不等价于 Unity Prefab 导入或 PlayMode 验证。
- `uncertain`：当前薄子类的 Unity 域重载、PlayMode 中猫的实际飞入轨迹、夹子开合、Actor 脱父级/Dynamic 物理、Retry 完成态、蜂群交互和 Android/iOS 横屏表现均未执行。

## 8. 回滚

回滚 `DLevel58ActorReleaseCtrl.cs/.meta` 和 `DLevel_058.prefab` 中 fileID `900000000000000007` 的继承字段绑定，可恢复修复前的“无可见飞入、不开夹，仅首笔将 Actor 切为 Dynamic”状态。回滚不需要修改 `DLevel2Ctrl`、共享 Rig、Actor Prefab、蜂巢、配置、UI 或存档。

## 9. 2026-08-18 角色落地防滑补充

- 原行为：Actor 根刚体继承 `Actor.physicsMaterial2D`，其摩擦力为 `0` 且使用 Minimum 合并；即使 `DLevel2Ctrl.ReleaseBall()` 已在切换 Dynamic 前清零线速度和角速度，角色落地后仍容易沿平面持续滑走。
- 修复：仅在 `DLevel_058.prefab` 的嵌套 Actor 根 Rigidbody2D（源 fileID `2233608387543695698`）覆盖 `m_Material` 为已有 `DrawStrokeStable.physicsMaterial2D`（GUID `96f0e9c7dbad4856bb70b847a68c2c55`）。该材质为摩擦 `0.8`、弹性 `0`、Maximum 摩擦合并。
- 边界：不修改共享 `Actor.prefab`、地面、Physics2D 矩阵、质量、重力、阻尼或约束；角色仍可被画线、蜜蜂和其它真实物理冲量水平推动。
- 静态验证：目标 Actor 材质 Override 恰好 `1` 条；当前 Prefab 有 `135` 个唯一 anchor、`0` 个重复 anchor、`0` 个悬空本地 fileID。
- `uncertain`：尚未在 Unity PlayMode 验证平地停止距离、斜面接触、画线/蜜蜂推动手感以及连续 Retry。
- 回滚：移除上述 Actor Rigidbody2D 的 `m_Material` Override，即恢复继承共享 Actor 零摩擦材质；无需回滚脚本或其它关卡。
