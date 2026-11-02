using Bear.EventSystem;
using Game.Events;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level118 关卡控制器：按有效起跳在蓝、黄机关组之间切换实体状态，并在进入指定触发区域后永久开启相机垂直跟随。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level118Ctrl : MonoBehaviour
{
    #region Inspector

    [Header("关卡引用")]
    /// <summary>本关玩家角色；只读取落地、死亡、击飞和刚体状态，不改共享角色规则。</summary>
    [SerializeField] private ActorCtrl actor;

    /// <summary>蓝色机关组；初始为完全不透明且碰撞生效。</summary>
    [SerializeField] private Transform group1;

    /// <summary>黄色机关组；初始为半透明且碰撞不生效。</summary>
    [SerializeField] private Transform group2;

    [Header("机关组状态")]
    /// <summary>虚体组统一使用的透明度；仅修改 SpriteRenderer Alpha，不改原有 RGB。</summary>
    [Range(0f, 1f)]
    [SerializeField] private float ghostAlpha = 0.3f;

    [Header("本关相机")]
    /// <summary>Level118 关卡相机；只在本关实例存活期间由本组件驱动。</summary>
    [SerializeField] private Camera targetCamera;

    /// <summary>沿用预制体已有的正交相机边界，防止跟随越出本关画面。</summary>
    [SerializeField] private CameraFollowRange followRange;

    /// <summary>预制体原有公共相机跟随；运行时停用，避免与本关的分段 Y 跟随互相写位置。</summary>
    [SerializeField] private CameraFollowArea legacyCameraFollowArea;

    /// <summary>相机开始水平跟随前的世界 X 安全区中心，保持 Level118 原预制体参数。</summary>
    [SerializeField] private float horizontalFollowCenterX = -4.62f;

    /// <summary>相机开始水平跟随前的世界 X 安全区宽度，保持 Level118 原预制体参数。</summary>
    [Min(0.01f)]
    [SerializeField] private float horizontalFollowWidth = 12f;

    /// <summary>相机跟随角色时使用的世界坐标偏移。</summary>
    [SerializeField] private Vector2 cameraOffset = Vector2.zero;

    /// <summary>相机跟随角色时的平滑时间，保持 Level118 原预制体手感。</summary>
    [Min(0f)]
    [SerializeField] private float followSmoothTime = 0.2f;

    /// <summary>角色回到水平安全区时，相机回到初始 X 的平滑时间。</summary>
    [Min(0f)]
    [SerializeField] private float returnSmoothTime = 0.5f;

    /// <summary>空中跳跃输入等待实际向上速度变化的最长时间；覆盖共享角色 0.25 秒跳跃缓冲窗口。</summary>
    [Min(0.05f)]
    [SerializeField] private float pendingJumpConfirmSeconds = 0.3f;

    #endregion

    #region Runtime State

    private EventSubscriber eventSubscriber;
    private SpriteRenderer[] group1Renderers;
    private SpriteRenderer[] group2Renderers;
    private Collider2D[] group1Colliders;
    private Collider2D[] group2Colliders;

    private bool group1IsSolid = true;
    private bool verticalFollowUnlocked;
    private bool pendingJumpConfirmation;
    private float pendingJumpDeadline;
    private float previousJumpVelocityY;
    private int lastToggleFrame = -1;

    private Vector3 cameraInitialPosition;
    private Vector3 cameraVelocity;
    private bool cameraInitialized;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        CacheGroupComponents();
        ApplyInitialGroupState();
        InitializeCamera();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        EventsUtils.ResetEvents(ref eventSubscriber);
        eventSubscriber.Subscribe<PlayerJumpEvent>(OnPlayerJump);
    }

    /// <summary>
    /// 绑定到本关 Trigger 的 OnTrigger2DHandle.OnEnter 动态事件；仅本关角色进入时永久解锁垂直跟随。
    /// 离开区域不重新锁定；编辑器态、组件停用或非本关角色的碰撞体调用无效，重建关卡后恢复未解锁状态。
    /// </summary>
    public void OnVerticalFollowTriggerEnter(Collider2D other)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || verticalFollowUnlocked || actor == null || other == null)
            return;

        if (other.attachedRigidbody == actor.RB && actor.RB != null)
            verticalFollowUnlocked = true;
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        ConfirmPendingJump();
        UpdateCameraPosition();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        EventsUtils.ResetEvents(ref eventSubscriber);
        pendingJumpConfirmation = false;
    }

    #endregion

    #region Setup

    private void CacheGroupComponents()
    {
        group1Renderers = group1 != null
            ? group1.GetComponentsInChildren<SpriteRenderer>(true)
            : System.Array.Empty<SpriteRenderer>();
        group2Renderers = group2 != null
            ? group2.GetComponentsInChildren<SpriteRenderer>(true)
            : System.Array.Empty<SpriteRenderer>();
        group1Colliders = group1 != null
            ? group1.GetComponentsInChildren<Collider2D>(true)
            : System.Array.Empty<Collider2D>();
        group2Colliders = group2 != null
            ? group2.GetComponentsInChildren<Collider2D>(true)
            : System.Array.Empty<Collider2D>();
    }

    private void ApplyInitialGroupState()
    {
        group1IsSolid = true;
        ApplyGroupState(group1Renderers, group1Colliders, true);
        ApplyGroupState(group2Renderers, group2Colliders, false);
    }

    private void InitializeCamera()
    {
        if (legacyCameraFollowArea != null)
            legacyCameraFollowArea.enabled = false;

        if (targetCamera == null)
            return;

        cameraInitialPosition = targetCamera.transform.position;
        cameraVelocity = Vector3.zero;
        cameraInitialized = true;
    }

    #endregion

    #region Jump And Group Switching

    private void OnPlayerJump(PlayerJumpEvent evt)
    {
        if (!CanConfirmJump())
            return;

        previousJumpVelocityY = actor.RB.linearVelocityY;
        pendingJumpDeadline = Time.time + Mathf.Max(0.05f, pendingJumpConfirmSeconds);
        pendingJumpConfirmation = true;
    }

    private bool CanConfirmJump()
    {
        return actor != null
               && actor.RB != null
               && !actor.IsDied
               && !actor.IsKnocked;
    }

    private void ConfirmPendingJump()
    {
        if (!pendingJumpConfirmation)
            return;

        if (!CanConfirmJump() || Time.time > pendingJumpDeadline)
        {
            pendingJumpConfirmation = false;
            return;
        }

        float currentVelocityY = actor.RB.linearVelocityY;
        bool beganRising = currentVelocityY > 0.25f
                           && currentVelocityY > previousJumpVelocityY + 0.1f;
        previousJumpVelocityY = currentVelocityY;

        if (!beganRising)
            return;

        pendingJumpConfirmation = false;
        ToggleGroupsOncePerFrame();
    }

    private void ToggleGroupsOncePerFrame()
    {
        if (lastToggleFrame == Time.frameCount)
            return;

        lastToggleFrame = Time.frameCount;
        group1IsSolid = !group1IsSolid;
        ApplyGroupState(group1Renderers, group1Colliders, group1IsSolid);
        ApplyGroupState(group2Renderers, group2Colliders, !group1IsSolid);
    }

    private void ApplyGroupState(
        SpriteRenderer[] renderers,
        Collider2D[] colliders,
        bool isSolid)
    {
        float alpha = isSolid ? 1f : Mathf.Clamp01(ghostAlpha);

        for (int index = 0; index < renderers.Length; index++)
        {
            SpriteRenderer spriteRenderer = renderers[index];
            if (spriteRenderer == null)
                continue;

            Color color = spriteRenderer.color;
            color.a = alpha;
            spriteRenderer.color = color;
        }

        for (int index = 0; index < colliders.Length; index++)
        {
            if (colliders[index] != null)
                colliders[index].enabled = isSolid;
        }
    }

    #endregion

    #region Camera Follow

    private void UpdateCameraPosition()
    {
        if (!cameraInitialized || targetCamera == null || actor == null)
            return;

        float halfWidth = Mathf.Max(0.01f, horizontalFollowWidth) * 0.5f;
        float actorX = actor.transform.position.x;
        bool shouldFollowX = actorX < horizontalFollowCenterX - halfWidth
                             || actorX > horizontalFollowCenterX + halfWidth;

        Vector3 targetPosition = new Vector3(
            shouldFollowX ? actorX + cameraOffset.x : cameraInitialPosition.x,
            verticalFollowUnlocked
                ? actor.transform.position.y + cameraOffset.y
                : cameraInitialPosition.y,
            cameraInitialPosition.z);

        if (followRange != null)
            targetPosition = followRange.ClampOrthographic(targetCamera, targetPosition);

        float smoothTime = shouldFollowX || verticalFollowUnlocked
            ? Mathf.Max(0f, followSmoothTime)
            : Mathf.Max(0f, returnSmoothTime);

        if (smoothTime > 0f)
        {
            targetCamera.transform.position = Vector3.SmoothDamp(
                targetCamera.transform.position,
                targetPosition,
                ref cameraVelocity,
                smoothTime);
        }
        else
        {
            targetCamera.transform.position = targetPosition;
        }

        if (followRange != null)
        {
            targetCamera.transform.position = followRange.ClampOrthographic(
                targetCamera,
                targetCamera.transform.position);
        }
    }

    #endregion
}
