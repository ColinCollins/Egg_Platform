# Level66 追击狗跟跳补充说明

## 背景

- 本次变更只影响 `Level66Ctrl` 的单狗追击分支。
- 目标是让追击中的狗在玩家发出 jump 指令时同步跳跃，降低追击路径被地形卡住的概率。

## 当前实现

- `Level66Ctrl` 通过 `EventSubscriber` 监听 `PlayerJumpEvent`。
- 只有当 `_state == DogChase` 时，才允许 chase dog 跟跳。
- 只有当 `_isPaused == false` 时，才允许 chase dog 跟跳。
- 只有当 `_chasingDog.root` 仍处于激活状态且 `DogEnemyCtrl` 引用有效时，才调用 `DogEnemyCtrl.TriggerJump()`。
- 三帽对撞分支、开场展示、洗牌、选帽揭示和 fight 展示阶段不会触发这条跟跳逻辑。

## 受影响脚本

- `Assets/Game/Scripts/Level/LevelItem/Level_66/Level66Ctrl.cs`

## 预期结果

- 一帽分支和两帽分支进入 `DogChase` 后，玩家每次触发 jump，追击狗会同步起跳一次。
- 暂停期间不会因为事件残留让狗额外跳跃。
- 非追击态不会误触发狗跳跃。
