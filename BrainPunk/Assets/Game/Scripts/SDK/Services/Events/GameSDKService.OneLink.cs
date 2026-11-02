using System;
using System.Collections.Generic;
using Guru.SDK.Framework.Utils.Network;
using Newtonsoft.Json.Linq;
using GuruLog = Guru.SDK.Framework.Utils.Log.Log;

public partial class GameSDKService
{
    #region OneLink 安装归因事件

    /// <summary>
    /// 上报 GCD 的开始、失败及返回指令观测，统一复用现有 LogEvent 通道。
    /// 仅由 OneLinkManager 运行态接收点调用；返回有效不代表已经跳转或领奖。
    /// 仅提取业务白名单，UID 只记是否存在；统计异常不得中断原有业务。
    /// </summary>
    /// <param name="state">观测状态：start、failed、returned。</param>
    /// <param name="source">receiver_bound、sdk_callback、sdk_cache 或 game_wait；区分 SDK 失败与游戏侧等待超时。</param>
    /// <param name="failureReason">SDK 失败传入原错误，仅分类后上传；本地解析／等待失败传固定原因，不上传原错误文本。</param>
    /// <param name="conversion">归因回包，只读取 af_status、is_first_launch，不上传整个对象。</param>
    /// <param name="command">Manager 按既有优先级解析出的目标，失败时可传原回包；不在本接口做目标路由。</param>
    /// <param name="resolution">existing、af_dp、campaign 或 none。</param>
    /// <param name="parseError">指令解析失败的固定原因；为空表示指令解析有效，不等于执行门禁通过。</param>
    /// <param name="waitSeconds">仅 game_wait 传实际观测窗口，不代表 SDK 网络请求超时配置。</param>
    public void OneLinkGcdEvent(string state, string source, string failureReason = null,
        JObject conversion = null, JObject command = null, string resolution = null,
        string parseError = null, int waitSeconds = 0)
    {
        try
        {
            var data = new Dictionary<string, object>
            {
                ["state"] = state,
                ["source"] = source,
                ["network_connected"] = NetworkUtils.IsNetworkConnected()
            };
            if (failureReason != null)
                data["failure_reason"] = source == "sdk_callback" ? ClassifyGcdFailure(failureReason) : failureReason;
            if (source == "game_wait") data["wait_seconds"] = waitSeconds;
            if (state == "returned")
            {
                JObject values = command;
                JToken click = command?["click_event"];
                if (click is JObject clickObject) values = clickObject;
                else if (click?.Type == JTokenType.String)
                {
                    try { values = JObject.Parse((string)click); }
                    catch (Exception) { parseError = "invalid_click_event"; }
                }
                data["command_valid"] = parseError == null;
                data["resolution"] = parseError == null ? resolution : "none";
                data["deep_link_value"] = ReadGcdEventValue(values?["deep_link_value"]);
                data["deep_link_sub1"] = ReadGcdEventValue(values?["deep_link_sub1"]);
                data["deep_link_sub2"] = ReadGcdEventValue(values?["deep_link_sub2"]);
                data["deep_link_sub3_present"] = !string.IsNullOrWhiteSpace(values?["deep_link_sub3"]?.ToString());
                data["af_status"] = ReadGcdEventValue(conversion?["af_status"]);
                data["is_first_launch"] = bool.TryParse(conversion?["is_first_launch"]?.ToString(), out bool first) && first;
                if (parseError != null) data["parse_error"] = parseError;
            }
            LogEvent("onelink_gcd", data);
        }
        catch (Exception exception)
        {
            GuruLog.W($"[OneLink] stage=gcd_analytics_failed, state={state}, error={exception.GetType().Name}.");
        }
    }

    private static string ClassifyGcdFailure(string error)
    {
        if (error == "invalid_conversion_data") return error;
        string text = error.ToLowerInvariant();
        if (text.Contains("timeout") || text.Contains("timed out") || text.Contains("超时")) return "timeout";
        if (text.Contains("network") || text.Contains("internet") || text.Contains("offline") ||
            text.Contains("connection") || text.Contains("connect to") || text.Contains("resolve host") ||
            text.Contains("unknownhost") || text.Contains("dns") || text.Contains("网络")) return "network_error";
        return "sdk_error";
    }

    private static string ReadGcdEventValue(JToken token)
    {
        if (token?.Type != JTokenType.String && token?.Type != JTokenType.Integer) return string.Empty;
        string value = token.ToString().Trim();
        return value.Length > 100 ? value.Substring(0, 100) : value;
    }

    #endregion
}
