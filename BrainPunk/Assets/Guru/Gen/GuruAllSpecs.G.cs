using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Guru.SDK.Framework.Core;
using Guru.SDK.Framework.Core.Spec;
using Guru.SDK.Framework.Core.Ads.Model;
using Guru.SDK.Framework.Core.Analytics.Model;
using Guru.SDK.Framework.Core.Financial.Manifest;
using Guru.SDK.Framework.Core.Financial.Product;
using Guru.SDK.Framework.Core.Firebase.RemoteConfig;
using Guru.SDK.Framework.Utils.Auth;
using Guru.SDK.Framework.Utils.Ads.Data;
using Guru.SDK.Framework.Utils.Product;
using Guru.SDK.Framework.GameService.Core;
using Guru.SDK.Framework.GameService.Core.Achievement;


internal class BrainPunkAppSpec: AppSpec
{
    private static BrainPunkAppSpec _instance;
    public static BrainPunkAppSpec Instance
    {
        get
        {
            if (_instance == null) _instance = new BrainPunkAppSpec();
            return _instance;
        }
    }
    
    // 生成 [BrainPunkAppSpec] 配置定义，预填充所有配置属性值
    public BrainPunkAppSpec(): 
        base(
        appName: "BrainPunk",
        flavor: "main",
        appDetails: new AppDetails (
			saasAppId: "brainpunk",
			authority: "brain.gurugame.ai",
			storagePrefix: "https://firebasestorage.googleapis.com/v0/b/brainpunk-52a55.firebasestorage.app/o/",
			defaultCdnPrefix: "https://cdn3-brainpunk.fungame.cloud",
			androidGooglePlayUrl: "https://play.google.com/store/apps/details?id=com.brain.tricky.puzzle.games.test.quest",
			iosAppStoreUrl: "https://apps.apple.com/us/app/id6757749388",
			policyUrl: "https://brain.gurugame.ai/policy.html",
			termsUrl: "https://brain.gurugame.ai/termsofservice.html",
			emailUrl: "brain@gurugame.ai",
			packageName: "com.brain.tricky.puzzle.games.test.quest",
			bundleId: "com.brain.tricky.puzzle.games.test.quest",
			facebookAppId: "25486648387670947",
			gameServiceClientId: "586245451016-mi9bei7sqidh7d4kt97cgoni7kh2q2dj.apps.googleusercontent.com"
        ), 
        deployment: new Deployment (
			propertyCacheSize: 256,
			logFileSizeLimit: 10485760,
			logFileCount: 7,
			persistentLogLevel: 1,
			conversionEvents: new HashSet<string> {
				"level_up",
				"level_up_1",
				"tch_ad_rev_roas_001"
			},
			apiConnectTimeout: 15000,
			apiReceiveTimeout: 15000,
			fullscreenAdsMinInterval: 60,
			purchaseEventTrigger: 1,
			bindGameCredentialStrategy: BindCredentialStrategy.IgnoreOnConflict,
			disableUnsoldManifestDeliverInventory: true,
			autoRequestNotificationPermission: true,
			shouldPreloadInterstitialByChecker: false,
			enabledCustomConsents: false,
			adjustEnableAdService: true
        ), 
        adsProfile: new AdsProfile (
			MaxSdkKey: new MaxSdkKey("V5I0i-vOTXkc_HEyqLg0lNm_ivlzp1wPVF3Vs7Jk4ix6WMAwEKGHMpugavZp_7xgss186Frvss23NGSWZXSago", "V5I0i-vOTXkc_HEyqLg0lNm_ivlzp1wPVF3Vs7Jk4ix6WMAwEKGHMpugavZp_7xgss186Frvss23NGSWZXSago"),
			BannerId: new AdUnitId("aab674f7fdecb215", "8c622fd013e9c761"),
			InterstitialId: new AdUnitId("e670774b655e9684", "030626c2321628f7"),
			RewardsId: new AdUnitId("85b76bf4b2bc5630", "bfb041251fb9203e"),
			MrecId: new AdUnitId("", ""),
			AmazonAppId: new AdAppId("fc8f78b2-ac6d-4d25-afd5-943f3f854d0d", "8aa939c5-662e-4a3f-aad7-13336b22ce28"),
			AmazonBannerSlotId: new AdSlotId("9e81ce69-d2ba-4ffe-b85a-e4eb6f84d4f6", "5b58f56f-cc6e-4e98-b10a-d5a9417ec360"),
			AmazonInterstitialSlotId: new AdSlotId("72f1809e-4f12-49d2-9ff5-c93eabce2835", "a1e0afe7-309b-427d-98cf-ce4f1663fb12"),
			AmazonRewardedSlotId: new AdSlotId("dd3ea773-0ac5-4bd2-9ce8-593a6e80504f", "1014df07-aec5-40e7-885d-86007762f41e"),
			IronsourceAppId: new AdAppId("2673930c5", "26738f73d"),
			TradPlusCreativeKey: new AdAppId("", ""),
			MaxDisabledB2BAdUnitIds: new MaxDisabledB2BAdUnitIds(
				ios: new List<string>() {
					"229d542e8d6da2f8",
					"b8c6d008b8a383fe",
					"9bac5f1974214d7c",
					"a9514c8603987aec",
					"1f559540fd678129",
					"c8d105fa0ddc5e05",
					"0eec013f444133d4",
					"1a021a26cc1b88af",
					"bc361c89613ae451",
					"48e60eebeec28788",
					"27a452941c303990",
					"605e801e59ff7187",
					"e926a3c3d7ea622e",
					"34a6612d875b441a",
				},
				android: new List<string>() {
					"a51dc2415c0cd732",
					"8a145614d13cb8dd",
					"d173d1593aaed580",
					"81ba833ae9aaef1d",
					"5e24bdb78217c87b",
					"2a3fa46fa7778b21",
					"0c7acead9470aeab",
					"74e4d647f286a10b",
					"df78b77172a654fc",
					"7081c702b35d9be7",
					"7fad21f5b6e771e6",
					"2e42874370679d39",
					"01648c743a637c46",
					"efde89356cdc7b70",
				})          
        ), 
        productProfile: new ProductProfile (
			oneOffChargeIapIds: BrainPunkProducts.oneOffChargeIapIds,
			subscriptionsIapIds: BrainPunkProducts.subscriptionsIapIds,
			igcIds: BrainPunkProducts.igcIds,
			rewardIds: BrainPunkProducts.rewardIds,
			noAdsCapIds: BrainPunkProducts.noAdsCapIds,
			groupMap: BrainPunkProducts.groupMap,
			manifestBuilders: BrainPunkProducts.manifestBuilders
        ), 
        remoteConfigSpec: new RemoteConfigSpec (
			defaultValues: new Dictionary<string, object>() {
				["analytics_config"] = "{\"cap\":[\"firebase\",\"facebook\",\"guru\"], \"init_delay_s\": 10}",
				["app_rater"] = "{\"entry_enable\": false,\"gap_days\":0,\"last_pop_gap_days\":7,\"validation\":\"2\"}",
				["bads_config"] = "{\"free_s\":0,\"win_count\":1,\"amazon_enable\":false}",
				["iads_config"] = "{\"free_s\":0,\"validation\":\"3\",\"scene\":\"game_start|game_continue|p_continue|p_main|game_win|star_complete\",\"retry_min_s\":10,\"retry_max_s\":600,\"amazon_enable\":true,\"imp_gap_s\":10,\"sp_scene\":\"game_start:60\",\"ignore_scene_check\":true}",
				["rads_config"] = "{\"free_s\":0,\"validation\":\"1\",\"amazon_enable\":true}",
				["level_config"] = "{ \"enabled\":false, \"showRatingLevel\":12, \"ad_interstitialCD\":30, \"ad_rewardResetInterstitialCD\":30, \"ad_interstitialShowMinLevel\":4, \"ad_interstitialShowIntervalCount\":2, \"ad_interstitialShowFailedCount\":12, \"mainGame_interstitalAdLevelRange\":[4,10,999], \"mainGame_interstitalAdRangeLevelCount\":[4,2,1], \"mianGame_interstititalAdRangeFailedCount\":[-1,12,6], \"level_sort\":[10001,10002,10003,10011,10005,10089,10004,10010,10143,10036,10088,10007,10076,10029,10006,10077,10039,10019,10015,10014,10009,10052,10042,10008,10091,10092,10031,10012,10017,10021,10013,10097,10018,10082,10067,10020,10016,10027,10022,10024,10023,10026,10075,10028,10025,10030,10090,10032,10033,10034,10035,10037,10095,10040,10080,10041,10038,10043,10046,10051,10045,10044,10047,10049,10054,10053,10048,10050,10058,10055,10079,10057,10056,10059,10060,10061,10062,10063,10064,10065,10066,10068,10074,10084,10085,10086,10087,10069,10072,10070,10073,10093,10078,10083,10105,10071,10094,10096,10098,10099,10103,10109,10115,10116,10104,10123,10124,10117,10101,10106,10126,10128,10119,10100,10081,10102,10125,10107,10110,10108,10133,10111,10112,10113,10127,10118,10120,10121,10122,10136,10130,10131,10132,10134,10129,10140,10142,10138,10144,10145,10146,10147,10137,10139,10141,10149,10150,10135,10151], \"open_gaps\":true, \"gameTipsPopupCoinCost\":60, \"ad_dailyNOAdPopupAutoShowCount\":5, \"shop_adfreeCoinAutoShowCount\":99, \"gameTipsPopupPassLevelCoinCost\":40, \"showTipsFailCount\":2, \"showTipsTimeLimit\":10, \"victoryHorizantalRewards\":[30,40,50,90], \"initGameCoinReward\":0, \"currentRemoteConfigVersion\":\"1.16.0\", \"showVersionBoard\":true, \"getFreeSkinLevel\":6, \"freeSkinId\":\"203004\", \"isOpenPostcard\":true, \"postcardGuideLevel\":8, \"gameTipsGuideLevel\":3, \"gameTipsGuideFailedCount\":3, \"gameTipsGuideWaitingTime\":30, \"dailyChallengeStep1CoinReward\":40, \"dailyChallengeSteps\":[4,10], \"dailyChallengeUnlockLevel\":10, \"dailyChallengeIsOpen\":true, \"dailyChallengeStep2CoinReward\":60, \"dailyChallengeRandomSkinPoolId\":1, \"dailyChallengeRewardMultiple\":2, \"dailyFreeCoinCount\":5, \"isOpenDailyChest\":true, \"autoShowChestGiftLevel\":7}",
				["drawgame_config"] = "{ \"enabled\":false, \"drawGameIsOpen\":true, \"drawGameShowTipsFailCount\":5, \"drawGameShowTipsTimeLimit\":20, \"drawGame_interstitialCD\":30, \"drawGame_rewardResetInterstitialCD\":30, \"drawGame_interstitialShowMinLevel\":6, \"drawGame_interstitialShowIntervalCount\":5, \"drawGame_interstitialShowFailedCount\":30, \"drawGame_interstitalAdLevelRange\":[4,999], \"drawGame_interstitalAdRangeLevelCount\":[4,2], \"drawGame_interstititalAdRangeFailedCount\":[6,6], \"drawGame_dailyNOAdPopupAutoShowCount\":5, \"drawGame_gameTipsPopupCoinCost\":60, \"drawGame_gameTipsPopupPassLevelCoinCost\":40, \"level_sort\":[20001,20045,20003,20004,20008,20002,20015,20022,20005,20023,20010,20007,20016,20012,20011,20006,20032,20033,20042,20043,20017,20019,20014,20035,20024,20029,20030,20027,20034,20036,20037,20038,20039,20040,20047,20053,20054,20055,20056,20057,20058,20059,20060,20061,20062,20063,20064,20065,20066,20041,20044,20046,20048,20049,20021,20050,20051,20069,20052,20067,20068,20020,20070,20071,20072,20073,20074,20009,20075,20076,20077,20078,20079,20080,20028,20031,20013,20025,20018,20026,20081,20082,20083,20084,20085,20086,20087,20088,20089,20090,20091,20092,20093,20094,20095,20096,20097,20098,20099]}",
				["iwannergame_config"] = "{ \"iWannerGame_IsOpen\":true, \"iWannerGame_showSkipFailCount\":2, \"iWannerGame_interstitialCD\":30, \"iWannerGame_rewardResetInterstitialCD\":30, \"iWannerGame_interstitialShowMinGroup\":1, \"iWannerGame_interstitialShowIntervalCount\":1, \"iWannerGame_interstitialShowFailedCount\":10, \"iWannerGame_dailyNOAdPopupAutoShowCount\":5, \"iWannerGame_interstitialShowPassedLevelCount\":-1, \"iwannarGame_interstitalAdLevelRange\":[1,999], \"iwannarGame_interstitalAdRangeLevelCount\":[1,1], \"iwannarGame_interstititalAdRangeFailedCount\":[12,12]}",
				["diggame_config"] = "{\"enabled\":false,\"digGameIsOpen\":true,\"digGameShowTipsFailCount\":5,\"digGameShowTipsTimeLimit\":20,\"digGame_interstitialCD\":30,\"digGame_rewardResetInterstitialCD\":30,\"digGame_interstitialShowMinLevel\":6,\"digGame_interstitialShowIntervalCount\":5,\"digGame_interstitialShowFailedCount\":30,\"digGame_dailyNOAdPopupAutoShowCount\":5,\"digGame_gameTipsPopupCoinCost\":60,\"digGame_gameTipsPopupPassLevelCoinCost\":40,\"digGame_interstitalAdLevelRange\":[4,999],\"digGame_interstitalAdRangeLevelCount\":[4,2],\"digGame_interstititalAdRangeFailedCount\":[6,6],\"level_sort\":[40001,40002,40003,40004,40005,40006,40007,40008,40009,40010,40011,40012,40013,40014,40015,40016,40017,40018,40019,40020,40021,40022,40023,40024,40025,40026,40027,40028,40029,40030]}",
			},
			convertedKeys: new Dictionary<string, string>()         
	    ))
    {
        //TODO: Complete rest code
    }

}

/// <summary>
/// 自动生成的全局 BrainPunk Products 定义类，请勿修改
/// </summary>
internal static class BrainPunkProducts 
{
	internal static ProductId NoAds = new ProductId (
		android: "brpk.a.iap.noads1",
		ios: "brpk.i.iap.noads1",
		attr: TransactionAttribute.Asset,
		points: false,
		extras: new Dictionary<string, string>() {
			["ignore_sales"] = "true",
		}
);

	internal static ProductId CoinPack250 = new ProductId (
		android: "brpk.a.iap.coinpack1",
		ios: "brpk.i.iap.coinpack1",
		attr: TransactionAttribute.Consumable,
		points: false);

	internal static ProductId CoinPack500 = new ProductId (
		android: "brpk.a.iap.coinpack2",
		ios: "brpk.i.iap.coinpack2",
		attr: TransactionAttribute.Consumable,
		points: false);

	internal static ProductId CoinPack1000 = new ProductId (
		android: "brpk.a.iap.coinpack3",
		ios: "brpk.i.iap.coinpack3",
		attr: TransactionAttribute.Consumable,
		points: false);

	internal static ProductId CoinPack2500 = new ProductId (
		android: "brpk.a.iap.coinpack4",
		ios: "brpk.i.iap.coinpack4",
		attr: TransactionAttribute.Consumable,
		points: false);

	internal static ProductId NoadBundle = new ProductId (
		android: "brpk.a.iap.noadsbundle",
		ios: "brpk.i.iap.noadsbundle",
		attr: TransactionAttribute.Asset,
		points: false,
		extras: new Dictionary<string, string>() {
			["ignore_sales"] = "true",
		}
);

	internal static ProductId NewnoadBundle = new ProductId (
		android: "brpk.a.iap.newnoadsbundle",
		ios: "brpk.i.iap.newnoadsbundle",
		attr: TransactionAttribute.Asset,
		points: false,
		extras: new Dictionary<string, string>() {
			["ignore_sales"] = "true",
		}
);

	internal static ProductId NewnoAds = new ProductId (
		android: "brpk.a.iap.newnoads",
		ios: "brpk.i.iap.newnoads",
		attr: TransactionAttribute.Asset,
		points: false,
		extras: new Dictionary<string, string>() {
			["ignore_sales"] = "true",
		}
);

	internal static ProductId Hint99 = new ProductId (
		android: "brpk.a.iap.hint99",
		ios: "brpk.i.iap.hint99",
		attr: TransactionAttribute.Asset,
		points: false);

	internal static ProductId GiftPack1 = new ProductId (
		android: "brpk.a.iap.giftpack1",
		ios: "brpk.i.iap.giftpack01",
		attr: TransactionAttribute.Asset,
		points: false);

	internal static ProductId GiftPack2 = new ProductId (
		android: "brpk.a.iap.giftpack2",
		ios: "brpk.i.iap.giftpack02",
		attr: TransactionAttribute.Asset,
		points: false);

	internal static ProductId GiftPack3 = new ProductId (
		android: "brpk.a.iap.giftpack3",
		ios: "brpk.i.iap.giftpack03",
		attr: TransactionAttribute.Asset,
		points: false);

	internal static ProductId GiftPack4 = new ProductId (
		android: "brpk.a.iap.giftpack4",
		ios: "brpk.i.iap.giftpack04",
		attr: TransactionAttribute.Asset,
		points: false);

	internal static ProductId GiftPack5 = new ProductId (
		android: "brpk.a.iap.giftpack5",
		ios: "brpk.i.iap.giftpack05",
		attr: TransactionAttribute.Asset,
		points: false);

	internal static ProductId FirstPack = new ProductId (
		android: "brpk.a.iap.firstpack",
		ios: "brpk.i.iap.firstpack",
		attr: TransactionAttribute.Consumable,
		points: false);

	internal static Manifest buildNoAdsManifest(TransactionIntent intent)
	{
		if (intent.ProductId != NoAds)
			return Manifest.Empty;

		var extras = new Dictionary<string, object>() {
			{ ExtraReservedField.ContentId, intent.ProductId.Sku },
			{ ExtraReservedField.Scene, intent.Scene },
			{ ExtraReservedField.Rate, intent.Rate },
			{ ExtraReservedField.Sales, intent.Sales },
			{ "ignore_sales", "true" }
		};
		return new Manifest(category: "no_ads", extras: extras);
	}

	internal static Manifest buildCoinPack250Manifest(TransactionIntent intent)
	{
		if (intent.ProductId != CoinPack250)
			return Manifest.Empty;

		var extras = new Dictionary<string, object>() {
			{ ExtraReservedField.ContentId, intent.ProductId.Sku },
			{ ExtraReservedField.Scene, intent.Scene },
			{ ExtraReservedField.Rate, intent.Rate },
			{ ExtraReservedField.Sales, intent.Sales }
		};
		return new Manifest(category: "coin", extras: extras);
	}

	internal static Manifest buildCoinPack500Manifest(TransactionIntent intent)
	{
		if (intent.ProductId != CoinPack500)
			return Manifest.Empty;

		var extras = new Dictionary<string, object>() {
			{ ExtraReservedField.ContentId, intent.ProductId.Sku },
			{ ExtraReservedField.Scene, intent.Scene },
			{ ExtraReservedField.Rate, intent.Rate },
			{ ExtraReservedField.Sales, intent.Sales }
		};
		return new Manifest(category: "coin", extras: extras);
	}

	internal static Manifest buildCoinPack1000Manifest(TransactionIntent intent)
	{
		if (intent.ProductId != CoinPack1000)
			return Manifest.Empty;

		var extras = new Dictionary<string, object>() {
			{ ExtraReservedField.ContentId, intent.ProductId.Sku },
			{ ExtraReservedField.Scene, intent.Scene },
			{ ExtraReservedField.Rate, intent.Rate },
			{ ExtraReservedField.Sales, intent.Sales }
		};
		return new Manifest(category: "coin", extras: extras);
	}

	internal static Manifest buildCoinPack2500Manifest(TransactionIntent intent)
	{
		if (intent.ProductId != CoinPack2500)
			return Manifest.Empty;

		var extras = new Dictionary<string, object>() {
			{ ExtraReservedField.ContentId, intent.ProductId.Sku },
			{ ExtraReservedField.Scene, intent.Scene },
			{ ExtraReservedField.Rate, intent.Rate },
			{ ExtraReservedField.Sales, intent.Sales }
		};
		return new Manifest(category: "coin", extras: extras);
	}

	internal static Manifest buildNoadBundleManifest(TransactionIntent intent)
	{
		if (intent.ProductId != NoadBundle)
			return Manifest.Empty;

		var extras = new Dictionary<string, object>() {
			{ ExtraReservedField.ContentId, intent.ProductId.Sku },
			{ ExtraReservedField.Scene, intent.Scene },
			{ ExtraReservedField.Rate, intent.Rate },
			{ ExtraReservedField.Sales, intent.Sales },
			{ "ignore_sales", "true" }
		};
		return new Manifest(category: "no_ads", extras: extras);
	}

	internal static Manifest buildNewnoadBundleManifest(TransactionIntent intent)
	{
		if (intent.ProductId != NewnoadBundle)
			return Manifest.Empty;

		var extras = new Dictionary<string, object>() {
			{ ExtraReservedField.ContentId, intent.ProductId.Sku },
			{ ExtraReservedField.Scene, intent.Scene },
			{ ExtraReservedField.Rate, intent.Rate },
			{ ExtraReservedField.Sales, intent.Sales },
			{ "ignore_sales", "true" }
		};
		return new Manifest(category: "no_ads", extras: extras);
	}

	internal static Manifest buildNewnoAdsManifest(TransactionIntent intent)
	{
		if (intent.ProductId != NewnoAds)
			return Manifest.Empty;

		var extras = new Dictionary<string, object>() {
			{ ExtraReservedField.ContentId, intent.ProductId.Sku },
			{ ExtraReservedField.Scene, intent.Scene },
			{ ExtraReservedField.Rate, intent.Rate },
			{ ExtraReservedField.Sales, intent.Sales },
			{ "ignore_sales", "true" }
		};
		return new Manifest(category: "no_ads", extras: extras);
	}

	internal static Manifest buildHint99Manifest(TransactionIntent intent)
	{
		if (intent.ProductId != Hint99)
			return Manifest.Empty;

		var extras = new Dictionary<string, object>() {
			{ ExtraReservedField.ContentId, intent.ProductId.Sku },
			{ ExtraReservedField.Scene, intent.Scene },
			{ ExtraReservedField.Rate, intent.Rate },
			{ ExtraReservedField.Sales, intent.Sales }
		};
		return new Manifest(category: "hint", extras: extras);
	}

	internal static Manifest buildGiftPack1Manifest(TransactionIntent intent)
	{
		if (intent.ProductId != GiftPack1)
			return Manifest.Empty;

		var extras = new Dictionary<string, object>() {
			{ ExtraReservedField.ContentId, intent.ProductId.Sku },
			{ ExtraReservedField.Scene, intent.Scene },
			{ ExtraReservedField.Rate, intent.Rate },
			{ ExtraReservedField.Sales, intent.Sales }
		};
		return new Manifest(category: "gift_pack", extras: extras);
	}

	internal static Manifest buildGiftPack2Manifest(TransactionIntent intent)
	{
		if (intent.ProductId != GiftPack2)
			return Manifest.Empty;

		var extras = new Dictionary<string, object>() {
			{ ExtraReservedField.ContentId, intent.ProductId.Sku },
			{ ExtraReservedField.Scene, intent.Scene },
			{ ExtraReservedField.Rate, intent.Rate },
			{ ExtraReservedField.Sales, intent.Sales }
		};
		return new Manifest(category: "gift_pack", extras: extras);
	}

	internal static Manifest buildGiftPack3Manifest(TransactionIntent intent)
	{
		if (intent.ProductId != GiftPack3)
			return Manifest.Empty;

		var extras = new Dictionary<string, object>() {
			{ ExtraReservedField.ContentId, intent.ProductId.Sku },
			{ ExtraReservedField.Scene, intent.Scene },
			{ ExtraReservedField.Rate, intent.Rate },
			{ ExtraReservedField.Sales, intent.Sales }
		};
		return new Manifest(category: "gift_pack", extras: extras);
	}

	internal static Manifest buildGiftPack4Manifest(TransactionIntent intent)
	{
		if (intent.ProductId != GiftPack4)
			return Manifest.Empty;

		var extras = new Dictionary<string, object>() {
			{ ExtraReservedField.ContentId, intent.ProductId.Sku },
			{ ExtraReservedField.Scene, intent.Scene },
			{ ExtraReservedField.Rate, intent.Rate },
			{ ExtraReservedField.Sales, intent.Sales }
		};
		return new Manifest(category: "gift_pack", extras: extras);
	}

	internal static Manifest buildGiftPack5Manifest(TransactionIntent intent)
	{
		if (intent.ProductId != GiftPack5)
			return Manifest.Empty;

		var extras = new Dictionary<string, object>() {
			{ ExtraReservedField.ContentId, intent.ProductId.Sku },
			{ ExtraReservedField.Scene, intent.Scene },
			{ ExtraReservedField.Rate, intent.Rate },
			{ ExtraReservedField.Sales, intent.Sales }
		};
		return new Manifest(category: "gift_pack", extras: extras);
	}

	internal static Manifest buildFirstPackManifest(TransactionIntent intent)
	{
		if (intent.ProductId != FirstPack)
			return Manifest.Empty;

		var extras = new Dictionary<string, object>() {
			{ ExtraReservedField.ContentId, intent.ProductId.Sku },
			{ ExtraReservedField.Scene, intent.Scene },
			{ ExtraReservedField.Rate, intent.Rate },
			{ ExtraReservedField.Sales, intent.Sales }
		};
		return new Manifest(category: "coin", extras: extras);
	}

	internal static HashSet<ProductId> oneOffChargeIapIds = new HashSet<ProductId>() 
	{
		NoAds,
		CoinPack250,
		CoinPack500,
		CoinPack1000,
		CoinPack2500,
		NoadBundle,
		NewnoadBundle,
		NewnoAds,
		Hint99,
		GiftPack1,
		GiftPack2,
		GiftPack3,
		GiftPack4,
		GiftPack5,
		FirstPack,
	};

	internal static HashSet<ProductId> subscriptionsIapIds = new HashSet<ProductId>() 
	{
	};

	internal static HashSet<ProductId> igcIds = new HashSet<ProductId>() 
	{
	};

	internal static HashSet<ProductId> rewardIds = new HashSet<ProductId>() 
	{
	};

	internal static HashSet<ProductId> noAdsCapIds = new HashSet<ProductId>() 
	{
		NoAds,
		NoadBundle,
		NewnoadBundle,
		NewnoAds,
	};

	internal static HashSet<ProductId> pointsIds = new HashSet<ProductId>() 
	{
	};

	internal static Dictionary<string, string> groupMap = new Dictionary<string, string>() 
	{
	};

	internal static List<ManifestBuilder> manifestBuilders = new List<ManifestBuilder>() 
	{
		buildNoAdsManifest,
		buildCoinPack250Manifest,
		buildCoinPack500Manifest,
		buildCoinPack1000Manifest,
		buildCoinPack2500Manifest,
		buildNoadBundleManifest,
		buildNewnoadBundleManifest,
		buildNewnoAdsManifest,
		buildHint99Manifest,
		buildGiftPack1Manifest,
		buildGiftPack2Manifest,
		buildGiftPack3Manifest,
		buildGiftPack4Manifest,
		buildGiftPack5Manifest,
		buildFirstPackManifest,
	};
}


/// <summary>
/// 全局 GuruAppSepc 工厂类定义
/// </summary>
public class GuruSpecFactory
{
    public static AppSpec Create(string flavor)
    {
		if (flavor == "main") {
			return BrainPunkAppSpec.Instance;
		}

        Debug.LogError($"Try to create AppSpec but flavor \"{flavor}\" is invalid");
        return null;
    }
}


/// <summary>
/// 全局 Flavor 定义
/// </summary>
public class Flavors
{
	public const string main = "main";

}


/// <summary>
/// 全局 Products 定义
/// </summary>
public class Products
{
	public static ProductId NoAds {
		get => BrainPunkProducts.NoAds;
	}
	public static ProductId CoinPack250 {
		get => BrainPunkProducts.CoinPack250;
	}
	public static ProductId CoinPack500 {
		get => BrainPunkProducts.CoinPack500;
	}
	public static ProductId CoinPack1000 {
		get => BrainPunkProducts.CoinPack1000;
	}
	public static ProductId CoinPack2500 {
		get => BrainPunkProducts.CoinPack2500;
	}
	public static ProductId NoadBundle {
		get => BrainPunkProducts.NoadBundle;
	}
	public static ProductId NewnoadBundle {
		get => BrainPunkProducts.NewnoadBundle;
	}
	public static ProductId NewnoAds {
		get => BrainPunkProducts.NewnoAds;
	}
	public static ProductId Hint99 {
		get => BrainPunkProducts.Hint99;
	}
	public static ProductId GiftPack1 {
		get => BrainPunkProducts.GiftPack1;
	}
	public static ProductId GiftPack2 {
		get => BrainPunkProducts.GiftPack2;
	}
	public static ProductId GiftPack3 {
		get => BrainPunkProducts.GiftPack3;
	}
	public static ProductId GiftPack4 {
		get => BrainPunkProducts.GiftPack4;
	}
	public static ProductId GiftPack5 {
		get => BrainPunkProducts.GiftPack5;
	}
	public static ProductId FirstPack {
		get => BrainPunkProducts.FirstPack;
	}
	public static class ProductName {
		public const string NoAds = "no_ads";
		public const string CoinPack250 = "coin_pack_250";
		public const string CoinPack500 = "coin_pack_500";
		public const string CoinPack1000 = "coin_pack_1000";
		public const string CoinPack2500 = "coin_pack_2500";
		public const string NoadBundle = "noad_bundle";
		public const string NewnoadBundle = "newnoad_bundle";
		public const string NewnoAds = "newno_ads";
		public const string Hint99 = "hint99";
		public const string GiftPack1 = "gift_pack_1";
		public const string GiftPack2 = "gift_pack_2";
		public const string GiftPack3 = "gift_pack_3";
		public const string GiftPack4 = "gift_pack_4";
		public const string GiftPack5 = "gift_pack_5";
		public const string FirstPack = "first_pack";
	}

	private static Dictionary<string, ProductId> _productMap = new Dictionary<string, ProductId>() 
	{
		["no_ads"] = NoAds,
		["coin_pack_250"] = CoinPack250,
		["coin_pack_500"] = CoinPack500,
		["coin_pack_1000"] = CoinPack1000,
		["coin_pack_2500"] = CoinPack2500,
		["noad_bundle"] = NoadBundle,
		["newnoad_bundle"] = NewnoadBundle,
		["newno_ads"] = NewnoAds,
		["hint99"] = Hint99,
		["gift_pack_1"] = GiftPack1,
		["gift_pack_2"] = GiftPack2,
		["gift_pack_3"] = GiftPack3,
		["gift_pack_4"] = GiftPack4,
		["gift_pack_5"] = GiftPack5,
		["first_pack"] = FirstPack,
	};

	private static List<string> _allProductNameList = new List<string>() 
	{
		ProductName.NoAds,
		ProductName.CoinPack250,
		ProductName.CoinPack500,
		ProductName.CoinPack1000,
		ProductName.CoinPack2500,
		ProductName.NoadBundle,
		ProductName.NewnoadBundle,
		ProductName.NewnoAds,
		ProductName.Hint99,
		ProductName.GiftPack1,
		ProductName.GiftPack2,
		ProductName.GiftPack3,
		ProductName.GiftPack4,
		ProductName.GiftPack5,
		ProductName.FirstPack,
	};

	/// <summary>
	/// 获取所有iap prudentName
	/// </summary>
	public static List<string> AllProductNameList => _allProductNameList;

	/// <summary>
	/// 获取 Product ID
	/// </summary>
	/// <param name="productName"></param>
	/// <returns></returns>
	public static ProductId GetProductId(string productName)
	{
		if (_productMap.TryGetValue(productName, out var productId))
		{
			return productId;
		}
		return null;
	}

	/// <summary>
	/// 获取 Product Name
	/// </summary>
	/// <param name="product"></param>
	/// <returns></returns>
	public static string GetProductName(ProductId product)
	{
		var findProduct = _productMap.FirstOrDefault(c => c.Value == product);
		return findProduct.Key;
	}


}

/// <summary>
/// 全局 Achievement 定义
/// </summary>
public class Achievements
{

	public static Achievement The_Adventure_Begins = Achievement.Create(
		id: "The_Adventure_Begins",
		type: AchievementRecordType.Single,
		bindings: new ServiceBinding []
		{
			ServiceBinding.Create(
				providerId: "google_play_games",
				remoteId: "CgkIiNrO94cREAIQDg",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "CgkIiNrO94cREAIQDg",
					["points"] = 10,
				},
				metadataJson: "{\"remote_id\":\"CgkIiNrO94cREAIQDg\",\"points\":10}"),
			ServiceBinding.Create(
				providerId: "apple_game_center",
				remoteId: "BrainPunk_The_Adventure_Begins",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "BrainPunk_The_Adventure_Begins",
					["points"] = 10,
				},
				metadataJson: "{\"remote_id\":\"BrainPunk_The_Adventure_Begins\",\"points\":10}"),
		},
		target: 1,
		groupId: "",
		defaultTitle: "The Adventure Begins",
		defaultDescription: "Complete Level 1.",
		defaultPoints: 0);

	public static Achievement Take_a_Break = Achievement.Create(
		id: "Take_a_Break",
		type: AchievementRecordType.Single,
		bindings: new ServiceBinding []
		{
			ServiceBinding.Create(
				providerId: "google_play_games",
				remoteId: "CgkIiNrO94cREAIQFw",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "CgkIiNrO94cREAIQFw",
					["points"] = 10,
				},
				metadataJson: "{\"remote_id\":\"CgkIiNrO94cREAIQFw\",\"points\":10}"),
			ServiceBinding.Create(
				providerId: "apple_game_center",
				remoteId: "BrainPunk_Take_a_Break",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "BrainPunk_Take_a_Break",
					["points"] = 10,
				},
				metadataJson: "{\"remote_id\":\"BrainPunk_Take_a_Break\",\"points\":10}"),
		},
		target: 1,
		groupId: "",
		defaultTitle: "Take a Break",
		defaultDescription: "Enter a To Be Continued level for the first time.",
		defaultPoints: 0);

	public static Achievement Pop_It = Achievement.Create(
		id: "Pop_It",
		type: AchievementRecordType.Single,
		bindings: new ServiceBinding []
		{
			ServiceBinding.Create(
				providerId: "google_play_games",
				remoteId: "CgkIiNrO94cREAIQCA",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "CgkIiNrO94cREAIQCA",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"CgkIiNrO94cREAIQCA\",\"points\":5}"),
			ServiceBinding.Create(
				providerId: "apple_game_center",
				remoteId: "BrainPunk_Pop_It",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "BrainPunk_Pop_It",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"BrainPunk_Pop_It\",\"points\":5}"),
		},
		target: 1,
		groupId: "",
		defaultTitle: "Pop It",
		defaultDescription: "Complete the designated level for the first time.",
		defaultPoints: 0);

	public static Achievement Mind_Your_Manners = Achievement.Create(
		id: "Mind_Your_Manners",
		type: AchievementRecordType.Single,
		bindings: new ServiceBinding []
		{
			ServiceBinding.Create(
				providerId: "google_play_games",
				remoteId: "CgkIiNrO94cREAIQCQ",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "CgkIiNrO94cREAIQCQ",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"CgkIiNrO94cREAIQCQ\",\"points\":5}"),
			ServiceBinding.Create(
				providerId: "apple_game_center",
				remoteId: "BrainPunk_Mind_Your_Manners",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "BrainPunk_Mind_Your_Manners",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"BrainPunk_Mind_Your_Manners\",\"points\":5}"),
		},
		target: 1,
		groupId: "",
		defaultTitle: "Mind Your Manners",
		defaultDescription: "Complete the designated level for the first time.",
		defaultPoints: 0);

	public static Achievement Fragile = Achievement.Create(
		id: "Fragile",
		type: AchievementRecordType.Single,
		bindings: new ServiceBinding []
		{
			ServiceBinding.Create(
				providerId: "google_play_games",
				remoteId: "CgkIiNrO94cREAIQEg",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "CgkIiNrO94cREAIQEg",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"CgkIiNrO94cREAIQEg\",\"points\":5}"),
			ServiceBinding.Create(
				providerId: "apple_game_center",
				remoteId: "BrainPunk_Fragile",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "BrainPunk_Fragile",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"BrainPunk_Fragile\",\"points\":5}"),
		},
		target: 1,
		groupId: "",
		defaultTitle: "Fragile",
		defaultDescription: "Complete the designated level for the first time.",
		defaultPoints: 0);

	public static Achievement The_Fourth_Wall = Achievement.Create(
		id: "The_Fourth_Wall",
		type: AchievementRecordType.Single,
		bindings: new ServiceBinding []
		{
			ServiceBinding.Create(
				providerId: "google_play_games",
				remoteId: "CgkIiNrO94cREAIQBA",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "CgkIiNrO94cREAIQBA",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"CgkIiNrO94cREAIQBA\",\"points\":5}"),
			ServiceBinding.Create(
				providerId: "apple_game_center",
				remoteId: "BrainPunk_The_Fourth_Wall",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "BrainPunk_The_Fourth_Wall",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"BrainPunk_The_Fourth_Wall\",\"points\":5}"),
		},
		target: 1,
		groupId: "",
		defaultTitle: "The Fourth Wall",
		defaultDescription: "Complete the designated level for the first time.",
		defaultPoints: 0);

	public static Achievement One_Mean_Big_Dog = Achievement.Create(
		id: "One_Mean_Big_Dog",
		type: AchievementRecordType.Single,
		bindings: new ServiceBinding []
		{
			ServiceBinding.Create(
				providerId: "google_play_games",
				remoteId: "CgkIiNrO94cREAIQAw",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "CgkIiNrO94cREAIQAw",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"CgkIiNrO94cREAIQAw\",\"points\":5}"),
			ServiceBinding.Create(
				providerId: "apple_game_center",
				remoteId: "BrainPunk_One_Mean_Big_Dog",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "BrainPunk_One_Mean_Big_Dog",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"BrainPunk_One_Mean_Big_Dog\",\"points\":5}"),
		},
		target: 1,
		groupId: "",
		defaultTitle: "One Mean Big Dog",
		defaultDescription: "Complete the designated level for the first time.",
		defaultPoints: 0);

	public static Achievement Cute_Little_Puppy = Achievement.Create(
		id: "Cute_Little_Puppy",
		type: AchievementRecordType.Single,
		bindings: new ServiceBinding []
		{
			ServiceBinding.Create(
				providerId: "google_play_games",
				remoteId: "CgkIiNrO94cREAIQCg",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "CgkIiNrO94cREAIQCg",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"CgkIiNrO94cREAIQCg\",\"points\":5}"),
			ServiceBinding.Create(
				providerId: "apple_game_center",
				remoteId: "BrainPunk_Cute_Little_Puppy",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "BrainPunk_Cute_Little_Puppy",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"BrainPunk_Cute_Little_Puppy\",\"points\":5}"),
		},
		target: 1,
		groupId: "",
		defaultTitle: "Cute Little Puppy",
		defaultDescription: "Complete the designated level for the first time.",
		defaultPoints: 0);

	public static Achievement Rule_Breaker = Achievement.Create(
		id: "Rule_Breaker",
		type: AchievementRecordType.Single,
		bindings: new ServiceBinding []
		{
			ServiceBinding.Create(
				providerId: "google_play_games",
				remoteId: "CgkIiNrO94cREAIQBg",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "CgkIiNrO94cREAIQBg",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"CgkIiNrO94cREAIQBg\",\"points\":5}"),
			ServiceBinding.Create(
				providerId: "apple_game_center",
				remoteId: "BrainPunk_Rule_Breaker",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "BrainPunk_Rule_Breaker",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"BrainPunk_Rule_Breaker\",\"points\":5}"),
		},
		target: 1,
		groupId: "",
		defaultTitle: "Rule Breaker",
		defaultDescription: "Complete the designated level for the first time.",
		defaultPoints: 0);

	public static Achievement Love_Dive = Achievement.Create(
		id: "Love_Dive",
		type: AchievementRecordType.Single,
		bindings: new ServiceBinding []
		{
			ServiceBinding.Create(
				providerId: "google_play_games",
				remoteId: "CgkIiNrO94cREAIQDw",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "CgkIiNrO94cREAIQDw",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"CgkIiNrO94cREAIQDw\",\"points\":5}"),
			ServiceBinding.Create(
				providerId: "apple_game_center",
				remoteId: "BrainPunk_Love_Dive",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "BrainPunk_Love_Dive",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"BrainPunk_Love_Dive\",\"points\":5}"),
		},
		target: 1,
		groupId: "",
		defaultTitle: "Love Dive",
		defaultDescription: "Complete the designated level for the first time.",
		defaultPoints: 0);

	public static Achievement Race_Against_Time = Achievement.Create(
		id: "Race_Against_Time",
		type: AchievementRecordType.Single,
		bindings: new ServiceBinding []
		{
			ServiceBinding.Create(
				providerId: "google_play_games",
				remoteId: "CgkIiNrO94cREAIQBQ",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "CgkIiNrO94cREAIQBQ",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"CgkIiNrO94cREAIQBQ\",\"points\":5}"),
			ServiceBinding.Create(
				providerId: "apple_game_center",
				remoteId: "BrainPunk_Race_Against_Time",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "BrainPunk_Race_Against_Time",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"BrainPunk_Race_Against_Time\",\"points\":5}"),
		},
		target: 1,
		groupId: "",
		defaultTitle: "Race Against Time",
		defaultDescription: "Complete the designated level for the first time.",
		defaultPoints: 0);

	public static Achievement Power_of_Words = Achievement.Create(
		id: "Power_of_Words",
		type: AchievementRecordType.Single,
		bindings: new ServiceBinding []
		{
			ServiceBinding.Create(
				providerId: "google_play_games",
				remoteId: "CgkIiNrO94cREAIQFg",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "CgkIiNrO94cREAIQFg",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"CgkIiNrO94cREAIQFg\",\"points\":5}"),
			ServiceBinding.Create(
				providerId: "apple_game_center",
				remoteId: "BrainPunk_Power_of_Words",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "BrainPunk_Power_of_Words",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"BrainPunk_Power_of_Words\",\"points\":5}"),
		},
		target: 1,
		groupId: "",
		defaultTitle: "Power of Words",
		defaultDescription: "Complete the designated level for the first time.",
		defaultPoints: 0);

	public static Achievement Fly_Away = Achievement.Create(
		id: "Fly_Away",
		type: AchievementRecordType.Single,
		bindings: new ServiceBinding []
		{
			ServiceBinding.Create(
				providerId: "google_play_games",
				remoteId: "CgkIiNrO94cREAIQEw",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "CgkIiNrO94cREAIQEw",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"CgkIiNrO94cREAIQEw\",\"points\":5}"),
			ServiceBinding.Create(
				providerId: "apple_game_center",
				remoteId: "BrainPunk_Fly_Away",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "BrainPunk_Fly_Away",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"BrainPunk_Fly_Away\",\"points\":5}"),
		},
		target: 1,
		groupId: "",
		defaultTitle: "Fly Away",
		defaultDescription: "Complete the designated level for the first time.",
		defaultPoints: 0);

	public static Achievement Is_There_a_Bunny_on_the_Moon = Achievement.Create(
		id: "Is_There_a_Bunny_on_the_Moon",
		type: AchievementRecordType.Single,
		bindings: new ServiceBinding []
		{
			ServiceBinding.Create(
				providerId: "google_play_games",
				remoteId: "CgkIiNrO94cREAIQDA",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "CgkIiNrO94cREAIQDA",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"CgkIiNrO94cREAIQDA\",\"points\":5}"),
			ServiceBinding.Create(
				providerId: "apple_game_center",
				remoteId: "BrainPunk_Is_There_a_Bunny_on_the_Moon",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "BrainPunk_Is_There_a_Bunny_on_the_Moon",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"BrainPunk_Is_There_a_Bunny_on_the_Moon\",\"points\":5}"),
		},
		target: 1,
		groupId: "",
		defaultTitle: "Is There a Bunny on the Moon",
		defaultDescription: "Complete the designated level for the first time.",
		defaultPoints: 0);

	public static Achievement I_Am_Not_a_Robot = Achievement.Create(
		id: "I_Am_Not_a_Robot",
		type: AchievementRecordType.Single,
		bindings: new ServiceBinding []
		{
			ServiceBinding.Create(
				providerId: "google_play_games",
				remoteId: "CgkIiNrO94cREAIQEQ",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "CgkIiNrO94cREAIQEQ",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"CgkIiNrO94cREAIQEQ\",\"points\":5}"),
			ServiceBinding.Create(
				providerId: "apple_game_center",
				remoteId: "BrainPunk_I_Am_Not_a_Robot",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "BrainPunk_I_Am_Not_a_Robot",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"BrainPunk_I_Am_Not_a_Robot\",\"points\":5}"),
		},
		target: 1,
		groupId: "",
		defaultTitle: "I Am Not a Robot",
		defaultDescription: "Complete the designated level for the first time.",
		defaultPoints: 0);

	public static Achievement A_Flash_of_Inspiration = Achievement.Create(
		id: "A_Flash_of_Inspiration",
		type: AchievementRecordType.Single,
		bindings: new ServiceBinding []
		{
			ServiceBinding.Create(
				providerId: "google_play_games",
				remoteId: "CgkIiNrO94cREAIQBw",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "CgkIiNrO94cREAIQBw",
					["points"] = 10,
				},
				metadataJson: "{\"remote_id\":\"CgkIiNrO94cREAIQBw\",\"points\":10}"),
			ServiceBinding.Create(
				providerId: "apple_game_center",
				remoteId: "BrainPunk_A_Flash_of_Inspiration",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "BrainPunk_A_Flash_of_Inspiration",
					["points"] = 10,
				},
				metadataJson: "{\"remote_id\":\"BrainPunk_A_Flash_of_Inspiration\",\"points\":10}"),
		},
		target: 1,
		groupId: "",
		defaultTitle: "A Flash of Inspiration",
		defaultDescription: "Use the Hint feature for the first time.",
		defaultPoints: 0);

	public static Achievement Puzzle_Master_I = Achievement.Create(
		id: "Puzzle_Master_I",
		type: AchievementRecordType.Incremental,
		bindings: new ServiceBinding []
		{
			ServiceBinding.Create(
				providerId: "google_play_games",
				remoteId: "CgkIiNrO94cREAIQFQ",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "CgkIiNrO94cREAIQFQ",
					["points"] = 5,
					["step"] = 5,
				},
				metadataJson: "{\"remote_id\":\"CgkIiNrO94cREAIQFQ\",\"points\":5,\"step\":5}"),
			ServiceBinding.Create(
				providerId: "apple_game_center",
				remoteId: "BrainPunk_Puzzle_Master_I",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "BrainPunk_Puzzle_Master_I",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"BrainPunk_Puzzle_Master_I\",\"points\":5}"),
		},
		target: 5,
		groupId: "",
		defaultTitle: "Puzzle Master I",
		defaultDescription: "Complete a total of 5 levels.",
		defaultPoints: 0);

	public static Achievement Puzzle_Master_II = Achievement.Create(
		id: "Puzzle_Master_II",
		type: AchievementRecordType.Incremental,
		bindings: new ServiceBinding []
		{
			ServiceBinding.Create(
				providerId: "google_play_games",
				remoteId: "CgkIiNrO94cREAIQFA",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "CgkIiNrO94cREAIQFA",
					["points"] = 5,
					["step"] = 10,
				},
				metadataJson: "{\"remote_id\":\"CgkIiNrO94cREAIQFA\",\"points\":5,\"step\":10}"),
			ServiceBinding.Create(
				providerId: "apple_game_center",
				remoteId: "BrainPunk_Puzzle_Master_II",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "BrainPunk_Puzzle_Master_II",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"BrainPunk_Puzzle_Master_II\",\"points\":5}"),
		},
		target: 10,
		groupId: "",
		defaultTitle: "Puzzle Master II",
		defaultDescription: "Complete a total of 10 levels.",
		defaultPoints: 0);

	public static Achievement Puzzle_Master_III = Achievement.Create(
		id: "Puzzle_Master_III",
		type: AchievementRecordType.Incremental,
		bindings: new ServiceBinding []
		{
			ServiceBinding.Create(
				providerId: "google_play_games",
				remoteId: "CgkIiNrO94cREAIQCw",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "CgkIiNrO94cREAIQCw",
					["points"] = 5,
					["step"] = 15,
				},
				metadataJson: "{\"remote_id\":\"CgkIiNrO94cREAIQCw\",\"points\":5,\"step\":15}"),
			ServiceBinding.Create(
				providerId: "apple_game_center",
				remoteId: "BrainPunk_Puzzle_Master_III",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "BrainPunk_Puzzle_Master_III",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"BrainPunk_Puzzle_Master_III\",\"points\":5}"),
		},
		target: 15,
		groupId: "",
		defaultTitle: "Puzzle Master III",
		defaultDescription: "Complete a total of 15 levels.",
		defaultPoints: 0);

	public static Achievement Puzzle_Master_IV = Achievement.Create(
		id: "Puzzle_Master_IV",
		type: AchievementRecordType.Incremental,
		bindings: new ServiceBinding []
		{
			ServiceBinding.Create(
				providerId: "google_play_games",
				remoteId: "CgkIiNrO94cREAIQEA",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "CgkIiNrO94cREAIQEA",
					["points"] = 5,
					["step"] = 20,
				},
				metadataJson: "{\"remote_id\":\"CgkIiNrO94cREAIQEA\",\"points\":5,\"step\":20}"),
			ServiceBinding.Create(
				providerId: "apple_game_center",
				remoteId: "BrainPunk_Puzzle_Master_IV",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "BrainPunk_Puzzle_Master_IV",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"BrainPunk_Puzzle_Master_IV\",\"points\":5}"),
		},
		target: 20,
		groupId: "",
		defaultTitle: "Puzzle Master IV",
		defaultDescription: "Complete a total of 20 levels.",
		defaultPoints: 0);

	public static Achievement Puzzle_Master_V = Achievement.Create(
		id: "Puzzle_Master_V",
		type: AchievementRecordType.Incremental,
		bindings: new ServiceBinding []
		{
			ServiceBinding.Create(
				providerId: "google_play_games",
				remoteId: "CgkIiNrO94cREAIQDQ",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "CgkIiNrO94cREAIQDQ",
					["points"] = 5,
					["step"] = 25,
				},
				metadataJson: "{\"remote_id\":\"CgkIiNrO94cREAIQDQ\",\"points\":5,\"step\":25}"),
			ServiceBinding.Create(
				providerId: "apple_game_center",
				remoteId: "BrainPunk_Puzzle_Master_V",
				alias: "",
				metadata: new Dictionary<string, object>()
				{
					["remote_id"] = "BrainPunk_Puzzle_Master_V",
					["points"] = 5,
				},
				metadataJson: "{\"remote_id\":\"BrainPunk_Puzzle_Master_V\",\"points\":5}"),
		},
		target: 25,
		groupId: "",
		defaultTitle: "Puzzle Master V",
		defaultDescription: "Complete a total of 25 levels.",
		defaultPoints: 0);

	public static ICollection<Achievement> AllAchievements = new List<Achievement>()
	{
		The_Adventure_Begins,
		Take_a_Break,
		Pop_It,
		Mind_Your_Manners,
		Fragile,
		The_Fourth_Wall,
		One_Mean_Big_Dog,
		Cute_Little_Puppy,
		Rule_Breaker,
		Love_Dive,
		Race_Against_Time,
		Power_of_Words,
		Fly_Away,
		Is_There_a_Bunny_on_the_Moon,
		I_Am_Not_a_Robot,
		A_Flash_of_Inspiration,
		Puzzle_Master_I,
		Puzzle_Master_II,
		Puzzle_Master_III,
		Puzzle_Master_IV,
		Puzzle_Master_V,
	};

	private static Dictionary<string, Achievement> _achievementMap = new Dictionary<string, Achievement>()
	{
		["The_Adventure_Begins"] = The_Adventure_Begins,
		["Take_a_Break"] = Take_a_Break,
		["Pop_It"] = Pop_It,
		["Mind_Your_Manners"] = Mind_Your_Manners,
		["Fragile"] = Fragile,
		["The_Fourth_Wall"] = The_Fourth_Wall,
		["One_Mean_Big_Dog"] = One_Mean_Big_Dog,
		["Cute_Little_Puppy"] = Cute_Little_Puppy,
		["Rule_Breaker"] = Rule_Breaker,
		["Love_Dive"] = Love_Dive,
		["Race_Against_Time"] = Race_Against_Time,
		["Power_of_Words"] = Power_of_Words,
		["Fly_Away"] = Fly_Away,
		["Is_There_a_Bunny_on_the_Moon"] = Is_There_a_Bunny_on_the_Moon,
		["I_Am_Not_a_Robot"] = I_Am_Not_a_Robot,
		["A_Flash_of_Inspiration"] = A_Flash_of_Inspiration,
		["Puzzle_Master_I"] = Puzzle_Master_I,
		["Puzzle_Master_II"] = Puzzle_Master_II,
		["Puzzle_Master_III"] = Puzzle_Master_III,
		["Puzzle_Master_IV"] = Puzzle_Master_IV,
		["Puzzle_Master_V"] = Puzzle_Master_V,
	};

	public static Achievement GetAchievement(string id)
	{
		if (_achievementMap.TryGetValue(id, out Achievement achievement))
			return achievement;
		return null;
	}


}
