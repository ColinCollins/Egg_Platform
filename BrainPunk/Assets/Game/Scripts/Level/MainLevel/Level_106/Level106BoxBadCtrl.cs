using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Scripts.Common;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Level 106 UFO 上的 Box-Bad 局部演出：右移解锁后允许玩家接近，接触一次后播放命中反馈并转入自由重力下落。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level106BoxBadCtrl : MonoBehaviour
{
    private const string BoxBadSkinName = "box_bad";
    private const string IdleAnimationName = "idle";
    private const string DieAnimationName = "die";
    private static readonly Vector2 DefaultKnockdownDirection = new Vector2(0.5f, 0.5f);
    private const float DefaultKnockdownForce = 50f;
    private const float DefaultKnockdownGravityScale = 4.5f;
    private const float DefaultKnockdownRotationDuration = 0.5f;
    private const string AttachedSortingLayerName = "Default";
    private const int AttachedSortingOrder = 4;
    private const string DetachedSortingLayerName = "SceneLayer";
    private const int DetachedSortingOrder = 210;

    [SerializeField] private ActorCtrl actor;
    [SerializeField] private ActorSpineCtrl spineCtrl;
    [SerializeField] private ParticleSystem knockEffect;
    [SerializeField] private Collider2D encounterTrigger;
    [SerializeField] private Vector2 knockdownDirection = new Vector2(0.5f, 0.5f);
    [SerializeField, Min(0f)] private float knockdownForce = DefaultKnockdownForce;
    [SerializeField, Min(0f)] private float knockdownGravityScale = DefaultKnockdownGravityScale;
    [SerializeField, Min(0f)] private float knockdownRotationDuration = DefaultKnockdownRotationDuration;
    [SerializeField, Min(0f)] private float knockdownImpactPauseDuration = 0.08f;

    private Transform initialParent;
    private Transform detachParent;
    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private Vector3 initialLocalScale;
    private Transform actorInitialParent;
    private Vector3 actorInitialLocalPosition;
    private Quaternion actorInitialLocalRotation;
    private Vector3 actorInitialLocalScale;
    private Rigidbody2D actorBody;
    private Collider2D actorCollider;
    private SortingGroup actorSortingGroup;
    private Level106Ctrl levelController;
    private RigidbodyType2D initialBodyType;
    private float initialGravityScale;
    private bool initialSimulated;
    private bool physicsStateCaptured;
    private bool initialActorColliderEnabled;
    private LayerMask initialActorColliderExcludeLayers;
    private bool colliderStateCaptured;
    private readonly List<Collider2D> ignoredUfoColliders = new List<Collider2D>();
    private bool isArmed;
    private bool hasKnockedDown;
    private int knockdownEffectVersion;
    private Tween knockdownRotationTween;

    internal bool HasKnockedDown => hasKnockedDown;

    private void Awake()
    {
        initialParent = transform.parent;
        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
        initialLocalScale = transform.localScale;

        if (actor == null)
        {
            actor = GetComponentInChildren<ActorCtrl>(true);
        }

        if (levelController == null)
        {
            levelController = GetComponentInParent<Level106Ctrl>();
        }

        if (spineCtrl == null)
        {
            spineCtrl = actor != null
                ? actor.SpineCtrl
                : GetComponentInChildren<ActorSpineCtrl>(true);
        }

        if (encounterTrigger == null)
        {
            encounterTrigger = GetComponent<Collider2D>();
        }

        if (actor != null)
        {
            actorBody = actor.GetComponent<Rigidbody2D>();
            actorCollider = actor.GetComponent<Collider2D>();
            actorSortingGroup = actor.GetComponent<SortingGroup>();
            CaptureActorTransformState();
            CapturePhysicsState();
            CaptureColliderState();
        }

        ApplyAttachedSorting();
        PrepareIdle();
        SetEncounterEnabled(false);
    }

    private void OnDisable()
    {
        SuspendRuntime();
        ApplyAttachedSorting();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        ActorCtrl playerActor = ResolvePlayerActor(other);
        if (!isArmed || hasKnockedDown || playerActor == null)
        {
            return;
        }

        Vector3 impactPosition = ResolveImpactPosition(other);
        KnockDown(impactPosition);
    }

    private static ActorCtrl ResolvePlayerActor(Collider2D collider)
    {
        ActorCtrl collidedActor = collider != null
            ? collider.GetComponentInParent<ActorCtrl>()
            : null;
        return collidedActor != null
            && collidedActor.gameObject.layer == LayerMask.NameToLayer("Actor")
            && collidedActor.CompareTag("Player")
                ? collidedActor
                : null;
    }

    private Vector3 ResolveImpactPosition(Collider2D playerCollider)
    {
        Vector3 boxBadCenter = encounterTrigger != null
            ? encounterTrigger.bounds.center
            : transform.position;
        Vector3 playerCenter = playerCollider != null
            ? playerCollider.bounds.center
            : boxBadCenter;
        Vector3 impactPosition = Vector3.Lerp(boxBadCenter, playerCenter, 0.5f);
        impactPosition.z = transform.position.z;
        return impactPosition;
    }

    internal void Arm(Transform newDetachParent)
    {
        if (hasKnockedDown || encounterTrigger == null)
        {
            return;
        }

        detachParent = newDetachParent;
        LockAttachedPhysics();
        isArmed = true;
        SetEncounterEnabled(true);
    }

    /// <summary>
    /// 新关卡实例启用时恢复箱子怪的作者初态；不得用于 Reset/Replay 旧实例复位。
    /// </summary>
    internal void InitializeRuntimeState()
    {
        SuspendRuntime();

        hasKnockedDown = false;
        detachParent = null;
        RestorePhysicsState();
        RestoreColliderState();

        if (initialParent != null)
        {
            transform.SetParent(initialParent, false);
            transform.localPosition = initialLocalPosition;
            transform.localRotation = initialLocalRotation;
            transform.localScale = initialLocalScale;
        }

        RestoreActorTransformState();
        ApplyAttachedSorting();
        PrepareIdle();
    }

    /// <summary>
    /// 旧关卡等待销毁时立即停止箱子怪触发与异步击飞表现，不恢复任何作者初态。
    /// </summary>
    internal void SuspendRuntime()
    {
        knockdownEffectVersion++;
        StopKnockdownEffect();
        isArmed = false;
        SetEncounterEnabled(false);
    }

    private void KnockDown(Vector3 impactPosition)
    {
        int effectVersion = ++knockdownEffectVersion;
        hasKnockedDown = true;
        isArmed = false;
        SetEncounterEnabled(false);

        if (detachParent != null)
        {
            transform.SetParent(detachParent, true);
        }

        ApplyDetachedSorting();
        levelController?.NotifyBoxBadHit(knockdownImpactPauseDuration);
        PlayImpactFeedback(impactPosition);
        PlayKnockdownMotionAfterPause(effectVersion).Forget();
    }

    private void PlayImpactFeedback(Vector3 impactPosition)
    {
        spineCtrl?.PlayAnimation(DieAnimationName, false, false);

        if (knockEffect != null)
        {
            knockEffect.transform.position = impactPosition;
            knockEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            knockEffect.Play(true);
        }

        AudioManager.PlaySound("trap");
    }

    private async UniTaskVoid PlayKnockdownMotionAfterPause(int effectVersion)
    {
        float pauseDuration = Mathf.Max(0f, knockdownImpactPauseDuration);
        if (pauseDuration > 0f)
        {
            await UniTask.WaitForSeconds(
                pauseDuration,
                cancellationToken: this.GetCancellationTokenOnDestroy());
        }

        if (!hasKnockedDown || effectVersion != knockdownEffectVersion)
        {
            return;
        }

        StartFreeFall();
    }

    private void StartFreeFall()
    {
        if (actor == null || actorBody == null || actorCollider == null)
        {
            return;
        }

        Transform freeFallParent = detachParent != null ? detachParent : transform.parent;
        actor.transform.SetParent(freeFallParent, true);

        actorBody.simulated = false;
        actorBody.bodyType = RigidbodyType2D.Dynamic;
        actorBody.gravityScale = Mathf.Max(0f, knockdownGravityScale);
        actorBody.linearVelocity = Vector2.zero;
        actorBody.angularVelocity = 0f;

        actorCollider.isTrigger = false;
        actorCollider.excludeLayers = (int)initialActorColliderExcludeLayers | LayerMask.GetMask("Actor");
        actorCollider.enabled = true;
        SetUfoCollisionsIgnored(true);

        actorBody.simulated = true;
        Vector2 direction = knockdownDirection.sqrMagnitude > 0.0001f
            ? knockdownDirection
            : DefaultKnockdownDirection;
        actorBody.linearVelocity = direction * Mathf.Max(0f, knockdownForce);
        actorBody.WakeUp();

        knockdownRotationTween?.Kill(false);
        Vector3 targetAngles = actor.transform.localEulerAngles;
        targetAngles.z += 360f;
        knockdownRotationTween = actor.transform
            .DOLocalRotate(
                targetAngles,
                Mathf.Max(0.01f, knockdownRotationDuration),
                RotateMode.FastBeyond360)
            .SetEase(Ease.Linear)
            .SetLoops(-1)
            .SetLink(actor.gameObject);
    }

    private void StopKnockdownEffect()
    {
        knockdownRotationTween?.Kill(false);
        knockdownRotationTween = null;
        SetUfoCollisionsIgnored(false);

        if (actorBody != null)
        {
            actorBody.linearVelocity = Vector2.zero;
            actorBody.angularVelocity = 0f;
            actorBody.simulated = false;
        }

        if (actorCollider != null)
        {
            actorCollider.enabled = false;
        }

        knockEffect?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void SetUfoCollisionsIgnored(bool ignored)
    {
        if (!ignored)
        {
            for (int i = 0; i < ignoredUfoColliders.Count; i++)
            {
                Collider2D ufoCollider = ignoredUfoColliders[i];
                if (actorCollider != null && ufoCollider != null)
                {
                    Physics2D.IgnoreCollision(actorCollider, ufoCollider, false);
                }
            }

            ignoredUfoColliders.Clear();
            return;
        }

        ignoredUfoColliders.Clear();
        if (actorCollider == null || initialParent == null)
        {
            return;
        }

        Collider2D[] ufoColliders = initialParent.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < ufoColliders.Length; i++)
        {
            Collider2D ufoCollider = ufoColliders[i];
            if (ufoCollider == null || ufoCollider == actorCollider || ufoCollider == encounterTrigger)
            {
                continue;
            }

            Physics2D.IgnoreCollision(actorCollider, ufoCollider, true);
            ignoredUfoColliders.Add(ufoCollider);
        }
    }

    private void ApplyAttachedSorting()
    {
        ApplyActorSorting(AttachedSortingLayerName, AttachedSortingOrder);
    }

    private void ApplyDetachedSorting()
    {
        ApplyActorSorting(DetachedSortingLayerName, DetachedSortingOrder);
    }

    private void ApplyActorSorting(string sortingLayerName, int sortingOrder)
    {
        if (actorSortingGroup == null && actor != null)
        {
            actorSortingGroup = actor.GetComponent<SortingGroup>();
        }

        if (actorSortingGroup == null)
        {
            return;
        }

        actorSortingGroup.sortingLayerName = sortingLayerName;
        actorSortingGroup.sortingOrder = sortingOrder;
    }

    private void PrepareIdle()
    {
        if (spineCtrl == null)
        {
            return;
        }

        spineCtrl.SetSkin(BoxBadSkinName);
        spineCtrl.PlayAnimation(IdleAnimationName, true, false);
    }

    private void CaptureActorTransformState()
    {
        if (actor == null || actorInitialParent != null)
        {
            return;
        }

        actorInitialParent = actor.transform.parent;
        actorInitialLocalPosition = actor.transform.localPosition;
        actorInitialLocalRotation = actor.transform.localRotation;
        actorInitialLocalScale = actor.transform.localScale;
    }

    private void RestoreActorTransformState()
    {
        if (actor == null || actorInitialParent == null)
        {
            return;
        }

        actor.transform.SetParent(actorInitialParent, false);
        actor.transform.localPosition = actorInitialLocalPosition;
        actor.transform.localRotation = actorInitialLocalRotation;
        actor.transform.localScale = actorInitialLocalScale;
    }

    private void CapturePhysicsState()
    {
        if (actorBody == null || physicsStateCaptured)
        {
            return;
        }

        initialBodyType = actorBody.bodyType;
        initialGravityScale = actorBody.gravityScale;
        initialSimulated = actorBody.simulated;
        physicsStateCaptured = true;
    }

    private void LockAttachedPhysics()
    {
        if (actorBody == null)
        {
            return;
        }

        CapturePhysicsState();
        actorBody.bodyType = RigidbodyType2D.Kinematic;
        actorBody.gravityScale = 0f;
        actorBody.linearVelocity = Vector2.zero;
        actorBody.angularVelocity = 0f;
        actorBody.simulated = initialSimulated;
    }

    private void RestorePhysicsState()
    {
        if (actorBody == null || !physicsStateCaptured)
        {
            return;
        }

        actorBody.bodyType = initialBodyType;
        actorBody.gravityScale = initialGravityScale;
        actorBody.linearVelocity = Vector2.zero;
        actorBody.angularVelocity = 0f;
        actorBody.simulated = initialSimulated;
    }

    private void CaptureColliderState()
    {
        if (actorCollider == null || colliderStateCaptured)
        {
            return;
        }

        initialActorColliderEnabled = actorCollider.enabled;
        initialActorColliderExcludeLayers = actorCollider.excludeLayers;
        colliderStateCaptured = true;
    }

    private void RestoreColliderState()
    {
        if (actorCollider == null || !colliderStateCaptured)
        {
            return;
        }

        actorCollider.excludeLayers = initialActorColliderExcludeLayers;
        actorCollider.enabled = initialActorColliderEnabled;
    }

    private void SetEncounterEnabled(bool isEnabled)
    {
        if (encounterTrigger != null)
        {
            encounterTrigger.enabled = isEnabled;
        }
    }
}
