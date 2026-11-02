# PostcardPopup 页面流风险评估

## 2026-09-09 GM 单张刷新边界

- 原行为：GM 保存完成状态后不刷新已打开的页面。Editor.log 证实点击和成功保存分支已执行，不能把用户最初的“无反应”直接归因为按钮事件未绑定。
- 当前实现：GM 通过已有 UIManager 获取实例，写入前检查 `CanApplyGmUnlock`；保存成功后以 `RefreshAfterGmUnlock -> ShowConfigPage(index, false)` 显示本次完成页。不增加全局事件，不修改正常解锁和奖励流程，不重复保存 CurrentId。
- 风险控制：页面动画、领奖或引导期间拒绝 GM 写入；加载失败不虚报页面已刷新。资源重建沿用原切页释放/加载链，每次 GM 点击最多一次，不增加每帧轮询。
- 验证：修改后的完整运行时源码编译 0 error；用户随后确认有效并结束排查。初版 10 项替身检查仅覆盖数据逻辑，不能充当新增 UI 调用的运行证据。
- `uncertain`：真实页面刷新、资源失败、缓存重开、引导/领奖中点击、Android/iOS 横屏未逐项实测；用户确认未细分这些场景。
- 回滚：删除两个 GM 专用成员及 GM 调用即可恢复原刷新边界；已写入的测试存档不自动回退。详见 `DevelopMD/GMPanel_Reference.md` 第 23 节。

## 2026-07-18 HasGuide 前置与首帧 ClickBlock 风险补充

### 事实与直接结论

- Flow1 当前在入口 Finger 完成后、`PostcardPopup.Create()` 前调用 `StartGuding()`；Popup `OnOpen()` 在页面加载前读取 `IsGuiding` 并刷新 ClickBlock。
- Victory 引导锁在 Stage One 完成、入口 Finger 出现前释放；星星动画锁仍独立参与 Victory ClickBlock，不会被引导锁释放覆盖。
- Popup 引导锁继续与页面动画锁取或：`_isGuiding || IsPagePlayingAnim`；Flow1 创建后的显式补锁与 Flow2 接管保持幂等。
- **P1 已缓解：Popup 打开首帧点击穿透。** pending 状态先于 UI 创建写入，Popup 不再依赖创建完成后的下一条语句才开启遮罩。
- **P1 已缓解：Victory Finger 被底层遮罩持续阻挡。** 引导锁在 Finger 等待前释放；动态 Finger 可以按既有层级接收点击。

### 风险与缺失证据

- **P1：中断恢复。** `StartGuding()` 已经写入后，如果 Popup 创建失败或 Stage Two 被取消，`IsGuiding=true` 可能保留；下次 Popup 打开会先开启遮罩，但是否应自动恢复 Flow2 或消费状态尚未有产品与 PlayMode 证据。
- **P1：Prefab 射线层级。** 当前工作区 `PostcardPopup.prefab` 有用户未提交改动，ClickBlock、GuidePart、Stage Two 和动态 Finger 的实际射线结果未验证。
- **P2：移动端。** Android/iOS 横屏、安全区和多指输入未验证。
- 构建通过：0 error / 99 warnings；回滚只涉及调用顺序和 Popup 首帧状态初始化，不涉及 DB schema、配置或资源迁移。

## 2026-07-18 GuideFlow 合并修复风险补充

### 事实

- 修改前是不可编译的新旧混用状态：胜利页调用已移除旧引导类型，Flow1/页面完成 partial 调用的新 Popup/Flow2 契约又被删除，构建共报告 13 个相关错误。
- 当前 Flow1 归 `GameVictoryPanel`，Flow2 归 `PostcardPopup`；两个宿主都在关闭边界主动取消自己持有的流程。
- 胜利页遮罩由“引导或星星动画”控制，Popup 遮罩由“引导或页面动画”控制；develop 的 2 秒完成效果等待未修改。
- 本次没有修改任务开始前已处于未提交状态的 `PostcardPopup.prefab`。

### 直接结论

- **编译阻断已解除。** 相同 C# 构建命令由修改前 13 个相关错误变为 0 error / 99 warnings。
- **结构所有权已恢复。** 旧引导类型与新 Flow 不再并行编排；页面完成和失败信号存在唯一 Flow2 接收方。
- **交互锁不会因单一状态结束而提前释放。** 两个界面都用本地布尔状态合并刷新同一 ClickBlock，并保留按钮入口防护作为代码级降级。

### 假设与缺失证据

- **P1：Prefab 射线层级待验证。** 当前工作区 prefab 有用户未提交改动；ClickBlock、GuidePart、动态 GuideFinger 与奖励弹窗的实际 EventSystem 命中尚未在 PlayMode 核对。
- **P1：取消与复用时序待验证。** 快速关闭并立即重开时，旧 Flow2 的异步 `OnFlowStopped()` 是否可能晚于新流程刷新遮罩，需要运行态覆盖。
- **P1：完整四阶段待验证。** GuidePart 完成、页面完成、奖励、Stage Three、引导切页和 Stage Four 尚未完成真实流程回归。
- **P2：移动端输入待验证。** Android/iOS 横屏、安全区、多指输入和低帧率下的遮罩与时序没有设备证据。
- 回滚必须成套处理 Flow1/Flow2 宿主和页面完成通知；仅回滚单个脚本会再次产生契约不一致。无数据迁移风险。

## 2026-07-17 CompleteEffects 固定等待 2 秒风险补充

### 事实与直接结论

- 修改前 `PlayCompleteEffects()` 在有效粒子调用 `Play(true)` 后立即返回，PageItem 随即触发 Animator `transform`。
- 当前实现在至少一个有效粒子发射后固定等待 2 秒，等待绑定 `PostcardPopup.GetCancellationTokenOnDestroy()`；对象销毁后 PageItem 的既有取消检查会阻止后续 `transform` 和 Chest 流程。
- 等待只代表粒子发射后的固定时间窗，不代表 ParticleSystem 完整生命周期；循环根粒子仍使用不阻塞的后台任务在自身 `duration` 后 `StopEmitting`。
- 本次不修改 DB/save、奖励、配置、prefab、资源加载、全局事件、analytics 或 Android/iOS 平台分支。

### 风险、缺失证据与回滚

- **P2：完成节奏变慢。** 满进度 Chest 会在粒子发射后多保持 2 秒待开启，叠加后续 1.7 秒 `transform`/正文窗口；是否过慢需以 Unity PlayMode 观感决定。
- **P2：粒子结束与 `transform` 仍不完全对齐。** 寿命小于 2 秒的粒子会先结束，寿命大于 2 秒或循环 duration 更长的粒子会继续与 `transform` 重叠；这是固定等待方案的预期边界。
- `git diff --check` 通过；C# 工程构建通过，结果为 0 errors / 100 warnings，warnings 未逐项审计。尚未在 Unity PlayMode、快速关闭或 Android/iOS 横屏真机上验证。
- 回滚时删除 `CompleteEffectsPostDelay` 及对应 `WaitForSeconds`，恢复 `PlayCompleteEffects()` 立即返回；不需要修改 prefab、DB、配置或资源。

## 2026-07-17 打开时定位首个未完成页风险补充

### 事实

- `TryLoadPostcardConfigs()` 会排除 unlock 占位配置，并通过 `ComparePostcardConfigSort` 按 `Sort`、同值 ID 顺序生成 `_postcardConfigs`。
- `TryResolveInitialIndex(...)` 仅在 `CurrentId` 能命中普通配置且该 ID 已存在于 `CompletedId` 时扫描未完成页；扫描结果通过既有 `SaveCurrentId(...)` 保存。
- `SaveCurrentId(...)` 保存失败会恢复原 `CurrentId`；本次未修改完成判定、页资源加载、奖励、Guide、事件或平台链路。

### 直接结论

- 正常顺序存档下，完成当前页后关闭并重开会进入排序后的下一张未完成页；若存在异常的前序未完成数据，则会优先回到排序序列中最早的未完成页，与明信片红点的“最近未完成页”推进口径一致。
- 全部完成、当前页未完成、`CurrentId` 无效/为空以及保存失败均保留明确降级路径，不会因为自动定位失败而清除原 ID。

### 假设与缺失证据

- 假设配置 `Id` 唯一且 Luban 已正常初始化；该约束来自现有表加载结构，本次未新增运行时重复 ID 校验。
- 缺失 Unity PlayMode、设备端 Android/iOS 横屏、真实存档失败注入和异常“前序未完成、后序已完成”数据的运行验证；当前结论限于代码、配置排序与 C# 构建证据。

### 回滚

- 删除 `TryResolveInitialIndex(...)` 内已完成页扫描分支即可恢复原始打开行为；无 DB schema、配置表、prefab 或资源迁移成本。

## 2026-07-13 完成变形后取消自动翻页风险补充

### 事实与直接结论

- 修改前，`HandlePageCompleteAsync()` 在 `_currentPage.PlayCompleteTransformAsync()` 成功返回后调用 `ShowNextPageAfterComplete()`，后者在存在下一条普通配置时调用 `ShowConfigPage(nextConfigIndex, true)`。
- 修改后，自动切页调用和对应私有方法已删除；异步流程进入 `finally` 后释放完成处理状态、恢复按钮交互并保留当前页。
- 本次不改 `DB.Postcard.CompletedId` 写入、奖励发放、Chest、Skin/Shop、Guide、资源加载、手动翻页或 `SaveCurrentId(...)` 逻辑。

### 风险、验证缺口与回滚

- **P2：完成页停留后的导航可用性。** 静态代码显示 `finally` 会调用 `RefreshButtonStates()`，有后续普通配置时 `NextBtn` 应恢复可见与可交互；Unity PlayMode 尚未验证实际按钮状态和触摸层级。
- **P2：完成表现分支收尾。** Skin 转换、ShopPanel 等待或 Guide 收尾分支仍需验证在不自动换页后不会遗留遮罩、交互锁或重复弹窗。
- **移动端未验证。** Android/iOS 横屏下完成页停留、手动 Next 触摸、布局和性能尚无真机证据。
- **验证证据。** 目标文件 `git diff --check` 通过；静态扫描未发现自动切页调用残留或新增编辑器生命周期、全局事件、存档、资源加载链路；`Assembly-CSharp.csproj` 构建通过，0 error / 98 warnings。
- 回滚时恢复 `ShowNextPageAfterComplete()` 调用及原私有方法；不涉及 DB、配置、prefab 或资源迁移。

## 评估范围

- 评估日期：2026-07-06；2026-07-07 补充最后一张普通明信片完成后的 `NextBtn -> Postcard_unlock` 末尾占位页流程；2026-07-07 补充完成奖励后的 Skin 转换弹窗流程；2026-07-09 补充整页 Complete 入口的 Chest 开箱动画等待和 Inspector 可调等待参数；2026-07-13 补充完成变形动画后取消自动翻页
- 变更入口：`PostcardPopup` 配置读取、页面加载、上一页/下一页、配置化锁定占位页、标题多语言刷新、Chest 刷新、动画交互锁、整页完成后的 Chest 开箱动画、奖励弹窗流程、奖励关闭后的 Skin 转换弹窗流程、完成变形后保留当前页，以及最后一张普通页完成后的末尾占位页按钮状态
- 涉及文件：`Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs`、`Assets/Game/UI/Postcard/Scripts/PostcardPopup_PageCompleteProcess.cs`、`Assets/Game/UI/Postcard/Scripts/Postcard_Chest.cs`、`Assets/Game/UI/GetSkinPopup/Scripts/GetSkinTransformPopup/GetSkinTransformPopup.cs`；`Assembly-CSharp.csproj` 作为 Unity/IDE 生成文件仅用于本地 `dotnet build` 同步验证
- 涉及系统：明信片弹窗、Bear UI 弹窗栈、Resources 页面资源、`DB.Postcard` 存档、`Postcard_PageItem` 解锁动画回调、`Postcard_Chest` 宝箱 Spine 表现、`sp_box-open_002` Spine 资源、`CommonGetRewardPopup` 领奖弹窗、`GetSkinTransformPopup` 皮肤展示弹窗、`ConfigManager.Tables.TbSkin` 皮肤配置查询

## 原始行为基线

- `PostcardPopup.Create()` 使用 `UIManager.Instance.OpenUI<PostcardPopup>(..., UILayer.Popup)` 打开弹窗。
- 旧实现只处理 `CloseBtn` 点击销毁弹窗，不创建明信片页面。
- `LastBtn`、`NextBtn` 虽已由生成脚本和 prefab 绑定，但没有逻辑。
- `Postcard_PageItem` 已能按配置和存档刷新 item 解锁状态，并在解锁动画前后调用 `PostcardPopup.OnPagePlayAnim(...)`，但弹窗侧缺少对应 API。

## 变更后链路

1. 玩家打开 `PostcardPopup`。
2. 弹窗通过场景中已初始化的 `ConfigManager` 和 `DBManager` 读取 `TbPostcardConfig.DataList` 与 `DB.Postcard.CurrentId`。
3. 弹窗把 `unlockProgress` 为空、`rewards` 为空且 `rewardMultiple <= 0` 的配置行识别为锁定占位页，其余配置作为普通明信片页。
4. `CurrentId` 无效时初始化为第一条普通明信片配置 id 并保存 `Postcard`。
5. 弹窗按当前配置的 `PrefabPath` 加载页面 prefab 并挂到 `Container`，再用 `ThemeName` 刷新标题文本，并按当前配置和存档状态刷新 Chest。
6. 当前页未完成且不是最后一张普通页时点击 `NextBtn`，会显示作为特殊 `Postcard_PageItem` 的 `Postcard_unlock`，并隐藏下一页按钮。
7. 当前页已完成且后面存在下一条普通明信片配置时点击 `NextBtn`，进入下一条普通明信片配置并保存新的 `CurrentId`。
8. 全部普通明信片完成后，`RefreshButtonStates()` 隐藏 `NextBtn`；点击入口也会按全部完成状态拦截 `ShowUnlockPage()`，并继续保留当前普通页 id。
9. `Postcard_PageItem` 播放解锁动画期间，弹窗按钮和 item 点击均被拦截。
10. 当前页整页首次完成时，`Postcard_PageItem` 等待 CompleteAnim 播放完成后才回调 `PostcardPopup.OnPageComplete()`。
11. `OnPageComplete()` 位于 `PostcardPopup_PageCompleteProcess.cs` partial，锁定弹窗交互，刷新 Chest 后调用 `Postcard_Chest.PlayChestOpenAnim()`。
12. 若开箱动画返回有效 `Spine.TrackEntry`，Popup 等待其 `Complete` 或 `End`，再按 Inspector 字段 `chestOpenCompleteExtraDelay` 额外停留；若无有效 TrackEntry，则降级等待 `pageCompleteStepDelay`。
13. 开箱等待和额外停留结束后，Popup 隐藏 Chest，再等待 `pageCompleteStepDelay` 后打开 `CommonGetRewardPopup`。
14. `CommonGetRewardPopup` 关闭回调触发后，Popup 从当前 `PostcardConfig.Rewards` 中解析 `GameProps.Skin`；解析到有效 `Config.Skin` 时打开 `GetSkinTransformPopup.Create(skin, callback)`。
15. `GetSkinTransformPopup` 关闭回调触发后，Popup 等待可能打开的 ShopPanel 关闭，再播放当前页完成变形；变形结束后释放交互锁并保留当前页，不自动切页。

## 已缓解风险

- 重复订阅：`BindButtons()` 开头先 `UnbindButtons()`，避免重复打开时多次回调。
- 页面挂点漏绑：`container` 为空时按子节点名 `Container` 查找，仍失败时回退到弹窗自身 transform，避免空引用阻断。
- 配置路径不一致：页面资源只读取 `prefabPath`，`themeName` 只作为标题多语言 key，避免把多语言字段误用为资源名。
- 锁定占位页复用：配置表中的 `Postcard_unlock` 统一按 `Postcard_PageItem` prefab 加载，`items` 为空时不绑定 item、不做解锁或整页完成检测。
- 标题绑定缺失：`title` 序列化字段为空时，优先复用 generated 字段 `UiMingxinpian01Txt`，再按 `title` 节点兜底查找 TMP 文本。
- Chest 绑定缺失：`chest` 序列化字段为空时，优先按 `Postcard_Chest` 子节点查找，再回退到当前弹窗子组件查找。
- 保存失败：写入 `CurrentId` 失败时回滚原值并记录 warning。
- 动画期间误操作：`OnPagePlayAnim(true)` 会禁用关闭和翻页，`Postcard_PageItem` 也会拒绝 item 点击。
- 生命周期清理：弹窗关闭时调用当前页 `OnExit()` 并销毁实例，解除 item 按钮订阅。
- 完成流程重入：`_isHandlingPageComplete` 会拦截重复完成回调，避免重复打开奖励弹窗或重复播放完成变形。
- 异步销毁取消：`pageCompleteStepDelay`、`chestOpenCompleteExtraDelay` 和奖励弹窗关闭等待都绑定 `GetCancellationTokenOnDestroy()`，Popup 销毁后不会继续后续表现。
- Skin 弹窗串联取消：Skin 转换弹窗等待也绑定 `GetCancellationTokenOnDestroy()`；Popup 被销毁时不会继续等待或播放后续表现。
- Skin 奖励解析边界：转换弹窗只在 `Rewards` 中存在 `GameProps.Skin` 且能通过 `BaseParamUtils.TryGetString(...)` 和 `TbSkin.GetOrDefault(...)` 解析到有效 `Config.Skin` 时打开，避免无效配置弹出空皮肤界面。
- Skin 发放职责隔离：Skin 所有权仍由 `CommonGetRewardPopup` 在普通奖励发放阶段调用 `Bag.GetNewSkin(...)`，`GetSkinTransformPopup` 只展示名称、图标和 ShopPanel 跳转，避免重复授予。
- Chest 隐藏恢复：完成领奖阶段显式 `Hide()` Chest；玩家后续手动切到未完成普通页或锁定占位页时 `RefreshChest(...)` 会先 `Show()` 再刷新，切到已完成普通页时保持隐藏。
- 代码阅读边界：完成页后的异步流程已集中到 `PostcardPopup_PageCompleteProcess.cs` partial，主 `PostcardPopup.cs` 不混入领奖等待逻辑；已删除完成后的自动切页私有方法。
- 末尾占位页边界：最后一张普通页未完成时不显示可点击 `NextBtn`；全部普通页完成后同样隐藏 `NextBtn` 并拦截 `ShowUnlockPage()`。尚有未完成普通页时的锁定占位提示路径保持不变。
- Chest 开箱动画缺失降级：整页完成时如果 `Postcard_Chest.PlayChestOpenAnim()` 没有拿到有效 `TrackEntry`，完成流程会走 Inspector 配置的短等待，不阻断领奖弹窗。
- Chest 开箱等待取消：开箱动画等待绑定 `PostcardPopup.GetCancellationTokenOnDestroy()`，Popup 被销毁时不会继续隐藏 Chest、打开奖励弹窗或播放后续表现。
- 表现时间可调：`pageCompleteStepDelay` 默认 0.5 秒、`chestOpenCompleteExtraDelay` 默认 1 秒，均暴露到 `PostcardPopup` Inspector，小于 0 时按 0 秒处理。

## 风险项

### P1-1 主题 prefab 绑定不完整可能导致后续页面不可解锁

- 问题：静态文本确认 `Postcard_ocean.prefab` 绑定了 `Postcard_PageItem` 和 `Postcard_ProgressItem`，`Postcard_unlock.prefab` 绑定了空 `items` 的 `Postcard_PageItem`；其他主题 prefab 未命中同类脚本。
- 影响：翻到后续主题时可能只能显示图片，无法点击解锁 item。
- 当前处理：运行时缺少 `Postcard_PageItem` 时拒绝切页并记录 warning，不临时补组件，避免掩盖 prefab 绑定问题。
- 后续验证：需要在 Unity Inspector 或 PlayMode 检查 `Postcard_forest`、`Postcard_food`、`Postcard_ems`、`Postcard_universe` 的绑定。

### P1-2 CurrentId 作为浏览页保存影响红点口径已局部缓解

- 问题：上一页/下一页进入实际配置页时会保存 `DB.Postcard.CurrentId`。
- 影响：如果玩家主动翻回已完成页后关闭，红点检查会以已完成页为当前页，可能暂时不提示后续未完成页。
- 当前处理：该行为按 `CurrentId 支持 lastBtn` 注释实现；锁定占位页不写入 `CurrentId`。2026-07-09 起，`PostcardRedDotChecker` 不再使用 `CurrentId` 作为红点目标配置，而是从 `TbPostcardConfig.DataList` 尾部向前查找最后一个未写入 `CompletedId` 的普通明信片配置，并跳过 `Postcard_unlock` 占位配置。
- 后续验证：需要 PlayMode 覆盖用户翻回已完成页关闭后，红点仍按最后一个未完成普通配置刷新；仍需确认 `CurrentCollectedPuzzledId` 与最后未完成配置在异常存档中的对应关系。

### P1-3 页面资源定位依赖 Resources 命名

- 问题：页面加载使用 `Resources.Load<Postcard_PageItem>(PrefabPath)`，依赖 `prefabPath` 和 Resources 路径稳定，并要求 prefab 已绑定 `Postcard_PageItem`。
- 影响：资源重命名、迁移到热更地址或缺失组件后，页面会拒绝切换并记录 warning。
- 当前处理：加载失败时记录配置 id、`themeName` 和 `prefabPath`，不临时补组件。
- 后续验证：资源打包和 YooAsset 路径接入如果变化，需要同步更新加载策略。

### P1-5 标题多语言配置未补齐可能导致标题为空或显示 key

- 问题：当前 `tbpostcardconfig.json` 中普通页 `themeName` 为空，unlock 行为 `???`，真实标题依赖后续 I2 词条和配置补齐。
- 影响：普通页标题会清空；多语言缺失时会回退显示 key 本身，可能出现 `???`。
- 当前处理：`ThemeName` 为空时主动清空标题，翻译结果为空时回退显示原 key，避免空引用或旧标题残留。
- 后续验证：需要在 Unity PlayMode 中确认每个主题配置的标题词条是否存在且语言切换后显示正确。

### P1-6 Chest 奖励和进度刷新依赖当前配置与存档口径

- 问题：Chest 进度使用 `UnlockProgress.Count` 作为最大值，使用 `CompletedId` 或 `CurrentCollectedPuzzledId` 计算当前值；如果存档中的当前收集列表和当前浏览页不一致，进度可能显示不符合预期。
- 影响：切换到未完成页时，宝箱进度可能显示为当前存档收集状态，而不是该页真实历史状态。
- 当前处理：页面切换时立即刷新；已完成普通页隐藏 Chest；未完成普通页按当前配置刷新；未解锁占位页显示空奖励和 `0/0`。
- 后续验证：需要 PlayMode 验证普通页切换、未解锁页返回、单个 item 解锁和整页完成后 Chest 显示。

### P1-7 Chest 奖励展示图标口径

- 问题：Chest 奖励预览必须与 `CommonGetRewardPopup` 使用同一奖励图标解析口径，不能从 `CommonRewardItem` 当前 `Image.sprite` 反向取图。
- 当前处理：`PostcardPopup` 将当前 `PostcardConfig.Rewards` 传给 Chest；Chest 对 Coin / Star 使用自身序列化 `coinSprite` / `starSprite`，对 Skin 使用 `ConfigManager.Tables.TbSkin` 与 `SkinUtils` 异步加载图标。
- 剩余风险：Skin 配置 id 无效、`TextureResources` 为空或资源不可加载时，Chest Skin 槽位会保持空图标；需要 PlayMode 验证真实配置。

### P1-8 完成流程结束后保留当前页仍缺少 PlayMode 验证

- 问题：完成链路依赖 `Postcard_PageItem` CompleteAnim 回调、`CommonGetRewardPopup.OnClose()` 回调和 `PostcardPopup` 的 UniTask 等待顺序；本次移除变形后的自动切页。
- 影响：预期结果是玩家停留在 Chest 已隐藏的完成页，并通过恢复交互后的 `NextBtn` 自主翻页；如果按钮状态没有在 `finally` 中正确刷新，可能无法继续浏览下一页。
- 当前处理：`HandlePageCompleteAsync()` 的 `finally` 统一释放 `_isHandlingPageComplete` / `IsPagePlayingAnim` 并调用 `RefreshButtonStates()`；手动翻页继续复用 `OnNextBtnClick()` 与 `ShowConfigPage(...)` 的保存失败保护。
- 后续验证：需要 PlayMode 覆盖完成后停留原页、按钮恢复、有下一页手动翻页、无下一页和 Popup 中途关闭路径。

### P1-10 Skin 转换弹窗串联仍缺少 PlayMode 验证

- 问题：本次未提交 diff 在奖励弹窗关闭后新增 `GetSkinTransformPopup` 等待链路，并给 `GetSkinTransformPopup.Create(Skin, Action)` 增加关闭回调；运行时实际顺序依赖 `CommonGetRewardPopup.OnClose()`、`GetSkinTransformPopup.OnClose()` 和 `PostcardPopup` 的 UniTask 取消。
- 影响：如果转换弹窗关闭回调未触发、goBtn 关闭后同时打开 ShopPanel、或 `Config.Skin.TextureResources` 资源缺失，可能导致 PostcardPopup 后续完成变形、交互恢复时序、UI 栈层级或皮肤展示异常。
- 当前处理：Skin 只在奖励配置能解析到有效 `Config.Skin` 时弹出；转换弹窗只展示皮肤名称和图标，不再次发放皮肤；关闭等待绑定 Popup 销毁 token；`GetSkinTransformPopup.OnClose()` 会取出并清空回调，降低重复触发风险。
- 后续验证：需要 PlayMode 覆盖包含 Skin 的明信片奖励、Skin-only 与 Coin/Star+Skin 组合、关闭按钮、goBtn 跳转 ShopPanel、外部销毁转换弹窗、无效 skin id、完成后停留原页和有下一页手动切换。

### P1-11 Chest 开箱动画等待仍缺少 PlayMode 验证

- 问题：整页完成入口现在依赖 `Postcard_Chest.PlayChestOpenAnim()` 返回的 `Spine.TrackEntry.Complete` 或 `End` 继续后续隐藏 Chest 与领奖流程。
- 影响：如果 Spine 绑定、动画名或 TrackEntry 回调异常，玩家可能看不到开箱动画；极端情况下可能停在完成流程等待阶段。
- 当前处理：`Postcard_Chest` 可从子级兜底解析 `UISpineCtrl`；缺失或播放失败时返回 `null` 并降级为 `pageCompleteStepDelay` 等待；有效 TrackEntry 完成后按 `chestOpenCompleteExtraDelay` 继续停留；等待绑定 Popup 销毁取消，并同时监听 `Complete` 与 `End`。
- 后续验证：需要 PlayMode 覆盖普通完成、缺失或禁用 Spine 兜底、关闭 Popup 中断等待、完成后停留原页和有下一页手动切页。

### P1-9 全部 page 完成后的末页收口仍缺少 PlayMode 验证

- 问题：末页 `NextBtn` 显隐和点击拦截依赖 `_postcardConfigs` 与 `DB.Postcard.CompletedId` 对全部普通 page 的一致判断。
- 影响：如果配置或存档读取失败，代码会保守判为未全部完成；异常存档可能继续保留末尾占位页入口，但不会把数据缺失误判为已全部完成。
- 当前处理：全部普通 page complete 时隐藏 `NextBtn`，并在 `OnNextBtnClick()` 再次拦截 `ShowUnlockPage()`；`Postcard_unlock` 不加入完成统计。
- 后续验证：需要 PlayMode 覆盖最后一张未完成、全部完成后按钮立即隐藏、关闭重开、异常存档“末页完成但前页未完成”，以及普通未完成页仍能进入 `Postcard_unlock`。

### P1-4 解锁记录顺序与注释文字顺序存在差异

- 问题：`Postcard_PageItem` 当前在播放 `PlayUnlockAnim` 前先写入明信片进度并保存，和注释中“动画后记录 Index”的文字顺序不完全一致。
- 影响：如果后续需求强依赖动画播放后才落库，当前实现会更早改变 `DB.Postcard`；但它避免了扣星后存档失败造成资源损失。
- 当前处理：保持先保存进度、再扣除 Star、再播放动画；保存失败时回滚进度并不扣星。
- 后续验证：需要确认产品口径优先保证注释顺序，还是优先保证失败时不扣错 Star。

## 验证证据

- 2026-07-07 `git diff --check -- Assets/Game/UI/Postcard/Scripts/PostcardPopup_PageCompleteProcess.cs Assets/Game/UI/GetSkinPopup/Scripts/GetSkinTransformPopup/GetSkinTransformPopup.cs`：通过，仅有工作区 LF/CRLF 转换提示。
- 2026-07-07 静态扫描：本次两个改动文件未新增编辑器生命周期、RemoteConfig、PlayerPrefs、StreamingAssets、广告、IAP、analytics、平台 SDK 或全局事件。
- 2026-07-07 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，97 warnings / 0 errors；warnings 属于当前工程既有输出范围，本次未逐项审计。
- 2026-07-07 `git diff --check -- Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs Assets/Game/UI/Postcard/Scripts/PostcardPopup功能说明.md Assets/Game/UI/Postcard/Scripts/PostcardPopup风险评估.md DevelopMD/已有功能设计与使用记录.md`：通过，仅有工作区 LF/CRLF 转换提示。
- 2026-07-07 静态扫描：本次 `PostcardPopup.cs` 改动未新增编辑器生命周期、RemoteConfig、广告、IAP、analytics、平台 SDK 或全局事件；命中项限于既有页面实例化、`SaveCurrentId(...)` 和页面刷新入口。
- 2026-07-07 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：未完成编译验证，当前 `Temp\bin\Debug` 缺失大量 Unity/插件元数据 DLL，首个错误为缺失 `Apple.Core.dll`，共 191 个 CS0006；未获得 `PostcardPopup.cs` 的业务代码编译诊断。
- 2026-07-06 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，97 warnings / 0 errors。
- 2026-07-06 `git diff --check -- Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs Assets/Game/UI/Postcard/Scripts/PostcardPopup功能说明.md Assets/Game/UI/Postcard/Scripts/PostcardPopup风险评估.md Assets/Game/UI/Postcard/Scripts/Postcard_PageItem功能说明.md Assets/Game/UI/Postcard/Scripts/Postcard_Chest功能说明.md Assets/Game/UI/Postcard/Scripts/Postcard_Chest风险评估.md DevelopMD/已有功能设计与使用记录.md`：通过，仅有工作区 LF/CRLF 转换提示；新增 `PostcardPopup_PageCompleteProcess.cs` 和 `.meta` 通过 `git diff --check --no-index -- NUL <file>` 检查，无行尾空白错误。
- 2026-07-06 静态扫描：未新增编辑器生命周期、RemoteConfig、广告、IAP、analytics、平台 SDK 或全局事件；只命中预期的 `CommonGetRewardPopup`、UniTask 等待、生命周期取消和既有 `SavePostcard`。
- 静态 prefab 证据：`PostcardPopup.prefab` 已绑定 `CloseBtn`、`LastBtn`、`NextBtn`，并存在 `Container` 子节点。
- 静态 prefab 证据：`PostcardPopup.prefab` 存在 `title` 节点，`UiMingxinpian01Txt` 当前绑定到标题 TMP 文本。
- 静态 prefab 证据：`PostcardPopup.prefab` 存在 `Postcard_Chest` 子节点，可作为 `chest` 序列化字段未绑定时的运行时兜底。
- 静态资源证据：Resources 下存在 `Postcard_unlock.prefab` 和 `Postcard_ocean.prefab`。
- 静态配置证据：`tbpostcardconfig.json` 存在 `id:30999`、`prefabPath:Postcard_unlock`、`unlockProgress:[]`、`rewards:[]`、`rewardMultiple:0` 的锁定页配置。
- 静态证据：`Postcard_ocean.prefab` 命中 `Postcard_PageItem` / `Postcard_ProgressItem` 脚本 GUID，`Postcard_unlock.prefab` 命中 `Postcard_PageItem` 且 `items` 为空，其余主题 prefab 绑定仍待补齐或确认。
- 2026-07-09 静态证据：`PostcardPopup_PageCompleteProcess.HandlePageCompleteAsync()` 在刷新 Chest 后调用 `Postcard_Chest.PlayChestOpenAnim()`，有效 TrackEntry 路径等待 `Complete` / `End` 和 `chestOpenCompleteExtraDelay` 后再隐藏 Chest；`pageCompleteStepDelay` 与 `chestOpenCompleteExtraDelay` 已暴露为 `PostcardPopup` Inspector 字段；`sp_box-open_002.json` 存在 `box-open`、`box-open_idle`、`box_idle` 动画名；`PostcardPopup.prefab` 中 Chest 子级存在 `SkeletonGraphic`、`UISpineCtrl` 和 `sp_box-open_002_SkeletonData` 绑定。
- 2026-07-09 `git diff --check -- Assets/Game/UI/Postcard/Scripts/Postcard_Chest.cs Assets/Game/UI/Postcard/Scripts/PostcardPopup_PageCompleteProcess.cs`：通过，仅有工作区 LF/CRLF 转换提示；静态敏感扫描未命中编辑器生命周期、每帧轮询、全局事件、RemoteConfig、广告、IAP、analytics 或 PlayerPrefs。
- 2026-07-09 Chest 奖励图标口径修正后，`BuildChestRewards(...)` 改为转交完整 `PostcardConfig.Rewards`，由 Chest 按 `CommonGetRewardPopup` 口径解析 Coin / Star / Skin；目标文件 `git diff --check` 通过，`dotnet build` 通过，0 warnings / 0 errors。
- 2026-07-09 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：未通过，首个错误为缺失 `Temp\bin\Debug\Apple.Core.dll`，共 191 个 `CS0006` 元数据缺失错误，未获得本次业务脚本编译诊断。
- 2026-07-09 追加 Inspector 等待参数后重跑 `git diff --check`：通过，仅有 LF/CRLF 转换提示；重跑 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，99 warnings / 0 errors。
- 2026-07-09 `PostcardRedDotChecker` 红点口径调整：`git diff --check -- Assets/Game/UI/Global/Reddot/PostcardRedDotChecker.cs Assets/Game/UI/Global/Reddot/RedDot核心结构说明.md Assets/Game/UI/Postcard/Scripts/PostcardPopup风险评估.md DevelopMD/已有功能设计与使用记录.md` 通过；静态敏感扫描未新增编辑器生命周期、每帧轮询、全局事件、资源加载、广告、IAP、analytics、PlayerPrefs 或写存档路径；`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，99 warnings / 0 errors。

## 未覆盖验证

- Unity Editor PlayMode：打开弹窗、初始化第一页、点击上一页/下一页、锁定页返回、完成页保留原页后手动进入下一页。
- Unity Editor PlayMode：整页完成后先播放 Chest `box-open`，切到 `box-open_idle` 后按 Inspector 配置额外停留，再隐藏 Chest 并进入 `CommonGetRewardPopup`。
- Unity 编译验证：虽然 `dotnet build .\Assembly-CSharp.csproj --no-restore ...` 已通过，仍需 Unity Editor 侧重新导入/PlayMode 验证序列化字段默认值和 Inspector 覆盖值。
- Unity Editor PlayMode：包含 `GameProps.Skin` 奖励的完成页在 `CommonGetRewardPopup` 关闭后打开 `GetSkinTransformPopup`，转换弹窗关闭并完成 transform 后保留当前页。
- Unity Editor PlayMode：转换弹窗关闭按钮和 goBtn 跳转 ShopPanel 后，PostcardPopup 的等待链路不会卡住或重复播放完成表现。
- Unity Editor PlayMode：最后一张普通明信片未完成时 `NextBtn` 隐藏，完成后 `NextBtn` 显示并可进入 `Postcard_unlock`，锁定页 `LastBtn` 可返回最后一张普通明信片。
- Unity Editor PlayMode：切换不同 config page 后 Chest 奖励槽、进度文本和进度条立即刷新。
- Unity Editor PlayMode：解锁单个 item 和整页完成后 Chest 进度刷新到最新值。
- Unity Editor PlayMode：解锁 item 动画期间关闭、上一页、下一页和 item 重复点击均不可用。
- Android/iOS 横屏 smoke：触摸按钮、页面位置、遮罩和 UI 栈恢复。
- Android/iOS 横屏 smoke：Skin 转换弹窗图标、文本、关闭、ShopPanel 跳转和返回后的 UI 栈表现。
- 主题 prefab 完整绑定：后续四个普通主题是否具备 `Postcard_PageItem` / `Postcard_ProgressItem`。
- 标题多语言：每个普通页和未解锁占位页的 `ThemeName` 是否能命中 I2 词条，语言切换后是否刷新为预期文本。

## 回滚路径

- 回退 `PostcardPopup.cs` 中页面配置读取、实例化、翻页、锁定页、`OnPagePlayAnim` 和 `OnPageComplete` 实现；如只回退本次阅读性拆分，则删除 `PostcardPopup_PageCompleteProcess.cs`、移除 `Assembly-CSharp.csproj` 引用，并把完成流程私有方法搬回 `PostcardPopup.cs`。
- 如只回退本次 Skin 转换弹窗串联，删除 `PostcardPopup_PageCompleteProcess.cs` 中 `WaitSkinTransformPopupCloseAsync(...)`、`ResolveSkinReward(...)` 及其调用点，并把 `GetSkinTransformPopup.Create(Skin, Action)` 恢复为无关闭回调参数。
- 如只回退本次 Chest 开箱动画等待，删除 `PostcardPopup_PageCompleteProcess.cs` 中 `WaitChestOpenAnimAsync(...)` 及 `PlayChestOpenAnim()` 调用点，恢复为刷新 Chest 后按 `pageCompleteStepDelay` 等待并隐藏 Chest；若只回退 Inspector 暴露参数，则把对应字段恢复为局部常量并移除序列化字段。
- 保留 `Postcard_PageItem` 现有解锁逻辑不会破坏旧关闭按钮行为。

## 2026-07-09 item 解锁粒子风险补充

### 变更入口与原始基线

- 入口：`Postcard_PageItem.TryUnlockItem(...)` 在解锁保存成功、扣星后调用 `_owner.PlayUnlockEffectsAsync(...).Forget()`。
- 播放方：`PostcardPopup.PlayUnlockEffectsAsync(Vector3)` 延迟 0.5 秒后，只移动并播放 `collectEffects` 中已绑定的 `ParticleSystem`；延迟等待随 Popup 销毁取消。
- 原始基线：`PostcardPopup` 中已有空的 `PlayCollectEffect(Vector3)` 和 `collectEffects` 字段；单个 item 解锁成功后只播放 `Postcard_ProgressItem.PlayUnlockAnim(...)`。

### 风险项

#### P2-4 粒子未绑定时新增表现不可见

- 风险：静态 YAML 检查未在 `PostcardPopup.prefab` 中发现 `collectEffects` 或 ParticleSystem 绑定，若 Unity Inspector 中也未绑定，运行时不会播放任何新粒子。
- 当前处理：代码对空列表和空项直接跳过；延迟期间 Popup 销毁会取消等待，不阻断保存、扣星、item 动画、guide 推进或完成奖励流程。
- 待验证：Unity Inspector 绑定粒子后，在普通解锁和 guide 解锁路径确认粒子可见。

#### P2-5 Canvas / 粒子层级导致播放位置偏移

- 风险：目标坐标来自真实 `Postcard_ProgressItem` 的 `RectTransform` 中心世界坐标；如果粒子父节点使用不同 Canvas、相机或非 UI 空间，视觉位置可能偏移。
- 当前处理：不做额外坐标系统抽象，只按当前真实 item 中心移动粒子，保持实现局部且可回滚。
- 待验证：Unity PlayMode 多分辨率、Android/iOS 横屏设备上确认粒子对齐。

### 验证证据

- `git diff --check -- Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs`：通过，仅有工作区 LF/CRLF 转换提示。
- 静态敏感扫描未新增编辑器生命周期、每帧轮询、全局事件、RemoteConfig、广告、IAP、analytics 或 PlayerPrefs；命中仅为既有 `Instantiate(...)` 和 Animator `Update(0f)`。
- `rg -n "!u!198|ParticleSystem|m_PlayOnAwake|collectEffects" Assets\Game\UI\Postcard\Resources\PostcardPopup.prefab`：无命中，粒子绑定仍待 Unity 侧确认。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 未通过，首个错误为缺失 `Temp\bin\Debug\Apple.Core.dll`，共 191 个 `CS0006`，未获得本次业务脚本编译诊断。

### 回滚路径

- 删除 `Postcard_PageItem.TryUnlockItem(...)` 中 `_owner.PlayUnlockEffectsAsync(...).Forget()` 调用。
- 删除或留空 `PostcardPopup.collectEffects` 与 `PlayUnlockEffectsAsync(...)`，不会影响已有解锁存档和奖励流程。

## 2026-07-09 Sort 排序与 Diary 正文风险补充

### 变更入口与原始基线

- 入口：`PostcardPopup.TryLoadPostcardConfigs()` 在弹窗开启时读取 `TbPostcardConfig.DataList`，现在对普通明信片配置执行 `_postcardConfigs.Sort(ComparePostcardConfigSort)`。
- 排序字段：`Config.PostcardConfig.Sort` 升序；`Sort` 相同时按 `Id` 做稳定兜底，不再依赖配置表原始行顺序。
- 正文入口：`Postcard_PageItem.GetCompleteContentText(...)` 现在优先使用当前页 `config.Diary` 作为 I2 多语言 key。
- 原始基线：普通明信片页缓存直接按 `DataList` 顺序追加；完成态正文直接缓存 `contentTxt.text` 初始值。

### 已缓解风险

- 配置行顺序变动风险：显示顺序改由 `Sort` 控制，配置表增删或调整行位置时不会直接改变弹窗逻辑顺序。
- 相同 `Sort` 风险：使用 `Id` 作为稳定兜底比较，避免同分值配置在不同运行环境中因原始列表顺序不同产生不可预测结果。
- 多语言正文缺失风险：`Diary` 为空时保留 prefab 初始文本兜底；翻译返回空时显示 key，便于暴露缺失词条。
- 串页缓存风险：`Postcard_PageItem.SetData(...)` 已在每次页面绑定时清空 `_completeContentText`，当前页正文不会复用上一页 Diary。

### 风险项

#### P2-6 未完成配置资源进入排序缓存

- 风险：当前静态配置显示 `30007`、`30008` 有 `Sort`、`Diary`、进度和奖励，但 `prefabPath` 为空；按 `Sort` 排序后它们会进入普通页缓存并位于中间顺序。
- 影响：玩家翻到这些配置时，`LoadPostcardPagePrefab(...)` 会返回 null，页面切换被拒绝并记录 warning。
- 当前处理：沿用既有缺失 prefab 保护，不临时跳过配置、不自动补页面，避免隐藏配置与资源未对齐的问题。
- 待验证：补齐资源后在 PlayMode 验证 `Sort` 顺序、上一页/下一页、完成后停留原页和 Chest 刷新。

#### P2-7 Diary 多语言词条缺失

- 风险：如果 `PostcardConfig.Diary` 对应 I2 词条缺失，完成态正文会显示 key。
- 影响：玩家可能看到 `U_Diary_Content_xxx` 形式的占位文本。
- 当前处理：回退显示 key 而不是空白，方便 QA 暴露配置缺口；`Diary` 为空时仍使用 prefab 初始文本兜底。
- 待验证：Unity PlayMode 下切换语言并确认每张普通明信片完成态正文。

### 验证证据

- 2026-07-09 静态检查确认 `PostcardConfig` 生成类包含 `Sort` / `Diary` 字段，`tbpostcardconfig.json` 当前普通配置已有对应字段。
- 2026-07-09 `git diff --check -- Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs`：通过，仅有 LF/CRLF 转换提示。
- 2026-07-09 敏感关键字扫描未发现新增编辑器生命周期、每帧轮询、全局事件、RemoteConfig、广告、IAP、analytics 或 PlayerPrefs；唯一命中为既有 Animator `Update(0f)`。
- 2026-07-09 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，99 warnings / 0 errors。

### 回滚路径

- 排序回滚：删除 `ComparePostcardConfigSort(...)`，恢复普通配置按 `DataList` 原始顺序追加，并将锁定页选择恢复为 `_unlockConfig ??= postcardConfig`。
- Diary 回滚：恢复 `GetCompleteContentText(...)` 读取 `contentTxt.text` 初始值，并删除 `ResolveLocalizedCompleteContentText(...)` 与 `I2.Loc` using。

## 2026-07-09 complete page 隐藏 Chest 风险补充

### 变更入口与原始基线

- 入口：`PostcardPopup.RefreshChest(Config.PostcardConfig postcardConfig, bool isImmediate)`。
- 原始基线：该方法解析到 `Postcard_Chest` 后无条件 `Show()`，所以已完成页在重新打开、切回或刷新时仍可能显示 Chest。
- 当前变更：`RefreshChest(...)` 先调用已有 `IsConfigCompleted(postcardConfig)`；已完成页直接 `Hide()` 并返回，未完成普通页和锁定占位页保持原刷新路径。

### 已缓解风险

- complete page 状态不一致：完成流程已经隐藏 Chest，浏览已完成页也会隐藏，避免同一页面在不同入口下 Chest 显示口径不同。
- 奖励预览误导：已完成页不再显示可点击 Chest 和奖励 bubble，降低玩家误以为还能继续领奖的风险。
- 回归范围控制：不改 `Postcard_Chest` 组件、不改 prefab、不改奖励发放和完成流程，只在 Popup 刷新入口增加一个本地判断。

### 风险项

#### P2-8 锁定占位页仍会显示 Chest 空状态

- 风险：`Postcard_unlock` 这类占位配置不会被 `IsConfigCompleted(...)` 判为已完成，因此仍沿用显示 Chest、空奖励和 `0/0` 的旧行为。
- 当前处理：本次需求只明确 complete page 隐藏 Chest，未改变锁定页表现，避免扩大 UI 回归范围。
- 待验证：如果策划希望锁定页也隐藏 Chest，需要另行确认并单独调整。

#### P2-9 完成瞬间与已完成页浏览的 Chest 时序不同

- 风险：刚完成当前页时仍需要先显示 Chest 并播放开箱动画，然后隐藏；已完成页重开时会直接隐藏，不再播放开箱。
- 当前处理：完成瞬间逻辑仍在 `PostcardPopup_PageCompleteProcess.HandlePageCompleteAsync()` 中控制；本次只影响 `RefreshChest(...)` 的普通刷新入口。
- 待验证：Unity PlayMode 覆盖首次完成、完成后停留原页、手动切到下一页、关闭重开已完成页、从下一页切回已完成页。

### 验证证据

- 2026-07-09 `git diff --check -- Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs Assets/Game/UI/Postcard/Scripts/PostcardPopup功能说明.md Assets/Game/UI/Postcard/Scripts/PostcardPopup风险评估.md Assets/Game/UI/Postcard/Scripts/Postcard_Chest功能说明.md Assets/Game/UI/Postcard/Scripts/Postcard_Chest风险评估.md DevelopMD/已有功能设计与使用记录.md`：通过，仅有 LF/CRLF 转换提示。
- 2026-07-09 本次 `PostcardPopup.cs` diff 敏感关键字扫描未命中 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、RemoteConfig、PlayerPrefs、PlayEvents、DispatchEvent、analytics、广告、IAP、实例化或写存档路径。
- 2026-07-09 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，98 warnings / 0 errors。

### 未验证缺口

- Unity PlayMode：complete page 重开或切回后 Chest 是否保持隐藏。
- Unity PlayMode：未完成普通页和锁定占位页 Chest 是否仍按旧逻辑显示并刷新。
- Unity PlayMode：首次整页完成时 Chest 开箱、隐藏、领奖、完成变形、停留原页与手动翻页链路是否正常。
- Android/iOS 横屏 smoke：complete page 页面布局在 Chest 隐藏后是否符合美术预期。

### 回滚路径

删除 `RefreshChest(...)` 中完成态隐藏分支，恢复原有所有配置页刷新时 `Show()` Chest 的行为。

## 2026-07-10 Chest 首次完成开箱软锁风险复核

### 事实与直接结论

- `Postcard_PageItem` 在 `_onCompleted` 前已写入并保存 `CompletedId`；修复前 `HandlePageCompleteAsync()` 随后调用普通 `RefreshCurrentChest(false)`，命中完成态隐藏分支后把 Chest 根节点设为 inactive。
- `UISpineCtrl.PlayAnimation(...)` 对已初始化但 inactive 的 `SkeletonGraphic` 仍可能返回非空 `TrackEntry`；inactive Spine 不再逐帧推进，而旧等待没有超时，因此 `IsPagePlayingAnim`、奖励、transform 和切页会一直停住。
- prefab、Spine 动画名和 Complete 页面 Animator 的静态证据正常；本次根因是首次完成与历史完成浏览共用了同一 Chest 刷新规则，不是资源缺失或页面 Animator 直接覆盖 Chest。

### 风险等级与缓解

- **P1 已缓解：首次完成奖励 UI 软锁。** 首次完成现在使用 `PrepareChestForPageComplete(...)` 强制显示 Chest 并刷新 `max/max`，不再触发历史完成页隐藏规则。
- **P1 已缓解：非空 TrackEntry 永久不回调。** 正常路径仍等待 `Complete` / `End`；超过 `max(3 秒, 动画时长 + 1 秒)` 后记录 Bear warning 并继续流程。
- **P1 已缓解：inactive 调用返回伪有效 TrackEntry。** `PlayChestOpenAnim()` 在 `gameObject.activeInHierarchy == false` 时返回 `null`，由 Popup 走短等待降级。
- **回归边界保持：** 历史完成页仍隐藏 Chest；DB/save、奖励发放、Guide、弹窗顺序、资源路径和平台链路未改。

### 验证证据与缺口

- 2026-07-10 `Assembly-CSharp.csproj` 编译通过，98 warnings / 0 errors；目标代码 `git diff --check` 通过，仅有 LF/CRLF 提示；敏感扫描没有新增编辑器生命周期、每帧轮询、全局事件或平台业务链路。
- 未验证：Unity PlayMode 首次完成的 `box-open -> box-open_idle`、超时 warning 降级、奖励弹窗、Skin/Shop、transform、停留原页、手动翻页和 `IsPagePlayingAnim` 最终释放。
- 未验证：Guide 最后一项、Popup 等待中销毁、Chest/Spine 缺失、历史完成页重开/切回、末页、Android/iOS 横屏。

### 回滚路径

恢复 `HandlePageCompleteAsync()` 对 `RefreshCurrentChest(false)` 的调用，删除 `PrepareChestForPageComplete(...)`、开箱等待超时和 inactive 检查，即可回到修复前实现；存档与资源没有迁移项。回滚会重新暴露本节记录的软锁风险。

## 2026-07-13 PageGray 完成粒子风险补充

### 事实与直接结论

- `Postcard_PageItem.ApplyGrayImageState(...)` 的首次完成 Tween 会把 `PostcardGrayImage.FillAmount` 驱动到 1；新增粒子调用位于该 Tween 后的同步回调内。
- `PostcardPopup.PlayCompleteEffects()` 只遍历 `completeEffects` 并调用 `ParticleSystem.Play(true)`，不等待粒子结束，不参与 DB/save、奖励发放、Chest、Guide 或完成后的页面导航判定。
- `_lastCompletedState` 仍限制该 Tween 只在页面从未完成转为完成时播放；历史完成页刷新不会触发完成粒子。
- `PostcardPopup.prefab` 当前静态 YAML 中存在两个非空粒子引用；实际渲染顺序、Canvas/粒子适配与美术效果仍需 PlayMode 证据。

### 风险项与缓解

- **P2：粒子引用失效或包含空项。** 播放方法对列表和单项均做空检查，失效时只缺少表现，不阻断完成主流程。
- **P2：粒子表现与完成页停留重叠。** 本需求明确不等待粒子；现有 Chest、领奖和变形流程保持并行，最终停留当前页时的视觉节奏需在 PlayMode 验证。
- **P2：移动端瞬时粒子开销。** 本次复用 prefab 中现有两个粒子，不新增运行时实例化；但 overdraw、批次和低端设备帧耗尚未测量。

### 验证证据与缺口

- 2026-07-13 C# 项目构建通过，98 warnings / 0 errors。
- 静态检查未发现新增 `Update`、编辑器生命周期、全局事件、资源加载、存档或平台 SDK 路径。
- 未验证：Unity PlayMode 的触发帧、两个粒子实际播放、重复完成保护、快速关闭/切页和粒子层级。
- 未验证：Android/iOS 横屏真机视觉与性能。

### 回滚路径

移除 `Postcard_PageItem` 中的完成粒子调用和 `PostcardPopup.PlayCompleteEffects()`；保留或清空 prefab 的 `completeEffects` 列表均不会影响存档与完成主流程。

## 2026-07-13 全部 page complete 后隐藏 unlockPage 风险补充

### 事实与直接结论

- `_postcardConfigs` 在加载配置时排除了 `IsUnlockConfig(...)` 命中的 `Postcard_unlock`，因此 `AreAllPostcardPagesCompleted()` 只遍历普通 page。
- 全部普通 page id 都存在于 `DB.Postcard.CompletedId` 时，`RefreshButtonStates()` 不再显示末页 `NextBtn`；即使点击回调被其他方式触发，`OnNextBtnClick()` 也不会调用 `ShowUnlockPage()`。
- 尚有后续普通 page 时的正常翻页、未完成非末页的锁定占位提示、`CurrentId` 保存、Chest、完成奖励、Guide、资源加载和事件链均未修改。

### 风险、验证缺口与回滚

- **P2：配置/存档不完整时保守保留旧入口。** `TryGetPostcardData(...)` 失败、普通配置列表为空、存在空配置或任一普通 id 未写入 `CompletedId` 时，全部完成判断返回 false；该降级避免错误隐藏入口，但异常数据下仍可能看到末尾占位页。
- **P2：完成后的按钮刷新时序。** `HandlePageCompleteAsync()` 的 `finally` 会调用 `RefreshButtonStates()`，静态链路满足立即收口条件；实际动画、弹窗关闭与按钮显隐仍需 Unity PlayMode 验证。
- `git diff --check` 通过；C# 项目构建通过，98 warnings / 0 errors；静态扫描未发现本次新增编辑器生命周期、每帧轮询、全局事件、存档写入或平台链路。
- Unity PlayMode、异常存档矩阵、Android/iOS 横屏触摸与布局仍未验证。
- 回滚时移除全部完成辅助判断，恢复末页完成后的旧 `NextBtn -> ShowUnlockPage()` 条件；无需迁移 DB、配置或资源。

## 2026-07-13 `_isGuiding` 按钮交互锁风险补充

### 事实

- 修改前 `PostcardPopup.RefreshButtonStates()` 的按钮交互条件只有 `!IsPagePlayingAnim`，关闭、上一页、下一页点击入口也没有检查 `_isGuiding`。
- Page complete 引导收尾原先会依次打开第三阶段、Next GuideFinger 和第四阶段，但第四阶段没有关闭回调，且回调尾部仍把 `_isGuiding` 设为 true。
- `Postcard_GuideFinger.Create(...)` 在目标或 Resources prefab 缺失时返回 null；领奖流程在 `_isGuiding` 为 true 时忽略普通关闭回调，因此 GuideFinger 创建失败会缺少原等待完成信号。

### 直接结论与已缓解风险

- **P1 已缓解：引导期间玩家按钮打断主链。** Close、Last、Next 的 `Interactable` 和点击入口都受 `_isGuiding` 限制；引导自身通过局部 `TryGoNextPage(true)` 推进，不开放玩家输入。
- **P1 已缓解：第四阶段结束后按钮永久锁定。** 第四阶段关闭回调统一执行 `CompletePostcardGuiding()`，清除本地标记并刷新按钮；`OnClose()` 也清理该内存态。
- **P1 已缓解：引导目标或资源失败后无恢复入口。** 初始 GuidePart 启动失败时不持锁；第三/第四阶段弹窗、领奖 Finger 和 Next Finger 创建失败时记录 warning 并降级结束本地引导锁。
- **P2：引导期间页面状态变化。** 内部 Next 仍复用既有完成态、下一配置、全部完成和 `CurrentId` 保存判断，没有新增绕过 DB/config 业务条件的翻页实现。

### 假设

- 正常配置下第三阶段关闭时 `NextBtn` 仍处于 active hierarchy，GuideFinger 可以定位并接收点击；该判断只有静态调用链证据，尚未以实际 prefab 阶段串联确认。
- 第四阶段 `PostcardGuidePopup` 的现有按钮/无效配置降级能最终进入 `OnClose()`；脚本具备一次性关闭回调，但实际 variant 和 UI 层级仍需 PlayMode 验证。

### 缺失证据与待验证项

- Unity PlayMode：进入 `_isGuiding` 后 Close/Last/Next 均不可点，真实 ProgressItem 引导仍能推进，领奖 GuideFinger 能正常发奖并解除等待。
- Unity PlayMode：第三阶段关闭、Next GuideFinger 点击、内部翻页、第四阶段关闭后按钮按新页面状态恢复。
- Unity PlayMode：GuidePart、GuideFinger、第三/第四阶段弹窗和奖励按钮目标分别缺失时，warning 与降级入口符合预期且不会重复发奖或残留遮罩。
- Unity PlayMode：Popup 被外部销毁、UI 缓存复用、快速重复关闭时不存在旧回调修改新实例状态。
- Android/iOS 横屏真机：禁用按钮、GuideFinger root 定位、射线层级与恢复后的首次点击尚未验证。

### 验证证据与回滚

- C# 项目构建通过，98 warnings / 0 errors；目标代码 `git diff --check` 通过，仅有 LF/CRLF 转换提示。
- 静态改动没有新增编辑器生命周期、每帧轮询、DB 字段、资源地址、全局事件、广告、IAP、analytics 或平台 SDK 链路。
- 回滚时移除 `_isGuiding` 的按钮条件与点击防护，恢复旧的 page-complete 引导内联回调，并移除 `CompletePostcardGuiding()` 和创建失败降级；无需存档或资源迁移。回滚会重新暴露本节 P1 风险。

## 2026-07-13 Stage Two 与 GuidePart 时序风险补充

### 事实与直接结论

- 修改前 `StartGuding()` 在 Stage One 创建前写入 `IsGuiding=true`；Finger 随后先打开 PostcardPopup，Popup 会在自身 `OnOpen()` 页面加载阶段立即启动 GuidePart，再打开 Stage Two。
- 修改后 Stage Two 关闭回调是 `StartGuding()` 与 `StartPendingPostcardGuide()` 的唯一当前入口，因此正常新流程下 GuidePart 不会与 Stage Two 同时显示。
- Stage Two 创建返回 null 时直接降级启动 GuidePart；Popup 或 PostcardManager 为 null 时只记录 warning，不留下本次新写入的 pending 状态。

### 风险与缺失证据

- **P1：历史 `IsGuiding=true` 存档可能提前触发。** PostcardPopup 仍保留打开时自动消费 pending guide 的兼容行为；异常中断或测试残留的旧 pending 状态可能在 Stage Two 前启动，需用干净存档和中断恢复场景验证。
- **P1：Stage Two 关闭时 Popup 已销毁。** 回调会检测 Unity 对象有效性并停止，不写 `HasGuide/IsGuiding`；玩家下次仍满足首次引导门槛，但实际重试行为待 PlayMode 验证。
- **P2：Stage Two 创建失败降级。** 失败时不等待不存在的回调，直接启动 GuidePart；warning、UI 层级和输入恢复尚未做故障注入验证。
- **P2：保存失败。** `StartGuding()` 当前仍沿用原 `DB.Postcard.Save()` 且没有把结果返回调用方；内存态可启动 GuidePart，但重启后的首次引导标记一致性未验证。
- C# 构建通过，98 warnings / 0 errors；Unity PlayMode、Android/iOS 横屏触摸和异常存档恢复仍未验证。
- 回滚时把 `StartGuding()` 恢复到 Stage One 前，删除 `StartPendingPostcardGuide()`，并恢复 Stage Two 无关闭回调调用；无需 DB schema、配置或资源迁移。

## 2026-07-14 Stage Two 与 GuidePart 重叠风险修正

### 事实与直接结论

- 代码复核确认，`GameVictoryPanel.TryToPostcardGuide()` 在 Stage One 前残留 `StartGuding()` 调用，导致 `IsGuiding=true` 早于 `PostcardPopup` 页面加载；Popup 因此在 Stage Two 关闭回调前自动启动 GuidePart。
- 当前已移除 `ShowPageItem()` 的自动启动入口，并把 `StartGuding()` 移入 Stage Two 关闭回调，保持它位于 `StartPendingPostcardGuide()` 之前；正常调用顺序变为 Stage Two 关闭 → 写入 pending 状态 → 启动并消费 GuidePart。
- 本次只改变首次明信片引导的 UI 时序，不改变存档字段、页面解锁、Star 消耗、奖励、资源或事件链。

### 风险、验证缺口与回滚

- **P1：回调目标失效。** Stage Two 关闭时若 Popup 或 PostcardManager 已失效，回调会停止且不写入 `HasGuide/IsGuiding`；玩家是否能从完整入口重试仍需 PlayMode 验证。
- **P2：Stage Two 创建失败降级。** 创建失败时仍同步执行同一回调，GuidePart 会直接启动；该故障路径尚未做运行态注入验证。
- **缺失证据：** 尚未完成 Unity PlayMode、Android/iOS 横屏触摸和 UI 层级验证。
- 已完成静态调用顺序、目标 diff 与 C# 编译检查。回滚时恢复 `ShowPageItem()` 自动启动并把 `StartGuding()` 移回 Stage One 前，但会重新引入重叠显示。

## 2026-07-14 Complete 页刷新回调时序风险补充

### 事实与直接结论

- 首次完成页的 `Refresh()` 已从 `Postcard_PageItem` 解锁回调移到 `CommonGetRewardPopup` 关闭回调；关闭回调先刷新当前页，再完成 `closeSource`，因此后续 Skin/ShopPanel/transform 不会早于刷新继续。
- 引导领奖路径已移除按钮点击后的提前 `closeSource.TrySetResult()`，与普通领奖统一等待 Popup 的 `OnClose()` 回调。
- 无奖励或 Popup 创建失败时仍执行一次降级刷新，不改变奖励发放、DB/save、资源或事件职责。

### 风险、验证缺口与回滚

- **P1：领奖 Popup 未正常关闭。** 页面完成态刷新与后续流程会继续保持等待；该行为符合回调时序要求，但异常销毁路径需要 PlayMode 验证。
- **P2：刷新视觉与后续 Skin 弹窗重叠。** `Refresh()` 启动的灰度/文字 Tween 不被等待，可能在 Skin 弹窗显示期间继续播放；是否需要等待视觉结束尚无需求证据。
- C# 构建通过（98 warnings / 0 errors），静态调用顺序和 diff 检查通过；普通领奖、引导领奖、无奖励降级及 Android/iOS 横屏尚未运行验证。
- 回滚路径为恢复 PageItem 完成分支先刷新再触发 `_onCompleted`，并移除 Popup 侧 `RefreshCompletedPage()`。

## 2026-07-14 PageComplete 前移到 Chest 前的时序风险修正

### 事实

- `HandlePageCompleteAsync()` 当前固定顺序为：`RefreshCompletedPage()` → `PlayCompleteTransformAsync()` → `PrepareChestForPageComplete()` → `PlayChestOpenAnim()` → 领奖/Skin/Shop。
- `CommonGetRewardPopup` 关闭回调只完成 `closeSource`，不再重复刷新页面；完成判定、`CompletedId`、Star 扣除和奖励发放入口未改。
- 影响代码仅限 `PostcardPopup_PageCompleteProcess.cs` 的编排和 `Postcard_PageItem.cs` 的事实性注释；未修改 prefab、Animator、Spine、配置、存档结构、资源地址或事件。

### 直接结论

- 页面完成视觉与 transform 已在 Chest 开启调用前完成等待，满足本次时序要求。
- 页面先 `Refresh()` 再执行 transform，避免 transform 前移但领奖关闭后再次刷新导致 `isComplete` 被重新写回过渡态。

### 假设与风险

- **P1：视觉衔接。** 1.5 秒固定 transform 等待是否覆盖所有主题页实际动画长度尚未由 PlayMode 证实；若动画更长，Chest 可能提前出现。
- **P2：Chest 节奏。** Chest 满进度刷新现在发生在页面 transform 之后，实际观感是否需要额外停顿仍待产品目视确认。
- **P2：取消路径。** 页面 transform 期间关闭或销毁 Popup 会取消后续 Chest 与领奖流程；与既有销毁取消策略一致，但快速关闭尚未运行验证。

### 缺失证据、验证与回滚

- C# 构建通过（99 warnings / 0 errors）；静态调用顺序和目标 diff 检查通过。
- 缺失 Unity PlayMode、不同主题 Animator/Spine 时长、普通/引导领奖、无奖励、Skin/Shop、快速关闭及 Android/iOS 横屏证据。
- 回滚：把页面刷新与 transform 恢复到奖励/Skin/Shop 之后，并恢复领奖关闭回调刷新；无需迁移 DB、配置或资源。

## 2026-07-14 PageComplete 内部 Tween 与粒子时序风险补充

### 事实

- 正文 `DOText` 已从 `Refresh()` 的 `ApplyCompleteVisualState(...)` 移到 `PlayCompleteTransformAsync()`，只在有效 `transform` trigger 发出后启动。
- transform 固定等待为 1.5 秒；正文为 1 秒延迟加 0.7 秒 Tween，完成流程实际按两者较大值等待 1.7 秒。
- 完成粒子调用位于 grayImage Sequence 的 `OnComplete`；Sequence 包含 0.7 秒填充和两次 0.2 秒 Yoyo 回弹，Kill 路径不触发 `OnComplete`。

### 直接结论

- 正文逐字播放属于页面转换过程，且 Chest 开启不会早于正文 Tween 的计划结束时间。
- 完成粒子不会在 grayImage 填充或回弹期间提前发射；只有完整 Sequence 自然完成后才进入既有 0.1 秒粒子延迟。

### 假设、风险与缺失证据

- **P1：动画资产时长。** 代码仍以 1.5 秒代表 transform 窗口；不同主题实际 Animator clip 若长于 1.7 秒，Chest 仍可能早于真实动画结束，尚待 PlayMode 抽样。
- **P2：长文案观感。** `DOText` 固定 0.7 秒，不随字符数变化；长语言文本可能过快，但本次未改变原文字时长。
- **P2：打断路径。** grayImage Sequence 被刷新、禁用或销毁时不会发射完成粒子；这是避免过期粒子的预期降级，实际快速关闭体验未验证。
- C# 构建通过（99 warnings / 0 errors），静态顺序与 diff 检查通过；Unity PlayMode、Android/iOS 横屏和低端设备粒子开销仍待验证。
- 回滚：恢复 Refresh 内正文 Tween、1.5 秒固定等待和 grayImage Sequence 前粒子调用；无需迁移 DB、配置、prefab 或资源。

## 2026-07-14 transform 等待粒子触发的风险补充

### 事实

- grayImage Sequence 自然完成后启动 `PlayCompleteEffectsAndSignalAsync(...)`，等待 Popup 的既有 0.1 秒延迟及全部 `ParticleSystem.Play(true)` 调用完成后释放局部完成信号。
- `PlayCompleteTransformAsync()` 在 Animator 状态定位与 `transform` trigger 之前等待该信号；信号只存在于当前 PageItem，不新增全局事件或存档状态。
- 粒子系统的完整生命周期未被等待，transform 的保证边界是“粒子已开始播放”。

### 直接结论

- 正常首次完成路径固定为 grayImage 完成 → 粒子 Play 调用 → transform → 文案 → Chest，transform 不再与 grayImage/粒子触发抢跑。
- 页面销毁会取消等待；grayImage 被 Kill 时会解除信号以避免完成流程永久挂起。

### 假设、风险与缺失证据

- **P1：粒子视觉重叠。** transform 在 `Play(true)` 调用后立即继续，粒子仍可能与页面转换视觉重叠；若产品要求等待粒子完整生命周期，需要另行确认具体粒子持续时间或结束信号。
- **P1：Sequence 打断降级。** 页面仍 active 但 grayImage 被其他 Refresh Kill 时会跳过粒子并继续 transform，优先保证奖励/Chest 流程不锁死；该异常路径尚未运行复现。
- **P2：异步异常。** 粒子入口异常时 `finally` 仍释放等待，避免流程卡死；异常本身会由 UniTask 调度报告，当前未做故障注入。
- C# 构建通过（99 warnings / 0 errors），静态顺序与 diff 检查通过；Unity PlayMode、快速关闭/重复刷新、不同粒子持续时间及 Android/iOS 横屏仍待验证。
- 回滚：移除完成信号与 transform 前等待，恢复 grayImage `OnComplete` 直接调用粒子；无需迁移 DB、配置、prefab 或资源。

## 2026-07-14 CompleteEffect 完整结束与 2 秒停留风险补充

### 事实

- `PostcardPopup.prefab` 的 `completeEffects` 当前绑定 3 个根粒子；其中 1 个非循环、2 个来自 `ConfettiDirectionalGreenYellow.prefab` 的循环根粒子，循环根粒子的 `main.duration` 为 5 秒。
- `PlayCompleteEffects()` 现在并行等待每个有效绑定：循环根粒子播放一个 duration 后停止发射，所有根及子粒子均满足 `IsAlive(true) == false` 后，再统一等待 2 秒。
- PageItem 既有局部完成信号只在 `PlayCompleteEffects()` 返回后释放，因此 Animator `transform` 后移到完整粒子表现和额外停留之后。

### 直接结论

- 正常路径不再只是保证“粒子已发射”，而是保证当前 3 个绑定粒子全部结束且额外经过 2 秒后才开始页面转换。
- 循环粒子不会造成完成流程永久等待；实现按资源自身 duration 播放一轮后停止发射，并保留已发射粒子的自然消散。

### 假设、风险与缺失证据

- **P1：整体节奏变长。** 当前循环根粒子一轮为 5 秒，之后还需等待存活粒子消散和 2 秒停留；真实总时长及引导流程观感尚未在 PlayMode 验证。
- **P1：资源配置变更。** 如果未来把循环粒子的 duration 配得过长，PageComplete 会相应延后；当前未增加固定超时，因为需求明确要求等待完整表现。
- **P2：快速关闭。** 等待绑定 Popup 销毁取消；快速关闭时不会继续进入 transform/Chest，但实际 UI 栈回收尚未运行验证。
- C# 构建通过（99 warnings / 0 errors）；静态 prefab 绑定与循环配置已核对。Unity PlayMode、不同帧率、Android/iOS 横屏和低端设备粒子开销仍待验证。
- 回滚：恢复 `PlayCompleteEffects()` 在 `Play(true)` 后立即返回，并删除循环停止、粒子存活等待与 2 秒停留；不涉及 DB、配置或资源迁移。

## 2026-07-14 满进度 Chest 前移到 CompleteEffects 阶段风险补充

### 事实

- `PlayCompleteEffects()` 由 grayImage 填充与回弹 Sequence 的 `OnComplete` 触发；当前在播放粒子前先准备 Chest，调用既有 `Show()`、`RefreshItems(...)` 和满值 `RefreshProgress(max, max, false)`。
- `HandlePageCompleteAsync()` 在 `PlayCompleteTransformAsync()` 返回后复用 `_pageCompleteChestView` 播放 `chestOpen`；未改变 `PlayChestOpenAnim()`、开箱完成/超时等待、隐藏和领奖调用点。
- grayImage Sequence 未自然完成时不会进入粒子入口；主流程保留原位置的 Chest 准备作为降级。

### 直接结论

- 正常路径中，玩家会在完成粒子开始时看到满进度 Chest；Chest 开箱仍发生在粒子等待和页面转换之后。
- 本次没有重复创建 Chest，也没有新增资源加载、事件、存档或奖励发放入口。

### 假设、风险与缺失证据

- **P1：视觉遮挡。** Chest 与完成粒子会同时可见，实际层级、遮挡和构图尚未在 PlayMode 验证。
- **P1：长时间停留。** Chest 会在完整粒子生命周期及 `CompleteEffectsPostDelay` 期间保持满进度未开启状态，节奏观感待运行确认。
- **P2：异常降级。** Sequence Kill 时 Chest 会回到 transform 后才准备，优先保证开箱数据正确；该路径尚未故障注入验证。
- C# 构建通过（0 warnings / 0 errors）；Unity PlayMode、快速关闭、不同帧率和 Android/iOS 横屏仍待验证。
- 回滚：删除 Chest 缓存和粒子入口准备，恢复 transform 后准备并立即开箱；不涉及 DB、配置或资源迁移。

## 2026-07-14 CompleteEffects 非阻塞风险补充

### 事实

- `PlayCompleteEffects()` 当前在全部有效 `ParticleSystem.Play(true)` 调用完成后立即返回，不再调用 `UniTask.WhenAll(...)`、`IsAlive(true)` 或粒子后的固定停留。
- PageItem 仍等待 `PlayCompleteEffects()` 返回后才发出 `transform`，但该返回边界现在只表示粒子已发射，不表示粒子已结束。
- 循环根粒子由独立本地 UniTask 在一个 `main.duration` 后执行 `StopEmitting`，该清理任务不阻塞 transform、正文或 Chest。

### 直接结论

- 满进度 Chest 出现后不再承担 5 秒以上的粒子等待；正常路径会立即进入 transform，并在既有 1.7 秒页面表现窗口后开始 `chestOpen`。
- 粒子可能继续与 transform、正文和 Chest 开箱重叠，这是本次明确的非阻塞行为变化。

### 假设、风险与缺失证据

- **P1：视觉重叠。** 循环彩带最长继续发射一个 duration，可能覆盖 Chest 开箱画面；实际层级与观感尚未在 PlayMode 验证。
- **P2：后台停止边界。** Popup 销毁会取消停止任务；对象随 Popup 一并销毁时无残留，Popup 复用/禁用路径仍待运行验证。
- C# 构建通过（0 warnings / 0 errors）；Unity PlayMode、快速关闭、不同帧率和 Android/iOS 横屏仍待验证。
- 回滚：恢复粒子生命周期等待、额外停留及 `WhenAll` 同步，不涉及 DB、配置或资源迁移。

## 2026-07-14 满进度 Chest 早于整页完成的风险补充

### 事实

- 最后一个进度 item 解锁动画回调会先调用 `PreparePageCompleteChest()`，再同步调用既有 `_onCompleted` 进入 `OnPageComplete()`；两者之间没有异步等待，`IsPagePlayingAnim` 保持为 true。
- Chest 在该步骤调用既有 `Show()`、奖励刷新和 `RefreshProgress(max, max, false)`；grayImage 与完成粒子不再负责准备 Chest。
- 有效 `chestOpen` 仍等待 Complete/End 或超时保护，但 Complete/End 后不再追加 `chestOpenCompleteExtraDelay`；prefab 中对应序列化字段已移除。

### 直接结论

- 满进度 Chest 的可见与数据刷新边界已早于整页完成入口，不会再等待 grayImage 或粒子阶段。
- 页面按钮不会因为前移刷新短暂解锁；开箱正常完成后会直接进入既有缩放隐藏和后续步骤。
- 历史完成页仍走普通 `RefreshChest(...)` 隐藏规则；专用满进度显示只由首次完成回调触发。

### 假设、风险与缺失证据

- **P1：视觉衔接。** Chest 会在最后一个 item 动画结束帧出现，随后进入 grayImage；真实层级、奖励图标异步刷新和满进度 Tween 观感尚未在 PlayMode 验证。
- **P1：开箱收尾节奏。** 删除额外 1 秒后，Complete/End 会立即触发缩放隐藏；Spine 最后一帧与隐藏 Tween 的衔接需运行确认。
- **P2：异常降级。** 若专用准备未获得 Chest，主流程会在开箱前补做准备；缺失引用和快速关闭故障注入尚未验证。
- 构建通过（99 warnings / 0 errors），目标 diff 检查通过；Unity PlayMode、不同帧率和 Android/iOS 横屏仍待验证。
- 回滚不涉及 DB、配置或资源迁移，只需恢复 Chest 准备点和开箱后的额外等待字段。

## 2026-07-17 `_isGuiding` 使用 ClickBlock 的交互风险补充

### 事实

- 修改前，`_isGuiding` 同时参与 Close、Last、Next 的 `Interactable` 和点击入口判断，引导内部翻页依赖 `allowWhileGuiding=true` 绕过同一条件。
- 当前 `SetGuidingState(...)` 同步设置 `_isGuiding` 与 `ClickBlock.activeSelf`；`OnOpen()` 会在运行态读取现有 `DB.Postcard.IsGuiding` 初始化遮罩，完整引导结束入口 `CompletePostcardGuiding()` 和 Popup 关闭会关闭遮罩。
- `PostcardPopup.prefab` 的 `ClickBlock` 是全拉伸的 `UIEmptyRaycastTarget`，`RaycastTarget=1`、默认 inactive，并序列化绑定到 Popup；其同级顺序在普通内容之后、`Postcard_GuidePart` 之前。该 Bear UI 组件的 `OnPopulateMesh(...)` 会清空顶点，不绘制可见网格。
- `Postcard_GuideFinger.Create(...)` 仍把独立手指实例化到传入 root 的最后一个子节点，因此静态层级上位于已有遮罩之后。

### 直接结论

- **P1 已缓解：按钮状态与引导状态分散。** `_isGuiding` 不再改写三个按钮状态，也不需要引导专用翻页参数，底层 Popup 输入统一由同一个遮罩入口控制。
- **P1 已缓解：遮罩提前释放。** 页内 GuidePart 完成回调不会关闭遮罩；只有完整第四阶段结束或既有失败降级才调用 `CompletePostcardGuiding()`。
- **P1 已缓解：Create 首帧状态错误或缓存实例残留遮罩。** prefab 默认关闭；OnOpen 不继承旧内存态，而是按当前 `DB.Postcard.IsGuiding` 初始化遮罩，OnClose 则显式执行 `SetGuidingState(false)`。
- 页面动画期间按钮仍由既有 `IsPagePlayingAnim` 禁用；本次只替换 `_isGuiding` 对按钮的直接控制，不改变动画交互锁。

### 假设

- 当前同级顺序和动态最后子节点行为足以让 `Postcard_GuidePart`、领奖/Next GuideFinger 接收射线；该结论有 prefab 与代码静态证据，但尚未经过 EventSystem 实际射线结果验证。
- `UIEmptyRaycastTarget` 在目标 World Space Canvas、UI Layer 5 下能覆盖横屏全屏；全拉伸 RectTransform 有静态证据，但尚未运行验证不同分辨率与安全区。

### 缺失证据、验证与回滚

- 缺少 Unity PlayMode 的 `IsGuiding=true/false` 两种 Create 首帧、页内 GuidePart 点击、底层 Close/Last/Next/PageItem 拦截、领奖弹窗、第三/第四阶段、Next Finger、失败降级及关闭重开验证。
- 缺少 Android/iOS 横屏真机触摸、不同宽高比和多指输入验证。
- `git diff --check` 与目标静态扫描通过；`Assembly-CSharp.csproj` 构建通过，结果为 0 error / 99 warnings，warnings 未逐项审计。
- 回滚时删除遮罩状态同步与 prefab `ClickBlock`，恢复 `_isGuiding` 的按钮条件、点击防护和 `TryGoNextPage(bool)`；无需迁移存档、配置或资源地址。

## 2026-07-17 StartGuding 前移的交互风险补充

### 事实与直接结论

- `GameVictoryPanel.NextStepGuideAsync()` 当前在 `PostcardPopup.Create()` 前调用 `PostcardManager.StartGuding()`；Popup 的 `OnOpen()` 因此可在任何页面内容显示前读取 `IsGuiding=true` 并开启 `ClickBlock`。
- Stage Two 关闭回调不再写 `IsGuiding`，只负责调用 `StartPendingPostcardGuide()`；页内 Guide 启动后对 DB pending 状态的消费不会清除 Popup 本地 `_isGuiding`。
- **P1 已缓解：Popup 到 Stage Two 之间点击穿透。** 正常引导路径不再存在 Popup 已显示但自身 ClickBlock 尚未开启的 1.5 秒等待和 Stage Two 展示窗口。
- **P1 已缓解：重复 StartGuding。** 引导状态只在 Popup 创建前写入一次，Stage Two 关闭不重复保存同一状态。
- 正常解锁点仍是 Stage Four 关闭后的 `CompletePostcardGuiding()`；GuidePart、GuidePopup、GuideFinger 创建失败时保留既有降级解锁，优先避免永久输入软锁。

### 风险、缺失证据与回滚

- **P1：Prefab/EventSystem 运行层级尚未验证。** 代码时序已保证 ClickBlock active，但是否在所有分辨率、Stage Two Canvas 和动态 GuideFinger 组合中拦截普通目标且放行指定引导目标，仍需 Unity PlayMode 射线验证。
- **P2：Popup 创建失败留下 pending。** `StartGuding()` 已先保存；若 `PostcardPopup.Create()` 返回 null，`IsGuiding=true` 会保留给后续恢复，而不会在当前入口自动回滚。需验证下次进入与异常中止策略符合产品预期。
- **P2：PostcardManager 缺失。** 当前在创建 Popup 前记录 warning 并停止，不会打开一个没有首帧遮罩保证的 PostcardPopup。
- C# 全工程项目引用构建通过：0 errors / 1061 warnings；首次依赖现有 `Temp/bin/Debug` 的快速构建因 Unity 临时 DLL 缺失失败，该失败不是本次代码编译错误。
- 尚未验证 Android/iOS 横屏、多指输入、快速关闭/销毁以及保存失败后的恢复表现。
- 回滚仅需恢复 `GameVictoryPanel.NextStepGuideAsync()` 的旧调用顺序和 `PostcardManager.StartGuding()` 注释；不涉及 DB schema、prefab、配置或资源迁移。

## 2026-07-17 页面切换表现 ClickBlock 风险补充

### 事实与直接结论

- `IsPagePlayingAnim` 现在通过统一 setter 同步 `clickBlock`；PageItem 解锁入口和整页完成流程开始/结束均进入该 setter。
- `clickBlock` active 条件为 `_isGuiding || IsPagePlayingAnim`，避免任一流程结束时提前释放另一个流程仍需要的遮罩。
- 既有 Close、Last、Next 的 Interactable 与点击入口保护未删除，ClickBlock 是覆盖页面 item 等其他射线目标的额外全屏门禁。
- 本次不新增 prefab、材质、资源加载、保存字段、事件、奖励或平台调用。

### 风险、验证与回滚

- **P1：引导目标层级。** 引导与页面动画重叠时 ClickBlock 会持续开启；静态层级允许 GuidePart/动态 GuideFinger 位于遮罩上方，但实际 EventSystem 命中仍需 PlayMode 验证。
- **P1：异常结束。** PageComplete 使用 `finally` 释放动画状态，普通解锁依赖 `PlayUnlockAnim` 回调；动画组件被异常禁用但回调未返回的故障注入尚未验证。
- **P2：遮罩范围。** prefab 是全拉伸 `UIEmptyRaycastTarget` 且已绑定，但不同横屏比例、安全区和多指触摸仍未运行验证。
- C# 构建通过：0 errors / 1062 warnings；静态扫描未发现绕过统一 setter 的 `IsPagePlayingAnim` 写入。
- 回滚时移除 `IsPagePlayingAnim` 对 `RefreshClickBlock()` 的参与并恢复 PageComplete 直接赋值即可；不涉及数据或资源迁移。

## 2026-07-18 ClickBlock 首帧绑定缺失修正

### 事实与直接结论

- 用户反馈引导打开 `PostcardPopup` 时 `ClickBlock` 没有首帧展开；工作区 prefab 证据显示 `PostcardPopup.clickBlock` 序列化引用及对应 stripped GameObject 引用被移除。
- `PostcardGuideFlow` 的 `StartGuding()` 已位于 `PostcardPopup.Create()` 前，`OnOpen()` 也在加载页面前调用 `SetGuidingState(...)`；因此状态写入与生命周期顺序不是本次失败原因。
- 当前已恢复 prefab 绑定，并让脚本在引用缺失时只从当前 Popup 层级解析名为 `ClickBlock` 的既有节点。只要节点仍存在，`RefreshClickBlock()` 不再因空引用静默跳过。

### 风险、验证与回滚

- **P1 已缓解：prefab 绑定漂移。** 序列化引用恢复，运行时局部解析提供第二条可验证路径；不创建替代遮罩，不改变既有 sibling 顺序。
- **P1 待运行验证：首帧射线。** 静态调用顺序可保证 `OnOpen()` 在 `OnShow()` 返回前更新 active 状态，但 Unity EventSystem 的实际命中仍需 PlayMode 录屏或断点确认。
- **P2：节点重命名。** 若后续同时清空序列化引用并重命名 `ClickBlock`，兜底仍会失败；应把 prefab 绑定检查保留在回归清单中。
- prefab 绑定脚本校验通过，C# 构建通过（0 errors / 99 warnings），目标 diff 检查通过；移动端真机未验证。
- 回滚只需删除局部解析并恢复本次前的 prefab 未绑定状态；无存档、配置或资源迁移。

## 2026-07-23 页面完成奖励前置发放风险补充

### 事实

- `Postcard_PageItem.TryUnlockItem(...)` 在整页完成时先写入 `DB.Postcard.CompletedId` 并调用 `DB.SavePostcard()`；只有保存成功后才扣星、播放解锁动画，并在动画回调中调用 `PreparePageCompleteChest()` 与 `OnPageComplete()`。
- 修改前，`HandlePageCompleteAsync()` 需要依次等待完成变形、Chest 动画和固定延时，之后才通过 `CommonGetRewardPopup.Create(...)` 发放 normal Coin、Star、Skin；奖励弹窗创建失败时流程直接继续。
- 修改后，`Postcard_PageItem.TryUnlockItem(...)` 在 `SavePostcard()` 成功、扣星之后、`PlayUnlockAnim(...)` 之前调用 `TryGrantPageCompleteNormalRewards(...)`；`HandlePageCompleteAsync()` 在首个异步等待前复用同一入口兜底。Coin、Star 复用 `SimpleBag.GrantRewards(...)`，Skin 复用 `GetNewSkin(...)`；成功后奖励弹窗传 `grantNormalReward: false`。
- 前置发放失败时，奖励弹窗传 `grantNormalReward: true` 保留旧路径；同一 Popup 生命周期内以 Postcard Id 记录已成功处理的页面。
- 本次没有新增 Postcard 奖励领取字段、待领取队列、跨存档事务或全局事件。

### 直接结论

- **P1 已缓解：解锁动画或页面完成表现中断导致奖励丢失。** 正常路径在启动最后一块解锁动画前已尝试发放；Bag/GameData 可用时，normal 奖励不再等待解锁回调、完成变形、Chest、延时或奖励弹窗创建。
- **P1 已缓解：展示弹窗重复发奖。** 前置成功后 `CommonGetRewardPopup` 只展示 normal 奖励，广告成功后的额外 Coin/Star 仍保持原职责。
- **P2 已缓解：同一实例重复完成回调。** 流程运行锁配合本地 Postcard Id 集合，覆盖并发进入和流程取消后同实例再次进入；该结论不延伸到应用重启或新 Popup 实例。
- **P1 仍存在：连续保存调用之间的强退窗口。** `SavePostcard()`、扣星和奖励入包是独立同步调用；在任意两个调用之间被系统强制终止时，重新打开后是否存在补扣或补领入口没有本地证据支持。
- **P1 仍存在：两条存档链非原子。** Postcard 完成保存、GameData 扣星和奖励保存无法共同提交或回滚，任一保存链异常都不能由当前局部逻辑证明完全一致。
- **P2：降级路径仍依赖奖励弹窗。** Bag/GameData 未就绪导致前置失败且奖励弹窗也创建失败时，旧有丢奖风险仍存在；当前实现只记录 warning 并继续表现流程。

### 假设

- 正常运行时 `PlayCtrl.Instance.Bag` 与 `DB.GameData` 会在 Postcard 页面完成前就绪，因此前置发放失败应属于异常初始化状态；尚无故障注入或运行日志证明其发生概率。
- 已完成页重新加载不会再次触发最后一个 item 的完成回调，因此本地幂等集合足以覆盖正常单次会话；该行为需要 PlayMode 重开页面验证，不能视为持久化幂等保证。

### 缺失证据、验证与回滚

- CodeGraph 与源码静态确认正常前置发奖位于 `PlayUnlockAnim(...)` 前，`HandlePageCompleteAsync()` 在首个 `await` 前兜底，Common 弹窗按结果切换发奖开关；C# 工程构建通过（0 errors / 99 warnings），目标差异格式检查通过。
- 缺少 Unity PlayMode 对 Coin、Star、Skin、混合奖励、广告倍率、动画中途销毁、奖励弹窗创建失败、Bag/GameData 缺失和同页重复回调的验证。
- 缺少“`DB.SavePostcard()` 成功后、扣星后、奖励保存后分别立即强退并重启”的恢复验证，也没有 Android/iOS 横屏真机强杀、低内存回收和存档写失败证据。
- 若需要彻底消除剩余 P1，后续方案必须增加可恢复的待领取/已领取状态或统一事务边界，并单独设计迁移、幂等和失败重试；本次需求未授权该存档结构扩展。
- 回滚时移除前置发奖、本地 id 集合和 `grantNormalReward` 参数传递，恢复奖励弹窗默认发奖；无存档迁移。

## 2026-07-23 Coin PropBlock 刷新时序风险补充

### 事实与直接结论

- Postcard prefab 当前包含一个新增的 Coin `PropBlock`，其 `IsAutoUpdate` 覆写为 false；因此 `SimpleBag.GrantRewards(...)` 派发 `UpdatePropEvent` 时不会自动修改该数字。
- `PostcardPopup.OnOpen()` 会同步当前真实余额；页面完成奖励前置入包后保持旧显示，直到 `CommonGetRewardPopup` 关闭、父 Popup 恢复显示或异步等待完成时才手动 `UpdateData()`。
- **P1 已缓解：奖励表现前数字提前跳变。** 父 Popup 的 Coin 显示不再跟随前置发奖事件立即变化。
- **P2 已缓解：奖励弹窗返回后数字不更新。** `OnShow()` 和异步等待共用一次性待刷新标记；弹窗创建失败也有立即刷新路径。
- 本次只控制显示时机，不改变真实 Coin 发放、广告额外奖励、保存、SDK 上报或 Common 奖励飞行动画。

### 风险、缺失证据与回滚

- **P1：prefab 配置依赖。** 若 `IsAutoUpdate` 被重新打开，事件自动刷新会绕过 Popup 控制；需要把该覆写纳入 prefab 回归检查。
- **P2：运行时兜底选择。** 序列化引用为空时使用当前 Popup 子级的首个 `PropBlock`；当前 prefab 静态证据只有目标 Coin 实例。若以后新增其他 PropBlock，应显式绑定 `coinPropBlock`，避免兜底选错。
- **P2：生命周期顺序待验证。** Bear UI 的 `OnShow()` 与 Common Popup 关闭回调先后由一次性标记兼容，但实际首帧是否存在旧数字闪现仍需 PlayMode 录屏确认。
- C# 构建通过（0 errors / 99 warnings），CodeGraph 调用点与目标差异格式检查通过；Unity PlayMode、Android/iOS 横屏、广告成功/失败及缓存实例重开未验证。
- 回滚时删除 PostcardPopup 的 Coin PropBlock 缓存和手动刷新入口，恢复 prefab 自动刷新或由其他界面统一刷新；无数据迁移。

## 2026-08-19 引导后一次性 TipsBubble 风险补充

### 事实

- `PostcardManager.StartGuding()` 在首次引导用 Popup 创建前写入 `HasGuide=true / IsGuiding=true`；`PostcardPopup.TryStartPostcardGuidePart(...)` 启动 GuidePart 后写入并保存 `IsGuiding=false`，完整 Flow2 的 Stage Three、切页和 Stage Four 在其后继续执行。
- 当前一次性门槛只在 `PostcardPopup.OnOpen()` 执行，条件为 `HasGuide && !IsGuiding && !HasShowTipsBubble`；首次引导当前打开的 Popup 因 `IsGuiding=true` 不会展示，同一 Popup 在 Flow2 收尾时也不会临时触发。
- `PostcardPopupTipsBubble` 不访问 DB；它只在运行态控制自身 unscaled DOTween、EventSystem 点击和 active 状态。Popup 在表现成功启动后设置 `HasShowTipsBubble=true` 并调用 `DB.SavePostcard()`。
- 气泡源 prefab 默认 inactive，新脚本组件绑定在根节点；可射线的子级 Image 保持 `RaycastTarget=true`，主 Popup 继续嵌套用户新增的同一 prefab 实例。
- 本次新增一个 bool 存档字段，不新增配置、资源加载、实例化、全局事件、奖励、广告、IAP、analytics 或平台分支。

### 直接结论

- **P1 已缓解：首次引导期间误展示。** 当前引导打开 Popup 时 `IsGuiding=true`，而气泡只检查一次 `OnOpen()`；GuidePart 消费 pending 状态后不会在同一生命周期重新检查。
- **P1 已缓解：重复打开反复展示。** 成功启动表现即写入并保存 true，不依赖玩家等待 3 秒或点击关闭；普通关闭和 Popup 清理也会终止残留 Tween。
- **P2 已缓解：组件或绑定缺失误消费。** 找不到 `PostcardPopupTipsBubble` 或 `TryShow()` 返回 false 时不改存档，资源修复后仍可再次满足展示门槛。
- **P2 已缓解：暂停态卡住。** 展开、等待和关闭均使用 unscaled DOTween，不依赖游戏 timescale；每次展示只有一条短 Sequence，没有逐帧业务扫描或持续分配循环。

### 假设

- Unity EventSystem 会把子级 Image 的点击沿层级交给根节点 `IPointerClickHandler`。该行为符合 Unity 事件层级模型并有静态射线配置证据，但当前 Popup Canvas 上的实际命中和 ClickBlock 同级遮挡尚未在 PlayMode 复现。
- 需求中的“hasGuide 结束”可由 `HasGuide=true / IsGuiding=false` 表示。现有数据没有“Stage Four 成功完成”的独立持久化字段，因此如果 GuidePart 已消费 `IsGuiding` 后 Flow2 失败或被中断，下次打开也会满足气泡门槛；产品是否把此异常路径视为“引导已结束”仍为 `uncertain`。

### 缺失证据、风险与回滚

- **P1：保存失败后的跨进程重复。** `DB.SavePostcard()` 返回 false 时内存仍为 true，本会话不重显，但重启后可能再次展示；没有事务、重试或独立恢复队列，不能宣称保存失败时也永久只展示一次。
- **P1：射线层级。** `PostcardPopup` 的 ClickBlock、GuidePart、气泡和动态 GuideFinger 的实际 sibling/Canvas 射线优先级需要验证；若气泡被遮罩拦截，自动关闭仍可完成，但点击关闭不可用。
- **P2：缓存实例与异常禁用。** 正常 `OnClose()` 会立即清理；未经过 `OnClose()` 的父节点强制禁用只能依赖组件 `OnDisable()` 杀 Tween，重新启用前的首帧 activeSelf/scale 是否闪现待运行验证。
- **P2：视觉与本地化。** 0.3 秒 `OutBounce`、0.2 秒关闭、现有英文/I2 文本、不同语言换行和横屏安全区仅有 prefab 静态证据，尚未做 30/60 fps 与多宽高比录屏检查。
- `dotnet build Assembly-CSharp.csproj --no-restore --nologo --verbosity:minimal` 通过：0 warning / 0 error；脚本 GUID、源 prefab 默认 inactive、主 Popup 嵌套引用和目标差异格式检查通过。
- 缺少 Unity PlayMode 的 `HasGuide/IsGuiding/HasShowTipsBubble` 组合矩阵、点击/自动关闭竞争、Popup 快速关闭重开、保存失败故障注入、旧档升级和 Android/iOS 横屏真机触摸验证。
- 回滚时删除气泡运行组件、Popup 一次性门槛、存档公开属性/初始化及字段；无需修改 Guide Flow、页面解锁、奖励、配置、资源地址或平台 SDK。

## 2026-08-19：BearResources / YooAsset 内置托管风险补充

### 事实

- 16 个 Postcard 页面 prefab 已从 Unity `Resources` 移至 `Assets/Game/UI/Postcard/Prefabs/Pages/`，`.meta` 随资产一起移动；`PostcardPopup.prefab` 外壳仍在 `Resources`。
- 页面加载改为以完整资产路径调用 `Bear.ResourceSystem.ResourceManager.Load<GameObject>()`，切页和关闭时调用 `Release(path)`；新 Collector 属于现有 `GamePackage`，当前仍使用内置 `ClearAndCopyAll` 策略。
- 配置短名、存档结构、解锁/奖励逻辑和 UI 可见流程不变；只新增加载失败时停止引导、消费 pending `IsGuiding` 并恢复输入的异常降级。

### 直接结论

- **P1 已缓解：资源句柄长期滞留。** 正常切页和关闭有明确 Release 边界；同一路径重建保留当前句柄，防止资源在实例化前被提前释放。
- **P1 已缓解：引导资源缺失导致输入软锁。** 加载失败会结束当前 Guide Flow 并解除本地引导锁；代价是该次教程不会自动重试，`HasGuide` 仍保持原语义。
- **P1 仍待验证：正式 YooAsset 构建产物。** 静态 Collector 配置和 C# 编译不能证明 Android/iOS 清单、AssetBundle 和共享依赖均被正确生成并随包携带；未构建前该结论为 `uncertain`。
- **P2：同步加载尖峰。** 为保持原有同步页面切换时序，本阶段继续使用同步 Load；首次解压/加载 AssetBundle 时的主线程耗时需要 PlayMode 和真机 Profile 证据。
- **P2：全局缓存的未来共享边界。** BearResources 按路径缓存且 Release 不提供调用方引用计数。当前扫描未发现其他消费者使用这些新路径；若后续多个界面共享同一路径，需要重新定义所有权，避免一方释放影响另一方。
- **P2：包体收益尚不存在。** 当前所有 Postcard Bundle 仍内置复制，迁移只建立 YooAsset 管理边界；没有构建报告证据支持 APK/IPA 已减小。
- **P2：依赖拆分未知。** `PackSeparately` 不等于所有依赖都按页隔离，共用贴图、字体或材质的重复/保留关系只能从正式构建报告判断。

### 缺失证据、验证与回滚

- 已验证 16/16 配置映射、0 个旧 Resources 页面、16 个新目录页面、Git 重命名/GUID 保持，以及目标运行时/Editor C# 工程 0 error；Unity 日志尾部未发现本次导入错误。
- 缺少正式 `GamePackage` 构建与 BuildReport、PlayMode 连续切页/同页重建/关闭重开/加载失败故障注入、Android/iOS 真机启动和低内存恢复证据。
- 回滚路径为恢复页面 Resources 目录和 `Resources.Load`，移除 Collector 及 BearResources Release/失败恢复；配置和存档无需迁移。若只回滚打包规则而保留新加载路径，页面会因不在 YooAsset 清单中而无法显示，不属于安全的部分回滚。

## 2026-08-26：TipsBubble CustomButton 重显风险补充

### 事实与直接结论

- `PostcardPopup.prefab` 的 StarBlock 下已有独立 `clickArea`，包含透明射线 `Image` 和 `CustomButton`；TipsBubble 的 `replayButton` 已显式绑定该组件。
- Popup 打开时用 `HasShowTipsBubble` 同步重显资格；首次自动展示成功后资格立即变为 true，但 `_isShown/_isClosing` 门禁会让按钮直到 bubble 隐藏后才可交互。
- 按钮点击复用 `TryShow()`，不会再次修改 `HasShowTipsBubble` 或调用 `DB.SavePostcard()`；原一次性自动提示与保存失败风险不变。
- **P2 已缓解：展示中重复触发。** `CustomButton.Interactable` 与回调内状态门禁共同阻止正在展示/关闭时重置 Tween；完全隐藏后才允许下一次展示。
- **P2 已缓解：重复订阅。** `_isReplayButtonBound` 保证多次打开刷新不叠加回调，`OnDestroy()` 负责正常销毁解绑。

### 风险、缺失证据与回滚

- **P1：Prefab 绑定依赖。** 若 `replayButton` 引用被清空，首次自动展示仍工作，但按钮不会改变交互状态也无法重显；需要把该序列化引用纳入 prefab 回归检查。
- **P2：点击区域与既有 UI 重叠。** 当前静态 YAML 能证明尺寸、射线和绑定，不能证明 StarBlock 上方的真实事件层级没有被 ClickBlock、GuidePart 或其他 Graphic 遮挡；PlayMode 结果仍为 `uncertain`。
- **P2：关闭边界。** Popup 关闭会隐藏 bubble；外部按钮和 bubble 同属 Popup，正常销毁不存在跨 Popup 对象引用，但缓存式禁用、快速关闭重开的实际事件时序仍待运行验证。
- 构建通过：0 error / 1138 warnings；静态检查确认无 `Update` 轮询、无新全局事件、无新 DB/config/resource/analytics/platform 路径。
- 缺少 Unity PlayMode 的首次展示前按钮状态、自动/手动关闭后连续重显、快速连点、Popup 关闭重开、30/60 fps，以及 Android/iOS 横屏真机触摸验证。
- 回滚时删除 `replayButton` prefab 覆写、按钮订阅和 Popup 的 `SetReplayEnabled(...)` 同步，不需要存档或配置迁移。
