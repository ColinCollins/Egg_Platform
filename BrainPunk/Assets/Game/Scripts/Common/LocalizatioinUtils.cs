using System;
using Bear.SaveModule;
using Game.ConfigModule;
using I2.Loc;
using UnityEngine;

public static class LocalizatioinUtils
{
    private const string DefaultLanguageKeyCode = "en";
    private const string SpanishSpainLanguageKeyCode = "es-ES";
    private const string SpanishLatinAmericaLanguageKeyCode = "es-US";

    #region 启动语言

    /// <summary>
    /// 在存档初始化完成后准备启动语言。
    /// 首次启动只采用一次 I2 设备语言解析结果；后续启动以 GameSetting 存档为准，并同步覆盖 I2 的历史缓存。
    /// </summary>
    public static void InitializeStartupLanguage()
    {
        string languageCode = DB.GameSetting.CurrentLanguageKeyCode;
        if (DB.GameData.PlayCount == 0)
        {
            languageCode = GetSupportedDeviceLanguageCode();

            DB.GameSetting.CurrentLanguageKeyCode = languageCode;

            if (!TryApplyI2Language(languageCode, false))
                Debug.LogWarning($"[LocalizationUtils] Cannot apply I2 startup language '{languageCode}' before config validation.");

            return;
        }

        if (!TrySetLanguage(languageCode, false))
            Debug.LogWarning($"[LocalizationUtils] Cannot apply startup language '{languageCode}' before config validation.");
    }

    /// <summary>
    /// 配置加载完成后校验并应用启动语言。
    /// 已保存语言必须同时存在于开放的 TbLanguage 与 I2 语言源中，否则统一回退英文。
    /// </summary>
    public static void ValidateAndApplyStartupLanguage()
    {
        string currentLanguageCode = DB.GameSetting.CurrentLanguageKeyCode;
        var languages = ConfigManager.Instance?.Tables?.TbLanguage?.DataList;
        bool isFirstLaunch = DB.GameData.PlayCount == 1;
        bool isOpenLanguage = languages != null && languages.Exists(language =>
            language != null &&
            language.IsOpen &&
            string.Equals(language.CorrespondingKey, currentLanguageCode, StringComparison.OrdinalIgnoreCase));

        string targetLanguageCode = currentLanguageCode;
        string fallbackReason = null;
        bool saveImmediately = !isFirstLaunch;

        if (languages == null)
        {
            // 配置暂时不可用时，老玩家保留已保存语言，避免一次加载失败覆盖用户选择。
            if (isFirstLaunch)
            {
                targetLanguageCode = DefaultLanguageKeyCode;
                fallbackReason = "language config is unavailable on first launch";
            }
        }
        else if (!isOpenLanguage)
        {
            targetLanguageCode = DefaultLanguageKeyCode;
            fallbackReason = $"language '{currentLanguageCode}' is not open";
        }

        if (!TrySetLanguage(targetLanguageCode, saveImmediately))
        {
            if (!string.Equals(targetLanguageCode, DefaultLanguageKeyCode, StringComparison.OrdinalIgnoreCase) &&
                TrySetLanguage(DefaultLanguageKeyCode, saveImmediately))
            {
                fallbackReason = $"language '{targetLanguageCode}' is not supported by I2";
                targetLanguageCode = DefaultLanguageKeyCode;
            }
            else
            {
                Debug.LogError($"[LocalizationUtils] Cannot apply language '{targetLanguageCode}' or English fallback.");
                return;
            }
        }

        if (isFirstLaunch)
            DB.GameSetting.Save();

        if (!string.IsNullOrEmpty(fallbackReason))
            Debug.LogWarning($"[LocalizationUtils] Startup language fallback to English because {fallbackReason}.");
    }

    #endregion

    #region 语言设置

    /// <summary>
    /// 同步设置 I2 当前语言与 GameSetting 存档语言。
    /// 语言代码必须存在于 I2 语言源；saveGameSetting 为 false 时只跳过重复的存档写盘，不影响 I2 缓存同步。
    /// </summary>
    /// <param name="languageCode">I2 使用的语言代码，如 en、zh-CN。</param>
    /// <param name="saveGameSetting">存档语言变化时是否立即保存 GameSetting。</param>
    /// <returns>语言代码有效并成功同步时返回 true。</returns>
    public static bool TrySetLanguage(string languageCode, bool saveGameSetting = true)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            return false;

        if (!TryApplyI2Language(languageCode, true))
            return false;

        bool hasGameSettingChanged = !string.Equals(
            DB.GameSetting.CurrentLanguageKeyCode,
            languageCode,
            StringComparison.OrdinalIgnoreCase);
        DB.GameSetting.CurrentLanguageKeyCode = languageCode;

        if (saveGameSetting && hasGameSettingChanged)
            DB.GameSetting.Save();

        return true;
    }

    #endregion

    #region I2 设备语言适配

    /// <summary>
    /// 先使用 I2 默认流程匹配设备语言，再统一执行项目层地区分区后处理，不依赖 GF 子模块修改。
    /// I2 无匹配结果时明确回退英文，避免采用语言源首项作为业务默认语言。
    /// </summary>
    /// <returns>I2 支持的设备语言代码；无法解析时返回英文。</returns>
    private static string GetSupportedDeviceLanguageCode()
    {
        LocalizationManager.InitializeIfNeeded();

        string deviceLanguage = LocalizationManager.GetCurrentDeviceLanguage(true);
        string supportedLanguage = LocalizationManager.GetSupportedLanguage(deviceLanguage, true);
        string languageCode = string.IsNullOrEmpty(supportedLanguage)
            ? null
            : LocalizationManager.GetLanguageCode(supportedLanguage);

        if (string.IsNullOrEmpty(languageCode))
            return DefaultLanguageKeyCode;

        return PostprocessLanguageRegion(deviceLanguage, languageCode);
    }

    /// <summary>
    /// 在 I2 完成默认匹配后统一处理项目语言分区；不参与或改变 I2 的匹配过程。
    /// 无地区信息时保留 I2 结果，分区目标不存在于 I2 语言源时也保留原匹配结果。
    /// </summary>
    /// <param name="deviceLanguage">I2 检测得到的设备语言名称，可能包含地区信息。</param>
    /// <param name="matchedLanguageCode">I2 默认匹配得到的语言代码。</param>
    /// <returns>完成项目分区后的语言代码。</returns>
    private static string PostprocessLanguageRegion(string deviceLanguage, string matchedLanguageCode)
    {
#if UNITY_IOS
        // iOS 保留原有简体系统语言显示繁体中文的产品规则，并统一保存实际应用的语言代码。
        if (string.Equals(matchedLanguageCode, "zh-CN", StringComparison.OrdinalIgnoreCase))
            return "zh-TW";
#endif

        string deviceLanguageCode = GoogleLanguages.GetLanguageCode(deviceLanguage);
        bool hasSpanishRegion = !string.IsNullOrEmpty(deviceLanguageCode) &&
                                deviceLanguageCode.StartsWith("es-", StringComparison.OrdinalIgnoreCase);
        bool matchedSpanish = matchedLanguageCode.StartsWith("es-", StringComparison.OrdinalIgnoreCase);
        if (!hasSpanishRegion || !matchedSpanish)
            return matchedLanguageCode;

        string partitionedLanguageCode = string.Equals(
            deviceLanguageCode,
            SpanishSpainLanguageKeyCode,
            StringComparison.OrdinalIgnoreCase)
            ? SpanishSpainLanguageKeyCode
            : SpanishLatinAmericaLanguageKeyCode;

        return string.IsNullOrEmpty(LocalizationManager.GetLanguageFromCode(partitionedLanguageCode, true))
            ? matchedLanguageCode
            : partitionedLanguageCode;
    }

    /// <summary>
    /// 仅向 I2 应用语言，不修改 GameSetting；用于首次候选与统一双写入口的内部复用。
    /// </summary>
    /// <param name="languageCode">需要应用的 I2 语言代码。</param>
    /// <param name="rememberLanguage">是否同步更新 I2 的历史语言缓存。</param>
    /// <returns>I2 语言源存在该代码并成功应用时返回 true。</returns>
    private static bool TryApplyI2Language(string languageCode, bool rememberLanguage)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            return false;

        string languageName = LocalizationManager.GetLanguageFromCode(languageCode);
        if (string.IsNullOrEmpty(languageName))
            return false;

        LocalizationManager.SetLanguageAndCode(languageName, languageCode, rememberLanguage);
        return true;
    }

    #endregion
}
