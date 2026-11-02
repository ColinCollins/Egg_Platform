# Postcard_TurnEffect 功能说明

## 需求背景

2026-07-08 需求：参考 `PageTurnEffect`，为明信片页面新增 `Postcard_TurnEffect` 脚本。该组件运行时通过 `Shader.Find("UI/PageCurl")` 创建私有翻页材质，播放表现时替换目标 Image / Graphic 原本材质，并播放 `OpenPage` 或 `ClosePage` 翻页效果。

2026-07-08 追加：当前脚本已提供 `Complete` 事件，以及 `TryOpenPage(Action)` / `TryClosePage(Action)` / `TryTurnPage(...)` 降级入口，调用方可判断翻页是否成功启动，避免运行时、目标 Graphic 或 shader 缺失时卡住上层流程。

## 影响范围

- 新增脚本：`Assets/Game/UI/Postcard/Scripts/Postcard_TurnEffect.cs`
- 新增脚本 meta：`Assets/Game/UI/Postcard/Scripts/Postcard_TurnEffect.cs.meta`
- 依赖 shader：`Assets/Game/ArtSrc/Shader/UI/UIPageCurl.shader`
- 相关系统：Unity UGUI `Graphic.material` / `Image.material`，DOTween `DOVirtual.Float`

本组件新增入口本身未修改 `PostcardPopup`、`Postcard_PageItem`、DB/save、Luban 配置、Resources 路径、RemoteConfig、全局事件、广告、IAP、analytics 或平台 SDK。`Postcard_ProgressItem` 本次会在相关 prefab 的 `coverImg` 上绑定该组件，但不再为组件序列化材质模板。

## 原有行为基线

- `Assets/Game/UI/Global/Scripts/PageTurnEffect.cs` 已提供通用 `UI/PageCurl` 翻页效果，会在 `Graphic` 上创建或使用翻页材质，并驱动 `_Distance` 播放 `OpenPage` / `ClosePage`。
- `PostcardGrayImage` 已在明信片模块中采用“缓存目标原材质、运行时替换、退出时恢复”的本地材质控制模式。
- 明信片模块此前没有单独的 Postcard 翻页材质控制脚本；仓库中 `Assets/Game/ArtSrc/Shader/UI/UIPageCurl.shader` 的 shader 名称为 `UI/PageCurl`。

## 当前行为

- `Postcard_TurnEffect` 可挂在带 `Graphic` 的对象上；`targetGraphic` 未绑定时，运行时回退到当前对象上的 `Graphic`。
- 播放时通过 `Shader.Find("UI/PageCurl")` 创建 `HideFlags.HideAndDontSave` 的私有运行时材质实例，再写入 `_Angle`、`_Radius`、`_ShadowStrength`、`_BackColor` 和 `_Distance`。
- `OpenPage()` 播放 `_Distance` 从 `1.5` 到 `-1`，表现为从可见翻到隐藏。为保持隐藏结果，Open 完成后不会自动恢复原材质。
- `ClosePage()` 播放 `_Distance` 从 `-1` 到 `1.5`，表现为从隐藏翻到可见。默认 `restoreOriginalMaterialAfterClose=true`，Close 完成后先恢复原材质，再调用完成回调。
- `TurnPage(float fromDistance, float toDistance, Action onComplete = null)` 支持自定义距离范围；自定义播放不会自动恢复原材质。
- `Complete` 事件会在成功播放到结束后触发；新播放打断旧播放、播放未成功启动，或未开启强制完成的组件禁用/销毁时不会触发。
- `TryOpenPage(Action)`、`TryClosePage(Action, bool)` 和 `TryTurnPage(...)` 会返回播放是否成功启动或立即完成；返回 false 时不会调用完成回调，也不会触发 `Complete`，调用方需要自行降级完成。
- `TryClosePage(..., forceCompleteOnInterrupt: true)` 只供已写入解锁存档、且中断后应释放普通页面交互锁的调用方使用。该标记为 true 时，组件禁用或销毁会以 DOTween `Kill(true)` 收口当前 tween，执行原完成回调；默认 false 继续保持中断即取消。
- 普通 `OpenPage(Action)`、`ClosePage(Action)` 和 `TurnPage(...)` 不返回启动结果；如果运行时、目标 Graphic 或 shader 缺失，传入回调不会被调用。
- `SetProgress(float progress)` 支持用 0..1 进度直接写入距离，便于拖拽或 Timeline 驱动；调用后需要外部按需要恢复原材质。
- `OnDisable()` / `OnDestroy()` 会停止当前 tween 并恢复原材质；`OnDestroy()` 同时销毁运行时材质实例。
- `OnValidate()` 只在 PlayMode 且运行时材质已存在时刷新本组件私有材质参数，不访问 DB/save/config，也不在编辑态创建材质实例。

## 数据、资源、UI 与事件流

- 数据流：不读取、不写入 `DB.Postcard` 或任何存档字段。
- 资源流：不加载 Resources 或 Addressable；只通过 `Shader.Find("UI/PageCurl")` 获取已在项目中的 shader，并在运行时创建私有材质实例。
- UI 流：播放开始时缓存目标 `Graphic.material`，替换为运行时翻页材质；Close 完成、禁用或销毁时按缓存恢复。
- 事件流：不新增 `PlayEvents` / 全局事件，不订阅全局事件；`Complete` 是本组件 C# 事件，只用于外部对象订阅本地播放完成通知。
- 平台流：不触碰 Android/iOS 平台 SDK、广告、IAP、analytics 或 RemoteConfig。

## 验证记录

- CodeGraph 已读取 `PageTurnEffect`，确认其 `OpenPage` / `ClosePage` 距离范围分别为 `1.5 -> -1` 与 `-1 -> 1.5`，本组件沿用该表现边界。
- 静态 shader 检查确认 `UIPageCurl.shader` 的 shader 名称为 `UI/PageCurl`，本组件固定使用该名称创建运行时材质。
- 静态敏感扫描 `Assets/Game/UI/Postcard/Scripts/Postcard_TurnEffect.cs` 只命中本组件 `OnValidate` 和注释中的 `DB` 字样；未命中 `Reset(`、`ExecuteAlways`、`ExecuteInEditMode`、`Update(`、`LateUpdate(`、`FixedUpdate(`、`PlayEvents`、`DispatchEvent`、`Subscribe<`、`RemoteConfig`、`IAP`、`Ads`、`Analytics`、`PlayerPrefs`、`SavePostcard` 或 `Instantiate(`。
- `git diff --no-index --check -- NUL Assets\Game\UI\Postcard\Scripts\Postcard_TurnEffect.cs`：通过，仅有 Git LF/CRLF 提示。
- `git diff --no-index --check -- NUL Assets\Game\UI\Postcard\Scripts\Postcard_TurnEffect.cs.meta`：通过，仅有 Git LF/CRLF 提示。
- 2026-07-08 重新评估当前脚本后，静态扫描确认相关 prefab 不再保留 `pageCurlMaterial` 序列化字段，新增组件只保留当前脚本字段。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，99 warnings / 0 errors。

## 剩余风险与未验证项

- 尚未在 Unity Inspector 中实际验证 `targetGraphic` 绑定和运行时 `Shader.Find("UI/PageCurl")` 可用性。
- 尚未在 Unity PlayMode 验证 `OpenPage` 隐藏状态保持、`ClosePage` 结束后恢复原材质、连续 Open/Close 打断、禁用/销毁恢复材质。
- 尚未在目标明信片 prefab 中确认应挂载的具体 Image、遮罩层级和与 `PostcardGrayImage` 等其它材质控制组件的先后关系。
- 尚未在 Android/iOS 横屏设备验证实际翻页视觉、触摸期间遮挡关系和性能。

## 回滚方式

- 删除 `Assets/Game/UI/Postcard/Scripts/Postcard_TurnEffect.cs` 及对应 `.meta`。
- 移除目标 prefab 上新增的 `Postcard_TurnEffect` 组件或清空相关表现调用。
- 不需要迁移或清理 DB/save/config 数据；本次未新增持久化字段。

## 2026-09-11 非最终 PageItem 翻页中断收口

### 背景与行为

- 非最后一块 PageItem 已保存、扣星并开启 `IsPagePlayingAnim` 后，翻页组件若在 Tween 完成前禁用或销毁，原先 `Kill(false)` 不会执行回调，导致 Popup 的 ClickBlock 无法关闭。
- `TryClosePage` 新增 `forceCompleteOnInterrupt` 可选参数；内部保存该状态。`OnDisable` / `OnDestroy` 按该状态执行 `StopTurnTween(forceComplete)`：true 时完成 tween 并执行回调，false 时保持取消。
- 该开关不是全局默认。`OpenPage`、`ClosePage`、`TryOpenPage`、`TryTurnPage` 与未传参的 `TryClosePage` 均维持 false，避免把一般销毁语义变成完成语义。

### 影响、验证与回滚

- 关联脚本：`Postcard_ProgressItem.cs`、`Postcard_PageItem.cs`；不改 prefab、配置、存档字段、奖励、资源、全局事件、广告、IAP、analytics 或平台 SDK。
- `Postcard_PageItem` 仅在 `completedNow == false` 时传入 true；最后一块和 Guide 调用保持 false，因此正常关闭不会强行进入整页完成或引导推进流程。
- 已执行目标调用点检查与 `git diff --check`；Unity PlayMode、Android/iOS 横屏的父节点禁用/销毁故障注入仍待验证。
- 回滚时移除 `forceCompleteOnInterrupt` 参数、私有状态和 `Postcard_PageItem` 的传参即可恢复中断取消行为；不需要数据迁移。
