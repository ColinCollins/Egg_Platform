using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Level 129 local pointer adapter for the shared aim button visual. It keeps
/// the shared CustomButton feedback but owns tap-versus-hold drag semantics.
/// </summary>
[DisallowMultipleComponent]
public sealed class Level129AimDragHandle : MonoBehaviour,
    IPointerDownHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IPointerUpHandler,
    ICancelHandler
{
    private const int InvalidPointerId = int.MinValue;

    private Level129Ctrl owner;
    private bool pointerDown;
    private int pointerId = InvalidPointerId;
    private float pointerDownTime;
    private Vector2 latestScreenPosition;

    internal void Initialize(Level129Ctrl newOwner)
    {
        owner = newOwner;
    }

    private void Update()
    {
        if (!pointerDown || owner == null)
            return;

        owner.HandleAimPointerHold(
            latestScreenPosition,
            Time.unscaledTime - pointerDownTime);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData == null || owner == null || !owner.CanBeginAimPointer())
            return;

        pointerDown = true;
        pointerId = eventData.pointerId;
        pointerDownTime = Time.unscaledTime;
        latestScreenPosition = eventData.position;
        owner.HandleAimPointerDown(latestScreenPosition);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (IsOwnedPointer(eventData))
            eventData.eligibleForClick = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsOwnedPointer(eventData))
            return;

        latestScreenPosition = eventData.position;
        owner?.HandleAimPointerMoved(
            latestScreenPosition,
            Time.unscaledTime - pointerDownTime);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (IsOwnedPointer(eventData))
            eventData.eligibleForClick = false;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!IsOwnedPointer(eventData))
            return;

        latestScreenPosition = eventData.position;
        owner?.HandleAimPointerReleased(
            latestScreenPosition,
            Time.unscaledTime - pointerDownTime,
            false);
        ResetPointerState();
    }

    public void OnCancel(BaseEventData eventData)
    {
        if (!pointerDown)
            return;

        owner?.HandleAimPointerReleased(
            latestScreenPosition,
            Time.unscaledTime - pointerDownTime,
            true);
        ResetPointerState();
    }

    private void OnDisable()
    {
        if (pointerDown)
        {
            owner?.HandleAimPointerReleased(
                latestScreenPosition,
                Time.unscaledTime - pointerDownTime,
                true);
        }

        ResetPointerState();
    }

    internal void Detach()
    {
        owner = null;
        ResetPointerState();
    }

    private bool IsOwnedPointer(PointerEventData eventData)
    {
        return pointerDown && eventData != null && eventData.pointerId == pointerId;
    }

    private void ResetPointerState()
    {
        pointerDown = false;
        pointerId = InvalidPointerId;
        pointerDownTime = 0f;
    }
}
