using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Spine;
using UnityEngine;

/// <summary>
/// Level091 黑箱敌人的专用 Actor 控制器。
/// 只管理 box_bad 皮肤、出门渐显、angry 和冲撞演出，不参与共享玩家输入。
/// </summary>
public sealed class Level91ActorCtrl : ActorCtrl
{
    #region Constants

    private const string BoxBadSkinName = "box_bad";
    private const string WalkAnimationName = "walk";
    private const string IdleAnimationName = "idle";
    private const string AngryAnimationName = "angry";
    private const string ChargeAnimationName = "walk01";
    private const float RunOffFallbackDistance = 16f;

    #endregion

    #region Inspector

    /// <summary>
    /// 黑箱命中主角后继续冲出相机视口的额外横向边距。
    /// 该值按视口宽度比例计算，仅影响 Level091 的退场距离。
    /// </summary>
    [Tooltip("命中主角后继续冲出相机视口的额外横向比例；0.15 表示额外跑出 15% 屏宽。")]
    [Min(0f)]
    [SerializeField] private float runOffViewportPadding = 0.15f;

    #endregion

    #region Sequence Configuration

    internal readonly struct SequenceConfig
    {
        internal readonly float WalkOutDistance;
        internal readonly float RevealDuration;
        internal readonly float AngryDelay;
        internal readonly float ChargeSpeed;
        internal readonly float MinimumChargeDuration;
        internal readonly float MaximumChargeDuration;

        internal SequenceConfig(
            float walkOutDistance,
            float revealDuration,
            float angryDelay,
            float chargeSpeed,
            float minimumChargeDuration,
            float maximumChargeDuration)
        {
            WalkOutDistance = walkOutDistance;
            RevealDuration = revealDuration;
            AngryDelay = angryDelay;
            ChargeSpeed = chargeSpeed;
            MinimumChargeDuration = minimumChargeDuration;
            MaximumChargeDuration = maximumChargeDuration;
        }
    }

    #endregion

    #region Runtime State

    private ActorCtrl playerActor;
    private SequenceConfig config;
    private Tween activeTween;
    private CancellationTokenSource sequenceCancellation;
    private bool initialized;
    private bool hasInitialPose;
    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;

    #endregion

    #region ActorCtrl Overrides

    public override void OnUpdate()
    {
        // 黑箱全程由本关演出控制，不读取玩家输入或通用移动状态。
    }

    protected override void HandleAnimators()
    {
        // 动画顺序由 PlaySequenceAsync 独占，禁止通用 Actor 状态覆盖。
    }

    #endregion

    #region Level091 API

    /// <summary>
    /// 缓存场景预放位置并保持黑箱隐藏。
    /// 仅允许 Level91Ctrl 在关卡 Awake 时调用，供重开或退出时恢复。
    /// </summary>
    internal void PrepareHidden()
    {
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
        hasInitialPose = true;
        initialized = false;
        SetSpineAlpha(0f);
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 初始化本关场景预放黑箱的演出引用。
    /// 仅允许 Level91Ctrl 在开门等待结束并激活该对象后调用一次。
    /// </summary>
    /// <param name="player">本关主角，用于确定朝向和冲撞终点。</param>
    /// <param name="sequenceConfig">本次出门与冲撞参数。</param>
    /// <returns>关键引用与 box_bad 皮肤均有效时返回 true。</returns>
    internal bool Initialize(ActorCtrl player, SequenceConfig sequenceConfig)
    {
        playerActor = player;
        config = sequenceConfig;

        if (playerActor == null || SpineCtrl == null)
            return false;

        if (!hasInitialPose)
        {
            initialLocalPosition = transform.localPosition;
            initialLocalRotation = transform.localRotation;
            hasInitialPose = true;
        }

        DisablePhysics();

        SetSpineAlpha(0f);
        initialized = SpineCtrl.SetSkin(BoxBadSkinName);
        return initialized;
    }

    /// <summary>
    /// 按 walk 渐显走出、idle 停顿、angry、walk01 冲撞的固定顺序播放演出。
    /// 仅允许 Level91Ctrl 在玩家和 HUD 已锁定后调用。
    /// </summary>
    /// <param name="onChargeStarted">开始冲撞时回调。</param>
    /// <param name="onImpact">到达主角位置时回调。</param>
    /// <param name="cancellationToken">关卡退出或重开时取消演出。</param>
    internal async UniTask PlaySequenceAsync(
        Action onChargeStarted,
        Action<Transform> onImpact,
        CancellationToken cancellationToken)
    {
        if (!initialized || playerActor == null || !isActiveAndEnabled)
            return;

        CancelSequence();

        CancellationTokenSource currentSequenceCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                this.GetCancellationTokenOnDestroy());
        sequenceCancellation = currentSequenceCancellation;
        CancellationToken sequenceToken = currentSequenceCancellation.Token;

        try
        {
            sequenceToken.ThrowIfCancellationRequested();

            float direction = Mathf.Sign(playerActor.transform.position.x - transform.position.x);
            if (Mathf.Approximately(direction, 0f))
                direction = -1f;

            Vector3 rotation = transform.localEulerAngles;
            rotation.y = direction < 0f ? 180f : 0f;
            transform.localEulerAngles = rotation;

            SpineCtrl.PlayAnimation(WalkAnimationName, true, false);
            await PlayRevealAndWalkOutAsync(direction, sequenceToken);

            sequenceToken.ThrowIfCancellationRequested();
            SpineCtrl.PlayAnimation(IdleAnimationName, true, false);
            await UniTask.WaitForSeconds(
                Mathf.Max(0f, config.AngryDelay),
                cancellationToken: sequenceToken);

            sequenceToken.ThrowIfCancellationRequested();
            TrackEntry angryEntry = SpineCtrl.PlayAnimation(AngryAnimationName, false, false);
            await WaitForAnimationCompleteAsync(angryEntry, sequenceToken);

            sequenceToken.ThrowIfCancellationRequested();
            onChargeStarted?.Invoke();

            sequenceToken.ThrowIfCancellationRequested();
            SpineCtrl.PlayAnimation(ChargeAnimationName, true, false);
            await PlayChargeAsync(sequenceToken);

            sequenceToken.ThrowIfCancellationRequested();
            onImpact?.Invoke(transform);

            sequenceToken.ThrowIfCancellationRequested();
            await PlayRunOffScreenAsync(direction, sequenceToken);
        }
        finally
        {
            if (ReferenceEquals(sequenceCancellation, currentSequenceCancellation))
                sequenceCancellation = null;

            currentSequenceCancellation.Dispose();
        }
    }

    /// <summary>
    /// 取消当前 DOTween 演出。
    /// 仅用于关卡重开、退出或敌人对象被禁用时的清理。
    /// </summary>
    internal void CancelSequence()
    {
        CancellationTokenSource currentSequenceCancellation = sequenceCancellation;
        if (currentSequenceCancellation != null &&
            !currentSequenceCancellation.IsCancellationRequested)
        {
            currentSequenceCancellation.Cancel();
        }

        if (activeTween != null && activeTween.IsActive())
            activeTween.Kill();

        activeTween = null;
    }

    /// <summary>
    /// 取消演出、恢复场景预放姿态并重新隐藏。
    /// 仅供 Level91Ctrl 在关卡退出或重开清理时调用。
    /// </summary>
    internal void ResetPresentation()
    {
        CancelSequence();

        if (hasInitialPose)
        {
            transform.localPosition = initialLocalPosition;
            transform.localRotation = initialLocalRotation;
        }

        playerActor = null;
        initialized = false;
        SetSpineAlpha(0f);
        gameObject.SetActive(false);
    }

    #endregion

    #region Sequence Steps

    private async UniTask PlayRevealAndWalkOutAsync(
        float direction,
        CancellationToken cancellationToken)
    {
        float duration = Mathf.Max(0.05f, config.RevealDuration);
        float alpha = 0f;
        float targetX = transform.position.x + direction * Mathf.Max(0f, config.WalkOutDistance);

        DG.Tweening.Sequence sequence = DOTween.Sequence()
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        sequence.Join(transform.DOMoveX(targetX, duration).SetEase(Ease.Linear));
        sequence.Join(
            DOTween.To(
                    () => alpha,
                    value =>
                    {
                        alpha = value;
                        SetSpineAlpha(value);
                    },
                    1f,
                    duration)
                .SetEase(Ease.OutQuad));

        await AwaitTweenAsync(sequence, cancellationToken);
    }

    private async UniTask PlayChargeAsync(CancellationToken cancellationToken)
    {
        float targetX = playerActor.transform.position.x;
        float distance = Mathf.Abs(targetX - transform.position.x);
        float duration = distance / Mathf.Max(0.1f, config.ChargeSpeed);
        duration = Mathf.Clamp(
            duration,
            Mathf.Max(0.05f, config.MinimumChargeDuration),
            Mathf.Max(config.MinimumChargeDuration, config.MaximumChargeDuration));

        Tween chargeTween = transform
            .DOMoveX(targetX, duration)
            .SetEase(Ease.InQuad)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        await AwaitTweenAsync(chargeTween, cancellationToken);
    }

    private async UniTask PlayRunOffScreenAsync(
        float direction,
        CancellationToken cancellationToken)
    {
        float targetX = ResolveRunOffTargetX(direction);
        float distance = Mathf.Abs(targetX - transform.position.x);
        if (distance <= 0.01f)
            return;

        float duration = distance / Mathf.Max(0.1f, config.ChargeSpeed);
        Tween runOffTween = transform
            .DOMoveX(targetX, Mathf.Max(0.05f, duration))
            .SetEase(Ease.Linear)
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy);
        await AwaitTweenAsync(runOffTween, cancellationToken);
    }

    private float ResolveRunOffTargetX(float direction)
    {
        float normalizedDirection = direction < 0f ? -1f : 1f;
        Camera camera = Camera.main;
        if (camera != null)
        {
            Vector3 viewportPosition = camera.WorldToViewportPoint(transform.position);
            viewportPosition.x = normalizedDirection < 0f
                ? -Mathf.Max(0f, runOffViewportPadding)
                : 1f + Mathf.Max(0f, runOffViewportPadding);

            float targetX = camera.ViewportToWorldPoint(viewportPosition).x;
            if ((targetX - transform.position.x) * normalizedDirection > 0.01f)
                return targetX;
        }

        return transform.position.x + normalizedDirection * RunOffFallbackDistance;
    }

    #endregion

    #region Await Helpers

    private static async UniTask WaitForAnimationCompleteAsync(
        TrackEntry entry,
        CancellationToken cancellationToken)
    {
        if (entry == null)
        {
            await UniTask.WaitForSeconds(0.35f, cancellationToken: cancellationToken);
            return;
        }

        var completionSource = new UniTaskCompletionSource();
        void OnComplete(TrackEntry _) => completionSource.TrySetResult();

        entry.Complete += OnComplete;
        try
        {
            await completionSource.Task.AttachExternalCancellation(cancellationToken);
        }
        finally
        {
            entry.Complete -= OnComplete;
        }
    }

    private async UniTask AwaitTweenAsync(
        Tween tween,
        CancellationToken cancellationToken)
    {
        activeTween = tween;
        var completionSource = new UniTaskCompletionSource();
        bool completed = false;
        tween.OnComplete(() =>
        {
            completed = true;
            completionSource.TrySetResult();
        });
        tween.OnKill(() =>
        {
            if (!completed)
                completionSource.TrySetCanceled(cancellationToken);
        });

        try
        {
            await completionSource.Task.AttachExternalCancellation(cancellationToken);
        }
        finally
        {
            if (cancellationToken.IsCancellationRequested && tween.IsActive())
                tween.Kill();

            if (ReferenceEquals(activeTween, tween))
                activeTween = null;
        }
    }

    #endregion

    #region Presentation Isolation

    private void DisablePhysics()
    {
        Rigidbody2D rigidbody2D = GetComponent<Rigidbody2D>();
        if (rigidbody2D != null)
        {
            rigidbody2D.linearVelocity = Vector2.zero;
            rigidbody2D.simulated = false;
        }

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;
    }

    #endregion

    #region Lifecycle

    private void OnDisable()
    {
        CancelSequence();
    }

    #endregion
}
