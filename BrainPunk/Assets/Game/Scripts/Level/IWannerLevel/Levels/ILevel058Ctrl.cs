using Bear.EventSystem;
using Bear.Logger;
using Game.Events;
using UnityEngine;

/// <summary>
/// IWanner 第 058 关局部控制器。
/// 激活后进入等待跳跃阶段；收到跳跃事件后禁止角色主动移动，并开启 AreaEffector2D、应用关卡力值与 0 度区域力角度。
/// </summary>
[DisallowMultipleComponent]
public sealed class ILevel058Ctrl : MonoBehaviour, IDebuger
{
    #region Serialized Fields

    /// <summary>
    /// 用途：指定响应第 058 关跳跃事件的 IWanner 角色。
    /// 调用边界：激活本控制器时清除该角色重力；后续跳跃事件完成后停止主动移动、修改根节点本地 Z 轴旋转并切换区域力。
    /// 注意事项：未配置时会从当前关卡子层级查找；Jump 后停止移动且不主动恢复，区域力阶段继续由 AreaEffector2D 推进。
    /// </summary>
    [Tooltip("第 058 关中响应旋转和水平区域力的 IWanner 角色；留空时从当前关卡子层级查找")]
    [SerializeField] private Actor_IWanner actor;

    /// <summary>
    /// 用途：指定第 058 关等待跳跃阶段与水平区域力阶段使用的控制器。
    /// 调用边界：EnableRightwardJump API 先关闭区域力；后续跳跃事件结束后再设置力值、Force Angle 并重新开启。
    /// 注意事项：区域力角度固定切换为 0 度；Collider、Mask、Drag 与 Force Target 继续沿用当前关卡配置。
    /// </summary>
    [Tooltip("等待跳跃后切换为 0 度并开启的区域重力控制器；留空时从当前对象查找")]
    [SerializeField] private IWannerGame_GravityCtrl gravityCtrl;

    /// <summary>
    /// 用途：配置跳跃事件后写入 AreaEffector2D 的 Force Magnitude。
    /// 调用边界：仅在 EnableRightwardJump 已开启门禁且收到有效 PlayerJumpEvent 后应用。
    /// 注意事项：保留 Unity AreaEffector2D 的原始数值语义；负值会让 0 度区域力实际反向。
    /// </summary>
    [Tooltip("跳跃事件后写入 AreaEffector2D 的 Force Magnitude；负值会让 0 度区域力反向")]
    [SerializeField] private float forceMagnitude = 150f;

    #endregion

    #region Runtime State

    private EventSubscriber _subscriber;
    private bool _isAreaEffectJumpEnabled;
    private bool _isAreaEffectActivationPending;

    #endregion

    #region Public API

    /// <summary>
    /// 用途：关闭当前 AreaEffector2D、清除 Actor 自身重力，并开启等待下一次跳跃事件的阶段门禁。
    /// 调用边界：供本关 TriggerExecutor 的 UnityEvent 或其它明确的运行时关卡逻辑调用，可安全重复调用。
    /// 注意事项：本 API 不禁止 Actor 移动；移动锁定会在后续 PlayerJumpEvent 的同步回调结束后与区域力一起应用。
    /// </summary>
    public void EnableRightwardJump()
    {
        if (!Application.isPlaying)
        {
            this.LogWarning("[ILevel058Ctrl] EnableRightwardJump is only available in PlayMode.");
            return;
        }

        ResolveReferences();
        if (gravityCtrl != null)
            gravityCtrl.SetGravityEnabled(false);
        else
            this.LogWarning("[ILevel058Ctrl] IWannerGame_GravityCtrl is missing; AreaEffector2D cannot enter the waiting stage.");

        Rigidbody2D actorBody = ResolveActorBody();
        if (actorBody != null)
        {
            actor.PreserveCurrentGroundCheckDirection();
            actorBody.gravityScale = 0f;
        }
        else
            this.LogWarning("[ILevel058Ctrl] Actor_IWanner or its Rigidbody2D is missing; gravityScale cannot be cleared and jump events will be ignored.");

        _isAreaEffectJumpEnabled = true;
        _isAreaEffectActivationPending = false;
    }

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<PlayerJumpEvent>(OnPlayerJump);
    }

    private void OnDisable()
    {
        _subscriber?.Dispose();
        _subscriber = null;
        _isAreaEffectActivationPending = false;
    }

    private void LateUpdate()
    {
        if (!_isAreaEffectActivationPending)
            return;

        _isAreaEffectActivationPending = false;
        ApplyAreaEffectAfterJump();
    }

    #endregion

    #region Jump Event

    private void OnPlayerJump(PlayerJumpEvent evt)
    {
        if (!_isAreaEffectJumpEnabled || actor == null || actor.IsDied)
            return;

        Rigidbody2D actorBody = ResolveActorBody();
        if (!CanControlBody(actorBody))
            return;

        actor.transform.Rotate(0f, 0f, 90f, Space.Self);
        _isAreaEffectActivationPending = true;
    }

    #endregion

    #region Internal

    private void ResolveReferences()
    {
        if (actor == null)
            actor = GetComponentInChildren<Actor_IWanner>(true);

        if (gravityCtrl == null)
            gravityCtrl = GetComponent<IWannerGame_GravityCtrl>();
    }

    private Rigidbody2D ResolveActorBody()
    {
        if (actor == null)
            return null;

        return actor.RB != null
            ? actor.RB
            : actor.GetComponent<Rigidbody2D>();
    }

    private static bool CanControlBody(Rigidbody2D actorBody)
    {
        return actorBody != null
               && actorBody.simulated
               && actorBody.bodyType != RigidbodyType2D.Static;
    }

    private void ApplyAreaEffectAfterJump()
    {
        ResolveReferences();
        if (actor != null)
        {
            actor.SetMoveInput(0f);
            actor.StopMoving();
        }

        if (gravityCtrl == null)
        {
            this.LogWarning("[ILevel058Ctrl] IWannerGame_GravityCtrl is missing; jump AreaEffector2D settings cannot be applied.");
            return;
        }

        gravityCtrl.SetForceMagnitude(forceMagnitude);
        gravityCtrl.SetForceAngle(0f);
        gravityCtrl.SetGravityEnabled(true);

        Rigidbody2D actorBody = ResolveActorBody();
        if (CanControlBody(actorBody))
            actorBody.WakeUp();
    }

    #endregion
}
