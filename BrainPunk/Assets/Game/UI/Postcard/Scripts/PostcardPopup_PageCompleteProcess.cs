using Bear.Logger;
using Bear.UI;
using Cysharp.Threading.Tasks;
using Config.Game;
using Game.ConfigModule;
using GameCommon;
using UnityEngine;
using System.Collections.Generic;

public partial class PostcardPopup
{
    #region Page Complete Process

    private const float ChestOpenAnimMinTimeout = 1f;
    private const float ChestOpenAnimTimeoutPadding = 1f;
    private const float CompleteEffectsPostDelay = 2f;

    /// <summary>
    /// 用途：控制整页完成流程中通用步骤之间的缓冲时间，便于在 Inspector 调整领奖前后节奏。
    /// 调用边界：仅供 Page Complete 内部流程使用。
    /// 注意事项：小于 0 时按 0 秒处理；缺少 Chest 开启动画时也会复用该等待作为降级窗口。
    /// </summary>
    [SerializeField] private float pageCompleteStepDelay = 0.5f;

    /// <summary>
    /// 成功完成 page 的时候触发
    /// 调用边界：由当前明信片页的灰度填充达到 1 时触发，仅播放已绑定的完成粒子。
    /// 注意事项：粒子发射后固定等待 2 秒再放行后续页面转换；循环粒子仍会在后台播放一个自身 duration 后停止发射，列表为空或包含空引用时安全跳过。
    /// </summary>
    [SerializeField] private List<ParticleSystem> completeEffects;

    private bool _isHandlingPageComplete;
    private Postcard_Chest _pageCompleteChestView;
    private readonly HashSet<string> _grantedPageCompleteRewardIds = new();

    /// <summary>
    /// 用途：当前明信片页完成后的回调入口，负责进入整页完成表现与领奖流程。
    /// 调用边界：由 Postcard_PageItem 在当前页全部 item 解锁完成后调用。
    /// 注意事项：当前实现会串联页面完成变形、宝箱开启动画、领奖弹窗、皮肤转换弹窗和可选 ShopPanel 等待；流程结束后保留当前页，不自动切页，也不直接新增存档字段。
    /// </summary>
    public void OnPageComplete()
    {
        HandlePageCompleteAsync().Forget();
    }

    /// <summary>
    /// 用途：在最后一个进度 item 稳定后、整页完成流程启动前显示 Chest，并刷新奖励与满进度。
    /// 调用边界：仅由当前 Postcard_PageItem 确认本次解锁完成整页后调用。
    /// 注意事项：只准备并缓存 Chest，不播放 chestOpen、不发奖、不写存档；后续整页完成流程复用该实例。
    /// </summary>
    internal void PreparePageCompleteChest()
    {
        _pageCompleteChestView = PrepareChestForPageComplete(GetVisibleConfig());
    }

    /// <summary>
    /// 用途：在当前页 grayImage 完成序列结束后触发完成粒子表现。
    /// 调用边界：仅由当前 Popup 持有的 Postcard_PageItem 在填充和回弹全部结束后调用。
    /// 注意事项：粒子发射后固定等待 2 秒再返回，但不等待完整生命周期；Chest 已在整页完成入口前完成准备。
    /// </summary>
    internal async UniTask PlayCompleteEffects()
    {
        if (completeEffects == null || completeEffects.Count == 0)
            return;

        bool hasPlayedEffect = false;
        for (int i = 0; i < completeEffects.Count; i++)
        {
            ParticleSystem completeEffect = completeEffects[i];
            if (completeEffect == null)
                continue;

            hasPlayedEffect = true;
            completeEffect.Play(true);
            if (completeEffect.main.loop)
                StopLoopingCompleteEffectAsync(completeEffect).Forget();
        }

        if (!hasPlayedEffect)
            return;

        await UniTask.WaitForSeconds(
                CompleteEffectsPostDelay,
                cancellationToken: this.GetCancellationTokenOnDestroy())
            .SuppressCancellationThrow();
    }

    private async UniTask StopLoopingCompleteEffectAsync(ParticleSystem completeEffect)
    {
        if (completeEffect == null)
            return;

        var destroyToken = this.GetCancellationTokenOnDestroy();
        float duration = Mathf.Max(0f, completeEffect.main.duration);
        bool isCanceled = await UniTask.WaitForSeconds(
                duration,
                cancellationToken: destroyToken)
            .SuppressCancellationThrow();
        if (isCanceled || this == null || completeEffect == null)
            return;

        completeEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    /// <summary>
    /// 用途：按固定顺序处理整页完成后的完整 UI 流程。
    /// 调用边界：仅由 OnPageComplete() 启动；通过 _isHandlingPageComplete 防止同一页重复进入。
    /// 注意事项：所有等待都跟随当前 Popup 销毁取消，取消后不会继续隐藏 Chest、打开弹窗或播放后续表现。
    /// </summary>
    private async UniTaskVoid HandlePageCompleteAsync()
    {
        if (_isHandlingPageComplete)
            return;

        _isHandlingPageComplete = true;
        SetPagePlayingAnimationState(true);
        Config.PostcardConfig completedConfig = GetVisibleConfig();
        Config.Skin skinReward = ResolveSkinReward(completedConfig);
        bool hasRewardConfig = HasRewardConfig(completedConfig);
        bool normalRewardGranted = !hasRewardConfig
                                   || TryGrantPageCompleteNormalRewards(completedConfig);
        RefreshButtonStates();

        try
        {
            RefreshCompletedPage();
            if (_currentPage != null && !await _currentPage.PlayCompleteTransformAsync())
                return;

            Postcard_Chest chestView = _pageCompleteChestView;
            if (chestView == null)
                chestView = PrepareChestForPageComplete(completedConfig);

            Spine.TrackEntry chestOpenEntry = chestView?.PlayChestOpenAnim();
            if (!await WaitChestOpenAnimAsync(chestOpenEntry))
                return;

            chestView?.Hide(true);

            if (!await WaitPageCompleteStepAsync())
                return;

            if (hasRewardConfig)
            {
                if (!await WaitRewardPopupCloseAsync(completedConfig, normalRewardGranted))
                    return;
            }

            if (!await WaitSkinTransformPopupCloseAsync(skinReward))
                return;

            if (skinReward != null && !await WaitShopPanelCloseIfOpenedAsync())
                return;
        }
        finally
        {
            if (this != null)
            {
                _isHandlingPageComplete = false;
                SetPagePlayingAnimationState(false);
                _pageCompleteChestView = null;
                RefreshButtonStates();

                // 页面完成链结束后通知 Flow2 进入 Stage Three 收尾。
                if (_postcardGuideFlow2?.IsRunning == true)
                    _postcardGuideFlow2.NotifyPageComplete();
            }
        }
    }

    /// <summary>
    /// 用途：为首次完成过渡准备 Chest，绕开普通浏览中“已完成页隐藏 Chest”的规则。
    /// 调用边界：正常路径由 PreparePageCompleteChest 在整页完成入口前调用；缓存缺失时由 HandlePageCompleteAsync 在 transform 后降级调用。
    /// 注意事项：强制显示当前 Chest 并把进度刷新到满值，但不播放 chestOpen、不发奖、不写存档，也不改变历史完成页的常规隐藏行为。
    /// </summary>
    private Postcard_Chest PrepareChestForPageComplete(Config.PostcardConfig completedConfig)
    {
        Postcard_Chest chestView = ResolveChest();
        if (chestView == null)
            return null;

        chestView.Show();
        chestView.RefreshItems(BuildChestRewards(completedConfig));

        int maxProgress = GetChestMaxProgress(completedConfig);
        chestView.RefreshProgress(maxProgress, maxProgress, false);
        return chestView;
    }

    /// <summary>
    /// 用途：等待 Chest 开启动画稳定后继续领奖流程。
    /// 调用边界：由整页完成流程在 PlayChestOpenAnim() 后调用。
    /// 注意事项：没有有效 TrackEntry 时降级为 pageCompleteStepDelay 等待；有效动画优先等待 Complete/End，结束后立即继续，超过动画时长兜底窗口后继续流程；返回 false 表示 Popup 已销毁或等待被取消。
    /// </summary>
    /// <param name="chestOpenEntry">Chest 开启动画返回的 Spine TrackEntry，可为空。</param>
    private async UniTask<bool> WaitChestOpenAnimAsync(Spine.TrackEntry chestOpenEntry)
    {
        if (chestOpenEntry == null)
            return await WaitPageCompleteStepAsync();

        UniTaskCompletionSource completeSource = new UniTaskCompletionSource();
        void OnChestOpenComplete(Spine.TrackEntry _)
        {
            completeSource.TrySetResult();
        }

        void OnChestOpenEnd(Spine.TrackEntry _)
        {
            completeSource.TrySetResult();
        }

        chestOpenEntry.Complete += OnChestOpenComplete;
        chestOpenEntry.End += OnChestOpenEnd;

        var destroyToken = this.GetCancellationTokenOnDestroy();
        float animationDuration = chestOpenEntry.Animation?.Duration ?? 0f;
        float timeout = Mathf.Max(
            ChestOpenAnimMinTimeout,
            animationDuration + ChestOpenAnimTimeoutPadding);
        bool didTimeoutOrCancel = await completeSource.Task
            .AttachExternalCancellation(destroyToken)
            .TimeoutWithoutException(
                System.TimeSpan.FromSeconds(timeout),
                DelayType.UnscaledDeltaTime);

        chestOpenEntry.Complete -= OnChestOpenComplete;
        chestOpenEntry.End -= OnChestOpenEnd;
        completeSource.TrySetResult();

        if (destroyToken.IsCancellationRequested || this == null)
            return false;

        if (didTimeoutOrCancel)
        {
            this.LogWarning($"[PostcardPopup] Chest open animation wait timed out after {timeout:F2}s; continuing page complete flow.");
            return true;
        }

        return true;
    }

    /// <summary>
    /// 用途：提供整页完成流程中两个步骤之间的固定缓冲时间。
    /// 调用边界：仅供 Page Complete 内部流程使用。
    /// 注意事项：返回 false 表示等待期间 Popup 被销毁，后续流程必须停止。
    /// </summary>
    private async UniTask<bool> WaitPageCompleteStepAsync()
    {
        float stepDelay = Mathf.Max(0f, pageCompleteStepDelay);
        if (stepDelay <= 0f)
            return true;

        bool isCanceled = await UniTask.WaitForSeconds(stepDelay, cancellationToken: this.GetCancellationTokenOnDestroy())
            .SuppressCancellationThrow();
        return !isCanceled;
    }

    /// <summary>
    /// 用途：打开当前明信片配置对应的通用奖励展示弹窗，并等待弹窗关闭。
    /// 调用边界：仅当当前 PostcardConfig 存在 Rewards 时由整页完成流程调用。
    /// 注意事项：normal 奖励已前置发放时关闭弹窗内重复发奖；前置发放失败时保留弹窗默认发奖作为降级。弹窗创建失败时视为可继续。
    /// </summary>
    /// <param name="postcardConfig">当前完成页的配置，用于 CommonGetRewardPopup 展示奖励。</param>
    /// <param name="normalRewardGranted">当前完成页的 normal 奖励是否已经在动画前成功发放。</param>
    private async UniTask<bool> WaitRewardPopupCloseAsync(
        Config.PostcardConfig postcardConfig,
        bool normalRewardGranted)
    {
        _refreshCoinAfterRewardPopup = true;
        UniTaskCompletionSource closeSource = new UniTaskCompletionSource();
        CommonGetRewardPopup popup = CommonGetRewardPopup.Create(
            postcardConfig,
            () => closeSource.TrySetResult(),
            grantNormalReward: !normalRewardGranted);
        if (popup == null)
        {
            RefreshCoinAfterRewardPopupIfPending();
            return true;
        }

        if (_isGuiding)
        {
            RectTransform rewardButtonTarget = popup.BtnGroup != null
                ? popup.BtnGroup
                : null;
            GuideFinger guideFinger = GuideFinger.Create(rewardButtonTarget, popup.transform as RectTransform, () =>
            {
                popup.OnNormalBtnClick(null);
            });

            if (guideFinger == null)
            {
                this.LogWarning("[PostcardPopup] Reward guide finger could not be created; restoring normal reward popup interaction.");
                if (_postcardGuideFlow2 == null || !_postcardGuideFlow2.NotifyFailed())
                    SetPostcardGuideInteractionLocked(false);
            }
        }

        bool isCanceled = await closeSource.Task
            .AttachExternalCancellation(this.GetCancellationTokenOnDestroy())
            .SuppressCancellationThrow();
        if (!isCanceled && this != null && isActiveAndEnabled)
            RefreshCoinAfterRewardPopupIfPending();

        return !isCanceled;
    }

    /// <summary>
    /// 用途：在页面完成保存成功后立即发放当前明信片配置中的普通奖励，避免后续动画或弹窗中断造成奖励丢失。
    /// 调用边界：正常路径由 Postcard_PageItem 在 SavePostcard 成功后、启动解锁动画前调用；HandlePageCompleteAsync 在首个异步等待前再次调用作为兜底。
    /// 注意事项：仅处理 Coin、Star 和可解析的 Skin；依赖未就绪或配置无法处理时返回 false，由 CommonGetRewardPopup 保留默认发奖降级。
    /// </summary>
    /// <param name="postcardConfig">当前已完成明信片页的奖励配置。</param>
    /// <returns>true 表示奖励已处理或无需处理；false 表示应由奖励弹窗尝试默认发放。</returns>
    internal bool TryGrantPageCompleteNormalRewards(Config.PostcardConfig postcardConfig)
    {
        if (!HasRewardConfig(postcardConfig))
            return true;

        if (!string.IsNullOrEmpty(postcardConfig.Id)
            && _grantedPageCompleteRewardIds.Contains(postcardConfig.Id))
        {
            return true;
        }

        var bag = GameManager.Instance?.Bag;
        if (bag == null || DB.GameData == null)
        {
            this.LogWarning(
                $"[PostcardPopup] Page complete normal reward grant skipped because Bag or GameData is unavailable. postcardId:{postcardConfig.Id}");
            return false;
        }

        Config.Skin skinReward = ResolveSkinReward(postcardConfig);
        var quantityRewards = new Dictionary<GameProps, int>();
        foreach (KeyValuePair<GameProps, Config.ParamBase> reward in postcardConfig.Rewards)
        {
            if (reward.Key != GameProps.Coin && reward.Key != GameProps.Star)
                continue;

            if (!BaseParamUtils.TryGetInt(reward.Value, out int count) || count <= 0)
                continue;

            quantityRewards[reward.Key] = count;
        }

        if (skinReward != null)
            bag.GetNewSkin(skinReward.Id, GetSkinWayType.Reward);

        bag.GrantRewards(quantityRewards, RewardType.Other);
        bool hasSupportedReward = skinReward != null || quantityRewards.Count > 0;
        if (!hasSupportedReward)
        {
            this.LogWarning(
                $"[PostcardPopup] Page complete reward config has no supported normal reward. postcardId:{postcardConfig.Id}");
            return false;
        }

        if (!string.IsNullOrEmpty(postcardConfig.Id))
            _grantedPageCompleteRewardIds.Add(postcardConfig.Id);

        return true;
    }

    /// <summary>
    /// 用途：在 Chest 开启动画前，把当前明信片页刷新为已完成视觉状态。
    /// 调用边界：仅由整页完成流程在 PlayCompleteTransformAsync() 前调用。
    /// 注意事项：只刷新当前页 UI，不重复发奖、不写存档；完成视觉和 transform 均先于 Chest 开启。
    /// </summary>
    private void RefreshCompletedPage()
    {
        if (this == null || _currentPage == null)
            return;

        _currentPage.Refresh();
    }

    /// <summary>
    /// 用途：当领奖配置中存在有效皮肤奖励时，打开皮肤转换弹窗并等待关闭。
    /// 调用边界：由整页完成流程在通用领奖弹窗关闭后调用。
    /// 注意事项：skinReward 为空或弹窗创建失败时直接继续；返回 false 表示等待期间 Popup 被销毁。
    /// </summary>
    /// <param name="skinReward">从当前 PostcardConfig.Rewards 中解析出的皮肤配置，可为空。</param>
    private async UniTask<bool> WaitSkinTransformPopupCloseAsync(Config.Skin skinReward)
    {
        if (skinReward == null)
            return true;

        UniTaskCompletionSource closeSource = new UniTaskCompletionSource();
        GetSkinTransformPopup popup = GetSkinTransformPopup.Create(skinReward, () => closeSource.TrySetResult());
        if (popup == null)
            return true;

        bool isCanceled = await closeSource.Task
            .AttachExternalCancellation(this.GetCancellationTokenOnDestroy())
            .SuppressCancellationThrow();
        return !isCanceled;
    }

    /// <summary>
    /// 用途：皮肤转换弹窗可能跳转打开 ShopPanel，本方法在检测到 ShopPanel 时等待其关闭。
    /// 调用边界：仅当本次完成页存在有效皮肤奖励时调用。
    /// 注意事项：先让出一帧等待 ShopPanel 创建；返回 false 表示 Popup 等待期间被销毁。
    /// </summary>
    private async UniTask<bool> WaitShopPanelCloseIfOpenedAsync()
    {
        bool isCanceled = await UniTask.DelayFrame(1, cancellationToken: this.GetCancellationTokenOnDestroy())
            .SuppressCancellationThrow();
        if (isCanceled)
            return false;

        while (UIManager.Instance?.GetUI<ShopPanel>() != null)
        {
            isCanceled = await UniTask.DelayFrame(1, cancellationToken: this.GetCancellationTokenOnDestroy())
                .SuppressCancellationThrow();
            if (isCanceled)
                return false;
        }

        return true;
    }

    /// <summary>
    /// 用途：从当前明信片奖励配置中解析 Skin 奖励并映射到皮肤配置表。
    /// 调用边界：由前置发奖入口解析授予目标，也由整页完成流程在首个异步等待前解析后续皮肤转换展示数据。
    /// 注意事项：不授予皮肤、不写存档；解析失败、配置缺失或皮肤 id 无效时返回 null。
    /// </summary>
    /// <param name="postcardConfig">当前完成页的明信片配置。</param>
    private static Config.Skin ResolveSkinReward(Config.PostcardConfig postcardConfig)
    {
        if (postcardConfig?.Rewards == null ||
            !postcardConfig.Rewards.TryGetValue(GameProps.Skin, out var rewardValue))
        {
            return null;
        }

        if (!BaseParamUtils.TryGetString(rewardValue, out string skinId) ||
            string.IsNullOrEmpty(skinId))
        {
            return null;
        }

        return ConfigManager.Instance?.Tables?.TbSkin?.GetOrDefault(skinId);
    }

    /// <summary>
    /// 用途：判断当前明信片配置是否存在任何奖励项。
    /// 调用边界：由整页完成流程决定是否需要打开 CommonGetRewardPopup。
    /// 注意事项：只判断 Rewards 集合是否存在和非空，不校验奖励内容是否可发放。
    /// </summary>
    /// <param name="postcardConfig">当前完成页的明信片配置。</param>
    private static bool HasRewardConfig(Config.PostcardConfig postcardConfig)
    {
        return postcardConfig?.Rewards != null && postcardConfig.Rewards.Count > 0;
    }

    #endregion
}
