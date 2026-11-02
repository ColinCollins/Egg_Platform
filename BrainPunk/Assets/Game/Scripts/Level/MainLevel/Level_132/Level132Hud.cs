using System;
using TMPro;
using UnityEngine;

/// <summary>仅改当前 132 面板实例；不改共享 GamePlayPanel 或其 Prefab。</summary>
internal sealed class Level132Hud : IDisposable
{
    private readonly GamePlayPanel _panel;
    private readonly CustomButton _jump;
    private readonly CustomButton _left;
    private readonly CustomButton _right;
    private readonly Transform _arrow;
    private readonly Quaternion _arrowRotation;
    private readonly bool _jumpActive;
    private readonly bool _leftActive;
    private readonly bool _rightActive;
    private readonly GameObject _clickBlock;
    private readonly GameObject _root;
    private readonly TextMeshProUGUI _setupError;
    private bool _showingSetupError;

    public bool IsBlocked => _panel == null || !_panel.isActiveAndEnabled ||
        (_clickBlock != null && _clickBlock.activeInHierarchy);

    public Level132Hud(GamePlayPanel panel)
    {
        _panel = panel;
        foreach (var button in panel.GetComponentsInChildren<CustomButton>(true))
        {
            switch (button.name)
            {
                case "jump_btn": _jump = button; break;
                case "leftMove_btn": _left = button; break;
                case "rightMove_btn": _right = button; break;
            }
        }
        if (_jump == null || _left == null || _right == null)
            throw new InvalidOperationException("[Level132] Gameplay movement buttons are missing.");
        _jumpActive = _jump.gameObject.activeSelf;
        _leftActive = _left.gameObject.activeSelf;
        _rightActive = _right.gameObject.activeSelf;
        _arrow = panel.JumpButtonIcon != null ? panel.JumpButtonIcon.transform : null;
        if (_arrow != null)
        {
            _arrowRotation = _arrow.localRotation;
        }
        foreach (var child in panel.GetComponentsInChildren<Transform>(true))
            if (child.name.StartsWith("ClickBlock", StringComparison.Ordinal))
                _clickBlock = child.gameObject;

        _root = new GameObject("Level132Status", typeof(RectTransform));
        var rect = (RectTransform)_root.transform;
        rect.SetParent(panel.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var source = panel.GetComponentInChildren<TMP_Text>(true);
        _setupError = CreateText("SetupError", source, new Vector2(0f, -195f), 30f);
        _setupError.gameObject.SetActive(false);
    }

    private TextMeshProUGUI CreateText(string name, TMP_Text source, Vector2 position, float size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var rect = (RectTransform)go.transform;
        rect.SetParent(_root.transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(600f, 80f);
        var text = go.GetComponent<TextMeshProUGUI>();
        if (source != null)
        {
            text.font = source.font;
            text.fontSharedMaterial = source.fontSharedMaterial;
        }
        text.color = Color.black;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    public void SetControls(bool move, bool jump, bool flip)
    {
        if (_panel == null)
            return;
        _panel.ResetState();
        _panel.SwitchMoveBtns(move);
        _jump.gameObject.SetActive(jump || flip);
        if (_arrow != null)
            _arrow.localRotation = flip ? Quaternion.Euler(0f, 0f, 90f) : _arrowRotation;
        // 首次 UI 入场动画可能在按钮隐藏时跳过缩放；重新开放时明确恢复可见比例。
        if (move)
            _left.transform.localScale = _right.transform.localScale = Vector3.one;
        if (jump || flip)
            _jump.transform.localScale = Vector3.one;
    }

    public void ShowSetupError()
    {
        if (_root == null || _showingSetupError) return;
        _showingSetupError = true;
        _setupError.gameObject.SetActive(true);
        // 仅配置失败时的开发诊断，不把错误态伪装成正常准备倒计时。
        _setupError.text = "Level 132: setup error\nSee Unity Console";
    }

    public void ClearMovement()
    {
        if (_panel != null) _panel.ResetState();
    }

    public void Dispose()
    {
        if (_arrow != null)
            _arrow.localRotation = _arrowRotation;
        if (_panel != null)
            _panel.ResetState();
        if (_jump != null) _jump.gameObject.SetActive(_jumpActive);
        if (_left != null) _left.gameObject.SetActive(_leftActive);
        if (_right != null) _right.gameObject.SetActive(_rightActive);
        if (_root != null) UnityEngine.Object.Destroy(_root);
    }
}
