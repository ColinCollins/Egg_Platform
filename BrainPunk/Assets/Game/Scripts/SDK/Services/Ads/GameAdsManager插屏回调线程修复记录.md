# GameAdsManager 插屏回调线程修复记录

日期：2026-10-10（Asia/Shanghai）

## 修改前核对

- 需求与验收对象：用户批准修复 Android IWanner 首通奖励面板加载崩溃，并要求明确主线、DrawGame 的影响。目标是 SDK 异步插屏结果交付到游戏逻辑前恢复 Unity 主线程；不是更换纹理、改变广告资格或修改结算资源。
- 崩溃事实：用户补充的完整堆栈包含 `NativeTransport.Looper -> ShowInterstitialAd -> ContinueWith -> InterstitialAdHelper.InternalCallback -> WaitForInterstitialAsync -> HandleGroupCompletionAsync -> IWannerGame_GameVictoryPanel_Reward.Create -> Resources.Load -> Texture2D.UploadTexture -> GetGfxDevice`。`libunity.so` Build ID 为 `d02c5407896f5a43`，`libil2cpp.so` 为 `0f8064a30dbe4589754c53537a1630af0dd78e30`。日志没有 SDK 具体返回值，不能把此次认定为广告成功关闭，也不能由一个问题推断全部崩溃的来源。
- 原流程与来源：`GameAdsManager.ShowInterstitialAd` 在 `ContinueWith(OnResult)` 内同步调用用户回调；`InterstitialAdHelper` 捕获请求玩法及其策略，成功时更新该玩法存档和标记，再完成业务等待。`UniTaskCompletionSource.TrySignalCompletion` 直接调用已注册 continuation，不自动回主线程。
- SDK 维护边界：只读核对 `NativeTransport.StartLooper/Looper`、`AndroidNativeGate.Invoke<T>`、`FusionAdsAndroidApi.ShowInterstitialAd`、`AdsController.ShowInterstitialAd`、`AdsCompleter`。原生请求在后台线程完成；正常完成广告的 `AdsCompleter` 有主线程切换，但请求返回失败等分支可绕过它。Packages、GuruSDK 及其真实目标、Assets/GF 均不得修改。
- 主线入口：`PlayCtrl_Success.RunVictoryProcessAsync` 请求胜利插屏但不等待结果再创建 Victory；`PlayCtrl.ResetGame` 的 `OnInterstitialCallback` 为空，重建沿原有 PlayerLoop 等待独立推进。仍共享成功策略写盘与免广告待消费标记，不能宣称无影响。
- DrawGame 入口：`DrawPlayCtrl.OnLevelResolved` 请求胜利插屏后独立启动结算；失败重建则在新 DLevel/HUD 全黑状态下等待 `WaitForFailureInterstitialAsync`，随后验证活动版本、淡入并恢复输入。后台结果可把其等待后代码带到错误线程，所以共享边界修复也覆盖此路径。
- IWanner 入口：完整 Group 胜利先锁定 Level/HUD、暂停并记录通关，再等待插屏；活动版本有效时直接创建首通 Reward 或复通 Victory。Reset 也等待相同方法，广告覆盖期间保留黑幕和输入锁。旧实例晚到回调仍由既有活动版本校验处理。
- 拟修改位置与理由：只把游戏侧 `GameAdsManager.ShowInterstitialAd` 的局部结果处理改为异步 `UniTask`，首先 `await UniTask.SwitchToMainThread()`，然后原样执行成功/失败映射、日志与回调；异步链使用现有 `Forget()` 消费。不改 SDK、Helper、控制器、资源、配置、广告 placement、激励广告或关卡打点生命周期。
- 必须保留：主线/Draw/IWanner 独立 Policy/存档、请求发起方捕获、Helper 重复回调门禁、未初始化同步失败返回、同步策略拒绝、成功/失败布尔语义，以及各玩法原有结算、黑幕和输入恢复顺序。
- 有意时序差异：已在主线程的结果继续立即交付；后台结果排到 Unity PlayerLoop 的 Update 后交付，原生覆盖期间主线程暂停时保持等待。不能承诺后台结果零延迟，亦不增加超时、轮询或原生覆盖期间强行推进 Tween。
- 验证计划：对实际结果处理与调用链执行 Editor 主程序集编译、隔离后台/主线程成功失败模拟、同步未初始化返回、空回调、重复结果防重及退出/切换后晚到结果；静态比较主线/Draw/IWanner 消费者和非目标代码。真机补测三玩法胜利与失败插屏、IWanner 首通/复通、Draw 黑幕恢复、免广告提示消费、退后台及横屏。
- 已知环境缺口：AGENTS 指定的 `../specs/037-remote-resource-loading/plan.md` 不存在；项目有 `.codegraph`，但本会话没有 CodeGraph MCP，CLI 不可用。以当前源码、原始堆栈、专项文档及 Git 历史补足本次局部依据，不触碰远端资源或配置流程。
- 任务开始已有改动：`Assets/Plugins/Android/mainTemplate.gradle`、`Assets/StreamingAssets/build_info`、`ProjectSettings/AndroidResolverDependencies.xml`、`ProjectSettings/guru_gf_settings.txt`，以及未跟踪 `Assets/Guru/Gen/GuruAllSpecs.G.cs.old`。本任务不混入、不覆盖这些文件。

## 修改与验证状态

- 已实施：只修改 `GameAdsManager.cs:123–140` 的局部结果函数与异步链；SDK 异步结果首先切回 Unity 主线程，再交付原有成功/失败映射。`IsAdsInitialized == false` 仍同步返回失败。没有修改 Helper、主线/Draw/IWanner 控制器、Prefab、广告资格、配置、存档结构、打点或激励广告。
- 因果链证据：`IWannerPlayCtrl.cs:1707` 等待插屏后，`:1709–1714` 校验活动版本，`:1725` 创建首通面板；`PlayCtrl_Success.cs:48/70`、`DrawPlayCtrl.cs:1165–1167/1222` 的胜利面板沿独立流程创建；Draw 失败重建仍在 `DrawPlayCtrl.cs:1822/1842–1848` 等待回调。这是路径差异，不是其它玩法绝对安全的证据。
- 独立源码编译：使用 Unity 6000.0.59f2 自带 Roslyn 和当前 Editor Bee 引用，输出只放 TEMP。Editor 主程序集 0 error / 113 warning；移除 Editor 条件的 Android 主程序集 0 error / 116 warning。目标文件只有沿用旧 SDK API 的 CS0618 警告，不以本轮修复为由迁移 SDK API。该检查不等于 Unity 导入、APK 构建或 IL2CPP 编译。
- 隔离模拟：提取实际修改前/后展示方法、完整 Helper、IWanner/Draw 等待方法、IWanner 活动版本 guard、UniTask 的 `ContinueWith` 和切主线程 awaitable；Task/builder、PlayerLoop、SDK、策略写盘及 UI 副作用均使用隔离桩。153 项断言通过；修改前成功/失败两种结果均复现 IWanner 等待后在工作线程执行。修改后覆盖三玩法×主/后台×成功/失败、后台等待主队列、主线程立即返回、发起策略/标记隔离、重复回调、四种 IWanner 失效状态、未初始化与策略拒绝同步返回、空回调及 placement/validator 透传。不能用这些桩推断真实 Unity 或原生 SDK 已通过。
- 差异验证：移除本轮局部 async、切线程与 `Forget()` 后，`GameAdsManager.cs` 全文与修改前 HEAD 一致；其它游戏代码未修改。未写 Packages、GuruSDK 或 GF；Git 提交以仓库历史为准，不推送。
- 证据目录：`C:/Users/admin/AppData/Local/Temp/brainpunk-interstitial-main-thread-20261010/`，包括 `Compile.ps1`、`compile-Editor.log`、`compile-Android.log`、`Verify.ps1` 和 `verify.log`。首轮隔离桩编译因未开启 nullable 注解上下文失败，仅修正测试上下文后重跑；未据此修改生产逻辑。
- 工作区保护：续作期间另观察到字体资源、`Level_073.prefab`、`ProjectSettings.asset`、当日其它文档等外部改动，均未纳入本修复；不把它们认定为本任务产物或回滚对象。
- 后续验证偏好：用户要求同类局部修复不再额外编写新的 `Verify.ps1`；复用已有编译、测试和差异检查，未实测范围仍明确记录。偏好来源见 `DevelopMD/AI规则提升队列.md` 的 BP-AI-039，本次已有证据保留。

## 真机验收清单（尚未执行）

- IWanner 首通/复通：插屏成功关闭、请求失败或无广告后，仅显示对应结算一次；失败 Reset 全黑等待后正常恢复；退后台或退出后晚到结果不重建旧面板。
- DrawGame：胜利结算仍按原流程；失败 Reset 在广告回调和活动版本校验后再淡入恢复，成功/失败均不黑屏或误开输入。
- 主线：胜利/失败结算与 Reset 正常；三玩法的成功策略计数、存档及免广告提示标记仍归发起玩法，不串档。
- Android/iOS 横屏与前后台：真实原生覆盖暂停期间不提前揭开或恢复输入；主线程恢复后回调交付。Android 同类崩溃是否消失须新包、同场景复测及完整日志确认；iOS 仅共享代码受影响，尚无平台通过证据。

## 剩余风险与回滚

- `uncertain`：未执行 Unity PlayMode、Android/iOS 真机、真实广告 SDK、IL2CPP 或内存峰值验证；缺失原生错误文本，线程错误之外的引擎/资源问题不能据此全部排除。
- 共享回调首次回到 Update 的时机可能影响主线/Draw 免广告标记与消费者的先后顺序，须真机核对；本次不改变其显示策略来规避尚未复现的时序问题。
- SDK 若不返回结果，既有等待仍不结束；本次不引入无依据的超时方案。激励广告同类线程风险不属于本次插屏修复范围。
- 回滚仅撤回 `GameAdsManager.ShowInterstitialAd` 局部结果函数的异步/切线程改动及对应 `Forget()`，保留其它用户修改、广告策略、存档与资源；回滚后后台结果风险会恢复。
