using DG.Tweening;
using UnityEngine;

public class Level74GamePlayPanelCtrl : MonoBehaviour
{
    private enum OperationMode
    {
        None,
        Cut,
        Dig
    }

    [SerializeField] private CustomButton cutButton;
    [SerializeField] private CustomButton digButton;
    [SerializeField] private float showScaleDuration = 0.1f;

    private OperationMode visibleMode = OperationMode.None;
    private System.Action cutHandler;
    private System.Action digHandler;
    private bool cutSubscribed;
    private bool digSubscribed;

    private void Awake()
    {
        HideAll();
    }

    public void Bind(System.Action onCut, System.Action onDig)
    {
        Unbind();
        cutHandler = onCut;
        digHandler = onDig;

        if (cutButton != null)
        {
            cutButton.OnClick += OnCutClicked;
            cutSubscribed = true;
        }

        CustomButton resolvedDigButton = ResolveDigButton();
        if (resolvedDigButton != null)
        {
            resolvedDigButton.OnClick += OnDigClicked;
            digSubscribed = true;
        }
    }

    public void Unbind()
    {
        if (cutSubscribed && cutButton != null)
            cutButton.OnClick -= OnCutClicked;

        CustomButton resolvedDigButton = ResolveDigButton();
        if (digSubscribed && resolvedDigButton != null)
            resolvedDigButton.OnClick -= OnDigClicked;

        cutSubscribed = false;
        digSubscribed = false;
        cutHandler = null;
        digHandler = null;
    }

    public void ShowCut(bool interactable)
    {
        ShowOnly(cutButton, interactable);
        visibleMode = OperationMode.Cut;
    }

    public void ShowDig(bool interactable)
    {
        ShowOnly(ResolveDigButton(), interactable);
        visibleMode = OperationMode.Dig;
    }

    public void HideAll()
    {
        visibleMode = OperationMode.None;
        HideButton(cutButton);

        CustomButton resolvedDigButton = ResolveDigButton();
        if (resolvedDigButton != cutButton)
            HideButton(resolvedDigButton);
    }

    /// <summary>
    /// 仅用于运行时 cut 操作完成后的按钮隐藏表现；通过 DoTween 缩放到 0 后再关闭对象，不负责清理绑定或生命周期销毁。
    /// </summary>
    public void HideCutWithScaleTween()
    {
        HideCurrentWithScaleTween(OperationMode.Cut);
    }

    /// <summary>
    /// 仅用于 Level74 运行时操作完成后的当前操作按钮隐藏表现；通过 DoTween 缩放到 0 后再关闭对象，重置、退出和销毁流程仍应使用 HideAll 立即清理。
    /// </summary>
    public void HideCurrentWithScaleTween()
    {
        HideCurrentWithScaleTween(visibleMode);
    }

    private void HideCurrentWithScaleTween(OperationMode mode)
    {
        CustomButton target = ResolveButton(mode);
        if (visibleMode == mode)
            visibleMode = OperationMode.None;

        HideButtonWithScaleTween(target);
    }

    public void SetInteractable(bool interactable)
    {
        if (visibleMode == OperationMode.Cut && cutButton != null)
            cutButton.Interactable = interactable;

        if (visibleMode == OperationMode.Dig)
        {
            CustomButton resolvedDigButton = ResolveDigButton();
            if (resolvedDigButton != null)
                resolvedDigButton.Interactable = interactable;
        }
    }

    private CustomButton ResolveDigButton()
    {
        return digButton != null ? digButton : cutButton;
    }

    private CustomButton ResolveButton(OperationMode mode)
    {
        switch (mode)
        {
            case OperationMode.Cut:
                return cutButton;
            case OperationMode.Dig:
                return ResolveDigButton();
            default:
                return null;
        }
    }

    private void ShowOnly(CustomButton target, bool interactable)
    {
        HideAll();

        if (target == null)
            return;

        target.gameObject.SetActive(true);
        target.Interactable = interactable;
        target.transform.DOKill();
        target.transform.localScale = Vector3.one * 0.5f;
        target.transform.DOScale(Vector3.one, showScaleDuration).SetLink(target.gameObject);
    }

    private void HideButton(CustomButton button)
    {
        if (button == null)
            return;

        button.transform.DOKill();
        button.Interactable = false;
        button.transform.localScale = Vector3.zero;
        button.gameObject.SetActive(false);
    }

    private void HideButtonWithScaleTween(CustomButton button)
    {
        if (button == null)
            return;

        button.transform.DOKill();
        button.Interactable = false;
        if (!button.gameObject.activeSelf)
        {
            button.transform.localScale = Vector3.zero;
            return;
        }

        button.transform.DOScale(Vector3.zero, Mathf.Max(0f, showScaleDuration))
            .SetLink(button.gameObject)
            .OnComplete(() =>
            {
                if (button != null)
                    button.gameObject.SetActive(false);
            });
    }

    private void OnCutClicked(CustomButton _)
    {
        cutHandler?.Invoke();
    }

    private void OnDigClicked(CustomButton _)
    {
        digHandler?.Invoke();
    }

    private void OnDisable()
    {
        HideAll();
    }

    private void OnDestroy()
    {
        Unbind();
        HideAll();
    }
}
