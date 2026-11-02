using System.IO;
using UnityEditor;
using UnityEngine;
using YooAsset.Editor;

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
