using Bear.Logger;
using UnityEngine;

/// <summary>
/// Level112 主角专用动画控制器；挂上误导拉环后托管 hang，避免共享移动动画覆盖悬挂姿势。
/// </summary>
public sealed class Level112ActorCtrl : ActorCtrl, IDebuger
{
    private static readonly string[] HangAnimationNames =
    {
        "hang",
        "controlhandle",
        "lift01_idle"
    };

    private bool isHangAnimationControlled;

    #region 共享动画覆盖

    protected override void HandleAnimators()
    {
        if (isHangAnimationControlled || !IsSpineReady)
            return;

        base.HandleAnimators();
    }

    /// <summary>
    /// 开始托管 Level112 拉环悬挂动画；动画资源缺失时恢复通用动画，但不阻断拉环运动。
    /// </summary>
    public bool BeginHangAnimation()
    {
        if (IsDied)
            return false;

        string animationName = ResolveHangAnimationName();
        if (string.IsNullOrEmpty(animationName))
        {
            this.LogWarning(
                $"[Level112ActorCtrl] Hang animation not found. Candidates: {string.Join(", ", HangAnimationNames)}");
            RestoreDefaultAnimation();
            return false;
        }

        Spine.TrackEntry hangEntry = SpineCtrl.PlayAnimation(animationName, true, false);
        if (hangEntry == null)
        {
            this.LogWarning($"[Level112ActorCtrl] Failed to play animation: {animationName}");
            RestoreDefaultAnimation();
            return false;
        }

        isHangAnimationControlled = true;
        return true;
    }

    /// <summary>
    /// 结束本关悬挂动画托管并恢复共享角色动画；供拉环卸载、关卡退出时重复调用。
    /// </summary>
    public void RestoreDefaultAnimation()
    {
        isHangAnimationControlled = false;
        if (isActiveAndEnabled && !IsDied && IsSpineReady)
            UpdateAnimation();
    }

    #endregion

    #region Spine 安全检查

    private bool IsSpineReady =>
        SpineCtrl != null &&
        SpineCtrl.AnimationState != null &&
        SpineCtrl.AnimationState.Data?.SkeletonData != null;

    private string ResolveHangAnimationName()
    {
        if (SpineCtrl == null || SpineCtrl.AnimationState == null)
        {
            this.LogWarning("[Level112ActorCtrl] SpineCtrl or AnimationState is missing.");
            return null;
        }

        Spine.SkeletonData skeletonData = SpineCtrl.AnimationState.Data?.SkeletonData;
        if (skeletonData == null)
            return null;

        foreach (string animationName in HangAnimationNames)
        {
            if (skeletonData.FindAnimation(animationName) != null)
                return animationName;
        }

        return null;
    }

    private void OnDisable()
    {
        isHangAnimationControlled = false;
    }

    #endregion
}
