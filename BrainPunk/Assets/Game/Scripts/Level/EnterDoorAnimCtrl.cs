using System;
using System.Threading;
using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using Game.Events;
using Game.Level.Actor;
using Game.Scripts.Common;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 表现 actor 从门内走出来的状态。
/// </summary>
public class EnterDoorAnimCtrl : MonoBehaviour, IEventSender
{
    private const string DoorOpenAnimName = "door_open";
    private const string DoorCloseAnimName = "dose_close";
    private const float SkinBindingWaitTimeoutSeconds = 1f;

    [SerializeField] private ActorCtrl actor;
    [SerializeField] private ActorSpineCtrl spineCtrl;
    [SerializeField] private SpriteRenderer shadow;
    [SerializeField] private SpriteRendererGroupAlpha groupAlpha;
    [SerializeField] private bool isLeft = false;
    [SerializeField] private float doorOpenDelaySeconds = 0.4f;
    [SerializeField] private float actorFadeInSeconds = 0.2f;
    [SerializeField] private float walkOutSeconds = 0.2f;
    [SerializeField] private float doorCloseDelaySeconds = 0.2f;
    [SerializeField] private float closeDoorSfxDelaySeconds = 0.25f;

    private bool _isPlaying;
    private bool _hasDispatchedBlock;
    private ActorSkinBinding _skinBinding;
    private float _currentActorAlpha = 1f;

    public UniTask Play()
    {
        return Play(null);
    }

    public async UniTask Play(Action callback)
    {
        if (_isPlaying)
            return;

        TryAssignRefs();
        if (actor == null)
        {
            callback?.Invoke();
            return;
        }

        _isPlaying = true;
        SetBlockState(true);
        BindSkinCallbacks();

        try
        {
            if (!TryPrepareActorForPlay())
            {
                callback?.Invoke();
                return;
            }

            actor.StopMoving();

            await UniTask.WaitForSeconds(0.1f, cancellationToken: this.destroyCancellationToken);
            if (groupAlpha)
            {
                groupAlpha.CollectRenderers();
                groupAlpha.SetAlpha(0);

            }

            await WaitForSkinBindingLoadWindow();
            if (!HasValidActor())
                return;

            if (spineCtrl != null && !spineCtrl.IsPlayingAnimation(DoorOpenAnimName))
            {
                AudioManager.PlaySound("openDoor");
                spineCtrl.PlayAnimation(DoorOpenAnimName, false);
            }

            await UniTask.WaitForSeconds(GetSafeDelay(doorOpenDelaySeconds), cancellationToken: this.destroyCancellationToken);
            if (!HasValidActor())
                return;

            // AudioManager.PlaySound("enterDoor");
            actor.ResumeMoving();
            actor.SetMoveInput(isLeft ? -1f : 1f);
            SafeUpdateActorAnimation();
            var fadeTask = FadeInActor();

            await UniTask.WaitForSeconds(GetSafeDelay(walkOutSeconds), cancellationToken: this.destroyCancellationToken);
            if (!HasValidActor())
                return;
            await fadeTask;
            if (!HasValidActor())
                return;

            actor.SetMoveInput(0f);
            SafeUpdateActorAnimation();
            actor.StopMoving();

            await UniTask.WaitForSeconds(GetSafeDelay(doorCloseDelaySeconds), cancellationToken: this.destroyCancellationToken);

            if (spineCtrl != null)
            {
                spineCtrl.PlayAnimation(DoorCloseAnimName, false);
                await UniTask.WaitForSeconds(GetSafeDelay(closeDoorSfxDelaySeconds), cancellationToken: this.destroyCancellationToken);
                AudioManager.PlaySound("closeDoor");
            }

            callback?.Invoke();
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            UnbindSkinCallbacks();
            SetActorAlpha(1f);
            SetBlockState(false);
            _isPlaying = false;
        }
    }

    private void TryAssignRefs()
    {
        if (actor == null)
        {
            actor = transform.root.Find("Actor")?.GetComponent<ActorCtrl>();
            if (actor == null)
            {
                actor = FindFirstObjectByType<ActorCtrl>();
            }
        }

        if (spineCtrl == null)
        {
            spineCtrl = GetComponent<ActorSpineCtrl>();
        }

        if (shadow == null && actor != null)
        {
            shadow = actor.transform.Find("Shadow")?.GetComponent<SpriteRenderer>();
        }

        if (groupAlpha == null && actor != null)
        {
            groupAlpha = actor.GetComponentInChildren<SpriteRendererGroupAlpha>();
        }
    }

    private async UniTask FadeInActor()
    {
        if (actor == null || actor.SpineCtrl == null)
            return;

        if (actorFadeInSeconds <= 0f)
        {
            SetActorAlpha(1f);
            return;
        }

        float elapsed = 0f;
        while (elapsed < actorFadeInSeconds)
        {
            elapsed += Time.deltaTime;
            SetActorAlpha(Mathf.Clamp01(elapsed / actorFadeInSeconds));
            await UniTask.Yield(cancellationToken: this.destroyCancellationToken);
        }

        SetActorAlpha(1f);
    }

    private void SetActorAlpha(float alpha)
    {
        _currentActorAlpha = Mathf.Clamp01(alpha);
        actor?.SpineCtrl?.SetAlpha(_currentActorAlpha);

        if (shadow == null && actor != null)
            shadow = actor.transform.Find("Shadow")?.GetComponent<SpriteRenderer>();

        if (shadow)
        {
            shadow.gameObject.SetActive(_currentActorAlpha >= .5f);
        }

        if (groupAlpha)
            groupAlpha.SetAlpha(_currentActorAlpha);
    }

    private UniTask WaitForSkinBindingLoadWindow()
    {
        return UniTask.WaitForSeconds(SkinBindingWaitTimeoutSeconds, cancellationToken: this.destroyCancellationToken);
    }

    private void BindSkinCallbacks()
    {
        UnbindSkinCallbacks();

        if (actor == null)
            return;

        _skinBinding = actor.GetComponentInChildren<ActorSkinBinding>(true);
        if (_skinBinding == null)
            return;

        _skinBinding.CurrentSelectionApplied += OnSkinBindingApplied;
    }

    private void UnbindSkinCallbacks()
    {
        if (_skinBinding != null)
            _skinBinding.CurrentSelectionApplied -= OnSkinBindingApplied;

        _skinBinding = null;
    }

    private void OnSkinBindingApplied(ActorSkinBinding binding)
    {
        if (!_isPlaying || binding != _skinBinding || groupAlpha == null)
            return;

        groupAlpha.CollectRenderers();
        groupAlpha.SetAlpha(_currentActorAlpha);
    }

    private bool TryPrepareActorForPlay()
    {
        if (!HasValidActor())
            return false;

        actor.transform.position = transform.position;
        SetActorAlpha(0f);
        return true;
    }

    private bool HasValidActor()
    {
        return actor != null;
    }

    private void SafeUpdateActorAnimation()
    {
        if (actor?.SpineCtrl == null)
            return;

        actor.UpdateAnimation();
    }

    private float GetSafeDelay(float seconds)
    {
        return Mathf.Max(0f, seconds);
    }

    private void SetBlockState(bool isBlocked)
    {
        if (_hasDispatchedBlock == isBlocked)
            return;

        _hasDispatchedBlock = isBlocked;
        this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, isBlocked);
    }

#if UNITY_EDITOR
    [Button("Auto Assign Refs")]
    private void AutoAssignRefs()
    {
        TryAssignRefs();
    }
#endif
}
