# Postcard_GuidePart 功能说明

## 需求背景

2026-07-07 需求：根据 `Postcard_GuidePart.cs` 顶部注释，实现明信片弹窗内的局部引导层。引导只在 `DB.Postcard.IsGuiding=true` 的明信片主弹窗打开后出现，按当前目标下标只允许点击一个 guide item，并通过真实 `Postcard_PageItem` 的解锁流程推进；所有有效目标解锁后关闭自身。原有注释内容未改写。

## 影响范围

- 修改脚本：`Assets/Game/UI/Postcard/Scripts/Postcard_GuidePart.cs`
- 修改脚本：`Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs`
- 修改脚本：`Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs`
- 修改 prefab：`Assets/Game/UI/Postcard/Resources/PostcardPopup.prefab`
- 修改 prefab：`Assets/Game/UI/Postcard/Resources/Postcard_ems.prefab`
- 复用 prefab：`Assets/Game/UI/Postcard/Prefabs/PostcardPopup_GuidePart.prefab`
- 相关存档字段：`DB.Postcard.HasGuide`、`DB.Postcard.IsGuiding`、`DB.Postcard.CompletedId`、`DB.Postcard.CurrentCollectedPuzzledId`

本次未新增 DB 字段、PlayerPrefs key、Luban 配置、RemoteConfig、Resources 路径、全局事件、广告、IAP、analytics 或平台 SDK 路径。

## 原有行为基线

- `NormalGuidePopup` 完成后会打开 `PostcardPopup`，并在打开/关闭阶段写入已有 `HasGuide` / `IsGuiding`。
- `PostcardPopup` 已有 `guidePart` 序列化字段，但主 prefab 静态 YAML 中该字段原先未绑定。
- `Postcard_GuidePart` 原脚本只有序列化字段和注释，没有启动、点击、指示手指或清理逻辑。
- `Postcard_PageItem` 已负责 item 点击、星星数量校验、`DB.SavePostcard()`、扣星、解锁动画、整页完成和奖励流程回调。

## 当前行为

- `PostcardPopup.OnOpen()` 和切页清理时会先停止当前 guide，避免旧引导覆盖新页面。
- 普通明信片页完成 `Postcard_PageItem.OnShow()` 后，`PostcardPopup` 读取当前 `DB.Postcard.IsGuiding`；只有该值为 true 且当前不是 `Postcard_unlock` 占位页时才尝试启动 guide。
- `PostcardPopup` 启动 guide 后会把 `IsGuiding=false` 并调用 `DB.SavePostcard()`。保存失败只记录 warning，不阻断弹窗；这避免 stale guide state 在同一运行时反复打开。
- `Postcard_GuidePart.TryStartGuide(...)` 只在 `Application.isPlaying` 下工作，传入当前真实 `Postcard_PageItem`，解析本地 guide item 列表，锁定真实页面的普通 item 点击入口，并显示第一个有效且未解锁的目标。
- Guide 启动、目标查询和解锁失败降级刷新时，会通过 `Postcard_PageItem.TryGetGuideProgressViewStateByListIndex(...)` 读取真实页面对应 item 的 `isUnlock`、`unlockCount` 和 `currentStar`，再刷新 guide 层 `Postcard_ProgressItem` 的 cost 文本、红点和遮罩状态。
- 引导期间，真实页面 item 的普通点击会被 `Postcard_PageItem.SetGuideInteractionLocked(true)` 拦截；只有 active guide item 的 `CustomButton` 可交互。
- active guide item 点击后，`Postcard_GuidePart` 调用 `Postcard_PageItem.TryUnlockGuidedItemByListIndex(...)`，后者继续复用原 `TryUnlockItem(...)`，不复制扣星、存档、回滚、动画或整页完成逻辑。
- 星星不足、目标已解锁、DB 缺失、保存失败或页面动画中时，真实页面解锁入口返回 false，引导保持在当前目标，不前进。
- 解锁被接受后，真实页面 item 和 guide 层 item 会分别播放 `Postcard_ProgressItem.PlayUnlockAnim(...)`；guide 同时等待真实页面稳定回调和 guide 可视动画完成，再跳到下一个有效未解锁目标。若后续目标已经解锁或无有效目标，guide 关闭并释放交互锁。
- `Postcard_ems.prefab` 的 5 个真实 `Postcard_ProgressItem.index` 已按当前配置和 guide 顺序修正为 `0..4`，避免所有目标都被当成 puzzle `0` 后首点即整页完成。
- `finger` 会在 guide 启动和 active index 改变时，把 active guide item 的 RectTransform 中心转换到 finger 父节点本地坐标并写入 `anchoredPosition`；不使用 `Update` 或每帧轮询。
- 2026-07-09 追加：`fingerPs` 由 `Postcard_GuidePart` 在 finger 显示且当前目标有效时启动协程，先延迟 0.5 秒，再每 1 秒执行一次 `Stop(...StopEmittingAndClear)` / `Play(true)`，形成循环点击提示粒子；finger 隐藏、目标无效、引导停止、组件禁用或销毁时停止并清空粒子。
- 若 `fingerPs` 未手动绑定，运行时会尝试从 `finger` 子节点查找 `ParticleSystem`；若仍未找到则自动跳过，不影响 guide item 点击、解锁、存档或 UI 刷新流程。若粒子不是 `finger` 子节点，播放前会同步到 `finger` 的世界坐标。
- `finger` 或目标 RectTransform 缺失时降级隐藏 finger，不阻断正常弹窗。
- `OnDisable()`、`OnDestroy()` 和 `StopGuide()` 会解除 guide item 点击订阅、释放真实页面交互锁、隐藏 finger，并清理完成回调。若解锁动画稳定回调在 guide 已停止后返回，会被状态保护拦截，不再推进旧 guide。
- `PostcardPopup.prefab` 已嵌套 `PostcardPopup_GuidePart.prefab`，`guidePart` 指向该嵌套实例的 `Postcard_GuidePart` 组件。

## 数据、UI 与事件流

1. `NormalGuidePopup` 记录 `HasGuide=true`、`IsGuiding=true` 并打开 `PostcardPopup`。
2. `PostcardPopup` 加载普通明信片页，调用 `_currentPage.OnShow()` 完成页面 item 刷新和按钮绑定。
3. `PostcardPopup.TryStartPendingPostcardGuide(false)` 读取 `DB.Postcard.IsGuiding`。
4. 若 `guidePart` 缺失，Popup 记录 warning 并消费 `IsGuiding=false`，避免正常明信片弹窗被 pending guide state 卡住。
5. 若 `guidePart` 存在，Popup 调用 `guide.TryStartGuide(_currentPage, OnPostcardGuideCompleted)`。
6. Guide 解析序列化 guide item 列表，并按列表下标与当前真实页面运行时 item 列表对应。
7. Guide 通过真实 `Postcard_PageItem` 读取每个目标的 cost、当前星星数和解锁状态，刷新 guide 层 item 的本地 UI 表现。
8. Guide 锁定真实页面普通点击，只启用当前 active guide item 的按钮。
9. 玩家点击 active guide item 后，Guide 请求 `Postcard_PageItem.TryUnlockGuidedItemByListIndex(index, callback)`。
10. `Postcard_PageItem` 继续执行原 star 校验、存档、回滚、扣星、`Postcard_ProgressItem.PlayUnlockAnim(...)` 和整页完成逻辑。
11. Guide 层当前 item 同步播放本地 `PlayUnlockAnim(...)`，保证遮罩层上的玩家可见 item 也有解锁表现。
12. accepted unlock 的页面稳定回调和 guide 本地动画都完成后，Guide 跳到下一个有效目标，或关闭自身并回调 Popup 刷新按钮状态。
13. 当前目标 finger 显示后，Guide 启动 `fingerPs` 本地协程，延迟 0.5 秒后首次播放，之后每 1 秒重播一次粒子；Guide 隐藏或停止时结束协程并清空粒子。

## 验证记录

- 2026-07-07 `git show HEAD:BrainPunk/Assets/Game/UI/Postcard/Scripts/Postcard_GuidePart.cs` 与当前 diff 对比：原 `Postcard_GuidePart` 注释块未改写。
- 2026-07-07 checklist：`specs/035-postcard-guide-part/checklists/requirements.md` 为 16/16 PASS。
- 2026-07-07 `git diff --check -- BrainPunk/Assets/Game/UI/Postcard/Scripts/Postcard_GuidePart.cs BrainPunk/Assets/Game/UI/Postcard/Scripts/PostcardPopup.cs BrainPunk/Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs BrainPunk/Assets/Game/UI/Postcard/Resources/PostcardPopup.prefab`：通过，仅有 LF/CRLF 提示。
- 2026-07-07 静态敏感扫描未命中：`OnValidate`、`Reset(`、`ExecuteAlways`、`ExecuteInEditMode`、`Update(`、`LateUpdate(`、`FixedUpdate(`、`PlayEvents`、`DispatchEvent`、`Subscribe<`、`RemoteConfig`、`IAP`、`Ads`、`Analytics`、`PlayerPrefs`。
- 2026-07-07 prefab 静态检查确认 `PostcardPopup.prefab` 的 `guidePart` 指向嵌套 guide prefab stripped component；`PostcardPopup_GuidePart.prefab` 已序列化 5 个 guide items 和 `finger`。
- 2026-07-07 `dotnet build .\Assembly-CSharp.csproj ...`：无法执行，当前 worktree 没有 `.csproj` / `.sln`。
- 2026-07-07 Unity 6000.0.59f2 batchmode 编译尝试：项目编译被既有依赖缺失阻断，首批错误来自 `Assets/Guru/Gen/GuruAllSpecs.G.cs` 缺失 Guru SDK 类型，以及 `Assets/BearGame/com.bear.ui@919f05cf0418/Runtime/Animation/UIScaleAnimation.cs` 缺失 `DG.Tweening` / `Tween`；日志未出现本次 touched Postcard 文件错误。
- 2026-07-08 静态排查确认 `Postcard_ems.prefab` 修复前 5 个真实 `Postcard_ProgressItem.index` 均为 `0`，而 `tbpostcardconfig.json` 的 `Postcard_ems.unlockProgress` 为 `0..4`；这会让首个 guided unlock 后 `IsAllProgressCollected(...)` 判断整页完成。
- 2026-07-08 已将 `Postcard_ems.prefab` 真实页面 item index 修正为 `0..4`，顺序与 `PostcardPopup_GuidePart.prefab` 的 guide items 列表一致。
- 2026-07-08 确认 `Postcard_GuidePart` 修复前没有读取真实页面 `unlockProgress` cost，只在已解锁跳过时调用 `guideItem.Refresh(true, 0, 0)`；现已改为通过 `Postcard_PageItem.TryGetGuideProgressViewStateByListIndex(...)` 同步真实页面 cost/currentStar/isUnlock。
- 2026-07-08 `git diff --check -- Assets/Game/UI/Postcard/Scripts/Postcard_GuidePart.cs Assets/Game/UI/Postcard/Scripts/Postcard_PageItem.cs Assets/Game/UI/Postcard/Resources/Postcard_ems.prefab Assets/Game/UI/Postcard/Scripts/Postcard_GuidePart功能说明.md Assets/Game/UI/Postcard/Scripts/Postcard_GuidePart风险评估.md DevelopMD/已有功能设计与使用记录.md`：通过，仅有 Git LF/CRLF 提示。
- 2026-07-08 静态敏感扫描 `Postcard_GuidePart.cs` 和 `Postcard_PageItem.cs` 未命中 `OnValidate`、`Reset(`、`ExecuteAlways`、`ExecuteInEditMode`、`Update(`、`LateUpdate(`、`FixedUpdate(`、`PlayEvents`、`DispatchEvent`、`Subscribe<`、`RemoteConfig`、`IAP`、`Ads`、`Analytics`、`PlayerPrefs`。
- 2026-07-08 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 未通过，首个错误为缺失 `Temp\bin\Debug\Apple.Core.dll`，共 191 个 `CS0006` 元数据缺失错误，未获得本次业务脚本编译诊断。
- 2026-07-09 `rg -n "OnValidate|Reset\(|ExecuteAlways|ExecuteInEditMode|Update\(|LateUpdate\(|FixedUpdate\(|PlayEvents|DispatchEvent|Subscribe<|RemoteConfig|IAP|Ads|Analytics|PlayerPrefs|DB\.|SavePostcard|Instantiate|Resources\.Load" Assets/Game/UI/Postcard/Scripts/Postcard_GuidePart.cs` 未命中，确认本次 `fingerPs` 循环未新增编辑器生命周期、每帧轮询、全局事件、存档、资源加载或平台链路。
- 2026-07-09 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 99 warnings / 0 errors。
- 2026-07-09 静态 prefab 搜索只在脚本中命中 `fingerPs`，`PostcardPopup_GuidePart.prefab` 与 `PostcardPopup.prefab` 未出现 `fingerPs` 绑定项；实际粒子表现仍需在 Unity Inspector 绑定后验证。
- 2026-07-09 `git diff --check -- Assets/Game/UI/Postcard/Scripts/Postcard_GuidePart.cs Assets/Game/UI/Postcard/Scripts/Postcard_GuidePart功能说明.md Assets/Game/UI/Postcard/Scripts/Postcard_GuidePart风险评估.md DevelopMD/已有功能设计与使用记录.md` 通过，仅有 Git LF/CRLF 提示。
- 2026-07-09 按需求将 `fingerPs` 调整为延迟 0.5 秒、间隔 1 秒后，重新执行上述敏感扫描、`dotnet build` 和 `git diff --check`；结果仍为敏感扫描无命中、编译 99 warnings / 0 errors、diff check 仅 LF/CRLF 提示。

## 剩余风险与未验证项

- 未运行 Unity PlayMode 验证 pending guide 打开、非 guiding baseline、guided unlock 成功/失败、close/reopen 和整页完成奖励流程。
- 未运行 Android/iOS 横屏 smoke，finger 对齐、触摸命中和关闭重开后的输入恢复仍需真机或等效设备验证。
- 当前实现仍使用序列化 `items` 列表下标与真实页面 item 列表下标对应；`Postcard_ems.prefab` 与 guide prefab 已静态对齐，其它主题页的视觉顺序和 `Index` 顺序仍需 Unity Inspector 或 PlayMode 确认。
- 本次 `dotnet build` 已通过，但未运行 Unity Editor PlayMode 或 batchmode compile，无法证明 Inspector 绑定、Prefab 序列化和运行时 UI 粒子层级均正确。
- 缺失 `guidePart`、缺失 `finger`、guide item 数量不匹配、保存失败等路径当前只完成静态保护，仍需 PlayMode 手动验证。
- `fingerPs` 的 prefab 绑定、实际延迟 0.5 秒且间隔 1 秒循环的视觉、非 finger 子节点时的位置同步、Android/iOS 横屏粒子层级和遮挡关系仍待 PlayMode 或设备验证。

## 回滚方式

- 从 `PostcardPopup.cs` 移除 `TryStartPendingPostcardGuide(...)` 调用和 `Postcard Guide` region，可关闭弹窗内引导入口。
- 从 `PostcardPopup.prefab` 解绑或移除嵌套 `PostcardPopup_GuidePart`，可让 pending guide state 走缺失绑定降级路径。
- 从 `Postcard_PageItem.cs` 移除 guide 专用 public API 和 `_isGuideInteractionLocked`，即可恢复普通 item 点击不受 guide 层影响。
- 本次未新增存档字段或配置，回滚不需要数据迁移。

## 2026-07-13：Finger 控制迁移到 GuideFinger

### 需求与影响范围

- 需求：将 `Postcard_GuidePart` 中关于 finger 的控制迁移到 `GuideFinger`，重点收口点击提示粒子的循环播放与清理。
- 修改脚本：`Postcard_GuidePart.cs`、`GuideFinger.cs`。
- 修改资源：`PostcardPopup_GuidePart.prefab` 的字段结构，以及 `PostcardPopup.prefab` 中 GuidePart、GuideFinger、finger RectTransform 和点击粒子的序列化绑定。
- 未修改解锁校验、扣星、DB/save、配置、资源加载、全局事件、analytics、广告、IAP 或平台 SDK 链路。

### 行为基线与当前行为

- 迁移前：目标中心换算、finger 显隐、粒子位置同步、延迟 0.5 秒首次播放、每 1 秒重播及停止清理都位于 `Postcard_GuidePart`；工作区中的迁移中间态已删除部分字段和启动方法，但仍保留旧调用，无法通过编译。
- 迁移后：`Postcard_GuidePart` 只解析当前 guide item 的 `RectTransform`，每个目标通过 `GuideFinger.Create(target, root, callback)` 在 GuidePart 根节点下创建独立手指，并在停止或切换时调用 `Close()`。
- `GuideFinger` 负责把 finger 移到目标中心、控制显隐、同步非子节点粒子位置、维护单个协程、延迟 0.5 秒首次播放并每 1 秒执行一次 `Stop(...Clear)` 后 `Play(true)`。
- 目标切换时复用已有粒子协程并立即同步位置；引导停止、完成、组件禁用或销毁时停止协程、清空粒子并隐藏 finger。
- 所有公开入口和 Unity 生命周期入口都在运行态生效，不新增 `Update` 或其他每帧轮询。
- 独立 `Postcard_GuideFinger.prefab` 负责 `finger`、点击粒子和 `ClickArea` 绑定；GuidePart 不再持有主 prefab 内的 `guideFinger` 序列化引用。

### 验证、风险与回滚

- CodeGraph 确认表现调用链为 `Postcard_GuidePart.RefreshFingerPosition()` → `GuideFinger.Create()` → `ShowAt()` → `StartParticleLoop()` → `PlayParticleLoop()`。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，98 warnings / 0 errors。
- 静态扫描未命中编辑器生命周期、`Update`/`LateUpdate`/`FixedUpdate`、全局事件、DB/save、资源加载或运行时实例化；GuidePart 内旧 finger 粒子字段、常量、协程和辅助方法均已移除。
- 静态 prefab 检查确认 GuideFinger 组件、finger RectTransform、粒子和 GuidePart stripped component 的 fileID 均存在且被引用。
- 未执行 Unity PlayMode 和 Android/iOS 横屏 smoke；实际 0.5 秒/1 秒节奏、Canvas 粒子层级、目标切换位置、关闭重开和销毁清理仍待运行时确认。
- 回滚：恢复 GuidePart 内的直接 item 点击绑定和旧 GuideFinger 引用，移除按目标创建独立 Finger 的调用；不需要数据迁移。

## 2026-07-13：独立 GuideFinger 目标与点击收尾

### 需求与影响范围

- `GuideFinger.Create(target, root, callback)` 在指定 UI 根节点下创建独立手指后，立即调用 `ShowAt(target)`，把 finger 定位到目标 RectTransform 的中心。
- `Postcard_GuideFinger.prefab` 的全屏透明 `ClickArea` 新增 `CustomButton` 并绑定到组件的 `clickArea` 字段。
- 点击 `ClickArea` 时，组件先清空本次 callback 并解除 `OnClick` 订阅，再执行 callback；无论 callback 是否抛出异常，`finally` 都会销毁当前 GuideFinger 根 GameObject。
- 独立实例的点击回调最多消费一次；Unity 的 `Destroy` 延迟到帧末执行期间，按钮监听已经解除，避免重复点击再次触发 callback。
- GuidePart 当前已接入该独立实例：每个 active target 创建一个 Finger，Finger 点击 callback 进入原 guided unlock 流程并销毁本次实例。
- 本次未新增 DB/save、配置、全局事件、analytics、广告、IAP 或平台 SDK 链路。

### 数据、资源与生命周期

- 资源入口保持为 `Resources.Load<GuideFinger>("Postcard_GuideFinger")`；非运行态、target 为空或 prefab 缺失时返回 null。
- callback 仅保存在当前实例内，不写入任何数据；`OnDisable` / `OnDestroy` 会解除按钮监听，`OnDestroy` 同时清空 callback 和 finger 粒子循环。
- `ClickArea` 缺失时仍可完成 target 定位和粒子表现，但无法通过点击消费 callback；当前 prefab 已静态绑定该组件。

### 验证与回滚

- 静态检查确认 `Create` 调用 `ShowAt(target)`、绑定 `clickArea`，点击入口执行一次 callback 并在 `finally` 中调用 `Close()` 销毁当前根 GameObject。
- `Postcard_GuideFinger.prefab` 静态检查确认 `ClickArea` 同时包含透明 `Image`、`CustomButton`，且脚本的 `clickArea` 引用指向该按钮。
- `dotnet build Assembly-CSharp.csproj --no-restore ...` 通过：98 warnings / 0 errors。
- 尚未执行 Unity PlayMode、callback 抛异常、快速连点、目标跨 Canvas 定位和 Android/iOS 横屏触摸验证。
- 回滚时恢复 GuidePart 的直接 guide item 按钮入口，并移除 `Create/Close` 调用和 prefab 的 `CustomButton/clickArea` 绑定；不需要数据迁移。

## 2026-07-13：GuideFinger 点击接回 ProgressItem 解锁链

### 当前行为

- `Postcard_GuidePart.SetActiveIndex(...)` 禁用 guide 复制 item 自身的按钮，并调用 `GuideFinger.Create(targetRect, guidePartRoot, OnGuideFingerClick)`。
- 玩家点击独立 Finger 的 `ClickArea` 后，Finger 消费一次 callback；GuidePart 使用当前 index 调用真实页面的 `Postcard_PageItem.TryUnlockGuidedItemByListIndex(...)`。
- 真实页面继续执行原 `TryUnlockItem(...)`，因此星星校验、存档、失败回滚、扣星、真实 `Postcard_ProgressItem.PlayUnlockAnim(...)` 和整页完成流程没有复制或改写。
- 解锁被接受后，guide 遮罩层对应的 `Postcard_ProgressItem.PlayUnlockAnim(...)` 仍同步播放；页面稳定回调和 guide 动画都完成后才推进下一目标。
- 星星不足、保存失败或其它校验拒绝时，GuidePart 刷新当前 item 表现并重新创建指向同一目标的 Finger。
- Finger 点击后自行销毁；目标切换、Guide 停止、组件禁用或销毁时，GuidePart 调用 `Close()` 清理尚未点击的运行时 Finger。

### 资源、数据与性能边界

- GuidePart prefab 已移除空的 `guideFinger` 字段；运行时从既有 Resources 路径创建 `Postcard_GuideFinger.prefab`。
- 本次不新增 DB 字段、数据写入入口、全局事件或解锁分支；数据变化仍只发生在 `Postcard_PageItem` 原流程。
- 每个目标最多创建一个 Finger，失败重试会重新创建；没有 `Update` 或每帧实例化，但移动端实例化/销毁峰值仍需 Profiler 验证。

### 验证与回滚

- CodeGraph 当前链路：`RefreshFingerPosition` → `GuideFinger.Create` → Finger callback `OnGuideFingerClick` → `TryUnlockGuidedItemByListIndex`，接受后同时进入真实页面和 guide 复制 item 的 `PlayUnlockAnim`。
- `dotnet build Assembly-CSharp.csproj --no-restore ...` 通过：98 warnings / 0 errors。
- 尚未运行 Unity PlayMode 验证正常解锁、星星不足重试、保存失败、最后一项完成、快速连点和 Finger 销毁时序；Android/iOS 横屏触摸与实例化开销也未验证。
- 回滚只需恢复 GuidePart 的 active guide item 按钮订阅和旧 finger 表现引用；不需要迁移数据。

## 2026-07-13：Stage Two 关闭后启动页内引导

- `Postcard_GuidePart` 的内部目标映射、Finger 点击、解锁和完成逻辑未改变；本次只调整外部显示时机。
- 胜利页 Finger 点击后先创建 `PostcardPopup` 与 Stage Two；Stage Two 关闭回调再写入 `IsGuiding`，并通过当前 Popup 的 `StartPendingPostcardGuide()` 调用既有 `TryStartGuide(...)`。
- Stage Two 创建失败时直接进入页内引导降级；Popup/Manager 已失效时停止并记录 warning。
- C# 构建通过，98 warnings / 0 errors；Stage Two 与 GuidePart 不重叠、关闭回调以及移动端触摸仍待 PlayMode/真机验证。回滚外部调用时序即可，不涉及 GuidePart prefab、配置或数据迁移。

## 2026-07-15：GuideFinger 等待式 Async API

### 需求与行为

- `GuideFinger.WaitForClickAsync(target, root, cancellationToken)` 提供给后续 `GuideFlowBase` 子类按顺序等待一次引导点击，不要求流程层再包装 callback。
- 正常点击返回 `GuideFlowResult.Completed`；外部取消、主动 `Close()`、组件禁用或销毁返回 `Cancelled`；目标、Resources prefab 或点击区域缺失返回 `Failed`。
- 同一 Finger 实例只保留一个等待完成源，点击、关闭和生命周期清理共用一次性完成入口，避免重复完成或遗留等待者。
- 现有 `Create(target, root, callback)` 的签名与调用方保持不变，本次未修改 `Postcard_GuidePart`、解锁流程或 prefab。

### 数据、资源与事件边界

- 资源入口仍为 `Resources.Load<GuideFinger>("Postcard_GuideFinger")`，没有新增资源地址或实例类型。
- 不读写 DB/save、配置、奖励、analytics、全局事件或平台 SDK；仅新增局部 `UniTaskCompletionSource` 和取消令牌等待。
- API 按 Unity 主线程调用设计，取消后会清理本次创建的 Finger；Android/iOS 共用同一逻辑。

### 验证、风险与回滚

- 目标脚本 `git diff --check` 与敏感项静态扫描通过；没有新增 `async void`、编辑器生命周期、每帧入口或全局事件。
- Unity 已在两个目标脚本修改后重新生成 `Library/ScriptAssemblies/Assembly-CSharp.dll/.pdb`，程序集二进制可检出 `GuideFinger+<WaitForClickAsync>` 状态机；目标编译完成。
- 命令行 `dotnet build Assembly-CSharp.csproj` 仍受 `Temp/bin/Debug` 中 191 个缺失元数据 DLL 阻断；额外的独立 Unity Roslyn 检查受项目同时引用模块化 Unity 程序集和旧式 `UnityEngine.dll` 的依赖冲突阻断，但不影响上述 Unity 实际目标程序集证据。
- 尚未执行 PlayMode 的正常点击、外部取消、主动关闭、快速点击、资源/点击区域缺失及 Android/iOS 横屏验证。
- 回滚时删除 `WaitForClickAsync`、等待完成源及相关绑定/生命周期完成逻辑即可；旧 callback API、prefab 和数据无需迁移。

## 2026-07-15：GuideFinger 脚本去除 Postcard 前缀

### 需求与影响范围

- 脚本文件由 `Postcard_GuideFinger.cs` 重命名为 `GuideFinger.cs`，MonoBehaviour 类型由 `Postcard_GuideFinger` 重命名为 `GuideFinger`。
- `Postcard_GuidePart`、`PostcardPopup_PageCompleteProcess` 和 `GameVictoryPanel` 的类型声明与 `Create(...)` 调用同步更新。
- `GuideFinger.cs.meta` 继续使用原 GUID `c30e1794b95c6b541b0f9ea5dc6ead87`，`Postcard_GuideFinger.prefab` 的脚本引用无需重绑。

### 行为与资源边界

- `Create(...)`、`WaitForClickAsync(...)`、点击、粒子循环、关闭和取消语义均未修改，只调整类型名称。
- Resources prefab 文件名和加载键继续保留 `Postcard_GuideFinger`，代码使用 `Resources.Load<GuideFinger>("Postcard_GuideFinger")`，避免改变已有资源路径。
- 不修改 prefab 序列化字段、DB/save、配置、事件、analytics 或平台 SDK；Android/iOS 共用原逻辑。

### 验证、风险与回滚

- 静态搜索确认 C# 中已无旧类型 `Postcard_GuideFinger` 的声明或调用；剩余同名内容仅为保留的 prefab 名称和 Resources 加载键。
- prefab YAML 的 `m_Script` GUID 与 `GuideFinger.cs.meta` 保持一致，组件引用未断开。
- Unity 已在 `Assembly-CSharp.csproj` 更新为 `GuideFinger.cs` 后重新生成晚于源文件的 `Assembly-CSharp.dll/.pdb`，重命名后的目标程序集编译完成。
- 命令行 `dotnet build Assembly-CSharp.csproj` 仍受 `Temp/bin/Debug` 中 191 个缺失元数据 DLL 阻断；快速点击、取消、prefab Inspector、PlayMode 和移动端触摸仍属于未覆盖项。
- 回滚时把文件、类型和调用点改回 `Postcard_GuideFinger`，同时保留同一 meta GUID；不需要迁移 prefab 或数据。
