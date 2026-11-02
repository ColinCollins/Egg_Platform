# CustomGrayRender UI 灰度工具

## Requirement / Background

新增通用 UI 灰度渲染工具 `CustomGrayRender`。当 `isGray` 为 `true` 时，组件会让当前节点及其子节点上的普通 `Graphic` 使用灰度 UI 材质渲染；`TMP_Text` / `TextMeshProUGUI` 只改字体颜色，不替换材质；当 `isGray` 为 `false`、组件禁用或销毁时，恢复每个对象原本的材质或颜色。

## Affected Files And Systems

- `Assets/Game/UI/Global/Scripts/CustomGrayRender.cs`
- `Assets/Game/ArtSrc/Shader/UI/CustomGray.shader`
- `Assets/Game/ArtSrc/Shader/UI/CustomGray.mat`
- Unity UGUI `Graphic` 渲染材质链路。

未修改 prefab、场景、DB、Save、RemoteConfig、Luban 配置、资源包收集规则、平台 SDK 或业务 UI 脚本。

## Existing Behavior Baseline

本地搜索未找到已有项目侧通用 UI 灰度脚本。已有灰度资源主要来自 Spine 自带 `SkeletonGraphic` 灰度 shader/material，项目 UI shader 目录已有 `UI_AddLinear`、`UITilling` 等 UGUI shader，均保留原状。

## New Behavior

- `CustomGrayRender` 挂在任意 UI 节点后，默认递归包含 inactive 子节点并收集 `Graphic`。
- 2026-07-01 优化后，组件新增 `graphics` 子节点记录和 `autoCollectGraphics` 开关；默认在启用、`Refresh()` 和 Inspector 校验时收集一次，后续灰度切换直接遍历记录数组，参考 `SpriteRendererGroupAlpha` 的子渲染器记录方式批量处理多个子节点。
- `isGray=true` 时，普通 `Graphic` 缓存 `Graphic.material` 后替换为灰度材质；`TMP_Text` 缓存 `color` 后按原颜色灰度值写回字体颜色，避免修改 TMP 字体材质、font atlas 或 TMP shader 实例。
- `isGray=false`、`OnDisable` 或 `OnDestroy` 时恢复缓存的原材质或原字体颜色。
- 提供 `SetGray(bool)`、`SetGrayWithoutCheck(bool)`、`Refresh()` 和 `CollectGraphics()`，便于业务脚本或 UnityEvent 显式切换；动态新增/移除子 `Graphic` 后需要调用 `Refresh()` 或 `CollectGraphics()` 更新记录。
- `SwitchRenderState()` 是 Odin Inspector 按钮入口，用于编辑器或运行时临时预览灰度开关；编辑器态会记录 Undo，且未指定 `grayMaterial` 时只加载已有 `Assets/Game/ArtSrc/Shader/UI/CustomGray.mat` 资产，不在 edit mode 创建默认材质。
- `grayMaterial` 可在 Inspector 指定自定义 UI shader 材质；未指定时使用 `Shader.Find("UI/CustomGray")` 创建隐藏运行时默认材质。

## Data / Resource / UI Flow

灰度切换只改变普通 UGUI `Graphic.material` 或 TMP 文本 `color`。不改变点击状态、raycastTarget、CanvasGroup、层级 active、保存数据、配置数据、事件系统、打点或资源加载流程。

`CustomGray.shader` 保留 UGUI 常用 stencil、clip rect、alpha clip、sprite atlas 和 vertex color/tint 支持，可配合 UI Mask/RectMask2D 与常规 Image/Text Graphic 渲染。

## Verification

- 2026-07-01 静态代码检查：`rg -n "\bReset\b|ExecuteAlways|ExecuteInEditMode|\bDB\b|\bSave\b|RemoteConfig|\bInstance\b|Instantiate" Assets/Game/UI/Global/Scripts/CustomGrayRender.cs Assets/Game/ArtSrc/Shader/UI/CustomGray.shader` 未发现匹配；`OnValidate` 仅做本地 `Graphic` 收集，运行时才调用材质应用。
- 2026-07-01 静态代码检查确认 `TMP_Text` 分支只写 `color`，不写 `material`；普通 `Graphic` 分支仍走 `ResolveGrayMaterial()`。
- 2026-07-01 `git diff --check -- Assets/Game/UI/Global/Scripts/CustomGrayRender.cs DevelopMD/CustomGrayRender_UIGray.md DevelopMD/已有功能设计与使用记录.md` 通过，仅有 Git 工作区 LF/CRLF 转换提示。
- 2026-07-01 `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 error / 97 warnings。
- 静态检查确认未改动现有 prefab 或业务 UI 脚本。

## Remaining Risks / Unverified Gaps

- Unity Editor/PlayMode 未运行，需验证 Image、普通 Text、TMP_Text/TextMeshProUGUI、Mask/RectMask2D、禁用/重新启用、动态新增子节点后 `Refresh()` 的实际渲染表现。
- 若关闭 `autoCollectGraphics`，必须由 Inspector 或业务脚本维护 `graphics` 记录；记录遗漏的子节点不会被灰度切换影响。
- 自定义 `grayMaterial` 若不兼容 UGUI stencil/clip 属性，可能影响 Mask 或 RectMask2D；默认 `UI/CustomGray` 已包含这些属性。
- 若运行时有其他脚本同时修改同一普通 `Graphic.material` 或 TMP `color`，恢复逻辑会回到 `CustomGrayRender` 首次进入灰度时缓存的值，业务需要明确调用顺序。
- Android/iOS 横屏设备渲染与性能未验证。

## Rollback Path

删除 `CustomGrayRender.cs`、`CustomGray.shader`、`CustomGray.mat` 及对应 `.meta` 文件，并移除 prefab 或场景中新增的组件引用。

## 2026-07-06 Postcard Page Image 横向灰度填充 Shader

### Requirement / Background

新增一个用于 Postcard page Image 的 UI shader，让材质提供 `_FillAmount`（Inspector 显示为 `fillAmount`，范围 `0..1`）参数，并按当前 Image 的 `uv.x` 从左到右动态决定灰度区域。需求只要求新增 shader 资源，不直接修改 Postcard prefab、脚本或运行时数据。

### Affected Files And Systems

- `Assets/Game/ArtSrc/Shader/UI/PostcardPageGrayFill.shader`
- `Assets/Game/ArtSrc/Shader/UI/PostcardPageGrayFill.mat`
- Unity UGUI `Image.material` 渲染链路。

未修改 Postcard prefab、`Postcard_PageItem`、DB、Save、RemoteConfig、Luban 配置、资源包收集规则、事件系统、打点或平台 SDK。

### Existing Behavior Baseline

现有 `UI/CustomGray` shader 只提供 `_GrayAmount` 控制整张 UI Graphic 的灰度混合比例，不提供基于横向 UV 的局部灰度填充。`Postcard_PageItem` 中存在明信片页背景 `Image bg` 引用，但当前实现不主动修改该 Image 材质。

### New Behavior

- 新增 `UI/PostcardPageGrayFill` shader，保留 `UI/CustomGray` 的灰度权重 `0.299 / 0.587 / 0.114`。
- `_FillAmount = 0` 时不灰化；`_FillAmount = 1` 时整张 Image 灰化；中间值按归一化后的 `uv.x <= _FillAmount` 区域灰化。
- 2026-07-06 修正后，shader 新增 `_FillUVRect`，由 `PostcardGrayImage` 按目标 sprite 的 `sprite.uv` min/max 写入，用于把图集 UV 归一化到本地横向 `0..1`。
- `_GrayAmount` 默认 `1`，用于控制已进入填充区域的灰度强度；默认材质 `PostcardPageGrayFill.mat` 可直接赋给 Postcard page Image 后调 `_FillAmount`。
- shader 保留 UGUI stencil、RectMask2D clip、alpha clip、sprite atlas 标记、vertex color/tint 与 `_TextureSampleAdd` 支持。

### Data / Resource / UI Flow

该资源只影响使用此材质的 UI Image 像素输出。运行时如果需要动画，可由业务脚本或 DOTween 调整材质实例的 `_FillAmount`；本次未新增脚本入口，也未改变点击、遮罩层级、保存、配置、事件或资源加载流程。

### Verification

- 2026-07-06 静态资源检查：`rg -n "UI/PostcardPageGrayFill|_FillAmount|_GrayAmount|PostcardPageGrayFill" Assets/Game/ArtSrc/Shader/UI/PostcardPageGrayFill.shader Assets/Game/ArtSrc/Shader/UI/PostcardPageGrayFill.mat` 确认 shader 名、材质名、`_FillAmount` 与 `_GrayAmount` 均存在。
- 2026-07-06 静态风险扫描：`rg -n "\bReset\b|ExecuteAlways|ExecuteInEditMode|\bDB\b|\bSave\b|RemoteConfig|\bInstance\b|Instantiate|PlayEvents" Assets/Game/ArtSrc/Shader/UI/PostcardPageGrayFill.shader Assets/Game/ArtSrc/Shader/UI/PostcardPageGrayFill.mat` 未发现匹配。
- 2026-07-06 空白检查：`rg -n "[ \t]+$" Assets/Game/ArtSrc/Shader/UI/PostcardPageGrayFill.shader Assets/Game/ArtSrc/Shader/UI/PostcardPageGrayFill.shader.meta Assets/Game/ArtSrc/Shader/UI/PostcardPageGrayFill.mat Assets/Game/ArtSrc/Shader/UI/PostcardPageGrayFill.mat.meta DevelopMD/CustomGrayRender_UIGray.md` 未发现行尾空白；PowerShell 逐行复查同样无输出。
- 2026-07-06 `git diff --check -- DevelopMD/CustomGrayRender_UIGray.md` 通过，仅提示该文档下次 Git 触碰时会按仓库设置从 LF 转 CRLF。新增 shader/material 仍是未跟踪文件，未纳入普通 `git diff --check`。
- 2026-07-06 `Select-String` 检查确认 `PostcardPageGrayFill.mat` 引用 `PostcardPageGrayFill.shader.meta` 中的 shader GUID。
- 2026-07-06 修正验证：`rg -n "_FillUVRect|FillUVRect|OnDidApplyAnimationProperties|OnValidate|sprite\.uv" Assets/Game/UI/Postcard/Scripts/PostcardGrayImage.cs Assets/Game/ArtSrc/Shader/UI/PostcardPageGrayFill.shader Assets/Game/ArtSrc/Shader/UI/PostcardPageGrayFill.mat` 确认组件、shader 和默认材质包含图集 UV 归一化参数链路；`dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false` 通过，结果为 0 error / 97 warnings。
- Unity Editor/PlayMode 未运行，shader 实际导入和 Postcard page Image 挂载效果待验证。

### Remaining Risks / Unverified Gaps

- 尚未在 Unity Editor 中实际导入 shader 或挂到 Postcard page Image 预览。
- 当前通过 `_FillUVRect` 归一化常规图集 UV；若 page 图片使用旋转打包图集或特殊 Mesh Type，需要在实际资源上确认横向方向是否符合美术预期。
- Android/iOS 横屏设备渲染与性能未验证。

### Rollback Path

删除 `PostcardPageGrayFill.shader`、`PostcardPageGrayFill.mat` 及对应 `.meta` 文件，并移除 Postcard page Image 上对该材质的引用。
