# ObjectPoolManager 逻辑说明

## 组件职责

- `ObjectPoolManager` 按类型名管理多个对象池。
- 每个对象池由 `RegisterPool<T>(createFunc, initialSize, maxSize)` 注册。
- 对象类型必须实现 `IRecycle`。

## 核心流程

### RegisterPool

代码位置：
- [ObjectPoolManager.cs](D:\GitStore\BrinPunk\BrainPunk\Assets\Game\Scripts\Common\ObjectPool\ObjectPoolManager.cs:41)

行为：
- 以 `typeof(T).Name` 作为池 key。
- 先向 `_pools` 写入 `null` 占位，再构造 `ObjectPool<T>`。
- `ObjectPool<T>` 构造时会预创建 `initialSize` 个对象。
- 预创建对象会被 `SetActive(false)`，并挂到 `_poolRoot` 下。

### Get

代码位置：
- [ObjectPoolManager.cs](D:\GitStore\BrinPunk\BrainPunk\Assets\Game\Scripts\Common\ObjectPool\ObjectPoolManager.cs:64)
- [ObjectPoolManager.cs](D:\GitStore\BrinPunk\BrainPunk\Assets\Game\Scripts\Common\ObjectPool\ObjectPoolManager.cs:246)

行为：
- 先从 `_pools` 查池。
- 找不到池时直接报错并返回 `null`。
- `ObjectPool<T>.Get()` 优先从 `_pool` 队列取对象。
- 如果队列为空，则检查 `_totalCreated >= _maxSize`。
- 达到上限时直接返回 `null`。
- 未达到上限时调用 `_createFunc()` 新建对象，并 `++_totalCreated`。
- 返回前会执行 `obj.OnSpawn()`。

### Recycle

代码位置：
- [ObjectPoolManager.cs](D:\GitStore\BrinPunk\BrainPunk\Assets\Game\Scripts\Common\ObjectPool\ObjectPoolManager.cs:91)
- [ObjectPoolManager.cs](D:\GitStore\BrinPunk\BrainPunk\Assets\Game\Scripts\Common\ObjectPool\ObjectPoolManager.cs:279)

行为：
- 先执行 `obj.OnRecycle()`。
- 再进入 `ObjectPool<T>.Recycle(obj)`。
- 如果对象已经在 `_poolSet` 里，跳过重复回收。
- 如果 `_maxSize > 0 && _pool.Count >= _maxSize`，则直接 `Destroy(obj.gameObject)`。
- 否则把对象 `SetActive(false)`，挂回 `_poolRoot`，并重新入队。

### UnregisterPool / Clear

代码位置：
- [ObjectPoolManager.cs](D:\GitStore\BrinPunk\BrainPunk\Assets\Game\Scripts\Common\ObjectPool\ObjectPoolManager.cs:142)
- [ObjectPoolManager.cs](D:\GitStore\BrinPunk\BrainPunk\Assets\Game\Scripts\Common\ObjectPool\ObjectPoolManager.cs:315)

行为：
- `UnregisterPool<T>()` 会调用 `pool.Clear()` 后把池从 `_pools` 移除。
- `ObjectPool<T>.Clear()` 只会销毁当前 `_pool` 队列里的待命对象。
- 已经借出去、当前不在 `_pool` 队列里的对象，不会被 `Clear()` 处理。

## ObjectPool<T> 内部状态

### `_pool`

- 含义：当前可复用的待命对象队列。
- 只有已经回收到池内的对象才在这里。

### `_poolSet`

- 含义：辅助判断对象是否已经在池中。
- 只用于避免重复回收。

### `_totalCreated`

- 含义：池逻辑认为“已经创建过”的对象总数。
- 在预创建和运行时新建时递增。
- 当前实现中，外部 `Destroy` 对象时不会自动递减。
- 当前实现中，`Recycle()` 因池满而 `Destroy` 对象时也不会递减。

## 当前实现的关键限制

### 限制 1：池不追踪已借出对象

直接证据：
- `Clear()` 只处理 `_pool` 队列，见 [ObjectPoolManager.cs](D:\GitStore\BrinPunk\BrainPunk\Assets\Game\Scripts\Common\ObjectPool\ObjectPoolManager.cs:315)。

结论：
- 只要对象已经被 `Get()` 借出，就脱离了对象池的清理控制。
- 这类对象如果没有被调用方自己回收或销毁，`UnregisterPool()` 不会管到它们。

### 限制 2：`_totalCreated` 可能和真实存活对象数不一致

直接证据：
- 新建对象时 `++_totalCreated`，见 [ObjectPoolManager.cs](D:\GitStore\BrinPunk\BrainPunk\Assets\Game\Scripts\Common\ObjectPool\ObjectPoolManager.cs:262)。
- 池满回收时对象被 `Destroy`，但没有 `--_totalCreated`，见 [ObjectPoolManager.cs](D:\GitStore\BrinPunk\BrainPunk\Assets\Game\Scripts\Common\ObjectPool\ObjectPoolManager.cs:292)。
- `Clear()` 最后直接把 `_totalCreated = 0`，但前提是只清了 `_pool` 队列，见 [ObjectPoolManager.cs](D:\GitStore\BrinPunk\BrainPunk\Assets\Game\Scripts\Common\ObjectPool\ObjectPoolManager.cs:327)。

结论：
- 运行过程中，只要有“外部销毁对象”或“池满时直接销毁对象”的情况，`_totalCreated` 就可能偏大。
- 一旦 `_pool` 为空且 `_totalCreated >= _maxSize`，后续 `Get()` 会直接返回 `null`，即使真实场景里已经没有那么多活对象了。

## Level64 相关风险结论

### Level64 的初始化与销毁链

代码位置：
- [Level64Ctrl.cs](D:\GitStore\BrinPunk\BrainPunk\Assets\Game\Scripts\Level\LevelItem\Level_64\Level64Ctrl.cs:39)
- [Level64Ctrl.cs](D:\GitStore\BrinPunk\BrainPunk\Assets\Game\Scripts\Level\LevelItem\Level_64\Level64Ctrl.cs:184)
- [Level64BlockCtrl.cs](D:\GitStore\BrinPunk\BrainPunk\Assets\Game\Scripts\Level\LevelItem\Level_64\Level64BlockCtrl.cs:51)
- [Level64BlockCtrl.cs](D:\GitStore\BrinPunk\BrainPunk\Assets\Game\Scripts\Level\LevelItem\Level_64\Level64BlockCtrl.cs:509)

行为链：
- `Level64Ctrl.Start()` 调用 `blockCtrl.ConfigurePool(...)` 和 `blockCtrl.Initialize()`。
- `Level64BlockCtrl.Initialize()` 注册 `Level64BlockItem` 对象池并生成初始 output。
- 关卡销毁时，`Level64BlockCtrl.OnDestroy()` 调用 `ClearAllItems()`。
- `ClearAllItems()` 当前会 `Destroy(_activeBlockItems)`，然后 `UnregisterPool()`。

### 为什么重建关卡时可能拿不到 item

直接结论：
- 根因不是单一的“没有回收到位”，而是“对象被外部销毁后，池内部计数和实际存活对象数可能失真”。

触发路径：
- `Level64BlockCtrl.ClearAllItems()` 会直接 `Destroy` 活跃对象，见 [Level64BlockCtrl.cs](D:\GitStore\BrinPunk\BrainPunk\Assets\Game\Scripts\Level\LevelItem\Level_64\Level64BlockCtrl.cs:511)。
- 这些对象不是通过 `ObjectPoolManager.Recycle()` 回池的。
- `ObjectPoolManager` 也没有对“外部已销毁对象”做计数修正。
- 如果同一生命周期里池还没完全重置，或者别的路径也在复用这一类池对象，就可能出现：
- `_pool` 里可用对象不足。
- `_totalCreated` 仍然认为已经到达 `maxSize`。
- `ObjectPool<T>.Get()` 命中 `if (_totalCreated >= _maxSize) return null;`。

### Level64 为什么更容易撞到这个问题

直接证据：
- `Level64` 在拿起 block 后，会把对象从 `_outputBlockItems` 移除，见 [Level64BlockCtrl.cs](D:\GitStore\BrinPunk\BrainPunk\Assets\Game\Scripts\Level\LevelItem\Level_64\Level64BlockCtrl.cs:245)。
- block 在 pickup / drop tween 过程中会被 `SetParent(null)`，见 [Level64Ctrl.cs](D:\GitStore\BrinPunk\BrainPunk\Assets\Game\Scripts\Level\LevelItem\Level_64\Level64Ctrl.cs:141) 和 [Level64Ctrl.cs](D:\GitStore\BrinPunk\BrainPunk\Assets\Game\Scripts\Level\LevelItem\Level_64\Level64Ctrl.cs:248)。
- 这类对象短时间内既不在 output 列表，也不一定已经进入 input 列表。

结论：
- 如果销毁时机落在“角色手持 / jump 中”这段窗口，最容易出现对象脱离普通列表管理。
- 这也是 `Level64` 重建关卡时最该重点检查的窗口。

## 建议

- 如果对象生命周期结束，希望仍由池管理，优先走 `Recycle()`，不要直接 `Destroy()`。
- 如果必须 `Destroy()`，对象池需要同时修正“已创建计数”。
- 如果 `UnregisterPool()` 语义要覆盖整个关卡实例，调用方必须自己追踪并清理所有已借出对象。
