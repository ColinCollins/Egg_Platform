using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Guru.SDK.Framework.Core;
using Guru.SDK.Framework.Core.Ads;
using Guru.SDK.Framework.Core.Ads.Core;
using Guru.SDK.Framework.Utils.Ads;
using Guru.SDK.Framework.Utils.Ads.Data;
using Guru.SDK.Framework.Utils.Controller.Ads;
using Guru.SDK.Framework.Utils.Extensions;
using Guru.SDK.Unikit.Logger;
using R3;

namespace Game
{

    public class GameAdsController: AdsController
    {
        /// <summary>
        /// 所属场景
        /// </summary>
        protected override string Scene => "GameView";
        private const string SceneTag = "GameView";
        
        
        /// <summary>
        /// 重载可用的广告类型
        /// </summary>
        protected override List<AdType> SupportedAdTypes => new List<AdType>()
            {
                AdType.Interstitial, // 插屏
                AdType.Rewarded, // 激励视频
                // AdType.Banner // Banner
            };
        
        
        // 项目配置的 广告 UnitId
        public string BannerId { get; private set; }

        // private bool _shouldHide = true;
        private bool _isShowing;
        private bool _isAdsInitialized;
        
        // 构造函数
        public GameAdsController() :
            base(BannerObserver, InterstitialObserver, RewardedObserver) // 这里向父类构造函数直接传入对应的广告生命周期观察者
        {
            //这里需要添加一个 AdsManger 初始化完成的监听
            AddSubscription(AdsManager.ObservableAdsManagerReady.Subscribe(ProcessAdsManagerReady));
        }
        /// <summary>
        /// 处理 AdsManager 就绪状态
        /// </summary>
        /// <param name="isReady"></param>
        private void ProcessAdsManagerReady(bool isReady)
        {
            if (!isReady) return;
            BindInterstitialAd(); // 初始化插屏
            BindRewardedAd(); // 初始化激励视频
            
            BannerId = GuruSdk.Instance.AdsProfile?.BannerId.Id ?? ""; // 这里必须要返回 BannerId
            // 可在此处扩展一些项目组的策略
            if (!string.IsNullOrEmpty(BannerId))
            {
                // InitializeBanner(); // 初始化 Banner 广告
            }
            else
            {
                Log.E($"[Ads] BannerId is Null, no banner initialized!!");
            }

            // 添加广告模块初始化监听
            AddSubscription(AdsManager.Instance.ObservableInitialized.Subscribe(initialized =>
            {
                if (initialized)
                {
                    // 监听广告初始化结束， 处理其他逻辑
                    // 预加载插屏广告
                    RequestLoadInterstitial();
                    // 预加载激励视频广告
                    RequestLoadRewardedAd();
                }
            }));
        }
        public bool IsAdsInitialized
        {
            get
            {
                try
                {
                    return AdsManagerDelegate.DelegateInstance.IsInitialized;
                }
                catch (Exception ex)
                {
                    Log.E(ex.Message);
                    return false;
                }
            }
        }
        
        // 请求 插屏广告
        public void RequestLoadInterstitial() => CheckAndLoadInterstitialAd().Forget();
        // 请求 激励视频
        public void RequestLoadRewardedAd() => CheckAndLoadRewardedAd().Forget();
        
        // 显示 Banner 
        public void RequestShowBanner()
        {
            if (_isShowing == false)
            {
                BannerProcessResumed();
                _isShowing = true;
            }
        }

        // 隐藏 Banner
        public void RequestHideBanner()
        {
            _isShowing = false;
            BannerProcessPaused();
        }

        // Banner 观察者：
        private static readonly AdsLifecycleObserver BannerObserver = new AdsLifecycleObserverDelegate(
            name: SceneTag + "BannerObserver",
            onRequestLoadCallback: bundle =>
            {
                // On Banner Load
            },
            onAdLoadedCallback: bundle =>
            {
                // On Banner Loaded 
            });


        // 插屏广告 观察者：
        private static readonly AdsLifecycleObserver InterstitialObserver = new AdsLifecycleObserverDelegate(
            name: SceneTag + "InterstitialObserver",
            onRequestLoadCallback: bundle =>
            {
                // On IV Load
            },
            onAdLoadedCallback: bundle =>
            {
                // On IV Loaded
            },
            onAdLoadFailedCallback: bundle =>
            {
                // On IV Load Failed
            },
            onAdDisplayFailedCallback: bundle =>
            {
                // On IV Display Failed
            },
            onAdHiddenCallback: bundle =>
            {
                // On IV Closed 
            });


        // 激励视频 观察者：
        private static readonly AdsLifecycleObserver RewardedObserver = new AdsLifecycleObserverDelegate(
            name: SceneTag + "RewardedObserver",
            onRequestLoadCallback: bundle =>
            {
                // On RV Load
            },
            onAdLoadedCallback: bundle =>
            {
                // On RV Load Failed 
            },
            onAdLoadFailedCallback: bundle =>
            {
                // On RV Load Failed    
            },
            onAdDisplayFailedCallback: bundle =>
            {
                // On RV Display Failed
            },
            onAdDisplayedCallback: bundle =>
            {
                RecordRewardAdDisplayed();
            },
            onAdRewardedCallback: bundle =>
            {
                // On RV Get Rewarded
            },
            onAdHiddenCallback: bundle =>
            {
                // On RV Closed 
            });

        /// <summary>
        /// 记录一次实际开始展示的激励视频，并立即保存全局 GameData 计数。
        /// </summary>
        private static void RecordRewardAdDisplayed()
        {
            var gameData = DB.GameData;
            if (gameData == null)
                return;

            gameData.RewardADCount++;
            if (!DB.SaveGameData())
                Log.E($"[Ads] Failed to save RewardADCount. count:{gameData.RewardADCount}");
        }
        
        

    }
}
