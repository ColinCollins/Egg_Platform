# IWannerGame 玩法制作进度 Checklist

> 更新时间：2026-08-13
> 当前结论：基础框架、首个 Group 配置解析、入口、独立 RemoteConfig 配置基础、YooAsset 收集、关卡加载和 HUD 输入逻辑已进入联调阶段；
> HUD 组内进度、成功推进、普通失败重建、失败阈值 Skip 激励广告门禁、Group 断点进度、临时 Id 选组入口、Group VictoryPanel、IWanner 独立插屏链与正式小关打点代码已接入；正式选组状态、广告/打点真机验证、TBN 和 Victory PlayMode 尚未完成验收。

## 状态说明

- `[x]`：已有代码或资源证据，并完成可执行的静态检查。
- `[ ]`：尚未实现或尚无本地证据。
- `[ ] ⚠️`：已有部分代码/资源，但缺少绑定、闭环或 Unity/设备验证，不能视为完成。

## P0：当前可进入联调的基础链路

- [X] 注册 `PlayModeType.IWanner`、Policy 和 `IWannerPlayCtrl`，由 `PlayControllerManager` 管理生命周期。
- [X] Loading 在 Luban 初始化后创建并初始化 `IWannerGameLevelCtrl`。
- [X] `IWannerGameLevelCtrl` 支持读取 Group 列表、按 `groupId` 查询以及按组内索引解析小关数据。
- [X] 当前临时入口固定进入配置第一行 Group 的第 0 小关。
- [X] `WannerGameEnter.prefab` 已挂 `IWannerGameEnter`，入口按钮引用已绑定。
- [X] `WannerGameEnter.prefab` 已作为嵌套 prefab 接入 `StartPanel.prefab`。
- [X] `IWannerPlayCtrl` 使用 Bear `ResourceManager.LoadAsync<GameObject>()` 异步加载关卡 prefab。
- [X] 运行时加载目录固定为 `Assets/Game/Prefabs/WannerLevel/`。
- [X] YooAsset `GamePackage/Level` 已加入 WannerLevel collector，沿用 `PackSeparately + CollectPrefab`。
- [X] 首个 Group 配置引用 `30001～30005`，对应 `ILevel_001～ILevel_005` 五个 prefab 已存在。
- [X] 五个 prefab 均挂有 `IWannerLevelCtrl`，并引用/包含 `Actor_IWanner` prefab。
- [X] `IWannerLevelCtrl` 具备初始化、输入门禁、暂停、恢复和退出清理边界。
- [X] C# 定向构建通过：`0 error / 101 warning`，warning 为工程既有范围。
- [X] ⚠️ 在 Unity PlayMode 从 StartPanel 实际进入首个 Group，并确认 HUD、关卡、相机和角色同时正常出现。
- [X] ⚠️ 在非 Editor 的 YooAsset GamePackage 清单/构建中验证五个小关可按完整工程路径加载。

## P0：RemoteConfig 与 Guru 默认配置

- [X] `RemoteConfigService` 启动拉取列表已加入独立顶层 key `iwannergame_config`。
- [X] `ConfigManager.RemoteConfig` 已提供 9 个 IWanner 专用 getter，运行态远端字段缺失时回退同名 `GlobalConst`。
- [X] `guru_spec.yaml` 与 `GuruAllSpecs.G.cs` 已加入相同的 9 项默认值，字段大小写与 Luban JSON 一致。
- [X] `LubanGenerator` 已提供 `Update IWannerGameConfig To Guru YAML/guru_spec.yaml` 菜单，支持保留未知字段、同步 GlobalConst 并串联 Guru 官方生成。
- [X] GM RemoteConfig 分页已加入 `iwannergame_config`，便于运行态查看当前业务缓存。
- [X] `IWannerGameEnter` 使用 `GetIWannerGameIsOpen()` 控制整个入口根节点显隐，并在点击时再次校验开关。
- [X] 普通失败/Skip 已消费 `GetIWannerGameShowSkipFailCount()`；配置小于等于 0 时关闭 Skip，配置为 `n` 时累计第 `n` 次失败后显示。
- [X] 插屏和免广告弹窗流程已消费 IWanner 专属 getter；提示时间字段已从 GlobalConst 与 RemoteConfig API 移除，非正数广告配置沿用共享策略的关闭语义。
- [ ] ⚠️ Unity Editor 实际点击 IWannerGameConfig 同步菜单，确认预览、无变化分支、写回和 Guru 官方生成产物。
- [ ] ⚠️ 验证真实 Firebase 覆盖、远端字段缺失、缓存旧版本无新 key、离线 Guru 默认值和 Android/iOS 启动流程。

## P0：GameplayPanel

- [X] `IWannerGame_GamePlayPanel` 使用 `BaseUIView + Bear.UI.UIManager`，与主线/DrawGame UI 创建方式一致。
- [X] 面板不再持有 `IWannerPlayCtrl`；方向和跳跃复用主线玩家输入事件。
- [X] 左右按住状态由面板逐帧派发，右方向优先级与主线 `GamePlayPanel` 一致。
- [X] Jump 使用 `OnClickDown -> PlayerJumpEvent`，与主线一致。
- [X] `Actor_IWanner` 只在 `IsGround == true` 时接受跳跃请求；离地后不进入共享土狼时间、双跳、水中跳跃或空中缓冲链。
- [ ] ⚠️ Unity PlayMode 与 Android/iOS 横屏验证起跳后连按、走落平台、上升/下落按跳和重新落地后再次起跳。
- [X] Pause 通过 `ActivePauseHost` 调用 Pause/Resume/Exit，复用 `GameSettingPopup`。
- [X] Reset 使用 IWanner 专属 `IWannerGameResetEvent`，由活动 `IWannerPlayCtrl` 接收，不进入主线 Reset 链。
- [X] Group Replay 复用共享 `GameReplayEvent`，由活动 IWanner 控制器重开当前 Group。
- [X] 已提供组内 Toggle 状态刷新逻辑：已完成格与当前所在格 `isOn = true`，后续格为 false。
- [X] Skip 默认隐藏，可见性与输入交互状态分离。
- [X] Skip 按钮通过当前 GameplayPanel 的局部回调提交给活动 `IWannerPlayCtrl`，不新增全局跳关事件。
- [X] `LeftBtn`、`RightBtn`、`JumpBtn`、`PauseBtn`、`ResetBtn`、`SkipBtn` 已在 prefab 绑定。
- [X] `IWannerLevelColorSettings` 可分别配置 HUD 图片色和 `u_level_devil_btn_skip_txt` 文本色；只影响已绑定目标。
- [X] Trap 根节点挂有 `OnTriggerTrapHandle` 时，其层级内 SpriteRenderer 与 Ground 统一应用 `GroundColor`。
- [X] ⚠️ Unity PlayMode 目测 Trap/Ground 色值一致，并确认 Trap 动画不会覆盖初始化后的颜色。
- [X] `IWannerGame_SequenceDisplayListener` 可由 Executor 触发并按列表顺序显示对象；`delay <= 0` 时立即全部显示。
- [X] ⚠️ Unity PlayMode 验证顺序显示的零/正 delay、重复触发、暂停恢复、空引用和销毁取消。
- [X] `IWannerGame_RecycleBackArea` 可按 LayerMask 在 Trigger2D 中销毁或隐藏命中 Collider 所在对象。
- [ ] ⚠️ Unity PlayMode 验证 Destroy/Hide、Layer 过滤、子 Collider 边界与碰撞矩阵配置。
- [X] 全屏 `ResetClickArea` 已在 prefab 默认隐藏并绑定；死亡失败后显示，点击派发 IWanner 专属 Failed Reset 事件。
- [X] `BlackMask` 与 `ClickBlock` 已绑定；进入、Reset、Replay 的黑屏和输入拦截逻辑已接入。
- [X] Inspector 已暴露黑幕目标透明度、渐显到黑时长、渐隐到透明时长。
- [X] GameplayPanel 采用小关级生命周期：每次进入、Reset、Replay 或后续切换小关都销毁旧实例并创建新实例。
- [X] 组内进度只在新 GameplayPanel 创建后初始化一次，不刷新或复用旧面板实例。
- [X] 五个 Toggle 已绑定同一横向容器，直接子节点兄弟顺序与 Group 索引从左到右一致。
- [X] “已过关/未过关”两组图片列表已各自绑定 5 个独立 Image，重复引用已修正。
- [X] 黑幕完全淡出后，五个有效 Toggle 从左到右依次执行“压缩蓄力、上跃拉伸、落地挤压、轻微回弹”的 Q 弹动画。
- [X] 全部 Toggle 落稳后统一应用本次 `currentLevelIndex` 状态；动画使用 unscaled time，面板关闭/销毁时停止并复位位置和缩放。
- [X] ⚠️ Unity PlayMode 验证首次进入、Reset、Replay 和组内切关都会完整重播 Q 弹动画，并确认状态只在全部落稳后切换。
- [X] ⚠️ Unity PlayMode 验证首次进入、Reset 和 Replay 的黑幕完整遮挡、淡出结束后才恢复输入。
- [X] ⚠️ Unity PlayMode 验证按住、松开、滑出、双指左右、暂停恢复和关闭面板时不会残留移动输入。
- [ ] ⚠️ Android/iOS 横屏触控验证方向、Jump、Reset、Pause 的指针取消和多指行为。

## P0：Reset 与 Replay 边界

- [X] HUD Reset 销毁当前关卡与 GameplayPanel，再重建当前 Group 的当前小关，保持 `currentLevelIndex` 不变。
- [X] Victory Replay 销毁当前关卡与 GameplayPanel，保持当前 Group，将 `currentLevelIndex` 重置为 0。
- [X] Reset 与 Replay 已分成独立入口，不互相复用业务语义。
- [X] ⚠️ Unity PlayMode 验证连续 Reset 始终停留在同一小关。
- [X] ⚠️ 接入 VictoryPanel 后验证 Replay 始终从当前 Group 第一关开始。
- [X] 按《关卡打点生命周期约定》补齐 Reset/Replay 对应的 attempt、revive、时长和开始/结束事件。

## P0：小关结果与 Group 推进

- [X] 定义实例级 `IWannerLevelResult`/`Resolved` 结果事件，由 `IWannerLevelCtrl` 一次性上报成功或失败。
- [X] `IWannerPlayCtrl` 在关卡初始化前订阅、销毁前退订；LevelCtrl 不再持有玩法控制器的异步委托。
- [X] `OnLevelResolved()` 同步处理具备异常收口；当前会话异常会淘汰旧异步版本并走现有失败清理。
- [X] `FallArea` 复用现有失败区域事件，死亡幂等、Reset、销毁和旧异步结果由运行时门禁隔离。
- [X] `OnTriggerTrapEvent` 已接入同一 `TryActorDeath()`/`Resolved(Failed)` 失败链；外部陷阱已播放死亡时不重复调用 `Die()`。
- [X] ⚠️ `ILevel_006` 补齐 `OnTriggerTrapHandle.knock` prefab 引用后，PlayMode 验证陷阱只累计一次失败并显示 ResetClickArea。
- [X] 小关成功后将 `currentLevelIndex + 1`，直接加载当前 Group 的下一小关。
- [X] 已建立可复用的关卡重建遮罩边界：先锁定输入并渐显到黑，加载完成后渐隐到透明再恢复输入。
- [X] 小关成功推进时先等待入口门动画完成，再接入上述遮罩边界。
- [X] 下一关创建全新 GameplayPanel，并按新的当前索引初始化 HUD 组内进度。
- [X] 最后一小关成功后停止继续加载，进入整个 Group 的 VictoryPanel 流程。
- [X] 结果链已具备实例幂等、输入锁、`_isOpeningLevel` 和 activation version 门禁。
- [X] `IWannerLevelCtrl` 已提供幂等 `TryActorDeath()` 与 `ActorDeath(Collider2D)` 触发入口；接受死亡后锁定输入并调用 `ActorCtrl.Die()`。
- [X] Actor 死亡时播放当前小关名为 `Feedback` 的 `MMF_Player` 震屏效果，且 Odin `AutoBind` 可持久化该引用。
- [X] 自动失败后保留死亡现场并显示全屏 `ResetClickArea`；用户点击后通过现有黑幕链销毁并重建当前小关。
- [X] ⚠️ Unity PlayMode 验证 ResetClickArea 默认隐藏、死亡后全屏可点、快速连点只重建一次且重建后不残留拦截。
- [X] ⚠️ Unity PlayMode 验证结果事件与 Reset/退出/旧异步加载同时发生时的门禁及退订结果。
- [X] ⚠️ Unity PlayMode 验证死亡触发只执行一次、不会再抢入成功门，并确认缺失 Feedback 时仍可正常死亡。
- [ ] 为没有可通关条件的占位小关定义明确的失败/退出边界。

## P1：Group 进度、选组与存档

- [X] 接入 `DB.IWannerGame`，记录当前 `groupId` 与零基的组内 `currentLevelIndex`。
- [X] 进入相同 Group 时按有效的已记录 `currentLevelIndex` 继续；越界索引回退并修正为 0。
- [X] 进入不同 Group 时把当前小关索引重置并保存为 0。
- [X] 成功/Skip 推进时即时落盘；退出或重启后重新选择相同 Group 可恢复当前小关进度。
- [X] GameSettingPopup Menu 会清理当前关卡/HUD、切回主线并恢复 StartPanel，随后在 StartPanel 上弹出 IWanner 选组界面；选组创建失败时保留 StartPanel 作为安全落点。
- [ ] 参考 `ChoiceLevelItem` 定义 Group 的解锁、已通关和可重玩状态。
- [ ] ⚠️ 已完成按 LevelSort 顺序显示 Id、点击后按同组续关/异组索引 0 进入的临时选组入口；正式解锁、通关、复玩
  状态与本地化名称尚未实现。
- [ ] 选组界面区分锁定、已解锁未通关和已通关状态。
- [ ] Group 配置变化只在下一次 Loading 后生效；游戏内不做实时配置刷新。
- [ ] 配置缺 Group、空 Path、重复小关、越界索引或缺失 prefab 时提供可恢复的失败路径。

## P1：VictoryPanel

- [X] Group 首次通关时打开 `VictoryPanel_Reward`，奖励广告、金币发放与领奖表现保持和
  `DrawGame_GameVictoryPanel_Reward` 一致。
- [X] Reward 广告使用 `RewardPlacement.GameVictory`；成功回调按转盘最终值通过 Bag 发放 Coin，播放通用收集动画并刷新 PropBlock。
- [X] Reward 广告失败或取消时不发金币并恢复转盘；广告请求和领奖动画期间锁定导航，同一面板只允许成功领取一次。
- [X] Group 重复通关时只打开普通 VictoryPanel 弹窗。
- [X] VictoryPanel Replay 接入 `GameReplayEvent -> TryReplayCurrentGroup()`。
- [X] VictoryPanel 下一步与 GameSettingPopup Menu 共用退出逻辑：切回主线并恢复 StartPanel，随后弹出 IWanner 选组界面。
- [X] VictoryPanel 可交互前已清理失败次数、暂停/输入/Skip 与正式 attempt 时长/revive 临时态。
- [ ] ⚠️ Unity PlayMode 尚未覆盖首通、复通、Replay 和退出四条路径；当前只有定向编译与静态 prefab 证据。

## P1：失败次数、Skip 与 TBN

- [X] 失败次数由活动 `IWannerPlayCtrl` 按当前小关保存在内存中，不写存档。
- [X] 自动失败和玩家手动 Reset 都累计当前小关失败次数。
- [X] 阈值为 `n` 时，累计第 `n` 次失败后的重建流程在新 GameplayPanel 创建时显示 Skip；配置小于等于 0 时关闭。
- [X] 退出玩法、Replay、GM 回退或成功/Skip 推进当前小关后清空失败次数。
- [X] Skip 默认不可见；未达到阈值时保持不可交互。
- [X] Skip 点击后使用 `RewardAdHelper` 请求 `RewardPlacement.InGamePassLevel`，只有奖励成功回调才推进下一小关。
- [X] 广告请求前暂停角色并锁定 HUD/Actor 输入；失败、关闭或等待取消时恢复当前关卡，广告期间与 Reset、Replay、暂停和 GM 命令互斥。
- [ ] ⚠️ Unity PlayMode 与 Android/iOS 真机覆盖奖励成功、展示失败、关闭、等待取消、退后台和展示中退出玩法。
- [ ] 为 TBN 配置或单独 Group 建立明确识别方式。
- [ ] TBN 加载普通 GameplayPanel 和不可通关占位小关，保留方向、Reset 等控件。
- [ ] TBN 的 Skip 始终不可见，不受普通失败阈值影响。
- [ ] 当前 `20999 -> DLevel_999` 只有 LevelData，尚未形成 WannerLevel 下可验证的 TBN 资源与 Group 闭环。

## P1：主线/DrawGame 一致性

- [X] Controller、UI、暂停 Host、资源加载和玩法专属 Reset 事件已按主线/DrawGame 基线收敛。
- [X] 未修改共享 `GamePlayPanel`、`ActorCtrl` 或已验收主线/Draw 关卡代码。
- [X] 已补齐与主线/DrawGame 一致的进入遮罩、Reset/Replay 黑幕、加载完成淡入和点击拦截。
- [ ] 补齐成功、失败、退出时与主线/DrawGame一致的资源、事件和运行态清理顺序。
- [X] 补齐关卡开始、Reset、Replay、成功和退出的正式 analytics 生命周期；普通成功与 Skip 成功按小关结算，Group 完成不重复上报。
- [X] 明确 IWanner 使用 `item_category=iwanner`、`level_name=Path`、Path 维度 `play_count/start_type`，累计序号使用主线 + DrawGame + IWannerGame 历史成功次数之和。
- [ ] ⚠️ Unity PlayMode 与真实 SDK 后台验证首次进入、连续 Reset/死亡、成功、Skip、退出、Group Replay 和三玩法切换的事件顺序与字段值。
- [ ] 完成与主线/DrawGame 的切换回归，确认旧玩法事件订阅不会响应 IWanner 输入或 Reset。

## P1：入口门关闭表现

- [X] 新增 `IWannerGame_EnterDoorAnimCtrl : BaseItemEventHandle`，可直接加入 TriggerExecutor。
- [X] 触发时启用 `door_close` 并从头播放 `close` 动画。
- [X] `effect` 按线性 Alpha 渐隐，Inspector 暴露渐隐延迟和渐隐时长。
- [X] `ResetState()` 停止旧 Tween，隐藏关闭态门并恢复 effect Alpha 1。
- [X] `IWannerGame_Door.prefab` 已挂载控制器并绑定两个 Spine 节点。
- [X] 提供 Odin `AutoBind` 编辑器按钮，可按 `door_close/effect` 名称补齐两个 Spine 引用。
- [X] `IWannerLevelCtrl` 提供 Odin `AutoBind`，可自动绑定门引用并持久化 `OnEnter -> Success(Collider2D)`。
- [X] 未持久化 Listener 时提供当前小关实例内的运行时绑定兜底，Shutdown 时移除。
- [X] 标准成功链直接等待 `EnterDoorAnimCtrl.PlayAsync()` 完成，不依赖 TriggerExecutor 推测完成时间。
- [X] 成功触发后立即锁输入、清零刚体速度并把 Actor 瞬移到门根节点位置；随后关闭当前 QBox 层级全部
  `Collider2D` 与根 `Rigidbody2D` 模拟，进门表现期间不再参与 2D 碰撞。
- [ ] ⚠️ 在每个含成功门的 WannerLevel prefab 点击 LevelCtrl AutoBind、保存并核对 OnEnter。
- [ ] ⚠️ Unity PlayMode 验证瞬移落点、进门后 Collider/Rigidbody 确实关闭、单次/重复触发、Reset、销毁中断和
  特效渐隐时序。

## P2：验证与交付门禁

- [ ] Unity PlayMode：入口、首关加载、移动、Jump、Pause/Resume、Reset、Menu 返回选组与失败回退。
- [ ] Unity PlayMode：五小关连续成功推进、Group Victory、Replay、再次通关。
- [ ] Unity PlayMode：加载失败、缺 Actor、缺 HUD 字段、缺 prefab 和配置越界降级。
- [ ] 连续执行至少 20 次 Reset/Replay/切组，观察旧实例、事件订阅、Collider 和输入残留。
- [ ] YooAsset Simulate/Host/Offline 目标模式至少覆盖项目实际发布使用的模式。
- [ ] Android 横屏真机冒烟验证。
- [ ] iOS 横屏真机冒烟验证。
- [ ] 完成正式功能记录、风险评估、回滚说明及验收结果更新。

## 当前推荐实施顺序

1. 完成首个 Group 的 PlayMode 入口、黑幕、Q 弹进度与输入联调。
2. 在 PlayMode 覆盖五小关成功、自动失败、手动 Reset、阈值 Skip 与异步互斥。
3. 接入 Group VictoryPanel，并固定 Reset/Replay 两条独立链路。
4. 接入 Group/小关存档与选组解锁状态。
5. 接入 TBN，并完成 Skip 激励广告的 Unity PlayMode 与 Android/iOS 真机联调。
6. 完成已接入 analytics 的 PlayMode/SDK 后台核对，并完成插屏、激励广告和免广告弹窗的设备验证。
