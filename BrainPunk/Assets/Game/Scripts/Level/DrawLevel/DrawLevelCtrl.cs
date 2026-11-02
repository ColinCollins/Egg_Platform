using System;
using System.Collections.Generic;
using Bear.EventSystem;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using Game.Events;
using Game.Level.Actor;
using Game.Scripts.Common;
using UnityEngine;

#if UNITY_EDITOR
using Sirenix.OdinInspector;
using UnityEditor;
#endif

/// <summary>
/// Draw 单关的游戏状态控制器。
/// 负责运行时门禁、失败/成功结算和关卡生命周期，具体画线行为由 DrawLineFeature 承担。
/// </summary>
public class DrawLevelCtrl : MonoBehaviour, IDrawLevelRuntime, IDebuger, IEventSender,
    IActorSkinBindingOwner
{
    #region Constants

    private const float TrapFailureDelaySeconds = 1.5f;
    private const float FailAreaFailureDelaySeconds = 0.2f;
    private const float ActorFadeOutSeconds = 0.4f;
    private const float ActorShadowHideAlphaThreshold = 0.5f;
    private const float SuccessDoorOpenFallbackSeconds = 0.4f;
    private const string SuccessDoorObjectName = "DrawGame_SuccessDoor";
    private const string SuccessDoorSpineObjectName = "door";

    #endregion

    #region Inspector References

    /// <summary>
    /// 当前关卡持有的画线功能。
    /// 调用边界：必须绑定同一 DLevel prefab 内的 DrawLineFeature，不持有跨关卡对象。
    /// </summary>
    [SerializeField] private DrawLineFeature drawFeature;

    /// <summary>
    /// 成功门中用于定位 DrawSuccessDoorEffectCtrl 的 door Spine 控制器。
    /// 调用边界：必须绑定同一 DLevel 下 DrawGame_SuccessDoor/door 节点，不能绑定 effect 或 ribbon。
    /// </summary>
    [SerializeField] private ActorSpineCtrl successDoorSpine;

    /// <summary>
    /// 成功时与 Actor 使用同一透明度同步渐隐的可选附属物组。
    /// 调用边界：只绑定当前 DLevel 内需要随人物退场的 SpriteRenderer 层级；为空时不改变既有关卡。
    /// 注意事项：若附属物存在 inactive 视觉状态，组组件必须启用 includeInactive。
    /// </summary>
    [SerializeField] private SpriteRendererGroupAlpha successExtraRendererGroup;

    #endregion

    #region Runtime State

    private readonly List<IDrawLevelIntroHandler> _introHandlers = new();
    private readonly List<IDrawStrokeCompletedHandler> _strokeCompletedHandlers = new();
    private EventSubscriber _subscriber;
    private ActorCtrl _actor;
    private SpriteRendererGroupAlpha _actorRendererGroup;
    private GameObject _actorShadowRoot;
    private DrawSuccessDoorEffectCtrl _successDoorEffect;
    private int _attemptVersion;
    private bool _actorShadowInitialActive;
    private bool _hasCachedActorShadowInitialActive;
    private bool _isRuntimeInitialized;
    private bool _isPaused;
    private bool _isResolved;
    private bool _isPlayingSuccess;
    private bool _isPlayingFailure;
    private bool _isFailed;
    private bool _isWaitingForTrapFailure;
    private bool _isWaitingForFailAreaFailure;

    #endregion

    #region IDrawLevelRuntime

    /// <inheritdoc />
    public event Action<DrawLevelResult> Resolved;

    /// <inheritdoc />
    public bool IsRuntimeReady =>
        _isRuntimeInitialized && drawFeature != null && drawFeature.IsRuntimeReady;

    /// <inheritdoc />
    public bool IsInputEnabled => drawFeature != null && drawFeature.IsInputEnabled;

    /// <inheritdoc />
    public bool IsPaused => _isPaused;

    /// <summary>
    /// 当前是否由关卡局部机制占用失败演出窗口。
    /// 调用边界：只供需要先冻结玩法、再提交标准失败重开的本地组件和共享倒计时读取。
    /// </summary>
    public bool IsFailurePerformanceActive => _isPlayingFailure;

    /// <summary>
    /// 当前是否已进入成功演出、失败等待或最终结算，供局部机关避免覆盖结算期角色状态。
    /// </summary>
    public bool IsOutcomeLocked =>
        _isResolved ||
        _isPlayingSuccess ||
        _isPlayingFailure ||
        _isFailed ||
        _isWaitingForTrapFailure ||
        _isWaitingForFailAreaFailure;

    /// <inheritdoc />
    public bool PlaysIntroAfterTransition { get; private set; }

    /// <inheritdoc />
    public bool AllowsMultipleStrokes =>
        drawFeature != null && drawFeature.AllowsMultipleStrokes;

    /// <inheritdoc />
    public int CompletedStrokeCount =>
        drawFeature != null ? drawFeature.CompletedStrokeCount : 0;

    /// <summary>
    /// 当前 Draw 关卡实际受控的玩家 Actor，供 ActorSkinBinding 校验皮肤应用目标。
    /// 调用边界：仅返回当前 DLevel 子层级中由 DrawLevelCtrl 管理的 Actor，不代表其它 Actor、工具或敌人。
    /// </summary>
    public ActorCtrl Actor
    {
        get
        {
            ResolveReferences();
            return _actor;
        }
    }

    /// <inheritdoc />
    public float DrawnLength => drawFeature != null ? drawFeature.DrawnLength : 0f;

    /// <inheritdoc />
    public DrawInkState InkState =>
        drawFeature != null ? drawFeature.InkState : default;

    /// <summary>
    /// 完成状态控制和画线功能初始化，建立一次新的关卡尝试。
    /// 调用边界：DLevel prefab 实例化完成后由 DrawPlayCtrl 调用。
    /// </summary>
    public void InitializeRuntime()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        if (drawFeature == null)
        {
            _isRuntimeInitialized = false;
            this.LogError("[DrawLevelCtrl] DrawLineFeature 缺失，Draw 关卡无法初始化。");
            return;
        }

        CacheLocalHandlers();
        drawFeature.StrokeCompleted -= OnStrokeCompleted;
        drawFeature.StrokeCompleted += OnStrokeCompleted;
        drawFeature.InitializeRuntime();
        if (!drawFeature.IsRuntimeReady)
        {
            _isRuntimeInitialized = false;
            this.LogError("[DrawLevelCtrl] DrawLineFeature 初始化失败，Draw 关卡保持关闭。");
            return;
        }

        AddFailureListeners();
        _isRuntimeInitialized = true;
        ResetGameState();
    }

    /// <summary>
    /// 使用当前关卡配置更新基础墨水长度和剩余墨水星级阈值。
    /// 调用边界：由 DrawPlayCtrl 在 InitializeRuntime 前调用，不访问配置或存档单例。
    /// </summary>
    public void ConfigureInk(
        float maxInkLength,
        float oneStarRemainingInk,
        float twoStarRemainingInk,
        float threeStarRemainingInk)
    {
        ResolveReferences();
        drawFeature?.ConfigureInk(
            maxInkLength,
            oneStarRemainingInk,
            twoStarRemainingInk,
            threeStarRemainingInk);
    }

    /// <summary>
    /// 等待当前关卡内全部开场 Handle 按层级扫描顺序执行完成。
    /// 默认关卡没有开场 Handle 时立即完成；关卡差异由 DLevel{Num}Ctrl 或本地 Handle 承担。
    /// </summary>
    public UniTask PlayIntroAsync()
    {
        return PlayIntroHandlersAsync(false);
    }

    /// <summary>
    /// Reset/Replay 重建后直接应用开场 Handle 声明的重开完成态。
    /// 调用边界：仅由 DrawPlayCtrl 重载流程调用；所有普通 Intro 和仅进关等待都不会重复执行。
    /// </summary>
    public UniTask PlayResetIntroAsync()
    {
        return PlayIntroHandlersAsync(true);
    }

    private async UniTask PlayIntroHandlersAsync(bool isRetry)
    {
        for (int i = 0; i < _introHandlers.Count; i++)
        {
            IDrawLevelIntroHandler handler = _introHandlers[i];
            if (isRetry && handler is IDrawLevelEnterOnlyIntroHandler)
            {
                continue;
            }

            try
            {
                if (isRetry)
                {
                    if (handler is IDrawLevelRetryIntroStateHandler retryStateHandler)
                        retryStateHandler.ApplyDrawRetryIntroState();
                    continue;
                }

                await handler.PlayDrawIntroAsync();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                this.LogError(
                    $"[DrawLevelCtrl] Intro handler failed. index:{i}, error:{exception}");
            }
        }
    }

    /// <summary>
    /// 更新当前活动关卡状态，并把画线输入处理转发给 DrawLineFeature。
    /// 注意事项：仅允许 DrawPlayCtrl.Tick 调用，避免非活动玩法继续运行。
    /// </summary>
    public void Tick()
    {
        if (!_isRuntimeInitialized || _isPaused)
            return;

        CheckFinished();
        if (_isResolved || _isPlayingSuccess || _isPlayingFailure)
            return;

        drawFeature.Tick();
    }

    /// <summary>
    /// 开启或关闭当前关卡的画线输入。
    /// </summary>
    public void SetInputEnabled(bool enabled)
    {
        drawFeature?.SetInputEnabled(
            enabled && !_isResolved && !_isPlayingSuccess && !_isPlayingFailure);
    }

    /// <summary>
    /// 设置当前关卡暂停状态，并同步给画线功能。
    /// </summary>
    public void SetPaused(bool paused)
    {
        _isPaused = paused;
        drawFeature?.SetPaused(paused);
    }

    /// <summary>
    /// 清理画线数据并恢复本关游戏状态。
    /// </summary>
    public void ResetAttempt()
    {
        if (!_isRuntimeInitialized)
            return;

        ResetGameState();
        drawFeature.ResetAttempt();
        NotifyAttemptReset();
    }

    /// <summary>
    /// 控制策划在场景中预设的虚线提示对象。
    /// </summary>
    public void ShowHint(bool visible)
    {
        drawFeature?.ShowHint(visible);
    }

    /// <summary>
    /// 增加当前尝试的奖励墨水容量，返回实际增加的 DrawSpace 长度。
    /// </summary>
    public float AddInk(float amount)
    {
        return drawFeature != null ? drawFeature.AddInk(amount) : 0f;
    }

    /// <summary>
    /// 更新玩家实际绘制线条的材质和颜色。
    /// </summary>
    public void SetDrawingStyle(Material material, Color color)
    {
        drawFeature?.SetDrawingStyle(material, color);
    }

    /// <summary>
    /// 更新玩家实际绘制线条的材质和渐变。
    /// </summary>
    public void SetDrawingStyle(Material material, Gradient gradient)
    {
        drawFeature?.SetDrawingStyle(material, gradient);
    }

    /// <summary>
    /// 原子化提交当前尝试的胜利或失败结果。
    /// 注意事项：同一尝试只接受第一次提交，重置后才允许再次结算。
    /// </summary>
    public bool TryResolve(DrawLevelResult result)
    {
        if (!_isRuntimeInitialized ||
            _isResolved ||
            _isPlayingSuccess ||
            _isPlayingFailure)
        {
            return false;
        }

        _isResolved = true;
        SetInputEnabled(false);
        Resolved?.Invoke(result);
        return true;
    }

    /// <summary>
    /// 原子占用一次延迟成功演出的状态，并在正式结算前锁住输入、失败与界面切换。
    /// 调用边界：供双球相遇、成功门等必须先播本地演出再结算的关卡组件调用；
    /// 成功占用后必须调用 <see cref="CompleteSuccessPerformance"/>，或由 Reset/销毁取消。
    /// </summary>
    public bool TryBeginSuccessPerformance()
    {
        if (!_isRuntimeInitialized ||
            _isResolved ||
            _isPlayingSuccess ||
            _isPlayingFailure ||
            _isFailed ||
            _isWaitingForTrapFailure ||
            _isWaitingForFailAreaFailure)
        {
            return false;
        }

        _isPlayingSuccess = true;
        SetInputEnabled(false);
        this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, true);
        return true;
    }

    /// <summary>
    /// 完成已占用的延迟成功演出并正式提交胜利结果。
    /// 注意事项：只有 <see cref="TryBeginSuccessPerformance"/> 成功后才允许调用；
    /// 若提交失败会立即解除界面切换锁，避免关卡残留在不可操作状态。
    /// </summary>
    public bool CompleteSuccessPerformance(string source)
    {
        if (!_isRuntimeInitialized || !_isPlayingSuccess || _isResolved)
            return false;

        _isPlayingSuccess = false;
        if (TryResolve(new DrawLevelResult(DrawLevelResultType.Success, source)))
            return true;

        this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, false);
        return false;
    }

    /// <summary>
    /// 取消尚未正式结算的成功演出占用，并恢复当前关卡输入与界面切换。
    /// 调用边界：只用于承载演出的组件被禁用、重置或销毁等中断路径；
    /// 已正式结算的关卡不会被该方法回退。
    /// </summary>
    public bool CancelSuccessPerformance()
    {
        if (!_isRuntimeInitialized || !_isPlayingSuccess || _isResolved)
            return false;

        _isPlayingSuccess = false;
        this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, false);
        SetInputEnabled(!_isPaused);
        return true;
    }

    /// <summary>
    /// 原子占用一次延迟失败演出的状态，并在正式失败重开前锁住输入和其它结算来源。
    /// 调用边界：供玩家受击等必须先播放本地表现、再走标准失败链的关卡组件调用；
    /// 占用成功后必须调用 <see cref="CompleteFailurePerformance"/>，或在组件中断时取消。
    /// </summary>
    public bool TryBeginFailurePerformance()
    {
        if (!_isRuntimeInitialized ||
            _isResolved ||
            _isPlayingSuccess ||
            _isPlayingFailure ||
            _isFailed ||
            _isWaitingForTrapFailure ||
            _isWaitingForFailAreaFailure)
        {
            return false;
        }

        _isPlayingFailure = true;
        SetInputEnabled(false);
        this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, true);
        return true;
    }

    /// <summary>
    /// 完成已占用的延迟失败演出，并复用标准 Draw 失败重建链路。
    /// 注意事项：只有 <see cref="TryBeginFailurePerformance"/> 成功后才允许调用；
    /// 若提交失败会解除本次占用并恢复可用输入，避免关卡永久停留在演出态。
    /// </summary>
    public bool CompleteFailurePerformance(string source)
    {
        if (!_isRuntimeInitialized || !_isPlayingFailure || _isResolved)
            return false;

        _isPlayingFailure = false;
        if (TryFailAndReset(source))
            return true;

        this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, false);
        SetInputEnabled(!_isPaused);
        return false;
    }

    /// <summary>
    /// 取消尚未正式提交的失败演出占用，并按当前暂停状态恢复输入。
    /// 调用边界：只用于承载演出的组件被禁用、重置或销毁等中断路径。
    /// </summary>
    public bool CancelFailurePerformance()
    {
        if (!_isRuntimeInitialized || !_isPlayingFailure || _isResolved)
            return false;

        _isPlayingFailure = false;
        this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, false);
        SetInputEnabled(!_isPaused);
        return true;
    }

    /// <summary>
    /// 原子提交当前 Draw 尝试失败，并派发 Draw 专用整关重置事件。
    /// 调用边界：仅供当前 DLevel 内的倒计时、机关或其它本地失败条件调用；
    /// 同一尝试已结算时返回 false，且不会重复派发重置事件。
    /// </summary>
    /// <param name="source">触发失败的本地组件或机制名称。</param>
    public bool TryFailAndReset(string source)
    {
        if (!TryResolve(new DrawLevelResult(DrawLevelResultType.Failed, source)))
            return false;

        this.DispatchEvent(Witness<DrawGameResetEvent>._, GameResetType.Failed);
        return true;
    }

    #endregion

    #region Unity Lifecycle

    private void OnDestroy()
    {
        if (_isPlayingSuccess || _isPlayingFailure)
            this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, false);

        _isPlayingSuccess = false;
        _isPlayingFailure = false;
        EventsUtils.ResetEvents(ref _subscriber);
        if (drawFeature != null)
            drawFeature.StrokeCompleted -= OnStrokeCompleted;

        _introHandlers.Clear();
        _strokeCompletedHandlers.Clear();
    }

    #endregion

    #region Drawing Result

    private void OnStrokeCompleted(DrawStrokeResult result)
    {
        for (int i = 0; i < _strokeCompletedHandlers.Count; i++)
        {
            try
            {
                _strokeCompletedHandlers[i].OnDrawStrokeCompleted(result);
            }
            catch (Exception exception)
            {
                this.LogError(
                    $"[DrawLevelCtrl] Stroke handler failed. index:{i}, error:{exception}");
            }
        }

        if (!AllowsMultipleStrokes)
            SetInputEnabled(false);
    }

    #endregion

    #region Success

    /// <summary>
    /// 接收 DrawGame_SuccessDoor 的触发回调并启动 Draw 专用短胜利动画。
    /// 调用边界：仅供同一 DLevel 内成功门的 OnTrigger2DHandle 动态绑定；
    /// 只接受当前关卡内尚未死亡的 Actor，动画完成前不会提交胜利结果。
    /// </summary>
    /// <param name="collider">进入成功门的碰撞体；必须属于当前关卡未死亡的 Actor。</param>
    public void OnTriggerSuccess(Collider2D collider)
    {
        TryTriggerActorSuccess(collider, true, SuccessDoorObjectName, 0f);
    }

    /// <summary>
    /// 接收篮筐、目标区等没有开门动画的 Actor 成功触发。
    /// 与成功门共用 Actor 身份校验、结果占用和渐隐，但不查找或播放 door_open。
    /// </summary>
    /// <param name="collider">属于当前关卡 Actor 复合刚体的碰撞体。</param>
    /// <param name="source">结算日志中的稳定本地来源名。</param>
    /// <param name="fadeDelaySeconds">成功占用后、Actor 与额外渲染组开始渐隐前的等待秒数。</param>
    /// <returns>是否已成功占用并启动本次胜利演出。</returns>
    public bool TryTriggerSuccessWithoutDoor(
        Collider2D collider,
        string source,
        float fadeDelaySeconds = 0f)
    {
        string resolvedSource = string.IsNullOrWhiteSpace(source)
            ? nameof(DrawLevelCtrl)
            : source;
        return TryTriggerActorSuccess(
            collider,
            false,
            resolvedSource,
            Mathf.Max(0f, fadeDelaySeconds));
    }

    private bool TryTriggerActorSuccess(
        Collider2D collider,
        bool playDoorAnimation,
        string source,
        float fadeDelaySeconds)
    {
        if (!Application.isPlaying ||
            !_isRuntimeInitialized ||
            _isResolved ||
            _isPlayingSuccess ||
            _isPlayingFailure ||
            _isFailed ||
            _isWaitingForTrapFailure ||
            _isWaitingForFailAreaFailure)
        {
            return false;
        }

        ActorCtrl triggerActor = ResolveSuccessTriggerActor(collider);
        if (triggerActor == null || triggerActor != _actor || triggerActor.IsDied)
            return false;

        if (!TryBeginSuccessPerformance())
            return false;

        triggerActor.SetMoveInput(0f);
        triggerActor.StopMoving();
        if (triggerActor.SpineCtrl != null)
            triggerActor.UpdateAnimation();

        PlaySuccessAsync(
            _attemptVersion,
            triggerActor,
            playDoorAnimation,
            source,
            fadeDelaySeconds).Forget();
        return true;
    }

    /// <summary>
    /// 从成功门收到的碰撞体解析当前关卡 Actor。
    /// 普通关卡直接从碰撞体父级命中 ActorCtrl；带球关卡中 Actor 是球刚体的子节点，
    /// 因此球壳 Collider 还需要按 attachedRigidbody 的复合刚体层级回认本关 Actor。
    /// </summary>
    private ActorCtrl ResolveSuccessTriggerActor(Collider2D collider)
    {
        if (collider == null)
            return null;

        ActorCtrl triggerActor = collider.GetComponentInParent<ActorCtrl>();
        if (triggerActor != null || _actor == null)
            return triggerActor;

        Rigidbody2D attachedBody = collider.attachedRigidbody;
        Rigidbody2D actorBody = _actor.GetComponentInParent<Rigidbody2D>();
        return attachedBody != null && attachedBody == actorBody
            ? _actor
            : null;
    }

    private async UniTask PlaySuccessAsync(
        int attemptVersion,
        ActorCtrl actor,
        bool playDoorAnimation,
        string source,
        float fadeDelaySeconds)
    {
        try
        {
            UniTask successDoorEffectTask = playDoorAnimation
                ? PlaySuccessDoorEffectAsync()
                : UniTask.WaitForSeconds(
                    SuccessDoorOpenFallbackSeconds,
                    cancellationToken: this.GetCancellationTokenOnDestroy());

            if (fadeDelaySeconds > 0f)
            {
                await UniTask.WaitForSeconds(
                    fadeDelaySeconds,
                    cancellationToken: this.GetCancellationTokenOnDestroy());
            }

            if (attemptVersion != _attemptVersion || !_isPlayingSuccess || _isResolved)
                return;

            await UniTask.WhenAll(
                FadeActorOutAsync(attemptVersion, actor),
                successDoorEffectTask);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (attemptVersion != _attemptVersion || !_isPlayingSuccess || _isResolved)
            return;

        CompleteSuccessPerformance(source);
    }

    private async UniTask PlaySuccessDoorEffectAsync()
    {
        ResolveReferences();
        if (_successDoorEffect != null)
        {
            await _successDoorEffect.PlayAsync(this.GetCancellationTokenOnDestroy());
            return;
        }

        this.LogError(
            $"[DrawLevelCtrl] {SuccessDoorObjectName} 未绑定 {nameof(DrawSuccessDoorEffectCtrl)}，" +
            "胜利将仅播放 Actor 渐隐。");
        await UniTask.WaitForSeconds(
            SuccessDoorOpenFallbackSeconds,
            cancellationToken: this.GetCancellationTokenOnDestroy());
    }

    private async UniTask FadeActorOutAsync(int attemptVersion, ActorCtrl actor)
    {
        if (actor == null)
            return;

        SpriteRendererGroupAlpha rendererGroup = _actorRendererGroup;
        rendererGroup?.CollectRenderers();
        SpriteRendererGroupAlpha extraRendererGroup = successExtraRendererGroup;
        extraRendererGroup?.CollectRenderers();
        float elapsed = 0f;

        while (elapsed < ActorFadeOutSeconds)
        {
            if (attemptVersion != _attemptVersion || !_isPlayingSuccess || actor == null)
                return;

            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / ActorFadeOutSeconds);
            actor.SetSpineAlpha(alpha);
            rendererGroup?.SetAlpha(alpha);
            extraRendererGroup?.SetAlpha(alpha);
            if (alpha < ActorShadowHideAlphaThreshold)
                SetActorShadowVisible(false);
            await UniTask.Yield(
                PlayerLoopTiming.Update,
                this.GetCancellationTokenOnDestroy());
        }

        if (attemptVersion != _attemptVersion || !_isPlayingSuccess || actor == null)
            return;

        actor.SetSpineAlpha(0f);
        rendererGroup?.SetAlpha(0f);
        extraRendererGroup?.SetAlpha(0f);
        SetActorShadowVisible(false);
    }

    #endregion

    #region Failure

    /// <summary>
    /// 监听主线关卡使用的陷阱和失败区域事件。
    /// 调用边界：仅在 DrawPlayCtrl 初始化本关运行时后注册，销毁关卡时统一解除。
    /// </summary>
    private void AddFailureListeners()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<OnTriggerTrapEvent>(OnTriggerTrap);
        _subscriber.Subscribe<OnTriggerFailAreaEvent>(OnTriggerFailArea);
    }

    private void OnTriggerTrap(OnTriggerTrapEvent evt)
    {
        if (!_isRuntimeInitialized ||
            _isResolved ||
            _isPlayingSuccess ||
            _isPlayingFailure ||
            _isWaitingForTrapFailure ||
            _isWaitingForFailAreaFailure)
        {
            return;
        }

        _isWaitingForTrapFailure = true;
        _actor?.Die();
        WaitForTrapFailureAsync(_attemptVersion).Forget();
    }

    private void OnTriggerFailArea(OnTriggerFailAreaEvent evt)
    {
        if (!_isRuntimeInitialized ||
            _isResolved ||
            _isPlayingSuccess ||
            _isPlayingFailure ||
            _isWaitingForTrapFailure ||
            _isWaitingForFailAreaFailure)
        {
            return;
        }

        _isWaitingForFailAreaFailure = true;
        AudioManager.PlaySound("failed");
        WaitForFailAreaFailureAsync(_attemptVersion).Forget();
    }

    private async UniTask WaitForTrapFailureAsync(int attemptVersion)
    {
        try
        {
            await UniTask.WaitForSeconds(
                TrapFailureDelaySeconds,
                cancellationToken: this.GetCancellationTokenOnDestroy());
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (attemptVersion != _attemptVersion || _isResolved)
            return;

        _isWaitingForTrapFailure = false;
        CheckFinished();
    }

    private async UniTask WaitForFailAreaFailureAsync(int attemptVersion)
    {
        try
        {
            await UniTask.WaitForSeconds(
                FailAreaFailureDelaySeconds,
                cancellationToken: this.GetCancellationTokenOnDestroy());
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (attemptVersion != _attemptVersion || _isResolved)
            return;

        _isWaitingForFailAreaFailure = false;
        _isFailed = true;
        CheckFinished();
    }

    /// <summary>
    /// 使用与 BaseLevelCtrl 一致的失败条件：失败区域已触发，或当前 Actor 已死亡。
    /// 注意事项：陷阱主动触发的死亡会先等待主线同款死亡演出时长，再提交失败结果。
    /// </summary>
    private void CheckFinished()
    {
        if (_isResolved ||
            _isPlayingSuccess ||
            _isPlayingFailure ||
            _isWaitingForTrapFailure ||
            _isWaitingForFailAreaFailure)
            return;

        if (_isFailed)
        {
            DispatchFailureReset(nameof(OnTriggerFailAreaEvent));
            return;
        }

        if (_actor != null && _actor.IsDied)
            DispatchFailureReset($"{nameof(ActorCtrl)}.{nameof(ActorCtrl.IsDied)}");
    }

    /// <summary>
    /// 原子提交 Draw 失败结果，并按主线结构派发失败重置请求。
    /// 注意事项：使用 Draw 专属事件，避免触发主线常驻的广告和统计订阅。
    /// </summary>
    private void DispatchFailureReset(string source)
    {
        TryFailAndReset(source);
    }

    #endregion

    #region Reference And State

    private void ResolveReferences()
    {
        if (drawFeature == null)
            drawFeature = GetComponent<DrawLineFeature>();

        if (_actor == null)
            _actor = GetComponentInChildren<ActorCtrl>(true);

        if (_actorRendererGroup == null && _actor != null)
            _actorRendererGroup =
                _actor.GetComponentInChildren<SpriteRendererGroupAlpha>(true);

        if (_actorShadowRoot == null && _actor != null)
        {
            ShadowController shadowController =
                _actor.GetComponentInChildren<ShadowController>(true);
            Transform shadowTransform = shadowController != null
                ? shadowController.transform
                : _actor.transform.Find("Shadow");
            _actorShadowRoot = shadowTransform != null
                ? shadowTransform.gameObject
                : null;
        }

        if (!_hasCachedActorShadowInitialActive && _actorShadowRoot != null)
        {
            _actorShadowInitialActive = _actorShadowRoot.activeSelf;
            _hasCachedActorShadowInitialActive = true;
        }

        if (_successDoorEffect == null && successDoorSpine != null)
            _successDoorEffect =
                successDoorSpine.GetComponentInParent<DrawSuccessDoorEffectCtrl>();
    }

    private void SetActorShadowVisible(bool visible)
    {
        if (_actorShadowRoot == null || !_hasCachedActorShadowInitialActive)
            return;

        _actorShadowRoot.SetActive(visible && _actorShadowInitialActive);
    }

    private void CacheLocalHandlers()
    {
        _introHandlers.Clear();
        _strokeCompletedHandlers.Clear();
        PlaysIntroAfterTransition = false;

        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IDrawLevelIntroHandler introHandler)
            {
                _introHandlers.Add(introHandler);
                if (introHandler is IDrawLevelVisibleIntroHandler)
                    PlaysIntroAfterTransition = true;
            }

            if (behaviours[i] is IDrawStrokeCompletedHandler handler)
                _strokeCompletedHandlers.Add(handler);
        }
    }

    private void NotifyAttemptReset()
    {
        for (int i = 0; i < _strokeCompletedHandlers.Count; i++)
        {
            try
            {
                _strokeCompletedHandlers[i].OnDrawAttemptReset();
            }
            catch (Exception exception)
            {
                this.LogError(
                    $"[DrawLevelCtrl] Stroke reset handler failed. index:{i}, error:{exception}");
            }
        }
    }

    private void ResetGameState()
    {
        bool wasPlayingSuccess = _isPlayingSuccess;
        bool wasPlayingFailure = _isPlayingFailure;
        _isResolved = false;
        _isPlayingSuccess = false;
        _isPlayingFailure = false;
        _isFailed = false;
        _isWaitingForTrapFailure = false;
        _isWaitingForFailAreaFailure = false;
        _attemptVersion++;
        _successDoorEffect?.ResetVisuals();
        SetActorShadowVisible(true);

        if (successExtraRendererGroup != null)
        {
            successExtraRendererGroup.CollectRenderers();
            successExtraRendererGroup.SetAlpha(1f);
        }

        if (wasPlayingSuccess || wasPlayingFailure)
            this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, false);
    }

    #endregion

    #region Editor

#if UNITY_EDITOR
    /// <summary>
    /// 在 Inspector 中手动绑定 DrawLevelCtrl 的本关引用。
    /// 调用边界：仅供 Unity Editor 非运行状态点击；只读取当前层级，不创建对象或访问运行时数据。
    /// 注意事项：成功门只绑定 DrawGame_SuccessDoor/door，避免误绑到 effect 的 Spine。
    /// </summary>
    [Button("AutoBind")]
    public void AutoBind()
    {
        if (Application.isPlaying)
            return;

        DrawLineFeature bindDrawFeature = GetComponent<DrawLineFeature>();
        Transform successDoorTransform =
            FindEditorChildByName(transform, SuccessDoorObjectName);
        Transform doorSpineTransform = successDoorTransform != null
            ? successDoorTransform.Find(SuccessDoorSpineObjectName)
            : null;
        ActorSpineCtrl bindSuccessDoorSpine = doorSpineTransform != null
            ? doorSpineTransform.GetComponent<ActorSpineCtrl>()
            : null;

        bool shouldBindDrawFeature =
            bindDrawFeature != null && drawFeature != bindDrawFeature;
        bool shouldBindSuccessDoor =
            bindSuccessDoorSpine != null && successDoorSpine != bindSuccessDoorSpine;
        if (!shouldBindDrawFeature && !shouldBindSuccessDoor)
        {
            if (bindSuccessDoorSpine == null)
            {
                this.LogError(
                    $"[DrawLevelCtrl] AutoBind 未找到 " +
                    $"{SuccessDoorObjectName}/{SuccessDoorSpineObjectName} 上的 ActorSpineCtrl。");
            }

            return;
        }

        Undo.RecordObject(this, "DrawLevelCtrl AutoBind");
        if (shouldBindDrawFeature)
            drawFeature = bindDrawFeature;
        if (shouldBindSuccessDoor)
            successDoorSpine = bindSuccessDoorSpine;

        EditorUtility.SetDirty(this);
        if (PrefabUtility.IsPartOfPrefabInstance(this))
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
    }

    private static Transform FindEditorChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != root && child.name == childName)
                return child;
        }

        return null;
    }
#endif

    #endregion
}
