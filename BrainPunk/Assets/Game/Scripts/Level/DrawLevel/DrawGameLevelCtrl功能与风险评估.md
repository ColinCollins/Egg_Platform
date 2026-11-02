# DrawGameLevelCtrl 功能与风险评估

## 1. 需求与背景

- Draw 选关需要像主线 `LevelCtrl` 一样，在最终关卡序列末尾补充 `20999 / DLevel_999`。
- Draw 专用 RemoteConfig 已拆分为 `drawgame_config`，其 `level_sort` 需要在 Luban 表加载后应用。
- 原临时实现把远端排序和 `20999` 追加放在 `DrawGame_ChoiceLevelPanel.OnCreate()`；面板销毁后再次进入 Draw 会重复构建相同序列，且 UI 承担了配置域职责。

## 2. 影响文件与系统

- `Assets/Game/Scripts/Play/DrawGameLevelCtrl.cs`
- `Assets/Game/Scripts/GameManager.cs`
- `Assets/Game/UI/Loading/Scripts/LoadManager.cs`
- `Assets/Game/UI/LevelChoicePanel/Scripts/DrawGame/DrawGame_ChoiceLevelPanel.cs`
- `Assets/Game/Scripts/Config/ConfigManager.RemoteConfig.cs`
- `Assets/Game/Scripts/SDK/Services/Remote/RemoteConfigService.cs`
- `Assets/Game/Editor/LubanGenerator.cs`
- `Assets/Guru/guru_spec.yaml`
- `Assets/Game/Prefabs/DrawLevel/DLevel_999.prefab`
- Luban `GlobalConst`、`TbDrawLevelData`、`TbDrawLevelSort` 及其生成 JSON。

本次不新增存档字段、全局事件、分析打点、资源地址规则或平台分支。

## 3. 原行为基线

- Draw 选关面板每次创建时直接复制 `TbDrawLevelSort.DataList`。
- 临时实现随后在面板内部解析 `drawgame_config.level_sort`，并追加 `20999`。
- 面板负责分页、选关、存档修复的同时还负责构建全局有效排序；关闭并重新进入 Draw 后会重新执行。
- `TbDrawLevelData` 已存在 `20999 -> DLevel_999`，但当前 `TbDrawLevelSort` 不包含 `20999`。

## 4. 当前行为

- `GameManager.Init()` 创建并持有普通对象 `DrawGameLevelCtrl`，不新增全局单例。
- `LoadManager.LoadConfigAsync()` 先完成 RemoteConfig，再等待 Luban 表初始化；表已就绪后调用 `DrawGameLevelCtrl.Init()`。
- `Init()` 幂等：成功后重复调用直接返回，不重建排序。
- 初始化顺序为：构建并校验本地 `TbDrawLevelSort` 候选 → 运行态按 `drawgame_config.enabled` 应用有效远端 `level_sort` → 保证末尾唯一 `20999`。
- 远端排序存在任一非法类型、重复、未知或空 path 时整份拒绝并保留本地顺序，不使用局部过滤后的半份配置。
- `DrawGame_ChoiceLevelPanel` 只复制控制器的最终只读序列，继续负责分页、选关项创建和 `DB.DrawGame` 进度修复。
- 模块初始化失败只使 Draw 面板拒绝使用半初始化数据，不阻断主线启动。

## 5. 数据、资源与 UI 流

```text
LoadManager
  -> RemoteConfigService 拉取 level_config / drawgame_config
  -> ConfigManager.InitializeAsync 加载 Luban 表
  -> DrawGameLevelCtrl.Init（一次）
      -> TbDrawLevelSort 本地顺序
      -> drawgame_config.enabled / level_sort（非 Editor 包）
      -> TbDrawLevelData 校验远端 path
      -> 追加 20999
  -> DrawGame_ChoiceLevelPanel 创建
      -> 读取 DrawGameLevelCtrl.LevelSorts
      -> GetLevelDataById(path)
      -> 展示、分页并写入 DB.DrawGame 当前进度
```

- `20999` 仍通过 `DrawLevelData.Path = DLevel_999` 进入现有 Draw prefab 加载链。
- 不改变 Draw Reset、胜负、广告回调、资源系统或主线 `LevelCtrl`。

## 6. 风险项

1. `drawgame_config.enabled` 为 `false` 时使用 Luban 本地排序，这是与主线 `LevelCtrl` 一致的门控语义。
2. 编辑器非运行态不应用远端排序；Unity PlayMode 与 Player 运行态均应用同一规则，真实平台表现仍需候选包验证。
3. 当前只静态确认 `TbDrawLevelData` 存在 `20999`；`DLevel_999` 的实际展示、点击、进关和返回仍需 PlayMode 验证。
4. 模块初始化失败不会阻断主线，因此错误会在进入 Draw 时外显为选关面板未就绪；需要结合 Loading 日志排查。
5. RemoteConfig 启动拉取仍可能使用缓存或 Guru 默认值，本次不改变其来源优先级和超时策略。

## 7. 验证证据

- Unity 刷新 `Assembly-CSharp.csproj` 并纳入新脚本后，执行单进程构建，结果为 `0 error / 101 warning`。
- Guru 官方菜单 `Guru/Guru Spec/Auto-Gen <GuruAllSpecs.G.cs>` 已执行，生成代码包含 `drawgame_config` 默认值。
- 静态调用链确认初始化发生在 RemoteConfig 拉取和 Luban 表加载之后、`GameManager.ReadyToPlay()` 之前。
- 静态确认选关面板不再包含 RemoteConfig 解析、`SimpleJSON` 构造或 `20999` 追加逻辑。
- 当前 `guru_spec.yaml.drawgame_config.level_sort` 与 `tbdrawlevelsort.json` 的 99 条 path 顺序一致，末项为 `20099`。
- `uncertain`：Unity PlayMode、多次进入/退出 Draw、远端 enabled=true、无效远端 path、Android/iOS 横屏和 YooAsset 候选包尚未验证。

## 8. 回滚路径

- 删除 `DrawGameLevelCtrl.cs` 及其 `.meta`。
- 从 `GameManager` 移除控制器持有，从 `LoadManager` 移除一次性初始化调用。
- 让 `DrawGame_ChoiceLevelPanel` 恢复直接读取 `TbDrawLevelSort/TbDrawLevelData`。
- 若同时回滚独立远端配置，再恢复 Draw getter 使用 `level_config`，并移除 `drawgame_config` 的 Guru、生成器和 RemoteConfigService 注册。
- 不涉及存档迁移；回滚后已有 `DB.DrawGame` 数据仍可继续使用。

## 9. 2026-08-04：DLevel_999 按最后关通关记录解锁

### 9.1 需求与原行为

- 需求：检测到当前有效 `DrawLevelSort` 中最后一个普通关卡已经通过时，解锁末尾的 `20999 / DLevel_999`。
- 原行为：选关项只检查 `DB.DrawGame.UnlockLevels`；而 Draw 通关查找下一关仍读取原始
  `TbDrawLevelSort.DataList`，该表不含运行时追加的 `20999`，因此已通过最后普通关的存档可能仍显示 999 锁定。

### 9.2 影响范围与当前行为

- 影响脚本：
  - `Assets/Game/Scripts/Play/DrawGameLevelCtrl.cs`
  - `Assets/Game/UI/LevelChoicePanel/Scripts/DrawGame/DrawGame_ChoiceLevelPanel.cs`
- `DrawGameLevelCtrl.TryGetWaitingLevelUnlockPair()` 只从当前最终有效排序中返回 `DLevel_999` 及其前面的最后一个有效关卡，
  不读取或修改存档。
- `DrawGame_ChoiceLevelPanel` 在首次创建和每次打开时检查：若最后普通关的排序 ID 已存在于
  `DB.DrawGame.PassedLevels`，且 999 的排序 ID 尚未存在于 `UnlockLevels`，则补写解锁并调用
  `DB.SaveDrawGame()`。
- 首次创建仍与原有存档修复合并为一次保存；再次打开仅在实际新增解锁记录时保存。
- 不新增存档字段、全局事件、广告路径、配置字段、资源地址、analytics 或平台分支；
  `DLevel_999` 继续使用 `LevelLockType.Level`，未满足条件时仍显示原有 TBC 锁定提示。

### 9.3 数据与 UI 流

```text
DrawGame_ChoiceLevelPanel 创建或打开
  -> DrawGameLevelCtrl 最终有效排序
  -> 定位 20999 及其前一个有效排序项
  -> DB.DrawGame.PassedLevels 检查最后普通关
  -> DB.DrawGame.UnlockLevels 补写 999 排序 ID
  -> DB.SaveDrawGame
  -> 刷新选关项状态
```

### 9.4 验证、风险与回滚

- `git diff --check` 通过。
- `dotnet build Assembly-CSharp.csproj --no-restore`：`0 error / 101 warning`；警告为项目现有告警。
- 静态确认没有新增 `OnValidate`、`Reset`、编辑器生命周期、全局事件或配置/资源修改。
- `uncertain`：尚未执行 Unity PlayMode 的“最后关未通过/已通过、旧存档补写、面板缓存后重开、保存失败”验证；
  非 Editor 远端排序、Android/iOS 横屏和候选包也尚未验证。
- 回滚时删除 `TryGetWaitingLevelUnlockPair()`，并移除选关面板创建、打开时的 999 解锁补写调用；
  无需迁移或清理已有存档，已写入的 `UnlockLevels` 记录会按原有存档继续保留。

## 10. 2026-08-04：最后普通关通关后直接进入 DLevel_999

### 10.1 需求、原因与影响范围

- 需求：DrawGame 通过当前有效排序的最后一个普通关后，胜利面板“下一关”应与主线一致，直接进入
  `20999 / DLevel_999`（TBN Level），不能退回选关页。
- 原因：`DrawGameLevelCtrl.LevelSorts` 已在最终有效排序末尾追加 `20999`，但
  `DrawPlayCtrl.TryGetNextLevel()` 仍读取原始 `TbDrawLevelSort.DataList`；原始表末项为
  `33 / 20039` 且不包含 `20999`，因此最后普通关查不到下一关。
- 影响脚本仅为 `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`；配置、prefab、存档结构、全局事件、
  analytics、广告和平台分支不变。

### 10.2 修复后行为与数据流

- `TryGetNextLevel()` 改为读取已初始化的 `GameManager.DrawGameLevel.LevelSorts`，并通过同一控制器的
  `GetLevelDataById()` 查询下一关数据，避免本地表、远端有效排序和运行时追加项使用不同数据源。
- `RecordVictoryProgress()` 与 `OnEnterNextLevel()` 原本已共同调用 `TryGetNextLevel()`，因此最后普通关
  通关时会同时把 999 排序 ID 写入 `UnlockLevels / CurrentLevel`，点击下一关后加载 `DLevel_999`。
- 其它普通关仍按最终有效排序进入下一项；若控制器未初始化、当前排序项不存在、已是最终项或下一关数据缺失，
  仍保留原有打开 Draw 选关页的降级行为。

```text
最后普通关胜利
  -> DrawPlayCtrl.RecordVictoryProgress
  -> DrawGameLevelCtrl.LevelSorts 定位下一项 20999
  -> DB.DrawGame 写入 PassedLevels / UnlockLevels / CurrentLevel
  -> 胜利面板派发 EnterNextLevelEvent
  -> DrawPlayCtrl.OnEnterNextLevel
  -> 同一最终有效排序取得 20999 -> DLevel_999
  -> TryEnterLevel 加载 Draw prefab
```

### 10.3 验证、剩余风险与回滚

- `git diff --check` 通过，仅有 Git 的 LF/CRLF 工作区提示。
- `dotnet build Assembly-CSharp.csproj --no-restore -p:BuildProjectReferences=false`：
  `0 error / 101 warning`，警告为项目现有告警。
- 静态配置确认：原始 `tbdrawlevelsort.json` 末项为 `33 / 20039` 且不含 `20999`；
  `tbdrawleveldata.json` 存在 `20999 -> DLevel_999`。
- 全依赖构建首次尝试受其它进程占用 `Temp/bin/Debug/Guru.SDK.UIUX.Base.dll` 阻断；随后关闭项目引用构建后，
  `Assembly-CSharp` 本体编译通过。
- `uncertain`：尚未执行 Unity PlayMode 的最后普通关胜利、胜利面板点击下一关、999 实际加载与返回验证；
  非 Editor 远端排序、保存失败降级、Android/iOS 横屏和候选包尚未验证。
- 回滚时只需把 `TryGetNextLevel()` 恢复为读取 `TbDrawLevelSort / TbDrawLevelData`；无需迁移存档或资源。

## 11. 2026-08-05：GM 批量解锁最终有效 Draw 排序

- 新增 `DrawGameLevelCtrl.TryUnlockAllLevels(out addedCount)`，只供运行态 GM/调试入口调用。
- API 遍历 `LevelSorts` 的最终有效排序并去重写入 `DB.DrawGame.UnlockLevels`，因此同时覆盖本地/远端有效顺序及运行时追加的 `20999 / DLevel_999`；不写通关、星级或当前关卡。
- 写入完成后最多执行一次 `DB.SaveDrawGame()`；控制器未就绪或存档缺失时拒绝执行，保存失败时返回 false 并记录 warning。
- C# 构建通过（0 error / 101 warning），差异检查通过；Unity PlayMode、远端排序、保存失败和 Android/iOS 横屏仍为 `uncertain`。
- 回滚只需删除该批量 API，并让 GMPanel 停止调用；无需清理已经写入的合法解锁 ID。完整 GM 行为见 `Assets/Game/Scripts/Play/GM调试关卡控制API功能与风险评估.md`。

## 12. 2026-09-18：RemoteConfig 成功回调刷新 Draw 及风险评估

> 本节保留 2026-09-18 实现时的校验基线；其中“只接受字符串、严格检查仅用于初始化后更新”的限制已由第 13 节替代。

### 12.1 需求、基线与实施范围

- 用户要求先补齐回调更新 Draw，再评估风险。依据 `DevelopMD/RemoteConfigService_Design.md` 第 20.5 节；此前晚到回调只保存原始 JSON，已初始化排序、选关副本与主页未同步。本文第 1–11 节为历史记录，当前运行时刷新以本节为准。
- 修改 8 个游戏脚本：`SDK/Services/Remote/RemoteConfigService.cs`、`GameManager.cs`、`Play/DrawGameLevelCtrl.cs`、`Play/DrawPlayCtrl.cs`，以及 Draw 选关面板/Item、Draw HUD、StartPanel。复用批量缓存保存、已持有面板引用、现有 GameManager.Update 与广告状态；没有新增全局事件、Prefab 引用、资源下载或存档结构。
- 原 `TryGetCurrentLevelData` 用本地排序按显示 ID 查真实数据，远端排序不同或运行时更新后可能得到其它内容。本轮改为接受进入请求时保存真实 DrawLevelData、资源路径、显示 ID 和 generation；所有当前数据消费者读取该上下文，遵守《关卡打点生命周期约定》。仅修改原查询 API 中与事实冲突的“一次性序列”注释及选关 API 调用边界注释。
- 原自动 5 秒 / 后台条件强制 10 秒、首次配置保存后再 Draw Init 的顺序保持；本次没有修改 GuruSDK/Packages/GF。

### 12.2 数据、资源、UI 与生命周期

```text
长期有效成功回调
  -> 已初始化 Draw：构建并校验完整候选
  -> 广告/暂停/进关/Reset 转场中：只保留最新有效 Draw JSON
     （缓存批量写入排除 Draw key，其它 key 可继续提交）
  -> GameManager.Update 在门禁解除后重查
  -> TrySetRawBatch 保存成功
  -> 同一同步段 ApplyRemoteConfig 切换列表/generation
  -> 已打开选关重取副本、修正页码、重新绑定 item/锁/星星
  -> 已打开 StartPanel 刷新 Draw 开关与有效列表总星数
```

- 相同 JSON 不重新保存/刷新；不同 JSON 但同一排序可更新其它 Draw 参数和 UI，排序 generation 不增加。未初始化时仍走原 Loading 路径，不提前创建管理器。
- 无效候选整份拒绝：非布尔 enabled、空/非字符串 level_sort、重复/未知 ID、空资源路径、没有普通关。`20999` 最终唯一并处于末尾。关闭/缺失 enabled 时恢复本地排序。新增严格检查只作用于初始化后的候选，首次 Init 的原有过滤规则仍在。
- 无效候选恢复 latestValidConfigs 的上次有效值；更晚的相同缓存成功回调可撤销旧候选，手动请求超时的缓存回退不能撤销候选。保存 false/异常不切列表、不刷新 UI；不每帧重试失败磁盘写入，下一次有效回调/手动入口才可能再提交。
- 当前 DLevel/HUD、墨水和会话身份不被更新重建；Reset、失败重试、Replay 复用原真实内容。星星与统计仍按真实 ID，通关/解锁/CurrentLevel/MaxLevel 保持按显示序号，无迁移或重新发奖。
- 下一关按新列表中当前显示序号的后一项选择，允许内容变动；列表缩短或当前是 TBC 时回选关。已经接受的目标在转场期间被门禁保护，翻页遮住后才销毁旧关的原流程不变。
- 复查发现新增热更新边界：结算时列表缩短导致没有后项，胜利页随后收到扩展列表，再点下一关时，原 RecordVictoryProgress 未记录这个目标。本轮在受理下一关时补齐该显示 ID 的 CurrentLevel/UnlockLevels，仅有变化才保存；不补通关、星星、MaxLevel 或奖励。真实保存失败沿用现有 warning/继续流程，完整存档故障验证仍待执行。
- 选关广告捕获原 sort/data/generation，广告期间冻结分页/关闭和提交；结果回调先校验版本/真实身份，再按原逻辑解锁进入，finally 释放门禁。HUD 复用现有 `_isRewardAdRequesting`，提示/设置暂停复用 `_isPaused`。新入口不增加编辑器 DB 访问、节点扫描或跨玩法事件。

### 12.3 七维风险评估

评估口径：下表的“事实”来源于本地源码、配置与实际编译方法验证；“风险/推导”不是已经发生的真机故障。未覆盖的运行态必须保持 `uncertain`。

| 维度 | 已查事实与控制 | 剩余风险、级别与验证办法 |
| --- | --- | --- |
| 初始化/依赖 | Service 仅在 Draw.IsReady 后执行新增解析；GameManager 尚未初始化时不调用 Instance；首次 Init 顺序未改。 | 中：初始化前后回调同帧、Editor/Player 不同初始远端策略尚未实测；验证首装、旧缓存启动及 Init 前后各一次回调。 |
| 状态/数据一致性 | 保存成功后无 await 地切序列；generation 拦截旧选择；实际方法测试确认当前内容在重排/移除后保留。 | 中：缓存/序列同步提交不保证后续 UI 方法异常时外观也刷新成功；需验证列表缩短、面板重开、真实结算 ID 和旧回调。序号进度随位置保留是已确认规则。 |
| 外部依赖/平台 | 本次不发额外 Firebase 请求，不改强制预算/SDK；原生覆盖期间门禁保持至回调结束。 | 中、uncertain：无广告结果、系统挂起或转场不结束会让候选一直延后；验证成功/失败/取消/无回调及 Android/iOS 暂停恢复。不要靠新增强制请求补偿。 |
| 业务/生命周期 | 当前数据只在接受进关时捕获，完整 Deactivate 清理；Reset/Replay 不清身份；下一关继续按序号推进；广告 finally 释放。 | 高影响、未见已复现故障：星星/实际 ID 打点依赖这份上下文，必须真机执行重排中 Reset 两次→胜利→Replay，以及胜利页更新后下一关；不能用查询方法测试代替全流程。 |
| 资源/性能/泄漏 | 候选单份、无每帧 JSON 解析（没有候选时立即返回）；不新增订阅/资源加载。15 个缺少本地 Prefab 的 ID（20070–20083、20100）已由用户确认为开发中关卡。 | 开发中资源按用户要求暂不处理、不作为本轮阻断项；不删除配置或补造资源。剩余性能风险为同步解析/存档/UI 刷新占用一帧，移动端耗时未测。 |
| 异常/回退 | 非法排序整份拒绝；TrySetRawBatch 失败恢复旧缓存引用，列表未切换；不循环写盘。 | 中：保存失败后无自动重试，需后续回调/手动重试；旧 JsonSaveProvider 直接写文件，进程崩溃原子性未保证。需注入 Save=false/异常并重启，验证文件与内存。 |
| 可观测/验证 | 编译 0 错误；31 项实际方法断言、17 项静态断言；提交与接受进关有 generation 日志。 | 中：Bear 日志受 DEBUG_MODE 控制，不能当生产包诊断保证；未跑 Unity/SDK/IL2CPP，应用包来源与完整启动/广告日志仍待采集。 |

### 12.4 场景矩阵与证据边界

| 场景 | 目标结果 | 本轮证据 / 未完成项 |
| --- | --- | --- |
| 有效排序、重复更新 | 按候选切换；同排序不增加 generation | 真实方法验证通过；真实缓存保存未执行。 |
| 重复/未知/空 ID、类型错、无普通关 | 拒绝整份，保留活动排序 | 真实方法验证通过；latestValidConfigs 恢复为源码核对。 |
| 关闭远端 enabled | 本地顺序并追加等待关 | 源码核对；真实 ConfigManager/DB 分支未运行。 |
| 保存 false/异常 | 旧缓存/排序保持 | TrySetRawBatch 回滚源码与保存后提交静态断言；故障注入未执行。 |
| 主页/选关正在显示 | 开关、有效总星、页数、item 对齐新列表 | 复用现有引用和刷新链静态核对；Unity 可见效果未验证。 |
| 正在画线，当前 ID 被移除 | 当前身份保持，星星仍记原内容；下一关按新位置规则 | 注入会话字段后真实查询验证通过；画线/星星/打点全流程未运行。 |
| 选关/提示/墨水广告，期间多次更新 | 最新有效候选延后，原奖励不转给新内容 | 绑定/捕获/校验/finally 与广告状态链静态核对；原生成功/失败/取消/无回调未实测。 |
| Reset/Replay/进关已受理 | 暂缓提交，原目标保持；旧关按原遮盖边界销毁 | 进关/重试/暂停门禁真实属性断言通过；Unity 动画和多次重试未执行。 |
| 列表缩短/TBC 后新增内容 | 无有效后项回选关，TBC 不进入新普通关 | TBC 真实方法断言通过，其它下一关路径源码核对；可见回退未验证。 |
| 退出/重启/横屏/系统暂停 | 无旧会话奖励串入，按有效缓存重新初始化 | 退出清理静态核对；Android/iOS/IL2CPP 全部 uncertain。 |

- 编译：项目实际 C# 源文件清单与现有 Unity/项目 DLL 交给 Roslyn，正常定义和去 DEBUG_MODE 的验证定义各编译一次，均 0 错误、1791 警告（1687 条 CS0649；未建立同条件基线，不认定全部为既有）。仅临时输出，不是 Unity 全量导入/Player/IL2CPP 构建。
- 方法验证：反射实际程序集，使用真实 Luban JSON，31 项断言通过；会话字段注入与门禁验证不涉及资源实例、UI 或 SDK。源码断言 17 项通过（含受理下一关重新同步进度）。常规项目构建仍有先前记录的 NETSDK1004 环境缺口，未还原只读依赖。
- 产物位于 `C:/Users/admin/AppData/Local/Temp/brainpunk-draw-remote-refresh-20260918/`：编译响应文件/日志、Program.cs、draw-check.log、StaticCheck.ps1、static-check.log、resource-audit.json、runtime-change.diff。关键源码入口：RemoteConfigService.PersistResolvedConfigs/TryApplyPendingDrawConfig，DrawGameLevelCtrl.TryPrepareRemoteConfig/ApplyRemoteConfig/IsCurrentSelection，DrawPlayCtrl.TryBeginLevelTransition/TryGetCurrentLevelData/TryGetNextLevel。
- 直接结论：已补齐游戏侧 Draw 回调更新链及身份保护，已通过上述编译、方法及静态范围；证据不足以宣布原生广告、落盘、发布资源和当前新版真机全流程无风险。经济价格/其它广告云控/主线/IWanner 的热更新同步不在本次实现范围。

### 12.5 回滚条件与路径

- 若真实关 ID/星星归属错误、广告奖励串关或丢失、转场目标改变、保存失败后配置与排序不同步、刷新后不能选关，停止交付并撤销本节对应的 8 个脚本差异。
- 回滚保留第 30 节的自动 5 秒和条件后台强制 10 秒、GameRoot 跟踪；仅撤销 Draw 候选提交/Update 调用、会话身份与选关版本/广告门禁、主页/HUD 局部 API。不整文件覆盖、不回滚其它任务、无需迁移或清空存档。
- 开发中关卡资源按用户确认排除本轮处理，不因本次评估改变关卡配置或发布范围；Packages/GuruSDK/GF 继续只读。

## 13. 2026-09-22：统一首次初始化与运行时刷新排序校验

### 13.1 问题基线与影响范围

- Android 覆盖安装日志中，Draw 已按旧缓存完成初始化后收到 99 项新 `level_sort`；远端数组使用整数 ID，运行时
  `TryPrepareRemoteConfig()` 只接受字符串 Token，因此候选被拒绝并保留旧的 58 项排序。
- 同日 iOS 日志中的相同整数数组在 Draw 初始化前到达，原 `SyncRemoteConfig()` 通过
  `ToObject<List<string>>()` 自动转换后成功初始化为 99 个普通关加 `20999`。这证明差异来自两条解析路径和回调时序，
  不是 Android/iOS 使用不同业务规则。
- 本次只修改 `Assets/Game/Scripts/Play/DrawGameLevelCtrl.cs`。不修改 `RemoteConfigService`、ForceFetch、Loading
  等待、缓存提交顺序、SDK/Packages/GF、Prefab、Luban 数据、存档、UI、事件或打点。

### 13.2 统一后的行为与数据流

- 本地 Luban 初始排序、初始化阶段远端配置和初始化后的运行时刷新最终都使用
  `TryBuildRemoteConfigCandidate()` / `TryBuildLevelSort()` 这一套候选构建与校验逻辑。
- `level_sort` 接受 JSON 整数或字符串 ID，并统一规范化为字符串；整数、字符串和两者混合的合法配置生成相同序列。
- 小数、布尔、null、对象、空数组、空 ID、重复 ID、未知关卡、空资源路径和只有 `20999` 的候选仍整份拒绝；
  `20999` 只保留一次并固定追加在末尾。
- 初始化时先构建并验证本地候选；远端候选无效则保留本地有效序列。运行时刷新无效仍由既有提交链保留当前缓存和活动序列。
  两个生命周期只保留失败回退对象不同，不再使用不同的 JSON 解析和关卡合法性规则。

### 13.3 验证、剩余风险与回滚

- 使用已有 Unity 真实引用响应文件重新编译当前完整游戏源码，DEBUG_MODE 与去 DEBUG_MODE 两次均为 `0 error / 1799 warning`；
  未建立同条件警告基线，不能把警告全部认定为既有。
- 反射调用实际编译代码的 20 项断言通过：当前 99 项整数线上格式在初始化前候选和运行时候选中均生成相同的
  100 项序列；99 项字符串格式结果一致；整数/字符串混合可规范化；上述非法类型、重复、未知和空候选全部拒绝。
- 静态断言确认首次同步与运行时准备均调用共同构建器，旧 `TryGetValue<List<string>>()` 与逐项过滤路径已移除；当前本地
  99 项排序 ID 为连续 `1..99`，重建后显示序号保持一致。
- 常规 `dotnet build Assembly-CSharp.csproj` 因 `Temp/bin/Debug` 缺少 200 个 Unity/插件依赖 DLL 而未进入有效代码编译；
  本轮未执行 Unity 导入、PlayMode、IL2CPP、覆盖安装或 Android/iOS 真机回调验证，平台结果仍为 `uncertain`。
- 验证产物位于 `C:/Users/admin/AppData/Local/Temp/brainpunk-draw-sort-unified-20260922/`。若真机出现有效配置被拒绝、
  排序数量/顺序错误或初始化失败，回滚本节对应的 `DrawGameLevelCtrl.cs` 差异即可；无需清存档、回滚远端值或修改 SDK。
