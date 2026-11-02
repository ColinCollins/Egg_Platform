using UnityEngine;

/// <summary>
/// Level 134 的池化台阶实体。攀爬阶段仅当前阶与下一阶提供实体承托；进入掉落态后启用
/// 重力与拖拽。Actor 接载、真实地板判定和对象池生命周期由 Level134Ctrl 统一编排。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level134Step : MonoBehaviour
{
    public enum StepState
    {
        Pooled,
        Climbable,
        Falling,
        Resting,
        Carried,
        ExitPlatform,
    }

    private Level134Ctrl owner;
    private SpriteRenderer spriteRenderer;
    private BoxCollider2D stepCollider;
    private Rigidbody2D stepBody;
    private StepState stateBeforeDrag;
    private Vector2 dragOffset;
    private Vector2 lastDragDelta;
    private bool touchingRealGround;
    private bool dragging;
    private bool simulatedBeforeSuspend;
    private bool suspended;
    private bool frozenForDoor;
    private int dragPointerId = int.MinValue;
    private int normalSortingOrder;

    public StepState State { get; private set; } = StepState.Pooled;
    public int LayerNumber { get; private set; }
    public bool IsDragging => dragging;
    public bool IsCarrier => State == StepState.Carried;
    public bool TouchingRealGround => touchingRealGround;
    public int DragPointerId => dragPointerId;
    public Vector2 LastDragDelta => lastDragDelta;
    public Collider2D Collider => stepCollider;
    public Rigidbody2D Body => stepBody;
    public SpriteRenderer Renderer => spriteRenderer;
    public float VisualHeight => spriteRenderer != null && spriteRenderer.sprite != null
        ? spriteRenderer.sprite.bounds.size.y * Mathf.Abs(transform.lossyScale.y)
        : 0f;
    public float TopY => stepCollider != null ? stepCollider.bounds.max.y : transform.position.y;
    public Bounds VisualBounds => spriteRenderer != null
        ? spriteRenderer.bounds
        : new Bounds(transform.position, Vector3.zero);

    public bool CanBeDragged => gameObject.activeInHierarchy
        && !frozenForDoor
        && (State == StepState.Falling || State == StepState.Resting || State == StepState.Carried);

    internal void Initialize(
        Level134Ctrl stepOwner,
        Sprite sprite,
        Vector3 scale,
        string sortingLayerName,
        int sortingOrder)
    {
        owner = stepOwner;
        transform.localScale = scale;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        spriteRenderer.sprite = sprite;
        spriteRenderer.sortingLayerName = sortingLayerName;
        spriteRenderer.sortingOrder = sortingOrder;
        normalSortingOrder = sortingOrder;

        stepCollider = GetComponent<BoxCollider2D>();
        if (stepCollider == null)
        {
            stepCollider = gameObject.AddComponent<BoxCollider2D>();
        }

        Vector2 spriteSize = sprite != null ? sprite.bounds.size : new Vector2(1.3f, 0.57f);
        float colliderHeight = Mathf.Max(0.12f, spriteSize.y * 0.28f);
        stepCollider.size = new Vector2(spriteSize.x, colliderHeight);
        stepCollider.offset = new Vector2(0f, spriteSize.y * 0.5f - colliderHeight * 0.5f);

        stepBody = GetComponent<Rigidbody2D>();
        if (stepBody == null)
        {
            stepBody = gameObject.AddComponent<Rigidbody2D>();
        }

        stepBody.freezeRotation = true;
        stepBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        stepBody.interpolation = RigidbodyInterpolation2D.Interpolate;
        stepBody.constraints = RigidbodyConstraints2D.FreezeRotation;
        Recycle();
    }

    internal void ConfigureClimbable(Vector3 worldPosition, int layerNumber)
    {
        ResetTransientState();
        transform.SetParent(owner.StepRoot, true);
        transform.position = worldPosition;
        transform.rotation = Quaternion.identity;
        gameObject.layer = owner.StepLayer;
        LayerNumber = layerNumber;
        State = StepState.Climbable;
        gameObject.SetActive(true);

        stepCollider.enabled = false;
        stepCollider.isTrigger = false;
        stepBody.simulated = true;
        stepBody.bodyType = RigidbodyType2D.Kinematic;
        stepBody.gravityScale = 0f;
        stepBody.linearVelocity = Vector2.zero;
        stepBody.angularVelocity = 0f;
        owner.SetStepActorCollisionIgnored(this, true);
    }

    internal void SetClimbableColliderEnabled(bool enabled)
    {
        if (State != StepState.Climbable || stepCollider == null)
        {
            return;
        }

        stepCollider.enabled = enabled;
        stepCollider.isTrigger = false;
        owner.SetStepActorCollisionIgnored(this, !enabled || owner.ShouldIgnoreActorCollision(this));
    }

    internal void ConfigureExitPlatform(Vector3 worldPosition)
    {
        ResetTransientState();
        transform.SetParent(owner.StepRoot, true);
        transform.position = worldPosition;
        transform.rotation = Quaternion.identity;
        gameObject.layer = owner.GroundLayer;
        LayerNumber = 0;
        State = StepState.ExitPlatform;
        gameObject.SetActive(true);

        stepCollider.enabled = true;
        stepCollider.isTrigger = false;
        stepBody.simulated = true;
        stepBody.bodyType = RigidbodyType2D.Kinematic;
        stepBody.gravityScale = 0f;
        stepBody.linearVelocity = Vector2.zero;
        stepBody.angularVelocity = 0f;
        owner.SetStepActorCollisionIgnored(this, owner.ShouldIgnoreActorCollision(this));
    }

    internal void BeginFalling()
    {
        if (State == StepState.Pooled || State == StepState.ExitPlatform)
        {
            return;
        }

        EndDragInternal(false);
        State = StepState.Falling;
        gameObject.layer = owner.StepLayer;
        stepCollider.enabled = true;
        stepCollider.isTrigger = false;
        stepBody.simulated = true;
        stepBody.bodyType = RigidbodyType2D.Dynamic;
        stepBody.gravityScale = owner.StepGravityScale;
        stepBody.linearVelocity = new Vector2(0f, stepBody.linearVelocity.y);
        stepBody.angularVelocity = 0f;
        owner.SetStepActorCollisionIgnored(this, true);
    }

    internal bool BeginDrag(int pointerId, Vector2 pointerWorldPosition)
    {
        if (dragging || !CanBeDragged || !owner.CanBeginStepDrag(this, pointerId))
        {
            return false;
        }

        dragging = true;
        dragPointerId = pointerId;
        stateBeforeDrag = State;
        dragOffset = (Vector2)transform.position - pointerWorldPosition;
        lastDragDelta = Vector2.zero;
        gameObject.layer = owner.StepLayer;
        spriteRenderer.sortingOrder = owner.DraggedStepSortingOrder;
        stepBody.bodyType = RigidbodyType2D.Kinematic;
        stepBody.gravityScale = 0f;
        stepBody.linearVelocity = Vector2.zero;
        stepBody.angularVelocity = 0f;
        owner.SetStepActorCollisionIgnored(this, true);
        owner.NotifyStepDragStarted(this);
        return true;
    }

    internal void MoveDrag(Vector2 pointerWorldPosition)
    {
        if (!dragging)
        {
            return;
        }

        Vector2 desired = pointerWorldPosition + dragOffset;
        desired = owner.ClampDraggedStepPosition(this, desired);
        lastDragDelta = desired - stepBody.position;
        stepBody.position = desired;
        transform.position = new Vector3(desired.x, desired.y, transform.position.z);
        stepBody.linearVelocity = Vector2.zero;
        owner.NotifyDraggedStepMoved(this);
    }

    internal void EndDrag(int pointerId)
    {
        if (!dragging || dragPointerId != pointerId)
        {
            return;
        }

        EndDragInternal(true);
    }

    internal void CancelDrag()
    {
        EndDragInternal(true);
    }

    internal void InterruptDrag()
    {
        EndDragInternal(false);
    }

    internal void AttachCarrier()
    {
        State = StepState.Carried;
        stepCollider.enabled = true;
        stepCollider.isTrigger = false;
        stepBody.simulated = true;
        owner.SetStepActorCollisionIgnored(this, true);
        if (!dragging)
        {
            stepBody.bodyType = RigidbodyType2D.Dynamic;
            stepBody.gravityScale = owner.StepGravityScale;
            stepBody.linearVelocity = new Vector2(0f, stepBody.linearVelocity.y);
        }
    }

    internal void MarkResting()
    {
        if (State == StepState.Falling || State == StepState.Carried)
        {
            State = StepState.Resting;
            gameObject.layer = owner.GroundLayer;
            owner.SetStepActorCollisionIgnored(this, owner.ShouldIgnoreActorCollision(this));
        }
    }

    internal void MakeStaticSupport()
    {
        if (State != StepState.Climbable)
        {
            return;
        }

        State = StepState.ExitPlatform;
        gameObject.layer = owner.GroundLayer;
        stepCollider.enabled = true;
        stepCollider.isTrigger = false;
        stepBody.simulated = true;
        stepBody.bodyType = RigidbodyType2D.Kinematic;
        stepBody.gravityScale = 0f;
        stepBody.linearVelocity = Vector2.zero;
        stepBody.angularVelocity = 0f;
        owner.SetStepActorCollisionIgnored(this, owner.ShouldIgnoreActorCollision(this));
    }

    internal void SetSuspended(bool suspended)
    {
        if (State == StepState.Pooled || stepBody == null || frozenForDoor)
        {
            return;
        }

        if (suspended)
        {
            if (this.suspended)
            {
                return;
            }

            simulatedBeforeSuspend = stepBody.simulated;
            stepBody.simulated = false;
            this.suspended = true;
        }
        else
        {
            if (!this.suspended)
            {
                return;
            }

            stepBody.simulated = simulatedBeforeSuspend;
            this.suspended = false;
        }
    }

    internal void FreezeForDoor()
    {
        EndDragInternal(false);
        frozenForDoor = true;
        suspended = false;
        stepBody.bodyType = RigidbodyType2D.Kinematic;
        stepBody.simulated = true;
        stepBody.gravityScale = 0f;
        stepBody.linearVelocity = Vector2.zero;
        stepBody.angularVelocity = 0f;
        stepCollider.enabled = true;
        stepCollider.isTrigger = false;
    }

    internal void Recycle()
    {
        ResetTransientState();
        State = StepState.Pooled;
        LayerNumber = 0;
        if (stepCollider != null)
        {
            stepCollider.enabled = false;
            stepCollider.isTrigger = true;
        }

        if (stepBody != null)
        {
            stepBody.simulated = false;
            stepBody.bodyType = RigidbodyType2D.Kinematic;
            stepBody.gravityScale = 0f;
            stepBody.linearVelocity = Vector2.zero;
            stepBody.angularVelocity = 0f;
        }

        gameObject.SetActive(false);
    }

    private void EndDragInternal(bool notifyOwner)
    {
        if (!dragging)
        {
            return;
        }

        int releasedPointer = dragPointerId;
        dragging = false;
        dragPointerId = int.MinValue;
        lastDragDelta = Vector2.zero;
        spriteRenderer.sortingOrder = normalSortingOrder;

        State = stateBeforeDrag == StepState.Carried || State == StepState.Carried
            ? StepState.Carried
            : StepState.Falling;
        stepBody.bodyType = RigidbodyType2D.Dynamic;
        stepBody.gravityScale = owner.StepGravityScale;
        stepBody.linearVelocity = new Vector2(0f, Mathf.Min(0f, stepBody.linearVelocity.y));
        stepBody.angularVelocity = 0f;

        if (notifyOwner)
        {
            owner.NotifyStepDragEnded(this, releasedPointer);
        }
    }

    private void ResetTransientState()
    {
        dragging = false;
        dragPointerId = int.MinValue;
        touchingRealGround = false;
        stateBeforeDrag = StepState.Pooled;
        simulatedBeforeSuspend = false;
        suspended = false;
        frozenForDoor = false;
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = normalSortingOrder;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        owner?.NotifyActorTouchedClimbable(this);
        UpdateGroundContact(collision, true);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        owner?.NotifyActorTouchedClimbable(this);
        UpdateGroundContact(collision, true);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        UpdateGroundContact(collision, false);
    }

    private void UpdateGroundContact(Collision2D collision, bool touching)
    {
        if (owner == null || collision == null || !owner.IsRealGround(collision.collider))
        {
            return;
        }

        touchingRealGround = touching;
        if (touching)
        {
            owner.NotifyStepTouchedGround(this);
        }
    }
}
