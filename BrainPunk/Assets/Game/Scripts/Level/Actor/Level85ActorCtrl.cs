using System;
using System.Threading.Tasks;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level85 actor-local presentation controls.
/// </summary>
public class Level85ActorCtrl : ActorCtrl, IDebuger
{
    private const string DefaultCelebrateAnimName = "celebrate";
    private const string DefaultIdleAnimName = "idle";

    [Header("Level85 Presentation")]
    [SerializeField] private string celebrateAnimName = DefaultCelebrateAnimName;
    [SerializeField] private string idleAnimName = DefaultIdleAnimName;

    private int presentationSession;
    private bool isLevel85PresentationLocked;
    private bool isCelebrating;
    private bool isRandomTurning;

    public bool IsCelebrating => isCelebrating;
    public bool IsRandomTurning => isRandomTurning;

    public override void OnUpdate()
    {
        if (isLevel85PresentationLocked)
        {
            UpdateLockedPresentationState();
            return;
        }

        base.OnUpdate();
    }

    protected override void HandleAnimators()
    {
        if (isLevel85PresentationLocked)
        {
            return;
        }

        base.HandleAnimators();
    }

    public async Task PlayDefaultCelebrate()
    {
        if (IsDied)
        {
            return;
        }

        presentationSession++;
        isLevel85PresentationLocked = true;
        isCelebrating = true;
        isRandomTurning = false;
        StopAllMotion();

        float random = UnityEngine.Random.Range(0.5f, 1f);
        await UniTask.WaitForSeconds(random);

        if (!TryPlayAnimation(celebrateAnimName, true))
        {
            PlayIdleReadable();
        }
    }

    public void StopCelebrateToIdle()
    {
        presentationSession++;
        isCelebrating = false;
        isRandomTurning = false;
        isLevel85PresentationLocked = true;
        StopAllMotion();
        PlayIdleReadable();
    }

    public async UniTask PlayRandomFacingSequence(
        Transform target,
        int turnCount,
        float turnInterval,
        int levelSession,
        Func<int, bool> isLevelSessionValid)
    {
        int localSession = ++presentationSession;
        isCelebrating = false;
        isRandomTurning = true;
        isLevel85PresentationLocked = true;
        StopAllMotion();
        PlayIdleReadable();

        int safeTurnCount = Mathf.Clamp(turnCount, 2, 3);
        float safeInterval = Mathf.Max(0f, turnInterval);

        for (int i = 0; i < safeTurnCount; i++)
        {
            if (!IsPresentationValid(localSession, levelSession, isLevelSessionValid))
            {
                return;
            }

            SetActorFaceDirection(UnityEngine.Random.value >= 0.5f);

            AudioManager.PlaySound("grassCut");
            try
            {
                await UniTask.WaitForSeconds(
                    safeInterval,
                    cancellationToken: this.GetCancellationTokenOnDestroy());
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        if (!IsPresentationValid(localSession, levelSession, isLevelSessionValid))
        {
            return;
        }

        isRandomTurning = false;
        FaceTarget(target);
    }

    public void FaceTarget(Transform target)
    {
        if (target == null)
        {
            return;
        }

        isCelebrating = false;
        isRandomTurning = false;
        isLevel85PresentationLocked = true;
        StopAllMotion();
        SetActorFaceDirection(target.position.x >= transform.position.x);
        PlayIdleReadable();
    }

    public void CancelLevel85Presentation(bool restoreAnimation)
    {
        presentationSession++;
        isCelebrating = false;
        isRandomTurning = false;
        isLevel85PresentationLocked = false;

        if (restoreAnimation && !IsDied)
        {
            PlayIdleReadable();
            UpdateAnimation();
        }
    }

    private bool IsPresentationValid(
        int localSession,
        int levelSession,
        Func<int, bool> isLevelSessionValid)
    {
        return localSession == presentationSession
            && isActiveAndEnabled
            && !IsDied
            && (isLevelSessionValid == null || isLevelSessionValid(levelSession));
    }

    private void UpdateLockedPresentationState()
    {
        if (!IsReady)
        {
            return;
        }

        UpdateAirbornStatus();
        StopAllMotion();
        HandleCollision();
    }

    private void StopAllMotion()
    {
        SetMoveInput(0f);

        if (RB != null)
        {
            RB.linearVelocity = Vector2.zero;
        }
    }

    private void PlayIdleReadable()
    {
        if (IsDied || SpineCtrl == null)
        {
            return;
        }

        TryPlayAnimation(idleAnimName, true);
    }

    private bool TryPlayAnimation(string animationName, bool loop)
    {
        if (SpineCtrl == null || SpineCtrl.AnimationState == null)
        {
            this.LogWarning("[Level85ActorCtrl] SpineCtrl or AnimationState is missing.");
            return false;
        }

        if (string.IsNullOrEmpty(animationName))
        {
            this.LogWarning("[Level85ActorCtrl] Animation name is empty.");
            return false;
        }

        var skeletonData = SpineCtrl.AnimationState.Data?.SkeletonData;
        if (skeletonData == null)
        {
            this.LogWarning("[Level85ActorCtrl] SkeletonData is missing.");
            return false;
        }

        if (skeletonData.FindAnimation(animationName) == null)
        {
            this.LogWarning($"[Level85ActorCtrl] Animation not found: {animationName}");
            return false;
        }

        return SpineCtrl.PlayAnimation(animationName, loop, false) != null;
    }

    private void OnDisable()
    {
        presentationSession++;
        isCelebrating = false;
        isRandomTurning = false;
        isLevel85PresentationLocked = false;
    }
}
