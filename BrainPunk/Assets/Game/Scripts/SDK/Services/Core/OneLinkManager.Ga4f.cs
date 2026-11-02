using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Guru.SDK.Framework.Core.Analytics.Transmitter;
using Guru.SDK.Framework.Utils.Analytics;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Scripting;
using GuruLog = Guru.SDK.Framework.Utils.Log.Log;

namespace Game
{
    /// <summary>Google Ads Android 首开的 GA4F 目标接收；只观察公开结果，不初始化 Firebase 或改写其缓存。</summary>
    public sealed partial class OneLinkManager
    {
        #region GA4F 首开窗口

        private bool _ga4fDdlStarted;
        private bool _ga4fDdlWaiting;
        private bool _ga4fDdlSelected;
        private double _ga4fDdlDeadline;
        private CancellationTokenSource _ga4fDdlWait;
#if UNITY_ANDROID && !UNITY_EDITOR
        private const string Ga4fDdlPreferencesName = "google.analytics.deferred.deeplink.prefs";
        private AndroidJavaObject _ga4fPreferences;
        // Android 对 listener 使用弱引用，游戏侧必须持有它直到注销。
        private Ga4fPreferencesListener _ga4fPreferencesListener;
#endif

        private void BeginGa4fDeferredLink()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Application.isPlaying || !_initialized || _ga4fDdlStarted || IgnoreAfInstallTarget ||
                _explicitLinkReceived || _externalUrlSeen || _isTestActivation ||
                _playingGameType.HasValue || _activationDiscarded || DB.GameData == null ||
                DB.GameData.PlayCount != 1)
                return;

            _ga4fDdlStarted = true;
            _ga4fDdlWaiting = true;
            _ga4fDdlDeadline = GetNativeDdlDeadline();
            _ga4fDdlWait = new CancellationTokenSource();
            GuruLog.I("[OneLink] stage=ga4f_ddl_wait_started, budgetSeconds=5.");
            WaitForGa4fDeferredLinkAsync(_ga4fDdlWait.Token).Forget();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private async UniTask WaitForGa4fDeferredLinkAsync(CancellationToken token)
        {
            try
            {
                while (_ga4fDdlWaiting)
                {
                    token.ThrowIfCancellationRequested();
                    ExpireGa4fDdlWait();
                    if (!_ga4fDdlWaiting) return;
                    if (_ga4fPreferences == null && TryAttachGa4fPreferences())
                        ReadGa4fDeferredLink();
                    if (!_ga4fDdlWaiting) return;
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { FailGa4fDeferredLink(exception); }
        }
#endif

        private void ExpireGa4fDdlWait()
        {
            if (_ga4fDdlWaiting && Time.realtimeSinceStartupAsDouble >= _ga4fDdlDeadline)
                CloseGa4fDdlWait("timeout");
        }

        private void CloseGa4fDdlWait(string reason)
        {
            if (!_ga4fDdlWaiting) return;
            _ga4fDdlWaiting = false;
            var wait = _ga4fDdlWait;
            _ga4fDdlWait = null;
            wait?.Cancel();
            wait?.Dispose();
            GuruLog.I($"[OneLink] stage=ga4f_ddl_wait_finished, reason={reason}.");
        }

        private void CancelGa4fDeferredLink(string reason)
        {
            CloseGa4fDdlWait(reason);
#if UNITY_ANDROID && !UNITY_EDITOR
            // 先清除受理资格，已排到 Unity 主线程的旧回调也不能再读或采用目标。
            var preferences = _ga4fPreferences;
            var listener = _ga4fPreferencesListener;
            _ga4fPreferences = null;
            _ga4fPreferencesListener = null;
            if (preferences == null) return;
            try
            {
                if (listener != null)
                    preferences.Call("unregisterOnSharedPreferenceChangeListener", listener);
            }
            catch (Exception exception)
            {
                GuruLog.W($"[OneLink] stage=ga4f_ddl_cleanup_failed, reason={exception.GetType().Name}.");
            }
            finally { preferences.Dispose(); }
#endif
        }

        #endregion

        #region Android 公开结果桥接

#if UNITY_ANDROID && !UNITY_EDITOR
        private bool TryAttachGa4fPreferences()
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            if (activity == null) return false;
            using var context = activity.Call<AndroidJavaObject>("getApplicationContext");
            if (context == null) return false;
            _ga4fPreferences = context.Call<AndroidJavaObject>("getSharedPreferences", Ga4fDdlPreferencesName, 0);
            if (_ga4fPreferences == null) return false;
            _ga4fPreferencesListener = new Ga4fPreferencesListener(this);
            _ga4fPreferences.Call("registerOnSharedPreferenceChangeListener", _ga4fPreferencesListener);
            GuruLog.I("[OneLink] stage=ga4f_ddl_listening.");
            // 先订阅再补读，覆盖 SDK 在游戏接点之前已经写入的结果，不主动发出归因请求。
            return true;
        }

        private async UniTask OnGa4fPreferencesChangedAsync()
        {
            await UniTask.SwitchToMainThread();
            if (!_initialized || !Application.isPlaying || _ga4fPreferences == null) return;
            try { ReadGa4fDeferredLink(); }
            catch (Exception exception) { FailGa4fDeferredLink(exception); }
        }

        private void ReadGa4fDeferredLink()
        {
            if (_ga4fPreferences == null || IgnoreAfInstallTarget || _explicitLinkReceived ||
                _externalUrlSeen || _isTestActivation || IgnoreLinkDuringGameplay("ga4f_ddl"))
                return;

            // 尚未写键是 pending，不是终态空结果；不能因此在首次读取时放行 AF。
            if (!_ga4fPreferences.Call<bool>("contains", "deeplink")) return;
            string url = _ga4fPreferences.Call<string>("getString", "deeplink", null);
            bool afterDeadline = Time.realtimeSinceStartupAsDouble >= _ga4fDdlDeadline;
            CancelGa4fDeferredLink("result");
            if (!TryParseAdDdlTargetUrl(url, out JObject payload, out _))
            {
                GuruLog.I("[OneLink] stage=ga4f_ddl_no_target, reason=invalid_or_empty_target.");
                return;
            }
            if (!AcceptPayloadCore(payload, true, resolvedLinkSource: null, fromNativeDdl: true)) return;
            _ga4fDdlSelected = true;
            _installRecoveryAccepted = false;
            CancelFacebookDeferredLink("install_target_selected");
            CancelUrlResolution();
            GuruLog.I($"[OneLink] stage=ga4f_ddl_selected, afterDeadline={afterDeadline}.");
        }

        private void FailGa4fDeferredLink(Exception exception)
        {
            CancelGa4fDeferredLink("bridge_exception");
            // JNI 异常可能含 URL、设备或 SDK 载荷，只记录类型。
            GuruLog.W($"[OneLink] stage=ga4f_ddl_failed, reason={exception.GetType().Name}.");
        }

        [Preserve]
        private sealed class Ga4fPreferencesListener : AndroidJavaProxy
        {
            private readonly OneLinkManager _owner;

            /// <summary>保持所属会话的引用；不使用 Instance，避免旧回调进入新会话。</summary>
            internal Ga4fPreferencesListener(OneLinkManager owner)
                : base("android.content.SharedPreferences$OnSharedPreferenceChangeListener") => _owner = owner;

            /// <summary>Java 反射入口；只通知 Unity 主线程，不在 Android UI 线程操作游戏、存档或 UI。</summary>
            [Preserve]
            public void onSharedPreferenceChanged(AndroidJavaObject preferences, string key)
            {
                preferences?.Dispose();
                if (key == "deeplink") _owner.OnGa4fPreferencesChangedAsync().Forget();
            }
        }
#endif

        #endregion

        #region Guru 公开生命周期接点

        /// <summary>现有状态同步之后、AF start 之前建立 Android 窗口；不阻塞后续 SDK 初始化。</summary>
        internal sealed class Ga4fDdlHook : AnalyticsTransmitter
        {
            /// <summary>只监听 GA4F 结果；非 Android Player 不触碰 JNI、不增加等待。</summary>
            public override UniTask Initialize()
            {
                Instance.BeginGa4fDeferredLink();
                return UniTask.CompletedTask;
            }

            /// <summary>不参与统计事件发送，保留已有渠道统计能力。</summary>
            public override AnalyticsCapabilities Transmit(string name, Dictionary<string, object> parameters,
                AnalyticsCapabilities capabilities, AppEventOptions options = null) => AnalyticsCapabilities.EmptyCapabilities;

            /// <summary>不处理用户属性，由既有统计通道发送。</summary>
            public override void TransmitProperty(string key, string value) { }
        }

        #endregion
    }
}
