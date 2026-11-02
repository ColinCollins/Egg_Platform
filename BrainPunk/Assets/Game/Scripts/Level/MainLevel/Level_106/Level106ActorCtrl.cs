using Game.Level.Actor;
using UnityEngine;

/// <summary>
/// 本关登乘等待与飞升期间的 idle 所有者；普通移动、碰撞和动画仍继承 ActorCtrl。
/// 不在 LateUpdate 抢写轨道，避免共享落地判定与 idle 反复切换。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level106ActorCtrl : ActorCtrl
{
    private bool ownsBoardingIdle;
    private bool boardingIdleApplied;
    private ActorSkinBinding skinBinding;

    public void BeginBoardingIdle()
    {
        if (IsDied || ownsBoardingIdle)
            return;

        ownsBoardingIdle = true;
        boardingIdleApplied = false;
        TryApplyBoardingIdle();
    }

    public void EndBoardingIdle(bool updateAnimation = true)
    {
        bool wasOwned = ownsBoardingIdle;
        ownsBoardingIdle = false;
        boardingIdleApplied = false;
        if (wasOwned && updateAnimation && isActiveAndEnabled && !IsDied && IsSpineReady)
            UpdateAnimation();
    }

    protected override void HandleAnimators()
    {
        if (IsDied || !IsSpineReady)
            return;

        if (ownsBoardingIdle)
        {
            TryApplyBoardingIdle();
            return;
        }

        base.HandleAnimators();
    }

    private bool IsSpineReady => SpineCtrl != null &&
        SpineCtrl.AnimationState?.Data?.SkeletonData != null;

    private void TryApplyBoardingIdle()
    {
        if (!ownsBoardingIdle || boardingIdleApplied || IsDied || !IsSpineReady)
            return;

        if (SpineCtrl.AnimationState.Data.SkeletonData.FindAnimation("idle") == null)
        {
            ownsBoardingIdle = false;
            Debug.LogWarning("Level 106 Actor 缺少 idle 动画，保留普通动画。", this);
            return;
        }

        boardingIdleApplied = SpineCtrl.PlayAnimation("idle", true, false) != null;
        if (!boardingIdleApplied)
        {
            ownsBoardingIdle = false;
            Debug.LogWarning("Level 106 Actor idle 应用失败，保留普通动画。", this);
        }
    }

    public override void Die()
    {
        EndBoardingIdle(false);
        base.Die();
    }

    private void OnEnable()
    {
        skinBinding = GetComponent<ActorSkinBinding>();
        if (skinBinding != null)
            skinBinding.CurrentSelectionApplied += OnSkinApplied;
        boardingIdleApplied = false;
    }

    private void OnSkinApplied(ActorSkinBinding _)
    {
        boardingIdleApplied = false;
        TryApplyBoardingIdle();
    }

    private void OnDisable()
    {
        if (skinBinding != null)
            skinBinding.CurrentSelectionApplied -= OnSkinApplied;
        skinBinding = null;
        boardingIdleApplied = false;
    }
}
