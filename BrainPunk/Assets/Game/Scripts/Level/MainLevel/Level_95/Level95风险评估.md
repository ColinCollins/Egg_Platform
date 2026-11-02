# Level095 可推动入口门吸水风险评估

更新日期：2026-07-28

## 原有行为基线

`Level_095.prefab` 基于 Level024 布局继续制作。Level024 的控制器会把可移动 UI 按钮映射为排水口位置、解锁瓶塞，并在瓶塞离开后移动 Water 和播放 `WaterOut`；这与 Level095 “没有瓶塞、不能用原方式放水，只能把入口门推入水中吸干”的规则冲突。

本轮只在 Level095 内建立私有控制器和门吸水流程，不修改 Level024、公共 StartDoor、成功门、Actor、Water、共享 HUD 或关卡基础流程。

2026-07-28 PlayMode 反馈确认初版 `mass=10 / gravityScale=1.8` 的门推动阻力不足，并会受密度为 `8` 的 BuoyancyEffector2D 托在水面。本次局部修正把门质量提高为 `30`，并只忽略 StartDoor Collider 与本关 Buoyancy Collider 的指定碰撞对。

2026-07-28 策划要求把 Rigidbody2D 直接挂到 Level095 StartDoor 实例，并自行在门的 Inspector 调整质量。控制器因此不再运行时追加刚体，也不再覆盖质量、重力、阻尼、约束等静态刚体参数。

## 受影响文件、资源与系统

- `Assets/Game/Prefabs/Level/Level_095.prefab` 及 meta
- `Assets/Game/Scripts/Level/MainLevel/Level_95/Level95Ctrl.cs` 及 meta
- `Assets/Game/Scripts/Level/MainLevel/Level_95.meta`
- Level095 本地功能说明、风险评估和共享设计记录
- 运行时复用：`BaseLevelCtrl`、`PlayCtrl`、`ActorCtrl`、`ActorSpineCtrl`、DOTween、UniTask、Physics2D、SoundsGood

明确未修改 `Level_024.prefab`、`Level24Ctrl`、公共 StartDoor/door_0 资源、共享 Actor/Water/HUD、Excel、Luban、JSON 配置、存档、埋点和平台 SDK。

## 风险清单

| 风险 | 处理方式与证据 | 验证状态 |
| --- | --- | --- |
| 095 继续执行 024 的瓶塞排水链 | 根控制器替换为私有 `Level95Ctrl`；`Cork / Outlet / WaterOut` 失活，旧 `OnCorkExit` 为空，没有活动 `Level24Ctrl` 绑定 | 静态通过；PlayMode 待验证 |
| 门在出生动画期间变成实体，把 Actor 卡住 | 等待 `ActorCtrlable`、`PLAYING` 且 Actor Collider 与门分离后才启用门物理；`walkOutSeconds=0.4` | 源码和 Prefab 静态通过；PlayMode 待验证 |
| 门加重后无法被角色推动，或仍然过轻 | Level095 StartDoor 实例预挂 Rigidbody2D，当前 Inspector 初始值为 `Mass=30 / Gravity Scale=1.8`；脚本不再覆盖静态参数，策划可直接调质量 | 参数静态通过；最终物理手感待 PlayMode 复验 |
| Rigidbody2D 丢失后运行时参数来源不明确 | `ConfigurePushableDoor()` 要求门上已有 Rigidbody2D；缺失时记录错误并停止启用门物理，不再运行时补组件掩盖 Prefab 配置错误 | 源码静态通过；Unity Inspector 待验证 |
| 预挂 Dynamic 刚体在出生阶段下落，或关闭模拟后角色分离门禁失效 | Prefab 保持 `Dynamic / Simulated=true` 便于 Inspector 调参；`Awake` 在首个物理帧前临时切 `Kinematic / Simulated=true`，阻止重力且保留 Collider 距离查询，角色分离后再切回 Dynamic | 生命周期与参数静态通过；出生流程待 PlayMode 验证 |
| 门受强浮力停在水面 | 只对 StartDoor Collider 与本关 `Water/Buoyancy` Collider 调用 `Physics2D.IgnoreCollision`；不修改共享 Water、Effector Mask、Layer 或其他 `Stuff` | API 与源码静态通过；完整下沉待 PlayMode 复验 |
| 玩家无法到门左侧，不能向右推门 | 当前 Actor 出生在门右侧，门实体化后需要先跳过约到世界 `Y=3.22` 的门 | 几何风险已确认；必须 PlayMode 验证 |
| 干区点击直接排水 | 先用门与 Buoyancy Collider 的距离判断；未入水只播放一次开门/关门 | 源码静态通过；点击流程待验证 |
| 忽略浮力后无法识别门已入水 | 入水判断显式使用 `Physics2D.Distance(_doorCollider, waterVolume)` 的几何距离，不依赖被忽略的碰撞/Trigger 回调 | API 语义与源码静态通过；入水点击待 PlayMode 复验 |
| 门只擦到水面就开始吸水 | 当前任意重叠或距离 `<=0.02` 即视为入水，可能早于玩家视觉上的“明显浸入” | 待策划手感确认 |
| Water Collider 抢走点击，门入水后点不到 | 点击检查全部 Physics2D 命中结果，再筛选 StartDoor 或子节点 | 源码静态通过；PlayMode 待验证 |
| 点击 HUD 同时触发门 | Touch 按 `fingerId`、鼠标按屏幕坐标调用 `InputUtils`；有 Touch 时不处理模拟鼠标 | 源码静态通过；移动端待验证 |
| 多次点击重复启动 Tween、声音或吸入特效 | `_doorClickInProgress` 锁门动画，`_isDraining` 一次性锁排水 | 源码静态通过；连点待验证 |
| 吸入表现找不到或重复实例化 | `Awake` 从本关成功门实例查找 `door_0/ribbon`，只克隆一个；缺失时警告，不循环查找或生成 | Prefab 模板静态存在；运行时待验证 |
| 吸入 Spine 被水层遮挡 | ribbon 是 StartDoor 子节点；StartDoor SortingGroup `101`，Water `201` | 未目测，待 PlayMode |
| 暂停时排水停止但门动画/声音继续，或恢复后门重新受浮力 | 统一状态方法同步 Tween、刚体、门 Spine、吸入 Spine 和本关 Sound；恢复物理模拟后重新调用本关 `IgnoreDoorBuoyancy()`；异步等待只在 `PLAYING` 累计 | 隔离编译与源码静态通过；暂停阶段矩阵待验证 |
| 重开后门永久禁用 | 不再用事件缓存作为 `IsPlayingState` 前置条件；每帧以 `PlayCtrl.CheckState(PLAYING)` 同步，覆盖 Reset/Replay 直接切状态机 | P1 源码修复并编译通过；重开待验证 |
| 吸水过程中门继续滚动或离开水面 | 开始排水时清零速度并临时改 Kinematic | 源码静态通过；运行时待验证 |
| 水视觉下降但 Actor 仍保持游泳状态 | 完成后关闭 Water 子树 Collider/Effector，并调用既有 `Actor.ExitWater(null)` | 源码静态通过；水中角色待验证 |
| 排水完成后门不再参与物理 | 关门收尾 finally 中恢复 Dynamic，并按当前权威状态恢复 simulated | 源码静态通过；完成/暂停交叉待验证 |
| 关卡退出后 Tween、事件或声音残留 | `OnDestroy` 解除订阅、Kill 排水 Tween、定向 Stop 本关持有 Sound | 源码静态通过；退出待验证 |
| 运行时实例化或 Update 造成移动端开销 | ribbon 固定实例化一次；Update 只做常数次状态查询，输入射线只在按下时执行 | 结构风险较低；设备性能待验证 |
| Prefab 手工 YAML 出现重复 fileID 或悬空内部引用 | 新增刚体后为 `178` 个定义、`178` 个唯一 fileID、`0` 重复、`0` 缺失内部 fileID 引用 | 静态通过；Unity 导入待验证 |
| 正式入口无法加载 Level095 | `tbleveldata.json / tblevelsort.json` 尚无 `10095 / Level_095` | 已确认未接入口；配置排序需单独确认 |
| 局部实现影响已发布关卡 | 只新增 Level095 私有脚本并修改 Level095 Prefab；共享代码与资源未修改 | 变更范围静态通过 |

## 性能与生命周期

- 默认目标仍是 Android/iOS 横屏代表设备平滑 60 fps。
- 每帧只执行常数次 `PlayCtrl` 状态读取；只有指针按下时才分配 Physics2D 命中数组和 UI Raycast 结果。
- 水体只创建一个 DOTween，吸入 ribbon 只实例化一次，不存在每帧 Instantiate、资源加载或全场景扫描。
- 异步序列使用 `GetCancellationTokenOnDestroy()`；销毁后不会继续访问关卡实例。
- 未增加 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode` 或编辑器态运行链。

## 已完成验证

- Unity 6000.0.59f2 Roslyn 隔离编译：exit code `0`、`0 error`，无 Level095 诊断，只有项目既有 warning。
- Level095 Prefab YAML：`178` 个定义、`178` 个唯一 fileID、`0` 重复、`0` 缺失内部 fileID 引用。
- 脚本 GUID 与 meta/Prefab 一致；五个序列化引用类型正确。
- StartDoor 实例 Rigidbody2D Added Component 的 fileID、GameObject 归属和参数已静态确认：`Mass=30 / Gravity Scale=1.8 / Dynamic / Simulated=true`，共享 StartDoor 源 Prefab 未增加刚体。
- `Level95Ctrl` 已无质量/重力序列化字段、运行时 AddComponent 或静态参数覆盖；门/浮力指定碰撞对的 `Physics2D.IgnoreCollision` 只在 Level095 脚本中出现一次。
- 引用解析在门物理未开放时切 `Kinematic / Simulated=true`；正式启用顺序为暂停模拟、切 Stuff/实体 Collider、恢复 Dynamic、再按权威游戏状态恢复模拟。
- 旧排水对象失活、旧回调清空、`Level24Ctrl` 活动绑定为零。
- 代码静态扫描未发现编辑器生命周期、DB、Save、RemoteConfig 或运行时资源加载入口。
- `Level95Ctrl.cs`、两份本地文档及其文档 meta 的尾随空白检查通过；共享记录的 `git diff --check` 通过。

隔离编译不是 Unity 正式程序集、Inspector、Prefab 导入或玩法验收。当前正式 `Library/ScriptAssemblies/Assembly-CSharp.dll` 早于 Level095 脚本，不能据此声称 Unity 已完成正式编译。

## PlayMode 与真机验收清单

- 进入关卡：出生动画、角色离门、门物理启用时机。
- 开场门体：Prefab Dynamic 参数可见，运行时首帧临时 Kinematic 不下落，Collider 距离门禁能在角色离开后正常放行。
- 推门：从门左侧向右推动，确认当前 Inspector `Mass=30` 或策划手调值明显增加阻力但仍可完成推门；覆盖地面碰撞、水中运动和极端受力。
- 下沉：门从轻触水面到完全入水都不能形成漂浮平衡，最终应完整沉到水底；Actor 和其他物体的浮力保持不变。
- 点击：干区开关门、水体覆盖后命中门、HUD 点击、鼠标和多 Touch。
- 入水边界：轻触水面、部分浸入、完全浸入，确认忽略指定碰撞对后 `Physics2D.Distance` 仍能启动吸水，并确认最终判定手感。
- 吸水：只启动一次、四秒完整排水、持续 Spine、声音同步和 ribbon 层级。
- 水中角色：排水时不异常穿透，完成后退出游泳并恢复陆地状态。
- 暂停矩阵：开门等待、排水中、关门等待分别暂停/恢复。
- 浮力恢复矩阵：门在干区、刚接触水面、完全浸入三种位置分别暂停/恢复，确认恢复后仍持续下沉。
- 生命周期：失败、Reset、Replay、退出、成功、重复进入，无旧回调或声音残留。
- 显示与性能：16:9、2340:1080 及项目支持比例；Android/iOS 横屏真机 60 fps 目标。
- 正式入口：配置排序确认后补齐 `10095 / Level_095`，再验证解锁、进入、成功、存档与埋点基线。

## 回滚路径

只回滚本次 Inspector 参数权威调整时，从 Level095 StartDoor 实例移除 fileID `950950000000000001` 的 Rigidbody2D Added Component，并恢复 Level95Ctrl 的运行时 AddComponent 与静态参数赋值；不要修改共享 StartDoor。

只回滚门加重和下沉行为时，将 StartDoor Rigidbody2D 的 `Mass` 恢复为 `10`，并移除首次物理启用、恢复 `PLAYING` 路径对 `IgnoreDoorBuoyancy()` 的调用及该私有方法。不要修改共享 BuoyancyEffector2D 的密度、Collider Mask 或 Water Layer。

整体回滚时恢复或删除 `Level_095.prefab`，删除 `Level_95` 新增脚本与文档，并移除共享记录中的 Level095 小节。不要联动修改或回滚 Level024、公共 StartDoor、成功门 `door_0`、Actor、Water、HUD、配置、存档或平台系统。

若只回滚门吸水能力，也必须从确认过的 Prefab 基线同时恢复 Level095 根控制器绑定、`OnCorkExit` 回调以及 `Cork / Outlet / WaterOut` 的活动状态；不能只移除 `Level95Ctrl`，否则会留下一个既没有门吸水、也没有完整旧排水链的半回滚状态。恢复旧 024 排水语义前必须先确认是否符合新的关卡需求。
