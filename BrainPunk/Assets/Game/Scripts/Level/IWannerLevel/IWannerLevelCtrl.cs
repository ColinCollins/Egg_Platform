using System;
using System.Threading;
using Bear.EventSystem;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using Game.Common;
using Game.Events;
using MoreMountains.Feedbacks;
using UnityEngine;
using Game.Scripts.Common;



#if UNITY_EDITOR
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEditor.Events;
#endif

/// <summary>
/// IWanner 单个小关的一次性结算类型。
/// </summary>
public enum IWannerLevelResultType
{
    Success,
    Failed,
}

/// <summary>
/// IWanner 关卡本地机制上报给 IWannerPlayCtrl 的一次性结算结果。
/// </summary>
public readonly struct IWannerLevelResult
{
    #region Public Properties

    /// <summary>本次结算是成功还是失败。</summary>
    public IWannerLevelResultType Type { get; }

    /// <summary>触发结算的稳定本地来源标识，仅用于日志与问题定位。</summary>
    public string Source { get; }

    #endregion

    #region Constructor

    /// <summary>
    /// 用途：创建一次仅在当前关卡实例内传播的结算结果。
    /// 调用边界：由 IWannerLevelCtrl 在首次接受成功或失败条件时创建。
    /// 注意事项：Source 为空时会归一为空字符串，不承载跨关卡状态。
    /// </summary>
    public IWannerLevelResult(IWannerLevelResultType type, string source)
    {
        Type = type;
        Source = source ?? string.Empty;
    }

    #endregion
}

/// <summary>
/// IWanner 单个小关的局部运行时控制器，负责角色输入、暂停、成功门提交和逐帧驱动。
/// 调用边界：由 IWannerPlayCtrl 在关卡 prefab 实例化后初始化；不读取配置、存档或玩法全局状态。
/// </summary>
[DisallowMultipleComponent]
public sealed class IWannerLevelCtrl : MonoBehaviour, IDebuger
{
    #region Result Event

    /// <summary>
    /// 当前关卡实例首次接受成功或失败条件时触发。
    /// 调用边界：IWannerPlayCtrl 必须在 InitializeRuntime() 前订阅，并在销毁关卡前解除订阅。
    /// 注意事项：这是关卡实例级同步通知，不通过全局 PlayEvents 传播，也不负责异步推进。
    /// </summary>
    public event Action<IWannerLevelResult> Resolved;

    #endregion

    #region References

    /// <summary>
    /// 当前 IWanner 场景中需要驱动的角色；为空时仅回退查找同一 GameObject 上的 ActorCtrl。
    /// </summary>
    [SerializeField] private ActorCtrl actor;

    /// <summary>
    /// 当前小关成功门上的触发器；Success API 只接收该触发器提交的玩家 Collider。
    /// </summary>
    [SerializeField] private OnTrigger2DHandle successDoorTrigger;

    /// <summary>
    /// 当前成功门的进入表现控制器；必须完成该表现后才通知玩法控制器切换小关。
    /// </summary>
    [SerializeField] private IWannerGame_EnterDoorAnimCtrl enterDoorAnimCtrl;

    [SerializeField] private Vector3 actorOffset = new Vector3(0, -0.3f, 0);

    /// <summary>
    /// Actor 死亡或成功进入门时播放的局部反馈；当前关卡使用 MMF_CameraShake 实现震屏。
    /// 字段名为兼容已有 prefab 序列化保持不变；未绑定时运行时会优先查找名为 Feedback 的子物体作为兜底。
    /// </summary>
    [SerializeField] private MMF_Player deathFeedback;

    /// <summary>
    /// 当前小关的场景与 HUD 配色资产；每个关卡 prefab 可绑定不同资产以复用颜色组合。
    /// </summary>
    [Header("关卡配色")]
    [SerializeField] private IWannerLevelColorSettings colorSettings;

    #endregion

    #region Runtime State

    private EventSubscriber _subscriber;
    private bool _isRuntimeReady;
    private bool _isInputEnabled;
    private bool _isPaused;
    private bool _isResultSubmitted;
    private bool _hasRuntimeDoorListener;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveReferences();

        if (actor != null)
            return;

        this.LogError("[IWannerLevelCtrl] ActorCtrl is missing; level runtime is disabled.");
        enabled = false;
    }

    private void OnEnable()
    {
        if (actor == null)
            return;

        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<PlayerRightMoveEvent>(OnPlayerRightMove);
        _subscriber.Subscribe<PlayerLeftMoveEvent>(OnPlayerLeftMove);
        _subscriber.Subscribe<PlayerMoveCancelEvent>(OnPlayerMoveCancel);
        _subscriber.Subscribe<PlayerJumpEvent>(OnPlayerJump);
        _subscriber.Subscribe<OnTriggerTrapEvent>(OnTriggerTrap);
        _subscriber.Subscribe<OnTriggerFailAreaEvent>(OnTriggerFailArea);
    }

    private void Update()
    {
        if (_isRuntimeReady && !_isPaused)
            actor?.OnUpdate();
    }

    private void OnDisable()
    {
        ShutdownRuntime();
        EventsUtils.ResetEvents(ref _subscriber);
    }

    #endregion

    #region Runtime API

    /// <summary>
    /// 用途：动态指定当前成功门触发器，可绑定到关卡本地 UnityEvent。
    /// 调用边界：仅 PlayMode 且尚未结算时有效；运行时已就绪时立即绑定新门。
    /// 注意事项：移除旧门的运行时 Success 监听并关闭指向本控制器的持久化 Success 回调，保留其它回调。
    /// 新门的同类持久化回调会启用；请同时配置同一扇门的进入动画，空引用不接受。
    /// </summary>
    public void SetSuccessDoorTrigger(OnTrigger2DHandle doorTrigger)
    {
        if (!Application.isPlaying || _isResultSubmitted || doorTrigger == null)
            return;

        UnbindRuntimeDoorTrigger();
        SetDoorSuccessListenerState(successDoorTrigger, UnityEngine.Events.UnityEventCallState.Off);
        successDoorTrigger = doorTrigger;
        SetDoorSuccessListenerState(successDoorTrigger, UnityEngine.Events.UnityEventCallState.RuntimeOnly);
        if (_isRuntimeReady)
            BindDoorTriggerAtRuntime();
    }

    /// <summary>
    /// 用途：动态指定成功后的进入门动画，可绑定到关卡本地 UnityEvent。
    /// 调用边界：仅 PlayMode 且尚未结算时有效，不立即播放或重置动画。
    /// 注意事项：同时影响成功时角色的门前定位；应与 SetSuccessDoorTrigger 指向同一扇门，空引用不接受。
    /// </summary>
    public void SetEnterDoorAnimCtrl(IWannerGame_EnterDoorAnimCtrl doorAnimCtrl)
    {
        if (!Application.isPlaying || _isResultSubmitted || doorAnimCtrl == null)
            return;

        enterDoorAnimCtrl = doorAnimCtrl;
    }

    /// <summary>
    /// 用途：标识当前 IWanner 小关是否反向映射玩家的左右移动事件。
    /// 调用边界：由关卡外部逻辑通过 SetReverse(bool) 设置；其它系统只读取当前状态。
    /// 注意事项：默认值为 false，仅影响 PlayerRightMoveEvent 与 PlayerLeftMoveEvent，不改变 SetMoveInput(float) 的方向语义。
    /// </summary>
    public bool IsReverse { get; private set; }

    /// <summary>
    /// 用途：标识当前 IWanner 小关是否允许 Actor 响应玩家跳跃事件。
    /// 调用边界：由关卡外部逻辑通过 SetJumpEnabled(bool) 设置；其它系统只读取当前状态。
    /// 注意事项：默认值为 true，仅控制当前 LevelCtrl 驱动的 Actor，不阻止其它局部 Listener 响应 PlayerJumpEvent。
    /// </summary>
    public bool IsJumpEnabled { get; private set; } = true;

    /// <summary>
    /// 用途：供关卡外部逻辑开启或关闭左右移动事件的反向映射。
    /// 调用边界：可在当前 IWanner 小关初始化前或运行期间调用，不负责开启输入或解除暂停。
    /// 注意事项：运行时持续按住方向按钮时，下一帧收到的移动事件会按新值生效。
    /// </summary>
    /// <param name="isReverse">true 表示右移事件触发左移、左移事件触发右移；false 保持默认方向。</param>
    public void SetReverse(bool isReverse)
    {
        IsReverse = isReverse;
    }

    /// <summary>
    /// 用途：供关卡外部逻辑开启或关闭当前 Actor 的跳跃响应。
    /// 调用边界：可在当前 IWanner 小关初始化前或运行期间调用，不负责修改 HUD、暂停状态或 Rigidbody2D 当前速度。
    /// 注意事项：设置为 false 只拒绝后续跳跃请求，不会中断已经发生的跳跃；新关卡实例默认恢复为 true。
    /// </summary>
    /// <param name="isEnabled">true 表示 Actor 可响应跳跃；false 表示 Actor 忽略跳跃请求。</param>
    public void SetJumpEnabled(bool isEnabled)
    {
        IsJumpEnabled = isEnabled;
    }

    /// <summary>
    /// 用途：在关卡 prefab 实例化完成后开放当前 IWanner 小关运行时。
    /// 调用边界：每个实例仅由活动中的 IWannerPlayCtrl 调用一次；编辑态和场景预览不得调用。
    /// 注意事项：调用方必须提前订阅 Resolved；缺少 ActorCtrl 或结果订阅者时返回 false，并由调用方清理实例。
    /// </summary>
    public bool InitializeRuntime()
    {
        if (!Application.isPlaying)
            return false;

        ResolveReferences();
        if (actor == null || Resolved == null)
            return false;

        _isResultSubmitted = false;
        _isRuntimeReady = true;
        _isPaused = false;
        ApplySceneColorSettings();
        BindDoorTriggerAtRuntime();
        SetInputEnabled(true);
        return true;
    }

    /// <summary>
    /// 用途：供成功门的 OnTrigger2DHandle.OnEnter 持久化调用，提交一次玩家到达出口的请求。
    /// 调用边界：由 AutoBind 绑定到当前小关成功门；非 Player Collider 会被忽略。
    /// 注意事项：本方法只做 UnityEvent 适配，实际幂等判断由 TrySuccess() 统一处理。
    /// </summary>
    /// <param name="collider">进入成功门触发区域的碰撞体。</param>
    public void Success(Collider2D collider)
    {
        if (collider == null || !collider.CompareTag("Player"))
            return;

        TrySuccess();
    }

    /// <summary>
    /// 用途：幂等提交当前 IWanner 小关成功，并立即锁定角色输入。
    /// 调用边界：由成功门 UnityEvent 或关卡内明确的成功条件调用；每个小关实例只接受一次。
    /// 注意事项：Actor 同步到门后会关闭物理碰撞，并触发一次震屏和中等手机震动；门动画和下一关推进由 IWannerPlayCtrl 串行处理。
    /// </summary>
    /// <returns>本次成功请求被接受时返回 true；运行时未就绪、暂停或已经结算时返回 false。</returns>
    public bool TrySuccess()
    {
        if (!_isRuntimeReady || _isPaused || _isResultSubmitted)
            return false;

        _isResultSubmitted = true;
        SetInputEnabled(false);
        actor?.StopMoving();
        SnapActorToSuccessDoor();
        DisableActorCollisionAfterDoorEntry();
        PlayEnterDoorImpactFeedback();

        AudioManager.PlaySound("iwannar_past");

        Resolved?.Invoke(new IWannerLevelResult(
            IWannerLevelResultType.Success,
            nameof(TrySuccess)));

        return true;
    }

    /// <summary>
    /// 用途：供关卡内死亡触发器的 OnTrigger2DHandle.OnEnter 持久化调用，提交一次玩家死亡请求。
    /// 调用边界：仅接受 Player Collider；非玩家碰撞体会被忽略。
    /// 注意事项：本方法只做 UnityEvent 适配，死亡幂等、输入锁定和震屏统一由 TryActorDeath() 处理。
    /// </summary>
    /// <param name="collider">进入死亡触发区域的碰撞体。</param>
    public void ActorDeath(Collider2D collider)
    {
        if (collider == null || !collider.CompareTag("Player"))
            return;

        TryActorDeath();
    }

    /// <summary>
    /// 用途：幂等触发当前 IWanner Actor 死亡，并播放关卡内配置的震屏 Feedback。
    /// 调用边界：由关卡死亡触发器或明确的玩法失败条件调用；每个小关实例只接受一次。
    /// 注意事项：死亡后只同步上报实例级结果；本类不读取失败阈值或决定 Skip 显示。
    /// </summary>
    /// <returns>本次死亡请求被接受时返回 true；运行时未就绪、暂停、已结算或缺少 Actor 时返回 false。Actor 已由外部陷阱置为死亡时仍可提交失败，但不会重复播放死亡动画。</returns>
    public bool TryActorDeath()
    {
        if (!_isRuntimeReady ||
            _isPaused ||
            _isResultSubmitted ||
            actor == null)
        {
            return false;
        }

        _isResultSubmitted = true;
        SetInputEnabled(false);
        if (!actor.IsDied)
            actor.Die();

        if (deathFeedback != null)
            deathFeedback.PlayFeedbacks(actor.transform.position);
        else
            this.LogWarning("[IWannerLevelCtrl] Death feedback is missing; actor death continues without screen shake.");

        AudioManager.PlaySound("failed");

        Resolved?.Invoke(new IWannerLevelResult(
            IWannerLevelResultType.Failed,
            nameof(TryActorDeath)));
        return true;
    }

    /// <summary>
    /// 用途：执行并等待当前入口门关闭及特效渐隐表现。
    /// 调用边界：仅由当前活动 IWannerPlayCtrl 在接受本关成功后调用。
    /// 注意事项：缺少入口门表现组件时记录错误并返回 false，调用方应采用不阻塞清理的降级策略。
    /// </summary>
    /// <param name="cancellationToken">当前小关销毁或玩法退出时取消等待。</param>
    /// <returns>入口门表现正常完成时返回 true，否则返回 false。</returns>
    public async UniTask<bool> PlayEnterDoorAnimationAsync(CancellationToken cancellationToken)
    {
        if (enterDoorAnimCtrl == null)
        {
            this.LogError("[IWannerLevelCtrl] IWannerGame_EnterDoorAnimCtrl is missing; success presentation skipped.");
            return false;
        }

        return await enterDoorAnimCtrl.PlayAsync(cancellationToken);
    }

    /// <summary>
    /// 用途：统一开启或关闭当前小关的角色输入。
    /// 调用边界：由 IWannerPlayCtrl 在加载、暂停、Reset 和退出边界调用。
    /// 注意事项：关闭时会立即清空水平输入；成功已提交时即使调用方请求开启也保持锁定。
    /// </summary>
    public void SetInputEnabled(bool enabledInput)
    {
        _isInputEnabled = _isRuntimeReady && !_isResultSubmitted && enabledInput;
        if (!_isInputEnabled && actor != null)
            actor.SetMoveInput(0f);
    }

    /// <summary>
    /// 用途：设置当前 IWanner 小关的暂停状态。
    /// 调用边界：仅由活动 IWannerPlayCtrl 的 Pause/Resume 调用。
    /// 注意事项：暂停会停止角色物理移动；恢复时仅在输入仍开放的情况下恢复角色移动能力。
    /// </summary>
    public void SetPaused(bool paused)
    {
        if (!_isRuntimeReady || actor == null)
            return;

        _isPaused = paused;
        actor.SetMoveInput(0f);
        if (paused)
            actor.StopMoving();
        else if (_isInputEnabled)
            actor.ResumeMoving();
    }

    /// <summary>
    /// 用途：写入当前角色的水平移动方向。
    /// 调用边界：由本控制器收到主线同款玩家移动事件后调用。
    /// 注意事项：非活动、暂停或输入关闭时会忽略非零输入，但仍允许归零。
    /// </summary>
    public void SetMoveInput(float direction)
    {
        if (actor == null)
            return;

        if (!CanAcceptInput() && !Mathf.Approximately(direction, 0f))
            return;

        actor.SetMoveInput(Mathf.Clamp(direction, -1f, 1f));
    }

    /// <summary>
    /// 用途：请求当前角色执行一次跳跃。
    /// 调用边界：由本控制器收到主线同款 PlayerJumpEvent 后调用。
    /// 注意事项：Actor_IWanner 默认仅在接地时接受请求，实例开启无限跳跃后可在空中接受；持续按住不会由本接口重复触发，重复策略由 HUD 的离散按下事件保证。
    /// </summary>
    public void TriggerJump()
    {
        if (!IsJumpEnabled || !CanAcceptInput())
            return;

        if (actor is Actor_IWanner iwannerActor)
        {
            iwannerActor.TriggerGroundedJump();
            return;
        }

        actor?.TriggerJump();
    }

    /// <summary>
    /// 用途：清理当前小关运行时输入与暂停状态。
    /// 调用边界：Reset、退出、玩法切换和 GameObject 禁用时调用，可重复执行。
    /// </summary>
    public void ShutdownRuntime()
    {
        UnbindRuntimeDoorTrigger();
        _isResultSubmitted = false;
        _isInputEnabled = false;
        _isPaused = false;
        _isRuntimeReady = false;
        enterDoorAnimCtrl?.ResetState();
        if (actor != null)
        {
            actor.SetMoveInput(0f);
            actor.StopMoving();
        }
    }

    /// <summary>
    /// 用途：向当前 IWanner GameplayPanel 提供本关绑定的 HUD 图片、Skip 文本与 Ground 颜色。
    /// 调用边界：仅由活动 IWannerPlayCtrl 在关卡初始化成功后读取。
    /// 注意事项：未绑定配色资产时返回 false，调用方应保留 GameplayPanel prefab 原始颜色。
    /// </summary>
    internal bool TryGetGameplayPanelColors(
        out Color gameplayPanelColor,
        out Color skipTextColor,
        out Color groundColor)
    {
        if (colorSettings == null)
        {
            gameplayPanelColor = Color.white;
            skipTextColor = Color.white;
            groundColor = Color.white;
            return false;
        }

        gameplayPanelColor = colorSettings.GameplayPanelColor;
        skipTextColor = colorSettings.SkipTextColor;
        groundColor = colorSettings.GroundColor;
        return true;
    }

    private bool CanAcceptInput()
    {
        return _isRuntimeReady && _isInputEnabled && !_isPaused && actor != null;
    }

    #endregion

    #region Runtime Helpers

    private void OnTriggerTrap(OnTriggerTrapEvent evt)
    {
        TryActorDeath();
    }

    private void OnTriggerFailArea(OnTriggerFailAreaEvent evt)
    {
        TryActorDeath();
    }

    private void PlayEnterDoorImpactFeedback()
    {
        if (deathFeedback != null)
        {
            Vector3 feedbackPosition = actor != null
                ? actor.transform.position
                : transform.position;
            deathFeedback.PlayFeedbacks(feedbackPosition);
        }
        else
        {
            this.LogWarning(
                "[IWannerLevelCtrl] Local screen shake feedback is missing; enter door continues without screen shake.");
        }

        VibrationManager.Instance.Vibrate(VibrationIntensity.Medium);
    }

    private void ResolveReferences()
    {
        if (actor == null)
            actor = GetComponentInChildren<ActorCtrl>(true);

        if (enterDoorAnimCtrl == null)
            enterDoorAnimCtrl = GetComponentInChildren<IWannerGame_EnterDoorAnimCtrl>(true);

        if (successDoorTrigger == null && enterDoorAnimCtrl != null)
            successDoorTrigger = enterDoorAnimCtrl.GetComponent<OnTrigger2DHandle>();

        if (deathFeedback == null)
        {
            Transform feedbackRoot = FindChildByName(transform, "Feedback");
            if (feedbackRoot != null)
                deathFeedback = feedbackRoot.GetComponent<MMF_Player>();
        }
    }

    private void ApplySceneColorSettings()
    {
        if (colorSettings == null)
            return;

        Camera levelCamera = GetComponentInChildren<Camera>(true);
        if (levelCamera != null)
            levelCamera.backgroundColor = colorSettings.CameraSolidColor;
        else
            this.LogWarning("[IWannerLevelCtrl] Level Camera is missing; camera color was not applied.");

        ApplyButtonColorSettings();

        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        int appliedGroundCount = 0;
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null)
                continue;

            bool isGroundRenderer = spriteRenderer.gameObject.name
                .StartsWith("Ground", StringComparison.Ordinal);

            if (isGroundRenderer)
            {
                spriteRenderer.color = colorSettings.GroundColor;
                appliedGroundCount++;
                continue;
            }

            bool isTrapRenderer =
                spriteRenderer.GetComponentInParent<OnTriggerTrapHandle>(true) != null;

            bool isTrap = spriteRenderer.transform.parent.gameObject.name.Contains("IWannerGame_Trap") || spriteRenderer.transform.parent.gameObject.name.Contains("IWannerGame_Saws");
            if (isTrapRenderer && isTrap)
            {
                spriteRenderer.color = colorSettings.GroundColor;
                continue;
            }

            bool isSpringRenderer =
                spriteRenderer.GetComponentInParent<IWannerGame_SpingTriggerCtrl>(true) != null;
            if (isSpringRenderer)
            {
                spriteRenderer.color = colorSettings.GroundColor;
                continue;
            }
        }

        if (appliedGroundCount == 0)
            this.LogWarning("[IWannerLevelCtrl] No Ground SpriteRenderer was found; ground color was not applied.");
    }

    private void ApplyButtonColorSettings()
    {
        SpriteRendererGroupColor[] colorGroups =
            GetComponentsInChildren<SpriteRendererGroupColor>(true);
        for (int i = 0; i < colorGroups.Length; i++)
        {
            SpriteRendererGroupColor colorGroup = colorGroups[i];
            if (colorGroup == null)
                continue;

            bool belongsToButton =
                colorGroup.GetComponentInParent<IWannerGame_Button>(true) != null ||
                string.Equals(
                    colorGroup.gameObject.name,
                    nameof(IWannerGame_Button),
                    StringComparison.Ordinal);
            if (belongsToButton)
                colorGroup.SetColor(colorSettings.GroundColor);
        }
    }

    private void SnapActorToSuccessDoor()
    {
        Transform doorTransform = enterDoorAnimCtrl != null
            ? enterDoorAnimCtrl.transform
            : successDoorTrigger?.transform;
        if (actor == null || doorTransform == null)
        {
            this.LogWarning("[IWannerLevelCtrl] Actor or success door is missing; actor snap skipped.");
            return;
        }

        Vector3 targetPosition = doorTransform.position;
        targetPosition.z = actor.transform.position.z;

        Rigidbody2D actorRigidbody = actor.RB != null
            ? actor.RB
            : actor.GetComponent<Rigidbody2D>();
        if (actorRigidbody != null)
        {
            actorRigidbody.linearVelocity = Vector2.zero;
            actorRigidbody.angularVelocity = 0f;
            actorRigidbody.position = new Vector2(targetPosition.x, targetPosition.y);
        }

        actor.transform.position = targetPosition + actorOffset;
        Physics2D.SyncTransforms();
        actor.UpdateAnimation();
    }

    /// <summary>
    /// QBox 进入成功门后关闭当前 Actor 的全部 2D 碰撞与刚体模拟，避免进门表现期间继续参与物理接触。
    /// 当前小关随后会销毁并重建，因此不在旧实例上恢复碰撞状态。
    /// </summary>
    private void DisableActorCollisionAfterDoorEntry()
    {
        if (actor == null)
            return;

        Collider2D[] actorColliders = actor.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < actorColliders.Length; i++)
        {
            Collider2D actorCollider = actorColliders[i];
            if (actorCollider != null)
                actorCollider.enabled = false;
        }

        Rigidbody2D actorRigidbody = actor.RB != null
            ? actor.RB
            : actor.GetComponent<Rigidbody2D>();
        if (actorRigidbody == null)
            return;

        actorRigidbody.linearVelocity = Vector2.zero;
        actorRigidbody.angularVelocity = 0f;
        actorRigidbody.simulated = false;
    }

    private void BindDoorTriggerAtRuntime()
    {
        if (successDoorTrigger == null)
        {
            this.LogWarning("[IWannerLevelCtrl] Success door trigger is missing; this level cannot submit door success.");
            return;
        }

        if (HasPersistentSuccessListener(successDoorTrigger))
            return;

        successDoorTrigger.OnEnter.RemoveListener(Success);
        successDoorTrigger.OnEnter.AddListener(Success);
        _hasRuntimeDoorListener = true;
    }

    private void UnbindRuntimeDoorTrigger()
    {
        if (_hasRuntimeDoorListener && successDoorTrigger != null)
            successDoorTrigger.OnEnter.RemoveListener(Success);

        _hasRuntimeDoorListener = false;
    }

    private bool HasPersistentSuccessListener(OnTrigger2DHandle trigger)
    {
        if (trigger?.OnEnter == null)
            return false;

        for (int i = 0; i < trigger.OnEnter.GetPersistentEventCount(); i++)
        {
            if (trigger.OnEnter.GetPersistentTarget(i) == this &&
                string.Equals(
                    trigger.OnEnter.GetPersistentMethodName(i),
                    nameof(Success),
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void SetDoorSuccessListenerState(
        OnTrigger2DHandle trigger, UnityEngine.Events.UnityEventCallState state)
    {
        if (trigger?.OnEnter == null)
            return;

        for (int i = 0; i < trigger.OnEnter.GetPersistentEventCount(); i++)
        {
            if (trigger.OnEnter.GetPersistentTarget(i) == this &&
                string.Equals(trigger.OnEnter.GetPersistentMethodName(i), nameof(Success), StringComparison.Ordinal))
            {
                trigger.OnEnter.SetPersistentListenerState(i, state);
            }
        }
    }

    #endregion

    #region Player Input Events

    private void OnPlayerRightMove(PlayerRightMoveEvent evt)
    {
        SetMoveInput(IsReverse ? -1f : 1f);
    }

    private void OnPlayerLeftMove(PlayerLeftMoveEvent evt)
    {
        SetMoveInput(IsReverse ? 1f : -1f);
    }

    private void OnPlayerMoveCancel(PlayerMoveCancelEvent evt)
    {
        SetMoveInput(0f);
    }

    private void OnPlayerJump(PlayerJumpEvent evt)
    {
        TriggerJump();
    }

    #endregion

    #region Editor Tools

#if UNITY_EDITOR
    /// <summary>
    /// 用途：自动绑定 Actor、死亡 Feedback、IWannerGame_Door 的触发器与进入动画，并把 Success(Collider2D) 持久化到 OnEnter。
    /// 调用边界：仅在 Unity Editor Inspector 手动点击；只读写当前关卡 prefab 层级和本地序列化数据。
    /// 注意事项：找不到门时保留原引用；不会访问 DB、配置、资源管理器或创建任何编辑器实例。
    /// </summary>
    [Button("AutoBind")]
    public void AutoBind()
    {
        if (Application.isPlaying)
            return;

        ActorCtrl bindActor = GetComponentInChildren<ActorCtrl>(true);
        Transform doorRoot = FindChildByName(transform, "IWannerGame_Door");
        IWannerGame_EnterDoorAnimCtrl bindDoorAnim = doorRoot != null
            ? doorRoot.GetComponent<IWannerGame_EnterDoorAnimCtrl>()
            : GetComponentInChildren<IWannerGame_EnterDoorAnimCtrl>(true);
        OnTrigger2DHandle bindDoorTrigger = doorRoot != null
            ? doorRoot.GetComponent<OnTrigger2DHandle>()
            : bindDoorAnim?.GetComponent<OnTrigger2DHandle>();
        Transform feedbackRoot = FindChildByName(transform, "Feedback");
        MMF_Player bindDeathFeedback = feedbackRoot != null
            ? feedbackRoot.GetComponent<MMF_Player>()
            : null;

        bool shouldBindActor = bindActor != null && actor != bindActor;
        bool shouldBindDoorAnim = bindDoorAnim != null && enterDoorAnimCtrl != bindDoorAnim;
        bool shouldBindDoorTrigger = bindDoorTrigger != null && successDoorTrigger != bindDoorTrigger;
        bool shouldBindDeathFeedback = bindDeathFeedback != null && deathFeedback != bindDeathFeedback;
        bool shouldBindSuccess = bindDoorTrigger != null && !HasPersistentSuccessListener(bindDoorTrigger);
        if (!shouldBindActor && !shouldBindDoorAnim && !shouldBindDoorTrigger &&
            !shouldBindDeathFeedback && !shouldBindSuccess)
            return;

        Undo.RecordObject(this, "IWannerLevelCtrl AutoBind");
        if (shouldBindActor)
            actor = bindActor;
        if (shouldBindDoorAnim)
            enterDoorAnimCtrl = bindDoorAnim;
        if (shouldBindDoorTrigger)
            successDoorTrigger = bindDoorTrigger;
        if (shouldBindDeathFeedback)
            deathFeedback = bindDeathFeedback;

        if (shouldBindSuccess)
        {
            Undo.RecordObject(bindDoorTrigger, "IWannerLevelCtrl Bind Success Door");
            UnityEventTools.AddPersistentListener<Collider2D>(bindDoorTrigger.OnEnter, Success);
            EditorUtility.SetDirty(bindDoorTrigger);
            if (PrefabUtility.IsPartOfPrefabInstance(bindDoorTrigger))
                PrefabUtility.RecordPrefabInstancePropertyModifications(bindDoorTrigger);
        }

        EditorUtility.SetDirty(this);
        if (PrefabUtility.IsPartOfPrefabInstance(this))
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
    }

#endif

    #endregion

    #region Hierarchy Helpers

    private static Transform FindChildByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, targetName, StringComparison.OrdinalIgnoreCase))
                return child;

            Transform result = FindChildByName(child, targetName);
            if (result != null)
                return result;
        }

        return null;
    }

    #endregion
}
