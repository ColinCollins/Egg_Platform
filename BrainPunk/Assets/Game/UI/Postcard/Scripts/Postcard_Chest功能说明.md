# Postcard_Chest 功能说明

## 需求背景

2026-07-06 按需求补充 `Postcard_Chest` 的基础运行逻辑：点击宝箱切换 bubble 显隐，并通过 `PostcardPopup` 在配置页切换时调用 `RefreshItems(...)` 与 `RefreshProgress(...)` 刷新宝箱奖励预览和进度条。实现保持局部，不接入真实奖励发放。

2026-07-09 追加：按需求补齐 `Postcard_Chest.PlayChestOpenAnim()`，在明信片页面整页 Complete 触发后、领奖弹窗流程继续前播放宝箱 `box-open -> box-open_idle` Spine 表现。2026-07-14 已取消有效开箱动画完成后的额外 1 秒停留，Complete/End 后直接进入隐藏与后续流程。该动画只作为完成瞬间的 UI 效果，不改变奖励发放、存档或页面完成判定。

## 影响范围

- `Assets/Game/UI/Postcard/Scripts/Postcard_Chest.cs`
- `Assets/Game/UI/Postcard/Scripts/PostcardPopup_PageCompleteProcess.cs`
- `Assets/Game/UI/Postcard/Resources/PostcardPopup.prefab`
- `Assets/Game/UI/Postcard/Prefabs/Postcard_ChestRewardItem.prefab`
- `Assets/Game/ArtSrc/Spines/sp_box-open_002/sp_box-open_002.json`
- `Assets/Game/ArtSrc/Spines/sp_box-open_002/sp_box-open_002_SkeletonData.asset`
- `PostcardPopup.prefab` 中已绑定的 `Postcard_Chest`、`bubble`、`rewardItems`、`clickBtn`、`coinSprite`、`starSprite`、`progressBar`、`progressTxt` 运行表现。
- `PostcardPopup.prefab` 中 `Postcard_Chest` 子级 `New SkeletonGraphic` 已绑定 `sp_box-open_002_SkeletonData.asset`，同节点挂有 `UISpineCtrl`；`Postcard_Chest` 运行时可从子级兜底解析该控制器。

本次除复用 `SkinUtils` 加载 Skin 预览 icon 外，未修改 DB、RemoteConfig、广告、IAP、analytics、平台 SDK 或全局事件。

## 原有行为基线

- `Awake()`、`ClickChest(...)`、`SwitchBubble(...)` 为空实现。
- `RefreshItems(...)` 只调用 `SwitchBubble(false)`，不会刷新奖励 item。
- 顶部注释已说明点击切换 bubble、显示时使用 `DOScale 0 -> 1`、隐藏时直接 `scale = 0`、刷新时先隐藏 bubble 再刷新奖励内容。

## 当前行为

- `Awake()` 只绑定 `clickBtn.OnClick`，不再注入假奖励数据。
- 点击宝箱会在显示/隐藏之间切换 bubble。
- 显示 bubble 时先置为 `scale = 0`，再使用 DOTween 缩放到 `Vector3.one`；`bubbleWaitingTime` 大于 0 时会延迟自动隐藏。
- 隐藏 bubble 时直接将 `localScale` 设为 `Vector3.zero`。
- `RefreshItems(...)` 会先隐藏 bubble，再按 `rewardItems` 现有列表槽位依次显示奖励；Coin / Star 按 `CommonGetRewardPopup` 的 `coinSprite` / `starSprite` 口径取图，Skin 按 `SkinUtils.ResolveIconLocation(...)` / `LoadIconAsync(...)` 异步加载真实皮肤图标；多余槽位会隐藏，列表为空时安全跳过。
- `RefreshProgress(current, max, isImmediate)` 会把 `current` 限制在 `0..max`，`max <= 0` 时显示 `0/0` 并把进度归零。
- `progressTxt` 显示为 `当前/最大`；`progressBar.fillAmount` 按比例刷新，`isImmediate=true` 或对象未激活时立即设置，否则用 0.2 秒 `DOFillAmount` 过渡。
- 面板 `OnDisable()` 和 `OnDestroy()` 会清理进度 tween，避免关闭弹窗后继续回写进度条。
- `PostcardPopup` 每次切换未完成普通配置页或未解锁占位页时，会先 `Show()` Chest 再按当前配置刷新奖励和进度；解锁动画结束后也会再次刷新 Chest 进度。
- `PostcardPopup` 刷新到已经 complete 的普通配置页时，会直接 `Hide()` Chest，不再显示奖励预览、bubble 或进度条；这与整页完成领奖流程结束后隐藏 Chest 的口径保持一致。
- 当前页整页完成并进入领奖流程时，`PostcardPopup` 会隐藏 Chest；如果奖励弹窗关闭后自动切到下一页，下一页刷新会重新显示 Chest。
- `Postcard_Chest.Show()` 会把宝箱 Spine 刷回 `box_idle`，避免上一页完成后的打开待机状态残留到下一页或重新打开流程。
- `PlayChestOpenAnim()` 会先隐藏奖励 bubble，再播放 `box-open`；动画完成后自动切到 `box-open_idle`。Chest 层级未激活、缺少 `UISpineCtrl` 或播放失败时返回 `null`，由 `PostcardPopup` 降级走原有短等待流程。
- `PostcardPopup` 在最后一个进度 item 稳定后、整页完成入口前强制显示 Chest，并刷新奖励和 `max/max` 进度；页面完成表现结束后调用 `PlayChestOpenAnim()`。如果获得有效 `TrackEntry`，优先等待 `Complete` 或 `End`，正常完成后立即继续隐藏 Chest 与领奖流程；超过动画时长兜底窗口则记录 warning 后继续。

## 数据与流程

Chest 自身只消费外部传入的奖励字典和 `current/max` 进度，不写入背包、不写入 `DB.Postcard`、不派发事件。奖励图标不再从 `CommonRewardItem` 子节点当前 `Image.sprite` 反向获取；Coin / Star 使用 Chest 自身序列化图标字段，Skin 使用当前奖励参数解析 `Config.Skin` 后异步加载图标。真实数据由 `PostcardPopup` 从当前 `PostcardConfig.Rewards`、`PostcardConfig.UnlockProgress` 和 `DB.Postcard.CompletedId` / `CurrentCollectedPuzzledId` 计算后传入。

宝箱开启动画资源流只使用 `PostcardPopup.prefab` 中已存在的 `SkeletonGraphic` / `UISpineCtrl` 和 `sp_box-open_002` Spine 资源。Skin 奖励预览图标复用 `CommonGetRewardPopup` 已使用的 `SkinUtils` 路径解析和异步加载口径；`Postcard_Chest` 不运行时实例化新奖励 item，也不新增全局事件、RemoteConfig、广告、IAP、analytics 或平台 SDK 链路。

## 验证记录

- `git diff --check -- Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs Assets/Game/UI/Postcard/Scripts/PostcardPopup功能说明.md Assets/Game/UI/Postcard/Scripts/PostcardPopup风险评估.md Assets/Game/UI/Postcard/Scripts/Postcard_PageItem功能说明.md Assets/Game/UI/Postcard/Scripts/Postcard_Chest功能说明.md Assets/Game/UI/Postcard/Scripts/Postcard_Chest风险评估.md DevelopMD/已有功能设计与使用记录.md` 通过，仅提示工作区后续可能按 Git 设置进行 LF/CRLF 转换。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 97 warnings / 0 errors。
- 2026-07-09 静态检查确认 `sp_box-open_002.json` 中存在 `box-open`、`box-open_idle`、`box_idle` 三个动画名；`PostcardPopup.prefab` 的 `Postcard_Chest` 子级存在 `SkeletonGraphic`、`UISpineCtrl` 和 `sp_box-open_002_SkeletonData` 绑定。
- 2026-07-09 静态检查确认 `PostcardPopup_PageCompleteProcess.cs` 中 `pageCompleteStepDelay` 与 `chestOpenCompleteExtraDelay` 已暴露为 Inspector 序列化字段，默认分别为 0.5 秒和 1 秒，小于 0 时按 0 秒处理。
- 2026-07-09 `git diff --check -- Assets/Game/UI/Postcard/Scripts/Postcard_Chest.cs Assets/Game/UI/Postcard/Scripts/PostcardPopup_PageCompleteProcess.cs`：通过，仅有工作区 LF/CRLF 转换提示。
- 2026-07-09 静态敏感扫描 `Postcard_Chest.cs` 和 `PostcardPopup_PageCompleteProcess.cs` 未命中编辑器生命周期、每帧轮询、全局事件、RemoteConfig、广告、IAP、analytics 或 PlayerPrefs 关键路径。
- 2026-07-09 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：未通过，首个错误为缺失 `Temp\bin\Debug\Apple.Core.dll`，共 191 个 `CS0006` 元数据缺失错误，未获得本次业务脚本编译诊断。
- 2026-07-09 追加 Inspector 等待参数后重跑 `git diff --check`：通过，仅有 LF/CRLF 转换提示；重跑 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，99 warnings / 0 errors。
- 2026-07-09 参考 `CommonGetRewardPopup` 修正 Chest 奖励图标来源后，`git diff --check -- Assets/Game/UI/Postcard/Scripts/Postcard_Chest.cs Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs Assets/Game/UI/Postcard/Resources/PostcardPopup.prefab Assets/Game/UI/Postcard/Scripts/Postcard_Chest功能说明.md Assets/Game/UI/Postcard/Scripts/Postcard_Chest风险评估.md Assets/Game/UI/Postcard/Scripts/PostcardPopup功能说明.md Assets/Game/UI/Postcard/Scripts/PostcardPopup风险评估.md DevelopMD/已有功能设计与使用记录.md` 通过，仅有 LF/CRLF 提示；`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，0 warnings / 0 errors；旧占位/旧口径扫描未发现无效 `RefreshSkin(,` / `Refresh(,` 或 `GetCurrentRewardSprite`。
- 2026-07-09 complete page 隐藏 Chest：`PostcardPopup.RefreshChest(...)` 当前会在已完成普通页直接调用 `Postcard_Chest.Hide()`；目标文件 `git diff --check` 通过，仅有 LF/CRLF 提示；本次 `PostcardPopup.cs` diff 敏感关键字扫描无命中；`dotnet build .\Assembly-CSharp.csproj --no-restore ...` 通过，98 warnings / 0 errors；Unity PlayMode 尚未验证。

## 剩余风险

- 未在 Unity PlayMode 中确认 `PostcardPopup` 切换不同 config page 时 Chest 奖励和进度是否都即时刷新。
- 未在 Unity PlayMode 中确认整页完成后 Chest 隐藏、领奖弹窗关闭后下一页重新显示 Chest 的完整表现。
- 未在 Unity PlayMode 中确认已经 complete 的页面重新打开或切回时 Chest 保持隐藏。
- 未在 Unity PlayMode 中确认 bubble 缩放表现、自动隐藏时机和 prefab 绑定是否完全符合预期。
- 当前只复用 prefab 中已绑定的 `rewardItems` 槽位，不动态生成新 item；真实多奖励数量超过槽位时仍需后续确认表现。
- Chest Skin 图标依赖 `ConfigManager.Tables.TbSkin`、`SkinUtils.ResolveIconLocation(...)` 和资源系统可加载对应 icon；配置无效或资源缺失时该槽位会保持空图标，仍需 PlayMode 验证。
- 未在 Unity PlayMode 中确认 `progressBar`、`progressTxt` 的 prefab 绑定、进度动画和边界值显示是否符合美术预期。
- 未在 Unity PlayMode 中确认整页 Complete 后 `box-open` 是否完整播放、是否准确切到 `box-open_idle`、`chestOpenCompleteExtraDelay` 停留是否符合预期、随后隐藏 Chest 并继续打开领奖弹窗。
- 未在 Unity PlayMode 中确认 Coin / Star sprite 绑定后的 Chest 预览显示、Skin icon 异步加载成功/失败回退和快速切页版本保护。
- 未在 Android/iOS 横屏设备验证宝箱开启动画位置、缩放、遮挡和触摸锁期间的 UI 栈表现。

## 回滚路径

回滚 `Postcard_Chest.cs` 中 DOTween 显隐、按钮绑定、rewardItem 刷新逻辑和 `RefreshProgress(...)` 进度刷新逻辑，并移除 `PostcardPopup` 中调用 Chest 刷新的逻辑，即可恢复到不控制 Chest 的状态。

若只回滚 2026-07-09 宝箱开启动画，恢复 `Postcard_Chest.PlayChestOpenAnim()` 为空实现或移除 `PostcardPopup_PageCompleteProcess.HandlePageCompleteAsync()` 中等待 `PlayChestOpenAnim()` 的调用，即可回到整页完成后短等待、隐藏 Chest、再进入领奖流程的旧顺序；若只回滚 Inspector 暴露等待参数，则把 `pageCompleteStepDelay` / `chestOpenCompleteExtraDelay` 恢复为局部常量并移除序列化字段。

## 2026-07-10 首次完成开箱稳定性修复

- 修复前 `CompletedId` 已写入后调用普通 Chest 刷新，会把首次完成所需的 Chest 当作历史完成页隐藏；inactive Spine 可能返回非空但不推进的 `TrackEntry`，导致 Popup 永久等待。
- 修复后首次完成由 Popup 专用步骤先 `Show()` Chest、刷新奖励并显示 `max/max`，再调用本组件播放 `box-open`；历史完成页普通浏览继续隐藏 Chest。
- 本组件新增 inactive 层级保护；Popup 同时增加基于动画时长的 unscaled timeout，因此资源或回调异常不再无限阻断领奖。
- 本次未改 prefab、Spine 资源、奖励数据、DB/save、事件或平台链路。`Assembly-CSharp.csproj` 编译通过，98 warnings / 0 errors；Unity PlayMode 与 Android/iOS 横屏仍待补验。
- 若只回滚本次修复，移除 inactive 检查，并恢复 Popup 的普通 Chest 刷新与无超时等待；无需迁移数据或资源，但会重新暴露完成流程软锁风险。

## 2026-07-13 Complete 流程 Chest 缩放隐藏

- 需求：整页 complete 开箱表现结束后，Chest 不再立即消失，而是通过 DOTween 缩放到 `Vector3.zero` 后隐藏；其它默认隐藏场景继续立即隐藏。
- 影响文件为 `Postcard_Chest.cs` 与 `PostcardPopup_PageCompleteProcess.cs`；不修改 prefab、Spine、奖励配置、DB/save、资源路径、全局事件或平台 SDK。
- `Postcard_Chest.Hide(bool playScaleAnimation = false)` 默认保持原立即隐藏行为；complete 流程唯一传入 true，使用 0.2 秒、`Ease.InQuad`、unscaled time 把 Chest 根节点缩到 0，完成后设为 inactive。
- `Show()` 会停止未完成隐藏 Tween 并恢复根节点 `localScale=Vector3.one`；`OnDisable()` / `OnDestroy()` 同样清理 Tween，避免切页、关闭 Popup 或重复显示后残留零缩放。
- C# 构建通过，98 warnings / 0 errors；目标代码 `git diff --check` 通过。Unity PlayMode 的开箱停留、0.2 秒缩小、领奖弹窗衔接、快速切页/关闭，以及 Android/iOS 横屏表现仍待验证。
- 回滚时恢复无参数 `Hide()` 直接 `SetActive(false)`，删除隐藏 Tween 字段/清理逻辑，并把 complete 流程恢复为 `chestView?.Hide()`；无需数据或资源迁移。

## 2026-07-14 Chest 隐藏动画参数开放到 Inspector

- 需求背景：Complete 流程的 Chest 缩放隐藏时长和缓动原先写死在代码中，无法由策划或美术在 `PostcardPopup` prefab Inspector 中直接调节。
- 影响文件：仅修改 `Postcard_Chest.cs`；不修改 Spine 动画、奖励配置、DB/save、资源路径、全局事件、分析打点或平台 SDK。
- 原有行为：隐藏持续时间使用代码常量；缓动类型直接写在 `SetEase(...)` 调用中，调整必须修改脚本并重新编译。
- 当前行为：新增序列化字段 `chestHideScaleDuration` 和 `chestHideEase`，当前代码默认分别为用户调整后的 `1` 秒与 `Ease.Linear`；字段只影响 `Hide(true)`，普通 `Hide()` 继续立即隐藏。
- 逻辑复核发现 `OnPagePlayAnim(false)` 原先会无条件刷新 Chest；如果当前配置已写入 `CompletedId`，该刷新会进入默认 `Hide()` 并立即隐藏。现在该入口只刷新未完成页，首次完成后的 Chest 只允许由 `HandlePageCompleteAsync()` 调用 `Hide(true)`；历史完成页仍在页面加载刷新时立即隐藏。
- 安全边界：时长通过 Inspector 的 `Min(0)` 和运行时 `Mathf.Max(0, ...)` 双重限制；不新增运行时实例、事件或数据写入。
- 验证：待完成 C# 构建、目标 diff 检查和静态敏感项扫描；Unity Inspector 显示、首次完成时是否只有一次 `Hide(true)`、PlayMode 动画节奏及 Android/iOS 横屏表现仍待验证。
- 回滚：删除两个序列化字段，恢复时长常量和 `SetEase(...)` 固定参数即可；无需迁移数据或资源。

## 2026-07-14 Root Animator 与 Chest 缩放冲突修复

- 问题证据：`PostcardPopup` 的 `Root Animator` 使用 `Postcard_Root.controller`，进场动画 `postcard in` 含 `Postcard_Chest.m_LocalScale` 曲线；它与 `Postcard_Chest.Hide(true)` 的 `DOScale` 同时写入同一个 Transform，导致隐藏 Tween 的缩小结果被 Animator 覆盖。
- 当前处理：进场动画播放结束后以零过渡切入 `postcard_idle`；该 idle Clip 不含任何动画曲线，并关闭 idle 状态的 `Write Defaults`，使 Animator 在 idle 阶段不再写入 Chest 的缩放属性。
- 保留边界：`postcard in` 的进场曲线和原有 `Write Defaults` 未改动；脚本侧未增加 LateUpdate 抢写或禁用整个 Root Animator，避免影响 Popup 其它进场轨道。
- 影响文件：`Postcard_Root.controller`、新增空 Clip `postcard_idle.anim` 及其 meta；不修改 DB/save、奖励、资源加载路径、全局事件或平台 SDK。
- 验证：静态资产确认 idle Clip 的曲线和 binding 列表均为空，转场 duration 为 0，idle 的 `Write Defaults` 为关闭；Unity PlayMode 中进场结束后的 Chest 缩小、快速完成边界及 Android/iOS 横屏仍待验证。
- 回滚：移除 `postcard in -> postcard_idle` 转场和 idle 状态/Clip，恢复原 Controller；此操作会重新暴露 Animator 与 Chest Tween 的属性争用。

## 2026-07-14 PlayChestOpenAnim 异步链优化

- 需求背景：开箱特效需要在 `box-open` 启动 0.5 秒后触发；原尝试把 `PlayChestOpenAnim()` 改为 `async Task<TrackEntry>`，并在完成流程中通过 `.Result` 取返回值，会在 Unity 主线程同步阻塞等待。
- 影响文件：`Postcard_Chest.cs` 的开箱表现和 `PostcardPopup_PageCompleteProcess.cs` 的调用表达式；不修改 prefab、Spine 资源、Animator Controller、奖励配置、DB/save、资源地址、全局事件、分析打点或平台 SDK。
- 当前行为：`PlayChestOpenAnim()` 保持同步返回 `Spine.TrackEntry`，调用方可立即进入既有动画完成/超时等待；本地 `UniTask` 独立等待 0.5 秒后触发 `openEffect` 的 `play` 参数，不阻塞主线程和领奖流程。
- 生命周期与重复调用：完成回调在启动延迟任务前立即订阅；`Show()`、重复开箱、`OnDisable()` 和 `OnDestroy()` 会解除旧 `TrackEntry.Complete` 回调并递增版本，旧延迟任务恢复后只会退出，不会触发过期特效或把新动画切回旧状态。
- 数据与 UI 流：本次只调整 Chest 本地 Spine/Animator 表现时序；`WaitChestOpenAnimAsync(...)`、奖励发放、页面刷新、Skin 转换、存档和导航顺序保持不变。
- 验证：目标代码 `git diff --check` 通过，仅有 LF/CRLF 转换提示；敏感项扫描未命中 `.Result`、编辑器生命周期、全局事件、RemoteConfig、PlayerPrefs 或直接 `Debug.Log`；`Assembly-CSharp.csproj` 构建通过，98 warnings / 0 errors。
- 未验证：Unity PlayMode 中 0.5 秒特效落点、重复触发、快速关闭/重开及 Android/iOS 横屏实际视觉仍待验证。
- 回滚：删除开箱版本号、当前 TrackEntry 字段和两个私有辅助方法，把 `openEffect.SetTrigger("play")` 恢复到完成回调；无需迁移数据或资源。

## 2026-07-14 满进度显示前移、开箱时点保持

- `PostcardPopup` 现在在 `completeEffects` 开始阶段调用既有 `Postcard_Chest.Show()`、`RefreshItems(...)` 和 `RefreshProgress(max, max, false)`，因此满进度 Chest 会与完成粒子同阶段出现。
- `Postcard_Chest.PlayChestOpenAnim()` 的代码和调用顺序未改，仍在粒子等待、页面 `transform` 和正文逐字结束后执行；之后的开箱等待、缩放隐藏和领奖流程保持原样。
- 本次不修改 `Postcard_Chest.cs`、prefab、Spine/Animator、奖励配置、DB/save、资源或事件。C# 构建通过（99 warnings / 0 errors）；Unity PlayMode 与移动端横屏视觉仍待验证。
- 回滚 Popup 中的 Chest 前移准备和缓存即可恢复原显示时点，Chest 模块无需迁移。

## 2026-07-14 满进度 Chest 不再等待 CompleteEffects 结束

- 满进度 Chest 仍在 `completeEffects` 发射阶段显示，但 Popup 在调用全部粒子 `Play(true)` 后立即进入 transform，不再等待粒子结束或执行粒子后的额外停留。
- `Postcard_Chest.PlayChestOpenAnim()` 自身代码未改；当前会在既有 transform/正文 1.7 秒窗口后启动，粒子允许与页面转换和开箱表现重叠。
- 本次不修改 `Postcard_Chest.cs`、prefab、Spine/Animator、奖励配置、DB/save、资源或事件。C# 构建通过（0 warnings / 0 errors）；实际重叠观感和移动端横屏仍待验证。
- 回滚 Popup 的非阻塞粒子入口即可恢复等待，Chest 模块无需迁移。

## 2026-07-14 满进度显示早于整页完成、开箱无额外停留

- `Postcard_Chest` 自身接口未改；Popup 在最后一个进度 item 动画稳定后、触发 `OnPageComplete()` 前调用既有 `Show()`、`RefreshItems(...)` 和 `RefreshProgress(max, max, false)`。
- grayImage 与 `completeEffects` 不再负责显示 Chest；页面转换和正文结束后仍调用既有 `PlayChestOpenAnim()`。
- Popup 继续等待 `box-open` 的 Complete/End 或超时保护，但正常完成后立即调用既有缩放隐藏，不再额外等待 1 秒；`PostcardPopup.prefab` 已删除废弃的 `chestOpenCompleteExtraDelay` 值。
- 不修改 Chest Spine/Animator、奖励配置、DB/save、资源加载、全局事件或平台 SDK。构建通过（99 warnings / 0 errors）；Unity PlayMode 和移动端横屏视觉仍待验证。
- 回滚 Popup 的准备点并恢复额外等待字段即可，Chest 组件无需数据或资源迁移。
