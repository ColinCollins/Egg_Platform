using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using MelenitasDev.SoundsGood.Domain;

namespace MelenitasDev.SoundsGood.Editor
{
    public class GetStartedWindow : EditorWindow
    {
        // ----- Serialized Fields
        [SerializeField] private VisualTreeAsset tree;

        // ----- Fields
        private Button openEnglishDocumentationButton;
        private Button openSpanishDocumentationButton;

        // ----- Unity Event
        void CreateGUI ()
        {
            tree.CloneTree(rootVisualElement);
            
            openEnglishDocumentationButton = rootVisualElement.Q<Button>("OpenEnglishDocumentationButton");
            openSpanishDocumentationButton = rootVisualElement.Q<Button>("OpenSpanishDocumentationButton");
            
            RegisterEvents();
        }
        
        // ----- Public Methods
        [MenuItem("Tools/Melenitas Dev/Sounds Good/Get Started!", false, 0)]
        public static void ShowWindow ()
        {
            var window = GetWindow(typeof(GetStartedWindow));
            window.titleContent = new GUIContent("Get Started!");
        }

        // ----- Private Methods
        private void RegisterEvents ()
        {
            openEnglishDocumentationButton.clicked += () => Application
                .OpenURL(AssetLocator.Instance.EnglishDocumentationUrl);
            openSpanishDocumentationButton.clicked += () => Application
                .OpenURL(AssetLocator.Instance.SpanishDocumentationUrl);
        }
    }
}