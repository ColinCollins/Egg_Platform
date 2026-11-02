# PostcardPopup 页面流功能说明

## 2026-09-09 GM 单张解锁后的页面刷新

- 背景：GM 单张解锁初版只更新存档。Editor.log 已记录真实按钮点击和成功保存分支，但未调用已打开 Popup 的页面刷新。
- 新增内部只读成员 `CanApplyGmUnlock`，仅在运行态、弹窗打开且无页面动画、领奖或引导时允许 GM 修改进度；由 GM 写入前检查。
- 新增内部 `RefreshAfterGmUnlock(postcardId)`，仅在上述状态允许且目标页已经完成时调用 `ShowConfigPage(index, false)`，复用页面资源、完成态、Chest 和翻页按钮刷新；不另存 CurrentId、不调用 OnPageComplete、不补发奖励。返回值检查新页面是否实际创建，失败由 GM 提示重开。
- 影响仅为 `PostcardPopup.cs` 与 GM 单张按钮调用链，不修改玩家正常解锁、自动定位、引导或领奖流程。关闭后再次打开仍按既有 TryResolveInitialIndex 选择未完成页。
- 完整运行时 Roslyn 编译 0 error，日志 `Temp/GMPostcardRefreshCheck.log`；用户随后确认有效。页面加载失败、动画/领奖/引导门禁、缓存重开及 Android/iOS 触控仍为 `uncertain`，未逐项实测。
- 回滚时移除两个 GM 专用成员及 GM 调用，已保存的完成数据不随代码回滚恢复。完整记录见 `DevelopMD/GMPanel_Reference.md` 第 23 节。

## 2026-07-18 对齐 GuideFlow 的 Popup 首帧 ClickBlock

### 需求、修改前后与影响范围

- 需求：保留 Popup 持有 Flow2 的结构，同时对齐 develop 的 `StartGuding()` 前置与 Popup 首帧阻挡要求。
- 修改前：Flow1 创建 Popup 后通过局部 API 开启引导锁；`OnOpen()` 本身不读取 `DB.Postcard.IsGuiding`，因此首帧状态依赖创建调用方后续补锁。
- 修改后：Flow1 在创建 Popup 前写入 `HasGuide/IsGuiding`；`OnOpen()` 在 `LoadInitialPage()` 前读取 `IsGuiding` 并调用 `SetGuidingState(...)`。引导 Popup 首帧即开启 ClickBlock，普通 Popup 的 `IsGuiding=false` 会明确关闭复用实例的旧引导态。
- Flow1 创建后仍幂等补锁，Flow2 启动、页面完成通知、引导切页和最终解锁职责不变；ClickBlock 条件继续是 `_isGuiding || IsPagePlayingAnim`。
- 本次只修改 `PostcardGuideFlow.cs` 和 `PostcardPopup.cs`；不修改 `PostcardPopup.prefab`、配置、资源地址、奖励、事件、analytics、广告、IAP 或平台 SDK。

### 验证、风险与回滚

- C# 构建通过：0 error / 99 warnings；静态检查确认 `StartGuding()` 位于 `PostcardPopup.Create()` 前，Popup guiding 初始化位于页面加载前，目标 `git diff --check` 通过。
- Unity PlayMode 的首帧射线、普通 Popup 复用、Stage Two 中断、快速关闭重开和 Android/iOS 横屏触摸尚未验证；当前工作区 prefab 有任务开始前的用户未提交改动，本次未覆盖。
- 回滚时删除 `OnOpen()` 的 guiding 初始化，并把 `StartGuding()` 恢复到 Stage Two 后；不涉及数据字段或资源迁移。

## 2026-07-17 CompleteEffects 发射后等待 2 秒

### 需求、范围与行为变化

- 需求：为了试看整页完成节奏，`completeEffects` 发射后先等待 2 秒，再触发 Animator `transform` 与正文逐字。
- 影响文件：`Assets/Game/UI/Postcard/Scripts/PostcardPopup_PageCompleteProcess.cs`。不修改 `PostcardPopup.prefab`、ParticleSystem/Animator/Spine 资产、配置、DB/save、奖励、资源地址、全局事件、analytics 或平台 SDK。
- 修改前：grayImage 填充和回弹完成后，全部有效粒子调用 `Play(true)` 便立即释放 PageItem 的局部完成信号，`transform` 与粒子同帧衔接。
- 修改后：至少一个有效粒子发射后，`PlayCompleteEffects()` 通过 Popup 销毁 token 等待固定 2 秒，返回后才释放局部信号并继续 `transform`。列表为空或没有有效绑定时不增加等待。
- 循环粒子仍在后台播放自身 `duration` 后执行 `StopEmitting`；新等待不检查 `IsAlive(true)`，不保证所有根及子粒子已完全消散。

### 验证、风险与回滚

- 静态时序为：grayImage 填充/回弹 → `completeEffects.Play(true)` → 固定等待 2 秒 → Animator `transform`/正文 → `chestOpen`。
- 等待沿用 scaled time，与当前 grayImage、Animator 和页面完成表现的时间基准一致；每次整页完成只创建一次本地 UniTask 等待，不新增每帧轮询或对象实例。
- `git diff --check` 通过；`dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过：0 errors / 100 warnings，warnings 为当前工程完整输出，本次未逐项审计。
- 待验证：Unity PlayMode 中的 2 秒观感、粒子与 Chest 层级、快速关闭/销毁取消，以及 Android/iOS 横屏性能。
- 回滚：删除 `CompleteEffectsPostDelay` 常量和 `PlayCompleteEffects()` 末尾的 `WaitForSeconds` 即可恢复发射后立即 `transform`；不涉及数据或资源迁移。

## 2026-07-17 打开时定位首个未完成页

### 需求、范围与行为变化

- 需求：`PostcardPopup` 打开时检查 `DB.Postcard.CurrentId`；若该 ID 对应普通明信片已经存在于 `CompletedId`，则按 `PostcardConfig.Sort` 升序定位首个未完成普通页。
- 修改文件：`Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs`。配置仍由 `TryLoadPostcardConfigs()` 排除 unlock 占位配置后按既有 `ComparePostcardConfigSort` 排序；同 `Sort` 继续按 ID 做稳定排序。
- 修改前：有效 `CurrentId` 无论是否完成都直接作为初始页，因此重新打开会停留在已完成页。
- 修改后：有效且未完成的 `CurrentId` 保持不变；有效且已完成时，选择排序列表中的首个未完成页，并复用 `SaveCurrentId(...)` 更新存档后显示。全部普通页均已完成时仍停留原当前页；无效或空 `CurrentId` 继续沿用原逻辑初始化为首个配置。

### 数据、UI、平台、验证与回滚

- 数据流仅复用 `DB.Postcard.CurrentId`、`CompletedId`、Luban `PostcardConfig.Sort` 和既有 `DB.SavePostcard()`；不新增配置、字段、资源加载、全局事件、奖励、分析打点或平台 SDK 分支。
- 自动目标保存失败时 `SaveCurrentId(...)` 会恢复原 ID，弹窗继续显示原完成页，避免初始化空白。该检查只在打开初始化执行一次，不新增 `Update`、实例化循环或持续 GC；Android/iOS 横屏布局与触摸结构不变。
- 已执行 C# 工程构建、`git diff --check` 和目标静态检查；Unity PlayMode、真实存档保存失败注入、全部完成、同 `Sort` 配置及 Android/iOS 横屏重开尚待手工验证。
- 回滚：删除 `TryResolveInitialIndex(...)` 中“当前页已完成后扫描首个未完成配置”的分支，即恢复始终按有效 `CurrentId` 打开；无需迁移存档或配置。

## 2026-07-14 transform 后置到完成粒子发射之后

### 需求、范围与当前流程

- 需求：Animator `transform` 页面转换必须放在完成粒子表现之后。
- 当前首次完成顺序：最后一个进度 item 动画结束 → 显示 Chest、刷新奖励与 `max/max` → 触发整页完成 → grayImage 0.7 秒填充 → 两次 0.2 秒 Yoyo 回弹 → 遍历 `completeEffects` 调用 `ParticleSystem.Play(true)` → 立即触发 Animator `transform` → 文案延迟 1 秒并逐字播放 0.7 秒 → Chest 开启；开箱 Complete/End 后不再额外停留。
- `PlayCompleteTransformAsync()` 通过 PageItem 内部 `UniTaskCompletionSource` 等待 grayImage Sequence 与 `PlayCompleteEffects()` 返回，不使用固定猜测延时，也不新增全局事件。
- “粒子表现之后”当前定义为所有非空完成粒子已经调用 `Play(true)`；不等待各 ParticleSystem 的完整生命周期结束。

### 异常边界、验证与回滚

- Popup/PageItem 销毁会取消 transform 等待并停止后续 Chest；grayImage Sequence 若被刷新或禁用提前 Kill，会解除局部等待，避免流程永久卡住，但该降级路径不会补发完成粒子。
- 未修改 prefab、ParticleSystem 参数、Animator 资产、DB/save、奖励、Guide、资源地址、全局事件、分析打点或平台 SDK。
- C# 构建通过：99 warnings / 0 errors；静态顺序和目标 diff 检查通过。Unity PlayMode、粒子真实可见帧与 transform 衔接、快速关闭及 Android/iOS 横屏仍待验证。
- 回滚：移除 `WaitCompleteEffectsAsync()` 与完成信号，在 transform 触发后继续由 grayImage `OnComplete` 独立播放粒子；无需迁移数据、配置或资源。

## 2026-07-14 PageComplete 文案与 grayImage 粒子时序修正（已被后续 transform 后置要求扩展）

> 当前实现仍保留“文案属于 transform、粒子位于 grayImage 完成后”，并进一步要求 transform 等到 `ParticleSystem.Play(true)` 已调用后才触发。

### 需求、范围与行为变化

- 需求：完成文案逐字播放归入页面 `transform` 转换过程；完成粒子必须等 `grayImage` 的填充与缩放回弹全部结束后再触发。
- 影响脚本：`Postcard_PageItem.cs` 调整本地 DOTween 编排，`PostcardPopup_PageCompleteProcess.cs` 仅同步修正粒子入口注释。未修改 prefab、Animator、ParticleSystem 绑定、配置、DB/save、奖励、资源地址、全局事件、分析打点或平台 SDK。
- 修改前：`Refresh()` 在启动 grayImage 时同时创建带 1 秒延迟的 `contentTxt.DOText`；完成粒子在 grayImage Sequence 建立前就调用，早于填充和回弹。
- 修改后：`Refresh()` 只准备正文静态状态；`PlayCompleteTransformAsync()` 触发 Animator `transform` 后调用 `PlayCompleteContentTextAnim()`，正文延迟 1 秒开始、0.7 秒完成。完成流程等待取 transform 1.5 秒与正文 1.7 秒的较大值，因此 Chest 不会早于正文结束启动。
- grayImage Sequence 仍依次执行 0.7 秒填充、材质恢复和两次 0.2 秒 Yoyo 回弹；仅在整个 Sequence 的 `OnComplete` 中调用 `PlayCompleteEffects()`，该入口直接发射粒子，不再增加粒子前置等待。

### 验证、风险与回滚

- C# 构建通过：99 warnings / 0 errors；静态检查确认 `DOText` 只从 transform 流程启动，完成粒子只从 grayImage Sequence 的 `OnComplete` 调用。
- Unity PlayMode、不同语言正文长度、不同主题 Animator 时长、粒子视觉落点及 Android/iOS 横屏仍待验证；DOTween 使用既有 scaled-time 行为。
- 回滚：把正文 Tween 恢复到 `ApplyCompleteContentTextState(...)`，恢复 1.5 秒固定等待，并把完成粒子调用移回 grayImage Sequence 启动前；不涉及数据、配置或资源迁移。

## 2026-07-14 PageComplete 动画前移到 Chest 开启前

### 需求、范围与行为变化

- 需求：整页完成时，当前页的 PageComplete 完成视觉与 `transform` 动画必须先播放，之后才进入 `chestOpen`。
- 影响脚本：`PostcardPopup_PageCompleteProcess.cs`；`Postcard_PageItem.cs` 只同步修正 `PlayCompleteTransformAsync()` 的调用边界注释。未修改 prefab、Animator、Spine、配置、DB/save、奖励内容、资源地址、全局事件、打点或平台 SDK。
- 修改前：`HandlePageCompleteAsync()` 先准备并开启 Chest，等待奖励、Skin 和可选 ShopPanel 结束后才播放当前页 `transform`；完成视觉 `Refresh()` 位于领奖关闭回调。
- 修改后：完成入口先调用 `RefreshCompletedPage()`，再等待 `_currentPage.PlayCompleteTransformAsync()`；成功后才调用 `PrepareChestForPageComplete()` 和 `PlayChestOpenAnim()`，随后沿用隐藏 Chest、领奖、Skin、Shop 和引导收尾流程。领奖关闭回调只负责放行等待，不再重复刷新页面。
- 当前顺序：页面完成视觉刷新 → 页面 `transform` → Chest 满进度准备与 `chestOpen` → 隐藏 Chest → 领奖 → Skin/Shop → 释放交互锁并停留当前页。

### 验证、风险与回滚

- 静态顺序检查确认 `RefreshCompletedPage()`、`PlayCompleteTransformAsync()`、`PrepareChestForPageComplete()`、`PlayChestOpenAnim()`、`WaitRewardPopupCloseAsync()` 按上述顺序出现；目标 diff 检查通过。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过：99 warnings / 0 errors。
- Unity PlayMode、实际 Animator/Spine 视觉衔接及 Android/iOS 横屏尚未验证；需要补验普通/引导完成、有无奖励、Skin/Shop 分支、快速关闭和历史完成页重开。
- 回滚：把页面刷新与 `PlayCompleteTransformAsync()` 恢复到奖励/Skin/Shop 之后，并恢复领奖关闭回调中的 `RefreshCompletedPage()`；无需迁移数据、配置或资源。

## 2026-07-13 完成变形后取消自动翻页

### 需求与影响范围

- 需求：`PostcardPopup` 在当前页完成变形动画结束后保留当前页，不再自动翻到下一张普通明信片。
- 修改文件：`Assets/Game/UI/Postcard/Scripts/PostcardPopup_PageCompleteProcess.cs`；不修改 prefab、配置、DB/save、资源地址、全局事件、打点或平台 SDK。

### 行为前后与流程

- 修改前：`HandlePageCompleteAsync()` 等待 Chest、领奖弹窗、Skin 转换弹窗、可选 ShopPanel 和 `_currentPage.PlayCompleteTransformAsync()` 后，调用 `ShowNextPageAfterComplete()`；存在下一条普通配置时通过 `ShowConfigPage(nextConfigIndex, true)` 自动切页并保存新的 `CurrentId`。
- 修改后：完成变形等待结束后直接进入 `finally`，释放 `_isHandlingPageComplete` 与 `IsPagePlayingAnim`，刷新按钮状态并保留当前完成页。若存在下一张普通页，玩家仍可点击 `NextBtn`，沿用原有手动翻页和 `CurrentId` 保存流程。
- Chest、奖励发放、Skin 展示、ShopPanel 等待、页面完成变形、Guide 收尾、资源加载和手动上一页/下一页流程不变。

### 验证、风险与回滚

- 已通过静态调用链检查：完成流程不再引用 `ShowNextPageAfterComplete()`，该私有方法已删除；`ShowConfigPage(...)` 仍由初始化和手动翻页入口使用。`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过，结果为 0 error / 98 warnings。
- Unity PlayMode、Android/iOS 横屏尚未验证。需补验完成后仍停留原页、交互锁释放、`NextBtn` 显示与手动翻页、无下一页末页、Skin/Shop 分支和 Guide 收尾。
- 回滚：在 `PlayCompleteTransformAsync()` 成功返回后恢复 `ShowNextPageAfterComplete()` 调用及其原私有方法，即可恢复自动翻页；无需迁移 DB、配置或资源。

## 需求背景

`PostcardPopup` 类注释要求弹窗读取 `PostcardConfig` 配置，初始化并维护 `DB.Postcard.CurrentId`，支持上一页、下一页、未解锁占位页，并在 `Postcard_PageItem` 播放解锁动画期间禁止关闭和点击。本次按最新配置结构优化页面加载：`unlock` 作为一条特殊 `PostcardConfig` 传入，`ThemeName` 用于标题多语言刷新，保留原脚本注释文字不改。2026-07-06 追加：整页 CompleteAnim 结束后隐藏 Chest，打开 `CommonGetRewardPopup` 发放当前配置奖励，奖励弹窗关闭后自动切到下一页。2026-07-07 追加：最后一张普通明信片完成后，`NextBtn` 仍允许玩家进入 `Postcard_unlock` 占位页。2026-07-07 追加：奖励弹窗关闭后，如果当前 `PostcardConfig.Rewards` 含有可解析的 `GameProps.Skin` 奖励，则继续弹出 `GetSkinTransformPopup` 展示本次获得皮肤。2026-07-08 追加：领奖弹窗、皮肤转换弹窗以及从皮肤转换弹窗跳转打开的 ShopPanel 都关闭后，再触发当前页 `transform` 动画并等待，播放完成后切到下一页。2026-07-09 追加：整页 Complete 入口先触发 `Postcard_Chest.PlayChestOpenAnim()` 并等待开箱动画结束，再按 Inspector 的 `chestOpenCompleteExtraDelay` 默认额外停留 1 秒，之后隐藏 Chest 和继续领奖弹窗流程。2026-07-09 追加：已经 complete 的普通页在普通刷新/浏览入口直接隐藏 Chest，不再显示奖励预览和进度。2026-07-13 追加：全部普通 page complete 后不再显示 `NextBtn`，也不再进入 `Postcard_unlock`；此前 2026-07-07 的末尾占位页行为被本次需求替代。2026-07-13 追加：当前页完成变形动画结束后保留在原页，不再自动切换下一页；玩家仍可通过 `NextBtn` 手动翻页。

## 影响范围

- 修改脚本：`Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs`
- 新增拆分脚本：`Assets/Game/UI/Postcard/Scripts/PostcardPopup_PageCompleteProcess.cs`
- 宝箱表现脚本：`Assets/Game/UI/Postcard/Scripts/Postcard_Chest.cs`
- 关联弹窗脚本：`Assets/Game/UI/GetSkinPopup/Scripts/GetSkinTransformPopup/GetSkinTransformPopup.cs`
- 本地验证生成文件：`Assembly-CSharp.csproj`，仅用于当前 `dotnet build` 同步新脚本条目
- 交互脚本：`Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs`
- 自动生成绑定：`Assets/Game/UI/Postcard/Scripts/PostcardPopup.Generated.cs`
- 弹窗 prefab：`Assets/Game/UI/Postcard/Resources/PostcardPopup.prefab`
- 皮肤转换弹窗 prefab：`Assets/Game/UI/GetSkinPopup/Resources/GetSkinTransformPopup.prefab`
- 页面资源：`Assets/Game/UI/Postcard/Resources/Postcard_ocean.prefab`、`Postcard_unlock.prefab`
- 宝箱 Spine 资源：`Assets/Game/ArtSrc/Spines/sp_box-open_002/sp_box-open_002.json`、`sp_box-open_002_SkeletonData.asset`
- 配置表：场景中已初始化的 `ConfigManager.Tables.TbPostcardConfig`，其中 `prefabPath` 是页面资源路径，`themeName` 是标题多语言 key
- 存档字段：`DB.Postcard.CurrentId`、`CompletedId`、`CurrentCollectedPuzzledId`

2026-07-09 开箱动画改动未修改 prefab、Luban 配置、RemoteConfig、广告、IAP、analytics、平台 SDK 或全局事件；宝箱动画使用 prefab 中已有 `UISpineCtrl` / `SkeletonGraphic` 和 `sp_box-open_002` 资源。此前 Skin 流程新增读取仅限从现有 `Rewards` 中解析 Skin id 并查 `TbSkin`。

## 原有行为基线

- `PostcardPopup` 只绑定 `CloseBtn`，没有读取明信片配置，也没有实例化页面资源。
- `LastBtn`、`NextBtn` 已在 `PostcardPopup.Generated.cs` 生成并在 prefab 绑定，但没有脚本逻辑。
- `PostcardPopup.prefab` 中存在名为 `Container` 的页面挂点，但当前 `container` 字段未序列化绑定。
- `tbpostcardconfig.json` 当前使用 `prefabPath` 指向 Resources 下的页面 prefab；`themeName` 由配置生成到 `PostcardConfig.ThemeName`，用于多语言标题。

## 当前行为

- `OnOpen()` 绑定关闭、上一页、下一页按钮，并加载当前明信片页面。
- 弹窗通过场景中的 `ConfigManager` 和 `DBManager` 读取配置与明信片存档，避免在打开 UI 时隐式创建或初始化管理器。
- 弹窗优先读取 `DB.Postcard.CurrentId`；为空或无效时使用配置表第一条 id 初始化并保存。
- 配置读取后会把 `unlockProgress` 为空、`rewards` 为空且 `rewardMultiple <= 0` 的配置行识别为未解锁占位页配置，不加入普通明信片页列表。
- 普通页面和未解锁占位页都通过 `PostcardConfig.PrefabPath` 加载 `Postcard_PageItem` prefab。
- `ThemeName` 不再作为资源名使用，只作为标题多语言 key；标题优先使用 `title` 序列化字段，未绑定时回退到 generated 字段 `UiMingxinpian01Txt`。
- Chest 优先使用 `chest` 序列化字段；未绑定时按 `Postcard_Chest` 子节点或子组件兜底查找。
- `container` 未绑定时会在当前弹窗层级中查找名为 `Container` 的子节点作为页面挂点。
- 当前页未完成且不是最后一页时，点击 `NextBtn` 显示配置表中的 `Postcard_unlock` 占位页，并隐藏下一页按钮。
- 最后一张普通明信片未完成时不会显示可点击的 `NextBtn`；全部普通 page complete 后同样隐藏 `NextBtn`，不再进入 `Postcard_unlock` 占位页。
- `Postcard_unlock` 作为特殊 `Postcard_PageItem` 加载；其 `items` 为空时只执行页面生命周期，不绑定 item 按钮、不做解锁检测或整页完成检测。
- 每次切换未完成普通配置页或锁定占位页时，Popup 会显示并刷新 Chest 奖励槽和进度；切换到已经 complete 的普通页时会直接隐藏 Chest。
- 锁定占位页点击 `LastBtn` 返回当前明信片页。
- 当前页已完成且后面仍存在普通明信片配置时，点击 `NextBtn` 进入下一条配置并写入 `DB.Postcard.CurrentId`；全部普通 page complete 后停留在最后一张普通页，不写入新的 `CurrentId`。
- `OnPagePlayAnim(true)` 会让关闭、上一页、下一页不可交互；`Postcard_PageItem` 在该状态下也会拒绝 item 点击。
- `OnPageComplete()` 当前进入完成流程后会锁定当前弹窗交互，先刷新页面完成视觉并等待当前页 `PlayCompleteTransformAsync()`；随后准备 Chest 满进度并调用 `Postcard_Chest.PlayChestOpenAnim()`。有效 Spine TrackEntry 等待 `Complete` 或 `End` 及 Inspector 的额外停留，缺少 TrackEntry 时使用 `pageCompleteStepDelay` 降级；之后依次隐藏 Chest、打开领奖弹窗、处理 Skin 转换弹窗和可选 ShopPanel，最后释放交互锁并保留当前页，不自动切页。
- 完成流程的 `OnPageComplete()`、异步等待、奖励弹窗等待、Skin 转换弹窗等待和奖励配置判断已拆到 `PostcardPopup_PageCompleteProcess.cs` partial；`PostcardPopup.cs` 保留页面加载、按钮、标题、Chest 刷新和数据辅助职责。
- `GameProps.Skin` 奖励解析复用 `BaseParamUtils.TryGetString(...)`，只在能通过 `ConfigManager.Instance.Tables.TbSkin.GetOrDefault(skinId)` 查到有效 `Config.Skin` 时弹出转换弹窗；无效 skin id 不展示空弹窗。
- `Postcard_PageItem` 解锁 item 时先校验当前页未完成、item 未解锁且 Star 足够；保存 `CurrentCollectedPuzzledId` / `CompletedId` 成功后再调用 `SimpleBag.RemoveTool(GameProps.Star, ...)` 并播放 `Postcard_ProgressItem.PlayUnlockAnim(Action)`。
- `Postcard_PageItem` 整页首次完成时回调 `PostcardPopup.OnPageComplete()`；Popup 先刷新当前页完成态并等待页面 transform，再进入 Chest、领奖、皮肤转换弹窗和可选 ShopPanel，流程结束后仍停留当前页。
- 上述顺序与注释中的动画后记录顺序存在实现差异，目的是避免扣星后 `DB.SavePostcard()` 失败导致玩家资源丢失；是否需要严格改回注释顺序仍待产品确认。

## 数据与流程

1. `PostcardPopup.Create()` 打开弹窗。
2. `PostcardPopup.OnOpen()` 读取场景中已初始化的 `ConfigManager.Tables.TbPostcardConfig.DataList`。
3. 脚本按配置内容拆分普通明信片页和未解锁占位页：普通页进入 `_postcardConfigs`，占位页保存到 `_unlockConfig`。
4. 脚本读取 `DB.Postcard.CurrentId`，无效时写入第一条普通明信片配置 id；未解锁占位页不写入 `CurrentId`。
5. 根据当前配置的 `PrefabPath` 加载明信片主题 prefab，实例化到 `Container`。
6. 普通页面和 `Postcard_unlock` prefab 都必须已经绑定 `Postcard_PageItem`；缺失时记录 warning 并拒绝切页，避免临时补组件掩盖 prefab 绑定问题。
7. 每次页面切换后，弹窗用当前页面配置的 `ThemeName` 调用 `LocalizationManager.GetTranslation(...)` 刷新标题；多语言缺失时回退显示 key 本身，key 为空时清空标题。
8. 弹窗会先判断当前普通配置页是否已经 complete；已完成页直接隐藏 Chest，未完成普通页才把当前配置的 `Rewards` 传给 Chest 刷新奖励槽。Chest 内部按 `CommonGetRewardPopup` 口径解析 Coin / Star / Skin 图标，并用 `UnlockProgress.Count` 和 `DB.Postcard` 当前收集状态刷新 Chest 进度。
9. `Postcard_unlock` 的 `items` 为空时不进入 item 绑定、刷新或完成检测分支，只作为未解锁占位页展示；Chest 同步显示空奖励和 `0/0`。
10. `Postcard_PageItem` 保存进度、扣除 Star 后播放 item 解锁动画。
11. `Postcard_ProgressItem` 动画完成后回调 `Postcard_PageItem`；若当前页尚未整页完成，则解除交互锁并刷新页面。
12. 若当前页本次完成，`Postcard_PageItem` 回调 `PostcardPopup.OnPageComplete()`；Popup 保持关闭、上一页、下一页不可交互。
13. Popup 调用当前页 `Refresh()` 启动 `PostcardGrayImage`、正文与完成态本地表现，再调用 `PlayCompleteTransformAsync()` 触发 `transform` 并等待固定窗口。
14. 页面 transform 等待成功后，Popup 准备 Chest 奖励槽和满进度，再调用 `Postcard_Chest.PlayChestOpenAnim()`。
15. 如果开箱动画返回有效 `Spine.TrackEntry`，Popup 等待其 `Complete` 或 `End`，再按 `chestOpenCompleteExtraDelay` 额外停留；如果缺少有效 TrackEntry，则降级等待 `pageCompleteStepDelay`。
16. 开箱等待结束后隐藏 Chest，再等待 `pageCompleteStepDelay`。
17. 当前配置存在 `Rewards` 时，Popup 调用 `CommonGetRewardPopup.Create(PostcardConfig, callback)` 发放 Chest 奖励；关闭回调只放行完成流程，不再刷新当前页。
18. 奖励弹窗关闭后，Popup 从同一份 `PostcardConfig.Rewards` 中解析有效 Skin 并展示 `GetSkinTransformPopup`；若转换弹窗打开 `ShopPanel`，继续等待 ShopPanel 关闭。
19. 后续弹窗全部结束后释放完成流程交互锁，并保留当前完成页，不自动切页。
20. 存在下一张普通配置时，玩家仍可通过 `NextBtn` 手动切页；全部普通页完成后不再显示末尾 unlock 占位页入口。

## 验证记录

- 2026-07-07 `git diff --check -- Assets/Game/UI/Postcard/Scripts/PostcardPopup_PageCompleteProcess.cs Assets/Game/UI/GetSkinPopup/Scripts/GetSkinTransformPopup/GetSkinTransformPopup.cs`：通过，仅有工作区 LF/CRLF 转换提示。
- 2026-07-07 静态扫描确认本次两个改动文件未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、RemoteConfig、PlayerPrefs、StreamingAssets、广告、IAP、analytics、平台 SDK 或全局事件。
- 2026-07-07 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，97 warnings / 0 errors；warnings 属于当前工程既有输出范围，本次未逐项审计。
- 2026-07-07 `git diff --check -- Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs Assets/Game/UI/Postcard/Scripts/PostcardPopup功能说明.md Assets/Game/UI/Postcard/Scripts/PostcardPopup风险评估.md DevelopMD/已有功能设计与使用记录.md`：通过，仅有工作区 LF/CRLF 转换提示。
- 2026-07-07 静态扫描确认本次 `PostcardPopup.cs` 改动未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、RemoteConfig、广告、IAP、analytics、平台 SDK 或全局事件；只命中既有页面实例化、`SaveCurrentId(...)` 和页面刷新入口。
- 2026-07-07 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：未完成编译验证，当前 `Temp\bin\Debug` 缺失大量 Unity/插件元数据 DLL，首个错误为缺失 `Apple.Core.dll`，共 191 个 CS0006；未获得 `PostcardPopup.cs` 的业务代码编译诊断。
- 2026-07-06 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，97 warnings / 0 errors。
- 2026-07-06 `git diff --check -- Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs Assets/Game/UI/Postcard/Scripts/PostcardPopup功能说明.md Assets/Game/UI/Postcard/Scripts/PostcardPopup风险评估.md Assets/Game/UI/Postcard/Scripts/Postcard_PageItem功能说明.md Assets/Game/UI/Postcard/Scripts/Postcard_Chest功能说明.md Assets/Game/UI/Postcard/Scripts/Postcard_Chest风险评估.md DevelopMD/已有功能设计与使用记录.md`：通过，仅有工作区 LF/CRLF 转换提示；新增 `PostcardPopup_PageCompleteProcess.cs` 和 `.meta` 通过 `git diff --check --no-index -- NUL <file>` 检查，无行尾空白错误。
- 2026-07-06 静态扫描确认 `PostcardPopup.cs` 和 `PostcardPopup_PageCompleteProcess.cs` 未新增 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、`RemoteConfig`、广告、IAP、analytics、平台 SDK 或全局事件；只命中预期的 `CommonGetRewardPopup`、UniTask 等待、生命周期取消和既有 `SavePostcard`。
- 静态证据确认 `PostcardPopup.Generated.cs` 存在 `CloseBtn`、`LastBtn`、`NextBtn` 字段。
- 静态证据确认 `PostcardPopup.prefab` 已绑定三个按钮，页面挂点可通过子节点名 `Container` 查找，标题文本可通过 `UiMingxinpian01Txt` 兜底取得。
- 2026-07-08 CodeGraph 与源码静态检查曾确认完成链路末尾为 `_currentPage.PlayCompleteTransformAsync()` -> `ShowNextPageAfterComplete()`；2026-07-13 已按新需求删除最后的自动切页调用与私有方法。
- 2026-07-08 `git diff --check -- Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs Assets/Game/UI/Postcard/Scripts/PostcardPopup_PageCompleteProcess.cs Assets/Game/UI/Postcard/Scripts/Postcard_PageItem功能说明.md Assets/Game/UI/Postcard/Scripts/PostcardPopup功能说明.md DevelopMD/已有功能设计与使用记录.md`：通过，仅有工作区 LF/CRLF 转换提示；静态敏感扫描未命中编辑器生命周期、每帧轮询、全局事件或平台链路。
- 2026-07-08 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，97 warnings / 0 errors；warnings 属于当前工程既有输出范围，本次未逐项审计。
- 2026-07-09 静态检查确认 `PostcardPopup_PageCompleteProcess.HandlePageCompleteAsync()` 在刷新 Chest 后调用 `Postcard_Chest.PlayChestOpenAnim()`，并通过 `TrackEntry.Complete` / `End` 等待开箱动画后再隐藏 Chest；`git diff --check` 和敏感关键字扫描已通过，Unity PlayMode 尚未执行。
- 2026-07-09 Chest 奖励图标口径修正后，`BuildChestRewards(...)` 不再提前丢弃 Skin 等非整数奖励参数，而是把当前配置 `Rewards` 交给 Chest 解析；`dotnet build` 通过，0 warnings / 0 errors。
- 2026-07-09 追加静态检查确认 `pageCompleteStepDelay` 和 `chestOpenCompleteExtraDelay` 已作为 `PostcardPopup` Inspector 序列化字段，默认分别为 0.5 秒和 1 秒；有效开箱 `TrackEntry` 完成后会先等待 `chestOpenCompleteExtraDelay`，再隐藏 Chest。
- 2026-07-09 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：未通过，首个错误为缺失 `Temp\bin\Debug\Apple.Core.dll`，共 191 个 `CS0006` 元数据缺失错误，未获得本次业务脚本编译诊断。
- 2026-07-09 追加 Inspector 等待参数后重跑 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，99 warnings / 0 errors。

## 剩余风险与未验证项

- 尚未运行 Unity PlayMode 验证页面实例化、上一页/下一页、锁定页返回和动画期间按钮禁用。
- 尚未运行 Unity PlayMode 验证最后一张普通明信片未完成时 `NextBtn` 隐藏、完成后 `NextBtn` 显示并进入 `Postcard_unlock`、`LastBtn` 返回最后一张普通页。
- 尚未运行 Unity PlayMode 验证切换不同 config page 时 Chest 奖励槽和进度是否即时刷新。
- 尚未运行 Unity PlayMode 验证已经 complete 的页面重新打开或切回时 Chest 是否保持隐藏。
- 尚未运行 Unity PlayMode 验证整页完成后隐藏 Chest、打开 `CommonGetRewardPopup`、皮肤转换弹窗和可选 ShopPanel 都关闭后播放 transform、保留当前页并恢复手动翻页的完整 UI 栈。
- 尚未运行 Unity PlayMode 验证整页完成后 `box-open` 是否完整播放、切到 `box-open_idle`、按 Inspector 配置额外停留、随后隐藏 Chest 并继续 `CommonGetRewardPopup` / Skin 转换 / transform，最终保留当前页并恢复手动翻页。
- 当前 `dotnet build` 被 Unity/插件元数据缺失阻断，仍需补齐 `Temp\bin\Debug` 依赖或通过 Unity 重新生成编译上下文后重跑编译验证。
- 尚未运行 Unity PlayMode 验证包含 Skin 奖励的明信片页在奖励弹窗关闭后打开 `GetSkinTransformPopup`，以及转换弹窗跳转 ShopPanel 后等待 ShopPanel 关闭、完成 transform 并保留当前页。
- 尚未在 Android/iOS 横屏设备验证触摸、遮罩、UI 栈和页面显示位置。
- 目前静态检查确认 `Postcard_ocean.prefab` 绑定了 `Postcard_PageItem` 与 `Postcard_ProgressItem`，`Postcard_unlock.prefab` 绑定了空 `items` 的 `Postcard_PageItem`；其他主题 prefab 的完整绑定仍待 Unity Inspector 或 PlayMode 验证，当前代码会在缺失绑定时拒绝切页并记录 warning。
- 当前 `tbpostcardconfig.json` 中普通页 `themeName` 仍为空、unlock 行为 `???`，标题多语言真实显示仍依赖后续配置与 I2 词条补齐。
- 当前 Chest 已支持按 `CommonGetRewardPopup` 口径展示 Coin / Star / Skin 预览；`GameProps.Skin` 仍会在领奖后通过 `GetSkinTransformPopup` 展示获得皮肤，Chest 预览只负责本地图标展示，不发放皮肤。

## 回滚方式

回退 `PostcardPopup.cs` 中本次新增的配置读取、页面实例化、翻页、锁定页和动画交互锁逻辑，并移除 `PostcardPopup_PageCompleteProcess.cs` 的工程引用，即可恢复到只绑定关闭按钮的旧行为；若只回退本次 Skin 转换弹窗链路，删除 `PostcardPopup_PageCompleteProcess.cs` 中 `WaitSkinTransformPopupCloseAsync(...)`、`ResolveSkinReward(...)` 及调用点，并回退 `GetSkinTransformPopup.Create(Skin, Action)` 的关闭回调参数即可。不需要迁移或清理新增 DB 字段。

## 2026-07-09 item 解锁粒子补充

### 需求背景

补充 `PostcardPopup.PlayUnlockEffectsAsync(...)`，在单个明信片 item 解锁被真实页面流程接受后，延迟 0.5 秒把已绑定 particle 移动到对应 `Postcard_PageItem` 真实 item 的位置播放。用户要求不做过度设计，因此本次只复用 `PostcardPopup` 本地序列化粒子列表，不新增资源加载、对象池、全局事件或配置项。

### 影响范围

- `Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs`：补充 `collectEffects` 字段说明、`PlayUnlockEffectsAsync(...)`，并保留 `PlayCollectEffect(...)` 转调。
- `Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs`：在 `TryUnlockItem(...)` 保存成功并扣星后，取当前真实 item 的 `RectTransform` 中心并调用 `_owner.PlayUnlockEffectsAsync(...).Forget()`。
- 静态检查确认 `Assets/Game/UI/Postcard/Resources/PostcardPopup.prefab` 当前没有 `collectEffects` 序列化项，也没有明显 ParticleSystem YAML 块；粒子未绑定时运行时会直接跳过。

### 行为说明

- 原有基线：`Postcard_PageItem` 解锁成功后只扣星并播放 `Postcard_ProgressItem.PlayUnlockAnim(...)`，`PostcardPopup` 中已有空的 `PlayCollectEffect(Vector3)` 和 `collectEffects` 字段。
- 新行为：解锁校验、`DB.SavePostcard()` 和扣星成功后，页面计算当前 item 的 UI 中心世界坐标；`PostcardPopup.PlayUnlockEffectsAsync(...)` 等待 0.5 秒，若 Popup 未销毁且仍处于 active，再遍历 `collectEffects`，将每个非空 `ParticleSystem` 移动到该坐标，清理旧发射并重新播放。
- 该表现同时覆盖普通 item 点击和 guide 点击，因为二者都复用 `Postcard_PageItem.TryUnlockItem(...)`。
- 未绑定粒子、列表为空或存在空项时静默跳过，不影响存档、扣星、guide 推进、Chest、奖励弹窗或整页完成流程。

### 数据、资源与事件流

- 不新增 DB 字段，不修改 `DB.Postcard` 保存内容，不新增 Luban、RemoteConfig、PlayerPrefs、Resources 路径、analytics、广告、IAP 或平台 SDK。
- 不实例化粒子对象，只延迟移动并播放 prefab 上已绑定的 `ParticleSystem`；延迟等待绑定 `GetCancellationTokenOnDestroy()`。
- 不新增 `PlayEvents` / 全局事件；触发点仍在本地 `Postcard_PageItem -> PostcardPopup` 引用链内。

### 验证记录

- `git diff --check -- Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs`：通过，仅有工作区 LF/CRLF 转换提示。
- 静态敏感扫描：`PostcardPopup.cs` 和 `Postcard_PageItem.cs` 未新增编辑器生命周期、每帧轮询、全局事件、RemoteConfig、IAP、Ads、Analytics 或 PlayerPrefs；命中仅为既有页面 `Instantiate(...)` 和 Animator `Update(0f)`。
- `rg -n "!u!198|ParticleSystem|m_PlayOnAwake|collectEffects" Assets\Game\UI\Postcard\Resources\PostcardPopup.prefab`：无命中，说明当前 prefab 侧尚无可静态确认的粒子绑定。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：未通过，首个错误为缺失 `Temp\bin\Debug\Apple.Core.dll`，共 191 个 `CS0006` 元数据缺失错误，未获得本次业务脚本编译诊断。

### 剩余风险与回滚

- 仍需 Unity Inspector / PlayMode 验证：把目标粒子绑定到 `PostcardPopup.collectEffects` 后，普通解锁和 guide 解锁是否在真实 item 中心播放；Android/iOS 横屏 Canvas 缩放下粒子位置是否正确。
- 当前静态证据显示 prefab 还没有粒子绑定；如果不绑定，代码会 no-op，玩家看不到新增表现。
- 回滚方式：删除 `Postcard_PageItem.TryUnlockItem(...)` 中 `_owner.PlayUnlockEffectsAsync(...).Forget()` 调用，并移除 `PostcardPopup.PlayUnlockEffectsAsync(...)` / `collectEffects` 字段即可；不需要数据迁移。

## 2026-07-09 Sort 排序与 Diary 正文补充

### 需求背景

用户要求 `PostcardPopup` 不再直接依赖 `TbPostcardConfig.DataList` 的配置表原始顺序，而是在弹窗开启时解析并缓存按 `PostcardConfig.Sort` 排序后的明信片列表；同时 `Postcard_PageItem.contentTxt` 需要通过新增的 `PostcardConfig.Diary` 多语言 key 填充完成态正文。

### 影响范围

- `Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs`：`TryLoadPostcardConfigs()` 在读取配置时构建 `_postcardConfigs` 缓存并按 `Sort` 升序排序；锁定占位页配置也通过同一比较规则选择。
- `Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs`：完成态正文由当前 `config.Diary` 通过 `LocalizationManager.GetTranslation(...)` 解析；缺少翻译时回退显示 key，`Diary` 为空时保留原 prefab 文本兜底。
- 读取字段：生成类 `Config.PostcardConfig.Sort`、`Config.PostcardConfig.Diary`。
- 未修改 prefab、DB/save 字段、Luban 表结构、资源路径、全局事件、广告、IAP、analytics、RemoteConfig 或平台 SDK。

### 原有行为基线

- `PostcardPopup` 原先把 `TbPostcardConfig.DataList` 中非锁定页配置按原始表顺序加入 `_postcardConfigs`，初始页、上一页/下一页、完成后自动切页都依赖该缓存顺序。
- `Postcard_PageItem` 原先在首次解析完成态正文时缓存 `contentTxt` prefab 上的初始文本，完成时用该文本播放 `DOText`。

### 当前行为

- `PostcardPopup.OnOpen()` 仍只在弹窗打开时读取一次配置；读取后普通明信片页缓存按 `Sort` 升序排序，`Sort` 相同则按 `Id` 做稳定兜底比较。
- `DB.Postcard.CurrentId` 仍按 id 查找当前页；若无效，则初始化到排序后的第一张普通明信片页。
- 上一页、下一页和最后的 `Postcard_unlock` 入口继续使用 `_postcardConfigs` 缓存，因此统一受 `Sort` 顺序控制；整页完成后的自动切页已于 2026-07-13 移除。
- `Postcard_PageItem` 在页面 `SetData(...)` 时清空正文缓存；`Refresh()` 触发完成态正文显示时按当前页 `config.Diary` 获取 I2 多语言文本，并继续沿用原有 `DOText` 逐字填充表现。

### 验证记录

- 2026-07-09 静态检查确认 `PostcardConfig` 生成类已包含 `Sort` 和 `Diary` 字段，`tbpostcardconfig.json` 当前普通页配置已有对应 key。
- 2026-07-09 `git diff --check -- Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs`：通过，仅有工作区 LF/CRLF 转换提示。
- 2026-07-09 敏感关键字扫描未发现本次新增编辑器生命周期、每帧轮询、全局事件、RemoteConfig、广告、IAP、analytics 或 PlayerPrefs；唯一命中为 `Postcard_PageItem.cs` 中既有 Animator `Update(0f)`。
- 2026-07-09 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，99 warnings / 0 errors。

### 剩余风险与回滚

- 尚未在 Unity PlayMode 验证排序后的手动翻页顺序、完成后停留原页、`CurrentId` 无效时初始化页面，以及语言切换后的 Diary 正文显示。
- 当前配置中 `30007`、`30008` 的 `prefabPath` 为空但具有进度和奖励，排序后会进入普通页缓存；若资源仍未补齐，翻到这些配置时会按既有逻辑记录 prefab 缺失 warning 并拒绝切页。
- 回滚排序逻辑：移除 `_postcardConfigs.Sort(ComparePostcardConfigSort)` 和锁定页比较选择，恢复 `_unlockConfig ??=`。
- 回滚 Diary 正文：让 `GetCompleteContentText(...)` 恢复读取 `contentTxt.text` 初始值，并移除 `I2.Loc` using 和 `ResolveLocalizedCompleteContentText(...)`。

## 2026-07-09 complete page 隐藏 Chest

### 需求背景

用户要求明信片 complete 的 page 不再显示 Chest。此前整页完成流程中会在开箱表现后隐藏 Chest，但重新打开或浏览一张已经完成的页面时，`PostcardPopup.RefreshChest(...)` 仍会无条件 `Show()` Chest，导致 complete page 上 Chest 又被显示出来。

### 影响范围

- `Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs`：`RefreshChest(Config.PostcardConfig postcardConfig, bool isImmediate)` 增加完成态判断。
- `Assets/Game/UI/Postcard/Scripts/Postcard_Chest.cs`：未改脚本，仅继续复用已有 `Hide()` / `Show()`。
- 未修改 `PostcardPopup.prefab`、`PostcardPopup_GuidePart.prefab`、配置表、DB/save 字段、资源路径、全局事件、广告、IAP、analytics、RemoteConfig 或平台 SDK。

### 原有行为基线

- `RefreshCurrentChest(...)` 通过 `GetVisibleConfig()` 取得当前展示配置后调用 `RefreshChest(...)`。
- `RefreshChest(...)` 解析到 `Postcard_Chest` 后会无条件调用 `chestView.Show()`，再刷新奖励槽和进度。
- 整页刚完成的异步领奖流程会隐藏 Chest，但再次刷新同一个已完成配置页时，`RefreshChest(...)` 仍可能把 Chest 重新显示出来。

### 当前行为

- `RefreshChest(...)` 在解析到 Chest 后先调用已有 `IsConfigCompleted(postcardConfig)` 检查当前普通页是否已经在 `DB.Postcard.CompletedId` 中。
- 已完成页直接调用 `chestView.Hide()` 并返回，不刷新奖励槽和进度，也不播放 Chest idle。
- 未完成普通页和未解锁占位页继续沿用原有逻辑：`Show()` Chest，刷新 `PostcardConfig.Rewards` 奖励预览，并按 `UnlockProgress` 与当前收集状态刷新进度。
- 完成瞬间的开箱流程不改变：`PostcardPopup_PageCompleteProcess.HandlePageCompleteAsync()` 仍会先刷新并播放 Chest 开箱，开箱完成后隐藏 Chest，再继续领奖、Skin 转换和页面 transform；transform 结束后保留当前页。

### 数据、资源与事件流

- 完成态判断复用 `PostcardPopup` 既有的 `TryGetPostcardData(...)` / `IsConfigCompleted(...)`，读取同一份 `DB.Postcard.CompletedId`；本次没有新增 DB 字段或写存档。
- 不新增资源加载、实例化、全局事件、PlayEvents、RemoteConfig、广告、IAP、analytics 或平台 SDK 链路。
- 该隐藏只影响 Popup 内 Chest 节点的 active 状态，不改变已完成页正文、翻页按钮、奖励发放或完成记录。

### 验证记录

- 2026-07-09 `git diff --check -- Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs Assets/Game/UI/Postcard/Scripts/PostcardPopup功能说明.md Assets/Game/UI/Postcard/Scripts/PostcardPopup风险评估.md Assets/Game/UI/Postcard/Scripts/Postcard_Chest功能说明.md Assets/Game/UI/Postcard/Scripts/Postcard_Chest风险评估.md DevelopMD/已有功能设计与使用记录.md`：通过，仅有 LF/CRLF 转换提示。
- 2026-07-09 本次 `PostcardPopup.cs` diff 敏感关键字扫描未命中 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、RemoteConfig、PlayerPrefs、PlayEvents、DispatchEvent、analytics、广告、IAP、实例化或写存档路径。
- 2026-07-09 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，98 warnings / 0 errors。
- 尚未执行 Unity PlayMode；complete page 重开、普通未完成页、锁定占位页和整页完成开箱流程仍需手工验证。

### 回滚路径

删除 `RefreshChest(...)` 中 `IsConfigCompleted(postcardConfig)` 分支，即可恢复为所有可见配置页刷新时都显示 Chest。不需要迁移数据或回滚资源。

## 2026-07-10 Chest 首次完成开箱与历史完成态解耦

### 需求背景

页内最后一个拼图解锁时，`Postcard_PageItem` 会先把当前配置写入 `DB.Postcard.CompletedId`，再回调 `PostcardPopup.OnPageComplete()`。修复前完成流程复用普通浏览的 `RefreshCurrentChest(false)`；该刷新看到页面已经 complete 后立即 `Hide()` Chest，随后仍对 inactive Chest 播放 `box-open` 并等待 `TrackEntry.Complete` / `End`。因此玩家看不到开箱动画，Spine 不推进时奖励、页面 transform 和切页流程会一直等待。

### 影响文件与系统

- `Assets/Game/UI/Postcard/Scripts/PostcardPopup_PageCompleteProcess.cs`：增加首次完成专用的 Chest 准备步骤和开箱等待超时降级。
- `Assets/Game/UI/Postcard/Scripts/Postcard_Chest.cs`：inactive 层级不再返回不可推进的开箱 `TrackEntry`。
- `Assets/Game/UI/Postcard/Resources/PostcardPopup.prefab`、`sp_box-open_002`、页面 Complete Animator 仅做静态绑定核对，未修改资源。
- 未修改 DB/save 字段或保存顺序、奖励配置与发放、页面完成判定、Guide 状态、全局事件、analytics、广告、IAP、RemoteConfig 或平台 SDK。

### 修复后行为与流程

1. 首次完成入口调用 `PrepareChestForPageComplete(...)`，强制 `Show()` 当前 Chest、刷新当前奖励，并把进度刷新为 `max/max`；不再经过“历史完成页直接隐藏 Chest”的普通刷新分支。
2. Chest 保持 active 后播放 `box-open`，正常情况下仍以 `Complete` / `End` 作为精确结束信号，并继续切到 `box-open_idle`。
3. 等待上限为 `max(3 秒, 动画时长 + 1 秒)`，使用 unscaled time；超时会通过 `Bear.Logger` 记录 warning，跳过仅供正常动画完成使用的 `chestOpenCompleteExtraDelay` 并继续既有完成流程，避免 UI 软锁。
4. inactive Chest、缺少 `UISpineCtrl` 或动画播放失败时，`PlayChestOpenAnim()` 返回 `null`，继续复用 `pageCompleteStepDelay` 降级。
5. 正常收到开箱完成事件后的 `chestOpenCompleteExtraDelay -> Hide Chest -> CommonGetRewardPopup -> GetSkinTransformPopup -> 可选 ShopPanel -> 页面 transform -> 下一页` 顺序不变；超时降级直接从 Hide Chest 继续。重新打开或切回历史完成页时，普通 `RefreshChest(...)` 仍直接隐藏 Chest，不重放奖励。

### 验证记录

- 2026-07-10 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，98 warnings / 0 errors；warnings 为当前工程既有输出，本次未逐项审计。
- 2026-07-10 目标代码 `git diff --check`：通过，仅有 LF/CRLF 转换提示。
- 2026-07-10 静态敏感扫描未发现目标代码新增编辑器生命周期、每帧轮询、全局事件、RemoteConfig、广告、IAP、analytics 或 PlayerPrefs 路径。
- 静态 prefab / Spine 证据确认 Chest、`UISpineCtrl`、`SkeletonGraphic`、`sp_box-open_002_SkeletonData` 和 `box_idle` / `box-open` / `box-open_idle` 均有效；页面 Complete Animator 与 Chest 位于独立层级，不直接控制 Chest。

### 剩余风险与回滚

- Unity PlayMode 尚未验证首次完成、Guide 最后一项、历史完成页重开、缺失 Spine 降级、等待中关闭 Popup、末页和下一页等组合场景。
- Android/iOS 横屏尚未验证开箱遮挡、UI 栈与触摸锁；静态编译不能替代真机表现验证。
- 回滚本次修复时，恢复完成入口调用 `RefreshCurrentChest(false)`、移除 `PrepareChestForPageComplete(...)` 与超时降级，并移除 `PlayChestOpenAnim()` 的 inactive 检查即可；不需要迁移存档、配置或资源。

## 2026-07-13 PageGray 填满时并行播放完成粒子（已被 2026-07-14 后续时序修正替代）

> 当前完成粒子不再与 grayImage 并行或在填充到 1 时立即触发，而是在填充和缩放回弹组成的完整 Sequence 结束后触发。

### 需求背景

用户要求当前明信片页首次完成时，在 `PostcardGrayImage.fillAmount` 从 0 变为 1 的时刻播放 `PostcardPopup.completeEffects`，粒子表现只需与现有完成流程并行触发，不等待粒子播放结束。

### 影响文件、资源与系统

- `Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs`：在灰度填充 Tween 后的既有回调中通知所属 `PostcardPopup` 播放完成粒子。
- `Assets/Game/UI/Postcard/Scripts/PostcardPopup_PageCompleteProcess.cs`：补充 `PlayCompleteEffects()`，遍历已绑定列表并调用 `ParticleSystem.Play(true)`。
- `Assets/Game/UI/Postcard/Resources/PostcardPopup.prefab`：静态检查确认 `completeEffects` 已绑定两个粒子系统引用；本次代码补充未改写该 prefab 的现有用户改动。
- 不修改 DB/save、奖励配置与发放、Chest、页面完成判定、Guide、资源加载路径、全局事件、analytics、广告、IAP、RemoteConfig 或平台 SDK。

### 原有行为基线与当前行为

- 原有首次完成流程会让 `grayImage.fillAmount` 在 0.7 秒内从 0 变为 1，随后恢复原材质并播放页面缩放回弹；`completeEffects` 虽已作为序列化列表存在，但没有运行时播放入口。
- 当前灰度 Tween 到达 1 后，同一个 DOTween 回调立即调用 `_owner.PlayCompleteEffects()`，再继续原有材质恢复和缩放回弹。
- `PlayCompleteEffects()` 只触发非空粒子及其子粒子，不增加 `await`、延时或完成回调；Chest、领奖弹窗与页面 transform 继续按原异步流程推进，结束后保留当前页。
- 历史已完成页面刷新时仍直接设置 `fillAmount = 1`，不会重放首次完成粒子；锁定占位页或未绑定 `grayImage` 时也不会进入该触发点。

### 验证记录

- 2026-07-13 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，98 warnings / 0 errors；warnings 为当前工程既有输出，本次未逐项审计。
- 静态代码检查确认粒子调用位于灰度填充 Tween 的后续回调，且没有新增等待、每帧轮询、存档写入或全局事件。
- 静态 prefab 检查确认 `completeEffects` 当前有两个非空 `ParticleSystem` 文件引用。

### 剩余风险与回滚

- 尚未在 Unity PlayMode 验证两个粒子是否在 `fillAmount = 1` 的目标帧同时出现、层级与缩放是否符合美术预期，以及快速关闭/切页时的粒子生命周期。
- Android/iOS 横屏表现、粒子 overdraw 与低端移动设备帧耗尚未验证。
- 回滚时删除 `Postcard_PageItem` 灰度回调中的 `_owner?.PlayCompleteEffects()` 和 `PostcardPopup.PlayCompleteEffects()` 即可恢复旧时序；不需要迁移存档、配置或资源。

## 2026-07-13 全部普通 page complete 后不展示 unlockPage

### 需求背景

用户要求所有普通明信片 page 都已 complete 时，不再展示 `unlockPage`。本次只调整 `PostcardPopup` 末尾占位页入口，不改变尚有未完成 page 时用于提示锁定状态的 `Postcard_unlock`。

### 影响文件、资源与系统

- 修改 `Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs` 的 Next 点击与按钮状态判断。
- 读取既有 `_postcardConfigs` 和 `DB.Postcard.CompletedId`；`_postcardConfigs` 已排除 `Postcard_unlock` 配置，因此“全部完成”只统计普通 page。
- 不修改 prefab、Luban 配置、Resources 路径、DB 字段或保存时机，不新增全局事件、analytics、广告、IAP、RemoteConfig 或平台 SDK 调用。

### 原有行为与当前行为

- 原有行为：最后一张普通 page complete 后，`RefreshButtonStates()` 会显示 `NextBtn`；点击后调用 `ShowUnlockPage()` 实例化 `Postcard_unlock`。
- 当前行为：`AreAllPostcardPagesCompleted()` 逐项检查普通 page id 是否都存在于 `CompletedId`。全部完成时，末页隐藏 `NextBtn`，点击入口也会再次拦截 `ShowUnlockPage()`，玩家停留在最后一张普通 page。
- 当前页未完成且后面仍有普通 page 时，原有 `NextBtn -> Postcard_unlock` 提示路径保持不变；已完成且仍有后续普通 page 时，原有正常翻页与 `CurrentId` 保存路径保持不变。
- DB 或配置读取失败时，全完成判断返回 false，保守沿用旧入口，不会把数据不完整误判为全部完成。

### 验证记录

- `git diff --check -- Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs`：通过，仅有工作区 LF/CRLF 转换提示。
- 静态敏感扫描只命中 `PostcardPopup.cs` 既有的页面 `Instantiate(...)`；本次未新增编辑器生命周期、每帧轮询、全局事件、RemoteConfig、PlayerPrefs 或资源加载入口。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，98 warnings / 0 errors；warnings 为当前工程既有输出，本次未逐项审计。

### 剩余风险与回滚

- 尚未在 Unity PlayMode 验证最后一页完成流程结束后 `NextBtn` 立即隐藏、关闭重开后的末页状态，以及异常存档中“最后一页完成但前页未完成”的保守占位页路径。
- Android/iOS 横屏触摸和末页布局尚未真机验证；本次没有新增每帧逻辑或实例化路径，静态证据不能替代设备验证。
- 回滚时删除 `AreAllPostcardPagesCompleted()`，并恢复 `OnNextBtnClick()` 的末页无条件 `ShowUnlockPage()` 与 `RefreshButtonStates()` 的旧末尾按钮条件；无需迁移存档、配置或资源。

## 2026-07-13 `_isGuiding` 期间锁定 Popup 按钮

### 需求与影响范围

- 用户要求 `PostcardPopup` 处于 `_isGuiding` 时，关闭、上一页、下一页按钮暂停响应；完整引导结束后再恢复。
- 本次修改 `PostcardPopup.cs` 与 `PostcardPopup_PageCompleteProcess.cs`，只调整当前 Popup 的本地按钮交互和引导收尾，不修改 prefab、配置、DB 字段、资源地址或全局事件。

### 调整前后行为

- 调整前，`RefreshButtonStates()` 只以 `IsPagePlayingAnim` 控制按钮交互，`_isGuiding` 不会阻止玩家关闭或翻页；第四阶段引导关闭后也没有把本地 `_isGuiding` 恢复为 false。
- 调整后，三个 Popup 按钮统一要求 `!IsPagePlayingAnim && !_isGuiding` 才可交互；关闭、上一页和普通下一页点击入口也会再次检查本地引导锁。
- 引导点击 Next 的回调使用内部放行入口继续原翻页逻辑，玩家按钮仍保持不可交互；第四阶段 `PostcardGuidePopup` 关闭回调会清除 `_isGuiding` 并刷新按钮。
- 初始 `Postcard_GuidePart` 只有成功启动后才持有本地引导锁；第三/第四阶段弹窗、领奖按钮 GuideFinger 或 Next GuideFinger 创建失败时，流程记录 Bear warning，并结束本地引导锁以恢复可操作入口，避免资源或目标异常造成永久锁定。
- `OnClose()` 会清除本地 `_isGuiding`，避免 UI 实例被缓存复用时继承旧交互锁。

### 数据、资源与事件流

- 继续沿用既有 `DB.Postcard.IsGuiding` 的一次性读取与消费保存，本次没有新增或改变存档字段、保存时机和失败回滚逻辑。
- GuideFinger 与四阶段引导弹窗继续沿用原 `Resources` 加载和 Bear UI 生命周期；没有新增实例类型、全局事件、`PlayEvents`、RemoteConfig、广告、IAP、analytics 或平台 SDK 调用。
- 新增的失败降级只清理当前 Popup 内存态并刷新按钮，不补发奖励、不跳过存档写入，也不修改 Page complete 判定。

### 验证、剩余风险与回滚

- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，98 warnings / 0 errors；warnings 为当前工程既有输出，本次未逐项审计。
- 目标代码 `git diff --check`：通过，仅有工作区 LF/CRLF 转换提示。
- 尚未在 Unity PlayMode 验证三个按钮锁定、GuideFinger 内部翻页、第四阶段关闭恢复、领奖 Finger/弹窗创建失败降级，以及 Popup 外部销毁后重开。
- Android/iOS 横屏触摸、UI 层级和引导阶段串联尚未真机验证。
- 回滚时恢复 `RefreshButtonStates()` 只检查 `IsPagePlayingAnim`，移除三个点击入口的 `_isGuiding` 防护、内部引导翻页放行、引导完成清锁与创建失败降级；无需迁移 DB、配置、prefab 或资源。

## 2026-07-13 Stage Two 关闭后再显示 GuidePart

- 原行为在胜利页 Finger 点击回调中先调用 `StartGuding()`，随后同时创建 `PostcardPopup` 和第二阶段引导；Popup 的 `OnOpen()` 会立即消费 `IsGuiding`，导致 `Postcard_GuidePart` 在 Stage Two 仍显示时提前启动。
- 当前先创建并保留 `PostcardPopup` 实例，再打开 `PostcardGuidePopup.Create(PostcardStageStep.Two, callback)`；只有 Stage Two 进入关闭回调后，才调用 `PostcardManager.StartGuding()` 和 `PostcardPopup.StartPendingPostcardGuide()`。
- `StartPendingPostcardGuide()` 只对当前已加载的普通 page 启动既有 GuidePart，不重新加载页面；当前页为空或已经显示 unlock 占位页时记录 Bear warning 并停止。
- Stage Two 创建失败时记录 warning，并直接启动页内 GuidePart 作为降级；Popup 或 PostcardManager 已失效时不写入引导状态。
- 本次不修改 prefab、配置、资源路径、DB 字段、全局事件或平台 SDK；仅把既有 `HasGuide/IsGuiding` 保存时机从 Stage One 前移动到 Stage Two 关闭后。
- C# 构建通过，98 warnings / 0 errors；Unity PlayMode 的 Stage Two 覆盖层、关闭回调、创建失败降级和 Popup 中途销毁仍待验证。回滚时恢复 Stage One 前调用 `StartGuding()`，删除 Popup 显式启动 API，并恢复 Stage Two 无回调创建。

## 2026-07-14 Stage Two 关闭回调时序修正

- 需求背景：运行态仍观察到 `Postcard_GuidePart` 与 `PostcardGuidePopup` 第二阶段重叠显示。
- 直接证据：`GameVictoryPanel.TryToPostcardGuide()` 在创建第一阶段弹窗前仍调用 `PostcardManager.StartGuding()`，提前写入 `HasGuide=true`、`IsGuiding=true`；随后 `PostcardPopup.ShowPageItem()` 又会自动调用 `TryStartPendingPostcardGuide()`，立即消费该状态。
- 修正后：第一阶段和第二阶段显示期间不再写入待启动状态，`ShowPageItem()` 也不再自动启动 GuidePart。第二阶段进入关闭回调且 `PostcardPopup`、`PostcardManager` 都有效后，才依次调用 `StartGuding()` 与 `StartPendingPostcardGuide()`，因此页内 GuidePart 只在 GuidePopup 结束后启动。
- 影响文件：`Assets/Game/UI/GameVictory/Scripts/GameVictoryPanel.cs`、`Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs`。未修改 prefab、配置、资源地址、DB 字段结构、全局事件、奖励或平台 SDK。
- 验证：已完成调用顺序静态检查、目标 diff 检查和 C# 编译；Unity PlayMode 的完整 Stage One → Finger → Stage Two → GuidePart 时序及 Android/iOS 横屏触摸仍待验证。
- 回滚：恢复 `ShowPageItem()` 的自动启动调用，把 `StartGuding()` 移回 Stage One 创建前，并从 Stage Two 关闭回调移除该调用；回滚会恢复 GuidePart 提前启动风险。

## 2026-07-14 Complete 页刷新移至领奖关闭回调（已被同日后续时序修正替代）

> 后续需求已明确 PageComplete 动画必须位于 `chestOpen` 之前；当前实现以本文顶部“PageComplete 动画前移到 Chest 开启前”为准。

- 需求背景：整页首次完成时，页面完成态 `Refresh()` 不应早于 Chest 与 `CommonGetRewardPopup` 流程执行。
- 修改前：最后一个进度项解锁动画结束后，`Postcard_PageItem` 立即刷新整页完成态，再触发 `PostcardPopup.OnPageComplete()` 打开 Chest 和领奖流程。
- 修改后：最后一个进度项解锁动画结束后直接进入 Page Complete 流程；存在奖励弹窗时，只有 `CommonGetRewardPopup.OnClose()` 触发其关闭回调后，才调用当前页 `Refresh()`，随后继续 Skin 弹窗、可选 ShopPanel 和页面 transform。
- 引导领奖分支不再在点击按钮后提前完成等待，统一等待 `CommonGetRewardPopup` 真正关闭；无奖励配置或弹窗创建失败时在相同流程位置降级刷新，避免页面卡在未刷新状态。
- 本次只调整本地 UI 回调顺序，不重复发奖、不新增存档、资源加载、全局事件或平台逻辑。C# 构建通过（98 warnings / 0 errors），目标 diff 检查通过；Unity PlayMode 与 Android/iOS 横屏表现仍待验证。
- 回滚时恢复 `Postcard_PageItem` 完成分支的 `Refresh(callback)`，删除 `RefreshCompletedPage()` 及领奖关闭回调中的调用，并恢复引导点击后直接完成 `closeSource`。

## 2026-07-14 CompleteEffect 播放结束后延迟 2 秒

- 需求背景：页面 `transform` 必须等 `completeEffects` 完整播放结束，并在结束后额外停留 2 秒，不能只等到 `ParticleSystem.Play(true)` 已调用。
- 修改前：grayImage 填充/回弹完成后，Popup 调用全部非空粒子的 `Play(true)` 就立即返回，PageItem 随即释放局部完成信号并触发 `transform`。
- 修改后：全部非空粒子仍同时开始播放；非循环粒子等待自身及子粒子全部消散，循环根粒子完整播放一个 `main.duration` 后执行 `StopEmitting`，再等待现存子粒子消散。全部绑定表现结束后统一等待 2 秒，方法返回后才释放 `transform`。
- 静态 prefab 证据：`PostcardPopup.prefab` 的 `completeEffects` 当前绑定 3 项，其中本地粒子为非循环、时长 2 秒；另外两项引用 `ConfettiDirectionalGreenYellow.prefab` 的循环根粒子，时长 5 秒，因此实现不能直接无限等待 `IsAlive(true)`。
- 当前正常顺序为：grayImage 填充/回弹 → 粒子同时播放 → 循环粒子一轮后停止发射 → 全部粒子消散 → 2 秒停留 → Animator `transform` 与正文逐字 → Chest。
- 影响文件为 `PostcardPopup_PageCompleteProcess.cs`；不修改 prefab、ParticleSystem 资产、Animator、配置、DB/save、奖励、资源地址、全局事件、分析打点或平台 SDK。
- 验证：C# 工程构建通过（99 warnings / 0 errors）；已静态核对 3 个绑定和循环配置。Unity PlayMode 的真实粒子寿命、2 秒停留观感、快速关闭，以及 Android/iOS 横屏性能仍待验证。
- 回滚：删除 `CompleteEffectsPostDelay`、`WaitCompleteEffectAsync(...)` 和完整生命周期等待，恢复 `PlayCompleteEffects()` 在调用全部 `Play(true)` 后立即返回；无需迁移数据或资源。

## 2026-07-14 满进度 Chest 前移到 CompleteEffects 阶段

- 需求背景：满进度 Chest 应在 `completeEffects` 开始的位置出现，`chestOpen`、页面转换、领奖和后续弹窗顺序保持不变。
- 修改前：`HandlePageCompleteAsync()` 等待粒子、停留、`transform` 和正文逐字结束后，才调用 `PrepareChestForPageComplete(...)` 显示 Chest、刷新奖励预览和 `max/max` 进度，并立即播放 `chestOpen`。
- 修改后：grayImage 填充/回弹自然结束进入 `PlayCompleteEffects()` 时，先调用 `PrepareChestForPageComplete(...)` 显示满进度 Chest，同一阶段再播放完成粒子；主流程仍等粒子结束、当前 `CompleteEffectsPostDelay`、`transform` 和正文逐字结束后，才对已准备的同一个 Chest 调用 `PlayChestOpenAnim()`。
- 降级路径：如果 grayImage Sequence 被 Kill、没有进入粒子回调，主流程会在原 `chestOpen` 位置补做一次 Chest 准备，避免直接播放未刷新到满进度的 Chest。
- 当前工作区的 `CompleteEffectsPostDelay` 被并行修改为 `0.5f`；本次不覆盖该参数，也不改变粒子生命周期、transform/文案、Chest 开箱等待、隐藏、领奖、Skin/Shop 或引导收尾逻辑。
- 影响文件仅为 `PostcardPopup_PageCompleteProcess.cs` 和本地说明文档；不修改 prefab、Spine/Animator/ParticleSystem 资产、配置、DB/save、奖励发放、资源地址、全局事件、分析打点或平台 SDK。
- 验证：C# 工程构建通过（99 warnings / 0 errors）；Unity PlayMode 的 Chest 出现帧、粒子遮挡、异常降级、快速关闭和 Android/iOS 横屏仍待验证。
- 回滚：删除 `_pageCompleteChestView` 缓存及 `PlayCompleteEffects()` 中的 Chest 准备，把 `HandlePageCompleteAsync()` 恢复为在 `chestOpen` 前直接调用 `PrepareChestForPageComplete(...)`。

## 2026-07-14 CompleteEffects 改为非阻塞发射

- 需求背景：`completeEffects` 只负责播放表现，不再阻塞页面转换或 Chest 开箱流程。
- 修改前：`PlayCompleteEffects()` 等待循环粒子一个 duration、等待全部根及子粒子 `IsAlive(true) == false`，并执行粒子后的额外停留，满进度 Chest 因此前移后长时间保持待开启。
- 修改后：显示满进度 Chest 后，Popup 遍历全部有效粒子并调用 `Play(true)`，方法随即返回；PageItem 的局部完成信号立即释放，Animator `transform` 与正文逐字按原 1.7 秒窗口继续，随后播放 `chestOpen`。
- 两个循环彩带粒子不会永久运行：每个循环根粒子启动一个不参与主流程等待的本地 UniTask，在自身 duration 后执行 `StopEmitting`；不再等待残留粒子消散。
- 删除 `CompleteEffectsPostDelay` 和 `IsAlive(true)` 等待；Chest 满进度显示、grayImage、transform/正文、chestOpen、隐藏、领奖、Skin/Shop 和引导收尾的其他调用点不变。
- 影响文件为 `PostcardPopup_PageCompleteProcess.cs` 和本地说明文档；不修改 prefab、ParticleSystem 参数、Animator/Spine、配置、DB/save、奖励、资源地址、全局事件、打点或平台 SDK。
- 验证：C# 工程构建通过（0 warnings / 0 errors）；Unity PlayMode 的粒子与 transform/chestOpen 重叠、循环粒子后台停止、快速关闭及 Android/iOS 横屏仍待验证。
- 回滚：恢复 `WaitCompleteEffectAsync(...)`、`CompleteEffectsPostDelay` 和 `UniTask.WhenAll(...)`，即可重新让 transform 等待完整粒子生命周期。

## 2026-07-14 满进度 Chest 前移到整页完成入口之前

- 需求背景：最后一块进度完成时，Chest 的显示、奖励内容和 `max/max` 应按普通进度刷新时点更新，并且早于整页完成流程；`chestOpen` Complete/End 后的额外 1 秒停留取消。
- 修改前：满进度 Chest 由 grayImage 结束后的 `PlayCompleteEffects()` 准备；有效开箱动画 Complete/End 后还会读取 `chestOpenCompleteExtraDelay` 再等待 1 秒。
- 修改后：`Postcard_PageItem` 在最后一个 item 解锁动画稳定后、调用 `_onCompleted` 进入 `OnPageComplete()` 之前，先调用 `PreparePageCompleteChest()`；该步骤显示 Chest、刷新当前奖励并执行 `RefreshProgress(max, max, false)`，但继续保持页面交互锁。
- `PlayCompleteEffects()` 现在只发射完成粒子；主流程继续复用已缓存 Chest，在 grayImage、粒子、Animator `transform` 和正文逐字结束后播放 `chestOpen`。缓存缺失时仍在开箱前降级补做满进度准备。
- `WaitChestOpenAnimAsync(...)` 仍等待有效 TrackEntry 的 Complete/End，并保留超时与 Popup 销毁保护；正常结束后立即进入 Chest 缩放隐藏，不再追加固定等待。prefab 中废弃的 `chestOpenCompleteExtraDelay` 序列化值已删除。
- 数据、完成判定、`CompletedId`、扣星、存档、奖励发放、Skin/Shop、Guide、资源地址和全局事件链路未改变。
- 验证：`Assembly-CSharp.csproj` 构建通过（99 warnings / 0 errors），目标 `git diff --check` 通过；Unity PlayMode 的最终 item 到 Chest 出现帧、开箱结束衔接、快速关闭和 Android/iOS 横屏仍待验证。
- 回滚：移除最终 item 回调中的 `PreparePageCompleteChest()`，恢复粒子入口准备 Chest，并恢复字段与 Complete/End 后额外等待；不涉及 DB、配置或资源迁移。

## 2026-07-17 `_isGuiding` 改用 ClickBlock 统一拦截

### 需求、影响范围与原行为

- 需求：`PostcardPopup` 进入本地 `_isGuiding` 阶段后，不再逐个把 Close、Last、Next 设为不可点击；改为开启全屏透明 `ClickBlock`，禁止玩家直接操作当前 Popup，直到完整明信片引导结束。
- 影响文件：`PostcardPopup.cs`、`PostcardPopup_PageCompleteProcess.cs`、`PostcardPopup.prefab`。不修改 `Postcard_GuidePart`、DB 字段、保存时机、配置、资源地址、奖励、全局事件、打点或平台 SDK。
- 原行为：`RefreshButtonStates()` 把 `_isGuiding` 合入三个按钮的 `Interactable`，三个点击入口另行检查 `_isGuiding`，引导 Next 还需要 `TryGoNextPage(true)` 特殊放行。

### 修改后行为与 UI 流

- `SetGuidingState(...)` 统一维护 `_isGuiding` 与 `clickBlock.activeSelf`：`Create()` 触发 `OnOpen()` 时先读取现有 `DB.Postcard.IsGuiding`，值为 true 就立即开启遮罩；页内引导成功启动后继续保持开启；第四阶段关闭、失败降级或 Popup 关闭时关闭遮罩。复用重开时重新以当前 DB 状态初始化，不继承旧内存态。
- `RefreshButtonStates()` 和三个按钮点击入口只保留既有 `IsPagePlayingAnim` 与页面状态判断；`TryGoNextPage()` 删除引导专用放行参数。引导期玩家射线由透明遮罩拦截，引导内部 Next 继续调用同一本地翻页逻辑。
- prefab 中 `ClickBlock` 默认关闭，使用 Bear UI 的 `UIEmptyRaycastTarget`；该组件强制启用 `raycastTarget` 且不生成可见网格。节点已序列化绑定到 `PostcardPopup.clickBlock`，同级顺序位于普通 Popup 内容与 `Postcard_GuidePart` 之间，页内引导按钮仍位于遮罩上方；动态 GuideFinger 继续实例化为最后一个子节点。
- 遮罩从页内 GuidePart 开始后持续到 `CompletePostcardGuiding()`，不会在 GuidePart 自身目标完成时提前关闭，因此领奖、第三阶段、Next Finger 和第四阶段期间仍禁止直接操作底层 PostcardPopup。

### 验证、剩余风险与回滚

- CodeGraph 已确认 `SetGuidingState(...)` 只有 OnOpen、OnClose、引导启动和完整引导结束四类调用点；`TryGoNextPage()` 只有玩家按钮与引导 Next 两个调用点。
- `git diff --check` 通过；CodeGraph 与静态扫描确认 `OnOpen()` 在 `LoadInitialPage()` 前读取 `IsGuiding` 并同步遮罩，且未发现 `_isGuiding` 继续参与按钮 `Interactable`、点击入口或 `allowWhileGuiding` 参数。prefab 与 `UIEmptyRaycastTarget` 源码静态核对确认遮罩默认关闭、不绘制网格、可拦射线且已绑定。
- `Assembly-CSharp.csproj` 已使用 `dotnet build --no-restore` 验证通过：0 error / 99 warnings；warnings 为当前工程输出，本次未逐项审计。
- Unity PlayMode 尚未验证完整引导期间底层按钮/页面 item 均不可点、页内 GuidePart 与动态 GuideFinger 可点、第四阶段结束立即恢复，以及 Popup 快速关闭/重开；Android/iOS 横屏触摸也未验证。
- 回滚：恢复 `_isGuiding` 的按钮条件与点击防护、`TryGoNextPage(bool allowWhileGuiding)`，删除 `SetGuidingState(...)` 和 `clickBlock` 绑定/节点；不涉及 DB、配置或资源迁移。

## 2026-07-17 StartGuding 前移到 PostcardPopup 创建前

### 需求、原行为与影响范围

- 需求：创建引导用 `PostcardPopup` 时，`DB.Postcard.IsGuiding` 必须已经为 true，Popup 首帧立即开启 `ClickBlock`；从 Popup 出现到完整 Guide 结束，不允许普通点击穿透到底层 `PropBlock`、页面或导航按钮。
- 原行为：`NextStepGuideAsync()` 先创建 `PostcardPopup`，等待并展示 Stage Two，直到 Stage Two 关闭回调才调用 `PostcardManager.StartGuding()`；因此 Popup 打开到 Stage Two 关闭之间，`OnOpen()` 读取到 `IsGuiding=false`，`ClickBlock` 保持关闭。
- 影响文件：`GameVictoryPanel.cs`、`PostcardManager.cs` 及本地说明/风险文档。未修改 prefab、DB 字段结构、配置、资源地址、奖励、全局事件、analytics 或平台 SDK。

### 修改后行为与数据/UI 流

- `NextStepGuideAsync()` 现在先校验 `PostcardManager`，调用 `StartGuding()` 写入并保存 `HasGuide=true`、`IsGuiding=true`，随后才调用 `PostcardPopup.Create()`。
- `PostcardPopup.OnOpen()` 因此在加载页面前读取到 `IsGuiding=true`，通过既有 `SetGuidingState(true)` 首帧开启全屏 `ClickBlock`。Stage Two 关闭回调只调用 `StartPendingPostcardGuide()`，不再重复写入引导状态。
- `StartPendingPostcardGuide()` 启动页内 GuidePart 后仍会消费持久化 `IsGuiding`，但 Popup 本地 `_isGuiding` 与 `ClickBlock` 保持开启；正常路径继续到 Stage Three、Next GuideFinger、Stage Four，只有 Stage Four 关闭回调执行 `CompletePostcardGuiding()` 后关闭遮罩。
- 既有资源/目标创建失败路径仍会调用 `CompletePostcardGuiding()` 恢复交互，避免因 GuidePart、GuidePopup 或 GuideFinger 缺失造成永久软锁；这属于异常降级，不是正常流程的中途解锁。

### 验证、剩余风险与回滚

- 已静态核对调用顺序为 `StartGuding()` -> `PostcardPopup.Create()` -> Stage Two -> `StartPendingPostcardGuide()`，且 `StartGuding()` 在工程内没有残留的 Stage Two 关闭回调调用。
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 已通过：0 errors / 1061 warnings；warnings 为包含项目引用后的全工程现有输出，本次未逐项审计。首次关闭项目引用的构建因 `Temp/bin/Debug` 缺少 Unity 生成 DLL 而未进入本次代码编译，改为构建项目引用后通过。
- 尚未在 Unity PlayMode 验证 Popup Create 首帧、等待 1.5 秒、Stage Two、GuidePart、领奖、Stage Three/Four 全链路的实际 EventSystem 射线；Android/iOS 横屏、多指和不同宽高比也未验证。
- 若 `PostcardPopup.Create()` 失败，已保存的 `IsGuiding=true` 会作为 pending 状态保留，后续打开 Popup 仍会按既有恢复机制开启遮罩；该异常恢复行为需 PlayMode 故障注入确认。
- 回滚时把 `StartGuding()` 移回 Stage Two 关闭回调，并恢复回调内的 `PostcardManager` 有效性检查；无需迁移 DB、配置或资源。

## 2026-07-17 页面切换表现期间 ClickBlock 拦截

### 需求、基线与影响范围

- 需求：`PostcardPopup` 播放页面切换表现时，直到动画流程结束前禁止任何点击操作。
- 原行为：`IsPagePlayingAnim` 只禁用 Close、Last、Next 并在三个点击入口做保护；页面 item 等其他射线目标没有统一全屏拦截。
- 影响文件：`PostcardPopup.cs`、`PostcardPopup_PageCompleteProcess.cs` 及本地说明/风险文档。复用现有 prefab `ClickBlock`，未修改 prefab、TurnEffect、页面资源、DB、配置、奖励、事件、analytics 或平台 SDK。

### 修改后行为与 UI 流

- `OnPagePlayAnim(true)` 在解锁/切换表现开始时同步开启 `clickBlock`，`OnPagePlayAnim(false)` 在表现稳定后解除动画阻塞。
- 整页完成流程原先直接写入 `IsPagePlayingAnim` 的开始和 `finally` 结束点，现统一调用 `SetPagePlayingAnimationState(...)`，确保 grayImage、transform、Chest、领奖等待等完整流程均同步遮罩。
- `RefreshClickBlock()` 合并 `_isGuiding || IsPagePlayingAnim`。动画结束时若完整明信片引导仍在进行，遮罩继续保持；引导结束时若动画仍在播放，遮罩同样不会提前关闭。
- 既有按钮 `Interactable` 和点击入口判断保留为防御性保护；本次不改变解锁、完成判定、扣星、保存、奖励或引导推进时机。

### 验证、风险与回滚

- 静态扫描确认 `IsPagePlayingAnim` 只由 `SetPagePlayingAnimationState(...)` 写入，PageItem 解锁和 PageComplete 两类入口均同步 ClickBlock。
- `dotnet build Assembly-CSharp.csproj` 通过：0 errors / 1062 warnings。首次 `--no-restore` 因 Unity Temp 缺少 NuGet 资产文件未进入代码编译，正常 restore/build 后通过。
- 尚未在 Unity PlayMode 验证解锁成功、最后一块整页完成、动画取消、引导重叠时的实际射线拦截与释放帧；Android/iOS 横屏、多指输入未验证。
- 回滚本次行为时让 `SetPagePlayingAnimationState(...)` 只更新 `IsPagePlayingAnim`，并让 `RefreshClickBlock()` 恢复只读取 `_isGuiding`；无需修改 prefab、DB、配置或资源。

## 2026-07-18 修复 ClickBlock 首帧未展开

### 需求、基线与原因

- 需求：引导路径创建 `PostcardPopup` 后，`ClickBlock` 必须在 Popup 首个可渲染帧按 `DB.Postcard.IsGuiding` 立即展开，不能等待 Stage Two 或后续 Flow2 才生效。
- 基线：`PostcardGuideFlow` 已在 `PostcardPopup.Create()` 前调用 `PostcardManager.StartGuding()`，`PostcardPopup.OnOpen()` 也会同步读取 `IsGuiding`；但当前 prefab 的 `PostcardPopup.clickBlock` 序列化引用被清空，导致 `RefreshClickBlock()` 在引用为空时直接返回。
- 直接原因不是异步时序。现有 `UIManager.OpenUI(...)` 在同一次同步调用内依次执行 `OnCreate()`、`OnOpen()`、`OnShow()`；真正缺口是 Popup 脚本没有取得既有 `ClickBlock` 节点。

### 修改后行为与影响范围

- 恢复 `PostcardPopup.prefab` 对既有嵌套 `ClickBlock` GameObject 的序列化绑定；节点、层级、默认 inactive 状态和 `UIEmptyRaycastTarget` 组件保持不变。
- `PostcardPopup.OnCreate()` 预先解析一次遮罩；`RefreshClickBlock()` 在序列化引用为空时，会在当前 Popup 层级内按名称查找既有 `ClickBlock`，再同步执行 `SetActive(_isGuiding || IsPagePlayingAnim)`。
- 正常引导路径中，`StartGuding()` 仍先写入 `IsGuiding=true`，随后 `OnOpen()` 在首个可渲染帧前展开遮罩；普通打开则在同一时序保持关闭。
- 本次只影响 `PostcardPopup.cs` 与 prefab 绑定，不新增对象、全局事件、配置、存档字段、资源地址、奖励、analytics 或平台分支。

### 验证、风险与回滚

- 静态校验确认 prefab 同时存在 `clickBlock: {fileID: 1718513514333433968}` 绑定及对应 nested prefab stripped GameObject。
- `git diff --check` 通过；`dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly` 通过，结果为 0 error / 99 warnings。
- Unity PlayMode 的首帧 `activeSelf`、实际 EventSystem 射线、缓存实例重开和 Android/iOS 横屏触摸仍待验证。
- 回滚时删除 `ResolveClickBlock()` 与 `OnCreate()` 的预解析，并再次移除 prefab 绑定即可；不涉及 DB、配置或资源迁移。

## 2026-07-23 页面完成奖励前置发放

### 需求、原行为与影响范围

- 需求：明信片整页完成后，不应把普通奖励的真实获取依赖于完整完成动画和 `CommonGetRewardPopup` 成功打开；动画中断、Popup 销毁或奖励弹窗创建失败时，已经保存的完成页不能因此丢失奖励。
- 原行为：`Postcard_PageItem.TryUnlockItem(...)` 在确认整页完成时先把当前配置 id 写入 `DB.Postcard.CompletedId` 并执行 `DB.SavePostcard()`；随后等待解锁动画回调进入 `PostcardPopup.OnPageComplete()`，而 Coin、Star、Skin 要到完成变形、Chest 动画和固定等待结束后，创建 `CommonGetRewardPopup` 时才发放。
- 影响文件：`Postcard_PageItem.cs`、`PostcardPopup_PageCompleteProcess.cs`、通用奖励弹窗说明、本地功能/风险文档和共享设计记录。复用现有 `SimpleBag.GrantRewards(...)`、`SimpleBag.GetNewSkin(...)` 与 `CommonGetRewardPopup` 的纯展示开关；未新增 DB 字段、全局事件、配置、prefab、资源地址、analytics 或平台分支。

### 修改后行为与数据、保存、UI 流

1. 正常路径在 `Postcard_PageItem.TryUnlockItem(...)` 的 `DB.SavePostcard()` 成功、扣除本次解锁星星之后、启动解锁动画之前，同步调用 Popup 的奖励入口；因此后续解锁动画没有回调也不再阻止 normal 奖励入包。
2. Coin、Star 由 `SimpleBag.GrantRewards(..., RewardType.Other)` 发放；可解析的 Skin 继续使用 `SimpleBag.GetNewSkin(..., GetSkinWayType.Reward)`，沿用原保存、事件和 SDK 上报路径。
3. `HandlePageCompleteAsync()` 在首个 `await` 前再次调用同一入口作为兜底；同一 `PostcardPopup` 生命周期内使用明信片配置 id 记录已处理页，正常前置已成功时不会重复发放。
4. 前置发放成功后，完成变形、Chest 和延时仍按原顺序播放；随后使用 `grantNormalReward: false` 打开 `CommonGetRewardPopup`，弹窗只负责展示、关闭、引导点击和广告额外数量奖励。
5. 若 Bag/GameData 未就绪或奖励配置无法由当前 Coin、Star、Skin 规则处理，前置入口返回失败；奖励弹窗保留 `grantNormalReward: true` 的旧发奖行为作为降级，避免因依赖初始化时序直接改变既有路径。
6. Skin 转换弹窗和可选 ShopPanel 等待继续复用前置解析出的同一 Skin 配置；玩家可见的完成表现顺序不变，只有 normal 奖励入包时机提前。

### 验证、剩余风险与回滚

- CodeGraph 与目标源码静态复核确认：正常解锁路径在 `SavePostcard()` 成功和扣星后、`PlayUnlockAnim(...)` 前调用奖励入口；`HandlePageCompleteAsync()` 在首个异步等待前复用该入口兜底；`WaitRewardPopupCloseAsync(...)` 按最终结果切换 `grantNormalReward`。
- `dotnet build Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过：0 errors / 99 warnings；warnings 为当前工程输出，本次未逐项审计。目标 `git diff --check` 通过。
- `DB.SavePostcard()`、扣星和奖励入包是连续但独立的同步调用；应用若恰在这些调用之间被系统强制终止，仍可能出现完成 id、星星消耗和奖励存档不同步。本次没有新增“待领取/已领取”持久化状态，不能把这种跨存档窗口描述为已消除。
- Postcard 完成存档与 GameData 奖励存档仍是两条独立保存链，不具备跨存档事务原子性；本地 id 集合只负责当前 Popup 生命周期幂等，不能替代持久化领取标记。
- 尚未在 Unity PlayMode 验证 Coin、Star、Skin、广告倍率、流程中断、Popup 创建失败、同页重复回调及引导路径；Android/iOS 横屏与强退恢复也未验证。
- 回滚时删除 `TryGrantPageCompleteNormalRewards(...)` 和本地已处理 id 集合，恢复完成动画后由 `CommonGetRewardPopup.Create(postcardConfig, callback)` 默认发放即可；无需迁移 DB、配置或资源。

## 2026-07-23 Coin PropBlock 手动刷新时机

### 需求、基线与影响范围

- 需求：`PostcardPopup` 顶部 Coin `PropBlock` 需要显示余额，但页面完成奖励已经前置入包时，数字不能在 Chest、奖励弹窗和飞行动画之前提前跳变。
- 当前 prefab 已存在 Coin `PropBlock` 实例，并把 `IsAutoUpdate` 设为 false；修改前 `PostcardPopup` 没有缓存或手动刷新该组件，因此奖励展示结束后可能一直保留旧数字。
- 影响文件：`PostcardPopup.cs`、`PostcardPopup_PageCompleteProcess.cs` 及本地功能/风险文档。未修改 `PropBlock` 共享实现、prefab 层级、奖励数量、发奖/存档、资源、事件、analytics 或平台分支。

### 修改后 UI 时序

1. `PostcardPopup.OnCreate()` 缓存 Coin `PropBlock`；序列化引用为空时，只在当前 Popup 子级查找既有组件，不实例化对象。
2. `OnOpen()` 清理上次生命周期的待刷新标记，并调用 `UpdateData()` 同步打开时的真实 Coin 余额。
3. 页面完成 normal 奖励仍按既有前置逻辑立即入包；由于 prefab 的 `IsAutoUpdate=false`，父 Popup 的 Coin 数字保持奖励前的显示。
4. 打开 `CommonGetRewardPopup` 前记录待刷新标记。奖励弹窗正常关闭后，由父 Popup 的 `OnShow()` 或等待完成后的兜底入口调用一次 `UpdateData()`；若奖励弹窗创建失败，则立即刷新。
5. `OnShow()` 与异步等待可能先后到达，但待刷新标记只会被成功刷新一次；后续 Skin 转换弹窗恢复显示不会重复刷新 Coin。

### 验证、风险与回滚

- CodeGraph 复核确认 `WaitRewardPopupCloseAsync(...)` 设置待刷新标记，刷新只进入 `RefreshCoinAfterRewardPopupIfPending()`；`OnOpen()` 只负责打开时基线同步。
- 两个目标 prefab 的静态 YAML 均确认新增 PropBlock 覆写 `IsAutoUpdate: 0`。C# 构建通过：0 errors / 99 warnings；目标差异格式检查通过。
- 尚未在 Unity PlayMode 验证 normal Coin、广告额外 Coin、无 Coin、奖励弹窗创建失败、引导点击、Skin 转换和 Popup 缓存重开；Android/iOS 横屏未验证。
- 若 prefab 后续把 `IsAutoUpdate` 改回 true，`UpdatePropEvent` 会在前置发奖时立即改写数字，手动时序将失效；该 prefab 设置必须保留。
- 回滚时删除 `coinPropBlock`、待刷新标记、`OnShow()` 刷新和奖励弹窗边界调用即可；不涉及存档、配置或资源迁移。

## 2026-08-19 引导后一次性 TipsBubble

### 需求、基线与影响范围

- 需求：首次明信片引导结束后，玩家后续打开 `PostcardPopup` 时只展示一次提示气泡；气泡以 0.3 秒弹性缩放出现，点击或完整停留 3 秒后缩放关闭。
- 修改前：Postcard 存档只有 `HasGuide/IsGuiding`，`PostcardPopup` 没有一次性气泡入口；工作区已有用户新增的 `hasShowTipsBubble` 字段、`Postcard_TipsBubble.prefab` 及其 Popup 嵌套实例，但字段尚无公开属性、初始化和保存消费链，prefab 也没有运行时控制组件。
- 现有状态语义：`PostcardManager.StartGuding()` 在首次引导用 Popup 创建前把 `HasGuide/IsGuiding` 同时置为 true；`PostcardPopup` 启动 GuidePart 后消费 `IsGuiding=false`，`HasGuide` 保持 true。因此本需求使用 `HasGuide && !IsGuiding && !HasShowTipsBubble` 作为后续打开门槛，并且只在 `OnOpen()` 检查，不在当前引导结束帧内自动弹出。
- 影响文件与系统：`Postcard.cs`、`Postcard_Partial.cs`、`PostcardPopup.cs`、新增 `PostcardPopupTipsBubble.cs`、`Postcard_TipsBubble.prefab` 和已有嵌套该 prefab 的 `PostcardPopup.prefab`；复用 Bear SaveModule 的 `DB.SavePostcard()`、DOTween 和 Unity EventSystem，不新增全局事件、资源加载、配置、奖励、analytics 或平台 SDK 分支。
- `specs/035-postcard-guide-part/plan.md` 原“无新增 DB 字段”描述属于此前 GuidePart 范围；本次用户后续需求显式新增一次性提示存档，行为边界以本节记录为准，未改写原 GuidePart 的解锁、奖励或 Flow 顺序。

### 修改后数据、UI 与生命周期流

1. `Postcard.Init()` 将 `hasShowTipsBubble` 默认初始化为 false，`HasShowTipsBubble` 提供运行态读写边界；已有存档没有该序列化字段时沿用 bool 默认 false，首次满足门槛时可以展示。
2. `PostcardPopup.OnOpen()` 在运行态取得 Postcard 数据并完成初始页面加载后刷新气泡。`HasGuide=false`、`IsGuiding=true`、`HasShowTipsBubble=true`、DB 不可用或组件缺失时均不展示；组件缺失或表现未启动时不消费标记。
3. `PostcardPopupTipsBubble.TryShow()` 把自身缩放置零，使用 unscaled DOTween 在 0.3 秒内以 `Ease.OutBounce` 恢复 prefab 原始比例，完整停留 3 秒后用 0.2 秒 `DOScale(Vector3.zero)` 关闭。DOTween 没有 `OutBound` 枚举，本实现采用语义最接近的 `OutBounce`。
4. prefab 子级 `bubble` Image 保持 `RaycastTarget=true`；点击由根节点 `IPointerClickHandler` 接收，立即中断等待/展开序列并播放同一 0.2 秒缩放关闭。自动关闭和点击关闭最终都把气泡根节点设为 inactive，并恢复原始比例供缓存实例清理。
5. 只有 `TryShow()` 成功启动后，Popup 才立即设置 `HasShowTipsBubble=true` 并调用 `DB.SavePostcard()`；因此关闭 Popup、点击关闭或自动关闭都不会在正常保存成功后再次展示。保存失败会记录 warning 并保留本次会话内的 true，但杀进程后的持久化不能保证。
6. `PostcardPopup.OnClose()` 主动立即隐藏气泡并终止 Tween；没有 `Update`、协程、运行时实例化或编辑器生命周期 DB 入口。

### 验证、剩余风险与回滚

- CodeGraph 与源码静态复核确认 `HasGuide` 在引导开始时置 true、`IsGuiding` 在 GuidePart 启动时消费，门槛检查仅位于 `PostcardPopup.OnOpen()`。
- prefab 静态校验确认新脚本 GUID 在 `Postcard_TipsBubble.prefab` 中唯一绑定，气泡源 prefab 默认 inactive，`PostcardPopup.prefab` 继续通过既有 prefab GUID 嵌套该气泡；脚本、存档字段、0.3/3/0.2 秒时序和 `OutBounce` 静态扫描通过。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo --verbosity:minimal` 通过：0 warning / 0 error。目标 `git diff --check` 在清理已有 Popup prefab 新增空字段尾随空格后通过。
- Unity PlayMode 尚未验证首次引导打开时不展示、引导结束后关闭重开只展示一次、点击命中、3 秒计时、缓存实例重开、保存失败和多语言文本布局；Android/iOS 横屏、安全区、触摸及低帧率观感仍为 `uncertain`。
- 回滚时移除 `PostcardPopupTipsBubble` 脚本及 prefab 组件、Popup 的刷新/清理入口、`HasShowTipsBubble` 属性和初始化；如要完整撤销数据结构，再移除 `Postcard.cs` 的字段。已有 PlayerPrefs 数据中多出的字段可被旧代码忽略，不涉及奖励、配置或资源地址迁移。

## 2026-08-19：Postcard 页面资源由 BearResources / YooAsset 内置托管

### 需求、原行为与影响范围

- 需求：本阶段不启用远程下载，先把明信片页面 prefab 从 Unity `Resources` 迁移到现有 YooAsset `GamePackage`，并统一使用 BearResources 加载。
- 原行为：`PostcardPopup` 根据 `PostcardConfig.PrefabPath` 同步调用 `Resources.Load<Postcard_PageItem>(...)`；页面切换和关闭只销毁实例，没有显式的资源句柄释放边界。
- 影响文件与资源：`PostcardPopup.cs`、16 个 `Postcard_*.prefab` 及其 `.meta`、`AssetBundleCollectorSetting.asset`、Postcard prefab 对齐工具和两个项目 Skill 的扫描路径。`PostcardPopup.prefab` 外壳继续保留在 `Resources`，配置表字段和值、存档结构、奖励、引导事件和平台 SDK 均未修改。
- 本迁移属于用户在 GuidePart 之后单独批准的资源管理改造，不把 `specs/035-postcard-guide-part/plan.md` 中“资源系统不在范围内”的原描述静默改写为历史需求。

### 修改后资源与生命周期流

1. 16 个页面 prefab 连同原 `.meta` 移至 `Assets/Game/UI/Postcard/Prefabs/Pages/`，因此资产 GUID 保持不变；旧 `Resources` 页面目录不再保留 `Postcard_*.prefab`。
2. YooAsset `GamePackage` 新增 `Postcard` 收集组，收集上述目录中的 prefab，标签为 `postcard`，打包规则为 `PackSeparately`。当前构建仍使用内置资源复制策略，没有新增远端 Host、版本服务或下载界面。
3. 配置仍保存原短名。Popup 在运行时把短名转换为 `Assets/Game/UI/Postcard/Prefabs/Pages/<prefabPath>.prefab`，通过 `Bear.ResourceSystem.ResourceManager.Load<GameObject>()` 同步加载，并校验根节点存在 `Postcard_PageItem`。
4. 页面切换和 Popup 关闭会销毁实例并调用 BearResources `Release(path)`；同一路径重建时暂时保留现有句柄，避免先释放再从同一资源实例化。加载、存档或实例化失败时，只释放本次新取得且未被当前页面占用的路径。
5. 引导期间若页面资源缺失、未进入清单或组件不完整，Popup 会停止本次 Guide Flow、消费已有 `IsGuiding` 待处理状态并解除本地输入锁，避免界面永久不可操作；`HasGuide` 保持原值，当前实现不会自动重试该次教程。
6. Android 与 iOS 共用同一加载代码；各平台仍必须生成并随包携带匹配的 YooAsset 清单和 AssetBundle。本阶段保持现有 `ClearAndCopyAll` 内置复制策略，因此资源托管方式已改变，但不能据此宣称 APK/IPA 已缩小。

### 验证、剩余风险与回滚

- 配置与文件静态核对：16 条 `PostcardConfig` 页面路径均能匹配新目录中的 prefab 和 `.meta`；旧 Resources 页面数为 0，新 YooAsset 页面数为 16。
- Git 将 16 组 prefab/meta 识别为重命名，原 GUID 得以保留；对这些 GUID 的工作区引用扫描未发现其他 Unity prefab/scene 对旧 Resources 路径的序列化依赖，命中仅为美术 `.layers.json` 和历史说明文档。
- `Assembly-CSharp.csproj` 完整定向构建通过：0 error / 106 warnings；`Assembly-CSharp-Editor.csproj` 构建通过：0 error / 4 warnings；目标差异格式检查通过。warnings 为当前工程输出，本次未逐项审计。
- 当前打开的 Unity Editor 日志尾部未发现本次脚本或 Collector 导入错误，但尚未执行正式 `GamePackage` 构建、构建报告依赖审计、PlayMode 切页/关闭测试，以及 Android/iOS 真机构建和横屏验证；这些项仍为 `uncertain`。
- `PackSeparately` 只限定页面主资产的打包粒度；共用贴图、字体、材质是否被重复或仍被 Player/其他 Bundle 引用，必须以正式构建报告为准。
- 回滚时把 16 个 prefab/meta 移回 `Assets/Game/UI/Postcard/Resources/`，移除 `Postcard` Collector，恢复 `Resources.Load` 及旧工具/Skill 路径，并删除 BearResources 释放和加载失败恢复逻辑即可；不需要迁移配置或存档。

## 2026-08-26：TipsBubble 支持 CustomButton 主动重显

### 需求、基线与影响范围

- 需求：保留 TipsBubble 原有一次性自动提示；原逻辑已经成功触发后，玩家可点击 Popup 顶部 StarBlock 下的 `CustomButton` 再次展示 bubble。
- 修改前：`PostcardPopupTipsBubble` 只接收首次 `TryShow()`、气泡自身点击关闭和 3 秒自动关闭；`HasShowTipsBubble=true` 后重新打开 Popup 不再展示，也没有主动重显入口。
- 影响文件：`PostcardPopupTipsBubble.cs`、`PostcardPopup.cs`、`PostcardPopup.prefab`。Prefab 沿用本次任务开始前已存在的 `StarBlock/clickArea` 与其 `CustomButton`，新增的只是 TipsBubble 组件到该按钮的显式序列化引用。
- 本次不修改 `CustomButton` 共享实现、`HasShowTipsBubble` 字段语义、Guide Flow、页面解锁、配置、资源加载、奖励、analytics、广告、IAP 或平台 SDK。

### 修改后 UI 与状态流

1. Popup 打开时先隐藏残留 bubble，再根据 `Postcard.HasShowTipsBubble` 调用 `SetReplayEnabled(...)` 同步重显资格。
2. 首次提示未成功触发时，重显按钮不可交互；首次 `TryShow()` 成功后仍立即写入 `HasShowTipsBubble=true` 并保存，同时开放后续重显资格。
3. bubble 正在展开、停留或关闭时，重显按钮保持不可交互；bubble 完全隐藏后按钮恢复可交互，点击复用原 `TryShow()` 动画与自动关闭时序。
4. 按钮重显只控制本地 UI，不再次读写 DB，也不改变一次性自动展示门槛；按钮事件使用幂等绑定并在组件销毁时解绑。

### 验证、剩余风险与回滚

- `Assembly-CSharp.csproj` 完整构建通过：0 error / 1138 warnings；warnings 为当前完整 Unity 工程既有输出，本次未逐项审计。
- 静态确认 prefab 的 `replayButton` 指向新增 `CustomButton` 组件，代码不存在 `Update` 轮询、全局事件或新的数据/资源链。
- Unity PlayMode 尚未验证首次自动展示、展示中连点、点击/超时关闭后重显、关闭重开以及 Android/iOS 横屏触摸；这些表现仍为 `uncertain`。
- 回滚时移除 `replayButton` 绑定、`SetReplayEnabled(...)` 调用和按钮订阅/交互状态逻辑，即恢复只允许一次性自动展示；无需迁移存档或配置。
