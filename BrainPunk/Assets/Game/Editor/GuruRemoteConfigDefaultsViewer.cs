#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public class GuruRemoteConfigDefaultsViewer : EditorWindow
    {
        private const string MenuPath = "Tools/Guru/RemoteConfig Default Values Viewer";
        private const string GuruAllSpecsAssetPath = "Assets/Guru/Gen/GuruAllSpecs.G.cs";

        private static readonly Regex DefaultValuesBlockRegex = new Regex(
            @"remoteConfigSpec\s*:\s*new\s+RemoteConfigSpec\s*\(\s*defaultValues\s*:\s*new\s+Dictionary<string,\s*object>\s*\(\)\s*\{(?<body>.*?)\}\s*,\s*convertedKeys\s*:",
            RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex EntryRegex = new Regex(
            @"\[\s*""(?<key>[^""]+)""\s*\]\s*=\s*""(?<value>(?:\\.|[^""\\])*)""\s*,?",
            RegexOptions.Compiled);

        private readonly List<RemoteConfigEntry> entries = new List<RemoteConfigEntry>();
        private readonly Dictionary<string, bool> foldoutStates = new Dictionary<string, bool>();

        private Vector2 scrollPosition;
        private string searchText = string.Empty;
        private string sourceFileAbsolutePath;
        private string loadError;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            GetWindow<GuruRemoteConfigDefaultsViewer>("Guru RC Defaults");
        }

        private void OnEnable()
        {
            sourceFileAbsolutePath = GetGuruAllSpecsAbsolutePath();
            ReloadEntries();
        }

        private void OnGUI()
        {
            DrawToolbar();

            if (!string.IsNullOrEmpty(loadError))
            {
                EditorGUILayout.HelpBox(loadError, MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField($"Source: {GuruAllSpecsAssetPath}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"Entries: {entries.Count}", EditorStyles.miniLabel);
            EditorGUILayout.Space(6f);

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            foreach (RemoteConfigEntry entry in entries)
            {
                if (!IsMatch(entry))
                {
                    continue;
                }

                DrawEntry(entry);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Search", GUILayout.Width(42f));
                string newSearchText = GUILayout.TextField(searchText, EditorStyles.toolbarTextField);
                if (newSearchText != searchText)
                {
                    searchText = newSearchText;
                }

                if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                {
                    ReloadEntries();
                    GUI.FocusControl(null);
                }

                if (GUILayout.Button("Ping Source", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                {
                    UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(GuruAllSpecsAssetPath);
                    if (asset != null)
                    {
                        EditorGUIUtility.PingObject(asset);
                        Selection.activeObject = asset;
                    }
                }
            }
        }

        private void ReloadEntries()
        {
            entries.Clear();
            loadError = null;

            if (string.IsNullOrEmpty(sourceFileAbsolutePath))
            {
                loadError = $"Cannot resolve source file path: {GuruAllSpecsAssetPath}";
                return;
            }

            if (!File.Exists(sourceFileAbsolutePath))
            {
                loadError = $"Source file not found:\n{sourceFileAbsolutePath}";
                return;
            }

            string sourceText;
            try
            {
                sourceText = File.ReadAllText(sourceFileAbsolutePath);
            }
            catch (Exception e)
            {
                loadError = $"Failed to read source file:\n{e.Message}";
                return;
            }

            Match blockMatch = DefaultValuesBlockRegex.Match(sourceText);
            if (!blockMatch.Success)
            {
                loadError = "Cannot find remoteConfigSpec.defaultValues block in GuruAllSpecs.G.cs";
                return;
            }

            string body = blockMatch.Groups["body"].Value;
            MatchCollection matches = EntryRegex.Matches(body);
            if (matches.Count == 0)
            {
                loadError = "No defaultValues entries were found in the parsed block.";
                return;
            }

            foreach (Match match in matches)
            {
                string key = match.Groups["key"].Value;
                string rawLiteral = match.Groups["value"].Value;
                string jsonText = Regex.Unescape(rawLiteral);

                var entry = new RemoteConfigEntry
                {
                    Key = key,
                    JsonText = jsonText
                };

                try
                {
                    entry.ParsedToken = JToken.Parse(jsonText);
                }
                catch (Exception e)
                {
                    entry.ParseError = e.Message;
                }

                entries.Add(entry);
                if (!foldoutStates.ContainsKey(key))
                {
                    foldoutStates[key] = false;
                }
            }
        }

        private bool IsMatch(RemoteConfigEntry entry)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return true;
            }

            return entry.Key.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0
                   || entry.JsonText.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawEntry(RemoteConfigEntry entry)
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                string title = string.IsNullOrEmpty(entry.ParseError)
                    ? entry.Key
                    : $"{entry.Key} (parse failed)";

                foldoutStates[entry.Key] = EditorGUILayout.Foldout(foldoutStates[entry.Key], title, true);
                if (!foldoutStates[entry.Key])
                {
                    return;
                }

                EditorGUILayout.TextField("Remote Key", entry.Key);

                if (!string.IsNullOrEmpty(entry.ParseError))
                {
                    EditorGUILayout.HelpBox(entry.ParseError, MessageType.Error);
                    EditorGUILayout.LabelField("Raw Value");
                    EditorGUILayout.TextArea(entry.JsonText, GUILayout.MinHeight(48f));
                    return;
                }

                DrawToken(entry.ParsedToken, 0, entry.Key);

                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Raw JSON");
                EditorGUILayout.TextArea(entry.JsonText, GUILayout.MinHeight(48f));
            }
        }

        private void DrawToken(JToken token, int indentLevel, string label)
        {
            if (token == null)
            {
                DrawPrimitiveField(indentLevel, label, "null");
                return;
            }

            switch (token.Type)
            {
                case JTokenType.Object:
                    DrawObject((JObject)token, indentLevel);
                    return;
                case JTokenType.Array:
                    DrawArray((JArray)token, indentLevel, label);
                    return;
                default:
                    DrawPrimitiveField(indentLevel, label, FormatToken(token));
                    return;
            }
        }

        private void DrawObject(JObject obj, int indentLevel)
        {
            foreach (JProperty property in obj.Properties())
            {
                if (IsPrimitive(property.Value))
                {
                    DrawPrimitiveField(indentLevel, property.Name, FormatToken(property.Value));
                    continue;
                }

                EditorGUILayout.Space(2f);
                DrawNestedHeader(indentLevel, property.Name);
                DrawToken(property.Value, indentLevel + 1, property.Name);
            }
        }

        private void DrawArray(JArray array, int indentLevel, string label)
        {
            if (AreAllChildrenPrimitive(array))
            {
                DrawPrimitiveField(indentLevel, label, FormatToken(array));
                return;
            }

            for (int i = 0; i < array.Count; i++)
            {
                JToken child = array[i];
                string childLabel = $"{label}[{i}]";
                if (IsPrimitive(child))
                {
                    DrawPrimitiveField(indentLevel, childLabel, FormatToken(child));
                    continue;
                }

                EditorGUILayout.Space(2f);
                DrawNestedHeader(indentLevel, childLabel);
                DrawToken(child, indentLevel + 1, childLabel);
            }
        }

        private void DrawPrimitiveField(int indentLevel, string key, string value)
        {
            if (ShouldUseMultilineValue(value))
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(indentLevel * 16f);
                        EditorGUILayout.LabelField(key, GUILayout.Width(260f));
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(indentLevel * 16f);
                        EditorGUILayout.TextArea(value, GUILayout.MinHeight(36f));
                    }
                }

                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(indentLevel * 16f);
                EditorGUILayout.LabelField(key, GUILayout.Width(260f));
                EditorGUILayout.SelectableLabel(value, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
        }

        private static bool IsPrimitive(JToken token)
        {
            return token == null
                   || token.Type == JTokenType.Integer
                   || token.Type == JTokenType.Float
                   || token.Type == JTokenType.String
                   || token.Type == JTokenType.Boolean
                   || token.Type == JTokenType.Null
                   || token.Type == JTokenType.Date
                   || token.Type == JTokenType.Guid
                   || token.Type == JTokenType.Uri
                   || token.Type == JTokenType.TimeSpan;
        }

        private static bool AreAllChildrenPrimitive(JArray array)
        {
            foreach (JToken child in array)
            {
                if (!IsPrimitive(child))
                {
                    return false;
                }
            }

            return true;
        }

        private static string FormatToken(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return "null";
            }

            if (token.Type == JTokenType.String)
            {
                return token.Value<string>() ?? string.Empty;
            }

            return token.ToString(Formatting.None);
        }

        private static void DrawNestedHeader(int indentLevel, string label)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(indentLevel * 16f);
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            }
        }

        private static bool ShouldUseMultilineValue(string value)
        {
            return !string.IsNullOrEmpty(value) && (value.Length > 120 || value.Contains("\n"));
        }

        private static string GetGuruAllSpecsAbsolutePath()
        {
            string assetsPath = Application.dataPath.Replace('\\', '/');
            string projectRoot = Directory.GetParent(assetsPath)?.FullName.Replace('\\', '/');
            if (string.IsNullOrEmpty(projectRoot))
            {
                return null;
            }

            return Path.Combine(projectRoot, GuruAllSpecsAssetPath).Replace('\\', '/');
        }

        private sealed class RemoteConfigEntry
        {
            public string Key;
            public string JsonText;
            public JToken ParsedToken;
            public string ParseError;
        }
    }
}
#endif
