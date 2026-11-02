# Postcard_PageItem 功能说明

## 2026-07-14 transform 等待完成粒子触发

- 需求：页面 Animator `transform` 必须在完成粒子发射之后触发。
- `ApplyGrayImageState(...)` 为本次首次完成创建局部 `UniTaskCompletionSource`；grayImage Sequence 自然完成后异步等待 Popup 的 `PlayCompleteEffects()`，所有有效粒子调用 `Play(true)` 后才完成信号。
- `PlayCompleteTransformAsync()` 进入后先等待该信号，再定位 Animator 来源状态、触发 `transform` 和启动正文逐字 Tween；正文仍保持 1 秒延迟、0.7 秒时长，transform 阶段仍等待 1.7 秒后才进入 Chest。
- grayImage Sequence 在自然完成前被 Kill 时会解除信号，避免完成流程永久卡住；随后仅在页面仍 active 时允许继续 transform，且不补发过期粒子。PageItem 销毁则通过 destroy token 取消整个等待。
- 当前顺序为 grayImage 填充/回弹 → 显示满进度 Chest并调用 `ParticleSystem.Play(true)` → transform/文案 → Chest 开箱；不等待粒子系统完整生命周期。
- 本次只新增 PageItem 局部异步同步，不新增公开 API、序列化字段、全局事件、资源加载、存档或配置。C# 构建通过（99 warnings / 0 errors），Unity PlayMode 与 Android/iOS 横屏仍待验证。
- 回滚时删除 `_completeEffectsCompletionSource`、三个局部等待辅助方法及 transform 前等待，恢复 grayImage `OnComplete` 直接 fire-and-forget 粒子即可。
- `PlayCompleteTransformAsync()` 和 `PlayCompleteEffects()` 的旧注释因调用边界再次变化而同步修正，其他既有注释未改写。

## 2026-07-14 完成文案纳入 transform 与粒子后置（已被后续 transform 后置要求扩展）

> 当前实现继续满足本节要求，并新增 transform 等待粒子 `Play(true)` 的同步门槛。

- 需求：`contentTxt` 的逐字 Tween 必须由页面转换过程启动；`completeEffects` 必须等待 grayImage 填充与回弹全部完成。
- 原有行为：`ApplyCompleteVisualState(...)` 在 `Refresh()` 中创建正文 Tween；`ApplyGrayImageState(...)` 在创建 grayImage Sequence 前通知 Popup 播放粒子。
- 当前行为：`ApplyCompleteContentTextState(...)` 只同步未完成空文本或已完成完整文本；有效 `transform` trigger 发出后，`PlayCompleteContentTextAnim()` 才清空正文并启动延迟 1 秒、持续 0.7 秒的 `DOText`。`PlayCompleteTransformAsync()` 最少等待 1.7 秒，覆盖正文结束后才允许 Chest 流程继续。
- grayImage 的 0.7 秒填充和两次 0.2 秒 Yoyo 回弹保持不变；Sequence 自然完成后才调用 Popup 粒子入口，刷新打断、禁用或销毁导致的 Kill 不发射完成粒子。
- 本次未修改存档、扣星、奖励、Guide、prefab、Animator 资产、粒子绑定、资源加载或事件。C# 构建通过（99 warnings / 0 errors），静态顺序与目标 diff 检查通过。
- Unity PlayMode、长短文案节奏、快速关闭/刷新打断、粒子层级及 Android/iOS 横屏仍待验证。回滚时恢复 Refresh 内正文 Tween、1.5 秒等待和 grayImage Sequence 前粒子调用即可。
- `PlayCompleteTransformAsync()` 与 `PlayCompleteEffects()` 的旧注释因调用边界已与代码事实冲突而同步修正，其他既有注释未改写。

## 需求背景

2026-07-06 需求：参考 `Postcard_ProgressItem.AutoBind()`，在 `Postcard_PageItem` 上补充 Unity Editor Inspector 手动 `AutoBind` 按钮，用于按明信片页 prefab 层级补齐页面级本地序列化引用。当前绑定规则以 `Postcard_ocean.prefab` 的结构为主要证据。

2026-07-06 补充需求：按脚本注释补齐 `grayImage` 控制逻辑。普通明信片页未完成时 `fillAmount = 0`，首次完成时播放 `fillAmount` 从 0 到 1 的动画；`Postcard_unlock` 锁定占位页不处理 `grayImage`。

2026-07-06 追加需求：整页首次完成时，`Postcard_PageItem` 需要补齐完成态表现，并把完成回调交给 `PostcardPopup` 继续处理 Chest、领奖弹窗和自动切页逻辑。

## 影响范围

- 修改脚本：`Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs`
- 参考脚本：`Assets/Game/UI/Postcard/Scripts/Postcard_ProgressItem.cs`
- 灰度组件：`Assets/Game/UI/Postcard/Scripts/PostcardGrayImage.cs`
- 参考 prefab：`Assets/Game/UI/Postcard/Resources/Postcard_ocean.prefab`
- 相关组件：根节点 `Animator`、子级 `Postcard_ProgressItem`、`mainPage` 上的 `Image` / `PostcardGrayImage`、完成态节点 `Postcard_CompleteMode` 或 `complete`
- 本次未修改 prefab、配置表、存档字段、资源加载路径、全局事件、广告、IAP、analytics 或平台 SDK。

## 原有行为基线

- `Postcard_PageItem` 已负责页面运行时生命周期、进度 item 点击订阅、星星数量校验、`DB.Postcard` 进度保存、扣除 Star 和 `Postcard_ProgressItem.PlayUnlockAnim(Action)` 回调。
- 运行时 `ResolveRuntimeItems()` 在 `items` 未绑定或为空时会从子节点查找 `Postcard_ProgressItem`，并按 `Index` 排序。
- `Postcard_ocean.prefab` 的根节点已挂 `Animator` 和 `Postcard_PageItem`，`mainPage` 节点上有背景 `Image` 和 `PostcardGrayImage`，完成态来自 `Postcard_CompleteMode.prefab` 实例。
- 新增本次逻辑前，`Postcard_PageItem` 只序列化 `grayImage` 字段，不会主动调用 `PostcardGrayImage.SetFillAmount(...)`。

## 当前行为

- `Postcard_PageItem.AutoBind()` 仅在 `#if UNITY_EDITOR` 中编译，并通过 Odin `[Button("AutoBind")]` 暴露给 Inspector。
- 点击 AutoBind 时会补齐：
  - `anim`：当前 GameObject 上的 `Animator`。
  - `items`：当前页面子级中所有 `Postcard_ProgressItem`，包含 inactive，并按 `Index` 排序。
  - `bg`：名为 `mainPage` 的子节点上的 `Image`。
  - `grayImage`：优先取 `mainPage` 上的 `PostcardGrayImage`，缺失时回退当前页面子级第一个 `PostcardGrayImage`。
  - `completePart`：优先按 `Postcard_CompleteMode`、`completePart`、`CompletePart`、`complete`、`Complete` 名称查找；仍找不到时回退到不属于 `Root` / `main` / `mainPage` 且不包含进度 item 的直接子节点。
- 只有检测到字段值实际变化时才调用 `Undo.RecordObject`、`EditorUtility.SetDirty`，并在 prefab instance 上记录 property override。
- AutoBind 不改变 `SetData`、`OnShow`、`Refresh`、`OnExit`、解锁保存、扣星和动画回调的运行时行为。
- `Refresh()` 会按当前页完成状态驱动 `grayImage`：
  - 普通页未完成时停止旧 tween，并调用 `grayImage.SetFillAmount(0f)`。
  - 普通页从未完成首次变为完成时，使用 DOTween 驱动 `fillAmount` 从 0 到 1。
  - 普通页已经完成后再次显示或刷新时，直接设置 `fillAmount = 1`，避免重复播放完成动画。
  - `Postcard_unlock` 通过 `config.PrefabPath` 或实例名识别后直接跳过，不读取或写入 `grayImage`。
- `OnExit()`、`OnDisable()`、`OnDestroy()` 会停止页面级 `grayImage` tween，避免页面销毁后继续回写。
- 整页首次完成时，`Postcard_ProgressItem` 的单个 item 解锁动画结束后会执行一次 `Refresh(Action)`，刷新完成态本地表现后触发 `_onCompleted`；transform 等待由 `PostcardPopup` 在领奖、皮肤转换弹窗和可选 ShopPanel 关闭后触发。
- 完成态回调触发前，`PostcardPopup.IsPagePlayingAnim` 仍保持锁定；若 `_onCompleted` 已被 `OnExit()` 清空，页面会回退调用 `_owner.OnPagePlayAnim(false)`，避免交互锁残留。

## 数据与资源流

- AutoBind 只读取当前 `Transform` 层级和已存在的 Unity 组件，只写入当前 `Postcard_PageItem` 的本地序列化字段。
- AutoBind 不访问 `DB` / save / config / `RemoteConfig`，不加载 Resources，不创建 GameObject，不实例化 prefab，不派发 `PlayEvents` 或 Bear 全局事件。
- 运行时页面解锁链路仍沿用原 `Postcard_PageItem` 逻辑；完成态灰度动画只修改 `PostcardGrayImage` 的本地材质参数，不写入存档、不消耗星星、不发放奖励。奖励弹窗由 `_onCompleted` 交给 `PostcardPopup` 处理，transform 等待发生在领奖、皮肤转换弹窗和可选 ShopPanel 之后。

## 验证记录

- AutoBind 阶段 `git diff --check -- Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs` 已通过，仅提示工作区后续可能按 Git 设置进行 LF/CRLF 转换。
- CompleteAnim 完成回调阶段 `git diff --check -- Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs Assets/Game/UI/Postcard/Scripts/PostcardPopup功能说明.md Assets/Game/UI/Postcard/Scripts/PostcardPopup风险评估.md Assets/Game/UI/Postcard/Scripts/Postcard_PageItem功能说明.md Assets/Game/UI/Postcard/Scripts/Postcard_Chest功能说明.md Assets/Game/UI/Postcard/Scripts/Postcard_Chest风险评估.md DevelopMD/已有功能设计与使用记录.md` 已通过，仅提示工作区后续可能按 Git 设置进行 LF/CRLF 转换。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过，结果为 99 warnings / 0 errors。
- 静态检查确认本次新增 AutoBind 和灰度控制区域没有 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`Instantiate`、`RemoteConfig` 或全局事件链路；脚本中既有 `DB.Postcard` / `DB.SavePostcard()` 命中仍属于运行时解锁流程。

## 剩余风险

- 尚未在 Unity Editor Inspector 中实际点击 `AutoBind` 验证写入结果、Undo 和 prefab override 记录。
- 尚未在 Unity PlayMode 中验证普通页完成瞬间 `grayImage` 从 0 到 1 的视觉表现，以及 `Postcard_unlock` 不处理 `grayImage` 的实际展示。
- 尚未在 Unity PlayMode 中验证领奖、皮肤转换弹窗和可选 ShopPanel 都关闭后再播放 transform、以及页面退出时完成回调不会残留。
- 其他主题 prefab 的层级命名虽然已有 `Postcard_CompleteMode` / `complete` 线索，但本次绑定规则主要以 `Postcard_ocean.prefab` 为证据，仍需后续在 Unity Inspector 中抽样确认。
- `completePart` 的兜底规则依赖直接子节点结构；若未来页面直接子节点新增非完成态装饰物，建议优先保持完成态节点明确命名，避免误绑定。

## 回滚方式

- 删除 `Postcard_PageItem.cs` 中 `#if UNITY_EDITOR` 的 `Sirenix.OdinInspector` / `UnityEditor` using、`AutoBind()` 和相关编辑器辅助方法，即可回到仅运行时解析 `items` 的状态。
- 删除 `Postcard_PageItem.cs` 中 `DG.Tweening` using、页面级 gray tween 字段、`ApplyGrayImageState(...)`、`StopGrayImageTween()`、`IsUnlockPage()` 以及 `Refresh()` / 生命周期中的调用，即可移除本次完成态灰度控制。
- 删除本说明文件及其 `.meta` 可移除本次文档记录。

## 2026-07-08 version 1.1 完成态动画补充

### 需求背景

按 `Postcard_PageItem.cs` 顶部用户补充的 `version 1.1` 注释实现完成态表现：普通页未完成时保持 `isComplete=false`；从未完成转为完成时由 `PostcardPopup` 在领奖、皮肤转换弹窗和可选 ShopPanel 关闭后触发一次 `transform` trigger 并等待，完成态正文 `contentTxt` 执行一次 `DOText` 逐字填充；页面打开时如果已经完成，只设置 `isComplete=true`，不重复播放 transform 和文字动画。原有注释文本未改写。

### 影响范围

- 修改脚本：`Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs`
- 读取既有动画控制器：`Assets/Game/ArtSrc/Animations/Postcard/Postcard_Page.controller`
- 读取既有完成态 prefab：`Assets/Game/UI/Postcard/Prefabs/Postcard_CompleteMode.prefab`
- 相关运行时组件：根节点 `Animator`、完成态 `Postcard_CompleteMode`、其子级 TMP 文本 `context_txt`、既有 `PostcardGrayImage`
- 本次未修改 prefab、Luban 配置、存档字段、资源路径、全局事件、RemoteConfig、广告、IAP、analytics 或平台 SDK。

### 原有行为基线

- `Postcard_PageItem.Refresh()` 已能识别当前页完成状态，并在首次完成时驱动 `PostcardGrayImage.fillAmount` 从 0 到 1。
- 根节点 `Animator` 字段已序列化预留，但运行时未主动设置 `isComplete`，也未触发 `transform` trigger。
- 完成态 prefab 中存在 TMP 文本节点 `context_txt`，但 `Postcard_PageItem` 未绑定或播放该文本的逐字动画。

### 当前行为

- `AutoBind()` 现在会在找到 `completePart` 后同步绑定其子级第一个 `TMP_Text` 到 `contentTxt`；运行时未绑定时也会从 `completePart` 子级兜底查找。
- Inspector 中新增 `Test Complete Effect` 测试按钮；仅在 PlayMode 下可用，用于手动触发当前页完成态本地表现、`transform` 和固定等待窗口，不写存档、不发奖、不切页。
- 静态检查确认 `Postcard_Page.controller` 里的 `transform` trigger 只挂在 `postcard big_idle -> postcard b2s` transition 上，不是 AnyState transition；因此播放 transform 前会先用完整路径 `Base Layer.postcard big_idle` 把 Animator 定位到来源状态，并立即 `Update(0f)`，避免当前处于 `postcard idle`、进入态或完成态 idle 时 trigger 被状态机忽略。
- `OnShow()` 在 `Refresh()` 设置完当前完成态后，会触发一次 Animator `enter` trigger；该触发只在普通页且 Animator 存在对应 trigger 参数时执行。
- `Refresh()` 统一进入 `ApplyCompleteVisualState(...)`：
  - 普通页未完成时设置 Animator `isComplete=false`，停止完成态文字 tween，并把完成态正文清空。
  - 普通页从未完成首次变为完成时先保持 Animator `isComplete=false` 并准备完整正文；正文逐字 Tween 不在 Refresh 中创建，而由随后 `PlayCompleteTransformAsync()` 的 transform 过程启动。
  - 普通页已经完成后再次显示或刷新时直接设置 Animator `isComplete=true` 和完整正文，不重复触发 `transform` 或文字动画。
  - `Postcard_unlock` 仍通过 `IsUnlockPage()` 跳过完成态动画与文字逻辑。
- `OnExit()`、`OnDisable()`、`OnDestroy()` 统一停止灰度和正文 tween，避免页面销毁后继续回写 UI。
- 整页首次完成时，`Postcard_PageItem` 通过 `_onCompleted` 进入 Popup 完成流程；`PostcardPopup` 先调用页面 `Refresh()`，再调用 `PlayCompleteTransformAsync()` 触发 `transform` 和正文逐字动画，成功后才开启 Chest。当前等待取 1.5 秒 transform 窗口与 1 秒延迟加 0.7 秒正文 Tween 的较大值，即 1.7 秒。

### 数据、资源、UI 与事件流

- 数据流：只读取 `DB.Postcard` 当前完成状态，未新增或修改存档字段。
- UI 流：完成态表现只写入当前页面 `Animator` 参数、`PostcardGrayImage.fillAmount` 和 `TMP_Text.text`。
- 资源流：不新增资源加载；`contentTxt` 只从当前实例已有 `completePart` 子级解析。
- 事件与平台流：未新增 `PlayEvents` / 全局事件、每帧轮询、广告、IAP、analytics、RemoteConfig 或平台 SDK 链路。

### 验证记录

- CodeGraph 已重新读取 `Postcard_PageItem.cs`，确认 `ApplyCompleteVisualState(...)` 由 `Refresh()` 调用，`FindCompleteContentText(...)` 同时被 `AutoBind()` 和运行时兜底调用；暂无覆盖测试。
- 静态证据确认 `Postcard_Page.controller` 中 `isComplete` 参数类型为 bool，`transform` 和 `enter` 参数类型为 trigger。
- 静态证据确认 `Postcard_Page.controller` 的 `enter` 分支依赖 `isComplete`：`enter && isComplete` 进入完成页分支，`enter && !isComplete` 进入未完成页分支；首次完成转换时脚本不再提前把 `isComplete` 写成 true。
- 静态证据确认 `PostcardPopup_PageCompleteProcess.HandlePageCompleteAsync()` 在 `RefreshCompletedPage()` 后、`PlayChestOpenAnim()` 前调用 `_currentPage.PlayCompleteTransformAsync()`；页面方法触发 `transform` 和正文 Tween 后，按 `Mathf.Max(CompleteTransformWaitDuration, CompleteTextDelay + CompleteGrayFillDuration)` 等待，取消时释放 `PostcardPopup.IsPagePlayingAnim`。
- 2026-07-08 追加排查确认：仅使用短 state 名 `postcard big_idle` 仍可能无法让 Animator 在打 `transform` 前稳定进入来源状态；代码已改为完整 state path 并强制一次零时长更新。
- 静态证据确认 `Test Complete Effect` 按钮只在 `Application.isPlaying` 时调用 `ApplyCompleteVisualState(true, true, null)` 和 `PlayCompleteTransformAsync()`；未接入 DB/save、奖励弹窗或切页流程。
- 静态证据确认 `Postcard_CompleteMode.prefab` 中存在 `context_txt` TMP 文本节点。
- 静态敏感扫描 `Postcard_PageItem.cs` 未命中 `OnValidate`、`Reset(`、`ExecuteAlways`、`ExecuteInEditMode`、`Update(`、`LateUpdate(`、`FixedUpdate(`、`PlayEvents`、`DispatchEvent`、`Subscribe<`、`RemoteConfig`、`IAP`、`Ads`、`Analytics` 或 `PlayerPrefs`。
- `git diff --check -- Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs Assets/Game/UI/Postcard/Scripts/PostcardPopup_PageCompleteProcess.cs Assets/Game/UI/Postcard/Scripts/Postcard_PageItem功能说明.md Assets/Game/UI/Postcard/Scripts/PostcardPopup功能说明.md DevelopMD/已有功能设计与使用记录.md` 已通过，仅提示工作区后续可能按 Git 设置进行 LF/CRLF 转换。
- 旧流程描述扫描未发现过期流程说法。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过，结果为 99 warnings / 0 errors。

### 剩余风险与未验证项

- 尚未在 Unity PlayMode 验证首次完成时 `isComplete`、`transform` trigger、1.7 秒完整表现等待、grayImage 动画、粒子和 `contentTxt.DOText` 的实际播放顺序。
- 尚未在 Unity PlayMode 验证页面进入时 `enter` trigger 与 `isComplete` 条件组合后的实际动画分支。
- 尚未验证已经完成的页面重新打开时是否只显示完成态并跳过 transform / DOText。
- 尚未在 Android/iOS 横屏设备验证完成态动画、文字填充和 Chest 奖励弹窗等待时序。
- `contentTxt` 运行时兜底依赖 `completePart` 下存在 TMP 文本；若后续完成态 prefab 改用多个文本或改层级，需要同步明确绑定规则。

### 回滚方式

- 移除 `Postcard_PageItem.cs` 中 `contentTxt` 字段、`TMP_Text` 引用、`ApplyCompleteVisualState(...)`、Animator 参数设置、正文 `DOText` 逻辑和 `StopCompleteVisualTweens()`，并让 `Refresh()` 恢复为直接调用 `ApplyGrayImageState(...)`。
- 若只需要关闭正文动画风险，可保留 Animator 和 grayImage 逻辑，删除 `ApplyCompleteContentTextState(...)` 相关调用与字段。

## 2026-07-09 item 解锁粒子触发补充

### 需求背景

`PostcardPopup` 需要在单个 item 解锁时播放粒子，并且粒子位置应对齐到真实 `Postcard_PageItem` 中被解锁的 item。为避免普通点击和 guide 点击分叉，本次把触发点放在 `Postcard_PageItem.TryUnlockItem(...)` 的原有真实解锁入口内。

### 当前行为

- 解锁校验、`DB.SavePostcard()` 和扣星成功后，`Postcard_PageItem` 会读取当前 `Postcard_ProgressItem` 的 `RectTransform` 中心世界坐标。
- 页面通过 `_owner.PlayUnlockEffectsAsync(...).Forget()` 通知 `PostcardPopup` 延迟播放已绑定粒子，然后继续进入 `Postcard_ProgressItem.PlayUnlockAnim(...)` 和整页完成判断。
- guide 点击仍调用 `TryUnlockGuidedItemByListIndex(...) -> TryUnlockItem(...)`，因此同样使用真实页面 item 位置播放粒子。
- 本次不改变 star 校验、保存失败回滚、扣星、整页完成、奖励弹窗、guide 推进或完成态动画顺序。

### 验证与风险

- `git diff --check -- Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs`：通过，仅有工作区 LF/CRLF 转换提示。
- 静态敏感扫描未发现新增编辑器生命周期、每帧轮询、全局事件、RemoteConfig、广告、IAP、analytics 或 PlayerPrefs。
- `dotnet build .\Assembly-CSharp.csproj --no-restore ...` 被当前 `Temp\bin\Debug` 元数据缺失阻断，首个错误为 `Apple.Core.dll` 缺失，共 191 个 `CS0006`。
- Unity PlayMode、粒子绑定、普通解锁、guide 解锁、Android/iOS 横屏位置对齐仍待补验。

### 回滚方式

删除 `TryUnlockItem(...)` 中计算 `unlockEffectPosition` 并调用 `_owner.PlayUnlockEffectsAsync(...).Forget()` 的代码，即可恢复到只播放 item 自身解锁动画的旧行为；无需迁移数据。

## 2026-07-09 Diary 多语言正文补充

### 需求背景

`PostcardConfig` 新增 `Diary` 字段后，完成态 `contentTxt` 需要通过该多语言 key 填充，而不是继续读取 prefab 上的固定初始文本。

### 影响范围

- `Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs`
  - 增加 `I2.Loc.LocalizationManager` 读取。
  - `GetCompleteContentText(...)` 改为优先解析当前 `config.Diary`。
  - `contentTxt` 字段注释同步修正，因为原注释“不读取配置”已经与新需求冲突。
- 不修改 prefab、存档、奖励、资源加载路径、全局事件或平台 SDK。

### 行为说明

- 页面实例化后 `SetData(...)` 会继续清空 `_completeContentText`，确保每张明信片页按自己的 `config.Diary` 重新解析。
- `Diary` 非空时调用 `LocalizationManager.GetTranslation(diaryKey)`；翻译为空时显示 key 本身，便于暴露缺失词条。
- `Diary` 为空时保留旧行为，使用 `contentTxt.text` 初始文本兜底，降低未补配置页面的表现风险。
- 原 `DOText` 逐字填充、完成态直接显示、未完成清空文本和 `Postcard_unlock` 跳过逻辑保持不变。

### 验证记录

- 2026-07-09 `git diff --check -- Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs`：通过，仅有工作区 LF/CRLF 转换提示。
- 2026-07-09 敏感关键字扫描未发现本次新增编辑器生命周期、每帧轮询、全局事件、RemoteConfig、广告、IAP、analytics 或 PlayerPrefs；唯一命中为既有 Animator `Update(0f)`。
- 2026-07-09 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，99 warnings / 0 errors。

### 剩余风险与回滚

- 尚未在 Unity PlayMode 验证完成态正文在不同语言下的实际显示和 `DOText` 动画。
- I2 词条缺失时会显示 `Diary` key；仍需后续配置与多语言资源补齐验证。
- 回滚方式：恢复 `GetCompleteContentText(...)` 读取 `contentTxt.text` 的旧逻辑，并删除 `ResolveLocalizedCompleteContentText(...)` 与 `I2.Loc` using。

## 2026-07-10 grayImage 完成后缩放回弹补充

### 需求背景

普通明信片页首次完成时，`ApplyGrayImageState(...)` 在现有 `fillAmount` 填充表现之后补充一次 `DOScale` 缩放回弹，使图片完成显色后产生明确的收尾反馈。

### 影响范围

- 修改脚本：`Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs`。
- UI 目标：当前页 `grayImage.transform`，即各普通明信片 prefab 中挂载 `PostcardGrayImage` 的 `mainPage` 图片节点；`Postcard_food.prefab` 中该节点名为 `mianPage`。
- 本次不修改 prefab、Animator、shader、材质、配置、DB/save、资源加载、奖励、Guide、全局事件、广告、IAP、analytics、RemoteConfig 或平台 SDK。

### 原有行为基线

- 普通页首次从未完成变为完成且页面处于可播放状态时，现有工作区代码会把 `grayImage.FillAmount` 从 `0f` Tween 到 `.7f`，持续 `1s`，使用 `Ease.OutCirc`。
- 灰度填充结束后没有额外缩放反馈；历史完成页刷新时直接设置 `fillAmount = 1f`，未完成页设置为 `0f`，`Postcard_unlock` 跳过 grayImage 流程。
- `OnExit()`、`OnDisable()`、`OnDestroy()` 已统一停止页面完成态 Tween。

### 当前行为

- `ApplyGrayImageState(...)` 现在用同一个 DOTween `Sequence` 编排灰度填充和缩放回弹，保证缩放严格在 fill Tween 完成后开始。
- 回弹以图片节点进入序列时的 `localScale` 为原始比例，使用 `DOScale(originalScale * 1.1f, 0.2f)`、`Ease.OutCirc` 和两次 `LoopType.Yoyo`，播放结束回到原始比例。
- 页面退出、禁用、销毁或刷新打断序列时，`OnKill` 会恢复捕获的原始比例并清理 Tween 引用，避免重复刷新造成 scale 漂移。
- 历史完成页、未完成页、不可播放状态和 `Postcard_unlock` 的既有分支保持不播放回弹；本次不改变完成回调、正文 `DOText`、Chest、领奖或后续 `transform` trigger 时序。
- 缩放目标不使用页面 `Root`：静态动画资产证据显示现有 Animator 会写入 `Root.m_LocalScale`，对子级图片节点缩放可避免与 Animator 争用同一 Transform 属性。

### 验证记录

- CodeGraph 确认 `ApplyGrayImageState(...)` 仅由 `ApplyCompleteVisualState(...)` 调用，`StopGrayImageTween()` 同时覆盖页面退出/禁用/销毁路径；该方法暂无自动化测试覆盖。
- 静态 YAML 抽查 8 个普通明信片 prefab，`grayImage` 均绑定到图片节点且初始 `localScale = (1,1,1)`；`Postcard_unlock.prefab` 的 `grayImage` 为空。
- 同模块 `Postcard_ProgressItem.PlayUnlockPulse()` 已使用 `1.1f`、`0.2f`、两次 `Yoyo` 和 `Ease.OutCirc`，本次沿用相同回弹参数。
- `git diff --check -- Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs` 已通过，仅提示工作区后续可能按 Git 设置进行 LF/CRLF 转换。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过，结果为 0 error / 98 warnings，warnings 未逐项审计。
- 静态敏感扫描未发现新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、每帧轮询、全局事件、RemoteConfig 或 PlayerPrefs 链路；唯一 `Update(0f)` 命中为既有 Animator 状态定位逻辑。

### 剩余风险与未验证项

- 尚未在 Unity PlayMode 实际确认 `.7f` 灰度填充结束后才开始回弹、回弹幅度和节奏符合美术预期，以及快速退出/切页时图片比例正确恢复。
- 尚未在 Android/iOS 横屏设备验证完成态回弹的帧率、触控流程和不同屏幕适配表现。
- 当前没有覆盖该视觉序列的 Unity 自动化测试；静态检查和 C# 编译不能替代运行时视觉验收。

### 回滚方式

- 删除 `CompleteGrayScaleMultiplier`、`CompleteGrayScaleDuration` 和 `ApplyGrayImageState(...)` 中追加的 `DOScale`，将灰度 Tween 恢复为独立 `_grayImageTween`，即可移除回弹；无需迁移数据、资源或配置。

## 2026-07-13 completePart 完成态显隐约束

### 需求背景

`Postcard_PageItem` 的 `completePart` 只允许在当前明信片页处于 complete 状态时显示。用户口述中的 `Postcasd_ProgressItem` 经本地字段与调用链核对后，实际归属类是 `Postcard_PageItem`。

### 影响范围

- 修改脚本：`Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs`。
- UI 目标：各明信片页 prefab 已绑定的 `completePart`；未绑定时沿用现有 `FindCompletePart()` 本地层级兜底。
- 不修改 `Postcard_ProgressItem`、prefab、配置、DB/save、资源加载、完成奖励、Guide、全局事件、广告、IAP、analytics、RemoteConfig 或平台 SDK。

### 原有行为基线

- `Refresh(...)` 已通过 `DB.Postcard.CompletedId` 计算 `isCompleted`，并把该状态交给 Animator、完成正文和灰度图片表现。
- `completePart` 仅用于完成态正文绑定与文本解析，没有任何运行时代码按 `isCompleted` 控制其 active 状态；因此未完成页是否显示该节点取决于 prefab 初始状态。

### 当前行为

- `ApplyCompleteVisualState(...)` 先调用 `ApplyCompletePartState(isCompleted)`，统一同步完成态根节点显隐。
- 普通页面在 `isCompleted == true` 时显示 `completePart`，非 complete 状态隐藏 `completePart`。
- `Postcard_unlock` 是特殊锁定占位页，其 `completePart` 绑定的是主要显示图片 `UI_mingxinpian_img_unlock_01`；`ApplyCompletePartState(...)` 识别到 unlock page 后直接返回，保留 prefab 原有 active 状态。
- `completePart` 未绑定时沿用现有 `FindCompletePart()` 解析规则；找不到时安全跳过，不阻断进度 item、Animator、正文、灰度图或完成回调。
- 首次完成动画、历史完成页恢复、正文 `DOText`、灰度填充、奖励流程和存档判定顺序保持不变。

### 数据、资源与 UI 流

1. 页面 `Refresh(...)` 读取既有 `DB.Postcard.CompletedId` 并计算 `isCompleted`。
2. 普通页面由 `ApplyCompleteVisualState(...)` 把该布尔状态同步到当前页面本地 `completePart.SetActive(...)`；unlock page 跳过该同步。
3. 后续 Animator、正文和 grayImage 继续使用同一个 `isCompleted` 执行原有表现。

本次不新增数据读取、写入、资源加载、实例化或事件链路。

### 验证记录

- `git diff --check -- Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs Assets/Game/UI/Postcard/Scripts/Postcard_PageItem功能说明.md DevelopMD/已有功能设计与使用记录.md`：通过，仅有工作区 LF/CRLF 转换提示。
- 目标脚本敏感关键字扫描只命中既有 `DB.SavePostcard()` 和 Animator `Update(0f)`；本次新增逻辑未引入编辑器生命周期、每帧轮询、全局事件、RemoteConfig、PlayerPrefs、实例化或新的 save 链路。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，98 warnings / 0 errors；warnings 未逐项审计。
- 2026-07-13 回归修复静态证据：`PostcardPopup.ShowUnlockPage()` 会实例化 `Postcard_unlock` 并调用 `Postcard_PageItem.OnShow()`；该 prefab 的 `completePart` 绑定到 active 的 `UI_mingxinpian_img_unlock_01`。修复后该路径由 `IsUnlockPage()` 保护，不再执行 `SetActive(false)`。
- 2026-07-13 加入 unlock page 特例保护后复跑同一 `dotnet build` 命令：通过，98 warnings / 0 errors；`Postcard_unlock.prefab` 工作区状态保持未修改。

### 剩余风险与未验证项

- 尚未在 Unity PlayMode 验证未完成页、历史完成页和首次完成转换过程中的实际显隐时序。
- 尚未在 Android/iOS 横屏设备验证该完成态节点的最终画面；本次仅切换已有本地节点，不引入持续运行开销。
- 普通页面未绑定且层级命名不符合现有查找规则时会安全跳过显隐同步，仍需通过 prefab Inspector 抽样确认绑定完整性。

### 回滚方式

- 删除 `ApplyCompleteVisualState(...)` 中的 `ApplyCompletePartState(isCompleted)` 调用及该私有方法，即可恢复由 prefab 初始 active 状态决定 `completePart` 显隐的旧行为；若只回滚 unlock page 特例保护，删除 `ApplyCompletePartState(...)` 开头的 `IsUnlockPage()` 返回分支即可。无需迁移数据、资源或配置。

## 2026-07-14 首次完成刷新职责后移

- 最后一个进度项解锁动画结束后，`Postcard_PageItem` 不再立即调用 `Refresh(callback)`；它先通知引导解锁稳定，再触发既有 `_onCompleted` 进入 Popup 的 Chest/领奖流程。
- 当前页完成态刷新职责仍由 `PostcardPopup` 承担，但后续需求已把执行点从 `CommonGetRewardPopup` 关闭回调前移到 Chest 开启前。若 PageItem 没有绑定 `_onCompleted`，仍保留本地 `Refresh()` 与交互解锁降级，避免独立使用时卡住。
- 本次不改变完成判定、`CompletedId` 写入、星星扣除、保存失败回滚或 item 解锁动画。C# 构建通过（98 warnings / 0 errors）；Unity PlayMode 的首次完成视觉顺序仍待验证。
- 回滚时把完成分支恢复为 `Refresh(callback)` 包裹 `onStable` 与 `_onCompleted` 调用即可。

## 2026-07-14 PageComplete 调用边界修正

- `PlayCompleteTransformAsync()` 的实现、Animator 参数和 1.5 秒等待未改；仅由 `PostcardPopup` 将调用点前移到页面完成视觉刷新之后、Chest 开启之前。
- 原公开方法注释写成“奖励流程结束后调用”，已与当前代码事实冲突，因此同步改为“刷新完成视觉后、Chest 开启前调用”；未改写其他既有注释。
- C# 构建通过（99 warnings / 0 errors），静态顺序与目标 diff 检查通过；Unity PlayMode 和 Android/iOS 横屏视觉仍待验证。
- 回滚只需恢复 Popup 的调用顺序及该方法注释，不涉及 PageItem 数据、资源或 Animator 资产迁移。

## 2026-07-14 transform 等待 CompleteEffect 结束后再停留 2 秒

- PageItem 的局部 `UniTaskCompletionSource` 接线不变；变化发生在 Popup 的 `PlayCompleteEffects()` 返回边界：它现在代表全部有效粒子结束并额外停留 2 秒，而不再只代表 `Play(true)` 已调用。
- 因此 `PlayCompleteTransformAsync()` 的正常入口顺序更新为 grayImage 完成 → completeEffects 完整结束 → 2 秒 → Animator `transform` 与正文逐字；随后才进入 Chest。
- 本次不改变 PageItem 的完成判定、Animator 参数、正文时长、DB/save、扣星、资源加载或事件链路。C# 构建通过（99 warnings / 0 errors）；Unity PlayMode 和移动端横屏视觉仍待验证。
- 回滚 Popup 的粒子完整等待即可恢复原信号时机，PageItem 无需额外迁移。

## 2026-07-14 transform 仅等待 CompleteEffects 发射

- PageItem 的局部 `UniTaskCompletionSource` 接线保持不变，但 Popup 的 `PlayCompleteEffects()` 现在在全部有效 `Play(true)` 调用后立即返回。
- 因此 `PlayCompleteTransformAsync()` 只保证 transform 晚于 grayImage Sequence 和粒子发射，不再等待循环 duration、残留粒子或额外停留；transform/正文仍保持既有 1.7 秒等待。
- 本次不改变 PageItem 完成判定、Animator 参数、正文 Tween、DB/save、扣星、资源加载或事件。C# 构建通过（0 warnings / 0 errors）；Unity PlayMode 和移动端横屏仍待验证。
- 回滚 Popup 的非阻塞粒子入口即可恢复旧信号时机，PageItem 无需迁移。

## 2026-07-14 最终进度先刷新满进度 Chest

- 最后一个进度 item 的解锁动画稳定后，PageItem 在既有 `_onCompleted` 触发整页完成前调用 Popup 的 `PreparePageCompleteChest()`；Chest 因此与普通进度刷新处于同一类稳定回调边界，但不会把 `IsPagePlayingAnim` 提前设为 false。
- 该调用只在已绑定 `_onCompleted` 的完整 Popup 流程执行；独立使用且没有完成回调时仍保持原本 `Refresh()` 与 `OnPagePlayAnim(false)` 降级。
- 完成判定、`CompletedId` 写入、`CurrentCollectedPuzzledId` 清理、保存失败回滚、扣星、引导稳定回调和 item 动画均未改变。
- 构建通过（99 warnings / 0 errors），目标 diff 检查通过；Unity PlayMode 的最终 item、普通/引导完成与快速关闭仍待验证。
- 回滚时删除最终完成分支中的 `PreparePageCompleteChest()` 调用即可，PageItem 数据结构无需迁移。

## 2026-07-20 CompletePart 延迟至 CompleteEffect 后显示

### 需求与原有行为

- 需求：首次完成明信片页时，`completePart` 必须在 `completeEffect` 流程返回后才显示；不要求新增或修改公开 API。
- 原有行为：`PostcardPopup` 在完成流程开始时调用页面 `Refresh()`，`ApplyCompletePartState(...)` 会立即按 `CompletedId` 显示 `completePart`，早于 grayImage、完成粒子和 transform。

### 当前行为与影响范围

- 首次完成刷新通过 `playCompleteAnim` 暂缓显示 `completePart`；grayImage 填充和回弹仍由原 `Refresh()` 启动，随后继续调用既有 `PlayCompleteEffects()`。
- `PlayCompleteTransformAsync()` 等待 `WaitCompleteEffectsAsync()` 返回后，以私有方法显示 `completePart`，再播放 Animator `transform` 和完成文案；Animator 缺失时也会先完成该等待与显示，再安全返回。
- 已完成页面重新打开时 `playCompleteAnim=false`，仍在普通 `Refresh()` 中立即显示；未完成页继续隐藏，`Postcard_unlock` 继续跳过该逻辑。
- 只修改 `Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs` 的私有表现时序；不修改公开 API、prefab、Animator、ParticleSystem、配置、DB/save、扣星、奖励、资源加载、全局事件、analytics 或平台 SDK。

### 验证、风险与回滚

- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，99 warnings / 0 errors；warnings 未逐项审计。
- 代码目标 `git diff --check` 通过，仅有工作区 LF/CRLF 转换提示。静态顺序为 `Refresh → grayImage → PlayCompleteEffects → 显示 completePart → transform/正文 → Chest`。
- `PlayCompleteEffects()` 当前语义仍是播放有效粒子后固定等待 2 秒，不代表全部粒子已经完全消失；Unity PlayMode 中的实际层级、无粒子绑定降级、快速关闭以及 Android/iOS 横屏表现仍待验证。
- 回滚时恢复 `ApplyCompleteVisualState(...)` 直接按 `isCompleted` 显示 `completePart`，并移除 `PlayCompleteTransformAsync()` 等待后的显示调用；无需迁移数据、配置或资源。

## 2026-07-20 Transform 完成窗口后播放正文 DOText

### 需求与原有行为

- 需求：页面 Animator `transform` 播放完成后，再立即启动完成正文的 `DOText` 动画，不增加额外 delay。
- 原有行为：触发 `transform` 后同帧调用 `PlayCompleteContentTextAnim()`；transform 的 1.5 秒等待与正文的 1 秒延迟、0.7 秒逐字 Tween 并行，整体按两者较大值 1.7 秒后进入 Chest。

### 当前行为与影响范围

- `PlayCompleteTransformAsync()` 触发 `transform` 后先等待既有 `CompleteTransformWaitDuration` 1.5 秒，再立即创建正文 Tween；正文只保留 `CompleteGrayFillDuration` 0.7 秒逐字播放，不再调用 `SetDelay(...)`。
- 首次完成的 `Refresh()` 通过 `playCompleteAnim=true` 立即把 `contentTxt` 清空，因此 grayImage、completeEffect 和 transform 阶段正文都保持为空；transform 等待结束后由 `PlayCompleteContentTextAnim()` 立即启动逐字显示。历史已完成页重新打开时仍直接显示完整正文。
- 当前静态顺序为 `completeEffect → 显示 completePart → transform 1.5 秒 → DOText 0.7 秒 → Chest`，正常路径总等待为 2.2 秒。
- 本次只修改 `Postcard_PageItem.cs` 的私有等待编排及已与代码事实冲突的公开方法注释；未修改当前工作区已有改动的 `Postcard_Page.controller`、`postcard big_in.anim`、`postcard small_in.anim`，也未修改公开 API、prefab、配置、DB/save、奖励、资源、事件或平台链路。

### 验证、风险与回滚

- 当前“transform 完成”仍采用项目既有固定 1.5 秒表现窗口，不读取 Animator State 实际长度，也不增加每帧轮询；如果动画资源时长或速度变化，需同步确认 1.5 秒是否仍准确。
- 静态检查确认首次完成分支在 completeEffect 前已把 `contentTxt` 设为空，历史完成分支仍显示完整正文；`git diff --check` 通过，仅有工作区 LF/CRLF 转换提示；C# 构建通过（99 warnings / 0 errors），warnings 未逐项审计。
- Unity PlayMode 中的实际 Animator 完成点、长短文案、快速关闭以及 Android/iOS 横屏节奏仍待验证。
- 回滚串行时序时恢复触发 `transform` 后立即调用 `PlayCompleteContentTextAnim()`，并把两个顺序等待合并回 `Mathf.Max(CompleteTransformWaitDuration, CompleteGrayFillDuration)`；若仅恢复正文 delay，则重新增加 `CompleteTextDelay` 和 Tween 的 `SetDelay(...)`。无需迁移数据或资源。

## 2026-07-20 CompletePart 激活状态改由脚本单点控制

### 需求与问题证据

- 需求：进入未完成明信片页时，`completePart` 必须保持隐藏；首次完成时仍只在 `completeEffect` 等待结束后显示。
- 运行反馈：关闭 `Postcard_Page.controller` 全部 State 的 Write Defaults 后，未完成页的 `completePart` 仍显示。
- 静态证据：`Postcard_CompleteMode.prefab` 根节点原本默认 `m_IsActive: 1`；同时 `postcard b2s.anim` 存在 `Postcard_CompleteMode.m_IsActive` 曲线，而运行时代码已经在 `Refresh()` 和 `PlayCompleteTransformAsync()` 内显式管理同一节点的显隐。资源与脚本同时拥有激活状态，存在初始化或 Animator 重绑定覆盖脚本状态的冲突。

### 当前行为与影响范围

- `Postcard_CompleteMode.prefab` 根节点默认改为 inactive，普通页面实例化后不会在脚本首次刷新或 Animator 初始化前先显示完成层。
- `UI_mingxinpian2_img_zs_01` 的 prefab 默认透明度改为 0，与 `postcard b2s.anim` 和 `postcard small_in.anim` 的第 0 帧透明度一致；完成节点激活到 Animator 首次采样之间不再先按 alpha 1 闪现一帧，后续仍由原动画渐显到 1。
- 删除 `postcard b2s.anim` 对 `Postcard_CompleteMode.m_IsActive` 的运行时曲线、编辑器曲线和对应 ClipBinding；b2s 仍保留原有位置、透明度等表现。
- 完成节点显隐只由 `Postcard_PageItem` 控制：未完成页 `Refresh()` 设为隐藏，历史完成页设为显示，首次完成页在 `completeEffect` 等待结束后由 `ApplyCompletePartState(true)` 显示。
- 不改变完成判定、`CompletedId`、星星扣除、奖励、资源加载、事件、analytics 或平台链路；不新增公开 API。

### 验证、风险与回滚

- 静态检查已确认 Postcard 页面 prefab 没有覆盖完成节点根物体的 `m_IsActive` 或目标 Image 的 `m_Color.a`，Postcard 动画目录也不再存在任何 `m_IsActive` 绑定；b2s 与 small_in 对目标 Image 的第 0 帧 alpha 均为 0。目标 `git diff --check` 通过，仅有工作区 LF/CRLF 转换提示。
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，0 warnings / 0 errors。该构建不能验证 Animator 实际首帧；未完成页、历史完成页、首次完成页和 Android/iOS 横屏表现仍需 Unity PlayMode 验证。
- 若需要回滚，恢复 `Postcard_CompleteMode.prefab` 根节点为 active、目标 Image 默认 alpha 为 1，并恢复 `postcard b2s.anim` 的 `m_IsActive` 曲线及 ClipBinding；脚本和存档无需迁移。
