using System;
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
/// Level151 专用密码弹窗。固定答案为 1358，其余输入、布局和会话生命周期沿用 Level128。
/// </summary>
public sealed class Level151PasswordPopup : PasswordPopup
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
    /// <summary>Inspector 按数字下标绑定 1～9；下标 0 保留给首次创建的 0 键。</summary>
    [SerializeField] private CustomButton[] _digitButtons = new CustomButton[10];
    /// <summary>Inspector 从左到右绑定四位显示文本，不依赖基类的私有生成字段。</summary>
    [SerializeField] private TextMeshProUGUI[] _displayTexts = new TextMeshProUGUI[MaxLength];

    /// <summary>Inspector 绑定原关闭按钮；只关闭当前密码输入会话。</summary>
    [SerializeField] private CustomButton _closeButton;
    private CustomButton _starButton;
    private CustomButton _hashButton;
    /// <summary>Inspector 绑定原错误反馈，保持错误动画和清空输入时序。</summary>
    [SerializeField] private MMF_Player _errorFeedback;
    /// <summary>Inspector 绑定原背景，只应用 128 已有的纵向布局参数。</summary>
    [SerializeField] private RectTransform background;
    /// <summary>Inspector 绑定原四位显示区域，沿用 128 的缩放与位置。</summary>
    [SerializeField] private RectTransform view;
    /// <summary>Inspector 绑定原九键容器，第四行仍在首次创建弹窗时生成。</summary>
    [SerializeField] private RectTransform keypadRoot;
    private bool _controlsBound;
    private bool _inputLocked;
    private bool _successDispatched;
    private bool _closeCallbackInvoked;
    private int _sessionVersion;
    private int _errorFeedbackSessionVersion;

    #region UI lifecycle

    /// <summary>仅由 UI 首次创建调用；绑定已有控件并生成与 128 相同的第四行。</summary>
    public override void OnCreate()
    {
        CreateBottomRow();
        BindControls();
        ClearInputState();
    }

    /// <summary>每次打开建立新输入会话，解除输入锁并清空旧输入。</summary>
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

    /// <summary>关闭时使旧成功任务失效；淡出期间仍保持输入锁定。</summary>
    public override void OnClose()
    {
        _sessionVersion++;
        _inputLocked = true;
        _successDispatched = false;
        _errorFeedback?.StopFeedbacks();
        ClearInputState();
        base.OnClose();
    }

    /// <summary>UI 销毁时解除本弹窗的按钮与反馈回调。</summary>
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

    private void CreateBottomRow()
    {
        CustomButton fallbackTemplate = _digitButtons[8];
        if (fallbackTemplate == null
            || keypadRoot == null)
        {
            Debug.LogWarning("[Level151PasswordPopup] Cannot create the bottom row because input8_btn is missing.", this);
            return;
        }

        ConfigureExtendedLayout(keypadRoot);
        _starButton = CreateBottomRowButton(
            "inputStar_btn",
            "*",
            _digitButtons[7] ?? fallbackTemplate,
            keypadRoot);
        _digitButtons[0] = CreateBottomRowButton("input0_btn", "0", fallbackTemplate, keypadRoot);
        _hashButton = CreateBottomRowButton(
            "inputHash_btn",
            "#",
            _digitButtons[9] ?? fallbackTemplate,
            keypadRoot);
        LayoutRebuilder.ForceRebuildLayoutImmediate(keypadRoot);
    }

    private void ConfigureExtendedLayout(RectTransform keypadRoot)
    {
        if (background != null)
        {
            background.sizeDelta = new Vector2(background.sizeDelta.x, BackgroundHeight);
            background.anchoredPosition = new Vector2(background.anchoredPosition.x, BackgroundAnchoredY);
        }

        if (view != null)
        {
            view.localScale = Vector3.one * ContentScale;
            view.anchoredPosition = new Vector2(view.anchoredPosition.x, ViewAnchoredY);
        }

        RectTransform closeButton = _closeButton.transform as RectTransform;
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
            Debug.LogWarning("[Level151PasswordPopup] Keypad GridLayoutGroup is missing.", this);
        }
    }

    private CustomButton CreateBottomRowButton(
        string objectName,
        string labelText,
        CustomButton template,
        RectTransform keypadRoot)
    {
        CustomButton button = null;
        if (template != null)
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
            CheckPassword();
    }

    private void CheckPassword()
    {
        if (IsPasswordValid(_inputBuffer.ToString()))
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
    /// 校验本关固定四位密码；仅提交完整输入时调用，不读取设备时间。
    /// 空值、不同长度、其它数字和符号均不能解锁。
    /// </summary>
    public static bool IsPasswordValid(string input)
    {
        return string.Equals(input, "1358", StringComparison.Ordinal);
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
