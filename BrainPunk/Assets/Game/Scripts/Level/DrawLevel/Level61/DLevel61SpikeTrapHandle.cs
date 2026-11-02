using Cysharp.Threading.Tasks;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// DLevel_061 尖刺命中后的局部失败处理器。
/// 先占用 DrawLevelCtrl 的失败演出窗口，再让角色死亡，避免共享 Tick 在下一帧提前结算。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 61 Spike Trap Handle")]
public sealed class DLevel61SpikeTrapHandle : MonoBehaviour,
    IDrawStrokeCompletedHandler
{
    #region Inspector References

    /// <summary>
    /// 当前 DLevel 根节点的状态控制器；只允许绑定同一关卡实例。
    /// </summary>
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;

    /// <summary>
    /// 本关唯一玩家 Actor；尖刺只处理这个角色，不误伤其它 Actor 层物体。
    /// </summary>
    [SerializeField] private ActorCtrl actor;

    /// <summary>
    /// 当前 DLevel 内复用的 Knock 粒子实例。
    /// </summary>
    [SerializeField] private ParticleSystem knockEffect;

    #endregion

    #region Settings

    /// <summary>
    /// 尖刺命中后保留失败演出的时间，结束后提交标准 Draw 失败重开。
    /// </summary>
    [Header("失败表现")]
    [SerializeField, Min(0f)] private float failDelay = 0.5f;

    /// <summary>
    /// 尖刺命中后施加给角色的向上击退力；设为 0 可关闭击退而保留死亡。
    /// </summary>
    [SerializeField, Min(0f)] private float knockbackForce = 10f;

    #endregion

    #region Runtime State

    private OnTrigger2DHandle[] _spikeTriggers;
    private int _attemptVersion;
    private bool _isFailing;
    private bool _ownsFailurePerformance;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        BindSpikeTriggers();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        _attemptVersion++;
        UnbindSpikeTriggers();
        StopKnock();
        _isFailing = false;
        ReleaseFailurePerformance();
    }

    #endregion

    #region Spike Trigger

    /// <summary>
    /// 由本关 Caltrop_long 的 OnTrigger2DHandle 调用；只接受当前 D061 Actor。
    /// </summary>
    public void OnSpikeTriggered(Collider2D collider)
    {
        if (!Application.isPlaying ||
            _isFailing ||
            collider == null ||
            drawLevelCtrl == null ||
            actor == null ||
            actor.IsDied ||
            drawLevelCtrl.IsOutcomeLocked)
        {
            return;
        }

        ActorCtrl hitActor = ResolveActor(collider);
        if (hitActor != actor || !hitActor.transform.IsChildOf(transform))
            return;

        if (!drawLevelCtrl.TryBeginFailurePerformance())
            return;

        _isFailing = true;
        _ownsFailurePerformance = true;
        int version = ++_attemptVersion;

        PlayKnock(GetImpactPosition(collider));
        actor.Die();
        if (knockbackForce > 0f)
            actor.Knockback(Vector2.up, knockbackForce).Forget();
        global::Game.Scripts.Common.AudioManager.PlaySound("trap");

        TriggerFailureAsync(version).Forget();
    }

    private ActorCtrl ResolveActor(Collider2D collider)
    {
        ActorCtrl hitActor = collider.GetComponentInParent<ActorCtrl>();
        if (hitActor != null)
            return hitActor;

        Rigidbody2D attachedBody = collider.attachedRigidbody;
        return attachedBody != null
            ? attachedBody.GetComponentInChildren<ActorCtrl>(true)
            : null;
    }

    private Vector3 GetImpactPosition(Collider2D collider)
    {
        Vector2 point = collider.ClosestPoint(actor.transform.position);
        float effectDepth = knockEffect != null
            ? knockEffect.transform.position.z
            : transform.position.z;
        return new Vector3(point.x, point.y, effectDepth);
    }

    private void BindSpikeTriggers()
    {
        UnbindSpikeTriggers();

        int triggerLayer = LayerMask.NameToLayer("TriggerItem");
        OnTrigger2DHandle[] candidates =
            GetComponentsInChildren<OnTrigger2DHandle>(true);

        int count = 0;
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i].gameObject.layer == triggerLayer)
                count++;
        }

        _spikeTriggers = new OnTrigger2DHandle[count];
        int index = 0;
        for (int i = 0; i < candidates.Length; i++)
        {
            OnTrigger2DHandle candidate = candidates[i];
            if (candidate.gameObject.layer != triggerLayer)
                continue;

            candidate.SetTargetTag(string.Empty);
            candidate.OnEnter.AddListener(OnSpikeTriggered);
            _spikeTriggers[index++] = candidate;
        }
    }

    private void UnbindSpikeTriggers()
    {
        if (_spikeTriggers == null)
            return;

        for (int i = 0; i < _spikeTriggers.Length; i++)
            _spikeTriggers[i]?.OnEnter.RemoveListener(OnSpikeTriggered);

        _spikeTriggers = null;
    }

    #endregion

    #region Draw Level Handlers

    /// <inheritdoc />
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
    }

    /// <inheritdoc />
    public void OnDrawAttemptReset()
    {
        if (!Application.isPlaying)
            return;

        _attemptVersion++;
        _isFailing = false;
        ReleaseFailurePerformance();
        StopKnock();
    }

    #endregion

    #region Failure Flow

    private async UniTaskVoid TriggerFailureAsync(int version)
    {
        bool canceled = await UniTask.WaitForSeconds(
                Mathf.Max(0f, failDelay),
                ignoreTimeScale: true,
                cancellationToken: this.GetCancellationTokenOnDestroy())
            .SuppressCancellationThrow();
        if (canceled ||
            this == null ||
            version != _attemptVersion ||
            !_isFailing ||
            !_ownsFailurePerformance ||
            drawLevelCtrl == null)
        {
            return;
        }

        _ownsFailurePerformance = false;
        if (!drawLevelCtrl.CompleteFailurePerformance(
                nameof(DLevel61SpikeTrapHandle)))
        {
            drawLevelCtrl.CancelFailurePerformance();
            _isFailing = false;
        }
    }

    private void ReleaseFailurePerformance()
    {
        if (!_ownsFailurePerformance)
            return;

        _ownsFailurePerformance = false;
        drawLevelCtrl?.CancelFailurePerformance();
    }

    private void PlayKnock(Vector3 worldPosition)
    {
        if (knockEffect == null)
            return;

        knockEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        knockEffect.transform.position = worldPosition;
        knockEffect.Play(true);
    }

    private void StopKnock()
    {
        if (knockEffect == null)
            return;

        knockEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    #endregion

    #region References

    private void ResolveReferences()
    {
        if (drawLevelCtrl == null)
            drawLevelCtrl = GetComponentInParent<DrawLevelCtrl>(true);

        if (actor == null && drawLevelCtrl != null)
            actor = drawLevelCtrl.Actor;
    }

    #endregion
}
