# Level66 追击狗跟跳风险补充

## 原始行为基线

- 变更前，`Level66Ctrl` 只在 `UpdateDogChase()` 中持续设置 chase dog 的水平移动输入。
- 玩家跳跃不会额外驱动 chase dog 跳跃。

## 本次变更

- 新增 `PlayerJumpEvent` 监听。
- 当且仅当 Level66 处于 `DogChase` 状态、未暂停且 chase dog 仍激活时，调用 `DogEnemyCtrl.TriggerJump()`。

## 主要风险

- `PlayerJumpEvent` 触发频率如果高于预期，可能让 chase dog 连续跳跃，影响追击节奏。
- 地形、碰撞体或 `DogEnemyCtrl` 的地面检测如果与玩家不一致，可能出现“玩家能跳而狗跳不起来”或“狗多跳一次”的观感差异。
- 如果暂停恢复与 jump 输入同帧发生，仍需在 PlayMode 中确认不会出现暂停帧误跳。

## 验证重点

- 一帽分支进入追击后，玩家起跳时 chase dog 是否同步起跳。
- 两帽分支进入追击后，玩家连续跳跃时 chase dog 是否按预期跟跳。
- 暂停/恢复期间按 jump，不应触发 chase dog 误跳。
- 三帽对撞分支不应响应这条跟跳逻辑。

## 未验证项

- 尚未执行 Unity Editor PlayMode。
- 尚未执行 Android/iOS 真机或模拟器验证。

## 回滚路径

- 回滚 `Assets/Game/Scripts/Level/LevelItem/Level_66/Level66Ctrl.cs` 中 `PlayerJumpEvent` 的订阅与处理逻辑。
