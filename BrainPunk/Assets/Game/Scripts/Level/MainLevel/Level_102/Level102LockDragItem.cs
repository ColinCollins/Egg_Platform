using UnityEngine;

/// <summary>
/// Level102 可见锁的局部拖拽适配器。
/// 锁被移走后的重新生成逻辑继续由 Level102Ctrl 编排，避免改写 Level101 或共享 DragableItem。
/// </summary>
public sealed class Level102LockDragItem : DragableItem
{
    #region Inspector

    [Tooltip("Level102 局部状态机；为空时从父级查找。")]
    [SerializeField] private Level102Ctrl levelCtrl;

    #endregion

    #region Drag Callbacks

    protected override void OnDragStart()
    {
        ResolveLevelCtrl();
        if (levelCtrl == null || !levelCtrl.TryBeginLockDrag())
        {
            SetEnableDrag(false);
            return;
        }

        base.OnDragStart();
    }

    protected override void OnDragEnd()
    {
        // 先让共享组件完成其 UnityEvent，再交回本关状态机，保持 Level101 的回调顺序。
        base.OnDragEnd();
        ResolveLevelCtrl();
        levelCtrl?.EndLockDrag();
    }

    #endregion

    #region References

    private void ResolveLevelCtrl()
    {
        if (levelCtrl == null)
            levelCtrl = GetComponentInParent<Level102Ctrl>();
    }

    #endregion
}
