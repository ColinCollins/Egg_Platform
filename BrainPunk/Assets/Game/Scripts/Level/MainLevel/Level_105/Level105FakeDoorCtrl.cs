using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Scripts.Common;
using Lofelt.NiceVibrations;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Level 105 专属假门（第一扇门 / FakeDoor）真假门演出控制器。复用 door_0 资源自带的门 Spine、
/// 彩带、彩虹粒子与共享 SuccessAnimCtrl / ActorDestroyAnimHandle 的既有动画名、音效、震动和时间参数，
/// 但播放、挂起与取消完全由 Level105Ctrl 管理：进门表现不进入真实胜利/奖励/存档/解锁业务，
/// 也绝不销毁或替换 Actor；出门表现按门当前朝向让同一 Actor 向左走出并保持朝左。
/// 判负中断、系统面板挂起与关卡销毁都有明确收尾，不会冻结半开门或污染 Actor。
/// </summary>
public sealed class Level105FakeDoorCtrl : MonoBehaviour
{
    #region Constants

    // 与共享 SuccessAnimCtrl / EnterDoorAnimCtrl 完全一致的真实动画资源名（dose_close 为资源原名）。
    private const string DoorOpenAnimName = "door_open";
    private const string DoorCloseAnimName = "dose_close";

    #endregion

    #region Inspector

    /// <summary>进出假门的同一个主角 Box；为空时回退取关卡根 Actor。</summary>
    [SerializeField] private ActorCtrl actor;

    /// <summary>FakeDoor 子节点 door 上的门 Spine 控制器；复用 door_open / dose_close。</summary>
    [SerializeField] private ActorSpineCtrl doorSpine;

    /// <summary>FakeDoor 子节点 ribbon（彩带）；进门成功表现期间启用。</summary>
    [SerializeField] private GameObject ribbon;

    /// <summary>FakeDoor 的彩虹粒子（ConfettiBlastRainbow），与 SuccessAnimCtrl.rainbon 同一引用。</summary>
    [SerializeField] private ParticleSystem rainbow;

    /// <summary>Box 进门收拢目标，对应 ActorDestroyAnimHandle.endTarget（door 子节点）；为空回退门根节点。</summary>
    [SerializeField] private Transform enterTarget;

    [Header("进门（与 SuccessAnimCtrl 对齐）")]
    /// <summary>开门后到 enterDoor 音效与进门收拢开始的等待，固定 0.4 秒。</summary>
    [SerializeField] private float doorOpenDelaySeconds = 0.4f;

    /// <summary>Box 移动/旋转/缩小进门消失的时长，与 door_0 上 ActorDestroyAnimHandle.duration 一致，固定 1.2 秒。</summary>
    [SerializeField] private float enterDurationSeconds = 1.2f;

    /// <summary>进门收拢目标点相对 enterTarget 的偏移，与 ActorDestroyAnimHandle.enterDoorOffset 一致。</summary>
    [SerializeField] private Vector3 enterDoorOffset = new Vector3(0f, 1f, 0f);

    /// <summary>彩带关闭到关门动画的等待，固定 0.2 秒。</summary>
    [SerializeField] private float ribbonOffToDoorCloseSeconds = 0.2f;

    /// <summary>关门动画到关门音效的等待，固定 0.25 秒。</summary>
    [SerializeField] private float closeDoorSfxDelaySeconds = 0.25f;

    /// <summary>成功震动与彩虹粒子后的收尾等待，固定约 0.8 秒。</summary>
    [SerializeField] private float finishSettleSeconds = 0.8f;

    [Header("出门（与 EnterDoorAnimCtrl 对齐）")]
    /// <summary>Box 从门内向左走出的时长，固定 0.2 秒。</summary>
    [SerializeField] private float exitWalkSeconds = 0.2f;

    /// <summary>出门时 Box 淡入时长，固定 0.2 秒。</summary>
    [SerializeField] private float exitFadeInSeconds = 0.2f;

    /// <summary>出门停稳到关门动画的等待，固定 0.2 秒。</summary>
    [SerializeField] private float exitDoorCloseDelaySeconds = 0.2f;

    #endregion

    #region Runtime State

    private bool isPlayingEnter;
    private bool isPlayingExit;
    private bool isWalkingOut;
    private bool isSuspended;
    private bool cancelledForFailure;
    private bool cancelledForTeardown;
    private CancellationTokenSource flowCancellation;
    private Sequence enterSequence;

    private bool isActorHidden;
    private bool hasActorBaseline;
    private Vector3 capturedActorScale = Vector3.one;
    private Vector3 capturedStandPosition;
    private bool actorPhysicsCaptured;
    private bool actorPhysicsWasSimulated;
    private float currentActorAlpha = 1f;

    private int exitDirectionSign = -1;
    private bool doorVisualsSuspended;
    private float doorSpineTimeScale = 1f;
    private float ribbonSpineTimeScale = 1f;
    private ActorSpineCtrl ribbonSpine;
    private SpriteRenderer shadow;
    private SpriteRendererGroupAlpha groupAlpha;

    #endregion

    #region Public API

    /// <summary>假门进门成功表现是否正在播放（含收尾）。</summary>
    public bool IsPlayingEnter => isPlayingEnter;

    /// <summary>假门重新出门表现是否正在播放。</summary>
    public bool IsPlayingExit => isPlayingExit;

    /// <summary>Box 是否已完整进门并隐藏在假门内；可见进门阶段为 false，出门淡入开始后恢复为 false。</summary>
    public bool IsActorHidden => isActorHidden;

    /// <summary>
    /// 播放一次与标准 SuccessAnimCtrl 完全对齐的假门进门成功表现：开门、彩带、enterDoor 音效、
    /// 同一 Actor 移动/旋转/缩小进门消失、彩带关闭、关门、关门音效、成功震动、彩虹粒子与约 0.8 秒收尾。
    /// 只有完整走完才返回 true；判负中断、销毁或显式取消均返回 false。全程不销毁 Actor、不触发真实胜利业务。
    /// </summary>
    public async UniTask<bool> PlayEnter()
    {
        if (isPlayingEnter || isPlayingExit)
            return false;

        TryAssignRefs();
        if (actor == null)
            return true;

        isPlayingEnter = true;
        cancelledForFailure = false;
        cancelledForTeardown = false;
        isActorHidden = false;

        flowCancellation?.Dispose();
        flowCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());
        CancellationToken cancellationToken = flowCancellation.Token;

        try
        {
            CaptureActorBaseline();
            actor.StopMoving();

            PlayDoorOpen();
            if (ribbon != null)
                ribbon.SetActive(true);

            await WaitForActiveSeconds(doorOpenDelaySeconds, cancellationToken);
            if (!CanContinue())
                return false;

            AudioManager.PlaySound("enterDoor");
            await PlayEnterTweenAsync();
            if (!CanContinue())
                return false;

            // Box 已完整进门隐藏；此后违规走“收尾完成→重新出门→死亡”路径。
            SuspendHiddenActorPhysics();
            isActorHidden = true;

            if (ribbon != null)
                ribbon.SetActive(false);

            await WaitForActiveSeconds(ribbonOffToDoorCloseSeconds, cancellationToken);
            if (!CanContinue())
                return false;

            PlayDoorCloseAnimation();
            await WaitForActiveSeconds(closeDoorSfxDelaySeconds, cancellationToken);
            if (!CanContinue())
                return false;

            AudioManager.PlaySound("closeDoor");
            PlaySuccessVibration();
            if (rainbow != null)
                rainbow.Play();

            await WaitForActiveSeconds(finishSettleSeconds, cancellationToken);
            return CanContinue();
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            isPlayingEnter = false;
            enterSequence?.Kill();
            enterSequence = null;

            flowCancellation?.Dispose();
            flowCancellation = null;
        }
    }

    /// <summary>
    /// 播放一次假门重新出门：同一 FakeDoor 开门，同一 Actor 按门当前朝向向其朝向侧走出并淡入，
    /// 停稳后假门完整关门并播放关门音效。只有完整走完才返回 true；销毁或显式取消均返回 false。
    /// 完成后 Actor 停在门朝向侧（当前布局为门左侧）并保持该朝向。
    /// </summary>
    public async UniTask<bool> PlayExit()
    {
        if (isPlayingEnter || isPlayingExit)
            return false;

        TryAssignRefs();
        if (actor == null)
            return true;

        isPlayingExit = true;
        cancelledForFailure = false;
        cancelledForTeardown = false;
        isWalkingOut = false;

        flowCancellation?.Dispose();
        flowCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());
        CancellationToken cancellationToken = flowCancellation.Token;

        try
        {
            PrepareActorAtDoorForExit();
            PlayDoorOpen();

            await WaitForActiveSeconds(doorOpenDelaySeconds, cancellationToken);
            if (!CanContinue())
                return false;

            isWalkingOut = true;
            if (!isSuspended)
                ApplyExitWalkMotion();
            UniTask fadeTask = FadeInActor(cancellationToken);

            await WaitForActiveSeconds(exitWalkSeconds, cancellationToken);
            if (!CanContinue())
                return false;

            await fadeTask;
            if (!CanContinue())
                return false;

            isWalkingOut = false;
            StopActorAtDoorSide();

            await WaitForActiveSeconds(exitDoorCloseDelaySeconds, cancellationToken);
            if (!CanContinue())
                return false;

            PlayDoorCloseAnimation();
            await WaitForActiveSeconds(closeDoorSfxDelaySeconds, cancellationToken);
            if (!CanContinue())
                return false;

            AudioManager.PlaySound("closeDoor");
            return CanContinue();
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            isWalkingOut = false;
            isPlayingExit = false;

            flowCancellation?.Dispose();
            flowCancellation = null;
        }
    }

    /// <summary>冻结或恢复假门进出门表现与计时；不修改全局 Time.timeScale。</summary>
    public void SetSuspended(bool suspended)
    {
        isSuspended = suspended;

        if (suspended)
        {
            if (enterSequence != null && enterSequence.IsActive() && enterSequence.IsPlaying())
                enterSequence.Pause();
            if (rainbow != null && rainbow.isPlaying)
                rainbow.Pause();
        }
        else
        {
            if (enterSequence != null && enterSequence.IsActive() && !enterSequence.IsPlaying())
                enterSequence.Play();
            if (rainbow != null && rainbow.isPlaying)
                rainbow.Play();
        }

        SetDoorVisualsSuspended(suspended);

        if (actor == null || actor.IsDied)
            return;

        if (suspended)
        {
            if (isWalkingOut)
            {
                actor.SetMoveInput(0f);
                SafeUpdateActorAnimation();
            }

            return;
        }

        if (isWalkingOut && !cancelledForFailure && !cancelledForTeardown)
            ApplyExitWalkMotion();
    }

    /// <summary>
    /// Box 尚未完全进门隐藏时的判负中断：同帧取消进门并杀掉收拢 Tween，把同一 Actor 恢复为
    /// 正常尺寸、可见与物理状态（不位移），彩带关闭；假门随后与死亡动画并行完整关门并播放关门音效，
    /// 不冻结在半开状态，也不等待关门完成。
    /// </summary>
    public void CancelEnterForFailure()
    {
        if (!isPlayingEnter)
            return;

        cancelledForFailure = true;
        enterSequence?.Kill();
        enterSequence = null;
        flowCancellation?.Cancel();

        if (ribbon != null)
            ribbon.SetActive(false);

        if (actor != null && !actor.IsDied)
            RestoreActorPresentationForFailure();

        PlayDoorCloseAnimation();
        PlayParallelDoorCloseSfxAsync().Forget();
    }

    /// <summary>关卡销毁、重开或外部流程终结时取消：可靠取消异步与 Tween，并把仍存活的 Actor 恢复为未被污染的状态。</summary>
    public void CancelForTeardown()
    {
        cancelledForTeardown = true;
        isWalkingOut = false;

        enterSequence?.Kill();
        enterSequence = null;
        flowCancellation?.Cancel();

        SetDoorVisualsSuspended(false);

        if (ribbon != null)
            ribbon.SetActive(false);

        if (actor != null && !actor.IsDied && hasActorBaseline)
        {
            RestoreHiddenActorPhysics();
            actor.transform.localScale = capturedActorScale;
            Vector3 euler = actor.transform.localEulerAngles;
            actor.transform.localEulerAngles = new Vector3(0f, euler.y, 0f);
            SetActorAlpha(1f);
        }

        isActorHidden = false;
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

        if (doorSpine == null)
        {
            Transform doorChild = transform.Find("door");
            doorSpine = doorChild != null ? doorChild.GetComponent<ActorSpineCtrl>() : null;
            if (doorSpine == null)
                doorSpine = GetComponentInChildren<ActorSpineCtrl>(true);
        }

        if (ribbon == null)
            ribbon = transform.Find("ribbon")?.gameObject;

        if (ribbonSpine == null && ribbon != null)
            ribbonSpine = ribbon.GetComponent<ActorSpineCtrl>();

        if (rainbow == null)
        {
            Transform rainbowChild = transform.Find("ConfettiBlastRainbow");
            rainbow = rainbowChild != null ? rainbowChild.GetComponent<ParticleSystem>() : null;
            if (rainbow == null)
                rainbow = GetComponentInChildren<ParticleSystem>(true);
        }

        if (enterTarget == null)
        {
            Transform doorChild = transform.Find("door");
            enterTarget = doorChild != null ? doorChild : transform;
        }

        if (shadow == null && actor != null)
            shadow = actor.transform.Find("Shadow")?.GetComponent<SpriteRenderer>();

        if (groupAlpha == null && actor != null)
            groupAlpha = actor.GetComponentInChildren<SpriteRendererGroupAlpha>(true);
    }

    #endregion

    #region Enter Flow

    private void CaptureActorBaseline()
    {
        if (actor == null || hasActorBaseline)
            return;

        hasActorBaseline = true;
        capturedActorScale = actor.transform.localScale;
        capturedStandPosition = actor.transform.position;
    }

    private async UniTask PlayEnterTweenAsync()
    {
        Transform actorTransform = actor.transform;
        Vector3 targetPosition = enterTarget.position + enterDoorOffset;
        float duration = Mathf.Max(0.01f, enterDurationSeconds);

        // 与 ActorDestroyAnimHandle.PlayDestroy 完全同构的移动/旋转/缩小/淡出，但终点不销毁 Actor。
        float alpha = currentActorAlpha;
        ActorSpineCtrl actorSpine = actor.SpineCtrl;

        enterSequence?.Kill();
        enterSequence = DOTween.Sequence();
        enterSequence.Append(actorTransform.DOMove(targetPosition, duration));
        enterSequence.Join(actorTransform.DOScale(Vector3.zero, duration));

        Vector3 angle = actorTransform.localEulerAngles;
        angle.z = angle.y != 0f ? -720f : 720f;
        enterSequence.Join(actorTransform.DORotate(angle, duration, RotateMode.FastBeyond360));
        enterSequence.Join(DOTween.To(() => alpha, x =>
        {
            alpha = x;
            currentActorAlpha = x;
            if (actorSpine != null)
                actorSpine.SetAlpha(x);
        }, 0f, duration));

        // 挂起时 Tween 被 Pause，完成等待自然顺延；判负/取消时 Kill 使等待立即返回。
        await enterSequence.AsyncWaitForCompletion();
    }

    private void SuspendHiddenActorPhysics()
    {
        Rigidbody2D body = actor != null ? actor.RB : null;
        if (body == null || actorPhysicsCaptured)
            return;

        actorPhysicsCaptured = true;
        actorPhysicsWasSimulated = body.simulated;
        body.linearVelocity = Vector2.zero;
        body.simulated = false;
    }

    private void RestoreHiddenActorPhysics()
    {
        if (!actorPhysicsCaptured)
            return;

        actorPhysicsCaptured = false;
        Rigidbody2D body = actor != null ? actor.RB : null;
        if (body == null)
            return;

        body.simulated = actorPhysicsWasSimulated;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
    }

    private void RestoreActorPresentationForFailure()
    {
        RestoreHiddenActorPhysics();

        actor.transform.localScale = capturedActorScale;
        Vector3 euler = actor.transform.localEulerAngles;
        actor.transform.localEulerAngles = new Vector3(0f, euler.y, 0f);
        SetActorAlpha(1f);
    }

    #endregion

    #region Exit Flow

    private void PrepareActorAtDoorForExit()
    {
        if (!hasActorBaseline)
            CaptureActorBaseline();

        RestoreHiddenActorPhysics();
        actor.StopMoving();

        // 出门方向服从门体当前朝向：门正面（Actor 进门一侧）朝向即走出方向，不翻转门体。
        exitDirectionSign = transform.right.x >= 0f ? -1 : 1;

        actor.transform.position = new Vector3(
            transform.position.x,
            capturedStandPosition.y,
            capturedStandPosition.z);
        actor.transform.localScale = capturedActorScale;
        actor.transform.localEulerAngles = Vector3.zero;
        SetActorAlpha(0f);
    }

    private void ApplyExitWalkMotion()
    {
        if (actor == null || actor.IsDied)
            return;

        actor.ResumeMoving();
        actor.SetMoveInput(exitDirectionSign);
        SafeUpdateActorAnimation();
    }

    private void StopActorAtDoorSide()
    {
        if (actor == null || actor.IsDied)
            return;

        actor.SetMoveInput(0f);
        SafeUpdateActorAnimation();
        actor.StopMoving();
    }

    private async UniTask FadeInActor(CancellationToken cancellationToken)
    {
        float duration = Mathf.Max(0f, exitFadeInSeconds);
        if (actor == null || actor.SpineCtrl == null || duration <= 0f)
        {
            SetActorAlpha(1f);
            isActorHidden = false;
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
        finally
        {
            isActorHidden = false;
        }
    }

    #endregion

    #region Door Presentation

    private void PlayDoorOpen()
    {
        if (doorSpine == null || doorSpine.IsPlayingAnimation(DoorOpenAnimName))
            return;

        AudioManager.PlaySound("openDoor");
        doorSpine.PlayAnimation(DoorOpenAnimName, false);
    }

    private void PlayDoorCloseAnimation()
    {
        if (doorSpine != null)
            doorSpine.PlayAnimation(DoorCloseAnimName, false);
    }

    private async UniTaskVoid PlayParallelDoorCloseSfxAsync()
    {
        try
        {
            await WaitForActiveSeconds(
                closeDoorSfxDelaySeconds,
                this.GetCancellationTokenOnDestroy());
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (cancelledForTeardown || !isActiveAndEnabled)
            return;

        AudioManager.PlaySound("closeDoor");
    }

    private static void PlaySuccessVibration()
    {
        if (DB.GameSetting.VibrationOn)
            HapticPatterns.PlayPreset(HapticPatterns.PresetType.Success);
    }

    private void SetDoorVisualsSuspended(bool suspended)
    {
        if (doorVisualsSuspended == suspended)
            return;

        doorVisualsSuspended = suspended;

        if (doorSpine != null && doorSpine.AnimationState != null)
        {
            if (suspended)
            {
                doorSpineTimeScale = doorSpine.AnimationState.TimeScale;
                doorSpine.AnimationState.TimeScale = 0f;
            }
            else
            {
                doorSpine.AnimationState.TimeScale = doorSpineTimeScale;
            }
        }

        if (ribbonSpine != null && ribbonSpine.AnimationState != null)
        {
            if (suspended)
            {
                ribbonSpineTimeScale = ribbonSpine.AnimationState.TimeScale;
                ribbonSpine.AnimationState.TimeScale = 0f;
            }
            else
            {
                ribbonSpine.AnimationState.TimeScale = ribbonSpineTimeScale;
            }
        }
    }

    private void SetActorAlpha(float alpha)
    {
        currentActorAlpha = Mathf.Clamp01(alpha);
        if (actor != null && actor.SpineCtrl != null)
            actor.SpineCtrl.SetAlpha(currentActorAlpha);

        if (shadow == null && actor != null)
            shadow = actor.transform.Find("Shadow")?.GetComponent<SpriteRenderer>();

        if (shadow != null)
            shadow.gameObject.SetActive(currentActorAlpha >= 0.5f);

        if (groupAlpha != null)
            groupAlpha.SetAlpha(currentActorAlpha);
    }

    #endregion

    #region Flow Guards

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

    private void SafeUpdateActorAnimation()
    {
        if (actor != null && actor.SpineCtrl != null)
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
