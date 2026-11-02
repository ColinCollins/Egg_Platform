using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Bear.Editor
{
    /// <summary>
    /// 明信片星星批量刷新工具：仅在编辑器中打开 Level prefab，并复用 Postcard_Star 的本地排序入口。
    /// </summary>
    public static class PostcardStarLevelPrefabRefresher
    {
        private const string MenuPath = "Tools/Bear/Postcard RefreshAllLevelStar";
        private const string LevelPrefabRoot = "Assets/Game/Prefabs/Level";
        private const string ReportTitle = "Postcard RefreshAllLevelStar";

        #region Menu Entry

        /// <summary>
        /// 菜单入口：遍历 Assets/Game/Prefabs/Level 下所有 Level prefab，每个 prefab 只调用一次 SearchAllStarInScene。
        /// 该入口仅供编辑器内手动整理 Postcard_Star 序号，不访问运行时 DB、存档、配置或 manager 链路。
        /// </summary>
        [MenuItem(MenuPath, false, 200)]
        public static void RefreshAllLevelStar()
        {
            RefreshReport report = RefreshAllLevelStarInPrefabs(LevelPrefabRoot);
            string log = report.BuildLog();
            Debug.Log(log);

            string dialogMessage = report.FailureCount == 0
                ? $"Processed {report.ProcessedWithStarCount} level prefabs with Postcard_Star.\nSaved {report.SavedCount}, unchanged {report.UnchangedCount}.\nSee Console for details."
                : $"Processed with {report.FailureCount} failures.\nSaved {report.SavedCount}, unchanged {report.UnchangedCount}.\nSee Console for details.";
            EditorUtility.DisplayDialog(ReportTitle, dialogMessage, "OK");
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateRefreshAllLevelStar()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode
                && AssetDatabase.IsValidFolder(LevelPrefabRoot);
        }

        #endregion

        #region Refresh Flow

        private static RefreshReport RefreshAllLevelStarInPrefabs(string rootFolder)
        {
            var report = new RefreshReport(rootFolder);
            if (!AssetDatabase.IsValidFolder(rootFolder))
            {
                report.AddFailure(rootFolder, $"Prefab folder not found: {rootFolder}");
                return report;
            }

            string[] prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { rootFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            try
            {
                for (int i = 0; i < prefabPaths.Length; i++)
                {
                    string prefabPath = prefabPaths[i];
                    float progress = prefabPaths.Length == 0 ? 1f : (float)i / prefabPaths.Length;
                    EditorUtility.DisplayProgressBar(ReportTitle, prefabPath, progress);
                    RefreshOnePrefab(prefabPath, report);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return report;
        }

        private static void RefreshOnePrefab(string prefabPath, RefreshReport report)
        {
            GameObject prefabRoot = null;

            try
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                if (prefabRoot == null)
                {
                    report.AddFailure(prefabPath, "LoadPrefabContents returned null.");
                    return;
                }

                global::Postcard_Star[] stars = prefabRoot.GetComponentsInChildren<global::Postcard_Star>(true);
                if (stars.Length == 0)
                {
                    report.AddSkippedNoStar(prefabPath);
                    return;
                }

                // 用户要求一个 prefab 只执行一次，因此只从第一个星星触发全局整理。
                stars[0].SearchAllStarInScene();

                int dirtyStarCount = CountDirtyStars(stars);
                bool hasDirtyContent = dirtyStarCount > 0 || EditorUtility.IsDirty(prefabRoot);
                if (!hasDirtyContent)
                {
                    report.AddUnchanged(prefabPath, stars.Length);
                    return;
                }

                GameObject savedRoot = PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath, out bool success);
                if (success && savedRoot != null)
                {
                    report.AddSaved(prefabPath, stars.Length, dirtyStarCount);
                }
                else
                {
                    report.AddFailure(prefabPath, "SaveAsPrefabAsset returned false.");
                }
            }
            catch (Exception ex)
            {
                report.AddFailure(prefabPath, ex.Message);
            }
            finally
            {
                if (prefabRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
            }
        }

        private static int CountDirtyStars(IReadOnlyList<global::Postcard_Star> stars)
        {
            int dirtyCount = 0;
            for (int i = 0; i < stars.Count; i++)
            {
                if (stars[i] != null && EditorUtility.IsDirty(stars[i]))
                {
                    dirtyCount++;
                }
            }

            return dirtyCount;
        }

        #endregion

        #region Report

        private sealed class RefreshReport
        {
            private readonly string rootFolder;
            private readonly List<string> savedPrefabs = new List<string>();
            private readonly List<string> unchangedPrefabs = new List<string>();
            private readonly List<string> skippedNoStarPrefabs = new List<string>();
            private readonly List<string> failures = new List<string>();

            public int SavedCount => savedPrefabs.Count;
            public int UnchangedCount => unchangedPrefabs.Count;
            public int SkippedNoStarCount => skippedNoStarPrefabs.Count;
            public int FailureCount => failures.Count;
            public int ProcessedWithStarCount => SavedCount + UnchangedCount;

            public RefreshReport(string rootFolder)
            {
                this.rootFolder = rootFolder;
            }

            public void AddSaved(string prefabPath, int starCount, int dirtyStarCount)
            {
                savedPrefabs.Add($"{prefabPath} (stars: {starCount}, dirty stars: {dirtyStarCount})");
            }

            public void AddUnchanged(string prefabPath, int starCount)
            {
                unchangedPrefabs.Add($"{prefabPath} (stars: {starCount})");
            }

            public void AddSkippedNoStar(string prefabPath)
            {
                skippedNoStarPrefabs.Add(prefabPath);
            }

            public void AddFailure(string prefabPath, string reason)
            {
                failures.Add($"{prefabPath}: {reason}");
            }

            public string BuildLog()
            {
                var sb = new StringBuilder();
                sb.AppendLine($"[{ReportTitle}] Prefab root: {rootFolder}");
                sb.AppendLine($"[{ReportTitle}] Saved: {SavedCount}");
                sb.AppendLine($"[{ReportTitle}] Unchanged: {UnchangedCount}");
                sb.AppendLine($"[{ReportTitle}] Skipped no Postcard_Star: {SkippedNoStarCount}");
                sb.AppendLine($"[{ReportTitle}] Failures: {FailureCount}");

                AppendSection(sb, "Saved Prefabs", savedPrefabs);
                AppendSection(sb, "Unchanged Prefabs", unchangedPrefabs);
                AppendSection(sb, "Failures", failures);
                return sb.ToString();
            }

            private static void AppendSection(StringBuilder sb, string title, List<string> lines)
            {
                if (lines.Count == 0)
                {
                    return;
                }

                sb.AppendLine($"[{ReportTitle}] ---- {title} ----");
                for (int i = 0; i < lines.Count; i++)
                {
                    sb.AppendLine(lines[i]);
                }
            }
        }

        #endregion
    }
}
