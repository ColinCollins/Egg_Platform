using System;
using System.Collections;
using Game.Level.Actor;
using Game.Scripts.Common;
using Spine;
using Spine.Unity;
using UnityEngine;

/// <summary>
/// Level 129 local actor adapter. Normal carry movement uses the shared actor
/// animations; one shot temporarily owns track 0 for pull, hold, release and
/// the short blend back to the carried-bow pose.
/// </summary>
public sealed class Level129ActorCtrl : ActorCtrl
{
    private const string PullAnimationName = "draw_pull";
    private const string HoldAnimationName = "draw_hold";
    private const string ShootAnimationName = "draw_shoot";
    private const string IdleAnimationName = "idle";
    // 本关拉弓与持弓阶段使用 2 倍速，使放箭前的整段等待缩短为原来的一半。
    private const float BowDrawAnimationTimeScale = 2f;
    // 共享 Box 骨骼的原生待发箭：它与飞行箭使用同一张 173x61 素材，挂在 bone35
    // 上，并由 draw_pull/draw_hold 的骨骼时间线带到手部。
    private const string ReadyArrowSlotName = "Sp_l78_gj_03";
    private const string ReadyArrowAttachmentName = "Sp_l78_gj_03";
    private const string DrawBowSfxTag = "drawBow";

    private bool hasBow;
    private bool archeryActive;
    private bool releaseInvoked;
    private int flowVersion;
    private int activeFlow;
    private float activeReleaseNormalizedTime;
    private float activeRecoveryDuration;
    private Action activeOnRelease;
    private Action activeOnRecoveryStart;
    private Action activeOnComplete;
    private TrackEntry activeEntry;
    private Coroutine releaseCoroutine;
    private Coroutine recoveryCoroutine;
    private SkeletonAnimation skeletonAnimation;
    private Slot readyArrowSlot;
    private Attachment readyArrowAttachment;
    private bool readyArrowWarningLogged;
    private bool archeryPaused;
    // Spine 回调中仅标记中断，下一次角色更新再清理，避免重入轨道切换。
    private bool archeryInterrupted;
    private bool refreshLocomotionAnimation;
    private ActorSkinBinding skinBinding;

    public bool HasBow => hasBow;

    public bool CanStartArcheryShot =>
        hasBow &&
        isActiveAndEnabled &&
        !IsDied &&
        !IsKnocked &&
        IsGround &&
        !archeryActive &&
        SpineCtrl != null;

    public override void OnUpdate()
    {
        if (archeryActive && archeryInterrupted && !archeryPaused)
            CompleteArchery(activeFlow);

        if (archeryActive)
            return;

        base.OnUpdate();
        if (refreshLocomotionAnimation && !IsDied)
        {
            refreshLocomotionAnimation = false;
            ReplayCurrentLocomotionAnimation();
        }
    }

    /// <summary>
    /// 射箭期间独占动画轨道；共享取消移动入口也会直接调用此方法，不能只保护 OnUpdate。
    /// </summary>
    protected override void HandleAnimators()
    {
        if (archeryActive)
            return;

        base.HandleAnimators();
    }

    private void OnEnable()
    {
        ResolveSkeletonAnimation();
        BindSkinBinding();
        refreshLocomotionAnimation = true;
        HideReadyArrow();
    }

    protected override void Start()
    {
        base.Start();
        refreshLocomotionAnimation = true;
    }

    public void SetBowCarried(bool carried)
    {
        hasBow = carried;
    }

    public bool TryPlayArcheryShot(
        float releaseNormalizedTime,
        float recoveryDuration,
        Action onRelease,
        Action onRecoveryStart,
        Action onComplete)
    {
        if (!CanStartArcheryShot)
            return false;

        ResolveSkeletonAnimation();
        activeFlow = ++flowVersion;
        archeryActive = true;
        archeryInterrupted = false;
        releaseInvoked = false;
        activeReleaseNormalizedTime = Mathf.Clamp01(releaseNormalizedTime);
        activeRecoveryDuration = Mathf.Max(0f, recoveryDuration);
        activeOnRelease = onRelease;
        activeOnRecoveryStart = onRecoveryStart;
        activeOnComplete = onComplete;
        SetMoveInput(0f);
        StopMoving();
        ShowReadyArrow();

        TrackEntry pullEntry = SpineCtrl.PlayAnimation(PullAnimationName, false, false);
        if (pullEntry == null)
        {
            PlayHold(activeFlow);
            return true;
        }

        AudioManager.PlaySound(DrawBowSfxTag);
        pullEntry.TimeScale = BowDrawAnimationTimeScale;
        activeEntry = pullEntry;
        activeEntry.Complete += OnPullComplete;
        activeEntry.Interrupt += OnArcheryInterrupted;
        activeEntry.End += OnArcheryInterrupted;
        return true;
    }

    public void SetArcheryPaused(bool paused)
    {
        // The shared project pause flow stops movement but deliberately leaves
        // ordinary actor animation running. Level 129 only owns pausing while
        // its archery transaction has track 0 locked; freezing idle/walk during
        // the reset pause window makes the rigidbody appear to skate.
        archeryPaused = paused && archeryActive;
        ResolveSkeletonAnimation();
        if (skeletonAnimation != null)
            skeletonAnimation.timeScale = archeryPaused ? 0f : 1f;
        ApplyReadyArrowVisibility();
    }

    private void OnSkeletonAnimationRebuild(ISkeletonAnimation _)
    {
        if (archeryActive)
        {
            DetachActiveEntry();
            archeryInterrupted = true;
        }

        readyArrowSlot = null;
        readyArrowAttachment = null;
        readyArrowWarningLogged = false;
        ResolveReadyArrowReferences();
        ApplyReadyArrowVisibility();
    }

    private void OnSkeletonUpdateComplete(ISkeletonAnimation _)
    {
        ApplyReadyArrowVisibility();
    }

    private void ApplyReadyArrowVisibility()
    {
        if (archeryActive && !releaseInvoked)
            ShowReadyArrow();
        else
            HideReadyArrow();
    }

    private void ShowReadyArrow()
    {
        ResolveReadyArrowReferences();
        if (readyArrowSlot == null || readyArrowAttachment == null)
            return;

        readyArrowSlot.Attachment = readyArrowAttachment;
        // draw_shoot has a t=0 alpha=0 key for this slot. The level-local
        // state machine owns visibility until the release callback fires.
        readyArrowSlot.A = 1f;
    }

    private void HideReadyArrow()
    {
        if (readyArrowSlot == null)
            return;

        readyArrowSlot.A = 0f;
        readyArrowSlot.Attachment = null;
    }

    public void CancelLevel129Action(bool resumeMovement)
    {
        CancelLevel129Action(resumeMovement, true);
    }

    private void CancelLevel129Action(bool resumeMovement, bool clearAnimationTrack)
    {
        flowVersion++;
        if (releaseCoroutine != null)
        {
            StopCoroutine(releaseCoroutine);
            releaseCoroutine = null;
        }

        if (recoveryCoroutine != null)
        {
            StopCoroutine(recoveryCoroutine);
            recoveryCoroutine = null;
        }

        DetachActiveEntry();
        archeryActive = false;
        archeryInterrupted = false;
        archeryPaused = false;
        releaseInvoked = false;
        activeRecoveryDuration = 0f;
        activeOnRelease = null;
        activeOnRecoveryStart = null;
        activeOnComplete = null;

        if (clearAnimationTrack && SpineCtrl?.AnimationState != null)
            SpineCtrl.AnimationState.ClearTrack(0);

        if (clearAnimationTrack)
            ResetSpineToSetupPose();
        else
            HideReadyArrow();

        if (resumeMovement && !IsDied)
        {
            ResumeMoving();
            HandleAnimators();
        }
    }

    public override void Die()
    {
        refreshLocomotionAnimation = false;
        // Preserve the shared transition into die. Clearing track 0 here leaves
        // setup-dependent death attachments detached, so the actor can sit down
        // without the standard project death-eye presentation.
        CancelLevel129Action(false, false);
        base.Die();
    }

    private void OnDisable()
    {
        CancelLevel129Action(false);
        UnbindSkinBinding();
        UnbindSkeletonAnimation();
    }

    private void OnDestroy()
    {
        CancelLevel129Action(false);
        UnbindSkinBinding();
        UnbindSkeletonAnimation();
    }

    private void OnPullComplete(TrackEntry entry)
    {
        if (!IsExpectedEntry(entry))
            return;

        DetachActiveEntry();
        PlayHold(activeFlow);
    }

    private void PlayHold(int currentFlow)
    {
        if (!IsCurrentFlow(currentFlow))
            return;

        TrackEntry holdEntry = SpineCtrl.PlayAnimation(HoldAnimationName, false, false);
        if (holdEntry == null)
        {
            PlayShoot(currentFlow);
            return;
        }

        holdEntry.TimeScale = BowDrawAnimationTimeScale;
        activeEntry = holdEntry;
        activeEntry.Complete += OnHoldComplete;
        activeEntry.Interrupt += OnArcheryInterrupted;
        activeEntry.End += OnArcheryInterrupted;
    }

    private void OnHoldComplete(TrackEntry entry)
    {
        if (!IsExpectedEntry(entry))
            return;

        DetachActiveEntry();
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
        activeEntry.Interrupt += OnArcheryInterrupted;
        activeEntry.End += OnArcheryInterrupted;
        releaseCoroutine = StartCoroutine(WaitForReleaseFrame(
            currentFlow,
            shootEntry,
            activeReleaseNormalizedTime));
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
        while (IsCurrentFlow(currentFlow) && !archeryInterrupted && shootEntry.TrackTime < releaseTrackTime)
            yield return null;

        if (IsCurrentFlow(currentFlow) && !archeryInterrupted)
            ReleaseOnce(currentFlow);

        releaseCoroutine = null;
    }

    private void OnShootComplete(TrackEntry entry)
    {
        if (!IsExpectedEntry(entry))
            return;

        DetachActiveEntry();
        ReleaseOnce(activeFlow);
        BeginRecovery(activeFlow);
    }

    private void CompleteFallback(int currentFlow)
    {
        if (!IsCurrentFlow(currentFlow))
            return;

        ReleaseOnce(currentFlow);
        BeginRecovery(currentFlow);
    }

    private void ReleaseOnce(int currentFlow)
    {
        if (!IsCurrentFlow(currentFlow) || releaseInvoked)
            return;

        releaseInvoked = true;
        HideReadyArrow();
        activeOnRelease?.Invoke();
    }

    private void BeginRecovery(int currentFlow)
    {
        if (!IsCurrentFlow(currentFlow))
            return;

        Action onRecoveryStart = activeOnRecoveryStart;
        activeOnRecoveryStart = null;
        TrackEntry recoveryEntry = SpineCtrl?.PlayAnimation(
            IdleAnimationName,
            true,
            false);
        if (recoveryEntry != null)
            recoveryEntry.MixDuration = activeRecoveryDuration;

        onRecoveryStart?.Invoke();
        if (activeRecoveryDuration <= 0f)
        {
            CompleteArchery(currentFlow);
            return;
        }

        recoveryCoroutine = StartCoroutine(WaitForRecovery(
            currentFlow,
            activeRecoveryDuration));
    }

    private IEnumerator WaitForRecovery(int currentFlow, float duration)
    {
        float elapsed = 0f;
        while (IsCurrentFlow(currentFlow) && elapsed < duration)
        {
            if (!archeryPaused)
                elapsed += Time.deltaTime;
            yield return null;
        }

        recoveryCoroutine = null;
        if (IsCurrentFlow(currentFlow))
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

        if (recoveryCoroutine != null)
        {
            StopCoroutine(recoveryCoroutine);
            recoveryCoroutine = null;
        }

        DetachActiveEntry();
        Action onComplete = activeOnComplete;
        activeOnRelease = null;
        activeOnRecoveryStart = null;
        activeOnComplete = null;
        activeRecoveryDuration = 0f;
        archeryPaused = false;
        archeryActive = false;
        archeryInterrupted = false;
        ResetSpineToSetupPose();

        if (!IsDied)
        {
            ResumeMoving();
            HandleAnimators();
        }

        onComplete?.Invoke();
    }

    private void ReplayCurrentLocomotionAnimation()
    {
        TrackEntry current = SpineCtrl?.AnimationState?.GetCurrent(0);
        if (current?.Animation == null)
        {
            HandleAnimators();
            current = SpineCtrl?.AnimationState?.GetCurrent(0);
        }

        if (current?.Animation != null)
        {
            float trackTime = current.TrackTime;
            TrackEntry replay = SpineCtrl.PlayAnimation(
                current.Animation.Name,
                current.Loop,
                false);
            if (replay != null)
                replay.TrackTime = trackTime;
        }
    }

    private bool IsExpectedEntry(TrackEntry entry)
    {
        return ReferenceEquals(entry, activeEntry) && IsCurrentFlow(activeFlow);
    }

    private bool IsCurrentFlow(int currentFlow)
    {
        return isActiveAndEnabled && archeryActive && currentFlow == flowVersion;
    }

    private void DetachActiveEntry()
    {
        if (activeEntry == null)
            return;

        activeEntry.Complete -= OnPullComplete;
        activeEntry.Complete -= OnHoldComplete;
        activeEntry.Complete -= OnShootComplete;
        activeEntry.Interrupt -= OnArcheryInterrupted;
        activeEntry.End -= OnArcheryInterrupted;
        activeEntry = null;
    }

    // 中断不补发箭；立即解除对 TrackEntry 的引用，避免 Spine 回收后继续读取旧条目。
    private void OnArcheryInterrupted(TrackEntry entry)
    {
        if (!IsExpectedEntry(entry))
            return;

        DetachActiveEntry();
        archeryInterrupted = true;
    }

    private void ResolveSkeletonAnimation()
    {
        SkeletonAnimation resolved = skeletonAnimation;
        if (resolved == null && SpineCtrl != null)
            resolved = SpineCtrl.GetComponentInChildren<SkeletonAnimation>(true);

        if (skeletonAnimation != resolved)
        {
            UnbindSkeletonAnimation();
            skeletonAnimation = resolved;
            readyArrowSlot = null;
            readyArrowAttachment = null;
            readyArrowWarningLogged = false;
        }

        if (skeletonAnimation == null)
            return;

        skeletonAnimation.UpdateComplete -= OnSkeletonUpdateComplete;
        skeletonAnimation.UpdateComplete += OnSkeletonUpdateComplete;
        skeletonAnimation.OnAnimationRebuild -= OnSkeletonAnimationRebuild;
        skeletonAnimation.OnAnimationRebuild += OnSkeletonAnimationRebuild;
        ResolveReadyArrowReferences();
    }

    private void ResolveReadyArrowReferences()
    {
        if (skeletonAnimation?.Skeleton == null)
            return;

        if (readyArrowSlot == null)
            readyArrowSlot = skeletonAnimation.Skeleton.FindSlot(ReadyArrowSlotName);

        if (readyArrowSlot == null)
        {
            LogReadyArrowWarningOnce($"Spine slot not found: {ReadyArrowSlotName}");
            return;
        }

        if (readyArrowAttachment == null)
        {
            readyArrowAttachment = skeletonAnimation.Skeleton.GetAttachment(
                readyArrowSlot.Data.Index,
                ReadyArrowAttachmentName);
        }

        if (readyArrowAttachment == null)
        {
            LogReadyArrowWarningOnce(
                $"Spine attachment not found: {ReadyArrowAttachmentName} on slot {ReadyArrowSlotName}");
        }
    }

    private void LogReadyArrowWarningOnce(string message)
    {
        if (readyArrowWarningLogged)
            return;

        readyArrowWarningLogged = true;
        Debug.LogWarning(
            $"[Level129ActorCtrl] {message}; archery will continue without the ready-arrow visual.",
            this);
    }

    private void UnbindSkeletonAnimation()
    {
        if (skeletonAnimation == null)
            return;

        skeletonAnimation.UpdateComplete -= OnSkeletonUpdateComplete;
        skeletonAnimation.OnAnimationRebuild -= OnSkeletonAnimationRebuild;
    }

    private void BindSkinBinding()
    {
        UnbindSkinBinding();
        skinBinding = GetComponentInChildren<ActorSkinBinding>(true);
        if (skinBinding != null)
            skinBinding.CurrentSelectionApplied += OnSkinSelectionApplied;
    }

    private void UnbindSkinBinding()
    {
        if (skinBinding != null)
            skinBinding.CurrentSelectionApplied -= OnSkinSelectionApplied;
        skinBinding = null;
    }

    private void OnSkinSelectionApplied(ActorSkinBinding binding)
    {
        if (binding == skinBinding)
            refreshLocomotionAnimation = true;
    }

    private void ResetSpineToSetupPose()
    {
        ResolveSkeletonAnimation();
        if (skeletonAnimation?.Skeleton == null)
            return;

        // 只复位骨骼与约束：SetToSetupPose 会经 GetAttachment 的 default 皮肤回退把
        // 全部 setup 附件按 setup 颜色重挂一帧（idle 下一帧才清理），形成收尾零件闪现。
        // 弓零件 01/02 的淡出由 idle 混合完成；待发箭由本关状态机显式卸下。
        skeletonAnimation.Skeleton.SetBonesToSetupPose();
        skeletonAnimation.Skeleton.UpdateWorldTransform(Skeleton.Physics.Update);
        HideReadyArrow();
    }
}
