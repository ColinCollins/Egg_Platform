using System.Text;
using Game.ConfigModule;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 负责 GMPanel 内 RemoteConfig 缓存的分页显示与按钮生命周期。
/// 仅在运行态读取业务生效缓存，不触发远端拉取、配置解析或存档写入。
/// </summary>
public sealed class GMRemoteConfigDisplay : MonoBehaviour
{
    #region Config

    private static readonly string[] ConfigKeys =
    {
        ConfigManager.RemoteConfig.LevelConfigKey,
        ConfigManager.RemoteConfig.DrawGameConfigKey,
        ConfigManager.RemoteConfig.IWannerGameConfigKey,
    };

    /// <summary>用于显示当前 ConfigKey 与原始 JSON 的文本。</summary>
    [SerializeField] private TMP_Text configText;

    /// <summary>切换到下一个 ConfigKey 的 Unity UI 按钮，在全部已注册 key 之间循环。</summary>
    [SerializeField] private Button nextBtn;

    /// <summary>切换到上一个 ConfigKey 的 Unity UI 按钮，在全部已注册 key 之间循环。</summary>
    [SerializeField] private Button lastBtn;

    #endregion

    #region Runtime State

    private int _configKeyIndex;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        BindButton(nextBtn, ShowNextConfig);
        BindButton(lastBtn, ShowLastConfig);
        RefreshDisplay();
    }

    private void OnDisable()
    {
        UnbindButton(nextBtn, ShowNextConfig);
        UnbindButton(lastBtn, ShowLastConfig);
    }

    #endregion

    #region Button Handlers

    private void ShowNextConfig()
    {
        ChangeConfig(1);
    }

    private void ShowLastConfig()
    {
        ChangeConfig(-1);
    }

    private void ChangeConfig(int offset)
    {
        _configKeyIndex =
            (_configKeyIndex + offset + ConfigKeys.Length) % ConfigKeys.Length;
        RefreshDisplay();
    }

    #endregion

    #region Display

    private void RefreshDisplay()
    {
        if (configText == null)
        {
            return;
        }

        var configKey = ConfigKeys[_configKeyIndex];
        var cache = DB.GameData?.RemoteConfigCache;
        var sb = new StringBuilder();
        sb.AppendLine("当前读取：DB.GameData.RemoteConfigCache（业务生效缓存）");
        sb.Append("ConfigKey：");
        sb.AppendLine(configKey);
        sb.AppendLine();

        if (cache == null ||
            !cache.TryGetValue(configKey, out var json) ||
            string.IsNullOrEmpty(json))
        {
            sb.AppendLine("(当前 ConfigKey 无 RemoteConfig 缓存)");
            configText.text = sb.ToString().TrimEnd();
            return;
        }

        sb.AppendLine(FormatJson(json));
        configText.text = sb.ToString().TrimEnd();
    }

    private static string FormatJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "(empty)";
        }

        try
        {
            var token = JToken.Parse(raw);
            return token.ToString(Formatting.Indented);
        }
        catch (JsonException)
        {
            return raw;
        }
    }

    #endregion

    #region Button Binding

    private static void BindButton(Button button, UnityAction handler)
    {
        if (button != null)
        {
            button.onClick.AddListener(handler);
        }
    }

    private static void UnbindButton(Button button, UnityAction handler)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(handler);
        }
    }

    #endregion
}
