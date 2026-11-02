# GuideFlow 集中管理功能说明

## 需求与背景

- 现有 `GameTipsGuideFlow`、`PostcardGuideFlow`、`PostcardGuideFlow2` 分别由不同宿主持有，原先只有单个 Flow 实例自身的防重入与取消能力，没有跨 Flow 的集中互斥。
- 新的服饰首次引导会跨越 Gameplay 与 Shop UI。为避免多个 Guide 同时创建手指遮罩、争用输入或修改页面状态，需要为所有继承 `GuideFlowBase` 的流程提供统一的串行执行边界。
- 本次只建立集中管理基础设施，不实现 `Shop2SkinGuideFlow`，也不把 `PlayCtrl` 的 Playing/Pause 状态切换放进通用管理器。

## 影响文件与系统

- `GuideFlowManager.cs`：新增进程内单例管理器，维护一个活动 Flow 与 FIFO 等待队列。
- `GuideFlowBase.cs`：`RunAsync(...)` 在执行子类逻辑前自动申请管理器执行权，在同步收尾后自动释放。
- 未修改现有 `GameTipsGuideFlow`、`PostcardGuideFlow`、`PostcardGuideFlow2` 及其宿主业务代码。
- 不新增 `PlayEvents`，不访问 DB、RemoteConfig、资源系统或 UI，不创建 Unity 场景对象。

## 修改前行为基线

- `GuideFlowBase` 只防止同一实例重复运行，不阻止不同 Flow 实例同时执行。
- `PostcardGuideFlow` 在自身尚未结束时，通过 `PostcardPopup.StartPendingPostcardGuide()` 启动 `PostcardGuideFlow2.RunAsync()`；这是现有两段流程的交接方式。
- Gameplay 暂停、胜利页交互锁和 Popup 关闭等状态仍分别由各业务宿主负责。

## 修改后行为

1. 任意 `GuideFlowBase.RunAsync(...)` 启动时自动向 `GuideFlowManager` 申请执行权。
2. 没有活动 Flow 时立即进入；已有活动 Flow 时按 FIFO 顺序等待。
3. 等待中的 Flow 仍处于 `IsRunning == true`，可通过原有 `Cancel()` 或外部取消令牌移出队列并返回 `Cancelled`。
4. Flow 正常完成、业务失败、取消或抛出异常时，基类先清理自身取消源并调用 `OnFlowStopped()`，然后释放管理器执行权。
5. 管理器释放当前 Flow 后唤醒下一个等待项，因此现有 Postcard Flow1 可以先把 Flow2 加入队列，再在自身退出后完成交接。
6. `Contains<T>()` 只提供活动项和有效等待项的只读防重复查询，不能替代具体 Flow 的业务资格判断。

## 状态与生命周期边界

- 管理器仅在 Unity 主线程使用，不提供跨线程同步保证。
- 管理器不统一切换 `PlayCtrl` 状态。Gameplay 类引导应由其宿主在取得业务启动资格后进入 Pause；胜利页、明信片页等流程继续保留自身现有状态策略。
- 管理器不持久化；场景或页面销毁时，仍由 Flow 宿主调用 `Cancel()` 或传入生命周期取消令牌。
- Android/iOS 横屏没有新增布局或平台分支，实际触摸交接仍需 PlayMode/真机验证。

## 数据、资源与事件流

- 数据与存档：无新增读写。
- RemoteConfig：无访问。
- UI 与资源：无创建、销毁或加载。
- 事件：无新增全局事件。
- 日志：通过 `Bear.Logger` 记录进入、排队、退出和异常释放请求，便于定位 Flow 卡队列问题。

## 验证记录

- 2026-07-21：临时将新脚本加入 Unity 生成的 `Assembly-CSharp.csproj` 后执行 `dotnet build`，结果为 `0 error / 101 warning`；临时工程文件改动随后已移除。
- `git diff --check` 检查通过，没有新增空白错误。
- 静态敏感项检查未发现 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`PlayEvents`、DB、Save、RemoteConfig 或 Instantiate 调用。
- CodeGraph 确认三个现有 `RunAsync` 宿主都会经由新管理边界，并确认 Postcard Flow1 到 Flow2 的启动发生在 Flow1 返回前。
- 未发现覆盖 GuideFlow 的自动化测试。

## 未验证风险

- 当前 Unity Editor 正在占用工程，本次未另启批处理 Unity；尚未完成 PlayMode 下的真实点击、取消、页面销毁和连续排队验证。
- 若某个宿主既不结束 Flow、也不在销毁时取消，其后的 Flow 会持续等待；管理器日志可定位当前占用者，但不会擅自超时终止业务流程。
- Gameplay 引导的 Pause/恢复策略尚未由本次基础设施实现，需要在 `Shop2SkinGuideFlow` 宿主接入时验证。
- Android/iOS 真机触摸和横屏安全区未验证。

## 回滚方式

1. 删除 `GuideFlowManager.cs` 及其 `.meta`。
2. 从 `GuideFlowBase.RunAsync(...)` 移除 `EnterAsync(...)`、`Exit(...)` 与 `hasEnteredManager`，恢复原有单实例取消和收尾逻辑。
3. 不需要回滚任何 Flow 业务脚本、Prefab、存档或配置。
