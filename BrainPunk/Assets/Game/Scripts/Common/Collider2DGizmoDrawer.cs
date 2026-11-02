using UnityEngine;

/// <summary>
/// 2D碰撞盒Gizmos绘制器 - 在编辑模式下显示2D碰撞盒的大小
/// 将此脚本附加到有Collider2D组件的GameObject上即可使用
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Collider2DGizmoDrawer : MonoBehaviour
{
    [Header("Gizmos设置")]
    [Tooltip("Gizmos颜色")]
    [SerializeField] private Color gizmoColor = Color.green;
    
    [Tooltip("Gizmos线条粗细")]
    [Range(1f, 10f)]
    [SerializeField] private float lineWidth = 2f;
    
    [Tooltip("是否填充碰撞盒")]
    [SerializeField] private bool fillCollider = false;
    
    [Tooltip("填充透明度")]
    [Range(0f, 1f)]
    [SerializeField] private float fillAlpha = 0.2f;
    
    private Collider2D collider2D;
    
    void OnValidate()
    {
        if (collider2D == null)
            collider2D = GetComponent<Collider2D>();
    }
    
    void OnDrawGizmos()
    {
        if (collider2D == null)
            collider2D = GetComponent<Collider2D>();
        
        if (collider2D == null)
            return;
        
        Bounds bounds = collider2D.bounds;
        Vector3 center = bounds.center;
        Vector3 size = bounds.size;

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(center, size);

        if (fillCollider)
        {
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, fillAlpha);
            Gizmos.DrawCube(center, size);
        }

        // Gizmos 不支持线宽，这里保留字段仅兼容旧数据。
        float centerSize = Mathf.Min(size.x, size.y) * 0.05f;
        Gizmos.color = gizmoColor;
        Gizmos.DrawLine(center + Vector3.up * centerSize, center + Vector3.down * centerSize);
        Gizmos.DrawLine(center + Vector3.left * centerSize, center + Vector3.right * centerSize);
    }
    
    void OnDrawGizmosSelected()
    {
        // 选中时也可以显示，使用相同的绘制逻辑
        OnDrawGizmos();
    }
}
