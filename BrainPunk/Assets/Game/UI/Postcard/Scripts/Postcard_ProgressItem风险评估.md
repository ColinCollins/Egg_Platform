# Postcard_ProgressItem 风险评估

## 需求与背景

2026-07-08 需求：`Postcard_ProgressItem` 的 `coverImg` 自动检索从末位为 `2` 的图片改为末位为 `1_01` 的图片；绑定时 `coverImg` 默认 inactive；解锁表现改为调用绑定的 `Postcard_TurnEffect.ClosePage`，表现完成后 `coverImg` 稳定 active=true。

2026-07-09 追加检查：用户在解锁表现中补充了 item 本体缩放反馈；本次只收敛该 tween 的生命周期管理，不调整扣星、存档、翻页、guide 推进或 prefab 绑定逻辑。

2026-07-13 追加表现：可解锁 `redDot` 使用 `DOScale` 从原始局部缩放到 `1.1` 倍，单程 `0.8` 秒、`Ease.InOutSine`、无限 `LoopType.Yoyo`。

## 原有行为基线

- `Refresh(isUnlock, unlockCount, currentStar)` 原先通过 `coverImg.fillAmount` 表示已解锁或未解锁，未解锁仍保持 `coverImg` 激活。
- `PlayUnlockAnim(Action)` 原先使用 `coverImg.DOFillAmount(0f, unlockAnimDuration)` 播放遮罩动画，结束后回调 `Postcard_PageItem` / `Postcard_GuidePart`。
- AutoBind 原先优先选择 `UI_mingxinpian_img_<末位为2的数字>_01` 的直接子级 Image 作为 `coverImg`。
- 资源 prefab 中 `coverImg` 静态绑定原先指向 `*_2_01` 图片。

## 影响文件与资源

- `Assets/Game/UI/Postcard/Scripts/Postcard_ProgressItem.cs`
- `Assets/Game/UI/Postcard/Scripts/Postcard_TurnEffect.cs`
- `Assets/Game/UI/Postcard/Prefabs/PostcardPopup_GuidePart.prefab`
- `Assets/Game/UI/Postcard/Resources/Postcard_ems.prefab`
- `Assets/Game/UI/Postcard/Resources/Postcard_food.prefab`
- `Assets/Game/UI/Postcard/Resources/Postcard_forest.prefab`
- `Assets/Game/UI/Postcard/Resources/Postcard_museum.prefab`
- `Assets/Game/UI/Postcard/Resources/Postcard_ocean.prefab`
- `Assets/Game/UI/Postcard/Resources/Postcard_universe.prefab`
- `Assets/Game/UI/Postcard/Sprites/Pictures/ocean/明信片 拼图.layers.prefab`
- `Assets/Game/ArtSrc/Shader/UI/UIPageCurl.shader`

## 新行为

- AutoBind 优先查找 `UI_mingxinpian_img_<末位为1的数字>_01`，同步绑定同物体上的 `Postcard_TurnEffect`，并把 `coverImg.gameObject` 设为 inactive。
- `Refresh(false, ...)` 让 `coverImg` 保持 inactive；`Refresh(true, ...)` 让 `coverImg` 保持 active。
- `PlayUnlockAnim(...)` 在解锁被接受后立即隐藏 `redDot` / `blockCost`、禁用点击、激活 `coverImg`，再尝试 `Postcard_TurnEffect.TryClosePage(...)`。
- item 本体缩放反馈延迟 0.6 秒播放，并挂回 `_unlockTween`；`Refresh()`、`OnDisable()`、`OnDestroy()` 会通过 `StopUnlockTween()` 清理，避免脱离生命周期的延迟 DOTween。
- 可解锁 `redDot` 显示时启动独立 `_redDotTween`；刷新隐藏、接受解锁、组件禁用或销毁时停止并恢复基础缩放，重新启用且红点仍显示时恢复动画。Tween 创建入口使用 `Application.isPlaying` 与 active hierarchy 防护。
- 翻页启动失败时直接执行完成回调，避免 `Postcard_PageItem` 或 `Postcard_GuidePart` 的交互锁卡住。
- 本次未新增 DB 字段、未写存档、未新增配置、未新增全局事件、未改广告/IAP/analytics/RemoteConfig/平台 SDK。

## 风险项

- **P1：Prefab YAML 静态修改风险**
  多个 prefab 通过静态 fileID 映射调整 `coverImg`、`turnEffect` 和 `m_IsActive`；需要 Unity Inspector 打开确认无 Missing Script、材质丢失或 override 异常。
- **P1：翻页材质表现风险**
  当前 `Postcard_TurnEffect` 通过 `Shader.Find("UI/PageCurl")` 创建运行时材质，`ClosePage` 完成后默认恢复原材质并回到可见状态，当前需求要求完成后 `coverImg` active=true；仍需 PlayMode 确认视觉上是目标的稳定显示状态。
- **P1：Guide 层同步风险**
  `Postcard_GuidePart` 会同步调用 guide 层 `Postcard_ProgressItem.PlayUnlockAnim(...)`；guide prefab 已改为 `*_1_01` 与 inactive，需要确认 finger 推进、guide 可视动画和真实页面稳定回调顺序一致。
- **P2：本体缩放 tween 生命周期风险**
  若延迟 `DOScale` 不挂入 `_unlockTween`，刷新、关闭、切页或销毁时可能无法停止，导致局部缩放残留或下次播放叠加。当前处理是把延迟缩放统一放回 `PlayUnlockPulse()`，在 `Awake()` 缓存初始局部缩放，并在正常完成或 `StopUnlockTween()` 中显式恢复该缓存值。
- **P2：红点循环 tween 生命周期风险**
  无限循环若不在显隐和组件生命周期中清理，会造成隐藏对象仍保留 tween、重复刷新叠加或缩放残留。当前使用独立 `_redDotTween`，在隐藏、解锁、`OnDisable`、`OnDestroy` 时 Kill 并恢复缓存的原始局部缩放；实际弹窗反复开关与切页仍需 PlayMode 验证。
- **OBS：未覆盖 source art prefab 使用路径**
  `Sprites/Pictures/ocean/明信片 拼图.layers.prefab` 已同步绑定 inactive 和 TurnEffect，但其是否参与运行时加载仍需 Unity 资源链确认。

## 验证证据

- CodeGraph 已确认 `Postcard_ProgressItem.PlayUnlockAnim(...)` 调用面主要来自 `Postcard_PageItem` 和 `Postcard_GuidePart`，本次未复制扣星、存档或完成页逻辑。
- 静态映射脚本确认 6 个 Resources 明信片页 prefab、`PostcardPopup_GuidePart.prefab` 和 ocean layers prefab 中的 `coverImg` 均指向 `*_1_01`，且目标 active 均为 `0`。
- 静态计数确认上述 prefab 中 `Postcard_ProgressItem` 与 `Postcard_TurnEffect` 数量一致，并且不再命中旧错误脚本 GUID `4ead0353ac43aa743ab637684837defb`。
- 重新评估当前 `Postcard_TurnEffect.cs` 后，静态确认相关 prefab 不再保留旧版 `pageCurlMaterial` 序列化字段。
- `git diff --check` 覆盖本次脚本、文档和相关 prefab 后通过，仅有工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，99 warnings / 0 errors。
- 2026-07-09 `git diff --check` 覆盖 `Postcard_ProgressItem.cs`、本功能说明、本风险评估和 `DevelopMD/已有功能设计与使用记录.md` 后通过，仅有 LF/CRLF 转换提示。
- 2026-07-09 静态敏感扫描 `Postcard_ProgressItem.cs` 未发现新增编辑器生命周期、每帧轮询、全局事件、DB/save、RemoteConfig、广告、IAP、analytics、PlayerPrefs 或实例化链路。
- 2026-07-09 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 被当前 `Temp\bin\Debug` 元数据缺失阻断，首个错误为 `Apple.Core.dll` 缺失，共 191 个 `CS0006`，未获得业务脚本编译诊断。
- 2026-07-13 `git diff --check -- Assets/Game/UI/Postcard/Scripts/Postcard_ProgressItem.cs` 通过；敏感关键字扫描未发现新增编辑器生命周期、每帧轮询、全局事件、DB/save、RemoteConfig、PlayerPrefs 或实例化链路。
- 2026-07-13 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，98 warnings / 0 errors。

## 未验证缺口

- 尚未在 Unity Editor 中打开 prefab 验证新增 `turnEffect` 字段、`Postcard_TurnEffect` 组件、target Graphic 引用和 active 默认状态。
- 尚未在 Unity PlayMode 验证普通点击解锁、引导点击解锁、快速连点、关闭弹窗中断、页面完成奖励流程后的交互锁释放。
- 尚未在 Android/iOS 横屏设备验证翻页视觉、触控遮挡和性能。
- 尚未在 Unity PlayMode / Android / iOS 横屏中确认红点呼吸节奏、停用/重启恢复、切页和解锁时复位，以及持续循环对移动端 UI 开销的实际影响。

## 回滚路径

- 回退 `Postcard_ProgressItem.cs` 中 `turnEffect` 字段、`coverImg` active 控制、`TryClosePage` 调用和 AutoBind 检索规则。
- 若需要回退翻页脚本协作，移除 `Postcard_ProgressItem` 对 `TryClosePage(Action)` 的依赖，或保留当前 `Postcard_TurnEffect` 的 Try 系列入口但不在进度 item 中调用。
- 回退相关 prefab 中 `coverImg` 从 `*_1_01` 到旧绑定、移除新增 `Postcard_TurnEffect` 组件并恢复目标图片 active 状态。
- 若只回退本次红点表现，移除 `Postcard_ProgressItem.cs` 中 `_redDotTween`、基础缩放缓存、`PlayRedDotTween()` / `StopRedDotTween()` 及其调用即可，不需要修改 prefab、DB、配置或资源。

## 2026-07-14：redDot 旋转抖动风险追加

### 基线、变更与影响面

- 基线：`redDot` 原本只有独立的无限呼吸缩放 Tween，显隐、解锁、禁用和销毁时统一清理并复位缩放。
- 变更：在同一生命周期入口增加与 `PostcardEnter.prefab` 最终配置一致的旋转 Sequence：`0.3` 秒、Z 轴 `18` 度、`vibrato = 5`、`randomness = 10`、衰减、线性缓动、每轮双摇、组间隔 `0.7` 秒、unscaled 无限循环。
- 影响文件只有 `Assets/Game/UI/Postcard/Scripts/Postcard_ProgressItem.cs`；未改 prefab、配置、存档、资源加载、事件、打点或平台链路。

### 风险项与控制

- **P2：无限 Sequence 残留或重复叠加。** 当前沿用 `Refresh`、解锁、`OnDisable`、`OnDestroy` 的既有 `StopRedDotTween()` 边界，同时 Kill 缩放和旋转 Tween；每次启动前现有流程都会先停止上一轮。
- **P2：旋转角度残留。** 首次播放缓存红点基础本地欧拉角，停止时显式恢复；实际快速切页、关闭弹窗和解锁中断仍需 PlayMode 验证。
- **P2：缩放与旋转叠加观感。** 两条 Tween 修改不同 Transform 属性，不会直接覆盖数值，但节奏叠加是否过强属于视觉验收项，当前静态证据无法确认。
- **P2：移动端持续 UI Tween 开销。** 仅达到解锁条件且显示的红点运行，未新增 `Update` 或对象实例化；Android/iOS 横屏的实际 CPU/电量影响尚未验证。

### 验证、缺口与回滚

- 目标脚本 `git diff --check` 通过；敏感项静态扫描未发现新增编辑器生命周期、业务每帧轮询、全局事件、DB/save、RemoteConfig、PlayerPrefs 或实例化链路。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，0 个错误、98 个警告。
- Unity PlayMode 的实际双摇节奏、暂停态、反复显隐/切页/关闭/解锁复位以及 Android/iOS 横屏表现仍未验证。
- 回滚时仅移除 `_redDotShakeTween`、基础角度缓存、旋转 Sequence 创建与停止复位代码，保留原呼吸缩放即可恢复基线。

## 2026-09-11 非最终翻页中断 ClickBlock 修复风险补充

### 基线与变更

- 基线：普通 PageItem 在已写存档、扣星并开启 Popup 动画锁后，依赖 `Postcard_TurnEffect` 的自然 `OnComplete` 回调解除锁；组件禁用或销毁会 `Kill(false)`，回调不会返回。
- 变更：仅非最后一块普通解锁传入 `forceCompleteOnInterrupt=true`。TurnEffect 被禁用或销毁时会完成当前 tween 并执行原回调；最后一块、Guide 与其它 TurnEffect 入口保持默认 false。

### 风险、验证与回滚

- **P1 已缓解：非最终表现异常中断后输入软锁。** 该块进度与星星消耗已经在动画前保存，强制完成只收口本地表现回调和交互锁，不重新扣星、不重写存档、不触发页面完成奖励。
- **P2：中断视觉跳帧。** Tween 被强制完成时会直接进入已解锁稳定画面；表现是否符合 Android/iOS 横屏观感仍需 PlayMode 与真机验证。
- 已完成静态调用点与目标差异格式检查；尚未验证父节点禁用、Popup 快速关闭、切页销毁和应用恢复时的实际 DOTween 回调顺序。
- 回滚时删除 `Postcard_PageItem` 的非最终块 true 传参与 TurnEffect/ProgressItem 的参数透传，恢复组件禁用时统一 `Kill(false)`；不涉及 prefab、存档或配置迁移。

## 2026-09-20 解锁缩放回弹风险复核

### 风险基线与修复

- **P2 已缓解：item 根缩放残留。** 原实现依赖绝对目标 `Vector3.one * 1.1f`、Yoyo 和 `Kill(true)` 收口，没有显式恢复基准。当前改为缓存 prefab 实例初始缩放，以初始值的 `1.1` 倍播放，并在完成、刷新、禁用和销毁时恢复缓存值。
- **P2 已缓解：ClickScale 禁用中断。** `clickArea` 的回弹依赖自身 `LateUpdate`；`blockCost.SetActive(false)` 会停止该更新。当前在隐藏消耗块前把已绑定点击节点恢复为 `Vector3.one`，不修改共享按钮脚本。
- **OBS：父级完成动画。** `postcard b2s.anim` / `postcard small_idle.anim` 会把父级 `Root` 保持在约 `0.75`，因此最终视觉或 `lossyScale` 仍可能不是 1；本修复只保证 `Postcard_ProgressItem` 与点击节点的本地缩放收口，不改变整页完成构图。

### 验证缺口与回滚

- Unity Bee/Roslyn 编译退出码为 0；静态确认正常完成和中断入口均执行初始缩放恢复，目标文件差异检查通过。
- Unity PlayMode 的普通块、最后一块、Guide 复制 item、弹窗快速关闭/切页，以及 Android/iOS 横屏低帧率下的回弹观感尚未实测，结论为 `uncertain`。
- 回滚仅需恢复 `Postcard_ProgressItem.cs` 原缩放 tween 与 `Kill(true)`，并移除本轮基准缩放缓存和点击节点复位；不回滚共享 `ClickScaleAnim`、prefab、配置或存档。
