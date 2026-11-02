using UnityEngine;

/// <summary>
/// Level101 箱子的局部举起/释放适配器。
/// 拖拽期间使用运动学刚体，释放后交回 Dynamic 让重力和门顶碰撞完成解谜。
/// </summary>
public sealed class Level101BoxDragItem : DragableItem
{
    #region Inspector

    [Tooltip("Level101 局部状态机；为空时从父级查找。")]
    [SerializeField] private Level101Ctrl levelCtrl;
    [Tooltip("箱子刚体；为空时从当前物体查找。")]
    [SerializeField] private Rigidbody2D boxRigidbody;

    #endregion

    #region Drag Callbacks

    protected override void OnDragStart()
    {
        ResolveReferences();
        if (levelCtrl == null || !levelCtrl.TryBeginBoxDrag())
        {
            SetEnableDrag(false);
            return;
        }

        levelCtrl.SetBoxKinematicForDrag();
        base.OnDragStart();
    }

    protected override void OnDragEnd()
    {
        base.OnDragEnd();
        ResolveReferences();
        levelCtrl?.EndBoxDrag();
    }

    #endregion

    #region Physics relay

    private void OnCollisionEnter2D(Collision2D collision)
    {
        ResolveReferences();
        levelCtrl?.HandleBoxCollision(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        ResolveReferences();
        levelCtrl?.HandleBoxCollision(collision);
    }

    #endregion

    #region References

    private void ResolveReferences()
    {
        if (levelCtrl == null)
            levelCtrl = GetComponentInParent<Level101Ctrl>();
        if (boxRigidbody == null)
            boxRigidbody = GetComponent<Rigidbody2D>();
    }

    #endregion
}
