using DG.Tweening;
using Game.ItemEvent;
using UnityEngine;

/// <summary>
/// 可以通过 TriggerExecutor 触发。提供 Vector2 控制物体移动 offset OnGizmos 展示最终的移动位置，包含移动方向线段。
/// 提供 durtaion 和 delay 给我设置参数。duration 默认 0.1。delay 默认 0；
/// </summary>
public class IWannerGame_MoveTriggerListener : BaseItemEventHandle
{
    #region Serialized Fields

    [Header("Target")]
    [Tooltip("要移动的目标；为空时默认移动自身所在 Transform")]
    [SerializeField] private Transform target;

    [Header("Move Settings")]
    [Tooltip("相对触发时位置的移动偏移（仅生效 X/Y，Z 保持不变）")]
    [SerializeField] private Vector2 offset;

    [Tooltip("移动时长（秒），默认 0.1")]
    [SerializeField] private float duration = 0.1f;

    [Tooltip("触发后延迟多少秒开始移动，默认 0")]
    [SerializeField] private float delay = 0f;

    #endregion

    #region Runtime State

    private Tweener _moveTweener;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (target == null)
            target = transform;
    }

    private void OnDestroy()
    {
        KillTweener();
    }

    #endregion

    #region BaseItemEventHandle

    /// <summary>
    /// 由 TriggerExecutor/BaseItemExecutor 调用：延迟 delay 秒后，用 duration 秒把 target 按 offset 平移。
    /// 移动期间 IsRunning 为 true，完成（或被中断）后置 IsDone，供顺序执行模式继续推进。
    /// 注意：偏移基于触发瞬间的位置，多次触发会累积位移。
    /// </summary>
    public override void Execute()
    {
        if (target == null)
        {
            Debug.LogWarning("[IWannerGame_MoveTriggerListener] Target is null!");
            IsDone = true;
            return;
        }

        KillTweener();

        IsRunning = true;
        Vector3 endPos = target.position + new Vector3(offset.x, offset.y, 0f);
        _moveTweener = target.DOMove(endPos, duration)
            .SetDelay(delay)
            .SetEase(Ease.Linear)
            .OnComplete(OnMoveFinished)
            .OnKill(OnMoveFinished);
    }

    /// <summary>
    /// 重置运行状态并停止未完成的移动，供 BaseItemExecutor 重新执行前或关卡 Reset 时调用。
    /// </summary>
    public override void ResetState()
    {
        KillTweener();
        base.ResetState();
    }

    #endregion

    #region Internal

    private void OnMoveFinished()
    {
        IsRunning = false;
        IsDone = true;
    }

    private void KillTweener()
    {
        if (_moveTweener != null && _moveTweener.IsActive())
            _moveTweener.Kill(false);
        _moveTweener = null;
    }

    #endregion

    #region Gizmos

    /// <summary>
    /// 编辑器下展示移动方向线段与最终落点（线框球），仅使用本地序列化数据，不触发运行逻辑。
    /// </summary>
    private void OnDrawGizmos()
    {
        Transform t = target != null ? target : transform;
        Vector3 start = t.position;
        Vector3 end = start + new Vector3(offset.x, offset.y, 0f);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawWireSphere(end, 0.1f);
    }

    #endregion
}
