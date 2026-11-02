#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Guru.SDK.Framework.Core.Ads;
using Guru.SDK.Framework.Core.Ads.Core;
using Guru.SDK.Framework.Utils.Ads;
using Guru.SDK.Framework.Utils.Ads.Data;
using Guru.SDK.Framework.Utils.Controller.Ads;
using Guru.SDK.Framework.Utils.Extensions;
using Guru.SDK.Framework.Utils.Network;
using Guru.SDK.Unikit.Logger;
using R3;

namespace Game
{
    internal class GameAdsManager
    {
        private readonly GameAdsController _adsController;
        private const string TAG = "GameAdsManager";
        
        internal GameAdsManager()
        {
            _adsController ??= new GameAdsController();
        }
        
        public void Prepare()
        {
            
        }

        public void ShowBanner(string scene = "")
        {
            if (!IsAdsInitialized)
            {
                Log.D($"IsAdsInitialized false!", TAG);
                return;
            }
            _adsController?.RequestShowBanner();
        }


        public void HideBanner()
        {
            if (!IsAdsInitialized)
            {
                Log.D($"IsAdsInitialized false!", TAG);
                return;
            }
            _adsController?.RequestHideBanner();
        }

        public bool BannerIsShown => _adsController?.BannerIsShown ?? false;

        public bool IsAdsReady => IsAdsInitialized;
        public bool IsInterstitialADReady => _adsController?.IsLoadedInterstitialAds ?? false;
        public bool IsRewardAdReady => _adsController?.IsLoadedRewardedAds ?? false;
        public bool IsAdsInitialized => _adsController?.IsAdsInitialized ?? false;
        
        
        public bool CheckAndReloadRewardAdReady()
        {
            if (!NetworkUtils.IsNetworkConnected())
            {
                return false;
            }
            
            if (!IsAdsInitialized)
            {
                Log.D($"IsAdsInitialized false!", TAG);
                return false;
            }
            
            if (!IsRewardAdReady)
            {
                // LoadRewardedAd();
            }
            return IsRewardAdReady;
        }
        
        public bool CheckAndReloadInterstitialAd()
        {
            if (!NetworkUtils.IsNetworkConnected())
            {
                return false;
            }
            
            if (!IsAdsInitialized)
            {
                Log.D($"IsAdsInitialized false!", TAG);
                return false;
            }
            
            if (!IsInterstitialADReady)
            {
                // LoadInterstitialAd();
            }
            return IsInterstitialADReady;
        }
        
        public void LoadInterstitialAd()
        {
            _adsController?.RequestLoadInterstitial();
        }

        /// <summary>
        /// 显示插屏广告
        /// </summary>
        /// <param name="placement">广告场景</param>
        /// <param name="onResultFunc">广告关闭事件</param>
        /// <param name="validator">项目组调用条件</param>
        public void ShowInterstitialAd(string placement, Action<string, bool>? onResultFunc = null, AdsValidator? validator = null)
        {
            Log.I("ShowInterstitialAd", TAG);
            if (!IsAdsInitialized)
            {
                Log.I($"ShowInterstitialAd failed IsAdsInitialized false!", TAG);
                onResultFunc?.Invoke(placement, false);
                return;
            }
            
            async UniTask OnResult(AdsResult result)
            {
                // 原生 SDK 的结果可能从工作线程返回；先切回 Unity 主线程，再处理存档、策略和 UI 回调。
                await UniTask.SwitchToMainThread();

                if (result.Cause.IsSuccess())
                {
                    Log.I($"ShowInterstitialAd success !", TAG);
                    onResultFunc?.Invoke(placement, true);
                }
                else
                {
                    Log.I($"ShowInterstitialAd failed !", TAG);
                    onResultFunc?.Invoke(placement, false);
                }
            }
            
            _adsController?.ShowInterstitialAd(placement, validator: validator).ContinueWith(OnResult).Forget();
        }

        public void LoadRewardedAd()
        {
            _adsController?.RequestLoadRewardedAd();
        }

        public void ShowRewardAd(string placement = "", Action<string, bool>? onResult = null,
            Func<UniTaskCompletionSource<AdCause>, UniTask<AdCause>>? loadingDialog = null)
        {
            Log.I("ShowRewardAd", TAG);
            
            if (!IsAdsInitialized && loadingDialog == null)
            {
                Log.I($"ShowRewardAd failed IsAdsInitialized false !", TAG);
                onResult?.Invoke(placement, false);
                return;
            }
            
            _adsController?.ShowRewardedAd(placement, loadingDialog: loadingDialog).ContinueWith(result =>
            {
                if (result.Cause.IsSuccess())
                {
                    // 获取到广告奖励
                    Log.I("ShowRewardAd success", TAG);
                    onResult?.Invoke(placement, true);
                }
                else
                {
                    // 没有获取到广告奖励
                    Log.I($"ShowRewardAd failed： {result.Cause.ToString()}", TAG);
                    onResult?.Invoke(placement, false);
                }
            });
        }

        // 显示 Max Debugger
        public void ShowMaxDebugger()
        {
            AdsManager.Instance.OpenDebugger().Forget();
        }
        
        
    }







}
