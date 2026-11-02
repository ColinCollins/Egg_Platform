using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class Level30Ctrl : MonoBehaviour
{
    #region Serialized Fields

    // Inspector 统一配置翅膀拖拽对象与对应移动控制器；每项必须挂载 FollowTarget，并只服务 Level30 拖拽开始流程。
    [SerializeField] private List<WingStartDragItem> wingStartDragItems = new List<WingStartDragItem>();

    [SerializeField] private Collider2D doorCollider;

    [SerializeField] private Collider2D lockCollider;

    #endregion

    #region Runtime State

    private Transform currentWing;

    private FollowTarget lockFollower;
    private bool hasWingBindLock = false;

    #endregion

    #region Drag Callbacks

    public void OnWingStartDrag(Transform target)
    {
        for (int i = 0; i < wingStartDragItems.Count; i++)
        {
            if (wingStartDragItems[i].TryHandleStartDrag(target))
            {
                currentWing = target;
            }
        }
    }

    public void OnWingEndDrag(Transform target)
    {
        if (target == null || lockCollider == null)
        {
            target.DOMoveY(10, 1f).OnComplete(() =>
            {
                Destroy(target.gameObject);
            });
            return;
        }

        Collider2D targetCollider = target.GetComponent<Collider2D>();
        if (targetCollider == null)
            targetCollider = target.GetComponentInChildren<Collider2D>();

        bool isIntersecting = false;
        if (targetCollider != null)
        {
            isIntersecting = targetCollider.bounds.Intersects(lockCollider.bounds);
        }

        if (isIntersecting)
        {
            BindLockToWing(lockCollider);
        }

        target.DOMoveY(10, 1f).OnComplete(() =>
        {
            Destroy(target.gameObject);
        });
    }

    public void BindLockToWing(Collider2D collider)
    {
        if (currentWing == null || hasWingBindLock)
            return;

        hasWingBindLock = true;
        lockFollower = collider.transform.GetComponent<FollowTarget>();
        lockFollower.enabled = true;
        lockFollower.SetTarget(currentWing);

        doorCollider.enabled = true;

        for (int i = 0; i < wingStartDragItems.Count; i++)
        {
            wingStartDragItems[i].DisableBoxCollider();
        }
    }

    #endregion

    #region Nested Types

    [Serializable]
    private class WingStartDragItem
    {
        // 拖拽开始时参与 FollowTarget 匹配的对象；需要与对应移动控制器的 target 保持一致。
        [SerializeField] private Transform itemTransform;

        // 命中拖拽对象后停止的移动控制器；只在 OnWingStartDrag 命中本项时调用 StopMove。
        [SerializeField] private PingPongMoveHandle moveHandle;

        public bool TryHandleStartDrag(Transform target)
        {
            var follower = itemTransform.GetComponent<FollowTarget>();
            if (follower.Target != target)
                return false;

            follower.enabled = false;
            moveHandle.StopMove();
            itemTransform.DOMoveY(-1.8f, .8f).SetEase(Ease.InQuart);
            return true;
        }

        public void DisableBoxCollider()
        {
            var boxCollider = itemTransform.GetComponent<BoxCollider2D>();
            if (boxCollider != null)
            {
                boxCollider.enabled = false;
            }
        }
    }

    #endregion
}
