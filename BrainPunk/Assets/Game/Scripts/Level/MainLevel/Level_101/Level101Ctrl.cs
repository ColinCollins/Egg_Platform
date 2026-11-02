using System.Collections;
using Bear.EventSystem;
using DG.Tweening;
using Game.Common;
using Game.Events;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using Spine;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Level101「砸晕门」的局部状态机。
/// 锁的拖拽只触发一次短暂的重新上锁表现；箱子从门顶自由落体命中后，
/// 先切到“正常门 + effect + 锁”的晕眩状态，再次拖走锁后才开放标准 BaseLevelCtrl 通关入口。
/// </summary>
public sealed class Level101Ctrl : MonoBehaviour
{
    private const float MinimumVerifiedFallDistance = 0.01f;
    private const float MinimumTopContactNormalY = 0.5f;
    private const string StunnedEffectSortingLayerName = "SceneLayer";
    private const int StunnedEffectSortingOrderGap = 1;
    private const int MinimumStunnedEffectSortingOrder = 110;
    private const string StunnedStarsMaterialName = "star_AB";
    private const string KnockWordObjectName = "Knock 1";
    private const string BoxBreakSoundTag = "crashBroke";

    #region Inspector

    [Header("基础引用")]
    [Tooltip("本关根节点上的标准关卡控制器；为空时从父级查找。")]
    [SerializeField] private BaseLevelCtrl baseLevelCtrl;
    [Tooltip("当前门根节点；为空时从自身或名称 door_0 查找。")]
    [SerializeField] private Transform doorRoot;
    [Tooltip("门原有的入口碰撞体。重新开启后会切回 Trigger 交给 BaseLevelCtrl 通关。")]
    [SerializeField] private BoxCollider2D doorCollider;

    [Header("门表现")]
    [Tooltip("正常门 Spine（door 子物体）。本关被砸晕后显示，并在移走锁后负责通行表现。")]
    [SerializeField] private GameObject doorSpineVisual;
    [Tooltip("用于重新上锁和待机的门体 Spine（body 子物体）。")]
    [SerializeField] private ActorSpineCtrl bodySpine;
    [Tooltip("被砸晕后需要隐藏的门体 Spine 表现，通常绑定 body。")]
    [SerializeField] private GameObject bodyVisual;
    [Tooltip("砸晕后与正常门同时显示的门体切换特效；初始和重置状态保持隐藏。")]
    [FormerlySerializedAs("stunnedDoorVisual")]
    [SerializeField] private GameObject doorEffectVisual;
    [Tooltip("眩晕星星特效的场景实例；若误绑 Prefab 资源，运行时会先实例化。")]
    [SerializeField] private GameObject stunnedEffect;
    [Tooltip("眩晕星星 Prefab 兼容兜底；当前关卡优先使用内嵌场景实例。")]
    [SerializeField] private GameObject stunnedEffectPrefab;
    [Tooltip("Level101 局部使用的星星贴图，避免覆盖项目中同 GUID 的共享 star_AB 材质。")]
    [SerializeField] private Texture2D stunnedStarsTexture;
    [Tooltip("眩晕特效的定位锚点；为空时使用 body 的 Transform。")]
    [SerializeField] private Transform stunnedEffectAnchor;
    [Tooltip("眩晕特效相对门碰撞体顶部的上移距离。")]
    [SerializeField, Min(0f)] private float stunnedEffectTopOffset = 0.35f;
    [Tooltip("被砸晕后打开的旧门 Spine 控制器；通常绑定 door 子物体。")]
    [SerializeField] private ActorSpineCtrl doorSpine;
    [Tooltip("旧门待机动画名称。")]
    [SerializeField] private string doorIdleAnimationName = "door_idle";
    [Tooltip("正常门与 body 主轮廓对齐后的局部位置；由两套 Spine 的 idle 网格边界计算。")]
    [SerializeField] private Vector3 normalDoorAlignedLocalPosition = new Vector3(0.1058f, -1.5815f, 0f);
    [Tooltip("正常门与 body 主轮廓对齐后的局部缩放；避免切换时出现尺寸跳变。")]
    [SerializeField] private Vector3 normalDoorAlignedLocalScale = new Vector3(1.0283f, 1.0165f, 1f);

    [Header("锁")]
    [Tooltip("可见锁图片的局部拖拽组件。")]
    [SerializeField] private Level101LockDragItem lockDrag;
    [Tooltip("锁图片 SpriteRenderer，用于拖走后的渐隐。")]
    [SerializeField] private SpriteRenderer lockRenderer;
    [Tooltip("body Spine 的重新上锁动画名称；当前资源为 lock。")]
    [SerializeField] private string relockAnimationName = "lock";
    [Tooltip("body Spine 的待机动画名称；当前资源为 idle。")]
    [SerializeField] private string idleAnimationName = "idle";
    [Tooltip("锁拖走后的渐隐时间（秒）。")]
    [SerializeField, Min(0f)] private float lockFadeDuration = 0.2f;
    [Tooltip("动画找不到或 Spine 未初始化时的重新上锁兜底时长（秒）。")]
    [SerializeField, Min(0.1f)] private float relockFallbackDuration = 1.9f;

    [Header("箱子")]
    [Tooltip("可举起箱子的 Level101 局部拖拽组件。")]
    [SerializeField] private Level101BoxDragItem boxDrag;
    [Tooltip("箱子刚体；为空时从 boxDrag 或子级查找。")]
    [SerializeField] private Rigidbody2D boxRigidbody;
    [Tooltip("箱子向下撞击门时所需的最小相对速度。")]
    [SerializeField, Min(0f)] private float minimumImpactDownwardSpeed = 1f;
    [Tooltip("碰撞点距离门顶的最大允许误差。")]
    [SerializeField, Min(0f)] private float topImpactTolerance = 0.7f;
    [Tooltip("释放后没有命中门时，箱子静止多久才恢复可再次拖拽。")]
    [SerializeField, Min(0.05f)] private float missedImpactRecoveryDelay = 0.25f;

    [Header("命中反馈")]
    [Tooltip("纯撞击粒子模板；运行时会关闭名为 Knock 1 的文字子节点。")]
    [SerializeField] private ParticleSystem impactEffectPrefab;
    [Tooltip("Level007 使用的箱子碎裂粒子模板；这里只播放短粒子，不保留破箱残骸。")]
    [SerializeField] private ParticleSystem boxBreakEffectPrefab;
    [Tooltip("撞击粒子的场景排序。")]
    [SerializeField] private int impactEffectSortingOrder = 200;
    [Tooltip("箱子碎裂粒子的场景排序。")]
    [SerializeField] private int boxBreakEffectSortingOrder = 201;

    #endregion

    #region Runtime State

    private enum LevelState
    {
        LockedIdle,
        Relocking,
        BoxDragging,
        BoxFalling,
        StunnedLocked,
        Unlocking,
        DoorUnlocked,
    }

    private LevelState state;
    private int flowVersion;
    private bool lockDragStarted;
    private bool boxDragStarted;
    private float boxReleaseTime;
    private Coroutine relockFallbackRoutine;
    private Coroutine missedImpactRoutine;
    private Tween lockFadeTween;
    private Vector3 lockInitialLocalPosition;
    private Quaternion lockInitialLocalRotation;
    private Vector3 lockInitialLocalScale;
    private Color lockInitialColor = Color.white;
    private Spine.AnimationState.TrackEntryDelegate relockCompleteHandler;
    private TrackEntry relockTrackEntry;
    private EventSubscriber eventSubscriber;
    private bool pausedBoxFalling;
    private Vector2 pausedBoxVelocity;
    private bool localInputPaused;
    private bool ownsStunnedEffect;
    private bool warnedMissingStunnedEffect;
    private Vector3 boxInitialPosition;
    private Quaternion boxInitialRotation;
    private Vector3 boxInitialScale;
    private bool hasInitialBoxPose;
    private float boxReleasePositionY;
    private bool hasBoxReleasePosition;
    private bool runtimeInitialized;
    private bool boxInitialActive;
    private ParticleSystem runtimeImpactEffect;
    private ParticleSystem runtimeBoxBreakEffect;
    private Transform doorRibbonVisual;
    private Collider2D[] lockColliders = System.Array.Empty<Collider2D>();
    private int actorColliderLayerMask;

    /// <summary>当前是否已经把门砸晕；只有移走锁后的 DoorUnlocked 才开放通关入口。</summary>
    public bool IsDoorStunned => state == LevelState.StunnedLocked ||
                                 state == LevelState.Unlocking ||
                                 state == LevelState.DoorUnlocked;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();
        CaptureLockPose();
        CaptureBoxPose();

        // 编辑器加载 Prefab 时只解析引用和初始姿态，运行时实例化/状态初始化必须留在 PlayMode。
        if (!Application.isPlaying)
            return;

        runtimeInitialized = true;
        try
        {
            ResolveStunnedEffectInstance();
        }
        catch (System.Exception exception)
        {
            // 星星是必需表现，但其资源异常不能阻断本关输入和核心状态机初始化。
            CleanupFailedStunnedEffectInitialization();
            Debug.LogWarning(
                $"[Level101Ctrl] 眩晕星星初始化失败，本关继续运行。{exception.GetType().Name}: {exception.Message}",
                this);
        }

        PrepareInitialState();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        localInputPaused = false;
        EventsUtils.ResetEvents(ref eventSubscriber);
        eventSubscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
        eventSubscriber.Subscribe<GamePauseEvent>(OnGamePause);
        eventSubscriber.Subscribe<GameResumeEvent>(OnGameResume);
        eventSubscriber.Subscribe<GameResetEvent>(OnGameReset);
        eventSubscriber.Subscribe<GameReplayEvent>(OnGameReplay);
        ApplyCollisionRulesForCurrentState();
        RefreshInputAvailability();
    }

    private void OnDisable()
    {
        // Prefab/场景编辑阶段不执行运行时清理；退出 PlayMode 时保留已初始化标记以完成收尾。
        if (!Application.isPlaying && !runtimeInitialized)
            return;

        BeginTeardown();
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying && !runtimeInitialized)
            return;

        lockFadeTween?.Kill();
        lockFadeTween = null;
        ClearRelockTrackEntry();
        EventsUtils.ResetEvents(ref eventSubscriber);
        ClearTransientImpactEffects();
        if (ownsStunnedEffect)
        {
            try
            {
                if (stunnedEffect != null)
                    Destroy(stunnedEffect);
            }
            catch (MissingReferenceException)
            {
                // YooAsset 释放外部模板后，Unity 可能保留一个已失效的托管包装；清理时直接忽略。
            }
        }
        stunnedEffect = null;
    }

    private void Update()
    {
        RefreshInputAvailability();

        if (state != LevelState.BoxFalling || boxRigidbody == null ||
            pausedBoxFalling || !IsGameplayInputOpen())
            return;

        // 未命中时箱子会停在地面；允许玩家再次举起尝试，避免一次误放造成软锁。
        if (Time.time - boxReleaseTime >= missedImpactRecoveryDelay &&
            boxRigidbody.linearVelocity.sqrMagnitude <= 0.04f &&
            missedImpactRoutine == null)
        {
            missedImpactRoutine = StartCoroutine(RecoverMissedBoxAfterDelay(flowVersion));
        }
    }

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        if (evt == null)
            return;

        if (evt.NewState == GamePlayStateName.PLAYING)
        {
            localInputPaused = false;
            ResumePausedBox();
            RefreshInputAvailability();
            return;
        }

        localInputPaused = true;
        PauseLocalInput();
    }

    private void OnGamePause(GamePauseEvent evt)
    {
        localInputPaused = true;
        PauseLocalInput();
    }

    private void OnGameResume(GameResumeEvent evt)
    {
        localInputPaused = false;
        ResumePausedBox();
        RefreshInputAvailability();
    }

    private void OnGameReset(GameResetEvent evt)
    {
        BeginTeardown();
    }

    private void OnGameReplay(GameReplayEvent evt)
    {
        BeginTeardown();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryHandleBoxImpact(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryHandleBoxImpact(collision);
    }

    #endregion

    #region Lock Flow

    /// <summary>
    /// 由 Level101LockDragItem 在锁开始拖拽时调用；返回 false 时本次输入被忽略。
    /// </summary>
    public bool TryBeginLockDrag()
    {
        bool canMoveLock = state == LevelState.LockedIdle || state == LevelState.StunnedLocked;
        if (!canMoveLock || lockDragStarted || boxDragStarted ||
            !IsGameplayInputOpen())
            return false;

        lockDragStarted = true;
        return true;
    }

    /// <summary>
    /// 由 Level101LockDragItem 在锁结束拖拽时调用，启动渐隐和 body 的 lock 动画。
    /// </summary>
    public void EndLockDrag()
    {
        if (!lockDragStarted ||
            (state != LevelState.LockedIdle && state != LevelState.StunnedLocked))
            return;

        lockDragStarted = false;

        // DragableItem 在暂停/切状态时会主动结束当前拖拽；此时不能把暂停误判为一次重锁。
        if (!IsGameplayInputOpen())
        {
            RestoreLockVisual();
            return;
        }

        if (state == LevelState.StunnedLocked)
        {
            BeginFinalUnlock();
            return;
        }

        state = LevelState.Relocking;
        int version = ++flowVersion;

        lockDrag?.SetEnableDrag(false);
        lockFadeTween?.Kill();
        if (lockRenderer != null)
        {
            lockFadeTween = lockRenderer.DOFade(0f, Mathf.Max(0f, lockFadeDuration))
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        }

        PlayRelockAnimation(version);
    }

    private void PlayRelockAnimation(int version)
    {
        ClearRelockTrackEntry();
        if (bodySpine != null && !string.IsNullOrEmpty(relockAnimationName))
        {
            TrackEntry entry = bodySpine.PlayAnimation(relockAnimationName, false, false);
            if (entry != null)
            {
                relockTrackEntry = entry;
                relockCompleteHandler = _ => FinishRelock(version);
                relockTrackEntry.Complete += relockCompleteHandler;
                relockFallbackRoutine = StartCoroutine(RelockTimeout(version, relockFallbackDuration));
                return;
            }
        }

        relockFallbackRoutine = StartCoroutine(RelockTimeout(version, relockFallbackDuration));
    }

    private IEnumerator RelockTimeout(int version, float duration)
    {
        float elapsed = 0f;
        while (elapsed < Mathf.Max(0.1f, duration))
        {
            if (version != flowVersion || state != LevelState.Relocking)
                yield break;

            elapsed += Mathf.Max(0f, Time.deltaTime);
            yield return null;
        }

        relockFallbackRoutine = null;
        FinishRelock(version);
    }

    private void FinishRelock(int version)
    {
        if (version != flowVersion || state != LevelState.Relocking)
            return;

        ClearRelockTrackEntry();
        if (relockFallbackRoutine != null)
        {
            StopCoroutine(relockFallbackRoutine);
            relockFallbackRoutine = null;
        }

        PlayBodyIdleWithoutMix();
        RestoreLockVisual();
        state = LevelState.LockedIdle;
        RefreshInputAvailability();
    }

    private void BeginFinalUnlock()
    {
        state = LevelState.Unlocking;
        int version = ++flowVersion;
        lockDrag?.SetEnableDrag(false);
        lockFadeTween?.Kill();
        lockFadeTween = null;

        if (lockRenderer == null || lockFadeDuration <= 0f)
        {
            FinishFinalUnlock(version);
            return;
        }

        lockFadeTween = lockRenderer
            .DOFade(0f, lockFadeDuration)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnComplete(() => FinishFinalUnlock(version));
    }

    private void FinishFinalUnlock(int version)
    {
        if (version != flowVersion || state != LevelState.Unlocking)
            return;

        lockFadeTween = null;
        if (lockDrag != null)
            lockDrag.gameObject.SetActive(false);

        state = LevelState.DoorUnlocked;
        ApplyCollisionRulesForCurrentState();

        RefreshInputAvailability();
    }

    private void RestoreLockVisual()
    {
        lockFadeTween?.Kill();
        lockFadeTween = null;

        if (lockDrag != null)
        {
            lockDrag.gameObject.SetActive(true);
            lockDrag.transform.localPosition = lockInitialLocalPosition;
            lockDrag.transform.localRotation = lockInitialLocalRotation;
            lockDrag.transform.localScale = lockInitialLocalScale;
        }

        if (lockRenderer != null)
            lockRenderer.color = lockInitialColor;
    }

    #endregion

    #region Box Flow

    /// <summary>由 Level101BoxDragItem 在箱子开始拖拽时调用。</summary>
    public bool TryBeginBoxDrag()
    {
        if (state != LevelState.LockedIdle || lockDragStarted || boxDragStarted ||
            !IsGameplayInputOpen())
            return false;

        boxDragStarted = true;
        state = LevelState.BoxDragging;
        lockDrag?.SetEnableDrag(false);
        return true;
    }

    /// <summary>由 Level101BoxDragItem 在箱子释放时调用，交回动态刚体等待门顶命中。</summary>
    public void EndBoxDrag()
    {
        if (!boxDragStarted || state != LevelState.BoxDragging)
            return;

        boxDragStarted = false;

        // 暂停或切换状态导致的 DragableItem.EndDrag 不应释放箱子进入坠落态。
        if (!IsGameplayInputOpen())
        {
            state = LevelState.LockedIdle;
            SetBoxKinematicForDrag();
            return;
        }

        state = LevelState.BoxFalling;
        boxReleaseTime = Time.time;

        if (boxRigidbody != null)
        {
            boxReleasePositionY = boxRigidbody.position.y;
            hasBoxReleasePosition = true;
            boxRigidbody.bodyType = RigidbodyType2D.Dynamic;
            boxRigidbody.simulated = true;
            boxRigidbody.linearVelocity = Vector2.zero;
            boxRigidbody.angularVelocity = 0f;
        }
    }

    /// <summary>由局部箱子拖拽组件在拖拽开始时切换刚体为运动学。</summary>
    public void SetBoxKinematicForDrag()
    {
        if (boxRigidbody == null)
            return;

        boxRigidbody.bodyType = RigidbodyType2D.Kinematic;
        boxRigidbody.linearVelocity = Vector2.zero;
        boxRigidbody.angularVelocity = 0f;
    }

    /// <summary>
    /// 接收箱子自身 Collider2D 的碰撞回调；只允许本关箱子在坠落态触发。
    /// 调用边界：仅由 Level101BoxDragItem 或本关根节点的 Collision2D 转发调用。
    /// </summary>
    public void HandleBoxCollision(Collision2D collision)
    {
        TryHandleBoxImpact(collision);
    }

    private void TryHandleBoxImpact(Collision2D collision)
    {
        if (state != LevelState.BoxFalling || collision == null || boxRigidbody == null)
            return;

        // 即使来自箱子 relay，也同时核对碰撞双方，避免组件误挂后转发其它碰撞。
        if (!IsDoorCollision(collision) ||
            !IsBoxCollision(collision) ||
            !TryGetTopDownImpact(collision, out Vector3 impactPoint))
            return;

        EnterStunnedState(impactPoint);
    }

    private bool IsBoxCollision(Collision2D collision)
    {
        if (collision.rigidbody == boxRigidbody || collision.otherRigidbody == boxRigidbody)
            return true;

        Collider2D ownCollider = collision.collider;
        Collider2D otherCollider = collision.otherCollider;
        return (ownCollider != null && ownCollider.attachedRigidbody == boxRigidbody) ||
               (otherCollider != null && otherCollider.attachedRigidbody == boxRigidbody);
    }

    private bool IsDoorCollision(Collision2D collision)
    {
        if (doorCollider == null || collision == null)
            return false;

        return IsDoorCollider(collision.collider) || IsDoorCollider(collision.otherCollider);
    }

    private bool IsDoorCollider(Collider2D candidate)
    {
        return candidate == doorCollider ||
               (candidate != null && candidate.transform == doorCollider.transform);
    }

    private bool TryGetTopDownImpact(Collision2D collision, out Vector3 impactPoint)
    {
        impactPoint = Vector3.zero;
        Collider2D boxCollider = GetBoxCollisionCollider(collision);
        if (boxCollider == null || doorCollider == null || !hasBoxReleasePosition)
            return false;

        Bounds doorBounds = doorCollider.bounds;
        Bounds boxBounds = boxCollider.bounds;
        float topY = doorBounds.max.y;
        float tolerance = Mathf.Max(0f, topImpactTolerance);

        // 方向由释放点和空间关系确认；relativeVelocity 的符号会随回调接收方变化，
        // 因此这里只使用其绝对值作为撞击速度。
        if (boxRigidbody.position.y > boxReleasePositionY - MinimumVerifiedFallDistance ||
            boxBounds.center.y <= topY ||
            Mathf.Abs(collision.relativeVelocity.y) < Mathf.Max(0f, minimumImpactDownwardSpeed))
            return false;

        for (int index = 0; index < collision.contactCount; index++)
        {
            ContactPoint2D contact = collision.GetContact(index);
            if (contact.point.y >= topY - tolerance &&
                Mathf.Abs(contact.normal.y) >= MinimumTopContactNormalY)
            {
                impactPoint = new Vector3(contact.point.x, contact.point.y, doorRoot != null
                    ? doorRoot.position.z
                    : transform.position.z);
                return true;
            }
        }

        return false;
    }

    private Collider2D GetBoxCollisionCollider(Collision2D collision)
    {
        Collider2D incomingCollider = collision.collider;
        if (incomingCollider != null && incomingCollider.attachedRigidbody == boxRigidbody)
            return incomingCollider;

        Collider2D otherCollider = collision.otherCollider;
        if (otherCollider != null && otherCollider.attachedRigidbody == boxRigidbody)
            return otherCollider;

        return null;
    }

    private void EnterStunnedState(Vector3 impactPoint)
    {
        if (IsDoorStunned)
            return;

        state = LevelState.StunnedLocked;
        flowVersion++;
        lockDragStarted = false;
        boxDragStarted = false;
        StopMissedImpactRecovery();

        boxDrag?.SetEnableDrag(false);
        lockDrag?.SetEnableDrag(false);

        Vector3 boxBreakPosition = GetBoxBreakPosition();
        StopAndHideBox();
        PlayImpactFeedback(impactPoint, boxBreakPosition);

        // 砸晕后先显示“正常门 + effect + 锁”，门仍是实体；玩家移走锁后才开放 Trigger。
        bodyVisual?.SetActive(false);
        ApplyNormalDoorAlignment();
        doorEffectVisual?.SetActive(true);
        doorSpineVisual?.SetActive(true);
        TrackEntry doorIdle = doorSpine?.PlayAnimation(doorIdleAnimationName, true, false);
        if (doorIdle != null)
            doorIdle.MixDuration = 0f;
        RestoreLockVisual();

        if (stunnedEffect != null)
        {
            PositionStunnedEffect();
            SetStunnedEffectActive(true);
        }

        ApplyCollisionRulesForCurrentState();

        RefreshInputAvailability();
    }

    private Vector3 GetBoxBreakPosition()
    {
        if (boxDrag != null)
        {
            Collider2D boxCollider = boxDrag.GetComponent<Collider2D>();
            if (boxCollider != null)
                return boxCollider.bounds.center;

            return boxDrag.transform.position;
        }

        return boxRigidbody != null ? boxRigidbody.transform.position : transform.position;
    }

    private void StopAndHideBox()
    {
        if (boxRigidbody != null)
        {
            boxRigidbody.linearVelocity = Vector2.zero;
            boxRigidbody.angularVelocity = 0f;
            boxRigidbody.bodyType = RigidbodyType2D.Kinematic;
            boxRigidbody.simulated = false;
        }

        GameObject boxObject = boxDrag != null
            ? boxDrag.gameObject
            : (boxRigidbody != null ? boxRigidbody.gameObject : null);
        boxObject?.SetActive(false);
    }

    private void PlayImpactFeedback(Vector3 impactPoint, Vector3 boxBreakPosition)
    {
        ClearTransientImpactEffects();
        runtimeImpactEffect = SpawnParticleEffect(
            impactEffectPrefab,
            impactPoint,
            impactEffectSortingOrder,
            true,
            "撞击");
        runtimeBoxBreakEffect = SpawnParticleEffect(
            boxBreakEffectPrefab,
            boxBreakPosition,
            boxBreakEffectSortingOrder,
            false,
            "箱子碎裂");
        AudioManager.PlaySound(BoxBreakSoundTag);
    }

    private ParticleSystem SpawnParticleEffect(
        ParticleSystem template,
        Vector3 position,
        int sortingOrder,
        bool hideKnockWord,
        string effectName)
    {
        ParticleSystem instance = null;
        try
        {
            if (template == null)
                return null;

            instance = Instantiate(template, position, template.transform.rotation, transform);
            if (hideKnockWord)
            {
                Transform knockWord = instance.transform.Find(KnockWordObjectName);
                knockWord?.gameObject.SetActive(false);
            }

            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            int lowestSortingOrder = renderers.Length > 0 ? renderers[0].sortingOrder : 0;
            for (int index = 1; index < renderers.Length; index++)
                lowestSortingOrder = Mathf.Min(lowestSortingOrder, renderers[index].sortingOrder);

            for (int index = 0; index < renderers.Length; index++)
            {
                int relativeSortingOrder = renderers[index].sortingOrder - lowestSortingOrder;
                renderers[index].sortingLayerName = StunnedEffectSortingLayerName;
                renderers[index].sortingOrder = sortingOrder + relativeSortingOrder;
            }

            instance.gameObject.SetActive(true);
            instance.Play(true);
            Destroy(instance.gameObject, GetParticleEffectLifetime(instance));
            return instance;
        }
        catch (System.Exception exception)
        {
            if (instance != null)
                Destroy(instance.gameObject);
            Debug.LogWarning(
                $"[Level101Ctrl] {effectName}特效播放失败，本关继续运行。{exception.GetType().Name}: {exception.Message}",
                this);
            return null;
        }
    }

    private static float GetParticleEffectLifetime(ParticleSystem root)
    {
        float lifetime = 0.5f;
        ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int index = 0; index < systems.Length; index++)
        {
            ParticleSystem.MainModule main = systems[index].main;
            float systemLifetime = main.startDelay.constantMax + main.duration + main.startLifetime.constantMax;
            lifetime = Mathf.Max(lifetime, systemLifetime);
        }

        return lifetime + 0.1f;
    }

    private void ClearTransientImpactEffects()
    {
        DestroyRuntimeEffect(ref runtimeImpactEffect);
        DestroyRuntimeEffect(ref runtimeBoxBreakEffect);
    }

    private void DestroyRuntimeEffect(ref ParticleSystem effect)
    {
        if (effect != null)
            Destroy(effect.gameObject);
        effect = null;
    }

    private IEnumerator RecoverMissedBoxAfterDelay(int version)
    {
        yield return null;
        missedImpactRoutine = null;

        if (version != flowVersion || state != LevelState.BoxFalling ||
            boxRigidbody == null || pausedBoxFalling || !IsGameplayInputOpen() ||
            boxRigidbody.linearVelocity.sqrMagnitude > 0.04f)
            yield break;

        state = LevelState.LockedIdle;
        RefreshInputAvailability();
    }

    #endregion

    #region Pause and reset

    private void PauseLocalInput()
    {
        StopMissedImpactRecovery();
        if (state == LevelState.Unlocking)
            lockFadeTween?.Pause();

        bool wasLockDragging = lockDragStarted || (lockDrag != null && lockDrag.IsDragging());
        bool wasBoxDragging = boxDragStarted || (boxDrag != null && boxDrag.IsDragging());
        lockDragStarted = false;
        boxDragStarted = false;

        if (state == LevelState.Relocking)
        {
            flowVersion++;
            state = LevelState.LockedIdle;
            ClearRelockTrackEntry();
            if (relockFallbackRoutine != null)
                StopCoroutine(relockFallbackRoutine);
            relockFallbackRoutine = null;
            PlayBodyIdleWithoutMix();
            RestoreLockVisual();
        }
        else if (state == LevelState.BoxDragging)
        {
            state = LevelState.LockedIdle;
        }

        if (state == LevelState.BoxFalling && boxRigidbody != null && !pausedBoxFalling)
        {
            pausedBoxFalling = true;
            pausedBoxVelocity = boxRigidbody.linearVelocity;
            boxRigidbody.simulated = false;
        }

        lockDrag?.SetEnableDrag(false);
        boxDrag?.SetEnableDrag(false);

        // SetEnableDrag(false) 会让共享 DragableItem 主动结束拖拽；在此之前已清除
        // 局部标记，因此回调不会启动新的流程。这里补一次姿态恢复，覆盖两种事件顺序。
        if (wasLockDragging)
        {
            RestoreLockVisual();
            if (state == LevelState.LockedIdle)
                PlayBodyIdleWithoutMix();
        }

        if (wasBoxDragging)
            SetBoxKinematicForDrag();
    }

    private void ResumePausedBox()
    {
        if (state == LevelState.Unlocking)
            lockFadeTween?.Play();

        if (IsDoorStunned)
            return;

        if (pausedBoxFalling && boxRigidbody != null)
        {
            pausedBoxFalling = false;
            boxRigidbody.bodyType = RigidbodyType2D.Dynamic;
            boxRigidbody.simulated = true;
            boxRigidbody.linearVelocity = pausedBoxVelocity;
        }
        else if (state != LevelState.BoxFalling)
        {
            pausedBoxFalling = false;
        }

        RefreshInputAvailability();
    }

    private void BeginTeardown()
    {
        localInputPaused = true;
        flowVersion++;
        ClearRelockTrackEntry();
        lockFadeTween?.Kill();
        lockFadeTween = null;
        if (relockFallbackRoutine != null)
            StopCoroutine(relockFallbackRoutine);
        StopMissedImpactRecovery();

        relockFallbackRoutine = null;
        missedImpactRoutine = null;
        lockDragStarted = false;
        boxDragStarted = false;
        pausedBoxFalling = false;
        hasBoxReleasePosition = false;
        lockDrag?.SetEnableDrag(false);
        boxDrag?.SetEnableDrag(false);
        ClearTransientImpactEffects();
        EventsUtils.ResetEvents(ref eventSubscriber);
    }

    private void ClearRelockTrackEntry()
    {
        if (relockTrackEntry != null && relockCompleteHandler != null)
            relockTrackEntry.Complete -= relockCompleteHandler;

        relockTrackEntry = null;
        relockCompleteHandler = null;
    }

    private void StopMissedImpactRecovery()
    {
        if (missedImpactRoutine != null)
            StopCoroutine(missedImpactRoutine);

        missedImpactRoutine = null;
    }

    #endregion

    #region Setup

    private void ResolveReferences()
    {
        if (baseLevelCtrl == null)
            baseLevelCtrl = GetComponent<BaseLevelCtrl>() ?? GetComponentInParent<BaseLevelCtrl>();

        if (doorRoot == null)
            doorRoot = transform.Find("door_0") ?? transform;

        if (doorCollider == null && doorRoot != null)
            doorCollider = doorRoot.GetComponent<BoxCollider2D>();

        if (doorSpineVisual == null && doorRoot != null)
            doorSpineVisual = doorRoot.Find("door")?.gameObject;

        if (bodySpine == null && doorRoot != null)
            bodySpine = doorRoot.Find("body")?.GetComponent<ActorSpineCtrl>();

        if (bodyVisual == null && bodySpine != null)
            bodyVisual = bodySpine.gameObject;

        if (doorSpine == null && doorSpineVisual != null)
            doorSpine = doorSpineVisual.GetComponent<ActorSpineCtrl>();

        if (doorRibbonVisual == null && doorRoot != null)
            doorRibbonVisual = doorRoot.Find("ribbon");

        if (doorEffectVisual == null && doorRoot != null)
            doorEffectVisual = doorRoot.Find("effect")?.gameObject;

        if (lockDrag == null && doorRoot != null)
            lockDrag = doorRoot.Find("lock")?.GetComponent<Level101LockDragItem>();

        if (lockRenderer == null && lockDrag != null)
            lockRenderer = lockDrag.GetComponent<SpriteRenderer>();

        if (stunnedEffectAnchor == null)
            stunnedEffectAnchor = bodyVisual != null ? bodyVisual.transform : doorRoot;

        if (boxDrag == null)
            boxDrag = GetComponentInChildren<Level101BoxDragItem>(true);

        if (boxRigidbody == null && boxDrag != null)
            boxRigidbody = boxDrag.GetComponent<Rigidbody2D>();
    }

    private void ResolveStunnedEffectInstance()
    {
        GameObject resolvedEffect = null;

        try
        {
            // 优先使用关卡 Prefab 内的场景实例；这条路径不依赖外部资源句柄的生命周期。
            if (stunnedEffect != null && stunnedEffect.scene.IsValid())
                resolvedEffect = stunnedEffect;
        }
        catch (MissingReferenceException)
        {
            // 外部资源被 YooAsset 释放后可能留下 fake-null 包装，不能继续访问 scene/Transform。
            stunnedEffect = null;
        }

        if (resolvedEffect == null)
        {
            GameObject template = null;
            try
            {
                if (stunnedEffectPrefab != null)
                    template = stunnedEffectPrefab;
            }
            catch (MissingReferenceException)
            {
                stunnedEffectPrefab = null;
            }

            // 兼容旧 Prefab：只有模板仍由资源系统持有时才尝试实例化；失效模板安全降级为无星星表现。
            if (template != null)
            {
                try
                {
                    resolvedEffect = Instantiate(template, transform);
                    resolvedEffect.name = template.name + "_Runtime";
                    ownsStunnedEffect = true;
                }
                catch (MissingReferenceException)
                {
                    resolvedEffect = null;
                    stunnedEffectPrefab = null;
                }
            }
        }

        stunnedEffect = resolvedEffect;
        if (stunnedEffect == null)
        {
            if (!warnedMissingStunnedEffect)
            {
                warnedMissingStunnedEffect = true;
                Debug.LogWarning("[Level101Ctrl] 眩晕星星特效引用已失效，本关继续运行但不显示星星。", this);
            }

            return;
        }

        try
        {
            ConfigureStunnedEffectRenderers();
            SetStunnedEffectActive(false);
        }
        catch (MissingReferenceException)
        {
            CleanupFailedStunnedEffectInitialization();
        }
    }

    private void CleanupFailedStunnedEffectInitialization()
    {
        if (ownsStunnedEffect)
        {
            try
            {
                if (stunnedEffect != null)
                    Destroy(stunnedEffect);
            }
            catch (MissingReferenceException)
            {
                // 资源句柄已先释放时只需清空托管引用。
            }
        }

        stunnedEffect = null;
        ownsStunnedEffect = false;
    }

    private void SetStunnedEffectActive(bool isActive)
    {
        try
        {
            if (stunnedEffect != null)
                stunnedEffect.SetActive(isActive);
        }
        catch (MissingReferenceException)
        {
            CleanupFailedStunnedEffectInitialization();
        }
    }

    private void ConfigureStunnedEffectRenderers()
    {
        if (stunnedEffect == null)
            return;

        int highestDoorSortingOrder = 0;
        if (doorRoot != null)
        {
            Renderer[] doorRenderers = doorRoot.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < doorRenderers.Length; index++)
            {
                Renderer renderer = doorRenderers[index];
                if (renderer == null || renderer.transform == stunnedEffect.transform ||
                    renderer.transform.IsChildOf(stunnedEffect.transform))
                    continue;

                highestDoorSortingOrder = Mathf.Max(highestDoorSortingOrder, renderer.sortingOrder);
            }
        }

        Renderer[] effectRenderers = stunnedEffect.GetComponentsInChildren<Renderer>(true);
        int effectBaseSortingOrder = Mathf.Max(
            MinimumStunnedEffectSortingOrder,
            highestDoorSortingOrder + StunnedEffectSortingOrderGap);
        for (int index = 0; index < effectRenderers.Length; index++)
        {
            Renderer renderer = effectRenderers[index];
            if (renderer == null)
                continue;

            int originalOrder = renderer.sortingOrder;
            renderer.sortingLayerName = StunnedEffectSortingLayerName;
            renderer.sortingOrder = effectBaseSortingOrder + originalOrder;

            Material sharedMaterial = renderer.sharedMaterial;
            if (stunnedStarsTexture != null && sharedMaterial != null &&
                sharedMaterial.name == StunnedStarsMaterialName)
            {
                MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetTexture("_MainTex", stunnedStarsTexture);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }
    }

    private void PositionStunnedEffect()
    {
        try
        {
            if (stunnedEffect == null)
                return;

            Vector3 effectPosition = stunnedEffectAnchor != null
                ? stunnedEffectAnchor.position
                : (doorRoot != null ? doorRoot.position : transform.position);

            if (doorCollider != null)
                effectPosition.y = doorCollider.bounds.max.y + Mathf.Max(0f, stunnedEffectTopOffset);

            stunnedEffect.transform.position = effectPosition;
        }
        catch (MissingReferenceException)
        {
            stunnedEffect = null;
            ownsStunnedEffect = false;
        }
    }

    private void CaptureLockPose()
    {
        if (lockDrag == null)
            return;

        lockInitialLocalPosition = lockDrag.transform.localPosition;
        lockInitialLocalRotation = lockDrag.transform.localRotation;
        lockInitialLocalScale = lockDrag.transform.localScale;
        if (lockRenderer != null)
            lockInitialColor = lockRenderer.color;
    }

    private void CaptureBoxPose()
    {
        if (boxDrag == null)
            return;

        boxInitialPosition = boxDrag.transform.position;
        boxInitialRotation = boxDrag.transform.rotation;
        boxInitialScale = boxDrag.transform.localScale;
        boxInitialActive = boxDrag.gameObject.activeSelf;
        hasInitialBoxPose = true;
    }

    private void PrepareInitialState()
    {
        state = LevelState.LockedIdle;
        lockDragStarted = false;
        boxDragStarted = false;
        hasBoxReleasePosition = false;

        ApplyCollisionRulesForCurrentState();

        ApplyNormalDoorAlignment();
        doorSpineVisual?.SetActive(false);
        doorEffectVisual?.SetActive(false);
        SetStunnedEffectActive(false);
        bodyVisual?.SetActive(true);
        PlayBodyIdleWithoutMix();
        doorSpine?.PlayAnimation(doorIdleAnimationName, true, false);
        RestoreLockVisual();

        RestoreBoxPoseAndPhysics();

        RefreshInputAvailability();
    }

    private void ApplyCollisionRulesForCurrentState()
    {
        ResolveCollisionFilterTargets();

        bool doorUnlocked = state == LevelState.DoorUnlocked;
        if (doorCollider != null)
        {
            // 重建接触对，保证角色已经站在门内时，解锁后仍能收到新的 Trigger 回调。
            doorCollider.enabled = false;
            SetActorLayersExcluded(doorCollider, !doorUnlocked);
            doorCollider.isTrigger = doorUnlocked;
            doorCollider.enabled = true;
        }

        for (int index = 0; index < lockColliders.Length; index++)
            SetActorLayersExcluded(lockColliders[index], true);
    }

    private void ResolveCollisionFilterTargets()
    {
        actorColliderLayerMask = 0;
        int actorLayer = LayerMask.NameToLayer("Actor");
        if (actorLayer >= 0)
        {
            actorColliderLayerMask = 1 << actorLayer;
        }
        else
        {
            ActorCtrl actor = baseLevelCtrl != null ? baseLevelCtrl.Actor : null;
            if (actor == null)
                actor = GetComponentInChildren<ActorCtrl>(true);
            if (actor != null)
                actorColliderLayerMask = 1 << actor.gameObject.layer;
        }

        lockColliders = lockDrag != null
            ? lockDrag.GetComponentsInChildren<Collider2D>(true)
            : System.Array.Empty<Collider2D>();
    }

    private void SetActorLayersExcluded(Collider2D target, bool excluded)
    {
        if (target == null || actorColliderLayerMask == 0)
            return;

        LayerMask excludedLayers = target.excludeLayers;
        excludedLayers.value = excluded
            ? excludedLayers.value | actorColliderLayerMask
            : excludedLayers.value & ~actorColliderLayerMask;
        target.excludeLayers = excludedLayers;
    }

    private void RestoreBoxPoseAndPhysics()
    {

        if (boxDrag != null && hasInitialBoxPose)
        {
            boxDrag.gameObject.SetActive(boxInitialActive);
            boxDrag.transform.SetPositionAndRotation(boxInitialPosition, boxInitialRotation);
            boxDrag.transform.localScale = boxInitialScale;
        }
        else if (boxRigidbody != null)
        {
            boxRigidbody.gameObject.SetActive(true);
        }

        if (boxRigidbody != null)
        {
            boxRigidbody.bodyType = RigidbodyType2D.Dynamic;
            boxRigidbody.simulated = true;
            boxRigidbody.linearVelocity = Vector2.zero;
            boxRigidbody.angularVelocity = 0f;
        }
    }

    private void ApplyNormalDoorAlignment()
    {
        if (doorSpineVisual != null)
            ApplyNormalDoorAlignment(doorSpineVisual.transform);

        // effect 与 door 在共享门 Prefab 中使用相同局部姿态，切换时必须保持同一补偿。
        if (doorEffectVisual != null)
            ApplyNormalDoorAlignment(doorEffectVisual.transform);

        // ribbon 在标准成功流中与 door 叠加，必须使用同一补偿才不会在进门时错位。
        ApplyNormalDoorAlignment(doorRibbonVisual);
    }

    private void ApplyNormalDoorAlignment(Transform visual)
    {
        if (visual == null)
            return;

        visual.localPosition = normalDoorAlignedLocalPosition;
        visual.localScale = normalDoorAlignedLocalScale;
    }

    private void PlayBodyIdleWithoutMix()
    {
        TrackEntry idle = bodySpine?.PlayAnimation(idleAnimationName, true, false);
        if (idle != null)
            idle.MixDuration = 0f;
    }

    private void RefreshInputAvailability()
    {
        bool gameplayInputOpen = IsGameplayInputOpen();
        bool lockInputOpen = gameplayInputOpen &&
                             (state == LevelState.LockedIdle || state == LevelState.StunnedLocked);
        bool boxInputOpen = gameplayInputOpen && state == LevelState.LockedIdle;
        if (lockDrag != null && !lockDragStarted)
            lockDrag.SetEnableDrag(lockInputOpen);
        if (boxDrag != null && !boxDragStarted)
            boxDrag.SetEnableDrag(boxInputOpen);
    }

    private bool IsGameplayInputOpen()
    {
        if (!Application.isPlaying || localInputPaused)
            return false;

        if (!Singleton<PlayCtrl>.IsInitialized)
            return true;

        PlayCtrl playCtrl = PlayCtrl.Instance;
        return !playCtrl.IsInitialized || playCtrl.CheckState(GamePlayStateName.PLAYING);
    }

    #endregion
}
