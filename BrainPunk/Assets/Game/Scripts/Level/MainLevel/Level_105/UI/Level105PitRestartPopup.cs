using System;
using System.Collections;
using System.Collections.Generic;
using Bear.UI;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Level 105 专用坑底重开弹窗：Box 落到安全坑底后询问玩家是否要重新开始的诱惑点假弹窗。
/// 该视图只保留 CommonGetRewardPopup 壳与 CustomButton 按压反馈；
/// 按钮不会重新开始关卡或执行任何共享弹窗业务，点击一律由 Level 105 主控制器判负。
/// 与 <see cref="Level105ChestConfirmPopup"/> 结构同构但刻意不共用代码，避免两个诱惑点耦合。
/// </summary>
public sealed class Level105PitRestartPopup : BaseUIView
{
    public enum Choice
    {
        Confirm,
        Cancel
    }

    [SerializeField] private CustomButton confirmButton;
    [SerializeField] private CustomButton cancelButton;
    [SerializeField] private Animator entryAnimator;
    [SerializeField] private float buttonFeedbackSeconds = 0.2f;

    private Action<Choice> onButtonDown;
    private Action<Choice> onButtonFeedbackCompleted;
    private Action onPresentationReady;
    private bool buttonsBound;
    private bool confirmButtonPreviousBlockClick;
    private bool cancelButtonPreviousBlockClick;
    private bool buttonFeedbackPending;
    private bool presentationReady;
    private bool closeRequested;
    private int presentationVersion;

    /// <summary>当前实例仍在 UI 栈中显示且尚未请求关闭。</summary>
    public bool IsShowing => !closeRequested && isActiveAndEnabled && IsOpen && IsVisible;

    /// <summary>
    /// 便捷打开入口；资源名固定为 nameof(Level105PitRestartPopup)。
    /// 也可由调用方直接使用 UIManager.OpenUI&lt;Level105PitRestartPopup&gt;。
    /// </summary>
    public static Level105PitRestartPopup Open(
        Action<Choice> buttonDown = null,
        Action<Choice> buttonFeedbackCompleted = null,
        Action presentationReadyCallback = null,
        UILayer layer = UILayer.Top)
    {
        Level105PitRestartPopup popup = UIManager.Instance?.OpenUI<Level105PitRestartPopup>(
            nameof(Level105PitRestartPopup),
            layer);
        popup?.ConfigureCallbacks(buttonDown, buttonFeedbackCompleted, presentationReadyCallback);
        return popup;
    }

    /// <summary>
    /// 配置本次展示回调。buttonDown 在按下同帧触发；buttonFeedbackCompleted 在约 0.2 秒
    /// 原按钮反馈结束且弹窗已关闭后触发；presentationReadyCallback 在壳 Animator 入场完成后触发。
    /// </summary>
    public void ConfigureCallbacks(
        Action<Choice> buttonDown,
        Action<Choice> buttonFeedbackCompleted,
        Action presentationReadyCallback)
    {
        ConfigureButtonCallbacks(buttonDown, buttonFeedbackCompleted);
        ConfigurePresentationReadyCallback(presentationReadyCallback);
    }

    /// <summary>分别接收按钮按下与约 0.2 秒反馈完成后的通知。</summary>
    public void ConfigureButtonCallbacks(
        Action<Choice> buttonDown,
        Action<Choice> buttonFeedbackCompleted)
    {
        onButtonDown = buttonDown;
        onButtonFeedbackCompleted = buttonFeedbackCompleted;
    }

    /// <summary>接收入场完成通知；若 Animator 已经完成，配置时立即补发一次。</summary>
    public void ConfigurePresentationReadyCallback(Action presentationReadyCallback)
    {
        onPresentationReady = presentationReadyCallback;
        if (presentationReady)
            onPresentationReady?.Invoke();
    }

    public override void OnCreate()
    {
        base.OnCreate();
        BindButtons();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        BindButtons();

        closeRequested = false;
        buttonFeedbackPending = false;
        presentationReady = false;
        presentationVersion++;

        SetButtonBlocking(true);
        StartCoroutine(ObserveEntryCompleted(presentationVersion));
    }

    public override void OnClose()
    {
        presentationVersion++;
        closeRequested = true;
        buttonFeedbackPending = false;
        presentationReady = false;
        StopAllCoroutines();
        UnbindButtons();

        onButtonDown = null;
        onButtonFeedbackCompleted = null;
        onPresentationReady = null;
        base.OnClose();
    }

    /// <summary>零输入展示期结束时关闭；不触发按钮回调。</summary>
    public void PlayOut(Action completed = null)
    {
        CloseNow(completed);
    }

    /// <summary>违规、重开或流程取消时立即移除弹窗；不触发按钮回调。</summary>
    public void Abort(Action completed = null)
    {
        CloseNow(completed);
    }

    /// <summary>
    /// 判断屏幕坐标是否命中“是”或“否”按钮。空白区域不在本视图判定，由 Level 105 主控制器判负。
    /// </summary>
    public bool IsScreenPositionOverInteractiveButton(Vector2 screenPosition)
    {
        if (!IsShowing || EventSystem.current == null)
            return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current) { position = screenPosition };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        for (int i = 0; i < results.Count; i++)
        {
            GameObject hitObject = results[i].gameObject;
            CustomButton button = hitObject != null
                ? hitObject.GetComponentInParent<CustomButton>()
                : null;
            if (IsInteractiveButton(button))
                return true;
        }

        return false;
    }

    private IEnumerator ObserveEntryCompleted(int scheduledVersion)
    {
        // Animator 在实例激活后的下一帧才保证已有有效当前状态。
        yield return null;

        if (entryAnimator != null && entryAnimator.runtimeAnimatorController != null)
        {
            while (IsPresentationCurrent(scheduledVersion))
            {
                AnimatorStateInfo state = entryAnimator.GetCurrentAnimatorStateInfo(0);
                if (!entryAnimator.IsInTransition(0) && state.normalizedTime >= 1f)
                    break;

                yield return null;
            }
        }

        if (!IsPresentationCurrent(scheduledVersion))
            yield break;

        presentationReady = true;
        onPresentationReady?.Invoke();
    }

    private bool IsPresentationCurrent(int scheduledVersion)
    {
        return scheduledVersion == presentationVersion && !closeRequested && isActiveAndEnabled;
    }

    private void BindButtons()
    {
        if (buttonsBound)
            return;

        buttonsBound = true;
        if (confirmButton != null)
        {
            confirmButtonPreviousBlockClick = confirmButton.IsBlockClick;
            confirmButton.OnClickDown += OnConfirmButtonDown;
        }

        if (cancelButton != null)
        {
            cancelButtonPreviousBlockClick = cancelButton.IsBlockClick;
            cancelButton.OnClickDown += OnCancelButtonDown;
        }
    }

    private void UnbindButtons()
    {
        if (!buttonsBound)
            return;

        buttonsBound = false;
        if (confirmButton != null)
        {
            confirmButton.OnClickDown -= OnConfirmButtonDown;
            confirmButton.IsBlockClick = confirmButtonPreviousBlockClick;
        }

        if (cancelButton != null)
        {
            cancelButton.OnClickDown -= OnCancelButtonDown;
            cancelButton.IsBlockClick = cancelButtonPreviousBlockClick;
        }
    }

    private void SetButtonBlocking(bool blockClick)
    {
        if (confirmButton != null)
            confirmButton.IsBlockClick = blockClick;
        if (cancelButton != null)
            cancelButton.IsBlockClick = blockClick;
    }

    private void OnConfirmButtonDown(CustomButton button)
    {
        BeginButtonFeedback(button, Choice.Confirm);
    }

    private void OnCancelButtonDown(CustomButton button)
    {
        BeginButtonFeedback(button, Choice.Cancel);
    }

    private void BeginButtonFeedback(CustomButton button, Choice choice)
    {
        if (buttonFeedbackPending || closeRequested || button == null || !button.Interactable)
            return;

        buttonFeedbackPending = true;
        onButtonDown?.Invoke(choice);
        StartCoroutine(CompleteButtonFeedback(presentationVersion, choice));
    }

    private IEnumerator CompleteButtonFeedback(int scheduledVersion, Choice choice)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, buttonFeedbackSeconds));

        if (scheduledVersion != presentationVersion || !buttonFeedbackPending || closeRequested)
            yield break;

        Action<Choice> completed = onButtonFeedbackCompleted;
        CloseNow();
        completed?.Invoke(choice);
    }

    private bool IsInteractiveButton(CustomButton button)
    {
        return button != null
               && button.Interactable
               && (button == confirmButton || button == cancelButton);
    }

    private void CloseNow(Action completed = null)
    {
        if (closeRequested)
            return;

        closeRequested = true;
        presentationVersion++;
        buttonFeedbackPending = false;
        StopAllCoroutines();

        if (UIManager.Instance != null)
            UIManager.Instance.DestroyUI(this);
        else if (gameObject != null)
            Destroy(gameObject);

        completed?.Invoke();
    }
}
