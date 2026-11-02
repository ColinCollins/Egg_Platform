# ILevel047Ctrl 功能与风险记录

## 需求与背景

- 日期：2026-08-20。
- ILevel 047 需要一个关卡局部控制器，供 Trigger2D 的本地 UnityEvent 调用；触发后控制一组
  `GameObject` 的 `Transform` 分步向下移动。
- 本次明确使用 `offsetY` 表示每一步的下移距离，并让目标列表、单步距离、步骤间隔和移动次数均可在
  Inspector 配置。

## 原始行为基线

- 修改前不存在 `ILevel047Ctrl.cs`，因此本关没有该分步下移公开 API。
- 工作区已有未跟踪的 `Assets/Game/Prefabs/WannerLevel/ILevel_047.prefab`；本次没有修改该 prefab，
  也没有推断或自动绑定其中的 Trigger、移动目标和参数。

## 影响文件、资源与系统

- 新增 `Assets/Game/Scripts/Level/IWannerLevel/Levels/ILevel047Ctrl.cs` 及 Unity `.meta`。
- 新增本记录及 Unity `.meta`。
- 在 `DevelopMD/已有功能设计与使用记录.md` 追加可复用的 IWanner 本地 Trigger/API 边界事实。
- 未修改 `ILevel_047.prefab`、共享 `ActorCtrl`、`IWannerLevelCtrl`、`OnTrigger2DHandle`、配置表、存档、
  资源加载、UI、分析打点或平台 SDK。

## 修改后行为

1. `targets` 保存需要同步下移的 `List<GameObject>`；每一步读取有效对象的 `Transform`，空引用跳过。
2. `TriggerMoveDown()` 仅在 PlayMode 生效，可绑定到 `OnTrigger2DHandle.OnEnter` 等本地 UnityEvent。
3. 第一步在 API 调用后的 DOTween 更新中立即执行；从第二步开始，每次等待 `interval` 秒。
4. 每一步让所有有效目标沿世界坐标负 Y 方向移动 `offsetY`，X/Z 坐标不变，共执行 `moveCount` 次。
5. `offsetY` 以正数配置；`moveCount` 或 `offsetY` 小于等于 0、目标列表无有效对象时，拒绝启动并通过
   Bear Logger 记录警告。
6. 移动进行中的重复调用会被忽略；完整结束后再次触发会从目标当前位置继续累积下移。
7. `StopMoveDown()` 可取消尚未执行的步骤，但不会恢复已经产生的位移；控制器销毁时也会清理自身 Sequence。

## 数据、资源、事件与性能边界

- 入口沿用 prefab 内本地 Trigger2D/UnityEvent 模式，没有新增 `PlayEvents` 或其它全局事件。
- 只读写已绑定目标的世界坐标，不访问 DB、Save、RemoteConfig、Luban、YooAsset 或资源管理器。
- 每一步只遍历一次 `targets`；没有新增 `Update()`、运行时查找、实例化或持续 GC 分配入口。
- 时间间隔使用项目已有 DOTween 的受 `timeScale` 影响时间，与正常玩法暂停语义一致。

## 验证证据

- Unity 6000.0.59f2 已自动导入新脚本；Editor 日志记录 `Finished compiling in 14s`，未出现
  `ILevel047Ctrl` 编译错误。
- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies` 未能作为第二条编译证据完成：当前生成工程引用的
  `Temp/bin/Debug` 下 133 个程序集不存在，构建在元数据解析阶段以 `CS0006` 终止，尚未进入目标脚本编译；
  本项记录为工程环境阻塞，不覆盖前述 Unity Editor 编译成功证据。
- 静态核对公开 API、三个序列化移动参数、`List<GameObject>`、Bear Logger、PlayMode 门禁和销毁清理路径。
- 静态确认脚本未包含 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB、Save、资源加载、
  全局事件或共享 Actor 修改。
- `ILevel_047.prefab` 的组件挂载、目标列表、Trigger UnityEvent 和实际参数尚未配置，因此 PlayMode 视觉与碰撞
  验证为 `uncertain`。
- Android/iOS 横屏、低帧率、暂停恢复、Reset/Replay 和移动平台碰撞反馈尚未验证，标记为 `uncertain`。

## 风险与待验证项

1. **P1：Prefab 尚未接线。** 在根节点挂载控制器、配置 `targets/offsetY/interval/moveCount`，并把目标
   Trigger 的 `OnEnter` 绑定到 `TriggerMoveDown()` 后，功能才会实际运行。
2. **P1：Reset 基线未确认。** 如果 Reset/Replay 不是销毁并重建本关 prefab，而是复用同一实例，已经产生的世界坐标
   位移不会自动恢复；需要由本关 Reset 流程显式复位目标位置或确认实例重建。
3. **P2：物理对象移动方式。** 当前按需求直接写 `Transform.position`。若目标持有 Dynamic `Rigidbody2D`，可能与物理
   解算发生竞争；应在 prefab 证据确认刚体类型后决定是否改为 `Rigidbody2D.MovePosition`。
4. **P2：重复 Trigger 语义。** 运行中重复调用会忽略，完成后再次调用会继续累积下移；这需要与本关 Trigger 是否一次性
   启用的实际设计共同验证。

## 回滚路径

- 删除 `ILevel047Ctrl.cs` 及其 `.meta`，并移除本记录和共享设计记录中的对应章节。
- 如果后续已在 prefab 接线，先从 `ILevel_047.prefab` 移除控制器组件和 Trigger UnityEvent 绑定；本次当前改动没有
  写入 prefab、配置、存档或共享运行时，因此无需迁移数据。

## 2026-09-07：替换为左右互斥触发（当前有效行为）

### 需求、基线与变更范围

- 用户要求将本关控制器改为两个 API：`TriggerLeft()` 和 `TriggerRight()`，二者只能触发一个，各自提供 callback 绑定 Executor。
- 修改前行为为上述多目标分步下移；本次有意移除 `targets/offsetY/interval/moveCount`、`TriggerMoveDown/StopMoveDown` 及 DOTween 编排。旧实现注释随旧实现移除，因为其已不符合新职责。
- 本节替代前文对当前 API、移动逻辑及回滚方式的说明；前文保留为历史证据。
- 本次仅修改 `ILevel047Ctrl.cs`、本记录及 `DevelopMD/已有功能设计与使用记录.md`。脚本 meta GUID 保持不变。第 47 关 Prefab 与副本均已有用户改动，本次不改其配置。

### 使用方法与调用流

1. 左侧 Trigger 的事件绑定同一个 `ILevel047Ctrl` 实例的 `TriggerLeft()`，右侧绑定 `TriggerRight()`。
2. 控制器 Inspector 的 `On Trigger Left`、`On Trigger Right` 分别绑定目标 Executor 的无参数 `Execute()`。`TriggerExecutor` 继承 `BaseItemExecutor.Execute()`，无需传 Collider2D。
3. 两入口仅在 PlayMode 接受调用，共享 `_hasTriggered`。先将状态锁定，再同步调用该侧 UnityEvent，另一侧及同侧重复调用全部忽略，包括回调重入。
4. 空回调仍消耗选择；回调抛异常也不会解除锁定。禁用再启用不复位，新实例拥有新的触发机会；没有新增 Reset API。
5. 不再直接移动对象，实际表现交给绑定的 Executor；没有新增全局事件、UI、存档、资源加载、SDK 或 Update 开销。

### 验证证据与风险

- `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -v:q`：通过，0 错误、121 个警告；日志位于本机临时目录 `ILevel047-build.log`。
- 静态核对两入口运行态门禁、共享锁以及先锁定后 Invoke 的顺序；未新增运行时层级搜索或编辑器回调。
- 当前 `ILevel_047.prefab` 的 Trigger 仍直接调用 `OnTriggerExecute`，尚未接入新互斥控制器；必须按上述方法重新绑定入口和回调，否则原直接执行链不受互斥控制。
- 副本 `ILevel_047 1.prefab` 仍持久绑定 `TriggerMoveDown`，旧方法删除后此绑定失效，使用副本前需按新需求替换。两个 Prefab 中旧序列化移动字段不再被读取。
- PlayMode 的左右先后、重复碰撞、回调重入及实际 Executor 表现未实测，标记 `uncertain`。同实例复用时不会恢复选择；Reset/Replay 实际实例重建边界未在本次验证，标记 `uncertain`。
- Android/iOS 横屏设备与暂停恢复未实测，标记 `uncertain`；新增入口不含帧循环、Tween 或平台分支。
- 环境缺口：指定的 `../specs/037-remote-resource-loading/plan.md` 不存在；CodeGraph CLI 不可用，采用本地代码和 YAML 读取；知识库检索“关卡开发”无匹配。本关已有功能记录可用。

### 回滚路径

- 仅还原本次 `ILevel047Ctrl.cs` 及文档段落，保留 `.meta` 和用户既有修改。
- 后续若接入新事件，回滚时同步还原相关 Prefab 的旧入口和移动参数；不能把 TriggerLeft/TriggerRight 绑定留给旧脚本。

## 2026-09-07：左右 callback 配置对侧成功门

- IWannerLevelCtrl 新增 SetSuccessDoorTrigger 与 SetEnterDoorAnimCtrl，供本关既有 callback 在 Executor 前动态配置对侧门。
- On Trigger Left：目标为本关 IWannerLevelCtrl，依次设置右门动画、右门触发器，再调用原 Executor；On Trigger Right 对称设置左门。两侧依然受 ILevel047Ctrl 的一次性互斥锁保护。
- 本次没有修改 ILevel047Ctrl 或 Prefab；Inspector 接线仍需配置。触发器设置包含旧门 Success 解绑及新门绑定，动画引用决定成功时角色定位与关门表现。
- 编译通过（0 错误、121 警告），PlayMode 左右映射、旧门失效和新门表现未验证（uncertain）。若其它未被迁移的门仍直接调用 Success，需要移除其独立通关接线。
- 回滚时移除 callback 中两个 API 的调用，再撤回 IWannerLevelCtrl 新增 API；不撤回已有左右互斥逻辑。

## 2026-09-07：Inspector 显式持有双门（取代 callback 手工传参方案）

- 根据用户确认，在 ILevel047Ctrl 新增五个 SerializeField：levelCtrl、leftSuccessDoorTrigger、leftEnterDoorAnimCtrl、rightSuccessDoorTrigger、rightEnterDoorAnimCtrl。Inspector 按 Level Controller、Left Door、Right Door 分组拖入本关组件。
- 原行为依赖 callback 手工调用两个 setter；现 TriggerLeft 内自动设置右门，TriggerRight 内自动设置左门。先校验所选门引用完整，再设置动画和触发器，锁定一次性选择后执行原 callback。callback 只需绑定 Executor，若之前手工加过门 setter，应移除以免覆盖自动选择。
- 缺少本关控制器或所选门任一组件时，通过 Bear.Logger 报错，拒绝 callback 且不消耗选择。引用不使用运行时查找兜底。
- 本次仅修改本关脚本及本地文档；未改 Prefab、共享 IWannerLevelCtrl 或门动画。Inspector 字段已提供，实际组件拖拽绑定由关卡制作时完成。
- 验证：Assembly-CSharp 构建通过（0 错误、121 警告）；静态核对 Left→右门、Right→左门及 callback 顺序。PlayMode、双门实际接线、成功动画、移动端均未实测，标记 uncertain。共享 setter 在结算后拒绝赋值的既有边界不变。
- 回滚：仅撤回本次五个字段、ConfigureDoor 及入口调用、Bear 日志引用；保留既有互斥 API 和 callback。回滚后需要恢复 callback 手工配置门的方式。
