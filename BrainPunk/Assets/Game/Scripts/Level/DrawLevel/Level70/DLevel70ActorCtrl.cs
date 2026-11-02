using System;
using Spine;
using UnityEngine;

/// <summary>
/// 第 70 关 Actor 的 eat 演出；只管理动画及回调，胜利由本关 Ctrl 提交。
/// </summary>
public sealed class DLevel70ActorCtrl : ActorCtrl
{
    #region Runtime State

    private TrackEntry _eatEntry;
    private Action _onEatComplete;
    private Action _onEatCancelled;

    #endregion

    #region Public API

    /// <summary>
    /// 播放一次非循环 eat，只有完整播放结束才调用 onComplete。
    /// 调用边界：同一关卡 Ctrl 占用成功演出后调用；重复播放、死亡或缺少动画时返回 false。
    /// onCancelled 用于动画被替换、清轨或对象禁用时取消尚未结算的成功演出。
    /// </summary>
    public bool PlayEat(Action onComplete, Action onCancelled)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || IsDied || _eatEntry != null)
            return false;

        Spine.AnimationState animationState = SpineCtrl != null ? SpineCtrl.AnimationState : null;
        if (animationState == null || animationState.Data.SkeletonData.FindAnimation("eat") == null)
            return false;

        TrackEntry entry = SpineCtrl.PlayAnimation("eat", false, false);
        if (entry == null)
            return false;

        SetMoveInput(0f);
        StopMoving();
        _eatEntry = entry;
        _onEatComplete = onComplete;
        _onEatCancelled = onCancelled;
        entry.Complete += OnEatComplete;
        entry.Interrupt += OnEatInterrupted;
        entry.End += OnEatInterrupted;
        return true;
    }

    /// <summary>
    /// 取消当前 eat 并使完成回调失效；用于本关 Reset、禁用和动画中断。
    /// 调用边界：只清理本对象持有的 eat，不清除外部后来替换的动画。
    /// </summary>
    public void CancelEat()
    {
        if (_eatEntry == null)
            return;

        TrackEntry entry = _eatEntry;
        Action onCancelled = _onEatCancelled;
        DetachEat();
        if (SpineCtrl != null && SpineCtrl.AnimationState?.GetCurrent(0) == entry)
            SpineCtrl.AnimationState.ClearTrack(0);

        ResumeMoving();
        onCancelled?.Invoke();
    }

    #endregion

    #region Animation Lifecycle

    /// <summary>
    /// eat 期间保留专用动画，避免共享移动、接地与待机逻辑覆盖当前演出。
    /// </summary>
    public override void OnUpdate()
    {
        if (_eatEntry == null)
            base.OnUpdate();
    }

    /// <summary>
    /// 外部刷新动画时同样保留 eat，完成或取消后恢复共享动画规则。
    /// </summary>
    protected override void HandleAnimators()
    {
        if (_eatEntry == null)
            base.HandleAnimators();
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
            CancelEat();
    }

    private void OnEatComplete(TrackEntry entry)
    {
        if (!ReferenceEquals(entry, _eatEntry))
            return;

        Action onComplete = _onEatComplete;
        DetachEat();
        onComplete?.Invoke();
    }

    private void OnEatInterrupted(TrackEntry entry)
    {
        if (ReferenceEquals(entry, _eatEntry))
            CancelEat();
    }

    private void DetachEat()
    {
        _eatEntry.Complete -= OnEatComplete;
        _eatEntry.Interrupt -= OnEatInterrupted;
        _eatEntry.End -= OnEatInterrupted;
        _eatEntry = null;
        _onEatComplete = null;
        _onEatCancelled = null;
    }

    #endregion
}
