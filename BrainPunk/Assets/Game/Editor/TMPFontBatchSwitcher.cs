using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 遍历指定文件夹下所有 Prefab，批量切换 TextMeshPro 字体（TMP_Text.font）。
/// 放在 Editor 目录下，避免进入运行时构建。
/// </summary>
public class TMPFontBatchSwitcher : EditorWindow
{
    [Header("Target")]
    [SerializeField] private DefaultAsset targetFolder;

    [Header("Font Replace")]
    [Tooltip("仅替换该字体（为空则替换所有 TMP_Text 的 font）")]
    [SerializeField] private TMP_FontAsset fromFont;

    [Tooltip("替换为该字体（必填）")]
    [SerializeField] private TMP_FontAsset toFont;

    [Header("Options")]
    [SerializeField] private bool includeSubfolders = true;
    [SerializeField] private bool includeInactive = true;
    [SerializeField] private bool dryRun = false;
    [SerializeField] private bool logEachPrefab = false;

    [MenuItem("Tools/Game/UI/TMP Font Batch Switcher")]
    public static void Open()
    {
        GetWindow<TMPFontBatchSwitcher>("TMP Font Switcher");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Batch switch TMP fonts in Prefabs", EditorStyles.boldLabel);

        targetFolder = (DefaultAsset)EditorGUILayout.ObjectField("Target Folder", targetFolder, typeof(DefaultAsset), false);
        fromFont = (TMP_FontAsset)EditorGUILayout.ObjectField("From Font (Optional)", fromFont, typeof(TMP_FontAsset), false);
        toFont = (TMP_FontAsset)EditorGUILayout.ObjectField("To Font", toFont, typeof(TMP_FontAsset), false);

        EditorGUILayout.Space(6);
        includeSubfolders = EditorGUILayout.ToggleLeft("Include Subfolders", includeSubfolders);
        includeInactive = EditorGUILayout.ToggleLeft("Include Inactive", includeInactive);
        dryRun = EditorGUILayout.ToggleLeft("Dry Run (no save)", dryRun);
        logEachPrefab = EditorGUILayout.ToggleLeft("Log each modified Prefab", logEachPrefab);

        using (new EditorGUI.DisabledScope(toFont == null || targetFolder == null))
        {
            if (GUILayout.Button("Run"))
            {
                Run();
            }
        }

        if (toFont == null)
        {
            EditorGUILayout.HelpBox("请指定 To Font。", MessageType.Warning);
        }

        if (targetFolder == null)
        {
            EditorGUILayout.HelpBox("请指定 Target Folder（Project 视图中的文件夹）。", MessageType.Info);
        }
    }

    private void Run()
    {
        string folderPath = AssetDatabase.GetAssetPath(targetFolder);
        if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
        {
            Debug.LogError($"[TMPFontBatchSwitcher] Invalid folder: {folderPath}");
            return;
        }

        // AssetDatabase.FindAssets 默认会递归搜索子文件夹
        // 如果 includeSubfolders 为 false，我们需要手动过滤结果
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
        
        // 如果不包含子文件夹，过滤掉不在直接子目录下的 Prefab
        if (!includeSubfolders)
        {
            System.Collections.Generic.List<string> filteredGuids = new System.Collections.Generic.List<string>();
            foreach (string guid in prefabGuids)
            {
                string prefabPath = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                string relativePath = prefabPath.Substring(folderPath.Length + 1);
                // 如果路径中没有 '/'，说明在直接子目录下
                if (!relativePath.Contains("/"))
                {
                    filteredGuids.Add(guid);
                }
            }
            prefabGuids = filteredGuids.ToArray();
        }
        
        int prefabCount = prefabGuids.Length;
        int modifiedPrefabCount = 0;
        int modifiedTextCount = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string guid = prefabGuids[i];
                string prefabPath = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                EditorUtility.DisplayProgressBar("TMP Font Batch Switcher", prefabPath, prefabCount == 0 ? 1f : (float)i / prefabCount);

                int changedInPrefab = ProcessPrefab(prefabPath, out bool saved);
                if (changedInPrefab > 0)
                {
                    modifiedTextCount += changedInPrefab;
                    modifiedPrefabCount++;
                    if (logEachPrefab)
                    {
                        Debug.Log($"[TMPFontBatchSwitcher] {(dryRun ? "[DryRun] " : "")}Modified {changedInPrefab} TMP_Text in: {prefabPath}");
                    }
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }

        Debug.Log($"[TMPFontBatchSwitcher] Done. Prefabs scanned={prefabCount}, prefabs modified={modifiedPrefabCount}, texts modified={modifiedTextCount}, dryRun={dryRun}");
    }

    private int ProcessPrefab(string prefabPath, out bool saved)
    {
        saved = false;
        int changed = 0;

        GameObject root = null;
        try
        {
            root = PrefabUtility.LoadPrefabContents(prefabPath);
            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(includeInactive);

            foreach (var t in texts)
            {
                if (t == null) continue;
                if (toFont == null) continue;

                // 如果指定了 fromFont，则仅替换匹配的
                if (fromFont != null && t.font != fromFont) continue;

                if (t.font == toFont) continue;

                t.font = toFont;
                EditorUtility.SetDirty(t);
                changed++;
            }

            if (changed > 0 && !dryRun)
            {
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                saved = true;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[TMPFontBatchSwitcher] Failed processing prefab: {prefabPath}\n{e}");
        }
        finally
        {
            if (root != null)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return changed;
    }
}

