# Level109 风险评估

## 2026-08-18 行为等价精简风险复核

- **事实**：停用与销毁原本重复设置关闭状态、禁用门触发和解绑事件；销毁专属的监听移除与克隆销毁仍在原位置执行。
- **直接结论**：运行态挂起、门触发边界和路线克隆所有权不变，不影响循环路线和结算流程。
- **假设**：无新增运行时假设；私有方法只封装原有语句。
- **缺失证据**：尚未执行 Unity PlayMode 的三格循环、停用/销毁、重开和移动端回归，标记为 `uncertain`。
- **回滚**：内联 `SuspendRuntime()` 即可。

## 已控制风险

- **旧点击直通通关**：本关实例禁用 `ClickArea` 并清空其 UnityEvent，同时禁用遗留的 `Level23Ctrl`；共享脚本不改。
- **左侧错误出现高台**：旧 gameplay `InfiniteScrollMap` 在本关 Prefab 默认 disabled，`Level109Ctrl` 启动时再次关闭；左路线只复制包含 `Ground_long` 的 `LeftGroundLoopItem`，右路线只复制包含首组 `Platsform` 的 `RightPlatformLoopItem`，不会再把右侧高台搬到左侧。
- **两条路线互相污染**：左三格池的索引始终 `<=0`，右三格池的索引始终 `>=0`；左路线进度只使用历史最小世界 X，右路线只使用历史最大世界 X，回头不会减少已完成次数，也不会把一种地形铺到另一条路线。
- **走远后无法回头**：每条路线保留当前格及相邻两格；窗口跨格时只搬走已离开窗口的 tile。静态往返模拟覆盖左 `x=-145`、右 `x=166` 后回到出生点，未出现重复索引或提前搬走当前格。
- **右侧循环交替重叠/大缝**：旧 `Platsform (1)` 内四个平台整体比首组左偏 3.24，现保持根节点直属、inactive 且不进入新父节点；两个运行时缓存均从 `RightPlatformLoopItem` 复制同一首组平台和其装饰，因此每轮布局一致。按 25 节距计算，轮次接缝约 0.425。
- **左侧不是地面循环**：左路线模板固定为 `LeftGroundLoopItem`，其核心地形固定绑定子节点 `Ground_long`；节距使用 Level020 同资源配置的 28。碰撞体宽约 28.729 只提供约 0.729 的接缝重叠，不作为计数或门位节距。
- **开场向左落空**：`Ground_long` 与左门模板一同右移 0.6，首段地面右沿和最左高台左沿由约 0.185 空隙改为约 0.415 水平重叠；Y、Collider 尺寸和共享 Prefab 未改。
- **背景误计数**：路线完成数只读取 Actor 历史最远世界 X；`Bg`/`Bg (1)` 仍只由背景 `InfiniteScrollMap` 使用，不参与路线池或门解锁。
- **门提前可见或误触发**：`leftExitDoor` 与 `rightExitDoor` 默认 inactive；两扇门的共享 `OnTrigger2DHandle` 在本关实例禁用，解锁后由本关动态触发器接管。
- **非玩家 Collider 误通关**：动态触发器清空 Tag 预筛后，由 `GetComponentInParent<ActorCtrl>()` 严格比对当前 `BaseLevelCtrl.Actor`，并拒绝死亡/击倒角色和重复请求。
- **门位随循环 tile 漂移**：两扇门保持关卡根直属，不加入回收池；控制器保存门相对各自路线模板的初始偏移，并按目标 tile anchor 定位。当前左第五段门 X=`-144.91`，右第七组门 X=`165.96`。
- **装饰影响回收边界**：循环复制对象改为父节点，但边界检测继续只读取 `leftTerrainSource=Ground_long` 与 `rightTerrainSource=Platsform`，父节点内新增装饰的 Collider 不参与 tile 索引计算。
- **门或旧缓存被误复制**：两个循环父节点初始只包含各自的 canonical 地形；两扇门和 `Platsform (1)` 都保持关卡根直属。后续编辑时不得把门、旧缓存、入口门或全局关卡控制对象拖入循环父节点。
- **背景回归隔离**：背景循环器继续使用本关原有 `detectRange=0.4`。左右玩法路线改为私有三格池，不借用也不改写背景循环参数，避免相邻背景启用过晚而露出画面边缘。
- **入口演出被计入循环**：首帧计数同时等待 `trackingStartDelaySeconds` 与 `BaseLevelCtrl.ActorCtrlable`，再建立世界 X 基线。
- **成功链路分叉**：触发成功只调用既有 `BaseLevelCtrl.SuccessStrightly()`，奖励、存档、成功状态和门演出仍由公共流程处理。
- **重开旧实例继续运行**：收到 `GameResetEvent`/`GameReplayEvent` 后立即进入 teardown，停止动态触发器并解除订阅；旧进度和门表现不再复位，随旧实例一起销毁；`OnDisable`/`OnDestroy` 也关闭本地监听。
- **临时禁用后触发器丢失**：关卡根节点重新启用时，`OnEnable` 会恢复本关动态触发器；如果实例已收到 Reset/Replay，则 `resetOrReplayReceived` 门禁保持 teardown，不会复活旧实例。

## 待验证风险

- **开场左落点实际手感**：静态几何只能证明约 0.415 的水平承接重叠，不能证明 Actor 的 Collider、移动速度和坠落轨迹一定能稳定落在地面顶面；需 PlayMode 从入口门向左连续走动验证。若仍擦右侧立面，应只在 Level109 调整首段地面 X，不修改共享 `Ground_long`。
- **世界 X 与实际循环的对应关系**：当前左/右节距为 28/25，计数依赖 Actor 根节点世界 X。运行时若有角色传送、关卡根缩放或非 1:1 世界单位，需要重新验收循环阈值与门出现时机。
- **运行时复制成本与清理**：每个 Level109 实例在 Awake 复制 2 个 `LeftGroundLoopItem` 和 2 个 `RightPlatformLoopItem`，均挂在本关根节点并由控制器单独记录；父节点内装饰会扩大复制成本。OnDestroy 只销毁运行时副本，不销毁作者模板。仍需在连续重开/Replay 的 Profiler 与层级中确认没有短帧重叠或残留实例。
- **装饰状态不会自动复位**：循环池只是搬运已有父节点，不会在每次换格时重建或调用子物体复位。当前父节点适合 Sprite、静态摆件或天然可重复的无状态效果；拾取物、一次性触发器、Animator 状态和其它交互对象需要单独设计循环复位后再加入。
- **门字段与 Prefab 序列化一致性**：静态检查已确认 Prefab 使用 `leftExitDoor`、`leftExitDoorSuccessAnim`、`rightExitDoor`、`rightExitDoorSuccessAnim`、`leftCycleRoot`、`rightCycleRoot`、`leftTerrainSource`、`rightTerrainSource`、`moveDoorsToUnlockLoop`、`hideDoorsUntilUnlocked`、`trackingStartDelaySeconds`，旧模板字段名称未残留；Unity Inspector 中的最终显示仍需策划在开发环境确认。
- **动态 Trigger 的真实物理行为**：静态 YAML 不能替代 Unity 导入后的 `OnTriggerEnter2D`、门 Collider 层级和 SuccessAnim 绑定验证；需确认门激活后旧触发器保持 disabled，玩家子 Collider 仍能到达动态触发器。
- **重开/Replay 时序**：teardown 依赖现有 `GameResetEvent`/`GameReplayEvent` 派发顺序；需要在 PlayMode 验证失败重开、胜利后 Replay、旧实例销毁等待窗口和重复事件。
- **临时 Disable/Enable 时序**：静态逻辑已覆盖触发器恢复和 teardown 门禁，但仍需在 PlayMode 确认 Unity 物理回调在根节点重新启用后的实际行为。
- **配置与本地化**：`LevelData.xlsx`/`LevelSort.xlsx` 与运行时 JSON 已有 Level109 行，但 `S_congraktTips_Des_level_109`、`S_answer_Des_level_109` 尚无本地化 Term；结算文本可能显示 key，需文案确认。
- **未完成平台验收**：尚未执行 Unity PlayMode、不同横屏比例、真实碰撞和 Android/iOS 设备测试，均标记为 `uncertain`。WPS 仍运行且未被控制，若工作簿有未保存编辑仍存在外部覆盖风险。

## 2026-08-18 修复验证

- `Level_109.prefab` 静态闭包为 212 个唯一定义、419 次本地引用、0 重复、0 缺失；左右循环父节点、独立地形边界引用、旧 gameplay 循环器 disabled、背景循环器 enabled/原有 `detectRange=0.4` 均已核对。
- Unity 6000.0.59f2 自带 Roslyn 使用当前 Bee `Assembly-CSharp.rsp` 后台完整编译为 `0 error / 107 warning`，`Level109Ctrl` 诊断为 0；没有启动或操作 Unity 前台。
- `git diff --check` 通过，仅报告仓库行尾转换提示。PlayMode 画面、真实碰撞、门触发和移动设备仍未验证。

## 回滚路径

仅回退循环父节点增量时，删除 `LeftGroundLoopItem`/`RightPlatformLoopItem`，把 `Ground_long`/`Platsform` 恢复为关卡根直属并将控制器模板引用改回子地形。完整回退路线修复时，再移除三格池/路线锚点字段和逻辑，恢复旧 gameplay 循环器 enabled、`Ground_long.x=-35.47`、左门模板 `x=-33.51` 与 `leftCycleWidth=25`；背景循环器未改变，共享脚本、共享地面/平台/门资源和其它关卡不需要回滚。

## 2026-08-26 增量：销毁窗口不复位门

- 原始基线会在旧实例销毁前清零进度、移动并隐藏两扇门，存在黑幕未完全覆盖时视觉跳变风险。
- 当前只保留 `isShuttingDown/resetOrReplayReceived` 门禁、Trigger 禁用和事件解绑；新实例负责所有初态。
- 编译为 0 error；未完成门已解锁、角色已接近门时分别触发 Reset/Replay 的 PlayMode 验证，标记为 `uncertain`。
- 回滚可恢复 `ResetDoor()` 与旧字段清零，但会重新引入旧实例表现回写。
