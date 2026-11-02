using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Bear.Editor
{
    /// <summary>
    /// Draw 关卡 Tips 引用修复工具：只在用户手动调用时检查 DLevel prefab，并追加缺失的提示对象引用。
    /// </summary>
    public static class DrawGameTipsReferenceRepairTool
    {
        #region Constants

        private const string MenuPath = "Tools/Bear/DrawGame/修复丢失的 Tips 引用（跳过 001/007/022）";
        private const string DrawLevelPrefabRoot = "Assets/Game/Prefabs/DrawLevel";
        private const string DrawLevelPrefabPrefix = "DLevel_";
        private const string TipsPropertyName = "tipsObjects";
        private const string LegacyTipsObjectName = "TipsObject";
        private const string TipsObjectName = "DrawGame_TipsObjectSubObject";
        private const string ReportTitle = "DrawGame Tips 引用修复";

        #endregion

        #region Menu Entry

        /// <summary>
        /// 手动扫描 DrawLevel prefab，并把层级中存在但未写入 tipsObjects 的提示对象追加到序列化列表。
        /// 调用边界：只操作本地 prefab 资源；不访问 DB、Save、配置、运行时单例，也不会处理 DLevel_001/007/022。
        /// </summary>
        [MenuItem(MenuPath, false, 210)]
        public static void RepairMissingTipsReferences()
        {
            RepairReport report = RepairPrefabs(DrawLevelPrefabRoot);
            string log = report.BuildLog();
            Debug.Log(log);

            string dialogMessage = report.FailureCount == 0
                ? $"已检查 {report.ScannedCount} 个 DLevel prefab。\n" +
                  $"修复 {report.SavedCount} 个，新增 {report.AddedReferenceCount} 条引用；" +
                  $"跳过 DLevel_001、DLevel_007、DLevel_022。\n详情见 Console。"
                : $"检查完成，但有 {report.FailureCount} 个失败项。\n" +
                  $"已修复 {report.SavedCount} 个，新增 {report.AddedReferenceCount} 条引用。\n" +
                  "详情见 Console。";
            EditorUtility.DisplayDialog(ReportTitle, dialogMessage, "确定");
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateRepairMissingTipsReferences()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode
                   && !EditorApplication.isCompiling
                   && AssetDatabase.IsValidFolder(DrawLevelPrefabRoot);
        }

        #endregion

        #region Repair Flow

        private static RepairReport RepairPrefabs(string rootFolder)
        {
            RepairReport report = new RepairReport(rootFolder);
            if (!AssetDatabase.IsValidFolder(rootFolder))
            {
                report.AddFailure(rootFolder, $"Prefab 目录不存在：{rootFolder}");
                return report;
            }

            string[] prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { rootFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsDrawLevelPrefabPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            try
            {
                for (int i = 0; i < prefabPaths.Length; i++)
                {
                    string prefabPath = prefabPaths[i];
                    float progress = prefabPaths.Length == 0 ? 1f : (float)i / prefabPaths.Length;
                    EditorUtility.DisplayProgressBar(ReportTitle, prefabPath, progress);

                    if (IsExcludedPrefab(prefabPath))
                    {
                        report.AddExcluded(prefabPath);
                        continue;
                    }

                    RepairOnePrefab(prefabPath, report);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return report;
        }

        private static void RepairOnePrefab(string prefabPath, RepairReport report)
        {
            GameObject prefabRoot = null;

            try
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                if (prefabRoot == null)
                {
                    report.AddFailure(prefabPath, "LoadPrefabContents 返回 null。");
                    return;
                }

                global::DrawLineFeature[] drawFeatures =
                    prefabRoot.GetComponentsInChildren<global::DrawLineFeature>(true);
                if (drawFeatures.Length == 0)
                {
                    report.AddNoDrawFeature(prefabPath);
                    return;
                }

                int addedReferenceCount = 0;
                for (int i = 0; i < drawFeatures.Length; i++)
                    addedReferenceCount += AppendMissingTipsReferences(drawFeatures[i]);

                if (addedReferenceCount == 0)
                {
                    report.AddUnchanged(prefabPath);
                    return;
                }

                GameObject savedRoot = PrefabUtility.SaveAsPrefabAsset(
                    prefabRoot,
                    prefabPath,
                    out bool success);
                if (success && savedRoot != null)
                {
                    report.AddSaved(prefabPath, addedReferenceCount);
                }
                else
                {
                    report.AddFailure(prefabPath, "SaveAsPrefabAsset 返回失败。");
                }
            }
            catch (Exception exception)
            {
                report.AddFailure(prefabPath, exception.Message);
            }
            finally
            {
                if (prefabRoot != null)
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static int AppendMissingTipsReferences(global::DrawLineFeature drawFeature)
        {
            if (drawFeature == null)
                return 0;

            List<GameObject> candidates = FindHintObjects(drawFeature.transform);
            if (candidates.Count == 0)
                return 0;

            SerializedObject serializedFeature = new SerializedObject(drawFeature);
            SerializedProperty tipsProperty = serializedFeature.FindProperty(TipsPropertyName);
            if (tipsProperty == null || !tipsProperty.isArray)
            {
                throw new InvalidOperationException(
                    $"{drawFeature.name} 缺少可序列化数组字段 {TipsPropertyName}。");
            }

            int addedCount = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                GameObject candidate = candidates[i];
                if (ContainsObjectReference(tipsProperty, candidate))
                    continue;

                int newIndex = tipsProperty.arraySize;
                tipsProperty.InsertArrayElementAtIndex(newIndex);
                tipsProperty.GetArrayElementAtIndex(newIndex).objectReferenceValue = candidate;
                addedCount++;
            }

            if (addedCount > 0)
            {
                serializedFeature.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(drawFeature);
            }

            return addedCount;
        }

        private static List<GameObject> FindHintObjects(Transform root)
        {
            List<GameObject> results = new List<GameObject>();
            if (root == null)
                return results;

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child == root || !IsHintObjectName(child.name))
                    continue;

                if (!results.Contains(child.gameObject))
                    results.Add(child.gameObject);
            }

            return results;
        }

        private static bool ContainsObjectReference(
            SerializedProperty arrayProperty,
            UnityEngine.Object target)
        {
            for (int i = 0; i < arrayProperty.arraySize; i++)
            {
                if (arrayProperty.GetArrayElementAtIndex(i).objectReferenceValue == target)
                    return true;
            }

            return false;
        }

        private static bool IsHintObjectName(string objectName)
        {
            return objectName == LegacyTipsObjectName
                   || objectName == TipsObjectName
                   || objectName.StartsWith(TipsObjectName + " (", StringComparison.Ordinal);
        }

        private static bool IsDrawLevelPrefabPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)
                || !assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string prefabName = Path.GetFileNameWithoutExtension(assetPath);
            return prefabName.StartsWith(DrawLevelPrefabPrefix, StringComparison.Ordinal);
        }

        private static bool IsExcludedPrefab(string assetPath)
        {
            string prefabName = Path.GetFileNameWithoutExtension(assetPath);
            return prefabName == "DLevel_001"
                   || prefabName == "DLevel_007"
                   || prefabName == "DLevel_022";
        }

        #endregion

        #region Report

        private sealed class RepairReport
        {
            private readonly string rootFolder;
            private readonly List<string> savedPrefabs = new List<string>();
            private readonly List<string> unchangedPrefabs = new List<string>();
            private readonly List<string> excludedPrefabs = new List<string>();
            private readonly List<string> noDrawFeaturePrefabs = new List<string>();
            private readonly List<string> failures = new List<string>();

            public int SavedCount => savedPrefabs.Count;
            public int UnchangedCount => unchangedPrefabs.Count;
            public int ExcludedCount => excludedPrefabs.Count;
            public int NoDrawFeatureCount => noDrawFeaturePrefabs.Count;
            public int FailureCount => failures.Count;
            public int ScannedCount => SavedCount + UnchangedCount + NoDrawFeatureCount;
            public int AddedReferenceCount { get; private set; }

            public RepairReport(string rootFolder)
            {
                this.rootFolder = rootFolder;
            }

            public void AddSaved(string prefabPath, int addedReferenceCount)
            {
                savedPrefabs.Add($"{prefabPath}（新增引用：{addedReferenceCount}）");
                AddedReferenceCount += addedReferenceCount;
            }

            public void AddUnchanged(string prefabPath)
            {
                unchangedPrefabs.Add(prefabPath);
            }

            public void AddExcluded(string prefabPath)
            {
                excludedPrefabs.Add(prefabPath);
            }

            public void AddNoDrawFeature(string prefabPath)
            {
                noDrawFeaturePrefabs.Add(prefabPath);
            }

            public void AddFailure(string prefabPath, string reason)
            {
                failures.Add($"{prefabPath}: {reason}");
            }

            public string BuildLog()
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine($"[{ReportTitle}] Prefab root: {rootFolder}");
                builder.AppendLine($"[{ReportTitle}] Scanned: {ScannedCount}");
                builder.AppendLine($"[{ReportTitle}] Saved: {SavedCount}");
                builder.AppendLine($"[{ReportTitle}] Added references: {AddedReferenceCount}");
                builder.AppendLine($"[{ReportTitle}] Unchanged: {UnchangedCount}");
                builder.AppendLine($"[{ReportTitle}] Excluded DLevel_001/007/022: {ExcludedCount}");
                builder.AppendLine($"[{ReportTitle}] No DrawLineFeature: {NoDrawFeatureCount}");
                builder.AppendLine($"[{ReportTitle}] Failures: {FailureCount}");

                AppendSection(builder, "Saved Prefabs", savedPrefabs);
                AppendSection(builder, "Excluded Prefabs", excludedPrefabs);
                AppendSection(builder, "No DrawLineFeature", noDrawFeaturePrefabs);
                AppendSection(builder, "Failures", failures);
                return builder.ToString();
            }

            private static void AppendSection(
                StringBuilder builder,
                string title,
                IReadOnlyList<string> lines)
            {
                if (lines.Count == 0)
                    return;

                builder.AppendLine($"[{ReportTitle}] ---- {title} ----");
                for (int i = 0; i < lines.Count; i++)
                    builder.AppendLine(lines[i]);
            }
        }

        #endregion
    }
}
