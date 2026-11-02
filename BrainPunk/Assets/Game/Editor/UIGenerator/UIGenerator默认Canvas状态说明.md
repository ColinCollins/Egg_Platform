# UIGenerator 默认 Canvas 状态说明

## 需求背景

2026-07-01 调整 UIGenerator 生成 UI prefab 后的项目后处理规则，使新生成 prefab 根 Canvas 默认对齐现有 `GameSettingPopup` 展示状态。

本次只修改编辑器生成工具默认值，不修改运行时 UIManager、已有业务 UI 脚本、已生成 prefab、资源加载、存档、配置、广告、IAP、analytics 或平台 SDK。

## 影响范围

- `Assets/Game/Editor/UIGenerator/UIGeneratorGamePostProcessor.cs`
  - `IUIGeneratorPostProcessor.OnPostProcessPrefab(...)` 后处理入口。
  - `EnsureCanvasUtilityComponents(...)` 统一根 Canvas 默认状态。
- 参考证据：
  - `Assets/Game/UI/SettingPopup/Sprites/GameSettingPopup.prefab` 根 Canvas 当前为 `Screen Space Overlay`、`sortingOrder=100`、根 RectTransform scale 为 0，是旧生成结果状态。
  - `Assets/Game/UI/SettingPopup/Resources/GameSettingPopup.prefab` 根 Canvas 当前为 `World Space`、`sortingOrder=301`、`additionalShaderChannels=25`、`vertexColorAlwaysGammaSpace=1`、根 RectTransform stretch 且 scale 为 1，是本次对齐目标。

## 原行为基线

生成器基础代码会创建带 `Canvas` 的 prefab 根节点；项目后处理此前将 Canvas 设置为 `ScreenSpaceOverlay`，开启 `overrideSorting`，并将 `sortingOrder` 设置为 100。

旧生成结果在 Inspector 中可能表现为 Overlay/Order 100，且根 RectTransform 不符合截图所示 stretch/scale 1 状态。

## 新行为

UIGenerator 保存 prefab 后，项目后处理会对根 Canvas 应用以下默认状态：

- 根对象 Layer 优先设置为项目 `UI` Layer；若项目未定义该 Layer，则保持原 Layer 并不中断生成。
- 根 RectTransform 设置为 stretch/stretch，Left/Right/Top/Bottom 为 0，Pivot 为 `(0.5, 0.5)`，Scale 为 `(1, 1, 1)`。
- Canvas Render Mode 设置为 `World Space`，Event Camera 为空。
- `overrideSorting=true`，Sorting Layer 保持 Default，Order in Layer 设置为 301。
- Additional Shader Channels 设置为 `TexCoord1 | Normal | Tangent`，对应序列化值 25。
- `vertexColorAlwaysGammaSpace=true`。
- 根节点缺少 `CanvasRenderer` 或 `GraphicRaycaster` 时补齐；`GraphicRaycaster` 保持 `ignoreReversedGraphics=true`、`blockingObjects=TwoD`。
- `_btn` 节点继续沿用既有后处理，自动补 `ClickVibration`。

## 数据、资源和流程

本次不新增配置文件、不修改 `UIGeneratorSettings.asset`，也不改变 JSON、PSD、Sprite、Font 查找流程。

后处理仍只在 Unity Editor 的 UIGenerator 生成流程中执行；运行时打开 UI 仍由 `UIManager` 和各业务 `Create(...)` 入口负责。

## 验证记录

- 已静态检查 `Assets/Game/Editor/UIGenerator/UIGeneratorGamePostProcessor.cs`，确认只改编辑器后处理默认状态。
- 已静态对照 `Assets/Game/UI/SettingPopup/Sprites/GameSettingPopup.prefab` 和 `Assets/Game/UI/SettingPopup/Resources/GameSettingPopup.prefab` 的根 Canvas 序列化字段。
- 已从 Unity 6000.0.59f2 本地 `UnityEngine.UIModule.xml` 和 UGUI 源码静态确认 `Canvas.additionalShaderChannels`、`Canvas.vertexColorAlwaysGammaSpace` 属性存在；PowerShell `Add-Type` 独立编译探针因宿主 .NET 引用冲突未作为有效编译证据。
- Unity batchmode 编译验证未完成：当前项目已有 Unity 实例打开，batchmode 日志返回 “Project already open”，未进入脚本编译阶段。

## 剩余风险

- 尚未在 Unity Editor 菜单中重新执行 `Tools/UI Generator` 或右键 JSON 生成流程，需补验新生成 prefab Inspector 是否完全匹配截图。
- 未验证已有打开的 Unity 实例完成脚本重新编译后的 Console 状态。
- 本次默认把新生成根 Canvas 改为 World Space；如果某个新 UI 明确需要 Overlay，应在该 prefab 生成后按具体需求覆盖。

## 回滚路径

回滚 `Assets/Game/Editor/UIGenerator/UIGeneratorGamePostProcessor.cs` 中 `ApplyDefaultCanvasState(Canvas)` 的默认值，恢复 `RenderMode.ScreenSpaceOverlay` 和 `sortingOrder=100`，并移除 RectTransform、CanvasRenderer、Additional Shader Channels、Gamma 默认设置即可恢复旧生成行为。
