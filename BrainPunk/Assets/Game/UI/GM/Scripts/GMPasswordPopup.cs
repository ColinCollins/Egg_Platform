using Bear.UI;
using Game.Scripts.Common;
using TMPro;
using UnityEngine;

/// <summary>
/// GM 面板密码弹窗
/// </summary>
public partial class GMPasswordPopup : BaseUIView
{
    [SerializeField] private string CorrectPassword = "1234";
    [SerializeField] private TMP_InputField passwordInput;

    public override void OnCreate()
    {
        base.OnCreate();

        BindButtons();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        ResetInput();
    }

    private void BindButtons()
    {
        YesBtn.OnClick += OnClickConfirm;
        CloseBtn.OnClick += Close;
    }

    private void OnClickConfirm(CustomButton btn)
    {
        CheckPassword();
    }

    private void CheckPassword()
    {
        var input = passwordInput != null ? passwordInput.text : string.Empty;

        if (input == CorrectPassword)
        {
            PlaySuccess();
            return;
        }

        AudioManager.PlaySound("passwordError");
        ResetInput();
    }

    private void PlaySuccess()
    {
        AudioManager.PlaySound("passwordCorrect");
        GMPanel.Create();
        Close(null);
    }

    public void ResetInput()
    {
        if (passwordInput == null)
        {
            return;
        }

        passwordInput.text = string.Empty;
        passwordInput.ActivateInputField();
    }

    private void Close(CustomButton btn)
    {
        UIManager.Instance.CloseUI(this);
    }

    public static GMPasswordPopup Create()
    {
        var panel = UIManager.Instance.OpenUI<GMPasswordPopup>(nameof(GMPasswordPopup), UILayer.Popup);
        return panel;
    }
}

