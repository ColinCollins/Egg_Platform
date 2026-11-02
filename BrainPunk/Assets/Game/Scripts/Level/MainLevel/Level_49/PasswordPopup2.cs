using DG.Tweening;
using System.Collections.Generic;
using System.Text;
using Bear.EventSystem;
using Bear.UI;
using Cysharp.Threading.Tasks;
using Game.Events;
using Game.Scripts.Common;
using I2.Loc;
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// level_50 dedicated password popup.
/// 给 screws complete 回调绑定 check，如果 screws 的状态全部为 complete，那么就播放开壳动画。
/// frontpart 向上移动并通过 CanvasGroup 渐隐
/// 
/// toggleSwitch 绑定功能，点击后 light 和 lock 都打开。然后过 2s 自动关闭 DestroyUI，并发送 OnTiggerItemEvent id = 1,
/// </summary>
public partial class PasswordPopup2 : BaseUIView, IEventSender
{
    [SerializeField] private bool ShowInputNumber = true;
    [SerializeField] private MMF_Player errorFeedback;
    [SerializeField] private List<Level50ScrewItem> screws;
    [SerializeField] private CanvasGroup frontPart;
    [SerializeField] private float frontPartMoveOffsetY = 120f;
    [SerializeField] private float frontPartMoveDuration = 0.35f;
    [SerializeField] private float frontPartFadeDuration = 0.2f;
    [SerializeField] private Ease frontPartMoveEase = Ease.OutCubic;
    [SerializeField] private Ease frontPartFadeEase = Ease.OutQuad;

    private const int MaxLength = 4;
    private readonly StringBuilder inputBuffer = new StringBuilder(MaxLength);
    private bool _isProcessingSuccess;
    private bool _isOpeningFrontPart;
    private bool _isToggleSwitchTriggered;
    private RectTransform _frontPartRectTransform;
    private Vector2 _frontPartInitialAnchoredPosition;
    private float _frontPartInitialAlpha = 1f;
    private Sequence _frontPartSequence;

    public override void OnCreate()
    {
        BindButtons();
        BindToggles();
        EnsureFrontPartReference();
        CacheFrontPartState();
        BindScrewCallbacks();

        if (errorFeedback != null)
        {
            errorFeedback.Events.OnComplete.RemoveAllListeners();
            errorFeedback.Events.OnComplete.AddListener(OnFeedbackComplete);
        }
    }

    public override void OnOpen()
    {
        _isProcessingSuccess = false;
        _isOpeningFrontPart = false;
        _isToggleSwitchTriggered = false;
        ResetInput();
        ResetScrews();
        ResetFrontPart();
        ResetToggles();
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

    private void BindToggles()
    {
        if (SpPasswordPopup2Switch01Toggle == null)
            return;

        SpPasswordPopup2Switch01Toggle.onValueChanged.RemoveListener(OnSwitchToggleValueChanged);
        SpPasswordPopup2Switch01Toggle.onValueChanged.AddListener(OnSwitchToggleValueChanged);
    }

    private void AddInput(int value)
    {
        if (_isProcessingSuccess)
            return;

        AudioManager.PlaySound("pressPassword");
        if (inputBuffer.Length >= MaxLength)
            return;

        inputBuffer.Append(value);
        RefreshView();

        if (inputBuffer.Length >= MaxLength)
            CheckPassword();
    }

    private void CheckPassword()
    {
        AudioManager.PlaySound("passwordError");
        errorFeedback?.PlayFeedbacks();
    }

    private void BindScrewCallbacks()
    {
        if (screws == null)
            return;

        for (int i = 0; i < screws.Count; i++)
        {
            if (screws[i] == null)
                continue;

            screws[i].OnPlayFinished -= OnScrewFinished;
            screws[i].OnPlayFinished += OnScrewFinished;
        }
    }

    private void OnScrewFinished(Level50ScrewItem _)
    {
        if (_isOpeningFrontPart || !AreAllScrewsFinished())
            return;

        PlaySuccess().Forget();
    }

    private bool AreAllScrewsFinished()
    {
        if (screws == null || screws.Count == 0)
            return false;

        for (int i = 0; i < screws.Count; i++)
        {
            if (screws[i] != null && !screws[i].Finished)
                return false;
        }

        return true;
    }

    private void ResetScrews()
    {
        if (screws == null)
            return;

        for (int i = 0; i < screws.Count; i++)
        {
            if (screws[i] == null)
                continue;

            screws[i].ResetVisual();
        }
    }

    private void ResetToggles()
    {
        SpPasswordPopup2Light01Toggle?.SetIsOnWithoutNotify(false);
        SpPasswordPopup2Lock01Toggle?.SetIsOnWithoutNotify(false);
        SpPasswordPopup2Switch01Toggle?.SetIsOnWithoutNotify(false);
    }

    private void EnsureFrontPartReference()
    {
        if (frontPart == null)
        {
            var groups = GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i] != null && groups[i].name == "frontPart")
                {
                    frontPart = groups[i];
                    break;
                }
            }
        }

        if (frontPart != null)
            _frontPartRectTransform = frontPart.transform as RectTransform;
    }

    private void CacheFrontPartState()
    {
        if (_frontPartRectTransform != null)
            _frontPartInitialAnchoredPosition = _frontPartRectTransform.anchoredPosition;

        if (frontPart != null)
            _frontPartInitialAlpha = frontPart.alpha;
    }

    private void ResetFrontPart()
    {
        _frontPartSequence?.Kill();

        if (_frontPartRectTransform != null)
            _frontPartRectTransform.anchoredPosition = _frontPartInitialAnchoredPosition;

        if (frontPart != null)
        {
            frontPart.alpha = _frontPartInitialAlpha;
            frontPart.interactable = true;
            frontPart.blocksRaycasts = true;
        }
    }

    private async UniTaskVoid PlaySuccess()
    {
        if (_isProcessingSuccess)
            return;

        _isProcessingSuccess = true;
        _isOpeningFrontPart = true;

        await PlayFrontPartOpenAnimation();
    }

    private void OnSwitchToggleValueChanged(bool isOn)
    {
        if (!isOn)
            return;

        if (!_isOpeningFrontPart)
        {
            SpPasswordPopup2Switch01Toggle?.SetIsOnWithoutNotify(false);
            return;
        }

        if (_isToggleSwitchTriggered)
            return;

        _isToggleSwitchTriggered = true;
        SpPasswordPopup2Light01Toggle?.SetIsOnWithoutNotify(true);
        SpPasswordPopup2Lock01Toggle?.SetIsOnWithoutNotify(true);

        SpPasswordPopup2Lock01Toggle.GetComponent<Image>().enabled = false;
        SpPasswordPopup2Switch01Toggle.GetComponent<Image>().enabled = false;

        CompleteByToggleSwitch().Forget();
        SystemTips.Show(transform.parent, LocalizationManager.GetTranslation("S_HurryTips_Des_level_06"));

        AudioManager.PlaySound("fanSwitch");
        
        AudioManager.PlaySound("passwordCorrect");
    }

    private async UniTaskVoid CompleteByToggleSwitch()
    {
        await UniTask.WaitForSeconds(2f);
        this.DispatchEvent(Witness<OnTiggerItemEvent>._, 1);
        Close(null);
    }

    private async UniTask PlayFrontPartOpenAnimation()
    {
        EnsureFrontPartReference();
        if (frontPart == null)
            return;

        var completed = false;
        _frontPartSequence?.Kill();
        _frontPartSequence = DOTween.Sequence().SetLink(gameObject);

        if (_frontPartRectTransform != null)
        {
            _frontPartSequence.Join(_frontPartRectTransform.DOAnchorPosY(
                    _frontPartInitialAnchoredPosition.y + frontPartMoveOffsetY,
                    Mathf.Max(0f, frontPartMoveDuration))
                .SetEase(frontPartMoveEase));
        }

        _frontPartSequence.Join(frontPart.DOFade(0f, Mathf.Max(0f, frontPartFadeDuration))
            .SetEase(frontPartFadeEase));

        _frontPartSequence.OnComplete(() =>
        {
            frontPart.alpha = 0f;
            frontPart.interactable = false;
            frontPart.blocksRaycasts = false;
            completed = true;
        });

        await UniTask.WaitUntil(() => completed);
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
        _frontPartSequence?.Kill();
        UIManager.Instance.DestroyUI(this);
    }

    public static PasswordPopup2 Create()
    {
        var panel = UIManager.Instance.OpenUI<PasswordPopup2>($"{typeof(PasswordPopup2).Name}", UILayer.Popup);
        return panel;
    }
}
