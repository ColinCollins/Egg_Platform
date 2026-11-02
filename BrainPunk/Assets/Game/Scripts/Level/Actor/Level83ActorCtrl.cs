using Bear.Logger;
using Game.Level083;
using UnityEngine;

/// <summary>
/// Level 083 主角专用动画控制器；隔离演奏与庆祝动画，避免扩展共享 ActorCtrl。
/// </summary>
public sealed class Level83ActorCtrl : ActorCtrl, IDebuger
{
    #region Animation Names

    private const string GuitarAnimationName = "play_guitar";
    private const string HarpAnimationName = "play_harp";
    private const string SaxophoneAnimationName = "play_sax";
    private const string CelebrateAnimationName = "celebrate";

    #endregion

    #region Runtime State

    private bool isLevel83AnimationControlled;
    private bool hasRigidbodyConstraintsSnapshot;
    private RigidbodyConstraints2D originalRigidbodyConstraints;

    #endregion

    #region ActorCtrl Overrides

    protected override void HandleAnimators()
    {
        if (isLevel83AnimationControlled)
            return;

        base.HandleAnimators();
    }

    #endregion

    #region Level 083 API

    /// <summary>
    /// 开始由 Level 083 托管主角动画；仅在玩家选定乐器后调用，退出本关流程时必须配对恢复。
    /// </summary>
    public void BeginLevel83AnimationControl()
    {
        if (IsDied)
            return;

        SetMoveInput(0f);
        LockHorizontalPosition();
        isLevel83AnimationControlled = true;
    }

    /// <summary>
    /// 播放所选乐器的循环演奏动画；仅接受 Level 083 的乐器类型，不负责谱面判定或音效。
    /// </summary>
    /// <param name="instrument">当前已选定且不可再次切换的 Level 083 乐器。</param>
    public void PlayPerformanceAnimation(Level83Instrument instrument)
    {
        string animationName = instrument switch
        {
            Level83Instrument.Guitar => GuitarAnimationName,
            Level83Instrument.Harp => HarpAnimationName,
            Level83Instrument.Saxophone => SaxophoneAnimationName,
            _ => string.Empty
        };

        PlayLevel83Animation(animationName);
    }

    /// <summary>
    /// 播放 Level 083 胜利庆祝循环动画；只负责角色表现，不触发关卡成功或奖励逻辑。
    /// </summary>
    public void PlayCelebrateAnimation()
    {
        PlayLevel83Animation(CelebrateAnimationName);
    }

    /// <summary>
    /// 结束 Level 083 动画托管并恢复通用角色动画；失败、重开、退出和对象清理时均可安全重复调用。
    /// </summary>
    /// <param name="preserveMoveInput">
    /// true 表示保留当前移动输入：仅用于庆祝循环被移动输入打断的场景，
    /// 事件订阅顺序不定，打断与 BaseLevelCtrl 同事件写入输入不得互相吞掉；
    /// 其余解除托管场景一律传 false 清零输入。
    /// </param>
    public void RestoreDefaultAnimation(bool preserveMoveInput = false)
    {
        isLevel83AnimationControlled = false;
        RestoreHorizontalPositionConstraint();
        if (!preserveMoveInput)
            SetMoveInput(0f);

        if (isActiveAndEnabled && !IsDied)
            UpdateAnimation();
    }

    #endregion

    #region Internal Helpers

    private void PlayLevel83Animation(string animationName)
    {
        if (IsDied || string.IsNullOrEmpty(animationName))
            return;

        if (!HasAnimation(animationName))
        {
            this.LogWarning($"[Level83ActorCtrl] Animation not found: {animationName}");
            RestoreDefaultAnimation();
            return;
        }

        isLevel83AnimationControlled = true;
        SpineCtrl.PlayAnimation(animationName, true, false);
    }

    private bool HasAnimation(string animationName)
    {
        if (SpineCtrl == null || SpineCtrl.AnimationState == null)
        {
            this.LogWarning("[Level83ActorCtrl] SpineCtrl or AnimationState is missing.");
            return false;
        }

        return SpineCtrl.AnimationState.Data?.SkeletonData?.FindAnimation(animationName) != null;
    }

    /// <summary>
    /// 演奏锁定期间保留主角当前水平站位，防止边缘接触产生的物理横向速度把主角带出演奏台。
    /// 只冻结 X，继续保留 Y 轴重力与台面承托；解除锁定时必须恢复原始 Rigidbody2D 约束。
    /// </summary>
    private void LockHorizontalPosition()
    {
        if (RB == null)
            return;

        if (!hasRigidbodyConstraintsSnapshot)
        {
            originalRigidbodyConstraints = RB.constraints;
            hasRigidbodyConstraintsSnapshot = true;
        }

        RB.linearVelocity = new Vector2(0f, RB.linearVelocityY);
        RB.constraints = originalRigidbodyConstraints | RigidbodyConstraints2D.FreezePositionX;
    }

    private void RestoreHorizontalPositionConstraint()
    {
        if (!hasRigidbodyConstraintsSnapshot)
            return;

        if (RB != null)
        {
            RB.linearVelocity = new Vector2(0f, RB.linearVelocityY);
            RB.constraints = originalRigidbodyConstraints;
        }

        hasRigidbodyConstraintsSnapshot = false;
    }

    private void OnDisable()
    {
        isLevel83AnimationControlled = false;
        RestoreHorizontalPositionConstraint();
    }

    #endregion
}
