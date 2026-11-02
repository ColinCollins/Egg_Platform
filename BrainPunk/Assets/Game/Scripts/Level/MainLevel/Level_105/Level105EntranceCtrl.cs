using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Level.Actor;
using Game.Scripts.Common;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Level 105 专属起始门出场控制器。表现沿用共享起始门的真实门、Actor、皮肤淡入与音效，
/// 但播放、挂起与取消完全由 Level105Ctrl 管理，且不覆盖 GameplayPanel，确保首帧规则仍能接收输入；
/// 判负取消后的异步收尾受死亡门禁保护，不会再次恢复 Actor。
/// </summary>
public sealed class Level105EntranceCtrl : MonoBehaviour
{
    #region Constants

    private const string DoorOpenAnimName = "door_open";
    private const string DoorCloseAnimName = "dose_close";
    private const float SkinBindingWaitTimeoutSeconds = 1f;

    #endregion

    #region Inspector

    [SerializeField] private ActorCtrl actor;
    [SerializeField] private ActorSpineCtrl spineCtrl;
    [SerializeField] private SpriteRenderer shadow;
    [SerializeField] private SpriteRendererGroupAlpha groupAlpha;
    [SerializeField] private bool isLeft;
    [SerializeField] private float doorOpenDelaySeconds = 0.4f;
    [SerializeField] private float actorFadeInSeconds = 0.2f;
    [SerializeField] private float walkOutSeconds = 0.2f;
    [SerializeField] private float doorCloseDelaySeconds = 0.2f;
    [SerializeField] private float closeDoorSfxDelaySeconds = 0.25f;

    #endregion

    #region Runtime State

    private bool isPlaying;
    private bool isSuspended;
    private bool isWalkingOut;
    private bool cancelledForFailure;
    private bool cancelledForTeardown;
    private float currentActorAlpha = 1f;
    private ActorSkinBinding skinBinding;
    private CancellationTokenSource playCancellationSource;

    #endregion

    #region Public API

    /// <summary>
    /// 播放一次 Level 105 本地出场。只有完整走完开门、淡入、走出和关门流程才返回 true；
    /// 判负、销毁或显式取消均返回 false，调用方不得据此恢复 Actor。
    /// </summary>
    public async UniTask<bool> Play()
    {
        if (isPlaying)
            return false;

        TryAssignRefs();
        if (actor == null)
            return true;

        isPlaying = true;
        cancelledForFailure = false;
        cancelledForTeardown = false;
        isWalkingOut = false;

        playCancellationSource?.Dispose();
        playCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());
        CancellationToken cancellationToken = playCancellationSource.Token;

        BindSkinCallbacks();

        try
        {
            if (!TryPrepareActorForPlay())
                return false;

            actor.StopMoving();

            await WaitForActiveSeconds(0.1f, cancellationToken);
            if (groupAlpha != null)
            {
                groupAlpha.CollectRenderers();
                groupAlpha.SetAlpha(0f);
            }

            await WaitForActiveSeconds(SkinBindingWaitTimeoutSeconds, cancellationToken);
            if (!CanContinue())
                return false;

            if (spineCtrl != null && !spineCtrl.IsPlayingAnimation(DoorOpenAnimName))
            {
                AudioManager.PlaySound("openDoor");
                spineCtrl.PlayAnimation(DoorOpenAnimName, false);
            }

            await WaitForActiveSeconds(doorOpenDelaySeconds, cancellationToken);
            if (!CanContinue())
                return false;

            isWalkingOut = true;
            ApplyWalkingOutMotion();
            UniTask fadeTask = FadeInActor(cancellationToken);

            await WaitForActiveSeconds(walkOutSeconds, cancellationToken);
            if (!CanContinue())
                return false;

            await fadeTask;
            if (!CanContinue())
                return false;

            isWalkingOut = false;
            StopActorForOpeningIdle();

            await WaitForActiveSeconds(doorCloseDelaySeconds, cancellationToken);
            if (!CanContinue())
                return false;

            if (spineCtrl != null)
            {
                spineCtrl.PlayAnimation(DoorCloseAnimName, false);
                await WaitForActiveSeconds(closeDoorSfxDelaySeconds, cancellationToken);
                if (!CanContinue())
                    return false;

                AudioManager.PlaySound("closeDoor");
            }

            return CanContinue();
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            isWalkingOut = false;
            UnbindSkinCallbacks();

            // 只有正常结束且 Actor 仍存活时才恢复可见和静止状态；判负/销毁路径绝不恢复死亡 Actor。
            if (!cancelledForFailure &&
                !cancelledForTeardown &&
                actor != null &&
                !actor.IsDied &&
                isActiveAndEnabled)
            {
                SetActorAlpha(1f);
                StopActorForOpeningIdle();
            }

            isPlaying = false;

            playCancellationSource?.Dispose();
            playCancellationSource = null;
        }
    }

    /// <summary>冻结或恢复本地出场节奏；不修改全局 Time.timeScale。</summary>
    public void SetSuspended(bool suspended)
    {
        isSuspended = suspended;
        if (!isPlaying || actor == null || actor.IsDied)
            return;

        if (suspended)
        {
            actor.SetMoveInput(0f);
            actor.StopMoving();
            SafeUpdateActorAnimation();
            return;
        }

        if (isWalkingOut && !cancelledForFailure && !cancelledForTeardown)
            ApplyWalkingOutMotion();
    }

    /// <summary>
    /// 同帧取消未完成出场并把仍存活的 Actor 恢复为可播放死亡动画的可见静止状态。
    /// 后续异步 finally 通过 cancelledForFailure 门禁，不会再次恢复移动或覆盖死亡状态。
    /// </summary>
    public void CancelForFailure()
    {
        if (!isPlaying)
            return;

        cancelledForFailure = true;
        isWalkingOut = false;
        playCancellationSource?.Cancel();

        if (actor != null && !actor.IsDied)
        {
            actor.SetMoveInput(0f);
            actor.StopMoving();
            SetActorAlpha(1f);
        }
    }

    /// <summary>关卡销毁或外部流程终结时取消，不再恢复 Actor 表现。</summary>
    public void CancelForTeardown()
    {
        if (!isPlaying)
            return;

        cancelledForTeardown = true;
        isWalkingOut = false;
        playCancellationSource?.Cancel();
    }

    #endregion

    #region Reference Resolution

    private void TryAssignRefs()
    {
        if (actor == null)
        {
            actor = transform.root.Find("Actor")?.GetComponent<ActorCtrl>();
            if (actor == null)
                actor = FindFirstObjectByType<ActorCtrl>();
        }

        spineCtrl ??= GetComponent<ActorSpineCtrl>();

        if (shadow == null && actor != null)
            shadow = actor.transform.Find("Shadow")?.GetComponent<SpriteRenderer>();

        if (groupAlpha == null && actor != null)
            groupAlpha = actor.GetComponentInChildren<SpriteRendererGroupAlpha>();
    }

    #endregion

    #region Entrance Flow

    private bool TryPrepareActorForPlay()
    {
        if (!CanContinue())
            return false;

        actor.transform.position = transform.position;
        SetActorAlpha(0f);
        return true;
    }

    private bool CanContinue()
    {
        return !cancelledForFailure &&
               !cancelledForTeardown &&
               actor != null &&
               !actor.IsDied &&
               isActiveAndEnabled;
    }

    private async UniTask WaitForActiveSeconds(float seconds, CancellationToken cancellationToken)
    {
        float duration = Mathf.Max(0f, seconds);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

            if (isSuspended)
                continue;

            elapsed += Mathf.Max(0f, Time.deltaTime);
        }
    }

    private async UniTask FadeInActor(CancellationToken cancellationToken)
    {
        if (actor == null || actor.SpineCtrl == null)
            return;

        float duration = Mathf.Max(0f, actorFadeInSeconds);
        if (duration <= 0f)
        {
            SetActorAlpha(1f);
            return;
        }

        try
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

                if (!CanContinue())
                    return;

                if (isSuspended)
                    continue;

                elapsed += Mathf.Max(0f, Time.deltaTime);
                SetActorAlpha(Mathf.Clamp01(elapsed / duration));
            }

            if (CanContinue())
                SetActorAlpha(1f);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ApplyWalkingOutMotion()
    {
        if (actor == null || actor.IsDied)
            return;

        actor.ResumeMoving();
        actor.SetMoveInput(isLeft ? -1f : 1f);
        SafeUpdateActorAnimation();
    }

    private void StopActorForOpeningIdle()
    {
        if (actor == null || actor.IsDied)
            return;

        actor.SetMoveInput(0f);
        SafeUpdateActorAnimation();
        actor.StopMoving();
    }

    #endregion

    #region Presentation and Skin Binding

    private void SetActorAlpha(float alpha)
    {
        currentActorAlpha = Mathf.Clamp01(alpha);
        actor?.SpineCtrl?.SetAlpha(currentActorAlpha);

        if (shadow == null && actor != null)
            shadow = actor.transform.Find("Shadow")?.GetComponent<SpriteRenderer>();

        if (shadow != null)
            shadow.gameObject.SetActive(currentActorAlpha >= 0.5f);

        if (groupAlpha != null)
            groupAlpha.SetAlpha(currentActorAlpha);
    }

    private void BindSkinCallbacks()
    {
        UnbindSkinCallbacks();
        if (actor == null)
            return;

        skinBinding = actor.GetComponentInChildren<ActorSkinBinding>(true);
        if (skinBinding != null)
            skinBinding.CurrentSelectionApplied += OnSkinBindingApplied;
    }

    private void UnbindSkinCallbacks()
    {
        if (skinBinding != null)
            skinBinding.CurrentSelectionApplied -= OnSkinBindingApplied;

        skinBinding = null;
    }

    private void OnSkinBindingApplied(ActorSkinBinding binding)
    {
        if (!isPlaying || binding != skinBinding || groupAlpha == null)
            return;

        groupAlpha.CollectRenderers();
        groupAlpha.SetAlpha(currentActorAlpha);
    }

    private void SafeUpdateActorAnimation()
    {
        if (actor?.SpineCtrl != null)
            actor.UpdateAnimation();
    }

    #endregion

    #region Cleanup

    private void OnDisable()
    {
        CancelForTeardown();
    }

    #endregion

    #region Editor

#if UNITY_EDITOR
    [Button("Auto Assign Refs")]
    private void AutoAssignRefs()
    {
        TryAssignRefs();
    }
#endif

    #endregion
}
