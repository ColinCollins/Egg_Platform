using System;
using Cysharp.Threading.Tasks;
using Game;
using GameCommon;
using I2.Loc;
using Guru.SDK.Framework.Utils.Ads.Data;
using System.Threading.Tasks;
using Game.Play;

/// <summary>
/// 激励视频播放辅助工具。
/// - 检测激励视频是否就绪；
/// - 使用等待弹窗播放；
/// - 根据结果弹出固定的成功 / 失败提示。
/// </summary>
public static class RewardAdHelper
{
    /// <summary>
    /// 当前正在等待的激励视频加载 Task 源（如果有）。
    /// 只通过 <see cref="CancelCurrent"/> 对外暴露取消能力。
    /// </summary>
    private static UniTaskCompletionSource<AdCause>? _currentRewardAdCompletionSource;

    /// <summary>
    /// 手动取消当前激励视频加载/等待（如果存在）。
    /// 会让等待对话框结束并返回 <see cref="AdCause.Canceled"/>。
    /// </summary>
    public static void CancelCurrent()
    {
        _currentRewardAdCompletionSource?.TrySetResult(AdCause.Canceled);
    }

    /// <summary>
    /// 尝试播放激励视频。
    /// </summary>
    /// <param name="placement">广告位标识。</param>
    /// <param name="onResult">回调：placement, 是否成功获取奖励。</param>
    public static void TryToShowRewardAd(string placement, string success = "U_HurryTips_RewardVideo_succeed", string failed = "U_HurryTips_GetAnswer_failed", Action<string, bool>? onResult = null)
    {
        var sdk = GameSDKService.Instance;
        IPlayController activeController =
            GameManager.Instance?.ControllerManager?.ActiveController;
        IInterstitialAdPolicy policy =
            (activeController as IInterstitialAdPolicyHost)?.InterstitialAdPolicy;

        bool callbackHandled = false;
        void InternalCallback(string p, bool isSuc)
        {
            if (callbackHandled)
                return;

            callbackHandled = true;
            if (isSuc)
            {
                SystemTips.Show(LocalizationManager.GetTranslation(success));
                policy?.OnRewardAdShown();
                // GameSDKService.Instance.LoadRewardAd();
            }
            else
            {
                SystemTips.Show(LocalizationManager.GetTranslation(failed));
            }

            onResult?.Invoke(p, isSuc);
        }

        sdk.ShowRewardAd(
            placement,
            InternalCallback,
            loadingDialog: completionSource =>
            {
                // 记录当前这一次的等待 Task 源，供 CancelCurrent 使用
                _currentRewardAdCompletionSource = completionSource;

                // 当 WaitingPopup 被任何方式关闭（超时、外部关闭等）时，同步触发一次取消
                // 这样可以确保 completionSource 结束，避免悬挂。
                WaitingPopup.SetOnCancel(CancelCurrent);

                return WaitingPopup.WaitingAdLoad(completionSource);
            });
    }
}

