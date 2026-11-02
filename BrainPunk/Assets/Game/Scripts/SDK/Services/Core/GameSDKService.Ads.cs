#nullable enable
using System;
using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using Guru.SDK.Framework.Utils.Ads.Data;
using Guru.SDK.Unikit.Logger;
using Game;
using Game.Events;
using Guru.UniKit.Platform.Tools;
using UnityEngine;

/// <summary>
/// 广告初始化
/// </summary>
public partial class GameSDKService
{
    
    private const string AdsSceneGameStart= "game_start";
    
    // 默认的 Banner 高度
    private const float PhoneBannerHeightDp = 50f;
    private const float TabletBannerHeightDp = 90f;
    private const float AndroidDpBaseDpi = 160f;
    
    //---------------- 外部暴露接口 ----------------
    
    // 显示 Banner
    public void ShowBanner(string placement = "")
    {
        _adsManager.ShowBanner(placement);
        // SendShowBannerEvent(true);
    }

    // 隐藏 Banner
    public void HideBanner()
    {
        _adsManager.HideBanner();
        // SendShowBannerEvent(false);
    }

    // Banner 是否显示中
    public bool BannerIsShown => IsInitialized && _adsManager.BannerIsShown;

    // 广告是否就绪
    public bool IsAdsReady => IsInitialized && _adsManager.IsAdsReady;

    // IV 是否就绪
    public bool IsInterstitialAdReady => IsInitialized && (_adsManager?.IsInterstitialADReady ?? false);

    // 加载 IV
    public void LoadInterstitialAd() => _adsManager?.LoadInterstitialAd();

    // 显示 IV
    public void ShowInterstitial(string placement, Action<string, bool>? onResultFunc = null, AdsValidator? validator = null)
    {
        _adsManager?.ShowInterstitialAd(placement, onResultFunc, validator);
    }
    
    // RV 是否就绪
    public bool IsRewardAdReady => IsInitialized && (_adsManager?.IsRewardAdReady ?? false);

    // 加载 RV
    public void LoadRewardAd() => _adsManager.LoadRewardedAd();

    // 显示 RV
    public void ShowRewardAd(string placement = "", Action<string, bool> onResult = null,
        Func<UniTaskCompletionSource<AdCause>, UniTask<AdCause>>? loadingDialog = null)
    {
        _adsManager.ShowRewardAd(placement, onResult, loadingDialog: loadingDialog);
    }

    // 显示 MAX 广告测试界面
    public void ShowMaxDebugger() => _adsManager.ShowMaxDebugger();

    #region Banner 高度计算

    /*
     * 详细的计算规范可参考: https://docs.google.com/document/d/1290iHq17C1G9HOqz51sKj1AoEEEfdB3UzB-RZSXB7PI/edit?tab=t.0
     */
    
    // 获取 Banner 高度 Dp
    public float GetBannerHeightDp()
    {
        return IsTablet ? TabletBannerHeightDp : PhoneBannerHeightDp;
    }
    
    public float GetBannerHeightPx()
    {
        var  bannerHeightPx = GetBannerHeightDp() * GetScreenDensity() + Screen.safeArea.y; // 算上安全区的大小
        return bannerHeightPx;
    }
    
    // 获取屏幕密度
    private float GetScreenDensity()
    {
        float maxDensity = 1f;
#if GURU_MAX
        maxDensity = MaxSdkUtils.GetScreenDensity();
#endif
        // Max 在 Editor 下通常返回 1，这里优先使用 DPI 计算的 density 作为兜底
        if (maxDensity > 1f)
        {
            return maxDensity;
        }

        var dpi = Screen.dpi;
        if (dpi > 0f)
        {
            return Mathf.Max(1f, dpi / AndroidDpBaseDpi);
        }

        return maxDensity;
    }

    #endregion
    
    



    // 设备是否是平板
    public bool IsTablet => PlatformUtils.IsTablet;

}
