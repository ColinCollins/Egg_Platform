using System;
using Spine;

/// <summary>
/// Level110 角色局部扩展：在吃草莓期间保持 eat 动画不被通用移动动画覆盖。
/// </summary>
public sealed class Level110ActorCtrl : ActorCtrl
{
    private const string EatAnimationName = "eat";
    private const string IdleAnimationName = "idle";

    private TrackEntry eatTrackEntry;
    private Action eatCompleteCallback;
    private bool isEating;

    /// <summary>
    /// 播放一次 eat 动画并在完整播放或意外中断时结束本关食用流程。
    /// 调用边界：仅由 Level110Ctrl 在草莓有效投放后调用。
    /// 注意事项：动画缺失时返回 false，调用方必须执行自己的兜底完成逻辑。
    /// </summary>
    /// <param name="onComplete">eat 轨道结束后的回调。</param>
    /// <returns>成功取得并跟踪 eat 动画轨道时返回 true。</returns>
    public bool TryPlayEat(Action onComplete)
    {
        if (isEating || IsDied || SpineCtrl == null || SpineCtrl.AnimationState == null)
            return false;

        SkeletonData skeletonData = SpineCtrl.AnimationState.Data?.SkeletonData;
        if (skeletonData?.FindAnimation(EatAnimationName) == null)
            return false;

        ClearEatTracking();
        SetMoveInput(0f);
        StopMoving();

        TrackEntry trackEntry = SpineCtrl.PlayAnimation(EatAnimationName, false, false);
        if (trackEntry == null)
            return false;

        isEating = true;
        eatCompleteCallback = onComplete;
        eatTrackEntry = trackEntry;
        trackEntry.Complete += HandleEatFinished;
        trackEntry.Interrupt += HandleEatFinished;
        return true;
    }

    protected override void HandleAnimators()
    {
        if (isEating)
            return;

        base.HandleAnimators();
    }

    private void HandleEatFinished(TrackEntry trackEntry)
    {
        if (eatTrackEntry != trackEntry)
            return;

        Action callback = eatCompleteCallback;
        ClearEatTracking();
        isEating = false;

        if (!IsDied && SpineCtrl != null)
            SpineCtrl.PlayAnimation(IdleAnimationName, true, false);

        callback?.Invoke();
    }

    private void ClearEatTracking()
    {
        if (eatTrackEntry != null)
        {
            eatTrackEntry.Complete -= HandleEatFinished;
            eatTrackEntry.Interrupt -= HandleEatFinished;
        }

        eatTrackEntry = null;
        eatCompleteCallback = null;
    }

    private void OnDisable()
    {
        ClearEatTracking();
        isEating = false;
    }
}
