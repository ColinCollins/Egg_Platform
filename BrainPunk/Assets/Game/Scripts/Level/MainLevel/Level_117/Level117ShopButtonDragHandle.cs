using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Level117 商店按钮的局部 EventSystem 转发器。
/// 真实拖拽才阻断 CustomButton 的点击；轻触仍由共享 CustomButton 正常处理。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level117ShopButtonDragHandle : MonoBehaviour,
    IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler, ICancelHandler
{
    private const int InvalidPointerId = int.MinValue;

    private Level117ShopButtonDrag owner;
    private RectTransform rectTransform;
    private CustomButton customButton;
    private PointerEventData activePointerData;
    private Vector2 pointerOffset;
    private bool pointerDown;
    private bool dragRecognized;
    private int activePointerId = InvalidPointerId;

    /// <summary>当前指针是否已经超过 EventSystem 阈值并进入真实拖拽。</summary>
    public bool IsDragging { get; private set; }

    /// <summary>当前是否存在已按下但尚未结束的指针周期。</summary>
    public bool HasActivePointerCycle => pointerDown || IsDragging;

    internal void Initialize(Level117ShopButtonDrag newOwner, RectTransform button, CustomButton buttonComponent)
    {
        owner = newOwner;
        rectTransform = button != null ? button : transform as RectTransform;
        customButton = buttonComponent != null ? buttonComponent : GetComponent<CustomButton>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData == null)
            return;

        if (pointerDown || IsDragging)
        {
            eventData.eligibleForClick = false;
            return;
        }

        pointerDown = owner != null && owner.CanBeginDrag();
        IsDragging = false;
        dragRecognized = false;
        if (!pointerDown || rectTransform == null)
        {
            eventData.eligibleForClick = false;
            activePointerId = InvalidPointerId;
            activePointerData = null;
            return;
        }

        activePointerId = eventData.pointerId;
        activePointerData = eventData;

        Vector2 pointerLocal = owner.GetPointerLocalPosition(eventData.position, eventData.pressEventCamera);
        pointerOffset = rectTransform.anchoredPosition - pointerLocal;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // EventSystem 仅在位移超过 pixelDragThreshold 后调用 OnBeginDrag。
        if (!IsOwnedPointer(eventData))
            return;

        if (!pointerDown || owner == null || !owner.CanBeginDrag())
        {
            CancelPointerCycle(true);
            return;
        }

        eventData.eligibleForClick = false;
        IsDragging = true;
        dragRecognized = true;
        owner.OnDragStarted();
        MoveToPointer(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!IsOwnedPointer(eventData))
            return;

        if (!IsDragging || owner == null || !owner.CanBeginDrag())
        {
            if (IsDragging)
                CancelPointerCycle(true);
            return;
        }

        MoveToPointer(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!IsOwnedPointer(eventData) || !IsDragging)
            return;

        eventData.eligibleForClick = false;
        IsDragging = false;
        owner?.OnDragEnded();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!IsOwnedPointer(eventData))
            return;

        if (IsDragging)
        {
            eventData.eligibleForClick = false;
            IsDragging = false;
            owner?.OnDragEnded();
        }

        if (dragRecognized)
        {
            eventData.eligibleForClick = false;
            ResetPointerState();
            owner?.OnSuppressedPointerReleased();
            return;
        }

        if (pointerDown && (owner == null || !owner.CanBeginDrag()))
        {
            ResetPointerState();
            owner?.SuppressReleasedPointerClick();
            return;
        }

        ResetPointerState();
    }

    public void OnCancel(BaseEventData eventData)
    {
        if (!HasActivePointerCycle)
            return;

        CancelPointerCycle(true);
    }

    internal void CancelPointerCycle(bool suppressClick)
    {
        bool wasActive = pointerDown || IsDragging;
        if (wasActive && suppressClick)
        {
            if (activePointerData != null)
                activePointerData.eligibleForClick = false;

            ResetPointerState();
            owner?.SuppressReleasedPointerClick();
        }
        else if (!suppressClick)
        {
            ResetPointerState();
            if (customButton != null)
                customButton.IsBlockClick = false;
        }
    }

    internal void Detach()
    {
        owner = null;
        rectTransform = null;
        customButton = null;
        ResetPointerState();
    }

    private void MoveToPointer(PointerEventData eventData)
    {
        if (rectTransform == null || owner == null)
            return;

        Vector2 pointerLocal = owner.GetPointerLocalPosition(eventData.position, eventData.pressEventCamera);
        rectTransform.anchoredPosition = owner.ClampButtonPosition(pointerLocal + pointerOffset);
    }

    private bool IsOwnedPointer(PointerEventData eventData)
    {
        return eventData != null && activePointerId != InvalidPointerId && eventData.pointerId == activePointerId;
    }

    private void ResetPointerState()
    {
        pointerDown = false;
        IsDragging = false;
        dragRecognized = false;
        activePointerId = InvalidPointerId;
        activePointerData = null;
    }
}
