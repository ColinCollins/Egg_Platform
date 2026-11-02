using UnityEditor;
using UnityEngine;

/// <summary>
/// 自动处理 Assets/Sprites/ 下导入的图片：TextureType=Sprite，SpriteMode=Single
/// </summary>
public class SpritesAutoImportProcessor : AssetPostprocessor
{
    private const string SpritesFolderName = "Sprites";

    void OnPreprocessTexture()
    {
        // Unity 的 assetPath 使用 '/' 分隔
        if (string.IsNullOrEmpty(assetPath))
            return;

        // 只考虑“导入文件的父文件夹名称”
        // 例如：
        // ✅ Assets/Sprites/a.png
        // ✅ Assets/UI/Sprites/a.png
        // ❌ Assets/Sprites/UI/a.png（父文件夹是 UI，不是 Sprites）
        int lastSlash = assetPath.LastIndexOf('/');
        if (lastSlash <= 0)
            return;

        int parentSlash = assetPath.LastIndexOf('/', lastSlash - 1);
        if (parentSlash < 0)
            return;

        string parentFolderName = assetPath.Substring(parentSlash + 1, lastSlash - parentSlash - 1);
        if (!string.Equals(parentFolderName, SpritesFolderName, System.StringComparison.OrdinalIgnoreCase))
            return;

        var importer = (TextureImporter)assetImporter;
        if (importer == null)
            return;

        // 只在需要时改，避免触发不必要的 reimport
        bool changed = false;

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }

        if (changed)
        {
            // 常见 2D 设置（可按需删）
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
        }
    }
}

