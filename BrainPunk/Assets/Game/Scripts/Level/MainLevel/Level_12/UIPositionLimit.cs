using UnityEngine;

public class UIPositionLimit : MonoBehaviour
{
    [Header("Position Limits")]
    [SerializeField] private Vector2 minPos;
    [SerializeField] private Vector2 maxPos;

    [Header("Axis Constraints")]
    [SerializeField] private bool limitX = true;
    [SerializeField] private bool limitY = true;

    [Header("Target")]
    [SerializeField] private RectTransform target;

    void Start()
    {
        if (target == null)
        {
            target = GetComponent<RectTransform>();
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        ClampPosition();
    }

    private void ClampPosition()
    {
        Vector2 pos = target.anchoredPosition;

        if (limitX)
        {
            pos.x = Mathf.Clamp(pos.x, minPos.x, maxPos.x);
        }

        if (limitY)
        {
            pos.y = Mathf.Clamp(pos.y, minPos.y, maxPos.y);
        }

        target.anchoredPosition = pos;
    }

    public void SetLimits(Vector2 min, Vector2 max)
    {
        minPos = min;
        maxPos = max;
    }

    public void SetTarget(RectTransform newTarget)
    {
        target = newTarget;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (target == null) return;

        Gizmos.color = Color.green;

        Vector3 center = target.position;
        Vector3 size = new Vector3(maxPos.x - minPos.x, maxPos.y - minPos.y, 0);
        Vector3 offset = new Vector3((maxPos.x + minPos.x) / 2, (maxPos.y + minPos.y) / 2, 0);

        Gizmos.DrawWireCube(target.parent.TransformPoint(offset), size);
    }
#endif
}
