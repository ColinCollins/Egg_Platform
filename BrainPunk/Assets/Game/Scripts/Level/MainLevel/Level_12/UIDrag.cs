using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UI 拖拽组件，使用 Unity 的拖拽接口实现
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UIDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    /// <summary>
    /// 拖拽开始时的回调委托
    /// </summary>
    public Action OnDragBegin;

    /// <summary>
    /// 拖拽结束时的回调委托
    /// </summary>
    public Action OnDragEnd;

    [Header("拖拽设置")]
    [SerializeField] private Canvas targetCanvas; // 目标 Canvas（为空则自动查找）

    [Header("移动范围限制")]
    [Tooltip("是否限制拖拽移动范围")]
    [SerializeField] private bool isLimit = false;

    [Tooltip("相对初始位置的偏移：左下角 (minX, minY)")]
    [SerializeField] private Vector2 boundaryMin = new Vector2(-200f, -200f);

    [Tooltip("相对初始位置的偏移：右上角 (maxX, maxY)")]
    [SerializeField] private Vector2 boundaryMax = new Vector2(200f, 200f);

    private RectTransform rectTransform;
    private Canvas canvas;
    private RectTransform canvasRectTransform;
    private Vector2 dragOffset;
    private Vector3 originalPosition; // 原始位置（用于重置）
    private Vector2 referenceAnchoredPosition; // 范围参考点（相对距离的基准）

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        originalPosition = transform.position;
        referenceAnchoredPosition = rectTransform.anchoredPosition;
    }

    void OnEnable()
    {
        // 初始化 Canvas
        if (targetCanvas == null)
        {
            // 尝试从当前 GameObject 或其父级获取 Canvas
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
        if (canvas == null || rectTransform == null)
            return;

        // 将屏幕坐标转换为 Canvas 本地坐标
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRectTransform, eventData.position, canvas.worldCamera, out localPoint))
        {
            // 计算拖拽偏移量（按钮本地坐标 - 点击位置的本地坐标）
            Vector2 buttonLocalPos = rectTransform.anchoredPosition;
            dragOffset = buttonLocalPos - localPoint;
        }

        // 触发拖拽开始回调
        OnDragBegin?.Invoke();
    }

    /// <summary>
    /// 拖拽中
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (canvas == null || rectTransform == null)
            return;

        // 将屏幕坐标转换为 Canvas 本地坐标
        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRectTransform, eventData.position, canvas.worldCamera, out localPoint))
        {
            // 计算新位置（点击位置 + 偏移量）
            Vector2 newPosition = localPoint + dragOffset;

            // 限制在 boundary 范围内（相对 referenceAnchoredPosition 的偏移）
            if (isLimit)
            {
                float minX = referenceAnchoredPosition.x + boundaryMin.x;
                float maxX = referenceAnchoredPosition.x + boundaryMax.x;
                float minY = referenceAnchoredPosition.y + boundaryMin.y;
                float maxY = referenceAnchoredPosition.y + boundaryMax.y;
                newPosition.x = Mathf.Clamp(newPosition.x, minX, maxX);
                newPosition.y = Mathf.Clamp(newPosition.y, minY, maxY);
            }

            // 更新 RectTransform 的 anchoredPosition
            rectTransform.anchoredPosition = newPosition;
        }
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
    /// 重置到原始位置
    /// </summary>
    public void ResetPosition()
    {
        transform.position = originalPosition;
    }

    /// <summary>
    /// 设置原始位置
    /// </summary>
    public void SetOriginalPosition(Vector3 position)
    {
        originalPosition = position;
    }

    /// <summary>
    /// 获取原始位置
    /// </summary>
    public Vector3 GetOriginalPosition()
    {
        return originalPosition;
    }

    /// <summary>
    /// 设置范围参考点（boundary 相对此位置的偏移）
    /// </summary>
    public void SetReferencePosition(Vector2 anchoredPosition)
    {
        referenceAnchoredPosition = anchoredPosition;
    }

    /// <summary>
    /// 使用当前 anchoredPosition 作为范围参考点
    /// </summary>
    public void SetReferenceToCurrentPosition()
    {
        if (rectTransform != null)
        {
            referenceAnchoredPosition = rectTransform.anchoredPosition;
        }
    }
}
