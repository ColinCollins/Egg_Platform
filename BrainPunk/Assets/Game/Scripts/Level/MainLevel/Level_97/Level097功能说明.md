# Level097 功能说明

## 2026-10-10 Line Collider 拾取（当前方案）

### 修改前核对

- 授权与验收：用户建议在 Line 上加 Collider，并授权“怎么方便怎么改”。选择整条当前可见绳线作为起拖区；保留气球本体命中、60px 阈值、抓取偏移、轻点爆炸、门位补生、自由球重抓/绳尾吸附和载人球不可拖动。
- 原实现与来源：上一版 `IsBalloonPickHit` 只支持物理 Collider 外扩和 LinePoint 邻域；本轮将挂点邻域替换为 Line 的独立拾取 Collider，气球本体外扩参数保留。`BalloonSlot.line` 已显式绑定 `Level5TrackLine`；`TakeBalloonFromSlot` 单独克隆 Line GameObject，补生替换 StartPoint，自由球设置 StartPoint/LooseRopeEnd，载人设置角色端；输入只在按下时选球，后续不会重选。
- 最小改法：在本关三条已有 Line 上各增加一个四点矩形 `PolygonCollider2D`，槽位显式绑定 `linePickCollider`，运行时自由球从已克隆的 Line 同节点取得该组件；不新增节点、脚本、名字/层级搜索或修改共享 `Level5TrackLine`。控制器新增 `linePickWidth=0.52` 世界单位，按下查询前由当前两端构造矩形并 SetPath，复用四点数组，不增加逐帧更新。
- 物理与查询隔离：Collider 设 Trigger、排除全部接触层、较高层覆盖优先级、无力/接触捕获/回调层；Line 改为已有 Ignore Raycast 层（2），避免进入默认全局拾取。当前 Actor 地面/墙/推物掩码分别 328/64/256，均不包含层 2；当前 GameCamera 掩码包含全部层，本关与共享 Camera 源码未发现改写 cullingMask 的入口（其它关卡 Level40 的相机调整不属于本关）。保留原圆形 Collider 与所有物理 Cast/Distance/IgnoreCollision。不修改全局 Physics2D 配置。
- 状态边界：仍先拒绝补生、失活、禁用和销毁对象；Line 缺引用、端点为空、宽度为 0 或两端重合时拒绝 Line 拾取，不让旧顶点误命中，气球本体仍可按原范围命中。手动编辑 Polygon 顶点只影响编辑态预览，运行时按下将依据绳端及宽度更新；不新增 OnValidate/ExecuteAlways。
- 改动范围：仅 `Level97Ctrl.cs`、`Level_097.prefab`、本关功能/风险记录及当天记录；保留当前 Prefab 已有位置/绳线/嵌套 override。上一版新增参数注释仍写“LinePoint 邻域”，将因职责变为仅气球本体外扩而同步纠正；不改无关旧注释、共享脚本、存档/配置/打点、SDK、Packages 或 GF。
- 验证计划：逐块核对 Prefab 新组件、绑定、四点几何、Layer/接触过滤和原物理不变；提取真实方法模拟整条绳线、端点移动/切换、宽度、禁用/补生/销毁、选择顺序、抓取偏移和60px分流；独立完整 Unity Roslyn 编译。原生 Polygon 查询、Unity 接触/渲染、PlayMode 以及 Android/iOS/Profiler 未实测时标记 uncertain；CodeGraph CLI/MCP 不可用，引用 feature plan 仍缺失，以本关文档、源码及资产补足依据。

### 实现与验证结果

- 已实现三组 `BalloonSlot.linePickCollider` Inspector 显式绑定；独立 PolygonCollider 随 Line 克隆，自由球缓存其直接组件引用。命中分流为气球 Collider 内部 → 本体 `balloonPickPadding` 外扩 → `IsLinePickHit`。Line 矩形总宽 `0.52`，端点沿绳方向各延伸半宽 `0.26`，覆盖整条绳线和底部挂点；不改变可见 LineRenderer 宽度。仅按下且需要检测 Line 时 SetPath 和 SyncTransforms，复用四点数组，无新增 Update/LateUpdate、节点、脚本或全局事件。
- 参数位置：`Level_097.prefab → Level97Ctrl → 点击与拖拽`；`Line Pick Width=0.52` 控制整条绳线的世界总宽，`Balloon Pick Padding=0.26` 仅控制本体外扩，`Drag Threshold Pixels=60` 保持原分流。Line 宽度为 0 只关闭 Line 拾取；两项范围参数都为 0 才回到仅物理 Collider 命中。运行时不以手动 Polygon 顶点为权威，而按当前端点/宽度更新，因此不要用 Edit Collider 调整运行时检测宽度。
- 行为：门位或自由球可按住绳中段/挂点起拖，沿用原偏移而非把气球中心移到手指；未到阈值松手仍播放点击爆炸。载人球整条线也加入其优先命中，但依旧不能被拖走；滑动达到阈值只抑制轻点。补生/失活/禁用/销毁拒绝、绳尾对 Boxy 的吸附规则以及物理升降/顶墙不改。
- 隔离测试：提取当前与本轮前的真实源码方法，使用简化圆形/四边形几何和副作用桩，`178` 项断言通过；包含三槽挂点、绳段 0/20/50/80/100% 采样、半宽内外、绳中段起拖/补生/松手、59/60px、偏移、宽度关闭/缩小、旋转/缩放坐标、禁用/补生/销毁、自由球重抓、载人不可拖动、自由绳端/角色端切换、重合/缺端点拒绝旧形状、原倒序。`73` 个非目标私有方法逐字一致。新增中段起拖样本使用明确超过阈值的 61px：初次用浮点坐标加60造成实际差值略小于60，已修正测试输入并保留原阈值及59/60样本，没有为测试改变生产分流。
- 编译：Unity `6000.0.59f2` 当前 Bee 响应文件与自带 Roslyn 完整主程序集独立输出到临时目录，`0 error / 113 warning`，本关无诊断；未覆盖正式 Bee DLL。验证目录 `C:/Users/admin/AppData/Local/Temp/brainpunk-level097-line-collider-20261010/` 包含 `BeforeCtrl.cs`、`verify-line-collider.ps1`、`verify.log`、`compile.log` 和 `prefab-verification.json`。
- Prefab 静态复核：`257` 个唯一定义、`565` 个非零纯本地引用，无重复或缺失；新增 Collider fileID 为 `797000000000000020/021/022`。几何使用已绑定 Nail LinePoint 与嵌套门根 override 解析后的两端构造，不修改用户绳线坐标。去除本轮三组组件/绑定/Line Layer 与宽度参数后，原文件正文一致；三个半径 `0.6` 的 Circle、刚体/关节/Actor、共享资源及用户原有摆放保持。首轮读取的输出预算不足导致检查材料截断，扩大预算重新读取后复核通过；未据截断结果改写资源。
- `uncertain`：尚未执行 Unity PlayMode、原生 Polygon OverlapPoint/接触隔离、手机触控、渲染或 Profiler；桩测试不证明原生物理。手工验收：等待编译、重新进入关卡，分别从三条绳中段和底部起拖；检查本体轻点、补生中拒绝、自由球重抓/吸附、载人点击及不可拖动、顶墙、暂停/重开、绳线可见、调宽后的手感和门附近选择。三条线汇聚门端或与载人线重叠时仍按原载人优先/倒序选球，不保证该区域能区分每一条重叠绳线。
- 局部回滚：仅关闭 Line 拾取可将宽度设为0或禁用其 Collider；完全撤回本轮则移除三组新 Polygon 和槽位绑定、恢复三条 Line Layer 为0、删除宽度/数组/Line helper/克隆缓存，并把上一版命中及新增参数说明恢复。保留原气球位置、绳线与嵌套 override，不整份还原 Prefab，更不删除关卡。
- API 依据：[Unity PolygonCollider2D.SetPath](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PolygonCollider2D.SetPath.html)、[Collider2D.OverlapPoint](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Collider2D.OverlapPoint.html)、[Collider2D.excludeLayers](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Collider2D-excludeLayers.html)。实际功能依据仍是本关代码、Prefab 与上述验证，文档不替代运行实测。

## 2026-10-10 点击区域延伸到 LinePoint

> 本节保留上一版变更和验证历史；当前挂点邻域拾取已由上方 Line Collider 方案取代。

### 修改前核对

- 需求与基线：用户要求扩大逻辑点击区域，气球底部 `LinePoint` 也能作为拖拽起点。原 `FindHitSlot` / `IsBalloonHit` 仅用物理 Collider 的 `OverlapPoint`；当前 Prefab 三个圆形碰撞半径均为 `0.6`。本次只扩大拾取范围，不扩大物理碰撞体，不把整条绳子纳入命中。
- 来源与原流程：`Level97Ctrl.TryBeginPointer → FindHitSlot / FindHitDetachedBalloon / IsBalloonHit → ContinuePointer → BeginDrag / EndPointer`；`Level5TrackLine.GetStartPoint()` 返回 Inspector 已绑定的绳起点。Prefab 三个起点均为气球 Nail 下的 `LinePoint`，不是门端 `EndPoint`，也不是用于吸附角色的运行时 `LooseRopeEnd`。
- 生命周期：门位补生时绳起点临时切到补生视觉的 RopePoint，但该槽位由 `slotRegrowing` 拒绝拾取；完成后恢复原 LinePoint。自由球在创建时显式设置自身 LinePoint，载人只替换绳终点。沿用这些引用，不增加按名字或层级查找。
- 拟修改：仅本关控制器增加 `balloonPickPadding=0.26`（世界单位），统一检测 Collider 内部、Collider 边缘外扩和当前 LinePoint 周围同半径区域，并在本关 Prefab 显式保存数值。载人优先、自由球倒序、门位倒序的选择顺序保持；新增范围可能与邻球重叠，仍按原顺序选取，不擅自改为最近球。
- 必须保留：`60px` 拖拽阈值、指针相对刚体偏移、轻点爆炸、门位拖走补生、自由绳尾 `0.05` 吸附、自由态/载人态物理、载人球不可拖动、暂停/重建/销毁门禁。用户已有气球摆放和绳线等 Prefab 差异不覆盖；共享脚本、配置、存档、事件、打点、Packages/GF 不改。
- 验证计划：按三槽当前序列化几何检查 LinePoint 与邻域命中；提取实际方法做命中、禁用/补生/销毁、选择顺序、拖拽阈值与偏移的隔离测试；独立输出完整 Unity Roslyn 主程序集；对比本次前后 Prefab 仅增加一项参数。Unity PlayMode、Android/iOS 手感仍需实际运行确认。
- 环境边界：引用的 `specs/037-remote-resource-loading/plan.md` 缺失；存在 `.codegraph` 但 CLI 与 MCP 不可用，以已读本关文档、源码和 Prefab 补足。无依赖上述缺口的资源或远端行为修改。

### 实现与验证结果

- 已实现：`FindHitSlot` 与自由/载人球的 `IsBalloonHit` 共用 `IsBalloonPickHit`。先保留原 Collider 内部命中，再用 `ClosestPoint` 检查边缘外扩，最后检查 `GetStartPoint()` 周围 `0.26` 的圆形区域；只在指针按下选球时查询，不新增逐帧扫描或 Collider。
- 门位球和自由球可从底部 LinePoint 起拖；原抓取偏移被保留，不会把球中心瞬移到手指。未达到 `60px` 松手仍是轻点爆炸，因此 LinePoint 附近轻点也会爆炸。载人球沿用原规则：轻点可戳破，达到阈值只抑制轻点，不允许拖离角色。
- 当前 Prefab 的三个 LinePoint 世界坐标（仅资产层级几何，不代表场景运行位置）为 `(6.16, 0.156)`、`(4.60, -0.034)`、`(7.8096, -0.1899)`；原半径 `0.6` 的命中均不包含挂点，新逻辑均包含且本次样本未串槽。本次前后 Prefab 去除新增 `balloonPickPadding: 0.26` 后全文一致，保留用户已有摆放/绳线/嵌套 override 差异。
- 隔离测试：从修改前后真实源码提取命中和指针方法，配合二维圆形几何及副作用桩完成 `85` 项断言；覆盖三槽挂点及邻域、区域外不命中、禁用/失活/补生/销毁拒绝、自由球重抓、轻点/取消、`59px/60px`、抓取偏移、拖后回原点不误爆、载人不可拖动、当前绳起点读取、范围重叠时原倒序。`74` 个非目标私有方法逐字比较一致，包含物理/吸附/补生/清理及输入生命周期。桩测试不证明真实 Unity 物理或触控手感。
- 编译：使用 Unity `6000.0.59f2` 当前 Bee `Assembly-CSharp.rsp` 与自带 Roslyn，独立输出主程序集到临时目录，`0 error / 113 warning`，Level97 无诊断；未改 Bee 正式 DLL。脚本与日志：`C:/Users/admin/AppData/Local/Temp/brainpunk-level097-pick-20261010/verify-picking.ps1`、`verify.log`、`compile.log`。首轮测试桩因未赋值字段警告终止，调整临时测试的警告处理后重跑通过，未因此修改生产逻辑。
- `uncertain`：未执行 Unity PlayMode、Android/iOS 或 Profiler。手工验收请重新进入 Level097，分别按住三球底部 LinePoint 移动超过 `60px`，确认门位补生、自由球重抓、绳尾吸附、顶墙行为、暂停/重开和轻点爆炸；邻球动态靠近后的选择手感仍须确认。若区域过宽，先在本关 Inspector 调小 `balloonPickPadding`，不调整物理半径。
- 局部回滚：把本关 `balloonPickPadding` 设为 `0` 即恢复原命中；完整撤回只还原这两个命中方法、删除新增 helper/参数及本关 Prefab 这一项参数。不要整份还原 Prefab，以免覆盖用户已有位置改动；不需要删除关卡或修改共享模块。

## 2026-08-18 黄色范围脚本精简

- 背景：删除无调用私有逻辑并合并重复生命周期清理，保持气球、载人、补生和输入行为不变。
- 影响文件：仅 `Level97Ctrl.cs`。`OnDisable()` / `OnDestroy()` 统一调用 `CleanupRuntimeState()`；删除仓库内零引用且从未调用的私有 `StopFindJumpButton()`。
- 前后差异：事件解绑、状态协调停止、指针取消、视觉恢复、气球释放/销毁和物理恢复的顺序不变；寻找跳跃按钮的现行启动/收口逻辑未修改。
- 验证：仓库源码与资产字符串检索未发现被删方法引用；`Assembly-CSharp.csproj` 编译通过（0 错误）。
- 回滚：恢复私有方法并把共享清理体分别展开即可；Prefab、配置和共享 Actor 无需回滚。

更新时间：2026-08-13

## 需求背景

Level097 以常规关卡 Level011 的场景、门和标准通关链为基础。气球的悬挂物理参考 `DLevel_045`：门位气球持续上浮并受最大绳长限制。轻点门位气球仍播放爆炸并从门后补生；拖走门位气球时，手中的气球成为独立自由球，原门位立即补生同款气球。自由球拖空松手后不回门，而是继续上浮并可再次拖拽或戳破；把自由绳尾拖到 Boxy 身上才会载人上升。载人时角色可左右移动但不能跳跃，戳破载人气球后气球真实销毁，Boxy 恢复重力下落。

## 影响范围

- `Assets/Game/Prefabs/Level/Level_097.prefab`
- `Assets/Game/Scripts/Level/MainLevel/Level_97/Level97Ctrl.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_97/Level97ActorCtrl.cs`
- 复用 Level011 内原有 `Knock_Explosion` 模板、`ballonExplosion` 音效、门与标准成功链。
- 只读参考 `Assets/Game/Prefabs/DrawLevel/DLevel_045.prefab` 的气球物理参数。

没有修改 Level011、DLevel045、共享 `ActorCtrl`、共享 `GamePlayPanel`、门脚本、粒子 Prefab 或音频资源。
Level097 保留启用的横向顶部 `AirCollider (1)`，另一个倾斜 `AirCollider` 维持复制场景当前的未激活状态。载人气球必须与 `AirCollider (1)` 真实碰撞：气球顶到墙后限制气球与 Boxy 继续上升，但 Boxy 仍可左右移动。没有关闭气球 Collider，也没有忽略该碰撞或修改 Level011。
气球与门的碰撞则在本关局部显式忽略：控制器从气球关节连接的门刚体层级收集门 Collider，并对门位气球和运行时自由球逐对调用 `Physics2D.IgnoreCollision`，不改全局 Layer 矩阵。

## 原有行为基线

- Level011 的三个气球各自通过 `ClickItemExecutor` 处理点击爆炸，关卡继续使用 `BaseLevelCtrl`、原门 Trigger、`SuccessAnimCtrl` 和 `EnterDoorAnimCtrl`。
- `DLevel_045` 的气球 Nail 使用 `gravityScale=-0.6`、半径 `0.6` 的 `CircleCollider2D` 和 `Max Distance Only` 的 `DistanceJoint2D`。
- Level097 复制场景时保留了 Level011 的 Actor、门、失败区、进出门动画和 UI 配置。

## 当前行为

### 气球物理

- 三只气球运行时均为 Dynamic，`gravityScale=-0.6`，锁定 X 与旋转。
- 每只气球使用半径 `0.6` 的圆形碰撞体，并通过只限制最大长度的 `DistanceJoint2D` 连接门。
- 物理关节锚点按当前可见绳两端对齐，不修改策划在 Prefab 中摆放的 Transform。

### 点击爆炸与补生

- 按下到松手的屏幕位移小于 `60px` 时按点击处理：在当前气球位置播放原爆炸模板和 `ballonExplosion`；需要更明显的位移才会进入拖拽。
- 轻点门位气球时，同帧隐藏真实气球 Sprite；刚体、碰撞体、物理关节和浮力继续运行，不因表现中断。
- 临时 Sprite 在 `0.65s` 内从该气球原门端绳点平滑移动并缩放到真实气球当前位置。原 LineRenderer 从补生第一帧起就改接到临时气球底部挂点，并随临时气球逐帧更新，不再等球长完后才突然接线；结束或中断时先恢复原绳端，再销毁临时对象。
- 临时 Sprite 使用真实气球同一 Sorting Layer，排序值为 `99`，低于门根 `SortingGroup` 的 `100`，因此从整扇门后出现；真实气球排序 `105` 不变。
- 拖走门位气球时会立即启动同一套门后补生，但手中的独立自由球不会被补生动画回收。轻点自由球会真实销毁该球；轻点载人球还会先解除载人并恢复 Boxy 重力和跳跃。

### 拖拽与载人飞行

- 屏幕位移达到 `60px` 后才进入拖拽，拖拽松手不再触发点击爆炸。
- 门位气球、拖出的自由球以及补生后再次出现的门位气球都不与门的物理 Collider 发生碰撞；该忽略关系在初始化、重新启用和槽位物理恢复时重应用，销毁自由球时对其逐对恢复。
- 拖拽期间临时关闭浮力和门关节，可视绳不再连接门，而是从气球底部向下垂出 `1.8` 世界单位。拖空松手后自由球恢复 Dynamic 与 `gravityScale=-0.6`，不会瞬移或回到门位；自由态临时忽略顶部 `AirCollider (1)`，因此会继续向上飞出场景，离场前仍可再次拖拽或轻点戳破。
- 吸附只读取 Boxy 根节点上启用的主碰撞体，不读取失活的 `pushCollider`。自由绳尾进入 Boxy 主碰撞体，或距离其边缘不超过 `0.05` 世界单位时才吸附；气球本体不参与吸附判定。拖动刚体后会在同一帧同步可见绳线，保证玩家看到的线尾与松手判定点一致。
- 同时最多一只气球载人。Boxy 已被带飞时，其他门位球和自由球仍可拖拽或戳破，但第二只球不会抢占载人状态。
- 自由球接触 Boxy 并吸附时会先恢复与顶部 `AirCollider (1)` 的碰撞，再进入载人运动；只有自由态忽略顶部墙，载人态始终受墙阻挡。
- 载人气球中心保持在角色上方 `(0, 3.2)`，气球底部到角色顶部约保留 `2.0` 世界单位可视绳长，避免气球遮住 Boxy。
- 载人气球每个物理帧先保持在 Boxy 上方，再用自身圆形 Collider 向上做无分配形状扫描；命中顶部 `AirCollider (1)` 时，气球与 Boxy 的共同上升距离会被限制到墙下 `0.02` 世界单位。接触后两者 Y 速度归零，不会因 Kinematic 位置跟随而穿墙。
- 如果玩家贴近顶部完成吸附，气球首帧已经与墙轻微重叠，则气球与 Boxy 会作为整体沿墙外法线退回墙下，避免卡墙抖动。
- 未碰墙时角色与气球共用 `2.5` 的竖直上升速度。角色的 X 速度仍由原 `ActorCtrl.OnUpdate()` 处理，因此空中和顶墙后都保留左右移动。
- `Level97ActorCtrl` 在载人状态拦截所有 `JumpButton()` 调用；HUD 跳跃按钮同时变为不可交互，失败恢复时还原原状态。
- 轻点载人气球会播放爆炸并真实销毁该运行时气球，清除载人状态、恢复 HUD 跳跃按钮和 Boxy 原重力，Boxy 随即下落；门位补生不受影响。

### 暂停、失败与通关

- 新实例默认锁定本关原始鼠标/触摸输入；只有现有状态事件明确切到 `PLAYING`，或 `GameResumeEvent` 后延迟一帧确认 FSM 实际处于 `PLAYING`，才会开放。这样首次加载、失败重开和 Replay 能正常恢复，同时退出 PAUSE 转去 FAILED 时不会误点气球。
- 暂停或离开 PLAYING 状态时取消正在进行的拖拽、将已拖出的球恢复自由物理并冻结载人速度；补生协程等待恢复后继续。
- 失败、角色死亡、Disable 或 Destroy 时恢复角色重力、跳跃按钮、气球初始物理和可见状态。
- Actor 进入原门 Trigger 后仍由 Level011 的标准 `BaseLevelCtrl` 成功链结算；本关控制器只停止载人速度，不替换成功演出、存档或打点。

## 运行流

```text
按下气球
  -> 门位球位移不足 60px 后松手：爆炸表现 -> 隐藏真实 Sprite -> 临时 Sprite 带原绳线从门后平滑长回 -> 恢复真实 Sprite/原绳端
  -> 自由球位移不足 60px 后松手：爆炸表现 -> 真正销毁自由球
  -> 载人球位移不足 60px 后松手：爆炸表现 -> 真正销毁载人球 -> Boxy 恢复重力下落
  -> 位移达到 60px：进入拖拽
       -> 从门位拖走：生成独立自由球 -> 原门位立即从门后补生
       -> 自由绳尾未接触角色：恢复自由浮力并继续上浮 -> 仍可重抓或戳破
       -> 自由绳尾接触角色：吸附到角色顶部 -> 角色/气球等速上升 -> 气球顶墙后共同停止上升 -> 角色仍可左右移动、不可跳跃
            -> 进入原门 Trigger：标准通关链
```

## 数据、资源、UI 与事件

- 不新增 DB 字段、存档、全局事件、运行时资源地址、analytics、广告、IAP、RemoteConfig 或平台 SDK 调用。
- 复用当前关卡 Prefab 内的爆炸模板，补生 Sprite 由当前气球 Sprite 临时创建并在完成后销毁。
- 顶部阻挡使用当前载人气球 Collider 的无分配 `Cast` 和 `Distance` 查询；复用固定命中数组，不新增碰撞事件、Layer 或物理矩阵配置。运行时自由球与来源门位初始重叠时，仅临时忽略这一对 Collider，分离、销毁或清理时恢复；气球与门的碰撞忽略覆盖门刚体层级下的 Collider，顶部空气墙从不被忽略。
- 只订阅现有 `SwitchGameStateEvent`、`GamePauseEvent`、`GameResumeEvent`、`GameResetEvent` 和 `GameReplayEvent`，用于冻结、恢复及在重建请求同帧封闭本关局部输入；没有新增或修改全局事件。
- 当前工作区生成配置已存在唯一 `10097 / Level_097`，`tblevelsort.json` 当前排序项为 `id=99, path=10097`。两份源 Excel 和生成 JSON 均已有工作区改动，本次没有擅自重写或重新生成配置，其来源和最终排序仍需配置流程复核。

## 当前参数

| 项目 | 当前值 |
| --- | --- |
| 气球浮力 | `gravityScale=-0.6` |
| 气球碰撞半径 | `0.6` |
| 拖拽阈值 | `60px` |
| 气球本体拾取外扩 | `0.26` 世界单位；`0` 只关闭本体外扩 |
| Line Collider 总宽度 | `0.52` 世界单位，两端各延伸半宽；`0` 关闭整条绳线拾取 |
| 自由绳尾吸附额外容差 | `0.05` 世界单位 |
| 拖空松手 | 恢复 Dynamic / `gravityScale=-0.6`，自由态穿过顶部墙飞走 |
| 自由垂绳长度 | `1.8` 世界单位 |
| 补生时长 | `0.65s` |
| 补生渲染排序 | `SceneLayer / 99`（门根为 `100`） |
| 载人上升速度 | `2.5` |
| 气球相对角色偏移 | `(0, 3.2)` |
| 绳端相对角色偏移 | `(0, 0.55)` |
| 顶墙安全间距 | `0.02` 世界单位 |

## 验证证据

- 使用 Unity 6000.0.59f2 当前 Android Bee `Assembly-CSharp.rsp` 与 Unity 自带 Roslyn，将完整 `Assembly-CSharp` 输出重定向到系统临时目录后隔离编译成功：`0 error / 104 warning`；104 条均为工程既有 warning，Level097 自身诊断为 `0`。临时 DLL、PDB 与响应文件均已删除，没有启动新 Unity 实例。
- 静态核对 `EnterLevelLoading`、`PlayCtrl.ResetGame/ReplayGame` 与 `PlayCtrl_Pause`：新关卡实例可能先于 PAUSE/PLAYING 事件建立，因此 Level097 输入门控初值为锁定；`GameResumeEvent` 在 PAUSE 退出时派发，Level097 会延后一帧核对真实 FSM，只有 PLAYING 才恢复。
- `GameResetEvent` 与 `GameReplayEvent` 会在等待旧关卡销毁的黑幕期间同帧取消拖拽并清零载人速度，避免旧实例继续响应原始输入。
- Prefab 静态审计为 `254` 个对象定义、`254` 个唯一 fileID、`556` 次非零本地引用、`0` 个重复定义、`0` 个悬空本地引用；`carryingCeilingCollider` 精确绑定到启用的 `AirCollider (1)` 的 `BoxCollider2D` fileID `5657534105333743630`。
- 三个气球槽位的 `root/body/collider/doorJoint/line/spriteRenderer` 均为有效本地引用；三组旧 `ClickItemExecutor` 均已禁用。
- Actor 已同时改绑到 `BaseLevelCtrl.actor`、`EnterDoorAnimCtrl.actor` 和 `ActorSkinBinding.actor`，三处均指向同一个 `Level97ActorCtrl`。
- 两份新脚本 GUID 与 Prefab 引用一致；`DLevel_045` 的 `-0.6 / 0.6 / Max Distance Only` 参数已直接核对。
- 静态状态机核对覆盖门位轻点、门位拖走立即补生、自由球拖空、自由球重抓/戳破、自由绳尾接触吸附、载人球戳破恢复重力，以及快速点击/触点取消不串槽。
- 针对“把气球的线拖给 Boxy 无法吸附”的回归，已静态确认 `CanAttachToActor` 读取运行时 `LooseRopeEnd.position`，不再读取气球本体 `Collider.Distance(actorMainCollider)`；拖动路径也会在移动刚体和 `Physics2D.SyncTransforms()` 后立即刷新同一个线尾 Transform 的可见位置。
- 三条初始绳线均为“气球底部 LinePoint -> 门根”；补生时起点临时切到补生球底部且第一帧先同步 LineRenderer，自由球切到运行时自由绳端，吸附切到角色顶部运行时节点。补生起点固定读取缓存的原门端，不会被自由球状态污染。
- Prefab 序列化拖拽阈值为 `60`，与 `Level97Ctrl.dragThresholdPixels` 默认值及 Level075 的本地拖拽阈值一致；静态核对显示当前门根 `DoorOutside` 及其子层 Collider 已登记，并对三只源球和运行时自由球应用门碰撞忽略。
- 嵌套门源 Prefab `Assets/Game/Prefabs/LevelItems/door_0.prefab` 的根 `SortingGroup` 与门可见 Renderer 均为 `SceneLayer / 100`，补生临时 Sprite 为同层 `99`；三只真实气球仍为 `105`。
- Level097 中 `AirCollider (1)` 及其 `BoxCollider2D` 均启用，另一个倾斜 `AirCollider` 未激活；源码不再包含载人碰撞忽略逻辑，而是按气球 Collider 到目标墙的形状扫描距离限制整组上升，未修改 Level011 Prefab、Layer 或全局碰撞矩阵。

## 未验证项

- 尚未执行 Unity PlayMode，补生球与绳线从第一帧同步出现、门后遮挡、`0.65s` 节奏和连续点击手感为 `uncertain`。
- `60px` 拖拽阈值、`1.8` 自由绳长度、自由绳尾 `0.05` 吸附容差、拖空上浮、自由球重抓/戳破、载人球戳破下落、载人间距和双端视觉绳为 `uncertain`；门碰撞忽略已完成代码/Prefab 静态核对，但实际物理手感仍待 PlayMode。
- 等速上升、空中左右移动、所有跳跃入口禁用、进入门 Trigger 后的完整成功演出为 `uncertain`。
- 载人气球顶住 `AirCollider (1)` 后整组停止上升、顶墙时左右移动、贴墙吸附去穿透、低帧率不穿墙，以及离开墙边后的行为为 `uncertain`。
- 暂停、首次入场门控、失败重开、Replay、退出、Android/iOS 横屏多指触控、60 fps 和实例化峰值为 `uncertain`。
- 配置源 Excel 中 10097 的最终排序和文案有效性尚未通过本轮隔离生成复核。

## 回滚路径

删除 Level097 私有控制器与 Actor 子类，移除 Prefab 根上的 `Level97Ctrl`，恢复 Actor 的原共享组件和三处引用，并恢复三个原 `ClickItemExecutor`，即可回到 Level011 复制态。Level011、DLevel045、共享 Actor、GamePanel、粒子、音频和标准门结算链无需回滚。
