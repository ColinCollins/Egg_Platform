using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GF.Editor
{
    [CreateAssetMenu(fileName = "ScriptRulerSetting", menuName = "GF/Create ScriptRulerSetting")]
    public class ScriptRulerSetting: ScriptableObject
    {
        [Header("代码保存父路径")] [SerializeField]
        private string _codePath = "Assets/Game/Scripts/Runtime/UI";
        
        [Header("代码模版路径")] [SerializeField]
        private string _templateCodePath = "Assets/GF/Scripts/Editor/UI/AutoGenerateTool/UITemplate.txt";

        [Header("绑定代码命名空间")] [SerializeField]
        private string _namespace;

        [Header("子组件名称(不会往下继续遍历)")] [SerializeField]
        private string _widgetName = "m_item";

        public string CodePath => _codePath;

        public string TemplateCodePath => _templateCodePath;

        public string Namespace => _namespace;

        public string WidgetName => _widgetName;
        
        [SerializeField] private List<ScriptGenerateRuler> scriptGenerateRule = new()
        {
            new ("obj_", "GameObject"),
            new ("trans_", "Transform"),
            new ("rect_", "RectTransform"),
            new ("txt_", "Text"),
            new ("btn_", "Button"),
            new ("img_", "Image"),
            new ("raw_", "RawImage"),
            new ("scr_", "ScrollRect"),
            new ("sld_", "Slider"),
            new ("cg_", "CanvasGroup"),
        };

        public List<ScriptGenerateRuler> ScriptGenerateRule => scriptGenerateRule;
    }
    
    [Serializable]
    public class ScriptGenerateRuler
    {
        public string uiElementRegex;
        public string componentName;

        public ScriptGenerateRuler(string uiElementRegex, string componentName)
        {
            this.uiElementRegex = uiElementRegex;
            this.componentName = componentName;
        }
    }

    [CustomPropertyDrawer(typeof(ScriptGenerateRuler))]
    public class ScriptGenerateRulerDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            var uiElementRegexRect = new Rect(position.x, position.y, 120, position.height);
            var componentNameRect = new Rect(position.x + 125, position.y, 150, position.height);
            EditorGUI.PropertyField(uiElementRegexRect, property.FindPropertyRelative("uiElementRegex"), GUIContent.none);
            EditorGUI.PropertyField(componentNameRect, property.FindPropertyRelative("componentName"), GUIContent.none);
            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }
}