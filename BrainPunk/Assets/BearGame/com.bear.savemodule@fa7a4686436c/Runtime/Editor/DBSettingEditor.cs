using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Bear.SaveModule.Editor
{
    /// <summary>
    /// DBSetting 编辑器窗口
    /// </summary>
    public class DBSettingEditor : EditorWindow
    {
        private DBSetting _dbSetting;
        private Vector2 _scrollPosition;
        private string _searchFilter = "";

        [MenuItem("Tools/Save Module/DB Setting Manager", false, 4)]
        public static void ShowWindow()
        {
            GetWindow<DBSettingEditor>("DB Setting Manager");
        }

        private void OnEnable()
        {
            // 尝试加载默认的 DBSetting
            string[] guids = AssetDatabase.FindAssets("t:DBSetting");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _dbSetting = AssetDatabase.LoadAssetAtPath<DBSetting>(path);
            }
        }

        private void OnGUI()
        {
            GUILayout.Label("DB Setting Manager", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // DBSetting 选择
            EditorGUILayout.BeginHorizontal();
            _dbSetting = (DBSetting)EditorGUILayout.ObjectField(
                "DB Setting", _dbSetting, typeof(DBSetting), false);

            if (GUILayout.Button("Create New", GUILayout.Width(100)))
            {
                CreateNewDBSetting();
            }

            EditorGUILayout.EndHorizontal();

            if (_dbSetting == null)
            {
                EditorGUILayout.HelpBox("Please select or create a DB Setting asset.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space();

            // 扫描按钮
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Scan All BaseSaveDataSO Classes", GUILayout.Height(30)))
            {
                ScanDataClasses();
            }

            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.HelpBox(
                "Scan will find all classes that inherit from BaseSaveDataSO across all namespaces.",
                MessageType.Info);

            EditorGUILayout.Space();

            // 搜索过滤
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search:", GUILayout.Width(60));
            _searchFilter = EditorGUILayout.TextField(_searchFilter);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // 数据类列表
            EditorGUILayout.LabelField($"Data Classes ({_dbSetting.DataClasses.Count}):", EditorStyles.boldLabel);

            if (_dbSetting.DataClasses.Count == 0)
            {
                EditorGUILayout.HelpBox("No data classes found. Click 'Scan' to scan the target namespace.", MessageType.Info);
            }
            else
            {
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

                var filteredClasses = _dbSetting.DataClasses.Where(c =>
                    string.IsNullOrEmpty(_searchFilter) ||
                    c.className.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    c.filePath.IndexOf(_searchFilter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

                foreach (var dataClass in filteredClasses)
                {
                    DrawDataClassItem(dataClass);
                }

                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.Space();

            // 操作按钮
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Apply Changes", GUILayout.Height(30)))
            {
                ApplyChanges();
            }

            if (GUILayout.Button("Refresh", GUILayout.Height(30)))
            {
                ScanDataClasses();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void CreateNewDBSetting()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create DB Setting",
                "DBSetting",
                "asset",
                "Please enter a file name for the DB Setting asset");

            if (!string.IsNullOrEmpty(path))
            {
                DBSetting newSetting = CreateInstance<DBSetting>();
                AssetDatabase.CreateAsset(newSetting, path);
                AssetDatabase.SaveAssets();
                _dbSetting = newSetting;
            }
        }

        private void ScanDataClasses()
        {
            if (_dbSetting == null)
            {
                return;
            }

            // 使用 DBSetting 的扫描方法，传入文件路径解析器
            _dbSetting.ScanDataClasses(GetScriptFilePath);
            
            EditorUtility.SetDirty(_dbSetting);
            AssetDatabase.SaveAssets();
        }

        private string GetScriptFilePath(Type type)
        {
            // 通过 MonoScript 获取文件路径
            MonoScript[] scripts = Resources.FindObjectsOfTypeAll<MonoScript>();
            
            foreach (MonoScript script in scripts)
            {
                if (script.GetClass() == type)
                {
                    string path = AssetDatabase.GetAssetPath(script);
                    return path;
                }
            }

            // 如果找不到 MonoScript，尝试通过类型名和命名空间查找文件
            // 这是一个备用方案，可能不够准确
            string[] guids = AssetDatabase.FindAssets($"{type.Name} t:MonoScript");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script != null && script.GetClass() == type)
                {
                    return path;
                }
            }

            return null;
        }

        private void DrawDataClassItem(DBSetting.DataClassInfo dataClass)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            
            // 有效性指示器
            GUIStyle statusStyle = new GUIStyle(EditorStyles.label);
            statusStyle.normal.textColor = dataClass.isValid ? Color.green : Color.yellow;
            EditorGUILayout.LabelField(dataClass.isValid ? "✓" : "!", statusStyle, GUILayout.Width(20));

            // 类名
            EditorGUILayout.LabelField(dataClass.className, EditorStyles.boldLabel, GUILayout.Width(200));

            // 存储类型
            StorageType newStorageType = (StorageType)EditorGUILayout.EnumPopup(
                dataClass.storageType, GUILayout.Width(120));

            if (newStorageType != dataClass.storageType)
            {
                dataClass.storageType = newStorageType;
                EditorUtility.SetDirty(_dbSetting);
            }

            EditorGUILayout.EndHorizontal();

            // 文件路径
            EditorGUILayout.LabelField(dataClass.filePath, EditorStyles.miniLabel);

            if (!dataClass.isValid)
            {
                EditorGUILayout.HelpBox(
                    "Static StorageType field not found. Will be added when applying changes.",
                    MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        private void ApplyChanges()
        {
            if (_dbSetting == null || _dbSetting.DataClasses.Count == 0)
            {
                EditorUtility.DisplayDialog("Info", "No data classes to update.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Confirm",
                $"This will modify {_dbSetting.DataClasses.Count} script file(s). Continue?",
                "Yes", "No"))
            {
                return;
            }

            int successCount = 0;
            int errorCount = 0;
            List<string> errorMessages = new List<string>();

            foreach (var dataClass in _dbSetting.DataClasses)
            {
                try
                {
                    if (string.IsNullOrEmpty(dataClass.filePath) || !File.Exists(dataClass.filePath))
                    {
                        errorCount++;
                        errorMessages.Add($"{dataClass.className}: File not found");
                        continue;
                    }

                    string content = File.ReadAllText(dataClass.filePath);

                    // 检查是否已有静态 StorageType 字段
                    bool hasStaticField = Regex.IsMatch(content,
                        @"public\s+static\s+StorageType\s+StorageType\s*=");

                    if (hasStaticField)
                    {
                        // 替换现有的静态字段值
                        content = Regex.Replace(content,
                            @"(public\s+static\s+StorageType\s+StorageType\s*=\s*StorageType\.)\w+;",
                            $"$1{dataClass.storageType};");
                    }
                    else
                    {
                        // 添加静态字段
                        // 找到类定义的位置，在类内部添加静态字段
                        // 支持多种继承方式：BaseSaveDataSO 或 ScriptableObject
                        Match classMatch = Regex.Match(content,
                            @"(public\s+(?:partial\s+)?class\s+" + Regex.Escape(dataClass.className) +
                            @"\s*:\s*(?:.*BaseSaveDataSO|.*ScriptableObject).*\{)");

                        if (classMatch.Success)
                        {
                            string classDeclaration = classMatch.Groups[1].Value;
                            string staticFieldDeclaration = $"\n        public static StorageType StorageType = StorageType.{dataClass.storageType};\n";
                            content = content.Replace(classDeclaration, classDeclaration + staticFieldDeclaration);
                        }
                        else
                        {
                            // 尝试更宽松的匹配
                            Match looseMatch = Regex.Match(content,
                                @"(public\s+(?:partial\s+)?class\s+" + Regex.Escape(dataClass.className) +
                                @"\s*:\s*[^{]+\{)");
                            
                            if (looseMatch.Success)
                            {
                                string classDeclaration = looseMatch.Groups[1].Value;
                                string staticFieldDeclaration = $"\n        public static StorageType StorageType = StorageType.{dataClass.storageType};\n";
                                content = content.Replace(classDeclaration, classDeclaration + staticFieldDeclaration);
                            }
                            else
                            {
                                errorCount++;
                                errorMessages.Add($"{dataClass.className}: Cannot find class declaration");
                                continue;
                            }
                        }
                    }

                    File.WriteAllText(dataClass.filePath, content);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    errorMessages.Add($"{dataClass.className}: {ex.Message}");
                }
            }

            AssetDatabase.Refresh();

            // 生成 DBManager 代码
            bool codeGenerated = DBManagerGenerator.GenerateAndSave(_dbSetting, false);
            
            // 显示结果
            string resultMessage = $"Apply completed!\n\n" +
                $"Success: {successCount}\n" +
                $"Errors: {errorCount}";
            
            if (codeGenerated)
            {
                resultMessage += "\n\nDBManager code generated successfully.";
            }
            else
            {
                resultMessage += "\n\nDBManager code generation skipped (no valid data classes).";
            }

            if (errorMessages.Count > 0)
            {
                resultMessage += "\n\nErrors:\n" + string.Join("\n", errorMessages.Take(10));
                if (errorMessages.Count > 10)
                {
                    resultMessage += $"\n... and {errorMessages.Count - 10} more errors";
                }
            }

            EditorUtility.DisplayDialog("Apply Result", resultMessage, "OK");

            // 重新扫描以更新状态
            ScanDataClasses();
        }
    }
}

