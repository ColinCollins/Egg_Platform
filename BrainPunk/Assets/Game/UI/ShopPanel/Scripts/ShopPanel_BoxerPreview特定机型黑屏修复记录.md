# ShopPanel BoxerPreview 特定机型黑屏修复记录

## 需求与背景

- 问题现象：一加、Pixel 2 XL、Sony H9493 等特定 Android 机型进入商店皮肤页时，玩家 Boxer 预览区域全黑，表现类似 RenderTexture 未渲染。
- 修复目标：保持现有皮肤目录、试穿、购买、存档、资源加载和页签切换行为不变，只降低 Boxer RenderTexture 对移动 GPU/驱动能力的要求，并补齐底层纹理丢失后的恢复保护。

## 事实与直接结论

### 已确认事实

- `Assets/Game/UI/ShopPanel/boxer.renderTexture` 仅通过同一 GUID 被以下三处引用：
  - `Assets/Game/Scenes/Main.unity` 的 `SkinRt_Camera.targetTexture`；
  - `Assets/Game/UI/ShopPanel/Resources/ShopPanel.prefab` 的 `boxerRenderTexture` 字段；
  - 同一 prefab 的 `Boxer_Rt.RawImage.texture`。
- Git 历史提交 `d66580fa3` 显示，该资源从其它 RenderTexture 复制为 Boxer RT 时把 `m_EnableRandomWrite` 从 `0` 改成了 `1`；当前工程未找到 `Graphics.SetRandomWriteTarget`、计算着色器写入或其它 Boxer RT 随机写调用。
- 修复前 `ShopPanelBoxerPreviewController.SetPreviewActive(...)` 在解析引用后直接启用相机；`ClearRenderTexture()` 通过设置 `RenderTexture.active` 隐式使用 RT，没有检查 `IsCreated()`，也没有读取 `Create()` 的成功结果。
- Unity 官方文档说明 `enableRandomWrite` 用于 Shader Model 5.0 随机访问写入，具体格式是否支持取决于图形 API、硬件和驱动；RenderTexture 的硬件资源也可能丢失，可通过 `IsCreated()` 与 `Create()` 检查和重建。
- 2026-08-05 通过 ADB 连接 OnePlus 5T（ONEPLUS A5010，Android 9 / API 28，Snapdragon 835）复现后，Logcat 明确输出：`RenderTexture.Create failed: depth/stencil format unsupported - D32 SFloat S8 UInt (94)`；随后 `ClearRenderTexture()` 输出 `Failed to set the active render target`。
- 同次 Logcat 中没有出现 Boxer 颜色格式、随机写、Shader 或部件资源加载失败作为 RT 创建阻断；当前直接失败点是 `boxer.renderTexture` 的 `m_DepthStencilFormat: 94`。

### 直接结论

- Boxer 预览链路不需要随机写能力，关闭 `m_EnableRandomWrite` 不改变相机输出到 RawImage 的业务职责，同时减少移动 GPU/驱动的格式能力约束。
- 预览激活前显式确认 RT 已创建，可以覆盖首次创建和底层纹理资源丢失两类状态；创建失败时不启用预览相机，避免缺失 target texture 时相机参与主屏渲染。
- 特定机型黑屏的直接根因已由 OnePlus 5T 真机日志确认：该设备不支持当前 `D32_SFloat_S8_UInt` 深度/模板附件，导致整个 Boxer RT 创建失败。
- Boxer 预览相机只渲染 Layer 2 的 2D Spine 与皮肤部件，URP Camera 配置不请求 Depth Texture 或 Color Texture；该预览不需要深度/模板附件，因此移除附件比改成另一种设备相关的深度格式更符合最小兼容边界。

### 待验证

- 待重新构建并在 OnePlus 5T 验证 `m_DepthStencilFormat: 0` 后 RT 能创建、相机持续输出且 RawImage 不再全黑。
- Pixel 2 XL 与 Sony H9493 尚未接入 ADB；它们的最终修复结果仍为 `uncertain`，但原问题现象和移动 GPU 深度格式兼容边界与 OnePlus 5T 相同，需要使用同一构建复测。

## 影响文件与修复行为

- `Assets/Game/UI/ShopPanel/boxer.renderTexture`
  - `m_EnableRandomWrite: 1 -> 0`；尺寸、颜色格式、过滤、Wrap、Mip、动态分辨率等其它配置不变。
  - 真机日志确认原 `D32_SFloat_S8_UInt` 深度/模板附件不受支持后，`m_DepthStencilFormat: 94 -> 0`；预览不再申请不需要的深度/模板附件，颜色格式、尺寸和采样配置不变。
- `Assets/Game/UI/ShopPanel/Scripts/ShopPanelBoxerPreviewController.cs`
  - 预览激活时调用 `EnsureRenderTextureCreated()`；已创建时直接复用，未创建时显式调用 `Create()`。
  - RT 缺失或创建失败时不启用预览相机，并通过现有 `Bear.Logger` 输出缺失信息，创建失败日志包含 `graphicsDeviceType` 与 `graphicsFormat`。
  - `ClearRenderTexture()` 改为只清颜色，不再请求清理已移除的深度附件。
  - 仅在 RT 可用时执行透明清理；关闭/挂起、部件异步加载、Spine 动作和 preview session 生命周期保持原边界。

## 数据、资源、UI 与平台边界

- 资源/UI 流保持为 `Main.unity/SkinRt_Camera -> boxer.renderTexture -> ShopPanel/Boxer_Rt.RawImage`，没有新增资源地址、实例化、材质、Shader 或 prefab 引用。
- 未修改 `DB.GameData`、Skin 配表、所有权、试穿、应用、Restore、广告、金币、内购、analytics、RemoteConfig 或平台 SDK。
- 未新增 `PlayEvents`、全局事件、编辑器生命周期入口、每帧轮询或平台条件分支。
- Android 与 iOS 共用同一兼容性保护；本次问题设备为 Android，iOS 仍需做基础回归以确认预览行为一致。

## 验证证据

- `git diff --check -- Assets/Game/UI/ShopPanel/boxer.renderTexture Assets/Game/UI/ShopPanel/Scripts/ShopPanelBoxerPreviewController.cs`：通过，仅有工作区 LF/CRLF 提示。
- 静态风险扫描：修改脚本未出现 `OnValidate`、`Reset`、`ExecuteAlways`、`ExecuteInEditMode`、DB/save、RemoteConfig、PlayerPrefs、StreamingAssets、全局事件或 Random Write 调用。
- GUID 静态核对：Boxer RT 仍只绑定到 Main 场景相机、ShopPanel 序列化字段和 `Boxer_Rt.RawImage`，引用链未改变。
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal /clp:ErrorsOnly /p:BuildProjectReferences=false /p:UseSharedCompilation=false /m:1 /nr:false /p:BuildInParallel=false`：通过，`0 error / 101 warning`。
- ADB/Logcat 根因验证：OnePlus 5T 已稳定捕获 `D32_SFloat_S8_UInt (94)` 不支持、RT 创建失败和 active render target 无效的连续错误链，证据直接指向 Boxer RT 深度/模板附件。
- 移除深度/模板附件并调整颜色清理后再次执行上述定向构建：通过，`0 error / 101 warning`。

## 剩余风险与真机复测清单

- 修复后 Android 包尚未完成构建和覆盖安装，OnePlus 5T 的修复结果仍待验证；根因本身已由修复前真机日志确认。
- Android 必测：一加问题机、Pixel 2 XL、Sony H9493 首次打开 Skin 页、Shop/Skin 快速往返、前后台切换后恢复、关闭重开、连续试穿四个部位；确认 Boxer 不黑屏、不残留旧帧，日志中无 `RenderTexture create failed`。
- 平台回归：至少一台非问题 Android 与一台 iOS 横屏设备执行相同基本路径，确认透明背景、颜色、清晰度、触摸和页签切换无变化。
- 若问题机仍黑屏，需要采集 `graphicsDeviceType`、`graphicsFormat`、`SystemInfo` 格式支持结果和目标设备日志，再检查当前颜色/深度格式与 Vulkan/OpenGLES 驱动差异；在缺少这些证据前不扩大到全局图形 API 或共享 URP 配置修改。

## 回滚路径

- 将 `boxer.renderTexture` 的 `m_EnableRandomWrite` 恢复为 `1`。
- 将 `boxer.renderTexture` 的 `m_DepthStencilFormat` 恢复为 `94`，并把 `ClearRenderTexture()` 恢复为同时清理深度与颜色。
- 移除 `EnsureRenderTextureCreated()` 及 `SetPreviewActive(...)` 中的 RT 就绪门禁，恢复相机直接按 `active` 启用和原清理条件。
- 回滚后会恢复修复前的特定 GPU 兼容性风险，不影响 Skin 存档与资源数据。
