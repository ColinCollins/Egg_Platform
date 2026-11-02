using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>139 世界对象点击；复用 EventSystem 指针路由，尊重 HUD，不添加实体碰撞。</summary>
public sealed class Level139ClickTarget : BaseRaycaster, IPointerClickHandler
{
    private Camera gameplayCamera;
    private Func<bool> canClick;
    private Func<Bounds> getBounds;
    private Action clicked;
    private int priority;
    private readonly List<RaycastResult> uiHits = new List<RaycastResult>();

    public override Camera eventCamera => null;
    public override int sortOrderPriority => priority;

    public void Bind(Camera camera, Func<bool> available, Func<Bounds> bounds, Action action, int order)
    {
        gameplayCamera = camera;
        canClick = available;
        getBounds = bounds;
        clicked = action;
        priority = order;
    }

    public override void Raycast(PointerEventData data, List<RaycastResult> results)
    {
        if (gameplayCamera == null || canClick == null || !canClick() || getBounds == null)
            return;
        // 与 138 同样优先让弹窗/移动 HUD 消费指针，不递归 RaycastAll。
        foreach (BaseRaycaster raycaster in RaycasterManager.GetRaycasters())
        {
            if (!(raycaster is GraphicRaycaster graphic) || !graphic.isActiveAndEnabled)
                continue;
            uiHits.Clear();
            graphic.Raycast(data, uiHits);
            if (uiHits.Count > 0)
                return;
        }
        Bounds bounds = getBounds();
        Ray ray = gameplayCamera.ScreenPointToRay(data.position);
        if (!new Plane(Vector3.forward, bounds.center).Raycast(ray, out float distance))
            return;
        Vector3 world = ray.GetPoint(distance);
        if (world.x < bounds.min.x || world.x > bounds.max.x
            || world.y < bounds.min.y || world.y > bounds.max.y)
            return;
        results.Add(new RaycastResult
        {
            gameObject = gameObject,
            module = this,
            distance = distance,
            index = results.Count,
            depth = priority,
            sortingOrder = priority,
            screenPosition = data.position,
            worldPosition = world
        });
    }

    public void OnPointerClick(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Left && canClick != null && canClick())
            clicked?.Invoke();
    }
}
