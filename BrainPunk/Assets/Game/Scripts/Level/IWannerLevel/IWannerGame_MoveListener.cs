using Bear.EventSystem;
using Game.Events;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// IWanner 玩家移动输入监听器。
/// 监听玩家左移、右移和跳跃事件，并将任一输入转发到统一的本地 UnityEvent。
/// </summary>
[DisallowMultipleComponent]
public sealed class IWannerGame_MoveListener : MonoBehaviour
{
    #region Serialized Fields

    /// <summary>
    /// 用途：控制当前组件是否只响应第一次左移、右移或跳跃事件。
    /// 调用边界：首次事件到达时先释放三类输入订阅，再调用 onMove；默认关闭以保持已有组件的连续监听行为。
    /// 注意事项：一次性状态在同一组件实例再次禁用、启用时不会重置；重建关卡实例后才会重新监听。
    /// </summary>
    [Tooltip("启用后只响应第一次左移、右移或跳跃事件，触发后立即停止监听")]
    [SerializeField] private bool isOnce;

    /// <summary>
    /// 用途：配置收到左移、右移或跳跃事件后需要执行的关卡本地回调。
    /// 调用边界：仅在 PlayMode 且本组件启用时触发；左右移动按住期间会跟随现有输入事件逐帧调用。
    /// 注意事项：该回调不区分输入类型，也不监听 PlayerMoveCancelEvent；需要区分方向时应使用专用监听器。
    /// </summary>
    [Tooltip("收到玩家左移、右移或跳跃事件时调用；左右按住期间会逐帧触发")]
    [SerializeField] private UnityEvent onMove = new();

    #endregion

    #region Runtime State

    private EventSubscriber _subscriber;
    private bool _hasTriggeredOnce;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying || (isOnce && _hasTriggeredOnce))
            return;

        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<PlayerRightMoveEvent>(OnPlayerRightMove);
        _subscriber.Subscribe<PlayerLeftMoveEvent>(OnPlayerLeftMove);
        _subscriber.Subscribe<PlayerJumpEvent>(OnPlayerJump);
    }

    private void OnDisable()
    {
        StopListening();
    }

    #endregion

    #region Player Input Events

    private void OnPlayerRightMove(PlayerRightMoveEvent evt)
    {
        HandleMove();
    }

    private void OnPlayerLeftMove(PlayerLeftMoveEvent evt)
    {
        HandleMove();
    }

    private void OnPlayerJump(PlayerJumpEvent evt)
    {
        HandleMove();
    }

    private void HandleMove()
    {
        if (isOnce && _hasTriggeredOnce)
            return;

        if (isOnce)
        {
            _hasTriggeredOnce = true;
            StopListening();
        }

        onMove?.Invoke();
    }

    #endregion

    #region Internal

    private void StopListening()
    {
        _subscriber?.Dispose();
        _subscriber = null;
    }

    #endregion
}
