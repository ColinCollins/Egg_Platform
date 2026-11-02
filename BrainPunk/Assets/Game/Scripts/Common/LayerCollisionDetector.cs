using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class Collision2DEvent : UnityEvent<Collision2D> { }

/// <summary>
/// 检测指定 LayerMask 的碰撞进入和退出事件
/// </summary>
public class LayerCollisionDetector : MonoBehaviour
{
    [Header("检测设置")]
    [SerializeField] private LayerMask targetLayerMask;

    [Header("事件")]
    [SerializeField] private Collision2DEvent onEnter = new Collision2DEvent();
    [SerializeField] private Collision2DEvent onExit = new Collision2DEvent();

    /// <summary>
    /// 在 Inspector 中可绑定的进入事件
    /// </summary>
    public Collision2DEvent OnEnter => onEnter;

    /// <summary>
    /// 在 Inspector 中可绑定的退出事件
    /// </summary>
    public Collision2DEvent OnExit => onExit;

    /// <summary>
    /// 检查碰撞体是否属于目标层
    /// </summary>
    private bool IsInTargetLayer(Collision2D collision)
    {
        return (targetLayerMask.value & (1 << collision.gameObject.layer)) > 0;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsInTargetLayer(collision))
        {
            onEnter?.Invoke(collision);
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (IsInTargetLayer(collision))
        {
            onExit?.Invoke(collision);
        }
    }
}
