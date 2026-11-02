using System.Collections.Generic;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// DLevel53 蜂群生存玩法的共享关卡结算控制器。
/// 蜂巢负责生成与管理蜂群；本组件只协调登记玩家命中、受击特效、失败演出和标准结算。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 53 Ctrl")]
public sealed class DLevel53Ctrl : MonoBehaviour,
    IDrawStrokeCompletedHandler,
    IDebuger
{
    private const string SwellAnimationName = "swell";
    private const string SwellIdleAnimationName = "swell_idle";

    #region Inspector References

    /// <summary>
    /// 当前 Draw 关卡根节点上的通用 Draw 关卡控制器。
    /// 仅用于读取暂停/结算状态，以及提交本关最终失败结果。
    /// </summary>
    [Header("运行时引用")]
    [Tooltip("当前 Draw 关卡根节点上的 DrawLevelCtrl。")]
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;

    /// <summary>
    /// 本关受蜂群追踪和命中判定的首个玩家角色。
    /// 双角色关卡的其它目标由 additionalActors 补充；蜜蜂不会把未登记的 Actor Layer 物体当作玩家。
    /// </summary>
    [Tooltip("本关需要保护的玩家 Actor。")]
    [SerializeField] private ActorCtrl actor;

    /// <summary>
    /// 本关额外需要保护的玩家角色；为空时保持原有单角色行为。
    /// 调用边界：只绑定同一 DrawLevel 内、Layer 为 Actor 且 Tag 为 Player 的角色。
    /// </summary>
    [Tooltip("双角色关卡填写第二个受保护 Actor；单角色关卡留空。")]
    [SerializeField] private ActorCtrl[] additionalActors;

    /// <summary>
    /// 本关蜂巢组件；蜂群生成、模板、路线与运行实例都由该组件管理。
    /// </summary>
    [Tooltip("包含 DLevel53Hive 的蜂巢实例。")]
    [SerializeField] private DLevel53Hive hive;

    /// <summary>
    /// 复用 D003 的 Knock_Explosion 根粒子实例；命中时移动到真实接触点后播放。
    /// </summary>
    [Tooltip("玩家受击时复用的 Knock_Explosion 粒子。")]
    [SerializeField] private ParticleSystem knockEffect;

    #endregion

    #region Failure Settings

    /// <summary>
    /// 玩家命中演出开始后，到提交标准失败重建之间的等待时间。
    /// 本关会先暂停共享成功倒计时，因此该时间不会继续被计入胜利进度。
    /// </summary>
    [Header("失败演出")]
    [Tooltip("玩家受击后等待多少秒再执行标准失败重建。")]
    [SerializeField, Min(0f)] private float failureDelay = 1.5f;

    #endregion

    #region Runtime State

    private readonly HashSet<DLevel53Bee> _playerHitBees = new();
    private readonly HashSet<ActorCtrl> _playerHitActors = new();
    private readonly List<ActorCtrl> _targetActors = new();
    private int _attemptVersion;
    private bool _isFailing;
    private bool _isLevelResolved;

    /// <summary>
    /// 蜜蜂是否必须停止动画与物理运动。
    /// 首次受击后的失败演出不停止蜂群；真正暂停或关卡结算仍会停止。
    /// 仅供本关蜂巢与蜜蜂读取，不代表全局 Physics2D 已暂停。
    /// </summary>
    internal bool IsBeeSimulationSuspended =>
        _isLevelResolved ||
        drawLevelCtrl == null ||
        drawLevelCtrl.IsPaused ||
        (drawLevelCtrl.IsOutcomeLocked && !_isFailing);

    /// <summary>
    /// 供兼容旧关卡读取首个追踪目标；双角色生成时请使用 GetRandomTargetActor。
    /// </summary>
    internal ActorCtrl TargetActor => actor;

    /// <summary>
    /// 为新生成的蜜蜂随机选择一个本关受保护角色。
    /// 调用边界：只由 DLevel53Hive 在生成时调用；返回值会作为该蜜蜂本次尝试的固定目标。
    /// </summary>
    internal ActorCtrl GetRandomTargetActor()
    {
        if (_targetActors.Count == 0)
            RefreshTargetActors();

        if (_targetActors.Count == 0)
            return null;

        return _targetActors[Random.Range(0, _targetActors.Count)];
    }

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        if (drawLevelCtrl != null)
            drawLevelCtrl.Resolved += OnLevelResolved;
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        if (drawLevelCtrl != null)
        {
            drawLevelCtrl.Resolved -= OnLevelResolved;
            if (_isFailing)
                drawLevelCtrl.CancelFailurePerformance();
        }

        _attemptVersion++;
        _isFailing = false;
        _isLevelResolved = false;
        _playerHitBees.Clear();
        _playerHitActors.Clear();
        StopKnockEffect();
    }

    #endregion

    #region Draw Attempt Lifecycle

    /// <inheritdoc />
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        // 生成入口由同一层级的 DLevel53Hive 消费；本组件保留接口以接收 Reset 生命周期。
    }

    /// <inheritdoc />
    public void OnDrawAttemptReset()
    {
        if (!Application.isPlaying)
            return;

        _attemptVersion++;
        if (_isFailing && drawLevelCtrl != null)
            drawLevelCtrl.CancelFailurePerformance();

        _isFailing = false;
        _isLevelResolved = false;
        _playerHitBees.Clear();
        _playerHitActors.Clear();
        StopKnockEffect();
    }

    #endregion

    #region Bee Hit API

    /// <summary>
    /// 接收单只蜜蜂与玩家的真实 Collision2D。
    /// 首只命中的蜜蜂启动一次失败链并播放角色肿胀动画；后续蜜蜂仍可各播放一次受击表现。
    /// 同一只蜜蜂的 CollisionStay 不会重复播放表现或提交失败。
    /// </summary>
    internal bool TryHandlePlayerHit(
        DLevel53Bee sourceBee,
        Collision2D collision,
        Collider2D playerCollider)
    {
        if (!Application.isPlaying ||
            sourceBee == null ||
            collision == null ||
            !IsCurrentPlayerCollider(playerCollider) ||
            _isLevelResolved ||
            drawLevelCtrl == null ||
            _targetActors.Count == 0)
        {
            return false;
        }

        ActorCtrl hitActor = ResolveActorFromCollider(playerCollider);
        if (hitActor == null || !_targetActors.Contains(hitActor))
            return false;

        if (_playerHitBees.Contains(sourceBee))
            return true;

        if (_isFailing)
        {
            _playerHitBees.Add(sourceBee);
            PlayPlayerHitPresentation(collision, playerCollider);
            PlayActorDeathPresentation(hitActor);
            return true;
        }

        if (drawLevelCtrl.IsOutcomeLocked || hitActor.IsDied)
            return false;

        if (!drawLevelCtrl.TryBeginFailurePerformance())
            return false;

        _playerHitBees.Add(sourceBee);
        _isFailing = true;
        int version = ++_attemptVersion;

        PlayPlayerHitPresentation(collision, playerCollider);
        PlayActorDeathPresentation(hitActor);
        CompleteFailureAsync(version).Forget();
        return true;
    }

    /// <summary>
    /// 判断碰撞体是否属于本关当前玩家。
    /// 调用方必须传入真实碰撞中的另一方 Collider2D，Trigger 不在此接口处理。
    /// </summary>
    internal bool IsCurrentPlayerCollider(Collider2D collider)
    {
        if (collider == null || _targetActors.Count == 0)
            return false;

        ActorCtrl hitActor = ResolveActorFromCollider(collider);
        return hitActor != null && _targetActors.Contains(hitActor);
    }

    #endregion

    #region Failure Flow

    private void PlayPlayerHitPresentation(
        Collision2D collision,
        Collider2D playerCollider)
    {
        PlayKnock(GetImpactPosition(collision, playerCollider));
        AudioManager.PlaySound("trap");
    }

    private void PlayActorDeathPresentation(ActorCtrl targetActor)
    {
        if (targetActor == null || !_playerHitActors.Add(targetActor))
            return;

        // 先锁定该角色的移动与自动动画，再用 swell 覆盖默认 die。
        targetActor.Die();
        PlayPlayerSwellAnimation(targetActor);
    }

    private void PlayPlayerSwellAnimation(ActorCtrl targetActor)
    {
        ActorSpineCtrl spine = targetActor != null ? targetActor.SpineCtrl : null;
        var animationState = spine != null ? spine.AnimationState : null;
        var skeletonData = animationState?.Data?.SkeletonData;
        if (skeletonData?.FindAnimation(SwellAnimationName) == null)
        {
            this.LogError(
                $"[DLevel53Ctrl] 角色 {targetActor?.name} 骨骼缺少 swell 动画，保留标准失败流程。");
            return;
        }

        spine.PlayAnimation(SwellAnimationName, false, false);
        if (skeletonData.FindAnimation(SwellIdleAnimationName) != null)
        {
            animationState.AddAnimation(
                0,
                SwellIdleAnimationName,
                true,
                0f);
        }
    }

    private Vector3 GetImpactPosition(
        Collision2D collision,
        Collider2D playerCollider)
    {
        Vector2 point = collision.contactCount > 0
            ? collision.GetContact(0).point
            : playerCollider.ClosestPoint(transform.position);
        float effectDepth = knockEffect != null
            ? knockEffect.transform.position.z
            : transform.position.z;
        return new Vector3(point.x, point.y, effectDepth);
    }

    private void PlayKnock(Vector3 impactPosition)
    {
        if (knockEffect == null)
            return;

        knockEffect.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear);
        knockEffect.transform.position = impactPosition;
        knockEffect.Play(true);
    }

    private async UniTaskVoid CompleteFailureAsync(int version)
    {
        bool canceled = await UniTask.WaitForSeconds(
                Mathf.Max(0f, failureDelay),
                ignoreTimeScale: true,
                cancellationToken: this.GetCancellationTokenOnDestroy())
            .SuppressCancellationThrow();
        if (canceled ||
            this == null ||
            version != _attemptVersion ||
            !_isFailing ||
            drawLevelCtrl == null)
        {
            return;
        }

        if (!drawLevelCtrl.CompleteFailurePerformance(nameof(DLevel53Ctrl)))
        {
            this.LogError(
                "[DLevel53Ctrl] 失败演出完成后未能提交标准失败重建，已尝试释放演出占用。");
            drawLevelCtrl.CancelFailurePerformance();
        }
    }

    #endregion

    #region State And References

    private void OnLevelResolved(DrawLevelResult result)
    {
        _isLevelResolved = true;
        DLevel53Hive[] hives =
            GetComponentsInChildren<DLevel53Hive>(true);
        for (int i = 0; i < hives.Length; i++)
            hives[i].SetSimulationSuspended(true);
    }

    private void StopKnockEffect()
    {
        if (knockEffect != null)
        {
            knockEffect.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void ResolveReferences()
    {
        if (drawLevelCtrl == null)
            drawLevelCtrl = GetComponentInParent<DrawLevelCtrl>(true);

        if (actor == null)
            actor = drawLevelCtrl != null
                ? drawLevelCtrl.Actor
                : GetComponentInChildren<ActorCtrl>(true);

        if (hive == null)
            hive = GetComponentInChildren<DLevel53Hive>(true);

        RefreshTargetActors();
    }

    private void RefreshTargetActors()
    {
        _targetActors.Clear();
        AddTargetActor(actor);

        if (additionalActors == null)
            return;

        for (int i = 0; i < additionalActors.Length; i++)
            AddTargetActor(additionalActors[i]);
    }

    private void AddTargetActor(ActorCtrl candidate)
    {
        if (candidate == null ||
            _targetActors.Contains(candidate) ||
            candidate.gameObject.layer != LayerMask.NameToLayer("Actor") ||
            !candidate.CompareTag("Player"))
        {
            return;
        }

        _targetActors.Add(candidate);
    }

    private static ActorCtrl ResolveActorFromCollider(Collider2D collider)
    {
        if (collider == null)
            return null;

        ActorCtrl hitActor = collider.GetComponent<ActorCtrl>();
        if (hitActor == null)
            hitActor = collider.GetComponentInParent<ActorCtrl>();

        if (hitActor == null && collider.attachedRigidbody != null)
        {
            hitActor = collider.attachedRigidbody.GetComponent<ActorCtrl>();
            if (hitActor == null)
            {
                hitActor = collider.attachedRigidbody
                    .GetComponentInChildren<ActorCtrl>(true);
            }
        }

        return hitActor;
    }

    #endregion
}
