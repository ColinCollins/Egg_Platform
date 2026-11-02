using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>在当前 HUD 增加右盘自动跑按钮；左台仍复用共享 Jump 和移动事件。</summary>
internal sealed class Level149Hud : IDisposable
{
    private readonly GamePlayPanel _panel;
    private readonly CustomButton _left;
    private readonly CustomButton _right;
    private readonly GameObject _block;
    private readonly GameObject _root;
    private readonly CustomButton _charge;
    private readonly Action _onCharge;
    private readonly RectTransform _jump;
    private readonly RectTransform _rect;
    private bool _leftHeld;
    private bool _rightHeld;
    private bool _visible;
    private bool _canBoard;
    private Tween _visibilityTween;
    /// <summary>当前 HUD 绑定的共享 GameplayPanel。</summary>
    public GamePlayPanel Panel => _panel;

    /// <summary>当前共享 HUD 是否不可操作。</summary>
    /// <remarks>面板未激活或 ClickBlock 生效时返回 true，本关按钮不得执行充电动作。</remarks>
    public bool Blocked => _panel == null || !_panel.isActiveAndEnabled ||
        (_block != null && _block.activeInHierarchy);

    /// <summary>根据当前按住的共享左右移动按钮返回方向输入。</summary>
    public float Direction => _leftHeld == _rightHeld ? 0f : _rightHeld ? 1f : -1f;

    /// <summary>创建本关局部充电按钮并接管共享左右移动按钮的按住状态。</summary>
    /// <param name="panel">当前运行时 GameplayPanel。</param>
    /// <param name="icon">本关充电按钮使用的闪电图标。</param>
    /// <param name="charge">右盘按钮通过可操作检查后的充电回调。</param>
    /// <remarks>构造失败表示共享 HUD 缺少本关所需的基础移动控件。</remarks>
    public Level149Hud(GamePlayPanel panel, Sprite icon, Action charge)
    {
        _panel = panel;
        foreach (var button in panel.GetComponentsInChildren<CustomButton>(true))
        {
            if (button.name == "jump_btn") _jump = button.transform as RectTransform;
            if (button.name == "leftMove_btn") _left = button;
            if (button.name == "rightMove_btn") _right = button;
        }
        Image sourceJumpIcon = panel.JumpButtonIcon;
        if (_jump == null || _left == null || _right == null || sourceJumpIcon == null)
            throw new InvalidOperationException("[Level149] Missing standard HUD movement buttons or jump icon.");
        foreach (var child in panel.GetComponentsInChildren<Transform>(true))
            if (child.name.StartsWith("ClickBlock", StringComparison.Ordinal)) _block = child.gameObject;

        // 006 的操作按钮靠近后 0.5 -> 1 弹出；外层只负责显隐，内层负责按压，避免争抢同一 Scale。
        _root = new GameObject("Level149ChargeButton", typeof(RectTransform));
        _root.SetActive(false);
        _root.layer = _jump.gameObject.layer;
        _rect = (RectTransform)_root.transform;
        _rect.SetParent(_jump.parent, false);
        _rect.SetSiblingIndex(_jump.GetSiblingIndex());
        _rect.localScale = Vector3.zero;
        // 在未激活父级下构造，保证 CustomButton.Awake 收集反馈组件前已完成配置。
        // Jump 的普通 C# 事件订阅不会被 Instantiate 复制。
        var buttonObject = UnityEngine.Object.Instantiate(_jump.gameObject, _rect, false);
        buttonObject.name = "ChargeButton";
        var buttonRect = (RectTransform)buttonObject.transform;
        buttonRect.anchorMin = Vector2.zero;
        buttonRect.anchorMax = Vector2.one;
        buttonRect.offsetMin = buttonRect.offsetMax = Vector2.zero;
        buttonRect.localScale = Vector3.one;
        _charge = buttonObject.GetComponent<CustomButton>();
        var press = buttonObject.GetComponent<ClickScaleAnim>();
        if (press == null) press = buttonObject.AddComponent<ClickScaleAnim>();
        press.enabled = true;
        press.SetScaleTarget(buttonRect);
        _onCharge = charge;
        _charge.OnClick += OnChargeClicked;
        // 按显式源图标的节点索引映射完整克隆，只替换 bg 下的箭头，不扫描或修改背景。
        Image image = buttonRect.GetChild(sourceJumpIcon.transform.parent.GetSiblingIndex())
            .GetChild(sourceJumpIcon.transform.GetSiblingIndex()).GetComponent<Image>();
        image.name = "ChargeIcon";
        image.sprite = icon;
        // Jump 箭头自带 -90° 旋转；换成闪电后必须恢复素材原始朝向。
        image.rectTransform.localRotation = Quaternion.identity;
        image.preserveAspect = true;
        image.raycastTarget = false;
        buttonObject.SetActive(true);
        _root.SetActive(true); // 隐藏时保持 Scale=0，让按压回弹完成，不残留上一次按下状态。
        _charge.Interactable = false;
        _charge.IsBlockClick = true;
        _left.OnClickEnter += LeftDown;
        _left.OnClickUp += LeftUp;
        _left.OnClickExit += LeftUp;
        _right.OnClickEnter += RightDown;
        _right.OnClickUp += RightUp;
        _right.OnClickExit += RightUp;
    }

    /// <summary>刷新充电按钮的显隐、可点击状态和位置。</summary>
    /// <param name="nearby">Actor 是否在右盘交互范围内。</param>
    /// <param name="canBoard">Actor 当前是否满足站稳等登盘条件。</param>
    /// <remarks>按钮可见但不可登盘时仍允许按压反馈，不执行充电回调。</remarks>
    public void Refresh(bool nearby, bool canBoard)
    {
        if (_root == null || _jump == null) return;
        _rect.anchorMin = _jump.anchorMin;
        _rect.anchorMax = _jump.anchorMax;
        _rect.pivot = _jump.pivot;
        _rect.sizeDelta = _jump.sizeDelta;
        _rect.anchoredPosition = _jump.anchoredPosition - Vector2.right * (_jump.rect.width + 24f);
        bool visible = nearby && !Blocked;
        _canBoard = visible && canBoard;
        if (_visible != visible)
        {
            _visible = visible;
            _visibilityTween?.Kill();
            if (visible) _rect.localScale = Vector3.one * 0.5f;
            _visibilityTween = _rect.DOScale(visible ? Vector3.one : Vector3.zero, 0.1f)
                .SetUpdate(true).OnKill(() => _visibilityTween = null);
        }
        // 可见但未站稳时仍接收 Down/Up 播放反馈，只阻止业务 Click。
        _charge.Interactable = visible;
        _charge.IsBlockClick = !_canBoard;
    }

    private void OnChargeClicked(CustomButton button)
    {
        if (_canBoard && button.Interactable && !Blocked) _onCharge?.Invoke();
    }

    /// <summary>清除本关缓存的左右按住状态并请求共享面板清理输入状态。</summary>
    public void ClearHeld()
    {
        _leftHeld = _rightHeld = false;
        if (_panel != null) _panel.ResetState();
    }
    private void LeftDown(CustomButton _) => _leftHeld = true;
    private void RightDown(CustomButton _) => _rightHeld = true;
    private void LeftUp(CustomButton _) => _leftHeld = false;
    private void RightUp(CustomButton _) => _rightHeld = false;

    /// <summary>解绑 HUD 事件、停止显隐 Tween 并销毁本关运行时按钮。</summary>
    /// <remarks>Reset、Replay、失败、通关和关卡禁用路径都必须调用；不恢复共享面板的 Prefab 初态。</remarks>
    public void Dispose()
    {
        if (_left != null)
        {
            _left.OnClickEnter -= LeftDown;
            _left.OnClickUp -= LeftUp;
            _left.OnClickExit -= LeftUp;
        }
        if (_right != null)
        {
            _right.OnClickEnter -= RightDown;
            _right.OnClickUp -= RightUp;
            _right.OnClickExit -= RightUp;
        }
        if (_charge != null) _charge.OnClick -= OnChargeClicked;
        _visibilityTween?.Kill();
        if (_root != null) UnityEngine.Object.Destroy(_root);
        ClearHeld();
    }
}
