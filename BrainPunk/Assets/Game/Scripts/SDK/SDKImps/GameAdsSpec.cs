using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Guru.SDK.Framework.Ads.FusionAds;
using Guru.SDK.Framework.Core;
using Guru.SDK.Framework.Core.Spec.Protocols.Ads;
using Guru.UniKit.Platform.Tools;
using UnityEngine;

namespace Game
{
    public class GameAdsSpec : IAdsSpecDelegate
    {
        private static readonly Lazy<GameAdsSpec> LazyInstance =
            new(() => new GameAdsSpec(), LazyThreadSafetyMode.ExecutionAndPublication);

        public static GameAdsSpec Instance => LazyInstance.Value;


        public static class RegionUtils
        {
            // EEA国家代码列表（大写和小写）
            private static readonly HashSet<string> EeaCountryCodes = new()
        {
			// 大写
			"BE", "BG", "CZ", "DK", "CY", "LV", "LT", "LU", "ES", "FR",
            "HR", "IT", "PL", "PT", "RO", "SI", "HU", "MT", "NL", "AT",
            "IS", "LI", "NO", "SK", "FI", "SE", "DE", "EE", "IE", "EL", "GB",
        };


            public static bool IsEea()
            {
                var countryCode = PlatformUtils.CountryCode.ToUpperInvariant();
                return !string.IsNullOrEmpty(countryCode) && EeaCountryCodes.Contains(countryCode);
            }
        }
        
        // [Obsolete("后续需要移除该方法")]
        private void ApplyTransitionalPrivacyPolicy() // <-------新增方法
        {
            try
            {
                if (RegionUtils.IsEea()) return;
                PrivacyManager.Instance.AfterAcceptPrivacy();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[GuruSdk]ApplyTransitionalPrivacyPolicy:" + e);
            }
        }


        public UniTask<GuruAdsSdk> BuildAdsSdk()
        {
            ApplyTransitionalPrivacyPolicy(); // <-------新增

            // var guruAds = MaxGuruAds.Instance;
            return new UniTask<GuruAdsSdk>(new GuruFusionAds());
        }
    }

}