using System;
using DG.Tweening;
using Game.Scripts.Common;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screw item animation for the level 50 password popup.
/// 增加回调，动画播放完成后，触发回调。
/// </summary>
public class Level50ScrewItem : MonoBehaviour
{
    [SerializeField] private CustomButton button;
    [SerializeField] private RectTransform screwTop;
    [SerializeField] private Image screwHandle;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private float moveOffsetY = 120f;
    [SerializeField] private float moveDuration = 0.35f;
    [SerializeField] private float fillDuration = 0.35f;
    [SerializeField] private float fadeDuration = 0.2f;
    [SerializeField] private Ease moveEase = Ease.OutCubic;
    [SerializeField] private Ease fillEase = Ease.Linear;
    [SerializeField] private Ease fadeEase = Ease.OutQuad;

    private Vector2 _initialScrewTopAnchoredPosition;
    private float _initialFillAmount;
    private Sequence _playSequence;
    private bool _isPlayed;

    public bool Finished { get; private set; }
    public bool IsPlaying { get; private set; }
    public event Action<Level50ScrewItem> OnPlayFinished;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<CustomButton>();

        if (screwTop != null)
            _initialScrewTopAnchoredPosition = screwTop.anchoredPosition;

        if (screwHandle != null)
            _initialFillAmount = screwHandle.fillAmount;

        if (button != null)
            button.OnClick += OnClickPlay;
    }

    private void OnDestroy()
    {
        if (button != null)
            button.OnClick -= OnClickPlay;

        _playSequence?.Kill();
        OnPlayFinished = null;
    }

    private void OnClickPlay(CustomButton _)
    {
        Play();
    }

    public void Play()
    {
        if (_isPlayed)
            return;

        _isPlayed = true;
        Finished = false;
        IsPlaying = true;
        _playSequence?.Kill();

        if (button != null)
            button.Interactable = false;

        _playSequence = DOTween.Sequence().SetLink(gameObject);
        var syncedDuration = Mathf.Max(0f, moveDuration);

        if (screwHandle != null)
        {
            _playSequence.Join(screwTop.DORotate(new Vector3(0,0, -360f), syncedDuration, RotateMode.FastBeyond360));
            var rt = screwHandle.transform as RectTransform;
            _playSequence.Join(rt.DOAnchorPosY(
                  _initialScrewTopAnchoredPosition.y + moveOffsetY,
                  syncedDuration)
              .SetEase(moveEase));
            _playSequence.Join(screwHandle.DOFillAmount(1f, syncedDuration / 4f)
                .SetEase(fillEase));
        }

        if (group != null)
        {
            _playSequence.Append(group.DOFade(0f, Mathf.Max(0f, fadeDuration))
                .SetEase(fadeEase));
        }

        _playSequence.OnComplete(() =>
        {
            if (group != null)
            {
                group.alpha = 0f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            IsPlaying = false;
            Finished = true;
            OnPlayFinished?.Invoke(this);
        });

        AudioManager.PlaySound("fanSwitch");
    }

    public void ResetVisual(bool resetPlayedState = true)
    {
        _playSequence?.Kill();

        if (resetPlayedState)
            _isPlayed = false;

        Finished = false;
        IsPlaying = false;

        if (screwTop != null)
            screwTop.anchoredPosition = _initialScrewTopAnchoredPosition;

        if (screwHandle != null)
            screwHandle.fillAmount = _initialFillAmount;

        if (group != null)
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }

        if (button != null)
            button.Interactable = true;
    }

    public void SetFinishedCallback(Action<Level50ScrewItem> callback)
    {
        OnPlayFinished = callback;
    }
}
