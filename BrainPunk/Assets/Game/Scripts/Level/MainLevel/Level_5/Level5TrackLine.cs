using DG.Tweening;
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class Level5TrackLine : MonoBehaviour
{
    [SerializeField] private Transform StartPoint;
    [SerializeField] private Transform EndPoint;

    public Transform GetStartPoint() => StartPoint;
    public Transform GetEndPoint() => EndPoint;

    public void SetStartPoint(Transform point) => StartPoint = point;
    public void SetEndPoint(Transform point) => EndPoint = point;

    private LineRenderer lineRenderer;
    private Color originalStartColor;
    private Color originalEndColor;
    private Tweener fadeTweener;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 2;
            originalStartColor = lineRenderer.startColor;
            originalEndColor = lineRenderer.endColor;
        }
    }

    public void DefaultFadeOut()
    {
        Debug.Log("Line Fade Out --------- ");
        FadeOut(0.3f);
    }

    /// <summary>
    /// 设置线段透明度，0 完全透明，1 为原始透明度。
    /// </summary>
    public void SetAlpha(float alpha)
    {
        if (lineRenderer == null) return;
        float a = Mathf.Clamp01(alpha);
        lineRenderer.startColor = new Color(originalStartColor.r, originalStartColor.g, originalStartColor.b, originalStartColor.a * a);
        lineRenderer.endColor = new Color(originalEndColor.r, originalEndColor.g, originalEndColor.b, originalEndColor.a * a);
    }

    /// <summary>
    /// 透明度从 1 渐变到 0，duration 为持续时间（秒）。
    /// </summary>
    public void FadeOut(float duration)
    {
        if (lineRenderer == null) return;
        fadeTweener?.Kill();
        float currentAlpha = 1f;
        fadeTweener = DOTween.To(() => currentAlpha, x =>
        {
            currentAlpha = x;
            SetAlpha(x);
        }, 0f, Mathf.Max(0f, duration)).SetEase(Ease.Linear);
    }

    void Update()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
                return;
            originalStartColor = lineRenderer.startColor;
            originalEndColor = lineRenderer.endColor;
        }

        // 实时更新 positionCount
        if (lineRenderer.positionCount != 2)
        {
            lineRenderer.positionCount = 2;
        }

        // 实时更新 position list（自动转换为本地坐标）
        if (StartPoint != null && EndPoint != null)
        {
            lineRenderer.SetPosition(0, transform.InverseTransformPoint(StartPoint.position));
            lineRenderer.SetPosition(1, transform.InverseTransformPoint(EndPoint.position));
        }
    }

    void OnDestroy()
    {
        fadeTweener?.Kill();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }

        if (lineRenderer != null)
        {
            if (lineRenderer.positionCount != 2)
            {
                lineRenderer.positionCount = 2;
            }

            if (StartPoint != null && EndPoint != null)
            {
                lineRenderer.SetPosition(0, transform.InverseTransformPoint(StartPoint.position));
                lineRenderer.SetPosition(1, transform.InverseTransformPoint(EndPoint.position));
            }
        }
    }
#endif
}
