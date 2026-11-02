using System;
using Newtonsoft.Json.Linq;
using GuruLog = Guru.SDK.Framework.Utils.Log.Log;

namespace Game
{
    /// <summary>验证受控广告 GCD 中的 DDL 目标 URL；不改变归因仲裁与首页跳转时序。</summary>
    public sealed partial class OneLinkManager
    {
        #region 广告 DDL URL 目标

        private static bool TryParseAdDdlUrlTarget(JObject conversion, out JObject target, out OneLinkRequest request,
            bool logDiagnostics = true)
        {
            target = null;
            request = null;
            if (!IsSupportedAdUrlSource(conversion) || conversion["af_dp"]?.Type != JTokenType.String ||
                string.IsNullOrWhiteSpace((string)conversion["af_dp"]))
                return false;

            string adUrl = ((string)conversion["af_dp"]).Trim();
            if (TryParseAdDdlTargetUrl(adUrl, out target, out request))
                return true;

            if (logDiagnostics) GuruLog.W("[OneLink] stage=gcd_ad_url, result=invalid_target; falling_back=true.");
            return false;
        }

        // Facebook／GA4F 原生结果与受控 GCD 共用目标白名单；不要求或伪造原生来源的 AF 归因字段。
        private static bool TryParseAdDdlTargetUrl(string url, out JObject target, out OneLinkRequest request)
        {
            target = null;
            request = null;
            if (Uri.TryCreate(url?.Trim(), UriKind.Absolute, out Uri uri) && IsDirectUri(uri) &&
                TryParseUrlQuery(uri, out target, out _) &&
                target["deep_link_sub2"]?.Type == JTokenType.String &&
                !string.IsNullOrWhiteSpace((string)target["deep_link_sub2"]) &&
                // 与 UDL 共用非空领取 ID、金额／皮肤参数校验；安装消费锁不依赖 ID 是否相同。
                OneLinkRequest.TryParse(target, out request, out _))
                return true;

            target = null;
            request = null;
            return false;
        }

        private static bool IsSupportedAdUrlSource(JObject conversion)
        {
            if (conversion?["media_source"]?.Type != JTokenType.String) return false;
            string source = (string)conversion["media_source"];
            if (string.Equals(source, "Facebook Ads", StringComparison.Ordinal)) return true;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (string.Equals(source, "googleadwords_int", StringComparison.Ordinal)) return true;
#endif
            return false;
        }

        #endregion
    }
}
