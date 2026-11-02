using System;
using System.Collections.Generic;
using Bear.EventSystem;
using Game.Common;
using Game.Events;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level143 钢绳机关控制器。
/// 玩家可以拖动五个图钉改变钢绳坡度；划线尝试切割时钢绳保持完整，
/// 并在真实接触点复用 Level052 的 SparkExplosion2D 火花反馈。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level143Ctrl : MonoBehaviour
{
    private sealed class RopeRuntime
    {
        public Level5Anchor Anchor;
        public Level5TrackLine TrackLine;
        public LineRenderer LineRenderer;
        public EdgeCollider2D Collider;
        public Rigidbody2D AnchorBody;
        public SpringJoint2D SpringJoint;
        public Vector3 InitialAnchorPosition;
        public Vector2 InitialDoorDirection;
        public readonly Vector2[] ColliderPoints = new Vector2[2];
    }

    private enum PointerMode
    {
        None,
        Touch,
        Mouse,
    }

    private enum InteractionMode
    {
        None,
        DragAnchor,
        TestRope,
    }

    #region Inspector

    [Header("机关引用")]
    [Tooltip("Level005 结构中的五个图钉锚点；为空时只在本关子层级自动查找。")]
    [SerializeField] private List<Level5Anchor> anchors = new();

    [Tooltip("Level005 留下的刀光 Trail 节点；只在尝试切钢绳时显示。")]
    [SerializeField] private Transform dragTrailTarget;

    [Tooltip("悬挂门的物理根；本关固定为 Kinematic，拖图钉不会带动门体。")]
    [SerializeField] private Rigidbody2D doorBody;

    [Tooltip("复用 Level052 的 SparkExplosion2D 粒子 Prefab 根 ParticleSystem。")]
    [SerializeField] private ParticleSystem sparkEffectPrefab;

    [Tooltip("Level052 为火花 Glow 使用的 ADD 材质；只赋给本关运行时火花实例。")]
    [SerializeField] private Material sparkGlowMaterial;

    [Tooltip("玩法世界相机；为空时优先查找本关子相机，再回退 Camera.main。")]
    [SerializeField] private Camera mainCamera;

    [Header("钢绳碰撞")]
    [Tooltip("EdgeCollider2D 的圆角半径；总可站立厚度约为该值的两倍。")]
    [SerializeField, Min(0.02f)] private float ropeColliderRadius = 0.12f;

    [Header("角色阴影")]
    [Tooltip("角色脚底实际踩住任意钢绳时，临时隐藏角色自身的 Shadow 节点。")]
    [SerializeField] private bool hideActorShadowOnRope = true;

    [Tooltip("钢绳接触点位于角色碰撞盒底部区域时允许的高度误差。")]
    [SerializeField, Min(0f)] private float actorFootContactTolerance = 0.18f;

    [Header("图钉拖拽")]
    [Tooltip("无独立图钉 Collider 时使用的世界坐标拾取半径。")]
    [SerializeField, Min(0.1f)] private float anchorPickRadius = 0.65f;

    [Tooltip("图钉距离门端点的最短距离，避免把钢绳缩成零长度。")]
    [SerializeField, Min(0.1f)] private float minimumRopeLength = 0.75f;

    [Tooltip("图钉限制在相机视口内时预留的归一化边距。")]
    [SerializeField, Range(0f, 0.2f)] private float viewportPadding = 0.04f;

    [Header("切割失败反馈")]
    [Tooltip("相邻输入采样至少相隔的屏幕像素，避免静止点击持续触发。")]
    [SerializeField, Min(1f)] private float minimumPointerStepPixels = 4f;

    [Tooltip("滑动线段未严格相交时允许触发火花的世界距离。")]
    [SerializeField, Min(0f)] private float sparkContactRadius = 0.12f;

    [Tooltip("连续摩擦火花的最小时间间隔。")]
    [SerializeField, Min(0.02f)] private float sparkInterval = 0.08f;

    [Tooltip("火花池大小；按 Level052 粒子约 0.5 秒寿命覆盖连续摩擦。")]
    [SerializeField, Range(1, 12)] private int sparkPoolSize = 6;

    [Tooltip("摩擦音效的最小间隔，避免连续划线叠加过多音源。")]
    [SerializeField, Min(0.05f)] private float frictionSoundInterval = 0.18f;

    [Tooltip("刀光相对关卡根节点的 Z 偏移。")]
    [SerializeField] private float dragTrailDepthOffset = -0.6f;

    #endregion

    #region Runtime State

    private readonly List<RopeRuntime> ropes = new();
    private readonly List<ParticleSystem> sparkPool = new();
    private BaseLevelCtrl baseLevelCtrl;
    private ActorCtrl actor;
    private Collider2D actorCollider;
    private GameObject actorShadowObject;
    private TrailRenderer dragTrail;
    private EventSubscriber eventSubscriber;
    private PointerMode pointerMode;
    private InteractionMode interactionMode;
    private RopeRuntime draggedRope;
    private int activeFingerId = -1;
    private int nextSparkIndex;
    private Vector2 pointerToAnchorOffset;
    private Vector2 latestScreenPosition;
    private Vector2 latestWorldPosition;
    private float nextSparkTime;
    private float nextFrictionSoundTime;
    private bool initialized;
    private bool isPaused;
    private bool teardownStarted;
    private bool actorShadowActiveBeforeRope;
    private bool actorShadowSuppressedByRope;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        InitializeRuntime();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        InitializeRuntime();
        RegisterEvents();
    }

    private void Update()
    {
        if (!Application.isPlaying || !initialized || isPaused || teardownStarted)
            return;

        HandlePointerInput();
    }

    private void FixedUpdate()
    {
        if (!Application.isPlaying || !initialized || teardownStarted || isPaused ||
            !IsGameplayPlaying())
        {
            SetActorShadowSuppressedByRope(false);
            return;
        }

        RefreshActorShadowForRopes();
    }

    private void OnDisable()
    {
        EventsUtils.ResetEvents(ref eventSubscriber);
        if (!Application.isPlaying)
            return;

        ClearPointer();
        StopAllSparks();
        SetActorShadowSuppressedByRope(false);
    }

    private void OnDestroy()
    {
        EventsUtils.ResetEvents(ref eventSubscriber);
        SetActorShadowSuppressedByRope(false);
    }

    #endregion

    #region Initialization

    private void InitializeRuntime()
    {
        if (initialized || !Application.isPlaying)
            return;

        ResolveReferences();
        PrepareFixedDoor();
        CreateRopeColliders();
        ConfigureDragTrail();
        CreateSparkPool();

        initialized = true;
        isPaused = !IsGameplayPlaying();
    }

    private void ResolveReferences()
    {
        baseLevelCtrl = GetComponent<BaseLevelCtrl>();
        actor = baseLevelCtrl != null
            ? baseLevelCtrl.Actor
            : GetComponentInChildren<ActorCtrl>(true);
        if (actor != null)
        {
            actorCollider = actor.GetComponent<Collider2D>();
            Transform shadowTransform = actor.transform.Find("Shadow");
            actorShadowObject = shadowTransform != null ? shadowTransform.gameObject : null;
        }

        anchors ??= new List<Level5Anchor>();
        anchors.RemoveAll(anchor => anchor == null);
        if (anchors.Count == 0)
            anchors.AddRange(GetComponentsInChildren<Level5Anchor>(true));

        dragTrailTarget ??= transform.Find("DragTarget");
        dragTrail = dragTrailTarget != null
            ? dragTrailTarget.GetComponent<TrailRenderer>()
            : null;

        if (doorBody == null)
        {
            Transform doorRoot = transform.Find("Rigibody2D");
            if (doorRoot != null)
                doorBody = doorRoot.GetComponent<Rigidbody2D>();
        }

        mainCamera ??= GetComponentInChildren<Camera>(true);
        mainCamera ??= Camera.main;
    }

    private void PrepareFixedDoor()
    {
        if (doorBody == null)
            return;

        doorBody.simulated = false;
        if (doorBody.bodyType != RigidbodyType2D.Static)
        {
            doorBody.linearVelocity = Vector2.zero;
            doorBody.angularVelocity = 0f;
        }

        doorBody.bodyType = RigidbodyType2D.Kinematic;
        doorBody.gravityScale = 0f;
        doorBody.constraints = RigidbodyConstraints2D.FreezeAll;
        doorBody.simulated = true;
    }

    private void CreateRopeColliders()
    {
        ropes.Clear();
        // 钢绳需要参与角色落地，但不能成为 ShadowController 的投影接收面。
        // 本关 Actor 的 whatIsGround 单独包含 WithoutShadowGround，不改共享 Actor。
        int ropeLayer = LayerMask.NameToLayer("WithoutShadowGround");
        if (ropeLayer < 0)
            ropeLayer = LayerMask.NameToLayer("Stuff");

        for (int index = 0; index < anchors.Count; index++)
        {
            Level5Anchor anchor = anchors[index];
            if (anchor == null)
                continue;

            Level5TrackLine trackLine = anchor.TrackLine ??
                                        anchor.GetComponentInChildren<Level5TrackLine>(true);
            if (trackLine == null || trackLine.GetStartPoint() == null ||
                trackLine.GetEndPoint() == null)
                continue;

            Rigidbody2D anchorBody = anchor.GetComponent<Rigidbody2D>();
            if (anchorBody != null)
            {
                anchorBody.bodyType = RigidbodyType2D.Kinematic;
                anchorBody.gravityScale = 0f;
                anchorBody.linearVelocity = Vector2.zero;
                anchorBody.angularVelocity = 0f;
                anchorBody.simulated = true;
            }

            SpringJoint2D springJoint = anchor.GetComponent<SpringJoint2D>();
            if (springJoint != null)
                springJoint.enabled = false;

            GameObject lineObject = trackLine.gameObject;
            if (ropeLayer >= 0)
                lineObject.layer = ropeLayer;

            EdgeCollider2D edgeCollider = lineObject.GetComponent<EdgeCollider2D>();
            if (edgeCollider == null)
                edgeCollider = lineObject.AddComponent<EdgeCollider2D>();
            edgeCollider.isTrigger = false;
            edgeCollider.edgeRadius = Mathf.Max(0.02f, ropeColliderRadius);
            edgeCollider.enabled = true;

            Vector2 doorDirection = (Vector2)anchor.transform.position -
                                    (Vector2)trackLine.GetEndPoint().position;
            RopeRuntime runtime = new()
            {
                Anchor = anchor,
                TrackLine = trackLine,
                LineRenderer = lineObject.GetComponent<LineRenderer>(),
                Collider = edgeCollider,
                AnchorBody = anchorBody,
                SpringJoint = springJoint,
                InitialAnchorPosition = anchor.transform.position,
                InitialDoorDirection = doorDirection.sqrMagnitude > 0.0001f
                    ? doorDirection.normalized
                    : Vector2.left,
            };
            ropes.Add(runtime);
            RefreshRope(runtime);
        }

        Physics2D.SyncTransforms();
    }

    private void ConfigureDragTrail()
    {
        if (dragTrail == null)
            return;

        dragTrail.emitting = false;
        dragTrail.Clear();
        dragTrail.time = 0.18f;
        dragTrail.minVertexDistance = 0.035f;
        dragTrail.numCapVertices = 3;
        dragTrail.numCornerVertices = 2;
        dragTrail.sortingLayerID = SortingLayer.NameToID("SceneLayer");
        dragTrail.sortingOrder = 1000;
    }

    private void CreateSparkPool()
    {
        sparkPool.Clear();
        if (sparkEffectPrefab == null)
        {
            Debug.LogWarning("[Level143Ctrl] 未绑定 Level052 SparkExplosion2D，钢绳仍不可切但不会显示火花。", this);
            return;
        }

        int count = Mathf.Clamp(sparkPoolSize, 1, 12);
        for (int index = 0; index < count; index++)
        {
            ParticleSystem instance = Instantiate(sparkEffectPrefab, transform);
            instance.name = $"Level143_Spark_{index + 1}";
            ConfigureSparkInstance(instance);
            instance.gameObject.SetActive(false);
            sparkPool.Add(instance);
        }
    }

    private void ConfigureSparkInstance(ParticleSystem instance)
    {
        if (instance == null)
            return;

        ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int index = 0; index < systems.Length; index++)
        {
            ParticleSystem particle = systems[index];
            ParticleSystem.MainModule main = particle.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            if (particle == instance)
            {
                main.startSize = 0.15f;
                main.startSpeed = 25f;
                main.startLifetime = 0.5f;
                main.gravityModifier = 0f;

                ParticleSystem.EmissionModule emission = particle.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)8) });

                ParticleSystem.ColorOverLifetimeModule color = particle.colorOverLifetime;
                color.enabled = true;
                Gradient gradient = new();
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(new Color(1f, 0.88827735f, 0.10849059f), 0f),
                        new GradientColorKey(new Color(1f, 0.5147606f, 0.19339621f), 1f),
                    },
                    new[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(0f, 1f),
                    });
                color.color = gradient;
            }
            else if (string.Equals(particle.gameObject.name, "Glow", StringComparison.Ordinal))
            {
                main.startSize = 2f;
            }

            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        ParticleSystemRenderer[] renderers =
            instance.GetComponentsInChildren<ParticleSystemRenderer>(true);
        for (int index = 0; index < renderers.Length; index++)
        {
            ParticleSystemRenderer renderer = renderers[index];
            renderer.sortingLayerName = "SceneLayer";
            renderer.sortingOrder = 250;
            if (sparkGlowMaterial != null &&
                string.Equals(renderer.gameObject.name, "Glow", StringComparison.Ordinal))
            {
                renderer.sharedMaterial = sparkGlowMaterial;
            }
        }
    }

    #endregion

    #region Pointer Input

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
            else if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
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
        latestScreenPosition = screenPosition;
        latestWorldPosition = ScreenToWorld(screenPosition);

        draggedRope = FindAnchorAt(latestWorldPosition);
        if (draggedRope != null)
        {
            interactionMode = InteractionMode.DragAnchor;
            pointerToAnchorOffset = (Vector2)draggedRope.Anchor.transform.position - latestWorldPosition;
            StopDragTrail(false);
            return;
        }

        interactionMode = InteractionMode.TestRope;
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
        if (screenDelta.sqrMagnitude < minimumPointerStepPixels * minimumPointerStepPixels)
            return;

        Vector2 worldPosition = ScreenToWorld(screenPosition);
        if (interactionMode == InteractionMode.DragAnchor && draggedRope != null)
            DragAnchor(draggedRope, worldPosition + pointerToAnchorOffset);
        else if (interactionMode == InteractionMode.TestRope)
        {
            TryPlayRopeSpark(latestWorldPosition, worldPosition);
            UpdateDragTrail(worldPosition);
        }

        latestScreenPosition = screenPosition;
        latestWorldPosition = worldPosition;
    }

    private void EndPointer(Vector2 screenPosition, bool canceled)
    {
        if (pointerMode == PointerMode.None)
            return;

        if (!canceled)
            UpdatePointer(screenPosition);

        ClearPointer();
    }

    private void ClearPointer()
    {
        StopDragTrail(true);
        pointerMode = PointerMode.None;
        interactionMode = InteractionMode.None;
        activeFingerId = -1;
        draggedRope = null;
        pointerToAnchorOffset = Vector2.zero;
        latestScreenPosition = Vector2.zero;
        latestWorldPosition = Vector2.zero;
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

    private RopeRuntime FindAnchorAt(Vector2 worldPosition)
    {
        RopeRuntime nearest = null;
        float nearestSqrDistance = anchorPickRadius * anchorPickRadius;

        for (int index = 0; index < ropes.Count; index++)
        {
            RopeRuntime rope = ropes[index];
            if (rope?.Anchor == null)
                continue;

            float sqrDistance = ((Vector2)rope.Anchor.transform.position - worldPosition).sqrMagnitude;
            if (sqrDistance > nearestSqrDistance)
                continue;

            nearest = rope;
            nearestSqrDistance = sqrDistance;
        }

        return nearest;
    }

    private void DragAnchor(RopeRuntime rope, Vector2 targetPosition)
    {
        if (rope?.Anchor == null || rope.TrackLine == null)
            return;

        targetPosition = ClampToCameraViewport(targetPosition);
        Vector2 doorPosition = rope.TrackLine.GetEndPoint().position;
        Vector2 fromDoor = targetPosition - doorPosition;
        float minimumLength = Mathf.Max(0.1f, minimumRopeLength);
        if (fromDoor.sqrMagnitude < minimumLength * minimumLength)
        {
            Vector2 direction = fromDoor.sqrMagnitude > 0.0001f
                ? fromDoor.normalized
                : rope.InitialDoorDirection;
            targetPosition = doorPosition + direction * minimumLength;
        }

        if (rope.AnchorBody != null)
        {
            rope.AnchorBody.position = targetPosition;
            rope.AnchorBody.linearVelocity = Vector2.zero;
            rope.AnchorBody.angularVelocity = 0f;
        }
        else
        {
            Vector3 position = rope.Anchor.transform.position;
            position.x = targetPosition.x;
            position.y = targetPosition.y;
            rope.Anchor.transform.position = position;
        }

        RefreshRope(rope);
        Physics2D.SyncTransforms();
    }

    private Vector2 ClampToCameraViewport(Vector2 worldPosition)
    {
        Camera camera = mainCamera != null ? mainCamera : Camera.main;
        if (camera == null)
            return worldPosition;

        float distance = Mathf.Abs(camera.transform.position.z - transform.position.z);
        float padding = Mathf.Clamp(viewportPadding, 0f, 0.2f);
        Vector3 first = camera.ViewportToWorldPoint(new Vector3(padding, padding, distance));
        Vector3 second = camera.ViewportToWorldPoint(new Vector3(1f - padding, 1f - padding, distance));
        return new Vector2(
            Mathf.Clamp(worldPosition.x, Mathf.Min(first.x, second.x), Mathf.Max(first.x, second.x)),
            Mathf.Clamp(worldPosition.y, Mathf.Min(first.y, second.y), Mathf.Max(first.y, second.y)));
    }

    private void StartDragTrail(Vector2 worldPosition)
    {
        if (dragTrailTarget == null)
            return;

        PositionDragTrail(worldPosition);
        if (dragTrail == null)
            return;

        dragTrail.emitting = false;
        dragTrail.Clear();
        dragTrail.AddPosition(dragTrailTarget.position);
        dragTrail.emitting = true;
    }

    private void UpdateDragTrail(Vector2 worldPosition)
    {
        PositionDragTrail(worldPosition);
    }

    private void PositionDragTrail(Vector2 worldPosition)
    {
        if (dragTrailTarget == null)
            return;

        Vector3 position = dragTrailTarget.position;
        position.x = worldPosition.x;
        position.y = worldPosition.y;
        position.z = transform.position.z + dragTrailDepthOffset;
        dragTrailTarget.position = position;
    }

    private void StopDragTrail(bool clear)
    {
        if (dragTrail == null)
            return;

        dragTrail.emitting = false;
        if (clear)
            dragTrail.Clear();
    }

    #endregion

    #region Rope Collision And Spark Feedback

    private void RefreshRope(RopeRuntime rope)
    {
        if (rope?.TrackLine == null || rope.TrackLine.GetStartPoint() == null ||
            rope.TrackLine.GetEndPoint() == null)
            return;

        Transform lineTransform = rope.TrackLine.transform;
        Vector3 startLocal = lineTransform.InverseTransformPoint(rope.TrackLine.GetStartPoint().position);
        Vector3 endLocal = lineTransform.InverseTransformPoint(rope.TrackLine.GetEndPoint().position);

        if (rope.LineRenderer != null)
        {
            if (rope.LineRenderer.positionCount != 2)
                rope.LineRenderer.positionCount = 2;
            rope.LineRenderer.SetPosition(0, startLocal);
            rope.LineRenderer.SetPosition(1, endLocal);
        }

        if (rope.Collider != null)
        {
            rope.ColliderPoints[0] = startLocal;
            rope.ColliderPoints[1] = endLocal;
            rope.Collider.points = rope.ColliderPoints;
            rope.Collider.edgeRadius = Mathf.Max(0.02f, ropeColliderRadius);
        }
    }

    private void TryPlayRopeSpark(Vector2 gestureStart, Vector2 gestureEnd)
    {
        float now = Time.unscaledTime;
        if (now < nextSparkTime)
            return;

        bool found = false;
        float bestGestureTime = float.MaxValue;
        Vector2 bestPoint = Vector2.zero;

        for (int index = 0; index < ropes.Count; index++)
        {
            RopeRuntime rope = ropes[index];
            if (!TryGetRopeEndpoints(rope, out Vector2 ropeStart, out Vector2 ropeEnd) ||
                !TryResolveSparkPoint(gestureStart, gestureEnd, ropeStart, ropeEnd,
                    out Vector2 point, out float gestureTime))
                continue;

            if (found && gestureTime >= bestGestureTime)
                continue;

            found = true;
            bestGestureTime = gestureTime;
            bestPoint = point;
        }

        if (!found)
            return;

        nextSparkTime = now + Mathf.Max(0.02f, sparkInterval);
        PlaySpark(bestPoint);

        if (now >= nextFrictionSoundTime)
        {
            nextFrictionSoundTime = now + Mathf.Max(0.05f, frictionSoundInterval);
            AudioManager.PlaySound("frictional");
        }
    }

    private static bool TryGetRopeEndpoints(
        RopeRuntime rope,
        out Vector2 start,
        out Vector2 end)
    {
        start = Vector2.zero;
        end = Vector2.zero;
        if (rope?.TrackLine == null || rope.TrackLine.GetStartPoint() == null ||
            rope.TrackLine.GetEndPoint() == null)
            return false;

        start = rope.TrackLine.GetStartPoint().position;
        end = rope.TrackLine.GetEndPoint().position;
        return (end - start).sqrMagnitude > 0.0001f;
    }

    private bool TryResolveSparkPoint(
        Vector2 gestureStart,
        Vector2 gestureEnd,
        Vector2 ropeStart,
        Vector2 ropeEnd,
        out Vector2 point,
        out float gestureTime)
    {
        if (TryIntersectSegments(
                gestureStart, gestureEnd, ropeStart, ropeEnd,
                out point, out gestureTime))
            return true;

        point = ClosestPointOnSegment(gestureEnd, ropeStart, ropeEnd);
        float radius = Mathf.Max(0f, sparkContactRadius);
        if ((point - gestureEnd).sqrMagnitude > radius * radius)
        {
            gestureTime = 0f;
            return false;
        }

        gestureTime = 1f;
        return true;
    }

    private static bool TryIntersectSegments(
        Vector2 firstStart,
        Vector2 firstEnd,
        Vector2 secondStart,
        Vector2 secondEnd,
        out Vector2 point,
        out float firstTime)
    {
        point = Vector2.zero;
        firstTime = 0f;
        Vector2 firstDirection = firstEnd - firstStart;
        Vector2 secondDirection = secondEnd - secondStart;
        float denominator = Cross(firstDirection, secondDirection);
        if (Mathf.Abs(denominator) <= 0.00001f)
            return false;

        Vector2 startDelta = secondStart - firstStart;
        float firstParameter = Cross(startDelta, secondDirection) / denominator;
        float secondParameter = Cross(startDelta, firstDirection) / denominator;
        if (firstParameter < 0f || firstParameter > 1f ||
            secondParameter < 0f || secondParameter > 1f)
            return false;

        firstTime = firstParameter;
        point = firstStart + firstDirection * firstParameter;
        return true;
    }

    private static Vector2 ClosestPointOnSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;
        if (lengthSquared <= 0.00001f)
            return start;

        float time = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
        return start + segment * time;
    }

    private static float Cross(Vector2 first, Vector2 second)
    {
        return first.x * second.y - first.y * second.x;
    }

    private void PlaySpark(Vector2 worldPosition)
    {
        if (sparkPool.Count == 0)
            return;

        ParticleSystem particle = sparkPool[nextSparkIndex % sparkPool.Count];
        nextSparkIndex = (nextSparkIndex + 1) % sparkPool.Count;
        if (particle == null)
            return;

        particle.transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
        particle.gameObject.SetActive(true);
        particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particle.Play(true);
    }

    private void StopAllSparks()
    {
        for (int index = 0; index < sparkPool.Count; index++)
        {
            ParticleSystem particle = sparkPool[index];
            if (particle == null)
                continue;

            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.gameObject.SetActive(false);
        }
    }

    #endregion

    #region Gameplay State

    private void RefreshActorShadowForRopes()
    {
        SetActorShadowSuppressedByRope(IsActorStandingOnAnyRope());
    }

    private bool IsActorStandingOnAnyRope()
    {
        if (!hideActorShadowOnRope || actor == null || actor.IsDied ||
            actorCollider == null || !actorCollider.enabled || !actorCollider.gameObject.activeInHierarchy)
            return false;

        Bounds actorBounds = actorCollider.bounds;
        float footLimit = actorBounds.min.y + Mathf.Max(0f, actorFootContactTolerance);
        for (int index = 0; index < ropes.Count; index++)
        {
            EdgeCollider2D ropeCollider = ropes[index]?.Collider;
            if (ropeCollider == null || !ropeCollider.enabled ||
                !ropeCollider.gameObject.activeInHierarchy || !actorCollider.IsTouching(ropeCollider))
                continue;

            ColliderDistance2D distance = actorCollider.Distance(ropeCollider);
            if (!distance.isValid)
                continue;

            // 只有角色底部落在绳面上才隐藏；碰到绳端或从绳下方擦过不算“踩住”。
            if (distance.pointA.y <= footLimit &&
                actorBounds.center.y >= distance.pointB.y)
                return true;
        }

        return false;
    }

    private void SetActorShadowSuppressedByRope(bool suppressed)
    {
        if (actorShadowObject == null)
        {
            actorShadowSuppressedByRope = false;
            return;
        }

        if (suppressed)
        {
            if (!actorShadowSuppressedByRope)
                actorShadowActiveBeforeRope = actorShadowObject.activeSelf;

            actorShadowSuppressedByRope = true;
            if (actorShadowObject.activeSelf)
                actorShadowObject.SetActive(false);
            return;
        }

        if (!actorShadowSuppressedByRope)
            return;

        actorShadowObject.SetActive(actorShadowActiveBeforeRope);
        actorShadowSuppressedByRope = false;
    }

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
        if (evt == null || teardownStarted)
            return;

        isPaused = !string.Equals(evt.NewState, GamePlayStateName.PLAYING, StringComparison.Ordinal);
        if (isPaused)
        {
            ClearPointer();
            SetActorShadowSuppressedByRope(false);
        }
    }

    private void OnGamePause(GamePauseEvent _)
    {
        if (teardownStarted)
            return;

        isPaused = true;
        ClearPointer();
        SetActorShadowSuppressedByRope(false);
    }

    private void OnGameResume(GameResumeEvent _)
    {
        if (teardownStarted)
            return;

        // Resume 在 Pause.OnExit 内发送，此时状态机尚未切换到 PLAYING。
        // 先解除本地暂停标记，实际输入仍由 CanReceiveInput 校验玩法状态。
        isPaused = false;
    }

    private void OnGameReset(GameResetEvent _)
    {
        BeginTeardown();
    }

    private void OnGameReplay(GameReplayEvent _)
    {
        BeginTeardown();
    }

    private void BeginTeardown()
    {
        if (teardownStarted)
            return;

        teardownStarted = true;
        isPaused = true;
        ClearPointer();
        StopAllSparks();
        SetActorShadowSuppressedByRope(false);
        for (int index = 0; index < ropes.Count; index++)
        {
            if (ropes[index]?.Collider != null)
                ropes[index].Collider.enabled = false;
        }
    }

    private bool CanReceiveInput()
    {
        if (!initialized || isPaused || teardownStarted ||
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
        Vector3 world = camera.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, distance));
        return new Vector2(world.x, world.y);
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
