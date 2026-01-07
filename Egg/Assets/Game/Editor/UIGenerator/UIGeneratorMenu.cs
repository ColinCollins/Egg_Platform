using System.IO;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
namespace Game.Editor.UIGenerator
{
    public static class UIGeneratorMenu
    {
        private const string OUTPUT_PATH = "Assets/Game/Prefabs/UI/Generated";

        [MenuItem("Tools/UI Generator/Generate from JSON")]
        public static void GenerateFromJSON()
        {
            string jsonPath = EditorUtility.OpenFilePanel("Select JSON File", "Assets/Game/ArtSrc", "json");
            
            if (string.IsNullOrEmpty(jsonPath))
            {
                return;
            }

            // 统一路径格式，确保跨平台兼容
            jsonPath = jsonPath.Replace('\\', '/');
            string dataPath = Application.dataPath.Replace('\\', '/');

            if (!jsonPath.StartsWith(dataPath))
            {
                EditorUtility.DisplayDialog("Error", "Please select a JSON file within the Assets folder.", "OK");
                return;
            }

            GenerateUIPrefab(jsonPath);
        }

        [MenuItem("Tools/UI Generator/Generate from Selected JSON")]
        public static void GenerateFromSelectedJSON()
        {
            Object selected = Selection.activeObject;
            
            if (selected == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a JSON file in the Project window.", "OK");
                return;
            }

            string jsonPath = AssetDatabase.GetAssetPath(selected);
            
            if (!jsonPath.EndsWith(".json"))
            {
                EditorUtility.DisplayDialog("Error", "Selected file is not a JSON file.", "OK");
                return;
            }

            GenerateUIPrefab(jsonPath);
        }

        [MenuItem("Assets/UI Generator/Generate UI", false, 0)]
        public static void GenerateUIFromContext()
        {
            Object selected = Selection.activeObject;
            
            if (selected == null)
            {
                return;
            }

            string jsonPath = AssetDatabase.GetAssetPath(selected);
            
            if (!jsonPath.EndsWith(".json"))
            {
                EditorUtility.DisplayDialog("Error", "Selected file is not a JSON file.", "OK");
                return;
            }

            GenerateUIPrefab(jsonPath);
        }

        [MenuItem("Assets/UI Generator/Generate UI", true)]
        public static bool ValidateGenerateUIFromContext()
        {
            Object selected = Selection.activeObject;
            if (selected == null)
                return false;

            string path = AssetDatabase.GetAssetPath(selected);
            return path.EndsWith(".json");
        }

        private static void GenerateUIPrefab(string jsonPath)
        {
            try
            {
                UIGenerator generator = new UIGenerator();
                GameObject prefabRoot = generator.GenerateUIPrefab(jsonPath);

                if (prefabRoot == null)
                {
                    EditorUtility.DisplayDialog("Error", "Failed to generate UI prefab. Check console for details.", "OK");
                    return;
                }

                // 确保输出目录存在
                if (!Directory.Exists(OUTPUT_PATH))
                {
                    Directory.CreateDirectory(OUTPUT_PATH);
                }

                // 获取文件名（不含扩展名）
                string fileName = Path.GetFileNameWithoutExtension(jsonPath);
                // 使用 Path.Combine 确保跨平台兼容性
                string prefabPath = Path.Combine(OUTPUT_PATH, $"{fileName}.prefab").Replace('\\', '/');

                // 保存为 Prefab
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                
                if (prefab != null)
                {
                    Debug.Log($"UI Prefab generated successfully: {prefabPath}");
                    Selection.activeObject = prefab;
                    EditorUtility.DisplayDialog("Success", $"UI Prefab generated successfully!\n\nPath: {prefabPath}", "OK");
                }
                else
                {
                    Debug.LogError("Failed to save prefab");
                    EditorUtility.DisplayDialog("Error", "Failed to save prefab.", "OK");
                }

                // 清理场景中的临时对象
                Object.DestroyImmediate(prefabRoot);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error generating UI prefab: {e.Message}\n{e.StackTrace}");
                EditorUtility.DisplayDialog("Error", $"Error generating UI prefab:\n{e.Message}", "OK");
            }
        }
    }
}
#endif

