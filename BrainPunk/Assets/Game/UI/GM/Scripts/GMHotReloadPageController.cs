using System;
using System.IO;
using System.Reflection;
using System.Text;
using Game.HotReload;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YooAsset;

/// <summary>
/// GM 面板 — HotReload 信息展示与调试 Page Controller
/// 挂载到 GMPanel 的 PageView Content 下的单个 Page 节点上
/// 
/// 设计原则：不依赖 GF 运行时框架（GF.GameSettingData / App.LocalStorage / DB 等），
/// 仅通过 Resources.Load 直接读取 GameSetting.asset，通过 PlayerPrefs 做本地持久化。
/// </summary>
public sealed class GMHotReloadPageController : MonoBehaviour
{
    private enum ButtonAction
    {
        SwitchStaging,
        SwitchRehearsal,
        SwitchProd,
        ClearCache,
        ExportReport,
        RestartScene,
    }

    private const string PassColor = "#00C853";
    private const string FailColor = "#FF3B30";
    private const string WarnColor = "#FFB300";
    private const string InfoColor = "#00B0FF";

    private const string PREF_KEY_ENV = "HotReload_Env";
    private const string PREF_KEY_BUILD_ID = "HotReload_BuildId";
    private const string PREF_KEY_CDN = "HotReload_CdnUrl";
    private const string PREF_KEY_CDN_FALLBACK = "HotReload_CdnUrlFallback";

    [Header("View")]
    [SerializeField] private TextMeshProUGUI debugText;

    [Header("Buttons")]
    [SerializeField] private Transform buttonContainer;
    [SerializeField] private CustomButton buttonTemplate;

    [Header("Behavior")]
    [SerializeField] private bool refreshViewOnEnable = true;
    [SerializeField] private float realtimeRefreshInterval = 0.5f;

    private float _nextRefreshRealtime;
    private string _lastAction = "空闲";

    private void OnEnable()
    {
        RebuildButtons();
        EnableRichText();
        _nextRefreshRealtime = 0f;

        if (refreshViewOnEnable)
        {
            RefreshView();
        }
    }

    private void Update()
    {
        if (!refreshViewOnEnable)
        {
            return;
        }

        if (realtimeRefreshInterval > 0f && Time.unscaledTime < _nextRefreshRealtime)
        {
            return;
        }

        _nextRefreshRealtime = Time.unscaledTime + Mathf.Max(0.05f, realtimeRefreshInterval);
        RefreshView();
    }

    private void OnDisable()
    {
        ClearGeneratedButtons();
    }

    public void RefreshView()
    {
        EnableRichText();

        if (debugText != null)
        {
            debugText.text = BuildDebugText();
            debugText.ForceMeshUpdate();
            LayoutRebuilder.ForceRebuildLayoutImmediate(debugText.rectTransform);
            Canvas.ForceUpdateCanvases();
        }
    }

    private string BuildDebugText()
    {
        var sb = new StringBuilder(1024);
        sb.AppendLine("[HotReload 调试信息]");

        // 1. 环境信息
        string envName = GetCurrentEnvName();
        string buildId = PlayerPrefs.GetString(PREF_KEY_BUILD_ID, "unknown");
        sb.AppendLine($"环境: {ColorInfoValue(envName)}  BuildId: {buildId}");

        // 2. 从 GameSetting.asset 直接读取（不经过 GF.GameSettingData）
        var setting = LoadGameSettingAsset();
        string packageName = GetPackageName(setting);
        string cdnUrl = GetCdnUrl(setting);
        string cdnFallback = GetCdnUrlFallback(setting);

        sb.AppendLine($"包名: {packageName}");
        sb.AppendLine($"CDN主: {ColorInfoValue(TruncateUrl(cdnUrl))}");
        if (!string.IsNullOrEmpty(cdnFallback))
        {
            sb.AppendLine($"CDN备: {ColorInfoValue(TruncateUrl(cdnFallback))}");
        }

        // 3. BuiltinPackageElement（通过反射读取，不依赖 GF 运行时）
        var packageElement = GetDefaultPackageElement(setting, packageName);
        string playModeStr = packageElement != null ? GetFieldValue(packageElement, "playMode", "N/A") : "N/A";
        string generation = packageElement != null ? GetFieldValue(packageElement, "generation", "N/A") : "N/A";
        string yooVersion = packageElement != null ? GetFieldValue(packageElement, "yooVersion", "N/A") : "N/A";
        bool isHostMode = packageElement != null && playModeStr == EPlayMode.HostPlayMode.ToString();

        sb.AppendLine($"PlayMode: {ColorBoolValue(isHostMode, playModeStr, $"{playModeStr} (非HostMode)")}");
        sb.AppendLine($"Generation: {generation}  YooVersion: {yooVersion}");

        // 4. YooAsset 包状态
        var package = YooAssets.TryGetPackage(packageName);
        string packageVersion = package != null ? package.GetPackageVersion() : "N/A";
        sb.AppendLine($"PackageVersion: {ColorInfoValue(packageVersion)}");

        // 5. HotReloadCtrl 状态
        var hotReload = HotReloadCtrl.Instance;
        if (hotReload != null)
        {
            bool isFinish = hotReload.IsFinish;
            bool isFailed = hotReload.IsFailed;
            string stateStr;
            if (isFailed) stateStr = ColorFailValue("Failed");
            else if (isFinish) stateStr = ColorPassValue("Finished");
            else stateStr = ColorWarnValue("Running / Waiting");

            sb.AppendLine($"热更状态: {stateStr}");
            sb.AppendLine($"  PackageName: {hotReload.PackageName}");
            sb.AppendLine($"  PackageVersion: {hotReload.PackageVersion}");
            sb.AppendLine($"  PlayMode: {hotReload.PlayMode}");
        }
        else
        {
            sb.AppendLine($"热更状态: {ColorWarnValue("HotReloadCtrl 未初始化")}");
        }

        // 6. 缓存信息
        float cacheSizeMB = GetYooAssetCacheSizeMB();
        sb.AppendLine($"缓存大小: {cacheSizeMB:F2} MB");

        // 7. PlayerPrefs 中记录的 RemoteConfig 简要信息（不依赖 DB）
        sb.AppendLine($"PlayerPrefs 键数: {CountPlayerPrefsKeys()}");

        // 8. 最后操作
        sb.AppendLine($"最后操作: {ColorActionText(_lastAction)}");

        return sb.ToString().TrimEnd();
    }

    #region No-GF GameSetting Access

    /// <summary>
    /// 直接通过 Resources.Load 读取 GameSetting.asset，绕过 GF.GameSettingData
    /// </summary>
    private static object LoadGameSettingAsset()
    {
        // GameSetting 在 GF 命名空间下，但 asset 文件在 Resources 目录
        // 使用 ScriptableObject 类型加载，避免强依赖 GF.GameSetting 类型引用
        var setting = Resources.Load<ScriptableObject>("GameSetting");
        return setting;
    }

    private static string GetPackageName(object setting)
    {
        if (setting == null) return "N/A";
        return GetFieldValue(setting, "defaultPackageName", "N/A");
    }

    private static string GetCdnUrl(object setting)
    {
        if (setting == null) return GetPlayerPrefsCdnFallback();

        // 优先从 PlayerPrefs 读取（被环境切换覆盖后的值）
        string prefCdn = PlayerPrefs.GetString(PREF_KEY_CDN, null);
        if (!string.IsNullOrEmpty(prefCdn))
            return prefCdn;

        // 回退到 GameSetting.asset 中的 cdnUrl 字段
        string assetCdn = GetFieldValue(setting, "cdnUrl", null);
        if (!string.IsNullOrEmpty(assetCdn))
            return assetCdn;

        return "N/A";
    }

    private static string GetCdnUrlFallback(object setting)
    {
        if (setting == null) return GetPlayerPrefsCdnFallback(isFallback: true);

        string prefCdn = PlayerPrefs.GetString(PREF_KEY_CDN_FALLBACK, null);
        if (!string.IsNullOrEmpty(prefCdn))
            return prefCdn;

        string assetCdn = GetFieldValue(setting, "cdnUrlFallback", null);
        return assetCdn;
    }

    private static string GetPlayerPrefsCdnFallback(bool isFallback = false)
    {
        return isFallback
            ? PlayerPrefs.GetString(PREF_KEY_CDN_FALLBACK, "")
            : PlayerPrefs.GetString(PREF_KEY_CDN, "");
    }

    /// <summary>
    /// 通过反射从 GameSetting.asset 的 builtinPackageList 中找到对应包名的元素
    /// </summary>
    private static object GetDefaultPackageElement(object setting, string packageName)
    {
        if (setting == null || string.IsNullOrEmpty(packageName))
            return null;

        try
        {
            var listField = setting.GetType().GetField("builtinPackageList", BindingFlags.Public | BindingFlags.Instance);
            if (listField == null) return null;

            var list = listField.GetValue(setting) as System.Collections.IList;
            if (list == null) return null;

            foreach (var item in list)
            {
                if (item == null) continue;
                var nameField = item.GetType().GetField("packageName", BindingFlags.Public | BindingFlags.Instance);
                if (nameField != null && nameField.GetValue(item) as string == packageName)
                    return item;
            }
        }
        catch
        {
            // 反射失败静默返回 null
        }

        return null;
    }

    /// <summary>
    /// 通用反射读字段
    /// </summary>
    private static string GetFieldValue(object obj, string fieldName, string defaultValue)
    {
        if (obj == null) return defaultValue;
        try
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
            {
                var value = field.GetValue(obj);
                return value?.ToString() ?? defaultValue;
            }
        }
        catch { }
        return defaultValue;
    }

    #endregion

    #region Button Actions

    private void SwitchEnv(string envName, string cdnRoot)
    {
        // 不依赖 App.LocalStorage，直接用 PlayerPrefs
        PlayerPrefs.SetString(PREF_KEY_CDN, cdnRoot);
        PlayerPrefs.SetString(PREF_KEY_ENV, envName);
        PlayerPrefs.Save();

        _lastAction = $"切换环境 -> {envName}, CDN={TruncateUrl(cdnRoot)}";
        RefreshView();
    }

    private void OnSwitchStaging(CustomButton _)
    {
        SwitchEnv("StagingHotfix", "https://your-cdn.com/hotfix/staging");
    }

    private void OnSwitchRehearsal(CustomButton _)
    {
        SwitchEnv("ReleaseRehearsal", "https://your-cdn.com/hotfix/rehearsal");
    }

    private void OnSwitchProd(CustomButton _)
    {
        _lastAction = "切换 Prod 需要手动确认（未实现弹窗时请在代码中注释此行）";
        RefreshView();
    }

    private void OnClearCache(CustomButton _)
    {
        string cacheRoot = Path.Combine(Application.persistentDataPath, "YooAsset");
        if (Directory.Exists(cacheRoot))
        {
            Directory.Delete(cacheRoot, recursive: true);
            _lastAction = $"已清理缓存: {cacheRoot}";
        }
        else
        {
            _lastAction = "缓存目录不存在，无需清理";
        }
        RefreshView();
    }

    private void OnExportReport(CustomButton _)
    {
        var sb = new StringBuilder(1024);
        sb.AppendLine("========== HotReload GM Report ==========");
        sb.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Device: {SystemInfo.deviceUniqueIdentifier}");
        sb.AppendLine(BuildDebugText());
        sb.AppendLine("=========================================");

        GUIUtility.systemCopyBuffer = sb.ToString();
        _lastAction = "报告已复制到剪贴板";
        RefreshView();
    }

    private void OnRestartScene(CustomButton _)
    {
        _lastAction = "重启场景...";
        RefreshView();
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    #endregion

    #region Button Generation

    private void RebuildButtons()
    {
        ClearGeneratedButtons();

        if (buttonTemplate == null)
        {
            return;
        }

        Transform parent = buttonContainer != null ? buttonContainer : buttonTemplate.transform.parent;
        if (parent == null)
        {
            return;
        }

        buttonTemplate.gameObject.SetActive(false);

        CreateButton(parent, ButtonAction.SwitchStaging, "切到 Staging");
        CreateButton(parent, ButtonAction.SwitchRehearsal, "切到 Rehearsal");
        CreateButton(parent, ButtonAction.SwitchProd, "切到 Prod (确认)");
        CreateButton(parent, ButtonAction.ClearCache, "清理缓存");
        CreateButton(parent, ButtonAction.ExportReport, "复制报告");
        CreateButton(parent, ButtonAction.RestartScene, "重启场景");
    }

    private void ClearGeneratedButtons()
    {
        foreach (Transform child in buttonContainer)
        {
            if (child == buttonTemplate.transform) continue;
            Destroy(child.gameObject);
        }
    }

    private void CreateButton(Transform parent, ButtonAction action, string label)
    {
        CustomButton instance = Instantiate(buttonTemplate, parent, false);
        instance.gameObject.name = label;
        instance.gameObject.SetActive(true);
        SetButtonLabel(instance, label);

        switch (action)
        {
            case ButtonAction.SwitchStaging:
                instance.OnClick += OnSwitchStaging;
                break;
            case ButtonAction.SwitchRehearsal:
                instance.OnClick += OnSwitchRehearsal;
                break;
            case ButtonAction.SwitchProd:
                instance.OnClick += OnSwitchProd;
                break;
            case ButtonAction.ClearCache:
                instance.OnClick += OnClearCache;
                break;
            case ButtonAction.ExportReport:
                instance.OnClick += OnExportReport;
                break;
            case ButtonAction.RestartScene:
                instance.OnClick += OnRestartScene;
                break;
        }
    }

    private static void SetButtonLabel(CustomButton button, string label)
    {
        TMP_Text tmpText = button.GetComponentInChildren<TMP_Text>(true);
        if (tmpText != null)
        {
            tmpText.text = label;
        }

        Text legacyText = button.GetComponentInChildren<Text>(true);
        if (legacyText != null)
        {
            legacyText.text = label;
        }
    }

    #endregion

    #region Helpers

    private void EnableRichText()
    {
        if (debugText != null)
        {
            debugText.richText = true;
        }
    }

    private static string GetCurrentEnvName()
    {
        return PlayerPrefs.GetString(PREF_KEY_ENV, "EditorSimulate / Unknown");
    }

    private static float GetYooAssetCacheSizeMB()
    {
        string cacheRoot = Path.Combine(Application.persistentDataPath, "YooAsset");
        if (!Directory.Exists(cacheRoot)) return 0f;

        long totalBytes = 0;
        try
        {
            var dirInfo = new DirectoryInfo(cacheRoot);
            foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
            {
                totalBytes += file.Length;
            }
        }
        catch
        {
            return -1f;
        }
        return totalBytes / (1024f * 1024f);
    }

    private static int CountPlayerPrefsKeys()
    {
        // PlayerPrefs 没有枚举所有 key 的 API，这里只统计已知的热更相关 key
        int count = 0;
        if (PlayerPrefs.HasKey(PREF_KEY_ENV)) count++;
        if (PlayerPrefs.HasKey(PREF_KEY_BUILD_ID)) count++;
        if (PlayerPrefs.HasKey(PREF_KEY_CDN)) count++;
        if (PlayerPrefs.HasKey(PREF_KEY_CDN_FALLBACK)) count++;
        return count;
    }

    private static string TruncateUrl(string url, int maxLen = 40)
    {
        if (string.IsNullOrEmpty(url)) return "N/A";
        if (url.Length <= maxLen) return url;
        return url.Substring(0, maxLen) + "...";
    }

    private static string ColorBoolValue(bool isPass, string passText, string failText)
    {
        return isPass ? $"<color={PassColor}>{passText}</color>" : $"<color={FailColor}>{failText}</color>";
    }

    private static string ColorPassValue(string value)
    {
        return $"<color={PassColor}>{value}</color>";
    }

    private static string ColorFailValue(string value)
    {
        return $"<color={FailColor}>{value}</color>";
    }

    private static string ColorWarnValue(string value)
    {
        return $"<color={WarnColor}>{value}</color>";
    }

    private static string ColorInfoValue(string value)
    {
        return $"<color={InfoColor}>{value}</color>";
    }

    private static string ColorActionText(string actionText)
    {
        if (string.IsNullOrWhiteSpace(actionText))
        {
            return ColorFailValue("None");
        }

        bool isFailure =
            actionText.IndexOf("失败", StringComparison.OrdinalIgnoreCase) >= 0 ||
            actionText.IndexOf("为空", StringComparison.OrdinalIgnoreCase) >= 0 ||
            actionText.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0 ||
            actionText.IndexOf("null", StringComparison.OrdinalIgnoreCase) >= 0;

        return isFailure ? ColorFailValue(actionText) : ColorPassValue(actionText);
    }

    #endregion
}
