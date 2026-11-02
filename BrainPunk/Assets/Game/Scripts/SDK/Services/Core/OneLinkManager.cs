using System;
using System.Collections.Generic;
using AppsFlyerSDK;
using Bear.UI;
using Config.Level;
using Cysharp.Threading.Tasks;
using Game.Play;
using Game.Scripts.Common;
using Guru.SDK.Framework.Analytics.Appsflyer;
using Guru.SDK.Framework.Core.Analytics.Transmitter;
using Guru.SDK.Framework.Utils.Analytics;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using GuruLog = Guru.SDK.Framework.Utils.Log.Log;

namespace Game
{
    /// <summary>接收 Facebook／GA4F 首开目标、OneLink UDL 参数及首次安装的 GCD 备用参数，暂存请求，仅在首页启动弹窗和进场动画结束后执行进关或奖励。
    /// 不主动打开首页、不打断关卡、不阻塞 Loading 或改写首次安装状态；SDK 初始化仍由 GuruSDK 管理。</summary>
    public sealed partial class OneLinkManager
    {
        #region 生命周期与状态

        private static OneLinkManager _instance = new OneLinkManager();

        /// <summary>应用会话内的 OneLink 服务；访问不会创建 Unity 对象或初始化 SDK。</summary>
        public static OneLinkManager Instance => _instance;

        /// <summary>最近一次有效 OneLink 业务 URL；忽略无关地址，延迟深链可能只有参数而没有完整 URL。</summary>
        public string Url { get; private set; } = string.Empty;

        /// <summary>是否有等待首页处理的参数或 URL（含解析中）；普通启动不会重放已消费的请求。</summary>
        public bool HasPendingRequest => _pending != null || _pendingUrl != null || _urlResolution?.IsAutomatic == true ||
                                         _gcdCandidate != null || _errorRetry?.Scheduled == true;

        private bool _initialized;
        private bool _appsFlyerBound;
        private bool _startupPopupsDispatched;
        private long _activation;
        private string _lastRequestKey;
        private string _waitReason;
        private OneLinkRequest _pending;
        private string _pendingUrl;
        private string _systemUrl;
        private string _sdkUrlBeforeSystem;
        private bool _activationAccepted;
        private bool _installRecoveryAccepted;
        private bool _explicitLinkReceived;
        private bool _isTestActivation;
        // GM 主动请求系统 URL 及其 ERROR 恢复沿用同轮例外；真实新 URL 重置，不能从 URL 参数取得此权限。
        private bool _gmSystemUrlActivation;
        private GameType? _playingGameType;
        private bool _activationDiscarded;
        private JObject _homeCallbackAwaitingUrl;
        private OneLinkManager() { }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            _instance.Dispose();
            _instance = new OneLinkManager();
        }

        /// <summary>由游戏 SDK 启动入口调用一次，提前捕获冷启动 URL 和后续唤起；只允许运行态调用。</summary>
        internal void Initialize()
        {
            if (!Application.isPlaying || _initialized)
                return;

            _initialized = true;
            Application.deepLinkActivated += OnApplicationLink;
            Application.quitting += Dispose;
            if (!string.IsNullOrEmpty(Application.absoluteURL))
                OnApplicationLink(Application.absoluteURL);
        }

        private void Dispose()
        {
            CancelNativeDeferredLinks("disposed");
            CancelInstallRecoveryNetworkWait();
            CancelGcdCandidate();
            _cachedConversionProbe?.Cancel();
            _cachedConversionProbe?.Dispose();
            _cachedConversionProbe = null;
            _lastCachedConversionRaw = null;
            UnbindConversionDataReceiver();
            CancelGcdAnalyticsTimeout();
            _gcdAnalyticsStarted = false;
            _gcdAnalyticsCompleted = false;
            _lastGcdAnalyticsRaw = null;
            CancelUrlResolution();
            Application.deepLinkActivated -= OnApplicationLink;
            Application.quitting -= Dispose;
            UnbindUnifiedDeepLinkReceiver();
            _initialized = false;
            _pending = null;
            _pendingUrl = null;
            _errorRetry = null;
            _automaticUrls.Clear();
            _homeCallbackAwaitingUrl = null;
        }

        private void ResetActivationState(bool testActivation = false)
        {
            ResetNativeInstallPriority();
            CancelInstallRecoveryNetworkWait();
            _activationDiscarded = false;
            _homeCallbackAwaitingUrl = null;
            _errorRetry = null;
            _automaticUrls.Clear();
            _activationAccepted = false;
            _installRecoveryAccepted = false;
            _isTestActivation = testActivation;
            _gmSystemUrlActivation = false;
        }

        #endregion

        #region 玩法进入历史与本轮标记

        private GameType? _entryGameType;
        // 在路由预留时固定来源，异步加载期间不得由新回调改变历史写入策略。
        private bool _entryBypassesHistory;

        /// <summary>路由前预留本次玩法；已有成功历史不限制再次进入，加载或运行中的预留仍拒绝并发；GM 保留不写历史的例外。</summary>
        /// <param name="bypassEntryHistory">仅来自游戏侧 GM 请求标记；成功后不写永久历史，不绕过其它进关条件。</param>
        internal bool TryBeginEntry(GameType gameType, out string error, bool bypassEntryHistory = false)
        {
            if (DB.GameData == null)
            {
                error = "GameData is unavailable.";
                return false;
            }
            DB.GameData.OneinlinkEnterType ??= new List<GameType>();
            if (_entryGameType.HasValue)
            {
                error = "Another OneLink entry is loading or running.";
                return false;
            }

            _entryGameType = gameType;
            _entryBypassesHistory = bypassEntryHistory;
            DB.GameData.IsEnterByOneLink = false;
            error = null;
            return true;
        }

        /// <summary>各玩法接受新关/新组时调用；保留本次 OneLink 来源并丢弃其它待跳转任务，普通进关清除上轮标记。同关 Reset 和组内推进不调用。</summary>
        internal void OnLevelEntryStarted(GameType gameType)
        {
            _playingGameType = gameType;
            DiscardPendingLink("gameplay_started");
            if (_entryGameType == gameType && DB.GameData?.IsEnterByOneLink == false)
                return;

            _entryGameType = null;
            _entryBypassesHistory = false;
            if (DB.GameData != null)
                DB.GameData.IsEnterByOneLink = false;
        }

        /// <summary>仅由当前有效关卡加载完成点调用；正式首次成功保存玩法历史，GM 只保留运行时来源，Reset 和 IWanner 组内推进不会重复保存。</summary>
        internal void ConfirmLevelEntered(GameType gameType)
        {
            if (_entryGameType != gameType || DB.GameData == null || DB.GameData.IsEnterByOneLink)
                return;

            DB.GameData.IsEnterByOneLink = true;
            if (_entryBypassesHistory)
            {
                GuruLog.I($"[OneLink] stage=entry_completed, gameType={gameType}, source=gm, historyRecorded=False.");
                return;
            }
            if (!DB.GameData.OneinlinkEnterType.Contains(gameType))
                DB.GameData.OneinlinkEnterType.Add(gameType);
            bool saved = DB.SaveGameData();
            GuruLog.I($"[OneLink] stage=entry_completed, gameType={gameType}, saved={saved}.");
            if (!saved)
                GuruLog.W($"[OneLink] stage=save_warning, gameType={gameType}, reason=entry_history.");
        }

        /// <summary>退出、加载失败或路由拒绝时清除对应玩法的本轮标记；不删除成功历史，不干扰正在切入的其它玩法。</summary>
        internal void EndEntry(GameType gameType)
        {
            if (_entryGameType != gameType)
                return;

            _entryGameType = null;
            _entryBypassesHistory = false;
            if (DB.GameData != null)
                DB.GameData.IsEnterByOneLink = false;
            GuruLog.I($"[OneLink] stage=entry_context_cleared, gameType={gameType}.");
        }

        /// <summary>胜利面板打开时读取本轮来源；IWanner 组内切小关继续属于同一次进入。</summary>
        internal bool IsCurrentEntry(GameType gameType) =>
            _entryGameType == gameType && DB.GameData?.IsEnterByOneLink == true;

        #endregion

        #region 游戏模式跳转门禁

        /// <summary>首页重新打开或保留首页恢复完成时调用；只恢复接收新点击，不恢复进关前或关内丢弃的请求。</summary>
        internal void NotifyHomeOpened()
        {
            if (!Application.isPlaying) return;
            _playingGameType = null;
        }

        private bool IgnoreLinkDuringGameplay(string source)
        {
            if (_playingGameType.HasValue)
            {
                DiscardPendingLink(source);
                return true;
            }
            if (!_activationDiscarded) return false;
            GuruLog.I($"[OneLink] stage=discarded, reason=activation_discarded, source={source}.");
            return true;
        }

        private void DiscardPendingLink(string source)
        {
            CancelNativeDeferredLinks("gameplay_active");
            _pendingIsInstallTarget = false;
            // 只使待跳转任务失效；当前关卡的来源标记和每玩法一次的成功历史由原进关流程管理。
            _activationDiscarded = true;
            _homeCallbackAwaitingUrl = null;
            _pending = null;
            _pendingUrl = null;
            _pendingFromGcd = false;
            _waitReason = null;
            _errorRetry = null;
            CancelInstallRecoveryNetworkWait();
            _automaticUrls.Clear();
            CancelGcdCandidate();
            CancelUrlResolution();
            GuruLog.I($"[OneLink] stage=discarded, reason=gameplay_active, gameType={_playingGameType}, source={source}.");
        }

        #endregion

        #region SDK 接收

        private void BindAppsFlyer()
        {
            if (!Application.isPlaying || _appsFlyerBound)
                return;

            Initialize();
            GuruAppsflyerManager receiver = GuruAppsflyerManager.Instance;
            if (receiver == null || AppsFlyer.instance == null)
            {
                GuruLog.W("[OneLink] stage=bind_failed, reason=appsflyer_not_prepared.");
                return;
            }

            BindUnifiedDeepLinkReceiver(receiver);
            BindConversionDataReceiver(receiver);
            _appsFlyerBound = true;
            GuruLog.I("[OneLink] stage=bound, diagnostics=onelink-v7.");
            StartCachedConversionProbe();
        }

        /// <summary>
        /// 仅由 BindAppsFlyer 在运行态、SDK 接收对象有效时调用；iOS 事件订阅会设置原生 delegate。
        /// 本步骤不调用 initSDK/startSDK，不增加联网等待，保持原订阅时机。
        /// </summary>
        private void BindUnifiedDeepLinkReceiver(GuruAppsflyerManager receiver)
        {
            // 原生回调发送到 Guru 创建的同一个对象；复用插件公开组件接收 onDeepLinking。
            if (receiver.GetComponent<AppsFlyer>() == null)
                receiver.gameObject.AddComponent<AppsFlyer>();
            AppsFlyer.OnDeepLinkReceived += OnDeepLinkReceived;
        }

        /// <summary>仅由 Dispose 退订本服务的 UDL 回调并清除共同绑定标记；不停止或重置原生 SDK。</summary>
        private void UnbindUnifiedDeepLinkReceiver()
        {
            if (_appsFlyerBound)
                AppsFlyer.OnDeepLinkReceived -= OnDeepLinkReceived;
            _appsFlyerBound = false;
        }

        private void OnApplicationLink(string url)
        {
            LogUrl(url, "unity");
            if (!IsRoutingUrl(url))
            {
                GuruLog.I("[OneLink] stage=url_ignored, reason=unrelated_url.");
                return;
            }
            _explicitLinkReceived = true;
            ResetNativeInstallPriority();
            if (_playingGameType.HasValue)
            {
                DiscardPendingLink("unity_url");
                return;
            }
            // UDL 可能先于 Unity URL 事件到达；同一来源只合并一次，避免重放刚受理的请求。
            if (!string.IsNullOrEmpty(url) && string.Equals(url, _sdkUrlBeforeSystem, StringComparison.Ordinal))
            {
                _systemUrl = url;
                _sdkUrlBeforeSystem = null;
                GuruLog.I("[OneLink] stage=url_merged, reason=sdk_already_received.");
                return;
            }

            bool retryReceivedUrl = !_activationAccepted && !_isTestActivation && _errorRetry != null &&
                (!_errorRetry.Stopped || string.IsNullOrWhiteSpace(_errorRetry.Url)) &&
                (!_errorRetry.BudgetStarted || Time.realtimeSinceStartupAsDouble < _errorRetry.Deadline) &&
                ((_activation == 0 && string.IsNullOrWhiteSpace(_errorRetry.Url)) ||
                 string.Equals(url, _systemUrl, StringComparison.Ordinal) ||
                 string.Equals(url, _errorRetry.Url, StringComparison.Ordinal));
            if (retryReceivedUrl)
            {
                // 同一次 ERROR 的 URL 补达／重复通知沿用原十秒预算，不能重置重试次数或覆盖本次归因。
                if (string.IsNullOrWhiteSpace(_errorRetry.Url)) _errorRetry.Stopped = false;
                CancelInstallRecoveryNetworkWait();
                Url = _systemUrl = _errorRetry.Url = url;
                if (_activation == 0) _activation++;
                CancelGcdCandidate();
                if (!string.IsNullOrWhiteSpace(url)) _externalUrlSeen = true;
                GuruLog.I("[OneLink] stage=url_merged, reason=error_retry_pending.");
                ScheduleErrorRetry(url);
                return;
            }

            // 回首页后的新点击可能先发 UDL、后发 Unity URL；必须确认同一 URL 才启用先到参数。
            JObject homeCallback = _homeCallbackAwaitingUrl;
            bool hasMatchingHomeCallback = homeCallback != null &&
                OneLinkRequest.TryParse(homeCallback, out OneLinkRequest homeRequest, out _) &&
                string.Equals(url, homeRequest.Url, StringComparison.Ordinal);
            CancelGcdCandidate();
            if (!string.IsNullOrWhiteSpace(url))
                _externalUrlSeen = true;
            CancelUrlResolution();
            ResetActivationState();
            Url = url ?? string.Empty;
            _activation++;
            _systemUrl = Url;
            _sdkUrlBeforeSystem = null;
            _pending = null;
            _pendingFromGcd = false;
            _pendingUrl = string.IsNullOrWhiteSpace(Url) ? null : Url;
            _waitReason = null;
            GuruLog.I($"[OneLink] stage=url_received, activation={_activation}, hasUrl={Url.Length > 0}.");
            if (hasMatchingHomeCallback)
                AcceptPayload(homeCallback, false);
            // 先等待 SDK 自动 UDL；若到首页空闲时仍只有 URL，再复用已验证的主动解析入口。
        }

        private void OnDeepLinkReceived(object sender, EventArgs args)
        {
            HandleDeepLinkAsync(args as DeepLinkEventsArgs).Forget();
        }

        private async UniTask HandleDeepLinkAsync(DeepLinkEventsArgs args)
        {
            await UniTask.SwitchToMainThread();
            if (!_initialized || !Application.isPlaying || args == null)
                return;

            // AppsFlyer 的 FOUND/NOT_FOUND 回调可能携带默认 UNEXPECTED 枚举；只有 ERROR 才按错误处理。
            GuruLog.I($"[OneLink] stage=callback, status={args.status}, deferred={args.isDeferred()}, error={(args.status == DeepLinkStatus.ERROR ? args.error.ToString() : "none")}.");
            try
            {
                JObject payload = args.deepLink == null ? null : JObject.FromObject(args.deepLink);
                LogPayload(payload, "sdk_callback");
                if (_activationDiscarded && !_playingGameType.HasValue && args.status == DeepLinkStatus.FOUND &&
                    !args.isDeferred() && OneLinkRequest.TryParse(payload, out OneLinkRequest homeRequest, out _) &&
                    IsRoutingUrl(homeRequest.Url))
                {
                    if (!IsOwnedCallbackMarker(GetCallbackMarker(payload)))
                        _homeCallbackAwaitingUrl = payload;
                }
                if (IgnoreLinkDuringGameplay("sdk_callback")) return;
                if (args.isDeferred() && IgnoreAfInstallTarget)
                {
                    GuruLog.I("[OneLink] stage=udl_late_ignored, reason=install_target_selected_or_executed.");
                    return;
                }
                ExpireErrorRetry();
                if (HandleUrlResolutionResult(args, payload))
                    return;
                // GCD 已实际尝试进关后，不让同次安装的晚到 UDL 再跳一次；后续普通直达链接不受影响。
                if (_gcdEntryAttempted && args.isDeferred())
                {
                    GuruLog.I("[OneLink] stage=udl_late_ignored, reason=gcd_entry_attempted.");
                    return;
                }
                bool replaceInstallReward = _installRecoveryAccepted && args.status == DeepLinkStatus.FOUND &&
                    OneLinkRequest.TryParse(payload, out OneLinkRequest installUdlRequest, out _) &&
                    CanUdlReplacePendingGcdReward(installUdlRequest);
                if (_installRecoveryAccepted && !replaceInstallReward)
                {
                    GuruLog.I("[OneLink] stage=callback_ignored, reason=install_already_recovered.");
                    return;
                }
                if (args.isDeferred() && (_explicitLinkReceived || _isTestActivation))
                {
                    GuruLog.I("[OneLink] stage=callback_ignored, reason=newer_link_or_test.");
                    return;
                }
                if (args.status == DeepLinkStatus.ERROR)
                {
                    HandleDeepLinkError(payload);
                    return;
                }
                if (_urlResolution == null && _errorRetry?.Scheduled == true)
                {
                    _errorRetry.Scheduled = false;
                    _errorRetry.Stopped = true;
                    GuruLog.I($"[OneLink] stage=retry_stopped, reason=sdk_{args.status}.");
                }
                bool accepted = args.status == DeepLinkStatus.FOUND &&
                                AcceptPayloadCore(payload, args.isDeferred(), resolvedLinkSource: "udl");
                bool valid = accepted || (args.status == DeepLinkStatus.FOUND &&
                                          OneLinkRequest.TryParse(payload, out _, out _));
                _udlResolved = true;
                if (valid)
                {
                    _udlAccepted = true;
                    CancelGcdCandidate();
                    if (accepted)
                    {
                        _pendingFromGcd = false;
                        _pendingUrl = null;
                        if (!string.IsNullOrEmpty(_pending.Url) &&
                            !string.Equals(_pending.Url, _systemUrl, StringComparison.Ordinal))
                            _sdkUrlBeforeSystem = _pending.Url;
                        if (_urlResolution?.IsAutomatic == true)
                            FinishUrlResolution(_urlResolution, true, "sdk_auto_received", "SDK 已自动返回参数。");
                    }
                    else if (_pendingFromGcd)
                        _pendingFromGcd = false; // 相同目标已排队，按 UDL 优先但不重复提交。
                }
                else
                    TryAcceptGcdCandidate("udl_without_target");
            }
            catch (Exception exception)
            {
                GuruLog.W($"[OneLink] stage=rejected, reason=payload_exception, error={exception.Message}.");
                _udlResolved = true;
                TryAcceptGcdCandidate("udl_payload_exception");
            }
        }

        #endregion

        #region GCD 与 UDL 共用受理

        /// <summary>校验并在内存中排队 SDK 业务参数；不持久化待执行目标，不在此处发奖或进关。</summary>
        internal bool AcceptPayload(JObject payload, bool isDeferred)
            => AcceptPayloadCore(payload, isDeferred, resolvedLinkSource: null);

        // 真实 SDK 接收点标记来源；GM 历史例外仅由代码调用方授予，不能从投放参数取得。
        private bool AcceptPayloadCore(JObject payload, bool isDeferred, string resolvedLinkSource, bool bypassEntryHistory = false,
            bool fromNativeDdl = false)
        {
            bool fromUdl = resolvedLinkSource == "udl";
            if (IgnoreLinkDuringGameplay("payload")) return false;
            if (isDeferred && !fromNativeDdl && IgnoreAfInstallTarget) return false;
            if (!OneLinkRequest.TryParse(payload, out OneLinkRequest request, out string error))
            {
                GuruLog.W($"[OneLink] stage=rejected, reason={error}.");
                return false;
            }
            if (!isDeferred) ResetNativeInstallPriority();
            else if (fromUdl) RememberAfInstallSource(payload);

            // 自动 UDL 与主动解析可能携带不同 link/deferred 字段；同次唤起按实际目标去重。
            string key = request.Action == OneLinkAction.OpenLevel
                ? $"{_activation}:{request.GameType}:{request.LevelPath.ToUpperInvariant()}"
                : $"{_activation}:reward:{request.RewardId}";
            bool replaceGcdReward = fromUdl && CanUdlReplacePendingGcdReward(request);
            bool replaceAfTarget = fromNativeDdl && _pending != null && !_installDdlExecutionStarted;
            if (key == _lastRequestKey && !replaceGcdReward && !replaceAfTarget)
            {
                GuruLog.I("[OneLink] stage=duplicate_ignored.");
                return false;
            }

            _lastRequestKey = key;
            _activationAccepted = true;
            _errorRetry = null;
            CancelInstallRecoveryNetworkWait();
            _automaticUrls.Clear();
            _pendingUrl = null;
            CancelGcdCandidate();
            if (!string.IsNullOrEmpty(request.Url))
                Url = request.Url;
            request.BypassEntryHistory = bypassEntryHistory && request.Action == OneLinkAction.OpenLevel;
            _pending = request;
            _pendingIsInstallTarget = isDeferred;
            _pendingFromGcd = false;
            _waitReason = null;
            if (replaceGcdReward)
            {
                _installRecoveryAccepted = false;
                GuruLog.I($"[OneLink] stage=gcd_reward_replaced, source=udl, action={request.Action}.");
            }
            if (request.Action == OneLinkAction.OpenLevel)
                GuruLog.I($"[OneLink] stage=queued, gameType={request.GameType}, level={(string.IsNullOrEmpty(request.LevelPath) ? "lowest_unpassed" : request.LevelPath)}, deferred={isDeferred}.");
            else
                GuruLog.I($"[OneLink] stage=reward_queued, action={request.Action}, targeted={!string.IsNullOrEmpty(request.TargetUserId)}, deferred={isDeferred}.");
            return true;
        }

        // 普通 GCD 与 iOS 安装恢复的奖励只在尚未消费时可被 UDL 接管；进关和已发奖记录不在此范围。
        private bool CanUdlReplacePendingGcdReward(OneLinkRequest request) =>
            _pending != null && (_pendingFromGcd || _installRecoveryAccepted) &&
            _pending.Action != OneLinkAction.OpenLevel && request.Action != OneLinkAction.OpenLevel;

        #endregion

        #region 首页门禁

        /// <summary>启动弹窗队列最后一步调用；礼包此时可能仍打开，实际关闭由 UI 栈门禁继续确认。</summary>
        internal void NotifyStartupPopupsDispatched()
        {
            // 存档字段按项目生成规则读写；新会话不得继承上次关卡的来源标记。
            if (Application.isPlaying && !_startupPopupsDispatched && _entryGameType == null && DB.GameData != null)
            {
                DB.GameData.IsEnterByOneLink = false;
                DB.GameData.OneinlinkEnterType ??= new List<GameType>();
            }
            _startupPopupsDispatched = true;
            GuruLog.I("[OneLink] stage=startup_popups_dispatched.");
        }

        /// <summary>仅由完成进场动画的 StartPanel.LateUpdate 调用；不在 SDK 回调中重入 UI 栈。</summary>
        internal void TryNavigateFromStartPanel(StartPanel panel)
        {
            if (Application.isPlaying)
            {
                ExpireErrorRetry();
                ExpireFacebookDdlWait();
                ExpireGa4fDdlWait();
            }
            if (!Application.isPlaying || !HasPendingRequest || panel == null || !panel.IsOpen || !panel.IsVisible)
                return;

            string reason = null;
            if (!_startupPopupsDispatched) reason = "startup_sequence";
            else if (SceneManager.GetActiveScene().name != "Main") reason = "loading_scene";
            else if (!Application.isFocused) reason = "application_unfocused";
            else if (UIManager.Instance.HasBlockingPopup) reason = "popup_or_transition";
            else if (GameManager.Instance.ControllerManager?.ActiveController?.Mode != PlayModeType.Main ||
                     !GameManager.Instance.PlayCtrl.CheckState(GamePlayStateName.START)) reason = "home_not_idle";
            else if (_pendingIsInstallTarget && WaitForFacebookInstallTarget) reason = "facebook_ddl";
            else if (_pendingIsInstallTarget && WaitForGa4fInstallTarget) reason = "ga4f_ddl";
            else if (_pending != null && _pending.Action != OneLinkAction.OpenLevel &&
                     !string.IsNullOrEmpty(_pending.TargetUserId) && !GameAccountUtils.HasValidUser)
                reason = "reward_user_not_ready";

            if (reason != null)
            {
                if (_waitReason != reason)
                {
                    _waitReason = reason;
                    GuruLog.I($"[OneLink] stage=waiting, reason={reason}.");
                }
                return;
            }

            if (_pending == null)
            {
                if (_urlResolution == null)
                {
                    if (_errorRetry?.Scheduled == true) TryStartErrorRetry();
                    else if (_pendingUrl != null) ResolvePendingUrl();
                }
                // 即使长链接已同步解析，下帧仍重新经过所有门禁再进关。
                return;
            }

            OneLinkRequest request = _pending;
            MarkInstallTargetExecutionStarted();
            _pending = null;
            bool fromGcd = _pendingFromGcd;
            _pendingFromGcd = false;
            if (fromGcd)
                _gcdEntryAttempted = true;
            _waitReason = null;
            try
            {
                if (request.Action != OneLinkAction.OpenLevel)
                {
                    ExecuteReward(request);
                    return;
                }
                if (OneLinkLevelRouter.TryEnter(request, panel, out string error))
                    GuruLog.I($"[OneLink] stage=entry_accepted, gameType={request.GameType}, level={(string.IsNullOrEmpty(request.LevelPath) ? "lowest_unpassed" : request.LevelPath)}.");
                else
                    GuruLog.W($"[OneLink] stage=entry_rejected, gameType={request.GameType}, reason={error}.");
            }
            catch (Exception exception)
            {
                GuruLog.W($"[OneLink] stage={(request.Action == OneLinkAction.OpenLevel ? "entry_exception" : "reward_exception")}, error={exception.Message}.");
            }
        }

        private void ResolvePendingUrl()
        {
            string url = _pendingUrl;
            _pendingUrl = null;
            _waitReason = null;
            if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out Uri uri))
            {
                GuruLog.W("[OneLink] stage=auto_url_rejected, reason=invalid_http_url.");
                return;
            }

            bool isHttpUrl = uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp;
            bool isDirectUri = IsDirectUri(uri);
            if (!isHttpUrl && !isDirectUri)
            {
                GuruLog.W("[OneLink] stage=auto_url_rejected, reason=unsupported_uri.");
                return;
            }

            if (IsOneLinkShortUrl(uri))
            {
                StartUrlResolution(uri, null, automatic: true);
                return;
            }

            bool parsed = TryParseUrlQuery(uri, out JObject payload, out string error);
            LogPayload(payload, "auto_url_query");
            if (parsed)
            {
                bool queued = AcceptPayloadCore(payload, false, resolvedLinkSource: null, bypassEntryHistory: _gmSystemUrlActivation);
                GuruLog.I($"[OneLink] stage=auto_url_parsed, queued={queued}.");
            }
            else
                GuruLog.W($"[OneLink] stage=auto_url_rejected, reason={error}.");
        }

        #endregion

        #region GuruSDK 生命周期接点

        /// <summary>
        /// 排在 GuruAppsflyer 后的游戏侧准备步骤，保证在 initSDK 后、startSDK 前订阅 UDL。
        /// 仅复用公开生命周期并记录游戏侧 GCD 观测事件，不拦截原统计事件，也不修改 SDK 文件。
        /// </summary>
        internal sealed class AppsFlyerHook : AnalyticsTransmitter
        {
            /// <summary>前一个 AppsFlyer transmitter 完成 Prepare 后接好 UDL，不额外等待网络。</summary>
            public override UniTask Prepare()
            {
                Instance.BindAppsFlyer();
                return UniTask.CompletedTask;
            }

            /// <summary>SDK 初始化后启动仅用于打点的 GCD 等待；不延迟启动，不改五秒 UDL 仲裁。</summary>
            public override async UniTask Initialize()
            {
                await UniTask.SwitchToMainThread();
                Instance.StartGcdAnalyticsTimeout();
            }

            /// <summary>本接点不发送事件，返回空能力集，保留原有统计通道的处理结果。</summary>
            public override AnalyticsCapabilities Transmit(string name, Dictionary<string, object> parameters,
                AnalyticsCapabilities capabilities, AppEventOptions options = null) => AnalyticsCapabilities.EmptyCapabilities;

            /// <summary>本接点不处理用户属性，全部属性仍由现有统计通道发送。</summary>
            public override void TransmitProperty(string key, string value) { }
        }

        #endregion
    }
}
