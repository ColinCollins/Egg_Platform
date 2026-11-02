using System.Linq;
using Bear.EventSystem;
using Bear.UI;
using Config;
using Cysharp.Threading.Tasks;
using Game.ConfigModule;
using Game.Events;
using Game.Play;
using Game.Scripts.Common;
using GameCommon;
using UnityEngine;

/// <summary>
/// 商店奖励提示弹窗。
/// 根据 shopId 查询 TbShop 奖励配置，点击广告按钮观看激励视频后发放奖励并播放飞行动画。
/// </summary>
public partial class ShopRewardTipPopup : BaseUIView, IEventSender
{
    [SerializeField] private CustomButton adBtn;
    [SerializeField] private CustomButton closeBtn;
    [SerializeField] private CommonUIRewardAnimCtrl rewardAnimCtrl;
    [SerializeField] private string shopId;

    private bool _isRewardAdRequesting;
    private bool needResumeGame;
    private IPlayPauseHost _pauseHost;

    public override void OnCreate()
    {
        base.OnCreate();
        RefreshUI();
    }

    /// <summary>
    /// 每次弹窗打开时重新绑定按钮事件；用于兼容 UIManager 复用实例但不重复调用 OnCreate 的生命周期。
    /// </summary>
    public override void OnOpen()
    {
        base.OnOpen();
        closeBtn.OnClick -= OnCloseBtnClick;
        closeBtn.OnClick += OnCloseBtnClick;
        adBtn.OnClick -= OnAdBtnClick;
        adBtn.OnClick += OnAdBtnClick;
    }

    public override void OnClose()
    {
        IPlayPauseHost pauseHost = _pauseHost;
        _pauseHost = null;

        base.OnClose();
        closeBtn.OnClick -= OnCloseBtnClick;
        adBtn.OnClick -= OnAdBtnClick;

        if (needResumeGame)
        {
            if (pauseHost != null)
                pauseHost.Resume();
            else
                this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PLAYING);
        }
    }

    /// <summary>
    /// 根据 shopId 查询 TbShop 并刷新奖励数量文本。
    /// </summary>
    private void RefreshUI()
    {
        var shop = ConfigManager.Instance?.Tables?.TbShop?.GetOrDefault(shopId);
        if (shop == null || shop.Rewards == null || shop.Rewards.Count == 0)
        {
            return;
        }

        if (!BaseParamUtils.TryGetIntParam(shop.Rewards.First().Value, out int rewardValue))
        {
            return;
        }

        if (UiRewarded01Txt != null)
        {
            UiRewarded01Txt.text = string.Format("X{0}", rewardValue > 0 ? rewardValue.ToString() : string.Empty);
        }
    }

    /// <summary>
    /// 关闭按钮点击。
    /// </summary>
    private void OnCloseBtnClick(CustomButton btn)
    {
        UIManager.Instance.DestroyUI(this);
    }

    /// <summary>
    /// 广告按钮点击：请求播放激励视频。
    /// </summary>
    private void OnAdBtnClick(CustomButton btn)
    {
        if (_isRewardAdRequesting)
        {
            return;
        }

        _isRewardAdRequesting = true;
        RewardAdHelper.TryToShowRewardAd(
            Config.Game.RewardPlacement.ShopReward.ToString(),
            onResult: OnRewardAdCallback);
    }

    /// <summary>
    /// 激励视频回调：成功则发放奖励、播放动画并关闭弹窗。
    /// </summary>
    private void OnRewardAdCallback(string placement, bool isSuc)
    {
        _isRewardAdRequesting = false;
        if (!isSuc || this == null || !isActiveAndEnabled)
        {
            return;
        }

        GrantRewardAndPlayAnimAsync().Forget();
    }

    /// <summary>
    /// 异步发放奖励并播放收集动画，完成后关闭弹窗。
    /// </summary>
    private async UniTask GrantRewardAndPlayAnimAsync()
    {
        var shop = ConfigManager.Instance?.Tables?.TbShop?.GetOrDefault(shopId);
        if (shop == null)
        {
            UIManager.Instance.CloseUI(this);
            return;
        }

        // 发放奖励
        foreach (var reward in shop.Rewards)
        {
            if (!BaseParamUtils.TryGetIntParam(reward.Value, out int rewardCount))
            {
                continue;
            }

            GameManager.Instance.Bag.AddTool(reward.Key, rewardCount, RewardType.Ad);
        }

        AudioManager.PlaySound("shopGetReward");
        // 播放奖励飞行动画
        if (rewardAnimCtrl != null)
        {
            CommonUIRewardPlayResult result = await rewardAnimCtrl.Play();
            if (result != CommonUIRewardPlayResult.Completed)
            {
                Debug.LogWarning($"[ShopRewardTipPopup] Reward animation did not complete. result:{result}", this);
            }
        }
        else
        {
            Debug.LogWarning("[ShopRewardTipPopup] Reward animation skipped. controller is null.", this);
        }
        
        // 动画完成后关闭弹窗
        if (this != null && isActiveAndEnabled)
        {
            UIManager.Instance.DestroyUI(this);
        }
    }

    /// <summary>
    /// 创建弹窗并传入 shopId；游戏内调用可继续传递当前玩法的暂停恢复能力。
    /// </summary>
    /// <param name="shopId">用于读取奖励内容的商店配置 ID。</param>
    /// <param name="resumeGame">关闭弹窗后是否恢复玩法。</param>
    /// <param name="pauseHost">当前玩法的暂停恢复能力；为空时保持原主线事件恢复方式。</param>
    public static ShopRewardTipPopup Create(
        string shopId,
        bool resumeGame = false,
        IPlayPauseHost pauseHost = null)
    {
        var panel = UIManager.Instance.OpenUI<ShopRewardTipPopup>($"{typeof(ShopRewardTipPopup).Name}", UILayer.Popup);
        if (panel == null)
            return null;

        panel.shopId = shopId;
        panel.needResumeGame = resumeGame;
        panel._pauseHost = resumeGame ? pauseHost : null;
        return panel;
    }
}
