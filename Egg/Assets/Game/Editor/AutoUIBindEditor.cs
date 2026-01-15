#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Game.Editor
{
    /// <summary>
    /// IAutoUIBind 编辑器拓展
    /// 用于自动扫描和绑定 UI 组件
    /// 支持任何继承自 MonoBehaviour 并实现 IAutoUIBind 接口的脚本
    /// </summary>
    [CustomEditor(typeof(MonoBehaviour), true)]
    [CanEditMultipleObjects]
    public class AutoUIBindEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            // 检查当前 MonoBehaviour 是否实现了 IAutoUIBind 接口
            MonoBehaviour monoBehaviour = (MonoBehaviour)target;
            if (!IsAutoUIBind(monoBehaviour))
            {
                return;
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("UI Component Generator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("扫描子对象并自动生成 partial 脚本，包含 [SerializeField] private 字段声明", MessageType.Info);

            if (GUILayout.Button("Generate UI Components", GUILayout.Height(30)))
            {
                GenerateUIComponents();
            }
        }

        private bool IsAutoUIBind(MonoBehaviour mb)
        {
            if (mb == null)
                return false;

            System.Type type = mb.GetType();
            System.Type iAutoUIBindType = typeof(IAutoUIBind);
            
            return iAutoUIBindType.IsAssignableFrom(type);
        }

        private void GenerateUIComponents()
        {
            MonoBehaviour targetMono = (MonoBehaviour)target;
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
                        if (!EditorUtility.DisplayDialog("Warning", 
                            $"原脚本 '{targetType.Name}' 不是 partial 类。\n\n生成的代码将无法正常工作。\n\n是否继续？\n\n（建议：将原脚本的 class 声明改为 'public partial class {targetType.Name}'）", 
                            "继续", "取消"))
                        {
                            return;
                        }
                    }
                }
            }

            // 扫描子对象
            List<UIComponentInfo> components = ScanChildObjects(targetGameObject);

            if (components.Count == 0)
            {
                EditorUtility.DisplayDialog("No Components Found", "未找到符合条件的子对象。\n\n需要：\n- 名称包含 _btn 并绑定 CustomButton\n- 名称包含 _txt 并绑定 TextMeshProUGUI\n- 名称包含 _img 并绑定 Image\n- 名称包含 _toggle 并绑定 Toggle", "OK");
                return;
            }

            // 生成代码
            string generatedCode = GeneratePartialClassCode(targetMono, components);

            // 保存文件
            string scriptPath = GetScriptPath(targetMono);
            if (string.IsNullOrEmpty(scriptPath))
            {
                EditorUtility.DisplayDialog("Error", "无法找到脚本文件路径", "OK");
                return;
            }

            string partialScriptPath = GetPartialScriptPath(scriptPath);
            SavePartialScript(partialScriptPath, generatedCode);

            // 刷新资源并等待编译
            AssetDatabase.Refresh();
            AssetDatabase.ImportAsset(partialScriptPath);
            
            // 等待编译完成后绑定
            EditorApplication.delayCall += () =>
            {
                // 再次等待一帧确保编译完成
                EditorApplication.delayCall += () =>
                {
                    BindComponentsToInspector(targetMono, components);
                };
            };

            EditorUtility.DisplayDialog("Success", $"已生成 {components.Count} 个组件声明\n\n文件路径：\n{partialScriptPath}\n\n正在自动绑定对象到 Inspector...", "OK");
        }

        private void BindComponentsToInspector(MonoBehaviour targetMono, List<UIComponentInfo> components)
        {
            if (targetMono == null)
            {
                Debug.LogWarning("Target MonoBehaviour is null");
                return;
            }

            GameObject targetGameObject = targetMono.gameObject;
            if (targetGameObject == null)
            {
                Debug.LogWarning("Target GameObject is null");
                return;
            }

            SerializedObject serializedObject = new SerializedObject(targetMono);
            
            int boundCount = 0;
            List<string> notFoundFields = new List<string>();
            
            foreach (var componentInfo in components)
            {
                // 查找对应的 GameObject
                Transform foundTransform = FindChildByName(targetGameObject.transform, componentInfo.Name);
                if (foundTransform == null)
                {
                    Debug.LogWarning($"[AutoUIBindEditor] 未找到对象: {componentInfo.Name}");
                    continue;
                }
                
                GameObject foundObject = foundTransform.gameObject;
                UnityEngine.Object component = null;
                
                // 根据类型获取组件
                switch (componentInfo.Type)
                {
                    case "CustomButton":
                        component = foundObject.GetComponent<CustomButton>();
                        break;
                    case "TextMeshProUGUI":
                        component = foundObject.GetComponent<TextMeshProUGUI>();
                        break;
                    case "Image":
                        component = foundObject.GetComponent<Image>();
                        break;
                    case "Toggle":
                        component = foundObject.GetComponent<Toggle>();
                        break;
                }
                
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
                }
            }
            
            // 应用修改
            serializedObject.ApplyModifiedProperties();
            
            // 标记对象为脏（需要保存）
            EditorUtility.SetDirty(targetMono);
            
            if (notFoundFields.Count > 0)
            {
                Debug.LogWarning($"[AutoUIBindEditor] 未找到以下字段（可能脚本尚未编译完成）: {string.Join(", ", notFoundFields)}");
            }
            
            Debug.Log($"[AutoUIBindEditor] 已自动绑定 {boundCount}/{components.Count} 个组件到 Inspector");
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

        private List<UIComponentInfo> ScanChildObjects(GameObject parent)
        {
            List<UIComponentInfo> components = new List<UIComponentInfo>();
            Transform[] children = parent.GetComponentsInChildren<Transform>(true);

            foreach (Transform child in children)
            {
                if (child == parent.transform)
                    continue;

                string objectName = child.name;
                GameObject obj = child.gameObject;

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
            }

            return components;
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

            // 生成按钮字段
            if (buttons.Count > 0)
            {
                code.AppendLine("        #region Buttons");
                foreach (var btn in buttons)
                {
                    code.AppendLine($"        [SerializeField] private CustomButton {btn.FieldName};");
                }
                code.AppendLine("        #endregion");
                code.AppendLine();
            }

            // 生成文本字段
            if (texts.Count > 0)
            {
                code.AppendLine("        #region Texts");
                foreach (var txt in texts)
                {
                    code.AppendLine($"        [SerializeField] private TextMeshProUGUI {txt.FieldName};");
                }
                code.AppendLine("        #endregion");
                code.AppendLine();
            }

            // 生成图片字段
            if (images.Count > 0)
            {
                code.AppendLine("        #region Images");
                foreach (var img in images)
                {
                    code.AppendLine($"        [SerializeField] private Image {img.FieldName};");
                }
                code.AppendLine("        #endregion");
                code.AppendLine();
            }

            // 生成 Toggle 字段
            if (toggles.Count > 0)
            {
                code.AppendLine("        #region Toggles");
                foreach (var toggle in toggles)
                {
                    code.AppendLine($"        [SerializeField] private Toggle {toggle.FieldName};");
                }
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

        private class UIComponentInfo
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public string FieldName { get; set; }
        }
    }
}
#endif
