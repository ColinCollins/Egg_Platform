using UnityEngine;

/// <summary>
/// Level101 可见锁的局部拖拽适配器。
/// 仅把共享 DragableItem 的输入回调转发给本关控制器，不改变共享组件行为。
/// </summary>
public sealed class Level101LockDragItem : DragableItem
{
    #region Inspector

    [Tooltip("Level101 局部状态机；为空时从父级查找。")]
    [SerializeField] private Level101Ctrl levelCtrl;

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
        base.OnDragEnd();
        ResolveLevelCtrl();
        levelCtrl?.EndLockDrag();
    }

    #endregion

    #region References

    private void ResolveLevelCtrl()
    {
        if (levelCtrl == null)
            levelCtrl = GetComponentInParent<Level101Ctrl>();
    }

    #endregion
}
