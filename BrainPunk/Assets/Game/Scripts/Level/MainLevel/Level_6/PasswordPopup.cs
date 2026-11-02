using System;
using System.Text;
using System.Threading.Tasks;
using Bear.EventSystem;
using Bear.UI;
using Cysharp.Threading.Tasks;
using Game.Events;
using Game.Play;
using Game.Scripts.Common;
using I2.Loc;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;

/// <summary>
/// 密码解锁设计关卡专用界面
/// </summary>
public partial class PasswordPopup : BaseUIView, IEventSender
{
    [SerializeField] private string CorrectPassword = "1234";
    [SerializeField] private bool ShowInputNumber = true;


    [SerializeField] private MMF_Player errorFeedback;

    private const int MaxLength = 4;
    private readonly StringBuilder inputBuffer = new StringBuilder(MaxLength);
    private bool _destroyDirectlyOnClose;
    private int _closeVersion;

    public Action OnPopupCloseCallback;
    public bool CancelPendingSuccessOnClose { get; set; }

    public override void OnCreate()
    {
        BindButtons();
        errorFeedback.Events.OnComplete.RemoveAllListeners();
        errorFeedback.Events.OnComplete.AddListener(OnFeedbackComplete);
    }

    public override void OnOpen()
    {
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

    public void AddInput(int value)
    {
        AudioManager.PlaySound("pressPassword");
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
            PlaySuccess().Forget();
            return;
        }

        AudioManager.PlaySound("passwordError");
        errorFeedback?.PlayFeedbacks();
        // ResetInput();
    }

    private async UniTaskVoid PlaySuccess()
    {
        int successCloseVersion = _closeVersion;
        AudioManager.PlaySound("passwordCorrect");
        SystemTips.Show(LocalizationManager.GetTranslation("S_HurryTips_Des_level_06"));
        await UniTask.WaitForSeconds(1f);

        if (CancelPendingSuccessOnClose && successCloseVersion != _closeVersion)
            return;

        this.DispatchEvent(Witness<OnTiggerItemEvent>._, 1);
        Close(null);
    }

    private void OnFeedbackComplete()
    {
        ResetInput();
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
            text.text = "-";
        }
    }

    private void Close(CustomButton btn)
    {
        _closeVersion++;
        // this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PLAYING);
        if (_destroyDirectlyOnClose)
        {
            OnDestroyView();
            Destroy(gameObject);

            OnPopupCloseCallback?.Invoke();

            return;
        }

        
        OnPopupCloseCallback?.Invoke();
        UIManager.Instance.DestroyUI(this);
    }

    public static PasswordPopup Create(string popupName = "PasswordPopup")
    {
        var panel = UIManager.Instance.OpenUI<PasswordPopup>(popupName, UILayer.Popup);
        // panel.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.PAUSE);
        return panel;
    }

    public static PasswordPopup Create(string popupName, Transform parent)
    {
        if (parent == null)
            return Create(popupName);

        GameObject prefab = Resources.Load<GameObject>(popupName);
        if (prefab == null)
        {
            Debug.LogWarning($"[PasswordPopup] Failed to load popup prefab: {popupName}");
            return null;
        }

        GameObject instance = Instantiate(prefab, parent, false);
        PasswordPopup panel = instance.GetComponent<PasswordPopup>();
        if (panel == null)
        {
            Debug.LogWarning($"[PasswordPopup] Prefab {popupName} does not contain PasswordPopup.");
            Destroy(instance);
            return null;
        }

        panel._destroyDirectlyOnClose = true;
        panel.transform.SetAsLastSibling();
        panel.OnCreate();
        panel.OnOpen();
        panel.OnShow();
        return panel;
    }
}
