# IWannerGame ChoiceLevelPopup 临时功能与风险记录

## 2026-10-14 当前规则：OneLink 胜利不推进后继，Next 回首组首关

- 用户明确撤回昨日 Next 选择规则并要求 OneLink 胜利不解锁下一组：仅改 `IWannerPlayCtrl`，OneLink Group 结算保留当前组/索引和小关统计，不新增 `PassedLevels`、不补后继 `UnlockGroups`、不推进 `MaxGroupId`；因此选组归一和入口门禁不会从本次胜利再推导后继。
- 专属 Next 固定进入配置首行 Group 的第 0 小关，接受后按原流程结束来源；Replay 保留来源，普通胜利 Next 仍返回选组。Popup、Item 四态、配置控制器、广告/GM 解锁、生成存档和 Prefab 未改。
- 已有通关/解锁不会撤销，历史普通通关的旧档补齐继续有效。OneLink 本次不新增 Group 已通关显示或 TBN 通关资格；后续普通完成仍照常通关和解锁。
- 同一生产源码隔离矩阵三套宏各 270 项断言、21 项保持边界静态检查、Editor/Android 源码编译通过；iOS 全量编译被两处原 Apple 引用错误阻断。PlayMode、真实来源/存档/广告和横屏真机仍为 `uncertain`。详细场景、证据路径及回滚见 [DeepLink 胜利面板功能记录](../../GameVictory/GameVictoryPanel_DeepLink功能与风险记录.md) 的 2026-10-14 章节。
- 下方 2026-10-13 Next 与 2026-10-09“DeepLink 也解锁后继”只作历史基线，由本节替换；普通 Group 通关解锁和既有旧档兼容仍有效。回滚只恢复本轮控制器增量，不删存档、不覆盖其它任务。

## 2026-10-13 OneLink 胜利 Next 改为未通关 Group（历史规则）

- 当前规则：OneLink 完成 IWanner Group 后，DeepLink 胜利页 Next 按 `LevelGroups` 配置顺序选择首个可进入、未记入 `PassedLevels` 且第 0 小关有效的 Group，并从该 Group 第 0 小关进入。当前配置下 Group1 通关后通常进入 `Group2 / ILevel_006`。
- 2026-10-09 下方章节中“按小关 `LevelSuccessCount` 选择、可回到已通关 Group 的漏玩小关”及旧私有方法名只代表当时历史实现，已被本条替换；后继解锁修复、`CanEnterGroup()` 权限、选组四态和普通入口仍有效。
- 选择器只在 IWanner OneLink Next 分支调用；普通胜利 Next 仍返回选组，Replay 仍从当前 Group 第 0 小关重玩。实现、115 项三套宏断言、Unity Bee 编译、剩余风险和回滚见 [DeepLink 胜利面板功能记录](../../GameVictory/GameVictoryPanel_DeepLink功能与风险记录.md) 的同日章节。

## 2026-10-09 Group 通关永久解锁下一组

### 修改前核对与验收边界

- 用户要求：Group 通关后解锁配置顺序中的下一 Group，普通、DeepLink、GM 进入后的 Group 结算均适用；最后一组不构造额外 Group，TBN 保留原规则。
- 可观察事实：当前 Editor 的 IWanner 存档为 `PassedLevels=[1,7,5]`、`UnlockGroups=[7,5]`、`MaxGroupId=8`。Group1 已落盘，但 Group2 不在永久解锁列表中。
- 源码基线：`IWannerPlayCtrl.RecordGroupVictoryProgress()` 只记通关、推进单个最高进度组；`CanEnterGroup()` 与 Item 的 Normal 状态均读取 `UnlockGroups`。`ResolveCurrentGroupId()` 由最高通关组推到 Group8，不能保留 Group1 对 Group2 的解锁资格。
- 调用链：最后一小关完成 → `HandleGroupCompletionAsync` → 通关落盘 → 插屏等待 → 普通/DeepLink 胜利；Popup `OnCreate` → `CreateLevelItems` → 旧档归一 → Item 四态 → 普通进组或锁定广告；DeepLink Next → 已有事件 → `TryEnterLowestPlayableUnpassedLevel` → `CanEnterGroup` → 原黑幕重建。
- 维护边界：复用 `IWannerGame.UnlockGroups`（手写模型已有 List 字段、`_Partial` 由 Bear SaveModule 的 `PartialClassGenerator` 生成属性）和 `DB.SaveIWannerGame()`；不改存档结构、生成属性、Luban/JSON、Prefab 绑定、Item/胜利按钮、关卡脚本、广告回调、统计、SDK 或只读目录。当前 Popup 无 Generated 文件，四态与普通/Boss/TBN 引用已存在，不需增补绑定。
- 拟修改：在 `IWannerGameLevelCtrl` 提供仅补齐已通关组直接后继的幂等方法；通关写入和 Popup 归一合并保存一次；进组/Next 的 `CanEnterGroup` 在权限判断前补齐旧档并按需保存，避免只修 UI 或依赖先打开选组。
- 不以 `Id + 1`、`Id <= MaxGroupId` 或解锁列表代替通关依据；`PassedLevels` 才能授予下一组，不能递归解锁下一组的后继。原 Max 不回退、实际 Current 高亮、已通关重玩、广告解锁和 TBN 规则保留。
- 事前验证场景：新档、1→2、跳到7后仍能进2、旧档 `[1,7,5]` 补齐2/6/8、不越过未通关组、重复结算/打开不重复写入、末组/Boss/TBN、非连续 Id、空集合/未就绪配置/缺存档、保存失败、UI 四态与实际入口、Next 首个未通过目标。无网/服务不可用不影响本地资格补齐；广告成功/失败/取消/关闭/晚到回调和资源失败恢复不改动，需平台回归。
- 环境缺口：两个约定位置的 `037-remote-resource-loading/plan.md` 均不存在；CodeGraph CLI 不可用，已用本地源码/配置/Prefab 证据替代。事前核对时实现与验证待执行，最终结果见下节；不能视为 PlayMode 或真机验收。

### 最终实现与验证证据

- 三个脚本已实现：`RestorePassedGroupUnlocks()` 仅检查配置相邻两行，前一组在 `PassedLevels` 时幂等补入下一组 Id；解锁列表不参与继续推导，不回写通关、统计或最高进度。通关与 Popup 原保存分支合并落盘；`CanEnterGroup()` 兼容未先开 Popup 的旧档，补齐有变化才保存并在失败时告警。
- Item 不需修改：Popup 在创建前完成补齐，Group2 直接进入现有 Normal/Current 点击链，不再走锁定广告；实际 Current 高亮与最高进度保留。普通/首通奖励/DeepLink 胜利共用同一个结算写入点，GM 也只改变来源门禁，不改变该结算入口。
- 生产源码隔离测试：TEMP `C:/Users/admin/AppData/Local/Temp/brainpunk-iwanner-group-unlock-20261009/Verify.ps1` 自动提取当前 7 个方法（进关门禁、胜利写入、当前小关保存、DeepLink Next、Popup 两个解析方法、Item 四态），直接编译完整配置控制器、存档模型与生成属性；Editor/Android/iOS 三套宏各通过 **133 项断言**。Unity 输入、资源、事件和磁盘保存外围使用替身，不是玩家态实测。
- 只读使用真实 Editor 存档副本：补齐后 `UnlockGroups=[7,5,2,6,8]`，Group2 UI 为 Normal 且进关允许，Group3/4 仍为 Lock；实际 Next 方法选择 `Group2 / ILevel_006` 并请求原 Reload 链。注册表原始字节前后相同，没有清档或直接改写玩家数据。
- 验证包括新档1→2、重复胜利/重开幂等、高组通关后低组资格不丢失、实际 Current 为7时2仍可进入、广告/GM 解锁但未通关不解锁后继、Boss15由14通关解锁、末组仍按原规则解锁 TBN、不连续/乱序 Id、缺存档/集合/未就绪配置、保存失败告警、Next 活跃/结算/加载/广告门禁、已通关 Group 被跳过小关可重访、全小关通过后无虚构目标。
- Unity 6000.0.59f2 Bee 引用定向 Roslyn 编译：Editor **0 错误 / 113 警告**，Android 宏 **0 错误 / 116 警告**，缺失源文件数均为0；iOS 被任务外 `ChoiceLevelPanel.cs:17`、`StartPanel.cs:14` 的 `Guru.UniKit.Apple` 缺失阻断（2错误）。初版临时编译脚本误补独立 Editor/asmdef 源码，已修正为仅补 Bee 未收录的必要游戏侧新文件；最终结果不包含跨程序集重复编译。
- 三个生产脚本与本轮文档 `git diff --check` 通过。原注释、Prefab、DB `_Partial`、配置、平台/SDK 文件及任务前已有改动均未改写；实现交付时未提交，后续用户已授权仅本地提交本轮增量，不推送远端。
- `uncertain`：未操作 Unity PlayMode，未验证真实 Bear PlayerPrefs 保存/重启、Android/iOS 横屏点击、资源加载失败恢复、插屏暂停返回、广告成功/失败/取消及晚到回调。最短验收：用现有存档重开 IWanner 选组并点击 Group2；新完成1后进2；DeepLink 跳7通关后 Next 应回到最早可进入未通过小关；杀进程重开确认2仍解锁。若失败，请保留存档并提供 Group/资源/保存告警，不通过清档掩盖问题。

### 风险与回滚

- 保存失败沿用告警并继续，当前会话使用补齐后的内存资格；跨重启持久化为 `uncertain`。旧通关记录仍可在下一次打开/进组时重新推导资格。
- 配置顺序改变后按当前顺序识别后继；已保存的解锁不会撤销，不跳过坏配置授予更后一组；资源和 Path 校验仍在实际进组前执行。
- 回滚只撤销本轮三个脚本与本节增量；已落盘的 `UnlockGroups` 默认保留，不能直接删除既有广告或 GM 解锁。若需数据回滚须单独定义来源/迁移方案。

## 2026-09-09 第一页末组通关后显示第二页

- 需求：第一页最后一个 Group 通关后，返回选组弹窗直接显示下一页。原行为仅恢复 `LastMapIndex`，进入第一页末组时保存值仍为 0，因此通关返回也停在第一页。
- 本次仅修改 `Scripts/IWannerGame_ChoiceLevelPopup.cs`：`RestoreMapPage()` 在夹取历史页码之后，若原目标为第一页、PageView 与 mapPages 均存在第二页，并且第一页最后一个已配置 Group 已在 `PassedLevels` 中，则使用既有 `SetPage(1, false)` 直接显示第二页。
- 末组按 `LevelGroups` 配置顺序和既有 `GetMapIndex()` 地图归属解析，不硬编码 Group 11，也不把预留点位或 TBN 作为末组。当前 Prefab 的第一页点位为 1～11、第二页为 12～22，结合当前 JSON 核对，实际触发 Group 为 11。
- 数据流：原玩法胜利流程将 Group 写入 `PassedLevels` → 原返回主页/创建选组流程 → Popup.OnShow → RefreshPages → RestoreMapPage → 第二页。没有修改胜利结算、Group 进度、广告、奖励、事件、Prefab、共享 PageView 或关卡脚本。
- 自动选页只改变展示，不把“尚未进入第二页关卡”记为进入记录；`LastMapIndex` 仍仅在成功进入关卡或历史越界归一时写入。旧的越界归一使用 `normalizedPageIndex`，不使用自动展示的目标页。已保存第二页或后续页的记录仍按原值恢复。
- 第一页末组尚未通关、只有一页、页绑定缺失或配置/存档不可用时沿用原恢复行为。只在 OnShow 执行一次离散查询，用户仍可手动翻回第一页；没有逐帧强制翻页。之后再次显示弹窗且恢复页为第一页时，仍会优先展示第二页。
- 原 pageView 字段注释只描述“恢复上次进入页”，与新增展示优先级不符，因此仅调整该句注释；其余已有注释不改写。
- 验证：源码构建 `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -v:q` 为 0 错误 / 123 警告；静态解析实际 Prefab 与 JSON 确认第一页末组为 Group 11、Group 12 位于第二页；目标 diff 空白检查通过。复核通关写入早于返回选组，以及 PageView 的无动画定位 API。
- 风险与未验证缺口（uncertain）：未在 Unity PlayMode 和 Android/iOS 横屏验证 Group 11 通关返回、旧档已通关重开、手动回第一页、仅一页及越界旧档。当前判断按“末组已通关”执行，不要求第一页其它组全部通关，符合本轮要求；广告跳组通关末组也会生效。最多逆序检查当前 Group 数量，不新增每帧开销或资源实例。
- 回滚：删除 `IsFirstMapLastGroupPassed()` 和自动改为页 1 的分支，恢复原页码变量及字段说明即可；无新增存档字段或数据迁移。

## 需求与背景

- 2026-08-11：把已有的 IWanner 临时选组 prefab 补成可用入口。
- 弹窗创建时按 Loading 已加载的 `IWannerLevelSort` 配置顺序生成 Item；每个 Item 文本只显示对应
  `IWannerLevelSort.Id`，点击后进入该 Group 的第 0 小关。
- 本阶段明确为临时功能，不处理 Group 解锁、已通关状态、存档续关、本地化名称或 Victory 返回选组。

## 修改前行为基线

- `IWannerGame_ChoiceLevelPopup` 与 `IWannerGame_ChoiceLevelItem` 只有空 `Start/Update`，没有数据、文本或点击逻辑。
- `IWannerGame_ChoiceLevelPopup.prefab` 已有失活 Item 模板、TMP 文本和 Container，但 Item 没有
  `CustomButton`；根节点挂的是复制来源 `CommmonPopup`，没有挂 IWanner 选择弹窗组件。
- `IWannerGameEnter` 点击后直接调用 `GameManager.TryEnterIWannerFirstGroup()`，固定进入配置第一行 Group。

## 影响文件、资源与系统

- 弹窗与 Item：
  - `Assets/Game/UI/IWannerGame/ChoiceLevelPopup/Scripts/IWannerGame_ChoiceLevelPopup.cs`
  - `Assets/Game/UI/IWannerGame/ChoiceLevelPopup/Scripts/IWannerGame_ChoiceLevelItem.cs`
  - `Assets/Game/UI/IWannerGame/ChoiceLevelPopup/Resources/IWannerGame_ChoiceLevelPopup.prefab`
- 主界面入口：`Assets/Game/UI/StartPanel/Scripts/IWannerGameEnter.cs`
- 玩法进入边界：`Assets/Game/Scripts/GameManager.cs`、
  `Assets/Game/Scripts/Play/IWanner/IWannerPlayCtrl.cs`
- 只读复用：`IWannerGameLevelCtrl.LevelGroups`、`TryGetGroup()`、`TryGetLevelData()`、Bear UI、
  `CustomButton`、现有 IWanner 关卡资源加载和失败回滚链。
- 本次未修改用户已有的 LevelSort Excel、运行时 JSON、生成配置代码、关卡 prefab、DB、Save、全局事件、
  Analytics、广告、RemoteConfig 或平台 SDK。

## 修改后行为与数据流

1. `IWannerGameEnter` 在入口开关通过后创建 Popup 层的 `IWannerGame_ChoiceLevelPopup`。
2. 弹窗 `OnCreate()` 读取 `GameManager.IWannerGameLevel.LevelGroups`；该列表由 Loading 从
   `TbIWannerLevelSort.DataList` 按配置顺序冻结。
3. 弹窗为每个非空排序行复制一次失活 Item 模板，传入完整 `IWannerLevelSort`，再激活克隆项。
4. Item 把 `Id` 转成文本，并通过本地回调把点击的配置对象交回弹窗；没有新增全局点击事件。
5. 弹窗把 `Id` 提交给 `GameManager.TryEnterIWannerGroup()`；控制器激活后由
   `IWannerPlayCtrl.TryEnterGroup()` 调用现有 `TryGetGroup()` 与 `TryGetLevelData(groupId, 0)` 校验。
6. 请求成功后沿用现有关卡异步加载、Normal 层关闭、GameplayPanel 创建和输入恢复链，同时销毁选组弹窗；
   同步失败时保留弹窗并恢复到 Main 控制器。

## 风险与边界

- `P1`：当前只完成临时全量列表，不读取解锁或通关状态；所有配置行都会显示，不能作为正式选组验收。
- `P1`：Group 在生成 Item 时只过滤空行，完整 Path/LevelData 校验发生在点击后；无效行会记录错误并留在弹窗。
- `P2`：弹窗每次创建按 Group 数量同步实例化 Item；当前 JSON 为 7 行，开销有限，后续行数显著增加时需评估
  ScrollRect、对象池和首帧实例化峰值。
- `P2`：Item 的点击依赖 prefab 中根节点 `CustomButton` 与子级 Image 的 Raycast Target；层级或射线配置变化后
  需重新核对触控命中。
- `uncertain`：尚未在 Unity PlayMode 验证 7 个 Item 的布局、关闭按钮、重复打开、点击每个 Group、无效配置
  失败保留、异步资源失败回滚以及 Android/iOS 横屏触控。

## 验证记录

- `Assembly-CSharp.csproj` 定向构建通过：`0 error / 101 warning`；warning 属于工程既有范围。
- 静态确认两个新脚本均已进入 `Assembly-CSharp.csproj`。
- 静态确认 prefab 根组件指向 `IWannerGame_ChoiceLevelPopup`，并绑定 Container、Item 模板和关闭按钮；Item 已绑定
  TMP 文本与本地 `CustomButton`，模板保持失活。
- 静态确认新代码没有 `Update`、`OnValidate`、`Reset`、DB/Save 或新全局事件入口。
- `git diff --check` 对本次已跟踪代码通过，仅有仓库现有 LF/CRLF 转换提示。

## 回滚路径

- 把 `IWannerGameEnter.OnEnterClicked()` 恢复为调用 `TryEnterIWannerFirstGroup()`，即可恢复原首组直入行为。
- 删除 `GameManager.TryEnterIWannerGroup()` 与 `IWannerPlayCtrl.TryEnterGroup()`，不会影响既有首组调试入口。
- 将 popup prefab 根组件恢复为原复制来源组件，并移除 Item 的 `CustomButton` 与两个脚本逻辑，可回到空临时 prefab；
  无需回滚配置、存档或关卡资源。

## Menu 返回选组补充（2026-08-11）

### 需求与修改前基线

- 通用 `GameSettingPopup` 的 Menu 按钮会把退出请求交给当前 `IPlayPauseHost.ExitToLevelSelection()`。
- 修改前 `IWannerPlayCtrl.ExitToLevelSelection()` 直接调用 `GameManager.ReturnToStartPanel()`，因此用户离开当前小关后还要从
  StartPanel 再次进入 IWanner，和 DrawGame 的“直接回到玩法选关页”行为不一致。

### 修改后行为与边界

1. Menu 请求现在复用既有 `TryOpenLevelSelection()`：增加 activation version、清理组结算/暂停/加载/失败计数状态，
   销毁当前关卡与 GameplayPanel，并恢复入口相机。
2. 清理完成后直接创建 `IWannerGame_ChoiceLevelPopup`，同时保持 `IWannerPlayCtrl` 激活；用户可继续选择 Group，
   无需经过 StartPanel。
3. 如果选组弹窗创建失败，记录错误并回退 `GameManager.ReturnToStartPanel()`；若 `GameManager` 本身不可用则仅记录错误，
   避免空引用形成第二次异常。
4. 本次没有修改 prefab、配置、资源路径、全局事件、存档结构、进度写入、广告、Analytics 或平台 SDK；当前 Group
   断点数据由既有存档链保持，Menu 返回选组不会额外写入或清空。

### 验证、风险与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal` 通过：`0 error / 1119 warning`；warning 为工程既有范围。
- 静态确认 Menu 与 Victory next 共用相同的选组恢复 helper，失败回退仍保留；未新增 Update、全局事件或资源加载入口。
- `uncertain`：Unity PlayMode 尚未验证暂停后点击 Menu、选组弹窗层级、连续快速点击、当前 Group 断点续关、弹窗创建失败
  回退及 Android/iOS 横屏触控。
- 回滚只需把 `IWannerPlayCtrl.ExitToLevelSelection()` 恢复为直接调用 `ReturnToStartPanel()`；弹窗、Item、Group 存档与
  Victory next 链均无需回滚。

## Item 五态显示补充（2026-08-12）

### 需求与修改前基线

- `IWannerGame_ChoiceLevelItem` 需要支持 `normal`、`passed`、`current`、`lock_state`、`boss` 五种互斥表现，
  其中 Boss 必须以 `IWannerLevelSort.IsBoss` 配置为准。
- 修改前脚本只有一个未绑定的 `TMP_Text` 和一个未绑定的 `CustomButton`，不会切换新 Item prefab 中已经存在的五个
  状态根节点；新 prefab 的默认激活状态还会同时显示 `passed_state` 与 `boss_state`。

### 影响文件、资源与系统

- `Assets/Game/UI/IWannerGame/ChoiceLevelPopup/Scripts/IWannerGame_ChoiceLevelItem.cs`
- `Assets/Game/UI/IWannerGame/ChoiceLevelPopup/Scripts/IWannerGame_ChoiceLevelPopup.cs`（仅同步事实已变化的 API 注释）
- `Assets/Game/UI/IWannerGame/ChoiceLevelPopup/Prefabs/IWannerGame_ChoiceILeveltem.prefab`
- 只读复用 `IWannerLevelSort.IsBoss`、`LockType` 与 `DB.IWannerGame.PassedLevels`、`MaxGroupId`、`UnlockGroups`；
  没有新增或修改存档字段、配置表、资源加载、全局事件、Analytics、广告或平台 SDK。

### 修改后行为与数据流

1. `SetData()` 先把 Group Id 同步到 Inspector 显式绑定的 `normalTxt`、`passedTxt`、`lockTxt`、`bossTxt`，
   再解析唯一显示状态；不再使用 `GetComponentInChildren<TMP_Text>()` 运行时搜索层级。
2. `IsBoss == true` 时固定显示 `boss_state`，优先级高于通关、当前和锁定状态。
3. 非 Boss Group 按 `PassedLevels`、`MaxGroupId` 的顺序判断 `passed_state` 与 `current_state`。
4. 其余 Group 在 `LockType == Unlock` 或 Id 已存在于 `UnlockGroups` 时显示 `normal_state`，否则显示
   `lock_state`；所有状态切换均为一次 `SetData()` 内的离散刷新，没有逐帧轮询。
5. Item 在启用期间给五个状态根节点上现有的 `CustomButton` 绑定同一局部回调，禁用时全部解绑；该历史行为已由
   下方“Group 通关推进与锁定入口补齐”收紧为仅可进入状态绑定，`lock_state` 不再提交进入请求。

### 验证、风险与回滚

- `git diff --check` 对本次已跟踪脚本与文档返回 0；Item prefab 当前是未跟踪美术资源，不在该命令覆盖范围内，
  已另行静态确认五个序列化状态引用均指向各自 prefab 根节点，且本次新增的五行引用没有尾随空白。脚本没有新增
  `Update`、`OnValidate`、`Reset`、编辑器生命周期或全局事件。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:q '-clp:ErrorsOnly;Summary'` 已通过，结果为
  `0 error / 101 warning`；warning 属于工程现有范围。
- `uncertain`：尚未在 Unity PlayMode 验证五种存档组合、Boss 与其它状态同时满足时的优先级、各状态点击命中、
  Android/iOS 横屏显示。该阶段尚未接入正式广告解锁；`MaxGroupId` 推进及 `UnlockGroups` 写入已由下方后续增量补齐。
- `current_state` 当前 prefab 层级没有 TMP 文本节点，因此没有虚构 `currentTxt` 字段或复制其它状态布局；
  是否需要新增当前关卡编号及其美术布局为 `uncertain`，需后续按 UI 设计确认。
- 工作区并行更新后的 `Resources/IWannerGame_ChoiceLevelPopup.prefab` 已绑定 `map`、新的 Item prefab 和
  `reviveTxt`，因此 `CreateLevelItems()` 已具备实例化五态 Item 的静态入口。当时 `closeButton` 仍为空；该缺口已由
  下方“close_btn 绑定补充”关闭，完整 Popup PlayMode 验证仍为 `uncertain`。
- 回滚时恢复 `IWannerGame_ChoiceLevelItem` 的单文本/单按钮实现，并移除 Item prefab 组件上的五个状态根节点引用与
  四个文本引用即可；
  不需要回滚配置、DB 或玩法控制器。

## Map 定位与累计重试显示补充（2026-08-12）

### 需求、基线与影响范围

- 用户提供的新 `IWannerGame_ChoiceLevelPopup.prefab` 已包含 `Map`、名称为 `1`～`11` 的直接子定位节点和
  `revive_txt`，并要求运行时 Item 按这些位置生成，同时显示当前玩家的 IWanner 历史重试总数。
- 修改前 `IWannerGame_ChoiceLevelPopup` 仍把所有 Item 直接生成到旧 `Container`，不读取 `Map` 定位节点，
  也没有 `revive_txt` 引用或刷新逻辑。
- 本次修改：
  - `Assets/Game/UI/IWannerGame/ChoiceLevelPopup/Scripts/IWannerGame_ChoiceLevelPopup.cs`
  - `Assets/Game/UI/IWannerGame/ChoiceLevelPopup/Resources/IWannerGame_ChoiceLevelPopup.prefab`
- 只读复用 `IWannerGameLevelCtrl.LevelGroups` 和 `DB.IWannerGame.LevelReviveCount`；没有新增或修改存档字段、
  重试写入、全局事件、Analytics、广告、RemoteConfig、资源地址或平台分支。

### 修改后行为与数据流

1. Popup `OnCreate()` 先刷新累计重试文本，再创建 Group Item；两项工作都只执行一次，没有逐帧轮询。
2. 每个非空 `IWannerLevelSort` 使用 `Id.ToString()` 查找 `Map` 下的同名直接子节点。定位存在时，Item prefab
   生成到该节点下，并把锚点、局部坐标、局部旋转和局部缩放归一；因此 Group 显示不依赖配置行或子节点排序。
3. 定位节点缺失时只记录带 Group Id 的 Bear Logger warning 并跳过该项，不把 Item 错放到其它 Group 位置。
4. `revive_txt` 对当前 `DB.IWannerGame.LevelReviveCount.Values` 求和；负值按 `0` 处理，空存档或空字典显示 `0`。
   该读取不调用 `EnsureValidCollections()`、不保存数据，也不改变《关卡打点生命周期约定》的 revive 口径。
5. Resources prefab 已把 `map`、专用 `itemPrefab` 和 `reviveTxt` 精确绑定到现有资源；用户的其它大范围美术改动保持不变。

### 验证、风险与回滚

- CodeGraph 已确认 `CreateLevelItems()` 与 `RefreshTotalReviveCount()` 都只有 `OnCreate()` 一个调用入口，未发现其它
  运行时依赖或覆盖测试；当前配置 JSON 有 7 个 Group，prefab 静态 YAML 有 `1`～`11` 共 11 个定位节点。
- Popup 脚本与两份中文记录的 `git diff --check` 返回 `0`；把用户当前的大范围 prefab 美术差异一并纳入时，
  会报告 4 行既有空 YAML 字段的尾随空格，本次新增的 `map`、`itemPrefab`、`reviveTxt` 绑定行不在报错范围。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:q '-clp:ErrorsOnly;Summary'` 进入编译后被工作区既有
  `Level71ShopBtnCtrl.cs:143` 的 `CS1061` 阻断，结果为 `1 error / 101 warning`；错误不在本次修改路径。
- `uncertain`：尚未执行 Unity PlayMode 和 Android/iOS 横屏验证，需实测七个 Item 的位置、缩放、层级、点击命中、
  重复打开与 `revive_txt` 显示。
- 后续已在 `IWannerPlayCtrl` 的既有死亡/手动 Reset 去重边界补齐 `DB.IWannerGame.LevelReviveCount` 写入；
  Popup 重新打开时会显示所有 IWanner 小关历史重试次数之和，具体写入边界见下方补充。
- Resources prefab 当时的 `closeButton` 为空；该历史缺口已由下方“close_btn 绑定补充”使用用户新增的
  `close_btn/CustomButton` 关闭，关闭动画与触控仍需 PlayMode 验收。
- 回滚时把 Item 恢复为旧 Container 直挂生成方式，移除 `map`、`reviveTxt` 字段与三个 prefab 引用即可；
  `LevelReviveCount` 存档、Item 五态逻辑、配置和玩法控制器无需回滚。

## close_btn 绑定补充（2026-08-12）

### 需求、基线与影响范围

- 用户已在 `IWannerGame_ChoiceLevelPopup.prefab` 新增 `close_btn`，要求接入弹窗关闭。
- 修改前 Popup 脚本已有 `closeButton -> OnCloseButtonClicked() -> UIManager.DestroyUI(this)` 的 Bear UI 局部关闭链，
  但 prefab 的 `closeButton` 序列化引用仍为 `0`，因此点击不会进入脚本。
- 本次只修改 `Assets/Game/UI/IWannerGame/ChoiceLevelPopup/Resources/IWannerGame_ChoiceLevelPopup.prefab`，
  没有修改脚本、全局事件、存档、配置、资源地址、Analytics、广告或平台逻辑。

### 修改后行为、验证与回滚

- `closeButton` 现在绑定 `close_btn` GameObject 上既有的 `CustomButton`；点击沿用现有按钮音效/缩放反馈，随后由
  `OnCloseButtonClicked()` 调用 `UIManager.Instance.DestroyUI(this)` 销毁当前 Popup。
- 静态 YAML 已确认 `close_btn` 与绑定的 `CustomButton` 属于同一 GameObject，字段 fileID 为
  `3986092125211631471`；CodeGraph 确认关闭回调只有本 Popup 的 `BindButtons()` 一个绑定入口。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:q '-clp:ErrorsOnly;Summary'` 已通过，结果为
  `0 error / 101 warning`；warning 属于工程现有范围。
- `uncertain`：尚未执行 Unity PlayMode 和 Android/iOS 横屏触控验证，需确认按钮射线、点击反馈、销毁动画以及
  关闭后 StartPanel 输入恢复。
- 回滚只需把 Popup 组件的 `closeButton` 引用恢复为空；脚本与其它 UI 节点无需回滚。

### revive_txt 历史为 0 的原因与写入补齐

- 原实现只有字段和空字典初始化，死亡/手动 Reset 没有写入方，因此旧版本 `revive_txt` 显示 `0` 是数据源为空，
  不是 TMP 或 prefab 绑定失败。
- 2026-08-12 已补齐写入：手动 Reset 在 `_isAwaitingFailureReset == false` 时记录一次，死亡在
  `ShowFailureResetArea()` 记录一次；死亡后点击 Reset 因等待失败重置门禁不再重复记录。
- 每次记录以当前 `IWannerLevelData.Path` 为 Key，使 `LevelReviveCount[Path]` 加一，并立即调用
  `DB.SaveIWannerGame()`；旧档空集合先通过 `EnsureValidCollections()` 修复。
- Group Replay、成功推进和 Skip 不进入该写入方法；Popup 仍只负责汇总显示，不在 UI 层修改存档。
- 静态调用检查与完整 C# 构建通过（`0 error / 101 warning`）；Unity PlayMode、杀进程重启以及 Android/iOS
  横屏流程仍为 `uncertain`。

## Group 通关推进与锁定入口补齐（2026-08-12）

### 需求、原行为与影响范围

- 玩家已经通关第 4 组，但第 5 组没有解锁并显示 `current_state`。直接代码证据显示，旧
  `RecordGroupVictoryProgress()` 只把当前 Group 加入 `PassedLevels`，没有推进 `MaxGroupId`；Item 又只在
  `MaxGroupId == Group Id` 时显示 Current，因此旧存档会停留在锁定表现。
- 旧 Item 给 `lock_state` 也绑定进入回调，`IWannerPlayCtrl.TryEnterGroup()` 只校验配置存在，不校验 Group 是否可进入；
  因而视觉锁定与实际入口权限不一致。
- 本次修改 `IWannerGameLevelCtrl.cs`、`IWannerPlayCtrl.cs`、`IWannerGame_ChoiceLevelPopup.cs` 和
  `IWannerGame_ChoiceLevelItem.cs`。只读当前 `tbiwannerlevelsort.json` 的配置顺序，复用 `PassedLevels`、
  `MaxGroupId`、`UnlockGroups` 和既有 `DB.SaveIWannerGame()`；没有修改配置、存档结构、资源地址、全局事件、
  Analytics、广告调用或平台分支。

### 修改后行为与数据流

1. `IWannerGameLevelCtrl.ResolveCurrentGroupId()` 按配置顺序合并存档现有 `MaxGroupId` 与最高已通关 Group：新档以
   配置首组为 Current；历史最高已通关组存在下一组时，以其下一组为 Current；已有更高 `MaxGroupId` 时不回退；
   最后一组已通关时保留最后一组 Id。计算不依赖 `Id + 1`，因此允许配置 Id 不连续。
2. Popup 创建 Item 前执行一次旧档归一并保存。当前“仅 `PassedLevels` 包含 4、`MaxGroupId == 0`”的存档会补写
   `MaxGroupId = 5`，随后第 4 组显示 Passed、第 5 组显示 Current；保存失败只告警，当前会话仍使用内存结果并在
   下次打开时重试。
3. Group 首次或重复通关仍先维护 `PassedLevels`，随后使用同一配置顺序解析器推进 `MaxGroupId`，并与既有
   `CurrentGroupID`、`CurrentLevel` 一次保存。重玩较低 Group 不会把已经更高的 Current 进度回退。
4. Item 由 Popup 显式传入归一后的 Current Group Id；`lock_state` 不再绑定点击，回调也二次拒绝 Lock 状态。
   `IWannerPlayCtrl.TryEnterGroup()` 作为权威入口再次校验：Boss、Passed、Current、配置 `LockType == Unlock` 或已在
   `UnlockGroups` 的 Group 可进入，其余请求返回 locked 错误。
5. 该逻辑只在选组创建、选组提交和 Group 通关时遍历当前 7 行配置，不新增 `Update`、实例化循环、资源加载或
   移动端平台分支；Victory 插屏前后的暂停、输入锁、回调和面板创建顺序保持不变。

### 验证、风险与回滚

- CodeGraph 已确认 `ResolveCurrentGroupId()` 有 Popup 归一、入口校验和通关写入三个调用方；通关写入仍只从
  `HandleGroupCompletionAsync()` 调用一次，入口校验位于加载关卡资源之前。
- 基于当前 JSON 配置执行静态矩阵：新档解析为 Group 1；旧档 `Passed=[4], Max=0` 解析为 Group 5；
  `Passed=[4], Max=6` 保持 Group 6；最后组 `Passed=[7], Max=7` 保持 Group 7，四项均通过。
- 目标脚本 `git diff --check` 通过；`dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:q
  '-clp:ErrorsOnly;Summary'` 通过，结果为 `0 error / 101 warning`。
- `uncertain`：尚未执行 Unity PlayMode 与 Android/iOS 横屏验证。需覆盖新档、旧档第 4 组已通关、4→5 首次通关、
  重玩低组不回退、最后一组、保存失败、Lock 点击无进入、Current/Passed/Boss 正常进入。
- 该阶段只保证已有 `UnlockGroups` 数据可进入；正式广告解锁写入及独立小关统计对象已由下方后续实现补齐。
- 代码回滚需同时移除配置顺序解析、Popup 旧档归一、通关推进和入口锁定。已经由本版本写入的 `MaxGroupId` 不会因
  代码回滚自动恢复；若产品要求数据级回滚，需另做存档迁移或清档，不能只还原脚本。

## 正式广告解锁与运行统计展示对接（2026-08-12）

### 需求、修改与职责边界

- 用户要求 `UnlockGroups` 成为正式功能，并把 IWanner 的小关游玩、成功、重试和时长统计从玩法控制器抽离。
- `IWannerGame_ChoiceLevelItem` 的 Lock 状态现在只派发锁定回调；Popup 持有广告请求、存档写入和进入组流程，
  Item 仍只负责五态显示与文本绑定。`revive_txt` 仍只汇总 `LevelReviveCount.Values`，不承担任何写入。
- 小关统计由新增 `IWannerLevelRuntimeStatistics` 维护，四项字典统一使用 `IWannerLevelData.Path`；Popup 不感知
  尝试生命周期。没有新增全局事件、SDK Analytics 事件、配置、资源地址或存档字段。

### 广告解锁、存档与输入流

1. 普通、Passed、Current、Boss Item 复用正常进入回调；Lock Item 进入锁定回调，不能直接触发资源加载。
2. `LockType.Ad` 请求 `RewardPlacement.OutGameUnlockLevel`。请求期间锁住关闭按钮和重复点击；Popup 关闭时调用
   `RewardAdHelper.CancelCurrent()` 并清理本次请求所有权。
3. 仅成功回调把 Group Id 幂等加入 `DB.IWannerGame.UnlockGroups` 并调用 `DB.SaveIWannerGame()`，随后走
   `TryEnterIWannerGroup()`；失败或取消不写入。保存失败使用 Bear Logger 告警，但不让已成功观看广告的玩家卡在弹窗。
4. 非 Ad 锁定类型显示既有 `U_HurryTips_TBC` 本地化提示。进入层仍在加载前验证 Boss、Passed、Current、配置直解锁
   或 `UnlockGroups`，防止 UI 状态与实际权限不一致。

### 验证、风险、未覆盖与回滚

- 静态检查确认 Lock 与普通回调分离，`UnlockGroups.Add` 只位于广告成功分支，失败和取消路径无写入；
  `Assembly-CSharp.csproj` 常规构建通过，结果为 `0 error / 101 warning`。
- 主要风险是广告成功但保存失败造成当前会话已解锁、重启后回退；当前沿用项目已有的“告警并继续”降级策略。
  `LevelLockType.Level` 的正式规则仍为 `uncertain`。
- `uncertain`：Unity PlayMode、真实广告成功/失败/取消/超时、Popup 关闭竞争、保存失败、重启恢复、解锁后进入及
  Android/iOS 横屏触控未验证。
- 回滚时移除 Popup 的激励视频请求、锁定回调和 Item 的 Lock 点击绑定；已保存的 `UnlockGroups` 默认保留且仍会被
  旧入口识别。若需要撤回数据，必须另行定义迁移和广告补偿方案。

## 普通/Boss/TBN 区分创建与 TBN 两态入口（2026-08-13）

### 需求、原行为与影响范围

- Item 资源结构已拆分为普通、Boss 和 TBN 三种 Prefab。旧代码只持有一个普通 Item Prefab，并在同一脚本中维护
  Boss 状态，不能正确使用新的专属层级；旧 Popup 也没有在 `LevelSort` 之外创建 TBN 入口。
- 本次修改 `IWannerGame_ChoiceLevelItem.cs`、`IWannerGame_ChoiceLevelPopup.cs`、`IWannerGameLevelCtrl.cs`、
  `IWannerPlayCtrl.cs`、`GameManager.cs` 与 ChoiceLevelPopup 相关 Prefab/动画/字体材质资源，并收录 TBN 目标资源
  `ILevel_999.prefab`。没有新增 Luban 配置、存档字段、全局事件、广告 placement 或 SDK Analytics 事件。
- `Resources/IWannerGame_ChoiceLevelPopup.prefab` 已分别绑定普通、Boss、TBN Item Prefab；`Sprites` 下旧 Popup Prefab
  的 GUID 在当前 `Assets` 中没有引用，本次随资源结构调整删除，运行时继续只从 `Resources` 路径加载正式 Popup。

### 创建、解锁与运行时流程

1. Popup 仍按每条 `IWannerLevelSort.Id` 查找 `Map/<Id>`。普通组实例化普通 Item，`IsBoss` 组实例化 Boss Item；
   Boss 的入口权限保持原规则，可直接进入，不再依赖普通 Item 内部的第五种 Boss 状态。
2. 所有配置组创建完成后，以 `LevelGroups.Count + 1` 查找末尾 Map 节点并额外实例化一个 TBN Item。TBN 不构造
   `IWannerLevelSort`，不进入 Group 进度、广告解锁或 `UnlockGroups` 数据流。
3. TBN 只有 Unlock 与 Lock 两种玩家可见状态：最后一个已配置 Group Id 已存在于 `PassedLevels` 时为 Unlock，
   否则为 Lock。该条件由 `IWannerGameLevelCtrl.IsTbnUnlocked()` 统一计算，Popup 展示和玩法入口各校验一次。
4. Lock TBN 点击只显示既有 `U_HurryTips_TBC` 提示，不请求激励视频，也不写存档。Unlock TBN 经
   `GameManager.TryEnterIWannerTbn()` 和 `IWannerPlayCtrl.TryEnterTbn()` 打开 `ILevel_999`；进入请求不改变
   `CurrentGroupID`、`CurrentLevel`、`MaxGroupId`、`PassedLevels` 或 `UnlockGroups`。
5. `ILevel_999` 复用 IWanner 已有资源加载、暂停、输入、Reset、失败和本地运行统计清理。TBN 成功后直接返回选组界面，
   不触发 Group 胜利面板和 Group 通关写入；运行统计按资源路径 `ILevel_999` 作为字典 Key 记录。

### 验证、风险与回滚

- 静态资源核对确认：Popup 的普通/Boss/TBN 引用分别指向三个现有 Item Prefab；TBN Animator 引用新增 Controller，
  Controller 引用新增动画；三个新增字体材质分别被普通或 Boss Item 引用；`ILevel_999.prefab` 包含现有
  `IWannerLevelCtrl` 资源结构。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:q '-clp:ErrorsOnly;Summary'` 通过，结果为
  `0 error / 101 warning`；warning 属于工程现有范围。目标文件空白检查在提交前通过。
- `uncertain`：尚未执行 Unity PlayMode、真机横屏和 AssetBundle/Resources 实际加载验证。需覆盖普通/Boss Prefab 表现、
  最后一组通关前后 TBN 两态、Lock 点击不弹广告、Unlock 进入 `ILevel_999`、死亡/Reset、成功返回选组、重复进入以及
  Android/iOS 输入恢复。
- 回滚需同时还原 Popup 三 Prefab 引用与创建分支，移除 TBN 判定/入口和 `_isTbnLevel` 运行分支，并恢复旧 Item Boss 状态；
  随后移除新增 Boss/TBN Item、动画、材质和 `ILevel_999` 资源。该功能未新增或迁移存档字段，代码回滚不需要数据迁移；
  已产生的 `ILevel_999` 本地统计 Key 可保留，也可在产品明确要求后另行清理。

## Map2 连续 Group 点位与 PageView 分页补充（2026-08-24）

### 需求、修改前基线与影响范围

- 用户把 Popup 地图层级调整为 `Scroll View/Viewport/Content/Map1|Map2`，其中 Map1 负责 Group 1～11，
  要求 Map2 继续承载后续 GroupItem，并指定使用项目现有 `PageView` 提供分页吸附。
- 用户后续明确确认 Map2 的本轮终点为 Group 22，因此最终点位范围固定为 `12`～`22`，不创建 Group 23 点位。
- 修改前脚本只序列化一个 `map`，运行时只能在 Map1 的 Points 直接子节点中查找；Map2 虽有 12 个点位，名称仍为
  `1`～`12`，Group 12、13 和当前 TBN 位置 14 均无法在第二页创建。PageView 已挂到滚动节点，但左右按钮为空，
  两个翻页图标默认失活且不接收射线。
- 本次修改：
  - `Assets/Game/UI/IWannerGame/ChoiceLevelPopup/Scripts/IWannerGame_ChoiceLevelPopup.cs`
  - `Assets/Game/UI/IWannerGame/ChoiceLevelPopup/Resources/IWannerGame_ChoiceLevelPopup.prefab`
- 只读核对当前 `tbiwannerlevelsort.json` 的 Group 1～13；没有修改配置、存档、解锁、广告、资源地址、
  Analytics、全局事件、RemoteConfig 或平台分支。

### 修改后结构与运行时流程

1. Popup 把单个 `map` 引用改为按 PageView 页序绑定的 `mapPages` 数组，当前依次绑定
   `Map1/Points` 与 `Map2/Points`。`GetMapPosition()` 逐页查找 Id 同名的直接子节点，空页会跳过，全部缺失时沿用
   Bear Logger warning 并跳过该 Item，不会错放到其它点位。
   原序列化字段注释限定“单个 Map 的直接子节点”，已与当前多页层级事实冲突，因此本次只同步修正该处旧注释，
   没有改写其它既有注释。
2. Map1 点位保持 `1`～`11` 不变；Map2 最终保留的 11 个有效点位连续编号为 `12`～`22`。因此当前 Group 12、13 与
   `LevelGroups.Count + 1 == 14` 的 TBN 都会生成到 Map2，15～22 作为后续 Group/TBN 的预留点位。
3. `PageView` 继续按其既有契约复用同节点的 `ScrollRect`、Viewport 和 Content；Content 的两个直接子节点
   Map1、Map2 即两页。这里的 ScrollRect 是 PageView 的 `[RequireComponent]` 内部依赖，不再由业务脚本直接控制分页。
4. 左右翻页图标已启用并补上标准 `UnityEngine.UI.Button`，分别绑定 PageView 的 `prevButton`、`nextButton`；
   图标 Image 开启 Raycast Target。页面边界的按钮可交互状态由 PageView 统一更新，拖拽结束仍按既有阈值吸附。

### 验证、剩余风险与回滚

- Unity 最新重序列化后的静态 YAML 校验结果：163 个本地对象无重复 fileID，GameObject 的组件引用无缺失；
  Map1 为 `1`～`11`，Map2 为 `12`～`22`；当前 Group 1～13 与 TBN 14 的点位覆盖无缺口；Content 直接子页数为 2。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:q '-clp:ErrorsOnly;Summary'` 以 exit code 0 通过。
- 目标 C# 脚本空白检查无新增问题；完整 prefab diff 仍报告本轮 Unity 自动重序列化产生的若干空字段尾随空格，
  本次没有为通过文本检查而清洗整份用户 prefab，避免制造无关资源格式 churn。
- `uncertain`：尚未执行 Unity PlayMode 与 Android/iOS 横屏验证。需实测左右按钮、滑动吸附、边界按钮状态、
  Group 12/13 与 TBN 14 的位置和点击命中、重复打开，以及第二页完整美术在 Viewport 内的裁切与对齐。
- 回滚时把脚本恢复为单 `map` 查找并把 prefab 引用恢复到 Map1/Points，同时撤销 Map2 点位连续编号、两个 Button
  和 PageView 的左右按钮绑定即可；配置、存档、Group 进度与玩法进入链无需回滚。

## 上次 Map Index 保存与自动恢复（2026-08-24）

### 需求、基线与影响范围

- 用户要求保存玩家上一次成功进入关卡所在的 Map Index，并在下次打开
  `IWannerGame_ChoiceLevelPopup` 时让 PageView 自动跳转到对应页；单纯浏览其它页不应改写该值。
- 修改前 PageView 每次启用都会停留在默认第 0 页；IWanner 独立存档没有选组地图专用的 Map Index 字段。
- 本次修改 `IWannerGame.cs`、`IWannerGame_Partial.cs`、`IWannerGame_ChoiceLevelPopup.cs` 与
  `IWannerGame_ChoiceLevelPopup.prefab`；在 `IWannerGame` 存档模型新增专用 `lastMapIndex`/`LastMapIndex`，
  不再借用历史 `chioceLevelPanelPageIndex`。本次没有新增全局事件、Analytics、广告、资源地址或平台分支。

### 修改后生命周期与存档流

1. Popup prefab 显式绑定承载 Map1、Map2 的 PageView。`OnShow()` 在 `base.OnShow()` 激活界面后先调用
   `PageView.RefreshPages()` 刷新页数和布局，再读取 `DB.IWannerGame.LastMapIndex`，按
   `0..PageCount-1` 夹取，并调用 `SetPage(index, false)` 让 PageView 本体无动画定位到对应页。
2. 恢复只调用 PageView 定位 API，不订阅 `OnPageChanged`，因此打开弹窗和玩家手动浏览页面都不会改写
   上一次进入记录。旧档缺字段时沿用 int 默认值 0；
   历史值超过当前页数时回到最近有效页，并通过 `DB.SaveIWannerGame()` 回写归一值。
3. Group 或 TBN 的进入请求成功后，Popup 用对应点位所属 `mapPages` 下标计算 Map Index；仅当新值与
   `LastMapIndex` 不同时写入并调用 `DB.SaveIWannerGame()`。进入失败不更新，PageView 按钮和拖拽也不触发写盘。
4. 保存失败时使用 Bear Logger warning；内存中的新 Index 会让当前会话再次打开时仍回到新页，但杀进程重启后可能
   回退到上一次成功落盘页，这是现有 `DB.SaveIWannerGame()` 失败降级边界。

### 验证、剩余风险与回滚

- 静态确认新字段在 `IWannerGame.Init()` 默认归零，`LastMapIndex` 直接读写同一序列化字段；
  `UIManager` 的调用顺序是 `OnCreate()` 早于 `OnShow()`，且 `BaseUIView.OnShow()` 负责激活界面。恢复现改为
  `base.OnShow()` 后刷新 PageView 并切页；Popup 不再订阅切页存档回调，prefab 引用指向现有 PageView 组件。
- `git diff --check` 对本次脚本和 prefab 返回 0，仅输出仓库既有 LF/CRLF 转换提示；
  `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:q '-clp:ErrorsOnly;Summary'` 以 exit code 0 通过。
- `uncertain`：尚未执行 Unity PlayMode、杀进程重启和 Android/iOS 横屏验证。需覆盖新档页 0、仅浏览 Map2 后重开仍回已进入页、
  成功进入 Map2 Group/TBN 后重开直接显示 Map2、进入失败不改写、
  杀进程恢复、越界旧值、存档失败、快速翻页后立即关闭，以及左右按钮/拖拽两种切页入口。
- 回滚只需移除 Popup 的 PageView 引用、恢复/订阅/保存代码和 prefab 绑定；
  `lastMapIndex`/`LastMapIndex` 可先保留为未使用存档字段，如需删除应另行验证已落盘旧档的兼容性。

## Current 状态优先跟随实际 Group 修复（2026-08-25）

### 需求、基线与影响范围

- 用户反馈已经进入 Group 13，但选组 Item 的 `current_state` 不在 Group 13。
- 修改前 Popup 只把 `MaxGroupId + PassedLevels` 按配置顺序解析出的进度 Group Id 传给 Item；
  `IWannerPlayCtrl.SaveCurrentLevelProgress()` 实际写入的 `CurrentGroupID` 没有参与 Current 表现判断。
  因此玩家通过广告解锁并进入较后的 Group 时，该 Group 会因 `UnlockGroups` 显示 Normal，Current 仍留在顺序进度 Group。
- 本次仅修改 `IWannerGame_ChoiceLevelPopup.cs`、`IWannerGame_ChoiceLevelItem.cs` 和本记录，并在
  `DevelopMD/已有功能设计与使用记录.md` 追加共享事实；不修改 prefab、配置、存档结构、资源、广告、事件、
  Analytics 或平台分支。

### 修复后行为与数据、UI 流

1. Popup 仍先调用 `IWannerGameLevelCtrl.ResolveCurrentGroupId()` 归一 `MaxGroupId`，继续保留旧档补齐、
   Group 通关后下一组推进和不回退最高进度的原行为。
2. Current 表现随后优先读取 `DB.IWannerGame.CurrentGroupID`。该 Id 必须大于零、能通过当前配置和 LevelData 校验、
   尚未存在于 `PassedLevels`，并满足 Boss、顺序进度、配置直接解锁或 `UnlockGroups` 任一现有入口条件；满足时由
   Item 把该实际 Group 显示为 Current。
3. `CurrentGroupID` 无效、对应 Group 已通关或不具备现有入口权限时，回退到归一后的顺序进度 Group，避免 UI 标记
   一个权威入口会拒绝的 Group。Item 内仍保持 `Passed` 高于 `Current`、Boss 使用专属普通表现的既有优先级。
4. 修复只改变 Popup 创建 Item 时的一次离散状态解析；不新增存档写入、逐帧轮询、实例化、资源加载或移动端分支。
   `IWannerGame_ChoiceLevelItem` 中“当前最高可进入”的旧注释已与新行为冲突，因此同步修正为实际 Group 优先、
   顺序进度回退；其它既有注释未改写。

### 验证、剩余风险与回滚

- CodeGraph 确认新增私有解析方法只有 Popup 的旧档归一入口调用，Item 创建仍只有一个 Current Group Id 输入；
  当前没有覆盖该私有逻辑的自动化测试。
- 基于当前 Group 1～13 JSON 执行静态矩阵：`CurrentGroupID=13/UnlockGroups=[13]` 返回 13；
  Group 13 无入口权限时回退 1；Group 5 已通关时回退进度 Group 6；无效 `CurrentGroupID=99` 回退 Group 4，均通过。
- 两个目标脚本的 `git diff --check` 通过，仅输出仓库现有 LF/CRLF 转换提示；
  `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo -v:minimal
  /p:UseSharedCompilation=false /nr:false /clp:ErrorsOnly` 通过，结果为 `0 error / 106 warning`。
- `uncertain`：尚未在 Unity PlayMode、Android/iOS 横屏实测 Group 13 返回选组、通关后下一组、重玩已通关 Group、
  清档、存档失败以及 Current/Passed/Normal/Lock/Boss 的最终美术显隐和点击入口。
- 回滚时删除 Popup 的 `ResolveDisplayedCurrentGroupId()`，恢复归一后直接返回 `progressionGroupId`，并还原 Item 的
  Current 字段/API 注释即可；无需迁移或清理存档，也不影响已保存的 `CurrentGroupID`、`MaxGroupId` 与 `UnlockGroups`。

## Boss 与普通 Group 解锁规则统一（2026-09-22）

### 需求、修改前基线与影响范围

- 用户明确要求 Boss Group 与普通 Group 使用完全一致的解锁逻辑；`IsBoss` 只允许决定专属 Item 美术，不得成为入口权限。
- 修改前存在三处 Boss 特判：Item 把 `IsBoss` 固定解析为 Normal；玩法权威入口把 `group.IsBoss` 直接视为可进入；
  Popup 恢复 `CurrentGroupID` 时也把 Boss 视为具备入口权限。因此未达到顺序进度、未通关、未配置直解锁且未写入
  `UnlockGroups` 的 Boss 仍可绕过锁定。
- 本次只修改 `IWannerGame_ChoiceLevelItem.cs`、`IWannerGame_ChoiceLevelPopup.cs` 和 `IWannerPlayCtrl.cs`；不修改
  Boss/普通 Item Prefab、Luban/JSON 配置、存档结构、资源地址、全局事件、广告 Placement、Analytics 或平台分支。

### 修改后 UI、权限与存档流

1. `IWannerLevelSort.IsBoss` 只用于 Popup 选择 `bossItemPrefab`；Boss Item 继续保留专属美术。
2. Boss 与普通 Item 统一按 `PassedLevels` → 当前顺序/实际 Group → `LockType == Unlock` 或 `UnlockGroups` → Lock
   的顺序解析显示状态。Boss 不再固定显示 Normal。
3. 锁定 Boss 点击后与普通 Group 相同：`LockType == Ad` 时请求既有激励视频，成功后写入 `UnlockGroups` 并进入；
   其它锁类型显示既有 `U_HurryTips_TBC` 提示。
4. `IWannerPlayCtrl.CanEnterGroup()` 在资源加载前统一校验已通关、当前顺序进度、配置直解锁或 `UnlockGroups`；
   不再接受单独的 Boss 豁免。Popup 的 `CurrentGroupID` 恢复校验使用同一组条件，避免 UI Current 与权威入口不一致。
5. 当前本地配置中 Group 15 为 Boss 且 `lockType=Ad`：未达到 Group 15 顺序进度且没有其它解锁证据时保持广告锁；
   Group 14 通关后 Group 15 成为当前顺序 Group，可像普通当前 Group 一样直接进入。

### 验证、剩余风险与回滚

- 全局静态扫描确认运行时代码中 `IsBoss` 只剩 Popup 选择 Boss Prefab 与缺失资源日志，不再参与 Item 状态、
  Current 恢复或玩法入口权限。
- 静态 Prefab 契约检查确认 `IWannerGame_ChoiceILeveltem_Boss.prefab` 是普通 Item Prefab 的 Variant，且普通源 Prefab
  的 `normalState`、`passedState`、`currentState`、`lockState` 均有非空序列化绑定；本次无需修改 Prefab。
- 三个目标脚本的 `git diff --check` 返回 0，仅输出仓库换行格式提示。
- 已尝试 `dotnet build Assembly-CSharp.csproj --no-restore --no-dependencies -m:1 --nologo -v:minimal
  /p:UseSharedCompilation=false /nr:false /clp:ErrorsOnly`；工程当前 `Temp/bin/Debug` 缺少 Apple、Bear、GF、Guru、Unity、
  YooAsset 等依赖 DLL，产生 200 个 `CS0006`，因此不能宣称编译通过，也没有把该结果归因于本次代码。
- `uncertain`：尚未在 Unity PlayMode 和 Android/iOS 横屏验证。需覆盖未解锁 Boss 的 Lock 美术与广告点击、广告失败/取消、
  广告成功写盘并进入、前一 Group 通关后 Boss 成为 Current、Boss 已通关重玩、关闭重开，以及存档失败与晚到广告回调。
- 回滚时在上述三个解析点恢复 `IsBoss` 直接放行即可；本次没有存档迁移、配置或资源改动。
