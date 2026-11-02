using System;
using UnityEngine;

/// <summary>
/// Level113 主角专用控制器；在保留通用移动的同时托管 obtain、hit 两段持钥匙动作。
/// </summary>
public sealed class Level113ActorCtrl : ActorCtrl
{
    private const string ObtainAnimationName = "obtain";
    private const string HitAnimationName = "hit";

    private bool isActionPlaying;
    private bool hasTool;
    private int actionToken;

    /// <summary>
    /// 当前是否已经把拖入按钮的钥匙挂到 point_hit。
    /// 调用边界：仅供 Level113 总控判定按钮能否进入 hit 流程。
    /// </summary>
    public bool HasTool => hasTool;

    /// <summary>
    /// 当前是否正在播放不可被移动动画覆盖的本关动作。
    /// 调用边界：仅供 Level113 总控锁定重复按钮输入。
    /// </summary>
    public bool IsLevelActionPlaying => isActionPlaying;

    #region Animation Override

    protected override void HandleAnimators()
    {
        if (isActionPlaying)
            return;

        base.HandleAnimators();
    }

    #endregion

    #region Level113 Actions

    /// <summary>
    /// 播放获得钥匙展示；完成后恢复通用 idle/walk 等移动动画并回调。
    /// 调用边界：钥匙已经到达 point_hit 后调用一次；动画缺失时立即降级完成，不阻断玩法。
    /// </summary>
    public bool PlayObtain(Action onComplete)
    {
        hasTool = true;
        return PlayLevelAction(ObtainAnimationName, onComplete);
    }

    /// <summary>
    /// 面向指定世界坐标并播放钥匙打击。
    /// 调用边界：仅响应已安装到 jump_btn 的钥匙；完成后恢复通用移动动画，钥匙仍由 point_hit 挂点持有。
    /// </summary>
    public bool PlayHit(float targetWorldX, Action onComplete)
    {
        if (!hasTool)
        {
            onComplete?.Invoke();
            return false;
        }

        SetActorFaceDirection(targetWorldX >= transform.position.x);
        return PlayLevelAction(HitAnimationName, onComplete);
    }

    /// <summary>
    /// 清除本关道具动画托管并恢复通用移动动画；仅在关卡退出或清理时调用，可重复执行。
    /// </summary>
    public void ClearLevel113ToolState()
    {
        actionToken++;
        isActionPlaying = false;
        hasTool = false;

        if (isActiveAndEnabled && !IsDied)
            UpdateAnimation();
    }

    private bool PlayLevelAction(string animationName, Action onComplete)
    {
        StopMoving();
        isActionPlaying = true;
        int token = ++actionToken;

        Spine.TrackEntry entry = TryPlay(animationName, false);
        if (entry == null)
        {
            CompleteLevelAction(token, onComplete);
            return false;
        }

        entry.Complete += _ => CompleteLevelAction(token, onComplete);
        return true;
    }

    private void CompleteLevelAction(int token, Action onComplete)
    {
        if (token != actionToken)
            return;

        isActionPlaying = false;
        if (isActiveAndEnabled && !IsDied)
            UpdateAnimation();
        onComplete?.Invoke();
    }

    private Spine.TrackEntry TryPlay(string animationName, bool loop)
    {
        Spine.AnimationState animationState = SpineCtrl != null ? SpineCtrl.AnimationState : null;
        Spine.SkeletonData skeletonData = animationState?.Data?.SkeletonData;
        if (skeletonData == null || skeletonData.FindAnimation(animationName) == null)
        {
            Debug.LogWarning($"[Level113ActorCtrl] Spine animation not found: {animationName}", this);
            return null;
        }

        return SpineCtrl.PlayAnimation(animationName, loop, false);
    }

    #endregion

    private void OnDisable()
    {
        actionToken++;
        isActionPlaying = false;
        hasTool = false;
    }
}
