using UnityEngine;

/// <summary>
/// Level107 入口门拖拽命中适配器。
/// 共享 DragableItem 只读取 Physics2D.OverlapPoint 的首个结果，门与平台重叠时可能被平台抢占；
/// 本组件直接检查门自身及子物体的 Collider/Renderer，让可见门体都可以稳定起拖。
/// </summary>
public sealed class Level107DoorDragItem : DragableItem
{
    protected override bool IsTouchOnObject(Vector2 screenPosition)
    {
        Vector3 worldPosition = ScreenToWorldPosition(screenPosition);
        Vector2 worldPoint = new Vector2(worldPosition.x, worldPosition.y);

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int index = 0; index < colliders.Length; index++)
        {
            Collider2D collider = colliders[index];
            if (collider != null
                && collider.enabled
                && collider.gameObject.activeInHierarchy
                && collider.OverlapPoint(worldPoint))
            {
                return true;
            }
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer renderer = renderers[index];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            Bounds bounds = renderer.bounds;
            if (worldPoint.x >= bounds.min.x
                && worldPoint.x <= bounds.max.x
                && worldPoint.y >= bounds.min.y
                && worldPoint.y <= bounds.max.y)
            {
                return true;
            }
        }

        return base.IsTouchOnObject(screenPosition);
    }

    protected override bool IsMouseOnObject(Vector2 screenPosition)
    {
        return IsTouchOnObject(screenPosition);
    }
}
