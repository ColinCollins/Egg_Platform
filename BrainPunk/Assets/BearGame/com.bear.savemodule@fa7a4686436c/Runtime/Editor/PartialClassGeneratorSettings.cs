using System.IO;
using UnityEditor;
using UnityEngine;

namespace Bear.SaveModule.Editor
{
    /// <summary>
    /// Partial 类生成器配置窗口
    /// </summary>
    public class PartialClassGeneratorSettings : EditorWindow
    {
        private const string PREF_KEY_TARGET_PATH = "Bear.SaveModule.PartialClassGenerator.TargetPath";
        private const string PREF_KEY_FORCE_REFRESH = "Bear.SaveModule.PartialClassGenerator.ForceRefresh";

        private string _targetPath = "";
        private bool _forceRefresh = false;
        private Object _targetPathObject;
        private Vector2 _scrollPosition;

        [MenuItem("Tools/Save Module/Partial Class Generator Settings", false, 3)]
        public static void ShowWindow()
        {
            GetWindow<PartialClassGeneratorSettings>("Partial Class Generator");
        }

        private void OnEnable()
        {
            LoadSettings();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            GUILayout.Label("Partial Class Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // ========== 配置区域 ==========
            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 目标路径设置
            EditorGUILayout.LabelField("Target Folder/File:", EditorStyles.miniLabel);
            EditorGUILayout.BeginHorizontal();
            
            Object newTargetPathObject = EditorGUILayout.ObjectField(
                _targetPathObject, typeof(Object), false);

            // 如果通过 ObjectField 选择了新对象，更新路径
            if (newTargetPathObject != _targetPathObject)
            {
                _targetPathObject = newTargetPathObject;
                if (_targetPathObject != null)
                {
                    string newPath = AssetDatabase.GetAssetPath(_targetPathObject);
                    if (!string.IsNullOrEmpty(newPath))
                    {
                        _targetPath = newPath.Replace('\\', '/');
                    }
                }
                else
                {
                    _targetPath = "";
                }
            }

            if (GUILayout.Button("Use Selected", GUILayout.Width(100)))
            {
                if (Selection.activeObject != null)
                {
                    string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
                    if (!string.IsNullOrEmpty(selectedPath))
                    {
                        _targetPath = selectedPath.Replace('\\', '/');
                        UpdateTargetPathObject();
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "Please select a folder or file in the Project window.", "OK");
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Current Path:", EditorStyles.miniLabel);
            
            // 显示路径状态
            bool isValidPath = IsValidTargetPath(_targetPath);
            if (!string.IsNullOrEmpty(_targetPath))
            {
                if (isValidPath)
                {
                    EditorGUILayout.SelectableLabel(_targetPath, EditorStyles.textField, GUILayout.Height(20));
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        $"Invalid path: {_targetPath}\nPlease select a folder within the Assets directory.",
                        MessageType.Warning);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No target path set. Please select a folder or file.", MessageType.Info);
            }

            EditorGUILayout.Space();

            // 强制刷新选项
            _forceRefresh = EditorGUILayout.Toggle("Force Refresh Partial Files", _forceRefresh);
            EditorGUILayout.HelpBox(
                "If enabled, existing partial files will be overwritten during batch generation.\n" +
                "If disabled, existing partial files will be skipped.",
                MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            // ========== 批量生成区域 ==========
            EditorGUILayout.LabelField("Batch Generation", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 批量生成按钮
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_targetPath) || !isValidPath);
            
            if (GUILayout.Button("Batch Generate Partial Classes", GUILayout.Height(35)))
            {
                GenerateWithCurrentSettings();
            }
            
            EditorGUI.EndDisabledGroup();

            if (string.IsNullOrEmpty(_targetPath) || !isValidPath)
            {
                EditorGUILayout.HelpBox(
                    "Please set a valid target folder first to enable batch generation.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"Will generate partial classes for all scripts in:\n{GetFolderPath(_targetPath)}",
                    MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            // ========== 设置管理区域 ==========
            EditorGUILayout.LabelField("Settings Management", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 操作按钮
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Save Settings"))
            {
                SaveSettings();
                EditorUtility.DisplayDialog("Success", "Settings saved successfully!", "OK");
            }

            if (GUILayout.Button("Load Settings"))
            {
                LoadSettings();
                EditorUtility.DisplayDialog("Success", "Settings loaded successfully!", "OK");
            }

            if (GUILayout.Button("Reset"))
            {
                if (EditorUtility.DisplayDialog("Reset Settings",
                    "Are you sure you want to reset all settings to default?", "Yes", "No"))
                {
                    ResetSettings();
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndScrollView();
        }

        private void UpdateTargetPathObject()
        {
            if (!string.IsNullOrEmpty(_targetPath))
            {
                _targetPathObject = AssetDatabase.LoadAssetAtPath<Object>(_targetPath);
            }
            else
            {
                _targetPathObject = null;
            }
        }

        private void SaveSettings()
        {
            EditorPrefs.SetString(PREF_KEY_TARGET_PATH, _targetPath);
            EditorPrefs.SetBool(PREF_KEY_FORCE_REFRESH, _forceRefresh);
        }

        private void LoadSettings()
        {
            _targetPath = EditorPrefs.GetString(PREF_KEY_TARGET_PATH, "");
            _forceRefresh = EditorPrefs.GetBool(PREF_KEY_FORCE_REFRESH, false);
            UpdateTargetPathObject();
        }

        private void ResetSettings()
        {
            _targetPath = "";
            _forceRefresh = false;
            _targetPathObject = null;
            SaveSettings();
        }

        /// <summary>
        /// 验证目标路径是否有效
        /// </summary>
        private bool IsValidTargetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            // 确保路径在 Assets 目录下
            if (!path.StartsWith("Assets/") && path != "Assets")
            {
                return false;
            }

            // 检查路径是否存在
            if (AssetDatabase.IsValidFolder(path))
            {
                return true;
            }

            // 如果是文件，检查其所在文件夹
            if (File.Exists(path) || AssetDatabase.LoadAssetAtPath<Object>(path) != null)
            {
                string directory = Path.GetDirectoryName(path);
                return !string.IsNullOrEmpty(directory) && 
                       (directory.StartsWith("Assets/") || directory == "Assets");
            }

            return false;
        }

        /// <summary>
        /// 获取文件夹路径（如果是文件则返回其所在文件夹）
        /// </summary>
        private string GetFolderPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "";
            }

            if (AssetDatabase.IsValidFolder(path))
            {
                return path;
            }

            // 如果是文件，获取其所在文件夹
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                return directory.Replace('\\', '/');
            }

            return path;
        }

        /// <summary>
        /// 使用当前设置进行批量生成
        /// </summary>
        private void GenerateWithCurrentSettings()
        {
            if (string.IsNullOrEmpty(_targetPath))
            {
                EditorUtility.DisplayDialog("Error", "Please set a target folder or file first.", "OK");
                return;
            }

            if (!IsValidTargetPath(_targetPath))
            {
                EditorUtility.DisplayDialog("Error", 
                    "Invalid target path. Please select a folder or file within the Assets directory.", "OK");
                return;
            }

            string folderPath = GetFolderPath(_targetPath);

            if (string.IsNullOrEmpty(folderPath))
            {
                EditorUtility.DisplayDialog("Error", "Cannot determine folder path.", "OK");
                return;
            }

            // 保存设置
            SaveSettings();

            // 执行批量生成
            PartialClassGenerator.BatchGeneratePartialClassesInFolder(folderPath, _forceRefresh);
        }

        /// <summary>
        /// 获取配置的目标路径
        /// </summary>
        public static string GetTargetPath()
        {
            return EditorPrefs.GetString(PREF_KEY_TARGET_PATH, "");
        }

        /// <summary>
        /// 获取配置的强制刷新选项
        /// </summary>
        public static bool GetForceRefresh()
        {
            return EditorPrefs.GetBool(PREF_KEY_FORCE_REFRESH, false);
        }
    }
}

