using UnityEngine;

/// <summary>
/// 提供一个 OnDragBegin, OnDragEnd, Begin 的时候 actorRigibody 的 bodyType 修改为 Kinematic,End 的时候修改为 Dynamic
/// </summary>
public class Level77Ctrl : MonoBehaviour
{
    [Tooltip("Level77 中需要在拖拽期间切换物理类型的 actor 刚体；未绑定时拖拽回调只会被忽略。")]
    [SerializeField] private Rigidbody2D actorRigidbody;

    [Tooltip("触发 actor 物理切换的拖拽物；绑定后只响应该 DragableItem 或其子节点发出的拖拽事件。")]
    [SerializeField] private DragableItem dragable;

    #region Unity 生命周期

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        SetActorBodyType(RigidbodyType2D.Dynamic);
    }

    #endregion

    #region 拖拽事件入口

    /// <summary>
    /// DragableItem.onDragStartEvent 的 Inspector 入口；拖拽开始时让 actor 临时脱离 Dynamic 物理影响。
    /// </summary>
    public void OnDragBegin(Transform target)
    {
        if (!IsExpectedDragTarget(target))
            return;

        OnDragBegin();
    }

    /// <summary>
    /// 无参拖拽开始入口；用于兼容 Inspector 或其他脚本直接调用。
    /// </summary>
    public void OnDragBegin()
    {
        if (!Application.isPlaying)
            return;

        SetActorBodyType(RigidbodyType2D.Kinematic);
    }

    /// <summary>
    /// 兼容 DragableItem 现有 Start 命名的拖拽开始入口，行为等同 OnDragBegin。
    /// </summary>
    public void OnDragStart(Transform target)
    {
        OnDragBegin(target);
    }

    /// <summary>
    /// 兼容 DragableItem 现有 Start 命名的无参入口，行为等同 OnDragBegin。
    /// </summary>
    public void OnDragStart()
    {
        OnDragBegin();
    }

    /// <summary>
    /// DragableItem.onDragEndEvent 的 Inspector 入口；拖拽结束时恢复 actor 的 Dynamic 物理。
    /// </summary>
    public void OnDragEnd(Transform target)
    {
        if (!IsExpectedDragTarget(target))
            return;

        OnDragEnd();
    }

    /// <summary>
    /// 无参拖拽结束入口；用于兼容 Inspector 或其他脚本直接调用。
    /// </summary>
    public void OnDragEnd()
    {
        if (!Application.isPlaying)
            return;

        SetActorBodyType(RigidbodyType2D.Dynamic);
    }

    #endregion

    #region 物理状态切换

    private void SetActorBodyType(RigidbodyType2D bodyType)
    {
        if (actorRigidbody == null || actorRigidbody.bodyType == bodyType)
            return;

        actorRigidbody.bodyType = bodyType;
    }

    private bool IsExpectedDragTarget(Transform target)
    {
        if (target == null || dragable == null)
            return true;

        Transform dragTransform = dragable.transform;
        return target == dragTransform || target.IsChildOf(dragTransform);
    }

    #endregion
}
