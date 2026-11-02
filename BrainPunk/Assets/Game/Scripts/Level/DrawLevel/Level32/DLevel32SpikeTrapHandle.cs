using Cysharp.Threading.Tasks;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// DLevel032 下方尖刺的双球失败入口。
/// 复用关卡现有 OnTrigger2DHandle，并在任一球内 Actor 命中时播放通用打击粒子、死亡与重置流程。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 32 Spike Trap Handle")]
public sealed class DLevel32SpikeTrapHandle : MonoBehaviour,
    IDrawStrokeCompletedHandler
{
    #region Inspector References

    /// <summary>
    /// 当前 DLevel 根节点上的状态控制器。
    /// </summary>
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;

    /// <summary>
    /// 复用 DLevel003 裁剪后的 Knock_Explosion 粒子实例。
    /// 调用边界：实例必须位于当前 DLevel032 内，并移除源 Prefab 的敲门子表现。
    /// </summary>
    [SerializeField] private ParticleSystem knockEffect;

    #endregion

    #region Settings

    [Header("失败表现")]
    [SerializeField, Min(0f)] private float failDelay = 0.5f;
    [SerializeField, Min(0f)] private float knockbackForce = 10f;

    #endregion

    private OnTrigger2DHandle[] _spikeTriggers;
    private int _attemptVersion;
    private bool _isFailing;

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
    }

    /// <summary>
    /// 由本关尖刺的 OnTrigger2DHandle 调用；兼容球根碰撞体与 Actor 子碰撞体。
    /// </summary>
    public void OnSpikeTriggered(Collider2D collider)
    {
        if (!Application.isPlaying || _isFailing || collider == null)
            return;

        ActorCtrl actor = ResolveActor(collider);
        if (actor == null || actor.IsDied || !actor.transform.IsChildOf(transform))
            return;

        _isFailing = true;
        int version = ++_attemptVersion;

        PlayKnock(actor.transform.position);
        actor.Die();
        actor.Knockback(Vector2.up, knockbackForce).Forget();
        global::Game.Scripts.Common.AudioManager.PlaySound("trap");

        TriggerFailureAsync(version).Forget();
    }

    /// <inheritdoc />
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
    }

    /// <inheritdoc />
    public void OnDrawAttemptReset()
    {
        _attemptVersion++;
        _isFailing = false;
        StopKnock();
    }

    private void ResolveReferences()
    {
        if (drawLevelCtrl == null)
            drawLevelCtrl = GetComponent<DrawLevelCtrl>();

    }

    private void BindSpikeTriggers()
    {
        int triggerLayer = LayerMask.NameToLayer("TriggerItem");
        OnTrigger2DHandle[] candidates =
            GetComponentsInChildren<OnTrigger2DHandle>(true);

        int count = 0;
        for (int i = 0; i < candidates.Length; i++)
        {
            OnTrigger2DHandle candidate = candidates[i];
            if (candidate.gameObject.layer == triggerLayer)
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
        {
            OnTrigger2DHandle trigger = _spikeTriggers[i];
            trigger?.OnEnter.RemoveListener(OnSpikeTriggered);
        }

        _spikeTriggers = null;
    }

    private ActorCtrl ResolveActor(Collider2D collider)
    {
        ActorCtrl actor = collider.GetComponentInParent<ActorCtrl>();
        if (actor != null)
            return actor;

        Rigidbody2D attachedBody = collider.attachedRigidbody;
        return attachedBody != null
            ? attachedBody.GetComponentInChildren<ActorCtrl>(true)
            : null;
    }

    private void PlayKnock(Vector3 worldPosition)
    {
        if (knockEffect == null)
            return;

        knockEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        knockEffect.transform.position = worldPosition;
        knockEffect.Play(true);
    }

    private async UniTaskVoid TriggerFailureAsync(int version)
    {
        bool isCanceled = await UniTask.WaitForSeconds(
                Mathf.Max(0f, failDelay),
                cancellationToken: this.GetCancellationTokenOnDestroy())
            .SuppressCancellationThrow();
        if (isCanceled || this == null || version != _attemptVersion)
            return;

        drawLevelCtrl?.TryFailAndReset(nameof(DLevel32SpikeTrapHandle));
    }

    private void StopKnock()
    {
        if (knockEffect == null)
            return;

        knockEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
