# Level66 风险评估

## 原始行为基线

- `Level66Ctrl.cs` 只有帽子戏法功能注释，`Start` 和 `Update` 为空，运行时不会锁移动、洗牌、处理帽子点击或揭示狗/钥匙。
- `Level_066.prefab` 根节点有 `BaseLevelCtrl` 与 `Level66Ctrl`；`BaseLevelCtrl.gamePlayPanelName` 保持 `GamePlayPanel_000`。
- 三个帽子 `magicHat_1`、`magicHat_2`、`magicHat_3` 已配置 `ClickItemEventHandle`，但 `OnClickEvent` 为空。
- 关卡已有 `Key` 和两只 `PuppyEnemy`；狗对象额外挂有 `DogEnemyCtrl`。
- 当前活动规格为 `specs/008-level66-presentation-update`，本次按该规格实现 Level66 新增表现细节。

## 受影响文件与资源

- `BrainPunk/Assets/Game/Scripts/Level/LevelItem/Level_66/Level66Ctrl.cs`
- `BrainPunk/Assets/Game/Prefabs/Level/Level_066.prefab`
- `BrainPunk/Assets/Game/Scripts/Level/LevelItem/Level_66/Level66功能说明.md`
- `BrainPunk/Assets/Game/Scripts/Level/LevelItem/Level_66/Level66风险评估.md`
- `BrainPunk/DevelopMD/已有功能设计与使用记录.md`

## 主要风险

- 手工修改 Unity prefab YAML 存在引用写错风险；已先采集根节点、帽子点击组件、actor、key、dog、fight 候选对象 fileID。
- `ClickItemEventHandle.OnClickEvent` 是私有序列化 UnityEvent，运行时不能直接追加 listener；本次已在 `Level_066.prefab` 中手工绑定，但仍需 Unity Editor Inspector/PlayMode 验证。
- `Sp_cj220_1_09` 作为打架表现对象的判断来自 prefab 名称、初始隐藏状态和场景位置，尚需 PlayMode 目视确认。
- `fightDuration` 到时后会切 `fightAnims.isStop=true` 并显示 `dogsDeadBodys`；需确认 fight 停止姿态和死亡狗体显隐符合美术预期。
- 狗追击与对撞复用 `DogEnemyCtrl` 的移动循环，需 PlayMode 验证碰撞危险、朝向、动画和地面检测是否符合关卡地形。
- 帽子打开暂用局部上移/缩放和 SpriteRenderer 透明度渐隐表现，若已有正式帽子打开动画，需要后续替换为美术动画入口。
- 洗牌改为每次随机选择两顶不同帽子互换位置，需 PlayMode 验证随机性、最终三个位置不重叠，以及 `shuffleSwapCount` / `shuffleSwapDuration.x/y` / `shuffleDurationCurve` 在 Inspector 中调整后能按曲线控制单次交换时间从最长过渡到最短。
- Editor 验证按钮使用 `#if UNITY_EDITOR` 包裹并放在 `OnGUI`，需在 Editor PlayMode 确认按钮只在洗牌完成且等待首次点击时出现，并且不会遮挡 QA 观察重点。
- 普通 `dotnet build Assembly-CSharp.csproj` 为了生成缺失的 `Temp/obj/Assembly-CSharp/project.assets.json` 执行过一次 restore；之后 quickstart 要求的 `--no-restore` 编译已通过。

## 验证清单

- [x] 编译：`cd BrainPunk && dotnet build Assembly-CSharp.csproj --no-restore`，2026-06-08 结果 0 errors / 1049 个项目既有 warnings，未见 Level66 相关 error。
- [x] Prefab 静态检查：确认 `Level66Ctrl` 引用、三个帽子 `OnClickEvent`、`GamePlayPanel_000`、Key、两只狗、fight 候选对象已写入 prefab YAML。
- [ ] Prefab Editor 检查：在 Unity Inspector 确认上述引用无 Missing、UnityEvent 可调用。
- [ ] US1 PlayMode：进入关卡后 1.5 秒内 actor 和帽子不可交互，Key 和两只狗显示，fight 隐藏，1.5 秒结束后 Key 和两只狗隐藏。
- [ ] US2 PlayMode：Key 和狗隐藏后才开始洗牌；每次随机两顶不同帽子互换位置，`shuffleSwapCount`、`shuffleSwapDuration.x/y` 和 `shuffleDurationCurve` 可调，单次交换时间按曲线从最长过渡到最短，完成交换后三个帽子不重叠。
- [ ] US3 PlayMode：1/2/3 个不同帽子点击分支、重复点击、晚点击、无首次点击均符合规格；选中帽子打开后渐隐消失且不可点击。
- [ ] US4 PlayMode：一只狗追击 actor；三帽分支两只狗先朝向、等待 1 秒、相向移动、触发 fight；fight 显示 2 秒后 `fightAnims.isStop=true` 且两个 `dogsDeadBodys` 显示。
- [ ] US5 PlayMode：Editor 两帽/三帽验证按钮只在洗牌完成等待首次点击时出现，使用当前内部洗牌顺序，每次尝试只触发一次；Key 通过原有流程拾取/使用/通关；失败、重开、退出、通关、暂停/恢复不会留下延迟回调。
- [ ] 非 Level66 回归：至少验证一个已验收关卡的 actor、key、enemy 行为。
- [ ] Android/iOS：横屏触摸快速点击、洗牌、狗追击、fight、重启/失败/退出 smoke。

## 当前实现证据

- `Level66Ctrl` 定义本地 `Level66State`，覆盖 OpeningLocked、Shuffling、WaitingForFirstClick、SelectionWindow、Revealing、DogChase、DogConfrontationDelay、DogConfrontationMoving、FightShown。
- `Level66Ctrl` 在 Awake/Start 中解析并缓存引用，`GameResetEvent` 触发新 session；`OnDisable`/`OnDestroy` 取消延迟和 tween。`SwitchGameStateEvent` 当前只处理 Pause/Resume。
- 开场通过 `BaseLevelCtrl.SetCtrlable(false)`、`ActorCtrl.StopMoving()` 和 `SetHatInteraction(false)` 锁定移动与帽子交互，同时显示 Key 与两只狗，隐藏 fight presentation。
- 固定 1.5 秒开场结束后调用本地隐藏逻辑隐藏 Key 和两只狗，随后才构建并播放程序控制洗牌；洗牌每步随机选择两顶不同帽子交换当前位置。
- 0.2 秒窗口只收集不同帽子，重复同一帽子会被 `_selectedHats.Contains(...)` 忽略；窗口结束后 `SetHatInteraction(false)` 并只执行一次 outcome。
- 选中帽子在结算时缓存揭示前世界坐标，先上移/缩放，再对帽子下所有 SpriteRenderer 做透明度渐隐，完成后隐藏帽子对象并关闭点击；restart/re-entry 恢复 active、位置、缩放、透明度和点击状态。
- Editor PlayMode 验证按钮通过 `#if UNITY_EDITOR` 包裹，触发前检查 PlayMode、非暂停、未结算、未触发过、状态为 WaitingForFirstClick。
- Dog chase 每帧根据 actor 与狗的 x 位置设置 `DogEnemyCtrl.SetMoveInput(...)` 并调用 `OnUpdate()`；两狗对撞分支使用 1 秒可取消等待和 x 距离阈值触发 fight。
- fight 进入时显示 `fightPresentation`，并在本地可暂停计时 `fightDuration` 结束后设置 `fightAnims.isStop=true`、显示 `dogsDeadBodys`；restart / reset / disable 会重置 stop bool 和死亡狗体显隐。
- `Level_066.prefab` 中 `magicHat_1/2/3` 已分别绑定 `ClickHat1/2/3`，`BaseLevelCtrl.gamePlayPanelName` 未改变。

## 未验证缺口

- 未运行 Unity Editor PlayMode，无法确认实际开场 Key/狗显示、隐藏后洗牌、帽子打开/渐隐视觉、Editor 验证按钮、Key pickup/use、狗地面检测、狗碰撞杀 actor、fight 候选对象表现。
- 未运行 Unity Editor PlayMode，无法确认 fight 的 2 秒停播节奏、`isStop` 终态、死亡狗体显示位置，以及 pause/resume 中 fight 计时观感。
- 未运行 Android/iOS 真机或模拟器，无法确认快速多点触摸在 0.2 秒窗口内的实际输入表现和 60 fps。
- 未执行非 Level66 回归关卡，无法用本轮证据证明 actor/key/enemy 共享行为在运行时完全无回归。
- 未通过 Unity MCP 或 Editor 自动保存 prefab；当前 prefab 接线为手工 YAML patch，建议首次进 Unity 后做 Inspector 检查并保存一次。

## 回滚路径

- 回滚 `Level66Ctrl.cs`、`Level_066.prefab`、Level66 本地文档和 `DevelopMD/已有功能设计与使用记录.md` 的本次改动即可；本实现不改共享点击、actor、key、dog、存档、配置表、ProjectSettings 或平台 SDK。
## 2026-06-09 P1 修复结论

- P1-1 已修复：三帽两狗对撞分支从销毁 `PuppyEnemyCtrl` 改为禁用 `PuppyEnemyCtrl.enabled`，`CacheDog(...)` 缓存初始 enabled 状态，`RestoreDog(...)` 在 Reset / Disable / SUCCESS / FAILED 路径恢复该状态。
- P1-2 已修复：Level66 每帧先由 chase / confrontation 状态设置 `DogEnemyCtrl.SetMoveInput(...)`，再通过 `UpdateDogsOnce()` 对 dogA / dogB 各调用一次 `OnUpdate()`；对撞距离检查在狗更新后执行。
- 静态证据：`Level66Ctrl.Update()` 当前顺序为 `UpdateDogChase()`、`UpdateDogConfrontation()`、`UpdateDogsOnce()`、`CheckDogConfrontationReached()`；代码内已无三帽分支的 `Destroy(_*.GetComponent<PuppyEnemyCtrl>())`。
- 剩余未验证：尚未在 Unity PlayMode 触发三帽 fight 后执行 Reset / 失败 / 通关 / 暂停恢复，仍需确认 `PuppyEnemyCtrl.enabled` 恢复后危险碰撞表现与原 prefab 一致；Android/iOS 设备未验证。

## 2026-06-10 清理结论

- 已移除 `Level66Ctrl` 中未启用的帽子出现动画残留，包括 `hatsShowDuration`、`ShowHatsAnimation()` 注释代码和 `Level_066.prefab` 上的旧序列化字段。
- 已移除 `Level66Ctrl` 对 `PingPongMoveHandle` 的不完整残留引用和 `_fightStopped` 空状态；prefab 中的 `PingPongPath1 (1)` 对象仍保留但不再由 Level66Ctrl 引用。
- 已移除 `SwitchGameStateEvent.SUCCESS/FAILED` 的已注释恢复分支；当前 Level66Ctrl 只对 Pause/Resume 做游戏状态响应，reset / disable / destroy 路径继续负责本地清理。
- 编译已通过，仍需 Unity PlayMode 确认清理后 fight 表现、重开/退出/失败/通关路径和 Inspector 序列化无 Missing。

## 2026-06-10 洗牌曲线风险补充

- `CalculateShuffleSwapDuration(...)` 改为用 `shuffleDurationCurve` 计算过渡比例；曲线输出会被限制在 0..1，避免异常曲线把单次交换时间推到配置最短/最长范围外。
- `Level_066.prefab` 已写入可调 `shuffleDurationCurve`；脚本字段默认线性只作为未序列化时的兜底。后续在 Inspector 调整曲线后仍需 PlayMode 验证实际加速观感和总洗牌时长是否符合设计。
