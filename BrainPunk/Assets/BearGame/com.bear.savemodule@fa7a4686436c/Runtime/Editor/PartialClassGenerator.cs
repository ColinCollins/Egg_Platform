using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Bear.SaveModule.Editor
{
    /// <summary>
    /// Partial 类生成器
    /// </summary>
    public class PartialClassGenerator : EditorWindow
    {
        private MonoScript _selectedScript;
        private string _generatedCode = "";
        private Vector2 _scrollPosition;

        [MenuItem("Assets/Create/Save Data/Generate Partial Class", false, 2)]
        public static void ShowWindow()
        {
            GetWindow<PartialClassGenerator>("Partial Class Generator");
        }

        private void OnEnable()
        {
            if (Selection.activeObject is MonoScript script)
            {
                _selectedScript = script;
                GeneratePartialClass();
            }
        }

        private void OnSelectionChange()
        {
            if (Selection.activeObject is MonoScript script)
            {
                _selectedScript = script;
                GeneratePartialClass();
            }
        }

        private void OnGUI()
        {
            GUILayout.Label("Partial Class Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _selectedScript = (MonoScript)EditorGUILayout.ObjectField(
                "Select Script", _selectedScript, typeof(MonoScript), false);

            EditorGUILayout.Space();

            if (_selectedScript != null)
            {
                if (GUILayout.Button("Generate Partial Class"))
                {
                    GeneratePartialClass();
                }

                EditorGUILayout.Space();

                if (!string.IsNullOrEmpty(_generatedCode))
                {
                    EditorGUILayout.LabelField("Generated Code:", EditorStyles.boldLabel);
                    _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
                    EditorGUILayout.TextArea(_generatedCode, GUILayout.Height(300));
                    EditorGUILayout.EndScrollView();

                    EditorGUILayout.Space();

                    if (GUILayout.Button("Save to File"))
                    {
                        SavePartialClass();
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Please select a ScriptableObject script that inherits from BaseSaveDataSO",
                    MessageType.Info);
            }
        }

        private void GeneratePartialClass()
        {
            if (_selectedScript == null)
            {
                _generatedCode = "";
                return;
            }

            string scriptPath = AssetDatabase.GetAssetPath(_selectedScript);
            _generatedCode = GeneratePartialClassCode(scriptPath);
        }

        private string GeneratePartialClassCode(string scriptPath)
        {
            if (!File.Exists(scriptPath))
            {
                return "// Script file not found";
            }

            string scriptContent = File.ReadAllText(scriptPath);

            string className = ExtractClassName(scriptContent);
            if (string.IsNullOrEmpty(className))
            {
                return "// Could not extract class name from script";
            }

            string namespaceName = ExtractNamespace(scriptContent);
            
            // 检查是否继承自 BaseSaveDataSO
            bool inheritsFromBaseSaveDataSO = CheckInheritsFromBaseSaveDataSO(scriptContent);

            List<FieldInfo> fields = ExtractPrivateFields(scriptContent);

            if (fields.Count == 0)
            {
                return "// No private fields found with [SerializeField] attribute";
            }

            return BuildPartialClassCode(className, namespaceName, fields, inheritsFromBaseSaveDataSO, scriptPath);
        }

        private string BuildPartialClassCode(string className, string namespaceName, List<FieldInfo> fields, bool inheritsFromBaseSaveDataSO, string scriptPath)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using System.Collections.Generic;");
            if (inheritsFromBaseSaveDataSO)
            {
                sb.AppendLine("#if UNITY_EDITOR");
                sb.AppendLine("using UnityEditor;");
                sb.AppendLine("#endif");
            }
            sb.AppendLine();

            if (!string.IsNullOrEmpty(namespaceName))
            {
                sb.AppendLine($"namespace {namespaceName}");
                sb.AppendLine("{");
                sb.AppendLine($"    public partial class {className}");
                sb.AppendLine("    {");
            }
            else
            {
                sb.AppendLine($"public partial class {className}");
                sb.AppendLine("{");
            }

            string indent = string.IsNullOrEmpty(namespaceName) ? "    " : "        ";

            // 如果继承自 BaseSaveDataSO，添加 Init() 方法
            if (inheritsFromBaseSaveDataSO)
            {
                sb.AppendLine($"{indent}/// <summary>");
                sb.AppendLine($"{indent}/// 初始化数据（设置默认值）");
                sb.AppendLine($"{indent}/// </summary>");
                sb.AppendLine($"{indent}public override void Init()");
                sb.AppendLine($"{indent}{{");
                
                // 为每个字段生成初始化代码
                foreach (var field in fields)
                {
                    string initValue = GetDefaultValueForType(field.Type, field.DefaultValue);
                    sb.AppendLine($"{indent}    {field.Name} = {initValue};");
                }
                
                sb.AppendLine($"{indent}}}");
                sb.AppendLine();
            }

            // 生成属性
            foreach (var field in fields)
            {
                string propertyName = ToPascalCase(field.Name);
                sb.AppendLine($"{indent}public {field.Type} {propertyName}");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    get => {field.Name};");
                sb.AppendLine($"{indent}    set => {field.Name} = value;");
                sb.AppendLine($"{indent}}}");
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(namespaceName))
            {
                sb.AppendLine("    }");
                sb.AppendLine("}");
            }
            else
            {
                sb.AppendLine("}");
            }

            return sb.ToString();
        }
        
        /// <summary>
        /// 检查类是否继承自 BaseSaveDataSO
        /// </summary>
        private bool CheckInheritsFromBaseSaveDataSO(string scriptContent)
        {
            // 检查是否包含 BaseSaveDataSO
            return scriptContent.Contains("BaseSaveDataSO") || 
                   Regex.IsMatch(scriptContent, @":\s*.*BaseSaveDataSO");
        }
        
        /// <summary>
        /// 获取资源文件路径
        /// </summary>
        private string GetAssetPath(string scriptPath, string className)
        {
            string directory = Path.GetDirectoryName(scriptPath);
            string assetPath = Path.Combine(directory, $"{className}.asset").Replace('\\', '/');
            return assetPath;
        }
        
        /// <summary>
        /// 获取 Resources 路径（如果资源在 Resources 目录下）
        /// </summary>
        private string GetResourcesPath(string assetPath)
        {
            // 如果资源在 Resources 目录下，提取相对路径
            int resourcesIndex = assetPath.IndexOf("/Resources/");
            if (resourcesIndex >= 0)
            {
                string relativePath = assetPath.Substring(resourcesIndex + "/Resources/".Length);
                return Path.ChangeExtension(relativePath, null).Replace('\\', '/');
            }
            // 否则返回类名
            return Path.GetFileNameWithoutExtension(assetPath);
        }

        private string ExtractClassName(string scriptContent)
        {
            Match match = Regex.Match(scriptContent, @"public\s+(?:partial\s+)?class\s+(\w+)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            return null;
        }

        private string ExtractNamespace(string scriptContent)
        {
            Match match = Regex.Match(scriptContent, @"namespace\s+([\w.]+)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            return null;
        }

        /// <summary>
        /// 提取私有字段（带 [SerializeField] 属性）
        /// 支持字段前有注释、复杂泛型类型和初始化值
        /// </summary>
        private List<FieldInfo> ExtractPrivateFields(string scriptContent)
        {
            List<FieldInfo> fields = new List<FieldInfo>();

            // 使用两步方法：
            // 1. 先找到所有 [SerializeField] private 的位置
            // 2. 然后解析每个位置的类型和字段名
            
            // 匹配模式：允许前面有注释，然后是 [SerializeField] private
            string basePattern = @"(?:^\s*//[^\r\n]*[\r\n]+)?\s*\[SerializeField\]\s+private\s+";
            MatchCollection baseMatches = Regex.Matches(scriptContent, basePattern, RegexOptions.Multiline);

            foreach (Match baseMatch in baseMatches)
            {
                int startIndex = baseMatch.Index + baseMatch.Length;
                string remaining = scriptContent.Substring(startIndex);
                
                // 解析类型（可能包含泛型和数组）
                string type = ExtractType(remaining);
                if (string.IsNullOrEmpty(type))
                {
                    continue;
                }

                // 跳过类型后的空白字符
                int typeEndIndex = type.Length;
                while (typeEndIndex < remaining.Length && char.IsWhiteSpace(remaining[typeEndIndex]))
                {
                    typeEndIndex++;
                }

                // 提取字段名（标识符）
                string remainingAfterType = remaining.Substring(typeEndIndex);
                Match fieldNameMatch = Regex.Match(remainingAfterType, @"^(\w+)");
                if (!fieldNameMatch.Success)
                {
                    continue;
                }

                string fieldName = fieldNameMatch.Groups[1].Value;
                
                // 提取初始化值（如果有）
                string defaultValue = ExtractDefaultValue(remainingAfterType, fieldName);

                // 排除 storageType 字段
                if (fieldName != "storageType" && type != "StorageType")
                {
                    fields.Add(new FieldInfo
                    {
                        Type = type.Trim(),
                        Name = fieldName.Trim(),
                        DefaultValue = defaultValue
                    });
                }
            }

            return fields;
        }

        /// <summary>
        /// 提取字段的默认值（初始化值）
        /// </summary>
        private string ExtractDefaultValue(string remainingText, string fieldName)
        {
            // 跳过字段名
            int index = fieldName.Length;
            
            // 跳过空白字符
            while (index < remainingText.Length && char.IsWhiteSpace(remainingText[index]))
            {
                index++;
            }
            
            // 检查是否有 = 号
            if (index >= remainingText.Length || remainingText[index] != '=')
            {
                return null; // 没有初始化值
            }
            
            index++; // 跳过 =
            
            // 跳过空白字符
            while (index < remainingText.Length && char.IsWhiteSpace(remainingText[index]))
            {
                index++;
            }
            
            // 提取初始化值，直到遇到分号
            StringBuilder valueBuilder = new StringBuilder();
            int depth = 0; // 用于处理嵌套的括号
            
            while (index < remainingText.Length)
            {
                char c = remainingText[index];
                
                if (c == ';' && depth == 0)
                {
                    break; // 遇到分号且不在括号内，结束
                }
                
                if (c == '(' || c == '[' || c == '{' || c == '<')
                {
                    depth++;
                }
                else if (c == ')' || c == ']' || c == '}' || c == '>')
                {
                    depth--;
                }
                
                valueBuilder.Append(c);
                index++;
            }
            
            string defaultValue = valueBuilder.ToString().Trim();
            return string.IsNullOrEmpty(defaultValue) ? null : defaultValue;
        }

        /// <summary>
        /// 从字符串开头提取类型（支持泛型和数组）
        /// </summary>
        private string ExtractType(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            int index = 0;
            StringBuilder typeBuilder = new StringBuilder();

            // 跳过前导空白
            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            // 提取基本类型名（可能包含命名空间，如 System.Collections.Generic.List）
            while (index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] == '.'))
            {
                typeBuilder.Append(text[index]);
                index++;
            }

            // 处理泛型参数 <>
            if (index < text.Length && text[index] == '<')
            {
                typeBuilder.Append('<');
                index++;

                int depth = 1;
                while (index < text.Length && depth > 0)
                {
                    if (text[index] == '<')
                    {
                        depth++;
                    }
                    else if (text[index] == '>')
                    {
                        depth--;
                    }
                    typeBuilder.Append(text[index]);
                    index++;
                }
            }

            // 处理数组声明 []
            while (index < text.Length && text[index] == '[')
            {
                typeBuilder.Append('[');
                index++;
                
                // 匹配数组内容（可能包含逗号，如 [,]）
                while (index < text.Length && text[index] != ']')
                {
                    typeBuilder.Append(text[index]);
                    index++;
                }
                
                if (index < text.Length)
                {
                    typeBuilder.Append(']');
                    index++;
                }
            }

            return typeBuilder.ToString();
        }

        /// <summary>
        /// 获取类型的默认值字符串（用于 Init() 方法）
        /// </summary>
        private string GetDefaultValueForType(string type, string defaultValue)
        {
            // 如果有显式的初始化值，直接使用
            if (!string.IsNullOrEmpty(defaultValue))
            {
                return defaultValue;
            }

            // 根据类型返回默认值
            type = type.Trim();
            
            // 处理泛型类型（如 List<int>）
            if (type.Contains("<"))
            {
                int genericStart = type.IndexOf('<');
                string baseType = type.Substring(0, genericStart).Trim();
                
                if (baseType == "List" || baseType.Contains("List"))
                {
                    return "new " + type + "()";
                }
                
                if (baseType == "Dictionary" || baseType.Contains("Dictionary"))
                {
                    return "new " + type + "()";
                }
            }
            
            // 处理数组类型
            if (type.EndsWith("[]"))
            {
                return "new " + type.Replace("[]", "[0]");
            }
            
            // 基本类型默认值
            switch (type)
            {
                case "int":
                case "Int32":
                    return "0";
                case "long":
                case "Int64":
                    return "0L";
                case "float":
                case "Single":
                    return "0f";
                case "double":
                case "Double":
                    return "0d";
                case "bool":
                case "Boolean":
                    return "false";
                case "string":
                case "String":
                    return "\"\"";
                case "Vector2":
                    return "Vector2.zero";
                case "Vector3":
                    return "Vector3.zero";
                case "Vector4":
                    return "Vector4.zero";
                case "Color":
                    return "Color.white";
                case "Quaternion":
                    return "Quaternion.identity";
                default:
                    // 对于其他类型，尝试使用 new 构造
                    if (type.Contains("List") || type.Contains("Dictionary") || type.Contains("HashSet"))
                    {
                        return "new " + type + "()";
                    }
                    return "default(" + type + ")";
            }
        }

        private string ToPascalCase(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            return char.ToUpper(input[0]) + input.Substring(1);
        }

        private void SavePartialClass()
        {
            if (_selectedScript == null || string.IsNullOrEmpty(_generatedCode))
            {
                return;
            }

            string scriptPath = AssetDatabase.GetAssetPath(_selectedScript);
            SavePartialClassToFile(scriptPath, _generatedCode, true);
        }

        private bool SavePartialClassToFile(string scriptPath, string generatedCode, bool showDialog = false)
        {
            if (string.IsNullOrEmpty(generatedCode) || generatedCode.StartsWith("//"))
            {
                return false;
            }

            string directory = Path.GetDirectoryName(scriptPath);
            string fileName = Path.GetFileNameWithoutExtension(scriptPath);
            string partialPath = Path.Combine(directory, $"{fileName}_Partial.cs");

            if (File.Exists(partialPath))
            {
                if (showDialog)
                {
                    if (!EditorUtility.DisplayDialog("File Exists",
                        $"File {fileName}_Partial.cs already exists. Overwrite?",
                        "Yes", "No"))
                    {
                        return false;
                    }
                }
            }

            File.WriteAllText(partialPath, generatedCode);
            
            // 如果类继承自 BaseSaveDataSO，创建 ScriptableObject 资源文件
            string scriptContent = File.ReadAllText(scriptPath);
            if (CheckInheritsFromBaseSaveDataSO(scriptContent))
            {
                CreateScriptableObjectAsset(scriptPath, fileName);
            }
            
            AssetDatabase.Refresh();

            if (showDialog)
            {
                EditorUtility.DisplayDialog("Success",
                    $"Partial class {fileName}_Partial.cs generated successfully!", "OK");
            }

            return true;
        }
        
        /// <summary>
        /// 创建 ScriptableObject 资源文件
        /// </summary>
        private void CreateScriptableObjectAsset(string scriptPath, string className)
        {
            try
            {
                // 获取脚本的类型
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                if (script == null)
                {
                    Debug.LogWarning($"Cannot load script at path: {scriptPath}");
                    return;
                }
                
                System.Type scriptType = script.GetClass();
                if (scriptType == null)
                {
                    Debug.LogWarning($"Cannot get class type from script: {className}");
                    return;
                }
                
                // 检查是否继承自 BaseSaveDataSO
                System.Type baseType = typeof(BaseSaveDataSO);
                if (!baseType.IsAssignableFrom(scriptType))
                {
                    return; // 不是 BaseSaveDataSO 的子类，不创建资源
                }
                
                // 资源文件路径
                string directory = Path.GetDirectoryName(scriptPath);
                string assetPath = Path.Combine(directory, $"{className}.asset").Replace('\\', '/');
                
                // 如果资源已存在，不覆盖
                if (File.Exists(assetPath))
                {
                    return;
                }
                
                // 创建 ScriptableObject 实例
                ScriptableObject instance = ScriptableObject.CreateInstance(scriptType);
                if (instance == null)
                {
                    Debug.LogWarning($"Cannot create instance of type: {className}");
                    return;
                }
                
                // 保存资源
                AssetDatabase.CreateAsset(instance, assetPath);
                AssetDatabase.SaveAssets();
                
                Debug.Log($"Created ScriptableObject asset: {assetPath}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error creating ScriptableObject asset for {className}: {ex.Message}");
            }
        }

        #region 批量生成功能

        [MenuItem("Tools/Save Module/Batch Generate Partial Classes", false, 1)]
        public static void BatchGeneratePartialClasses()
        {
            string selectedPath = EditorUtility.OpenFolderPanel("Select Folder to Generate Partial Classes", "Assets", "");

            if (string.IsNullOrEmpty(selectedPath))
            {
                return;
            }

            // 转换为相对路径
            if (selectedPath.StartsWith(Application.dataPath))
            {
                selectedPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
            }
            else
            {
                EditorUtility.DisplayDialog("Error", "Please select a folder within the Assets directory.", "OK");
                return;
            }

            bool forceRefresh = PartialClassGeneratorSettings.GetForceRefresh();
            BatchGeneratePartialClassesInFolder(selectedPath, forceRefresh);
        }

        // [MenuItem("Assets/Save Module/Batch Generate Partial Classes", false, 1)]
        // [MenuItem("Tools/Save Module/Batch Generate Partial Classes (Selected Folder)", false, 2)]
        public static void BatchGeneratePartialClassesBySelctor()
        {
            if (Selection.activeObject == null)
            {
                EditorUtility.DisplayDialog("Error", "Please select a folder or file in the Project window.", "OK");
                return;
            }

            string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);

            if (string.IsNullOrEmpty(selectedPath))
            {
                EditorUtility.DisplayDialog("Error", "Invalid selection. Please select a folder or file.", "OK");
                return;
            }

            // 确保路径使用正斜杠
            selectedPath = selectedPath.Replace('\\', '/');

            // 检查是否是文件夹
            if (!AssetDatabase.IsValidFolder(selectedPath))
            {
                // 如果不是文件夹，尝试获取其所在文件夹
                string directory = Path.GetDirectoryName(selectedPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    selectedPath = directory.Replace('\\', '/');
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "Cannot determine folder path. Please select a folder in the Project window.", "OK");
                    return;
                }
            }

            // 确保路径在 Assets 目录下
            if (!selectedPath.StartsWith("Assets/") && selectedPath != "Assets")
            {
                EditorUtility.DisplayDialog("Error", "Please select a folder within the Assets directory.", "OK");
                return;
            }

            bool forceRefresh = PartialClassGeneratorSettings.GetForceRefresh();
            BatchGeneratePartialClassesInFolder(selectedPath, forceRefresh);
        }

        /// <summary>
        /// 批量生成 Partial 类
        /// </summary>
        /// <param name="folderPath">目标文件夹路径</param>
        /// <param name="forceRefresh">是否强制刷新已存在的文件</param>
        public static void BatchGeneratePartialClassesInFolder(string folderPath, bool forceRefresh = false)
        {
            if (!Directory.Exists(folderPath))
            {
                EditorUtility.DisplayDialog("Error", $"Folder not found: {folderPath}", "OK");
                return;
            }

            // 获取所有 .cs 文件，排除 _Partial 文件
            string[] allFiles = Directory.GetFiles(folderPath, "*.cs", SearchOption.AllDirectories);
            List<string> targetFiles = new List<string>();

            foreach (string file in allFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                if (!fileName.EndsWith("_Partial"))
                {
                    targetFiles.Add(file);
                }
            }

            if (targetFiles.Count == 0)
            {
                EditorUtility.DisplayDialog("Info", "No valid scripts found in the selected folder.", "OK");
                return;
            }

            int successCount = 0;
            int skipCount = 0;
            int errorCount = 0;
            List<string> errorMessages = new List<string>();

            foreach (string scriptPath in targetFiles)
            {
                // 转换为 Unity 相对路径
                string relativePath = scriptPath.Replace('\\', '/');
                if (relativePath.StartsWith(Application.dataPath))
                {
                    relativePath = "Assets" + relativePath.Substring(Application.dataPath.Length);
                }
                else if (!relativePath.StartsWith("Assets/"))
                {
                    // 如果不在 Assets 目录下，跳过
                    skipCount++;
                    continue;
                }

                try
                {
                    string generatedCode = GeneratePartialClassCodeStatic(relativePath);
                    
                    if (string.IsNullOrEmpty(generatedCode) || generatedCode.StartsWith("//"))
                    {
                        skipCount++;
                        continue;
                    }

                    string directory = Path.GetDirectoryName(relativePath);
                    string fileName = Path.GetFileNameWithoutExtension(relativePath);
                    string partialPath = Path.Combine(directory, $"{fileName}_Partial.cs").Replace('\\', '/');

                    // 如果已存在且不强制刷新，跳过
                    if (File.Exists(partialPath) && !forceRefresh)
                    {
                        skipCount++;
                        continue;
                    }

                    File.WriteAllText(partialPath, generatedCode);
                    
                    // 如果类继承自 BaseSaveDataSO，创建 ScriptableObject 资源文件
                    string scriptContent = File.ReadAllText(relativePath);
                    if (CheckInheritsFromBaseSaveDataSOStatic(scriptContent))
                    {
                        CreateScriptableObjectAssetStatic(relativePath, fileName);
                    }
                    
                    successCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    errorMessages.Add($"{Path.GetFileName(relativePath)}: {ex.Message}");
                }
            }

            AssetDatabase.Refresh();

            // 显示结果
            string resultMessage = $"Batch generation completed!\n\n" +
                $"Success: {successCount}\n" +
                $"Skipped: {skipCount}\n" +
                $"Errors: {errorCount}";

            if (errorMessages.Count > 0)
            {
                resultMessage += "\n\nErrors:\n" + string.Join("\n", errorMessages.Take(10));
                if (errorMessages.Count > 10)
                {
                    resultMessage += $"\n... and {errorMessages.Count - 10} more errors";
                }
            }

            EditorUtility.DisplayDialog("Batch Generation Result", resultMessage, "OK");
        }

        private static string GeneratePartialClassCodeStatic(string scriptPath)
        {
            if (!File.Exists(scriptPath))
            {
                return null;
            }

            string scriptContent = File.ReadAllText(scriptPath);

            string className = ExtractClassNameStatic(scriptContent);
            if (string.IsNullOrEmpty(className))
            {
                return null;
            }

            string namespaceName = ExtractNamespaceStatic(scriptContent);
            
            // 检查是否继承自 BaseSaveDataSO
            bool inheritsFromBaseSaveDataSO = CheckInheritsFromBaseSaveDataSOStatic(scriptContent);
            
            List<FieldInfo> fields = ExtractPrivateFieldsStatic(scriptContent);

            if (fields.Count == 0)
            {
                return null;
            }

            return BuildPartialClassCodeStatic(className, namespaceName, fields, inheritsFromBaseSaveDataSO, scriptPath);
        }
        
        /// <summary>
        /// 检查类是否继承自 BaseSaveDataSO（静态方法）
        /// </summary>
        private static bool CheckInheritsFromBaseSaveDataSOStatic(string scriptContent)
        {
            return scriptContent.Contains("BaseSaveDataSO") || 
                   Regex.IsMatch(scriptContent, @":\s*.*BaseSaveDataSO");
        }

        private static string ExtractClassNameStatic(string scriptContent)
        {
            Match match = Regex.Match(scriptContent, @"public\s+(?:partial\s+)?class\s+(\w+)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            return null;
        }

        private static string ExtractNamespaceStatic(string scriptContent)
        {
            Match match = Regex.Match(scriptContent, @"namespace\s+([\w.]+)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
            return null;
        }

        /// <summary>
        /// 提取私有字段（带 [SerializeField] 属性）- 静态方法版本
        /// 支持字段前有注释、复杂泛型类型和初始化值
        /// </summary>
        private static List<FieldInfo> ExtractPrivateFieldsStatic(string scriptContent)
        {
            List<FieldInfo> fields = new List<FieldInfo>();

            // 使用两步方法：
            // 1. 先找到所有 [SerializeField] private 的位置
            // 2. 然后解析每个位置的类型和字段名
            
            // 匹配模式：允许前面有注释，然后是 [SerializeField] private
            string basePattern = @"(?:^\s*//[^\r\n]*[\r\n]+)?\s*\[SerializeField\]\s+private\s+";
            MatchCollection baseMatches = Regex.Matches(scriptContent, basePattern, RegexOptions.Multiline);

            foreach (Match baseMatch in baseMatches)
            {
                int startIndex = baseMatch.Index + baseMatch.Length;
                string remaining = scriptContent.Substring(startIndex);
                
                // 解析类型（可能包含泛型和数组）
                string type = ExtractTypeStatic(remaining);
                if (string.IsNullOrEmpty(type))
                {
                    continue;
                }

                // 跳过类型后的空白字符
                int typeEndIndex = type.Length;
                while (typeEndIndex < remaining.Length && char.IsWhiteSpace(remaining[typeEndIndex]))
                {
                    typeEndIndex++;
                }

                // 提取字段名（标识符）
                string remainingAfterType = remaining.Substring(typeEndIndex);
                Match fieldNameMatch = Regex.Match(remainingAfterType, @"^(\w+)");
                if (!fieldNameMatch.Success)
                {
                    continue;
                }

                string fieldName = fieldNameMatch.Groups[1].Value;
                
                // 提取初始化值（如果有）
                string defaultValue = ExtractDefaultValueStatic(remainingAfterType, fieldName);

                // 排除 storageType 字段
                if (fieldName != "storageType" && type != "StorageType")
                {
                    fields.Add(new FieldInfo
                    {
                        Type = type.Trim(),
                        Name = fieldName.Trim(),
                        DefaultValue = defaultValue
                    });
                }
            }

            return fields;
        }

        /// <summary>
        /// 提取字段的默认值（初始化值）- 静态方法版本
        /// </summary>
        private static string ExtractDefaultValueStatic(string remainingText, string fieldName)
        {
            // 跳过字段名
            int index = fieldName.Length;
            
            // 跳过空白字符
            while (index < remainingText.Length && char.IsWhiteSpace(remainingText[index]))
            {
                index++;
            }
            
            // 检查是否有 = 号
            if (index >= remainingText.Length || remainingText[index] != '=')
            {
                return null; // 没有初始化值
            }
            
            index++; // 跳过 =
            
            // 跳过空白字符
            while (index < remainingText.Length && char.IsWhiteSpace(remainingText[index]))
            {
                index++;
            }
            
            // 提取初始化值，直到遇到分号
            StringBuilder valueBuilder = new StringBuilder();
            int depth = 0; // 用于处理嵌套的括号
            
            while (index < remainingText.Length)
            {
                char c = remainingText[index];
                
                if (c == ';' && depth == 0)
                {
                    break; // 遇到分号且不在括号内，结束
                }
                
                if (c == '(' || c == '[' || c == '{' || c == '<')
                {
                    depth++;
                }
                else if (c == ')' || c == ']' || c == '}' || c == '>')
                {
                    depth--;
                }
                
                valueBuilder.Append(c);
                index++;
            }
            
            string defaultValue = valueBuilder.ToString().Trim();
            return string.IsNullOrEmpty(defaultValue) ? null : defaultValue;
        }

        /// <summary>
        /// 从字符串开头提取类型（支持泛型和数组）- 静态方法版本
        /// </summary>
        private static string ExtractTypeStatic(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "";
            }

            int index = 0;
            StringBuilder typeBuilder = new StringBuilder();

            // 跳过前导空白
            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }

            // 提取基本类型名（可能包含命名空间，如 System.Collections.Generic.List）
            while (index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] == '.'))
            {
                typeBuilder.Append(text[index]);
                index++;
            }

            // 处理泛型参数 <>
            if (index < text.Length && text[index] == '<')
            {
                typeBuilder.Append('<');
                index++;

                int depth = 1;
                while (index < text.Length && depth > 0)
                {
                    if (text[index] == '<')
                    {
                        depth++;
                    }
                    else if (text[index] == '>')
                    {
                        depth--;
                    }
                    typeBuilder.Append(text[index]);
                    index++;
                }
            }

            // 处理数组声明 []
            while (index < text.Length && text[index] == '[')
            {
                typeBuilder.Append('[');
                index++;
                
                // 匹配数组内容（可能包含逗号，如 [,]）
                while (index < text.Length && text[index] != ']')
                {
                    typeBuilder.Append(text[index]);
                    index++;
                }
                
                if (index < text.Length)
                {
                    typeBuilder.Append(']');
                    index++;
                }
            }

            return typeBuilder.ToString();
        }

        private static string BuildPartialClassCodeStatic(string className, string namespaceName, List<FieldInfo> fields, bool inheritsFromBaseSaveDataSO, string scriptPath)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("using UnityEngine;");
            sb.AppendLine("using System.Collections.Generic;");
            if (inheritsFromBaseSaveDataSO)
            {
                sb.AppendLine("#if UNITY_EDITOR");
                sb.AppendLine("using UnityEditor;");
                sb.AppendLine("#endif");
            }
            sb.AppendLine();

            if (!string.IsNullOrEmpty(namespaceName))
            {
                sb.AppendLine($"namespace {namespaceName}");
                sb.AppendLine("{");
                sb.AppendLine($"    public partial class {className}");
                sb.AppendLine("    {");
            }
            else
            {
                sb.AppendLine($"public partial class {className}");
                sb.AppendLine("{");
            }

            string indent = string.IsNullOrEmpty(namespaceName) ? "    " : "        ";

            // 如果继承自 BaseSaveDataSO，添加 Init() 方法
            if (inheritsFromBaseSaveDataSO)
            {
                sb.AppendLine($"{indent}/// <summary>");
                sb.AppendLine($"{indent}/// 初始化数据（设置默认值）");
                sb.AppendLine($"{indent}/// </summary>");
                sb.AppendLine($"{indent}public override void Init()");
                sb.AppendLine($"{indent}{{");
                
                // 为每个字段生成初始化代码
                foreach (var field in fields)
                {
                    string initValue = GetDefaultValueForTypeStatic(field.Type, field.DefaultValue);
                    sb.AppendLine($"{indent}    {field.Name} = {initValue};");
                }
                
                sb.AppendLine($"{indent}}}");
                sb.AppendLine();
            }

            // 生成属性
            foreach (var field in fields)
            {
                string propertyName = ToPascalCaseStatic(field.Name);
                sb.AppendLine($"{indent}public {field.Type} {propertyName}");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    get => {field.Name};");
                sb.AppendLine($"{indent}    set => {field.Name} = value;");
                sb.AppendLine($"{indent}}}");
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(namespaceName))
            {
                sb.AppendLine("    }");
                sb.AppendLine("}");
            }
            else
            {
                sb.AppendLine("}");
            }

            return sb.ToString();
        }
        
        /// <summary>
        /// 获取资源文件路径（静态方法）
        /// </summary>
        private static string GetAssetPathStatic(string scriptPath, string className)
        {
            string directory = Path.GetDirectoryName(scriptPath);
            string assetPath = Path.Combine(directory, $"{className}.asset").Replace('\\', '/');
            return assetPath;
        }
        
        /// <summary>
        /// 获取 Resources 路径（静态方法）
        /// </summary>
        private static string GetResourcesPathStatic(string assetPath)
        {
            int resourcesIndex = assetPath.IndexOf("/Resources/");
            if (resourcesIndex >= 0)
            {
                string relativePath = assetPath.Substring(resourcesIndex + "/Resources/".Length);
                return Path.ChangeExtension(relativePath, null).Replace('\\', '/');
            }
            return Path.GetFileNameWithoutExtension(assetPath);
        }

        /// <summary>
        /// 获取类型的默认值字符串（用于 Init() 方法）- 静态方法版本
        /// </summary>
        private static string GetDefaultValueForTypeStatic(string type, string defaultValue)
        {
            // 如果有显式的初始化值，直接使用
            if (!string.IsNullOrEmpty(defaultValue))
            {
                return defaultValue;
            }

            // 根据类型返回默认值
            type = type.Trim();
            
            // 处理泛型类型（如 List<int>）
            if (type.Contains("<"))
            {
                int genericStart = type.IndexOf('<');
                string baseType = type.Substring(0, genericStart).Trim();
                
                if (baseType == "List" || baseType.Contains("List"))
                {
                    return "new " + type + "()";
                }
                
                if (baseType == "Dictionary" || baseType.Contains("Dictionary"))
                {
                    return "new " + type + "()";
                }
            }
            
            // 处理数组类型
            if (type.EndsWith("[]"))
            {
                return "new " + type.Replace("[]", "[0]");
            }
            
            // 基本类型默认值
            switch (type)
            {
                case "int":
                case "Int32":
                    return "0";
                case "long":
                case "Int64":
                    return "0L";
                case "float":
                case "Single":
                    return "0f";
                case "double":
                case "Double":
                    return "0d";
                case "bool":
                case "Boolean":
                    return "false";
                case "string":
                case "String":
                    return "\"\"";
                case "Vector2":
                    return "Vector2.zero";
                case "Vector3":
                    return "Vector3.zero";
                case "Vector4":
                    return "Vector4.zero";
                case "Color":
                    return "Color.white";
                case "Quaternion":
                    return "Quaternion.identity";
                default:
                    // 对于其他类型，尝试使用 new 构造
                    if (type.Contains("List") || type.Contains("Dictionary") || type.Contains("HashSet"))
                    {
                        return "new " + type + "()";
                    }
                    return "default(" + type + ")";
            }
        }

        private static string ToPascalCaseStatic(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            return char.ToUpper(input[0]) + input.Substring(1);
        }
        
        /// <summary>
        /// 创建 ScriptableObject 资源文件（静态方法）
        /// </summary>
        private static void CreateScriptableObjectAssetStatic(string scriptPath, string className)
        {
            try
            {
                // 获取脚本的类型
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                if (script == null)
                {
                    return;
                }
                
                System.Type scriptType = script.GetClass();
                if (scriptType == null)
                {
                    return;
                }
                
                // 检查是否继承自 BaseSaveDataSO
                System.Type baseType = typeof(BaseSaveDataSO);
                if (!baseType.IsAssignableFrom(scriptType))
                {
                    return; // 不是 BaseSaveDataSO 的子类，不创建资源
                }
                
                // 资源文件路径
                string directory = Path.GetDirectoryName(scriptPath);
                string assetPath = Path.Combine(directory, $"{className}.asset").Replace('\\', '/');
                
                // 如果资源已存在，不覆盖
                if (File.Exists(assetPath))
                {
                    return;
                }
                
                // 创建 ScriptableObject 实例
                ScriptableObject instance = ScriptableObject.CreateInstance(scriptType);
                if (instance == null)
                {
                    return;
                }
                
                // 保存资源
                AssetDatabase.CreateAsset(instance, assetPath);
            }
            catch (System.Exception)
            {
                // 静默失败，不影响批量生成流程
            }
        }

        #endregion

        [Serializable]
        private class FieldInfo
        {
            public string Type;
            public string Name;
            public string DefaultValue; // 字段的初始化值（如果有）
        }
    }
}

