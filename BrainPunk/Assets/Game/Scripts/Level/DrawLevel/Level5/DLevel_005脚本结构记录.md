# DLevel_005 脚本结构记录

## 1. 基本信息

- 关卡：DrawGame 第 5 关
- 记录日期：2026-07-31（在 2026-07-28 初版上更新）
- 交付/验收状态：参数化物理发射、2.5 倍循环、射后 hold、8 段连续区域分层、
  箭头 Collider、渐显、箭间隔离、延迟命中定格、玩家真实接触点特效、无击退死亡、
  画线稳定摩擦和失败淡出保留箭已实现；
  独立 C# 编译通过，Unity PlayMode 与移动端尚未验收
- 参考基线关卡：DLevel_002（Draw 局部 Handler 与倒计时）、主线 Level 078（射箭动作）
- 选择基线理由：DLevel_002 已使用 `IDrawStrokeCompletedHandler` 和共享倒计时；
  用户明确要求参考 Level 78 的 Actor 拉弓射箭序列
- 基线证据限制：当前没有可确认“已验收”的同机制 DrawGame 结构记录，验收状态为
  `uncertain`；实现只复用已有局部契约，不上提共享抽象
- 主要脚本：`DLevel5Ctrl.cs`、`DLevel5ActorCtrl.cs`、
  `DLevel5ArrowImpactHandle.cs`、`DLevel5ArrowTrapHandle.cs`
- 关卡 prefab：`Assets/Game/Prefabs/DrawLevel/DLevel_005.prefab`

## 2. 脚本与组件树

```text
DLevel_005
├─ DrawLevelCtrl                         # 唯一主运行时控制器
├─ DrawLineFeature                       # 画线、运行时线段与墨水
├─ DelayAutoBeginDrawHandle              # 原有开场门禁
├─ DrawLevelCountdownCtrl                # 首笔后 10 秒，归零成功
├─ DLevel5Ctrl                           # 本关射箭循环控制器
├─ Actor                                 # Player，原 ActorCtrl
├─ DrawGame_DrawLine
│  ├─ Rigidbody2D + DrawStrokeStable      # 本关局部 0.8 摩擦、0 弹性
│  └─ OnTriggerTrapHandle                 # Collision2D 线段陷阱
└─ level5
   ├─ Enemy
   │  └─ DLevel5ActorCtrl                # 替换 Enemy 原 ActorCtrl
   └─ gongjian
      └─ arrow                           # 隐藏模板，Collision2D 箭矢陷阱
         ├─ DLevel5ArrowTrapHandle       # 玩家接触点特效、死亡、无击退失败
         └─ RuntimeArrow clone
            └─ DLevel5ArrowImpactHandle  # 运行时加入；绝对朝向、碰撞解析、命中定格
```

## 3. 类职责与边界

| 类/组件 | 类型 | 职责 | 继承/接口 | 创建与销毁方 | 不负责事项 |
| --- | --- | --- | --- | --- | --- |
| DrawLevelCtrl | 共享主控制器 | 初始化、输入门禁、Handler 分发、失败/成功和 Reset | `IDrawLevelRuntime` | DrawPlayCtrl | 不持有 Level5 射箭规则 |
| DrawLineFeature | 共享功能 | 生成有效线段、EdgeCollider2D 和笔画结果 | MonoBehaviour | DLevel prefab | 不判断 Level5 胜负或攻击 |
| DrawLevelCountdownCtrl | 共享可选组件 | 首笔后倒计时，归零提交 Success | `IDrawStrokeCompletedHandler` | DLevel prefab | 不驱动 Enemy 或箭 |
| DLevel5Ctrl | 关卡局部控制器 | 首笔启动、2.5 倍射箭节奏、8 段连续分层随机袋、真实 Impulse、刚体参数、生成箭、结算停止及按结果清理 | `IDrawStrokeCompletedHandler` | DLevel prefab | 不实现 Spine 动画细节，不直接提交胜利 |
| DLevel5ActorCtrl | 关卡 Actor 子类 | 拉弓/持弓/射箭序列、播放倍率、松弦和完成回调、射后 hold | `ActorCtrl` | Enemy prefab 实例 override | 不生成箭、不处理倒计时、trap 或存档 |
| DLevel5ArrowImpactHandle | 关卡局部运行时组件 | Sprite 渐显、速度绝对朝向、真实对方 Collider 解析、箭间隔离、下一物理步冻结并附着 | `MonoBehaviour` | DLevel5Ctrl 加入箭克隆；随箭清理 | 不触发胜负、不修改 Layer 矩阵、不处理 Player 伤害 |
| DLevel5ArrowTrapHandle | 关卡局部陷阱组件 | 箭命中 Player 的真实接触点粒子、死亡、速度归零、音效与失败事件 | `IEventSender` | 箭模板及其克隆 | 不施加击退、不处理线段或箭定格 |
| OnTriggerTrapHandle | 既有共享陷阱组件 | 线段的 Collision2D 玩家命中表现与事件 | `IEventSender` | 画线模板及其克隆 | 不处理 Level5 箭的玩家命中 |

## 4. 生命周期与关键调用

1. DLevel_005 创建时，DLevel5Ctrl 隐藏箭模板、缓存同关卡引用，并把 Enemy
   Rigidbody2D 与 Collider2D 关闭为纯演出对象。
2. DrawLevelCtrl 初始化时扫描 DLevel5Ctrl 和 DrawLevelCountdownCtrl 两个局部 Handler。
3. 第一笔有效线段完成后：
   - 倒计时组件开始 10 秒倒计时；
   - DLevel5Ctrl 开启攻击状态，并在 Update 中启动第一套射箭动作。
4. DLevel5ActorCtrl 以 `attackSpeedMultiplier=2.5` 依次播放
   `draw_pull`、`draw_hold`、`draw_shoot`。
5. `draw_shoot` 到 0.55 归一化时点回调 DLevel5Ctrl；后者从弓弦附近克隆 arrow，设置
   显式质量、重力倍率、Continuous/Interpolate/FreezeRotation；8 段分层索引每轮洗牌，
   每箭从当前未消费段内随机归一化位置，并让速度 `22.2~23.8` 与仰角 `65°~57°` 共用
   该位置连续插值，随后施加 `mass * sampledSpeed` 的等效 Impulse，不读取 Player 目标点。
6. 克隆箭运行时加入 DLevel5ArrowImpactHandle，以 0.06 秒渐显并与当前尝试内旧箭逐对
   忽略碰撞；飞行中以速度 `atan2` 设置绝对 Z 角。碰撞回调排除本箭层级，只在真正
   命中线、Player、地板等非箭 Collider 时标记命中，下一物理步再冻结、附着和关闭
   模拟，让本次接触先向动态画线传递动量；箭间偶发接触只恢复飞行。若命中 Player，
   DLevel5ArrowTrapHandle 同帧在真实接触点播放粒子、提交死亡链并清零玩家速度，不调用
   向上击退。
7. 动作完整结束后 Enemy 循环 `draw_hold`，等待
   `shootInterval / attackSpeedMultiplier`，再回到第 4 步。
8. Player 与线段发生 Collision2D 时走共享 trap 链；与箭发生 Collision2D 时走
   DLevel5ArrowTrapHandle 的无击退 trap 链。两者都按既有失败事件重建整关；Failed
   `Resolved` 只停射并使旧回调失效，现场箭覆盖 0.5 秒淡出窗口，旧关卡真正禁用时才清理。
9. 倒计时归零时提交 Success；DLevel5Ctrl 从 Resolved 回调停止动作并清理箭。
10. 显式 Reset、Disable、Destroy 都清理飞行与定格箭；整关重建恢复 prefab 初态，
    Failed 保留的箭不会跨到下一次关卡实例。

## 5. Prefab、序列化与资源绑定

| 宿主 | 字段/组件 | 绑定对象或资源 | 必填/可选 | 缺失时行为 |
| --- | --- | --- | --- | --- |
| DLevel_005 根 | `DLevel5Ctrl.drawLevelCtrl` | 同根 DrawLevelCtrl | 必填 | 首笔记录错误，不启动射箭 |
| DLevel5Ctrl | `enemyActor` | Enemy 上 DLevel5ActorCtrl | 必填 | 不启动射箭 |
| DLevel5Ctrl | `arrowTemplate` | level5/gongjian/arrow | 必填 | 不启动射箭 |
| DLevel5Ctrl | `shootInterval` | 0.05 秒 | 必填配置 | 最小按 0 处理，并除以播放倍率 |
| DLevel5Ctrl | `attackSpeedMultiplier` | 2.5 | 必填配置 | 最小按 0.01 处理 |
| DLevel5Ctrl | `launchOffset` | (-0.3, 0.3) | 可调配置 | 控制箭与弓弦的视觉衔接 |
| DLevel5Ctrl | `launchSpeed` | 23 | 可调配置 | 连续速度范围中点 |
| DLevel5Ctrl | `launchRangeSpeedBandOffset` | 0.8 | 可调配置 | 连续速度半宽，当前覆盖 22.2~23.8 |
| DLevel5Ctrl | `launchSpeedRandomOffset` | 0 | 可调配置 | 额外速度抖动；非零会削弱均匀性 |
| DLevel5Ctrl | `launchAngleDegrees` | 61° | 可调配置 | 连续仰角范围中点，代码限制在 -80° 至 80° |
| DLevel5Ctrl | `launchRangeAngleBandOffsetDegrees` | 4° | 可调配置 | 近端 65°、远端 57° |
| DLevel5Ctrl | `launchAngleRandomOffsetDegrees` | 0° | 可调配置 | 额外角度抖动；非零会削弱均匀性 |
| DLevel5Ctrl | `launchDistributionSegmentCount` | 8 | 可调配置 | 每轮每段各消费一次，运行时限制 2~16 |
| DLevel5Ctrl | `arrowGravityScale` | 4 | 可调配置 | 0 时不产生下坠弧线 |
| DLevel5Ctrl | `arrowMass` | 8 | 可调配置 | 最小按 0.001；影响动态画线受力 |
| DLevel5Ctrl | `arrowFadeInDuration` | 0.06 秒 | 可调配置 | 0 时立即显示 |
| Enemy | DLevel5ActorCtrl | ActorSpineCtrl、pushCollider、Actor 基础字段 | 必填 | 动画或基础状态不完整 |
| RuntimeArrow | Rigidbody2D、箭头 BoxCollider2D、SpriteRenderer、DLevel5ArrowTrapHandle、运行时 DLevel5ArrowImpactHandle | Player Actor 与 Knock 粒子 | 必填 | 缺刚体时本次箭立即销毁；无 SpriteRenderer 时跳过渐显；缺 trap 引用时无对应受击表现 |
| DrawGame_DrawLine | Rigidbody2D `m_Material` | `DrawStrokeStable.physicsMaterial2D`（friction 0.8、bounciness 0） | 本关配置 | 缺失时回到无显式稳定摩擦材质 |
| DrawGame_DrawLine | OnTriggerTrapHandle | Player Actor 与 Knock 粒子 | 必填 | 线段不会触发 trap |
| DrawLevelCountdownCtrl | `cdTime/isSuccess` | 10 / true | 必填 | 不符合本关坚持到时获胜规则 |

## 6. 数据、UI、资源与事件流

- DB/存档：无新增；成功继续由 DrawPlayCtrl 原有进度/星级链处理。
- Luban/配置：无新增或修改。
- UI：复用 Draw HUD 倒计时和胜利/失败流程。
- 局部引用：DLevel5Ctrl 只持有同 prefab 的主控制器、Enemy 和箭模板。
- 事件：不新增全局事件；trap 复用 OnTriggerTrapEvent，结算复用 Resolved。
- 资源加载：随 DLevel_005 prefab 同步引用既有 `DrawStrokeStable`，无新地址或运行时异步加载。
- Analytics/SDK：无新增或修改。

## 7. 相对参考基线的差异

| 差异 | 原因 | 玩家/运行时影响 | 验证范围 | 是否形成演进候选 |
| --- | --- | --- | --- | --- |
| 一个主控制器下并列倒计时与射箭 Handler | 倒计时和攻击是独立职责 | 首笔同时开始计时与攻击 | Handler 扫描、首笔门禁 | 继续沿用既有结构 |
| 新增 DLevel5ActorCtrl 替换 Enemy 原 ActorCtrl | 单关动画名和动作状态不得进入共享 ActorCtrl | Enemy 独立播放射箭序列 | prefab 替换、Skin 绑定、动画 | 局部必要差异 |
| 箭按 8 段连续分层随机袋和自身重力自然飞行 | 双档只覆盖两个窄簇，中间区域没有样本 | 每轮随机打乱全部分层，每段内再随机；速度与仰角共用位置，轨迹不追踪 Player | 发射动画衔接、区域均匀性、蓝线高度、命中率 | DLevel5 局部行为 |
| 箭碰撞盒只覆盖箭头，显式质量不随面积缩小 | 全身碰撞会由箭身/尾部提前命中，AutoMass 会让小盒质量过低 | 只有箭头扎到物体才命中，动态线获得更明显动量 | 细线命中率、Player trap、画线受力 | DLevel5 局部行为 |
| 动画倍率 2.5、间隔 0.05，射后停留 hold | 当前反馈射速仍慢 | 加快完整动作和动作间等待，不再出现每箭后的 idle | Spine 回调、松弦同步、循环重入 | DLevel5 局部行为 |
| 运行时箭以 0.06 秒 SpriteRenderer 渐显 | 弓弦附近直接出现过于突兀 | 箭在飞行开始时短暂淡入 | 首帧闪烁、暂停、Reset 清理 | DLevel5 局部行为 |
| 箭间逐对忽略；碰撞时排除自身 Collider，场景命中下一物理步才冻结 | 防止箭互撞/误判，并让当前 Dynamic 接触先传递动量 | 箭可穿过其他箭；画线先受力，箭随后扎住 | Physics2D 回调顺序、Player trap、清理 | DLevel5 局部行为 |
| 克隆使用单位旋转，以速度绝对角驱动并冻结物理旋转 | 模板 Y180 与增量角差会引入朝向反转/振荡，碰撞冲量也可能加角速度 | 飞行箭头稳定沿切线转向，命中后保持最后造型 | 上升/下降段朝向、命中角度、移动目标附着 | DLevel5 局部行为 |
| 箭使用局部 DLevel5ArrowTrapHandle | 共享 trap 固定把粒子放在 Actor 根节点并施加向上击退 | 粒子出现在箭头与身体接触处；Player 死亡但不跳起 | 身体各部位命中、连续碰撞、失败重建 | 单关表现，不修改共享陷阱 |
| 线模板改为 Collision2D trap | 运行时 EdgeCollider2D 非 Trigger | Actor 碰线触发失败 | 碰线、失败重建 | DLevel5 局部配置 |
| 线模板刚体绑定 DrawStrokeStable | 画线与场景/箭接触时需要稳定摩擦且不得改变其他关 | 本关运行时线使用 0.8 摩擦、0 弹性 | 斜线滑动、箭撞线、Player 碰线 | 复用既有资源，仅 DLevel5 Prefab override |
| Failed 结算停射但不立即清箭 | 无条件结算清理会在死亡淡出中抹掉命中现场 | 箭保留到旧关卡实例真正禁用，Success/Reset 仍按原边界清理 | 死亡演出、0.5 秒淡出、失败重开、快速退出 | DLevel5 局部生命周期 |
| Enemy 物理关闭 | 避免箭出生时撞射手 | Enemy 仅作射箭演出 | 站位、箭出生 | 不上提共享 Actor |

## 8. 可复用结论与优化候选

- 本关继续沿用的稳定结构：一个主 DrawLevelCtrl，多个互不依赖的局部 Handler。
- 本关验证的新结构：单关 Enemy 动画职责放在关卡 Actor 子类，关卡控制器只拥有攻击节奏
  与生成物生命周期。
- 暂不共享的局部实现：动画名、松弦时点、随机左向发射参数、箭头 Collider、渐显、
  8 段连续分层袋、箭间逐对隔离、延迟命中附着、玩家接触点无击退受伤、无定时销毁和
  Enemy 物理关闭。
- 后续关卡需继续观察的候选：不定时销毁的持续生成物应区分“结果已锁定”和“场景真正
  退场”的清理边界；死亡现场可保留到淡出，Reset/退出必须最终清理；
  是否形成共享能力需要多关证据和完整回归，不在本关上提。

## 9. 验证证据与缺口

- 静态检查：脚本 GUID、prefab 组件、Enemy Actor 替换、Skin 绑定、箭/线 trap 模式、
  倒计时和 Inspector 参数均已核对；Prefab 当前 149 个对象锚点无重复，且没有缺失
  内部 fileID 引用；目标 C#/Prefab/Markdown 的 `git diff --check` 通过。
- CodeGraph：识别新类继承、Handler、射箭回调和清理关系。
- 编译：Unity Editor 输出 `Finished compiling in 25s` 且未出现本次 C# 错误；命令行
  全依赖构建首次超时，随后 `BuildProjectReferences=false` 聚焦构建通过，
  `0 warning / 0 error`（5.58 秒）。箭改为左向且默认速度提高到 15 后再次聚焦构建，
  `0 error / 103 warning`（11.14 秒），未新增编译错误；Unity Editor 随后再次完成
  18 秒脚本编译，最近日志未出现 DLevel5/C#/YAML 错误。
- 改为 Rigidbody2D Impulse 且移除定时销毁后再次聚焦构建，结果为
  `0 error / 103 warning`（8.10 秒）；旧速度/寿命字段与带时限销毁调用静态扫描无残留，
  prefab 箭刚体仍为 Dynamic。
- 2026-07-31 此前目标点弹道版本的三个 Level5 脚本曾使用 Unity Bee 的完整 `Assembly-CSharp.rsp`
  和 Unity 自带 Roslyn 独立编译，结果 `0 error`；只有项目既有 warning。静态检查确认
  当时 prefab 的目标弹道参数均已序列化；代码没有
  调用 `Physics2D.IgnoreLayerCollision`，箭间隔离只作用于成对 Collider。运行中的
  Unity 也已重新生成 Assembly-CSharp.dll，最新编译/程序集重载区段没有 Level5 或
  `error CS` 诊断。
- BadBox 出生修复后 Unity 再次生成 Assembly-CSharp.dll；静态几何与调用顺序确认
  模板约 `(6.387,-1.95)` 经 `launchOffset=(-1,1)` 得到运行时起点
  `(5.387,-0.95)`，并从该位置反算弹道。Prefab 145 个 YAML 对象锚点保持唯一。
- 高度、悬空与朝向修复后 Unity 于 17:23:49 再次生成 Assembly-CSharp.dll，且最新
  编译/重载区段未出现新增 `error CS`。静态断言确认高度 `2.5`、单位旋转、
  `FreezeRotation`、碰撞双方 Collider 解析与自身层级排除、箭间恢复飞行，以及
  不再使用 `transform.right / DeltaAngle` 的绝对速度角实现。
- 此前参数化物理发射版本再次使用同一 Bee 响应文件和 Unity Roslyn 完整独立编译，结果
  `0 error`，输出只有工程既有 warning。静态检查确认目标点字段/计算无残留；Prefab
  序列化速度 18、仰角 20°、重力倍率 1.5、质量 8、渐显 0.06 秒、动画倍率 2.25、
  间隔 0.2 秒；箭头 Collider、显式质量、Continuous、Interpolate 和 FreezeRotation
  均存在，场景冻结位于 `WaitForFixedUpdate` 之后。运行中的 Unity 于 17:50:47
  重新生成 Assembly-CSharp.dll，最近日志未出现 `error CS`。
- 随机弧线调整后完整 Bee/Roslyn 独立编译再次通过，结果 `0 error`；运行中的 Unity 于
  18:00:32 重新生成 Assembly-CSharp.dll，最近日志无 `error CS`。静态检查确认速度与
  仰角各自独立 `Random.Range`，Prefab 序列化随机幅度 `1` 与 `3°`，目标点代码无残留。
- 前后双档随机袋初版调整后，Unity Bee 于 `18:21:57` 再次生成
  `Library/ScriptAssemblies/Assembly-CSharp.dll`，时间晚于 Level5 脚本；最近日志
  `error CS=0` 且无编译失败标记。静态检查确认随机袋、80° 角度上限、双档字段、模板
  重力和 145 个唯一 YAML 锚点均存在。
- 最终蓝线参数使用 Unity Bee 完整响应文件与 Unity DotNetSdkRoslyn 独立编译，结果
  `exit 0 / 0 error / 104 warning`；静态几何估算短程档落程 `9.57~10.76`、长程档
  `12.25~13.45`，顶点相对发射点抬升 `4.64~5.20`（相对地面总高 `5.70~6.26`）。
  静态扫描没有 Player/target/目的地反算残留，`git diff --check` 通过。运行中的 Unity
  Editor 正在连续导入其他关卡资源，因此最终参数对应的 Editor 自动刷新时间不作为
  本轮编译证据。
- 新增 DLevel5ArrowTrapHandle 后，当前 Unity Bee 响应文件已包含该源文件，完整 Unity
  Roslyn 独立编译为 `exit 0 / 0 error / 104 warning`。静态检查确认 contact point、
  ClosestPoint 回退、无 Knockback、玩家线/角速度归零，以及箭/画线局部与共享 handler
  各一处绑定；Prefab 当前 149 个对象锚点唯一且内部 fileID 引用完整。
- 8 段连续分层修改后，同一 Unity Bee/Roslyn 完整编译再次通过：
  `exit 0 / 0 error / 104 warning`。当前 Prefab 的速度 `22.2~23.8`、仰角 `65°~57°`、
  重力倍率 4 在平地静态计算中产生 `10.074~13.817` 连续落程；8 个边界相邻间距仅为
  `0.454~0.476`，顶点抬升为 `5.077~5.167`。代码静态检查确认每轮填入全部分层、
  Fisher-Yates 洗牌、逐段消费，并在首笔/Reset/结算时清空袋。
- 本次摩擦与死亡现场保留修改后再次独立编译完整 Assembly-CSharp，结果为
  `exit 0 / 0 error / 104 warning`；静态检查确认 `DrawStrokeStable` GUID 与本关 override
  一致、Prefab 149 个锚点唯一且无缺失内部引用，Failed 条件清理以及 Reset/Disable
  最终清理入口均存在。目标 `git diff --check` 通过，仅有换行转换提示。
- Unity PlayMode：未执行。
- Android/iOS：未执行。
- 未验证项：速度 `22.2~23.8 / 65°~57°` 的 8 段连续分层自然弧线、2.5 倍动作与松弦同步、
  实际连续射速、射后 hold、
  0.06 秒渐显、箭头命中细线、画线受力、箭间穿透且不悬空、飞行朝向稳定、
  碰线/Player/地板后延迟定格、Player 身体接触点特效与不跳起、线摩擦手感、
  死亡淡出期间现场箭可见、暂停、失败重开、
  10 秒胜利、Replay、移动端物理与性能。

## 10. 风险与回滚

- 主要风险：8 段连续速度/仰角与重力倍率 4 的蓝线高度、区域落点手感和
  命中率待 PlayMode 确认；尖端小 Collider 对各种角度细线的命中可靠性、箭质量 8 对质量 `20~80`
  画线的推动幅度，以及命中等待一物理步是否出现轻微反弹也待确认。定格箭仍保留
  可见对象，射速提高后 10 秒内的累计对象和移动端渲染开销增加。
- 回滚路径：移除 DLevel5Ctrl；恢复 Enemy 原 ActorCtrl 与 Skin 绑定；将画线模板 trap
  恢复 Trigger 模式；删除四个 Level5 本地脚本。若只回滚本次优化，则删除
  DLevel5ArrowImpactHandle，并把 DLevel5Ctrl/DLevel5ActorCtrl 恢复为旧发射、1 倍动画、
  原间隔和射后 idle；若只回滚本轮，则恢复目标点弹道字段/计算、全身 Collider、
  AutoMass 和立即定格，并移除渐显。若只回滚玩家命中修正，则把箭模板重新绑定到共享
  OnTriggerTrapHandle 并删除 DLevel5ArrowTrapHandle。若只回滚连续区域分层，则恢复
  两档消费方法、删除 launchDistributionSegmentCount，并把速度/角度范围配置恢复为
  `0.5 / 0.3 / 3 / 1`。若只回滚本次变更，移除画线模板 Rigidbody2D 的
  `DrawStrokeStable` override，并把 `OnLevelResolved` 恢复为无条件清箭。共享 Draw
  代码、物理材质资源本体、倒计时和存档不需要回滚。
