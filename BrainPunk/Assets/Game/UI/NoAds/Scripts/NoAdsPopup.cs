using System;
using Bear.EventSystem;
using Bear.Logger;
using Bear.UI;
using DG.Tweening;
using GameCommon;
using UnityEngine;
using static Products;

public partial class NoAdsPopup : BaseUIView, IDebuger, IEventSender
{
    [SerializeField] private UISpineCtrl spineCtrl;
    [SerializeField] private CommonYellowBtn buyBtn;

    private string lastUIName = "";
    private const string NoAdProductName = ProductName.NewnoAds;
    private bool _isPurchasing;
    private bool _hasClosed;

    public override void OnCreate()
    {
        base.OnCreate();

        buyBtn.Content.text = GameManager.Instance.Purchase.GetPrice(NoAdProductName);
        buyBtn.Btn.OnClick += BuyNoAd;
        CloseBtn.OnClick += ClosePanel;
    }

    public override void OnOpen()
    {
        base.OnOpen();
        _isPurchasing = false;
        _hasClosed = false;
        SetButtonsInteractable(true);

        if (spineCtrl)
        {
            spineCtrl.PlayAnimation("in", false).Complete += (track) =>
            {
                spineCtrl.PlayAnimation("idle", true);
            };
        }
        
        ContentTxt.alpha = 0;
        ContentTxt.DOFade(1, 0.5f).SetDelay(.5f);
    }

    public void Show()
    {
        GameSDKService.Instance?.IAP_IMP(lastUIName);
    }

    private void BuyNoAd(CustomButton btn)
    {
        if (_isPurchasing)
        {
            this.LogWarning("[NoAdsPopup] Buy ignored. purchase already in progress.");
            return;
        }

        var purchaseManager = GameManager.Instance?.Purchase;
        if (purchaseManager == null)
        {
            this.LogError("[NoAdsPopup] Buy failed. PurchaseManager is null.");
            return;
        }

        var bag = GameManager.Instance?.Bag;
        if ((bag != null && bag.GetToolCount(Config.Game.GameProps.NoAds) > 0) ||
            purchaseManager.IsPurchased(NoAdProductName) ||
            purchaseManager.IsPurchased(ProductName.NoAds) ||
            purchaseManager.IsPurchased(ProductName.NoadBundle) ||
            purchaseManager.IsPurchased(ProductName.NewnoadBundle))
        {
            this.LogWarning("[NoAdsPopup] Buy ignored. NoAds already purchased.");
            ClosePopup();
            return;
        }

        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            purchaseManager.OnPurchaseFailed();
            return;
        }

        var data = Products.GetProductId(NoAdProductName);
        if (data == null)
        {
            this.LogError("[NoAdsPopup] Buy failed. product data is null.");
            purchaseManager.OnPurchaseFailed();
            return;
        }

        _isPurchasing = true;
        SetButtonsInteractable(false);
        GameSDKService.Instance?.IAP_CLK(data.Sku);
        purchaseManager.Purchase(NoAdProductName, purchaseCallback: OnPurchaseCallback);
    }

    private void OnPurchaseCallback(string productName, bool isSuc)
    {
        _isPurchasing = false;
        SetButtonsInteractable(true);

        var purchaseManager = GameManager.Instance?.Purchase;
        if (purchaseManager == null)
        {
            this.LogError($"[NoAdsPopup] Purchase callback failed. PurchaseManager is null. productName:{productName}, isSuc:{isSuc}");
            return;
        }

        if (isSuc)
        {
            purchaseManager.OnPurchaseSuccess(productName);
            ClosePopup();
        }
        else
        {
            purchaseManager.OnPurchaseFailed();
        }
    }

    private void ClosePanel(CustomButton btn)
    {
        if (_isPurchasing)
        {
            this.LogWarning("[NoAdsPopup] Close ignored. purchase in progress.");
            return;
        }

        ClosePopup();
    }

    public static NoAdsPopup Create(string lastUIName = "ChoiceLevelPanel")
    {
        var panel = UIManager.Instance.OpenUI<NoAdsPopup>($"{typeof(NoAdsPopup).Name}", UILayer.Popup);
        panel.lastUIName = lastUIName;
        panel.Show();
        return panel;
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (buyBtn?.Btn != null)
        {
            buyBtn.Btn.Interactable = interactable;
        }

        if (CloseBtn != null)
        {
            CloseBtn.Interactable = interactable;
        }
    }

    private void ClosePopup()
    {
        if (_hasClosed)
        {
            return;
        }

        _hasClosed = true;
        if (this != null && IsOpen)
        {
            UIManager.Instance.DestroyUI(this);
        }

        GameSDKService.Instance?.IAP_CLOSE(lastUIName);
    }
}
