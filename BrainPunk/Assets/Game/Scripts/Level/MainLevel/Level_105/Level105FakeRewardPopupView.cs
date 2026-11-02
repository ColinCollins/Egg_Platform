using Bear.UI;
using UnityEngine;

/// <summary>
/// Level 105 专属 100 金币纯展示页。组件只操作真实 CommonGetRewardPopup Variant 的展示节点，
/// 不调用领奖、广告、背包或余额修改业务；所有 CustomButton 仅保留按下/抬起反馈。
/// </summary>
public sealed class Level105FakeRewardPopupView : BaseUIView
{
    #region Inspector

    [SerializeField] private int coinAmount = 100;
    [SerializeField] private Sprite coinSprite;

    #endregion

    #region Runtime State

    private CommonRewardItem rewardItem;

    #endregion

    #region UIView Lifecycle

    public override void OnCreate()
    {
        base.OnCreate();
        ResolveReferences();
        BlockBusinessButtons();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        ResolveReferences();
        BlockBusinessButtons();
        PreparePresentation();
    }

    #endregion

    #region Public API

    /// <summary>仅刷新本地奖励数量文本，不授予或扣除任何真实道具。</summary>
    public void SetCoinAmount(int amount)
    {
        coinAmount = Mathf.Max(1, amount);
        if (rewardItem != null)
            rewardItem.Refresh(coinSprite, coinAmount);
    }

    #endregion

    #region Presentation and Lookup

    private void ResolveReferences()
    {
        if (rewardItem == null)
        {
            CommonRewardItem[] items = GetComponentsInChildren<CommonRewardItem>(true);
            if (items.Length > 0)
                rewardItem = items[0];
        }
    }

    private void PreparePresentation()
    {
        Transform adButton = FindDescendant("ad_btn");
        if (adButton != null)
            adButton.gameObject.SetActive(false);

        Transform clickBlock = FindDescendant("ClickBlock");
        if (clickBlock != null)
            clickBlock.gameObject.SetActive(false);

        if (rewardItem == null)
            return;

        rewardItem.gameObject.SetActive(true);
        rewardItem.Refresh(coinSprite, Mathf.Max(1, coinAmount));
    }

    private void BlockBusinessButtons()
    {
        CustomButton[] buttons = GetComponentsInChildren<CustomButton>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null)
                buttons[i].IsBlockClick = true;
        }
    }

    private Transform FindDescendant(string objectName)
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name == objectName)
                return candidate;
        }

        return null;
    }

    #endregion
}
