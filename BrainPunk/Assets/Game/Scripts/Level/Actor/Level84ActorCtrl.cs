using Bear.Logger;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Level84 actor-specific actions for turning pages, soccer shot, and goalkeeper save.
/// </summary>
public class Level84ActorCtrl : ActorCtrl, IDebuger
{
    private const string TurningThePageAnimName = "turning_the_pages";
    private const string ShootReadyAnimName = "football_shoot_ready";
    private const string ShootAnimName = "football_shoot";
    private const string GoalkeeperSaveAnimName = "football_save";
    private const string BlockFailureAnimName = "blockFailure";
    private bool isSoccerActionPlaying = false;

    public bool IsSoccerActionPlaying => isSoccerActionPlaying;

    public override void OnUpdate()
    {
        if (isSoccerActionPlaying)
        {
            UpdateLockedActionState();
            return;
        }

        base.OnUpdate();
    }

    protected override void HandleAnimators()
    {
        if (isSoccerActionPlaying)
            return;

        base.HandleAnimators();
    }

    public async UniTask PlayFootballShoot()
    {
        await PlaySoccerAction(ShootReadyAnimName, ShootAnimName);
    }

    public async UniTask PlayGoalkeeperSave()
    {
        if (IsDied)
        {
            return;
        }

        isSoccerActionPlaying = true;
        StopAllMotion();

        await PlayAnimationOrDelay(GoalkeeperSaveAnimName, 0.35f);

        isSoccerActionPlaying = false;
        if (!IsDied)
        {
            UpdateAnimation();
        }
    }

    /// <summary>
    /// 播放足球守门失败动作；仅用于关卡表现，不改变胜负、存档、事件或物理状态。
    /// </summary>
    public async UniTask PlayBlockFailure()
    {
        await PlaySoccerAction(BlockFailureAnimName, string.Empty);
    }

    public void StopSoccerAction()
    {
        if (!isSoccerActionPlaying)
            return;

        isSoccerActionPlaying = false;

        if (!IsDied)
        {
            UpdateAnimation();
        }
    }

    private void UpdateLockedActionState()
    {
        if (!IsReady)
            return;

        UpdateAirbornStatus();

        if (IsSit || isSoccerActionPlaying)
        {
            StopAllMotion();
        }
        else
        {
            StopHorizontalMotion();
        }

        HandleCollision();
    }

    private async UniTask PlaySoccerAction(string firstAnimation, string secondAnimation)
    {
        if (IsDied)
        {
            return;
        }

        isSoccerActionPlaying = true;
        StopAllMotion();

        if (!string.IsNullOrEmpty(firstAnimation))
        {
            await PlayAnimationOrDelay(firstAnimation, 0.35f);
            await PlayAnimationOrDelay(firstAnimation, 0.35f);
        }

        if (!string.IsNullOrEmpty(secondAnimation))
        {
            await PlayAnimationOrDelay(secondAnimation, 0.45f);
        }

        isSoccerActionPlaying = false;
        if (!IsDied)
        {
            UpdateAnimation();
        }
    }

    private async UniTask PlayAnimationOrDelay(string animationName, float fallbackDuration)
    {
        if (!HasAnimation(animationName))
        {
            await UniTask.WaitForSeconds(fallbackDuration, cancellationToken: this.GetCancellationTokenOnDestroy());
            return;
        }

        bool completed = false;
        var entry = SpineCtrl.PlayAnimation(animationName, false, false);
        if (entry == null)
        {
            await UniTask.WaitForSeconds(fallbackDuration, cancellationToken: this.GetCancellationTokenOnDestroy());
            return;
        }

        entry.Complete += _ => completed = true;
        float duration = Mathf.Max(fallbackDuration, entry.AnimationEnd);
        float elapsed = 0f;

        while (!completed && elapsed < duration)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, this.GetCancellationTokenOnDestroy());
            elapsed += Time.deltaTime;
        }
    }

    private void StopAllMotion()
    {
        SetMoveInput(0f);

        if (RB != null)
        {
            RB.linearVelocity = Vector2.zero;
        }
    }

    private void StopHorizontalMotion()
    {
        SetMoveInput(0f);

        if (RB != null)
        {
            RB.linearVelocity = new Vector2(0f, RB.linearVelocityY);
        }
    }

    private bool HasAnimation(string animationName)
    {
        if (SpineCtrl == null || SpineCtrl.AnimationState == null)
        {
            this.LogWarning("[Level84ActorCtrl] SpineCtrl or AnimationState is missing.");
            return false;
        }

        var skeletonData = SpineCtrl.AnimationState.Data?.SkeletonData;
        if (skeletonData == null)
        {
            this.LogWarning("[Level84ActorCtrl] SkeletonData is missing.");
            return false;
        }

        if (skeletonData.FindAnimation(animationName) != null)
        {
            return true;
        }

        this.LogWarning($"[Level84ActorCtrl] Animation not found: {animationName}");
        return false;
    }

    private void OnDisable()
    {
        isSoccerActionPlaying = false;
    }
}
