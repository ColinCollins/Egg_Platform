using UnityEngine;

/// <summary>
/// 相机Gizmos绘制器 - 在编辑模式下显示指定分辨率的视口框
/// 将此脚本附加到Camera上即可使用
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraGizmoDrawer : MonoBehaviour
{
    [Header("1920×1080")]
    [SerializeField] private bool show1920x1080 = true;
    
    [Header("2340×1080")]
    [SerializeField] private bool show2340x1080 = true;
    
    [Header("1920×1440")]
    [SerializeField] private bool show1920x1440 = false;
    
    [Header("2340×1440")]
    [SerializeField] private bool show2340x1440 = false;
    
    private static readonly Color Color1920x1080 = Color.black;
    private static readonly Color Color2340x1080 = Color.red;
    private static readonly Color Color1920x1440 = Color.green;
    private static readonly Color Color2340x1440 = new Color(0.5f, 0f, 0.5f); // purple
    
    private Camera cam;
    
    void OnValidate()
    {
        if (cam == null)
            cam = GetComponent<Camera>();
    }
    
    void OnDrawGizmos()
    {
        if (cam == null)
            cam = GetComponent<Camera>();
        
        if (cam == null)
            return;
        
        float distance = cam.nearClipPlane;
        
        if (show1920x1080)
        {
            DrawResolutionGizmo(1920, 1080, Color1920x1080, distance);
        }
        
        if (show2340x1080)
        {
            DrawResolutionGizmo(2340, 1080, Color2340x1080, distance);
        }
        
        if (show1920x1440)
        {
            DrawResolutionGizmo(1920, 1440, Color1920x1440, distance);
        }
        
        if (show2340x1440)
        {
            DrawResolutionGizmo(2340, 1440, Color2340x1440, distance);
        }
    }
    
    /// <summary>
    /// 绘制指定分辨率的视口框
    /// </summary>
    private void DrawResolutionGizmo(int width, int height, Color color, float distance)
    {
        Vector3[] corners = GetCameraCornersForResolution(width, height, distance);

        Gizmos.color = color;
        Gizmos.DrawLine(corners[0], corners[1]);
        Gizmos.DrawLine(corners[1], corners[2]);
        Gizmos.DrawLine(corners[2], corners[3]);
        Gizmos.DrawLine(corners[3], corners[0]);
    }
    
    /// <summary>
    /// 根据分辨率获取视口四个角的世界坐标
    /// </summary>
    private Vector3[] GetCameraCornersForResolution(int width, int height, float distance)
    {
        Vector3[] corners = new Vector3[4];
        
        float aspect = (float)width / height;
        
        if (cam.orthographic)
        {
            // 正交相机：以 1080 为基准，orthographicSize 对应半高
            float refHeight = 1080f;
            float halfHeight = cam.orthographicSize * (height / refHeight);
            float halfWidth = halfHeight * aspect;
            
            corners[0] = cam.transform.position + cam.transform.right * -halfWidth + cam.transform.up * halfHeight + cam.transform.forward * distance;
            corners[1] = cam.transform.position + cam.transform.right * halfWidth + cam.transform.up * halfHeight + cam.transform.forward * distance;
            corners[2] = cam.transform.position + cam.transform.right * halfWidth + cam.transform.up * -halfHeight + cam.transform.forward * distance;
            corners[3] = cam.transform.position + cam.transform.right * -halfWidth + cam.transform.up * -halfHeight + cam.transform.forward * distance;
        }
        else
        {
            // 透视相机：按分辨率比例计算视口
            float fov = cam.fieldOfView;
            float halfHeight = distance * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            halfHeight *= (height / 1080f);
            float halfWidth = halfHeight * aspect;
            
            corners[0] = cam.transform.position + cam.transform.right * -halfWidth + cam.transform.up * halfHeight + cam.transform.forward * distance;
            corners[1] = cam.transform.position + cam.transform.right * halfWidth + cam.transform.up * halfHeight + cam.transform.forward * distance;
            corners[2] = cam.transform.position + cam.transform.right * halfWidth + cam.transform.up * -halfHeight + cam.transform.forward * distance;
            corners[3] = cam.transform.position + cam.transform.right * -halfWidth + cam.transform.up * -halfHeight + cam.transform.forward * distance;
        }
        
        return corners;
    }
}
