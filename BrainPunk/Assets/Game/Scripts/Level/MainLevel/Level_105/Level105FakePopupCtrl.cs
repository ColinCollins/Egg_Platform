using System;
using System.Collections.Generic;
using System.Threading;
using Bear.UI;
using Cysharp.Threading.Tasks;
using Game.Play;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Level 105 假弹窗编排适配器：通过 UIManager 打开关卡专属真实 Prefab Variant，
/// 使用本地 BaseUIView 类型和唯一 Resources 名称，保留遮罩、入场与按钮反馈，
/// 但不接触共享奖励/胜利业务。按钮按下同帧通知判负，约 0.2 秒反馈后销毁 UI。
/// </summary>
public sealed class Level105FakePopupCtrl : MonoBehaviour
{
    #region Constants and Types

    private const string CoinPopupResourceName = "Level105_CommonGetRewardPopup";
    private const string VictoryPopupResourceName = "Level105_GameVictoryPanel";
    private const string RewardVictoryPopupResourceName = "Level105_GameVictoryPanel_Reward";

    public enum PopupKind
    {
        CoinReward,
        Victory
    }

    private sealed class FakeButtonBinding
    {
        public CustomButton Button;
        public bool PreviousIsBlockClick;
    }

    #endregion

    #region Inspector

    [SerializeField] private PopupKind popupKind;
    [SerializeField] private int coinAmount = 100;
    [SerializeField] private float buttonFeedbackHoldSeconds = 0.2f;

    #endregion

    #region Runtime State

    private readonly List<FakeButtonBinding> boundButtons = new List<FakeButtonBinding>();
    private BaseUIView activePopup;
    private Action onFakeButtonPressed;
    private Action onFakeButtonFeedbackCompleted;
    private Action onPresentationReady;
    private bool buttonFailureScheduled;
    private int popupVersion;

    #endregion

    #region Public API

    /// <summary>当前是否存在由本适配器打开且尚未销毁的 Level 105 假弹窗。</summary>
    public bool IsShowing => activePopup != null;

    /// <summary>配置假按钮按下同帧与反馈结束后的判负回调；不接管真实 UI 业务回调。</summary>
    public void ConfigureFailureCallbacks(Action buttonPressed, Action buttonFeedbackCompleted)
    {
        onFakeButtonPressed = buttonPressed;
        onFakeButtonFeedbackCompleted = buttonFeedbackCompleted;
    }

    /// <summary>配置真实 Variant 入场完成回调；金币观察 Animator，胜利观察 Spine in 轨道。</summary>
    public void ConfigurePresentationReadyCallback(Action presentationReady)
    {
        onPresentationReady = presentationReady;
    }

    /// <summary>打开关卡专属纯展示 UI；创建失败时返回 false，调用方可安全降级避免永久卡关。</summary>
    public bool PlayIn()
    {
        CloseActivePopup();
        buttonFailureScheduled = false;

        activePopup = popupKind switch
        {
            PopupKind.CoinReward => OpenCoinRewardPresentation(),
            PopupKind.Victory => OpenVictoryPresentation(),
            _ => null
        };

        popupVersion++;
        if (activePopup == null)
            return false;

        BindFakeButtons(activePopup);
        WaitForPresentationReadyAsync(popupVersion).Forget();
        return true;
    }

    /// <summary>超时正常消失：只销毁本地展示，不触发按钮判负。</summary>
    public void PlayOut()
    {
        popupVersion++;
        buttonFailureScheduled = false;
        CloseActivePopup();
    }

    /// <summary>违规、重开或销毁时立即销毁本地展示。</summary>
    public void AbortAndHide()
    {
        popupVersion++;
        buttonFailureScheduled = false;
        CloseActivePopup();
    }

    /// <summary>
    /// 命中当前假弹窗可交互按钮时，由 CustomButton 按压链保留原反馈；
    /// 其余位置由 Level105Ctrl 作为普通屏幕操作立即判负。
    /// </summary>
    public bool IsScreenPositionOverInteractiveButton(Vector2 screenPosition)
    {
        if (activePopup == null || EventSystem.current == null || boundButtons.Count == 0)
            return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current) { position = screenPosition };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        for (int i = 0; i < results.Count; i++)
        {
            CustomButton button = results[i].gameObject != null
                ? results[i].gameObject.GetComponentInParent<CustomButton>()
                : null;
            if (button != null && button.Interactable && IsBoundFakeButton(button))
                return true;
        }

        return false;
    }

    #endregion

    #region Popup Creation

    private BaseUIView OpenCoinRewardPresentation()
    {
        Level105FakeRewardPopupView panel = UIManager.Instance?.OpenUI<Level105FakeRewardPopupView>(
            CoinPopupResourceName,
            UILayer.Top);
        panel?.SetCoinAmount(Mathf.Max(1, coinAmount));
        return panel;
    }

    private BaseUIView OpenVictoryPresentation()
    {
        string resourceName = ShouldUseRewardVictoryVariant()
            ? RewardVictoryPopupResourceName
            : VictoryPopupResourceName;

        return UIManager.Instance?.OpenUI<Level105FakeVictoryPopupView>(
            resourceName,
            UILayer.Popup);
    }

    /// <summary>只读判断本局真实结算会使用普通版还是首次通关奖励版，不写存档。</summary>
    private static bool ShouldUseRewardVictoryVariant()
    {
        int currentLevel = PlayCtrl.Instance?.Level?.CurrentLevel ?? 0;
        if (currentLevel <= 0)
            return false;

        List<int> passedLevels = DB.GameData?.PassedLevels;
        return passedLevels != null && !passedLevels.Contains(currentLevel);
    }

    #endregion

    #region Presentation Readiness

    private async UniTaskVoid WaitForPresentationReadyAsync(int scheduledVersion)
    {
        CancellationToken cancellationToken = this.GetCancellationTokenOnDestroy();

        try
        {
            switch (popupKind)
            {
                case PopupKind.CoinReward:
                    await WaitForCoinEntryAsync(scheduledVersion, cancellationToken);
                    break;
                case PopupKind.Victory:
                    await WaitForVictoryEntryAsync(scheduledVersion, cancellationToken);
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (IsCurrentPopup(scheduledVersion))
            onPresentationReady?.Invoke();
    }

    private async UniTask WaitForCoinEntryAsync(int scheduledVersion, CancellationToken cancellationToken)
    {
        Animator animator = ResolveCoinEntryAnimator();
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

        while (IsCurrentPopup(scheduledVersion))
        {
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
                return;

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (!animator.IsInTransition(0) && state.normalizedTime >= 1f)
                return;

            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }
    }

    private async UniTask WaitForVictoryEntryAsync(int scheduledVersion, CancellationToken cancellationToken)
    {
        UISpineCtrl spineCtrl = null;
        Spine.TrackEntry entry = null;
        UISpineCtrl[] spineCtrls = activePopup != null
            ? activePopup.GetComponentsInChildren<UISpineCtrl>(true)
            : Array.Empty<UISpineCtrl>();

        for (int i = 0; i < spineCtrls.Length; i++)
        {
            UISpineCtrl candidate = spineCtrls[i];
            Spine.TrackEntry candidateEntry = candidate?.GetAnimationState()?.GetCurrent(0);
            if (candidateEntry?.Animation == null ||
                !string.Equals(candidateEntry.Animation.Name, "in", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            spineCtrl = candidate;
            entry = candidateEntry;
            break;
        }

        if (spineCtrl == null || entry?.Animation == null)
            return;

        while (IsCurrentPopup(scheduledVersion))
        {
            Spine.TrackEntry currentEntry = spineCtrl.GetAnimationState()?.GetCurrent(0);
            if (!ReferenceEquals(currentEntry, entry) || entry.TrackTime >= entry.AnimationEnd)
                return;

            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
        }
    }

    private Animator ResolveCoinEntryAnimator()
    {
        if (activePopup == null)
            return null;

        Transform root = activePopup.transform.Find("Root");
        Animator animator = root != null ? root.GetComponent<Animator>() : null;
        return animator != null ? animator : activePopup.GetComponentInChildren<Animator>(true);
    }

    private bool IsCurrentPopup(int scheduledVersion)
    {
        return scheduledVersion == popupVersion && activePopup != null;
    }

    #endregion

    #region Fake Button Interception

    private void BindFakeButtons(BaseUIView popup)
    {
        UnbindFakeButtons();

        CustomButton[] buttons = popup.GetComponentsInChildren<CustomButton>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            CustomButton button = buttons[i];
            if (button == null)
                continue;

            // IsBlockClick 只阻断 OnPointerClick 业务；OnPointerDown/Up 的缩放、音效和震动仍保留。
            boundButtons.Add(new FakeButtonBinding
            {
                Button = button,
                PreviousIsBlockClick = button.IsBlockClick
            });
            button.IsBlockClick = true;
            button.OnClickDown += HandleFakeButtonDown;
        }
    }

    private void UnbindFakeButtons()
    {
        for (int i = 0; i < boundButtons.Count; i++)
        {
            FakeButtonBinding binding = boundButtons[i];
            CustomButton button = binding.Button;
            if (button == null)
                continue;

            button.OnClickDown -= HandleFakeButtonDown;
            button.IsBlockClick = binding.PreviousIsBlockClick;
        }

        boundButtons.Clear();
    }

    private bool IsBoundFakeButton(CustomButton button)
    {
        for (int i = 0; i < boundButtons.Count; i++)
        {
            if (boundButtons[i].Button == button)
                return true;
        }

        return false;
    }

    private void HandleFakeButtonDown(CustomButton button)
    {
        if (buttonFailureScheduled || button == null || !button.Interactable)
            return;

        buttonFailureScheduled = true;
        onFakeButtonPressed?.Invoke();

        int scheduledVersion = popupVersion;
        CompleteButtonFailureAfterFeedback(scheduledVersion).Forget();
    }

    private async UniTaskVoid CompleteButtonFailureAfterFeedback(int scheduledVersion)
    {
        try
        {
            await UniTask.Delay(
                TimeSpan.FromSeconds(Mathf.Max(0f, buttonFeedbackHoldSeconds)),
                ignoreTimeScale: true,
                cancellationToken: this.GetCancellationTokenOnDestroy());
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (scheduledVersion != popupVersion || !buttonFailureScheduled)
            return;

        popupVersion++;
        CloseActivePopup();
        onFakeButtonFeedbackCompleted?.Invoke();
    }

    #endregion

    #region Cleanup

    private void CloseActivePopup()
    {
        UnbindFakeButtons();

        BaseUIView popup = activePopup;
        activePopup = null;
        if (popup == null)
            return;

        if (UIManager.Instance != null)
            UIManager.Instance.DestroyUI(popup);
        else if (popup.gameObject != null)
            Destroy(popup.gameObject);
    }

    private void OnDestroy()
    {
        CloseActivePopup();
        onFakeButtonPressed = null;
        onFakeButtonFeedbackCompleted = null;
        onPresentationReady = null;
    }

    #endregion
}
