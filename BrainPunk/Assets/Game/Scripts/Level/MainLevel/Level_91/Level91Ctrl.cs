using System;
using System.Threading;
using Bear.EventSystem;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Level;
using Game.Play;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level091 关卡控制器：管理连续敲门失败流程、暂停页门铃成功流程及两条流程的输入互斥。
/// </summary>
public sealed class Level91Ctrl : MonoBehaviour, IEventSender, IDebuger
{
    #region State

    private enum Level91State
    {
        Ready,
        DoorbellRinging,
        DoorOpen,
        AngrySequence,
        Charging,
        Failed
    }

    private const string DoorOpenAnimationName = "door_open";
    private const string ImpactVfxObjectName = "Knock_Explosion_Impact";

    #endregion

    #region Inspector

    [Header("关卡引用")]
    [Tooltip("本关 BaseLevelCtrl，仅用于开关玩家控制。")]
    [SerializeField] private BaseLevelCtrl baseLevelCtrl;

    [Tooltip("本关玩家 Actor。")]
    [SerializeField] private ActorCtrl actor;

    [Tooltip("右侧出口门前的敲门命中区域。")]
    [SerializeField] private GameObject knockTarget;

    [Tooltip("右侧出口门的通关 Trigger；失败流程保持关闭，门铃成功后才开启。")]
    [SerializeField] private Collider2D exitDoorTrigger;

    [Tooltip("右侧出口门的 Spine 控制器。")]
    [SerializeField] private ActorSpineCtrl exitDoorSpine;

    [Tooltip("连续敲门时播放的局部粒子表现。")]
    [SerializeField] private ParticleSystem knockVfx;

    [Tooltip("冲撞命中时播放的 Knock_Explosion，独立于敲门粒子。")]
    [SerializeField] private ParticleSystem impactVfx;

    [Tooltip("场景中预放并初始隐藏的 Level091 黑箱 Actor，位置可直接在 Inspector 调整。")]
    [SerializeField] private Level91ActorCtrl boxBadActor;

    [Header("暂停页门铃")]
    [Tooltip("Ding Dong 放射线图片。")]
    [SerializeField] private Sprite dingDongRaysSprite;

    [Tooltip("Ding Dong 文字图片。")]
    [SerializeField] private Sprite dingDongTextSprite;

    [Header("连续敲门")]
    [Min(1)]
    [SerializeField] private int requiredKnockCount = 3;

    [Min(0.1f)]
    [SerializeField] private float knockTimeLimit = 1.5f;

    [Header("黑箱演出")]
    [Min(0f)]
    [SerializeField] private float doorOpenLeadDelay = 0.5f;

    [Min(0f)]
    [SerializeField] private float enemyWalkOutDistance = 1.8f;

    [Min(0.05f)]
    [SerializeField] private float enemyRevealDuration = 0.45f;

    [Min(0f)]
    [SerializeField] private float preAngryDelay = 0.5f;

    [Min(0.1f)]
    [SerializeField] private float enemyChargeSpeed = 12f;

    [Min(0.05f)]
    [SerializeField] private float enemyMinimumChargeDuration = 0.2f;

    [Min(0.1f)]
    [SerializeField] private float enemyMaximumChargeDuration = 2.5f;

    [Header("玩家击飞")]
    [Tooltip("沿用 Level10046：固定向左上方击飞，不根据接触点重新归一化。")]
    [SerializeField] private Vector2 knockbackDirection = new Vector2(-0.5f, 0.5f);

    [Min(0f)]
    [SerializeField] private float impactDelay = 0.1f;

    [Min(0.1f)]
    [SerializeField] private float knockbackForce = 50f;

    [Min(0.1f)]
    [SerializeField] private float rotationDuration = 0.5f;

    [Min(0f)]
    [SerializeField] private float failureDelay = 1f;

    #endregion

    #region Runtime State

    private Camera mainCamera;
    private EventSubscriber subscriber;
    private Level91State state = Level91State.Ready;
    private Tween actorSpinTween;
    private CancellationTokenSource lifecycleCancellation;
    private int currentKnockCount;
    private float knockElapsedTime;
    private bool hudInputBlocked;
    private bool gameplayPaused;

    #endregion

    #region Public API

    /// <summary>
    /// Ding Dong 放射线图片，仅供 Level091 专用 HUD 生成门铃表现。
    /// </summary>
    public Sprite DingDongRaysSprite => dingDongRaysSprite;

    /// <summary>
    /// Ding Dong 文字图片，仅供 Level091 专用 HUD 生成门铃表现。
    /// </summary>
    public Sprite DingDongTextSprite => dingDongTextSprite;

    /// <summary>
    /// 当前是否允许暂停页门铃触发成功流程。
    /// </summary>
    public bool CanRingDoorbell => state == Level91State.Ready && isActiveAndEnabled;

    /// <summary>
    /// 当前是否允许暂停页播放门铃反馈；门已打开后仍可重复播放，但不会再次改变关卡状态。
    /// </summary>
    public bool CanPlayDoorbellFeedback =>
        isActiveAndEnabled &&
        (state == Level91State.Ready || state == Level91State.DoorOpen);

    #endregion

    #region Lifecycle

    private void Awake()
    {
        ResolveReferences();
        mainCamera = Camera.main;
        ResetKnockWindow();

        if (boxBadActor != null)
            boxBadActor.PrepareHidden();

        if (exitDoorTrigger != null)
            exitDoorTrigger.enabled = false;
    }

    private void OnEnable()
    {
        CancelLifecycleTasks();
        lifecycleCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());
        gameplayPaused = false;

        EventsUtils.ResetEvents(ref subscriber);
        subscriber.Subscribe<GamePauseEvent>(OnGamePause);
        subscriber.Subscribe<GameResumeEvent>(OnGameResume);
    }

    private void Update()
    {
        if (state != Level91State.Ready)
            return;

        if (gameplayPaused)
        {
            ResetKnockWindow();
            return;
        }

        UpdateKnockWindow();
        if (TryGetKnockPosition(out Vector2 screenPosition))
            RegisterKnock(screenPosition);
    }

    private void OnDisable()
    {
        CleanupRuntimeState();
    }

    private void OnDestroy()
    {
        CleanupRuntimeState();
    }

    #endregion

    #region Doorbell Success Flow

    /// <summary>
    /// 尝试由 Level091 暂停页的左侧音效按钮启动门铃流程。
    /// 返回 false 表示流程已开始、已结束或关卡正在失败演出，调用方不得继续播放门铃。
    /// </summary>
    public bool TryBeginDoorbell()
    {
        if (!CanRingDoorbell)
            return false;

        state = Level91State.DoorbellRinging;
        ResetKnockWindow();
        LockActorControl();
        SetHudInputBlocked(true);
        return true;
    }

    /// <summary>
    /// Ding Dong 表现结束后打开右侧出口门，并恢复玩家移动。
    /// 仅允许 Level091 专用 HUD 在成功取得门铃流程所有权后调用。
    /// </summary>
    public void CompleteDoorbell()
    {
        if (state != Level91State.DoorbellRinging)
            return;

        OpenExitDoor(true);
        state = Level91State.DoorOpen;

        if (baseLevelCtrl != null)
            baseLevelCtrl.SetCtrlable(true);

        actor?.ResumeMoving();
        SetHudInputBlocked(false);
    }

    #endregion

    #region Reference Resolution

    private void ResolveReferences()
    {
        if (baseLevelCtrl == null)
            baseLevelCtrl = GetComponent<BaseLevelCtrl>();

        if (actor == null && baseLevelCtrl != null)
            actor = baseLevelCtrl.Actor;

        if (actor == null)
        {
            ActorCtrl[] actors = GetComponentsInChildren<ActorCtrl>(true);
            for (int i = 0; i < actors.Length; i++)
            {
                if (!(actors[i] is Level91ActorCtrl))
                {
                    actor = actors[i];
                    break;
                }
            }
        }

        if (boxBadActor == null)
            boxBadActor = GetComponentInChildren<Level91ActorCtrl>(true);

        if (impactVfx == null)
        {
            ParticleSystem[] particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particleSystems.Length; i++)
            {
                if (particleSystems[i].name == ImpactVfxObjectName)
                {
                    impactVfx = particleSystems[i];
                    break;
                }
            }
        }
    }

    #endregion

    #region Knock Input

    private void UpdateKnockWindow()
    {
        if (currentKnockCount <= 0)
            return;

        knockElapsedTime += Time.deltaTime;
        if (knockElapsedTime > knockTimeLimit)
            ResetKnockWindow();
    }

    private bool TryGetKnockPosition(out Vector2 screenPosition)
    {
        screenPosition = default;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Began || InputUtils.IsPointerOverUI(touch.position))
                continue;

            screenPosition = touch.position;
            return IsKnockTarget(screenPosition);
        }

        if (!Input.GetMouseButtonDown(0) || InputUtils.IsPointerOverUI(Input.mousePosition))
            return false;

        screenPosition = Input.mousePosition;
        return IsKnockTarget(screenPosition);
    }

    private bool IsKnockTarget(Vector2 screenPosition)
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null || knockTarget == null)
            return false;

        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(screenPosition);
        RaycastHit2D hit = Physics2D.Raycast(worldPosition, Vector2.zero);
        return hit.collider != null && hit.collider.gameObject == knockTarget;
    }

    private void RegisterKnock(Vector2 screenPosition)
    {
        if (state != Level91State.Ready)
            return;

        if (currentKnockCount == 0)
            knockElapsedTime = 0f;

        currentKnockCount++;
        PlayKnockFeedback(screenPosition);

        if (currentKnockCount >= Mathf.Max(1, requiredKnockCount))
            BeginAngrySequence();
    }

    private void PlayKnockFeedback(Vector2 screenPosition)
    {
        if (knockVfx != null && mainCamera != null)
        {
            Vector3 worldPosition = mainCamera.ScreenToWorldPoint(screenPosition);
            worldPosition.z = 0f;
            knockVfx.transform.position = worldPosition;
            knockVfx.Play();
        }

        AudioManager.PlaySound("knockDoor");
    }

    #endregion

    #region Angry Failure Flow

    private void BeginAngrySequence()
    {
        if (state != Level91State.Ready)
            return;

        state = Level91State.AngrySequence;
        ResetKnockWindow();

        if (exitDoorTrigger != null)
            exitDoorTrigger.enabled = false;

        LockActorControl();
        SetHudInputBlocked(true);
        CancellationToken cancellationToken = lifecycleCancellation != null
            ? lifecycleCancellation.Token
            : this.GetCancellationTokenOnDestroy();
        PlayAngrySequenceAsync(cancellationToken).Forget();
    }

    private async UniTaskVoid PlayAngrySequenceAsync(CancellationToken cancellationToken)
    {
        OpenExitDoor(false);

        try
        {
            if (boxBadActor == null || actor == null || exitDoorSpine == null)
            {
                this.LogError("[Level91Ctrl] 黑箱演出引用不完整，已取消失败演出，避免无预警击飞玩家。");
                CancelBrokenAngrySequence();
                return;
            }

            await UniTask.WaitForSeconds(
                Mathf.Max(0f, doorOpenLeadDelay),
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            boxBadActor.gameObject.SetActive(true);

            var sequenceConfig = new Level91ActorCtrl.SequenceConfig(
                enemyWalkOutDistance,
                enemyRevealDuration,
                preAngryDelay,
                enemyChargeSpeed,
                enemyMinimumChargeDuration,
                enemyMaximumChargeDuration);

            if (!boxBadActor.Initialize(actor, sequenceConfig))
            {
                this.LogError("[Level91Ctrl] 黑箱初始化失败，已取消失败演出，避免无预警击飞玩家。");
                CancelBrokenAngrySequence();
                return;
            }

            await boxBadActor.PlaySequenceAsync(
                OnEnemyChargeStarted,
                HandleEnemyImpact,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // 关卡重开或退出时取消演出，不再写入失败状态。
        }
        catch (Exception exception)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            this.LogError($"[Level91Ctrl] 黑箱演出异常：{exception}");
            CancelBrokenAngrySequence();
        }
    }

    private void CancelBrokenAngrySequence()
    {
        if (boxBadActor != null)
            boxBadActor.ResetPresentation();

        state = Level91State.Ready;
        ResetKnockWindow();

        if (baseLevelCtrl != null)
            baseLevelCtrl.SetCtrlable(true);

        actor?.ResumeMoving();
        SetHudInputBlocked(false);
    }

    private void OnEnemyChargeStarted()
    {
        if (state == Level91State.AngrySequence)
            state = Level91State.Charging;
    }

    private void HandleEnemyImpact(Transform enemyTransform)
    {
        if (state != Level91State.AngrySequence && state != Level91State.Charging)
            return;

        state = Level91State.Failed;
        LockActorControl();
        SetHudInputBlocked(true);

        CancellationToken cancellationToken = lifecycleCancellation != null
            ? lifecycleCancellation.Token
            : this.GetCancellationTokenOnDestroy();

        actor?.StopMoving();
        Vector3 impactPosition = enemyTransform != null
            ? enemyTransform.position
            : actor != null
                ? actor.transform.position
                : transform.position;
        PlayImpactAndFailureAsync(impactPosition, cancellationToken).Forget();
    }

    private async UniTaskVoid PlayImpactAndFailureAsync(
        Vector3 impactPosition,
        CancellationToken cancellationToken)
    {
        try
        {
            await UniTask.WaitForSeconds(
                Mathf.Max(0f, impactDelay),
                cancellationToken: cancellationToken);

            if (state != Level91State.Failed)
                return;

            if (impactVfx != null)
            {
                impactVfx.transform.position = impactPosition;
                impactVfx.Play();
            }

            AudioManager.PlaySound("trap");

            if (actor != null)
            {
                actor.Knockback(knockbackDirection, knockbackForce).Forget();

                if (actorSpinTween != null && actorSpinTween.IsActive())
                    actorSpinTween.Kill();

                actorSpinTween = actor.transform
                    .DOLocalRotate(
                        new Vector3(0f, 0f, 360f),
                        Mathf.Max(0.1f, rotationDuration),
                        RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear)
                    .SetLoops(-1)
                    .SetLink(actor.gameObject, LinkBehaviour.KillOnDestroy);
            }

            await UniTask.WaitForSeconds(
                Mathf.Max(0f, failureDelay),
                cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (state == Level91State.Failed && !cancellationToken.IsCancellationRequested)
            this.DispatchEvent(Witness<OnTriggerTrapEvent>._);
    }

    #endregion

    #region Door And Input Control

    private void OpenExitDoor(bool enableSuccessTrigger)
    {
        if (exitDoorTrigger != null)
            exitDoorTrigger.enabled = enableSuccessTrigger;

        if (exitDoorSpine != null &&
            !exitDoorSpine.IsPlayingAnimation(DoorOpenAnimationName))
        {
            exitDoorSpine.PlayAnimation(DoorOpenAnimationName, false, false);
            AudioManager.PlaySound("openDoor");
        }
    }

    private void LockActorControl()
    {
        if (baseLevelCtrl != null)
            baseLevelCtrl.SetCtrlable(false);

        if (actor == null)
            return;

        actor.SetMoveInput(0f);
        actor.StopMoving();
    }

    private void OnGamePause(GamePauseEvent _)
    {
        gameplayPaused = true;
        ResetKnockWindow();
    }

    private void OnGameResume(GameResumeEvent _)
    {
        gameplayPaused = false;

        if (state == Level91State.DoorbellRinging ||
            state == Level91State.AngrySequence ||
            state == Level91State.Charging ||
            state == Level91State.Failed)
        {
            LockActorControl();
            CancellationToken cancellationToken = lifecycleCancellation != null
                ? lifecycleCancellation.Token
                : this.GetCancellationTokenOnDestroy();
            RelockActorNextFrameAsync(cancellationToken).Forget();
        }
    }

    private async UniTaskVoid RelockActorNextFrameAsync(CancellationToken cancellationToken)
    {
        try
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (state == Level91State.DoorbellRinging ||
            state == Level91State.AngrySequence ||
            state == Level91State.Charging ||
            state == Level91State.Failed)
        {
            LockActorControl();
        }
    }

    private void SetHudInputBlocked(bool blocked)
    {
        hudInputBlocked = blocked;
        this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, blocked);

        if (PlayCtrl.Instance?.CurrentGamePlayPanel is Level91GamePlayPanel panel)
            panel.SetSpecialInputEnabled(!blocked);
    }

    private void ResetKnockWindow()
    {
        currentKnockCount = 0;
        knockElapsedTime = 0f;
    }

    #endregion

    #region Cleanup

    private void CleanupRuntimeState()
    {
        CancelLifecycleTasks();
        if (boxBadActor != null)
            boxBadActor.ResetPresentation();

        if (actorSpinTween != null && actorSpinTween.IsActive())
            actorSpinTween.Kill();

        actorSpinTween = null;
        gameplayPaused = false;
        EventsUtils.ResetEvents(ref subscriber);

        if (hudInputBlocked)
        {
            hudInputBlocked = false;
            this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, false);
            if (PlayCtrl.Instance?.CurrentGamePlayPanel is Level91GamePlayPanel panel)
                panel.SetSpecialInputEnabled(true);
        }
    }

    private void CancelLifecycleTasks()
    {
        if (lifecycleCancellation == null)
            return;

        lifecycleCancellation.Cancel();
        lifecycleCancellation.Dispose();
        lifecycleCancellation = null;
    }

    #endregion
}
