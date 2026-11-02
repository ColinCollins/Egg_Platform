using System;
using Newtonsoft.Json.Linq;
using GuruLog = Guru.SDK.Framework.Utils.Log.Log;

namespace Game
{
    /// <summary>OneLink 白名单参数诊断；不改变协议、归因或跳转状态。</summary>
    public sealed partial class OneLinkManager
    {
        #region 参数诊断

        // 只记录业务白名单字段及地址结构，不输出原始归因对象、任意 query 或 URL 用户凭据。
        private static string DescribeLogValue(JToken value)
        {
            if (value == null) return "Missing";
            if (!(value is JValue)) return value.Type.ToString();
            string text = value.ToString(Newtonsoft.Json.Formatting.None);
            return value.Type + ":" + (text.Length > 256 ? text.Substring(0, 256) + "<truncated>" : text);
        }

        private static JObject DescribeParameters(JObject values) => new JObject
        {
            ["deep_link_value"] = DescribeLogValue(values?["deep_link_value"]),
            ["deep_link_sub1"] = DescribeLogValue(values?["deep_link_sub1"]),
            ["deep_link_sub2"] = DescribeLogValue(values?["deep_link_sub2"]),
            ["deep_link_sub3_present"] = !string.IsNullOrWhiteSpace(values?["deep_link_sub3"]?.ToString()),
            ["link_type"] = values?["link"]?.Type.ToString() ?? "Missing"
        };

        private static void LogPayload(JObject payload, string source)
        {
            try
            {
                JToken token = payload?["click_event"];
                JObject click = token as JObject;
                string selectedSource = click != null ? "click_event" : "root";
                if (token?.Type == JTokenType.String)
                {
                    try { click = JObject.Parse((string)token); selectedSource = "click_event"; }
                    catch (Exception) { selectedSource = "invalid_click_event"; }
                }
                var details = new JObject
                {
                    ["payload_present"] = payload != null,
                    ["root"] = DescribeParameters(payload),
                    ["click_event_type"] = token?.Type.ToString() ?? "Missing",
                    ["click_event"] = DescribeParameters(click),
                    ["selected_source"] = payload == null ? "none" : selectedSource,
                    ["selected"] = payload == null || selectedSource == "invalid_click_event" ? null : DescribeParameters(click ?? payload)
                };
                GuruLog.I($"[OneLink] stage=parameters, source={source}, details=" + details.ToString(Newtonsoft.Json.Formatting.None));
                if (payload?["link"]?.Type == JTokenType.String) LogUrl((string)payload["link"], source + "_root_link");
                if (click?["link"]?.Type == JTokenType.String) LogUrl((string)click["link"], source + "_click_link");
            }
            catch (Exception exception)
            {
                GuruLog.W($"[OneLink] stage=diagnostics_failed, source={source}, error={exception.GetType().Name}.");
            }
        }

        private static void LogUrl(string url, string source)
        {
            try
            {
                bool absolute = Uri.TryCreate(url?.Trim(), UriKind.Absolute, out Uri uri);
                var details = new JObject
                {
                    ["length"] = url?.Length ?? 0,
                    ["absolute"] = absolute,
                    ["scheme"] = absolute ? uri.Scheme : "",
                    ["host"] = absolute ? uri.Host : "",
                    ["path"] = absolute ? DescribeLogValue(new JValue(uri.AbsolutePath)) : "",
                    ["http_supported"] = absolute && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
                };
                if (!absolute) details["invalid_prefix"] = DescribeLogValue(new JValue(url?.Split('?', '#')[0]));
                var parameters = new JArray();
                if (absolute)
                {
                    foreach (string part in uri.Query.TrimStart('?').Split('&'))
                    {
                        int separator = part.IndexOf('=');
                        if (separator < 0) continue;
                        string key = Uri.UnescapeDataString(part.Substring(0, separator).Replace("+", " "));
                        if (key != "deep_link_value" && key != "deep_link_sub1" && key != "deep_link_sub2") continue;
                        parameters.Add(new JObject { ["key"] = key,
                            ["value"] = DescribeLogValue(new JValue(Uri.UnescapeDataString(part.Substring(separator + 1).Replace("+", " ")))) });
                    }
                }
                details["parameters"] = parameters;
                GuruLog.I($"[OneLink] stage=url_details, source={source}, details=" + details.ToString(Newtonsoft.Json.Formatting.None));
            }
            catch (Exception exception)
            {
                GuruLog.W($"[OneLink] stage=diagnostics_failed, source={source}, error={exception.GetType().Name}.");
            }
        }

        #endregion
    }
}
