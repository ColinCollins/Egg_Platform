using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Level 115 单个交换位置的 EventSystem 适配器。业务所有权、交换和关闭判定均回到 Popup。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level115QuizDragHandle : MonoBehaviour,
    IPointerDownHandler,
    IPointerClickHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    ICancelHandler
{
    private const int NoPointer = int.MinValue;

    private readonly Dictionary<int, int> _pointerDownVersions = new Dictionary<int, int>();
    private readonly HashSet<int> _dragPointers = new HashSet<int>();

    private Level115QuizPopup _owner;
    private int _positionIndex;
    private int _activePointerId = NoPointer;

    public void Configure(Level115QuizPopup owner, int positionIndex)
    {
        _owner = owner;
        _positionIndex = positionIndex;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData == null)
            return;

        _pointerDownVersions[eventData.pointerId] = _owner != null ? _owner.InteractionVersion : -1;
        _dragPointers.Remove(eventData.pointerId);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null)
            return;

        int pointerId = eventData.pointerId;
        if (!_dragPointers.Contains(pointerId) &&
            _pointerDownVersions.TryGetValue(pointerId, out int interactionVersion))
        {
            _owner?.HandlePositionClick(_positionIndex, interactionVersion);
        }

        ClearPointer(pointerId);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_owner == null || eventData == null)
            return;

        int pointerId = eventData.pointerId;
        _dragPointers.Add(pointerId);
        if (!_pointerDownVersions.TryGetValue(pointerId, out int interactionVersion) ||
            !_owner.TryBeginDrag(_positionIndex, interactionVersion, eventData))
        {
            return;
        }

        _activePointerId = pointerId;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_activePointerId == NoPointer || eventData == null || eventData.pointerId != _activePointerId)
            return;

        _owner?.ContinueDrag(_positionIndex, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (eventData == null)
            return;

        int pointerId = eventData.pointerId;
        if (pointerId == _activePointerId)
        {
            _activePointerId = NoPointer;
            _owner?.EndDrag(_positionIndex, pointerId, eventData);
        }

        ClearPointer(pointerId);
    }

    public void OnCancel(BaseEventData eventData)
    {
        CancelOwnedDrag();
    }

    private void OnDisable()
    {
        CancelOwnedDrag();
    }

    private void CancelOwnedDrag()
    {
        if (_activePointerId != NoPointer)
        {
            int pointerId = _activePointerId;
            _activePointerId = NoPointer;
            _owner?.CancelDrag(_positionIndex, pointerId);
        }

        _pointerDownVersions.Clear();
        _dragPointers.Clear();
    }

    private void ClearPointer(int pointerId)
    {
        _pointerDownVersions.Remove(pointerId);
        _dragPointers.Remove(pointerId);
    }
}
