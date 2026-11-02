using System;
using System.Collections;
using Game.Scripts.Common;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Level102 的箱子/残骸局部碰撞链。
/// 箱子不接受 DragableItem 输入；玩家接触箱子后隐藏箱体并显示残骸，
/// 残骸显示后默认只作为视觉残留，不再与角色发生二次互动。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level102BoxBreakOnActor : MonoBehaviour
{
    public enum BreakStage
    {
        Intact,
        Debris
    }

    [Header("阶段")]
    [Tooltip("Intact 挂在完整箱子上；Debris 由组件自动挂到残骸上。")]
    [SerializeField] private BreakStage stage = BreakStage.Intact;

    [Header("残骸（仅 Intact 使用）")]
    [Tooltip("第一次碎裂后显示的残骸对象；建议使用一个独立的 Sprite 子物体。")]
    [SerializeField] private GameObject debrisVisual;
    [Tooltip("残骸碰撞体；为空且允许自动补齐时，会在残骸对象上创建 BoxCollider2D。")]
    [SerializeField] private Collider2D debrisCollider;
    [Tooltip("没有 Collider 时是否按残骸 Sprite 自动创建碰撞体。")]
    [SerializeField] private bool addDebrisColliderIfMissing = true;
    [Tooltip("自动查找残骸时使用的对象名；查找范围为当前关卡根节点。")]
    [SerializeField] private string debrisObjectName = "Box_2";
    [Tooltip("第一次碎裂时是否把残骸的完整世界姿态对齐到箱子；关闭时仅跟随箱子当前 X，Y/Z、旋转和缩放保留场景初始值。")]
    [SerializeField] private bool alignDebrisToBox = true;
    [Tooltip("允许在运行时给残骸添加本组件，省去在复制关卡中手动补挂。")]
    [SerializeField] private bool addDebrisRelayAtRuntime = true;

    [Tooltip("残骸显示后是否仍允许与角色发生碰撞/二次碎裂；Level102 默认关闭。")]
    [SerializeField] private bool debrisInteractWithActor = false;

    [Header("碎裂反馈")]
    [Tooltip("Level007 的 BoxBroken 粒子模板；可绑定关卡内隐藏模板实例。")]
    [SerializeField] private ParticleSystem brokenEffectPrefab;
    [Tooltip("模板引用为空时，是否自动查找关卡内名称以 BoxBroken 开头的粒子实例。")]
    [SerializeField] private bool autoFindBrokenEffect = true;
    [Tooltip("特效实例的父节点；为空时使用当前关卡根节点。")]
    [SerializeField] private Transform effectParent;
    [Tooltip("播放的音效标签。")]
    [SerializeField] private string breakSoundTag = "crashBroke";
    [Tooltip("碎裂粒子相对实际接触点的世界坐标偏移；可把爆点从角色脚下下移。")]
    [SerializeField] private Vector2 breakEffectOffset = new Vector2(0f, -0.15f);
    [Tooltip("完整箱子第一次碎裂时的回调。")]
    [SerializeField] private UnityEvent onIntactBroken;
    [Tooltip("残骸第二次碎裂时的回调。")]
    [SerializeField] private UnityEvent onDebrisBroken;

    [Header("角色判定")]
    [Tooltip("完整箱子是否必须由角色从顶部踩到才触发；关闭后任意真实物理接触都会触发。")]
    [SerializeField] private bool requireTopContact = true;
    [Tooltip("残骸是否沿用顶部判定；默认关闭，角色碰到残骸任意位置都会触发二次碎裂。")]
    [SerializeField] private bool debrisRequireTopContact = false;
    [Tooltip("顶部接触判定允许的碰撞点误差（世界单位）。")]
    [SerializeField, Min(0f)] private float topContactTolerance = 0.25f;
    [Tooltip("顶部判定时角色中心至少高于箱体中心的距离。")]
    [SerializeField] private float actorCenterAboveOffset = 0.01f;
    [Tooltip("角色层；为空时自动使用名为 Actor 的层。")]
    [SerializeField] private LayerMask actorLayerMask;
    [Tooltip("角色标签；默认只接受 Player。")]
    [SerializeField] private string actorTag = "Player";

    [Header("物理与输入")]
    [Tooltip("可选的 Level102 局部控制器；绑定后在关卡尚未进入可玩状态、暂停或重置期间忽略残留碰撞。为空时自动从父级查找。")]
    [SerializeField] private Level102Ctrl levelController;

    [Tooltip("初始化时关闭当前物体上的所有 DragableItem，防止箱子被拖动。")]
    [SerializeField] private bool disableDragging = true;
    [Tooltip("完整箱子等待角色接触期间是否固定为 Static。")]
    [SerializeField] private bool lockBodyUntilBreak = true;
    [Tooltip("残骸没有碰撞体时使用的本地尺寸兜底值。")]
    [SerializeField] private Vector2 fallbackDebrisColliderSize = Vector2.one;

    [Header("残骸重新武装")]
    [Tooltip("残骸显示后，要求触发角色与残骸边界连续保持不重叠的物理帧数。")]
    [SerializeField, Min(1)] private int debrisClearFixedFrames = 2;

    // 残骸 relay 由完整箱子在运行时接管；保留序列化字段也允许策划在 Prefab 中预先挂好 relay。
    [SerializeField, HideInInspector] private Level102BoxBreakOnActor debrisOwner;

    private Collider2D sourceCollider;
    private Rigidbody2D sourceBody;
    private Renderer[] sourceRenderers = Array.Empty<Renderer>();
    private bool[] sourceRendererEnabled = Array.Empty<bool>();
    private DragableItem[] dragItems = Array.Empty<DragableItem>();
    private ParticleSystem runtimeEffect;
    private bool runtimeInitialized;
    private bool hasBroken;
    private bool initialColliderEnabled;
    private RigidbodyType2D initialBodyType;
    private bool initialSimulated;
    private Vector2 initialLinearVelocity;
    private float initialAngularVelocity;
    private float initialGravityScale;
    private RigidbodyConstraints2D initialConstraints;
    private Vector3 initialScale;
    private bool debrisInitialActive;
    private Vector3 debrisInitialPosition;
    private Quaternion debrisInitialRotation;
    private Vector3 debrisInitialScale;
    private bool debrisPoseCaptured;
    private int actorLayerBit;
    private ActorCtrl debrisBlockedActor;
    private Coroutine debrisArmRoutine;
    private bool debrisArmed;

    /// <summary>当前阶段是否已经完成碎裂。</summary>
    public bool HasBroken => hasBroken;

    private bool IsDebrisRelay => stage == BreakStage.Debris;

    private void Awake()
    {
        CaptureSourceState();
        ResolveActorLayerMask();
        ResolveLevelController();

        if (IsDebrisRelay)
        {
            // 预先放置的 relay 可以自持有；运行时创建的 relay 会在激活前由
            // ConfigureAsDebris 重新绑定到完整箱子。
            debrisOwner ??= this;
        }
        else
        {
            ResolveDebrisReference();
            CaptureDebrisPose();
        }

        ResolveBrokenEffectReference();
    }

    private void Start()
    {
        if (!Application.isPlaying)
            return;

        // 动态添加的 relay 会在 ConfigureAsDebris 中先完成初始化，随后才由
        // ActivateDebris 启用对象。Unity 可能在启用后的下一帧才调用 Start；
        // 这里不能再次初始化，否则会清掉刚启动的离开检测协程。
        if (IsDebrisRelay && runtimeInitialized)
            return;

        InitializeRuntimeState();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying || !runtimeInitialized)
            return;

        // 失败重试后重新启用预先放置的残骸时恢复可用状态；首次启用则由 Start 初始化。
        if (IsDebrisRelay && debrisOwner == null)
            debrisOwner = this;
    }

    private void OnDisable()
    {
        StopDebrisArming();

        if (runtimeEffect != null)
        {
            Destroy(runtimeEffect.gameObject);
            runtimeEffect = null;
        }
    }

    private void OnDestroy()
    {
        StopDebrisArming();

        if (runtimeEffect != null)
            Destroy(runtimeEffect.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryHandleCollision(collision, null);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        // Stay 也覆盖角色在复制的 Prefab 初始化完成时已经站在箱子上的情况。
        TryHandleCollision(collision, null);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        TryHandleCollision(null, collision);
    }

    /// <summary>
    /// 配置运行时添加到残骸对象上的 relay。
    /// </summary>
    public void ConfigureAsDebris(Level102BoxBreakOnActor owner)
    {
        // 初始化阶段和 owner 激活残骸时都会调用 PrepareDebrisRelay。同一 owner 已经
        // 初始化过的 relay 保留最初快照，不再把已隐藏/碎裂状态重新当成初始状态。
        Level102BoxBreakOnActor resolvedOwner = owner != null ? owner : this;
        if (IsDebrisRelay && runtimeInitialized && debrisOwner == resolvedOwner)
            return;

        stage = BreakStage.Debris;
        debrisOwner = resolvedOwner;
        brokenEffectPrefab = debrisOwner.brokenEffectPrefab;
        effectParent = debrisOwner.effectParent;
        breakSoundTag = debrisOwner.breakSoundTag;
        requireTopContact = debrisOwner.debrisRequireTopContact;
        topContactTolerance = debrisOwner.topContactTolerance;
        actorCenterAboveOffset = debrisOwner.actorCenterAboveOffset;
        actorLayerMask = debrisOwner.actorLayerMask;
        actorTag = debrisOwner.actorTag;
        levelController = debrisOwner.levelController != null
            ? debrisOwner.levelController
            : levelController;
        disableDragging = debrisOwner.disableDragging;
        addDebrisColliderIfMissing = debrisOwner.addDebrisColliderIfMissing;
        fallbackDebrisColliderSize = debrisOwner.fallbackDebrisColliderSize;
        debrisClearFixedFrames = debrisOwner.debrisClearFixedFrames;
        debrisInteractWithActor = debrisOwner.debrisInteractWithActor;
        breakEffectOffset = debrisOwner.breakEffectOffset;
        ResolveActorLayerMask();
        CaptureSourceState();
        InitializeRuntimeState();
    }

    /// <summary>可由关卡控制器在复位时调用，恢复箱体和残骸的初态。</summary>
    public void ResetRuntimeState()
    {
        if (IsDebrisRelay)
        {
            StopDebrisArming();
            hasBroken = false;
            RestoreVisualState();
            RestorePhysicsState();
            if (sourceCollider != null)
                sourceCollider.enabled = initialColliderEnabled;
            return;
        }

        StopDebrisArming();
        hasBroken = false;
        RestoreVisualState();
        RestorePhysicsState();
        if (sourceCollider != null)
            sourceCollider.enabled = initialColliderEnabled;

        if (debrisVisual != null)
        {
            Level102BoxBreakOnActor relay = debrisVisual.GetComponent<Level102BoxBreakOnActor>();
            if (relay != null && relay != this)
                relay.ResetRuntimeState();

            if (debrisPoseCaptured)
            {
                debrisVisual.transform.SetPositionAndRotation(debrisInitialPosition, debrisInitialRotation);
                debrisVisual.transform.localScale = debrisInitialScale;
            }

            // 每次尝试开始时都隐藏残骸；原始 active 值只作为旧 Prefab 的元数据保留。
            debrisVisual.SetActive(false);
            if (debrisCollider != null)
                debrisCollider.enabled = false;
        }

        DisableDraggingComponents();
        if (lockBodyUntilBreak)
            LockBody();
    }

    private void InitializeRuntimeState()
    {
        if (runtimeInitialized && !IsDebrisRelay)
            return;

        runtimeInitialized = true;
        ResolveSourceReferences();

        if (disableDragging)
            DisableDraggingComponents();

        if (IsDebrisRelay)
        {
            StopDebrisArming();
            hasBroken = false;
            debrisArmed = false;
            PrepareDebrisCollider();
            if (sourceCollider != null)
                sourceCollider.enabled = false;
            // owner 激活残骸后它应立即可见；第一次碎裂前由 owner 保持整个对象 inactive。
            RestoreVisualState();
            return;
        }

        hasBroken = false;
        RestoreVisualState();
        if (debrisVisual != null)
        {
            PrepareDebrisRelay();
            debrisVisual.SetActive(false);
            if (debrisCollider != null)
                debrisCollider.enabled = false;
        }

        if (lockBodyUntilBreak)
            LockBody();
    }

    private void CaptureSourceState()
    {
        sourceCollider = GetComponent<Collider2D>();
        sourceBody = GetComponent<Rigidbody2D>();
        initialScale = transform.localScale;
        initialColliderEnabled = sourceCollider == null || sourceCollider.enabled;

        sourceRenderers = GetComponentsInChildren<Renderer>(true);
        sourceRendererEnabled = new bool[sourceRenderers.Length];
        for (int index = 0; index < sourceRenderers.Length; index++)
            sourceRendererEnabled[index] = sourceRenderers[index] != null && sourceRenderers[index].enabled;

        if (sourceBody != null)
        {
            initialBodyType = sourceBody.bodyType;
            initialSimulated = sourceBody.simulated;
            initialLinearVelocity = sourceBody.linearVelocity;
            initialAngularVelocity = sourceBody.angularVelocity;
            initialGravityScale = sourceBody.gravityScale;
            initialConstraints = sourceBody.constraints;
        }
    }

    private void ResolveSourceReferences()
    {
        sourceCollider ??= GetComponent<Collider2D>();
        sourceBody ??= GetComponent<Rigidbody2D>();
        if (sourceRenderers.Length == 0)
            sourceRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void ResolveActorLayerMask()
    {
        actorLayerBit = 0;
        if (actorLayerMask.value != 0)
        {
            actorLayerBit = actorLayerMask.value;
            return;
        }

        int actorLayer = LayerMask.NameToLayer("Actor");
        if (actorLayer >= 0)
            actorLayerBit = 1 << actorLayer;
    }

    private void ResolveLevelController()
    {
        if (levelController != null)
            return;

        levelController = GetComponentInParent<Level102Ctrl>();
    }

    private void ResolveBrokenEffectReference()
    {
        if (brokenEffectPrefab != null || !autoFindBrokenEffect)
            return;

        Transform root = transform.root;
        ParticleSystem[] systems = root != null
            ? root.GetComponentsInChildren<ParticleSystem>(true)
            : Array.Empty<ParticleSystem>();
        for (int index = 0; index < systems.Length; index++)
        {
            ParticleSystem candidate = systems[index];
            if (candidate != null &&
                candidate.name.StartsWith("BoxBroken", StringComparison.OrdinalIgnoreCase))
            {
                brokenEffectPrefab = candidate;
                break;
            }
        }
    }

    private void ResolveDebrisReference()
    {
        if (debrisVisual != null)
            return;

        if (!string.IsNullOrWhiteSpace(debrisObjectName))
        {
            Transform root = transform.root;
            Transform[] transforms = root != null
                ? root.GetComponentsInChildren<Transform>(true)
                : Array.Empty<Transform>();
            for (int index = 0; index < transforms.Length; index++)
            {
                Transform candidate = transforms[index];
                if (candidate != transform && candidate.name == debrisObjectName)
                {
                    debrisVisual = candidate.gameObject;
                    break;
                }
            }
        }
    }

    private void CaptureDebrisPose()
    {
        if (debrisVisual == null || debrisVisual == gameObject)
            return;

        debrisInitialActive = debrisVisual.activeSelf;
        debrisInitialPosition = debrisVisual.transform.position;
        debrisInitialRotation = debrisVisual.transform.rotation;
        debrisInitialScale = debrisVisual.transform.localScale;
        debrisCollider ??= debrisVisual.GetComponent<Collider2D>();
        debrisPoseCaptured = true;
    }

    private void PrepareDebrisRelay()
    {
        if (debrisVisual == null || debrisVisual == gameObject)
            return;

        debrisCollider ??= debrisVisual.GetComponent<Collider2D>();
        Level102BoxBreakOnActor relay = debrisVisual.GetComponent<Level102BoxBreakOnActor>();

        // Level102 的残骸只保留视觉，不应在显示后再次响应角色接触。
        // 先禁用已有 relay/Collider，再决定是否允许兼容旧配置的自动补齐。
        if (!debrisInteractWithActor)
        {
            if (relay != null)
            {
                relay.StopDebrisArming();
                relay.enabled = false;
            }

            if (debrisCollider != null)
                debrisCollider.enabled = false;
            return;
        }

        if (relay == null && addDebrisRelayAtRuntime)
            relay = debrisVisual.AddComponent<Level102BoxBreakOnActor>();

        if (relay != null)
        {
            relay.ConfigureAsDebris(this);
            debrisCollider = relay.sourceCollider;
        }
        else if (addDebrisColliderIfMissing)
        {
            PrepareDebrisCollider();
        }
    }

    private void PrepareDebrisCollider()
    {
        if (IsDebrisRelay)
        {
            sourceCollider ??= GetComponent<Collider2D>();
            if (sourceCollider == null && addDebrisColliderIfMissing)
                sourceCollider = gameObject.AddComponent<BoxCollider2D>();

            if (sourceCollider is BoxCollider2D boxCollider)
                ConfigureFallbackBoxCollider(boxCollider);

            if (sourceCollider != null)
                sourceCollider.enabled = false;
            return;
        }

        if (debrisVisual == null)
            return;

        debrisCollider ??= debrisVisual.GetComponent<Collider2D>();
        if (debrisCollider == null && addDebrisColliderIfMissing)
        {
            debrisCollider = debrisVisual.AddComponent<BoxCollider2D>();
            if (debrisCollider is BoxCollider2D boxCollider)
                ConfigureFallbackBoxCollider(boxCollider);
        }
    }

    private void ConfigureFallbackBoxCollider(BoxCollider2D boxCollider)
    {
        if (boxCollider == null)
            return;

        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        Vector2 size = fallbackDebrisColliderSize;
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            // 将渲染器世界边界换算为碰撞体 owner 的本地单位，
            // 让带嵌套层级或缩放的残骸仍能得到合理的命中范围。
            Vector3 localBounds = transform.InverseTransformVector(spriteRenderer.bounds.size);
            size = new Vector2(
                Mathf.Max(0.05f, Mathf.Abs(localBounds.x)),
                Mathf.Max(0.05f, Mathf.Abs(localBounds.y)));
        }

        boxCollider.isTrigger = false;
        boxCollider.size = size;
        boxCollider.offset = Vector2.zero;
    }

    private void DisableDraggingComponents()
    {
        if (!disableDragging)
            return;

        dragItems = GetComponentsInChildren<DragableItem>(true);
        for (int index = 0; index < dragItems.Length; index++)
        {
            DragableItem item = dragItems[index];
            if (item == null)
                continue;

            item.SetEnableDrag(false);
            // 同时禁用组件，防止残留的 Level101Ctrl 在 RefreshInputAvailability 中
            // 再次打开其内部拖拽标记。
            item.enabled = false;
        }
    }

    private void LockBody()
    {
        if (sourceBody == null)
            return;

        sourceBody.bodyType = RigidbodyType2D.Static;
        sourceBody.simulated = true;
        sourceBody.linearVelocity = Vector2.zero;
        sourceBody.angularVelocity = 0f;
        sourceBody.WakeUp();
    }

    private void RestorePhysicsState()
    {
        if (sourceBody == null)
            return;

        sourceBody.bodyType = initialBodyType;
        sourceBody.simulated = initialSimulated;
        sourceBody.gravityScale = initialGravityScale;
        sourceBody.constraints = initialConstraints;
        sourceBody.linearVelocity = initialLinearVelocity;
        sourceBody.angularVelocity = initialAngularVelocity;
    }

    private void RestoreVisualState()
    {
        if (sourceRenderers == null)
            return;

        for (int index = 0; index < sourceRenderers.Length; index++)
        {
            Renderer renderer = sourceRenderers[index];
            if (renderer != null)
                renderer.enabled = index < sourceRendererEnabled.Length && sourceRendererEnabled[index];
        }
    }

    private void SetSourceRenderers(bool enabled)
    {
        if (sourceRenderers == null)
            return;

        for (int index = 0; index < sourceRenderers.Length; index++)
        {
            Renderer renderer = sourceRenderers[index];
            if (renderer != null)
                renderer.enabled = enabled;
        }
    }

    private void TryHandleCollision(Collision2D collision, Collider2D triggerCollider)
    {
        if (!Application.isPlaying || hasBroken ||
            (IsDebrisRelay && (debrisOwner == null || !debrisOwner.debrisInteractWithActor)))
            return;

        // Physics callbacks can arrive while Level102 is tearing down, resetting,
        // paused, or before its Start lifecycle has opened gameplay input. Ignore
        // those stale contacts instead of breaking the box during the next attempt.
        ResolveLevelController();
        if (levelController != null && !levelController.CanAcceptGameplayInput)
            return;

        Collider2D actorCollider = ResolveActorCollider(collision, triggerCollider);
        if (actorCollider == null || !IsPlayerActor(actorCollider))
            return;

        if (requireTopContact && !IsTopContact(actorCollider, collision))
            return;

        Vector3 contactPoint = ResolveContactPoint(collision, triggerCollider, actorCollider);
        if (IsDebrisRelay)
        {
            if (!debrisArmed)
                return;

            Level102BoxBreakOnActor owner = debrisOwner != null ? debrisOwner : this;
            owner.BreakDebris(this, contactPoint);
        }
        else
        {
            BreakIntact(contactPoint, actorCollider);
        }
    }

    private Collider2D ResolveActorCollider(Collision2D collision, Collider2D triggerCollider)
    {
        if (triggerCollider != null)
            return IsPlayerActor(triggerCollider) ? triggerCollider : null;

        if (collision == null)
            return null;

        Collider2D first = collision.collider;
        if (first != null && IsPlayerActor(first))
            return first;

        Collider2D second = collision.otherCollider;
        return second != null && IsPlayerActor(second) ? second : null;
    }

    private bool IsPlayerActor(Collider2D collider)
    {
        ActorCtrl actor = collider != null ? collider.GetComponentInParent<ActorCtrl>() : null;
        if (actor == null || actor.IsDied)
            return false;

        // Actor.prefab 的 Player 标签通常挂在实际碰撞子节点，而不是
        // ActorCtrl 根节点；两处任一命中都应视为玩家，避免踩箱路径被过滤。
        if (!string.IsNullOrWhiteSpace(actorTag) &&
            !actor.CompareTag(actorTag) &&
            !collider.CompareTag(actorTag))
            return false;

        if (actorLayerBit != 0)
        {
            int actorRootBit = 1 << actor.gameObject.layer;
            int colliderBit = 1 << collider.gameObject.layer;
            if ((actorLayerBit & actorRootBit) == 0 && (actorLayerBit & colliderBit) == 0)
                return false;
        }

        return true;
    }

    private bool IsTopContact(Collider2D actorCollider, Collision2D collision)
    {
        if (sourceCollider == null || actorCollider == null)
            return false;

        Bounds boxBounds = sourceCollider.bounds;
        Bounds actorBounds = actorCollider.bounds;
        if (actorBounds.center.y < boxBounds.center.y + actorCenterAboveOffset)
            return false;

        if (collision == null || collision.contactCount == 0)
            return actorBounds.min.y >= boxBounds.max.y - Mathf.Max(0.05f, topContactTolerance);

        float minimumTop = boxBounds.max.y - Mathf.Max(0.05f, topContactTolerance);
        for (int index = 0; index < collision.contactCount; index++)
        {
            ContactPoint2D contact = collision.GetContact(index);
            if (contact.point.y >= minimumTop && Mathf.Abs(contact.normal.y) >= 0.2f)
                return true;
        }

        return false;
    }

    private Vector3 ResolveContactPoint(
        Collision2D collision,
        Collider2D triggerCollider,
        Collider2D actorCollider)
    {
        if (collision != null && collision.contactCount > 0)
        {
            Vector2 sum = Vector2.zero;
            int count = 0;
            for (int index = 0; index < collision.contactCount; index++)
            {
                ContactPoint2D contact = collision.GetContact(index);
                sum += contact.point;
                count++;
            }

            if (count > 0)
                return new Vector3(sum.x / count, sum.y / count, transform.position.z);
        }

        Bounds sourceBounds = sourceCollider != null ? sourceCollider.bounds : new Bounds(transform.position, Vector3.one);
        Vector2 sourcePoint = sourceBounds.center;
        Vector2 actorPoint = actorCollider != null ? actorCollider.ClosestPoint(sourcePoint) : sourcePoint;
        return new Vector3((sourcePoint.x + actorPoint.x) * 0.5f,
            (sourcePoint.y + actorPoint.y) * 0.5f,
            transform.position.z);
    }

    private void BreakIntact(Vector3 contactPoint, Collider2D actorCollider)
    {
        if (hasBroken)
            return;

        hasBroken = true;
        DisableDraggingComponents();
        if (sourceCollider != null)
            sourceCollider.enabled = false;
        if (sourceBody != null)
        {
            sourceBody.linearVelocity = Vector2.zero;
            sourceBody.angularVelocity = 0f;
            sourceBody.bodyType = RigidbodyType2D.Kinematic;
            sourceBody.simulated = false;
        }

        debrisBlockedActor = actorCollider != null
            ? actorCollider.GetComponentInParent<ActorCtrl>()
            : null;

        // 先让残骸 relay 捕获自己的初始 Renderer 状态；当残骸嵌在箱体层级内时，
        // 下面的隐藏操作可能会同时影响它，激活后再由 relay 恢复。
        PrepareDebrisRelay();
        SetSourceRenderers(false);
        PlayBreakFeedback(contactPoint);
        ActivateDebris();
        onIntactBroken?.Invoke();
    }

    private void BreakDebris(Level102BoxBreakOnActor relay, Vector3 contactPoint)
    {
        if (relay == null || relay.hasBroken || !relay.debrisArmed ||
            !debrisInteractWithActor)
            return;

        relay.hasBroken = true;
        // 理论上 armed 后等待协程已结束；显式停止仍可防止边界帧上的
        // 旧协程在二次碎裂后再次打开 Collider。
        relay.StopDebrisArming();
        if (relay.sourceCollider != null)
            relay.sourceCollider.enabled = false;
        relay.SetSourceRenderers(false);
        relay.PlayBreakFeedback(contactPoint);
        relay.onDebrisBroken?.Invoke();
        if (relay != this)
            onDebrisBroken?.Invoke();
    }

    private void ActivateDebris()
    {
        if (debrisVisual == null || debrisVisual == gameObject)
            return;

        if (alignDebrisToBox)
        {
            debrisVisual.transform.SetPositionAndRotation(transform.position, transform.rotation);
        }
        else if (debrisPoseCaptured)
        {
            // The box can be pushed before it breaks. Keep the authored vertical
            // placement and visual pose, while following only the box's current X
            // so the wreckage stays over the position where the box was pushed.
            Vector3 debrisPosition = debrisInitialPosition;
            debrisPosition.x = transform.position.x;
            debrisVisual.transform.SetPositionAndRotation(debrisPosition, debrisInitialRotation);
            debrisVisual.transform.localScale = debrisInitialScale;
        }

        if (debrisVisual.transform.localScale == Vector3.zero)
            debrisVisual.transform.localScale = initialScale;

        PrepareDebrisRelay();
        debrisVisual.SetActive(true);

        Level102BoxBreakOnActor relay = debrisVisual.GetComponent<Level102BoxBreakOnActor>();
        if (!debrisInteractWithActor)
        {
            // 只显示残骸，不启用 Collider 或 relay；角色后续经过时不会再次爆炸。
            if (relay != null)
                relay.enabled = false;
            if (debrisCollider != null)
                debrisCollider.enabled = false;
        }
        else if (relay != null)
        {
            relay.RestoreVisualState();
            // 残骸与触发箱子的角色通常仍在同一位置。先保持 Collider 关闭，
            // 等角色完全离开后再重新武装，避免同一接触在下一物理帧触发二次碎裂。
            relay.BeginDebrisArming(debrisBlockedActor);
        }
        else if (debrisCollider != null)
        {
            // 没有 relay 时没有二次碎裂逻辑，保留旧 Prefab 的物理表现。
            debrisCollider.enabled = true;
        }
    }

    /// <summary>
    /// 显示残骸但暂不启用碰撞，直到触发首次碎裂的角色完全离开残骸边界。
    /// </summary>
    private void BeginDebrisArming(ActorCtrl actor)
    {
        if (!IsDebrisRelay)
            return;

        StopDebrisArming();
        debrisBlockedActor = actor;
        debrisArmed = false;

        if (sourceCollider == null)
        {
            debrisArmed = true;
            return;
        }

        sourceCollider.enabled = false;
        debrisArmRoutine = StartCoroutine(ArmDebrisAfterActorLeaves());
    }

    private IEnumerator ArmDebrisAfterActorLeaves()
    {
        int clearFrames = 0;
        int requiredFrames = Mathf.Max(1, debrisClearFixedFrames);

        while (true)
        {
            yield return new WaitForFixedUpdate();

            if (!Application.isPlaying || !isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                debrisArmRoutine = null;
                yield break;
            }

            if (IsBlockedActorOverlappingDebris())
            {
                clearFrames = 0;
                continue;
            }

            clearFrames++;
            if (clearFrames < requiredFrames)
                continue;

            debrisArmed = true;
            if (sourceCollider != null)
                sourceCollider.enabled = true;

            debrisArmRoutine = null;
            yield break;
        }
    }

    private bool IsBlockedActorOverlappingDebris()
    {
        if (debrisBlockedActor == null || debrisBlockedActor.IsDied)
            return false;

        if (!TryGetDebrisBounds(out Bounds debrisBounds))
            return false;

        Collider2D[] actorColliders = debrisBlockedActor.GetComponentsInChildren<Collider2D>(true);
        for (int index = 0; index < actorColliders.Length; index++)
        {
            Collider2D actorCollider = actorColliders[index];
            if (actorCollider == null || !actorCollider.enabled ||
                !actorCollider.gameObject.activeInHierarchy)
                continue;

            // 这是 2D 物理接触判定，忽略渲染层可能造成的 Z 轴差异。
            if (BoundsOverlap2D(actorCollider.bounds, debrisBounds))
                return true;
        }

        return false;
    }

    private bool TryGetDebrisBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        if (sourceCollider != null)
        {
            Bounds colliderBounds = sourceCollider.bounds;
            if (colliderBounds.size.x > 0.001f && colliderBounds.size.y > 0.001f)
            {
                bounds = colliderBounds;
                hasBounds = true;
            }
        }

        Renderer[] renderers = sourceRenderers;
        if (renderers == null || renderers.Length == 0)
            renderers = GetComponentsInChildren<Renderer>(true);

        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer renderer = renderers[index];
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private static bool BoundsOverlap2D(Bounds first, Bounds second)
    {
        return first.min.x <= second.max.x && first.max.x >= second.min.x &&
               first.min.y <= second.max.y && first.max.y >= second.min.y;
    }

    private void StopDebrisArming()
    {
        if (debrisArmRoutine != null)
            StopCoroutine(debrisArmRoutine);

        debrisArmRoutine = null;
        debrisBlockedActor = null;
        debrisArmed = false;
    }

    private void PlayBreakFeedback(Vector3 position)
    {
        if (runtimeEffect != null)
        {
            Destroy(runtimeEffect.gameObject);
            runtimeEffect = null;
        }

        if (brokenEffectPrefab != null)
        {
            try
            {
                Transform parent = effectParent != null ? effectParent : transform.root;
                Vector3 effectPosition = position + (Vector3)breakEffectOffset;
                runtimeEffect = Instantiate(
                    brokenEffectPrefab,
                    effectPosition,
                    brokenEffectPrefab.transform.rotation,
                    parent);
                runtimeEffect.gameObject.SetActive(true);
                runtimeEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                runtimeEffect.Play(true);
                Destroy(runtimeEffect.gameObject, ResolveEffectLifetime(runtimeEffect));
            }
            catch (Exception exception)
            {
                if (runtimeEffect != null)
                    Destroy(runtimeEffect.gameObject);
                runtimeEffect = null;
                Debug.LogWarning(
                    $"[Level102BoxBreakOnActor] 碎裂特效播放失败，继续执行碰撞状态。{exception.GetType().Name}: {exception.Message}",
                    this);
            }
        }

        if (!string.IsNullOrWhiteSpace(breakSoundTag))
            AudioManager.PlaySound(breakSoundTag);
    }

    private static float ResolveEffectLifetime(ParticleSystem root)
    {
        float lifetime = 0.5f;
        ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int index = 0; index < systems.Length; index++)
        {
            ParticleSystem.MainModule main = systems[index].main;
            lifetime = Mathf.Max(
                lifetime,
                main.startDelay.constantMax + main.duration + main.startLifetime.constantMax);
        }

        return lifetime + 0.1f;
    }
}
