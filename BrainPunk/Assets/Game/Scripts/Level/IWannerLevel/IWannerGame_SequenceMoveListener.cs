using System.Collections.Generic;
using Bear.Logger;
using DG.Tweening;
using Game.ItemEvent;
using UnityEngine;

/// <summary>
/// IWanner 多目标移动监听器。
/// 由 TriggerExecutor/BaseItemExecutor 触发后，整体等待一次 delay，再按配置顺序或并行移动有效目标。
/// </summary>
public sealed class IWannerGame_SequenceMoveListener : BaseItemEventHandle, IDebuger
{
    #region Serialized Fields

    /// <summary>
    /// 用途：保存需要依次移动的 Transform，实际执行顺序与列表顺序一致。
    /// 调用边界：仅在 Execute 时读取并捕获每个有效目标的终点；列表中的空引用会被安全跳过。
    /// 注意事项：每个条目的终点都在 Execute 时捕获；同一 Transform 不应在列表中重复配置。
    /// </summary>
    [Header("Targets")]
    [Tooltip("按列表顺序依次移动的目标；空引用会被跳过")]
    [SerializeField] private List<Transform> targets = new();

    /// <summary>
    /// 用途：配置每个目标相对触发时世界坐标的移动偏移。
    /// 调用边界：所有有效目标共用该值，仅修改 X/Y，Z 保持不变。
    /// 注意事项：重复触发会从各目标当时的位置再次累积位移。
    /// </summary>
    [Header("Move Settings")]
    [Tooltip("每个目标相对触发时位置的移动偏移（仅生效 X/Y，Z 保持不变）")]
    [SerializeField] private Vector2 offset;

    /// <summary>
    /// 用途：配置多个有效目标按顺序移动或同时移动。
    /// 调用边界：Sequence 按 targets 顺序执行并应用 interval；Parallel 在统一 delay 后同时启动且忽略 interval。
    /// 注意事项：默认值为 Sequence，用于保持已有组件的顺序移动行为。
    /// </summary>
    [Tooltip("移动模式；Sequence 按列表顺序移动，Parallel 在统一时间同时移动")]
    [SerializeField] private ExecuteMode mode = ExecuteMode.Sequence;

    /// <summary>
    /// 用途：配置每个目标完成一次移动所需的时间。
    /// 调用边界：所有有效目标共用该值，并使用线性缓动。
    /// 注意事项：单位为秒；小于或等于 0 时在该目标轮次立即写入终点。
    /// </summary>
    [Tooltip("每个目标的移动时长（秒）")]
    [Min(0f)]
    [SerializeField] private float duration = 0.1f;

    /// <summary>
    /// 用途：配置整组顺序移动开始前的等待时间。
    /// 调用边界：每次 Execute 只在第一个有效目标动作前等待一次。
    /// 注意事项：单位为秒，不会重复作用于后续目标。
    /// </summary>
    [Tooltip("整组顺序移动开始前的延迟（秒）")]
    [Min(0f)]
    [SerializeField] private float delay;

    /// <summary>
    /// 用途：配置 Sequence 模式下相邻两个有效目标移动动作之间的等待时间。
    /// 调用边界：前一个目标完成移动后开始计时，计时结束后下一个目标才开始移动；Parallel 模式不读取该值。
    /// 注意事项：单位为秒；首个目标之前和最后一个目标之后都不会额外应用该值。
    /// </summary>
    [Tooltip("Sequence 模式下相邻有效目标移动动作之间的间隔（秒）；Parallel 模式忽略")]
    [Min(0f)]
    [SerializeField] private float interval;

    #endregion

    #region Runtime State

    private Sequence _moveSequence;

    #endregion

    #region Unity Lifecycle

    private void OnDestroy()
    {
        KillMoveSequence();
    }

    #endregion

    #region BaseItemEventHandle

    /// <summary>
    /// 用途：整体等待 delay 后，按 mode 配置顺序或并行执行 targets 的相对位移。
    /// 调用边界：由 TriggerExecutor/BaseItemExecutor 或明确的 IWanner 关卡本地逻辑调用。
    /// 注意事项：Sequence 应用 interval，Parallel 忽略 interval；重复调用会停止旧序列并重新捕获终点。
    /// </summary>
    public override void Execute()
    {
        if (!Application.isPlaying)
            return;

        KillMoveSequence();
        IsRunning = true;
        IsDone = false;

        if (!HasValidTarget())
        {
            this.LogWarning("[IWannerGame_SequenceMoveListener] No valid target Transform is configured.");
            CompleteMovement();
            return;
        }

        float moveDuration = Mathf.Max(0f, duration);
        float startDelay = Mathf.Max(0f, delay);
        float moveInterval = Mathf.Max(0f, interval);
        Vector3 moveOffset = new Vector3(offset.x, offset.y, 0f);

        Sequence sequence = DOTween.Sequence();
        int appendedTargetCount = 0;
        for (int i = 0; i < targets.Count; i++)
        {
            Transform sequenceTarget = targets[i];
            if (sequenceTarget == null)
                continue;

            if (mode == ExecuteMode.Sequence && appendedTargetCount > 0 && moveInterval > 0f)
                sequence.AppendInterval(moveInterval);

            Vector3 endPosition = sequenceTarget.position + moveOffset;
            if (moveDuration <= 0f)
            {
                if (mode == ExecuteMode.Parallel)
                    sequence.InsertCallback(0f, () => ApplyEndPosition(sequenceTarget, endPosition));
                else
                    sequence.AppendCallback(() => ApplyEndPosition(sequenceTarget, endPosition));
            }
            else
            {
                Tween moveTween = sequenceTarget
                    .DOMove(endPosition, moveDuration)
                    .SetEase(Ease.Linear);

                if (mode == ExecuteMode.Parallel && appendedTargetCount > 0)
                    sequence.Join(moveTween);
                else
                    sequence.Append(moveTween);
            }

            appendedTargetCount++;
        }

        _moveSequence = sequence
            .SetDelay(startDelay)
            .OnComplete(CompleteMovement)
            .OnKill(CompleteMovement);
    }

    /// <summary>
    /// 用途：停止未完成的顺序移动并恢复监听器的未执行状态。
    /// 调用边界：供 BaseItemExecutor 新一轮执行前或关卡 Reset 流程调用。
    /// 注意事项：只停止后续动作并重置执行标志，不恢复已经产生的 Transform 位移。
    /// </summary>
    public override void ResetState()
    {
        KillMoveSequence();
        base.ResetState();
    }

    #endregion

    #region Internal

    private bool HasValidTarget()
    {
        if (targets == null)
            return false;

        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] != null)
                return true;
        }

        return false;
    }

    private static void ApplyEndPosition(Transform moveTarget, Vector3 endPosition)
    {
        if (moveTarget != null)
            moveTarget.position = endPosition;
    }

    private void CompleteMovement()
    {
        _moveSequence = null;
        IsRunning = false;
        IsDone = true;
    }

    private void KillMoveSequence()
    {
        if (_moveSequence != null && _moveSequence.IsActive())
            _moveSequence.Kill(false);

        _moveSequence = null;
    }

    #endregion

    #region Gizmos

    /// <summary>
    /// 用途：在编辑器中展示每个有效目标的移动方向与终点。
    /// 调用边界：只读取 targets 与 offset 的本地序列化数据，不触发运行时执行链。
    /// 注意事项：预览以编辑器当前坐标为起点，不包含 delay、duration 或 interval 的时间表现。
    /// </summary>
    private void OnDrawGizmos()
    {
        if (targets == null)
            return;

        Vector3 moveOffset = new Vector3(offset.x, offset.y, 0f);
        Gizmos.color = Color.green;

        for (int i = 0; i < targets.Count; i++)
        {
            Transform sequenceTarget = targets[i];
            if (sequenceTarget == null)
                continue;

            Vector3 startPosition = sequenceTarget.position;
            Vector3 endPosition = startPosition + moveOffset;
            Gizmos.DrawLine(startPosition, endPosition);
            Gizmos.DrawWireSphere(endPosition, 0.1f);
        }
    }

    #endregion
}
