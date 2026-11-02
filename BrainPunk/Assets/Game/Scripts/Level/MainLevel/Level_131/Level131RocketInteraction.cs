using Game.ItemEvent;
using UnityEngine;

/// <summary>
/// 初始火箭的 Actor 拾取与世界拖拽入口。拖拽期间不触发背包路线，
/// 结束时由 Level131Ctrl 判断是否吸附到方块安装点。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level131RocketInteraction : MonoBehaviour
{
    private Level131Ctrl owner;
    private DragEventsListener dragListener;
    private Collider2D interactionCollider;
    private MoveFloatHandle floatHandle;
    private bool isDragging;
    private bool isConsumed;
    private bool isReturning;
    private bool isPaused;

    public bool IsDragging => isDragging;
    public Vector3 RestLocalPosition { get; private set; }

    public void Initialize(Level131Ctrl levelOwner)
    {
        owner = levelOwner;
        RestLocalPosition = transform.localPosition;
        floatHandle = GetComponent<MoveFloatHandle>();
        interactionCollider = GetComponent<Collider2D>();
        dragListener = GetComponent<DragEventsListener>() ?? gameObject.AddComponent<DragEventsListener>();
        dragListener.BeginDragHandlers -= HandleBeginDrag;
        dragListener.DragHandlers -= HandleDrag;
        dragListener.EndDragHandlers -= HandleEndDrag;
        dragListener.BeginDragHandlers += HandleBeginDrag;
        dragListener.DragHandlers += HandleDrag;
        dragListener.EndDragHandlers += HandleEndDrag;
    }

    public void Consume()
    {
        isConsumed = true;
        isDragging = false;
        isReturning = false;
        floatHandle?.StopMove();
        dragListener?.SetEnableDrag(false);
        if (interactionCollider != null)
            interactionCollider.enabled = false;
    }

    public void RestoreForRetry()
    {
        isConsumed = false;
        isDragging = false;
        isReturning = false;
        if (interactionCollider != null)
            interactionCollider.enabled = true;
        dragListener?.SetEnableDrag(true);
        floatHandle?.StartMove();
        if (isPaused)
            floatHandle?.PauseMove();
    }

    public void BeginReturn()
    {
        isReturning = true;
        floatHandle?.StopMove();
        dragListener?.SetEnableDrag(false);
        if (interactionCollider != null)
            interactionCollider.enabled = false;
    }

    public void SetPaused(bool paused)
    {
        isPaused = paused;
        if (paused)
            floatHandle?.PauseMove();
        else if (!isConsumed && !isDragging && !isReturning)
            floatHandle?.ResumeMove();
    }

    private void HandleBeginDrag(Transform _)
    {
        if (isPaused)
            return;

        if (isConsumed || isReturning || owner == null || !owner.CanBeginRocketDrag(this))
        {
            dragListener?.SetEnableDrag(false);
            return;
        }

        isDragging = true;
        floatHandle?.PauseMove();
        owner.BeginRocketDrag(this, dragListener);
    }

    private void HandleDrag(Transform _)
    {
        if (isDragging && owner != null)
            owner.UpdateRocketDrag(this, dragListener);
    }

    private void HandleEndDrag(Transform _)
    {
        if (!isDragging)
            return;

        isDragging = false;
        owner?.EndRocketDrag(this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isConsumed || isDragging || isReturning || isPaused || owner == null || other == null)
            return;

        ActorCtrl actor = other.GetComponentInParent<ActorCtrl>();
        if (actor != null && actor.CompareTag("Player"))
            owner.TryEquipBackpack(this, actor);
    }

    private void OnDestroy()
    {
        floatHandle?.StopMove();
        if (dragListener == null)
            return;

        dragListener.BeginDragHandlers -= HandleBeginDrag;
        dragListener.DragHandlers -= HandleDrag;
        dragListener.EndDragHandlers -= HandleEndDrag;
    }
}
