using System;
using Bear.UI;
using DG.Tweening;
using UnityEngine;

/// <summary>139 私有 HUD：沿用 006 按钮外观与 0.1 秒缩放，不订阅全局密码事件。</summary>
public sealed class Level139Hud : MonoBehaviour
{
    public CustomButton Use_btn;
    private Action requestOpen;
    private Tween transition;
    private bool shown;

    private void Awake()
    {
        if (Use_btn == null)
            return;
        Use_btn.OnClick += OnClick;
        HideImmediately();
    }

    public void Bind(Action callback)
    {
        requestOpen = callback;
        HideImmediately();
    }

    public void SetEntry(bool visible, bool clickable)
    {
        if (Use_btn == null)
            return;
        Use_btn.IsBlockClick = !visible || !clickable;
        Use_btn.Interactable = visible && clickable;
        if (shown == visible)
            return;
        shown = visible;
        transition?.Kill();
        Transform button = Use_btn.transform;
        if (visible)
        {
            Use_btn.gameObject.SetActive(true);
            button.localScale = Vector3.one * 0.5f;
            transition = button.DOScale(Vector3.one, 0.1f);
        }
        else
        {
            transition = button.DOScale(Vector3.zero, 0.1f).OnComplete(() =>
            {
                if (Use_btn != null && !shown)
                    Use_btn.gameObject.SetActive(false);
            });
        }
    }

    private void OnClick(CustomButton button)
    {
        if (shown)
            requestOpen?.Invoke();
    }

    private void HideImmediately()
    {
        transition?.Kill();
        transition = null;
        shown = false;
        if (Use_btn == null)
            return;
        Use_btn.IsBlockClick = true;
        Use_btn.Interactable = false;
        Use_btn.transform.localScale = Vector3.zero;
        Use_btn.gameObject.SetActive(false);
    }

    private void OnDisable() => HideImmediately();

    private void OnDestroy()
    {
        HideImmediately();
        requestOpen = null;
        if (Use_btn != null)
            Use_btn.OnClick -= OnClick;
    }
}
