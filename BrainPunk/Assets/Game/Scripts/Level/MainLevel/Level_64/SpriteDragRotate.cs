using System;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// SpriteRenderer 拖拽旋转组件，参考 UIDragRotate 实现。
/// 拖拽时围绕指定 pivot 旋转世界空间中的 Sprite 对象，支持角度限制。
/// 需要挂载对象带有 Collider2D 以接收点击检测。
/// </summary>
// [RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Collider2D))]
public class SpriteDragRotate : MonoBehaviour
{
    /// <summary>
    /// 拖拽开始时的回调委托
    /// </summary>
    public Action OnDragBegin;

    /// <summary>
    /// 拖拽结束时的回调委托
    /// </summary>
    public Action OnDragEnd;

    /// <summary>
    /// 旋转角度改变时的回调委托，参数为当前角度
    /// </summary>
    public Action<float> OnAngleChanged;

    [Header("旋转设置")]
    [Tooltip("旋转中心点（为空则使用自身位置）")]
    [SerializeField] private Transform pivotTransform;

    [Tooltip("是否顺时针旋转（默认逆时针）")]
    [SerializeField] private bool clockwise = false;

    [Header("角度限制")]
    [Tooltip("是否限制旋转角度")]
    [SerializeField] private bool limitAngle = false;

    [Tooltip("最小角度")]
    [SerializeField] private float minAngle = -180f;

    [Tooltip("最大角度")]
    [SerializeField] private float maxAngle = 180f;

    [Header("拖拽灵敏度")]
    [Tooltip("旋转灵敏度倍数")]
    [SerializeField] private float sensitivity = 1f;

    private SpriteRenderer spriteRenderer;
    private Camera mainCamera;

    private float startAngle;           // 拖拽开始时的对象角度
    private float startPointerAngle;    // 拖拽开始时的指针角度
    private float currentAngle;         // 当前角度
    private float originalAngle;        // 原始角度（用于重置）
    private bool isDragging;            // 是否正在拖拽

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalAngle = transform.eulerAngles.z;
        currentAngle = originalAngle;

        // 如果没有指定旋转中心，使用自身
        if (pivotTransform == null)
        {
            pivotTransform = transform;
        }
    }

    void OnEnable()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    void Update()
    {
        HandleMouseInput();
        HandleTouchInput();
    }

    #region Input Handling

    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (!InputUtils.IsPointerOverUI(Input.mousePosition) && IsPointerOverSprite(Input.mousePosition))
            {
                BeginDrag(Input.mousePosition);
            }
        }
        else if (isDragging && Input.GetMouseButton(0))
        {
            Dragging(Input.mousePosition);
        }
        else if (isDragging && Input.GetMouseButtonUp(0))
        {
            EndDrag();
        }
    }

    private void HandleTouchInput()
    {
#if UNITY_ANDROID || UNITY_IOS
        if (Input.touchCount <= 0)
            return;

        Touch touch = Input.GetTouch(0);

        if (touch.phase == TouchPhase.Began)
        {
            if (!InputUtils.IsPointerOverUI(touch.position) && IsPointerOverSprite(touch.position))
            {
                BeginDrag(touch.position);
            }
        }
        else if (isDragging && (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary))
        {
            Dragging(touch.position);
        }
        else if (isDragging && (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled))
        {
            EndDrag();
        }
#endif
    }

    private bool IsPointerOverSprite(Vector2 screenPosition)
    {
        if (mainCamera == null)
            return false;

        Vector2 worldPos = mainCamera.ScreenToWorldPoint(screenPosition);
        RaycastHit2D hit = Physics2D.Raycast(worldPos, Vector2.zero);

        return hit.collider != null && hit.collider.gameObject == gameObject;
    }

    #endregion

    #region Drag Logic

    private void BeginDrag(Vector2 screenPosition)
    {
        if (mainCamera == null || pivotTransform == null)
            return;

        // 获取旋转中心点在屏幕上的位置
        Vector2 pivotScreenPos = mainCamera.WorldToScreenPoint(pivotTransform.position);

        // 计算当前指针相对于旋转中心的角度
        startPointerAngle = CalculateAngle(pivotScreenPos, screenPosition);

        // 记录当前对象的角度（转换为 -180 ~ 180 范围便于处理）
        startAngle = NormalizeAngle(transform.eulerAngles.z);

        isDragging = true;

        // 触发拖拽开始回调
        OnDragBegin?.Invoke();
    }

    private void Dragging(Vector2 screenPosition)
    {
        if (mainCamera == null || pivotTransform == null)
            return;

        // 获取旋转中心点在屏幕上的位置
        Vector2 pivotScreenPos = mainCamera.WorldToScreenPoint(pivotTransform.position);

        // 计算当前指针相对于旋转中心的角度
        float currentPointerAngle = CalculateAngle(pivotScreenPos, screenPosition);

        // 计算角度差
        float angleDelta = currentPointerAngle - startPointerAngle;

        // 处理角度跳变（从 180 到 -180）
        if (angleDelta > 180f) angleDelta -= 360f;
        if (angleDelta < -180f) angleDelta += 360f;

        // 应用灵敏度
        angleDelta *= sensitivity;

        // 根据方向调整
        if (clockwise)
        {
            angleDelta = -angleDelta;
        }

        // 计算新角度
        float newAngle = startAngle + angleDelta;

        // 限制角度范围
        if (limitAngle)
        {
            newAngle = Mathf.Clamp(newAngle, minAngle, maxAngle);
        }

        // 应用旋转
        SetRotation(newAngle);
    }

    private void EndDrag()
    {
        isDragging = false;

        // 触发拖拽结束回调
        OnDragEnd?.Invoke();
    }

    #endregion

    #region Utils

    /// <summary>
    /// 计算从中心点到目标点的角度
    /// </summary>
    private float CalculateAngle(Vector2 center, Vector2 target)
    {
        return Mathf.Atan2(target.y - center.y, target.x - center.x) * Mathf.Rad2Deg;
    }

    /// <summary>
    /// 将角度标准化到 -180 ~ 180 范围
    /// </summary>
    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    #endregion

    #region Public API

    /// <summary>
    /// 设置旋转角度
    /// </summary>
    public void SetRotation(float angle)
    {
        currentAngle = angle;
        transform.rotation = Quaternion.Euler(0, 0, angle);
        OnAngleChanged?.Invoke(angle);
    }

    /// <summary>
    /// 获取当前角度
    /// </summary>
    public float GetCurrentAngle()
    {
        return currentAngle;
    }

    /// <summary>
    /// 重置到原始角度
    /// </summary>
    public void ResetRotation()
    {
        SetRotation(originalAngle);
    }

    /// <summary>
    /// 设置原始角度
    /// </summary>
    public void SetOriginalAngle(float angle)
    {
        originalAngle = angle;
    }

    /// <summary>
    /// 获取原始角度
    /// </summary>
    public float GetOriginalAngle()
    {
        return originalAngle;
    }

    /// <summary>
    /// 设置角度限制
    /// </summary>
    public void SetAngleLimit(bool enable, float min, float max)
    {
        limitAngle = enable;
        minAngle = min;
        maxAngle = max;
    }

    /// <summary>
    /// 设置旋转中心
    /// </summary>
    public void SetPivot(Transform pivot)
    {
        pivotTransform = pivot;
    }

    /// <summary>
    /// 设置旋转方向
    /// </summary>
    public void SetClockwise(bool isClockwise)
    {
        clockwise = isClockwise;
    }

    /// <summary>
    /// 是否正在拖拽
    /// </summary>
    public bool IsDragging()
    {
        return isDragging;
    }

    /// <summary>
    /// 获取最大角度
    /// </summary>
    public float GetMaxAngle()
    {
        return maxAngle;
    }

    /// <summary>
    /// 获取最小角度
    /// </summary>
    public float GetMinAngle()
    {
        return minAngle;
    }

    #endregion
}
