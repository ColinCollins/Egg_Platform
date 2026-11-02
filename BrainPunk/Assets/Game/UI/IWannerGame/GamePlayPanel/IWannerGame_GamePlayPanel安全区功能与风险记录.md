# IWannerGame GamePlayPanel 横向安全区功能与风险记录

## 需求与背景

- 需求：在 `IWannerGame_GamePlayPanel` 上检测移动设备横向安全区，并把左右 inset 应用到全拉伸的 `Root`。
- `Assets/GF/Scripts/Runtime/UI/UIBase.cs` 通过 `transform.Find("root")` 查找小写根节点，而当前 prefab 使用大写 `Root`，因此既有自动 `SafeAreaResize()` 不会命中该节点。
- `Assets/GF/Scripts/Runtime/UI/Aspect/SafeAreaSizer.cs` 只从 `Screen.safeArea` 计算上下 inset；其左右参数来自平板固定 Padding，不等同于横屏刘海的左右安全区。本次不修改该共享实现，避免扩大其它 UI 的回归范围。

## 影响文件与系统

- 新增 `Scripts/IWannerGame_GamePlayPanelSafeArea.cs` 及其 `.meta`。
- 修改 `Resources/IWannerGame_GamePlayPanel.prefab`：仅在面板根对象挂载一个上述组件，`root` 绑定到子节点 `Root`，`leftOffset/rightOffset` 默认均为 `0`；恢复编辑器后发现并移除了 `Root` 自身的重复组件。
- 仅影响 IWanner 运行时 HUD 的横向 UI 布局；不新增或修改配置、存档、资源加载、全局事件、分析打点、广告、支付和平台 SDK 流程。
- prefab 在本次任务开始前已有进度节点锚点调整；本次未覆盖或改写该既有改动。

## 修改前行为基线

- `Root` 左右拉伸，水平 `offsetMin.x/offsetMax.x` 均为 `0`。
- HUD 没有基于 `Screen.safeArea.xMin/xMax` 的专用横向缩进逻辑。
- `BlackMask` 与 `ClickBlock -------------------` 等全拉伸节点均位于 `Root` 下。

## 修改后行为

- 组件仅在 `Application.isPlaying` 时运行，首次启用时保存 `Root` 原始左右 offset。
- 左侧 inset 使用 `Screen.safeArea.xMin`，右侧 inset 使用 `Screen.width - Screen.safeArea.xMax`；按 `Root` 父节点宽度与屏幕像素宽度的比例换算成 UI 本地单位。
- 最终值为“原始 offset + 安全区 inset + Inspector 额外 offset”。左侧写入 `offsetMin.x`；右侧以向内为正方向写入 `offsetMax.x` 的负值。上下 offset 保持不变。
- 在 `OnEnable`、`Start`、RectTransform 尺寸变化、应用恢复焦点或从暂停恢复时重算；不使用 `Update` 逐帧轮询。
- RectTransform 尺寸回调重入期间直接退出；计算结果与当前左右 offset 一致时不再写入，避免布局赋值再次触发同一回调。
- `leftOffset/rightOffset` 支持正负值：正值继续向内收缩，负值允许向安全区外扩展。

## UI 与平台流

1. HUD 实例启用并完成基础层级建立。
2. 组件读取当前 `Screen.width` 与 `Screen.safeArea`。
3. 使用面板 RectTransform 宽度把像素 inset 换算到本地 UI 坐标。
4. 只更新已绑定 `Root` 的水平 offset，所有 `Root` 子节点随其布局一起移动或收缩。
5. Android/iOS 共用 Unity `Screen.safeArea`；未加入平台分支。

## 验证证据

- 静态确认 prefab 上组件、脚本 GUID、`Root` 引用及两个默认 `0` offset 均已序列化。
- 使用 Unity 6000.0.59f2 对应的 `UnityEngine.CoreModule.dll` 执行 Roslyn 定向编译：`0 error`；3 个警告均为独立编译器无法识别 Unity Inspector 注入而产生的序列化字段未赋值警告。
- 静态确认脚本没有 `Update`、`OnValidate`、`Reset`、`ExecuteAlways`、DB、Save、资源加载、实例创建或全局事件链路。
- 初版 PlayMode 日志直接证明 `OnRectTransformDimensionsChange -> offsetMin/offsetMax -> OnRectTransformDimensionsChange` 无限递归；BrainPunk Unity 主进程无响应，日志一度以约 `5 MB/s` 增长。最终实现已增加同步重入保护和异步/后续回调的目标值不变保护。
- BrainPunk Unity 重新载入修复脚本后完成 Tundra/AssetDatabase 脚本编译和程序集重载，最近日志中无 `error CS`、`Compilation failed` 或 `StackOverflowException`。
- `git diff --check` 作为最终校验执行。

## 剩余风险与未验证项

- `uncertain`：尚未在 Unity PlayMode、Device Simulator、Android 刘海/打孔横屏和 iOS 横屏真机验证实际 inset、触控区域及切后台恢复。
- `uncertain`：当前换算假设运行时面板父 RectTransform 横向覆盖完整屏幕；若 UI 相机或父级 viewport 不是全屏，需按实际 viewport 再校准。
- `BlackMask` 与 `ClickBlock -------------------` 是 `Root` 的全拉伸子节点，因此会一同收进安全区；这是按“调整 Root 左右 offset”实现的直接结果。黑幕边缘覆盖与安全区外触摸是否符合最终体验仍需 PlayMode/真机确认。
- 为避免常驻开销，本组件不会逐帧比较 `Screen.safeArea`。若运行时安全区改变但没有触发尺寸、焦点或暂停恢复回调，本帧不会自动重算。
- `uncertain`：修复后的脚本已通过定向编译、Unity 工程编译和静态调用链复核，但尚未重新进入 PlayMode 执行 HUD 打开与横屏安全区回归。

## 回滚路径

- 从 `IWannerGame_GamePlayPanel.prefab` 移除 `IWannerGame_GamePlayPanelSafeArea` 组件。
- 删除新增脚本及 `.meta`；不需要回滚存档、配置、资源地址或事件订阅。
- prefab 中原有的进度节点锚点改动不属于本功能，回滚安全区时必须保留。

## 2026-08-17 递归问题修复记录

- 原因：初版在 `OnRectTransformDimensionsChange()` 内无条件写入 `Root.offsetMin/offsetMax`，Unity 的 RectTransform setter 同步再次派发布局尺寸回调，形成无限递归。
- 修复：增加 `_isApplying` 重入门禁；写入前分别使用 `Mathf.Approximately` 比较当前值与目标值，没有变化时直接返回；左右两侧只写实际需要更新的一侧，并用 `try/finally` 保证门禁复位。
- Prefab 修复：保留面板对象上的唯一组件及其 `Root` 引用，移除 `Root` 自身的重复组件，避免两个实例同时控制同一 RectTransform。
- 影响：不改变横屏左右安全区计算、Inspector 额外偏移、上下布局、prefab 绑定或其它系统边界，只阻止重复布局写入。
- 验证：修复后 Roslyn 定向编译 `0 error`；Unity 工程脚本编译与程序集重载完成且无新增编译/栈溢出错误；修复后 Unity PlayMode 与 Android/iOS 真机仍待重新验证。
