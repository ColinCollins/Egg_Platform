using System;
using DG.Tweening;
using Game.Scripts.Common;
using Spine;
using Spine.Unity;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class Level106MoveButtonLock : MonoBehaviour
{
    [SerializeField] private SkeletonGraphic skeletonGraphic;
    [SerializeField] private string idleAnimation = "lock_idle";
    [SerializeField] private string unlockAnimation = "unlock";
    [SerializeField] private string unlockSfxTag = "unlock";
    [SerializeField, Min(0f)] private float destroyDelay = 0.1f;
    [SerializeField, Min(0f)] private float showDuration = 0.3f;
    [SerializeField] private Ease showEase = Ease.OutBack;

    private TrackEntry unlockEntry;
    private Tween showTween;
    private Vector3 visibleScale;
    private Action unlockCompleted;
    private bool isUnlocking;

    private void Awake()
    {
        visibleScale = transform.localScale;
        ResolveSkeletonGraphic();
        Spine.AnimationState state = skeletonGraphic != null ? skeletonGraphic.AnimationState : null;
        if (state != null && !string.IsNullOrEmpty(idleAnimation))
        {
            state.SetAnimation(0, idleAnimation, false);
        }
    }

    private void OnEnable()
    {
        PlayShowAnimation();
    }

    public void PlayUnlockAndDestroy(Action onUnlocked)
    {
        if (isUnlocking)
        {
            return;
        }

        isUnlocking = true;
        StopShowAnimation(true);
        unlockCompleted = onUnlocked;
        if (!string.IsNullOrEmpty(unlockSfxTag))
        {
            AudioManager.PlaySound(unlockSfxTag);
        }

        ResolveSkeletonGraphic();
        Spine.AnimationState state = skeletonGraphic != null ? skeletonGraphic.AnimationState : null;
        unlockEntry = state != null && !string.IsNullOrEmpty(unlockAnimation)
            ? state.SetAnimation(0, unlockAnimation, false)
            : null;

        if (unlockEntry == null)
        {
            ScheduleUnlockCompletion();
            return;
        }

        unlockEntry.Complete += OnUnlockComplete;
    }

    private void ResolveSkeletonGraphic()
    {
        if (skeletonGraphic == null)
        {
            skeletonGraphic = GetComponent<SkeletonGraphic>();
        }

        skeletonGraphic?.Initialize(false);
    }

    private void OnUnlockComplete(TrackEntry entry)
    {
        if (unlockEntry != null)
        {
            unlockEntry.Complete -= OnUnlockComplete;
            unlockEntry = null;
        }

        ScheduleUnlockCompletion();
    }

    private void ScheduleUnlockCompletion()
    {
        if (destroyDelay <= 0f)
        {
            CompleteUnlock();
            return;
        }

        Invoke(nameof(CompleteUnlock), destroyDelay);
    }

    private void CompleteUnlock()
    {
        Action callback = unlockCompleted;
        unlockCompleted = null;
        callback?.Invoke();
        Destroy(gameObject);
    }

    private void PlayShowAnimation()
    {
        StopShowAnimation(false);
        transform.localScale = Vector3.zero;
        if (showDuration <= 0f)
        {
            transform.localScale = visibleScale;
            return;
        }

        showTween = transform.DOScale(visibleScale, showDuration)
            .SetEase(showEase)
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(() => showTween = null);
    }

    private void StopShowAnimation(bool restoreScale)
    {
        showTween?.Kill(false);
        showTween = null;
        if (restoreScale)
        {
            transform.localScale = visibleScale;
        }
    }

    private void OnDisable()
    {
        StopShowAnimation(false);
    }

    private void OnDestroy()
    {
        StopShowAnimation(false);
        CancelInvoke();
        unlockCompleted = null;
        if (unlockEntry != null)
        {
            unlockEntry.Complete -= OnUnlockComplete;
            unlockEntry = null;
        }
    }
}
