using System;
using Bear.EventSystem;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Events;
using Game.Level;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// Level 92 专用的双次进门流程：第一次播放结束门表现后从出生门返回，第二次才交给标准通关流程。
/// </summary>
public sealed class Level92Ctrl : MonoBehaviour, IEventSender, IDebuger
{
    private const int RequiredDoorEntryCount = 2;

    #region 序列化引用

    [Tooltip("Level 92 根节点上的基础关卡控制器；仅在第二次进门时转交标准通关流程。")]
    [SerializeField] private BaseLevelCtrl levelCtrl;

    [Tooltip("结束门的标准成功表现；第一次进门只复用门、彩带、烟花、音效和震动表现。")]
    [SerializeField] private SuccessAnimCtrl successAnim;

    [Tooltip("Level 92 玩家角色；用于两次进门时播放缩入结束门的本地表现。")]
    [SerializeField] private ActorCtrl actor;

    [Tooltip("角色缩入结束门时的目标点；应绑定到 Level 92 结束门内部的 door 节点。")]
    [SerializeField] private Transform actorEnterTarget;

    [Tooltip("结束门打开后，角色开始缩入门内前的等待时间。")]
    [SerializeField] private float actorEnterDelay = 0.4f;

    [Tooltip("角色移动、旋转并缩入结束门的持续时间。")]
    [SerializeField] private float actorEnterDuration = 1.2f;

    [Tooltip("角色缩入结束门时，相对目标点的世界坐标偏移。")]
    [SerializeField] private Vector3 actorEnterOffset = new Vector3(0f, 1f, 0f);

    #endregion

    #region 运行时状态

    private int doorEntryCount;
    private bool isFirstPassPlaying;
    private bool isFinalPassAccepted;
    private Sequence actorEnterSequence;
    private Rigidbody2D actorRigidbody;
    private SpriteRendererGroupAlpha actorRendererGroup;
    private Vector3 actorInitialLocalScale;
    private Quaternion actorInitialLocalRotation;
    private bool actorInitialSimulated;
    private bool hasCachedActorState;
    private int actorEnterFlowVersion;
    private EventSubscriber resetSubscriber;

    #endregion

    #region Unity 生命周期

    private void Awake()
    {
        ResolveReferences();
        CacheActorState();
        ResetAttemptState();
    }

    private void OnEnable()
    {
        EventsUtils.ResetEvents(ref resetSubscriber);
        resetSubscriber.Subscribe<GameResetEvent>(OnGameReset);
    }

    private void OnDisable()
    {
        EventsUtils.ResetEvents(ref resetSubscriber);
        ResetAttemptState();
    }

    private void OnDestroy()
    {
        EventsUtils.ResetEvents(ref resetSubscriber);
        ResetAttemptState();
    }

    #endregion

    #region 结束门流程

    /// <summary>
    /// 仅供 Level 92 结束门的 OnTrigger2DHandle 动态绑定。
    /// 第一次触发播放假通关并从出生门返回，第二次触发才调用 BaseLevelCtrl 的原始通关入口。
    /// </summary>
    /// <param name="collider">进入结束门的碰撞体；只接受 Actor 层且未死亡的玩家角色。</param>
    public void OnActorEnterSuccessDoor(Collider2D collider)
    {
        if (!Application.isPlaying || !IsValidActorCollider(collider))
            return;

        if (isFirstPassPlaying || isFinalPassAccepted)
            return;

        if (doorEntryCount == 0)
        {
            doorEntryCount = 1;
            PlayFirstDoorPass();
            return;
        }

        doorEntryCount = RequiredDoorEntryCount;
        isFinalPassAccepted = true;
        levelCtrl.SetCtrlable(false);
        actor.SetMoveInput(0f);
        actor.StopMoving();
        PlayActorEnterDoorAsync(++actorEnterFlowVersion).Forget();
        levelCtrl.OnActorTrigger2D(collider);
    }

    private void PlayFirstDoorPass()
    {
        isFirstPassPlaying = true;
        int flowVersion = ++actorEnterFlowVersion;
        levelCtrl.IsActorEnterDoor = false;
        levelCtrl.SetCtrlable(false);
        actor.SetMoveInput(0f);
        actor.StopMoving();

        if (successAnim == null)
        {
            this.LogWarning("[Level92Ctrl] successAnim is missing; continue with the start-door return fallback.");
            ReturnFromStartDoorAsync(flowVersion).Forget();
            return;
        }

        PlayActorEnterDoorAsync(flowVersion).Forget();
        successAnim.Play(() => ReturnFromStartDoorAsync(flowVersion).Forget());
    }

    private async UniTask PlayActorEnterDoorAsync(int flowVersion)
    {
        try
        {
            await UniTask.WaitForSeconds(
                Mathf.Max(0f, actorEnterDelay),
                cancellationToken: this.destroyCancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (flowVersion != actorEnterFlowVersion || actor == null || actorEnterTarget == null)
            return;

        KillActorEnterSequence();

        actor.SetMoveInput(0f);
        actor.StopMoving();

        actorRigidbody ??= actor.RB != null ? actor.RB : actor.GetComponent<Rigidbody2D>();
        if (actorRigidbody != null)
        {
            actorRigidbody.linearVelocity = Vector2.zero;
            actorRigidbody.simulated = false;
        }

        Transform actorTransform = actor.transform;
        float duration = Mathf.Max(0.01f, actorEnterDuration);
        Vector3 targetEulerAngles = actorTransform.localEulerAngles;
        targetEulerAngles.z = targetEulerAngles.y != 0f ? -720f : 720f;

        actorEnterSequence = DOTween.Sequence().SetLink(gameObject);
        actorEnterSequence.Append(actorTransform.DOMove(actorEnterTarget.position + actorEnterOffset, duration));
        actorEnterSequence.Join(actorTransform.DOScale(Vector3.zero, duration));
        actorEnterSequence.Join(actorTransform.DORotate(targetEulerAngles, duration, RotateMode.FastBeyond360));
        actorEnterSequence.OnComplete(() => actorEnterSequence = null);
    }

    private async UniTask ReturnFromStartDoorAsync(int flowVersion)
    {
        if (flowVersion != actorEnterFlowVersion
            || doorEntryCount != 1
            || !isFirstPassPlaying
            || actor == null
            || actor.IsDied)
        {
            if (flowVersion == actorEnterFlowVersion)
                ResetAttemptState();

            return;
        }

        int returnFlowVersion = ++actorEnterFlowVersion;
        PrepareActorHiddenForStartDoor();
        RestoreActorForStartDoor();
        levelCtrl.IsActorEnterDoor = false;

        try
        {
            await levelCtrl.TryToPlayEnterDoorAnim();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        finally
        {
            bool isCurrentReturnFlow = returnFlowVersion == actorEnterFlowVersion
                                       && doorEntryCount == 1
                                       && isFirstPassPlaying
                                       && actor != null
                                       && !actor.IsDied;
            if (isCurrentReturnFlow)
            {
                if (levelCtrl != null)
                    levelCtrl.SetCtrlable(true);

                if (actor != null && !actor.IsDied)
                {
                    RestoreActorVisualAfterStartDoor();
                    actor.ResumeMoving();
                }

                this.DispatchEvent(Witness<GamePlayPanelSwitchBlockEvent>._, false);
                isFirstPassPlaying = false;
            }
            else
            {
                if (returnFlowVersion == actorEnterFlowVersion)
                    ResetAttemptState();

                if (levelCtrl != null)
                {
                    levelCtrl.IsActorEnterDoor = false;
                    levelCtrl.SetCtrlable(false);
                }

                if (actor != null)
                {
                    actor.SetMoveInput(0f);
                    actor.StopMoving();
                }
            }
        }
    }

    #endregion

    #region 状态与引用

    private void OnGameReset(GameResetEvent evt)
    {
        ResetAttemptState();

        if (levelCtrl != null)
            levelCtrl.IsActorEnterDoor = false;

        if (actor != null && !actor.IsDied)
            RestoreActorForStartDoor();
    }

    private bool IsValidActorCollider(Collider2D collider)
    {
        if (collider == null || levelCtrl == null || actor == null || actor.IsDied)
            return false;

        return LayerMask.LayerToName(collider.gameObject.layer) == "Actor";
    }

    private void ResolveReferences()
    {
        if (levelCtrl == null)
            levelCtrl = GetComponent<BaseLevelCtrl>() ?? GetComponentInParent<BaseLevelCtrl>();

        if (actor == null && levelCtrl != null)
            actor = levelCtrl.Actor;

        if (successAnim == null)
            successAnim = GetComponentInChildren<SuccessAnimCtrl>(true);

        if (actorEnterTarget == null && successAnim != null)
            actorEnterTarget = successAnim.transform.Find("door");

        if (levelCtrl == null || actor == null)
            this.LogWarning("[Level92Ctrl] Required levelCtrl or actor reference is missing; the double-door flow cannot run.");
    }

    private void CacheActorState()
    {
        if (actor == null)
            return;

        actorInitialLocalScale = actor.transform.localScale;
        actorInitialLocalRotation = actor.transform.localRotation;
        actorRigidbody = actor.RB != null ? actor.RB : actor.GetComponent<Rigidbody2D>();
        actorInitialSimulated = actorRigidbody == null || actorRigidbody.simulated;
        hasCachedActorState = true;
    }

    private void RestoreActorForStartDoor()
    {
        KillActorEnterSequence();

        if (actor == null || !hasCachedActorState)
            return;

        actor.transform.localScale = actorInitialLocalScale;
        actor.transform.localRotation = actorInitialLocalRotation;
        actor.SetMoveInput(0f);

        if (actorRigidbody != null)
        {
            actorRigidbody.linearVelocity = Vector2.zero;
            actorRigidbody.simulated = actorInitialSimulated;
        }
    }

    /// <summary>
    /// 在恢复角色缩放前隐藏主 Spine 与动态皮肤渲染器，避免出生门流程重新收集渲染器前出现闪帧。
    /// </summary>
    private void PrepareActorHiddenForStartDoor()
    {
        if (actor == null)
            return;

        actor.SetSpineAlpha(0f);
        actorRendererGroup ??= actor.GetComponentInChildren<SpriteRendererGroupAlpha>(true);
        if (actorRendererGroup == null)
            return;

        actorRendererGroup.CollectRenderers();
        actorRendererGroup.SetAlpha(0f);
    }

    /// <summary>
    /// 出生门流程结束后兜底恢复全部角色渲染器，兼容出生门引用缺失或提前结束的降级路径。
    /// </summary>
    private void RestoreActorVisualAfterStartDoor()
    {
        if (actor == null)
            return;

        actor.SetSpineAlpha(1f);
        actorRendererGroup ??= actor.GetComponentInChildren<SpriteRendererGroupAlpha>(true);
        if (actorRendererGroup == null)
            return;

        actorRendererGroup.CollectRenderers();
        actorRendererGroup.SetAlpha(1f);
    }

    private void KillActorEnterSequence()
    {
        if (actorEnterSequence == null)
            return;

        actorEnterSequence.Kill();
        actorEnterSequence = null;
    }

    private void ResetAttemptState()
    {
        actorEnterFlowVersion++;
        doorEntryCount = 0;
        isFirstPassPlaying = false;
        isFinalPassAccepted = false;
        KillActorEnterSequence();
    }

    #endregion
}
