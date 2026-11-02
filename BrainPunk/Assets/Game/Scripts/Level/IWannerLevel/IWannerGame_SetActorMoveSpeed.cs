using Bear.Logger;
using Game.ItemEvent;
using UnityEngine;

/// <summary>
/// IWanner 角色移动速度设置器，由执行器或 UnityEvent 调用 Execute 后立即设置基础移动速度。
/// 只影响显式绑定的 Actor 实例；不自动触发、不恢复旧速度，也不改变跳跃或外部附加速度。
/// </summary>
public sealed class IWannerGame_SetActorMoveSpeed : BaseItemEventHandle, IDebuger
{
    #region 序列化配置

    /// <summary>
    /// 用途：指定本次修改速度的 IWanner Actor；必须在 Inspector 显式绑定当前关卡实例。
    /// 调用边界：只在运行时 Execute 时访问；缺失时跳过设置并结束执行，不扫描层级。
    /// </summary>
    [Tooltip("需要修改移动速度的 IWanner Actor，必须显式绑定")]
    [SerializeField] private Actor_IWanner target;

    /// <summary>
    /// 用途：设置角色基础移动速度的绝对值，单位为世界单位/秒，默认 5。
    /// 调用边界：Execute 时应用；负值按 0 处理，非有限值拒绝应用。
    /// 注意事项：0 只取消玩家输入产生的水平移动，平台和机关附加速度仍按原逻辑生效。
    /// </summary>
    [Tooltip("目标基础移动速度（世界单位/秒），不是倍率；0 不会锁定角色或清除外力")]
    [Min(0f)]
    [SerializeField] private float moveSpeed = 5f;

    #endregion

    #region 执行入口

    /// <summary>
    /// 用途：立即把目标 Actor 的基础移动速度设置为 Inspector 配置值。
    /// 调用边界：供 TriggerExecutor/BaseItemExecutor、UnityEvent 或关卡局部逻辑调用，仅 PlayMode 生效。
    /// 注意事项：重复调用覆盖当前速度；ResetState 仅重置执行状态，不恢复旧速度。
    /// 目标缺失或数值非法时记录警告并标记完成，避免顺序执行器等待不结束。
    /// </summary>
    public override void Execute()
    {
        if (!Application.isPlaying)
            return;

        IsRunning = true;
        IsDone = false;

        if (target == null)
            this.LogWarning("[IWannerGame_SetActorMoveSpeed] 未绑定目标 Actor，跳过速度设置。");
        else if (float.IsNaN(moveSpeed) || float.IsInfinity(moveSpeed))
            this.LogWarning("[IWannerGame_SetActorMoveSpeed] 移动速度必须为有限数值，跳过速度设置。");
        else
            target.SetMoveSpeed(Mathf.Max(0f, moveSpeed));

        IsRunning = false;
        IsDone = true;
    }

    #endregion
}
