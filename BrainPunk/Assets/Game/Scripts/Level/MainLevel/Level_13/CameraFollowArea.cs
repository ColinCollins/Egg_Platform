using Game.Play;
using UnityEngine;

public class CameraFollowArea : MonoBehaviour
{
    [Header("跟随控制")]
    [SerializeField] private bool canFollow = true; // 是否允许跟随
    [SerializeField] private bool strickX = false; // 阻止x 变化
    [SerializeField] private bool strickY = false; // 组织y变化

    [Header("目标设置")]
    [SerializeField] private Transform actor; // 要跟随的目标（角色）

    [Header("范围设置")]
    [SerializeField] private Vector2 followAreaCenter = Vector2.zero; // 跟随区域中心点（世界坐标）
    [SerializeField] private Vector2 followAreaSize = new Vector2(10f, 5f); // 跟随区域大小（世界坐标）

    [Header("相机设置")]
    [SerializeField] private Camera targetCamera; // 目标相机（为空则自动查找）
    [SerializeField] private float followSmoothTime = 0.3f; // 跟随平滑时间
    [SerializeField] private float returnSmoothTime = 0.5f; // 返回初始位置的平滑时间

    [SerializeField] private Vector2 cameraOffset = new Vector2(0f, 5f); // 跟随区域大小（世界坐标）

    [Header("Follow Range (Optional)")]
    [SerializeField] private CameraFollowRange followRange; // 限制相机移动范围（正交相机）

    private Camera camera;
    private Vector3 cameraInitialPosition; // 相机初始位置
    private Vector3 cameraVelocity; // 相机平滑移动速度
    private bool isFollowing = false; // 当前是否在跟随

    void Start()
    {
        InitializeCamera();
    }

    /// <summary>
    /// 初始化相机
    /// </summary>
    private void InitializeCamera()
    {
        // 获取相机
        if (targetCamera == null)
        {
            // 尝试从 LevelCtrl 中查找
            if (PlayCtrl.Instance != null && PlayCtrl.Instance.LevelCtrl != null)
            {
                Transform cameraTransform = PlayCtrl.Instance.LevelCtrl.transform.Find("GameCamera");
                if (cameraTransform != null)
                {
                    camera = cameraTransform.GetComponent<Camera>();
                }
            }

            // 如果还没找到，使用主相机
            if (camera == null)
            {
                camera = Camera.main;
                if (camera == null)
                {
                    camera = FindFirstObjectByType<Camera>();
                }
            }
        }
        else
        {
            camera = targetCamera;
        }

        // 记录相机初始位置
        if (camera != null)
        {
            cameraInitialPosition = camera.transform.position;
        }
        else
        {
            Debug.LogWarning("[Level13Ctrl] Camera not found!");
        }
    }

    void LateUpdate()
    {
        if (!canFollow || actor == null || camera == null)
            return;

        // 检测 Actor 是否在跟随范围内
        bool shouldFollow = IsActorOutOfRange(actor.position);

        // 如果状态改变，重置速度以实现平滑过渡
        if (shouldFollow != isFollowing)
        {
            cameraVelocity = Vector3.zero;
            isFollowing = shouldFollow;
        }

        // 根据状态更新相机位置（在 LateUpdate 中每帧更新，避免与 FixedUpdate 不同步导致的残影/闪烁）
        if (isFollowing)
        {
            // 跟随 Actor（只跟随 X 和 Y，保持 Z）
            Vector3 targetPosition = new Vector3(
                strickX ? cameraInitialPosition.x : actor.position.x + cameraOffset.x,
                strickY ? cameraInitialPosition.y : actor.position.y + cameraOffset.y,
                cameraInitialPosition.z
            );

            if (followRange != null)
            {
                targetPosition = followRange.ClampOrthographic(camera, targetPosition);
            }

            if (followSmoothTime > 0) 
                camera.transform.position = Vector3.SmoothDamp(
                    camera.transform.position,
                    targetPosition,
                    ref cameraVelocity,
                    followSmoothTime
                );
            else
            {
                camera.transform.position = targetPosition;
            }

            if (followRange != null)
            {
                camera.transform.position = followRange.ClampOrthographic(camera, camera.transform.position);
            }
        }
        else
        {
            Vector3 targetPosition = cameraInitialPosition;
            if (followRange != null)
            {
                targetPosition = followRange.ClampOrthographic(camera, targetPosition);
            }

            // 平滑返回初始位置
            camera.transform.position = Vector3.SmoothDamp(
                camera.transform.position,
                targetPosition,
                ref cameraVelocity,
                returnSmoothTime
            );

            if (followRange != null)
            {
                camera.transform.position = followRange.ClampOrthographic(camera, camera.transform.position);
            }
        }
    }

    /// <summary>
    /// 检测 Actor 是否超出跟随范围
    /// </summary>
    private bool IsActorOutOfRange(Vector3 actorPosition)
    {
        // 计算跟随区域的边界
        float minX = followAreaCenter.x - followAreaSize.x * 0.5f;
        float maxX = followAreaCenter.x + followAreaSize.x * 0.5f;
        float minY = followAreaCenter.y - followAreaSize.y * 0.5f;
        float maxY = followAreaCenter.y + followAreaSize.y * 0.5f;

        // 检测是否在范围内
        bool inRangeX = actorPosition.x >= minX && actorPosition.x <= maxX;
        bool inRangeY = actorPosition.y >= minY && actorPosition.y <= maxY;

        // 如果超出范围，返回 true（需要跟随）
        return !(inRangeX && inRangeY);
    }

    /// <summary>
    /// 设置是否允许跟随
    /// </summary>
    public void SetCanFollow(bool canFollow)
    {
        this.canFollow = canFollow;
    }

    /// <summary>
    /// 设置跟随区域
    /// </summary>
    public void SetFollowArea(Vector2 center, Vector2 size)
    {
        followAreaCenter = center;
        followAreaSize = size;
    }

    /// <summary>
    /// 在编辑器中可视化跟随区域
    /// </summary>
    void OnDrawGizmosSelected()
    {
        // 绘制跟随区域
        Gizmos.color = Color.yellow;
        Vector3 center = new Vector3(followAreaCenter.x, followAreaCenter.y, 0);
        Vector3 size = new Vector3(followAreaSize.x, followAreaSize.y, 0.1f);
        Gizmos.DrawWireCube(center, size);
    }
}
