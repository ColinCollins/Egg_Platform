using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game
{
    /// <summary>各广告渠道原生来源与 AF 共用的首开预算和一次性消费保护；不定义跨渠道优先级。</summary>
    public sealed partial class OneLinkManager
    {
        #region 安装目标的公共状态

        private const double NativeDdlBudgetSeconds = 5;
        private bool _nativeDdlWindowStarted;
        private double _nativeDdlDeadline;
        private bool _installDdlExecutionStarted;
        private bool _pendingIsInstallTarget;
        private string _afInstallMediaSource;

        // 只锁住本次运行的安装目标；不写存档，新明确链接／GM 仍走原来的直接链接流程。
        private bool IgnoreAfInstallTarget => _facebookDdlSelected || _ga4fDdlSelected || _installDdlExecutionStarted;
        private bool IsGoogleAfInstall => string.Equals(_afInstallMediaSource, "googleadwords_int", StringComparison.Ordinal);
        private bool WaitForFacebookInstallTarget => _facebookDdlWaiting && !_ga4fDdlSelected && !IsGoogleAfInstall;
        private bool WaitForGa4fInstallTarget => _ga4fDdlWaiting && !_facebookDdlSelected &&
                                               (string.IsNullOrEmpty(_afInstallMediaSource) || IsGoogleAfInstall);

        private double GetNativeDdlDeadline()
        {
            if (!_nativeDdlWindowStarted)
            {
                _nativeDdlWindowStarted = true;
                _nativeDdlDeadline = Time.realtimeSinceStartupAsDouble + NativeDdlBudgetSeconds;
            }
            return _nativeDdlDeadline;
        }

        // 仅由真实 SDK 的首装 GCD／deferred UDL 接收点调用；不从 URL 或 campaign 猜渠道。
        private void RememberAfInstallSource(JObject payload)
        {
            if (payload?["media_source"]?.Type != JTokenType.String) return;
            string source = ((string)payload["media_source"])?.Trim();
            if (!string.IsNullOrEmpty(source) && !string.Equals(source, "None", StringComparison.OrdinalIgnoreCase))
                _afInstallMediaSource = source;
        }

        #endregion

        #region 清理与消费锁定

        private void CancelNativeDeferredLinks(string reason)
        {
            CancelFacebookDeferredLink(reason);
            CancelGa4fDeferredLink(reason);
        }

        private void ResetNativeInstallPriority()
        {
            CancelNativeDeferredLinks("new_explicit_target");
            _facebookDdlSelected = false;
            _ga4fDdlSelected = false;
            // 已执行锁只随进程／会话实例重建清除，普通新链接不能让旧安装回调重新发奖。
            _pendingIsInstallTarget = false;
            _afInstallMediaSource = null;
        }

        private void MarkInstallTargetExecutionStarted()
        {
            if (_pendingIsInstallTarget)
            {
                // 在路由／奖励调用前占用；执行拒绝或同步重入也不能让另一来源再消费同次安装。
                _installDdlExecutionStarted = true;
                CancelNativeDeferredLinks("target_execution_started");
            }
            _pendingIsInstallTarget = false;
        }

        #endregion
    }
}
