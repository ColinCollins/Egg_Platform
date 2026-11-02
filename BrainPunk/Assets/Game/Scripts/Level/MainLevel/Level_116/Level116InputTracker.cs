using System;
using Game.Play;
using UnityEngine;

/// <summary>
/// Level 116 本地输入观察器。
/// 只读绑定当前 GameplayPanel 的左右移动按钮，以独立保留左右同时按下事实；
/// 不修改共享 GamePlayPanel、CustomButton 或全局输入事件合同。
/// </summary>
public sealed class Level116InputTracker : MonoBehaviour
{
    private const string RightButtonPath = "Root/rightMove_btn";
    private const string LeftButtonPath = "Root/leftMove_btn";

    private GamePlayPanel _boundPanel;
    private CustomButton _rightButton;
    private CustomButton _leftButton;

    private bool _uiRightHeld;
    private bool _uiLeftHeld;
    private bool _keyboardRightHeld;
    private bool _keyboardLeftHeld;

    public bool IsRightHeld => _uiRightHeld || _keyboardRightHeld;

    public bool IsLeftHeld => _uiLeftHeld || _keyboardLeftHeld;

    public event Action StateChanged;

    private void Update()
    {
        ResolvePanelBinding();
        UpdateKeyboardState();
    }

    private void OnDisable()
    {
        UnbindPanel();
        ClearHeldState();
    }

    private void OnDestroy()
    {
        StateChanged = null;
    }

    public void ClearHeldState()
    {
        bool changed = _uiRightHeld || _uiLeftHeld || _keyboardRightHeld || _keyboardLeftHeld;
        _uiRightHeld = false;
        _uiLeftHeld = false;
        _keyboardRightHeld = false;
        _keyboardLeftHeld = false;

        if (changed)
            StateChanged?.Invoke();
    }

    private void ResolvePanelBinding()
    {
        GamePlayPanel currentPanel = PlayCtrl.Instance != null
            ? PlayCtrl.Instance.CurrentGamePlayPanel
            : null;

        if (currentPanel == _boundPanel)
            return;

        UnbindPanel();
        _boundPanel = currentPanel;
        if (_boundPanel == null)
            return;

        _rightButton = ResolveButton(_boundPanel.transform, RightButtonPath);
        _leftButton = ResolveButton(_boundPanel.transform, LeftButtonPath);
        BindButtonEvents();
    }

    private static CustomButton ResolveButton(Transform panelRoot, string path)
    {
        Transform buttonTransform = panelRoot != null ? panelRoot.Find(path) : null;
        return buttonTransform != null ? buttonTransform.GetComponent<CustomButton>() : null;
    }

    private void BindButtonEvents()
    {
        if (_rightButton != null)
        {
            _rightButton.OnClickDown -= OnRightPressed;
            _rightButton.OnClickEnter -= OnRightPressed;
            _rightButton.OnClickUp -= OnRightReleased;
            _rightButton.OnClickExit -= OnRightReleased;

            _rightButton.OnClickDown += OnRightPressed;
            _rightButton.OnClickEnter += OnRightPressed;
            _rightButton.OnClickUp += OnRightReleased;
            _rightButton.OnClickExit += OnRightReleased;
        }

        if (_leftButton != null)
        {
            _leftButton.OnClickDown -= OnLeftPressed;
            _leftButton.OnClickEnter -= OnLeftPressed;
            _leftButton.OnClickUp -= OnLeftReleased;
            _leftButton.OnClickExit -= OnLeftReleased;

            _leftButton.OnClickDown += OnLeftPressed;
            _leftButton.OnClickEnter += OnLeftPressed;
            _leftButton.OnClickUp += OnLeftReleased;
            _leftButton.OnClickExit += OnLeftReleased;
        }
    }

    private void UnbindPanel()
    {
        if (_rightButton != null)
        {
            _rightButton.OnClickDown -= OnRightPressed;
            _rightButton.OnClickEnter -= OnRightPressed;
            _rightButton.OnClickUp -= OnRightReleased;
            _rightButton.OnClickExit -= OnRightReleased;
        }

        if (_leftButton != null)
        {
            _leftButton.OnClickDown -= OnLeftPressed;
            _leftButton.OnClickEnter -= OnLeftPressed;
            _leftButton.OnClickUp -= OnLeftReleased;
            _leftButton.OnClickExit -= OnLeftReleased;
        }

        bool changed = _uiRightHeld || _uiLeftHeld;
        _uiRightHeld = false;
        _uiLeftHeld = false;
        _rightButton = null;
        _leftButton = null;
        _boundPanel = null;

        if (changed)
            StateChanged?.Invoke();
    }

    private void UpdateKeyboardState()
    {
        bool rightHeld = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);
        bool leftHeld = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
        if (_keyboardRightHeld == rightHeld && _keyboardLeftHeld == leftHeld)
            return;

        _keyboardRightHeld = rightHeld;
        _keyboardLeftHeld = leftHeld;
        StateChanged?.Invoke();
    }

    private void OnRightPressed(CustomButton _)
    {
        SetUiRightHeld(true);
    }

    private void OnRightReleased(CustomButton _)
    {
        SetUiRightHeld(false);
    }

    private void OnLeftPressed(CustomButton _)
    {
        SetUiLeftHeld(true);
    }

    private void OnLeftReleased(CustomButton _)
    {
        SetUiLeftHeld(false);
    }

    private void SetUiRightHeld(bool held)
    {
        if (_uiRightHeld == held)
            return;

        _uiRightHeld = held;
        StateChanged?.Invoke();
    }

    private void SetUiLeftHeld(bool held)
    {
        if (_uiLeftHeld == held)
            return;

        _uiLeftHeld = held;
        StateChanged?.Invoke();
    }
}
