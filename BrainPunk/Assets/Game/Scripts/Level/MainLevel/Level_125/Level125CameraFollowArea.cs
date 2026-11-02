using Game.Play;
using UnityEngine;

/// <summary>
/// Level 125 专属独立轴相机跟随；只服务本关两屏与抬升区构图，禁止作为共享相机组件复用。
/// 初始及高位释放前的阶段锁住进关时的纵向构图；吊环返程上升并承载 Actor 时强制跟随 Y，
/// 高位释放后恢复普通纵向死区逻辑。横向参考 Level 130：零死区、本关前置偏移 4、0.12 秒平滑，并保留本关视野边界。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level125CameraFollowArea : MonoBehaviour
{
    private enum FollowMode
    {
        InitialLocked,
        HighRingRide,
        Normal
    }

    #region Inspector 配置

    [Header("Follow Control")]
    [SerializeField] private bool canFollow = true; // 是否允许本关相机继续跟随。
    [SerializeField] private bool strickY; // 是否把 Y 固定在相机初始位置。

    [Header("Target")]
    [SerializeField] private Transform actor; // 本关真实 Actor Transform。
    [SerializeField] private Level125GadgetCtrl gadget; // 本关吊环机关，用于切换相机阶段。

    [Header("Follow Area")]
    [SerializeField] private Vector2 followAreaCenter = new Vector2(0f, -20f); // 仅 Y 用于普通纵向死区，世界坐标；X 不参与跟随判断。
    [SerializeField] private Vector2 followAreaSize = new Vector2(0f, 44.27f); // 仅 Y 表示纵向死区高度；横向固定零死区。

    [Header("Camera")]
    [SerializeField] private Camera targetCamera; // 目标 GameCamera；为空时只按本关既有回退链查找。
    [SerializeField] private float followSmoothTime = 0.12f; // 仅纵向追随目标的 SmoothDamp 时间。
    [SerializeField] private float returnSmoothTime = 0.5f; // 纵向回到死区后返回初始高度的时间。
    [SerializeField] private Vector2 cameraOffset = new Vector2(4f, 0f); // 横向参考 Level 130，本关向右前置 4 个世界单位；Y 偏移保持原值。
    [SerializeField, Min(0f)] private float horizontalFollowSmoothTime = 0.12f; // 独立横向平滑，不受纵向阶段切换影响。

    [Header("Follow Range (Optional)")]
    [SerializeField] private CameraFollowRange followRange; // 正交相机最终边界；为空时不额外夹取。

    #endregion

    #region 运行时状态

    private Camera resolvedCamera;
    private Vector3 cameraInitialPosition;
    private float cameraVelocityY;
    private float horizontalVelocity;
    private bool isFollowing;
    private FollowMode followMode = FollowMode.InitialLocked;
    private float ringFollowStartY;
    private bool hasCameraInitialPosition;
    private bool cameraWarningLogged;

    #endregion

    #region 初始化与 Unity 消息

    private void Awake()
    {
        ResolveGadget();
        InitializeCamera();
    }

    private void Start()
    {
        ResolveGadget();
        InitializeCamera();
    }

    private void OnEnable()
    {
        horizontalVelocity = 0f;
    }

    private void OnDisable()
    {
        horizontalVelocity = 0f;
    }

    private void InitializeCamera()
    {
        if (resolvedCamera == null && targetCamera != null)
        {
            resolvedCamera = targetCamera;
        }
        else if (resolvedCamera == null)
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
            if (!hasCameraInitialPosition)
            {
                cameraInitialPosition = resolvedCamera.transform.position;
                hasCameraInitialPosition = true;
            }
        }
        else if (!cameraWarningLogged)
        {
            Debug.LogWarning("[Level125CameraFollowArea] Camera not found.");
            cameraWarningLogged = true;
        }
    }

    private void LateUpdate()
    {
        if (!canFollow || actor == null)
        {
            return;
        }

        // 关卡实例刚创建时若相机尚未可解析，在当前实例内继续重试，不依赖切换场景。
        if (resolvedCamera == null || !hasCameraInitialPosition)
            InitializeCamera();

        if (resolvedCamera == null || !hasCameraInitialPosition)
            return;

        ResolveGadget();
        FollowMode nextMode = ResolveFollowMode();
        if (nextMode != followMode)
        {
            cameraVelocityY = 0f;
            followMode = nextMode;
            if (nextMode == FollowMode.HighRingRide)
            {
                // 上升段只允许镜头向上，避免 Actor 在低位被抓住时先把开局镜头拉低。
                ringFollowStartY = Mathf.Max(
                    cameraInitialPosition.y,
                    resolvedCamera.transform.position.y);
            }
        }

        switch (followMode)
        {
            case FollowMode.HighRingRide:
                UpdateHighRingFollow();
                break;
            case FollowMode.Normal:
                UpdateNormalFollow();
                break;
            default:
                UpdateInitialLockedFollow();
                break;
        }

        resolvedCamera.transform.position = ClampToFollowRange(resolvedCamera.transform.position);
    }

    #endregion

    #region 阶段跟随计算

    private FollowMode ResolveFollowMode()
    {
        if (gadget == null)
        {
            // 绑定缺失时保留普通相机回退行为，不让关卡整体失去相机控制。
            return FollowMode.Normal;
        }

        if (gadget.IsHighRingRideActive)
        {
            return FollowMode.HighRingRide;
        }

        return gadget.HasCompletedHighRingRide
            ? FollowMode.Normal
            : FollowMode.InitialLocked;
    }

    private void UpdateInitialLockedFollow()
    {
        UpdateFollowingState(false);
        MoveCamera(cameraInitialPosition.y, false);
    }

    private void UpdateHighRingFollow()
    {
        // 返程搭乘阶段保留 Y 的上行起点下限，X 与其他阶段一样紧跟 Actor。
        UpdateFollowingState(true);
        float targetY = Mathf.Max(
            ringFollowStartY,
            actor.position.y + cameraOffset.y);
        MoveCamera(targetY, true);
    }

    private void UpdateNormalFollow()
    {
        bool shouldFollowY = !strickY && IsOutOfRangeY(actor.position.y);
        UpdateFollowingState(shouldFollowY);
        MoveCamera(
            shouldFollowY ? actor.position.y + cameraOffset.y : cameraInitialPosition.y,
            shouldFollowY);
    }

    private void UpdateFollowingState(bool following)
    {
        if (following == isFollowing)
        {
            return;
        }

        cameraVelocityY = 0f;
        isFollowing = following;
    }

    private void MoveCamera(float targetY, bool followingTarget)
    {
        Vector3 targetPosition = ClampToFollowRange(new Vector3(
            actor.position.x + cameraOffset.x,
            targetY,
            cameraInitialPosition.z));
        float smoothTime = followingTarget ? followSmoothTime : returnSmoothTime;

        // 与指定 dev130 worktree 的连续横向相机一致：先夹取目标，再独立平滑 X。
        targetPosition.x = horizontalFollowSmoothTime > 0f
            ? Mathf.SmoothDamp(
                resolvedCamera.transform.position.x,
                targetPosition.x,
                ref horizontalVelocity,
                horizontalFollowSmoothTime)
            : targetPosition.x;

        // 保持 125 原有纵向计算；横向速度不随纵向模式切换清零。
        if (smoothTime > 0f)
        {
            targetPosition.y = Mathf.SmoothDamp(
                resolvedCamera.transform.position.y,
                targetPosition.y,
                ref cameraVelocityY,
                smoothTime);
        }

        resolvedCamera.transform.position = targetPosition;
    }

    private bool IsOutOfRangeY(float actorY)
    {
        float minY = followAreaCenter.y - followAreaSize.y * 0.5f;
        float maxY = followAreaCenter.y + followAreaSize.y * 0.5f;

        return actorY < minY || actorY > maxY;
    }

    private Vector3 ClampToFollowRange(Vector3 position)
    {
        return followRange != null
            ? followRange.ClampOrthographic(resolvedCamera, position)
            : position;
    }

    private void ResolveGadget()
    {
        if (gadget == null)
        {
            gadget = GetComponentInChildren<Level125GadgetCtrl>(true);
        }
    }

    #endregion

    #region Editor 可视化

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(
            new Vector3(0f, followAreaCenter.y, 0f),
            new Vector3(0f, followAreaSize.y, 0.1f));
    }

    #endregion
}
