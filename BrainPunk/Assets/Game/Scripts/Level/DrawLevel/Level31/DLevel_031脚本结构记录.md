# DLevel_031 脚本结构记录

## 1. 关卡目标

- 共享猫夹球装置从左侧场外飞入，到位后自动开夹放球并退场。
- 球落地演出结束后才开放画线。
- 玩家围住球画出带长尾的曲线，依靠线自身重量、摩擦和刚体旋转把球撬向右上方成功门。
- 玩法只使用 Physics2D 实际受力，不增加脚本冲量、固定关节或自定义成功条件。

## 2. 当前代码归属

DLevel031 没有新增运行时 C# 类，使用现有共享边界：

| 文件/组件 | 职责 |
|---|---|
| `Assets/Game/Prefabs/DrawLevel/DLevel_031.prefab` | 本关层级、显式引用、球和画线物理参数、门与失败边界 |
| `Assets/Game/Scripts/Level/DrawLevel/Level9/DLevel10Ctrl.cs` | 配置方向入场、夹持、自动放球、原路退场、落地双弹和 Retry 最终态 |
| `DrawAlienClipBallRigView` | 解析共享 Alien、夹子、球、Spine、BoneFollower 和入场轨迹内部引用 |
| `DrawLevelCtrl` | 串行等待可见 Intro、画线门禁、成功提交和球壳同步渐隐 |
| `DrawLineFeature` | 生成运行时线、构建碰撞链、按长度写入质量并启用 Dynamic 模拟 |
| `DrawLevelCountdownCtrl` | 第一笔有效线后启动 20 秒失败倒计时 |

`DLevel10Ctrl` 虽保留历史类名和 Level9 路径，但其入场方向本来就支持 `FromLeft / FromRight / FromTop`；
DLevel025 已有 FromLeft 复用先例。本次没有复制或派生另一套入场控制器。

## 3. Prefab 根组件

`DLevel_031` 根节点当前持有：

1. `DrawLevelCtrl`
2. `DrawLineFeature`
3. `DrawLevelCountdownCtrl`
4. `DLevel10Ctrl`

原先误用的 `DLevel27Ctrl : DLevel2Ctrl` 已被替换，不能与 `DLevel10Ctrl` 同时保留。两者都实现猫夹球 Intro，
同时挂载会被 `DrawLevelCtrl` 顺序执行并争用同一个球。

## 4. 入场与放球时序

```text
关卡可见
  -> DrawLevelCtrl 关闭画线输入
  -> DLevel10Ctrl 恢复闭夹、持球 Kinematic 初态
  -> Rig 按路径距离从左侧场外飞到 (-5.42, 0.95)
  -> 播放 clip_open
  -> 球保持世界姿态改挂到 DLevel_031 根并切 Dynamic
  -> 猫与夹子沿原路退场
  -> 球检测到稳定落地后临时 Kinematic 播放两次弹跳
  -> 球恢复 Dynamic
  -> 退场和落球演出均结束
  -> DrawLevelCtrl 开放画线
```

关键序列化配置：

| 字段 | 当前值 | 说明 |
|---|---:|---|
| `rig` | 本关共享 Rig 的显式 stripped 引用 | 不依赖旧 `level10/Alien` 层级回退 |
| `ballReleaseParent` | `DLevel_031` 根 Transform | 退场时球不再跟随猫 |
| `entranceState` | `FromLeft (0)` | 沿世界 X 负方向的场外起点进入 |
| `entranceDuration` | `0.78s` | 保留本关既有横向入场节奏 |
| `exitDuration` | `0.8s` | 沿原路径退场 |
| `landingTimeout` | `8s` | 落地异常时仍解除 Intro 门禁 |
| `retryLandingSearchDistance` | `20` | Retry 向下寻找本关 Static 实体落点 |
| `firstBounceHeight / Duration` | `0.55 / 0.34s` | 落地双弹第一段参数 |

Attempt Reset/Replay 不重播完整飞入：控制器直接应用开夹放球最终态，把球放到下方最近的 Static 实体表面，
清零速度并让猫保持在场外。

## 5. 球体物理

共享复合球的外壳仍是唯一 Rigidbody2D 和 CircleCollider2D；内部 Actor 的身份供成功门和 FallArea 回认，
但不再有第二套实体刚体。DLevel031 局部覆盖：

| 参数 | 当前值 |
|---|---:|
| Mass | `1` |
| Gravity Scale | `1.8` |
| Linear Damping | `0.04` |
| Angular Damping | 继承共享值 `0.5` |
| Interpolate | `Interpolate` |
| Collision Detection | 继承 `Continuous` |
| Physics Material | 无高弹材质 |

当前 Prefab 的球质量为 `1`，较复制基线 `0.5` 略有提高；这是本次回读到的现值，本轮摩擦调整没有修改球
质量。重力和低线性阻尼让球保持下落节奏，同时仍可被长线杯口撬起。

## 6. 画线物理

| 参数 | 当前值 |
|---|---:|
| 视觉宽度 | `0.2` |
| Collider Radius | `0.1` |
| Max Ink Length | `50` |
| Stroke Mass | `100` |
| Scale Mass By Length | `true` |
| Minimum Mass Ratio | `0.25` |
| Gravity Scale | `2.5` |
| Linear Damping | 继承模板值 `0` |
| Angular Damping | `0.2` |
| Interpolate | `Interpolate` |
| Collision Detection | 继承 `Continuous` |
| Physics Material | `DLevel31Grip`：friction `1`、bounce `0`、Maximum Combine |
| 多笔/笔间碰撞 | 开启多笔、关闭笔间碰撞 |

绘制期间运行时线刚体保持 `simulated=false`；有效笔画完成后，`DrawLineFeature` 按当前线长相对 50 的比例
写入最终质量，再开启 Dynamic 模拟。模板上的重力、阻尼、插值和材质保留，因此长尾会真实下落和旋转，
本关最大摩擦杯口能更稳定地抓住球与地面支点。该材质只绑定 DLevel031 的画线模板实例，球与其它关卡
继续使用原材质，避免同时增加球对地面的滚动阻力。

## 7. 成功、失败与配置

- 唯一启用的高处成功门位于约 `(2.46, 2.45)`，继续通过标准 Trigger 调用
  `DrawLevelCtrl.OnTriggerSuccess`。
- `successExtraRendererGroup` 显式绑定球壳 `SpriteRendererGroupAlpha`，成功时球壳与内部 Actor 同步渐隐。
- 失败边界保留 Top、Bottom、Right 三个共享 FallArea；左侧长地面覆盖到约 `x=-27.4`，当前未增加 Left。
- 倒计时为 20 秒，第一笔有效画线完成后启动。
- `tbdrawleveldata.json`、`tbdrawlevelsort.json` 和 Excel 源表当前没有 DLevel031 条目。正式入口顺序、墨水上限
  与星级阈值未由本次玩法需求确定，因此未推断写入。

## 8. 共享脚本更正

`DLevel10Ctrl.ValidateReferences()` 旧逻辑会在合法的非 FromTop 配置下记录 Error，但仍继续运行；这与已有
DLevel025 FromLeft 配置冲突。本次删除该错误诊断，并把类注释、方法注释和 Inspector 标题更正为配置方向。
方向计算、Tween、放球、落地、退场和 Retry 算法均未改变。

## 9. 验证记录

- DLevel031 Prefab：147 个 YAML anchor，全部唯一。
- 147 个唯一非零本地 fileID 全部可解析，无缺失引用。
- 根节点 DLevel10Ctrl 脚本 GUID 1 处、旧 DLevel27Ctrl GUID 0 处。
- Unity 6000.0.59f2 已成功使用 PrefabImporter 导入 DLevel031；导入后的日志段无编译错误、断言或 Prefab 错误。
- 使用当前 Bee `Assembly-CSharp.rsp` 和 Unity 自带 Roslyn 独立全量编译，退出码 0、`0 error`，仅有工程既有 warning。
- 尚未执行完整 PlayMode 和移动端真机验证。

## 10. 2026-08-04 夹球摩擦跟进

- 试玩前反馈担心 `DrawStrokeStable(friction=0.8)` 的杯口仍会让球滑出。
- 新增本关独立 `Assets/Game/ArtSrc/Physics/DLevel31Grip.physicsMaterial2D`，使用 friction `1`、
  bounce `0`、Friction Combine `Maximum`、Bounce Combine `Minimum`。
- DLevel031 只把画线模板 Rigidbody2D 的 `m_Material` override 改为该材质；没有修改共享
  `DrawStrokeStable`、共享画线 Prefab、球材质或 Physics2D Layer Matrix。
- 本次修改后 YAML 复核为 147 个唯一 anchor、147 个唯一非零本地引用且 0 缺失；Unity 已分别用
  NativeFormatImporter/PrefabImporter 成功导入材质与 DLevel031，后续日志未出现目标错误。真实夹持、
  地面卡滞和撬升效率仍以 PlayMode 为准。
