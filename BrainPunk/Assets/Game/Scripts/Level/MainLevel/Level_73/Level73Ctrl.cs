using System.Collections;
using DG.Tweening;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level073 专用的长按蓄力跳跃控制器。
/// </summary>
public sealed class Level73Ctrl : MonoBehaviour
{
    #region Inspector

    // 蓄力条根节点；仅在长按达到阈值后显示。
    [SerializeField] private GameObject chargePanel;

    // 渐变填充图；保持原始比例，由本关遮罩从底部向上裁切显示。
    [SerializeField] private SpriteRenderer chargeFill;

    // 蓄力填充遮罩；仅作用于填充图，避免缩放渐变素材本身。
    [SerializeField] private SpriteMask chargeMask;

    // 蓄力条出现和消失时的本关淡入淡出时长，单位为秒。
    [SerializeField, Min(0.01f)] private float chargePanelFadeSeconds = 0.12f;

    // 按住多久后开始蓄力，单位为秒。
    [SerializeField, Min(0f)] private float longHoldSeconds = 1.5f;

    // 蓄力条从空到满所需时间，单位为秒。
    [SerializeField, Min(0.01f)] private float chargeFillSeconds = 4.5f;

    // 蓄力跳的前冲速度下限；普通短按跳跃不会使用该值。
    [SerializeField, Min(0f)] private float minimumChargedForwardSpeed = 2f;

    // 蓄力跳的前冲速度上限；随蓄力进度线性增加。
    [SerializeField, Min(0f)] private float maximumChargedForwardSpeed = 24f;

    // 刚进入蓄力时松手所使用的跳跃竖直速度。
    [SerializeField] private float minimumChargedJumpForce = 18f;

    // 蓄力条填满后松手所使用的跳跃竖直速度。
    [SerializeField] private float maximumChargedJumpForce = 30f;

    // 满蓄力后角色必须飞到的关卡终点。
    [SerializeField] private Transform endPoint;

    // 满蓄力飞向终点的表现时长，单位为秒。
    [SerializeField, Min(0.01f)] private float endpointFlightDuration = 2f;

    // 满蓄力飞向终点时的跳跃弧线高度。
    [SerializeField, Min(0f)] private float endpointFlightJumpPower = 5.5f;

    // 蓄力期间循环播放的角色 Spine 动画。
    [SerializeField] private string chargeAnimationName = "jump_ready_3";

    // 蓄力动作进入和退出时的本关 Spine 混合时长，单位为秒。
    [SerializeField, Min(0f)] private float chargeAnimationMixSeconds = 0.15f;

    #endregion

    #region Runtime State

    private const int MousePointerId = -1;
    private const int ChargeAnimationTrackIndex = 1;

    private BaseLevelCtrl levelCtrl;
    private ActorCtrl actor;
    private CustomButton jumpButton;
    private RectTransform jumpButtonRect;
    private Canvas jumpButtonCanvas;
    private Vector3 fillInitialScale;
    private Vector3 fillInitialLocalPosition;
    private Vector3 maskInitialScale;
    private Vector3 maskInitialLocalPosition;
    private float maskHeight;
    private SpriteRenderer[] chargePanelRenderers;
    private float holdStartedAt;
    private float chargeStartedAt;
    private float chargedJumpReleasedAt;
    private float chargePanelAlpha;
    private int activePointerId;
    private int chargePanelFadeVersion;
    private Tween endpointFlightTween;
    private Tween chargePanelFadeTween;
    private bool jumpButtonEnabled;
    private bool jumpButtonInteractable;
    private bool isHolding;
    private bool isCharging;
    private bool isChargedJumpInFlight;
    private bool isEndpointFlight;
    private bool chargeAnimationQueued;
    private bool endpointFlightBodyWasSimulated;
    private bool endpointFlightWasCtrlable;
    private bool hasDisabledSharedJumpButton;
    private bool hasCachedChargeFillState;
    private bool isChargePanelVisible;

    #endregion

    private IEnumerator Start()
    {
        levelCtrl = GetComponent<BaseLevelCtrl>();
        actor = levelCtrl != null ? levelCtrl.Actor : null;

        CacheChargeFillState();
        CacheChargePanelFadeState();
        HideChargePanelImmediately();

        while (Application.isPlaying && (PlayCtrl.Instance == null || PlayCtrl.Instance.CurrentGamePlayPanel == null))
        {
            yield return null;
        }

        if (!Application.isPlaying || PlayCtrl.Instance == null)
        {
            yield break;
        }

        BindJumpButton(PlayCtrl.Instance.CurrentGamePlayPanel.transform);
    }

    private void Update()
    {
        UpdateChargedJumpFlight();

        if (isHolding && !CanControlActor())
        {
            CancelHold();
            return;
        }

        UpdateJumpInput();

        if (isHolding && !isCharging && Time.time >= holdStartedAt + longHoldSeconds)
        {
            BeginCharging();
        }
    }

    private void LateUpdate()
    {
        if (!isCharging)
        {
            return;
        }

        SetChargePresentation(true, GetChargeAmount());
        EnsureChargeAnimation();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            CancelHold();
        }
    }

    private void OnDisable()
    {
        CancelHold();
        CancelEndpointFlight();
        HideChargePanelImmediately();
    }

    private void UpdateJumpInput()
    {
        if (jumpButtonRect == null || !hasDisabledSharedJumpButton)
        {
            return;
        }

        if (!isHolding)
        {
            TryBeginHoldFromTouch();
            TryBeginHoldFromMouse();
            return;
        }

        if (activePointerId == MousePointerId)
        {
            if (Input.GetMouseButtonUp(0))
            {
                FinishHold(true);
            }

            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId != activePointerId)
            {
                continue;
            }

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                FinishHold(true);
            }

            return;
        }

        CancelHold();
    }

    private void TryBeginHoldFromTouch()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Began && IsJumpButtonScreenPoint(touch.position))
            {
                BeginHold(touch.fingerId);
                return;
            }
        }
    }

    private void TryBeginHoldFromMouse()
    {
        if (Input.touchCount == 0 && Input.GetMouseButtonDown(0) && IsJumpButtonScreenPoint(Input.mousePosition))
        {
            BeginHold(MousePointerId);
        }
    }

    private bool IsJumpButtonScreenPoint(Vector2 screenPoint)
    {
        Camera eventCamera = null;
        if (jumpButtonCanvas != null && jumpButtonCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            eventCamera = jumpButtonCanvas.worldCamera;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(jumpButtonRect, screenPoint, eventCamera);
    }

    private void BeginHold(int pointerId)
    {
        if (!CanControlActor())
        {
            return;
        }

        activePointerId = pointerId;
        isHolding = true;
        holdStartedAt = Time.time;
    }

    private void BeginCharging()
    {
        if (!CanControlActor())
        {
            CancelHold();
            return;
        }

        isCharging = true;
        chargeStartedAt = Time.time;
        actor.StopMoving();
        SetChargePresentation(true, 0f);

        EnsureChargeAnimation();
    }

    private void FinishHold(bool applyJump)
    {
        bool wasCharging = isCharging;
        float chargeAmount = wasCharging ? GetChargeAmount() : 0f;

        if (wasCharging)
        {
            ClearChargeAnimation();
        }

        isHolding = false;
        isCharging = false;
        activePointerId = MousePointerId;
        SetChargePresentation(false, 0f);

        if (!applyJump || !CanControlActor())
        {
            RestoreMovementAfterCanceledCharge(wasCharging);
            return;
        }

        if (!wasCharging)
        {
            actor.TriggerJump();
            return;
        }

        if (actor.RB == null)
        {
            RestoreMovementAfterCanceledCharge(true);
            return;
        }

        if (chargeAmount >= 1f && BeginEndpointFlight())
        {
            return;
        }

        BeginChargedJumpFlight(chargeAmount);
    }

    private void CancelHold()
    {
        FinishHold(false);
    }

    private bool CanControlActor()
    {
        return Time.timeScale > 0f
               && !isChargedJumpInFlight
               && !isEndpointFlight
               && actor != null
               && !actor.IsDied
               && (levelCtrl == null || levelCtrl.ActorCtrlable);
    }

    private void BeginChargedJumpFlight(float chargeAmount)
    {
        if (actor == null || actor.RB == null)
        {
            RestoreMovementAfterCanceledCharge(true);
            return;
        }

        // 二次曲线降低蓄力初段的增幅，满蓄力仍保留原有最高力度。
        float chargeStrength = chargeAmount * chargeAmount;
        float forwardSpeed = Mathf.Lerp(minimumChargedForwardSpeed, maximumChargedForwardSpeed, chargeStrength);
        float jumpForce = Mathf.Lerp(minimumChargedJumpForce, maximumChargedJumpForce, chargeStrength);
        float direction = actor.FaingRight ? 1f : -1f;

        // StopMoving 让 ActorCtrl 不会在每帧覆写蓄力跳的前冲速度。
        actor.SetMoveInput(0f);
        actor.StopMoving();
        actor.RB.linearVelocity = new Vector2(direction * forwardSpeed, jumpForce);

        isChargedJumpInFlight = true;
        chargedJumpReleasedAt = Time.time;
        AudioManager.PlaySound("jump");
    }

    private void UpdateChargedJumpFlight()
    {
        if (!isChargedJumpInFlight)
        {
            return;
        }

        if (actor == null || actor.IsDied)
        {
            isChargedJumpInFlight = false;
            return;
        }

        if (Time.time < chargedJumpReleasedAt + 0.05f)
        {
            return;
        }

        if (actor.IsGround && (actor.RB == null || actor.RB.linearVelocityY <= 0.01f))
        {
            isChargedJumpInFlight = false;
            actor.SetMoveInput(0f);
            actor.ResumeMoving();
        }
    }

    private bool BeginEndpointFlight()
    {
        if (actor == null || endPoint == null)
        {
            Debug.LogWarning("[Level73Ctrl] EndPoint is missing; falling back to the regular charged jump.");
            return false;
        }

        isEndpointFlight = true;
        endpointFlightWasCtrlable = levelCtrl == null || levelCtrl.ActorCtrlable;
        levelCtrl?.SetCtrlable(false);

        actor.SetMoveInput(0f);
        actor.StopMoving();

        if (actor.RB != null)
        {
            endpointFlightBodyWasSimulated = actor.RB.simulated;
            actor.RB.linearVelocity = Vector2.zero;
            actor.RB.simulated = false;
        }

        Vector3 targetPosition = endPoint.position;
        targetPosition.z = actor.transform.position.z;

        actor.transform.DOKill();
        endpointFlightTween = actor.transform
            .DOJump(
                targetPosition,
                Mathf.Max(0f, endpointFlightJumpPower),
                1,
                Mathf.Max(0.01f, endpointFlightDuration))
            .SetEase(Ease.Linear)
            .SetLink(gameObject)
            .OnComplete(CompleteEndpointFlight);

        AudioManager.PlaySound("jump");
        return true;
    }

    private void CompleteEndpointFlight()
    {
        if (!isEndpointFlight)
        {
            return;
        }

        endpointFlightTween = null;
        isEndpointFlight = false;

        if (actor != null && endPoint != null)
        {
            Vector3 targetPosition = endPoint.position;
            targetPosition.z = actor.transform.position.z;
            actor.transform.position = targetPosition;
        }

        levelCtrl?.SuccessStrightly();
    }

    private void CancelEndpointFlight()
    {
        if (!isEndpointFlight)
        {
            return;
        }

        isEndpointFlight = false;
        endpointFlightTween?.Kill();
        endpointFlightTween = null;

        if (actor != null && actor.RB != null)
        {
            actor.RB.simulated = endpointFlightBodyWasSimulated;
            actor.RB.linearVelocity = Vector2.zero;
        }

        if (levelCtrl != null)
        {
            levelCtrl.SetCtrlable(endpointFlightWasCtrlable);
        }

        if (actor != null && !actor.IsDied && (levelCtrl == null || endpointFlightWasCtrlable))
        {
            actor.SetMoveInput(0f);
            actor.ResumeMoving();
        }
    }

    private void RestoreMovementAfterCanceledCharge(bool wasCharging)
    {
        if (!wasCharging
            || isChargedJumpInFlight
            || isEndpointFlight
            || actor == null
            || actor.IsDied
            || (levelCtrl != null && !levelCtrl.ActorCtrlable))
        {
            return;
        }

        actor.SetMoveInput(0f);
        actor.ResumeMoving();
    }

    private float GetChargeAmount()
    {
        if (!isCharging)
        {
            return 0f;
        }

        return Mathf.Clamp01((Time.time - chargeStartedAt) / chargeFillSeconds);
    }

    private void EnsureChargeAnimation()
    {
        if (string.IsNullOrEmpty(chargeAnimationName) || actor == null || actor.SpineCtrl == null || actor.SpineCtrl.AnimationState == null)
        {
            return;
        }

        if (chargeAnimationQueued)
        {
            return;
        }

        // ActorCtrl 每帧使用轨道 0 驱动常规状态动画；蓄力使用本关独占的轨道 1。
        // 先放入空动画再排入蓄力循环，才能让轨道 1 从轨道 0 的常规动作平滑淡入。
        var animationState = actor.SpineCtrl.AnimationState;
        animationState.SetEmptyAnimation(ChargeAnimationTrackIndex, 0f);
        Spine.TrackEntry chargeEntry = animationState.AddAnimation(ChargeAnimationTrackIndex, chargeAnimationName, true, 0f);
        chargeEntry.SetMixDuration(Mathf.Max(0f, chargeAnimationMixSeconds), 0f);
        chargeAnimationQueued = true;
    }

    private void ClearChargeAnimation()
    {
        bool wasQueued = chargeAnimationQueued;
        chargeAnimationQueued = false;

        if (actor == null || actor.SpineCtrl == null || actor.SpineCtrl.AnimationState == null)
        {
            return;
        }

        Spine.TrackEntry currentEntry = actor.SpineCtrl.AnimationState.GetCurrent(ChargeAnimationTrackIndex);
        if (wasQueued || (currentEntry != null && currentEntry.Animation != null && currentEntry.Animation.Name == chargeAnimationName))
        {
            actor.SpineCtrl.AnimationState.SetEmptyAnimation(
                ChargeAnimationTrackIndex,
                Mathf.Max(0f, chargeAnimationMixSeconds));
        }
    }

    private void CacheChargeFillState()
    {
        if (chargeFill != null)
        {
            fillInitialScale = chargeFill.transform.localScale;
            fillInitialLocalPosition = chargeFill.transform.localPosition;
            chargeFill.maskInteraction = chargeMask != null
                ? SpriteMaskInteraction.VisibleInsideMask
                : SpriteMaskInteraction.None;
        }

        hasCachedChargeFillState = true;
        if (chargeMask == null)
        {
            return;
        }

        maskInitialScale = chargeMask.transform.localScale;
        maskInitialLocalPosition = chargeMask.transform.localPosition;
        maskHeight = chargeMask.sprite != null ? chargeMask.sprite.bounds.size.y : 0f;
    }

    private void SetChargePresentation(bool visible, float amount)
    {
        if (visible)
        {
            ShowChargePanel();
            SetChargeFillAmount(amount);
            return;
        }

        HideChargePanel();
    }

    private void SetChargeFillAmount(float amount)
    {
        if (chargeFill == null)
        {
            return;
        }

        chargeFill.transform.localScale = fillInitialScale;
        chargeFill.transform.localPosition = fillInitialLocalPosition;

        if (chargeMask == null)
        {
            chargeFill.enabled = true;
            return;
        }

        float fillAmount = Mathf.Clamp01(amount);
        Vector3 scale = maskInitialScale;
        scale.y *= fillAmount;
        chargeMask.transform.localScale = scale;

        Vector3 position = maskInitialLocalPosition;
        position.y -= maskHeight * maskInitialScale.y * (1f - fillAmount) * 0.5f;
        chargeMask.transform.localPosition = position;
        chargeFill.enabled = fillAmount > 0f;
    }

    private void CacheChargePanelFadeState()
    {
        if (chargePanel == null)
        {
            return;
        }

        chargePanelRenderers = chargePanel.GetComponentsInChildren<SpriteRenderer>(true);
        SetChargePanelAlpha(1f);
    }

    private void ShowChargePanel()
    {
        if (chargePanel == null || (isChargePanelVisible && chargePanel.activeSelf))
        {
            return;
        }

        chargePanelFadeVersion++;
        chargePanelFadeTween?.Kill();
        chargePanelFadeTween = null;

        if (!chargePanel.activeSelf)
        {
            chargePanel.SetActive(true);
            SetChargePanelAlpha(0f);
        }

        isChargePanelVisible = true;
        chargePanelFadeTween = FadeChargePanelTo(1f, Ease.OutQuad);
    }

    private void HideChargePanel()
    {
        if (chargePanel == null || !chargePanel.activeSelf)
        {
            isChargePanelVisible = false;
            ResetChargeFill();
            return;
        }

        if (!isChargePanelVisible)
        {
            return;
        }

        isChargePanelVisible = false;
        int fadeVersion = ++chargePanelFadeVersion;
        chargePanelFadeTween?.Kill();
        chargePanelFadeTween = FadeChargePanelTo(0f, Ease.InQuad);
        if (chargePanelFadeTween == null)
        {
            CompleteChargePanelHide(fadeVersion);
            return;
        }

        chargePanelFadeTween.OnComplete(() => CompleteChargePanelHide(fadeVersion));
    }

    private Tween FadeChargePanelTo(float targetAlpha, Ease ease)
    {
        if (chargePanelRenderers == null || chargePanelRenderers.Length == 0)
        {
            SetChargePanelAlpha(targetAlpha);
            return null;
        }

        return DOTween.To(
                () => chargePanelAlpha,
                SetChargePanelAlpha,
                targetAlpha,
                Mathf.Max(0.01f, chargePanelFadeSeconds))
            .SetEase(ease)
            .SetLink(gameObject);
    }

    private void CompleteChargePanelHide(int fadeVersion)
    {
        if (fadeVersion != chargePanelFadeVersion || isChargePanelVisible)
        {
            return;
        }

        chargePanelFadeTween = null;
        if (chargePanel != null)
        {
            chargePanel.SetActive(false);
        }

        ResetChargeFill();
        SetChargePanelAlpha(1f);
    }

    private void HideChargePanelImmediately()
    {
        chargePanelFadeVersion++;
        chargePanelFadeTween?.Kill();
        chargePanelFadeTween = null;
        isChargePanelVisible = false;

        if (chargePanel != null)
        {
            chargePanel.SetActive(false);
        }

        ResetChargeFill();
        SetChargePanelAlpha(1f);
    }

    private void ResetChargeFill()
    {
        if (!hasCachedChargeFillState || chargeFill == null)
        {
            return;
        }

        chargeFill.transform.localScale = fillInitialScale;
        chargeFill.transform.localPosition = fillInitialLocalPosition;
        chargeFill.enabled = false;

        if (chargeMask == null)
        {
            return;
        }

        Vector3 scale = maskInitialScale;
        scale.y = 0f;
        chargeMask.transform.localScale = scale;

        Vector3 position = maskInitialLocalPosition;
        position.y -= maskHeight * maskInitialScale.y * 0.5f;
        chargeMask.transform.localPosition = position;
    }

    private void SetChargePanelAlpha(float alpha)
    {
        chargePanelAlpha = Mathf.Clamp01(alpha);
        if (chargePanelRenderers == null)
        {
            return;
        }

        for (int i = 0; i < chargePanelRenderers.Length; i++)
        {
            SpriteRenderer renderer = chargePanelRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            Color color = renderer.color;
            color.a = chargePanelAlpha;
            renderer.color = color;
        }
    }

    private void BindJumpButton(Transform panelTransform)
    {
        if (panelTransform == null)
        {
            Debug.LogWarning("[Level73Ctrl] GamePlayPanel transform is missing.");
            return;
        }

        Transform buttonTransform = panelTransform.Find("Root/jump_btn");
        jumpButton = buttonTransform != null ? buttonTransform.GetComponent<CustomButton>() : null;
        jumpButtonRect = buttonTransform as RectTransform;

        if (jumpButton == null || jumpButtonRect == null)
        {
            Debug.LogWarning("[Level73Ctrl] Root/jump_btn is missing or is not a RectTransform.");
            return;
        }

        jumpButtonCanvas = jumpButton.GetComponentInParent<Canvas>();
        jumpButtonEnabled = jumpButton.enabled;
        jumpButtonInteractable = jumpButton.Interactable;

        // Interactable 不会拦截 OnPointerDown；同时禁用组件，避免子 Graphic 的射线事件冒泡到共享跳跃回调。
        jumpButton.Interactable = false;
        jumpButton.enabled = false;
        hasDisabledSharedJumpButton = true;
    }

    private void OnDestroy()
    {
        CancelEndpointFlight();
        CancelHold();

        if (jumpButton != null && hasDisabledSharedJumpButton)
        {
            jumpButton.Interactable = jumpButtonInteractable;
            jumpButton.enabled = jumpButtonEnabled;
        }
    }
}
