using DG.Tweening;
using UnityEngine;

/// <summary>人工侧刺整排伸出；仅伸出完成且方块开始推进后由控制器执行破障。</summary>
[DisallowMultipleComponent]
public sealed class Level131PlatformSpike : MonoBehaviour
{
    [SerializeField] private float revealDuration = 0.25f;
    [SerializeField] private float retractDistance = 0.5f;

    private Vector3 extendedPosition;
    private Tween revealTween;
    public BoxCollider2D HitCollider { get; private set; }
    public bool IsExtended { get; private set; }

    public void InitializeSerialized()
    {
        extendedPosition = transform.localPosition;
        HitCollider = GetComponent<BoxCollider2D>();
        if (HitCollider != null)
        {
            HitCollider.isTrigger = true;
            HitCollider.enabled = false;
        }
        IsExtended = false;
    }

    public void Reveal()
    {
        revealTween?.Kill();
        IsExtended = false;
        if (HitCollider != null)
            HitCollider.enabled = false;
        transform.localPosition = extendedPosition - Vector3.right * retractDistance;
        gameObject.SetActive(true);
        revealTween = transform.DOLocalMoveX(extendedPosition.x, Mathf.Max(0.01f, revealDuration))
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                IsExtended = true;
                if (HitCollider != null)
                    HitCollider.enabled = true;
                revealTween = null;
            });
    }

    public void SetPaused(bool paused)
    {
        if (paused)
            revealTween?.Pause();
        else
            revealTween?.Play();
    }

    private void OnDisable()
    {
        revealTween?.Kill();
        revealTween = null;
        IsExtended = false;
        if (HitCollider != null)
            HitCollider.enabled = false;
    }

    private void OnDestroy()
    {
        revealTween?.Kill();
    }
}
