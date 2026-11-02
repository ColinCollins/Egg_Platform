using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Facebook.Unity;
using Guru.SDK.Framework.Core.Analytics.Transmitter;
using Guru.SDK.Framework.Utils.Analytics;
using Newtonsoft.Json.Linq;
using UnityEngine;
using GuruLog = Guru.SDK.Framework.Utils.Log.Log;
#if UNITY_IOS && !UNITY_EDITOR
using Att = Unity.Advertisement.IosSupport.ATTrackingStatusBinding;
#endif

namespace Game
{
    /// <summary>首开 Facebook 原生延迟深链的有限优先窗口；不初始化 SDK、不伪造 AF 归因、不在回调中执行业务。</summary>
    public sealed partial class OneLinkManager
    {
        #region Facebook 首开状态

        private bool _facebookDdlStarted;
        private bool _facebookDdlWaiting;
        private bool _facebookDdlRequestPending;
        private bool _facebookDdlSelected;
        private double _facebookDdlDeadline;
        private CancellationTokenSource _facebookDdlWait;
#if UNITY_IOS && !UNITY_EDITOR
        // 授权交互不占取链预算，但也不能让安装候选无限等下去；不改变SDK自身的授权配置。
        private const double FacebookAttWaitSeconds = 30;
        private const int FacebookAttPollMilliseconds = 100;
        private bool _facebookDdlAttWaiting;
        private double _facebookDdlAttDeadline;
#endif

        #endregion

        #region 有限等待与原生获取

        private void BeginFacebookDeferredLink()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (!Application.isPlaying || !_initialized || _facebookDdlStarted || IgnoreAfInstallTarget ||
                _explicitLinkReceived || _externalUrlSeen || _isTestActivation ||
                _playingGameType.HasValue || _activationDiscarded || DB.GameData == null ||
                DB.GameData.PlayCount != 1)
                return;

            _facebookDdlStarted = true;
            _facebookDdlWaiting = true;
#if UNITY_IOS
            _facebookDdlAttWaiting = RequiresFacebookAtt();
            if (_facebookDdlAttWaiting)
            {
                _facebookDdlAttDeadline = Time.realtimeSinceStartupAsDouble + FacebookAttWaitSeconds;
                // 本次运行已建立的截止时间不因授权检查而重开。
                if (_nativeDdlWindowStarted)
                    _facebookDdlAttDeadline = Math.Min(_facebookDdlAttDeadline, _nativeDdlDeadline);
                GuruLog.I("[OneLink] stage=facebook_ddl_att_wait_started, maxWaitSeconds=30.");
            }
            else
                _facebookDdlDeadline = GetNativeDdlDeadline();
#else
            _facebookDdlDeadline = GetNativeDdlDeadline();
#endif
            _facebookDdlWait = new CancellationTokenSource();
            GuruLog.I("[OneLink] stage=facebook_ddl_wait_started, budgetSeconds=5.");
            WaitForFacebookDeferredLinkAsync(_facebookDdlWait.Token).Forget();
#endif
        }

        private async UniTask WaitForFacebookDeferredLinkAsync(CancellationToken token)
        {
            try
            {
                while (_facebookDdlWaiting)
                {
                    token.ThrowIfCancellationRequested();
                    ExpireFacebookDdlWait();
                    if (!_facebookDdlWaiting) return;
#if UNITY_IOS && !UNITY_EDITOR
                    if (_facebookDdlAttWaiting)
                    {
                        // 与Guru原授权流程使用相同的系统只读接口，不依赖可能尚未刷新的ATT缓存。
                        var status = Att.GetAuthorizationTrackingStatus();
                        if (status == Att.AuthorizationTrackingStatus.DENIED || status == Att.AuthorizationTrackingStatus.RESTRICTED)
                        {
                            CloseFacebookDdlWait(status == Att.AuthorizationTrackingStatus.DENIED ? "att_denied" : "att_restricted");
                            return;
                        }
                        if (status != Att.AuthorizationTrackingStatus.AUTHORIZED)
                        {
                            await UniTask.Delay(FacebookAttPollMilliseconds, DelayType.UnscaledDeltaTime, cancellationToken: token);
                            continue;
                        }

                        _facebookDdlAttWaiting = false;
                        _facebookDdlDeadline = GetNativeDdlDeadline();
                        GuruLog.I("[OneLink] stage=facebook_ddl_att_ready, attSystem=AUTHORIZED, fetchBudgetSeconds=5.");
                        ExpireFacebookDdlWait();
                        if (!_facebookDdlWaiting) return;
                    }
#endif
                    if (!_facebookDdlRequestPending && FB.IsInitialized)
                    {
#if UNITY_IOS && !UNITY_EDITOR
                        // SDK就绪可能晚于权限决定；若期间权限撤回，不再次等待或越过用户选择。
                        if (RequiresFacebookAtt() && Att.GetAuthorizationTrackingStatus() != Att.AuthorizationTrackingStatus.AUTHORIZED)
                        {
                            CloseFacebookDdlWait("att_not_authorized_before_request");
                            return;
                        }
#endif
                        // 先占用请求，兼容同步回调；只复用 Guru 的初始化，绝不再次 FB.Init。
                        _facebookDdlRequestPending = true;
                        GuruLog.I("[OneLink] stage=facebook_ddl_requested.");
                        LogFacebookDdlContext("request");
                        FB.Mobile.FetchDeferredAppLinkData(OnFacebookDeferredLink);
                    }
                    if (!_facebookDdlWaiting) return;
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                _facebookDdlRequestPending = false;
                string reason = "request_exception";
#if UNITY_IOS && !UNITY_EDITOR
                if (_facebookDdlAttWaiting) reason = "att_query_exception";
#endif
                CloseFacebookDdlWait(reason);
                // 异常消息可能含原始 URL 或 SDK 数据，只记录类型。
                GuruLog.W($"[OneLink] stage=facebook_ddl_failed, reason={exception.GetType().Name}.");
            }
        }

        private void OnFacebookDeferredLink(IAppLinkResult result) => HandleFacebookDeferredLinkAsync(result).Forget();

        private async UniTask HandleFacebookDeferredLinkAsync(IAppLinkResult result)
        {
            await UniTask.SwitchToMainThread();
            if (!_initialized || !Application.isPlaying || !_facebookDdlRequestPending)
                return;

            _facebookDdlRequestPending = false;
            bool afterDeadline = Time.realtimeSinceStartupAsDouble >= _facebookDdlDeadline;
            LogFacebookDdlResult(result, afterDeadline);
            CloseFacebookDdlWait("callback");
            if (_installDdlExecutionStarted || _ga4fDdlSelected || _explicitLinkReceived || _externalUrlSeen || _isTestActivation ||
                IgnoreLinkDuringGameplay("facebook_ddl"))
                return;

            try
            {
                if (result == null || result.Cancelled || !string.IsNullOrEmpty(result.Error))
                {
                    string detail = result == null ? "null_result" : result.Cancelled ? "cancelled" : "sdk_error";
                    GuruLog.I($"[OneLink] stage=facebook_ddl_no_target, reason=empty_or_failed_result, detail={detail}.");
                    return;
                }

                // 保留上游 TargetUrl／Url／Extras 解析；来源优先级与安装消费锁由统一受理处理。
                if (!TryParseFacebookPayload(result, out JObject payload, out _) ||
                    !OneLinkRequest.TryParse(payload, out _, out _))
                {
                    bool hasExtras = result.Extras != null || result.ResultDictionary?.ContainsKey("extras") == true;
                    string detail = string.IsNullOrWhiteSpace(result.TargetUrl) && string.IsNullOrWhiteSpace(result.Url) && !hasExtras
                        ? "missing_target" : "invalid_target";
                    GuruLog.I($"[OneLink] stage=facebook_ddl_no_target, reason=invalid_or_missing_target, detail={detail}.");
                    return;
                }

                if (!AcceptPayloadCore(payload, true, resolvedLinkSource: null, fromNativeDdl: true)) return;
                _facebookDdlSelected = true;
                _installRecoveryAccepted = false;
                CancelGa4fDeferredLink("install_target_selected");
                CancelUrlResolution();
                GuruLog.I($"[OneLink] stage=facebook_ddl_selected, afterDeadline={afterDeadline}.");
            }
            catch (Exception exception)
            {
                GuruLog.W($"[OneLink] stage=facebook_ddl_failed, reason={exception.GetType().Name}.");
            }
        }

        /// <summary>
        /// TargetUrl 优先于 Url，完整 URL 参数优先；参数不足时仅由 Extras 补充白名单 sub1–sub3。
        /// 支持 Android JSON/内层 extras；Ref 不作为配置，非法 scheme 或重复参数不能用 Extras 绕过。
        /// </summary>
        internal static bool TryParseFacebookPayload(IAppLinkResult result, out JObject payload, out string error)
        {
            payload = null;
            error = "empty_payload";
            if (result == null) return false;

            string url = string.IsNullOrWhiteSpace(result.TargetUrl) ? result.Url : result.TargetUrl;
            if (!string.IsNullOrWhiteSpace(url))
            {
                if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri uri) ||
                    (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp && !IsDirectUri(uri)))
                {
                    error = "unsupported_uri";
                    return false;
                }
                if (TryParseUrlQuery(uri, out payload, out error)) return true;
                if (error != "missing_deep_link_parameters") return false;
            }

            JObject extras = result.Extras == null ? null : JObject.FromObject(result.Extras);
            if (extras == null && result.ResultDictionary != null &&
                result.ResultDictionary.TryGetValue("extras", out object rawExtras) && rawExtras != null)
                extras = rawExtras is string json ? JObject.Parse(json) : JObject.FromObject(rawExtras);
            if (extras == null) return false;

            JObject values = extras["extras"] as JObject ?? extras;
            payload ??= new JObject { ["link"] = url };
            foreach (string key in new[] { "deep_link_value", "deep_link_sub1", "deep_link_sub2", "deep_link_sub3" })
            {
                if (payload[key] == null && values[key] != null)
                    payload[key] = values[key].DeepClone();
            }
            if (payload["deep_link_value"] == null || payload["deep_link_sub1"] == null)
            {
                error = "missing_deep_link_parameters";
                return false;
            }
            error = null;
            return true;
        }

#if UNITY_IOS && !UNITY_EDITOR
        /// <summary>iOS13沿用原取链路径；无法识别版本时仍查询权限，不猜测已授权。</summary>
        private static bool RequiresFacebookAtt() =>
            !Version.TryParse(UnityEngine.iOS.Device.systemVersion, out Version version) || version.Major >= 14;
#endif

        private void ExpireFacebookDdlWait()
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (_facebookDdlWaiting && _facebookDdlAttWaiting)
            {
                if (Time.realtimeSinceStartupAsDouble >= _facebookDdlAttDeadline)
                    CloseFacebookDdlWait("att_timeout");
                return;
            }
#endif
            if (_facebookDdlWaiting && Time.realtimeSinceStartupAsDouble >= _facebookDdlDeadline)
                CloseFacebookDdlWait("timeout");
        }

        private void CloseFacebookDdlWait(string reason)
        {
            if (!_facebookDdlWaiting) return;
            _facebookDdlWaiting = false;
#if UNITY_IOS && !UNITY_EDITOR
            _facebookDdlAttWaiting = false;
#endif
            var wait = _facebookDdlWait;
            _facebookDdlWait = null;
            wait?.Cancel();
            wait?.Dispose();
            GuruLog.I($"[OneLink] stage=facebook_ddl_wait_finished, reason={reason}.");
            LogFacebookDdlContext("wait_finished");
        }

        private void CancelFacebookDeferredLink(string reason)
        {
            // 只取消游戏侧等待／受理资格，不能宣称中止了已发出的原生网络请求。
            _facebookDdlRequestPending = false;
            CloseFacebookDdlWait(reason);
        }

        #endregion

        #region 脱敏诊断

        /// <summary>只读缓存及系统ATT快照，不触发权限刷新／弹窗或探测Meta连通性；诊断异常不得阻止获取与受理。</summary>
        private static void LogFacebookDdlContext(string point)
        {
            try
            {
                string att = "not_applicable";
                string attSystem = "not_applicable";
#if UNITY_IOS && !UNITY_EDITOR
                att = Guru.SDK.Framework.Core.Consent.ConsentManager.AttStatus.ToString();
                attSystem = RequiresFacebookAtt() ? Att.GetAuthorizationTrackingStatus().ToString() : "not_applicable";
#endif
                bool connected = Guru.SDK.Framework.Utils.Network.NetworkUtils.IsNetworkConnected();
                GuruLog.I($"[OneLink] stage=facebook_ddl_context, diagnosticVersion=1, point={point}, " +
                          $"sdkInitialized={FB.IsInitialized}, attCached={att}, attSystem={attSystem}, networkConnected={connected}.");
            }
            catch (Exception exception)
            {
                GuruLog.W($"[OneLink] stage=facebook_ddl_diagnostics_failed, point={point}, error={exception.GetType().Name}.");
            }
        }

        /// <summary>在业务仲裁前记录回调形态，不输出Error、Result或URL原文；存在字段不代表目标已经通过校验。</summary>
        private static void LogFacebookDdlResult(IAppLinkResult result, bool afterDeadline)
        {
            try
            {
                bool hasExtras = result?.Extras != null || result?.ResultDictionary?.ContainsKey("extras") == true;
                GuruLog.I($"[OneLink] stage=facebook_ddl_result, diagnosticVersion=1, resultPresent={result != null}, " +
                          $"cancelled={result?.Cancelled ?? false}, hasError={!string.IsNullOrEmpty(result?.Error)}, " +
                          $"errorHint={DescribeFacebookDdlError(result?.Error)}, " +
                          $"hasTargetUrl={!string.IsNullOrWhiteSpace(result?.TargetUrl)}, hasUrl={!string.IsNullOrWhiteSpace(result?.Url)}, " +
                          $"hasExtras={hasExtras}, afterDeadline={afterDeadline}.");
            }
            catch (Exception exception)
            {
                GuruLog.W($"[OneLink] stage=facebook_ddl_diagnostics_failed, point=result, error={exception.GetType().Name}.");
            }
        }

        /// <summary>错误描述只映射固定提示，未知语言／内容保持unknown；文本线索不是网络请求或后台错误的确证。</summary>
        private static string DescribeFacebookDdlError(string error)
        {
            if (string.IsNullOrEmpty(error)) return "none";
            bool Has(string text) => error.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;

            // 对应本工程FBSDK 17.0.3的两个请求前拒绝分支，不以当前ATT缓存值反推错误。
            if (Has("ATTrackingManager.AuthorizationStatus must be `authorized`")) return "att_or_idfa_required";
            if (Has("AdvertiserTrackingEnabled must be enabled")) return "advertiser_tracking_required";
            if (Has("timed out") || Has("请求超时") || Has("要求逾時")) return "network_timeout_hint";
            if (Has("Internet connection appears to be offline") || Has("似乎已断开与互联网") || Has("似乎已離線")) return "network_offline_hint";
            if (Has("hostname could not be found") || Has("找不到指定主機名稱") || Has("找不到使用指定主机名")) return "network_dns_hint";
            if (Has("SSL error") || Has("secure connection") || Has("安全连接") || Has("安全連線")) return "network_tls_hint";
            if (Has("NSURLErrorDomain") || Has("network connection was lost") || Has("Could not connect to the server")) return "network_transport_hint";
            if (Has("client token")) return "client_token_hint";
            if (Has("Invalid App ID") || Has("Invalid application ID")) return "app_id_hint";
            if (Has("validating access token") || Has("Invalid OAuth access token")) return "access_token_hint";
            return "unknown";
        }

        #endregion

        #region Guru 公开生命周期接点

        /// <summary>排在 AF 前、现有状态同步步骤后的 Initialize 接点；不保证 ATT 已决定，不发送统计或改变授权。</summary>
        internal sealed class FacebookDdlHook : AnalyticsTransmitter
        {
            /// <summary>先回Unity主线程再建立Facebook优先窗口；不等待原生网络完成或重复初始化SDK。</summary>
            public override async UniTask Initialize()
            {
                // Guru公开生命周期可能从后台线程进入；首次读取Unity状态／存档前完成线程切换。
                await UniTask.SwitchToMainThread();
                Instance.BeginFacebookDeferredLink();
            }

            /// <summary>不参与统计事件发送，所有渠道的原有统计能力保持不变。</summary>
            public override AnalyticsCapabilities Transmit(string name, Dictionary<string, object> parameters,
                AnalyticsCapabilities capabilities, AppEventOptions options = null) => AnalyticsCapabilities.EmptyCapabilities;

            /// <summary>不处理用户属性，交由既有统计通道发送。</summary>
            public override void TransmitProperty(string key, string value) { }
        }

        #endregion
    }
}
