using System;
using System.Collections;
using Bear.EventSystem;
using Game.Events;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level097 气球玩法控制器：门位气球负责补生，拖出的自由球可上浮、重抓或戳破，
/// 自由绳尾接触角色后进入载人飞行，载人球戳破时恢复角色重力。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level97Ctrl : MonoBehaviour
{
    #region Constants

    // 气球本体为 105，门根 SortingGroup 为 100；补生视觉低一层，确保从整扇门后出现。
    private const int RegrowBehindDoorSortingOrder = 99;
    private const float CarryingCeilingSkin = 0.02f;
    private const int CarryingCeilingHitCapacity = 8;

    #endregion

    #region Serialized Types

    [Serializable]
    private sealed class BalloonSlot
    {
        /// <summary>整只气球的槽位根节点；用于识别属于该气球的点击对象。</summary>
        [Tooltip("整只气球的槽位根节点。")]
        public Transform root;

        /// <summary>承载气球贴图、碰撞和关节的 Nail 刚体。</summary>
        [Tooltip("气球 Nail Rigidbody2D。")]
        public Rigidbody2D body;

        /// <summary>DLevel_045 风格的圆形气球碰撞体，同时作为点击和拖拽命中区。</summary>
        [Tooltip("气球圆形碰撞体。")]
        public Collider2D collider;

        /// <summary>只限制最大绳长的 DistanceJoint2D；拖拽和载人时临时停用。</summary>
        [Tooltip("连接门的最大距离关节。")]
        public DistanceJoint2D doorJoint;

        /// <summary>气球到门之间的可见绳线。</summary>
        [Tooltip("气球到门之间的可见绳线。")]
        public Level5TrackLine line;

        /// <summary>Line 上的独立拾取碰撞体；Inspector 显式绑定，仅用于选球，不承担物理或绳尾吸附。</summary>
        [Tooltip("绑定同一 Line 上的 PolygonCollider2D；仅用于点击和拖拽拾取。")]
        [SerializeField] public PolygonCollider2D linePickCollider;

        /// <summary>真实气球贴图；点击补生期间只隐藏该渲染器，不改变气球物理。</summary>
        [Tooltip("Nail 上的气球 SpriteRenderer。")]
        public SpriteRenderer spriteRenderer;
    }

    private sealed class DetachedBalloon
    {
        public GameObject Root;
        public Rigidbody2D Body;
        public Collider2D Collider;
        public SpriteRenderer SpriteRenderer;
        public DistanceJoint2D DoorJoint;
        public Level5TrackLine Line;
        /// <summary>随 Line 一起克隆的拾取碰撞体；沿用源 Prefab 的接触隔离，仅查询当前绳段。</summary>
        public PolygonCollider2D LinePickCollider;
        public LineRenderer LineRenderer;
        public Transform LooseRopeEnd;
        public Collider2D IgnoredSourceCollider;
        public int SourceIndex;
        public bool IgnoresCarryingCeiling;
        public bool IsDestroyed;
    }

    private readonly struct BalloonInitialState
    {
        public readonly Vector3 BodyLocalPosition;
        public readonly Quaternion BodyLocalRotation;
        public readonly Vector3 BodyLocalScale;
        public readonly RigidbodyType2D BodyType;
        public readonly RigidbodyConstraints2D Constraints;
        public readonly float GravityScale;
        public readonly bool Simulated;
        public readonly bool ColliderEnabled;
        public readonly bool JointEnabled;
        public readonly bool RendererEnabled;
        public readonly bool LineRendererEnabled;
        public readonly Transform LineStartPoint;
        public readonly Transform LineEndPoint;

        public BalloonInitialState(BalloonSlot slot)
        {
            Transform bodyTransform = slot?.body != null ? slot.body.transform : null;
            BodyLocalPosition = bodyTransform != null ? bodyTransform.localPosition : Vector3.zero;
            BodyLocalRotation = bodyTransform != null ? bodyTransform.localRotation : Quaternion.identity;
            BodyLocalScale = bodyTransform != null ? bodyTransform.localScale : Vector3.one;
            BodyType = slot?.body != null ? slot.body.bodyType : RigidbodyType2D.Dynamic;
            Constraints = slot?.body != null
                ? slot.body.constraints
                : RigidbodyConstraints2D.FreezePositionX | RigidbodyConstraints2D.FreezeRotation;
            GravityScale = slot?.body != null ? slot.body.gravityScale : -0.6f;
            Simulated = slot?.body != null && slot.body.simulated;
            ColliderEnabled = slot?.collider != null && slot.collider.enabled;
            JointEnabled = slot?.doorJoint != null && slot.doorJoint.enabled;
            RendererEnabled = slot?.spriteRenderer != null && slot.spriteRenderer.enabled;
            LineRenderer lineRenderer = slot?.line != null ? slot.line.GetComponent<LineRenderer>() : null;
            LineRendererEnabled = lineRenderer != null && lineRenderer.enabled;
            LineStartPoint = slot?.line != null ? slot.line.GetStartPoint() : null;
            LineEndPoint = slot?.line != null ? slot.line.GetEndPoint() : null;
        }
    }

    #endregion

    #region Inspector

    [Header("Level097 场景引用")]
    [Tooltip("保留原关卡基础控制器，继续负责输入、失败和过关流程。")]
    [SerializeField] private BaseLevelCtrl baseLevel;
    [Tooltip("本关专用角色控制器；载人时屏蔽跳跃但保留左右移动。")]
    [SerializeField] private Level97ActorCtrl levelActor;
    [Tooltip("本关相机；用于把屏幕输入转换为世界坐标。")]
    [SerializeField] private Camera levelCamera;
    [Tooltip("Level011 原有的 Knock_Explosion；点击时克隆播放，不移动场景模板。")]
    [SerializeField] private GameObject explosionTemplate;
    [Tooltip("载人气球必须碰撞的顶部空气墙；命中后限制气球与角色继续上升。")]
    [SerializeField] private Collider2D carryingCeilingCollider;
    [Tooltip("按场景中的蓝、黄、粉三个槽位依次绑定。")]
    [SerializeField] private BalloonSlot[] balloons = Array.Empty<BalloonSlot>();

    [Header("点击与拖拽")]
    [Tooltip("屏幕位移超过该像素值后才进入拖拽；此前松手按点击爆炸处理。")]
    [SerializeField, Min(1f)] private float dragThresholdPixels = 60f;
    /// <summary>仅气球本体的拾取外扩距离；不改变物理或吸附范围，设为 0 关闭本体外扩，Line 拾取单独配置。</summary>
    [Tooltip("气球本体碰撞体边缘的拾取外扩世界距离；不影响物理和 Line 检测宽度。")]
    [SerializeField, Min(0f)] private float balloonPickPadding = 0.26f;
    /// <summary>整条当前绳线的拾取总宽度；按下时同步 Line Collider，设为 0 关闭绳线拾取。</summary>
    [Tooltip("Line Collider 的总宽度（世界单位）；端点各延伸半宽，运行时按下检测前同步。")]
    [SerializeField, Min(0f)] private float linePickWidth = 0.52f;
    [Tooltip("拖拽松手时，自由绳尾距离角色主碰撞体小于该世界距离即可吸附。")]
    [SerializeField, Min(0f)] private float attachTolerance = 0.05f;
    [Tooltip("气球脱离门后，从气球下沿垂下的自由绳长。")]
    [SerializeField, Min(0.1f)] private float looseRopeLength = 1.8f;

    [Header("爆炸与补生")]
    [Tooltip("纯视觉气球从门后长回真实气球位置所需时间。")]
    [SerializeField, Min(0.01f)] private float regrowDuration = 0.65f;
    [Tooltip("纯视觉气球起点相对门上绳点向下隐藏的距离。")]
    [SerializeField, Min(0f)] private float regrowHiddenOffsetY = 0.25f;

    [Header("载人飞行")]
    [Tooltip("气球载人后角色与气球共用的竖直上升速度。")]
    [SerializeField, Min(0.01f)] private float carryingRiseSpeed = 2.5f;
    [Tooltip("载人气球中心相对角色中心的固定偏移。")]
    [SerializeField] private Vector2 carriedBalloonOffset = new(0f, 3.2f);
    [Tooltip("载人时绳子连接在角色顶部的局部偏移。")]
    [SerializeField] private Vector2 carriedRopeAttachOffset = new(0f, 0.55f);

    #endregion

    #region Runtime State

    private BalloonInitialState[] initialStates = Array.Empty<BalloonInitialState>();
    private bool[] slotRegrowing = Array.Empty<bool>();
    private GameObject[] regrowVisuals = Array.Empty<GameObject>();
    private LineRenderer[] lineRenderers = Array.Empty<LineRenderer>();
    private readonly System.Collections.Generic.List<DetachedBalloon> detachedBalloons = new();
    private readonly System.Collections.Generic.List<Collider2D> doorColliders = new();
    private Transform actorRopeAttachPoint;
    private Collider2D actorMainCollider;
    private EventSubscriber subscriber;
    private BalloonSlot pointerSlot;
    private DetachedBalloon pointerDetachedBalloon;
    private int pointerSlotIndex = -1;
    private int activeFingerId = int.MinValue;
    private Vector2 pointerStartScreenPosition;
    private Vector2 pointerOffsetWorld;
    private bool pointerTracking;
    private bool pointerDragging;
    private bool pointerStartedOnCarriedBalloon;
    private bool runtimeReady;
    private bool isPaused = true;
    private bool finishHandled;
    private DetachedBalloon carriedBalloon;
    private float actorInitialGravityScale;
    private CustomButton jumpButton;
    private bool jumpButtonInitialEnabled;
    private bool jumpButtonInitialInteractable;
    private bool hasJumpButtonState;
    private readonly RaycastHit2D[] carryingCeilingHits = new RaycastHit2D[CarryingCeilingHitCapacity];
    private readonly Vector2[] linePickPoints = new Vector2[4];
    private Coroutine findJumpButtonCoroutine;
    private Coroutine reconcileGameplayStateCoroutine;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        BindLevelEvents();
        if (runtimeReady)
            ReapplyDoorCollisionIgnores();
    }

    private void Start()
    {
        InitializeRuntime();
    }

    private void Update()
    {
        if (!runtimeReady)
            return;

        if (IsLevelFinished())
        {
            HandleFinishedState();
            return;
        }

        if (isPaused)
            return;

        ProcessPointerInput();
    }

    private void FixedUpdate()
    {
        if (!runtimeReady || isPaused || IsLevelFinished())
            return;

        RestoreSeparatedSourceCollisions();
        if (carriedBalloon != null)
            ApplyCarryingMotion();
    }

    private void OnDisable() => CleanupRuntimeState();

    private void OnDestroy() => CleanupRuntimeState();

    private void CleanupRuntimeState()
    {
        EventsUtils.ResetEvents(ref subscriber);
        StopGameplayStateReconcile();
        CancelPointer(true);
        RestoreRegrowVisuals();
        ReleaseCarriedBalloon(true);
        DestroyAllDetachedBalloons();
        RestoreAllBalloonPhysics();
    }

    #endregion

    #region Initialization

    private void InitializeRuntime()
    {
        baseLevel ??= GetComponent<BaseLevelCtrl>();
        levelActor ??= baseLevel != null ? baseLevel.Actor as Level97ActorCtrl : null;
        levelCamera ??= GetComponentInChildren<Camera>(true);
        levelCamera ??= Camera.main;

        balloons ??= Array.Empty<BalloonSlot>();
        initialStates = new BalloonInitialState[balloons.Length];
        slotRegrowing = new bool[balloons.Length];
        regrowVisuals = new GameObject[balloons.Length];
        lineRenderers = new LineRenderer[balloons.Length];
        doorColliders.Clear();

        bool allSlotsValid = balloons.Length > 0;
        for (int i = 0; i < balloons.Length; i++)
        {
            BalloonSlot slot = balloons[i];
            PrepareSlot(slot, i);
            RegisterDoorColliders(slot);
            initialStates[i] = new BalloonInitialState(slot);
            lineRenderers[i] = slot?.line != null ? slot.line.GetComponent<LineRenderer>() : null;
            allSlotsValid &= IsSlotBound(slot);
        }

        for (int i = 0; i < balloons.Length; i++)
            SetBalloonDoorCollision(balloons[i]?.collider, true);

        actorMainCollider = levelActor != null ? levelActor.GetComponent<Collider2D>() : null;
        runtimeReady = baseLevel != null && levelActor != null && actorMainCollider != null &&
                       levelCamera != null && allSlotsValid;
        if (!runtimeReady)
        {
            Debug.LogWarning("[Level97Ctrl] BaseLevelCtrl, Actor main collider, Camera or balloon bindings are missing.", this);
            return;
        }

        actorRopeAttachPoint = CreateActorRopeAttachPoint();
        findJumpButtonCoroutine = StartCoroutine(FindJumpButtonWhenReady());
    }

    private Transform CreateActorRopeAttachPoint()
    {
        if (levelActor == null)
            return null;

        Transform ropePoint = new GameObject("Level097_ActorRopeAttachPoint").transform;
        ropePoint.SetParent(levelActor.transform, false);
        ropePoint.localPosition = carriedRopeAttachOffset;
        return ropePoint;
    }

    private void PrepareSlot(BalloonSlot slot, int index)
    {
        if (slot?.body == null)
        {
            Debug.LogWarning($"[Level97Ctrl] Balloon slot {index} has no body.", this);
            return;
        }

        slot.spriteRenderer ??= slot.body.GetComponent<SpriteRenderer>();
        slot.body.bodyType = RigidbodyType2D.Dynamic;
        slot.body.gravityScale = -0.6f;
        slot.body.constraints = RigidbodyConstraints2D.FreezePositionX |
                                RigidbodyConstraints2D.FreezeRotation;
        slot.body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        if (slot.doorJoint != null)
        {
            slot.doorJoint.enabled = true;
            slot.doorJoint.maxDistanceOnly = true;
            slot.doorJoint.autoConfigureDistance = false;
            AlignJointWithVisibleLine(slot);
        }

        if (slot.collider != null)
            slot.collider.enabled = true;
    }

    private void RegisterDoorColliders(BalloonSlot slot)
    {
        Rigidbody2D doorBody = slot?.doorJoint != null ? slot.doorJoint.connectedBody : null;
        if (doorBody == null)
            return;

        Collider2D[] colliders = doorBody.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider != null && !doorColliders.Contains(collider))
                doorColliders.Add(collider);
        }
    }

    private void SetBalloonDoorCollision(Collider2D balloonCollider, bool ignored)
    {
        if (balloonCollider == null)
            return;

        for (int i = 0; i < doorColliders.Count; i++)
        {
            Collider2D doorCollider = doorColliders[i];
            if (doorCollider == null || doorCollider == balloonCollider)
                continue;

            Physics2D.IgnoreCollision(balloonCollider, doorCollider, ignored);
        }
    }

    private void ReapplyDoorCollisionIgnores()
    {
        doorColliders.Clear();
        for (int i = 0; i < balloons.Length; i++)
            RegisterDoorColliders(balloons[i]);

        for (int i = 0; i < balloons.Length; i++)
            SetBalloonDoorCollision(balloons[i]?.collider, true);

        for (int i = 0; i < detachedBalloons.Count; i++)
            SetBalloonDoorCollision(detachedBalloons[i]?.Collider, true);
    }

    private static void AlignJointWithVisibleLine(BalloonSlot slot)
    {
        Transform startPoint = slot?.line != null ? slot.line.GetStartPoint() : null;
        Transform endPoint = slot?.line != null ? slot.line.GetEndPoint() : null;
        DistanceJoint2D joint = slot?.doorJoint;
        Rigidbody2D connectedBody = joint != null ? joint.connectedBody : null;
        if (startPoint == null || endPoint == null || joint == null || connectedBody == null)
            return;

        joint.anchor = slot.body.transform.InverseTransformPoint(startPoint.position);
        joint.connectedAnchor = connectedBody.transform.InverseTransformPoint(endPoint.position);
        joint.distance = Vector2.Distance(startPoint.position, endPoint.position);
    }

    #endregion

    #region Input

    private void ProcessPointerInput()
    {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        ProcessTouchInput();
#else
        ProcessMouseInput();
#endif
    }

#if UNITY_ANDROID || UNITY_IOS
    private void ProcessTouchInput()
    {
        if (pointerTracking)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.fingerId != activeFingerId)
                    continue;

                switch (touch.phase)
                {
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary:
                        ContinuePointer(touch.position);
                        break;
                    case TouchPhase.Ended:
                        EndPointer(touch.position, false);
                        break;
                    case TouchPhase.Canceled:
                        EndPointer(touch.position, true);
                        break;
                }

                return;
            }

            CancelPointer(true);
            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Began && TryBeginPointer(touch.position, touch.fingerId))
                return;
        }
    }
#endif

    private void ProcessMouseInput()
    {
        const int MousePointerId = -1;
        if (!pointerTracking)
        {
            if (Input.GetMouseButtonDown(0))
                TryBeginPointer(Input.mousePosition, MousePointerId);
            return;
        }

        if (activeFingerId != MousePointerId)
            return;

        if (Input.GetMouseButton(0))
        {
            ContinuePointer(Input.mousePosition);
            return;
        }

        if (Input.GetMouseButtonUp(0))
            EndPointer(Input.mousePosition, false);
        else
            CancelPointer(true);
    }

    private bool TryBeginPointer(Vector2 screenPosition, int pointerId)
    {
        Vector2 worldPosition = ScreenToWorld(screenPosition);
        if (carriedBalloon != null && IsBalloonHit(carriedBalloon, worldPosition))
        {
            pointerDetachedBalloon = carriedBalloon;
            pointerStartedOnCarriedBalloon = true;
        }
        else
        {
            pointerDetachedBalloon = FindHitDetachedBalloon(worldPosition);
            if (pointerDetachedBalloon == null)
            {
                pointerSlot = FindHitSlot(worldPosition, out int hitIndex);
                pointerSlotIndex = hitIndex;
                if (pointerSlot == null)
                    return false;
            }
        }

        activeFingerId = pointerId;
        pointerStartScreenPosition = screenPosition;
        pointerTracking = true;
        pointerDragging = false;

        Rigidbody2D hitBody = pointerDetachedBalloon?.Body ?? pointerSlot?.body;
        pointerOffsetWorld = hitBody != null ? hitBody.position - worldPosition : Vector2.zero;
        return true;
    }

    private void ContinuePointer(Vector2 screenPosition)
    {
        if (!pointerTracking)
            return;

        float pointerDistance = Vector2.Distance(screenPosition, pointerStartScreenPosition);
        if (pointerStartedOnCarriedBalloon)
        {
            pointerDragging |= pointerDistance >= dragThresholdPixels;
            return;
        }

        if (!pointerDragging &&
            pointerDistance >= dragThresholdPixels)
        {
            BeginDrag();
        }

        Rigidbody2D draggedBody = pointerDetachedBalloon?.Body;
        if (!pointerDragging || draggedBody == null)
            return;

        Vector2 target = ScreenToWorld(screenPosition) + pointerOffsetWorld;
        draggedBody.position = target;
        draggedBody.linearVelocity = Vector2.zero;
        draggedBody.angularVelocity = 0f;
        Physics2D.SyncTransforms();
        SyncVisibleLine(pointerDetachedBalloon.Line, pointerDetachedBalloon.LineRenderer);
        TryRestoreSourceCollision(pointerDetachedBalloon);
    }

    private void EndPointer(Vector2 screenPosition, bool canceled)
    {
        if (!pointerTracking)
            return;

        if (canceled)
        {
            CancelPointer(true);
            return;
        }

        ContinuePointer(screenPosition);
        BalloonSlot releasedSlot = pointerSlot;
        int releasedIndex = pointerSlotIndex;
        DetachedBalloon releasedBalloon = pointerDetachedBalloon;
        bool wasDragging = pointerDragging;
        bool startedOnCarried = pointerStartedOnCarriedBalloon;
        ClearPointerState();

        if (startedOnCarried)
        {
            if (!wasDragging && releasedBalloon == carriedBalloon)
                PopCarriedBalloon();
            return;
        }

        if (!wasDragging)
        {
            if (releasedBalloon != null)
                PopDetachedBalloon(releasedBalloon);
            else if (releasedSlot != null)
                PlayExplosionAndRegrow(releasedSlot, releasedIndex);
            return;
        }

        if (CanAttachToActor(releasedBalloon))
            AttachToActor(releasedBalloon);
        else
            ReleaseDetachedBalloon(releasedBalloon);
    }

    private void BeginDrag()
    {
        pointerDragging = true;
        if (pointerDetachedBalloon == null && pointerSlot != null)
        {
            BalloonSlot sourceSlot = pointerSlot;
            int sourceIndex = pointerSlotIndex;
            pointerDetachedBalloon = TakeBalloonFromSlot(sourceSlot, sourceIndex);
            if (pointerDetachedBalloon == null)
            {
                pointerDragging = false;
                return;
            }

            pointerSlot = null;
            StartSlotRegrow(sourceSlot, sourceIndex, false);
        }

        if (pointerDetachedBalloon?.Body == null)
        {
            pointerDragging = false;
            return;
        }

        ConfigureDetachedForDrag(pointerDetachedBalloon);
    }

    private DetachedBalloon FindHitDetachedBalloon(Vector2 worldPosition)
    {
        for (int i = detachedBalloons.Count - 1; i >= 0; i--)
        {
            DetachedBalloon balloon = detachedBalloons[i];
            if (balloon != carriedBalloon && IsBalloonHit(balloon, worldPosition))
                return balloon;
        }

        return null;
    }

    private BalloonSlot FindHitSlot(Vector2 worldPosition, out int slotIndex)
    {
        slotIndex = -1;
        for (int i = balloons.Length - 1; i >= 0; i--)
        {
            BalloonSlot slot = balloons[i];
            if (!IsValidSlot(slot, i) || slotRegrowing[i] ||
                !IsBalloonPickHit(slot.collider, slot.linePickCollider, slot.line, worldPosition))
            {
                continue;
            }

            slotIndex = i;
            return slot;
        }

        return null;
    }

    private bool IsBalloonHit(DetachedBalloon balloon, Vector2 worldPosition)
    {
        return balloon is { IsDestroyed: false, Collider: not null } &&
               IsBalloonPickHit(balloon.Collider, balloon.LinePickCollider, balloon.Line, worldPosition);
    }

    // 本体外扩与独立 Line 拾取分开；物理碰撞体和绳尾吸附仍走原流程。
    private bool IsBalloonPickHit(Collider2D balloonCollider, PolygonCollider2D linePickCollider, Level5TrackLine line, Vector2 worldPosition)
    {
        if (balloonCollider == null || !balloonCollider.enabled ||
            !balloonCollider.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (balloonCollider.OverlapPoint(worldPosition))
            return true;
        float paddingSquared = balloonPickPadding * balloonPickPadding;
        if (balloonPickPadding > 0f &&
            (balloonCollider.ClosestPoint(worldPosition) - worldPosition).sqrMagnitude <= paddingSquared)
            return true;

        return IsLinePickHit(linePickCollider, line, worldPosition);
    }

    // 只在按下选球时同步当前绳段；复用四点数组，不新增逐帧物理形状重建。
    private bool IsLinePickHit(PolygonCollider2D pickCollider, Level5TrackLine line, Vector2 worldPosition)
    {
        if (pickCollider == null || !pickCollider.enabled || !pickCollider.gameObject.activeInHierarchy ||
            line == null || linePickWidth <= 0f)
        {
            return false;
        }

        Transform startPoint = line.GetStartPoint();
        Transform endPoint = line.GetEndPoint();
        if (startPoint == null || endPoint == null)
            return false;

        Vector2 start = startPoint.position;
        Vector2 end = endPoint.position;
        Vector2 direction = end - start;
        float length = direction.magnitude;
        if (length <= 0.0001f)
            return false;

        direction /= length;
        float halfWidth = linePickWidth * 0.5f;
        Vector2 normal = new Vector2(-direction.y, direction.x) * halfWidth;
        start -= direction * halfWidth;
        end += direction * halfWidth;
        Transform pickTransform = pickCollider.transform;
        linePickPoints[0] = pickTransform.InverseTransformPoint(start + normal);
        linePickPoints[1] = pickTransform.InverseTransformPoint(end + normal);
        linePickPoints[2] = pickTransform.InverseTransformPoint(end - normal);
        linePickPoints[3] = pickTransform.InverseTransformPoint(start - normal);
        pickCollider.SetPath(0, linePickPoints);
        Physics2D.SyncTransforms();
        return pickCollider.OverlapPoint(worldPosition);
    }

    private void CancelPointer(bool releaseDraggedBalloon)
    {
        DetachedBalloon canceledBalloon = pointerDetachedBalloon;
        bool wasDragging = pointerDragging;
        bool startedOnCarried = pointerStartedOnCarriedBalloon;
        ClearPointerState();
        if (releaseDraggedBalloon && wasDragging && !startedOnCarried)
            ReleaseDetachedBalloon(canceledBalloon);
    }

    private void ClearPointerState()
    {
        pointerSlot = null;
        pointerDetachedBalloon = null;
        pointerSlotIndex = -1;
        activeFingerId = int.MinValue;
        pointerTracking = false;
        pointerDragging = false;
        pointerStartedOnCarriedBalloon = false;
    }

    private Vector2 ScreenToWorld(Vector2 screenPosition)
    {
        Vector3 world = levelCamera.ScreenToWorldPoint(screenPosition);
        return new Vector2(world.x, world.y);
    }

    #endregion

    #region Explosion and Regrow

    private void PlayExplosionAndRegrow(BalloonSlot slot, int index)
    {
        StartSlotRegrow(slot, index, true);
    }

    private void StartSlotRegrow(BalloonSlot slot, int index, bool playExplosion)
    {
        if (!IsValidSlot(slot, index) || slotRegrowing[index])
            return;

        slotRegrowing[index] = true;
        if (playExplosion)
        {
            PlayExplosionVisual(slot.body.position);
            AudioManager.PlaySound("ballonExplosion");
        }
        StartCoroutine(RegrowVisualRoutine(slot, index));
    }

    private IEnumerator RegrowVisualRoutine(BalloonSlot slot, int index)
    {
        SpriteRenderer source = slot.spriteRenderer;
        if (source == null)
        {
            slotRegrowing[index] = false;
            yield break;
        }

        bool sourceWasEnabled = source.enabled;
        LineRenderer lineRenderer = GetLineRenderer(index);
        bool lineWasEnabled = lineRenderer != null && lineRenderer.enabled;
        source.enabled = false;
        if (lineRenderer != null)
            lineRenderer.enabled = false;

        GameObject visual = CreateRegrowVisual(source, index);
        if (visual == null)
        {
            source.enabled = sourceWasEnabled;
            if (lineRenderer != null)
                lineRenderer.enabled = lineWasEnabled;
            slotRegrowing[index] = false;
            yield break;
        }

        Transform visualTransform = visual.transform;
        Vector3 targetWorldScale = source.transform.lossyScale;
        Vector3 startWorldScale = targetWorldScale * 0.08f;
        Vector3 targetPosition = slot.body.transform.position;
        Vector3 startPosition = ResolveRegrowStart(index, targetPosition);
        visualTransform.SetPositionAndRotation(startPosition, slot.body.transform.rotation);
        visualTransform.localScale = startWorldScale;

        Transform regrowRopePoint = CreateRegrowRopePoint(visualTransform, source, index);
        if (slot.line != null && regrowRopePoint != null)
        {
            slot.line.SetStartPoint(regrowRopePoint);
            slot.line.SetEndPoint(initialStates[index].LineEndPoint);
            SyncVisibleLine(slot.line, lineRenderer);
            if (lineRenderer != null)
                lineRenderer.enabled = lineWasEnabled;
        }

        float elapsed = 0f;
        while (elapsed < regrowDuration && slot?.body != null)
        {
            while (isPaused)
                yield return null;

            float normalized = Mathf.Clamp01(elapsed / regrowDuration);
            float moveT = Mathf.SmoothStep(0f, 1f, normalized);
            float scaleT = Mathf.SmoothStep(0f, 1f, normalized);
            targetPosition = slot.body.transform.position;
            startPosition = ResolveRegrowStart(index, targetPosition);

            visualTransform.SetPositionAndRotation(
                Vector3.LerpUnclamped(startPosition, targetPosition, moveT),
                slot.body.transform.rotation);
            visualTransform.localScale = Vector3.LerpUnclamped(startWorldScale, targetWorldScale, scaleT);
            SyncVisibleLine(slot.line, lineRenderer);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (slot?.body != null)
        {
            visualTransform.SetPositionAndRotation(slot.body.transform.position, slot.body.transform.rotation);
            visualTransform.localScale = targetWorldScale;
            SyncVisibleLine(slot.line, lineRenderer);
        }

        RestoreLineEndpoints(slot, index, lineWasEnabled);
        source.enabled = sourceWasEnabled;
        DestroyRegrowVisual(index);
        slotRegrowing[index] = false;
    }

    private GameObject CreateRegrowVisual(SpriteRenderer source, int index)
    {
        if (source == null || source.sprite == null)
            return null;

        GameObject visual = new($"Level097_Regrow_{index}");
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = source.sprite;
        renderer.color = source.color;
        renderer.sharedMaterial = source.sharedMaterial;
        renderer.flipX = source.flipX;
        renderer.flipY = source.flipY;
        renderer.drawMode = source.drawMode;
        renderer.size = source.size;
        renderer.tileMode = source.tileMode;
        renderer.maskInteraction = source.maskInteraction;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = RegrowBehindDoorSortingOrder;
        regrowVisuals[index] = visual;
        return visual;
    }

    private Transform CreateRegrowRopePoint(Transform visual, SpriteRenderer source, int index)
    {
        if (visual == null || source == null || index < 0 || index >= initialStates.Length)
            return null;

        Transform originalStart = initialStates[index].LineStartPoint;
        if (originalStart == null)
            return null;

        Transform ropePoint = new GameObject("RopePoint").transform;
        ropePoint.SetParent(visual, false);
        ropePoint.localPosition = source.transform.InverseTransformPoint(originalStart.position);
        return ropePoint;
    }

    private Vector3 ResolveRegrowStart(int index, Vector3 fallback)
    {
        Transform doorPoint = index >= 0 && index < initialStates.Length
            ? initialStates[index].LineEndPoint
            : null;
        Vector3 start = doorPoint != null ? doorPoint.position : fallback;
        start.y -= regrowHiddenOffsetY;
        return start;
    }

    private void PlayExplosionVisual(Vector2 position)
    {
        if (explosionTemplate == null)
            return;

        GameObject instance = Instantiate(explosionTemplate, position, Quaternion.identity, transform);
        instance.SetActive(true);
        ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
            particles[i].Play(true);

        Destroy(instance, 2f);
    }

    private void DestroyRegrowVisual(int index)
    {
        if (index < 0 || index >= regrowVisuals.Length || regrowVisuals[index] == null)
            return;

        Destroy(regrowVisuals[index]);
        regrowVisuals[index] = null;
    }

    private void RestoreLineEndpoints(BalloonSlot slot, int index, bool lineEnabled)
    {
        if (!IsValidSlot(slot, index) || slot.line == null)
            return;

        BalloonInitialState state = initialStates[index];
        slot.line.SetStartPoint(state.LineStartPoint);
        slot.line.SetEndPoint(state.LineEndPoint);
        LineRenderer lineRenderer = GetLineRenderer(index);
        SyncVisibleLine(slot.line, lineRenderer);
        if (lineRenderer != null)
            lineRenderer.enabled = lineEnabled;
    }

    private static void SyncVisibleLine(Level5TrackLine line, LineRenderer lineRenderer)
    {
        Transform startPoint = line != null ? line.GetStartPoint() : null;
        Transform endPoint = line != null ? line.GetEndPoint() : null;
        if (lineRenderer == null || startPoint == null || endPoint == null)
            return;

        if (lineRenderer.positionCount != 2)
            lineRenderer.positionCount = 2;
        Transform lineTransform = lineRenderer.transform;
        lineRenderer.SetPosition(0, lineTransform.InverseTransformPoint(startPoint.position));
        lineRenderer.SetPosition(1, lineTransform.InverseTransformPoint(endPoint.position));
    }

    #endregion

    #region Drag Completion

    private DetachedBalloon TakeBalloonFromSlot(BalloonSlot slot, int index)
    {
        if (!IsValidSlot(slot, index))
            return null;

        GameObject detachedRoot = new($"Level097_DetachedBalloon_{index}");
        Transform detachedTransform = detachedRoot.transform;
        detachedTransform.SetParent(slot.root.parent, false);
        detachedTransform.localPosition = slot.root.localPosition;
        detachedTransform.localRotation = slot.root.localRotation;
        detachedTransform.localScale = slot.root.localScale;

        GameObject bodyObject = Instantiate(slot.body.gameObject, detachedTransform, false);
        bodyObject.name = "Nail";
        Rigidbody2D body = bodyObject.GetComponent<Rigidbody2D>();
        Collider2D balloonCollider = bodyObject.GetComponent<Collider2D>();
        SpriteRenderer spriteRenderer = bodyObject.GetComponent<SpriteRenderer>();
        DistanceJoint2D doorJoint = bodyObject.GetComponent<DistanceJoint2D>();

        GameObject lineObject = Instantiate(slot.line.gameObject, detachedTransform, false);
        lineObject.name = "Line";
        Level5TrackLine line = lineObject.GetComponent<Level5TrackLine>();
        LineRenderer lineRenderer = lineObject.GetComponent<LineRenderer>();
        PolygonCollider2D linePickCollider = lineObject.GetComponent<PolygonCollider2D>();
        if (body == null || balloonCollider == null || spriteRenderer == null ||
            line == null || lineRenderer == null)
        {
            Destroy(detachedRoot);
            Debug.LogWarning($"[Level97Ctrl] Failed to create detached balloon for slot {index}.", this);
            return null;
        }

        if (doorJoint != null)
            doorJoint.enabled = false;

        Transform sourceLineStart = slot.line.GetStartPoint();
        Transform ropeStart = new GameObject("LinePoint").transform;
        ropeStart.SetParent(body.transform, false);
        ropeStart.localPosition = sourceLineStart != null
            ? slot.body.transform.InverseTransformPoint(sourceLineStart.position)
            : Vector3.down * 0.65f;

        Transform looseRopeEnd = new GameObject("LooseRopeEnd").transform;
        looseRopeEnd.SetParent(body.transform, true);
        looseRopeEnd.position = ropeStart.position + Vector3.down * looseRopeLength;
        line.SetStartPoint(ropeStart);
        line.SetEndPoint(looseRopeEnd);
        lineRenderer.enabled = initialStates[index].LineRendererEnabled;
        SyncVisibleLine(line, lineRenderer);

        DetachedBalloon detached = new()
        {
            Root = detachedRoot,
            Body = body,
            Collider = balloonCollider,
            SpriteRenderer = spriteRenderer,
            DoorJoint = doorJoint,
            Line = line,
            LinePickCollider = linePickCollider,
            LineRenderer = lineRenderer,
            LooseRopeEnd = looseRopeEnd,
            IgnoredSourceCollider = slot.collider,
            SourceIndex = index,
        };

        if (slot.collider != null)
            Physics2D.IgnoreCollision(detached.Collider, slot.collider, true);
        SetBalloonDoorCollision(detached.Collider, true);

        detachedBalloons.Add(detached);
        return detached;
    }

    private static bool IsValidDetachedBalloon(DetachedBalloon balloon)
    {
        return balloon != null && !balloon.IsDestroyed && balloon.Root != null &&
               balloon.Body != null && balloon.Collider != null;
    }

    private void ConfigureDetachedForDrag(DetachedBalloon balloon)
    {
        if (!IsValidDetachedBalloon(balloon))
            return;

        balloon.Body.bodyType = RigidbodyType2D.Kinematic;
        balloon.Body.simulated = true;
        balloon.Body.gravityScale = 0f;
        balloon.Body.constraints = RigidbodyConstraints2D.FreezeRotation;
        balloon.Body.linearVelocity = Vector2.zero;
        balloon.Body.angularVelocity = 0f;
        if (balloon.DoorJoint != null)
            balloon.DoorJoint.enabled = false;
        SetLooseRopeEnd(balloon);
    }

    private void ReleaseDetachedBalloon(DetachedBalloon balloon)
    {
        if (!IsValidDetachedBalloon(balloon) || balloon == carriedBalloon)
            return;

        balloon.Body.bodyType = RigidbodyType2D.Dynamic;
        balloon.Body.simulated = true;
        balloon.Body.gravityScale = -0.6f;
        balloon.Body.constraints = RigidbodyConstraints2D.FreezeRotation;
        balloon.Body.angularVelocity = 0f;
        if (balloon.DoorJoint != null)
            balloon.DoorJoint.enabled = false;
        if (balloon.Collider != null)
            balloon.Collider.enabled = true;
        SetDetachedCeilingIgnored(balloon, true);
        SetLooseRopeEnd(balloon);
        balloon.Body.WakeUp();
        TryRestoreSourceCollision(balloon);
    }

    private bool CanAttachToActor(DetachedBalloon balloon)
    {
        if (!IsValidDetachedBalloon(balloon) || balloon.LooseRopeEnd == null ||
            (carriedBalloon != null && carriedBalloon != balloon) ||
            actorMainCollider == null || !actorMainCollider.enabled || actorMainCollider.isTrigger ||
            !actorMainCollider.gameObject.activeInHierarchy)
        {
            return false;
        }

        Physics2D.SyncTransforms();
        Vector2 ropeEndPosition = balloon.LooseRopeEnd.position;
        if (actorMainCollider.OverlapPoint(ropeEndPosition))
            return true;

        Vector2 closestPoint = actorMainCollider.ClosestPoint(ropeEndPosition);
        return Vector2.Distance(closestPoint, ropeEndPosition) <= attachTolerance;
    }

    private void AttachToActor(DetachedBalloon balloon)
    {
        if (!IsValidDetachedBalloon(balloon) || levelActor?.RB == null)
        {
            ReleaseDetachedBalloon(balloon);
            return;
        }

        carriedBalloon = balloon;
        actorInitialGravityScale = levelActor.RB.gravityScale;

        ConfigureDetachedForDrag(balloon);
        SetDetachedCeilingIgnored(balloon, false);
        if (balloon.Line != null)
            balloon.Line.SetEndPoint(actorRopeAttachPoint != null ? actorRopeAttachPoint : levelActor.transform);

        levelActor.RB.gravityScale = 0f;
        levelActor.SetBalloonFlying(true);
        SetJumpButtonEnabled(false);
        TryRestoreSourceCollision(balloon);
        ApplyCarryingMotion();
    }

    private void ApplyCarryingMotion()
    {
        Rigidbody2D actorBody = levelActor?.RB;
        Rigidbody2D balloonBody = carriedBalloon?.Body;
        if (actorBody == null || balloonBody == null)
            return;

        balloonBody.position = actorBody.position + carriedBalloonOffset;
        Physics2D.SyncTransforms();
        ResolveCarryingCeilingOverlap(actorBody, balloonBody);

        float fixedDeltaTime = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
        float desiredRiseDistance = carryingRiseSpeed * fixedDeltaTime;
        float allowedRiseDistance = GetAllowedCarryingRiseDistance(desiredRiseDistance);
        float allowedRiseSpeed = allowedRiseDistance / fixedDeltaTime;

        Vector2 actorVelocity = actorBody.linearVelocity;
        actorVelocity.y = allowedRiseSpeed;
        actorBody.linearVelocity = actorVelocity;

        balloonBody.linearVelocity = new Vector2(actorVelocity.x, allowedRiseSpeed);
        balloonBody.angularVelocity = 0f;
    }

    private void ResolveCarryingCeilingOverlap(Rigidbody2D actorBody, Rigidbody2D balloonBody)
    {
        Collider2D balloonCollider = carriedBalloon?.Collider;
        if (actorBody == null || balloonBody == null || balloonCollider == null ||
            !balloonCollider.enabled || carryingCeilingCollider == null ||
            !carryingCeilingCollider.enabled || !carryingCeilingCollider.gameObject.activeInHierarchy)
        {
            return;
        }

        ColliderDistance2D ceilingDistance = balloonCollider.Distance(carryingCeilingCollider);
        if (!ceilingDistance.isValid || !ceilingDistance.isOverlapped || ceilingDistance.normal.y >= -0.01f)
            return;

        Vector2 correction = ceilingDistance.normal *
                             (Mathf.Abs(ceilingDistance.distance) + CarryingCeilingSkin);
        actorBody.position += correction;
        balloonBody.position += correction;
        Physics2D.SyncTransforms();
    }

    private float GetAllowedCarryingRiseDistance(float desiredDistance)
    {
        Collider2D balloonCollider = carriedBalloon?.Collider;
        if (desiredDistance <= 0f || balloonCollider == null || !balloonCollider.enabled ||
            carryingCeilingCollider == null || !carryingCeilingCollider.enabled ||
            !carryingCeilingCollider.gameObject.activeInHierarchy)
        {
            return Mathf.Max(0f, desiredDistance);
        }

        ColliderDistance2D ceilingDistance = balloonCollider.Distance(carryingCeilingCollider);
        if (ceilingDistance.isValid && ceilingDistance.normal.y < -0.01f &&
            ceilingDistance.distance <= CarryingCeilingSkin)
        {
            return 0f;
        }

        float castDistance = desiredDistance + CarryingCeilingSkin;
        int hitCount = balloonCollider.Cast(Vector2.up, carryingCeilingHits, castDistance, true);
        float allowedDistance = desiredDistance;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = carryingCeilingHits[i];
            if (hit.collider != carryingCeilingCollider)
                continue;

            allowedDistance = Mathf.Min(
                allowedDistance,
                Mathf.Max(0f, hit.distance - CarryingCeilingSkin));
        }

        return allowedDistance;
    }

    private void PopDetachedBalloon(DetachedBalloon balloon)
    {
        if (!IsValidDetachedBalloon(balloon))
            return;

        if (balloon == carriedBalloon)
        {
            PopCarriedBalloon();
            return;
        }

        PlayExplosionVisual(balloon.Body.position);
        AudioManager.PlaySound("ballonExplosion");
        DestroyDetachedBalloon(balloon);
    }

    private void PopCarriedBalloon()
    {
        DetachedBalloon popped = carriedBalloon;
        if (!IsValidDetachedBalloon(popped))
            return;

        carriedBalloon = null;
        levelActor?.SetBalloonFlying(false);
        if (levelActor?.RB != null)
        {
            Vector2 actorVelocity = levelActor.RB.linearVelocity;
            actorVelocity.y = 0f;
            levelActor.RB.linearVelocity = actorVelocity;
            levelActor.RB.gravityScale = actorInitialGravityScale;
        }

        RestoreJumpButton();
        PlayExplosionVisual(popped.Body.position);
        AudioManager.PlaySound("ballonExplosion");
        DestroyDetachedBalloon(popped);
    }

    private void ReleaseCarriedBalloon(bool restoreRuntimeState)
    {
        if (carriedBalloon == null)
        {
            RestoreJumpButton();
            return;
        }

        DetachedBalloon released = carriedBalloon;
        carriedBalloon = null;

        levelActor?.SetBalloonFlying(false);
        if (levelActor?.RB != null)
        {
            Vector2 actorVelocity = levelActor.RB.linearVelocity;
            actorVelocity.y = 0f;
            levelActor.RB.linearVelocity = actorVelocity;
            if (restoreRuntimeState)
                levelActor.RB.gravityScale = actorInitialGravityScale;
        }

        if (restoreRuntimeState)
            ReleaseDetachedBalloon(released);
        RestoreJumpButton();
    }

    private void RestoreSlotPhysics(BalloonSlot slot, int index)
    {
        if (!IsValidSlot(slot, index))
            return;

        BalloonInitialState state = initialStates[index];
        Transform bodyTransform = slot.body.transform;
        bodyTransform.localPosition = state.BodyLocalPosition;
        bodyTransform.localRotation = state.BodyLocalRotation;
        bodyTransform.localScale = state.BodyLocalScale;
        slot.body.bodyType = state.BodyType;
        slot.body.constraints = state.Constraints;
        slot.body.gravityScale = state.GravityScale;
        slot.body.linearVelocity = Vector2.zero;
        slot.body.angularVelocity = 0f;
        slot.body.simulated = state.Simulated;
        if (slot.collider != null)
        {
            slot.collider.enabled = state.ColliderEnabled;
            SetBalloonDoorCollision(slot.collider, true);
        }
        if (slot.doorJoint != null)
            slot.doorJoint.enabled = state.JointEnabled;
        if (slot.line != null && !slotRegrowing[index])
        {
            slot.line.SetStartPoint(state.LineStartPoint);
            slot.line.SetEndPoint(state.LineEndPoint);
        }

        LineRenderer lineRenderer = GetLineRenderer(index);
        if (!slotRegrowing[index])
            SyncVisibleLine(slot.line, lineRenderer);
        if (lineRenderer != null && !slotRegrowing[index])
            lineRenderer.enabled = state.LineRendererEnabled;
        if (slot.spriteRenderer != null && !slotRegrowing[index])
            slot.spriteRenderer.enabled = state.RendererEnabled;

        Physics2D.SyncTransforms();
        slot.body.WakeUp();
    }

    #endregion

    #region Game State and HUD

    private void BindLevelEvents()
    {
        EventsUtils.ResetEvents(ref subscriber);
        subscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
        subscriber.Subscribe<GamePauseEvent>(OnGamePauseLocal);
        subscriber.Subscribe<GameResumeEvent>(OnGameResumeLocal);
        subscriber.Subscribe<GameResetEvent>(OnGameResetLocal);
        subscriber.Subscribe<GameReplayEvent>(OnGameReplayLocal);
    }

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        StopGameplayStateReconcile();
        isPaused = evt.NewState != GamePlayStateName.PLAYING;
        if (!isPaused)
            return;

        SuspendGameplay();
    }

    private void OnGamePauseLocal(GamePauseEvent evt)
    {
        SuspendGameplay();
    }

    private void OnGameResumeLocal(GameResumeEvent evt)
    {
        StopGameplayStateReconcile();
        isPaused = true;
        reconcileGameplayStateCoroutine = StartCoroutine(ReconcileGameplayStateAfterResume());
    }

    private void OnGameResetLocal(GameResetEvent evt)
    {
        SuspendGameplay();
    }

    private void OnGameReplayLocal(GameReplayEvent evt)
    {
        SuspendGameplay();
    }

    private void SuspendGameplay()
    {
        StopGameplayStateReconcile();
        isPaused = true;
        CancelPointer(true);
        StopCarryingVelocity();
    }

    private IEnumerator ReconcileGameplayStateAfterResume()
    {
        // GameResumeEvent 在 PAUSE 节点退出时派发；下一帧再确认实际进入的目标状态。
        yield return null;
        reconcileGameplayStateCoroutine = null;
        if (!isActiveAndEnabled || PlayCtrl.Instance == null ||
            !PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING))
        {
            yield break;
        }

        isPaused = false;
    }

    private void StopGameplayStateReconcile()
    {
        if (reconcileGameplayStateCoroutine == null)
            return;

        StopCoroutine(reconcileGameplayStateCoroutine);
        reconcileGameplayStateCoroutine = null;
    }

    private bool IsLevelFinished()
    {
        return baseLevel == null || baseLevel.IsSuccess() || baseLevel.IsFail() ||
               levelActor == null || levelActor.IsDied;
    }

    private void HandleFinishedState()
    {
        if (finishHandled)
            return;

        finishHandled = true;
        CancelPointer(true);
        if (baseLevel == null || levelActor == null || baseLevel.IsFail() || levelActor.IsDied)
            ReleaseCarriedBalloon(true);
        else
            StopCarryingVelocity();
    }

    private void StopCarryingVelocity()
    {
        if (carriedBalloon == null)
            return;

        if (levelActor?.RB != null)
            levelActor.RB.linearVelocity = Vector2.zero;
        if (carriedBalloon.Body != null)
            carriedBalloon.Body.linearVelocity = Vector2.zero;
    }

    private IEnumerator FindJumpButtonWhenReady()
    {
        yield return null;
        while (isActiveAndEnabled &&
               (PlayCtrl.Instance == null || PlayCtrl.Instance.CurrentGamePlayPanel == null))
        {
            yield return null;
        }

        findJumpButtonCoroutine = null;
        if (!isActiveAndEnabled || PlayCtrl.Instance?.CurrentGamePlayPanel == null)
            yield break;

        Transform buttonTransform = PlayCtrl.Instance.CurrentGamePlayPanel.transform.Find("Root/jump_btn");
        jumpButton = buttonTransform != null ? buttonTransform.GetComponent<CustomButton>() : null;
        if (jumpButton == null)
            yield break;

        jumpButtonInitialEnabled = jumpButton.enabled;
        jumpButtonInitialInteractable = jumpButton.Interactable;
        hasJumpButtonState = true;
        if (carriedBalloon != null)
            SetJumpButtonEnabled(false);
    }

    private void SetJumpButtonEnabled(bool enabled)
    {
        if (!hasJumpButtonState || jumpButton == null)
            return;

        jumpButton.Interactable = enabled && jumpButtonInitialInteractable;
        jumpButton.enabled = enabled && jumpButtonInitialEnabled;
    }

    private void RestoreJumpButton()
    {
        if (!hasJumpButtonState || jumpButton == null)
            return;

        jumpButton.enabled = jumpButtonInitialEnabled;
        jumpButton.Interactable = jumpButtonInitialInteractable;
    }

    #endregion

    #region Helpers

    private bool IsSlotBound(BalloonSlot slot)
    {
        return slot?.root != null && slot.body != null && slot.collider != null &&
               slot.doorJoint != null && slot.line != null && slot.spriteRenderer != null;
    }

    private bool IsValidSlot(BalloonSlot slot, int index)
    {
        return IsSlotBound(slot) && index >= 0 && index < initialStates.Length;
    }

    private static void SetLooseRopeEnd(DetachedBalloon balloon)
    {
        if (!IsValidDetachedBalloon(balloon) || balloon.Line == null || balloon.LooseRopeEnd == null)
            return;

        balloon.Line.SetEndPoint(balloon.LooseRopeEnd);
        SyncVisibleLine(balloon.Line, balloon.LineRenderer);
        if (balloon.LineRenderer != null)
            balloon.LineRenderer.enabled = true;
    }

    private void TryRestoreSourceCollision(DetachedBalloon balloon)
    {
        if (!IsValidDetachedBalloon(balloon) || balloon.IgnoredSourceCollider == null)
            return;

        Collider2D sourceCollider = balloon.IgnoredSourceCollider;
        if (sourceCollider == null || !sourceCollider.enabled || !sourceCollider.gameObject.activeInHierarchy)
        {
            RestoreSourceCollision(balloon);
            return;
        }

        Physics2D.SyncTransforms();
        ColliderDistance2D distance = balloon.Collider.Distance(sourceCollider);
        if (!distance.isValid || distance.isOverlapped)
            return;

        RestoreSourceCollision(balloon);
    }

    private void RestoreSeparatedSourceCollisions()
    {
        for (int i = 0; i < detachedBalloons.Count; i++)
            TryRestoreSourceCollision(detachedBalloons[i]);
    }

    private static void RestoreSourceCollision(DetachedBalloon balloon)
    {
        if (balloon?.Collider != null && balloon.IgnoredSourceCollider != null)
            Physics2D.IgnoreCollision(balloon.Collider, balloon.IgnoredSourceCollider, false);
        if (balloon != null)
            balloon.IgnoredSourceCollider = null;
    }

    private void SetDetachedCeilingIgnored(DetachedBalloon balloon, bool ignored)
    {
        if (!IsValidDetachedBalloon(balloon) || carryingCeilingCollider == null)
            return;

        Physics2D.IgnoreCollision(balloon.Collider, carryingCeilingCollider, ignored);
        balloon.IgnoresCarryingCeiling = ignored;
    }

    private void RestoreDetachedCeilingCollision(DetachedBalloon balloon)
    {
        if (balloon == null || !balloon.IgnoresCarryingCeiling)
            return;

        if (balloon.Collider != null && carryingCeilingCollider != null)
            Physics2D.IgnoreCollision(balloon.Collider, carryingCeilingCollider, false);
        balloon.IgnoresCarryingCeiling = false;
    }

    private void DestroyDetachedBalloon(DetachedBalloon balloon)
    {
        if (!IsValidDetachedBalloon(balloon))
            return;

        RestoreDetachedCeilingCollision(balloon);
        SetBalloonDoorCollision(balloon.Collider, false);
        balloon.IsDestroyed = true;
        RestoreSourceCollision(balloon);
        if (balloon.Collider != null)
            balloon.Collider.enabled = false;
        if (balloon.SpriteRenderer != null)
            balloon.SpriteRenderer.enabled = false;
        if (balloon.LineRenderer != null)
            balloon.LineRenderer.enabled = false;
        detachedBalloons.Remove(balloon);
        if (pointerDetachedBalloon == balloon)
            pointerDetachedBalloon = null;
        Destroy(balloon.Root);
    }

    private void DestroyAllDetachedBalloons()
    {
        for (int i = detachedBalloons.Count - 1; i >= 0; i--)
        {
            DetachedBalloon balloon = detachedBalloons[i];
            if (balloon == null)
                continue;

            RestoreDetachedCeilingCollision(balloon);
            SetBalloonDoorCollision(balloon.Collider, false);
            RestoreSourceCollision(balloon);
            balloon.IsDestroyed = true;
            if (balloon.Root != null)
                Destroy(balloon.Root);
        }

        detachedBalloons.Clear();
        pointerDetachedBalloon = null;
        carriedBalloon = null;
    }

    private LineRenderer GetLineRenderer(int index)
    {
        return index >= 0 && index < lineRenderers.Length ? lineRenderers[index] : null;
    }

    private void RestoreRegrowVisuals()
    {
        StopAllCoroutines();
        findJumpButtonCoroutine = null;
        for (int i = 0; i < balloons.Length; i++)
        {
            BalloonSlot slot = balloons[i];
            if (slot?.spriteRenderer != null && i < initialStates.Length)
                slot.spriteRenderer.enabled = initialStates[i].RendererEnabled;
            if (i < initialStates.Length)
                RestoreLineEndpoints(slot, i, initialStates[i].LineRendererEnabled);
            DestroyRegrowVisual(i);
            if (i < slotRegrowing.Length)
                slotRegrowing[i] = false;
        }
    }

    private void RestoreAllBalloonPhysics()
    {
        for (int i = 0; i < balloons.Length; i++)
        {
            BalloonSlot slot = balloons[i];
            if (slot == null || !IsValidSlot(slot, i))
                continue;

            RestoreSlotPhysics(slot, i);
        }
    }

    #endregion
}
