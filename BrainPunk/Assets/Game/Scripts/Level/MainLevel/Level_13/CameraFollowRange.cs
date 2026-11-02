using UnityEngine;

/// <summary>
/// Camera movement boundary for orthographic cameras (world space).
/// Clamps camera center position so the camera view stays inside the boundary.
/// </summary>
public class CameraFollowRange : MonoBehaviour
{
    [Header("Boundary (World Space)")]
    [SerializeField] private Vector2 boundaryCenter = Vector2.zero;
    [SerializeField] private Vector2 boundarySize = new Vector2(20f, 10f);

    [Header("Axis Limit")]
    [SerializeField] private bool limitX = true;
    [SerializeField] private bool limitY = true;

    public Rect GetBoundaryRect()
    {
        Vector2 min = boundaryCenter - boundarySize * 0.5f;
        return new Rect(min, boundarySize);
    }

    /// <summary>
    /// Clamp a desired camera position to the boundary (orthographic camera only).
    /// </summary>
    public Vector3 ClampOrthographic(Camera cam, Vector3 desiredPosition)
    {
        if (cam == null) return desiredPosition;
        if (!cam.orthographic) return desiredPosition;

        Rect rect = GetBoundaryRect();

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;

        float minX = rect.xMin + halfWidth;
        float maxX = rect.xMax - halfWidth;
        float minY = rect.yMin + halfHeight;
        float maxY = rect.yMax - halfHeight;

        // If the boundary is smaller than the camera view, lock to boundary center on that axis.
        float clampedX = desiredPosition.x;
        float clampedY = desiredPosition.y;

        if (limitX)
        {
            if (minX > maxX) clampedX = rect.center.x;
            else clampedX = Mathf.Clamp(desiredPosition.x, minX, maxX);
        }

        if (limitY)
        {
            if (minY > maxY) clampedY = rect.center.y;
            else clampedY = Mathf.Clamp(desiredPosition.y, minY, maxY);
        }

        return new Vector3(clampedX, clampedY, desiredPosition.z);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Rect rect = GetBoundaryRect();
        Vector3 center = new Vector3(rect.center.x, rect.center.y, 0f);
        Vector3 size = new Vector3(rect.size.x, rect.size.y, 0.1f);
        Gizmos.DrawWireCube(center, size);
    }
#endif
}

