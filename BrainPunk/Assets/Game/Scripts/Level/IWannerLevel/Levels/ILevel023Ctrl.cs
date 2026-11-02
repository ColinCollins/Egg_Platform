using Bear.Logger;
using UnityEngine;

/// <summary>
/// IWanner 第 23 小关局部控制器。
/// 负责在关卡机制触发时开放指定陷阱的动态物理与跳跃事件监听。
/// </summary>
[DisallowMultipleComponent]
public sealed class ILevel023Ctrl : MonoBehaviour, IDebuger
{
    #region Serialized Fields

    /// <summary>
    /// 用途：指定 IWannerGame_Trap(1) 上需要切换为 Dynamic 的 Rigidbody2D。
    /// 调用边界：仅由本关的 EnableTrapJump API 在运行时修改 Body Type。
    /// 注意事项：本控制器不修改质量、重力、约束、速度或其它物理参数。
    /// </summary>
    [Tooltip("IWannerGame_Trap(1) 上需要切换为 Dynamic 的 Rigidbody2D")]
    [SerializeField] private Rigidbody2D trapRigidbody;

    /// <summary>
    /// 用途：指定 IWannerGame_Trap(1) 上响应玩家跳跃事件的监听器。
    /// 调用边界：仅由本关的 EnableTrapJump API 开启；监听器负责实际写入跳跃速度。
    /// 注意事项：应与 trapRigidbody 指向同一个陷阱对象。
    /// </summary>
    [Tooltip("IWannerGame_Trap(1) 上的跳跃事件监听器")]
    [SerializeField] private IWannerGame_JumpListener trapJumpListener;

    #endregion

    #region Public API

    /// <summary>
    /// 将 IWannerGame_Trap(1) 切换为 Dynamic，并开启其玩家跳跃事件监听。
    /// 可由本关 TriggerExecutor 的 UnityEvent 或其它明确的本关运行时逻辑重复调用。
    /// 引用缺失时仅记录警告，不修改其它对象，也不影响 Actor 自身的跳跃开关。
    /// </summary>
    public void EnableTrapJump()
    {
        if (!Application.isPlaying)
        {
            this.LogWarning("[ILevel023Ctrl] EnableTrapJump is only available in PlayMode.");
            return;
        }

        if (trapRigidbody == null || trapJumpListener == null)
        {
            this.LogWarning("[ILevel023Ctrl] Trap Rigidbody2D or jump listener is missing.");
            return;
        }

        trapRigidbody.bodyType = RigidbodyType2D.Dynamic;
        trapRigidbody.WakeUp();
        trapJumpListener.enabled = true;
    }

    #endregion
}
