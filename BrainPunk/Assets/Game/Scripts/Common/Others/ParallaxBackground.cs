using UnityEngine;

/// <summary>
/// 远景移动脚本 - 根据主角移动来移动远景图片，产生视差效果
/// </summary>
public class ParallaxBackground : MonoBehaviour
{
    [Header("目标设置")]
    [Tooltip("要跟踪的主角Transform，如果为空则自动查找ActorCtrl")]
    [SerializeField] private Transform targetTransform;
    
    [Header("移动参数")]
    [Tooltip("水平轴(X)移动速度比例，值越小移动越慢，产生视差效果")]
    [Range(-1f, 1f)]
    [SerializeField] private float horizontalSpeed = -0.5f;
    
    [Tooltip("垂直轴(Y)移动速度比例，值越小移动越慢，产生视差效果")]
    [Range(-1f, 1f)]
    [SerializeField] private float verticalSpeed = 0f;
    
    [Header("缓动设置")]
    [Tooltip("移动平滑时间（秒），值越大缓动越明显，建议范围 0.1-0.5")]
    [Range(0.01f, 2f)]
    [SerializeField] private float smoothTime = 0.2f;
    
    [Tooltip("最大移动速度限制，防止快速移动时出现抖动")]
    [SerializeField] private float maxSpeed = Mathf.Infinity;
    
    [Header("初始位置")]
    [Tooltip("是否在Start时记录初始位置")]
    [SerializeField] private bool useInitialPosition = true;
    
    private Vector3 initialPosition;
    private Vector3 lastTargetPosition;
    private Vector3 targetParallaxPosition;
    private Vector3 currentVelocity;
    private ActorCtrl actorCtrl;
    
    void Start()
    {
        // 如果没有指定目标，尝试自动查找ActorCtrl
        if (targetTransform == null)
        {
            actorCtrl = FindObjectOfType<ActorCtrl>();
            if (actorCtrl != null)
            {
                targetTransform = actorCtrl.transform;
            }
            else
            {
                Debug.LogWarning($"[ParallaxBackground] 未找到主角，请在Inspector中指定targetTransform");
                enabled = false;
                return;
            }
        }
        
        // 记录初始位置
        if (useInitialPosition)
        {
            initialPosition = transform.position;
        }
        
        // 记录目标的初始位置
        if (targetTransform != null)
        {
            lastTargetPosition = targetTransform.position;
            // 初始化目标视差位置
            Vector3 targetDelta = targetTransform.position - lastTargetPosition;
            targetParallaxPosition = transform.position + new Vector3(
                targetDelta.x * horizontalSpeed,
                targetDelta.y * verticalSpeed,
                0f
            );
        }
        
        // 初始化速度
        currentVelocity = Vector3.zero;
    }
    
    void LateUpdate()
    {
        if (targetTransform == null)
            return;
        
        // 计算主角的移动距离
        Vector3 currentTargetPosition = targetTransform.position;
        Vector3 deltaPosition = currentTargetPosition - lastTargetPosition;
        
        // 根据各轴的速度比例计算目标视差位置
        Vector3 parallaxDelta = new Vector3(
            deltaPosition.x * horizontalSpeed,
            deltaPosition.y * verticalSpeed,
            0f
        );
        
        // 更新目标位置
        targetParallaxPosition += parallaxDelta;
        
        // 使用平滑阻尼移动到目标位置，实现缓动效果
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetParallaxPosition,
            ref currentVelocity,
            smoothTime,
            maxSpeed
        );
        
        // 更新记录的位置
        lastTargetPosition = currentTargetPosition;
    }
    
    /// <summary>
    /// 设置水平轴移动速度比例
    /// </summary>
    /// <param name="speed">速度比例 (-1 到 1，负值表示反向移动)</param>
    public void SetHorizontalSpeed(float speed)
    {
        horizontalSpeed = Mathf.Clamp(speed, -1f, 1f);
    }
    
    /// <summary>
    /// 设置垂直轴移动速度比例
    /// </summary>
    /// <param name="speed">速度比例 (-1 到 1，负值表示反向移动)</param>
    public void SetVerticalSpeed(float speed)
    {
        verticalSpeed = Mathf.Clamp(speed, -1f, 1f);
    }
    
    /// <summary>
    /// 设置水平和垂直轴移动速度比例
    /// </summary>
    /// <param name="horizontal">水平速度比例</param>
    /// <param name="vertical">垂直速度比例</param>
    public void SetMoveSpeed(float horizontal, float vertical)
    {
        horizontalSpeed = Mathf.Clamp(horizontal, -1f, 1f);
        verticalSpeed = Mathf.Clamp(vertical, -1f, 1f);
    }
    
    /// <summary>
    /// 设置目标Transform
    /// </summary>
    /// <param name="target">要跟踪的目标</param>
    public void SetTarget(Transform target)
    {
        targetTransform = target;
        if (target != null)
        {
            lastTargetPosition = target.position;
        }
    }
    
    /// <summary>
    /// 重置到初始位置
    /// </summary>
    public void ResetPosition()
    {
        if (useInitialPosition)
        {
            transform.position = initialPosition;
            targetParallaxPosition = initialPosition;
        }
        if (targetTransform != null)
        {
            lastTargetPosition = targetTransform.position;
        }
        currentVelocity = Vector3.zero;
    }
    
    /// <summary>
    /// 设置平滑时间
    /// </summary>
    /// <param name="time">平滑时间（秒）</param>
    public void SetSmoothTime(float time)
    {
        smoothTime = Mathf.Max(0.01f, time);
    }
}
