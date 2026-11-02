using System;
using System.Collections.Generic;
using System.Threading;
using AppsFlyerSDK;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using GuruLog = Guru.SDK.Framework.Utils.Log.Log;

namespace Game
{
    /// <summary>URL 白名单解析、请求关联与 SDK 原生桥接；与主文件共享同一会话状态。</summary>
    public sealed partial class OneLinkManager
    {
        #region URL 解析状态

        private const string UrlTestMarkerPrefix = "bp_gm_url_";
        private const string AutomaticUrlMarkerPrefix = "bp_auto_url_";
        private UrlResolveOperation _urlResolution;
        // 只保留当前唤起的自动请求地址，等待结束后仍可接收成功；新点击或成功受理即清空。
        private readonly Dictionary<string, string> _automaticUrls = new Dictionary<string, string>();

        private sealed class UrlResolveOperation
        {
            internal string Id;
            internal string Url;
            internal string LastSdkStatus;
            internal bool IsAutomatic;
            internal bool IsErrorRetry;
            internal bool IsManualSystemUrl;
            internal bool SawSdkError;
            internal string LogStage => IsManualSystemUrl ? "manual_url" : IsAutomatic ? "auto_url" : "test_url";
            internal Action<bool, string> Completed;
            internal readonly CancellationTokenSource Timeout = new CancellationTokenSource();
            internal double Deadline = Time.realtimeSinceStartupAsDouble + 10;
        }

        #endregion

        #region 共用 URL 解析

        private static bool IsOneLinkShortUrl(Uri uri) =>
            string.Equals(uri.Host, "brainpunk.onelink.me", StringComparison.OrdinalIgnoreCase) &&
            uri.AbsolutePath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).Length == 2;

        // 独立 URI 只接受已配置的固定入口；正常解析与 ERROR 本地回退使用同一边界。
        private static bool IsDirectUri(Uri uri) =>
            string.Equals(uri.Scheme, "uribrainpunk", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(uri.Host, "link", StringComparison.OrdinalIgnoreCase) &&
            (uri.AbsolutePath.Length == 0 || uri.AbsolutePath == "/");

        private static bool IsRoutingUrl(string url) =>
            Uri.TryCreate(url?.Trim(), UriKind.Absolute, out Uri uri) &&
            (IsOneLinkShortUrl(uri) || TryParseUrlQuery(uri, out JObject payload, out _) &&
                OneLinkRequest.TryParse(payload, out _, out _));

        private static bool TryParseUrlQuery(Uri uri, out JObject payload, out string error)
        {
            error = null;
            payload = new JObject { ["link"] = uri.OriginalString };
            foreach (string part in uri.Query.TrimStart('?').Split('&'))
            {
                int separator = part.IndexOf('=');
                if (separator < 0)
                    continue;
                string key = Uri.UnescapeDataString(part.Substring(0, separator).Replace("+", " "));
                if (key != "deep_link_value" && key != "deep_link_sub1" && key != "deep_link_sub2" && key != "deep_link_sub3")
                    continue;
                if (payload[key] != null)
                {
                    error = "duplicate_parameter";
                    return false;
                }
                payload[key] = Uri.UnescapeDataString(part.Substring(separator + 1).Replace("+", " "));
            }

            if (payload["deep_link_value"] == null || payload["deep_link_sub1"] == null)
            {
                error = "missing_deep_link_parameters";
                return false;
            }
            return true;
        }

        private static bool RejectUrlTest(string reason, string message, Action<bool, string> completed, bool automatic = false)
        {
            GuruLog.W($"[OneLink] stage={(automatic ? "auto_url" : "test_url")}_rejected, reason={reason}.");
            completed?.Invoke(false, message);
            return false;
        }

        // 仅供主动解析的 sub10 回调关联和 ERROR 获取 URL；协议参数仍由 OneLinkRequest 决定。
        private static JObject GetCallbackValues(JObject payload)
        {
            JToken click = payload?["click_event"];
            if (click is JObject values) return values;
            if (click?.Type != JTokenType.String) return payload;
            try { return JObject.Parse((string)click); }
            catch (Exception) { return null; }
        }

        private static string GetCallbackMarker(JObject payload, JObject values = null) =>
            (values ?? GetCallbackValues(payload))?["deep_link_sub10"]?.ToString() ?? payload?["deep_link_sub10"]?.ToString();

        private static bool IsOwnedCallbackMarker(string marker) =>
            marker != null && (marker.StartsWith(UrlTestMarkerPrefix, StringComparison.Ordinal) ||
                               marker.StartsWith(AutomaticUrlMarkerPrefix, StringComparison.Ordinal));

        private bool StartUrlResolution(Uri uri, Action<bool, string> completed, bool automatic = false, bool isErrorRetry = false,
            bool manualSystemUrl = false)
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
#if UNITY_IOS
            if (!isErrorRetry && !manualSystemUrl)
                return RejectUrlTest("platform_not_supported", "iOS 短链仅支持 ERROR 恢复或 GM 主动请求当前系统 URL。", completed, automatic);
#endif
            if (!_appsFlyerBound || AppsFlyer.instance == null)
                return RejectUrlTest("sdk_not_ready", "AppsFlyer 尚未就绪，请完成正常启动后重试。", completed, automatic);

            foreach (string part in uri.Query.TrimStart('?').Split('&'))
            {
                string key = Uri.UnescapeDataString(part.Split('=')[0]);
                if (key == "deep_link_sub10")
                    return RejectUrlTest("reserved_test_parameter", "短链解析使用 sub10 关联回调，请勿在 URL 中填写 sub10。", completed, automatic);
            }

            var operation = new UrlResolveOperation
            {
                Id = (automatic ? AutomaticUrlMarkerPrefix : UrlTestMarkerPrefix) + Guid.NewGuid().ToString("N"),
                Url = uri.OriginalString,
                IsAutomatic = automatic,
                IsErrorRetry = isErrorRetry,
                IsManualSystemUrl = manualSystemUrl,
                Completed = completed
            };
            if (isErrorRetry)
                operation.Deadline = Math.Min(Time.realtimeSinceStartupAsDouble + ErrorRetryIntervalSeconds, _errorRetry.Deadline);
            _urlResolution = operation;
            if (automatic) _automaticUrls[operation.Id] = operation.Url;
            if (!automatic)
            {
                CancelGcdCandidate();
                _externalUrlSeen = true;
                _activation++;
                ResetActivationState(testActivation: true);
                // 新 GM URL 测试接管尚未进关的目标，避免旧 GCD 在等待新结果时先跳转。
                _pending = null;
                _pendingFromGcd = false;
                _waitReason = null;
                _pendingUrl = null;
                _sdkUrlBeforeSystem = null;
            }
            var builder = new UriBuilder(uri);
            string query = builder.Query.TrimStart('?');
            builder.Query = (query.Length == 0 ? "" : query + "&") + "deep_link_sub10=" + operation.Id;
            GuruLog.I($"[OneLink] stage={operation.LogStage}_started, request={operation.Id}, url={uri.GetLeftPart(UriPartial.Path)}, timeoutSeconds={operation.Deadline - Time.realtimeSinceStartupAsDouble:F2}.");
            WaitForUrlResolutionAsync(operation).Forget();
            try
            {
#if UNITY_ANDROID
                // 使用独立 Intent，不启动 Activity、不替换真实启动 Intent，也不修改 SDK 的全局订阅。
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var uriClass = new AndroidJavaClass("android.net.Uri");
                using var nativeUri = uriClass.CallStatic<AndroidJavaObject>("parse", builder.Uri.AbsoluteUri);
                using var intent = new AndroidJavaObject("android.content.Intent", "android.intent.action.VIEW", nativeUri);
                using var sdkClass = new AndroidJavaClass("com.appsflyer.AppsFlyerLib");
                using var sdk = sdkClass.CallStatic<AndroidJavaObject>("getInstance");
                sdk.Call("performOnDeepLinking", intent, activity);
#else
                // 仅重交本次真实 URL，不重复 startSDK；不修改 GuruSDK 的回调订阅。
                AppsFlyer.handleOpenUrl(builder.Uri.AbsoluteUri, null, null);
#endif
                return true;
            }
            catch (Exception exception)
            {
                GuruLog.W($"[OneLink] stage={operation.LogStage}_native_error, request={operation.Id}, error={exception.Message}.");
                FinishUrlResolution(operation, false, "native_error", "SDK 解析调用失败，请查看 [OneLink] 日志。");
                return false;
            }
#else
            return RejectUrlTest("platform_not_supported", "短链接主动解析目前支持 Android；iOS 由 SDK 自动 UDL 返回参数。", completed, automatic);
#endif
        }

        private async UniTask WaitForUrlResolutionAsync(UrlResolveOperation operation)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(Math.Max(0, operation.Deadline - Time.realtimeSinceStartupAsDouble)),
                    ignoreTimeScale: true, cancellationToken: operation.Timeout.Token);
                FinishUrlResolution(operation, false, "timeout", (operation.IsErrorRetry ? "重试等待超时，已停止本次等待。" : "解析超过 10 秒，已停止本次等待。") + operation.LastSdkStatus);
            }
            catch (OperationCanceledException) { }
        }

        private void CancelUrlResolution()
        {
            if (_urlResolution != null)
                FinishUrlResolution(_urlResolution, false, "cancelled", "已取消本次短链接等待。");
        }

        private void FinishUrlResolution(UrlResolveOperation operation, bool queued, string reason, string message)
        {
            if (_urlResolution != operation)
                return;
            _urlResolution = null;
            operation.Timeout.Cancel();
            operation.Timeout.Dispose();
            GuruLog.I($"[OneLink] stage={operation.LogStage}_finished, request={operation.Id}, queued={queued}, reason={reason}.");
            if (operation.IsAutomatic && (operation.SawSdkError || operation.IsErrorRetry && reason == "timeout") &&
                (reason == "sdk_failed" || reason == "timeout"))
                ScheduleErrorRetry(!string.IsNullOrWhiteSpace(_systemUrl) ? _systemUrl : operation.Url);
            else if (operation.IsErrorRetry && !queued && _errorRetry != null)
            {
                _errorRetry.Stopped = true;
                GuruLog.I($"[OneLink] stage=retry_stopped, reason={reason}.");
            }
            operation.Completed?.Invoke(queued, message);
        }

        private bool HandleUrlResolutionResult(DeepLinkEventsArgs args, JObject payload)
        {
            JObject values = GetCallbackValues(payload);
            string marker = GetCallbackMarker(payload, values);
            if (IsOwnedCallbackMarker(marker))
            {
                UrlResolveOperation operation = _urlResolution;
                if (operation == null || marker != operation.Id)
                {
                    if (args.status == DeepLinkStatus.FOUND && _automaticUrls.TryGetValue(marker, out string url))
                    {
                        bool accepted = AcceptResolvedUrl(payload, values, url, marker, "auto_url_late");
                        if (accepted && _urlResolution?.IsAutomatic == true)
                            FinishUrlResolution(_urlResolution, true, "late_success_received", "同次唤起的晚到结果已排队。");
                        return true;
                    }
                    GuruLog.I($"[OneLink] stage=url_late_ignored, request={marker}.");
                    return true;
                }
                operation.SawSdkError = args.status == DeepLinkStatus.ERROR;
                if (Time.realtimeSinceStartupAsDouble >= operation.Deadline &&
                    !(operation.IsAutomatic && args.status == DeepLinkStatus.FOUND))
                {
                    FinishUrlResolution(operation, false, "timeout", (operation.IsErrorRetry ? "重试等待超时，已停止本次等待。" : "解析超过 10 秒，已停止本次等待。") + operation.LastSdkStatus);
                    return true;
                }
                if (args.status != DeepLinkStatus.FOUND)
                {
                    FinishUrlResolution(operation, false, "sdk_failed", $"SDK 解析失败：{args.status} / {args.error}。");
                    return true;
                }

                bool queued = AcceptResolvedUrl(payload, values, operation.Url, marker, operation.LogStage);
                FinishUrlResolution(operation, queued, queued ? "queued" : "invalid_parameters",
                    queued ? "解析成功，已排队等待首页执行指令。" : "SDK 已返回，但指令参数无效，请查看日志。");
                return true;
            }

            if (_urlResolution != null && args.status != DeepLinkStatus.FOUND)
            {
                // 无请求 ID 的状态不能归属普通 GM 测试；正式原 URL 的 ERROR 由 HandleDeepLinkError 限次恢复，补发中的无标识状态只记录。
                _urlResolution.LastSdkStatus = $" 最近 SDK 状态：{args.status} / {args.error}（未关联请求）。";
                if (args.status == DeepLinkStatus.ERROR)
                    _urlResolution.SawSdkError = true;
                GuruLog.W($"[OneLink] stage={_urlResolution.LogStage}_unmatched_status, status={args.status}, error={args.error}.");
            }
            if (_urlResolution?.IsAutomatic == false && !args.isDeferred())
            {
                GuruLog.I("[OneLink] stage=test_url_unmatched_ignored. 本次测试仍等待携带请求标识的回调。");
                return true;
            }
            return false;
        }

        private bool AcceptResolvedUrl(JObject payload, JObject values, string url, string marker, string stage)
        {
            payload.Remove("deep_link_sub10");
            if (payload["click_event"]?.Type == JTokenType.String && values != null)
                payload["click_event"] = values;
            values ??= payload;
            values.Remove("deep_link_sub10");
            values["link"] = url;
            GuruLog.I($"[OneLink] stage={stage}_resolved, request={marker}.");
            LogPayload(payload, stage);
            // 仅已关联的 GM 短链或本轮 GM 系统 URL（含恢复）附带例外；真实 UDL/FB/GCD 不经过此授权。
            return AcceptPayloadCore(payload, false, resolvedLinkSource: null,
                bypassEntryHistory: marker.StartsWith(UrlTestMarkerPrefix, StringComparison.Ordinal) || _gmSystemUrlActivation);
        }

        #endregion
    }
}
