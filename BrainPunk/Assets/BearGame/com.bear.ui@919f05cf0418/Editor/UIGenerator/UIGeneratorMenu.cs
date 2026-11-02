using System.IO;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
namespace Game.Editor.UIGenerator
{
    public static class UIGeneratorMenu
    {
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

        [MenuItem("Tools/UI Generator/Generate GameTipsPopup")]
        public static void GenerateGameTipsPopup()
        {
            const string assetPath = "Assets/Game/UI/GameTipsPopup/Sprites/GameTipsPopup.json";
            string fullPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath).Replace('\\', '/');
            if (!File.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("Error", $"JSON not found: {assetPath}", "OK");
                return;
            }
            GenerateUIPrefab(fullPath, "Assets/Game/UI/GameTipsPopup/Resources");
        }

        [MenuItem("Tools/UI Generator/Generate ShopPanel")]
        public static void GenerateShopPanel()
        {
            const string assetPath = "Assets/Game/UI/ShopPanel/Sprites/ShopPanel.json";
            string fullPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath).Replace('\\', '/');
            if (!File.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("Error", $"JSON not found: {assetPath}", "OK");
                return;
            }
            GenerateUIPrefab(fullPath, "Assets/Game/UI/ShopPanel/Resources");
        }

        private static void GenerateUIPrefab(string jsonPath, string outputPathOverride = null)
        {
            try
            {
                // 获取配置
                UIGeneratorSettings settings = UIGeneratorSettings.Instance;
                
                // 使用配置创建生成器
                UIGeneratorConfig config = new UIGeneratorConfig();
#if TextMeshPro
                if (settings.defaultTMPFont != null)
                {
                    config.DefaultFont = settings.defaultTMPFont;
                }
#else
                if (settings.defaultLegacyFont != null)
                {
                    config.DefaultLegacyFont = settings.defaultLegacyFont;
                }
#endif
                if (!string.IsNullOrEmpty(settings.defaultSpriteFolder))
                {
                    config.DefaultSpriteFolder = settings.defaultSpriteFolder.Replace('\\', '/');
                }
                
                UIGenerator generator = new UIGenerator(config);
                GameObject prefabRoot = generator.GenerateUIPrefab(jsonPath);

                if (prefabRoot == null)
                {
                    EditorUtility.DisplayDialog("Error", "Failed to generate UI prefab. Check console for details.", "OK");
                    return;
                }

                // 输出路径：显式 override > JSON 同级目录 > Settings 兜底
                string outputPath;
                if (!string.IsNullOrEmpty(outputPathOverride))
                {
                    outputPath = outputPathOverride.Replace('\\', '/');
                }
                else
                {
                    outputPath = UIGenerator.GetJsonSiblingOutputDirectory(jsonPath);
                    if (string.IsNullOrEmpty(outputPath))
                    {
                        outputPath = settings.outputPath;
                    }
                }

                if (!Directory.Exists(outputPath))
                {
                    Directory.CreateDirectory(outputPath);
                }

                string fileName = Path.GetFileNameWithoutExtension(jsonPath);
                string prefabPath = Path.Combine(outputPath, $"{fileName}.prefab").Replace('\\', '/');

                // 保存为 Prefab
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);

                if (prefab != null)
                {
                    UIGeneratorPostProcessPipeline.Invoke(prefabRoot, prefabPath, jsonPath);
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    AssetDatabase.SaveAssets();

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

