# Level005 风险评估

## 已知风险

- 仓库要求引用的 `specs/037-remote-resource-loading/plan.md` 在当前工作区缺失，相关计划依据为 `uncertain`；本次仅依据现有关卡脚本、Prefab 和用户需求实现。
- 完整工程后台 Roslyn 编译（955 个源文件）已 `EXIT=0`、0 errors；107 条 warning 中 Level005 仅为审计响应文件同时引用旧程序集产生的预期 `CS0436`，其余为工程既有 warning；Level81 当前没有阻断本次构建。
- 定向编译只覆盖 Level005 三个脚本和现有程序集引用，未覆盖 Unity Editor 序列化、Prefab 导入和完整 Assembly-CSharp 生成。
- 门的实际摆动幅度、不同切割顺序的观感、动态半绳在暂停/恢复和重开后的视觉一致性尚未在前台 Unity PlayMode 验证，标记为 `uncertain`。
- 真实触控采样、低帧率下的切口命中、不同相机深度和 Android/iOS 输入行为尚未验证，标记为 `uncertain`。
- 当前 Level005 Prefab 保留 authored `radius=0.5`，大于 Level081 的 `sliceBladeRadius=0.15`；为避免擅自改变既有序列化参数，本轮未改该值。若 PlayMode 出现误切，可在本关 Inspector 将其调到约 `0.15~0.2` 后复测，当前手感结论为 `uncertain`。
- 若关卡 Prefab 后续新增 Collider 或改名门对象，私有引用解析需要重新做静态检查；当前已优先使用序列化门引用和 `Rigibody2D` 名称，避免误绑锚点。

## 已采取的缓解

- 切绳采用先建对象、后提交状态的事务顺序，创建失败时不会留下半切状态。
- 切口沿绳方向夹在两端之间，避免断口越过钉子或门；所有运行时切点和半绳没有 Collider，关节禁止碰撞。
- Reset/Replay/OnDisable 清理运行时对象并恢复捕获的 SpringJoint、Rigidbody 和 Collider 状态；暂停只暂时关闭动态模拟，不销毁绳段。
- `DistanceJoint2D.autoConfigureConnectedAnchor=false` 已在创建断口关节时显式设置，避免绑定 `connectedBody` 后自动覆盖门侧切点；当前 Level005 Prefab 的 authored `SpringJoint2D` 该值均为 false。该字段现已纳入 `SpringJoint2D` 初始状态快照，未来策划调整 authored 值后 Reset/Replay 也会按初态恢复。

## 验证证据与回滚

定向命令使用 Unity Roslyn：

```text
F:\6000.0.59f2\6000.0.59f2\Editor\Data\NetCoreRuntime\dotnet.exe
  F:\6000.0.59f2\6000.0.59f2\Editor\Data\DotNetSdkRoslyn\csc.dll
  @BrainPunk/.codex-temp/Level005Audit.rsp
```

结果为 `EXIT=0`。若需回滚，恢复 `CutManager.cs` 并删除本目录两份 Level005 文档；Level081 和共享 Level5 脚本不在回滚范围内。

完整工程复核使用同一 Unity Roslyn：

```text
F:\6000.0.59f2\6000.0.59f2\Editor\Data\NetCoreRuntime\dotnet.exe
  F:\6000.0.59f2\6000.0.59f2\Editor\Data\DotNetSdkRoslyn\csc.dll
  @BrainPunk/.codex-temp/Assembly-CSharp.Level005Check.rsp
```

结果为 `EXIT=0`、0 errors；完整日志保存在 `BrainPunk/.codex-temp/Assembly-CSharp.Level005FinalCheck2.log`。

## 2026-09-01：本轮反馈修正复核

### 事实与直接结论

- 变更范围仅为 `CutManager.cs`；`Level5Anchor.cs`、`Level5TrackLine.cs`、
  `Level_005.prefab`、Level081 和共享物理层没有工作区改动。
- 代码现在把一次拖动限制为一次有效切割，并在真实相交优先的前提下将切点投影到绳段内部；可见断口和两侧精确长度关节避免把整条线视觉上当作一次性删除。
- 门释放后的旋转由局部角阻尼、角速度上限、刹车和摆角上限共同约束；默认人工角冲量缩放为 `0`。该结论来自源码静态检查，不能替代运行态观感验证。
- 断口切点不带 Collider，门侧半绳使用独立切点；未切支撑绳当前默认替换为 `maxDistanceOnly=true` 的张力约束并继续使用门根端点，edge 端点仅在本关显式开启后生效；所有新增关节关闭碰撞。

### 风险、假设与缺失证据

- `specs/037-remote-resource-loading/plan.md` 在当前工作区缺失，按仓库规则相关计划依据为 `uncertain`。
- 未在前台 Unity PlayMode 验证门停止自转的速度、摆角、两侧半绳张力、不同切割顺序、暂停/恢复或重开后的视觉连续性；这些均为 `uncertain`。
- Prefab authored `radius=0.5` 仍大于 Level081 对照值 `0.15`。宽命中半径在低帧率或靠近相邻绳时可能误切；如 PlayMode 复现，应在本关 Inspector 先试 `0.15~0.2`，不要修改共享脚本。
- 五个 authored 线端仍引用门根 `Rigibody2D`；默认释放后视觉和物理端点继续使用门根，只有显式开启 edge 选项时才切换到按方向生成的门边挂点。若后续策划改变门层级或增加 Collider，必须重新核对门侧切点和关节锚点；当前未发现缺失引用。
- 门边挂点来自可见门的最大 Trigger 边界，默认不启用；两绳最终摆幅、Solver 稳定性和切割顺序仍需 PlayMode 截图/录屏确认。

### 验证证据与回滚

- 后台 Unity Roslyn 定向及完整工程编译最新均为 `EXIT=0 / 0 errors`；日志：
  `BrainPunk/.codex-temp/Level005AuditPhysicsFinal2.log`、
  `BrainPunk/.codex-temp/Assembly-CSharp.Level005PhysicsFinal2.log`。
- 静态 Prefab 引用、关节 `autoConfigureConnectedAnchor=false`、新增运行时对象清理和 `git diff --check` 已核对；未启动 Unity/PlayMode，也未做 Android/iOS 实机验证。
- 回滚条件：若运行态出现门姿态异常、断口跳点或误切，恢复 `CutManager.cs` 本轮前版本；不回滚未改动的 Prefab、Level081 或共享代码。

## 2026-09-02：当前反馈修复风险复核

### 变更与缓解

- 本轮只修改 `CutManager.cs` 的私有运行时逻辑：开局到释放统一使用门边挂点（两个 edge 开关源码默认均为 `true`），并按当前门边几何重新设置初始 `SpringJoint2D.distance`，避免首刀时端点或绳长跳变。
- 切割候选改为真实相交、绳段内部、刀光路径顺序、距离的稳定优先级；一次手势仍只切一根。切口两侧各保留独立无碰撞动态端点和半绳，门侧线段提升渲染层级，避免中段切割被门 Sprite 遮成“整根消失”。
- 剩余支撑绳默认 `maxDistanceOnly=true`，释放后允许松弛；门的角速度在 `FixedUpdate` 做阻尼/刹车，`LateUpdate` 只清理求解器残余，保留有限摆动并降低持续自转风险。

### 仍需关注的风险

- 门边挂点相对 authored 门根会缩短每根绳的几何距离，虽然开局和释放已保持一致，最终两绳下垂位置、摆幅和美术观感仍依赖 Unity 2D Solver，当前为 `uncertain`。若策划只保留视觉 edge、关闭物理 edge，视觉与物理会按选项有意分离；需要改变 edge 配置时应成对调试并复测。
- Prefab 仍序列化 authored 门根端点和 `radius=0.5`；运行时覆盖由脚本完成。宽命中半径在低帧率或汇合点附近仍可能选中相邻绳，真实触控命中为 `uncertain`。
- 未启动前台 Unity，未执行 PlayMode、真实触控、不同切割顺序、长时间双绳摆动、Prefab 导入或移动端验证；`specs/037-remote-resource-loading/plan.md` 仍缺失，计划依据为 `uncertain`。

### 验证证据与回滚

- 定向 Roslyn：`.codex-temp/Level005AuditFinalFeedback2.log`，`EXIT=0`；全量 Assembly-CSharp：`.codex-temp/Assembly-CSharp.Level005FinalFeedback2.log`，`EXIT=0`、`0 errors`，工程既有 warning 数为 107。
- 静态 Prefab 核对保持 5/5 anchor、线段和 SpringJoint，所有 authored 连接体仍为门根；运行时切点无 `Collider2D`，新增关节关闭碰撞、自动锚点和自动距离；`git diff --check` 通过。
- 本轮源码 SHA-256：`B35CD6202CA1599D10D61AAD662847A5EE83A7C5D0E1B2E3AB705808F4AC8890`。
- 回滚仅恢复 `Assets/Game/Scripts/Level/MainLevel/Level_5/CutManager.cs` 本轮改动；不回滚 `Level_005.prefab`、Level081 或共享脚本。

## 2026-09-01：门边物理与切口同步复核

### 新增风险

- `useDoorEdgeAttachments` 当前默认关闭，只在门释放后且由 Inspector 显式开启时切换；这会让五根绳的物理力臂与可见门边一致，也会改变释放后的摆动幅度。当前没有前台 PlayMode 证据，实际姿态标记为 `uncertain`。
- `TryCutSegment`、`CutLine` 和未断绳视觉刷新共用当前门侧端点；若未来运行时替换门视觉层级或关闭几何 Trigger，需重新验证命中线与视觉线是否一致。
- 根端回退模式默认将释放质心置为 `(0,0)`，以排除 door_0 Trigger 偏移造成的恒定重力扭矩；若设计师显式填写非零 `releasedDoorCenterOfMass`，应重新检查持续旋转风险。

### 已完成检查

- 定向后台编译日志 `BrainPunk/.codex-temp/Level005AuditPostEdge.log`：`EXIT=0`；完整工程日志 `BrainPunk/.codex-temp/Assembly-CSharp.Level005PostEdge.log`：`EXIT=0`、0 errors、107 条工程既有 warning，Level005 私有脚本无新增 warning。
- `git diff --check`、Level005 Prefab 无差异、五个 authored 端点引用门根、运行时切点无 Collider、关节 `enableCollision=false` 均已静态核对。
- 未启动 Unity、未执行 PlayMode/真实触控/移动端验证；门停止自转、断口可见性和两绳物理观感仍为 `uncertain`。

### 回滚

仅恢复 `CutManager.cs` 的本轮修改；不回滚 Prefab、Level081 或共享脚本。

## 2026-09-01：剩余支撑绳默认张力模式（覆盖此前默认值描述）

### 风险与缓解

- 此前记录中“默认门边挂点”和“未断绳默认 `maxDistanceOnly=false`”已被当前实现覆盖：`useDoorEdgeAttachments=false`、`useDoorEdgePhysics=false`、`releasedRopesMaxDistanceOnly=true`。默认物理端点仍是 authored 门根，剩余绳只限制最大长度。
- 张力模式避免双绳定长约束的另一圆交点跳转，物理上允许门在重力下自然下垂；具体落点和摆幅依赖切割顺序、Solver 和帧率，尚未有运行态证据，保留 `uncertain`。
- 断口半绳继续使用定长关节以保持切口位置和可见长度；运行时切点无 Collider，所有新增关节 `enableCollision=false`、`autoConfigureConnectedAnchor=false`、`autoConfigureDistance=false`。
- 若策划需要门边视觉或物理端点，可在本关 Inspector 单独开启对应 edge 选项，并重新检查门边触发框偏移与绳长；不应直接修改共享关卡或 Level081 资源。

### 验证与回滚

- 静态检查：Level005 Prefab 无 diff；五根 authored 线端/关节仍连接门根；新增运行时对象及关节生命周期清理路径存在。
- 后台 Roslyn 定向和完整工程编译均 `EXIT=0`、`0 errors`；PlayMode、真实触控、长时摆动、Prefab 导入和移动端仍未验证。`specs/037-remote-resource-loading/plan.md` 缺失，按仓库规则标记为 `uncertain`。
- 回滚仅需恢复 `Assets/Game/Scripts/Level/MainLevel/Level_5/CutManager.cs` 本节改动；不回滚 Prefab、Level081 或共享脚本。

## 2026-09-02：门旋转回正与张力模式复核

### 风险与缓解

- `StabilizeReleasedDoorRotation()` 现在在限速/刹车之外施加可关闭的弱角回正力（默认 `0.5`，死区 `1` 度），目标是消除求解器偶发角速度累积；它不直接写入 `Rigidbody2D.rotation`，仍允许有限摆动。
- 未断支撑绳默认 `releasedRopesMaxDistanceOnly=true`，避免双绳定长约束跳到另一圆交点；断口半绳保持定长以保留切口。过强回正或过低/过高阻尼仍可能改变设计师预期，实际手感需 PlayMode 调参。
- 当前默认门边视觉/物理挂点均关闭（`useDoorEdgeAttachments=false`、`useDoorEdgePhysics=false`），降低门触发框偏移造成的端点突变；显式开启后必须重新检查门边几何。

### 验证与回滚

- 2026-09-02 定向/完整后台 Roslyn 编译均 `EXIT=0`、`0 errors`；Prefab 无 diff，静态关节和切点断言通过。
- PlayMode、真实触控、不同切割顺序、长时双绳摆动、Prefab 导入和移动端尚未验证，全部为 `uncertain`；`specs/037-remote-resource-loading/plan.md` 缺失亦为 `uncertain`。
- 回滚仅恢复 `Assets/Game/Scripts/Level/MainLevel/Level_5/CutManager.cs` 本节改动，不回滚 Level005 Prefab、Level081 或共享物理脚本。

## 2026-09-02：当前实现收尾审计

### 需求与缓解

- 本轮覆盖三项反馈：门释放后不应持续自转；中段刀口必须在实际切点保留钉子侧和门侧半绳；剩余两根绳应避免定长约束跳到另一圆交点。
- 当前旋转稳定在 `LateUpdate()`（物理关节求解后）执行，释放门仍可有限摆动；角阻尼、刹车、角速度上限、摆角上限和静止阈值均由本关私有字段控制，默认角冲量缩放为 `0`。这降低持续自转风险，但具体观感仍需 PlayMode。
- 切口使用同一输入采样的真实线段交点，单手势最多切一根；两侧半绳各自使用无 Collider 动态切点和精确长度 DistanceJoint，门侧克隆线提升排序层级以保留可见断口。
- 未断绳默认 `maxDistanceOnly=true`，edge 视觉/物理挂点默认均关闭；该组合避免释放瞬间缩短 authored 绳长，但也意味着默认两绳仍以门根为物理端点，若策划要求门边力臂需在本关 Inspector 显式开启并重新验证。

### 新验证证据

- 定向编译日志 `BrainPunk/.codex-temp/Level005AuditFinalCurrent2.log` 与全量编译日志 `BrainPunk/.codex-temp/Assembly-CSharp.Level005FinalCurrent3.log` 均 `EXIT=0`；全量为 `0 errors`、`107` 条工程既有 warning。
- 静态断言确认 authored Prefab 的 5/5 anchor、line、SpringJoint 和门端引用闭合；Level005 Prefab、Level5Anchor/TrackLine 无差异；运行时切点无 Collider；新增关节 `enableCollision=false`、`autoConfigureConnectedAnchor=false`、`autoConfigureDistance=false`。
- 当前 `CutManager.cs` SHA-256：`B4C6911A337C632218D459A70D300159E9182DB75FC3CC45A1D21605D75171AA`。

### 残余风险与回滚

- PlayMode、真实触控、不同切割顺序、长时间双绳摆动、Prefab 导入和移动端尚未验证，均为 `uncertain`；`specs/037-remote-resource-loading/plan.md` 缺失，计划依据为 `uncertain`。
- 若实际画面仍显示持续旋转或两绳姿态不符，应优先在本关 Inspector 调整私有阻尼/edge 选项；回滚仅恢复 `CutManager.cs`，不回滚 Prefab、Level081 或共享脚本。

## 2026-09-02：门背后绳端与 Reset 转场复核

### 新增风险与缓解

- 可见门端点现在固定为 authored 门根并使用源线排序层级，门 Sprite 会遮住进入门内部的末端；物理 edge 挂点仍可能改变门的受力摆幅，但不再改变可见线的位置。具体遮挡和端点观感尚未有 PlayMode 证据，标记为 `uncertain`。
- 断口端点的 `cutPointSettlementDamping` 同时作为 Rigidbody2D 阻尼下限，并由 `FixedUpdate()` 额外做指数速度衰减。阻尼过高会让切断后的半绳显得僵硬，过低仍可能出现长时间摆动，参数可在本关 Inspector 调整。
- 柔性曲线只改 `LineRenderer` 的多点路径，不改变 `DistanceJoint2D` 长度、端点位置或碰撞体；`Level5TrackLine` 仍会在 Update 写入两端，控制器在 LateUpdate 覆盖采样点。执行顺序和不同材质的曲线观感尚未在运行态验证，标记为 `uncertain`。
- Reset/Replay 转场不再调用 `RestoreAnchorState()`：旧场景销毁前 authored SpringJoint 和 LineRenderer 被禁用，避免出现可见的瞬间重连；普通 `OnDisable`/`OnDestroy` 仍恢复 authored 状态。若外部流程未来改为不销毁旧关卡，需重新验证该禁用态是否能正确交给后续初始化。

### 验证证据与回滚

- 定向日志：`BrainPunk/.codex-temp/Level005AuditCurrentFeedback.log`，`EXIT=0`；完整工程日志：`BrainPunk/.codex-temp/Assembly-CSharp.Level005CurrentFeedback.log`，`EXIT=0`、0 errors；工程既有 warning 未作为本轮失败。
- 静态核对：`CreateHalfLine(..., false)` 保持门侧半绳 authored 排序；`ResolveCutVisualDoorPoint()` 返回 authored 门根；`CleanupRuntime(false)` 只进入转场禁用分支；`git diff --check` 通过。
- PlayMode、真实触控、移动端、长时间双绳摆动、Prefab 导入仍未验证，均为 `uncertain`；`specs/037-remote-resource-loading/plan.md` 当前缺失。
- 回滚仅恢复 `CutManager.cs` 与本节文档，不回滚 `Level_005.prefab`、Level081 或共享脚本。

## 2026-09-02：断绳摆动速度调校复核

- 为缓解“轻飘、甩动很慢”，断口端点默认额外阻尼改为 `0.75`，切断分离冲量缩放改为 `0.22`，并增加 `cutPointGravityMultiplier=1.35`。这组值提高响应速度，同时保留每物理步速度衰减；过低阻尼或过高重力仍可能在极端切割顺序下产生较大摆幅，参数可在本关 Inspector 回调。
- 调整只在 `CutManager.cs` 的运行时切点创建和 `FixedUpdate()` 生效；没有修改 `Level_005.prefab`、`Level5Anchor.cs`、`Level5TrackLine.cs` 或共享物理资源。
- 定向日志 `BrainPunk/.codex-temp/Level005AuditCurrentFeedbackFinal.log` 与全量日志 `BrainPunk/.codex-temp/Assembly-CSharp.Level005CurrentFeedbackFinal.log` 均 `EXIT=0`、0 errors。PlayMode、真实触控和长时间摆动尚未验证，标记为 `uncertain`。
