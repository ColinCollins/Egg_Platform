using Game.ItemEvent;
using UnityEngine;

/// <summary>
/// Detects when the target is dragged above a height threshold, then released, and
/// triggers Execute after it collides with the configured layer.
/// </summary>
[RequireComponent(typeof(DragableItem))]
public class HeightDetector : BaseItemExecutor
{
    [Header("Binding")]
    [Tooltip("Target to monitor. If empty, uses this transform.")]
    [SerializeField] private Transform targetObject;

    [Header("Detection")]
    [Tooltip("World-space Y threshold. Releasing above this height arms the collision trigger.")]
    [SerializeField] private float heightThreshold = 5f;

    [Tooltip("Collision layers that can trigger Execute after release.")]
    [SerializeField] private LayerMask collisionLayerMask = 1 << 0;

    [Header("Debug")]
    [Tooltip("Show debug logs.")]
    [SerializeField] private bool showDebugInfo;

    private Transform targetTransform;
    private DragableItem dragableItem;
    private bool hasExceededHeight;
    private bool isWaitingForCollision;

    private void Awake()
    {
        ResolveTarget();
    }

    private void Update()
    {
        if (targetTransform == null)
            return;

        UpdateHeightState();
        OnUpdate();
    }

    private void UpdateHeightState()
    {
        if (hasExceededHeight)
            return;

        float currentHeight = targetTransform.position.y;
        if (currentHeight <= heightThreshold)
            return;

        hasExceededHeight = true;
        if (showDebugInfo)
        {
            Debug.Log($"[HeightDetector] {targetTransform.name} exceeded height threshold {heightThreshold:F2}");
        }
    }

    public void CheckHeightAndRelease()
    {
        if (targetTransform == null || !hasExceededHeight || isWaitingForCollision)
            return;

        if (dragableItem != null && dragableItem.IsDragging())
            return;

        isWaitingForCollision = true;
        if (showDebugInfo)
        {
            Debug.Log($"[HeightDetector] {targetTransform.name} released above threshold, waiting for collision");
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!hasExceededHeight || !isWaitingForCollision)
            return;

        if ((collisionLayerMask.value & (1 << collision.gameObject.layer)) == 0)
            return;

        if (showDebugInfo)
        {
            Debug.Log($"[HeightDetector] {targetTransform.name} hit target layer collider {collision.gameObject.name}, executing");
        }

        Execute();
        ResetState();
    }

    private void ResetState()
    {
        hasExceededHeight = false;
        isWaitingForCollision = false;

        if (showDebugInfo)
        {
            Debug.Log($"[HeightDetector] {targetTransform.name} state reset");
        }
    }

    public void ResetTrigger()
    {
        ResetState();
    }

    public void SetHeightThreshold(float height)
    {
        heightThreshold = height;
    }

    public float GetCurrentHeight()
    {
        if (targetTransform == null)
            return 0f;

        return targetTransform.position.y;
    }

    public void SetTarget(Transform target)
    {
        targetObject = target;
        ResolveTarget();
    }

    private void ResolveTarget()
    {
        targetTransform = targetObject != null ? targetObject : transform;

        dragableItem = targetTransform.GetComponent<DragableItem>();
        if (dragableItem == null)
        {
            dragableItem = targetTransform.GetComponentInParent<DragableItem>();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Transform drawTarget = targetObject != null ? targetObject : transform;
        if (drawTarget == null)
            return;

        Vector3 thresholdPos = new Vector3(drawTarget.position.x, heightThreshold, drawTarget.position.z);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(
            new Vector3(drawTarget.position.x - 1f, heightThreshold, drawTarget.position.z),
            new Vector3(drawTarget.position.x + 1f, heightThreshold, drawTarget.position.z));

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(drawTarget.position, thresholdPos);
    }
}
