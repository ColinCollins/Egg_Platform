using Bear.EventSystem;
using Bear.Logger;
using Game.Events;
using UnityEngine;

/// <summary>
/// IWanner 区域重力控制器。
/// 控制 AreaEffector2D 区域力，并在有效落地 Jump 时同步反转力方向、角色 Y 轴朝向与 Ground 检测方向。
/// </summary>
[DisallowMultipleComponent]
public sealed class IWannerGame_GravityCtrl : MonoBehaviour, IDebuger
{
    #region Serialized Fields

    /// <summary>
    /// 用途：指定受区域重力与跳跃反转影响的 IWanner 角色。
    /// 调用边界：运行时只同步 Ground 检测方向与根节点 Y 轴朝向，不修改该角色 Rigidbody2D.gravityScale。
    /// 注意事项：未配置时从当前关卡子层级查找；角色必须位于 AreaEffector2D 的有效区域和 Collider Mask 内。
    /// </summary>
    [Tooltip("受 AreaEffector2D 重力影响并在落地 Jump 时反转方向的 IWanner 角色；留空时从子层级查找")]
    [SerializeField] private Actor_IWanner actor;

    /// <summary>
    /// 用途：指定作为当前关卡重力来源的 AreaEffector2D。
    /// 调用边界：Force Angle 是本组件同步 Ground 检测方向的来源；运行时 API 只修改 forceAngle 与 enabled。
    /// 注意事项：未配置时从当前对象子层级查找；Collider、Mask、Force Magnitude、Drag 与 Force Target 仍由 prefab 配置。
    /// </summary>
    [Tooltip("作为当前关卡重力来源的 AreaEffector2D；留空时从子层级查找")]
    [SerializeField] private AreaEffector2D areaEffector;

    /// <summary>
    /// 用途：配置当前 AreaEffector2D 是否开启。
    /// 调用边界：组件进入 PlayMode 时立即写入 AreaEffector2D.enabled，运行时由 SetGravityEnabled/ToggleGravityEnabled 同步更新。
    /// 注意事项：只控制 AreaEffector2D，不修改 Actor 的 gravityScale 或目标 GameObject.activeSelf。
    /// </summary>
    [Tooltip("进入 PlayMode 时是否开启绑定的 AreaEffector2D")]
    [SerializeField] private bool isGravityEnabled = true;

    /// <summary>
    /// 用途：配置当前 AreaEffector2D 的 Force Angle。
    /// 调用边界：组件进入 PlayMode 时立即写入 AreaEffector2D.forceAngle，Jump 反转与 SetForceAngle 会同步更新该值。
    /// 注意事项：角度按 0～360 度归一化；0/90/180/270 分别表示右/上/左/下。
    /// </summary>
    [Tooltip("写入 AreaEffector2D 的 Force Angle；0/90/180/270 分别为右/上/左/下")]
    [SerializeField] private float forceAngle;

    #endregion

    #region Runtime State

    private EventSubscriber _subscriber;
    private bool _isWaitingForLanding;
    private bool _hasLeftPreviousGround;

    #endregion

    #region Public API

    /// <summary>
    /// 用途：运行时设置 AreaEffector2D 的 Force Angle，并让 Actor Ground 检测方向与实际区域力方向一致。
    /// 调用边界：供关卡 UnityEvent 或局部控制器调用；仅在 PlayMode 生效，角度会归一化到 0～360 度。
    /// 注意事项：Effector 关闭时只保存角度，Ground 检测继续保留最后方向，直到重力重新开启。
    /// </summary>
    /// <param name="angle">AreaEffector2D 的目标 Force Angle，0/90/180/270 分别为右/上/左/下。</param>
    public void SetForceAngle(float angle)
    {
        if (!Application.isPlaying)
            return;

        forceAngle = Mathf.Repeat(angle, 360f);
        ResolveReferences();
        if (areaEffector == null)
        {
            this.LogWarning("[IWannerGame_GravityCtrl] AreaEffector2D is missing; Force Angle config was saved but not applied.");
            return;
        }

        areaEffector.forceAngle = forceAngle;
        if (isGravityEnabled && areaEffector.enabled)
            SyncActorGravityDirection();
    }

    /// <summary>
    /// 用途：运行时设置绑定 AreaEffector2D 的 Force Magnitude。
    /// 调用边界：供关卡 UnityEvent 或局部控制器在切换区域力阶段时调用；仅在 PlayMode 生效。
    /// 注意事项：保留 Unity 原始数值语义，负值会反转 Force Angle 对应的实际施力方向；不会修改角度、开关或 Actor 自身重力。
    /// </summary>
    /// <param name="magnitude">写入 AreaEffector2D 的 Force Magnitude。</param>
    public void SetForceMagnitude(float magnitude)
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        if (areaEffector == null)
        {
            this.LogWarning("[IWannerGame_GravityCtrl] AreaEffector2D is missing; Force Magnitude cannot be applied.");
            return;
        }

        areaEffector.forceMagnitude = magnitude;
        if (isGravityEnabled && areaEffector.enabled)
            SyncActorGravityDirection();
    }

    /// <summary>
    /// 用途：开启或关闭绑定的 AreaEffector2D 重力。
    /// 调用边界：供关卡 UnityEvent 或局部控制器运行时切换阶段；可安全重复调用。
    /// 注意事项：不会修改 Actor 自身 gravityScale；关闭后 Ground 检测保留最后区域力方向。
    /// </summary>
    /// <param name="isEnabled">true 开启区域重力，false 关闭区域重力。</param>
    public void SetGravityEnabled(bool isEnabled)
    {
        if (!Application.isPlaying)
            return;

        isGravityEnabled = isEnabled;
        ResolveReferences();
        if (areaEffector == null)
        {
            this.LogWarning("[IWannerGame_GravityCtrl] AreaEffector2D is missing; gravity switch config was saved but not applied.");
            return;
        }

        if (!isEnabled && actor != null)
            actor.PreserveCurrentGroundCheckDirection();

        areaEffector.enabled = isGravityEnabled;
        if (isGravityEnabled)
            SyncActorGravityDirection();
    }

    /// <summary>
    /// 用途：在开启与关闭之间切换绑定的 AreaEffector2D 重力。
    /// 调用边界：供不便传递 bool 参数的关卡 UnityEvent 使用；仅在 PlayMode 生效。
    /// 注意事项：关闭时保留最后 Ground 检测方向，重新开启时按当前 Force Angle 重新同步。
    /// </summary>
    public void ToggleGravityEnabled()
    {
        if (!Application.isPlaying)
            return;

        SetGravityEnabled(!isGravityEnabled);
    }

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        if (areaEffector == null)
        {
            this.LogWarning("[IWannerGame_GravityCtrl] AreaEffector2D is missing.");
            return;
        }

        ApplyConfiguredEffectorState();
        if (actor == null)
        {
            this.LogWarning("[IWannerGame_GravityCtrl] Actor_IWanner is missing.");
            return;
        }

        if (isGravityEnabled)
            SyncActorGravityDirection();

        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<PlayerJumpEvent>(OnPlayerJump);
    }

    private void LateUpdate()
    {
        if (!_isWaitingForLanding || actor == null || actor.IsDied)
            return;

        if (!actor.IsGround)
        {
            _hasLeftPreviousGround = true;
            return;
        }

        if (!_hasLeftPreviousGround)
            return;

        _isWaitingForLanding = false;
        _hasLeftPreviousGround = false;
    }

    private void OnDisable()
    {
        _subscriber?.Dispose();
        _subscriber = null;
    }

    #endregion

    #region Gravity Control

    private void OnPlayerJump(PlayerJumpEvent evt)
    {
        if (actor == null
            || areaEffector == null
            || !isGravityEnabled
            || !areaEffector.enabled
            || actor.IsDied
            || !actor.IsGround
            || _isWaitingForLanding)
        {
            return;
        }

        SetForceAngle(forceAngle + 180f);

        Vector3 actorScale = actor.transform.localScale;
        actorScale.y = -actorScale.y;
        actor.transform.localScale = actorScale;

        _isWaitingForLanding = true;
        _hasLeftPreviousGround = false;

        Rigidbody2D actorBody = ResolveActorBody();
        if (actorBody != null)
            actorBody.WakeUp();
    }

    #endregion

    #region Internal

    private void ResolveReferences()
    {
        if (actor == null)
            actor = GetComponentInChildren<Actor_IWanner>(true);
        if (areaEffector == null)
            areaEffector = GetComponentInChildren<AreaEffector2D>(true);
    }

    private Rigidbody2D ResolveActorBody()
    {
        if (actor == null)
            return null;

        return actor.RB != null
            ? actor.RB
            : actor.GetComponent<Rigidbody2D>();
    }

    private void ApplyConfiguredEffectorState()
    {
        forceAngle = Mathf.Repeat(forceAngle, 360f);
        areaEffector.forceAngle = forceAngle;
        areaEffector.enabled = isGravityEnabled;
    }

    private void SyncActorGravityDirection()
    {
        if (actor == null || areaEffector == null)
            return;

        Vector2 forceDirection = ResolveForceDirection();
        actor.SetGroundCheckDirection(forceDirection);
    }

    private Vector2 ResolveForceDirection()
    {
        float angleRadians = forceAngle * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians));
        if (!areaEffector.useGlobalAngle)
            direction = areaEffector.transform.TransformDirection(direction);
        if (areaEffector.forceMagnitude < 0f)
            direction = -direction;

        return direction.normalized;
    }

    #endregion
}
