# Level999 点击跳转延迟功能与风险评估

## 2026-08-07：进场 2 秒后开放 DrawGame 跳转

### 需求与原行为基线

- 需求：主线 `Level_999` 进场后等待 2 秒，才允许点击关内入口跳转 DrawGame。
- 追加需求：第一次有效点击触发跳转后，本次关卡实例内禁止再次触发跳转。
- 修改前，`Level_999.prefab` 的 `enterDrawGame` 点击事件直接调用
  `Level999Ctrl.EnterDrawGameAtHighestPlayableLevel()`；该公开入口仅检查运行态和 DrawGame 数据，进场后可立即触发。
- 首次延迟改动只限制进场前 2 秒，门禁开放后的多次点击仍会重复进入 DrawGame 跳转链。
- 关联的碰撞入口 `EnterDoorAnim()` 会先播放销毁、震动及合计 2 秒的等待，再调用同一跳转入口。

### 影响文件、资源与系统

- 修改：`Assets/Game/Scripts/Level/MainLevel/Level_999/Level999Ctrl.cs`。
- 新增：本功能与风险评估记录。
- 只读核对：`Assets/Game/Prefabs/Level/Level_999.prefab` 的点击 UnityEvent 仍绑定
  `Level999Ctrl.EnterDrawGameAtHighestPlayableLevel()`；Prefab 未修改。
- 复用既有 `UniTask`、组件销毁取消令牌、`GameManager` DrawGame 进入链和 Bear Logger；未新增全局事件、
  配置、资源地址、存档字段、analytics、广告、IAP、RemoteConfig 或平台分支。

### 修改后行为与调用流

1. `Level999Ctrl.Start()` 启动本关局部 2 秒计时，初始不允许触发 DrawGame 跳转。
2. 计时使用组件销毁取消令牌；关卡在 2 秒内退出或销毁时，不会在失效对象上开放门禁。
3. 计时完成后开放门禁；之前调用 `EnterDrawGameAtHighestPlayableLevel()` 会直接返回，不排队、不播放跳转。
4. 门禁开放后第一次有效调用先解析当前可玩的最大 Draw 关卡；解析成功后立即设置本关实例的一次性触发锁，
   再退出主线控制器并进入对应 Draw 关卡。
5. 一次性触发锁设置后，当前关卡实例内的后续调用全部直接返回；重进关卡产生新实例后才重新计时并恢复一次触发机会。
6. 目标 Draw 关卡解析失败时只记录既有错误且不消费一次性触发锁，避免初始化未就绪造成永久不可用。
7. `EnterDoorAnim()` 自身已有合计 2 秒的表现等待，最终仍经过同一门禁；点击入口与碰撞入口不会绕开延迟或一次性锁。

### 数据、资源、UI、事件与平台边界

- 延迟状态只存在于当前 `Level999Ctrl` 实例内，重进关卡会重新计时，不写入 DB 或存档。
- 一次性触发状态同样只存在于当前 `Level999Ctrl` 实例内；未把共享 `ClickItemEventHandle.maxClickCount` 改为 1，
  因此 2 秒门禁内的误触不会提前消耗唯一一次有效触发机会。
- 点击组件、Prefab 接线、Draw 解锁数据读取和关卡资源加载流程保持不变。
- 门禁计时不使用 `Update`，只创建一次异步等待；对 Android/iOS 横屏触控没有新增输入分支。
- 未修改关卡开始、结束、Reset、Replay、成功/退出清理或 analytics 生命周期。

### 风险评估

| 风险 | 等级 | 当前控制 | 未验证缺口 |
| --- | --- | --- | --- |
| 进场后误触导致立即跳转 | 低 | 公开跳转入口统一检查 2 秒门禁 | Unity PlayMode 真实快速点击待验证 |
| 关卡销毁后异步回调写入失效对象 | 低 | 使用 `destroyCancellationToken` 并抑制取消异常 | 快速退出/切场景 PlayMode 待验证 |
| 2 秒边界帧的点击手感 | 低 | 计时完成后才写入准许状态，早到调用不排队 | Android/iOS 真机触控边界待验证 |
| 合法点击后重复点击引发重复进入 | 低 | 成功解析目标后立即设置一次性锁，后续调用统一返回 | Unity PlayMode 连点与多点触控待验证 |
| 目标已解析但控制器切换失败后无法重试 | 中 | 按需求保留一次性锁，避免重复切换副作用 | 需在 PlayMode 验证异常回退表现 |

### 验证记录

- 静态代码核对：点击 UnityEvent 的目标公开入口已统一增加门禁；门禁只在本关 `Start()` 后异步开放。
- 静态顺序核对：目标 Draw 关卡解析成功后先写入 `_hasTriggeredDrawEntry`，再调用 `TryEnterDraw()`；
  后续调用在读取 `GameManager`、DB 或执行控制器切换前返回。
- 静态资源核对：`Level_999.prefab` 的点击绑定与碰撞绑定未修改。
- `git diff --check` 对已跟踪目标文件通过，仅有工作区 LF/CRLF 转换提示。
- 首次完整依赖构建与工作区另一构建进程争用 `Temp/obj` 输出而出现 `CS2012` 文件锁；随后使用
  `BuildProjectReferences=false` 定向构建 `Assembly-CSharp.csproj` 通过，结果为 `0 error / 101 warning`。
- 追加一次性锁后重新使用 `BuildProjectReferences=false` 定向构建 `Assembly-CSharp.csproj`，结果为
  `0 error / 0 warning`。
- `uncertain`：尚未执行 Unity PlayMode 的 2 秒前连续点击、2 秒后双击/多点触控、控制器切换失败、快速退出，
  以及 Android/iOS 横屏真机验证。

### 回滚路径

- 从 `Level999Ctrl` 删除 `DrawEntryEnableDelay`、`_canEnterDrawGame`、`EnableDrawEntryAfterDelay()`、
  `Start()` 中的计时调用和公开跳转入口的门禁判断，即可恢复进场后立即允许跳转的旧行为。
- 仅回滚一次性触发限制时，删除 `_hasTriggeredDrawEntry` 字段、入口条件中的对应判断及目标解析后的赋值，
  即可保留 2 秒进场门禁并恢复多次调用行为。
- Prefab、配置、存档和资源未迁移，无需额外回滚。

## 2026-08-10：DrawGame 未开放时隐藏关内入口

### 需求与原行为基线

- 需求：`IsDrawGameOpen` 对应的 DrawGame 功能配置未开启时，隐藏主线 `Level_999` 内的跳转入口。
- 修改前，StartPanel 已使用 `ConfigManager.RemoteConfig.GetDrawGameIsOpen()` 隐藏首页 Draw 入口；
  `Level_999.prefab` 的 `enterDrawGame` 始终激活，点击或碰撞仍可进入 Draw 跳转链。
- 该 getter 优先读取 `drawgame_config.drawGameIsOpen`，字段缺失时回退
  `GlobalConst.DrawGameIsOpen`；本次沿用同一配置口径，不新增开关。

### 影响文件、资源与系统

- 修改 `Level999Ctrl.cs`：初始化入口可见性，并在点击与碰撞公开入口增加配置防护。
- 修改 `Level_999.prefab`：把现有完整 `enterDrawGame` 节点绑定到 `drawGameEntry`。
- 未修改 RemoteConfig、GlobalConst、DB、存档、关卡开始/结束、打点、资源加载、广告或平台 SDK。

### 修改后行为与调用流

1. `Level999Ctrl.Start()` 读取一次 DrawGame 功能开关并同步 `enterDrawGame` 节点显隐。
2. 开关关闭时入口节点失活，不启动原有 2 秒开放计时；关卡其它成就和进入逻辑保持不变。
3. 点击公开入口与碰撞入口都会再次读取同一开关，防止外部 UnityEvent 或运行期配置状态绕过隐藏门控。
4. 开关开启时保持原行为：入口显示，进场 2 秒后才允许首次有效跳转。

### 验证、风险与回滚

- 静态确认 prefab 绑定指向 `enterDrawGame` 完整根节点，失活后其点击组件与碰撞组件一并停止响应。
- 静态确认关闭分支不会启动 2 秒计时，也不会进入 `GameManager.TryEnterDraw` 链。
- 使用 `BuildProjectReferences=false` 定向构建 `Assembly-CSharp.csproj` 通过：`0 error / 101 warning`；
  warning 为工程现有项。
- `uncertain`：尚未在 Unity PlayMode 验证开关开/关两种启动状态、远端缺失回退和 Android/iOS 横屏表现。
- 回滚时删除 `drawGameEntry` 字段及 prefab 绑定、`Start()` 显隐分支和两个公开入口的配置判断即可；
  原有 2 秒门禁、一次性锁、配置与存档无需迁移。

## 2026-08-18：Level_999 TBC 传送门跳转打点

### 需求与原行为基线

- 用户提供的点位规格要求上报 `enter_tbc_portal`，必选参数 `type` 为 `int`：`0=main`、`1=draw`、`2=iwannar`。
- 修改前，`GameSDKService.Game.cs` 中已有空的 `EnterTBCPortal()` 占位 API，不会生成事件；主线
  `Level999Ctrl.EnterDrawGameAtHighestPlayableLevel()` 成功进入 Draw 关卡请求后也没有 TBC 传送门打点。
- 本次需求只补齐主线 `Level_999` 当前已经存在并可执行的关内传送跳转，不把 Draw/IWanner 选关界面的 TBC Item 点击
  视为关内传送门，也不改动工作区中尚未形成有效脚本接线的其它 `Level_999` 资源。

### 影响文件与事件流

- `Assets/Game/Scripts/SDK/Services/Events/GameSDKService.Game.cs`：`EnterTBCPortal(int type)` 使用
  `Dictionary<string, object>` 写入整数 `type`，再调用 `LogEvent("enter_tbc_portal", data)`。
- `Assets/Game/Scripts/Level/MainLevel/Level_999/Level999Ctrl.cs`：主线入口在
  `GameManager.TryEnterDrawLevel(...)` 返回 `true` 后，调用
  `GameSDKService.Instance.EnterTBCPortal((int)Game.Play.PlayModeType.Main)`，最终上报 `type=0`。
- 事件边界为“目标 Draw 进关请求已被当前 DrawPlayCtrl 接受”，不是资源加载完成回调；2 秒门禁前点击、功能开关关闭、
  重复触发、目标解析失败、控制器切换失败和目标进关请求拒绝都不上报。
- 既有 `_hasTriggeredDrawEntry` 一次性锁继续保证当前 `Level999Ctrl` 实例最多进入一次有效跳转链；本次没有新增计数、
  DB、存档、配置、资源、UI、全局事件、广告、IAP 或 RemoteConfig 修改。
- 本事件独立于 `level_start` / `level_end` 生命周期，不改变主线退出事件、Draw 开始事件、revive、`play_count`、
  `start_type` 或跨玩法累计口径。

### 风险、验证与未验证缺口

| 风险 | 等级 | 当前控制 | 未验证缺口 |
| --- | --- | --- | --- |
| 无效点击或失败跳转误计入 | 低 | 只在 `TryEnterDrawLevel(...)` 返回 `true` 后上报 | PlayMode 故障注入待验证 |
| 双击/碰撞与点击并发导致重复上报 | 低 | 复用目标解析后的 `_hasTriggeredDrawEntry` 一次性锁 | PlayMode 多点触控待验证 |
| `type` 口径与点位表不一致 | 低 | 复用数值顺序相同的 `PlayModeType.Main` 并显式转为 `int` | 真实 SDK 后台字段类型待验证 |
| SDK 上报时序早于资源实际加载完成 | 中 | 文档明确事件表示“进关请求已接受”，不宣称资源加载成功 | 数据侧是否要求加载完成口径待确认 |

- `git diff --check` 已通过，只有工作区既有 LF/CRLF 转换提示。
- 定向执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly
  /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`，结果为
  `0 error / 109 warning`；warning 为工程现有范围。
- 静态检索确认当前仅有一个运行时调用点，API 发送的事件名、字段名与整数值均符合用户截图。
- `uncertain`：尚未执行 Unity PlayMode 的有效跳转/失败跳转/快速连点验证，也未在 GuruSDK 真实后台、Android/iOS
  横屏真机确认事件到达、字段类型与时序。

### 回滚路径

- 删除 `Level999Ctrl` 成功分支中的 `EnterTBCPortal(...)` 调用即可停止主线 `Level_999` 上报。
- 若同时撤销 API，删除 `GameSDKService.Game.cs` 中 `EnterTBCPortal(int type)`；不涉及存档、配置或资源迁移。

## 2026-08-25：Draw 目标改为排序最小的已解锁未通关关卡

### 需求、原行为与影响范围

- 需求：主线 `Level_999` 传送门改为进入 Draw 最终有效排序中最前的已解锁且未通关关卡。
- 修改前，`TryGetHighestPlayableDrawLevel()` 从 `LevelSorts` 末尾反向扫描，选择最大的已解锁且配置有效关，
  不排除 `PassedLevels` 中的已通关项。
- 影响仅限 `Level999Ctrl` 的目标解析；Prefab 绑定、2 秒门禁、Actor 演出、震动、一次性触发锁、
  `GameManager.TryEnterDraw/TryEnterDrawLevel`、Draw Loading 和 HUD 初始化保持不变。

### 新行为和数据/资源/事件流

1. 从 `DrawGameLevelCtrl.LevelSorts` 第一项开始正向扫描，候选必须存在于 `DB.DrawGame.UnlockLevels`、
   不存在于 `DB.DrawGame.PassedLevels`，且 `Path` 能解析到 `DrawLevelData`。
2. 若无有效的未通关候选，进入排序最前的有效已解锁关供重玩；若存档没有可用解锁数据，
   回退到排序中第一条配置有效关，保持清档/旧档入口可用。
3. 目标确定后的主线退出、Draw 激活、选关面板短暂创建后关闭、`EnterLevelLoading`、DLevel/HUD 创建与输入开放不变。
4. 本次不主动写入 `CurrentLevel`、`UnlockLevels` 或 `PassedLevels`；Draw 关内后续胜利、Replay 与选关继续使用原存档流。
5. `level_start/level_end` 仍由 Draw 玩法控制器负责；`enter_tbc_portal(type=0)` 仍在目标进关请求被接受后才上报。

### 风险、验证、未验证缺口与回滚

- 风险：远程排序改变后“最小”按当前最终有效列表顺序计算，不强制按数值 Id 重排；缺失配置的候选会安全跳过。
- 静态验证：已确认目标解析器正向扫描并排除 `PassedLevels`，两级回退均跳过缺失
  `DrawLevelData` 的项；`Level_999.prefab` 仍绑定 `EnterDrawGameAtHighestPlayableLevel()` 和 `EnterDoorAnim()`。
- 定向 `git diff --check` 通过；NuGet restore 后执行 `dotnet build Assembly-CSharp.csproj --no-restore`
  成功，结果为 `0 error / 1124 warning`，warning 未在本次逐项归因。
- `uncertain`：尚未执行 Unity PlayMode 的“部分通关”、“全部通关”、“无解锁存档”、连点、Loading 失败恢复及
  Android/iOS 横屏验证。
- 回滚：恢复从 `LevelSorts.Count - 1` 向 0 扫描已解锁有效关，删除 `PassedLevels` 过滤和分级回退即可；
  不涉及存档、配置或资源迁移。

## 2026-08-18：Draw / IWanner 反向进入主线的后续扩展

- 上文“当前仅有一个运行时调用点”是 DLevel/ILevel 接线前的时点结论；现行范围已扩展为三个关内入口：
  主线 `Level999Ctrl` 进入 Draw、`DLevel999Ctrl` 与 `ILevel999Ctrl` 进入最小序列的已解锁未通关主线关卡。
- 两个反向入口不调用 `ReturnToStartPanel()`，而是经 `GameManager.TryEnterMainLevel()` 切换 Main 控制器后，调用
  `PlayCtrl.TryEnterLowestUnlockedUnpassedLevel()`；后者通过 `LevelCtrl` 扫描最终排序，跳过已通关、未解锁或缺少配置的项，
  选择最小 `LevelSort.Id` 后派发现有 `EnterLevelEvent`。
- 现有 Draw/IWanner 控制器在停用阶段先完成退出统计与清理，主线随后沿标准进关路径发送 `level_start`；
  该顺序符合 `DevelopMD/关卡打点生命周期约定.md` 的跨玩法切换口径。
- 目标进关请求被接受后，DLevel 上报 `type=1`、ILevel 上报 `type=2`；上文主线入口的 `type=0` 与失败不上报边界不变。
- D/I 入口的实现、风险和回滚分别记录在
  `Assets/Game/Scripts/Level/DrawLevel/Level999/DLevel_999主线传送门功能与风险评估.md`、
  `Assets/Game/Scripts/Level/IWannerLevel/Levels/ILevel999Ctrl功能与风险记录.md`。

## 2026-09-11：SupportUsPopup 打开期间阻断传送门点击穿透

### 需求、原行为基线与风险目标

- 需求：主线 `Level_999` 点击 SupportUs 打开 `SupportUsPopup` 后，弹窗区域内的后续点击不得穿透并触发关内 DrawGame 传送门。
- 修改前，`SupportUsPopup` 已使用 Bear.UI `UILayer.Popup` 和默认遮罩；但 `ClickItemEventHandle` 自行读取鼠标/触摸并执行
  `Physics2D.Raycast`，不经过 UI EventSystem，因此遮罩无法自动阻断世界 Collider 点击。
- 2026-08-19 的同类修复仅覆盖 `DLevel_999`：其点击事件绑定 `DLevel999Ctrl.EnterMainLevelByClick()`，由控制器检查
  Popup、Top、System 层是否存在打开视图。主线 `Level_999` 仍直接绑定
  `Level999Ctrl.EnterDrawGameAtHighestPlayableLevel()`，未复用该点击门禁。
- 风险目标：只修复主线 `Level_999` 传送门点击入口，不改变共享 `ClickItemEventHandle`，避免其它已验收关卡承担输入行为回归。

### 影响文件、系统与修改后流程

- 修改 `Assets/Game/Scripts/Level/MainLevel/Level_999/Level999Ctrl.cs`：新增公开点击入口
  `EnterDrawGameByClick()`，仅在运行态且 Popup、Top、System 层均无有效打开视图时转调原跳转入口。
- 修改 `Assets/Game/Prefabs/Level/Level_999.prefab`：`enterDrawGame` 的 `ClickItemEventHandle.OnClickEvent` 从
  `EnterDrawGameAtHighestPlayableLevel()` 改绑为 `EnterDrawGameByClick()`。
- `HasOpenPopup()` 只在一次世界点击回调中扫描 UIManager 子级，不进入 `Update()`；关闭但被 UIManager 缓存的视图因
  `IsOpen=false` 不会阻断，DEBUG_MODE 常驻 `GMPanel` 不作为业务覆盖层。
- SupportUs 点击仍按原链打开 Popup；Popup 打开期间，传送门点击入口直接返回。Popup 关闭后，原 2 秒门禁、DrawGame
  开关、目标解析和一次性触发锁继续决定是否允许跳转。
- `EnterDoorAnim()` 仍直接调用原跳转入口，碰撞传送表现不经过点击专用门禁，与 DLevel_999 既有边界保持一致。
- 未修改 SupportUs Popup/遮罩、共享输入组件、DB/存档、RemoteConfig、本地配置、资源地址、全局事件、广告、IAP、
  analytics 或 Android/iOS 平台分支；有效跳转后的 `enter_tbc_portal(type=0)` 上报时序保持不变。

### 验证、剩余风险与回滚

- 静态确认 `Level_999.prefab` 的世界点击 UnityEvent 只改绑点击专用入口，组件目标仍为同一 `Level999Ctrl`。
- 静态确认门禁与 DLevel_999 已有实现一致：仅检查 `IsOpen=true` 的 Popup、Top、System 视图，并排除常驻 GMPanel。
- 静态确认碰撞入口、2 秒门禁、一次性锁、Draw 目标解析、SDK 打点和 SupportUs 打开链均未改动。
- `git diff --check` 通过；输出仅包含工作区既有的 LF/CRLF 转换提示，没有空白错误。
- Prefab YAML 接线断言通过：点击入口绑定 1 处、旧方法直接绑定 0 处、公开目标方法 1 处、目标
  `Level999Ctrl` 组件定义 1 处。
- 定向执行 `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly
  /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，
  结果为 `0 error / 123 warning`；未建立修改前 warning 数量基线，因此不把 warning 归因于本次修改。
- `uncertain`：尚未在 Unity PlayMode 验证 SupportUsPopup 打开时点击传送门重叠区域、关闭弹窗后恢复点击、碰撞入门，
  以及 Android/iOS 横屏触控。
- 回滚时将 Prefab 点击方法恢复为 `EnterDrawGameAtHighestPlayableLevel`，并从 `Level999Ctrl` 删除
  `EnterDrawGameByClick()`、`HasOpenPopup()` 及 `using Bear.UI`；不涉及数据或资源迁移。

### 2026-09-11 PlayMode 反馈补充：穿透到 SupportEnter

- 用户提供的运行时堆栈进一步确认，弹窗打开后的世界点击还会命中 `SupportEnter` 自身，再次调用
  `SupportUsCtrl.OpenSupportUsPopup(Transform, Vector2)`；UIManager 因同一 `SupportUsPopup` 已位于 Popup 栈顶而报错并返回。
- 因此完整修复由两层局部门禁组成：`Level999Ctrl.EnterDrawGameByClick()` 阻止穿透到 Draw 传送门；
  `SupportUsPopup.Create()` 在 `IsOpen=true` 时返回现有实例，阻止 SupportUs 点击或 Trigger 重复调用 `OpenUI`。
- SupportUs 幂等实现及验证缺口记录在
  `Assets/Game/UI/SupportUs/Scripts/SupportUsPopup功能记录.md` 的 2026-09-11 章节。
