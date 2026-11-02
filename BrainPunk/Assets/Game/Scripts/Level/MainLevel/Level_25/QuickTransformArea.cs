using UnityEngine;

/// <summary>
/// 快速变换区域：根据目标在轴上的位置，将超出边界的 Target 移动到对侧边缘（横向左/右，竖向上/下）。
/// </summary>
public class QuickTransformArea : MonoBehaviour
{
    public enum AreaDirection
    {
        Horizontal,
        Vertical
    }

    [Header("方向")]
    [SerializeField] private AreaDirection direction = AreaDirection.Horizontal;

    [Header("边界（世界坐标）")]
    [Tooltip("横向：左边缘 X；竖向：下边缘 Y")]
    [SerializeField] private float minBound = -10f;

    [Tooltip("横向：右边缘 X；竖向：上边缘 Y")]
    [SerializeField] private float maxBound = 10f;

    [Header("目标")]
    [Tooltip("需要做边界回绕的 Transform 列表")]
    [SerializeField] private Transform[] targets = new Transform[0];

    [SerializeField] private bool showGizmos = true;

    private void LateUpdate()
    {
        if (targets == null || targets.Length == 0) return;
        if (minBound >= maxBound) return;

        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == null) continue;
            WrapTargetPosition(targets[i]);
        }
    }

    private void WrapTargetPosition(Transform target)
    {
        Vector3 pos = target.position;

        if (direction == AreaDirection.Horizontal)
        {
            if (pos.x < minBound)
            {
                pos.x = maxBound;
                target.position = pos;
            }
            else if (pos.x > maxBound)
            {
                pos.x = minBound;
                target.position = pos;
            }
        }
        else
        {
            if (pos.y < minBound)
            {
                pos.y = maxBound;
                target.position = pos;
            }
            else if (pos.y > maxBound)
            {
                pos.y = minBound;
                target.position = pos;
            }
        }
    }

    /// <summary>
    /// 从区域内移除指定目标（将对应槽位置空，不再参与边界回绕）
    /// </summary>
    public void RemoveTarget(Transform t)
    {
        if (t == null || targets == null) return;
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] == t)
            {
                targets[i] = null;
                return;
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        Gizmos.color = direction == AreaDirection.Horizontal ? Color.cyan : Color.green;
        Vector3 center;
        Vector3 size;

        if (direction == AreaDirection.Horizontal)
        {
            center = new Vector3((minBound + maxBound) * 0.5f, transform.position.y, transform.position.z);
            size = new Vector3(maxBound - minBound, 1f, 1f);
        }
        else
        {
            center = new Vector3(transform.position.x, (minBound + maxBound) * 0.5f, transform.position.z);
            size = new Vector3(1f, maxBound - minBound, 1f);
        }

        Gizmos.DrawWireCube(center, size);
    }
#endif
}
