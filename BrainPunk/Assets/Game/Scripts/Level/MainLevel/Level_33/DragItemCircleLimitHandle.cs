using UnityEngine;

/// <summary>
/// 拖拽物体圆形限制处理器，限制 item 的 position 只能在指定的 radius 圆边范围上移动
/// </summary>
public class DragItemCircleLimitHandle : MonoBehaviour
{
    [Header("圆形限制设置")]
    [Tooltip("圆心位置（世界坐标）")]
    [SerializeField] private Vector2 circleCenter = Vector2.zero;

    [Tooltip("圆半径（世界坐标）")]
    [SerializeField] private float radius = 1f;

    [Tooltip("是否保持 Z 轴位置")]
    [SerializeField] private bool keepZPosition = true;

    [Header("目标 Item")]
    [Tooltip("要限制的 Item Transform（为空时使用当前对象）")]
    [SerializeField] private Transform targetItem;

    /// <summary>
    /// 限制位置到圆边上（供 DragUpdate 事件调用）
    /// </summary>
    /// <param name="itemTransform">要限制的 Item Transform</param>
    public void LimitToCircle(Transform itemTransform)
    {
        if (itemTransform == null)
            return;

        Vector3 currentPos = itemTransform.position;
        Vector2 currentPos2D = new Vector2(currentPos.x, currentPos.y);

        // 计算从圆心到当前位置的方向
        Vector2 direction = currentPos2D - circleCenter;
        float distance = direction.magnitude;

        // 如果距离为0，使用默认方向（向右）
        if (distance < 0.001f)
        {
            direction = Vector2.right;
        }
        else
        {
            direction.Normalize();
        }

        // 计算圆边上的位置
        Vector2 circleEdgePos = circleCenter + direction * radius;

        // 应用位置
        Vector3 newPosition = new Vector3(circleEdgePos.x, circleEdgePos.y, keepZPosition ? currentPos.z : 0f);
        itemTransform.position = newPosition;
    }

    /// <summary>
    /// 限制位置到圆边上（使用 targetItem）
    /// </summary>
    public void LimitToCircle()
    {
        Transform item = targetItem != null ? targetItem : transform;
        LimitToCircle(item);
    }

    /// <summary>
    /// 限制位置到圆边上（使用指定位置）
    /// </summary>
    /// <param name="currentPosition">当前位置</param>
    /// <returns>限制后的位置</returns>
    public Vector3 LimitPositionToCircle(Vector3 currentPosition)
    {
        Vector2 currentPos2D = new Vector2(currentPosition.x, currentPosition.y);

        // 计算从圆心到当前位置的方向
        Vector2 direction = currentPos2D - circleCenter;
        float distance = direction.magnitude;

        // 如果距离为0，使用默认方向（向右）
        if (distance < 0.001f)
        {
            direction = Vector2.right;
        }
        else
        {
            direction.Normalize();
        }

        // 计算圆边上的位置
        Vector2 circleEdgePos = circleCenter + direction * radius;

        // 返回限制后的位置
        return new Vector3(circleEdgePos.x, circleEdgePos.y, keepZPosition ? currentPosition.z : 0f);
    }

    /// <summary>
    /// 设置圆心位置
    /// </summary>
    public void SetCircleCenter(Vector2 center)
    {
        circleCenter = center;
    }

    /// <summary>
    /// 设置圆半径
    /// </summary>
    public void SetRadius(float r)
    {
        radius = Mathf.Max(0f, r);
    }

    /// <summary>
    /// 获取圆心位置
    /// </summary>
    public Vector2 GetCircleCenter() => circleCenter;

    /// <summary>
    /// 获取圆半径
    /// </summary>
    public float GetRadius() => radius;

    void OnDrawGizmosSelected()
    {
        // 在 Scene 视图中绘制圆形辅助线
        Gizmos.color = Color.yellow;
        Vector3 center3D = new Vector3(circleCenter.x, circleCenter.y, 0f);
        
        // 绘制圆形（使用多个线段近似）
        int segments = 32;
        float angleStep = 360f / segments;
        Vector3 prevPoint = center3D + new Vector3(radius, 0f, 0f);
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 nextPoint = center3D + new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0f
            );
            Gizmos.DrawLine(prevPoint, nextPoint);
            prevPoint = nextPoint;
        }
        
        // 绘制圆心
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center3D, 0.1f);
    }
}
