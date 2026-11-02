# NormalGuidePopup 加固记录

## 需求背景

2026-07-07 针对 `NormalGuidePopup` 做运行时稳定性加固。该弹窗当前由 `GMPanel.ShowPostcardGuide()` 调用，负责播放明信片引导文案，结束后打开 `PostcardPopup`，并写入 `DB.Postcard.HasGuide` / `DB.Postcard.IsGuiding`。

## 影响文件与系统

- `Assets/Game/UI/Postcard/Scripts/NormalGuidePopup/NormalGuidePopup.cs`
  - 加固引导步骤推进、按钮绑定、DOTween 生命周期、打开/关闭存档标记。
- `Assets/Game/UI/Postcard/Resources/NormalGuidePopup.prefab`
  - 本次未修改；静态证据显示 `tipsKey` 当前绑定 3 条 key，按钮、入口节点和 `ContentTxt` 已序列化到脚本字段。
- 相关系统：
  - `Bear.UI.UIManager`：`OpenUI` 可能复用已有 UI 实例，`DestroyUI` 会进入 `OnDestroyView()` / `OnClose()`。
  - `DB.Postcard`：仅继续使用已有 `HasGuide` / `IsGuiding` 字段，不新增存档字段。
  - `PostcardPopup.Create()`：引导完成后仍打开明信片主界面。

## 原有行为基线

- `OnOpen()` 每次直接 `_step = 0` 后绑定按钮并播放 `tipsKey[0]`。
- `BindBtn()` 只追加 `btn.OnClick += PlayNextStep`，没有先解绑；在 UIManager 复用实例或重复打开时可能重复推进。
- `IsOver()` 使用 `_step > tipsKey.Count`，当 prefab 当前 3 条 `tipsKey` 播放到 `_step == 3` 时仍未结束，随后 `PlayTips()` 读取 `tipsKey[3]` 会越界。
- `PlayTips()` 未校验 `tipsKey`、`ContentTxt`、翻译结果，也没有停止旧的 `DOText`。
- `RefreshEnterState()` 未校验 `postcardEnter`，也没有停止旧的缩放 Tween。
- `Create()` 在 `OpenUI` 失败时也会直接写入 `HasGuide` / `IsGuiding`，且没有 `Application.isPlaying` 外层保护。
- `OnClose()` 未解绑按钮、未停止 Tween，也未把 `IsGuiding` 恢复为 false。

## 加固后行为

- `IsOver()` 改为 `tipsKey == null || _step >= tipsKey.Count`，避免最后一步后读取越界。
- `OnOpen()` 会先重置 `_isCompleting`，绑定按钮前先去重；按钮缺失或 `tipsKey` 为空时记录 warning 并降级为直接完成引导。
- `PlayNextStep()` 增加 `_isCompleting` 防重复完成保护，避免末尾重复点击多次打开 `PostcardPopup`。
- `PlayTips()` 增加 `ContentTxt`、步骤范围、空 key 和缺失翻译兜底；新文案 Tween 开始前会停止旧 `DOText`。
- `RefreshEnterState()` 增加 `postcardEnter` 空引用保护；入口缩放 Tween 开始前会停止旧缩放 Tween。
- `OnClose()` 统一解绑按钮、停止 Tween，并把 `DB.Postcard.IsGuiding` 写回 false。
- `Create()` 增加 `Application.isPlaying` 保护，并只在 `OpenUI` 返回非空弹窗后记录引导已打开状态。
- 存档写入改为 `DB.SavePostcard()`，保持与当前 Postcard 管理流程一致；保存失败只记录 warning，不阻断 UI 关闭或进入 `PostcardPopup`。

## 数据、资源、UI 与事件流

- 数据流：
  - 打开成功后写入 `HasGuide = true`、`IsGuiding = true`。
  - 弹窗关闭或完成销毁时写入 `HasGuide = true`、`IsGuiding = false`。
  - 未新增 DB 字段、PlayerPrefs key、RemoteConfig key 或 Luban 配置。
- UI 流：
  - 点击引导按钮按 `tipsKey` 顺序推进。
  - 最后一条文案之后调用 `PostcardPopup.Create()`，再销毁当前 `NormalGuidePopup`。
  - 空配置时直接进入完成路径，避免卡死在空引导弹窗。
- 资源流：
  - 本次未修改 prefab、sprite、Resources 路径或加载方式。
- 事件与平台流：
  - 未新增全局事件、广告、IAP、analytics、平台 SDK 或每帧轮询路径。

## 验证记录

- CodeGraph 已定位 `NormalGuidePopup` 的调用面：当前主要由自身流程和 `GMPanel` 触发，暂无覆盖测试。
- 静态 prefab 检查确认 `NormalGuidePopup.prefab` 当前 `tipsKey` 有 3 项，旧 `_step > tipsKey.Count` 存在可复现越界风险。
- 静态搜索：
  - `rg -n "OnValidate|Reset\\(|ExecuteAlways|ExecuteInEditMode|DispatchEvent|Subscribe<|Instantiate\\(|RemoteConfig|Ads|IAP|Analytics|PlayerPrefs" Assets\\Game\\UI\\Postcard\\Scripts\\NormalGuidePopup\\NormalGuidePopup.cs`
  - 结果：无命中。
- `git diff --no-index --check -- NUL Assets\\Game\\UI\\Postcard\\Scripts\\NormalGuidePopup\\NormalGuidePopup.cs`：无行尾空白错误，仅返回 Git 对未跟踪文件的 LF/CRLF 提示。
- `dotnet build .\\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过，结果为 0 error / 97 warnings。

## 剩余风险与未验证项

- 尚未在 Unity PlayMode 实际点击验证：空 `tipsKey`、3 条 `tipsKey` 完整推进、快速重复点击、关闭后 `IsGuiding=false`、引导完成后 `PostcardPopup` UI 栈表现。
- 尚未在 Android/iOS 横屏设备上验证触摸输入、DOTween 表现和 UI 层级。
- 当前文件与对应 prefab 仍处于未跟踪文件集合中；最终提交前需要确认这些新资源是否属于本次功能交付范围。

## 回滚路径

恢复 `NormalGuidePopup.cs` 中本次新增的空引用保护、按钮解绑、Tween 清理、`IsOver()` 边界修正和 `Create()` 存档写入保护，即可回到旧行为。若只需要快速关闭本次改动风险，可将 `NormalGuidePopup.cs` 还原到加固前版本，同时保留 prefab 与生成脚本不变。

## 2026-07-09 version1.1 Spine 随机引导动画

### 需求背景

`NormalGuidePopup` 原注释中预留 version1.1：`PlayTips()` 时 `spineCtrl` 需要在 `tutorial_idle` 状态下随机选择一个 tutorial API 播放，并在播放结束后回到 `tutorial_idle`；如果当前不是 `tutorial_idle`，则不切换播放，避免引导文案快速推进时打断正在播放的 Spine 表现。

### 影响文件与系统

- `Assets/Game/UI/Postcard/Scripts/NormalGuidePopup/NormalGuidePopup.cs`
  - 实现 `PlayTutorial01()`、`PlayTutorial02()`、`PlayTutorial03()` 的 Spine 播放链路。
  - `PlayTips()` 在刷新文案后调用随机 tutorial 入口。
  - 关闭或完成时清理当前 Spine `TrackEntry.Complete` 回调。
- `Assets/Game/UI/Postcard/Resources/NormalGuidePopup.prefab`
  - `spineCtrl` 绑定到新增的 `UISpineCtrl`。
  - `SkeletonGraphic.startingAnimation` 调整为 `tutorial_idle`，`startingLoop` 调整为 `1`，确保随机播放入口初始可用。
- 相关系统：
  - `UISpineCtrl`：复用 `GetCurrentAnimationName()`、`PlayAnimation(...)` 和运行时子级兜底查找。
  - Spine 资源：静态解析 `Assets/Game/ArtSrc/Spines/ui_Box_001/Ui_Box.json`，确认存在 `tutorial_idle`、`tutorial_01`、`tutorial_02`、`tutorial_02_idle`、`tutorial_02_return`、`tutorial_03`。

### 原有行为基线

- version1.1 的 `spineCtrl` 字段和 `PlayTutorial01()` / `PlayTutorial02()` / `PlayTutorial03()` API 已预留，但方法体为空。
- `PlayTips()` 只刷新 `ContentTxt.DOText(...)`，不会触发 Spine 动画。
- prefab 中新增的 `SkeletonGraphic` 初始动画为 `tutorial_01` 且不循环；这会导致后续 `GetCurrentAnimationName()` 不是 `tutorial_idle`，与“非 idle 不切换”的规则冲突。

### 新行为

- `PlayTips()` 每次成功刷新文案后调用 `PlayRandomTutorial()`。
- `PlayRandomTutorial()` 只在当前 Spine 动画名等于 `tutorial_idle` 时随机调用 `PlayTutorial01()`、`PlayTutorial02()` 或 `PlayTutorial03()`。
- `PlayTutorial01()` 播放 `tutorial_01` 非循环动画，完成后切回 `tutorial_idle` 循环。
- `PlayTutorial02()` 串联播放 `tutorial_02`、`tutorial_02_idle`、`tutorial_02_return`，最后切回 `tutorial_idle` 循环。
- `PlayTutorial03()` 播放 `tutorial_03` 非循环动画，完成后切回 `tutorial_idle` 循环。
- `spineCtrl` 未绑定时会从子节点查找 `UISpineCtrl`；仍找不到时只记录 warning，不阻断文案推进、存档写入或打开 `PostcardPopup`。

### 数据、资源、UI 与事件流

- 数据流：不新增或修改 `DB.Postcard` 字段；`HasGuide` / `IsGuiding` 写入仍沿用 2026-07-07 加固后的流程。
- 资源流：不新增 Resources 加载路径；使用 prefab 已绑定的 `SkeletonGraphic` 和 `ui_Box_001` Spine 数据。
- UI 流：文案推进和入口缩放 Tween 保持原流程；Spine 表现只在本弹窗内部播放，不影响 `PostcardPopup.Create()`。
- 事件流：只订阅当前 Spine `TrackEntry.Complete`，关闭或完成时解绑；未新增全局事件、PlayEvents、RemoteConfig、广告、IAP、analytics 或平台 SDK 链路。

### 验证记录

- 静态解析 `Assets/Game/ArtSrc/Spines/ui_Box_001/Ui_Box.json`，确认 version1.1 使用的 6 个动画名存在。
- 静态扫描 `NormalGuidePopup.cs` 未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`RemoteConfig`、`PlayEvents`、`DispatchEvent` 或 `Update()`。
- `dotnet build .\Assembly-CSharp.csproj --no-restore`：通过，结果为 0 errors / 99 warnings；warnings 属于当前工程既有范围，本次未逐项审计。
- `git diff --check -- Assets/Game/UI/Postcard/Scripts/NormalGuidePopup/NormalGuidePopup.cs Assets/Game/UI/Postcard/Resources/NormalGuidePopup.prefab`：未通过，命中当前未提交 version1.1 注释行、预留段落空白行、`#endregion` 和 prefab 空 YAML 字段的行尾空白；本次按用户“不要改注释”的要求未清理注释文本。

### 剩余风险与未验证项

- 尚未在 Unity PlayMode 实际验证三条 tutorial 随机播放、`TrackEntry.Complete` 回调、`tutorial_02` 三段串联以及完成后回到 `tutorial_idle`。
- 尚未验证快速连续点击 tips 时是否稳定遵守“非 idle 不切换”。
- 尚未在 Android/iOS 横屏设备验证 Spine UI 表现、触摸推进、遮罩层级和性能。
- prefab 仍有较大的未提交结构差异，最终提交前需要用户确认这些 UI 资源改动都属于本次 version1.1。

### 回滚路径

回退 `NormalGuidePopup.cs` 中 version1.1 Spine helper、随机入口和 `TrackEntry` 回调清理，并把 `NormalGuidePopup.prefab` 的 `SkeletonGraphic.startingAnimation` / `startingLoop` 恢复为旧值，即可关闭本次 Spine 随机动画；引导文案、Star 补齐、存档状态和打开 `PostcardPopup` 的流程仍会按 2026-07-07 加固逻辑运行。

## 2026-07-09 version2.0 第二阶段引导

### 需求背景

`NormalGuidePopup` 原注释补充 version2.0：新增 `isPartTwo` 状态。第二阶段引导关闭或完成时，不再执行开启 `PostcardPopup` 的操作。结合当前 `NormalGuidePopup_PartTwo.prefab` 资源证据，第二阶段需要作为独立引导弹窗使用，避免误写首阶段 `DB.Postcard.HasGuide` / `DB.Postcard.IsGuiding` 状态或重复补 Star。

### 影响文件与系统

- `Assets/Game/UI/Postcard/Scripts/NormalGuidePopup/NormalGuidePopup.cs`
  - 新增序列化字段 `isPartTwo`，由 `NormalGuidePopup_PartTwo.prefab` 或 `Create(true)` 指定。
  - `TryToShowNormalGuidePopup(bool isPartTwo = false)` 支持第二阶段入口，并增加运行态外层保护。
  - `Create(bool isPartTwo = false)` 按阶段选择 `NormalGuidePopup` / `NormalGuidePopup_PartTwo` 资源。
- `Assets/Game/UI/Postcard/Resources/NormalGuidePopup_PartTwo.prefab`
  - 静态 YAML 显示根节点名为 `NormalGuidePopup_PartTwo`，脚本字段 `isPartTwo: 1`，并已绑定 `tipsKey`、`btn`、`postcardEnter`、`spineCtrl`、`ContentTxt`。
- `Assets/Game/UI/Postcard/Resources/NormalGuidePopup.prefab`
  - 保持首阶段资源路径和默认首阶段行为；当前资源已绑定 `spineCtrl` 并以 `tutorial_idle` 循环作为初始动画。
- 相关系统：`Bear.UI.UIManager`、`DB.Postcard`、`PostcardPopup.Create()`、`PlayCtrl.Instance.Bag`。

### 原有行为基线

- 首阶段引导打开后会补足 9 个 Star，写入 `HasGuide=true` / `IsGuiding=true`。
- 弹窗关闭时会写回 `HasGuide=true` / `IsGuiding=false`。
- 引导完成会调用 `PostcardPopup.Create()` 打开明信片主界面。
- `TryToShowNormalGuidePopup()` 原先先读取 `DB.Postcard.HasGuide`，没有运行态外层保护。

### 新行为

- `isPartTwo=false` 时保留首阶段行为：检查 `HasGuide`、打开首阶段 prefab、打开成功后写首阶段引导状态、关闭时写 `IsGuiding=false`、完成后打开 `PostcardPopup`。
- `isPartTwo=true` 时打开 `NormalGuidePopup_PartTwo`，跳过 `HasGuide` 拦截，不补 Star，不写 `DB.Postcard.HasGuide` / `DB.Postcard.IsGuiding`，完成或关闭后只销毁当前弹窗。
- `TryToShowNormalGuidePopup(...)` 现在先判断 `Application.isPlaying`，非运行态直接返回 false，避免编辑器态入口触碰 DB。
- `Create(...)` 在 `OpenUI` 返回 null 时直接返回 null，不写入首阶段引导状态。

### 数据、资源、UI 与事件流

- 数据流：
  - 首阶段仍沿用 `DB.Postcard.HasGuide` / `DB.Postcard.IsGuiding` 和 `DB.SavePostcard()`。
  - 第二阶段不读写明信片引导存档状态，不新增 DB 字段、PlayerPrefs、RemoteConfig 或 Luban 配置。
- 资源流：
  - 首阶段资源名为 `NormalGuidePopup`。
  - 第二阶段资源名为 `NormalGuidePopup_PartTwo`，当前文件仍是未跟踪资源，需要最终纳入提交或交付清单。
- UI 流：
  - 文案推进和 version1.1 Spine 随机动画复用同一脚本流程。
  - 第二阶段完成后不会串联 `PostcardPopup.Create()`。
- 事件与平台流：
  - 未新增全局事件、PlayEvents、广告、IAP、analytics、RemoteConfig、平台 SDK 或每帧轮询。

### 验证记录

- CodeGraph 已定位 `NormalGuidePopup` 的 `TryToShowNormalGuidePopup`、`Create`、`OnOpen`、`OnClose`、`CompleteGuide` 影响面；当前无自动化覆盖测试。
- 静态 prefab 检查确认 `NormalGuidePopup_PartTwo.prefab` 的 `isPartTwo: 1`、`spineCtrl`、`ContentTxt`、`tipsKey`、`btn` 绑定存在。
- 静态扫描 `NormalGuidePopup.cs` 未发现新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`RemoteConfig`、`PlayEvents`、`DispatchEvent`、`Update()`、`PlayerPrefs` 或 `Instantiate()`。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过，结果为 0 error / 99 warnings。
- `git diff --check -- Assets\Game\UI\Postcard\Scripts\NormalGuidePopup\NormalGuidePopup.cs Assets\Game\UI\Postcard\Resources\NormalGuidePopup.prefab Assets\Game\UI\Postcard\Resources\NormalGuidePopup_PartTwo.prefab DevelopMD\NormalGuidePopup_HardeningRecord.md DevelopMD\已有功能设计与使用记录.md` 当前未通过；剩余命中为保留的 version 注释行尾空白和 Unity prefab 新增空 YAML 字段行尾空白，本次按“不要改注释”和避免手改 prefab 空字段处理。

### 剩余风险与未验证项

- 尚未在 Unity PlayMode 实际验证首阶段/第二阶段分别打开、点击完成、手动关闭、UI 栈销毁顺序和 `PostcardPopup` 是否只在首阶段打开。
- 尚未在 Android/iOS 横屏设备验证触摸推进、Spine 表现、遮罩层级和性能。
- `NormalGuidePopup_PartTwo.prefab` 与 `.meta` 当前为未跟踪文件，最终提交前需要确认它们属于 version2.0 交付资源。
- 如果外部直接调用 `Create(true)` 且资源误配 `isPartTwo=false`，`OnOpen()` 同步执行时会依赖 prefab 序列化值；当前静态证据显示 PartTwo prefab 已正确配置。

### 回滚路径

回退 `NormalGuidePopup.cs` 中 `isPartTwo` 字段、`TryToShowNormalGuidePopup(bool)` / `Create(bool)` 阶段参数、`OnOpen()` / `OnClose()` / `CompleteGuide()` 的第二阶段分支，并移除或停用 `NormalGuidePopup_PartTwo.prefab`，即可恢复首阶段单一引导弹窗行为。首阶段 version1.1 Spine 动画可按前一节回滚路径独立处理。

## 2026-07-10 Create 关闭回调与文案 Tween 清理

### 需求背景

`NormalGuidePopup.Create(...)` 需要支持可选 `closeCallback`，供调用方在引导弹窗完成 Bear UI 关闭生命周期后串联本地流程。任务开始时的未提交代码仅保存了回调参数，没有触发与清理逻辑；同时，`PlayTips()` 遇到空白 key 时会在停止旧 `DOText` 前返回，旧 Tween 可能继续回写已清空的文案。

### 影响文件与系统

- `Assets/Game/UI/Postcard/Scripts/NormalGuidePopup/NormalGuidePopup.cs`
  - 完成 `Create(bool, Action)` 的关闭回调契约。
  - 调整空白文案 key 分支的 Tween 清理顺序。
  - 使用 `Bear.Logger` 隔离外部回调异常。
- `DevelopMD/NormalGuidePopup_HardeningRecord.md`
  - 记录本次行为基线、验证证据、风险和回滚方式。
- `DevelopMD/已有功能设计与使用记录.md`
  - 记录可复用的 Bear UI 关闭回调生命周期事实。
- 本次未修改 `NormalGuidePopup.prefab`、`NormalGuidePopup_PartTwo.prefab`、DB、配置或资源路径。

### 原有行为基线

- 首阶段完成仍是先调用 `PostcardPopup.Create()`，再通过 `UIManager.DestroyUI(this)` 销毁当前引导弹窗；第二阶段只销毁当前弹窗。
- `OnClose()` 会解绑按钮、停止文案/入口/Spine 回调，并在首阶段尝试写回 `HasGuide=true`、`IsGuiding=false`。
- 任务开始时新增的 `closeCallback` 只在 `Create(...)` 中写入字段，没有执行入口，也没有在关闭后清空；UI 复用或二次进入 `OnClose()` 时存在旧回调残留风险。
- `PlayTips()` 的空白 key 分支先清空文本再返回，没有停止上一条文案的 `DOText`。

### 新行为

- `Create(bool isPartTwo = false, Action closeCallback = null)` 在弹窗打开成功后覆盖本次实例的回调字段；即使传入 null，也会覆盖旧值。
- `OnOpen()` 检出按钮缺失或空 `tipsKey` 时先记录待完成状态；`Create(...)` 注入阶段参数、回调并完成首阶段打开状态记录后，会在同一调用栈执行降级完成，避免 `OpenUI` 的同步 `OnOpen()` 早于回调初始化。
- `OnClose()` 在按钮/Tween/Spine 清理、首阶段存档收尾、状态复位和 `base.OnClose()` 完成后统一消费回调。
- 回调执行前先复制并清空实例字段，确保 `CloseUI` 后再次 `DestroyUI` 或回调重入时最多执行一次。
- 外部回调抛出的异常会通过 `this.LogError(...)` 记录并被隔离，避免中断 `UIManager` 后续字典移除、资源卸载或对象销毁。
- `OpenUI` 返回 null 或非运行态调用时仍返回 null，不写首阶段引导状态，也不执行回调。
- 首阶段和第二阶段原有 DB、补 Star 和 `PostcardPopup` 分支不变；回调只作为关闭通知，不替代这些流程。
- `PlayTips()` 现在会在读取和判断 key 前停止旧文案 Tween，空白 key 不再允许上一条 `DOText` 继续回写。
- prefab 名称改用 `nameof(NormalGuidePopup)` 构造，`UIManager.Instance` 增加空安全访问；资源名和正常打开行为不变。

### 数据、资源、UI 与事件流

- 数据流：首阶段仍使用 `DB.Postcard.HasGuide` / `DB.Postcard.IsGuiding` 和 `DB.SavePostcard()`；第二阶段仍不读写这些状态。
- UI 流：正常完成、空配置降级完成、`CloseUI`、`CloseTopUI`、`CloseAllUI` 或 `DestroyUI` 进入 `OnClose()` 后消费一次回调。按当前代码顺序，首阶段完成回调位于 `PostcardPopup.Create()` 之后；该顺序尚未做 PlayMode 验证。
- 资源流：仍只加载 `NormalGuidePopup` 或 `NormalGuidePopup_PartTwo`，未修改 prefab 或新增资源地址。
- 事件与平台流：未新增 `PlayEvents`、全局事件、广告、IAP、analytics、RemoteConfig、平台 SDK 或每帧轮询。

### 验证记录

- 当前仓库没有 `.codegraph/` 索引；本次按规则改用目标源码、Bear UI 源码、Git diff 和精确搜索取证。
- `UIManager.DestroyUI(...) -> BaseUIView.OnDestroyView() -> NormalGuidePopup.OnClose()` 的静态调用链已确认；`CloseUI` / `CloseTopUI` / `CloseAllUI` 也直接调用 `OnClose()`。
- 静态调用方检查确认现有调用仍为 `GMPanel` 的 `Create()` 和 `PostcardPopup_PageCompleteProcess` 的 `Create(_isGuiding)`；新增可选参数不改变现有调用。
- 静态 prefab 检查确认首阶段和第二阶段资源仍有 `tipsKey`、`btn` 与正确的 `isPartTwo` 序列化字段。
- 风险关键字扫描未命中 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`PlayEvents`、`DispatchEvent`、`Update()`、`PlayerPrefs`、`Instantiate` 或平台/广告/配置链路。
- `git diff --check -- BrainPunk/Assets/Game/UI/Postcard/Scripts/NormalGuidePopup/NormalGuidePopup.cs` 通过，仅输出工作区 LF/CRLF 转换提示。
- 首次执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 时，`Temp\bin\Debug` 为空，出现 191 个 Unity 生成依赖 DLL 缺失错误，首个错误为 `Apple.Core.dll` 元数据文件不存在。
- 随后允许构建项目引用；外层命令在 184 秒达到工具超时，但子进程继续结束并把 `Temp\bin\Debug` 补充到 576 个文件。最终修改后再次执行上述目标程序集命令已通过，结果为 0 error / 98 warnings；warnings 属于当前完整程序集范围，本次未逐项审计。
- Unity 6000.0.59f2 当前已打开该工程，但 `Editor.log` 最后写入时间早于本次脚本修改；因此已有 C# 项目编译证据，不包含 Unity Editor domain reload 或 PlayMode 通过结论。

### 剩余风险与未验证项

- 尚未在 Unity PlayMode 验证首/二阶段正常完成、外部关闭、重复关闭、回调一次性、回调异常隔离和空白 key 文案清理。
- `NormalGuidePopup` 的参数初始化和空配置降级收尾以 `Create(...)` 为公开入口；静态搜索没有直接 `UIManager.OpenUI<NormalGuidePopup>(...)` 调用方。未来若绕过 `Create(...)` 直接打开，待完成状态不会由工厂消费，不属于当前 API 契约。
- 直接调用 Unity `Destroy(gameObject)` 或场景销毁不保证进入 Bear UI 的 `OnClose()`，因此不属于当前关闭回调契约。
- 回调中同步重开同一个 `NormalGuidePopup` 可能落在 `UIManager.DestroyUI` 尚未移除旧实例字典项的窗口内；调用方应串联其他 UI 或延后重开同一弹窗。
- 尚未在 Android/iOS 横屏设备验证触摸、遮罩/UI 栈和性能；本次没有新增持续分配或每帧逻辑。

### 回滚路径

移除 `Create(...)` 的 `closeCallback` 参数、`_closeCallback` 字段、设置/消费 helper 和 `OnClose()` 回调入口，即可恢复无关闭通知的旧行为。若只回滚文案优化，将 `KillContentTween()` 移回非空翻译分支；DB、prefab、资源和阶段分支不需要回滚。

## 2026-07-13 已通关关卡胜利星补录（职责迁移）

### 需求背景

历史已通关关卡需要按关卡配置补齐明信片胜利星。最初实现由首阶段 `NormalGuidePopup.FullNeededStarCount()` 触发弹窗内私有补录方法；当前职责已迁入 `PostcardManager`，并改由游戏加载后处理流程调用，不再依赖玩家进入明信片引导弹窗。

### 影响文件与系统

- `Assets/Game/UI/Postcard/Scripts/PostcardManager.cs` 新增公开 `TryGetAllCompleteLevelStar()`，集中历史关卡遍历和胜利星补录职责。
- `Assets/Game/UI/Loading/Scripts/LoadManager.cs` 在 `GameManager.Instance.ReadyToPlay()` 之后、`InitGameReward()` 之前调用该方法。
- `Assets/Game/UI/Postcard/Scripts/NormalGuidePopup/NormalGuidePopup.cs` 已移除私有实现，`FullNeededStarCount()` 不再执行历史通关星补录。
- 读取 `DB.GameData.PassedLevels`，通过当前运行态 `LevelCtrl` 的 `LevelSort.Path` 映射到 `LevelData`。
- 复用 `GameManager.Instance.Postcard.TryGrantVictoryStars(...)` 写入 `DB.Postcard.LevelCollectedStar` 与 `DB.Postcard.LevelWaitingAnimStar`，并沿用其 `DB.SavePostcard()`、重复星去重和保存失败回滚。
- 未修改 prefab、Luban/RemoteConfig、关卡、资源、全局事件、analytics、广告、IAP 或平台 SDK。

### 行为变化与数据流

- `FullNeededStarCount()` 原有“背包 Star 至少补到 9”行为保持不变，但不再承担历史通关星补录。
- 加载后处理流程调用 `PostcardManager.TryGetAllCompleteLevelStar()`；因此补录入口从首阶段引导弹窗迁到游戏加载流程。
- 非运行态、通关列表为空时直接跳过；`LevelCtrl` 或 `PostcardManager` 缺失时记录 Bear warning 并停止补录，不阻断加载后续流程。
- 对 `PassedLevels` 中的 sortId 先用 `HashSet<int>` 去重，再调用 `LevelCtrl.GetLevelSortById(...)` 和 `GetLevelDataById(...)` 获取当前有效配置。
- 每个有效关卡按 `LevelData.LevelVictoryStarCount` 调用 `TryGrantVictoryStars(...)`；已有星 id 不重复写入，配置数量仍由管理器限制到 0 至 3。
- sort/path/LevelData 缺失或单关保存失败时记录 warning 并继续处理其他关卡。
- 不再按 `PassedLevels.Count` 直接额外增加背包 Star；本方法只补录既有明信片胜利星存档链，背包的引导保底仍由 `FullNeededStarCount()` 负责。

### 验证记录

- 静态源码证据确认 `PassedLevels` 保存的是关卡 sortId，`LevelCtrl` 提供 sortId 到 `LevelSort.Path`、再到 `LevelData` 的运行态映射。
- 静态 diff 确认补录实现已从 `NormalGuidePopup` 迁入 `PostcardManager`，`LoadManager` 是当前唯一未注释调用点；实现继续复用 `TryGrantVictoryStars(...)`，没有新增全局事件、每帧逻辑或编辑器生命周期入口。
- 旧实现阶段曾完成完整项目引用构建与目标程序集编译；本次职责迁移的文档调整没有重新执行 C# 编译或 Unity PlayMode，不能沿用旧编译结果声称当前工作区已验证。

### 剩余风险与回滚路径

- 尚未在 Unity PlayMode 验证重复 sortId、无效 sort/path、历史星已存在、保存失败和多关补录结果；Android/iOS 横屏设备也未验证。
- `TryGrantVictoryStars(...)` 会同时累加历史关卡的 `LevelWaitingAnimStar`；后续重玩相应关卡并打开胜利面板时可能消费该等待动画，实际产品表现待 PlayMode 确认。
- 每个首次新增星记录的历史关卡会独立调用一次 `DB.SavePostcard()`；当前流程在游戏加载后处理阶段执行，大量历史关卡时的移动端加载耗时仍待测量。
- 回滚当前职责迁移时，移除 `LoadManager` 调用和 `PostcardManager.TryGetAllCompleteLevelStar()`；若要恢复旧触发时机，再恢复 `NormalGuidePopup.FullNeededStarCount()` 末尾的私有补录调用。无需修改存档结构。

## 2026-07-13 PostcardStageStep 与只读展示职责

### 需求背景

`NormalGuidePopup` 改为四阶段展示组件，使用 `PostcardStageStep` 选择对应资源。旧 `isPartTwo`、补 Star、引导状态写入和完成后自动打开 `PostcardPopup` 的职责全部移除；弹窗只保留 `HasGuide` 只读门槛、`PlayTips` 文案/Spine 表现以及 UI 生命周期清理。

### 影响文件、资源与系统

- `Assets/Game/UI/Postcard/Scripts/NormalGuidePopup/NormalGuidePopup.cs`
  - `PostcardStageStep.One` 至 `Four` 映射到 `NormalGuidePopup_1` 至 `_4`。
  - `IsShowNormalGuidePopup(...)` 保留 RemoteConfig 关卡门槛并只读取 `DB.Postcard.HasGuide`。
  - `Create(...)` 只按阶段打开 UI、注入关闭回调和处理空配置降级。
  - 移除 `isPartTwo`、`RecordHasGuide`、`RecordGuideState`、`DB.SavePostcard()`、补 Star 入口和 `PostcardPopup.Create()` 串联。
- `Assets/Game/UI/Postcard/Scripts/PostcardPopup_PageCompleteProcess.cs`
  - 原 `Create(true)` 第二阶段入口迁移为 `Create(PostcardStageStep.Two)`；`_isGuiding` 仍是当前 Popup 内部流程状态。
- `Assets/Game/UI/Postcard/Resources/NormalGuidePopup_1.prefab`
  - 移除已不存在的 `isPartTwo` 序列化字段。
- 当前工作区已有 `NormalGuidePopup_1` 至 `_4` prefab 与 meta；本次未创建这些资源，也未修改其 tips、Spine 或布局内容。

### 原有行为基线与当前行为

- 原行为：首阶段会补足背包 Star、写入 `HasGuide=true` / `IsGuiding=true`，关闭时写回 `IsGuiding=false`，完成后自动打开 `PostcardPopup`；`isPartTwo=true` 只绕开这些首阶段行为。
- 当前行为：`PostcardStageStep` 只决定加载哪个阶段 prefab；无效枚举返回 null，不尝试加载资源。
- `IsShowNormalGuidePopup(...)` 在运行态检查关卡门槛，并把 `DB.Postcard` 为空或 `HasGuide=true` 都视为不可显示；该入口不写数据。
- `OnOpen()` 绑定按钮并调用 `PlayTips(false)` / 首段 Spine；后续点击继续复用 `PlayTips()` 和现有 Spine 随机表现。
- 引导完成只解绑按钮、清理 Tween/Spine 回调并销毁当前 UI；`OnClose()` 只做本地清理和一次性关闭回调。
- 数据状态切换、存档、奖励和后续 UI 串联全部由外部流程负责。

### 数据、资源、UI 与事件流

- 数据流：本类唯一 DB 访问为读取 `DB.Postcard.HasGuide`；无 `HasGuide` / `IsGuiding` 赋值、无 `DB.SavePostcard()`、无背包增减。
- 资源流：`One/Two/Three/Four` 分别请求 `NormalGuidePopup_1/_2/_3/_4`；四个文件与 meta 已完成静态存在性检查。
- UI 流：page complete 的旧第二阶段入口改为 `PostcardStageStep.Two`；第一阶段现有 GM 和关卡胜利入口继续使用默认参数。
- 事件流：保留当前按钮回调、Spine `TrackEntry.Complete` 和关闭回调；未新增全局事件、PlayEvents、analytics、广告、IAP 或平台 SDK。

### 验证记录

- CodeGraph 确认 `GameVictoryPanel` 先调用 `IsShowNormalGuidePopup` 做只读门槛判断，再调用 `Create(One)`；`Create` 通过 `GetStagePrefabName` 路由资源，`PostcardStageStep.Two` 已由 page-complete 流程使用。
- 静态扫描未命中 `isPartTwo`、`RecordGuideState`、`RecordHasGuide`、`FullNeededStarCount`、`SavePostcard`、背包增减、`PostcardPopup.Create()` 或 DB 字段赋值。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，98 warnings / 0 errors。
- 两个修改脚本的 `git diff --check` 通过，仅有 LF/CRLF 提示。
- Unity PlayMode 未执行，不能确认 `_1` 至 `_4` 的实际加载、各 prefab variant 继承关系、tips 配置和 UI 关闭时序。

### 风险、未验证项与回滚

- P1：`NormalGuidePopup_2` 至 `_4` 是当前未跟踪的 prefab variant；静态文件存在不等于 UIManager 可在运行时成功加载，需 PlayMode 逐阶段验证。
- P1：弹窗不再写 `HasGuide/IsGuiding` 或自动打开 `PostcardPopup`；外部流程若尚未接管这些职责，可能重复触发第一阶段或在引导结束后无后续 UI。当前代码按用户要求不在本类补偿。
- P2：`IsShowNormalGuidePopup(...)` 只负责第一阶段现有调用点的 HasGuide 门槛；page-complete 第二阶段走 `Create(Two)`，不受该门槛影响。未来第三/四阶段调用方需明确是否先做相同门槛判断。
- P2：Android/iOS 横屏的四阶段触摸、Spine、遮罩和关闭回调仍未验证。
- 回滚：恢复 `isPartTwo` bool API、旧 `NormalGuidePopup` / `_PartTwo` 资源路由、数据写入和 `PostcardPopup.Create()` 分支，并把 page-complete 调用恢复为旧 bool 参数。若只回滚阶段资源路由，可保留当前只读职责并把 `Create` 固定到单一 prefab。
- 顶部 version2.0 注释因与本次“移除 isPartTwo 且不得修改数据”的明确需求冲突，已最小更新为 `PostcardStageStep` 与只读展示说明。

### 2026-07-13 编译调用链收口

- `PlayCtrl_Success` 中未启用的旧检查方法仍引用已移除的 `TryToShowNormalGuidePopup`，导致完整 C# 编译出现 `CS0117`；该方法和对应的注释调用已移除，胜利流程继续直接执行原 `VictoryProcess()`。
- 移除了 `NormalGuidePopup.cs` 中误加入且运行时代码不需要的 `UnityEditor.Build.Pipeline` using，避免 Player 代码依赖 Editor 程序集。
- `IsShowNormalGuidePopup` 删除未使用的 step/callback 参数，只保留当前调用所需的关卡 id；阶段选择继续由 `Create(PostcardStageStep, Action)` 承担。
- 旧 `postcardEnter` 表现分支移除后不再有 `_enterTween` 写入点，本次同步移除该残留字段和空清理方法；文案 `_contentTween` 与 Spine TrackEntry 清理保持不变。
- 调整后 `dotnet build Assembly-CSharp.csproj --no-restore ...` 通过：98 warnings / 0 errors；Unity PlayMode 和四阶段资源加载仍未验证。

## 2026-07-14 异步生命周期与重复创建加固

### 需求背景

`NormalGuidePopup` 的文案切换和完成退出都包含 1 秒异步等待。原实现没有把等待任务与当前弹窗生命周期、当前文案请求绑定；快速连续点击或等待期间被外部关闭时，旧任务仍可能恢复并修改 UI，完成任务还可能再次请求销毁。Spine 随机分支使用 `Random.Range(1, 3)`，按 Unity 整数上限不包含规则只能得到 1 或 2，`randomIndex == 0` 的 `PlayTutorial01()` 分支不可达。

### 影响文件与原有行为基线

- 修改 `Assets/Game/UI/Postcard/Scripts/NormalGuidePopup/NormalGuidePopup.cs`；未修改四阶段 prefab、Generated 脚本、调用方、DB、配置或资源路径。
- 原文案任务在 `HideTips()` 返回后直接读取可变 `_step`，多个等待任务会共享后续步骤值；`OnClose()` 只清理当时存在的 Tween 和 Spine 回调，不能阻止旧任务稍后新建 Tween。
- 原完成任务等待后无生命周期复核，若期间由 `CloseUI` / `CloseTopUI` / `CloseAllUI` 进入 `OnClose()`，旧任务仍会调用 `DestroyUI(this)`。
- Bear `UIManager.OpenUI(path)` 在同路径实例已位于栈顶时会返回原实例而不再次执行 `OnOpen()`；原 `Create(...)` 随后会覆盖该实例的关闭回调。
- 原 `OnOpen()` 不复位 `groundAnim.isOut`；若实例由 Bear `CloseUI` 关闭后缓存并复用，退出动画参数可能沿用上次值。

### 加固后行为

- `_lifecycleVersion` 标识本次打开生命周期；`OnOpen()` 建立新版本，`OnClose()` 立即使旧版本失效。完成延时结束后只有弹窗仍处于同一打开生命周期才允许调用 `DestroyUI(this)`。
- `_tipRequestVersion` 标识最新文案请求；`PlayTips(...)` 在等待前捕获目标步骤和两个版本，等待后只允许最新且未进入完成阶段的请求更新文本、创建 Tween 或显示箭头。
- 新文案请求会在等待前停止旧文案 Tween；快速点击仍沿用现有步骤推进语义，但旧等待任务不会再覆盖最新步骤或在关闭后恢复 UI。
- `Task` 改为项目现有 `UniTask`，所有无等待调用点显式 `.Forget()`；完成阶段会先使所有文案请求失效。
- `Create(...)` 在调用 `OpenUI` 前检查同路径实例；目标仍处于打开状态时只记录 Bear warning 并返回原实例，不重复入栈，也不覆盖首个关闭回调。同一实例首次注入回调后还会标记已配置，重新打开和关闭时重置该本地标记。
- `OnOpen()` 复位 `groundAnim.isOut=false`，保证缓存实例重开从入口状态开始。
- Spine 随机范围改为 `Random.Range(0, 3)`，`PlayTutorial01/02/03` 三个分支均可到达；首开固定调用 `PlayTutorial01()` 的既有行为不变。

### 数据、资源、UI 与事件流

- 数据流保持只读：仍只由 `IsShowNormalGuidePopup(...)` 读取 RemoteConfig 门槛和 `DB.Postcard.HasGuide`；本次不写 DB、不保存、不增减背包物品。
- 资源流保持 `PostcardStageStep.One/Two/Three/Four` 到 `NormalGuidePopup_1/_2/_3/_4`；静态 YAML 显示 `_2` 至 `_4` 仍引用 `_1` 的 prefab GUID，本次未改任何资源绑定。
- UI 流仍是点击推进、最后一步播放退出动画、等待 1 秒后销毁并在 `OnClose()` 消费一次关闭回调；只增加过期任务和重复回调覆盖防护。
- 未新增 `PlayEvents`、全局事件、每帧轮询、实例化、analytics、广告、IAP 或平台 SDK 路径。

### 验证记录

- CodeGraph 与直接源码检查确认调用方覆盖 `GameVictoryPanel` 的第一/二阶段、`PostcardPopup_PageCompleteProcess` 的第三/四阶段和 `GMPanel`；CodeGraph 未发现该类的自动化测试覆盖。
- Bear UI 源码确认 `CloseUI` 会执行 `OnClose()`但保留实例，`DestroyUI` 会先从栈移除再执行 `OnDestroyView() -> OnClose()`，从而支持本次生命周期版本边界判断。
- `git diff --check -- Assets/Game/UI/Postcard/Scripts/NormalGuidePopup/NormalGuidePopup.cs` 通过，仅输出工作区 LF/CRLF 转换提示。
- 静态扫描未命中新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`PlayEvents`、`DispatchEvent`、`Update()`、`PlayerPrefs`、`Instantiate()`、`System.Threading.Tasks` 或旧 `Random.Range(1, 3)`。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过：0 errors / 98 warnings。

### 剩余风险与回滚路径

- 尚未在 Unity PlayMode 验证快速连点、文案等待期间外部关闭、同阶段重复 `Create(...)`、关闭后重开、四阶段回调串联和三个随机 Spine 分支的实际表现。
- 尚未在 Android/iOS 横屏设备验证触摸节奏、遮罩/UI 栈、Spine 表现与性能；本次没有新增每帧逻辑或资源加载。
- 重复 `Create(...)` 会返回当前已打开的同路径实例，不会重新置顶或替换调用方回调；调用方若需要区分“新建成功”和“已有实例”，当前返回值契约没有单独状态，仍需按实际产品流程做 PlayMode 验证。
- 回滚时移除 `_lifecycleVersion`、`_tipRequestVersion`、`_isCreateConfigured` 及对应校验 helper，把 `PlayTips` / `CompleteGuideAsync` 恢复为原 `Task` 调用，删除 `OnOpen()` 的 `isOut` 复位，并把随机范围恢复原值即可；DB、prefab、资源和调用方无需回滚。

## 2026-07-14 首条提示完成前禁止点击推进

### 需求背景

弹窗打开后会异步执行首个 `PlayTips(false)`，其中 `HideTips()` 有 1 秒等待，随后 `DOText` 播放逐字动画。原按钮在 `OnOpen()` 中立即完成绑定，首条提示尚未完整显示时连续点击仍会递增 `_step`，并可能提前进入 `CompleteGuideAsync()` 关闭弹窗。本次要求给首次点击增加时序保护：首条提示的等待和逐字动画完成前，点击不得推进步骤或触发结束。

### 影响文件与原有行为基线

- 修改 `Assets/Game/UI/Postcard/Scripts/NormalGuidePopup/NormalGuidePopup.cs`；未修改 Generated 脚本、四阶段 prefab、调用方、配置、DB 或资源路径。
- 原行为为 `OnOpen()` 直接 `PlayTips(false).Forget()`，`PlayNextStep(...)` 只检查 `_isCompleting` 和 `IsOpen`；首条提示的异步等待与按钮推进没有互斥边界。
- 后续每步仍由同一按钮执行 `_step++`，越过 `tipsKey` 后进入既有 `CompleteGuideAsync()`；本次不改变后续步骤节奏和退出动画。

### 修改后行为与 UI 流

- `OnOpen()` 在启动首条提示前设置 `_isFirstTipPlaying=true`，并由 `PlayFirstTipAsync(...)` 先等待 `PlayTips(false)` 返回，再等待该次创建的 `DOText` Tween 完成或被清理。
- `PlayNextStep(...)` 在 `_isFirstTipPlaying` 期间直接忽略点击，因此首条提示的初始化等待和逐字动画尚未完成时，连续点击不会修改 `_step`，也不会触发结束。
- 首条提示返回且对应 Tween 结束后，仅当弹窗仍属于同一 `_lifecycleVersion` 且保持打开时才解除点击锁；旧弹窗的异步返回不能解锁重开的新实例。
- `OnClose()` 清理本地点击锁。空配置、按钮缺失、外部关闭、重复创建、后续文案请求版本和现有退出流程保持原边界。
- 当前锁定边界覆盖 `PlayTips(false)` 的 `HideTips()` 等待和首条 `DOText` Tween；Tween 正常完成或被生命周期清理后，才按当前生命周期状态决定是否解锁。

### 数据、资源、事件与平台边界

- 本次只新增弹窗实例内的布尔时序状态和局部 `UniTask` 包装，不读写 DB、不保存、不增减背包物品。
- 不新增 prefab 字段、资源加载、`PlayEvents`、全局事件、每帧 `Update`、实例化、analytics、广告、IAP、RemoteConfig 写入或平台 SDK 调用。
- Android/iOS 共用同一 UI 点击路径，没有新增平台分支。

### 验证记录

- `git diff --check -- Assets/Game/UI/Postcard/Scripts/NormalGuidePopup/NormalGuidePopup.cs` 通过，仅有工作区 LF/CRLF 转换提示。
- 静态源码检查确认：首条提示启动前加锁；`PlayNextStep(...)` 在加锁期间返回；首条 Tween 纳入等待；解锁受 `_lifecycleVersion` / `IsOpen` 保护；`OnClose()` 复位锁状态。
- 敏感项扫描未发现本次新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`PlayEvents`、`DispatchEvent`、`Update()`、`PlayerPrefs` 或 `Instantiate()`。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -t:Rebuild -v:minimal /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过：0 errors / 99 warnings；输出中没有引用 `NormalGuidePopup.cs` 的 warning，99 条为当前目标程序集其他文件的既有警告。

### 剩余风险与回滚路径

- 尚未在 Unity PlayMode 验证首条提示 1 秒等待内快速连点、等待结束后的首次有效点击、仅一条 `tipsKey` 时的正常结束、等待期间外部关闭/重开和四阶段 prefab 的实际输入表现。
- 尚未在 Android/iOS 横屏设备验证多点触控与帧率波动下的按钮节奏。
- 若首条 Tween 因外部关闭被 Kill，等待会结束，但旧生命周期检查会阻止它影响后续重开的弹窗；该分支仍需 PlayMode 验证。
- 回滚时移除 `_isFirstTipPlaying`、`PlayFirstTipAsync(...)` 和 `PlayNextStep(...)` 的对应判断，并把 `OnOpen()` 恢复为直接 `PlayTips(false).Forget()`；其他异步生命周期、资源、DB 和调用方无需回滚。

## 2026-07-15 GuidePopup 等待式 Async API

### 需求背景与影响范围

- 为后续 `GuideFlowBase` 子类补充 `NormalGuidePopup.ShowAsync(step, cancellationToken)`，让流程层可直接等待弹窗关闭结果，不再为每个步骤重复包装 callback。
- 仅修改 `NormalGuidePopup.cs`；现有 `Create(step, closeCallback)`、四阶段 prefab、Generated 脚本和现有调用方均未改动。
- 原有基线只有 callback 关闭通知；同阶段弹窗已打开时 `Create(...)` 返回现有实例且不会替换首个回调，无法安全追加第二个等待者。

### 当前行为

- 弹窗按既有 `Create(...)` 打开并把 Bear UI 的 `OnClose()` 转换为 `GuideFlowResult.Completed`。
- 外部取消会销毁本次创建且仍打开的弹窗，并返回 `Cancelled`；非运行态、阶段无效、UIManager 或创建失败返回 `Failed`。
- 同阶段弹窗已打开时在调用 `Create(...)` 前返回 `AlreadyRunning`，不覆盖现有 callback，也不会让新等待永久悬挂。
- API 只返回结果值，不新增 `async void`；现有 callback 路径继续兼容，Postcard 业务编排本次没有迁移。

### 数据、资源、UI 与事件流

- 阶段到 prefab 的映射、Bear `UIManager.OpenUI/DestroyUI` 和原 `OnClose()` 回调消费方式保持不变。
- 不读写 DB/save、RemoteConfig、奖励或资源配置，不新增 `PlayEvents`、全局事件、实例化入口、analytics、广告、IAP 或平台 SDK。
- API 按 Unity 主线程调用设计；Android/iOS 没有新增平台分支。

### 验证记录、风险与回滚

- 目标脚本 `git diff --check` 与静态敏感项扫描通过；没有新增 `async void`、编辑器生命周期、每帧入口或全局事件。
- Unity 已在目标脚本修改后重新生成 `Library/ScriptAssemblies/Assembly-CSharp.dll/.pdb`，程序集二进制可检出 `NormalGuidePopup+<ShowAsync>` 状态机；目标编译完成。
- 命令行 `dotnet build Assembly-CSharp.csproj` 当前仍受 `Temp/bin/Debug` 中 191 个缺失元数据 DLL 阻断；额外的独立 Unity Roslyn 检查受项目模块化 Unity 程序集与旧式 `UnityEngine.dll` 依赖冲突阻断，但不影响上述 Unity 实际目标程序集证据。
- 尚未执行 PlayMode 的正常关闭、外部取消、同阶段重复打开、外部销毁、四阶段串联和 Android/iOS 横屏验证。
- 回滚时删除 `ShowAsync(...)` 即可；现有 `Create(...)`、Bear UI 生命周期、prefab、配置和数据不需要回滚。

## 2026-07-15 每条提示播放期间禁止点击推进

### 需求背景与原有基线

- 原点击锁只由 `PlayFirstTipAsync(...)` 管理，首条提示的 1 秒隐藏等待和逐字 Tween 完成前不能点击；后续 `PlayTips().Forget()` 不会重新加锁。
- 后续提示播放期间连续点击仍可递增 `_step`；请求版本只能阻止旧提示覆盖新内容，不能阻止步骤被快速跳过。
- 本次仅修改 `Assets/Game/UI/Guide/Scripts/NormalGuidePopup/NormalGuidePopup.cs`，不修改四阶段 prefab、调用方、配置、DB 或资源路径。

### 修改后行为与生命周期

- `_isFirstTipPlaying` 收敛为 `_isTipPlaying`，`PlayNextStep(...)` 在任意提示播放期间直接返回。
- 每次有效 `PlayTips(...)` 在开始隐藏旧提示前加锁，经过 `HideTips()` 的 1 秒等待、文案解析和 `DOText` 逐字 Tween 后才解锁；空 key 或请求失效会通过 `finally` 收尾。
- 解锁要求弹窗仍处于同一 `_lifecycleVersion` 且请求仍是最新 `_tipRequestVersion`，旧生命周期或旧请求不能提前解锁新的提示。
- `OnClose()` 继续使生命周期和请求版本失效、清理 Tween，并显式复位点击锁；完成阶段仍先解绑按钮。
- `PlayFirstTipAsync(...)` 已删除，首条与后续提示统一调用同一个 `PlayTips(...)` 锁定边界。

### 数据、资源、事件与平台边界

- 不读写 DB/save、RemoteConfig、奖励或资源配置，不新增 prefab 字段、资源加载、全局事件、analytics、广告、IAP 或平台 SDK。
- 没有新增 `Update`、轮询或对象实例化；Android/iOS 共用同一按钮入口与 UniTask/Tween 时序。

### 验证、风险与回滚

- `git diff --check` 与敏感项静态扫描通过，没有新增 `async void`、编辑器生命周期、全局事件或每帧入口。
- Unity Hot Reload 已成功编译并应用 `OnOpen`、`PlayNextStep`、`PlayTips`、`OnClose`；当前处于运行中的编辑器未产生晚于源码的完整 `Assembly-CSharp.dll`，因此完整目标程序集重建仍待退出运行态后确认。
- 静态调用链确认每次 `PlayTips` 在首次异步等待前同步加锁，逐字 Tween 等待结束后才按生命周期和请求版本解锁。
- 尚未在 PlayMode 实际验证首条/后续提示快速连点、空 key、Tween 被 Kill、外部关闭重开和 Android/iOS 横屏触摸。
- 回滚时恢复 `_isFirstTipPlaying` 与 `PlayFirstTipAsync(...)`，让后续 `PlayTips()` 不等待 Tween；prefab、数据和资源无需回滚。

## 2026-07-15 PostcardGuidePopup 重命名为 NormalGuidePopup

### 需求背景与影响范围

- 原 `PostcardGuidePopup` 类型、主脚本、Generated 脚本和脚本目录统一重命名为 `NormalGuidePopup`。
- 四个 Resources prefab 由 `PostcardGuidePopup_1/_2/_3/_4` 重命名为 `NormalGuidePopup_1/_2/_3/_4`，内部 GameObject 名称与阶段加载键同步更新。
- `GameVictoryPanel`、`PostcardPopup_PageCompleteProcess`、`InterstitialAdPolicy`、`GMPanel` 和相关 Postcard 管理日志/文档的类型与静态 API 调用同步更新；`IsShowPostcardGuidePopup(...)` 对应改为 `IsShowNormalGuidePopup(...)`。
- `PostcardStageStep` 及四阶段业务含义保持不变，本次是名称与资源键迁移，不调整引导时序。

### GUID、资源与 UI 生命周期

- 主脚本 meta GUID 保持 `41bbd43a25bea0546ba1e430ee281f0b`，`NormalGuidePopup_1.prefab` 的 `m_Script` 继续引用该 GUID。
- 主 prefab meta GUID 保持 `e9ddec4847148b7419c8449dfd50af57`，`NormalGuidePopup_2/_3/_4` 的 `m_SourcePrefab` 继续引用该 GUID；四个 prefab 各自 meta GUID 均未变化。
- `GetStagePrefabName(...)` 继续通过 `nameof(NormalGuidePopup)` 生成 `_1` 至 `_4`，与重命名后的 Resources 文件完全一致。
- Bear `UIManager.GetUI/OpenUI/DestroyUI`、重复打开防护、关闭 callback、Async API、提示点击锁和 Spine 表现逻辑均未改写。
- 不修改 DB/save、RemoteConfig、奖励、analytics、全局事件、广告、IAP 或平台 SDK。

### 验证记录

- C#、prefab 和当前维护文档中已无旧 `PostcardGuidePopup` 类型或资源键；历史风险报告未做追溯改写。
- Unity 生成的 `Assembly-CSharp.csproj` 只引用 `NormalGuidePopup.cs` 与 `NormalGuidePopup.Generated.cs`；Unity 已重新生成晚于源文件的 `Assembly-CSharp.dll/.pdb`。
- 目标程序集二进制可检出 `NormalGuidePopup+<PlayTips>` 与 `NormalGuidePopup+<ShowAsync>`，未检出旧类型对应状态机。
- 静态 YAML 核对四个 prefab 内部名称、主脚本 GUID 和三个变体的主 prefab GUID 均正确；`git diff --check` 与敏感项扫描通过。

### 剩余风险与回滚

- 尚未在 Unity PlayMode 依次打开四阶段 popup，验证 Resources 加载、重复打开、每条提示点击锁、关闭 callback 和 `ShowAsync` 取消；Android/iOS 横屏仍未验证。
- `Assets/Game/UI/Postcard/Sprites/PostcardGuidePopup.json` 属于未被当前资源引用命中的美术 JSON，本次按“脚本和 prefab”范围保留原名，是否继续改名需由其美术工具链单独确认。
- 回滚时把类型、静态 API、目录、脚本、Generated 脚本及四个 prefab 名称恢复为 `PostcardGuidePopup`，继续保留现有 meta GUID；DB、配置和数据无需迁移。

## 2026-07-17 GameTipsGuideStep 等待式弹窗接入

### 需求与原有基线

- `NormalGuidePopup` 原只公开 `PostcardStageStep` 的 `Create(...)` 与 `ShowAsync(...)`；`GameTipsGuideFlow` 的前两段文本使用独立 `GuideDialogue` prefab。
- 本次新增 `GameTipsGuideStep` 等待式入口，使首次提示剧情引导可复用同一套 Bear UI 关闭、取消和重复打开处理，不改变明信片阶段调用契约。

### 当前行为与边界

- `ShowAsync(PostcardStageStep, ...)` 与 `ShowAsync(GameTipsGuideStep, ...)` 先把阶段映射为 `NormalGuidePopup_{编号}`，再进入同一个私有等待实现；实际创建统一通过 `CreateByPrefabName(...)`。
- 新入口只由 `GameTipsGuideFlow` 调用，加载 `_20001/_20002` 两个 prefab；不新增 callback 列表、全局事件、DB/save、配置、analytics 或平台分支。
- 外部取消仍销毁本次弹窗并返回 `Cancelled`，同资源已打开仍返回 `AlreadyRunning`，创建失败仍返回 `Failed`。

### 验证、风险与回滚

- CodeGraph 确认两个阶段枚举重载均进入同一私有 `ShowAsync(string, ...)`，原 Postcard 两个调用点仍指向 `PostcardStageStep` 重载；静态 diff 与 prefab 映射检查通过。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly` 构建成功，结果为 0 个错误、1116 个项目既有警告。
- Unity PlayMode 的实际资源加载、关闭回调、外部取消、重复打开与 Android/iOS 横屏仍待验证。
- 回滚时移除 `GameTipsGuideStep` 重载和私有字符串分流，把 `PostcardStageStep` 方法恢复为原实现；明信片 prefab 与业务数据无需回滚。

## 2026-07-22 Spine Skin 设置 API

### 需求背景与影响范围

- `NormalGuidePopup` 需要向运行态调用方提供设置 Spine Skin 的公开入口，并约定未传名称时使用 `box_original`。
- 修改 `Assets/Game/Scripts/Guide/Scripts/NormalGuidePopup/NormalGuidePopup.cs` 与 `Assets/Game/Scripts/Guide/Scripts/Flow/GameTipsGuideFlow.cs`；不修改 prefab、Spine 资源、配置、DB 或存档。
- 原有行为基线为 prefab 静态保存 `SkeletonGraphic.initialSkinName=box_original`，弹窗本身没有可供调用方在创建后切换 Skin 的 API。

### 当前行为与调用边界

- 新增实例 API `SetSpineSkin(string skinName = "box_original")`，调用方可在取得弹窗实例后设置 Skin，并通过 bool 返回值判断是否成功。
- `ShowAsync(GameTipsGuideStep, CancellationToken, string skinName = "box_original")` 会把 Skin 名称传到新建弹窗；`GameTipsGuideFlow` 的开场弹窗明确传入 `box_rich`。
- API 复用现有 `UISpineCtrl.SetSkin(...)`，由该组件查找目标 Skin，并在成功后恢复 Slot Setup Pose、刷新 Spine 世界变换；不复制底层 Spine 切换逻辑。
- `skinName` 为空或空白、`UISpineCtrl` 未绑定且子节点查找失败、资源中不存在目标 Skin 时返回 false；前两类错误由 `NormalGuidePopup` 使用 Bear warning 记录。
- Skin 仅在 GameTips 等待式重载创建出弹窗后设置；设置失败保留 prefab 当前外观并继续引导。明信片 `PostcardStageStep`、`Create(...)` 与 `OnOpen()` 初始逻辑保持不变。

### 数据、资源、UI 与事件流

- 只操作当前弹窗已有的 `UISpineCtrl` 和已加载的 Spine Skeleton；不新增资源加载、实例化、全局事件或每帧逻辑。
- 不访问 DB、Save、RemoteConfig、analytics、广告、IAP 或平台 SDK；Android/iOS 共用同一运行态 UI 路径。

### 验证、风险与回滚

- 静态检查确认新增 API 仅解析现有 `UISpineCtrl` 并调用其 `SetSkin(...)`；GameTips 重载默认参数为 `box_original`，当前流程传入 `box_rich`，空值和缺失组件均有失败返回。
- `git diff --check` 通过；`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 构建通过，结果为 0 个错误、99 个项目既有警告。
- Unity PlayMode 的默认 Skin、有效自定义 Skin、无效 Skin 和 Android/iOS 横屏表现仍待验证。
- 回滚本次接入时移除 GameTips 重载的 `skinName` 参数、`CreateByPrefabName(...)` 的 Skin 转发和 `GameTipsGuideFlow` 的 `OpeningPopupSkinName` 参数即可；若完整回滚 API，再删除 `DefaultSpineSkinName` 与 `SetSpineSkin(...)`。prefab、Spine 资源、配置和数据无需回滚。

## 2026-07-24 DOText 前强制刷新 TMP

- `NormalGuidePopup.PlayTips(...)` 现在参考 `GameVictoryPanel.RefreshTips()`：向 `ContentTxt`
  写入完整本地化文本后执行 `UpdateFontAsset()` 与 `ForceMeshUpdate(true, true)`，随后清空并启动原有 `DOText`。
- 同一规则同步应用到 `GuideDialogue.PlayTip()` 的逐字动画分支；零时长或空文本分支保持直接显示。
- 本次不修改点击锁、异步生命周期、Spine、prefab、配置、存档、事件、资源或平台流程。
- 代码差异与静态敏感项检查通过；补齐 Unity 临时项目引用产物后，目标 C# 构建通过
  （0 个错误、99 个项目既有警告）。Unity PlayMode、回退字体与 Android/iOS 横屏仍待验证。
- 详细影响、验证缺口和回滚方式见
  `Assets/Game/Scripts/Guide/Guide_DOText_TMP强制刷新说明.md`。
