using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 控制船向右移动，移动速度暴露给我，默认为 2
/// 船接触运行时画线后切换为 Dynamic，保持初始 Y 并限制旋转角度
/// 角色在接触画线前与船同步保持 Kinematic，接触后一起切换为 Dynamic
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 36 Ctrl")]
public sealed class DLevel036Ctrl : MonoBehaviour, IDrawStrokeCompletedHandler
{
    #region Constants

    private const int MaxLineContactCount = 8;
    private const string DrawLayerName = "Draw";
    private const float DirectionEpsilon = 0.0001f;

    #endregion

    #region Inspector References

    /// <summary>
    /// 承载 Ship 视觉与碰撞体的船身刚体。
    /// 调用边界：只绑定 Scene/Ship 刚体；Line 接触后由 Kinematic 切为 Dynamic。
    /// </summary>
    [Tooltip("Scene/Ship 节点上的 Rigidbody2D。")]
    [SerializeField] private Rigidbody2D platformRigidbody;

    /// <summary>
    /// 跟随船水平移动的铰链支点刚体。
    /// 调用边界：只绑定 Scene/ShipPivot 的 Kinematic Rigidbody2D，不参与碰撞。
    /// </summary>
    [Tooltip("Scene/ShipPivot 节点上的 Kinematic Rigidbody2D。")]
    [SerializeField] private Rigidbody2D shipPivotRigidbody;

    /// <summary>
    /// 随船移动的本关角色根刚体。
    /// 调用边界：只绑定 DLevel_036/Scene/Actor；等待阶段与船同步，Line 接触后恢复 Dynamic。
    /// </summary>
    [Tooltip("DLevel_036/Scene/Actor 根节点上的 Rigidbody2D。")]
    [SerializeField] private Rigidbody2D actorRigidbody;

    #endregion

    #region Inspector Settings

    /// <summary>
    /// Ship 和 Actor 进入等待态时获得的初始水平速度，单位为世界单位/秒。
    /// 注意事项：只在进入等待态或 Reset 时设置一次，不在 FixedUpdate 中持续控制。
    /// </summary>
    [Tooltip("Ship 和 Actor 等待态的初始水平速度（世界单位/秒）。")]
    [UnityEngine.Serialization.FormerlySerializedAs("moveSpeed")]
    [SerializeField] private float initialHorizontalVelocity = 2f;

    /// <summary>
    /// Line 首次砸中船头时，Actor 获得的基准抛射冲量。
    /// 注意事项：X 方向由 Line 与 Actor 的相对位置决定，Y 始终向上；零向量会关闭额外抛射。
    /// </summary>
    [Tooltip("Line 砸中船头后施加给 Actor 的基准冲量，X 符号由相对位置决定。")]
    [SerializeField] private Vector2 baseCatapultForce = new(0.1f, 0.8f);

    /// <summary>
    /// Line 每下落一个世界单位时，基准抛射冲量增加的倍率。
    /// 调用边界：仅使用画线完成时高度与命中时高度的正差值；配置为 0 时力度不随高度变化。
    /// </summary>
    [Tooltip("Line 每下落 1 个世界单位，基准冲量增加的倍率。")]
    [SerializeField, Min(0f)] private float forcePerDropUnit = 1f;

    /// <summary>
    /// Line 几乎无下落距离时仍保证 Actor 获得的最小向上冲量。
    /// 注意事项：仅抬高最终冲量的 Y 分量，不改变左右方向；配置为 0 时关闭最低保障。
    /// </summary>
    [Tooltip("Actor 命中后的最小向上冲量，避免零落差时几乎无法起飞。")]
    [SerializeField, Min(0f)] private float minimumActorUpwardImpulse = 8f;

    /// <summary>
    /// Ship 切为 Dynamic 后回到初始角度的扭簧强度。
    /// 注意事项：只负责回正，不产生首次旋转；配置为 0 时关闭主动回弹。
    /// </summary>
    [Tooltip("Ship 偏离初始角度后的回正扭矩强度。")]
    [SerializeField, Min(0f)] private float shipSpringStrength = 10f;

    /// <summary>
    /// Ship 回弹时抵消角速度的阻尼强度。
    /// 注意事项：值越大越快停止摆动，配置过大会让回弹显得僵硬。
    /// </summary>
    [Tooltip("Ship 回弹角速度阻尼，值越大越快稳定。")]
    [SerializeField, Min(0f)] private float shipSpringDamping = 2f;

    #endregion

    #region Runtime State

    private DrawLevelCtrl _drawLevelCtrl;
    private DrawLineFeature _drawLineFeature;
    private readonly Dictionary<Rigidbody2D, float> _lineStartHeights = new();
    private readonly ContactPoint2D[] _lineContacts = new ContactPoint2D[MaxLineContactCount];
    private ContactFilter2D _lineContactFilter;
    private Vector2 _initialShipPosition;
    private float _initialShipRotation;
    private Vector2 _initialPivotPosition;
    private float _initialPivotRotation;
    private Vector2 _initialActorPosition;
    private float _initialActorRotation;
    private bool _hasInitialState;
    private bool _isLineContactFilterReady;
    private bool _isDynamic;
    private bool _isResolved;
    private bool _isSubscribed;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        CacheInitialState();
        PrepareWaitingState();
        SubscribeToLevelResolved();
    }

    private void FixedUpdate()
    {
        if (_isResolved || platformRigidbody == null || shipPivotRigidbody == null ||
            actorRigidbody == null ||
            (_drawLevelCtrl != null && _drawLevelCtrl.IsPaused))
            return;

        if (_isDynamic)
        {
            ApplyShipSpring();
            return;
        }

        if (TryGetLineContact(out Rigidbody2D lineRigidbody))
            ReleaseBodiesToDynamic(lineRigidbody);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        HoldBodiesAtCurrentPosition();
        _lineStartHeights.Clear();
        UnsubscribeFromLevelResolved();
    }

    #endregion

    #region IDrawStrokeCompletedHandler

    /// <summary>
    /// 记录当前完成 Line 开始下落前的高度，用于命中时计算抛射力度。
    /// 调用边界：只持有当前尝试内由 DrawLineFeature 管理的刚体，Reset/Disable 时清空。
    /// </summary>
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        if (_drawLineFeature == null ||
            !_drawLineFeature.TryGetCompletedStrokeObject(result.StrokeIndex, out GameObject strokeObject) ||
            strokeObject == null)
        {
            return;
        }

        Rigidbody2D lineRigidbody = strokeObject.GetComponent<Rigidbody2D>();
        if (lineRigidbody != null)
            _lineStartHeights[lineRigidbody] = lineRigidbody.worldCenterOfMass.y;
    }

    /// <summary>
    /// 当前尝试重置时，把船恢复到关卡初始位置并重新开始水平移动。
    /// </summary>
    public void OnDrawAttemptReset()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        if (platformRigidbody == null || shipPivotRigidbody == null ||
            actorRigidbody == null || !_hasInitialState)
            return;

        _lineStartHeights.Clear();
        _isResolved = false;
        RestoreInitialStructure();
        PrepareWaitingState();
    }

    #endregion

    #region Ship Movement

    private bool TryGetLineContact(out Rigidbody2D lineRigidbody)
    {
        lineRigidbody = null;
        Rigidbody2D currentPlatformRigidbody = platformRigidbody;
        if (currentPlatformRigidbody == null)
            return false;

        EnsureLineContactFilter();
        int contactCount = currentPlatformRigidbody.GetContacts(_lineContactFilter, _lineContacts);
        for (int i = 0; i < contactCount; i++)
        {
            Rigidbody2D contactRigidbody = _lineContacts[i].otherRigidbody;
            if (contactRigidbody == null)
                continue;

            lineRigidbody = contactRigidbody;
            return true;
        }

        return false;
    }

    private void EnsureLineContactFilter()
    {
        if (_isLineContactFilterReady)
            return;

        int drawLayer = LayerMask.NameToLayer(DrawLayerName);
        int drawLayerMask = drawLayer >= 0 ? 1 << drawLayer : 0;
        _lineContactFilter = new ContactFilter2D
        {
            useTriggers = false,
            useLayerMask = true,
            layerMask = drawLayerMask
        };
        _isLineContactFilterReady = true;
    }

    private void ReleaseBodiesToDynamic(Rigidbody2D lineRigidbody)
    {
        ReleaseBodyToDynamic(platformRigidbody);
        ReleaseBodyToDynamic(actorRigidbody);
        Vector2 catapultForce = CalculateCatapultForce(lineRigidbody);
        if (catapultForce != Vector2.zero)
            actorRigidbody.AddForce(catapultForce, ForceMode2D.Impulse);

        _isDynamic = true;
    }

    private Vector2 CalculateCatapultForce(Rigidbody2D lineRigidbody)
    {
        if (lineRigidbody == null || actorRigidbody == null)
            return Vector2.zero;

        Vector2 linePosition = lineRigidbody.worldCenterOfMass;
        Vector2 actorPosition = actorRigidbody.worldCenterOfMass;
        float horizontalOffset = linePosition.x - actorPosition.x;
        float horizontalSign = Mathf.Abs(horizontalOffset) > DirectionEpsilon
            ? Mathf.Sign(horizontalOffset)
            : Mathf.Sign(baseCatapultForce.x);
        if (Mathf.Approximately(horizontalSign, 0f))
            horizontalSign = 1f;

        Vector2 orientedBaseForce = new(
            Mathf.Abs(baseCatapultForce.x) * horizontalSign,
            Mathf.Abs(baseCatapultForce.y));
        float dropHeight = 0f;
        if (_lineStartHeights.TryGetValue(lineRigidbody, out float startHeight))
            dropHeight = Mathf.Max(0f, startHeight - linePosition.y);

        float heightMultiplier = 1f + dropHeight * Mathf.Max(0f, forcePerDropUnit);
        Vector2 catapultImpulse = orientedBaseForce * heightMultiplier;
        catapultImpulse.y = Mathf.Max(catapultImpulse.y, Mathf.Max(0f, minimumActorUpwardImpulse));
        return catapultImpulse;
    }

    private void ApplyShipSpring()
    {
        float springStrength = Mathf.Max(0f, shipSpringStrength);
        float springDamping = Mathf.Max(0f, shipSpringDamping);
        if (springStrength <= Mathf.Epsilon && springDamping <= Mathf.Epsilon)
            return;

        float returnAngle = Mathf.DeltaAngle(platformRigidbody.rotation, _initialShipRotation);
        float springTorque = returnAngle * springStrength;
        float dampingTorque = -platformRigidbody.angularVelocity * springDamping;
        platformRigidbody.AddTorque(springTorque + dampingTorque, ForceMode2D.Force);
    }

    private void PrepareWaitingState()
    {
        if (platformRigidbody == null || shipPivotRigidbody == null || actorRigidbody == null)
            return;

        Vector2 initialVelocity = new(initialHorizontalVelocity, 0f);
        PrepareMovingBody(shipPivotRigidbody, initialVelocity);
        PrepareMovingBody(platformRigidbody, initialVelocity);
        PrepareMovingBody(actorRigidbody, initialVelocity);
        _isDynamic = false;
    }

    private void HoldBodiesAtCurrentPosition()
    {
        HoldBody(shipPivotRigidbody);
        HoldBody(platformRigidbody);
        HoldBody(actorRigidbody);
        _isDynamic = false;
    }

    private static void HoldBody(Rigidbody2D body)
    {
        if (body == null)
            return;

        body.bodyType = RigidbodyType2D.Kinematic;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        StopBodyMotion(body);
    }

    private static void PrepareMovingBody(Rigidbody2D body, Vector2 initialVelocity)
    {
        if (body == null)
            return;

        body.bodyType = RigidbodyType2D.Kinematic;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.linearVelocity = initialVelocity;
        body.angularVelocity = 0f;
        body.WakeUp();
    }

    private static void ReleaseBodyToDynamic(Rigidbody2D body)
    {
        if (body == null)
            return;

        Vector2 currentVelocity = body.linearVelocity;
        body.bodyType = RigidbodyType2D.Dynamic;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.linearVelocity = currentVelocity;
        body.WakeUp();
    }

    private static void StopBodyMotion(Rigidbody2D body)
    {
        if (body == null)
            return;

        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    #endregion

    #region State And References

    private void ResolveReferences()
    {
        if (platformRigidbody == null)
        {
            Transform ship = transform.Find("Scene/Ship");
            if (ship != null)
                platformRigidbody = ship.GetComponent<Rigidbody2D>();
        }

        if (shipPivotRigidbody == null)
        {
            Transform pivot = transform.Find("Scene/ShipPivot");
            if (pivot != null)
                shipPivotRigidbody = pivot.GetComponent<Rigidbody2D>();
        }

        if (actorRigidbody == null)
        {
            ActorCtrl actor = GetComponentInChildren<ActorCtrl>(true);
            if (actor != null)
                actorRigidbody = actor.GetComponent<Rigidbody2D>();
        }

        if (_drawLevelCtrl == null)
            _drawLevelCtrl = GetComponentInParent<DrawLevelCtrl>();

        if (_drawLineFeature == null)
            _drawLineFeature = GetComponent<DrawLineFeature>();
    }

    private void CacheInitialState()
    {
        if (_hasInitialState || platformRigidbody == null || shipPivotRigidbody == null ||
            actorRigidbody == null)
            return;

        _initialShipPosition = platformRigidbody.position;
        _initialShipRotation = platformRigidbody.rotation;
        _initialPivotPosition = shipPivotRigidbody.position;
        _initialPivotRotation = shipPivotRigidbody.rotation;
        _initialActorPosition = actorRigidbody.position;
        _initialActorRotation = actorRigidbody.rotation;
        _hasInitialState = true;
    }

    private void RestoreInitialStructure()
    {
        platformRigidbody.position = _initialShipPosition;
        platformRigidbody.rotation = _initialShipRotation;
        shipPivotRigidbody.position = _initialPivotPosition;
        shipPivotRigidbody.rotation = _initialPivotRotation;
        actorRigidbody.position = _initialActorPosition;
        actorRigidbody.rotation = _initialActorRotation;
    }

    private void SubscribeToLevelResolved()
    {
        if (_isSubscribed || _drawLevelCtrl == null)
            return;

        _drawLevelCtrl.Resolved += OnLevelResolved;
        _isSubscribed = true;
    }

    private void UnsubscribeFromLevelResolved()
    {
        if (!_isSubscribed || _drawLevelCtrl == null)
            return;

        _drawLevelCtrl.Resolved -= OnLevelResolved;
        _isSubscribed = false;
    }

    private void OnLevelResolved(DrawLevelResult result)
    {
        _isResolved = true;
        HoldBodiesAtCurrentPosition();
    }

    #endregion
}
