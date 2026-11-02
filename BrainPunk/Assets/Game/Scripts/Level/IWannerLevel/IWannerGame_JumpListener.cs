using Bear.EventSystem;
using Bear.Logger;
using Game.Events;
using UnityEngine;

/// <summary>
/// IWanner 目标刚体跳跃事件监听器。
/// 监听玩家跳跃事件，并通过 Rigidbody2D 让指定目标产生向上的跳跃速度。
/// </summary>
[DisallowMultipleComponent]
public sealed class IWannerGame_JumpListener : MonoBehaviour, IDebuger
{
    #region Serialized Fields

    /// <summary>
    /// 用途：指定响应玩家跳跃事件的目标 Rigidbody2D。
    /// 调用边界：运行时收到 PlayerJumpEvent 时写入其垂直速度；未配置时尝试获取当前对象上的 Rigidbody2D。
    /// 注意事项：目标应启用物理模拟并允许 Y 轴移动；本组件不修改 Body Type、Gravity Scale 或约束。
    /// </summary>
    [Tooltip("响应玩家跳跃事件的目标 Rigidbody2D；留空时使用当前对象上的 Rigidbody2D")]
    [SerializeField] private Rigidbody2D target;

    /// <summary>
    /// 用途：设置目标每次响应跳跃事件时获得的向上速度。
    /// 调用边界：每次 PlayerJumpEvent 都会覆盖目标当前 Y 速度，并保留 X 速度。
    /// 注意事项：运行时按非负值处理；小于等于 0 时忽略本次跳跃事件。
    /// </summary>
    [Tooltip("目标响应跳跃事件时获得的向上速度")]
    [Min(0f)]
    [SerializeField] private float jumpVelocity = 18f;

    #endregion

    #region Runtime State

    private EventSubscriber _subscriber;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        ResolveTarget();
        if (target == null)
        {
            this.LogWarning("[IWannerGame_JumpListener] Target Rigidbody2D is missing.");
            return;
        }

        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<PlayerJumpEvent>(OnPlayerJump);
    }

    private void OnDisable()
    {
        _subscriber?.Dispose();
        _subscriber = null;
    }

    #endregion

    #region Jump Event

    private void OnPlayerJump(PlayerJumpEvent evt)
    {
        if (target == null || !target.simulated || target.bodyType == RigidbodyType2D.Static)
            return;

        float effectiveJumpVelocity = Mathf.Max(0f, jumpVelocity);
        if (effectiveJumpVelocity <= 0f)
            return;

        target.linearVelocity = new Vector2(target.linearVelocityX, effectiveJumpVelocity);
    }

    #endregion

    #region Internal

    private void ResolveTarget()
    {
        if (target == null)
            target = GetComponent<Rigidbody2D>();
    }

    #endregion
}
