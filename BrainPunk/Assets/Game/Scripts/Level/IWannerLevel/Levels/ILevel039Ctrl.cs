using System;
using System.Collections.Generic;
using Bear.Logger;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// IWanner 第 039 关中央控制器：按 Portal 的实际触发顺序统一切换入口并移动对应地块。
/// </summary>
[DisallowMultipleComponent]
public sealed class ILevel039Ctrl : MonoBehaviour, IDebuger
{
    #region Nested Types

    [Serializable]
    private sealed class PortalStep
    {
        /// <summary>
        /// 用途：指定当前步骤的 Trigger Collider，步骤消费时立即禁用。
        /// 调用边界：仅由 TriggerNextStep 按列表顺序处理。
        /// 注意事项：为空时不影响后续配置执行，但会记录警告。
        /// </summary>
        [Tooltip("当前步骤触发后需要禁用的 Trigger Collider")]
        [SerializeField] private Collider2D triggerCollider;

        /// <summary>
        /// 用途：指定当前 Portal 本体 Collider，步骤消费时立即禁用。
        /// 调用边界：仅由 TriggerNextStep 按列表顺序处理。
        /// 注意事项：为空时不影响后续配置执行，但会记录警告。
        /// </summary>
        [Tooltip("当前步骤触发后需要禁用的 Portal Collider")]
        [SerializeField] private Collider2D portalCollider;

        /// <summary>
        /// 用途：指定当前 Portal 的显示节点，触发后缩放至零。
        /// 调用边界：缩放时长由控制器统一配置，缩放不会阻塞下一个步骤。
        /// 注意事项：该节点应与后续可能激活的 Portal 节点相互独立。
        /// </summary>
        [Tooltip("触发后缩放至 0 的当前 Portal 显示节点")]
        [SerializeField] private Transform portalVisual;

        /// <summary>
        /// 用途：指定当前步骤触发后立即出现的下一个 Portal。
        /// 调用边界：在当前 Portal 被关闭后立即激活；最后一步允许为空。
        /// 注意事项：此激活不等待地块移动结束，以保持 prefab 原有并行时序。
        /// </summary>
        [Tooltip("当前步骤触发后立即激活的下一个 Portal；最后一步可为空")]
        [SerializeField] private GameObject nextPortal;

        /// <summary>
        /// 用途：指定当前步骤需要移动的地块或其它 Transform。
        /// 调用边界：以触发瞬间的世界坐标加 moveOffset 作为本次终点。
        /// 注意事项：为空时只执行 Portal 切换，不启动移动。
        /// </summary>
        [Tooltip("当前步骤需要移动的目标 Transform")]
        [SerializeField] private Transform moveTarget;

        /// <summary>
        /// 用途：配置 moveTarget 相对触发瞬间世界坐标的移动偏移。
        /// 调用边界：每个步骤只读取一次并生成固定终点。
        /// 注意事项：数值使用世界坐标单位。
        /// </summary>
        [Tooltip("移动目标相对当前位置的世界坐标偏移")]
        [SerializeField] private Vector2 moveOffset;

        /// <summary>
        /// 用途：配置当前步骤地块移动的持续时间。
        /// 调用边界：运行时按非负值处理。
        /// 注意事项：单位为秒，设为 0 时立即到达终点。
        /// </summary>
        [Tooltip("当前步骤地块移动时长（秒）")]
        [Min(0f)]
        [SerializeField] private float moveDuration = 1f;

        /// <summary>
        /// 用途：配置当前步骤开始移动前的等待时间。
        /// 调用边界：运行时按非负值处理，使用 DOTween 游戏时间。
        /// 注意事项：等待期间不阻止后续 Portal 步骤触发。
        /// </summary>
        [Tooltip("当前步骤开始移动前的延迟（秒）")]
        [Min(0f)]
        [SerializeField] private float moveDelay;

        /// <summary>
        /// 用途：指定移动完成后才激活的额外对象。
        /// 调用边界：仅在 moveTarget 成功完成移动后激活；允许为空。
        /// 注意事项：第 039 关第五步用它开启下方 Portal。
        /// </summary>
        [Tooltip("当前步骤移动完成后激活的对象；不需要时留空")]
        [SerializeField] private GameObject activateAfterMove;

        public Collider2D TriggerCollider => triggerCollider;
        public Collider2D PortalCollider => portalCollider;
        public Transform PortalVisual => portalVisual;
        public GameObject NextPortal => nextPortal;
        public Transform MoveTarget => moveTarget;
        public Vector2 MoveOffset => moveOffset;
        public float MoveDuration => moveDuration;
        public float MoveDelay => moveDelay;
        public GameObject ActivateAfterMove => activateAfterMove;
    }

    #endregion

    #region Constants

    private static readonly AnimationCurve PortalDisappearEase =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    #endregion

    #region Serialized Fields

    /// <summary>
    /// 用途：配置当前 Portal 显示节点缩放到零的时间。
    /// 调用边界：所有步骤共用该配置，运行时按非负值处理。
    /// 注意事项：缩放与下一 Portal 激活、地块移动并行执行。
    /// </summary>
    [Header("Portal Sequence")]
    [Tooltip("当前 Portal 缩放到 0 的时长（秒）")]
    [Min(0f)]
    [SerializeField] private float portalScaleDuration = 0.1f;

    /// <summary>
    /// 用途：按实际触发顺序配置六个 Portal 步骤。
    /// 调用边界：每次 TriggerNextStep 只消费一个步骤。
    /// 注意事项：运行期间不要改变列表顺序，否则已消费索引会失去含义。
    /// </summary>
    [Tooltip("按 Portal 实际触发顺序配置的步骤列表")]
    [SerializeField] private List<PortalStep> steps = new();

    #endregion

    #region Runtime State

    private int _currentStepIndex;

    #endregion

    #region Public API

    /// <summary>
    /// 用途：供六个 Portal 的同类 Trigger 事件调用，依序执行下一段关卡机关。
    /// 调用边界：仅 PlayMode 有效；每次有效调用都会永久消费当前索引，不依赖 OnEnable。
    /// 注意事项：同一步骤的 Trigger Collider 会先被禁用，额外调用超过列表长度时只记录警告。
    /// </summary>
    public void TriggerNextStep()
    {
        if (!Application.isPlaying)
        {
            this.LogWarning("[ILevel039Ctrl] TriggerNextStep is only available in PlayMode.");
            return;
        }

        if (_currentStepIndex >= steps.Count)
        {
            this.LogWarning("[ILevel039Ctrl] All configured Portal steps have already been consumed.");
            return;
        }

        int stepIndex = _currentStepIndex;
        _currentStepIndex++;
        ExecuteStep(steps[stepIndex], stepIndex);
    }

    #endregion

    #region Unity Lifecycle

    private void OnDestroy()
    {
        DOTween.Kill(this);
    }

    #endregion

    #region Runtime Helpers

    private void ExecuteStep(PortalStep step, int stepIndex)
    {
        if (step == null)
        {
            this.LogWarning($"[ILevel039Ctrl] Portal step {stepIndex} is null; the step was consumed.");
            return;
        }

        SetColliderEnabled(step.TriggerCollider, false, stepIndex, "Trigger");
        SetColliderEnabled(step.PortalCollider, false, stepIndex, "Portal");
        PlayPortalDisappear(step.PortalVisual, stepIndex);

        if (step.NextPortal != null)
            step.NextPortal.SetActive(true);

        MoveStepTarget(step, stepIndex);
    }

    private void SetColliderEnabled(
        Collider2D targetCollider,
        bool isEnabled,
        int stepIndex,
        string colliderRole)
    {
        if (targetCollider == null)
        {
            this.LogWarning(
                $"[ILevel039Ctrl] Step {stepIndex} is missing its {colliderRole} Collider2D.");
            return;
        }

        targetCollider.enabled = isEnabled;
    }

    private void PlayPortalDisappear(Transform portalVisual, int stepIndex)
    {
        if (portalVisual == null)
        {
            this.LogWarning($"[ILevel039Ctrl] Step {stepIndex} is missing its Portal visual.");
            return;
        }

        float effectiveDuration = Mathf.Max(0f, portalScaleDuration);
        if (effectiveDuration <= 0f)
        {
            portalVisual.localScale = Vector3.zero;
            return;
        }

        portalVisual
            .DOScale(Vector3.zero, effectiveDuration)
            .SetEase(PortalDisappearEase)
            .SetId(this)
            .SetLink(gameObject);
    }

    private void MoveStepTarget(PortalStep step, int stepIndex)
    {
        Transform moveTarget = step.MoveTarget;
        if (moveTarget == null)
        {
            this.LogWarning($"[ILevel039Ctrl] Step {stepIndex} is missing its move target.");
            return;
        }

        Vector3 destination = moveTarget.position + (Vector3)step.MoveOffset;
        float effectiveDuration = Mathf.Max(0f, step.MoveDuration);
        float effectiveDelay = Mathf.Max(0f, step.MoveDelay);
        GameObject activateAfterMove = step.ActivateAfterMove;

        Tweener moveTween = moveTarget
            .DOMove(destination, effectiveDuration)
            .SetDelay(effectiveDelay)
            .SetEase(Ease.Linear)
            .SetId(this)
            .SetLink(gameObject);

        if (activateAfterMove != null)
        {
            moveTween.OnComplete(() =>
            {
                if (activateAfterMove != null)
                    activateAfterMove.SetActive(true);
            });
        }
    }

    #endregion
}
