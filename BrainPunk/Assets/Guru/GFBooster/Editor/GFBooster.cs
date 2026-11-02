using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.UI;
using YooAsset.Editor;
using Application = UnityEngine.Application;
using EU=UnityEditor.EditorUtility;
using Object = System.Object;

namespace GF.Editor
{
    
    #region Welcome Window

    /// <summary>
    /// GF 启动欢迎界面
    /// </summary>
    public class GfWelcomeWindow : EditorWindow
    {

        public enum DisplayStatus
        {
            None = 0,
            Displayed = 1,
            NeverShow = 2,
        }

        private const string Version = "0.1.0";

        private static bool _neverShowThis = false;
        private Vector2 _scrollPosition;
        private static readonly Vector2 WindowSize = new Vector2(600, 800);

        private const string YooAssetsHelpUrl = "https://www.yooasset.com/docs/Introduce";
        private const string GuruSupportEmail = "mailto:yufei.hu@castboc.fm";
        private static string GfHelpUrl => $"file://{Application.dataPath}/GF/README.md";
        
        private static GfSettingsFile _settings;
        private static GfSettingsFile GfSettings => _settings ??= GfSettingsFile.LoadOrCreat();

        
        
        #region 启动检查


        [InitializeOnLoadMethod]
        private static void InitializeOnLoad()
        {
            if (GfSettings.CheckShouldShow())
            {
                ShowWindow();
            }
        }

        #endregion


        private void OnEnable()
        {
            if(GfSettings.displayStatus == DisplayStatus.NeverShow)
            {
                _neverShowThis = true;
            }
        }

        #region GUI

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // 标题
            ShowTitle();

            // 快速开始
            ShowQuickStart();

            // 项目工具
            // DrawSection("项目工具", () =>
            // {
            //     DrawButton("构建资源包", "打开资源包构建工具",
            //         () => { DisplayDialog("提示", "请使用 Jenkins 或命令行进行构建", "确定"); });
            //
            //     DrawButton("创建美术资源包", "自动创建关卡和缩略图资源包", () =>
            //     {
            //         Debug.Log("美术资源包创建完成");
            //     });
            // });
            //
            // DrawSeparator();

            // 文档
            ShowDocumentation();
            
            // 选项
            ShowOptions();
            
            EditorGUILayout.EndScrollView();
        }

        private void DrawSection(string sTitle, Action sContent)
        {
            GUILayout.Space(10);

            GUIStyle sectionTitleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16
            };
            EditorGUILayout.LabelField(sTitle, sectionTitleStyle);

            GUILayout.Space(5);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            sContent?.Invoke();
            EditorGUILayout.EndVertical();
        }

        private void DrawButton(string label, string tooltip, Action onClick, Color color = default)
        {
            var btnHeight = 40;
            var defColor = Color.gray;
            GUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10);


            if (color != default)
            {
                defColor = GUI.color;
                GUI.color = color;
            }
            
            GUIContent content = new GUIContent(label, tooltip);
            if (GUILayout.Button(content, GUILayout.Height(btnHeight)))
            {
                onClick?.Invoke();
            }

            if (color != default)
            {
                GUI.color = defColor;
            }

            GUILayout.Space(10);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(5);
        }

        private void DrawSeparator()
        {
            GUILayout.Space(10);
            Rect rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            GUILayout.Space(10);
        }

        private static bool DisplayDialog(string sTitle, string message, string ok,  string cancel = "")
        {
            return EU.DisplayDialog(sTitle, message, ok, cancel);
        }

        
        
        
        
        #endregion
        
        #region EditorMenu

        private const string MenuName = "GF/[ Welcome to GF ]";
        [MenuItem(MenuName, false, 0)]
        public static void ShowWindow()
        {
            var windowWidth = WindowSize.x;
            var windowHeight = WindowSize.y;

            var tabName  = "WELCOME";
            GfWelcomeWindow window = GetWindow<GfWelcomeWindow>(tabName);
            window.minSize = new Vector2(windowWidth, windowHeight);
            window.maxSize = new Vector2(windowWidth, windowHeight * 2);
            window.Show();
            
            GfSettings.SetDisplayed();
        }

        #endregion

        #region Title

        private void ShowTitle()
        {
            GUILayout.Space(20);
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 24,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("欢迎使用 GuruFramework", titleStyle, GUILayout.Height(60));

            GUILayout.Space(10);

            // 版本信息
            GUIStyle versionStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.gray }
            };
            EditorGUILayout.LabelField($"Version {Version}", versionStyle);

            GUILayout.Space(20);
            DrawSeparator();
        }

        #endregion
        
        #region Quick Start

        private void ShowQuickStart()
        {
            EditorGUI.indentLevel++;
            // 快速开始
            DrawSection("快速开始", () =>
            {
                DrawButton("一键初始化项目", "创建游戏自身的项目结构", () =>
                {
                    InputDialog.Display("项目名:", InitProjectStructure);
                }, color: new Color(0.72f, 1f, 0f));
            });

            EditorGUI.indentLevel--;
            
            DrawSeparator();
        }



        /// <summary>
        /// 初始化工程结构
        /// </summary>
        private void InitProjectStructure(string appName)
        {
            GfBuilder.Build(appName);
            
        }


        #endregion

        #region Docmention

        private void ShowDocumentation()
        {
            EditorGUI.indentLevel++;
            
            // 文档链接
            DrawSection("文档与支持", () =>
            {
                DrawButton("GF 框架文档", "打开 GF 框架文档", () => { Application.OpenURL(GfHelpUrl); });

                DrawButton("YooAsset 文档", "查看 YooAsset 资源管理文档", () => { Application.OpenURL(YooAssetsHelpUrl); });

                DrawButton("技术支持", "联系 Guru 技术支持团队", () => { Application.OpenURL(GuruSupportEmail); });
            });

            DrawSeparator();
            GUILayout.Space(20);
            
            
            EditorGUI.indentLevel--;   
        }
        

        #endregion

        #region Options

        private void ShowOptions()
        {
            EditorGUI.indentLevel++;
            // 启动时显示选项
            EditorGUI.BeginChangeCheck();
            _neverShowThis = EditorGUILayout.ToggleLeft("  不再显示此窗口", _neverShowThis);
            if (EditorGUI.EndChangeCheck())
            {
                if (_neverShowThis)
                {
                    var sTitle = "不再显示欢迎页面";
                    var msg = $"关闭后仍然可以在菜单\n\n{MenuName}\n\n中打开欢迎页面。";

                    GfSettings.displayStatus = DisplayStatus.NeverShow;
                    DisplayDialog(sTitle, msg, "OK", "Cancel");
                }
                else
                {
                    GfSettings.displayStatus = DisplayStatus.Displayed;
                    GfSettings.SetDisplayed();
                }
            }
            EditorGUI.indentLevel--;
            
            GUILayout.Space(10);
        }


        #endregion
        
        #region Settings File

        
        [Serializable]
        // GF 设置文件
        internal class GfSettingsFile
        {
            [JsonProperty("display_status")]
            public DisplayStatus displayStatus = DisplayStatus.None;

            [JsonProperty("display_date")]
            public string displayDate = "";
            
            private static string FilePath =>
                Path.GetFullPath($"{Application.dataPath}/../ProjectSettings/guru_gf_settings.txt");

            public static GfSettingsFile LoadOrCreat()
            {
                if (!File.Exists(FilePath))
                {
                    return new GfSettingsFile();
                }

                var json = File.ReadAllText(FilePath);
                return ReadFormJson(json);
            }


            private static GfSettingsFile ReadFormJson(string json)
            {
                try
                {
                    return JsonConvert.DeserializeObject<GfSettingsFile>(json);
                }
                catch (Exception e)
                {
                    Debug.LogError(e.ToString());
                    return new GfSettingsFile();
                }
            }

            public void Save()
            {
                var json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(FilePath, json);
            }

            
                    
            public bool CheckShouldShow()
            {
                if (displayStatus == DisplayStatus.NeverShow) return false;
                if (displayStatus == DisplayStatus.None) return true;
                if (string.IsNullOrEmpty(displayDate)) return true;
                if (GfSettings.displayStatus == DisplayStatus.Displayed)
                {
                    // 上次展示 7 天以上再进行一次展示
                    var lastDate = DateTime.Parse(displayDate);
                    if ((DateTime.Now - lastDate).Days > 7)
                    {
                        return true;
                    }
                }

                return false;
            }


            public void SetDisplayed()
            {
                if(displayStatus == DisplayStatus.None)
                    displayStatus = DisplayStatus.Displayed;
                displayDate = DateTime.Now.ToString("g");
                
                
                Save();
            }

        }

        #endregion
        
    }
    
    #endregion
    
    #region Input Dialog
    
    internal class InputDialog:EditorWindow
    {
        private static Vector2 _windowSize = new Vector2(400, 60);

        private string _label;
        private string _content;
        private string _okString;
        private Action<string> _onConfirmedHandle;
            
        private static InputDialog OpenWindow(string tabName = "")
        {
            var windowWidth = _windowSize.x;
            var windowHeight = _windowSize.y;

            if(string.IsNullOrEmpty(tabName))
                tabName = "输入框";
            
            InputDialog window = GetWindow<InputDialog>(tabName);
            window.minSize = new Vector2(windowWidth, windowHeight);
            window.maxSize = new Vector2(windowWidth, windowHeight);
            window.Show();
            return window;
        }

        private void OnGUI()
        {
            GUILayout.Space(20);
            
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(_label, GUILayout.Width(80));

            EditorGUI.BeginChangeCheck();
            _content = GUILayout.TextField(_content);
            if (EditorGUI.EndChangeCheck())
            {
                    
            }

            if (GUILayout.Button(_okString, GUILayout.Width(80)))
            {
                _content = _content.Trim();
                _onConfirmedHandle?.Invoke(_content);
                Close();
            }
                
            EditorGUILayout.EndHorizontal();
        }


        public static void Display(string label, Action<string> onConformed, string defaultString = "", string ok = "确定", string windowName = "")
        {
            var window = OpenWindow(windowName);
            window._label = label;
            window._content = defaultString;
            window._okString = ok;
            window._onConfirmedHandle = onConformed;
        }
        
     
            
    }
    
    #endregion
    
    #region GF Builder
    
    internal class GfBuilder
    {
        
        private const string DefaultPackageName = "GamePackage";
        
        private readonly string _appName;
        private readonly string _appPath;
        private readonly string _appBundlesPath;
        private readonly string _appScriptsPath;

        public string AppName => _appName;
        
        
        private string[] _subFolders = new[]
        {
            "Scenes",
            "Bundles",
            "Bundles/Root",
            "Bundles/UI",
            "Resources",
            "Scripts",
            "Scripts/Editor",
            "Scripts/Runtime",
            "Scripts/Runtime/UI",
        };

        private const string RootScriptTemplate = @"using System;
using UnityEngine;

namespace $AppName
{
    public class Root: MonoBehaviour
    {
        // [$AppName] 根节点启动入口
        private void Start()
        {
            Debug.Log($""<color=#88ff00>=== [{nameof($AppName)}] Root Start Success! ===</color>\n<color=yellow>开始编写项目代码吧！</color>"");
            // TODO: 开始你的第一行代码！
            
        }
    }
}";

        private const string RootPrefabTemplate = @"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!1 &5880663032555271224
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  serializedVersion: 6
  m_Component:
  - component: {fileID: 8687575473917955773}
  - component: {fileID: 8828636257810891095}
  m_Layer: 0
  m_Name: Root
  m_TagString: Untagged
  m_Icon: {fileID: 0}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
--- !u!4 &8687575473917955773
Transform:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 5880663032555271224}
  serializedVersion: 2
  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}
  m_LocalPosition: {x: 0, y: 0, z: 0}
  m_LocalScale: {x: 1, y: 1, z: 1}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {fileID: 0}
  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}
--- !u!114 &8828636257810891095
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 5880663032555271224}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: $fileID, guid: $guid, type: 3}
  m_Name: 
  m_EditorClassIdentifier: 
";
        

        public static void Build(string appName)
        {
            var builder = new GfBuilder(appName);
            builder.TryBuildAll();
        }


        private GfBuilder(string appName)
        {
            if (appName.Contains(" "))
            {
                var words = appName.Split(' ');

                var name = "";
                for (int i = 0; i < words.Length; i++)
                {
                    name += words[i][0].ToString().ToUpper();
                    if (words.Length > 1)
                    {
                        name += words[i].Substring(1);
                    }
                }

                appName = name;
            }
            
            _appName = appName;
            _appPath = $"Assets/{_appName}";
            _appBundlesPath = $"{_appPath}/Bundles";
            _appScriptsPath = $"{_appPath}/Scripts/Runtime";
            

        }


        private void TryBuildAll()
        {
            // 不会重复建立项目
            if (Directory.Exists(_appPath))
            {
                EU.DisplayDialog("创建错误", "APP 已存在，请勿重复创建。", "好的，我知道了");
                return;
            }

            try
            {
                StartBuildFlow();
            }
            catch (Exception e)
            {
                EU.DisplayDialog("出错了！", $"构建出现了错误：\n\n{e.Message}", "收到");
            }
        }


        private void StartBuildFlow()
        {
            // ------ 创建流程 START ------
            // #1. 检查项目目录是否存在
            var appPath = Path.GetFullPath(_appPath);
            EnsurePathExists(appPath);
            
            var editorPath = Path.GetFullPath($"{Application.dataPath}/Editor");
            EnsurePathExists(editorPath);
            
            // #2. 创建各个子目录
            foreach (var f in _subFolders)
            {
                var path = $"{_appPath}/{f}";
                EnsurePathExists(path);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // #3. 组件创建
            CreateLaunchScene();
            CreateScriptRules();
            CreateGameSettings();
            CreateAssetBundleCollector();
            CreateRoot();
            CreateUILayers();
            
            // ------ 创建流程 END ------
            // 保存
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 构建一次 Bundle
            CallYooAssetBuild();

            OnDelayNotification();
            
            // 最后拉起编译            
            // CompilationPipeline.compilationFinished += OnCompilationFinished;
            CompilationPipeline.RequestScriptCompilation();
        }


        // 确保路径可用
        private static void EnsurePathExists(string path)
        {
            if (Directory.Exists(path))
            {
                Debug.Log($"路径已存在: {path}");
                return;
            }
            
            Directory.CreateDirectory(path);
            Debug.Log($"已创建路径: {path}");
        }

        // 创建 SO
        private static T CreateSo<T>(string assetPath) where T : ScriptableObject
        {
            var dir = Directory.GetParent(assetPath);
            if(!dir.Exists)
                dir.Create();
            
            var so = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(so, assetPath);
            EU.SetDirty(so);

            return so;
        }

        // 创建脚本设置
        private void CreateScriptRules()
        {
            var path = $"Assets/Editor/ScriptRulerSetting.asset";
            ScriptRulerSetting so = null;
            if (!File.Exists(path))
            {
                so = CreateSo<ScriptRulerSetting>(path);
            }
            else
            {
                so = AssetDatabase.LoadAssetAtPath<ScriptRulerSetting>(path);
            }
            
            if (!File.Exists(path) || so == null)
            {
                Debug.LogError($"SO 创建失败: {path}");
                return;
            }

            try
            {
                // 创建对应的属性
                var data = new Dictionary<string, object>()
                {
                    { "_namespace", _appName },
                    { "_codePath", $"{_appScriptsPath}/UI"}
                };

                var result = ScriptableObjectModifier.ModifyProperties(so, data);
                if (result)
                {
                    EU.SetDirty(so);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"✅ 成功修改 {so.name}");
                }
                else
                {
                    Debug.LogError($"❌ 修改 {so.name} 失败");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"ScriptRulerSetting.asset 创建失败: {e.Message}");
            }
        }
        
        
        /// <summary>
        /// 创建游戏设置
        /// </summary>
        private void CreateGameSettings()
        {
            var path = $"Assets/{_appName}/Resources/GameSetting.asset";
            if (File.Exists(path))
            {
                Debug.Log($"GameSetting.asset 已存在: {path}");
                return;
            }
            
            var so = CreateSo<GameSetting>(path);

            if (!File.Exists(path))
            {
                Debug.LogError($"SO 创建失败: {path}");
                return;
            }
       
            try
            { 
                // 创建 GameSettings 默认值
                so.defaultPackageName = DefaultPackageName;
                so.builtinPackageList = new List<BuiltinPackageElement>()
                {
                   new BuiltinPackageElement()
                   {
                       packageName = DefaultPackageName,
                       playMode = YooAsset.EPlayMode.EditorSimulateMode, // 默认是 Editor 模拟
                       generation = "",
                       yooVersion = "1.0.0"
                   }
                };
                
                Debug.Log($"✅ 创建 GameSetting.asset 成功");
                
                EU.SetDirty( so);
                AssetDatabase.SaveAssets();
            }
            catch (Exception e)
            {
                Debug.LogError($"GameSetting.asset 创建失败: {e.Message}");
            }
            
        }

        
        /// <summary>
        /// 创建 YooAssets 打包配置
        /// </summary>
        private void CreateAssetBundleCollector()
        {
            var soPath = $"Assets/{_appName}/AssetBundleCollectorSetting.asset";
            var so = CreateSo<AssetBundleCollectorSetting>(soPath);

            var rootBundlePath = $"{_appBundlesPath}/Root";
            var rootGuid = AssetDatabase.GUIDFromAssetPath(rootBundlePath);

            so.ShowPackageView = true;
            so.ShowEditorAlias = true;
            so.Packages = new List<AssetBundleCollectorPackage>()
            {
                new AssetBundleCollectorPackage()
                {
                    PackageName = DefaultPackageName,
                    PackageDesc = "默认打包配置",
                    Groups =  new List<AssetBundleCollectorGroup>()
                    {
                        new AssetBundleCollectorGroup()
                        {
                            GroupName = "Root",
                            GroupDesc = "启动节点",
                            // ActiveRuleName = "EnableGroup",
                            Collectors = new List<AssetBundleCollector>()
                            {
                                new AssetBundleCollector()
                                {
                                    CollectPath = rootBundlePath,
                                    CollectorGUID = $"{rootGuid}",
                                    // CollectorType = 0,
                                    // AddressRuleName = "AddressByFileName",
                                    // PackRuleName = "PackDirectory",
                                    // FilterRuleName =  "CollectAll"
                                }
                            }
                        }
                    }
                }
            };
            
            EU.SetDirty(so);
            AssetDatabase.SaveAssets();
            Debug.Log($"✅ 创建 YooAssets 配置成功");
        }
        
        /// <summary>
        /// 创建 Root 脚本和对象
        /// </summary>
        private void CreateRoot(Action callback = null)
        {
            var rootAssetPath = $"{_appBundlesPath}/Root/Root.prefab";
            if (File.Exists(rootAssetPath))
            {
                Debug.Log($"Root.prefab 已存在: {rootAssetPath}");
                callback?.Invoke();
                return;
            }
            
            var rootScriptPath = $"{_appScriptsPath}/Root.cs";
            if (!File.Exists(rootScriptPath))
            {
                var contents = RootScriptTemplate.Replace("$AppName", _appName);
                File.WriteAllText(rootScriptPath, contents);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            var ta = AssetDatabase.LoadAssetAtPath<TextAsset>(rootScriptPath);
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ta, out var guid, out var localId))
            {
                var prefabStr = RootPrefabTemplate.Replace("$fileID", $"{localId}").Replace("$guid", guid);
                File.WriteAllText(rootAssetPath, prefabStr);
            }

            
            // 刷新资源数据库，触发脚本编译
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void CreateUILayers()
        {
            try
            {
                // 获取 TagManager - 正确的路径是 ProjectSettings/TagManager.asset
                var tagManagerPath = "ProjectSettings/TagManager.asset";
                
                // 检查文件是否存在
                var fullPath = Path.GetFullPath(tagManagerPath);
                if (!File.Exists(fullPath))
                {
                    Debug.LogError($"❌ TagManager.asset 文件不存在: {fullPath}");
                    Debug.LogWarning("⚠️ Unity 项目设置文件缺失，这通常不应该发生。请检查项目完整性。");
                    return;
                }
                
                // 加载 TagManager 资源
                var tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath(tagManagerPath);
                if (tagManagerAssets == null || tagManagerAssets.Length == 0)
                {
                    Debug.LogError($"❌ 无法加载 TagManager.asset: {tagManagerPath}");
                    return;
                }
                
                var tagManager = new SerializedObject(tagManagerAssets[0]);
                var sortingLayersProp = tagManager.FindProperty("m_SortingLayers");
                
                if (sortingLayersProp == null)
                {
                    Debug.LogError("❌ 无法找到 m_SortingLayers 属性");
                    return;
                }
                
                // 遍历所有 UILayer 枚举值并创建对应的 SortingLayer
                foreach (var layerName in Enum.GetNames(typeof(UILayer)))
                {
                    // 检查是否已存在该 SortingLayer
                    bool exists = false;
                    for (int i = 0; i < sortingLayersProp.arraySize; i++)
                    {
                        var layer = sortingLayersProp.GetArrayElementAtIndex(i);
                        var name = layer.FindPropertyRelative("name").stringValue;
                        if (name == layerName)
                        {
                            exists = true;
                            break;
                        }
                    }
                    
                    // 如果不存在，则创建
                    if (!exists)
                    {
                        sortingLayersProp.InsertArrayElementAtIndex(sortingLayersProp.arraySize);
                        var newLayer = sortingLayersProp.GetArrayElementAtIndex(sortingLayersProp.arraySize - 1);
                        newLayer.FindPropertyRelative("name").stringValue = layerName;
                        newLayer.FindPropertyRelative("uniqueID").intValue = layerName.GetHashCode();
                        Debug.Log($"✅ 创建 SortingLayer: {layerName}");
                    }
                    else
                    {
                        Debug.Log($"SortingLayer 已存在: {layerName}");
                    }
                }
                
                // 应用修改并保存
                tagManager.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();
                Debug.Log($"✅ UI 层级创建完成");
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ 创建 UI 层级时发生错误: {e.Message}\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// 创建启动场景 
        /// </summary>
        private void CreateLaunchScene()
        {
            var scenePath = $"{_appPath}/Scenes/Launch.unity";
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);

            // 创建 App
            var go = new GameObject("App");
            var app = go.AddComponent<App>();
            app.startUpAppName = _appName;

            // 创建 UIRoot
            CreateUIRoot();
            
            EditorSceneManager.SaveScene(scene, scenePath);
            Debug.Log($"✅ 创建启动场景成功");

            var scenes = EditorBuildSettings.scenes.ToList();
            foreach (var s in scenes)
            {
                if (s.path == scenePath)
                    return;
            }
            
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"✅ 添加启动场景到构建设置");
        }


        private void CreateUIRoot()
        {
            var uiRootName = "UIRoot";
            var rootObj = new GameObject(uiRootName);
            var canvas = rootObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            // var rootRect = uiRoot.GetComponent<RectTransform>();
            // rootRect.anchorMin = Vector2.zero;
            // rootRect.anchorMax = Vector2.one;
            rootObj.AddComponent<GraphicRaycaster>();
            rootObj.AddComponent<AudioListener>();
            var canvasScaler = rootObj.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.scaleFactor = 0;
            canvasScaler.referenceResolution = new Vector2(1080, 2340);
            
            var camObj = new GameObject("UICamera");
            var cam = camObj.AddComponent<Camera>();
            cam.transform.SetParent(rootObj.transform);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localScale = Vector3.one;
            cam.clearFlags = CameraClearFlags.Depth;
            cam.orthographic = true;
            cam.orthographicSize = 5;
            cam.cullingMask = LayerMask.GetMask("UI");
            cam.depth = 0;
            canvas.worldCamera = cam;
            
            var evtSystem = new GameObject("EventSystem");
            evtSystem.AddComponent<EventSystem>();
            evtSystem.AddComponent<StandaloneInputModule>();
            evtSystem.transform.SetParent(rootObj.transform);
            evtSystem.transform.localPosition = Vector3.zero;
            evtSystem.transform.localScale = Vector3.one;
            
            var nodeObj = new GameObject("Root");
            nodeObj.transform.SetParent(rootObj.transform);
            nodeObj.transform.localScale = Vector3.one;
            var rect = nodeObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.localPosition = Vector2.zero;
            rect.pivot = Vector2.one * 0.5f;
            
            // Create prefab
            var dir = $"{_appBundlesPath}/UI";
            var assetPath = $"{dir}/{uiRootName}.prefab";
            EnsurePathExists(dir);
            var prefab = PrefabUtility.SaveAsPrefabAsset(rootObj, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            UnityEngine.Object.DestroyImmediate(rootObj);
            
            var go = PrefabUtility.InstantiatePrefab(prefab);
            go.name = uiRootName;
        }



        /// <summary>
        /// 构建内置的 bundle
        /// </summary>
        private void CallYooAssetBuild()
        {
            var bundleBuilder = new ScriptableBuildPipeline();
            // string packageVersion = DateTime.UtcNow.ToString("yyyy-MM-dd-hh-mm-ss");
            string packageVersion = "";
            var buildParameters = new ScriptableBuildParameters()
            {
                CompressOption = ECompressOption.LZ4,
                BuildOutputRoot = AssetBundleBuilderHelper.GetDefaultBuildOutputRoot(),
                BuildinFileRoot = AssetBundleBuilderHelper.GetStreamingAssetsRoot(),
                BuildTarget = BuildTarget.Android,
                BuildMode = EBuildMode.IncrementalBuild,
                PackageName = DefaultPackageName,
                PackageVersion = packageVersion,
                FileNameStyle = EFileNameStyle.HashName,
                BuildinFileCopyOption = EBuildinFileCopyOption.ClearAndCopyAll,
                EncryptionServices = new EncryptoHTXOR()
            };
            bundleBuilder.Run(buildParameters, false);
            Debug.Log($"✅ 构建内置的 bundle 成功");
        }
        
        private void OnDelayNotification()
        {
            if (EU.DisplayDialog($"App 启动成功", $"[{_appName}] 项目经成功的创建！\n\n第一步已完成，启动一切正常！\n\n=== 可以预览项目了===", "太棒了！"))
            {
                EditorApplication.isPlaying = true;
            }
        }
        
    }
    
    #endregion
    
    #region SO Modifer

    internal static class ScriptableObjectModifier
    {
        
        /// <summary>
        /// 设置序列化属性的值
        /// </summary>
        private static bool SetSerializedPropertyValue(SerializedProperty property, object value)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    property.intValue = System.Convert.ToInt32(value);
                    return true;
                    
                case SerializedPropertyType.Boolean:
                    property.boolValue = System.Convert.ToBoolean(value);
                    return true;
                    
                case SerializedPropertyType.Float:
                    property.floatValue = System.Convert.ToSingle(value);
                    return true;
                    
                case SerializedPropertyType.String:
                    property.stringValue = value?.ToString() ?? "";
                    return true;
                    
                case SerializedPropertyType.Color:
                    if (value is Color color)
                    {
                        property.colorValue = color;
                        return true;
                    }
                    break;
                    
                case SerializedPropertyType.ObjectReference:
                    if (value is UnityEngine.Object objRef)
                    {
                        property.objectReferenceValue = objRef;
                        return true;
                    }
                    break;
                    
                case SerializedPropertyType.LayerMask:
                    property.intValue = System.Convert.ToInt32(value);
                    return true;
                    
                case SerializedPropertyType.Enum:
                    property.enumValueIndex = System.Convert.ToInt32(value);
                    return true;
                    
                case SerializedPropertyType.Vector2:
                    if (value is Vector2 vec2)
                    {
                        property.vector2Value = vec2;
                        return true;
                    }
                    break;
                    
                case SerializedPropertyType.Vector3:
                    if (value is Vector3 vec3)
                    {
                        property.vector3Value = vec3;
                        return true;
                    }
                    break;
                    
                case SerializedPropertyType.Vector4:
                    if (value is Vector4 vec4)
                    {
                        property.vector4Value = vec4;
                        return true;
                    }
                    break;
                    
                case SerializedPropertyType.Rect:
                    if (value is Rect rect)
                    {
                        property.rectValue = rect;
                        return true;
                    }
                    break;
                    
                case SerializedPropertyType.AnimationCurve:
                    if (value is AnimationCurve curve)
                    {
                        property.animationCurveValue = curve;
                        return true;
                    }
                    break;
                    
                case SerializedPropertyType.Bounds:
                    if (value is Bounds bounds)
                    {
                        property.boundsValue = bounds;
                        return true;
                    }
                    break;
                    
                case SerializedPropertyType.Quaternion:
                    if (value is Quaternion quat)
                    {
                        property.quaternionValue = quat;
                        return true;
                    }
                    break;
            }
            Debug.LogWarning($"不支持的属性类型: {property.propertyType}");
            return false;
        }
        /// <summary>
        /// 批量修改多个属性
        /// </summary>
        /// <param name="so">目标 ScriptableObject</param>
        /// <param name="propertyValues">属性名称和值的字典</param>
        /// <param name="saveAsset">是否立即保存资源</param>
        public static bool ModifyProperties(ScriptableObject so, Dictionary<string, object> propertyValues, bool saveAsset = true)
        {
            if (so == null || propertyValues == null || propertyValues.Count == 0)
            {
                return false;
            }
            // 记录撤销操作
            Undo.RecordObject(so, $"批量修改 {so.name} 的属性");
            var serializedObject = new SerializedObject(so);
            bool anySuccess = false;
            foreach (var kvp in propertyValues)
            {
                var property = serializedObject.FindProperty(kvp.Key);
                if (property != null && SetSerializedPropertyValue(property, kvp.Value))
                {
                    anySuccess = true;
                    Debug.Log($"✅ 修改 {so.name}.{kvp.Key} = {kvp.Value}");
                }
                else
                {
                    Debug.LogWarning($"⚠️ 无法修改 {so.name}.{kvp.Key}");
                }
            }
            if (anySuccess)
            {
                serializedObject.ApplyModifiedProperties();
                EU.SetDirty(so);
                
                if (saveAsset)
                {
                    AssetDatabase.SaveAssets();
                }
            }
            return anySuccess;
        }
        /// <summary>
        /// 使用反射直接修改字段（绕过序列化系统）
        /// </summary>
        /// <param name="so">目标 ScriptableObject</param>
        /// <param name="fieldName">字段名称</param>
        /// <param name="newValue">新值</param>
        /// <param name="saveAsset">是否立即保存资源</param>
        public static bool ModifyFieldDirect(ScriptableObject so, string fieldName, object newValue, bool saveAsset = true)
        {
            if (so == null)
            {
                Debug.LogError("ScriptableObject 为空");
                return false;
            }
            try
            {
                // 记录撤销操作
                Undo.RecordObject(so, $"直接修改 {so.name} 的 {fieldName}");
                var type = so.GetType();
                var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null)
                {
                    Debug.LogError($"未找到字段: {fieldName}");
                    return false;
                }
                // 类型转换
                object convertedValue = ConvertValue(newValue, field.FieldType);
                
                // 设置字段值
                field.SetValue(so, convertedValue);
                // 标记为脏数据
                EU.SetDirty(so);
                
                if (saveAsset)
                {
                    AssetDatabase.SaveAssets();
                }
                Debug.Log($"✅ 直接修改成功 {so.name}.{fieldName} = {newValue}");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"直接修改字段时发生错误: {ex.Message}");
                return false;
            }
        }
        /// <summary>
        /// 值类型转换
        /// </summary>
        private static object ConvertValue(object value, System.Type targetType)
        {
            if (value == null) return null;
            
            if (targetType.IsAssignableFrom(value.GetType()))
            {
                return value;
            }
            try
            {
                return System.Convert.ChangeType(value, targetType);
            }
            catch
            {
                Debug.LogWarning($"无法将 {value.GetType()} 转换为 {targetType}");
                return value;
            }
        }
        /// <summary>
        /// 获取 SO 的所有可修改属性信息
        /// </summary>
        /// <param name="so">目标 ScriptableObject</param>
        /// <returns>属性信息列表</returns>
        public static List<PropertyInfo> GetModifiableProperties(ScriptableObject so)
        {
            var properties = new List<PropertyInfo>();
            
            if (so == null) return properties;
            var serializedObject = new SerializedObject(so);
            var iterator = serializedObject.GetIterator();
            
            if (iterator.NextVisible(true))
            {
                do
                {
                    // 跳过脚本引用
                    if (iterator.propertyPath == "m_Script") continue;
                    
                    properties.Add(new PropertyInfo
                    {
                        Name = iterator.propertyPath,
                        DisplayName = iterator.displayName,
                        Type = iterator.propertyType,
                        CurrentValue = GetSerializedPropertyValue(iterator)
                    });
                } 
                while (iterator.NextVisible(false));
            }
            return properties;
        }
        /// <summary>
        /// 获取序列化属性的当前值
        /// </summary>
        private static object GetSerializedPropertyValue(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return property.intValue;
                case SerializedPropertyType.Boolean:
                    return property.boolValue;
                case SerializedPropertyType.Float:
                    return property.floatValue;
                case SerializedPropertyType.String:
                    return property.stringValue;
                case SerializedPropertyType.Color:
                    return property.colorValue;
                case SerializedPropertyType.ObjectReference:
                    return property.objectReferenceValue;
                case SerializedPropertyType.LayerMask:
                    return property.intValue;
                case SerializedPropertyType.Enum:
                    return property.enumValueIndex;
                case SerializedPropertyType.Vector2:
                    return property.vector2Value;
                case SerializedPropertyType.Vector3:
                    return property.vector3Value;
                case SerializedPropertyType.Vector4:
                    return property.vector4Value;
                case SerializedPropertyType.Rect:
                    return property.rectValue;
                case SerializedPropertyType.AnimationCurve:
                    return property.animationCurveValue;
                case SerializedPropertyType.Bounds:
                    return property.boundsValue;
                case SerializedPropertyType.Quaternion:
                    return property.quaternionValue;
                default:
                    return property.displayName;
            }
        }
        /// <summary>
        /// 属性信息类
        /// </summary>
        public class PropertyInfo
        {
            public string Name;
            public string DisplayName;
            public SerializedPropertyType Type;
            public object CurrentValue;
        }


    }
    

    #endregion
    
}