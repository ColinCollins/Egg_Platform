# OneLink 接入、路径路由与编辑器测试

## 2026-10-13：撤回 iOS AF 联网延后方案

### 撤回前核对

- 用户明确要求“先不改这块，还原”：只撤回上轮未提交的 iOS 网络门禁、AF 包装与延后绑定/计时，保留已提交的 d296d25a1 绑定/退订职责整理、既有 Firebase plist=false 配置及所有其它工作区内容。
- 当前来源与边界：GameAnalytics.cs 的 AF 工厂、OneLinkManager.cs 接收区的两个 iOS 内部入口及 AppsFlyerHook 条件分支、新增 GameAppsFlyerTransmitter.cs/meta；当前两个旧脚本的 Git diff 仅为本方案。对照 HEAD 和前轮 before 快照，原链为 Guru AF Prepare/initSDK → Hook.Prepare 绑定 UDL/GCD → Guru AF Initialize/startSDK → Hook.Initialize 开始 GCD 等待。没有本轮新增的生成代码、Prefab 引用或数据迁移。
- 拟执行与验证：以定向补丁恢复两个现有脚本及原时序注释，删除本轮新增脚本/meta；只替换模块和当日记录的 iOS 方案章节，保留其它主题。验收为两个脚本与 HEAD 无 diff、新脚本/meta 不存在、运行时代码无包装及两个通知入口引用、前轮归因整理和 plist 不变；不进行全库 restore/reset 或 Packages/Guru/GF 写入。
- 回滚留存：新增脚本及 meta 在删除前已备份至前轮 TEMP 目录，名称 GameAppsFlyerTransmitter.withdrawn.cs/.cs.meta；前轮 before 快照、测试项目与编译日志仍保留。CodeGraph CLI/MCP 不可用，037 plan 仍缺失，撤回依据是当前精确 diff/源码/既有模块记录；真机首装 UDL 原因仍 uncertain，撤回不等于修复。
- 历史验证不再代表当前实现：联网方案曾通过 52 项隔离断言和 22 项源码检查，但未做首装真机/完整 iOS 构建。本轮不再保留该方案为有效运行时设计，恢复后的状态以撤回复核为准。

### 撤回复核

- 已定向撤回：GameAnalytics.cs、OneLinkManager.cs 与 HEAD 无 diff；新增包装脚本/meta 已删除且有 TEMP 副本，游戏侧 SDK 源码无包装/通知接口引用。原归因职责整理、plist 回调及 Packages/GF 无 diff，SDK 目录只剩本撤回文档修改；模块其它章节与 HEAD 一致，日期记录其它主题与撤回前一致。
- `verify-rollback.ps1/verify-rollback.log` 的 9 项源码/文件/文档检查通过，另核对日期主题正文保持；两份记录 diff 格式检查通过。证据在上述前轮 TEMP 目录；本轮不修改 Git index、不提交 Git，也不重新生成 SDK/程序集。尚未重编译或执行 Unity/真机测试，iOS 首装 UDL 仍 uncertain；恢复后的当前方案没有新增联网门禁。如后续重新采用延后方案，必须基于 TEMP 副本重新接入、核对和真机验收，不能直接沿用历史测试结论。

## 2026-10-13：AppsFlyer 接收与订阅职责整理

### 修改前核对

- 范围与验收：用户要求先优化当前相关代码，本轮只整理现有接收/订阅/释放职责，不实施前述 iOS 网络等待或统计包装方案。保留 initSDK → UDL 绑定 → startSDK 的时机，完整保留参数解析、GCD/UDL 仲裁、重试、首页/关内门禁、打点字段和平台行为；本轮不能被当作首装 UDL 修复。
- 来源与基线：`OneLinkManager.cs:67–108,252–277,642–656`，`OneLinkManager.Attribution.cs:18–42,48–83,127–191`，`GameAnalytics.cs:40–47`；本模块 2026-10-12 初始化顺序约定。只读核对插件 `AppsFlyer.cs:1001–1006`、iOS Wrapper:350–357：订阅 UDL 会设置原生 delegate，不是纯托管数据监听。Guru `Prepare:202/InitializeInternal:268` 继续拥有 init/start，游戏侧不重复调用。
- 当前流程：先初始化 Unity URL 接收，检查 Guru 接收对象和插件实例；添加 UDL 接收组件并订阅 → 开始 GCD 观测并订阅成功/失败 BehaviorSubject → 设置共同已绑定门闩 → 记录 bound → 探测缓存；GCD 十秒等待仍由 Hook.Initialize 发起。Dispose 保留原取消、成功/失败退订、计时清理、URL/UDL 退订及状态清理顺序；GCD 初值同步回放必须仍发生在共同门闩设置之前。
- 维护边界与拟改：两个 OneLink partial 都是手写运行时代码，无生成器和序列化绑定。本轮仅在原类新增私有 UDL 绑定/退订方法，将 GCD 绑定/退订移入已有 Attribution partial 的私有方法；原语句和注释不改、无新增状态/公共 API/全局事件/原生请求。GameAnalytics、SDK 初始化、consent/收入/token、Packages/Guru/GF、plist 与 Loading/RemoteConfig 不修改。
- 验证：展开新私有方法后与原 Bind/Dispose 顺序逐字比较；非目标源码一致；隔离比较正常、缺接收者、缺插件、重复绑定、已有组件、同步 GCD 回放、释放/重复释放、退出后晚回调、重新绑定和注入订阅异常的轨迹；复用当前 Unity 引用进行 Editor/Android/iOS 条件编译。无性能优化收益承诺；不增加队列、持续分配或逐帧任务。
- 未决项与回滚：Unity 原生 delegate 的网络错误原因仍 uncertain，未取得当前测试 IPA/原生日志；CodeGraph CLI/MCP 不可用、037 plan 缺失，以当前源码与已读记录补核。回滚只将四段绑定/释放语句内联并移除新增私有方法，不回滚用户其它工作区修改。任务开始已有 Dig/字体/Android/当天记录等改动，按原样保留。

### 实施与验证

- `OneLinkManager.BindAppsFlyer` 保留原门禁、Initialize、接收对象检查、共同绑定标记、bound 日志与缓存探测，仅顺序调用 `BindUnifiedDeepLinkReceiver` 和 `BindConversionDataReceiver`。UDL 的接收组件/事件订阅留在主 partial；GCD 观测与成功/失败订阅移入 Attribution partial。两种退订分别由私有方法承接，Dispose 的原调用次序不变；移除主文件已无用途的 R3 using。旧注释按原文保留，仅补充新方法的中文职责与 iOS delegate 副作用说明。
- 源码与隔离验证：143 项断言通过；展开新方法后原 Bind/Dispose 的语句和顺序一致，其余主文件/归因源码、原注释、GameAnalytics 配置及四接点顺序不变。10 类修改前后对照覆盖正常/已有组件/无初值回放/冷启动 URL/编辑态/缺接收对象/缺插件/两者皆缺/两类订阅异常，每类比较首次与重复绑定、回放、退订、重复清理、晚到 UDL 以及重绑轨迹。同步回放仍看到 bound=false，缓存探测看到 bound=true。
- 验证边界：生产绑定/初始化/清理方法原样提取，SDK 与 Unity 副作用为桩；注入异常只验证本轮不改变原中断位置及部分绑定状态，不代表原异常处理已完善，也不是原生错误复现。没有新增网络重试、异常吞噬或自动回滚。
- 条件编译：使用当前 Unity 6000.0.59f2 Roslyn/Bee 引用，把输出与响应文件写入独立 TEMP；修改前后 Editor 均为 0 error / 660 warning、Android 均为 0 error / 665 warning。iOS 修改前后均为 2 error / 121 warning，阻塞是 ChoiceLevelPanel.cs:17 与 StartPanel.cs:14 缺少 Guru.UniKit.Apple；本轮不改只读 SDK 或无关 UI，不能报告 iOS 构建通过。定向 diff 格式检查通过。
- 证据：`C:/Users/admin/AppData/Local/Temp/brainpunk-onelink-bind-refactor-20261013-7f4c/` 内有两份源码快照、GameAnalytics/日期快照、`verify.ps1/verify.log`、`compile.ps1` 与 before/current 三宏响应文件及日志。编译脚本复用已读前轮 TEMP 脚本，补充本轮源码对照；正式程序集、Git index、Packages/Guru/GF 和用户其它任务未由本轮写入。
- `uncertain`：未执行 Unity PlayMode、iOS 首装/联网权限恢复、真实 UDL/GCD/ATT/收入/token、Android/iOS 新包或后台收数验证。本轮仅优化代码可读性与职责位置，不改变仍未等待网络的绑定/启动现状，不声明修复首装问题。按前述四段内联即可局部回滚，无存档/配置迁移，未提交 Git。

## 2026-10-12 第二轮 Dig 选关/云控更新

Dig 路径解析后查询当前有效 Dig 排序（包含末尾真实数据40999），云控关闭时拒绝；匹配失败按首未通关排序回退。入口先激活独立选关面板，再复用面板的解锁/保存/版本校验和进关链，TBC 排序号不再固定999。本地30普通时末尾序号31，远端数量变化时重新计算。细节与未验证平台回调见 Dig选关与云控功能及风险记录；本轮不改主线/Draw/IWanner 路由。


## 2026-10-12：DigGame 进关协议扩展

- 本次用户明确要求 `sub1=dig`。旧解析器只接受 main/default、draw、iwanner；现新增 GameType.Dig，sub2 使用配置内 DigLevel_* Path，支持平铺及 iOS 对象/字符串 click_event。原 sub2 注释仅列主线/Draw，已与新增 Dig 支持不符，限定补齐该说明；其它无关注释不改。
- Router 新增独立 Dig 分支：匹配普通关或 DigLevel_999；空/无效目标按本地有效序列找首未通关关卡，全通后 TBC。`GameManager.TryEnterDigLevel` 与首页使用同一激活/加载/失败回首页链；只有链接可补目标解锁，不伪造通关记录，不加载链接提供的任意资源路径。
- DigPlayCtrl 揭幕/输入就绪后 ConfirmLevelEntered，退出/失败 Deactivate 时 EndEntry；复用原首页空闲、同次重复/并发、关内丢弃、安装预算/来源与 GM 门禁。不修改 OneLinkManager 核心接收/仲裁、SDK、Packages 或正式统计。Debug 参数说明增加 dig，SRN 尾标映射增加 DL-OL-dig-001/999；来源白名单和奖励协议保留。
- 33 项托管断言检查当前解析/SRN/配置/存档与 Dig 路由正文，外围服务是替身；Editor/Android 条件源码编译 0 错误，iOS 两处已有 Guru.UniKit.Apple 引用错误。真实投放链接、冷启动/唤起、生产资源加载/失败清理、关内晚回调及 iOS 原生行为仍 `uncertain`。
- 详细场景和产物见 `DevelopMD/验证记录/DigGame_2026-10-12/验证记录.md`，Dig 当前设计与回滚见 `Assets/Game/Scripts/Level/DigLevel/DigGame功能说明.md`。回滚仅撤 dig 解析/路由/SRN 分支和 Dig 来源确认/清理接点，保持其它玩法和历史来源规则。

## 2026-10-12：GameAnalytics 合并残留与配置职责整理

### 修改前核对

- 用户授权优化 GameAnalytics。当前待提交合并的 `GameAnalytics.cs:36` 注册不存在的 `OneLinkManager.FacebookHook`，前轮 Editor/Android 定向编译报 CS0426；当前 Facebook 生命周期由 `FacebookDdlHook` 负责。旧章节中的 FacebookHook 为历史实现，不能据此再初始化 Facebook 或新增取数接点。
- 完整入口为 `GameSDKService.InitService` 提前接好 OneLink URL → `GameProtocol.AnalyticsDelegate` → `GameAnalytics.CustomTransmitters` → Guru 按序注册并完成全部 Prepare → 全部 Initialize。必须保留 `FacebookDdlHook → Ga4fDdlHook → GuruAppsflyer → AppsFlyerHook` 顺序：AppsFlyerHook.Prepare 仍在 SDK initSDK 后订阅 UDL，Facebook/GA4F Initialize 仍在 AF 启动前建立窗口；不修改 ATT、五秒窗口、GCD 观测计时和来源仲裁。
- 只读核对 SDK 接口与消费处：`IAnalyticsDelegate.cs:15-20` 接受 null 规则，`GuruAnalyticsStrategy.cs:626-685` 判空后保留配置策略；`GuruAnalytics.Transmitter.cs:68-74,420-464` 逐序消费 IReadOnlyList，游戏侧没有增删该集合的调用方。拟仅删除失效注册、以固定数组替换接点 List、把无实例依赖的配置方法改为 private static、显式说明 null 规则及整理分区；19 项 AF 白名单、收入规则、配置值和原注释不改。
- 游戏侧为手写代码，Packages/GuruSDK/GF 只读；没有单独 GameAnalytics 文档，本轮沿用本模块记录及当日记录。037 plan 缺失，CodeGraph 返回当前源码但标记 GameAnalytics 符号索引过期，SDK 内容以只读源码补核。验证按源码差异、Unity 引用定向编译和真机分层报告，真实 SDK/后台收数仍为 `uncertain`。

### 实施、验证与剩余边界

- **实现事实**：仅修改 `SDKImps/GameAnalytics.cs`，删除未定义的 FacebookHook 注册；接点集合由 List 改为固定四项数组，对外仍为 IReadOnlyList；配置工厂改为 private static。两项自定义策略由默认返回 null 改为明确返回 null，语义不变；补中文职责说明与三个 region，原注释不改。没有新增全局事件、原生请求、缓存、线程切换、异常吞噬或 SDK 初始化入口。
- **源码核对**：18 项断言通过，逐项比较全部 19 个 AF 事件名、规则与顺序、两项 Revenue、四个有效接点顺序、DevKey/AppleId、debugMode=false、配置方法体、Lazy 模式、null 规则语义与原注释。Git index 全部条目与修改前一致；只有一个运行时文件及本说明、当日记录出现本轮未暂存差异。`git diff --check` 通过，未暂存、提交或结束用户进行中的 merge。
- **定向编译**：复用当前 Unity 6000.0.59f2 Roslyn 与 Editor Bee 引用，补齐当前跟踪的游戏/配置源码，输出全部写入 TEMP。Editor 0 error / 660 warning、Android 宏 0 error / 663 warning；相比前轮各有 1 条 FacebookHook CS0426，本轮该编译阻塞消失。现有其它警告未在本任务中处理。这不是 Unity 完整导入、Player/IL2CPP 构建或运行时测试。
- **iOS 与真机缺口**：iOS 宏仍为 2 error / 121 warning，分别是 `Assets/Game/UI/LevelChoicePanel/Scripts/ChoiceLevelPanel.cs:17`、`Assets/Game/UI/StartPanel/Scripts/StartPanel.cs:14` 缺少 `Guru.UniKit.Apple`，与前轮同样的 Windows 引用限制；不能据此宣布 iOS 整体编译通过。真实首次安装 Helper、已安装 URL 唤起、ATT 接受/拒绝、SDK 请求失败或晚到、应用切后台和统计后台收数均未实测，保持 `uncertain`。本轮未改变这些处理方法，但不据此保证整条平台流程正确。
- **证据**：`C:/Users/admin/AppData/Local/Temp/brainpunk-gameanalytics-optimize-20261012/` 内有 `GameAnalytics.before.cs`、`git-status-before.txt`、`git-stage-before.txt`、`source-invariants.log`、`compile-current.ps1`、三组 `current-*.rsp` 与 `compile-*.log`。编译脚本沿用前轮，原审查目录与日志保留。
- **最短后续验收与回滚**：在具备完整 iOS 引用的环境构建 Android/iOS 正式包，核对 AF 白名单事件及收入参数、`onelink_gcd` 开始/返回/失败观测，并使用各自 App ID 重测 Helper 首次安装进入指定关卡；源码编译通过不能替代该验收。若仅回滚整理，恢复 List 和实例工厂即可，保留四个有效接点；若必须逐字恢复修改前文件，会重新引入已知编译错误，不应作为可交付回滚。不回退其它合并文件、不改只读 SDK 或玩家数据。

## 2026-10-10：关闭 AppsFlyer debugMode

### 修改前核对

- 需求与行为基线：用户要求关闭 AppsFlyer 调试模式；当前游戏侧 `GameAnalytics.CreateAppsFlyerConfig()` 显式传入 `debugMode: true`。本节替代 2026-09-28 临时开启日志的当前配置说明，保留其历史记录。
- 来源与维护边界：`GameProtocol.AnalyticsDelegate` → `GameAnalytics.CustomTransmitters` → `GuruAppsflyer.Prepare(CreateAppsFlyerConfig())`；只读核对 `Packages/com.guru.sdk.framework.analytics.appsflyer/Runtime/GuruAppsflyer.cs` 的公开构造参数，以及 `Prepare()` 中 `setIsDebug(config.DebugMode)` 先于 `initSDK()` 的顺序。游戏侧为手写文件，Packages、GuruSDK、GF 不修改。
- 拟改位置与影响：只将构造参数改为 `false`，更新原“临时开启”注释，因为其会与修改后代码冲突；保留事件白名单、四个 transmitter 的顺序、OneLink 回调、重试、路由及存档流程。Android/iOS 共用该配置，不另加平台分支。
- 验证场景与未决项：核对游戏侧其它初始化／调试设置、精确源码差异及 `git diff --check`。`uncertain`：不以静态检查代替重新构建后的 Android/iOS 原生日志验收；当前已运行的 SDK 不会因修改源码即时切换。037 plan 缺失，CodeGraph 不可用，本次以当前本地源码补足依据。

### 实施、验证与回滚

- 已将唯一游戏侧 `AppsFlyerConfig` 构造调用改为 `debugMode: false`，同步纠正原临时开启注释。对照 HEAD 的源码断言通过，运行时代码仅有这两行变动；全游戏脚本检索未发现其它 `AppsFlyerConfig` 或 `setIsDebug` 设置，目标文件 `git diff --check` 通过。正常事件及 OneLink 相关代码未改；当日记录补充本次事实，不覆盖其它任务内容。
- 本轮未进行 Unity 编译、Player 构建或真机日志验证，Android/iOS 实际日志为 `uncertain`；需重新构建并在 SDK 初始化后确认调试输出关闭，不应把 Guru 普通阶段日志误认为 AppsFlyer 调试开关仍开启。回滚只将本构造参数恢复为 `true` 并同步用途注释、重新构建，不改 SDK 或玩家数据。

## 2026-10-10：GCD 打点 onelink_gcd

### 修改前核对

- 需求与基线：用户要求记录 GCD 开始、失败原因（超时／网络失败）及返回指令，最终事件名称为 `onelink_gcd`。接入前只有阶段日志；`BindAppsFlyer` 订阅成功数据及五秒缓存探测，未订阅 SDK 已公开的 `ObservableConversionDataFailed`。其初始空字符串不代表失败。
- 来源与维护边界：`OneLinkManager.cs` 的 `BindAppsFlyer/Dispose/AppsFlyerHook`，`OneLinkManager.Attribution.cs` 的缓存／成功回调／目标解析，`GameSDKService.Analytics.cs` 的 `LogEvent`；只读核对 `GuruAppsflyerManager` 的成功／失败 BehaviorSubject 和 `GuruAppsflyer.InitializeInternal` 的 startSDK 生命周期。Packages、GuruSDK、GF 不修改；037 plan 缺失，CodeGraph CLI 不可用，使用当前源码查证。
- 拟改与影响：在绑定接收器时记录 `state=start`，订阅真实失败回调记录 `failed`；成功回调或缓存快照记录 `returned` 并按已有 af_dp／完整字段／campaign 优先级提取业务指令。记录发生在玩法／来源仲裁前，表示收到数据，不表示跳转或领奖成功。重复同一原始快照只上报一次，普通链接重置不重开 GCD 统计轮次。
- 超时约定：新增仅用于打点的十秒等待，从 AppsFlyerHook.Initialize 开始，不从 Prepare／缓存探测开始；仅在仍无成功／失败结果时记录 `failed/failure_reason=timeout/source=game_wait`。这是游戏侧等待超时，不证明 SDK 请求终止或网络根因；原五秒 UDL 仲裁不是 GCD 超时。超时不改变原候选、取数、重试、Loading 或迟到结果受理。
- 字段及隐私：`state` 使用 `start/failed/returned`；失败按 SDK 文本明确的 timeout／timed out 和 network／连接类关键词分类，未知为 `sdk_error`，另记当前 `network_connected` 观察值，不用“有网”证明服务可用。返回只上传白名单指令 `deep_link_value/sub1/sub2`、`deep_link_sub3_present`、`command_valid/resolution/parse_error`；字符串最多 100 字符，不上传 UID、完整 URL、任意 query、原始错误或归因包。
- 统计通道补查：`GuruAppsflyer.IsValidEvent` 只放行策略白名单，否则通用 LogEvent 虽能经过 Guru 等既有通道，AF transmitter 会丢弃本事件。因此同步在游戏侧 `GameAnalytics.CreateAppsFlyerConfig` 的现有 ExplicitEvents 增加 `onelink_gcd`；不修改 SDK 策略实现、全局能力或其它事件规则。
- 验证计划：接收器初始空值、开始一次、成功／缓存去重、三个指令及三种解析来源、自然／非首次返回、真实失败分类、无结果超时、失败／超时后迟到有效结果、Dispose 取消及重新绑定、统计异常不影响路由；用源码隔离模拟与真实 Unity 引用编译分开核对。真实 Android/iOS SDK 回调、ATT 等待与后台收数尚为 `uncertain`。
- 回滚：只撤销本节对应游戏侧打点、失败订阅及统计计时，不改 SDK、不清存档、不撤销其它 OneLink 功能。

### 打点职责调整前核对（事后补查）

- 用户指出不应在 Manager 自行封装打点。初版实际调用现有 `GameSDKService.Instance.LogEvent`，没有新传输通道，但字段组装与错误分类放在 Attribution 中，没有沿用既有业务事件的封装归属。
- 重新核对 `Services/Events/GameSDKService.Game.cs` 的 `LevelStartEvent/GetNewSkin/EnterTBCPortal` 和 `GameSDKService.IAP.cs`：事件专用 API 在 GameSDKService 组装参数，再调用既有 LogEvent。这部分是用户指出后的职责补查，不作为初版事前核对证据。
- 拟调整：新增游戏侧 `Services/Events/GameSDKService.OneLink.cs` 的单一 `OneLinkGcdEvent` 业务 API，将事件名、字段组装、错误分类、隐私白名单及统计异常隔离移入该 API。Manager 只监听生命周期、取消计时、按原规则解析目标并提交观测数据；移除 Manager 自行组装的事件字典与 ReportGcdEvent。前述状态、超时及路由约定不变，不修改通用 LogEvent、SDK 或其它业务事件。
- 验证：隔离工程直接编译新增业务 API，只替代通用 LogEvent 的底层传输；重跑相同字段与生命周期场景，确认 Manager 不再硬编码事件名或统计字典。真实引用编译须显式加入新 partial，不能以旧 Bee 清单遗漏新文件后通过作为证据。

### 最终接口与字段

#### 命名修正前核对

- 用户明确要求将 gdc 修正为 gcd。当前事件发送与 AF 白名单使用旧拼写，业务 API 有五处 Manager 调用；已核对发送接口、调用方和相应文档，本轮只同步事件为 `onelink_gcd`、接口为 `OneLinkGcdEvent`，不改字段、失败分类、超时或路由。
- 验证计划：旧接口／事件在生产代码中无残留，新事件发送与白名单精确一致，重跑原三宏隔离测试及 Editor／Android 引用编译；保留原 TEMP 证据目录名称，改名后的日志另留 rename 前缀。原生 SDK 与后台收数仍未验证，回滚只恢复本轮名字替换。
- 修正结果：发送端、AF 白名单及五处调用已同步；旧事件／API 在 SDK 源码和当前说明中无残留。更名后 Editor 136／Android 136／iOS 138 项隔离断言通过；真实引用编译 Editor 0 error／113 warning、Android 0 error／116 warning，命名阶段日志为原证据目录的 `rename-tests-UNITY_*.log` 和 `compile-Rename-*.log`。`git diff --check` 通过；iOS 原生引用与后台收数的未验证边界不变。

- 统一入口：`Services/Events/GameSDKService.OneLink.cs:24` 的 `OneLinkGcdEvent` → 原 `GameSDKService.Analytics.cs:45` 的 `LogEvent` → GuruAnalytics。事件名 `onelink_gcd`；`SDKImps/GameAnalytics.cs:52` 仅补 AF 策略白名单。Manager 的 Attribution 只提交观测数据、保留独立计时与快照去重，不自行组装统计字典。
- 修改文件：上述新 partial／meta、`OneLinkManager.cs`、`OneLinkManager.Attribution.cs`、`OneLinkManager.DdlUrl.cs`、`GameAnalytics.cs` 及本记录。DdlUrl／归因解析只加关闭重复诊断的可选参数，既有调用默认仍输出原日志，目标优先级不改。原 Hook 注释“不发送统计事件”已与本次行为冲突，限定更正该说明；其它旧注释未改。

| state | 意义 | 主要附加字段 |
| --- | --- | --- |
| `start` | 接收器已绑定，开始观测；不等于已经发送一次网络请求 | `source=receiver_bound` |
| `failed` | 收到 SDK 非空失败，或游戏侧无结果等待超时 | `failure_reason=timeout/network_error/sdk_error/invalid_conversion_data`；十秒观测超时另带 `source=game_wait/wait_seconds=10` |
| `returned` | 成功流或缓存取得回包，不代表执行成功 | `deep_link_value/deep_link_sub1/deep_link_sub2/deep_link_sub3_present`、`command_valid/resolution`、`af_status/is_first_launch`；无效指令带 `parse_error` |

- 公共字段为 `state/source/network_connected`；`source` 区分 `sdk_callback/sdk_cache/game_wait/receiver_bound`。业务指令保留关卡、金币、皮肤三种，映射后的 campaign／af_dp 目标也返回对应指令；UID 只报存在，字符串上限 100 字符。
- `command_valid` 只表示参数解析成功：自然／非首次、UDL 或原生已优先选中、关内晚到等仍受原门禁限制。相同原始快照经实时回调和缓存仅上报一次；变化后的回包继续观测。Dispose 取消统计等待、释放成功／失败订阅并清观测状态；普通链接／GM 不重开轮次。
- 失败分类是 SDK 文本关键词归类，不是可穷尽的原生错误码协议；未识别的网络错误可能归为 `sdk_error`。当前 `network_connected=false` 只表示连接状态观察，不自动推断本次失败的根因。游戏等待超时不取消任何取数或路由，后续有效结果仍可 `returned` 和按原流程执行；与 UDL 五秒候选仲裁独立。

### 验证、边界与回滚

- 隔离证据：`C:/Users/admin/AppData/Local/Temp/brainpunk-onelink-gdc-analytics-20261010/Verify.ps1` 直接编译当前 Manager 非原生 partial、Request 和新 `GameSDKService.OneLink.cs`；仅通用 LogEvent、SDK／Unity 时钟、原生渠道及 Router 等外围为替身。Editor 136、Android 136、iOS 138 项断言通过，共 410 次（含平台重复），日志为同目录 `tests-UNITY_*.log`。
- 已覆盖：空初始值／一次开始、Prepare 缓存窗口不算超时、重复 Initialize 不延长十秒窗口、三动作／平铺及 click_event、af_dp 优先与 campaign、参数无效／自然／非首次、SDK 失败分类、当前无网观察、失败／超时后有效返回、五秒仲裁进关、双向缓存去重、绑定重放、玩法及来源门禁、Dispose、GM 不重开、统计异常不阻断路由；iOS 另核对 ERROR 无网等待及恢复后的同数据去重。
- 测试设施纠正：首轮路由测试复用旧实例，遗留 `_lastRequestKey` 触发原防重；测试改为调用真实 `ResetSession` 以建立独立场景。iOS UDL ERROR 替身将原生非空枚举错误模拟成 null 字符串，导致旧入口在记录日志时抛错；补齐该替身，不修改生产门禁或 UDL 错误处理。早期失败输出仍可从本轮工具记录复查。
- 真实引用定向编译：同目录 `Compile.ps1` 显式加入新 partial；每组 1177 份源码。Editor 0 error／113 warning，Android 0 error／116 warning；iOS 宏复用当前 Bee 引用仍有两条 `Guru.UniKit.Apple` 缺失错误，分别位于 `ChoiceLevelPanel.cs:17` 和 `StartPanel.cs:14`，与本任务前的当日记录相同。因此 iOS 整体编译未通过，不用隔离测试代替真机／原生编译证据。
- 本次文件 `git diff --check` 通过，新脚本 GUID 在 Assets 中唯一；Packages／GF 未改，原有字体、构建信息、项目设置等工作区改动保留，不混入本次范围。未提交 Git。
- `uncertain`：未执行 Unity PlayMode、APK／IPA、真实 SDK 错误内容、ATT／后台暂停、统计配置过滤及后台收数；十秒是 Unity 非缩放等待，不保证原生覆盖层暂停 PlayerLoop 时仍按墙钟触发。验收用隔离测试设备正式包核对 `start → returned`、断网实际失败分类、无回调 `failed/game_wait` 及恢复后 `returned`；确认 UID／完整 URL 不出现在事件参数。
- 回滚只撤销新增业务 API／meta、AF 白名单、Manager 打点调用／失败订阅／观测计时和解析诊断开关；保留原 UDL／GCD／首开优先、奖励存档及其它当日修改，不改只读 SDK 或清玩家数据。

## 2026-10-10：取消每种玩法仅一次的链接进关限制

### 修改前核对

- 需求与基线：用户明确要求取消通过链接跳转关卡仅生效一次的限制。当前 `TryBeginEntry` 在 `OneinlinkEnterType` 已包含目标玩法时拒绝正式请求，主线、Draw、IWanner 共用此判断；GM 已有不受该判断限制且不写历史的例外。本节取代 2026-09-24 的每玩法一次约定，以及 2026-10-09 中正式链接仍限一次的部分；其它规则保持原样。
- 来源与完整入口：系统 URL／AppsFlyer／Facebook／GA4F → `AcceptPayloadCore` → 首页 `TryNavigateFromStartPanel` → `OneLinkLevelRouter.TryEnter` → `TryBeginEntry` → 各玩法原加载流程 → `ConfirmLevelEntered`；失败与退出通过 `EndEntry` 清理本次来源。新的系统 URL 增加 `_activation` 并恢复接收资格，同次回调仍按实际目标去重。
- 维护边界与拟改位置：只删除 `OneLinkManager.cs` 中依赖成功历史的拒绝分支，并同步该方法与旧规则冲突的注释；保留 GameData 不可用及加载／运行中的保护。`ConfirmLevelEntered` 仍记录正式进入过的玩法，旧列表仅作兼容记录，不再决定能否进入；不清玩家记录。已核对 `GameData.cs` 的序列化私有字段、`GameData_Partial.cs` 输出，以及 Bear SaveModule 的 `PartialClassGenerator`，本轮不修改字段、生成器、生成文件或存储结构。
- 保留范围：首页、弹窗、配置开关、资源目标／回退、解锁、加载失败、关内丢弃、同次重复回调、首次安装结果仅处理一次、GM 不写正式历史、奖励编号与用户校验，以及本次链接进入的胜利页行为。已阅读关卡打点生命周期约定，本轮不修改统计或进出关调用顺序，不改 Prefab、Packages、GuruSDK 或 GF。
- 验证计划与未决项：先用实际进关标记区块的隔离测试复现旧列表阻止再次进入，再验证三个玩法已有／空／null 历史、成功退出后再进、旧档重建、加载并发、失败重试、来源清理和 GM；按当前 Unity Bee 引用对修改前后分别定向编译，产物仅写 TEMP。CodeGraph CLI 不可用，引用的 037 feature plan 缺失，知识库“功能开发工作流”未命中，改用仓库规则与源码；真实 SDK、Unity PlayMode、Android／iOS 资源加载及存储仍待验证。

### 实施与验证

- 已实施：删除 `TryBeginEntry` 中 `OneinlinkEnterType.Contains(gameType)` 的拒绝分支，更新该方法中与新行为冲突的摘要注释；运行代码仅此一处。主线、Draw、IWanner 的新有效链接不再因已有正式进入记录而被拒绝，旧档无需清理；成功历史仍按原方式记录，GM 仍不写该历史，当前关卡的 OneLink 来源与胜利页行为不变。
- 隔离测试：`VerifyEntry.ps1` 直接提取并编译当前生产 Manager 的“玩法进入历史与本轮标记”完整区块，DB、日志及区块外请求清理为替身。修改前 46 项通过／12 项失败，失败均为三个玩法的已有历史、首次成功后再次进入、GM 后正式进入和保存失败后再进；修改后 97 项断言通过／0 失败。其它覆盖包括 null 旧列表、已有记录在新会话中再次受理、同／跨玩法并发拒绝、加载失败重试、错误玩法回调不干扰本次来源、重复完成不重复保存、普通新关清理来源及 GM 不写正式历史。两次断言数不同是旧判断失败时未进入后续检查，不将增加的执行分支当作新增独立场景。
- 定向编译：`Compile.ps1 -Mode Editor/Android/iOS -Stage Before/After` 只读复用当前 Unity 6000.0.59f2 Bee 引用，每组 1176 份源码。修改前后 Editor 均 0 error／113 warning，Android 均 0 error／116 warning；iOS 均为相同的 2 error／34 warning，错误分别为 `ChoiceLevelPanel.cs:17`、`StartPanel.cs:14` 缺少 `Guru.UniKit.Apple`，不是本次新增，不修改 SDK。此结果限定当前 Windows 缓存引用，不能据此判断完整 iOS 原生构建。
- 证据目录：`C:/Users/admin/AppData/Local/Temp/brainpunk-onelink-repeat-entry-20261010/`，包含验证脚本、修改前后断言日志及三组编译日志／响应文件。实际方法调用隔离测试不是 Unity PlayMode 或真实磁盘测试；没有 APK／IPA、真实链接回调、手机横屏、异步资源加载和胜利页实测，均为 `uncertain`。真机最短验收：三个玩法各通过正式链接成功进入，退出到首页后重新点击相同链接及另一关目标，应再次进入且识别本次链接来源；加载中或关内点击仍应被忽略，同一奖励编号仍不能重复领取。
- 回滚只恢复该历史拒绝分支及对应摘要注释；保留旧存档和进度，不回退其它任务改动。此次不会迁移／删除玩家数据，也不改变首次安装结果在本次运行内仅开始执行一次的保护。

## 2026-10-10：移除可选调试计时后的发布交付

- 用户明确要求移除Timing及所有调用，并要求处理后重新推送。本次以已发布3135b04d2为父提交，正常快进更新远端feature/deeplink_simon，不改写历史；上游feature/1.15.1_common继续整分支合并。
- 已删除OneLinkManager.Timing.cs/.meta，GameSDKService.cs与OneLinkManager.Rewards.cs恢复为钉定上游7f7706820原样。InstallDdl.cs/.meta保留，五秒窗口和运行内防重没有因精简文件而删除；跨重启恢复仍不交付。
- 相对上游最终只有10个运行／配置／meta路径及两份模块说明。运行文件与本地候选3192a7f8f逐字一致；253项隔离回归通过，四条件Unity引用定向编译0 error、每组1176份源码。测试和过程记录留本地，不随远端交付。
- 没有APK／IPA或真实SDK／投放验收。发布前后核对远端并预演合并；零冲突结论限定当次上游tip，后续并行改动须重新核对。

## 2026-10-10：同步iOS Facebook ATT时序修复——发布前核对

- **授权与基线**：用户要求将已完成A/B/C对照的调整同步远端 `feature/deeplink_simon`。目标远端为 `f9f1162d3`；复用干净稀疏候选 `codex/onelink-1.15.1-merge@3192a7f8f`，其运行文件与该目标一致。最初上游跟踪ref仍为7f7706820，发布前ls-remote发现实际已到ad635cb59；原因是本仓库fetch规则不含common，已用显式refspec刷新，后续以新tip预演，不能把旧跟踪ref当作最新上游。原完整开发分支与远端分叉，不直接push原分支、不强推，也不把整个脏工作区打包提交。
- **需求与现行规则**：iOS14+读取实际系统ATT，不只等Guru缓存；未决定／未知最多30秒、每100ms只读检查，不新弹权限窗；Authorized后SDK就绪与取链共用原五秒，最多请求一次。拒绝／受限／读取异常／超时退出并保留AF及正常游戏；iOS13沿用原路径，Android／Editor不增加权限等待。用户已批准授权等待不占五秒，本节及SRN说明同步该例外，不恢复Timing或跨重启扩展。
- **来源与适配边界**：原完整工程的Facebook／InstallDdl时序修复在13:40前完成实编译和A/B/C真机实验。但远端保留上游HTTP(S)／URI／Extras、非空奖励ID及不同受理签名，不能整份覆盖。远端 `InstallDdl.MarkInstallTargetExecutionStarted` 已无窗口前置条件且锁不被普通链接清除，满足新等待阶段防重，故不改。只移植 `OneLinkManager.Facebook.cs` 的ATT阶段及脱敏诊断，保留原解析函数／首开判据／回调受理接口和主线程；Context区分系统与缓存，结果诊断只记存在布尔及固定错误提示，不输出凭据／标识／原始链接。Extras仅补充存在布尔，不改解析。
- **拟交付及隔离**：相对当前远端只允许Facebook一个运行文件＋OneLink／SRN两份模块说明。测试及当日过程记录留本地候选；原完整工作区、本地QA构建／控制台规避、参考研究、设备日志／包体、Packages／Guru／GF／ProjectSettings／玩法／UI都不交付。037 plan缺失，按当前代码与已确认需求实施。
- **验证计划**：候选原三平台基线150／91／12共253项已通过。沿既有公开hook／SDK回调／首页／玩法边界先红复现授权前发出请求，再迁入已验实现；新增实际／缓存相反、拒绝／受限／超时、SDK就绪占预算、Google AF先执行、显式接管、防重、iOS13和脱敏／Extras兼容，完整回归并以真实Unity引用做四条件定向编译。发布前保留解析函数／其它运行文件的字节证据，用独立index仅导出白名单，正常快进推送后回读远端并预演与钉定上游合并。
- **验收与回滚边界**：A/C为原完整QA包同设备／账号／Helper样本，Facebook自身开关开／关均成功；B旧时序在Facebook开时仍报权限前置错误。它证明目标游戏时序阻塞与当前路径的开关非必要性，不代表所有场景或本发布整合版已重新装机；候选尚须完成本轮测试／定向编译。已发布回滚只能另建提交撤本次增量，不改历史或清玩家数据。

### 迁移结果、验证证据与发布范围

- 已将实测的ATT阶段、原五秒衔接及脱敏诊断迁入Facebook partial。请求循环、截止／取消、系统与缓存快照、固定错误分类与已真机验证的QA实现逐字一致；初始门禁仍保持远端PlayCount=1，不带入本地恢复入口。额外只记录上游Extras是否存在，避免无URL但有Extras时误读诊断；不输出原始值。
- `TryParseFacebookPayload` 与发布前逐字一致，HTTP(S)／URI／Extras、非空奖励ID、受理签名不变。InstallDdl执行锁文件与发布前逐字一致，已有锁覆盖授权等待中Google AF先执行；其它运行文件没有修改。不增加Timing、InstallRecovery、存档键或SDK依赖，不改变GM／IWanner／胜利／RemoteConfig或平台配置。
- **候选红绿与兼容回归**：在未迁移时新增“十秒授权后仍有原生窗口”用例，旧版预期0请求、实际1请求（原91项通过，新1失败）；移植后92项通过。完整扩展后Android165／iOS128／Editor13／Editor+iOS13，共319项通过，覆盖拒绝／受限／未知30秒、缓存相反、SDK就绪仍占五秒、权限撤回、异常脱敏、明确接管、后台截止、同步重复回调、iOS13、Extras及非空ID、无新增存储、原有会话防重等。
- **真实引用定向编译**：Editor／Android／Android Debug／iOS四组各1176份上游＋候选源码均0 error，分别113／110／110／110条既有警告。输出全在TEMP，沙箱禁止写原／候选工程和SDK；未启动Unity或重打本发布组合APK／IPA。编译和回归证据 `/tmp/brainpunk-onelink-att-publish-20261010-1345/`，包括 `red-tests.log`、四组测试日志、`real-compile/summary.json` 和 `compatibility.safe.json`。原完整QA的317项与本发布候选319项是不同集成基线，不混用计数。
- **真机证据范围**：原完整QA A组12:50:33授权后请求、12:50:37完成Level_022保存；B旧时序13:09:17请求、13:09:18权限错误、13:09:30才授权；C在Facebook自身跟踪关闭时13:39:02授权后请求、13:39:06完成保存。A/C均有Facebook选中、AF仅NOT_FOUND、无系统URI；指向目标游戏时序是实际阻塞，当前Helper历史条件下Facebook自身开关开不是必要条件。原始设备／凭据／日志只留本机；远端本节仅含脱敏摘要，不扩大为商业投放／商店安装或所有版本验收。
- **发布操作**：候选测试与过程记录只保存在本地提交；以当前远端f9f1162d3为父提交，独立index仅写入Facebook脚本及本模块／SRN两说明，保留远端旧发布说明而不整份覆盖本地QA历史。正常快进push后须回读目标tip并预演合并；在回执确认前不把“候选完成”说成“远端已更新”。
- **最新上游兼容补查**：ad635cb59已合入f9f1162d3，另有取消重复进关限制及关卡／UI调整；Facebook文件与f9仍相同，新ATT改动不触碰这些路径。本次不将上游其它文件夹带到Simon三文件更新中；模块新增说明插入旧发布说明之后，避开上游在文首新增的取消次数章节。后续预演必须保留最新Manager／其它上游文件，零冲突不等于上游可直接快进，相关结果另行记录。
- **最新合并验证结果**：与ad635cb59执行merge-tree预演退出0，合并树相对该上游只改变相同三条白名单路径，Manager及其它上游路径字节均保留。使用验证脚本的Git树模式直接编译该合并树，不拿候选旧Manager覆盖新上游：Editor／Android／Android Debug／iOS四组各1176源码再次全部0 error，证据 `latest-merged-compile/summary.json`。本轮是冲突检查及真实引用编译，不是对最新上游玩法UI做真机回归，也不承诺无快进合并提交；上游后续移动须重新预演。

## 2026-10-10：按用户要求移除调试计时——修改前核对

- **确认范围**：用户只要求调整 `Timing.cs/.meta` 及全部计时调用，不要求删除或合并 `InstallDdl.cs`。本轮撤销调试计时，保留原生共享五秒窗口、当前运行内执行锁、非空奖励 ID、上游 AF 网络恢复与 GM／IWanner／胜利规则；不恢复已撤销的跨重启功能。
- **来源与拟改位置**：当前发布 `3135b04d2` 的 Timing partial 仅在 Android 调试条件输出 `OneLinkTiming`，调用点分布于主 Manager、Attribution、GameSDKService、Rewards。拟删除该 partial/meta 和所有调用、仅为计时新增的 AppsFlyerHook.Initialize；删除测试 TraceTiming 空实现以确保编译不掩盖漏清调用。GameSDKService／Rewards 将与钉定上游 `7f7706820` 逐字一致，不修改 SDK／RemoteConfig 行为。
- **验证计划**：核对全部生产 C# 与测试桩中无计时符号，InstallDdl 与本轮前候选逐字相同；重新执行三平台既有 253 项回归及四条件 Unity 引用定向编译，确认正式／调试编译均无悬空引用。原有阶段日志保留，只有专属计时诊断被移除；完整 APK／IPA／真实投放仍未验收。
- **发布与回滚**：在隔离候选修改，以后续正常快进提交更新远端 `feature/deeplink_simon`，不强推；原开发目录及并行 QA 保留。若以后需要恢复计时，单独批准并新增提交，不回退其它业务或玩家数据。实施结果见下节。

### 计时精简结果与验证

- 已删除 `OneLinkManager.Timing.cs/.meta`、19处外部计时调用、仅用于计时的 AppsFlyerHook.Initialize，以及测试中的 TraceTiming 空实现；SDK运行代码和测试桩中无 TraceTiming／OneLinkTiming／诊断时钟字段残留。上游原有阶段日志保留。
- `GameSDKService.cs`、`OneLinkManager.Rewards.cs` 与上游 `7f7706820` 逐字相同，最终不再产生发布差异。InstallDdl／Facebook／GA4F／DdlUrl 及 InstallDdl.meta 与本轮前候选逐字相同；本次没有删除或合并 InstallDdl，也没有改变五秒窗口和运行内消费锁。
- 重新执行隔离回归：Android150／iOS91／Editor12，共253项通过、0失败。重新执行四条件 Unity Roslyn 定向编译，每组1176份源码：Editor 0 error／113 warning，Android、Android Debug、iOS各0 error／110 warning。`git diff --check`通过。
- 证据目录 `/tmp/brainpunk-onelink-no-timing-20261010/`；编译命令使用现有只读验证脚本加该目录的 `real-compile` 输出参数，没有启动Unity、构建APK／IPA或操作真实SDK。原工作区与并行QA仍未覆盖。
- 相对钉定上游的最终交付收敛为10个运行／配置／meta路径＋2份模块说明；去掉Timing的两个新增路径以及GameSDKService／Rewards的两个修改路径。下面当前运行清单同步移除这四项，其它历史章节的计时描述仅代表旧版本。

## 2026-10-10：按用户要求撤销跨重启恢复——修改前核对

- **新授权取代范围**：用户明确表示上游没有跨重启恢复、不需要这项本地扩展，要求以上游为准。本节取代下方先前方案 A 中“保存待执行目标／消费状态并跨重启恢复／阻止重放”的额外持久化要求；不再将跨进程单次消费作为本次交付承诺。此前发布 `d82ca686c` 的历史保留，以后续快进提交撤回，不强推。
- **当前基线与拟改文件**：已发布版本新增 `OneLinkManager.InstallRecovery.cs/.meta`，在主 Manager、Facebook、GA4F、InstallDdl 和 Attribution 中接入 PlayerPrefs 记录、恢复、关闭及等待。拟删除这一新增 partial/meta，移除所有新恢复调用和来源枚举参数，原生探测回到 `DB.GameData.PlayCount == 1`；不读、不写、不清理历史恢复键，不清玩家领取历史。
- **必须保留**：上游 Attribution 内已有的 iOS 无 URL ERROR／联网恢复、SDK 公开安装数据订阅与缓存探测不是本次跨进程恢复，不删除 `_installRecoveryAccepted` 等既有状态。非空奖励 ID、上游 Facebook URL／Extras、GM／IWanner／胜利、原生五秒优先和普通活动链接规则不变。
- **剩余防重边界**：仅保留当前运行内的安装目标执行锁，开始执行后不同 ID 的晚到结果不能二次执行；移除持久化兜底后，该内存锁须覆盖原生窗口尚未建立时 AF 已消费的情况，并在同一运行内不因后续普通链接清除。进程退出时未消费目标丢失，后续是否取得目标依原 SDK 回调；已领取同 ID 仍由上游 GameData 记录防重，但不再保证重启后的不同 ID 属于同一份资格。
- **验证计划与边界**：沿既有 SDK／系统链接／首页／重建会话测试入口，先复现重启不应恢复但旧代码仍恢复，再移除扩展；检查无新增 PlayerPrefs 读写、忽略遗留 pending／closed 记录、非首次不重查原生缓存、同进程不同 ID 晚到／背包重入／普通活动后晚到、原有 iOS ERROR 恢复仍可用。重新执行三平台隔离回归和四条件定向编译，结果待补；真实 SDK／存档／APK／IPA 仍未验证。
- **维护与回滚**：仅修改隔离候选及后续净化发布；原开发工作区与并行 iOS QA 不覆盖，SDK／Packages／GF 只读。撤销本轮需另行批准恢复该扩展，以新提交处理已发布历史，不清档或改写远端历史。

### 撤销结果与验证

- 已删除新增 `OneLinkManager.InstallRecovery.cs/.meta`，移除主 Manager／Facebook／GA4F／InstallDdl 的持久化、恢复、等待和来源枚举参数；Attribution 恢复调用原 `AcceptPayload`。原生探测只在 PlayCount=1 启动，不因历史恢复记录在后续启动补发。现有 `_installRecoveryAccepted`／网络恢复订阅／缓存探测保留，属于上游既有能力。
- 内存消费锁不依赖原生窗口是否已建立，执行后也不会被同一运行内的普通新链接清除；普通链接本身继续可受理。没有新增 PlayerPrefs 读取、写入或删除，旧恢复键如已存在只作为无人读取的数据保留，玩家奖励 ID 历史不清理。
- 红灯用例“Facebook已受理未执行目标退出后不恢复”在撤销前预期 0 次发奖、实际 1 次；删除扩展后通过。原恢复测试从当前测试项目移除，另以 `SessionLifetimeCases` 检查无恢复／无新增存储访问、遗留记录不影响新 SDK 回调、当前运行消费锁和原奖励 ID 防重。最终 Android 150／iOS 91／Editor 12，共 253 项隔离回归通过，含上游 iOS 无 URL ERROR 恢复用例；之前 312 项是撤销前历史，不能再作为当前用例总数。
- 重新执行四条件 Unity Roslyn 定向编译，每组 1177 份源码：Editor 0 error／113 warning；Android、Android Debug、iOS 各 0 error／110 warning。命令 `python3 /tmp/brainpunk-onelink-merge-20261010/compile_candidate.py /tmp/brainpunk-onelink-no-restart-20261010/real-compile`；测试日志在后者父目录。源码清单不再包含已移除恢复 partial，不带本地并行 iOS QA。
- 本次相对首次发布仅撤回该扩展和必要调用，保留非空 ID、原生优先、GA4F 配置及上游其它功能。当前运行外的不同 ID 重领不作保证，未执行 APK／IPA 或真实 SDK 测试。远端发布使用新提交，不改写已发布历史；当前运行文件清单已从旧版移除两个恢复路径。

## 2026-10-10：1.15.1 渠道接入合并与非空奖励 ID——实施前核对

- **授权与基线**：用户完成来源／奖励 ID 讨论并明确要求“调整，然后处理和上游的冲突”。以上游 `feature/1.15.1_common@7f7706820` 为完整发布底座，在独立稀疏 worktree `codex/onelink-1.15.1-merge` 整理最小业务增量，不修改原 `feature/deeplink_simon@68a1df094` 工作区；不推送。037 feature plan 缺失，CodeGraph 目录只有忽略文件，无可用索引；依据当前源码、上游模块／GM 文档与本轮确认实施。
- **有意变更及当前协议**：Facebook／GA4F／AF `af_dp` 的奖励 ID 对齐通用 `OneLinkRequest` 非空 ID 规则，解除仅 `coindl/skindl` 的限制；同步调整原生目标恢复校验。campaign 保留既有 `DL-GC-数量`／`DL-GS-SkinId` 命名与固定 ID 映射，不实施讨论中未定稿的新 campaign 格式。Facebook 保留上游 `TargetUrl/Url/Extras`、HTTP(S)／业务 URI 解析能力；GA4F／`af_dp` 保留原固定业务 URI 和完整目标边界，不据 ID 放宽新增 URL 类型。
- **方案 A**：Meta 使用 Facebook 优先、Google Android 使用 GA4F 优先，AF 并行兜底、原生共享五秒预算；AF 内部 UDL／完整 GCD／campaign 仲裁保持。合法 `af_dp` 优先于 campaign；来源不一致时选整份目标，不拼 ID、数量或用户限制。一次安装目标在执行前落盘消费，不以投放填写的 RewardId 作为该消费锁；执行开始后不同 ID 的晚到结果与重启均不重复执行，失败也不换来源重试。后续普通链接按原规则处理；不承诺清档／存档回滚／换设备后的服务端防重。
- **冲突解决边界**：只保留一套 FacebookDdlHook，复用既有 SDK 初始化；迁入本地主线程修复、GA4F／InstallDdl／InstallRecovery／Timing 及必要调用点。保留上游 Facebook 脚本 GUID、GM 来源权限、玩法历史例外、IWanner Group／精确小关 Router、Request 和胜利／玩法／UI 的上游字节。Manager 受理接口维持上游 `resolvedLinkSource/bypassEntryHistory` 调用契约，新增来源及恢复参数仅供真实接收点使用。Package／Guru／GF 不物化到候选工作区、不写入；不混入依赖快照、字体、生成资源、QA 隔离构建器或并行 iOS 调试。
- **验证边界**：沿已有 SDK 回调／系统链接／GM 接口→真实 Manager／Request／奖励执行→首页消费的隔离测试入口，先复现自定义 ID 被拒绝，再验证非空／空 ID、原生优先、不同 ID 晚到、奖励同步重入、跨进程恢复、消费保存失败及 GM 历史例外；测试时钟、SDK、PlayerPrefs、背包／UI／Router 仍为替身。静态核对 Request／Router、SDK／Packages、玩法／胜利等未授权路径相对上游无差异。定向编译与真实 Unity／APK／IPA／投放验收分开报告，不操作用户设备或后台。
- **状态与回滚**：此节作为代码修改前核对，实施结果见下节；合并后真实运行验收仍为 `uncertain`。回滚仅撤销候选相对钉定上游的本次增量，不清除玩家历史或恢复记录，不覆盖原工作区。文档历史的固定 ID、旧 Facebook Prepare 与“最近目标覆盖”说明以本节限定范围为准；原有平台验证不自动变成本次合并验证。

### 实现与验证结果（首次整合历史，跨重启部分已按文首撤销）

- **完成范围**：候选已整合原生优先／安装恢复、非空奖励 ID 和上游接口。Facebook 使用上游原样的 `TryParseFacebookPayload`；GA4F／`af_dp` 仍使用受控业务 URI，但共用 `OneLinkRequest` 非空 ID 校验。恢复不再拒绝自定义奖励 ID，Facebook 缺省关卡继续按上游语义恢复；GA4F 仍要求完整关卡目标。campaign 源码未改。
- **冲突处理**：只注册 FacebookDdlHook、Ga4fDdlHook、AF transmitter、AppsFlyerHook 四项，无旧 FacebookHook／QA 隔离注册；Facebook `.meta` 使用上游 GUID。Manager 新增安装状态，但上游 GM 进关历史区块逐字保留，受理签名兼容 `resolvedLinkSource/bypassEntryHistory`。Request／Router／Debug／Url／SrnCampaign 文件与上游逐字一致，玩法、胜利 UI、资源、SDK、ProjectSettings 不叠加本地差异。两边文档不整份互相覆盖：保留上游历史，新增本次实际协议、证据与边界，不混入本地 iOS QA 日志。
- **先红后绿**：基线 Android 153 项通过；新增 `af_dp` 非空 ID 用例预期 250、实际 campaign 100；入口放宽后恢复用例预期 250、实际 0；Facebook Extras 兼容用例预期 125、实际 0。分别调整共享 URL 校验、恢复校验及保留上游解析后转绿。最后使用 `tools/tests/FacebookDdl/FacebookDdl.csproj` 的三平台宏执行 Android 180／iOS 120／Editor 12 项，共 312 通过、0 失败。旧固定 ID 拒绝用例改为空 ID 拒绝并新增正向用例，未通过删除非法输入覆盖伪造通过。
- **真实引用定向编译**：`python3 /tmp/brainpunk-onelink-merge-20261010/compile_candidate.py` 以 Unity 6000.0.59f2 Bee 引用编译上游源码与候选增量，每组 1178 份 C#；Editor 0 error／113 warning，Android 非调试、Android Debug、iOS 各 0 error／110 warning。只读复用依赖及编译引用，输出全在 TEMP，并用 sandbox 拒绝原／候选工程与 `~/.guru` 写入。移除 Bee 清单中旧的独立 SrnCampaignTargetMapper 项及不属于候选的 iOS QA 源码，不添加替代生产桩；源码、rsp、日志及 `summary.json` 留在同目录 `real-compile/`。这不是完整 Unity 导入或原生构建，警告未逐项归因。
- **静态核验**：`git diff --check` 通过；代码无冲突标记，索引无 unmerged 条目；Manager 玩法历史区块与 Facebook 解析函数对上游精确比较通过。Manifest XML 除 `google_analytics_deferred_deep_link_enabled=true` 外结构相同；四个新脚本 GUID 在上游 Assets 中无碰撞。Packages／GF／Guru 在稀疏候选中未物化，原工作区保留既有及并行修改。
- **未验证／交付边界**：未构建 APK／IPA、运行 Unity 或操作真实 SDK／投放后台。iOS ATT 最终结果与取链时序的既有缺口没有借合并修复；真实存储故障、IL2CPP、SDK 返回顺序、IWanner 资源加载与胜利 Replay 仍需正式包回归。候选处于独立本地分支，不代表原 `feature/deeplink_simon` 已切换或远端已更新。回滚仅针对本次候选提交，不清理玩家数据或原工作区。

### 相对上游的运行文件清单

以下路径均位于 `BrainPunk/`，不包含上游原有而未改的玩法／UI 文件：

```text
Assets/Game/Scripts/SDK/SDKImps/GameAnalytics.cs
Assets/Game/Scripts/SDK/Services/Core/OneLinkManager.cs
Assets/Game/Scripts/SDK/Services/Core/OneLinkManager.Attribution.cs
Assets/Game/Scripts/SDK/Services/Core/OneLinkManager.DdlUrl.cs
Assets/Game/Scripts/SDK/Services/Core/OneLinkManager.Facebook.cs
Assets/Game/Scripts/SDK/Services/Core/OneLinkManager.Ga4f.cs
Assets/Game/Scripts/SDK/Services/Core/OneLinkManager.Ga4f.cs.meta
Assets/Game/Scripts/SDK/Services/Core/OneLinkManager.InstallDdl.cs
Assets/Game/Scripts/SDK/Services/Core/OneLinkManager.InstallDdl.cs.meta
Assets/Plugins/Android/AndroidManifest.xml
```

远端仅附本模块文档与SRN交接清单；回归测试和当日过程记录留在本地候选3192a7f8f，不进入此次发布差异。

## 2026-10-09：IWanner sub2 区分 Group 与小关，并准备 Facebook Android 真链测试

- **需求与基线**：用户明确要求 IWanner Group 使用 `IGroup_{id}`，小关使用 `ILevel_{num}`。修改前当前分支仍把 IWanner 的资源 Path 或配置 level ID 解析为所属组第 0 小关，与 `DevelopMD/OneLink指令配置说明.md` 已记录的新协议不一致；本节替换下方旧章节中“IWanner 指定目标进入所属组首关”的现行含义。
- **修改范围与行为**：`OneLinkLevelRouter` 仅在 IWanner 分支解析大小写精确的 `IGroup_` 正整数后缀，并按 `IWannerLevelSort.Id` 查 Group；调用既有可空 `levelIndex` 入口，使同组续接有效存档、换组从索引 0 开始。`ILevel_XXX` 仅按当前有效配置中的精确资源 Path 匹配，并传实际零起始组内索引，强制进入该小关。裸组号、裸 level ID、错误大小写、缺失目标或未命中配置继续走既有最低未通关回退；`ILevel_999` 特例保持不变。主线、Draw、Facebook/AppsFlyer 参数接收、首页门禁、解锁/保存和打点链未改。
- **Facebook/Google Play 测试边界**：当前 Android 包名为 `com.brain.tricky.puzzle.games.test.quest`，自定义 Manifest 已声明 `brain.gurugame.ai/default_link`、`brainpunk.onelink.me/IjED` 和 `uribrainpunk://`；两份线上 `assetlinks.json` 于 2026-10-09 均返回 200 且包含该包名。Facebook App ID 与 SDK 依赖已在项目配置中，但 Meta 后台的 Android Package/Class、Key Hash、Deep Linking 开关、广告 Deferred Deep Link，以及 Google Play 测试轨包体仍无法由本地仓库证明。
- **验证证据**：复用隔离 Router harness，对 Editor、Android、Android+DEBUG_MODE、iOS、iOS+DEBUG_MODE 各通过 647 项断言，覆盖本地 15 个 Group/75 个小关、同组续关、换组、精确小关、非法/旧参数回退、TBN、Facebook URL/Extras 与 AppsFlyer/GM 入口；外部 SDK、Unity 加载和 UI 为替身。复用 Unity 6000.0.59f2 Bee 引用的定向编译，Editor 为 0 error/113 warning、Android 为 0 error/116 warning；不是完整 Unity 导入、Gradle/AAB 或真机验证。
- **未验证风险与回滚**：`uncertain`：尚无连接的 ADB 设备，也没有本次代码构建的 Google Play 安装包、Meta 测试广告点击、真实 `FetchDeferredAppLinkData` 回调或实际关卡画面。正式验收必须从 Meta 安装广告点击进入 Google Play，在未安装状态完成安装并首次启动，核对 `facebook_requested → facebook_callback → queued → target_resolved → entry_accepted`，并以对应 `level_start`/画面作为最终证据。回滚时恢复 Router 原 IWanner 匹配块、`OneLinkRequest` 与 `IWannerPlayCtrl` 的相关注释，并同步撤回本节和指令文档；不要删除玩家存档或回退 Facebook Hook。

## 2026-10-09：Facebook 延迟深链适配 deeplink 分支——修改前核对

- **需求与来源**：将 `feature/1.16.0_common` 的 `405924dea` 中 Facebook `FetchDeferredAppLinkData` 接入移植至当前 `feature/deeplink`（基线 `8bf55496b`），按目标分支现有功能适配，不整份覆盖旧版 Manager，不带入源提交的 IWanner 路由修改。
- **原行为与证据**：`GameAnalytics` 仅注册 AppsFlyer 接点；`OneLinkManager` 已具有 UDL、首次安装 GCD、iOS 安装恢复、真实 ERROR 单次重试、首页门禁和玩法进入后丢弃规则。`Url.TryParseUrlQuery` 支持 sub1–sub3；`OneLinkRequest` 支持 open_level/getCoin/getSkin；`Rewards.ExecuteReward` 负责 Uid 校验、领取 ID 存档防重、背包发放和保存成功后的纯展示弹窗。`CanUdlReplacePendingGcdReward` 允许真实 UDL 完整接管尚未消费的 GCD 奖励。Packages 中 Guru 的 `GuruAnalytics.Facebook.InitFacebook` 已负责 FB.Init，游戏侧不得重复初始化。
- **拟改位置与原因**：新建手写 `OneLinkManager.Facebook.cs` partial；`GameAnalytics` 增加与 AppsFlyer 同阶段的 Facebook Prepare 接点；Manager.Dispose 取消 Facebook 会话等待。FB 回调先切主线程、核验生命周期和错误，再把 TargetUrl（优先）/Url 或 Extras 转成当前协议，复用现有 URL 白名单和请求解析；保留 sub3，固定 `uribrainpunk://link` 与 HTTP(S) 可用，其它 scheme 拒绝。短链无业务参数且无 Extras 时不猜目标、不额外联网展开。
- **仲裁与边界**：Facebook 作为已解析的 deferred 通道，沿用 UDL 的新点击/GM 优先、玩法内及作废轮次丢弃、GCD 已消费后拒绝、iOS 恢复仅待执行奖励可接管的规则；当前按 activation/实际目标或 RewardId 去重，不移植旧版只按玩法去重键。尚未消费的 GCD 奖励允许 FB 与真实 UDL 相同的完整参数接管；不同有效目标仍沿用最近受理者覆盖待执行请求的原则。共用受理核心增加准确来源日志，不改变原两参数 AcceptPayload。Facebook 失败不触发 AppsFlyer ERROR 重试，不撤销原 GCD 候选。
- **不修改范围**：Request/Router/Rewards 的业务、三玩法历史、玩家存档、UI/Prefab、配置/RemoteConfig/Loading、统计发放、GM 和 AppsFlyer 原生标识隔离均保留；Packages/GuruSDK/GF 只读。开工已有 gradle/build_info/AndroidResolverDependencies/guru_gf_settings 与未跟踪 `DevelopMD/OneLink指令配置说明.md` 均属用户改动，排除本任务。
- **验证计划及缺口**：实际生产 partial 的隔离测试覆盖三指令/URL/Extras/UId、失败/取消、初始化一次/退出清理、UDL/GCD/FB 先后与重复、待执行奖励接管、已消费拒绝、新链接/GM、三玩法丢弃和首页门禁；使用现有 Unity 引用定向编译，产物仅写 TEMP。完整 Unity/IL2CPP、真实 Meta 首装返回与 Android/iOS 网络/签名均为 `uncertain`。引用的 037 plan 和本地 CodeGraph 索引不存在，以当前源码及上述模块记录为依据。回滚只撤销本次接点/FB partial/共用核心来源增量，不删存档、不回退目标分支已有功能。

### 最终实现与数据流

- `GameAnalytics` 顺序为 GuruAppsflyer Prepare → AppsFlyerHook → FacebookHook。FacebookHook 立即返回；Android/iOS 非 Editor 的正常运行会话异步等待 `FB.IsInitialized` 后请求一次 `FB.Mobile.FetchDeferredAppLinkData`。编辑器/其它平台不调用原生接口；退出/会话重置取消等待，旧原生网络结果由取消令牌丢弃。不重复调用 FB.Init，不影响统计事件能力。
- 新 `OneLinkManager.Facebook.cs` 独立承载等待、回调、规范化和接点；Manager 仅增加 Dispose 清理，并把原真实 UDL 的受理来源标识扩展为已解析通道来源。`AcceptPayloadCore` 仍使用原 activation/实际关卡目标或 RewardId 去重；`CanResolvedLinkReplacePendingGcdReward` 保留原尚未消费、新旧均为奖励、GCD/安装恢复来源三个条件，日志分别记 `source=udl` 或 `source=facebook`。
- 回调 → 主线程与会话/错误/关内/新点击/GM/已消费 GCD 校验 → TargetUrl/Url/Extras 白名单 → 当前 OneLinkRequest → 共用 pending → StartPanel.LateUpdate 首页门禁 → 原 Router 或 ExecuteReward。Facebook 不是另一套路由，不直接开 UI、读写存档或发奖；动作支持 open_level/getCoin/getSkin，奖励 sub3 由原账号校验处理。合法协议但 Uid 不符/皮肤不存在时仍由执行层拒绝，不新增自动回退。
- 例：`uribrainpunk://link?deep_link_value=getCoin&deep_link_sub1=fb_oct09&deep_link_sub2=200&deep_link_sub3=<GuruUid>`；`https://brain.gurugame.ai/default_link?deep_link_value=getSkin&deep_link_sub1=fb_skin_oct09&deep_link_sub2=<SkinId>`；跳关参数和 IWanner 目标解释完全沿用本分支原 Router，不移植源提交的组/小关新规则。不要以旧已领取 ID 验收再次发奖。
- 注释变更仅为使原“只有 UDL 可接管”的描述与新增真实 FB 通道一致，保留无关注释。开工后 HEAD 增至 `ccfbee36f`（新增 GamePlayPanel/首次引导提交），`git diff 8bf55496b HEAD -- Assets/Game/Scripts/SDK` 为空；本轮不撤销这些提交，并在已有当日共享文件中追加主题。

### 验证证据、未验证项与回滚

- TEMP 目录：`C:/Users/admin/AppData/Local/Temp/brainpunk-facebook-deeplink-20261009/`。`Harness.csproj` 直接编译当前 OneLinkManager 全部 partial 及 OneLinkRequest；`Harness.cs`、`Stubs.cs`、`Verify.ps1` 和 `tests-*.log` 可复查/复跑。Editor、Android、Android DEBUG_MODE 各通过 185 次断言，iOS、iOS DEBUG_MODE 各通过 187 次，共 929 次（含平台重复）。Unity、FB/AF 网络、UI/背包/存储/关卡 Router 为替身，不能把这些数字当成真机验收。
- 覆盖三指令、HTTP/固定 URI、TargetUrl 优先、typed/JSON/内层 Extras、sub3 保留、重复参数/非法 URI/错误 JSON/Ref 拒绝、单次初始化等待与退出失效、旧原生标识隔离、UDL/FB 双向同目标防重与不同目标覆盖、GCD 未消费接管与已消费拒绝、SRN 映射、iOS ERROR 无 URL 实际恢复方法的 FB 接管、UID 等待/不符、金币溢出/无效皮肤、保存失败内存防重、同步背包重入、纯展示一次、三玩法关内与回首页作废、启动/Loading/焦点/Popup/面板/首页状态门禁。首次 iOS harness 失败来自替身 ERROR 枚举用 null 字符串与未绑定 AF；按实际调用前提修正替身后五宏重跑通过，未为测试修改生产门禁。
- `Compile.ps1` 使用 Unity 6000.0.59f2 Roslyn 和现有 Bee 工程引用，输出全在 TEMP：Editor 0 error/113 warning，Android 0 error/116 warning，真实 FB SDK 接口参与编译。Bee 源码清单有 21 份已不在当前分支的 Dig 脚本，脚本记录到 `compile-removed-absent-sources.txt` 并移除这些不存在项，补入全部当前 OneLink partial；因此属于复用缓存引用的定向编译，不是重新导入后完整 Unity 编译，警告未逐项归因。
- iOS 同样定向编译失败：`ChoiceLevelPanel.cs:17`、`StartPanel.cs:14` 缺少 `Guru.UniKit.Apple` 命名空间。该问题已在本分支首次引导记录出现，本轮没有修改这些文件或 SDK；不能声称完整 iOS 编译通过。`git diff --check` 无空白错误（仅已有 LF/CRLF 提示）。Packages/GuruSDK/GF 无本轮改动。
- `uncertain`：FB 初始化若永不成功，等待直到会话退出但不阻塞 Loading；无业务参数的不透明短链不额外展开。真实 Meta 点击后商店首装、双 SDK 返回顺序、签名/归因权限、Android/iOS 原生网络、实际横屏 UI/存档和 IL2CPP 未实测。用隔离安装与未领取 ID 覆盖成功/无参数/断网、重复返回、新点击、首页遮挡和关内晚到，并查 `facebook_requested → facebook_callback → queued/reward_queued → entry_accepted/reward_completed`；GCD 奖励接管查 `gcd_reward_replaced, source=facebook`。
- 回滚只撤销 GameAnalytics 的 FacebookHook、删除本次 FB partial/meta、移除 Dispose 的 FB 清理并恢复原受理来源标识；同步标记本节/当日记录。既有 AppsFlyer/GCD/奖励/首次引导和玩家领取历史不回退。此次适配留在 `feature/deeplink` 工作区，未提交或推送 Git。

## 2026-09-29：以上游为基线移植受控 SRN 关卡／奖励目标——修改前核对

- **需求与原行为**：以 `origin/feature/deeplink` 的最新业务、GM 系统 URL 测试与真实 ERROR 单次恢复为基线，只移植 `feature/deeplink_simon` 已提交的受控 GCD 目标逻辑及相邻投放约定；保留当前工作区全部未提交文件，不把历史插件或 Packages 差异带给上游。上游 `OneLinkManager.Attribution` 仅接受已有完整 `OneLinkRequest` 的首次非自然安装 GCD，iOS 无 URL 安装补偿也只读取原载荷；没有从受控 `af_dp` 或 campaign 尾标恢复目标。上游 `OneLinkRequest` 和奖励执行层已支持现有完整字段中的关卡、金币与皮肤请求。
- **来源与维护边界**：只改本目录 `OneLinkManager.Attribution.cs`、新增的 `OneLinkManager.DdlUrl.cs`／`OneLinkManager.SrnCampaign.cs` 及各自脚本 `.meta`，并更新本文与 `SRN渠道GCD参数与投放交接清单.md`。从已提交的 Simon 版本取五份源码／meta，不引入其 `GameSDKService` 历史门禁、其它 OneLink partial、GM Popup、Packages／GuruSDK／GF、第三方插件、存档或测试临时文件。原 `Url.IsDirectUri/TryParseUrlQuery` 与 `OneLinkRequest.TryParse` 为共用入口；原 `Attribution` 的首装门禁、五秒 GCD／UDL 仲裁和 iOS 网络恢复继续作为目标受理外围。
- **拟改位置、影响和验证计划**：GCD 有效受控广告 URL 优先、旧完整业务字段次之、严格 campaign 尾标兜底；iOS 安装补偿复用同一转换。广告来源仅 Meta 双端／Google Android，URL 必须为固定业务 URI；campaign 保留既有 `DL-OL` 关卡并新增 `DL-GC` 金币／`DL-GS` 皮肤，奖励固定领取 ID 由原有本地存档去重。非法 URL 回退公共 campaign 时可能丢失 URL 中的 Uid 限定，用户此前已选择沿用原回退行为，投放不能将其当作严格定向验券。对照五份源码与已提交 Simon blob、文档约定及上游 API；验证编译、映射边界、差异严格限于七个目标文件，并确认上游 GM／ERROR 单次恢复不被覆盖。完整 Unity、真机 SRN 回调、正式发奖和商店首装均为 `uncertain`；回滚仅撤销本次目标转换与约定，不动原协议和玩家存档。

### 隔离移植验证与交付边界

- 五份代码与 `.meta` 的 Git blob SHA-1 分别等于已提交的 `a6589801b` 中同路径版本；`OneLinkRequest.cs` 在上游和原 Simon 分支相同，`Url.IsDirectUri/TryParseUrlQuery` 在上游与原隔离测试使用的源码方法体一致。`TryResolveGcdTarget/IsSameAdAndCampaignTarget` 方法体与已测试的提取版本去空白后相同。复用 `/tmp/brainpunk-onelink-reward-map-20260929/` 的隔离 .NET 测试运行 Android／iOS／Editor 宏，分别通过 **120／115／115 项断言**，覆盖关卡、奖励、来源、非法 URL、原字段优先和冲突目标；外围 Unity／SDK 类、真实 UDL/GCD 时序和下游发奖仍为替身或未运行，不能据此推断真实广告发奖成功。
- 以最新上游基线建立隔离稀疏工作树，只修改这五份源码／meta 和本目录两份文档；上游 `OneLinkManager.Debug/Retry/Url`、GM Prefab 与 `GameSDKService` 保留，不把 Simon 旧三次 ERROR 策略、iOS 启动等待或其它历史验证覆盖上游新行为。对比上游检查差异范围、`git diff --check` 和五份 blob 一致性；未执行 Unity 导入、全量编译、IPA/APK 构建、真机 SRN 安装或跨设备防重，均为 `uncertain`。若需撤回，以上游父提交为基线撤销本次七文件提交；保留已有 `OneLinkRequest/Rewards`、玩家 GameData 与上游 GM／ERROR 实现，不删除旧存档或改只读 SDK。

> 当前进关次数规则以本文顶部「2026-10-10：取消每种玩法仅一次的链接进关限制」为准：新的有效链接可再次进入。下方旧章节中的每玩法一次和 IWanner 组首规则仅代表当时基线，IWanner 当前目标协议见 2026-10-09 的 Group／小关章节。

> 奖励协议另见末尾「2026-09-29：getCoin / getSkin 奖励指令」；仍以 sub1 在 GameData 中独立防重，不随本轮取消进关次数限制而放宽。

## 需求与基线（2026-09-22）

- 用户要求收到链接后，在 StartPanel 进场动画及启动弹窗全部结束后自动进入对应玩法。不限首次安装；游戏中收到请求保留到返回首页。
- 最终协议：`deep_link_value=open_level`，`deep_link_sub1=main/draw/iwanner`，`deep_link_sub2` 为资源 Path，例如 `Level_002`、`DLevel_002`、`ILevel_002`。撤销本轮早期按排序号／组号解析 sub2 的方案。
- 路径存在时解锁并进入目标；未传、空白或当前可进入配置中找不到该路径时，按对应玩法的最小未通关规则回退。不要把资源名后缀当作排序 ID。
- 原项目已有 Guru AppsFlyer 初始化和旧归因回调，但没有游戏侧 OneLink 路由。原首页只有手动入口；普通 IWanner 选组同组恢复索引、不同组从第 0 小关开始。
- 未找到既有 OneLink 专项文档；AGENTS 引用的 `../specs/037-remote-resource-loading/plan.md` 不存在。本实现依据当前代码、UI 文档和用户本轮确认，不修改已验收关卡脚本。

## 协议与当前本地配置证据

| sub1 | sub2 示例 | 对应玩法 | 当前本地 JSON 的解析结果 |
| --- | --- | --- | --- |
| main | Level_002 | GameType.Default | LevelData 10002，排序 ID 2 |
| draw | DLevel_002 | GameType.Draw | DrawLevelData 20002，排序 ID 6 |
| iwanner | ILevel_002 | GameType.IWanner | IWannerLevelData 30002，组 ID 1，组内索引 1（第二小关） |

- 证据为 `Assets/Game/Configs/tb{level,drawlevel,iwannerlevel}{data,sort}.json`；运行时仍以控制器当前生效的数据为准，包括 Draw 已提交的远端排序，不改 RemoteConfig 初始化或刷新时序。
- 路径去首尾空白，按 `LevelData.Path` 忽略大小写匹配，进关仍使用配置中的原始 Path；不接受任意资源地址，不按下划线后的数字拼接配置 ID，也不直接加载 URL。资源表存在但当前序列不包含的普通关卡视为未命中。初版区分大小写，后因用户真实链接返回 `level_022` 调整；三个本地资源表没有仅大小写不同的 Path 冲突。
- `main` 兼容 `default`；其它玩法和 action 拒绝。对象、数组等结构错误的 sub2 拒绝；旧数字作为未命中的路径处理，不继续解释为关卡号。
- 同路径若在序列内重复，主线取最小排序 ID、Draw 取最终列表首项、IWanner 取最小组 ID 中首次出现的小关。当前本地三个资源表均未发现重复 Path。

## 实现与数据流

1. `GameSDKService.InitService()` 提前订阅 Unity URL；`GameAnalytics` 把 `OneLinkManager.AppsFlyerHook` 放在 `GuruAppsflyer.Prepare` 后。接点只复用 SDK 公开组件及事件，在 initSDK 后、startSDK 前订阅 UDL；不修改 Packages/GuruSDK。
2. 接点在 Guru 回调对象上补充公开的 `AppsFlyer` 接收组件；UDL 回到主线程后兼容 Android 平铺字段与 iOS `click_event` 对象／JSON 字符串。Unity 冷启动 URL 与后续唤起事件暂存本次链接；若 SDK 尚未返回有效参数，在首页门禁放行后尝试主动解析，详见末节。
3. `OneLinkRequest` 只解析协议；`OneLinkManager` 只保存会话内最新有效请求。不写请求存档，不因普通启动重放已消费请求，不让 Loading 等待网络。
4. `LoadManager.StartStartupPopupSequence()` 在 ChestGift 步骤之后通知启动弹窗已全部派发；此时礼包可能仍打开，因此不直接跳转。
5. `StartPanel.LateUpdate()` 在 Play Mode、打开且可见、进场动画结束后检查请求。Manager 再检查 Main 场景、应用获得焦点、Main 控制器处于 START、启动队列已派发结束、Popup/Top 栈为空、关闭销毁完成、共享遮罩淡出结束。待处理 URL 的主动解析与结果进关都经过这些门禁；常驻 System GMPanel 不作为 Popup。
6. 请求在路由前消费一次，同步拒绝只记录日志。`OneLinkLevelRouter` 读取当前配置后调用原进关链，不发送新全局事件或复制关卡打点。

### 三种玩法、回退与解锁

- 主线：在当前 `LevelSorts` 通过 LevelData.Path 查找目标；未命中复用 `LevelCtrl.TryGetLowestUnpassedOrLastLevel()`，按最小有效未通关排序 ID 选择，全通后进入 `Level_999`。目标 `UnlockLevel` 后经 StartPanel 派发既有 `EnterLevelEvent`。
- Draw：按当前最终列表匹配 Path；未命中取列表最前的有效未通关关，全通后取最前有效关重玩。沿用传送门的列表顺序与全通回退，但根据用户允许直接解锁的要求，不保留传送门的已解锁候选过滤。经 `TryEnterDraw` 激活后复用选关面板的配置版本校验、当前关／页码／目标解锁保存，再调用 `TryEnterDrawLevel`。
- IWanner：解析目标所属的有效组及零起始组内索引，只解锁该组。向既有 `GameManager.TryEnterIWannerGroup` / `IWannerPlayCtrl.TryEnterGroup` 传递可选索引，避免被同组旧存档续关覆盖；普通选组省略索引时维持原续关规则。进度写入、加载和统计由原控制器执行。
- IWanner 未命中：选择 ID 最小的未通关有效组中，配置位置最前且 Path 没有成功次数的小关；跳过缺失配置。没有候选时请求原 TBN 入口，其原解锁检查仍生效。绕过选组弹窗时补齐该弹窗原本负责的旧档集合初始化。
- `ILevel_999` 不属于普通组，是控制器固定 TBN 资源。明确指定该 Path 时允许本次直接进入，不伪造最后一组通关；不新增永久 TBN 解锁存档。普通选组 TBN 的通关门禁保持不变。
- 解锁不写 PassedLevels、不赠送奖励；后续真实游玩仍走原胜利／组完成规则。IWanner 直达组内后段后通关最后一小关，仍可能按原规则记整组完成，这是指定小关入口的进度影响。
- Draw/IWanner 整个玩法被配置关闭时拒绝；未初始化配置、无有效候选等也拒绝，保留原首页／失败恢复边界。IWanner 不创建地图选择弹窗，因此不改 LastMapIndex。

## GM 与编辑器两个 API

在 Unity 编辑器 Play Mode 或启用 `DEBUG_MODE` 的手机调试包使用；非运行态入口直接返回，不初始化运行时服务、不读写存档。两者均在 `#if UNITY_EDITOR || DEBUG_MODE` 内，不含这两个宏的包不会包含测试 API。参数 API 的 true 表示已排队；URL API 的 true 表示已排队或已开始异步短链解析，最终结果用可选回调确认。

```csharp
#if UNITY_EDITOR || DEBUG_MODE
// API 1：模拟 sub1 / sub2 参数；每次调用视为新点击。
Game.OneLinkManager.TestDeepLink("main", "Level_002");
Game.OneLinkManager.TestDeepLink("draw", "DLevel_002");
Game.OneLinkManager.TestDeepLink("iwanner", "ILevel_002");
Game.OneLinkManager.TestDeepLink("draw"); // 测试缺省回退

// API 2：长链接本地解析 query；Android 调试包的 OneLink 短链接由 SDK 在线解析。
Game.OneLinkManager.TestDeepLinkUrl(
    "https://example.test/link?deep_link_value=open_level&deep_link_sub1=draw&deep_link_sub2=DLevel_002");
// 短链仅 Android 真机支持；queued 只表示已进入首页待处理队列，不代表关卡加载完成。
Game.OneLinkManager.TestDeepLinkUrl("https://brainpunk.onelink.me/IjED/gacjwdmm",
    (queued, message) => { /* 展示本次结果 message */ });
#endif
```

- example.test 长链接仅本地解析，不发送网络请求。支持 HTTP(S)、无 sub2、编码后的参数；重复的深链键拒绝，未知 query 键忽略。保留输入 URL 的原始编码用于诊断。
- `brainpunk.onelink.me/<模板>/<短码>` 在 Android 调试包中经 `performOnDeepLinking` 解析，再用 UDL 返回参数；即使短链追加了 query 也由 SDK 按后台优先规则处理。编辑器和 iOS 的本测试 API 暂不支持短码在线解析，会显示平台限制；正式 iOS UDL 接收保持原流程。
- 两个 API 复用正式解析和首页门禁，不跳过公告、礼包、遮罩、玩法开关、配置校验。测试成功会使用并修改当前设备玩家的正常关卡进度／解锁存档；长链接／参数模拟不验证网络，Android 短链解析也不验证系统唤起、签名、安装或再归因。
- 在正常启动后测试；仅直接打开 Main 场景而没有完成正式启动链时，可能一直等待 `startup_sequence`。在游戏中调用则等待回到首页。

## 日志、验证与证据边界

- 使用可导出的 Guru 日志 `[OneLink]`：`bound`、`url_received`、`callback`、`queued`、`waiting`、`duplicate_ignored`、`target_resolved`、`entry_accepted/rejected/exception`，编辑器另有 `debug_test/test_url_rejected`。`target_resolved` 记录请求 Path、实际 Path、排序／组 ID、组内索引、是否 fallback。`entry_accepted` 只代表同步受理，不代表资源加载完成；后者查原玩法加载日志。
- 定向 Roslyn 编译使用 Unity 的 `com.bear.ui.rsp` 与 `Assembly-CSharp.rsp`，输出／引用替换到系统 TEMP，不写 Packages、GF 或 Unity 原程序集。UI 编译 0 错误；游戏编译 0 错误 / 105 警告，未逐项归因全部既有警告。
- TEMP 验证目录：`C:/Users/admin/AppData/Local/Temp/brainpunk-onelink-20260922/`，保留 `ui-compile.log`、`game-compile.log`、`tests.log`、`tests-player-symbols.log`、`local-config.json` 及 harness 源码／工程／提取脚本。
- 59 项编辑器符号下的断言通过，覆盖协议、iOS 嵌套、Path 与排序不等价、缺失路径回退、跨玩法错误路径、指定组内索引、默认续关、仅目标解锁、旧档集合、TBN 特例、弹窗／遮罩／首页状态、消费与去重、重复模拟点击、两个 API 和 URL 边界。取消 UNITY_EDITOR 后 49 项断言通过，包含两个 API 均不存在的反射检查。
- harness 直接编译三份 OneLink 源码，提取当前 IWanner 两层入口、续关、保存方法、主线回退／解锁、Draw 提交流程及 UI 查询原方法；Unity/SDK/资源异步操作用替身。三项实际 JSON 映射测试通过，但这不是 Unity PlayMode 或真机结果。
- `uncertain`：Android/iOS 原生域名与 OneLink 模板配置、签名包唤起、未安装后延迟回调、SDK 冷／热启动顺序、同链接反复点击、原生广告／支付回焦、横屏弹窗动画、实际资源和后台打点未实测。须用实际 URL 和签名包补验；本任务没有修改域名／entitlements／Android manifest。
- 去重键由 Unity 唤起次数、GameType 和忽略大小写的目标 Path 组成，不再把 link/deferred 的差异视为另一次请求。SDK 先返回且携带与随后 Unity 事件完全相同的 URL 时合并一次；同链接再次点击仍依赖新 Unity URL 事件。只保留本次待处理目标，不宣称能识别所有无请求标识的网络回调实际点击先后，详见末节风险。
- “路径不存在”在这里指当前可进入配置未命中。配置存在但 Prefab 未交付、热更包损坏或异步加载失败，仍走原控制器失败恢复，不循环重试其它关。保存失败只告警，不能保证磁盘持久化；已完成的解锁写入不会因后续异步失败自动撤销。

## 影响文件与回滚

- 新增 `OneLinkManager.cs`、`OneLinkRequest.cs`、`OneLinkLevelRouter.cs` 及 meta；游戏 SDK 初始化／Analytics 接点、LoadManager 队列末尾、StartPanel 首页检查、UIManager 只读阻塞查询、Draw 选关提交返回值、GameManager 与 IWannerPlayCtrl 可选入口参数。
- 原有 GameManager “只供选组／首小关”、IWannerPlayCtrl 仅恢复存档和 TBN 必须通关的注释，与新增 OneLink 调用边界不再一致，故仅更新相关注释。其余旧注释保留。
- 无新增 Prefab 绑定、存档字段、配置字段、全局事件；共享 UI 只增加查询，不调整层级／遮罩生命周期。首页无待处理请求时早返回，不扫描配置；路径解析只在一次请求消费时扫描配置。
- 回滚先移除 SDK Hook、初始化、启动队列通知和 StartPanel 检查，再删除三份 OneLink 脚本及测试 API；恢复 Draw 提交包装、UIManager 查询、IWanner 可选索引／TBN 入口参数及其转发。已有正常进关解锁和进度不会自动回滚，不删除玩家存档。保留与本任务无关的字体、关卡 Prefab、平台设置等用户改动。

## 2026-09-22：接入 GMPanel 与 Android 调试包（首版记录）

- 用户要求在 GMPanel 操作两个测试 API，并确认 Android 能否测试。原 API 仅 UNITY_EDITOR，Android 无法调用；本轮扩展到 UNITY_EDITOR 或 DEBUG_MODE。
- GMPanel 的现有滚动按钮容器新增 **OneLink 测试**，通过显式序列化按钮引用打开 `GMOneLinkTestPopup`。新弹窗提供 sub1、sub2、完整 URL 三个 TMP 输入框，参数测试／URL 测试／粘贴 URL 按钮和错误提示。
- 复用 GMPasswordPopup 的 UGUI 输入框、背景和按钮样式制作独立 Prefab；原密码弹窗未改。所有 8 个引用在新 Prefab 显式绑定，无运行时按名称扫描或动态拼 UI。新增 GM 按钮同样在 GMPanel.prefab 显式绑定，不改 Generated 文件。
- 提交有效请求后先关闭输入法及本弹窗，正式路由继续等待其它弹窗和遮罩。非法参数、无参数短码等拒绝时保留弹窗显示提示；关窗重开解除并重绑事件，测试覆盖无重复提交。游戏中提交后仍等返回首页，不强制中断关卡。
- 当前 `ProjectSettings/ProjectSettings.asset` 的 Android 定义包含 DEBUG_MODE；按此定义重新打包后可用 GM 测试。构建流水线若移除该宏则按钮隐藏、Create 拒绝、两个 API 不编入。Development Build 本身不是此功能的唯一开关，判断依据是宏。
- Android **游戏内模拟**与**外部真实链接唤起**分别验收：当前自定义 Manifest 的 VIEW filter 只声明 `https://brain.gurugame.ai/default_link`，没有截图中的 `brainpunk.onelink.me`；`guru_spec.yaml` authority 同样为前者。本轮未修改这些平台配置。最终合并 Manifest、签名指纹、域名验证、OneLink 后台和真实点击仍为 uncertain；不能据 GM 模拟通过宣称外部短链已可唤起。
- 本轮定向编译：Editor 0 错误；当前源文件／现有程序集引用去掉 UNITY_EDITOR 宏、保留 UNITY_ANDROID+DEBUG_MODE 后 C# 编译 0 错误 / 108 警告。旧 Player Bee rsp 仍引用已迁移的 Assets/DrawGame、Assets/IWannerGame 路径，直接编译失败；保留 `game-compile-android.log` 记录，后改用当前 rsp 做 Android 宏验证。此过程没有构建 APK、Gradle 或 IL2CPP。
- 更新后的 harness 直接包含 GMOneLinkTestPopup 源码：Editor 与 DEBUG_MODE 各 68 项断言通过，无 Editor/Debug 宏 50 项通过。新增覆盖有效提交自动关窗、手机输入框失焦、无效输入留窗、剪贴板粘贴、重开不重复绑定、正式包入口禁用。Unity UI、TMP、系统剪贴板和触屏键盘仍为模拟替身；Android 真实键盘、粘贴、布局和资源加载尚未验证。
- Prefab 静态检查确认新弹窗 120 个序列化对象全部内部引用可解析，8 个输入／按钮／状态引用类型匹配；GMPanel 新增入口的父子树和脚本引用有效。运行渲染、横屏小尺寸与真机输入仍为 uncertain。
- 日志保存在 TEMP `brainpunk-onelink-20260922`：`game-compile-gm.log`、`game-compile-android-current.log`、`tests-gm-editor.log`、`tests-gm-android-debug.log`、`tests-gm-release.log`。前文 59/49 项为 GM 接入前阶段结果，本节覆盖当前验证口径。
- 回滚本轮：删除 GM 新按钮及绑定、GMOneLinkTestPopup 脚本／Prefab／meta，测试 API 恢复 UNITY_EDITOR 限制及原 void 签名；正式 UDL 和路径路由保留。不要回滚其它用户已有修改，也不要删除玩家存档。

## 2026-09-22：补齐 Android GM 短链解析

### 问题证据、目标与范围

- 用户报告 `BP_T-YYY38` 的 16:31:09、16:31:24 均为 `test_url_rejected / missing_deep_link_parameters`。首版按钮只检查输入 URL 的 query，未向 SDK 提交短链；这两次拒绝不是 SHA-256 校验结果。
- 用户原始链接为 `https://brainpunk.onelink.me/IjED/gacjwdmm`。本机以 Android UA 请求得到 HTTP 301，Location 的 referrer 含 `deep_link_value=open_level`、`deep_link_sub1=default`、`deep_link_sub2=level_022`。这是 HTTP 响应证据，不等于手机系统唤起或 UDL 实测。
- 只修改 `OneLinkManager.cs`、`GMOneLinkTestPopup.cs` 和 `OneLinkLevelRouter.cs`，复用现有序列化输入框、状态文本与按钮。未修改 Prefab、SDK/Packages/GF、Manifest、远端后台、Firebase、启动 Loading 等待、关卡资源或存档结构。
- 将初版“短码只拒绝”“URL API 的 true 恒表示排队”“Path 大小写敏感”的描述替换为本节行为。原有注释仅在与新行为冲突的 URL API、GM 弹窗和 URL 输入字段处调整。

### 请求、回调与 UI 生命周期

1. 正常启动后在 GM 粘贴短链接；仅 Android 且带 DEBUG_MODE 时调用 SDK。未绑定 SDK、链接格式错误、不支持的平台或已有请求正在执行，立即给出原因。
2. 游戏侧通过 JNI 调用现有 AppsFlyer 6.17.3 的公开 `performOnDeepLinking(Intent, Context)`。使用独立 Intent，不启动 Activity、不替换真实 Activity Intent、不重启 SDK，也不替换已有全局 UDL 订阅。
3. 每次请求在 `deep_link_sub10` 追加 `bp_gm_url_` 开头的唯一测试标识。此字段在 GM 短链测试中保留用于回调关联，不能在 URL 或后台占用；本次原始短链附加标识后的 HTTP referrer 能回传该字段。普通生产链接的 sub1/sub2 协议不变。
4. Popup 保持打开并显示“正在解析”，禁用两个提交按钮、粘贴按钮及三个输入框，关闭按钮仍可操作。收到匹配请求的 UDL FOUND 后记录三个参数，移除测试标识，恢复原 URL，再调用已有 `AcceptPayload`；只有排队成功才关闭 Popup，仍等首页其它门禁结束后进关。
5. 等待上限为 10 秒，忽略 timeScale；超时或关闭弹窗后停止本次等待和进关，但没有取消 SDK 原生网络请求。携带旧标识的成功、重复或晚到回调丢弃，不能关闭重开的 Popup 或覆盖新请求。正常系统 URL 到来会先取消当前 GM 测试，再沿原 UDL 流程处理。
6. SDK NOT_FOUND/ERROR 若不带请求标识，无法判定属于启动回调、旧请求还是当前请求，因此记录最近状态，继续等本次匹配结果，超时时一并显示；不能用无标识失败回调结束后一次请求。手动测试等待期间，无标识的非 deferred 回调不作为本次测试结果；测试结束后恢复正常接收。
7. Popup 通过打开版本号屏蔽已关闭／重开实例的旧结果。同步长链接保持原本地解析行为并立即回调，参数测试保持原 API；不新增 PlayEvents、运行时节点查找或资源加载分支。
8. OneLink 的 Path 比较改为 OrdinalIgnoreCase。主线 `level_022`、Draw `dlevel_002`、IWanner `ilevel_002` 都用配置中对应资源的原始大小写进关；未命中仍走原回退。没有改其它选关入口或底层资源系统的大小写语义。

### 日志与验证

- 新增 `[OneLink] stage=test_url_started/resolved/finished/native_error/late_ignored/unmatched_status/unmatched_ignored`；开始记录请求 ID、域名/路径和 10 秒预算，成功记录 `deep_link_value/sub1/sub2` JSON，结束记录 queued 与 reason。不会打印完整 SDK 归因对象或输入 URL 的任意 query。
- SDK 依据：本地 Dependencies.xml 声明 Android 6.17.3；从 Maven Central 读取同版本 AAR 至系统 TEMP，经 javap 确认公开 `performOnDeepLinking(android.content.Intent, android.content.Context)` 存在。官方接口说明：<https://dev.appsflyer.com/hc/docs/android-sdk-reference-appsflyerlib#performondeeplinking>。
- HTTP 证据：用户短链能返回上述三个参数，追加测试标识后仍回传该标识；尚不是 SDK 端到端验证。三个本地玩法配置的 Path 忽略大小写后无冲突。
- 定向编译复用当前 Unity Bee 引用，输出至系统 TEMP：Editor 0 错误 / 105 警告，Android DEBUG_MODE 0 错误 / 108 警告；未逐项归因所有工程既有警告。模拟 harness 直接编译当前三个 OneLink 脚本和 GM Popup：Editor 74 项、Android DEBUG_MODE 106 项、无调试宏 53 项通过。
- 新增覆盖真实配置中的 `level_022 -> Level_022` 及仅解锁目标、三种玩法大小写、SDK 未就绪、异步等待、原生异常、协议无效、重复提交、参数 API 与短链互斥、10 秒超时、切后台后超预算回调先于计时器执行、取消、旧结果晚到、重试/重开、无标识 NOT_FOUND/ERROR、真实 URL 打断测试及完成后恢复普通 UDL。
- 测试修复了长链接使用 Uri.AbsoluteUri 导致原始百分号编码被标准化的问题，最终保留 Uri.OriginalString；原有 URL 保留和解析回归通过。临时目录为 `C:/Users/admin/AppData/Local/Temp/brainpunk-onelink-20260922/`，见 `HarnessShortUrl.csproj`、`short-url-*-compile.log`、`short-url-*-tests.log` 与 `native-public-api.txt`。

### 待验证、风险与回滚

- `uncertain`：尚未重新构建 APK、运行 IL2CPP 或在 Android 真机取得本轮 UDL 成功回调；JNI、网络、SDK 是否按预期回传测试标识、真机断网/服务不可用、横屏提示换行与键盘待测。模拟时钟和 SDK 替身只证明游戏侧状态处理。
- 若短链后台占用 sub10、SDK 不回传标识或格式变化，需要根据真机 `test_url_* / callback` 日志调整关联方式，不能把一次无标识回调误认作当前请求。后台切勿使用 `bp_gm_url_` 保留前缀。
- 10 秒为应用侧预算；原生覆盖层或切后台导致 Unity 不推进时，结果处理/超时 UI 要等 PlayerLoop 恢复，匹配回调也会再检查 realtimeSinceStartupAsDouble 截止时间，避免它先于延迟计时器恢复时受理超时结果。测试不宣称取消原生请求或验证安装/再归因，正式 SDK 启动与统计由原 GuruSDK 管理。
- 编辑器与 iOS 仍只支持参数和显式 query 的 URL 测试，短链接在线 GM 测试范围是 Android。外部浏览器点击唤起还需补齐 OneLink 后台 App Links、实际 APK 证书指纹和 Manifest；当前域名 assetlinks.json 请求 404，不能把本轮 GM 完成等同于系统唤起完成。
- 回滚本节：恢复 Manager 的同步 URL 解析、Popup 的同步结果处理和 Router 的 Ordinal 比较；同时撤回本节说明。无需变更 SDK 或迁移存档；已经通过测试写入的解锁进度不会随代码回滚自动撤销。

## 2026-09-22：正式首页自动解析 URL

### 需求、基线与范围

- 用户反馈 GM 链接测试成功，要求将同一解析能力用于实际打开应用后的自动跳转。此反馈证明用户的测试入口可用；本轮没有取得新包的外部点击日志，不据此宣称系统 App Links 或本轮自动流程已在真机验证。
- 修改前：`Initialize()` 已读取 `Application.absoluteURL` 并订阅 `deepLinkActivated`，但系统 URL 只记录，主动短链解析封在测试宏内；正式流程只能等待 SDK 自动返回有效 UDL。`StartPanel.LateUpdate()` 和启动弹窗末尾通知已经存在。
- 本轮运行时只修改 `OneLinkManager.cs`。复用原 StartPanel、LoadManager 和 `UIManager.HasBlockingPopup`，不添加 UI、序列化绑定、全局事件、存档字段或 Loading 网络等待，不修改 Packages、GuruSDK、GF、Manifest、关卡和路由解锁规则。
- 原“系统 URL 只记录”“不等待网络”和仅测试取消入口的注释与新行为不符，按实际范围更新；其它旧注释保留。`HasPendingRequest` 现在也包含待解析 URL 和自动解析中的请求，不再只表示参数已经有效。

### 当前流程与生命周期

1. 初始化捕获冷启动 URL；后续 `Application.deepLinkActivated` 捕获新点击。系统链接单独保存在待处理字段，不读取剪贴板、不写死测试 URL、不将 `Url` 历史值当作每次回首页的新请求，也不轮询旧 `absoluteURL`。
2. SDK 自动 FOUND 若先返回有效协议，直接沿原链暂存参数并清除待解析 URL，因此不再发主动请求。若 SDK 先于 Unity 事件返回且带完全相同的 `link`，随后系统事件只合并一次，不清掉有效参数或重复进关。
3. 首页完成进场、启动弹窗队列派发完毕、Popup/Top 清空、关闭销毁/遮罩淡出结束、Main START 且应用有焦点后，才处理尚未解析的 URL。
4. 明确 query 的 HTTP(S) 链接本地解析；`brainpunk.onelink.me/<模板>/<短码>` 在 Android 通过原 `performOnDeepLinking` 桥接解析。即使短码追加 query 仍交 SDK 合并后台参数。共用解析器已移出调试宏，Android Release 也可调用；GM 两个 API 仍只编入编辑器/DEBUG_MODE。
5. 每个实际系统 URL 最多触发一次主动解析，最长等待 10 秒；没有新增首页输入锁。SDK 回调只排队，不直接修改 UI 或进入关卡；下一次首页检查再次验证全部门禁。等待期间玩家已进关则保留结果到回首页；新弹窗出现则继续等待关闭。
6. 同次唤起按玩法和忽略大小写的 Path 去重，忽略 SDK 与主动解析在 `link/deferred` 上的差异。主动解析仍占用 `deep_link_sub10`，自动请求使用 `bp_auto_url_`，GM 使用 `bp_gm_url_`，后台不得使用这两个前缀或占用主动解析的 sub10。
7. 新系统 URL 取消当前主动解析等待并替换未消费目标；携带旧标识的晚到结果丢弃。主动解析中 SDK 原始自动回调若先返回有效参数，会结束主动等待；随后其带标识结果丢弃。已超时 URL 不按帧重试，再次实际点击允许重试。
8. GM 关闭仅取消 GM 请求，不能取消正式自动解析；自动解析期间两项 GM 提交都返回 busy。退出/重建会话取消两种解析并解绑。
9. iOS 保留 SDK 自动 UDL（含无 URL 的延迟深链）和 query 本地解析。**本轮没有新增 iOS 主动解析不透明短码的 API**；没有自动 UDL 的 iOS 短码仅记录平台限制，不能用 Android 测试结果推断 iOS 可主动补拉。

### 日志与验证证据

- 正常 SDK 提前返回：`url_received` / `callback` → `queued` → 必要的 `waiting` → `target_resolved` → `entry_accepted`。SDK 先于系统 URL 且来源相同会出现 `url_merged`。
- Android 主动补拉：`url_received` → 必要的 `waiting` → `auto_url_started` → `auto_url_resolved` → `queued` → `auto_url_finished` → `target_resolved` → `entry_accepted`。query 本地解析记录 `auto_url_parsed`；超时/原生异常/拒绝记录 `auto_url_finished/native_error/rejected`，旧标识记录 `url_late_ignored`。只记录域名/路径及协议字段，不输出完整归因对象。
- StartPanel 进场门禁依据 `StartPanel.cs:109`，启动派发通知依据 `LoadManager.StartStartupPopupSequence()`；本轮未修改这两处。Popup、Top、关闭销毁与共享遮罩门禁继续使用原只读查询。动画真实时序仍不能由替身模拟证明。
- TEMP：`C:/Users/admin/AppData/Local/Temp/brainpunk-onelink-20260922/`。`HarnessAutoUrl.csproj` 直接编译三份 OneLink、GM Popup 源码和提取的原路由/保存/门禁方法；`AutoTests.cs` 补充运行态自动获取测试，Unity/SDK/原生 JNI/网络为替身。
- 执行 `dotnet run --project HarnessAutoUrl.csproj -p:DefineConstants=...`：Editor 92 项、Android DEBUG_MODE 151 项、Android 无 DEBUG_MODE 96 项、iOS 符号 71 项断言通过。保留 `auto-url-*-tests.log`；正式包同时验证 GM 公共测试 API 不存在。iOS 符号只证明游戏侧处理分支，不是 Xcode/原生 SDK 验证。
- 覆盖冷启动一次获取、普通启动不处理、query 路由、SDK 先返回不主动重复请求、SDK 先于系统 URL（含 deferred）、无 URL deferred、相同 URL 再点击、所有首页门禁、解析中新弹窗/游戏中结果、新请求替换、两条回调先后、超时/后台恢复截止、JNI 失败、无效参数、sub10 占用、会话清理与 GM 隔离。
- 使用 Unity 当前源码/引用的 Roslyn 定向编译，输出到 TEMP：Editor 0 错误 / 105 警告；Android 去掉 UNITY_EDITOR 和 DEBUG_MODE 的正式包符号 0 错误 / 108 警告。见 `auto-url-editor-compile.log`、`auto-url-android-release-compile.log`。没有执行 Gradle、APK、IL2CPP 或 Xcode 构建，工程既有警告未逐项归因。

### 风险、待验证与回滚

- `uncertain`：外部点击是否把真实 URL 送到 Unity，依赖安装包签名、最终 Manifest、域名验证和 OneLink 后台；本轮均未配置。若日志没有 `url_received` 且 SDK 没有有效 FOUND，游戏侧没有链接来源，无法自行从任意短码猜出参数。
- `uncertain`：Android 真机新流程的冷/热启动、重复点击、后台恢复、真实网络以及 iOS 原始 UDL 顺序，仍需新包日志验证。SDK 先于 Unity 事件但没有 `link`、URL 被 SDK 改写、多个未标记自动回调乱序，无法精确关联点击；现有合并只承诺相同 URL 的可观察分支。
- 10 秒只取消主动解析的游戏侧等待，不取消 SDK 原生网络请求；带旧标识结果不会排队。没有关联标识的 SDK 自动 UDL 仍属于原接收通道，即使晚于这 10 秒也可能排队，不能宣称所有晚到网络结果均被取消。
- 新 URL 到来会替换此前未消费请求；新 URL 无效/不支持时旧请求不会恢复。未绑定 SDK、平台不支持或参数拒绝时记录一次原因，不循环重试；SDK 后续自己返回有效 UDL 仍可进关。
- 玩法关闭、配置缺失、资源加载失败和正常存档影响沿用原路由记录；本轮未扩展解锁规则。横屏 UI、资源成功完成和打点没有真机验证，`entry_accepted` 仍只表示同步受理。
- 回滚本轮 Manager 改动即可恢复“只等待正式 UDL、主动解析仅 GM”基线，同时回退本节和关联文档说明；StartPanel/LoadManager、SDK/平台配置和资源无需回退。已经执行的目标解锁不会随代码回滚自动撤销。

## 2026-09-24：补齐正式回调与 URL 参数日志

### 问题、目标与范围

- 用户提供的 `BP-YYGFX_debug_log(12).zip` 为 iPhone、应用版本 1.16.0。10:49:37 已收到 `FOUND`，随后游戏拒绝 `unsupported_action`；10:49:41 首页兜底拒绝 `invalid_http_url`。旧日志只记录状态和拒绝原因，没有实际协议字段或 Unity 收到的地址类型，无法判定后台参数缺失还是游戏选择参数位置不正确。
- 既有详细参数输出只在 Android 主动短链的成功分支；正式 SDK 自动回调，包括 iOS，不经过该日志。此前对“外层 action 存在而 click_event 缺失”的解释仅为代码可触发路径，不是本次真机已证实原因。
- 用户要求记录 URL 返回参数。本轮运行时代码仅修改 `OneLinkManager.cs`，复用 `GuruLog` 写入现有 SDK 诊断日志；不是新增 Analytics 上报事件。保留协议解析、去重、首页门禁、解锁和进关行为，不修改 OneLinkRequest、SDK/Packages、GF、平台配置、Prefab、存档、远端配置或 Loading 等待。

### 日志内容与使用方式

- 绑定日志升级为 `diagnostics=onelink-v2`，用于检查安装包是否包含本轮日志代码。
- `stage=parameters`：记录 `root`、`click_event` 各自的 `deep_link_value/sub1/sub2`，`click_event_type`、`selected_source` 和按现行解析规则选用的 `selected`。区分 `Missing`、`Null:null`、空字符串、数字、对象、数组；保留空白并转义换行，单个标量诊断截断至 256 字符后标记 `<truncated>`。
- 自动 UDL 在结果关联、参数拒绝和排队前记录，`source=sdk_callback`；FOUND、NOT_FOUND、ERROR 均经过该记录。GM 参数测试使用 `gm_parameters`；GM 长链接和首页本地解析分别使用 `gm_url_query`、`auto_url_query`，缺参数或重复参数的部分解析结果也记录。
- `stage=url_details`：Unity 收到 URL（含合并的重复通知）、GM URL 测试入口、SDK 根对象或 click_event 的字符串 `link` 均记录来源、原始长度、是否为绝对地址、scheme、host、path、HTTP(S) 支持情况及 query 内三个协议参数。重复 query 参数逐项保留。无法解析的地址只记录 `?/#` 前的截断片段。
- 不输出完整 SDK 归因对象、URL 用户凭据、任意 query、fragment 或对象型字段的内部内容。数据缺失仍可通过类型和字段记录定位；诊断异常单独记录 `diagnostics_failed`，不阻断原流程。
- SDK `status` 为 ERROR 时才显示其 error；成功/未找到显示 `error=none`。本地 AppsFlyerEventArgs 将未知错误字符串映射为 UNEXPECTED，不能把 FOUND 回调同时出现的默认枚举当作失败依据。
- 查验顺序：确认 v2 标记 → `url_details` 确认 Unity 收到何种链接 → `callback` / `parameters` 确认 SDK 实际字段及选择位置 → 对照 `rejected` 或 `queued` → 查看原 `waiting` / `entry_accepted`。这是一套取证步骤，不意味着当前 iOS 跳转问题已经修复。

### 验证、缺口与回滚

- 使用现有 Unity Bee 引用进行 Roslyn 定向编译，输出仅在系统 TEMP：Editor 0 错误 / 105 警告，Android 无 DEBUG_MODE 0 错误 / 108 警告；工程既有警告未逐项归因。
- 模拟 harness 直接编译当前 OneLink 脚本和 GM Popup：iOS 符号 88 项、Editor 111 项、Android DEBUG_MODE 170 项、Android 无 DEBUG_MODE 113 项通过。含原门禁、去重、超时和路由回归，以及新增缺 action、根字段/嵌套字段不一致、JSON 字符串、非法 JSON、空值/缺字段/错误类型、ERROR 空 payload、自定义 Scheme、非法地址、重复 query、日志转义/截断/字段过滤的验证。
- 模拟首次复跑发现旧 SDK 替身把 error 定义成可空字符串，与实际 SDK 不可空枚举不一致；仅修正 TEMP 替身默认值后四组全部通过，未为此改动生产 SDK。模拟证据不等于 iOS 原生 SDK 或真机验证。
- 证据目录：`C:/Users/admin/AppData/Local/Temp/brainpunk-onelink-diagnostics-20260924/`，包括 `DiagnosticsTests.cs`、`diagnostics-*-tests.log`、`editor-compile.log`、`release-compile.log`。定向 `git diff --check` 用于本轮代码和文档；原有字体资源空白问题不在本次范围。
- `uncertain`：尚未构建或安装新 IPA/APK，真实 iOS 回调字段和本次失败的最终根因仍缺证据。需要安装包含 v2 日志的包，重复原链接操作并导出日志；旧包日志无法追补未记录的参数。日志诊断没有修改原本可能拒绝的条件。
- 回滚仅撤销本节对应 Manager 日志调用、私有诊断方法和标记调整；不需迁移资源或存档，不撤回其它既有 OneLink 功能。

## 2026-09-24：ERROR 后重试与首次安装补偿

### 原有行为、需求与修改范围

- 用户明确要求 SDK 返回 ERROR 后重新尝试。原基线：SDK ERROR 只记录，首页 URL 只消费一次，iOS 没有主动补拉；SDK 后续自行返回 FOUND 仍可正常排队。
- 同日用户进一步收紧要求为“最多重试两次，10 秒内收不到就结束”。该要求替换前一轮 v3 的三次重试、5/10/20 秒间隔和无网无限保留等待；以下记录当时的 v4 行为，当前规则见末尾 v5 章节。
- 运行时代码只修改游戏侧 `OneLinkManager.cs`，复用现有 SDK 公开接口、首页门禁和路由。未修改 Packages/GuruSDK/GF、原生平台配置、RemoteConfig、Loading、关卡解析、存档结构、资源或 Prefab。GM 手动测试维持原有 10 秒预算，不自动重试。
- 既有协议仍为 `open_level`、sub1 玩法和 sub2 levelpath；无效目标的回退与解锁仍由原路由处理。本轮不修改根对象与 click_event 的业务参数选择规则。

### 重试与数据流

- 正式 SDK 首次返回 ERROR 后开始一个 **10 秒总预算，最多追加两次重试**。第一轮在首页门禁和网络条件允许时立即发起；第二轮最早在首次 ERROR 后第 5 秒发起。每轮最多等待 5 秒，且不得超过总预算的剩余时间。例：第 7 秒才恢复网络，第一次重试最多只剩 3 秒，不会再获得完整 5 秒。
- `StartPanel` 原有调用仍在首页进入动画、启动弹窗派发与遮罩等门禁通过后发起重试；没有新增 Loading 等待或输入锁。**等待首页、弹窗、前台或网络都计入这 10 秒**，因此可能实际只发起一次，甚至没有机会发起。已经在 10 秒内成功取得的参数仍可排队，等首页门禁通过后进入，不要求进关本身也在 10 秒内完成。
- 系统报告 `NotReachable` 时约每秒检查一次，不消耗请求次数，但继续消耗总预算。10 秒内恢复网络才能继续；超时后即使联网也不恢复本次请求。这里只是系统网络状态提示，不代表 AppsFlyer 服务一定可访问。
- Android 复用原 `performOnDeepLinking`；iOS 使用公开 `AppsFlyer.handleOpenUrl` 将带请求标识的 URL 重新交给 SDK。每次使用独立 sub10 标识；带旧标识的晚到回调忽略。没有重启 SDK，也没有新增原生桥接。
- 只有正式 ERROR 才开启恢复流程。恢复流程内第一轮 ERROR 或等待超时，都可在剩余预算内安排第二轮；NOT_FOUND、FOUND 参数非法和原生接口调用异常不再安排下一轮。原本没有收到任何 ERROR 的普通 URL 请求及 GM 测试，继续保持原单次 10 秒等待。
- 无标识 ERROR 不能确认所属请求，因此不提前终止当前请求。如果 ERROR 在原自动请求等待期间到来，总预算从这次 ERROR 计算，而非原请求结束后再加 10 秒；原请求结束时最多只使用剩余预算重试。
- 成功受理参数后清除重试；新的真实链接替换旧请求，会话结束取消等待与订阅。ERROR 先于首次 Unity URL 到达，以及恢复期间相同 URL 重复通知的分支，沿用同一个截止时间和次数，不重置十秒计时。不同点击都没有关联标识时，仍不能精确判定旧自动回调属于哪次点击。
- 达到截止时间后，在首页处理或回调入口清理游戏侧等待，记录 `total_timeout`。当前唤起的晚到 FOUND（包括无关联标识的自动 UDL）、ERROR 和安装补偿均不再触发进关。Unity PlayerLoop 暂停时不会保证日志恰好在第 10 秒输出，但恢复后会先检查实际截止时间再处理结果。新的系统 URL 通知可开启新的唤起处理。
- 普通 HTTP(S) 长链接本身已经包含合法玩法参数时，保留原本地解析能力，即使 SDK 返回 ERROR 也无需等待网络重试；OneLink 短码仍交给 SDK 解析后台参数。已占用 sub10 的 URL 继续按原规则拒绝主动解析。

### iOS 首次安装没有 URL

- 本地现有公开 Unity/iOS 接口没有确认可用的“无 URL 强制重发延迟深链”方法。本轮没有将重复 startSDK 视为重发，而是订阅已有 `GuruAppsflyerManager.ObservableConversionData` 补收安装归因参数。
- 仅在正式 UDL 已经 ERROR、没有可重交的 URL、尚未成功受理且仍在首次 ERROR 后 10 秒内时启用补偿；结果须满足 `is_first_launch=true`、`af_status=Non-organic`，且通过原 OneLink 协议校验。自然安装、非首次启动、超时和无效玩法参数不跳转。
- 安装归因先到时可读取当前 SDK 对象已收到的结果；先 ERROR 时等待后续回调。这里不读取历史存档归因。UDL 和安装补偿先成功者生效，后续同次回调不重复排队；新链接重新开始一次处理。
- **此路径是补收安装结果，不是主动重发无 URL 请求。** 如果 SDK 后续始终没有返回有效安装参数，游戏侧仍无法猜出目标关卡；断网恢复后是否实际补回，需要新 iPhone 包验证。
- 接口依据：[Unity SDK API](https://dev.appsflyer.com/hc/docs/api)、[iOS UDL 网络授权后的延迟深链](https://dev.appsflyer.com/hc/docs/dl_ios_unified_deep_linking#deferred-deep-linking-after-network-consent)、[iOS 安装归因补偿说明](https://dev.appsflyer.com/hc/docs/dl_ios_ocds_ddl)。本地只读核对 `AppsFlyer.cs` / `AppsFlyeriOS.cs` / `AppsFlyeriOSWrapper.mm` 的 handleOpenUrl 转发，以及 GuruAppsflyerManager 的转换数据发布；当前 iOS 依赖版本为 6.17.7。

### 日志与验收

- 本轮绑定标记为 `diagnostics=onelink-v4`，保留前一节全部参数诊断。`retry_scheduled` 记录次数、间隔和剩余秒数，`retry_waiting` 区分无网和没有 URL，`retry_started` 记录发起，`auto_url_finished` 记录结果，`retry_stopped` 记录终止原因；总预算到期记录 `reason=total_timeout`，晚到 UDL 记录 `callback_ignored / retry_budget_expired`。参数已在长链接内时记录 `retry_local_fallback`。
- 安装补偿使用 `source=install_recovery` 记录参数，受理后记录 `install_recovery_queued`。实际进入关卡仍查看原有 `queued`、`waiting`、`entry_accepted`；后者只表示同步受理，不证明资源加载完成。
- TEMP 模拟直接编译当前生产脚本：iOS 150 项、Editor 113 项、Android DEBUG_MODE 220 项、Android 无 DEBUG_MODE 159 项断言通过。保留前轮的 ERROR、参数、路由、门禁和去重回归，增加两次上限、5 秒单轮等待、10 秒总预算、断网与弹窗占用预算、晚恢复后等待裁剪、重复 URL 不续时、截止前后成功结果、安装补偿超时，以及原自动请求中途 ERROR 不后移截止时间。
- 复用 Unity Bee 引用进行 Roslyn 定向编译：Editor 0 错误 / 105 警告，Android 无 DEBUG_MODE 0 错误 / 108 警告；既有警告未逐项归因。仅编译输出写入 TEMP，不启动平台构建或改写 SDK。
- 全工程切换 iOS 符号的编译被本机已编译依赖阻挡：`ChoiceLevelPanel.cs:17` 和 `StartPanel.cs:14` 引用的 `Guru.UniKit.Apple.GameKit` 命名空间不可用，共 2 错误。没有修改这些 UI 或 SDK 文件来绕过问题。
- 另将当前 OneLinkManager/OneLinkRequest 与真实 Unity、AppsFlyer、Guru、R3 程序集做 iOS 接口定向编译，0 错误 / 4 个 CS0436 警告；警告为源文件优先于旧游戏程序集同名类型。为跨程序集编译，TEMP 内替代了未修改的内部 OneLinkLevelRouter；这只验证新代码和真实 SDK 的接口签名，不等同完整 iOS 构建、原生回调或路由实测。
- 证据目录：`C:/Users/admin/AppData/Local/Temp/brainpunk-onelink-retry-20260924/`。本轮使用 `RetryTests.cs`、`budget-*-tests.log` 和 `budget-*-compile.log`；上一轮 `retry-*-tests.log` 和 `ios-compile.log` 保留作阶段证据。执行 `dotnet run --project HarnessAutoUrl.csproj -p:DefineConstants=...` 四组模拟，以及 Roslyn 原有 Editor/Android 和 iOS 接口引用定向编译；本轮三组定向编译再次通过，错误/警告数同上。代码和两份本地记录执行定向 `git diff --check`；不处理既有字体资源空白问题。

### 剩余风险与回滚

- `uncertain`：尚未构建或安装新 IPA/APK，iOS handleOpenUrl 对实际短链的重交结果、sub10 是否完整回传、断网恢复后的 SDK 安装回调及 Android/iOS 冷热启动仍需真机验证。模拟不能证明原生请求已经成功。
- 10 秒只结束游戏侧等待，不中止 SDK 原生网络请求；本次唤起的晚到回调会被游戏侧忽略。SDK 自身请求行为不受两次上限控制。新的系统 URL 通知会开始新的处理，若旧自动回调没有标识且在新点击后才到达，仍存在原有的点击归属歧义。
- 旧 iPhone 日志是 FOUND 后参数被拒绝，并非 ERROR；本轮重试不会自动修复那个拒绝分支。需使用 v4 参数日志确认实际字段，不能报告旧问题已经解决。
- 从 v4 回滚到前轮 v3 需要同时恢复三次次数、延迟安排、单次 10 秒计时，以及移除总截止时间与超时回调拦截；不能仅改次数。相关超时提示按是否属于重试区分，GM 原来的 10 秒提示保持不变。
- 回滚仅撤销本节对应 Manager 的重试状态、iOS 重交接口分支、安装订阅及标记调整，保留前一节 v2 诊断与原首页逻辑；同步回退本节和当日记录。不需迁移资源或存档，已执行的关卡解锁不会随代码回滚撤销。

## 2026-09-24：iPhone 日志 13 的参数缺失分析

- 来源：用户提供 `com.brain.tricky.puzzle.games.test.quest_BP-YYGFX_debug_log(13).zip`，SHA256 为 `34BD0C5C549121909EE8F2887D3EF6B125A4B8EC93D5CE0C22231A1A1CC93AC0`。只读分析解压在 `C:/Users/admin/AppData/Local/Temp/brainpunk-onelink-ios-20260924-log13/`；以下行号均指其中 `gurusdk.log`，共 894 行，范围 15:03:39 至 15:04:05。仅更新分析记录，没有修改运行时代码。
- 可观察事实：第 5 行为 `appVersion=1.15.1, platform=IPhonePlayer`，第 226 行为 `diagnostics=onelink-v4`，确认本日志含最新诊断代码。全文件只有 1 次 OneLink FOUND 回调、0 次 ERROR、0 次 retry_started、0 次 queued、0 次 entry_accepted，未出现 `brainpunk.onelink.me`。
- 15:03:40，第 71/290 行分别记录 Unity 和 SDK 提供的地址结构：scheme 为应用包名、host 为 `google`、path 为 `/link/`，三个跳转协议 query 参数为空。日志没有记录其它 query，不能还原完整 81 字符 URL，也不能仅凭名称断言它来自哪个 SDK 或操作。
- 同秒第 288–291 行：回调 `FOUND, deferred=False, error=none`；根对象 `deep_link_value=null`，sub1/sub2 缺失；click_event 的三项协议参数全部缺失。游戏按 OneLinkRequest 的 `open_level` 校验拒绝，原因 `unsupported_action`。这是收到回调但没有有效跳转参数，不是 SDK 报 ERROR，也不是代码把已有目标参数读丢。前次对“外层有值而内层缺失”的假设不适用于这份新日志。
- 15:03:44，第 368–370 行：首页弹窗派发结束，经过弹窗/转场门禁后，原 URL 本地兜底因自定义 Scheme 不属于 HTTP(S) 而拒绝 `invalid_http_url`。这不是对 OneLink 短链接的网络请求失败。
- 网络证据：第 274 行 Firebase 原生请求返回 `NSURLErrorDomain -1009`，包含 `Denied over Wi-Fi interface`；第 310 行为 `NotReachable`。15:03:44 第 340–345 行网络状态转为 Wifi。可以确认启动时请求受网络不可用影响，但不能仅凭日志区分系统联网授权、用户断网或其它网络原因，更不能直接证明它导致 OneLink 归因缺失。
- 15:04:01，第 865 行 AppsFlyer 返回 `is_first_launch=true, af_status=Organic, af_message=organic install`，没有 deep_link 参数。本次安装结果没有提供可进入的玩法/关卡；并且本次从未 ERROR，OneLinkManager 的 ERROR 重试及无 URL 安装补偿条件均未开启。不存在“十秒预算结束后丢弃了有效关卡数据”的日志证据。
- 直接结论：本次不跳转的游戏侧原因已定位为没有接收到有效跳转协议；新增“两次、总计十秒”重试未被触发。需要继续定位真实点击到安装归因/系统 URL 传入之间的过程，不能通过放宽关卡参数校验或把所有空参数 FOUND 盲目重试来认定已解决。
- 用户随后确认操作为“点击 OneLink 后，通过 TestFlight 下载并安装，再从桌面打开”。这是安装后延迟跳转测试，不是已安装状态下点击链接的直接唤起。该场景不要求 Unity 一定收到原 OneLink URL，不能将日志没有短链地址单独当作失败原因；关键是 SDK 后续没有交付有效目标参数。
- 用户进一步实测反馈“第二次点击链接可以唤醒并跳转”。该反馈证明这台设备、该包和该链接在这次已安装点击场景下能完成唤起及进关；第二次成功的原始日志未提供，不能推定具体通过 Universal Link 还是 Scheme。直接点击成功不证明首次安装点击已经匹配，也不排除重复安装测试设备设置的影响。排查范围应集中到首次安装的点击匹配与参数恢复，不应继续把通用唤起或关卡路由当作首要疑点。
- 官方依据：[AppsFlyer 安装归因测试说明](https://support.appsflyer.com/hc/en-us/articles/360001559405-Testing-the-SDK-integration-for-marketers) 明确允许待发布 iOS 应用在点击归因链接后通过 TestFlight 或 Xcode 安装；[iOS UDL 文档](https://dev.appsflyer.com/hc/docs/dl_ios_unified_deep_linking#testing-deferred-deep-linking) 描述点击、安装、启动后由 SDK 匹配点击并取回参数的流程。不能仅因安装来源为 TestFlight 就判定不支持或已经找到根因。
- 用户对测试设备登记回答“不确定／还没有登记”。这仍需后台确认，不能把未登记当作已证实根因。`uncertain`：OneLink 点击是否被后台记录并关联此安装、有效测试设备登记、OneLink iOS 目标应用与 TestFlight 包及 SDK App ID 是否一致、上述自定义 Scheme 的来源均未核验。后续应对照后台点击/安装记录和原生 DDL 请求日志，而非要求桌面启动必须携带短链接。
- 下一步先按 [官方测试设备登记说明](https://support.appsflyer.com/hc/en-us/articles/207031996-Registering-test-devices) 核对 `Settings → Test Devices`。日志第 303 行 IDFA 全零，实际有 IDFV，因此当前非授权跟踪测试应使用 IDFV；官方指出删除同开发者全部应用后重装可能导致 IDFV 改变，重复安装测试需要同开发者辅助应用或官方 IDFV 工具维持标识，再核对登记值。登记保存可能需要几分钟生效。该前置检查可排除重复安装识别干扰，不保证本次缺失参数一定由它导致。
- 当前安装补偿代码只覆盖正式 ERROR 且无 URL 的分支，不会因一次 FOUND 缺少业务参数而开启。这是已确认的覆盖边界；但本次实际安装归因结果本身为 Organic 且没有目标参数，即使放宽该门禁也不能据此认定本日志场景就可恢复跳转。
- 其它独立异常：第 156 行广告模块 `SynchronizationContext` 异常，第 270/326/338 行 IAP 无商品，第 464/467 行广告配置回退。没有证据将其列为本次 OneLink 参数缺失的原因；本轮未扩展为这些模块的修复或风险评估。
- 验证仅为日志全文检索、次数统计和当前 Manager/Request 分支静态核对，不包含真机复现、SDK 原生请求追踪、后台或最终 IPA 配置检查。回滚本次仅需撤销本节和当日分析记录，无运行时或存档变更。

## 2026-09-24：日志 14 已返回目标参数，游戏侧未接收进关

- 来源：用户登记测试设备后提供 `com.brain.tricky.puzzle.games.test.quest_BP-YYGFX_debug_log(14).zip`，SHA256 为 `5C599674AEEF685BADF44C2368A6621FACD9ADB08ED3CA22E4239BE4D54E7A73`。解压副本位于 `C:/Users/admin/AppData/Local/Temp/brainpunk-onelink-ios-20260924-log14/`。以下行号对应 `gurusdk.log`，1053 行，时间范围 15:17:38–15:17:58；仍为 iPhone 1.15.1、onelink-v4。
- 与日志 13 的实测差异：归因由 Organic 变为 Non-organic；安装回调新增 `deep_link_value=open_level`、`deep_link_sub1=default`、`deep_link_sub2=level_022`、`shortlink=gacjwdmm`、`media_source=DeepLink_test`、`is_first_launch=true`。目标是主线资源 `Level_022`，不是已证明的当前排序第 22 关。该组协议参数符合当前 OneLinkRequest 解析条件，未实测加载目标关卡。
- 两份日志的 IDFV 不同（13 为 `03D5593A…`，14 为 `6FB47132…`）；不能仅因用户已登记就断言实际登记值与本次安装一致，也不能证明归因变化只有登记这一个原因。本轮已经取得目标参数，不应继续把“后台完全没有识别到链接”作为本轮结论。

| 时间 | 日志行 | 事实 |
| --- | --- | --- |
| 15:17:39 | 223–226、273–275 | 收到无协议参数的应用 Scheme `google/link/`；UDL ERROR，开始 10 秒总预算。 |
| 15:17:41 | 337–342 | 系统网络从 None 变为 Wifi；仍需等待首页门禁。 |
| 15:17:46 | 774–775、791–792 | 首页启动弹窗派发结束后开始第一次重试，重交的仍是 `包名://google/link/`，该请求只剩 2.65 秒预算。 |
| 15:17:47 | 834–844 | 两次 FOUND 均无 action/sub1/sub2；其中携带主动请求标识的结果以 `invalid_parameters` 结束并停止重试，不安排第二次。 |
| 15:17:49 | 847 | 总预算到期，`total_timeout, attempts=1`。 |
| 15:17:58 | 962–967 | 安装归因回调返回 Non-organic 和完整目标参数，距 ERROR 约 19 秒、距总预算结束约 9 秒。 |

- 全文统计：OneLink 回调 3 次（ERROR 1 次、FOUND 2 次），主动重试开始 1 次，`queued=0`、`entry_accepted=0`。最终有效参数出现在安装归因回调中，没有作为有效 UDL 目标排队。这次已经证实 iOS 公开接口重交 URL 会触发带请求标识的回调，但本次对象并非带目标参数的 OneLink 地址，不能称为完整重试跳关成功。
- 当前源码直接解释未受理：`OneLinkManager.cs:426` 订阅安装归因；`:684` 在解析数据前检查 `ExpireErrorRetry()`，而 `:580–593` 对过了十秒的本次恢复返回 true。本次 15:17:58 到达时必然受该截止条件限制。另有独立限制 `:684–685`：恢复不能已停止且必须没有 URL；本次此前已被 `invalid_parameters` 停止，且 `_errorRetry.Url` 保留了 google/link 地址，任一条件都阻止补偿。因此不能只把总时限调长就认定此问题可修复。
- 游戏侧的实现边界：`:506` 优先选用任意非空 `_systemUrl` 进行重试，而安装补偿把“有任何 URL”视为不需要安装参数补偿。本次 google/link 无业务参数，仍被用于重交并阻挡后续有效安装数据，这是本轮日志实际暴露的覆盖缺口；该地址的来源及是否影响原生 DDL 行为仍未证明。
- 用户现行要求是“两次、十秒内收不到就算了”，前轮实现明确包括超时后忽略晚到结果。若严格保留这个边界，本次约 19 秒才返回的有效安装参数应继续放弃；若希望这类结果仍进关，需要明确把“主动请求最多两次/十秒”与“SDK 自行返回安装参数后的首页处理”分开，并保留去重、不打断游戏与最新点击优先。后者是待确认的行为调整，本轮只分析，不改运行时代码。
- 证据范围：比对两份原始日志的同类字段、数量和时间线，静态核对现有接收与丢弃分支。没有访问 AppsFlyer 后台、修改注册、测试网络环境或重新构建运行。更新本节及当日记录；回滚只撤销文档新增，无存档或运行时变更。

## 2026-09-24：v5 三次主动重试与晚到安装回调（当前规则）

- 用户已在日志 14 分析后确认修改：主动重试改为最多三次，保留从首次 ERROR 起总共 10 秒；SDK 自行晚到的有效参数仍可排队跳转。本节替代前述 v4 的两次上限和超时后一律丢弃结果规则，日志 14 的“待确认”属于当时的分析状态。
- 实现范围仅为游戏侧 OneLinkManager 与文档。继续使用 SDK 现有接口、OneLinkRequest 校验、StartPanel 门禁和 OneLinkLevelRouter；不改 SDK、平台配置、资源或存档结构。
- 主动重试最多三轮，单轮等待最多 10/3 秒并裁剪到总预算，最早在 ERROR 后 0、10/3、20/3 秒开始。首页、联网和弹窗等待仍计入预算，实际次数可以少于三次。无有效 URL 时不能凭空重发请求。ERROR 或本轮等待超时才安排后续轮次；NOT_FOUND、参数非法或原生异常不继续盲目重试。
- 系统 URL 只有属于已支持的 `brainpunk.onelink.me/模板/短码` 或包含有效玩法协议才参与自动路由；无参数的 google/link、未知地址和重复协议字段等无效 URL 记录后忽略，不重交、不覆盖待处理目标、不阻挡安装补偿。原始字段诊断保留在 `source=unity`，无效地址不再等到首页处理后才拒绝。正式 ERROR 中的 link 同样校验。
- iOS 安装回调独立于 ERROR、重试停止和十秒预算，仍需首次启动、Non-organic 和有效协议；只有当前会话尚未收到真实玩法链接/GM 测试、尚未受理目标时才补偿。已收到系统玩法链接或 ERROR 携带的有效地址后，不允许安装数据或无标识 deferred UDL 覆盖这次直接点击，即使这次直接点击后来超时。安装补偿先受理后，同次 UDL 不再排队；UDL 先受理后，安装结果不再排队。
- 有效结果只写入现有 pending 请求。调用链为 SDK UDL/安装回调 → OneLinkRequest → pending → `StartPanel.LateUpdate` → `TryNavigateFromStartPanel` → OneLinkLevelRouter；首页动画、弹窗、场景、前台和空闲门禁全部通过后才进关。回调期间不重入 UI、不发全局事件、不额外锁输入。实际解锁仍由原路由执行，不写通关记录、不改存档结构。
- 当前唤起的自动请求标识在主动等待结束后仍允许成功结果入队；只保留本次原请求及最多三轮重试的 URL/标识，不保留已结束计时器。新点击、GM 测试、已成功受理或会话结束会清除旧请求标识，旧点击的带标识结果不能再覆盖新请求。同次早一轮重试晚成功时可结束正在等的后一轮。GM 手动测试仍按原来的单次十秒处理，超时/关闭后的带标识结果仍丢弃。
- 保留 SDK 的 `BehaviorSubject` 安装数据订阅，订阅时可收到当前值；只读证据为 `Packages/com.guru.sdk.framework.analytics.appsflyer/Runtime/GuruAppsflyerManager.cs:22–39`。不新增 SDK 启动或无 URL 网络补拉。自动解析共享一个结果处理方法，解析失败的 click_event 字符串保持非法，不用根对象参数将其改造成有效 click_event。
- 日志标记为 `diagnostics=onelink-v5`。`retry_started` 记录实际轮次；`retry_stopped/total_timeout` 仅表示停止主动等待；`url_ignored/unrelated_url` 说明无关地址；`source=install_recovery` 记录回调协议参数；`install_recovery_ignored` 区分已受理、新链接/测试优先、非首次非自然安装；`install_recovery_queued, afterRetryDeadline=True` 可识别超过主动预算后补收。`auto_url_late_resolved` 记录当前唤起的带标识晚到结果，最终仍以 `entry_accepted` 查看进关请求受理，不能等同资源加载成功。

### v5 验证与风险

- TEMP 测试直接编译当前生产 Manager/Request/Router，五种宏组合通过：iOS 144、iOS DEBUG_MODE 168、Editor 115、Android DEBUG_MODE 215、Android 无 DEBUG_MODE 154 项断言。覆盖三轮次数与间隔、十秒边界、离线/弹窗耗时、晚到带标识和无标识 UDL、真实安装参数、重复回调、更新点击、GM、已有 SDK 安装结果订阅和会话退出清理。
- 日志 14 复现使用原始 `gurusdk.log` 中的实际安装 JSON：第 0 秒 ERROR、google/link 与空参数 FOUND，第 10 秒结束主动预算，第 19 秒发出安装回调；模拟中没有重交 google/link，`default/level_022` 成功排队，首页空闲后仅受理一次主线 `Level_022`。路由表用对应 Path 的模拟条目检查，未加载真实关卡 Prefab，不是新 IPA 真机验证。
- 复用 Unity 引用进行 Roslyn 定向编译：Editor 0 错误/105 警告，Android 无调试宏 0 错误/108 警告；真实 Unity/AppsFlyer/Guru/R3 程序集的 iOS 接口定向编译 0 错误/4 个同名类型 CS0436 警告。完整 iOS 工程编译仍有前节记载的本机 GameKit 引用缺口，本轮未重复该已知失败检查；iOS 接口定向编译继续使用 TEMP 中未修改路由的替身处理内部类型边界。
- 证据目录：`C:/Users/admin/AppData/Local/Temp/brainpunk-onelink-latecallback-20260924/`，`RetryTests.cs`、`v5-*-tests.log`、`v5-*-compile.log`。命令为 `dotnet run --project HarnessAutoUrl.csproj -p:DefineConstants=...` 和 `dotnet csc.dll @editor.rsp / @android.rsp / @ios-api.rsp`。调整旧模拟中的占位 URL 为真实支持的短链，并将旧“超时必须丢弃”断言更新为已批准的新行为；首次 iOS DEBUG_MODE 检查还修正了旧测试只适用于 Android 的短链提示与主动请求断言，不改生产平台限制。
- 修改的旧注释只涉及本轮已改变的事实：Url 不再接收无关地址、预算超时不再丢弃全部有效回调、安装回调不再依赖 ERROR；其它旧注释保留。定向 `git diff --check` 通过。
- `uncertain`：尚未打包或真机运行 v5；新的 iOS/Android 原生返回时机、系统唤起与 SDK 回调先后关系仍需新包复测。游戏侧十秒超时不能中止 SDK 原生请求；没有关联标识的多个直接点击回调仍无法可靠确定归属。收到新直接点击后会舍弃安装补偿，即使新点击没成功，这是避免旧安装参数覆盖用户新选择的明确边界。游戏中晚到参数会保留到回首页，不立即中断当前关卡。
- 回滚：仅撤销本节对应 OneLinkManager 修改，并同步回退本节和当日 v5 记录；恢复 v4 两次、五秒单轮及十秒后不受理的行为。不回滚 SDK、原生配置、资源或其它用户改动，也不撤销已通过原路由执行的解锁存档。

## 2026-09-24：每种玩法仅一次 OneLink 进关（实施约定）

- 用户最新口径：GameData 使用 `List<GameType> oneinlinkEnterType`，按玩法永久去重；不保存关卡名。主线、Draw、IWanner 各可触发一次，同玩法换新目标仍拒绝。
- 原行为：只过滤同次唤起中的重复回调，跨点击/重启可以再次进关；胜利面板始终提供重玩；IWanner 指定资源会进入组内对应小关。
- 目标行为：进关请求暂存玩法，在资源与运行时成功创建后才保存该玩法；受理失败、加载失败或取消不消耗机会。`IsEnterByOneLink` 是当前流程标记，按现有字段生成规则参与存档，启动及普通新关/退出时清除，Reset 保留；IWanner 组内推进保留到组结算。
- IWanner 指定资源名或配置 level ID 时定位所属组，再进入索引 0。找不到时保留现有传送门回退规则。TBN 保留既有独立入口。
- 三种玩法的普通/奖励胜利面板都隐藏本次 OneLink 进入后的重玩按钮并拦截其点击；普通进入恢复。复用 Inspector 已绑定按钮，不修改 Prefab、广告门禁、全局事件、通关统计及 `level_start/level_end` 约定。
- 风险与回滚条件：重点核对加载失败误消耗、旧存档空列表、普通进关残留标记、IWanner 组中途清标记、重复 SDK 回调。发生上述问题时回滚本节涉及的游戏侧路由/控制器/UI/存档修改；不得回写 SDK 或 Packages。已有解锁与玩法历史是玩家数据，不随代码回滚自动删除。
- 验证结果与证据见下节；当前没有 Unity Play Mode 或 Android/iOS 真机结果。

### 实现、验证与剩余边界

- 文件：`GameData.cs` / `GameData_Partial.cs` 保存并初始化玩法列表；`OneLinkManager.cs` 管理待加载/已进入状态；`OneLinkLevelRouter.cs` 在解锁前去重并解析 IWanner 组首关；`OneLinkRequest.cs` 更新协议注释；`PlayCtrl.cs`、`DrawPlayCtrl.cs`、`IWannerPlayCtrl.cs` 接入成功、失败、普通新关与退出；三个胜利面板基类读取来源快照，普通版及 Reward 派生版共用。
- 数据流：SDK/GM → pending → 首页原门禁 → `TryBeginEntry` 检查 `OneinlinkEnterType` → 原解锁/加载流程 → `ConfirmLevelEntered` → 列表追加玩法并 `DB.SaveGameData()`。不会保存 sub2 或 prefab 名。私有字段保持用户指定的 `oneinlinkEnterType`，公开属性与 JSON 命名遵循现有生成器；两个字段都按项目存档规则序列化，首页启动队列完成时清除上次会话的 `IsEnterByOneLink`，保留玩法历史。
- 写入时机：主线实例/HUD 创建完成；Draw 转场及 Intro 完成进入画线前；IWanner 当前有效加载完成、开放输入前。请求受理不等于已记录。主线异常保持原抛出行为并清除待加载来源；Draw/IWanner 复用原失败恢复。退出清理只影响对应玩法，主线停用不会清除正在切入 Draw/IWanner 的来源。
- 重玩：面板 OnOpen 按本轮玩法缓存来源，隐藏已绑定按钮，点击处理也拒绝；后续广告/购买按钮刷新沿用这个快照。普通新关重新取值，恢复按钮。同关 Reset/失败重试保留，IWanner 组内推进保留；退出及下一主线/Draw 关卡清除。没有新增全局事件或修改统计逻辑。
- 兼容：旧存档缺字段时使用字段默认值，列表为 null 时由 OneLinkManager 在启动/路由入口补空列表，不重置原关卡进度；不追溯旧版本已经发生的 OneLink 进入。不同 sub1 别名 main/default 共用 Default 次数，TBN 使用 IWanner 次数。GM 参数/URL 测试使用真实运行存档，也遵守每种玩法一次限制。
- 日志版本：`onelink-v6`。`entry_duplicate` 表示该玩法已用过；`entry_completed` 表示成功进入并带 `saved` 结果；`entry_context_cleared` 表示本轮来源清除；`save_warning` 的 `entry_history` 表示保存失败。保存失败时本会话仍防重复，但不能保证重启后的持久性；不能把 `entry_accepted` 当成已加载成功。
- 编译：复用当前 Unity 引用的 Roslyn Editor / Android 定向编译，分别 0 错误、105 / 108 个警告；没有新增编译错误。未进行完整 iOS 构建，本机此前记录的 GameKit 引用缺口未在本任务修复。
- 模拟检查：直接编译当前 Manager、Router、Request、GameData 及初始化源码，并提取当前 IWanner 进组方法，在 Editor / Android / iOS 条件下各通过 74 项存档、路由与状态断言。Editor 追加从三种胜利面板直接提取的按钮刷新/Replay 方法，18 项通过，总计 92 项；覆盖隐藏、直接调用拦截、广告/购买状态刷新、普通进入恢复和重复点击。模拟使用 SDK、Unity UI、异步加载替身，不等于真实场景运行。
- 静态检查：24 项源码/Prefab 检查通过，核对三种玩法普通/Reward 共六个 Prefab 的非空按钮引用、引用组件存在、Reward 复用基类刷新，以及新关/新组入口范围；定向 `git diff --check` 通过。
- 证据：`C:/Users/admin/AppData/Local/Temp/brainpunk-onelink-pertype-20260924/` 下的 `PerTypeTests.cs`、`VictoryHarness.cs`、`VictoryTests.cs`、`pertype-*-tests.log`、`*-compile.log`、`source-prefab-checks.json`。命令为 `dotnet run --project HarnessAutoUrl.csproj -p:DefineConstants=UNITY_EDITOR/UNITY_ANDROID/UNITY_IOS`（分别执行），编译使用 `dotnet csc.dll @editor.rsp` / `@android.rsp`。增加 UI 替身时第一次误将共享遮罩默认设为可见，导致首页门禁断言失败；只修正 TEMP 替身默认值后 92 项通过，未通过更改生产门禁规避测试。
- `uncertain`：未执行 Unity Play Mode、实际关卡资源加载/取消、真实 PlayerPrefs 磁盘故障、Android/iOS 横屏与 SDK 真机回调、原生广告恢复后的视觉检查。需新包使用独立存档分别测试三种玩法首进/重复/不同目标，IWanner 中间小关链接从组首开始并整组结算，普通进入恢复按钮，退出/重启/断网及失败加载不误记。入口三次重试/十秒预算本轮没有改动。
- 旧注释只调整两个已变化的事实：Request 的 sub2 支持 IWanner 配置 ID；IWanner 进组传入索引由原指定小关改为组首或回退索引。其它旧注释保留。
- 回滚范围：本节列出的存档、路由、三个控制器及三个胜利基类改动和对应文档；恢复仅回调级去重、IWanner 直接指定小关及常规 Replay 显示。不得批量还原其它字体/原生配置/构建信息改动，也不清理用户已解锁进度。若要恢复旧的 OneLink 产品策略，可保留未被读取的玩法历史字段，不需删除玩家存档。

### GameData 生成边界纠正：事后补查（2026-09-24）

- 本节是用户指出问题后的补查，不是修改前已完成的证据。上一轮只验证当前手写代码，漏掉生成链，不能据此声称符合项目工作流。
- 来源：`PartialClassGenerator.cs` 的 `GeneratePartialClassCodeStatic` / `ExtractPrivateFieldsStatic` / `BuildPartialClassCodeStatic`。输入为 GameData.cs 的 `[SerializeField] private` 字段，输出为 GameData_Partial.cs 的首字母大写属性和 Init；生成器不会检查手写同名属性，也不复制输入 using 或 Json 特性。
- 原错误：手写 `OneinlinkEnterType` 会与生成属性重名；手加 `IsEnterByOneLink = false` 无对应私有字段，重生成会丢；字段 `List<GameType>` 的 using 不会进入生成文件。7 月 29 日 DrawGame 生成边界记录已存在，本次未及时查阅。
- 纠正位置：GameData.cs 仅新增两个序列化私有字段，枚举使用完整类型名 `Config.Level.GameType`；_Partial 只同步实际生成器输出的两个属性和对应初始化。空列表修复及启动时清除上轮来源放在 OneLinkManager。保持每玩法一次、成功后记录、普通进关恢复 Replay 和 IWanner 组首进入的既定行为。
- 纯代码生成方法已逐字提取到 TEMP 执行，不调用批量写盘/Unity 资源 API，不改生成器。预览发现现有 `giftEntryStateVersion` 初始化在手写文件默认 0、旧 Partial 为 1，且生成器不保留已有属性注释；属于本任务之前的差异，本次不覆盖它们。
- 项目 `AGENTS.md` 增加“修改前核对门槛”，由规则队列 BP-AI-017 的重复返工证据提升。核对记录必须先于生产代码改动；事后补查不得伪装成事前检查。不是增加用户审批步骤。

- 纠正后的验证：实际生成器纯代码方法预览输出中，两处属性及初始化与本次同步结果一致；将整份重新生成的 Partial 仅作为 TEMP 编译输入，Editor 定向编译 0 错误，证明不会再因本轮字段产生重名或类型引用缺失。当前工作区 Android 定向编译 0 错误。更新序列化口径后，Editor 模拟 98 项通过，包括旧档 null 列表入口修复、上次保存的来源在启动清除、历史保留及重复启动通知不清除当前来源。证据为同目录 `generator-check/`、`corrected-editor-tests.log`、`corrected-regenerated-editor-compile.log`、`corrected-android-compile.log`；前节 74/92 项为纠正前证据，不能用来证明生成兼容性。尚未运行 Unity 编辑器菜单或真机，生成菜单的资源/界面侧效果仍未验证。

### 2026-09-24：用户要求重新检查后的复核

本轮为既有实现的复核，不是首次修改前的核对。本轮没有修改业务代码、Prefab、SDK 或存档；不提交 Git。范围为每玩法一次、当前进入来源、三玩法普通/Reward 胜利页重玩限制、IWanner 指定目标进入组首，以及这些改动依赖的生成、首页和加载链。`037-remote-resource-loading/plan.md` 当前缺失，未以不存在的计划作为证据。

| 验收对象 | 当前源码依据及检查结果 | 证据范围 |
| --- | --- | --- |
| GameData 维护方式 | `PartialClassGenerator.GeneratePartialClassCodeStatic` 读取序列化私有字段，`BuildPartialClassCodeStatic` 生成属性/Init；重新核对 TEMP 中提取的方法与当前生成器一致，两个新增属性和初始化与重新生成结果一致 | 源码、真实生成方法的隔离预览及编译；不运行 Unity 批量生成菜单 |
| 每种玩法一次，不记录关卡 | `OneLinkManager.TryBeginEntry` 在解锁前查玩法列表，`ConfirmLevelEntered` 才追加并保存；main/default 共用 Default，三个玩法独立，重复目标与同玩法新目标均拒绝 | 当前生产 Manager/Router/Request/GameData 参与模拟；真实磁盘持久化仍未实测 |
| 首页门禁 | `LoadManager.StartStartupPopupSequence` 队列末尾通知；`StartPanel.LateUpdate` 检查进场动画；Manager 再查前台、Main 场景、START 状态及 `UIManager.HasBlockingPopup` 的栈/关闭动画/遮罩 | 完整相关源码链；真机唤起和 UI 动画待验 |
| 成功/失败分界 | 主线在实例及 HUD 创建后确认；Draw 在有效转场及 Intro 完成点确认；IWanner 在有效加载和淡入完成后确认。路由拒绝、控制器失败及退出有清理入口 | 已读实际异步调用及清理链；旧模拟通过直接调用 Confirm/End 验证状态，不等于真实加载故障或取消实测 |
| 当前来源生命周期 | 普通新关、新组与退出清除；Reset 保留；IWanner 组内推进不调用新组入口，保留至组胜利；重启通知清除保存的旧来源但保留玩法列表 | 源码和模拟。普通入口仍可游玩已使用 OneLink 的玩法 |
| 重玩按钮 | 三种面板 OnOpen 在按钮刷新前读取来源；普通/Reward 六个 Prefab 的按钮引用存在；Reward 刷新调用基类且未另行恢复重玩可见性；处理函数也拒绝 OneLink 重玩 | 提取按钮方法与当前源码一致；按钮隐藏、点击拦截和普通来源恢复模拟通过。未覆盖真实 Spine/Tween/原生覆盖层画面 |
| IWanner 指定关卡 | 资源 Path 或配置 level ID 匹配后传入索引 0，控制器显式索引优先于旧续关位置。本地 `30002`/`ILevel_002` → group 1 / `ILevel_001`；`30009`/`ILevel_009` → group 2 / `ILevel_006` | 实际本地 JSON、路由源码和入口模拟；不等同当前手机远端资源已交付 |
| 不存在目标及解锁 | 保持主线最小未通关、Draw 最终列表首个未通关、IWanner 最小未完成组内未成功小关的现有回退；解锁不写通关。异步失败不撤回之前已执行的目标解锁/当前选关进度 | 源码；明确保留既有副作用边界，不宣称失败会恢复全部存档 |

#### 本轮验证与结论边界

- `HarnessAutoUrl.csproj` 直接引用当前 OneLink 三份源码及 GameData，在 `UNITY_EDITOR`、`UNITY_ANDROID`、`UNITY_IOS` 三种宏下各通过 98 项断言。与此前只复跑 Editor 相比，本轮另外检查了修正后的 Android/iOS 宏组合；SDK、Unity 和异步资源仍有替身。
- 使用已核对一致的实际生成器纯方法重新生成 TEMP Partial，以该文件替换编译输入后，Roslyn Editor 定向编译退出码 0，0 错误/105 警告。没有覆盖工作区 Partial；此前记录的 `giftEntryStateVersion` 旧默认值差异仍保留，不能因此批量重写生成文件。
- 34 项静态检查通过，包括生成方法来源一致、新增生成成员一致、提取 UI 方法一致、OnOpen 顺序、六个 Prefab 按钮引用、三个 Reward 继承和本地 IWanner 组首配置。证据为 TEMP `brainpunk-onelink-pertype-20260924/recheck-static.json`、`recheck-UNITY_*-tests.log`、`recheck-GameData_Partial.generated.cs`、`recheck-regenerated-editor-compile.log`。
- **直接结论**：在本轮源码、生成预览、静态资源和模拟覆盖的场景内，未发现新增的明确需求偏差；原生成属性重名/类型引用问题未在当前生成结果中出现。此结论不能外推为整个功能或其它历史功能已经全部正确。
- **uncertain / 缺失证据**：Unity PlayMode、实际资源失败/取消及晚到完成、真实 PlayerPrefs 保存故障、手机冷启动/回焦和普通/Reward 胜利页广告后的画面尚未运行。尤其主线 `CreateLevelAsync` 没有 Draw/IWanner 的 activationVersion 校验，旧模拟的取消用例仅覆盖 End 后没有新请求的情况，不能证明取消后同玩法重入与旧完成交错安全；需要从真实可达入口验证，不据孤立状态调用认定已经发生问题，也不擅自扩大本次改动。
- 后续真机检查应使用独立存档：三玩法首进/同目标重复/同玩法换目标/换玩法；三玩法 Reset 和退出后普通进入；IWanner 中间小关链接从组首游玩至组结算；缺失配置与资源加载失败、取消后重入及迟到回调。每个结果应记录入口、设备、包版本和日志，不以 Editor 宏模拟代替。
- 本轮只追加复核记录。业务回滚仍按上文 v6 范围执行；记录回滚只删除本节，不清理玩家历史或其它工作区改动。


### 2026-09-24：主线异步交错补查（代码级复现，待修复）

本次仍为检测，没有修改业务代码、生成器、Prefab、SDK 或配置，也没有提交 Git。上次“未发现新增的明确需求偏差”只覆盖当时的顺序模拟；本节补充实际方法的异步交错证据，不能继续用原 98 项通过推断取消后重入安全。

#### 发现：旧主线加载会修改新 OneLink 请求的记录（P2）

- **facts / 源码事实**：`PlayCtrl.CreateLevelAsync`（675–715 行）在资源 await 后直接写共享 `LevelPrefab`、创建关卡并调用 `ConfirmLevelEntered(Default)`；空资源及异常调用 `EndEntry(Default)`。`ResetRuntimeForControllerExit`（854 行）会结束来源、销毁当前现场，但没有使正在等待的主线资源请求失效。清理由 `Deactivate` / `TryRestoreStartPanel` 调用，后者来自 `GameManager.ReturnToStartPanel`。`OneLinkManager.ConfirmLevelEntered` / `EndEntry`（140 / 155 行）只核对玩法，没有区分请求 A、B。Draw/IWanner 的对应异步路径则检查 `activationVersion`，不能据此把主线也当作已经受保护。
- **触发条件**：主线 A 的资源加载尚未返回 → 控制器被清理并返回首页 → 新 OneLink 主线 B 开始加载 → A 晚到。这里的 A 可以是旧的普通主线加载；错误不要求此前已消耗主线的 OneLink 次数。
- **direct conclusions / 代码级复现**：保持 A/B 两个资源等待独立，执行当前生产 `CreateLevelAsync` 与退出清理方法。A 成功时，B 仍未完成就出现 `IsEnterByOneLink=true`、历史含 Default；随后令 B 失败，次数仍已消耗。A 返回 null 或抛异常时，会清除 B 的待进入来源；B 随后成功，标记仍 false、历史为空，因此无法按该次 OneLink 来源隐藏胜利页重玩，后续也可能再次自动进入。
- **根因范围**：主线异步资源缺少失效检查是既有结构问题；本次新加的按玩法 Confirm/End 将其影响扩展到了 OneLink 来源和永久次数。不能把既有资源竞态全部归为本次新增，也不能声称新记录逻辑已隔离它。
- **hypotheses / 尚未确定**：实际玩家通过哪些可见按钮、何种资源耗时能在初次加载遮罩期间形成上述清理/重入时序，尚未完成 Unity/手机操作复现。该缺口不证明普通顺序点击必然错误，也不是用户现有 iOS 日志的已确认根因。

#### 验证方法与边界

- 隔离脚本位于 `C:/Users/admin/AppData/Local/Temp/brainpunk-onelink-pertype-20260924/MainAsyncAudit.cs`，项目为 `MainAsyncAudit.csproj`，结果为 `main-async-audit.log`，来源说明及源码摘要在 `main-async-audit-source.json`。
- 两个生产方法的函数体逐字提取；仅将 `CreateLevelAsync` 的返回包装从 `UniTask<bool>` 换为 `Task<bool>`，使用可控制完成顺序的资源任务。OneLinkManager 和 GameData 直接编译当前源码；Unity 对象、UI、状态机及资源依赖为替身。因此这是异步逻辑的隔离复现，不是 Unity/真机实测。
- 命令：`dotnet run --project MainAsyncAudit.csproj --no-restore`。正常顺序基线写入一次；旧任务成功、空资源、异常三种交错均复现预期的错误状态。该项目断言的是“缺陷存在”，退出码 0 不代表业务验证通过。
- 同时复跑 `dotnet run --project HarnessAutoUrl.csproj -p:DefineConstants=UNITY_EDITOR --no-restore`，98 项原有合同模拟通过，日志 `deep-review-contract-tests.log`。它证明旧测试没有覆盖上述交错，不能抵消本次发现。
- 重新沿链核对了生成字段归属、每玩法一次、IWanner ID/Path 到组首、普通/Reward 重玩快照、首页门禁及普通新关/Reset/退出边界；这些已检查路径未新增其它明确发现。Draw Intro 的 Handler 异常在 `DrawLevelCtrl.PlayIntroHandlersAsync` 内处理，不能只看外层没有 catch 就认定它必然使 OneLink 挂起。
- **missing evidence / uncertain**：真实操作可达时序、完整 Unity 状态机与资源系统、手机 SDK 冷启动/回焦、实际存档磁盘失败、原生广告后的界面仍未验证。

#### 修复方向及范围限制

- 待修复：主线必须让已退出或已被新进入替代的异步任务失效，旧任务不得创建关卡、推进状态或调用新请求的 Confirm/End；OneLink 记录也应与具体进入请求关联。
- 动手前需核对主线 Enter/Next/Reset/Replay、漫画回调、翻页回调和退出清理的完整时序，避免只给 Confirm 增加判断却留下旧关卡实例或旧状态推进；仍须遵守已有打点生命周期约定。本次只完成检测，不以未经验证的共享重构代替修复。
- 本轮产物为本节、当天记录和 TEMP 验证文件；回滚只移除本轮记录/隔离产物，不回退既有工作区业务改动或清理玩家存档。

<!-- 以下补入对方分支历史记录；最终合并规则与本次验证以文末合并章节为准。 -->

## 2026-09-23：补充 iOS OneLink Associated Domains

- 项目侧 `Assets/Guru/guru_spec.yaml` 的 `details` 增加 `ios_domains: [brainpunk.onelink.me]`，保留原有 `authority: brain.gurugame.ai`。该配置用于让 iOS 导出同时生成 `applinks:brain.gurugame.ai` 和 `applinks:brainpunk.onelink.me`。
- 只修改项目源配置，没有修改 `Packages/`、GuruSDK 安装目录、AndroidManifest 或生成的 Xcode 工程。YAML 解析检查已确认 `ios_domains` 读取为 `brainpunk.onelink.me`。
- 重新导出 iOS 后，必须在最终 `Unity-iPhone.entitlements` 和 Xcode Signing & Capabilities 中确认两个 Associated Domains；当前未重新导出、未重新签名，也未完成 OneLink 后台/AASA 和真机冷启动、热启动验证，相关结论仍为 `uncertain`。
- 回滚：移除 `details.ios_domains` 下的 `brainpunk.onelink.me` 即可恢复本次配置前状态，不涉及代码、SDK 或玩家存档。

## 2026-09-23：自动跳转跳过启动 ChestGift

- OneLink 请求在 StartPanel 首次进场完成时仍处于待处理状态，`LoadManager` 会跳过启动序列中的 ChestGift，再执行既有 `NotifyStartupPopupsDispatched()`；公告和旧数据转换弹窗不受影响。
- 判断复用 `HasPendingRequest`，覆盖有效参数已排队、系统 URL 等待解析和自动解析中的状态；该门禁不消费或清除请求，目标解析、首页阻塞检查和进关仍由 OneLinkManager 原流程负责。
- 只影响 Loading 的启动自动礼包，不关闭已经打开的 ChestGift，也不改变主页入口、首次胜利礼包、购买、奖励、Daily 或存档。
- 完整运行时程序集编译为 `0 error / 1030 warning`。Android/iOS 真机冷启动、UDL 晚到和普通启动礼包回归仍为 `uncertain`；回滚时移除 LoadManager 的待处理请求检查即可。

## 2026-09-23：Android OneLink App Links 主 Activity 配置

- 初始需求：为 `https://brainpunk.onelink.me/IjED/<short-id>` 增加 Android App Link。主 Activity 原先只有 `brain.gurugame.ai/default_link`；远端提交 `4b7c3ab5f` 增加 `brainpunk.onelink.me` 的独立 VIEW/DEFAULT/BROWSABLE、HTTPS、`autoVerify=true` 过滤器，限制 `pathPrefix=/IjED`。
- 用户确认当前**只使用 IjED 模板**，因此拉取远端提交后保留 `/IjED` 路径限定；没有保留此前工作区内对整个域名所有路径放行的配置。原 `brain.gurugame.ai/default_link` 入口及游戏 UDL 路由、存档、统计和 GuruSDK 不变。
- 静态 XML 检查通过。尚未针对**收窄后的最终源码**重新构建 APK，也未进行真机验证。新增其它模板时须另行确认并调整入口；`pathPrefix` 是前缀匹配，不等于对模板 ID 的精确段匹配。
- 回滚本次收窄：移除新过滤器的 `android:pathPrefix="/IjED"`，会恢复域名下所有路径匹配；本次并不回滚整个 OneLink 域名入口。

## 2026-09-23：Android 15 模拟器外部短信点击长短链实测（收窄前的 APK）

- 测试包：`BrainPunk/BuildOutput/Android/Tricky_Box_DEBUG_1.16.0_26092364.apk`，证书 SHA-256 为 `C3:A7:8E:D0:DF:42:C7:C5:8A:F1:C3:70:20:F4:A9:E2:CE:88:E5:FF:38:9C:BC:20:EA:D6:38:35:44:A0:C1:A0`。设备：Pixel_8_Pro 模拟器 Android 15（API 35），arm64；`adb install -r` 安装，不卸载、不清应用数据。此包为当时未限定路径的配置，不可当作收窄后重建包的验证。
- 域名：当日 `brainpunk.onelink.me/.well-known/assetlinks.json` 返回 HTTPS 200，JSON 包名、关系与上述证书指纹一致；模拟器 `pm verify-app-links --re-verify` 后 `pm get-app-links` 显示 `brainpunk.onelink.me: verified`。此前 404 是较早状态；旧域名 `brain.gurugame.ai: legacy_failure` 不计作新域名验收。
- Google Messages 中实际点击短链 `https://brainpunk.onelink.me/IjED/gacjwdmm` 及完整长链，均由消息应用发起 VIEW Intent 并打开游戏主 Activity。`/tmp/deeplink-sms-short-stream.log`：`url_received` → `auto_url_started` → `callback FOUND` → `queued Default/level_022` → `target_resolved path=Level_022 sortId=36 fallback=False` → `entry_accepted`；`/tmp/deeplink-sms-long-stream.log` 也出现 `callback FOUND`、排队及相同目标的 `entry_accepted`。截图 `/tmp/deeplink-sms-long-final.png` 显示 Level 36。
- 结论限于该 Debug APK、模拟器、已安装应用的短信点击及当时在线后台；真机、Play 重签包、iOS、未安装延迟深链和**当前收窄配置重新构建后**的表现均为 `uncertain`。当时回调 `status=FOUND` 同时带 `error=UNEXPECTED`，只能以实际排队进关为该次结果，不能断言错误字段成因。非 OneLink 的广告/计费报错也不能视为已经修复。

## 2026-09-23：Extended DDL 的 GCD 备用候选

- **依据与范围**：本轮用户指定的 `[EXT]AppsFlyer-延迟深度链接实现方式 (1).pdf`「推荐方案／Extended DDL／GCD 配置流程」，以工程最终 `open_level` 协议为业务目标；下载目录的活动／副玩法示例不改变已上线协议。Android 和 iOS 共用游戏侧逻辑；本轮只验 Android **已安装唤起**，不把它当作 GCD 首次归因验收。
- **原行为**：`GameAnalytics` 中 Guru 提供 UDL 初始化与 GCD 原生接收，但 `OneLinkManager` 只订阅 `AppsFlyer.OnDeepLinkReceived`。纯首次安装 GCD 不进入游戏路由；UDL 已排队后按首页动画、弹窗与玩法开关门禁处理。
- **本次变更**：`OneLinkManager.cs` 在同一个 Guru 回调对象上订阅 `ObservableConversionData`，不重启 SDK／修改 Packages。仅 `af_status=Non-organic` 且 `is_first_launch=true` 且参数能被原 `OneLinkRequest` 解析的 GCD 目标候选参与跳转；仅有 SRN campaign 的包保持首页，不根据广告名称推断路径。若 UDL 未给出有效目标，GCD 候选最多等 UDL 5 秒后经相同首页门禁与 `OneLinkLevelRouter` 进入；有效 UDL 优先、相同目标去重、已实际尝试 GCD 后忽略迟到的 deferred UDL，不影响之后的直接唤起。无 GCD 候选时不增加等待，也不改启动 Loading。
- **数据与风险**：候选仅保留当前会话内存，不新增持久化字段、资源地址、全局事件或关卡修改；退出时退订并取消计时。`gcd_candidate/gcd_no_target/gcd_queued/gcd_skipped/udl_late_ignored` 只记状态和原因，不新增完整归因日志。SDK 的 Guru 原有完整归因打印仍属于 SDK 只读代码，分发日志前需自行脱敏。单次入口的受理仍不证明异步关卡加载完成。
- **另见**：`SRN渠道GCD参数与投放交接清单.md` 列出渠道 ID、渠道配置者、AF 后台交接和后续 SRN 真正验收前提。真实 GCD、iOS 签名包、Google Play 分发包与无投放的 SRN 均标记 `uncertain`。
- **新包构建**：使用 Unity 6000.0.59f2 调用 `BuildToolV2.BuildAndroidDebug`，产物 `BuildOutput/Android/Tricky_Box_DEBUG_1.16.0_26092378.apk`；APK `versionCode=26092378`，实际 SHA-256 签名为 `C3:A7:8E:D0:DF:42:C7:C5:8A:F1:C3:70:20:F4:A9:E2:CE:88:E5:FF:38:9C:BC:20:EA:D6:38:35:44:A0:C1:A0`。`aapt dump xmltree` 确认最终 APK 包含独立的 `https://brainpunk.onelink.me/IjED` VIEW/DEFAULT/BROWSABLE/autoVerify 过滤器。Unity 日志 `/tmp/deeplink-gcd-build-20260923.log` 出现 `Guru Build End` 并产出 APK，同时 **AppleGameCenter 预处理器报了缺少 `link.xml` 的异常**；该构建告警不能隐瞒，不擅自修改只读 SDK。
- **新包 Android 外部唤起测试**：在 Android Studio SDK 的 Android 15 Google Play 模拟器中创建独立、一次性 `/tmp/deeplink-gcd-avd/QA_20260923`，不卸载用户原有 AVD 的包或数据；`adb install -r` 新包成功。2026-09-23 在线 `https://brainpunk.onelink.me/.well-known/assetlinks.json` 返回 200，但仅列出同包名 **`78:52:AC:7F:F3:8D:D3:73:45:35:E2:C5:D9:18:59:F7:2B:69:85:70:C7:5B:D6:51:3C:E3:DF:06:8F:57:6B:57`**，与本 APK 的 C3 指纹不一致；模拟器 `pm verify-app-links --re-verify` 后 `brainpunk.onelink.me: 1024`，用户选择状态为 Disabled。从 Google Messages 收到模拟短信后**实际点开**短链接 `https://brainpunk.onelink.me/IjED/gacjwdmm`，日志 `/tmp/deeplink-gcd-qa-click-logcat.txt` 显示 VIEW intent 分配给 Chrome 的 `IntentDispatcher`，最终停留 Chrome 首次运行页，**未唤起游戏**。这证明本次失败首先卡在域名签名关联，不能用游戏内测试或强制 ADB Intent 代替外部点击验收；下一步由 AF 后台负责人更新资产关联并重新测试。
- **未验证／回滚**：当前没有真实首次非自然归因安装，**GCD 的 5 秒分支、UDL/GCD 去重、iOS 运行时与已安装成功唤起仍是 `uncertain`**；构建只能证明编译与 APK 输出，不能证明回调。Unity 本次构建曾自动改写 `Packages/manifest.json` 与 `Packages/packages-lock.json`，两文件开工前无改动；按只读规则不在本任务内继续更改或擅自恢复，应由用户授权后单独处理，其余自动生成的文件亦须清理／复核后再提交。回滚游戏侧时移除 GCD 订阅、候选计时和专用状态，保留已验收 UDL/OneLink 协议与旧资源／存档；已执行的解锁不会自动撤销。

### 2026-09-23 20:07–20:24：AF 增加指纹后的新包复测

- 同日 20:07（Asia/Shanghai）`assetlinks.json` 仍只返回 `78:52:…`；20:07 之后再次请求已返回 `78:52:…` 与新 APK 实签 `C3:A7:…:C1:A0` 两枚指纹。独立 Android 15 模拟器重新请求域名验证后 `brainpunk.onelink.me: verified`；此前失败留存的用户选择 `Disabled` 不代表当前服务器指纹仍错误，实际 `get-app-link-owners` 显示该应用为 VERIFIED。本次没有强制向游戏发送 VIEW Intent。
- **短链已安装冷启动**：从 Google Messages 点击 `https://brainpunk.onelink.me/IjED/gacjwdmm`，ActivityTaskManager 将 VIEW 交给 `com.google.firebase.MessagingUnityPlayerActivity`；经模拟器通知权限拒绝、隐私偏好自行确认、游戏更新公告关闭后，`/tmp/deeplink-gcd-retest-short-logcat.txt` 记录 `url_received → callback FOUND/deferred=False/error=UNEXPECTED → queued Default/level_022 → target_resolved Level_022/sortId=36/fallback=False → entry_accepted`。`/tmp/deeplink-gcd-retest-short-post-popup.png` 显示关卡 Level 36，证明实际资源和画面到达，不能单凭 `entry_accepted` 推断。
- **长链游戏中热唤起**：从第二条 Google Messages 对话点击用户所给 `/IjED?af_xp=custom&pid=DeepLink_test&c=Deeplink&is_retargeting=true&af_reengagement_window=30d&deep_link_value=open_level&deep_link_sub1=default&deep_link_sub2=level_022`；`/tmp/deeplink-gcd-retest-long-logcat.txt` 显示系统 VIEW 再次交给游戏、UDL `FOUND/deferred=False/error=UNEXPECTED` 并排队。用户仍处 Level 36 时不打断；通过游戏 Pause → Menu → 返回首页后，才 `target_resolved Level_022 → entry_accepted`。`/tmp/deeplink-gcd-retest-long-return.png` 显示 Level 36。长链接**含 `c=Deeplink`、`is_retargeting=true`**，不能作为“无 campaign 的新客首次安装”样本。
- **实际 GCD 回调与优先级观察**：只在上述一次性 QA 模拟器卸载测试包（不操作用户原有 AVD），20:19 从 Messages 在**未安装**状态点击该长链，系统转至 Chrome，再跳往 Google Play；Play Store 提示需要登录，因此不能从商店实际安装。随后 20:21 左右侧载**同一** Debug APK 并以普通桌面入口首次打开；日志 `/tmp/deeplink-gcd-fresh-first-open-logcat.txt` 显示 UDL `FOUND/deferred=True` 先于 GCD 排队；Guru 原生 `onConversionDataSuccess` 随后确实返回 `af_status=Non-organic`、`is_first_launch=true`、`media_source=DeepLink_test`、`campaign=Deeplink`、`deep_link_value=open_level`、`sub1=default`、`sub2=level_022`，并显示 `retargeting_conversion_type=re-attribution`。游戏侧没有 `gcd_candidate/gcd_queued`，符合已接受有效 UDL 后跳过 GCD 的优先规则；关闭更新公告后 UDL 路由 `Level_022/sortId=36 → entry_accepted`，`/tmp/deeplink-gcd-fresh-firstopen-final.png` 为画面证据。
- **边界与后续**：本次证明设备上真实 GCD 回调带有所需字段以及 UDL 优先的可见链路；**没有执行 GCD 5 秒后备进关分支**，也不能把同设备重装、侧载及 `re-attribution` 标为 Play 正式安装／真正新客验收。两条链接都不能直接证明“无 campaign GCD 后备”：长链明确带 `c`，短链此轮只验已安装。SDK 回调均为 `FOUND` 同时 `error=UNEXPECTED`，当前无法单凭此日志解释 error 字段原因；如要覆盖 UDL NOT_FOUND → GCD 5 秒兜底，需专门让 AF/QA 提供不会由 UDL 返回目标、但 GCD 首次非自然安装含相同业务字段的受控测试链接、商店安装或明确可复现的安装条件。

### 2026-09-23 20:36–20:40：超过 UDL 点击窗口的补测

- 未再点击新链接。以 20:19:14 的长链点击为起点，20:36:54 后在**同一隔离模拟器**再次侧载、普通启动（距离该次点击已超过 17 分钟）；仅卸载／重装此隔离 AVD 的测试应用，不清理用户原有模拟器。
- `/tmp/deeplink-gcd-late-17min-firstopen-logcat.txt`：UDL `callback status=NOT_FOUND`，原生 GCD `af_status=Organic`、`is_first_launch=true`，没有 `deep_link_value`／`deep_link_sub1`／`deep_link_sub2`，故**没有** `gcd_candidate/gcd_queued` 或 `entry_accepted`；关闭更新公告后 `/tmp/deeplink-gcd-late-17min-default-home.png` 为正常首页。UDL 无结果与 GCD 自然量并不能证明 5 秒 GCD 备用分支工作或失败；缺少可用 GCD 目标时不应该跳转。
- 因此，本轮已完成**系统唤起、已安装 UDL、同设备侧载安装后的 UDL 延迟参数、实际 GCD 数据回调和 UDL 优先去重**的分层观察；**GCD-only 后备跳转、无 campaign 情况、Play 真正安装及 iOS 仍为 `uncertain`**。要覆盖 GCD-only，需要 AF 后台提供一个不由 UDL 回传有效路由、但能完成有效首次非自然归因并在 GCD 返回 `open_level/sub1/sub2` 的专用测试方案；不能复用本轮带 `c=Deeplink` 且 `is_retargeting=true` 的长链冒充该场景。

## 2026-09-24：不带 campaign／再营销参数的原模板长链实测

- 用户指定同一个 `IjED` 模板下的 `af_xp=custom&pid=DeepLink_test&deep_link_value=open_level&deep_link_sub1=default&deep_link_sub2=level_022` 长链；聊天换行处 `d` + `efault` 仅为显示折行，实测 URL 的值是连续的 `default`。没有 `c`、`is_retargeting` 和 `af_reengagement_window`。仍用前次 `26092378.apk`，未重新构建，不写只读 Packages；当日在线 `assetlinks.json` 含此 APK 的 C3 签名。
- **已安装冷启动**：Android 15 的隔离 `QA_20260923` 从 Google Messages 实际点击该长链（非手动强制 VIEW），`pm get-app-links` 为 `brainpunk.onelink.me: verified`；`/tmp/deeplink-20260924-nocampaign-cold-logcat.txt` 记录系统 VIEW→游戏主 Activity、`url_received → UDL FOUND/deferred=False/error=UNEXPECTED → queued → target_resolved Level_022/sortId=36/fallback=False → entry_accepted`。`/tmp/deeplink-20260924-nocampaign-current.png` 显示 Level 36；既有存档的 SDK GCD 为 Organic/`is_first_launch=false`，不能用于证明本次点击的 GCD 安装归因。
- **从未安装过游戏的独立设备上的新安装诊断**：新建 Android 15 隔离 `QA_20260924`（原有 AVD/玩家存档不受影响），确认无目标包；从 Google Messages 点击同一 URL，系统转 Chrome 再到 Google Play。此模拟器的 Play Store 要求登录，无法走真实商店安装；几分钟内侧载相同 APK，普通 Launcher 首次打开，不通过 GCD 调试注入。`/tmp/deeplink-20260924-nocampaign-fresh-firstopen-logcat.txt` 显示 UDL `FOUND/deferred=True/error=UNEXPECTED` 先排队，Guru 原生 GCD 后续返回 **`af_status=Non-organic`、`is_first_launch=true`、`media_source=DeepLink_test`、`deep_link_value=open_level`、`deep_link_sub1=default`、`deep_link_sub2=level_022`、`is_retargeting=false`、`retargeting_conversion_type=none`**；`campaign` 是 SDK 回调中的字面字符串 `"None"`，原 URL 的确没有 `c`，不能将它写成 JSON null。由于 UDL 已有有效目标，游戏侧未出现 `gcd_candidate/gcd_queued`，符合 UDL 优先；关闭公告后进 Level 36，截图 `/tmp/deeplink-20260924-nocampaign-fresh-final.png`。
- **结果边界**：这条由用户指定的链接**确实可以同时触发 UDL 和带业务字段的 GCD 回调**，不需要为了取得 GCD 数据强制另换单平台链接；前两次 UDL 都有效，故没有执行 GCD 后备。侧载模拟器不等于正式 Play 商店安装或线上新客 QA，iOS 未实测；AF `FOUND` 同时有 `error=UNEXPECTED`，本次跳转结果不能说明该 error 字段成因。下节记录在全新设备隔离点击窗口后得到的 GCD-only 结果。

### 2026-09-24 10:45–11:07：同一无 campaign 链接的 GCD-only 后备实测

- **安装与来源**：新增从未安装过目标包的独立 Android 15 `QA_GCD_ONLY_20260924` 模拟器，10:45:44（Asia/Shanghai）在 Google Messages 从外部实际点击相同 URL（无 `c`、无 `is_retargeting`），Chrome 打开并向 Google Play 发起商店 Intent；Play 未登录，无法完成真实商店安装。等待至 11:03:26 后仍确认设备未安装目标包，然后**首次**侧载同一 `26092378.apk` 并从普通 Launcher 打开：距离点击至少 17 分钟。点击和商店跳转证据 `/tmp/deeplink-20260924-gcdonly-click-logcat.txt`，首次启动证据 `/tmp/deeplink-20260924-gcdonly-17m-firstopen-logcat.txt`。
- **UDL 和 GCD 回调**：11:05:47 UDL `status=NOT_FOUND/deferred=False/error=UNEXPECTED`，11:05:54 Guru GCD 原生回调实际返回 `af_status=Non-organic`、`is_first_launch=true`、`media_source=DeepLink_test`、字面 `campaign="None"`、`deep_link_value=open_level`、`deep_link_sub1=default`、`deep_link_sub2=level_022`、`is_retargeting=false`、`retargeting_conversion_type=none`。日志随后出现 `[OneLink] stage=gcd_candidate → queued → gcd_queued reason=udl_without_target`，证明走的是**游戏侧 GCD 备用候选**，不是 UDL 成功假冒的跳转。
- **进关画面**：关闭正式启动更新公告后，日志 `startup_popups_dispatched → target_resolved gameType=Default requested=level_022 path=Level_022 sortId=36 fallback=False → entry_accepted`；截图 `/tmp/deeplink-20260924-gcdonly-17m-final.png` 显示实际 Level 36。由于先收到了 UDL `NOT_FOUND`，GCD 候选到达时立即受理，**没有测试“UDL 不回调且 GCD 候选先到、等待满 5 秒”这条计时上限**。
- **验收边界与回滚**：本轮在相同 Debug APK／模拟器／AppsFlyer 在线状态下，已实测无 campaign 的 GCD-only **备用进关链**，但只证明模拟器侧载的对应分支，不代表正式 Play/App Store 安装、真实 SRN、iOS 或生产发布 QA 通过。未修改运行代码、SDK、Packages、Prefab、存档结构或后台配置；功能回滚仍按本模块“Extended DDL 的 GCD 备用候选”章节移除游戏侧订阅／状态，设备的已写入解锁存档不会自动回滚。回调日志包含 GuruSDK 原始归因字段，分发前需脱敏。

### 2026-09-24 11:22–11:54：原始有 c 链接在全新设备超过 20 分钟后首次安装

- **依据与隔离**：用户要求复测**原始** `IjED` 长链，保留 `pid=DeepLink_test&c=Deeplink&is_retargeting=true&af_reengagement_window=30d&deep_link_value=open_level&deep_link_sub1=default&deep_link_sub2=level_022`。全新、此前从未安装此包的独立 Android 15 `/tmp/deeplink-gcd-avd/QA_C_20260924` 模拟器，于 11:22:46（Asia/Shanghai）在 Google Messages 实际点击 URL，经 Chrome 转发到 Google Play。因 Play Store 未登录，只能侧载；到 11:44:07 仍用 `pm list packages` 确认未安装，随后**首次**安装同一已签名 `26092378.apk`，距实际点击超过 21 分钟。点击／商店跳转证据 `/tmp/deeplink-20260924-c-fresh-click-logcat.txt`，签名及包名未变，不更新 AF 后台和运行代码。
- **首次启动的设备中断及重试**：第一次启动在 AppsFlyer 回调前，11:46:03 `DynamiteLoaderV2Impl` 记录 Google Play Services `cronet_dynamite` 模块配置变化触发进程主动发送 SIGKILL 9，画面回到 Messages；`/tmp/deeplink-20260924-c-first-install-21m-logcat.txt` 不包含 UDL/GCD 回调，故**不能视作归因失败或成功**。同一设备不重装、不再点击，于 11:50 左右从普通 Launcher 重新启动；此后回调中的 `is_first_launch=true`，但本次不属于“毫无启动中断的一次性首次打开”，需与前述无 c 试验区分。
- **第二次启动获得 GCD 备用目标**：`/tmp/deeplink-20260924-c-first-install-retry-logcat.txt`：11:51:50 UDL `NOT_FOUND/deferred=False/error=UNEXPECTED`，11:51:58 GCD `af_status=Non-organic`、`is_first_launch=true`、`media_source=DeepLink_test`、`campaign=Deeplink`、`deep_link_value=open_level`、`deep_link_sub1=default`、`deep_link_sub2=level_022`、`is_retargeting=true`，此次 `retargeting_conversion_type=none`、`match_type=probabilistic`、`af_click_lookback=7d`。游戏日志 `gcd_candidate → queued Default/level_022 → gcd_queued reason=udl_without_target`，是 GCD 接管而非 UDL FOUND。关闭更新公告后 `target_resolved path=Level_022 sortId=36 fallback=False → entry_accepted`；实际画面截图 `/tmp/deeplink-20260924-c-fallback-final.png` 为 **Level 36**。
- **比较结论／缺口**：**有 c 的原始链接**也能在该全新设备、先点击后超过 20 分钟才首次侧载的条件下通过 Android GCD 兜底进关；先前同设备已归因安装后未再点击的重装返回 Organic，不支持“有 c 必然使 GCD 失败”的结论。仍不能仅凭两次不同设备实测精确断言旧 Organic 的 AF 服务端原因，也不能宣称 `c` 是惟一变化：此链接另外带再营销参数，前次新链不带。由于 UDL 先于有效 GCD 明确返回 NOT_FOUND，本次也**没有覆盖候选先到且 UDL 不回调时等满 5 秒**；侧载、Cronet 中断、真实 Play 安装、iOS 与 SRN 均需按各自范围标记。此轮只有测试产物及文档变更，回滚功能沿用本模块上节，不触碰 SDK、只读 Packages 或既有玩家存档。

## 2026-09-24：GM 调试包验证 GCD 先到、UDL 无回调的 5 秒分支（历史验证，当前分支不包含探针）

- **背景／已确认边界**：先前两次侧载的真实 AF GCD 备用进关均是 UDL 先返回 `NOT_FOUND`，理由为 `udl_without_target`；没有测到 GCD 候选先到而 UDL 始终不回调的 `udl_wait_5s_elapsed`。用户明确允许为此在游戏侧增加仅调试构建可用的测试入口，并一次性允许 Unity 构建处理 `Packages/manifest.json`／`packages-lock.json` 两个清单；不修改其它 Packages、GF 或 SDK 源码。历史已安装短链、真实归因与 GCD-only 实测仍以本文件前述章节为准，**下述测试是注入模拟数据**，不能混写成 AF 服务端真实静默。
- **改动与调用链**：`OneLinkManager.cs` 仅在 `UNITY_EDITOR || (DEBUG_MODE && DEVELOPMENT_BUILD)` 编入 `TestGcdTimeout()`，从正常首页 GM 注入固定 `Non-organic/is_first_launch=true/open_level/default/Level_022`，游戏侧暂时不处理晚到 UDL／真实 GCD 回调；共用正式 GCD 参数校验、候选计时、首页门禁及 `OneLinkLevelRouter`，无新资源加载或存档格式。`GMPanel.cs` 复用原序列化 OneLink 按钮：Popup 已打开时**再次点击同一个入口**开始测试并关闭 Popup；`GMOneLinkTestPopup.cs` 只在受控构建追加说明文本。原参数／URL 测试的第一次点击仍是打开 Popup，不改 Prefab 或 Generated 绑定；真实系统 URL 到来会取消当前模拟，测试尝试进关后恢复处理真实 UDL。可通过关掉 GM 面板遮挡观看实际关卡。用户设备的正常进度会因直达 Level 36 而受原路由解锁写入；测试仅用隔离模拟器存档。
- **红灯基线**：在旧 `26092378.apk` 上，打开 GM OneLink Popup 后第二次点击 OneLink 测试只得到 `UIManager OpenUI aborted: UI GMOneLinkTestPopup is already on top of layer Popup`，没有 `gcd_candidate/gcd_queued` 或进关；证据 `/tmp/deeplink-gcd-probe-red-logcat.txt`、`/tmp/deeplink-gcd-probe-red-second-click.png`。没有为了红灯去点击现有参数测试、修改玩法存档或尝试伪造真实 AF 回调。
- **Android 绿灯**：Unity 6000.0.59f2 `BuildToolV2.BuildAndroidDebug` 输出 `BuildOutput/Android/Tricky_Box_DEBUG_1.16.0_26092450.apk`，versionCode `26092450`，实签 SHA-256 `C3:A7:8E:D0:DF:42:C7:C5:8A:F1:C3:70:20:F4:A9:E2:CE:88:E5:FF:38:9C:BC:20:EA:D6:38:35:44:A0:C1:A0`；同一独立 Android 15 AVD 覆盖安装（保留其存档）并从桌面正常启动，GM 第二次点击开启模拟。`/tmp/deeplink-gcd-probe-green-scenario-logcat.txt` 显示 `13:00:51.635 gcd_candidate → 13:00:56.705 gcd_queued reason=udl_wait_5s_elapsed`，测试日志 `elapsedSeconds=5.07`；随后 `13:00:56.717 target_resolved Level_022/sortId=36/fallback=False → 13:00:57.043 entry_accepted → gcd_probe_finished`。首秒时已取屏且尚无 `gcd_queued`；关闭 GM 叠层后 `/tmp/deeplink-gcd-probe-green-level36.png` 显示实际 Level 36。入口使用模拟 GCD，**没有把该日志当成新一次真实归因**。
- **新增真实链接回归**：同一 `26092450` 调试包完成模拟进关后，在 Level 36 游戏中由 Google Messages 点击现有 `/IjED/gacjwdmm` 短链；设备域名为 `verified`，`/tmp/deeplink-gcd-probe-udl-regression-logcat.txt` 显示系统 VIEW 交游戏、UDL `FOUND/deferred=False`、`queued level_022`，游戏中未打断；通过 Pause → Menu → 返回首页后 `target_resolved Level_022/sortId=36 → entry_accepted`，`/tmp/deeplink-gcd-probe-udl-final.png` 显示实际 Level 36。此项验证探针结束后普通**已安装 UDL**仍能唤起及进关，但不是模拟候选先到第 2 秒 UDL 抢占的竞态测试；UDL 回调的 `error=UNEXPECTED` 原因仍未由此确定。
- **构建与剩余风险**：`/tmp/deeplink-gcd-probe-build-20260924.log` 最终产出 APK／`Guru Build End`，但 AppleGameCenter 预处理器仍报缺少 `link.xml`，未编辑 SDK 处理；两份允许写入的 Packages 清单构建前后 SHA-256 未变，其历史脏状态仍须与本任务分开审查。`DEVELOPMENT_BUILD` 来自构建器 Debug 分支 `BuildOptions.Development`，正式版测试代码有编译宏隔离；**尚未单独构建 Release APK 验证剔除情况**。未测试“GCD 先到后第 2 秒 UDL 有效目标覆盖”具体竞态、iOS、Play 分发、应用原生覆盖层暂停 PlayerLoop 后计时偏移；这些场景标 `uncertain`，不可据此宣称全部 Extended DDL 已验收。
- **回滚**：删除 `TestGcdTimeout`、探针字段及游戏侧仅探针宏保护的 UDL/GCD 忽略与日志分支；GM 第二次点击恢复普通 `GMOneLinkTestPopup.Create()` 行为，删去 Popup 探针提示。保留正式 GCD/UDL 仲裁、原 OneLink 调试参数／URL 路径，不回退用户以前确认的关卡协议或实际解锁存档；独立 QA AVD 可另行卸载。
- **分支范围修订（2026-09-24）**：用户确认该 GM 注入入口仅是历史测试工具，不应随 `feature/deeplink_simon` 合并到 `feature/deeplink`。当前分支已删除 `TestGcdTimeout`、专用字段／拦截／探针日志、`GMPanel` 的二次点击分支及 Popup 提示；GM 原参数／URL 测试不变。上方记录的旧 APK 及日志仍是当时的模拟验证证据，**不能用来证明当前分支已构建或已在设备上重验五秒路径**；正式 Extended DDL GCD 候选、UDL 仲裁和五秒等待仍保留。撤回本次范围修订只需另行恢复测试专用入口，不需要改变正式 GCD 算法。

## 2026-09-24：原始带 c 长链接逐关改参批量路由测试

- **需求与基线**：用户要求三个玩法的**每一个**可直达关卡都用原 `IjED` 长链接测试，不接受只抽样，也不改本地关卡参数。沿用 `af_xp=custom`、`pid=DeepLink_test`、`c=Deeplink`、`is_retargeting=true`、`af_reengagement_window=30d`、`deep_link_value=open_level`，每条仅修改 `deep_link_sub1`／`deep_link_sub2`；批量脚本断言其它 query 键值不变。仍用 `BuildOutput/Android/Tricky_Box_DEBUG_1.16.0_26092378.apk`（versionCode `26092378`）；独立 `/tmp/deeplink-batch-qa-20260924/QA_BATCH_20260924.avd` 安装，不读写用户原有模拟器的游戏存档，也不修改项目关卡、路由、SDK 或 Packages。Android 15 `pm get-app-links` 对本 APK 签名 C3 的 `brainpunk.onelink.me` 为 `verified`。这是**已安装包**每次经 ADB 发送外部 VIEW Intent 的长链测试，**不是** Google Messages 人工点击、首次安装 GCD、商店发行或玩法通关测试；含 `is_retargeting=true` 不得拿来声称首次自然／非自然安装归因。
- **测试入口与断言**：仅在独立模拟器内每次 `am force-stop` 应用，然后用已对整个 URL 作 shell 引号保护的 `adb shell am start -a android.intent.action.VIEW -c android.intent.category.BROWSABLE -d <改参长链>` 启动；早期试探因未正确转义 `&` 导致 SDK 无目标，修正引号后才开始计入批量结果。对每个目标分别验证 `[OneLink] callback FOUND → queued → target_resolved` 的 `requested/path/sortId/index/fallback=False`、`entry_accepted` 及 `[EVENT] level_start` 的相同 Path／玩法，并在逐项检查后存屏；逐关结果与复测历史在 `/tmp/deeplink-batch-qa-20260924/batch-results.jsonl`，最终各目标截图在同目录 `screenshots/`。日志确认 323 个常规目标都走 UDL 回调，本批次没有把 `entry_accepted` 单独算资源加载成功。
- **常规排序目标结果**：本地配置逐项枚举主线 149、Draw 99、IWanner 15 组共 75 小关，**323 个目标全部分别提交 URL**。主线 148／149、Draw 99／99、IWanner 75／75 在自动等待期取得各自 Path 的 `level_start`，三玩法的配置排序／组内索引与日志逐项相符。`Level_001` 的链接得到 `target_resolved Level_001/fallback=False → entry_accepted`，但先停在新手漫画开场页；不能仅按通用超时当成功。单独重测后点该页 `Play`，观察到 `[EVENT] level_start level_name=Level_001`，`/tmp/deeplink-batch-qa-20260924/main-001-after-intro.png` 为 Level 1 教学弹窗画面，因此该关在**需额外用户操作**条件下进关。主线 `Level_092/033/034` 和 IWanner `ILevel_032` 首轮分别因模拟器未空闲、较慢启动或工具异常未满足断言，均独立复测后取得正确 Path 的 `level_start`；不得将首轮异常静默删除。所有常规目标最终各存一张截图，但除代表关和异常目标外**未逐张人工验收画面或游戏机制**，其余逐关结论限定为日志可证明的进关和存图，非 323 关玩法通关验收。
- **额外可直达／不可直达资源**：同链单测 `main/Level_999` → `Level_999/sortId=150/fallback=False/level_start`，`draw/DLevel_999` → `DLevel_999/sortId=100/fallback=False/level_start`，`iwanner/ILevel_999` → 特殊 TBN 入口 `ILevel_999/fallback=False/level_start`，各有截图。两项 `999` 在本地 `tblevelsort.json`／`tbdrawlevelsort.json` 中不在上述 149／99 项，但**本次运行时生效的排序**包含它们；不可把本地 JSON 行数当运行时可达总数。`tbdrawleveldata.json` 另有 `DLevel_100`，但本次 `draw/DLevel_100` 实际 `target_resolved path=DLevel_001/sortId=1/fallback=True`、`level_start DLevel_001`，**未直达 DLevel_100**；本地 Draw 排序也不含它，未发现 `DLevel_100.prefab`，不能报告为通过。额外目标证据在 `batch-results-special.jsonl`；本轮 323 常规 + 3 个可直达 `999` 为 **326 个成功直达目标**，另有 1 个不支持直接跳转的 `DLevel_100` 负例。
- **指纹、版本及风险／回滚**：2026-09-24 13:38（Asia/Shanghai）在线 `assetlinks.json` HTTP 200 仍为同包名同时列 78 与 C3 两枚 SHA-256，`apksigner verify --print-certs` 核对本 APK和**期间另一任务新增**的 `26092450.apk` 都实签 `C3:A7:8E:D0:DF:42:C7:C5:8A:F1:C3:70:20:F4:A9:E2:CE:88:E5:FF:38:9C:BC:20:EA:D6:38:35:44:A0:C1:A0`；因此对这两枚测试包的 Android 域名签名**无需更新**，正式 Play 包的签名仍 `uncertain`。本批次逐关安装并运行的是 `26092378`，**没有对新出的 `26092450` 重跑全部 326 关**；不得将结果冒充新包的全量验收。测试包本身的开发者控制台出现 Ads/材质等红字，未据此归因 OneLink；批量进关持续写入独立 QA AVD 的解锁／运行存档，不回滚正式玩家数据。需要撤销本轮测试时只处理上述 `/tmp` 测试产物和独立 QA AVD；本轮没有业务代码变更可回滚，原 OneLink 功能回滚仍见本文件前文。

## 2026-09-24：Android／iOS OneLink URI 备用唤起配置

- **需求及基线**：用户最初确认两端统一使用 `uribrainpunk://` 作为现有 `https://brainpunk.onelink.me/IjED` 的备用唤起，后续明确追加第二条独立链路：外部 `uribrainpunk://link?deep_link_value=open_level&deep_link_sub1=<玩法>&deep_link_sub2=<可选资源Path>` 直接传入动态业务参数，并授权更新本节与构建新 Android APK 验证两条链路；测试例子的 `default/Level_022` 不得写死在逻辑中。两种入口复用原有 `OneLinkRequest` 的 `open_level`、`main/default/draw/iwanner` 与可选资源 Path、首页门禁和关卡路由；其他 action 不由本次新增，URI 路径不推导关卡。原 HTTPS 及 OneLink 的无参数备用 URI 保持现有 UDL 行为。修改前 Android 主 Activity 只有两个独立 HTTPS 过滤器，iOS `ProjectSettings.asset` 的 `iOSURLSchemes` 为空；当前旧 Android APK 均无新 scheme。
- **配置及流向**：在 `Assets/Plugins/Android/AndroidManifest.xml` 同一接收 Activity 新增独立 VIEW／DEFAULT／BROWSABLE 的 `uribrainpunk` scheme 过滤器，不加 `autoVerify`，不修改原 `brain.gurugame.ai/default_link` 和 `brainpunk.onelink.me/IjED` HTTPS 过滤器；`ProjectSettings/ProjectSettings.asset` 的 `iOSURLSchemes` 添加不带 `://` 的 `uribrainpunk`。用户已确认 `IjED` 模板后台保存 `uribrainpunk://`；这只是后台操作事实，仍需真实点击验证生效。无参数备用 URI 只拉起 App，业务目标依旧经 OneLink UDL；独立 URI 则从 query 提取原有白名单字段、通过同一 `OneLinkRequest` 受理后等首页门禁。`link` 是固定入口标识而非目标；只接受该 host，不处理其他 URI host/path 或以任意 URI 当资源地址。不得改 GuruSDK、关卡资源、存档和埋点。
- **静态检查与验收矩阵**：XML 可解析、备用过滤器与两个 HTTPS 过滤器分离、iOS scheme 名称一致，均经静态断言通过；构建及 Android 模拟器验证结果见下节。验收仍应分别覆盖 OneLink HTTPS、真实浏览器／短信在 HTTPS 未拉起时的自动 URI 备用、独立 URI 多玩法／缺 sub2／非法参数／关内热唤起，以及 iOS 冷／热启动。不能以直发 scheme 或正常 HTTPS 成功冒充 OneLink 备用链路。无参数备用 URI 且 UDL 未返回有效目标时不从 URI 跳关，但 `HasPendingRequest` 在 URI 等待期间为真，冷启动可能跳过 ChestGift；还需观察 UDL 先到、URI 系统事件后到时现有待处理请求是否被覆盖。
- **回滚**：移除 Android 新增的独立 scheme 过滤器及 iOS `iOSURLSchemes` 新条目，并由后台负责人移除 `IjED` 的备用 URI 设置；保留两个原 HTTPS 入口及原 UDL 路由。已生成安装包需重新构建、安装验证，旧包不随源配置回滚自动改变。

### 2026-09-24：新 Android Debug APK 与两条链路实测

- **构建及安全边界**：Unity 6000.0.59f2 的 `BuildToolV2.BuildAndroidDebug` 产出 `BuildOutput/Android/Tricky_Box_DEBUG_1.16.0_26092457.apk`（包名 `com.brain.tricky.puzzle.games.test.quest`、versionCode `26092457`、APK SHA-256 `578157d1d9966afb751a05502a552f74dd44a5a7d5a61294e2c66b77f4bf5401`）；最终 APK Manifest 中独立声明 `uribrainpunk` 和原 `brainpunk.onelink.me/IjED` HTTPS 过滤器。`apksigner` 实签证书 SHA-256 为 `C3:A7:8E:D0:DF:42:C7:C5:8A:F1:C3:70:20:F4:A9:E2:CE:88:E5:FF:38:9C:BC:20:EA:D6:38:35:44:A0:C1:A0`；构建时沙箱禁止写 `Packages`、`Assets/GF`、`~/.guru` 和项目 `Library/PackageCache`，两份 Packages 清单 SHA 前后分别保持 `84884bd07245de793f1714682e49b22c4aa642772f4c235734240e67b3ac9a0c`、`89fcabed90d45cb7ca47b2eeee43adbe62fc5f8286282988e9c7e87ede5d6228`。Unity 自动改写的无关字体、资源清单、gradle、build_info 和 PlayerSettings versionCode 已按构建前干净基线恢复；保留已产出的 APK 和本轮 URI 设置。构建日志 `/tmp/deeplink-uri-build-20260924.log`。
- **设备隔离事故（不得省略）**：先用已有 `Pixel_8_Pro` 名称附加 `-datadir /tmp/brainpunk-uri-qa-20260924/datadir` 启动，发现该选项**没有**将可写数据隔离到空的临时目录，原 `~/.android/avd/Pixel_8_Pro.avd/userdata-qemu.img.qcow2` 时间戳变为 2026-09-24 14:33:02。对原 AVD 安装新包报 `INSTALL_FAILED_UPDATE_INCOMPATIBLE`，未替换旧包；立刻关闭，未卸载或清数据，也未擅自回滚镜像。原模拟器用户数据是否存在其它改变为 `uncertain`，应由所有者核对。之后仅使用新建的独立 AVD `QA_URI_20260924`，其配置及磁盘都位于 `/tmp/brainpunk-uri-qa-20260924/avds/`，先确认未安装游戏再安装新包；原镜像时间戳此后没有再变化。验收结束关闭该 QA 模拟器，不动用户原 AVD。
- **独立 URI 直达（通过于独立 Android 15 QA AVD）**：外部隐式 VIEW `uribrainpunk://link?deep_link_value=open_level&deep_link_sub1=iwanner&deep_link_sub2=ILevel_002` 冷启动，日志 `url_received → auto_url_parsed queued=True → target_resolved ILevel_002/fallback=False → entry_accepted → level_start ILevel_002`，SDK 较晚 UDL `FOUND` 被 `duplicate_ignored`；见 `/tmp/brainpunk-uri-qa-20260924/direct-iwanner-cold-logcat.txt`、`direct-iwanner-cold.png`。`draw/DLevel_002` 的外部 VIEW 则由 SDK UDL `FOUND` 先排队并得到 `level_start DLevel_002`、画面 Level 6；见 `direct-draw-after-permission-logcat.txt`、`after-draw-permission.png`，此样本**不能**单独证明本地解析分支。关内热唤起另一个 `iwanner/ILevel_002` 后排队且未立刻打断 Draw；未对该次热唤起做“返回首页最终进关”的完整验收。省略 `deep_link_sub2` 的 `draw` URI 解析得到 `DLevel_001/fallback=True` 与 `level_start`；非法 `deep_link_value=open_event` 两次被 `unsupported_action` 拒绝，无进关日志。裸 `uribrainpunk://` 可拉起包，但无有效业务目标时拒绝进入关卡，不能把裸 URI 当作成功选关。
- **OneLink HTTPS 主路径（通过，限 ADB 隐式 VIEW）**：当日在线 `assetlinks.json` 为 200 且包名和 APK C3 指纹匹配；QA AVD `pm get-app-links` 对 `brainpunk.onelink.me` 为 `verified`。外部 `https://brainpunk.onelink.me/IjED?...deep_link_value=open_level&deep_link_sub1=default&deep_link_sub2=Level_022` 被系统交给新包，UDL `FOUND → queued → target_resolved Level_022/sortId=36/fallback=False → entry_accepted → level_start Level_022`，画面 Level 36；证据 `onelink-https-logcat.txt`、`onelink-https.png`。这是 **ADB 隐式 VIEW**，不能称为真实短信点击或 URI 备用成功。
- **OneLink 自动 URI 备用路径（14:54 初测未通过；15:31 更新后补验见下文）**：在**独立 QA AVD** 暂时将该包域名验证设为 `none`，分别试长链接的外部隐式 VIEW 以及向 QA 模拟器本机模拟注入短信后**在 Messages 中点击**既有 `/IjED/gacjwdmm` 短链接。两次均进入 Chrome，再由 `market://details` 跳 Google Play；当时没有观察到系统以 `uribrainpunk://` 打开游戏或 UDL 进关。证据 `onelink-fallback-attempt-logcat.txt`、`onelink-sms-fallback-logcat.txt`、`onelink-sms-fallback.png`。**当时**尚未观察到有效 `af_dp`，因此当时不能宣称备用链路已验收；同日 15:31 后台链接状态变化后的独立 QA 复测结果另见下文，勿沿用本条旧结论。
- **其余风险与回滚**：iOS 仅在项目 PlayerSettings 配置 scheme，尚无导出 Xcode/IPA、真机 URI／Universal Link 和 UDL 实测，均 `uncertain`。`entry_accepted` 本身不是资源完成的证据，因此以上成功项同时检查 `level_start` 和代表性画面；未全量测试所有 Path。应用源回滚除了撤去上节两端平台配置，还需删去 `OneLinkManager.ResolvePendingUrl()` 本轮 `isDirectUri` 白名单分支，后台负责人取消模板备用 URI；不要修改已有 HTTPS 或 SDK，也不要试图回滚原 AVD 镜像来掩盖本次隔离事故。

### 2026-09-24 15:15：同设备对照定位旧短链备用参数

- **用户质疑与检查方法**：用户指出“未唤起”也可能是游戏侧配置或现有链接参数缺失，不能先把原因归于浏览器。复用**同一个独立 QA AVD 和同一个 `26092457` APK**，该 AVD 暂将域名验证设为 `none`，从 Messages 真正点击：A 为原 `/IjED/gacjwdmm`，B 仅在 A URL 后附加 `?af_dp=uribrainpunk%3A%2F%2F` 的**诊断链接**。其余短码、后台业务目标和 APK 不变；B 不等于原链接已修复或后台配置已生效。
- **原短链 A（再次失败）**：Android UA 对原 URL 的 HTTP 响应是 301 → `market://details`，无 URI scheme；短信点击后的 `/tmp/brainpunk-uri-qa-20260924/diagnostic-original-retest-logcat.txt` 在 15:15:37 仍记录 Chrome 打开 `market://details`，最终停留 Google Play，未触发 App Intent。`deep_link_value`、`deep_link_sub1`、`deep_link_sub2` 已在返回的 referrer 字段中出现，因此不能说是**关卡业务参数缺失**；当前证据更具体地指向**原短链在此环境缺少有效 URI 备用目标**。
- **带 `af_dp` 的 B（诊断成功）**：同 Android UA 的响应变为含脚本的 HTTP 200 页面，其中 `app_link` 是 `uribrainpunk://?af_deeplink=true&af_dp=...&deep_link_value=open_level&deep_link_sub1=default&deep_link_sub2=level_022...`，不是 `uribrainpunk://link?...`；短信真实点击后 Chrome 发出指向游戏包的 `uribrainpunk` VIEW Intent，SDK UDL `FOUND → queued Default/level_022 → target_resolved Level_022/sortId=36/fallback=False → entry_accepted → level_start Level_022`，屏幕实际 Level 36。日志 `/tmp/brainpunk-uri-qa-20260924/diagnostic-af-dp-sms-logcat.txt`，截图 `diagnostic-af-dp-sms.png`。这说明 **Android 包体和 manifest 能接收 AF 生成的 URI，SDK 在该次能恢复业务参数**；但成功链接带新增 `af_dp`，不能把原链接或其它浏览器／iOS 认定为已通过。
- **责任边界与待办**：原短链的有效 `af_dp` 未被观察到，附加后形成控制变量使备用链路成功。用户随后给出的 AF **链接编辑页**截图中，“Android 和 iOS 备选方案 → 更改 URI scheme 以调起应用”的输入框为空；提示说明此处对应链接级 `af_dp`，填写可覆盖模板默认值。因此空框**单独不足以证明错误**（模板默认值可能生效），但结合原短链返回 `market://` 和诊断链接带 `af_dp` 后成功，可确认当前实际测试的旧短链在该设备／时点没有生效的 URI 备用地址。建议在该链接级字段填写 `uribrainpunk://`，完成链接保存／更新并验证最终产出的链接；旧短链是否自动继承链接或模板改动仍需由 AF 后台核对，不能当作事实。另一个独立的游戏侧缺口是 `OneLinkManager.ResolvePendingUrl()` 目前只允许 `uribrainpunk://link?...` 本地解析，而 AF 在 B 中构造的 host 为空的 `uribrainpunk://?...` 不匹配：本次靠 UDL FOUND 进入关卡，故**该缺口不是本次未唤起的原因**；若 SDK 没有返回有效 UDL，AF 生成的 URI 虽已唤起包，游戏仍不能凭 URI 本地解析关卡。这个行为与此前“无有效 UDL 的裸备用 URI 留在首页”的约定有关；若要扩展白名单解析，须先确认需求并更新本节，再实现／重构建包；本轮只诊断、不修改代码或重发 APK。QA 验证状态恢复为 `verified` 后关闭，原有用户 AVD 未再次启动。

### 2026-09-24 15:31：原短链后台参数生效后，Android 备用唤起补验

- **后端响应核对**：用户在 AF 链接级编辑页增加 `af_force_deeplink=true` 后，再次只读请求原始、**不附加任何诊断 query** 的 `https://brainpunk.onelink.me/IjED/gacjwdmm`。Android 与 iPhone UA 均返回 HTTP 200 的跳转页面，`app_link` 均为 `uribrainpunk://?...`，其中 `af_dp=uribrainpunk://`、`af_force_deeplink=true` 和 `deep_link_value=open_level/deep_link_sub1=default/deep_link_sub2=level_022` 均已出现。UA 结果只能证明当时 OneLink 服务端生成了这些字段，**不等于 iOS 已安装包真机能够唤起**。本次后台生效的 `af_dp` 与 `af_force_deeplink` 同时出现，不能只把成功归因于其中一项。
- **同一 Android 15 独立 QA AVD 补验（通过）**：继续使用已安装 Debug APK `26092457` 的 `/tmp/brainpunk-uri-qa-20260924/avds/QA_URI_20260924`，只在该 AVD 将 App Links 域名验证暂置为 `none`，从 Google Messages **实际点击未追加 `af_dp` 的原短链**。`/tmp/brainpunk-uri-qa-20260924/final-original-short-uri-fallback-logcat.txt` 于 15:31:19 记录 Chrome 发出面向游戏主 Activity 的 `uribrainpunk` VIEW Intent；游戏 `url_received → UDL FOUND → queued Default/level_022 → target_resolved path=Level_022/sortId=36/fallback=False → level_start Level_022 → entry_accepted`；`final-original-short-uri-fallback-settled.png` 显示 Level 36。此前原短链在同类型 QA 条件下跳 Play、附加 `af_dp` 诊断链接成功；此补验确认**原短链目前在该 QA Android 条件下也能走 URI 备用并进关**，但不能推出所有浏览器、首次安装、Play 正式包或 iOS 也通过。
- **收尾及剩余边界**：QA 域名状态最后重新验证为 `brainpunk.onelink.me: verified`，随后关闭 QA 模拟器；原用户 AVD 未重新打开，其此前镜像时间戳变化的影响仍 `uncertain`。本次没有编辑游戏代码、SDK、Packages 或重新打包。`uribrainpunk://?...` 在游戏本地解析路径中仍因 host 为空而不匹配 `link`；此次有有效 UDL 所以不影响实际进关，UDL 静默的备用情况仍需先经需求确认是否允许扩大解析白名单，再实现和验证。iOS 仅有 User-Agent 响应证据，真机／IPA 与浏览器矩阵未验。

## 2026-09-24：补齐正式回调与 URL 参数日志（整合远端）

### 问题、目标与范围

- 远端收到的 `BP-YYGFX_debug_log(12).zip` 为 iPhone、当时应用版本 1.16.0：10:49:37 SDK `FOUND` 后游戏拒绝 `unsupported_action`，10:49:41 首页 URL 兜底拒绝 `invalid_http_url`。旧日志没有协议字段或 Unity 收到的地址类型，不能据此断言后台缺参、click_event 选取错误或 URI scheme 有问题。
- 原详细参数日志只覆盖 Android 主动短链成功分支，正式 SDK 自动回调缺少实际字段。远端仅为游戏侧 `OneLinkManager.cs` 增加 `GuruLog` 诊断，不新增 Analytics 事件、不改变 `OneLinkRequest` 的业务参数选择及有效性校验；不修改 SDK/Packages、GF、存档、Loading 或平台后台。

### 日志与隐私边界

- 历史独立版本的诊断标记为 `onelink-v2`；本次整合后标记为 **`diagnostics=onelink-v4`**。`stage=parameters` 记录 root 与 click_event 的 `deep_link_value/sub1/sub2`、click_event 类型及被选用的来源；区分 Missing、null、空白、数字、对象或数组，标量诊断截断至 256 字符。自动 UDL 在解析／排队前以 `sdk_callback` 记录；GM 参数、GM URL query、首页 URL query 各有来源标记，缺参也输出可读的局部信息。
- `stage=url_details` 仅记录 Unity／GM／SDK 的地址长度、scheme、host、path、是否 HTTP(S) 与白名单三参数；重复 query 参数逐项记载。不输出原始 SDK 归因对象、URL 凭据、任意 query 或 fragment。诊断抛异常只记 `diagnostics_failed`，不阻断深链受理。`status=ERROR` 才记录 SDK error；FOUND/NOT_FOUND 中的默认 `UNEXPECTED` 不作为失败判据。
- 排障顺序：确认 v4 标记 → `url_details` → `callback`／`parameters` → `rejected` 或 `queued` → `waiting`／`entry_accepted` 与实际画面；诊断日志不能证明旧 iOS 问题已修复。

### 远端独立版本验证与回滚

- 远端独立版本通过 TEMP harness：iOS 88、Editor 111、Android DEBUG_MODE 170、Android 无 DEBUG_MODE 113 项；Roslyn 定向编译 Editor 0 错误／105 警告，Android 0 错误／108 警告。证据在 `C:/Users/admin/AppData/Local/Temp/brainpunk-onelink-diagnostics-20260924/`；模拟 SDK 不等于 iPhone 原生 SDK／真机，以上**不是本次合并版**的通过结果。
- 回滚本节只撤销 Manager 中诊断方法、日志调用与标记；不撤回此前 UDL／GCD／URI 功能或历史测试证据。真实 iOS 字段与旧日志失败根因仍 `uncertain`。

## 2026-09-24：ERROR 后重试与首次安装补偿（整合远端）

### 原行为与最终要求

- 旧版 UDL 的 SDK ERROR 只记录，不主动补拉；远端用户将初版三次、非固定总预算收紧为**从首次正式 ERROR 起最多追加两次、十秒总预算**。GM 手动测试仍是原十秒等待、不自动重试。游戏仍只在既有 StartPanel 首页门禁放行时发起主动重交，不新增 Loading 锁或更改玩法、资源与存档。
- 每次重试最多等待五秒且不超过总预算；第二次最早在首次 ERROR 后第五秒。离线、首页弹窗、失焦、关卡中等待均计入十秒；无网检查不耗重试次数。成功受理、收到新系统 URL 或会话清理均结束旧等待。十秒过后本次 ERROR 会话的晚到 UDL 不再进关；预算结束不代表取消 SDK 原生网络请求。

### URL、iOS 与 GCD 仲裁

- Android 复用已有 `performOnDeepLinking`，iOS **仅在正式 ERROR、有真实 URL 时**经公开 `AppsFlyer.handleOpenUrl` 重交；不重复 startSDK。每次携带独立 sub10 标识，旧标记结果忽略；无标记 ERROR 不提前结束仍在执行的原自动请求。首个 ERROR 启动预算，即便此时原自动请求仍在等待，也不把预算向后顺延。NOT_FOUND、无效 FOUND 与原生异常不触发下一轮；合法 HTTP(S) 长链可沿原本地 query 解析。
- iOS 首装可能没有 URL：**只在已有正式 ERROR 且十秒预算内**，从单一 Guru 转化数据订阅补收 `is_first_launch=true`、`af_status=Non-organic`、协议有效的转化数据；先到可从当前 SDK 状态读取，后到等回调。不读取旧存档或伪造无 URL 重发。不保证 SDK 断网恢复会补回归因，不能将它当作真机已通过。
- 本地既有 GCD 是另一条独立的首次非自然安装路径：有效 GCD 候选在 UDL 无目标时接管，若 UDL 未决最多等候五秒；与远端十秒 ERROR 重试并存。**ERROR 本身不是最终无目标**：已有候选维持其五秒计时，重试成功优先使用 UDL；GCD 先受理则终止游戏侧重试等待；iOS 的无 URL／ERROR 补偿复用同一条转化数据订阅，避免两个订阅或二次进关。无效／迟到候选不推断关卡，已尝试 GCD 后的同次 deferred UDL 不再进关。正式真机竞态仍需验证。
- `retry_scheduled/retry_waiting/retry_started/retry_stopped` 与 `auto_url_finished` 记录预算和终止原因；iOS 补偿用 `source=install_recovery`／`install_recovery_queued`；GCD 维持 `gcd_candidate/gcd_queued`。检查 `target_resolved` 和实际画面，不能只凭 `entry_accepted` 宣称资源加载成功。

### 验证、风险与回滚

- 远端独立 v4 的 TEMP harness：iOS 150、Editor 113、Android DEBUG_MODE 220、Android 无 DEBUG_MODE 159 项；Editor/Android Roslyn 定向编译 0 错误，iOS SDK 接口定向编译 0 错误／4 个同名类型警告。完整 iOS 符号编译遇到项目 GameKit 命名空间引用缺失；证据在 `C:/Users/admin/AppData/Local/Temp/brainpunk-onelink-retry-20260924/`。这些结果和上方旧 APK 的设备实测**均不能冒充本次整合版的编译或设备验收**。
- `uncertain`：合并后 Android／iOS 冷热启动、重试 sub10 回传、iOS 无 URL 安装补偿、GCD 与 ERROR 同时到达、Play／App Store 分发和 iOS Scheme 的真实设备表现；新 APK/IPA 尚未由本次合并构建。旧 iPhone 是 FOUND 后参数拒绝，不会被 ERROR 重试直接修复。不同点击的无标记自动 UDL 回调归属仍可能不明确。
- 回滚远端 v4 须同时撤销十秒总预算、两次上限、晚到拦截、iOS 重交／补偿和日志；不能只改次数。保留本地 GCD／URI 原有回滚路径；关卡解锁和独立 QA 存档不会随代码回滚自动撤销。

## 2026-09-24：上游／本地整合

- 用户选择 `ProjectSettings.asset` 版本号 **1.15.1**，同时保留 `iOSURLSchemes: uribrainpunk`；`AndroidManifest.xml` 原两个 HTTPS intent-filter 与本地 `uribrainpunk` intent-filter 一并保留。`OneLinkManager.cs` 汇合正式 UDL／URI／Extended DDL GCD、v4 诊断与 ERROR 重试；GM 五秒测试探针已从目标分支撤出，SDK／GF／Packages 内容未因本次整合编辑。
- `DevelopMD/已有功能设计与使用记录/2026-09-24.md` 保留本地 Android／URI 实测与远端 iOS 诊断／重试两组记录。远端风险报告保持原文，未将旧测试产物视为本次测试。当前验证结果以本节后续实际执行的命令为准；未运行的新包和真机验证均为 `uncertain`。
- **本次验证**：只在 `/tmp` 生成 Roslyn 响应文件、路由替身及输出，直接编译合并后的 `OneLinkManager.cs`＋`OneLinkRequest.cs`，分别以 Editor、Android DEBUG_MODE、Android 无调试宏、iOS 符号编译，四组均无编译错误；路由使用 TEMP 替身，因此不代表完整 Unity 工程编译。`git diff --check` 对本次代码／配置／文档通过；XML 解析和静态断言检查 Android HTTPS 与 URI 过滤器、iOS Scheme、1.15.1、单一转换数据订阅、GCD／重试入口及双文档主题均通过。`dotnet build --no-restore` 尝试被 `/tmp` 缺少 `project.assets.json` 阻断（NETSDK1004），不能报告为全工程通过。
- **撤出探针后复核**：当前 Manager、GMPanel、Popup 均不再含 `TestGcdTimeout`／`gcd_probe_*`，两个 GM 脚本及 GM 参考文档与 `feature/deeplink` 基线无差异；正式 `ObservableConversionData`、`WaitForGcdFallbackAsync`、UDL 仲裁和 iOS ERROR 补偿调用仍在。重跑上述四种宏组合定向编译均为 0 错误（Editor 有 5 条未使用字段警告），`git diff --check` 通过；**未在撤出后的代码上构建／安装新 APK 或 IPA**。
- **待验证**：尚未在本次合并代码上执行 Unity 构建、模拟器或 iOS 真机运行测试；GCD 与 ERROR 同时抵达及从候选五秒转到十秒重试的竞态须专项验证。上述历史 APK 与远端 TEMP 测试仅对应各自合并前版本。
- **后续设备回归入口**：先从本工作区构建新 Android Debug APK／iOS 测试包，仅在隔离设备测试。分别从外部点击 `IjED` HTTPS、`uribrainpunk://link?...` 直达和 AF 自动 `uribrainpunk://?...` 回退，逐项核对系统 VIEW、`callback`、`queued`、`target_resolved` 与关卡画面；对合法无目标 UDL 与有效首次非自然 GCD 覆盖 `gcd_queued`（无 UDL 回调时约五秒）。受控触发 ERROR 并模拟离线／首页弹窗及晚到结果，核对首次 ERROR 起十秒、最多两次主动重交、GCD 抢先时停止重试、相同目标不二次进关；iOS 无 URL 首装还须核对 `install_recovery_queued` 或保留首页的无目标路径。新构建、真实 SDK 归因、Play/App Store 与 iOS 真机结果均不得由静态／替身编译冒充。
- 回滚本次整合时分别还原 v4 游戏逻辑和这两份文档的对应新增章节；Android/iOS URI、原有 GCD 及其历史证据不能因为回滚远端 ERROR 逻辑而一起删掉。


## 2026-09-24：合并 deeplink_simon，v7 生效规则与验证

### 范围、依据与最终行为

- 合并父版本：本分支 `a535de980`，对方 `8d5520755`（`origin/feature/deeplink_simon`），共同基线 `d17a169db`。本轮解决 Manager 和本记录、当天记录共三个文件的冲突；对方自动合入的 SRN 交接清单、AndroidManifest、iOS URL Scheme 和 9 月 23 日记录均保留。双方历史实测章节保留，历史“最多两次／超时丢弃／v4”不作为当前规则。
- **当前日志版本为 `onelink-v7`。有效规则：每玩法一次；三次主动重试、总十秒；十秒只截止主动网络等待，有效晚到 UDL/GCD 仍按优先级和首页门禁排队；GCD 候选等待尚未决出的 UDL 最多五秒。** 两项时间分别约束主动重试和已有安装候选，不增加启动 Loading 的等待。
- UDL 已有有效目标时不接受 GCD；GCD 已有有效目标而 UDL 返回 NOT_FOUND/无有效协议时立即排队；UDL 尚未返回则 GCD 等满五秒。GCD 必须首次非自然安装且能通过原 `open_level/sub1/sub2` 解析，不从 campaign 猜关卡。
- iOS 无 URL 的 ERROR 首装补偿继续可用，包含十秒后到达的有效结果；只有一条 `ObservableConversionData` 订阅在游戏侧仲裁。无 ERROR 且 UDL 未返回时，iOS 安装数据与 Android 一样遵守 GCD 最多五秒等待，而不再沿旧 iOS 单独订阅立即排队；这是接入对方 UDL 优先设计后的统一规则。新真实链接、GM 测试、已受理目标优先，GCD 已尝试进入后不再接受旧 deferred UDL。
- `AcceptPayload` 同时保留本分支清除 `_automaticUrls` / `_pendingUrl` 和对方取消 GCD 候选的收尾，避免成功后旧请求重放。GCD 计时独立于 ERROR 预算，十秒截止不丢弃已经取得的有效 GCD 候选；退订时仍取消候选和计时。
- URI 保留固定入口 `uribrainpunk://link`（无其它路径），从 query 解析协议。合法 URI 即使 SDK 先报 ERROR 或断网，也通过本地回退解析，不额外发起网络请求。裸 URI 只唤醒，不提供默认业务目标；无关 URL 不覆盖已经排队的有效目标。
- 每玩法成功后写历史、IWanner ID/Path 映射到组首、普通/Reward 胜利隐藏 Replay、GameData 生成成员均保持本分支实现。本轮不修改这些文件；此前主线旧资源加载交错问题仍待修复，不因本次合并变为已解决。

### 合并时额外发现并修正的衔接问题

- 双方各自保留安装订阅会导致 iOS 双重接收及丢失其中一份退订句柄；最终只使用对方的跨平台订阅，iOS 补偿由同一回调分派。
- 对方旧 ERROR 预算判断会在入口处挡掉本分支允许的晚到安装数据；移除这层结果丢弃，保留主动重试次数/期限和新请求优先限制。
- 对方 URI 本地解析与旧 ERROR 重试条件不一致；新增私有固定 URI 判断供两处共用，保证合法 URI 的 ERROR 回退同样本地完成。
- `gm-queued-gcd-before-fix.log` 复现了已经排队的 GCD 在新 GM 短链测试等待期间先跳关。非自动 `StartUrlResolution` 现在同步清除旧 `_pending`、来源及等待原因；正式自动解析不受这项清理影响。新增测试验证只进入新测试结果。没有恢复对方已删除的 GM GCD 注入探针，也没有改 GM Prefab/API。
- 本次只修订与合并后事实冲突的注释：重试两次改三次、超时丢弃改仅截止主动等待、安装订阅及来源优先关系；其余旧注释保持。

### 验证证据

| 验证 | 本轮结果与边界 |
| --- | --- |
| 当前生产源码模拟 | Editor 130、Android 136、Android DEBUG_MODE 141、iOS 136、iOS DEBUG_MODE 137 项断言通过；每组含原 98 项生成字段/存档/玩法/胜利按钮合同模拟，并加入本次回调顺序、五秒/十秒、URI、GM 交错检查 |
| 时序检查 | 覆盖 GCD/UDL 先后、FOUND 无有效目标、NOT_FOUND、五秒未到/到期、重复归因、自然量/非首次/缺协议/campaign-only、十秒后安装结果、离线与晚到标记回调、旧标记失效、GCD 后每玩法一次、不同玩法新链接、弹窗/前后台/关内门禁、已有 SDK 转化值重放、退订及 GM 新目标替换已排队 GCD |
| 定向编译 | 使用当前 Unity 引用的 Roslyn Editor 和 Android 编译均退出 0；分别 0 错误/105 警告、0 错误/108 警告。未进行完整 Unity 构建或 iOS IPA 构建 |
| 三方及资源静态检查 | 39 项通过：本分支关键方法保留、单订阅/单退订、三次十秒、对方 GCD 计时/取消方法、路由/存档/控制器文件未改、对方五项自动合入文件保留、Manifest XML/独立 URI 及原 HTTPS 过滤器、iOS scheme、六项无关改动摘要未变 |
| 文档及差异 | 双方独有模块主题及当天主题均保留；先记录修改前核对再写业务代码。定向 `git diff --check` 通过，最终冲突状态在合并收尾核对 |

- 证据目录：`C:/Users/admin/AppData/Local/Temp/brainpunk-onelink-merge-20260924/`。`baseline.json` 与三方源码保存开工状态；`MergeTests.cs` / `MergeTests.csproj`、`final-test-*.log` 是组合验证；`final-editor-compile.log` / `final-android-compile.log`、`static-verification.json` 是编译和静态证据。测试项目直接编译当前 Manager/Router/Request/GameData，SDK、时钟、Unity UI、资源与控制器部分为替身。
- 复跑：`dotnet run --project MergeTests.csproj -p:DefineConstants=UNITY_EDITOR --no-restore`；其它宏使用 `UNITY_ANDROID`、`UNITY_IOS`，调试组合用 `%3BDEBUG_MODE` 作为 MSBuild 分隔。定向编译命令为 `dotnet csc.dll @editor.rsp` / `@android.rsp`，从项目目录执行，输出只在 TEMP。
- **uncertain**：未运行合并后的 Unity PlayMode、APK/IPA、真实 SDK 网络与系统 App/Universal Links、商店归因、手机资源加载、磁盘故障及原生广告后的画面。对方 `/tmp/` 历史设备证据未在本机取得，不能充作合并版实测。尤其不能以宏模拟宣称 Android/iOS 真机已经全部通过。
- 回滚：以合并前本分支 `a535de980` 为功能基线，按合并提交的第一父版本撤销本轮游戏侧 Manager/平台 URI 声明与新增记录；不得夹带无关工作区文件、写 SDK/Packages/GF 或删除玩家解锁/OneLink 历史。仅文档回滚则移除本节及当天合并章节，保留双方原有证据。


## 2026-09-24：所有玩法进入游戏模式后丢弃跳转

### 需求、原行为与当前行为

- 用户明确要求：主线、Draw、IWanner 进入游戏模式后，直接放弃本次 OneLink 跳转，不等返回首页补跳。此前关内接收的参数仍排队，直到首页空闲才尝试路由；本节替代历史记录中“关内结果保留至首页”的规则。
- 三个玩法现有的 `OnLevelEntryStarted` 均在接受新关/新组时调用。因此从开始进关（包括首次资源加载/漫画/转场）就关闭受理，暂停、胜负结算、Reset、Replay 与 IWanner 组内推进不恢复受理。退出到选关后也须返回首页，才重新接受新点击。
- 进关时清除已排队参数、待解析 URL、GCD 候选/五秒计时、主动解析等待/标识和 ERROR 重试。关内收到 Unity URL、UDL FOUND/ERROR、安装转化数据以及 GM 参数/URL 测试均拒绝。这里只停止游戏侧等待，不能取消 SDK 内部已经发出的网络请求。
- 返回首页只恢复新点击的受理资格，不恢复旧任务。旧轮次无关联回调不会自行重新排队；新的系统 URL 或 GM 提交建立新轮次。冷启动尚未进关、首页动画/弹窗尚未结束时，原有排队及首页门禁保留。
- 已回首页时若新点击的非 deferred UDL 先于 Unity URL 到达，且有效参数带明确 URL、没有旧解析标识，则仅暂存一份等待确认的参数；它不计入 `HasPendingRequest`，不触发网络或跳转。随后必须收到相同的系统 URL 才采用，不同 URL、新 GM 测试或再次进关会清掉候选。关内收到的参数不做此暂存。

### 原流程、修改位置与数据边界

- `Assets/Game/Scripts/SDK/Services/Core/OneLinkManager.cs`：游戏侧手写代码；复用三玩法已有进关通知，记录内存中的关卡会话、丢弃轮次以及首页待 URL 确认的单个回调。新增 `NotifyHomeOpened()`，仅用于恢复接收新点击。
- `Assets/Game/UI/StartPanel/Scripts/StartPanel.cs`：`OnOpen()` 通知首页已打开；不改原有入口刷新、动画、序列化/生成绑定、Prefab 或 UI 栈规则。`OnCreate/OnShow/LateUpdate` 原职责保留。
- `Assets/Game/Scripts/GameManager.cs`：`TryExitDrawLevelSelection()` 的保留首页返回分支也通知恢复，覆盖该路径不重新执行 `StartPanel.OnOpen()` 的情况。首次进入 Draw 选关但未选择关卡，不会提前进入游戏会话。
- 丢弃只清理请求，不调用 `EndEntry`，不修改 `IsEnterByOneLink`、`OneinlinkEnterType` 或解锁/通关存档；因此当前已通过 OneLink 进入的关卡仍按原来源隐藏 Replay。没有增加事件、资源加载、SDK 请求、持久化字段或打点。
- 调整了一条已有注释：`OnLevelEntryStarted` 的“保留尚未加载的 OneLink 请求”改为保留本次来源、丢弃其它待跳转任务，避免与本次行为冲突；其它已有注释未重写。

### 验证与日志

- 前置核对先记录在当天共享记录，之后才修改业务源码。实现中发现“首页新点击 UDL 先到、Unity URL 后到”会丢失参数，先以 iOS 宏模拟复现失败，再补充匹配 URL 确认分支。
- TEMP 证据目录：`C:/Users/admin/AppData/Local/Temp/brainpunk-onelink-gameplay-20260924/`。`GameplayTests.cs` / `GameplayTests.csproj` 直接编译当前 OneLink Manager/Router/Request/GameData，SDK、UI、时钟与部分控制器为替身；组合原每玩法次数/胜利来源、合并规则及新增游戏会话交错验证。
- 最终模拟：Editor 191、Android 193、Android DEBUG_MODE 205、iOS 193、iOS DEBUG_MODE 201。覆盖三玩法会话内连续回调拒绝、普通进入不消耗次数、OneLink 来源/次数保留、参数/URL/GCD/ERROR 等待取消、旧标记晚到、回首页不自动补跳、新点击和首页 UDL/URL 两种顺序、错误 URL 不使用候选及首页弹窗继续等待。
- 暂停/结算/Reset 的具体 Unity 执行过程未由模拟运行；源码核对这些流程不会调用首页恢复通知。模拟只验证同一关卡会话未结束时重复回调仍拒绝，不能称为真机阶段验证。
- 使用当前 Unity 引用的 Roslyn 定向编译：Editor 0 错误/105 警告、Android 0 错误/108 警告；日志为 `final-compile-*.log`。过程中的 GameManager 命名空间引用编译错误已修正后复编通过；未构建 APK/IPA。
- 日志：`[OneLink] stage=discarded, reason=gameplay_active, gameType=..., source=...` 表示关卡会话期间放弃；`reason=activation_discarded` 表示旧轮次已作废。GM API 返回 false，URL 测试回调附关内拒绝说明。

### 风险、未验证项与回滚

- `uncertain`：尚未进行 Unity PlayMode、Android/iOS 新包、实际系统唤起/SDK 回调次序、原生广告恢复和磁盘验证。应真机覆盖三玩法的加载、游玩、暂停、结算、重开期间点击，以及回首页旧回调不跳、新点击仍能跳。
- 已作废轮次若只有无关联 SDK 回调、没有新的 Unity URL/GM 激活，无法证明它属于新点击，当前按放弃处理；这是防止旧结果回首页补跳的保守边界。SDK 回调缺少 `link` 或与系统 URL 不一致的先后顺序仍需真机验证，尤其 iOS 普通短链不主动补拉。
- 本变更不修复 `PlayCtrl.CreateLevelAsync` 的旧资源任务失效缺口；强行绕开 UI 返回首页并新开主线加载后，旧任务仍可能影响共享状态。此前代码级 A/B 交错结论保留，不得把跳转接收策略当成资源取消修复。
- 回滚仅撤销上述三个脚本的本次游戏模式门禁/首页恢复通知，以及本节与当天对应记录；无需改存档、生成代码、配置、SDK/Packages/GF 或资源。撤销后恢复关内参数可留到首页的旧策略。

## 2026-09-24 iOS 关闭 Firebase Dynamic Links 首次安装自动检索

### 修改前核对

- 需求与行为基线：用户批准关闭 Firebase Dynamic Links 首次安装自动检索。日志 `(15).zip` 中 UDL 仅收到 `google/link/`、`FOUND/deferred=False` 且缺少目标参数；正确目标后来由 GCD 返回。此次只消除 Firebase 自动检索产生的回传，不能据此认定已修复 UDL 安装匹配。
- 权威来源：项目 `Packages/com.google.firebase.dynamic-links/Firebase/Editor/DynamicLinksDependencies.xml:8` 声明 iOS Firebase/DynamicLinks 11.14.0。对应官方 `FIRDynamicLinks.m:130` 在自动检索开启时调用 `checkForPendingDynamicLink`；`:509` 从主 App 的 Info.plist 读取 `FirebaseDeepLinkAutomaticRetrievalEnabled`，未设置时默认 YES；`:547` 经 openURL 回传。`FIRDLRetrievalProcessResult.m:43` 在无链接时生成 `://google/link/?dismiss=1&is_weak_match=1`。源码：https://github.com/firebase/firebase-ios-sdk/blob/11.14.0/FirebaseDynamicLinks/Sources/FIRDynamicLinks.m 。这不是 `GoogleService-Info.plist` 的配置。
- 原流程与维护边界：`BuildToolV2.BuildIOS` 的本地 Debug/Release 与 Jenkins 入口复用 `AppBuilder.BuildIOS`，SDK 内两次 `BuildPipeline.BuildPlayer` 均会执行 Unity 导出回调。`BPGameCustomBuildDelegate` 是游戏侧手写构建入口；SDK `XCProjectModifier` 的顺序为 1，SKAdNetwork/Apple 为 10，I2 本地化为 10000，均读取既有主 Info.plist 后修改各自字段。没有找到已有的游戏侧 plist 扩展方法，也未发现其它写入该 Firebase 开关的源码。
- 拟修改位置与理由：在现有 `Assets/Editor/BuildPipline/BPGameCustomBuildDelegate.cs` 添加仅 `UNITY_IOS` 编译的私有静态 `PostProcessBuild(10001)` 回调，在已知本地化处理之后读取导出目录的 Info.plist，将开关以 Boolean false 写入并记录构建日志。普通 Unity 导出同样覆盖，不依赖自定义构建成功回调；重复导出可重复写入同一个键。
- 保留边界：只写 iOS 导出产物中的一个键；不修改 Packages/GuruSDK/GF、Firebase 初始化、RemoteConfig、AppsFlyer/OneLink/GCD 运行时逻辑、URL Scheme、关联域、存档、资源、玩法或 Android 构建。原有 Firebase 延迟链接自动恢复会有意关闭；当前任务已获用户明确批准。不会禁止外部主动打开 URL，也不将该开关当作全局拦截器。
- 验证计划：隔离编译现有构建类；使用真实 Unity plist 库验证缺键、原值 true、重复执行、非 iOS 不写文件，以及原 URL Scheme/其它 plist 内容语义保留；静态核对宏保护与构建注册。Windows 未安装 iOS Build Support，不能在本机完成实际 Xcode 导出/IPA 与真机验收；关闭后首次安装 UDL 是否恢复为 `uncertain`，需新包实测。
- 环境与文档缺口：现有 OneLink 文档及 SBP 构建设计未记录此开关，以上用当前源码补齐；`.codegraph` 存在但 CLI 不可用且无 MCP 工具，已改用源码检索；指定的 `../specs/037-remote-resource-loading/plan.md` 不存在。知识库检索“iOS 构建”无匹配。开工已有七项无关工作区变化，均保留。

### 验证结果与回滚

- 实现：构建类新增 `DisableFirebaseDynamicLinksAutomaticRetrieval`，`UNITY_IOS` 条件编译、`PostProcessBuild(10001)` 注册，并再次判断 `BuildTarget.iOS`；读取现有主 Info.plist 后只设置上述布尔键。完成后输出 `[OneLink] stage=ios_build_config, FirebaseDeepLinkAutomaticRetrievalEnabled=false, path=...`，可在 Mac/CI 构建日志中确认执行。
- 定向编译：使用当前 Unity 6000.0.59f2 的引用、ScriptAssemblies 和真实 `UnityEditor.iOS.Extensions.Xcode.dll` 编译完整修改后的构建类；`UNITY_IOS`、`UNITY_ANDROID` 均为 0 错误，各 3 个 CS0436 警告来自隔离编译同时引用既有 Editor 程序集中的同名构建类，编译器使用本次源码。没有执行实际 Unity 导出。
- 隔离执行：从生产源码原样提取新增回调，替换 Unity 的目标枚举/属性/日志设施，使用真实 Unity plist 库读写 TEMP 样例。缺键、已有 true、已有 false、已有字符串 true 均得到唯一的 Boolean false；重复执行产物一致；URL Scheme、本地化列表、网络配置及另一 Firebase 字段的值保持不变；非 iOS 目标不改文件，导出目录不存在时也直接跳过。初次用 XML 节点顺序比较误报，查明 Unity 会排序字典键后改为字典语义比较，生产代码未因此变更。
- 证据目录：`C:/Users/admin/AppData/Local/Temp/brainpunk-ios-fdl-disable-20260924/`，包含两平台编译 rsp/log、`PlistValidation.cs`、`PlistValidation.csproj`、`plist-validation.log` 和输出样例。目标文件 `git diff --check` 通过。
- `uncertain`：本机 Windows 无 iOS Build Support，没有构建 Xcode/IPA 或执行手机测试。验收时先看构建日志及导出主 Info.plist，再用新包覆盖“点击 OneLink 后安装并桌面首次启动”“已安装后点击唤醒”；分别记录 UDL 和 GCD。预期关闭自动 Firebase 回传，但是否恢复原 UDL 结果仍未证实，不应把 GCD 成功当作 UDL 通过。
- 平台差异：iOS 此路径通过内部生成 URL 再调用 App 的 openURL；Android 官方接收接口以 `getDynamicLink(intent)` 的结果回调交付参数，无目标时可为空。此次开关仅用于 iOS，不据此宣称 Android 的所有归因场景均无风险。参考：https://firebase.google.com/docs/dynamic-links/android/receive#handle_deep_links 。
- 回滚：撤销构建类新增的 iOS 导出回调及条件 using，重新导出并打包；已经导出的 Info.plist 需重新生成或移除该键。无需更改 SDK 或游戏存档。

## 2026-09-28 游戏侧开启 AppsFlyer 调试日志

### 修改前核对

- 需求与基线：用户要求开启 debugMode，明确禁止修改 Package。当前 `GameAnalytics.CreateAppsFlyerConfig()` 未传第四个参数，使用 SDK 构造函数默认值 false；日志（21）仍只有 UDL `ERROR/UNEXPECTED`，不包含原生请求明细。
- 来源与维护边界：`Assets/Game/Scripts/SDK/SDKImps/GameAnalytics.cs` 是游戏侧手写初始化入口，不属于生成文件。只读核对 `Packages/com.guru.sdk.framework.analytics.appsflyer/Runtime/GuruAppsflyer.cs`：构造函数保存 `debugMode`；`Prepare()` 在 `initSDK()` 前调用 `AppsFlyer.setIsDebug(config.DebugMode)`，后续才 `startSDK()`。沿用已有初始化、UDL/GCD 订阅及统计配置。
- 拟修改与影响：仅在原构造调用显式传 `debugMode: true`，补充临时诊断用途注释；不增加 SDK 启动、回调、重试或额外日志转存。该值不依赖 Development Build，Android/iOS 及 TestFlight 所用配置都会开启日志；正式发布前应恢复 false。Packages、GuruSDK 缓存与 GF 全部只读。
- 验证场景与未决项：核对 API 参数和初始化先后顺序、审查精确 diff 并执行 `git diff --check`；重新打包后由真机确认原生控制台出现 AppsFlyer 请求日志。`uncertain`：是否输出这次 UDL 的详细错误、是否进入现有 ZIP 均需真机验证，开启开关不等于已经采集到原生日志。
- 环境缺口：CodeGraph CLI/MCP 不可用，采用当前源码核对；引用的 `../specs/037-remote-resource-loading/plan.md` 不存在，知识库检索“SDK 日志”无匹配。模块既有 OneLink 文档及当前 SDK 公开配置接口足以确认本次最小变更，不依赖上述缺失资料。

### 结果与回滚

- 已实现：`GameAnalytics.cs` 原构造调用增加 `debugMode: true` 和一行中文用途注释；没有新增 API、字段、平台分支或修改其它运行时文件，未修改任何 Package。
- 静态验证：源码核对公开构造参数及 `setIsDebug → initSDK → startSDK` 顺序；`git diff` 确认运行时代码只有上述两行；目标文件 `git diff --check` 通过。未进行 Unity 编译、iOS 打包或真机验证；新增测试不适用于此次单个配置参数变更，实际日志内容仍需新包采集确认。
- 回滚：删除显式的 `debugMode: true` 及本次用途注释，恢复 SDK 默认 false，重新打包；不需要修改 Package、存档或玩法配置。

## 2026-09-29：UDL ERROR 等待原生网络恢复（更正后的当前规则）

> 事后核对更正：首轮将 getConversionData 称为“补拉”不成立。当前 iOS Wrapper 仅设置 delegate，不发请求；结构优化已删除该无效调用。首轮 987 次断言来自 GameplayRunner，未调用独立 RetryTests，不能用于证明其中 iOS 网络恢复用例。以文末本轮验证为准。

### 修改前核对与证据

- 用户要求启动期 UDL ERROR 后等待网络有效，再触发恢复。日志 (24) 中 14:08:37 原生 ConnectivityTrack 为 None，同秒 UDL ERROR/UNEXPECTED 与 Firebase 离线；14:08:40 变为 Wifi；14:08:56 首次安装转化数据返回 level_022 后完成进关。时序支持存在启动期网络窗口，但不能证明 UDL ERROR 只有网络一个根因。
- 原 URL 重试读取 Application.internetReachability；改为只读 SDK 已有 NetworkUtils.IsNetworkConnected。无 URL 时曾尝试再次调用 getConversionData，该 API 的真实含义已在下节源码核对后纠正，不能据调用名称推断会发网络请求。
- 保留边界：正常 SDK 初始化、三玩法路由、五秒 GCD 备用目标、首页门禁、关内丢弃、晚到有效安装数据与每玩法一次历史。没有延迟/重启 SDK，不修改 Packages/GuruSDK/GF、远端配置、资源或存档。

### 当前行为与验收

- 有 URL 的 ERROR：原生状态 None 时不发重试、不消耗次数，也不启动十秒预算；网络有效且首页门禁通过后可开始解析。已经在线收到 ERROR 时即开始计时，之后等待首页/弹窗/前台、再次离线仍消耗同一预算，不通过重复通知延长。
- iOS 无 URL 的 ERROR：网络无效时暂存有效安装数据并订阅网络状态；恢复后清理订阅并处理已有或后续 SDK 安装结果。没有可重放的 URL 时不主动重发 UDL，也不重复注册归因 delegate。
- 新激活、有效 URL 补达、进关、成功受理、会话重置和销毁均取消安装恢复等待；保留原 SDK ConversionData 数据订阅。回调只排队，实际进关仍由首页门禁与原路由执行。
- 可观察日志：native_network_unavailable 表示等待接口恢复；retry_budget_started 表示主动预算开始；install_recovery_network_ready 表示可继续处理安装数据；只有 callback/FOUND 或 install_recovery_queued、最终 entry_completed 才分别证明取得目标与完成进关。
- uncertain：尚未进行 Unity PlayMode、IPA/IL2CPP 或真机网络授权/断网恢复验证。NetworkUtils 只证明 Wi-Fi/蜂窝/以太网接口有效，不能证明 DNS 或 AppsFlyer 服务可达；无 URL 的原生 UDL 重新请求能力需 SDK 维护方提供和验证。
- 回滚：仅回退本节游戏侧网络门控、延后预算和安装数据等待；不迁移存档、不修改 SDK。结构整理和具体回归证据见下节。

## 2026-09-29：结构整理、无效调用清理与提交

### 修改前核对

- 需求：用户授权清理本次 TEMP、提交其余本次成果，并整理 OneLinkManager、删除无效部分。验收对象为现有完整跳转链：SDK/Unity URL → 协议及回调仲裁 → 网络重试/GCD → 首页门禁 → 三玩法路由与一次性历史；结构变化须保留此链的可观察行为。
- 原始证据：当前 Manager 1430 余行混合生命周期、URL、调试 API、GCD、ERROR 恢复、诊断及首页门禁。调用方已核对 GameSDKService.Initialize、GameAnalytics.AppsFlyerHook、StartPanel.OnOpen/LateUpdate、LoadManager、OneLinkLevelRouter 及三玩法进入/完成/退出与胜利面板。它是手写普通 C# 类，不是组件、序列化字段或生成代码，原脚本 meta 保持。
- 无效部分：只读核对 `Packages/com.guru.unity.appsflyer/AppsFlyer/AppsFlyer.cs:594` 的 Register a Conversion Data Listener，以及 `Plugins/iOS/AppsFlyeriOSWrapper.mm:328` 只设置 callback object/delegate。游戏侧再次调用不会补拉数据，删除该调用、专属 requested 标志及误导性 refresh 日志；网络恢复后仅恢复处理已有/后续安装数据。源码无法承诺无 URL 时主动重发 UDL。
- 拟修改：按职责拆分同一个 sealed partial 类到 URL、Attribution、Retry、Diagnostics、Debug 文件；主文件保留会话/玩法状态、SDK 接收、共用受理及首页门禁。合并重复的回调标识读取、GCD 校验；仅在已切至主线程的唯一调用点同步处理安装参数，去掉重复线程切换。保留 GM API、关联标识、晚到结果、网络/弹窗门禁及存档契约。
- 回归与回滚条件：先运行当前 GameplayRunner 作为基线，再实际运行 DiagnosticsTests/RetryTests，补充网络恢复、重复事件、新 URL/玩法/销毁取消的验证；Editor/Android 源码引用编译和五平台宏模拟。若正常路由、限次、去重、取消或历史存档出现差异则修正/回退对应整理，不靠删除断言接受回归。无 Unity/IPA/真机证据的部分维持 uncertain。
- 清理/提交边界：仅清理已确认属于本次的 `C:/Users/admin/AppData/Local/Temp/brainpunk-onelink-gameplay-20260924` 测试目录；历史原始日志、其它临时任务与工作区 `.scratch` 不在此范围。提交本次规则、OneLink 源码及文档；开始前字体、Android 依赖、build_info、ProjectSettings 等既有改动保留不混入。
- 回归发现与修正前核对：Android 初次主动解析在第 4 秒收到 ERROR 时，上一轮代码创建了未启动预算的 ErrorRetryState，导致原解析第 10 秒结束后才开始计时，违反已有“在线 ERROR 不后移十秒截止时间”的规则。修改前快照以相同测试稳定失败。本轮将 ERROR 状态创建收敛到 Retry 分文件，并在原生网络已有效时立即启动预算；离线仍等待网络。验收为第 4 秒 ERROR、第 14 秒截止，后续回调不续时。

### 最终结构与清理结果

| 文件 | 职责与维护边界 |
| --- | --- |
| OneLinkManager.cs | 582 行；生命周期、玩法来源与历史接口、SDK 接收/仲裁、共用受理、首页门禁和 Guru 接点 |
| OneLinkManager.Url.cs | URL 协议白名单、短链原生桥接、超时及请求标识关联；共用标识读取与判断 |
| OneLinkManager.Attribution.cs | GCD 五秒候选、唯一 ConversionData 订阅、iOS 网络等待与安装目标受理 |
| OneLinkManager.Retry.cs | ERROR 恢复入口、三次上限、十秒预算、原生网络门控 |
| OneLinkManager.Diagnostics.cs | 只输出白名单的参数/地址诊断，保留现有日志和隐私边界 |
| OneLinkManager.Debug.cs | 仅 Editor/DEBUG_MODE 的测试 API；仍使用正式门禁和玩家存档 |

- 六份文件编译为同一个 sealed partial 类，不增加对象、组件、全局事件或外部 API。原脚本 GUID 保持，新增五个脚本 meta GUID 在 Assets 中唯一。
- 删除无效的 getConversionData 重注册、专属请求标志和 refresh 日志；合并唯一调用链里的重复 GCD 校验，去掉安装数据二次切主线程。ResetActivationState 按实际职责放回生命周期区；旧注释随原方法迁移，仅更正与实际 API 含义冲突的说明。
- 网络状态有效只表示接口可用；有 URL 时可以重交既有链接，无 URL 时仅继续处理 SDK 数据，不能把 delegate 注册称为联网补拉。新 URL 补达也会取消旧安装恢复订阅，防止旧轮次参与新链接。

### 验证证据与未覆盖

- 定向源码编译：使用原 Unity 项目引用的 editor.rsp/android.rsp，并显式加入五个新 partial 源文件。Editor 0 error/105 warning、Android 0 error/108 warning，没有 OneLink 编译告警；未逐项归因其它脚本告警。没有执行 Unity Player/IL2CPP 构建。
- 模拟实际入口为 VerificationRunner，依次执行 GameplayChecks、DiagnosticsTests、RetryTests。最终 UNITY_EDITOR 214、UNITY_ANDROID 252、UNITY_ANDROID;DEBUG_MODE 270、UNITY_IOS 276、UNITY_IOS;DEBUG_MODE 287 次断言全部通过；共 1299 次，包含平台重复场景。原 GameplayRunner 的 191/194/206/194/202 次断言均保留通过。
- 新增/补接覆盖：初始 None 时零原生请求且预算不消耗；恢复 Wi-Fi/Mobile/Ethernet；无 URL 安装数据先到/后到；重复网络事件不重复恢复；新链接、UDL 先成功、进关和会话销毁取消订阅；无效 API 调用次数为零；首请求在线 ERROR 的截止时间不后移。保留三轮上限、晚到标记、弹窗/前台门禁、三玩法历史/失败路由与日志脱敏回归。
- 测试设施更正：旧诊断断言仍期待 invalid_http_url，实际现行分支为 unsupported_uri；Android 无 URL 已终止而后才收到首个真实 URL 的预算与 iOS 不同；旧 iOS 测试遗漏 GCD 五秒时钟推进及真实玩法进入生命周期。此次按现行源码补齐这些场景，不因过时预期更改生产行为。修改前快照复现在线 ERROR 截止时间后移，修正后同一断言通过。
- 静态检查：git diff --check 通过；新旧脚本 GUID 核对通过；Packages/GuruSDK、GF 无本次改动。所有外部调用接口保持，OneLinkRequest/OneLinkLevelRouter、共享 UI/玩法脚本、生成存档均未改动。
- uncertain：未做 Unity PlayMode、iOS/Android 真机、联网授权/服务不可达、真实 AppsFlyer 标识回传和异步切后台验证。模拟只证明编译条件下的游戏侧状态处理。未配置自定义 Scheme 在普通 URL 解析与 ERROR 原生重交中的既有边界不同，本轮保留此行为，不借结构优化扩展协议改动。
- 回滚：可按本次提交整体回退游戏侧文件及五个 partial/meta，恢复单文件结构；不回退其它工作区内容，不改 SDK、平台依赖或存档。若只回滚计时修正，必须同时恢复对应预算规则记录，不能宣称原缺口已修复。
- TEMP 收尾：验证完成后，将上述本次测试目录整体移入 Windows 回收站（86 个文件，18,977,896 字节），确认原路径已不存在；可从回收站恢复。保留其它历史日志目录、用户原 ZIP、Unity Temp 和其它任务缓存。永久递归删除先被安全检查拒绝，随后采用可恢复方式完成清理。

## 2026-09-29 首装 GCD 缓存补偿（不修改 GuruSDK）

### 背景与问题

- iPhone 首装真机日志确认 AppsFlyer 已返回有效数据：`af_status=Non-organic`、`is_first_launch=true`、`deep_link_value=open_level`、`deep_link_sub1=default`、`deep_link_sub2=level_022`。
- 但 OneLink 只有 `stage=bound`，没有 `gcd_candidate`、`install_recovery_queued`、`queued` 或 `entry_accepted`；随后原生重复回调变为 `is_first_launch=false`，首装补偿无法再受理。
- GuruSDK 的缓存命中分支只更新公开的 `GuruAppsflyer.ConversionData`，不会重放 `GuruAppsflyerManager.ObservableConversionData`。GuruSDK 与 Packages 按只读边界保留，不在本轮修改。

### 修改与数据流

- `OneLinkManager.BindAppsFlyer()` 接好原有 UDL/GCD 订阅后，启动游戏侧短时缓存探测：最多 5 秒、每 100 毫秒检查一次，不阻塞 Loading。
- 探测读取公开的 `GuruAppsflyer.ConversionData`，发现有效快照后复用现有 `HandleConversionDataAsync` / `AcceptGcdPayload`，仍由原有 `Non-organic + is_first_launch + open_level/sub1/sub2` 白名单决定是否成为 GCD 候选。
- UDL、系统 URL、GM 测试和已受理目标仍保持更高优先级；缓存快照按原始数据去重；退出/会话重置时取消探测。没有新增存档、埋点、SDK 请求或资源等待。
- 新增诊断：`conversion_cache_seen` 表示读到 GuruAppsflyer 缓存；`conversion_cache_miss` 表示 5 秒窗口内未读到有效快照。

### 验证与未验证项

- `dotnet restore BrainPunk/Assembly-CSharp.csproj` 成功；`dotnet build BrainPunk/Assembly-CSharp.csproj --no-restore` 通过，0 错误、1107 个既有警告。
- `git diff --check` 仅报告工作区既有 `.meta` 文件尾随空白，本次 OneLink 改动未新增该类问题。
- 尚未用包含本次改动的新 IPA 在 iPhone 上复测；预期首装日志应出现 `conversion_cache_seen → gcd_candidate → queued → target_resolved → entry_accepted`。UDL 直接成功、自然安装、已安装再营销和缓存晚到场景仍需真机覆盖。

### 回滚

- 仅删除 `OneLinkManager.cs` 的缓存探测字段、探测任务、取消逻辑和 `StartCachedConversionProbe()` 调用，即恢复修改前行为；不改 GuruSDK、Packages、存档或平台配置。

### 2026-09-29 推送前合并记录

- 合并远端 a3b6e9040 与本地 c63f0a6dd：上述缓存探测字段、常量和方法迁入 OneLinkManager.Attribution.cs，绑定入口与 Dispose 取消保留主文件；远端探测方法逐字比对一致。AcceptGcdPayload 已在结构优化中并入 HandleConversionDataAsync，原安装校验仍保留。
- 合并后重新定向编译：Editor 0 error/105 warning、Android 0 error/108 warning，无 OneLink 编译告警。此前 1299 次模拟断言属于合并前版本，本轮没有把该数字作为合并后验证；未进行 IPA/真机测试。
- 原字体、Gradle、build_info、ProjectSettings、.scratch 文件合并前后 SHA256 一致；仅提交合并结果及记录，不包含这些本地未提交修改。远端随附 meta/2026-09-21 历史文档按原提交保留。

## 2026-09-29：getCoin / getSkin 奖励指令

### 修改前核对

- 需求：在 `deep_link_value` 增加 `getCoin`、`getSkin`；`sub1` 为领取唯一标识，两个动作共用同一 GameData 领取集合；`sub2` 为金币正整数或 Skin 配置 Id；可选 `sub3` 限定用户。原 `open_level`、SDK 初始化/ERROR 网络恢复/GCD 仲裁、首页门禁和关内丢弃规则保留。
- 来源：`OneLinkRequest.TryParse` 当前仅接收 open_level，`OneLinkManager.Url.TryParseUrlQuery` 仅保留 sub1/sub2；`AcceptPayload` 的会话键只含玩法和路径，必须按指令区分，不能把奖励当成默认玩法。
- 用户身份依据：`GameAccountUtils.CurrentUser/CurrentUid`、`GameSettingPopup.SendEmail` 使用 Guru `AccountDataStore.User.Uid`。本次采用同一 Uid，精确匹配；指定用户但身份尚未就绪时在首页等待，不主动登录、不用设备 ID 或 AppsFlyer ID 代替。身份有效但不匹配时拒绝，不记录领取。
- 发奖依据：`SimpleBag.AddTool(Coin, ..., RewardType.Media, autoSave:false)` 负责余额、既有更新事件和 earn_virtual_currency；`GetNewSkin(..., Reward, autoSave:false)` 负责 OwnedSkin、WaitingPreviewSkinId、红点与 getNewSkin，不穿戴。本次复用这些入口，不创建领奖弹窗，不改变共享背包方法。
- 保存依据：`GameData` 是 PlayerPrefs 的单份 JSON；`DB.SaveGameData -> BaseSaveDataSO.Save -> SaveManager -> PlayerPrefsSaveProvider` 同步保存。先校验，再将标识加入集合，调用背包发奖（关闭自动保存），最后将奖励与标识同份保存；背包发出事件前奖励已写入，标识也已存在，可阻止重入重复。保存失败保留奖励和标识的内存状态，不伪报持久化成功；再次点击同标识仅补存，不再发奖。现有背包事件/打点先于保存，不能宣称服务端事务或打点恰好一次。
- 生成边界：`GameData.cs` 的 SerializeField 是输入；`PartialClassGenerator.BuildPartialClassCodeStatic` 生成 Init 初始化和 PascalCase 公共属性。仅同步新增集合对应生成片段，不整文件覆盖既有差异、不改 GameData.asset、不写 Packages/GuruSDK/GF。
- 拟修改范围：Request 协议、Manager 共用受理/首页分发、新 Rewards partial、URL sub3 白名单、诊断只记录 sub3 是否提供、GM URL 提示，以及 GameData 原字段与生成片段。旧档集合为 null 时局部初始化；已有皮肤视为领取完成，不重复加皮肤、不转换金币、不重复打点。
- 验证矩阵：三动作、Android 平铺/iOS 嵌套/URL、sub3 丢失/空/匹配/不匹配/身份未就绪、非法数量与溢出、无效 Skin、已拥有 Skin、同标识跨动作/回调/新点击/重启、防重集合旧档 null、保存失败后补存、首页/弹窗/后台/玩法门禁，以及 open_level 原回归。代码和模拟之外的 Unity/原生平台证据保持 uncertain。
- 安全与未知：该协议未提供服务端验券或签名，仅实现本地防重；伪造新标识、篡改数量/目标用户、清档/换设备均不由此集合防护。已向用户说明，不擅自新增服务端、签名协议或运营数量上限。CodeGraph 目录存在但工具不可用；引用的 037 plan 缺失，依据当前源码、模块文档与用户本轮协议实施。

### 已实现协议与行为

| 字段 | getCoin | getSkin |
| --- | --- | --- |
| deep_link_value | getCoin | getSkin |
| deep_link_sub1 | 必填，奖励唯一标识，如 coin_20260929_01 | 必填，奖励唯一标识，如 skin_20260929_01 |
| deep_link_sub2 | 必填，正整数，如 100；拒绝小数、负数、零及 int/余额溢出 | 必填，当前 TbSkin 的精确 Id，如 201001 |
| deep_link_sub3 | 可省略/null/空白；否则精确匹配当前有效 Guru Uid | 同左 |

- 两个动作不区分大小写；sub1、Skin Id、Uid 去首尾空白后区分大小写。sub1 全局共用：同标识先领金币，再改成皮肤或另一数量也不再发放。运营需为每次独立奖励设置不同标识。
- 示例参数：`deep_link_value=getCoin&deep_link_sub1=coin_20260929_01&deep_link_sub2=100`；指定用户时追加 `&deep_link_sub3=<GuruUid>`。皮肤示例：`deep_link_value=getSkin&deep_link_sub1=skin_20260929_01&deep_link_sub2=201001`，201001 存在于当前本地 tbskin.json，不代表任意线上版本均已交付。
- UDL 平铺/iOS click_event、系统 URL、GCD 与缓存快照共用 Request；URL 白名单新增 sub3 且重复键拒绝，不能因本地解析丢失用户限制。GCD 保留首次非自然安装与原仲裁，不扩大为普通启动重复发奖。
- `OneLinkManager.Rewards.cs` 从首页消费点分发，不调用 OneLinkLevelRouter、不修改 OneinlinkEnterType/穿戴/关卡进度；已有皮肤仅登记领取标识，不新增皮肤或重复打点。只读现有 Skin 配置，不改资源或下载链。
- `GameData.OneLinkClaimedRewardIds` 是新增持久化集合；无网可处理已拿到的合法参数，不给奖励另加网络请求。指定 Uid 未就绪保留待处理请求，但退出、关内丢弃、新有效请求仍按原生命周期处理；账号不匹配不会主动切号。
- 成功以 `reward_completed` 为准，排队日志是 `reward_queued`；重复记录 `reward_duplicate` 和补存结果。`reward_incomplete` 不表示持久化成功，内存已发奖时仍保留防重；背包打点异常也会尝试保存已改变的奖励与标识。
- 新增 sub3 的诊断只记是否提供，不输出 Uid。原 Request/Manager 注释和 URL/GM 提示曾只描述关卡，与新增奖励范围冲突，因此局部改为进关/奖励或执行指令；没有重写无关注释。

### 验证与证据边界

- 验证目录：`C:/Users/admin/AppData/Local/Temp/brainpunk-onelink-rewards-20260929`。直接编译当前全部 OneLink partial/Request/Router、GameData 原字段与 partial；金币和皮肤使用从当前 SimpleBag 逐字提取的 GetToolCount/AddTool/GetNewSkin，Unity、SDK、身份、配置存在性、存储 I/O、事件/打点外围使用替身。现有进关下游使用此前 pertype 验证的提取快照，不声称重新验证所有下游玩法实现。
- 五宏奖励矩阵：UNITY_EDITOR 89、UNITY_ANDROID 91、UNITY_ANDROID+DEBUG_MODE 92、UNITY_IOS 91、UNITY_IOS+DEBUG_MODE 92 次断言通过，共 455 次（含平台重复，非 455 个独立用例）。覆盖两种奖励、平铺/iOS/URL/短链标识回调、冷启动、sub3 全链保留、跨动作去重、JSON 序列化重启、旧档 null、错误用户/身份等待/消费重校验、溢出、无效/已拥有皮肤、保存失败补存、事件重入、打点抛错、首页/弹窗/前台/关内丢弃、GCD/缓存与 open_level 三玩法的受理及完成确认。
- 使用 Unity Roslyn 及现有 Unity 工程引用定向编译：Editor 0 error / 113 warning；Android 0 error / 116 warning，没有 OneLink/GameData 诊断。warnings 未全部逐项归因，不等于 Player/IL2CPP 构建结果。
- iOS 整体定向编译受环境阻塞：ChoiceLevelPanel.cs:17、StartPanel.cs:14 缺少 `Guru.UniKit.Apple` 引用，2 个 CS0234；未修改只读 SDK 或共享 UI 规避。iOS 宏模拟通过与整体 iOS/IPA 编译必须分开看待。
- 运行已留存的 PartialClassGenerator 静态生成方法副本输出 GameData 预览：新增集合 Init 恰好一次，属性与实际片段忽略换行后相同（首次逐字符比较仅因换行不同为 false）。未回写整份生成文件，也未改 .asset。
- `uncertain`：未做 Unity PlayMode、真实 PlayerPrefs 磁盘失败/强杀、真实账号异步恢复、Android/iOS OneLink 后台与签名包、实际皮肤资源加载/红点视觉或后台打点验证。单份 JSON 与模拟防重不是服务端 exactly-once 保证；清档、存档回滚、换设备和伪造参数仍有风险，正式运营需另行确定可信发奖校验方案。

### 回滚

- 一起回退 Request 奖励分支、Manager 奖励分发/门禁、Rewards partial/meta、URL sub3、诊断/测试提示及 GameData 字段/生成片段。不要删除用户整个存档、回退 SDK 或现有字体/平台工作区改动。
- 已领取的金币、皮肤不会因回滚代码自动撤销；后续重新上线必须保留或迁移 OneLinkClaimedRewardIds，否则会重新获得领取资格。本轮未提交 Git、未执行每日风险报告上传或飞书发送。

## 2026-09-29：GM 读取系统 URL 与真实 ERROR 单次恢复

> 后续入口位置已按用户截图改为下文“系统 URL 控件迁入 OneLink 测试弹窗”；本节 IMGUI 位置只记录首次实现，不再作为当前操作说明。

### 需求与前置核对恢复记录

- 用户明确分为两件事：GMPanel 显示当前 `_systemUrl`，按钮主动请求这个 URL；正式 UDL 返回 ERROR 时使用保存的 `_systemUrl` 自动重试一次。不是模拟 ERROR，不是填写另一个测试 URL，也不是无 URL 首装主动重取。
- 本轮首次实现前已核对并记录基线；约 17:54 用户误重置工作区，首次代码和核对记录一起丢失。用户确认“刚才我给删掉了不小心”并要求重新修改。本节恢复先前核对事实，同时记录恢复后的验证；不声称本节文件写入早于重置后的代码恢复。不恢复其它任务被重置的内容。
- 来源与原流程：`OneLinkManager.ReceiveSystemUrl` 保存真实 URL，`HandleDeepLinkAsync → HandleDeepLinkError → ScheduleErrorRetry → TryStartErrorRetry → StartUrlResolution` 负责 ERROR 恢复；原上限为三次。`GMPanel.OnGUI` 已展示语言、用户 ID、Star 和每日挑战，`GMOneLinkTestPopup` 则是独立输入测试，不读取真实系统 URL。
- 维护边界：复用 GM 已有运行态 IMGUI 信息区及游戏侧 SDK 公开桥接；不改 Prefab、生成绑定、Packages/GuruSDK/GF、SDK 初始化、RemoteConfig、存档结构或奖励协议。已读取 GMPanel 参考文档、OneLink 本文、历史及当天记录和编辑态约束。CodeGraph 索引存在但 CLI/MCP 不可用，使用当前源码；规则引用的 037 plan 本地缺失。
- 拟修改位置与理由：Debug partial 暴露真实地址和手动入口；Url partial 区分手动请求日志并允许 iOS 手动重交；Retry partial 按本轮明确要求限制一次，并消除原请求 ERROR 后先耗尽等待才恢复的问题；GMPanel 原有 IMGUI 区添加显示和按钮。原首页/前台/玩法门禁、GCD 仲裁、URL 白名单、进关历史与奖励防重保留。
- 验收矩阵：空 URL、SDK 未就绪、无网、非运行态、关内、已有待处理请求、双击、手动真实请求、ERROR 重复到达、补发再次 ERROR、NOT_FOUND、原请求晚到成功、换链接、进关取消、奖励防重及平台宏。真机布局、SDK 网络与回调回传属于必须另测的未知项，不用模拟结论替代。

### 当前行为与调用边界

- `OneLinkManager.Debug.SystemUrl` 只读显示当前真实系统 URL；不会用 GM 输入框或带请求标识的 SDK 地址覆盖它。`RequestSystemUrl(out message)` 只在 Editor/DEBUG_MODE 编译；Editor 明确拒绝原生请求，Android/iOS 调试包才调用 SDK。
- GM 展开后的信息区，在“每日挑战进度”下方显示可滚动、换行的 `_systemUrl`，下方按钮为“主动请求当前 _systemUrl”。地址为空时按钮禁用。保留原 GM 显隐规则；`OnGUI` 外层增加 `Application.isPlaying`，避免编辑态访问运行数据/单例。没有新增 Prefab 节点或层级查找。
- 手动入口拒绝未初始化、SDK 未绑定、关内、离线或已有 URL 解析/自动补发/待执行指令的状态；每次受理是独立手动请求，等待结束后可再次点击。不会伪造 ERROR，也不会重启 SDK。返回提示仅说明是否发起，异步结果看日志，不把“请求已发起”称为跳转成功。
- 请求链：按钮 → `RequestSystemUrl` → `StartUrlResolution(automatic:true, manualSystemUrl:true)` → Android `performOnDeepLinking` / iOS `handleOpenUrl` → 现有 UDL 接收和正式门禁 → 路由或发奖。请求仍追加已有 `deep_link_sub10` 关联标识，业务 URL 和 `_systemUrl` 不变；已有 sub10 冲突按原规则拒绝。
- 自动恢复：真实 ERROR 优先使用 `_systemUrl`，缺失时保留原回调 link 兜底。短链在原生网络有效后、首页门禁允许时最多补发 **一次**，十秒预算不从首次离线 ERROR 开始消耗。再次 ERROR 或超时不再补发；`NOT_FOUND` 不当作 ERROR。网络接口有效不等于 AppsFlyer 服务可达。
- 已带合法目标参数的长链接保留原本地解析兜底，不强制增加网络依赖；iOS 无 URL 仍是安装归因/缓存恢复，并未新增无 URL UDL 请求。不要用本次 GM 按钮宣称无 URL 首装问题已解决。
- 无标识 ERROR 不能精确关联某个原生请求：原 URL 首请求进行中允许进入一次有界恢复，补发期间的无标识 ERROR 只记录并继续等待，不产生第二次补发。同一次唤起原请求晚到 FOUND 仍可被受理；新链接会清理旧请求关联，已受理后的重复结果不会再次执行。
- 成功仍通过正式首页、弹窗、前台及存档门禁；GM 请求不是绕过历史或重复领取限制的工具。没有新增存档字段、资源、全局事件或打点。仅修正两处与新单次恢复事实冲突的旧注释（三次上限、无标识错误的处理说明），不重写其它注释。

### 验证记录与未覆盖

- 证据目录：`C:/Users/admin/AppData/Local/Temp/brainpunk-onelink-systemurl-20260929`；`Verify.ps1`、`Harness.csproj`、`SystemUrlTests.cs` 和各宏日志可复跑。直接编译当前 OneLink 源码和 GameData；Unity、SDK、网络、配置、存储使用替身，部分玩法下游使用既有提取快照。没有把模拟原生调用当作真实 HTTP 或 SDK 成功。
- 五种宏新增断言分别为 Editor 12、Android 23、Android+DEBUG_MODE 44、iOS 18、iOS+DEBUG_MODE 39，共 136 次；同轮保留奖励回归 89/91/92/91/92，共 455 次。总计 591 次断言执行通过，包含跨平台重复场景，不是 591 个独立用例。
- 实际覆盖：空地址/SDK/网络/玩法/并发拒绝、按钮不伪造 ERROR、不覆盖 `_systemUrl`、真实桥接调用、原地址附加标识、独立手动再请求、单次自动恢复、断网等待、超时/重复错误不循环、旧标识隔离、晚到成功去重、进关取消、原生桥接抛异常，以及 Release 不暴露 GM API。现有奖励与首页弹窗门禁回归同轮通过。
- 使用现有 Unity 引用与 Unity Roslyn 定向编译当前脚本：Editor 0 error / 113 warning，Android 0 error / 116 warning。初次恢复验证误用了已被重置删除的 DigGame 源文件清单，产生 CS2001，已改用当前提交对应的 OneLink 引用清单；随后发现并修正 GM `CalcHeight` 需要 `GUIContent` 的 CS1503，再编译通过。没有为消除错误恢复无关文件或修改只读依赖。
- `uncertain`：未执行 Unity PlayMode、APK/IPA/IL2CPP 打包、Android/iOS 横屏布局及原生 UDL 回调测试；iOS 宏模拟不代表完整 iOS 编译成功，前轮完整 iOS 编译还存在缺 `Guru.UniKit.Apple` 引用的环境问题。未验证真实断网恢复、切后台、SDK 是否回传 sub10，以及设备网络有效但服务不可达的端到端结果。
- 真机重点：调试包首页展开 GM，核对显示的真实 URL；点击后应见 `manual_url_started`。发生真实 ERROR 后应见 `retry_started, attempt=1`，不应出现 attempt=2；最终进关看 `entry_completed`，发奖看 `reward_completed`，不能只看 started。空地址必须显示未收到，不得伪造 URL。

### 回滚

- 仅回退本节对应的 Debug/Url/Retry partial 和 GMPanel 局部增量及文档；不回退奖励指令、SDK、Prefab、存档或其它工作区改动。还原旧三次策略时同步修正文档，不把旧行为写成仍是单次。手动测试已造成的进度/奖励按正式流程保存，代码回滚不会撤销这些玩家数据。本轮未提交或推送 Git。

## 2026-09-29：系统 URL 控件迁入 OneLink 测试弹窗

### 修改前核对

- 需求与基线：用户明确要求把地址和按钮放在截图中的 `GMOneLinkTestPopup`，无需打开外侧 GM 信息区。保留原 sub1/sub2、输入 URL、粘贴及关闭按钮；只迁移系统 URL 控件，不改正式 ERROR 单次恢复。
- 来源：`GMOneLinkTestPopup.cs` 手写 SerializeField 绑定、OnOpen/OnClose、SetTestingUrl、Close；对应 Prefab 根组件的八项显式引用；`OneLinkManager.Debug.RequestSystemUrl/CancelUrlTest` 与 `TryNavigateFromStartPanel`。Popup 会阻挡首页门禁，系统 URL 请求受理后必须关闭当前 Popup，拒绝时留窗提示；不能等待自动重试成功才关窗，否则 Popup 会反过来阻塞重试。
- 维护边界：该 Popup 不是 Generated partial；UI 生成器对已存在类只绑定，不重写手写主类。本次直接维护现有 Prefab，克隆其输入框/按钮样式、显式绑定两个新增字段，不新增运行时查找或通用生成器。Packages/GuruSDK/GF 与正式 Request/Retry/Url 保持不改。
- 拟改：Popup 新增只读系统 URL 输入显示和主动请求按钮，打开及运行态地址变化时刷新；原 URL 测试期间禁用系统请求；受理后关闭输入法和 Popup，但不取消正式请求。扩大现有背景并重排控件以避免重叠；移除 GMPanel 外部重复区域，保留编辑态 OnGUI 保护。
- 验证场景：空/更新地址、GM 折叠仍显示、无网/Editor/SDK 未就绪拒绝留窗、发起后关窗且不取消正式链、原输入 URL 等待互斥、关闭重开不重复绑定、旧回调隔离、Prefab 内部引用/字段类型/布局边界、五宏 OneLink 回归及 Editor/Android 定向编译。实际 Unity 渲染/触屏键盘/原生 SDK 待真机验证。

### 完成后的职责与使用

- `GMPanel` 仅保留打开 OneLink 测试弹窗的既有入口；上一轮新增的外侧地址显示、按钮、滚动状态已全部移除，仅保留 OnGUI 运行态保护。`GMOneLinkTestPopup` 持有只读地址、系统请求按钮、刷新/提示和关闭逻辑；Manager 的 URL 状态、请求和自动 ERROR 单次恢复保持上一轮实现，不把 SDK 职责搬入 UI。
- 在原“解析 URL 并测试／粘贴 URL”按钮下方新增“当前 _systemUrl（只读）”区域和“主动请求当前 _systemUrl”按钮，独立于用户可编辑的 URL 输入。即使外侧 GM 显示 False，只要此弹窗打开，新控件也随弹窗显示；空地址提示尚未收到，按钮不可交互。
- 两个新引用由 Prefab 显式绑定：`systemUrlInput` 是只读、多行、不弹软键盘的 TMP_InputField，支持查看长地址；`systemUrlButton` 是既有风格 CustomButton。打开时及运行态地址变化时刷新文本，不分配新的管理器对象或全局事件，不向 Manager 写入输入框值。
- 系统请求受理后立即关闭当前 Popup 并停用四个输入框，释放首页门禁；不将请求开始当作成功。`_testingUrl` 仅表示原“输入 URL 测试”，因此关闭不会取消已发起的正式系统 URL 请求；失败留窗展示原因。原输入 URL 测试过程中禁用系统按钮，原参数、粘贴、关闭、版本号过滤与解绑保持。
- 背景和边框由 960×680 调为 960×940；保留原输入区相对排列，新增系统地址区放在其下、状态说明之前。脚本摘要因新职责增加而局部更新，未重写其它原注释。用户要求完成后提交并上传，只包含本任务明确归属的九份代码/Prefab/文档。

### 验证、未覆盖与回滚

- 本轮证据目录：`C:/Users/admin/AppData/Local/Temp/brainpunk-onelink-popup-20260929`。`Harness.csproj` 直接编译当前 Popup 和 OneLink 源码；`PopupTests.cs` 的 Unity/TMP/SDK/UIManager 为替身，不能替代实际渲染、键盘或网络。
- 弹窗新增五宏断言 11/1/20/1/17，共 50 次通过；同轮既有系统 URL 136 次和奖励 455 次回归通过，共 641 次断言执行（含平台重复）。覆盖空/变化地址、与手动输入隔离、离线/Editor 拒绝留窗、发起关窗且不取消正式请求、关闭解绑、重开并发拒绝、关窗后的真实 ERROR 单次补发及发奖、原输入测试互斥/取消、粘贴、参数错误和 Release 入口禁用。
- Editor/Android 定向 Unity Roslyn 编译 0 error，113/116 warning；没有运行 APK/IPA/IL2CPP 构建。Prefab 静态核对 152 个序列化对象 ID 唯一且内部引用可解析、10 项 Inspector 引用类型匹配、只读/多行/隐藏键盘属性存在、主要控件矩形不重叠且位于背景边界内；证据为 `prefab-validation.json`。保留原 Prefab GUID，未新增资源依赖。
- `uncertain`：未操作当前 Unity 会话、未渲染新 Prefab 或验证真实横屏分辨率/软键盘、长文本触摸滚动及原生 SDK 回调。静态布局不证明最终美术效果；请关闭旧实例后重新打开弹窗，必要时重新进入 Play Mode；真机需新调试包验证。
- 回滚仅撤回 Popup 两个新引用/逻辑及 Prefab 新节点和布局，将控件恢复上一轮位置时同时恢复对应 GMPanel 代码；不要撤销 Manager 正式单次恢复或奖励存档。本次未改 SDK、Packages/GF、生成器或平台配置。

## 2026-09-30：撤销错误恢复的 TestFlight 调试宏门禁

### 事后补查与修改前基线

- 用户明确指出：OneLink 真机验收通过 TestFlight 进行，`OneLink 测试`入口不得依赖 `UNITY_EDITOR || DEBUG_MODE`；该门禁不是用户需求。Git 行级证据显示，`GMPanel.cs` 的入口宏由提交 `1d4163fb94705e668cb99fda2b33f5199bda2707` 首次加入。用户此前已在工作区去掉该限制，但 2026-09-29 重置后恢复 OneLink 文件时沿用旧提交基线，错误地把旧门禁一并恢复；这是事后补查，不把本次记录伪装成旧实现前已确认的设计。
- 当前完整阻断链不只在 `GMPanel.cs`：`GMPanel.OnCreate` 在无调试宏时隐藏按钮；`GMOneLinkTestPopup.Create/刷新/点击/关闭` 受相同宏控制；`OneLinkManager.Debug.cs` 整个 partial 在无宏时不编译。只删除截图中的按钮判断仍无法在无 `DEBUG_MODE` 的 TestFlight 中使用。
- 现有正式包已有独立人工门禁：设置页 `gmBtn` 连续点击五次打开 `GMPasswordPopup`，密码通过后创建 `GMPanel`；该路径不依赖 `DEBUG_MODE`。`StartPanel` 自动创建常驻 GMPanel 的行为仍只在 `DEBUG_MODE`，因此撤销 OneLink 自身门禁不会让普通正式启动自动出现 GM。
- 本次目标：恢复用户原意——通过现有设置页隐藏入口和密码进入 GM 后，OneLink 按钮、弹窗、真实 `_systemUrl` 查看/主动请求以及弹窗原有参数/URL 测试均可使用，不再依赖调试宏。保留运行态、SDK 就绪、首页/玩法、网络、并发、URL 白名单、进关历史与奖励防重门禁；不改 Prefab 绑定、正式自动 ERROR 单次恢复、SDK 初始化、Packages/GuruSDK/GF、存档结构或其它 GM 功能。
- 影响与风险：无调试宏构建会重新包含 OneLink GM 测试 API，参数/URL 测试仍可能修改当前存档、进关或发奖；访问边界沿用项目既有 GM 隐藏入口和密码，不新增面向普通玩家的按钮。验收需至少覆盖无 `DEBUG_MODE` 编译下的按钮绑定、Popup 创建、系统 URL 刷新/请求、关闭取消、参数/URL 测试，以及 DEBUG_MODE/Editor 回归；TestFlight 原生显示与 AppsFlyer 回调必须用新包真机验证，静态检查不能替代。
- 拟修改位置：仅调整 `GMPanel.cs`、`GMOneLinkTestPopup.cs`、`OneLinkManager.Debug.cs` 的 OneLink 专属条件编译和与事实冲突的注释；同时更新本文、`DevelopMD/GMPanel_Reference.md` 与 2026-09-30 共享记录。回滚条件是无调试宏编译失败、现有 GM 密码入口无法约束访问，或 OneLink 原业务门禁发生回归；回滚只恢复这三份脚本的宏边界，不改用户其它工作区文件。

### 完成后的行为与边界

- `GMPanel.OnCreate` 现在始终绑定 Prefab 已有的 `oneLinkTestButton`，不再在无调试宏时隐藏它；`GMOneLinkTestPopup.Create`、系统 URL 刷新/主动请求、参数测试、URL 测试和关闭取消也不再被 `UNITY_EDITOR || DEBUG_MODE` 裁掉。
- `OneLinkManager.Debug.cs` 的 GM API 改为所有构建均编译，因此无 `DEBUG_MODE` 的 Android/iOS 构建可以读取真实 `SystemUrl`、主动交给平台 SDK，并复用原参数/URL 测试。Editor 仍由内部平台分支明确拒绝原生请求；Android/iOS 分别继续调用 `performOnDeepLinking` / `handleOpenUrl`。
- 正式/TestFlight 的人工路径为：设置页现有 `gmBtn` 连点五次 → `GMPasswordPopup` → 密码通过 → `GMPanel` → `OneLink 测试`。`StartPanel.OnCreate` 的自动常驻 GM 创建仍保留 `#if DEBUG_MODE`，普通启动不会因为本次修复自动展示 GM。Prefab、序列化引用、UI 层级和密码流程均未修改。
- OneLink 请求仍受运行态、SDK 初始化/绑定、网络、首页/玩法、并发、URL 协议、回调标识、进关历史和奖励防重约束。原参数/URL 测试会使用真实玩家存档，可能进关或发奖；本次只是恢复访问，不新增发奖能力、存档字段、事件、打点或 SDK 调用类型。

### 验证、未覆盖与回滚

- 临时验证目录 `C:/Users/admin/AppData/Local/Temp/brainpunk-onelink-popup-20260929` 的断言已按新要求改为无调试宏也必须包含 GM API。当前生产源码五组宏执行通过：Editor `SystemUrl 12 / Popup 11`，Android 有/无 `DEBUG_MODE` 均为 `44 / 20`，iOS 有/无 `DEBUG_MODE` 均为 `39 / 17`。覆盖空地址、刷新、平台桥接、并发、ERROR 单次补发、奖励门禁、关闭解绑、重开、参数/URL 测试和无宏 Popup 创建。
- 旧验证脚本最初因 C# 响应文件缺少 `-target:library` 报 `CS5001`；该错误来自临时命令没有入口点，不是生产源码。补齐输出类型后，当前 Unity 引用下 Editor 与 Android 无调试宏定向编译均为 `0 error / 2 warning`。`git diff --check` 与最终目标文件宏检索另行执行。
- `uncertain`：未执行 Unity PlayMode、Xcode/IL2CPP/IPA、TestFlight 新包安装、设置页五击/密码/横屏触摸、真实 AppsFlyer 回调或 App Store 审核环境验证。iOS 无宏 harness 证明游戏侧分支，不等于完整 iOS 构建；需新 TestFlight 包沿上述人工路径确认按钮与真实 URL。
- 回滚只恢复三份 OneLink/GM 脚本的条件编译及对应文档；不会恢复用户其它工作区变更，也不回滚正式 ERROR 单次恢复、奖励协议、Prefab 或存档。若未来要让 TestFlight 可用而 App Store 完全不包含该能力，必须引入经构建流程验证的独立渠道标识，不能再次私自用 `DEBUG_MODE` 替代用户验收要求。

## 2026-09-30：GM 增加金币与皮肤领奖参数测试

### 修改前核对

- 需求与行为基线：用户要求补充 getCoin/getSkin 领奖测试。当前参数按钮固定提交 open_level；URL 测试已经支持奖励协议。拟在同一 Popup 增加两个领奖按钮和可选 sub3 用户输入，复用 sub1/sub2；原进关、URL、系统 URL 测试保留。测试实际修改当前存档，同一 sub1 在两种奖励间共用防重记录。
- 来源路径/符号：`GMOneLinkTestPopup.TestParameters/TestUrl/OnOpen/OnClose`、`OneLinkManager.Debug.TestDeepLink/SubmitTestPayload`、`OneLinkRequest.TryParseReward`、`OneLinkManager.TryNavigateFromStartPanel`、`OneLinkManager.Rewards.ExecuteReward`。已核对 GMPanel 参考文档、OneLink 奖励及 TestFlight 章节、当日/历史记录与 Editor 态约束。
- 原流程及维护边界：Popup 手写 SerializeField 与 Prefab 显式绑定，没有 Generated 文件；BaseUIViewEditor 只在人工点击生成按钮时扫描输出 `.Generated.cs`。不调用生成器，不改 GMPanel.Generated、DB 字段/生成片段、正式奖励规则、SDK/Packages/GF 或原生重试策略。有效请求先排队、关闭输入键盘与 Popup，再由首页/弹窗/账号门禁消费。
- 拟改位置与影响：Debug partial 增加奖励专用参数 API，先校验动作与参数再提交；Popup 增加两个按钮和 sub3 绑定，打开/关闭成对绑定，URL 等待期间统一禁用；Prefab 在现有 960×940 背景内重排并显式绑定。原 sub1/sub2 注释与提示仅描述玩法，不符合共用输入事实，因此局部更新，其它旧注释不改。
- 验证场景：金币/皮肤、跨动作同标识防重、用户匹配/不匹配/未就绪、非法参数留窗且不覆盖旧目标、已拥有/不存在皮肤、关内和 URL 并发拒绝、关窗解绑/重开、URL 等待禁用、编辑态不触发数据链、无 DEBUG_MODE 的 Android/iOS API、原进关/URL/系统 URL 回归；Prefab 引用与矩形边界检查，Unity 引用定向编译和五宏模拟分别留证。
- 基线验证：修改前执行现有 PopupTests（UNITY_EDITOR），旧奖励/系统 URL 与 Popup 断言通过，Popup 11 项；日志在 `C:/Users/admin/AppData/Local/Temp/brainpunk-onelink-popup-20260929/baseline-20260930-reward-ui.log`。CodeGraph CLI/MCP 不可用，使用当前源码；037 plan 缺失，由现行模块文档和源码补足依据。
- 未决项：没有阻止游戏侧实现的协议歧义；Unity 实际渲染、移动端键盘/横屏触摸与 TestFlight 安装包尚未验证，标记 uncertain，不能用替身测试替代。

### 实现与使用

- `OneLinkManager.Debug.TestDeepLinkReward(action, sub1, sub2, sub3, out message)` 仅接收 getCoin/getSkin；运行态、关内、URL 并发与协议预检通过后，复用 SubmitTestPayload → AcceptPayload → 首页门禁 → ExecuteReward。协议预检失败不会清除或覆盖已有待执行目标。不存在皮肤、用户不匹配、已领取、存档失败等业务结果仍由正式执行层判断；API 返回 true 只表示排队。
- GM → OneLink 测试：原参数按钮标为“进关测试”，同排新增“金币测试”“皮肤测试”。共用 sub1/sub2：进关时填玩法/Path；领奖时填奖励唯一标识/金币数量或皮肤 Id。新增 sub3 输入只用于领奖目标 Guru Uid，可留空。示例：金币 `gm_coin_001 / 100`；皮肤 `gm_skin_001 / 201001`（当前本地 Skin 配置存在，真机还需相同配置）。两个动作共用领取历史，相同标识不会再次发奖。
- 有效请求关窗并关闭五个输入框的键盘，再等待首页门禁执行；拒绝留窗显示原因。OnOpen/OnClose 成对绑定/解绑两个新按钮；URL 测试期间同时禁用领奖按钮和 sub3 输入。没有新增 SDK 请求、全局事件或存档字段，也没有改变之前的正式 UDL 单次恢复。
- Prefab 保留 960×940 背景和所有旧 fileID，增加一个 TMP_InputField 与两个现有样式的 CustomButton，共 39 个对象/组件块；13 个字段显式绑定。调整布局与用途提示，不使用运行时层级查找，不执行 UI 生成器。

### 验证、风险与回滚

- 证据目录：`C:/Users/admin/AppData/Local/Temp/brainpunk-onelink-reward-ui-20260930`。`Verify.ps1` 执行当前生产源码；`RewardPopupTests.cs` 验证新增入口，复用现有 Popup/SystemUrl/Reward 回归。Unity UI、SDK、网络、配置存在性与存储使用替身，部分下游使用既有提取快照；没有真实发奖到用户存档或发送原生请求。
- 五宏新增领奖 UI/API 断言：Editor 32、Android 有/无 DEBUG_MODE 各 34、iOS 有/无 DEBUG_MODE 各 32，合计 164 次通过（含平台重复）。同轮旧奖励、系统 URL、Popup 回归也通过，总计 884 次断言执行。覆盖币/皮肤、跨动作防重、账号限制/等待、旧目标保留、非法输入、关内/并发/离线、进关取消、弹窗门禁、关闭重开与编辑态保护。
- Unity 6000.0.59f2 Roslyn + 当前 Unity 引用定向编译：Editor 0 error / 113 warning，Android 0 error / 116 warning。未逐项归因全部警告，不把五宏模拟称为完整 iOS/IL2CPP 构建通过。
- `validate_prefab.py`：191 个对象/组件 fileID 唯一且本地引用闭合，13 个字段类型匹配，父子关系正确，系统 URL 只读/新增 sub3 可编辑；16 个直接控件矩形均在背景内且不重叠。验证脚本最初误把音效组件 GUID 当 CustomButton，并未兼容原有 stripped RectTransform；核对真实 meta 与既有嵌套引用后修正脚本，最终通过，不据脚本错误改生产绑定。
- `uncertain`：未运行 Unity PlayMode、移动端横屏/键盘/实际字形渲染、APK/IPA/TestFlight 新包。需要新代码和匹配 Prefab 一起交付；真机以 `reward_completed`、背包变化及重开后的领取防重作为结果，不能以弹窗关闭代替成功。
- 回滚仅撤回 Debug partial 的新增 API、Popup 新控件逻辑与对应 Prefab 布局/绑定，以及本节文档；不回退正式奖励实现、SDK 或用户原有工作区变更。真实 GM 测试已写入的奖励和领取记录不会随代码回滚撤销。

## 2026-09-30：OneLink 领奖成功后展示通用奖励弹窗

### 修改前核对

- 需求与行为基线：用户要求通过链接打开 App 领取金币或皮肤时，显示现有 `CommonGetRewardPopup`。当前 `ExecuteReward` 只发奖和保存，没有奖励展示；保留参数协议、用户校验、同标识防重、已拥有皮肤不重复授予、首页等待及关内丢弃规则。
- 来源：`StartPanel.cs:110–115` 的动画结束后 LateUpdate，`OneLinkManager.cs:474–534` 的启动弹窗/场景/前台/首页/账号门禁，`OneLinkManager.Rewards.ExecuteReward` 的先登记、背包发奖和 finally 同份保存。UDL、GCD、系统 URL 与 GM 参数测试最终共用这一消费点，不在 SDK 回调里直接弹窗。
- 展示依据：已阅读 `CommonGetRewardPopup功能说明.md` 的纯展示模式及关闭生命周期；源码 `SetData`、`BindPreGrantedCoinDisplay`、`ApplyRewardParam`、`OnNormalBtnClick`、`ClosePopupAsync` 和 `Create` 确认 `grantNormalReward:false` 不重复发奖，`multiple:1` 不提供广告额外奖励，Coin 数量和 Skin Id 均已有展示接口。现有 ChestGift/DailyChallenge 已使用此方式。弹窗位于 Top，`UIManager.HasBlockingPopup` 会继续阻挡下一条首页指令，直至关闭与遮罩结束。
- 维护边界与拟改：仅修改手写 `OneLinkManager.Rewards.cs`，成功发奖并保存后调用现有 Create，分别传 Coin 数量或 Skin Id；新的有效领取即使皮肤已拥有也展示本次领取结果，不再授予。重复标识、拒绝、发奖异常或存档失败不弹成功窗；展示创建失败单独记录日志，保留已经保存的奖励和防重状态。无需改 StartPanel、公共弹窗/Prefab、生成代码、DB 结构、SDK/Packages/GF 或网络重试。
- 验证场景：金币/皮肤正确展示参数、一次发奖一次展示、同标识重复、已拥有皮肤、非法参数/账号/余额/保存失败/打点异常、首页弹窗与前台等待、新请求等待奖励窗关闭、创建失败不重发、原奖励与 GM 路径回归。使用当前生产代码和既有隔离替身验证调用合同，再做 Unity 引用定向编译；移动端真正显示、关闭动画和资源加载仍需真机验证。
- 工具与未决项：CodeGraph 索引存在但 CLI/MCP 当前不可用，以当前源码和文档查证；引用的 037 plan 缺失，模块已有记录补足。没有依赖未决产品取舍的代码变更；未改变外部受控包。开始前的字体、Android 构建配置、build_info、ProjectSettings、旧 meta 删除及 `.scratch/` 为用户已有变更，不纳入本次修改。
### 最终行为与数据流

- `OneLinkManager.Rewards.ExecuteReward` 保留原发奖与 finally 保存，仅在发奖返回且保存成功后调用 `CommonGetRewardPopup.Create(rewards, multiple:1, grantNormalReward:false)`。金币传 `GameProps.Coin -> int`，皮肤传 `GameProps.Skin -> string Id`；弹窗不再次入包，不改变原 `RewardType.Media` 打点、自动穿戴或关卡进度。
- 新领取记录对应的皮肤即使已拥有，也展示该皮肤；已有标识只按原规则补存，不再弹窗。保存失败或发奖抛错仍按原规则保留内存奖励与防重记录，本次不展示成功；没有增加跨启动补展示队列。
- 原 StartPanel 等待条件全部保留：启动弹窗派发完成、首页动画结束、Main 场景、App 前台、无阻挡弹窗、主线首页空闲，以及指定账号就绪。奖励窗位于 Top，正常关闭沿用通用弹窗的按钮、金币动画、数字刷新和遮罩清理；这些共享代码和 Prefab 本轮未修改。
- 发奖完成仍记 `stage=reward_completed`，随后创建成功记 `stage=reward_popup_opened`；创建返回空或抛异常记 `stage=reward_popup_failed`。展示失败不撤销已保存奖励、不清除领取标识、不重复发奖。opened 只表示 Create 返回实例，不代表已验证移动端渲染。
- GM 金币/皮肤及 URL 测试使用同一链路。已领过 `coindl` 或 `skindl` 的存档再次使用原链接不会发奖或弹窗；验收新领取需在链接/GM 中换一个未领取的 sub1，不清空用户现有记录。

### 验证、边界与回滚

- 隔离证据目录：`C:/Users/admin/AppData/Local/Temp/brainpunk-onelink-reward-display-20260930`。`Verify.ps1` 使用当前 OneLink 生产源码，新增 `RewardDisplayTests.cs`；`RewardDisplayStub.cs` 只记录展示参数/发奖保存先后、模拟创建失败与 Top 阻挡，不执行真实通用弹窗、Prefab 或动画。旧回归默认不模拟奖励窗停留，新增门禁场景单独启用阻挡；背包使用既有提取代码，平台/存储外围为替身。
- Editor、Android 有/无 DEBUG_MODE、iOS 有/无 DEBUG_MODE 五组均通过。每组新增 37 次展示断言，共 185 次；连同既有奖励、系统 URL、GM 回归共 1069 次断言执行（包含平台重复）。覆盖 UDL 平铺/iOS 嵌套、GCD 仲裁、冷启动长链接、GM 参数、金币/新旧皮肤、账号等待/不符、溢出、保存/打点异常、防重、首页/前后台/Loading/其它弹窗等待、三玩法进关取消、两奖励排队和创建失败保留已发奖励。
- 使用 Unity 6000.0.59f2 Roslyn 及现有工程引用定向编译：Editor 0 error / 113 warning，Android 0 error / 116 warning。警告未全部逐项归因；定向编译不是完整 Player/IL2CPP 构建。`git diff --check` 通过；修改限一个 Rewards partial 与本节/当日文档。
- `uncertain`：未执行 Unity PlayMode、Android/iOS 新包的真实显示、皮肤图标加载、金币飞行动画或原生回调端到端验收；iOS 宏模拟不等于 IPA 构建成功。真机验收分别使用新 sub1 的金币与皮肤链接，确认首页原弹窗结束后显示正确奖励，关闭后背包数量不再增加，再点同链接不重复领取/弹出。
- 回滚只撤销 `OneLinkManager.Rewards.cs` 本轮保存成功后展示与展示日志增量，同步移除本节及当日对应说明。无需改 SDK、存档结构、公共弹窗或 Prefab；回滚代码不会撤销已经领取的奖励与标识。本轮未执行 Git 提交或推送。
## 2026-09-30：发奖前允许 UDL 替换 GCD 奖励——前置核对

- 用户确认修复合并复核发现的待领奖时序问题。基线证据见 `DevelopMD/已有功能设计与使用记录/2026-09-30.md` 合并复核章节及 TEMP `brainpunk-onelink-merge-review-20260930`：GCD 已排队且首页弹窗阻挡时，同 ID 不同数量/用户限制的 UDL 被忽略，分别错误沿用 100 金币或因旧用户限制漏领。验收对象为最终正确发奖且只显示一次，而非仅成功解析。
- 已核对 Manager 的初始化、UDL 接收、AcceptPayload、防重、首页消费、玩法丢弃、真实新链接/GM 重置；Attribution 的普通五秒候选和 iOS ERROR 无 URL 网络恢复；Url 的标识隔离和自动解析结束；Request 的协议校验及 Rewards 的账号/背包/保存/奖励窗链。模块与 SRN 文档、当日审查和历史记录已检索，沿用现有 UDL 优先约定。
- 原因与边界：AcceptPayload 的奖励 key 仅含 activation/RewardId，排队阶段就挡住同 ID 的 UDL。iOS 恢复另用 `_installRecoveryAccepted` 拒绝后续回调，因此也必须在待执行奖励范围内允许 UDL 接管。主线程回调与首页同步消费之间没有本轮新增异步等待；消费先清空 `_pending`，其后不能再按“尚未执行”替换。
- 拟改：只改手写 `OneLinkManager.cs`。保留原两参数 AcceptPayload 入口，私有核心增加 UDL 来源标识，只有当前等待项属于普通 GCD 或 iOS 安装恢复、旧/新请求均为奖励、且来自真实 FOUND UDL 时，允许完整替换待执行请求并清除安装恢复来源。第一次接管后继续原同 ID 防重，增加脱敏 `gcd_reward_replaced` 日志。iOS 只放行上述已校验奖励，不放开进关、ERROR 或 NOT_FOUND。
- 必须保留：非法协议不覆盖旧目标，指定用户与皮肤存在性仍在原执行层核对；已经发奖、保存失败保留的内存记录、重复回调、旧标识原生回调、新 GM/新链接接管、关内/返回首页失效规则不变；不重置 GameData 领取历史，不修改 Rewards/UI/Prefab/DB 生成器、进关路由、SRN 映射与已确认的定向 URL 失败回退政策，不写 SDK/Packages/GF。
- 验证矩阵：普通原字段与 SRN 映射、同 ID 不同数量/皮肤/动作/用户、相同参数但新 URL、重复 UDL、不同 ID、无效 UDL、UDL 先到、GCD 候选未超时、GCD 已消费、账号等待、已领历史与保存失败、关内晚到、旧原生标识、GM/真实新链接、iOS ERROR 恢复和恢复后无效/进关回调。先让复现测试按正确结果失败，再改生产代码，最后跑五宏回归与包含新 SRN partial 的定向编译。
- 维护与未决项：本文件及三个 OneLink 业务文件均为手写；本次无需改生成内容。CodeGraph CLI/MCP 不可用、引用的 037 plan 缺失，使用当前代码和文档。没有阻塞实现的产品歧义；真实平台回调顺序、手机弹窗渲染、IL2CPP 仍为 uncertain，隔离测试不可替代。前轮合并审查记录属于已有未提交文档，保留；用户原有环境改动不纳入本修复。

### 实现结果

- `OneLinkManager.HandleDeepLinkAsync` 将真实 FOUND UDL 交给带来源标识的私有 `AcceptPayloadCore`；原两参数入口保持不变，GCD、GM 和 URL 解析不能借此放开重复受理。`CanUdlReplacePendingGcdReward` 要求当前 `_pending` 非空、来自普通 GCD 或 iOS 安装恢复，且新旧动作均为奖励。
- 条件满足时，第一次有效 UDL 即使奖励 ID 相同，也完整替换请求中的数量、动作、Skin Id、Uid 和 URL，清除 GCD/安装恢复来源，记录 `stage=gcd_reward_replaced, source=udl`；仍等待原首页条件后执行。随后同 ID 的重复 UDL 不再享有接管例外。日志不输出 Uid 原值。
- iOS 安装恢复仅在以上未消费奖励条件下放行有效 FOUND 奖励；进关、ERROR、NOT_FOUND、无效参数以及已消费恢复请求仍走原门禁。普通 GCD 的已消费 deferred 回调门禁、关内丢弃、新 GM/新点击及原生请求标识隔离均保留。
- `ExecuteReward` 与 GameData 完全未改；背包发放、媒体奖励打点、一次保存、已领取 ID、防保存失败重发及 CommonGetRewardPopup 纯展示逻辑沿用原实现。消费会先同步清空 `_pending`，因此背包事件里的 UDL 重入也不能再接管本次奖励。合法协议但账号不符/皮肤不存在等情况，仍由执行层拒绝，不增加自动回退。

### 验证、风险与回滚

- 隔离证据在 `C:/Users/admin/AppData/Local/Temp/brainpunk-onelink-gcd-reward-handoff-20260930`。修改前 `baseline-editor.log` 明确失败于“发奖前同 ID UDL 数量覆盖 GCD”；修改后同一场景收到 200，正确用户替换场景收到 100 且弹窗一次。旧审查目录及失败证据保留，没有覆盖。
- `Verify.ps1` 执行当前生产 OneLink 源码；新增 `GcdRewardHandoffTests.cs` 在 Editor、Android 有/无 DEBUG_MODE 各通过 82 次断言，iOS 有/无 DEBUG_MODE 各通过 155 次，合计 556 次。连同原奖励/GM/系统 URL/展示/SRN 回归，本轮共 1929 次断言执行（含平台重复）；前轮审查的缺陷断言只在本轮隔离副本改为正确行为预期，没有把仍复现缺陷当作修复通过。
- 覆盖普通字段与 SRN 目标、同 ID 的数量/动作/皮肤/用户/URL 替换、无效/重复 UDL、身份等待、既有领取记录、保存失败、三玩法进关丢弃、返回首页不复活旧目标、GM/新链接优先、旧自动请求标识隔离、背包同步事件重入；iOS 另覆盖两类 GCD 进入 ERROR 无 URL 恢复后的同矩阵及非奖励回调不放行。替身用于 SDK/Unity UI/存储，部分下游仍使用既有提取代码。
- Unity 6000.0.59f2 Roslyn 与当前工程引用定向编译：Editor 0 error / 113 warning，Android 0 error / 116 warning；`git diff --check` 通过。未逐项归因所有警告，未执行完整 iOS/IL2CPP 构建。
- `uncertain`：真实原生 UDL/GCD 顺序、真机首页遮挡及奖励动画、真实存档 I/O 仍未验证。真机复测重点看 `gcd_reward_replaced -> reward_completed -> reward_popup_opened`，同 ID 重复回调只能出现防重日志；测试时使用未领取的标识或隔离测试存档，不能删玩家现有历史。
- 回滚仅撤销本轮 Manager 的来源识别、待执行奖励接管与日志增量，同步标记本节及当日记录状态；不回退 SRN 映射、奖励弹窗、GM 功能或玩家奖励存档。没有增加字段、公共 API、资源或事件，未修改既有注释。尚未提交 Git。

## 2026-10-09：DeepLink 专用胜利面板与最低未通过关卡续玩

### 需求与实现边界

- DeepLink 进入主线、Draw、IWanner 后，胜利统一打开 `GameVictoryPanel_DeepLink`；普通入口、首通奖励面板和原分享流程保持原样。
- 专用面板移除继承的 `GameVictoryPanel`，绑定独立脚本；主页、重玩、下一关使用现有控制器入口。脚本不持有或修改 `tips_txt`，不绑定 share/NoAd/奖励功能。
- Next 目标为当前玩法按有效配置顺序最靠前的可玩未通过关卡：主线复用最低未通过查询，Draw 复用保留旧 DLevel 至遮罩完成的胜利转场，IWanner 查找可进入 Group 内未记录成功的小关并复用黑幕重建。
- Replay 保留本轮 DeepLink 上下文；Next 接受或 Home 恢复后清理。IWanner 结算仍以 Group 为单位 Replay，但专用面板固定显示三星，且不写入星级存档。
- 专用面板的 Home、Replay、Next 离开路径均使用 `DestroyUI` 移除实例；Replay 后再次胜利会重新创建面板，不复用上一次关闭实例。该调整不改变三玩法现有星星动画／静态展示策略。

### 验证与限制

- Editor/Android Unity Bee 引用定向编译分别为 `0 error / 113 warning`、`0 error / 116 warning`；Unity PrefabImporter 成功导入专用 Prefab，新增 YAML anchor 和继承 fileID 静态检查通过，`git diff --check` 通过。
- `uncertain`：未执行 PlayMode、Android/iOS 真机 DeepLink 胜利、按钮、星星和横屏表现；iOS 定向编译仍被工程现有 `Guru.UniKit.Apple` 引用缺失阻断；当前 Unity Editor 日志另有任务外 GameGuide Generated 重复字段／旧路径缺失错误，尚不能声明全工程脚本重载通过。
- 完整文件、数据/事件流、风险与回滚见 `Assets/Game/UI/GameVictory/GameVictoryPanel_DeepLink功能与风险记录.md`。

## 2026-10-09：GM 进关测试绕过每玩法一次历史（本节为当前 GM 规则）

### 修改前核对

- 用户明确要求 GM 工具跳过本次定位出的进关历史门禁。该例外覆盖 GM 参数、输入长/短 URL、主动请求系统 URL；正式 UDL、Facebook、GCD、系统唤起仍保留每玩法一次规则。旧章节“GM 成功仍消耗一次历史”是历史基线，不再作为当前 GM 规则。
- 原证据：`OneLinkManager.TryBeginEntry` 检查 `DB.GameData.OneinlinkEnterType`，`ConfirmLevelEntered` 在加载完成后写入历史。Editor.log 已记录 `ILevel_032` 成功后，`IGroup_3` 排队但被 `entry_duplicate` 拒绝；不是 GM 按钮或 Group 3 配置缺失。
- 受影响入口：`GMOneLinkTestPopup → OneLinkManager.Debug → 参数/URL 受理 → OneLinkLevelRouter → TryBeginEntry → 现有加载回调 ConfirmLevelEntered`。真实新系统链接会重置本轮来源；短链回调必须关联请求标识，旧回调不得给正式请求附加 GM 权限。
- 拟议最小变更：仅游戏侧 OneLink Manager/Debug/Url、Request、Router；把仅代码可赋值的 GM 来源绑定到请求，并在进关预留时固定本轮例外。GM 成功仍设置运行时 DeepLink 来源以测试专用胜利/Replay，但不新增、清除或迁移永久玩法历史；关卡解锁、进度等既有存档副作用仍存在，不是沙盒。
- 保留首页/弹窗/关内/并发/SDK/网络/配置校验、奖励账号与领取防重；不改 GameData 生成文件、GM Prefab、共享玩法/统计/RemoteConfig、Packages、GuruSDK 或 GF。旧注释中“所有 GM 经过历史门禁”和“所有成功均写历史”将按新例外局部纠正，不改无关注释。
- 验证计划：三个玩法已有/未有历史、重复测试、加载失败和普通入口清理、正式链接与晚到回调隔离、GM 短链/手动系统 URL 的 ERROR 恢复与奖励防重；生产源码替身断言和 Unity 引用定向编译分别记录。CodeGraph CLI 不可用，使用源码/日志取证；引用的 037 feature plan 本地缺失。真机/PlayMode 尚未验证，结论保持 `uncertain`。

### 最终行为与调用链

- `OneLinkRequest.BypassEntryHistory` 不从 payload/query 读取。GM 参数和长 URL 在 `SubmitTestPayload` 授权；短 URL 只在匹配本次请求标识后授权。`RequestSystemUrl` 单独标记当前手动轮次，其关联结果、ERROR 一次恢复、本地长 URL 回退和当前轮次晚到结果沿用；新真实系统 URL 或新测试重置轮次。未关联的真实 UDL/FB/GCD 不获取 GM 权限，即使覆盖了尚未消费的 GM 请求。
- Router 将请求标记传入 `TryBeginEntry`，固定到 `_entryBypassesHistory`；GM 仍拒绝缺 GameData、并发加载和其它玩法业务条件。`ConfirmLevelEntered` 先设置原运行时来源，GM 分支输出 `entry_completed, source=gm, historyRecorded=False` 并返回，不追加历史，也不为该历史调用 `SaveGameData`。正式来源保留原保存及失败日志。
- 异步加载完成读取预留来源，不读取可能已变化的当前 SDK 回调标记。失败/退出及普通新进关清理本轮例外；Reset/Replay/组内推进沿用原来源生命周期。没有新增持久化字段、资源、UI、全局事件或打点；GM 仍可能按原路由解锁目标、保存关卡进度或按奖励规则发奖。
- 直接修改为 `OneLinkManager.cs`、`OneLinkManager.Debug.cs`、`OneLinkManager.Url.cs`、`OneLinkRequest.cs`、`OneLinkLevelRouter.cs`，文档同步本节、`DevelopMD/GMPanel_Reference.md` 和当日共享记录。对应源码定位可搜索 `BypassEntryHistory`、`_entryBypassesHistory`、`_gmSystemUrlActivation`。

### 验证、剩余风险与回滚

- 证据目录：`C:/Users/admin/AppData/Local/Temp/brainpunk-onelink-gm-history-bypass-20261009/`。`Verify.ps1` 执行当前 Manager/Request 生产源码，Editor 356、Android 有/无 DEBUG_MODE 各 397、iOS 有/无 DEBUG_MODE 各 392 次断言通过（1934 次，含平台重复）；SDK、Unity、DB、背包及此层 Router 使用替身。
- `VerifyRouter.ps1` 另执行实际 Manager、Request、Router、IWannerGameLevelCtrl 与当前本地配置，五组宏各 689 次断言通过（3445 次）。确认已有 IWanner 历史时，GM `IGroup_3`/`ILevel_012` 进入目标 Group/索引且可重复，正式 Router 仍拒绝已有历史，玩法关闭/缺存档/未就绪不放行。加载器/存储/UI 为替身；四个提取的进组/续关/门禁/进度方法与当前 `IWannerPlayCtrl` 的方法体去空白逐项相等，真实异步资源加载未模拟。
- 两层合计 5379 次断言执行，不代表 5379 个独立场景。覆盖三个玩法空/已有历史、成功/失败/重复、DeepLink 来源、正式链接覆盖与旧标识隔离、GM 短 URL、手动系统 URL 的成功/ERROR/晚到/本地回退、新真实点击，以及奖励防重/Uid 等既有回归。最初新增路由用例因替身旧存档列表未初始化而失败，已按实际启动初始化顺序修正测试夹具后重跑；未为此改生产逻辑。
- `Compile.ps1 -Mode Editor/Android` 使用当前 Unity 6000.0.59f2 Bee 引用定向编译，分别 `0 error / 113 warning`、`0 error / 116 warning`，两者源文件缺失数均为 0；产物写 TEMP，不是完整 Unity/IL2CPP 构建。iOS 定向编译失败于任务外 `ChoiceLevelPanel.cs:17`、`StartPanel.cs:14` 缺少 `Guru.UniKit.Apple`，2 error；未修改只读 SDK。目标文件 `git diff --check` 通过。
- `uncertain`：未执行 Unity PlayMode、实际 Android/iOS SDK 标识回传、横屏输入、真实资源加载/存档、胜利 Replay 或 IL2CPP。手动系统 URL 的旁路只对已关联本次 GM 请求的结果生效；无标识真实 UDL 不自动取得 GM 权限，真机须核对 `manual_url_resolved → entry_completed, source=gm`。iOS 普通输入短链仍按原平台规则不支持，本次未扩大其平台能力。
- 手动验收：正常首页打开 GM OneLink，sub1=`iwanner`、sub2=`IGroup_3`，成功后回首页再测 `ILevel_012` 和另一组；应无历史重复拒绝，成功日志应包含 `historyRecorded=False`。已有正式历史无需删除；真正外部链接在同存档下仍应受原历史限制。GM 改关卡进度是既有行为，不能用生产玩家存档验证无损。
- 回滚只撤销本次 GM 来源、进关预留与历史写入例外；不清理已有玩法历史或玩家进度，不回退本次开工前已存在的 DeepLink 胜利面板、文档、字体、平台配置等改动。当前分支为 `feature/1.15.1_common`；实现交付时未提交，后续用户已授权仅提交本轮相关修改到本地，不推送。
