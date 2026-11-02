#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Luban 配置生成工具
    /// 用于调用 Luban 的 gen.bat 生成配置代码和数据
    /// </summary>
    public static class LubanGenerator
    {
        private const string CurrentRemoteConfigVersionKey = "currentRemoteConfigVersion";
        private const string MENU_PATH = "Tools/Luban/Generate Config";
        private const string GEN_BAT_NAME = "gen.bat";
        private const string TABLES_CS_PATH = "Assets/Configs/Code/Tables.cs";
        private const string CONFIG_FILE_NAMES_PATH = "Assets/Game/Scripts/Config/ConfigFileNames.cs";

        private const string MENU_UPDATE_ALL_GURU_CONFIGS = "Tools/Luban/Update All Configs To Guru YAML";
        private const string GURU_SPEC_AUTO_GEN_MENU = "Guru/Guru Spec/Auto-Gen <GuruAllSpecs.G.cs>";
        private const string LEVELSORT_JSON_ASSET_PATH = "Assets/Game/Configs/tblevelsort.json";
        private const string DIG_LEVELSORT_JSON_ASSET_PATH = "Assets/Game/Configs/tbdiglevelsort.json";
        private const string DigGameConfigKey = "diggame_config";
        private const string DRAW_LEVELSORT_JSON_ASSET_PATH = "Assets/Game/Configs/tbdrawlevelsort.json";
        private const string GLOBALCONST_JSON_ASSET_PATH = "Assets/Game/Configs/lubanconfig_tbglobalconst.json";
        private const string GURU_SPEC_YAML_ASSET_PATH = "Assets/Guru/guru_spec.yaml";
        private const string DrawGameConfigKey = "drawgame_config";
        private const string IWannerGameConfigKey = "iwannergame_config";

        /// <summary>
        /// Global 表字段名，与 lubanconfig_tbglobalconst.json / ConfigManager.RemoteConfig 读取键一致。
        /// </summary>
        private static readonly string[] GlobalConstLevelConfigKeys =
        {
            CurrentRemoteConfigVersionKey,
            "showTipsFailCount",
            "showTipsTimeLimit",
            "ad_interstitialCD",
            "ad_rewardResetInterstitialCD",
            "ad_interstitialShowMinLevel",
            "ad_interstitialShowIntervalCount",
            "ad_interstitialShowFailedCount",
            "mainGame_interstitalAdLevelRange",
            "mainGame_interstitalAdRangeLevelCount",
            "mianGame_interstititalAdRangeFailedCount",
            "showRatingLevel",
            "open_gaps",
            "victoryHorizantalRewards",
            "gameTipsPopupCoinCost",
            "gameTipsPopupPassLevelCoinCost",
            "ad_dailyNOAdPopupAutoShowCount",
            "shop_adfreeCoinAutoShowCount",
            "dailyFreeCoinCount",
            "showVersionBoard",
            "getFreeSkinLevel",
            "freeSkinId",
            "isOpenPostcard",
            "postcardGuideLevel",
            "gameTipsGuideLevel",
            "gameTipsGuideFailedCount",
            "gameTipsGuideWaitingTime",
            "dailyChallengeIsOpen",
            "dailyChallengeStep1CoinReward",
            "dailyChallengeStep2CoinReward",
            "dailyChallengeSteps",
            "dailyChallengeUnlockLevel",
            "dailyChallengeRandomSkinPoolId",
            "dailyChallengeRewardMultiple",
            "isOpenDailyChest",
            "autoShowChestGiftLevel"
        };

        private static readonly string[] LegacyLevelConfigKeysToRemove =
        {
            "victoryHorziontalRewards",
            "shopFreeCoinShowCount",
        };

        /// <summary>
        /// drawgame_config 需要从 GlobalConst 同步的 DrawGame 专用字段。
        /// 字段名必须与 ConfigManager.RemoteConfig 的读取键保持一致。
        /// </summary>
        private static readonly string[] GlobalConstDrawGameConfigKeys =
        {
            "drawGameIsOpen",
            "drawGameShowTipsFailCount",
            "drawGameShowTipsTimeLimit",
            "drawGame_interstitialCD",
            "drawGame_rewardResetInterstitialCD",
            "drawGame_interstitialShowMinLevel",
            "drawGame_interstitialShowIntervalCount",
            "drawGame_interstitialShowFailedCount",
            "drawGame_interstitalAdLevelRange",
            "drawGame_interstitalAdRangeLevelCount",
            "drawGame_interstititalAdRangeFailedCount",
            "drawGame_dailyNOAdPopupAutoShowCount",
            "drawGame_gameTipsPopupCoinCost",
            "drawGame_gameTipsPopupPassLevelCoinCost"
        };

        /// <summary>
        /// diggame_config 需要从 GlobalConst 同步的 DigGame 专用字段。
        /// 字段名必须与 ConfigManager.RemoteConfig 的读取键保持一致。
        /// </summary>
        private static readonly string[] GlobalConstDigGameConfigKeys =
        {
            "digGameIsOpen",
            "digGameShowTipsFailCount",
            "digGameShowTipsTimeLimit",
            "digGame_interstitialCD",
            "digGame_rewardResetInterstitialCD",
            "digGame_interstitialShowMinLevel",
            "digGame_interstitialShowIntervalCount",
            "digGame_interstitialShowFailedCount",
            "digGame_interstitalAdLevelRange",
            "digGame_interstitalAdRangeLevelCount",
            "digGame_interstititalAdRangeFailedCount",
            "digGame_dailyNOAdPopupAutoShowCount",
            "digGame_gameTipsPopupCoinCost",
            "digGame_gameTipsPopupPassLevelCoinCost"
        };

        /// <summary>
        /// iwannergame_config 需要从 GlobalConst 同步的 IWannerGame 专用字段。
        /// 字段名必须与 ConfigManager.RemoteConfig 的读取键保持一致。
        /// </summary>
        private static readonly string[] GlobalConstIWannerGameConfigKeys =
        {
            "iWannerGame_IsOpen",
            "iWannerGame_showSkipFailCount",
            "iWannerGame_interstitialCD",
            "iWannerGame_rewardResetInterstitialCD",
            "iWannerGame_interstitialShowMinGroup",
            "iWannerGame_interstitialShowIntervalCount",
            "iWannerGame_interstitialShowFailedCount",
            "iWannerGame_dailyNOAdPopupAutoShowCount",
            "iWannerGame_interstitialShowPassedLevelCount",
            "iwannarGame_interstitalAdLevelRange",
            "iwannarGame_interstitalAdRangeLevelCount",
            "iwannarGame_interstititalAdRangeFailedCount"
        };

        /// <summary>
        /// 已从 IWannerGame GlobalConst 移除的历史字段。
        /// 更新 guru_spec.yaml 时仅清理这些已确认废弃字段，其它未知扩展字段继续保留。
        /// </summary>
        private static readonly string[] DeprecatedIWannerGameConfigKeys =
        {
            "iWannerGame_ShowTipsTimeLimit",
            "iWannerGame_interstitialShowMinLevel"
        };
        
        [MenuItem(MENU_PATH, false, 1)]
        public static void GenerateConfig()
        {
            string genBatPath = GetGenBatPath();
            
            if (string.IsNullOrEmpty(genBatPath))
            {
                EditorUtility.DisplayDialog("Error", 
                    $"找不到 {GEN_BAT_NAME} 文件！\n\n请确保文件存在于：\nLuban/Configs/gen.bat", 
                    "OK");
                return;
            }

            if (!File.Exists(genBatPath))
            {
                EditorUtility.DisplayDialog("Error", 
                    $"文件不存在：\n{genBatPath}", 
                    "OK");
                return;
            }

            // 确认生成
            if (!EditorUtility.DisplayDialog("Generate Config", 
                $"即将执行配置生成：\n\n{genBatPath}\n\n这可能会覆盖现有的配置代码文件。", 
                "确定", "取消"))
            {
                return;
            }

            ExecuteGenBat(genBatPath);
        }

        /// <summary>
        /// 按主线排序、主线常量、DrawGame、IWannerGame、DigGame 的顺序统一更新 Guru YAML。
        /// 调用边界：仅供 Unity Editor 菜单使用；全部内容校验通过后才写入，并在最后统一触发一次 Guru 代码生成。
        /// </summary>
        [MenuItem(MENU_UPDATE_ALL_GURU_CONFIGS, false, 10)]
        public static void UpdateAllGuruConfigsToGuruSpec()
        {
            ApplyAllGuruConfigsToYamlAsset(GURU_SPEC_YAML_ASSET_PATH);
        }

        public static void UpdateLevelSortToGuruSpec()
        {
            ApplyLevelSortTableToYamlAsset(GURU_SPEC_YAML_ASSET_PATH);
        }

        public static void UpdateGlobalConstToGuruSpec()
        {
            ApplyGlobalConstTableToYamlAsset(GURU_SPEC_YAML_ASSET_PATH);
        }

        /// <summary>
        /// 从 GlobalConst 与 TbDrawLevelSort 生成或更新 Guru YAML 的 drawgame_config 默认值。
        /// 注意事项：保留已有 enabled 和未知扩展字段，只覆盖 DrawGame 专用字段与 level_sort。
        /// </summary>
        public static void UpdateDrawGameConfigToGuruSpec()
        {
            ApplyDrawGameConfigToYamlAsset(GURU_SPEC_YAML_ASSET_PATH);
        }

        /// <summary>
        /// 从 GlobalConst 与 TbDigLevelSort 生成或更新 Guru YAML 的 diggame_config 默认值。
        /// 注意事项：保留已有 enabled 和未知扩展字段，只覆盖 DigGame 专用字段与 level_sort。
        /// </summary>
        public static void UpdateDigGameConfigToGuruSpec()
        {
            ApplyDigGameConfigToYamlAsset(GURU_SPEC_YAML_ASSET_PATH);
        }

        /// <summary>
        /// 从 GlobalConst 生成或更新 Guru YAML 的 iwannergame_config 默认值。
        /// 注意事项：只覆盖 IWannerGame 专用字段，保留已有未知扩展字段。
        /// </summary>
        public static void UpdateIWannerGameConfigToGuruSpec()
        {
            ApplyIWannerGameConfigToYamlAsset(GURU_SPEC_YAML_ASSET_PATH);
        }

        private static void ApplyAllGuruConfigsToYamlAsset(string yamlAssetPath)
        {
            string projectRoot = GetProjectRoot();
            if (string.IsNullOrEmpty(projectRoot))
            {
                EditorUtility.DisplayDialog("Error", "无法定位项目根目录。", "OK");
                return;
            }

            string levelSortAbsPath = ToAbsolutePath(projectRoot, LEVELSORT_JSON_ASSET_PATH);
            string drawLevelSortAbsPath = ToAbsolutePath(projectRoot, DRAW_LEVELSORT_JSON_ASSET_PATH);
            string digLevelSortAbsPath = ToAbsolutePath(projectRoot, DIG_LEVELSORT_JSON_ASSET_PATH);
            string globalConstAbsPath = ToAbsolutePath(projectRoot, GLOBALCONST_JSON_ASSET_PATH);
            string yamlAbsPath = ToAbsolutePath(projectRoot, yamlAssetPath);
            var requiredFiles = new[]
            {
                levelSortAbsPath,
                drawLevelSortAbsPath,
                digLevelSortAbsPath,
                globalConstAbsPath,
                yamlAbsPath
            };
            string[] missingFiles = requiredFiles.Where(path => !File.Exists(path)).ToArray();
            if (missingFiles.Length > 0)
            {
                EditorUtility.DisplayDialog(
                    "Error",
                    $"Guru YAML 更新所需文件不完整：\n\n{string.Join("\n", missingFiles)}",
                    "OK");
                return;
            }

            if (!TryLoadGlobalConstRow(globalConstAbsPath, out JObject globalRow, out string loadError))
            {
                EditorUtility.DisplayDialog("Error", loadError, "OK");
                return;
            }

            List<string> levelSortPaths = ExtractPathListFromLevelSortJson(levelSortAbsPath);
            List<string> drawLevelSortPaths = ExtractPathListFromLevelSortJson(drawLevelSortAbsPath);
            List<string> digLevelSortPaths = ExtractPathListFromLevelSortJson(digLevelSortAbsPath);
            if (levelSortPaths.Count == 0 || drawLevelSortPaths.Count == 0 || digLevelSortPaths.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Error",
                    $"未提取到完整关卡排序：\n\n主线：{levelSortPaths.Count} 条\nDrawGame：{drawLevelSortPaths.Count} 条",
                    "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Update All Guru Configs",
                    $"即将统一更新 `{yamlAssetPath}`：\n\n" +
                    $"1. 主线关卡排序（{levelSortPaths.Count} 条）\n" +
                    "2. 主线 GlobalConst\n" +
                    $"3. DrawGame 配置与排序（{drawLevelSortPaths.Count} 条）\n" +
                    "4. IWannerGame 配置\n" +
                    $"5. DigGame 配置与排序（{digLevelSortPaths.Count} 条）\n\n" +
                    "全部内容校验通过后一次写入，并统一生成 GuruAllSpecs.G.cs。",
                    "确定",
                    "取消"))
            {
                return;
            }

            bool allLevelSortValuesAreInt = levelSortPaths.All(path => int.TryParse(path, out _));
            string levelSortArrayText = allLevelSortValuesAreInt
                ? string.Join(",", levelSortPaths.Select(path => int.Parse(path).ToString()))
                : string.Join(",", levelSortPaths.Select(path => $"\"{EscapeJsonString(path)}\""));

            try
            {
                string originalYamlContent = File.ReadAllText(yamlAbsPath);
                if (!TryUpdateGuruSpecLevelSort(
                        originalYamlContent,
                        levelSortArrayText,
                        yamlAssetPath,
                        out string levelSortYamlContent,
                        out string levelSortError))
                {
                    EditorUtility.DisplayDialog("Error", $"主线关卡排序更新失败：\n\n{levelSortError}", "OK");
                    return;
                }

                if (!TryMergeGlobalConstIntoLevelConfig(
                        levelSortYamlContent,
                        globalRow,
                        yamlAssetPath,
                        out string globalConstYamlContent,
                        out string globalConstError))
                {
                    EditorUtility.DisplayDialog("Error", $"主线 GlobalConst 更新失败：\n\n{globalConstError}", "OK");
                    return;
                }

                if (!TryMergeDrawGameConfig(
                        globalConstYamlContent,
                        globalRow,
                        drawLevelSortPaths,
                        yamlAssetPath,
                        out string drawGameYamlContent,
                        out string drawGameError))
                {
                    EditorUtility.DisplayDialog("Error", $"DrawGame 配置更新失败：\n\n{drawGameError}", "OK");
                    return;
                }

                if (!TryMergeIWannerGameConfig(
                        drawGameYamlContent,
                        globalRow,
                        yamlAssetPath,
                        out string iwannerYamlContent,
                        out string iWannerGameError))
                {
                    EditorUtility.DisplayDialog("Error", $"IWannerGame 配置更新失败：\n\n{iWannerGameError}", "OK");
                    return;
                }

                if (!TryMergeDigGameConfig(
                        iwannerYamlContent,
                        globalRow,
                        digLevelSortPaths,
                        yamlAssetPath,
                        out string finalYamlContent,
                        out string digError))
                {
                    EditorUtility.DisplayDialog("Error", $"DigGame 配置更新失败：\n\n{digError}", "OK");
                    return;
                }

                if (finalYamlContent != originalYamlContent)
                {
                    File.WriteAllText(yamlAbsPath, finalYamlContent);
                    AssetDatabase.ImportAsset(yamlAssetPath);
                    UnityEngine.Debug.Log($"[LubanGenerator] 已统一更新 {yamlAssetPath} 的五组 Guru 配置。");
                }
                else
                {
                    UnityEngine.Debug.Log($"[LubanGenerator] {yamlAssetPath} 的五组 Guru 配置均无变化。");
                }

                if (!AutoGenerateGuruAllSpecs())
                {
                    return;
                }

                EditorUtility.DisplayDialog(
                    finalYamlContent == originalYamlContent ? "No Changes" : "Success",
                    finalYamlContent == originalYamlContent
                        ? "五组配置与 Guru YAML 中一致，未写入；已重新触发 Guru 代码生成。"
                        : $"已统一更新 {yamlAssetPath}，并触发 Guru 代码生成。",
                    "OK");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[LubanGenerator] 统一更新 Guru YAML 失败: {e}");
                EditorUtility.DisplayDialog("Error", $"统一更新失败：\n\n{e.Message}", "OK");
            }
        }

        private static void ApplyDrawGameConfigToYamlAsset(string yamlAssetPath)
        {
            string projectRoot = GetProjectRoot();
            if (string.IsNullOrEmpty(projectRoot))
            {
                EditorUtility.DisplayDialog("Error", "无法定位项目根目录。", "OK");
                return;
            }

            string globalConstAbsPath = ToAbsolutePath(projectRoot, GLOBALCONST_JSON_ASSET_PATH);
            string drawLevelSortAbsPath = ToAbsolutePath(projectRoot, DRAW_LEVELSORT_JSON_ASSET_PATH);
            string yamlAbsPath = ToAbsolutePath(projectRoot, yamlAssetPath);
            if (!File.Exists(globalConstAbsPath) ||
                !File.Exists(drawLevelSortAbsPath) ||
                !File.Exists(yamlAbsPath))
            {
                EditorUtility.DisplayDialog(
                    "Error",
                    $"DrawGameConfig 输入文件不完整：\n\n{globalConstAbsPath}\n{drawLevelSortAbsPath}\n{yamlAbsPath}",
                    "OK");
                return;
            }

            if (!TryLoadGlobalConstRow(globalConstAbsPath, out JObject globalRow, out string loadError))
            {
                EditorUtility.DisplayDialog("Error", loadError, "OK");
                return;
            }

            List<string> drawLevelSortPaths = ExtractPathListFromLevelSortJson(drawLevelSortAbsPath);
            if (drawLevelSortPaths.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Error",
                    $"未从表提取到任何 path：\n\n{DRAW_LEVELSORT_JSON_ASSET_PATH}",
                    "OK");
                return;
            }

            string preview = string.Join(", ", drawLevelSortPaths.Take(15));
            if (drawLevelSortPaths.Count > 15)
                preview += ", ...";

            if (!EditorUtility.DisplayDialog(
                    "Update DrawGameConfig",
                    $"即将把 DrawGame GlobalConst 字段和 `{DRAW_LEVELSORT_JSON_ASSET_PATH}` 顺序写入 " +
                    $"`{yamlAssetPath}` 的 {DrawGameConfigKey}。\n\n" +
                    $"关卡数量：{drawLevelSortPaths.Count}\n预览：[{preview}]\n\n" +
                    "保留已有 enabled 和未知扩展字段；更新 DrawGame 专用字段与 level_sort。",
                    "确定",
                    "取消"))
            {
                return;
            }

            try
            {
                string yamlContent = File.ReadAllText(yamlAbsPath);
                if (!TryMergeDrawGameConfig(
                        yamlContent,
                        globalRow,
                        drawLevelSortPaths,
                        yamlAssetPath,
                        out string newYamlContent,
                        out string error))
                {
                    EditorUtility.DisplayDialog("Error", error, "OK");
                    return;
                }

                if (newYamlContent == yamlContent)
                {
                    AutoGenerateGuruAllSpecs();
                    EditorUtility.DisplayDialog("No Changes", "DrawGameConfig 与 YAML 中一致，未写入。", "OK");
                    return;
                }

                File.WriteAllText(yamlAbsPath, newYamlContent);
                AssetDatabase.ImportAsset(yamlAssetPath);
                UnityEngine.Debug.Log(
                    $"[LubanGenerator] 已更新 {yamlAssetPath} 的 {DrawGameConfigKey}（{drawLevelSortPaths.Count} 条关卡）。");
                AutoGenerateGuruAllSpecs();
                EditorUtility.DisplayDialog("Success", $"已更新 {yamlAssetPath}。", "OK");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[LubanGenerator] 更新 DrawGameConfig 失败: {e}");
                EditorUtility.DisplayDialog("Error", $"更新失败：\n\n{e.Message}", "OK");
            }
        }

        private static void ApplyDigGameConfigToYamlAsset(string yamlAssetPath)
        {
            string projectRoot = GetProjectRoot();
            if (string.IsNullOrEmpty(projectRoot))
            {
                EditorUtility.DisplayDialog("Error", "无法定位项目根目录。", "OK");
                return;
            }

            string globalConstAbsPath = ToAbsolutePath(projectRoot, GLOBALCONST_JSON_ASSET_PATH);
            string digLevelSortAbsPath = ToAbsolutePath(projectRoot, DIG_LEVELSORT_JSON_ASSET_PATH);
            string yamlAbsPath = ToAbsolutePath(projectRoot, yamlAssetPath);
            if (!File.Exists(globalConstAbsPath) ||
                !File.Exists(digLevelSortAbsPath) ||
                !File.Exists(yamlAbsPath))
            {
                EditorUtility.DisplayDialog(
                    "Error",
                    $"DigGameConfig 输入文件不完整：\n\n{globalConstAbsPath}\n{digLevelSortAbsPath}\n{yamlAbsPath}",
                    "OK");
                return;
            }

            if (!TryLoadGlobalConstRow(globalConstAbsPath, out JObject globalRow, out string loadError))
            {
                EditorUtility.DisplayDialog("Error", loadError, "OK");
                return;
            }

            List<string> digLevelSortPaths = ExtractPathListFromLevelSortJson(digLevelSortAbsPath);
            if (digLevelSortPaths.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Error",
                    $"未从表提取到任何 path：\n\n{DIG_LEVELSORT_JSON_ASSET_PATH}",
                    "OK");
                return;
            }

            string preview = string.Join(", ", digLevelSortPaths.Take(15));
            if (digLevelSortPaths.Count > 15)
                preview += ", ...";

            if (!EditorUtility.DisplayDialog(
                    "Update DigGameConfig",
                    $"即将把 DigGame GlobalConst 字段和 `{DIG_LEVELSORT_JSON_ASSET_PATH}` 顺序写入 " +
                    $"`{yamlAssetPath}` 的 {DigGameConfigKey}。\n\n" +
                    $"关卡数量：{digLevelSortPaths.Count}\n预览：[{preview}]\n\n" +
                    "保留已有 enabled 和未知扩展字段；更新 DigGame 专用字段与 level_sort。",
                    "确定",
                    "取消"))
            {
                return;
            }

            try
            {
                string yamlContent = File.ReadAllText(yamlAbsPath);
                if (!TryMergeDigGameConfig(
                        yamlContent,
                        globalRow,
                        digLevelSortPaths,
                        yamlAssetPath,
                        out string newYamlContent,
                        out string error))
                {
                    EditorUtility.DisplayDialog("Error", error, "OK");
                    return;
                }

                if (newYamlContent == yamlContent)
                {
                    AutoGenerateGuruAllSpecs();
                    EditorUtility.DisplayDialog("No Changes", "DigGameConfig 与 YAML 中一致，未写入。", "OK");
                    return;
                }

                File.WriteAllText(yamlAbsPath, newYamlContent);
                AssetDatabase.ImportAsset(yamlAssetPath);
                UnityEngine.Debug.Log(
                    $"[LubanGenerator] 已更新 {yamlAssetPath} 的 {DigGameConfigKey}（{digLevelSortPaths.Count} 条关卡）。");
                AutoGenerateGuruAllSpecs();
                EditorUtility.DisplayDialog("Success", $"已更新 {yamlAssetPath}。", "OK");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[LubanGenerator] 更新 DigGameConfig 失败: {e}");
                EditorUtility.DisplayDialog("Error", $"更新失败：\n\n{e.Message}", "OK");
            }
        }

        private static void ApplyIWannerGameConfigToYamlAsset(string yamlAssetPath)
        {
            string projectRoot = GetProjectRoot();
            if (string.IsNullOrEmpty(projectRoot))
            {
                EditorUtility.DisplayDialog("Error", "无法定位项目根目录。", "OK");
                return;
            }

            string globalConstAbsPath = ToAbsolutePath(projectRoot, GLOBALCONST_JSON_ASSET_PATH);
            string yamlAbsPath = ToAbsolutePath(projectRoot, yamlAssetPath);
            if (!File.Exists(globalConstAbsPath) || !File.Exists(yamlAbsPath))
            {
                EditorUtility.DisplayDialog(
                    "Error",
                    $"IWannerGameConfig 输入文件不完整：\n\n{globalConstAbsPath}\n{yamlAbsPath}",
                    "OK");
                return;
            }

            if (!TryLoadGlobalConstRow(globalConstAbsPath, out JObject globalRow, out string loadError))
            {
                EditorUtility.DisplayDialog("Error", loadError, "OK");
                return;
            }

            var previewLines = new List<string>(GlobalConstIWannerGameConfigKeys.Length);
            foreach (string key in GlobalConstIWannerGameConfigKeys)
            {
                previewLines.Add(globalRow.TryGetValue(key, out JToken token)
                    ? $"{key}: {FormatJsonTokenForPreview(token)}"
                    : $"{key}: <missing>");
            }

            if (!EditorUtility.DisplayDialog(
                    "Update IWannerGameConfig",
                    $"即将把 IWannerGame GlobalConst 字段写入 `{yamlAssetPath}` 的 " +
                    $"{IWannerGameConfigKey}。\n\n{string.Join("\n", previewLines)}\n\n" +
                    "保留已有未知扩展字段；只更新 IWannerGame 专用字段。",
                    "确定",
                    "取消"))
            {
                return;
            }

            try
            {
                string yamlContent = File.ReadAllText(yamlAbsPath);
                if (!TryMergeIWannerGameConfig(
                        yamlContent,
                        globalRow,
                        yamlAssetPath,
                        out string newYamlContent,
                        out string error))
                {
                    EditorUtility.DisplayDialog("Error", error, "OK");
                    return;
                }

                if (newYamlContent == yamlContent)
                {
                    AutoGenerateGuruAllSpecs();
                    EditorUtility.DisplayDialog("No Changes", "IWannerGameConfig 与 YAML 中一致，未写入。", "OK");
                    return;
                }

                File.WriteAllText(yamlAbsPath, newYamlContent);
                AssetDatabase.ImportAsset(yamlAssetPath);
                UnityEngine.Debug.Log(
                    $"[LubanGenerator] 已更新 {yamlAssetPath} 的 {IWannerGameConfigKey}。");
                AutoGenerateGuruAllSpecs();
                EditorUtility.DisplayDialog("Success", $"已更新 {yamlAssetPath}。", "OK");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[LubanGenerator] 更新 IWannerGameConfig 失败: {e}");
                EditorUtility.DisplayDialog("Error", $"更新失败：\n\n{e.Message}", "OK");
            }
        }

        private static void ApplyGlobalConstTableToYamlAsset(string yamlAssetPath)
        {
            string projectRoot = GetProjectRoot();
            if (string.IsNullOrEmpty(projectRoot))
            {
                EditorUtility.DisplayDialog("Error", "无法定位项目根目录。", "OK");
                return;
            }

            string jsonAbsPath = ToAbsolutePath(projectRoot, GLOBALCONST_JSON_ASSET_PATH);
            string yamlAbsPath = ToAbsolutePath(projectRoot, yamlAssetPath);

            if (!File.Exists(jsonAbsPath))
            {
                EditorUtility.DisplayDialog("Error", $"找不到 Global 表：\n\n{jsonAbsPath}", "OK");
                return;
            }

            if (!File.Exists(yamlAbsPath))
            {
                EditorUtility.DisplayDialog("Error", $"找不到 YAML：\n\n{yamlAbsPath}", "OK");
                return;
            }

            if (!TryLoadGlobalConstRow(jsonAbsPath, out JObject globalRow, out string loadError))
            {
                EditorUtility.DisplayDialog("Error", loadError, "OK");
                return;
            }

            string preview = BuildGlobalConstPreview(globalRow);
            if (!EditorUtility.DisplayDialog(
                    "Update GlobalConst",
                    $"即将把 `{GLOBALCONST_JSON_ASSET_PATH}` 的字段合并写入 `{yamlAssetPath}` 的 level_config。\n\n" +
                    $"{preview}\n\n" +
                    "仅更新 GlobalConst 对应键；保留 enabled、level_sort 等其它键；移除已废弃的旧键名。",
                    "确定",
                    "取消"))
            {
                return;
            }

            try
            {
                string yamlContent = File.ReadAllText(yamlAbsPath);
                if (!TryMergeGlobalConstIntoLevelConfig(yamlContent, globalRow, yamlAssetPath, out string newYamlContent, out string error))
                {
                    EditorUtility.DisplayDialog("Error", error, "OK");
                    return;
                }

                if (newYamlContent == yamlContent)
                {
                    AutoGenerateGuruAllSpecs();
                    EditorUtility.DisplayDialog("No Changes", "GlobalConst 字段与 YAML 中一致，未写入。", "OK");
                    return;
                }

                File.WriteAllText(yamlAbsPath, newYamlContent);
                AssetDatabase.ImportAsset(yamlAssetPath);
                UnityEngine.Debug.Log($"[LubanGenerator] 已将 {GLOBALCONST_JSON_ASSET_PATH} 合并写入 {yamlAssetPath} 的 level_config。");
                AutoGenerateGuruAllSpecs();
                EditorUtility.DisplayDialog("Success", $"已更新 {yamlAssetPath}。", "OK");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[LubanGenerator] 更新 YAML 失败: {e}");
                EditorUtility.DisplayDialog("Error", $"更新失败：\n\n{e.Message}", "OK");
            }
        }

        private static void ApplyLevelSortTableToYamlAsset(string yamlAssetPath)
        {
            string projectRoot = GetProjectRoot();
            if (string.IsNullOrEmpty(projectRoot))
            {
                EditorUtility.DisplayDialog("Error", "无法定位项目根目录。", "OK");
                return;
            }

            string jsonAbsPath = ToAbsolutePath(projectRoot, LEVELSORT_JSON_ASSET_PATH);
            string yamlAbsPath = ToAbsolutePath(projectRoot, yamlAssetPath);

            if (!File.Exists(jsonAbsPath))
            {
                EditorUtility.DisplayDialog("Error", $"找不到 levelsort 表：\n\n{jsonAbsPath}", "OK");
                return;
            }

            if (!File.Exists(yamlAbsPath))
            {
                EditorUtility.DisplayDialog("Error", $"找不到 YAML：\n\n{yamlAbsPath}", "OK");
                return;
            }

            List<string> levelSortPaths = ExtractPathListFromLevelSortJson(jsonAbsPath);
            if (levelSortPaths.Count == 0)
            {
                EditorUtility.DisplayDialog("Error", $"未从表提取到任何 path：\n\n{LEVELSORT_JSON_ASSET_PATH}", "OK");
                return;
            }

            string preview = string.Join(", ", levelSortPaths.Take(15));
            if (levelSortPaths.Count > 15)
            {
                preview += ", ...";
            }

            if (!EditorUtility.DisplayDialog(
                    "Update LevelSort",
                    $"即将把 `{LEVELSORT_JSON_ASSET_PATH}` 的关卡顺序写入 `{yamlAssetPath}` 的 level_config。\n\n" +
                    $"数量：{levelSortPaths.Count}\n预览：[{preview}]\n\n" +
                    "仅替换 level_config JSON 内的 level_sort / levelsort 数组，其余 YAML 与键名保持不变。",
                    "确定",
                    "取消"))
            {
                return;
            }

            bool allInt = levelSortPaths.All(p => int.TryParse(p, out _));
            string arrayText = allInt
                ? string.Join(",", levelSortPaths.Select(p => int.Parse(p).ToString()))
                : string.Join(",", levelSortPaths.Select(p => $"\"{EscapeJsonString(p)}\""));

            try
            {
                string yamlContent = File.ReadAllText(yamlAbsPath);
                if (!TryUpdateGuruSpecLevelSort(yamlContent, arrayText, yamlAssetPath, out string newYamlContent, out string error))
                {
                    EditorUtility.DisplayDialog("Error", error, "OK");
                    return;
                }

                if (newYamlContent == yamlContent)
                {
                    AutoGenerateGuruAllSpecs();
                    EditorUtility.DisplayDialog("No Changes", "关卡序列与 YAML 中一致，未写入。", "OK");
                    return;
                }

                File.WriteAllText(yamlAbsPath, newYamlContent);
                AssetDatabase.ImportAsset(yamlAssetPath);
                UnityEngine.Debug.Log($"[LubanGenerator] 已将 {LEVELSORT_JSON_ASSET_PATH} 的关卡顺序写入 {yamlAssetPath}（{levelSortPaths.Count} 条）。");
                AutoGenerateGuruAllSpecs();
                EditorUtility.DisplayDialog("Success", $"已更新 {yamlAssetPath}。", "OK");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[LubanGenerator] 更新 YAML 失败: {e}");
                EditorUtility.DisplayDialog("Error", $"更新失败：\n\n{e.Message}", "OK");
            }
        }

        /// <summary>
        /// 获取 gen.bat 文件路径
        /// </summary>
        private static string GetGenBatPath()
        {
            // 从 Assets 目录向上查找 Luban/Configs/gen.bat
            string assetsPath = Application.dataPath.Replace('\\', '/');
            string projectRoot = Directory.GetParent(assetsPath)?.FullName.Replace('\\', '/');
            
            if (string.IsNullOrEmpty(projectRoot))
            {
                return null;
            }

            string genBatPath = Path.Combine(projectRoot, "Luban", "Configs", GEN_BAT_NAME).Replace('\\', '/');
            return genBatPath;
        }

        private static string GetProjectRoot()
        {
            string assetsPath = Application.dataPath.Replace('\\', '/');
            return Directory.GetParent(assetsPath)?.FullName.Replace('\\', '/');
        }

        private static string ToAbsolutePath(string projectRoot, string assetPath)
        {
            if (string.IsNullOrEmpty(projectRoot) || string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            return Path.Combine(projectRoot, assetPath).Replace('\\', '/');
        }

        /// <summary>
        /// 触发 Guru 官方 Spec 代码生成菜单；仅供本工具的配置同步命令在正常完成后调用。
        /// </summary>
        private static bool AutoGenerateGuruAllSpecs()
        {
            if (EditorApplication.ExecuteMenuItem(GURU_SPEC_AUTO_GEN_MENU))
            {
                UnityEngine.Debug.Log($"[LubanGenerator] 已自动触发 {GURU_SPEC_AUTO_GEN_MENU}。");
                return true;
            }

            UnityEngine.Debug.LogError($"[LubanGenerator] 未找到 Guru Spec 生成菜单：{GURU_SPEC_AUTO_GEN_MENU}");
            EditorUtility.DisplayDialog(
                "Guru Spec Auto-Gen Failed",
                $"未找到菜单：\n\n{GURU_SPEC_AUTO_GEN_MENU}\n\n请确认 Guru Spec 编辑器包已正确加载。",
                "OK");
            return false;
        }

        private static List<string> ExtractPathListFromLevelSortJson(string jsonAbsPath)
        {
            string json = File.ReadAllText(jsonAbsPath);

            // 轻量解析：仅提取 "path":"xxx"。避免 Unity JsonUtility 顶层数组限制。
            Regex regex = new Regex(@"""path""\s*:\s*""([^""]+)""", RegexOptions.Multiline);
            MatchCollection matches = regex.Matches(json);

            List<string> result = new List<string>(matches.Count);
            foreach (Match match in matches)
            {
                if (match.Groups.Count < 2)
                {
                    continue;
                }

                string value = match.Groups[1].Value?.Trim();
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                result.Add(value);
            }

            return result;
        }

        private static bool TryLoadGlobalConstRow(string jsonAbsPath, out JObject globalRow, out string error)
        {
            globalRow = null;
            error = null;

            try
            {
                JArray root = JArray.Parse(File.ReadAllText(jsonAbsPath));
                JObject firstRow = root.Count > 0 ? root[0] as JObject : null;
                if (firstRow == null)
                {
                    error = $"Global 表格式无效（需为非空 JSON 数组）：\n\n{GLOBALCONST_JSON_ASSET_PATH}";
                    return false;
                }

                globalRow = firstRow;
                return true;
            }
            catch (Exception e)
            {
                error = $"解析 Global 表失败：\n\n{e.Message}";
                return false;
            }
        }

        private static string BuildGlobalConstPreview(JObject globalRow)
        {
            var lines = new List<string>(GlobalConstLevelConfigKeys.Length);
            foreach (string key in GlobalConstLevelConfigKeys)
            {
                if (!TryGetLevelConfigSourceToken(globalRow, key, out JToken token))
                {
                    lines.Add($"{key}: <missing>");
                    continue;
                }

                string suffix = string.Equals(key, CurrentRemoteConfigVersionKey, StringComparison.Ordinal)
                    ? " (from Application.version)"
                    : string.Empty;
                lines.Add($"{key}: {FormatJsonTokenForPreview(token)}{suffix}");
            }

            return string.Join("\n", lines);
        }

        private static string FormatJsonTokenForPreview(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return "null";
            }

            if (token is JArray array)
            {
                var values = new List<string>(array.Count);
                foreach (JToken child in array.Children())
                {
                    values.Add(child.ToString());
                }

                return $"[{string.Join(", ", values)}]";
            }

            return token.ToString();
        }

        private static bool TryMergeGlobalConstIntoLevelConfig(
            string yamlContent,
            JObject globalRow,
            string yamlPathForErrors,
            out string newYamlContent,
            out string error)
        {
            newYamlContent = yamlContent;
            error = null;

            if (!TryGetLevelConfigLineMatch(yamlContent, yamlPathForErrors, out Match match, out error))
            {
                return false;
            }

            string jsonText = match.Groups["json"].Value;
            JObject levelConfig;
            try
            {
                levelConfig = JObject.Parse(jsonText);
            }
            catch (Exception e)
            {
                error = $"level_config JSON 解析失败：{e.Message}\n\n{yamlPathForErrors}";
                return false;
            }

            foreach (string key in GlobalConstLevelConfigKeys)
            {
                if (!TryGetLevelConfigSourceToken(globalRow, key, out JToken sourceToken))
                {
                    error = $"Global 表缺少字段 `{key}`。\n\n{GLOBALCONST_JSON_ASSET_PATH}";
                    return false;
                }

                levelConfig[key] = sourceToken.DeepClone();
            }

            foreach (string legacyKey in LegacyLevelConfigKeysToRemove)
            {
                levelConfig.Remove(legacyKey);
            }

            List<string> keyOrder = BuildLevelConfigKeyOrder(jsonText, levelConfig);
            string newJson = SerializeLevelConfigJson(levelConfig, keyOrder);
            if (newJson == jsonText)
            {
                return true;
            }

            return TryReplaceLevelConfigLine(yamlContent, match, newJson, out newYamlContent);
        }

        private static bool TryGetLevelConfigSourceToken(JObject globalRow, string key, out JToken token)
        {
            token = null;

            if (string.Equals(key, CurrentRemoteConfigVersionKey, StringComparison.Ordinal))
            {
                token = JValue.CreateString(Application.version);
                return true;
            }

            return globalRow.TryGetValue(key, out token);
        }

        private static bool TryMergeDrawGameConfig(
            string yamlContent,
            JObject globalRow,
            IReadOnlyList<string> drawLevelSortPaths,
            string yamlPathForErrors,
            out string newYamlContent,
            out string error)
        {
            newYamlContent = yamlContent;
            error = null;

            Regex drawGameLineRegex = new Regex(
                $@"^(?<indent>\s*){Regex.Escape(DrawGameConfigKey)}:\s*'(?<json>\{{[^']*\}})'\s*$",
                RegexOptions.Multiline);
            Match drawGameMatch = drawGameLineRegex.Match(yamlContent);
            JObject drawGameConfig;
            if (drawGameMatch.Success)
            {
                try
                {
                    drawGameConfig = JObject.Parse(drawGameMatch.Groups["json"].Value);
                }
                catch (Exception e)
                {
                    error = $"{DrawGameConfigKey} JSON 解析失败：{e.Message}\n\n{yamlPathForErrors}";
                    return false;
                }
            }
            else
            {
                drawGameConfig = new JObject
                {
                    ["enabled"] = false
                };
            }

            foreach (string key in GlobalConstDrawGameConfigKeys)
            {
                if (!globalRow.TryGetValue(key, out JToken sourceToken))
                {
                    error = $"Global 表缺少 DrawGame 字段 `{key}`。\n\n{GLOBALCONST_JSON_ASSET_PATH}";
                    return false;
                }

                drawGameConfig[key] = sourceToken.DeepClone();
            }

            var levelSort = new JArray();
            for (int i = 0; i < drawLevelSortPaths.Count; i++)
            {
                string path = drawLevelSortPaths[i];
                levelSort.Add(int.TryParse(path, out int numericPath)
                    ? new JValue(numericPath)
                    : new JValue(path));
            }

            drawGameConfig["level_sort"] = levelSort;

            var keyOrder = new List<string> { "enabled" };
            foreach (string key in GlobalConstDrawGameConfigKeys)
            {
                if (!keyOrder.Contains(key))
                    keyOrder.Add(key);
            }

            keyOrder.Add("level_sort");
            foreach (JProperty property in drawGameConfig.Properties())
            {
                if (!keyOrder.Contains(property.Name))
                    keyOrder.Add(property.Name);
            }

            string newJson = SerializeLevelConfigJson(drawGameConfig, keyOrder);
            if (drawGameMatch.Success)
            {
                string replacementLine =
                    $"{drawGameMatch.Groups["indent"].Value}{DrawGameConfigKey}: '{newJson}'";
                newYamlContent = ReplaceFirst(yamlContent, drawGameMatch.Value, replacementLine);
                return true;
            }

            if (!TryGetLevelConfigLineMatch(
                    yamlContent,
                    yamlPathForErrors,
                    out Match levelConfigMatch,
                    out error))
            {
                return false;
            }

            string newLine = yamlContent.Contains("\r\n") ? "\r\n" : "\n";
            string indent = levelConfigMatch.Groups["indent"].Value;
            string insertedLines =
                levelConfigMatch.Value + newLine + $"{indent}{DrawGameConfigKey}: '{newJson}'";
            newYamlContent = ReplaceFirst(yamlContent, levelConfigMatch.Value, insertedLines);
            return true;
        }

        private static bool TryMergeDigGameConfig(
            string yamlContent,
            JObject globalRow,
            IReadOnlyList<string> digLevelSortPaths,
            string yamlPathForErrors,
            out string newYamlContent,
            out string error)
        {
            newYamlContent = yamlContent;
            error = null;

            Regex digGameLineRegex = new Regex(
                $@"^(?<indent>\s*){Regex.Escape(DigGameConfigKey)}:\s*'(?<json>\{{[^']*\}})'\s*$",
                RegexOptions.Multiline);
            Match digGameMatch = digGameLineRegex.Match(yamlContent);
            JObject digGameConfig;
            if (digGameMatch.Success)
            {
                try
                {
                    digGameConfig = JObject.Parse(digGameMatch.Groups["json"].Value);
                }
                catch (Exception e)
                {
                    error = $"{DigGameConfigKey} JSON 解析失败：{e.Message}\n\n{yamlPathForErrors}";
                    return false;
                }
            }
            else
            {
                digGameConfig = new JObject
                {
                    ["enabled"] = false
                };
            }

            foreach (string key in GlobalConstDigGameConfigKeys)
            {
                if (!globalRow.TryGetValue(key, out JToken sourceToken))
                {
                    error = $"Global 表缺少 DigGame 字段 `{key}`。\n\n{GLOBALCONST_JSON_ASSET_PATH}";
                    return false;
                }

                digGameConfig[key] = sourceToken.DeepClone();
            }

            var levelSort = new JArray();
            for (int i = 0; i < digLevelSortPaths.Count; i++)
            {
                string path = digLevelSortPaths[i];
                levelSort.Add(int.TryParse(path, out int numericPath)
                    ? new JValue(numericPath)
                    : new JValue(path));
            }

            digGameConfig["level_sort"] = levelSort;

            var keyOrder = new List<string> { "enabled" };
            foreach (string key in GlobalConstDigGameConfigKeys)
            {
                if (!keyOrder.Contains(key))
                    keyOrder.Add(key);
            }

            keyOrder.Add("level_sort");
            foreach (JProperty property in digGameConfig.Properties())
            {
                if (!keyOrder.Contains(property.Name))
                    keyOrder.Add(property.Name);
            }

            string newJson = SerializeLevelConfigJson(digGameConfig, keyOrder);
            if (digGameMatch.Success)
            {
                string replacementLine =
                    $"{digGameMatch.Groups["indent"].Value}{DigGameConfigKey}: '{newJson}'";
                newYamlContent = ReplaceFirst(yamlContent, digGameMatch.Value, replacementLine);
                return true;
            }

            if (!TryGetLevelConfigLineMatch(
                    yamlContent,
                    yamlPathForErrors,
                    out Match levelConfigMatch,
                    out error))
            {
                return false;
            }

            string newLine = yamlContent.Contains("\r\n") ? "\r\n" : "\n";
            string indent = levelConfigMatch.Groups["indent"].Value;
            string insertedLines =
                levelConfigMatch.Value + newLine + $"{indent}{DigGameConfigKey}: '{newJson}'";
            newYamlContent = ReplaceFirst(yamlContent, levelConfigMatch.Value, insertedLines);
            return true;
        }

        private static bool TryMergeIWannerGameConfig(
            string yamlContent,
            JObject globalRow,
            string yamlPathForErrors,
            out string newYamlContent,
            out string error)
        {
            newYamlContent = yamlContent;
            error = null;

            Regex iwannerGameLineRegex = new Regex(
                $@"^(?<indent>\s*){Regex.Escape(IWannerGameConfigKey)}:\s*'(?<json>\{{[^']*\}})'\s*$",
                RegexOptions.Multiline);
            Match iwannerGameMatch = iwannerGameLineRegex.Match(yamlContent);
            JObject iwannerGameConfig;
            if (iwannerGameMatch.Success)
            {
                try
                {
                    iwannerGameConfig = JObject.Parse(iwannerGameMatch.Groups["json"].Value);
                }
                catch (Exception e)
                {
                    error = $"{IWannerGameConfigKey} JSON 解析失败：{e.Message}\n\n{yamlPathForErrors}";
                    return false;
                }
            }
            else
            {
                iwannerGameConfig = new JObject();
            }

            foreach (string deprecatedKey in DeprecatedIWannerGameConfigKeys)
            {
                iwannerGameConfig.Remove(deprecatedKey);
            }

            foreach (string key in GlobalConstIWannerGameConfigKeys)
            {
                if (!globalRow.TryGetValue(key, out JToken sourceToken))
                {
                    error = $"Global 表缺少 IWannerGame 字段 `{key}`。\n\n{GLOBALCONST_JSON_ASSET_PATH}";
                    return false;
                }

                iwannerGameConfig[key] = sourceToken.DeepClone();
            }

            var keyOrder = new List<string>(GlobalConstIWannerGameConfigKeys);
            foreach (JProperty property in iwannerGameConfig.Properties())
            {
                if (!keyOrder.Contains(property.Name))
                {
                    keyOrder.Add(property.Name);
                }
            }

            string newJson = SerializeLevelConfigJson(iwannerGameConfig, keyOrder);
            if (iwannerGameMatch.Success)
            {
                string replacementLine =
                    $"{iwannerGameMatch.Groups["indent"].Value}{IWannerGameConfigKey}: '{newJson}'";
                newYamlContent = ReplaceFirst(yamlContent, iwannerGameMatch.Value, replacementLine);
                return true;
            }

            Regex drawGameLineRegex = new Regex(
                $@"^(?<indent>\s*){Regex.Escape(DrawGameConfigKey)}:\s*'\{{[^']*\}}'\s*$",
                RegexOptions.Multiline);
            Match anchorMatch = drawGameLineRegex.Match(yamlContent);
            if (!anchorMatch.Success
                && !TryGetLevelConfigLineMatch(
                    yamlContent,
                    yamlPathForErrors,
                    out anchorMatch,
                    out error))
            {
                return false;
            }

            string newLine = yamlContent.Contains("\r\n") ? "\r\n" : "\n";
            string indent = anchorMatch.Groups["indent"].Value;
            string insertedLines =
                anchorMatch.Value + newLine + $"{indent}{IWannerGameConfigKey}: '{newJson}'";
            newYamlContent = ReplaceFirst(yamlContent, anchorMatch.Value, insertedLines);
            return true;
        }

        /// <summary>
        /// 只替换 level_config 单行 JSON 内 level_sort 或 levelsort 的数组部分，键名与 YAML 其它内容保持原样。
        /// </summary>
        private static bool TryUpdateGuruSpecLevelSort(
            string yamlContent,
            string levelsortArrayText,
            string yamlPathForErrors,
            out string newYamlContent,
            out string error)
        {
            newYamlContent = yamlContent;
            error = null;

            if (string.IsNullOrEmpty(levelsortArrayText))
            {
                error = "关卡 id 列表为空。";
                return false;
            }

            if (!TryGetLevelConfigLineMatch(yamlContent, yamlPathForErrors, out Match match, out error))
            {
                return false;
            }

            string json = match.Groups["json"].Value;
            Regex levelsortRegex = new Regex(@"""(?<key>levelsort|level_sort)""\s*:\s*\[[^\]]*\]", RegexOptions.IgnoreCase);
            Match keyMatch = levelsortRegex.Match(json);
            if (!keyMatch.Success)
            {
                error = $"level_config JSON 中未找到 level_sort 或 levelsort 数组。\n\n{yamlPathForErrors}";
                return false;
            }

            string key = keyMatch.Groups["key"].Value;
            string newJson = levelsortRegex.Replace(json, $"\"{key}\":[{levelsortArrayText}]", 1);
            if (newJson == json)
            {
                return true;
            }

            return TryReplaceLevelConfigLine(yamlContent, match, newJson, out newYamlContent);
        }

        private static bool TryGetLevelConfigLineMatch(
            string yamlContent,
            string yamlPathForErrors,
            out Match match,
            out string error)
        {
            match = null;
            error = null;

            if (string.IsNullOrEmpty(yamlContent))
            {
                error = "YAML 内容为空。";
                return false;
            }

            Regex lineRegex = new Regex(@"^(?<indent>\s*)level_config:\s*'(?<json>\{[^']*\})'\s*$", RegexOptions.Multiline);
            match = lineRegex.Match(yamlContent);
            if (!match.Success)
            {
                error = $"未在 `{yamlPathForErrors}` 中找到 level_config: '{{...}}' 行。";
                return false;
            }

            return true;
        }

        private static bool TryReplaceLevelConfigLine(
            string yamlContent,
            Match match,
            string newJson,
            out string newYamlContent)
        {
            string indent = match.Groups["indent"].Value;
            string oldLine = match.Value;
            string replacementLine = $"{indent}level_config: '{newJson}'";
            newYamlContent = ReplaceFirst(yamlContent, oldLine, replacementLine);
            return true;
        }

        private static List<string> BuildLevelConfigKeyOrder(string jsonText, JObject levelConfig)
        {
            var order = new List<string>();
            Regex keyRegex = new Regex(@"""(?<key>[^""]+)""\s*:");
            foreach (Match keyMatch in keyRegex.Matches(jsonText))
            {
                string key = keyMatch.Groups["key"].Value;
                if (!order.Contains(key))
                {
                    order.Add(key);
                }
            }

            foreach (string key in GlobalConstLevelConfigKeys)
            {
                if (levelConfig.ContainsKey(key) && !order.Contains(key))
                {
                    order.Add(key);
                }
            }

            foreach (JProperty property in levelConfig.Properties())
            {
                if (!order.Contains(property.Name))
                {
                    order.Add(property.Name);
                }
            }

            return order;
        }

        private static string SerializeLevelConfigJson(JObject levelConfig, IList<string> keyOrder)
        {
            var builder = new StringBuilder();
            builder.Append('{');

            bool first = true;
            foreach (string key in keyOrder)
            {
                if (!levelConfig.TryGetValue(key, out JToken value))
                {
                    continue;
                }

                if (!first)
                {
                    builder.Append(',');
                }

                first = false;
                builder.Append(' ');
                builder.Append('"');
                builder.Append(key);
                builder.Append("\":");
                AppendLevelConfigValue(builder, value);
            }

            builder.Append('}');
            return builder.ToString();
        }

        private static void AppendLevelConfigValue(StringBuilder builder, JToken value)
        {
            if (value == null || value.Type == JTokenType.Null)
            {
                builder.Append("null");
                return;
            }

            switch (value.Type)
            {
                case JTokenType.Boolean:
                    builder.Append(value.Value<bool>() ? "true" : "false");
                    return;
                case JTokenType.Integer:
                    builder.Append(value.Value<long>().ToString(CultureInfo.InvariantCulture));
                    return;
                case JTokenType.Float:
                    double number = value.Value<double>();
                    if (Math.Abs(number - Math.Round(number)) < 0.0001d)
                    {
                        builder.Append(((long)Math.Round(number)).ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(number.ToString(CultureInfo.InvariantCulture));
                    }

                    return;
                case JTokenType.Array:
                    builder.Append('[');
                    bool first = true;
                    foreach (JToken child in value.Children())
                    {
                        if (!first)
                        {
                            builder.Append(',');
                        }

                        first = false;
                        AppendLevelConfigValue(builder, child);
                    }

                    builder.Append(']');
                    return;
                case JTokenType.String:
                    builder.Append('"');
                    builder.Append(EscapeJsonString(value.Value<string>() ?? string.Empty));
                    builder.Append('"');
                    return;
                default:
                    builder.Append(value.ToString());
                    return;
            }
        }

        private static string ReplaceFirst(string text, string search, string replace)
        {
            int pos = text.IndexOf(search, System.StringComparison.Ordinal);
            if (pos < 0)
            {
                return text;
            }

            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }

        private static string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

        /// <summary>
        /// 执行 gen.bat 文件
        /// </summary>
        private static void ExecuteGenBat(string batPath)
        {
            string workingDirectory = Path.GetDirectoryName(batPath).Replace('\\', '/');
            
            UnityEngine.Debug.Log($"[LubanGenerator] 开始执行配置生成...");
            UnityEngine.Debug.Log($"[LubanGenerator] 工作目录: {workingDirectory}");
            UnityEngine.Debug.Log($"[LubanGenerator] 批处理文件: {batPath}");

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = batPath,
                WorkingDirectory = workingDirectory,
                UseShellExecute = true,
                CreateNoWindow = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            try
            {
                Process process = Process.Start(startInfo);
                
                if (process != null)
                {
                    UnityEngine.Debug.Log($"[LubanGenerator] 进程已启动 (PID: {process.Id})");
                    
                    // 等待进程完成（可选，如果需要同步等待）
                    // process.WaitForExit();
                    
                    // 等待进程完成后更新配置文件名
                    EditorApplication.delayCall += () =>
                    {
                        // 延迟执行，确保文件写入完成
                        EditorApplication.delayCall += () =>
                        {
                            AssetDatabase.Refresh();
                            UpdateConfigFileNames();
                            UnityEngine.Debug.Log("[LubanGenerator] 配置生成完成，已刷新资源数据库并更新配置文件名。");
                        };
                    };
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "无法启动生成进程！", "OK");
                }
            }
            catch (System.Exception e)
            {
                string errorMsg = $"执行配置生成时发生错误：\n\n{e.Message}";
                UnityEngine.Debug.LogError($"[LubanGenerator] {errorMsg}");
                EditorUtility.DisplayDialog("Error", errorMsg, "OK");
            }
        }

        /// <summary>
        /// 验证 gen.bat 文件是否存在
        /// </summary>
        [MenuItem(MENU_PATH + " (Validate)", false, 2)]
        public static void ValidateGenBat()
        {
            string genBatPath = GetGenBatPath();
            
            if (string.IsNullOrEmpty(genBatPath))
            {
                EditorUtility.DisplayDialog("Validation", 
                    $"❌ 找不到 {GEN_BAT_NAME} 文件！", 
                    "OK");
                return;
            }

            if (File.Exists(genBatPath))
            {
                EditorUtility.DisplayDialog("Validation", 
                    $"✅ 找到 gen.bat 文件：\n\n{genBatPath}", 
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog("Validation", 
                    $"❌ 文件不存在：\n\n{genBatPath}", 
                    "OK");
            }
        }

        /// <summary>
        /// 从 Tables.cs 中提取配置文件名
        /// </summary>
        private static List<string> ExtractConfigFileNamesFromTables()
        {
            List<string> fileNames = new List<string>();
            
            string tablesPath = TABLES_CS_PATH.Replace('/', Path.DirectorySeparatorChar);
            if (!File.Exists(tablesPath))
            {
                UnityEngine.Debug.LogWarning($"[LubanGenerator] Tables.cs 文件不存在: {tablesPath}");
                return fileNames;
            }

            try
            {
                string content = File.ReadAllText(tablesPath);
                
                // 使用正则表达式匹配 loader("filename") 模式
                Regex regex = new Regex(@"loader\s*\(\s*""([^""]+)""\s*\)", RegexOptions.Multiline);
                MatchCollection matches = regex.Matches(content);
                
                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        string fileName = match.Groups[1].Value;
                        if (!string.IsNullOrEmpty(fileName) && !fileNames.Contains(fileName))
                        {
                            fileNames.Add(fileName);
                        }
                    }
                }
                
                UnityEngine.Debug.Log($"[LubanGenerator] 从 Tables.cs 中提取到 {fileNames.Count} 个配置文件名");
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[LubanGenerator] 解析 Tables.cs 时发生错误: {e.Message}");
            }
            
            return fileNames;
        }

        /// <summary>
        /// 更新 ConfigFileNames.cs 文件
        /// </summary>
        /// <returns>成功完成检查或更新时返回 true；输入缺失或处理异常时返回 false。</returns>
        private static bool UpdateConfigFileNames()
        {
            List<string> configFileNames = ExtractConfigFileNamesFromTables();
            
            if (configFileNames.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[LubanGenerator] 未找到配置文件名，跳过更新。");
                return false;
            }

            string configFileNamesPath = CONFIG_FILE_NAMES_PATH.Replace('/', Path.DirectorySeparatorChar);
            if (!File.Exists(configFileNamesPath))
            {
                UnityEngine.Debug.LogWarning($"[LubanGenerator] ConfigFileNames.cs 文件不存在: {configFileNamesPath}");
                return false;
            }

            try
            {
                string content = File.ReadAllText(configFileNamesPath);
                string originalContent = content;

                // 构建新的配置文件名数组代码
                string newArrayCode = "return new string[]\n            {\n";
                foreach (string fileName in configFileNames)
                {
                    newArrayCode += $"                \"{fileName}\",\n";
                }
                newArrayCode = newArrayCode.TrimEnd(',', '\n') + "\n            };";

                // 使用正则表达式匹配 GetFileNames 方法中的 return 语句
                Regex regex = new Regex(
                    @"(public\s+static\s+string\[\]\s+GetFileNames\(\)\s*\{[^}]*return\s+new\s+string\[\]\s*\{[^}]*\};)",
                    RegexOptions.Singleline | RegexOptions.Multiline
                );

                Match match = regex.Match(content);
                if (match.Success)
                {
                    // 找到方法体，替换 return 语句部分
                    string methodBody = match.Groups[1].Value;
                    Regex returnRegex = new Regex(@"return\s+new\s+string\[\]\s*\{[^}]*\};", RegexOptions.Singleline);
                    string newMethodBody = returnRegex.Replace(methodBody, newArrayCode);
                    content = content.Replace(methodBody, newMethodBody);
                }
                else
                {
                    // 如果没找到完整方法，尝试只替换 return 语句
                    Regex returnRegex = new Regex(@"return\s+new\s+string\[\]\s*\{[^}]*\};", RegexOptions.Singleline);
                    content = returnRegex.Replace(content, newArrayCode);
                }

                // 如果内容有变化，写入文件
                if (content != originalContent)
                {
                    File.WriteAllText(configFileNamesPath, content);
                    AssetDatabase.ImportAsset(CONFIG_FILE_NAMES_PATH);
                    UnityEngine.Debug.Log($"[LubanGenerator] 已更新 ConfigFileNames.cs 中的配置文件名列表:\n{string.Join("\n", configFileNames.Select(f => $"  - {f}"))}");
                }
                else
                {
                    UnityEngine.Debug.Log("[LubanGenerator] ConfigFileNames.cs 中的配置文件名列表无需更新。");
                }

                return true;
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[LubanGenerator] 更新 ConfigFileNames.cs 时发生错误: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 手动更新配置文件名（用于测试）
        /// </summary>
        [MenuItem(MENU_PATH + " (Update File Names)", false, 3)]
        public static void ManualUpdateConfigFileNames()
        {
            if (!UpdateConfigFileNames())
            {
                EditorUtility.DisplayDialog("Update Failed", "配置文件名更新失败，请检查 Console 日志。", "OK");
                return;
            }

            AutoGenerateGuruAllSpecs();
            EditorUtility.DisplayDialog("Update Complete", "配置文件名已更新！", "OK");
        }
    }
}
#endif
