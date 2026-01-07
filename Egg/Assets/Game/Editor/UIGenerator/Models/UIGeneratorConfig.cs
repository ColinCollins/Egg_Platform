using UnityEngine;

namespace Game.Editor.UIGenerator
{
    [System.Serializable]
    public class UIGeneratorConfig
    {
        [Header("Canvas Settings")]
        public Vector2 CanvasResolution = new Vector2(1920, 1080);

        [Header("Design Resolution")]
        public Vector2 DesignResolution = new Vector2(1920, 1080);

        [Header("Resource Paths")]
        public string SpriteSearchPath = "Assets/Game/ArtSrc/Temp";

        [Header("Default Settings")]
        public TMPro.TMP_FontAsset DefaultFont;
        public Color DefaultShapeColor = Color.white;
        public Color PlaceholderColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

        public float ScaleFactor => CanvasResolution.y / DesignResolution.y;
    }
}

