using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MelenitasDev.SoundsGood.Editor
{
    public class ImagePopupWindow : EditorWindow
    {
        private Texture2D imageToDisplay;

        void CreateGUI ()
        {
            var image = new VisualElement();
            image.style.backgroundImage = new StyleBackground(imageToDisplay);
            image.style.width = imageToDisplay.width * 1.5f;
            image.style.height = imageToDisplay.height * 1.5f;
            rootVisualElement.Add(image);
        }

        public static void Show (Texture2D texture)
        {
            var window = CreateInstance<ImagePopupWindow>();
            window.imageToDisplay = texture;
            window.titleContent = new GUIContent("Preview");
            window.minSize = new Vector2(texture.width * 1.5f, texture.height * 1.5f);
            window.maxSize = new Vector2(texture.width * 1.5f, texture.height * 1.5f);
            window.ShowUtility();
        }
    }
}