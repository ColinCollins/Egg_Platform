using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 区域检测触发器：检测目标物体移入/移出指定 boundary，触发相应事件
/// </summary>
public class TriggerAreaHandle : MonoBehaviour
{
    [Header("区域边界")]
    [Tooltip("边界中心相对当前对象位置的偏移（世界单位 XY）")]
    [SerializeField] private Vector2 offset = new Vector2(0, 0);
    [Tooltip("边界大小（世界单位），以（位置 + offset）为中心")]
    [SerializeField] private Vector2 boundarySize = new Vector2(10f, 10f);

    [Header("检测设置")]
    [Tooltip("检测的目标物体（为空则检测自身）")]
    [SerializeField] private Transform targetObject;

    [Tooltip("检测 Z 轴（3D）或仅 XY（2D）")]
    [SerializeField] private bool checkZAxis = false;

    [Header("事件")]
    [SerializeField] private UnityEvent<Transform> OnEnterEvent;
    [SerializeField] private UnityEvent<Transform> OnExitEvent;

    private bool isInside = false;
    private bool wasInside = false;

    void Start()
    {
        if (targetObject == null)
            targetObject = transform;

        // 初始化状态
        CheckCurrentState();
    }

    void OnEnable()
    {
        if (targetObject == null) return;

        CheckAreaState();
    }

    void Update()
    {
        if (targetObject == null) return;

        CheckAreaState();
    }

    private void CheckCurrentState()
    {
        if (targetObject == null) return;

        wasInside = IsInsideBoundary(targetObject.position);
        isInside = wasInside;
    }

    private void CheckAreaState()
    {
        if (targetObject == null) return;

        bool currentlyInside = IsInsideBoundary(targetObject.position);

        // 状态变化时触发事件
        if (!wasInside && currentlyInside)
        {
            OnEnter();
            Debug.Log("Check Area State: " + currentlyInside);
        }
        else if (wasInside && !currentlyInside)
        {
            OnExit();
        }

        wasInside = currentlyInside;
    }

    private bool IsInsideBoundary(Vector3 position)
    {
        Rect rect = GetBoundaryRect();
        // Debug.Log($"[TriggerAreaHandle] GetBoundaryRect: x={rect.x}, y={rect.y}, width={rect.width}, height={rect.height}, xMin={rect.xMin}, xMax={rect.xMax}, yMin={rect.yMin}, yMax={rect.yMax}");

        if (checkZAxis)
        {
            // 3D 检测：检查 X, Y, Z（Z 轴以 transform.position.z 为中心）
            float zCenter = transform.position.z;
            float zHalf = boundarySize.y * 0.5f;
            return position.x >= rect.xMin && position.x <= rect.xMax &&
                   position.y >= rect.yMin && position.y <= rect.yMax &&
                   position.z >= zCenter - zHalf && position.z <= zCenter + zHalf;
        }
        else
        {
            // 2D 检测：仅检查 X, Y
            return position.x >= rect.xMin && position.x <= rect.xMax &&
                   position.y >= rect.yMin && position.y <= rect.yMax;
        }
    }

    /// <summary>
    /// 边界中心 = transform.position (XY) + offset，再按 boundarySize 展开为矩形
    /// </summary>
    public Rect GetBoundaryRect()
    {
        Vector2 center = new Vector2(transform.position.x, transform.position.y) + offset;
        Vector2 min = center - boundarySize * 0.5f;
        return new Rect(min, boundarySize);
    }

    /// <summary>
    /// 移入区域时触发
    /// </summary>
    private void OnEnter()
    {
        isInside = true;
        OnEnterEvent?.Invoke(targetObject);
    }

    /// <summary>
    /// 移出区域时触发
    /// </summary>
    private void OnExit()
    {
        isInside = false;
        OnExitEvent?.Invoke(targetObject);
    }

    /// <summary>
    /// 设置边界大小（中心为当前对象位置）
    /// </summary>
    public void SetBoundarySize(Vector2 size)
    {
        boundarySize = size;
    }

    /// <summary>
    /// 设置目标物体
    /// </summary>
    public void SetTargetObject(Transform target)
    {
        targetObject = target;
    }

    /// <summary>
    /// 手动触发 Enter 事件
    /// </summary>
    public void TriggerEnter()
    {
        OnEnter();
    }

    /// <summary>
    /// 手动触发 Exit 事件
    /// </summary>
    public void TriggerExit()
    {
        OnExit();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 center = transform.position + new Vector3(offset.x, offset.y, 0f);
        Vector3 size = checkZAxis
            ? new Vector3(boundarySize.x, boundarySize.y, boundarySize.y)
            : new Vector3(boundarySize.x, boundarySize.y, 0.1f);

        Gizmos.color = isInside ? Color.green : Color.red;
        Gizmos.DrawWireCube(center, size);
    }
#endif
}
