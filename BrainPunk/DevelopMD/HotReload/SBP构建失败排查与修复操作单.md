# SBP 构建失败排查与修复操作单

创建时间：2026-05-21  
状态：方案草稿，未落代码  
关联文档：[SBP构建管线迁移代码设计.md](SBP构建管线迁移代码设计.md)

---

## 1. 问题现象

当前 Android 打包失败发生在 YooAsset 的 `TaskBuilding_SBP` 阶段，不在 Gradle、签名或导出阶段。

`Editor.log` 中最早出现的关键报错是：

- `An asset is marked with HideFlags.DontSave but is included in the build`
- `Asset: 'Packages/com.unity.ugui/Editor Resources/Gizmos/TMP - Font Asset Icon.psd'`
- `Asset: 'Packages/com.unity.ugui/Editor Resources/Gizmos/TMP - Text Component Icon.psd'`
- `Included from scene: 'Assets/Game/Scenes/Loading.unity'`

后续出现的：

- `Assertion failed on expression: 'm_LockCount == 0'`
- `Could not find file .../Library/BuildCache/.../CAB-...`
- `AssetBundle build failed. Task: TaskBuilding_SBP`

都属于连锁失败，不是第一触发点。

---

## 2. 当前结论

这次问题更像是：

1. `Loading.unity` 中的 TMP 相关对象在编辑器态生成了 `DontSave` 临时对象或 editor-only 依赖。
2. `BPGameCustomBuildDelegate.OnBeforeBuildStart()` 会先执行 YooAsset/SBP 资源构建，再进入 Unity 正常 `BuildPlayer` 流程。
3. TMP 自带的 `TMP_PreBuildProcessor` 挂在 `IPreprocessBuildWithReport`，清理动态字体数据的时机比当前 SBP 资源构建更晚。
4. 因为时序反了，SBP 在构建资源时提前看到了 TMP 编辑器态残留对象，于是直接失败。

换句话说，这不是“手动把 TMP icon 拖到了场景里”，而是“当前 SBP 构建时机和 TMP 动态字体清理时机不兼容”。

---

## 3. 已确认的相关事实

### 3.1 构建时序

当前构建入口：

- `BPGameCustomBuildDelegate.OnBeforeBuildStart()`
- `RefreshPackageAssets(buildTarget)`
- `BPAssetBuilder.Build(...)`
- `TaskBuilding_SBP`

也就是说，资源包先构建，Player 包后构建。

### 3.2 TMP 的默认预处理时机

TMP 自带：

- `TMP_PreBuildProcessor : IPreprocessBuildWithReport`

它会在 `OnPreprocessBuild()` 中遍历所有 `TMP_FontAsset`，对动态字体执行 `ClearCharacterAndGlyphTablesInternal()`。

这个时机属于 Unity 正常 Player 构建预处理，不会自动覆盖当前项目“先 SBP，后 BuildPlayer”的自定义流程。

### 3.3 当前项目中的字体状态

以下字体资产都满足：

- `m_AtlasPopulationMode: 1`，即 `Dynamic`
- `m_ClearDynamicDataOnBuild: 1`

相关文件：

- `Assets/Game/ArtSrc/Font/en SDF.asset`
- `Assets/Game/ArtSrc/Font/korean SDF.asset`
- `Assets/Game/ArtSrc/Font/Twinkly SDF.asset`
- `Assets/Game/ArtSrc/Font/fallback SDF.asset`

同时 `Assets/TextMesh Pro/Resources/TMP Settings.asset` 中也开启了：

- `m_ClearDynamicDataOnBuild: 1`

### 3.4 Loading 资源链

`Assets/Game/Scenes/Loading.unity` 中引用了：

- `Assets/Game/UI/Loading/Prefab/LoadingPanel.prefab`

`LoadingPanel.prefab` 内的 `TextMeshProUGUI` 直接引用：

- `Assets/Game/ArtSrc/Font/en SDF.asset`

因此 `Loading` 场景和这组动态 TMP 字体存在直接依赖关系。

### 3.5 当前 SBP 构建包装层的两个实现问题

1. `SbpUseCache` 配置未生效  
虽然 `BPAssetBuildConfig` 定义了 `SbpUseCache`，但 `ScriptableBuildParameters.GetBundleBuildParameters()` 里写死了：

- `buildParams.UseCache = true`

2. 自动 DeepClean 不完整  
当前正式构建只会删除：

- `Library/BuildCache`

但不会自动删除：

- YooAsset Pipeline Output 目录

这会放大缓存脏数据问题。

---

## 4. 修复目标

本次修复建议只解决以下 3 个目标，不做扩散性重构：

1. 在 SBP 开始前，显式执行一次 TMP 动态字体清理。
2. 把当前“自动 DeepClean”补成真正的 `BuildCache + Pipeline Output` 双清理。
3. 给构建前增加最小诊断日志，方便确认是否仍有 TMP 编辑器态对象残留。

---

## 5. 具体操作方案

### 5.1 操作一：在 SBP 前手动清理 TMP 动态字体数据

#### 目的

把 TMP 原本只会在 `BuildPlayer` 预处理里做的事情，提前到当前项目的 SBP 资源构建前。

#### 建议修改文件

- `Assets/Editor/BuildPipline/BPGameCustomBuildDelegate.cs`

#### 建议新增流程

在 `OnBeforeBuildStart()` 内，`RefreshPackageAssets(buildTarget)` 之前插入：

1. 遍历所有 `TMP_FontAsset`
2. 过滤条件：
   - `atlasPopulationMode == Dynamic || DynamicOS`
   - `clearDynamicDataOnBuild == true`
3. 对满足条件的字体执行：
   - 清字符表
   - 清字形表
   - 保存脏资源
4. 调用 `AssetDatabase.SaveAssets()`
5. 视情况补一次 `AssetDatabase.Refresh()`

#### 目标效果

让 SBP 看到的是“构建态已经清理过”的 TMP 字体资产，而不是编辑器态临时残留。

#### 注意事项

- 这一步属于构建前资源整理，不应在普通编辑器生命周期里自动执行。
- 不要挂在 `OnValidate`、`ExecuteAlways`、预览逻辑里做。
- 只在打包入口触发。

---

### 5.2 操作二：补齐真正的 DeepClean

#### 目的

避免旧的 SBP Cache 和旧的 YooAsset 输出目录一起干扰问题定位。

#### 建议修改文件

- `Assets/Editor/BuildPipeline/BPAssetBuilder.cs`

#### 当前问题

当前 `forceCleanCache == true` 时只执行：

- `BPSBPCacheCleaner.ClearBuildCache()`

但工具类里实际已经提供了：

- `ClearPipelineOutput()`

#### 建议修改

把正式包或手动 DeepClean 时的自动清理改为：

1. `ClearBuildCache()`
2. `ClearPipelineOutput()`

#### 目标效果

让“官方全量构建”真正接近一次干净的全量重建，减少误判缓存问题。

---

### 5.3 操作三：增加构建前 TMP 诊断日志

#### 目的

如果修复一和修复二落地后仍偶发失败，需要更快确认是哪一类 TMP 对象还残留。

#### 建议修改文件

- `Assets/Editor/BuildPipline/BPGameCustomBuildDelegate.cs`

#### 建议输出内容

构建前记录：

1. 本次被清理的 `TMP_FontAsset` 数量与路径
2. `Loading.unity` 直接依赖的主要 TMP 字体路径
3. 是否检测到动态字体且 atlas 非空

#### 目标效果

下一次如果再出 `TMP - Font Asset Icon` 或 `TMP - Text Component Icon`，可以直接对照构建前状态，而不是重新做全量人工排查。

---

## 6. 临时绕过方案

以下方案只用于验证，不建议直接作为最终长期方案。

### 6.1 临时关闭动态字体构建前清理

可临时把以下资源的 `m_ClearDynamicDataOnBuild` 改为 `0`：

- `en SDF.asset`
- `korean SDF.asset`
- `Twinkly SDF.asset`
- `fallback SDF.asset`

用途：

- 用来验证“是否就是 TMP 预清理时序导致的 SBP 问题”

风险：

- 只是绕过，不是根修
- 动态字体 atlas 可能继续带着编辑器态增量数据进包

### 6.2 临时切回 BBP 对比一次

可临时把：

- `BPAssetBuildConfig.asset -> UseScriptableBuildPipeline = false`

用途：

- 验证是否只有 SBP 对该问题更敏感

风险：

- 只能帮助对比，不解决根因
- 即使 BBP 过了，也不表示资源链是健康的

---

## 7. 建议的落地顺序

按下面顺序处理，风险最低：

1. 先加文档和日志，不改业务资源。
2. 在 `BPGameCustomBuildDelegate` 里补“SBP 前 TMP 动态字体清理”。
3. 把自动 DeepClean 补成双清理。
4. 重新执行一次 Release DeepClean 构建。
5. 如果问题仍存在，再单独对 `Loading.unity` 做对象级排查。

---

## 8. 验收标准

修复后至少满足：

1. `Editor.log` 中不再出现：
   - `TMP - Font Asset Icon.psd`
   - `TMP - Text Component Icon.psd`
2. `TaskBuilding_SBP` 可以完成，不再报 `UnityEngine build failed`
3. 正式包 DeepClean 后可以稳定连续构建两次
4. 第二次构建不因旧 `BuildCache` 或旧输出目录造成误判

---

## 9. 本次方案边界

这份操作单先不包含以下内容：

- `Loading.unity` 对象级逐个排查报告
- TMP 字体资产是否应该从 Dynamic 改成 Static 的长期设计决策
- HotReload / YooAsset 收集规则大改
- Unity 包版本升级评估

如果本操作单对应的最小修复落地后问题仍存在，再进入下一轮更细的对象级排查。
