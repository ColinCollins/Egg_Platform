using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// DrawGame 入场实体的摆放契约与 Scene 轨迹预览。
/// 实体根节点始终摆在最终位置，entranceStartPoint 子节点表示运行时入场起点。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Entrance Path View")]
public sealed class DrawEntrancePathView : MonoBehaviour
{
    public const string DefaultStartPointName = "entranceStartPoint";

    [Header("入场位置")]
    [Tooltip("入场起点；实体根节点当前位置就是最终位置。建议保持为根节点的直接子节点。")]
    [SerializeField] private Transform entranceStartPoint;

    [Header("Scene Gizmo")]
    [Tooltip("Scene 视图中的轨迹、标记和起点轮廓颜色。")]
    [SerializeField] private Color pathColor = new Color(0.05f, 0.85f, 1f, 0.95f);

    [Tooltip("起点和终点标记的世界空间半径。")]
    [SerializeField, Min(0.01f)] private float pointRadius = 0.22f;

    [Tooltip("把当前实体的渲染包围盒平移到起点，显示入场前的虚拟占位。")]
    [SerializeField] private bool drawStartFootprint = true;

    /// <summary>
    /// 返回配置的起点 Transform；未显式绑定时仅按约定名称在直接子节点中查找。
    /// </summary>
    public Transform EntranceStartPoint => ResolveStartPoint();

    /// <summary>
    /// 根节点当前世界位置就是策划配置的最终位置。
    /// </summary>
    public Vector3 FinalPosition => transform.position;

    /// <summary>
    /// 当前是否具备可用的独立起点。
    /// </summary>
    public bool IsConfigured => ResolveStartPoint() != null;

    /// <summary>
    /// 在移动实体之前同时读取起点和最终位置。
    /// 起点通常是实体子节点，因此运行时移动根节点后不可再次读取它来做 Reset。
    /// </summary>
    public bool TryGetWorldPositions(
        out Vector3 startPosition,
        out Vector3 finalPosition)
    {
        finalPosition = transform.position;
        Transform startPoint = ResolveStartPoint();
        startPosition = startPoint != null
            ? startPoint.position
            : finalPosition;
        return startPoint != null;
    }

    private Transform ResolveStartPoint()
    {
        if (entranceStartPoint != null)
            return entranceStartPoint;

        return transform.Find(DefaultStartPointName);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
            return;

        Transform startPoint = ResolveStartPoint();
        if (startPoint == null)
            return;

        Vector3 startPosition = startPoint.position;
        Vector3 finalPosition = transform.position;
        float radius = Mathf.Max(0.01f, pointRadius);

        Color previousGizmoColor = Gizmos.color;
        Color previousHandleColor = Handles.color;

        Gizmos.color = pathColor;
        Gizmos.DrawWireSphere(startPosition, radius);
        Gizmos.DrawWireSphere(finalPosition, radius);
        DrawCross(startPosition, radius);
        DrawCross(finalPosition, radius);

        Handles.color = pathColor;
        Handles.DrawDottedLine(startPosition, finalPosition, 5f);
        DrawDirectionArrow(startPosition, finalPosition, radius);

        if (drawStartFootprint && TryGetRendererBounds(out Bounds finalBounds))
        {
            Color footprintColor = pathColor;
            footprintColor.a *= 0.55f;
            Gizmos.color = footprintColor;
            Vector3 startBoundsCenter = finalBounds.center
                + startPosition
                - finalPosition;
            Gizmos.DrawWireCube(startBoundsCenter, finalBounds.size);
        }

        Handles.color = previousHandleColor;
        Gizmos.color = previousGizmoColor;
    }

    private static void DrawCross(Vector3 position, float radius)
    {
        Gizmos.DrawLine(
            position + Vector3.left * radius,
            position + Vector3.right * radius);
        Gizmos.DrawLine(
            position + Vector3.down * radius,
            position + Vector3.up * radius);
    }

    private static void DrawDirectionArrow(
        Vector3 startPosition,
        Vector3 finalPosition,
        float radius)
    {
        Vector3 path = finalPosition - startPosition;
        if (path.sqrMagnitude <= Mathf.Epsilon)
            return;

        Vector3 direction = path.normalized;
        Vector3 perpendicular = Vector3.Cross(Vector3.forward, direction).normalized;
        float arrowLength = Mathf.Clamp(path.magnitude * 0.08f, radius * 1.5f, radius * 4f);
        Vector3 tip = finalPosition - direction * radius;
        Vector3 wingCenter = tip - direction * arrowLength;
        Vector3 wingOffset = perpendicular * arrowLength * 0.45f;

        Handles.DrawLine(tip, wingCenter + wingOffset);
        Handles.DrawLine(tip, wingCenter - wingOffset);
    }

    private bool TryGetRendererBounds(out Bounds combinedBounds)
    {
        combinedBounds = default;
        bool hasBounds = false;
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer childRenderer in renderers)
        {
            if (childRenderer == null ||
                !childRenderer.enabled ||
                !childRenderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds = childRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                combinedBounds.Encapsulate(childRenderer.bounds);
            }
        }

        return hasBounds;
    }
#endif
}
