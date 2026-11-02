using UnityEngine;

/// <summary>
/// 直接移动组件，使用 Vector2 设置初始速度和加速度
/// 不使用 Rigidbody，直接通过 transform 移动
/// </summary>
public class DirectMove : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("初始速度 (x, y)")]
    [SerializeField] private Vector2 initSpeed = Vector2.zero;
    
    [Tooltip("加速度 (x, y)")]
    [SerializeField] private Vector2 accelerate = Vector2.zero;

    private Vector2 _currentVelocity;

    private void Start()
    {
        InitializeMovement();
    }

    /// <summary>
    /// 初始化移动参数
    /// </summary>
    private void InitializeMovement()
    {
        _currentVelocity = initSpeed;
    }

    private void Update()
    {
        // 更新速度
        _currentVelocity.x += accelerate.x * Time.deltaTime;
        _currentVelocity.y += accelerate.y * Time.deltaTime;

        // 应用位移
        transform.position += new Vector3(_currentVelocity.x, _currentVelocity.y, 0f) * Time.deltaTime;
    }

    /// <summary>
    /// 设置初始速度
    /// </summary>
    public void SetInitSpeed(Vector2 speed)
    {
        initSpeed = speed;
        _currentVelocity = speed;
    }

    /// <summary>
    /// 设置加速度
    /// </summary>
    public void SetAccelerate(Vector2 accel)
    {
        accelerate = accel;
    }

    /// <summary>
    /// 获取当前速度
    /// </summary>
    public Vector2 GetCurrentVelocity()
    {
        return _currentVelocity;
    }
}
