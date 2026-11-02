# Level 46 冲撞撞飞实现记录

## 需求背景

2026-07-20 为 Level 083 的“Box Bad 生气后冲向主角并将其撞飞出屏”方案调查既有关卡实现。静态检索确认 `Level_046` 是当前工程中唯一同时具备“角色主动冲向 Actor”和“碰撞后撞飞 Actor”完整语义的实际配置关卡。

## 影响文件、资源与系统

- 冲撞角色：`Assets/Game/Scripts/Level/Actor/Level46EnemyCtrl.cs`。
- 关卡编排：`Assets/Game/Scripts/Level/LevelItem/Level_46/Level46Ctrl.cs`。
- 共享移动与击飞：`Assets/Game/Scripts/Level/Actor/ActorCtrl.cs`。
- 关卡 Prefab：`Assets/Game/Prefabs/Level/Level_046.prefab`。
- 角色资源：`Assets/Game/ArtSrc/Spines/sp_Box_001/Box_SkeletonData.asset`，skin 为 `box_bad`。
- 碰撞爆点：`Assets/Game/ArtSrc/Particles/Prefabs/Knock_Explosion.prefab`。
- 失败边界：`Assets/Game/Prefabs/LevelItems/FallArea.prefab`、`FailAreaTriggerHandle` 与 `BaseLevelCtrl`。
- 配置入口：`tbleveldata.json` 的 `10046 -> Level_046`，`tblevelsort.json` 的玩家排序项 `46 -> 10046`。

本次只调查并记录已有实现，不修改上述脚本、Prefab、资源或配置。

## 现有行为基线

1. `Level_046.prefab` 的点击事件调用 `Level46Ctrl.PlayPickUpScroll()`。
2. 卷轴播放 `pickup` 后固定等待 `1s`，控制器调用 `enemy.ReadyToMove()`。
3. `ReadyToMove()` 启用敌人的 `PolygonCollider2D` 和 `Rigidbody2D.simulated`，设置 `SetMoveInput(-1)` 并调用 `SwitchDash(true)`。
4. `Level46Ctrl.Update()` 在敌人尚未命中时每帧调用 `enemy.OnUpdate()`；`ActorCtrl.HandleMovement()` 按 `xInput * moveSpeed` 写入水平速度。
5. Prefab 把敌人的 `moveSpeed` 配为 `11`。`SwitchDash(true)` 不提高速度，只让地面移动动画从 `walk` 切换为循环 `walk01`。
6. 敌人使用动态 `Rigidbody2D`、零重力、插值、连续碰撞检测和冻结旋转；碰撞区域是初始关闭的 `PolygonCollider2D` trigger。
7. trigger 首次接触 tag 为 `Player` 的对象后，用 `IsDie` 阻止重复命中并进入撞飞表现。

## 撞飞表现

- 先调用主角 `StopMoving()`，将现有速度清零并关闭自主移动。
- 固定等待 `0.1s` 后，把 `Knock_Explosion` 移到冲撞角色位置并播放，同时播放 `trap` 音效。
- 调用 `actorCtrl.Knockback(new Vector2(-0.5f, 0.5f), 50f)`；该方法直接把主角刚体速度设为方向乘力度，并维持默认 `2s` 的 knocked 标记。
- 对主角根 Transform 执行 `360° / 0.5s` 的无限循环旋转，形成翻滚飞出表现。
- `Level46EnemyCtrl` 本身不调用 `Actor.Die()`，也不直接派发失败事件。
- `Level_046.prefab` 在 `x=-18.07` 与 `x=18.01` 各放置一个 `10×10` 的 `FallArea`。主角进入边界后由 `FailAreaTriggerHandle` 派发 `OnTriggerFailAreaEvent`，`BaseLevelCtrl` 播放失败音效并进入重开流程。

因此，Level 46 的效果不是一个单独的“撞飞动画”，而是由 `walk01` 冲刺循环、刚体水平移动、trigger 命中、短暂击中特效与音效、主角刚体击飞、DOTween 翻滚以及外围失败区共同组合。

## 相似实现排除

- `Level_027` 的 `PuppyEnemyCtrl` 会在接触玩家后播放自身 `knockback`、调用玩家 `Die()`、击飞并旋转，但对象本身没有同类主动冲刺控制。
- `Level_026` 是玩家或工具撞到敌人后的受击链路，不是敌人冲向玩家。
- `Level_025` 是玩家踢飞敌人，方向相反。
- `Level_050` 是陨石下落命中玩家，属于投射/陷阱命中。

## Level 083 参考边界

- 可参考的组合方式：移动动画与实际位移分离、首次命中门禁、碰撞爆点、`Knockback(...)`、旋转翻滚和镜头外失败边界。
- `sp_box_025` 也包含 `walk01`，时长同为约 `0.5333s`，但其动画 JSON 与 `sp_Box_001` 的 `walk01` 哈希不同，不能据此宣称两者视觉完全一致。
- 不应直接复制 `moveSpeed=11`、固定向左输入、`(-0.5, 0.5) × 50`、`0.1s` 延迟或无限旋转；这些都是 Level 46 的构图参数。
- Level 083 合同要求 `angry` 完成后才开始冲撞，并在主角完全出屏后失败；Level 46 没有 `angry` 前置，也没有用代码显式判定“完全出屏”。
- Level 46 通过侧边 `FallArea` 收口，Level 083 是否复用该方式、按相机 viewport 判定或使用关卡局部边界仍待实现方案确认。

## 验证

- 静态检索所有同时包含碰撞回调与 `Knockback(...)` 的关卡脚本，并核对所有 `SwitchDash(true)` 调用点。
- 静态读取 `Level46EnemyCtrl`、`Level46Ctrl`、`ActorCtrl`、`FailAreaTriggerHandle` 和 `BaseLevelCtrl` 调用链。
- 通过脚本 GUID 反查确认 `Level46EnemyCtrl` 只绑定于 `Level_046.prefab`。
- 静态读取 Prefab 序列化值，确认敌人 `moveSpeed=11`、初始 collider disabled、rigidbody simulated disabled、`box_bad` skin、`Knock_Explosion` 引用及左右 `FallArea` 位置/尺寸。
- 静态读取配置，确认 `Level_046` 存在正常关卡数据和玩家排序入口。
- 未启动 Unity Editor，未执行 Level 46 PlayMode、录屏、移动端构建或真机验证。

## 剩余风险与未验证项

- 未人工观察 `walk01` 冲刺、碰撞爆点、旋转速度及最终出屏节奏。
- 未验证玩家实际从碰撞点进入左侧 `FallArea` 所需时间，也未确认不同帧率和宽高比表现。
- `DOLocalRotate(...).SetLoops(-1)` 没有局部 tween 句柄；关卡销毁时由 `Level46Ctrl.OnDestroy()` 对目标 Transform 执行 `DOKill()`，中途重开与异常退出仍需 PlayMode 验证。
- 敌人命中后控制器停止调用 `enemy.OnUpdate()`，但脚本没有显式把敌人刚体速度清零；其碰撞后最终位置依赖物理现场，不能作为 Level 083 模板行为。

## 回滚

本记录不改变运行时行为。回滚时删除本文件及引用它的调查文档增量即可，无需修改代码、Prefab、配置、资源或玩家数据。
