# Level109「有志者事竟成」功能说明

## 2026-08-18 黄色范围脚本精简

- 背景：合并停用和销毁阶段重复的运行态挂起逻辑，保持循环路线、门触发、克隆清理和关卡流程不变。
- 影响文件：仅 `Level109Ctrl.cs`。私有 `SuspendRuntime()` 统一设置关闭状态、禁用运行时门触发并重置事件；销毁路径仍继续移除动态监听和销毁路线克隆。
- 前后差异：原三项清理的顺序与调用时机不变，没有修改循环坐标、门、地形、资源、配置、UI、打点或成功失败链。
- 验证：`Assembly-CSharp.csproj` 编译通过（0 错误），目标 diff 检查通过。
- 回滚：将 `SuspendRuntime()` 的三行内容内联回两个生命周期回调。

## 需求与基线

本关复制 Level023 的场景结构。左侧 `Ground_long` 地面和右侧四平台组分别无限循环；左路线完成 4 次循环，或右路线完成 6 次循环后，在各自的下一个循环位置出现过关门。达到条件后仍可回头，已经完成的循环不会倒退。左侧地面节距为 28 世界单位，右侧四平台组节距为 25 世界单位。

修复前，Level023 遗留的 gameplay `InfiniteScrollMap` 把 `Platsform`/`Platsform (1)` 当作同一条双向路线：玩家向左时也会把右侧高台搬过去；真正的左地面 `Ground_long` 只有一段且未参与循环。两份高台组内部坐标还相差 3.24，导致循环交替出现重叠和大缝。`Bg`/`Bg (1)` 只负责背景滚动，不能作为路线计数来源。Level023 遗留的 `ClickArea -> Level23Ctrl.ClickLevelSuccess` 直通链路仅在 Level109 实例内关闭。

## 新行为

- `Level109Ctrl` 只挂在 `Level_109.prefab` 根节点，不修改 `InfiniteScrollMap`、`BaseLevelCtrl`、`Level23Ctrl` 源码、共享地面/平台/门 Prefab 或其它关卡。Prefab 上 fileID `5209297218462569700` 的旧混合 gameplay 循环器默认 disabled，控制器启动时再次关闭它。
- 左路线以 `LeftGroundLoopItem` 为 index 0 循环父节点，内部包含局部零坐标的 `Ground_long`；右路线以 `RightPlatformLoopItem` 为 index 0 循环父节点，内部包含首组 `Platsform`。运行时分别复制完整父节点形成 3 格池，因此以后放进对应父节点的静态装饰会随整段一起复制和回收。左索引永远不大于 0、节距为 28；右索引永远不小于 0、节距为 25。旧的偏移缓存组 `Platsform (1)` 保持根节点直属、inactive 且不参与本关路线。两侧都保留“当前格、前一格、后一格”，窗口变化时只搬出窗口的格，因此走远后可以安全回头，不会把玩家脚下 Collider 提前搬走。
- `leftTerrainSource=Ground_long`、`rightTerrainSource=Platsform` 单独提供回收边界，新增装饰即使带 Collider 也不会改变循环切换时机。循环池不会自动重置子物体的自定义状态，循环父节点内只应直接放静态或可重复的无状态装饰；需要一次性触发、拾取或状态复位的玩法对象必须另做复位逻辑。
- 开场左循环父节点和左门一起右移 0.6：`LeftGroundLoopItem.x=-34.87`、其子 `Ground_long.localPosition=(0,0,0)`、左门模板 `x=-32.91`。地面世界位置不变；地面右沿与首个高台左沿由约 0.185 的空隙改为约 0.415 的水平重叠，用于接住玩家从开场高台向左落下。右循环父节点与子平台组均保持局部零坐标；共享资源未修改。
- 控制器使用 `BaseLevelCtrl.Actor` 的世界 X 建立进度基线。入口门演出期间不计数；`trackingStartDelaySeconds` 与 `ActorCtrlable` 同时满足后记录 `actorInitialX`。向左路线保存历史最小值 `leftMinReachedX`，向右路线保存历史最大值 `rightMaxReachedX`，分别计算 `actorInitialX - leftMinReachedX` 与 `rightMaxReachedX - actorInitialX`，因此回头不会减少已完成次数。
- `leftCyclesToUnlock` 默认 4，`rightCyclesToUnlock` 默认 6；`leftCycleWidth=28`、`rightCycleWidth=25`，`cyclePositionTolerance` 用于抵抗浮点误差。门不再仅从自身绝对坐标硬算，而是保存相对路线模板的初始偏移，再落到目标循环锚点。当前左门目标为 `-32.91 - 4*28 = -144.91`，位于第五段地面；右门目标为 `15.96 + 6*25 = 165.96`，位于第七组高台。`moveDoorsToUnlockLoop` 可关闭门位移。
- `leftExitDoor`（场景中的 `door_0`）和 `rightExitDoor`（`door_0 (1)`）是两扇独立出口门；`hideDoorsUntilUnlocked` 开启时两门初始隐藏，满足各自路线条件后分别显示，不要求两条路线都完成。
- 两扇门的共享 `OnTrigger2DHandle` 在本关实例禁用，运行时各自添加本关局部触发器。触发回调严格比对当前 `BaseLevelCtrl.Actor` 的 `ActorCtrl`，拒绝其它 Collider、死亡/击倒角色和重复成功请求；成功仍调用既有 `BaseLevelCtrl.SuccessStrightly()`，复用标准开门、星星收集和成功状态链路。
- 收到 `GameResetEvent` 或 `GameReplayEvent` 后，旧实例立即进入 teardown：停止局部触发器并解除事件订阅，避免旧实例在销毁等待窗口继续计数或通关；进度、门位置和显隐初态由新 Prefab 实例建立，不再回写旧实例。`OnDisable`/`OnDestroy` 继续解除监听和清理运行时克隆。
- 如果关卡根节点只是临时禁用后重新启用，`OnEnable` 会恢复本关动态触发器；已经收到 Reset/Replay 的旧实例仍保持 teardown，不会重新接管门碰撞。

## 绑定与配置

`Level_109.prefab` 根节点绑定 `BaseLevelCtrl`、Actor、两扇出口门和本关控制器字段：`levelCtrl`、`actor`、`leftExitDoor`、`leftExitDoorSuccessAnim`、`rightExitDoor`、`rightExitDoorSuccessAnim`、`mixedGameplayLoop`、`leftCycleRoot`、`rightCycleRoot`、`leftTerrainSource`、`rightTerrainSource`。两扇门的成功动画均来自门实例自身的 `SuccessAnimCtrl`；入口 `StartDoor` 不参与循环门计数。策划需要循环的纯视觉装饰应作为 `LeftGroundLoopItem` 或 `RightPlatformLoopItem` 的子物体添加，不要放进旧 `Platsform (1)`。

旧 gameplay `InfiniteScrollMap` 组件 fileID 为 `5209297218462569700`，保留序列化数据但默认 disabled；背景滚动组件 fileID 为 `7716418680240002741`，tile size 为 50，绑定 `Bg`/`Bg (1)`，继续使用原有 `detectRange=0.4`，不参与左右玩法路线循环。两扇出口门均是关卡根节点直接子级（Transform fileID `1164032669087621897` 与 `6615728965381012200`），不属于循环池，运行时按路线锚点独立定位。

运行时配置包含 `10109 -> Level_109` 与排序项 `102 -> 10109`；源 `LevelData.xlsx`、`LevelSort.xlsx` 已保留现有行序并追加该关配置，WPS 进程未被本轮控制或关闭。`S_congraktTips_Des_level_109` 与 `S_answer_Des_level_109` 当前尚未在 `I2Languages.asset`/`localization.csv` 找到对应文案，需本地化确认。

## 验证记录

- 2026-08-18 增加循环父节点后静态 YAML 闭包检查：`Level_109.prefab` 有 212 个唯一定义、419 次本地引用、0 重复 fileID、0 悬空引用；两组循环父节点、两项独立地形边界引用与 `28/25` 节距均存在。旧 gameplay 循环器为 disabled，背景循环器保持 enabled 且保留原有 `detectRange=0.4`。
- 静态几何确认：左地面按 28 排列时，相邻约重叠 0.729；右侧同一四平台模板按 25 排列时，轮次接缝约 0.425。三格窗口使用左索引 `<=0`、右索引 `>=0`，对向左至 `x=-145`、向右至 `x=166` 再原路返回的序列模拟未出现重复索引或路线跨边界铺设。
- 已回读 `tbleveldata.json`/`tblevelsort.json` 和两份源 xlsx，检查新增 ID、路径、排序连续性、无重复/孤儿引用及 XML 可解析性；跟踪文件执行 `git diff --check`，Level109 未跟踪脚本/Markdown/Prefab 另行执行尾随空白与 fileID 静态扫描，未发现实质空白或悬空本地引用。
- 使用 Unity 6000.0.59f2 自带 Roslyn 和当前 Bee `Assembly-CSharp.rsp` 在临时输出路径后台完整编译通过：`0 error / 107 warning / Level109 diagnostics 0`；warning 为当前工程诊断。未启动、聚焦或操控 Unity 前台。
- 未完成 PlayMode 的开场左落点手感、长距离左右循环画面、门实际显示/碰撞、重开/Replay 和横屏移动端验收；这些结果标记为 `uncertain`，需在项目开发环境补测。

## 回滚

仅回退“装饰循环父节点”增量时：删除 `LeftGroundLoopItem`/`RightPlatformLoopItem`，把 `Ground_long`/`Platsform` 恢复为关卡根直属并恢复前者世界坐标，控制器重新直接绑定两项地形模板。回退完整路线修复时，再移除 `Level109Ctrl` 的路线模板/三格池逻辑，把 fileID `5209297218462569700` 恢复 enabled，把 `Ground_long.x`/左门模板 X 恢复为 `-35.47/-33.51`，并把 `leftCycleWidth` 恢复 25。背景循环器未改变，无需回滚；共享脚本和其它关卡不需要回滚。

## 2026-08-26：Reset/Replay teardown 精简

- 删除旧实例的进度清零、`IsActorEnterDoor` 回写、门位置恢复和门显隐复位；`BeginTeardown()` 现在只设置不可复活门禁并调用 `SuspendRuntime()`。
- 保留动态门 Trigger 停止、事件解绑、OnDestroy 监听移除和循环克隆销毁，这些属于销毁窗口安全清理。
- 已通过 C# 编译（0 error / 120 warning）；门在黑幕窗口内不再跳回初始位置。真实 Reset/Replay 画面仍需 PlayMode 验证。
