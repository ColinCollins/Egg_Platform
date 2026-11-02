using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 控制一个 Key 的显隐。
/// HasGetLevel61Key = true 的情况下不显示。其他条件下显示。
/// 显示情况下，调用 TryShow，设置一个显示延迟。延迟显示 KeyImage 1s 渐显，延迟 2s可以配置。
/// 点击 KeyBtn 后，HasGetLevel61Key 变为 true。播放一个 key 动画，要是向上跳一下，然后从正下方移除屏幕。然后 SetActive = false
/// 
/// </summary>
public class Level61ChoicePanelKeyCtrl : MonoBehaviour
{
    [SerializeField] private CustomButton keyBtn;

    [Header("Show")]
    [SerializeField, Min(0f)] private float displayDelay = 2f;
    [SerializeField, Min(0f)] private float fadeInDuration = 1f;

    [Header("Claim Animation")]
    [SerializeField, Min(0f)] private float upwardJumpDistance = 80f;
    [SerializeField, Min(0f)] private float upwardJumpDuration = 0.2f;
    [SerializeField, Min(0f)] private float downwardExitDuration = 0.45f;

    private RectTransform keyRect;
    private Graphic keyGraphic;
    private Sequence showSequence;
    private Sequence claimSequence;
    private Vector2 initialAnchoredPosition;
    private Vector3 initialLocalScale = Vector3.one;
    private bool hasInitialTransform;
    private bool isClaimable;
    private bool isClaiming;
    private bool isButtonBound;

    private void Awake()
    {
        CacheReferences();
        BindButton();
        ResetKeyVisual(false);
    }

    private void OnEnable()
    {
        CacheReferences();
        BindButton();
        ResetKeyVisual(false);
    }

    public void TryShow()
    {
        StopTweens();
        CacheReferences();
        BindButton();

        isClaimable = false;
        isClaiming = false;

        if (!HasRequiredReferences())
        {
            Debug.LogWarning("[Level61ChoicePanelKeyCtrl] TryShow skipped: keyBtn, key RectTransform, or key Graphic is missing.");
            HidePrompt();
            return;
        }

        if (!IsEligibleToShow())
        {
            HidePrompt();
            return;
        }

        PrepareKeyForShow();
        PlayDelayedShow();
    }

    private void OnKeyClick(CustomButton btn)
    {
        if (!isClaimable || isClaiming)
            return;

        var gameData = DB.GameData;
        if (gameData == null)
        {
            Debug.LogWarning("[Level61ChoicePanelKeyCtrl] Claim skipped: GameData is null.");
            HidePrompt();
            return;
        }

        if (gameData.HasGetLevel61Key)
        {
            HidePrompt();
            return;
        }

        gameData.HasGetLevel61Key = true;
        gameData.Save();

        isClaimable = false;
        isClaiming = true;
        SetButtonInteractable(false);
        PlayClaimAnimation();
    }

    private void PlayDelayedShow()
    {
        showSequence?.Kill();
        showSequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject);

        float delay = Mathf.Max(0f, displayDelay);
        if (delay > 0f)
            showSequence.AppendInterval(delay);

        showSequence.AppendCallback(() =>
        {
            if (!HasRequiredReferences() || !IsEligibleToShow())
            {
                HidePrompt();
                return;
            }

            PrepareKeyForShow();
        });

        float duration = Mathf.Max(0f, fadeInDuration);
        if (duration > 0f)
        {
            showSequence.Append(keyGraphic
                .DOFade(1f, duration)
                .SetEase(Ease.Linear));
        }
        else
        {
            showSequence.AppendCallback(() => SetKeyAlpha(1f));
        }

        showSequence.OnComplete(() =>
        {
            showSequence = null;

            if (!HasRequiredReferences() || !IsEligibleToShow())
            {
                HidePrompt();
                return;
            }

            isClaimable = true;
            SetButtonInteractable(true);
            SetKeyAlpha(1f);
        });
    }

    private void PlayClaimAnimation()
    {
        showSequence?.Kill();
        showSequence = null;

        claimSequence?.Kill();
        claimSequence = DOTween.Sequence()
            .SetUpdate(true)
            .SetLink(gameObject);

        ResetKeyTransform();
        SetKeyAlpha(1f);

        Vector2 upPosition = initialAnchoredPosition + Vector2.up * Mathf.Max(0f, upwardJumpDistance);
        Vector2 downPosition = initialAnchoredPosition + Vector2.down * GetDownExitDistance();

        float upDuration = Mathf.Max(0f, upwardJumpDuration);
        if (upDuration > 0f)
        {
            claimSequence.Append(keyRect
                .DOAnchorPos(upPosition, upDuration)
                .SetEase(Ease.OutQuad));
        }
        else
        {
            claimSequence.AppendCallback(() => keyRect.anchoredPosition = upPosition);
        }

        float downDuration = Mathf.Max(0f, downwardExitDuration);
        if (downDuration > 0f)
        {
            claimSequence.Append(keyRect
                .DOAnchorPos(downPosition, downDuration)
                .SetEase(Ease.InQuad));
        }
        else
        {
            claimSequence.AppendCallback(() => keyRect.anchoredPosition = downPosition);
        }

        claimSequence.OnComplete(() =>
        {
            claimSequence = null;
            isClaiming = false;
            HidePrompt();
        });
    }

    private bool IsEligibleToShow()
    {
        var gameData = DB.GameData;
        if (gameData == null)
            return false;

        return !gameData.HasGetLevel61Key;
    }

    private bool HasRequiredReferences()
    {
        return keyBtn != null && keyRect != null && keyGraphic != null;
    }

    private void PrepareKeyForShow()
    {
        if (keyBtn != null)
            keyBtn.gameObject.SetActive(true);

        ResetKeyTransform();
        SetKeyAlpha(0f);
        SetButtonInteractable(false);
    }

    private void HidePrompt()
    {
        StopTweens();
        isClaimable = false;
        isClaiming = false;
        ResetKeyVisual(false);
    }

    private void ResetKeyVisual(bool visible)
    {
        ResetKeyTransform();
        SetKeyAlpha(visible ? 1f : 0f);
        SetButtonInteractable(false);

        if (keyBtn != null)
            keyBtn.gameObject.SetActive(visible);
    }

    private void ResetKeyTransform()
    {
        if (keyRect == null || !hasInitialTransform)
            return;

        keyRect.anchoredPosition = initialAnchoredPosition;
        keyRect.localScale = initialLocalScale;
    }

    private void SetKeyAlpha(float alpha)
    {
        if (keyGraphic == null)
            return;

        Color color = keyGraphic.color;
        color.a = Mathf.Clamp01(alpha);
        keyGraphic.color = color;
    }

    private void SetButtonInteractable(bool interactable)
    {
        if (keyBtn != null)
            keyBtn.Interactable = interactable;
    }

    private void CacheReferences()
    {
        if (keyBtn == null)
            return;

        if (keyRect == null)
            keyRect = keyBtn.GetComponent<RectTransform>();

        if (keyGraphic == null)
            keyGraphic = keyBtn.BGraphic != null ? keyBtn.BGraphic : keyBtn.GetComponent<Graphic>();

        if (!hasInitialTransform && keyRect != null)
        {
            initialAnchoredPosition = keyRect.anchoredPosition;
            initialLocalScale = keyRect.localScale;
            hasInitialTransform = true;
        }
    }

    private float GetDownExitDistance()
    {
        float canvasHeight = 0f;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.transform is RectTransform canvasRect)
            canvasHeight = Mathf.Abs(canvasRect.rect.height);

        if (canvasHeight <= 0f)
            canvasHeight = Screen.height > 0 ? Screen.height : 720f;

        float keyHeight = keyRect != null ? Mathf.Abs(keyRect.rect.height) : 0f;
        return canvasHeight + keyHeight + 100f;
    }

    private void BindButton()
    {
        if (keyBtn == null || isButtonBound)
            return;

        keyBtn.OnClick -= OnKeyClick;
        keyBtn.OnClick += OnKeyClick;
        isButtonBound = true;
    }

    private void UnbindButton()
    {
        if (keyBtn == null || !isButtonBound)
            return;

        keyBtn.OnClick -= OnKeyClick;
        isButtonBound = false;
    }

    private void StopTweens()
    {
        showSequence?.Kill();
        showSequence = null;

        claimSequence?.Kill();
        claimSequence = null;
    }

    private void OnDisable()
    {
        StopTweens();
        isClaimable = false;
        isClaiming = false;
        ResetKeyVisual(false);
        UnbindButton();
    }

    private void OnDestroy()
    {
        StopTweens();
        UnbindButton();
    }
}
