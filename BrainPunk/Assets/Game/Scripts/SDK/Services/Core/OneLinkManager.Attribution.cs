using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Guru.SDK.Framework.Analytics.Appsflyer;
using Guru.SDK.Framework.Utils.Network;
using Newtonsoft.Json.Linq;
using R3;
using UnityEngine;
using GuruLog = Guru.SDK.Framework.Utils.Log.Log;

namespace Game
{
    /// <summary>安装归因候选与 iOS 无 URL 的网络恢复等待；有效目标仍交由首页门禁处理。</summary>
    public sealed partial class OneLinkManager
    {
        #region 归因状态

        private IDisposable _conversionSubscription;
        private IDisposable _conversionFailedSubscription;
        private CancellationTokenSource _gcdAnalyticsTimeout;
        private bool _gcdAnalyticsStarted;
        private bool _gcdAnalyticsCompleted;
        private string _lastGcdAnalyticsRaw;
        private CancellationTokenSource _cachedConversionProbe;
        private string _lastCachedConversionRaw;
        private JObject _gcdCandidate;
        private CancellationTokenSource _gcdTimeout;
        private bool _udlResolved;
        private bool _udlAccepted;
        private bool _externalUrlSeen;
        private bool _pendingFromGcd;
        private bool _gcdEntryAttempted;
#if UNITY_IOS && !UNITY_EDITOR
        private IDisposable _installRecoveryNetworkSubscription;
        private AppsflyerConversionData _networkDeferredConversionData;
#endif

        private const int GcdWaitForUdlSeconds = 5;
        private const int CachedConversionProbeMilliseconds = 5000;
        private const int CachedConversionPollMilliseconds = 100;
        // 仅统计 SDK 初始化后的结果等待，不取消真实 GCD，也不复用 UDL 仲裁的五秒预算。
        private const int GcdAnalyticsWaitSeconds = 10;

        #endregion

        #region GCD 接收绑定与释放

        /// <summary>
        /// 仅在 UDL 接收绑定后订阅 Guru 的成功/失败数据流；初值同步回放仍先于共同绑定标记设置。
        /// 不主动补拉、不重复初始化 SDK，GCD 结果等待仍由原 Hook.Initialize 启动。
        /// </summary>
        private void BindConversionDataReceiver(GuruAppsflyerManager receiver)
        {
            // GuruSDK 已经是原生 GCD 的接收者；仅订阅其公开数据流，不重复初始化或修改 SDK。
            // iOS 的 ERROR 首装补偿也复用这一条订阅，不能再单独覆盖 _conversionSubscription。
            BeginGcdAnalytics();
            _conversionSubscription = receiver.ObservableConversionData.Subscribe(OnConversionDataChanged);
            _conversionFailedSubscription = receiver.ObservableConversionDataFailed.Subscribe(OnConversionDataFailed);
        }

        /// <summary>仅由 Dispose 释放本服务的两个 GCD 订阅；缓存探测和超时仍按原清理顺序独立取消。</summary>
        private void UnbindConversionDataReceiver()
        {
            _conversionSubscription?.Dispose();
            _conversionSubscription = null;
            _conversionFailedSubscription?.Dispose();
            _conversionFailedSubscription = null;
        }

        #endregion

        #region GCD 打点

        private void BeginGcdAnalytics()
        {
            if (_gcdAnalyticsStarted) return;
            _gcdAnalyticsStarted = true;
            GameSDKService.Instance.OneLinkGcdEvent("start", "receiver_bound");
        }

        private void StartGcdAnalyticsTimeout()
        {
            if (!_initialized || !Application.isPlaying || !_appsFlyerBound ||
                _gcdAnalyticsCompleted || _gcdAnalyticsTimeout != null) return;
            _gcdAnalyticsTimeout = new CancellationTokenSource();
            WaitForGcdAnalyticsAsync(_gcdAnalyticsTimeout.Token).Forget();
        }

        private async UniTask WaitForGcdAnalyticsAsync(CancellationToken token)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(GcdAnalyticsWaitSeconds), ignoreTimeScale: true,
                    cancellationToken: token);
                if (token.IsCancellationRequested || !_initialized || !Application.isPlaying || _gcdAnalyticsCompleted)
                    return;
                _gcdAnalyticsCompleted = true;
                GameSDKService.Instance.OneLinkGcdEvent("failed", "game_wait", "timeout",
                    waitSeconds: GcdAnalyticsWaitSeconds);
            }
            catch (OperationCanceledException) { }
        }

        private void CancelGcdAnalyticsTimeout()
        {
            _gcdAnalyticsTimeout?.Cancel();
            _gcdAnalyticsTimeout?.Dispose();
            _gcdAnalyticsTimeout = null;
        }

        private void OnConversionDataFailed(string error)
        {
            // BehaviorSubject 首次订阅立即返回空字符串；只把 SDK 的非空错误视为失败。
            if (!string.IsNullOrWhiteSpace(error)) ReportGcdFailureAsync(error).Forget();
        }

        private async UniTask ReportGcdFailureAsync(string error)
        {
            await UniTask.SwitchToMainThread();
            if (!_initialized || !Application.isPlaying) return;
            _gcdAnalyticsCompleted = true;
            CancelGcdAnalyticsTimeout();
            GameSDKService.Instance.OneLinkGcdEvent("failed", "sdk_callback", error);
        }

        private void ReportGcdReturned(AppsflyerConversionData data, string source)
        {
            if (string.Equals(_lastGcdAnalyticsRaw, data.RawConversionData, StringComparison.Ordinal)) return;
            _lastGcdAnalyticsRaw = data.RawConversionData;
            _gcdAnalyticsCompleted = true;
            CancelGcdAnalyticsTimeout();
            try
            {
                JObject payload = JObject.FromObject(data.ConversionData);
                bool valid = TryResolveGcdTarget(payload, out JObject target, out string resolution, out string error,
                    logDiagnostics: false);
                GameSDKService.Instance.OneLinkGcdEvent("returned", source,
                    conversion: payload, command: target ?? payload, resolution: valid ? resolution : "none",
                    parseError: valid ? null : error ?? "no_target");
            }
            catch (Exception)
            {
                GameSDKService.Instance.OneLinkGcdEvent("failed", source, "invalid_conversion_data");
            }
        }

        #endregion

        #region SDK 缓存快照

        // GuruSDK 的缓存命中路径只更新 GuruAppsflyer.ConversionData，不会重放 ObservableConversionData。
        // 游戏侧短时读取公开快照，补上首装数据与 OneLink 路由之间的断点；不修改 SDK，也不阻塞启动。
        private void StartCachedConversionProbe()
        {
            _cachedConversionProbe?.Cancel();
            _cachedConversionProbe?.Dispose();
            _cachedConversionProbe = new CancellationTokenSource();
            ProbeCachedConversionAsync(_cachedConversionProbe.Token).Forget();
        }

        private async UniTask ProbeCachedConversionAsync(CancellationToken token)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + CachedConversionProbeMilliseconds / 1000d;
            try
            {
                while (Time.realtimeSinceStartupAsDouble < deadline)
                {
                    AppsflyerConversionData cached = GuruAppsflyer.ConversionData;
                    if (cached != null && cached.IsValid)
                    {
                        if (!string.Equals(_lastCachedConversionRaw, cached.RawConversionData, StringComparison.Ordinal))
                        {
                            _lastCachedConversionRaw = cached.RawConversionData;
                            GuruLog.I("[OneLink] stage=conversion_cache_seen, source=GuruAppsflyer.");
                            await HandleConversionDataAsync(cached, "sdk_cache");
                        }
                        return;
                    }

                    await UniTask.Delay(CachedConversionPollMilliseconds, DelayType.UnscaledDeltaTime,
                        cancellationToken: token);
                }

                GuruLog.I("[OneLink] stage=conversion_cache_miss, window_ms=" + CachedConversionProbeMilliseconds + ".");
            }
            catch (OperationCanceledException) { }
        }

        #endregion

        #region GCD 备用目标

        private void OnConversionDataChanged(AppsflyerConversionData data)
        {
            HandleConversionDataAsync(data).Forget();
        }

        private async UniTask HandleConversionDataAsync(AppsflyerConversionData data, string analyticsSource = "sdk_callback")
        {
            await UniTask.SwitchToMainThread();
            if (!_initialized || !Application.isPlaying || data == null || !data.IsValid) return;
            ReportGcdReturned(data, analyticsSource);
            if (IgnoreLinkDuringGameplay("conversion_callback")) return;
            if (IgnoreAfInstallTarget) return;
            // 十秒只截止主动重试；晚到的有效安装数据仍经过 GCD/首装仲裁和首页门禁。
            ExpireErrorRetry();
#if UNITY_IOS && !UNITY_EDITOR
            // 同一份 SDK 转化数据：正式 ERROR 且没有 URL 时先试远端的首装补偿，
            // 其它情况仍走原有 GCD 五秒候选；只由一个回调订阅者仲裁，不重复入队。
            if (_errorRetry != null && string.IsNullOrWhiteSpace(_errorRetry.Url))
            {
                if (!NetworkUtils.IsNetworkConnected())
                {
                    WaitForInstallRecoveryNetwork(data);
                    return;
                }
                HandleInstallConversion(data);
                if (_installRecoveryAccepted) return;
            }
#endif
            if (_externalUrlSeen || _explicitLinkReceived || _isTestActivation || _gcdEntryAttempted)
                return;

            JObject payload;
            try { payload = JObject.FromObject(data.ConversionData); }
            catch (Exception exception)
            {
                GuruLog.W($"[OneLink] stage=gcd_rejected, reason=invalid_conversion_data, error={exception.Message}.");
                return;
            }

            if (!string.Equals(payload.Value<string>("af_status"), "Non-organic", StringComparison.OrdinalIgnoreCase) ||
                !bool.TryParse(payload["is_first_launch"]?.ToString(), out bool firstLaunch) || !firstLaunch)
                return;
            RememberAfInstallSource(payload);
            // UDL 目标已排队时仍可用真实首装 GCD 补齐渠道；只调整相应等待，不替换 UDL 目标。
            if (_udlAccepted || _activationAccepted || _gcdCandidate != null) return;
            if (!TryResolveGcdTarget(payload, out JObject target, out string resolution, out string error))
            {
                GuruLog.I($"[OneLink] stage=gcd_no_target, reason={error}. SRN campaign 未命中受控命名规则。");
                return;
            }
            if (resolution == "campaign")
                GuruLog.I("[OneLink] stage=gcd_campaign_mapped.");

            _gcdCandidate = target;
            GuruLog.I("[OneLink] stage=gcd_candidate, firstLaunch=True, status=Non-organic.");
            if (_udlResolved)
                TryAcceptGcdCandidate("udl_without_target");
            else
            {
                _gcdTimeout = new CancellationTokenSource();
                WaitForGcdFallbackAsync(_gcdTimeout.Token).Forget();
            }
        }

        // 广告 URL 仅接受固定 URI 的完整目标，奖励 ID 沿用通用非空校验；原字段和 campaign 保持兜底。
        private static bool TryResolveGcdTarget(JObject conversion, out JObject target, out string resolution, out string error,
            bool logDiagnostics = true)
        {
            target = conversion;
            resolution = "existing";
            error = null;
            if (TryParseAdDdlUrlTarget(conversion, out JObject urlTarget, out OneLinkRequest urlRequest, logDiagnostics))
            {
                if (logDiagnostics && TryMapSrnCampaignTarget(conversion, out JObject campaignTarget) &&
                    OneLinkRequest.TryParse(campaignTarget, out OneLinkRequest campaignRequest, out _) &&
                    !IsSameAdAndCampaignTarget(urlRequest, campaignRequest))
                    GuruLog.W("[OneLink] stage=gcd_ad_url, reason=campaign_target_mismatch, url_priority=true.");
                target = urlTarget;
                resolution = "af_dp";
                if (logDiagnostics) GuruLog.I("[OneLink] stage=gcd_ad_url, result=accepted.");
                return true;
            }

            if (OneLinkRequest.TryParse(conversion, out _, out error)) return true;
            if (TryMapSrnCampaignTarget(conversion, out target))
            {
                resolution = "campaign";
                return true;
            }
            target = null;
            return false;
        }

        // 奖励还须比较实际奖品与用户限制，不能只用领取 ID 判断两个目标相同。
        private static bool IsSameAdAndCampaignTarget(OneLinkRequest url, OneLinkRequest campaign)
        {
            if (url.Action != campaign.Action) return false;
            if (url.Action == OneLinkAction.OpenLevel)
                return url.GameType == campaign.GameType &&
                       string.Equals(url.LevelPath, campaign.LevelPath, StringComparison.OrdinalIgnoreCase);

            return string.Equals(url.RewardId, campaign.RewardId, StringComparison.Ordinal) &&
                   string.Equals(url.TargetUserId, campaign.TargetUserId, StringComparison.Ordinal) &&
                   (url.Action == OneLinkAction.GetCoin
                       ? url.CoinCount == campaign.CoinCount
                       : string.Equals(url.SkinId, campaign.SkinId, StringComparison.Ordinal));
        }

        private async UniTask WaitForGcdFallbackAsync(CancellationToken token)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(GcdWaitForUdlSeconds), ignoreTimeScale: true,
                    cancellationToken: token);
                TryAcceptGcdCandidate("udl_wait_5s_elapsed");
            }
            catch (OperationCanceledException) { }
        }

        private void TryAcceptGcdCandidate(string reason)
        {
            if (_gcdCandidate == null || _udlAccepted || _externalUrlSeen || _gcdEntryAttempted)
                return;
            ExpireErrorRetry();
            JObject candidate = _gcdCandidate;
            CancelGcdCandidate();
            if (_pending != null || _pendingUrl != null)
            {
                GuruLog.I("[OneLink] stage=gcd_skipped, reason=another_link_pending.");
                return;
            }
            if (AcceptPayload(candidate, true))
            {
                _pendingFromGcd = true;
                // GCD 已取得有效目标后终止还在等待的自动解析；迟到的标记回调会被忽略。
                if (_urlResolution?.IsAutomatic == true)
                    FinishUrlResolution(_urlResolution, true, "gcd_received", "GCD 已排队等待首页进入关卡。");
                GuruLog.I($"[OneLink] stage=gcd_queued, reason={reason}.");
            }
        }

        private void CancelGcdCandidate()
        {
            _gcdCandidate = null;
            if (_gcdTimeout == null)
                return;
            _gcdTimeout.Cancel();
            _gcdTimeout.Dispose();
            _gcdTimeout = null;
        }

        #endregion

        #region iOS 安装恢复

        private void CancelInstallRecoveryNetworkWait()
        {
#if UNITY_IOS && !UNITY_EDITOR
            _installRecoveryNetworkSubscription?.Dispose();
            _installRecoveryNetworkSubscription = null;
            _networkDeferredConversionData = null;
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        private void WaitForInstallRecoveryNetwork(AppsflyerConversionData data = null)
        {
            if (_errorRetry == null || !string.IsNullOrWhiteSpace(_errorRetry.Url) ||
                _activationAccepted || _explicitLinkReceived || _isTestActivation ||
                _playingGameType.HasValue || _activationDiscarded)
                return;

            if (data?.IsValid == true)
                _networkDeferredConversionData = data;
            if (NetworkUtils.IsNetworkConnected())
            {
                ResumeInstallRecoveryAfterNetwork();
                return;
            }
            if (_installRecoveryNetworkSubscription != null)
                return;

            GuruLog.I("[OneLink] stage=retry_waiting, reason=native_network_unavailable.");
            _installRecoveryNetworkSubscription = NetworkUtils.ObservableConnectivityTrack.Subscribe(track =>
            {
                if (NetworkUtils.IsValidNetwork(track.PrimaryNetwork))
                    ResumeInstallRecoveryAfterNetwork();
            });
        }

        private void ResumeInstallRecoveryAfterNetwork()
        {
            AppsflyerConversionData pendingData = _networkDeferredConversionData;
            CancelInstallRecoveryNetworkWait();
            if (!_initialized || !Application.isPlaying || _errorRetry == null ||
                !string.IsNullOrWhiteSpace(_errorRetry.Url) || _activationAccepted || _explicitLinkReceived ||
                _isTestActivation || _playingGameType.HasValue || _activationDiscarded)
                return;

            StartErrorRetryBudget(_errorRetry);
            // getConversionData 只注册原生回调；恢复时复用既有数据流，不把重复注册当作联网补拉。
            AppsflyerConversionData availableData = pendingData?.IsValid == true
                ? pendingData : GuruAppsflyerManager.Instance?.ConversionData;
            GuruLog.I($"[OneLink] stage=install_recovery_network_ready, network={NetworkUtils.CurrentConnectivityStatus}, hasData={availableData?.IsValid == true}.");
            if (availableData?.IsValid == true)
                HandleConversionDataAsync(availableData).Forget();
        }
        // 安装回调独立于主动重试预算；只补收首次安装有效参数，真实链接/GM 测试和已受理目标优先。
        private void HandleInstallConversion(AppsflyerConversionData data)
        {
            // 唯一调用点 HandleConversionDataAsync 已切到主线程并校验数据与玩法状态。
            try
            {
                JObject payload = JObject.Parse(data.RawConversionData);
                LogPayload(payload, "install_recovery");
                if (_activationAccepted || _explicitLinkReceived || _isTestActivation)
                {
                    GuruLog.I($"[OneLink] stage=install_recovery_ignored, reason={(_activationAccepted ? "already_accepted" : "newer_link_or_test")}.");
                    return;
                }
                bool firstLaunch = bool.TryParse(payload["is_first_launch"]?.ToString(), out bool value) && value;
                if (!firstLaunch || !string.Equals(payload["af_status"]?.ToString(), "Non-organic", StringComparison.OrdinalIgnoreCase))
                {
                    GuruLog.I("[OneLink] stage=install_recovery_ignored, reason=not_first_nonorganic_install.");
                    return;
                }
                bool afterRetryDeadline = _errorRetry?.BudgetStarted == true &&
                                          Time.realtimeSinceStartupAsDouble >= _errorRetry.Deadline;
                RememberAfInstallSource(payload);
                if (!TryResolveGcdTarget(payload, out JObject target, out string resolution, out _))
                {
                    GuruLog.I("[OneLink] stage=install_recovery_ignored, reason=no_target.");
                    return;
                }
                if (resolution == "campaign") GuruLog.I("[OneLink] stage=gcd_campaign_mapped.");
                if (!AcceptPayload(target, true)) return;
                _installRecoveryAccepted = true;
                if (_urlResolution?.IsAutomatic == true)
                    FinishUrlResolution(_urlResolution, true, "install_recovery_received", "安装回调已返回有效参数。");
                if (!string.IsNullOrEmpty(_pending.Url)) _sdkUrlBeforeSystem = _pending.Url;
                GuruLog.I($"[OneLink] stage=install_recovery_queued, afterRetryDeadline={afterRetryDeadline}.");
            }
            catch (Exception exception)
            {
                GuruLog.W($"[OneLink] stage=install_recovery_rejected, reason={exception.GetType().Name}.");
            }
        }
#endif

        #endregion
    }
}
