using System;
using UnityEngine;

/// <summary>146 私有 HUD：在原按钮功能执行前记录点击，不替换暂停、提示、商店、重来或跳跃。</summary>
public sealed class Level146GamePanelFeature : MonoBehaviour
{
    #region 显式绑定与局部回调
    /// <summary>绑定同一面板的原按钮；四个入口监听 OnClick，跳跃沿用 OnClickDown。</summary>
    [SerializeField] private CustomButton pauseButton;
    [SerializeField] private CustomButton resetButton;
    [SerializeField] private CustomButton hintButton;
    [SerializeField] private CustomButton shopButton;
    [SerializeField] private CustomButton jumpButton;
    /// <summary>仅当前 146 场景订阅；不增加或改写全局玩法事件。</summary>
    public event Action<Level146Ctrl.RequiredButton> ButtonPressed;

    private void Awake()
    {
        if (!Application.isPlaying) return;
        // Awake 早于 GamePlayPanel.OnCreate 的原功能订阅，保证暂停/重开改变状态之前留痕。
        pauseButton.OnClick += OnPause;
        resetButton.OnClick += OnReset;
        hintButton.OnClick += OnHint;
        shopButton.OnClick += OnShop;
        jumpButton.OnClickDown += OnJump;
    }

    private void OnPause(CustomButton button) => Record(button, Level146Ctrl.RequiredButton.Pause);
    private void OnReset(CustomButton button) => Record(button, Level146Ctrl.RequiredButton.Reset);
    private void OnHint(CustomButton button) => Record(button, Level146Ctrl.RequiredButton.Hint);
    private void OnShop(CustomButton button) => Record(button, Level146Ctrl.RequiredButton.Shop);
    private void OnJump(CustomButton button) => Record(button, Level146Ctrl.RequiredButton.Jump);

    private void Record(CustomButton button, Level146Ctrl.RequiredButton kind)
    {
        if (button.Interactable && button.isActiveAndEnabled) ButtonPressed?.Invoke(kind);
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying) return;
        pauseButton.OnClick -= OnPause;
        resetButton.OnClick -= OnReset;
        hintButton.OnClick -= OnHint;
        shopButton.OnClick -= OnShop;
        jumpButton.OnClickDown -= OnJump;
        ButtonPressed = null;
    }
    #endregion
}
