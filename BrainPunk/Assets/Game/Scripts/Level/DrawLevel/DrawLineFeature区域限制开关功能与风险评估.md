# DrawLineFeature 区域限制开关功能与风险评估

## 1. 需求与影响范围

- 需求：所有 DLevel 的 `DrawLineFeature` 增加“是否开启区域限制”选项，默认关闭。
- “区域限制”仅指玩家是否只能在绑定的 `Draw Area` 范围内画线，不代表地板、门或其它
  场景碰撞体是否允许画线。
- 影响关卡：`DLevel_001`～`DLevel_010`。这 10 个 Prefab 均挂载同一 GUID 的
  `DrawLineFeature`，因此由共享脚本统一提供选项，不批量改写关卡 Prefab。

## 2. 受影响文件与系统

- 运行时代码：`Assets/Game/Scripts/Level/DrawLevel/DrawLineFeature.cs`。
- 关卡资源：`Assets/Game/Prefabs/DrawLevel/DLevel_001.prefab`～`DLevel_010.prefab`
  继续保留原有 `drawArea` 引用，本次没有修改这些 Prefab。
- 文档：本文与 `DevelopMD/已有功能设计与使用记录.md`。
- 未修改 DB、存档、Luban、Resources、热更地址、UI、全局事件、analytics、SDK、
  Layer 矩阵或平台分支。

## 3. 修改前行为基线

- `InitializeRuntime()` 始终要求项目存在 `Draw` Layer，并要求 `drawArea` 已绑定且位于
  `Draw` Layer；校验失败时不开放画线。
- 落笔必须通过 `drawArea.OverlapPoint(...)`；连续画线离开区域时立即完成当前有效笔画。
- 地板、门等非 `Draw` Layer 的 `Collider2D` 已通过独立的 `OverlapCircle` 与
  `CircleCast` 阻挡落笔和连续采样，该逻辑不依赖 `drawArea.OverlapPoint(...)`。

## 4. 修改后行为

- 新增序列化字段 `enableDrawAreaRestriction`，Inspector 默认值为 `false`。
- 关闭时：不要求 `drawArea` 存在，不执行区域重叠判断；玩家可在输入相机投射到
  `DrawSpace` 平面的任意非阻挡位置画线。
- 开启时：保留修改前规则；`drawArea` 必须绑定、启用、处于活动层级且位于 `Draw`
  Layer，落笔和连续采样都必须在其范围内。
- 无论开关状态如何，项目仍必须存在 `Draw` Layer，以构造非 Draw 障碍掩码；地板、门、
  已完成线段等非 Draw 碰撞体的阻挡逻辑始终执行。
- 输入开启、UI 拦截、采样间距、墨水消耗、笔画完成、碰撞体生成、Reset 和结算流程不变。

## 5. 数据、资源、UI 与事件流

- 配置来源仅为各 DLevel 上 `DrawLineFeature` 的 Unity 序列化布尔字段。
- 既有 Prefab YAML 未记录新字段时，C# 与 Unity 的布尔缺省值均为 `false`，符合默认关闭
  要求；下一次在 Unity 中保存 Prefab 时可能显式写入 `enableDrawAreaRestriction: 0`。
- `AutoBind` 仍可预先补齐 `drawArea`，但关闭开关时运行时不会用它限制绘制范围。
- 本次没有新增资源加载、DB/存档写入、UI 消息、局部或全局事件。

## 6. 验证证据

- 静态扫描确认 `DrawLineFeature` 脚本 GUID
  `7390537236784c0384b1f84839bd3893` 被 10 个 DLevel Prefab 引用，覆盖
  `DLevel_001`～`DLevel_010`。
- 代码差异确认关闭开关只短路区域判断；`IsBlockedAt(...)` 与
  `IsBlockedBetween(...)` 的调用和非 Draw Layer 掩码未删除或绕过。
- 使用 `Library/Bee/artifacts/1300b0aE.dag/Assembly-CSharp.rsp` 与本机 Roslyn 对完整
  `Assembly-CSharp` 编译，结果为 `0 error / 96 warning`；warning 为当前工程既有输出。
- `git diff --check` 已通过目标代码和文档检查。

## 7. 剩余风险与未验证项

- Unity Inspector 中的中文 Tooltip、开关显示顺序，以及开启/关闭后的实际触摸手感尚未在
  Unity Editor PlayMode 验证。
- 2026-08-03 校准：阻挡查询现使用显式 `ContactFilter2D.useTriggers=true` 与非 Draw LayerMask，
  Trigger 命中不再依赖运行时全局 `Physics2D.queriesHitTriggers`；对象仍必须启用 Collider2D 并位于
  非 Draw Layer。该校准由 D025 成功门 Trigger 实测漏拦触发，完整 Draw 关回归仍待验证。
- Android/iOS 横屏多点触控、屏幕边缘绘制、快速跨越障碍和 60 fps 表现尚未真机验证。
- 默认关闭会有意扩大所有现有 DLevel 的可落笔范围；关卡旧解法与边缘画线仍需逐关回归。

## 8. 回滚路径

1. 单关快速回滚：在对应 DLevel 的 `DrawLineFeature` 上开启
   `enableDrawAreaRestriction`，即可恢复原 `Draw Area` 范围约束。
2. 全局代码回滚：移除该字段及区域判断/初始化中的两个分支，恢复始终校验并读取
   `drawArea` 的旧逻辑。
3. 本次没有数据、存档或资源迁移，回滚不需要清理玩家数据。

## 9. 2026-08-04：按住期间离开可绘制区域不再提前落笔

### 9.1 需求与原行为基线

- 新需求：活动笔画只有在 Touch Up（Editor/Standalone 对应 Mouse Up）时才正常完成；手指仍按住时，
  进入 Draw Area 外或非 Draw Collider 阻挡区域只暂停新增线段，回到可继续采样的位置后仍能续画。
- 修改前 `TryContinueStroke()` 在目标点离开 Draw Area、`AppendSamplesTowards(...)` 命中非 Draw
  障碍、或墨水耗尽时都会立即调用 `CompleteActiveStroke()`，造成按住状态下提前落笔。

### 9.2 受影响文件与修改后行为

- 运行时代码仍只修改 `Assets/Game/Scripts/Level/DrawLevel/DrawLineFeature.cs`；本文与
  `DevelopMD/已有功能设计与使用记录.md` 同步记录。未修改任何 DLevel prefab、配置或资源。
- `TryContinueStroke()` 发现当前位置不在 Draw Area 时直接返回，不完成当前笔画。
- 连续采样命中非 Draw 障碍或墨水耗尽时停止追加采样，但保持当前活动笔画和手指 ID；玩家仍按住并
  回到从最后有效采样点可达的位置时，可以继续同一笔。跨越阻挡 Collider 的路径仍不会生成线段。
- `TouchPhase.Ended` / Mouse Up 仍调用 `CompleteActiveStroke()`。`TouchPhase.Canceled`、活动触摸丢失、
  暂停、失焦、组件禁用或显式关闭输入仍走既有取消路径，不把不完整输入提交为有效笔画。
- 落笔区域校验、UI 拦截、墨水计量、碰撞体生成、`StrokeCompleted`、Reset、DB、存档、资源加载、
  全局事件、analytics 与 Android/iOS 平台分支均未增加新链路。

### 9.3 验证、风险与回滚

- 静态扫描确认 `TryContinueStroke()` 不再调用 `CompleteActiveStroke()`；正常完成调用仅保留在
  Touch/Mouse Up 入口，非 Draw 障碍检测 `IsBlockedBetween(...)` 仍由 `AppendSamplesTowards(...)` 执行。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly
  /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 编译通过，结果为
  `0 error / 102 warning`；warning 属于当前工程既有范围。
- 目标代码与两份中文记录的 `git diff --check` 通过，仅有工作区 LF/CRLF 转换提示。
- 待 Unity PlayMode 验证：按住离开/返回 Draw Area、按住碰到/离开阻挡物、墨水耗尽后抬手、短于最小
  有效长度后抬手、多点触控、暂停/失焦/系统取消以及 Reset/Replay。
- Android/iOS 横屏真机触控与 60 fps 表现尚未验证；本次没有新增每帧查询或分配，性能风险较低但仍需真机确认。
- 回滚时恢复 `TryContinueStroke()` 中三个提前 `CompleteActiveStroke()` 分支即可；没有数据迁移或 prefab 回滚。

## 10. 2026-08-04：Down 建立画线检测、Up 结束画线

### 10.1 需求与原行为基线

- 新需求：手指或鼠标按下时即建立一次画线检测，不要求首次按压点已经位于 Draw Area；按住拖入
  合法绘制位置后才真正起笔，抬起时结束本次画线。
- 原行为把“活动手指”绑定在 `TryBeginStroke()` 成功创建线段之后。首次按压位于 Draw Area 外、
  非 Draw Collider 上或无法投影的位置时不会保存 finger ID，后续移动阶段也不会再次尝试起笔，
  因此同一次按压拖入合法区域仍无法画线。
- 原行为已经在活动线段阶段拒绝 Draw Area 外的当前触点，并通过非 Draw Layer Collider 查询阻止
  障碍内采样；这些禁画边界必须保留。

### 10.2 受影响文件与修改后行为

- 运行时代码仅修改 `Assets/Game/Scripts/Level/DrawLevel/DrawLineFeature.cs`；同步更新本文和
  `DevelopMD/已有功能设计与使用记录.md`。未修改 DLevel prefab、DB/存档、Luban、资源地址、UI、
  全局事件、analytics、SDK 或平台配置。
- `TryBeginPointerSession()` 在 Down/Began 时先锁定当前 mouse/finger；首次位置不满足 Draw Area、
  投影或非 Draw Collider 门禁时不生成 GameObject、不消耗 Ink，也不派发 `StrokeStarted`。
- 按住期间只要尚未生成活动线段，就用同一 pointer 的当前位置重新执行落笔校验；首次进入合法位置
  才创建 `RuntimeStroke` 并派发既有 `StrokeStarted`。从 UI 开始的按压继续被拒绝，不会穿过 UI 后起笔。
- `TouchPhase.Ended` / Mouse Up 调用 `CompletePointerSession()`，完成有效线段并释放 pointer；
  `TouchPhase.Canceled`、触摸丢失、暂停、失焦、组件禁用、Reset 或关闭输入仍取消未完成线段、退还
  本笔 Ink 并释放 pointer，不会把系统中断误提交为有效笔画。
- 开启 `enableDrawAreaRestriction` 时，除当前触点外，每个固定间距插值采样点也必须通过 Draw Area
  校验；从区域外绕回时不能用一条直线跨过禁画区。关闭区域限制时不增加 Draw Area 物理查询，
  非 Draw Layer Collider 的 `OverlapCircle` / `CircleCast` 阻挡保持不变。

### 10.3 数据、资源、事件与性能边界

- 输入状态仍由 `DrawLevelCtrl.Tick()` 每帧转发，不新增独立 `Update`、协程、全局事件或运行时资源加载。
- pointer 会话只复用现有 `_activeFingerId` 与 `_usingEditorMouse`；活动线段状态继续由
  `_activeStrokeObject` / `_activeLineRenderer` 管理，未新增序列化字段或 prefab 数据迁移。
- `StrokeStarted` 仍只代表合法区域内线段对象已经成功创建；`StrokeCompleted`、完成笔碰撞体、质量、
  Ink 和星级计算仍走原链路。区域限制开启时，每个已有采样步最多增加一次 Draw Area `OverlapPoint`，
  关闭时通过布尔短路跳过该检查。

### 10.4 验证证据

- 静态差异确认 Down/Began 入口只建立 pointer 会话；`TryBeginStroke()` 仍同时校验 UI、投影、
  `IsInsideDrawArea(...)` 与 `IsBlockedAt(...)`，未在非法位置创建或追加线段。
- 静态差异确认正常完成入口仍只有 Mouse Up / `TouchPhase.Ended`；取消、暂停、失焦、Disable、Reset 和
  输入关闭均通过 `CancelPointerSession(...)` 同时清理活动线段与 pointer。
- 使用 Unity 6000.0.59f2 自带 Roslyn 和
  `Library/Bee/artifacts/1300b0aEDbg.dag/Assembly-CSharp.rsp` 编译完整 Assembly-CSharp，结果为
  `0 error`；输出 warning 属于工程既有范围。
- `dotnet build` 的首次 `--no-restore` 尝试在 64 秒超时，后续发现 `Temp/obj/Assembly-CSharp/project.assets.json`
  缺失；允许 restore 的全量构建在 184 秒再次超时，因此不把这两次尝试记为通过证据。

### 10.5 剩余风险、手工验证与回滚

- 待 Unity PlayMode 验证：区域外 Down 后拖入起笔、非 Draw Collider 上 Down 后拖出起笔、区域内正常
  Down/Move/Up、只在区域外移动后 Up、Draw Area 外绕回、UI 上 Down 后移出、短线、墨水耗尽、暂停、
  Reset、失焦、Touch Canceled、触摸丢失以及双指竞争。
- Android/iOS 横屏真机触控、快速拖动的边界采样手感和 60 fps 表现尚未验证；静态证据不能替代真机。
- 回滚代码时移除 `TryBeginPointerSession()` 和 pointer/活动线段状态拆分，恢复仅在 Down/Began 直接调用
  `TryBeginStroke(...)`；同时移除 `AppendSamplesTowards()` 的 Draw Area 插值点校验。没有数据、资源或
  prefab 迁移需要回滚。
