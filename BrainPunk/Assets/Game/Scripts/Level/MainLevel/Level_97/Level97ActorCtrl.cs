using UnityEngine;

/// <summary>
/// Level097 专用角色控制器；载人飞行时只屏蔽跳跃，保留继承的左右移动。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level97ActorCtrl : ActorCtrl
{
    #region Runtime State

    private bool isBalloonFlying;

    #endregion

    #region Level097 API

    /// <summary>
    /// 设置角色是否正在被气球带飞；仅影响跳跃入口，不关闭左右移动。
    /// 调用边界：只由同一 Level097 实例上的 Level97Ctrl 在吸附、失败恢复和销毁清理时调用。
    /// 注意事项：传入 true 后仍保留基类 OnUpdate，调用方必须继续负责角色的竖直速度和重力状态。
    /// </summary>
    /// <param name="isFlying">true 表示气球载人飞行中。</param>
    public void SetBalloonFlying(bool isFlying)
    {
        isBalloonFlying = isFlying;
    }

    #endregion

    #region ActorCtrl Overrides

    protected override void HandleLanding()
    {
        if (isBalloonFlying)
            return;

        base.HandleLanding();
    }

    protected override void JumpButton()
    {
        if (isBalloonFlying)
            return;

        base.JumpButton();
    }

    #endregion
}
