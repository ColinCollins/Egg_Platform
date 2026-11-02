using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace Bear.Editor
{
    /// <summary>
    /// 类型收集器工具：扫描指定文件夹下的所有 C# 类，生成一个静态类型注册表。
    /// </summary>
    public class ClassCollectorEditorWindow : EditorWindow
    {
        private const string PREFS_KEY_SOURCE_FOLDER = "ClassCollector_SourceFolder";
        private const string PREFS_KEY_INCLUDE_ABSTRACT = "ClassCollector_IncludeAbstract";
        private const string PREFS_KEY_INCLUDE_STATIC = "ClassCollector_IncludeStatic";
        private const string PREFS_KEY_INCLUDE_INTERFACE = "ClassCollector_IncludeInterface";
        private const string PREFS_KEY_PREFAB_LIST = "ClassCollector_PrefabList";

        [SerializeField] private string sourceFolder = "Assets/Game/Scripts";
        [SerializeField] private bool includeAbstract = false;
        [SerializeField] private bool includeStatic = false;
        [SerializeField] private bool includeInterface = false;
        [SerializeField] private bool generateLinkXML = true;
        [SerializeField] private string linkXMLOutputPath = "Assets/link.xml";
        [SerializeField] private List<GameObject> prefabList = new List<GameObject>();

        private Vector2 scrollPos;
        private Vector2 prefabScrollPos;
        private List<CollectedTypeInfo> collectedTypes = new List<CollectedTypeInfo>();
        private bool hasScanned = false;

        [MenuItem("Tools/Bear/Class Collector")]
        public static void ShowWindow()
        {
            var window = GetWindow<ClassCollectorEditorWindow>("Class Collector");
            window.minSize = new Vector2(500, 400);
            window.LoadPrefs();
        }

        /// <summary>
        /// 构建流水线调用的自动收集入口（无弹窗）。
        /// </summary>
        public static bool TryRunAutoCollection(out string message)
        {
            var runner = CreateInstance<ClassCollectorEditorWindow>();
            try
            {
                runner.LoadPrefs();
                runner.ScanTypes();

                if (!runner.hasScanned || runner.collectedTypes.Count == 0)
                {
                    message = "ClassCollector 扫描失败，未收集到任何类型。";
                    return false;
                }

                var distinctTypes = runner.collectedTypes
                    .GroupBy(t => t.FullName)
                    .Select(g => g.First())
                    .ToList();

                if (distinctTypes.Count == 0)
                {
                    message = "ClassCollector 扫描结果为空（去重后无类型）。";
                    return false;
                }

                runner.GenerateLinkXML(distinctTypes);

                message = $"ClassCollector 自动执行完成：已生成 link.xml，{distinctTypes.Count} 个类型。";
                return true;
            }
            catch (Exception ex)
            {
                message = $"ClassCollector 自动执行异常：{ex.Message}";
                Debug.LogError($"[ClassCollector] {message}\n{ex}");
                return false;
            }
            finally
            {
                DestroyImmediate(runner);
            }
        }

        private void LoadPrefs()
        {
            sourceFolder = EditorPrefs.GetString(PREFS_KEY_SOURCE_FOLDER, "Assets/Game/Scripts");
            includeAbstract = EditorPrefs.GetBool(PREFS_KEY_INCLUDE_ABSTRACT, false);
            includeStatic = EditorPrefs.GetBool(PREFS_KEY_INCLUDE_STATIC, false);
            includeInterface = EditorPrefs.GetBool(PREFS_KEY_INCLUDE_INTERFACE, false);
            generateLinkXML = EditorPrefs.GetBool("ClassCollector_GenerateLinkXML", true);
            linkXMLOutputPath = EditorPrefs.GetString("ClassCollector_LinkXMLOutputPath", "Assets/link.xml");
            LoadPrefabList();
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString(PREFS_KEY_SOURCE_FOLDER, sourceFolder);
            EditorPrefs.SetBool(PREFS_KEY_INCLUDE_ABSTRACT, includeAbstract);
            EditorPrefs.SetBool(PREFS_KEY_INCLUDE_STATIC, includeStatic);
            EditorPrefs.SetBool(PREFS_KEY_INCLUDE_INTERFACE, includeInterface);
            EditorPrefs.SetBool("ClassCollector_GenerateLinkXML", generateLinkXML);
            EditorPrefs.SetString("ClassCollector_LinkXMLOutputPath", linkXMLOutputPath);
            SavePrefabList();
        }

        private void LoadPrefabList()
        {
            prefabList ??= new List<GameObject>();
            prefabList.Clear();
            string saved = EditorPrefs.GetString(PREFS_KEY_PREFAB_LIST, "");
            if (string.IsNullOrEmpty(saved)) return;
            foreach (string guid in saved.Split(';'))
            {
                if (string.IsNullOrEmpty(guid)) continue;
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab != null) prefabList.Add(prefab);
                }
            }
        }

        private void SavePrefabList()
        {
            var guids = new List<string>();
            foreach (var prefab in prefabList)
            {
                if (prefab == null) continue;
                string path = AssetDatabase.GetAssetPath(prefab);
                string guid = AssetDatabase.AssetPathToGUID(path);
                if (!string.IsNullOrEmpty(guid)) guids.Add(guid);
            }
            EditorPrefs.SetString(PREFS_KEY_PREFAB_LIST, string.Join(";", guids));
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("类型收集器", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("配置", EditorStyles.boldLabel);
            sourceFolder = EditorGUILayout.TextField("源文件夹", sourceFolder);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("过滤条件", EditorStyles.boldLabel);
            includeAbstract = EditorGUILayout.Toggle("包含抽象类 (abstract)", includeAbstract);
            includeStatic = EditorGUILayout.Toggle("包含静态类 (static)", includeStatic);
            includeInterface = EditorGUILayout.Toggle("包含接口 (interface)", includeInterface);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("防剪裁", EditorStyles.boldLabel);
            generateLinkXML = EditorGUILayout.Toggle("同时生成 link.xml", generateLinkXML);
            if (generateLinkXML)
            {
                linkXMLOutputPath = EditorGUILayout.TextField("link.xml 路径", linkXMLOutputPath);
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("指定 Prefab 扫描", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("拖拽 Prefab 到下方列表，扫描时会自动收集这些 Prefab 内所有引用的脚本类型并加入 link.xml", MessageType.Info);
            DrawPrefabList();

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("扫描", GUILayout.Height(30)))
            {
                SavePrefs();
                ScanTypes();
            }
            GUI.enabled = hasScanned && collectedTypes.Count > 0;
            if (GUILayout.Button("生成 link.xml", GUILayout.Height(30)))
            {
                SavePrefs();
                GenerateCode();
            }
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            if (hasScanned)
            {
                EditorGUILayout.LabelField($"扫描结果：共 {collectedTypes.Count} 个类型", EditorStyles.boldLabel);
                scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
                foreach (var info in collectedTypes)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(info.FullName, GUILayout.ExpandWidth(true));
                    EditorGUILayout.LabelField($"[{info.Kind}]", GUILayout.Width(80));
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void ScanTypes()
        {
            collectedTypes.Clear();
            hasScanned = false;

            if (!AssetDatabase.IsValidFolder(sourceFolder))
            {
                EditorUtility.DisplayDialog("错误", $"文件夹不存在：{sourceFolder}", "确定");
                return;
            }

            // 1. 扫描文件夹脚本
            string[] guids = AssetDatabase.FindAssets("t:MonoScript", new[] { sourceFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (monoScript == null) continue;

                var type = monoScript.GetClass();
                if (type == null) continue;

                // 过滤嵌套类
                if (type.IsNested) continue;

                // 过滤 Editor-only 类
                if (IsEditorOnlyClass(type, path)) continue;

                // 过滤接口
                if (type.IsInterface && !includeInterface) continue;

                // 过滤抽象类
                if (type.IsAbstract && !type.IsInterface && !includeAbstract) continue;

                // 过滤静态类（C# 中静态类是 abstract + sealed）
                if (type.IsAbstract && type.IsSealed && !type.IsInterface && !includeStatic) continue;

                string kind = type.IsInterface ? "interface" : (type.IsAbstract && type.IsSealed ? "static" : (type.IsAbstract ? "abstract" : "class"));
                collectedTypes.Add(new CollectedTypeInfo
                {
                    FullName = type.FullName,
                    Namespace = type.Namespace,
                    Name = type.Name,
                    Kind = kind,
                    AssemblyName = type.Assembly.GetName().Name
                });
            }

            // 2. 扫描指定 Prefab 内的脚本引用
            int prefabTypeCount = ScanPrefabs();

            // 去重并排序
            collectedTypes = collectedTypes
                .GroupBy(t => t.FullName)
                .Select(g => g.First())
                .OrderBy(t => t.FullName)
                .ToList();
            hasScanned = true;

            Debug.Log($"[ClassCollector] 扫描完成，文件夹脚本 {collectedTypes.Count - prefabTypeCount} 个，Prefab 脚本 {prefabTypeCount} 个，共 {collectedTypes.Count} 个类型");
        }

        private int ScanPrefabs()
        {
            if (prefabList == null || prefabList.Count == 0) return 0;

            var addedFullNames = new HashSet<string>(collectedTypes.Select(t => t.FullName));
            int count = 0;

            foreach (var prefab in prefabList)
            {
                if (prefab == null) continue;

                string prefabPath = AssetDatabase.GetAssetPath(prefab);
                GameObject root = PrefabUtility.IsPartOfPrefabAsset(prefab)
                    ? prefab
                    : AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

                if (root == null)
                {
                    Debug.LogWarning($"[ClassCollector] 无法加载 Prefab：{prefabPath}");
                    continue;
                }

                // 扫描所有 Component（不只是 MonoBehaviour），才能覆盖 Unity 内置组件如 SpringJoint2D
                var components = root.GetComponentsInChildren<Component>(true);
                foreach (var component in components)
                {
                    if (component == null) continue;

                    var type = component.GetType();
                    if (type == null) continue;

                    // 过滤 Transform / RectTransform 等每个 GameObject 必带的基础类型
                    if (typeof(Transform).IsAssignableFrom(type)) continue;

                    // 过滤嵌套类
                    if (type.IsNested) continue;

                    // 过滤 Editor-only 类
                    string scriptPath = GetScriptPathByType(type);
                    if (IsEditorOnlyClass(type, scriptPath)) continue;

                    // 过滤接口
                    if (type.IsInterface && !includeInterface) continue;

                    // 过滤抽象类
                    if (type.IsAbstract && !type.IsInterface && !includeAbstract) continue;

                    // 过滤静态类
                    if (type.IsAbstract && type.IsSealed && !type.IsInterface && !includeStatic) continue;

                    if (addedFullNames.Add(type.FullName))
                    {
                        string kind = type.IsInterface ? "interface" : (type.IsAbstract && type.IsSealed ? "static" : (type.IsAbstract ? "abstract" : "class"));
                        collectedTypes.Add(new CollectedTypeInfo
                        {
                            FullName = type.FullName,
                            Namespace = type.Namespace,
                            Name = type.Name,
                            Kind = $"{kind} (prefab)",
                            AssemblyName = type.Assembly.GetName().Name
                        });
                        count++;
                    }
                }
            }

            return count;
        }

        private string GetScriptPathByType(Type type)
        {
            if (type == null) return null;
            try
            {
                string[] guids = AssetDatabase.FindAssets($"t:MonoScript {type.Name}");
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var monoScript = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                    if (monoScript != null && monoScript.GetClass() == type)
                    {
                        return path;
                    }
                }
            }
            catch { }
            return null;
        }

        private void DrawPrefabList()
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);

            int removeIndex = -1;
            prefabScrollPos = EditorGUILayout.BeginScrollView(prefabScrollPos, GUILayout.MinHeight(80), GUILayout.MaxHeight(200));
            for (int i = 0; i < prefabList.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                prefabList[i] = EditorGUILayout.ObjectField($"Prefab {i + 1}", prefabList[i], typeof(GameObject), false) as GameObject;
                if (GUILayout.Button("✕", GUILayout.Width(24)))
                {
                    removeIndex = i;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            if (removeIndex >= 0)
            {
                prefabList.RemoveAt(removeIndex);
                SavePrefabList();
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("+ 添加", GUILayout.Width(80)))
            {
                prefabList.Add(null);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // 支持拖拽
            var dropArea = GUILayoutUtility.GetLastRect();
            Event evt = Event.current;
            switch (evt.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropArea.Contains(evt.mousePosition)) break;
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (evt.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        foreach (UnityEngine.Object draggedObject in DragAndDrop.objectReferences)
                        {
                            if (draggedObject is GameObject go)
                            {
                                string path = AssetDatabase.GetAssetPath(go);
                                if (!string.IsNullOrEmpty(path) && PrefabUtility.IsPartOfPrefabAsset(go))
                                {
                                    if (!prefabList.Contains(go))
                                    {
                                        prefabList.Add(go);
                                    }
                                }
                            }
                        }
                        SavePrefabList();
                        evt.Use();
                    }
                    break;
            }
        }

        /// <summary>
        /// 判断是否为 Editor-only 类（运行时不可用）
        /// </summary>
        private bool IsEditorOnlyClass(Type type, string assetPath)
        {
            // 1. 文件路径在 Editor 文件夹下（包括子文件夹）
            if (!string.IsNullOrEmpty(assetPath))
            {
                string[] pathParts = assetPath.Split('/');
                for (int i = 0; i < pathParts.Length - 1; i++)
                {
                    if (pathParts[i].Equals("Editor", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            // 2. 继承自 UnityEditor 命名空间下的 Editor-only 基类
            var baseType = type.BaseType;
            while (baseType != null)
            {
                string ns = baseType.Namespace;
                if (!string.IsNullOrEmpty(ns) && ns.StartsWith("UnityEditor"))
                {
                    return true;
                }
                baseType = baseType.BaseType;
            }

            // 3. 类名或命名空间明显是 Editor-only（如继承 PropertyDrawer 但上面可能没走到的情况）
            if (!string.IsNullOrEmpty(type.Namespace) && type.Namespace.StartsWith("UnityEditor"))
                return true;

            // 4. 被 Editor-only 的程序集定义（Editor asmdef）编译的类
            //    运行时程序集不会包含 .Editor 后缀的 asmdef
            var assemblyName = type.Assembly.GetName().Name;
            if (!string.IsNullOrEmpty(assemblyName) && assemblyName.EndsWith(".Editor"))
                return true;

            // 5. 被 InitializeOnLoad / InitializeOnLoadMethod 等 Editor 特性标记
            var attributes = type.GetCustomAttributes(false);
            foreach (var attr in attributes)
            {
                string attrName = attr.GetType().FullName;
                if (attrName == "UnityEditor.InitializeOnLoadAttribute" ||
                    attrName == "UnityEditor.InitializeOnLoadMethodAttribute" ||
                    attrName == "UnityEditor.Callbacks.DidReloadScripts" ||
                    attrName == "UnityEditor.Callbacks.OnOpenAssetAttribute" ||
                    attrName == "UnityEditor.Callbacks.PostProcessBuildAttribute" ||
                    attrName == "UnityEditor.Callbacks.PostProcessSceneAttribute")
                {
                    return true;
                }
            }

            // 6. 源码中直接引用了 UnityEditor 命名空间（using 或类型引用）
            if (HasUnityEditorReference(assetPath))
                return true;

            return false;
        }

        /// <summary>
        /// 检查源码文件中是否包含 UnityEditor 的直接引用
        /// </summary>
        private bool HasUnityEditorReference(string assetPath)
        {
            try
            {
                string fullPath = Path.Combine(Application.dataPath, "..", assetPath);
                if (!File.Exists(fullPath)) return false;

                string rawContent = File.ReadAllText(fullPath);

                // 1. 如果类定义在 #if UNITY_EDITOR 块内 → 是 Editor-only 类
                if (HasClassInsideEditorBlock(rawContent))
                    return true;

                // 2. 移除注释和 #if UNITY_EDITOR 块，检查块外的 Editor 引用
                string content = RemoveComments(rawContent);
                content = RemoveEditorBlocks(content);

                // 3. 检查块外是否还有 using UnityEditor;
                if (System.Text.RegularExpressions.Regex.IsMatch(content, @"\busing\s+UnityEditor\s*;"))
                    return true;

                // 4. 检查块外是否还有 UnityEditor.xxx 类型引用
                if (System.Text.RegularExpressions.Regex.IsMatch(content, @"\bUnityEditor\.\w+"))
                    return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ClassCollector] 读取文件失败：{assetPath}, Error: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// 检查是否有类/接口/结构体定义在 #if UNITY_EDITOR 条件编译块内
        /// </summary>
        private bool HasClassInsideEditorBlock(string content)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(
                content,
                @"#\s*if\s+UNITY_EDITOR(.*?)#\s*endif",
                System.Text.RegularExpressions.RegexOptions.Singleline);

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                string blockContent = match.Groups[1].Value;
                // 检测块内是否有类型定义
                if (System.Text.RegularExpressions.Regex.IsMatch(
                    blockContent,
                    @"\b(?:public|internal|private|protected|abstract|sealed|static\s+)?\s*(?:partial\s+)?(?:class|interface|struct|enum)\s+\w+"))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 移除 C# 代码中的注释，减少检测误判
        /// </summary>
        private string RemoveComments(string code)
        {
            // 移除 // 单行注释
            var singleLine = System.Text.RegularExpressions.Regex.Replace(code, @"//.*$", string.Empty, System.Text.RegularExpressions.RegexOptions.Multiline);
            // 移除 /* */ 多行注释
            var multiLine = System.Text.RegularExpressions.Regex.Replace(singleLine, @"/\*.*?\*/", string.Empty, System.Text.RegularExpressions.RegexOptions.Singleline);
            return multiLine;
        }

        /// <summary>
        /// 移除 #if UNITY_EDITOR ... #endif 条件编译块
        /// </summary>
        private string RemoveEditorBlocks(string code)
        {
            return System.Text.RegularExpressions.Regex.Replace(
                code,
                @"#\s*if\s+UNITY_EDITOR.*?#\s*endif",
                string.Empty,
                System.Text.RegularExpressions.RegexOptions.Singleline);
        }

        private void GenerateCode()
        {
            if (collectedTypes.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有收集到任何类型，请先扫描。", "确定");
                return;
            }

            // 去重（按 FullName）
            var distinctTypes = collectedTypes
                .GroupBy(t => t.FullName)
                .Select(g => g.First())
                .ToList();

            if (!generateLinkXML)
            {
                EditorUtility.DisplayDialog("提示", "请先开启 link.xml 生成。", "确定");
                return;
            }

            GenerateLinkXML(distinctTypes);

            EditorUtility.DisplayDialog("完成",
                $"link.xml 已生成：{linkXMLOutputPath}\n共 {distinctTypes.Count} 个类型。",
                "确定");
        }

        private void GenerateLinkXML(List<CollectedTypeInfo> types)
        {
            string directory = Path.GetDirectoryName(linkXMLOutputPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 1) 生成本次扫描得到的规则
            var assemblyMap = BuildGeneratedAssemblyMap(types);

            // 2) 合并已有 link.xml 中的手工规则，避免覆盖
            MergeExistingLinkXmlRules(assemblyMap, linkXMLOutputPath);

            // 3) 输出最终结果
            var sb = BuildLinkXmlContent(assemblyMap);

            File.WriteAllText(linkXMLOutputPath, sb.ToString(), Encoding.UTF8);
            AssetDatabase.ImportAsset(linkXMLOutputPath);
            AssetDatabase.Refresh();

            Debug.Log($"[ClassCollector] link.xml 已生成：{linkXMLOutputPath}");
        }

        private string GetAssemblyName(CollectedTypeInfo info)
        {
            // 扫描阶段已记录真实程序集名称，避免 Type.GetType 跨程序集查找失败
            if (!string.IsNullOrEmpty(info.AssemblyName))
                return info.AssemblyName;

            return "Assembly-CSharp";
        }

        private Dictionary<string, LinkerAssemblyInfo> BuildGeneratedAssemblyMap(List<CollectedTypeInfo> types)
        {
            var assemblyMap = new Dictionary<string, LinkerAssemblyInfo>(StringComparer.Ordinal);

            foreach (var info in types)
            {
                var assemblyName = GetAssemblyName(info);
                if (!assemblyMap.TryGetValue(assemblyName, out var assemblyInfo))
                {
                    assemblyInfo = new LinkerAssemblyInfo(assemblyName);
                    assemblyMap.Add(assemblyName, assemblyInfo);
                }

                assemblyInfo.TypeNames.Add(info.FullName);
            }

            return assemblyMap;
        }

        private void MergeExistingLinkXmlRules(Dictionary<string, LinkerAssemblyInfo> assemblyMap, string linkXmlPath)
        {
            if (!File.Exists(linkXmlPath))
            {
                return;
            }

            try
            {
                var doc = XDocument.Load(linkXmlPath);
                var root = doc.Root;
                if (root == null || !string.Equals(root.Name.LocalName, "linker", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                foreach (var assemblyElement in root.Elements("assembly"))
                {
                    var assemblyName = assemblyElement.Attribute("fullname")?.Value;
                    if (string.IsNullOrWhiteSpace(assemblyName))
                    {
                        continue;
                    }

                    if (!assemblyMap.TryGetValue(assemblyName, out var assemblyInfo))
                    {
                        assemblyInfo = new LinkerAssemblyInfo(assemblyName);
                        assemblyMap.Add(assemblyName, assemblyInfo);
                    }

                    var preserve = assemblyElement.Attribute("preserve")?.Value;
                    if (!string.IsNullOrWhiteSpace(preserve))
                    {
                        assemblyInfo.Preserve = preserve;
                    }

                    foreach (var typeElement in assemblyElement.Elements("type"))
                    {
                        var typeName = typeElement.Attribute("fullname")?.Value;
                        if (string.IsNullOrWhiteSpace(typeName))
                        {
                            continue;
                        }

                        assemblyInfo.TypeNames.Add(typeName);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ClassCollector] 读取现有 link.xml 失败，将仅使用本次扫描结果。Error: {ex.Message}");
            }
        }

        private string BuildLinkXmlContent(Dictionary<string, LinkerAssemblyInfo> assemblyMap)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<linker>");

            // 固定把 Assembly-CSharp 放在最前，其他按名称排序，减少 diff 噪音
            var orderedAssemblies = assemblyMap.Values
                .OrderBy(a => a.FullName == "Assembly-CSharp" ? 0 : 1)
                .ThenBy(a => a.FullName, StringComparer.Ordinal);

            foreach (var assembly in orderedAssemblies)
            {
                if (!string.IsNullOrWhiteSpace(assembly.Preserve))
                {
                    sb.AppendLine($"    <assembly fullname=\"{assembly.FullName}\" preserve=\"{assembly.Preserve}\">");
                }
                else
                {
                    sb.AppendLine($"    <assembly fullname=\"{assembly.FullName}\">");
                }

                foreach (var typeName in assembly.TypeNames.OrderBy(t => t, StringComparer.Ordinal))
                {
                    // Prefab/Bundle 驱动的类型需要完整保留，避免构建裁剪掉方法或构造函数。
                    sb.AppendLine($"        <type fullname=\"{typeName}\" preserve=\"all\" />");
                }

                sb.AppendLine("    </assembly>");
            }

            sb.AppendLine("</linker>");
            return sb.ToString();
        }

        private struct CollectedTypeInfo
        {
            public string FullName;
            public string Namespace;
            public string Name;
            public string Kind;
            public string AssemblyName;
        }

        private sealed class LinkerAssemblyInfo
        {
            public string FullName { get; }
            public string Preserve { get; set; }
            public HashSet<string> TypeNames { get; }

            public LinkerAssemblyInfo(string fullName)
            {
                FullName = fullName;
                Preserve = string.Empty;
                TypeNames = new HashSet<string>(StringComparer.Ordinal);
            }
        }
    }
}
