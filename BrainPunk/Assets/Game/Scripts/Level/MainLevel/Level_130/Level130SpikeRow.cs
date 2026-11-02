using Bear.EventSystem;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// One physical Level 130 spike row. All active rows gate jump input; only visible
/// rows execute it. Rows keep the standard Actor trap result. Entering a FallArea or falling below the
/// gameplay view permanently removes a row from this level instance.
/// </summary>
[DisallowMultipleComponent]
public sealed class Level130SpikeRow : MonoBehaviour, IEventSender
{
    [Header("References")]
    [SerializeField] private Rigidbody2D body;
    [SerializeField] private Collider2D supportCollider;
    [SerializeField] private Collider2D[] tipColliders;
    [SerializeField] private ActorCtrl actor;
    [SerializeField] private Level130Ctrl levelCtrl;

    [Header("Jump")]
    [SerializeField] private LayerMask supportLayers = 1 << 6;
    [SerializeField, Min(0.01f)] private float jumpHeight = 4.5f;
    [SerializeField, Min(0f)] private float fallGravityScale = 0.78125f;
    [SerializeField, Min(0.01f)] private float groundProbeDistance = 0.1f;
    [SerializeField, Min(0f)] private float ascendingVelocityThreshold = 0.1f;
    [SerializeField] private string jumpSfxTag = "jump";

    [Header("Actor Trap")]
    [SerializeField, Min(0f)] private float knockForce = 10f;
    [SerializeField, Min(0f)] private float trapDelay = 0.5f;

    [Header("Door Landing")]
    [SerializeField, Min(0.01f)] private float doorLandingFadeDuration = 0.3f;

    [Header("Stone Landing Visuals")]
    [SerializeField] private bool alignVisualsOnStonePlatforms;
    [SerializeField, Min(0f)] private float stoneLandingVisualInset;

    private bool jumpInProgress;
    private bool becameAirborne;
    private bool actorTrapFired;
    private bool isGameplayActive = true;
    private bool pausedBodyWasSimulated;
    private Vector2 pausedVelocity;
    private float pausedAngularVelocity;
    private float ascendingGravityScale;
    private bool removedFromSynchronizedGroup;
    private bool collisionDisabledAfterDoorLanding;
    private Sequence doorLandingFadeTween;
    private SpriteRenderer[] spikeVisuals;
    private Vector3[] authoredVisualPositions;
    private Level130AutomaticPlatform[] stonePlatforms;
    private float stoneVisualOffsetY;

    public Rigidbody2D Body => body;
    public Collider2D SupportCollider => supportCollider;
    public bool IsAscending => body != null && body.simulated && body.linearVelocityY > ascendingVelocityThreshold;
    public float CenterWorldY => supportCollider != null ? supportCollider.bounds.center.y : transform.position.y;
    public bool IsSynchronizedParticipant => !removedFromSynchronizedGroup;

    private void Awake()
    {
        ResolveReferences();
        ascendingGravityScale = body != null ? body.gravityScale : 1f;
    }

    private void FixedUpdate()
    {
        if (!jumpInProgress || body == null || !body.simulated)
            return;

        bool grounded = IsGrounded();
        if (!becameAirborne)
        {
            if (!grounded || body.linearVelocityY > ascendingVelocityThreshold)
                becameAirborne = true;
            return;
        }

        if (body.linearVelocityY <= 0f)
            body.gravityScale = Mathf.Max(0f, fallGravityScale);

        if (grounded && body.linearVelocityY <= ascendingVelocityThreshold)
        {
            jumpInProgress = false;
            becameAirborne = false;
            body.gravityScale = ascendingGravityScale;
            StopHorizontalMotion();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleTrigger(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleTrigger(other);
    }

    private void LateUpdate()
    {
        if (!alignVisualsOnStonePlatforms || !isGameplayActive || removedFromSynchronizedGroup ||
            body == null || !body.simulated)
        {
            return;
        }

        float offsetY = 0f;
        if (!IsAscending)
        {
            for (int i = 0; stonePlatforms != null && i < stonePlatforms.Length; i++)
            {
                Level130AutomaticPlatform platform = stonePlatforms[i];
                if (platform == null || !platform.isActiveAndEnabled ||
                    !platform.TryGetStandingVisualTop(supportCollider, out float surfaceY))
                {
                    continue;
                }

                float authoredBottom = float.PositiveInfinity;
                for (int j = 0; spikeVisuals != null && j < spikeVisuals.Length; j++)
                {
                    SpriteRenderer visual = spikeVisuals[j];
                    if (visual != null && visual.enabled && visual.gameObject.activeInHierarchy)
                        authoredBottom = Mathf.Min(authoredBottom, visual.bounds.min.y - stoneVisualOffsetY);
                }

                if (!float.IsPositiveInfinity(authoredBottom))
                {
                    // Match the accepted second row's overlap with the stone,
                    // rather than merely touching the outer Sprite bounds.
                    float targetBottomY = surfaceY - Mathf.Max(0f, stoneLandingVisualInset);
                    offsetY = Mathf.Min(0f, targetBottomY - authoredBottom);
                }
                break;
            }
        }

        ApplyStoneVisualOffset(offsetY);
    }

    private void OnDisable()
    {
        ApplyStoneVisualOffset(0f);
    }

    private void ApplyStoneVisualOffset(float worldOffsetY)
    {
        if (authoredVisualPositions == null || spikeVisuals == null)
            return;

        for (int i = 0; i < spikeVisuals.Length; i++)
        {
            SpriteRenderer visual = spikeVisuals[i];
            if (visual == null || visual.transform == transform)
                continue;

            Transform visualTransform = visual.transform;
            Vector3 localOffset = visualTransform.parent != null
                ? visualTransform.parent.InverseTransformVector(Vector3.up * worldOffsetY)
                : Vector3.up * worldOffsetY;
            // In this row each damage Trigger is on the same object as its Sprite.
            // Move that whole child, never the support Collider or Rigidbody root.
            visualTransform.localPosition = authoredVisualPositions[i] + localOffset;
        }

        stoneVisualOffsetY = worldOffsetY;
    }

    public bool IsVisibleIn(Camera camera, Plane[] viewPlanes)
    {
        ResolveReferences();
        if (removedFromSynchronizedGroup || !isActiveAndEnabled || camera == null || viewPlanes == null)
            return false;

        for (int i = 0; spikeVisuals != null && i < spikeVisuals.Length; i++)
        {
            SpriteRenderer visual = spikeVisuals[i];
            if (visual == null || !visual.enabled || !visual.gameObject.activeInHierarchy ||
                visual.sprite == null || visual.color.a <= 0f ||
                (camera.cullingMask & (1 << visual.gameObject.layer)) == 0)
            {
                continue;
            }

            if (GeometryUtility.TestPlanesAABB(viewPlanes, visual.bounds))
                return true;
        }

        return false;
    }

    public bool TryJump()
    {
        ResolveReferences();
        if (!CanStartJump())
            return false;

        StartJump(true);
        return true;
    }

    public bool CanStartJump()
    {
        ResolveReferences();
        if (removedFromSynchronizedGroup || !isGameplayActive || jumpInProgress ||
            body == null || !body.simulated || !IsGrounded())
        {
            return false;
        }

        float gravity = Mathf.Abs(Physics2D.gravity.y * ascendingGravityScale);
        return gravity > Mathf.Epsilon;
    }

    public void StartJump()
    {
        StartJump(true);
    }

    public void StartJump(bool playSound)
    {
        if (!CanStartJump())
            return;

        ApplyStoneVisualOffset(0f);
        body.gravityScale = ascendingGravityScale;
        float gravity = Mathf.Abs(Physics2D.gravity.y * ascendingGravityScale);
        jumpInProgress = true;
        becameAirborne = false;
        float jumpVelocity = Mathf.Sqrt(2f * gravity * Mathf.Max(0.01f, jumpHeight));
        body.linearVelocity = new Vector2(0f, jumpVelocity);
        if (playSound && !string.IsNullOrWhiteSpace(jumpSfxTag))
            AudioManager.PlaySound(jumpSfxTag);
    }

    public void StopHorizontalMotion()
    {
        if (body != null && body.simulated)
            body.linearVelocity = new Vector2(0f, body.linearVelocityY);
    }

    public void DisableCollisionAfterDoorLanding()
    {
        if (collisionDisabledAfterDoorLanding)
            return;

        ResolveReferences();
        collisionDisabledAfterDoorLanding = true;
        removedFromSynchronizedGroup = true;
        jumpInProgress = false;
        becameAirborne = false;

        if (supportCollider != null)
            supportCollider.enabled = false;

        for (int i = 0; tipColliders != null && i < tipColliders.Length; i++)
        {
            if (tipColliders[i] != null)
                tipColliders[i].enabled = false;
        }

        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }

        PlayDoorLandingFade();
    }

    public void SetGameplayActive(bool active)
    {
        ResolveReferences();
        if (isGameplayActive == active || body == null)
            return;

        isGameplayActive = active;
        if (doorLandingFadeTween != null && doorLandingFadeTween.IsActive())
        {
            if (active)
                doorLandingFadeTween.Play();
            else
                doorLandingFadeTween.Pause();
        }

        if (!active)
        {
            pausedBodyWasSimulated = body.simulated;
            if (pausedBodyWasSimulated)
            {
                pausedVelocity = body.linearVelocity;
                pausedAngularVelocity = body.angularVelocity;
                body.simulated = false;
            }
            return;
        }

        if (pausedBodyWasSimulated && !removedFromSynchronizedGroup)
        {
            body.simulated = true;
            body.linearVelocity = pausedVelocity;
            body.angularVelocity = pausedAngularVelocity;
        }
        pausedBodyWasSimulated = false;
    }

    private void OnDestroy()
    {
        if (doorLandingFadeTween != null && doorLandingFadeTween.IsActive())
            doorLandingFadeTween.Kill();
        doorLandingFadeTween = null;
    }

    private void PlayDoorLandingFade()
    {
        if (!gameObject.activeInHierarchy)
            return;

        if (doorLandingFadeTween != null && doorLandingFadeTween.IsActive())
            doorLandingFadeTween.Kill();

        SpriteRenderer[] visuals = GetComponentsInChildren<SpriteRenderer>(true);
        float duration = Mathf.Max(0.01f, doorLandingFadeDuration);
        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        bool hasVisibleRenderer = false;
        for (int i = 0; i < visuals.Length; i++)
        {
            SpriteRenderer visual = visuals[i];
            if (visual == null || !visual.enabled)
                continue;

            hasVisibleRenderer = true;
            sequence.Join(visual.DOFade(0f, duration).SetEase(Ease.Linear));
        }

        if (!hasVisibleRenderer)
        {
            sequence.Kill();
            return;
        }

        doorLandingFadeTween = sequence;
        sequence.OnComplete(() =>
        {
            doorLandingFadeTween = null;
            for (int i = 0; i < visuals.Length; i++)
            {
                if (visuals[i] != null)
                    visuals[i].enabled = false;
            }
        });
    }

    private void HandleTrigger(Collider2D other)
    {
        if (removedFromSynchronizedGroup || other == null)
            return;

        if (other.gameObject.layer == LayerMask.NameToLayer("FallArea"))
        {
            RemoveFromSynchronizedGroup();
            return;
        }

        if (TryStopAtStonePlatformUnderside(other))
            return;

        TryTriggerActorTrap(other);

        Level130BreakablePlatform platform = other.GetComponentInParent<Level130BreakablePlatform>();
        if (platform != null)
            platform.TryBreakFromSpike(this, other);
    }

    private bool TryStopAtStonePlatformUnderside(Collider2D platformCollider)
    {
        if (!IsAscending || platformCollider == null || !platformCollider.enabled ||
            platformCollider.isTrigger || !IsOwnedStonePlatformCollider(platformCollider))
        {
            return false;
        }

        Bounds platformBounds = platformCollider.bounds;
        if (CenterWorldY >= platformBounds.center.y || tipColliders == null)
            return false;

        bool crossesUnderside = false;
        float highestVisualTop = float.NegativeInfinity;
        for (int i = 0; i < tipColliders.Length; i++)
        {
            Collider2D tipCollider = tipColliders[i];
            if (tipCollider == null || !tipCollider.enabled || !tipCollider.gameObject.activeInHierarchy)
                continue;

            Bounds tipBounds = tipCollider.bounds;
            bool horizontalOverlap = tipBounds.max.x >= platformBounds.min.x &&
                                     tipBounds.min.x <= platformBounds.max.x;
            bool straddlesUnderside = tipBounds.min.y < platformBounds.min.y &&
                                     tipBounds.max.y >= platformBounds.min.y;
            if (!horizontalOverlap || !straddlesUnderside)
                continue;

            SpriteRenderer visual = tipCollider.GetComponent<SpriteRenderer>();
            float visualTop = visual != null && visual.enabled
                ? visual.bounds.max.y
                : tipBounds.max.y;
            highestVisualTop = Mathf.Max(highestVisualTop, visualTop);
            crossesUnderside = true;
        }

        if (!crossesUnderside)
            return false;

        float visiblePenetration = Mathf.Max(0f, highestVisualTop - platformBounds.min.y);
        if (visiblePenetration > 0f)
            body.position += Vector2.down * visiblePenetration;

        body.linearVelocity = new Vector2(body.linearVelocityX, 0f);
        body.gravityScale = Mathf.Max(0f, fallGravityScale);
        becameAirborne = true;
        return true;
    }

    private static bool IsOwnedStonePlatformCollider(Collider2D candidate)
    {
        Level130AutomaticPlatform automaticPlatform =
            candidate.GetComponentInParent<Level130AutomaticPlatform>();
        if (automaticPlatform != null && automaticPlatform.OwnsPlatformCollider(candidate))
            return true;

        Level130HorizontalPlatformDrag draggablePlatform =
            candidate.GetComponentInParent<Level130HorizontalPlatformDrag>();
        return draggablePlatform != null && draggablePlatform.OwnsPlatformCollider(candidate);
    }

    public bool TryRemoveIfFallenBelow(float cameraBottomY)
    {
        if (removedFromSynchronizedGroup || !isGameplayActive || !isActiveAndEnabled)
            return false;

        ResolveReferences();
        if (body == null || !body.simulated || body.linearVelocityY >= 0f)
            return false;

        // Do not depend on jumpInProgress: a row can fall without ever jumping.
        float highestWorldY = GetHighestActiveWorldY();
        if (float.IsNegativeInfinity(highestWorldY) || highestWorldY >= cameraBottomY)
            return false;

        RemoveFromSynchronizedGroup();
        return true;
    }

    private float GetHighestActiveWorldY()
    {
        float highestWorldY = GetActiveColliderTop(supportCollider);
        for (int i = 0; tipColliders != null && i < tipColliders.Length; i++)
            highestWorldY = Mathf.Max(highestWorldY, GetActiveColliderTop(tipColliders[i]));

        for (int i = 0; spikeVisuals != null && i < spikeVisuals.Length; i++)
        {
            SpriteRenderer visual = spikeVisuals[i];
            if (visual != null && visual.enabled && visual.gameObject.activeInHierarchy)
                highestWorldY = Mathf.Max(highestWorldY, visual.bounds.max.y);
        }

        return highestWorldY;
    }

    private static float GetActiveColliderTop(Collider2D target)
    {
        return target != null && target.enabled && target.gameObject.activeInHierarchy
            ? target.bounds.max.y
            : float.NegativeInfinity;
    }

    private void RemoveFromSynchronizedGroup()
    {
        if (removedFromSynchronizedGroup)
            return;

        removedFromSynchronizedGroup = true;
        jumpInProgress = false;
        becameAirborne = false;
        pausedBodyWasSimulated = false;
        pausedVelocity = Vector2.zero;
        pausedAngularVelocity = 0f;
        if (body != null)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }

        gameObject.SetActive(false);
    }

    public bool TryCommitActorTrapIfTouching(Collider2D actorCollider)
    {
        ResolveReferences();
        if (!IsActorCollider(actorCollider) || !IsTouchingAnyTip(actorCollider))
            return false;

        return TryTriggerActorTrap(actorCollider);
    }

    private bool TryTriggerActorTrap(Collider2D other)
    {
        if (actorTrapFired || actor == null || actor.IsDied)
            return actorTrapFired;

        if (!IsActorCollider(other))
            return false;

        Vector3 hitPosition = CalculateActorContactPoint(other);
        if (levelCtrl != null && !levelCtrl.TryCommitSpikeTrap(hitPosition))
            return true;

        actorTrapFired = true;
        actor.Die();
        actor.Knockback(Vector2.up, knockForce).Forget();
        AudioManager.PlaySound("trap");
        DispatchTrapFailure().Forget();
        return true;
    }

    private bool IsActorCollider(Collider2D candidate)
    {
        if (candidate == null || actor == null)
            return false;

        Transform candidateTransform = candidate.transform;
        if (candidateTransform != actor.transform && !candidateTransform.IsChildOf(actor.transform))
            return false;

        return candidate.gameObject.CompareTag("Player") &&
               string.Equals(LayerMask.LayerToName(candidate.gameObject.layer), "Actor");
    }

    private bool IsTouchingAnyTip(Collider2D actorCollider)
    {
        if (actorCollider == null || !actorCollider.enabled || tipColliders == null)
            return false;

        for (int i = 0; i < tipColliders.Length; i++)
        {
            Collider2D tipCollider = tipColliders[i];
            if (tipCollider == null || !tipCollider.enabled || !tipCollider.gameObject.activeInHierarchy)
                continue;

            ColliderDistance2D distance = tipCollider.Distance(actorCollider);
            if (distance.isValid && distance.isOverlapped)
                return true;
        }

        return false;
    }

    private Vector3 CalculateActorContactPoint(Collider2D actorCollider)
    {
        Vector2 actorCenter = actorCollider.bounds.center;
        Collider2D nearestTip = null;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; tipColliders != null && i < tipColliders.Length; i++)
        {
            Collider2D tip = tipColliders[i];
            if (tip == null || !tip.enabled || !tip.gameObject.activeInHierarchy)
                continue;

            ColliderDistance2D distance = tip.Distance(actorCollider);
            if (!distance.isValid)
                continue;

            float candidateDistance = distance.isOverlapped ? 0f : distance.distance;
            if (candidateDistance >= nearestDistance)
                continue;

            nearestDistance = candidateDistance;
            nearestTip = tip;
            if (distance.isOverlapped)
                break;
        }

        if (nearestTip == null)
            return new Vector3(actorCenter.x, actorCenter.y, transform.position.z);

        Vector2 actorPoint = actorCollider.ClosestPoint(nearestTip.bounds.center);
        Vector2 tipPoint = nearestTip.ClosestPoint(actorPoint);
        Vector2 contactPoint = (actorPoint + tipPoint) * 0.5f;
        return new Vector3(contactPoint.x, contactPoint.y, transform.position.z);
    }

    private async UniTaskVoid DispatchTrapFailure()
    {
        if (trapDelay > 0f)
        {
            bool canceled = await UniTask.WaitForSeconds(
                    trapDelay,
                    cancellationToken: this.GetCancellationTokenOnDestroy())
                .SuppressCancellationThrow();
            if (canceled || this == null)
                return;
        }

        this.DispatchEvent(Witness<OnTriggerTrapEvent>._);
    }

    private bool IsGrounded()
    {
        if (supportCollider == null)
            return false;

        Bounds bounds = supportCollider.bounds;
        Vector2 probeSize = new Vector2(Mathf.Max(0.05f, bounds.size.x * 0.9f), 0.05f);
        Vector2 origin = new Vector2(bounds.center.x, bounds.min.y + 0.02f);
        RaycastHit2D hit = Physics2D.BoxCast(
            origin,
            probeSize,
            0f,
            Vector2.down,
            Mathf.Max(0.01f, groundProbeDistance),
            supportLayers);
        return hit.collider != null;
    }

    private void ResolveReferences()
    {
        if (body == null)
            body = GetComponent<Rigidbody2D>();
        if (supportCollider == null)
        {
            Collider2D[] colliders = GetComponents<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && !colliders[i].isTrigger)
                {
                    supportCollider = colliders[i];
                    break;
                }
            }
        }
        if (tipColliders == null || tipColliders.Length == 0)
        {
            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
            int count = 0;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].isTrigger)
                    count++;
            }

            tipColliders = new Collider2D[count];
            int index = 0;
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].isTrigger)
                    tipColliders[index++] = colliders[i];
            }
        }
        if (actor == null)
        {
            Game.Level.BaseLevelCtrl baseLevel = GetComponentInParent<Game.Level.BaseLevelCtrl>();
            if (baseLevel != null)
                actor = baseLevel.Actor;
        }
        if (levelCtrl == null)
            levelCtrl = GetComponentInParent<Level130Ctrl>();
        if (spikeVisuals == null)
            spikeVisuals = GetComponentsInChildren<SpriteRenderer>(true);
        if (alignVisualsOnStonePlatforms && authoredVisualPositions == null)
        {
            authoredVisualPositions = new Vector3[spikeVisuals.Length];
            for (int i = 0; i < spikeVisuals.Length; i++)
                authoredVisualPositions[i] = spikeVisuals[i].transform.localPosition;
            if (levelCtrl != null)
                stonePlatforms = levelCtrl.GetComponentsInChildren<Level130AutomaticPlatform>(true);
        }
    }
}
