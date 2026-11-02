using UnityEngine;

/// <summary>
/// Level112 误导拉环：接受场景拖拽下拉，松手后以发条节奏缓慢回卷；Actor 接触后挂环升空。
/// 镜头、缩放和背景渐暗只在 Actor 已挂载时推进，避免玩家仅试拉一次就丢失主场景构图。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class Level112RingCtrl : MonoBehaviour
{
    private enum RingPhase
    {
        Idle,
        Dragging,
        Returning,
        Rewinding
    }

    #region Inspector 配置

    [Header("拖拽与行程")]
    [SerializeField] private DragEventsListener dragInput; // 拉环自身的鼠标/触摸输入，只允许纵向下拉。
    [SerializeField] private Camera targetCamera; // 本关 GameCamera；挂环后仅由本组件纵向跟随。
    [SerializeField] private float lowestCenterY = -0.6f; // 比上一版再低 0.25，环底仍略高于站立 Actor，保留起跳要求。
    [SerializeField, Min(0.01f)] private float minimumPullDistance = 0.35f; // 小于该距离的误触只回到初始位，不启动长行程。
    [SerializeField, Min(0.01f)] private float visualTransitionDistance = 48f; // 仅控制夜色/拉远过渡距离，不限制上升高度。
    [SerializeField, Min(0.01f)] private float riseSpeed = 1.8f; // 回卷基础速度，保持长时间远离感。
    [SerializeField, Min(0.01f)] private float initialRiseSpeed = 0.45f; // 松手后的起步速度，给玩家留出起跳挂环窗口。
    [SerializeField, Min(0.01f)] private float riseAccelerationDuration = 3f; // 在该时长内平滑提速到 riseSpeed。
    [SerializeField, Min(0.01f)] private float returnSpeed = 3.5f; // 未达到有效下拉阈值时的短距离复位速度。
    [SerializeField, Range(0f, 0.8f)] private float ratchetStrength = 0.12f; // 只保留轻微速度起伏，避免高频脉冲形成抖动。
    [SerializeField, Min(0.1f)] private float ratchetFrequency = 1.2f; // 低频发条节奏，不创建 Tween 或临时对象。

    [Header("Actor 挂载")]
    [SerializeField] private Vector2 actorHangOffset = new Vector2(0f, -1.25f); // 对齐 Level125 的 hang 相对环心偏移。

    [Header("远离镜头与夜色")]
    [SerializeField] private SpriteRenderer backgroundRenderer; // CommonBg/Bg 保持日间纸纹与世界位置不变。
    [SerializeField] private SpriteRenderer nightOverlayRenderer; // 独立叠加夜色，避免把底图横纹直接乘没。
    [SerializeField, Range(0f, 1f)] private float nightOverlayAlpha = 0.78f;
    [SerializeField] private Color nightColor = new Color(0.24313726f, 0.24705882f, 0.4627451f, 1f); // 沿用现有关卡夜色调。
    [SerializeField, Min(0f)] private float cameraYOffset = 1.5f; // 挂环 Actor 在镜头中的纵向构图偏移。
    [SerializeField, Min(0.01f)] private float cameraSmoothTime = 0.18f; // 镜头纵向追随的 SmoothDamp 时间。
    [SerializeField, Min(0.1f)] private float farOrthographicSize = 8.5f; // 随距离轻微拉远，强化角色越来越远的观感。
    [SerializeField, Min(50f)] private float backgroundPanelHeight = 160f; // 运行时强制三层背景达到该高度，避免嵌套 Prefab 覆盖未生效时横纹在 Y=25 截断。
    [SerializeField, Min(14.4f)] private float backgroundLoopStep = 72f; // 五个横纹贴图周期，分段回收时纹理相位不跳。

    #endregion

    #region 运行时状态

    private RingPhase phase;
    private Level112ActorCtrl carriedActor;
    private Rigidbody2D carriedBody;
    private RigidbodyType2D carriedOriginalBodyType;
    private bool carriedOriginalSimulated;
    private float restCenterX;
    private float restCenterY;
    private float dragBeginRingY;
    private float dragBeginWorldY;
    private float currentCenterY;
    private float rewindTime;
    private float actorAttachRingY;
    private float actorAttachViewportX;
    private Vector3 cameraInitialPosition;
    private float cameraVerticalVelocity;
    private float cameraInitialOrthographicSize;
    private Color dayColor;
    private Transform backgroundRoot;
    private Transform nightOverlayRoot;
    private SpriteRenderer paperLineRenderer;
    private bool paperLineRendererResolved;
    private Vector3 backgroundInitialPosition;
    private Vector3 nightOverlayInitialPosition;

    #endregion

    #region Unity 生命周期

    private void Awake()
    {
        if (dragInput == null)
            dragInput = GetComponent<DragEventsListener>();
        if (targetCamera == null)
            targetCamera = Camera.main;

        restCenterX = transform.position.x;
        restCenterY = transform.position.y;
        currentCenterY = restCenterY;
        phase = RingPhase.Idle;

        if (targetCamera != null)
        {
            cameraInitialPosition = targetCamera.transform.position;
            cameraInitialOrthographicSize = targetCamera.orthographicSize;
        }

        if (backgroundRenderer != null)
        {
            dayColor = backgroundRenderer.color;
            backgroundRoot = backgroundRenderer.transform.parent;
            if (backgroundRoot != null)
                backgroundInitialPosition = backgroundRoot.position;
        }

        if (nightOverlayRenderer != null)
        {
            nightOverlayRoot = nightOverlayRenderer.transform;
            nightOverlayInitialPosition = nightOverlayRoot.position;
        }

        EnsureBackgroundCoverage();
        SetNightOverlayProgress(0f);
    }

    private void OnEnable()
    {
        if (dragInput == null)
            return;

        dragInput.BeginDragHandlers += OnBeginDrag;
        dragInput.DragHandlers += OnDrag;
        dragInput.EndDragHandlers += OnEndDrag;
    }

    private void OnDisable()
    {
        if (dragInput != null)
        {
            dragInput.BeginDragHandlers -= OnBeginDrag;
            dragInput.DragHandlers -= OnDrag;
            dragInput.EndDragHandlers -= OnEndDrag;
        }

        DetachActor();
        RestoreVisualState();
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;
        switch (phase)
        {
            case RingPhase.Returning:
                StepShortReturn(deltaTime);
                break;
            case RingPhase.Rewinding:
                StepRewinding(deltaTime);
                break;
        }

        if (carriedActor != null && carriedActor.IsDied)
            DetachActor();
    }

    private void LateUpdate()
    {
        if (carriedActor == null || targetCamera == null)
            return;

        ApplyCarriedActorPose();

        float targetY = Mathf.Max(
            cameraInitialPosition.y,
            carriedActor.transform.position.y + cameraYOffset);
        float cameraY = Mathf.SmoothDamp(
            targetCamera.transform.position.y,
            targetY,
            ref cameraVerticalVelocity,
            cameraSmoothTime);
        targetCamera.transform.position = new Vector3(
            ResolveViewportAnchoredCameraX(),
            cameraY,
            cameraInitialPosition.z);
        LoopBackgrounds(cameraY);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (phase != RingPhase.Rewinding || carriedActor != null || other == null)
            return;

        Level112ActorCtrl actor = other.GetComponentInParent<Level112ActorCtrl>();
        if (actor == null || actor.IsDied || !actor.CompareTag("Player"))
            return;

        AttachActor(actor);
    }

    #endregion

    #region 拉环拖拽与发条回卷

    private void OnBeginDrag(Transform _)
    {
        if (phase != RingPhase.Idle || dragInput == null)
            return;

        phase = RingPhase.Dragging;
        dragBeginRingY = transform.position.y;
        dragBeginWorldY = ScreenToWorldY(dragInput.BeginScreenPosition);
    }

    private void OnDrag(Transform _)
    {
        if (phase != RingPhase.Dragging || dragInput == null)
            return;

        float pointerWorldY = ScreenToWorldY(dragInput.CurrentScreenPosition);
        float desiredY = dragBeginRingY + pointerWorldY - dragBeginWorldY;
        currentCenterY = Mathf.Clamp(desiredY, lowestCenterY, restCenterY);
        ApplyRingPosition();
    }

    private void OnEndDrag(Transform _)
    {
        if (phase != RingPhase.Dragging)
            return;

        float pullDistance = restCenterY - currentCenterY;
        if (pullDistance < minimumPullDistance)
        {
            phase = RingPhase.Returning;
            return;
        }

        rewindTime = 0f;
        phase = RingPhase.Rewinding;
        dragInput?.SetEnableDrag(false);
    }

    private void StepShortReturn(float deltaTime)
    {
        currentCenterY = Mathf.MoveTowards(
            currentCenterY,
            restCenterY,
            returnSpeed * deltaTime);
        ApplyRingPosition();
        if (Mathf.Approximately(currentCenterY, restCenterY))
            phase = RingPhase.Idle;
    }

    private void StepRewinding(float deltaTime)
    {
        rewindTime += deltaTime;
        float pulse = 0.5f + 0.5f * Mathf.Sin(
            rewindTime * ratchetFrequency * Mathf.PI * 2f);
        float speedMultiplier = Mathf.Lerp(1f - ratchetStrength, 1f, pulse);
        float accelerationProgress = Mathf.SmoothStep(
            0f,
            1f,
            rewindTime / Mathf.Max(0.01f, riseAccelerationDuration));
        float currentRiseSpeed = Mathf.Lerp(
            Mathf.Min(initialRiseSpeed, riseSpeed),
            riseSpeed,
            accelerationProgress);
        currentCenterY += currentRiseSpeed * speedMultiplier * deltaTime;
        ApplyRingPosition();

        ApplyCarriedActorPose();

        UpdateRideVisuals();
    }

    private void ApplyRingPosition()
    {
        Vector3 position = transform.position;
        position.x = restCenterX;
        position.y = currentCenterY;
        transform.position = position;
    }

    private float ScreenToWorldY(Vector2 screenPosition)
    {
        if (targetCamera == null)
            return transform.position.y;

        float depth = Mathf.Abs(targetCamera.transform.position.z - transform.position.z);
        return targetCamera.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, depth)).y;
    }

    #endregion

    #region Actor、镜头与夜色

    private void AttachActor(Level112ActorCtrl actor)
    {
        carriedActor = actor;
        carriedBody = actor.RB;
        actorAttachRingY = currentCenterY;
        actorAttachViewportX = targetCamera != null
            ? targetCamera.WorldToViewportPoint(transform.position).x
            : 0.5f;
        cameraVerticalVelocity = 0f;
        actor.StopMoving();

        if (carriedBody != null)
        {
            carriedOriginalBodyType = carriedBody.bodyType;
            carriedOriginalSimulated = carriedBody.simulated;
            carriedBody.linearVelocity = Vector2.zero;
            carriedBody.angularVelocity = 0f;
            carriedBody.bodyType = RigidbodyType2D.Kinematic;
            carriedBody.simulated = false;
        }

        actor.BeginHangAnimation();
        ApplyCarriedActorPose();
    }

    private void DetachActor()
    {
        if (carriedActor == null)
            return;

        Level112ActorCtrl actor = carriedActor;
        if (carriedBody != null)
        {
            carriedBody.simulated = false;
            carriedBody.bodyType = carriedOriginalBodyType;
            carriedBody.simulated = carriedOriginalSimulated;
            carriedBody.linearVelocity = Vector2.zero;
            carriedBody.angularVelocity = 0f;
            if (carriedOriginalSimulated)
                carriedBody.WakeUp();
        }

        actor.RestoreDefaultAnimation();
        if (!actor.IsDied)
            actor.ResumeMoving();
        carriedActor = null;
        carriedBody = null;
    }

    private void ApplyCarriedActorPose()
    {
        if (carriedActor == null)
            return;

        carriedActor.transform.position = transform.position + (Vector3)actorHangOffset;
    }

    private float ResolveViewportAnchoredCameraX()
    {
        if (targetCamera == null || !targetCamera.orthographic)
            return cameraInitialPosition.x;

        float halfWidth = targetCamera.orthographicSize * targetCamera.aspect;
        float viewportOffset = (actorAttachViewportX - 0.5f) * 2f;
        return transform.position.x - viewportOffset * halfWidth;
    }

    private void UpdateRideVisuals()
    {
        if (carriedActor == null)
            return;

        float progress = Mathf.Clamp01(
            (currentCenterY - actorAttachRingY) / Mathf.Max(0.01f, visualTransitionDistance));
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);

        if (nightOverlayRenderer != null)
            SetNightOverlayProgress(easedProgress);
        else if (backgroundRenderer != null)
            backgroundRenderer.color = Color.Lerp(dayColor, nightColor, easedProgress);
        if (targetCamera != null)
        {
            targetCamera.orthographicSize = Mathf.Lerp(
                cameraInitialOrthographicSize,
                Mathf.Max(cameraInitialOrthographicSize, farOrthographicSize),
                easedProgress);
        }
    }

    private void RestoreVisualState()
    {
        if (backgroundRenderer != null)
            backgroundRenderer.color = dayColor;
        SetNightOverlayProgress(0f);
        if (backgroundRoot != null)
            backgroundRoot.position = backgroundInitialPosition;
        if (nightOverlayRoot != null)
            nightOverlayRoot.position = nightOverlayInitialPosition;
        if (targetCamera != null)
        {
            targetCamera.transform.position = cameraInitialPosition;
            targetCamera.orthographicSize = cameraInitialOrthographicSize;
        }
    }

    private void SetNightOverlayProgress(float progress)
    {
        if (nightOverlayRenderer == null)
            return;

        Color overlayColor = nightColor;
        overlayColor.a = Mathf.Clamp01(progress) * nightOverlayAlpha;
        nightOverlayRenderer.color = overlayColor;
        nightOverlayRenderer.enabled = overlayColor.a > 0f;
    }

    private void LoopBackgrounds(float cameraY)
    {
        EnsureBackgroundCoverage();
        if (backgroundRoot == null)
            return;

        float step = Mathf.Max(14.4f, backgroundLoopStep);
        float recycleThreshold = step * 0.5f;
        if (cameraY - backgroundRoot.position.y <= recycleThreshold)
            return;

        int stepCount = Mathf.FloorToInt(
            (cameraY - backgroundRoot.position.y + recycleThreshold) / step);
        float offsetY = stepCount * step;
        backgroundRoot.position += Vector3.up * offsetY;
        if (nightOverlayRoot != null)
            nightOverlayRoot.position += Vector3.up * offsetY;
    }

    private void EnsureBackgroundCoverage()
    {
        EnsureRendererHeight(backgroundRenderer);
        EnsureRendererHeight(nightOverlayRenderer);

        if (!paperLineRendererResolved)
        {
            paperLineRendererResolved = true;
            if (backgroundRoot != null)
            {
                SpriteRenderer[] renderers = backgroundRoot.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (SpriteRenderer candidate in renderers)
                {
                    Material material = candidate != null ? candidate.sharedMaterial : null;
                    if (candidate == backgroundRenderer || material == null || material.shader == null)
                        continue;
                    if (material.shader.name != "Game/TxtlineRender")
                        continue;

                    paperLineRenderer = candidate;
                    break;
                }
            }
        }

        EnsureRendererHeight(paperLineRenderer);
    }

    private void EnsureRendererHeight(SpriteRenderer renderer)
    {
        if (renderer == null)
            return;

        Vector2 size = renderer.size;
        float requiredHeight = Mathf.Max(50f, backgroundPanelHeight);
        if (size.y >= requiredHeight)
            return;

        size.y = requiredHeight;
        renderer.size = size;
    }

    #endregion
}
