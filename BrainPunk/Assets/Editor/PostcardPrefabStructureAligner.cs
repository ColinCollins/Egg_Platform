using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Bear.Editor
{
    /// <summary>
    /// 明信片 Prefab 结构补齐工具：识别选中的扁平美术 Prefab，按相同拼图数量的运行时模板补齐组件与层级。
    /// </summary>
    public static class PostcardPrefabStructureAligner
    {
        private const string ContextMenuPath = "Assets/Bear/Postcard/补齐结构与组件";
        private const string TargetRoot = "Assets/Game/UI/Postcard/Sprites/Pictures/";
        private const string RuntimeRoot = "Assets/Game/UI/Postcard/Prefabs/Pages/";
        private const string UniverseReferencePath = RuntimeRoot + "Postcard_universe.prefab";
        private const string CostBlockPrefabPath = "Assets/Game/UI/Postcard/Prefabs/Postcard_CostBlock.prefab";
        private const string CompleteModePrefabPath = "Assets/Game/UI/Postcard/Prefabs/Postcard_CompleteMode.prefab";
        private const string CommandLineTargetArgument = "-postcardTarget";

        private static readonly Regex PieceImageNameRegex = new Regex(
            @"^UI_mingxinpian_img_(\d+)([12])_01$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly string[] TemplatePreference =
        {
            UniverseReferencePath,
            RuntimeRoot + "Postcard_ocean.prefab",
            RuntimeRoot + "Postcard_office.prefab",
            RuntimeRoot + "Postcard_concert.prefab",
            RuntimeRoot + "Postcard_ems.prefab",
            RuntimeRoot + "Postcard_food.prefab",
            RuntimeRoot + "Postcard_forest.prefab",
            RuntimeRoot + "Postcard_museum.prefab"
        };

        #region Menu Entry

        /// <summary>
        /// 用途：从 Project 资源右键菜单处理当前选中的 Postcard_*.prefab。
        /// 调用边界：仅允许处理 Pictures 下的美术 prefab；会覆盖目标 prefab 内容但保持目标 meta GUID。
        /// 注意事项：执行前会弹窗确认，结构识别不唯一或目标已是运行时 prefab 时不会保存。
        /// </summary>
        [MenuItem(ContextMenuPath, false, 2100)]
        public static void AlignSelectedPostcardPrefabs()
        {
            string[] targetPaths = GetSelectedTargetPaths();
            if (targetPaths.Length == 0)
            {
                EditorUtility.DisplayDialog("Postcard Prefab 结构补齐", "请选择 Pictures 目录下名称以 Postcard_ 开头的 prefab。", "确定");
                return;
            }

            string message = "将补齐以下 prefab 的运行时结构与组件，并保留主题图片、拼图数量和 meta GUID：\n\n"
                + string.Join("\n", targetPaths);
            if (!EditorUtility.DisplayDialog("Postcard Prefab 结构补齐", message, "开始执行", "取消"))
                return;

            var report = new StringBuilder();
            int successCount = 0;
            for (int i = 0; i < targetPaths.Length; i++)
            {
                bool success = TryAlignPrefabAtPath(targetPaths[i], out string itemReport);
                if (success)
                    successCount++;
                report.AppendLine(itemReport);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(report.ToString());
            EditorUtility.DisplayDialog(
                "Postcard Prefab 结构补齐",
                $"成功 {successCount}/{targetPaths.Length}。\n详细信息请查看 Console。",
                "确定");
        }

        [MenuItem(ContextMenuPath, true)]
        private static bool ValidateAlignSelectedPostcardPrefabs()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && GetSelectedTargetPaths().Length > 0;
        }

        /// <summary>
        /// 用途：供 Skill 或命令行通过 Unity -executeMethod 指定单个 prefab 执行同一套补齐流程。
        /// 调用边界：命令行必须传入 -postcardTarget 后跟项目 Assets 相对路径。
        /// 注意事项：仅在 batch mode 结束时主动设置进程退出码，交互式 Editor 不会被关闭。
        /// </summary>
        public static void AlignFromCommandLine()
        {
            string targetPath = GetCommandLineArgument(CommandLineTargetArgument);
            bool success = TryAlignPrefabAtPath(targetPath, out string report);
            if (success)
                Debug.Log(report);
            else
                Debug.LogError(report);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (Application.isBatchMode)
                EditorApplication.Exit(success ? 0 : 1);
        }

        #endregion

        #region Public Alignment API

        /// <summary>
        /// 用途：按目标路径识别主题美术结构，并补齐 Postcard_PageItem、Animator、ProgressItem、TurnEffect、CostBlock 与 CompleteMode。
        /// 调用边界：只接受 Pictures 目录下名称以 Postcard_ 开头的 prefab；调用方负责决定是否执行。
        /// 注意事项：目标已包含 Postcard_PageItem、主题映射歧义或找不到同数量模板时返回 false 且不保存。
        /// </summary>
        /// <param name="targetPath">Unity 项目内以 Assets/ 开头的 prefab 路径。</param>
        /// <param name="report">执行结果与失败原因。</param>
        /// <returns>成功保存并通过静态结构验证时返回 true。</returns>
        public static bool TryAlignPrefabAtPath(string targetPath, out string report)
        {
            report = string.Empty;
            targetPath = NormalizeAssetPath(targetPath);
            if (!IsSupportedTargetPath(targetPath))
            {
                report = $"[PostcardPrefabStructureAligner] 不支持的目标路径：{targetPath ?? "<null>"}";
                return false;
            }

            string originalGuid = AssetDatabase.AssetPathToGUID(targetPath);
            if (string.IsNullOrEmpty(originalGuid))
            {
                report = $"[PostcardPrefabStructureAligner] 目标 prefab 尚未被 AssetDatabase 正确导入：{targetPath}";
                return false;
            }

            if (!TryCaptureTheme(targetPath, out ThemeSnapshot theme, out string captureError))
            {
                report = $"[PostcardPrefabStructureAligner] {targetPath} 识别失败：{captureError}";
                return false;
            }

            if (!TrySelectTemplate(theme.Groups.Count, out string templatePath, out string templateError))
            {
                report = $"[PostcardPrefabStructureAligner] {targetPath} 模板选择失败：{templateError}";
                return false;
            }

            if (!TryBuildAndSaveTarget(templatePath, targetPath, theme, out string buildError))
            {
                report = $"[PostcardPrefabStructureAligner] {targetPath} 保存失败：{buildError}";
                return false;
            }

            AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
            string currentGuid = AssetDatabase.AssetPathToGUID(targetPath);
            if (!string.Equals(originalGuid, currentGuid, StringComparison.Ordinal))
            {
                report = $"[PostcardPrefabStructureAligner] {targetPath} meta GUID 发生变化：{originalGuid} -> {currentGuid}";
                return false;
            }

            if (!TryValidateSavedTarget(targetPath, theme, out string validationError))
            {
                report = $"[PostcardPrefabStructureAligner] {targetPath} 保存后验证失败：{validationError}";
                return false;
            }

            report = $"[PostcardPrefabStructureAligner] 完成：{targetPath}\n"
                + $"  识别拼图组：{theme.Groups.Count}\n"
                + $"  结构模板：{templatePath}\n"
                + $"  Universe 契约：{UniverseReferencePath}\n"
                + $"  保持 meta GUID：{currentGuid}";
            return true;
        }

        #endregion

        #region Theme Detection

        private static bool TryCaptureTheme(string targetPath, out ThemeSnapshot theme, out string error)
        {
            theme = null;
            error = string.Empty;
            GameObject targetRoot = null;
            try
            {
                targetRoot = PrefabUtility.LoadPrefabContents(targetPath);
                if (targetRoot == null)
                {
                    error = "LoadPrefabContents 返回 null。";
                    return false;
                }

                if (targetRoot.GetComponent<Postcard_PageItem>() != null)
                {
                    error = "目标已经包含 Postcard_PageItem；为避免覆盖已完成的运行时 prefab，本工具不会重复转换。";
                    return false;
                }

                List<ThemeGroupSnapshot> groups = null;
                Transform artRoot = null;
                Transform[] transforms = targetRoot.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < transforms.Length; i++)
                {
                    List<ThemeGroupSnapshot> candidateGroups = CaptureDirectGroups(transforms[i]);
                    if (candidateGroups.Count == 0)
                        continue;
                    if (groups != null)
                    {
                        error = "检测到多个包含拼图组的候选父节点，语义映射不唯一。";
                        return false;
                    }

                    artRoot = transforms[i];
                    groups = candidateGroups;
                }

                if (artRoot == null || groups == null || groups.Count == 0)
                {
                    error = "未检测到同时包含 *_1_01、*_2_01、感叹号和 CostBlock 的拼图组。";
                    return false;
                }

                groups = groups.OrderBy(group => group.SortKey).ToList();
                if (groups.Select(group => group.SortKey).Distinct().Count() != groups.Count)
                {
                    error = "拼图图片编号重复，无法确定 ProgressItem.Index 映射。";
                    return false;
                }

                Image[] directImages = GetDirectComponents<Image>(artRoot);
                Image character = directImages.FirstOrDefault(image =>
                    image != null && image.name.IndexOf("juese", StringComparison.OrdinalIgnoreCase) >= 0);
                Image[] backgroundCandidates = directImages.Where(image =>
                        image != null
                        && image != character
                        && !image.name.StartsWith("UI_common_img_gantan_01", StringComparison.Ordinal)
                        && !PieceImageNameRegex.IsMatch(image.name))
                    .ToArray();

                if (character == null || backgroundCandidates.Length != 1)
                {
                    error = $"主题背景或角色图识别失败：背景候选 {backgroundCandidates.Length}，角色候选 {(character == null ? 0 : 1)}。";
                    return false;
                }

                theme = new ThemeSnapshot
                {
                    RootName = targetRoot.name,
                    ArtRootRect = RectTransformSnapshot.Capture(artRoot as RectTransform),
                    Background = ImageSnapshot.Capture(backgroundCandidates[0]),
                    Character = ImageSnapshot.Capture(character),
                    Groups = groups
                };
                return true;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return false;
            }
            finally
            {
                if (targetRoot != null)
                    PrefabUtility.UnloadPrefabContents(targetRoot);
            }
        }

        private static List<ThemeGroupSnapshot> CaptureDirectGroups(Transform parent)
        {
            var groups = new List<ThemeGroupSnapshot>();
            for (int i = 0; i < parent.childCount; i++)
            {
                if (TryCaptureThemeGroup(parent.GetChild(i), out ThemeGroupSnapshot group))
                    groups.Add(group);
            }
            return groups;
        }

        private static bool TryCaptureThemeGroup(Transform groupTransform, out ThemeGroupSnapshot group)
        {
            group = null;
            Image[] images = GetDirectComponents<Image>(groupTransform);
            Image cover = null;
            Image baseImage = null;
            Image redDot = null;
            int sortKey = -1;

            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image.name.StartsWith("UI_common_img_gantan_01", StringComparison.Ordinal))
                {
                    redDot = image;
                    continue;
                }

                Match match = PieceImageNameRegex.Match(image.name);
                if (!match.Success || !int.TryParse(match.Groups[1].Value, out int imageSortKey))
                    continue;

                if (sortKey >= 0 && sortKey != imageSortKey)
                    return false;
                sortKey = imageSortKey;
                if (match.Groups[2].Value == "1")
                    cover = image;
                else
                    baseImage = image;
            }

            Transform costBlock = null;
            for (int i = 0; i < groupTransform.childCount; i++)
            {
                Transform child = groupTransform.GetChild(i);
                if (child.name.IndexOf("PostcardCostBlock", StringComparison.OrdinalIgnoreCase) >= 0
                    || child.name.IndexOf("Postcard_CostBlock", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    costBlock = child;
                    break;
                }
            }

            if (cover == null || baseImage == null || redDot == null || costBlock == null || sortKey < 0)
                return false;
            if (cover.sprite == null || baseImage.sprite == null || redDot.sprite == null)
                return false;

            group = new ThemeGroupSnapshot
            {
                Name = groupTransform.name,
                SortKey = sortKey,
                Rect = RectTransformSnapshot.Capture(groupTransform as RectTransform),
                Cover = ImageSnapshot.Capture(cover),
                BaseImage = ImageSnapshot.Capture(baseImage),
                RedDot = ImageSnapshot.Capture(redDot),
                CostBlockRect = RectTransformSnapshot.Capture(costBlock as RectTransform)
            };
            return true;
        }

        #endregion

        #region Template Selection And Save

        private static bool TrySelectTemplate(int groupCount, out string templatePath, out string error)
        {
            templatePath = null;
            error = string.Empty;
            for (int i = 0; i < TemplatePreference.Length; i++)
            {
                string candidate = TemplatePreference[i];
                if (AssetDatabase.LoadAssetAtPath<GameObject>(candidate) == null)
                    continue;
                if (TryInspectRuntimeTemplate(candidate, groupCount, out _))
                {
                    templatePath = candidate;
                    return true;
                }
            }

            error = $"运行时页面目录下没有通过结构契约且包含 {groupCount} 个 Postcard_ProgressItem 的模板。";
            return false;
        }

        private static bool TryInspectRuntimeTemplate(string templatePath, int expectedGroupCount, out string error)
        {
            error = string.Empty;
            GameObject templateRoot = null;
            try
            {
                templateRoot = PrefabUtility.LoadPrefabContents(templatePath);
                if (templateRoot == null
                    || templateRoot.GetComponent<Postcard_PageItem>() == null
                    || templateRoot.GetComponent<Animator>() == null)
                {
                    error = "模板根缺少 Postcard_PageItem 或 Animator。";
                    return false;
                }

                Transform visualRoot = FindDirectChild(templateRoot.transform, "Root");
                Transform mainPage = visualRoot == null ? null : FindDirectChild(visualRoot, "mainPage");
                Postcard_ProgressItem[] items = templateRoot.GetComponentsInChildren<Postcard_ProgressItem>(true);
                if (visualRoot == null || mainPage == null || items.Length != expectedGroupCount)
                {
                    error = "模板 Root/mainPage 或拼图数量不符合要求。";
                    return false;
                }

                if (FindDescendantByName(templateRoot.transform, "Postcard_CompleteMode") == null)
                {
                    error = "模板缺少 Postcard_CompleteMode。";
                    return false;
                }

                for (int i = 0; i < items.Length; i++)
                {
                    if (!TryResolveRuntimeGroup(items[i], out _, out string groupError))
                    {
                        error = $"模板 ProgressItem[{i}] 不完整：{groupError}";
                        return false;
                    }
                }

                return true;
            }
            finally
            {
                if (templateRoot != null)
                    PrefabUtility.UnloadPrefabContents(templateRoot);
            }
        }

        private static bool TryBuildAndSaveTarget(
            string templatePath,
            string targetPath,
            ThemeSnapshot theme,
            out string error)
        {
            error = string.Empty;
            GameObject templateRoot = null;
            try
            {
                templateRoot = PrefabUtility.LoadPrefabContents(templatePath);
                if (templateRoot == null)
                {
                    error = "LoadPrefabContents 返回 null。";
                    return false;
                }

                Transform visualRoot = FindDirectChild(templateRoot.transform, "Root");
                Transform mainPage = visualRoot == null ? null : FindDirectChild(visualRoot, "mainPage");
                Image mainPageImage = mainPage == null ? null : mainPage.GetComponent<Image>();
                if (visualRoot == null || mainPageImage == null)
                {
                    error = "模板 Root/mainPage 结构缺失。";
                    return false;
                }

                Postcard_ProgressItem[] runtimeItems = templateRoot.GetComponentsInChildren<Postcard_ProgressItem>(true)
                    .OrderBy(item => item.Index)
                    .ToArray();
                if (runtimeItems.Length != theme.Groups.Count
                    || runtimeItems.Select(item => item.Index).Distinct().Count() != runtimeItems.Length)
                {
                    error = "模板 ProgressItem 数量或 Index 不唯一。";
                    return false;
                }

                Image character = GetDirectComponents<Image>(visualRoot)
                    .FirstOrDefault(image => image != null
                        && image.gameObject != mainPage.gameObject
                        && image.name.IndexOf("juese", StringComparison.OrdinalIgnoreCase) >= 0);
                if (character == null)
                {
                    error = "模板 Root 下未找到角色图。";
                    return false;
                }

                templateRoot.name = theme.RootName;
                theme.ArtRootRect.Apply(visualRoot as RectTransform);
                theme.Background.Apply(mainPageImage, false);
                mainPageImage.gameObject.name = "mainPage";
                theme.Character.Apply(character, true);

                for (int i = 0; i < runtimeItems.Length; i++)
                {
                    if (!TryResolveRuntimeGroup(runtimeItems[i], out RuntimeGroupBindings bindings, out string groupError))
                    {
                        error = $"模板 ProgressItem[{i}] 绑定失败：{groupError}";
                        return false;
                    }

                    ThemeGroupSnapshot source = theme.Groups[i];
                    bindings.Group.name = source.Name;
                    source.Rect.Apply(bindings.Group as RectTransform);
                    source.Cover.Apply(bindings.Cover, true);
                    source.BaseImage.Apply(bindings.BaseImage, true);
                    source.RedDot.Apply(bindings.RedDot, true);
                    source.CostBlockRect.Apply(bindings.CostBlock as RectTransform);
                }

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(templateRoot, targetPath, out bool success);
                if (!success || saved == null)
                {
                    error = "PrefabUtility.SaveAsPrefabAsset 返回失败。";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return false;
            }
            finally
            {
                if (templateRoot != null)
                    PrefabUtility.UnloadPrefabContents(templateRoot);
            }
        }

        #endregion

        #region Validation

        private static bool TryValidateSavedTarget(string targetPath, ThemeSnapshot theme, out string error)
        {
            error = string.Empty;
            GameObject targetRoot = null;
            try
            {
                targetRoot = PrefabUtility.LoadPrefabContents(targetPath);
                if (targetRoot == null)
                {
                    error = "LoadPrefabContents 返回 null。";
                    return false;
                }

                if (targetRoot.GetComponent<Postcard_PageItem>() == null
                    || targetRoot.GetComponent<Animator>() == null
                    || targetRoot.GetComponent<Canvas>() != null)
                {
                    error = "根组件契约不满足：需要 Postcard_PageItem + Animator，且不应保留美术 Canvas。";
                    return false;
                }

                int missingScriptCount = targetRoot.GetComponentsInChildren<Transform>(true)
                    .Sum(transform => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject));
                if (missingScriptCount > 0)
                {
                    error = $"检测到 {missingScriptCount} 个 Missing Script。";
                    return false;
                }

                Postcard_ProgressItem[] items = targetRoot.GetComponentsInChildren<Postcard_ProgressItem>(true)
                    .OrderBy(item => item.Index)
                    .ToArray();
                if (items.Length != theme.Groups.Count)
                {
                    error = $"ProgressItem 数量不一致：期望 {theme.Groups.Count}，实际 {items.Length}。";
                    return false;
                }

                Transform visualRoot = FindDirectChild(targetRoot.transform, "Root");
                Transform mainPage = visualRoot == null ? null : FindDirectChild(visualRoot, "mainPage");
                Image mainPageImage = mainPage == null ? null : mainPage.GetComponent<Image>();
                if (mainPageImage == null || mainPageImage.sprite != theme.Background.Sprite)
                {
                    error = "mainPage 未保留目标主题背景图。";
                    return false;
                }

                if (FindDescendantByName(targetRoot.transform, "Postcard_CompleteMode") == null)
                {
                    error = "缺少 Postcard_CompleteMode。";
                    return false;
                }

                for (int i = 0; i < items.Length; i++)
                {
                    if (!TryResolveRuntimeGroup(items[i], out RuntimeGroupBindings bindings, out string groupError))
                    {
                        error = $"ProgressItem[{i}] 结构无效：{groupError}";
                        return false;
                    }

                    if (bindings.Cover.sprite != theme.Groups[i].Cover.Sprite
                        || bindings.BaseImage.sprite != theme.Groups[i].BaseImage.Sprite)
                    {
                        error = $"ProgressItem[{i}] 未保留目标主题拼图图片。";
                        return false;
                    }

                    if (!ValidateSerializedReferences(items[i], out string referenceError))
                    {
                        error = $"ProgressItem[{i}] 引用缺失：{referenceError}";
                        return false;
                    }
                }

                if (!ValidateSerializedReferences(targetRoot.GetComponent<Postcard_PageItem>(), out string pageReferenceError))
                {
                    error = $"Postcard_PageItem 引用缺失：{pageReferenceError}";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                return false;
            }
            finally
            {
                if (targetRoot != null)
                    PrefabUtility.UnloadPrefabContents(targetRoot);
            }
        }

        private static bool ValidateSerializedReferences(Postcard_ProgressItem item, out string error)
        {
            return ValidateObjectReferences(item, new[] { "clickBtn", "text", "blockCost", "coverImg", "turnEffect", "redDot" }, out error);
        }

        private static bool ValidateSerializedReferences(Postcard_PageItem pageItem, out string error)
        {
            if (!ValidateObjectReferences(pageItem, new[] { "anim", "bg", "grayImage", "completePart", "contentTxt" }, out error))
                return false;

            var serializedObject = new SerializedObject(pageItem);
            SerializedProperty items = serializedObject.FindProperty("items");
            if (items == null || !items.isArray || items.arraySize == 0)
            {
                error = "items";
                return false;
            }

            for (int i = 0; i < items.arraySize; i++)
            {
                if (items.GetArrayElementAtIndex(i).objectReferenceValue == null)
                {
                    error = $"items[{i}]";
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateObjectReferences(UnityEngine.Object target, IEnumerable<string> propertyNames, out string error)
        {
            error = string.Empty;
            var serializedObject = new SerializedObject(target);
            foreach (string propertyName in propertyNames)
            {
                SerializedProperty property = serializedObject.FindProperty(propertyName);
                if (property == null || property.objectReferenceValue == null)
                {
                    error = propertyName;
                    return false;
                }
            }
            return true;
        }

        #endregion

        #region Runtime Group Binding

        private static bool TryResolveRuntimeGroup(
            Postcard_ProgressItem item,
            out RuntimeGroupBindings bindings,
            out string error)
        {
            bindings = null;
            error = string.Empty;
            if (item == null)
            {
                error = "Postcard_ProgressItem 为空。";
                return false;
            }

            Image[] images = GetDirectComponents<Image>(item.transform);
            Image cover = images.FirstOrDefault(image => image != null && image.GetComponent<Postcard_TurnEffect>() != null);
            Image redDot = images.FirstOrDefault(image => image != null
                && image.name.StartsWith("UI_common_img_gantan_01", StringComparison.Ordinal));
            Image baseImage = images.FirstOrDefault(image => image != null && image != cover && image != redDot);
            Transform costBlock = null;
            for (int i = 0; i < item.transform.childCount; i++)
            {
                Transform child = item.transform.GetChild(i);
                string sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject);
                if (string.Equals(sourcePath, CostBlockPrefabPath, StringComparison.OrdinalIgnoreCase))
                {
                    costBlock = child;
                    break;
                }
            }

            if (cover == null || baseImage == null || redDot == null || costBlock == null)
            {
                error = $"cover={(cover != null)}, base={(baseImage != null)}, redDot={(redDot != null)}, costBlock={(costBlock != null)}";
                return false;
            }

            bindings = new RuntimeGroupBindings
            {
                Group = item.transform,
                Cover = cover,
                BaseImage = baseImage,
                RedDot = redDot,
                CostBlock = costBlock
            };
            return true;
        }

        #endregion

        #region Helpers

        private static string[] GetSelectedTargetPaths()
        {
            return Selection.objects
                .Select(AssetDatabase.GetAssetPath)
                .Select(NormalizeAssetPath)
                .Where(IsSupportedTargetPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static bool IsSupportedTargetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)
                || !path.StartsWith(TargetRoot, StringComparison.OrdinalIgnoreCase)
                || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                return false;

            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            return fileName.StartsWith("Postcard_", StringComparison.Ordinal);
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? path : path.Replace('\\', '/').Trim();
        }

        private static string GetCommandLineArgument(string argumentName)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], argumentName, StringComparison.OrdinalIgnoreCase))
                    return NormalizeAssetPath(args[i + 1]);
            }
            return null;
        }

        private static T[] GetDirectComponents<T>(Transform parent) where T : Component
        {
            var result = new List<T>();
            for (int i = 0; i < parent.childCount; i++)
            {
                T component = parent.GetChild(i).GetComponent<T>();
                if (component != null)
                    result.Add(component);
            }
            return result.ToArray();
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            if (parent == null)
                return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (string.Equals(child.name, name, StringComparison.Ordinal))
                    return child;
            }
            return null;
        }

        private static Transform FindDescendantByName(Transform root, string name)
        {
            if (root == null)
                return null;
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            return transforms.FirstOrDefault(transform => string.Equals(transform.name, name, StringComparison.Ordinal));
        }

        #endregion

        #region Data Models

        private sealed class ThemeSnapshot
        {
            public string RootName;
            public RectTransformSnapshot ArtRootRect;
            public ImageSnapshot Background;
            public ImageSnapshot Character;
            public List<ThemeGroupSnapshot> Groups;
        }

        private sealed class ThemeGroupSnapshot
        {
            public string Name;
            public int SortKey;
            public RectTransformSnapshot Rect;
            public ImageSnapshot Cover;
            public ImageSnapshot BaseImage;
            public ImageSnapshot RedDot;
            public RectTransformSnapshot CostBlockRect;
        }

        private sealed class RuntimeGroupBindings
        {
            public Transform Group;
            public Image Cover;
            public Image BaseImage;
            public Image RedDot;
            public Transform CostBlock;
        }

        private sealed class ImageSnapshot
        {
            public string Name;
            public Sprite Sprite;
            public RectTransformSnapshot Rect;

            public static ImageSnapshot Capture(Image source)
            {
                return new ImageSnapshot
                {
                    Name = source.name,
                    Sprite = source.sprite,
                    Rect = RectTransformSnapshot.Capture(source.rectTransform)
                };
            }

            public void Apply(Image target, bool copyName)
            {
                if (copyName)
                    target.gameObject.name = Name;
                target.sprite = Sprite;
                Rect.Apply(target.rectTransform);
                EditorUtility.SetDirty(target);
            }
        }

        private sealed class RectTransformSnapshot
        {
            public Vector2 AnchorMin;
            public Vector2 AnchorMax;
            public Vector2 AnchoredPosition;
            public Vector2 SizeDelta;
            public Vector2 Pivot;
            public Vector3 LocalScale;
            public Quaternion LocalRotation;

            public static RectTransformSnapshot Capture(RectTransform source)
            {
                if (source == null)
                    throw new InvalidOperationException("目标节点缺少 RectTransform。");
                return new RectTransformSnapshot
                {
                    AnchorMin = source.anchorMin,
                    AnchorMax = source.anchorMax,
                    AnchoredPosition = source.anchoredPosition,
                    SizeDelta = source.sizeDelta,
                    Pivot = source.pivot,
                    LocalScale = source.localScale,
                    LocalRotation = source.localRotation
                };
            }

            public void Apply(RectTransform target)
            {
                if (target == null)
                    throw new InvalidOperationException("模板节点缺少 RectTransform。");
                target.anchorMin = AnchorMin;
                target.anchorMax = AnchorMax;
                target.anchoredPosition = AnchoredPosition;
                target.sizeDelta = SizeDelta;
                target.pivot = Pivot;
                target.localScale = LocalScale;
                target.localRotation = LocalRotation;
                EditorUtility.SetDirty(target);
            }
        }

        #endregion
    }
}
