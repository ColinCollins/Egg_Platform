using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Level108 下箭头拖拽组件；命中公共跳跃按钮后安装，未命中则回到场景初始位置。
/// </summary>
public sealed class Level108DownArrowDrag : DragableItem
{
    #region Inspector

    [Tooltip("Level108 总控；拖拽结束时由它判定是否命中跳跃按钮。")]
    [SerializeField] private Level108Ctrl owner;

    [Tooltip("下箭头渲染器，用于精确点击、拖动层级和安装后的 UI 图标替换。")]
    [SerializeField] private SpriteRenderer arrowRenderer;

    [Tooltip("无效投放返回初始位置的持续时间。")]
    [SerializeField, Min(0.05f)] private float returnDuration = 0.24f;

    [Tooltip("拖动时临时增加的渲染顺序。")]
    [SerializeField, Min(1)] private int draggingOrderOffset = 300;

    [Tooltip("拖动时使用的屏幕顶层 Canvas 排序值，确保箭头不会被操作按钮遮住。")]
    [SerializeField] private int dragOverlaySortingOrder = 32760;

    #endregion

    #region Runtime State

    private Tween returnTween;
    private Vector3 originWorldPosition;
    private int originSortingOrder;
    private bool hasCachedOrigin;
    private bool installed;
    private GameObject dragOverlayRoot;
    private Image dragOverlayImage;

    /// <summary>
    /// 下箭头 Sprite，供 Level108Ctrl 安装成功后替换公共跳跃按钮图标。
    /// </summary>
    public Sprite ArrowSprite
    {
        get
        {
            EnsureCached();
            return arrowRenderer != null ? arrowRenderer.sprite : null;
        }
    }

    /// <summary>
    /// 当前场景箭头在屏幕上的实际显示尺寸，安装到 UI 时用于保持视觉大小不跳变。
    /// </summary>
    public Vector2 ArrowScreenSize
    {
        get
        {
            EnsureCached();
            return CalculateArrowScreenSize();
        }
    }

    #endregion

    #region Drag Overrides

    protected override bool IsTouchOnObject(Vector2 screenPosition)
    {
        EnsureCached();
        if (arrowRenderer == null)
            return base.IsTouchOnObject(screenPosition);

        Vector3 worldPosition = ScreenToWorldPosition(screenPosition);
        return arrowRenderer.bounds.Contains(new Vector3(
            worldPosition.x,
            worldPosition.y,
            arrowRenderer.bounds.center.z));
    }

    protected override void OnDragStart()
    {
        EnsureCached();
        returnTween?.Kill();
        returnTween = null;
        if (arrowRenderer != null)
            arrowRenderer.sortingOrder = originSortingOrder + draggingOrderOffset;
        CreateDragOverlay();
        base.OnDragStart();
    }

    protected override void OnDragUpdate()
    {
        UpdateDragOverlayPosition();
        base.OnDragUpdate();
    }

    protected override void OnDragEnd()
    {
        base.OnDragEnd();
        if (installed)
            return;

        if (owner != null && owner.TryInstallDownArrow(this))
            return;

        DestroyDragOverlay(true);
        ReturnToOrigin();
    }

    #endregion

    #region Install And Reset

    /// <summary>
    /// 完成下箭头安装并关闭场景实例。
    /// 调用边界：仅由 Level108Ctrl 在确认命中 jump_btn 后调用。
    /// 注意事项：按钮上的最终显示由 Level108Ctrl 使用同一 Sprite 接管。
    /// </summary>
    public void CompleteInstallation()
    {
        if (installed)
            return;

        EnsureCached();
        installed = true;
        SetEnableDrag(false);
        returnTween?.Kill();
        returnTween = null;
        DestroyDragOverlay(false);
        gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        DestroyDragOverlay(!installed);
    }

    private void CreateDragOverlay()
    {
        if (dragOverlayRoot != null || arrowRenderer == null || arrowRenderer.sprite == null)
            return;

        Vector2 overlaySize = CalculateArrowScreenSize();
        dragOverlayRoot = new GameObject("Level108_DownArrowDragOverlay", typeof(RectTransform), typeof(Canvas));
        Canvas canvas = dragOverlayRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = Mathf.Clamp(dragOverlaySortingOrder, short.MinValue, short.MaxValue);

        GameObject imageObject = new GameObject("Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(dragOverlayRoot.transform, false);
        dragOverlayImage = imageObject.GetComponent<Image>();
        dragOverlayImage.sprite = arrowRenderer.sprite;
        dragOverlayImage.preserveAspect = true;
        dragOverlayImage.raycastTarget = false;
        dragOverlayImage.color = arrowRenderer.color;
        dragOverlayImage.rectTransform.sizeDelta = overlaySize;

        arrowRenderer.enabled = false;
        UpdateDragOverlayPosition();
    }

    private Vector2 CalculateArrowScreenSize()
    {
        if (InputCamera == null || arrowRenderer == null)
            return Vector2.one * 96f;

        Bounds bounds = arrowRenderer.bounds;
        Vector3 min = InputCamera.WorldToScreenPoint(bounds.min);
        Vector3 max = InputCamera.WorldToScreenPoint(bounds.max);
        return new Vector2(
            Mathf.Max(1f, Mathf.Abs(max.x - min.x)),
            Mathf.Max(1f, Mathf.Abs(max.y - min.y)));
    }

    private void UpdateDragOverlayPosition()
    {
        if (dragOverlayImage == null || InputCamera == null)
            return;

        dragOverlayImage.rectTransform.position = InputCamera.WorldToScreenPoint(transform.position);
    }

    private void DestroyDragOverlay(bool restoreWorldRenderer)
    {
        if (arrowRenderer != null && restoreWorldRenderer)
            arrowRenderer.enabled = true;

        dragOverlayImage = null;
        if (dragOverlayRoot == null)
            return;

        GameObject root = dragOverlayRoot;
        dragOverlayRoot = null;
        if (Application.isPlaying)
            Destroy(root);
        else
            DestroyImmediate(root);
    }

    private void ReturnToOrigin()
    {
        EnsureCached();
        returnTween?.Kill();
        returnTween = transform.DOMove(originWorldPosition, Mathf.Max(0.05f, returnDuration))
            .SetEase(Ease.OutQuad)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                if (arrowRenderer != null)
                    arrowRenderer.sortingOrder = originSortingOrder;
                returnTween = null;
            });
    }

    private void EnsureCached()
    {
        if (hasCachedOrigin)
            return;

        arrowRenderer ??= GetComponent<SpriteRenderer>();
        originWorldPosition = transform.position;
        if (arrowRenderer != null)
            originSortingOrder = arrowRenderer.sortingOrder;
        hasCachedOrigin = true;
    }

    #endregion
}
