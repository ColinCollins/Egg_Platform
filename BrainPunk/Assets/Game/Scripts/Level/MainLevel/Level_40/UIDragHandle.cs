using System;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class UIDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public Action<PointerEventData> OnBeginDragEvent;
    public Action<PointerEventData> OnDragEvent;
    public Action<PointerEventData> OnEndDragEvent;

    public Vector2 BeginScreenPosition { get; private set; }
    public Vector2 RelativeDragDelta { get; private set; }
    public bool IsDragging { get; private set; }

    public void OnBeginDrag(PointerEventData eventData)
    {
        IsDragging = true;
        BeginScreenPosition = eventData.position;
        RelativeDragDelta = Vector2.zero;
        OnBeginDragEvent?.Invoke(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsDragging)
            return;

        RelativeDragDelta = eventData.position - BeginScreenPosition;
        OnDragEvent?.Invoke(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!IsDragging)
            return;

        RelativeDragDelta = eventData.position - BeginScreenPosition;
        IsDragging = false;
        OnEndDragEvent?.Invoke(eventData);
    }
}
