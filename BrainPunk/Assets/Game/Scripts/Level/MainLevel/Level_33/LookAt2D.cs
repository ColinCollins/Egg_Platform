using UnityEngine;

/// <summary>
/// 2D LookAt 组件，物体总是旋转 z 轴朝向指定位置
/// </summary>
public class LookAt2D : MonoBehaviour
{
    [Header("目标设置")]
    [Tooltip("目标位置 Transform（优先使用）")]
    [SerializeField] private Transform targetTransform;

    [Tooltip("目标位置（世界坐标，当 targetTransform 为空时使用）")]
    [SerializeField] private Vector2 targetPosition = Vector2.zero;

    [Header("旋转设置")]
    [Tooltip("是否在 Update 中持续更新")]
    [SerializeField] private bool updateInUpdate = true;

    [Tooltip("旋转偏移角度（度）")]
    [SerializeField] private float rotationOffset = 0f;

    [Tooltip("是否平滑旋转")]
    [SerializeField] private bool smoothRotation = false;

    [Tooltip("平滑旋转速度（度/秒）")]
    [SerializeField] private float rotationSpeed = 360f;

    private float _currentZRotation;

    void Start()
    {
        _currentZRotation = transform.eulerAngles.z;
        if (!updateInUpdate)
        {
            UpdateRotation();
        }
    }

    void Update()
    {
        if (updateInUpdate)
        {
            UpdateRotation();
        }
    }

    /// <summary>
    /// 更新旋转，使物体朝向目标位置
    /// </summary>
    public void UpdateRotation()
    {
        Vector2 targetPos = GetTargetPosition();
        if (targetTransform == null && targetPosition == Vector2.zero && !Application.isPlaying)
        {
            return;
        }

        Vector2 currentPos = new Vector2(transform.position.x, transform.position.y);
        Vector2 direction = targetPos - currentPos;

        // 如果距离太小，不更新旋转
        if (direction.sqrMagnitude < 0.001f)
            return;

        // 计算目标角度（Unity 的 Atan2 返回弧度，转换为度）
        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        targetAngle += rotationOffset;

        if (smoothRotation)
        {
            // 平滑旋转
            float currentAngle = _currentZRotation;
            float deltaAngle = Mathf.DeltaAngle(currentAngle, targetAngle);
            float maxRotation = rotationSpeed * Time.deltaTime;
            
            if (Mathf.Abs(deltaAngle) > maxRotation)
            {
                _currentZRotation += Mathf.Sign(deltaAngle) * maxRotation;
            }
            else
            {
                _currentZRotation = targetAngle;
            }
        }
        else
        {
            _currentZRotation = targetAngle;
        }

        // 应用旋转（只旋转 z 轴）
        transform.rotation = Quaternion.Euler(0f, 0f, _currentZRotation);
    }

    /// <summary>
    /// 获取目标位置
    /// </summary>
    private Vector2 GetTargetPosition()
    {
        if (targetTransform != null)
        {
            return new Vector2(targetTransform.position.x, targetTransform.position.y);
        }
        return targetPosition;
    }

    /// <summary>
    /// 设置目标 Transform
    /// </summary>
    public void SetTarget(Transform target)
    {
        targetTransform = target;
    }

    /// <summary>
    /// 设置目标位置
    /// </summary>
    public void SetTargetPosition(Vector2 position)
    {
        targetPosition = position;
        targetTransform = null;
    }

    /// <summary>
    /// 设置旋转偏移
    /// </summary>
    public void SetRotationOffset(float offset)
    {
        rotationOffset = offset;
    }

    /// <summary>
    /// 设置是否平滑旋转
    /// </summary>
    public void SetSmoothRotation(bool smooth)
    {
        smoothRotation = smooth;
    }

    /// <summary>
    /// 设置平滑旋转速度
    /// </summary>
    public void SetRotationSpeed(float speed)
    {
        rotationSpeed = Mathf.Max(0f, speed);
    }

    void OnDrawGizmosSelected()
    {
        // 在 Scene 视图中绘制朝向线
        Vector2 targetPos = GetTargetPosition();
        Vector2 currentPos = new Vector2(transform.position.x, transform.position.y);
        
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, new Vector3(targetPos.x, targetPos.y, transform.position.z));
        
        // 绘制目标点
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(new Vector3(targetPos.x, targetPos.y, transform.position.z), 0.2f);
    }
}
