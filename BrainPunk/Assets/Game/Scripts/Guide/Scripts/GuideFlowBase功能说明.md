# GuideFlowBase 功能说明

## 需求背景

为后续不同业务引导提供最小公共流程骨架，先统一防重入、取消和结束清理边界。本次只创建基类，不修改或接入现有 Postcard 引导逻辑。

## 影响文件与系统

- `Assets/Game/Scripts/Guide/GuideFlowBase.cs`
  - 新增 `GuideFlowResult` 执行结果。
  - 新增普通 C# 抽象类 `GuideFlowBase`。
- 未修改 UI prefab、DB、配置、资源地址、全局事件、分析打点或平台 SDK。
- 未新增 Bear UI、事件、存档或资源系统依赖。

## 原有行为基线

项目原先没有公共 Guide Flow 基类。Postcard 引导继续由现有 `GameVictoryPanel`、`NormalGuidePopup`、`PostcardPopup`、`Postcard_GuidePart` 和 `PostcardPopup_PageCompleteProcess` 编排。

## 当前行为

- `RunAsync(...)` 限制同一实例只运行一次；重复启动返回 `AlreadyRunning`。
- 外部取消令牌会与基类自己的 `Cancel()` 合并。
- 已确认的取消统一转换为 `Cancelled`；其他异常继续向调用方传播。
- `ExecuteAsync(...)` 由子类实现，要求所有异步等待沿用基类传入的取消令牌。
- `OnFlowStopped()` 仅作为同步解绑和状态恢复入口。
- 当前 `PostcardGuideFlow2` 已由 `PostcardPopup` 构造运行；基类为该页面内流程统一防重入、取消和同步收尾，`PostcardGuideFlow` 的接入状态见其独立功能说明。
- `GuideFinger.WaitForClickAsync(...)` 与 `NormalGuidePopup.ShowAsync(...)` 已由 Flow2 复用，并沿用 `ExecuteAsync(...)` 收到的取消令牌。

## 数据、资源、UI 与事件流

- 数据与存档：无读写。
- 资源与 UI：无加载、实例化或界面操作。
- 事件：无全局事件或订阅。
- 平台：无 Android/iOS 分支；仅使用项目已有 UniTask 与 .NET 取消令牌。

## 验证记录

- `git diff --check` 已通过；仅有共享中文记录文件的既有 LF/CRLF 转换提示。
- 敏感项静态扫描未发现 `MonoBehaviour`、Unity API、`async void`、DB/save、资源加载、全局事件、编辑器生命周期或每帧入口。
- Unity 生成的 `Assembly-CSharp.csproj` 尚未包含新脚本，因此没有把既有工程构建结果当作本文件的编译证据。
- 使用本机 .NET Roslyn 10.0.101、项目现有 `Library/ScriptAssemblies/UniTask.dll` 和 .NET 参考程序集对 `GuideFlowBase.cs` 做独立编译，结果为 0 error，退出码 0；临时 DLL 已从系统临时目录删除。
- 两个表现层 Async API 的目标 diff 与敏感项静态扫描通过，没有新增 `async void`。Unity 已在脚本修改后重新生成 `Assembly-CSharp.dll/.pdb`，且二进制可检出两个新异步状态机；命令行构建和独立 Roslyn 检查仍分别受 `Temp` 元数据缺失与项目新旧 Unity 程序集依赖冲突阻断。

## 剩余风险与未验证项

- Flow2 已有运行时接入，但 Popup 生命周期取消、UI 销毁、流程交接和重复启动表现仍未经过 PlayMode 验证。
- 需要在首个具体 Guide Flow 接入时补充 EditMode/PlayMode 测试。
- `Assembly-CSharp.csproj` 工程级编译已通过；Unity Editor 目标程序集已在 Flow1/Flow2 新脚本纳入后完成重建。
- 本类按 Unity 主线程使用设计，不提供跨线程并发安全保证。

## 回滚路径

删除 `Assets/Game/UI/Guide/Scripts/Flow` 下对应脚本及 meta，并移除 `DevelopMD/已有功能设计与使用记录.md` 中的同主题记录即可；当前没有调用方或数据迁移。
