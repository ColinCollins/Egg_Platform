# Level097 风险评估

## 2026-10-10 Line Collider 方案风险复核（当前）

- **基线与授权**：上一版仅本体外扩和 LinePoint 邻域命中；用户建议加 Line Collider 并授权选择最方便的改法。本轮有意扩大到整条当前可见绳线；只改本关控制器与 Prefab，复用原输入/补生/载人/吸附/清理，不修改共享 Level5TrackLine、Actor、配置、存档、事件、打点或 SDK。上一版说明保留为历史，不作为当前命中边界。
- **事实**：三条 Line 显式绑定独立四点矩形 PolygonCollider；气球物理半径仍0.6。Line Pick Width=0.52（两端各延伸半宽），本体外扩0.26单独保留；查询前同步当前 StartPoint/EndPoint，而非持续重建。新 Collider 为 Trigger，excludeLayers=全部、优先级1（本关原 Collider 为0）、力/捕获/回调层0，Line 位于 Ignore Raycast（2）；Actor 三种场景查询掩码均不包含2，当前 Camera 掩码包含2。

| 风险与影响链 | 当前控制与证据 | 验证边界 |
| --- | --- | --- |
| Line 新形状参与门/角色触发或影响气球物理 | 静态核对 Trigger、全部接触排除及优先级/回调过滤；保留原圆形 Collider 和 Cast/Distance/IgnoreCollision | 原生接触、真实顶墙和触发回调未实测，uncertain |
| 新 Collider 被默认全局查询识别、绳线不可见 | Line 使用层2；本关Actor查询掩码328/64/256不含2，Camera源掩码为全层且本关未改cullingMask | 显式 AllLayers 查询仍可能看到它；实际渲染与其它运行时查询待验 |
| 补生或自由球/载人端点切换后沿旧线误命中 | 门位先拒绝补生；Line 查询前读取当前两端、SetPath、SyncTransforms；宽度0、缺端点、近零长度拒绝，不查询旧顶点 | 几何桩断言覆盖，原生Polygon查询与Unity对象销毁语义未代验 |
| 点绳时球中心跳到指针或拖后误爆 | 原抓取偏移、60px分流、点击/拖拽互斥方法不改；绳中段起拖、补生和松手测试通过 | Unity触控、低帧率及移动端待验 |
| Line 缺绑定或克隆引用错位 | 三槽 Inspector 显式绑定；自由球从已克隆Line同节点取得Polygon，不新增名字/层级搜索；缺Line引用时本体拾取仍保留 | Prefab纯本地引用闭合；Unity实际克隆/导入需PlayMode |
| 门端汇聚或载人线覆盖其它球时选错目标 | 沿用载人优先、自由球/门位倒序，未擅自更改选择策略；允许本轮新增整线命中 | 汇聚区域确实存在多线覆盖，用户实际选择手感为uncertain |
| 频繁形状更新带来开销 | 仅按下选球且本体未命中时计算/SetPath；四点数组复用，不增加逐帧任务/节点/脚本 | 原生SetPath、全局SyncTransforms和多自由球成本需Profiler |

- **验证证据**：178项真实方法+二维几何/副作用桩断言通过，73个非目标方法逐字相同；独立Unity Roslyn主程序集0 error / 113 warning，本关无诊断。Prefab257个唯一定义、565个非零纯本地引用、无重复/缺失，三组绑定、初始四点、隔离标志及原物理已静态核对；剥离本轮改动后原正文一致，保留用户原有位置/绳线/override。目录 `C:/Users/admin/AppData/Local/Temp/brainpunk-level097-line-collider-20261010/`，详见本关功能说明；初次读取材料截断已重新读取，浮点中段测试输入已修正，未因此改生产逻辑。
- **假设与未知**：本轮按用户授权选择整条绳线可起拖，轻点仍爆炸、载人仍不可拖动；不扩大绳尾吸附范围。没有运行PlayMode、移动端、原生Collider或Profiler验证；不得把编译/几何桩通过视为实际玩法/物理/渲染通过。CodeGraph不可用、feature plan缺失，本关源码/文档/资产提供局部依据，不扩大其它模块。
- **回滚**：宽度设0可关闭Line拾取；两项范围参数均0恢复仅气球物理命中。完全撤回仅移除本轮Polygon、绑定、Line层2和新方法/宽度/克隆缓存，恢复上一版挂点邻域逻辑；不得覆盖用户原Prefab摆放，更不删除本关。

## 2026-10-10 LinePoint 拾取扩展风险复核

> 以下为上一版历史证据，当前范围和隔离措施以上方 Line Collider 章节为准。

- **原行为与范围**：原点击只按气球物理 Collider 命中；本次仅修改 `Level97Ctrl` 的门位/自由/载人球命中方法，增加世界距离参数 `balloonPickPadding=0.26`，并在 `Level_097.prefab` 保存该值。复用 `Level5TrackLine` 已绑定起点，不改该共享组件。半径仍为 `0.6`，吸附仍按自由绳尾与角色 `0.05` 容差；用户原有 Prefab 差异保留。
- **事实与直接结论**：统一命中覆盖物理 Collider 内部、边缘外扩和底部 LinePoint 邻域；检查 Collider 启用及 activeInHierarchy，门位先拒绝补生，运行时球先拒绝销毁。补生临时起点不会通过门位拾取入口；载人仅换绳终点，因此新增命中不读取门端或角色端。

| 风险 | 控制与验证证据 | 未验证边界 |
| --- | --- | --- |
| 扩大物理半径影响顶墙与球间碰撞 | 不改半径、刚体、Cast、Distance、碰撞忽略；本次前后 Prefab 仅新增拾取参数，74 个非目标方法逐字相同 | 原物理的 Unity 运行表现未代验 |
| 新范围串到邻球 | 载人优先、自由球倒序、门位倒序保持；当前三槽 LinePoint 均选自身，重叠样本沿用倒序 | 动态靠近时可能按原优先级选到另一球，需手感验收 |
| 补生动画的临时挂点可误拖 | `slotRegrowing` 在查询前拒绝槽位，补生方法未改；隔离测试覆盖该拒绝 | PlayMode 快速连续操作待验 |
| 点击挂点产生非预期爆炸 | 挂点只是扩大原拾取起点，未达到 60px 松手仍沿用爆炸；保留原点击/拖拽互斥 | 用户实际手指抖动与分辨率待验 |
| 起拖时气球瞬移到指针中心 | `TryBeginPointer` 仍缓存 body.position - worldPosition；隔离测试覆盖挂点偏移、59/60px 和拖后回原点 | 移动端触控与物理同步待验 |
| 载人球被新增命中拖离角色 | 原载人专用 ContinuePointer 分支未改；隔离测试确认不移动、不释放、不误爆 | 载人轻点/顶墙/多球并行需 PlayMode |
| 禁用/销毁对象或自由绳尾误命中 | 保留对象状态门禁；只读取 GetStartPoint，不读取 EndPoint/LooseRopeEnd；覆盖禁用、失活、销毁及挂点外样本 | Unity Object 销毁语义与生命周期需运行确认 |
| 扩展检测新增常态开销 | 仅原指针按下选球路径执行 ClosestPoint 与平方距离计算，不新增 Collider、对象、事件或逐帧扫描 | Profiler 未执行，移动端成本为 uncertain |

- **验证**：真实方法与简化圆形几何/副作用桩的 `85` 项断言通过；当前资产有 `254` 个唯一定义，三槽显式 StartPoint 绑定及物理半径已核对。独立 Unity Roslyn 主程序集编译 `0 error / 113 warning`，Level97 无诊断。证据位于 `C:/Users/admin/AppData/Local/Temp/brainpunk-level097-pick-20261010/`；桩测试与编译不等于 PlayMode 或真机验证。
- **假设与缺失证据**：产品范围按气球底部 LinePoint 邻域处理，不把整条绳子纳入拖拽；未新增载人拖动能力。尚未执行实际三球点按、自由球重抓/吸附、补生连续点击、暂停/重开、Android/iOS 横屏多指和动态重叠手感；这些均为 `uncertain`。feature plan 缺失、CodeGraph 不可用，源码与 Prefab 已补足本次局部依据，无关键设计未决项。
- **回滚**：设拾取参数为 `0` 可关闭扩展；若完整撤回，只恢复本次命中代码并删除新增参数及 Prefab 单项，不覆盖原有摆放等差异，不删除本关或修改物理/共享流程。

## 2026-08-18 行为等价精简风险复核

- **事实**：两个生命周期回调的清理体相同；`StopFindJumpButton()` 在本仓库 C#、Prefab/UnityEvent 和文本资产中均无调用证据。
- **直接结论**：清理顺序和气球状态恢复合同不变；删除的方法不会改变当前源码可达路径。
- **假设**：不假定仓库外部二进制或运行时反射通过字符串调用该私有方法；当前无此证据。
- **缺失证据**：Level97 的 `BalloonSlot` 字段由 Unity 序列化赋值，编译器 `CS0649` 不能作为删除依据；PlayMode 气球全流程和移动端输入尚未回归，标记为 `uncertain`。
- **回滚**：恢复 `StopFindJumpButton()` 与原分散清理代码即可。

更新时间：2026-08-13

## 原有行为基线

Level097 复制 Level011 的场景和标准门结算链。Level011 的气球点击由各自的旧执行器处理；角色使用共享 `ActorCtrl`。本关有意改变的范围仅为气球物理、点击后的视觉补生、拖拽吸附和载人飞行，不改变门、失败区、成功演出、存档或打点。

## 受影响文件与既有系统

- 本关资源：`Level_097.prefab`、`Level97Ctrl.cs`、`Level97ActorCtrl.cs`。
- 只读复用：Level011 的场景、爆炸模板、音效和门成功链；`DLevel_045` 的气球物理参数；共享 Actor 的基础移动。
- 当前配置状态：生成 JSON 已有 `10097 / Level_097` 与排序 99，但源 Excel 和生成 JSON 均处于已有改动状态，本轮未重写。
- 不修改：Level011、DLevel045、共享 `ActorCtrl`、共享 HUD、粒子/音频资源、DB、analytics 和平台系统。
- 必要局部差异：Level097 保留启用的横向顶部 `AirCollider (1)`，另一个倾斜 `AirCollider` 维持未激活；载人气球顶到横向顶部墙后必须限制整组继续上升。Level011 Prefab、Layer 和全局碰撞矩阵保持不变。

## 风险与控制

| 风险 | 当前控制 | 验证状态 |
| --- | --- | --- |
| 轻点被误判为拖拽 | 移动达到 `60px` 才切换拖拽；松手最后一帧也先更新距离再分流，并与 Prefab 序列化值一致 | 代码静态通过，触摸手感待验 |
| 拖拽松手又触发爆炸 | 点击与拖拽使用互斥状态，进入拖拽后只吸附或恢复自由上浮 | 代码静态通过，PlayMode 待验 |
| 爆炸后改变浮力或绳长 | 只替换真实 Sprite 的可见表现与原绳线端点；刚体、Collider 和 Joint 不销毁、不重建 | 代码静态通过，物理观感待验 |
| 补生球先出现、结束才突然接线 | 临时球先在门后初始化位置/缩放，再把原 LineRenderer 接到临时底部挂点并启用；动画每帧同步线坐标 | 代码静态通过，画面待验 |
| 补生中断留下已销毁绳端 | 清理顺序固定为恢复真实 Sprite、恢复原始绳线两端并同步、销毁临时球、清补生状态 | 代码静态通过，Disable/Destroy 待验 |
| 补生视觉跑到门前 | 门根 SortingGroup 为 `SceneLayer/100`，临时 Sprite 固定为同层 `99`；真实气球 `105` 不改 | 静态排序通过，画面待验 |
| 门或气球移动时补生目标错位 | 起点固定读取初始化缓存的原门端，终点每帧读取真实气球位置；不读取拖拽态自由绳端 | 代码静态通过，运动中待验 |
| 拖拽时绳线仍连门 | 关物理关节后把可视终点切到气球下方 `1.8` 的运行时节点；自由球始终保留该垂绳 | 代码静态通过，自由绳观感待验 |
| 拖空后气球回门或残留 Kinematic | 松手统一恢复 Dynamic、`gravityScale=-0.6`、Collider 和自由绳，不再执行回门 Tween | 代码静态通过，物理观感待验 |
| 自由球被顶部墙挡住无法飞走 | 仅自由态临时忽略目标顶部墙；吸附 Boxy 前恢复碰撞，销毁/清理也对称恢复 | 代码静态通过，飞出节奏待验 |
| 快速点击或系统取消串成拖拽 | 单活动触点按 fingerId 跟踪；触点丢失/取消直接清理，鼠标丢失松手也主动取消，不用取消坐标补算拖拽 | 代码静态通过，移动端多指待验 |
| 绳尾无法吸附或未碰 Boxy 却隔空吸附 | 只按自由绳尾到 Boxy 根主 Collider 的点包含/最近距离判定；不读取气球本体或失活 `pushCollider`，额外容差仅 `0.05`；拖动后同帧同步可见线尾 | 代码静态与隔离编译通过，PlayMode 待验 |
| 拖走气球后门位缺球 | 首次从门位进入拖拽时即启动同槽 `0.65s` 补生；自由球与门位状态分离 | 代码静态通过，画面待验 |
| 自由球或载人球无法戳破 | 自由球轻点真销毁；载人球轻点先恢复 Boxy 重力/跳跃再真销毁 | 代码静态通过，下落手感待验 |
| 载人时其他气球被锁死 | 当前载人球优先命中；未命中时仍搜索其他自由球和门位球，但第二只球不能抢占载人状态 | 代码静态通过，连续操作待验 |
| 载人后气球遮住 Boxy | 气球中心相对角色提高到 `(0, 3.2)`，绳接角色顶部 `(0, 0.55)`，保留约 `2.0` 可视间距 | 静态几何通过，镜头内观感待验 |
| Kinematic 气球被位置跟随强制穿过顶部墙 | 每个 FixedUpdate 用气球真实 Collider 向上 Cast，按命中剩余距离钳制气球与 Boxy 的共同 Y 速度，墙下保留 `0.02` 间距 | 代码静态与编译通过，PlayMode 待验 |
| 靠墙吸附时气球首帧已穿入墙内 | 用 `Collider2D.Distance` 检查目标墙重叠，气球与 Boxy 整体沿墙外法线退回后再扫描上升 | 代码静态通过，边界手感待验 |
| 顶墙后无法左右移动 | 只钳制共同 Y 速度，不修改 ActorCtrl 的 X 输入和速度 | 调用链静态通过，PlayMode 待验 |
| 顶墙查询产生持续 GC | 复用固定 `RaycastHit2D[8]`，每个物理帧只做目标墙 Distance 和短距离 Cast | 静态通过，移动端 Profiler 待验 |
| 气球与角色上升速度漂移 | 同一 `FixedUpdate` 写入角色 Y 速度并把气球定位到角色固定偏移 | 代码静态通过，实际物理帧待验 |
| 飞行时左右移动被锁 | 不调用 `SetCtrlable(false)` 或 `StopMoving()`；保留基类 `OnUpdate()` | 调用链静态通过，PlayMode 待验 |
| 飞行时仍可跳跃 | Actor 子类拦截 `JumpButton()`，HUD 按钮同时禁用 | 静态通过，键盘/触摸待验 |
| 替换 Actor 后门动画或皮肤失联 | `BaseLevelCtrl`、`EnterDoorAnimCtrl`、`ActorSkinBinding` 三处改绑同一 Actor | Prefab 静态通过 |
| 旧气球执行器重复爆炸或销毁 | 三个旧 `ClickItemExecutor` 均禁用 | Prefab 静态通过 |
| 暂停期间协程或载人运动偷跑 | 补生协程等待恢复；当前拖拽转回自由物理；载人速度清零 | 代码静态通过，事件顺序待验 |
| 加载/重开时遮罩下误点气球 | Level097 原始输入默认锁定；恢复事件延后一帧核对 FSM，仅真实 PLAYING 才开放 | 调用链静态通过，入场/Replay 待验 |
| Reset/Replay 黑幕淡出期间旧实例继续响应输入 | 直接订阅既有 Reset/Replay 事件，在请求同帧取消拖拽并清零载人速度 | 调用链与隔离编译通过，PlayMode 待验 |
| PAUSE 退出到 FAILED 时恢复事件误开放 | 不直接信任 `GameResumeEvent`，下一帧以 `PlayCtrl.CheckState(PLAYING)` 决定是否恢复 | 调用链静态通过，退出/失败待验 |
| 失败/销毁后重力或按钮未恢复 | 失败、Disable、Destroy 都收敛到本地恢复逻辑 | 代码静态通过，重开待验 |
| 点击频繁产生 GC 峰值 | 每次点击创建一个临时 Sprite 和一个爆炸实例，完成后销毁；同槽补生期间拒绝重复输入 | 移动端性能待验 |
| 正式入口或排序错误 | 当前生成配置静态存在唯一 10097；不在脏 Excel 上推断或重写排序 | 来源与最终配置待复核 |
| 顶部阻挡对象绑错 | Prefab 的 `carryingCeilingCollider` 精确指向启用的 `AirCollider (1)` 的 `BoxCollider2D` fileID `5657534105333743630`；未激活的倾斜墙不参与 | 静态绑定已核对，飞行边界待 PlayMode 验证 |
| 气球与门发生物理碰撞 | 从各 `DistanceJoint2D.connectedBody` 的门刚体层级收集 Collider，对三只源球和运行时自由球逐对 `IgnoreCollision`；初始化、重新启用、槽位恢复都会重应用，销毁时恢复 | 代码/Prefab 静态通过，PlayMode 待验 |

## 验证证据

- 使用 Unity 当前 Android Bee `Assembly-CSharp.rsp` 与 Unity 自带 Roslyn 完整隔离编译 `Assembly-CSharp` 成功：`0 error / 104 warning`；104 条均为工程既有 warning，Level097 自身诊断为 `0`，临时 DLL/PDB/RSP 已删除。
- Prefab 共 `254` 个唯一定义、`556` 次非零本地引用，重复定义与纯本地引用缺失数均为 `0`；顶部阻挡引用是 `carryingCeilingCollider -> 5657534105333743630`。
- 三套气球物理组件、三个禁用的旧点击执行器、Level97 控制器字段和 Actor 三处引用已逐项静态核对。
- Prefab `dragThresholdPixels` 为 `60`，源码默认也为 `60`；门根 `DoorOutside` 及嵌套门 Collider 已由控制器登记，源球与自由球均有显式门碰撞忽略，未修改 Layer 矩阵。
- 状态机已逐项静态核对门位轻点、门位拖走补生、自由球拖空、重抓/戳破、自由绳尾接触吸附、载人球戳破恢复 Boxy，以及快速触点取消；未发现三球共享状态导致直接吸附的路径。
- 本轮回归复核确认吸附入口使用 `LooseRopeEnd.position` 对 Boxy 根 `PolygonCollider2D` 做 `OverlapPoint/ClosestPoint`，不再使用气球圆形碰撞体；拖动路径会在移动刚体后立即刷新同一线尾的 `LineRenderer`。最新完整 `Assembly-CSharp` 后台编译仍为 `0 error`，Level097 自身 `0 diagnostics`。
- 三条绳的初始门端、补生临时起点、自由球垂绳端和载人角色顶部端已逐项静态核对；补生起点只使用缓存的原门端，临时对象销毁前先恢复原始两端。
- 嵌套门源 Prefab `Assets/Game/Prefabs/LevelItems/door_0.prefab` 的根 SortingGroup 与可见 Renderer 均为 `SceneLayer/100`，补生临时 Sprite 为 `SceneLayer/99`，不会再使用错误的 `101`。
- 新脚本 `.meta` GUID 唯一并与 Prefab 中的 `m_Script` 一致。
- `DLevel_045.prefab` 已核对三只气球的 `gravityScale=-0.6`、`CircleCollider2D.radius=0.6` 和 `m_MaxDistanceOnly=1`。
- `EnterLevelLoading`、`PlayCtrl.ResetGame/ReplayGame` 和 `PlayCtrl_Pause` 的状态顺序已静态核对；Level097 以默认锁定覆盖实例创建到首个状态事件之间的输入窗口，在 Reset/Replay 请求同帧封闭旧实例输入，并在恢复事件后复核下一帧的真实 FSM。
- 当前 `tbleveldata.json` 结构化读取到唯一 `10097 / Level_097`，`tblevelsort.json` 读取到 `id=99 / path=10097`。
- `AirCollider (1)` 的 GameObject、Collider 均启用，另一个倾斜 `AirCollider` 的 GameObject 未激活；载人顶部阻挡由本关私有的 Collider Cast/Distance 完成。自由球松手后会临时忽略顶部墙以飞离场景，吸附 Boxy 前立即恢复；另有一对临时忽略只用于新自由球与来源门位球的初始重叠。两类忽略均在销毁/清理时对称恢复，载人态不会忽略顶部墙。

## 未验证缺口

- 没有操作前台 Unity，也没有执行整关 PlayMode；因此所有玩家可见手感仍为 `uncertain`。
- 需要实测：三只气球各自点击、快速连续点击、位移未达到 `60px` 时不进入拖拽、达到阈值后拖拽、松手不误爆、拖空自由上浮并穿过顶部墙、离场前重抓/戳破、`1.8` 自由绳长度、自由绳尾 `0.05` 吸附范围和失活辅助碰撞体不误吸附。
- 需要实测：门位气球和自由球在门根移动/重新启用后仍不发生物理碰撞，且销毁/重置后忽略关系不会污染其它关卡对象。
- 需要实测：拖走气球后门位立即补生、补生球从门后出现第一帧就带着绳线、`0.65s` 长回遮挡、爆炸与长回的连续性，以及自由球与门位补生互不抢状态。
- 需要实测：载人过程中其他气球仍可操作、第二只不抢占载人状态，以及载人球戳破后 Boxy 立即恢复重力下落和跳跃按钮。
- 需要实测：载人偏移 `(0, 3.2)` 是否既不遮住 Boxy，又不会让气球在镜头顶部被裁切。
- 需要实测：角色与气球同速上升、空中左右移动、触摸/键盘均不可跳、到门后的成功演出。
- 需要实测：载人气球顶住启用的顶部 `AirCollider (1)` 后整组停止上升，顶墙时左右移动、贴墙吸附恢复、离开墙边、低帧率和连续物理帧均不穿透或抖动。
- 需要实测：首次入场遮罩、暂停/恢复、失败区、死亡、失败重开、Replay、退出和重新进入。
- Android/iOS 横屏多指、不同分辨率、60 fps、物理稳定性和点击实例化峰值未验证。
- 源 Excel 当前内容、配置生成差异、本地化 key 和正式排序尚未通过隔离 Luban 流程复核。

## 回滚路径

回滚仅需删除 Level097 私有脚本和文档，移除 `Level97Ctrl`，将 Level097 Actor 恢复为复制源组件并恢复三处 Actor 引用，再启用三个旧 `ClickItemExecutor`。如需只撤回本轮视觉调整，可恢复 `0.24s` 补生、移除自由绳端与角色顶部端，并恢复原载人偏移；共享参考关卡、Actor、GamePanel、门、粒子、音频、存档和打点均无需回滚。
