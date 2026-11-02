# Level090 双水槽液面联动风险评估

更新日期：2026-07-27

## 原有行为基线

`Level_090.prefab` 从 Level024 布局继续制作，原先仍绑定 `Level24Ctrl`。该控制器会把可移动 UI 按钮映射为排水口位置、延迟激活排水口、解锁瓶塞并在瓶塞离开后降低 Water；这与 Level090 的“双池封闭、无瓶塞、不可排水”要求冲突。

本轮只在 Level090 内建立独立控制器、双水体和延迟镜头跟随，不修改 Level024 或任何共享实现。

## 受影响文件、资源与系统

- `Assets/Game/Prefabs/Level/Level_090.prefab`
- `Assets/Game/Scripts/Level/MainLevel/Level_90/Level90Ctrl.cs` 及 meta
- `Assets/Game/Scripts/Level/MainLevel/Level_90.meta`
- Level090 本地文档和共享设计记录
- 复用但未修改：`ActorCtrl`、`InteractableWater`、`WaterTriggerHandler`、`GamePlayPanel_005`、共享 HUD、关卡基础流程

明确未修改 `Level_024.prefab`、`Level24Ctrl`、`ActorCtrl`、`InteractableWater`、共享 HUD、Excel/Luban/JSON 配置以及其他关卡。

## 风险清单

| 风险 | 处理方式与证据 | 验证状态 |
| --- | --- | --- |
| 090 继续执行 024 的排水链 | 根控制器替换为本关 `Level90Ctrl`；活动 Prefab 中没有 `Level24Ctrl` GUID/类型，`Outlet`、`Cork` 均失活，瓶塞 Exit 回调为空 | 静态通过；PlayMode 待验证 |
| 只复制水面视觉，角色无法在右池游泳 | 右池新增完整 Water/Buoyancy 树，不是单独 Sprite；Prefab 有 2 个 `InteractableWater` 和 4 个 Actor Enter/Exit 绑定 | 静态通过；两池游泳待验证 |
| Water 视觉、边缘碰撞和浮力区缩放不同步 | 联动统一修改完整 Water 根节点的 X Scale、X/Y Position，子节点随根节点变换 | 结构静态通过；运行时 Collider/Effector 待验证 |
| Water 高度调整后旧人工位移上限提前截停 | `maxDividerTravel=0` 表示不加人工截断，运行时直接按左右当前 Height、位置、宽度和体积计算各自到顶边界；仍可填入正数恢复额外上限 | 当前数值断言通过；PlayMode 待验证 |
| 隔板越界或把水面顶出槽体 | 当前自动边界约为 `-3.72693 / +3.92314`，每次输入和外部位置同步仍会 Clamp，极限液面均为 `Y=0.2092199` | 公式复算通过；拖动手感待验证 |
| 浮点误差造成隔板在顶部抖动或越界 | 每次同步都重新 Clamp 隔板位移和液面，`LateUpdate` 也会收敛外部位置变化 | 源码静态通过；连续拖动待验证 |
| 两个 Buoyancy 区域重叠，Actor 的单一进水状态被错误 Exit 覆盖 | 当前静态几何在两池与隔板之间保留约 `0.05` 的间隙，避免两个水触发区同时覆盖角色 | 静态几何通过；跨池事件顺序待验证 |
| 上涨时液面穿过角色，角色仍停在原世界高度 | 水体变形前确认 Actor 所在 Buoyancy 区域，记录该侧液面净位移，并在 `FixedUpdate` 只增加角色刚体 Y | 隔离编译与源码静态通过；PlayMode 待验证 |
| 同一物理帧内先升后降造成“棘轮式”误抬 | 待托举量按有符号液面差累计，下降会抵消尚未消费的上涨量；算例 `+0.2 -> -0.2` 最终为 `0` | 算例与源码静态通过；高帧率运行待验证 |
| 角色跨槽或离水后仍消费上一池的托举量 | 待托举量绑定选中的 Buoyancy Collider；跨槽、离水、暂停和每次消费后都会清空 | 源码静态通过；跨池/暂停待验证 |
| 连续拖动把角色逐步顶出水面 | 每次累计都以“当前水面 - Actor 碰撞体中心 Y”为上限，并计入尚未消费的位移 | 源码静态通过；顶面碰撞待验证 |
| 水面托举破坏潜水和水平游动 | 仅增加与上涨液面相同的刚体位置 Y，不覆盖水平或潜水速度；不把深处角色直接吸附到绝对水面 | 隔离编译通过；潜水中拖板待验证 |
| 托举所需碰撞体引用缺失后影响原液面玩法 | Actor 刚体/碰撞体或 Buoyancy Collider 缺失时警告并停用托举；显式非空判断避免 Unity 空对象比较误选水槽 | 源码静态通过；缺失引用注入待验证 |
| 移动端触摸选中错误对象或多指串线 | 仅当 `OverlapPointAll` 命中隔板或子节点时开始拖动；移动端当前只跟踪首个 Touch | 源码静态通过；Android/iOS 多指待验证 |
| 暂停后隔板仍随旧手势移动 | `SwitchGameStateEvent` 离开 `PLAYING` 时设置暂停并取消拖动 | 源码静态通过；暂停/恢复待验证 |
| 镜头开局就跟随，出生区构图被破坏 | 开局相机固定；使用 `WorldToViewportPoint`，仅当 Actor 到达 `viewport.x >= 0.5` 才锁存跟随 | 源码与 Prefab 静态通过；PlayMode 待验证 |
| 游泳上下移动误触发镜头 | 触发只比较 viewport X，不使用 Y；跟随时也只修改相机 X | 源码静态通过；两池游泳待验证 |
| 直接复用共享 Follow 导致角色回走后镜头归位 | 未给 090 挂共享 `CameraFollowArea`；本关私有 `_hasStartedCameraFollow` 只置 true、不在本轮复位 | 静态通过；触发后回走待验证 |
| 水位未变化时 `LateUpdate` 提前返回，镜头不更新 | 水位同步改为条件分支，镜头更新放在分支之后独立执行 | 源码静态通过；运行时待验证 |
| 回走时镜头越过出生区左边界 | 跟随目标 X 取 Actor X 与相机开局 X 的较大值，Y/Z 固定为开局值 | 源码静态通过；回走手感待验证 |
| 不同横屏比例下触发点偏离屏幕中线 | 阈值使用 viewport `0.5`，不写死世界 X；相机参数显式序列化为 `0.5 / 0.2` | 静态通过；多宽高比待验证 |
| 右侧未设最大 X 导致门口露出场景外 | 门中心约 `X=12.72`，地面和背景静态几何延伸到门后；为保持居中跟随暂不 Clamp | 几何静态检查通过；门口构图待验证 |
| 当前面板没有 `down_btn` 时产生异常 | 面板、节点和 Actor 调用均有空值保护；缺失时只跳过潜水按钮展示或潜水加速度 | 源码静态通过；实际面板绑定待验证 |
| 保留可移动按钮时误恢复排水能力 | 按钮仍由 `GamePlayPanel_005` 的横向 `UIDrag` 管理；`Level90Ctrl` 不读取其位置，不解锁瓶塞或激活 Outlet | 静态通过；UI 拖动待验证 |
| Prefab 手工 YAML 新增节点存在重复 fileID 或悬空引用 | 当前 192 个 YAML 文档、0 重复 fileID、0 缺失内部引用；脚本 GUID 可解析 | 静态通过；Unity 导入待验证 |
| 关卡制作完成但正式入口无法加载 | `tbleveldata.json`、`tblevelsort.json` 均无 `10090 / Level_090` | 已确认未接入口；配置接入需单独需求 |
| 本地代码影响存档、配置或商业系统 | 控制器不访问 DB、Save、RemoteConfig、资源加载、analytics、广告、IAP、网络或平台 SDK | 源码扫描通过 |
| 把隔离编译误当作 Prefab 与玩法验收 | 最新托举实现隔离编译已通过，但正式 `ScriptAssemblies` 尚未刷新到最新源码；即使程序集成功也不能证明 Prefab Inspector 引用、Collider/Effector 或 PlayMode 行为正确 | 隔离编译通过；正式刷新、Inspector/PlayMode 待验证 |

## 性能与生命周期

- 每帧只进行少量输入状态检查和隔板位置比较；水位仅在隔板位置变化时同步。
- 镜头触发前每帧进行一次 `WorldToViewportPoint`；锁存后每帧进行一次标量 `SmoothDamp`，不扫描对象、不创建集合，也不加载资源。
- `Physics2D.OverlapPointAll` 只在开始鼠标或触摸按下时调用，不在拖动每帧扫描整个场景。
- 角色托举只在水位同步时读取固定数量的 Actor/左右 Buoyancy Bounds，并在每次物理帧执行一次常量时间的待位移消费；不增加对象扫描或集合分配。
- 没有新增 Tween、协程轮询、运行时资源加载、Instantiate 或持续扩张容器。
- 事件订阅在销毁时通过 `EventsUtils.ResetEvents` 解除；潜水按钮延迟任务绑定本组件销毁令牌。
- 上述结论是源码静态结论，移动端帧率和长时间暂停/恢复仍未实测。

## 已完成验证

- Prefab YAML：`192` 个文档、`192` 个唯一 fileID、`0` 重复、`0` 缺失内部引用。
- 组件和事件：`2` 个 `InteractableWater`；两套 Buoyancy 的 Enter/Exit 共 `4` 个 Actor 绑定。
- 排水隔离：活动 Prefab 中旧控制器引用为 `0`；`Outlet`、`Cork` 均为 inactive；旧瓶塞 Exit 回调为空。
- 引用完整性：Actor、隔板、相机、左右 Water 和 `Level90Ctrl` 脚本 GUID 均有可解析序列化引用。
- 镜头配置：Prefab 显式保存中线阈值 `0.5` 和平滑时间 `0.2`；090 中没有共享 `CameraFollowArea / CameraFollowRange` GUID。
- Water 手调一致性：左右 Water 根节点均为 `Y=-5.15`、`Height=6`；对应 EdgeCollider 顶边 local Y 为 `3`，Buoyancy BoxCollider 高度为 `6`。
- 几何公式：当前初始液面 `Y=-2.15`，槽底和槽顶不变；自动模式复算得到约 `-3.72693 / +3.92314` 的到顶边界，两侧极限水面均为 `Y=0.2092199`。
- 边界扫描：未发现编辑器自动写入、DB/Save、RemoteConfig、DOTween、资源加载或实例化入口。
- 配置扫描：`tbleveldata.json`、`tblevelsort.json` 中没有 `10090` 或 `Level_090`。
- 角色托举逻辑：当前水槽选择、有符号净位移累计、水面上限、跨槽/离水/暂停清空与缺失引用降级均完成源码静态复核。
- 净位移算例：同一物理帧前 `+0.2 -> -0.2` 后待托举量归零；连续上涨后小幅回落会等量收缩。
- 隔离编译：Unity 6000.0.59f2 自带 dotnet+csc 使用隔离响应文件；最新结果 exit code `0`、`0 error`、无 Level90 warning，临时 DLL `17:39:13` 晚于源码 `17:38:54`。
- 正式程序集刷新：当前 `Library/ScriptAssemblies/Assembly-CSharp.dll` 为 `17:30:22`，早于最新脚本，尚不能记录为通过。
- 文本差异：`git diff --check` 通过。

## PlayMode 与真机验收清单

- 向左、向右拖动时，隔板、左右槽宽和液面方向正确且连续。
- 上升侧到顶后不能继续拖动，反向拖动可立即恢复。
- 当前 Water 高度下，左侧约在 `delta=-3.72693`、右侧约在 `delta=+3.92314` 停止，画面水面与槽顶一致；再次调整左右 Water 高度或初始位置后，边界仍会自动重算。
- 两池水面、EdgeCollider、Buoyancy 和游泳触发范围始终对齐。
- 角色分别进入/离开左右池，以及跨池时，重力、浮力、游泳动画和潜水操作正常。
- 角色在左右池水面附近时，上涨侧随液面同步上移、下降侧不被下拉；反向拖回后不会残留额外抬升。
- 角色主动潜水、边走边拖、快速往返拖板、跨槽、暂停恢复、击飞以及靠近槽顶障碍时，托举不覆盖操作速度、不穿透且不在恢复后突跳。
- 角色到屏幕中线前镜头完全固定；到线后平滑开始只跟 X，触发后回走仍保持跟随状态且镜头不越过开局 X。
- 暂停/恢复时镜头不漂移、不突跳；成功门附近构图没有露出场景外。
- 16:9、2340:1080 及项目支持的其他横屏宽高比下，中线触发位置和角色构图一致。
- 潜水按钮延迟两秒显示；左下按钮仍可在原限制范围内移动，但不会产生瓶塞或排水口。
- 暂停、恢复、失败、成功、重开和退出后没有残留拖动、错误水位或事件订阅。
- `Outlet`、`Cork` 和排水动画在所有路径都不出现。
- Android/iOS 横屏单指、多指、不同分辨率与代表性设备 60 fps。

## 回滚路径

整体回滚只应恢复本轮修改前的 `Level_090.prefab`，删除 `Level90Ctrl` 及其本地文档，并移除共享记录中的 Level090 日期小节。由于 `Level_090.prefab` 在本轮开始时属于未跟踪资产，回滚前必须先确认可用的本地备份或工作区历史，不能用 Git 命令误删策划原布局。

`Level_024.prefab`、`Level24Ctrl`、`ActorCtrl`、`InteractableWater`、共享 HUD 和配置从未纳入本轮修改，不需要且不得联动回滚。
