using Sirenix.OdinInspector;
using Game.ItemEvent;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 拾取动画：Target 按照一定速度朝向 EndTarget 运动（2D），使用 LookAt 和 Translate 实现移动。
/// 当距离小于阈值时完成。
/// </summary>
public class PickupAnimListener : BaseItemEventHandle
{
    [Header("Target Settings")]
    [Tooltip("被移动的物体（同时作为起点；为空则移动自身）")]
    [SerializeField] private Transform target;

    [Tooltip("结束位置")]
    [SerializeField] private Transform endTarget;

    [Header("Movement Settings")]
    [Tooltip("移动速度（单位/秒）")]
    [SerializeField] private float moveSpeed = 15f;

    [Tooltip("旋转速度（度/秒）")]
    [SerializeField] private float rotationSpeed = 270;

    [Tooltip("完成距离阈值（当距离小于此值时算作完成）")]
    [SerializeField] private float completionDistance = .8f;

    [Header("Events")]
    [Tooltip("到达 EndTarget 时触发的事件")]
    public UnityEvent OnReachEndTarget;

    [Header("Direction & Offset")]
    [Tooltip("为 true 时方向为 EndTarget→Target，否则为 Target→EndTarget")]
    [SerializeField] private bool flipDirection;

    [Tooltip("方向偏移量（Play 时添加到方向向量上，2D：X/Y）")]
    [SerializeField] private Vector2 offset = Vector2.zero;

    private Vector2 _initialDirection;
    private bool _hasReachedTarget;

    private void Awake()
    {
        if (target == null)
            target = transform;
    }

    [Button("Play")]
    public override void Execute()
    {
        if (target == null)
            target = transform;

        if (endTarget == null)
        {
            Debug.LogWarning("[PickupAnimListener] EndTarget is null!");
            IsDone = true;
            return;
        }

        // 只计算左右朝向（X轴方向）
        float deltaX = endTarget.position.x - target.position.x;

        // 判断左右：deltaX > 0 表示 endTarget 在右边，deltaX < 0 表示在左边
        Vector2 horizontalDirection = deltaX >= 0 ? Vector2.right : Vector2.left;

        // 根据 flipDirection 决定是否翻转
        Vector2 direction = flipDirection ? -horizontalDirection : horizontalDirection;

        // 计算初始方向：direction + offset
        _initialDirection = (direction + offset).normalized;

        // 根据初始方向旋转物体（2D：只旋转 Z 轴）
        if (_initialDirection.magnitude > 0.001f)
        {
            float angle = Mathf.Atan2(_initialDirection.y, _initialDirection.x) * Mathf.Rad2Deg;
            target.rotation = Quaternion.Euler(0f, 0f, angle - 90f); // -90 因为通常 Sprite 的 up 是 Y 轴正方向
        }

        IsRunning = true;
        IsDone = false;
        _hasReachedTarget = false;
    }

    private void Update()
    {
        if (!IsRunning || IsDone)
            return;

        if (target == null || endTarget == null)
        {
            IsRunning = false;
            IsDone = true;
            return;
        }

        // 计算当前方向（实时更新，2D）
        Vector2 currentPos = new Vector2(target.position.x, target.position.y);
        Vector2 targetPos2D = new Vector2(endTarget.position.x, endTarget.position.y);
        Vector2 toTarget = targetPos2D - currentPos;
        float distance = toTarget.magnitude;

        // 检查是否完成
        if (distance < completionDistance)
        {
            if (!_hasReachedTarget)
            {
                _hasReachedTarget = true;
                OnReachEndTarget?.Invoke();
            }
            IsRunning = false;
            IsDone = true;
            return;
        }

        // RotateTowards 平滑转向目标（2D：只旋转 Z 轴）
        if (toTarget.magnitude > 0.001f)
        {
            float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg - 90f; // -90 因为通常 Sprite 的 up 是 Y 轴正方向
            Quaternion targetRotation = Quaternion.Euler(0f, 0f, targetAngle);

            // 使用 RotateTowards 平滑旋转
            float maxRotationDelta = rotationSpeed * Time.deltaTime;
            target.rotation = Quaternion.RotateTowards(target.rotation, targetRotation, maxRotationDelta);
        }

        // Translate 移动：沿着物体当前朝向的前方运动（2D）
        Vector2 forwardDirection = new Vector2(target.up.x, target.up.y);
        Vector3 movement = new Vector3(forwardDirection.x, forwardDirection.y, 0f) * moveSpeed * Time.deltaTime;
        target.Translate(movement, Space.World);
    }

    public override void ResetState()
    {
        base.ResetState();
        _hasReachedTarget = false;
    }

    /// <summary>
    /// 设置移动速度
    /// </summary>
    public void SetMoveSpeed(float speed)
    {
        moveSpeed = Mathf.Max(0f, speed);
    }

    /// <summary>
    /// 设置完成距离阈值
    /// </summary>
    public void SetCompletionDistance(float distance)
    {
        completionDistance = Mathf.Max(0f, distance);
    }

    /// <summary>
    /// 设置是否翻转方向
    /// </summary>
    public void SetFlipDirection(bool flip)
    {
        flipDirection = flip;
    }

    /// <summary>
    /// 设置方向偏移量（2D）
    /// </summary>
    public void SetOffset(Vector2 offsetValue)
    {
        offset = offsetValue;
    }


    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
