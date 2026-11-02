using System;
using System.Collections;
using System.Collections.Generic;
using Bear.UI;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Level 105 专用来电假弹窗：断网假弹窗移除后 Box 继续右行，到达中间路程节点停住后，
/// 从屏幕顶部外下滑入仿系统来电横幅（头像 + 来电人名称 + 接听/挂断双诱饵按钮）。
/// 它是无遮罩横幅：进入真实 Top UI 层但不携带全屏遮罩，游戏画面完整可见，
/// 展示期间暂停/重来/提示白名单保持安全。绿色「接听」与红色「挂断」按钮都不会执行
/// 任何电话或共享弹窗业务，点击一律由 Level 105 主控制器判负。
/// 与 <see cref="Level105UpdatePopup"/> 结构同构但刻意不共用代码，避免两个诱惑点耦合。
/// </summary>
public sealed class Level105CallPopup : BaseUIView
{
    public enum Choice
    {
        Answer,
        Decline
    }

    [SerializeField] private CustomButton answerButton;
    [SerializeField] private CustomButton declineButton;
    [SerializeField] private RectTransform bannerRoot;
    [SerializeField] private float buttonFeedbackSeconds = 0.2f;
    [SerializeField] private float entrySlideSeconds = 0.35f;
    [SerializeField] private float entryStartOffsetY = 300f;

    private Action<Choice> onButtonDown;
    private Action<Choice> onButtonFeedbackCompleted;
    private Action onPresentationReady;
    private bool buttonsBound;
    private bool answerButtonPreviousBlockClick;
    private bool declineButtonPreviousBlockClick;
    private bool buttonFeedbackPending;
    private bool presentationReady;
    private bool closeRequested;
    private bool entryTargetCaptured;
    private Vector2 entryTargetPosition;
    private int presentationVersion;

    /// <summary>当前实例仍在 UI 栈中显示且尚未请求关闭。</summary>
    public bool IsShowing => !closeRequested && isActiveAndEnabled && IsOpen && IsVisible;

    /// <summary>
    /// 便捷打开入口；资源名固定为 nameof(Level105CallPopup)。
    /// 也可由调用方直接使用 UIManager.OpenUI&lt;Level105CallPopup&gt;。
    /// </summary>
    public static Level105CallPopup Open(
        Action<Choice> buttonDown = null,
        Action<Choice> buttonFeedbackCompleted = null,
        Action presentationReadyCallback = null,
        UILayer layer = UILayer.Top)
    {
        Level105CallPopup popup = UIManager.Instance?.OpenUI<Level105CallPopup>(
            nameof(Level105CallPopup),
            layer);
        popup?.ConfigureCallbacks(buttonDown, buttonFeedbackCompleted, presentationReadyCallback);
        return popup;
    }

    /// <summary>
    /// 配置本次展示回调。buttonDown 在按下同帧触发；buttonFeedbackCompleted 在约 0.2 秒
    /// 原按钮反馈结束且弹窗已关闭后触发；presentationReadyCallback 在横幅下滑入场完成后触发。
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

    /// <summary>接收入场完成通知；若入场已经完成，配置时立即补发一次。</summary>
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
        StartCoroutine(SlideInAndObserveEntry(presentationVersion));
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
    /// 判断屏幕坐标是否命中「接听」或「挂断」按钮。横幅本体与画面空白不在本视图判定，
    /// 由 Level 105 主控制器作为普通屏幕操作判负。
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

    /// <summary>横幅自屏幕顶部外下滑入场；使用真实时间，系统面板挂起不阻断入场表现。</summary>
    private IEnumerator SlideInAndObserveEntry(int scheduledVersion)
    {
        if (bannerRoot != null)
        {
            if (!entryTargetCaptured)
            {
                entryTargetCaptured = true;
                entryTargetPosition = bannerRoot.anchoredPosition;
            }

            Vector2 startPosition = entryTargetPosition + new Vector2(0f, Mathf.Max(0f, entryStartOffsetY));
            bannerRoot.anchoredPosition = startPosition;

            float duration = Mathf.Max(0.01f, entrySlideSeconds);
            float elapsed = 0f;
            while (IsPresentationCurrent(scheduledVersion) && elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // ease-out：逼近目标时减速，模拟系统横幅落位。
                float eased = 1f - (1f - t) * (1f - t);
                bannerRoot.anchoredPosition = Vector2.LerpUnclamped(startPosition, entryTargetPosition, eased);
                yield return null;
            }

            if (IsPresentationCurrent(scheduledVersion))
                bannerRoot.anchoredPosition = entryTargetPosition;
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
        if (answerButton != null)
        {
            answerButtonPreviousBlockClick = answerButton.IsBlockClick;
            answerButton.OnClickDown += OnAnswerButtonDown;
        }

        if (declineButton != null)
        {
            declineButtonPreviousBlockClick = declineButton.IsBlockClick;
            declineButton.OnClickDown += OnDeclineButtonDown;
        }
    }

    private void UnbindButtons()
    {
        if (!buttonsBound)
            return;

        buttonsBound = false;
        if (answerButton != null)
        {
            answerButton.OnClickDown -= OnAnswerButtonDown;
            answerButton.IsBlockClick = answerButtonPreviousBlockClick;
        }

        if (declineButton != null)
        {
            declineButton.OnClickDown -= OnDeclineButtonDown;
            declineButton.IsBlockClick = declineButtonPreviousBlockClick;
        }
    }

    private void SetButtonBlocking(bool blockClick)
    {
        if (answerButton != null)
            answerButton.IsBlockClick = blockClick;
        if (declineButton != null)
            declineButton.IsBlockClick = blockClick;
    }

    private void OnAnswerButtonDown(CustomButton button)
    {
        BeginButtonFeedback(button, Choice.Answer);
    }

    private void OnDeclineButtonDown(CustomButton button)
    {
        BeginButtonFeedback(button, Choice.Decline);
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
               && (button == answerButton || button == declineButton);
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
