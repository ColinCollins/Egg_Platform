using Bear.Logger;
using UnityEngine;

/// <summary>
/// 71 关角色局部动作控制器，仅承载长椅睡眠动画，不向共享 ActorCtrl 注入单关状态。
/// </summary>
public sealed class Level71ActorCtrl : ActorCtrl, IDebuger
{
    #region Constants

    private const string SleepAnimationName = "sofa_sleep";

    #endregion

    #region Public API

    /// <summary>
    /// 用途：角色持续坐在长椅 3 秒后切换到循环睡眠动画。
    /// 调用边界：仅由当前 Level71Ctrl 在运行态且角色仍处于坐下状态时调用。
    /// 注意事项：未坐下、角色死亡或 Spine 未绑定时不会切换动画。
    /// </summary>
    /// <returns>成功进入睡眠循环时返回 true。</returns>
    public bool PlaySleepLoop()
    {
        if (!Application.isPlaying || !IsSit || IsDied || SpineCtrl == null)
            return false;

        SpineCtrl.PlayAnimation(SleepAnimationName, true, false);
        return true;
    }

    #endregion
}
