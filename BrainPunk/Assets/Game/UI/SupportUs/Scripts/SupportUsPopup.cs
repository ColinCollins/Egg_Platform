using Bear.UI;
using Game;
using I2.Loc;

/// <summary>
/// 支持我们弹窗：展示累计激励广告观看次数，并提供主动观看激励广告的入口。
/// </summary>
public partial class SupportUsPopup : BaseUIView
{
    private const string DescriptionLocalizationKey = "U_SupportUs_Des_02";
    private const string RewardAdPlacement = "SupportUs";

    private static readonly string[] RewardAdSuccessTipLocalizationKeys =
    {
        "U_SupportUs_Thanks_Des_01",
        "U_SupportUs_Thanks_Des_02",
        "U_SupportUs_Thanks_Des_03",
        "U_SupportUs_Thanks_Des_04",
        "U_SupportUs_Thanks_Des_05",
        "U_SupportUs_Thanks_Des_06",
        "U_SupportUs_Thanks_Des_07",
        "U_SupportUs_Thanks_Des_08",
    };

    private bool _isRewardAdRequesting;
    private bool _isClosing;

    #region 生命周期

    public override void OnOpen()
    {
        base.OnOpen();
        _isRewardAdRequesting = false;
        _isClosing = false;
        BindButtons();
        SetButtonsInteractable(true);
        RefreshDescriptionText();
    }

    public override void OnCreate()
    {
        base.OnCreate();
    }

    public override void OnClose()
    {
        UnbindButtons();
        _isRewardAdRequesting = false;
        _isClosing = false;
        base.OnClose();
    }

    #endregion

    #region 按钮事件

    private void BindButtons()
    {
        UnbindButtons();

        if (UiCommonX01Btn != null)
            UiCommonX01Btn.OnClick += OnCloseBtnClick;

        if (UICommonYellow01Btn != null)
            UICommonYellow01Btn.OnClick += OnRewardAdBtnClick;
    }

    private void UnbindButtons()
    {
        if (UiCommonX01Btn != null)
            UiCommonX01Btn.OnClick -= OnCloseBtnClick;

        if (UICommonYellow01Btn != null)
            UICommonYellow01Btn.OnClick -= OnRewardAdBtnClick;
    }

    private void OnCloseBtnClick(CustomButton _)
    {
        if (_isRewardAdRequesting || _isClosing)
            return;

        _isClosing = true;
        SetButtonsInteractable(false);
        UIManager.Instance.CloseAndDestroyUI(this);
    }

    private void OnRewardAdBtnClick(CustomButton _)
    {
        if (_isRewardAdRequesting || _isClosing)
            return;

        _isRewardAdRequesting = true;
        SetButtonsInteractable(false);
        RewardAdHelper.TryToShowRewardAd(
            RewardAdPlacement,
            success: GetRandomRewardAdSuccessTipKey(),
            onResult: OnRewardAdResult);
    }

    private static string GetRandomRewardAdSuccessTipKey()
    {
        int index = UnityEngine.Random.Range(0, RewardAdSuccessTipLocalizationKeys.Length);
        return RewardAdSuccessTipLocalizationKeys[index];
    }

    private void OnRewardAdResult(string _, bool __)
    {
        _isRewardAdRequesting = false;

        if (this == null || !isActiveAndEnabled)
            return;

        SetButtonsInteractable(true);
        RefreshDescriptionText();
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (UiCommonX01Btn != null)
            UiCommonX01Btn.Interactable = interactable;

        if (UICommonYellow01Btn != null)
            UICommonYellow01Btn.Interactable = interactable;
    }

    #endregion

    #region 文本刷新

    private void RefreshDescriptionText()
    {
        if (USupportusDes02Txt == null)
            return;

        int rewardAdCount = DB.GameData?.RewardADCount ?? 0;
        string descriptionFormat = LocalizationManager.GetTranslation(DescriptionLocalizationKey);
        if (string.IsNullOrEmpty(descriptionFormat))
        {
            USupportusDes02Txt.text = rewardAdCount.ToString();
            return;
        }

        USupportusDes02Txt.text = string.Format(
            descriptionFormat,
            rewardAdCount);
    }

    #endregion

    /// <summary>
    /// 打开支持我们弹窗；按钮监听与累计观看次数会在 OnOpen 中初始化。
    /// 调用边界：弹窗已经打开时直接返回现有实例，不重复调用 UIManager.OpenUI 或再次执行 OnOpen。
    /// </summary>
    public static SupportUsPopup Create()
    {
        UIManager uiManager = UIManager.Instance;
        uiManager.Initialize();

        string popupPath = nameof(SupportUsPopup);
        SupportUsPopup existingPanel = uiManager.GetUI<SupportUsPopup>(popupPath);
        if (existingPanel != null && existingPanel.IsOpen)
            return existingPanel;

        var panel = uiManager.OpenUI<SupportUsPopup>(popupPath, UILayer.Popup);
        return panel;
    }
}
