using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Bear.Editor;
using BrainPunk.Editor.BuildPipeline;
using GF;
using Guru.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;
#if UNITY_IOS
using System.IO;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
#endif

/// <summary>
/// BrainPunk 项目组自定义构建管线
/// </summary>
public class BPGameCustomBuildDelegate : IAppBuildDelegate
{
    private static BPGameCustomBuildDelegate _instance;
    public static BPGameCustomBuildDelegate Instance => _instance ??= new BPGameCustomBuildDelegate();
    private static readonly MethodInfo TmpClearDynamicFontTablesMethod =
        typeof(TMP_FontAsset).GetMethod("ClearCharacterAndGlyphTablesInternal", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// 标记下次构建是否需要先执行 Deep Clean（清 SBP Build Cache + Pipeline Output）
    /// 由 BuildToolV2 或 Editor 菜单设置，构建后自动重置
    /// </summary>
    public static bool ForceDeepCleanForNextBuild = false;

    public void OnBeforeBuildStart(BuildTarget buildTarget)
    {
        Debug.Log("======== Project Asset Process Start ========");

        // ClassCollector 在本地手动处理，构建流程中跳过自动执行
        // if (!ClassCollectorEditorWindow.TryRunAutoCollection(out var collectMsg))
        // {
        //     throw new Exception($"ClassCollector auto run failed before build: {collectMsg}");
        // }
        // Debug.Log($"[BuildPipeline] {collectMsg}");
        // WaitForEditorReady("ClassCollector auto collection");

        ChangeGameSetting();
        // AssetDatabase.SaveAssets();
        // WaitForEditorReady("saving GameSetting");
        SaveOpenScenesBeforeBundleBuild();

        RefreshPackageAssets(buildTarget);
        Debug.Log("======== Project Asset Process End ========");
    }


    public void OnSetBuildPlayerOptions(BuildTarget buildTarget, BuildPlayerOptions buildPlayerOptions)
    {
        // 构建参数设置
        var assetBundleManifestPath = buildPlayerOptions.assetBundleManifestPath;
        if (string.IsNullOrEmpty(assetBundleManifestPath))
        {
            return;
        }

        // TODO: 执行 Bundle 构建逻辑
        if (buildTarget == BuildTarget.Android)
        {

        }
        else if (buildTarget == BuildTarget.iOS)
        {

        }

    }

    public void SaveBuildNumberAndCode(BuildTarget buildTarget, string buildVersion, string buildCode)
    {
        Debug.Log($"[{buildTarget}] Current Build Version: {buildVersion}-{buildCode}");
        // TODO: 项目组可自行保存版本信息
    }

    public void OnBuildSuccess(BuildTarget buildTarget, string outputFolder)
    {
        Debug.Log($"[{buildTarget}] Build Success: {outputFolder}");
        // TODO:  项目组处理后继步骤：打包成功
        BackToEditorGameSetting();
    }

    public void OnBuildFailed(BuildTarget buildTarget, Exception ex)
    {
        Debug.Log($"[{buildTarget}] Build  Failed: {ex}");
        // TODO:  项目组处理后继步骤：打包失败
        BackToEditorGameSetting();
    }

    #region iOS 导出配置

#if UNITY_IOS
    /// <summary>
    /// 在 iOS 导出完成后关闭 Firebase Dynamic Links 的首次安装自动检索，避免产生 google/link 空回传。
    /// 在现有 I2 本地化回调（10000）之后执行，只修改导出工程的主 Info.plist；重复导出仍写入同一布尔键。
    /// </summary>
    [PostProcessBuild(10001)]
    private static void DisableFirebaseDynamicLinksAutomaticRetrieval(BuildTarget buildTarget, string buildPath)
    {
        if (buildTarget != BuildTarget.iOS)
            return;

        string plistPath = Path.Combine(buildPath, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        plist.root.SetBoolean("FirebaseDeepLinkAutomaticRetrievalEnabled", false);
        plist.WriteToFile(plistPath);
        Debug.Log($"[OneLink] stage=ios_build_config, FirebaseDeepLinkAutomaticRetrievalEnabled=false, path={plistPath}.");
    }
#endif

    #endregion


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
    /// 资源构建入口（接入 BPAssetBuilder 封装，支持 BBP/SBP 切换）
    /// </summary>
    private static void RefreshPackageAssets(BuildTarget buildTarget)
    {
        var config = AssetDatabase.LoadAssetAtPath<BPAssetBuildConfig>("Assets/Resources/BPAssetBuildConfig.asset");
        if (config == null)
        {
            Debug.LogWarning("[BPGameCustomBuildDelegate] BPAssetBuildConfig not found, creating default...");
            config = ScriptableObject.CreateInstance<BPAssetBuildConfig>();
            AssetDatabase.CreateAsset(config, "Assets/Resources/BPAssetBuildConfig.asset");
            AssetDatabase.SaveAssets();
        }

        if (config.UseScriptableBuildPipeline)
        {
            PrepareTmpDynamicFontsForSbpBuild();
        }

        // CI 正式包或手动 Deep Clean：需要绝对干净的构建，清 SBP 缓存后走 IncrementalBuild
        bool isOfficialBuild = IsOfficialBuild();
        bool forceClean = config.UseScriptableBuildPipeline && (isOfficialBuild || ForceDeepCleanForNextBuild);

        if (forceClean)
        {
            if (ForceDeepCleanForNextBuild)
                Debug.Log("[BPGameCustomBuildDelegate] DeepClean flag set, clearing SBP cache for full rebuild...");
            else if (isOfficialBuild)
                Debug.Log("[BPGameCustomBuildDelegate] Official build detected, clearing SBP cache for full rebuild...");
        }

        // 消费掉 DeepClean 标记，避免影响后续构建
        ForceDeepCleanForNextBuild = false;
        var result = BPAssetBuilder.Build(buildTarget, config, forceCleanCache: forceClean);
        if (result == null || !result.Success)
        {
            string failedTask = result?.FailedTask ?? "UnknownTask";
            string errorInfo = result?.ErrorInfo ?? "UnknownError";
            throw new Exception($"AssetBundle build failed. Task: {failedTask}, Error: {errorInfo}");
        }

        Debug.Log($"[BPGameCustomBuildDelegate] Package output: {result.OutputPackageDirectory}");
    }

    private static void PrepareTmpDynamicFontsForSbpBuild()
    {
        string[] fontAssetGuids = AssetDatabase.FindAssets("t:TMP_FontAsset");
        int dynamicFontCount = 0;
        int clearedCount = 0;
        int forcedClearCount = 0;
        var clearedAssetLogs = new List<string>();

        foreach (string guid in fontAssetGuids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (fontAsset == null)
                continue;

            if (fontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic &&
                fontAsset.atlasPopulationMode != AtlasPopulationMode.DynamicOS)
            {
                continue;
            }

            dynamicFontCount++;

            Texture2D atlasTexture = fontAsset.atlasTexture;
            int atlasWidth = atlasTexture != null ? atlasTexture.width : 0;
            if (atlasWidth <= 1)
                continue;

            bool clearDynamicDataOnBuild = IsTmpClearDynamicDataOnBuildEnabled(fontAsset);
            bool forcedClear = !clearDynamicDataOnBuild;

            ClearTmpDynamicFontData(fontAsset);

            EditorUtility.SetDirty(fontAsset);
            if (fontAsset.atlasTexture != null)
                EditorUtility.SetDirty(fontAsset.atlasTexture);

            clearedCount++;
            if (forcedClear)
                forcedClearCount++;

            if (clearedAssetLogs.Count < 8)
            {
                string clearMode = forcedClear ? "forced" : "default";
                clearedAssetLogs.Add($"{assetPath} (atlasWidth={atlasWidth}, mode={clearMode})");
            }
        }

        if (clearedCount <= 0)
        {
            Debug.Log($"[BPGameCustomBuildDelegate] TMP pre-SBP cleanup finished. Dynamic fonts scanned: {dynamicFontCount}, cleared: 0.");
            return;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var summary = new StringBuilder();
        summary.Append($"[BPGameCustomBuildDelegate] TMP pre-SBP cleanup finished. Dynamic fonts scanned: {dynamicFontCount}, cleared: {clearedCount}");
        if (forcedClearCount > 0)
            summary.Append($", forced clears: {forcedClearCount}");

        if (clearedAssetLogs.Count > 0)
        {
            summary.AppendLine();
            summary.Append(string.Join("\n", clearedAssetLogs));
        }

        Debug.Log(summary.ToString());
    }

    private static bool IsTmpClearDynamicDataOnBuildEnabled(TMP_FontAsset fontAsset)
    {
        var serializedObject = new SerializedObject(fontAsset);
        SerializedProperty property = serializedObject.FindProperty("m_ClearDynamicDataOnBuild");
        return property != null && property.boolValue;
    }

    private static void ClearTmpDynamicFontData(TMP_FontAsset fontAsset)
    {
        // SBP 先于 TMP 自带的 BuildPlayer 预处理执行，这里直接清编辑器态动态 atlas 残留。
        if (TmpClearDynamicFontTablesMethod != null)
        {
            TmpClearDynamicFontTablesMethod.Invoke(fontAsset, null);
            return;
        }

        fontAsset.ClearFontAssetData(true);
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

        // 优先级 3：通过 Jenkins 参数推断（BuildToolV2 的 -params 中 Release 即正式包）
        foreach (var arg in args)
        {
            if (arg.Contains("-params") && arg.ToUpper().Contains("RELEASE"))
                return true;
        }

        // 优先级 4：DeepClean 命令行参数
        foreach (var arg in args)
        {
            if (arg.Equals("--deep-clean", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void SaveOpenScenesBeforeBundleBuild()
    {
        var dirtyScenes = GetDirtyScenesSummary();
        if (string.IsNullOrEmpty(dirtyScenes))
        {
            return;
        }

        Debug.Log($"[BuildPipeline] Found dirty scenes before bundle build:\n{dirtyScenes}");
        if (!EditorSceneManager.SaveOpenScenes())
        {
            throw new Exception("[BuildPipeline] Failed to save open scenes before bundle build.");
        }

        AssetDatabase.SaveAssets();
        // WaitForEditorReady("saving open scenes");

        dirtyScenes = GetDirtyScenesSummary();
        if (!string.IsNullOrEmpty(dirtyScenes))
        {
            throw new Exception($"[BuildPipeline] Dirty scenes still remain after SaveOpenScenes:\n{dirtyScenes}");
        }
    }

    private static string GetDirtyScenesSummary()
    {
        var sceneCount = EditorSceneManager.sceneCount;
        var sb = new StringBuilder();

        for (var i = 0; i < sceneCount; ++i)
        {
            var scene = EditorSceneManager.GetSceneAt(i);
            if (!scene.isDirty)
            {
                continue;
            }

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

    private static void WaitForEditorReady(string phase, int timeoutSeconds = 600)
    {
        if (!IsEditorBusy())
        {
            return;
        }

        var startTime = DateTime.UtcNow;
        double nextLogSeconds = 10;
        Debug.Log($"[BuildPipeline] Editor busy after {phase}, waiting... isCompiling={EditorApplication.isCompiling}, isUpdating={EditorApplication.isUpdating}");

        while (IsEditorBusy())
        {
            var elapsed = DateTime.UtcNow - startTime;
            if (HasScriptCompilationFailed())
            {
                throw new Exception($"[BuildPipeline] Script compilation failed while waiting after {phase}. Fix Console compile errors and retry build.");
            }

            if (elapsed.TotalSeconds >= timeoutSeconds)
            {
                throw new Exception(
                    $"[BuildPipeline] Editor did not become ready after {phase} within {timeoutSeconds}s. " +
                    $"isCompiling={EditorApplication.isCompiling}, isUpdating={EditorApplication.isUpdating}, " +
                    $"scriptCompilationFailed={HasScriptCompilationFailed()}. Check Console compile errors or long-running asset imports.");
            }

            if (elapsed.TotalSeconds >= nextLogSeconds)
            {
                Debug.Log($"[BuildPipeline] Still waiting after {elapsed.TotalSeconds:F0}s. isCompiling={EditorApplication.isCompiling}, isUpdating={EditorApplication.isUpdating}");
                nextLogSeconds += 10;
            }

            System.Threading.Thread.Sleep(200);
        }

        Debug.Log($"[BuildPipeline] Editor ready after {phase} in {(DateTime.UtcNow - startTime).TotalSeconds:F1}s.");
    }

    private static bool IsEditorBusy()
    {
        return EditorApplication.isCompiling || EditorApplication.isUpdating;
    }

    private static bool HasScriptCompilationFailed()
    {
        var property = typeof(EditorUtility).GetProperty("scriptCompilationFailed", BindingFlags.Public | BindingFlags.Static);
        if (property == null || property.PropertyType != typeof(bool))
        {
            return false;
        }

        return (bool)property.GetValue(null, null);
    }
}
