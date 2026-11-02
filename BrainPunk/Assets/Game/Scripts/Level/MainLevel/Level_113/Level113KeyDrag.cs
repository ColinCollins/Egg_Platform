using Game.ItemEvent;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Level113 台面钥匙拖拽器；使用独立指针所有权，并用顶层 Overlay 保证拖动图标不被 UI 遮挡。
/// </summary>
public sealed class Level113KeyDrag : MonoBehaviour
{
    private const int NoPointer = int.MinValue;
    private const int MousePointer = -1;

    private Level113Ctrl owner;
    private SpriteRenderer keyRenderer;
    private Collider2D keyCollider;
    private MoveFloatHandle moveFloat;
    private GameObject overlayRoot;
    private Image overlayImage;
    private int activePointerId = NoPointer;
    private bool ready;
    private bool installed;

    /// <summary>
    /// 钥匙当前 Sprite；只供 Level113 总控替换 jump_btn 图标。
    /// </summary>
    public Sprite KeySprite => keyRenderer != null ? keyRenderer.sprite : null;

    /// <summary>
    /// 钥匙当前世界显示换算出的屏幕像素尺寸；只供按钮安装时保持视觉尺寸。
    /// </summary>
    public Vector2 KeyScreenSize => CalculateScreenSize();

    /// <summary>
    /// 当前是否由本组件持有一个鼠标或触摸拖拽。
    /// </summary>
    public bool IsDragging => activePointerId != NoPointer;

    #region Runtime Binding

    /// <summary>
    /// 绑定 Level113 运行时引用。
    /// 调用边界：只由 Level113Ctrl 初始化场景内既有钥匙时调用，不创建或替换资源。
    /// </summary>
    public void Configure(
        Level113Ctrl levelOwner,
        SpriteRenderer renderer,
        Collider2D collider,
        MoveFloatHandle floatHandle)
    {
        owner = levelOwner;
        keyRenderer = renderer != null ? renderer : GetComponent<SpriteRenderer>();
        keyCollider = collider != null ? collider : GetComponent<Collider2D>();
        moveFloat = floatHandle != null ? floatHandle : GetComponent<MoveFloatHandle>();
    }

    /// <summary>
    /// 设置钥匙是否处于台面可拖拽状态。
    /// 调用边界：跟随、飞行、安装和被消耗期间必须传 false；false 会安全取消当前拖拽。
    /// </summary>
    public void SetReady(bool value)
    {
        ready = value && !installed;
        if (!ready)
            CancelDrag(true);
    }

    /// <summary>
    /// 标记钥匙已安装到按钮并移交给 Level113 总控的飞行动画。
    /// 调用边界：仅在投放中心命中 jump_btn 后调用；之后不可再次拖拽。
    /// </summary>
    public void CompleteInstallation()
    {
        installed = true;
        ready = false;
        activePointerId = NoPointer;
        DestroyOverlay(false);
        if (keyCollider != null)
            keyCollider.enabled = false;
    }

    #endregion

    #region Input

    private void Update()
    {
        if (!ready || installed || owner == null || !owner.CanOperateKey(this))
        {
            if (IsDragging)
                CancelDrag(true);
            return;
        }

        if (Input.touchCount > 0)
        {
            HandleTouches();
            return;
        }

        HandleMouse();
    }

    private void HandleTouches()
    {
        if (activePointerId == MousePointer)
            CancelDrag(true);

        if (activePointerId == NoPointer)
        {
            for (int index = 0; index < Input.touchCount; index++)
            {
                Touch touch = Input.GetTouch(index);
                if (touch.phase == TouchPhase.Began && IsPointerOnKey(touch.position))
                {
                    BeginDrag(touch.fingerId, touch.position);
                    break;
                }
            }
        }

        if (activePointerId < 0)
            return;

        for (int index = 0; index < Input.touchCount; index++)
        {
            Touch touch = Input.GetTouch(index);
            if (touch.fingerId != activePointerId)
                continue;

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                EndDrag(touch.position);
            else
                UpdateOverlayPosition(touch.position);
            return;
        }

        CancelDrag(true);
    }

    private void HandleMouse()
    {
        if (activePointerId == NoPointer && Input.GetMouseButtonDown(0) && IsPointerOnKey(Input.mousePosition))
            BeginDrag(MousePointer, Input.mousePosition);

        if (activePointerId != MousePointer)
            return;

        if (Input.GetMouseButtonUp(0))
            EndDrag(Input.mousePosition);
        else if (Input.GetMouseButton(0))
            UpdateOverlayPosition(Input.mousePosition);
    }

    private void BeginDrag(int pointerId, Vector2 screenPosition)
    {
        if (!owner.CanOperateKey(this))
            return;

        activePointerId = pointerId;
        moveFloat?.PauseMove();
        if (keyCollider != null)
            keyCollider.enabled = false;
        CreateOverlay(screenPosition);
    }

    private void EndDrag(Vector2 screenPosition)
    {
        activePointerId = NoPointer;
        if (owner != null && owner.TryInstallKey(this, screenPosition))
            return;

        DestroyOverlay(true);
        if (keyCollider != null)
            keyCollider.enabled = true;
        moveFloat?.ResumeMove();
    }

    private void CancelDrag(bool restoreWorldRenderer)
    {
        activePointerId = NoPointer;
        DestroyOverlay(restoreWorldRenderer);
        if (ready && keyCollider != null)
            keyCollider.enabled = true;
        if (ready)
            moveFloat?.ResumeMove();
    }

    private bool IsPointerOnKey(Vector2 screenPosition)
    {
        Camera camera = owner != null ? owner.InputCamera : Camera.main;
        if (camera == null || keyRenderer == null || keyRenderer.sprite == null)
            return false;

        float depth = Mathf.Abs(camera.transform.position.z - transform.position.z);
        Vector3 world = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
        world.z = keyRenderer.bounds.center.z;
        return keyRenderer.bounds.Contains(world);
    }

    #endregion

    #region Drag Overlay

    private void CreateOverlay(Vector2 screenPosition)
    {
        if (overlayRoot != null || keyRenderer == null || keyRenderer.sprite == null)
            return;

        overlayRoot = new GameObject("Level113_KeyDragOverlay", typeof(RectTransform), typeof(Canvas));
        Canvas canvas = overlayRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32760;

        GameObject imageObject = new GameObject("Key", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(overlayRoot.transform, false);
        overlayImage = imageObject.GetComponent<Image>();
        overlayImage.sprite = keyRenderer.sprite;
        overlayImage.color = keyRenderer.color;
        overlayImage.preserveAspect = true;
        overlayImage.raycastTarget = false;
        overlayImage.rectTransform.sizeDelta = CalculateScreenSize();

        keyRenderer.enabled = false;
        UpdateOverlayPosition(screenPosition);
    }

    private void UpdateOverlayPosition(Vector2 screenPosition)
    {
        if (overlayImage != null)
            overlayImage.rectTransform.position = screenPosition;
    }

    private Vector2 CalculateScreenSize()
    {
        Camera camera = owner != null ? owner.InputCamera : Camera.main;
        if (camera == null || keyRenderer == null)
            return Vector2.one * 96f;

        Bounds bounds = keyRenderer.bounds;
        Vector3 min = camera.WorldToScreenPoint(bounds.min);
        Vector3 max = camera.WorldToScreenPoint(bounds.max);
        return new Vector2(
            Mathf.Max(1f, Mathf.Abs(max.x - min.x)),
            Mathf.Max(1f, Mathf.Abs(max.y - min.y)));
    }

    private void DestroyOverlay(bool restoreWorldRenderer)
    {
        if (restoreWorldRenderer && keyRenderer != null)
            keyRenderer.enabled = true;

        overlayImage = null;
        if (overlayRoot == null)
            return;

        GameObject root = overlayRoot;
        overlayRoot = null;
        Destroy(root);
    }

    #endregion

    private void OnDisable()
    {
        CancelDrag(!installed);
    }

    private void OnDestroy()
    {
        DestroyOverlay(false);
    }
}
