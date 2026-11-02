using Bear.Logger;
using UnityEngine;

/// <summary>
/// DrawGame 第 22 关车辆控制器。
/// 第一笔有效画线完成后，将线段放到车辆起点，并驱动车身与两只车轮同步向右加速行驶。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 22 Ctrl")]
public sealed class DLevel022Ctrl : MonoBehaviour, IDrawStrokeCompletedHandler, IDebuger
{
    #region Inspector References

    /// <summary>
    /// 当前关卡的画线组件，用于取得第一笔完成后的运行时线段。
    /// 调用边界：只绑定 DLevel_022 根节点上的 DrawLineFeature，不持有线段销毁权。
    /// </summary>
    [SerializeField] private DrawLineFeature drawLineFeature;

    /// <summary>
    /// 当前关卡控制器，用于在暂停或结算后停止车辆推进。
    /// 调用边界：只绑定同一 DLevel_022 根节点上的 DrawLevelCtrl。
    /// </summary>
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;

    /// <summary>
    /// 需要等待第一笔完成后才启用动态物理的玩家刚体。
    /// 调用边界：只绑定 DLevel_022 内 Actor 根节点的 Rigidbody2D；进入与重置时保持 Kinematic。
    /// </summary>
    [SerializeField] private Rigidbody2D actorRigidbody;

    /// <summary>
    /// 完成线段的车辆出生点。
    /// 调用边界：绑定 DLevel_022/Scene/startPoint；线段包围盒中心会直接对齐该世界坐标。
    /// </summary>
    [SerializeField] private Transform startPoint;

    /// <summary>
    /// 两只车轮的共同父节点，用于解析车轮和恢复当前尝试的初始层级位置。
    /// 调用边界：绑定 DLevel_022/Scene/wheels；运行时不直接移动该 Transform。
    /// </summary>
    [SerializeField] private Transform wheelsRoot;

    /// <summary>
    /// 需要随车辆滚动的两只车轮。
    /// 调用边界：按前后顺序绑定 wheels 下的两个 Sp_hx_mtlz_01；每项必须带 Dynamic Rigidbody2D
    /// 和 CircleCollider2D，缺失时会阻止车辆启动。
    /// </summary>
    [SerializeField] private Transform[] wheels = new Transform[2];

    #endregion

    #region Inspector Settings

    /// <summary>
    /// 车辆最大前进速度，单位为世界单位/秒。
    /// 注意事项：车辆达到该值后保持匀速；配置为 0 时车辆不前进。
    /// </summary>
    [Tooltip("车辆最大前进速度（世界单位/秒），达到后保持匀速。")]
    [SerializeField, Min(0f)] private float maxForwardSpeed = 4f;

    /// <summary>
    /// 车辆线性加速度，单位为世界单位/秒²。
    /// 注意事项：配置为 0 时跳过加速阶段，第一帧直接使用最大速度。
    /// </summary>
    [Tooltip("车辆从 0 增加到最大速度的线性加速度（世界单位/秒²）；0 表示立即达到最大速度。")]
    [SerializeField, Min(0f)] private float acceleration = 2f;

    #endregion

    #region Runtime State

    private Rigidbody2D _vehicleBody;
    private Rigidbody2D[] _wheelBodies;
    private Vector3[] _initialWheelLocalPositions;
    private Quaternion[] _initialWheelRotations;
    private float[] _wheelRadii;
    private Vector3 _initialWheelsRootLocalPosition;
    private Quaternion _initialWheelsRootLocalRotation;
    private float _currentSpeed;
    private bool _hasCachedInitialState;
    private bool _hasPlacedVehicleBody;
    private bool _isDriving;
    private bool _isResolved;
    private bool _isSubscribed;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        CacheInitialState();
        HoldActor();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        SubscribeToLevelResolved();
    }

    private void FixedUpdate()
    {
        if (!_isDriving || _isResolved || (drawLevelCtrl != null && drawLevelCtrl.IsPaused))
            return;

        if (_vehicleBody == null)
        {
            StopDriving();
            return;
        }

        float maximumSpeed = Mathf.Max(0f, maxForwardSpeed);
        float configuredAcceleration = Mathf.Max(0f, acceleration);
        _currentSpeed = configuredAcceleration > 0f
            ? Mathf.MoveTowards(
                _currentSpeed,
                maximumSpeed,
                configuredAcceleration * Time.fixedDeltaTime)
            : maximumSpeed;

        float distance = _currentSpeed * Time.fixedDeltaTime;
        if (distance <= 0f)
            return;

        Vector2 bodyVelocity = _vehicleBody.linearVelocity;
        bodyVelocity.x = _currentSpeed;
        _vehicleBody.linearVelocity = bodyVelocity;

        DriveWheelBodies();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        StopDriving();
        HoldActor();
        UnsubscribeFromLevelResolved();
    }

    #endregion

    #region IDrawStrokeCompletedHandler

    /// <summary>
    /// 第一笔有效画线完成后，将线段转换为动态车身并从 startPoint 启动车辆。
    /// 后续笔画或重复回调不会再次接管运行时线段。
    /// </summary>
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying || !result.IsFirstStroke || _hasPlacedVehicleBody || _isResolved)
            return;

        ResolveReferences();
        if (!ValidateStrokePlacementReferences())
            return;

        if (!drawLineFeature.TryGetCompletedStrokeObject(
                result.StrokeIndex,
                out GameObject strokeObject))
        {
            this.LogError($"[DLevel022Ctrl] 未找到第 {result.StrokeIndex} 笔运行时线段。");
            return;
        }

        Rigidbody2D strokeRigidbody = strokeObject.GetComponent<Rigidbody2D>();
        if (strokeRigidbody == null)
        {
            this.LogError("[DLevel022Ctrl] 已完成线段缺少 Rigidbody2D，无法作为车辆主体移动。");
            return;
        }

        PrepareVehicleBody(strokeObject, strokeRigidbody);
        _hasPlacedVehicleBody = true;
        _vehicleBody = strokeRigidbody;

        ReleaseActor();

        CacheInitialState();
        if (!ValidateDrivingReferences())
            return;

        _currentSpeed = 0f;
        _isDriving = true;
    }

    /// <summary>
    /// 当前尝试重置时停止车辆并恢复车轮初始位置；运行时线段仍由 DrawLineFeature 统一销毁。
    /// </summary>
    public void OnDrawAttemptReset()
    {
        StopDriving();
        HoldActor();
        RestoreWheelState();
        _hasPlacedVehicleBody = false;
        _isResolved = false;
    }

    #endregion

    #region Vehicle Movement

    private void PrepareVehicleBody(GameObject strokeObject, Rigidbody2D vehicleBody)
    {
        vehicleBody.simulated = false;
        vehicleBody.linearVelocity = Vector2.zero;
        vehicleBody.angularVelocity = 0f;
        vehicleBody.bodyType = RigidbodyType2D.Dynamic;
        vehicleBody.interpolation = RigidbodyInterpolation2D.Interpolate;

        // 与 DLevel7 的投掷定位保持一致：先移动运行时线段 Transform，再恢复 Physics2D 模拟。
        strokeObject.transform.position = startPoint.position;
        vehicleBody.position = startPoint.position;
        vehicleBody.simulated = true;
        vehicleBody.WakeUp();
    }

    private void HoldActor()
    {
        if (actorRigidbody == null)
            return;

        actorRigidbody.linearVelocity = Vector2.zero;
        actorRigidbody.angularVelocity = 0f;
        actorRigidbody.bodyType = RigidbodyType2D.Kinematic;
    }

    private void ReleaseActor()
    {
        if (actorRigidbody == null)
        {
            this.LogError("[DLevel022Ctrl] Actor Rigidbody2D 未绑定；车身已定位，但无法启用 Actor 动态物理。");
            return;
        }

        actorRigidbody.linearVelocity = Vector2.zero;
        actorRigidbody.angularVelocity = 0f;
        actorRigidbody.bodyType = RigidbodyType2D.Dynamic;
        actorRigidbody.WakeUp();
    }

    private void DriveWheelBodies()
    {
        for (int i = 0; i < _wheelBodies.Length; i++)
        {
            Rigidbody2D wheelBody = _wheelBodies[i];
            float radius = _wheelRadii[i];
            if (wheelBody == null)
                continue;

            Vector2 wheelVelocity = wheelBody.linearVelocity;
            wheelVelocity.x = _currentSpeed;
            wheelBody.linearVelocity = wheelVelocity;
            wheelBody.angularVelocity = radius > Mathf.Epsilon
                ? -(_currentSpeed / radius) * Mathf.Rad2Deg
                : 0f;
            wheelBody.WakeUp();
        }
    }

    private void StopDriving()
    {
        _isDriving = false;
        _currentSpeed = 0f;

        if (_vehicleBody != null)
        {
            _vehicleBody.linearVelocity = Vector2.zero;
            _vehicleBody.angularVelocity = 0f;
        }

        StopWheelMotion();
        _vehicleBody = null;
    }

    private void StopWheelMotion()
    {
        if (_wheelBodies == null)
            return;

        for (int i = 0; i < _wheelBodies.Length; i++)
        {
            Rigidbody2D wheelBody = _wheelBodies[i];
            if (wheelBody == null)
                continue;

            wheelBody.linearVelocity = Vector2.zero;
            wheelBody.angularVelocity = 0f;
        }
    }

    #endregion

    #region State And References

    private void ResolveReferences()
    {
        if (drawLineFeature == null)
            drawLineFeature = GetComponent<DrawLineFeature>();

        if (drawLevelCtrl == null)
            drawLevelCtrl = GetComponent<DrawLevelCtrl>();

        if (actorRigidbody == null)
        {
            ActorCtrl actorCtrl = GetComponentInChildren<ActorCtrl>(true);
            if (actorCtrl != null)
                actorRigidbody = actorCtrl.GetComponent<Rigidbody2D>();
        }

        if (startPoint == null)
            startPoint = transform.Find("Scene/startPoint");

        if (wheelsRoot == null)
            wheelsRoot = transform.Find("Scene/wheels");

        if (!HasTwoWheels() && wheelsRoot != null && wheelsRoot.childCount >= 2)
        {
            wheels = new[]
            {
                wheelsRoot.GetChild(0),
                wheelsRoot.GetChild(1)
            };
        }
    }

    private bool ValidateStrokePlacementReferences()
    {
        if (drawLineFeature == null || startPoint == null)
        {
            this.LogError("[DLevel022Ctrl] DrawLineFeature 或 startPoint 未绑定，无法定位完成线段。");
            return false;
        }

        return true;
    }

    private bool ValidateDrivingReferences()
    {
        if (wheelsRoot == null || !HasTwoWheels())
        {
            this.LogError("[DLevel022Ctrl] wheelsRoot 或两只车轮未完整绑定；车身已定位，但无法启动车辆。");
            return false;
        }

        if (!HasTwoWheelBodies())
        {
            this.LogError("[DLevel022Ctrl] 两只车轮必须绑定 Dynamic Rigidbody2D；车身已定位，但无法启动车辆。");
            return false;
        }

        return true;
    }

    private bool HasTwoWheels()
    {
        return wheels != null &&
               wheels.Length == 2 &&
               wheels[0] != null &&
               wheels[1] != null;
    }

    private bool HasTwoWheelBodies()
    {
        return _wheelBodies != null &&
               _wheelBodies.Length == 2 &&
               _wheelBodies[0] != null &&
               _wheelBodies[1] != null &&
               _wheelBodies[0].bodyType == RigidbodyType2D.Dynamic &&
               _wheelBodies[1].bodyType == RigidbodyType2D.Dynamic;
    }

    private void CacheInitialState()
    {
        if (_hasCachedInitialState || wheelsRoot == null || !HasTwoWheels())
            return;

        _initialWheelsRootLocalPosition = wheelsRoot.localPosition;
        _initialWheelsRootLocalRotation = wheelsRoot.localRotation;
        _wheelBodies = new Rigidbody2D[wheels.Length];
        _initialWheelLocalPositions = new Vector3[wheels.Length];
        _initialWheelRotations = new Quaternion[wheels.Length];
        _wheelRadii = new float[wheels.Length];

        for (int i = 0; i < wheels.Length; i++)
        {
            Transform wheel = wheels[i];
            _wheelBodies[i] = wheel.GetComponent<Rigidbody2D>();
            _initialWheelLocalPositions[i] = wheel.localPosition;
            _initialWheelRotations[i] = wheel.localRotation;
            _wheelRadii[i] = ResolveWheelRadius(wheel);
        }

        _hasCachedInitialState = true;
    }

    private void RestoreWheelState()
    {
        if (!_hasCachedInitialState || wheelsRoot == null || !HasTwoWheels())
            return;

        wheelsRoot.localPosition = _initialWheelsRootLocalPosition;
        wheelsRoot.localRotation = _initialWheelsRootLocalRotation;
        for (int i = 0; i < wheels.Length; i++)
        {
            Rigidbody2D wheelBody = _wheelBodies[i];
            bool wasSimulated = wheelBody != null && wheelBody.simulated;
            if (wheelBody != null)
                wheelBody.simulated = false;

            wheels[i].localPosition = _initialWheelLocalPositions[i];
            wheels[i].localRotation = _initialWheelRotations[i];

            if (wheelBody == null)
                continue;

            wheelBody.position = wheels[i].position;
            wheelBody.rotation = wheels[i].eulerAngles.z;
            wheelBody.linearVelocity = Vector2.zero;
            wheelBody.angularVelocity = 0f;
            wheelBody.simulated = wasSimulated;
            if (wasSimulated)
                wheelBody.Sleep();
        }
    }

    private static float ResolveWheelRadius(Transform wheel)
    {
        CircleCollider2D wheelCollider = wheel.GetComponent<CircleCollider2D>();
        if (wheelCollider == null)
            return 0f;

        Vector3 scale = wheel.lossyScale;
        float maximumScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
        return wheelCollider.radius * maximumScale;
    }

    private void SubscribeToLevelResolved()
    {
        if (_isSubscribed || drawLevelCtrl == null)
            return;

        drawLevelCtrl.Resolved += OnLevelResolved;
        _isSubscribed = true;
    }

    private void UnsubscribeFromLevelResolved()
    {
        if (!_isSubscribed || drawLevelCtrl == null)
            return;

        drawLevelCtrl.Resolved -= OnLevelResolved;
        _isSubscribed = false;
    }

    private void OnLevelResolved(DrawLevelResult result)
    {
        _isResolved = true;
        StopDriving();
    }

    #endregion
}
