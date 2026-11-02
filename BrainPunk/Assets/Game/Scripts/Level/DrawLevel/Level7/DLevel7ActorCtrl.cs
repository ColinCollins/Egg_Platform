using System;
using System.Collections;
using Spine;
using UnityEngine;

/// <summary>
/// DrawGame 第 7 关 Actor 专用投掷动画控制器。
/// 只负责 throwing 的释放时点与完成后回到 idle，不承担线段物理、气球或关卡结算。
/// </summary>
public sealed class DLevel7ActorCtrl : ActorCtrl
{
    #region Constants

    private const string ThrowAnimationName = "throwing";
    private const string IdleAnimationName = "idle";
    private const float ReleaseNormalizedTime = 0.45f;

    #endregion

    #region Runtime State

    private bool _isThrowSequenceActive;
    private bool _releaseInvoked;
    private int _flowVersion;
    private int _activeFlow;
    private Action _onRelease;
    private Action _onComplete;
    private TrackEntry _activeEntry;
    private Coroutine _releaseCoroutine;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// DLevel7 Actor 由关卡控制器驱动，不接收通用玩家移动和自动动画更新。
    /// </summary>
    public override void OnUpdate()
    {
    }

    private void OnDisable()
    {
        CancelThrowAction(false);
    }

    private void OnDestroy()
    {
        CancelThrowAction(false);
    }

    #endregion

    #region Public API

    /// <summary>
    /// 播放一次 throwing，在固定释放时点调用 onRelease，完成后恢复循环 idle。
    /// 调用边界：只供同一 DLevel7 实例内的 DLevel7Ctrl 调用；同一时间只允许一套投掷动作。
    /// </summary>
    /// <param name="onRelease">throwing 进行到释放时点时调用一次，用于施加线段冲量。</param>
    /// <param name="onComplete">throwing 完成且恢复 idle 后调用一次。</param>
    /// <returns>是否成功启动投掷动作。</returns>
    public bool PlayThrowSequence(Action onRelease, Action onComplete)
    {
        if (!isActiveAndEnabled || _isThrowSequenceActive || SpineCtrl == null)
            return false;

        int currentFlow = ++_flowVersion;
        _activeFlow = currentFlow;
        _isThrowSequenceActive = true;
        _releaseInvoked = false;
        _onRelease = onRelease;
        _onComplete = onComplete;

        SetMoveInput(0f);
        StopMoving();

        TrackEntry throwEntry = SpineCtrl.PlayAnimation(ThrowAnimationName, false, false);
        if (throwEntry == null)
        {
            CompleteThrowFallback(currentFlow);
            return true;
        }

        _activeEntry = throwEntry;
        _activeEntry.Complete += OnThrowComplete;
        _releaseCoroutine = StartCoroutine(WaitForReleaseFrame(currentFlow, throwEntry));
        return true;
    }

    /// <summary>
    /// 取消当前 throwing，并使旧释放与完成回调失效。
    /// 调用边界：关卡 Reset、禁用和销毁时调用；returnToIdle 只应在对象仍激活时开启。
    /// </summary>
    /// <param name="returnToIdle">取消后是否恢复循环 idle。</param>
    public void CancelThrowAction(bool returnToIdle)
    {
        _flowVersion++;
        if (_releaseCoroutine != null)
        {
            StopCoroutine(_releaseCoroutine);
            _releaseCoroutine = null;
        }

        DetachActiveEntry();
        _isThrowSequenceActive = false;
        _releaseInvoked = false;
        _onRelease = null;
        _onComplete = null;

        if (SpineCtrl?.AnimationState != null)
            SpineCtrl.AnimationState.ClearTrack(0);

        if (returnToIdle && isActiveAndEnabled)
            SpineCtrl?.PlayAnimation(IdleAnimationName, true, false);
    }

    #endregion

    #region Animation Flow

    private IEnumerator WaitForReleaseFrame(int currentFlow, TrackEntry throwEntry)
    {
        float releaseTrackTime = Mathf.Lerp(
            throwEntry.AnimationStart,
            throwEntry.AnimationEnd,
            ReleaseNormalizedTime);
        while (IsCurrentFlow(currentFlow) && throwEntry.TrackTime < releaseTrackTime)
            yield return null;

        if (IsCurrentFlow(currentFlow))
            ReleaseOnce(currentFlow);

        _releaseCoroutine = null;
    }

    private void OnThrowComplete(TrackEntry entry)
    {
        if (!ReferenceEquals(entry, _activeEntry) || !IsCurrentFlow(_activeFlow))
            return;

        DetachActiveEntry();
        ReleaseOnce(_activeFlow);
        CompleteThrow(_activeFlow);
    }

    private void CompleteThrowFallback(int currentFlow)
    {
        if (!IsCurrentFlow(currentFlow))
            return;

        ReleaseOnce(currentFlow);
        CompleteThrow(currentFlow);
    }

    private void ReleaseOnce(int currentFlow)
    {
        if (!IsCurrentFlow(currentFlow) || _releaseInvoked)
            return;

        _releaseInvoked = true;
        _onRelease?.Invoke();
    }

    private void CompleteThrow(int currentFlow)
    {
        if (!IsCurrentFlow(currentFlow))
            return;

        if (_releaseCoroutine != null)
        {
            StopCoroutine(_releaseCoroutine);
            _releaseCoroutine = null;
        }

        DetachActiveEntry();
        Action onComplete = _onComplete;
        _onRelease = null;
        _onComplete = null;
        _isThrowSequenceActive = false;
        SpineCtrl.PlayAnimation(IdleAnimationName, true, false);
        onComplete?.Invoke();
    }

    private void DetachActiveEntry()
    {
        if (_activeEntry == null)
            return;

        _activeEntry.Complete -= OnThrowComplete;
        _activeEntry = null;
    }

    private bool IsCurrentFlow(int currentFlow)
    {
        return isActiveAndEnabled &&
               _isThrowSequenceActive &&
               currentFlow == _flowVersion;
    }

    #endregion
}
