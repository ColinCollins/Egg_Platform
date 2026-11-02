using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Bear.Editor
{
    /// <summary>
    /// Skin 部件 prefab 生成工具：只在 Unity Editor 菜单中手动执行，
    /// 从指定 ArtSrc/Skins 子目录读取 Sprite 并生成同名 prefab。
    /// </summary>
    public static class SkinsPrefabGenerator
    {
        #region Constants

        private const string MenuPath = "Tools/Bear/Auto Generate Skins Prefabs";
        private const string DestinationFolder = "Assets/Game/Prefabs/Skins";

        private static readonly string[] SourceFolders =
        {
            "Assets/Game/ArtSrc/Skins/Back",
            "Assets/Game/ArtSrc/Skins/Clothes",
            "Assets/Game/ArtSrc/Skins/Face"
        };

        #endregion

        #region Menu Entry

        /// <summary>
        /// 从 Skin 部件 Sprite 批量生成 prefab。调用边界：仅允许在 Unity Editor 菜单或 batchmode 验证中执行，
        /// 不访问运行时 DB、存档、RemoteConfig、资源加载器或平台服务。
        /// </summary>
        [MenuItem(MenuPath, false, 100)]
        public static void AutoGenerateSkinsPrefabs()
        {
            var result = GeneratePrefabs();
            LogResult(result);
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Auto Generate Skins Prefabs", result.BuildDialogMessage(), "OK");
            }
        }

        #endregion

        #region Generation

        private static GenerationResult GeneratePrefabs()
        {
            var result = new GenerationResult();

            EnsureAssetFolder(DestinationFolder);

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (var sourceFolder in SourceFolders)
                {
                    GenerateFromFolder(sourceFolder, result);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            return result;
        }

        private static void GenerateFromFolder(string sourceFolder, GenerationResult result)
        {
            if (!AssetDatabase.IsValidFolder(sourceFolder))
            {
                result.Skipped.Add($"Folder missing: {sourceFolder}");
                return;
            }

            var spriteEntries = CollectSprites(sourceFolder);
            if (spriteEntries.Count == 0)
            {
                result.Skipped.Add($"No sprites: {sourceFolder}");
                return;
            }

            foreach (var entry in spriteEntries)
            {
                GeneratePrefab(entry, result);
            }
        }

        private static List<SpriteEntry> CollectSprites(string sourceFolder)
        {
            var assetPaths = AssetDatabase.FindAssets("t:Sprite", new[] { sourceFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();

            var sprites = new List<SpriteEntry>();
            foreach (var assetPath in assetPaths)
            {
                var loadedSprites = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                    .OfType<Sprite>()
                    .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
                    .ToList();

                if (loadedSprites.Count == 0)
                {
                    continue;
                }

                foreach (var sprite in loadedSprites)
                {
                    sprites.Add(new SpriteEntry(assetPath, sprite));
                }
            }

            return sprites
                .OrderBy(entry => entry.AssetPath, StringComparer.Ordinal)
                .ThenBy(entry => entry.Sprite.name, StringComparer.Ordinal)
                .ToList();
        }

        private static void GeneratePrefab(SpriteEntry entry, GenerationResult result)
        {
            if (entry.Sprite == null)
            {
                result.Skipped.Add($"Missing sprite: {entry.AssetPath}");
                return;
            }

            if (!TryGetPrefabPath(entry.Sprite.name, out var prefabPath, out var pathError))
            {
                result.Failed.Add($"{entry.AssetPath}: {pathError}");
                return;
            }

            var existed = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null;
            var root = CreatePrefabRoot(entry.Sprite);

            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out var success);
                if (!success)
                {
                    result.Failed.Add($"{entry.AssetPath}: failed to save prefab {prefabPath}");
                    return;
                }

                if (existed)
                {
                    result.Updated.Add(prefabPath);
                }
                else
                {
                    result.Created.Add(prefabPath);
                }
            }
            catch (Exception ex)
            {
                result.Failed.Add($"{entry.AssetPath}: {ex.Message}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreatePrefabRoot(Sprite sprite)
        {
            var prefabName = sprite.name;
            var root = new GameObject(prefabName);
            ResetTransform(root.transform);

            var spriteNode = new GameObject(prefabName);
            spriteNode.transform.SetParent(root.transform, false);
            ResetTransform(spriteNode.transform);

            var spriteRenderer = spriteNode.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.size = sprite.bounds.size;

            return root;
        }

        #endregion

        #region Asset Paths

        private static void EnsureAssetFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder))
            {
                return;
            }

            var parts = assetFolder.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static bool TryGetPrefabPath(string spriteName, out string prefabPath, out string error)
        {
            prefabPath = string.Empty;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(spriteName))
            {
                error = "sprite name is empty";
                return false;
            }

            if (spriteName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                error = $"sprite name contains invalid file name characters: {spriteName}";
                return false;
            }

            prefabPath = $"{DestinationFolder}/{spriteName}.prefab";
            return true;
        }

        #endregion

        #region Utility

        private static void ResetTransform(Transform target)
        {
            target.localPosition = Vector3.zero;
            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one;
        }

        private static void LogResult(GenerationResult result)
        {
            var builder = new StringBuilder();
            builder.AppendLine("[SkinsPrefabGenerator] Auto Generate Skins Prefabs finished.");
            builder.AppendLine($"Created: {result.Created.Count}");
            builder.AppendLine($"Updated: {result.Updated.Count}");
            builder.AppendLine($"Skipped: {result.Skipped.Count}");
            builder.AppendLine($"Failed: {result.Failed.Count}");

            AppendPaths(builder, "Created", result.Created);
            AppendPaths(builder, "Updated", result.Updated);
            AppendPaths(builder, "Skipped", result.Skipped);
            AppendPaths(builder, "Failed", result.Failed);

            if (result.Failed.Count > 0)
            {
                Debug.LogError(builder.ToString());
            }
            else
            {
                Debug.Log(builder.ToString());
            }
        }

        private static void AppendPaths(StringBuilder builder, string title, IReadOnlyList<string> paths)
        {
            if (paths.Count == 0)
            {
                return;
            }

            builder.AppendLine($"-- {title} --");
            foreach (var path in paths)
            {
                builder.AppendLine(path);
            }
        }

        #endregion

        #region Types

        private readonly struct SpriteEntry
        {
            public readonly string AssetPath;
            public readonly Sprite Sprite;

            public SpriteEntry(string assetPath, Sprite sprite)
            {
                AssetPath = assetPath;
                Sprite = sprite;
            }
        }

        private sealed class GenerationResult
        {
            public readonly List<string> Created = new List<string>();
            public readonly List<string> Updated = new List<string>();
            public readonly List<string> Skipped = new List<string>();
            public readonly List<string> Failed = new List<string>();

            public string BuildDialogMessage()
            {
                return $"生成完成\nCreated: {Created.Count}\nUpdated: {Updated.Count}\nSkipped: {Skipped.Count}\nFailed: {Failed.Count}\n\n完整列表见 Console。";
            }
        }

        #endregion
    }
}
