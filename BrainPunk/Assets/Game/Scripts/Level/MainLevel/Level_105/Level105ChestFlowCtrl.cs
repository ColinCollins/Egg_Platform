using Game.ItemEvent;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level 105 钥匙/宝箱纯表现组件：只处理钥匙接触拾取、跟随、宝箱动画、Shiny 与挂起恢复。
/// 玩法阶段、第五跳资格、1.2 秒时间推进、钥匙数量与胜负仍由唯一 Domain / 主控制器负责；本组件不自建玩法状态机。
/// </summary>
public sealed class Level105ChestFlowCtrl : MonoBehaviour
{
    private const string KeyPickupSound = "getKey";
    private const string ChestOpenSound = "openChest";
    private const string ChestIdleAnimation = "box_idle";
    private const string ChestOpenAnimation = "box-open";
    private const string ChestOpenIdleAnimation = "box-open_idle";

    #region Inspector

    [Header("Actor")]
    /// <summary>Level 105 的 Box；接触检测、钥匙跟随及站位 X 计算均以该 Actor 为准。</summary>
    [SerializeField] private ActorCtrl actor;

    [Header("Key")]
    /// <summary>关卡内钥匙表现根节点；必须与本组件分属不同 GameObject，避免清理钥匙时停用本组件。</summary>
    [SerializeField] private Transform keyRoot;

    /// <summary>钥匙接触范围；第五跳期间由主控制器调用拾取 API，本组件不会自行判断跳跃阶段。</summary>
    [SerializeField] private Collider2D keyCollider;

    /// <summary>钥匙初始轻微浮动组件；Prefab 中应保持 autoStart=true 且 target 指向 keyRoot。</summary>
    [SerializeField] private MoveFloatHandle keyFloatHandle;

    /// <summary>
    /// 复用 Level 18 钥匙实例的 FollowTarget 表现；Prefab 固定接线 offset.y=0.7、smoothSpeed=15，
    /// 未拾取时禁用，拾取后指向 Box。
    /// </summary>
    [SerializeField] private FollowTarget keyFollowTarget;

    [Header("Chest")]
    /// <summary>Level 18 Chest.prefab 在 Level 105 中的实例根；其 Renderer / Collider 会从后代自动解析。</summary>
    [SerializeField] private Transform chestRoot;

    /// <summary>宝箱 Spine 控制器；只播放 box_idle、box-open 与 box-open_idle，不承载触发业务。</summary>
    [SerializeField] private ActorSpineCtrl chestSpine;

    /// <summary>Box 站在宝箱左侧时，Actor Collider 与宝箱可见/碰撞边界之间保留的世界间隙。</summary>
    [SerializeField, Min(0f)] private float chestStandGap = 0.1f;

    [Header("Shiny")]
    /// <summary>开箱瞬间实例化的 Level 18 Shiny.prefab；只生成运行时实例，不加载或修改共享资源。</summary>
    [SerializeField] private GameObject shinyPrefab;

    /// <summary>Shiny 相对宝箱根节点的世界偏移；默认沿用 Level 18 的向上 0.5。</summary>
    [SerializeField] private Vector3 shinyWorldOffset = new Vector3(0f, 0.5f, 0f);

    #endregion

    #region Runtime State

    private Collider2D actorBodyCollider;
    private Renderer[] chestRenderers;
    private Collider2D[] chestColliders;

    private Vector3 initialKeyLocalPosition;
    private Vector3 initialChestLocalScale;
    private bool hasCachedInitialState;
    private bool hasResetAttempt;

    private bool isKeyHeld;
    private bool isKeyFollowing;
    private bool isKeyFloating;
    private bool isChestOpening;
    private bool isChestOpenIdle;
    private bool isSuspended;
    private bool isCleaningUp;

    private Spine.TrackEntry suspendedChestEntry;
    private float suspendedChestTimeScale = 1f;

    private GameObject runtimeShiny;
    private Animator[] shinyAnimators;
    private float[] shinyAnimatorSpeeds;
    private ParticleSystem[] shinyParticles;
    private bool[] shinyParticlesWerePlaying;
    private bool hasShinySuspendSnapshot;

    #endregion

    #region Public API

    /// <summary>
    /// 当前钥匙是否仍以 Level 105 本地表现形态被持有；仅供表现接线观察，Domain 仍是玩法钥匙数量的唯一权威。
    /// </summary>
    public bool HasPresentationKey => isKeyHeld;

    /// <summary>
    /// 重置本次尝试：钥匙回初始位置、显示并恢复浮动，本地表现未持有；宝箱固定朝左并播放 box_idle；清除旧 Shiny。
    /// 调用边界：由 Level105Ctrl 在每次尝试初始化完成后显式调用，不在本组件内推导重开阶段。
    /// </summary>
    public void ResetAttempt()
    {
        if (!Application.isPlaying)
            return;

        CacheInitialState();
        ResolveRuntimeReferences();
        ClearRuntimeShiny();
        RestoreSuspendedChestTrack();

        isKeyHeld = false;
        isKeyFollowing = false;
        isKeyFloating = true;
        isChestOpening = false;
        isChestOpenIdle = false;

        if (keyRoot != null)
        {
            keyRoot.localPosition = initialKeyLocalPosition;
            SetKeyVisible(true);
        }

        if (keyCollider != null)
            keyCollider.enabled = true;

        RefreshKeyFollowState();
        RestartKeyFloat();
        FaceChestLeft();
        PlayChestAnimation(ChestIdleAnimation, true);
    }

    /// <summary>
    /// 检查钥匙是否与 Actor 主体 Collider 实际接触/紧贴；主体 Collider 不可用时，回退判断 Actor 根位置是否落在钥匙 Collider 内。
    /// 本方法不判断“是否第 5 跳”，调用方必须先由唯一玩法流程确认第五跳资格。
    /// </summary>
    public bool IsActorTouchingKey()
    {
        if (!Application.isPlaying || isSuspended || isKeyHeld || isKeyFollowing ||
            actor == null || keyRoot == null || keyCollider == null ||
            !keyRoot.gameObject.activeInHierarchy || !keyCollider.enabled)
        {
            return false;
        }

        ResolveRuntimeReferences();
        if (IsUsableActorBodyCollider(actorBodyCollider))
        {
            if (actorBodyCollider.IsTouching(keyCollider))
                return true;

            ColliderDistance2D distance = actorBodyCollider.Distance(keyCollider);
            float contactDistance = Mathf.Max(0.02f, Physics2D.defaultContactOffset * 2f);
            if (distance.isValid && distance.distance <= contactDistance)
                return true;
        }

        return keyCollider.OverlapPoint(actor.transform.position);
    }

    /// <summary>
    /// 执行第 5 跳自动拾取表现：仅在当前 Collider/位置接触成立时播放 getKey、停止浮动并开始可见跟随 Box。
    /// 不新增 HUD、不暂停 Box，也不负责增加 Domain 钥匙数量；调用方应先让唯一 Domain 接受钥匙事件。
    /// </summary>
    public bool TryPickupKeyOnFifthJump()
    {
        if (!IsActorTouchingKey())
            return false;

        isKeyHeld = true;
        isKeyFollowing = true;
        isKeyFloating = false;

        StopKeyFloat();
        if (keyCollider != null)
            keyCollider.enabled = false;
        SetKeyVisible(true);
        RefreshKeyFollowState();

        AudioManager.PlaySound(KeyPickupSound);
        return true;
    }

    /// <summary>
    /// 消耗跟随中的钥匙并立即开始开箱表现：隐藏钥匙、播放 box-open / openChest，并在同帧实例化 Shiny。
    /// 调用边界：只可由唯一主流程在确认弹窗完全关闭且 Domain 已批准消耗后调用；挂起时拒绝执行。
    /// </summary>
    /// <returns>本地表现确有钥匙且已接受开箱时返回 true；重复、挂起或未持有时返回 false。</returns>
    public bool ConsumeKeyAndOpen()
    {
        if (!Application.isPlaying || isSuspended || !isKeyHeld)
            return false;

        isKeyHeld = false;
        isKeyFollowing = false;
        isKeyFloating = false;
        isChestOpening = true;
        isChestOpenIdle = false;

        StopKeyFloat();
        if (keyCollider != null)
            keyCollider.enabled = false;
        RefreshKeyFollowState();
        SetKeyVisible(false);

        PlayChestAnimation(ChestOpenAnimation, false);
        AudioManager.PlaySound(ChestOpenSound);
        SpawnShiny();
        return true;
    }

    /// <summary>
    /// 切换循环 box-open_idle。本组件不读取 deltaTime、不自行推进 1.2 秒；
    /// 只能由唯一 Domain / 主控制器在 ChestOpening 固定时长完成后下达该表现指令，挂起时拒绝执行。
    /// </summary>
    /// <returns>本次调用实际完成 box-open_idle 切换时返回 true。</returns>
    public bool TrySwitchChestToOpenIdle()
    {
        if (!Application.isPlaying || isSuspended || !isChestOpening || isChestOpenIdle)
            return false;

        isChestOpening = false;
        isChestOpenIdle = true;
        PlayChestAnimation(ChestOpenIdleAnimation, true);
        return true;
    }

    /// <summary>
    /// 冻结/恢复本组件表现：暂停初始钥匙浮动、复用的 FollowTarget、当前宝箱 Spine TrackEntry，
    /// 以及运行时 Shiny 的 Animator 与粒子。本方法不修改 Time.timeScale，也不推进任何玩法阶段。
    /// </summary>
    public void SetSuspended(bool suspended)
    {
        if (!Application.isPlaying)
            return;

        if (isSuspended == suspended)
        {
            RefreshKeyFollowState();
            if (suspended)
            {
                if (isKeyFloating)
                    PauseKeyFloat();
                FreezeCurrentChestTrack();
                FreezeShinyPresentation();
            }
            return;
        }

        isSuspended = suspended;
        RefreshKeyFollowState();
        if (suspended)
        {
            if (isKeyFloating)
                PauseKeyFloat();
            FreezeCurrentChestTrack();
            FreezeShinyPresentation();
            return;
        }

        RestoreSuspendedChestTrack();
        RestoreShinyPresentation();
        if (isKeyFloating)
            ResumeKeyFloat();
    }

    /// <summary>
    /// 计算 Box 根节点在宝箱左侧的无重叠世界 X：宝箱边界取可见 Renderer 与仍启用 Collider 的并集，
    /// 再扣除 Actor 主体 Collider 的右边界偏移及序列化 gap。只计算标定，不移动 Actor。
    /// </summary>
    /// <param name="actorRootX">成功时返回应设置给 Actor 根节点的世界 X。</param>
    /// <returns>Actor 主体 Collider 与宝箱有效边界均可解析时返回 true，否则返回 false。</returns>
    public bool TryGetActorStandXLeftOfChest(out float actorRootX)
    {
        actorRootX = actor != null ? actor.transform.position.x : 0f;
        ResolveRuntimeReferences();

        if (actor == null || !IsUsableActorBodyCollider(actorBodyCollider) ||
            !TryGetChestLeftEdge(out float chestLeftEdge))
        {
            return false;
        }

        float actorRightOffsetFromRoot = actorBodyCollider.bounds.max.x - actor.transform.position.x;
        actorRootX = chestLeftEdge - Mathf.Max(0f, chestStandGap) - actorRightOffsetFromRoot;
        return true;
    }

    /// <summary>中止本次表现并释放运行时对象；用于失败、重开前或外部流程终止，不触发任何玩法结果。</summary>
    public void Abort()
    {
        Cleanup();
    }

    /// <summary>停止钥匙浮动/跟随、恢复被本组件冻结的播放速度并清除运行时 Shiny；可重复调用。
    /// 钥匙保持当前位置可见、不随死亡/收尾隐藏（2026-08-07 用户确认，与宝箱「不消失」口径一致）；
    /// 每次尝试初始化由 ResetAttempt 统一归位重置，不在此隐藏。</summary>
    public void Cleanup()
    {
        if (!Application.isPlaying || isCleaningUp)
            return;

        isCleaningUp = true;
        RestoreSuspendedChestTrack();
        RestoreShinyPresentation();
        isSuspended = false;

        isKeyHeld = false;
        isKeyFollowing = false;
        isKeyFloating = false;
        isChestOpening = false;
        isChestOpenIdle = false;

        StopKeyFloat();
        if (keyCollider != null)
            keyCollider.enabled = false;
        RefreshKeyFollowState();
        ClearRuntimeShiny();
        isCleaningUp = false;
    }

    #endregion

    #region Unity Messages

    private void Awake()
    {
        CacheInitialState();
        ResolveRuntimeReferences();
    }

    private void OnDisable()
    {
        Cleanup();
    }

    private void OnDestroy()
    {
        Cleanup();
    }

    #endregion

    #region Key Presentation

    private void RestartKeyFloat()
    {
        if (keyFloatHandle == null)
            return;

        keyFloatHandle.enabled = true;
        if (hasResetAttempt)
        {
            keyFloatHandle.ResetToStart();
            keyFloatHandle.StartMove();
        }
        else
        {
            // 首次尝试沿用 Key.prefab 的 autoStart，避免与 MoveFloatHandle.Start 的脚本顺序竞争。
            keyFloatHandle.ResumeMove();
        }

        hasResetAttempt = true;
        if (isSuspended)
            PauseKeyFloat();
    }

    private void PauseKeyFloat()
    {
        if (keyFloatHandle == null)
            return;

        keyFloatHandle.PauseMove();
        keyFloatHandle.enabled = false;
    }

    private void ResumeKeyFloat()
    {
        if (keyFloatHandle == null)
            return;

        keyFloatHandle.enabled = true;
        keyFloatHandle.ResumeMove();
    }

    private void StopKeyFloat()
    {
        if (keyFloatHandle == null)
            return;

        keyFloatHandle.StopMove();
        keyFloatHandle.enabled = false;
    }

    private void SetKeyVisible(bool visible)
    {
        if (keyRoot != null && keyRoot.gameObject.activeSelf != visible)
            keyRoot.gameObject.SetActive(visible);
    }

    private void RefreshKeyFollowState()
    {
        if (keyFollowTarget == null)
            return;

        keyFollowTarget.SetTarget(isKeyFollowing && actor != null ? actor.transform : null);
        keyFollowTarget.enabled = isKeyFollowing && !isSuspended;
    }

    #endregion

    #region Chest Presentation

    private void FaceChestLeft()
    {
        if (chestRoot == null)
            return;

        Vector3 scale = initialChestLocalScale;
        float parentScaleX = chestRoot.parent != null ? chestRoot.parent.lossyScale.x : 1f;
        // Chest 美术原生正 scale 即朝左（2026-08-04 preview 实测：负 scale 呈现为朝右）；
        // 父节点世界 scale 为负时取负局部 scale 补偿，保证世界朝向恒为左。
        float leftFacingLocalSign = parentScaleX < 0f ? -1f : 1f;
        scale.x = Mathf.Abs(initialChestLocalScale.x) * leftFacingLocalSign;
        chestRoot.localScale = scale;
    }

    private Spine.TrackEntry PlayChestAnimation(string animationName, bool loop)
    {
        RestoreSuspendedChestTrack();
        Spine.TrackEntry entry = chestSpine != null
            ? chestSpine.PlayAnimation(animationName, loop, false)
            : null;

        if (isSuspended)
            FreezeCurrentChestTrack();

        return entry;
    }

    private void FreezeCurrentChestTrack()
    {
        RestoreSuspendedChestTrack();
        suspendedChestEntry = chestSpine?.AnimationState?.GetCurrent(0);
        if (suspendedChestEntry == null)
            return;

        suspendedChestTimeScale = suspendedChestEntry.TimeScale;
        suspendedChestEntry.TimeScale = 0f;
    }

    private void RestoreSuspendedChestTrack()
    {
        if (suspendedChestEntry != null)
            suspendedChestEntry.TimeScale = suspendedChestTimeScale;

        suspendedChestEntry = null;
        suspendedChestTimeScale = 1f;
    }

    #endregion

    #region Shiny Presentation

    private void SpawnShiny()
    {
        ClearRuntimeShiny();
        if (!Application.isPlaying || shinyPrefab == null || chestRoot == null)
            return;

        runtimeShiny = Instantiate(shinyPrefab, transform);
        runtimeShiny.transform.position = chestRoot.position + shinyWorldOffset;
        runtimeShiny.SetActive(true);

        shinyAnimators = runtimeShiny.GetComponentsInChildren<Animator>(true);
        shinyAnimatorSpeeds = new float[shinyAnimators.Length];
        shinyParticles = runtimeShiny.GetComponentsInChildren<ParticleSystem>(true);
        shinyParticlesWerePlaying = new bool[shinyParticles.Length];
        hasShinySuspendSnapshot = false;

        if (isSuspended)
            FreezeShinyPresentation();
    }

    private void FreezeShinyPresentation()
    {
        if (runtimeShiny == null || hasShinySuspendSnapshot)
            return;

        for (int i = 0; shinyAnimators != null && i < shinyAnimators.Length; i++)
        {
            Animator animator = shinyAnimators[i];
            if (animator == null)
                continue;

            shinyAnimatorSpeeds[i] = animator.speed;
            animator.speed = 0f;
        }

        for (int i = 0; shinyParticles != null && i < shinyParticles.Length; i++)
        {
            ParticleSystem particle = shinyParticles[i];
            if (particle == null)
                continue;

            shinyParticlesWerePlaying[i] = particle.isPlaying;
            if (shinyParticlesWerePlaying[i])
                particle.Pause(false);
        }

        hasShinySuspendSnapshot = true;
    }

    private void RestoreShinyPresentation()
    {
        if (!hasShinySuspendSnapshot)
            return;

        for (int i = 0; shinyAnimators != null && i < shinyAnimators.Length; i++)
        {
            Animator animator = shinyAnimators[i];
            if (animator != null && shinyAnimatorSpeeds != null && i < shinyAnimatorSpeeds.Length)
                animator.speed = shinyAnimatorSpeeds[i];
        }

        for (int i = 0; shinyParticles != null && i < shinyParticles.Length; i++)
        {
            ParticleSystem particle = shinyParticles[i];
            if (particle != null && shinyParticlesWerePlaying != null &&
                i < shinyParticlesWerePlaying.Length && shinyParticlesWerePlaying[i])
            {
                particle.Play(false);
            }
        }

        hasShinySuspendSnapshot = false;
    }

    private void ClearRuntimeShiny()
    {
        RestoreShinyPresentation();

        GameObject shiny = runtimeShiny;
        runtimeShiny = null;
        shinyAnimators = null;
        shinyAnimatorSpeeds = null;
        shinyParticles = null;
        shinyParticlesWerePlaying = null;
        hasShinySuspendSnapshot = false;

        if (shiny == null)
            return;

        shiny.SetActive(false);
        if (Application.isPlaying)
            Destroy(shiny);
    }

    #endregion

    #region Reference And Bounds Helpers

    private void CacheInitialState()
    {
        if (hasCachedInitialState)
            return;

        if (keyRoot != null)
            initialKeyLocalPosition = keyRoot.localPosition;
        if (chestRoot != null)
            initialChestLocalScale = chestRoot.localScale;

        hasCachedInitialState = true;
    }

    private void ResolveRuntimeReferences()
    {
        if (!IsUsableActorBodyCollider(actorBodyCollider))
            actorBodyCollider = ResolveActorBodyCollider();

        if (chestRoot == null)
            return;

        if (chestRenderers == null || chestRenderers.Length == 0)
            chestRenderers = chestRoot.GetComponentsInChildren<Renderer>(true);
        if (chestColliders == null || chestColliders.Length == 0)
            chestColliders = chestRoot.GetComponentsInChildren<Collider2D>(true);
    }

    private Collider2D ResolveActorBodyCollider()
    {
        if (actor == null)
            return null;

        Collider2D rootCollider = actor.GetComponent<Collider2D>();
        if (IsUsableActorBodyCollider(rootCollider))
            return rootCollider;

        Collider2D[] candidates = actor.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            if (IsUsableActorBodyCollider(candidates[i]))
                return candidates[i];
        }

        return null;
    }

    private static bool IsUsableActorBodyCollider(Collider2D candidate)
    {
        return candidate != null && candidate.enabled && !candidate.isTrigger && candidate.gameObject.activeInHierarchy;
    }

    private bool TryGetChestLeftEdge(out float chestLeftEdge)
    {
        chestLeftEdge = 0f;
        bool hasBounds = false;

        if (chestRenderers != null)
        {
            for (int i = 0; i < chestRenderers.Length; i++)
            {
                Renderer renderer = chestRenderers[i];
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                    continue;

                IncludeLeftEdge(renderer.bounds, ref hasBounds, ref chestLeftEdge);
            }
        }

        if (chestColliders != null)
        {
            for (int i = 0; i < chestColliders.Length; i++)
            {
                Collider2D collider = chestColliders[i];
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                    continue;

                IncludeLeftEdge(collider.bounds, ref hasBounds, ref chestLeftEdge);
            }
        }

        return hasBounds;
    }

    private static void IncludeLeftEdge(Bounds bounds, ref bool hasBounds, ref float leftEdge)
    {
        float minX = bounds.min.x;
        if (float.IsNaN(minX) || float.IsInfinity(minX) || bounds.size.sqrMagnitude <= Mathf.Epsilon)
            return;

        leftEdge = hasBounds ? Mathf.Min(leftEdge, minX) : minX;
        hasBounds = true;
    }

    #endregion
}
