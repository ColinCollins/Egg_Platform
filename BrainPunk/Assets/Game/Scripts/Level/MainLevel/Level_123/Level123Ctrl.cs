using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level123 专属机关与纸片角色流程。
/// 075 的按钮、门和刺墙资源只作为 Prefab 基线复制，运行时状态均在本类内维护。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level123Ctrl : MonoBehaviour
{
    private const int DoorDragCastHitCapacity = 8;
    private const float DoorDragCollisionSkin = 0.01f;

    [Header("场景引用")]
    [SerializeField] private BaseLevelCtrl baseLevel;
    [SerializeField] private Transform ceiling;
    [SerializeField] private Transform door;
    [SerializeField] private DragableItem dragItem;
    [SerializeField, Tooltip("门横向拖拽时用于阻止穿过齿墙的实体碰撞面。")]
    private Collider2D doorDragBarrier;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private SpriteRenderer targetFrame;
    [SerializeField] private Transform worldUpButton;
    [SerializeField] private Level123WorldButtonClick worldUpButtonClick;

    [Header("墙体动作")]
    [SerializeField] private float wallLift = 0.35f;
    [SerializeField, Min(0f)] private float wallLiftDuration = 0.22f;
    [SerializeField, Min(0f)] private float wallShakeDelay = 0.05f;
    [SerializeField, Min(0f)] private float wallShakeDuration = 0.22f;
    [SerializeField, Min(0f)] private float wallShakeStrength = 0.12f;
    [SerializeField, Min(1)] private int wallShakeVibrato = 18;
    [SerializeField] private float wallFinalY = -3.15f;
    [SerializeField, Min(0.05f)] private float wallFallDuration = 0.72f;

    [Header("开场墙体下降")]
    [SerializeField] private float ceilingDownY = -3.15f;
    [SerializeField, Min(0f)] private float ceilingDownDuration = 2.5f;
    [SerializeField, Min(0f)] private float ceilingStartDelay = 2f;

    [Header("按钮")]
    [SerializeField, Min(0f)] private float targetTolerance = 0.5f;
    [SerializeField, Min(0f)] private float initButtonScaleDuration = 0.3f;
    [SerializeField, Min(0f)] private float initButtonDelayOffset = 0.8f;
    [SerializeField, Min(0f)] private float initButtonDelayStep = 0.06f;

    [Header("门碎反馈")]
    [SerializeField] private Sprite crushedDoorSprite;
    [SerializeField] private ParticleSystem doorBrokenVfxPrefab;
    [SerializeField] private Transform doorVisual;
    [SerializeField] private Transform crushedDoorSpawnPoint;
    [SerializeField] private float crushedDoorGroundY = -2.365f;
    [SerializeField, Min(0.01f)] private float cameraShakeDuration = 0.25f;
    [SerializeField, Min(0f)] private float cameraShakeStrength = 0.18f;
    [SerializeField, Min(1)] private int cameraShakeVibrato = 20;

    [Header("纸片角色")]
    [SerializeField] private ActorCtrl actor;
    [SerializeField] private SpriteRenderer paperRenderer;
    [SerializeField] private Collider2D paperCollider;
    [SerializeField] private ShadowController shadowController;
    [SerializeField] private ParticleSystem compressionSmokePrefab;
    [SerializeField, Min(0.01f)] private float squeezeWideDuration = 0.18f;
    [SerializeField, Min(0.01f)] private float squeezeFlatDuration = 0.28f;
    [SerializeField] private Vector3 squeezeWideScale = new Vector3(1.18f, 0.82f, 1f);
    [SerializeField] private Vector3 squeezeFlatScale = new Vector3(1.08f, 0.55f, 1f);
    [SerializeField, Min(0f)] private float smokeDelay = 0.02f;
    [SerializeField] private Vector3 smokeScale = new Vector3(1.6f, 1.6f, 1f);
    [SerializeField, Min(0.01f)] private float paperMoveSpeed = 2.7f;
    [SerializeField, Min(0.01f)] private float paperAcceleration = 8f;
    [SerializeField, Min(0.01f)] private float paperBraking = 2.2f;
    [SerializeField] private float paperGravityScale = 0.65f;
    [SerializeField, Min(0f)] private float paperLinearDamping = 0.15f;

    [Header("时序")]
    [SerializeField, Min(0f)] private float doorDragDelay = 1.5f;

    private readonly List<Collider2D> actorColliders = new List<Collider2D>();
    private readonly List<bool> actorColliderStates = new List<bool>();
    private readonly List<Renderer> actorRenderers = new List<Renderer>();
    private readonly List<bool> actorRendererStates = new List<bool>();
    private readonly List<Collider2D> lethalSpikeColliders = new List<Collider2D>();
    private readonly RaycastHit2D[] doorDragCastHits = new RaycastHit2D[DoorDragCastHitCapacity];

    private Vector3 worldUpButtonInitialScale = Vector3.one;
    private Vector3 actorSpineInitialScale = Vector3.one;
    private Vector3 actorInitialScale = Vector3.one;
    private Vector2 actorInitialVelocity;
    private Vector2 actorVelocityBeforeCrush;
    private float actorInitialGravityScale;
    private float actorInitialLinearDamping;
    private float actorInitialMoveSpeed;
    private float actorInitialJumpForce;
    private bool actorInitialSimulated;
    private bool paperInitialActive;
    private bool paperInitialColliderEnabled;
    private int paperInitialLayer;
    private string paperInitialTag;
    private bool paperInitialLayerCaptured;
    private bool upButtonInstalled;
    private bool mechanismActivated;
    private bool doorCrushed;
    private bool actorCrushed;
    private bool paperActor;
    private bool paperSpikeCollisionsIgnored;
    private bool ceilingDescending;
    private bool cleanedUp;
    private bool referencesResolved;
    private bool doorDragCollisionInitialized;
    private bool doorDragTracking;
    private float paperVelocityX;
    private Vector3 doorLastAllowedPosition;
    private Collider2D doorDragCollider;
    private ContactFilter2D doorDragContactFilter;
    private Transform actorSpine;
    private bool shadowInitialActive;
    private bool shadowInitialActiveCaptured;
    private Transform cameraShakeTarget;
    private Vector3 cameraInitialLocalPosition;
    private Tween cameraShakeTween;
    private Tween worldUpButtonIntroTween;
    private Tween ceilingIntroTween;
    private Sequence wallSequence;
    private Sequence doorCrushSequence;
    private Sequence actorSqueezeSequence;
    private FieldInfo jumpForceField;
    private CustomButton sharedJumpButton;
    private GameObject sharedJumpButtonSlotObject;
    private bool sharedJumpButtonSlotInitialActive;
    private bool sharedJumpButtonInitialEnabled;
    private bool sharedJumpButtonInitialInteractable;
    private bool sharedJumpButtonDisabled;
    private GameObject legacyUpButtonSlotObject;
    private bool legacyUpButtonSlotInitialActive;

    /// <summary>按钮是否已吸附到红框；吸附后只允许点击机关，不再进入拖拽。</summary>
    public bool IsWorldUpButtonInstalled => upButtonInstalled;

    /// <summary>角色是否已经切换为纸片状态。</summary>
    public bool IsPaperActor => paperActor;

    /// <summary>只有墙体实际向下运动时，绿色无刺压区才能把普通角色压成纸片。</summary>
    public bool CanCrushActor => ceilingDescending && !paperActor;

    private void Awake()
    {
        if (baseLevel == null)
            baseLevel = GetComponent<BaseLevelCtrl>();
        ResolveActorReferences();
        CacheLethalSpikeColliders();
        InitializeDoorDragCollision();

        if (worldUpButton != null)
        {
            worldUpButtonInitialScale = worldUpButton.localScale;
            worldUpButton.localScale = Vector3.zero;
        }

        if (paperRenderer != null)
        {
            paperInitialActive = paperRenderer.gameObject.activeSelf;
            paperRenderer.gameObject.SetActive(false);
        }

        if (paperCollider != null)
        {
            paperInitialColliderEnabled = paperCollider.enabled;
            paperInitialLayer = paperCollider.gameObject.layer;
            paperInitialTag = paperCollider.gameObject.tag;
            paperInitialLayerCaptured = true;
            paperCollider.enabled = false;
        }
    }

    private void Start()
    {
        ResolveActorReferences();
        PrepareWorldUpButton();
        StartCeilingIntro();
        StartCoroutine(WaitForActorReady());
        StartCoroutine(InitializeWorldUpButton());
        Invoke(nameof(OpenDragDoor), doorDragDelay);
    }

    private void StartCeilingIntro()
    {
        if (ceiling == null)
            return;

        ceiling.DOKill();
        ceilingIntroTween = ceiling
            .DOMoveY(ceilingDownY, Mathf.Max(0f, ceilingDownDuration))
            .SetDelay(Mathf.Max(0f, ceilingStartDelay))
            .SetUpdate(true)
            .SetEase(Ease.Linear)
            .SetLink(gameObject)
            .OnStart(BeginCeilingDescent)
            .OnComplete(FinishCeilingIntro)
            .OnKill(FinishCeilingIntro);
    }

    private void FinishCeilingIntro()
    {
        EndCeilingDescent();
        ceilingIntroTween = null;
    }

    private IEnumerator WaitForActorReady()
    {
        for (int attempt = 0; attempt < 30; attempt++)
        {
            ResolveActorReferences();
            if (actor != null && actor.RB != null && actorSpine != null)
                yield break;
            yield return null;
        }
    }

    private void Update()
    {
        if (cleanedUp || paperActor || actor == null || actor.RB == null)
            return;

        if (actorCrushed)
        {
            actor.SetMoveInput(0f);
            actor.StopMoving();
            if (actor.RB.simulated)
                actor.RB.simulated = false;
        }
    }

    private void LateUpdate()
    {
        ConstrainDoorDragAgainstBarrier();

        if (!paperActor || actor == null || actor.RB == null || !actor.RB.simulated)
            return;

        // 暂停、成功或切出关卡时不再用上一帧输入写回纸片速度。
        if (!IsGamePlaying() || actor.IsDied ||
            (baseLevel != null &&
             (baseLevel.IsActorEnterDoor || baseLevel.IsSuccess() || baseLevel.IsFail())))
        {
            paperVelocityX = 0f;
            Vector2 stoppedVelocity = actor.RB.linearVelocity;
            stoppedVelocity.x = 0f;
            actor.RB.linearVelocity = stoppedVelocity;
            return;
        }

        float input = Mathf.Clamp(actor.XInput, -1f, 1f);
        float targetVelocity = input * paperMoveSpeed;
        float rate = Mathf.Abs(input) > 0.01f ? paperAcceleration : paperBraking;
        paperVelocityX = Mathf.MoveTowards(paperVelocityX, targetVelocity, rate * Time.deltaTime);

        Vector2 velocity = actor.RB.linearVelocity;
        velocity.x = paperVelocityX;
        if (velocity.y > 0f)
            velocity.y = 0f;
        actor.RB.linearVelocity = velocity;
    }

    private void InitializeDoorDragCollision()
    {
        if (doorDragCollisionInitialized || door == null || doorDragBarrier == null)
            return;

        doorDragCollider = door.GetComponent<Collider2D>();
        if (doorDragCollider == null)
            return;

        doorDragContactFilter = new ContactFilter2D
        {
            useTriggers = false
        };
        doorDragContactFilter.SetLayerMask(1 << doorDragBarrier.gameObject.layer);
        doorLastAllowedPosition = door.position;
        doorDragCollisionInitialized = true;
    }

    private void ConstrainDoorDragAgainstBarrier()
    {
        if (cleanedUp || doorCrushed || door == null || dragItem == null || doorDragBarrier == null)
        {
            doorDragTracking = false;
            return;
        }

        InitializeDoorDragCollision();
        if (!doorDragCollisionInitialized || doorDragCollider == null || !doorDragCollider.enabled ||
            !doorDragBarrier.enabled || !doorDragBarrier.gameObject.activeInHierarchy)
        {
            doorDragTracking = false;
            return;
        }

        bool isDragging = dragItem.enabled && dragItem.IsDragging();
        if (!isDragging)
        {
            doorDragTracking = false;
            doorLastAllowedPosition = door.position;
            return;
        }

        if (!doorDragTracking)
        {
            doorDragTracking = true;
            doorLastAllowedPosition = door.position;
            return;
        }

        Vector3 proposedPosition = door.position;
        Vector2 movement = (Vector2)(proposedPosition - doorLastAllowedPosition);
        float requestedDistance = movement.magnitude;
        if (requestedDistance <= Mathf.Epsilon)
            return;

        // DragableItem 直接写 Transform；先回到安全点同步物理世界，再扫掠本帧整段位移。
        door.position = doorLastAllowedPosition;
        Physics2D.SyncTransforms();

        Vector2 direction = movement / requestedDistance;
        float allowedDistance = requestedDistance;
        int hitCount = doorDragCollider.Cast(
            direction,
            doorDragContactFilter,
            doorDragCastHits,
            requestedDistance + DoorDragCollisionSkin);
        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit2D hit = doorDragCastHits[index];
            if (hit.collider != doorDragBarrier || hit.distance < 0f ||
                Vector2.Dot(direction, hit.normal) >= -0.001f)
            {
                continue;
            }

            allowedDistance = Mathf.Min(
                allowedDistance,
                Mathf.Max(0f, hit.distance - DoorDragCollisionSkin));
        }

        Vector3 resolvedPosition = doorLastAllowedPosition + (Vector3)(direction * allowedDistance);
        resolvedPosition.z = proposedPosition.z;
        door.position = resolvedPosition;
        Physics2D.SyncTransforms();
        doorLastAllowedPosition = resolvedPosition;
    }

    private void ResolveActorReferences()
    {
        if (referencesResolved && actor != null && actor.RB != null && actorSpine != null)
            return;

        if (actor == null && baseLevel != null)
            actor = baseLevel.Actor;
        if (actor == null)
            actor = GetComponentInChildren<ActorCtrl>(true);
        if (actor == null)
            return;

        Transform resolvedSpine = actor.Body != null ? actor.Body : actor.transform.Find("spine");
        if (actor.RB == null || resolvedSpine == null)
            return;

        actorSpine = resolvedSpine;
        actorSpineInitialScale = actorSpine.localScale;
        actorInitialScale = actor.transform.localScale;
        actorInitialMoveSpeed = actor.MoveSpeed;

        actorInitialGravityScale = actor.RB.gravityScale;
        actorInitialLinearDamping = actor.RB.linearDamping;
        actorInitialSimulated = actor.RB.simulated;
        actorInitialVelocity = actor.RB.linearVelocity;
        jumpForceField = typeof(ActorCtrl).GetField(
            "jumpForce", BindingFlags.Instance | BindingFlags.NonPublic);
        if (jumpForceField != null)
            actorInitialJumpForce = (float)jumpForceField.GetValue(actor);

        if (shadowController == null)
            shadowController = actor.GetComponentInChildren<ShadowController>(true);
        if (shadowController != null && !shadowInitialActiveCaptured)
        {
            shadowInitialActive = shadowController.gameObject.activeSelf;
            shadowInitialActiveCaptured = true;
        }

        actorColliders.Clear();
        actorColliderStates.Clear();
        Collider2D[] colliders = actor.GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D collider in colliders)
        {
            if (collider == null || collider == paperCollider)
                continue;
            actorColliders.Add(collider);
            actorColliderStates.Add(collider.enabled);
        }

        actorRenderers.Clear();
        actorRendererStates.Clear();
        if (actorSpine != null)
        {
            Renderer[] renderers = actorSpine.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;
                actorRenderers.Add(renderer);
                actorRendererStates.Add(renderer.enabled);
            }
        }

        referencesResolved = true;
    }

    private void CacheLethalSpikeColliders()
    {
        lethalSpikeColliders.Clear();
        if (ceiling == null)
            return;

        OnTriggerTrapHandle[] spikeHandlers = ceiling.GetComponentsInChildren<OnTriggerTrapHandle>(true);
        foreach (OnTriggerTrapHandle spikeHandler in spikeHandlers)
        {
            if (spikeHandler == null)
                continue;

            Collider2D spikeCollider = spikeHandler.GetComponent<Collider2D>();
            if (spikeCollider != null && !lethalSpikeColliders.Contains(spikeCollider))
                lethalSpikeColliders.Add(spikeCollider);
        }
    }

    private bool IsActorOverlappingLethalSpike()
    {
        for (int actorIndex = 0; actorIndex < actorColliders.Count; actorIndex++)
        {
            Collider2D actorCollider = actorColliders[actorIndex];
            if (actorCollider == null || !actorCollider.enabled || !actorCollider.gameObject.activeInHierarchy)
                continue;

            for (int spikeIndex = 0; spikeIndex < lethalSpikeColliders.Count; spikeIndex++)
            {
                Collider2D spikeCollider = lethalSpikeColliders[spikeIndex];
                if (spikeCollider == null || !spikeCollider.enabled ||
                    !spikeCollider.gameObject.activeInHierarchy)
                    continue;

                ColliderDistance2D distance = actorCollider.Distance(spikeCollider);
                if (distance.isValid && distance.isOverlapped)
                    return true;
            }
        }

        return false;
    }

    private void PrepareWorldUpButton()
    {
        upButtonInstalled = false;
        worldUpButtonClick?.SetEnableDrag(false);
        worldUpButtonClick?.SetEnableClick(false);
        if (worldUpButtonClick != null)
            worldUpButtonClick.enabled = false;

        if (worldUpButton != null)
        {
            worldUpButton.DOKill();
            worldUpButton.localScale = Vector3.zero;
            worldUpButton.gameObject.SetActive(false);
        }
    }

    private IEnumerator InitializeWorldUpButton()
    {
        yield return new WaitForSecondsRealtime(0.5f);

        RectTransform jumpButtonSlot = null;
        for (int attempt = 0; attempt < 20 && jumpButtonSlot == null; attempt++)
        {
            jumpButtonSlot = FindButtonSlot("jump_btn");
            if (jumpButtonSlot == null)
                yield return new WaitForSecondsRealtime(0.1f);
        }

        Camera cameraToUse = worldCamera != null ? worldCamera : Camera.main;
        if (jumpButtonSlot == null || cameraToUse == null || worldUpButton == null || worldUpButtonClick == null)
        {
            Debug.LogWarning("[Level123Ctrl] World jump button or GamePanel jump_btn slot was not found.", this);
            yield break;
        }

        float buttonZ = targetFrame != null ? targetFrame.transform.position.z : 0f;
        worldUpButton.position = CommonUtility.ConvertUIToWorldPosition(jumpButtonSlot, cameraToUse, buttonZ);
        DisableSharedJumpButton(jumpButtonSlot);
        sharedJumpButtonSlotObject = jumpButtonSlot.gameObject;
        sharedJumpButtonSlotInitialActive = jumpButtonSlot.gameObject.activeSelf;
        jumpButtonSlot.gameObject.SetActive(false);

        RectTransform legacySlot = FindButtonSlot("up_btn");
        if (legacySlot != null)
        {
            legacyUpButtonSlotObject = legacySlot.gameObject;
            legacyUpButtonSlotInitialActive = legacySlot.gameObject.activeSelf;
            legacySlot.gameObject.SetActive(false);
        }

        worldUpButton.gameObject.SetActive(true);
        worldUpButton.localScale = Vector3.zero;
        float introDelay = Mathf.Max(0f, initButtonDelayOffset) + 2f * Mathf.Max(0f, initButtonDelayStep);
        bool complete = false;
        worldUpButtonIntroTween = worldUpButton.DOScale(worldUpButtonInitialScale,
                Mathf.Max(0f, initButtonScaleDuration))
            .SetDelay(introDelay)
            .SetUpdate(true)
            .SetEase(Ease.OutBack)
            .SetLink(worldUpButton.gameObject)
            .OnComplete(() => complete = true);

        while (!complete && worldUpButtonIntroTween != null && worldUpButtonIntroTween.IsActive())
            yield return null;
        worldUpButtonIntroTween = null;

        while (!IsGamePlaying())
            yield return null;

        worldUpButtonClick.enabled = true;
        yield return null;
        worldUpButtonClick.SetEnableClick(true);
        worldUpButtonClick.SetEnableDrag(true);
    }

    private static RectTransform FindButtonSlot(string buttonName)
    {
        if (PlayCtrl.Instance == null || PlayCtrl.Instance.CurrentGamePlayPanel == null)
            return null;

        Transform panel = PlayCtrl.Instance.CurrentGamePlayPanel.transform;
        Transform slot = panel.Find($"Root/{buttonName}");
        if (slot == null && panel.childCount > 0)
            slot = panel.GetChild(0).Find(buttonName);
        return slot as RectTransform;
    }

    private void DisableSharedJumpButton(RectTransform slot)
    {
        if (sharedJumpButtonDisabled || slot == null)
            return;

        sharedJumpButton = slot.GetComponent<CustomButton>();
        if (sharedJumpButton == null)
            sharedJumpButton = slot.GetComponentInChildren<CustomButton>(true);
        if (sharedJumpButton == null)
        {
            // 槽位可能只包含图形而没有 CustomButton；仍记录并恢复 GameObject 可见性。
            sharedJumpButtonSlotObject = slot.gameObject;
            sharedJumpButtonSlotInitialActive = slot.gameObject.activeSelf;
            sharedJumpButtonDisabled = true;
            return;
        }

        sharedJumpButtonSlotObject = slot.gameObject;
        sharedJumpButtonSlotInitialActive = slot.gameObject.activeSelf;
        sharedJumpButtonInitialEnabled = sharedJumpButton.enabled;
        sharedJumpButtonInitialInteractable = sharedJumpButton.Interactable;
        sharedJumpButton.Interactable = false;
        sharedJumpButton.enabled = false;
        sharedJumpButtonDisabled = true;
    }

    private void RestoreSharedJumpButton()
    {
        if (!sharedJumpButtonDisabled)
            return;

        if (sharedJumpButton != null)
        {
            sharedJumpButton.Interactable = sharedJumpButtonInitialInteractable;
            sharedJumpButton.enabled = sharedJumpButtonInitialEnabled;
        }
        if (sharedJumpButtonSlotObject != null)
            sharedJumpButtonSlotObject.SetActive(sharedJumpButtonSlotInitialActive);
        sharedJumpButton = null;
        sharedJumpButtonSlotObject = null;
        sharedJumpButtonDisabled = false;
    }

    private void RestoreLegacyUpButton()
    {
        if (legacyUpButtonSlotObject == null)
            return;

        legacyUpButtonSlotObject.SetActive(legacyUpButtonSlotInitialActive);
        legacyUpButtonSlotObject = null;
    }

    /// <summary>在按钮松手时按容差检查红框，命中后吸附并锁定拖拽。</summary>
    public void TryInstallWorldUpButton()
    {
        if (!IsGamePlaying() || upButtonInstalled || targetFrame == null || worldUpButton == null)
            return;

        Bounds bounds = targetFrame.bounds;
        bounds.Expand(Mathf.Max(0f, targetTolerance) * 2f);
        Vector3 position = worldUpButton.position;
        if (!bounds.Contains(new Vector3(position.x, position.y, bounds.center.z)))
            return;

        upButtonInstalled = true;
        Vector3 snapped = targetFrame.bounds.center;
        snapped.z = position.z;
        worldUpButton.SetParent(targetFrame.transform, true);
        worldUpButton.position = snapped;
        worldUpButtonClick?.SetEnableDrag(false);
        worldUpButtonClick?.SetEnableClick(true);
    }

    /// <summary>处理已吸附按钮的点击，只启动一次本关刺墙动作序列。</summary>
    public void ClickWorldUpButton()
    {
        if (!upButtonInstalled || mechanismActivated || paperActor)
            return;
        mechanismActivated = true;
        StartWallSequence();
    }

    private void StartWallSequence()
    {
        if (ceiling == null)
            return;

        if (ceilingIntroTween != null && ceilingIntroTween.IsActive())
            ceilingIntroTween.Kill();
        ceilingIntroTween = null;
        ceiling.DOKill();
        wallSequence?.Kill();
        EndCeilingDescent();
        Vector3 startPosition = ceiling.position;
        Vector3 liftPosition = startPosition + Vector3.up * wallLift;
        wallSequence = DOTween.Sequence()
            .Append(ceiling.DOMove(liftPosition, Mathf.Max(0f, wallLiftDuration)).SetEase(Ease.OutQuad))
            .AppendInterval(Mathf.Max(0f, wallShakeDelay))
            .Append(ceiling.DOShakePosition(Mathf.Max(0f, wallShakeDuration), wallShakeStrength,
                Mathf.Max(1, wallShakeVibrato), 90f, false, true))
            .AppendCallback(BeginCeilingDescent)
            .Append(ceiling.DOMoveY(wallFinalY, Mathf.Max(0.05f, wallFallDuration)).SetEase(Ease.InCubic))
            .AppendCallback(EndCeilingDescent)
            .SetUpdate(true)
            .SetLink(gameObject);
        wallSequence.OnComplete(FinishWallSequence);
        wallSequence.OnKill(FinishWallSequence);
        AudioManager.PlaySound("button");
    }

    private void BeginCeilingDescent()
    {
        ceilingDescending = true;
    }

    private void EndCeilingDescent()
    {
        ceilingDescending = false;
    }

    private void FinishWallSequence()
    {
        EndCeilingDescent();
        wallSequence = null;
    }

    private void OpenDragDoor()
    {
        if (!doorCrushed && dragItem != null)
            dragItem.enabled = true;
    }

    /// <summary>
    /// 由绿色无刺压区转发碰撞；仅在墙体下坠期间处理本关角色，其它对象直接忽略。
    /// </summary>
    public void TryCrushActorFromGreenZone(Collider2D target)
    {
        if (cleanedUp || !ceilingDescending || target == null)
            return;

        ResolveActorReferences();
        ActorCtrl hitActor = target.GetComponentInParent<ActorCtrl>();
        if (hitActor != null && hitActor == actor)
        {
            // 角色横跨绿色区和相邻刺时，必须让共享刺死亡回调优先，不能先暂停刚体进入纸片流程。
            if (IsActorOverlappingLethalSpike())
                return;

            TryCrushActor();
        }
    }

    /// <summary>
    /// 门接触下降墙体的任意子碰撞体时压碎；绿色无刺区与各段刺均属于该墙层级。
    /// </summary>
    public void TryCrushDoorFromWall(Collider2D wallCollider)
    {
        if (cleanedUp || !ceilingDescending || wallCollider == null || ceiling == null)
            return;

        Transform hitTransform = wallCollider.transform;
        if (hitTransform != ceiling && !hitTransform.IsChildOf(ceiling))
            return;

        CrushDoor();
    }

    private void CrushDoor()
    {
        if (!ceilingDescending || doorCrushed || door == null)
            return;

        doorCrushed = true;
        BoxCollider2D collider = door.GetComponent<BoxCollider2D>();
        Vector3 effectPosition = collider != null ? collider.bounds.center : door.position;
        if (dragItem != null)
            dragItem.enabled = false;
        foreach (Collider2D doorCollider in door.GetComponentsInChildren<Collider2D>(true))
        {
            if (doorCollider != null)
                doorCollider.enabled = false;
        }

        Transform visual = doorVisual != null ? doorVisual : door;
        if (visual == null)
        {
            CompleteDoorCrush(effectPosition);
            return;
        }

        visual.DOKill();
        Vector3 initial = visual.localScale;
        doorCrushSequence = DOTween.Sequence()
            .Append(visual.DOScale(Vector3.Scale(initial, new Vector3(1.12f, 0.82f, 1f)), 0.26f).SetEase(Ease.InQuad))
            .Append(visual.DOScale(Vector3.Scale(initial, new Vector3(1.25f, 0.55f, 1f)), 0.36f).SetEase(Ease.Linear))
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(() => CompleteDoorCrush(effectPosition));
    }

    private void CompleteDoorCrush(Vector3 effectPosition)
    {
        doorCrushSequence = null;
        if (cleanedUp || door == null)
            return;
        CreateCrushedDoor();
        PlayDoorBrokenVfx(effectPosition);
        door.gameObject.SetActive(false);
        PlayCameraShake();
        AudioManager.PlaySound("blockByDoor");
    }

    private void CreateCrushedDoor()
    {
        if (crushedDoorSprite == null)
            return;

        Vector3 position;
        Quaternion rotation;
        if (crushedDoorSpawnPoint != null)
        {
            position = crushedDoorSpawnPoint.position;
            rotation = crushedDoorSpawnPoint.rotation;
        }
        else
        {
            position = door.position;
            position.y = crushedDoorGroundY - crushedDoorSprite.bounds.min.y;
            rotation = Quaternion.identity;
        }

        GameObject crushedDoor = new GameObject("CrushedDoor_123");
        crushedDoor.transform.SetParent(transform, true);
        crushedDoor.transform.SetPositionAndRotation(position, rotation);
        SpriteRenderer renderer = crushedDoor.AddComponent<SpriteRenderer>();
        renderer.sprite = crushedDoorSprite;
        if (targetFrame != null)
        {
            renderer.sortingLayerID = targetFrame.sortingLayerID;
            renderer.sortingOrder = targetFrame.sortingOrder + 1;
        }
        else
        {
            renderer.sortingLayerName = "SceneLayer";
            renderer.sortingOrder = 201;
        }
    }

    private void PlayDoorBrokenVfx(Vector3 position)
    {
        if (doorBrokenVfxPrefab == null)
            return;
        ParticleSystem instance = Instantiate(doorBrokenVfxPrefab, position,
            doorBrokenVfxPrefab.transform.rotation, transform);
        instance.gameObject.SetActive(true);
        instance.Play(true);
    }

    private void PlayCameraShake()
    {
        ResolveCameraShakeTarget();
        if (cameraShakeTarget == null || cameraShakeStrength <= 0f)
            return;
        cameraShakeTween?.Kill();
        cameraShakeTarget.localPosition = cameraInitialLocalPosition;
        cameraShakeTween = cameraShakeTarget.DOShakePosition(
                Mathf.Max(0.01f, cameraShakeDuration), cameraShakeStrength,
                Mathf.Max(1, cameraShakeVibrato), 90f, false, true)
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(RestoreCameraPosition)
            .OnKill(RestoreCameraPosition);
    }

    private void ResolveCameraShakeTarget()
    {
        if (cameraShakeTarget != null)
            return;
        Camera cameraToUse = worldCamera != null ? worldCamera : Camera.main;
        if (cameraToUse == null)
            return;
        cameraShakeTarget = cameraToUse.transform;
        cameraInitialLocalPosition = cameraShakeTarget.localPosition;
    }

    private void RestoreCameraPosition()
    {
        if (cameraShakeTarget != null)
            cameraShakeTarget.localPosition = cameraInitialLocalPosition;
        cameraShakeTween = null;
    }

    private void TryCrushActor()
    {
        if (cleanedUp || actorCrushed || paperActor || !CanCrushActor)
            return;
        ResolveActorReferences();
        if (actor == null || actor.RB == null || actor.IsDied)
            return;

        actorCrushed = true;
        baseLevel?.SetCtrlable(false);
        worldUpButtonClick?.SetEnableDrag(false);
        worldUpButtonClick?.SetEnableClick(false);
        if (actor is Level123ActorCtrl level123Actor)
            level123Actor.SetJumpLocked(true);
        actor.SetJumpForce(0f);
        actor.SetMoveInput(0f);
        actor.StopMoving();
        actorVelocityBeforeCrush = actor.RB.linearVelocity;
        actor.RB.simulated = false;

        if (actorSpine != null)
        {
            actorSpine.DOKill();
            actorSqueezeSequence = DOTween.Sequence()
                .Append(actorSpine.DOScale(Vector3.Scale(actorSpineInitialScale, squeezeWideScale),
                    Mathf.Max(0.01f, squeezeWideDuration)).SetEase(Ease.OutQuad))
                .Append(actorSpine.DOScale(Vector3.Scale(actorSpineInitialScale, squeezeFlatScale),
                    Mathf.Max(0.01f, squeezeFlatDuration)).SetEase(Ease.InQuad))
                .SetUpdate(true)
                .SetLink(gameObject)
                .OnComplete(() => CompleteActorCrush());
        }
        else
        {
            CompleteActorCrush();
        }
    }

    private void CompleteActorCrush()
    {
        actorSqueezeSequence = null;
        if (cleanedUp || actor == null || actor.RB == null || actor.IsDied)
            return;

        PlayCompressionSmoke();
        if (smokeDelay > 0f)
            StartCoroutine(EnterPaperAfterDelay(smokeDelay));
        else
            EnterPaperState();
    }

    private IEnumerator EnterPaperAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        EnterPaperState();
    }

    private void EnterPaperState()
    {
        if (cleanedUp || paperActor || actor == null || actor.RB == null || actor.IsDied)
            return;

        paperActor = true;
        if (actor is Level123ActorCtrl level123Actor)
            level123Actor.SetJumpLocked(true);
        paperVelocityX = 0f;
        actor.transform.localScale = actorInitialScale;
        if (actorSpine != null)
            actorSpine.localScale = actorSpineInitialScale;
        for (int index = 0; index < actorRenderers.Count; index++)
        {
            if (actorRenderers[index] != null)
                actorRenderers[index].enabled = false;
        }

        for (int index = 0; index < actorColliders.Count; index++)
        {
            if (actorColliders[index] != null)
                actorColliders[index].enabled = false;
        }

        if (paperRenderer != null)
        {
            paperRenderer.gameObject.SetActive(true);
            paperRenderer.enabled = true;
        }
        if (shadowController != null)
            shadowController.gameObject.SetActive(false);
        if (paperCollider != null)
        {
            DisableSharedJumpButton(FindButtonSlot("jump_btn"));
            paperCollider.gameObject.layer = LayerMask.NameToLayer("Actor");
            paperCollider.gameObject.tag = "Player";
            paperCollider.enabled = true;
        }

        SetPaperSpikeCollisionsIgnored(true);
        actor.RB.simulated = true;
        actor.RB.gravityScale = paperGravityScale;
        actor.RB.linearDamping = paperLinearDamping;
        actor.RB.linearVelocity = new Vector2(0f, Mathf.Min(0f, actorVelocityBeforeCrush.y));
        actor.SetMoveSpeed(paperMoveSpeed);
        actor.SetJumpForce(0f);
        actor.SetPreserveExternalHorizontalMomentum(false);
        actor.ResumeMoving();
        baseLevel?.SetCtrlable(true);
    }

    private void SetPaperSpikeCollisionsIgnored(bool ignored)
    {
        if (paperCollider == null || paperSpikeCollisionsIgnored == ignored)
            return;

        for (int index = 0; index < lethalSpikeColliders.Count; index++)
        {
            Collider2D spikeCollider = lethalSpikeColliders[index];
            if (spikeCollider != null)
                Physics2D.IgnoreCollision(paperCollider, spikeCollider, ignored);
        }

        paperSpikeCollisionsIgnored = ignored;
    }

    private void PlayCompressionSmoke()
    {
        if (compressionSmokePrefab == null || actor == null)
            return;
        ParticleSystem instance = Instantiate(compressionSmokePrefab, actor.transform.position,
            compressionSmokePrefab.transform.rotation, transform);
        instance.transform.localScale = smokeScale;
        ParticleSystemRenderer[] renderers = instance.GetComponentsInChildren<ParticleSystemRenderer>(true);
        int sortingLayerId = targetFrame != null ? targetFrame.sortingLayerID : 0;
        int sortingOrder = targetFrame != null ? targetFrame.sortingOrder + 20 : 220;
        foreach (ParticleSystemRenderer renderer in renderers)
        {
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder;
        }
        ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem system in systems)
        {
            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.useUnscaledTime = true;
        }

        instance.gameObject.SetActive(true);
        instance.Play(true);
    }

    private void Cleanup()
    {
        if (cleanedUp)
            return;
        cleanedUp = true;
        RestoreSharedJumpButton();
        RestoreLegacyUpButton();
        SetPaperSpikeCollisionsIgnored(false);
        StopAllCoroutines();
        CancelInvoke(nameof(OpenDragDoor));
        worldUpButtonClick?.SetEnableDrag(false);
        worldUpButtonClick?.SetEnableClick(false);
        if (worldUpButtonClick != null)
            worldUpButtonClick.enabled = false;

        wallSequence?.Kill();
        wallSequence = null;
        if (ceilingIntroTween != null && ceilingIntroTween.IsActive())
            ceilingIntroTween.Kill();
        ceilingIntroTween = null;
        EndCeilingDescent();
        lethalSpikeColliders.Clear();
        doorCrushSequence?.Kill();
        actorSqueezeSequence?.Kill();
        worldUpButtonIntroTween?.Kill();
        cameraShakeTween?.Kill();
        RestoreCameraPosition();

        if (worldUpButton != null)
        {
            worldUpButton.DOKill();
            worldUpButton.localScale = Vector3.zero;
            worldUpButton.gameObject.SetActive(false);
        }

        if (actor != null)
        {
            if (actor is Level123ActorCtrl level123Actor)
                level123Actor.SetJumpLocked(false);
            actor.transform.localScale = actorInitialScale;
            if (actorSpine != null)
                actorSpine.localScale = actorSpineInitialScale;
            for (int index = 0; index < actorRenderers.Count; index++)
            {
                if (actorRenderers[index] != null)
                    actorRenderers[index].enabled = actorRendererStates[index];
            }
            for (int index = 0; index < actorColliders.Count; index++)
            {
                if (actorColliders[index] != null)
                    actorColliders[index].enabled = actorColliderStates[index];
            }
            if (paperRenderer != null)
            {
                paperRenderer.enabled = paperInitialActive;
                paperRenderer.gameObject.SetActive(paperInitialActive);
            }
            if (paperCollider != null)
            {
                paperCollider.enabled = paperInitialColliderEnabled;
                if (paperInitialLayerCaptured)
                {
                    paperCollider.gameObject.layer = paperInitialLayer;
                    paperCollider.gameObject.tag = paperInitialTag;
                }
            }
            if (actor.RB != null)
            {
                actor.RB.simulated = actorInitialSimulated;
                actor.RB.gravityScale = actorInitialGravityScale;
                actor.RB.linearDamping = actorInitialLinearDamping;
                actor.RB.linearVelocity = actorInitialVelocity;
            }
            actor.SetMoveSpeed(actorInitialMoveSpeed);
            if (jumpForceField != null)
                jumpForceField.SetValue(actor, actorInitialJumpForce);
            actor.ResumeMoving();
        }
        if (shadowController != null && shadowInitialActiveCaptured)
            shadowController.gameObject.SetActive(shadowInitialActive);
    }

    private static bool IsGamePlaying()
    {
        return PlayCtrl.Instance != null &&
               PlayCtrl.Instance.CheckState(GamePlayStateName.PLAYING);
    }

    private void OnDisable() => Cleanup();
    private void OnDestroy() => Cleanup();
}
