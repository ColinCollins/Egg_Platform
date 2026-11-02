using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Common;
using UnityEngine;
using UnityEngine.UI;

public class CommonUIRewardItem : MonoBehaviour, IRecycle
{
    [SerializeField] private Image icon;
    [SerializeField] private CanvasGroup canvasGroup;

    private RectTransform _rectTransform;
    private Sequence _sequence;
    private UniTaskCompletionSource<bool> _playCompletionSource;

    public RectTransform RectTransform
    {
        get
        {
            if (_rectTransform == null)
                _rectTransform = transform as RectTransform;

            return _rectTransform;
        }
    }

    private void Awake()
    {
        if (icon == null)
            icon = GetComponentInChildren<Image>(true);

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void SetIcon(Sprite sprite)
    {
        if (icon != null)
            icon.sprite = sprite;
    }

    public UniTask<bool> PlayAsync(
        Vector2 startAnchoredPosition,
        Vector2 scatterAnchoredPosition,
        Vector2 controlAnchoredPosition,
        Vector2 endAnchoredPosition,
        float scatterDuration,
        float flyDuration,
        float startScale,
        float scatterScale,
        float endScale,
        bool useUnscaledTime)
    {
        KillSequence(completeTask: true);

        RectTransform.anchoredPosition = startAnchoredPosition;
        RectTransform.localScale = Vector3.one * startScale;
        RectTransform.localRotation = Quaternion.identity;
        canvasGroup.alpha = 1f;

        _playCompletionSource = new UniTaskCompletionSource<bool>();

        _sequence = DOTween.Sequence();
        _sequence.SetUpdate(useUnscaledTime);
        _sequence.Append(RectTransform.DOAnchorPos(scatterAnchoredPosition, scatterDuration).SetEase(Ease.OutCubic));
        _sequence.Join(RectTransform.DOScale(scatterScale, scatterDuration).SetEase(Ease.OutBack));

        Tweener flyTweener = DOTween.To(
                () => 0f,
                t => RectTransform.anchoredPosition = EvaluateQuadratic(scatterAnchoredPosition, controlAnchoredPosition, endAnchoredPosition, t),
                1f,
                flyDuration)
            .SetEase(Ease.InOutSine)
            .SetUpdate(useUnscaledTime);

        _sequence.Append(flyTweener);
        _sequence.Join(RectTransform.DOScale(endScale, flyDuration).SetEase(Ease.InQuad));
        _sequence.Insert(scatterDuration + flyDuration * 0.55f, canvasGroup.DOFade(0f, flyDuration * 0.45f).SetEase(Ease.InQuad));
        _sequence.OnComplete(() => OnSequenceFinished(completed: true));
        _sequence.OnKill(() => OnSequenceFinished(completed: false));

        return _playCompletionSource.Task;
    }

    public void OnRecycle()
    {
        KillSequence(completeTask: true);
        RectTransform.localScale = Vector3.one;
        RectTransform.localRotation = Quaternion.identity;
        canvasGroup.alpha = 1f;
        gameObject.SetActive(false);
    }

    public void OnSpawn()
    {
        gameObject.SetActive(true);
        RectTransform.localScale = Vector3.one;
        RectTransform.localRotation = Quaternion.identity;
        canvasGroup.alpha = 1f;
    }

    private void OnDestroy()
    {
        KillSequence(completeTask: true);
    }

    private void OnSequenceFinished(bool completed)
    {
        _sequence = null;
        _playCompletionSource?.TrySetResult(completed);
        _playCompletionSource = null;
    }

    private void KillSequence(bool completeTask)
    {
        if (_sequence != null)
        {
            Sequence sequence = _sequence;
            _sequence = null;
            sequence.Kill();
        }

        if (completeTask)
        {
            _playCompletionSource?.TrySetResult(false);
            _playCompletionSource = null;
        }
    }

    private static Vector2 EvaluateQuadratic(Vector2 start, Vector2 control, Vector2 end, float t)
    {
        float oneMinusT = 1f - t;
        return oneMinusT * oneMinusT * start
            + 2f * oneMinusT * t * control
            + t * t * end;
    }
}
