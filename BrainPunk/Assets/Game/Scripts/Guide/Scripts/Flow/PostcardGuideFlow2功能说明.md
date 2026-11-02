# PostcardGuideFlow2 功能说明

## 2026-07-18 合并后恢复 Popup 持有与生命周期闭环

- 最新 develop 合并曾删除 `PostcardPopup._postcardGuideFlow2` 及其启动、取消、交互锁和引导翻页适配，但页面完成 partial 仍保留 `NotifyPageComplete()` / `NotifyFailed()` 调用，导致结构断链和编译失败。
- 当前由 `PostcardPopup.StartPendingPostcardGuide()` 唯一构造并启动 Flow2；重复启动复用正在运行的实例，Popup `OnOpen()` / `OnClose()` 主动取消旧实例，销毁令牌继续传入 `RunAsync(...)`。
- GuidePart 完成回调只发送局部完成信号；页面完成 `finally` 发送页面完成信号；Flow2 完成、失败、取消或异常均通过 `OnFlowStopped()` 恢复 Popup 交互锁。
- 引导切页继续复用 Popup 原页面加载和 `CurrentId` 保存链，只有 Flow2 注入的 `TryGoNextPageForGuide()` 可以在 `_isGuiding=true` 时通过；玩家 Close/Last/Next 入口仍被引导锁拒绝。
- develop 的页面动画 ClickBlock 与 2 秒完成粒子等待保持不变；ClickBlock 使用 `_isGuiding || IsPagePlayingAnim`，任一状态结束不会提前释放另一个状态的遮罩。
- C# 工程构建通过：0 error / 99 warnings；CodeGraph 已确认持有和通知链。Unity PlayMode、快速关闭/复用、奖励失败、切页失败、Stage Three/Four 和 Android/iOS 横屏触摸尚未验证。
- 回滚需同时移除页面完成 partial 的 Flow2 通知或恢复完整旧回调链，不能只删除 Popup 字段；不涉及 DB schema、配置或资源迁移。

## 需求背景

明信片首次引导在 Stage Two 后还包含 `Postcard_GuidePart` 拼图目标、页面完成与奖励、Stage Three、下一页 Finger 和 Stage Four。`PostcardGuideFlow2` 负责集中这部分编排，并由 `PostcardPopup` 正式持有运行；Popup 只通过局部委托和 Notify API 交接既有生命周期，不复制数据或业务实现。

## 影响文件与系统

- `Assets/Game/UI/Guide/Scripts/Flow/PostcardGuideFlow2.cs`
  - `PostcardGuideFlow2 : GuideFlowBase` 完整编排 Stage Three、下一页 Finger、切页和 Stage Four。
  - Flow2 公开 `NotifyGuidePartCompleted()`、`NotifyPageComplete()` 与 `NotifyFailed()`，由 Popup 既有生命周期节点发送局部信号。
  - 构造参数直接注入下一页目标、Finger 根节点、GuidePart 启动、引导切页和本地交互锁，不再要求 Popup 实现专用接口。
- `Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs`
  - 持有、构造、运行和取消 Flow2，并提供 GuidePart、切页和交互锁局部适配。
- `Assets/Game/UI/Postcard/Scripts/PostcardPopup_PageCompleteProcess.cs`
  - 页面完成 `finally` 发送 `NotifyPageComplete()`；奖励 Finger 失败发送 `NotifyFailed()`；删除旧 Stage Three/Four 回调链。
- 未修改 `Postcard_GuidePart`、prefab、Resources 路径、配置、DB 字段、奖励实现、全局事件、analytics、广告、IAP 或平台 SDK。

## 原有行为基线

- Stage Two 结束后，现有入口调用 `PostcardManager.StartGuding()` 和 `PostcardPopup.StartPendingPostcardGuide()`。
- Popup 消费 `IsGuiding`，启动 `Postcard_GuidePart`，并在页面完成、Chest、奖励链结束后播放 Stage Three。
- Stage Three 关闭后创建下一页 Finger；点击后切页、等待 1.3 秒并播放 Stage Four，最后恢复 Popup 按钮。
- 接入前 Stage Three/Four 编排位于 `PostcardPopup_PageCompleteProcess` 的三个私有回调方法；接入后这些方法已由 Flow2 替换并删除。

## Flow2 编排内容

当前由 `PostcardPopup` 调用 `PostcardGuideFlow2.RunAsync(...)`，按以下顺序执行：

1. 校验 PlayMode 和注入委托，开启 Popup 本地引导交互锁。
2. 在 Flow2 内先建立 GuidePart 与页面完成信号源，再调用注入的 GuidePart 启动委托并传入完成回调；依次等待 `NotifyGuidePartCompleted()` 和 `NotifyPageComplete()`。
3. 沿用原节奏等待 1.5 秒，通过 `NormalGuidePopup.ShowAsync(Stage Three)` 等待第三阶段关闭。
4. 校验 Popup 注入的下一页按钮处于 active 状态及 UI 根节点有效，通过 `GuideFinger.WaitForClickAsync(...)` 等待一次有效点击。
5. 调用注入的引导切页委托复用原切页与保存链。
6. 沿用原节奏等待 1.3 秒，通过 `NormalGuidePopup.ShowAsync(Stage Four)` 等待第四阶段关闭。
7. 无论完成、失败、取消或异常，`OnFlowStopped()` 都会通过局部委托尝试恢复 Popup 普通交互。

Flow2 不复制拼图解锁、扣星、存档、Chest、奖励、页面加载或切页实现，也不增加 `Update` 轮询或全局事件。

## Flow2 扩展 API 与依赖边界

- `NotifyGuidePartCompleted()`
  - 由 Popup 的 GuidePart 完成回调调用，结束 Flow2 内部的拼图等待阶段。
- `NotifyPageComplete()`
  - 由页面完成流程的 finally 收尾节点调用；可早于 GuidePart 信号到达，Flow2 会按顺序消费。
- `NotifyFailed()`
  - 用于 Stage Three 开始前的 GuidePart/奖励降级失败点；进入 Stage Three 后应使用继承的 `Cancel()`。
- `Func<Action, bool> tryStartGuidePart`
  - 只负责同步启动既有 GuidePart；Flow2 传入一次性完成回调，不要求 Popup 包装总 Async API。
- `RectTransform nextPageGuideTarget` / `RectTransform guideRoot`
  - 分别注入当前可引导的下一页按钮和兼容 Canvas 下的 Finger 根节点。
- `Func<bool> tryGoNextPageForGuide`
  - 复用 Popup 原切页、页面刷新和 `CurrentId` 保存逻辑；不得在 Flow2 中复制数据代码。
- `Action<bool> setGuideInteractionLocked`
  - 只切换当前 Popup 的本地关闭/翻页交互，不写 DB、不启动异步任务。

## 数据、资源与生命周期

- Flow1 在创建引导用 `PostcardPopup` 前完成 `PostcardManager.StartGuding()`，让 Popup 首帧读取 pending 状态并开启 ClickBlock；Stage Two 结束后调用 `PostcardPopup.StartPendingPostcardGuide()` 启动 Flow2。
- Flow2 的 GuidePart 启动委托复用 Popup 既有 `ConsumePendingGuideState(...)`，继续消费并保存 `DB.Postcard.IsGuiding`；Flow2 本身不直接访问 DB。
- 所有 Flow2 延迟、Popup 和 Finger 等待沿用 `GuideFlowBase` 提供的同一取消令牌。
- Popup 销毁令牌传给 `RunAsync(...)`，`OnOpen/OnClose` 额外主动调用 `Cancel()`，覆盖 Bear UI 复用和快速关闭边界。
- Flow1 与 Flow2 不互相继承：Flow1 归胜利页生命周期，Flow2 归 PostcardPopup 生命周期。

## 验证记录

- CodeGraph 与静态搜索确认 Popup 只有一个 Flow2 构造入口、一个页面完成通知入口，旧 Stage Three/Four 三个私有方法和旧 Popup 创建链无残留。
- `git diff --check` 通过；目标脚本未新增 editor lifecycle、每帧轮询、全局事件或 `async void`。
- `dotnet build Assembly-CSharp.csproj` 通过，结果为 98 个现有警告、0 个错误。

## 剩余风险与回滚

- GuidePart 完成和页面完成 `finally` 已分别接入两个 Notify API；Flow2 在启动 GuidePart 前建立信号源，避免同步或快速完成时漏接。
- 尚未执行 PlayMode；Stage Three/Four、取消、重复运行、目标缺失、切页失败、Popup 快速关闭和 Android/iOS 横屏均未验证。
- 奖励 Finger、Stage Popup、下一页 Finger 或引导切页失败会结束 Flow2 并恢复交互；当前只完成静态保护，降级观感仍待运行验证。
- 回滚时恢复 Popup 原 Stage Three/Four 回调链并移除 Flow2 字段、构造、通知和取消入口；没有数据、配置、prefab 或资源迁移。
