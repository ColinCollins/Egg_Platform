# GameVictoryPanel_DeepLink 功能与风险记录

## 2026-10-14 IWanner OneLink 不推进解锁，Next 固定回第一关

### 修改前核对

- 本次范围为 IWanner：撤回昨日“Next 进入最早未通关 Group”的选择规则；OneLink 完成 Group 后不解锁下一 Group，Next 固定进入当前配置第一行 Group 的第 0 小关。主线、Draw、Dig 与普通 IWanner 入口保留。
- 基线与来源：`IWannerPlayCtrl.OnEnterNextLevelEvent` 调用昨日新增的 `TryEnterLowestPlayableUnpassedGroup`；`RecordGroupVictoryProgress` 无来源区分地新增 `PassedLevels`、补齐 `UnlockGroups` 并推进 `MaxGroupId`。`IWannerGameLevelCtrl.RestorePassedGroupUnlocks/ResolveCurrentGroupId`、Popup `ResolveAndSaveCurrentGroupId` 和实际入口 `CanEnterGroup` 都会从 Group 通关记录推导后继资格，仅跳过胜利时的一次解锁不足以满足要求。
- 拟改位置：仅在 `IWannerPlayCtrl` 隔离 OneLink 的 Group 进度写入，保留当前 Group/组内索引保存，但不新增 Group 通关、不推进最高 Group、不补后继解锁；小关成功统计、打点和组内推进照常。Next 复用 `TryGetFirstGroup`、第 0 小关查询和原黑幕重建，接受或失败后按原入口清理来源。无需新增存档字段、生成代码、事件或 Prefab 引用。
- 保留边界：不删除既有通关/解锁，不反向锁关；同玩法 Replay 保留 OneLink 来源，再胜仍不推进 Group；Next 接受后来源结束，首组以普通游玩继续。普通完整 Group 胜利照常通关、解锁并推进最高进度。TBN 无正常胜利分支，现有权限和广告流程不改。
- 验证场景：OneLink 首组/后组/末组胜利及重复胜利、旧档已有解锁、普通胜利、选组归一/入口推导、JSON 副本重载、保存失败、第一组已通关/组内续关/全部已通关、非连续 ID、首组无效/缺关卡资源配置、Next 重复与加载/广告门禁、来源清理和普通 Next；静态比较其它玩法/UI/统计/资源重建保持。执行隔离源码断言、Editor/Android/iOS 条件编译和差异检查；PlayMode、真实磁盘/资源/广告与双端真机仍待验证。
- 回滚仅恢复本轮 IWanner 控制器差异；不批量回退昨日提交、不覆盖其它任务，也不删除历史存档。2026-10-13 选择器及 2026-10-09“DeepLink 也解锁后继”的描述自本节生效后只作历史基线。

### 实现与验证

- 实现仅修改 `IWannerPlayCtrl`：`RecordGroupVictoryProgress` 保留当前组/索引保存，把新增 `PassedLevels`、`RestorePassedGroupUnlocks` 和 `MaxGroupId` 推进限定为非 OneLink；Next 改为 `TryEnterFirstGroupAfterDeepLinkVictory`，使用首组查询、第 0 小关和原输入锁/保存/黑幕重建尾部。旧方法注释因目标规则改变而定向更新，其余原注释未调整。
- OneLink 最后一小关仍在 `OnLevelResolved` 先调用 `_levelStatistics.CompleteAttempt(true, false)`，之后完成门动画、Group 结算、插屏与专属面板；组内推进和小关统计不改。`OneLinkLevelRouter.TryEnterIWanner` 原有“只解锁本次目标 Group”保留，不为本次胜利授予后继。
- TEMP `C:/Users/admin/AppData/Local/Temp/brainpunk-iwanner-onelink-first-20261014/Verify.ps1` 抽取 8 个实际生产方法，直接编译完整配置控制器及存档模型；Editor、Android、iOS 三套宏各通过 **270 项断言**。包含所有 15 个配置 Group 的 OneLink 重复胜利、普通通关/旧档恢复、末组 TBN 边界、JSON 副本重载/选组/实际入口无新增后继、已有记录保留、保存失败、固定首组/首关、全部通关与非连续 ID、首组无效/空 Path、Next 来源清理/重复/普通分支。
- `VerifyBoundaries.ps1` 的 **21 项静态检查**通过：12 个原生命周期/统计/Replay/加载/入口方法、Next 原遮罩重建尾部、7 个 UI/OneLink/存档/配置/选组文件与本轮差异格式核对。UI、生成绑定、Prefab、其它玩法和只读目录无本轮运行时修改。
- 使用 Unity 6000.0.59f2 当前 Bee 清单与真实引用定向编译：Editor `0 error / 113 warning`，Android `0 error / 118 warning`，缺失源文件数均为 0；iOS 仍为既有 `ChoiceLevelPanel.cs:17`、`StartPanel.cs:14` 两处 `Guru.UniKit.Apple` 引用错误，不宣称 iOS 全量编译通过。全工作区差异检查发现用户已有 `en SDF.asset` 空白问题；本轮文件的定向 `git diff --check` 通过，未修改字体。

### 剩余风险与验收

- `uncertain`：隔离测试中的 Unity UI/资源实例/OneLink 来源及 DB 磁盘外围为替身；JSON 副本重载不等于 Bear PlayerPrefs 实际落盘。尚未执行 PlayMode、真实 OneLink、广告原生覆盖恢复、进程重启或 Android/iOS 横屏真机。
- 不回收历史解锁或通关；历史普通通关缺少后继解锁时，既有旧档补齐仍可授予该资格，不属于本次 OneLink 新胜利产生的解锁。旧版 OneLink 已写入的 Group 通关也无法在无来源字段时与普通通关区分，本轮不迁移或删除。
- 本次 OneLink 胜利不新增整组 `PassedLevels`，因此也不凭本次胜利授予末组通关的 TBN 资格、Group 已通关显示或消费普通首通判定；后续普通进入并完成该组仍按真实 Group 首通结算。小关历史成功次数保留，统计 `start_type` 仍以小关成功次数为准。GM OneLink 测试同样遵循来源规则，未带该来源的普通 GM 入口仍照常通关。
- 最短实测：保留未解锁后继的存档，通过 OneLink 进入 Group7 并完成整组，核对胜利页及 Group8 未解锁；Replay 再胜仍不解锁；Next 应为 `Group1 / ILevel_001 / index 0`，不是保存的组内续关；返回/重启后 Group8 仍锁定。随后普通完成 Group1，确认原下一组解锁。失败时保留原档与 OneLink/Group/资源/保存日志，按本节回滚控制器差异，不清档掩盖问题。

## 2026-10-13 IWanner OneLink Next 按未通关 Group 跳转（历史规则）

### 需求与行为基线

- 用户实测目标：OneLink 进入 IWanner 第一个 Group 并完成整组后，胜利页点击 `nextBtn`，应进入配置顺序最靠前、当前可进入且尚未通关的 Group；当前配置下通常是 Group2，也允许在更早候选不可进入、已通关或无效时选择其它 Group。
- 当前源码事实：`GameVictoryPanel_DeepLink.OnNextLevel()` 只销毁面板并派发既有 `EnterNextLevelEvent`；`IWannerPlayCtrl.OnEnterNextLevelEvent()` 在确认 IWanner OneLink 上下文后调用 `TryEnterLowestPlayableUnpassedLevel()`。该方法按 Group 配置顺序遍历，但以每个小关的 `LevelSuccessCount` 为排除依据，因此可能重新选中已经记入 `PassedLevels`、但小关成功计数缺失或不完整的 Group。
- 当前进度事实：整组成功先由 `RecordGroupVictoryProgress()` 把 Group Id 写入 `PassedLevels`，再通过 `RestorePassedGroupUnlocks()` 解锁配置中的直接后继 Group；`CanEnterGroup()` 是进入资格的现有权威门禁。当前 JSON 按 Group1、Group2……排列，所以 Group1 成功后，第一个有效候选应为 Group2 的第 0 小关。
- 必须保留：普通 IWanner 胜利 Next 返回选组、Replay 重玩当前 Group、OneLink 标记在目标被接受后清理、插屏完成后再显示胜利页、原黑幕重建、输入锁、存档和统计链路均不改变。

### 拟修改位置、理由与影响范围

- 仅修改 `IWannerPlayCtrl` 的 OneLink Next 私有目标解析：按冻结的 `LevelGroups` 配置顺序，跳过 `PassedLevels` 中的 Group，再复用 `CanEnterGroup()` 和 `TryGetLevelData(groupId, 0)` 校验，进入首个合格 Group 的第 0 小关。
- “最小 Group”解释为配置顺序最靠前，而不是假定 Group Id 连续或直接使用 `Id + 1`；这与现有选组、解锁和当前配置权威顺序一致。
- 不修改 `GameVictoryPanel_DeepLink`、Prefab、全局事件、Group 通关落盘、广告、TBN、打点、关卡资源、存档结构、Packages、GuruSDK 或 GF。
- 已核对 `DevelopMD/关卡打点生命周期约定.md`：本轮不改变 `level_start`、`level_end`、成功次数、Replay/Reset 或结算清理时序，不与现行约定冲突。

### 事前验证场景与未决项

- Group1 已通关且 Group2 已解锁/未通关：选择 Group2 第 0 小关；Group1 即使缺少某些 `LevelSuccessCount` 也不得被重新选中。
- 更早 Group 已通关时跳过；未通关但锁定时跳过；候选第 0 小关配置无效时继续寻找下一个可进入、未通关且有效的 Group。
- 候选 Group 存在部分小关成功记录但整组未通关时，仍从该 Group 第 0 小关进入；这是本次由“小关级目标”调整为“Group 级目标”的明确边界。
- 所有合格 Group 均已通关或不存在有效目标时，结束 OneLink 标记并沿用返回选组降级；普通入口与 Replay 不受影响。
- 计划执行当前源码的隔离行为断言、Editor/Android 定向编译与差异检查；Unity PlayMode、真实 OneLink、PlayerPrefs 和 Android/iOS 横屏真机链路在未实测前均标记为 `uncertain`。

### 最终实现与验证证据

- `TryEnterLowestPlayableUnpassedLevel()` 已改为 Group 级的 `TryEnterLowestPlayableUnpassedGroup()`：先以 `PassedLevels` 跳过已通关 Group，再复用 `CanEnterGroup()` 校验当前资格，并只读取候选 Group 的第 0 小关。进入后的存档、输入锁、遮罩和 `ReloadCurrentLevelAsync()` 调用保持原链路。
- Group1 已通关且小关成功计数全部缺失时仍选择 `Group2 / ILevel_006 / index 0`；Group2 有部分小关成功记录但未整组通关时仍从 `ILevel_006` 开始；Group1、2 已通关时选择 `Group3 / ILevel_011 / index 0`；无效候选会继续选择更后的可进入 Group；全部 Group 已通关时不伪造目标。
- TEMP `C:/Users/admin/AppData/Local/Temp/brainpunk-iwanner-onelink-next-group-20261013/Verify.ps1` 每套宏从当前生产源码抽取 7 个实际方法，并直接编译配置控制器和存档模型；Editor、Android、iOS 条件各通过 **115 项断言**。Unity UI、资源实例化和 DB 磁盘外围为替身，不等同于 PlayMode 或平台端到端验证。
- Unity 6000.0.59f2 当前 Bee 引用定向编译：Editor `0 error / 113 warning`，Android `0 error / 118 warning`，两者缺失源码数均为 0；iOS 仍只有任务外 `ChoiceLevelPanel.cs:17`、`StartPanel.cs:14` 的两处 `Guru.UniKit.Apple` 缺失错误。
- 旧隔离脚本依赖 2026-10-09 本机存档含 Group1 通关，当前玩家存档已不满足该历史前提；本轮未清档或改写注册表，改用合成状态矩阵验证目标选择，不能把旧存档状态变化解释为本次代码问题。

### 剩余风险与回滚

- `uncertain`：尚未在 Unity PlayMode 或真实 OneLink 链路完成 Group1→胜利页→Next→Group2 的资源加载、遮罩和输入恢复，也未验证 PlayerPrefs 重启及 Android/iOS 横屏真机。
- 本轮不会续玩候选未通关 Group 的首个失败/未成功小关，而是按用户要求统一从该 Group 的第 0 小关进入；如果产品期望“选 Group 后恢复组内进度”，需另行定义，不应重新混用 `LevelSuccessCount` 来判断 Group 是否通关。
- 回滚只需把 OneLink Next 私有选择器恢复为按 `LevelSuccessCount` 遍历小关及对应方法名/日志；没有新增存档字段或数据迁移，不删除已经形成的 `PassedLevels`、`UnlockGroups` 或小关统计。

## 2026-10-12 Dig 待播星存档对齐（当前增量）

- 背景：用户此前要求沿用 Draw 星级规则；前轮 CreateForDig(int,int) 仅接内存增量，本轮纠正未获确认的差异。修改前来源、生成／存储边界、失败场景与回滚已先记录在 DigGame功能说明.md 的同日待播星章节。
- 现在 DigPlayCtrl.TryRecordVictoryStars 在工厂前保存最高星／待播正增量；CreateForDig(DigLevelData) → ConfigureDigStars → ConsumeDigWaitingStars 只读写独立 DB.DigGame。读取后删除当前 DataId 的待播项并保存，再调用已有 RefreshStars 和门禁；缺星组仍消费并解除门禁，与 ConfigureDrawStars 相同。
- 最高星不降；待播数限制在 0～最高星。没有等待值时不伪造新增星；面板创建失败不进入消费，记录仍可供后续配置读取，但不存在启动时自动重开面板／补播承诺。读取消费在动画完成前发生，途中退出也不会把记录恢复。
- 范围：修改本类 Dig 专用工厂、增加 Dig 配置与消费方法；普通／Reward 由其独立基类消费。共享 Prefab、星组组件、Main/Draw/IWanner 工厂和消费、tips、Home/Replay/Next、OneLink 与广告不改。旧工厂注释因内存传参职责不再成立定向更新；下方早期 Dig “不增加等待存档”描述被本节替换。
- 验证：89 项实际方法／JSON／生成器隔离断言包含普通首胜／复胜／奖励／DeepLink、保存失败、UI 工厂空、缺星组、已有待播、消费异常和 Draw 同场景比较；33 项原方法／源码保持检查通过。Editor/Android 宏编译 0 错误，iOS 两处原 Apple 引用缺失。证据：DevelopMD/验证记录/Dig待播星对齐_2026-10-12/。真实 UI/Spine/PlayerPrefs／双端横屏仍 uncertain。
- 风险与回滚：消费保存失败沿 Draw 的内存删除／旧磁盘记录保留语义，后续重启可能再次读旧等待值，未扩展为完成确认机制。仅撤 Dig 星级存档增量和本类 Dig 专用方法／签名，不回滚其它三玩法或删除玩家最高星／通关；旧版本忽略新增 JSON 字段。

## 2026-10-09 IWanner 通关后继解锁补齐

- 当前 Editor 存档 `Passed=[1,7,5] / Max=8 / Unlock=[7,5]` 让 Next 的原 `CanEnterGroup()` 跳过已通关 Group1 的后继 Group2，随后命中 Group7 被跳过的小关 `ILevel_031`；不是按钮从当前关开始遍历。
- 本轮在 IWanner 通关写入、选组归一和权威门禁中补齐每个已通关 Group 的直接下一组永久解锁；旧档不需重打一遍。Next 的遍历、成功次数判断、事件、黑幕重建及 DeepLink 标记清理均未修改，新增资格仍不放行无通关前驱的更后组。
- 真实存档副本的实际 Next 方法隔离执行选择 `Group2 / ILevel_006`，没有虚构小关成功次数；Editor/Android 定向编译0错误。Unity 资源/UI/存储外围为替身，PlayMode与真机仍为 `uncertain`；iOS仍被既有Apple引用缺失阻断。
- 完整事前核对、三套宏各133项断言、行为边界与数据回滚限制见 [IWanner 选组功能记录](../IWannerGame/ChoiceLevelPopup/IWannerGame_ChoiceLevelPopup临时功能与风险记录.md) 的同日后继解锁章节。只回滚本轮解锁补齐增量，不撤销此前 DeepLink 胜利需求或删除玩家已有解锁。

## 需求与验收范围

- 通过 DeepLink 进入主线、Draw 或 IWanner 关卡后，胜利结算默认打开 `GameVictoryPanel_DeepLink`。
- `home_btn` 返回主页，`replay_btn` 重玩本次结算关卡；IWanner 的胜利结算单位为 Group，因此沿用现有“从当前 Group 第 0 小关重玩”的语义。
- `nextBtn` 在主线/Draw/Dig 进入当前玩法按配置顺序最靠前、有效且尚未通过的关卡；IWanner 按 2026-10-14 当前规则固定回首组首关。不得绕开各玩法现有加载、转场和打点链路。
- 新脚本不得赋值或替换 `tips_txt`；DeepLink 面板不绑定、不执行分享功能。
- IWanner 使用该面板时，星星固定显示三颗，不读取主线明信片星或 Draw 星级存档。

## 修改前核对（2026-10-09）

### 可观察事实

- `Resources/GameVictoryPanel_DeepLink.prefab` 是 `GameVictoryPanel.prefab` 的变体，根节点当前仍继承 `GameVictoryPanel` 组件。
- 变体把基座的分享按钮节点重命名为 `home_btn`，但旧组件仍会把同一个按钮当作分享入口；旧脚本还包含 tips 文本替换、明信片引导、免广告和奖励版分支，不符合本次专用面板职责。
- 主线、Draw、IWanner 已分别存在 Replay 和胜利后导航链路：主线复用 `GameReplayEvent`/进关事件；Draw 的胜利下一关必须通过 `TryBeginLevelTransition(..., keepCurrentLevelUntilCovered: true)` 保留旧 DLevel 至翻页遮住；IWanner Replay 以整个 Group 为单位并通过黑幕重建。
- DeepLink 上下文由 `OneLinkManager.IsCurrentEntry(gameType)` 判断；同玩法 Replay 会保留入口标记，返回主页或接受新的普通关卡后应结束该标记。
- 主线星星由 `Postcard_VictoryStarGroupCtrl` 展示并消费本轮等待动画；Draw 星级保存在 `DB.DrawGame.LevelCollectedStar/LevelWaitingAnimStar`；IWanner 现有结算没有对应星级存档。

### 直接结论

- 专用面板应使用独立 `GameVictoryPanel_DeepLink` 脚本，并从 prefab 变体移除继承的 `GameVictoryPanel` 组件，才能从结构上隔离 tips、share、免广告和奖励逻辑。
- 三种玩法只应在胜利面板创建处分流，后续 Replay/加载/转场仍复用各自现有控制器链路。
- IWanner 固定三星应作为纯 UI 表现写入专用面板配置，不应伪造任何主线、Draw 或 IWanner 存档数据。

### 风险与回滚条件

- Prefab Variant 的移除组件、添加组件和继承引用若绑定错误，会导致 Resources 能加载但按钮或星星为空；必须静态核对脚本 GUID、所有 fileID 引用和基础 prefab 源对象。
- Draw 若绕过现有胜利转场会提前销毁旧关卡并暴露空画面；任何 DeepLink Next 实现都必须继续走保留旧关卡的现有入口。
- DeepLink 标记若在 Replay 前被清理，重玩再次胜利会退回普通面板；若 Next/Home 后不清理，则会污染后续普通胜利。
- 任一玩法目标解析失败时不得伪造通关状态；应保留现有选关/主页降级路径并记录错误。
- 回滚时可恢复三处胜利面板工厂调用和三处 DeepLink Next 分支，删除专用脚本，并把 prefab 变体恢复为继承 `GameVictoryPanel`。

## 修改后行为

- 主线、Draw、IWanner 的胜利创建点先检查当前玩法的 DeepLink 上下文；命中时无论首通或复通都创建 `GameVictoryPanel_DeepLink`，普通入口仍保留各自原胜利／奖励面板。
- 专用脚本显式绑定 Spine、主页、重玩、下一关、星星、点击拦截、已有胜利粒子和 tips 的 TMP 引用；tips 只控制可见字符数，不执行关卡文案读取或替换。没有 share、NoAd、奖励或明信片入口字段。
- `home_btn` 调用现有 `GameManager.ReturnToStartPanel`；返回成功后销毁专用面板实例。`replay_btn` 在派发已有 `GameReplayEvent` 前直接销毁实例。Replay 不提前清理 DeepLink 标记，因此重玩再次胜利会重新创建专用面板。
- `nextBtn` 派发已有 `EnterNextLevelEvent`，由活动控制器处理：
  - 主线使用 `TryEnterLowestUnpassedOrLastLevel`；
  - Draw 在当前有效排序中选择首个有效、未通关项，再走 `keepCurrentLevelUntilCovered: true` 的既有转场；
  - IWanner 固定进入配置第一行 Group 的第 0 小关，不筛除已通关组、不恢复组内索引，并复用现有黑幕重建；本条由 2026-10-14 要求替换昨日“最早未通关 Group”的规则。
- Next 请求被控制器接受后才结束对应 DeepLink 标记；目标不存在或请求失败时结束标记并沿用现有选关降级，避免污染后续普通胜利。
- 主线继续展示并消费实际明信片星；Draw 展示永久最高星并消费、播放本轮新增星动画；IWanner 通过 `RefreshStaticStars(3)` 固定点亮三颗星，不写任何星级存档。
- `tips_txt` 仍由 prefab 上用户已有的 I2 Localize 配置负责；每次打开先隐藏字符，Spine `in` 的 `Text` 事件触发 `1s` 逐字展示，入场完成时补齐尚未启动的表现。脚本不赋值、清空或替换文本；旧分享按钮只作为 `home_btn` 绑定，分享功能不再进入专用面板。

## 数据、资源、事件与 UI 流

- 资源流：三玩法胜利创建点 → `UIManager.OpenUI<GameVictoryPanel_DeepLink>` → `Resources/GameVictoryPanel_DeepLink.prefab`。Prefab Variant 移除继承的 `GameVictoryPanel` 组件，并在根节点显式添加、绑定新脚本；Prefab GUID 与用户新增美术资源保持不变。
- 事件流：Replay/Next 分别复用 `GameReplayEvent`、`EnterNextLevelEvent`，未新增 `PlayEvents` 或全局事件。Home 复用控制器切换和主页恢复入口。
- 存档流：主线仍由原成功流程先授予明信片星；Draw 仍由 `TryRecordVictoryStars` 写入永久／等待星，专用面板仅消费等待项；IWanner 三星完全是显示状态，OneLink 胜利保留小关统计及当前位置，但不新增 Group 通关/后继解锁/最高 Group 进度，普通入口照常记录。
- 转场流：主线继续走 `EnterLevelEvent`；Draw 仍在翻页完全遮住后销毁旧 DLevel；IWanner 仍先使旧 HUD/关卡不可输入，再经 `ReloadCurrentLevelAsync` 黑幕重建。
- DeepLink 状态流：Replay 保留 → 再次胜利仍命中专用面板；Next 接受或 Home 恢复后清理 → 后续普通胜利回到原面板。Home/Replay/Next 离开专用面板时均使用 `DestroyUI`，不保留关闭实例供后续复用。

## 影响文件与系统

- 专用 UI：`Assets/Game/UI/GameVictory/Scripts/GameVictoryPanel_DeepLink.cs`、对应 `.meta`、`Resources/GameVictoryPanel_DeepLink.prefab`。
- 星级展示：`Assets/Game/UI/Postcard/Scripts/Postcard_VictoryStarGroupCtrl.cs` 新增无存档副作用的静态星级 API，以及接收最高星／待播星数的 `RefreshStars` 动画 API。
- 胜利分流与导航：`PlayCtrl_Success.cs`、`PlayCtrl.cs`、`DrawPlayCtrl.cs`、`IWannerPlayCtrl.cs`。
- 未修改 Packages、GuruSDK、GF、RemoteConfig、Luban 配置、存档结构、平台 SDK、共享 GameplayPanel 或关卡 prefab。

## 验证证据

- Unity 6000.0.59f2 的当前 Bee 响应文件加上新增脚本后，Roslyn 定向编译：Editor `0 error / 113 warning`，Android `0 error / 116 warning`；编译清单不存在缺失源文件。
- 当前 Unity Editor 已用 PrefabImporter 成功导入 GUID `273ec23ba3bd0474c8c8a26654d73fcc` 的专用 Prefab，Editor.log 未出现该资源的 YAML 或 PrefabImporter 错误。
- Prefab 静态检查：19 个 YAML anchor 无重复；11 个新增本地引用均恰好有一个 anchor 和一个字段引用；新脚本 GUID 与 `.meta` 一致；旧 `GameVictoryPanel` 组件被移除且不存在残留属性覆盖；所有继承来源 fileID 均可在基础 Prefab 找到。
- 星星资源核对：继承的 `Postcard_VictoryStarGroup` 明确绑定 3 个 Toggle，IWanner 的 `RefreshStaticStars(3)` 可完整点亮三颗。
- `git diff --check` 对本次代码、Prefab 和功能记录通过，仅有仓库换行格式提示。
- 常规 `dotnet build Assembly-CSharp.csproj` 因当前工程 `Temp/bin/Debug` 中约 200 个 Unity/插件引用缺失而不可用；该失败不能用于判断本次代码，故使用 Unity Bee 的实际引用清单完成上述定向编译。

## 剩余风险与未验证缺口

- `uncertain`：尚未运行 Unity PlayMode 实测三玩法胜利分流、按钮点击、星星动画、I2 tips 显示和快速重复操作。
- `uncertain`：尚未在 Android/iOS 横屏真机验证触控、SafeArea、粒子、原生插屏返回后结算显示及真实 DeepLink 全链路。
- 当前 Unity Editor 日志还保留任务外 `IWannarGame_GameGuidePopup.Generated.cs` 重复字段及已移动 Guide 源文件缺失的编译错误；本次定向编译不包含这些失效/重复来源，因此不能据此宣称整个打开中的 Unity 工程已完成一次无错误脚本重载。
- iOS 定向编译仍被工程现有 `ChoiceLevelPanel.cs`、`StartPanel.cs` 缺少 `Guru.UniKit.Apple` 引用阻断；本次未修改只读 SDK 或平台依赖，因此不能声明 iOS 编译通过。
- 当前验证证明脚本可按 Editor/Android 宏编译且 Prefab 可导入，不等同于玩家态运行和平台端到端验收。

## 回滚路径

- 恢复 `PlayCtrl_Success`、`DrawPlayCtrl`、`IWannerPlayCtrl` 的原胜利面板创建分支，以及三玩法 DeepLink Next 目标分支。
- 删除 `GameVictoryPanel_DeepLink.cs` 及 `.meta`，从 Prefab Variant 移除新组件并恢复继承的 `GameVictoryPanel` 组件。
- 删除 `Postcard_VictoryStarGroupCtrl.RefreshStaticStars`；该 API 未改变原 `Refresh(LevelData)` 行为，也没有存档迁移需要回滚。
- 玩家已经形成的通关、星级或 DeepLink 历史仍由原流程保存，代码回滚不会撤销已有玩家数据。

## 2026-10-09 补充：关闭后销毁专用面板

### 原行为与需求

- Replay、Next 已在事件派发前调用 `UIManager.DestroyUI(this)`；Home 只调用 `GameManager.ReturnToStartPanel`，其内部 `CloseAllUI(UILayer.Popup)` 仅执行 `OnClose`，实例仍保留在 `UIManager` 缓存中。
- 本次要求专用面板离开时统一销毁，后续再次胜利由 `OpenUI<GameVictoryPanel_DeepLink>` 创建新实例，不依赖关闭实例的显示状态复原。

### 修改后流程

- Home 返回主页成功后显式 `DestroyUI(this)`；若返回流程已关闭 Popup、但后续控制器或 StartPanel 恢复失败，也销毁已经关闭的专用面板，避免残留缓存实例。
- 如果 Home 在关闭 Popup 前即失败，面板仍保持打开并恢复按钮，玩家可以重试；这保留了原失败恢复能力。
- Replay、Next 保持原先的先销毁、再派发事件顺序。Bear.UI 的 `DestroyUI` 会调用 `OnDestroyView`，而 `BaseUIView.OnDestroyView` 会继续调用本面板 `OnClose`，因此 Tween 与 Spine 回调清理仍会执行。
- 此生命周期调整不改变星星播放策略：主线仍只动画消费到的待播放星；Draw、IWanner 的 `RefreshStaticStars` 仍为静态显示。销毁并重新创建实例本身不会让静态星星自动播放获得动画。

### 验证、风险与回滚

- 静态核对 Home/Replay/Next 三条按钮路径均可到达 `DestroyUI(this)`，且 Home 仅在仍打开的失败场景保留实例。
- 当前 Unity Bee Editor/Android-Editor 响应文件定向 Roslyn 编译通过：`0 error / 113 warning`；本次脚本没有新增定向警告。
- `uncertain`：尚未在 PlayMode 验证 Home 成功、Home 早期失败、Replay、Next 后 `UIManager.GetUI<GameVictoryPanel_DeepLink>()` 均不残留旧实例，也未验证连续重建的资源释放时序。
- 回滚只移除 Home 成功分支和“已关闭失败”分支中的 `DestroyUI(this)`；Replay、Next 的既有销毁行为不回滚，数据、事件、资源和存档结构无需迁移。

## 2026-10-09 补充：tips 配合 Spine 展开（修改前核对）

### 需求、基线与来源

- 用户实测 IWanner DeepLink 胜利时没有 tips 动画，本轮明确要求 tips 显示配合 VictoryPanel Spine 的展开。保留此前“不赋值或替换 `tips_txt` 文案”的要求，本次只改变文字可见字符数，不重写文本或本地化键。
- 当前入口为 `IWannerPlayCtrl.HandleGroupCompletionAsync` → `GameVictoryPanel_DeepLink.CreateForIWanner`；主线与 Draw 的 DeepLink 也创建同一个专用 Prefab，因此本次显示时序会用于三个 DeepLink 入口，普通胜利面板不受影响。
- 当前专用脚本 `OnOpen` 只播放粒子与 Spine，未绑定 tips；Variant 的 tips 保留 TMP 和 `I2.Localize`。基础 Prefab 的 tips 容器已有 `BoneFollowerGraphic`，跟随 `Text_HollowSkeleton`，不需要增加位置、缩放或骨骼控制。
- 实际绑定链为 `GameVictoryPanel_DeepLink.prefab` → 基础 `GameVictoryPanel.prefab` → `UI_Victory_SkeletonData.asset` → `ui_victory_001/UI_Victory.json`。`in` 中已有 `Text` 事件，时间为 `0.9s`，无需新增 Spine 事件或修改美术；原主线 `RefreshTips` 的逐字时长为 `1s`。
- 项目内 `GameTipsPopup.RefreshTips` 已使用 `TMP.maxVisibleCharacters` 与 DOTween 实现逐字展示，本次复用该显示方式而不复用其取文案、云控或按钮流程。CodeGraph CLI 当前不可用，引用的 037 plan 缺失，依据以上本地源码、Prefab 和动画数据进行核对。

### 拟修改位置、边界与验证场景

- 仅在专用脚本新增带中文说明的 TMP 序列化引用，并在专用 Variant 显式绑定已有 `tips_txt`；不修改基础 Prefab、生成绑定、Spine 资源或共享星星组件。
- 每次 `OnOpen` 把可见字符数设为 0；收到当前 `in` 的 `Text` 事件后，以 `1s` 线性逐字显示现有文字。若入场完成时还未开始，则使用现有完成回调补齐；每次打开只启动一次。
- 关闭时终止文字 Tween、退订 Spine 事件并恢复完整可见字符数；再次打开重新开始。保留原按钮交互、Home/Replay/Next、星星静态展示、粒子、存档与 DeepLink 来源生命周期。
- 验证覆盖资源事件时机、TMP/Variant fileID 显式引用、正常打开、重复事件、入场完成补齐、关闭清理、重开和文案不改写；执行真实 Unity 引用定向编译和差异检查。Unity PlayMode 与 Android/iOS 横屏视觉效果、本地化切换中途的表现仍为 `uncertain`。

### 修改结果、风险与回滚

- `GameVictoryPanel_DeepLink.cs` 新增 `tipsText` 与文字 Tween，`ResetOpenState` 设置 `maxVisibleCharacters = 0`；当前 `in` 的 `Text` 事件触发动画，`CompleteEntrance` 补齐未启动分支，单次打开标记避免重复启动。
- 逐字过程使用现有 TMP 的字符数和 `1s` 线性 unscaled DOTween，结束后恢复完整可见字符数；没有对 `.text` 赋值、重新取本地化文案或增加字体/配置修改。
- `OnClose` 终止文字 Tween，并与重置入口共同退订 Spine 事件；关闭恢复完整字符可见度，下一次打开重新置 0。导航按钮仍只等待既有入场和星星表现，不新增 tips 完成门禁。
- 专用 Variant 新增 TMP stripped 引用，显式绑定到基础 Prefab 中原 `tips_txt` 组件；没有新增节点或改动基础 Prefab、骨骼跟随、I2 Term、Spine 资源与共享星级逻辑。原类注释“不接入 tips”因本轮新增显示职责不再符合实现，已仅调整该注释。
- Unity 6000.0.59f2 当前 Bee Editor 响应文件定向 Roslyn 编译结果：`exit code 0 / 0 error / 113 warning`。该结果证明当前脚本和引用可编译，不代表 Unity 玩家态展示或真机验收。
- `uncertain`：尚未在 PlayMode、Android/iOS 横屏验证真实展开/逐字节奏、关闭途中销毁、Replay 后再次胜利及文字动画中途切换语言。验收时应看到展开前无 tips 字符，约 `0.9s` 的 `Text` 事件后开始显示，并在约 `1s` 后显示完整原文。
- 回滚仅撤销专用脚本本轮 tips 字段、可见字符 Tween 和 Spine 事件接入，并移除 Variant 的 `tipsText` 字段与新增 TMP stripped 引用；文案、本地化、存档、星级和 DeepLink 进关历史无需回滚。

## 2026-10-09 补充：Draw DeepLink 新增星动画（修改前核对）

### 需求、基线与来源

- 用户指出 DrawGame 星星动画也未接入。本次验收对象为 Draw 通过 DeepLink 打开的专用胜利面板：已有最高星静态显示，本轮新增星逐颗播放；没有新增星时不伪造等待数据或重播获得动画。此前 tips 配合 Spine 展开、不改文案以及 IWanner 固定静态三星保留。
- `DrawPlayCtrl.ShowVictoryPanel` 先按本次 `InkState.StarCount` 调用 `TryRecordVictoryStars`，再选择 DeepLink／普通／Reward 面板。`TryRecordVictoryStars` 只按历史最高值的正增量累加 `DB.DrawGame.LevelWaitingAnimStar`，保存失败会回滚该增量。本轮不修改控制器、星级计算、进度、Replay、转场或打点。
- 普通 Draw 的 `CacheAndConsumeVictoryStars → RefreshVictoryStarGroup → DrawGame_VictoryStarGroup.Refresh` 已缓存最高星和新增星并消费等待项；DeepLink 的 `ConfigureDrawStars → ConsumeDrawWaitingStars` 只返回最高星，删除等待项后调用 `RefreshStaticStars`，因此丢失待播放数量。这是两条代码路径的实际差异，不是普通 Draw 的动画未接入。
- 专用 Variant 已显式绑定基础 Prefab 的 `Postcard_VictoryStarGroupCtrl`，且该星组已绑定三个 Toggle 与既有 Animator。基础 Prefab 覆写 `waitingPlayDelay=1.2`、`waitingAnimInterval=0.5`；源星组 `scaleDuration=0.5`，故首颗触发约在刷新后 `1.7s`。这些资源本轮保持不变。

### 拟修改位置、边界与验证场景

- 专用面板消费等待数据时同时缓存新增星数量并限制到最高星数，仍仅删除当前关卡等待项并沿用保存失败日志；不修改永久最高星，也不读写 Postcard 或 Bag。
- 在已有星组新增纯数值 `RefreshStars` 展示入口，复用其 `ApplyStarState`、错峰 Animator／音效、呼吸收尾、版本隔离及禁用／销毁清理。主线 `Refresh(LevelData)` 和静态星 API 原实现不改，不新增层级查找、全局事件、Prefab 或资源。新 API 仅有本次 Draw DeepLink 调用方。
- `ConfigureDrawStars` 使用最高星与新增星调用新入口；等待及播放期间沿用专用面板 ClickBlock 和导航门禁，结束或无动画时释放。关闭／销毁仍通过星组既有生命周期停止动画，重玩后是否播放取决于新一轮是否提升最高星，而非强制重复获得动画。
- 核对首次 0→1/2/3 星、最高星提升 1→3、同星／降星复通、等待数据为负或超上限、缺配置／DB／组件、消费保存失败、关闭等待／播放中的面板、主线与 IWanner 旧入口不变；执行 Unity Bee 定向编译、资源绑定及差异检查。PlayMode／Android/iOS 横屏动画与真实存档落盘仍为 `uncertain`。

### 修改结果、主线复核与验证

- 专用面板在删除当前 Draw 等待项前，读取并缓存夹取后的待播数量；随后调用 `RefreshStars(最高星, 待播星, 完成回调)`，等待／播放期间启用原 ClickBlock 和导航门禁，无动画或完成时释放。无星组仍消费一次并直接放行，不额外创建组件。
- 新星组 API 不读 DB／Postcard／Bag，仅把数值转换为当前 UI 的有效星序号后复用 `ApplyStarState`；原 `Refresh(LevelData)`、静态 API、异步等待／错峰／Animator／音效／完成／清理方法未改。仅新增一个 API 及 Draw DeepLink 调用，不修改普通 Draw／Reward 面板。
- 用户追加要求核对主线 DeepLink。事实链为 `PlayCtrl_Success.ShowVictory` 在首通时先 `TryToGetPostcardVictoryStar → PostcardManager.TryGrantVictoryStars` 写永久星与待播星，再创建专用面板；`Create(LevelData) → ConfigureMainStars → victoryStarGroup.Refresh(LevelData, CompleteStarPresentation)` 与普通主线使用同一个星组入口。主线不存在本次 Draw 的“只静态显示并丢弃待播数量”差异，故未额外修改主线流程。
- 主线无动画的边界保留：没有待播新星、`LevelSumStarCount=0`、发星／保存失败、缺 PostcardManager 或星组引用时，不能据此认定漏接；待播星夹取到当前配置可见数量。主线重复通关不会再次发首通默认星，但关内新增收集仍可形成待播数量。
- 内存隔离执行当前源码自动提取的 `ConfigureMainStars`、`ConfigureDrawStars`、`ConsumeDrawWaitingStars`、两种星组刷新、`ApplyStarState` 与数量校验方法，共 **578 项断言通过**。覆盖 0～3 及负数／超范围最高星与待播星、已有星静态／新增星调度、主线非法／重复星 ID 与配置数量、一次消费、其它关等待记录保留、无动画门禁、缺 DB／配置／组件、消费保存失败、编辑态 guard。Unity、存储、动画调度与完成驱动为替身，只证明方法的数据与状态分支，不证明真实帧时序、动画触发或落盘。
- Unity 6000.0.59f2 当前 Bee Editor 响应文件定向 Roslyn 编译：`exit code 0 / 0 error / 113 warning`。自动比较 HEAD，共享星组原主线刷新、静态刷新、动画调度／完成／清理等 9 个方法源码保持原样；Variant 20 个 anchor 无重复，星组与 TMP 的显式源引用有效。7 个目标文件 `git diff --check` 通过，仅换行格式提示。
- `uncertain`：PlayMode／Android/iOS 横屏未执行；实际星星起播／收尾、等待或播放中关闭销毁、暂停／原生覆盖后 Animator 与 Tween 时钟差异、真实保存失败及重启仍需实测。普通主线／Reward 和 IWanner 原代码未改，不以此宣称所有玩家态场景均已通过。
- 回滚只撤销本轮专用面板的 Draw 待播数量缓存、动画调用和共享星组 `RefreshStars` API；保留 tips 接入、IWanner 静态三星、普通主线与 Draw 原流程及玩家已有永久星。等待项仍沿用创建面板时消费，动画中途关闭不恢复已消费项，这与既有主线／Draw 语义一致。


## 2026-10-12 当前有效扩展：Dig DeepLink 专属胜利

### 需求、原行为与实现

- 用户要求DeepLink进入DigLevel后采用deeplink专属胜利界面。本节将上方三玩法专属胜利的适用范围扩展至Dig；既有三玩法工厂、事件、按钮、Spine、tips、不接分享/NoAds/奖励的职责继续按原实现。事前核对见当日Dig DeepLink专题。
- 原Dig胜利仅按首胜/复胜创建DigGame_GameVictoryPanel_Reward或普通面板；现在在创建点先核验OneLinkManager.IsCurrentEntry(Dig)，命中均走GameVictoryPanel_DeepLink.CreateForDig。未确认加载、其它玩法来源或普通入口仍按原分支。
- 控制器继续按真实DigLevelData.Id计算最高星=max(历史,本次)，新增星=max(0,本次-历史)。新增工厂只接受这两个数值、复用现有Postcard星组RefreshStars与点击拦截/完成回调，不读取/消费Main/Draw/IW存档，也不新增等待星持久字段；通关/最高星/解锁仍在创建面板之后由RecordVictory保存。
- 专属Replay复用现有GameReplayEvent和完整重建，不调用OnLevelEntryStarted、不清DeepLink；再胜仍用专属面板，同关提示/广告墨水资格保留。Home复用ReturnToStartPanel→Dig.Deactivate→EndEntry，成功后销毁面板；已有Home失败恢复不变。
- 专属Next复用EnterNextLevelEvent，由Dig用已有ResolveLowestUnpassed查询当前有效序列第一个未通过项；普通全通时TBC在末尾。接受目标后复用原统计、解锁、翻页遮住旧关再重建的转场，结束Dig来源；目标丢失/失败回选关并结束来源。普通Next仍按当前实际DataId在新序列定位相邻项。

### 影响、验证与风险

- 代码仅DigPlayCtrl.cs（持有BaseUIView、胜利分流、DeepLink Next）及GameVictoryPanel_DeepLink.cs（新增Dig纯表现工厂）。Prefab、Generated绑定、共享星组、OneLinkManager、存档/配置/SDK/广告与既有三玩法工厂均未改。原注释保留；原普通Next内容身份说明仍放在对应分支。
- 本轮22项实际方法隔离托管断言通过：普通/DeepLink首胜与复胜、最高星与新增星、星动画门禁与导航去重、Dig存档隔离与创建后进度、Replay来源/资格、未确认/其它模式、最低未通关/TBC/重排/丢目标、空星组/工厂/管理器失败。UI/SDK/资源/统计外围为替身，不能视为生产触控或真实动画验收。
- 14个生命周期/原三玩法方法相对修改前逐方法保持，包括OnResolved、OnReset、OnReplay、重建、Deactivate、进关与既有星工厂/按钮。当前1214源码Editor/Android宏定向编译0 error、113/118 warning；iOS仍是ChoiceLevelPanel.cs:17及StartPanel.cs:14缺Guru.UniKit.Apple两处既有错误。不是主工程导入、实际打包或真机通过。
- `uncertain`：生产Prefab的Spine/星动画、关闭销毁/连续重建、Home早期失败、TBC显示、实际深链首次安装/再次唤起、插屏回调及双端横屏触控。最短验收：深链进入001→首胜→Replay复胜→Next或Home→普通进关再次胜利，核对界面与来源；重复/晚广告回调按原版本门禁检查。002～030的29个资源仍缺失。
- 证据：DevelopMD/验证记录/DigDeepLink胜利_2026-10-12/中的managed-tests.log、static-check.json、三平台宏编译日志及实际方法抽取fixture。

### 回滚

仅撤销Dig分流/BaseUIView持有、专属Next来源/目标分支与新增CreateForDig；恢复普通双面板/相邻Next，不删除通关/星级或改写原三玩法。按本轮提交差异回滚，保留地图修复及其它未提交内容。Excel对照原“深链专属胜利未接入”项随本轮改为已接入，同时保留真实平台验证缺口。
