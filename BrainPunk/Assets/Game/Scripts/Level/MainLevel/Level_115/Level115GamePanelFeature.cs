using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Level 115 专属 Gameplay HUD 功能：显示课桌手掌按钮并把点击请求交给关卡控制器。
/// 不复用或修改 Level 006 的密码弹窗 Feature。
/// </summary>
public sealed class Level115GamePanelFeature : MonoBehaviour
{
    [SerializeField] private CustomButton Use_btn;

    private Action _onUse;
    private Tween _visibilityTween;
    private int _visibilityVersion;
    private bool _isVisible;

    public bool IsVisible => _isVisible;

    private void Awake()
    {
        if (Use_btn == null)
            return;

        Use_btn.OnClick += OnUseClicked;
        SetVisibleImmediately(false);
    }

    /// <summary>
    /// 绑定当前 Level 115 实例的课桌交互入口；传入 null 时解除绑定。
    /// </summary>
    public void Configure(Action onUse)
    {
        _onUse = onUse;
    }

    /// <summary>
    /// Popup 关闭后清除 CustomButton 的旧点击冷却与指针状态；同帧重激活不会改变可见布局。
    /// </summary>
    public void ResetInteractionState()
    {
        if (Use_btn == null || !_isVisible || !Use_btn.gameObject.activeSelf)
            return;

        Use_btn.gameObject.SetActive(false);
        Use_btn.gameObject.SetActive(true);
    }

    /// <summary>
    /// 按 Level 006 手掌按钮的 0.1 秒缩放方式显示或隐藏。
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (Use_btn == null || (_isVisible == visible && Use_btn.gameObject.activeSelf == visible))
            return;

        _isVisible = visible;
        int version = ++_visibilityVersion;

        _visibilityTween?.Kill();
        _visibilityTween = null;
        Use_btn.transform.DOKill();

        if (visible)
        {
            Use_btn.gameObject.SetActive(true);
            Use_btn.transform.localScale = Vector3.one * 0.5f;
            _visibilityTween = Use_btn.transform
                .DOScale(Vector3.one, 0.1f)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (version == _visibilityVersion)
                        _visibilityTween = null;
                });
            return;
        }

        if (!Use_btn.gameObject.activeSelf)
        {
            Use_btn.transform.localScale = Vector3.zero;
            return;
        }

        _visibilityTween = Use_btn.transform
            .DOScale(Vector3.zero, 0.1f)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                if (version != _visibilityVersion || _isVisible)
                    return;

                Use_btn.gameObject.SetActive(false);
                _visibilityTween = null;
            });
    }

    private void SetVisibleImmediately(bool visible)
    {
        _isVisible = visible;
        _visibilityVersion++;

        _visibilityTween?.Kill();
        _visibilityTween = null;

        if (Use_btn == null)
            return;

        Use_btn.transform.DOKill();
        Use_btn.transform.localScale = visible ? Vector3.one : Vector3.zero;
        Use_btn.gameObject.SetActive(visible);
    }

    private void OnUseClicked(CustomButton button)
    {
        if (_isVisible)
            _onUse?.Invoke();
    }

    private void OnDestroy()
    {
        _onUse = null;
        _visibilityVersion++;
        _visibilityTween?.Kill();
        _visibilityTween = null;

        if (Use_btn != null)
        {
            Use_btn.OnClick -= OnUseClicked;
            Use_btn.transform.DOKill();
        }
    }
}
