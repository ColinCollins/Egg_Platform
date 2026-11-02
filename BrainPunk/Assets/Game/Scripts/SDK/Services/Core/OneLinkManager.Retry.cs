using System;
using Guru.SDK.Framework.Analytics.Appsflyer;
using Newtonsoft.Json.Linq;
using Guru.SDK.Framework.Utils.Network;
using UnityEngine;
using GuruLog = Guru.SDK.Framework.Utils.Log.Log;

namespace Game
{
    /// <summary>UDL ERROR 的限次重试与网络预算；只管理游戏侧等待，不重新初始化 SDK。</summary>
    public sealed partial class OneLinkManager
    {
        #region ERROR 恢复

        private const int MaxErrorRetryAttempts = 1;
        private const double ErrorRetryBudgetSeconds = 10;
        private const double ErrorRetryIntervalSeconds = ErrorRetryBudgetSeconds / MaxErrorRetryAttempts;

        private sealed class ErrorRetryState
        {
            internal string Url;
            internal int Attempts;
            internal double NextAttemptAt;
            internal double Deadline;
            internal bool BudgetStarted;
            internal bool Scheduled;
            internal bool WaitingForNetwork;
            internal bool Stopped;
            internal bool BudgetExpired;
        }

        private ErrorRetryState _errorRetry;

        private void HandleDeepLinkError(JObject payload)
        {
            // ERROR 不是 UDL 的最终无目标结果。GCD 候选保留原五秒上限，
            // 同时允许有 URL 的 UDL 补发一次、总计十秒；任一路先成功即停止另一条。
            if (_urlResolution?.IsAutomatic == true && !_activationAccepted && !_isTestActivation)
            {
                // 原请求 ERROR 后直接交给恢复链；不先耗尽原请求等待。重试期间的无标识错误可能来自旧请求，继续等本次结果。
                if (!_urlResolution.IsErrorRetry)
                {
                    _urlResolution.SawSdkError = true;
                    FinishUrlResolution(_urlResolution, false, "sdk_failed", "SDK 返回 ERROR，等待使用原 URL 重试一次。");
                }
                return;
            }
            if (_urlResolution == null)
            {
                JToken link = GetCallbackValues(payload)?["link"];
                ScheduleErrorRetry(!string.IsNullOrWhiteSpace(_systemUrl) ? _systemUrl :
                    link?.Type == JTokenType.String ? (string)link : null);
            }
            if (_errorRetry?.Stopped == true)
                TryAcceptGcdCandidate("udl_error_no_retry");
        }

        // 首次原生网络有效后才开始十秒主动请求预算；预算开始后的首页门禁仍计时，SDK 晚到的有效结果仍可排队。
        private bool ExpireErrorRetry()
        {
            if (_errorRetry == null || !_errorRetry.BudgetStarted ||
                Time.realtimeSinceStartupAsDouble < _errorRetry.Deadline)
                return false;
            if (!_errorRetry.BudgetExpired)
            {
                _errorRetry.BudgetExpired = true;
                _errorRetry.Stopped = true;
                _errorRetry.Scheduled = false;
                _pendingUrl = null;
                if (_urlResolution?.IsAutomatic == true) CancelUrlResolution();
                GuruLog.I($"[OneLink] stage=retry_stopped, reason=total_timeout, attempts={_errorRetry.Attempts}, budgetSeconds=10.");
            }
            return true;
        }

        private static void StartErrorRetryBudget(ErrorRetryState retry)
        {
            if (retry == null || retry.BudgetStarted)
                return;

            double now = Time.realtimeSinceStartupAsDouble;
            retry.BudgetStarted = true;
            retry.Deadline = now + ErrorRetryBudgetSeconds;
            retry.NextAttemptAt = now + retry.Attempts * ErrorRetryIntervalSeconds;
            GuruLog.I($"[OneLink] stage=retry_budget_started, budgetSeconds={ErrorRetryBudgetSeconds:F0}.");
        }

        private void ScheduleErrorRetry(string url)
        {
            if (!_initialized || !_appsFlyerBound || _activationAccepted || _isTestActivation || _urlResolution != null)
                return;
            bool firstError = _errorRetry == null;
            bool hasRoutingUrl = IsRoutingUrl(url);
            if (hasRoutingUrl)
            {
                _explicitLinkReceived = true;
                CancelGcdCandidate();
            }
            _errorRetry ??= new ErrorRetryState { Url = hasRoutingUrl ? url : null };
            if (ExpireErrorRetry() || _errorRetry.Stopped || _errorRetry.Scheduled) return;
            _pendingUrl = null;
            if (string.IsNullOrWhiteSpace(_errorRetry.Url))
            {
#if UNITY_IOS && !UNITY_EDITOR
                if (firstError) GuruLog.I("[OneLink] stage=retry_waiting, reason=no_url_wait_install_conversion.");
                WaitForInstallRecoveryNetwork(GuruAppsflyerManager.Instance?.ConversionData);
#else
                _errorRetry.Stopped = true;
                if (firstError) GuruLog.W("[OneLink] stage=retry_stopped, reason=no_url.");
#endif
                return;
            }
            if (_errorRetry.Attempts >= MaxErrorRetryAttempts || !Uri.TryCreate(_errorRetry.Url.Trim(), UriKind.Absolute, out Uri uri))
            {
                _errorRetry.Stopped = true;
                GuruLog.W($"[OneLink] stage=retry_stopped, reason={(_errorRetry.Attempts >= MaxErrorRetryAttempts ? "attempt_limit" : "invalid_url")}, attempts={_errorRetry.Attempts}.");
                return;
            }
            // 已带合法协议参数的长链照常本地解析；不因 ERROR 强制依赖网络。
            if ((uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp || IsDirectUri(uri)) && !IsOneLinkShortUrl(uri) &&
                TryParseUrlQuery(uri, out JObject localPayload, out _) && OneLinkRequest.TryParse(localPayload, out _, out _))
            {
                _errorRetry.Stopped = true;
                _pendingUrl = _errorRetry.Url;
                GuruLog.I("[OneLink] stage=retry_local_fallback, reason=parameters_already_in_url.");
                return;
            }
            _errorRetry.Scheduled = true;
            _waitReason = null;
            if (NetworkUtils.IsNetworkConnected())
            {
                StartErrorRetryBudget(_errorRetry);
                _errorRetry.NextAttemptAt = Math.Max(Time.realtimeSinceStartupAsDouble,
                    _errorRetry.Deadline - ErrorRetryBudgetSeconds + _errorRetry.Attempts * ErrorRetryIntervalSeconds);
                GuruLog.I($"[OneLink] stage=retry_scheduled, activation={_activation}, attempt={_errorRetry.Attempts + 1}, delaySeconds={_errorRetry.NextAttemptAt - Time.realtimeSinceStartupAsDouble:F2}, remainingSeconds={_errorRetry.Deadline - Time.realtimeSinceStartupAsDouble:F2}.");
            }
            else
            {
                _errorRetry.WaitingForNetwork = true;
                GuruLog.I("[OneLink] stage=retry_waiting, reason=native_network_unavailable.");
            }
        }

        // 仍使用既有首页门禁；原生网络有效后才启动十秒预算，无网不发请求也不消耗次数。
        private void TryStartErrorRetry()
        {
            ErrorRetryState retry = _errorRetry;
            if (retry?.Scheduled != true || retry.Stopped)
                return;
            if (!NetworkUtils.IsNetworkConnected())
            {
                if (!retry.WaitingForNetwork) GuruLog.I("[OneLink] stage=retry_waiting, reason=native_network_unavailable.");
                retry.WaitingForNetwork = true;
                return;
            }
            StartErrorRetryBudget(retry);
            if (ExpireErrorRetry() || Time.realtimeSinceStartupAsDouble < retry.NextAttemptAt)
                return;
            retry.WaitingForNetwork = false;
            retry.Scheduled = false;
            retry.Attempts++;
            GuruLog.I($"[OneLink] stage=retry_started, activation={_activation}, attempt={retry.Attempts}, platform={Application.platform}.");
            if (!StartUrlResolution(new Uri(retry.Url.Trim()), null, automatic: true, isErrorRetry: true))
            {
                retry.Stopped = true;
                GuruLog.W("[OneLink] stage=retry_stopped, reason=request_not_started.");
            }
        }

        #endregion
    }
}
