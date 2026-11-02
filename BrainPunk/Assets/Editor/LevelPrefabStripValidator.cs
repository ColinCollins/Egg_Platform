using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Scripting;

namespace Bear.Editor
{
    public static class LevelPrefabStripValidator
    {
        private const string LevelPrefabRoot = "Assets/Game/Prefabs/Level";
        private const string LinkXmlPath = "Assets/link.xml";
        private const string ReportTitle = "Level Prefab Strip Validator";

        [MenuItem("Tools/Bear/Validate Level Prefab Script Preserve")]
        public static void Validate()
        {
            if (!AssetDatabase.IsValidFolder(LevelPrefabRoot))
            {
                EditorUtility.DisplayDialog(ReportTitle, $"Prefab folder not found: {LevelPrefabRoot}", "OK");
                return;
            }

            var scriptUsages = CollectScriptUsages(LevelPrefabRoot);
            var preserveRules = LinkerRules.Load(LinkXmlPath);

            var risky = new List<RiskyScriptInfo>();
            foreach (var pair in scriptUsages)
            {
                var type = pair.Key;
                if (type == null)
                {
                    continue;
                }

                if (IsTypePreserved(type, preserveRules))
                {
                    continue;
                }

                risky.Add(new RiskyScriptInfo(type, pair.Value));
            }

            PrintReport(scriptUsages, risky, preserveRules);
        }

        private static Dictionary<Type, HashSet<string>> CollectScriptUsages(string rootFolder)
        {
            var usages = new Dictionary<Type, HashSet<string>>();
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { rootFolder });

            foreach (var prefabGuid in prefabGuids)
            {
                var prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (root == null)
                {
                    continue;
                }

                var transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (var transform in transforms)
                {
                    var components = transform.GetComponents<MonoBehaviour>();
                    foreach (var component in components)
                    {
                        if (component == null)
                        {
                            // Missing script reference already exists in prefab.
                            continue;
                        }

                        var type = component.GetType();
                        if (!usages.TryGetValue(type, out var paths))
                        {
                            paths = new HashSet<string>(StringComparer.Ordinal);
                            usages[type] = paths;
                        }

                        paths.Add(prefabPath);
                    }
                }
            }

            return usages;
        }

        private static bool IsTypePreserved(Type type, LinkerRules rules)
        {
            if (type == null)
            {
                return true;
            }

            // Not in Assembly-CSharp usually means external package/plugin.
            if (!string.Equals(type.Assembly.GetName().Name, "Assembly-CSharp", StringComparison.Ordinal))
            {
                return true;
            }

            if (Attribute.IsDefined(type, typeof(PreserveAttribute), false))
            {
                return true;
            }

            if (rules.IsTypePreserved(type))
            {
                return true;
            }

            return false;
        }

        private static void PrintReport(
            Dictionary<Type, HashSet<string>> scriptUsages,
            List<RiskyScriptInfo> risky,
            LinkerRules rules)
        {
            var totalTypes = scriptUsages.Keys.Count;
            var riskyCount = risky.Count;

            var sb = new StringBuilder();
            sb.AppendLine($"[{ReportTitle}] Prefab root: {LevelPrefabRoot}");
            sb.AppendLine($"[{ReportTitle}] link.xml: {(rules.IsLoaded ? LinkXmlPath : "missing or invalid")}");
            sb.AppendLine($"[{ReportTitle}] Used script types in prefabs: {totalTypes}");
            sb.AppendLine($"[{ReportTitle}] Risky script types: {riskyCount}");

            if (riskyCount > 0)
            {
                sb.AppendLine($"[{ReportTitle}] ---- Risky Types ----");
                foreach (var item in risky.OrderBy(x => x.Type.FullName, StringComparer.Ordinal))
                {
                    sb.AppendLine($"- {item.Type.FullName}");
                    foreach (var prefabPath in item.PrefabPaths.OrderBy(x => x, StringComparer.Ordinal).Take(3))
                    {
                        sb.AppendLine($"    {prefabPath}");
                    }

                    if (item.PrefabPaths.Count > 3)
                    {
                        sb.AppendLine($"    ... and {item.PrefabPaths.Count - 3} more");
                    }
                }
            }

            Debug.Log(sb.ToString());

            var dialogMessage = riskyCount == 0
                ? "Validation passed. No risky script types found."
                : $"Found {riskyCount} risky script types.\nSee Console for full report.";
            EditorUtility.DisplayDialog(ReportTitle, dialogMessage, "OK");
        }

        private sealed class RiskyScriptInfo
        {
            public readonly Type Type;
            public readonly List<string> PrefabPaths;

            public RiskyScriptInfo(Type type, IEnumerable<string> prefabPaths)
            {
                Type = type;
                PrefabPaths = prefabPaths.Distinct(StringComparer.Ordinal).ToList();
            }
        }

        private sealed class LinkerRules
        {
            public static readonly LinkerRules Empty = new LinkerRules(false);

            private readonly HashSet<string> preserveAllAssemblies = new HashSet<string>(StringComparer.Ordinal);
            private readonly Dictionary<string, List<string>> assemblyTypePatterns = new Dictionary<string, List<string>>(StringComparer.Ordinal);

            public bool IsLoaded { get; }

            private LinkerRules(bool isLoaded)
            {
                IsLoaded = isLoaded;
            }

            public static LinkerRules Load(string assetPath)
            {
                if (string.IsNullOrEmpty(assetPath))
                {
                    return Empty;
                }

                var fullPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), assetPath);
                if (!System.IO.File.Exists(fullPath))
                {
                    return Empty;
                }

                try
                {
                    var doc = XDocument.Load(fullPath);
                    var result = new LinkerRules(true);

                    var assemblies = doc.Root?.Elements("assembly");
                    if (assemblies == null)
                    {
                        return result;
                    }

                    foreach (var assemblyNode in assemblies)
                    {
                        var assemblyName = assemblyNode.Attribute("fullname")?.Value;
                        if (string.IsNullOrEmpty(assemblyName))
                        {
                            continue;
                        }

                        var preserveValue = assemblyNode.Attribute("preserve")?.Value;
                        if (string.Equals(preserveValue, "all", StringComparison.OrdinalIgnoreCase))
                        {
                            result.preserveAllAssemblies.Add(assemblyName);
                        }

                        foreach (var typeNode in assemblyNode.Elements("type"))
                        {
                            var typePattern = typeNode.Attribute("fullname")?.Value;
                            if (string.IsNullOrEmpty(typePattern))
                            {
                                continue;
                            }

                            if (!result.assemblyTypePatterns.TryGetValue(assemblyName, out var patterns))
                            {
                                patterns = new List<string>();
                                result.assemblyTypePatterns[assemblyName] = patterns;
                            }

                            patterns.Add(typePattern);
                        }
                    }

                    return result;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[{ReportTitle}] Failed to parse link.xml: {ex.Message}");
                    return Empty;
                }
            }

            public bool IsTypePreserved(Type type)
            {
                if (type == null)
                {
                    return false;
                }

                var assemblyName = type.Assembly.GetName().Name;
                if (string.IsNullOrEmpty(assemblyName))
                {
                    return false;
                }

                if (preserveAllAssemblies.Contains(assemblyName))
                {
                    return true;
                }

                if (!assemblyTypePatterns.TryGetValue(assemblyName, out var patterns))
                {
                    return false;
                }

                var fullName = type.FullName ?? type.Name;
                var shortName = type.Name;

                foreach (var pattern in patterns)
                {
                    if (IsPatternMatch(pattern, fullName, shortName))
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool IsPatternMatch(string pattern, string fullName, string shortName)
            {
                if (string.IsNullOrEmpty(pattern))
                {
                    return false;
                }

                if (string.Equals(pattern, fullName, StringComparison.Ordinal) ||
                    string.Equals(pattern, shortName, StringComparison.Ordinal))
                {
                    return true;
                }

                if (pattern.IndexOf('*') < 0)
                {
                    return false;
                }

                if (pattern == "*")
                {
                    return true;
                }

                if (pattern.EndsWith(".*", StringComparison.Ordinal))
                {
                    var prefix = pattern.Substring(0, pattern.Length - 1);
                    return fullName.StartsWith(prefix, StringComparison.Ordinal);
                }

                var patternParts = pattern.Split('*');
                var searchIndex = 0;
                foreach (var part in patternParts)
                {
                    if (part.Length == 0)
                    {
                        continue;
                    }

                    var foundIndex = fullName.IndexOf(part, searchIndex, StringComparison.Ordinal);
                    if (foundIndex < 0)
                    {
                        return false;
                    }

                    searchIndex = foundIndex + part.Length;
                }

                return true;
            }
        }
    }
}
