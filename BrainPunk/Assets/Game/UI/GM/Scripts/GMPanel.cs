
using System;
using System.Collections.Generic;
using Bear.EventSystem;
using Bear.SaveModule;
using Bear.UI;
using Cysharp.Threading.Tasks;
using Game;
using Game.ConfigModule;
using Game.Events;
using Game.Play;
using GameCommon;
using Guru;
using Guru.SDK.Framework.Core.Data.Settings;
using Guru.Sdk.Info;
using Guru.UniKit.Debugger;
using R3;
using SDK.Remote;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class GMPanel : BaseUIView, IEventSender
{
    public ScrollRect View1;
    public ScrollRect View2;
    public PageView pages;
    private bool _isView1Visible;
    private bool _isInterstitialConfigVisible;
    private bool _isRemoteConfigFetchRunning;
    private IPlayController _interstitialDisplayController;
    private CompositeDisposable _guruDebugModeSubscriptions;
    private static bool s_isRuntimeDebugHostOpen;

    /// <summary>
    /// 用途：供其它运行态调试 IMGUI 判断 GMPanel 是否已经打开。
    /// 调用边界：只读状态，不关心 GM 内部 View1/View2 显隐，也不负责创建或查找 GMPanel。
    /// </summary>
    internal static bool IsRuntimeDebugHostOpen => s_isRuntimeDebugHostOpen;

    [SerializeField] private GameObject rtConfigObj;
    [SerializeField] private TMP_Text noad_Txt;

    /// <summary>Prefab 显式绑定的 OneLink 测试入口；通过现有 GM 隐藏入口与密码控制访问，不依赖调试宏。</summary>
    [SerializeField] private CustomButton oneLinkTestButton;

    public override void OnCreate()
    {
        base.OnCreate();

        ResolvePagesReference();
        _isView1Visible = GetInitialViewVisibility();

        BindButton(SwitchBtn, OnSwitchBtnClick);
        BindButton(RewardAdBtn, TestForRewardAd);
        BindButton(InterstitialBtn, TestForInterstitialAd);
        BindButton(InterstitialSwitchBtn, OnInterstitialConfigSwitchClick);
        BindButton(NextLevelBtn, NextLevel);
        BindButton(LastLevelBtn, LastLevel);
        BindButton(EnterLevelLoadingBtn, EnterLevelLoadingPanel);
        BindButton(DeleteBtn, DeleteAllData);
        BindButton(VictoryBtn, SendVictoryEvent);
        // Purchase1Btn.OnClick += TryToPurchase5Tips;
        BindButton(RestoreBtn, TryToRestorePurchase);
        BindButton(WaitingPopupBtn, ShowWaitingPopup);
        BindButton(RatingPopupBtn, ShowRating);
        BindButton(DebugBtn, ShowMaxDebug);
        BindButton(UnlockBtn, UnlockAllLevel);
        BindButton(UnlockPostcardBtn, CompleteAllPostcards);
        BindButton(UnlockSkinBtn, UnlockAllSkins);
        BindButton(SdkLogBtn, SendLogToSDK);
        BindButton(LoginBtn, TryLogin);
        BindButton(AchievementClearBtn, OnAchievementClear);
        BindButton(ComicsPopupBtn, ShowComics);
        BindButton(SdkDebugBtn, ShowSDKDebug);
        BindButton(UnlimitTipsBtn, OnUnlimitTipsClick);
        BindButton(ClearRemoteConfigBtn, OnCloseRemoteConfig);
        BindButton(FetchRemoteConfigBtn, FetchRemoteConfig);
        BindButton(GetRandomSkinBtn, GetRandomSkin);

        BindButton(RewardTest1Btn, GetRewardTest1);
        BindButton(RewardTest2Btn, GetRewardTest2);

        BindButton(GetStarBtn, Get100Star);
        BindButton(RemoveAllStarBtn, RemoveAllStar);
        BindButton(PostcardGuideBtn, ShowPostcardGuide);
        BindButton(PostcardClearRecordBtn, ClearPostcardStarRecord);

        BindButton(GameTipGuideClearBtn, ClearGameTipGuideData);

        BindButton(NoadBtn, SwitchNoAd);
        BindButton(DailyChallengeBtn, DailyChallengeIncreaseProcess);
        BindButton(DailyChestGiftBtn, ShowDailyChestGift);
        BindButton(DailyCoinGiftBtn, ShowDailyCoinGift);

        BindButton(oneLinkTestButton, ShowOneLinkTest);

        SetView1Visible(false);
        RefreshInterstitialConfigSwitchText();

    }

    /// <summary>
    /// 用途：标记 GMPanel 已打开，并在打开期间同步中台 DebugMode 到 GM 调试内容显隐。
    /// 调用边界：由 Bear.UI 打开生命周期调用；只控制已创建面板的既有调试内容，不创建或查找调试入口。
    /// </summary>
    public override void OnOpen()
    {
        base.OnOpen();
        _guruDebugModeSubscriptions?.Dispose();
        _guruDebugModeSubscriptions = new CompositeDisposable();
        GuruSettings.Instance.DebugMode
            .Observe()
            .DistinctUntilChanged()
            .ObserveOnMainThread()
            .Subscribe(SetView1Visible)
            .AddTo(_guruDebugModeSubscriptions);
        UnbindButton(UnlockCurrentPostcardBtn, CompleteCurrentPostcard);
        BindButton(UnlockCurrentPostcardBtn, CompleteCurrentPostcard);
        s_isRuntimeDebugHostOpen = true;
        RefreshDailyChestGiftIndexText();
    }

    private void DailyChallengeIncreaseProcess(CustomButton btn)
    {
        DB.GameSchedule.DailyChallengeCurrentProcess++;
        DB.GameSchedule.Save();
    }

    #region Gift Popup 调试入口

    private void RefreshDailyChestGiftIndexText()
    {
        if (ChestIndexTxt == null)
            return;

        int giftIndex = GiftManager.Instance.ChestGift.CurrentIndex;
        ChestIndexTxt.text = $"每日宝箱\nChest Index: {giftIndex}";
    }

    private void ShowDailyChestGift(CustomButton _)
    {
        int giftIndex = GiftManager.Instance.ChestGift.CurrentIndex;
        if (GiftManager.Instance.Open(GiftPopupType.ChestGift, giftIndex) == null)
            Debug.LogWarning($"[GMPanel] Daily ChestGift preview could not be opened. giftIndex:{giftIndex}");
    }

    private void ShowDailyCoinGift(CustomButton _)
    {
        if (GiftManager.Instance.Open(GiftPopupType.CoinGift) == null)
            Debug.LogWarning("[GMPanel] Daily CoinGift preview could not be opened.");
    }

    #endregion

    private void ShowOneLinkTest(CustomButton _)
    {
        if (!Application.isPlaying)
            return;
        GMOneLinkTestPopup.Create();
    }

    private void SwitchNoAd(CustomButton btn)
    {
        InterstitialAdPolicy.isNoADForTest = !InterstitialAdPolicy.isNoADForTest;
        if (noad_Txt)
            noad_Txt.text = string.Format("NoAd: {0}", InterstitialAdPolicy.isNoADForTest);
    }

    private void ClearGameTipGuideData(CustomButton btn)
    {
        DB.GameData.ToolConsumesCache.Clear();
        DB.GameData.HasTriggeredGameTipsGuide = false;
        DB.GameData.Save();
    }

    private void ClearPostcardStarRecord(CustomButton btn)
    {
        DB.Postcard.HasGuide = false;
        DB.Postcard.LevelCollectedStar.Clear();
        DB.Postcard.Save();
    }

    private void ShowPostcardGuide(CustomButton btn)
    {
        NormalGuidePopup.Create();
        // throw new NotImplementedException();
    }

    private void RemoveAllStar(CustomButton btn)
    {
        var bag = GameManager.Instance.Bag;
        var starCount = bag.GetToolCount(Config.Game.GameProps.Star);
        GameManager.Instance.Bag.RemoveTool(Config.Game.GameProps.Star, starCount, CostType.Other);
    }

    private void Get100Star(CustomButton btn)
    {
        GameManager.Instance.Bag.AddTool(Config.Game.GameProps.Star, 100, RewardType.Other);
    }

    private void GetRewardTest2(CustomButton btn)
    {
        // 测试 CommonGetRewardPopup 获取 getRandomSkin ，不传入倍率
        if (!TryGetRewardTestSkin(out var skin))
        {
            return;
        }

        Debug.Log($"[GMPanel] GetRewardTest2 Skin:{skin.Id}");
        CommonGetRewardPopup.Create(new Dictionary<Config.Game.GameProps, object>
        {
            { Config.Game.GameProps.Skin, skin.Id },
        });
    }

    private void GetRewardTest1(CustomButton btn)
    {
        // 测试 CommonGetRewardPopup 获取 getRandomSkin 和 coin 和 star， 传入倍率 4
        if (!TryGetRewardTestSkin(out var skin))
        {
            return;
        }

        var rewards = new Dictionary<Config.Game.GameProps, object>
        {
            { Config.Game.GameProps.Coin, 100 },
            { Config.Game.GameProps.Star, 5 },
            { Config.Game.GameProps.Skin, skin.Id },
        };

        Debug.Log($"[GMPanel] GetRewardTest1 Skin:{skin.Id}, Coin:100, Star:5, Multiple:4");
        CommonGetRewardPopup.Create(rewards, 4, () =>
        {
            GetSkinTransformPopup.Create(skin);
        });
    }

    private static bool TryGetRewardTestSkin(out Config.Skin skin)
    {
        skin = null;
        if (GameManager.Instance?.Bag == null)
        {
            Debug.LogError("[GMPanel] GetRewardTest failed. Bag is null.");
            return false;
        }

        skin = FindRandomUnownedSkin();
        if (skin != null)
        {
            return true;
        }

        Debug.LogWarning("[GMPanel] GetRewardTest skipped. No unowned skin candidate.");
        SystemTips.Show("没有可随机获取的未拥有皮肤");
        return false;
    }

    private void GetRandomSkin(CustomButton btn)
    {
        if (GameManager.Instance?.Bag == null)
        {
            Debug.LogError("[GMPanel] GetRandomSkin failed. Bag is null.");
            return;
        }

        var skin = FindRandomUnownedSkin();
        if (skin == null)
        {
            Debug.LogWarning("[GMPanel] GetRandomSkin skipped. No unowned skin candidate.");
            SystemTips.Show("没有可随机获取的未拥有皮肤");
            return;
        }

        Debug.Log($"[GMPanel] GetRandomSkin: {skin.Id}");
        GetSkinPopup.Create(skin);
    }

    /// <summary>
    /// 从 Skin 配表中随机选择一个未拥有的可授予皮肤；仅供 GM 调试按钮调用，不直接写入存档。
    /// </summary>
    private static Config.Skin FindRandomUnownedSkin()
    {
        var skinList = ConfigManager.Instance?.Tables?.TbSkin?.DataList;
        if (skinList == null || skinList.Count == 0)
        {
            Debug.LogWarning("[GMPanel] GetRandomSkin failed. Skin config is empty.");
            return null;
        }

        var gameData = DB.GameData;
        if (gameData == null)
        {
            Debug.LogError("[GMPanel] GetRandomSkin failed. GameData is null.");
            return null;
        }

        var ownedSkinIds = gameData.OwnedSkin != null
            ? new HashSet<string>(gameData.OwnedSkin, StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        var candidates = new List<Config.Skin>();
        for (int i = 0; i < skinList.Count; i++)
        {
            var skin = skinList[i];
            if (CanRandomGrantSkin(skin, ownedSkinIds))
            {
                candidates.Add(skin);
            }
        }

        return candidates.Count > 0
            ? candidates[UnityEngine.Random.Range(0, candidates.Count)]
            : null;
    }

    private static bool CanRandomGrantSkin(Config.Skin skin, HashSet<string> ownedSkinIds)
    {
        if (skin == null || string.IsNullOrEmpty(skin.Id))
        {
            return false;
        }

        if (ownedSkinIds != null && ownedSkinIds.Contains(skin.Id))
        {
            return false;
        }

        return skin.UnlockType != Config.Game.SkinUnlockType.Default &&
               skin.UnlockType != Config.Game.SkinUnlockType.Unavailable;
    }

    private void OnCloseRemoteConfig(CustomButton btn)
    {
        RemoteConfigService.ClearRemoteCache();
    }

    private void FetchRemoteConfig(CustomButton btn)
    {
        if (_isRemoteConfigFetchRunning)
        {
            Debug.LogWarning("[GMPanel] Manual Remote Config fetch ignored because a request is already running.");
            SystemTips.Show("Remote Config 正在拉取中");
            return;
        }

        _isRemoteConfigFetchRunning = true;
        FetchRemoteConfigAsync().Forget();
    }

    private async UniTaskVoid FetchRemoteConfigAsync()
    {
        try
        {
            Debug.Log("[GMPanel] Manual Remote Config fetch requested.");
            SystemTips.Show("开始拉取 Remote Config");
            await RemoteConfigService.ForceUpdateRemoteConfigsAsync();
            Debug.Log("[GMPanel] Manual Remote Config fetch flow completed. Check RemoteConfigService logs for the remote/fallback result.");
            SystemTips.Show("Remote Config 拉取流程已结束，请查看日志");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[GMPanel] Manual Remote Config fetch failed with an unhandled exception: {exception}");
            SystemTips.Show("Remote Config 拉取异常，请查看日志");
        }
        finally
        {
            _isRemoteConfigFetchRunning = false;
        }
    }

    public override void OnClose()
    {
        s_isRuntimeDebugHostOpen = false;
        _guruDebugModeSubscriptions?.Dispose();
        _guruDebugModeSubscriptions = null;
        base.OnClose();

        UnbindButton(SwitchBtn, OnSwitchBtnClick);
        UnbindButton(RewardAdBtn, TestForRewardAd);
        UnbindButton(InterstitialBtn, TestForInterstitialAd);
        UnbindButton(InterstitialSwitchBtn, OnInterstitialConfigSwitchClick);
        UnbindButton(NextLevelBtn, NextLevel);
        UnbindButton(LastLevelBtn, LastLevel);
        UnbindButton(EnterLevelLoadingBtn, EnterLevelLoadingPanel);
        UnbindButton(DeleteBtn, DeleteAllData);
        UnbindButton(VictoryBtn, SendVictoryEvent);
        UnbindButton(RestoreBtn, TryToRestorePurchase);
        UnbindButton(WaitingPopupBtn, ShowWaitingPopup);
        UnbindButton(RatingPopupBtn, ShowRating);
        UnbindButton(DebugBtn, ShowMaxDebug);
        UnbindButton(UnlockBtn, UnlockAllLevel);
        UnbindButton(UnlockPostcardBtn, CompleteAllPostcards);
        UnbindButton(UnlockCurrentPostcardBtn, CompleteCurrentPostcard);
        UnbindButton(UnlockSkinBtn, UnlockAllSkins);
        UnbindButton(SdkLogBtn, SendLogToSDK);
        UnbindButton(LoginBtn, TryLogin);
        UnbindButton(AchievementClearBtn, OnAchievementClear);
        UnbindButton(ComicsPopupBtn, ShowComics);
        UnbindButton(SdkDebugBtn, ShowSDKDebug);
        UnbindButton(UnlimitTipsBtn, OnUnlimitTipsClick);
        UnbindButton(ClearRemoteConfigBtn, OnCloseRemoteConfig);
        UnbindButton(FetchRemoteConfigBtn, FetchRemoteConfig);
        UnbindButton(GetRandomSkinBtn, GetRandomSkin);
        UnbindButton(RewardTest1Btn, GetRewardTest1);
        UnbindButton(RewardTest2Btn, GetRewardTest2);
        UnbindButton(DailyChestGiftBtn, ShowDailyChestGift);
        UnbindButton(DailyCoinGiftBtn, ShowDailyCoinGift);
        UnbindButton(oneLinkTestButton, ShowOneLinkTest);
    }

    private void OnUnlimitTipsClick(CustomButton btn)
    {
        GameManager.Instance.Bag.AddTool(Config.Game.GameProps.UnlimitTips, 1, RewardType.Other);
    }

    private void ShowSDKDebug(CustomButton btn)
    {
        try
        {
            Debug.Log("[GMPanel] Opening GuruDebugger...");
            GuruDebugger.ShowAdsMonitor();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GMPanel] ShowSDKDebug failed: {ex.Message}");
        }
    }

    private void ShowComics(CustomButton btn)
    {
        ComicsPopup.Create();
    }

    private void OnAchievementClear(CustomButton btn)
    {
        GameAchievementUtils.ResetAchievements();
    }

    private void TryLogin(CustomButton btn)
    {
        // GameAccountUtils.TryAutoLoginGame().Forget();
        GameAchievementUtils.TryToGetPackage();
    }

    private void SendLogToSDK(CustomButton btn)
    {
        LegacySDKProxy.SendLogToPbc().Forget();
    }

    private void ShowRating(CustomButton btn)
    {
        RatingPopup.Create();
    }

    private void UnlockAllLevel(CustomButton btn)
    {
        LevelCtrl mainLevelCtrl = PlayCtrl.Instance?.Level;
        DrawGameLevelCtrl drawLevelCtrl = GameManager.Instance?.DrawGameLevel;
        IWannerGameLevelCtrl iwannerLevelCtrl = GameManager.Instance?.IWannerGameLevel;
        DigGameLevelCtrl digLevelCtrl = GameManager.Instance?.DigGameLevel;

        int mainAddedCount = 0;
        bool mainSucceeded =
            mainLevelCtrl != null &&
            mainLevelCtrl.TryUnlockAllLevels(out mainAddedCount);
        int drawAddedCount = 0;
        bool drawSucceeded =
            drawLevelCtrl != null &&
            drawLevelCtrl.TryUnlockAllLevels(out drawAddedCount);
        int iwannerAddedCount = 0;
        bool iwannerSucceeded =
            iwannerLevelCtrl != null &&
            iwannerLevelCtrl.TryUnlockAllGroups(out iwannerAddedCount);
        int digAddedCount = 0;
        bool digSucceeded =
            digLevelCtrl != null &&
            digLevelCtrl.TryUnlockAllLevels(out digAddedCount);
        if (digSucceeded)
            UIManager.Instance?.GetUI<DigGame_ChoiceLevelPanel>()?.RefreshRemoteConfig();

        if (!mainSucceeded || !drawSucceeded || !iwannerSucceeded || !digSucceeded)
        {
            Debug.LogWarning(
                $"[GMPanel] Unlock all levels incomplete. main:{mainSucceeded}, draw:{drawSucceeded}, iwanner:{iwannerSucceeded}, dig:{digSucceeded}");
            return;
        }

        Debug.Log(
            $"[GMPanel] Unlock all levels complete. mainAdded:{mainAddedCount}, drawAdded:{drawAddedCount}, iwannerAdded:{iwannerAddedCount}, digAdded:{digAddedCount}");
    }

    /// <summary>
    /// 解锁 Skin 配表中的全部可用皮肤；仅供 GM 调试按钮调用，批量授予后统一保存一次。
    /// </summary>
    private void UnlockAllSkins(CustomButton btn)
    {
        var bag = GameManager.Instance?.Bag;
        var gameData = DB.GameData;
        var skinList = ConfigManager.Instance?.Tables?.TbSkin?.DataList;
        if (bag == null || gameData == null || skinList == null)
        {
            Debug.LogError(
                $"[GMPanel] Unlock all skins failed. bag:{bag != null}, gameData:{gameData != null}, skinConfig:{skinList != null}");
            SystemTips.Show("解锁所有皮肤失败，请检查配置和存档初始化");
            return;
        }

        int addedCount = 0;
        for (int i = 0; i < skinList.Count; i++)
        {
            var skin = skinList[i];
            if (skin == null ||
                string.IsNullOrEmpty(skin.Id) ||
                skin.UnlockType == Config.Game.SkinUnlockType.Unavailable)
            {
                continue;
            }

            if (bag.GetNewSkin(skin.Id, GetSkinWayType.Free, false))
            {
                addedCount++;
            }
        }

        if (addedCount > 0)
        {
            gameData.Save();
        }

        Debug.Log($"[GMPanel] Unlock all skins complete. added:{addedCount}");
        SystemTips.Show(addedCount > 0
            ? $"已解锁 {addedCount} 个皮肤"
            : "所有可用皮肤已解锁");
    }

    /// <summary>
    /// 完成全部普通 Postcard 配置；仅供 GM 调试按钮调用，不扣星、不发放页面完成奖励。
    /// </summary>
    private void CompleteAllPostcards(CustomButton btn)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        var postcardData = DB.Postcard;
        var postcardConfigs = ConfigManager.Instance?.Tables?.TbPostcardConfig?.DataList;
        if (postcardData == null || postcardConfigs == null)
        {
            Debug.LogError(
                $"[GMPanel] Complete all postcards failed. postcardData:{postcardData != null}, postcardConfig:{postcardConfigs != null}");
            SystemTips.Show("完成所有明信片失败，请检查配置和存档初始化");
            return;
        }

        var postcardIds = new HashSet<string>(StringComparer.Ordinal);
        Config.PostcardConfig firstPostcardConfig = null;
        for (int i = 0; i < postcardConfigs.Count; i++)
        {
            Config.PostcardConfig postcardConfig = postcardConfigs[i];
            if (!IsCompletablePostcardConfig(postcardConfig) || !postcardIds.Add(postcardConfig.Id))
            {
                continue;
            }

            if (firstPostcardConfig == null ||
                postcardConfig.Sort < firstPostcardConfig.Sort ||
                (postcardConfig.Sort == firstPostcardConfig.Sort &&
                 string.Compare(postcardConfig.Id, firstPostcardConfig.Id, StringComparison.Ordinal) < 0))
            {
                firstPostcardConfig = postcardConfig;
            }
        }

        if (postcardIds.Count == 0 || firstPostcardConfig == null)
        {
            Debug.LogWarning("[GMPanel] Complete all postcards skipped. No completable postcard config.");
            SystemTips.Show("没有可完成的明信片配置");
            return;
        }

        List<string> originalCompletedIds = postcardData.CompletedId != null
            ? new List<string>(postcardData.CompletedId)
            : null;
        List<int> originalCollectedPuzzleIds = postcardData.CurrentCollectedPuzzledId != null
            ? new List<int>(postcardData.CurrentCollectedPuzzledId)
            : null;
        string originalCurrentId = postcardData.CurrentId;
        bool originalHasGuide = postcardData.HasGuide;
        bool originalIsGuiding = postcardData.IsGuiding;

        postcardData.CompletedId ??= new List<string>();
        var completedIdSet = new HashSet<string>(postcardData.CompletedId, StringComparer.Ordinal);
        int addedCount = 0;
        foreach (string postcardId in postcardIds)
        {
            if (completedIdSet.Add(postcardId))
            {
                postcardData.CompletedId.Add(postcardId);
                addedCount++;
            }
        }

        postcardData.CurrentCollectedPuzzledId = new List<int>();
        if (!postcardIds.Contains(postcardData.CurrentId))
        {
            postcardData.CurrentId = firstPostcardConfig.Id;
        }

        postcardData.HasGuide = true;
        postcardData.IsGuiding = false;

        if (!DB.SavePostcard())
        {
            postcardData.CompletedId = originalCompletedIds;
            postcardData.CurrentCollectedPuzzledId = originalCollectedPuzzleIds;
            postcardData.CurrentId = originalCurrentId;
            postcardData.HasGuide = originalHasGuide;
            postcardData.IsGuiding = originalIsGuiding;
            Debug.LogWarning("[GMPanel] Complete all postcards failed because SavePostcard returned false.");
            SystemTips.Show("完成所有明信片失败，存档未写入");
            return;
        }

        RedDotManager.Refresh(RedDotKey.Postcard);
        Debug.Log(
            $"[GMPanel] Complete all postcards complete. added:{addedCount}, total:{postcardIds.Count}");
        SystemTips.Show(addedCount > 0
            ? $"已完成 {addedCount} 个明信片"
            : "所有明信片内容已完成");
    }

    /// <summary>
    /// 仅供 GM 按钮调用，一次完成当前一张普通明信片；当前页已完成或无效时按配置顺序选择未完成页。
    /// 沿用全部解锁的存档语义，不扣星、不发奖励；保存失败时恢复本次修改前的状态。
    /// </summary>
    private void CompleteCurrentPostcard(CustomButton btn)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        PostcardPopup postcardPopup = UIManager.Instance.GetUI<PostcardPopup>();
        if (postcardPopup != null && postcardPopup.IsOpen && !postcardPopup.CanApplyGmUnlock)
        {
            ShowCurrentPostcardUnlockResult("请等待明信片动画或引导结束");
            return;
        }

        var postcardData = DB.Postcard;
        var postcardConfigs = ConfigManager.Instance?.Tables?.TbPostcardConfig?.DataList;
        if (postcardData == null || postcardConfigs == null)
        {
            Debug.LogError("[GMPanel] Complete current postcard failed. Postcard data or config is unavailable.");
            ShowCurrentPostcardUnlockResult("配置或存档未初始化");
            return;
        }

        Config.PostcardConfig targetConfig = null;
        for (int i = 0; i < postcardConfigs.Count; i++)
        {
            Config.PostcardConfig postcardConfig = postcardConfigs[i];
            if (!IsCompletablePostcardConfig(postcardConfig) ||
                postcardData.CompletedId?.Contains(postcardConfig.Id) == true)
            {
                continue;
            }

            if (string.Equals(postcardConfig.Id, postcardData.CurrentId, StringComparison.Ordinal))
            {
                targetConfig = postcardConfig;
                break;
            }

            if (targetConfig == null ||
                postcardConfig.Sort < targetConfig.Sort ||
                (postcardConfig.Sort == targetConfig.Sort &&
                 string.Compare(postcardConfig.Id, targetConfig.Id, StringComparison.Ordinal) < 0))
            {
                targetConfig = postcardConfig;
            }
        }

        if (targetConfig == null)
        {
            ShowCurrentPostcardUnlockResult("全部完成或无可用配置");
            return;
        }

        List<string> originalCompletedIds = postcardData.CompletedId;
        List<int> originalCollectedPuzzleIds = postcardData.CurrentCollectedPuzzledId;
        string originalCurrentId = postcardData.CurrentId;
        bool originalHasGuide = postcardData.HasGuide;
        bool originalIsGuiding = postcardData.IsGuiding;

        var completedIds = originalCompletedIds != null
            ? new List<string>(originalCompletedIds)
            : new List<string>();
        completedIds.Add(targetConfig.Id);
        postcardData.CompletedId = completedIds;
        postcardData.CurrentCollectedPuzzledId = new List<int>();
        postcardData.CurrentId = targetConfig.Id;
        postcardData.HasGuide = true;
        postcardData.IsGuiding = false;

        if (!DB.SavePostcard())
        {
            postcardData.CompletedId = originalCompletedIds;
            postcardData.CurrentCollectedPuzzledId = originalCollectedPuzzleIds;
            postcardData.CurrentId = originalCurrentId;
            postcardData.HasGuide = originalHasGuide;
            postcardData.IsGuiding = originalIsGuiding;
            Debug.LogWarning("[GMPanel] Complete current postcard failed because SavePostcard returned false.");
            ShowCurrentPostcardUnlockResult("保存失败，已恢复进度");
            return;
        }

        RedDotManager.Refresh(RedDotKey.Postcard);
        bool hasOpenPopup = postcardPopup != null && postcardPopup.IsOpen;
        bool refreshed = hasOpenPopup && postcardPopup.RefreshAfterGmUnlock(targetConfig.Id);
        Debug.Log($"[GMPanel] Complete current postcard complete. id:{targetConfig.Id}");
        ShowCurrentPostcardUnlockResult(hasOpenPopup && !refreshed
            ? $"已解锁 {targetConfig.Id}，请重开明信片"
            : $"已解锁 {targetConfig.Id}");
    }

    /// <summary>
    /// 在原按钮的显式绑定文本上保留本次操作结果，同时沿用系统提示；仅供单张 GM 解锁调用。
    /// </summary>
    private void ShowCurrentPostcardUnlockResult(string message)
    {
        if (UnlockCurrentPostcardStatusTxt != null)
            UnlockCurrentPostcardStatusTxt.text = $"解锁当前明信片\n{message}";

        SystemTips.Show(message);
    }

    private static bool IsCompletablePostcardConfig(Config.PostcardConfig postcardConfig)
    {
        if (postcardConfig == null || string.IsNullOrWhiteSpace(postcardConfig.Id))
        {
            return false;
        }

        bool isUnlockConfig =
            (postcardConfig.UnlockProgress == null || postcardConfig.UnlockProgress.Count <= 0) &&
            (postcardConfig.Rewards == null || postcardConfig.Rewards.Count <= 0) &&
            postcardConfig.RewardMultiple <= 0;
        return !isUnlockConfig;
    }

    public override void OnShow()
    {
        base.OnShow();
        RefreshInterstitialControllerBinding(true);
    }

    private void ShowMaxDebug(CustomButton btn)
    {
        GameSDKService.Instance.ShowMaxDebugger();
    }


    private void TryToRestorePurchase(CustomButton btn)
    {
#if UNITY_EDITOR
        // Editor 只测试平台资产回调之后的补发链；正式平台仍从 SDK 请求恢复订单。
        PurchaseManager purchaseManager = GameManager.Instance?.Purchase;
        if (purchaseManager == null)
        {
            Debug.LogWarning("GM GiftPack1 restore test skipped: PurchaseManager is unavailable.");
            return;
        }

        purchaseManager.DebugAddRestoredGiftPack1();
        purchaseManager.SyncRestore();
#else
        GameManager.Instance.Purchase.Restore();
#endif
    }
    private void TryToPurchase5Tips(CustomButton btn)
    {
        GameManager.Instance.Bag.AddTool(Config.Game.GameProps.Tips, 5, RewardType.Other);
    }

    private void ShowWaitingPopup(CustomButton btn)
    {
        WaitingPopup.Create();
    }

    private void DeleteAllData(CustomButton btn)
    {
        DBManager.Instance.ClearPlayerPrefsCache();
        DBManager.Instance.ClearJsonCache();

        Application.Quit();
    }

    /// <summary>
    /// SwitchBtn 点击后切换 GM 调试内容，并把同一状态同步到中台 DebugMode。
    /// </summary>
    private void OnSwitchBtnClick(CustomButton btn)
    {
        bool nextVisible = !_isView1Visible;
        SetView1Visible(nextVisible);
        GuruSettings.Instance.DebugMode.Set(nextVisible);
    }

    /// <summary>
    /// 提供 bool 用于切换 View1 是否展示
    /// </summary>
    public void SetView1Visible(bool isShow)
    {
        _isView1Visible = isShow;

        if (View1 != null)
        {
            View1.gameObject.SetActive(isShow);
        }

        if (View2 != null)
        {
            View2.gameObject.SetActive(isShow);
        }

        if (pages != null)
        {
            pages.gameObject.SetActive(isShow);
        }

        if (rtConfigObj != null)
        {
            rtConfigObj.gameObject.SetActive(isShow);
        }
        RefreshSwitchText();
    }

    private void ResolvePagesReference()
    {
        if (pages == null)
        {
            pages = GetComponentInChildren<PageView>(true);
        }
    }

    private bool GetInitialViewVisibility()
    {
        if (View1 != null)
        {
            return View1.gameObject.activeSelf;
        }

        if (View2 != null)
        {
            return View2.gameObject.activeSelf;
        }

        if (pages != null)
        {
            return pages.gameObject.activeSelf;
        }

        return false;
    }

    private void RefreshSwitchText()
    {
        if (SwitchTxt != null)
        {
            SwitchTxt.text = "GM: " + _isView1Visible;
        }
    }

    private static void BindButton(CustomButton button, CustomButtonClick handler)
    {
        if (button != null)
        {
            button.OnClick += handler;
        }
    }

    private static void UnbindButton(CustomButton button, CustomButtonClick handler)
    {
        if (button != null)
        {
            button.OnClick -= handler;
        }
    }

    private void OnInterstitialConfigSwitchClick(CustomButton btn)
    {
        SetInterstitialConfigVisible(!_isInterstitialConfigVisible);
    }

    private void SetInterstitialConfigVisible(bool isShow)
    {
        _isInterstitialConfigVisible =
            isShow && TryGetActiveInterstitialContext(out _, out _);
        RefreshInterstitialConfigSwitchText();
    }

    private void RefreshInterstitialConfigSwitchText()
    {
        if (InterstitialConfigTxt != null)
        {
            if (TryGetActiveInterstitialContext(
                    out IPlayController activeController,
                    out _))
            {
                InterstitialConfigTxt.text =
                    $"插屏信息[{activeController.Mode}]: {_isInterstitialConfigVisible}";
            }
            else
            {
                InterstitialConfigTxt.text = "插屏信息: false (未进入玩法)";
            }
        }
    }

    /// <summary>
    /// 同步 GM 插屏数据显示所绑定的玩法控制器。
    /// Controller 首次生效或发生切换时默认展示对应玩法数据；同一 Controller 下保留手动开关结果。
    /// </summary>
    /// <param name="forceRefresh">
    /// 是否强制按当前 Controller 重置显示状态；GMPanel 每次显示时传 true。
    /// </param>
    private void RefreshInterstitialControllerBinding(bool forceRefresh = false)
    {
        TryGetActiveInterstitialContext(
            out IPlayController activeController,
            out _);

        if (!forceRefresh &&
            ReferenceEquals(_interstitialDisplayController, activeController))
        {
            return;
        }

        _interstitialDisplayController = activeController;
        _isInterstitialConfigVisible = activeController != null;
        RefreshInterstitialConfigSwitchText();
    }

    /// <summary>
    /// 获取已经完成初始化并处于激活状态的玩法插屏上下文。
    /// 未进入玩法、Controller 正在切换或玩法未提供插屏 Policy 时返回 false。
    /// </summary>
    private static bool TryGetActiveInterstitialContext(
        out IPlayController activeController,
        out IInterstitialAdPolicy policy)
    {
        activeController =
            GameManager.Instance?.ControllerManager?.ActiveController;
        policy =
            (activeController as IInterstitialAdPolicyHost)?.InterstitialAdPolicy;

        if (activeController != null &&
            activeController.IsInitialized &&
            activeController.IsActive &&
            policy != null)
        {
            return true;
        }

        activeController = null;
        policy = null;
        return false;
    }

    private void TestForRewardAd(CustomButton btn)
    {
        Debug.Log($"[GMPanel AdTest] ReawrdAD is Ready {GameSDKService.Instance.IsRewardAdReady} ");
        Debug.Log($"[GMPanel AdTest] SDK is Ready {GameSDKService.Instance.IsInitialized} ");

        RewardAdHelper.TryToShowRewardAd(Config.Game.RewardPlacement.GameTest.ToString(), success: "U_HurryTips_RewardVideo_succeed", onResult: (placement, isSuc) =>
        {
            // GameSDKService.Instance.LoadRewardAd();
        });
    }

    private void TestForInterstitialAd(CustomButton btn)
    {
        Debug.Log($"[GMPanel AdTest] Interstitial is Ready {GameSDKService.Instance.IsInterstitialAdReady} ");
        // Debug.Log($"[GMPanel AdTest] SDK is Ready {GameSDKService.Instance.IsInitialized} ");

        GameSDKService.Instance.ShowInterstitial(Config.Game.InterstitialPlacement.GameTest.ToString(), (placement, isSuc) =>
        {
            Debug.Log($"[GMPanel AdTest] placement: {placement}, isSuc: {isSuc}");
            if (isSuc)
            {
                IPlayController activeController =
                    GameManager.Instance?.ControllerManager?.ActiveController;
                IInterstitialAdPolicy policy =
                    (activeController as IInterstitialAdPolicyHost)?.InterstitialAdPolicy;
                policy?.OnInterstitialShown(Config.Game.InterstitialPlacement.GameTest);
            }

            // GameSDKService.Instance.LoadInterstitialAd();
        });
    }

    private void EnterLevelLoadingPanel(CustomButton btn)
    {
        EnterLevelLoading.Create();
    }

    public static GMPanel Create()
    {
        var panel = UIManager.Instance.OpenUI<GMPanel>($"{typeof(GMPanel).Name}", UILayer.System);
        return panel;
    }


    const float margin = 20f;
    const float width = 730f;
    const float height = 60f;
    private void OnGUI()
    {
        if (!Application.isPlaying)
            return;

        if (DebugModeSwitchAllBtns.IsDebugModeActive)
        {
            if (!DebugModeSwitchAllBtns.isShow)
                return;
        }

        RefreshInterstitialControllerBinding();
        if (_isInterstitialConfigVisible &&
            TryGetActiveInterstitialContext(
                out IPlayController activeController,
                out IInterstitialAdPolicy policy))
        {
            ShowInterstitialData(activeController, policy);
            if (activeController.Mode == PlayModeType.Main)
                ShowTrackData();
        }


        if (!_isView1Visible)
        {
            return;
        }

        var rect = new Rect(margin, margin, width, height);
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 30,
            normal = { textColor = Color.white }
        };
        GUI.Label(rect, $"当前语言: {DB.GameSetting.CurrentLanguageKeyCode}", style);


        // UDID Button
        var udid = SystemInfo.deviceUniqueIdentifier;
        var buttonRect = new Rect(margin, margin + height + 10f, width, height);

        var skin = GUI.skin.button;
        skin.fontSize = 25;
        // if (GUI.Button(buttonRect, $"设备ID: {udid}", skin))
        // {
        //     GUIUtility.systemCopyBuffer = udid;
        //     Debug.Log($"[GMPanel] DevID copied: {udid}");
        // }

        buttonRect = new Rect(margin, margin + height + 10f, width / 2f, height);
        string uid = Guru.SDK.Framework.Core.Account.AccountDataStore.Instance.Uid;
        if (GUI.Button(buttonRect, $"用户ID: {uid}", skin))
        {
            GUIUtility.systemCopyBuffer = uid;
            Debug.Log($"[GMPanel] UDID copied: {uid}");
        }

        buttonRect = new Rect(margin, margin + height * 2f + 10f, width / 2f, height);
        GUI.Label(buttonRect, $"SDK版本: {SdkInfo.Version}", skin);

        var starCount = GameManager.Instance?.Bag?.GetToolCount(Config.Game.GameProps.Star) ?? 0;
        buttonRect = new Rect(margin, margin + height * 3f + 10f, width / 2f, height);
        GUI.Label(buttonRect, $"Bag Star数量: {starCount}", skin);

        var dailyChallengeCurrentProcess = DB.GameSchedule?.DailyChallengeCurrentProcess ?? 0;
        buttonRect = new Rect(margin, margin + height * 4f + 10f, width / 2f, height);
        GUI.Label(buttonRect, $"每日挑战进度：{dailyChallengeCurrentProcess}", skin);

    }

    private void ShowInterstitialData(
        IPlayController activeController,
        IInterstitialAdPolicy policy)
    {
        if (!(activeController is IInterstitialAdPolicyHost policyHost))
            return;

        if (!TryGetInterstitialDisplayData(
                activeController.Mode,
                policyHost.InterstitialLevel,
                out int failCount,
                out int successCount,
                out int interval))
        {
            return;
        }

        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 30,
            normal = { textColor = Color.blue }
        };

        var rowGap = 10f;
        var rowHeight = height + rowGap;
        var bottomY = Screen.height - margin - rowHeight * 5f;
        var failCountRect = new Rect(margin, bottomY, width * 2f, height);
        GUI.Label(
            failCountRect,
            $"插屏记录失败次数[{activeController.Mode}]: {failCount}",
            style);

        var lastDateRect = new Rect(margin, bottomY + rowHeight, width * 2f, height);
        var interstitialCd = policy.CurrentInterstitialCD;
        GUI.Label(lastDateRect, $"插屏记录CD: {interstitialCd}", style);

        var triggerCountRect = new Rect(margin, bottomY + rowHeight * 2f, width * 2f, height);
        var interstitialIntervalDiff = interval - successCount;
        GUI.Label(triggerCountRect,
            $"插屏间隔差值: {interstitialIntervalDiff}  (Remote 间隔 - 成功关卡数)",
            style);
    }

    /// <summary>
    /// 按当前玩法读取完全隔离的插屏调试数据。
    /// 注意事项：主线、Draw、IWanner 分别读取各自 DB 与按当前关卡区间解析后的间隔配置。
    /// </summary>
    private static bool TryGetInterstitialDisplayData(
        PlayModeType mode,
        int currentLevel,
        out int failCount,
        out int successCount,
        out int interval)
    {
        failCount = 0;
        successCount = 0;
        interval = 0;

        switch (mode)
        {
            case PlayModeType.Main when DB.GameData != null:
                failCount = DB.GameData.InterstitialFailCount;
                successCount = DB.GameData.InterstitialLevelSuccessCount;
                interval =
                    ConfigManager.RemoteConfig.GetAdInterstitialShowIntervalCount(currentLevel);
                return true;

            case PlayModeType.Draw when DB.DrawGame != null:
                failCount = DB.DrawGame.InterstitialFailCount;
                successCount = DB.DrawGame.InterstitialLevelSuccessCount;
                interval =
                    ConfigManager.RemoteConfig.GetDrawGameInterstitialShowIntervalCount(currentLevel);
                return true;

            case PlayModeType.IWanner when DB.IWannerGame != null:
                failCount = DB.IWannerGame.InterstitialFailCount;
                successCount = DB.IWannerGame.InterstitialLevelSuccessCount;
                interval =
                    ConfigManager.RemoteConfig.GetIWannerGameInterstitialShowIntervalCount(currentLevel);
                return true;

            default:
                return false;
        }
    }

    private void ShowTrackData()
    {
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 30,
            normal = { textColor = Color.blue }
        };
        var rowGap = 10f;
        var rowHeight = height + rowGap;
        var bottomY = Screen.height - margin - rowHeight * 5f;
        var levelState = PlayCtrl.Instance != null && PlayCtrl.Instance.Level != null
            ? PlayCtrl.Instance.Level.CurrentLevelState
            : null;
        var levelPath = levelState != null ? levelState.LevelPath : string.Empty;
        var currentAttemptSeconds = levelState != null ? levelState.CurrentLevelTimeSeconds : 0f;

        if (string.IsNullOrEmpty(levelPath))
            return;

        DB.GameData.LevelPlayDuration.TryGetValue(levelPath, out var levelDuration);

        var currentAttemptRect = new Rect(margin, bottomY + rowHeight * 3f, width * 2f, height);
        GUI.Label(currentAttemptRect, $"当次时长: {currentAttemptSeconds:F2}", style);

        var levelDurationRect = new Rect(margin, bottomY + rowHeight * 4f, width * 3f, height);
        GUI.Label(levelDurationRect, $"总时长（毫秒）[{levelPath}]: {levelDuration}", style);
    }

    private static string FormatInterstitialDate(long unixSeconds)
    {
        if (unixSeconds <= 0)
        {
            return "N/A (0)";
        }

        try
        {
            var time = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).ToLocalTime();
            return $"{time:yyyy-MM-dd HH:mm:ss} ({unixSeconds})";
        }
        catch (ArgumentOutOfRangeException)
        {
            return $"Invalid ({unixSeconds})";
        }
    }

}
