using System;
using System.Collections.Generic;
using Bear.EventSystem;
using Bear.Logger;
using Game.Events;
using Game.Common;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level005 绳索切割控制器。
///
/// 原绳在切断前仍由场景中的 SpringJoint2D 绷紧；切断后，
/// 钉子侧和门侧各自保留一条没有碰撞体的动态半绳。刀光输入沿用
/// Level081 的“按下起笔、按移动线段检测、松手收笔”边界。
/// </summary>
[DisallowMultipleComponent]
public sealed class CutManager : MonoBehaviour, IDebuger
{
    private sealed class AnchorInitialState
    {
        public Level5Anchor Anchor;
        public Level5TrackLine TrackLine;
        public Transform StartPoint;
        public Transform EndPoint;
        public Rigidbody2D Body;
        public SpringJoint2D SpringJoint;
        public bool SpringEnabled;
        public Rigidbody2D SpringConnectedBody;
        public bool SpringEnableCollision;
        public bool SpringAutoConfigureConnectedAnchor;
        public bool SpringAutoConfigureDistance;
        public Vector2 SpringAnchor;
        public Vector2 SpringConnectedAnchor;
        public float SpringDistance;
        public float SpringFrequency;
        public float SpringDampingRatio;
        public Transform RuntimeDoorAttachment;
        public RigidbodyType2D BodyType;
        public RigidbodyConstraints2D Constraints;
        public float GravityScale;
        public float LinearDamping;
        public float AngularDamping;
        public float Mass;
        public Vector2 LinearVelocity;
        public float AngularVelocity;
        public CollisionDetectionMode2D CollisionDetectionMode;
        public bool Simulated;
        public readonly List<ColliderState> Colliders = new();
    }

    private sealed class ColliderState
    {
        public Collider2D Collider;
        public bool Enabled;
    }

    private sealed class CutRopeRuntime
    {
        public Level5Anchor Anchor;
        public Level5TrackLine AnchorLine;
        public Level5TrackLine DoorLine;
        public GameObject AnchorCutPointObject;
        public GameObject DoorCutPointObject;
        public Rigidbody2D AnchorCutPointBody;
        public Rigidbody2D DoorCutPointBody;
        public DistanceJoint2D AnchorJoint;
        public DistanceJoint2D DoorJoint;
    }

    /// <summary>
    /// Runtime replacement for an authored SpringJoint2D after the door is
    /// released. The current endpoint distance is retained so the visible
    /// support rope stays taut while the door swings.
    /// </summary>
    private sealed class ReleasedRopeRuntime
    {
        public Level5Anchor Anchor;
        public DistanceJoint2D Joint;
    }

    private sealed class CutCandidate
    {
        public Level5Anchor Anchor;
        public Vector2 CutPosition;
        public float SwipeParameter;
        public float Distance;
    }

    private enum PointerMode
    {
        None,
        Touch,
        Mouse,
    }

    #region Inspector references

    // Keep these four field names stable: Level_005.prefab already serializes them.
    [SerializeField] private List<Level5Anchor> anchors = new();
    [SerializeField] private Transform dragObject;
    [SerializeField] private Transform doorObject;
    [SerializeField] private float radius = 0.5f;

    [Header("输入与刀光")]
    [Tooltip("切割输入使用的世界相机；为空时依次使用关卡子相机和 Camera.main。")]
    [SerializeField] private Camera mainCamera;
    [Tooltip("相邻刀光采样至少相隔的屏幕像素，避免点击原地误切。")]
    [SerializeField, Min(1f)] private float minimumSliceStepPixels = 4f;
    [Tooltip("松手时启用近失误容错所需的最小手势长度（屏幕像素）；短点击不会被当作切割。")]
    [SerializeField, Min(1f)] private float minimumGestureLengthPixels = 10f;
    [Tooltip("松手容错的最大绳线距离（世界单位），避免 Level005 旧的宽半径误选相邻绳。")]
    [SerializeField, Min(0f)] private float nearMissRadius = 0.2f;
    [Tooltip("刀光相对关卡根节点的 Z 偏移；负值使其位于场景画面前方。")]
    [SerializeField] private float dragTrailDepthOffset = -0.6f;

    [Header("未断绳与门")]
    [Tooltip("未断绳的弹簧频率；数值越大越能保持门在中心。")]
    [SerializeField, Min(0.01f)] private float ropeFrequency = 2.5f;
    [Tooltip("未断绳的阻尼比。")]
    [SerializeField, Range(0f, 1f)] private float ropeDampingRatio = 0.55f;
    [Tooltip("门首次断绳后使用的重力倍率。")]
    [SerializeField, Min(0f)] private float doorGravityScale = 1f;
    [Tooltip("门首次断绳后的线性阻尼。")]
    [SerializeField, Min(0f)] private float doorLinearDamping = 0.25f;
    [Tooltip("门首次断绳后的角阻尼。")]
    [SerializeField, Min(0f)] private float doorAngularDamping = 0.35f;
    [Tooltip("是否允许门在断绳后旋转。")]
    [SerializeField] private bool allowDoorRotation = true;
    [Tooltip("是否完全锁定门的旋转。关闭时保留受阻尼和摆角限制的有限摆动，避免持续自转。")]
    [SerializeField] private bool keepDoorUpright = false;
    [Tooltip("释放后未断绳是否只限制最大长度。开启后门边和门根模式都允许绳索松弛，再由张力自然吊住门；关闭则使用定长约束。")]
    [SerializeField] private bool releasedRopesMaxDistanceOnly = true;
    [Tooltip("兼容旧 Inspector 的门侧半绳排序值；当前门侧半绳保持 authored 层级并显示在门 Sprite 后方。")]
    [SerializeField] private int doorHalfLineSortingOrder = 110;
    [Tooltip("是否为物理挂点创建门边隐藏附件；视觉绳端始终从门背后的 authored 门根进入。")]
    [SerializeField] private bool useDoorEdgeAttachments = true;
    [Tooltip("是否把释放后的物理关节也接到门边；关闭时回退到 authored 门根。Level005 为保持可见绳线与 Line 一致而关闭。")]
    [SerializeField] private bool useDoorEdgePhysics = true;
    [Tooltip("门边挂点向门的几何边界内缩的距离；仅在释放后生效。")]
    [SerializeField, Min(0f)] private float doorAttachmentInset = 0.05f;
    [Tooltip("门释放后的局部质心回退值。存在有效门几何触发框时会自动使用其局部中心，避免门内偏心触发框持续施加扭矩。")]
    [SerializeField] private Vector2 releasedDoorCenterOfMass = Vector2.zero;
    [Tooltip("释放后的最小角阻尼，避免门在一次切割冲量后持续自转。")]
    [SerializeField, Min(0f)] private float minimumReleasedAngularDamping = 12f;
    [Tooltip("释放后门的最大角速度（度/秒）；设为 0 表示不限制。")]
    [SerializeField, Min(0f)] private float maximumReleasedAngularVelocity = 180f;
    [Tooltip("切断瞬间切点分离速度的缩放，降低半绳被甩飞的程度。")]
    [SerializeField, Range(0f, 1f)] private float cutPointImpulseScale = 0.22f;
    [Tooltip("切断瞬间门的切向冲量缩放。")]
    [SerializeField, Range(0f, 1f)] private float doorSwingImpulseScale = 0.25f;
    [Tooltip("门受切向冲量的施力点从质心向切口方向偏移的比例。")]
    [SerializeField, Range(0f, 1f)] private float doorSwingForcePointRatio = 0.15f;
    [Tooltip("门受切向冲量的最大物理力臂（世界单位），避免远处切口造成自转。")]
    [SerializeField, Min(0f)] private float maximumDoorSwingLever = 0.2f;
    [Tooltip("兼容旧参数的角冲量缩放；默认关闭，避免门持续自转。")]
    [SerializeField, Range(0f, 1f)] private float doorSwingTorqueScale = 0f;
    [Tooltip("释放后每个物理步额外消除的角速度（度/秒²）。")]
    [SerializeField, Min(0f)] private float releasedAngularBrake = 180f;
    [Tooltip("释放后把门缓慢拉回初始姿态的弱角弹簧强度；设为 0 可关闭。")]
    [SerializeField, Min(0f)] private float releasedAngularRestoreTorque = 0.5f;
    [Tooltip("角度回正死区（度）；小于该误差时不施加回正力。")]
    [SerializeField, Min(0f)] private float releasedAngularRestoreDeadZone = 1f;
    [Tooltip("门相对初始姿态允许的最大摆角；设为 0 表示不限制角度。")]
    [SerializeField, Min(0f)] private float maximumReleasedSwingAngle = 75f;
    [Tooltip("门接近静止时归零的角速度阈值（度/秒）。")]
    [SerializeField, Min(0f)] private float angularSettleVelocity = 0.5f;

    [Header("断口半绳")]
    [Tooltip("切口两侧动态端点沿绳方向分开的总距离。")]
    [SerializeField, Min(0f)] private float cutPointSeparation = 0.08f;
    [Tooltip("为保证断口可见而使用的最小间隙；实际值仍会按绳长限制。")]
    [SerializeField, Min(0f)] private float minimumVisibleCutGap = 0.34f;
    [Tooltip("动态绳端质量。")]
    [SerializeField, Min(0.01f)] private float cutPointMass = 0.12f;
    [Tooltip("动态绳端重力倍率。")]
    [SerializeField, Min(0f)] private float cutPointGravityScale = 0.9f;
    [Tooltip("动态绳端线性阻尼。")]
    [SerializeField, Min(0f)] private float cutPointLinearDamping = 0.18f;
    [Tooltip("切断时给两侧绳端的切向分离速度。")]
    [SerializeField, Min(0f)] private float cutPointSeparationImpulse = 0.18f;
    [Tooltip("切断时给门的切向冲量；不同钉子会产生不同摆动方向。")]
    [SerializeField, Min(0f)] private float doorSwingImpulse = 0.85f;
    [Tooltip("切断时给门的角冲量。")]
    [SerializeField, Min(0f)] private float doorSwingTorqueImpulse = 0.35f;

    [Header("绳的柔性与收敛")]
    [Tooltip("断绳端点的额外线性阻尼。数值越大，断开后的绳端越快停止甩动。")]
    [SerializeField, Min(0f)] private float cutPointSettlementDamping = 0.75f;
    [Tooltip("断绳端点的重力倍率。提高后下坠和摆动响应更快，避免绳子显得轻飘。")]
    [SerializeField, Min(0f)] private float cutPointGravityMultiplier = 1.35f;
    [Tooltip("绳视觉曲线采样数量；数值越高，曲线越平滑。")]
    [SerializeField, Range(2, 12)] private int flexibleRopeSegments = 7;
    [Tooltip("绳视觉下垂幅度（世界单位），只影响 LineRenderer，不改变物理长度。")]
    [SerializeField, Min(0f)] private float flexibleRopeSag = 0.16f;
    [Tooltip("短绳的最小下垂幅度（世界单位），避免视觉仍像完全笔直的线。")]
    [SerializeField, Min(0f)] private float flexibleRopeMinimumSag = 0.025f;

    #endregion

    #region Runtime state

    private readonly List<AnchorInitialState> initialStates = new();
    private readonly List<CutRopeRuntime> cutRopes = new();
    private readonly List<ReleasedRopeRuntime> releasedRopes = new();

    private BaseLevelCtrl baseLevelCtrl;
    private Rigidbody2D doorBody;
    private TrailRenderer dragTrail;
    private Collider2D doorAttachmentCollider;
    private EventSubscriber eventSubscriber;
    private PointerMode pointerMode;
    private int activeFingerId = -1;
    private Vector2 gestureStartScreenPosition;
    private Vector2 latestScreenPosition;
    private Vector2 latestWorldPosition;
    private Vector2 gestureStartWorldPosition;
    private bool hasGestureStartWorldPosition;
    private bool cutCommittedForGesture;
    private bool initialized;
    private bool doorReleased;
    private bool isPaused;
    private bool physicsPaused;
    private bool resetOrReplayReceived;
    private bool cleanupComplete;
    private bool doorSimulatedBeforePause;
    private readonly List<bool> cutPointSimulatedBeforePause = new();

    private Vector3 initialDoorPosition;
    private Quaternion initialDoorRotation;
    private RigidbodyType2D initialDoorBodyType;
    private RigidbodyConstraints2D initialDoorConstraints;
    private float initialDoorGravityScale;
    private float initialDoorLinearDamping;
    private float initialDoorAngularDamping;
    private float initialDoorMass;
    private float initialDoorInertia;
    private Vector2 initialDoorCenterOfMass;
    private Vector2 initialDoorLinearVelocity;
    private float initialDoorAngularVelocity;
    private CollisionDetectionMode2D initialDoorCollisionDetectionMode;
    private bool initialDoorSimulated;
    private Vector3 initialDragPosition;
    private bool hasInitialDragPosition;

    #endregion

    #region Unity lifecycle

    private void Awake()
    {
        InitializeRuntime();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        // A reset/replay can reuse the same level object. Cleanup deliberately
        // keeps the captured authoring state, so re-enable can build a fresh
        // runtime without requiring a scene reload.
        if (cleanupComplete || resetOrReplayReceived)
        {
            resetOrReplayReceived = false;
            initialized = false;
            cleanupComplete = false;
        }

        if (!initialized)
            InitializeRuntime();

        RegisterEvents();
        isPaused = !IsGameplayPlaying();
        SetPhysicsPaused(isPaused);
    }

    private void Update()
    {
        if (!Application.isPlaying || !initialized || resetOrReplayReceived)
            return;

        bool shouldPause = !IsGameplayPlaying();
        if (shouldPause != isPaused)
        {
            isPaused = shouldPause;
            SetPhysicsPaused(isPaused);
        }

        if (isPaused)
        {
            ClearPointer();
            return;
        }

        HandlePointerInput();
    }

    private void FixedUpdate()
    {
        if (!Application.isPlaying || !initialized || isPaused || physicsPaused)
            return;

        StabilizeCutPointMotion(Time.fixedDeltaTime);

        if (!doorReleased || doorBody == null || !doorBody.simulated ||
            !ShouldAllowDoorRotation())
            return;

        // Apply damping once per physics step.  The matching LateUpdate
        // clamp below only removes solver residue after the joint solve.
        StabilizeReleasedDoorRotation(Time.fixedDeltaTime);
    }

    /// <summary>
    /// 统一门释放后的旋转开关。Prefab 仍保留旧的 allowDoorRotation 字段，
    /// keepDoorUpright 作为本关默认安全闸，避免旧序列化值让门持续自转。
    /// </summary>
    private bool ShouldAllowDoorRotation()
    {
        return allowDoorRotation && !keepDoorUpright;
    }

    private RigidbodyConstraints2D ResolveReleasedDoorConstraints()
    {
        // The authored door root currently has FreezePositionY because it is
        // only a kinematic anchor before the first cut.  Carrying that bit
        // into the dynamic phase prevents the remaining ropes from suspending
        // the door and is the source of the immovable two-rope pose.
        RigidbodyConstraints2D constraints = initialDoorConstraints &
            ~(RigidbodyConstraints2D.FreezePositionX |
              RigidbodyConstraints2D.FreezePositionY |
              RigidbodyConstraints2D.FreezeRotation);
        if (!ShouldAllowDoorRotation())
            constraints |= RigidbodyConstraints2D.FreezeRotation;

        return constraints;
    }

    private void ClampReleasedDoorAngularVelocity()
    {
        if (doorBody == null)
            return;

        float maximumAngularVelocity = Mathf.Max(0f, maximumReleasedAngularVelocity);
        if (maximumAngularVelocity > 0f)
        {
            float angularVelocity = doorBody.angularVelocity;
            if (Mathf.Abs(angularVelocity) > maximumAngularVelocity)
                doorBody.angularVelocity = Mathf.Sign(angularVelocity) * maximumAngularVelocity;
        }

        float maximumSwingAngle = Mathf.Max(0f, maximumReleasedSwingAngle);
        if (maximumSwingAngle > 0f)
        {
            float initialAngle = initialDoorRotation.eulerAngles.z;
            float relativeAngle = Mathf.DeltaAngle(initialAngle, doorBody.rotation);
            if (Mathf.Abs(relativeAngle) > maximumSwingAngle)
            {
                // Do not write Rigidbody2D.rotation here: doing so competes
                // with the DistanceJoint2D solver and can create visible
                // jitter. Only stop an outward spin; the joint/damping solver
                // remains responsible for the actual pose.
                if (doorBody.angularVelocity * Mathf.Sign(relativeAngle) > 0f)
                    doorBody.angularVelocity = 0f;
            }
        }

        if (Mathf.Abs(doorBody.angularVelocity) <= Mathf.Max(0f, angularSettleVelocity))
            doorBody.angularVelocity = 0f;
    }

    private void StabilizeReleasedDoorRotation(float deltaTime)
    {
        ClampReleasedDoorAngularVelocity();

        float brake = Mathf.Max(0f, releasedAngularBrake);
        if (brake > 0f)
        {
            float step = Mathf.Clamp(deltaTime, 0f, 0.05f);
            doorBody.angularVelocity = Mathf.MoveTowards(
                doorBody.angularVelocity,
                0f,
                brake * step);
        }

        // A distance joint can inject a small angular velocity after this
        // method has run (for example when a remaining rope becomes taut).
        // Apply a deliberately weak, configurable angular spring so that the
        // door settles back toward its authored pose instead of accumulating a
        // perpetual spin, while still leaving visible pendulum motion.
        float restoreTorque = Mathf.Max(0f, releasedAngularRestoreTorque);
        float restoreDeadZone = Mathf.Max(0f, releasedAngularRestoreDeadZone);
        if (restoreTorque > 0f)
        {
            float relativeAngle = Mathf.DeltaAngle(
                initialDoorRotation.eulerAngles.z,
                doorBody.rotation);
            if (Mathf.Abs(relativeAngle) > restoreDeadZone)
            {
                float angleRadians = relativeAngle * Mathf.Deg2Rad;
                doorBody.AddTorque(-angleRadians * restoreTorque, ForceMode2D.Force);
            }
        }
    }

    private void StabilizeCutPointMotion(float deltaTime)
    {
        float damping = Mathf.Max(0f, cutPointSettlementDamping);
        if (damping <= 0f)
            return;

        float step = Mathf.Clamp(deltaTime, 0f, 0.05f);
        float attenuation = Mathf.Exp(-damping * step);
        for (int index = 0; index < cutRopes.Count; index++)
        {
            CutRopeRuntime cut = cutRopes[index];
            if (cut == null)
                continue;

            DampCutPoint(cut.AnchorCutPointBody, attenuation);
            DampCutPoint(cut.DoorCutPointBody, attenuation);
        }
    }

    private static void DampCutPoint(Rigidbody2D body, float attenuation)
    {
        if (body == null || !body.simulated || body.bodyType != RigidbodyType2D.Dynamic)
            return;

        body.linearVelocity *= attenuation;
        if (body.linearVelocity.sqrMagnitude <= 0.0009f)
            body.linearVelocity = Vector2.zero;
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || !initialized)
            return;

        // Unity solves 2D joints immediately after FixedUpdate.  Clamp the
        // residual angular velocity here, after that solve, so a rope cannot
        // re-inject a visible spin on every frame.  No transform rotation is
        // written, therefore the door can still make a finite pendulum swing.
        if (!isPaused && !physicsPaused && doorReleased && doorBody != null &&
            doorBody.simulated && ShouldAllowDoorRotation())
        {
            // The solver may add a small angular velocity after FixedUpdate.
            // Clamp only that residue here; physical damping remains tied to
            // the fixed timestep above.
            ClampReleasedDoorAngularVelocity();
        }

        for (int index = 0; index < cutRopes.Count; index++)
        {
            CutRopeRuntime cut = cutRopes[index];
            RefreshFlexibleLineVisual(cut?.AnchorLine);
            RefreshFlexibleLineVisual(cut?.DoorLine);
        }

        // Authored ropes remain soft-looking before their first cut as well.
        // Level5TrackLine still owns the endpoint references; this pass only
        // replaces its two-point render path after its Update has run.
        for (int index = 0; index < initialStates.Count; index++)
        {
            AnchorInitialState state = initialStates[index];
            if (state?.Anchor != null && !state.Anchor.IsCut)
                RefreshFlexibleLineVisual(state.TrackLine);
        }

    }

    private void OnDisable()
    {
        EventsUtils.ResetEvents(ref eventSubscriber);
        CleanupRuntime();
    }

    private void OnDestroy()
    {
        EventsUtils.ResetEvents(ref eventSubscriber);
        CleanupRuntime();
    }

    #endregion

    #region Initialization

    private void InitializeRuntime()
    {
        if (initialized || !Application.isPlaying)
            return;

        ResolveReferences();
        CaptureInitialState();
        CreateRuntimeDoorAttachments();
        PrepareInitialPhysics();
        initialized = true;
        cleanupComplete = false;
        isPaused = !IsGameplayPlaying();
        SetPhysicsPaused(isPaused);
    }

    private void ResolveReferences()
    {
        baseLevelCtrl ??= GetComponent<BaseLevelCtrl>();

        if (anchors == null)
            anchors = new List<Level5Anchor>();

        if (anchors.Count == 0)
        {
            Level5Anchor[] discovered = GetComponentsInChildren<Level5Anchor>(true);
            anchors.AddRange(discovered);
        }

        anchors.RemoveAll(anchor => anchor == null);
        dragObject ??= transform.Find("DragTarget");
        doorObject ??= transform.Find("Rigibody2D");
        if (doorObject == null)
        {
            // A missing prefab reference must never bind the first anchor by
            // accident. Prefer the authored door name, then a body with a
            // non-trigger collider that is not one of the anchors.
            Transform namedDoor = transform.Find("Door");
            if (namedDoor != null && namedDoor.GetComponent<Rigidbody2D>() != null)
                doorObject = namedDoor;
            else
            {
                Rigidbody2D[] candidates = GetComponentsInChildren<Rigidbody2D>(true);
                for (int index = 0; index < candidates.Length; index++)
                {
                    Rigidbody2D candidate = candidates[index];
                    if (candidate == null || IsAnchorBody(candidate))
                        continue;

                    Collider2D[] candidateColliders = candidate.GetComponentsInChildren<Collider2D>(true);
                    bool hasSolidCollider = false;
                    for (int colliderIndex = 0; colliderIndex < candidateColliders.Length; colliderIndex++)
                    {
                        if (candidateColliders[colliderIndex] != null && !candidateColliders[colliderIndex].isTrigger)
                        {
                            hasSolidCollider = true;
                            break;
                        }
                    }

                    if (hasSolidCollider)
                    {
                        doorObject = candidate.transform;
                        break;
                    }
                }
            }
        }

        mainCamera ??= GetComponentInChildren<Camera>(true);
        mainCamera ??= Camera.main;
        dragTrail = dragObject != null ? dragObject.GetComponent<TrailRenderer>() : null;
        doorBody = doorObject != null ? doorObject.GetComponent<Rigidbody2D>() : null;
        if (doorBody == null && doorObject != null)
            doorBody = doorObject.gameObject.AddComponent<Rigidbody2D>();

        doorAttachmentCollider = FindDoorAttachmentCollider();

        ConfigureDragTrail();
    }

    private bool IsAnchorBody(Rigidbody2D candidate)
    {
        for (int index = 0; index < anchors.Count; index++)
        {
            Level5Anchor anchor = anchors[index];
            if (anchor != null && candidate.transform.IsChildOf(anchor.transform))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 选择门层级中最大的启用 Trigger 作为几何挂点边界。Level005 的
    /// door_0 Trigger 覆盖完整门形，根节点的小实体 Collider 只负责物理
    /// 占位，不能拿它推导绳子的视觉/受力挂点。
    /// </summary>
    private Collider2D FindDoorAttachmentCollider()
    {
        if (doorObject == null)
            return null;

        Collider2D bestTrigger = null;
        float bestTriggerArea = -1f;
        Collider2D bestCollider = null;
        float bestArea = -1f;
        Collider2D[] colliders = doorObject.GetComponentsInChildren<Collider2D>(true);
        for (int index = 0; index < colliders.Length; index++)
        {
            Collider2D collider = colliders[index];
            if (collider == null || !collider.enabled)
                continue;

            Bounds bounds = collider.bounds;
            float area = Mathf.Abs(bounds.size.x * bounds.size.y);
            if (area > bestArea)
            {
                bestArea = area;
                bestCollider = collider;
            }

            if (collider.isTrigger && area > bestTriggerArea)
            {
                bestTriggerArea = area;
                bestTrigger = collider;
            }
        }

        return bestTrigger != null ? bestTrigger : bestCollider;
    }

    /// <summary>
    /// 创建只存在于本次运行时的门边挂点。它们是 Transform 锚点，不含
    /// Renderer/Collider，不会改变 prefab，也不会参与碰撞；门旋转时会
    /// 随门根一起移动。`useDoorEdgeAttachments` 和
    /// `useDoorEdgePhysics` 分别控制视觉端点和物理端点；Level005 当前
    /// 均关闭，开局、切口和释放阶段统一使用 authored 门根端点。
    /// </summary>
    private void CreateRuntimeDoorAttachments()
    {
        if (doorObject == null || initialStates.Count == 0 ||
            (!useDoorEdgeAttachments && !useDoorEdgePhysics))
            return;

        if (doorAttachmentCollider == null)
            doorAttachmentCollider = FindDoorAttachmentCollider();

        for (int index = 0; index < initialStates.Count; index++)
        {
            AnchorInitialState state = initialStates[index];
            if (state == null || state.Anchor == null || state.RuntimeDoorAttachment != null)
                continue;

            Vector2 ropeStartPosition = state.StartPoint != null
                ? (Vector2)state.StartPoint.position
                : state.Anchor.transform.position;
            Vector2 attachmentPosition = ResolveDoorAttachmentPosition(ropeStartPosition);
            GameObject attachmentObject = new GameObject(
                "Level005_DoorVisualAttachment_" + state.Anchor.GetInstanceID());
            attachmentObject.transform.SetParent(doorObject, true);
            attachmentObject.transform.position = new Vector3(
                attachmentPosition.x,
                attachmentPosition.y,
                doorObject.position.z);
            attachmentObject.transform.localRotation = Quaternion.identity;
            attachmentObject.hideFlags = HideFlags.HideInHierarchy;
            state.RuntimeDoorAttachment = attachmentObject.transform;
        }
    }

    /// <summary>
    /// The authored line endpoint is the visible geometry. Optional edge
    /// attachments are only used for physics and never move the visible rope
    /// in front of the door Sprite.
    /// </summary>
    private Transform ResolveVisualDoorPoint(AnchorInitialState state)
    {
        return state?.EndPoint != null ? state.EndPoint : doorObject;
    }

    private Transform ResolvePhysicalDoorPoint(AnchorInitialState state)
    {
        // Physics may use a hidden edge endpoint for a believable force arm.
        // Rendering and hit testing deliberately remain on the authored root
        // so the visible rope enters from behind the door.
        if (useDoorEdgePhysics && state?.RuntimeDoorAttachment != null)
            return state.RuntimeDoorAttachment;

        return ResolveVisualDoorPoint(state);
    }

    private Transform ResolveRuntimePhysicalDoorPoint(AnchorInitialState state)
    {
        // CutLine runs just before doorReleased is set. Resolve the physical
        // endpoint explicitly so the new door-side half uses the same hidden
        // force attachment that ReleaseDoorPhysics will bind after the cut.
        return useDoorEdgePhysics && state?.RuntimeDoorAttachment != null
            ? state.RuntimeDoorAttachment
            : ResolveVisualDoorPoint(state);
    }

    /// <summary>
    /// 门侧半绳的视觉端点保持 authored 门根，使绳段从门背后进入；
    /// 物理 edge 挂点只承担力臂，不改变可见线的连接位置。
    /// </summary>
    private Transform ResolveCutVisualDoorPoint(AnchorInitialState state)
    {
        // The visible rope must enter the door from behind its Sprite. Keep
        // the authored door-root endpoint for rendering and hit testing even
        // when physics uses a hidden edge attachment for a more believable
        // force arm. Sorting remains authored (below the door Sprite).
        return ResolveVisualDoorPoint(state);
    }

    private Transform ResolveCurrentVisualDoorPoint(AnchorInitialState state)
    {
        // Keep the visible endpoint identical before and after release so a
        // first cut cannot re-aim the line from the authored root to a front
        // edge attachment.
        return ResolveCutVisualDoorPoint(state);
    }

    private Vector2 ResolveDoorAttachmentPosition(Vector2 anchorPosition)
    {
        if (doorAttachmentCollider == null || !doorAttachmentCollider.enabled)
            doorAttachmentCollider = FindDoorAttachmentCollider();

        Vector2 center = doorAttachmentCollider != null
            ? doorAttachmentCollider.bounds.center
            : (Vector2)(doorObject != null ? doorObject.position : Vector3.zero);
        Vector2 direction = anchorPosition - center;
        if (direction.sqrMagnitude <= 0.000001f)
            direction = Vector2.up;
        direction.Normalize();

        if (doorAttachmentCollider != null)
        {
            Bounds bounds = doorAttachmentCollider.bounds;
            Vector2 point;
            // For the authored Level005 door, use the actual BoxCollider2D
            // shape in local space. ClosestPoint(anchor) is the nearest point
            // on the rectangle, which may be a side midpoint or a corner and
            // therefore gives each rope a different, incorrect force arm.
            // A centre-to-anchor ray always lands on the corresponding edge.
            if (TryResolveBoxColliderRadialPoint(
                    doorAttachmentCollider as BoxCollider2D,
                    anchorPosition,
                    out Vector2 boxCenter,
                    out point))
            {
                center = boxCenter;
            }
            else
            {
                // Generic colliders retain their authored closest-point
                // behavior. If that result is unavailable, degenerate, or
                // the anchor overlaps the AABB, use a conservative bounds ray
                // so the attachment still remains on the door side.
                point = doorAttachmentCollider.ClosestPoint(anchorPosition);
                bool invalidPoint = float.IsNaN(point.x) || float.IsNaN(point.y) ||
                                    float.IsInfinity(point.x) || float.IsInfinity(point.y);
                if (invalidPoint || bounds.Contains(anchorPosition) ||
                    (point - center).sqrMagnitude <= 0.000001f)
                {
                    point = ResolveBoundsRadialPoint(bounds, center, direction);
                }
            }

            float inset = Mathf.Max(0f, doorAttachmentInset);
            if (inset > 0f)
                point = Vector2.MoveTowards(point, center, inset);
            return point;
        }

        // A missing collider is unusual, but keeping a small local fallback
        // preserves a useful force arm without changing the authored scene.
        float fallbackRadius = 0.35f;
        if (doorBody != null)
        {
            Collider2D physicalCollider = doorBody.GetComponent<Collider2D>();
            if (physicalCollider != null)
                fallbackRadius = Mathf.Max(fallbackRadius, physicalCollider.bounds.extents.magnitude);
        }
        return center + direction * fallbackRadius;
    }

    private static bool TryResolveBoxColliderRadialPoint(
        BoxCollider2D box,
        Vector2 anchorPosition,
        out Vector2 center,
        out Vector2 boundaryPoint)
    {
        center = default;
        boundaryPoint = default;
        if (box == null || !box.enabled || box.transform == null)
            return false;

        Transform colliderTransform = box.transform;
        center = colliderTransform.TransformPoint(box.offset);
        Vector2 localAnchor = colliderTransform.InverseTransformPoint(anchorPosition);
        Vector2 localDirection = localAnchor - box.offset;
        if (localDirection.sqrMagnitude <= 0.000001f)
            localDirection = Vector2.up;

        Vector2 halfSize = box.size * 0.5f;
        if (halfSize.x <= 0.000001f || halfSize.y <= 0.000001f)
            return false;

        Vector2 normalizedDirection = localDirection.normalized;
        float xDistance = Mathf.Abs(normalizedDirection.x) > 0.0001f
            ? halfSize.x / Mathf.Abs(normalizedDirection.x)
            : float.PositiveInfinity;
        float yDistance = Mathf.Abs(normalizedDirection.y) > 0.0001f
            ? halfSize.y / Mathf.Abs(normalizedDirection.y)
            : float.PositiveInfinity;
        float distance = Mathf.Min(xDistance, yDistance);
        if (float.IsNaN(distance) || float.IsInfinity(distance) || distance <= 0f)
            return false;

        Vector2 localBoundary = box.offset + normalizedDirection * distance;
        boundaryPoint = colliderTransform.TransformPoint(localBoundary);
        return !(float.IsNaN(boundaryPoint.x) || float.IsNaN(boundaryPoint.y) ||
                 float.IsInfinity(boundaryPoint.x) || float.IsInfinity(boundaryPoint.y));
    }

    private static Vector2 ResolveBoundsRadialPoint(Bounds bounds, Vector2 center, Vector2 direction)
    {
        float xDistance = Mathf.Abs(direction.x) > 0.0001f
            ? bounds.extents.x / Mathf.Abs(direction.x)
            : float.PositiveInfinity;
        float yDistance = Mathf.Abs(direction.y) > 0.0001f
            ? bounds.extents.y / Mathf.Abs(direction.y)
            : float.PositiveInfinity;
        float distance = Mathf.Min(xDistance, yDistance);
        if (float.IsNaN(distance) || float.IsInfinity(distance) || distance <= 0f)
            return center;

        return center + direction * distance;
    }

    private Vector2 ResolveReleasedDoorCenterOfMass()
    {
        // In the optional root-attachment mode every authored rope and every
        // runtime cut half is physically attached to the door root. A trigger
        // child can move Unity's automatically calculated COM below that root,
        // which creates a permanent gravity torque and an endless spin. Use an
        // explicit designer override, otherwise keep the force line at zero.
        if (!useDoorEdgePhysics)
        {
            return releasedDoorCenterOfMass.sqrMagnitude > 0.000001f
                ? releasedDoorCenterOfMass
                : Vector2.zero;
        }

        if (doorBody != null && doorAttachmentCollider != null &&
            doorAttachmentCollider.enabled)
        {
            // The attachment collider describes the visible door's actual
            // centre, while the authored Rigidbody2D root is offset above it.
            // Using that local centre removes the constant gravity torque that
            // otherwise makes the released door spin in place.
            return doorBody.transform.InverseTransformPoint(
                doorAttachmentCollider.bounds.center);
        }

        return releasedDoorCenterOfMass;
    }

    /// <summary>
    /// 释放后的门根节点只有一个很小的占位 Collider，Unity 会据此算出
    /// 极小的转动惯量（Level005 约 0.0417），而可见门实际宽高约为
    /// 2.54 x 3.05。用可见门的矩形近似惯量，避免绳子的微小冲量把门
    /// 变成持续高速旋转的陀螺。质量仍沿用门 Rigidbody2D 的 authored 值。
    /// </summary>
    private float ResolveReleasedDoorInertia()
    {
        if (doorBody == null)
            return Mathf.Max(0.01f, initialDoorInertia);

        float width;
        float height;
        if (doorAttachmentCollider is BoxCollider2D box && box.enabled)
        {
            Vector3 scale = box.transform.lossyScale;
            width = Mathf.Abs(box.size.x * scale.x);
            height = Mathf.Abs(box.size.y * scale.y);
        }
        else if (doorAttachmentCollider != null && doorAttachmentCollider.enabled)
        {
            Vector2 size = doorAttachmentCollider.bounds.size;
            width = Mathf.Abs(size.x);
            height = Mathf.Abs(size.y);
        }
        else
        {
            // Keep a valid, conservative fallback for custom door geometry.
            Collider2D physicalCollider = doorBody.GetComponent<Collider2D>();
            Vector2 size = physicalCollider != null
                ? physicalCollider.bounds.size
                : Vector2.one;
            width = Mathf.Abs(size.x);
            height = Mathf.Abs(size.y);
        }

        if (width <= 0.0001f || height <= 0.0001f)
            return Mathf.Max(0.01f, initialDoorInertia);

        float mass = Mathf.Max(0.01f, doorBody.mass);
        // Rectangle moment of inertia around its centre: m(w²+h²)/12.
        float inertia = mass * (width * width + height * height) / 12f;
        return Mathf.Max(0.01f, inertia);
    }

    private void ConfigureDragTrail()
    {
        if (dragTrail == null)
            return;

        // Keep the Level005 prefab untouched while matching the short, crisp
        // blade trail used by Level081.
        dragTrail.time = 0.18f;
        dragTrail.minVertexDistance = 0.035f;
        dragTrail.numCapVertices = 3;
        dragTrail.numCornerVertices = 2;
        dragTrail.sortingLayerID = SortingLayer.NameToID("SceneLayer");
        dragTrail.sortingOrder = 1000;
    }

    private void CaptureInitialState()
    {
        if (initialStates.Count > 0)
            return;

        if (doorBody != null)
        {
            initialDoorPosition = doorBody.position;
            initialDoorRotation = doorBody.transform.rotation;
            initialDoorBodyType = doorBody.bodyType;
            initialDoorConstraints = doorBody.constraints;
            initialDoorGravityScale = doorBody.gravityScale;
            initialDoorLinearDamping = doorBody.linearDamping;
            initialDoorAngularDamping = doorBody.angularDamping;
            initialDoorMass = doorBody.mass;
            initialDoorInertia = doorBody.inertia;
            initialDoorCenterOfMass = doorBody.centerOfMass;
            initialDoorLinearVelocity = doorBody.linearVelocity;
            initialDoorAngularVelocity = doorBody.angularVelocity;
            initialDoorCollisionDetectionMode = doorBody.collisionDetectionMode;
            initialDoorSimulated = doorBody.simulated;
        }

        if (dragObject != null)
        {
            initialDragPosition = dragObject.position;
            hasInitialDragPosition = true;
        }

        for (int index = 0; index < anchors.Count; index++)
        {
            Level5Anchor anchor = anchors[index];
            if (anchor == null)
                continue;

            Level5TrackLine line = anchor.TrackLine;
            if (line == null)
                line = anchor.GetComponentInChildren<Level5TrackLine>(true);

            Rigidbody2D body = anchor.GetComponent<Rigidbody2D>();
            if (body == null)
                body = anchor.gameObject.AddComponent<Rigidbody2D>();

            // Read the joint directly. Level5Anchor intentionally remains a
            // shared, data-only component and its Awake order is not guaranteed
            // relative to this controller.
            SpringJoint2D joint = anchor.GetComponent<SpringJoint2D>();
            Transform start = line != null ? line.GetStartPoint() : anchor.transform;
            Transform end = line != null ? line.GetEndPoint() : doorObject;
            AnchorInitialState state = new()
            {
                Anchor = anchor,
                TrackLine = line,
                StartPoint = start,
                EndPoint = end,
                Body = body,
                SpringJoint = joint,
                SpringEnabled = joint != null && joint.enabled,
                SpringConnectedBody = joint != null ? joint.connectedBody : null,
                SpringEnableCollision = joint != null && joint.enableCollision,
                SpringAutoConfigureConnectedAnchor = joint != null && joint.autoConfigureConnectedAnchor,
                SpringAutoConfigureDistance = joint != null && joint.autoConfigureDistance,
                SpringAnchor = joint != null ? joint.anchor : Vector2.zero,
                SpringConnectedAnchor = joint != null ? joint.connectedAnchor : Vector2.zero,
                SpringDistance = joint != null ? joint.distance : 0f,
                SpringFrequency = joint != null ? joint.frequency : 0f,
                SpringDampingRatio = joint != null ? joint.dampingRatio : 0f,
                BodyType = body.bodyType,
                Constraints = body.constraints,
                GravityScale = body.gravityScale,
                LinearDamping = body.linearDamping,
                AngularDamping = body.angularDamping,
                Mass = body.mass,
                LinearVelocity = body.linearVelocity,
                AngularVelocity = body.angularVelocity,
                CollisionDetectionMode = body.collisionDetectionMode,
                Simulated = body.simulated,
            };

            Collider2D[] colliders = anchor.GetComponentsInChildren<Collider2D>(true);
            for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
            {
                Collider2D collider = colliders[colliderIndex];
                if (collider == null)
                    continue;

                state.Colliders.Add(new ColliderState
                {
                    Collider = collider,
                    Enabled = collider.enabled,
                });
            }

            initialStates.Add(state);
        }
    }

    private void PrepareInitialPhysics()
    {
        doorReleased = false;
        if (dragTrail != null)
        {
            dragTrail.emitting = false;
            dragTrail.Clear();
        }
        if (dragObject != null && hasInitialDragPosition)
            dragObject.position = initialDragPosition;

        if (doorBody != null)
        {
            doorBody.simulated = false;
            if (doorBody.bodyType != RigidbodyType2D.Static)
            {
                doorBody.linearVelocity = Vector2.zero;
                doorBody.angularVelocity = 0f;
            }
            doorBody.position = initialDoorPosition;
            doorBody.transform.rotation = initialDoorRotation;
            doorBody.bodyType = RigidbodyType2D.Kinematic;
            doorBody.constraints = RigidbodyConstraints2D.FreezeRotation;
            doorBody.gravityScale = 0f;
            doorBody.centerOfMass = initialDoorCenterOfMass;
            doorBody.simulated = true;
        }

        for (int index = 0; index < initialStates.Count; index++)
        {
            AnchorInitialState state = initialStates[index];
            if (state == null || state.Anchor == null)
                continue;

            RestoreAnchorState(state);
            Rigidbody2D body = state.Body;
            if (body != null)
            {
                body.simulated = false;
                if (body.bodyType != RigidbodyType2D.Static)
                {
                    body.linearVelocity = Vector2.zero;
                    body.angularVelocity = 0f;
                }
                body.bodyType = RigidbodyType2D.Static;
                body.constraints = RigidbodyConstraints2D.FreezeAll;
                body.gravityScale = 0f;
                body.simulated = true;
            }

            for (int colliderIndex = 0; colliderIndex < state.Colliders.Count; colliderIndex++)
            {
                ColliderState colliderState = state.Colliders[colliderIndex];
                if (colliderState.Collider != null)
                    colliderState.Collider.enabled = false;
            }

            Level5TrackLine line = state.TrackLine;
            if (line != null)
            {
                line.SetStartPoint(state.StartPoint != null ? state.StartPoint : state.Anchor.transform);
                line.SetEndPoint(ResolveCurrentVisualDoorPoint(state));
                ResetLineVisual(line);
            }

            SpringJoint2D joint = state.SpringJoint;
            if (joint == null || doorBody == null)
                continue;

            joint.connectedBody = doorBody;
            joint.enabled = state.SpringEnabled;
            joint.enableCollision = false;
            joint.autoConfigureConnectedAnchor = false;
            joint.autoConfigureDistance = false;
            joint.anchor = state.StartPoint != null && body != null
                ? body.transform.InverseTransformPoint(state.StartPoint.position)
                : Vector2.zero;
            Transform physicalDoorPoint = ResolvePhysicalDoorPoint(state);
            joint.connectedAnchor = physicalDoorPoint != null
                ? doorBody.transform.InverseTransformPoint(physicalDoorPoint.position)
                : Vector2.zero;
            // When edge physics is enabled, the connected anchor is already
            // the matching runtime door-edge attachment.  Keep the authored
            // SpringJoint length in the same geometry so enabling the joint
            // cannot pull the door toward the old root endpoint on the first
            // physics step.  Root mode intentionally retains the authored
            // distance for backwards-compatible prefab behaviour.
            float distance = ResolveAuthoredRopeDistance(state);
            if (useDoorEdgePhysics && physicalDoorPoint != null &&
                state.StartPoint != null)
            {
                distance = Vector2.Distance(
                    state.StartPoint.position,
                    physicalDoorPoint.position);
            }
            joint.distance = Mathf.Max(0.01f, distance);
            joint.frequency = Mathf.Max(0.01f, ropeFrequency);
            joint.dampingRatio = Mathf.Clamp01(ropeDampingRatio);
        }

        Physics2D.SyncTransforms();
    }

    #endregion

    #region Pointer input

    private void HandlePointerInput()
    {
        if (Input.touchCount > 0)
        {
            HandleTouchInput();
            return;
        }

        if (pointerMode == PointerMode.Touch)
        {
            EndPointer(latestScreenPosition, true);
            return;
        }

        HandleMouseInput();
    }

    private void HandleTouchInput()
    {
        if (pointerMode == PointerMode.Touch)
        {
            if (!TryGetActiveTouch(out Touch touch))
            {
                EndPointer(latestScreenPosition, true);
                return;
            }

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                EndPointer(touch.position, touch.phase == TouchPhase.Canceled);
            else
                UpdatePointer(touch.position);
            return;
        }

        if (pointerMode != PointerMode.None)
            return;

        for (int index = 0; index < Input.touchCount; index++)
        {
            Touch touch = Input.GetTouch(index);
            if (touch.phase != TouchPhase.Began)
                continue;

            BeginPointer(touch.position, touch.fingerId, PointerMode.Touch);
            if (pointerMode != PointerMode.None)
                break;
        }
    }

    private void HandleMouseInput()
    {
        if (pointerMode == PointerMode.None)
        {
            if (Input.GetMouseButtonDown(0))
                BeginPointer(Input.mousePosition, -1, PointerMode.Mouse);
            return;
        }

        if (pointerMode != PointerMode.Mouse)
            return;

        if (Input.GetMouseButton(0))
            UpdatePointer(Input.mousePosition);
        else if (Input.GetMouseButtonUp(0))
            EndPointer(Input.mousePosition, false);
        else
            EndPointer(latestScreenPosition, true);
    }

    private void BeginPointer(Vector2 screenPosition, int fingerId, PointerMode mode)
    {
        if (!CanReceiveInput() || IsPointerOverUi(screenPosition, fingerId))
            return;

        pointerMode = mode;
        activeFingerId = fingerId;
        cutCommittedForGesture = false;
        gestureStartScreenPosition = screenPosition;
        latestScreenPosition = screenPosition;
        latestWorldPosition = ScreenToWorld(screenPosition);
        gestureStartWorldPosition = latestWorldPosition;
        hasGestureStartWorldPosition = true;
        StartDragTrail(latestWorldPosition);
    }

    private void UpdatePointer(Vector2 screenPosition)
    {
        if (pointerMode == PointerMode.None || !CanReceiveInput())
        {
            ClearPointer();
            return;
        }

        Vector2 screenDelta = screenPosition - latestScreenPosition;
        if (screenDelta.sqrMagnitude < minimumSliceStepPixels * minimumSliceStepPixels)
            return;

        Vector2 worldPosition = ScreenToWorld(screenPosition);
        TryCutSegment(latestWorldPosition, worldPosition, false);
        latestScreenPosition = screenPosition;
        latestWorldPosition = worldPosition;
        UpdateDragTrail(worldPosition);
    }

    private void EndPointer(Vector2 screenPosition, bool canceled)
    {
        if (pointerMode == PointerMode.None)
            return;

        if (!canceled)
        {
            UpdatePointer(screenPosition);

            // A short final movement can be below the sampling threshold. If
            // the deliberate gesture still ended over a rope, use the whole
            // gesture once as a forgiving fallback. This is intentionally
            // deferred until release; approaching a rope cannot commit an
            // early cut at the wrong position.
            if (!cutCommittedForGesture && hasGestureStartWorldPosition)
            {
                float gestureLength = Vector2.Distance(
                    gestureStartScreenPosition,
                    screenPosition);
                if (gestureLength >= Mathf.Max(1f, minimumGestureLengthPixels))
                {
                    TryCutSegment(
                        gestureStartWorldPosition,
                        ScreenToWorld(screenPosition),
                        true);
                }
            }
        }

        if (dragTrail != null)
            dragTrail.emitting = false;

        pointerMode = PointerMode.None;
        activeFingerId = -1;
        cutCommittedForGesture = false;
        hasGestureStartWorldPosition = false;
        gestureStartScreenPosition = Vector2.zero;
        gestureStartWorldPosition = Vector2.zero;
        latestScreenPosition = Vector2.zero;
        latestWorldPosition = Vector2.zero;
    }

    private void ClearPointer()
    {
        if (dragTrail != null)
        {
            dragTrail.emitting = false;
            dragTrail.Clear();
        }

        pointerMode = PointerMode.None;
        activeFingerId = -1;
        cutCommittedForGesture = false;
        hasGestureStartWorldPosition = false;
        gestureStartScreenPosition = Vector2.zero;
        gestureStartWorldPosition = Vector2.zero;
        latestScreenPosition = Vector2.zero;
        latestWorldPosition = Vector2.zero;
    }

    private void StartDragTrail(Vector2 worldPosition)
    {
        if (dragObject == null)
            return;

        Vector3 position = dragObject.position;
        position.x = worldPosition.x;
        position.y = worldPosition.y;
        position.z = transform.position.z + dragTrailDepthOffset;
        dragObject.position = position;

        if (dragTrail == null)
            dragTrail = dragObject.GetComponent<TrailRenderer>();
        if (dragTrail == null)
            return;

        dragTrail.emitting = false;
        dragTrail.Clear();
        dragTrail.AddPosition(position);
        dragTrail.emitting = true;
    }

    private void UpdateDragTrail(Vector2 worldPosition)
    {
        if (dragObject == null)
            return;

        Vector3 position = dragObject.position;
        position.x = worldPosition.x;
        position.y = worldPosition.y;
        position.z = transform.position.z + dragTrailDepthOffset;
        dragObject.position = position;
    }

    private bool TryGetActiveTouch(out Touch activeTouch)
    {
        for (int index = 0; index < Input.touchCount; index++)
        {
            Touch touch = Input.GetTouch(index);
            if (touch.fingerId != activeFingerId)
                continue;

            activeTouch = touch;
            return true;
        }

        activeTouch = default;
        return false;
    }

    private bool CanReceiveInput()
    {
        if (!initialized || isPaused || resetOrReplayReceived ||
            !Singleton<PlayCtrl>.IsInitialized || !IsGameplayPlaying())
            return false;

        if (baseLevelCtrl == null)
            return true;

        ActorCtrl actor = baseLevelCtrl.Actor;
        return baseLevelCtrl.ActorCtrlable &&
               !baseLevelCtrl.IsSuccess() &&
               !baseLevelCtrl.IsFail() &&
               (actor == null || !actor.IsDied);
    }

    private static bool IsPointerOverUi(Vector2 screenPosition, int fingerId)
    {
        return fingerId >= 0
            ? InputUtils.IsPointerOverUI(fingerId)
            : InputUtils.IsPointerOverUI(screenPosition);
    }

    private Vector2 ScreenToWorld(Vector2 screenPosition)
    {
        Camera camera = mainCamera != null ? mainCamera : Camera.main;
        if (camera == null)
            return screenPosition;

        float distance = Mathf.Abs(camera.transform.position.z - transform.position.z);
        Vector3 world = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, distance));
        return new Vector2(world.x, world.y);
    }

    #endregion

    #region Cutting and runtime rope creation

    private void TryCutSegment(Vector2 start, Vector2 end, bool allowNearMiss)
    {
        // A single swipe can cross multiple ropes. Snapshot all crossings
        // before mutating any rope so releasing the door after the first cut
        // cannot move the shared endpoint and hide later crossings.
        List<CutCandidate> intersectingCandidates = new();
        CutCandidate nearestNearMiss = null;

        for (int index = 0; index < initialStates.Count; index++)
        {
            AnchorInitialState state = initialStates[index];
            if (state == null || state.Anchor == null || state.Anchor.IsCut || state.TrackLine == null)
                continue;

            // Use the same current endpoints that the uncut renderer displays:
            // authored door-root geometry by default, or the matching runtime
            // door-edge attachment when the optional edge-visual mode is
            // enabled. The renderer may be rebound to runtime cut-point
            // transforms, but each new cut is always tested against that
            // rope's current full visible segment.
            Transform lineStart = state.StartPoint != null
                ? state.StartPoint
                : state.TrackLine.GetStartPoint();
            Transform lineEnd = ResolveCurrentVisualDoorPoint(state);
            if (lineStart == null || lineEnd == null)
                continue;

            bool intersects = SegmentsIntersect(start, end, lineStart.position, lineEnd.position);
            float distance = SegmentToSegmentDistance(start, end, lineStart.position, lineEnd.position);
            float allowedDistance = allowNearMiss
                ? Mathf.Min(
                    Mathf.Max(0f, radius),
                    Mathf.Max(0f, nearMissRadius))
                : Mathf.Max(0f, radius);
            if (distance > allowedDistance || (!intersects && !allowNearMiss))
                continue;

            Vector2 candidatePoint;
            if (intersects && TryGetSegmentIntersection(
                    start,
                    end,
                    lineStart.position,
                    lineEnd.position,
                    out Vector2 intersection))
            {
                // Preserve the actual crossing point. Replacing it with a
                // nearest-endpoint projection is especially visible when
                // several ropes share the same door endpoint.
                candidatePoint = intersection;
            }
            else
            {
                candidatePoint = ClosestPointOnSegmentToSegment(
                    start,
                    end,
                    lineStart.position,
                    lineEnd.position);
            }
            float swipeParameter = SegmentParameter(start, end, candidatePoint);

            if (intersects)
            {
                intersectingCandidates.Add(new CutCandidate
                {
                    Anchor = state.Anchor,
                    CutPosition = candidatePoint,
                    SwipeParameter = swipeParameter,
                    Distance = distance,
                });
            }
            else if (allowNearMiss &&
                     (nearestNearMiss == null || distance < nearestNearMiss.Distance))
            {
                nearestNearMiss = new CutCandidate
                {
                    Anchor = state.Anchor,
                    CutPosition = candidatePoint,
                    SwipeParameter = swipeParameter,
                    Distance = distance,
                };
            }
        }

        // Preserve the old forgiving release behaviour when no rope was
        // actually crossed: only the closest near miss is accepted. Once a
        // swipe genuinely crosses ropes, every crossing is committed.
        if (intersectingCandidates.Count == 0 && nearestNearMiss != null)
            intersectingCandidates.Add(nearestNearMiss);

        intersectingCandidates.Sort((first, second) =>
        {
            int order = first.SwipeParameter.CompareTo(second.SwipeParameter);
            return order != 0 ? order : first.Distance.CompareTo(second.Distance);
        });

        bool committed = false;
        for (int index = 0; index < intersectingCandidates.Count; index++)
        {
            CutCandidate candidate = intersectingCandidates[index];
            if (candidate?.Anchor == null || candidate.Anchor.IsCut)
                continue;

            CutLine(candidate.Anchor, start, end, candidate.CutPosition);
            committed |= candidate.Anchor.IsCut;
        }

        cutCommittedForGesture |= committed;
    }

    private void CutLine(
        Level5Anchor anchor,
        Vector2 swipeStart,
        Vector2 swipeEnd,
        Vector2 requestedCutPosition)
    {
        if (anchor == null || anchor.IsCut || doorObject == null || doorBody == null)
            return;

        AnchorInitialState state = FindInitialState(anchor);
        Level5TrackLine sourceLine = state != null ? state.TrackLine : anchor.TrackLine;
        if (sourceLine == null)
            return;

        Transform anchorPoint = state?.StartPoint != null
            ? state.StartPoint
            : sourceLine.GetStartPoint() != null
                ? sourceLine.GetStartPoint()
                : anchor.transform;
        // Keep the cut geometry on the current full visible rope. Before
        // release this is the authored door-root segment; after release it is
        // still the root segment by default, or the same runtime door-edge
        // segment used by hit testing and rendering when edge visuals are on.
        Transform doorPoint = ResolveCurrentVisualDoorPoint(state);
        if (anchorPoint == null || doorPoint == null)
            return;

        Vector2 ropeStart = anchorPoint.position;
        Vector2 ropeEnd = doorPoint.position;
        Vector2 ropeDirection = (ropeEnd - ropeStart).normalized;
        if (ropeDirection.sqrMagnitude < 0.0001f)
            ropeDirection = Vector2.up;

        float ropeLength = Vector2.Distance(ropeStart, ropeEnd);
        // Use the hit-test projection captured from the same input sample.
        // Re-projecting after a moving-door update can shift a middle cut to
        // the wrong rope end and make the whole rope appear to disappear.
        Vector2 cutPosition = requestedCutPosition;
        if (float.IsNaN(cutPosition.x) || float.IsNaN(cutPosition.y) ||
            float.IsInfinity(cutPosition.x) || float.IsInfinity(cutPosition.y))
        {
            cutPosition = ClosestPointOnSegmentToSegment(
                swipeStart,
                swipeEnd,
                ropeStart,
                ropeEnd);
        }
        float gap = Mathf.Min(
            Mathf.Max(Mathf.Max(0f, cutPointSeparation), Mathf.Max(0f, minimumVisibleCutGap)),
            ropeLength * 0.35f);
        float halfGap = gap * 0.5f;
        float cutAlongRope = Vector2.Dot(cutPosition - ropeStart, ropeDirection);
        if (ropeLength > 0.0001f)
        {
            float minAlongRope = halfGap;
            float maxAlongRope = Mathf.Max(minAlongRope, ropeLength - halfGap);
            cutAlongRope = Mathf.Clamp(cutAlongRope, minAlongRope, maxAlongRope);
            cutPosition = ropeStart + ropeDirection * cutAlongRope;
        }

        Vector2 anchorCutPosition = cutPosition - ropeDirection * halfGap;
        Vector2 doorCutPosition = cutPosition + ropeDirection * halfGap;
        Rigidbody2D anchorBody = state != null ? state.Body : anchor.GetComponent<Rigidbody2D>();
        if (anchorBody == null)
            return;

        // Build the complete runtime rope before changing the authored state.
        // A failed allocation therefore leaves the original spring and line
        // untouched instead of leaving a half-cut rope behind.
        string cutId = anchor.GetInstanceID().ToString();
        GameObject anchorCutPointObject = CreateCutPoint("Level005_AnchorCutPoint_" + cutId, anchorCutPosition, false);
        // The door-side free end follows the door's gravity while the door is
        // released; matching the gravity prevents the visible half from
        // collapsing relative to its attachment point.
        GameObject doorCutPointObject = CreateCutPoint(
            "Level005_DoorCutPoint_" + cutId,
            doorCutPosition,
            false,
            Mathf.Max(0f, doorGravityScale));
        if (anchorCutPointObject == null || doorCutPointObject == null)
        {
            DestroyRuntimeObject(anchorCutPointObject);
            DestroyRuntimeObject(doorCutPointObject);
            return;
        }

        Rigidbody2D anchorCutPointBody = anchorCutPointObject.GetComponent<Rigidbody2D>();
        Rigidbody2D doorCutPointBody = doorCutPointObject.GetComponent<Rigidbody2D>();
        // The two visible halves retain their authored cut lengths. A
        // max-distance-only joint is correct for a support rope, but a free
        // cut endpoint would otherwise collapse to its anchor under gravity
        // and make a middle cut look like the whole rope vanished.
        DistanceJoint2D anchorJoint = CreateCutJoint(
            anchorCutPointBody,
            anchorBody,
            anchorPoint,
            anchorCutPosition,
            false);
        DistanceJoint2D doorJoint = CreateCutJoint(
            doorCutPointBody,
            doorBody,
            ResolveRuntimePhysicalDoorPoint(state),
            doorCutPosition,
            false);
        if (anchorJoint == null || doorJoint == null)
        {
            DestroyRuntimeObject(anchorCutPointObject);
            DestroyRuntimeObject(doorCutPointObject);
            return;
        }

        Transform cutVisualDoorPoint = ResolveCurrentVisualDoorPoint(state);
        Level5TrackLine doorLine = CreateHalfLine(
            sourceLine,
            "Level005_DoorHalfLine_" + cutId,
            doorCutPointBody.transform,
            cutVisualDoorPoint,
            false);
        if (doorLine == null)
        {
            DestroyRuntimeObject(anchorCutPointObject);
            DestroyRuntimeObject(doorCutPointObject);
            return;
        }

        // Commit only after both joints and both visual halves exist.
        sourceLine.SetStartPoint(anchorPoint);
        sourceLine.SetEndPoint(anchorCutPointBody.transform);
        RefreshLineVisual(sourceLine);
        anchor.SetCut(true);
        RemoveReleasedRope(anchor);
        if (state?.SpringJoint != null)
            state.SpringJoint.enabled = false;

        CutRopeRuntime runtime = new()
        {
            Anchor = anchor,
            AnchorLine = sourceLine,
            DoorLine = doorLine,
            AnchorCutPointObject = anchorCutPointObject,
            DoorCutPointObject = doorCutPointObject,
            AnchorCutPointBody = anchorCutPointBody,
            DoorCutPointBody = doorCutPointBody,
            AnchorJoint = anchorJoint,
            DoorJoint = doorJoint,
        };
        cutRopes.Add(runtime);

        bool wasDoorReleased = doorReleased;
        ReleaseDoorPhysics();
        // On the first cut ReleaseDoorPhysics rebinds before enabling the
        // door body. Later cuts happen while the body is already dynamic, so
        // rebind just this newly-created half with its joint temporarily off.
        if (wasDoorReleased)
            RebindCutDoorAttachment(runtime);
        anchorCutPointBody.simulated = true;
        doorCutPointBody.simulated = true;

        Vector2 swipeDirection = swipeEnd - swipeStart;
        if (swipeDirection.sqrMagnitude < 0.0001f)
            swipeDirection = ropeDirection;
        swipeDirection.Normalize();
        Vector2 tangent = new(-ropeDirection.y, ropeDirection.x);
        float tangentSign = Mathf.Sign(Cross(swipeDirection, ropeDirection));
        if (Mathf.Abs(tangentSign) < 0.5f)
            tangentSign = 1f;

        float cutPointImpulse = Mathf.Max(0f, cutPointSeparationImpulse) *
                                Mathf.Clamp01(cutPointImpulseScale);
        if (cutPointImpulse > 0f)
        {
            // A small nudge keeps both loose halves visibly alive without
            // throwing them away from their respective nail/door anchors.
            anchorCutPointBody.linearVelocity = -tangent * (cutPointImpulse * tangentSign);
            doorCutPointBody.linearVelocity = tangent * (cutPointImpulse * tangentSign);
        }

        float swingImpulse = Mathf.Max(0f, doorSwingImpulse) *
                             Mathf.Clamp01(doorSwingImpulseScale);
        if (swingImpulse > 0f)
        {
            Vector2 forceLever = cutPosition - doorBody.worldCenterOfMass;
            float maximumLever = Mathf.Max(0f, maximumDoorSwingLever);
            if (maximumLever > 0f)
                forceLever = Vector2.ClampMagnitude(forceLever, maximumLever);
            Vector2 forcePoint = doorBody.worldCenterOfMass +
                                 forceLever * Mathf.Clamp01(doorSwingForcePointRatio);
            doorBody.AddForceAtPosition(
                tangent * (swingImpulse * tangentSign),
                forcePoint,
                ForceMode2D.Impulse);
        }

        // Keep the legacy torque field available to designers, but make it an
        // explicitly opt-in, bounded contribution.  The default scale is 0,
        // so a cut cannot start an unbounded spin by itself.
        float torqueImpulse = Mathf.Max(0f, doorSwingTorqueImpulse) *
                              Mathf.Clamp01(doorSwingTorqueScale);
        if (torqueImpulse > 0f)
        {
            Vector2 lever = cutPosition - doorBody.worldCenterOfMass;
            float torqueSign = Mathf.Sign(Cross(lever, tangent));
            if (Mathf.Abs(torqueSign) < 0.5f)
                torqueSign = tangentSign;
            doorBody.AddTorque(torqueImpulse * torqueSign, ForceMode2D.Impulse);
        }

        // Enable both joints only after all connected anchors, distances and
        // initial velocities are final. This prevents a newly cut rope from
        // injecting a solver impulse into an already-released door.
        anchorJoint.enabled = true;
        doorJoint.enabled = true;

        Physics2D.SyncTransforms();
        RefreshLineVisual(sourceLine);
        RefreshLineVisual(doorLine);
    }

    private void ReleaseDoorPhysics()
    {
        if (doorBody == null)
            return;

        if (!doorReleased)
        {
            doorReleased = true;
            doorBody.simulated = false;
            if (doorBody.bodyType != RigidbodyType2D.Static)
            {
                doorBody.linearVelocity = Vector2.zero;
                doorBody.angularVelocity = 0f;
            }
            doorBody.bodyType = RigidbodyType2D.Dynamic;
            doorBody.gravityScale = Mathf.Max(0f, doorGravityScale);
            doorBody.linearDamping = Mathf.Max(0f, doorLinearDamping);
            doorBody.angularDamping = Mathf.Max(
                Mathf.Max(0f, doorAngularDamping),
                Mathf.Max(0f, minimumReleasedAngularDamping));
            doorBody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            doorBody.constraints = ResolveReleasedDoorConstraints();
            // The authored door contains a large trigger child below the
            // physical root. Use the visible door centre as the release COM so
            // gravity does not create a constant torque around the rope root.
            doorBody.centerOfMass = ResolveReleasedDoorCenterOfMass();
            doorBody.inertia = ResolveReleasedDoorInertia();
            doorBody.linearVelocity = Vector2.zero;
            doorBody.angularVelocity = 0f;
            if (!ShouldAllowDoorRotation())
                doorBody.rotation = initialDoorRotation.eulerAngles.z;

            // Replace the remaining authored springs with runtime ropes.
            // Released support ropes are tension-only by default. When edge
            // physics is disabled, the physical endpoint remains the authored
            // door root, matching the visible LineRenderer endpoint.
            CreateReleasedRopeJoints();

            // All cut halves are already in the runtime list at this point.
            // Rebind them while the door is still unsimulated so moving the
            // connected anchor from the authored root to the door edge cannot
            // inject a one-frame solver impulse.
            RebindAllCutDoorAttachments();

            doorBody.simulated = true;
            doorBody.WakeUp();
        }
    }

    private void CreateReleasedRopeJoints()
    {
        if (doorBody == null)
            return;

        CreateRuntimeDoorAttachments();
        Physics2D.SyncTransforms();

        for (int index = 0; index < initialStates.Count; index++)
        {
            AnchorInitialState state = initialStates[index];
            if (state == null || state.Anchor == null || state.Anchor.IsCut ||
                state.Body == null || state.SpringJoint == null || !state.SpringEnabled)
                continue;

            if (HasReleasedRope(state.Anchor))
                continue;

            // Configure while the door is unsimulated so no transient spring
            // and distance-joint impulses can be mixed in the same step.
            state.SpringJoint.enabled = false;
            DistanceJoint2D joint = doorBody.gameObject.AddComponent<DistanceJoint2D>();
            joint.enabled = false;
            joint.connectedBody = state.Body;
            joint.autoConfigureConnectedAnchor = false;
            joint.autoConfigureDistance = false;
            // Keep an edge-attached rope continuous at release.  The authored
            // distance was measured from the door root, so reusing it for a
            // door-edge anchor creates slack and removes the intended lever
            // arm when only two ropes remain.  Root-attached ropes retain the
            // authored minimum for backwards-compatible tension behaviour.
            bool edgePhysics = useDoorEdgePhysics &&
                               state.RuntimeDoorAttachment != null;
            // Both edge and root attachments use the same designer-selectable
            // tension policy.  In particular, the default max-distance-only
            // mode lets two remaining ropes go slack and settle naturally
            // instead of locking the door to the second intersection of two
            // exact-distance circles.
            joint.maxDistanceOnly = releasedRopesMaxDistanceOnly;
            joint.enableCollision = false;
            Transform attachment = ResolvePhysicalDoorPoint(state);
            Transform visualDoorPoint = ResolveCurrentVisualDoorPoint(state);
            Transform startPoint = state.StartPoint != null
                ? state.StartPoint
                : state.Anchor.transform;
            joint.anchor = attachment != null
                ? doorBody.transform.InverseTransformPoint(attachment.position)
                : Vector2.zero;
            joint.connectedAnchor = startPoint != null
                ? state.Body.transform.InverseTransformPoint(startPoint.position)
                : Vector2.zero;

            float currentDistance = attachment != null && startPoint != null
                ? Vector2.Distance(attachment.position, startPoint.position)
                : 0f;
            float authoredDistance = ResolveAuthoredRopeDistance(state);
            float targetDistance = edgePhysics
                ? currentDistance
                : Mathf.Max(currentDistance, authoredDistance);

            // The tiny epsilon prevents a degenerate zero-length constraint
            // when custom geometry overlaps.
            joint.distance = Mathf.Max(0.01f, targetDistance);
            joint.enabled = true;

            if (state.TrackLine != null && visualDoorPoint != null)
            {
                // Keep the renderer on the same current door-side endpoint as
                // the physical joint. This makes the two-rope pose and any
                // later cut line up with the geometry used for hit testing.
                state.TrackLine.SetEndPoint(visualDoorPoint);
                RefreshLineVisual(state.TrackLine);
            }

            releasedRopes.Add(new ReleasedRopeRuntime
            {
                Anchor = state.Anchor,
                Joint = joint,
            });
        }
    }

    private void RebindAllCutDoorAttachments()
    {
        for (int index = 0; index < cutRopes.Count; index++)
            RebindCutDoorAttachment(cutRopes[index]);
    }

    private void RebindCutDoorAttachment(CutRopeRuntime cut)
    {
        if (cut == null || cut.Anchor == null || doorBody == null)
            return;

        AnchorInitialState state = FindInitialState(cut.Anchor);
        Transform attachment = ResolvePhysicalDoorPoint(state);
        if (attachment == null)
            return;

        if (cut.DoorLine != null)
        {
            Transform visualDoorPoint = ResolveCutVisualDoorPoint(state);
            if (visualDoorPoint != null)
                cut.DoorLine.SetEndPoint(visualDoorPoint);
            RefreshLineVisual(cut.DoorLine);
        }

        DistanceJoint2D joint = cut.DoorJoint;
        if (joint == null || cut.DoorCutPointBody == null)
            return;

        bool wasEnabled = joint.enabled;
        joint.enabled = false;
        joint.connectedBody = doorBody;
        joint.autoConfigureConnectedAnchor = false;
        joint.autoConfigureDistance = false;
        joint.connectedAnchor = doorBody.transform.InverseTransformPoint(attachment.position);
        joint.distance = Mathf.Max(
            0.01f,
            Vector2.Distance(cut.DoorCutPointBody.position, attachment.position));
        joint.enabled = wasEnabled;
    }

    private bool HasReleasedRope(Level5Anchor anchor)
    {
        for (int index = 0; index < releasedRopes.Count; index++)
        {
            if (releasedRopes[index]?.Anchor == anchor)
                return true;
        }

        return false;
    }

    private void RemoveReleasedRope(Level5Anchor anchor)
    {
        if (anchor == null)
            return;

        for (int index = releasedRopes.Count - 1; index >= 0; index--)
        {
            ReleasedRopeRuntime rope = releasedRopes[index];
            if (rope?.Anchor != anchor)
                continue;

            if (rope.Joint != null)
            {
                rope.Joint.enabled = false;
                DestroyRuntimeComponent(rope.Joint);
            }
            releasedRopes.RemoveAt(index);
        }
    }

    private GameObject CreateCutPoint(
        string name,
        Vector2 worldPosition,
        bool simulated,
        float? gravityScaleOverride = null)
    {
        GameObject pointObject = new(name);
        pointObject.transform.SetParent(transform, true);
        pointObject.transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
        int nonCollisionLayer = LayerMask.NameToLayer("NonCollisionRigibodyLayer");
        if (nonCollisionLayer >= 0)
            pointObject.layer = nonCollisionLayer;

        Rigidbody2D body = pointObject.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.mass = Mathf.Max(0.01f, cutPointMass);
        float baseGravityScale = gravityScaleOverride ?? cutPointGravityScale;
        body.gravityScale = Mathf.Max(
            0f,
            baseGravityScale * Mathf.Max(0f, cutPointGravityMultiplier));
        // A detached rope endpoint is intentionally dynamic, but an almost
        // undamped endpoint keeps pendulum-swinging forever after a cut. The
        // designer value remains the lower bound; the private settlement
        // value supplies a predictable stop without changing rope joints.
        body.linearDamping = Mathf.Max(
            Mathf.Max(0f, cutPointLinearDamping),
            Mathf.Max(0f, cutPointSettlementDamping));
        body.angularDamping = 0.8f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.simulated = simulated;
        // Deliberately no Collider2D: rope halves and their endpoints never block the actor.
        return pointObject;
    }

    private DistanceJoint2D CreateCutJoint(
        Rigidbody2D pointBody,
        Rigidbody2D connectedBody,
        Transform connectedPoint,
        Vector2 pointPosition,
        bool maxDistanceOnly)
    {
        if (pointBody == null || connectedBody == null)
            return null;

        DistanceJoint2D joint = pointBody.gameObject.AddComponent<DistanceJoint2D>();
        joint.enabled = false;
        joint.connectedBody = connectedBody;
        // Keep the authored cut point stable after assigning the connected body.
        // Unity's default can recalculate connectedAnchor when the body is bound.
        joint.autoConfigureConnectedAnchor = false;
        joint.autoConfigureDistance = false;
        // The caller chooses exact length for visible cut halves and
        // max-distance-only for any future tension-only use. Keeping this
        // choice explicit prevents a visual half from collapsing while
        // retaining physically plausible support after subsequent cuts.
        joint.maxDistanceOnly = maxDistanceOnly;
        joint.enableCollision = false;
        joint.anchor = Vector2.zero;
        joint.connectedAnchor = connectedPoint != null
            ? connectedBody.transform.InverseTransformPoint(connectedPoint.position)
            : connectedBody.transform.InverseTransformPoint(pointPosition);
        joint.distance = Mathf.Max(0.01f,
            connectedPoint != null
                ? Vector2.Distance(pointPosition, connectedPoint.position)
                : Vector2.Distance(pointPosition, connectedBody.position));
        return joint;
    }

    private Level5TrackLine CreateHalfLine(
        Level5TrackLine source,
        string name,
        Transform startPoint,
        Transform endPoint,
        bool promoteDoorHalfSorting)
    {
        if (source == null || startPoint == null || endPoint == null)
            return null;

        GameObject lineObject = Instantiate(source.gameObject);
        lineObject.name = name;
        // Keep both halves in the authored sorting hierarchy. The door-side
        // half intentionally remains behind the door Sprite, so its endpoint
        // reads as entering the back of the door instead of being drawn over
        // the door face.
        Transform visualParent = promoteDoorHalfSorting
            ? transform
            : source.transform.parent != null
            ? source.transform.parent
            : transform;
        lineObject.transform.SetParent(visualParent, true);
        Level5TrackLine line = lineObject.GetComponent<Level5TrackLine>();
        if (line == null)
        {
            DestroyRuntimeObject(lineObject);
            return null;
        }

        line.SetStartPoint(startPoint);
        line.SetEndPoint(endPoint);
        LineRenderer renderer = line.GetComponent<LineRenderer>();
        if (renderer != null)
        {
            renderer.enabled = true;
            LineRenderer sourceRenderer = source.GetComponent<LineRenderer>();
            if (sourceRenderer != null)
            {
                // Keep the authored layer as the source of truth. Hardcoding
                // a layer here makes a runtime clone disappear when a project
                // changes its sorting-layer table.
                renderer.sortingLayerID = sourceRenderer.sortingLayerID;
                renderer.sortingOrder = sourceRenderer.sortingOrder;
            }
            if (promoteDoorHalfSorting)
                renderer.sortingOrder = ResolveDoorHalfLineSortingOrder(sourceRenderer);
        }
        RefreshLineVisual(line);
        return line;
    }

    /// <summary>
    /// Legacy helper retained for Inspector compatibility. The current Level005
    /// path does not promote door-side halves; they use the source renderer's
    /// authored order and therefore stay behind the door Sprite.
    /// </summary>
    private int ResolveDoorHalfLineSortingOrder(LineRenderer sourceRenderer)
    {
        int sortingOrder = Mathf.Max(
            doorHalfLineSortingOrder,
            sourceRenderer != null ? sourceRenderer.sortingOrder + 1 : 0);

        if (doorObject == null)
            return sortingOrder;

        int sortingLayerId = sourceRenderer != null
            ? sourceRenderer.sortingLayerID
            : SortingLayer.NameToID("SceneLayer");
        Renderer[] doorRenderers = doorObject.GetComponentsInChildren<Renderer>(true);
        for (int index = 0; index < doorRenderers.Length; index++)
        {
            Renderer renderer = doorRenderers[index];
            if (renderer != null && renderer.sortingLayerID == sortingLayerId)
                sortingOrder = Mathf.Max(sortingOrder, renderer.sortingOrder + 1);
        }

        return sortingOrder;
    }

    private AnchorInitialState FindInitialState(Level5Anchor anchor)
    {
        for (int index = 0; index < initialStates.Count; index++)
        {
            if (initialStates[index]?.Anchor == anchor)
                return initialStates[index];
        }

        return null;
    }

    #endregion

    #region Pause, reset and cleanup

    private void RegisterEvents()
    {
        EventsUtils.ResetEvents(ref eventSubscriber);
        eventSubscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
        eventSubscriber.Subscribe<GamePauseEvent>(OnGamePause);
        eventSubscriber.Subscribe<GameResumeEvent>(OnGameResume);
        eventSubscriber.Subscribe<GameResetEvent>(OnGameReset);
        eventSubscriber.Subscribe<GameReplayEvent>(OnGameReplay);
    }

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        if (evt == null || resetOrReplayReceived)
            return;

        isPaused = !string.Equals(evt.NewState, GamePlayStateName.PLAYING, StringComparison.Ordinal);
        if (isPaused)
            ClearPointer();
        SetPhysicsPaused(isPaused);
    }

    private void OnGamePause(GamePauseEvent _)
    {
        if (resetOrReplayReceived)
            return;

        isPaused = true;
        ClearPointer();
        SetPhysicsPaused(true);
    }

    private void OnGameResume(GameResumeEvent _)
    {
        if (resetOrReplayReceived)
            return;

        isPaused = !IsGameplayPlaying();
        SetPhysicsPaused(isPaused);
    }

    private void OnGameReset(GameResetEvent _)
    {
        BeginTeardownForReset();
    }

    private void OnGameReplay(GameReplayEvent _)
    {
        BeginTeardownForReset();
    }

    private void BeginTeardownForReset()
    {
        if (resetOrReplayReceived)
            return;

        resetOrReplayReceived = true;
        isPaused = true;
        ClearPointer();
        // PlayCtrl destroys and recreates the level after its transition
        // delay. Disable authored links during that window instead of
        // restoring them visibly; restoring here creates a one-frame rope
        // reconnect before the old level is destroyed.
        CleanupRuntime(false);
    }

    private void SetPhysicsPaused(bool paused)
    {
        if (physicsPaused == paused)
            return;

        if (paused)
        {
            physicsPaused = true;
            doorSimulatedBeforePause = doorBody != null && doorBody.simulated;
            cutPointSimulatedBeforePause.Clear();
            for (int index = 0; index < cutRopes.Count; index++)
            {
                Rigidbody2D body = cutRopes[index]?.AnchorCutPointBody;
                cutPointSimulatedBeforePause.Add(body != null && body.simulated);
                if (body != null)
                    body.simulated = false;

                body = cutRopes[index]?.DoorCutPointBody;
                cutPointSimulatedBeforePause.Add(body != null && body.simulated);
                if (body != null)
                    body.simulated = false;
            }

            if (doorBody != null)
                doorBody.simulated = false;
            return;
        }

        physicsPaused = false;
        if (doorBody != null && doorSimulatedBeforePause)
        {
            doorBody.simulated = true;
            doorBody.WakeUp();
        }

        int stateIndex = 0;
        for (int index = 0; index < cutRopes.Count; index++)
        {
            Rigidbody2D body = cutRopes[index]?.AnchorCutPointBody;
            bool shouldSimulate = stateIndex < cutPointSimulatedBeforePause.Count && cutPointSimulatedBeforePause[stateIndex++];
            if (body != null && shouldSimulate)
            {
                body.simulated = true;
                body.WakeUp();
            }

            body = cutRopes[index]?.DoorCutPointBody;
            shouldSimulate = stateIndex < cutPointSimulatedBeforePause.Count && cutPointSimulatedBeforePause[stateIndex++];
            if (body != null && shouldSimulate)
            {
                body.simulated = true;
                body.WakeUp();
            }
        }
    }

    private void CleanupRuntime(bool restoreAuthoredState = true)
    {
        if (cleanupComplete && !initialized)
            return;

        cleanupComplete = true;
        initialized = false;
        doorReleased = false;
        physicsPaused = false;
        ClearPointer();

        for (int index = 0; index < cutRopes.Count; index++)
        {
            CutRopeRuntime cut = cutRopes[index];
            if (cut == null)
                continue;

            DestroyRuntimeObject(cut.AnchorCutPointObject);
            DestroyRuntimeObject(cut.DoorCutPointObject);
            if (cut.DoorLine != null)
                DestroyRuntimeObject(cut.DoorLine.gameObject);
        }
        cutRopes.Clear();

        // These joints live on the authored door GameObject, so remove the
        // component itself without deactivating the door hierarchy.
        for (int index = 0; index < releasedRopes.Count; index++)
        {
            ReleasedRopeRuntime rope = releasedRopes[index];
            if (rope?.Joint != null)
            {
                rope.Joint.enabled = false;
                DestroyRuntimeComponent(rope.Joint);
            }
        }
        releasedRopes.Clear();
        cutPointSimulatedBeforePause.Clear();
        DestroyRuntimeDoorAttachments();

        if (!restoreAuthoredState)
        {
            DisableAuthoredStateForTeardown();
            return;
        }

        for (int index = 0; index < initialStates.Count; index++)
        {
            AnchorInitialState state = initialStates[index];
            if (state == null)
                continue;

            RestoreAnchorState(state);
            if (state.TrackLine != null)
            {
                state.TrackLine.SetStartPoint(state.StartPoint != null ? state.StartPoint : state.Anchor.transform);
                state.TrackLine.SetEndPoint(state.EndPoint != null ? state.EndPoint : doorObject);
                ResetLineVisual(state.TrackLine);
            }

            if (state.Body != null)
            {
                state.Body.simulated = false;
                if (state.Body.bodyType != RigidbodyType2D.Static)
                {
                    state.Body.linearVelocity = Vector2.zero;
                    state.Body.angularVelocity = 0f;
                }
                RestoreRigidbodyState(
                    state.Body,
                    state.BodyType,
                    state.Constraints,
                    state.GravityScale,
                    state.LinearDamping,
                    state.AngularDamping,
                    state.Mass,
                    state.LinearVelocity,
                    state.AngularVelocity,
                    state.CollisionDetectionMode,
                    state.Simulated);
            }

            for (int colliderIndex = 0; colliderIndex < state.Colliders.Count; colliderIndex++)
            {
                ColliderState colliderState = state.Colliders[colliderIndex];
                if (colliderState.Collider != null)
                    colliderState.Collider.enabled = colliderState.Enabled;
            }
        }

        if (doorBody != null)
        {
            doorBody.simulated = false;
            if (doorBody.bodyType != RigidbodyType2D.Static)
            {
                doorBody.linearVelocity = Vector2.zero;
                doorBody.angularVelocity = 0f;
            }
            doorBody.position = initialDoorPosition;
            doorBody.transform.rotation = initialDoorRotation;
            doorBody.centerOfMass = initialDoorCenterOfMass;
            RestoreRigidbodyState(
                doorBody,
                initialDoorBodyType,
                initialDoorConstraints,
                initialDoorGravityScale,
                initialDoorLinearDamping,
                initialDoorAngularDamping,
                initialDoorMass,
                initialDoorLinearVelocity,
                initialDoorAngularVelocity,
                initialDoorCollisionDetectionMode,
                initialDoorSimulated);
            // RestoreRigidbodyState intentionally remains shared by the
            // anchor snapshots; the door's manually assigned inertia is
            // restored explicitly after its authored body state is back.
            doorBody.inertia = Mathf.Max(0.01f, initialDoorInertia);
        }

        Physics2D.SyncTransforms();
    }

    private void DisableAuthoredStateForTeardown()
    {
        for (int index = 0; index < initialStates.Count; index++)
        {
            AnchorInitialState state = initialStates[index];
            if (state == null)
                continue;

            if (state.SpringJoint != null)
                state.SpringJoint.enabled = false;

            if (state.TrackLine != null)
            {
                LineRenderer renderer = state.TrackLine.GetComponent<LineRenderer>();
                if (renderer != null)
                    renderer.enabled = false;
            }
        }

        if (doorBody != null)
        {
            doorBody.simulated = false;
            if (doorBody.bodyType != RigidbodyType2D.Static)
            {
                doorBody.linearVelocity = Vector2.zero;
                doorBody.angularVelocity = 0f;
            }
        }
    }

    private void DestroyRuntimeDoorAttachments()
    {
        for (int index = 0; index < initialStates.Count; index++)
        {
            AnchorInitialState state = initialStates[index];
            if (state?.RuntimeDoorAttachment == null)
                continue;

            DestroyRuntimeObject(state.RuntimeDoorAttachment.gameObject);
            state.RuntimeDoorAttachment = null;
        }
    }

    private static void DestroyRuntimeObject(UnityEngine.Object target)
    {
        if (target == null)
            return;

        if (target is GameObject gameObject)
            gameObject.SetActive(false);
        else if (target is Component component && component.gameObject != null)
            component.gameObject.SetActive(false);

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(target);
        else
            UnityEngine.Object.DestroyImmediate(target);
    }

    private static void DestroyRuntimeComponent(Component target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(target);
        else
            UnityEngine.Object.DestroyImmediate(target);
    }

    #endregion

    #region Geometry and state helpers

    private static void RestoreAnchorState(AnchorInitialState state)
    {
        if (state == null)
            return;

        if (state.Anchor != null)
            state.Anchor.SetCut(false);

        SpringJoint2D joint = state.SpringJoint;
        if (joint == null)
            return;

        joint.connectedBody = state.SpringConnectedBody;
        joint.enableCollision = state.SpringEnableCollision;
        joint.autoConfigureConnectedAnchor = state.SpringAutoConfigureConnectedAnchor;
        joint.autoConfigureDistance = state.SpringAutoConfigureDistance;
        joint.anchor = state.SpringAnchor;
        joint.connectedAnchor = state.SpringConnectedAnchor;
        joint.distance = state.SpringDistance;
        joint.frequency = state.SpringFrequency;
        joint.dampingRatio = state.SpringDampingRatio;
        joint.enabled = state.SpringEnabled;
    }

    private static void RestoreRigidbodyState(
        Rigidbody2D body,
        RigidbodyType2D bodyType,
        RigidbodyConstraints2D constraints,
        float gravityScale,
        float linearDamping,
        float angularDamping,
        float mass,
        Vector2 linearVelocity,
        float angularVelocity,
        CollisionDetectionMode2D collisionDetectionMode,
        bool simulated)
    {
        if (body == null)
            return;

        // Unity ignores velocity writes on Static bodies. Apply velocities
        // while the body is non-static, then assign the requested final type.
        if (body.bodyType == RigidbodyType2D.Static && bodyType != RigidbodyType2D.Static)
            body.bodyType = bodyType;

        body.constraints = constraints;
        body.gravityScale = gravityScale;
        body.linearDamping = linearDamping;
        body.angularDamping = angularDamping;
        body.mass = mass;
        body.collisionDetectionMode = collisionDetectionMode;
        if (bodyType != RigidbodyType2D.Static)
        {
            body.linearVelocity = linearVelocity;
            body.angularVelocity = angularVelocity;
        }

        body.bodyType = bodyType;
        body.simulated = simulated;
    }

    private static void ResetLineVisual(Level5TrackLine line)
    {
        if (line == null)
            return;

        LineRenderer renderer = line.GetComponent<LineRenderer>();
        if (renderer != null)
            renderer.enabled = true;
        line.SetAlpha(1f);
        RefreshLineVisual(line);
    }

    private void RefreshFlexibleLineVisual(Level5TrackLine line)
    {
        if (line == null)
            return;

        LineRenderer renderer = line.GetComponent<LineRenderer>();
        Transform start = line.GetStartPoint();
        Transform end = line.GetEndPoint();
        if (renderer == null || start == null || end == null)
            return;

        int sampleCount = Mathf.Clamp(flexibleRopeSegments + 1, 3, 13);
        renderer.positionCount = sampleCount;

        Vector3 startWorld = start.position;
        Vector3 endWorld = end.position;
        float distance = Vector2.Distance(startWorld, endWorld);
        float sag = Mathf.Clamp(
            Mathf.Max(0f, flexibleRopeSag) * Mathf.Clamp01(distance / 3f),
            Mathf.Max(0f, flexibleRopeMinimumSag),
            Mathf.Max(0f, distance * 0.24f));

        for (int index = 0; index < sampleCount; index++)
        {
            float t = sampleCount <= 1 ? 0f : index / (float)(sampleCount - 1);
            Vector3 worldPosition = Vector3.Lerp(startWorld, endWorld, t);
            // A gravity-facing parabola gives the rope a stable soft-body
            // silhouette without injecting animation or changing physics.
            worldPosition.y -= Mathf.Sin(Mathf.PI * t) * sag;

            if (renderer.useWorldSpace)
                renderer.SetPosition(index, worldPosition);
            else
                renderer.SetPosition(index, line.transform.InverseTransformPoint(worldPosition));
        }
    }

    private static void RefreshLineVisual(Level5TrackLine line)
    {
        if (line == null)
            return;

        LineRenderer renderer = line.GetComponent<LineRenderer>();
        if (renderer == null)
            return;

        if (renderer.positionCount != 2)
            renderer.positionCount = 2;

        Transform start = line.GetStartPoint();
        Transform end = line.GetEndPoint();
        if (start == null || end == null)
            return;

        renderer.SetPosition(0, line.transform.InverseTransformPoint(start.position));
        renderer.SetPosition(1, line.transform.InverseTransformPoint(end.position));
    }

    private static float SegmentToSegmentDistance(Vector2 firstStart, Vector2 firstEnd, Vector2 secondStart, Vector2 secondEnd)
    {
        if (SegmentsIntersect(firstStart, firstEnd, secondStart, secondEnd))
            return 0f;

        return Mathf.Min(
            DistancePointToSegment(firstStart, secondStart, secondEnd),
            DistancePointToSegment(firstEnd, secondStart, secondEnd),
            DistancePointToSegment(secondStart, firstStart, firstEnd),
            DistancePointToSegment(secondEnd, firstStart, firstEnd));
    }

    private static float SegmentParameter(Vector2 segmentStart, Vector2 segmentEnd, Vector2 point)
    {
        Vector2 delta = segmentEnd - segmentStart;
        float lengthSquared = delta.sqrMagnitude;
        if (lengthSquared <= 0.000001f)
            return 0f;

        return Mathf.Clamp01(Vector2.Dot(point - segmentStart, delta) / lengthSquared);
    }

    private float ResolveAuthoredRopeDistance(AnchorInitialState state)
    {
        if (state == null)
            return 0f;

        // SpringJoint2D.distance is the authored gameplay length. Prefer it
        // over the renderer's approximate endpoints so releasing a rope does
        // not introduce a one-frame shortening impulse.
        if (state.SpringDistance > 0.0001f)
            return state.SpringDistance;

        if (state.StartPoint != null && state.EndPoint != null)
            return Vector2.Distance(state.StartPoint.position, state.EndPoint.position);

        return Vector2.Distance(
            state.Anchor != null ? state.Anchor.transform.position : Vector2.zero,
            doorObject != null ? doorObject.position : Vector2.zero);
    }

    private static Vector2 ClosestPointOnSegmentToSegment(Vector2 firstStart, Vector2 firstEnd, Vector2 secondStart, Vector2 secondEnd)
    {
        Vector2 firstDelta = firstEnd - firstStart;
        float firstLengthSquared = firstDelta.sqrMagnitude;
        if (firstLengthSquared <= 0.000001f)
            return ClosestPoint(secondStart, secondEnd, firstStart);

        Vector2 secondDelta = secondEnd - secondStart;
        float secondLengthSquared = secondDelta.sqrMagnitude;
        if (secondLengthSquared <= 0.000001f)
            return secondStart;

        if (SegmentsIntersect(firstStart, firstEnd, secondStart, secondEnd))
        {
            if (TryGetSegmentIntersection(firstStart, firstEnd, secondStart, secondEnd, out Vector2 intersection))
                return intersection;

            // Collinear overlap has infinitely many valid intersections. Use
            // the midpoint of the overlap, then project it onto the rope.
            Vector2 overlapStart = ClosestPoint(secondStart, secondEnd, firstStart);
            Vector2 overlapEnd = ClosestPoint(secondStart, secondEnd, firstEnd);
            return (overlapStart + overlapEnd) * 0.5f;
        }

        // For non-intersecting segments the minimum is always attained by an
        // endpoint projected onto the opposite segment.  Keep the returned
        // point on the rope (the second segment) so the cut cannot jump to a
        // false point on the swipe path or to an unrelated endpoint.
        Vector2 firstCandidate = ClosestPoint(secondStart, secondEnd, firstStart);
        Vector2 secondCandidate = ClosestPoint(secondStart, secondEnd, firstEnd);
        Vector2 thirdProjection = ClosestPoint(firstStart, firstEnd, secondStart);
        Vector2 fourthProjection = ClosestPoint(firstStart, firstEnd, secondEnd);

        float firstDistance = (firstStart - firstCandidate).sqrMagnitude;
        float secondDistance = (firstEnd - secondCandidate).sqrMagnitude;
        float thirdDistance = (secondStart - thirdProjection).sqrMagnitude;
        float fourthDistance = (secondEnd - fourthProjection).sqrMagnitude;

        Vector2 result = firstCandidate;
        float bestDistance = firstDistance;
        if (secondDistance < bestDistance)
        {
            bestDistance = secondDistance;
            result = secondCandidate;
        }
        if (thirdDistance < bestDistance)
        {
            bestDistance = thirdDistance;
            result = secondStart;
        }
        if (fourthDistance < bestDistance)
            result = secondEnd;
        return result;
    }

    private static bool TryGetSegmentIntersection(
        Vector2 firstStart,
        Vector2 firstEnd,
        Vector2 secondStart,
        Vector2 secondEnd,
        out Vector2 intersection)
    {
        Vector2 firstDelta = firstEnd - firstStart;
        Vector2 secondDelta = secondEnd - secondStart;
        float denominator = Cross(firstDelta, secondDelta);
        if (Mathf.Abs(denominator) <= 0.000001f)
        {
            intersection = default;
            return false;
        }

        float firstParameter = Cross(secondStart - firstStart, secondDelta) / denominator;
        intersection = firstStart + firstDelta * firstParameter;
        return true;
    }

    private static Vector2 ClosestPoint(Vector2 segmentStart, Vector2 segmentEnd, Vector2 point)
    {
        Vector2 delta = segmentEnd - segmentStart;
        float lengthSquared = delta.sqrMagnitude;
        if (lengthSquared <= 0.000001f)
            return segmentStart;

        float t = Mathf.Clamp01(Vector2.Dot(point - segmentStart, delta) / lengthSquared);
        return segmentStart + delta * t;
    }

    private static float DistancePointToSegment(Vector2 point, Vector2 segmentStart, Vector2 segmentEnd)
    {
        return Vector2.Distance(point, ClosestPoint(segmentStart, segmentEnd, point));
    }

    private static bool SegmentsIntersect(Vector2 firstStart, Vector2 firstEnd, Vector2 secondStart, Vector2 secondEnd)
    {
        float firstOrientation = Cross(firstEnd - firstStart, secondStart - firstStart);
        float secondOrientation = Cross(firstEnd - firstStart, secondEnd - firstStart);
        float thirdOrientation = Cross(secondEnd - secondStart, firstStart - secondStart);
        float fourthOrientation = Cross(secondEnd - secondStart, firstEnd - secondStart);

        const float epsilon = 0.00001f;
        bool properIntersection =
            ((firstOrientation > epsilon && secondOrientation < -epsilon) ||
             (firstOrientation < -epsilon && secondOrientation > epsilon)) &&
            ((thirdOrientation > epsilon && fourthOrientation < -epsilon) ||
             (thirdOrientation < -epsilon && fourthOrientation > epsilon));
        if (properIntersection)
            return true;

        return (Mathf.Abs(firstOrientation) <= epsilon && OnSegment(firstStart, firstEnd, secondStart)) ||
               (Mathf.Abs(secondOrientation) <= epsilon && OnSegment(firstStart, firstEnd, secondEnd)) ||
               (Mathf.Abs(thirdOrientation) <= epsilon && OnSegment(secondStart, secondEnd, firstStart)) ||
               (Mathf.Abs(fourthOrientation) <= epsilon && OnSegment(secondStart, secondEnd, firstEnd));
    }

    private static bool OnSegment(Vector2 start, Vector2 end, Vector2 point)
    {
        return point.x >= Mathf.Min(start.x, end.x) - 0.00001f &&
               point.x <= Mathf.Max(start.x, end.x) + 0.00001f &&
               point.y >= Mathf.Min(start.y, end.y) - 0.00001f &&
               point.y <= Mathf.Max(start.y, end.y) + 0.00001f;
    }

    private static float Cross(Vector2 first, Vector2 second)
    {
        return first.x * second.y - first.y * second.x;
    }

    private static bool IsGameplayPlaying()
    {
        if (!Singleton<PlayCtrl>.IsInitialized)
            return true;

        PlayCtrl playCtrl = Singleton<PlayCtrl>.Instance;
        if (playCtrl == null || !playCtrl.IsInitialized)
            return true;

        return playCtrl.IsActive && playCtrl.CheckState(GamePlayStateName.PLAYING);
    }

    #endregion
}
