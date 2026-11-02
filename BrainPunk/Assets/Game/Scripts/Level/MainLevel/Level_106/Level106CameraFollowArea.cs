using Game.Play;
using UnityEngine;

/// <summary>
/// Level 106 专属独立轴相机跟随；只服务本关竖直送层与登杆横向 profile，禁止作为共享相机组件复用。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public sealed class Level106CameraFollowArea : MonoBehaviour
{
    #region Inspector 配置

    [Header("Follow Control")]
    [SerializeField] private bool canFollow = true; // 是否允许本关相机继续跟随。
    [SerializeField] private bool strickX; // 是否把 X 固定在相机初始位置。
    [SerializeField] private bool strickY; // 是否把 Y 固定在相机初始位置。

    [Header("Target")]
    [SerializeField] private Transform actor; // 本关真实 Actor Transform。

    [Header("Follow Area")]
    [SerializeField] private Vector2 followAreaCenter = Vector2.zero; // 当前死区中心，使用世界坐标。
    [SerializeField] private Vector2 followAreaSize = new Vector2(10f, 5f); // 当前死区尺寸，使用世界单位。

    [Header("Camera")]
    [SerializeField] private Camera targetCamera; // 目标 GameCamera；为空时只按本关既有回退链查找。
    [SerializeField] private float followSmoothTime = 0.3f; // 越界后追随目标的 SmoothDamp 时间。
    [SerializeField] private float returnSmoothTime = 0.5f; // 双轴回到死区后返回初始构图的时间。
    [SerializeField] private Vector2 cameraOffset = new Vector2(0f, 5f); // Actor 越界轴上的目标偏移。

    [Header("Follow Range (Optional)")]
    [SerializeField] private CameraFollowRange followRange; // 正交相机最终边界；为空时不额外夹取。

    #endregion

    #region 运行时状态

    private Camera resolvedCamera;
    private Vector3 cameraInitialPosition;
    private Vector3 cameraVelocity;
    private bool isFollowing;
    private float worldOffsetY;
    private Transform ascentUfo;
    private float ascentCameraOffsetY;

    #endregion

    #region 初始化与 Unity 消息

    private void Start()
    {
        InitializeCamera();
    }

    private void InitializeCamera()
    {
        if (targetCamera != null)
        {
            resolvedCamera = targetCamera;
        }
        else
        {
            if (PlayCtrl.Instance != null && PlayCtrl.Instance.LevelCtrl != null)
            {
                Transform cameraTransform = PlayCtrl.Instance.LevelCtrl.transform.Find("GameCamera");
                if (cameraTransform != null)
                {
                    resolvedCamera = cameraTransform.GetComponent<Camera>();
                }
            }

            if (resolvedCamera == null)
            {
                resolvedCamera = Camera.main;
                if (resolvedCamera == null)
                {
                    resolvedCamera = FindFirstObjectByType<Camera>();
                }
            }
        }

        if (resolvedCamera != null)
        {
            cameraInitialPosition = resolvedCamera.transform.position;
        }
        else
        {
            Debug.LogWarning("[Level106CameraFollowArea] Camera not found.");
        }
    }

    private void LateUpdate()
    {
        if (!canFollow || actor == null || resolvedCamera == null)
        {
            return;
        }

        ResolveOutOfRangeAxes(actor.position, out bool outOfRangeX, out bool outOfRangeY);
        bool shouldFollow = outOfRangeX || outOfRangeY;
        if (shouldFollow != isFollowing)
        {
            cameraVelocity = Vector3.zero;
            isFollowing = shouldFollow;
        }

        if (isFollowing)
        {
            Vector3 targetPosition = new Vector3(
                strickX || !outOfRangeX ? cameraInitialPosition.x : actor.position.x + cameraOffset.x,
                strickY || !outOfRangeY ? cameraInitialPosition.y : actor.position.y + cameraOffset.y,
                cameraInitialPosition.z);
            targetPosition = ClampToFollowRange(targetPosition);

            if (followSmoothTime > 0f)
            {
                resolvedCamera.transform.position = Vector3.SmoothDamp(
                    resolvedCamera.transform.position,
                    targetPosition,
                    ref cameraVelocity,
                    followSmoothTime);
            }
            else
            {
                resolvedCamera.transform.position = targetPosition;
            }
        }
        else
        {
            Vector3 targetPosition = ClampToFollowRange(cameraInitialPosition);
            resolvedCamera.transform.position = Vector3.SmoothDamp(
                resolvedCamera.transform.position,
                targetPosition,
                ref cameraVelocity,
                returnSmoothTime);
        }

        SyncAscentCameraY();
        resolvedCamera.transform.position = ClampToFollowRange(resolvedCamera.transform.position);
    }

    #endregion

    #region 独立轴跟随计算

    private void ResolveOutOfRangeAxes(
        Vector3 actorPosition,
        out bool outOfRangeX,
        out bool outOfRangeY)
    {
        float minX = followAreaCenter.x - followAreaSize.x * 0.5f;
        float maxX = followAreaCenter.x + followAreaSize.x * 0.5f;
        float minY = followAreaCenter.y - followAreaSize.y * 0.5f;
        float maxY = followAreaCenter.y + followAreaSize.y * 0.5f;

        outOfRangeX = actorPosition.x < minX || actorPosition.x > maxX;
        outOfRangeY = actorPosition.y < minY || actorPosition.y > maxY;
    }

    private Vector3 ClampToFollowRange(Vector3 position)
    {
        if (followRange == null)
            return position;

        Vector3 result = followRange.ClampOrthographic(resolvedCamera, position);
        if (worldOffsetY != 0f && resolvedCamera != null && resolvedCamera.orthographic)
        {
            Rect boundary = followRange.GetBoundaryRect();
            result.y = Mathf.Clamp(
                position.y,
                boundary.yMin + worldOffsetY + resolvedCamera.orthographicSize,
                boundary.yMax - resolvedCamera.orthographicSize);
        }

        return result;
    }

    #endregion

    #region 本关公开入口

    /// <summary>
    /// 启停本关相机跟随；终局或击飞阶段调用时不重置相机位置和死区 profile。
    /// </summary>
    public void SetCanFollow(bool enabled)
    {
        canFollow = enabled;
    }

    /// <summary>飞升起步保留当前构图，此后 Y 直接同步 UFO 显示 Transform；X 仍走原 profile。</summary>
    public void BeginAscentFollow(Transform ufo)
    {
        if (resolvedCamera == null)
            InitializeCamera();
        if (ufo == null || resolvedCamera == null)
            return;

        ascentUfo = ufo;
        ascentCameraOffsetY = resolvedCamera.transform.position.y - ufo.position.y;
        cameraVelocity.y = 0f;
    }

    public void EndAscentFollow(bool syncFinalPosition = false)
    {
        // Tween 完成回调早于 LateUpdate；正常到顶先消费最后一帧 UFO 位移。
        if (syncFinalPosition)
            SyncAscentCameraY();
        ascentUfo = null;
        ascentCameraOffsetY = 0f;
        cameraVelocity.y = 0f;
    }

    private void SyncAscentCameraY()
    {
        if (ascentUfo == null || resolvedCamera == null)
            return;

        Vector3 position = resolvedCamera.transform.position;
        position.y = ascentUfo.position.y + ascentCameraOffsetY;
        resolvedCamera.transform.position = ClampToFollowRange(position);
        cameraVelocity.y = 0f;
    }

    private void OnDisable()
    {
        EndAscentFollow();
    }

    /// <summary>
    /// 切换本关死区 profile；中心和尺寸均为世界空间值，调用本身不瞬移相机。
    /// </summary>
    public void SetFollowArea(Vector2 center, Vector2 size)
    {
        followAreaCenter = center + Vector2.up * worldOffsetY;
        followAreaSize = size;
    }

    public void ApplyWorldShift(float deltaY)
    {
        if (resolvedCamera == null)
            InitializeCamera();

        worldOffsetY += deltaY;
        cameraInitialPosition.y += deltaY;
        followAreaCenter.y += deltaY;
        if (resolvedCamera != null)
            resolvedCamera.transform.position += Vector3.up * deltaY;
    }

    #endregion

    #region Editor 可视化

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(
            new Vector3(followAreaCenter.x, followAreaCenter.y, 0f),
            new Vector3(followAreaSize.x, followAreaSize.y, 0.1f));
    }

    #endregion
}
