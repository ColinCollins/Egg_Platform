using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Dig;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Bear.Editor
{
    /// <summary>
    /// DigGame 地图批量刷新工具：逐个调用关卡现有 InitMap，并立即保存对应 Prefab。
    /// </summary>
    public static class DigGameMapRefreshTool
    {
        #region 常量与状态

        private const string MenuPath = "Tools/Bear/DigGame/刷新所有 DigLevel 地图";
        private const string PrefabRoot = "Assets/Game/Prefabs/DigLevel";
        private const string PrefabPrefix = "DigLevel_";
        private const string ReportTitle = "DigGame 地图刷新";
        private static bool isRefreshing;

        #endregion

        #region 菜单入口

        /// <summary>
        /// 刷新并保存 DigLevel 目录中的全部普通关卡地图。
        /// 调用边界：仅允许在非播放、非编译状态下手动执行；不初始化玩法、DB 或存档系统。
        /// </summary>
        [MenuItem(MenuPath, false, 220)]
        public static void RefreshAllDigLevelMaps()
        {
            if (isRefreshing)
                return;

            isRefreshing = true;
            RefreshReport report;
            try
            {
                report = RefreshPrefabs();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                isRefreshing = false;
            }

            string log = report.BuildLog();
            if (report.FailedPaths.Count > 0)
                Debug.LogError(log);
            else
                Debug.Log(log);

            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    ReportTitle,
                    $"已刷新 {report.RefreshedCount} 关，跳过 {report.SkippedPaths.Count} 关，失败 {report.FailedPaths.Count} 关。" +
                    (report.Canceled ? "\n用户已取消，取消前完成的关卡已经保存。" : string.Empty) +
                    "\n详情见 Console。",
                    "确定");
            }
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateRefreshAllDigLevelMaps()
        {
            return !isRefreshing
                   && !EditorApplication.isPlayingOrWillChangePlaymode
                   && !EditorApplication.isCompiling
                   && AssetDatabase.IsValidFolder(PrefabRoot);
        }

        #endregion

        #region 批量刷新

        private static RefreshReport RefreshPrefabs()
        {
            var report = new RefreshReport();
            string[] prefabPaths = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(IsDigLevelPrefabPath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();

            for (int i = 0; i < prefabPaths.Length; i++)
            {
                string prefabPath = prefabPaths[i];
                if (EditorUtility.DisplayCancelableProgressBar(
                        ReportTitle,
                        prefabPath,
                        prefabPaths.Length == 0 ? 1f : (float)i / prefabPaths.Length))
                {
                    report.Canceled = true;
                    break;
                }

                if (IsOpenInPrefabStage(prefabPath))
                {
                    report.SkippedPaths.Add($"{prefabPath}：正在 Prefab Stage 中编辑");
                    continue;
                }

                RefreshOnePrefab(prefabPath, report);
            }

            return report;
        }

        private static void RefreshOnePrefab(string prefabPath, RefreshReport report)
        {
            GameObject prefabRoot = null;
            try
            {
                prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                if (prefabRoot == null)
                    throw new InvalidOperationException("LoadPrefabContents 返回 null。");

                DigTerrainCtrl terrain = prefabRoot.GetComponent<DigTerrainCtrl>();
                if (terrain == null)
                {
                    report.SkippedPaths.Add($"{prefabPath}：根节点没有 DigTerrainCtrl");
                    return;
                }

                terrain.InitMapFromPrefabAsset(prefabPath);
                GameObject savedRoot = PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath, out bool success);
                if (!success || savedRoot == null)
                    throw new InvalidOperationException("SaveAsPrefabAsset 返回失败。");

                report.RefreshedCount++;
            }
            catch (Exception exception)
            {
                report.FailedPaths.Add($"{prefabPath}：{exception.Message}");
                Debug.LogException(exception);
            }
            finally
            {
                if (prefabRoot != null)
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static bool IsDigLevelPrefabPath(string path)
        {
            return string.Equals(Path.GetDirectoryName(path)?.Replace('\\', '/'), PrefabRoot, StringComparison.Ordinal)
                   && Path.GetFileNameWithoutExtension(path).StartsWith(PrefabPrefix, StringComparison.Ordinal);
        }

        private static bool IsOpenInPrefabStage(string prefabPath)
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            return stage != null && string.Equals(stage.assetPath, prefabPath, StringComparison.Ordinal);
        }

        #endregion

        #region 报告

        private sealed class RefreshReport
        {
            public int RefreshedCount;
            public bool Canceled;
            public readonly List<string> SkippedPaths = new List<string>();
            public readonly List<string> FailedPaths = new List<string>();

            public string BuildLog()
            {
                var builder = new StringBuilder();
                builder.AppendLine($"[{ReportTitle}] 刷新 {RefreshedCount}，跳过 {SkippedPaths.Count}，失败 {FailedPaths.Count}，取消 {Canceled}。");
                foreach (string item in SkippedPaths)
                    builder.AppendLine($"[跳过] {item}");
                foreach (string item in FailedPaths)
                    builder.AppendLine($"[失败] {item}");
                return builder.ToString();
            }
        }

        #endregion
    }
}
