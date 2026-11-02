using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(LineRenderer))]
public class Level58TrackLine : MonoBehaviour
{
    [SerializeField] private Transform StartPoint;
    [SerializeField] private Transform EndPoint;

    private LineRenderer _lineRenderer;

    public Transform GetStartPoint() => StartPoint;
    public Transform GetEndPoint() => EndPoint;

    public void SetStartPoint(Transform point) => StartPoint = point;
    public void SetEndPoint(Transform point) => EndPoint = point;

    private void Awake()
    {
        EnsureLineRenderer();
        UpdateLinePositions();
    }

    private void Update()
    {
        if (!EnsureLineRenderer())
            return;

        UpdateLinePositions();
    }

    private bool EnsureLineRenderer()
    {
        if (_lineRenderer == null)
            _lineRenderer = GetComponent<LineRenderer>();

        if (_lineRenderer == null)
            return false;

        if (_lineRenderer.positionCount != 2)
            _lineRenderer.positionCount = 2;

        if (!_lineRenderer.useWorldSpace)
            _lineRenderer.useWorldSpace = true;

        return true;
    }

    private void UpdateLinePositions()
    {
        if (_lineRenderer == null || StartPoint == null || EndPoint == null)
            return;

        _lineRenderer.SetPosition(0, StartPoint.position);
        _lineRenderer.SetPosition(1, EndPoint.position);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!EnsureLineRenderer())
            return;

        UpdateLinePositions();
    }
#endif
}
