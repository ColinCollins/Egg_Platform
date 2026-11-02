using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 世界/场景拖拽事件监听：鼠标或触摸，命中规则与 <see cref="DragableItem"/> 一致（2D OverlapPoint、3D Raycast、无 Collider 时半径兜底）。
/// 移动端按 <see cref="Touch.fingerId"/> 绑定触点，多指可同时拖拽不同物体（本组件一次只跟一根手指）。
/// 只广播 Begin / Drag / End，不修改 Transform 位置；需要拖动位移时请配合 <see cref="DragableItem"/> 或自行在回调里处理。
/// </summary>
public class DragEventsListener : MonoBehaviour
{
    [Header("拖拽设置")]
    [Tooltip("是否监听拖拽")]
    [SerializeField]
    private bool enableDrag = true;

    [Tooltip("为空则用 Camera.main，再退化为 FindFirstObjectByType")]
    [SerializeField]
    private Camera targetCamera;

    [Header("点击检测")]
    [Tooltip("无 Collider 时的命中半径（世界 XY 平面距离）")]
    [SerializeField]
    private float fallbackPickRadius = 1f;

    [Header("拖拽事件")]
    [Tooltip("在自身 Transform 上开始拖拽")]
    [SerializeField]
    private UnityEvent<Transform> onBeginDrag = new UnityEvent<Transform>();

    [Tooltip("拖拽移动中（每一帧命中且仍在拖拽时）")]
    [SerializeField]
    private UnityEvent<Transform> onDrag = new UnityEvent<Transform>();

    [Tooltip("结束拖拽")]
    [SerializeField]
    private UnityEvent<Transform> onEndDrag = new UnityEvent<Transform>();

    public UnityEvent<Transform> BeginDrag => onBeginDrag;

    public UnityEvent<Transform> Drag => onDrag;

    public UnityEvent<Transform> EndDrag => onEndDrag;

    /// <summary>代码侧订阅，参数为挂在本组件上的 Transform。</summary>
    public event Action<Transform>? BeginDragHandlers;

    public event Action<Transform>? DragHandlers;

    public event Action<Transform>? EndDragHandlers;

    public bool IsDragging { get; private set; }

    /// <summary>本次拖拽按下时的屏幕坐标。</summary>
    public Vector2 BeginScreenPosition { get; private set; }

    /// <summary>最近一次 Drag 时的屏幕坐标。</summary>
    public Vector2 CurrentScreenPosition { get; private set; }

    /// <summary>相对 <see cref="BeginScreenPosition"/> 的屏幕位移。</summary>
    public Vector2 RelativeScreenDelta { get; private set; }

    /// <summary>
    /// 当前跟进的触摸 <see cref="Touch.fingerId"/>；未在拖鼠标或非触摸平台时为 -1。
    /// </summary>
    public int TrackingFingerId { get; private set; } = -1;

    private Camera _camera;

    private void Awake()
    {
        if (targetCamera == null)
        {
            _camera = Camera.main;
            if (_camera == null)
                _camera = FindFirstObjectByType<Camera>();
        }
        else
        {
            _camera = targetCamera;
        }
    }

    private void Update()
    {
        if (!enableDrag || _camera == null)
            return;

        HandleDragInput();
    }

    private void HandleDragInput()
    {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        if (Input.touchCount <= 0)
        {
            if (IsDragging)
                FinishDrag();
            return;
        }

        // 尚未认领触点：任意一指在本物体上 Began 即可开始（同一时间本组件只跟一根手指）
        if (!IsDragging)
        {
            for (var i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.phase != TouchPhase.Began)
                    continue;
                if (!IsPointerOnObject(t.position))
                    continue;
                StartDrag(t.position, t.fingerId);
                break;
            }

            return;
        }

        var trackingId = TrackingFingerId;
        var found = false;
        for (var i = 0; i < Input.touchCount; i++)
        {
            var t = Input.GetTouch(i);
            if (t.fingerId != trackingId)
                continue;

            found = true;
            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                FinishDrag();
            }
            else if (!IsPointerOnObject(t.position))
            {
                FinishDrag();
            }
            else if (t.phase == TouchPhase.Moved)
            {
                ContinueDrag(t.position);
            }
            break;
        }

        // 触点列表里已没有这一路 id（系统未再投递 Ended 的极端情况）
        if (!found)
            FinishDrag();
#else
        // Editor / Standalone / WebGL 等：鼠标
        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerOnObject(Input.mousePosition))
                StartDrag(Input.mousePosition, -1);
        }
        else if (Input.GetMouseButton(0) && IsDragging)
        {
            if (!IsPointerOnObject(Input.mousePosition))
                FinishDrag();
            else
                ContinueDrag(Input.mousePosition);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            FinishDrag();
        }
#endif
    }

    private bool IsPointerOnObject(Vector2 screenPosition)
    {
        Vector3 worldPos = ScreenToWorldPosition(screenPosition);
        var worldPos2D = new Vector2(worldPos.x, worldPos.y);

        var hit2D = Physics2D.OverlapPoint(worldPos2D);
        if (hit2D != null)
        {
            var t = hit2D.transform;
            if (t == transform || t.IsChildOf(transform))
                return true;
        }

        var ray = _camera.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out var hit3D, Mathf.Infinity))
        {
            var t = hit3D.transform;
            if (t == transform || t.IsChildOf(transform))
                return true;
        }

        float distance = Vector2.Distance(worldPos2D, new Vector2(transform.position.x, transform.position.y));
        return distance <= fallbackPickRadius;
    }

    /// <param name="fingerId">触摸为 <see cref="Touch.fingerId"/>；鼠标为 -1。</param>
    private void StartDrag(Vector2 screenPosition, int fingerId)
    {
        if (IsDragging)
            return;

        IsDragging = true;
        TrackingFingerId = fingerId;
        BeginScreenPosition = screenPosition;
        CurrentScreenPosition = screenPosition;
        RelativeScreenDelta = Vector2.zero;

        onBeginDrag?.Invoke(transform);
        BeginDragHandlers?.Invoke(transform);
    }

    private void ContinueDrag(Vector2 screenPosition)
    {
        CurrentScreenPosition = screenPosition;
        RelativeScreenDelta = screenPosition - BeginScreenPosition;

        onDrag?.Invoke(transform);
        DragHandlers?.Invoke(transform);
    }

    private void FinishDrag()
    {
        if (!IsDragging)
            return;

        IsDragging = false;
        TrackingFingerId = -1;

        onEndDrag?.Invoke(transform);
        EndDragHandlers?.Invoke(transform);
    }

    private Vector3 ScreenToWorldPosition(Vector2 screenPosition)
    {
        if (_camera == null)
            return Vector3.zero;

        if (_camera.orthographic)
        {
            float zDistance = Mathf.Abs(_camera.transform.position.z - transform.position.z);
            var screenPosWithZ = new Vector3(screenPosition.x, screenPosition.y, zDistance);
            return _camera.ScreenToWorldPoint(screenPosWithZ);
        }

        float zDist = Mathf.Abs(_camera.transform.position.z - transform.position.z);
        return _camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, zDist));
    }

    public void SetEnableDrag(bool enable)
    {
        enableDrag = enable;
        if (!enable)
            FinishDrag();
    }

    private void OnDestroy()
    {
        onBeginDrag?.RemoveAllListeners();
        onDrag?.RemoveAllListeners();
        onEndDrag?.RemoveAllListeners();
        BeginDragHandlers = null;
        DragHandlers = null;
        EndDragHandlers = null;
    }
}
