using System;
using System.Globalization;
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
/// Level128 专用密码弹窗。答案为提交时的设备本地时间，支持 12/24 小时制及上一分钟容错。
/// </summary>
public sealed class Level128PasswordPopup : PasswordPopup
{
    private const int MaxLength = 4;
    private const float ContentScale = 0.92f;
    private const float BackgroundHeight = 1028f;
    private const float BackgroundAnchoredY = -14f;
    private const float ViewAnchoredY = 337.25f;
    private const float CloseButtonAnchoredY = 471.3f;
    private const float KeypadHeight = 685f;
    private const float KeypadAnchoredY = -132f;
    private readonly StringBuilder _inputBuffer = new StringBuilder(MaxLength);
    private readonly CustomButton[] _digitButtons = new CustomButton[10];
    private readonly TextMeshProUGUI[] _displayTexts = new TextMeshProUGUI[MaxLength];

    private CustomButton _closeButton;
    private CustomButton _starButton;
    private CustomButton _hashButton;
    private MMF_Player _errorFeedback;
    private bool _controlsBound;
    private bool _inputLocked;
    private bool _successDispatched;
    private bool _closeCallbackInvoked;
    private int _sessionVersion;
    private int _errorFeedbackSessionVersion;

    #region UI lifecycle

    public override void OnCreate()
    {
        ResolveControls();
        BindControls();
        ClearInputState();
    }

    public override void OnOpen()
    {
        _isOpen = true;
        _sessionVersion++;
        _inputLocked = false;
        _successDispatched = false;
        _closeCallbackInvoked = false;
        _errorFeedback?.StopFeedbacks();
        ClearInputState();
    }

    public override void OnClose()
    {
        _sessionVersion++;
        _inputLocked = true;
        _successDispatched = false;
        _errorFeedback?.StopFeedbacks();
        ClearInputState();
        base.OnClose();
    }

    public override void OnDestroyView()
    {
        UnbindControls();
        base.OnDestroyView();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && IsVisible && !_inputLocked)
            ClearInputState();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && IsVisible && !_inputLocked)
            ClearInputState();
    }

    #endregion

    #region Control setup

    private void ResolveControls()
    {
        for (int digit = 1; digit <= 9; digit++)
            _digitButtons[digit] = FindComponent<CustomButton>($"input{digit}_btn");

        CreateBottomRow();
        _closeButton = FindComponent<CustomButton>("close_btn");
        _errorFeedback = FindComponent<MMF_Player>("Feedback");

        for (int index = 0; index < _displayTexts.Length; index++)
            _displayTexts[index] = FindComponent<TextMeshProUGUI>($"num{index + 1}_txt");
    }

    private void CreateBottomRow()
    {
        CustomButton fallbackTemplate = _digitButtons[8];
        if (fallbackTemplate == null
            || fallbackTemplate.transform.parent is not RectTransform keypadRoot)
        {
            Debug.LogWarning("[Level128PasswordPopup] Cannot create the bottom row because input8_btn is missing.", this);
            return;
        }

        ConfigureExtendedLayout(keypadRoot);
        _starButton = EnsureBottomRowButton(
            "inputStar_btn",
            "*",
            _digitButtons[7] ?? fallbackTemplate,
            keypadRoot);
        _digitButtons[0] = EnsureBottomRowButton("input0_btn", "0", fallbackTemplate, keypadRoot);
        _hashButton = EnsureBottomRowButton(
            "inputHash_btn",
            "#",
            _digitButtons[9] ?? fallbackTemplate,
            keypadRoot);
        LayoutRebuilder.ForceRebuildLayoutImmediate(keypadRoot);
    }

    private void ConfigureExtendedLayout(RectTransform keypadRoot)
    {
        RectTransform background = FindComponent<RectTransform>("Bg");
        if (background != null)
        {
            background.sizeDelta = new Vector2(background.sizeDelta.x, BackgroundHeight);
            background.anchoredPosition = new Vector2(background.anchoredPosition.x, BackgroundAnchoredY);
        }

        RectTransform view = FindComponent<RectTransform>("View");
        if (view != null)
        {
            view.localScale = Vector3.one * ContentScale;
            view.anchoredPosition = new Vector2(view.anchoredPosition.x, ViewAnchoredY);
        }

        RectTransform closeButton = FindComponent<RectTransform>("close_btn");
        if (closeButton != null)
            closeButton.anchoredPosition = new Vector2(closeButton.anchoredPosition.x, CloseButtonAnchoredY);

        keypadRoot.localScale = Vector3.one * ContentScale;
        keypadRoot.sizeDelta = new Vector2(keypadRoot.sizeDelta.x, KeypadHeight);
        keypadRoot.anchoredPosition = new Vector2(keypadRoot.anchoredPosition.x, KeypadAnchoredY);

        GridLayoutGroup grid = keypadRoot.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
        }
        else
        {
            Debug.LogWarning("[Level128PasswordPopup] Keypad GridLayoutGroup is missing.", this);
        }
    }

    private CustomButton EnsureBottomRowButton(
        string objectName,
        string labelText,
        CustomButton template,
        RectTransform keypadRoot)
    {
        CustomButton button = FindComponent<CustomButton>(objectName);
        if (button == null && template != null)
        {
            GameObject buttonObject = Instantiate(template.gameObject, keypadRoot, false);
            buttonObject.name = objectName;
            button = buttonObject.GetComponent<CustomButton>();
        }

        if (button == null)
            return null;

        if (button.transform.parent != keypadRoot)
            button.transform.SetParent(keypadRoot, false);

        LayoutElement layoutElement = button.GetComponent<LayoutElement>();
        if (layoutElement != null)
            layoutElement.ignoreLayout = false;

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
            label.text = labelText;

        button.transform.SetAsLastSibling();
        return button;
    }

    private T FindComponent<T>(string objectName) where T : Component
    {
        Transform target = FindDescendant(transform, objectName);
        return target != null ? target.GetComponent<T>() : null;
    }

    private static Transform FindDescendant(Transform root, string objectName)
    {
        if (root.name == objectName)
            return root;

        for (int index = 0; index < root.childCount; index++)
        {
            Transform result = FindDescendant(root.GetChild(index), objectName);
            if (result != null)
                return result;
        }

        return null;
    }

    private void BindControls()
    {
        if (_controlsBound)
            return;

        for (int digit = 0; digit <= 9; digit++)
        {
            if (_digitButtons[digit] != null)
                _digitButtons[digit].OnClick += OnDigitClicked;
        }

        if (_starButton != null)
            _starButton.OnClick += OnStarClicked;

        if (_hashButton != null)
            _hashButton.OnClick += OnHashClicked;

        if (_closeButton != null)
            _closeButton.OnClick += OnCloseClicked;

        if (_errorFeedback != null)
        {
            _errorFeedback.Events.OnComplete.RemoveListener(OnErrorFeedbackComplete);
            _errorFeedback.Events.OnComplete.AddListener(OnErrorFeedbackComplete);
        }

        _controlsBound = true;
    }

    private void UnbindControls()
    {
        if (!_controlsBound)
            return;

        for (int digit = 0; digit <= 9; digit++)
        {
            if (_digitButtons[digit] != null)
                _digitButtons[digit].OnClick -= OnDigitClicked;
        }

        if (_starButton != null)
            _starButton.OnClick -= OnStarClicked;

        if (_hashButton != null)
            _hashButton.OnClick -= OnHashClicked;

        if (_closeButton != null)
            _closeButton.OnClick -= OnCloseClicked;

        if (_errorFeedback != null)
            _errorFeedback.Events.OnComplete.RemoveListener(OnErrorFeedbackComplete);

        _controlsBound = false;
    }

    #endregion

    #region Password input

    private void OnDigitClicked(CustomButton button)
    {
        for (int digit = 0; digit <= 9; digit++)
        {
            if (_digitButtons[digit] != button)
                continue;

            AppendCharacter((char)('0' + digit));
            return;
        }
    }

    private void OnStarClicked(CustomButton button) => AppendCharacter('*');

    private void OnHashClicked(CustomButton button) => AppendCharacter('#');

    private void AppendCharacter(char value)
    {
        if (_inputLocked || _inputBuffer.Length >= MaxLength)
            return;

        AudioManager.PlaySound("pressPassword");
        _inputBuffer.Append(value);
        RefreshView();

        if (_inputBuffer.Length == MaxLength)
            CheckPassword(DateTimeOffset.Now);
    }

    private void CheckPassword(DateTimeOffset submittedAt)
    {
        if (IsPasswordValid(_inputBuffer.ToString(), submittedAt))
        {
            _inputLocked = true;
            int successSessionVersion = _sessionVersion;
            AudioManager.PlaySound("passwordCorrect");
            SystemTips.Show(LocalizationManager.GetTranslation("S_HurryTips_Des_level_06"));
            PlaySuccessAsync(successSessionVersion).Forget();
            return;
        }

        AudioManager.PlaySound("passwordError");
        if (_errorFeedback == null)
        {
            ClearInputState();
            return;
        }

        _errorFeedbackSessionVersion = _sessionVersion;
        _errorFeedback.PlayFeedbacks();
    }

    /// <summary>
    /// 校验四位设备本地时间密码。接受提交分钟与上一分钟，并兼容 12/24 小时制。
    /// 调用边界：运行时提交校验和确定性测试；输入必须包含四个十进制数字。
    /// </summary>
    public static bool IsPasswordValid(string input, DateTimeOffset submittedAt)
    {
        if (string.IsNullOrEmpty(input) || input.Length != MaxLength)
            return false;

        for (int index = 0; index < input.Length; index++)
        {
            if (input[index] < '0' || input[index] > '9')
                return false;
        }

        DateTimeOffset previousMinute = submittedAt.AddMinutes(-1);
        return MatchesMinute(input, submittedAt) || MatchesMinute(input, previousMinute);
    }

    private static bool MatchesMinute(string input, DateTimeOffset time)
    {
        return string.Equals(input, time.ToString("HHmm", CultureInfo.InvariantCulture), StringComparison.Ordinal)
               || string.Equals(input, time.ToString("hhmm", CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    private void OnErrorFeedbackComplete()
    {
        if (_errorFeedbackSessionVersion == _sessionVersion && IsVisible && !_inputLocked)
            ClearInputState();
    }

    private void ClearInputState()
    {
        _inputBuffer.Clear();
        RefreshView();
    }

    private void RefreshView()
    {
        for (int index = 0; index < _displayTexts.Length; index++)
        {
            TextMeshProUGUI text = _displayTexts[index];
            if (text == null)
                continue;

            text.text = index < _inputBuffer.Length ? _inputBuffer[index].ToString() : "-";
        }
    }

    #endregion

    #region Close and success

    private async UniTask PlaySuccessAsync(int successSessionVersion)
    {
        await UniTask.WaitForSeconds(1f);

        if (this == null
            || !_isOpen
            || successSessionVersion != _sessionVersion
            || _successDispatched)
        {
            return;
        }

        _successDispatched = true;
        this.DispatchEvent(Witness<OnTiggerItemEvent>._, 1);
        CloseAndDestroy();
    }

    private void OnCloseClicked(CustomButton button)
    {
        if (IsOpen)
            CloseAndDestroy();
    }

    private void CloseAndDestroy()
    {
        if (!_closeCallbackInvoked)
        {
            _closeCallbackInvoked = true;
            OnPopupCloseCallback?.Invoke();
        }

        if (UIManager.Instance != null)
            UIManager.Instance.DestroyUI(this);
    }

    #endregion
}
