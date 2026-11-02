# DLevel_084 脚本结构记录

当前实现已按文末“新线出生保护与有界弹射结构修正”更新，早期材质和纯限速描述为历史基线。

## 1. 基本信息与基线

- 关卡：`20084 / DLevel_084 / 富有弹性的线`，2026-09-14。
- 用户要求：纯物理、高弹画线，保留自行复制摆放的场景素材，修复复制后的逻辑与绑定，按 index 填 LevelData，Sort 最后追加。
- 参考 D002 的单套猫夹球入场/首笔释放/Retry 接口，以及 D013 的独立物理材质加嵌套 Prefab override。D039 仅用于识别原复制脚本中的石头依赖，未修改其实现。
- 历史参考记录包含未完成 PlayMode 的项目，不能据此推定全部已验收。本关已完成静态检查、运行时全量编译和独立 Unity Physics2D 测试；完整关卡体验与移动端为 `uncertain`。
- 初次制作只使用材质和 Prefab 参数；后续为封闭画线高速问题新增局部 `DLevel84BallSpeedLimiter`，仅 D084/D085 的球挂载。当前使用成对材质、球质量 1 与速度上限 30，窄框按活动间距进一步降速；下文第一版验证仅作为历史证据。

## 2. 组件树与职责

```text
DLevel_084
├─ DrawLevelCtrl                       通用尝试、输入、结果锁、成功/失败及 Reset
├─ DrawLineFeature                     原有多笔物理线，模板局部覆盖高弹材质
├─ DrawLevelCountdownCtrl              首笔后 20 秒，超时失败
├─ DLevel2Ctrl                         单套猫夹球 Intro 与首笔释放
├─ Alien / DrawGame_AlienClipBallRig
│  ├─ DrawEntrancePathView / DrawAlienClipBallRigView
│  ├─ ActorSpineCtrl / SkeletonAnimation / BoneFollower
│  └─ clipSwingPivot / yellow_ball
│     └─ Rigidbody2D + CircleCollider2D + DLevel84BallSpeedLimiter + 内嵌 Actor
├─ DrawGame_SuccessDoor                启用的目标门，Trigger → 当前 DrawLevelCtrl
│  └─ DrawSuccessDoorEffectCtrl         开门、彩纸及局部表现
├─ DrawGame_SuccessDoor_DisabledLegacy  保留停用的备用旧门
├─ Ground_mid / 四周 FallArea
└─ 原有背景、场景装饰、提示对象、相机
```

组件分别由 DLevel Prefab 创建与销毁。`DLevel2Ctrl` 实现 `IDrawLevelVisibleIntroHandler`、`IDrawStrokeCompletedHandler`，沿用继承的 Retry 完成态契约；倒计时也实现 Stroke Handler。材质不实现任何回调，不主动写速度。

## 3. 生命周期与关键调用

1. DrawPlayCtrl 加载本关并建立 HUD/打点上下文，向 DrawLevelCtrl 提供表内墨水数据并初始化。本关 `isConfig=0`，实际使用 Prefab 参数，表内已同步为相同数值。
2. DLevel2Ctrl 在运行时 Awake 缓存装置初态。首次进关在转场揭开后播放可见 Intro，等待期间球保持 Kinematic；完成后才开放画线。
3. 第一笔有效线完成后，画线 Rigidbody 开启模拟，猫夹打开，球脱离动画父级并切换为 Dynamic。倒计时在同一笔完成分发中启动。后续笔画不会重复放球。
4. 接触弹性由 `PhysicsMaterial2D` 与 Physics2D 求解。球质量为 1，重力与阻尼保持；球地适度回弹、球线高弹射、线地稳定。新增限速器在物理步前和碰撞后截断超速，保留方向与角速度；窄框根据相对表面间距进一步降低安全速度。
5. 启用目标门的 `OnTrigger2DHandle.onEnter` 显式调用当前 DrawLevelCtrl.OnTriggerSuccess，按球复合刚体中的 Actor 身份判定，通过结果锁后播放门表现并提交成功。
6. 20 秒超时复用 TryFailAndReset；四周 FallArea 沿用现有失败事件链。暂停仍由 Draw 通用控制器和倒计时处理，未改变共享暂停协议。
7. Reset/Replay 沿用 DrawPlayCtrl 黑幕下销毁并重建关卡/HUD。新实例经 PlayResetIntroAsync → ApplyDrawRetryIntroState 直接到位；共享 D002 的 OnDisable 只停止 Intro Tween、使旧动画回调失效。未将主线“旧实例只 teardown”规则套用为重写 Draw 接口。

## 4. 显式绑定与参数

| 宿主/字段 | 最终绑定或参数 | 用途 |
| --- | --- | --- |
| DrawLevelCtrl.drawFeature | 本关 DrawLineFeature `900000000000000002` | 画线输入、墨水与回调 |
| DrawLevelCtrl.successDoorSpine | 启用目标门的 door ActorSpineCtrl，新本地 fileID `900000000000000084` | 解析同门的成功表现组件 |
| 目标门 onEnter.m_Target | 根 DrawLevelCtrl `900000000000000001` | 修复复制后空目标 |
| DLevel2Ctrl.drawLevelCtrl | 同一根 DrawLevelCtrl | 局部生命周期 |
| DLevel2Ctrl.alien | `1255546920056987011` | 保留用户猫的位置与入场 |
| yellowBallRigidbody / ballReleaseParent | `6262018380601849612` / 根 Transform | 首笔释放球 |
| Spine/BoneFollower/clipSwingPivot | 全部保留用户 Prefab 中现有显式引用 | 入场与开夹 |
| DrawGame_DrawLine Rigidbody2D | 局部材质 `DLevel084_ElasticLine` | 摩擦 0.8、弹性 2（用户当前值）；摩擦 Minimum、弹性 Multiply |
| yellow_ball Rigidbody2D | 局部材质 `DLevel084_BallLaunch` | 摩擦 0.05、弹性 0.4；摩擦 Minimum、弹性 Maximum |
| Ground_mid BoxCollider2D | 局部材质 `DLevel084_StableGround` | 摩擦 0.8、弹性 0；摩擦 Minimum、弹性 Multiply |
| 画线参数 | 墨水 50；星级阈值 0/10/20；质量 32、最小比例 0.25；多笔开启 | 保留用户原参数，线质量约 8～32 |
| 球参数 | 质量 1、重力倍率 1.5、线性阻尼 0.03、Continuous | 试玩反馈后从质量 5 减至 1，降低球撞线时的反作用位移 |
| 球限速组件 | `DLevel84BallSpeedLimiter`，ballRigidbody=`6262018380601849612`，maxLinearSpeed=30 | 球对象上的局部 added component；窄框采用更低上限，保持转动 |
| 倒计时 | 20 秒、isSuccess=false | 根据目标门作出的当前实现假设，已向用户询问并说明 |

关键引用缺失时沿用共享组件的错误/降级处理，本关不新增查找兜底。停用旧门仍保持停用，启用门的回调和 Spine 都明确指向本关对象。

## 5. 数据、资源、UI 与事件

- `LevelData.xlsx/draw_level`：第 64 行新增 20084，20066 → 20084 → 20999，TBC 移至 65 行；名称“富有弹性的线”，版本备注取当前工程 1.14.0。
- `LevelSort.xlsx/drawLevelSort`：第 64 行已写入 `59 / 20084 / 富有弹性的线`，位于最后。源文件由 WPS 打开，因此在原窗口追加并保存；回读确认仅新增 B64/C64/D64 三个单元格，所有原有表值保持不变。
- `tbdrawleveldata.json` 与 `tbdrawlevelsort.json`：由 Luban 4.5.0 对配置快照生成，确认各仅增加一个本关条目后同步到运行时配置目录。
- 资源继续从 `Assets/Game/Prefabs/DrawLevel/DLevel_084.prefab` 加载；新增材质为 Prefab 直接依赖。未修改共享 DrawLine、Rig、门、Actor 或 GF。
- UI、存档、提示资格、revive、Analytics、广告与 SDK 全部沿用 Draw 既有流程。无新增全局事件。
- Tips 原来引用 D039 的“悬崖落石”。现改绑 `S_DrawLevel_Tips_Level_84`，本地 I2Languages 新增本关标题并按现有 Draw 惯例填入前 12 种语言，其它 64 个语言槽为空；只新增此词条，已有词条逐字节保留。线上语言源未发布，后续整表导入需保留本关词条。
- 未修改线上 RemoteConfig 或远端排序初始化；若测试环境启用了远端排序，线上数组可能覆盖本地末项，这是待验证环境条件。

## 6. 相对基线的差异与结构复盘

| 差异 | 原因与影响 | 复盘结论 |
| --- | --- | --- |
| D039 控制器改为 D002 | 场内没有 Stone，旧脚本 Awake 会报错 | 直接复用职责匹配的现有组件，无需空的 D084 子类 |
| 成对材质只覆盖线、球与地面 | 用户要求球地正常回弹、线地稳定、球线弹射 | 球 Maximum、线地 Multiply，合并弹性为球地 0.4、球线 2、线地 0；两关球材质分别持有 |
| 球质量 5 → 1 | 较重的球会把较轻的短线推走，削弱球的回弹 | 线仍按长度为 8～32，球更易沿接触法线弹出；不强改碰撞速度 |
| 目标门补 target 和 door Spine | 原 target 为 0，不能正常提交与播放门胜利 | 将 UnityEvent 接收者和表现引用一起核验 |
| 10 秒自动成功改为 20 秒失败 | 原生存倒计时会绕开场景目标门 | 当前按进门玩法实现，仍可根据用户反馈局部调整 |
| Tips 改绑 D084 标题词条 | D039 的旧词条会在运行时覆盖为“悬崖落石” | 新增本关词条及局部引用，不改旧词条 |

## 7. 验证证据与缺口

- Prefab：108 个唯一 YAML 对象，108 个唯一非零本地引用，0 重复、0 缺失；脚本和嵌套 Prefab GUID 均可解析。
- 第一版相对用户 Prefab 备份仅改 6 个组件/override 块并增加 1 个门 Spine stripped 引用；接触调优只增加球和地面两个 override 块的差异。全部原有布局和素材块保持不变。
- 原有全工程 Unity/Bee 响应文件经 Unity 自带 Roslyn 编译，`exit=0 / 0 error / 113 warning`；输出在任务验证目录，没有覆盖正在运行的 Editor 程序集。
- 第一版 Unity 6000.0.59f2 独立测试中，Maximum 与零弹性材料合并结果为 1，固定支撑反弹出/入速度比约 1.0213，动态质量 8 的线与质量 5 的球碰撞比约 0.2357。这也解释了后续用户反馈的线落地过弹。本轮改为 Multiply，最终验证见同目录功能与风险评估最新章节；独立测试不等于完整 D084 通关测试。
- 配置：两表经 Artifact Tool 编辑、回读、错误扫描与渲染。LevelData 仅将新增行合入原 XLSX，其它 ZIP 条目逐字节保持相同，原有单元格值、类型与样式验证通过；LevelSort 最终通过 WPS 原窗口追加保存，回读确认所有工作表仅新增三个目标单元格，排序全序列与运行时 JSON 一致。
- 主工程成功进入 PlayMode 并加载主界面；Firebase 编辑器 iOS 引用提示未阻止启动。检测到用户操作 Unity 后停止电脑输入，没有完成本关进门及重试测试。
- `uncertain`：未实际运行完整 D084 的进门路线、场景薄线高速接触、20 秒手感、暂停、连续 Reset/Replay、提示解法与 Android/iOS 横屏。不得把物理单项测试视作整关验收。

## 8. 风险与回滚

- 线与球的高弹接触不能保证任意画法可达，接触法线、线的支撑和动态质量比例仍由求解器决定。当前球质量 1、线质量 8～32；只在本关材质与物理参数范围内调试。
- 如需恢复，使用本任务保存的原 Prefab 副本，移除本关材质和 20084 配置并重新生成两份 Draw JSON。不得回滚其它用户改动或修改共享已验收关卡。
- 后续新增关卡应基于当时最新的 Sort 末项追加，不得重复使用本次排序号 59 或覆盖用户未保存工作。

## 2026-09-14 球地适度回弹，保留球线弹射

- 用户明确点名 D084、D085 均应有正常的球地弹性；此前球地零弹性为历史方案。本轮分别把两关独立球材质改为 0.4/Maximum，线维持当前 2/Multiply、显式稳定地面维持 0/Multiply。Unity 实测合并结果为球地 0.4、球线 2、线地 0。
- 只有两份球材质的弹性与合并方式变化。D084 摩擦 0.05、D085 摩擦 0.25/角阻尼 0.05 保留；资源继续使用各自原 GUID，布局、放球、尖刺、门、Reset/Replay、配置及全局事件没有变化。
- Unity 6000.0.59f2 隔离物理步对照：两关球落地出射由 0 变为 +3.845（入射 -9.325），线落地仍为 0，球碰固定线支撑的出射均保持 +19.227；D085 零转速落斜坡，回弹并持续接触 1 秒后滚动比为 1.000。
- 当前用户 Prefab 快照 D084 为 104 个唯一对象/非零本地引用，D085 为 153 个，均完整；本轮 Prefab 与修改前逐字节一致，两份球材质与测试导入材料逐字节一致。实际对象数取当前快照，不沿用早期制作记录。
- 结构复盘：把 Maximum 仅用于球，保留线和显式地面的 Multiply，即可区分三组接触，无需新增碰撞脚本、写速度或扩展共享接口。本次用户授权两关同步调整，仍保留各关独立球材质以支持后续局部调参。
- `uncertain`：固定支撑对照不等于完整动态线布局、进门路线及 Android/iOS 试玩。球地恢复回弹会改变接触时机和轨迹；完整手感需关卡内确认。
- 证据：`E:/WorkSpace/outputs/dlevel84-85-bounce-20260914/bounce-physics-result.txt` 与 `validation.json`；回滚仅恢复两关球材质的 1/Multiply，保留原 GUID、地面绑定和 D085 滚动参数。详细风险见两关同目录功能与风险评估。

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
