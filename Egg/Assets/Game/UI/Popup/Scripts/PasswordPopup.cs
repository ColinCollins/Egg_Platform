using System.Text;
using Bear.EventSystem;
using Bear.UI;
using Game.Events;
using Game.Play;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;

/// <summary>
/// level_6 专用界面
/// </summary>
public partial class PasswordPopup : BaseUIView, IEventSender
{
    [SerializeField] private string CorrectPassword = "1234";
    [SerializeField] private bool ShowInputNumber = true;


    [SerializeField] private MMF_Player errorFeedback;

    private const int MaxLength = 4;
    private readonly StringBuilder inputBuffer = new StringBuilder(MaxLength);

    public override void OnOpen()
    {
        BindButtons();
        ResetInput();
    }

    private void BindButtons()
    {
        Input1Btn.OnClick += _ => AddInput(1);
        Input2Btn.OnClick += _ => AddInput(2);
        Input3Btn.OnClick += _ => AddInput(3);
        Input4Btn.OnClick += _ => AddInput(4);
        Input5Btn.OnClick += _ => AddInput(5);
        Input6Btn.OnClick += _ => AddInput(6);
        Input7Btn.OnClick += _ => AddInput(7);
        Input8Btn.OnClick += _ => AddInput(8);
        Input9Btn.OnClick += _ => AddInput(9);
        CloseBtn.OnClick += Close;
    }

    private void AddInput(int value)
    {
        if (inputBuffer.Length >= MaxLength)
            return;

        inputBuffer.Append(value);
        RefreshView();

        if (inputBuffer.Length >= MaxLength)
        {
            CheckPassword();
        }
    }

    private void CheckPassword()
    {
        if (inputBuffer.ToString() == CorrectPassword)
        {
            this.DispatchEvent(Witness<OnTiggerItemEvent>._, 1);
            Close(null);
            return;
        }

        errorFeedback?.PlayFeedbacks();
        // ResetInput();
    }

    public void ResetInput()
    {
        inputBuffer.Clear();
        RefreshView();
    }

    private void RefreshView()
    {
        SetText(Num1Txt, 0);
        SetText(Num2Txt, 1);
        SetText(Num3Txt, 2);
        SetText(Num4Txt, 3);
    }

    private void SetText(TextMeshProUGUI text, int index)
    {
        if (text == null)
            return;

        if (index < inputBuffer.Length)
        {
            text.text = ShowInputNumber ? inputBuffer[index].ToString() : "*";
        }
        else
        {
            text.text = string.Empty;
        }
    }

    private void Close(CustomButton btn)
    {
        this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PLAYING);
        UIManager.Instance.CloseUI(this);
    }

    public static PasswordPopup Create()
    {
        var panel = UIManager.Instance.OpenUI<PasswordPopup>($"{typeof(PasswordPopup).Name}", UILayer.Popup);
        panel.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PAUSE);
        return panel;
    }
}
