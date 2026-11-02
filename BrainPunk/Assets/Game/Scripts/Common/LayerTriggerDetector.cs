using UnityEngine;

/// <summary>
/// 检测指定 LayerMask 的触发器进入和退出事件
/// </summary>
public class LayerTriggerDetector : MonoBehaviour
{
    [Header("检测设置")]
    [SerializeField] private LayerMask targetLayerMask;

    [Header("事件")]
    [SerializeField] private Collider2DEvent onEnter = new Collider2DEvent();
    [SerializeField] private Collider2DEvent onExit = new Collider2DEvent();

    /// <summary>
    /// 在 Inspector 中可绑定的进入事件
    /// </summary>
    public Collider2DEvent OnEnter => onEnter;

    /// <summary>
    /// 在 Inspector 中可绑定的退出事件
    /// </summary>
    public Collider2DEvent OnExit => onExit;

    /// <summary>
    /// 检查碰撞体是否属于目标层
    /// </summary>
    private bool IsInTargetLayer(Collider2D other)
    {
        return (targetLayerMask.value & (1 << other.gameObject.layer)) > 0;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsInTargetLayer(other))
        {
            onEnter?.Invoke(other);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (IsInTargetLayer(other))
        {
            onExit?.Invoke(other);
        }
    }
}
