# BeginerPopup 首次主线进关弹窗

## 2026-09-24 需求与原有行为

- 用户明确选择仅覆盖主线任意关卡，不增加 Draw 或 IWanner 入口。首次进入主线不再要求排序 ID 为 1；已弹出过的同一存档不重复弹出。
- 原触发位于 `PlayCtrl.EnterGame`，仅 sortId == 1 调用，等待 3 秒后读取 `DB.GameData.HasBegineTutorial`；没有关联关卡实例，加载失败或等待期间离开后仍可能触发。
- `BeginerPopup.Create` 使用 Popup 层，显示时通过既有 SwitchGameStateEvent 暂停，关闭时恢复 PLAYING；创建后写入 HasBegineTutorial 并 Save。Prefab 的 closeBtn 已显式绑定，生成绑定和资源无需调整。
- 未找到弹窗既有专项说明；已查阅进关转场、Bear UI 共享遮罩、Postcard 下层 UI 恢复记录以及关卡打点生命周期约定。工程所引用的 feature plan 不存在，本次依据用户需求与实际调用链。

## 修改后行为与影响范围

- 只修改 PlayCtrl 中的弹窗触发与等待，以及 BeginerPopup 的一次性创建保护；不改变关卡加载、通关、复活、打点或存档字段结构。
- 在主线关卡成功创建后安排等待，复用主线普通进关、下一关、重开、Replay 的公共创建路径。已展示过则立即跳过；保留原来的 3 秒延迟。
- 等待绑定本轮关卡对象生命周期；被销毁、替换、退出主线、回到首页或进入成功/失败状态后不再弹出。暂停或其它 Popup/Top/共享遮罩尚未关闭时等待，恢复到前台可游玩状态才弹出。
- 弹窗真正创建成功后才写入原有已展示标记；创建失败不消耗机会。一次性范围是当前存档，清档后可以重新展示；旧存档标记为 true 的玩家继续跳过，false 的玩家下次成功进入主线可补看。
- 数据/UI 流：成功创建主线实例 → 尚未展示 → 延迟与主线状态/UI 门禁 → BeginerPopup.Create → Popup 暂停 → 保存已展示 → 关闭恢复。沿用现有字段、事件、资源与关闭动画，不新增全局事件。

## 验证、风险与回滚

- 源码入口：`PlayCtrl.cs:344` 为等待与门禁，`:672` 的 CreateLevelAsync 成功创建实例和 HUD 后在 `:700` 调用；EnterGame、OnEnterNextLevel、ResetGame、ReplayGame 四条主线创建链均复用此处。OneLink 主线最终经 EnterLevelEvent/EnterGame 到达同一链，未增加独立 SDK 接口。
- 一次性存档：`GameData.cs:135` 原字段默认 false，`GameData_Partial.cs:249` 原属性未变；`BeginerPopup.cs:85` 创建入口先检查，UI 创建成功后保存。旧的“限制第一关才会弹出”注释随对应已废弃条件一起删除，其它旧注释保留。
- Prefab 静态核对：`BeginerPopup.prefab:883–916` 关联现有脚本、closeBtn 与生成绑定；开关动画忽略 TimeScale。BaseLevelCtrl.DestroyLevel 销毁关卡 GameObject，现有 UniTask 销毁令牌可取消待显示任务。未改 Prefab、生成文件、关卡脚本、事件定义、资源或 Packages/GF。
- TEMP 模拟通过 56 项断言，覆盖第一关、Level_022、Level_999 的首次展示与三秒延迟、暂停/关闭恢复、重复 Create、重复等待、已展示存档、关卡加载失败、实例销毁/替换、START/SUCCESS/FAILED、主线停用、其它弹窗和暂停等待、前后台、弹窗资源加载失败后再进关补看，以及编辑态禁止创建与写存档。
- 模拟直接使用生产 BeginerPopup.cs，逐字提取 PlayCtrl 的 ShowBeginerPopup/CreateLevelAsync 控制流；TEMP 将后者返回的 UniTask<bool> 适配为 Task<bool>，使用可推进时钟、关卡/UI/存档替身。它验证等待与一次性标记，不是完整 Unity 状态机或真实磁盘持久化测试。证据：`C:/Users/admin/AppData/Local/Temp/brainpunk-beginer-20260924/{Harness.cs,Extracted.cs,beginer-tests.log}`；命令 `dotnet run --project BeginerTests.csproj`。
- 完整当前游戏脚本复用 Unity Bee 引用的 Roslyn 定向编译通过：Editor 0 错误/105 警告，Android 无 DEBUG_MODE 0 错误/108 警告；本次没有引入新的编译错误。输出只写 TEMP，日志为同目录的 `editor-compile.log` / `android-compile.log`。代码、文档及 meta 的定向 `git diff --check` 通过。
- `uncertain`：尚未进入 Unity PlayMode 或打包真机验证。需补验新存档 OneLink 直达主线 Level_022 的转场/公告/新手弹窗顺序、Close 后触控恢复、应用重启后不重复，以及 Android/iOS 横屏效果。共享 UI/关卡打点逻辑未改，不把本次模拟当作原生广告覆盖、真实资源加载或全部关卡的完整回归。
- 非主线玩法不增加 BeginerPopup。首轮关卡若在弹出前已结束，本次不补弹到结算页面，未展示标记仍保留给后续主线进关。
- 回滚仅撤销本轮 PlayCtrl/BeginerPopup 改动；无需迁移存档或资源，已经写入的 HasBegineTutorial 不自动清除。
