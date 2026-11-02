# Level64 功能说明

## 脚本分工

- `Level64Ctrl`
  - 关卡主控。
  - 负责砖块计数、开门、拖拽旋转联动、`Level64BlockCtrl` 初始化，以及 actor 级别的取砖/放砖接口。
- `Level64AICtrl`
  - AI 调度器。
  - 负责从起始门生成 AI、根据区域触发调用主控 API、切换 AI 的运输 state，以及关卡完成后的退场表现。
- `Level64ActorCtrl`
  - AI/玩家共用的 actor 表现层。
  - 负责移动与动画表现，同时保存当前运输 state。
  - 只提供 state 读写接口，不执行 AI 决策。
- `Level64BlockCtrl`
  - 被动砖块控制器。
  - 负责对象池、output/input 双层缓存、格位计算、回收、停止补排。
  - 不持有 `Level64Ctrl`，不自己决定初始化时机。
- `Level64BlockItem`
  - 单个砖块对象。
  - 负责 sprite 切换、`SortingGroup` 排序抬升/恢复，以及对象池回收重置。

## 初始化关系

### `Level64Ctrl.Start()`

- 订阅 `dragRotate.OnAngleChanged`
- 调用 `InitializeBlockCtrl()`
- 刷新剩余数量文本

### `Level64Ctrl.InitializeBlockCtrl()`

主控统一计算池参数并下发给 `Level64BlockCtrl`：

- `configuredMaxPoolSize = maxBlockCount`
- `configuredInitialPoolSize = min(blockCtrl.GetInitialOutputCapacity(), maxBlockCount)`

然后顺序调用：

- `blockCtrl.ConfigurePool(configuredInitialPoolSize, configuredMaxPoolSize)`
- `blockCtrl.Initialize()`

### `Level64BlockCtrl.Initialize()`

- 使用 `Level64Ctrl` 下发的 `initialPoolSize` / `maxPoolSize`
- 注册 `Level64BlockItem` 对象池
- 生成初始 output 缓存
- 通过 `_isInitialized` 防止重复初始化

## 对象池与生成限制

### 池参数来源

- `initialPoolSize` 和 `maxPoolSize` 由 `Level64Ctrl` 决定。
- `Level64BlockCtrl` 只负责接收与执行。

### 初始池大小

- `blockCtrl.GetInitialOutputCapacity()` 返回：
  - `max(1, outputCacheRows) * max(1, size.x) * max(1, size.y)`
- `Level64Ctrl` 用这个值与 `maxBlockCount` 取最小值，作为初始池大小。

### 最大池大小

- `maxPoolSize` 直接受 `maxBlockCount` 限制。

### Output 生成限制

`GenerateOutputBlocks()` 和 `GenerateOutputLayer(int layer)` 都会读取剩余可生成容量：

- `remainingCapacity = maxPoolSize - GetOutputCacheCount()`，当 `maxPoolSize > 0` 时生效
- 当剩余容量为 `0` 时，停止继续生成
- `SetOver(true)` 后，不再补新后排

## Output 区逻辑

### 双层缓存

- Output 默认按前后两排生成，沿 `z` 轴分层。
- 每一排都是一个完整的 `size.x * size.y` 网格。
- `LayerIndex = 0` 表示前排。
- `LayerIndex = 1` 表示后排。

### 取砖规则

- `OutputTriggerEnter()` 总是优先从前排取。
- 当前排取空且 `outputCacheRows > 1` 时：
  - 后排整体前移，变成新前排
  - 若未 over 且仍有池容量，则尝试生成新的后排

### Output 相关 API

- `OutputTriggerEnter()`
  - 从 output 当前前排取出一个砖块。
- `GetOutputCacheCount()`
  - 返回 `_outputBlockItems` 当前非空数量。
- `GetInitialOutputCapacity()`
  - 返回当前 output 配置下的初始容量估算值。
- `SetOver(bool isOver)`
  - 控制 output 是否继续补新排。

## Input 区逻辑

### 双层缓存

- Input 也使用前后两排缓存，沿 `z` 轴分层。
- `LayerIndex = 1` 作为后排，优先填充。
- 后排填满后，再填前排 `LayerIndex = 0`。
- 当前后两排都满时：
  - 旧后排整排回收
  - 旧前排整体后移，变成新的后排
  - 新放入的砖块从新的前排重新开始填

### 单排内部顺序

- 从左到右
- 从下到上
- 左下角是每一排的第一个格子

### Input 相关 API

- `GetInputAvailablePosition()`
  - 纯查询接口。
  - 返回下一块砖如果放入 input 时的世界坐标预览。
  - 不修改内部状态。
- `InputTriggerEnter(Level64BlockItem item)`
  - input 放置的唯一提交入口。
  - 内部统一完成双层换排、格位计算和最终落位。

## `Level64ActorCtrl` 运输 state API

`Level64ActorCtrl` 现在持有运输状态，但不执行 AI 逻辑。

### State 定义

- `TransportState.ToOutput`
  - 朝 output 方向运输。
- `TransportState.ToInput`
  - 朝 input 方向运输。

### State API

- `GetTransportState()`
  - 返回当前运输 state。
- `SetTransportState(TransportState state)`
  - 直接设置当前运输 state。
- `SwitchTransportState()`
  - 在 `ToOutput` / `ToInput` 之间切换。
- `IsTransportToOutput()`
  - 判断当前是否朝 output 方向运输。
- `IsTransportToInput()`
  - 判断当前是否朝 input 方向运输。

## `Level64Ctrl` actor 级接口

`Level64Ctrl` 按 actor 维护持物和 tween 状态，不再只支持单个 `item`。

### 持物与 tween 状态

- `_carriedItems`
  - `Dictionary<Level64ActorCtrl, Level64BlockItem>`
  - 记录每个 actor 当前手里的砖。
- `_itemTweens`
  - `Dictionary<Level64ActorCtrl, Tween>`
  - 记录每个 actor 当前正在执行的取砖/放砖 jump。

### 对外 API

- `TryPickupOutputForActor(Level64ActorCtrl targetActor)`
  - 如果 actor 当前没持砖、没在 tween 中，就从 output 取砖并开始 pickup jump。
- `TryDropInputForActor(Level64ActorCtrl targetActor)`
  - 如果 actor 当前持砖、没在 tween 中，就取 input 预览落点并开始 drop jump。
  - 同时更新 `currentBlockCount`、剩余数量、over 状态和开门判定。
- `IsActorCarrying(Level64ActorCtrl targetActor)`
  - 判断某个 actor 当前是否持砖。
- `IsActorBusy(Level64ActorCtrl targetActor)`
  - 判断某个 actor 当前是否还在 jump tween 中。
- `IsActorInOutputArea(Level64ActorCtrl targetActor)`
  - 判断某个 actor 是否已经进入 output 区域。
- `IsActorInInputArea(Level64ActorCtrl targetActor)`
  - 判断某个 actor 是否已经进入 input 区域。

### 玩家入口

保留原来的触发器入口：

- `OnPlayerEnterOutputArea(Collider2D collider)`
- `OnPlayerEnterInputArea(Collider2D collider)`

这两个方法现在只负责从 `collider` 解析 actor，然后转发给 actor 级 API。

## `Level64AICtrl` 调度链路

### AI 生成

- `ClickStartDoor()` 调用 `TrySpawnAI()`
- `TrySpawnAI()` 最多生成 `maxAICount` 个 AI
- 新生成的 AI：
  - 以 `startDoor` 的 x 位置出生
  - 开启 dash 表现
  - tag 设为 `tools`
  - layer 设为 `TriggerItem`
  - 初始 state 设为 `TransportState.ToOutput`
  - 生成后先调用 `RemoveGeneratedAIInheritedSkins()` 清理从源 actor 克隆来的玩家 SkinPart 外观，再按生成顺序调用 `ActorSpineCtrl.SetSkin(...)` 设置 helper full-body 皮肤：
    - 禁用克隆 AI 上的 `ActorSkinBinding`
    - 清空 `back` / `face` / `body` 部件挂点下的子物体
    - 清理可能随源 actor 克隆过来的 `BlockItem(Clone)`
    - 第 1 个 helper 使用 `box_rich`，第 2 个 helper 使用 `box_bad`

### 调度规则

`Level64AICtrl` 不再自己维护一份 `_transportStates` 字典，而是直接读写 `Level64ActorCtrl` 上的 state。

- 当 actor state 是 `ToOutput`
  - `Level64AICtrl` 让 actor 朝右移动
  - 进入 output 区域后调用 `levelCtrl.TryPickupOutputForActor(tool)`
  - pickup 成功后，把 actor state 切到 `ToInput`
- 当 actor state 是 `ToInput`
  - `Level64AICtrl` 让 actor 朝左移动
  - 进入 input 区域后调用 `levelCtrl.TryDropInputForActor(tool)`
  - drop 成功后，把 actor state 切回 `ToOutput`

### 职责边界

- `Level64AICtrl`
  - 只负责调度、触发和切 state。
- `Level64ActorCtrl`
  - 只负责存 state 和做表现。
- `Level64Ctrl`
  - 负责真正的取砖、放砖、jump 表现、计数变化和开门判定。

## 关卡完成后退场

- 当 `levelCtrl.IsFinished == true` 后，AI 不再继续搬砖
- 所有 AI 改为朝右移动
- 接近 `door` 时：
  - 停止移动
  - `DOScale(Vector3.zero, exitScaleDuration)`
  - 销毁对应 AI 对象

## 角色取放表现

### 取砖表现

- `TryPickupOutputForActor()` 取到砖后：
  - `item.OffsetSortingGroupOrder(1)`
  - `actor.SwitchLiftState(true)`
  - 播放 pickup jump
- pickup jump 的目标点每帧跟随当前 actor 的实时世界坐标
- jump 完成后，砖块挂到 actor 身上，对齐到 `actor.position + Vector3.up`

### 放砖表现

- `TryDropInputForActor()` 会先读取 `blockCtrl.GetInputAvailablePosition()`
- 砖块执行 `DOJump` 到该目标点
- jump 完成后：
  - `blockItem.RestoreSortingGroupOrder()`
  - `blockCtrl.InputTriggerEnter(blockItem)`
  - `actor.SwitchLiftState(false)`
  - 从 `_carriedItems` 移除

## 计数与结束条件

- `maxBlockCount`
  - 本关目标放砖数量
- `currentBlockCount`
  - 已经进入放砖流程的数量
- `GetLeftBlockCount()`
  - 返回 `maxBlockCount - currentBlockCount`

`RefreshOver()` 的判定：

- `maxBlockCount - (currentBlockCount + blockCtrl.GetOutputCacheCount()) <= 0`

含义：

- 当“已放数量 + output 当前缓存数量”达到目标后，不再继续补新 output block

## 开门逻辑

- 当 `GetLeftBlockCount() <= 0` 时：
  - `doorCollider.enabled = true`
  - `doorSpineCtrl.PlayAnimation("door_open", false)`
  - `AudioManager.PlaySound("openDoor")`
  - `IsFinished = true`

## 排序层逻辑

`Level64BlockItem` 当前会缓存自身或子节点上的 `SortingGroup`：

- `OffsetSortingGroupOrder(int delta)`
  - 在默认 `sortingOrder` 基础上做增量偏移。
- `RestoreSortingGroupOrder()`
  - 恢复默认 `sortingOrder`。
- `OnSpawn()` / `OnRecycle()`
  - 自动恢复默认排序，避免对象池复用污染。

## 当前实现约束

- `Level64BlockCtrl` 不持有 `Level64Ctrl`
- `Level64BlockCtrl` 不通过 `Start()` 自己初始化
- 初始化时序完全由 `Level64Ctrl` 主控
- input 预览和 input 提交已经拆开：
  - 查询不改状态
  - 提交才改状态
- output 的停止补排控制完全在 `Level64BlockCtrl` 内部
- `GetOutputCacheCount()` 不包含角色手里已经取走但还没放入 input 的那一块
- prefab 里的 input/output 触发器当前仍只用于玩家入口，AI 走 `Level64AICtrl -> Level64Ctrl` 的主动调用链

## 2026-06-10 v2.0.0 补充

- `Level64AICtrl.maxAICount` 默认值为 2，`Level_064.prefab` 当前也序列化为 2。
- v2.0.0 按 AI helper 生成顺序切换皮肤：第 1 个调用 `ActorSpineCtrl.SetSkin("box_rich")`，第 2 个调用 `ActorSpineCtrl.SetSkin("box_bad")`；该行为在 2026-06-23 v2.1.0 中继续保留。
- AI helper 仍从 start door 的 x 坐标生成，保持 dash、0.7 缩放、`TriggerItem` layer 和 `TransportState.ToOutput` 初始状态。
- `Level64AICtrl.DispatchTransportTool()` 现在不仅移动 helper，还会在到达 output/input x 范围后调用 `Level64Ctrl.TryPickupOutputForActor(tool)` / `TryDropInputForActor(tool)`。
- AI drop 与玩家 drop 共用 `TryDropInputForActor()`，每次成功 drop 只增加 1 个 `currentBlockCount`。
- `Level64Ctrl.maxBlockCount` 默认值为 10000，`GetLeftBlockCount()` 显示剩余数量并 clamp 到 0。
- 10000 是累计成功 drop 数，不是同时存在的可见砖块数。
- `Level64BlockCtrl.GetRecommendedPoolCapacity()` 按 output 初始缓存 + input 双层显示 + 少量额外余量计算池上限；`Level64Ctrl.InitializeBlockCtrl()` 不再把对象池上限直接设为 `maxBlockCount`。
- 完成时 `GameFinished()` 先 `OpenDoor()`，再启动非阻塞完成反馈；反馈 1 秒内把显示剩余数归 0。
- 完成反馈结束后隐藏 `blockCountTxt` 和 `TaskTips`，显示 `SuccessTips`；字段为空时会按子节点名称兜底查找，缺失只记录 warning。
- `Level64Ctrl.CleanupActorCarry(Level64ActorCtrl)` 用于 AI 退出/销毁时停止 carried brick tween，并把未放下的砖回收到 `Level64BlockCtrl`。

## 2026-06-23 v2.1.0 补充

- `Level64AICtrl` 继续按生成顺序给 AI helper 设置 `box_rich` / `box_bad`，保留 v2.0.0 的 helper full-body 皮肤表现。
- `TrySpawnAI()` 在 `CreateToolActor()` 后立即调用 `RemoveGeneratedAIInheritedSkins(createdTool)`，再调用 `AssignSpawnOrderSkin(createdTool, spawnOrder)`。
- `RemoveGeneratedAIInheritedSkins()` 的清理范围只限生成出来的 AI helper 从源 actor 继承的玩家 SkinPart 外观：
  - 禁用 helper 子层级中的 `ActorSkinBinding`，避免后续玩家 Skin 变更事件继续刷新到该 helper。
  - 只清理带 `BoneFollowerAutoBinder` 或 `BoneFollower` 的 `back`、`face`、`body` 挂点子物体，不删除挂点自身。
  - 保留移动、dash、运输 state、取砖/放砖、退场和 carried brick 清理逻辑不变。
