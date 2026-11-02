using Game.ItemEvent;
using UnityEngine;

/// <summary>
/// IWanner 加速移动触发监听器。
/// 触发后沿 offset 方向以指定初速度和加速度移动，到达触发瞬间计算出的终点后停止。
/// 当目标挂载已启用的 Kinematic Rigidbody2D 时，按实例配置选择物理移动方式；其他目标保留逐帧 Transform 移动。
/// </summary>
public sealed class IWannerGame_AccelerateMoveTriggerListener : BaseItemEventHandle
{
    #region Types

    private enum KinematicMovementMode
    {
        MovePosition,
        LinearVelocity,
    }

    #endregion

    #region Constants

    private const float ArrivalEpsilon = 0.0001f;

    #endregion

    #region Serialized Fields

    /// <summary>
    /// 用途：指定实际移动的目标；为空时默认使用当前组件所在 Transform。
    /// 调用边界：仅在运行时触发移动和编辑器 Gizmos 预览时读取。
    /// 注意事项：移动期间销毁目标会中止本次执行并标记完成。
    /// </summary>
    [Header("Target")]
    [Tooltip("要移动的目标；为空时默认移动自身所在 Transform")]
    [SerializeField] private Transform target;

    /// <summary>
    /// 用途：定义相对触发时位置的移动方向和距离。
    /// 调用边界：只使用 X/Y 分量，目标的 Z 坐标保持触发时数值。
    /// 注意事项：重复触发会从当时位置重新计算终点，因此位移会累积。
    /// </summary>
    [Header("Move Settings")]
    [Tooltip("相对触发时位置的移动偏移（仅生效 X/Y，Z 保持不变）")]
    [SerializeField] private Vector2 offset;

    /// <summary>
    /// 用途：设置触发开始时沿 offset 方向的速度，单位为世界单位/秒。
    /// 调用边界：每次 Execute 都会用该值重置当前速度。
    /// 注意事项：运行时按非负值处理；若初速度与加速度都为 0，则本次执行不会产生位移。
    /// </summary>
    [Tooltip("沿 offset 方向的初始速度（世界单位/秒）")]
    [Min(0f)]
    [SerializeField] private float initialSpeed = 1f;

    /// <summary>
    /// 用途：设置沿 offset 方向持续增加的速度，单位为世界单位/秒²。
    /// 调用边界：仅在本次移动尚未到达终点时，按当前移动路径的更新步应用。
    /// 注意事项：运行时按非负值处理，不支持负加速度或反向移动。
    /// </summary>
    [Tooltip("沿 offset 方向的加速度（世界单位/秒²）")]
    [Min(0f)]
    [SerializeField] private float acceleration = 1f;

    /// <summary>
    /// 用途：选择 Kinematic Rigidbody2D 目标的推进方式。
    /// 调用边界：仅在 Execute 捕获到已启用的 Kinematic Rigidbody2D 时生效；普通 Transform 目标不读取该配置。
    /// 注意事项：MovePosition 保持既有关卡行为；LinearVelocity 用于需要向乘客暴露真实刚体速度的移动平台实例。
    /// </summary>
    [Header("Rigidbody Settings")]
    [Tooltip("Kinematic 目标的移动方式；默认 MovePosition 保持既有关卡行为")]
    [SerializeField] private KinematicMovementMode kinematicMovementMode = KinematicMovementMode.MovePosition;

    #endregion

    #region Runtime State

    private Vector3 _endPosition;
    private float _currentSpeed;
    private Rigidbody2D _targetRigidbody;
    private bool _useRigidbodyMovement;
    private bool _useLinearVelocityMovement;
    private bool _finishLinearVelocityMovementOnNextFixedUpdate;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (target == null)
            target = transform;
    }

    private void Update()
    {
        if (!IsRunning || _useRigidbodyMovement)
            return;

        if (target == null)
        {
            CompleteMovement();
            return;
        }

        AdvanceMovement(Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (!IsRunning || !_useRigidbodyMovement)
            return;

        if (!CanUseRigidbodyMovement())
        {
            StopVelocityDrivenRigidbody();
            _targetRigidbody = null;
            _useRigidbodyMovement = false;
            _useLinearVelocityMovement = false;
            return;
        }

        if (_finishLinearVelocityMovementOnNextFixedUpdate)
        {
            CompleteVelocityDrivenMovement();
            return;
        }

        AdvanceMovement(Time.fixedDeltaTime);
    }

    private void OnDisable()
    {
        if (IsRunning)
            CompleteMovement();
    }

    #endregion

    #region BaseItemEventHandle

    /// <summary>
    /// 用途：从目标当前位置开始一次加速移动，并在当前位置加 offset 的终点精确停下。
    /// 调用边界：由 TriggerExecutor/BaseItemExecutor 或明确的关卡本地逻辑调用。
    /// 注意事项：重复调用会中断旧过程并以当前坐标、配置初速度重新计算终点。
    /// </summary>
    public override void Execute()
    {
        StopVelocityDrivenRigidbody();

        if (target == null)
        {
            IsRunning = false;
            IsDone = true;
            return;
        }

        _targetRigidbody = target.GetComponent<Rigidbody2D>();
        _useRigidbodyMovement = CanUseRigidbodyMovement();
        _useLinearVelocityMovement = _useRigidbodyMovement
                                     && kinematicMovementMode == KinematicMovementMode.LinearVelocity;
        _finishLinearVelocityMovementOnNextFixedUpdate = false;
        _endPosition = target.position + new Vector3(offset.x, offset.y, 0f);
        _currentSpeed = Mathf.Max(0f, initialSpeed);
        IsRunning = true;
        IsDone = false;

        if (offset == Vector2.zero)
        {
            CompleteMovement();
            return;
        }

        if (_currentSpeed <= 0f && acceleration <= 0f)
            CompleteMovement();
    }

    /// <summary>
    /// 用途：停止当前移动并恢复未执行状态。
    /// 调用边界：供 BaseItemExecutor 每轮执行前和关卡 Reset 流程调用。
    /// 注意事项：只重置运行状态，不把目标位置还原到触发前坐标。
    /// </summary>
    public override void ResetState()
    {
        StopVelocityDrivenRigidbody();
        _currentSpeed = 0f;
        _targetRigidbody = null;
        _useRigidbodyMovement = false;
        _useLinearVelocityMovement = false;
        base.ResetState();
    }

    #endregion

    #region Internal

    private bool CanUseRigidbodyMovement()
    {
        return _targetRigidbody != null
               && _targetRigidbody.simulated
               && _targetRigidbody.bodyType == RigidbodyType2D.Kinematic;
    }

    private void AdvanceMovement(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        float previousSpeed = _currentSpeed;
        _currentSpeed += Mathf.Max(0f, acceleration) * deltaTime;
        float moveDistance = (previousSpeed + _currentSpeed) * 0.5f * deltaTime;
        if (moveDistance <= 0f)
            return;

        if (_useRigidbodyMovement)
        {
            if (_useLinearVelocityMovement)
            {
                AdvanceVelocityDrivenMovement(deltaTime, moveDistance);
                return;
            }

            Vector2 endPosition = _endPosition;
            Vector2 nextPosition = Vector2.MoveTowards(_targetRigidbody.position, endPosition, moveDistance);
            _targetRigidbody.MovePosition(nextPosition);

            if (nextPosition == endPosition)
                CompleteMovement();

            return;
        }

        target.position = Vector3.MoveTowards(target.position, _endPosition, moveDistance);
        if (target.position == _endPosition)
            CompleteMovement();
    }

    private void AdvanceVelocityDrivenMovement(float deltaTime, float moveDistance)
    {
        Vector2 endPosition = _endPosition;
        Vector2 remainingOffset = endPosition - _targetRigidbody.position;
        float remainingDistance = remainingOffset.magnitude;
        if (remainingDistance <= ArrivalEpsilon)
        {
            CompleteVelocityDrivenMovement();
            return;
        }

        float actualMoveDistance = Mathf.Min(moveDistance, remainingDistance);
        Vector2 movementDirection = remainingOffset / remainingDistance;
        _targetRigidbody.linearVelocity = movementDirection * (actualMoveDistance / deltaTime);
        _finishLinearVelocityMovementOnNextFixedUpdate =
            actualMoveDistance >= remainingDistance - ArrivalEpsilon;
    }

    private void CompleteVelocityDrivenMovement()
    {
        if (_targetRigidbody != null)
            _targetRigidbody.position = (Vector2)_endPosition;

        CompleteMovement();
    }

    private void StopVelocityDrivenRigidbody()
    {
        if (_useLinearVelocityMovement && _targetRigidbody != null)
            _targetRigidbody.linearVelocity = Vector2.zero;

        _finishLinearVelocityMovementOnNextFixedUpdate = false;
    }

    private void CompleteMovement()
    {
        StopVelocityDrivenRigidbody();
        _currentSpeed = 0f;
        _targetRigidbody = null;
        _useRigidbodyMovement = false;
        _useLinearVelocityMovement = false;
        IsRunning = false;
        IsDone = true;
    }

    #endregion

    #region Gizmos

    /// <summary>
    /// 编辑器下展示移动方向线段与最终落点，仅使用本地序列化数据，不触发运行逻辑。
    /// </summary>
    private void OnDrawGizmos()
    {
        Transform previewTarget = target != null ? target : transform;
        Vector3 start = previewTarget.position;
        Vector3 end = start + new Vector3(offset.x, offset.y, 0f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawWireSphere(end, 0.1f);
    }

    #endregion
}
