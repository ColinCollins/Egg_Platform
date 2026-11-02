using UnityEngine;
using UnityEngine.EventSystems;

public enum Level111UiGestureKind
{
    LevelBadge,
    ShopRotate,
    PauseIcon,
    HintIcon
}

/// <summary>把 EventSystem 手势转发给 Level111 的共享 UI 临时控制器。</summary>
public sealed class Level111UiGestureHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private Level111UiRepairController owner;

    public Level111UiGestureKind Kind { get; private set; }
    public RectTransform RectTransform { get; private set; }

    public void Configure(Level111UiRepairController gestureOwner, Level111UiGestureKind gestureKind)
    {
        owner = gestureOwner;
        Kind = gestureKind;
        RectTransform = transform as RectTransform;
        enabled = RectTransform != null;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        owner?.OnGestureBegin(this, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        owner?.OnGestureDrag(this, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        owner?.OnGestureEnd(this, eventData);
    }
}
