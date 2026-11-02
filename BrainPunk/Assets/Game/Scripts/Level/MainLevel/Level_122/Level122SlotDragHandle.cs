using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>下方空方格拖动转发器；面板保证整局最多只有一个方格能吸附到实体锁。</summary>
[DisallowMultipleComponent]
public sealed class Level122SlotDragHandle : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    ICancelHandler
{
    private Level122MatchPanel _owner;
    private int _slotIndex;
    private int _activePointerId = int.MinValue;

    public void Configure(Level122MatchPanel owner, int slotIndex)
    {
        _owner = owner;
        _slotIndex = slotIndex;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_owner == null || eventData == null || !_owner.TryBeginSlotDrag(_slotIndex, eventData))
            return;

        _activePointerId = eventData.pointerId;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData != null && eventData.pointerId == _activePointerId)
            _owner?.ContinueSlotDrag(_slotIndex, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData == null || eventData.pointerId != _activePointerId)
            return;

        _activePointerId = int.MinValue;
        _owner?.EndSlotDrag(_slotIndex, eventData);
    }

    public void OnCancel(BaseEventData eventData)
    {
        CancelActiveDrag();
    }

    private void OnDisable()
    {
        CancelActiveDrag();
    }

    private void CancelActiveDrag()
    {
        if (_activePointerId == int.MinValue)
            return;

        _activePointerId = int.MinValue;
        _owner?.CancelSlotDrag(_slotIndex);
    }

    private void OnDestroy()
    {
        _owner = null;
    }
}
