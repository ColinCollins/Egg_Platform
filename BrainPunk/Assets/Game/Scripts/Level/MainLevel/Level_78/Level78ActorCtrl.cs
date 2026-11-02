using System;
using System.Collections;
using Game.Scripts.Common;
using Spine;
using Spine.Unity;
using UnityEngine;

/// <summary>
/// Level 078 专用角色适配器；在本关射箭动作期间阻止通用移动动画覆盖拉弓序列。
/// </summary>
public sealed class Level78ActorCtrl : ActorCtrl
{
    #region Constants

    private const string PullAnimationName = "draw_pull";
    private const string HoldAnimationName = "draw_hold";
    private const string ShootAnimationName = "draw_shoot";
    private const string IdleAnimationName = "idle";
    private const string CelebrateAnimationName = "celebrate";
    private const float BoxReadyHoldTimeScale = 1f;
    private const string DrawBowSfxTag = "drawBow";

    #endregion

    #region Fields

    private bool level78ControlLocked;
    private bool archerySequenceActive;
    private bool releaseInvoked;
    private bool activeReturnToReadyPose;
    private bool activeWaitForShotAfterHold;
    private int flowVersion;
    private int activeFlow;
    private float activeReleaseNormalizedTime;
    private Action activeOnRelease;
    private Action activeOnComplete;
    private TrackEntry activeEntry;
    private Coroutine releaseCoroutine;

    #endregion

    #region Lifecycle

    public override void OnUpdate()
    {
        if (level78ControlLocked || archerySequenceActive)
            return;

        base.OnUpdate();
    }

    private void OnDisable()
    {
        CancelLevel78Action(false);
    }

    private void OnDestroy()
    {
        CancelLevel78Action(false);
    }

    #endregion

    #region Public API

    /// <summary>
    /// 当前是否可以原子启动一套新的射箭动作；调用方应在提交领域层单箭事务前检查。
    /// </summary>
    public bool CanPlayArcherySequence =>
        isActiveAndEnabled && !archerySequenceActive && SpineCtrl != null;

    /// <summary>
    /// 播放 Level 078 拉弓准备动作，并以低速持弓循环保持蓄力感；仅供 Box 到达射箭点且尚未开始本箭时调用。
    /// </summary>
    /// <returns>是否成功启动拉弓与持弓准备动作。</returns>
    public bool ShowArcheryReadyPose()
    {
        if (!CanPlayArcherySequence)
            return false;

        SetMoveInput(0f);
        StopMoving();
        return BeginBoxReadyPose();
    }

    /// <summary>
    /// 锁定或恢复 Level 078 角色移动；射箭三次期间由关卡控制器保持锁定。
    /// </summary>
    /// <param name="locked">是否锁定通用移动与动画更新。</param>
    public void SetLevel78ControlLocked(bool locked)
    {
        level78ControlLocked = locked;
        SetMoveInput(0f);

        if (locked)
            StopMoving();
        else
            ResumeMoving();
    }

    /// <summary>
    /// 自动播放 draw_pull、draw_hold、draw_shoot，并在松弦归一化时点触发一次释放回调。
    /// </summary>
    /// <param name="releaseNormalizedTime">draw_shoot 内的松弦归一化时间，范围 0 到 1。</param>
    /// <param name="onRelease">视觉松弦时调用一次，用于生成并发射箭矢。</param>
    /// <param name="onComplete">完整射箭动作结束时调用一次。</param>
    /// <returns>是否成功启动射箭动作序列。</returns>
    public bool PlayArcherySequence(
        float releaseNormalizedTime,
        Action onRelease,
        Action onComplete)
    {
        return BeginArcherySequence(
            false,
            false,
            releaseNormalizedTime,
            onRelease,
            onComplete);
    }

    /// <summary>
    /// 从持弓待发姿态直接播放 draw_shoot；仅供 Box 的有效射箭点击调用。
    /// </summary>
    /// <param name="releaseNormalizedTime">draw_shoot 内的松弦归一化时间，范围 0 到 1。</param>
    /// <param name="onRelease">视觉松弦时调用一次，用于生成并发射箭矢。</param>
    /// <param name="onComplete">射击动作结束时调用一次；未结算时角色恢复持弓待发。</param>
    /// <returns>是否成功启动射击动作。</returns>
    public bool PlayArcheryShotFromReadyPose(
        float releaseNormalizedTime,
        Action onRelease,
        Action onComplete)
    {
        return BeginArcherySequence(
            true,
            true,
            releaseNormalizedTime,
            onRelease,
            onComplete);
    }

    /// <summary>
    /// 结束当前射箭动作并切换为循环普通待机；仅供 Box 落败后的结果播报阶段调用。
    /// idle 不 key 射箭动画动过的部分手臂骨骼（如 Soft_arm_Rot_L/R），
    /// 必须先还原初始姿态再播放，避免拉弓手型残留。
    /// </summary>
    /// <returns>是否成功播放普通待机动画。</returns>
    public bool ShowFailureSettlementIdlePose()
    {
        CancelLevel78Action(false);
        ResetSpineToSetupPose();
        return SpineCtrl?.PlayAnimation(IdleAnimationName, true, false) != null;
    }

    /// <summary>
    /// 结束当前射箭动作并切换为循环共享庆祝动画；仅供 Box 获胜后的结果播报阶段调用。
    /// 先还原初始姿态，清除射箭动画遗留的骨骼与附件状态。
    /// </summary>
    /// <returns>是否成功播放庆祝动画。</returns>
    public bool ShowVictorySettlementCelebratePose()
    {
        CancelLevel78Action(false);
        ResetSpineToSetupPose();
        return SpineCtrl?.PlayAnimation(CelebrateAnimationName, true, false) != null;
    }

    /// <summary>
    /// 将骨骼与插槽附件全部还原到初始姿态；仅在本关结算姿态切换前调用，
    /// 清除射箭动画在未覆盖骨骼（如 Soft_arm_Rot_L/R）上遗留的拉弓手型。
    /// 通过本组件本地访问 SkeletonAnimation，不改动共享 ActorSpineCtrl。
    /// </summary>
    private void ResetSpineToSetupPose()
    {
        var skeletonAnimation = SpineCtrl != null
            ? SpineCtrl.GetComponentInChildren<SkeletonAnimation>()
            : null;
        if (skeletonAnimation?.Skeleton == null)
            return;

        skeletonAnimation.Skeleton.SetToSetupPose();
        skeletonAnimation.Skeleton.UpdateWorldTransform(Skeleton.Physics.Update);
    }

    /// <summary>
    /// 取消当前 Level 078 动作和旧松弦回调；重开、禁用、销毁时必须调用。
    /// </summary>
    /// <param name="resumeMovement">取消后是否恢复通用移动。</param>
    public void CancelLevel78Action(bool resumeMovement)
    {
        flowVersion++;
        if (releaseCoroutine != null)
        {
            StopCoroutine(releaseCoroutine);
            releaseCoroutine = null;
        }

        DetachActiveEntry();
        archerySequenceActive = false;
        releaseInvoked = false;
        activeReturnToReadyPose = false;
        activeWaitForShotAfterHold = false;
        activeOnRelease = null;
        activeOnComplete = null;

        if (SpineCtrl?.AnimationState != null)
            SpineCtrl.AnimationState.ClearTrack(0);

        if (resumeMovement)
            SetLevel78ControlLocked(false);
    }

    #endregion

    #region Animation Flow

    private bool BeginBoxReadyPose()
    {
        if (!CanPlayArcherySequence)
            return false;

        int currentFlow = ++flowVersion;
        archerySequenceActive = true;
        activeWaitForShotAfterHold = true;
        activeFlow = currentFlow;
        activeOnRelease = null;
        activeOnComplete = null;
        SetMoveInput(0f);
        StopMoving();

        TrackEntry pullEntry = SpineCtrl.PlayAnimation(PullAnimationName, false, false);
        if (pullEntry == null)
            return PlayBoxReadyHold(currentFlow);

        // 拉弓动画启动即播放拉弓音效，火后即忘、与动画时长脱钩。
        AudioManager.PlaySound(DrawBowSfxTag);
        activeEntry = pullEntry;
        activeEntry.Complete += OnPullComplete;
        return true;
    }

    private bool PlayBoxReadyHold(int currentFlow)
    {
        if (!IsCurrentFlow(currentFlow))
            return false;

        TrackEntry holdEntry = SpineCtrl.PlayAnimation(HoldAnimationName, true, false);
        if (holdEntry == null)
        {
            archerySequenceActive = false;
            activeWaitForShotAfterHold = false;
            return false;
        }

        holdEntry.TimeScale = BoxReadyHoldTimeScale;
        activeEntry = holdEntry;
        activeEntry.Complete += OnHoldComplete;
        return true;
    }

    private bool BeginArcherySequence(
        bool shootFromReadyPose,
        bool returnToReadyPose,
        float releaseNormalizedTime,
        Action onRelease,
        Action onComplete)
    {
        if (!CanPlayArcherySequence)
            return false;

        int currentFlow = ++flowVersion;
        archerySequenceActive = true;
        releaseInvoked = false;
        activeReturnToReadyPose = returnToReadyPose;
        activeWaitForShotAfterHold = false;
        activeFlow = currentFlow;
        activeReleaseNormalizedTime = Mathf.Clamp01(releaseNormalizedTime);
        activeOnRelease = onRelease;
        activeOnComplete = onComplete;
        SetMoveInput(0f);
        StopMoving();

        if (shootFromReadyPose)
        {
            PlayShoot(currentFlow);
            return true;
        }

        TrackEntry pullEntry = SpineCtrl.PlayAnimation(PullAnimationName, false, false);
        if (pullEntry == null)
        {
            CompleteFallback(currentFlow);
            return true;
        }

        // 拉弓动画启动即播放拉弓音效，火后即忘、与动画时长脱钩。
        AudioManager.PlaySound(DrawBowSfxTag);
        activeEntry = pullEntry;
        activeEntry.Complete += OnPullComplete;
        return true;
    }

    private void OnPullComplete(TrackEntry entry)
    {
        if (!IsExpectedEntry(entry))
            return;

        DetachActiveEntry();
        if (activeWaitForShotAfterHold)
        {
            PlayBoxReadyHold(activeFlow);
            return;
        }

        TrackEntry holdEntry = SpineCtrl.PlayAnimation(HoldAnimationName, false, false);
        if (holdEntry == null)
        {
            PlayShoot(activeFlow);
            return;
        }

        activeEntry = holdEntry;
        activeEntry.Complete += OnHoldComplete;
    }

    private void OnHoldComplete(TrackEntry entry)
    {
        if (!IsExpectedEntry(entry))
            return;

        DetachActiveEntry();
        if (activeWaitForShotAfterHold)
        {
            activeWaitForShotAfterHold = false;
            archerySequenceActive = false;
            return;
        }

        PlayShoot(activeFlow);
    }

    private void PlayShoot(int currentFlow)
    {
        if (!IsCurrentFlow(currentFlow))
            return;

        TrackEntry shootEntry = SpineCtrl.PlayAnimation(ShootAnimationName, false, false);
        if (shootEntry == null)
        {
            CompleteFallback(currentFlow);
            return;
        }

        activeEntry = shootEntry;
        activeEntry.Complete += OnShootComplete;
        releaseCoroutine = StartCoroutine(WaitForReleaseFrame(
            currentFlow,
            shootEntry,
            activeReleaseNormalizedTime));
    }

    private void OnShootComplete(TrackEntry entry)
    {
        if (!IsExpectedEntry(entry))
            return;

        DetachActiveEntry();
        ReleaseOnce(activeFlow);
        CompleteArchery(activeFlow);
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

        releaseCoroutine = null;
    }

    private void ReleaseOnce(int currentFlow)
    {
        if (!IsCurrentFlow(currentFlow) || releaseInvoked)
            return;

        releaseInvoked = true;
        activeOnRelease?.Invoke();
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

        if (releaseCoroutine != null)
        {
            StopCoroutine(releaseCoroutine);
            releaseCoroutine = null;
        }

        DetachActiveEntry();
        Action onComplete = activeOnComplete;
        bool returnToReadyPose = activeReturnToReadyPose;
        activeOnRelease = null;
        activeOnComplete = null;
        archerySequenceActive = false;
        activeReturnToReadyPose = false;
        if (returnToReadyPose)
            BeginBoxReadyPose();
        else
            SpineCtrl.PlayAnimation(IdleAnimationName, true, false);

        onComplete?.Invoke();
    }

    private bool IsExpectedEntry(TrackEntry entry)
    {
        return ReferenceEquals(entry, activeEntry) && IsCurrentFlow(activeFlow);
    }

    private void DetachActiveEntry()
    {
        if (activeEntry == null)
            return;

        activeEntry.Complete -= OnPullComplete;
        activeEntry.Complete -= OnHoldComplete;
        activeEntry.Complete -= OnShootComplete;
        activeEntry = null;
    }

    private bool IsCurrentFlow(int currentFlow)
    {
        return isActiveAndEnabled && archerySequenceActive && currentFlow == flowVersion;
    }

    #endregion
}
