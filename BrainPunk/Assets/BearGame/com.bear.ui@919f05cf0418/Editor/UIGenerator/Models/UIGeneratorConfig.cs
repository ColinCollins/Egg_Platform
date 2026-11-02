using UnityEngine;
#if TextMeshPro
using TMPro;
#endif

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
        [Tooltip("Image 找不到资源时，在此目录下遍历所有 Sprite 按名称匹配")]
        public string DefaultSpriteFolder = "";

        [Header("Default Settings")]
#if TextMeshPro
        public TMP_FontAsset DefaultFont;
#else
        public Font DefaultLegacyFont;
#endif
        public Color DefaultShapeColor = Color.white;
        public Color PlaceholderColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

        public float ScaleFactor => CanvasResolution.y / DesignResolution.y;
    }
}
