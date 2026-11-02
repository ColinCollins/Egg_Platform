using System;
using Bear.Logger;
using Config.Game;
using Game;
using Game.Play;

/// <summary>
/// 插屏广告播放辅助工具。
/// - 根据 InterstitialAdPolicy 判断是否可以展示；
/// - 封装 GameSDKService 的展示逻辑；
/// - 成功播放后只更新发起玩法自己的策略状态。
/// </summary>
public static class InterstitialAdHelper
{
    public static int TriggerCount = 0;
    /// <summary>
    /// 主线最近一次成功插屏是否尚未消费免广告弹窗提示。
    /// 注意事项：Draw 插屏不会修改该主线标记。
    /// </summary>
    public static bool HasInsterstitial = false;

    /// <summary>
    /// DrawGame 最近一次成功插屏是否尚未消费免广告弹窗提示。
    /// 注意事项：仅由 DrawPlayCtrl 消费，不得与主线 HasInsterstitial 混用。
    /// </summary>
    public static bool HasDrawInterstitial = false;

    /// <summary>
    /// IWannerGame 最近一次成功插屏是否尚未消费免广告弹窗提示。
    /// 注意事项：仅由 IWannerPlayCtrl 消费，不得与主线或 Draw 标记混用。
    /// </summary>
    public static bool HasIWannerInterstitial = false;

    /// <summary>仅由 Dig 消费的成功插屏免广告提示标记。</summary>
    public static bool HasDigInterstitial = false;

    private static void Log(string message)
    {
        BearLogger.Log($"[InterstitialAdHelper] {message}", typeof(InterstitialAdHelper));
    }


    /// <summary>
    /// 尝试播放插屏广告（根据当前关卡和策略判断）。
    /// </summary>
    /// <param name="placement">广告位标识。</param>
    /// <param name="onResult">回调：placement, 是否成功展示。</param>
    public static void TryToShowInterstitial(string placement, Action<string, bool> onResult = null)
    {
        var sdk = GameSDKService.Instance;
        IPlayController activeController =
            GameManager.Instance?.ControllerManager?.ActiveController;
        var policyHost = activeController as IInterstitialAdPolicyHost;

        if (sdk == null || policyHost == null)
        {
            Log(
                $"sdk or active policy host is null. sdkNull:{sdk == null}, " +
                $"hostNull:{policyHost == null}, placement:{placement}");
            onResult?.Invoke(placement, false);
            return;
        }

        IInterstitialAdPolicy policy = policyHost.InterstitialAdPolicy;
        if (policy == null)
        {
            Log(
                $"InterstitialAdPolicy is null. mode:{activeController.Mode}, " +
                $"placement:{placement}");
            onResult?.Invoke(placement, false);
            return;
        }

        int currentLevel = policyHost.InterstitialLevel;
        if (!Enum.TryParse(placement, out InterstitialPlacement placementType))
        {
            Log($"Invalid placement string: {placement}");
            onResult?.Invoke(placement, false);
            return;
        }

        if (!policy.CanShowInterstitial(currentLevel, placementType))
        {
            Log(
                $"CanShowInterstitial return false. mode:{activeController.Mode}, " +
                $"level:{currentLevel}, placement:{placementType}");
            onResult?.Invoke(placement, false);
            return;
        }

        PlayModeType requestMode = activeController.Mode;
        Log(
            $"Try show interstitial. mode:{requestMode}, " +
            $"level:{currentLevel}, placement:{placement}");

        bool callbackHandled = false;
        void InternalCallback(string p, bool success)
        {
            if (callbackHandled)
            {
                Log(
                    $"Duplicate interstitial callback ignored. mode:{requestMode}, " +
                    $"placement:{p}, success:{success}");
                return;
            }

            callbackHandled = true;
            Log(
                $"Interstitial result. mode:{requestMode}, " +
                $"placement:{p}, success:{success}");

            if (success)
            {
                TriggerCount = 0;
                policy.OnInterstitialShown(placementType);
                if (requestMode == PlayModeType.Main)
                    HasInsterstitial = true;
                else if (requestMode == PlayModeType.Draw)
                    HasDrawInterstitial = true;
                else if (requestMode == PlayModeType.IWanner)
                    HasIWannerInterstitial = true;
                else if (requestMode == PlayModeType.Dig)
                    HasDigInterstitial = true;
            }

            onResult?.Invoke(p, success);
        }

        TriggerCount++;
        sdk.ShowInterstitial(placement, InternalCallback);
    }
}

