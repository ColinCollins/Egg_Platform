using System;
using System.Collections;
using Spine;
using UnityEngine;

/// <summary>
/// DrawGame 第 5 关 Enemy 专用角色控制器。
/// 只负责拉弓、持弓、射箭动画序列及松弦回调，不承担箭矢生成、倒计时或关卡结算。
/// </summary>
public sealed class DLevel5ActorCtrl : ActorCtrl
{
    #region Constants

    private const string PullAnimationName = "draw_pull";
    private const string HoldAnimationName = "draw_hold";
    private const string ShootAnimationName = "draw_shoot";
    private const string IdleAnimationName = "idle";
    private const float MinimumPlaybackSpeed = 0.01f;

    #endregion

    #region Runtime State

    private bool _isArcherySequenceActive;
    private bool _releaseInvoked;
    private int _flowVersion;
    private int _activeFlow;
    private float _releaseNormalizedTime;
    private float _playbackSpeed = 1f;
    private Action _onRelease;
    private Action _onComplete;
    private TrackEntry _activeEntry;
    private Coroutine _releaseCoroutine;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// DLevel5 Enemy 由关卡控制器驱动，不接收通用玩家移动与动画更新。
    /// </summary>
    public override void OnUpdate()
    {
    }

    private void OnDisable()
    {
        CancelArcheryAction(false);
    }

    private void OnDestroy()
    {
        CancelArcheryAction(false);
    }

    #endregion

    #region Public API

    /// <summary>
    /// 当前是否可以启动一套新的拉弓射箭动作。
    /// 调用边界：仅供同一 DLevel5 实例内的 DLevel5Ctrl 查询。
    /// </summary>
    public bool CanPlayArcherySequence =>
        isActiveAndEnabled && !_isArcherySequenceActive && SpineCtrl != null;

    /// <summary>
    /// 依次播放 draw_pull、draw_hold、draw_shoot，并在松弦时点回调箭矢生成。
    /// 调用边界：同一时间只允许一套动作；回调不得跨关卡持有本组件。
    /// </summary>
    /// <param name="releaseNormalizedTime">draw_shoot 内的松弦归一化时间，范围 0 到 1。</param>
    /// <param name="playbackSpeed">整套射箭动作的播放速度倍率，必须大于 0。</param>
    /// <param name="onRelease">松弦时调用一次，用于生成并发射箭矢。</param>
    /// <param name="onComplete">完整射箭动作结束时调用一次。</param>
    /// <returns>是否成功启动动作序列。</returns>
    public bool PlayArcherySequence(
        float releaseNormalizedTime,
        float playbackSpeed,
        Action onRelease,
        Action onComplete)
    {
        if (!CanPlayArcherySequence)
            return false;

        int currentFlow = ++_flowVersion;
        _isArcherySequenceActive = true;
        _releaseInvoked = false;
        _activeFlow = currentFlow;
        _releaseNormalizedTime = Mathf.Clamp01(releaseNormalizedTime);
        _playbackSpeed = Mathf.Max(MinimumPlaybackSpeed, playbackSpeed);
        _onRelease = onRelease;
        _onComplete = onComplete;

        SetMoveInput(0f);
        StopMoving();

        TrackEntry pullEntry = PlayAnimationWithSpeed(PullAnimationName, false);
        if (pullEntry == null)
        {
            PlayHold(currentFlow);
            return true;
        }

        _activeEntry = pullEntry;
        _activeEntry.Complete += OnPullComplete;
        return true;
    }

    /// <summary>
    /// 取消当前射箭动作并使旧松弦/完成回调失效。
    /// 调用边界：关卡结算、重置、禁用和销毁时调用；returnToIdle 只应在对象仍激活时开启。
    /// </summary>
    /// <param name="returnToIdle">取消后是否恢复循环 idle 动画。</param>
    public void CancelArcheryAction(bool returnToIdle)
    {
        _flowVersion++;
        if (_releaseCoroutine != null)
        {
            StopCoroutine(_releaseCoroutine);
            _releaseCoroutine = null;
        }

        DetachActiveEntry();
        _isArcherySequenceActive = false;
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

    private void OnPullComplete(TrackEntry entry)
    {
        if (!IsExpectedEntry(entry))
            return;

        DetachActiveEntry();
        PlayHold(_activeFlow);
    }

    private void PlayHold(int currentFlow)
    {
        if (!IsCurrentFlow(currentFlow))
            return;

        TrackEntry holdEntry = PlayAnimationWithSpeed(HoldAnimationName, false);
        if (holdEntry == null)
        {
            PlayShoot(currentFlow);
            return;
        }

        _activeEntry = holdEntry;
        _activeEntry.Complete += OnHoldComplete;
    }

    private void OnHoldComplete(TrackEntry entry)
    {
        if (!IsExpectedEntry(entry))
            return;

        DetachActiveEntry();
        PlayShoot(_activeFlow);
    }

    private void PlayShoot(int currentFlow)
    {
        if (!IsCurrentFlow(currentFlow))
            return;

        TrackEntry shootEntry = PlayAnimationWithSpeed(ShootAnimationName, false);
        if (shootEntry == null)
        {
            CompleteFallback(currentFlow);
            return;
        }

        _activeEntry = shootEntry;
        _activeEntry.Complete += OnShootComplete;
        _releaseCoroutine = StartCoroutine(WaitForReleaseFrame(
            currentFlow,
            shootEntry,
            _releaseNormalizedTime));
    }

    private void OnShootComplete(TrackEntry entry)
    {
        if (!IsExpectedEntry(entry))
            return;

        DetachActiveEntry();
        ReleaseOnce(_activeFlow);
        CompleteArchery(_activeFlow);
    }

    private IEnumerator WaitForReleaseFrame(
        int currentFlow,
        TrackEntry shootEntry,
        float releaseNormalizedTime)
    {
        float releaseTrackTime = Mathf.Lerp(
            shootEntry.AnimationStart,
            shootEntry.AnimationEnd,
            releaseNormalizedTime);
        while (IsCurrentFlow(currentFlow) && shootEntry.TrackTime < releaseTrackTime)
            yield return null;

        if (IsCurrentFlow(currentFlow))
            ReleaseOnce(currentFlow);

        _releaseCoroutine = null;
    }

    private void ReleaseOnce(int currentFlow)
    {
        if (!IsCurrentFlow(currentFlow) || _releaseInvoked)
            return;

        _releaseInvoked = true;
        _onRelease?.Invoke();
    }

    private void CompleteFallback(int currentFlow)
    {
        if (!IsCurrentFlow(currentFlow))
            return;

        ReleaseOnce(currentFlow);
        CompleteArchery(currentFlow);
    }

    private void CompleteArchery(int currentFlow)
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
        _isArcherySequenceActive = false;
        PlayAnimationWithSpeed(HoldAnimationName, true);
        onComplete?.Invoke();
    }

    private TrackEntry PlayAnimationWithSpeed(string animationName, bool loop)
    {
        TrackEntry entry = SpineCtrl?.PlayAnimation(animationName, loop, false);
        if (entry != null)
            entry.TimeScale = _playbackSpeed;

        return entry;
    }

    private bool IsExpectedEntry(TrackEntry entry)
    {
        return ReferenceEquals(entry, _activeEntry) && IsCurrentFlow(_activeFlow);
    }

    private void DetachActiveEntry()
    {
        if (_activeEntry == null)
            return;

        _activeEntry.Complete -= OnPullComplete;
        _activeEntry.Complete -= OnHoldComplete;
        _activeEntry.Complete -= OnShootComplete;
        _activeEntry = null;
    }

    private bool IsCurrentFlow(int currentFlow)
    {
        return isActiveAndEnabled &&
               _isArcherySequenceActive &&
               currentFlow == _flowVersion;
    }

    #endregion
}
