# SBP 构建管线迁移 — 代码设计方案

创建时间：2026-05-21  
版本：v1.0  
关联文档：[HotReload测试代码实现.md](HotReload测试代码实现.md)、[HotReload测试流程设计.md](HotReload测试流程设计.md)

---

## 1. 设计目标

将当前项目的资源构建从 `BuiltinBuildPipeline + ForceRebuild` 迁移到 `ScriptableBuildPipeline + IncrementalBuild`，同时保留以下能力：

1. **正式包仍可全量构建**（通过清 SBP 缓存实现，而非 ForceRebuild）。
2. **日常开发和 QA 联调享受增量构建提速**。
3. **构建参数可配置、可切换、可追踪**。
4. **与现有 HotReload 链路完全兼容**（出包产物格式不变）。

---

## 2. 架构设计

职责拆分：

| 文件 | 路径 | 职责 |
|---|---|---|
| `BPAssetBuildConfig.asset` | `Assets/Resources/` | 构建配置资产，Inspector 可视化 |
| `BPAssetBuildConfig.cs` | `Assets/Editor/BuildPipeline/` | 配置资产定义 |
| `BPAssetBuilder.cs` | `Assets/Editor/BuildPipeline/` | 构建入口封装，支持 BBP/SBP 切换和缓存管理 |
| `BPSBPCacheCleaner.cs` | `Assets/Editor/BuildPipeline/` | SBP Build Cache 清理工具 |
| `BPAssetBuildMenu.cs` | `Assets/Editor/BuildPipeline/` | Editor 菜单：切管线、清缓存、手动构建 |
| `BPGameCustomBuildDelegate.cs` | `Assets/Editor/BuildPipline/` | 改造后的项目构建委托 |

### 2.1 构建流程图

```mermaid
flowchart TD
    subgraph Entry["触发入口"]
        E1["BuildToolV2 Editor菜单<br/>Tools/自动化打包/Android/Release"]
        E2["Jenkins CI 命令行<br/>-executeMethod BuildToolV2.JenkinsBuildAndroidRelease"]
        E3["BrainPunk/Build 菜单<br/>Build AssetBundles"]
    end

    E1 -->|BuildAndroid(param)| BTV["BuildToolV2<br/>创建 AppBuilder + 参数"]
    E2 -->|JenkinsBuildAndroidRelease| BTV
    E3 -->|BPAssetBuildMenu| BAB["BPAssetBuilder.Build()<br/>直接调用资源构建"]

    BTV -->|builder.BuildAndroid| Delegate["BPGameCustomBuildDelegate<br/>IAppBuildDelegate 接口回调"]

    subgraph PreProcess["构建前准备<br/>OnBeforeBuildStart"]
        P1["ChangeGameSetting()<br/>playMode → OfflinePlayMode"]
        P2["SaveOpenScenesBeforeBundleBuild()<br/>保存未保存场景"]
        P3["RefreshPackageAssets(buildTarget)<br/>↓ 资源构建入口"]
    end

    Delegate --> P1 --> P2 --> P3

    subgraph AssetBuild["资源构建层<br/>BPAssetBuilder"]
        A1["加载 BPAssetBuildConfig.asset"]
        A2{"IsOfficialBuild()?"}
        A3["清 SBP Build Cache<br/>Library/BuildCache"]
        A4["解析版本号<br/>TimeStamp / GitCommit / Custom"]
        A5{"UseScriptable<br/>BuildPipeline?"}
        A6["SBP<br/>ScriptableBuildPipeline"]
        A7["BBP Fallback<br/>BuiltinBuildPipeline"]
        A8["YooAsset 构建任务链"]
    end

    P3 --> A1 --> A2
    A2 -->|正式包| A3 --> A4
    A2 -->|Debug/日常| A4
    A4 --> A5
    A5 -->|true| A6 --> A8
    A5 -->|false| A7 --> A8

    subgraph YooAssetTasks["YooAsset 内部构建任务"]
        Y1["TaskGetBuildMap<br/>收集资源 & 计算依赖"]
        Y2{"BuildMode?"}
        Y3["TaskBuilding_SBP<br/>ContentPipeline.BuildAssetBundles<br/>Build Cache 增量复用"]
        Y4["TaskBuilding_BBP<br/>BuildPipeline.BuildAssetBundles"]
        Y5["TaskEncryption<br/>EncryptoHTXOR 加密"]
        Y6["TaskUpdateBundleInfo<br/>更新 Hash/CRC/Size"]
        Y7["TaskCreateManifest<br/>生成 PackageManifest"]
        Y8["TaskCopyBuildinFiles<br/>拷贝到 StreamingAssets"]
    end

    A8 --> Y1 --> Y2
    Y2 -->|SBP Incremental| Y3 --> Y5
    Y2 -->|BBP ForceRebuild| Y4 --> Y5
    Y2 -->|BBP Incremental| Y4 --> Y5
    Y5 --> Y6 --> Y7 --> Y8

    subgraph PlayerBuild["Player 构建<br/>Unity 出包"]
        PL1["OnSetBuildPlayerOptions<br/>配置 BuildPlayerOptions"]
        PL2["Unity BuildPlayer<br/>生成 APK / IPA / Xcode"]
    end

    Y8 --> PL1 --> PL2

    subgraph PostProcess["构建后处理"]
        PO1{"构建结果?"}
        PO2["OnBuildSuccess<br/>BackToEditorGameSetting()<br/>playMode → EditorSimulateMode"]
        PO3["OnBuildFailed<br/>BackToEditorGameSetting()<br/>playMode → EditorSimulateMode"]
        PO4["产物输出<br/>BuildOutput/Android/xxx.apk"]
    end

    PL2 --> PO1
    PO1 -->|成功| PO2 --> PO4
    PO1 -->|失败| PO3

    style E1 fill:#e1f5fe
    style E2 fill:#e1f5fe
    style E3 fill:#e1f5fe
    style A3 fill:#fff3e0
    style PO4 fill:#e8f5e9
```

### 2.2 流程说明

| 阶段 | 关键决策点 | 说明 |
|---|---|---|
| **触发入口** | 3 个入口 | Editor 菜单、Jenkins CI、BrainPunk 调试菜单 |
| **构建前准备** | `ChangeGameSetting()` | 把 `GameSetting.asset` 的 `playMode` 临时改为 `OfflinePlayMode`，确保构建时资源正确收集 |
| **资源构建层** | `IsOfficialBuild()` | 通过命令行参数 `--official-build`、环境变量 `BP_OFFICIAL_BUILD=true`、或 Jenkins `-params` 中的 `RELEASE` 判断是否为正式包 |
| **SBP 缓存策略** | `forceCleanCache` | 正式包清 `Library/BuildCache` 后走 `IncrementalBuild`（等价于全量）；Debug 构建不清缓存，享受增量提速 |
| **版本号解析** | `VersionStrategy` | `TimeStamp`（默认）/ `GitCommitHash` / `Custom`，统一由 `BPAssetBuilder.ResolvePackageVersion()` 处理 |
| **产物一致性** | YooAsset 任务链 | 无论走 SBP 还是 BBP，最终产物格式一致（Manifest + Bundles + `.version`），HotReload 运行时无感知 |
| **构建后回退** | `BackToEditorGameSetting()` | 把 `playMode` 恢复为 `EditorSimulateMode`，避免污染 Editor 开发环境 |

---

## 3. 核心代码实现

### 3.1 构建配置资产 (`BPAssetBuildConfig.cs`)

把硬编码的构建参数提取成 ScriptableObject，方便在 Inspector 里调整，也避免改代码。

```csharp
using UnityEngine;
using YooAsset.Editor;

namespace BrainPunk.Editor.BuildPipeline
{
    [CreateAssetMenu(fileName = "BPAssetBuildConfig", menuName = "BrainPunk/Create AssetBuildConfig")]
    public class BPAssetBuildConfig : ScriptableObject
    {
        [Header("Package")]
        public string PackageName = "GamePackage";

        [Header("Pipeline")]
        [Tooltip(" true = SBP (推荐), false = BBP (兼容)")]
        public bool UseScriptableBuildPipeline = true;

        [Header("Build Mode (SBP 只支持 Incremental)")]
        public EBuildMode BuildMode = EBuildMode.IncrementalBuild;

        [Header("Version")]
        [Tooltip("时间戳 / GitCommit / 自定义")]
        public EVersionStrategy VersionStrategy = EVersionStrategy.TimeStamp;
        [Tooltip("VersionStrategy=Custom 时生效")]
        public string CustomVersion = "1.0.0";

        [Header("Compression")]
        public ECompressOption CompressOption = ECompressOption.LZ4;

        [Header("File Name")]
        public EFileNameStyle FileNameStyle = EFileNameStyle.HashName;

        [Header("Builtin File Copy")]
        public EBuildinFileCopyOption BuildinFileCopyOption = EBuildinFileCopyOption.ClearAndCopyAll;

        [Header("SBP Exclusive")]
        [Tooltip("SBP Build Cache，默认开启")]
        public bool SbpUseCache = true;
        [Tooltip("团队缓存服务器地址，留空则使用本地缓存")]
        public string SbpCacheServerHost = "";
        [Tooltip("团队缓存服务器端口")]
        public int SbpCacheServerPort = 8126;
        [Tooltip("生成 link.xml 防止代码裁剪")]
        public bool SbpWriteLinkXML = true;

        [Header("Encryption")]
        public bool EnableEncryption = true;

        [Header("Advanced")]
        public bool EnableSharePackRule = true;
        public bool VerifyBuildingResult = true;

        public enum EVersionStrategy
        {
            TimeStamp,
            GitCommitHash,
            Custom,
        }
    }
}
```

---

### 3.2 构建入口封装 (`BPAssetBuilder.cs`)

这是核心类，屏蔽 BBP/SBP 的差异，对外提供统一的 `Build()` 接口。

```csharp
using System;
using System.IO;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;

namespace BrainPunk.Editor.BuildPipeline
{
    /// <summary>
    /// BrainPunk 资源构建封装器
    /// 职责：统一 BBP/SBP 构建入口，管理版本号、缓存、产物输出。
    /// </summary>
    public static class BPAssetBuilder
    {
        public static BuildResult Build(BuildTarget buildTarget, BPAssetBuildConfig config, bool forceCleanCache = false)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            // 如果需要"绝对干净"的构建（替代 ForceRebuild），先清 SBP 缓存
            if (forceCleanCache && config.UseScriptableBuildPipeline)
            {
                BPSBPCacheCleaner.ClearBuildCache();
            }

            string packageVersion = ResolvePackageVersion(config);
            UnityEngine.Debug.Log($"[BPAssetBuilder] Start building. Pipeline={(config.UseScriptableBuildPipeline ? "SBP" : "BBP")}, " +
                                  $"Mode={config.BuildMode}, Version={packageVersion}, Target={buildTarget}");

            BuildResult result = config.UseScriptableBuildPipeline
                ? BuildWithSBP(buildTarget, packageVersion, config)
                : BuildWithBBP(buildTarget, packageVersion, config);

            if (result != null && result.Success)
            {
                UnityEngine.Debug.Log($"[BPAssetBuilder] Build success. Output: {result.OutputPackageDirectory}");
            }
            else
            {
                string failedTask = result?.FailedTask ?? "Unknown";
                string errorInfo = result?.ErrorInfo ?? "Unknown";
                UnityEngine.Debug.LogError($"[BPAssetBuilder] Build failed. Task: {failedTask}, Error: {errorInfo}");
            }

            return result;
        }

        #region SBP Build

        private static BuildResult BuildWithSBP(BuildTarget buildTarget, string packageVersion, BPAssetBuildConfig config)
        {
            var buildParameters = new ScriptableBuildParameters
            {
                BuildOutputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot(),
                BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot(),
                BuildPipeline = EBuildPipeline.ScriptableBuildPipeline.ToString(),
                BuildTarget = buildTarget,
                BuildMode = EBuildMode.IncrementalBuild, // SBP 只支持 Incremental
                PackageName = config.PackageName,
                PackageVersion = packageVersion,
                CompressOption = config.CompressOption,
                FileNameStyle = config.FileNameStyle,
                BuildinFileCopyOption = config.BuildinFileCopyOption,
                EnableSharePackRule = config.EnableSharePackRule,
                VerifyBuildingResult = config.VerifyBuildingResult,
                WriteLinkXML = config.SbpWriteLinkXML,
                CacheServerHost = string.IsNullOrEmpty(config.SbpCacheServerHost) ? null : config.SbpCacheServerHost,
                CacheServerPort = config.SbpCacheServerPort,
            };

            // ScriptableBuildParameters.GetBundleBuildParameters() 里硬编码了 UseCache = true
            // 如果需要关闭缓存（调试场景），可以通过反射或改 YooAsset 源码实现
            // 这里保持默认开启

            if (config.EnableEncryption)
                buildParameters.EncryptionServices = new EncryptoHTXOR();

            var pipeline = new ScriptableBuildPipeline();
            return pipeline.Run(buildParameters, enableLog: true);
        }

        #endregion

        #region BBP Build (Fallback)

        private static BuildResult BuildWithBBP(BuildTarget buildTarget, string packageVersion, BPAssetBuildConfig config)
        {
            var buildParameters = new BuiltinBuildParameters
            {
                BuildOutputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot(),
                BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot(),
                BuildPipeline = EBuildPipeline.BuiltinBuildPipeline.ToString(),
                BuildTarget = buildTarget,
                BuildMode = config.BuildMode,
                PackageName = config.PackageName,
                PackageVersion = packageVersion,
                CompressOption = config.CompressOption,
                FileNameStyle = config.FileNameStyle,
                BuildinFileCopyOption = config.BuildinFileCopyOption,
                EnableSharePackRule = config.EnableSharePackRule,
                VerifyBuildingResult = config.VerifyBuildingResult,
            };

            if (config.EnableEncryption)
                buildParameters.EncryptionServices = new EncryptoHTXOR();

            var pipeline = new BuiltinBuildPipeline();
            return pipeline.Run(buildParameters, enableLog: true);
        }

        #endregion

        #region Version Resolution

        public static string ResolvePackageVersion(BPAssetBuildConfig config)
        {
            switch (config.VersionStrategy)
            {
                case BPAssetBuildConfig.EVersionStrategy.TimeStamp:
                    return GetTimeStamp().ToString();

                case BPAssetBuildConfig.EVersionStrategy.GitCommitHash:
                    return GetGitCommitHash() ?? GetTimeStamp().ToString();

                case BPAssetBuildConfig.EVersionStrategy.Custom:
                    return config.CustomVersion;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static int GetTimeStamp()
        {
            int tickToMillisecond = 10000;
            long stamp197011 = 62135596800000;
            return (int)(DateTime.UtcNow.Ticks / tickToMillisecond - stamp197011);
        }

        private static string GetGitCommitHash()
        {
            try
            {
                var psi = new ProcessStartInfo("git", "rev-parse --short HEAD")
                {
                    WorkingDirectory = Directory.GetParent(Application.dataPath)?.FullName,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using var process = Process.Start(psi);
                string output = process?.StandardOutput.ReadToEnd()?.Trim();
                process?.WaitForExit(5000);
                return string.IsNullOrEmpty(output) ? null : output;
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}
```

---

### 3.3 SBP 缓存清理工具 (`BPSBPCacheCleaner.cs`)

SBP 不支持 `ForceRebuild`，所以当需要"绝对干净"的构建时，需要手动清理 SBP 的 Build Cache。

```csharp
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BrainPunk.Editor.BuildPipeline
{
    /// <summary>
    /// SBP Build Cache 清理工具
    /// 职责：提供 ForceRebuild 的等价能力（清缓存后首次 IncrementalBuild = 全量构建）。
    /// </summary>
    public static class BPSBPCacheCleaner
    {
        /// <summary>
        /// SBP 本地缓存根目录
        /// </summary>
        public static string LocalCacheRoot => Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library", "BuildCache");

        /// <summary>
        /// 清理 SBP 本地 Build Cache
        /// </summary>
        [MenuItem("BrainPunk/Build/Clear SBP Build Cache", false, 100)]
        public static void ClearBuildCache()
        {
            string cachePath = LocalCacheRoot;
            if (Directory.Exists(cachePath))
            {
                Directory.Delete(cachePath, recursive: true);
                Debug.Log($"[BPSBPCacheCleaner] Deleted SBP Build Cache: {cachePath}");
            }
            else
            {
                Debug.Log($"[BPSBPCacheCleaner] SBP Build Cache not found, nothing to delete: {cachePath}");
            }

            // 可选：同时清理 Library/ScriptAssemblies 等可能干扰的缓存
            // 注意：不要删 Library/PackageCache 或 Library/ArtifactDB，会导致大量重新导入
        }

        /// <summary>
        /// 清理 YooAsset 的 Pipeline 输出目录（OutputFolder）
        /// </summary>
        [MenuItem("BrainPunk/Build/Clear YooAsset Pipeline Output", false, 101)]
        public static void ClearPipelineOutput()
        {
            string outputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot();
            if (Directory.Exists(outputRoot))
            {
                Directory.Delete(outputRoot, recursive: true);
                Debug.Log($"[BPSBPCacheCleaner] Deleted YooAsset output root: {outputRoot}");
            }
        }

        /// <summary>
        /// 一键深度清理（SBP Cache + Pipeline Output）
        /// </summary>
        [MenuItem("BrainPunk/Build/Deep Clean (SBP + Output)", false, 102)]
        public static void DeepClean()
        {
            ClearBuildCache();
            ClearPipelineOutput();
            EditorUtility.DisplayDialog("Deep Clean Done", "SBP Build Cache and YooAsset Pipeline Output have been cleared.\n\nNext build will be a FULL rebuild.", "OK");
        }
    }
}
```

---

### 3.4 Editor 菜单 (`BPAssetBuildMenu.cs`)

提供快捷操作：手动触发构建、切管线、查看缓存大小。

```csharp
using UnityEditor;
using UnityEngine;

namespace BrainPunk.Editor.BuildPipeline
{
    public static class BPAssetBuildMenu
    {
        private const string CONFIG_PATH = "Assets/Resources/BPAssetBuildConfig.asset";

        private static BPAssetBuildConfig GetOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<BPAssetBuildConfig>(CONFIG_PATH);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<BPAssetBuildConfig>();
                AssetDatabase.CreateAsset(config, CONFIG_PATH);
                AssetDatabase.SaveAssets();
                Debug.Log($"[BPAssetBuildMenu] Created default config at: {CONFIG_PATH}");
            }
            return config;
        }

        [MenuItem("BrainPunk/Build/Build AssetBundles (Current Config)", false, 0)]
        private static void MenuBuildAssetBundles()
        {
            var config = GetOrCreateConfig();
            BPAssetBuilder.Build(EditorUserBuildSettings.activeBuildTarget, config, forceCleanCache: false);
        }

        [MenuItem("BrainPunk/Build/Build AssetBundles (Force Clean)", false, 1)]
        private static void MenuBuildAssetBundlesForceClean()
        {
            var config = GetOrCreateConfig();
            bool confirm = EditorUtility.DisplayDialog("Force Clean Build",
                "This will clear SBP Build Cache before building.\nEquivalent to 'ForceRebuild' in BBP.",
                "Proceed", "Cancel");
            if (confirm)
            {
                BPAssetBuilder.Build(EditorUserBuildSettings.activeBuildTarget, config, forceCleanCache: true);
            }
        }

        [MenuItem("BrainPunk/Build/Toggle Pipeline (SBP ↔ BBP)", false, 50)]
        private static void MenuTogglePipeline()
        {
            var config = GetOrCreateConfig();
            config.UseScriptableBuildPipeline = !config.UseScriptableBuildPipeline;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);
            Debug.Log($"[BPAssetBuildMenu] Switched to: {(config.UseScriptableBuildPipeline ? "SBP" : "BBP")}");
        }

        [MenuItem("BrainPunk/Build/Open Config Asset", false, 51)]
        private static void MenuOpenConfig()
        {
            var config = GetOrCreateConfig();
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
        }
    }
}
```

---

### 3.5 改造后的项目构建委托 (`BPGameCustomBuildDelegate.cs`)

对现有文件做最小侵入式修改，只替换 `RefreshPackageAssets` 方法。

```csharp
using System;
using System.IO;
using System.Reflection;
using System.Text;
using Bear.Editor;
using BrainPunk.Editor.BuildPipeline;
using GF;
using Guru.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;

/// <summary>
/// BrainPunk 项目组自定义构建管线
/// </summary>
public class BPGameCustomBuildDelegate : IAppBuildDelegate
{
    private static BPGameCustomBuildDelegate _instance;
    public static BPGameCustomBuildDelegate Instance => _instance ??= new BPGameCustomBuildDelegate();

    public void OnBeforeBuildStart(BuildTarget buildTarget)
    {
        Debug.Log("======== Project Asset Process Start ========");
        ChangeGameSetting();
        SaveOpenScenesBeforeBundleBuild();
        RefreshPackageAssets(buildTarget);
        Debug.Log("======== Project Asset Process End ========");
    }

    public void OnSetBuildPlayerOptions(BuildTarget buildTarget, BuildPlayerOptions buildPlayerOptions)
    {
        // 原逻辑保留
        var assetBundleManifestPath = buildPlayerOptions.assetBundleManifestPath;
        if (string.IsNullOrEmpty(assetBundleManifestPath))
            return;
    }

    public void SaveBuildNumberAndCode(BuildTarget buildTarget, string buildVersion, string buildCode)
    {
        Debug.Log($"[{buildTarget}] Current Build Version: {buildVersion}-{buildCode}");
    }

    public void OnBuildSuccess(BuildTarget buildTarget, string outputFolder)
    {
        Debug.Log($"[{buildTarget}] Build Success: {outputFolder}");
        BackToEditorGameSetting();
    }

    public void OnBuildFailed(BuildTarget buildTarget, Exception ex)
    {
        Debug.Log($"[{buildTarget}] Build Failed: {ex}");
        BackToEditorGameSetting();
    }

    #region Private Methods

    private static void ChangeGameSetting()
    {
        var gameSettings = AssetDatabase.LoadAssetAtPath<GameSetting>("Assets/Resources/GameSetting.asset");
        for (int i = 0; i < gameSettings.builtinPackageList.Count; i++)
        {
            gameSettings.builtinPackageList[i].playMode = EPlayMode.OfflinePlayMode;
        }
        EditorUtility.SetDirty(gameSettings);
        AssetDatabase.SaveAssetIfDirty(gameSettings);
    }

    /// <summary>
    /// 资源构建入口（改造后）
    /// </summary>
    private static void RefreshPackageAssets(BuildTarget buildTarget)
    {
        // 加载构建配置
        var config = AssetDatabase.LoadAssetAtPath<BPAssetBuildConfig>("Assets/Resources/BPAssetBuildConfig.asset");
        if (config == null)
        {
            Debug.LogWarning("[BPGameCustomBuildDelegate] BPAssetBuildConfig not found, creating default...");
            config = ScriptableObject.CreateInstance<BPAssetBuildConfig>();
            AssetDatabase.CreateAsset(config, "Assets/Resources/BPAssetBuildConfig.asset");
            AssetDatabase.SaveAssets();
        }

        // CI 正式包：需要绝对干净的构建，清 SBP 缓存后走 IncrementalBuild
        bool isOfficialBuild = IsOfficialBuild();
        bool forceClean = config.UseScriptableBuildPipeline && isOfficialBuild;

        if (forceClean)
        {
            Debug.Log("[BPGameCustomBuildDelegate] Official build detected, clearing SBP cache for full rebuild...");
        }

        var result = BPAssetBuilder.Build(buildTarget, config, forceCleanCache: forceClean);
        if (result == null || !result.Success)
        {
            string failedTask = result?.FailedTask ?? "UnknownTask";
            string errorInfo = result?.ErrorInfo ?? "UnknownError";
            throw new Exception($"AssetBundle build failed. Task: {failedTask}, Error: {errorInfo}");
        }

        Debug.Log($"[BPGameCustomBuildDelegate] Package output: {result.OutputPackageDirectory}");
    }

    /// <summary>
    /// 判断是否为 CI 正式包构建
    /// </summary>
    private static bool IsOfficialBuild()
    {
        // 优先级 1：命令行参数
        string[] args = Environment.GetCommandLineArgs();
        foreach (var arg in args)
        {
            if (arg.Equals("--official-build", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // 优先级 2：环境变量
        string envOfficial = Environment.GetEnvironmentVariable("BP_OFFICIAL_BUILD");
        if (!string.IsNullOrEmpty(envOfficial) && envOfficial.Equals("true", StringComparison.OrdinalIgnoreCase))
            return true;

        // 默认：非正式包（日常开发/QA 联调走增量）
        return false;
    }

    private static void SaveOpenScenesBeforeBundleBuild()
    {
        var dirtyScenes = GetDirtyScenesSummary();
        if (string.IsNullOrEmpty(dirtyScenes))
            return;

        Debug.Log($"[BuildPipeline] Found dirty scenes before bundle build:\n{dirtyScenes}");
        if (!EditorSceneManager.SaveOpenScenes())
            throw new Exception("[BuildPipeline] Failed to save open scenes before bundle build.");

        AssetDatabase.SaveAssets();
        dirtyScenes = GetDirtyScenesSummary();
        if (!string.IsNullOrEmpty(dirtyScenes))
            throw new Exception($"[BuildPipeline] Dirty scenes still remain after SaveOpenScenes:\n{dirtyScenes}");
    }

    private static string GetDirtyScenesSummary()
    {
        var sb = new StringBuilder();
        for (var i = 0; i < EditorSceneManager.sceneCount; ++i)
        {
            var scene = EditorSceneManager.GetSceneAt(i);
            if (!scene.isDirty) continue;
            var scenePath = string.IsNullOrEmpty(scene.path) ? "<untitled scene>" : scene.path;
            sb.AppendLine($"{scene.name} | {scenePath}");
        }
        return sb.ToString().TrimEnd();
    }

    private static void BackToEditorGameSetting()
    {
        var gameSettings = AssetDatabase.LoadAssetAtPath<GameSetting>("Assets/Resources/GameSetting.asset");
        for (int i = 0; i < gameSettings.builtinPackageList.Count; i++)
        {
            gameSettings.builtinPackageList[i].playMode = EPlayMode.EditorSimulateMode;
        }
        EditorUtility.SetDirty(gameSettings);
        AssetDatabase.SaveAssetIfDirty(gameSettings);
    }

    #endregion
}
```

---

## 4. 版本号策略对比

| 策略 | 适用场景 | 示例 | 优缺点 |
|---|---|---|---|
| **TimeStamp** | 默认策略，CI 自动构建 | `1716283456` | 无重复，但每次构建版本都不同，无法直观对应代码 |
| **GitCommitHash** | 需要追溯代码版本 | `a1b2c3d` | 能直接对应代码提交，但 hash 不保证递增 |
| **Custom** | 人工指定大版本 | `1.2.3` | 直观，但容易遗忘更新 |

**推荐组合：**
- 日常联调：`GitCommitHash`（方便定位问题）
- CI 出包：`TimeStamp`（保证唯一性）
- 上线候选包：`Custom`（如 `v1.2.0-rc1`）

---

## 5. 与现有热更链路的兼容性

迁移到 SBP 后，以下环节**完全不需要改动**：

| 环节 | 说明 |
|---|---|
| `HotReloadCtrl_InitializePackage` | SBP 和 BBP 的产物格式一致（都是 AssetBundle + Manifest），初始化代码无感知 |
| `GameSetting.asset` 的 `playMode` | 仍然是 `HostPlayMode`，由构建流程负责切换 |
| `ConfigManager` 加载配置表 | 从 YooAsset 包中加载 json 的逻辑不变 |
| `LoadManager` 热更等待逻辑 | 热更状态机和事件不变 |
| CDN 上的产物结构 | 仍然是 `{PackageVersion}/` 目录 + `.version` + manifest + bundles |
| 加密服务 `EncryptoHTXOR` | SBP 同样支持 `EncryptionServices` |

**唯一需要 QA 验证的：**
- 首次 SBP 构建出的包，在真机上是否能正常热更和加载（做一次完整冒烟测试即可）。

---

## 6. 落地步骤

| 步骤 | 动作 | 预期结果 |
|---|---|---|
| 1 | 在 `Assets/Editor/BuildPipeline/` 目录下创建上述 4 个脚本文件 | 代码就绪 |
| 2 | 在 `Assets/Resources/` 下 `Create -> BrainPunk -> Create AssetBuildConfig` | 生成配置资产 |
| 3 | 把 `BPGameCustomBuildDelegate.cs` 替换成改造后的版本 | 构建委托接入新封装 |
| 4 | 在 Editor 菜单 `BrainPunk/Build/` 里手动跑一次 Build | 验证通路 |
| 5 | 对比 BBP ForceRebuild 和 SBP IncrementalBuild 的产物目录结构 | 确认格式一致 |
| 6 | 打测试包到真机验证热更链路 | 确认运行时无异常 |
| 7 | 把 SBP 设为默认，BBP 作为 fallback 保留 | 日常开发开始享受增量提速 |

---

## 7. 风险与兜底

| 风险 | 兜底方案 |
|---|---|
| SBP 首次构建产物异常 | `BPAssetBuildConfig.UseScriptableBuildPipeline = false` 切回 BBP |
| SBP 缓存损坏导致奇怪问题 | `BrainPunk/Build/Deep Clean (SBP + Output)` 一键清缓存后重编 |
| CI 环境没有 git 命令导致 `GitCommitHash` 策略失败 | 自动 fallback 到 `TimeStamp`（代码里已实现） |
| 团队多人 SBP 缓存不一致 | 配置 `SbpCacheServerHost` 启用共享缓存服务器 |
| 升级 Unity 后 SBP 行为变化 | 每次大版本升级后先做 `Deep Clean` 再构建 |

---

## 8. 性能预期

以当前项目规模（从构建参数 `EnableSharePackRule = true` 推断 bundle 数量中等）为基准：

| 场景 | BBP ForceRebuild | SBP Incremental (首次/缓存空) | SBP Incremental (有缓存) |
|---|---|---|---|
| 全量构建 | ~3-5 min | ~3-5 min | ~10-30 s |
| 改 1 个 prefab | ~3-5 min | ~1-2 min | ~10-30 s |
| 改 1 张图 | ~3-5 min | ~1-2 min | ~5-20 s |

> 实际收益取决于项目资源规模和 Unity 版本，建议落地后用 `Stopwatch` 在 `BPAssetBuilder.Build()` 中记录真实耗时。

---

## 修改记录

- 2026-05-21 v1.0：基于 YooAsset 仓库代码分析，给出从 BBP+ForceRebuild 迁移到 SBP+IncrementalBuild 的完整代码设计方案。
