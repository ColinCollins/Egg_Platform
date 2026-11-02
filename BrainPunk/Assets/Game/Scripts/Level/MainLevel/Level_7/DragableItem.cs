using UnityEngine;
using UnityEngine.Events;
using Bear.EventSystem;
using Game.Events;
using Game.Play;

/// <summary>
/// 可拖拽物体组件，支持移动端触摸和PC端鼠标拖拽
/// </summary>
public class DragableItem : MonoBehaviour
{
    [Header("拖拽设置")]
    [Tooltip("是否启用拖拽")]
    [SerializeField] private bool enableDrag = true;

    [Tooltip("拖拽时是否限制在屏幕范围内")]
    [SerializeField] private bool clampToScreen = false;

    [Tooltip("拖拽时是否保持Z轴位置")]
    [SerializeField] private bool keepZPosition = true;

    [Tooltip("拖拽时使用的相机（为空则使用主相机）")]
    [SerializeField] private Camera targetCamera;

    [Header("拖拽限制")]
    [Tooltip("是否限制拖拽范围")]
    [SerializeField] private bool limitDragArea = false;

    [Tooltip("拖拽范围中心点（世界坐标）")]
    [SerializeField] private Vector2 dragAreaCenter = Vector2.zero;

    [Tooltip("拖拽范围大小（世界坐标）")]
    [SerializeField] private Vector2 dragAreaSize = Vector2.one;

    [Tooltip("限制 X 轴移动")]
    [SerializeField] private bool limitX = false;

    [Tooltip("限制 Y 轴移动")]
    [SerializeField] private bool limitY = false;

    [Header("拖拽偏移")]
    [Tooltip("拖拽时的偏移量（世界坐标）")]
    [SerializeField] private Vector2 dragOffset = Vector2.zero;

    [Header("拖拽限制参数")]
    [Tooltip("单次拖拽的最小移动距离，小于该值则不移动（0 为不限制）")]
    [SerializeField] private float minMoveLimit = 0f;

    [Header("拖拽事件")]
    [Tooltip("拖拽开始时触发，传入自己的 Transform")]
    [SerializeField] private UnityEvent<Transform> onDragStartEvent;

    [Tooltip("拖拽更新时触发，传入自己的 Transform")]
    [SerializeField] private UnityEvent<Transform> onDragUpdateEvent;

    [Tooltip("拖拽结束时触发，传入自己的 Transform")]
    [SerializeField] private UnityEvent<Transform> onDragEndEvent;

    private bool isDragging = false;
    private Vector3 offset;
    private Vector3 dragStartPosition;
    private float originalZ;
    private Camera mainCamera;
    private EventSubscriber _subscriber;
    private bool _isPause;
    private Vector3 lastMousePosition;

    /// <summary>
    /// Input camera used by the shared drag implementation. Specialized drag
    /// items can use it for UI-aware screen-to-world conversion without adding a
    /// second input loop.
    /// </summary>
    protected Camera InputCamera => mainCamera;

    void Awake()
    {
        // 获取或设置相机
        if (targetCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null)
            {
                mainCamera = FindFirstObjectByType<Camera>();
            }
        }
        else
        {
            mainCamera = targetCamera;
        }

        originalZ = transform.position.z;
    }

    private void Start()
    {
        AddListener();
    }

    private void OnDestroy()
    {
        EventsUtils.ResetEvents(ref _subscriber);
    }

    void Update()
    {
        if (!enableDrag || mainCamera == null || _isPause)
            return;

        HandleDragInput();
    }

    private void AddListener()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
    }

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        _isPause = !evt.NewState.Equals(GamePlayStateName.PLAYING);
        if (_isPause && isDragging)
        {
            EndDrag();
        }
    }

    private void HandleDragInput()
    {
#if UNITY_EDITOR
        // PC端鼠标检测
        if (Input.GetMouseButtonDown(0))
        {
            lastMousePosition = Input.mousePosition;
            if (IsMouseOnObject(Input.mousePosition))
            {
                StartDrag(Input.mousePosition);
            }
        }
        else if (Input.GetMouseButton(0))
        {
            Vector3 currentMousePosition = Input.mousePosition;
            if (isDragging && currentMousePosition != lastMousePosition)
            {
                UpdateDragPosition(currentMousePosition);
            }

            lastMousePosition = currentMousePosition;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            EndDrag();
        }

#elif UNITY_ANDROID || UNITY_IOS
        // 移动端触摸检测
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            Vector2 touchPosition = touch.position;

            if (touch.phase == TouchPhase.Began)
            {
                if (IsTouchOnObject(touchPosition))
                {
                    StartDrag(touchPosition);
                }
            }
            else if (touch.phase == TouchPhase.Moved && isDragging)
            {
                UpdateDragPosition(touchPosition);
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                EndDrag();
            }
        }
            
#endif
    }

    [Header("点击检测")]
    [Tooltip("无 Collider 时的点击半径（世界坐标）")]
    [SerializeField] private float fallbackPickRadius = 1.0f;

    protected virtual bool IsTouchOnObject(Vector2 screenPosition)
    {
        // 将屏幕坐标转换为世界坐标
        Vector3 worldPos = ScreenToWorldPosition(screenPosition);
        Vector2 worldPos2D = new Vector2(worldPos.x, worldPos.y);

        // 2D Collider 命中检测（允许 Collider 在子物体上）
        Collider2D hit2D = Physics2D.OverlapPoint(worldPos2D);
        if (hit2D != null)
        {
            var t = hit2D.transform;
            if (t == transform || t.IsChildOf(transform))
                return true;
        }

        // 3D Collider 命中检测（允许 Collider 在子物体上）
        if (mainCamera != null)
        {
            Ray ray = mainCamera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out var hit3D, Mathf.Infinity))
            {
                var t = hit3D.transform;
                if (t == transform || t.IsChildOf(transform))
                    return true;
            }
        }

        // 如果没有碰撞体，使用简单的距离判断
        float distance = Vector2.Distance(worldPos2D, transform.position);
        return distance <= fallbackPickRadius;
    }

    protected virtual bool IsMouseOnObject(Vector2 screenPosition)
    {
        return IsTouchOnObject(screenPosition);
    }

    private void StartDrag(Vector2 screenPosition)
    {
        isDragging = true;
        Vector3 worldPos = ScreenToWorldPosition(screenPosition);
        offset = transform.position - worldPos;
        dragStartPosition = transform.position;

        // 触发拖拽开始事件
        OnDragStart();
    }

    private void UpdateDragPosition(Vector2 screenPosition)
    {
        Vector3 worldPos = ScreenToWorldPosition(screenPosition);
        Vector3 newPosition = worldPos + offset + (Vector3)dragOffset;

        // 保持Z轴位置
        if (keepZPosition)
        {
            newPosition.z = originalZ;
        }

        // 限制拖拽范围
        if (limitDragArea)
        {
            newPosition.x = Mathf.Clamp(newPosition.x,
                dragAreaCenter.x - dragAreaSize.x * 0.5f,
                dragAreaCenter.x + dragAreaSize.x * 0.5f);
            newPosition.y = Mathf.Clamp(newPosition.y,
                dragAreaCenter.y - dragAreaSize.y * 0.5f,
                dragAreaCenter.y + dragAreaSize.y * 0.5f);
        }

        // 限制在屏幕范围内
        if (clampToScreen)
        {
            Vector3 screenPos = mainCamera.WorldToScreenPoint(newPosition);
            screenPos.x = Mathf.Clamp(screenPos.x, 0, Screen.width);
            screenPos.y = Mathf.Clamp(screenPos.y, 0, Screen.height);
            newPosition = mainCamera.ScreenToWorldPoint(screenPos);
            if (keepZPosition)
            {
                newPosition.z = originalZ;
            }
        }

        // 限制 X 或 Y 轴移动
        if (limitX)
        {
            newPosition.x = transform.position.x;
        }
        if (limitY)
        {
            newPosition.y = transform.position.y;
        }

        // 最小移动限制：单次拖拽位移不足时不移动
        if (minMoveLimit > 0f)
        {
            float distanceFromStart = Vector2.Distance(
                new Vector2(newPosition.x, newPosition.y),
                new Vector2(dragStartPosition.x, dragStartPosition.y));
            if (distanceFromStart < minMoveLimit)
            {
                return;
            }
        }

        transform.position = newPosition;

        // 触发拖拽更新事件
        OnDragUpdate();
    }

    private void EndDrag()
    {
        if (isDragging)
        {
            isDragging = false;
            // 触发拖拽结束事件
            OnDragEnd();
        }
    }

    protected virtual Vector3 ScreenToWorldPosition(Vector2 screenPosition)
    {
        if (mainCamera == null)
            return Vector3.zero;

        // 对于正交相机
        if (mainCamera.orthographic)
        {
            float zDistance = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
            Vector3 screenPosWithZ = new Vector3(screenPosition.x, screenPosition.y, zDistance);
            Vector3 worldPosition = mainCamera.ScreenToWorldPoint(screenPosWithZ);
            return worldPosition;
        }
        else
        {
            // 对于透视相机
            float zDistance = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
            Vector3 worldPosition = mainCamera.ScreenToWorldPoint(
                new Vector3(screenPosition.x, screenPosition.y, zDistance));
            return worldPosition;
        }
    }

    // 事件回调（可在子类中重写）
    protected virtual void OnDragStart()
    {
        onDragStartEvent?.Invoke(transform);
    }

    protected virtual void OnDragUpdate()
    {
        onDragUpdateEvent?.Invoke(transform);
    }

    protected virtual void OnDragEnd()
    {
        onDragEndEvent?.Invoke(transform);
    }

    // 公共方法
    public void SetEnableDrag(bool enable)
    {
        enableDrag = enable;
        if (!enable && isDragging)
        {
            EndDrag();
        }
    }

    public bool IsDragging()
    {
        return isDragging;
    }

    public void SetDragArea(Vector2 center, Vector2 size)
    {
        dragAreaCenter = center;
        dragAreaSize = size;
        limitDragArea = true;
    }

    public void ClearDragArea()
    {
        limitDragArea = false;
    }

    // 在编辑器中可视化拖拽范围
    void OnDrawGizmosSelected()
    {
        if (limitDragArea)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(dragAreaCenter, dragAreaSize);
        }
    }
}
