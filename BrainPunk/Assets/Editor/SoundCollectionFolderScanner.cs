using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MelenitasDev.SoundsGood.Domain;
using UnityEditor;
using UnityEngine;

namespace Bear.Editor
{
    /// <summary>
    /// SoundCollection 文件夹扫描工具：从 Project 右键菜单递归收集音频，并以文件名补充 SFX 条目。
    /// </summary>
    internal static class SoundCollectionFolderScanner
    {
        private const string MenuPath = "Assets/Bear/扫描音频并补充到 SoundCollection";
        private const string SoundCollectionPath = "Assets/SoundsGood/Data/Collections/SoundCollection.asset";
        private const string ReportTitle = "SoundCollection 音频扫描";

        private static readonly HashSet<string> CSharpKeywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
            "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
            "void", "volatile", "while"
        };

        #region Menu Entry

        [MenuItem(MenuPath, false, 2000)]
        private static void ScanSelectedFolder(MenuCommand menuCommand)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(ReportTitle, "请先停止 PlayMode，等待编辑器返回可编辑状态后再执行。", "确定");
                return;
            }

            if (!TryGetSelectedFolder(menuCommand.context, out string folderPath))
            {
                EditorUtility.DisplayDialog(ReportTitle, "请在 Project 窗口中右键选择一个文件夹后再执行。", "确定");
                return;
            }

            ScanReport report = ScanFolder(folderPath);
            WriteReport(report);
            EditorUtility.DisplayDialog(ReportTitle, report.BuildDialogMessage(), "确定");
        }

        private static bool TryGetSelectedFolder(UnityEngine.Object contextObject, out string folderPath)
        {
            if (TryGetFolderPath(contextObject, out folderPath)
                || TryGetFolderPath(Selection.activeObject, out folderPath))
            {
                return true;
            }

            string[] selectedGuids = Selection.assetGUIDs;
            for (int i = 0; i < selectedGuids.Length; i++)
            {
                string selectedPath = AssetDatabase.GUIDToAssetPath(selectedGuids[i]);
                if (!string.IsNullOrEmpty(selectedPath) && AssetDatabase.IsValidFolder(selectedPath))
                {
                    folderPath = selectedPath;
                    return true;
                }
            }

            folderPath = null;
            return false;
        }

        private static bool TryGetFolderPath(UnityEngine.Object assetObject, out string folderPath)
        {
            folderPath = AssetDatabase.GetAssetPath(assetObject);
            return !string.IsNullOrEmpty(folderPath) && AssetDatabase.IsValidFolder(folderPath);
        }

        #endregion

        #region Scan Flow

        private static ScanReport ScanFolder(string folderPath)
        {
            var report = new ScanReport(folderPath);
            SoundDataCollection soundCollection = AssetDatabase.LoadAssetAtPath<SoundDataCollection>(SoundCollectionPath);
            if (soundCollection == null)
            {
                report.AddFailure($"未找到 SoundCollection：{SoundCollectionPath}");
                return report;
            }

            string[] audioPaths = AssetDatabase.FindAssets("t:AudioClip", new[] { folderPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            report.ScannedAudioCount = audioPaths.Length;
            if (audioPaths.Length == 0)
            {
                return report;
            }

            HashSet<string> existingNames = new HashSet<string>(
                soundCollection.Sounds
                    .Where(sound => sound != null && !string.IsNullOrEmpty(sound.Tag))
                    .Select(sound => sound.Tag),
                StringComparer.OrdinalIgnoreCase);

            List<IGrouping<string, string>> audioGroups = audioPaths
                .GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Undo.RecordObject(soundCollection, "扫描音频并补充 SoundCollection");

            foreach (IGrouping<string, string> audioGroup in audioGroups)
            {
                string soundName = audioGroup.Key;
                string[] sameNamePaths = audioGroup.ToArray();

                if (sameNamePaths.Length > 1)
                {
                    report.AddDuplicate(soundName, $"扫描目录内存在 {sameNamePaths.Length} 个同名音频", sameNamePaths);
                    continue;
                }

                if (existingNames.Contains(soundName))
                {
                    report.AddDuplicate(soundName, "SoundCollection 中已存在同名条目", sameNamePaths);
                    continue;
                }

                if (!IsValidGeneratedSfxName(soundName))
                {
                    report.AddInvalidName(soundName, sameNamePaths[0]);
                    continue;
                }

                string audioPath = sameNamePaths[0];
                AudioClip audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath);
                if (audioClip == null)
                {
                    report.AddFailure($"无法加载 AudioClip：{audioPath}");
                    continue;
                }

                bool created = soundCollection.CreateSound(
                    new[] { audioClip },
                    soundName,
                    CompressionPreset.FrequentSound,
                    false,
                    out string result);

                if (!created)
                {
                    report.AddFailure($"{audioPath}：{result}");
                    continue;
                }

                existingNames.Add(soundName);
                report.AddCreated(soundName, audioPath);
            }

            if (report.CreatedCount == 0)
            {
                return report;
            }

            EditorUtility.SetDirty(soundCollection);
            if (!TrySaveCollectionAndGenerateSfx(out string saveError))
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                report.AddFailure($"SoundCollection 已保存，但 SFX_Generated.cs 自动刷新失败：{saveError}");
            }

            return report;
        }

        private static bool IsValidGeneratedSfxName(string soundName)
        {
            if (string.IsNullOrEmpty(soundName) || CSharpKeywords.Contains(soundName))
            {
                return false;
            }

            if (!char.IsLetter(soundName[0]) && soundName[0] != '_')
            {
                return false;
            }

            for (int i = 1; i < soundName.Length; i++)
            {
                if (!char.IsLetterOrDigit(soundName[i]) && soundName[i] != '_')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TrySaveCollectionAndGenerateSfx(out string error)
        {
            try
            {
                Assembly editorAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(assembly => assembly.GetName().Name == "SoundsGood.Editor");
                Type helperType = editorAssembly?.GetType("MelenitasDev.SoundsGood.Editor.EditorHelper");
                Type sectionsType = editorAssembly?.GetType("MelenitasDev.SoundsGood.Editor.Sections");
                MethodInfo saveMethod = helperType?.GetMethod(
                    "SaveCollectionChanges",
                    BindingFlags.Static | BindingFlags.NonPublic);

                if (sectionsType == null || saveMethod == null)
                {
                    error = "未找到 SoundsGood.Editor 的保存入口。";
                    return false;
                }

                object soundsSection = Enum.Parse(sectionsType, "Sounds");
                saveMethod.Invoke(null, new[] { soundsSection, (object)true });
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                Exception sourceException = ex is TargetInvocationException invocationException
                    ? invocationException.InnerException ?? invocationException
                    : ex;
                error = sourceException.Message;
                return false;
            }
        }

        #endregion

        #region Report

        private static void WriteReport(ScanReport report)
        {
            string log = report.BuildConsoleLog();
            if (report.WarningCount > 0 || report.FailureCount > 0)
            {
                Debug.LogWarning(log);
                return;
            }

            Debug.Log(log);
        }

        private sealed class ScanReport
        {
            private readonly string folderPath;
            private readonly List<string> createdEntries = new List<string>();
            private readonly List<string> duplicateEntries = new List<string>();
            private readonly List<string> invalidNameEntries = new List<string>();
            private readonly List<string> failures = new List<string>();

            public int ScannedAudioCount { get; set; }
            public int CreatedCount => createdEntries.Count;
            public int DuplicateCount => duplicateEntries.Count;
            public int InvalidNameCount => invalidNameEntries.Count;
            public int FailureCount => failures.Count;
            public int WarningCount => DuplicateCount + InvalidNameCount;

            public ScanReport(string folderPath)
            {
                this.folderPath = folderPath;
            }

            public void AddCreated(string soundName, string audioPath)
            {
                createdEntries.Add($"{soundName} <- {audioPath}");
            }

            public void AddDuplicate(string soundName, string reason, IReadOnlyList<string> audioPaths)
            {
                duplicateEntries.Add($"{soundName}：{reason}；{string.Join(" | ", audioPaths)}");
            }

            public void AddInvalidName(string soundName, string audioPath)
            {
                invalidNameEntries.Add($"{soundName}：无法生成合法的 SFX C# 字段；{audioPath}");
            }

            public void AddFailure(string message)
            {
                failures.Add(message);
            }

            public string BuildDialogMessage()
            {
                var sb = new StringBuilder();
                sb.AppendLine($"扫描音频：{ScannedAudioCount}");
                sb.AppendLine($"新增条目：{CreatedCount}");
                sb.AppendLine($"重名跳过：{DuplicateCount}");
                sb.AppendLine($"名称无效：{InvalidNameCount}");
                sb.AppendLine($"失败：{FailureCount}");

                if (DuplicateCount > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("重名警告：");
                    foreach (string duplicateEntry in duplicateEntries.Take(8))
                    {
                        sb.AppendLine($"- {duplicateEntry}");
                    }

                    if (DuplicateCount > 8)
                    {
                        sb.AppendLine($"- 其余 {DuplicateCount - 8} 项请查看 Console");
                    }
                }

                if (InvalidNameCount > 0 || FailureCount > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("名称无效或失败详情请查看 Console。");
                }

                return sb.ToString();
            }

            public string BuildConsoleLog()
            {
                var sb = new StringBuilder();
                sb.AppendLine($"[{ReportTitle}] Folder: {folderPath}");
                sb.AppendLine($"[{ReportTitle}] Scanned: {ScannedAudioCount}, Created: {CreatedCount}, Duplicates: {DuplicateCount}, Invalid names: {InvalidNameCount}, Failures: {FailureCount}");
                AppendSection(sb, "Created", createdEntries);
                AppendSection(sb, "Duplicate warnings", duplicateEntries);
                AppendSection(sb, "Invalid names", invalidNameEntries);
                AppendSection(sb, "Failures", failures);
                return sb.ToString();
            }

            private static void AppendSection(StringBuilder sb, string title, IReadOnlyList<string> entries)
            {
                if (entries.Count == 0)
                {
                    return;
                }

                sb.AppendLine($"[{ReportTitle}] ---- {title} ----");
                for (int i = 0; i < entries.Count; i++)
                {
                    sb.AppendLine(entries[i]);
                }
            }
        }

        #endregion
    }
}
