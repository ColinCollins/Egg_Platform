using DG.Tweening;
using UnityEngine;

/// <summary>
/// UI <see cref="RectTransform"/> 锚点移动：在 XY 上以 DOTween 从「静置位置 + startOffset」插值到「静置位置 + endOffset」。
/// 静置锚点在 <see cref="Awake"/> 时从当前 <c>anchoredPosition</c> 读取；布局变化后可调用 <see cref="CaptureRestPosition"/>。
/// </summary>
[DisallowMultipleComponent]
public class MoveAnimation : MonoBehaviour
{
    [Tooltip("为空则使用本物体上的 RectTransform")]
    [SerializeField]
    private RectTransform target;

    [Header("位移（相对静置 anchoredPosition 的偏移，支持 X/Y）")]
    [SerializeField]
    private Vector2 startOffset;

    [SerializeField]
    private Vector2 endOffset;

    [Header("时间")]
    [SerializeField]
    private float delay;

    [SerializeField]
    private float duration = 0.5f;

    [Header("曲线")]
    [SerializeField]
    private Ease ease = Ease.OutQuad;

    [SerializeField]
    private bool ignoreTimeScale = true;

    [Header("播放")]
    [Tooltip("每次 OnEnable 自动 Play")]
    [SerializeField]
    private bool playOnEnable = true;

    private Vector2 _restAnchored;
    private Tweener _tweener;

    private void Awake()
    {
        var rt = ResolveTarget();
        if (rt != null)
            _restAnchored = rt.anchoredPosition;
    }

    private void OnEnable()
    {
        if (playOnEnable)
            Play();
    }

    private void OnDisable()
    {
        KillTween();
    }

    private void OnDestroy()
    {
        KillTween();
    }

    private RectTransform ResolveTarget()
    {
        if (target != null)
            return target;
        return transform as RectTransform;
    }

    /// <summary>以当前 anchoredPosition 为新的静置点（例如父 Layout 重建后）。</summary>
    public void CaptureRestPosition()
    {
        var rt = ResolveTarget();
        if (rt != null)
            _restAnchored = rt.anchoredPosition;
    }

    /// <summary>从 startOffset 播放到 endOffset（相对静置点）。</summary>
    public void Play()
    {
        var rt = ResolveTarget();
        if (rt == null || duration < 0f)
            return;

        KillTween();
        rt.anchoredPosition = _restAnchored + startOffset;

        var endPos = _restAnchored + endOffset;
        _tweener = rt.DOAnchorPos(endPos, duration)
            .SetDelay(Mathf.Max(0f, delay))
            .SetEase(ease)
            .SetUpdate(ignoreTimeScale);
    }

    public void KillTween()
    {
        if (_tweener != null && _tweener.IsActive())
            _tweener.Kill();
        _tweener = null;
    }
}
