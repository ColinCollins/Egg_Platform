using System;
using System.Threading;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Scripts.Common;
using Game.Scripts.Common.VFX;
using UnityEngine;

/// <summary>
/// IWanner 传送门：指定 Tag 的 Collider2D 进入当前 Trigger 后，锁定 Actor 并播放传送表现；
/// 目标为 Actor_IWanner 时，入口正向播放 distortion 后隐藏，抵达终点时直接恢复 Scale 并播放 distortion_reverse。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class IWannerGame_Portal : MonoBehaviour, IDebuger
{
    #region Serialized Fields

    /// <summary>
    /// 用途：限制允许触发当前传送门的 Collider2D Tag。
    /// 调用边界：OnTriggerEnter2D 只在进入者 Tag 与该值一致时启动传送。
    /// 注意事项：Tag 仅负责触发检测；进入者可以与实际被传送的 target 不是同一对象。
    /// </summary>
    [Header("Trigger Settings")]
    [Tooltip("允许触发传送门的 Collider2D Tag")]
    [SerializeField] private string triggerTag = "Player";

    /// <summary>
    /// 用途：控制当前 Portal 是否只允许完成一次传送。
    /// 调用边界：首次成功传送并恢复 Actor 移动后，将 Portal 在 0.2 秒内缩放到 0，并阻止后续触发。
    /// 注意事项：默认关闭；传送被取消、中断或失败时不会消耗本次机会。
    /// </summary>
    [Tooltip("成功传送一次后，以 InBack 缩放到 0 并停止响应后续触发")]
    [SerializeField] private bool isOnce;

    /// <summary>
    /// 用途：指定 Tag 检测通过后需要被传送并临时停止移动的 Actor。
    /// 调用边界：由匹配 triggerTag 的 Collider2D 触发后调用 StopMoving，抵达 endPoint 且完成出口表现后调用 ResumeMoving。
    /// 注意事项：Actor_IWanner 的入口 distortion 完成后才隐藏，抵达终点后直接恢复 Scale 并播放 distortion_reverse；取消、销毁或动作中断不会提前恢复移动。
    /// </summary>
    [Header("Teleport Settings")]
    [Tooltip("进入当前传送门后需要被传送的 Actor")]
    [SerializeField] private ActorCtrl target;

    /// <summary>
    /// 用途：指定目标传送后的世界坐标。
    /// 调用边界：完成动画占位等待后读取一次 endPoint.position。
    /// 注意事项：建议把终点放在其它传送门 Collider2D 外，避免落点立即触发反向传送。
    /// </summary>
    [Tooltip("传送目标的终点 Transform")]
    [SerializeField] private Transform endPoint;

    /// <summary>
    /// 用途：在入口 distortion 完成后，从 Actor 世界坐标发射并吸附到当前 endPoint 的 WorldCoinAttraction。
    /// 调用边界：仅在当前 Portal 显式绑定、Actor 与 endPoint 均有效时触发；未绑定时不改变原传送流程。
    /// 注意事项：多个 Portal 可以共用同一实例，重复触发会由 WorldSpriteParticleAttractor 清空上一轮后重新播放。
    /// </summary>
    [Tooltip("入口 distortion 完成后，从 Actor 位置吸附到当前 endPoint 的 WorldCoinAttraction")]
    [SerializeField] private WorldSpriteParticleAttractor worldCoinAttraction;

    /// <summary>
    /// 用途：让 WorldCoinAttraction 在入口 distortion 完成前提前触发。
    /// 调用边界：仅用于已绑定特效的 IWanner 入口 distortion，运行时按非负值处理。
    /// 注意事项：默认提前 0.3 秒；大于动画剩余时长时会在 distortion 启动后立即触发。
    /// </summary>
    [Tooltip("WorldCoinAttraction 相对入口 distortion 完成点的提前时间（秒）")]
    [Min(0f)]
    [SerializeField] private float worldCoinAttractionLeadTime = 0.3f;

    /// <summary>
    /// 用途：配置 Actor 的缩放表现时长。
    /// 调用边界：仅供非 Actor_IWanner 目标在入口消失时使用，运行时按非负值处理。
    /// 注意事项：单位为秒；所有目标抵达终点后都会直接把 Scale 设为 1，不再播放出现缩放。
    /// </summary>
    [Tooltip("非 IWanner Actor 的入口消失缩放时长（秒）")]
    [Min(0f)]
    [SerializeField] private float scaleDuration = 0.25f;

    /// <summary>
    /// 用途：配置 Actor 隐藏后、移动到 endPoint 前的等待时间。
    /// 调用边界：Actor_IWanner 的入口 distortion 完成并把 Scale 设为 0 后开始等待；其它 Actor 的消失缩放完成后开始等待。
    /// 注意事项：单位为秒，默认 1 秒，使用游戏时间。
    /// </summary>
    [Tooltip("Actor 隐藏后、传送到 endPoint 前的等待时间（秒）")]
    [Min(0f)]
    [SerializeField] private float teleportDelay = 1f;

    #endregion

    #region Public API

    /// <summary>
    /// 用途：在运行时切换当前 Portal 使用的传送终点。
    /// 调用边界：可在触发前调用；若在 Actor 尚未搬运到终点时调用，新终点会用于本次传送。
    /// 注意事项：传入 null 时不会覆盖现有终点，并通过 Bear Logger 记录警告。
    /// </summary>
    /// <param name="newEndPoint">新的传送终点 Transform。</param>
    public void SetEndPoint(Transform newEndPoint)
    {
        if (newEndPoint == null)
        {
            this.LogWarning(
                "[IWannerGame_Portal] New endPoint is null; current endPoint remains unchanged.");
            return;
        }

        endPoint = newEndPoint;
    }

    /// <summary>
    /// 用途：预约在下一次成功传送完成并等待指定时间后切换终点。
    /// 调用边界：可在当前传送触发前或进行中调用；只有 Actor 完成出口表现并恢复移动后才开始延迟。
    /// 注意事项：重复调用会覆盖上一笔预约；传送取消时不会提前切换，预约会保留到下一次成功传送。
    /// </summary>
    /// <param name="newEndPoint">成功传送后需要切换到的新终点。</param>
    /// <param name="delaySeconds">传送完成后的延迟秒数，运行时按非负值处理。</param>
    public void SetEndPointAfterTeleport(Transform newEndPoint, float delaySeconds)
    {
        if (!Application.isPlaying)
        {
            this.LogWarning(
                "[IWannerGame_Portal] Delayed endPoint switching is only available in PlayMode.");
            return;
        }

        if (newEndPoint == null)
        {
            this.LogWarning(
                "[IWannerGame_Portal] Delayed endPoint is null; pending endPoint remains unchanged.");
            return;
        }

        _pendingEndPoint = newEndPoint;
        _pendingEndPointDelay = Mathf.Max(0f, delaySeconds);
    }

    #endregion

    #region Runtime State

    private CancellationTokenSource _teleportCancellation;
    private Tweener _scaleTweener;
    private Tweener _onceScaleTweener;
    private Transform _pendingEndPoint;
    private float _pendingEndPointDelay;
    private bool _isTeleporting;
    private bool _hasTeleported;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        BoxCollider2D triggerCollider = GetComponent<BoxCollider2D>();
        if (triggerCollider == null)
        {
            this.LogWarning(
                "[IWannerGame_Portal] Collider2D is missing; portal cannot receive trigger events.");
        }
        else if (!triggerCollider.isTrigger)
        {
            this.LogWarning(
                "[IWannerGame_Portal] Collider2D is not marked as Trigger; portal will not run as expected.");
        }

        if (string.IsNullOrWhiteSpace(triggerTag))
        {
            this.LogWarning(
                "[IWannerGame_Portal] Trigger Tag is empty; teleport is disabled.");
        }

        if (target == null || endPoint == null)
        {
            this.LogWarning(
                "[IWannerGame_Portal] Target or endPoint is missing; teleport is disabled.");
        }
    }

    private void OnDisable()
    {
        CancelTeleport();
    }

    private void OnDestroy()
    {
        CancelTeleport();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!Application.isPlaying
            || _isTeleporting
            || (isOnce && _hasTeleported)
            || string.IsNullOrWhiteSpace(triggerTag)
            || target == null
            || endPoint == null)
            return;

        if (other == null || !other.CompareTag(triggerTag))
            return;

        StartTeleport();
    }

    #endregion

    #region Teleport Flow

    private void StartTeleport()
    {
        CancelTeleport();

        ActorCtrl teleportTarget = target;
        Transform targetTransform = teleportTarget.transform;
        Rigidbody2D targetBody = teleportTarget.RB != null
            ? teleportTarget.RB
            : teleportTarget.GetComponent<Rigidbody2D>();

        teleportTarget.SetMoveInput(0f);
        teleportTarget.StopMoving();
        if (targetBody != null)
        {
            targetBody.linearVelocity = Vector2.zero;
            targetBody.simulated = false;
        }
        else
        {
            this.LogWarning(
                "[IWannerGame_Portal] Target Rigidbody2D is missing; falling cannot be locked during teleport.");
        }

        _isTeleporting = true;
        _teleportCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());
        TeleportTargetAsync(
                _teleportCancellation,
                teleportTarget,
                targetTransform,
                targetBody)
            .Forget();
    }

    private async UniTaskVoid TeleportTargetAsync(
        CancellationTokenSource cancellation,
        ActorCtrl teleportTarget,
        Transform targetTransform,
        Rigidbody2D targetBody)
    {
        try
        {
            Actor_IWanner iwannerTarget = teleportTarget as Actor_IWanner;
            bool hasHidden;
            if (iwannerTarget != null)
            {
                bool hasPlayedWorldCoinAttraction = false;
                void PlayWorldCoinAttractionOnce()
                {
                    if (hasPlayedWorldCoinAttraction)
                        return;

                    hasPlayedWorldCoinAttraction = true;
                    PlayWorldCoinAttraction(targetTransform);
                }

                AudioManager.PlaySound("iwannar_portalIn");
                bool entryDistortionCompleted = await PlayIwannerDistortionAsync(
                    iwannerTarget,
                    useReverseAnimation: false,
                    cancellationToken: cancellation.Token,
                    preCompleteLeadTime: worldCoinAttractionLeadTime,
                    preCompleteAction: PlayWorldCoinAttractionOnce);


                if (!entryDistortionCompleted
                    && cancellation == _teleportCancellation
                    && !iwannerTarget.IsDied)
                {
                    this.LogWarning(
                        "[IWannerGame_Portal] Entry distortion was interrupted; target remains visible while movement and Rigidbody2D remain locked.");
                }

                if (!entryDistortionCompleted
                    || cancellation != _teleportCancellation
                    || targetTransform == null)
                {
                    return;
                }

                PlayWorldCoinAttractionOnce();
                targetTransform.localScale = Vector3.zero;
                hasHidden = true;
            }
            else
            {
                hasHidden = await PlayScaleAsync(
                    targetTransform,
                    Vector3.zero,
                    Ease.Linear,
                    cancellation.Token);
            }

            if (!hasHidden || cancellation != _teleportCancellation)
                return;

            float effectiveDelay = Mathf.Max(0f, teleportDelay);
            if (effectiveDelay > 0f)
            {
                bool isCanceled = await UniTask.Delay(
                        Mathf.RoundToInt(effectiveDelay * 1000f),
                        cancellationToken: cancellation.Token)
                    .SuppressCancellationThrow();
                if (isCanceled || cancellation != _teleportCancellation)
                    return;
            }

            if (teleportTarget == null || targetTransform == null || endPoint == null)
            {
                this.LogWarning(
                    "[IWannerGame_Portal] Target or endPoint became unavailable before teleport completed.");
                return;
            }

            targetTransform.position = endPoint.position;
            UniTask<bool> distortionTask = PlayIwannerDistortionAsync(
                iwannerTarget,
                useReverseAnimation: true,
                cancellationToken: cancellation.Token);


            AudioManager.PlaySound("iwannar_portalOut");

            targetTransform.localScale = Vector3.one;
            bool distortionCompleted = await distortionTask;
            if (!distortionCompleted
                && cancellation == _teleportCancellation
                && iwannerTarget != null
                && !iwannerTarget.IsDied)
            {
                this.LogWarning(
                    "[IWannerGame_Portal] Exit distortion was interrupted; movement and Rigidbody2D remain locked.");
            }

            if (!distortionCompleted
                || cancellation != _teleportCancellation
                || teleportTarget == null)
            {
                return;
            }

            if (targetBody != null)
            {
                targetBody.linearVelocity = Vector2.zero;
                targetBody.simulated = true;
            }

            // Actor 完整抵达并播放完出口 distortion 后，才恢复移动与 Rigidbody2D 下落。
            teleportTarget.ResumeMoving();

            PlayOnceDisappear();

            await ApplyPendingEndPointAsync(cancellation);
        }
        finally
        {
            CompleteTeleport(cancellation);
        }
    }

    private async UniTask<bool> PlayIwannerDistortionAsync(
        Actor_IWanner iwannerTarget,
        bool useReverseAnimation,
        CancellationToken cancellationToken,
        float preCompleteLeadTime = 0f,
        Action preCompleteAction = null)
    {
        if (iwannerTarget == null)
            return true;

        Spine.TrackEntry trackEntry = iwannerTarget.PlayDistortionTracked(useReverseAnimation);
        if (trackEntry == null)
        {
            if (iwannerTarget != null && !iwannerTarget.IsDied)
            {
                this.LogWarning(
                    "[IWannerGame_Portal] IWanner distortion could not start; teleport continues without waiting for it.");
                return true;
            }

            return false;
        }

        var completionSource = new UniTaskCompletionSource();
        bool isCompleted = false;

        using CancellationTokenSource linkedCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                iwannerTarget.GetCancellationTokenOnDestroy());
        using CancellationTokenSource preCompleteCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(linkedCancellation.Token);

        void HandleComplete(Spine.TrackEntry _)
        {
            isCompleted = true;
            preCompleteCancellation.Cancel();
            completionSource.TrySetResult();
        }

        void HandleInterrupt(Spine.TrackEntry _)
        {
            preCompleteCancellation.Cancel();
            completionSource.TrySetResult();
        }

        trackEntry.Complete += HandleComplete;
        trackEntry.Interrupt += HandleInterrupt;
        UniTask preCompleteTask = PlayPreCompleteActionAsync(
            trackEntry,
            preCompleteLeadTime,
            preCompleteAction,
            preCompleteCancellation.Token);
        try
        {
            bool isCanceled = await completionSource.Task
                .AttachExternalCancellation(linkedCancellation.Token)
                .SuppressCancellationThrow();
            preCompleteCancellation.Cancel();
            await preCompleteTask;
            return !isCanceled && isCompleted;
        }
        finally
        {
            preCompleteCancellation.Cancel();
            trackEntry.Complete -= HandleComplete;
            trackEntry.Interrupt -= HandleInterrupt;
        }
    }

    private async UniTask PlayPreCompleteActionAsync(
        Spine.TrackEntry trackEntry,
        float leadTime,
        Action preCompleteAction,
        CancellationToken cancellationToken)
    {
        if (trackEntry == null || preCompleteAction == null)
            return;

        float remainingTrackTime = Mathf.Max(
            0f,
            trackEntry.TrackComplete - trackEntry.TrackTime);
        float trackTimeScale = Mathf.Abs(trackEntry.TimeScale);
        float remainingSeconds = trackTimeScale > Mathf.Epsilon
            ? remainingTrackTime / trackTimeScale
            : 0f;
        float delaySeconds = Mathf.Max(
            0f,
            remainingSeconds - Mathf.Max(0f, leadTime));
        if (delaySeconds > 0f)
        {
            bool isCanceled = await UniTask.Delay(
                    Mathf.RoundToInt(delaySeconds * 1000f),
                    cancellationToken: cancellationToken)
                .SuppressCancellationThrow();
            if (isCanceled)
                return;
        }

        if (!cancellationToken.IsCancellationRequested)
            preCompleteAction.Invoke();
    }

    private void PlayWorldCoinAttraction(Transform actorTransform)
    {
        if (worldCoinAttraction == null)
            return;

        if (actorTransform == null || endPoint == null)
        {
            this.LogWarning(
                "[IWannerGame_Portal] Actor or endPoint is unavailable; WorldCoinAttraction was not played.");
            return;
        }

        GameObject attractionObject = worldCoinAttraction.gameObject;
        if (!attractionObject.activeSelf)
            attractionObject.SetActive(true);

        if (!worldCoinAttraction.isActiveAndEnabled)
        {
            this.LogWarning(
                "[IWannerGame_Portal] WorldCoinAttraction is not active in hierarchy; effect was not played.");
            return;
        }

        worldCoinAttraction.transform.position = actorTransform.position;
        worldCoinAttraction.Play(endPoint);
    }

    private async UniTask<bool> PlayScaleAsync(
        Transform targetTransform,
        Vector3 endScale,
        Ease ease,
        CancellationToken cancellationToken)
    {
        if (targetTransform == null || cancellationToken.IsCancellationRequested)
            return false;

        float effectiveDuration = Mathf.Max(0f, scaleDuration);
        if (effectiveDuration <= 0f)
        {
            targetTransform.localScale = endScale;
            return true;
        }

        bool isCompleted = false;
        Tweener scaleTweener = targetTransform
            .DOScale(endScale, effectiveDuration)
            .SetEase(ease)
            .OnComplete(() => isCompleted = true);
        _scaleTweener = scaleTweener;

        await scaleTweener.AsyncWaitForCompletion();

        if (_scaleTweener == scaleTweener)
            _scaleTweener = null;

        return isCompleted
               && !cancellationToken.IsCancellationRequested
               && targetTransform != null;
    }

    private async UniTask ApplyPendingEndPointAsync(CancellationTokenSource cancellation)
    {
        if (_pendingEndPoint == null || cancellation != _teleportCancellation)
            return;

        float effectiveDelay = Mathf.Max(0f, _pendingEndPointDelay);
        if (effectiveDelay > 0f)
        {
            bool isCanceled = await UniTask.Delay(
                    Mathf.RoundToInt(effectiveDelay * 1000f),
                    cancellationToken: cancellation.Token)
                .SuppressCancellationThrow();
            if (isCanceled || cancellation != _teleportCancellation)
                return;
        }

        if (_pendingEndPoint == null)
        {
            _pendingEndPointDelay = 0f;
            this.LogWarning(
                "[IWannerGame_Portal] Pending endPoint was lost before it could be applied.");
            return;
        }

        endPoint = _pendingEndPoint;
        _pendingEndPoint = null;
        _pendingEndPointDelay = 0f;
    }

    private void PlayOnceDisappear()
    {
        if (!isOnce || _hasTeleported)
            return;

        _hasTeleported = true;
        KillOnceScaleTween();
        Tweener onceScaleTweener = null;
        onceScaleTweener = transform
            .DOScale(Vector3.zero, 0.2f)
            .SetEase(Ease.InBack)
            .OnComplete(() =>
            {
                if (_onceScaleTweener == onceScaleTweener)
                    _onceScaleTweener = null;
            });
        _onceScaleTweener = onceScaleTweener;
    }

    private void CompleteTeleport(CancellationTokenSource cancellation)
    {
        if (cancellation != _teleportCancellation)
        {
            cancellation.Dispose();
            return;
        }

        _teleportCancellation = null;
        _isTeleporting = false;
        KillScaleTween();
        cancellation.Dispose();
    }

    private void CancelTeleport()
    {
        CancellationTokenSource cancellation = _teleportCancellation;
        _teleportCancellation = null;
        _isTeleporting = false;

        if (cancellation == null)
        {
            KillScaleTween();
            KillOnceScaleTween();
            return;
        }

        cancellation.Cancel();
        KillScaleTween();
        KillOnceScaleTween();
        cancellation.Dispose();
    }

    private void KillScaleTween()
    {
        if (_scaleTweener != null && _scaleTweener.IsActive())
            _scaleTweener.Kill(false);
        _scaleTweener = null;
    }

    private void KillOnceScaleTween()
    {
        if (_onceScaleTweener != null && _onceScaleTweener.IsActive())
            _onceScaleTweener.Kill(false);
        _onceScaleTweener = null;
    }

    #endregion
}
