using System;
using Game.Level.Actor;
using UnityEngine;

/// <summary>
/// 本关 Actor 的动画所有者。飞行时阻止共享 idle/jump 切换，不靠 LateUpdate 反复重播火箭姿态。
/// 移动、跳跃、碰撞检测和正常动画仍继承 ActorCtrl；死亡前通知只用于本关碰刺反馈。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level131ActorCtrl : ActorCtrl
{
    private const string FlightAnimationName = "rocket_fly";
    private bool ownsFlightAnimation;
    private bool flightAnimationApplied;
    private ActorSkinBinding skinBinding;

    public event Action Dying;

    public void BeginBackpackFlightAnimation()
    {
        if (IsDied || ownsFlightAnimation)
            return;

        ownsFlightAnimation = true;
        flightAnimationApplied = false;
        TryApplyFlightAnimation();
    }

    public void RestoreDefaultAnimation()
    {
        ownsFlightAnimation = false;
        flightAnimationApplied = false;
        if (isActiveAndEnabled && !IsDied && IsSpineReady)
            UpdateAnimation();
    }

    protected override void HandleAnimators()
    {
        if (IsDied || !IsSpineReady)
            return;

        if (ownsFlightAnimation)
        {
            // 仅处理 Spine 尚未就绪的首次应用；成功后不再写轨道，直到状态切换/皮肤重建。
            TryApplyFlightAnimation();
            return;
        }

        base.HandleAnimators();
    }

    private bool IsSpineReady => SpineCtrl != null &&
        SpineCtrl.AnimationState?.Data?.SkeletonData != null;

    private void TryApplyFlightAnimation()
    {
        if (!ownsFlightAnimation || flightAnimationApplied || IsDied || !IsSpineReady)
            return;

        if (SpineCtrl.AnimationState.Data.SkeletonData.FindAnimation(FlightAnimationName) == null)
        {
            ownsFlightAnimation = false;
            Debug.LogWarning("Level 131 Actor 缺少 rocket_fly 动画，保留普通动画，不反复写入无效轨道。", this);
            return;
        }

        flightAnimationApplied = SpineCtrl.PlayAnimation(FlightAnimationName, true, false) != null;
        if (!flightAnimationApplied)
        {
            ownsFlightAnimation = false;
            Debug.LogWarning("Level 131 Actor 火箭姿态应用失败，保留普通动画。", this);
        }
    }

    public override void Die()
    {
        if (IsDied)
            return;

        ownsFlightAnimation = false;
        flightAnimationApplied = false;
        try
        {
            // 即使由共享陷阱直接调用 Die，本关仍可在死亡动画之前处理真实接触反馈。
            Dying?.Invoke();
        }
        catch (Exception exception)
        {
            // 视觉反馈异常不能阻止原有死亡流程。
            Debug.LogException(exception, this);
        }
        base.Die();
    }

    private void OnEnable()
    {
        skinBinding = GetComponent<ActorSkinBinding>();
        if (skinBinding != null)
            skinBinding.CurrentSelectionApplied += OnSkinApplied;
        flightAnimationApplied = false;
    }

    private void OnSkinApplied(ActorSkinBinding _)
    {
        // 皮肤异步重建完成后只重新应用一次；不是每帧检查当前动画再强行纠正。
        flightAnimationApplied = false;
        TryApplyFlightAnimation();
    }

    private void OnDisable()
    {
        if (skinBinding != null)
            skinBinding.CurrentSelectionApplied -= OnSkinApplied;
        skinBinding = null;
        // 临时禁用保留玩法所有权，再启用时只重新应用一次；死亡/退出飞行显式清除所有权。
        flightAnimationApplied = false;
    }
}
