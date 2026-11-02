using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Bear.SaveModule.Editor
{
    /// <summary>
    /// 保存数据脚本生成器
    /// </summary>
    public class SaveDataScriptGenerator : EditorWindow
    {
        private string _scriptName = "";
        private List<FieldInfo> _fields = new List<FieldInfo>();
        private Vector2 _scrollPosition;
        
        [MenuItem("Assets/Create/Save Data/New Save Data Script", false, 1)]
        public static void ShowWindow()
        {
            GetWindow<SaveDataScriptGenerator>("Save Data Script Generator");
        }
        
        private void OnGUI()
        {
            GUILayout.Label("Save Data Script Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            _scriptName = EditorGUILayout.TextField("Script Name", _scriptName);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Fields", EditorStyles.boldLabel);
            
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            for (int i = 0; i < _fields.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                
                _fields[i].Type = EditorGUILayout.TextField("Type", _fields[i].Type);
                _fields[i].Name = EditorGUILayout.TextField("Name", _fields[i].Name);
                
                if (GUILayout.Button("Remove", GUILayout.Width(60)))
                {
                    _fields.RemoveAt(i);
                    break;
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Add Field"))
            {
                _fields.Add(new FieldInfo { Type = "int", Name = "field" });
            }
            
            EditorGUILayout.Space();
            
            EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(_scriptName));
            if (GUILayout.Button("Generate Script"))
            {
                GenerateScript();
            }
            EditorGUI.EndDisabledGroup();
        }
        
        private void GenerateScript()
        {
            if (string.IsNullOrEmpty(_scriptName))
            {
                EditorUtility.DisplayDialog("Error", "Script name cannot be empty", "OK");
                return;
            }
            
            string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(selectedPath))
            {
                selectedPath = "Assets";
            }
            
            if (!AssetDatabase.IsValidFolder(selectedPath))
            {
                selectedPath = Path.GetDirectoryName(selectedPath);
            }
            
            string scriptPath = Path.Combine(selectedPath, $"{_scriptName}.cs");
            
            if (File.Exists(scriptPath))
            {
                if (!EditorUtility.DisplayDialog("File Exists", 
                    $"File {_scriptName}.cs already exists. Overwrite?", 
                    "Yes", "No"))
                {
                    return;
                }
            }
            
            string scriptContent = GenerateScriptContent();
            File.WriteAllText(scriptPath, scriptContent);
            
            AssetDatabase.Refresh();
            
            EditorUtility.DisplayDialog("Success", 
                $"Script {_scriptName}.cs generated successfully!", "OK");
            
            Close();
        }
        
        private string GenerateScriptContent()
        {
            StringBuilder sb = new StringBuilder();
            
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine();
            sb.AppendLine("namespace Bear.SaveModule");
            sb.AppendLine("{");
            // sb.AppendLine($"    [CreateAssetMenu(fileName = \"{_scriptName}\", menuName = \"Save Data/{_scriptName}\")]");
            sb.AppendLine($"    public partial class {_scriptName} : BaseSaveDataSO");
            sb.AppendLine("    {");
            
            foreach (var field in _fields)
            {
                if (!string.IsNullOrEmpty(field.Type) && !string.IsNullOrEmpty(field.Name))
                {
                    sb.AppendLine($"        [SerializeField] private {field.Type} {field.Name};");
                }
            }
            
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            return sb.ToString();
        }
        
        [Serializable]
        private class FieldInfo
        {
            public string Type = "int";
            public string Name = "field";
        }
    }
}

