# DLevel_085 脚本结构记录

当前实现已按文末“新线出生保护与有界弹射结构修正”更新，早期材质和纯限速描述为历史基线。

当前版本已接入顶部尖刺和 D084/D085 共用的局部球限速器；见相应后续章节。首次接入时的无尖刺、无限速检查保留为历史基线。

## 基线与本轮范围

- 2026-09-14，关卡 `20085 / DLevel_085 / 弹性的线2`。用户确认仍为纯物理关，没有需要新增的机关，其它部分不用扩展。
- 以用户当前 D085 Prefab 为布局和参数基线；复用 D084 当前“球碰线弹射、线与地面稳定”的成对材质，以及 D002 的猫夹球控制器。本次没有新增运行时脚本。
- D084 文档中的独立物理测试是材质方案的历史证据，不能当作 D085 整关验收。本轮只完成资源、绑定、复制残留与配置静态核验。

## 组件树与职责

```text
DLevel_085
├─ DrawLevelCtrl                    尝试状态、输入、结果锁、成功/失败与 Reset
├─ DrawLineFeature                  多笔物理画线、墨水、笔画完成分发
├─ DrawLevelCountdownCtrl           首笔后 20 秒超时失败
├─ DLevel2Ctrl                      单套猫夹球可见入场、首笔放球、Retry 到位
├─ DrawGame_AlienClipBallRig
│  ├─ DrawEntrancePathView / DrawAlienClipBallRigView
│  ├─ Spine / BoneFollower / clipSwingPivot
│  └─ yellow_ball                   Rigidbody2D、CircleCollider2D、DLevel84BallSpeedLimiter、Actor
├─ DrawGame_SuccessDoor             启用的目标门与成功表现
├─ DrawGame_SuccessDoor_DisabledLegacy（停用）
├─ Ground_mid / Ground_long         静态地面 Collider
├─ StoneB (1)                       静态竖柱 Collider 与五块拼接图片
├─ 四周 FallArea                    既有失败链
└─ Tips、提示线、背景、装饰与相机
```

组件由关卡实例持有。竖柱没有 Rigidbody2D，属于正常静态障碍；它不需要机关脚本。物理材质只参与接触求解，没有碰撞回调主动写球速度。

## 生命周期、接口与清理

1. DrawPlayCtrl 加载 Prefab，建立 HUD、关卡数据及尝试上下文；DrawLevelCtrl 初始化输入与结果状态。本关 `isConfig=0`，实际使用 Prefab 墨水参数，两表已填相同数值。
2. DLevel2Ctrl 在运行时 Awake 缓存初态，通过既有 `IDrawLevelVisibleIntroHandler` 等待可见 Intro 完成后开放画线。球在夹持阶段保持 Kinematic。
3. 首笔有效线完成后，经 `IDrawStrokeCompletedHandler` 释放球并启动倒计时；球脱离动画父级、切为 Dynamic。后续笔画不重复放球。
4. 球与线/地面/竖柱继续由 Physics2D 求解，球上的局部限速器仅截断超速，窄框按活动间距进一步降速。有效门的 `OnTrigger2DHandle.onEnter` 调用当前 DrawLevelCtrl.OnTriggerSuccess，沿用 Actor 判定、结果锁与同门成功表现。
5. 超时和 FallArea 沿用通用失败链。暂停、成功结算、提示、广告、存档、Analytics 和 HUD 事件归属均保持现行 Draw 流程。
6. Reset/Replay 沿用黑幕下销毁并重建关卡与 HUD；新实例经 PlayResetIntroAsync / ApplyDrawRetryIntroState 直接到位。旧 D002 组件禁用时清理 Intro Tween、使旧回调失效，不额外增加重复复位逻辑。

## 显式绑定与参数

| 宿主/字段 | 当前引用或数值 | 核验结论 |
| --- | --- | --- |
| 根 DrawLevelCtrl | `900000000000000001` | 门、放球组件与画线均指向本关 |
| drawFeature | `900000000000000002` | 本关 DrawLineFeature |
| successDoorSpine | `7594331605050263445` | 当前启用门中的 Spine，非停用旧门 |
| successExtraRendererGroup | `3091325050892645083` | 本关成功表现组 |
| 有效门 onEnter | 根 DrawLevelCtrl / OnTriggerSuccess | target 与方法完整，门启用 |
| DLevel2Ctrl.alien | `1255546920056987011` | 现有猫夹球实例 |
| yellowBallRigidbody / ballReleaseParent | `6262018380601849612` / `8549164348132234925` | 球刚体与本关释放父级 |
| alienSpineCtrl / alienSkeletonAnimation | `6833647044240291983` / `1326194386933571420` | 现有 Spine 对象 |
| clipBoneFollower / clipSwingPivot / clipSkeletonAnimation | `6907488015446782417` / `1943803583873973196` / `2849637529966163699` | 夹持和开夹引用完整 |
| 画线 | 墨水 50；星级 0/10/20；质量 32、最小比例 0.25 | 多笔开启；质量约 8～32 |
| 球 | 质量 1、重力倍率 1.5、线性阻尼 0.03、角阻尼 0.05、Continuous | 本关降低角阻尼以保留自然旋转 |
| 球限速组件 | `DLevel84BallSpeedLimiter`，ballRigidbody=`6262018380601849612`，maxLinearSpeed=30 | 球对象的局部 added component；窄框按间距降速，不改角速度 |
| 成对材质 | 当前线/球/斜坡弹性 2/0.4/0；球 Maximum，线/地 Multiply | 球地适度回弹、球线高弹射、线地稳定；本关球摩擦 0.25/Minimum |
| 新长地面与竖柱 | 显式 StableGround，弹性 0、Multiply | 根据真实接触测试补齐；原“无材质等同零弹性”判断已撤回 |
| 新增斜平台 StoneB (2) | BoxCollider2D `6676497914815354429` 显式 StableGround | 与地面同材质，原角度、尺寸、偏移与非 Trigger 状态保持 |
| 倒计时 | 20 秒、isSuccess=false | 首笔启动，进门成功或超时失败 |

所有绑定均沿用用户 Prefab，未添加自动查找兜底。竖柱为 Layer 13 的启用非 Trigger BoxCollider2D，长地面也为启用静态 Collider；项目物理层配置允许其与球接触。

## 数据、资源与标题

- `LevelData.xlsx/draw_level` 第 65 行：`20085 / DLevel_085 / 弹性的线2 / 1.14.0 / 0 / 1 / 50 / 0 / 10 / 20`。按 id 顺序位于 20084 与 20999 之间，TBC 移至第 66 行。
- `LevelSort.xlsx/drawLevelSort` 第 65 行：`60 / 20085 / 弹性的线2`，当前末项，方便测试。源表被 WPS 打开，已在原窗口写入并保存。
- Luban 4.5 对当前配置快照生成 JSON；确认 `tbdrawleveldata.json` 和 `tbdrawlevelsort.json` 各仅新增本关一条后同步到 Assets/Game/Configs。
- Tips 从 `S_DrawLevel_Tips_Level_84` 改绑 `S_DrawLevel_Tips_Level_85`，编辑态文字同步为“弹性的线2”。I2Languages 仅追加本关词条，沿用既有 Draw 惯例填写前 12 种语言，其余语言槽为空；旧词条逐字节保留。没有同步外部语言源。
- Prefab 名称与配置路径一致，资源由既有 Draw 加载链读取。无新增全局事件、存档字段、SDK 或 UI 协议。

## 相对基线差异与结构复盘

- 本轮 Prefab 只改 Tips 实例的名称文字和词条；原物理参数、竖柱、长地面、斜坡、门、猫、提示线及其它布局全部保持原样。
- 从 D084 复制来的放球、门、失败绑定已完整，不需要再换控制器。已修复的问题是旧标题与缺失的配置入口。
- 同一纯物理玩法可以复用现有控制器和参数。静态障碍无 Rigidbody2D 是合法结构，不应因此添加空控制器或复位封装。
- D085 当前线和地面复用 Level84 材质，球已使用 Level85 独立材质；单关调球继续修改本关资源。用户此次明确点名两关的球地弹性，分别调整各自球材质。

## 验证、剩余风险与回滚

- Prefab 125 个唯一 YAML 对象、125 个唯一非零本地引用，无缺失；脚本、嵌套 Prefab 与物理材质引用可解析。相对修改前备份只改变 Tips 实例。
- 两表回读确认原有表值保留，LevelData 编号升序、Sort 末项 60/20085；两份 Draw JSON 去掉本关新增项后与修改前完全一致。
- 表格编辑前后完成局部渲染与错误扫描；LevelData 原行样式保留，Sort 在 WPS 中实际显示和保存已核对。Luban 数据校验与生成成功。
- `uncertain`：完整进门路线、复制的提示画法、薄线/竖柱边缘高速碰撞、20 秒手感、暂停、连续 Reset/Replay 及移动端。没有改运行时 C#，本轮没有重复编译或执行整关 PlayMode 验收。
- 回滚仅恢复 D085 Tips、移除本关 I2 词条和两表 20085 条目并重新生成对应 JSON；保留用户布局、D084、共享资源与其它工作区改动。备份和验证产物位于 `E:/WorkSpace/outputs/dlevel85-20260914/`。

## 2026-09-14 顶部尖刺补充

### 需求、参考与改动

- 用户新增 `SpikeStrip_00` 并要求命中后与前面尖刺关相同地出现效果、死亡和重来。刺原有 Trigger 回调为 `TrapEventHandlle.PlayKnock`，但 target 为 0，不能执行。
- 参考 D003/D032 的 Knock_Explosion 裁剪实例与 trap 音效，以及 D061 的 `TryBeginFailurePerformance → 死亡演出 → CompleteFailurePerformance` 时序。本次仅新增本关 `DLevel85SpikeTrapHandle.cs/.meta`，更新 D085 Prefab；不改共享组件、既有关卡或已完成的表格。
- 用户刺的图片、位置 `(7.28, 6.12)`、旋转 -180°、宽度 66.46、高度 0.48、Layer 13 和 Trigger 保持原样。清空复制的 Tag 过滤后，以显式球刚体身份过滤接触，画线不会触发死亡。

### 组件、绑定与调用

```text
DLevel_085
├─ DLevel85SpikeTrapHandle（新增）
├─ SpikeStrip_00 / OnTrigger2DHandle
│  └─ OnEnter → DLevel85SpikeTrapHandle.OnSpikeTriggered
└─ DLevel085_Knock_Explosion（新增裁剪粒子实例）
```

| 字段/事件 | 显式绑定 |
| --- | --- |
| Handler | 根组件 `850000000000000001`，脚本 GUID `98914b9b95184ab99d506874c9370f18` |
| drawLevelCtrl | `900000000000000001` |
| playerBall | `6262018380601849612`，实际 yellow_ball 刚体 |
| spikeCollider | `2235164531252919237`，用户新增尖刺 BoxCollider2D |
| knockEffect | `850000000000000004`，本关粒子实例 |
| 尖刺 onEnter | target `850000000000000001` / `OnSpikeTriggered(Collider2D)`，运行时调用 |
| failDelay / knockbackSpeed | 0.5 秒 / 10 |

1. 触发器只负责转发 Collider；Handler 只接受 `collider.attachedRigidbody == playerBall`。当前玩家来自已绑定 DrawLevelCtrl 的既有 Actor API，不新增层级查找或动态订阅。
2. 只有未死亡、未被其它结果锁定的玩家可以占用失败演出。先锁输入与其它结算，再在刺的最近接触点播放 Knock，调用 Actor.Die 和 trap 音效。
3. Rig 已移除球内 Actor 的 Rigidbody2D，不能调用会直接访问该刚体的 Actor.Knockback。本关仅在致死接触后将球速度设为刺尖方向 × 10：当前刺局部 up 朝世界下方，让球受击退开。正常球/线弹性不变。
4. Handler.Update 累计 0.5 秒非缩放时间后只提交一次 CompleteFailurePerformance。通用 Tick 不会抢先处理 Actor.IsDied，倒计时在演出占用期间暂停。通过既有 DrawGameResetEvent 失败链重建并沿用当前 revive/打点约定。
5. 组件实现既有 IDrawStrokeCompletedHandler，首笔回调不做额外操作；Attempt Reset 和 Disable 只取消本组件持有的未完成演出并停止粒子。没有协程/异步延迟或遗留回调，也不对旧关做额外复位。

### 粒子资源与结构复盘

- 粒子来自 `Assets/Game/ArtSrc/Particles/Prefabs/Knock_Explosion.prefab`（GUID `972fa0cfdd6f5e04a8c9fde50e1bb924`），与 D003/D032 一致移除源子对象 `5393762992232218095`，保留 ParticleSystem `462519149007686675`。源粒子 playOnAwake=0，避免进关误播；Sorting Order 210。
- 新增实例 `850000000000000002`、Transform `850000000000000003` 归本关根持有，随整关销毁。保留前关粒子的 z=-8.189852 和其它样式参数，只在命中时改变世界 x/y。
- 复盘结论：单个明确的刺使用 Inspector 持久化事件绑定即可，无需复制 D061 的运行时 TriggerItem 全层级扫描。复用现有失败演出接口，球壳身份和球刚体击退保持 D085 局部，避免旧关承担变化。

### 验证与回滚边界

- Unity 自带 Roslyn 对当前完整 Assembly-CSharp 响应文件编译，输出到隔离目录；0 error / 113 warning，没有覆盖运行中的 Editor 程序集。
- Prefab 共 134 个唯一对象、134 个非零本地引用，无缺失。相对用户新增刺后的备份，仅修改根组件列表、根子对象列表和该刺事件三个原有块，新增 Handler 和粒子实例/引用四个块；刺外观、碰撞体和原场景内容逐块保持不变。
- 定向隔离检查使用真实 Unity Physics2D、OnTrigger2DHandle 和本关 Handler；DrawLevelCtrl/Actor/Audio 边界采用测试替身，只验证调用契约，不代表完整游戏的死亡动画、粒子贴图、重开加载、存档/打点或移动端验收。结果见同目录风险记录。
- 回滚本次刺接入时恢复 `E:/WorkSpace/outputs/dlevel85-spike-20260914/DLevel_085.before.prefab` 并移除新增脚本及 .meta；该备份保留用户新增的刺和已接入的 20085 标题。两表、D084 和共享资源不需要回滚。

## 2026-09-14 长地面与竖柱材质补齐

- 用户反馈线落地过弹。两关线与球材质相同，D084 地面显式绑定 StableGround，D085 新增 Ground_long 和竖柱却为无材质；此前将无材质当作显式零弹性的静态判断错误，已更正。
- 真实 Unity 6000.0.59f2 对照：当前线弹性 2，通过根刚体赋材质、子 BoxCollider2D 接触无刚体地面，入射速度 -8.633；无材质地面出射 +17.658，而显式 StableGround 出射 0。球落 StableGround 为 0；球撞弹性线支撑从 -9.418 到 +19.424，保留弹射。
- 只在本关 Ground_long 实例 `6574338486681126746` 的源 BoxCollider2D `6600411729220565881` 增加材质 override，并给竖柱 BoxCollider2D `3583285206964772767` 绑定同一 StableGround GUID `fdfb396140f74c76bdcfa189d2c39a48`。
- 修改时保留了工作区最新保存的 Prefab，其对象数已为 153；修改后仍为 153 个对象及 153 个非零本地引用，仅上述两个块改变。线/球材质、线弹性 2、D084、尖刺死亡脚本、最新布局与两表保持原样。
- 接触测试不替代完整解法和移动端验收。证据目录 `E:/WorkSpace/outputs/dlevel85-ground-20260914/` 中，`DLevel_085.latest-before.prefab` 为实际动手前的最新副本，`ground-physics-result.txt` 为物理结果。回滚仅撤销这两处材质绑定，避免覆盖后续场景修改。

## 2026-09-14 球滚动与旋转保持

- 根据试玩反馈，将 D085 球从低摩擦滑动调整为更容易靠接触形成滚动。旋转本来未锁定，D002 也只在初态/释放时清零转速；本次不改控制器。
- 新增本关球材质 `DLevel085_RollingBall.physicsMaterial2D`（GUID `37d5a56cb72e42adaadafbef58976482`），从 D084 球材质复制后只改名称和摩擦 `0.05 → 0.25`；弹性 1、Multiply、摩擦 Minimum 保持。Rig 实例 `3168915018210331464` 的球源刚体 `9015388286338645060` 改绑此材质并追加 `m_AngularDamping=0.05`，原继承值为 0.5。
- 真实物理验证中，零初始转速的球在 -25° 稳定斜坡接触 1 秒后，滚动比由 0.180 提高到 1.003，转速幅度由 96.0 提高到 397.8 度/秒；下坡速度有所降低，这是滑动转为滚动的轨迹变化。无接触保转、稳定地面不反弹、弹性线发射对照均通过。
- 153 个对象与引用完整，只改一个 Rig override 块；D084、共享材质、稳定地面和尖刺保持不变。没有新增脚本、每帧转速赋值或初始旋转，正常转动继续由 Physics2D 产生。
- 当前只剩线与稳定地面复用 Level84 材质，球已独立。后续调滚动手感应优先修改本关球材质/角阻尼，保持此前落地稳定与弹射的接触验证；完整解法与移动端仍需试玩。
- 完整数据与回滚见本关风险记录“滚动调整结果与证据”，证据目录 `E:/WorkSpace/outputs/dlevel85-roll-20260914/`。

## 2026-09-14 球地适度回弹，保留球线弹射

- 用户明确点名 D084、D085 均应有正常的球地弹性；此前球地零弹性为历史方案。本轮分别把两关独立球材质改为 0.4/Maximum，线维持当前 2/Multiply、显式稳定地面维持 0/Multiply。Unity 实测合并结果为球地 0.4、球线 2、线地 0。
- 只有两份球材质的弹性与合并方式变化。D084 摩擦 0.05、D085 摩擦 0.25/角阻尼 0.05 保留；资源继续使用各自原 GUID，布局、放球、尖刺、门、Reset/Replay、配置及全局事件没有变化。
- Unity 6000.0.59f2 隔离物理步对照：两关球落地出射由 0 变为 +3.845（入射 -9.325），线落地仍为 0，球碰固定线支撑的出射均保持 +19.227；D085 零转速落斜坡，回弹并持续接触 1 秒后滚动比为 1.000。
- 当前用户 Prefab 快照 D084 为 104 个唯一对象/非零本地引用，D085 为 153 个，均完整；本轮 Prefab 与修改前逐字节一致，两份球材质与测试导入材料逐字节一致。实际对象数取当前快照，不沿用早期制作记录。
- 结构复盘：把 Maximum 仅用于球，保留线和显式地面的 Multiply，即可区分三组接触，无需新增碰撞脚本、写速度或扩展共享接口。本次用户授权两关同步调整，仍保留各关独立球材质以支持后续局部调参。
- `uncertain`：固定支撑对照不等于完整动态线布局、进门路线及 Android/iOS 试玩。球地恢复回弹会改变接触时机和轨迹；完整手感需关卡内确认。
- 证据：`E:/WorkSpace/outputs/dlevel84-85-bounce-20260914/bounce-physics-result.txt` 与 `validation.json`；回滚仅恢复两关球材质的 1/Multiply，保留原 GUID、地面绑定和 D085 滚动参数。详细风险见两关同目录功能与风险评估。

## 2026-09-14 蓝色斜平台碰撞材质对齐

- 用户指出的 `StoneB (2)` 原 BoxCollider2D 未指定材质，现仅绑定地面同款 `DLevel084_StableGround`，GUID `fdfb396140f74c76bdcfa189d2c39a48`。摩擦/弹性合并规则一致，球落平台适度回弹，画线落平台稳定。
- 155 个当前对象及本地引用完整；仅 Collider `6676497914815354429` 的材质字段变化，位置、旋转、形状、层、其它 Prefab 块与脚本不变。材质与此前真实 Unity 测试输入一致，本轮没有重复测试或增加强制速度/旋转逻辑。
- 复盘：复制或新增静态平台必须按接触需求显式赋材质，不能以外观同款推定碰撞参数相同。沿用原资源依赖和生命周期，无需新增机关或 Reset。新增平台的完整解法与移动端仍为 `uncertain`；证据与仅撤销本 Collider 材质绑定的回滚见同目录风险记录及 `E:/WorkSpace/outputs/dlevel85-platform-20260914/`。

## 2026-09-14 封闭线框限速结构补充

- 球对象新增 `DLevel84BallSpeedLimiter`，两关复用这一局部组件，未修改 D002 或共享球 Rig。Rig 实例 `3168915018210331464` 的 added component 指向 `840850000000000001`；宿主是源 `yellow_ball` GameObject `1553638369533164018`，本地 stripped 引用为 `840850000000000002`，球刚体仍显式绑定 `6262018380601849612`。
- 最大线速度 30，窄框根据 Rigidbody2D.Cast 获取的相对表面间距降低上限；FixedUpdate 与 OnCollisionEnter2D/Stay2D 都执行约束，不改角速度、不赋予弹射冲量。运行态、Dynamic、simulated 与 enabled 门禁覆盖夹持和暂停；不实现 Draw Reset 接口，因为没有需要复位的尝试状态。
- 结构复盘：真实测试说明单次钳制球速度不足以约束窄框内同一物理步的连续增能，需根据可运动距离提前降低入射速度。这里的形状 Cast 只用于物理限速，不承担用户对象查找或 Inspector 引用绑定；复用命中数组，没有全局设置或跨关卡状态。
- 最终 12 组封闭框各 60 秒真实物理模拟通过，包含两关材质、动态质量 8/32、紧凑和旋转框；原版峰值 3336 并穿出，本版保持框内与有限状态。普通球地/球线回弹、085 滚动与启停边界保留，运行时编译无错误。完整原始画法、正式关卡生命周期及移动端仍未验收。
- 绑定、改动范围、物理结果与仅移除本组件的回滚路径见同目录风险记录“球限速实施结果与验证”，证据目录 `E:/WorkSpace/outputs/dlevel84-85-speed-limit-20260914/`。之前“纯材质、不写速度”的条目仅作为限速需求前的历史基线。


## 2026-09-14 新线出生保护与有界弹射结构修正

本节取代此前“线材质弹性 2、仅球速钳制足以覆盖新增笔画”的方案。用户实际报告补画一根线时穿模，测试复现同一物理步位置已跳出、末速度却已受限。

```text
当前 DLevel 根
├─ DrawLineFeature → StrokeCompleted → 既有 IDrawStrokeCompletedHandler 分发
├─ DLevel84StrokeSpawnGuard（新增，组件 840860000000000001）
│  ├─ drawFeature = 900000000000000002
│  ├─ 只取得当前已完成运行时线条的附属碰撞体
│  ├─ 出生重叠动态碰撞对暂缓 → 分离超过 0.02 → 恢复
│  └─ AttemptReset / Disable 仅释放自身临时忽略关系
└─ yellow_ball / DLevel84BallSpeedLimiter（原组件 840850000000000001）
   ├─ elasticLineMaterial = 8db86cf100124ebfbbb13b4e50990f8f
   ├─ lineLaunchRestitution = 2；每物理步最多一次有界法向弹射
   └─ maxLinearSpeed = 30，窄框按间距继续降速；角速度保持
```

线材质本体现在为 0/Multiply，球和地面材质不变。零增能的求解器负责线线/线地稳定接触，球组件在有效线接触后补足弹射速度。出生保护先于下一物理步工作，不更改笔画几何、静态地面或 Trigger；新状态随本关实例存在，由局部接口清理，不增加全局 Reset 或事件。

复盘结论：对高弹多线场景，回调后的速度上限不能保证同一步的位移安全；须避免大于 1 的材质在求解器内反复增能，并处理新实体初始重叠。改动限制在已请求的 084/085/086 系列局部资源和显式组件，未把功能上提到 DrawLineFeature，未改变已验收其它关卡。

最终 36 组单次追加 + 16 组连续追加、各 30 秒均通过，正常单面发射/球地回弹和滚动保持，临时忽略关系清理与编译通过。组件复制基线必须同时包含零弹线材质、球有界弹射绑定和根出生保护，不能只复制其中一部分。完整关卡、任意画法及移动端仍为 uncertain，细节、边界与回滚见本关同日功能风险记录和 E:/WorkSpace/outputs/dlevel-new-stroke-tunnel-20260914/。
