#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;
#if TextMeshPro
using TMPro;
#endif

namespace Game.Editor.UIGenerator
{
    [CreateAssetMenu(fileName = "UIGeneratorSettings", menuName = "Game/UI Generator Settings", order = 1)]
    public class UIGeneratorSettings : ScriptableObject
    {
        private const string SettingsAssetPath = "Assets/Resources/UIGeneratorSettings.asset";

        [Header("输出设置")]
        [Tooltip("生成的 Prefab 保存路径")]
        public string outputPath = "Assets/Prefabs/UI/Generated";

        [Header("字体设置")]
#if TextMeshPro
        [Tooltip("默认 TMP 字体：JSON 中按名称找不到对应字体时使用（需安装 TextMesh Pro，且工程含 TextMeshPro 宏）")]
        public TMP_FontAsset defaultTMPFont;
#else
        [Tooltip("默认 Unity UI Text 字体：无 TMP 时 UIGenerator 使用 Text 组件；JSON 中按名称找不到时使用此 Font")]
        public Font defaultLegacyFont;
#endif

        [Header("文件管理预设")]
        [Tooltip("Image 生成时若在 JSON 同目录找不到对应资源，会在此目录下遍历所有 Sprite 按名称匹配")]
        public string defaultSpriteFolder = "Assets/Sprites";

        private static UIGeneratorSettings _instance;

        public static UIGeneratorSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = AssetDatabase.LoadAssetAtPath<UIGeneratorSettings>(SettingsAssetPath);
                    if (_instance == null)
                    {
                        _instance = CreateInstance<UIGeneratorSettings>();
                        string folder = Path.GetDirectoryName(SettingsAssetPath)?.Replace('\\', '/');
                        if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
                        {
                            Directory.CreateDirectory(folder);
                            AssetDatabase.Refresh();
                        }

                        AssetDatabase.CreateAsset(_instance, SettingsAssetPath);
                        AssetDatabase.SaveAssets();
                    }
                }

                return _instance;
            }
        }

        [MenuItem("Tools/UI Generator/Settings")]
        public static void OpenSettings()
        {
            Selection.activeObject = Instance;
            EditorUtility.FocusProjectWindow();
            EditorGUIUtility.PingObject(Instance);
        }
    }
}
#endif
