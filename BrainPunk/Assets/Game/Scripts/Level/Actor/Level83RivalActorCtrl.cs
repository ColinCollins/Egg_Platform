using Bear.Logger;

/// <summary>
/// Level 083 Box Bad 专用角色控制器；演奏失败冲撞链期间托管动画，
/// 避免共享 ActorCtrl 状态机的推实体物（push）判定劫持冲撞表现。
/// 模式与同关 Level83ActorCtrl 的动画托管门禁同构，先例为 Level 017/060/066 狗敌人与 Level 020 门敌人。
/// 需求来源：docs/issues/level-083-rival-animation-gate/PRD.md。
/// </summary>
public sealed class Level83RivalActorCtrl : ActorCtrl, IDebuger
{
    #region Animation Names

    /// <summary>冲撞期间循环播放的动画。</summary>
    private const string ChargeAnimationName = "walk01";

    /// <summary>冲撞命中后的收口定格动画；PRD 决策 D2 默认保持 walk01 停位移，如需 idle 仅改此处。</summary>
    private const string ChargeStopAnimationName = "walk01";

    #endregion

    #region Runtime State

    private bool isChargeAnimationControlled;

    /// <summary>当前是否处于冲撞动画托管；供玩法与测试观察。</summary>
    public bool IsChargeAnimationControlled => isChargeAnimationControlled;

    #endregion

    #region ActorCtrl Overrides

    protected override void HandleAnimators()
    {
        // 门禁生效期间共享状态机整体闭嘴：含推实体物判定在内的所有分支都不执行。
        if (isChargeAnimationControlled)
            return;

        base.HandleAnimators();
    }

    #endregion

    #region Level 083 Charge Animation API

    /// <summary>
    /// 开始冲撞动画托管；只在演奏失败链入口（PlayAngryThenCharge）调用。
    /// 约束 C2：开启后保持到关卡重开、退出或对象销毁，不得提前关闭。
    /// </summary>
    public void BeginChargeAnimationControl()
    {
        if (IsDied)
            return;

        isChargeAnimationControlled = true;
    }

    /// <summary>显式循环播放冲撞动画 walk01；门禁开启期间冲撞全程保持。</summary>
    public void PlayChargeAnimation()
    {
        PlayChargeControlledAnimation(ChargeAnimationName);
    }

    /// <summary>冲撞命中主角后显式播放收口定格动画；调用前移动输入必须已清零。</summary>
    public void PlayChargeStopAnimation()
    {
        PlayChargeControlledAnimation(ChargeStopAnimationName);
    }

    /// <summary>
    /// 结束动画托管并恢复共享状态机。
    /// 约束 C1：先清零移动输入再关门禁，避免同帧“输入非零 + 射线命中”闪现一帧 push。
    /// </summary>
    public void RestoreDefaultAnimation()
    {
        SetMoveInput(0f);
        isChargeAnimationControlled = false;

        if (isActiveAndEnabled && !IsDied)
            UpdateAnimation();
    }

    #endregion

    #region Internal Helpers

    private void PlayChargeControlledAnimation(string animationName)
    {
        if (IsDied || string.IsNullOrEmpty(animationName))
            return;

        if (!isChargeAnimationControlled)
        {
            this.LogWarning($"[Level83RivalActorCtrl] 门禁未开启，拒绝播放冲撞动画：{animationName}。约束 C3：移动必须经过门禁。");
            return;
        }

        if (!HasAnimation(animationName))
        {
            this.LogWarning($"[Level83RivalActorCtrl] Animation not found: {animationName}");
            RestoreDefaultAnimation();
            return;
        }

        SpineCtrl.PlayAnimation(animationName, true, false);
    }

    private bool HasAnimation(string animationName)
    {
        if (SpineCtrl == null || SpineCtrl.AnimationState == null)
        {
            this.LogWarning("[Level83RivalActorCtrl] SpineCtrl or AnimationState is missing.");
            return false;
        }

        return SpineCtrl.AnimationState.Data?.SkeletonData?.FindAnimation(animationName) != null;
    }

    private void OnDisable()
    {
        isChargeAnimationControlled = false;
    }

    #endregion
}
