using DG.Tweening;
using UnityEngine;

/// <summary>
/// 售罄标识简单入场动画。
/// 独立脚本，方便在预制上复用与管理。
/// </summary>
public class ShopSoldOutAnim : MonoBehaviour
{
    [SerializeField] private RectTransform soldOutRoot;
    [SerializeField] private CanvasGroup group;

    private bool hasPlay = false;

    private Sequence _sequence;

    /// <summary>
    /// 播放售罄动画。
    /// </summary>
    public void Play()
    {
        if (soldOutRoot == null || group == null || hasPlay)
            return;

        _sequence?.Kill();

        _sequence = DOTween.Sequence();
        group.alpha = 0f;

        var pos = soldOutRoot.anchoredPosition;
        pos.y += 150f;
        soldOutRoot.anchoredPosition = pos;

        _sequence.Append(soldOutRoot.DOAnchorPosY(0f, .3f).SetUpdate(true));
        _sequence.JoinCallback(() =>
        {
            group.DOFade(1f, 0.5f).SetUpdate(true);
        });
        _sequence.Join(soldOutRoot.DOPunchRotation(Vector3.forward * 15f, .5f, 8, .5f).SetDelay(.1f).SetEase(Ease.OutBack).SetUpdate(true));

        _sequence.SetUpdate(true);
    }

    private void OnDestroy()
    {
        if (_sequence != null && _sequence.IsActive())
        {
            _sequence.Kill();
            _sequence = null;
        }
    }
}
