# Level095 可推动入口门吸水功能说明

更新日期：2026-07-28

## 需求与背景

Level095 基于 Level024 的场景与潜水操作制作进阶关卡，但不修改 Level024。Level024 的瓶塞、排水口和出水动画在本关中不显示，也不能触发原排水链；Level095 唯一允许的排水方式，是把入口门推入水中后点击入口门，使门持续播放吸入表现并把水吸干。

入口门在开场阶段继续承担角色出生动画。角色走出门框后，入口门才转为可被角色推动的物理物体。门在干区被点击时只播放一次开门和关门；门进入水体后再次点击，才启动一次完整吸水流程。

2026-07-28 PlayMode 反馈确认初版入口门过轻，并且会受 `Water/Buoyancy` 的强浮力停在水面。本次修正要求门仍可被角色推动，但推动阻力明显提高；进入水中后必须持续下沉，不能再漂浮。

2026-07-28 策划进一步要求把 `Rigidbody2D` 直接挂在 Level095 的 StartDoor 实例上，由门自身 Inspector 作为质量等静态物理参数的权威配置；运行时脚本不再追加刚体或覆盖这些参数。

## 影响范围

- `Assets/Game/Prefabs/Level/Level_095.prefab`
- `Assets/Game/Prefabs/Level/Level_095.prefab.meta`
- `Assets/Game/Scripts/Level/MainLevel/Level_95/Level95Ctrl.cs`
- `Assets/Game/Scripts/Level/MainLevel/Level_95/Level95Ctrl.cs.meta`
- `Assets/Game/Scripts/Level/MainLevel/Level_95.meta`
- 本目录的功能说明和风险评估
- `DevelopMD/已有功能设计与使用记录.md` 中的 Level095 日期小节

仅复用且未修改：

- `Assets/Game/Prefabs/Level/Level_024.prefab` 与 `Level24Ctrl`
- 公共 `StartDoor`、成功门 `door_0` 和它们的 Spine 资源
- `ActorCtrl`、`ActorSpineCtrl`、`InteractableWater` 与 Water 预制体
- `BaseLevelCtrl`、`PlayCtrl`、共享 HUD 和 `GamePlayPanel_005`
- 公共 `AudioManager`、DOTween、UniTask 和输入工具
- Excel、Luban 生成物、JSON 配置、存档和平台系统

Prefab 中策划已经调整的 StartDoor、Camera、FallArea 和 Actor Transform 保持原值，没有被本次功能替换覆盖。

## Level024 基线

- `Level24Ctrl` 订阅 `PlayerDiveEvent`，调用 `ActorCtrl.AddDiveVelocity()`，并在开局隐藏 `Root/down_btn` 两秒后显示。
- Level024 会把可移动 UI 按钮映射到 `Outlet`，按钮移动达到阈值后解锁瓶塞。
- 瓶塞离开触发区后，Level024 播放 `drainage`，移动 Water，并显示 `WaterOut`。
- 上述瓶塞排水链只作为复制来源的行为基线，不属于 Level095。

## Level095 行为

### 入口门物理启用

- 根节点使用本关私有 `Level95Ctrl`，不再由 `Level24Ctrl` 接管。
- StartDoor 保留原开场 Trigger，开场动画与角色出生流程仍由现有 `BaseLevelCtrl / EnterDoorAnimCtrl` 负责。
- Prefab 将入口门的 `walkOutSeconds` 设为 `0.4`，让角色先自然走出门框。
- 只有在 `BaseLevelCtrl.ActorCtrlable`、当前状态为 `PLAYING`，并且 Actor 与门碰撞体已经分离后，才启用门的实体物理，避免开场阶段把角色卡在门内。
- Level095 的 StartDoor 嵌套实例直接挂有本关专用 `Rigidbody2D` Override；共享 `StartDoor.prefab` 不增加刚体。Prefab Inspector 中该组件为 `Dynamic / Simulated=true`，可直接显示并调整 Mass 等参数。
- `Awake` 在首个物理帧前把入口门临时切为 `Kinematic / Simulated=true`：门不会受重力下落，同时 Collider 仍参与显式距离查询，保证角色离开门框的启用门禁可继续工作。
- 启用时只修改 Level095 场景实例：StartDoor 根切到 `Stuff` 层，原 `BoxCollider2D` 从 Trigger 改为实体，并打开门上已有刚体的物理模拟。缺少该刚体时控制器记录错误并停止启用门物理，不再运行时静默补组件。
- 当前门上 Rigidbody2D 的 Inspector 初始参数为 `Mass=30`、`Gravity Scale=1.8`、`Freeze Rotation`、`Interpolate`、`Continuous` 和 `Never Sleep`。`Level95Ctrl` 不覆盖质量、重力、阻尼、约束、插值、碰撞检测或休眠模式；策划可直接在 StartDoor 的 Rigidbody2D 上调整质量。
- 只对 Level095 的 StartDoor Collider 与本关 `Water/Buoyancy` Collider 调用 `Physics2D.IgnoreCollision(..., true)`。该局部碰撞对不再形成浮力接触，因此门入水后由自身重力持续下沉；Actor、其他 `Stuff`、Water 层和共享 `BuoyancyEffector2D` 参数均不修改。

### 点击与干区开关门

- 触摸输入优先于鼠标兼容输入；有 Touch 的帧不再重复处理鼠标。
- Touch 按实际 `fingerId`、鼠标按屏幕坐标调用共享 `InputUtils`，命中 HUD 时不触发世界门点击。
- 世界点击使用全部 2D 命中结果查找 StartDoor 或其子节点，因此门进入 Water Collider 后，水体不会因为先命中而抢走门点击。
- 门在干区被点击后播放 `door_open`，等待 `0.8` 秒，再播放 `dose_close` 和 `door_idle`；期间锁住重复点击。
- 干区开关门复用 `openDoor / closeDoor` 音效，不改变成功门或公共入口门脚本。

### 入水判定与持续吸水

- 门 Collider 与 `Water/Buoyancy` Collider 的几何形状发生重叠，或两者距离不大于 `0.02` 时，当前实现判定门已入水。
- 浮力接触虽然被指定碰撞对忽略，入水判定仍显式调用 `Physics2D.Distance(_doorCollider, waterVolume)`，不依赖 Trigger 回调或碰撞接触；“忽略浮力后仍能点击吸水”需要 PlayMode 复验。
- 第一次有效入水点击后立即锁存 `_isDraining`，后续点击不会重复启动排水。
- 门刚体速度清零并临时改为 Kinematic，防止吸水过程中继续漂移。
- 门播放 `door_open -> door_open_idle`；控制器在 `Awake` 中有上界地克隆一次成功门 `door_0/ribbon` 到 StartDoor 下，吸水时循环播放 `Inhalation_effects`。
- 吸入 ribbon 当前按 X 轴镜像，跟随被推动后的 StartDoor，不新增运行时资源加载。
- 同时播放既有 `drainage` 音效，并用 DOTween 在 `4` 秒内把 Water 根节点移动到本地 `Y=-8`。
- 完成后关闭 Water 子树中的 Collider 和 Effector，调用 Actor 的既有 `ExitWater(null)` 退出游泳状态，停止吸入表现，再关门并把门刚体恢复为 Dynamic。
- 本关没有瓶塞排水；“门吸水”是唯一水体排空路径。

### 暂停、恢复与重开

- `PlayCtrl.CheckState(GamePlayStateName.PLAYING)` 是当前状态的权威来源。
- `SwitchGameStateEvent` 用于即时同步；`Update` 仍会轮询并在状态变化时统一应用，覆盖 Reset/Replay 直接切状态机而没有发送该事件的路径。
- 切出 `PLAYING` 时同时暂停排水 Tween、入口门刚体、门 Spine、吸入 Spine 和本关持有的音效；恢复后统一继续。
- 门刚体首次启用以及每次恢复 `PLAYING`、重新打开物理模拟后，都会重申 StartDoor 与 Buoyancy Collider 的指定碰撞忽略；该操作只发生在初始化或状态切换，不在每帧重复执行。
- 干区开关门和排水完成后的异步等待按帧累计，只有处于 `PLAYING` 时才增加 `Time.deltaTime`。项目暂停不修改 `Time.timeScale`，因此该门禁避免暂停期间开关门序列继续推进。
- 销毁时解除事件、终止本关排水 Tween，并定向停止本关持有的门和排水音效。

### 潜水按钮与旧排水隔离

- `PlayerDiveEvent -> Level95Ctrl -> ActorCtrl.AddDiveVelocity()`，保持 Level024 的潜水输入语义。
- `Root/down_btn` 继续采用开局隐藏两秒再显示的节奏。
- 左下角可移动按钮继续使用 `GamePlayPanel_005` 的既有拖动能力；095 不读取其位置，也不再把它映射到排水口。
- `Cork`、`Outlet`、`WaterOut` 在 Level095 Prefab 中均为 inactive。
- `Level95Ctrl` 初始化时会再次强制关闭上述三个旧排水物体，避免本关实例被误勾选后恢复瓶塞排水画面。
- 旧 `Level24Ctrl.OnCorkExit` 的 UnityEvent 回调已清空，活动 Prefab 中没有 `Level24Ctrl` 脚本绑定。

## 数据、资源与系统流

| 类型 | Level095 行为 |
| --- | --- |
| 事件 | 只订阅既有 `PlayerDiveEvent` 和 `SwitchGameStateEvent`；销毁时解除订阅 |
| 输入/UI | 运行时查找 `Root/down_btn`；世界点击过滤 UI，不修改共享面板 |
| 物理 | StartDoor 实例预挂 Inspector 可调的 Rigidbody2D；脚本只控制开场/吸水阶段的 `Dynamic / Kinematic` 与 `simulated` 状态，并忽略门与本关 Buoyancy Collider 的指定碰撞对 |
| Spine | 复用入口门动画；从本关成功门实例克隆一次 ribbon，循环播放 `Inhalation_effects` |
| 音频 | 复用 `openDoor`、`closeDoor`、`drainage`；只暂停和停止本关持有的 Sound |
| 水体 | 只移动 Level095 的 Water 根，并在吸干后关闭其 Collider/Effector |
| 资源 | 无 Resources、Addressables 或网络加载；运行时实例化量固定为一个 ribbon |
| 存档 | 不读写 DB 或 Save，不新增存档字段或迁移 |
| 配置 | 不修改 Excel、Luban 或 JSON；`10095 / Level_095` 尚未登记正式关卡配置 |
| 商业化 | 不新增埋点、广告、IAP、RemoteConfig、网络或平台 SDK 调用 |

## 已完成验证

- Unity 6000.0.59f2 自带 dotnet 与 Roslyn，基于现有 `Assembly-CSharp` 响应文件显式加入 `Level95Ctrl.cs` 进行隔离编译，结果为 exit code `0`、`0 error`，没有 Level095 诊断；输出只有项目既有 warning。
- Prefab 静态引用检查为 `178` 个 YAML 定义、`178` 个唯一 fileID、`0` 重复、`0` 缺失内部引用。
- `Level95Ctrl` 脚本 GUID `fd517e9bf1e54f85a0d71afa4e7731ef` 与 meta、Prefab 绑定一致。
- StartDoor 实例新增的 Rigidbody2D fileID 为 `950950000000000001`，Added Component 目标只指向本关 StartDoor 实例；静态参数为 `Mass=30 / Gravity Scale=1.8 / Dynamic / Simulated=true`。
- `Level95Ctrl` 已无 `doorMass / doorGravityScale` 字段、运行时 `AddComponent<Rigidbody2D>()` 或静态刚体参数赋值；源码中只有一处门与 `waterVolume` 的局部 `Physics2D.IgnoreCollision` 调用。
- 源码静态确认 `Awake`/引用解析阶段在门物理未开放时使用 `Kinematic / Simulated=true`，正式启用前先暂停模拟、切实体 Collider 和 `Dynamic`，再按当前游戏状态恢复模拟。
- Actor、Water、Buoyancy Collider、Camera、StartDoor 五个序列化引用均已解析到对应类型。
- `Cork / Outlet / WaterOut` 均为 inactive；旧 `OnCorkExit` 事件为空；Prefab 中没有活动 `Level24Ctrl` 绑定。
- 入口门 `walkOutSeconds=0.4` 已静态确认。
- 源码扫描未发现 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、RemoteConfig 或运行时资源加载入口。
- `tbleveldata.json` 与 `tblevelsort.json` 中均未找到 `10095 / Level_095`。
- `Level95Ctrl.cs`、两份本地文档及其文档 meta 的尾随空白检查已通过；共享记录的 `git diff --check` 已通过。

## 未验证项

- Unity Inspector 中 Level095 的 Missing Script、序列化引用、StartDoor Rigidbody2D 显示与 Prefab `Dynamic / Simulated=true` 参数。
- PlayMode 中角色出生后门不会提前实体化，且角色能稳定越过门、站到左侧并把门向右推入水中。
- 开场临时 `Kinematic` 时门 Collider 的角色分离距离查询、出生 Trigger 与门动画是否保持原行为。
- StartDoor Rigidbody2D 当前 `Mass=30` 以及策划后续手调质量后的推动阻力是否符合手感，角色是否仍能稳定把门推入水中。
- 门接触水面后是否不再平衡漂浮，并能完整下沉到水底；指定碰撞对被忽略后，门与地面、Actor 及非 Buoyancy 水体碰撞是否保持正常。
- 干区点击开关门、水体 Collider 覆盖下点击门、重复点击门。
- 门刚接触水面与明显浸入水体时的判定边界；`Physics2D.IgnoreCollision` 后 `Physics2D.Distance` 是否仍能稳定触发吸水，以及当前 `0.02` 接触容差是否符合最终策划手感。
- `door_0/ribbon` 克隆后的朝向、位置、循环动画和可见层级；StartDoor SortingGroup 为 `101`、Water 为 `201`，吸入表现是否被水层压暗或遮挡需要目测。
- 四秒吸水过程中 Water、Spine 和音效是否同步，吸干后 Actor 是否正确退出游泳，门是否恢复 Dynamic。
- 暂停/恢复分别发生在开门、吸水、关门阶段，以及 Reset/Replay 没有 `SwitchGameStateEvent` 时的恢复。
- 失败、重开、退出、成功、重复进入和资源销毁流程。
- HUD 点击穿透、多指竞争、Android/iOS 横屏触摸与代表性设备 60 fps。
- 正式选关入口；补齐 `10095 / Level_095` 配置前，不能通过现有正式配置链进入本关。

## 回滚

若只撤回本次“Prefab 直接挂刚体、Inspector 调参”调整，从 Level095 StartDoor 实例移除 fileID `950950000000000001` 的 Rigidbody2D Added Component，并恢复控制器原先的运行时 AddComponent 与静态参数配置。不要把刚体添加到共享 `StartDoor.prefab`。

若只撤回“门加重并下沉”修正，将 StartDoor Rigidbody2D 的 `Mass` 从当前 `30` 恢复为 `10`，并移除 `ConfigurePushableDoor()` 内及恢复 `PLAYING` 路径中对 `IgnoreDoorBuoyancy()` 的两处调用，以及该私有方法。不要通过修改共享 Water 密度、Effector Mask 或 Layer 来回滚。

如需整体撤回 Level095，应从本轮修改前的本地备份、工作区历史或确认过的基线恢复 `Level_095.prefab`，删除 `Level_95` 中新增的控制器与本地文档，并移除共享记录中的 `2026-07-28：Level095 可推动入口门吸水` 小节。

不要修改或回滚 `Level_024.prefab`、`Level24Ctrl`、公共 StartDoor、成功门 `door_0`、共享 Actor、Water、HUD 或音频系统。
