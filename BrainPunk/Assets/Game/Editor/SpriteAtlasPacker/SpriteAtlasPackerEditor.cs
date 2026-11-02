#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace Game.Editor.SpriteAtlasTools
{
    public static class SpriteAtlasPackerEditor
    {
        [MenuItem("Tools/SpriteAtlas/Build From Selected Config")]
        private static void BuildFromSelectedConfig()
        {
            var config = Selection.activeObject as SpriteAtlasPackerConfig;
            if (config == null)
            {
                EditorUtility.DisplayDialog(
                    "SpriteAtlas Packer",
                    "请选择一个 SpriteAtlasPackerConfig 资产再执行此命令。",
                    "OK");
                return;
            }

            BuildAtlas(config);
        }

        public static void BuildAtlas(SpriteAtlasPackerConfig config)
        {
            if (config == null)
            {
                return;
            }

            if (config.atlas == null)
            {
                Debug.LogError("[SpriteAtlasPacker] atlas 为空，请先指定目标 SpriteAtlas。");
                return;
            }

            SpriteAtlas atlas = config.atlas;
            string atlasPath = AssetDatabase.GetAssetPath(atlas);
            if (string.IsNullOrEmpty(atlasPath))
            {
                Debug.LogError("[SpriteAtlasPacker] 无法获取 SpriteAtlas 的资源路径。");
                return;
            }

            bool isV2Atlas = atlasPath.EndsWith(".spriteatlasv2");

            if (isV2Atlas)
            {
                var atlasAsset = SpriteAtlasAsset.Load(atlasPath);
                if (atlasAsset == null)
                {
                    Debug.LogError($"[SpriteAtlasPacker] SpriteAtlasAsset.Load 失败: {atlasPath}");
                    return;
                }

                var existingPackables = atlas.GetPackables();
                if (existingPackables != null && existingPackables.Length > 0)
                {
                    atlasAsset.Remove(existingPackables);
                }

                var importer = AssetImporter.GetAtPath(atlasPath) as SpriteAtlasImporter;
                if (importer != null)
                {
                    var packingSettings = importer.packingSettings;
                    packingSettings.enableTightPacking = config.tightPacking;
                    packingSettings.enableRotation = config.rotation;
                    importer.packingSettings = packingSettings;

                    var textureSettings = importer.textureSettings;
                    textureSettings.readable = false;
                    textureSettings.generateMipMaps = false;
                    importer.textureSettings = textureSettings;

                    importer.includeInBuild = config.includeInBuild;

                    var platformSettings = importer.GetPlatformSettings(string.Empty);
                    platformSettings.overridden = true;
                    platformSettings.maxTextureSize = config.maxTextureSize;
                    importer.SetPlatformSettings(platformSettings);
                }

                var spritesToPack = CollectSprites(config);
                if (spritesToPack.Count == 0)
                {
                    Debug.LogWarning("[SpriteAtlasPacker] 没有找到任何符合条件的 Sprite，将不会修改 Objects for Packing。");
                }

                atlasAsset.Add(spritesToPack.ToArray());

                SpriteAtlasAsset.Save(atlasAsset, atlasPath);

                if (AssetImporter.GetAtPath(atlasPath) is SpriteAtlasImporter importerAfterSave)
                {
                    importerAfterSave.SaveAndReimport();
                }

                SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget);

                Debug.Log($"[SpriteAtlasPacker] Build 完成 -> {atlasPath} (sprites: {spritesToPack.Count})");
                return;
            }

            atlas.Remove(atlas.GetPackables());

            var packSettings = atlas.GetPackingSettings();
            packSettings.enableTightPacking = config.tightPacking;
            packSettings.enableRotation = config.rotation;
            atlas.SetPackingSettings(packSettings);

            var texSettings = atlas.GetTextureSettings();
            texSettings.readable = false;
            texSettings.generateMipMaps = false;
            atlas.SetTextureSettings(texSettings);

            atlas.SetIncludeInBuild(config.includeInBuild);

            var legacyPlatformSettings = new TextureImporterPlatformSettings
            {
                name = string.Empty,
                overridden = true,
                maxTextureSize = config.maxTextureSize
            };
            atlas.SetPlatformSettings(legacyPlatformSettings);

            var spritesToPackLegacy = CollectSprites(config);
            if (spritesToPackLegacy.Count == 0)
            {
                Debug.LogWarning("[SpriteAtlasPacker] 没有找到任何符合条件的 Sprite，将不会修改 Objects for Packing。");
            }

            atlas.Add(spritesToPackLegacy.ToArray());

            EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget);

            Debug.Log($"[SpriteAtlasPacker] Build 完成 -> {atlasPath} (sprites: {spritesToPackLegacy.Count})");
        }

        private static List<Object> CollectSprites(SpriteAtlasPackerConfig config)
        {
            var spritesToPack = new List<Object>();

            if (config.spriteFolders == null || config.spriteFolders.Count == 0)
            {
                Debug.LogWarning("[SpriteAtlasPacker] spriteFolders 为空，请在配置里至少指定一个 Folder。");
                return spritesToPack;
            }

            for (int i = 0; i < config.spriteFolders.Count; i++)
            {
                var folderObj = config.spriteFolders[i];
                if (folderObj == null)
                {
                    continue;
                }

                string folderPath = AssetDatabase.GetAssetPath(folderObj);
                if (!AssetDatabase.IsValidFolder(folderPath))
                {
                    Debug.LogWarning($"[SpriteAtlasPacker] skip: not a folder -> {folderPath}");
                    continue;
                }

                Debug.Log($"[SpriteAtlasPacker] 扫描目录: {folderPath}");

                var guids = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });
                Debug.Log($"[SpriteAtlasPacker] 在目录 {folderPath} 中找到 Sprite 数量: {guids.Length}");

                int filteredBySize = 0;
                foreach (var guid in guids)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                    if (sprite == null)
                    {
                        continue;
                    }

                    if (config.enableSizeFilter)
                    {
                        float w = sprite.rect.width;
                        float h = sprite.rect.height;
                        if (w > config.maxSpriteWidth || h > config.maxSpriteHeight)
                        {
                            filteredBySize++;
                            Debug.LogWarning($"[SpriteAtlasPacker] oversize skip: {assetPath} ({w}x{h})");
                            continue;
                        }
                    }

                    spritesToPack.Add(sprite);
                }

                if (config.enableSizeFilter)
                {
                    Debug.Log($"[SpriteAtlasPacker] 目录 {folderPath} 中，因尺寸过滤被跳过的数量: {filteredBySize}");
                }
            }

            return spritesToPack;
        }

        [MenuItem("Tools/SpriteAtlas/Clean Unused Sprites In SceneDecoration")]
        private static void CleanUnusedSpritesInSceneDecoration()
        {
            const string targetFolder = "Assets/Game/ArtSrc/Sprites/SceneDecoration";

            if (!AssetDatabase.IsValidFolder(targetFolder))
            {
                Debug.LogError($"[SpriteCleanup] 目标文件夹不存在: {targetFolder}");
                return;
            }

            var spriteGuids = AssetDatabase.FindAssets("t:Sprite", new[] { targetFolder });
            if (spriteGuids == null || spriteGuids.Length == 0)
            {
                Debug.Log($"[SpriteCleanup] 目标文件夹中没有找到任何 Sprite: {targetFolder}");
                return;
            }

            var spritePaths = new HashSet<string>();
            foreach (var guid in spriteGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                {
                    spritePaths.Add(path);
                }
            }

            var referencedSprites = new HashSet<string>();
            var allAssetPaths = AssetDatabase.GetAllAssetPaths();

            try
            {
                for (int i = 0; i < allAssetPaths.Length; i++)
                {
                    var path = allAssetPaths[i];
                    if (!path.StartsWith("Assets/"))
                    {
                        continue;
                    }

                    EditorUtility.DisplayProgressBar(
                        "清理未引用的 Sprites",
                        $"扫描依赖: {path} ({i + 1}/{allAssetPaths.Length})",
                        (float)(i + 1) / allAssetPaths.Length);

                    var dependencies = AssetDatabase.GetDependencies(path, true);
                    if (dependencies == null || dependencies.Length == 0)
                    {
                        continue;
                    }

                    foreach (var dep in dependencies)
                    {
                        if (spritePaths.Contains(dep) && dep != path)
                        {
                            referencedSprites.Add(dep);
                        }
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            var unusedSprites = spritePaths.Except(referencedSprites).ToList();
            if (unusedSprites.Count == 0)
            {
                Debug.Log($"[SpriteCleanup] {targetFolder} 中的 Sprites 均有引用，未删除任何资源。");
                return;
            }

            foreach (var spritePath in unusedSprites)
            {
                Debug.Log($"[SpriteCleanup] 删除未引用 Sprite: {spritePath}");
                AssetDatabase.DeleteAsset(spritePath);
            }

            AssetDatabase.Refresh();

            Debug.Log($"[SpriteCleanup] 目标文件夹: {targetFolder}，总 Sprite 数量: {spritePaths.Count}，" +
                      $"被引用的数量: {referencedSprites.Count}，已删除未引用的数量: {unusedSprites.Count}");
        }

        [CustomEditor(typeof(SpriteAtlasPackerConfig))]
        private class SpriteAtlasPackerConfigEditor : UnityEditor.Editor
        {
            public override void OnInspectorGUI()
            {
                base.OnInspectorGUI();

                var config = (SpriteAtlasPackerConfig)target;

                EditorGUILayout.Space();
                if (GUILayout.Button("应用符合条件的 Sprites 到 Atlas"))
                {
                    SpriteAtlasPackerEditor.BuildAtlas(config);
                }
            }
        }
    }
}
#endif
