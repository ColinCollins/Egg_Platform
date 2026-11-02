using Bear.Logger;
using UnityEngine;

/// <summary>
/// Level 125 主角专用动画控制器；吊环挂载期间托管 hang，避免共享移动动画覆盖挂载姿势。
/// </summary>
public sealed class Level125ActorCtrl : ActorCtrl, IDebuger
{
    private const string HangAnimationName = "hang";

    private bool isHangAnimationControlled;

    protected override void HandleAnimators()
    {
        if (isHangAnimationControlled || !IsSpineReady)
            return;

        base.HandleAnimators();
    }

    /// <summary>
    /// 开始由 Level 125 吊环托管主角动画；动画缺失时保留通用动画，不阻断机关流程。
    /// </summary>
    public bool BeginHangAnimation()
    {
        if (IsDied)
            return false;

        if (!HasAnimation(HangAnimationName))
        {
            this.LogWarning($"[Level125ActorCtrl] Animation not found: {HangAnimationName}");
            RestoreDefaultAnimation();
            return false;
        }

        Spine.TrackEntry hangEntry = SpineCtrl.PlayAnimation(HangAnimationName, true, false);
        if (hangEntry == null)
        {
            this.LogWarning($"[Level125ActorCtrl] Failed to play animation: {HangAnimationName}");
            RestoreDefaultAnimation();
            return false;
        }

        isHangAnimationControlled = true;
        return true;
    }

    /// <summary>
    /// 结束吊环动画托管并恢复共享角色动画；可重复调用。
    /// </summary>
    public void RestoreDefaultAnimation()
    {
        isHangAnimationControlled = false;
        if (isActiveAndEnabled &&
            !IsDied &&
            IsSpineReady)
            UpdateAnimation();
    }

    private bool IsSpineReady =>
        SpineCtrl != null &&
        SpineCtrl.AnimationState != null &&
        SpineCtrl.AnimationState.Data?.SkeletonData != null;

    private bool HasAnimation(string animationName)
    {
        if (SpineCtrl == null || SpineCtrl.AnimationState == null)
        {
            this.LogWarning("[Level125ActorCtrl] SpineCtrl or AnimationState is missing.");
            return false;
        }

        return SpineCtrl.AnimationState.Data?.SkeletonData?.FindAnimation(animationName) != null;
    }

    private void OnDisable()
    {
        isHangAnimationControlled = false;
    }
}
