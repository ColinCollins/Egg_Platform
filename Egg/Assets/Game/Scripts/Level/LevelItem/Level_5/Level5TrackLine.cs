using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class Level5TrackLine : MonoBehaviour
{
    [SerializeField] private Transform StartPoint;
    [SerializeField] private Transform EndPoint;
    
    public Transform GetStartPoint() => StartPoint;
    public Transform GetEndPoint() => EndPoint;
    
    public void SetStartPoint(Transform point) => StartPoint = point;
    public void SetEndPoint(Transform point) => EndPoint = point;
    
    private LineRenderer lineRenderer;

    void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        
        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 2;
        }
    }

    void Update()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
                return;
        }

        // 实时更新 positionCount
        if (lineRenderer.positionCount != 2)
        {
            lineRenderer.positionCount = 2;
        }

        // 实时更新 position list（自动转换为本地坐标）
        if (StartPoint != null && EndPoint != null)
        {
            lineRenderer.SetPosition(0, transform.InverseTransformPoint(StartPoint.position));
            lineRenderer.SetPosition(1, transform.InverseTransformPoint(EndPoint.position));
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }
        
        if (lineRenderer != null)
        {
            if (lineRenderer.positionCount != 2)
            {
                lineRenderer.positionCount = 2;
            }
            
            if (StartPoint != null && EndPoint != null)
            {
                lineRenderer.SetPosition(0, transform.InverseTransformPoint(StartPoint.position));
                lineRenderer.SetPosition(1, transform.InverseTransformPoint(EndPoint.position));
            }
        }
    }
#endif
}
