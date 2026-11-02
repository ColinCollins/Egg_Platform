using UnityEngine;

public class FollowTarget : MonoBehaviour
{
    [SerializeField] private Transform target;
    public Transform Target { get => target; }
    [SerializeField] private bool followX = true;
    [SerializeField] private bool followY = true;
    [SerializeField] private float smoothSpeed = 10f;
    [SerializeField] private Vector2 offset = Vector2.zero;

    private Vector3 initialPosition;

    void Start()
    {
        initialPosition = transform.position;
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 targetPos = target.position;
        Vector3 newPos = transform.position;

        if (followX)
        {
            newPos.x = targetPos.x + offset.x;
        }

        if (followY)
        {
            newPos.y = targetPos.y + offset.y;
        }

        // Keep original Z
        newPos.z = transform.position.z;

        // Smooth follow
        if (smoothSpeed > 0)
        {
            transform.position = Vector3.Lerp(transform.position, newPos, smoothSpeed * Time.deltaTime);
        }
        else
        {
            transform.position = newPos;
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
