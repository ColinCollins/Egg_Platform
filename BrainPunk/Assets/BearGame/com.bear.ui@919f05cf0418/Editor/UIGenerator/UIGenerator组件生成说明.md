# UIGenerator 组件生成说明

## 2026-08-10：交互组节点补齐 CanvasRenderer

### 需求与背景

`UIGenerator` 将 PSD JSON 中名称包含 `_btn` 或 `_toggle` 的组节点识别为交互节点，并在生成的
GameObject 上绑定 `UIEmptyRaycastTarget`。本次要求是在绑定该空射线组件时，显式配套绑定
`CanvasRenderer`，保证生成 Prefab 的 UI 渲染组件契约完整。

### 影响范围

- `Assets/BearGame/com.bear.ui@919f05cf0418/Editor/UIGenerator/UIGenerator.cs`
- PSD JSON 生成流程中的 `_btn`、`_toggle` 组节点。
- 不修改 Artboard 数据结构、Sprite/Text 生成、Prefab 保存、运行时 UI、存档、配置、资源地址、事件、
  analytics 或平台流程。

### 原有行为基线

`CreatePsdGroupContainer()` 在识别到交互组节点后，直接调用 `AddComponent<UIEmptyRaycastTarget>()`，
再组装 Button 或 Toggle 行为；生成器代码中没有显式添加 `CanvasRenderer`。

### 修改后行为

交互组节点先显式调用 `AddComponent<CanvasRenderer>()`，再添加 `UIEmptyRaycastTarget` 并组装原有
Button/Toggle 行为。非交互组节点不增加组件，原有交互分类、射线目标和 Button/Toggle 绑定逻辑不变。

### 数据与流程边界

改动只发生在 Unity Editor 内的 Prefab 临时对象生成阶段。没有新增 DB、Save、RemoteConfig、运行时单例、
资源加载、全局事件或移动平台分支，也没有引入 Editor 生命周期回调。

### 验证证据

- `git diff --check -- Assets/BearGame/com.bear.ui@919f05cf0418/Editor/UIGenerator/UIGenerator.cs` 通过。
- `dotnet build .\Assembly-CSharp-Editor.csproj --verbosity minimal` 通过，结果为 `0 error / 1123 warning`；
  warning 为工程既有项。
- 静态扫描确认 `UIGenerator` 中只有这一处绑定 `UIEmptyRaycastTarget`，且 `CanvasRenderer` 位于其前添加。

### 剩余风险与未验证项

- 尚未在 Unity Editor 中实际执行一次包含 `_btn` / `_toggle` 组节点的 JSON 生成并检查 Prefab，实际生成结果
  中是否恰好各有一个 `CanvasRenderer` 仍为 `uncertain`。
- 建议手工生成样例后确认交互组节点同时存在 `CanvasRenderer`、`UIEmptyRaycastTarget` 和对应 Button/Toggle，
  且点击/切换命中正常。

### 回滚路径

删除 `CreatePsdGroupContainer()` 中位于 `AddComponent<UIEmptyRaycastTarget>()` 前的
`AddComponent<CanvasRenderer>()` 调用，即可恢复原有生成行为。

## 2026-08-25：支持图层 rotation 属性

### 需求与背景

新版 PSD 导出样例 `二级选关3.layers.json` 的 `document.exportMeta.scriptVersion` 为 `2.3.0`，非组图层新增
根级 `rotation` 数值。样例中共 12 个图层携带该字段，当前均为文本图层，包含正角度、零角度和负角度。

### 影响范围

- `Models/PsdExportModels.cs`：`PsdLayerNode` 增加 `rotation` 映射。
- `Models/LayerData.cs`：通用 `LayerData` 增加 `rotation` 映射，兼容 PSD 与旧 Artboard 数据入口。
- `UIGenerator.cs`：PSD 节点转换时传递角度，创建非组图层的 `RectTransform` 时设置本地 Z 轴旋转。
- 不修改组节点层级、锚点/尺寸/位置算法、Sprite/Text 组件生成、Prefab 保存、运行时 UI、存档、配置、资源、
  事件、analytics 或平台流程。

### 原有行为基线

两个图层模型均未声明 `rotation`，PSD 转换不会保留该值；`CreateLayerGameObject()` 会把所有生成图层的
`localRotation` 固定为单位旋转，因此新版样例中的倾斜文本会被生成为 0 度。原注释也声明统一 rotation，
在支持导出角度后已与代码事实冲突，因此随实现同步改为坐标转换说明。

### 修改后行为

PSD 路径按 `PsdLayerNode.Rotation -> LayerData.Rotation` 传递角度；旧 Artboard 路径可直接反序列化
`LayerData.rotation`。设计稿坐标系 Y 轴向下，而 Unity UI 坐标系 Y 轴向上，因此最终使用
`Quaternion.Euler(0, 0, -rotation)` 设置 `RectTransform.localRotation`。角度字段使用可空值保留“字段是否存在”，
旧 JSON 未提供字段时通过 `GetValueOrDefault()` 按 0 度生成，结果继续保持单位旋转。

### 数据与流程边界

PSD JSON 的非组节点先由 Json.NET 反序列化为 `PsdLayerNode`，再由 `BuildLayerDataFromPsd()` 转为
`LayerData`，最终在 `CreateLayerGameObject()` 创建 RectTransform。旧 Artboard JSON 直接进入
`LayerData -> CreateLayerGameObject()`。改动只发生在 Unity Editor 生成阶段，没有新增 Editor 生命周期回调、
DB、Save、RemoteConfig、运行时单例、全局事件或移动平台分支。

### 验证证据

- 使用 PowerShell 递归解析用户样例，确认 12 个非组节点携带 `rotation`，取值覆盖 `6、4、2、0、-2`。
- CodeGraph 复核确认 `CreateLayerGameObject()` 同时由 PSD 节点路径和旧 Artboard 路径调用，当前没有覆盖测试。
- `dotnet build .\Assembly-CSharp-Editor.csproj --no-restore --verbosity minimal` 通过，结果为
  `0 error / 1128 warning`；warning 为工程既有项。
- 定向 `git diff --check` 通过，仅输出工作区行尾转换提示。

### 剩余风险与未验证项

- 尚未在 Unity Editor 中用该样例实际生成 Prefab 并与 PSD 视觉逐项比对，正负角度最终观感仍需人工确认，
  此项为 `uncertain`。
- 当前样例没有组节点 `rotation`，本次只对非组图层应用角度；若后续导出器为组节点提供旋转，其边界、
  子节点坐标与是否应继承旋转仍需另行验证。
- 没有改动运行时代码；Android/iOS 横屏不受直接影响，但未执行平台构建或真机验证。

### 回滚路径

删除两个模型中的 `Rotation` 属性、`BuildLayerDataFromPsd()` 的角度传递，并把
`CreateLayerGameObject()` 的 `localRotation` 恢复为 `Quaternion.identity`，即可恢复旧生成行为。

## 2026-08-25：显式 rotation 图片使用 Simple 与 Native Size

### 需求与背景

图片在 PSD 中旋转后，JSON 的 `bounds` 可能是旋转后的轴对齐包围盒，与 Sprite 原始像素尺寸不同。旧图片生成
规则会把这种尺寸差识别为需要九宫格拉伸并切换为 `Image.Type.Sliced`，导致旋转图片被拉伸。当前要求是只要
图片 JSON 明确包含 `rotation` 字段，就使用 `Simple` 和 Sprite 原生尺寸。

### 影响范围

- `Models/PsdExportModels.cs` 与 `Models/LayerData.cs`：`Rotation` 改为 `float?`，保留字段缺失与显式 0 的差异。
- `UIGenerator.cs`：旋转角缺失时仍按 0 度设置；图片生成依据 `Rotation.HasValue` 选择渲染与尺寸策略。
- 不修改没有 `rotation` 字段的图片、Sprite 查找、文本/纯色层组件、组层级、Prefab 保存、运行时 UI、存档、
  配置、资源地址、事件、analytics 或平台流程。

### 原有行为基线

`Rotation` 原为不可空 `float`，无法区分 JSON 未提供字段和显式 `rotation: 0`。`CreateImageLayer()` 在 JSON 布局尺寸
与 Sprite 像素尺寸不一致时优先设置 `Image.Type.Sliced`；旋转后的包围盒尺寸差会误触发该规则。

### 修改后行为

`PsdLayerNode.Rotation` 与 `LayerData.Rotation` 使用可空值传递字段存在性。图片带显式 `rotation` 时，
`CreateImageLayer()` 强制设置 `Image.Type.Simple` 并调用 `image.SetNativeSize()`；该分支优先于尺寸不匹配、Button、
Toggle 和 Sprite border 的 Sliced 判断。未带字段的图片继续完整沿用原有规则。RectTransform 角度仍使用
`-Rotation.GetValueOrDefault()`，保持 PS/Unity 坐标系反向转换与旧数据 0 度兼容。

### 数据与流程边界

Json.NET 通过可空属性记录 `rotation` 是否出现，经 PSD 节点或旧 Artboard 图层进入 `LayerData`。非组图片先由
`CreateImageLayer()` 完成 Sprite、Simple 与 Native Size 初始化，再由 `CreateLayerGameObject()` 最后写入角度。
只有成功加载 Sprite 且角度字段存在时才执行 Simple 与 Native Size；Sprite 加载失败仍使用原有占位符流程。

### 验证证据

- CodeGraph 复核确认图片只通过 `CreateLayerGameObject() -> CreateImageLayer()` 进入类型与尺寸选择，当前没有覆盖测试。
- `dotnet build .\Assembly-CSharp-Editor.csproj --no-restore --verbosity minimal` 通过，结果为
  `0 error / 1140 warning`；warning 为工程既有项。
- 三个代码文件的定向 `git diff --check` 通过，仅输出工作区行尾转换提示。

### 剩余风险与未验证项

- 当前 `StartPanel.json` 的 31 个 `LayerKind.NORMAL` 图片均未携带 `rotation`，尚无仓库内实际旋转图片样例用于生成；
  Unity Editor 中的 Simple 类型、Native Size、角度、中心点和最终视觉对齐仍为 `uncertain`。
- 显式 `rotation: 0` 也会按用户要求进入 Simple + Native Size；如果导出器未来给所有图片无条件输出 0，可能改变原本
  依赖 Sliced 拉伸的图片，需要导出契约或实际样例回归确认。
- Android/iOS 横屏不受直接运行时代码影响，但未执行平台构建或真机验证。

### 回滚路径

将两个 `Rotation` 属性恢复为不可空 `float`，把角度写入恢复为 `-layer.Rotation`，并删除
`CreateImageLayer()` 的 `hasExplicitRotation` 分支，即可恢复原有图片类型与尺寸判断。

## 2026-08-27：文字旋转方向与字体 Max 高度修正

### 需求与背景

实际生成结果中，文字图层沿用图片等非文字图层的角度取反规则后旋转方向不正确；同时文字已开启自动字号，
但 TMP 的 `fontSizeMax` 和旧版 Text 的 `resizeTextMaxSize` 仍取导出字号，而不是生成图层的 Height。

### 影响范围

- `UIGenerator.cs`：仅调整非组图层创建时的文字旋转符号，以及 TMP/旧版 Text 自动字号 Max。
- 图片、形状、组节点、锚点、尺寸、位置、字体资源查找、Prefab 保存、运行时 UI、存档、配置、资源、事件、
  analytics 与平台流程不变。

### 原有行为基线

`CreateLayerGameObject()` 对文字、图片和形状统一使用 `-Rotation.GetValueOrDefault()`；`CreateTextLayer()` 把自动字号
Max 设为导出 `fontSize` 或 24 的回退值，因此 Max 不随生成文字框高度变化。

### 修改后行为

文字图层使用 `+Rotation.GetValueOrDefault()`，非文字图层继续使用原有负号转换。TMP 的 `fontSizeMax` 设为
`Mathf.Max(1f, layer.Height)`；旧版 Text 的 `resizeTextMaxSize` 使用相同 Height 四舍五入后的正整数值。
当前 `ConvertSize()` 直接返回 JSON 宽高，所以该 Height 与生成 RectTransform 的 `sizeDelta.y` 一致。

### 数据与流程边界

PSD 与旧 Artboard JSON 仍通过 `LayerData` 进入 `CreateLayerGameObject()`；仅当 `LayerData.IsTextLayer` 为真时切换角度
符号，之后由 `CreateTextLayer()` 写入自动字号上下限。没有新增 Editor 生命周期回调、DB、Save、RemoteConfig、
运行时单例、全局事件或移动平台分支。

### 验证证据

- CodeGraph 确认 `CreateLayerGameObject()` 是 PSD 与旧 Artboard 非组图层共用入口，当前没有覆盖测试。
- 静态检查确认文字角度使用正号、非文字角度仍使用负号；TMP 与旧版 Text 的 Max 均来自 `layer.Height`。
- 首次 `--no-restore` 构建因缺少 `Temp/obj/Assembly-CSharp-Editor/project.assets.json` 未进入编译；执行
  `dotnet restore .\Assembly-CSharp-Editor.csproj` 后，`dotnet build .\Assembly-CSharp-Editor.csproj --no-restore
  --verbosity minimal` 通过，结果为 `0 error / 1142 warning`，warning 为工程既有项。

### 剩余风险与未验证项

- 尚未在 Unity Editor 中重新生成 Prefab，并逐项核对正负角文字的视觉方向与 Auto Size Max Inspector 值，
  此项为 `uncertain`。
- Height 小于 1 时 Max 会夹到 1，避免 TMP/旧版 Text 获得无效的非正字号；仓库样例中未验证该异常输入。
- 改动只发生在编辑器生成代码；未执行 Android/iOS 构建或真机验证。

### 回滚路径

把 `CreateLayerGameObject()` 的角度恢复为统一 `-layer.Rotation.GetValueOrDefault()`，并把 TMP/旧版 Text 的 Max
恢复为各自的 `text.fontSize`，即可恢复本次修改前的生成行为。

## 2026-09-02：平移 PSD Text Outline 与 TMP 材质生成能力

### 需求与背景

当前 `feature/1.14.0_common` 分支不包含 `feature/remote_loading` 提交 `5149353c3` 中的文本描边能力。
本次按用户要求把该能力平移到当前 `UIGenerator`，但不带入同一历史提交中的图层创建策略重构，避免覆盖当前
分支已经存在的文字旋转、字号、图片 Native Size 和交互组件逻辑。

### 影响文件与系统

- `Models/PsdExportModels.cs`：映射 PSD 文本节点的 `outline.enabled/color/width/widthUnit`。
- `Models/LayerData.cs`：为 PSD 与旧 Artboard 入口增加共用的 `TextOutlineData`。
- `UIGenerator.cs`：传递描边数据，在 TMP 文字创建完成后复用或创建字体材质，并绑定到
  `TextMeshProUGUI.fontSharedMaterial`。
- 没有修改现有字体资产、Prefab、`UIGeneratorSettings.asset`、运行时 UI、存档、配置、资源加载、事件、
  analytics 或平台 SDK。

### 原有行为基线

当前分支的 PSD 和通用图层模型都没有 Outline 字段。Json.NET 会忽略 JSON 中的 `outline`，`CreateTextLayer()`
只设置字体、字号、样式、文案与文字颜色，最终继续使用字体基础材质，不会创建或绑定描边材质。

### 修改后行为

PSD 入口把根级 `outline` 转换到 `TextInfoData.Outline`；旧 Artboard JSON 也可通过
`textInfo.outline` 直接反序列化。仅当 `enabled=true`、宽度大于 0、颜色有效且当前使用 TMP 时应用描边。

生成器优先在实际字体所在目录查找与基础材质使用同一主纹理、且描边颜色和换算宽度相同的材质；命中时复用，
否则复制基础材质并设置 TMP `_OutlineColor`、`_OutlineWidth`，调用 `ShaderUtilities.UpdateShaderRatios()` 后
通过 `AssetDatabase.CreateAsset()` 创建 `{字体名} with {RGB} outline.mat`。字体目录不在可写的 `Assets/`
范围时只输出警告并保持基础材质，避免改写 Package 内容。

`widthUnit=px` 时使用 `width / _GradientScale` 换算并夹到 0–1；其它单位或空单位按 TMP 归一化宽度夹到
0–1。未启用、零宽、无效颜色、缺字体或缺基础材质时保持原有基础材质。

### 数据、资源与 UI 流

`PSD JSON outline -> PsdTextOutlineData -> TextOutlineData -> CreateTextLayer() -> 实际 TMP 字体目录 ->`
`复用/创建 Material -> TextMeshProUGUI.fontSharedMaterial`。该链路只在用户主动执行 UIGenerator 的 Editor
生成阶段运行，不新增 `OnValidate`、`Reset`、编辑器预览回调、DB、Save、RemoteConfig 或运行时单例访问。

### 验证证据

- 定向 `git diff --check` 通过，仅有工作区 LF/CRLF 转换提示。
- 静态扫描确认两个 JSON 模型入口、PSD 转换、TMP 应用、材质复用/创建和 `fontSharedMaterial` 绑定链路完整。
- `dotnet build .\Assembly-CSharp-Editor.csproj --no-restore --verbosity minimal` 通过，结果为
  `0 error / 1144 warning`；warning 为工程既有项。
- 当前分支未找到含 `"outline"` 的 UI JSON 样例，因此没有生成新的 `.mat` 文件，也未执行 Unity Editor
  实际生成与视觉比对。

### 剩余风险与未验证项

- Unity Editor 中的实际材质创建路径、Inspector 引用、材质复用和视觉一致性仍为 `uncertain`。
- 当前仓库部分旧 Artboard JSON 使用与 `textInfo` 同级的 `stroke` 字段；本次按历史功能平移，仅支持
  `outline` / `textInfo.outline` 契约，`stroke` 不会自动转换为 TMP Outline。
- 旧版 Unity UI `Text` 分支保持原行为，不生成 TMP 描边材质。
- 规则中声明的 `specs/037-remote-resource-loading/plan.md` 与 `../specs/037-remote-resource-loading/plan.md`
  在当前工作区均不存在，因此无法核对该 feature plan；本次以现有模块文档和历史提交为证据边界。
- 改动只发生在 Editor 生成工具，未执行 Android/iOS 构建或真机横屏验证。

### 回滚路径

删除两个模型中的 Outline 类型和属性、PSD Outline 转换、`CreateTextLayer()` 的 `ApplyTextOutline()` 调用，
以及材质查找/创建辅助方法，即可恢复为忽略 Outline 并统一使用字体基础材质的原行为；无需迁移 Prefab、存档或配置。

## 2026-09-02：图片图层 opacity 映射到 Image Alpha

### 需求与背景

UI JSON 的图片图层包含根级 `opacity`（0–100），但原生成链路没有把它写入 Unity `Image`。这会导致半透明图片
在生成 Prefab 后变成组件 Alpha 为 1 的显示效果。本次补充该参数，同时区分“JSON 未提供字段”和“显式传入 0”。

### 影响文件与系统

- `Models/PsdExportModels.cs`：把 PSD 图层的 `opacity` 改为可空百分比字段。
- `Models/LayerData.cs`：为通用图层增加可空的根级 `Opacity`。
- `UIGenerator.cs`：在 PSD 转换时传递该字段，并在普通图片、交互图片和纯色 Shape 创建的 `Image` 上应用 Alpha。
- 没有修改图片文件、Sprite 导入设置、Prefab、运行时 UI、存档、配置、资源加载、事件或平台 SDK。

### 原有行为基线

PSD 模型虽能反序列化 `opacity`，但只把它传给文字数据；通用 `LayerData` 没有图片图层的根级字段。
`CreateImageLayer()` 与 `CreateShapeLayer()` 均不会根据 JSON 调整 `Image.color.a`。Sprite 自身的逐像素 Alpha 会保留，
但图层整体不透明度会丢失。

### 修改后行为

`LayerData.Opacity` 与 `PsdLayerNode.Opacity` 使用 `float?` 表示 0–100 的整体不透明度。字段缺失时保持组件原有
颜色 Alpha；显式 `0` 会生成完全透明的 `Image`。PSD 转换只接受 0–100 的值，越界值按缺失处理。

创建图片或 Shape 时执行 `baseColor.a *= opacity / 100`。因此加载到 Sprite 后默认白色的组件 Alpha、缺图占位色
以及 Shape 默认色都会保留各自已有 Alpha，再乘以图层百分比；Sprite 纹理的逐像素 Alpha 不会被覆盖。
旧 Artboard JSON 可直接把根级 `opacity` 反序列化到 `LayerData`，其越界值在应用时夹到 0–100。

### 数据、资源与 UI 流

`PSD/Artboard JSON opacity -> PsdLayerNode.Opacity / LayerData.Opacity -> CreateImageLayer() 或 CreateShapeLayer()`
`-> Image.color.a`。该链路仅在用户主动运行 UIGenerator 的 Editor 生成阶段执行，不新增 `OnValidate`、`Reset`、
编辑器预览回调、DB、Save、RemoteConfig 或运行时单例访问。

### 验证证据

- 扫描 `Assets/Game/UI` 下 JSON，共识别 1049 个 `opacity` 数值，范围为约 `3.1373` 至 `100`；其中包含
  `20`、`50`、`66`、`80` 等非 100 图片图层样例，证明百分比契约不是仅有默认值。
- 定向静态检查确认 PSD 转换、普通/交互图片和 Shape 三条 `Image` 创建路径均接入 Alpha 应用方法。
- 定向 `git diff --check` 通过，仅有工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp-Editor.csproj --no-restore --verbosity minimal` 通过，结果为
  `0 error / 1144 warning`；warning 为工程既有项。

### 剩余风险与未验证项

- 尚未在 Unity Editor 中用半透明图片 JSON 重新生成 Prefab，并检查 Inspector 与最终视觉，结果为 `uncertain`。
- 本次只处理创建为 Unity `Image` 的图层；Group 的整体透明度和 `solidfill.opacity` 不在本次范围内。
- 改动只发生在 Editor 生成工具，未执行 Android/iOS 构建或真机横屏验证。

### 回滚路径

删除两个模型的根级可空 `Opacity`、PSD 转换中的赋值，以及 `CreateImageLayer()` / `CreateShapeLayer()` 的
`ApplyLayerOpacity()` 调用和辅助方法，即可恢复为忽略图片图层整体不透明度的原行为；无需迁移已有 JSON、Prefab、
存档或配置。

## 2026-09-02：图片 rotation 最终落地与 GiftPopup 产物修正

### 需求与背景

`ChestGiftPopup.json` 中有 4 个非组图片携带非零 `rotation`，但当前 Prefab 只有
`UI_common_icon_jinbi_01_2` 保留了角度；气泡和两枚装饰金币的 RectTransform 均被保存为单位旋转。
本次修正 UIGenerator 的角度落地时序，并同步校正当前 GiftPopup 产物。

### 影响文件与系统

- `UIGenerator.cs`：把非组图层的最终 `localRotation` 写入移动到 Text/Image/Shape 组件初始化之后。
- `Assets/Game/UI/GiftPopup/Prefabs/ChestGiftPopup.prefab`：校正 3 个缺失角度的图片 RectTransform。
- 不修改 JSON、Sprite、图片导入设置、组节点角度、运行时脚本、存档、配置、资源地址、事件或平台 SDK。

### 原有行为基线

生成器先写入 `RectTransform.localRotation`，随后才进入 `CreateImageLayer()`；显式带 rotation 的图片会在其中执行
`Image.SetNativeSize()`。现有 GiftPopup 产物表明该时序没有稳定保存全部图片角度：气泡 `-26.4025`、金币
`-48` 和 `-119.4892` 在 Prefab 中均为 0。缺少当次 Unity 生成日志，具体由哪一步覆盖仍为 `uncertain`。

### 修改后行为

组件类型、Sprite、Native Size 与图片类型全部初始化完成后，再统一把 JSON 角度写入 RectTransform。文字仍使用
`+rotation`，图片和 Shape 仍使用 `-rotation`，只调整落地时序，不改变既有坐标系约定。

当前 GiftPopup 同步修正为：气泡 `-26.4025 -> Unity +26.4025`、金币 `-48 -> +48`、金币副本
`-119.4892 -> +119.4892`；原本正确的 `UI_common_icon_jinbi_01_2` 继续保持 `+56.5829`。

### 数据、资源与 UI 流

`JSON rotation -> PsdLayerNode/LayerData -> 创建 Text/Image/Shape -> Image Simple/Native Size ->`
`RectTransform.localRotation`。代码链仅在用户主动执行 UIGenerator 的 Editor 生成阶段运行；Prefab 修正只改变
3 个已有图片节点的本地旋转。

### 验证证据

- 递归解析 `ChestGiftPopup.json`，并从 Prefab quaternion 反算 Z 轴角度；4 个非零图片全部满足
  `Prefab Unity Z = -JSON rotation`，误差小于 `0.001`。
- 定向差异复核确认 Prefab 只修改 3 个目标 RectTransform，没有改动其它节点、组件或资源引用。
- `dotnet build .\Assembly-CSharp-Editor.csproj --no-restore --verbosity minimal` 通过，结果为
  `0 error / 1144 warning`；warning 为工程既有项。

### 剩余风险与未验证项

- 尚未在 Unity Editor 中重新运行一次完整 JSON 生成并目视检查，最终 Inspector 与画面结果仍为 `uncertain`。
- Group 的 rotation 仍按单位旋转处理，不属于本次修复范围。
- 未执行 Android/iOS 构建或真机横屏验证；本次没有修改运行时代码。

### 回滚路径

把 `localRotation` 写入移回组件创建之前，并将 GiftPopup 三个目标 RectTransform 恢复为单位 quaternion，即可恢复
本次修改前状态；无需迁移 JSON、存档或配置。

## 2026-09-03：Outline 材质等价复用加固

### 需求与背景

UIGenerator 生成 TMP 描边文字时，相同 Outline 材质应复用已有资产，不应继续创建带数字后缀的重复 `.mat`。
本次只加固 Editor 生成阶段的材质检索和创建边界，不清理或改写现有材质。

### 影响文件与系统

- `UIGenerator.cs`：增加单次生成任务内的 Outline 材质缓存、固定目标路径复核和统一等价判定。
- 不修改 JSON 模型、字体资产、现有材质、Prefab、运行时 UI、存档、配置、资源加载、事件或平台 SDK。

### 原有行为基线

原实现会在字体目录中通过 `AssetDatabase.FindAssets()` 检索同主纹理、同描边颜色和同宽度的材质；未命中后直接
使用 `AssetDatabase.GenerateUniqueAssetPath()` 创建资产。新建材质没有进入当前生成任务的显式缓存，固定命名路径
也没有在创建前单独同步导入和复核。若 AssetDatabase 当轮检索没有及时返回刚生成的资产，存在继续创建数字后缀
副本的窗口；当前没有 Unity 生成日志可直接复现该时序，因此具体触发条件仍为 `uncertain`。

### 修改后行为

每次 `GenerateUIPrefab()` 开始时清空任务级缓存。查找顺序为：本轮已命中/创建缓存、字体目录 AssetDatabase
检索、固定 `{字体名} with {RGB} outline.mat` 路径同步导入复核；任一层命中都直接返回已有材质。新建成功后立即
加入本轮缓存，因此同一次 JSON 中重复的描边配置不依赖 AssetDatabase 索引刷新。

材质仅在 shader、TMP 主纹理、Outline RGBA 颜色和换算后的 Outline 宽度均相同时视为等价；不同字体图集、
不同 shader、颜色或宽度不会误复用。固定路径已被非等价材质占用时，仍保留唯一后缀创建行为，避免覆盖用户资产。

### 数据、资源与 UI 流

`JSON outline -> TMP 字体与基础材质 -> 本轮缓存 -> 字体目录 AssetDatabase -> 固定路径复核 ->`
`命中则 fontSharedMaterial 复用 / 未命中才 CreateAsset`。链路仅在用户主动执行 UIGenerator 的 Editor 生成阶段运行，
不新增 `OnValidate`、`Reset`、编辑器预览回调、DB、Save、RemoteConfig 或运行时单例访问。

### 验证证据

- 定向静态检查确认缓存查找、字体目录查找、固定路径复核、新建后立即缓存和统一等价判定五个环节均已接入。
- 扫描 `Assets/Game/ArtSrc/Font`，当前没有文件名以数字结尾的 `*outline*.mat` 副本；本次未删除任何材质。
- `git diff --check` 通过，仅有工作区 LF/CRLF 转换提示。
- `dotnet build .\Assembly-CSharp-Editor.csproj --no-restore --verbosity minimal` 中 `com.bear.ui` 已成功编译；完整构建
  被工作区并行新增的 `CoinGiftPopup.Generated.cs` 3 个重复字段 `CS0102` 阻断，本次 UIGenerator 文件没有编译错误。

### 剩余风险与未验证项

- Unity 当前正在打开该工程，未另启批处理 Editor 重跑包含重复 Outline 的 JSON；实际连续生成两次后的资产数量和
  Prefab 引用检查仍为 `uncertain`。
- 等价检索继续限定在字体资产所在目录，避免跨模块复用外部目录中可能由人工维护的材质。
- 本次只改变 Editor 资产生成行为，未执行 Android/iOS 构建或真机横屏验证。

### 回滚路径

删除 `_outlineMaterialCache`、固定路径 `LoadMaterialAsset()` 复核和 `IsMatchingOutlineMaterial()` 统一判定，并把
`ApplyTextOutline()`、`FindOrCreateOutlineMaterial()`、`FindMatchingOutlineMaterial()` 恢复为静态方法，即可回到
仅依赖字体目录 AssetDatabase 检索并用唯一后缀创建的原行为；无需迁移 JSON、Prefab、存档或配置。

## 2026-09-07：旋转图片保留 JSON 尺寸并取消类型强制

### 需求与原行为基线

- 显式带 `rotation` 的图片原先会强制使用 `Image.Type.Simple`，随后调用 `Image.SetNativeSize()`；该调用会用 Sprite 原生尺寸覆盖已经从 JSON `width/height` 写入的 RectTransform 尺寸。
- 实际生成结果表明 Native Size 会造成图片尺寸与设计稿预期尺寸不一致；后续生成的带 `rotation: 0` 按钮仍因 rotation 特殊分支被强制为 Simple，导致有 Border 的按钮没有生成 Slice。用户明确要求 UIGenerator 不再调用 Native Size，也不再按 rotation 强制 Image 类型。

### 修改后行为与影响范围

- 所有 Sprite 加载成功的图片统一执行同一套类型判断，不再读取 `Rotation.HasValue` 决定 Image 类型：布局尺寸与 Sprite 尺寸不一致时使用 `Sliced`，Button/Toggle 且 Sprite 有 Border 时使用 `Sliced`，否则使用 `Simple`。
- RectTransform 保留 `CreateLayerGameObject()` 根据 JSON `width/height` 设置的布局尺寸；`rotation` 只在组件初始化完成后按既有正负号规则写入最终角度，不再影响图片尺寸或类型。
- `UI_common_btn_yellow_01` 的 JSON 明确带 `rotation: 0`，Sprite Border 为 left=47/right=64；按新规则会进入 Button/Border 的 `Sliced` 分支。
- 本次只改变 Editor 图片生成策略，不修改 JSON 模型、Sprite 导入设置、已有 Prefab、运行时 UI、存档、配置、资源加载、事件、analytics 或平台 SDK。旧注释因已与代码行为冲突而同步改写。

### 验证、剩余风险与回滚

- 静态检查确认 UIGenerator 已无 `SetNativeSize()` 和 `hasExplicitRotation` 图片类型分支；所有图片共用现有 Simple/Sliced 判断，最终角度仍在组件初始化之后写入。
- `dotnet build Assembly-CSharp-Editor.csproj --no-restore` 通过，结果为 `0 error / 4 warning`；warning 来自现有 `BuildToolV2.cs`。首次跳过依赖的构建因缺少 `Assembly-CSharp-Editor-firstpass.dll` 未进入本次代码编译，包含项目依赖后已补齐并验证通过。
- `uncertain`：需在 Unity Editor 中重新生成 Prefab，核对 `rotation: 0` 且有 Border 的按钮恢复 Sliced、RectTransform 尺寸等于 JSON `width/height`，并检查非零旋转图片进入通用 Sliced 判断后的视觉；现有历史 Prefab 不会被自动迁移。
- 回滚时恢复 `hasExplicitRotation` 分支，并按所需旧行为重新加入强制 Simple 或 `SetNativeSize()`；无需迁移 JSON、配置或存档。
