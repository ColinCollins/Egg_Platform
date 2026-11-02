using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// UI 拖拽旋转组件，拖拽时旋转 UI 对象，支持角度限制
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UIDragRotate : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
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
    [SerializeField] private RectTransform pivotTransform;

    [Tooltip("目标 Canvas（为空则自动查找）")]
    [SerializeField] private Canvas targetCanvas;

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

    private RectTransform rectTransform;
    private Canvas canvas;
    private RectTransform canvasRectTransform;

    private float startAngle;           // 拖拽开始时的对象角度
    private float startPointerAngle;    // 拖拽开始时的指针角度
    private float currentAngle;         // 当前角度
    private float originalAngle;        // 原始角度（用于重置）

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalAngle = rectTransform.eulerAngles.z;
        currentAngle = originalAngle;

        // 如果没有指定旋转中心，使用自身
        if (pivotTransform == null)
        {
            pivotTransform = rectTransform;
        }
    }

    void OnEnable()
    {
        // 初始化 Canvas
        if (targetCanvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = FindObjectOfType<Canvas>();
            }
        }
        else
        {
            canvas = targetCanvas;
        }

        if (canvas != null)
        {
            canvasRectTransform = canvas.GetComponent<RectTransform>();
        }
    }

    /// <summary>
    /// 开始拖拽
    /// </summary>
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (canvas == null || rectTransform == null || pivotTransform == null)
            return;

        // 获取旋转中心点在屏幕上的位置
        Vector2 pivotScreenPos = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, pivotTransform.position);

        // 计算当前指针相对于旋转中心的角度
        startPointerAngle = CalculateAngle(pivotScreenPos, eventData.position);

        // 记录当前对象的角度（转换为 -180 ~ 180 范围便于处理）
        startAngle = NormalizeAngle(rectTransform.eulerAngles.z);

        // 触发拖拽开始回调
        OnDragBegin?.Invoke();
    }

    /// <summary>
    /// 拖拽中
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (canvas == null || rectTransform == null || pivotTransform == null)
            return;

        // 获取旋转中心点在屏幕上的位置
        Vector2 pivotScreenPos = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, pivotTransform.position);

        // 计算当前指针相对于旋转中心的角度
        float currentPointerAngle = CalculateAngle(pivotScreenPos, eventData.position);

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

    /// <summary>
    /// 结束拖拽
    /// </summary>
    public void OnEndDrag(PointerEventData eventData)
    {
        // 触发拖拽结束回调
        OnDragEnd?.Invoke();
    }

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

    /// <summary>
    /// 设置旋转角度
    /// </summary>
    public void SetRotation(float angle)
    {
        currentAngle = angle;
        rectTransform.rotation = Quaternion.Euler(0, 0, angle);
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
    public void SetPivot(RectTransform pivot)
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
}
