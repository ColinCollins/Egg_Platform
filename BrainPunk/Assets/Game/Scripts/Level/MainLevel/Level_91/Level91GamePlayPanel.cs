using System;
using System.Threading;
using Bear.EventSystem;
using Bear.UI;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Level091 专用 Gameplay HUD。
/// 公共暂停弹窗打开后，仅把当前弹窗实例的左侧 Sfx Toggle 改绑为门铃。
/// </summary>
public sealed class Level91GamePlayPanel : GamePlayPanel, IEventSender
{
    #region Constants

    private const string PauseButtonPath = "Root/pause_btn";
    private const string SfxToggleName = "Sfx_toggle";
    private const string GameSettingPopupPath = "GameSettingPopup";
    private const string DoorbellSfxTag = "l91Doorbell";
    private static readonly Vector2 DingDongRaysTarget = new Vector2(-55f, 55f);
    private static readonly Vector2 DingDongTextTarget = new Vector2(160f, 55f);
    private static readonly Vector2 DingDongRaysBaseSize = new Vector2(108f, 164f);
    private static readonly Vector2 DingDongTextBaseSize = new Vector2(205f, 140f);

    #endregion

    #region Inspector

    /// <summary>
    /// Ding Dong 整组表现相对暂停页 Sfx_toggle 视觉中心的偏移。
    /// 仅用于 Level091，策划可在 GamePlayPanel_091 Inspector 中直接调整 X/Y。
    /// </summary>
    [Tooltip("Ding Dong 整组表现相对 Sfx_toggle 视觉中心的偏移；只调整本关，不影响共享暂停页。")]
    [SerializeField] private Vector2 dingDongOffsetFromSfx = new Vector2(130f, -55f);

    /// <summary>
    /// Ding Dong 放射线与文字的统一尺寸倍率。
    /// 策划可在 GamePlayPanel_091 Inspector 中调整，不改变动画时序。
    /// </summary>
    [Tooltip("Ding Dong 放射线与文字的统一尺寸倍率；1.5 表示放大到原尺寸的 150%。")]
    [Min(0.1f)]
    [SerializeField] private float dingDongVisualScale = 1.5f;

    /// <summary>
    /// Ding Dong 文字相对原目标位置的局部偏移。
    /// 仅移动文字，不影响左侧放射线与整组锚点。
    /// </summary>
    [Tooltip("Ding Dong 文字相对原目标位置的局部偏移；X 为负时向左移动。")]
    [SerializeField] private Vector2 dingDongTextLocalOffset = new Vector2(-55f, 0f);

    #endregion

    #region Runtime State

    private CustomButton pauseButton;
    private Toggle currentSfxToggle;
    private GameSettingPopup currentSettingPopup;
    private Level91Ctrl levelCtrl;
    private GameObject dingDongFxRoot;
    private Image dingDongRaysImage;
    private Image dingDongTextImage;
    private Tween dingDongTween;
    private CancellationTokenSource panelCancellation;
    private bool specialInputEnabled = true;
    private bool doorbellInProgress;
    private int doorbellFlowVersion;
    private Vector3 doorbellButtonBaseScale = Vector3.one;

    #endregion

    #region Lifecycle

    public override void OnCreate()
    {
        base.OnCreate();
        ResolveReferences();
        BindPauseButton();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        ResetPanelCancellation();
        specialInputEnabled = true;
        doorbellInProgress = false;
        ResolveReferences();
        BindPauseButton();
        SetSpecialInputEnabled(true);
    }

    public override void OnClose()
    {
        doorbellFlowVersion++;
        CancelPanelTasks();

        if (pauseButton != null)
        {
            pauseButton.OnClick -= OnPauseButtonClicked;
            pauseButton.Interactable = true;
        }

        CleanupDingDongFx();
        ReleaseDoorbellToggleBinding(true);
        specialInputEnabled = true;
        doorbellInProgress = false;
        base.OnClose();
    }

    #endregion

    #region Level091 API

    /// <summary>
    /// 设置 Level091 的暂停按钮和当前门铃是否可交互。
    /// 失败、冲刺、击飞及门铃播放期间由 Level91Ctrl 关闭。
    /// </summary>
    public void SetSpecialInputEnabled(bool enabled)
    {
        specialInputEnabled = enabled;
        ResolveReferences();

        if (pauseButton != null)
            pauseButton.Interactable = enabled;

        RefreshDoorbellInteractable();
    }

    #endregion

    #region Reference And Binding

    private void ResolveReferences()
    {
        if (pauseButton == null)
        {
            Transform pauseTransform = transform.Find(PauseButtonPath);
            if (pauseTransform != null)
                pauseButton = pauseTransform.GetComponent<CustomButton>();
        }

        if (levelCtrl == null)
            levelCtrl = FindFirstObjectByType<Level91Ctrl>();
    }

    private void BindPauseButton()
    {
        if (pauseButton == null)
            return;

        pauseButton.OnClick -= OnPauseButtonClicked;
        pauseButton.OnClick += OnPauseButtonClicked;
    }

    private void OnPauseButtonClicked(CustomButton _)
    {
        if (!specialInputEnabled || doorbellInProgress)
            return;

        CancellationToken cancellationToken = panelCancellation != null
            ? panelCancellation.Token
            : this.GetCancellationTokenOnDestroy();
        BindDoorbellToOpenedPopupAsync(cancellationToken).Forget();
    }

    private async UniTaskVoid BindDoorbellToOpenedPopupAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (TryBindDoorbellToOpenedPopup())
                    return;

                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // HUD 关闭或关卡重开时停止查找暂停弹窗。
        }
    }

    private bool TryBindDoorbellToOpenedPopup()
    {
        currentSettingPopup = UIManager.Instance != null
            ? UIManager.Instance.GetUI<GameSettingPopup>(GameSettingPopupPath)
            : null;

        if (currentSettingPopup == null || !currentSettingPopup.gameObject.activeInHierarchy)
            return false;

        Toggle[] toggles = currentSettingPopup.GetComponentsInChildren<Toggle>(true);
        currentSfxToggle = null;
        for (int i = 0; i < toggles.Length; i++)
        {
            if (toggles[i].name == SfxToggleName)
            {
                currentSfxToggle = toggles[i];
                break;
            }
        }

        if (currentSfxToggle == null)
            return false;

        // 只改当前 Level091 暂停弹窗实例；普通关卡下次 OnEnable 会恢复共享 Sfx 逻辑。
        CleanupDingDongFx();
        currentSfxToggle.onValueChanged.RemoveAllListeners();
        currentSfxToggle.SetIsOnWithoutNotify(false);
        doorbellButtonBaseScale = currentSfxToggle.transform.localScale;
        currentSfxToggle.onValueChanged.AddListener(OnDoorbellToggleChanged);
        RefreshDoorbellInteractable();
        return true;
    }

    private void ReleaseDoorbellToggleBinding(bool closePopup)
    {
        if (currentSfxToggle != null)
        {
            currentSfxToggle.transform.DOKill();
            currentSfxToggle.transform.localScale = doorbellButtonBaseScale;
            currentSfxToggle.onValueChanged.RemoveListener(OnDoorbellToggleChanged);
            currentSfxToggle.enabled = true;
            currentSfxToggle.interactable = true;
        }

        if (closePopup &&
            currentSettingPopup != null &&
            currentSettingPopup.gameObject.activeInHierarchy &&
            UIManager.Instance != null)
        {
            UIManager.Instance.CloseUI(currentSettingPopup);
        }

        currentSfxToggle = null;
        currentSettingPopup = null;
    }

    #endregion

    #region Doorbell Flow

    private void RefreshDoorbellInteractable()
    {
        if (currentSfxToggle == null)
            return;

        // 本关把音效 Toggle 当作实体门铃。禁用组件可拦截失败演出输入，同时不触发置灰视觉。
        currentSfxToggle.enabled = specialInputEnabled || doorbellInProgress;
        currentSfxToggle.interactable = true;
    }

    private void OnDoorbellToggleChanged(bool _)
    {
        if (currentSfxToggle != null)
            currentSfxToggle.SetIsOnWithoutNotify(false);

        if (!specialInputEnabled && !doorbellInProgress)
            return;

        ResolveReferences();
        if (doorbellInProgress ||
            levelCtrl == null ||
            !levelCtrl.CanPlayDoorbellFeedback)
        {
            RefreshDoorbellInteractable();
            return;
        }

        AudioManager.PlaySound(DoorbellSfxTag);
        bool shouldOpenDoor = levelCtrl.TryBeginDoorbell();
        doorbellInProgress = true;
        int flowVersion = ++doorbellFlowVersion;
        RefreshDoorbellInteractable();
        CancellationToken cancellationToken = panelCancellation != null
            ? panelCancellation.Token
            : this.GetCancellationTokenOnDestroy();
        PlayDingDongAsync(
            levelCtrl,
            flowVersion,
            shouldOpenDoor,
            cancellationToken).Forget();
    }

    private async UniTaskVoid PlayDingDongAsync(
        Level91Ctrl owner,
        int flowVersion,
        bool shouldOpenDoor,
        CancellationToken cancellationToken)
    {
        try
        {
            CreateDingDongFx(owner);
            if (dingDongFxRoot == null)
            {
                await UniTask.WaitForSeconds(
                    0.1f,
                    true,
                    cancellationToken: cancellationToken);
            }
            else
            {
                RectTransform buttonRect = currentSfxToggle != null
                    ? currentSfxToggle.transform as RectTransform
                    : null;
                RectTransform raysRect = dingDongRaysImage != null
                    ? dingDongRaysImage.rectTransform
                    : null;
                RectTransform textRect = dingDongTextImage != null
                    ? dingDongTextImage.rectTransform
                    : null;

                Vector2 raysTarget = DingDongRaysTarget;
                Vector2 textTarget = DingDongTextTarget + dingDongTextLocalOffset;

                if (buttonRect != null)
                    buttonRect.localScale = doorbellButtonBaseScale;

                if (raysRect != null)
                {
                    raysRect.anchoredPosition = raysTarget + Vector2.left * 58f;
                    raysRect.localScale = Vector3.one * 0.72f;
                    dingDongRaysImage.color = new Color(1f, 1f, 1f, 0f);
                }

                if (textRect != null)
                {
                    textRect.anchoredPosition = textTarget + Vector2.left * 96f;
                    textRect.localScale = Vector3.one * 0.82f;
                    dingDongTextImage.color = new Color(1f, 1f, 1f, 0f);
                }

                Sequence sequence = DOTween.Sequence()
                    .SetUpdate(true)
                    .SetLink(dingDongFxRoot, LinkBehaviour.KillOnDestroy);

                if (buttonRect != null)
                {
                    sequence.Append(
                        buttonRect
                            .DOScale(doorbellButtonBaseScale * 0.86f, 0.07f)
                            .SetEase(Ease.InQuad));
                    sequence.Append(
                        buttonRect
                            .DOScale(doorbellButtonBaseScale * 1.08f, 0.1f)
                            .SetEase(Ease.OutBack));
                    sequence.Append(
                        buttonRect
                            .DOScale(doorbellButtonBaseScale, 0.08f)
                            .SetEase(Ease.OutQuad));
                }

                if (raysRect != null)
                {
                    sequence.Insert(
                        0.04f,
                        raysRect.DOAnchorPos(raysTarget, 0.22f).SetEase(Ease.OutCubic));
                    sequence.Insert(
                        0.04f,
                        raysRect.DOScale(1f, 0.22f).SetEase(Ease.OutBack));
                    sequence.Insert(
                        0.04f,
                        dingDongRaysImage.DOFade(1f, 0.16f).SetEase(Ease.OutQuad));
                }

                if (textRect != null)
                {
                    sequence.Insert(
                        0.16f,
                        textRect.DOAnchorPos(textTarget, 0.28f).SetEase(Ease.OutCubic));
                    sequence.Insert(
                        0.16f,
                        textRect.DOScale(1f, 0.28f).SetEase(Ease.OutBack));
                    sequence.Insert(
                        0.16f,
                        dingDongTextImage.DOFade(1f, 0.2f).SetEase(Ease.OutQuad));
                }

                sequence.AppendInterval(0.3f);

                if (raysRect != null)
                {
                    sequence.Append(
                        raysRect
                            .DOAnchorPosX(raysTarget.x + 24f, 0.16f)
                            .SetEase(Ease.InQuad));
                    sequence.Join(
                        dingDongRaysImage.DOFade(0f, 0.14f).SetEase(Ease.InQuad));
                }

                if (textRect != null)
                {
                    sequence.Join(
                        textRect
                            .DOAnchorPosX(textTarget.x + 30f, 0.2f)
                            .SetEase(Ease.InQuad));
                    sequence.Join(
                        dingDongTextImage.DOFade(0f, 0.18f).SetEase(Ease.InQuad));
                }

                dingDongTween = sequence;

                await AwaitTweenAsync(sequence, cancellationToken);
            }

            if (shouldOpenDoor &&
                flowVersion == doorbellFlowVersion &&
                IsOpen &&
                owner != null)
            {
                owner.CompleteDoorbell();
            }
        }
        catch (OperationCanceledException)
        {
            // 玩家主动关闭暂停弹窗时仍完成已按下的门铃；关卡销毁取消时不再写状态。
            if (shouldOpenDoor &&
                !cancellationToken.IsCancellationRequested &&
                flowVersion == doorbellFlowVersion &&
                IsOpen &&
                owner != null)
            {
                owner.CompleteDoorbell();
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Level91GamePlayPanel] Ding Dong 表现异常：{exception}");
            if (shouldOpenDoor &&
                flowVersion == doorbellFlowVersion &&
                IsOpen &&
                owner != null)
            {
                owner.CompleteDoorbell();
            }
        }
        finally
        {
            if (flowVersion == doorbellFlowVersion)
            {
                doorbellInProgress = false;
                CleanupDingDongFx();
            }
        }
    }

    #endregion

    #region Ding Dong Presentation

    private void CreateDingDongFx(Level91Ctrl owner)
    {
        CleanupDingDongFx();
        if (owner == null ||
            (owner.DingDongRaysSprite == null && owner.DingDongTextSprite == null))
        {
            return;
        }

        dingDongFxRoot = new GameObject(
            "Level91_DingDongFx",
            typeof(RectTransform));

        RectTransform buttonRect = currentSfxToggle != null
            ? currentSfxToggle.transform as RectTransform
            : null;
        RectTransform effectParent = currentSettingPopup != null
            ? currentSettingPopup.transform as RectTransform
            : null;
        if (buttonRect == null || effectParent == null)
        {
            Destroy(dingDongFxRoot);
            dingDongFxRoot = null;
            return;
        }

        dingDongFxRoot.layer = effectParent.gameObject.layer;
        RectTransform rootRect = dingDongFxRoot.transform as RectTransform;
        rootRect.SetParent(effectParent, false);
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.position = buttonRect.TransformPoint(buttonRect.rect.center);
        rootRect.anchoredPosition += dingDongOffsetFromSfx;
        Vector3 localPosition = rootRect.localPosition;
        localPosition.z = 0f;
        rootRect.localPosition = localPosition;
        rootRect.sizeDelta = new Vector2(430f, 180f);
        rootRect.SetAsLastSibling();

        dingDongRaysImage = CreateFxImage(
            "Rays",
            owner.DingDongRaysSprite,
            rootRect,
            DingDongRaysBaseSize * Mathf.Max(0.1f, dingDongVisualScale),
            DingDongRaysTarget);
        dingDongTextImage = CreateFxImage(
            "Text",
            owner.DingDongTextSprite,
            rootRect,
            DingDongTextBaseSize * Mathf.Max(0.1f, dingDongVisualScale),
            DingDongTextTarget + dingDongTextLocalOffset);
    }

    private static Image CreateFxImage(
        string objectName,
        Sprite sprite,
        RectTransform parent,
        Vector2 size,
        Vector2 anchoredPosition)
    {
        if (sprite == null)
            return null;

        GameObject imageObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        imageObject.layer = parent.gameObject.layer;

        RectTransform rect = imageObject.transform as RectTransform;
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private static async UniTask AwaitTweenAsync(
        Tween tween,
        CancellationToken cancellationToken)
    {
        var completionSource = new UniTaskCompletionSource<bool>();
        tween.OnComplete(() => completionSource.TrySetResult(true));
        tween.OnKill(() => completionSource.TrySetResult(false));

        bool completed;
        try
        {
            completed = await completionSource.Task.AttachExternalCancellation(cancellationToken);
        }
        finally
        {
            if (cancellationToken.IsCancellationRequested && tween.IsActive())
                tween.Kill();
        }

        if (!completed)
            throw new OperationCanceledException(cancellationToken);
    }

    private void CleanupDingDongFx()
    {
        if (dingDongTween != null && dingDongTween.IsActive())
            dingDongTween.Kill();

        dingDongTween = null;

        if (currentSfxToggle != null)
        {
            currentSfxToggle.transform.DOKill();
            currentSfxToggle.transform.localScale = doorbellButtonBaseScale;
        }

        if (dingDongFxRoot != null)
            Destroy(dingDongFxRoot);

        dingDongFxRoot = null;
        dingDongRaysImage = null;
        dingDongTextImage = null;
    }

    #endregion

    #region Cancellation

    private void ResetPanelCancellation()
    {
        CancelPanelTasks();
        panelCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());
    }

    private void CancelPanelTasks()
    {
        if (panelCancellation == null)
            return;

        panelCancellation.Cancel();
        panelCancellation.Dispose();
        panelCancellation = null;
    }

    #endregion
}
