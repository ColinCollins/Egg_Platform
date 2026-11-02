# Postcard_Chest 风险评估

## 原始行为基线

`Postcard_Chest` 原先只有字段和空方法；`RefreshItems(...)` 仅隐藏 bubble，不刷新奖励内容，`RefreshProgress(...)` 为空实现。玩家点击宝箱不会触发 bubble 显示或奖励预览刷新，宝箱进度条也不会根据外部传入进度变化。

补充控制后，`Postcard_Chest` 自身不再写入假奖励数据，当前奖励与进度由 `PostcardPopup` 在配置页切换和解锁动画结束时刷新；整页完成领奖阶段由 Popup 隐藏 Chest，自动切到下一页后再显示并刷新。

2026-07-09 补充：整页完成入口现在会先播放 `Postcard_Chest.PlayChestOpenAnim()`，等待 `box-open` 的 Spine `Complete` 或 `End` 后，再按 `PostcardPopup` Inspector 字段 `chestOpenCompleteExtraDelay` 默认额外停留 1 秒，随后隐藏 Chest 并继续领奖流程。原先完成流程是刷新 Chest 后短等待、隐藏 Chest、再打开奖励弹窗。

2026-07-09 追加：complete page 重新打开或浏览时也需要隐藏 Chest。原先 `PostcardPopup.RefreshChest(...)` 每次刷新都会 `Show()` Chest，导致已完成页在非首次完成流程中仍可能显示 Chest；当前由 Popup 在刷新入口检测完成态并调用 `Postcard_Chest.Hide()`。

## 影响文件与资源

- `Assets/Game/UI/Postcard/Scripts/Postcard_Chest.cs`
- `Assets/Game/UI/Postcard/Scripts/PostcardPopup_PageCompleteProcess.cs`
- `Assets/Game/UI/Postcard/Resources/PostcardPopup.prefab`
- `Assets/Game/UI/Postcard/Prefabs/Postcard_ChestRewardItem.prefab`
- `Assets/Game/ArtSrc/Spines/sp_box-open_002/sp_box-open_002.json`
- `Assets/Game/ArtSrc/Spines/sp_box-open_002/sp_box-open_002_SkeletonData.asset`

## 已使用系统

- `CustomButton.OnClick`：绑定宝箱点击入口。
- DOTween：用于 bubble 显示缩放和延迟自动隐藏。
- `CommonRewardItem.Refresh(Sprite, int)`：用于已绑定奖励 item 列表的占位刷新。
- `CommonRewardItem.RefreshSkin(Sprite, int)` / `SkinUtils`：用于 Skin 奖励预览图标加载，口径对齐 `CommonGetRewardPopup`。
- `Image.fillAmount` 与 `TMP_Text.text`：用于按外部传入 `current/max` 刷新宝箱进度显示。
- `UISpineCtrl` / `SkeletonGraphic`：用于播放 `box_idle`、`box-open` 和 `box-open_idle` 宝箱 Spine 动画。
- `Spine.TrackEntry.Complete` / `End`：用于让 `PostcardPopup` 等待开箱表现结束，再按 Inspector 等待参数停留后隐藏 Chest。

## 风险项

- P1：`bubble`、`rewardItems` 或 `clickBtn` prefab 绑定缺失时会导致表现缺失。当前代码对空引用做了跳过，不阻断弹窗打开，但视觉仍需 PlayMode 验证。
- P1：`DOVirtual.DelayedCall` 自动隐藏依赖对象生命周期；当前在 `OnDisable()` 和 `OnDestroy()` 中 Kill tween，降低关闭弹窗后回调残留风险。
- P1：`progressBar` 或 `progressTxt` prefab 绑定缺失时会导致进度显示缺失。当前代码对空引用做安全跳过，不阻断弹窗打开。
- P1：连续刷新进度时旧 fill tween 可能与新值竞争；当前每次刷新前 Kill 旧 tween，并在 `OnDisable()` / `OnDestroy()` 清理，降低关闭后回写风险。
- P1：如果 `PostcardPopup` 未正确解析到 `Postcard_Chest`，页面切换仍可继续，但宝箱奖励和进度不会刷新；当前 Popup 会优先使用序列化字段，未绑定时按 `Postcard_Chest` 节点和子组件兜底查找。
- P1：完成领奖流程会隐藏 Chest；如果奖励弹窗关闭后下一页保存失败或没有下一页，Chest 会保持隐藏，是否需要在末页完成后重新显示仍待产品确认。
- P1：`spineCtrl`、`SkeletonGraphic` 或 `sp_box-open_002` 资源绑定缺失时，`PlayChestOpenAnim()` 会返回 `null` 并降级走 `pageCompleteStepDelay` 短等待流程；玩家不会看到开箱动画，但领奖流程不应被阻断。
- 已缓解（原 P1）：如果 `box-open` 动画 TrackEntry 未触发 `Complete` 或 `End`，完成流程过去会停在开箱等待；当前等待除 Popup 销毁取消和双事件监听外，还会在 `max(3 秒, 动画时长 + 1 秒)` 后记录 warning 并继续流程。
- P1：`Show()` 现在会重置为 `box_idle`，用于避免开箱待机状态残留；如果后续美术要求同一已完成页重开仍保持打开状态，需要重新确认该口径。
- OBS：`pageCompleteStepDelay` 和 `chestOpenCompleteExtraDelay` 已暴露到 `PostcardPopup` Inspector，配置为负数时按 0 秒处理；实际节奏仍需 PlayMode 由美术/策划确认。
- OBS：`max <= 0`、`current < 0` 或 `current > max` 会被限制到安全显示范围；真实业务是否希望显示特殊文案仍需 UI/策划确认。
- OBS：Chest 当前按 `CommonGetRewardPopup` 的口径解析 Coin / Star / Skin；Skin 依赖配置 id 和 icon 资源可加载，资源缺失时不阻断 Chest 刷新但图标为空。
- OBS：Chest 进度按当前配置 `UnlockProgress.Count` 作为最大值；未完成页按 `DB.Postcard.CurrentCollectedPuzzledId` 命中配置 key 的数量显示，complete page 当前直接隐藏 Chest，不再显示满值进度；跨页浏览时该存档字段是否只代表当前页仍需 PlayMode 验证。
- OBS：complete page 当前不再刷新 Chest 进度，因此玩家不会在已完成页看到满值 `max/max`；该口径来自用户本次要求，若后续需要保留满值进度但隐藏奖励 bubble，需要重新拆分显示规则。

## 验证证据

- 已执行 `git diff --check -- Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs Assets/Game/UI/Postcard/Scripts/PostcardPopup功能说明.md Assets/Game/UI/Postcard/Scripts/PostcardPopup风险评估.md Assets/Game/UI/Postcard/Scripts/Postcard_PageItem功能说明.md Assets/Game/UI/Postcard/Scripts/Postcard_Chest功能说明.md Assets/Game/UI/Postcard/Scripts/Postcard_Chest风险评估.md DevelopMD/已有功能设计与使用记录.md`，结果通过，仅提示工作区后续可能按 Git 设置进行 LF/CRLF 转换。
- 已执行 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`，结果为 97 warnings / 0 errors。
- 2026-07-09 静态证据：`Assets/Game/ArtSrc/Spines/sp_box-open_002/sp_box-open_002.json` 存在 `box-open`、`box-open_idle`、`box_idle` 动画；`PostcardPopup.prefab` 的 `Postcard_Chest` 子级存在 `SkeletonGraphic`、`UISpineCtrl` 和 `sp_box-open_002_SkeletonData` 绑定。
- 2026-07-09 静态证据：`PostcardPopup_PageCompleteProcess.cs` 中 `pageCompleteStepDelay` 与 `chestOpenCompleteExtraDelay` 为 Inspector 序列化字段，默认分别为 0.5 秒和 1 秒；等待路径使用 `Mathf.Max(0f, ...)` 防止负数等待。
- 2026-07-09 `git diff --check -- Assets/Game/UI/Postcard/Scripts/Postcard_Chest.cs Assets/Game/UI/Postcard/Scripts/PostcardPopup_PageCompleteProcess.cs`：通过，仅有工作区 LF/CRLF 转换提示。
- 2026-07-09 静态敏感扫描 `Postcard_Chest.cs` 和 `PostcardPopup_PageCompleteProcess.cs` 未命中编辑器生命周期、每帧轮询、全局事件、RemoteConfig、广告、IAP、analytics 或 PlayerPrefs 关键路径。
- 2026-07-09 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：未通过，首个错误为缺失 `Temp\bin\Debug\Apple.Core.dll`，共 191 个 `CS0006` 元数据缺失错误，未获得本次业务脚本编译诊断。
- 2026-07-09 追加 Inspector 等待参数后重跑 `git diff --check`：通过，仅有 LF/CRLF 转换提示；重跑 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，99 warnings / 0 errors。
- 2026-07-09 参考 `CommonGetRewardPopup` 修正 Chest 奖励图标来源后，`PostcardPopup.prefab` 的 `Postcard_Chest` 已绑定与 `CommonGetRewardPopup` 相同的 coin/star sprite GUID；目标文件 `git diff --check` 通过，仅有 LF/CRLF 提示；`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，0 warnings / 0 errors；静态扫描未发现无效 `RefreshSkin(,` / `Refresh(,` / `GetCurrentRewardSprite`。
- 2026-07-09 complete page 隐藏 Chest：`PostcardPopup.RefreshChest(...)` 已增加完成态隐藏分支；目标文件 `git diff --check` 通过，仅有 LF/CRLF 提示；本次 `PostcardPopup.cs` diff 敏感关键字扫描无命中；`dotnet build .\Assembly-CSharp.csproj --no-restore ...` 通过，98 warnings / 0 errors；Unity PlayMode 尚未验证。

## 未验证缺口

- Unity PlayMode 中打开 `PostcardPopup`，确认进入初始页时 Chest 奖励来自当前 `PostcardConfig.Rewards`，进度来自当前页收集状态。
- Unity PlayMode 中切换不同 config page，确认 Chest 奖励槽和进度立即刷新，未解锁占位页显示空奖励和 `0/0`。
- Unity PlayMode 中解锁单个 item 和完成整页后，确认 Chest 进度刷新到最新值。
- Unity PlayMode 中完成整页后，确认 Chest 在领奖阶段隐藏，并在自动切到下一页后重新显示和刷新。
- Unity PlayMode 中已经 complete 的页面重新打开或从其它页切回时，确认 Chest 保持隐藏。
- Unity PlayMode 中完成整页后，确认 `box-open` 完整播放、切换到 `box-open_idle`、按 `chestOpenCompleteExtraDelay` 停留、随后隐藏 Chest 并继续 `CommonGetRewardPopup` / Skin 转换 / transform / 自动切页流程。
- Unity PlayMode 中确认 Chest 的 Coin / Star 图标与 `CommonGetRewardPopup` 一致，Skin 奖励能加载真实 icon；配置无效或资源缺失时不会回写旧图。
- Unity Editor 侧重新导入/PlayMode 验证序列化字段默认值和 Inspector 覆盖值；`Assembly-CSharp.csproj` 静态编译已通过。
- Unity PlayMode 中调用 `RefreshProgress(...)`，确认 `0/max`、中间值、满值、`max <= 0`、连续刷新和关闭弹窗时的进度表现。
- Android/iOS 横屏设备上的点击区域、缩放动画和弹窗关闭清理。
- Android/iOS 横屏设备上的开箱 Spine 位置、缩放、遮挡关系和触摸锁期间 UI 栈表现。

## 回滚路径

移除 `PostcardPopup` 中 Chest 解析和刷新调用，`Postcard_Chest` 可继续保留为被动 UI 控件；若需完全回到早期空实现，则恢复 `Awake()`、`ClickChest(...)`、`SwitchBubble(...)`、`RefreshProgress(...)` 空实现和 `RefreshItems(...)` 仅隐藏 bubble 的状态。

若只回滚 2026-07-09 开箱动画，移除 `PostcardPopup_PageCompleteProcess.HandlePageCompleteAsync()` 中 `PlayChestOpenAnim()` / `WaitChestOpenAnimAsync(...)` 调用，并把 `Postcard_Chest.PlayChestOpenAnim()` 恢复为空实现或保留为未调用的本地表现接口即可；若只回滚 Inspector 暴露等待参数，则把 `pageCompleteStepDelay` / `chestOpenCompleteExtraDelay` 恢复为局部常量并移除序列化字段。

## 2026-07-10 首次完成 Chest inactive 软锁风险复核

- **原始基线：** 最后一个 item 保存完成状态后，Popup 的普通刷新会因 `CompletedId` 命中而 `Hide()` Chest；之后开箱仍可能返回非空 `TrackEntry`，但 inactive `SkeletonGraphic` 不更新，旧代码没有超时。
- **当前处理：** 首次完成专用准备步骤强制显示 Chest、刷新奖励和满值进度；`PlayChestOpenAnim()` 拒绝 inactive 层级；事件等待增加 unscaled 有界超时。历史完成页隐藏规则保持不变。
- **影响范围：** 只修改 `PostcardPopup_PageCompleteProcess.cs` 和 `Postcard_Chest.cs`；静态检查确认 prefab、Spine 数据和页面 Complete Animator 无需修改，也未触碰 DB/save、奖励、事件、analytics 或平台 SDK。
- **验证证据：** `Assembly-CSharp.csproj` 编译通过，98 warnings / 0 errors；目标代码 `git diff --check` 通过；敏感扫描无新增高风险入口。
- **未验证：** PlayMode 中正常 Complete/End、强制超时、缺失 Spine、Popup 销毁、Guide 最后一项、历史完成页、末页，以及 Android/iOS 横屏表现。
- **回滚：** 删除首次完成专用准备、超时等待和 inactive 检查即可；无需回滚资源或存档，回滚后软锁风险恢复。

## 2026-07-13 Complete Chest 缩放隐藏风险补充

### 事实与直接结论

- 普通 `Hide()` 调用仍使用默认参数 false 并立即隐藏；只有 `HandlePageCompleteAsync()` 在开箱等待结束后调用 `Hide(true)`。
- 缩放目标为 0，持续 0.2 秒，使用 unscaled time；Tween 完成后关闭 GameObject，并把根节点缩放恢复为 1，保证下一次 `Show()` 的初始状态。
- Show、Disable、Destroy 都会 Kill 当前隐藏 Tween，因此切页或 Popup 生命周期变化不会留下仍运行的 Chest 缩放 Tween。

### 风险与未验证项

- **P2：领奖弹窗与缩放重叠。** 当前默认 `pageCompleteStepDelay=0.5` 大于隐藏时长；如果 Inspector 将该等待改为小于 0.2 秒，后续领奖 UI 可能与 Chest 缩小短暂重叠，实际层级需 PlayMode 验证。
- **P2：快速 Show/Hide。** Show 会终止 Tween并恢复 scale=1，静态逻辑可避免零缩放残留；连续切页、关闭重开和外部禁用尚未运行时验证。
- **P2：移动端 UI 重建。** 本次只新增单次 RectTransform Tween，没有每帧常驻逻辑或实例化；Android/iOS 横屏 Canvas rebuild 与低端设备帧耗尚未测量。
- C# 构建通过，98 warnings / 0 errors；目标 diff 检查通过。Unity PlayMode、真机视觉与性能仍未验证。
- 回滚时移除可选缩放隐藏分支并恢复 complete 流程直接 Hide；无 DB、配置、prefab 或资源迁移。

## 2026-07-14 Chest 隐藏动画 Inspector 参数风险补充

### 事实与直接结论

- Complete 缩放隐藏的持续时间与 Ease 已从代码硬编码改为 `Postcard_Chest` 私有序列化字段，当前代码默认保留用户调整后的 `1` 秒和 `Ease.Linear`。
- 两个字段只在 `Hide(true)` 中读取；普通完成页浏览调用的默认 `Hide()`、Spine 开箱、奖励发放、DB/save 和页面完成状态均未修改。
- 持续时间在 Inspector 和运行时均限制为不小于 0；配置为 0 时允许立即完成缩放 Tween。
- 调用链复核确认存在一个额外立即隐藏入口：`OnPagePlayAnim(false) → RefreshCurrentChest(false) → RefreshChest(...) → Hide()`。当前已限制 `OnPagePlayAnim(false)` 只刷新未完成配置，避免已写入 `CompletedId` 后绕过 PageComplete 的 `Hide(true)`。

### 风险与未验证项

- **P2：节奏配置不匹配。** 如果隐藏时长大于后续 `pageCompleteStepDelay`，领奖弹窗可能在 Chest 尚未完全缩小时出现；需要在 Inspector 调参后同步验证两个参数的组合。
- **P2：Ease 造成视觉过冲。** 部分 Ease 会产生超调或回弹，可能与 UI 边界、粒子和奖励弹窗重叠；当前默认 `Linear` 不引入过冲。
- **P2：运行时重复调用仍需验证。** 静态代码中完成流程只保留一次 `Hide(true)`；页面加载仍可对历史完成页调用默认 `Hide()`。首次完成的实际调用次数需要 PlayMode 日志或断点确认。
- 尚未在 Unity Inspector 中确认现有 prefab 实例的字段显示和默认值，也未进行 PlayMode、Android/iOS 横屏视觉验证。
- 回滚时恢复代码常量与固定 Ease；无需修改 DB、配置表、资源地址或存档。

## 2026-07-14 Root Animator 属性争用风险补充

### 事实与直接结论

- `postcard in.anim` 直接绑定 `Postcard_Chest.m_LocalScale`，与 `Hide(true)` 的缩放 Tween 写入目标相同；这是隐藏过程看不到缩小表现的直接冲突点。
- 新增的 `postcard_idle.anim` 不含任何曲线或 binding；Controller 在进场结束后零过渡进入该状态，并已关闭 idle 的 `Write Defaults`，因此进入 idle 后不再由 Animator 回写 Chest 缩放。
- 未禁用 Root Animator，也未调整 `postcard in` 的曲线，Popup 其它进场表现保持原资产路径。

### 风险、验证与回滚

- **P2：Hide 发生在进场状态结束前。** 此时 `postcard in` 仍会写 Chest 缩放；正常 Complete 链路预计晚于进场，但该时序尚未用 PlayMode 日志或 Animator 状态采样验证。
- **P2：Animator 状态配置回退。** 若以后重新开启 idle 的 `Write Defaults`，或给 idle Clip 增加 Chest scale 曲线，属性争用会复现；修改 Controller 时需复核这两项。
- 静态 YAML 已确认空 Clip、零过渡与 idle `Write Defaults=Off`；Unity PlayMode、真机横屏视觉和首次完成实际调用次数仍待验证。
- 回滚时移除 idle 状态、空 Clip和转场即可；无需迁移 DB、配置或存档，但会恢复原属性争用。

## 2026-07-14 PlayChestOpenAnim 异步链风险补充

### 事实

- 优化前的工作区实现会在 `PlayChestOpenAnim()` 内等待 0.5 秒后才订阅 `TrackEntry.Complete`，调用方再通过 `.Result` 同步取得异步返回值。
- 当前实现同步返回 `TrackEntry`，先订阅完成事件，再以独立 `UniTask` 延迟触发 `openEffect`；完成流程仍使用既有 `WaitChestOpenAnimAsync(...)` 监听和超时保护。
- Chest 维护当前开箱 TrackEntry 与开箱版本号；重复播放、Show、Disable、Destroy 均会解除旧完成回调并使旧延迟任务失效。

### 直接结论

- 已移除 `.Result` 造成的 Unity 主线程同步等待，开箱 Spine 启动后调用方可立即继续异步完成等待。
- 完成事件不再因 0.5 秒延迟订阅而漏掉早于该时间完成的动画；旧动画完成回调也不会覆盖后续开箱状态。
- 本次不产生每帧轮询、运行时实例化、DB/save、资源加载或全局事件开销；新增成本仅为每次开箱一个有界延迟任务和常量级状态。

### 假设与待验证缺口

- **P2：视觉时序。** 0.5 秒特效落点沿用当前需求值，但尚未在 Unity PlayMode 中确认与实际 Spine 帧、Root Animator 和 Chest 缩放的视觉配合。
- **P2：快速生命周期切换。** 静态代码会让过期任务退出并解除旧回调；快速完成、关闭、重开、重复调用的实际表现仍需 PlayMode 验证。
- **P2：TimeScale。** 延迟继续使用原尝试的 scaled time；暂停态下特效会等待 TimeScale 恢复，该行为是否符合产品节奏尚未确认。
- Android/iOS 横屏设备上的帧时序、遮挡和低端机表现未验证。

### 验证与回滚

- `git diff --check` 通过，仅有 LF/CRLF 转换提示；目标敏感项扫描无命中；`Assembly-CSharp.csproj` 构建通过，98 warnings / 0 errors。
- 回滚时恢复完成回调内直接触发 `openEffect`，删除版本号、当前 TrackEntry 和延迟辅助方法；不涉及 prefab、配置、资源、DB 或存档迁移。

## 2026-07-14 满进度提前显示与开箱立即收尾风险

### 事实与直接结论

- Popup 在最后一个进度 item 动画稳定后、整页完成入口前刷新 Chest 奖励与 `max/max`，Chest 的首次完成显示不再依赖 grayImage 或完成粒子回调。
- `box-open` 仍等待 TrackEntry Complete/End 或超时保护；正常完成后不再追加 1 秒，直接进入 `Hide(true)`。
- `Postcard_Chest.cs` 未修改，普通未完成页刷新、历史完成页隐藏、bubble、奖励预览和 Spine 本地生命周期保持原实现。

### 风险、验证与回滚

- **P1：提前显示观感。** 最终 item 结束到 grayImage 开始之间的 Chest 出现帧、满进度 Tween 和奖励图标异步加载仍需 PlayMode 验证。
- **P1：立即隐藏观感。** 开箱最后一帧与 0.2 秒缩放隐藏之间不再有停顿，需确认不会显得突兀或截断 `box-open_idle`。
- 构建通过（99 warnings / 0 errors），静态 diff 检查通过；快速关闭、重复完成保护和 Android/iOS 横屏仍待验证。
- 回滚仅需恢复 Popup 的准备点与额外等待字段，不需要迁移 Chest prefab 资源、配置、DB 或存档。
