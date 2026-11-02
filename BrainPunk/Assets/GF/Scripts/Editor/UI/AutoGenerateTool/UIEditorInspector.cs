using System;
using System.IO;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GF.Editor
{
    [CustomEditor(typeof(CanvasRenderer))]
    public class UIEditorInspector: UnityEditor.Editor
    {
        private CanvasRenderer _canvasRenderer;
        private bool _isUI = false;
        // private UIType _uiType;
        private UILayer _uiLayer;
        private string _path;
        private bool _hideWhenRemove;
        private ScriptRulerSetting _rulerSetting;
        private const string Gap = "/";
        private GUIStyle _titleStyle;
        private string _uiName;
        private string _scriptPath;

        private const string UI_LAYER_KEY = "UI_LAYER_KEY";
        private const string UI_PATH_KEY = "UI_PATH_KEY";
        private const string UI_SCRIPT_PATH_KEY = "UI_SCRIPT_PATH_KEY";
        private const string UI_HIDE_KEY = "UI_HIDE_KEY";
        private void OnEnable()
        {
            _titleStyle = new GUIStyle ();
            _titleStyle.fontSize = 20;
            _titleStyle.fontStyle = FontStyle.Bold;
            _titleStyle.alignment = TextAnchor.MiddleCenter;
            _titleStyle.normal.textColor = Color.red;
            // _rulerSetting =
            //     AssetDatabase.LoadAssetAtPath<ScriptRulerSetting>(
            //         "Assets/Editor/ScriptRulerSetting.asset");
            _rulerSetting = AssetDatabase.FindAssets("t:ScriptRulerSetting").Select(x =>
            {
                string path = AssetDatabase.GUIDToAssetPath(x);
                return AssetDatabase.LoadAssetAtPath<ScriptRulerSetting>(path);
            }).FirstOrDefault();
            _canvasRenderer = target as CanvasRenderer;
            _isUI = ShowRule();
            _uiName = _canvasRenderer.gameObject.name;
            // _uiType = (UIType) EditorPrefs.GetInt(UI_TYPE_KEY + _uiName, 0);
            _uiLayer = (UILayer) EditorPrefs.GetInt(UI_LAYER_KEY + _uiName, 0);
            _path = EditorPrefs.GetString(UI_PATH_KEY + _uiName, "");
            _scriptPath = GetScriptPath();
            _hideWhenRemove = EditorPrefs.GetBool(UI_HIDE_KEY + _uiName, false);
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (!_isUI)
            {
                return;
            }
            EditorGUILayout.BeginVertical();
            
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("UI脚本生成器",_titleStyle);
            EditorGUILayout.Space();
            EditorGUILayout.Space();
            
            EditorGUI.BeginChangeCheck();
            // _uiType = (UIType) EditorGUILayout.EnumPopup("UI类型", _uiType);
            _uiLayer = (UILayer)EditorGUILayout.EnumPopup("UI层级", _uiLayer);
            EditorGUILayout.BeginHorizontal();
            _path = EditorGUILayout.TextField("资源路径", _path);
            bool generatePath = GUILayout.Button("生成路径", GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();
            _scriptPath = EditorGUILayout.TextField("脚本路径", _scriptPath);
            _hideWhenRemove = EditorGUILayout.Toggle("关闭时只需隐藏", _hideWhenRemove);
            if (EditorGUI.EndChangeCheck())
            {
                // EditorPrefs.SetInt(UI_TYPE_KEY + _uiName, (int) _uiType);
                EditorPrefs.SetInt(UI_LAYER_KEY + _uiName, (int) _uiLayer);
                EditorPrefs.SetString(UI_PATH_KEY + _uiName, _path);
                EditorPrefs.SetString(UI_SCRIPT_PATH_KEY + _uiName, _scriptPath);
                EditorPrefs.SetBool(UI_HIDE_KEY + _uiName, _hideWhenRemove);
            }

            Color originColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.green;
            bool generateUIScript = GUILayout.Button("生成代码");
            bool generateVariable = GUILayout.Button("刷新UI变量");
            
            GUI.backgroundColor = originColor;
            
            EditorGUILayout.EndVertical();

            if (generateUIScript)
            {
                GenerateUIScript();
            }

            if (generateVariable)
            {
                GenerateVariable();
            }
            
            if (generatePath)
            {
                string prefabPath = GetPrefabPath(_canvasRenderer.transform.parent.gameObject);
                LogKit.I($"prefabPath = {prefabPath}");
                _path = prefabPath;
                // 保存路径
                EditorPrefs.SetString(UI_PATH_KEY + _uiName, _path);
            }
        }

        //显示生成按钮的规则
        private bool ShowRule()
        {
            //layer为UI
            if (_canvasRenderer.gameObject.layer != LayerMask.NameToLayer("UI"))
            {
                return false;
            }

            string objName = _canvasRenderer.gameObject.name;
            //判断objName以View/Popup结尾
            if (objName.EndsWith("View") || objName.EndsWith("Popup"))
            {
                return true;
            }

            return false;
        }

        private string GetScriptPath()
        {
            string path = EditorPrefs.GetString(UI_SCRIPT_PATH_KEY + _uiName, "");
            if (string.IsNullOrEmpty(path))
            {
                path = $"{_rulerSetting.CodePath}/{_uiName}/{_uiName}.cs";
            }

            return path;
        }

        private void GenerateUIScript()
        {
            string dataPath = Application.dataPath;
            dataPath = dataPath.Substring(0, dataPath.LastIndexOf("Assets", StringComparison.Ordinal));
            string path = Path.Combine(dataPath, _scriptPath);
            if (File.Exists(path))
            {
                UnityEditor.EditorUtility.DisplayDialog("提示", "文件已存在", "确定");
                return;
            }
            
            TextAsset ta = AssetDatabase.LoadAssetAtPath<TextAsset>(_rulerSetting.TemplateCodePath);
            StringBuilder sb = new StringBuilder(ta.ToString());
            sb.Replace("{UI_NAMESPACE}", _rulerSetting.Namespace);
            sb.Replace("{UI_LAYER}",$"UILayer.{_uiLayer}");
            sb.Replace("{UI_PATH}",_path);
            sb.Replace("{UI_HIDE}",_hideWhenRemove.ToString().ToLower());
            sb.Replace("{UI_NAME}", _uiName);
            // sb.Replace("{UI_PARENT}", _uiType.ToString());
            
            Utility.Files.WriteStringByFile(path, sb.ToString());
            GenerateVariable();
            AssetDatabase.Refresh();
        }

        private void GenerateVariable()
        {
            string dataPath = Application.dataPath;
            dataPath = dataPath.Substring(0, dataPath.LastIndexOf("Assets", StringComparison.Ordinal));
            string path = Path.Combine(dataPath, _scriptPath);
            if (!File.Exists(path))
            {
                UnityEditor.EditorUtility.DisplayDialog("提示", "请先创建UI代码文件", "确定");
                return;
            }

            string codeStr = Utility.Files.ReadStringByFile(path);
            
            int startIndex = codeStr.IndexOf("#region 自动生成代码，勿修改", StringComparison.Ordinal);
            int endIndex = codeStr.IndexOf("#endregion", StringComparison.Ordinal);

            if(startIndex == -1 || endIndex == -1)
            {
                LogKit.E("未找到代码区域");
                UnityEditor.EditorUtility.DisplayDialog("提示", "未找到代码区域", "确定");
                return;
            }
            
            string generateCode = codeStr.Substring(startIndex, endIndex - startIndex);
            StringBuilder sbVar = new StringBuilder();
            StringBuilder sbBind = new StringBuilder();
            Transform root = _canvasRenderer.transform;
            Ergodic(root, root, ref sbVar, ref sbBind);
            
            // 脚本工具生成的代码
            StringBuilder sb = new StringBuilder();
            sb.Append("#region 自动生成代码，勿修改\n\n");
            sb.Append(sbVar);
            sb.Append("\n");
            sb.Append("\t\tprotected override void ScriptGenerator()\n");
            sb.Append("\t\t{\n");
            sb.Append(sbBind);
            sb.Append("\t\t}\n\n\t\t");
            codeStr = codeStr.Replace(generateCode, sb.ToString());
            
            Utility.Files.WriteStringByFile(path, codeStr);
            AssetDatabase.Refresh();
        }
        
        private void Ergodic(Transform root, Transform transform, ref StringBuilder strVar, ref StringBuilder strBind)
        {
            for (int i = 0; i < transform.childCount; ++i)
            {
                Transform child = transform.GetChild(i);
                WriteScript(root, child, ref strVar, ref strBind);
                if (child.name.StartsWith(_rulerSetting.WidgetName))
                {
                    // 子 Item 不再往下遍历
                    continue;
                }

                Ergodic(root, child, ref strVar, ref strBind);
            }
        }
        
        private void WriteScript(Transform root, Transform child, ref StringBuilder strVar, ref StringBuilder strBind)
        {
            string varName = child.name;
            string componentName = string.Empty;

            var rule = _rulerSetting.ScriptGenerateRule.Find(t => varName.StartsWith(t.uiElementRegex));

            if (rule != null)
            {
                componentName = rule.componentName;
            }

            if (componentName == string.Empty)
            {
                return;
            }

            string varPath = GetRelativePath(child, root);
            if (!string.IsNullOrEmpty(varName))
            {
                strVar.Append("\t\tprivate " + componentName + " " + varName + ";\n");
                switch (componentName)
                {
                    case "Transform":
                        strBind.Append($"\t\t\t{varName} = FindChild(\"{varPath}\");\n");
                        break;
                    case "GameObject":
                        strBind.Append($"\t\t\t{varName} = FindChild(\"{varPath}\").gameObject;\n");
                        break;
                    default:
                        strBind.Append($"\t\t\t{varName} = FindChildComponent<{componentName}>(\"{varPath}\");\n");
                        break;
                }
            }
        }
        
        private string GetRelativePath(Transform child, Transform root)
        {
            StringBuilder path = new StringBuilder();
            path.Append(child.name);
            while (child.parent != null && child.parent != root)
            {
                child = child.parent;
                path.Insert(0, Gap);
                path.Insert(0, child.name);
            }

            return path.ToString();
        }
        
        /// <summary>
        /// 获取组件的预制体路径
        /// </summary>
        /// <param name="gameObject"></param>
        /// <returns></returns>
        public string GetPrefabPath(GameObject gameObject)
        {
#if UNITY_EDITOR
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                string prefabPath = prefabStage.prefabAssetPath;

                Debug.Log($"当前打开的预制体路径为：{prefabPath}");
                return prefabPath;
            }
#endif
            // 不是预制体实例
            return null;
        }
    }
}