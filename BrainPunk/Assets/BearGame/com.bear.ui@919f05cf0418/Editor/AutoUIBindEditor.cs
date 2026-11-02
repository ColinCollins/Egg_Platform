#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Bear.UI;

namespace Game.Editor
{
    /// <summary>
    /// BaseAutoUIBind 编辑器拓展
    /// 用于自动扫描和绑定 UI 组件
    /// 支持任何继承自 BaseAutoUIBind 的脚本
    /// 
    /// 注意：由于 Unity 的 CustomEditor 优先级机制，如果有更具体的编辑器（如 BaseUIViewEditor），
    /// 那些编辑器会优先匹配。
    /// </summary>
    [CustomEditor(typeof(BaseAutoUIBind), true)]
    [CanEditMultipleObjects]
    public class AutoUIBindEditor : UnityEditor.Editor
    {
        // 缓存检查结果，避免重复检查
        private static Dictionary<System.Type, bool> _typeCache = new Dictionary<System.Type, bool>();

        public override void OnInspectorGUI()
        {
            BaseAutoUIBind monoBehaviour = (BaseAutoUIBind)target;
            System.Type targetType = monoBehaviour.GetType();
            
            // 检查是否有更具体的编辑器（如 BaseUIViewEditor）
            // 如果有，让更具体的编辑器处理
            if (targetType != typeof(MonoBehaviour) && HasMoreSpecificEditor(targetType))
            {
                // 使用默认 Inspector，让更具体的编辑器处理
                DrawDefaultInspector();
                return;
            }

            DrawDefaultInspector();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("UI Component Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "扫描子对象并自动生成 partial 脚本，包含 [SerializeField] private 字段声明。以 - 前缀命名的节点会跳过不分析。\n" +
                "同名前缀合并为 List：去掉类型标记（如 _toggle）后，若末尾是 _数字 则识别为序号层。基础层（如 ..._01）与副本层（如 ..._01_2）会归并到同一前缀。",
                MessageType.Info);

            // 检查是否已生成代码文件
            string scriptPath = GetScriptPath(monoBehaviour);
            bool hasGeneratedFile = false;
            if (!string.IsNullOrEmpty(scriptPath))
            {
                string partialScriptPath = GetPartialScriptPath(scriptPath);
                hasGeneratedFile = File.Exists(partialScriptPath);
            }

            if (hasGeneratedFile)
            {
                EditorGUILayout.HelpBox("代码文件已生成，点击下方按钮绑定组件到 Inspector", MessageType.Info);
                if (GUILayout.Button("Bind Components to Inspector", GUILayout.Height(30)))
                {
                    BindComponentsToInspector(monoBehaviour);
                }

                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox("如果层级有变化（新增/改名/删除），可点击重新扫描并覆盖生成 partial 脚本", MessageType.None);
                if (GUILayout.Button("Rescan & Regenerate Partial Script", GUILayout.Height(26)))
                {
                    GenerateUIComponents();
                }
            }
            else
            {
                if (GUILayout.Button("Generate UI Components", GUILayout.Height(30)))
                {
                    GenerateUIComponents();
                }
            }
        }

        /// <summary>
        /// 检查是否有更具体的编辑器（如 BaseUIViewEditor）
        /// </summary>
        private bool HasMoreSpecificEditor(System.Type type)
        {
            // 检查是否是 BaseUIView 的子类
            System.Type baseUIViewType = typeof(BaseUIView);
            if (baseUIViewType != null && baseUIViewType.IsAssignableFrom(type) && type != baseUIViewType)
            {
                return true;
            }

            return false;
        }

        private void GenerateUIComponents()
        {
            BaseAutoUIBind targetMono = (BaseAutoUIBind)target;
            GameObject targetGameObject = targetMono.gameObject;

            // 检查原脚本是否是 partial 类
            System.Type targetType = targetMono.GetType();
            MonoScript script = MonoScript.FromMonoBehaviour(targetMono);
            if (script != null)
            {
                string scriptContent = script.text;
                if (!scriptContent.Contains($"partial class {targetType.Name}") &&
                    !scriptContent.Contains($"class {targetType.Name}"))
                {
                    // 尝试检查是否有 partial 关键字
                    if (!scriptContent.Contains("partial"))
                    {
                        Debug.LogWarning(
                            $"[AutoUIBindEditor] Script '{targetType.Name}' is not partial. " +
                            "Generated code may not work.");
                    }
                }
            }

            // 扫描子对象
            List<UIComponentInfo> components = ScanChildObjects(targetGameObject);

            if (components.Count == 0)
            {
                Debug.LogWarning("[AutoUIBindEditor] No matching child components found.");
                return;
            }

            ApplyListGrouping(components);

            // 生成代码
            string generatedCode = GeneratePartialClassCode(targetMono, components);

            // 保存文件
            string scriptPath = GetScriptPath(targetMono);
            if (string.IsNullOrEmpty(scriptPath))
            {
                Debug.LogError("[AutoUIBindEditor] Script path not found.");
                return;
            }

            string partialScriptPath = GetPartialScriptPath(scriptPath);
            SavePartialScript(partialScriptPath, generatedCode);

            // 刷新资源
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(partialScriptPath);

            Debug.Log(
                $"[AutoUIBindEditor] Generated {components.Count} components. " +
                $"Path: {partialScriptPath}. " +
                $"Please wait for compilation to complete, then click 'Bind Components to Inspector' button.");
        }

        private void BindComponentsToInspector(MonoBehaviour targetMono)
        {
            if (targetMono == null)
            {
                Debug.LogWarning("[AutoUIBindEditor] Target MonoBehaviour is null");
                return;
            }

            GameObject targetGameObject = targetMono.gameObject;
            if (targetGameObject == null)
            {
                Debug.LogWarning("[AutoUIBindEditor] Target GameObject is null");
                return;
            }

            // 重新扫描子对象获取组件信息
            List<UIComponentInfo> components = ScanChildObjects(targetGameObject);
            
            if (components.Count == 0)
            {
                Debug.LogWarning("[AutoUIBindEditor] No matching child components found.");
                return;
            }

            ApplyListGrouping(components);

            SerializedObject serializedObject = new SerializedObject(targetMono);
            serializedObject.Update(); // 确保更新到最新状态

            int boundCount = 0;
            List<string> notFoundFields = new List<string>();

            Debug.Log($"[AutoUIBindEditor] Starting to bind {components.Count} components to {targetMono.GetType().Name}");

            var listGroups = components
                .Where(c => c.IsListElement && !string.IsNullOrEmpty(c.ListFieldName))
                .GroupBy(c => new { c.ListFieldName, c.Type })
                .ToList();

            foreach (var grp in listGroups)
            {
                var ordered = grp.OrderBy(c => c.ListSortKey).ThenBy(c => c.ListSortString, StringComparer.Ordinal).ToList();
                var objects = new List<UnityEngine.Object>();
                var ok = true;
                foreach (var componentInfo in ordered)
                {
                    Transform foundTransform = FindChildByName(targetGameObject.transform, componentInfo.Name);
                    if (foundTransform == null)
                    {
                        Debug.LogWarning($"[AutoUIBindEditor] 未找到对象: {componentInfo.Name}");
                        ok = false;
                        break;
                    }

                    GameObject foundObject = foundTransform.gameObject;
                    UnityEngine.Object component = ResolveComponentReference(foundObject, componentInfo.Type);
                    if (component == null)
                    {
                        Debug.LogWarning($"[AutoUIBindEditor] 对象 {componentInfo.Name} 上未找到 {componentInfo.Type} 组件");
                        ok = false;
                        break;
                    }

                    objects.Add(component);
                }

                if (!ok || objects.Count == 0)
                    continue;

                SerializedProperty property = serializedObject.FindProperty(grp.Key.ListFieldName);
                if (property != null && property.isArray)
                {
                    property.arraySize = objects.Count;
                    for (var i = 0; i < objects.Count; i++)
                    {
                        property.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
                    }

                    boundCount += objects.Count;
                    Debug.Log($"[AutoUIBindEditor] 已绑定 List: {grp.Key.ListFieldName} ({objects.Count}项)");
                }
                else
                {
                    notFoundFields.Add(grp.Key.ListFieldName);
                    Debug.LogWarning($"[AutoUIBindEditor] 未找到 List 字段: {grp.Key.ListFieldName}");
                }
            }

            foreach (var componentInfo in components.Where(c => !c.IsListElement))
            {
                // 查找对应的 GameObject
                Transform foundTransform = FindChildByName(targetGameObject.transform, componentInfo.Name);
                if (foundTransform == null)
                {
                    Debug.LogWarning($"[AutoUIBindEditor] 未找到对象: {componentInfo.Name}");
                    continue;
                }

                GameObject foundObject = foundTransform.gameObject;
                UnityEngine.Object component = ResolveComponentReference(foundObject, componentInfo.Type);

                if (component == null)
                {
                    Debug.LogWarning($"[AutoUIBindEditor] 对象 {componentInfo.Name} 上未找到 {componentInfo.Type} 组件");
                    continue;
                }

                // 设置 SerializedProperty
                SerializedProperty property = serializedObject.FindProperty(componentInfo.FieldName);
                if (property != null)
                {
                    property.objectReferenceValue = component;
                    boundCount++;
                    Debug.Log($"[AutoUIBindEditor] 已绑定: {componentInfo.FieldName} = {componentInfo.Name}");
                }
                else
                {
                    notFoundFields.Add(componentInfo.FieldName);
                    Debug.LogWarning($"[AutoUIBindEditor] 未找到字段: {componentInfo.FieldName} (类型: {targetMono.GetType().Name})");
                }
            }

            // 应用修改
            bool hasChanges = serializedObject.ApplyModifiedProperties();

            // 标记对象为脏（需要保存）
            EditorUtility.SetDirty(targetMono);
            
            // 强制刷新 Inspector
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

            if (notFoundFields.Count > 0)
            {
                Debug.LogWarning($"[AutoUIBindEditor] 未找到以下字段（可能脚本尚未编译完成或字段名不匹配）: {string.Join(", ", notFoundFields)}");
            }

            Debug.Log($"[AutoUIBindEditor] 已自动绑定 {boundCount}/{components.Count} 个组件到 Inspector (hasChanges: {hasChanges})");
        }

        private Transform FindChildByName(Transform parent, string name)
        {
            // 先检查直接子对象
            foreach (Transform child in parent)
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            // 递归查找所有子对象
            Transform[] allChildren = parent.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in allChildren)
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        private static UnityEngine.Object ResolveComponentReference(GameObject foundObject, string componentType)
        {
            switch (componentType)
            {
                case "CustomButton":
                    return foundObject.GetComponent<CustomButton>();
                case "TextMeshProUGUI":
                    return foundObject.GetComponent<TextMeshProUGUI>();
                case "Image":
                    return foundObject.GetComponent<Image>();
                case "Toggle":
                    return foundObject.GetComponent<Toggle>();
                case "GameObject":
                    return foundObject;
                default:
                    return null;
            }
        }

        private static string GetTypeMarkerForComponentType(string componentType)
        {
            switch (componentType)
            {
                case "CustomButton":
                    return "_btn";
                case "TextMeshProUGUI":
                    return "_txt";
                case "Image":
                    return "_img";
                case "Toggle":
                    return "_toggle";
                case "GameObject":
                    return "_state";
                default:
                    return null;
            }
        }

        private static string GetListFieldSuffix(string componentType)
        {
            switch (componentType)
            {
                case "CustomButton":
                    return "Btns";
                case "TextMeshProUGUI":
                    return "Txts";
                case "Image":
                    return "Imgs";
                case "Toggle":
                    return "Toggles";
                case "GameObject":
                    return "States";
                default:
                    return "Items";
            }
        }

        private static bool TryGetListablePrefix(
            string objectName,
            string componentType,
            out string prefixRaw,
            out string indexStr)
        {
            prefixRaw = null;
            indexStr = null;
            string marker = GetTypeMarkerForComponentType(componentType);
            if (string.IsNullOrEmpty(marker) || !objectName.Contains(marker))
                return false;

            // 类型标记可能在中间（如 UI_pingfen_toggle_xing_01_2），先移除再做序号分析
            string normalizedName = objectName.Replace(marker, "");
            if (string.IsNullOrEmpty(normalizedName))
                return false;

            string beforeMarker = normalizedName;
            if (string.IsNullOrEmpty(beforeMarker))
                return false;

            // 尝试去掉末尾的 _数字 段（只去一层）
            int lu = beforeMarker.LastIndexOf('_');
            if (lu <= 0)
                return false; // 没有下划线或下划线在开头，无法分组

            string right = beforeMarker.Substring(lu + 1);
            if (string.IsNullOrEmpty(right) || !right.All(char.IsDigit))
                return false; // 右侧不是数字，不是序号

            string remaining = beforeMarker.Substring(0, lu);

            // 关键逻辑：检查 remaining 是否还包含 "_数字" 模式（多层序号）
            bool hasMoreNumberSegment = ContainsUnderscoreNumberSuffix(remaining);
            if (hasMoreNumberSegment)
            {
                // 多层（如 xing_01_2 → 前缀 xing_01）
                prefixRaw = remaining;
                indexStr = right;
            }
            else
            {
                // 第一层（如 xing_01 → 前缀 xing_01，保留完整）
                prefixRaw = beforeMarker;
                indexStr = right;
            }
            return !string.IsNullOrEmpty(prefixRaw);
        }

        private static bool ContainsUnderscoreNumberSuffix(string s)
        {
            if (string.IsNullOrEmpty(s))
                return false;
            int idx = s.LastIndexOf('_');
            if (idx <= 0)
                return false;
            string after = s.Substring(idx + 1);
            return !string.IsNullOrEmpty(after) && after.All(char.IsDigit);
        }

        private static string PrefixSegmentsToPascal(string prefixRaw)
        {
            string[] parts = prefixRaw.Split('_');
            var sb = new StringBuilder();
            foreach (string part in parts)
            {
                if (part.Length == 0)
                    continue;
                sb.Append(char.ToUpper(part[0]));
                if (part.Length > 1)
                    sb.Append(part.Substring(1));
            }

            return sb.ToString();
        }

        private void ApplyListGrouping(List<UIComponentInfo> components)
        {
            var keyed = new List<(UIComponentInfo info, string prefixKey, string prefixRaw, string indexStr, int indexNum)>();

            foreach (var c in components)
            {
                if (!TryGetListablePrefix(c.Name, c.Type, out string prefixRaw, out string indexStr))
                    continue;

                int indexNum = 0;
                int.TryParse(indexStr, out indexNum);
                string prefixKey = prefixRaw.ToLowerInvariant();
                keyed.Add((c, prefixKey, prefixRaw, indexStr, indexNum));
            }

            var counts = keyed
                .GroupBy(x => (x.info.Type, x.prefixKey))
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var t in keyed)
            {
                var key = (t.info.Type, t.prefixKey);
                if (counts[key] < 2)
                {
                    t.info.IsListElement = false;
                    t.info.ListFieldName = null;
                    continue;
                }

                t.info.IsListElement = true;
                t.info.ListSortString = t.indexStr;
                t.info.ListSortKey = t.indexNum;
            }

            foreach (var grp in keyed.Where(x => x.info.IsListElement).GroupBy(x => (x.info.Type, x.prefixKey)))
            {
                string canonicalPrefix = grp
                    .OrderBy(x => x.indexNum)
                    .ThenBy(x => x.indexStr, StringComparer.Ordinal)
                    .First()
                    .prefixRaw;
                string listName = PrefixSegmentsToPascal(canonicalPrefix) + GetListFieldSuffix(grp.First().info.Type);

                foreach (var x in grp)
                    x.info.ListFieldName = listName;
            }
        }

        private List<UIComponentInfo> ScanChildObjects(GameObject parent)
        {
            List<UIComponentInfo> components = new List<UIComponentInfo>();
            ScanChildObjectsRecursive(parent.transform, components);
            return components;
        }

        private void ScanChildObjectsRecursive(Transform parent, List<UIComponentInfo> components)
        {
            // 遍历直接子对象
            foreach (Transform child in parent)
            {
                GameObject obj = child.gameObject;

                // 检查是否包含 BaseAutoUIBind 或 BaseUIView 组件
                // 如果包含，跳过该对象及其所有子对象
                if (obj.GetComponent<BaseAutoUIBind>() != null || obj.GetComponent<BaseUIView>() != null)
                {
                    continue; // 跳过该对象及其所有子对象
                }

                // 以 - 前缀命名的节点跳过不分析（及其子对象）
                if (child.name.StartsWith("-"))
                {
                    continue;
                }

                string objectName = child.name;

                // 检查 _btn 后缀 + CustomButton
                if (objectName.Contains("_btn"))
                {
                    CustomButton btn = obj.GetComponent<CustomButton>();
                    if (btn != null)
                    {
                        components.Add(new UIComponentInfo
                        {
                            Name = objectName,
                            Type = "CustomButton",
                            FieldName = GetFieldName(objectName)
                        });
                    }
                }

                // 检查 _txt 后缀 + TextMeshProUGUI
                if (objectName.Contains("_txt"))
                {
                    TextMeshProUGUI txt = obj.GetComponent<TextMeshProUGUI>();
                    if (txt != null)
                    {
                        components.Add(new UIComponentInfo
                        {
                            Name = objectName,
                            Type = "TextMeshProUGUI",
                            FieldName = GetFieldName(objectName)
                        });
                    }
                }

                // 检查 _img 后缀 + Image
                if (objectName.Contains("_img"))
                {
                    Image img = obj.GetComponent<Image>();
                    if (img != null)
                    {
                        components.Add(new UIComponentInfo
                        {
                            Name = objectName,
                            Type = "Image",
                            FieldName = GetFieldName(objectName)
                        });
                    }
                }

                // 检查 _toggle 后缀 + Toggle
                if (objectName.Contains("_toggle"))
                {
                    Toggle toggle = obj.GetComponent<Toggle>();
                    if (toggle != null)
                    {
                        components.Add(new UIComponentInfo
                        {
                            Name = objectName,
                            Type = "Toggle",
                            FieldName = GetFieldName(objectName)
                        });
                    }
                }

                // 检查 _state 后缀 + GameObject
                if (objectName.Contains("_state"))
                {
                    components.Add(new UIComponentInfo
                    {
                        Name = objectName,
                        Type = "GameObject",
                        FieldName = GetFieldName(objectName)
                    });
                }

                // 递归遍历子对象
                ScanChildObjectsRecursive(child, components);
            }
        }

        private string GetFieldName(string objectName)
        {
            // 转换为 PascalCase，保留后缀
            string fieldName = objectName;
            string suffix = "";

            // 提取后缀并转换为大写形式
            if (fieldName.Contains("_btn"))
            {
                suffix = "Btn";
                fieldName = fieldName.Replace("_btn", "");
            }
            else if (fieldName.Contains("_txt"))
            {
                suffix = "Txt";
                fieldName = fieldName.Replace("_txt", "");
            }
            else if (fieldName.Contains("_img"))
            {
                suffix = "Img";
                fieldName = fieldName.Replace("_img", "");
            }
            else if (fieldName.Contains("_toggle"))
            {
                suffix = "Toggle";
                fieldName = fieldName.Replace("_toggle", "");
            }
            else if (fieldName.Contains("_state"))
            {
                suffix = "State";
                fieldName = fieldName.Replace("_state", "");
            }

            // 移除下划线并转换为驼峰命名
            string[] parts = fieldName.Split('_');
            StringBuilder sb = new StringBuilder();
            foreach (string part in parts)
            {
                if (part.Length > 0)
                {
                    sb.Append(char.ToUpper(part[0]));
                    if (part.Length > 1)
                    {
                        sb.Append(part.Substring(1));
                    }
                }
            }

            // 添加后缀
            if (!string.IsNullOrEmpty(suffix))
            {
                sb.Append(suffix);
            }

            return sb.ToString();
        }

        private string GeneratePartialClassCode(MonoBehaviour targetMono, List<UIComponentInfo> components)
        {
            System.Type targetType = targetMono.GetType();
            string className = targetType.Name;
            string namespaceName = targetType.Namespace ?? "";

            StringBuilder code = new StringBuilder();

            // 添加文件头注释
            code.AppendLine("// This file is auto-generated by AutoUIBindEditor");
            code.AppendLine("// Do not modify this file manually");
            code.AppendLine();

            // 添加 using 语句
            code.AppendLine("using System.Collections.Generic;");
            code.AppendLine("using UnityEngine;");
            code.AppendLine("using UnityEngine.UI;");
            code.AppendLine("using TMPro;");
            code.AppendLine();

            // 添加命名空间（如果有）
            if (!string.IsNullOrEmpty(namespaceName))
            {
                code.AppendLine($"namespace {namespaceName}");
                code.AppendLine("{");
            }

            // 添加 partial 类声明
            code.AppendLine($"    public partial class {className}");
            code.AppendLine("    {");

            // 按类型分组
            var buttons = components.Where(c => c.Type == "CustomButton").ToList();
            var texts = components.Where(c => c.Type == "TextMeshProUGUI").ToList();
            var images = components.Where(c => c.Type == "Image").ToList();
            var toggles = components.Where(c => c.Type == "Toggle").ToList();
            var gameObjects = components.Where(c => c.Type == "GameObject").ToList();

            // 生成按钮字段
            if (buttons.Count > 0)
            {
                code.AppendLine("        #region Buttons");
                AppendListAndSingleFields(
                    code,
                    buttons,
                    c => c.IsListElement,
                    n => $"        [SerializeField] private List<CustomButton> {n};",
                    b => $"        [SerializeField] private CustomButton {b.FieldName};");
                code.AppendLine("        #endregion");
                code.AppendLine();
            }

            // 生成文本字段
            if (texts.Count > 0)
            {
                code.AppendLine("        #region Texts");
                AppendListAndSingleFields(
                    code,
                    texts,
                    c => c.IsListElement,
                    n => $"        [SerializeField] private List<TextMeshProUGUI> {n};",
                    t => $"        [SerializeField] private TextMeshProUGUI {t.FieldName};");
                code.AppendLine("        #endregion");
                code.AppendLine();
            }

            // 生成图片字段
            if (images.Count > 0)
            {
                code.AppendLine("        #region Images");
                AppendListAndSingleFields(
                    code,
                    images,
                    c => c.IsListElement,
                    n => $"        [SerializeField] private List<Image> {n};",
                    i => $"        [SerializeField] private Image {i.FieldName};");
                code.AppendLine("        #endregion");
                code.AppendLine();
            }

            // 生成 Toggle 字段
            if (toggles.Count > 0)
            {
                code.AppendLine("        #region Toggles");
                AppendListAndSingleFields(
                    code,
                    toggles,
                    c => c.IsListElement,
                    n => $"        [SerializeField] private List<Toggle> {n};",
                    t => $"        [SerializeField] private Toggle {t.FieldName};");
                code.AppendLine("        #endregion");
                code.AppendLine();
            }

            // 生成 GameObject 字段
            if (gameObjects.Count > 0)
            {
                code.AppendLine("        #region GameObjects");
                AppendListAndSingleFields(
                    code,
                    gameObjects,
                    c => c.IsListElement,
                    n => $"        [SerializeField] private List<GameObject> {n};",
                    g => $"        [SerializeField] private GameObject {g.FieldName};");
                code.AppendLine("        #endregion");
            }

            code.AppendLine("    }");

            // 关闭命名空间
            if (!string.IsNullOrEmpty(namespaceName))
            {
                code.AppendLine("}");
            }

            return code.ToString();
        }

        private string GetScriptPath(MonoBehaviour targetMono)
        {
            // 获取脚本的 MonoScript
            MonoScript script = MonoScript.FromMonoBehaviour(targetMono);
            if (script == null)
            {
                return null;
            }

            return AssetDatabase.GetAssetPath(script);
        }

        private string GetPartialScriptPath(string originalScriptPath)
        {
            // 统一路径格式
            originalScriptPath = originalScriptPath.Replace('\\', '/');

            string directory = Path.GetDirectoryName(originalScriptPath).Replace('\\', '/');
            string fileName = Path.GetFileNameWithoutExtension(originalScriptPath);
            string extension = Path.GetExtension(originalScriptPath);

            // 生成 partial 脚本路径（添加 .Generated 后缀）
            string partialFileName = $"{fileName}.Generated{extension}";
            return Path.Combine(directory, partialFileName).Replace('\\', '/');
        }

        private void SavePartialScript(string filePath, string content)
        {
            // 统一路径格式
            filePath = filePath.Replace('\\', '/');

            // 确保目录存在
            string directory = Path.GetDirectoryName(filePath).Replace('\\', '/');
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 写入文件
            File.WriteAllText(filePath, content, Encoding.UTF8);
            Debug.Log($"Generated partial script: {filePath}");
        }

        private static void AppendListAndSingleFields(
            StringBuilder code,
            List<UIComponentInfo> items,
            Func<UIComponentInfo, bool> isListElement,
            Func<string, string> formatListLine,
            Func<UIComponentInfo, string> formatSingleLine)
        {
            foreach (var grp in items.Where(isListElement).GroupBy(c => c.ListFieldName).OrderBy(g => g.Key))
                code.AppendLine(formatListLine(grp.Key));

            foreach (var item in items.Where(c => !isListElement(c)).OrderBy(c => c.FieldName))
                code.AppendLine(formatSingleLine(item));
        }

        private class UIComponentInfo
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public string FieldName { get; set; }

            public bool IsListElement { get; set; }
            public string ListFieldName { get; set; }
            public int ListSortKey { get; set; }
            public string ListSortString { get; set; }
        }
    }
}
#endif
