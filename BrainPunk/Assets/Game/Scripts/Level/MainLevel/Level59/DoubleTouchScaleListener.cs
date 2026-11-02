using Game.ItemEvent;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 监听两指缩放操作
/// 记录 touches startPosition, 以及 currentPosition distance upper，触发 ScaleUpEvent，反之触发 ScaleDownEvent
/// 提供一个 progress = 1，记录当前变化范围。
/// 提供一个 reset API 用于重置变化
/// </summary>
public class DoubleTouchScaleListener : BaseItemEventHandle
{
    [Header("Events")]
    [Tooltip("双指距离变大时触发")]
    [SerializeField] private UnityEvent onScaleUpEvent;

    [Tooltip("双指距离变小时触发")]
    [SerializeField] private UnityEvent onScaleDownEvent;

    [Header("Settings")]
    [Tooltip("触发事件的最小距离变化阈值（像素）")]
    [SerializeField] private float threshold = 1f;
    [SerializeField] private float touchScaleSensitivity = 0.005f;

    [Tooltip("PC端是否启用鼠标滚轮模拟缩放")]
    [SerializeField] private bool enableMouseWheel = true;

    [Tooltip("鼠标滚轮灵敏度")]
    [SerializeField] private float mouseWheelSensitivity = 0.1f;

    [Tooltip("是否打印鼠标滚轮调试日志")]
    [SerializeField] private bool logMouseWheel = true;

    [Tooltip("Scale 的变化范围 (min, max)")]
    [SerializeField] private Vector2 scaleRange = new Vector2(0.1f, 2f);

    /// <summary>
    /// 当前缩放进度，基于初始两指距离的比率，初始值为 1
    /// </summary>
    public float Scale { get; private set; } = 1f;

    private bool _isTracking;
    private float _pendingTouchDelta;
    private int _lastTouchCount;

    public override void Execute()
    {
        IsRunning = true;
        IsDone = true;
    }

    private void Update()
    {
        if (!IsRunning)
            return;

        HandleTouchInput();

        if (enableMouseWheel)
            HandleMouseWheelInput();
    }

    private void HandleTouchInput()
    {
        if (Input.touchCount == 2)
        {
            Touch touch0 = Input.GetTouch(0);
            Touch touch1 = Input.GetTouch(1);

            if (IsTouchEnded(touch0) || IsTouchEnded(touch1))
            {
                ResetTouchTracking();
                _lastTouchCount = Input.touchCount;
                return;
            }

            if (!_isTracking || _lastTouchCount != 2 || touch0.phase == TouchPhase.Began || touch1.phase == TouchPhase.Began)
            {
                _isTracking = true;
                _pendingTouchDelta = 0f;
                _lastTouchCount = Input.touchCount;
                return;
            }

            Vector2 previous0 = touch0.position - touch0.deltaPosition;
            Vector2 previous1 = touch1.position - touch1.deltaPosition;
            Vector2 pinchAxis = previous0 - previous1;
            if (pinchAxis.sqrMagnitude <= Mathf.Epsilon)
                return;

            Vector2 normalizedAxis = pinchAxis.normalized;
            float pinchDelta = Vector2.Dot(touch0.deltaPosition, normalizedAxis) -
                               Vector2.Dot(touch1.deltaPosition, normalizedAxis);

            _pendingTouchDelta += pinchDelta;
            if (Mathf.Abs(_pendingTouchDelta) >= threshold)
            {
                Scale += _pendingTouchDelta * touchScaleSensitivity;
                ClampScale();

                if (_pendingTouchDelta > 0f)
                    onScaleUpEvent?.Invoke();
                else
                    onScaleDownEvent?.Invoke();

                _pendingTouchDelta = 0f;
            }
        }
        else
        {
            ResetTouchTracking();
        }

        _lastTouchCount = Input.touchCount;
    }

    private static bool IsTouchEnded(Touch touch)
    {
        return touch.phase == TouchPhase.Canceled || touch.phase == TouchPhase.Ended;
    }

    private void ResetTouchTracking()
    {
        _isTracking = false;
        _pendingTouchDelta = 0f;
    }

    private void HandleMouseWheelInput()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (logMouseWheel && Mathf.Abs(scroll) > 0.0001f)
        {
            Debug.Log($"[DoubleTouchScaleListener] Mouse ScrollWheel: {scroll}, Scale before: {Scale}");
        }

        if (Mathf.Abs(scroll) > 0.001f)
        {
            if (scroll > 0)
            {
                Scale += mouseWheelSensitivity;
                onScaleUpEvent?.Invoke();
            }
            else
            {
                Scale -= mouseWheelSensitivity;
                onScaleDownEvent?.Invoke();
            }

            ClampScale();

            if (logMouseWheel)
            {
                Debug.Log($"[DoubleTouchScaleListener] Scale after: {Scale}");
            }
        }
    }

    /// <summary>
    /// 重置缩放状态和进度
    /// </summary>
    public void ResetScale()
    {
        Scale = 1f;
        ClampScale();
        ResetTouchTracking();
        _lastTouchCount = 0;
    }

    /// <summary>
    /// 设置当前缩放值
    /// </summary>
    /// <param name="scale">目标缩放值</param>
    public void SetScale(float scale)
    {
        Scale = scale;
        ClampScale();
        ResetTouchTracking();
    }

    private void ClampScale()
    {
        Scale = Mathf.Clamp(Scale, scaleRange.x, scaleRange.y);
    }

    public override void ResetState()
    {
        base.ResetState();
        ResetScale();
    }

    private void OnDisable()
    {
        ResetTouchTracking();
    }
}
