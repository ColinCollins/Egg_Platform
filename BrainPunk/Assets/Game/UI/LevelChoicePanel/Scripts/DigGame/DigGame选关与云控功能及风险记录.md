# DigGame 选关、云控与打点功能及风险记录

## 2026-10-14 选关面板统一命名：修改前核对

- 用户要求统一为 `DigGame_ChoiceLevelPanel`。修改前工作区已将 Prefab 与 meta 改为新名，Prefab 根名称已更新且 GUID `f9e04b124b604970a74644c0b2d34c0b` 保持；脚本两份 partial、Item owner、DigPlayCtrl 与 GameManager 仍使用旧类名。Create 以 `typeof(...).Name` 作为 OpenUI 资源键，因此必须同步类型与文件名。
- 维护边界：手写面板负责玩法，Generated 的唯一绑定为 LevelselectionTxt；Prefab 是字段绑定输入，游戏侧 UIGeneratorUIViewScriptGenerator 及 Bear BaseUIViewEditor 按类型名生成 partial。只同步类名、文件名、日志标签及调用引用，保留全部序列化字段、fileID 与三个 meta GUID；不改生成器、UI 结构、分页、广告、存档、云控或结算行为。
- 验证计划：比对改名前后源码除名称替换外一致，Prefab 除根名称外一致，meta 保持；核对 Create 资源键、Prefab 脚本 GUID 与引用字段；生产目录旧名检索及 Editor／Android 条件编译。未进行真机选关点击验收，结果标记 `uncertain`；历史验证日志保留原时间点名称。CodeGraph CLI／MCP 不可用，037 plan 缺失，本次以当前源码、生成器与 Prefab 为依据。
- 回滚：类型、两份脚本及 meta、Prefab 与 meta 和调用方名称必须一起恢复；不整文件覆盖其他工作区修改。

### 完成与验证

- 当前统一名称为 `DigGame_ChoiceLevelPanel`，两份 partial 脚本及 meta 同步改名；Item owner／Init 参数、DigPlayCtrl 创建入口、GameManager 查询类型、Create 的类型资源键与日志标签已同步。工作区原有新名称 Prefab 及其 meta 直接沿用。
- 9 份源码／Prefab／meta 与 HEAD 基线对比，除名称替换外逐字一致（仅忽略换行差异）；三个 GUID、所有序列化字段及 fileID 保持。Assets/Game 下 C#、Prefab 与模块文档旧名检索为 0，历史验证快照保留原名称。
- 当前主程序集 1223 份源码 Editor／Android 条件编译均 0 错误，分别有 113／118 条警告；定向 diff 检查通过。证据位于 `DevelopMD/验证记录/Dig选关命名_2026-10-14/`，没有执行实际运行态选关点击或 APK／IL2CPP 构建，这些范围仍为 `uncertain`。本次仅命名修正，不重跑分页／广告行为测试。

## 2026-10-13 当前分页规则与验证

Dig已按今天Draw提交1655d0453调整：打开时优先当前有效关卡页，当前ID不在排序或真实配置缺失时夹取已有保存页。按钮翻页、QuickTurn拖动、关闭及远端刷新不再写页码；通过现有选择校验的普通／广告／OneLink进关仍在TryEnterSelectedLevel记录CurrentLevel与ChoiceLevelPanelPageIndex并执行原保存／请求链。首次创建不把失效CurrentLevel强改第一关，集合补齐及首关／TBC解锁保留。

首页和关内返回均采用普通打开定位，取消最高可玩页和退出ID二次覆盖；退出仍沿原ReturnToStartPanel及restoreChoice条件，开关、广告版本门禁、进关、胜利推进、统计／资源／HUD清理不改。仅修改手写面板、GameManager和DigPlayCtrl三个文件；原“首页定位最高已解锁关”注释随行为改变定向更新，其他旧注释保留。

实际源码隔离50项：修改前15项不符合新规则，修改后全部通过；17个相关方法按玩法类型替换后与当前Draw一致。序列化声明与UI Prefab／Generated／DB／主线／Draw无本轮差异，唯一页码赋值保留在进关入口。Editor／Android全源码条件编译0错误。首轮Editor因并行Terrain CompressionLevel歧义阻断，后续并行代码修正后重跑通过，本选关任务未改地形。逐场景及日志见DevelopMD/验证记录/Dig选关分页_2026-10-13/验证记录.md。

验证使用Unity显示／DB落盘／SDK／控制器切换桩，正式UI、旧档真实重启、平台广告、后台和双端真机仍uncertain；iOS编译未执行。旧浏览页历史不迁移，保存失败沿原警告及内存／磁盘边界。没有新DB字段、事件、扫描或运行时实例；本轮未自动提交Git。只按三个文件分页diff局部回滚，保留其它并行修改。

## 2026-10-13 配合Draw选关分页修改：修改前核对

- 用户要求Dig选关配合今天Draw修改。依据为提交1655d0453及Draw选关文档2026-10-13当前有效约定；本节替代下文首页最高可玩页、退出指定页和浏览／关闭／远端刷新保存页码的历史规则。验收是玩家打开面板的定位及页码保存时机，范围不扩展为全部广告／SDK表现一致。
- 基线／来源：DigGame_ChoiceLevelPanel.OnClose／RefreshRemoteConfig／ChangePage／OnQuickTurnPageChanged调用SavePageIndex；TryInitializeData把失效CurrentLevel改第一关；RefreshInitialPageIndex未检查真实配置。DigGameEnter.Enter→GameManager.TryEnterDig→DigPlayCtrl.FocusHighestPlayableLevel覆写首页页码，ExitToLevelSelection再按退出ID定位。主线GetInitialPageIndex／EnterLevel及当前Draw完整对应链已核对，知识库无匹配功能规则，CodeGraph CLI／MCP不可调用，037 plan仍缺失。
- 修改位置／边界：仅手写Dig面板、GameManager.TryEnterDig与DigPlayCtrl分页定位相关方法；去掉非进关页码写入和非法当前ID回写，补真实关卡配置检查，删除无人继续调用的旧定位API。首页和关内返回均普通打开；保留Dig独立存档、集合修复／首关／TBC解锁、有效选择版本检查、进关及广告门禁。退出仍先ReturnToStartPanel再按原restoreChoice条件打开Dig，打点、资源／HUD清理、转场与开关不变。
- 生成／资源核对：DigGame.cs是PartialClassGenerator输入，现有CurrentLevel／ChoiceLevelPanelPageIndex由DigGame_Partial.cs生成；UI Generated由UIGenerator／BaseUIViewEditor扫描Prefab维护。只复用已有字段及手写入口，不修改DB/schema／生成器／生成文件／Prefab／配置。面板Prefab1449～1456的内容、item、按钮及QuickTurn显式绑定保留；不改Draw、主线、只读Packages／SDK／GF。
- 验证：复用今天Draw的实际源码隔离夹具，检查当前有效／失效／缺配置、保存页边界、首次创建、翻页／拖动／关闭重开、正常／OneLink进关、失败保存、广告等待／结束、排序变化、首页和关内／加载退出。先记录修改前不符新规则的结果，再执行修改后；快速Editor／Android编译及定向引用／序列化／方法diff，不启动Unity整关。该方法隔离对Unity对象／DB落盘／SDK为桩，正式UI、旧档真实重启及双端真机仍uncertain。
- 风险／回滚：旧档浏览页历史不迁移，不能定位时沿已有页夹取；取消非法CurrentLevel回写不影响TryEnterSelectedLevel、ResolveCurrentLevel及正式进关／胜利写有效ID的现有职责。保存失败仍警告并沿原受理链，不能保证磁盘成功。只按本次三个文件diff回滚分页改动，保留其它并行内容；事前快照TEMP/brainpunk-dig-pagination-20261013/before。

日期：2026-10-12。本节为第二轮当前有效设计；上轮“仅本地排序、没有正式统计”的说明为历史基线。

## 需求与修改前依据

用户要求 Dig LevelData/LevelSort 补到 30 关，数据结构沿用 Draw；复制 Draw 选关面板、item 和星组，内容/存档独立；解锁、广告解锁、TBC、RemoteConfig 和打点对齐 Draw。用户已确认墨水/提示沿用 Draw 规则，已确认独立 diggame_config 接入游戏侧配置读取/缓存/延迟提交链，已确认全局成功次数从三玩法改为四玩法。实施前已更新《DevelopMD/关卡打点生命周期约定.md》顶端当前有效扩展；ForceFetch 次数和两个等待预算没有改动。

修改前 Dig Excel 只有一个数据行和 7 个字段，LevelSort 源工作簿缺少 digLevelSort 页，生成 JSON 仅一行；首页直接进关，控制器本地合成排序号 999 的 TBC，没有正式关卡统计。参考证据为 DrawGameLevelCtrl、DrawPlayCtrl、DrawGame_ChoiceLevelPanel/LevelChoiceItem/ChoiceLevelStarGroupCtrl、Draw 的三个对应 Prefab、ConfigManager.RemoteConfig、RemoteConfigService、LubanGenerator、实际 Excel/生成器和打点权威文档。修改前核对保存在当日使用记录。

## 文件与维护边界

- `Luban/Configs/Datas/LevelData.xlsx` 的 dig_level 增加 40001～40030 和 40999，列扩展到 maxInkLength/inkStarStage1～3。普通行类型 Dig=3、锁类型 Ad=1、容量20、阈值0/4/10；首行原备注/版本保留。40999 路径 DigLevel_999、顺序锁=2，不消费墨水。
- `LevelSort.xlsx` 新增 digLevelSort 页，30 行顺序关联真实数据 ID。TBC 与 Draw 相同，由控制器追加为最后一个排序项，本地排序号31；远端普通关数量变化时重新计算末尾关号。
- `Const/Global.xlsx` 新增14个 digGame 专用字段；三份工作簿非目标 ZIP 内容/原样式保留。数据由 artifact-tool 作者化，再合入原工作簿，避免导出库丢失原字体 charset 等元数据。
- 实际 Luban 生成同步仅包含 DigLevelData.cs、GlobalConst.cs 和 Dig 两份/Global 一份 JSON；不覆盖其它生成差异。Dig DB 手写字段作为 PartialClassGenerator 输入，Partial 只增加相应属性和 Init 默认值，不放业务逻辑。
- 新脚本位于本目录，当前类名为 `DigGame_ChoiceLevelPanel`；item 和星组均使用 Dig 独立类型。Generated 副本与实际 Prefab 字段/fileID 对齐；生成绑定权威输入仍是 Prefab。
- 资源位于 `UI/DigGame/ChoiceLevelPopup/{Resources,Prefabs}` 和 `UI/DigGame/Prefabs`。三个 Prefab 和脚本 GUID 独立，面板→item→star 的嵌套引用全量替换。共用的按钮、美术、布局、快速分页与纯表现组件沿用原组件，不读 Draw 进度。
- DigPlayCtrl.Analytics.cs、GameSDKService.Dig.cs、DigInterstitialAdPolicy.cs 是独立实现。只读 Packages/GuruSDK/GF 未改写；共享 GameManager、首页、配置服务、广告 helper/每日模块仅增加 Dig 分支。

## 修改后行为及数据流

首页 Dig → 游戏侧开关/配置门禁 → 激活 DigPlayCtrl → 打开独立 Popup，保留首页 Normal 层并定位最高已解锁页。关闭选关复用仍可见首页；关内 Menu/加载失败先完整回首页，再恢复 Dig 选关页。分页、TBC 解锁和普通/广告锁表现直接复制 Draw 结构。首关默认解锁；普通胜利顺序解锁下一关；TBC 根据末尾普通关的 PassedLevels 解锁，隐藏星组，未解锁点击提示 TBC。

选关广告捕获 owner、排序、真实数据和配置版本；等待期间禁止分页/关闭和云控提交。成功有效回调仅写 Dig 解锁并进关，失败/取消不解锁，finally 释放门禁。普通/OneLink 进关共用面板的保存和版本校验链。已获得提示/奖励墨水资格在同关 Reset/Replay 保留，换关/退出清除；提示金币价格只读 Dig getter。等待提示与失败提示阈值在新尝试开放时读取，已有提示不再提醒。

Luban 默认 → LubanGenerator 的 Dig 白名单/排序 → guru_spec.yaml 的 diggame_config → Guru 现行模板转义输出 → 启动现有拉取/缓存 → DigGameLevelCtrl.Init。运行期有效回调在无广告/暂停/转场时一次保存缓存，再同步提交排序/刷新已有 UI；无效 ID、重复 ID、空数组、错误 enabled 类型拒绝整份排序候选。enabled=false 回退本地；字段缺失/类型解析失败使用本地 Global；同序列只更新参数，不变更排序版本。保留 ForceFetchMaxAttempts=1、自动等待5秒、后台强制等待10秒及 Loading 原有初始化顺序。

正在运行的 Dig 固定进入时数据对象、Path 与统计 ID，云控不重建现场。下一关按原内容在当前列表定位；原内容已移出列表则返回选关。进度中的排序 ID 延续 Draw 口径，远端重排可能改变其对应内容；最高星/统计使用真实数据 ID 保持内容身份。为避免重排时顺序解锁错误内容，胜利只有排序仍指向原数据时才自动解锁其下一项。

Dig 插屏策略/冷却/失败和胜利计数/每日免广告弹窗均独立。失败/手动 Reset 先报告 fail 并建立新尝试，全黑重建新实例后请求失败插屏，回调后才揭幕和恢复输入；TBC 手动 Reset 同样沿该广告链，TBC 不弹自动免广告提示。胜利先锁物理/HUD，再报告成功并等插屏回调开放胜利 UI。统一 RewardAdHelper 捕获原策略，激励成功不会改变其它玩法 CD。

Dig 统计：新关 start；Reset 增 revive→fail→清本次时长并保留 revive→新 start；成功增加真实 ID 成功次数→success/派生事件→清本次时长/revive→胜利 UI；Replay 清时长/revive且不加 revive→start；退出活动尝试 exit 并清现场。Main/Draw/IWanner/Dig 成功次数共同用于全局 level、b_play 和 N≤50 的派生事件，派生参数使用实际 ID40001等。TBC 有 start/Reset/exit，没有普通成功、最高星或首胜奖励；主线传送门受理成功才报 enter_tbc_portal(type=3)。虚拟货币沿现行“最近一次 start Path”上下文，因此纳入 Dig。

## 验证与一致性边界

可复查材料：`DevelopMD/验证记录/DigGame选关云控_2026-10-12/验证记录.md`。

| 场景 | 当前证据 | 仍需验收 |
|---|---|---|
| 配置30普通+TBC、字段同构、绑定与隔离 | Luban 重生成相等、Excel原单元格/样式检查、三个Prefab/全部脚本副本结构比较 | 生产场景布局/输入 |
| 正常已解锁、分页/关闭重开、星组 | Draw副本源码和Prefab/fileID结构 | Unity完整UI点击/动画/缓存刷新 |
| 广告成功/失败/取消、已报告无网/服务不可用 | 复用同一RewardAdHelper与版本校验/finally源码 | SDK/设备回调和提示 |
| 广告关闭但无结果、晚回调 | 无结果保持等待门禁；旧owner/版本不写新配置，源码边界 | 真实SDK是否保证回调，关闭重开、原生覆盖暂停 |
| 云控重排/失效ID/落盘失败/延迟/撤销候选 | 51项隔离托管断言，实际提交和保存方法，存储/SDK替身 | Firebase网络恢复与真实DB落盘 |
| Reset/成功/Replay/退出与四玩法累计 | 实际Analytics partial、SDK Dig方法和累计方法；隔离事件序列断言 | 后台实际收数、生产UI生命周期 |
| 普通关配置注入墨水 | 3项原生Unity边界断言 | 生产首关地图/复杂触控及帧率 |

游戏程序集 Editor/Android 条件编译0错误，编辑器配置生成器0错误。iOS 条件编译有两处既有 Guru.UniKit.Apple 引用缺失（主线 ChoiceLevelPanel:17、StartPanel:14），因此 iOS 完整编译为 uncertain。完整旧核心隔离回归尝试因测试夹具旧地形 API 不匹配，适配后末段又发生原生 D3D 崩溃，未记为通过；本轮原生结论仅限独立配置边界3项。

## 风险、未验证项与回滚

- 普通第2～30关只有配置，29个 Prefab 尚不存在；请求这些资源会日志报错并返回选关，不能称为30个可玩关卡。没有复制首关冒充新玩法。
- 生产 UI、真实保存/旧档往返、真实 Firebase/广告成功失败取消/无回调、Android/iOS横屏恢复和后台收数均 uncertain。无结果广告回调会维持输入/配置提交门禁，需设备验收，不能假设无缩放时间等待会在原生覆盖期间推进。
- 保存失败不切换列表；延迟候选恢复提交的失败重试语义沿用 Draw，未增加主动重试或改变原启动拉取。
- 回滚按第二轮修改前快照局部撤销 Dig 表/生成输出、UI副本、Dig统计及接入分支；保留第一轮基础能力与同步地图/首页/字体/美术改动。四玩法累计回退必须同步恢复打点权威文档和所有累计消费者，不能仅删 Dig 事件 API。不要删除玩家已有成功/星级字段来回滚代码。

## 2026-10-16 选关页面与 Draw 的只读复核

- 范围：用户撤回广告加墨疑问，要求先看选关页面。本轮只读核对运行代码／Prefab，并记录结论，没有修改运行脚本、资源或存档，没有重新声称整体玩法对齐。CodeGraph CLI／MCP 不可用，依据当前源码；知识库仅命中历史提交摘要，无本功能额外规则。
- 面板、Item、星组及两份 Generated 共 5 份源码，在去掉注释、空行、玩法名称与 TBC ID 差异后逐行一致。对应每页 10 项、页码定位／保存、按钮与快速拖动、锁定／广告锁／当前／通关状态优先级、广告快照／finally 门禁、最高星读取和 TBC 隐藏。Prefab 显式绑定各自 Item 与星组，状态节点、点击区和广告图引用存在；Dig 已换南瓜美术，不能称视觉资源相同。两个 Prefab 的静态 active 状态不同，但 SetData 会经 RefreshVisualState 重设全部状态根，不据此判定运行错误。
- 已确认差异一（影响截图）：DigGameLevelCtrl.RecordVictory:305–306 对每次胜利写 MaxLevel=max(原值,id)，DrawPlayCtrl.RecordVictoryProgress:1344–1350 在首胜写 MaxLevel=max(原值,id+1)。两选关面板 IsCurrent 都比较 MaxLevel，Item.ResolveState:145 又优先于 Passed，因此首次通关 10 后 Dig 仍将 10 显示为当前，Draw 推进到 11。Dig 当前状态使用紫色底／帽子，Passed 为橙色；三星独立按真实关卡 ID 读取。截图中 10 三星仍当前的表现与这条差异吻合，未读取现场 DB，不能据截图证明存档的其它值。
- 已确认差异二（配置变化边界）：Dig RecordVictory:309–314 要求排序 Id 与进入时 data.Id 同时匹配才解锁后继；Draw TryGetNextLevel:1687–1705 按排序 Id 定位并检查下一关数据存在。运行中排序重排可能带来不同后继解锁与回选关定位结果；Dig 此限制已在既有文档明确记录，不能为形式一致静默移除保护。
- 已确认差异三（异常边界）：DigPlayCtrl.ShowVictoryPanel:364 在胜利 UI 创建失败时先抛异常，跳过 RecordVictory；DrawPlayCtrl.ShowVictoryPanel:1258 先记录进度再处理 UI 创建失败。两者均先写最高星，所以 Dig 此异常可能留下星数已有但通关／解锁未写的状态；本轮没有现场异常日志，不能判定截图中的 8／9 经历过该异常。
- 同场景证据边界：正常已解锁、普通锁、广告锁、星组、分页／关闭重开与配置版本检查已做静态对应；无网、服务不可用、失败／取消、晚回调均共用 RewardAdHelper，同一调用参数和回调源码。等待无结果时两面板均保持门禁；等待弹窗关闭由共用 helper 取消。真实 SDK／设备回调、完整 UI 点击／遮罩／触摸、实际保存重启和当前截图存档均未实测，仍 uncertain；未新增测试或编译任务，不将静态相同等同于运行已验证。
- 后续处理边界：如修复当前关显示，应修正 Dig 通关进度的语义，并评估旧存档恢复，不能只改 Item 的颜色条件。其它异常／重排差异需分别保留现行约束并验证。当前无生产变更可回滚，撤本段文档即可撤销本轮记录。
