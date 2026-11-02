# Level005 绳索切割功能说明

## 需求与基线

Level005 的门由场景内多根 `SpringJoint2D` 绷在中央。原实现只在拖动点靠近绳线时切断 SpringJoint，并创建没有稳定生命周期管理的运行时刚体；门侧和钉子侧的断绳表现不完整。

## 当前实现

- `CutManager` 使用按下、移动、松手的连续手势边界，采样间隔和 UI 过滤沿用 Level081 的切割手感；`DragTarget` 的 `TrailRenderer` 在运行时配置为短拖尾。
- 刀光只在 PlayCtrl 处于 PLAYING 且 BaseLevelCtrl 允许角色操作、关卡未成功/失败时接收，避免死亡或进门演出期间继续切绳。
- 开局只由 Level005 私有控制器整理物理：门暂时为 Kinematic，锚点为 Static，锚点 Collider 在玩法期间关闭；SpringJoint 保持门居中并关闭关节碰撞。
- 每次有效切割先计算绳段内的真实切口，再事务式创建两个无 Collider 的动态切点、两个 `DistanceJoint2D` 和门侧半绳。全部创建成功后才标记锚点已切、释放门为 Dynamic，并按刀光方向施加切向速度、门冲量和角冲量。
- 原线改为锚点到锚点侧切点，克隆线改为门到门侧切点；两段线由端点 Transform 每帧刷新，因此可随门和切点摆动。切点关节 `enableCollision=false`，运行时对象使用非碰撞层且不添加 Collider，绳、钉子和角色不会因本功能新增碰撞。
- Reset、Replay、禁用和销毁会销毁运行时切点/克隆线，恢复锚点 Cut 标志、SpringJoint 完整参数、门与锚点 Rigidbody 状态、Collider 状态和线条透明度。

## 影响范围

本次行为变更只在 `Assets/Game/Scripts/Level/MainLevel/Level_5/CutManager.cs`。`Level5Anchor.cs`、`Level5TrackLine.cs` 保持仓库原始 API 和 GUID，未修改 Level081 或其他关卡脚本、Prefab、共享物理层和配置。

## 验证

- Unity 6000.0.59f2 Roslyn 定向编译：Level005 三个脚本 `EXIT=0`、无 error；日志中的 `CS0436` 仅来自定向编译同时引用旧 `Assembly-CSharp` 的同名类型覆盖。
- Unity 6000.0.59f2 Roslyn 完整工程后台编译（955 个源文件）`EXIT=0`、无 error；107 条 warning 中 Level005 仅为审计响应文件同时引用旧程序集产生的预期 `CS0436`，其余为工程既有 warning。
- 已静态检查 Level005 Prefab 的五个锚点、门、DragTarget 和脚本 GUID 引用闭合；Level005 Prefab 本身无工作区差异。
- `CutManager` 仅在门释放阶段临时使用连续碰撞，重置时恢复门原始碰撞模式；刀光拖尾运行时置于 `SceneLayer` 前景并在重开时复位位置。断口关节已显式设置 `autoConfigureConnectedAnchor=false`，绑定门侧切点后不会被 Unity 自动重算连接锚点。
- 未启动前台 Unity，未执行 PlayMode、真实触摸设备或长时间物理摆动验证，这些项目仍为 `uncertain`。
- 仓库指定的 `specs/037-remote-resource-loading/plan.md` 当前缺失，相关计划依据为 `uncertain`。

## 回滚

恢复 `CutManager.cs` 到变更前版本，并删除本文件及同目录风险评估即可。共享脚本已保持原状，无需回滚其他关卡。

## 2026-09-01：门自转、切点与双半绳反馈修正

### 需求与基线

- 本轮反馈指出：门在释放后会持续自转；刀光在绳中段切下时却像整根绳一起消失；切断后钉子侧和门侧两段绳的悬挂不符合预期。
- 基线仍是 Level005 Prefab 中五根 authored `SpringJoint2D`。本轮未改变 Prefab、锚点位置、门视觉层级或共享 Level081 脚本。

### 实现与影响

- `CutManager.LateUpdate()` 在物理关节求解后做局部稳定：释放后的角阻尼至少为 `12`，角速度上限为 `180` 度/秒，每帧以 `180` 度/秒² 刹车，摆角限制为 `75` 度，低于 `0.5` 度/秒时归零；角冲量改为显式可选（默认缩放 `0`），切向施力力臂限制为 `0.2` 世界单位。
- `TryCutSegment()` 为一次拖动只提交一根绳，并优先选择刀光真正穿过的绳。`ClosestPointOnSegmentToSegment()` 在近距离但未相交时返回绳段上的投影点；`CutLine()` 对切点沿绳方向夹紧并保留最小可见断口（默认 `0.34` 世界单位），因此两侧半绳不会被误移到端点。
- 断口两侧各创建无 `Collider2D` 的动态切点和精确长度 `DistanceJoint2D`；原线保留锚点到锚点侧切点，克隆线连接门侧切点到门侧视觉挂点。门释放后未断绳默认替换为 `maxDistanceOnly=true` 的张力约束，避免剩余两根绳落到另一组距离圆交点；所有关节 `enableCollision=false`。
- 运行时门边挂点为本关可选模式。开局和默认释放状态使用 Prefab 的门根端点；仅在 Inspector 同时启用对应 edge 选项后，未断绳和门侧半绳才接到各自门边，根端模式的显式回退质心默认为 `(0,0)`。
- 运行时切点、半绳和释放后局部关节在 Reset、Replay、禁用及销毁时清理，并恢复 authored SpringJoint、Rigidbody、Collider、线端点和门姿态。

### 数据与事件流

本轮没有新增 DB、Luban、存档、全局事件、资源地址或共享 UI；输入、暂停、失败、重开和胜利仍沿用 `CutManager` 与既有 `PlayCtrl` 生命周期。

### 验证与未验证项

- 使用 Unity `6000.0.59f2` 自带 Roslyn 在后台执行 Level005 定向编译和完整工程编译，最新结果均为 `EXIT=0`、`0 errors`；对应日志为 `BrainPunk/.codex-temp/Level005AuditPostEdge.log` 与 `BrainPunk/.codex-temp/Assembly-CSharp.Level005PostEdge.log`。
- 静态核对确认 Prefab 有五个锚点、五根线，五个线端均引用门根 `Rigibody2D`；Prefab 工作区无差异。未启动前台 Unity，未执行 PlayMode、真实触摸或长时间物理摆动，门的实际观感和切割命中仍为 `uncertain`。
- 未切支撑绳现在默认采用张力（最大距离）约束并保留门根端点；不同切割顺序的摆幅和两绳最终姿态仍需 PlayMode 观察，不能由静态编译证明。

### 本轮回滚

仅需恢复 `CutManager.cs` 到本轮前版本即可撤销旋转稳定、投影切点和局部张力绳；不需要回滚 Level005 Prefab、Level081 或共享物理脚本。

## 2026-09-01：门边挂点与切口一致性修正

### 需求与影响

- 针对“门持续旋转”“中段切割像整根绳消失”以及“两根绳吊门姿态异常”的反馈，继续只修改本关私有 `CutManager.cs`；`Level5Anchor.cs`、`Level5TrackLine.cs`、Level005 Prefab、Level081 和共享物理脚本均未改动。

### 行为

- `useDoorEdgeAttachments` 仅在 Inspector 显式开启时生效，默认关闭；开局 SpringJoint 与线条始终使用 authored 门根，避免初始场景被运行时几何挂点改位。
- 释放时默认保留门根端点并用张力约束；显式开启 edge 模式时才按当前门边挂点计算保守绳长，并把对应 authored 线的视觉终点同步到该挂点。
- 门侧半绳克隆脱离钉子 `SortingGroup` 并置于 `SceneLayer` order `101`，切点与关节仍无碰撞；根端回退模式显式使用零质心，避免 door_0 Trigger 的偏移产生恒定重力扭矩。

### 验证与风险

- 后台定向 Roslyn：`BrainPunk/.codex-temp/Level005AuditPostEdge.log`，`EXIT=0`；完整工程 Roslyn：`BrainPunk/.codex-temp/Assembly-CSharp.Level005PostEdge.log`，`EXIT=0`、0 errors、107 条工程既有 warning。`git diff --check` 与 Prefab 静态引用检查通过。
- 未启动前台 Unity，未执行 PlayMode、真实触摸或长时间物理摆动；门的摆动幅度、两绳最终姿态、不同切割顺序和移动端表现仍为 `uncertain`。

### 回滚

仅恢复 `CutManager.cs` 本轮改动即可回滚；不回滚 Level005 Prefab、Level081 或共享脚本。

## 2026-09-01：双绳姿态的物理端点修正

- 静态复核发现门边挂点比 authored 门根短约 `1.2~1.95` 世界单位；若同时把 `DistanceJoint2D.distance` 改成该短距离，会在释放时改变绳长并放大只剩两根绳时的位移/转矩。
- `useDoorEdgeAttachments` 现在只控制释放后的可见绳端，默认仍开启；新增 `useDoorEdgePhysics` 默认关闭。未断绳和门侧半绳的物理关节默认继续接 authored `Rigibody2D` 门根，避免视觉端点变化改变受力。
- 若策划明确开启 `useDoorEdgePhysics`，未断绳会自动改为 tension-only `DistanceJoint2D`，并保留 authored 绳长（取 authored 长度与当前距离的较大值），不会把绳压成刚性杆；该可选模式仍需 PlayMode 观察。
- 其余切口交点保存、单手势单绳、无碰撞切点、门旋转刹车与 Reset/Replay 清理逻辑不变。当前 Level005 Prefab、Level081 和共享脚本仍未修改。

### 验证

- `CutManager.cs` 最新 SHA-256：`4CC27ACEC4B211E358ACAE616F4F20E7D7F5066E2FE0F4F29942973D4D86F0BA`；后台 Unity Roslyn 定向编译和完整工程编译均 `EXIT=0`、`0 errors`，完整工程保留 `107` 条既有 warning。
- 静态核对确认五根 authored 线端仍指向门根、Prefab 无工作区差异；未启动前台 Unity，门的实际停止自转、两绳姿态、触控命中和移动端表现仍为 `uncertain`。

### 回滚

仅恢复 `CutManager.cs` 本次变更即可回到上一版；不回滚 Level005 Prefab、Level081 或共享脚本。

## 2026-09-01：剩余支撑绳张力模式复核（覆盖此前默认值描述）

### 需求与实现

- 本节覆盖本文件前文关于“释放后默认使用门边挂点”以及“未断支撑绳默认定长”的历史描述；当前有效默认值以 `CutManager.cs` 为准。
- `releasedRopesMaxDistanceOnly` 现在默认为 `true`。门释放后，尚未切断的支撑绳只限制最大长度，允许绳索松弛并以张力吊住门，避免两根定长约束把门锁在两个圆的另一交点，造成异常跳位或扭转。
- 断口两侧仍使用精确长度 `DistanceJoint2D`，从实际切口向两端保留两段半绳；切点无 `Collider2D`，因此中段切割不会把整条绳视觉上删除，也不会给角色增加碰撞。
- `useDoorEdgeAttachments` 与 `useDoorEdgePhysics` 均默认为 `false`。开局和释放后的默认视觉/物理端点继续使用 Prefab authored 的 `Rigibody2D` 门根；门边挂点仅在 Inspector 显式开启对应选项时创建。这样避免门边几何触发框与 authored 绳长不一致时产生突变。
- 门仍允许有限旋转；释放后的角阻尼、角速度上限、刹车和摆角限制保留，切割角冲量默认关闭，避免门持续自转。门侧半绳克隆提升到高于门渲染组的排序层级，保持断口可见。

### 验证与未验证项

- Level005 Prefab 无工作区差异；五根 authored 线端和五个 `SpringJoint2D` 连接体仍指向门根 `Rigibody2D`。脚本静态核对确认运行时切点不添加 Collider，新增关节关闭碰撞并关闭自动连接锚点/距离配置。
- Unity 6000.0.59f2 Roslyn 定向及完整工程后台编译均 `EXIT=0`、`0 errors`；最新日志为 `BrainPunk/.codex-temp/Level005AuditCurrent.log` 和 `BrainPunk/.codex-temp/Assembly-CSharp.Level005Current.log`。
- 未启动前台 Unity，未运行 PlayMode、真实触控或长时间双绳摆动；门的最终摆幅、两绳张力观感和移动端表现仍标记为 `uncertain`。仓库指定的 `specs/037-remote-resource-loading/plan.md` 缺失，相关计划依据同样为 `uncertain`。

### 回滚

仅恢复 `CutManager.cs` 到本节之前版本即可撤销默认张力模式；Level005 Prefab、Level081、Level5Anchor/TrackLine 和共享物理脚本不在回滚范围内。

## 2026-09-02：门旋转收敛参数复核

- 在保留有限摆动的前提下，释放后的门新增本关可调弱角弹簧：`releasedAngularRestoreTorque=0.5`、死区 `1` 度，目标为 authored 初始姿态；`keepDoorUpright` 仍是需要完全锁定时的显式开关，默认不冻结旋转。
- 角速度上限、刹车和最大摆角按源码默认值启用，角冲量默认缩放为 `0`。剩余支撑绳仍默认 `maxDistanceOnly=true`，断口半绳仍为定长；因此回正力不会把绳索重新变成刚性杆。
- 本节覆盖前文关于默认门边挂点/定长支撑绳的旧描述；当前 `useDoorEdgeAttachments=false`、`useDoorEdgePhysics=false`，默认端点仍为 authored 门根。

### 验证与风险

- 2026-09-02 后台 Unity Roslyn 定向和完整工程编译均 `EXIT=0`；日志为 `BrainPunk/.codex-temp/Level005AuditSep2.log` 与 `BrainPunk/.codex-temp/Assembly-CSharp.Level005Sep2.log`。静态 Prefab 引用、无 Collider 切点、关节开关和 `git diff --check` 已核对。
- 未启动前台 Unity，未执行 PlayMode、真实触控或双绳长时摆动；回正强度、门最终姿态、断口在不同排序层级下的观感和移动端表现仍为 `uncertain`。`specs/037-remote-resource-loading/plan.md` 缺失，相关计划依据也为 `uncertain`。

### 回滚

恢复 `CutManager.cs` 本节新增字段和 `StabilizeReleasedDoorRotation()` 改动即可；不回滚 Level005 Prefab、Level081 或共享脚本。

## 2026-09-02：当前实现收尾审计（以本节覆盖早期默认值描述）

### 本轮反馈与当前行为

- 本轮针对“门释放后持续自转”“中段切口整根消失”和“仅剩两根绳时姿态异常”做收尾。当前有效实现以源码默认值和本节为准，前文历史记录中的 `FixedUpdate`、默认 edge 挂点或默认定长描述不再代表当前默认配置。
- 旋转收敛在 `LateUpdate()` 执行，位于 `DistanceJoint2D` 求解之后；释放门仍允许有限摆动，但由 `minimumReleasedAngularDamping=12`、`releasedAngularBrake=180`、角速度上限 `180`、最大摆角 `75` 和 `0.5` 度/秒静止阈值共同收敛。角冲量缩放默认 `0`，不会主动制造持续自转。
- `TryCutSegment()` 只在一次手势中提交一根绳，并优先真实相交的绳段；`CutLine()` 保存同一次采样得到的绳上交点，沿绳方向夹紧并保留默认 `0.34` 世界单位可见断口。原线保留钉子侧半绳，克隆线保留门侧半绳，二者分别由精确长度 `DistanceJoint2D` 驱动。
- 运行时切点不添加 `Collider2D`，新增关节显式关闭碰撞、自动连接锚点和自动距离。未断支撑绳默认 `releasedRopesMaxDistanceOnly=true`，允许松弛后再受张力；`useDoorEdgeAttachments=false`、`useDoorEdgePhysics=false`，所以默认端点仍是 Prefab authored 的 `Rigibody2D` 门根，edge 模式仅供本关 Inspector 明确开启后复测。
- Reset、Replay、暂停、禁用和销毁路径会清理运行时切点、半绳、释放关节和隐藏挂点，并恢复 authored SpringJoint、Rigidbody、Collider、线端点和门姿态。

### 影响与验证

- 行为变更仍只落在私有 `Assets/Game/Scripts/Level/MainLevel/Level_5/CutManager.cs`；`Level_005.prefab`、`Level5Anchor.cs`、`Level5TrackLine.cs`、Level081 和共享物理脚本没有工作区差异。
- 定向 Roslyn 编译：`BrainPunk/.codex-temp/Level005AuditFinalCurrent2.log`，`EXIT=0`；全量 Assembly-CSharp 编译：`BrainPunk/.codex-temp/Assembly-CSharp.Level005FinalCurrent3.log`，`EXIT=0`、`0 errors`、`107` 条工程既有 warning。
- 静态断言全部通过：5 个 anchor、5 个 authored line、5 个 SpringJoint、5 个门端引用；Prefab/共享 Level5 脚本无差异；运行时切点无 Collider；新增关节关闭碰撞并关闭自动锚点/距离；切割与 LateUpdate 收敛路径存在。当前 `CutManager.cs` SHA-256 为 `B4C6911A337C632218D459A70D300159E9182DB75FC3CC45A1D21605D75171AA`。

### 未验证项与回滚

- 未启动前台 Unity，未执行 PlayMode、真实触摸、不同切割顺序、长时间双绳摆动、Prefab 导入或移动端验证；门最终摆幅、两绳观感、实际触控命中和移动端行为均为 `uncertain`。仓库要求的 `specs/037-remote-resource-loading/plan.md` 当前缺失，相关计划依据同样为 `uncertain`。
- 若运行态仍需改变两绳受力点，可只在 Level005 Inspector 试调 `useDoorEdgeAttachments`/`useDoorEdgePhysics` 及阻尼参数；回滚本轮实现只需恢复 `CutManager.cs`，不回滚 Prefab、Level081 或共享脚本。

## 2026-09-02：当前反馈修复（端点一致性与切口选择）

### 需求背景

- 本轮针对门释放后持续自转、中段切绳看起来整根消失，以及只剩两根绳时门姿态异常进行修正。附件图片仅作为视觉参考，行为以本关脚本和策划文字为准。

### 当前行为

- `useDoorEdgeAttachments` 与 `useDoorEdgePhysics` 的源码默认值均为 `true`。运行时为每根绳创建隐藏的门边挂点；开局 `SpringJoint2D`、切口命中/可见线、门侧半绳和释放后的支撑关节使用同一门边端点，避免首刀从门根切换到门边时出现跳变。即使只打开物理 edge，视觉端点也会自动跟随该挂点，避免 Inspector 误调造成分离。
- `PrepareInitialPhysics()` 在 edge 物理模式下用绳起点到门边挂点的当前距离设置初始弹簧长度；root 回退模式仍使用 authored `SpringJoint2D.distance`。释放后的未断支撑绳默认使用 `releasedRopesMaxDistanceOnly=true`，允许松弛后再受张力，断口两侧半绳继续使用精确长度。
- 一次手势最多提交一根绳。候选排序现在明确为真实相交优先、绳段内部交点优先、刀光路径先后优先，最后才比较距离；切口位置沿当前绳段夹紧并保留可见间隙。门侧半绳提升排序层级，切点和半绳均不带碰撞体，新增关节关闭碰撞和自动锚点/距离配置。
- 释放门的旋转在 `FixedUpdate()` 做物理步阻尼/回正，在 `LateUpdate()` 仅清理求解器残余角速度；角速度上限、摆角上限和静止阈值保留有限摆动而抑制持续自转。Reset/Replay/暂停/禁用/销毁会清理运行时挂点、半绳和关节并恢复 authored 状态。

### 影响范围

- 行为修改仅在私有控制器 `Assets/Game/Scripts/Level/MainLevel/Level_5/CutManager.cs`；`Level_005.prefab`、`Level5Anchor.cs`、`Level5TrackLine.cs`、Level081 与共享物理脚本未修改。Prefab 的 authored 线端仍指向 `Rigibody2D` 门根，运行时覆盖由控制器完成。

### 验证

- Unity 6000.0.59f2 Roslyn 定向编译：`.codex-temp/Level005AuditFinalFeedback2.log`，`EXIT=0`，仅有定向审计产生的类型冲突 warning。
- Assembly-CSharp 全量后台编译：`.codex-temp/Assembly-CSharp.Level005FinalFeedback2.log`，`EXIT=0`、`0 errors`，107 条为工程既有 warning。
- Prefab 静态断言保持 5 个 anchor、5 个线段、5 个 `SpringJoint2D`，且 authored 端点/连接体未被改写；`git diff --check` 通过。
- 本轮源码 SHA-256：`B35CD6202CA1599D10D61AAD662847A5EE83A7C5D0E1B2E3AB705808F4AC8890`。

### 未验证项与回滚

- 未启动前台 Unity，未执行 PlayMode、真实触控、不同切割顺序、长时间双绳摆动、Prefab 导入或移动端验证；门最终摆幅、两绳观感、edge 挂点对美术构图的影响和触控命中仍为 `uncertain`。仓库要求的 `specs/037-remote-resource-loading/plan.md` 缺失，相关计划依据同样为 `uncertain`。
- 如运行态仍需改变受力点或门姿态，优先在 Level005 Inspector 调整本控制器私有阻尼/edge 参数；回滚仅恢复 `CutManager.cs` 本轮改动，不回滚 Prefab、Level081 或共享脚本。

## 2026-09-02：门背后绳端、断绳收敛与柔性视觉

### 需求背景

- 线段端点应从门的背后进入，不能露在门 Sprite 前面。
- 绳被割开后应有短暂自然运动，但不能持续甩动；Reset/Replay 也不应出现一帧重新连绳。
- 直线视觉需要有轻微柔体下垂感，且不能改变绳的碰撞与关节物理。

### 当前行为

- `ResolveCutVisualDoorPoint()` 与 `ResolveCurrentVisualDoorPoint()` 始终使用 authored 门根作为可见端点和切割命中几何；门侧半绳不再提升排序层级，沿用源线 authored sorting order，因此在门 Sprite 后方收口。`useDoorEdgePhysics` 仍可为物理关节提供隐藏的门边力臂，但不会把可见线移到门前。
- 断口动态端点继续无 Collider；`cutPointSettlementDamping` 在创建端点时作为线性阻尼下限，并在 `FixedUpdate()` 以指数衰减清理残余速度，降低断绳后持续甩动。切口分离冲量仍由既有 Inspector 参数控制。
- `LateUpdate()` 在 `Level5TrackLine.Update()` 之后为未断绳和两侧半绳生成多点重力下垂曲线；曲线只写入 `LineRenderer` 采样点，端点、关节长度和碰撞设置不变。采样数、下垂幅度和短绳最小下垂均暴露在 Level005 `CutManager` Inspector。
- `GameResetEvent`/`GameReplayEvent` 的转场清理使用 `CleanupRuntime(false)`：销毁运行时切点、半绳、释放关节和隐藏挂点，并临时禁用 authored SpringJoint/LineRenderer，不在旧场景销毁前恢复连接；普通禁用和销毁仍走完整 authored 状态恢复。

### 影响与验证

- 本轮仅修改 `Assets/Game/Scripts/Level/MainLevel/Level_5/CutManager.cs` 与本地文档；未修改 `Level_005.prefab`、`Level5Anchor.cs`、`Level5TrackLine.cs`、Level081 或共享物理脚本。
- 定向 Roslyn 编译 `.codex-temp/Level005AuditCurrentFeedback.log`：`EXIT=0`；全量 Assembly-CSharp 编译 `.codex-temp/Assembly-CSharp.Level005CurrentFeedback.log`：`EXIT=0`、`0 errors`。`git diff --check` 通过。
- 已静态确认可见门端点回退到 authored 门根、门侧半绳不再调用提升排序路径、柔性曲线与断点收敛路径存在，Reset 转场分支不调用 authored 恢复逻辑。

### 未验证项与回滚

- 前台 Unity PlayMode、真实触控、不同切割顺序、长时间摆动、Prefab 导入和移动端仍未执行；门背后遮挡效果、曲线美术观感和 Reset 转场的实际帧表现为 `uncertain`。仓库指定的 `specs/037-remote-resource-loading/plan.md` 缺失，计划依据同样为 `uncertain`。
- 回滚只需恢复本文件对应版本的 `CutManager.cs`；不回滚 Prefab、Level081 或共享脚本。

## 2026-09-02：断绳摆动速度调校

- `cutPointSettlementDamping` 默认由 `2.5` 调整为 `0.75`，避免阻尼把断口绳端拖成慢速漂浮；`cutPointImpulseScale` 默认提高到 `0.22`，切断瞬间保留更清晰的动作响应。
- 新增 `cutPointGravityMultiplier=1.35`，断口动态端点的有效重力按本关 Inspector 的基础值乘以该倍率，令下坠和摆动更快、更有重量感；该参数只作用于运行时切点，不修改 authored Rigidbody 或共享脚本。
- 断口端点仍由 `FixedUpdate()` 指数收敛，速度调校不会恢复持续甩动的旧行为。定向与全量后台编译均保持 `EXIT=0`、0 errors；实际 PlayMode 摆动速度和美术观感仍为 `uncertain`。

## 2026-09-02：Prefab 摆动参数同步

### 需求与实现

- 反馈指出切断后的绳子仍显得轻飘、甩动过慢。此前脚本默认值已调校，但 `Level_005.prefab` 中仍保留旧的序列化值，运行时会以 Prefab Inspector 值覆盖脚本默认值。
- 已仅更新本关 `Level_005.prefab`：`cutPointSettlementDamping` 从 `2.5` 同步为 `0.75`，`cutPointImpulseScale` 从 `0.1` 同步为 `0.22`；`cutPointGravityMultiplier=1.35` 保持不变。断口端点因此保留更明显的切断初速并更快响应重力，仍沿用现有的无碰撞切点和 FixedUpdate 收敛逻辑。
- 未修改共享脚本、Level081 或其它关卡资源。

### 验证与未验证项

- `Level005Audit.rsp` 定向 Roslyn 编译：`BrainPunk/.codex-temp/Level005AuditPrefabSync.log`，`EXIT=0`。
- 静态检查确认 Prefab 当前序列化值为 `0.22 / 0.75 / 1.35`，`git diff --check` 通过。
- 未启动前台 Unity，未执行 PlayMode、触控或长时间摆动；实际摆动速度和美术观感仍为 `uncertain`。仓库指定的 `specs/037-remote-resource-loading/plan.md` 缺失，相关计划依据同样为 `uncertain`。

### 回滚

仅恢复 `Level_005.prefab` 中上述两个参数即可回到本轮前的运行配置；无需回滚脚本或其它关卡。

## 2026-09-02：补齐入口动画并锁定悬挂门旋转

### 需求与实现

- Level005 原先 `BaseLevelCtrl.enterDoorAnim` 为空，入口只有静态 Sprite，首次进入关卡不会播放标准进门流程。
- 已复用 `StartDoor.prefab` 与前置关卡一致的 `EnterDoorAnimCtrl`，把它作为 Level005 的入口门并对齐到原静态门位置（`-8.84, -1.2`）；`BaseLevelCtrl.enterDoorAnim` 保持为空，由既有 `StartDoor` 名称查找逻辑取得该组件，沿用 `door_open`、角色淡入走出和 `dose_close` 流程。动画组件的四个对象引用留空，使用组件内既有的运行时自动绑定，避免旧演员嵌套 Prefab 的断引用。原静态 `StartDoor_Legacy` 保留但停用，避免重叠渲染。
- Level005 悬挂门的 `CutManager` Inspector 已设置 `allowDoorRotation=0`、`keepDoorUpright=1`。释放门时继续使用 `FreezeRotation` 约束，并将角速度归零，绳索切割和位移逻辑不变。
- 未修改共享 `EnterDoorAnimCtrl`、Level004 或其它关卡资源。

### 验证与未验证项

- 静态检查确认入口 `enterDoorAnim` 为空且 Level 根下新增 `StartDoor.prefab` 实例含 `EnterDoorAnimCtrl`；组件引用由运行时自动绑定，旧静态门为 inactive。Unity 已完成该 Prefab 的重新导入，最新 artifact 无 `Broken text PPtr` 或 Prefab 导入错误。
- 静态检查确认 Level005 序列化旋转开关为 `allowDoorRotation: 0`、`keepDoorUpright: 1`；`CutManager.cs` 定向 Roslyn 编译保持 `EXIT=0`。
- 当前 Unity 编辑器已完成项目加载，但为避免抢占用户前台控制权未启动 PlayMode；入口动画实际画面、角色出门方向、门不旋转的运行态仍标记为 `uncertain`。仓库指定的 `specs/037-remote-resource-loading/plan.md` 缺失，相关计划依据同样为 `uncertain`。

### 回滚

恢复 `Level_005.prefab` 的 `enterDoorAnim`、入口 PrefabInstance 和 `StartDoor_Legacy` 状态，并将两个旋转开关还原，即可撤销本轮改动；共享脚本无需回滚。

## 2026-09-02：顶部绳线垂直对齐

### 需求与实现

- 截图反馈指出 Level005 初始状态顶部锚点到悬挂门的绳线向右倾斜。
- 原因是 `Circle (1)/linestart` 的世界横坐标为 `4.43`（锚点 `4.69` 加本地偏移 `-0.26`），而门侧 authored 端点使用 `Rigibody2D` 根节点 `x=4.65`；两端横坐标相差 `0.22`。`CutManager` 的柔性曲线只在此直线基础上增加下垂，不是倾斜的主因。
- 仅修改 `Level_005.prefab`：将 `Circle (1)/linestart` 的本地 X 从 `-0.26` 调整为 `-0.04`，使其世界 X 与门根一致；同步该 LineRenderer 的 authored 首点。未修改 `CutManager.cs`、`Level5TrackLine.cs`、Level081 或共享物理逻辑。

### 验证与未验证项

- 静态检查确认 `Circle (1)/linestart` 和门根的计算世界 X 均为 `4.65`，对应 LineRenderer 首点为 `x=-0.03999996`。
- `git diff --check` 仍只报告工作区既有 Prefab 导入块的空值行尾空白；本次新增坐标行没有格式错误。Unity 重新导入和运行态视觉效果待导入完成后确认，未启动 PlayMode。
- 由于仓库指定的 `specs/037-remote-resource-loading/plan.md` 缺失，计划依据仍为 `uncertain`。

### 回滚

将 `Circle (1)/linestart` 本地 X 和对应 LineRenderer 首点恢复为本次修改前的 `-0.26`（约 `-0.25999975`）即可回滚；无需回滚脚本或其它关卡资源。

## 2026-09-02：绳端统一、门质量与多绳切割

### 需求与实现

- 截图反馈指出门悬挂时可见绳线与 `Line` 不重合、下方半绳端点异常，同时门的重量感不足，并要求一次滑动可以切开多根绳。
- 仅在 Level005 范围内调整：关闭 `useDoorEdgeAttachments` 和 `useDoorEdgePhysics`，让 authored 绳、断口半绳和物理关节统一使用 `Rigibody2D` 门根端点；门本体 `Rigidbody2D.m_Mass` 从 `1` 提高到 `3`，五个锚点质量保持 `1`。
- `CutManager.TryCutSegment` 现在会先收集当前刀光线段穿过的全部未断绳，按刀光路径排序后一次提交；若没有真实穿线，仅保留原有“最近一次近失误”容错。移除了同一手势只能提交一根的早退条件，切点快照在门释放前完成，避免第一根绳导致门移动后漏切后续绳。

### 验证与未验证项

- 定向 Roslyn 编译 `.codex-temp/Level005AuditMultiCut.log`：`EXIT=0`；仅保留既有类型冲突警告，无 `error CS`。
- 静态检查确认门质量为 `3`、锚点质量均为 `1`、两个门边挂点开关为 `0`，并确认 `Circle (1)/linestart` 与门根的 X 坐标仍为 `4.65`。
- Unity 已重新导入 Level005 Prefab（最新 artifact `d9b50a51fb0734b5101fb2a3617d1072`），该次导入没有新增 `Broken text PPtr` 或 Prefab 错误。未启动 PlayMode 或接管前台 Unity，因此门的重量观感、上下半绳遮挡和同一滑动切多根的真实触控表现仍待运行态确认；该计划依据缺失仍标记为 `uncertain`。

### 回滚

恢复本轮 `Level_005.prefab` 的 `useDoorEdgeAttachments`、`useDoorEdgePhysics`、门 `m_Mass`，并恢复 `CutManager.TryCutSegment` 的单候选逻辑即可回滚；不需要回滚其它关卡或共享脚本。
