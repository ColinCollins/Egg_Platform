# Level66 功能说明

## 已确认上下文

- `Level66Ctrl.cs` 原始状态只有功能注释、空 `Start` 和空 `Update`，本次已实现 Level66 本地帽子戏法流程。
- `Level_066.prefab` 根节点同时挂有 `BaseLevelCtrl` 与 `Level66Ctrl`，`BaseLevelCtrl.gamePlayPanelName` 为 `GamePlayPanel_000`。
- 关卡中已有 `magicHat_1`、`magicHat_2`、`magicHat_3`，每个对象都有 `ClickItemEventHandle`、`BoxCollider2D`；本次 prefab 已将三个 `OnClickEvent` 分别绑定到 `Level66Ctrl.ClickHat1/2/3`。
- 关卡中已有一个 `Key` prefab instance 和两个 `PuppyEnemy` prefab instance；两只狗对象额外挂有 `DogEnemyCtrl`，可复用 `ActorCtrl` 移动接口。
- `Hats` 父节点已有 Animator；当前实现以固定 1.5 秒作为开场展示时机，不依赖 Animator 动画完成事件。

## 现有系统复用

- 玩家移动锁定通过 `BaseLevelCtrl.SetCtrlable(false)` 阻断输入转发，并调用 `ActorCtrl.SetMoveInput(0f)`、`StopMoving()`、`ResumeMoving()` 控制 actor。
- 帽子点击入口复用 `ClickItemEventHandle` 的触摸/鼠标射线、暂停过滤、点击次数逻辑。
- 钥匙只移动和显隐现有关卡内 `Key` 对象，拾取、使用、通关、存档、打点仍由原有 key 事件链负责。
- 狗危险碰撞沿用 `PuppyEnemyCtrl`，狗移动沿用 `DogEnemyCtrl` / `ActorCtrl` 的 `SetMoveInput()` 与 `OnUpdate()`。

## 运行流程

- 关卡开始时缓存帽子、帽子 SpriteRenderer 初始颜色、钥匙、狗、打架表现、点击开关等初始状态。
- 开场固定 1.5 秒内关闭玩家移动与帽子点击，显示钥匙和两只狗，隐藏打架表现。
- 开场 1.5 秒结束后立即隐藏钥匙和两只狗，然后才开始程序控制的帽子戏法洗牌。
- 三个帽子只沿本地 x 坐标在三个初始位置之间随机两两互换位置；`shuffleSwapCount` 指定交换次数，`shuffleSwapDuration.x/y` 指定单次交换最短/最长时间，`shuffleDurationCurve` 控制每次交换从最长过渡到最短的曲线节奏。
- 三个初始帽子位置来自 prefab：`magicHat_1` x=4.23、`magicHat_2` x=0.36、`magicHat_3` x=-3.8，y 均为 -1.94。
- 洗牌结束后进入等待首次点击状态；若玩家不点击帽子，关卡不自动结算。
- 首个有效帽子点击启动固定 0.2 秒容错窗口；窗口内只记录不同帽子，重复同一帽子和窗口结束后的点击会被忽略。
- 0.2 秒窗口结束后，所有被选中的帽子先做轻量打开表现，再渐隐消失并变为不可点击，本次尝试只结算一次。
- 选 1 个帽子：第一只狗移动到该帽子当前世界位置并追击 actor。
- 选 2 个帽子：第一只狗移动到第一个帽子当前世界位置并追击 actor，钥匙移动到第二个帽子当前世界位置并显示。
- 选 3 个帽子：第一只狗、钥匙、第二只狗分别对应第一、第二、第三个点击帽子；两只狗先面向彼此，等待 1 秒，再相向移动，距离足够近后隐藏狗并显示打架表现。
- fight 表现显示后会持续 `fightDuration`（当前 prefab 为 2 秒）；到时将 `fightAnims.SetBool("isStop", true)`，停止 fight 动画状态并显示 `dogsDeadBodys` 两个死亡狗体。
- Unity Editor PlayMode 中，当洗牌完成且关卡等待首次有效帽子点击时，左上角显示两个调试按钮：`Level66 Two Hats` 触发两帽分支，`Level66 Three Hats` 触发三帽分支；按钮使用当前内部洗牌顺序，每次尝试只允许触发一次，移动端构建不编译这些按钮。

## 清理策略

- `GameResetEvent` 会重启 Level66 本地 session；`OnDisable`、`OnDestroy` 会取消延迟任务、停止 DOTween、停止 Level66 驱动的狗移动并恢复本地状态。`SwitchGameStateEvent` 仅处理 Pause/Resume。
- Pause 时暂停 Level66 DOTween 与狗移动，0.2 秒/1 秒等 Level66 本地计时也会停止累积；Resume 时恢复 tween，并根据当前状态恢复追击或相向移动。
- Restart / re-entry 会恢复帽子 active、位置、缩放、透明度与点击开关，避免上一次已淡出的帽子影响下一次尝试。
- 缺少必要引用时只输出聚焦 warning 并跳过不安全行为，避免重启、退出、失败或通关路径被脚本异常阻塞。

## Prefab 绑定

- `baseLevelCtrl` -> `Level_066` 根节点 `BaseLevelCtrl`
- `actor` -> `BaseLevelCtrl.actor`
- `hatsAnimator` -> `Hats` 对象上的 Animator
- `hats[0]` -> `magicHat_1` 与其 `ClickItemEventHandle`
- `hats[1]` -> `magicHat_2` 与其 `ClickItemEventHandle`
- `hats[2]` -> `magicHat_3` 与其 `ClickItemEventHandle`
- `keyRoot` -> 现有关卡内 `Key` prefab instance transform
- `dogA` / `dogB` -> 两个 `PuppyEnemy` prefab instance transform 与新增的 `DogEnemyCtrl`
- `fightPresentation` -> 初始隐藏的 `Sp_cj220_1_09`
- `fightAnims` -> `FightAnimation` 上的 Animator，使用 `isStop` 参数切到 fight 结束姿态
- `dogsDeadBodys[0]` / `dogsDeadBodys[1]` -> `FightAnimation` 子节点 `SP_l66_dog_img_01`、`SP_l66_dog_img_02`

## 验证记录

- 2026-06-08：`cd BrainPunk && dotnet build Assembly-CSharp.csproj --no-restore` 通过，0 errors / 1049 个项目既有 warnings，未见 Level66 相关 error。
- 2026-06-08：静态检查确认 `Level_066.prefab` 保持 `GamePlayPanel_000`，`Level66Ctrl` 引用字段已写入，三个帽子点击事件分别绑定到 `ClickHat1/2/3`。
- 2026-06-08：未执行 Unity Editor PlayMode、非 Level66 回归、Android/iOS 真机或模拟器验证。

## 待 PlayMode 确认

- `Sp_cj220_1_09` 目前作为打架表现候选对象绑定，需在 Unity 中目视确认它是否为正确的 fight presentation。
- fight 停止后 `isStop` 对应的 Animator 终态，以及两个 `dogsDeadBodys` 的层级、朝向、显隐时机，仍需在 Unity PlayMode 目视确认。
- 帽子“打开”当前使用轻量上移、缩放与 SpriteRenderer 透明度渐隐表现，需确认是否满足最终美术表现；若有专用打开动画，应在 prefab/Animator 中替换为正式动画入口。
- 狗相向移动触发 fight 使用距离阈值检测，需在实际狗碰撞体和美术尺寸下确认触发点观感。
- Editor PlayMode 左上角验证按钮位置和文案需确认是否影响内部 QA 观察视图；按钮不会进入移动端构建。
## 2026-06-09 修复记录

- 三帽两狗对撞分支不再运行时 `Destroy(PuppyEnemyCtrl)`；改为临时禁用两只狗的 `PuppyEnemyCtrl.enabled`，并在 `RestoreDog(...)` 中恢复初始 enabled 状态，避免本局 Reset / 失败 / 通关回退后丢失危险碰撞组件。
- `DogEnemyCtrl.OnUpdate()` 仍由 Level66 手动驱动，因为 `ActorCtrl` 只暴露 `OnUpdate()`，没有自身 Unity `Update()`；本次把 chase / confrontation 分支改为只设置输入，统一在每帧末尾对 dogA / dogB 各调用一次 `OnUpdate()`。
- 两狗对撞距离检查改为在统一 `OnUpdate()` 之后执行，避免同一帧内先刷新一次、再移动一次造成移动与动画重复刷新。
- `FaceDog(...)` 保留一次性 `OnUpdate()`，只用于对撞等待前立即刷新朝向并立刻停止输入，不属于每帧驱动路径。

## 2026-06-10 清理记录

- 移除未启用的帽子出现动画残留：`hatsShowDuration`、`ShowHatsAnimation()` 注释代码和对应 prefab 序列化字段。
- 移除三帽 fight 中未继续使用的 `PingPongMoveHandle` 控制字段及相关注释调用；fight 当前只显示 `fightPresentation`，等待 `fightDuration` 后设置 `fightAnims.isStop=true` 并显示 `dogsDeadBodys`。
- 清理 `SwitchGameStateEvent.SUCCESS/FAILED` 已注释恢复分支；当前 SUCCESS/FAILED 不再由 Level66Ctrl 做额外恢复，避免与外部通关/失败流程重复处理。

## 2026-06-10 洗牌曲线调整

- `CalculateShuffleSwapDuration(...)` 现在使用 `shuffleDurationCurve.Evaluate(normalizedIndex)` 计算过渡比例；曲线 x 轴为洗牌进度，y 轴为从最长单次时长到最短单次时长的比例。
- 脚本字段默认值为线性兜底；`Level_066.prefab` 会保存 Inspector 中实际调整后的 `shuffleDurationCurve`，用于改变洗牌加速手感。
