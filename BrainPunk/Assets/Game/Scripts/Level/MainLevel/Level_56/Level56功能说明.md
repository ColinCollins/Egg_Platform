# Level56 FlipBird 功能说明

## 现状说明

- 本文档以当前仓库代码为准，不再沿用早期纯设计描述。
- 当前实现由三个脚本配合完成：
  - `Level56Ctrl`：关卡主流程控制。
  - `Level56GamePanelCtrl`：小游戏期间的 UI 和距离表现。
  - `Level56FlipBirdGameCtrl`：滚动、刷柱子、计时、成功回调。
- 当前关卡 prefab 为 `Assets/Game/Prefabs/Level/Level_056.prefab`。
- 当前关卡面板 prefab 为 `Assets/Game/UI/GamePanel/Resources/GamePlayPanel_019_56.prefab`。

## 脚本职责

### Level56Ctrl

- 接收触发区进入事件，只允许目标 `actor` 启动小游戏。
- 在小游戏运行期间锁定 `actor` 的 X 轴位置，并清掉横向输入。
- 负责调用 `Level56GamePanelCtrl` 和 `Level56FlipBirdGameCtrl`。
- 监听 `Level56FlipBirdGameCtrl.OnSuccess / OnFail`。
- 成功后停止小游戏、停止 UI 倒计时，并将角色移动到 `finishPoint`。

### Level56GamePanelCtrl

- 挂在 `GamePlayPanel_019_56.prefab` 上。
- 负责隐藏左右移动按钮。
- 负责把小游戏时长映射成 `1000m -> 0m` 的距离倒计时表现。
- 不负责关卡世界逻辑，也不直接驱动 `actor`。

### Level56FlipBirdGameCtrl

- 只负责小游戏内部运行逻辑：
  - 背景或地图滚动。
  - 柱子生成、移动、回收。
  - 游戏时长计时。
  - 成功/失败事件抛出。
- 不直接处理 UI。
- 不直接处理角色移动到终点。
- 不直接调用关卡通关逻辑。

## Level56FlipBirdGameCtrl 正确用法

### 正确调用入口

- 不要让触发器、UI 或其他脚本直接零散调用 `StartPlay()`。
- 正确接法是由 `Level56Ctrl` 统一驱动：
  1. 触发区调用 `Level56Ctrl.OnActorEnterFlipBirdArea(...)`。
  2. `Level56Ctrl` 校验进入对象是否为目标 `actor`。
  3. `Level56Ctrl.StartFlipBird()` 内部执行：
     - `panelCtrl.StartGoal(flipBirdGame.Duration)`
     - `flipBirdGame.SetActor(actor)`
     - `flipBirdGame.StartPlay()`
  4. `Level56Ctrl` 通过 `OnSuccess / OnFail` 接收小游戏结果。

### 生命周期

- `Level56Ctrl.Start()` 中会：
  - 解析 `finishPoint`
  - 解析 `flipBirdGame`
  - 缓存 `Level56GamePanelCtrl`
  - 绑定 `flipBirdGame.OnSuccess / OnFail`
- `Level56FlipBirdGameCtrl.StartPlay()` 中会：
  - 自动解析 `spawnRoot`
  - 缓存后备柱子来源
  - 缓存并重置滚动节点初始位置
  - 回收已存在柱子
  - 尝试注册 `Level56PipeObstacle` 对象池
  - 重置计时并开始运行
- `Level56FlipBirdGameCtrl.StopPlay()` 只负责停止小游戏自身：
  - 停止计时
  - 停止生成新的 pipe
  - 让场上最后一批 pipe 继续移动到回收线
  - 等最后一批 pipe 自然回收后再清理自己注册的对象池
- `StopPlay()` 不会恢复角色移动，也不会恢复 UI；这些由 `Level56Ctrl` 和 `Level56GamePanelCtrl` 负责。

### 成功和失败

- 当前实现里，`OnSuccess` 会在 `elapsedTime >= gameDuration` 时触发。
- 当前实现里，`OnFail` 只是预留事件，`Level56FlipBirdGameCtrl` 内部还没有主动触发失败的代码路径。
- 因此目前真实行为是：
  - 可以计时成功。
  - 可以被外部停止。
  - 还没有完整的“碰撞后失败”回路。

### Actor 的正确传递方式

- 推荐始终由 `Level56Ctrl` 在启动前调用 `flipBirdGame.SetActor(actor)`。
- `Level56FlipBirdGameCtrl` 虽然在 `actor == null` 时会回退到 `FindFirstObjectByType<ActorCtrl>()`，但这只是兜底，不应作为正式接法。
- 正式接法应保证关卡主控明确传入当前关卡使用的 `actor`。

## Inspector 配置

### Level56Ctrl

- `actor`：当前关卡玩家 `ActorCtrl`。
- `finishPoint`：小游戏完成后角色移动到的终点；如果为空，会自动查找名为 `PathPoint`，找不到再查 `FinishPoint`。
- `flipBirdGame`：关联的 `Level56FlipBirdGameCtrl`；如果为空，会从自身子层级自动查找。
- `finishMoveDuration`：成功后移动到终点的耗时。
- `finishMoveEase`：成功后移动到终点的缓动曲线。

### Level56GamePanelCtrl

- `playPanel`：当前 `GamePlayPanel`；为空时会优先 `GetComponent<GamePlayPanel>()`，否则回退到 `PlayCtrl.Instance.CurrentGamePlayPanel`。
- `goalTxt`：距离文本。
- `goalBoard`：距离面板根节点。
- `hideGoalBoardOnStop`：`StopGoal()` 时是否隐藏距离面板。

### Level56FlipBirdGameCtrl

- `actor`：小游戏使用的 `ActorCtrl`，推荐由 `Level56Ctrl.SetActor(...)` 在运行前设置。
- `scrollTargets`：要滚动的节点列表。每项都要配置：
  - `target`
  - `moveSpeed`
  - `resetThresholdX`
  - `resetOffsetX`
  - `useLocalPosition`
- `pipePrefabs`：优先使用的柱子 prefab 列表。
- `spawnRoot`：柱子生成父节点；为空时会自动递归查找名为 `Pipes` 的节点。
- `pipeSpawnYOffsetRange`：柱子生成时的随机 Y 偏移范围。
- `gameDuration`：小游戏总时长。
- `spawnInterval`：刷柱子间隔。
- `pipeMoveSpeed`：柱子向左移动速度。
- `pipeRecycleX`：柱子回收阈值 X。
- `poolInitialSize / poolMaxSize`：对象池初始化和最大数量。

## 资源来源规则

### 滚动节点

- `scrollTargets` 不会自动收集，必须手动在 Inspector 里配置。
- 如果 `scrollTargets` 为空，小游戏仍然会计时并可能成功，但不会有背景滚动表现。

### ScrollTargetData 设置规则

- `scrollTargets` 里的每一项都代表“一块需要循环向左滚动的节点”。
- 推荐把会循环的背景、地面、装饰分成 2 份或多份，首尾排开后一起挂进 `scrollTargets`。
- `target`
  - 指向要滚动的那个 `Transform`。
  - 不要留空；留空的项会被代码直接跳过。
- `moveSpeed`
  - 表示该节点每秒向左移动多少单位。
  - 同一层要做无缝循环时，通常同层节点要用相同速度。
  - 远景速度小一点，近景速度大一点，可以做视差。
- `resetThresholdX`
  - 表示这个节点的 `x` 移动到多左边时，开始执行“跳回右侧”。
  - 它只控制滚动节点循环，不控制 pipe 回收。
  - 例子：当 `resetThresholdX = -20` 时，节点移动到 `x <= -20` 就会触发重置。
- `resetOffsetX`
  - 表示触发重置后，给当前 `x` 再加多少距离。
  - 这个值通常要等于“同一循环组的总宽度”或“相邻两块同层节点的水平间距”。
  - 例子：两块背景初始分别在 `x = 0` 和 `x = 40`，则常见配置是 `resetOffsetX = 40`，这样左边那块移出后会被挪回右边。
- `useLocalPosition`
  - 节点是某个父节点下的子物体，且循环布局按本地坐标摆放时，设为 `true`。
  - 节点直接按世界坐标排布，或父节点可能移动/缩放导致本地坐标不稳定时，设为 `false`。
  - 同一组循环节点应统一使用同一种坐标系，避免一半 local 一半 world。
- `initialPosition`
  - 这是运行时缓存值，不需要在 Inspector 配。
  - `StartPlay()` 时会缓存当前初始位置，重新开始小游戏时会先恢复到这里。

### ScrollTargetData 推荐配置步骤

1. 先把同一层要循环的节点在场景里摆好，例如 A 在 `x = 0`，B 在 `x = 40`。
2. 把 A、B 都加入 `scrollTargets`，并给它们相同的 `moveSpeed`。
3. 如果这组节点是父节点下的子物体，通常把 `useLocalPosition` 设为 `true`。
4. 把 `resetThresholdX` 设成“节点完全离开可视区域后”的左边界。
5. 把 `resetOffsetX` 设成一轮循环需要跳回去的总距离；两块等宽拼接时，通常就是两块之间的水平间距。
6. 运行后如果看到节点接缝、重叠或跳跃：
   - 优先检查 `resetOffsetX` 是否和实际排布宽度一致。
   - 再检查 `resetThresholdX` 是否设得过早或过晚。
   - 最后检查该组节点是否错误地混用了 local/world 坐标。

### 柱子来源

- `pipePrefabs` 有配置时，优先从 `pipePrefabs` 随机取 prefab 生成柱子。
- `pipePrefabs` 为空时，会回退到 `spawnRoot` 子节点作为后备来源：
  - 启动时收集 `spawnRoot` 的直接子节点。
  - 收集后会把这些子节点先设为 `inactive`，作为模板来源。
- 如果 `pipePrefabs` 和 `spawnRoot` 子节点都为空，则不会生成任何柱子。

## 当前主流程

1. actor 进入触发区域。
2. 触发区调用 `Level56Ctrl.OnActorEnterFlipBirdArea(...)`。
3. `Level56Ctrl` 校验对象并启动 `StartFlipBird()`。
4. `StartFlipBird()` 内部会：
   - 锁定当前 `actor` 的 X 位置。
   - 隐藏左右移动按钮。
   - 开启 `1000m -> 0m` UI 倒计时。
   - 调用 `flipBirdGame.SetActor(actor)`。
   - 调用 `flipBirdGame.StartPlay()`。
5. `Level56Ctrl.Update()` 在小游戏运行期间持续锁 X，并清横向速度。
6. `Level56FlipBirdGameCtrl.Update()` 在小游戏运行期间：
   - 推进游戏计时。
   - 更新滚动节点。
   - 更新存活柱子。
   - 按时间生成新柱子。
7. 达到 `gameDuration` 后，`Level56FlipBirdGameCtrl` 触发 `OnSuccess`。
8. `Level56Ctrl.OnFlipBirdSuccess()` 内部会：
   - 调用 `flipBirdGame.StopPlay()`
   - 调用 `panelCtrl.StopGoal()`
   - 调用 `MoveActorToFinishPoint()`

## 当前实现边界

- `Level56FlipBirdGameCtrl` 目前没有公开的“主动失败”入口，也没有内部碰撞失败逻辑。
- `OnFail` 虽已暴露，但当前代码不会自动触发它。
- `StopPlay()` 会清掉自己注册的 `Level56PipeObstacle` 对象池，不应把这个池视为跨系统共享常驻池。
- 当前 `Level_056.prefab` 中 `Level56FlipBirdGameCtrl` 的 `scrollTargets` 和 `pipePrefabs` 序列化字段默认是空的，后续如果要补足表现，需要在 prefab Inspector 中明确配置。
