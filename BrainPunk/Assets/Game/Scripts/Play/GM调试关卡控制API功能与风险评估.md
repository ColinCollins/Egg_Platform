# GM 调试关卡控制 API 功能与风险评估

## 2026-10-14 Dig GM 能力接入：修改前核对

- 需求与基线：用户要求 GM 全部解锁、胜利、上一关、下一关支持 Dig。GMPanel 已显式绑定四个按钮；前三项关卡操作由 GMPanel.PlayController 经 ActiveController as IGMFeature 分派，DigPlayCtrl 尚未实现接口。DigGameLevelCtrl.TryUnlockAllLevels 已具备当前有效排序批量补权限、最多一次保存的能力，但 GMPanel.UnlockAllLevel 仅调用 Main／Draw／IWanner。
- 来源与原链：IGMFeature、GMPanel.PlayController、GMPanel.UnlockAllLevel、DigPlayCtrl.TryEnterLevel／BeginTransition／OnResolved、DigLevelCtrl.BeginVictory、DigGameLevelCtrl.LevelSorts／TryUnlockAllLevels。已核对本模块历史记录、DevelopMD/GMPanel_Reference.md、四玩法打点生命周期约定及 GM Prefab 绑定；知识库只有历史任务线索，CodeGraph CLI／MCP 不可调用，按本地实现核对。
- 拟改位置：GMPanel 全解锁追加现有 Dig API 及结果日志；DigPlayCtrl 实现 IGMFeature，运行态普通关胜利调用 DigLevelCtrl 的 GM 胜利入口，由原 BeginVictory 延迟／唯一结果链提交。TBC 拒绝胜利。上下关以进入时内容 ID 在当前有效排序中定位相邻项，边界不循环、移除当前内容／目标配置缺失时拒绝；GM 可解锁被切换的目标，但不写跳过关卡的通关、星级或成功次数。
- 生命周期与范围：只在初始化、活动、可操作、非暂停／后台、非转场／广告／结果锁定时接受。切换复用 TryEnterLevel，旧尝试 exit、新尝试 start、会话资格重置及翻页遮挡后的旧实例清理照旧；受理后记录 Dig 当前目标关号并结束旧 OneLink 来源，使 GM 新目标按普通关处理。首次／末尾／TBC 上下关仅按有效排序边界处理，不按排序 ID 加减，也不跳过缺少资源的配置项。
- 维护边界：只改手写 GMPanel、DigPlayCtrl、共享 DigLevelCtrl；不改 GM 路由 partial、Generated、Prefab、主线／Draw／IWanner 行为、SDK、云控、配置表或存档结构。TryEnterLevel 原“仅 OneLink 可解锁”注释随新增 GM 调用定向更新，其余旧注释不变。批量解锁的保存失败沿原 API 返回 false 且保留本次内存权限；GM 切换保存失败沿既有日志处理，不另造事务／重试。
- 验证计划：检查四玩法全解锁聚合及失败报告、Dig 批量幂等／单次保存、胜利自然结果链／重复／暂停／TBC门禁、两方向相邻切换／重排／首尾／缺配置／无活动关／暂停／转场／广告／结果锁定、OneLink 清理、切换只解锁目标不伪造成功；Editor／Android 条件编译及定向 diff。运行时／资源／SDK 使用的真实部分与替身分别记录，完整生产 HUD、广告、资源包、重启持久化及双端真机为 uncertain。
- 回滚：定向撤上述三个脚本的 GM 增量及新增普通关胜利入口；保留前一轮选关面板更名和用户美术修改，不清理玩家存档。

- 增量前置核对：发现选关 Item 的点击事件由 SetData 缓存；仅写解锁权限会让已打开列表暂时保留广告按钮。确认现有 DigGame_ChoiceLevelPanel.RefreshRemoteConfig 只重读当前有效数据并刷新 Item，不拉远端、不建面板；SetData 会先清旧点击绑定再绑定新状态。GM 解锁成功后复用该刷新入口；面板关闭／广告占用时沿其既有门禁延后，不改配置提交链。验证补充刷新调用与保存失败不误报刷新。

### 最终行为与验证

- 全解锁覆盖 Main／Draw／IWanner／Dig；Dig 使用当前有效排序，包含 TBC，只补 UnlockLevels，有变动时一次保存，成功后刷新现存选关界面的状态及点击绑定。任一玩法失败仍汇总 incomplete，其它已成功操作保留。
- Dig 胜利由 IGMFeature → DigPlayCtrl.TryForceVictory → DigLevelCtrl.TryForceVictory → BeginVictory／原延迟 → Resolved → 原成功统计／插屏／星级／胜利面板链处理。未获得提示也能使用 GM；不修改自然吞噬／到达条件，TBC 拒绝。上下关使用当前内容 ID 在最新排序中的相邻项，GM 可以进入锁定目标并保存当前关号，结束旧 OneLink 来源；不把被跳过关卡写为通过。首尾不循环，TBC 可返回最后普通关。
- 三项关卡命令拒绝未初始化／未激活、选关无运行时、暂停、普通关后台暂停、加载转场、广告占用和结果锁定。旧关沿 TryEnterLevel 先 exit 后新关 start，翻页完全遮挡后由原 BuildLevelAsync 清理旧实例／创建新关；关内提示及奖励墨水资格按原新关入口清除。
- Unity 6000.0.59f2 隔离 PlayMode **38 项通过**：实际 GM 路由、从生产逐字抽取的 GM 方法／TryEnterLevel／批量解锁方法，以及真实 DigLevelCtrl 的胜利锁与延迟参与。持久化、配置查询、完整控制器切换／UI 刷新、OneLink、统计与资源加载使用替身，因此只能证明命令分派、边界和调用次序，不能证明生产广告／保存／加载端到端。首轮 TEMP 夹具字段声明语法错误已修正，最终测试日志无该错误；未因此改生产代码。
- 1223 份生产源码条件编译：Editor **0 错误／113 警告**，Android **0 错误／118 警告**；定向 diff 检查通过。测试与日志见 [GM Dig 验证记录](../../../../DevelopMD/验证记录/DigGM_2026-10-14/验证记录.md)。原 GMPanel.PlayController、Generated、Prefab、主线／Draw／IWanner 和 Analytics 源码未修改。
- 剩余风险为 `uncertain`：生产列表视觉刷新／真实点击、存档重启、DeepLink 胜利面板、翻页与资源缺失后的恢复、原生广告和双端真机未复测。目标配置存在但资源缺失时沿原加载失败返回流程，不新增自动跳过。最短现场验收为：打开 Dig 选关点击全解锁、进入任一普通关执行上一关／下一关／胜利，再检查首关与 TBC；真机在转场／广告期间确认 GM 不接收重复命令。

## 1. 需求与原行为基线

- GMPanel 的“解锁全部关卡”原本只遍历主线 `LevelCtrl.LevelSorts`，并逐关调用 `UnlockLevel()`；每新增一关都会立即保存一次，DrawGame 不受影响。
- GMPanel 的“直接胜利”原本固定派发主线 `SwitchGameStateEvent(SUCCESS)`；DrawGame 不订阅该事件，因此活动控制器为 DrawPlayCtrl 时不会结算。
- 本次需求是同一组 GM 功能同时支持 Main 与 Draw，且不绕过两种玩法各自的排序与结算职责。

## 2. 影响范围

- `Assets/Game/UI/GM/Scripts/GMPanel.cs`
- `Assets/Game/Scripts/Play/LevelCtrl.cs`
- `Assets/Game/Scripts/Play/DrawGameLevelCtrl.cs`
- `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`
- 主线存档 `DB.GameData.UnlockLevels`
- Draw 存档 `DB.DrawGame.UnlockLevels`

本次未修改 prefab、Luban 表、RemoteConfig、资源地址、存档结构、全局事件定义、广告配置、analytics 接口或平台分支。

## 3. 新行为与数据流

### 3.1 批量解锁

- `LevelCtrl.TryUnlockAllLevels(out addedCount)` 遍历主线最终有效排序，去重补入 `DB.GameData.UnlockLevels`，全部写入后最多保存一次。
- `DrawGameLevelCtrl.TryUnlockAllLevels(out addedCount)` 遍历初始化后的最终有效排序，因此覆盖本地/远端排序结果和运行时追加的 `20999 / DLevel_999`；全部写入后最多调用一次 `DB.SaveDrawGame()`。
- 两个 API 只增加解锁记录，不写 `PassedLevels`、星级、当前关卡或通关统计。GMPanel 的 Unlock 按钮依次调用两者并记录新增数量。

```text
GMPanel.UnlockAllLevel
  -> LevelCtrl.TryUnlockAllLevels
     -> DB.GameData.UnlockLevels -> 单次 Save
  -> DrawGameLevelCtrl.TryUnlockAllLevels
     -> DB.DrawGame.UnlockLevels -> 单次 SaveDrawGame
```

### 3.2 按活动玩法直接胜利

- 活动控制器为 Main 或尚未建立活动控制器时，GMPanel 保留既有 `SwitchGameStateEvent(SUCCESS)` 主线链。
- 活动控制器为 DrawPlayCtrl 时，GMPanel 调用 `TryForceVictory(nameof(GMPanel))`，不再派发主线胜利事件。
- Draw 仅在已初始化、已激活、未暂停、非加载/过渡、阶段为 `Drawing` 且当前 `IDrawLevelRuntime` 就绪时接受。
- 接受后通过 `IDrawLevelRuntime.TryResolve(Success)` 原子提交，继续进入现有 `OnLevelResolved()`：成功插屏策略、每日挑战、成功统计、星级与通关进度保存、胜利面板均保持原链。
- 选关页、加载中、暂停中、成功演出已占用或已结算时返回 false，避免重复结算。

## 4. 验证证据

- `git diff --check` 已通过，未发现新增空白错误。
- `dotnet build .\Assembly-CSharp.csproj --no-dependencies --no-restore --nologo -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：构建成功，0 个错误、101 个项目既有警告。
- 静态确认没有新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode` 或全局事件。
- 静态确认 Draw 强制胜利使用关卡运行时的一次性 `TryResolve()` 门禁，没有直接调用胜利面板或手工写通关存档。

## 5. 剩余风险与回滚

- `uncertain`：尚未在 Unity PlayMode 验证 Main/Draw 全解锁后选关列表立即刷新、关闭重开和重启后的持久化结果。
- `uncertain`：尚未验证 Draw 直接胜利在普通 Drawing、暂停、加载、成功演出占用、重复点击、每日挑战弹窗、成功插屏和保存失败场景下的表现。
- 主线 `GameData.Save()` 没有返回值，GM API 无法直接报告落盘失败；Draw 保存失败时 API 返回 false，但本次新增的内存解锁记录会保留到本次运行结束。
- 尚未执行 Android/iOS 横屏调试包触摸 smoke；本次没有新增平台分支。
- 回滚时移除两个 `TryUnlockAllLevels()`、`DrawPlayCtrl.TryForceVictory()`，并把 GMPanel 恢复为主线逐关解锁和固定派发主线胜利事件；无需迁移或清理已有存档。

## 6. 2026-08-10：IWanner 上一小关与当前小关直接成功

### 6.1 需求、原行为基线与影响范围

- 原 `GMPanel.LastLevel()` 固定修改 `PlayCtrl.Level`，即使活动玩法是 IWanner，也会操作主线当前关卡。
- 原 `GMPanel.SendVictoryEvent()` 只特殊处理 Draw；IWanner 会落入主线 `SwitchGameStateEvent(SUCCESS)`，无法通过当前 IWanner 小关。
- 本次新增 `Assets/Game/Scripts/Play/Controller/IGMFeature.cs` 及其 `.meta`，并影响
  `PlayCtrl.cs`、`DrawPlayCtrl.cs`、`IWannerPlayCtrl.cs`、`GMPanel.cs` 和独立的
  `GMPanel.PlayController.cs/.meta`。未修改 GMPanel prefab、生成字段、Luban、资源地址、存档结构、广告、
  analytics 或平台分支。

### 6.2 修改后行为与职责拆分

- `IGMFeature` 统一声明 `TryEnterNextLevel()`、`TryEnterPreviousLevel()` 和 `TryForceVictory(source)`；新增玩法
  Controller 只有实现该接口才会获得 GMPanel 的关卡调试能力。
- GMPanel 的三个按钮方法迁入独立 partial 文件，只解析 `ActiveController as IGMFeature` 并转发命令，不再引用
  `PlayCtrl`、`DrawPlayCtrl` 或 `IWannerPlayCtrl` 的具体类型；没有活动控制器时明确拒绝，不回退到其它玩法。
- `PlayCtrl` 实现三项能力，继续使用原 `SetCurrentLevel + EnterLevelEvent` 和 `SwitchGameStateEvent(SUCCESS)`；
  `DrawPlayCtrl` 只支持既有 `TryForceVictory()`，上下关明确返回 false；IWanner 支持上一小关和当前小关胜利，
  下一小关明确返回 false。返回 false 时 GMPanel 只记录当前 Mode 与命令，不得改写其它玩法状态。
- IWanner 的 `TryEnterPreviousLevel()` 将目标索引设为当前索引减一，先通过
  `IWannerGameLevelCtrl.TryGetLevelData()` 校验配置，再沿用黑幕渐显、销毁旧关卡/HUD、创建新关卡/HUD、
  黑幕渐隐和恢复输入的既有重建链。第一小关、加载中或运行时无效时返回 false，不修改索引。
- IWanner 的 `TryForceVictory(source)` 不直接修改小关索引，而是调用当前 `IWannerLevelCtrl.TrySuccess()`；因此继续复用死亡/暂停/
  重复结算门禁、角色停靠成功门、进门表现和 `HandleCurrentLevelSuccessAsync()` 组内推进链。

```text
GMPanel Next/Last/Victory
  -> ActiveController as IGMFeature
     -> Main: 上一关 / 下一关 / 当前关胜利
     -> Draw: 上下关返回 false / 当前关胜利
     -> IWanner: 下一关返回 false / 上一小关 / 当前小关胜利

IWanner TryEnterPreviousLevel
  -> 校验上一索引配置 -> ReloadCurrentLevelAsync

IWanner TryForceVictory
  -> IWannerLevelCtrl.TrySuccess
  -> 既有进门表现与组内推进链
```

- 上一小关和 GM 成功均不新增 DB 写入、奖励、Group VictoryPanel、全局事件定义或 IWanner analytics；当前
  IWanner 本身尚未建立正式 attempt/通关存档，本次不伪造这些未实现职责。

### 6.3 验证、剩余风险与回滚

- `Assembly-CSharp.csproj` 已收录新增接口与 partial；定向构建结果为 `0 error / 101 warning`，warning 属于
  工程既有基线。
- CodeGraph 静态确认 GMPanel 三个按钮只调用 `IGMFeature`，不再出现具体 Controller 类型分支。
- 静态确认 IWanner 上一小关在目标配置解析成功前不修改当前索引；切换接受后立即关闭 HUD/关卡输入并开启
  `GamePlayPanelSwitchBlockEvent`，随后复用现有 activation version 过滤旧异步结果。
- 静态确认 IWanner GM 成功只调用 `TrySuccess()`，没有直接调用成功回调、直接创建 VictoryPanel、手工写存档或
  派发主线 SUCCESS。
- `uncertain`：尚未在 Unity PlayMode 验证第 1 小关拒绝、2～5 小关逐级回退、黑幕交接、加载失败回退、
  暂停/死亡/重复胜利拒绝、末关成功后的再操作，以及 Android/iOS 横屏调试按钮触控。
- 回滚时删除 `IGMFeature.cs/.meta`，从三个 Controller 移除接口实现，把按钮逻辑恢复为具体玩法分流；若同时
  回滚本轮 IWanner 能力，再移除 `TryEnterPreviousLevel()` 与 `TryForceVictory(source)`。不涉及存档或资源迁移。

## 7. 2026-08-19：全关解锁补充 IWanner 全部 Group

### 7.1 需求、原行为基线与影响范围

- 原 `GMPanel.UnlockAllLevel()` 只调用主线 `LevelCtrl.TryUnlockAllLevels()` 与 Draw
  `DrawGameLevelCtrl.TryUnlockAllLevels()`；IWanner 的 `DB.IWannerGame.UnlockGroups` 没有被 GM 按钮写入。
- 本次需求是点击同一“解锁全部关卡”按钮时，同时解锁当前 IWanner 配置中的全部 Group。
- 影响文件为 `Assets/Game/UI/GM/Scripts/GMPanel.cs` 与
  `Assets/Game/Scripts/Play/IWanner/IWannerGameLevelCtrl.cs`。未修改 IWanner 配表、独立存档结构、
  `PassedLevels`、`MaxGroupId`、当前 Group/小关、奖励、prefab、资源、全局事件、analytics 或平台分支。

### 7.2 修改后行为与数据流

- `IWannerGameLevelCtrl.TryUnlockAllGroups(out addedCount)` 遍历 Loading 后冻结的非空 Group 配置，按 Group Id
  幂等补入 `DB.IWannerGame.UnlockGroups`，全部写入后最多调用一次 `DB.SaveIWannerGame()`。
- 该 API 只增加显式解锁权限；不把 Group 伪造成已通关，也不解锁依赖最后一组通关记录的 TBN Item。
- `GMPanel.UnlockAllLevel()` 依次执行 Main、Draw、IWanner 三个批量 API；完整与失败日志均包含 IWanner
  成功状态或新增数量。任一控制器未就绪或保存失败时记录 incomplete，但其它已经成功的玩法不会回滚。

```text
GMPanel.UnlockAllLevel
  -> LevelCtrl.TryUnlockAllLevels
  -> DrawGameLevelCtrl.TryUnlockAllLevels
  -> IWannerGameLevelCtrl.TryUnlockAllGroups
     -> DB.IWannerGame.UnlockGroups
     -> DB.SaveIWannerGame（有变化时一次）
```

### 7.3 验证、剩余风险与回滚

- `Assembly-CSharp.csproj` 定向构建通过：`0 error / 106 warning`，warning 属于当前工程既有范围。
- 静态确认 IWanner 批量 API 只写 `UnlockGroups`，没有写 `PassedLevels`、`MaxGroupId`、当前进度、奖励或 TBN 权限。
- `uncertain`：尚未在 Unity PlayMode 验证全新存档、部分已解锁存档、重复点击、保存失败、选组界面关闭重开与
  杀进程重启后的显示/持久化结果，也未执行 Android/iOS 横屏调试按钮触控 smoke。
- IWanner 保存失败时新增 Group 仍保留在本次运行的内存存档中，API 返回 false 并由 GMPanel 记录 incomplete；
  Main/Draw 已成功保存的数据不会事务性回滚。
- 回滚时移除 `TryUnlockAllGroups()` 及 GMPanel 的 IWanner 调用和日志字段；已有 `UnlockGroups` 存档记录不会自动清理，
  不需要迁移其它玩法、配置或资源。
