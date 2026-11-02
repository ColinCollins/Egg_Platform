using System;
using System.Collections.Generic;
using System.Threading;
using Bear.EventSystem;
using Bear.Logger;
using Bear.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game;
using Game.ConfigModule;
using Game.Events;
using UnityEngine;
#if UNITY_IOS
using Guru.UniKit.Apple.GameKit;
using R3;
#endif

public partial class StartPanel : BaseUIView, IDebuger, IEventSender
{
    private static readonly int EnterAnimatorStateHash = Animator.StringToHash("in");
    private static readonly int IdleAnimatorStateHash = Animator.StringToHash("idle");

    [SerializeField] private CustomButton drawGameBtn;

    /// <summary>
    /// 用途：让 main 节点的大范围点击区域复用 PlayBtn 的主线进入逻辑。
    /// 调用边界：仅在 StartPanel 创建时绑定、销毁时解绑。
    /// 注意事项：必须绑定 main/clickArea 的 CustomButton，不能改为重复实现进入流程。
    /// </summary>
    [SerializeField] private CustomButton mainClickAreaBtn;

    /// <summary>
    /// 用途：提供 StartPanel 根节点进场动画的真实完成状态。
    /// 调用边界：只在当前 StartPanel 实例首次创建时读取和关闭，不在再次显示时重播。
    /// </summary>
    [SerializeField] private Animator rootAnimator;

    /// <summary>
    /// 用途：在 StartPanel 首次进场动画期间拦截所有按钮输入。
    /// 调用边界：必须绑定 Prefab 现有 ClickBlock；只在动画完成后解除。
    /// </summary>
    [SerializeField] private GameObject clickBlock;

    /// <summary>
    /// 用途：刷新主页 DrawGame 入口进度。
    /// 调用边界：仅由 StartPanel 打开流程调用；必须在 Prefab Inspector 显式绑定。
    /// </summary>
    [Header("主页子入口")]
    [SerializeField] private DrawGameEnter drawGameEnter;

    /// <summary>
    /// 用途：刷新主页 IWannerGame 入口进度。
    /// 调用边界：仅由 StartPanel 打开流程调用；必须在 Prefab Inspector 显式绑定。
    /// </summary>
    [SerializeField] private IWannerGameEnter iwannerGameEnter;

    /// <summary>Dig 独立首页入口；Prefab 显式绑定，打开首页刷新有效配置与通关进度。</summary>
    [SerializeField] private DigGameEnter digGameEnter;

    /// <summary>
    /// 用途：刷新主页明信片入口状态。
    /// 调用边界：仅由 StartPanel 打开流程调用；必须在 Prefab Inspector 显式绑定。
    /// </summary>
    [SerializeField] private PostcardEnter postcardEnter;

    /// <summary>
    /// 用途：刷新主页 ChestGift 入口，并提供启动礼包关闭动画的收回目标。
    /// 调用边界：仅由 StartPanel 打开与启动礼包配置流程读取；必须在 Prefab Inspector 显式绑定。
    /// </summary>
    [SerializeField] private DailyChestGiftEnter dailyChestGiftEnter;

    /// <summary>
    /// 提供当前主页 ChestGift 入口中心，仅供启动礼包本次关闭收回动画使用。
    /// 调用边界：复用 Inspector 绑定的 DailyChestGiftEnter，不负责打开礼包、资格判定或入口显隐。
    /// </summary>
    public RectTransform ChestGiftReturnTarget => dailyChestGiftEnter != null
        ? dailyChestGiftEnter.transform as RectTransform
        : null;

    private EventSubscriber noAdSubscriber;
    private bool isAchievementProcessing;
    private UniTaskCompletionSource enterAnimationCompletionSource;
    private bool isEnterAnimationCompleted;
#if UNITY_IOS && !UNITY_EDITOR
    private IDisposable achievementAuthStateSubscription;
#endif

    /// <summary>免广告入口按钮，复用 StartPanel 自动生成的现有序列化绑定。</summary>
    private CustomButton NoAdBtn => UIZhuyeNoads01Btn;

    public override void OnOpen()
    {
        base.OnOpen();
        OneLinkManager.Instance.NotifyHomeOpened();
        RefreshMainLevelStarProgress();
        RefreshDrawGameEntryVisibility();
        SubscribeAchievementAuthState();
        RefreshAchievementButtonVisibility();
        RefreshNoAdButtonVisibility();
        AddNoAdListeners();

        RefreshHomeEntries();
    }

    private void RefreshHomeEntries()
    {
        drawGameEnter?.Refresh();
        iwannerGameEnter?.Refresh();
        digGameEnter?.Refresh();
        postcardEnter?.Refresh();
        dailyChestGiftEnter?.RefreshForHome();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || !IsOpen || !IsVisible || !isEnterAnimationCompleted)
            return;

        OneLinkManager.Instance.TryNavigateFromStartPanel(this);
    }

    /// <summary>Draw 云控提交后刷新已打开首页的入口开关与星数，不重播首页动画或其它启动流程。</summary>
    internal void RefreshDrawRemoteConfig()
    {
        if (!Application.isPlaying) return;
        if (!IsOpen)
        {
            Guru.SDK.Framework.Utils.Log.Log.I("[DrawRemoteConfigUI] stage=home_skipped, reason=panel_closed.");
            return;
        }
        RefreshDrawGameEntryVisibility();
        drawGameEnter?.Refresh();
        Guru.SDK.Framework.Utils.Log.Log.I($"[DrawRemoteConfigUI] stage=home_refreshed, entryPresent={drawGameEnter != null}, entryActive={drawGameEnter != null && drawGameEnter.gameObject.activeSelf}.");
    }

    /// <summary>Dig 云控提交后仅刷新已打开首页的 Dig 开关和进度，不重播首页动画。</summary>
    internal void RefreshDigRemoteConfig()
    {
        if (!Application.isPlaying || !IsOpen) return;
        digGameEnter?.Refresh();
    }

    #region Main Level Star Progress

    private void RefreshMainLevelStarProgress()
    {
        LevelCtrl levelCtrl = PlayCtrl.Instance?.Level;
        List<Config.LevelSort> levelSorts = levelCtrl?.LevelSorts;
        if (levelCtrl == null || levelSorts == null)
        {
            SetMainLevelStarProgress(0, 0);
            this.LogWarning("[StartPanel] Main level data is not ready; star progress falls back to 0/0.");
            return;
        }

        Dictionary<string, List<int>> collectedStars = DB.Postcard?.LevelCollectedStar;
        var countedLevelIds = new HashSet<string>();
        int collectedStarCount = 0;
        int totalStarCount = 0;

        for (int i = 0; i < levelSorts.Count; i++)
        {
            Config.LevelSort sortData = levelSorts[i];
            Config.LevelData levelData = sortData == null
                ? null
                : levelCtrl.GetLevelDataById(sortData.Path);
            int levelTotalStarCount = levelData == null
                ? 0
                : Mathf.Clamp(levelData.LevelSumStarCount, 0, 3);
            if (levelTotalStarCount <= 0 ||
                string.IsNullOrEmpty(levelData.Id) ||
                !countedLevelIds.Add(levelData.Id))
            {
                continue;
            }

            totalStarCount += levelTotalStarCount;
            if (collectedStars == null ||
                !collectedStars.TryGetValue(levelData.Id, out List<int> levelCollectedStars) ||
                levelCollectedStars == null)
            {
                continue;
            }

            int levelCollectedStarCount = 0;
            for (int starId = 1; starId <= 3; starId++)
            {
                if (levelCollectedStars.Contains(starId))
                    levelCollectedStarCount++;
            }

            collectedStarCount += Mathf.Min(levelCollectedStarCount, levelTotalStarCount);
        }

        SetMainLevelStarProgress(collectedStarCount, totalStarCount);
    }

    private void SetMainLevelStarProgress(int collectedStarCount, int totalStarCount)
    {
        if (UiZhuye01Txt != null)
            UiZhuye01Txt.text = collectedStarCount.ToString();

        if (UiZhuye02Txt != null)
            UiZhuye02Txt.text = $"/{totalStarCount}";
    }

    #endregion

    public override void OnCreate()
    {
        base.OnCreate();
        enterAnimationCompletionSource = new UniTaskCompletionSource();
        isEnterAnimationCompleted = false;
        SetClickBlocked(true);
#if DEBUG_MODE
        this.Log("------ gmbtn1");
        GMPanel.Create();        
#endif
        PlayBtn.OnClick += OnClickPlay;
        if (mainClickAreaBtn != null)
        {
            mainClickAreaBtn.OnClick -= OnClickPlay;
            mainClickAreaBtn.OnClick += OnClickPlay;
        }
        else
        {
            this.LogWarning("[StartPanel] Main click area button is missing.");
        }

        ChoiceLevelBtn.OnClick += OnShowChoiceLevel;
        BindButtonGroupButtons();
        BindNoAdButton();

        if (drawGameBtn != null)
            drawGameBtn.OnClick += OnClickDrawLevel;
        else
            this.LogWarning("[StartPanel] Draw entry button is missing.");

        // TestBtn.OnClick += TestTips;

        PlayBtn.transform.localScale = Vector3.zero;
        ChoiceLevelBtn.transform.localScale = Vector3.zero;
        Tweener playButtonTween = PlayBtn.transform
            .DOScale(Vector3.one, 0.4f)
            .SetDelay(1f)
            .SetEase(Ease.OutBack, 3f);
        Tweener choiceLevelButtonTween = ChoiceLevelBtn.transform
            .DOScale(Vector3.one, 0.4f)
            .SetDelay(1f)
            .SetEase(Ease.OutBack, 3f);

        CompleteEnterAnimationAsync(playButtonTween, choiceLevelButtonTween).Forget();

/*         var entry = spineCtrl.PlayAnimation("loading", false);
        entry.Complete += (track) =>
        {
            spineCtrl.PlayAnimation("loading01", true);
        }; */
    }

    #region Enter Animation Lifecycle

    /// <summary>
    /// 等待当前 StartPanel 实例的首次进场动画完成。
    /// 调用边界：供启动弹窗序列等待首页可交互边界；不会主动创建或重播 StartPanel。
    /// 注意事项：调用方必须传入自身生命周期取消令牌，避免应用退出时保留等待。
    /// </summary>
    public UniTask WaitForEnterAnimationAsync(CancellationToken cancellationToken)
    {
        if (isEnterAnimationCompleted || enterAnimationCompletionSource == null)
            return UniTask.CompletedTask;

        return enterAnimationCompletionSource.Task.AttachExternalCancellation(cancellationToken);
    }

    private async UniTask CompleteEnterAnimationAsync(Tweener playButtonTween, Tweener choiceLevelButtonTween)
    {
        CancellationToken cancellationToken = this.GetCancellationTokenOnDestroy();
        try
        {
            await UniTask.WhenAll(
                DisableRootAnimatorAfterEnterAsync(cancellationToken),
                WaitForTweenCompletionAsync(playButtonTween, cancellationToken),
                WaitForTweenCompletionAsync(choiceLevelButtonTween, cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            this.LogError($"[StartPanel] Enter animation wait failed. error:{exception}");
        }

        if (this == null || cancellationToken.IsCancellationRequested)
            return;

        isEnterAnimationCompleted = true;
        SetClickBlocked(false);
        enterAnimationCompletionSource?.TrySetResult();
    }

    private static async UniTask WaitForTweenCompletionAsync(Tween tween, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested &&
               tween != null &&
               tween.IsActive() &&
               !tween.IsComplete())
        {
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }
    }

    /// <summary>
    /// 等待 StartPanel_Root 的进场状态完整结束后关闭 Animator，避免其继续接管 main 的缩放。
    /// </summary>
    private async UniTask DisableRootAnimatorAfterEnterAsync(CancellationToken cancellationToken)
    {
        if (rootAnimator == null)
        {
            this.LogWarning("[StartPanel] StartPanel_Root Animator is missing.");
            return;
        }

        while (!cancellationToken.IsCancellationRequested && rootAnimator != null && rootAnimator.enabled)
        {
            if (!rootAnimator.IsInTransition(0))
            {
                AnimatorStateInfo stateInfo = rootAnimator.GetCurrentAnimatorStateInfo(0);
                bool hasEnteredIdle = stateInfo.shortNameHash == IdleAnimatorStateHash;
                bool hasFinishedEnter = stateInfo.shortNameHash == EnterAnimatorStateHash &&
                                        stateInfo.normalizedTime >= 1f;
                if (hasEnteredIdle || hasFinishedEnter)
                {
                    rootAnimator.enabled = false;
                    return;
                }
            }

            await UniTask.Yield(PlayerLoopTiming.Update);
        }
    }

    private void SetClickBlocked(bool isBlocked)
    {
        if (clickBlock != null && clickBlock.activeSelf != isBlocked)
            clickBlock.SetActive(isBlocked);
    }

    #endregion



    void OnDestroy()
    {
        enterAnimationCompletionSource?.TrySetResult();
        if (mainClickAreaBtn != null)
            mainClickAreaBtn.OnClick -= OnClickPlay;

        PlayBtn.transform.DOKill();
        ChoiceLevelBtn.transform.DOKill();
        UnbindButtonGroupButtons();
        UnbindNoAdButton();
        EventsUtils.ResetEvents(ref noAdSubscriber);
        UnsubscribeAchievementAuthState();
        if (drawGameBtn != null)
            drawGameBtn.OnClick -= OnClickDrawLevel;
    }

    /// <summary>
    /// 直接进入游戏
    /// </summary>
    /// <param name="btn"></param>
    private void OnClickPlay(CustomButton btn)
    {
        this.DispatchEvent(Witness<EnterLevelEvent>._, PlayCtrl.Instance.Level.CurrentLevelSort);

        WaitToClose().Forget();
    }

    /// <summary>
    /// OneLink 在首页空闲时提交已校验的主线排序项；复用原进关事件和首页延迟关闭流程。
    /// 不直接创建关卡，不改写关卡打点或通关状态。
    /// </summary>
    internal void EnterOneLinkMainLevel(Config.LevelSort level)
    {
        this.DispatchEvent(Witness<EnterLevelEvent>._, level);
        WaitToClose().Forget();
    }

    private async UniTask WaitToClose()
    {
        await UniTask.WaitForSeconds(1f);

        UIManager.Instance.CloseUI(this);
    }

    private void OnClickDrawLevel(CustomButton btn)
    {
        if (!ConfigManager.RemoteConfig.GetDrawGameIsOpen())
        {
            RefreshDrawGameEntryVisibility();
            return;
        }

        if (!GameManager.Instance.TryEnterDraw(out string error))
            this.LogError($"[StartPanel] Enter DrawPlayCtrl failed. error:{error}");
    }

    private void RefreshDrawGameEntryVisibility()
    {
        if (!Application.isPlaying || drawGameBtn == null)
            return;

        var drawGameEntry = drawGameBtn.transform.parent;
        if (drawGameEntry == null)
        {
            this.LogWarning("[StartPanel] Draw entry root is missing.");
            drawGameBtn.gameObject.SetActive(ConfigManager.RemoteConfig.GetDrawGameIsOpen());
            return;
        }

        drawGameEntry.gameObject.SetActive(ConfigManager.RemoteConfig.GetDrawGameIsOpen());
    }

    private void OnShowChoiceLevel(CustomButton btn)
    {
        ClickTransformPanel.Create(() =>
        {
            ChoiceLevelPanel.Create();
        });
    }

    private void ShowSetting(CustomButton btn)
    {
        GameSettingPopup.Create();
    }

    #region No Ads

    private void BindNoAdButton()
    {
        if (NoAdBtn == null)
        {
            this.LogWarning("[StartPanel] NoAd entry button is missing.");
            return;
        }

        NoAdBtn.OnClick -= ShowNoAd;
        NoAdBtn.OnClick += ShowNoAd;
    }

    private void UnbindNoAdButton()
    {
        if (NoAdBtn != null)
            NoAdBtn.OnClick -= ShowNoAd;
    }

    private void AddNoAdListeners()
    {
        EventsUtils.ResetEvents(ref noAdSubscriber);
        noAdSubscriber.Subscribe<UpdatePropEvent>(OnNoAdPropUpdate);
        noAdSubscriber.Subscribe<OnRestoreSuccessEvent>(OnRestoreSuccess);
    }

    private void OnNoAdPropUpdate(UpdatePropEvent evt)
    {
        if (evt.Prop != Config.Game.GameProps.NoAds)
            return;

        RefreshNoAdButtonVisibility();
    }

    /// <summary>恢复补齐永久权益后，刷新主页免广告与 ChestGift 入口。</summary>
    private void OnRestoreSuccess(OnRestoreSuccessEvent evt)
    {
        RefreshNoAdButtonVisibility();
        dailyChestGiftEnter?.Refresh();
    }

    private void RefreshNoAdButtonVisibility()
    {
        if (NoAdBtn == null)
            return;

        bool shouldShowNoAd = GameManager.Instance.Bag.GetToolCount(Config.Game.GameProps.NoAds) <= 0;
        NoAdBtn.transform.parent.gameObject.SetActive(shouldShowNoAd);
    }

    private void ShowNoAd(CustomButton btn)
    {
        NoAdsPopup.Create();
    }

    #endregion

    private void BindButtonGroupButtons()
    {
        if (SettingBtn != null)
        {
            SettingBtn.OnClick -= ShowSetting;
            SettingBtn.OnClick += ShowSetting;
        }

        if (ShopBtn != null)
        {
            ShopBtn.OnClick -= ShowShop;
            ShopBtn.OnClick += ShowShop;
        }

        if (GasBtn != null)
        {
            GasBtn.OnClick -= ShowAchievement;
            GasBtn.OnClick += ShowAchievement;
        }
    }

    private void UnbindButtonGroupButtons()
    {
        if (SettingBtn != null)
            SettingBtn.OnClick -= ShowSetting;

        if (ShopBtn != null)
            ShopBtn.OnClick -= ShowShop;

        if (GasBtn != null)
            GasBtn.OnClick -= ShowAchievement;
    }

    private void ShowShop(CustomButton btn)
    {
        ShopPanel.Create(false, GetType().Name);

        if (TransformPopup.HasTransformableTips())
            TransformPopup.Create();
    }

    private async void ShowAchievement(CustomButton btn)
    {
        if (isAchievementProcessing)
            return;

        try
        {
            isAchievementProcessing = true;

#if UNITY_IOS && !UNITY_EDITOR
            if (!AppleGameCenterAuthAgent.Instance.IsAuthenticated)
            {
                RefreshAchievementButtonVisibility();
                return;
            }
#endif

            if (!GameAccountUtils.IsGameLoggedIn)
            {
                if (!GameAccountUtils.IsGameLoginSupported())
                {
                    this.LogWarning("[StartPanel] Game login is not supported on this device.");
                    return;
                }

                bool loginSuccess = await GameAccountUtils.LoginWithGame(true);
                if (!loginSuccess)
                {
                    this.LogWarning("[StartPanel] Game login failed; achievements cannot be shown.");
                    return;
                }
            }

            GameAchievementUtils.ShowAchievementUI();
        }
        catch (Exception exception)
        {
            this.LogError($"[StartPanel] Show achievements failed. error:{exception}");
        }
        finally
        {
            isAchievementProcessing = false;
        }
    }

    private void RefreshAchievementButtonVisibility()
    {
        if (GasBtn == null)
            return;

        bool isVisible = ConfigManager.RemoteConfig.GetOpenGaps();
#if UNITY_IOS && !UNITY_EDITOR
        isVisible &= AppleGameCenterAuthAgent.Instance.IsAuthenticated;
#endif
        GasBtn.gameObject.SetActive(isVisible);
    }

    private void SubscribeAchievementAuthState()
    {
#if UNITY_IOS && !UNITY_EDITOR
        UnsubscribeAchievementAuthState();
        achievementAuthStateSubscription = AppleGameCenterAuthAgent.Instance.AuthStateUpdated
            .Where(state => state.OldState != null || state.NewState != null)
            .Subscribe(_ => RefreshAchievementButtonVisibility());
#endif
    }

    private void UnsubscribeAchievementAuthState()
    {
#if UNITY_IOS && !UNITY_EDITOR
        achievementAuthStateSubscription?.Dispose();
        achievementAuthStateSubscription = null;
#endif
    }


    private void TestTips(CustomButton btn)
    {
        SystemTips.Show(transform.parent, "test - luoweiming");
    }

    public override void OnClose()
    {
        base.OnClose();
        EventsUtils.ResetEvents(ref noAdSubscriber);
        UnsubscribeAchievementAuthState();
    }

    public static StartPanel Create()
    {
        var panel = UIManager.Instance.OpenUI<StartPanel>($"{typeof(StartPanel).Name}", UILayer.Normal);
        return panel;
    }
}
