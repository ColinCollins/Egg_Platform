# CommonPopup Quit 防插队功能记录

## 2026-07-22 最小修复

### 需求与背景

- `PlayCtrl.Update()` 原来在任意游戏状态收到 `KeyCode.Escape` 后都会直接打开 `CommmonPopup` 退出确认弹窗。
- Guide Flow 与胜利结算期间也会命中该入口，退出弹窗可能覆盖或隐藏当前业务 UI。已确认 DailyChallenge 与 Quit 同属 `UILayer.Popup`，新 Popup 会使原栈顶只进入 `OnHide()`；DailyChallenge 的关闭等待依赖 `OnClose()`，因此存在结算流程无法继续到 VictoryPanel 的风险。
- 本次按最小修复处理，只阻止 Quit 在已知独占流程中插队，不引入通用返回键路由，不修改 Bear UI 栈行为。

### 影响文件与系统

- `Assets/Game/Scripts/Play/PlayCtrl.cs`
  - `ShowExitPopup()` 在创建退出弹窗前调用 `CanShowExitPopup()`。
  - 新增私有判断，复用 `UIManager.GetUI(...)`、`GuideFlowManager` 只读状态与现有 FSM `CheckState(...)`。
- 不修改 `CommmonPopup` 脚本或 prefab、`UIManager`、`GuideFlowManager`、DailyChallenge、Guide 业务脚本和关卡资源。

### 原有行为基线

- 每次收到 Escape 都调用 `CommmonPopup.Create(...)`。
- 没有退出弹窗防重复判断。
- 没有 Guide Flow 活动/排队判断。
- 没有 SUCCESS 状态判断，因此 Rating、DailyChallenge、奖励与胜利面板衔接期间也可插入 Quit。

### 修改后行为

- 已存在且仍为打开状态的 `CommmonPopup` 时，重复 Escape 不再再次请求打开。
- `GuideFlowManager.HasActiveFlow` 为 true 或仍有排队 Flow 时，Escape 被当前 Guide 流程消费，不打开 Quit。
- PlayCtrl 处于 `GamePlayStateName.SUCCESS` 时不打开 Quit，避免干扰 Rating、DailyChallenge 与 VictoryPanel 的串行结算。
- 其他状态保持原行为，仍由 `CommmonPopup.Create(...)` 打开退出确认弹窗。

### 数据、资源、UI、事件与平台边界

- 不新增或修改 DB、Save、RemoteConfig、Luban 配置、资源路径、prefab、全局事件、analytics、广告、IAP 或平台 SDK。
- 只在用户按下 Escape 的离散输入入口执行常量级状态查询，没有新增 `Update` 轮询、实例化循环或持续分配。
- Android 实体返回键与 Unity Editor Escape 共用该分支；iOS 没有新增平台分支。

### 验证记录

- `git diff --check -- Assets/Game/Scripts/Play/PlayCtrl.cs` 通过；仅有工作副本后续按 Git 配置转换为 CRLF 的提示。
- 静态检查确认守卫依次覆盖已打开 Quit、Guide Flow 活动/排队和 SUCCESS 状态，守卫失败时不会调用 `CommmonPopup.Create(...)`。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过：0 个错误、99 个项目既有警告。

### 剩余风险与未验证项

- 尚未在 Unity PlayMode 验证 Guide Popup、GuideFinger、GameTipsPopup 各阶段按 Escape 后流程继续完成。
- 尚未在 Rating、DailyChallenge 进度/奖励/自动关闭和 VictoryPanel 衔接期间逐段验证 Escape。
- 尚未在 Android 横屏设备验证实体返回键；iOS 横屏未新增行为，但也未做回归。
- 本次只覆盖 Guide Flow 与 SUCCESS。其他非 SUCCESS 模态 Popup 是否也应阻断 Quit 仍待后续统一返回键策略确认。

### 回滚路径

- 删除 `ShowExitPopup()` 开头的 `CanShowExitPopup()` 判断和该私有方法，即可恢复原来任意状态 Escape 都打开 Quit 的行为。
- 回滚不涉及 prefab、资源、配置、存档或数据迁移。

## 2026-08-05 DrawGamePlay 返回键补齐 Exit App

### 需求与原有基线

- 需求：DrawGamePlay 模式收到 Escape/Android Back 时，与主线一样使用默认 `CommmonPopup` 显示 Exit App 确认。
- 修改前返回键监听只存在于 `PlayCtrl.Update()`；进入 Draw 后 `PlayControllerManager` 只驱动活动的 `DrawPlayCtrl.Tick()`，因此主线 `ShowExitPopup()` 不会被调用。
- `DrawPlayCtrl.Tick()` 原本只在活动且未暂停时驱动 analytics 与关卡 Tick，没有 Escape、`CommmonPopup` 或 `Application.Quit()` 链路。

### 影响文件与修改后行为

- 运行时仅修改 `Assets/Game/Scripts/Play/DrawPlayCtrl.cs`，未修改已验收的主线 `PlayCtrl`、`CommmonPopup`、`PlayControllerManager`、UI prefab 或资源路径。
- 活动 Draw 控制器的 `Tick()` 现在先处理 `Input.GetKeyDown(KeyCode.Escape)`；该离散输入在 Draw 暂停时也可读取，但原有暂停门禁仍阻止关卡 Tick 和时长统计推进。
- 允许时使用主线相同的 `U_Quit_Title_01` / `U_Quit_Des_01` 本地化键打开默认 `CommmonPopup`；确认后调用 `Application.Quit()`，Unity Editor 中同时退出 PlayMode。
- 以下情况拒绝创建 Exit App 弹窗：已有打开的默认 `CommmonPopup`、引导正在执行或排队、Draw 关卡正在加载、Reset/Replay 过渡未完成、Draw 已进入 `Resolved` 结算阶段。
- Draw 选关、Intro、正常 Drawing 和非过渡暂停阶段允许调起 Exit App 弹窗。

### 数据、UI、平台与性能边界

- UI 流为 `PlayControllerManager.Tick -> DrawPlayCtrl.Tick -> CommmonPopup.Create -> Bear UIManager Popup 层`；不新增全局事件、不修改 UI 栈规则。
- 不读写 DB/Save，不修改 Luban、RemoteConfig、analytics、广告、IAP、资源加载或平台 SDK 流程。
- 没有新增 MonoBehaviour `Update()`；检查复用已有 Draw 每帧 `Tick()`，只增加一次 `GetKeyDown` 常量级查询，不产生循环或持续实例化。
- Android 实体/系统返回键由 Unity 映射为 `KeyCode.Escape`；iOS 没有新增平台分支。

### 验证记录

- CodeGraph 确认新调用链为 `DrawPlayCtrl.Tick -> ShowExitPopup -> CanShowExitPopup`，两个新私有方法均只有一个调用点，未发现覆盖测试。
- 静态检查确认 Draw 只有一个 Escape 入口，且守卫覆盖重复弹窗、Guide Flow、加载、尝试过渡与 `Resolved`。
- `git diff --check -- Assets/Game/Scripts/Play/DrawPlayCtrl.cs` 通过，仅有工作副本后续按 Git 设置转换 LF/CRLF 的提示。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo -v:quiet -m:1 /p:UseSharedCompilation=false /nr:false /clp:ErrorsOnly` 在 184 秒上限内未返回错误输出，但也未完成，因此编译结果为 `uncertain`。

### 剩余风险与回滚

- `uncertain`：尚未在 Unity PlayMode 分别验证 Draw 选关、Intro、Drawing、暂停、加载、Reset/Replay 过渡、Guide Flow 和 Victory 结算阶段的 Escape 结果。
- `uncertain`：尚未在 Android 横屏真机验证系统返回键、确认退出、取消后继续游戏与输入恢复；iOS 横屏也未回归。
- 回滚时删除 `DrawPlayCtrl.Tick()` 的 Escape 分支和本地 `Exit App` 区域，并恢复原 `_isActive && !_isPaused` Tick 结构即可；不涉及 prefab、资源、配置或存档迁移。

## 2026-08-19 IWannerGame 返回键补齐 Exit App

### 需求、基线与修改后行为

- IWanner 活动控制器原先把 Escape/Android Back 送入 `OpenPausePopup()`，只会打开 `GameSettingPopup`；主线与 DrawGame
  已使用默认 `CommmonPopup` 作为 Exit App 确认。
- `IWannerPlayCtrl.Tick()` 现改为调用本地 `ShowExitPopup()`，复用相同本地化键和确认退出行为；HUD PauseBtn 仍打开设置。
- IWanner 本地门禁覆盖：默认退出弹窗已打开、Guide Flow 活动/排队、关卡加载、失败等待 Reset、Skip 广告请求、Group 结算。
  暂停状态与 DrawGame 一致，仍允许收到返回键并尝试打开退出确认。

### 系统边界、验证与回滚

- 不修改 `CommmonPopup`、`UIManager`、HUD/UI prefab、资源、DB/Save、Luban、RemoteConfig、打点、广告策略或平台 SDK；
  只在既有控制器 Tick 中处理离散按键。
- 完整 `Assembly-CSharp` 构建通过：`0 error / 106 warning`。Unity PlayMode 各状态与 Android 真机 Back 键仍为
  `uncertain`；iOS 无新增返回键分支，也未回归。
- 回滚时恢复 IWanner Escape 对 `OpenPausePopup()` 的调用并删除本地 Exit App 方法即可。


## 2026-10-12 Dig Android Back / Escape 补齐

### 需求、基线与来源

- 用户要求补充Draw/Dig对照表中的返回键。原Dig Tick只同步HUD、按暂停/转场/结果/就绪门禁更新统计和提示，没有系统返回键；活动控制器由GameManager.Update→PlayControllerManager.Tick驱动。
- 参考本文Draw/IWanner现行Exit App契约与DrawPlayCtrl.Tick/ShowExitPopup/CanShowExitPopup；CommonPopup默认Create、Yes/No/Close与已有Prefab绑定；Dig Tick/状态/重建、HUD和选关广告pending、GuideFlowManager、TBC结果锁及设置Popup宿主。原始修改前核对在当日Dig Back专题。

### 修改后与完整边界

- 仅DigPlayCtrl新增局部Exit App方法和Tick离散按键：原HUD同步后，活动控制器才读取GetKeyDown(KeyCode.Escape)，并在原暂停/就绪门禁前尝试打开默认CommmonPopup。选关（Runtime为空）、普通/TBC和非转场暂停允许；没有新增MonoBehaviour.Update或持续实例化。
- 与Draw共用U_Quit_Title_01/U_Quit_Des_01本地化键、默认CommmonPopup资源及Popup层；确认执行Application.Quit，UNITY_EDITOR额外结束PlayMode。No先关闭再回调、Close销毁无回调沿用原组件；本次不额外Pause/Resume，取消/关闭保持原玩法或设置暂停状态。
- 拒绝默认CommmonPopup已开、Guide活动/排队、transition加载/Reset/Replay、resolved结算、Runtime已锁结果、选关RV或HUD加墨RV尚未收尾。Dig显式处理FixedUpdate先锁结果但控制器尚未处理的窗口；暂停本身不阻挡返回键，广告门禁与暂停分别判断。
- 不修改原主线/Draw/IWanner、CommmonPopup、UIManager、Guide、HUD/Prefab/Generated、存档/配置/云控、统计/奖励/DeepLink、广告或平台SDK。Application.Quit不等于菜单返回选关；设置Menu仍走原ExitToLevelSelection。

### 验证与未覆盖

- 22项实际Tick/Show/Can与CommonPopup关闭方法隔离断言分别在Editor/Player宏通过：无键/非活动/选关/暂停、重复/关闭重开、文案、确认先关再Quit、Editor分支、取消保留暂停、Guide/两广告/加载/结算/先锁结果拒绝、收尾恢复与TBC形态。输入/UI/Guide/Quit平台服务为替身，测试没有实际退出应用或Editor。
- 原Tick的HUD/统计/提示主体对照保留，12个原Dig生命周期/进度/DeepLink/暂停方法不变；其它模式控制器、默认Popup与资源无diff。当前1214源码Editor/Android宏编译0 error（113/118 warning）；iOS仍为ChoiceLevelPanel.cs:17、StartPanel.cs:14两处既有Guru.UniKit.Apple引用缺失，未报告iOS通过。
- `uncertain`：真实Unity UI栈、关闭重开、Guide中返回键、广告等待取消/晚回调、Android实体/系统或预测返回键映射、横屏触控、实际退出应用与iOS回归；本轮只证明游戏侧入口与受测逻辑。最短验收：Dig选关/001/TBC/暂停→Back→取消/Close/确认；再在加载/Reset/Replay/胜利/Guide/两广告期间按Back，确认没有插队及收尾后恢复。
- 证据：DevelopMD/验证记录/Dig返回键_2026-10-12/；22场景两宏、静态源码边界及三组编译日志分开保存。不要把模拟的Quit次数当作真机退出成功。

### 回滚

删除Dig Tick按键与活动入口新增两行，恢复原组合统计门禁并移除局部退出应用区域即可；不改Shared Popup/资源或玩家数据。按本轮提交差异处理，保留并行地图/关卡/其它工作区修改。Excel返回键项随后改为代码接入并记录场景/证据边界。
