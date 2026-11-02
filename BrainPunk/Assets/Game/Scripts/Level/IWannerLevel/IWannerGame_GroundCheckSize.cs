using Bear.Logger;
using DG.Tweening;
using Game.ItemEvent;
using UnityEngine;

/// <summary>
/// IWanner Actor 地面检测框尺寸变化监听器。
/// 由 TriggerExecutor/BaseItemExecutor 触发后，在指定时间内把当前 Actor 的 GroundCheckSize 线性变化到目标值。
/// </summary>
public sealed class IWannerGame_GroundCheckSize : BaseItemEventHandle, IDebuger
{
    #region Serialized Fields

    /// <summary>
    /// 用途：指定需要修改 GroundCheckSize 的 IWanner Actor。
    /// 调用边界：仅在 Execute 时读取；未配置时会从当前对象或父级 IWannerLevelCtrl 的子层级查找。
    /// 注意事项：应指向当前活动小关的 Actor，避免影响非活动或即将销毁的实例。
    /// </summary>
    [Header("Target")]
    [Tooltip("需要修改 GroundCheckSize 的 IWanner Actor；留空时从当前关卡层级自动查找")]
    [SerializeField] private Actor_IWanner target;

    /// <summary>
    /// 用途：配置触发后地面 BoxCast 检测框尺寸的最终值。
    /// 调用边界：每次 Execute 都从 Actor 当前尺寸线性变化到该绝对值。
    /// 注意事项：单位为 Unity 世界单位；运行时会分别把 X/Y 负值按 0 处理。
    /// </summary>
    [Header("Ground Check Size Settings")]
    [Tooltip("GroundCheckSize 的目标值（Unity 世界单位）")]
    [SerializeField] private Vector2 targetGroundCheckSize = new Vector2(1f, 0.2f);

    /// <summary>
    /// 用途：配置 GroundCheckSize 完成变化所需的时间。
    /// 调用边界：每次 Execute 创建的线性 Tween 使用该时长。
    /// 注意事项：单位为秒；小于或等于 0 时立即应用目标值。
    /// </summary>
    [Tooltip("GroundCheckSize 变化时长（秒）")]
    [Min(0f)]
    [SerializeField] private float duration = 0.1f;

    #endregion

    #region Runtime State

    private Tweener _sizeTweener;

    #endregion

    #region Unity Lifecycle

    private void OnDestroy()
    {
        KillTweener();
    }

    #endregion

    #region BaseItemEventHandle

    /// <summary>
    /// 用途：从目标 Actor 当前值开始，线性变化到 Inspector 配置的 GroundCheckSize。
    /// 调用边界：由 TriggerExecutor/BaseItemExecutor 或明确的 IWanner 关卡本地逻辑调用。
    /// 注意事项：重复调用会停止旧 Tween 并从当时的当前值重新开始；找不到 Actor 时记录警告并结束执行。
    /// </summary>
    public override void Execute()
    {
        if (!Application.isPlaying)
            return;

        KillTweener();

        Actor_IWanner targetActor = ResolveTarget();
        if (targetActor == null)
        {
            this.LogWarning("[IWannerGame_GroundCheckSize] Actor_IWanner is missing.");
            CompleteExecution();
            return;
        }

        IsRunning = true;
        IsDone = false;

        Vector2 endSize = new Vector2(
            Mathf.Max(0f, targetGroundCheckSize.x),
            Mathf.Max(0f, targetGroundCheckSize.y));
        float changeDuration = Mathf.Max(0f, duration);
        if (changeDuration <= 0f)
        {
            targetActor.SetGroundCheckSize(endSize);
            CompleteExecution();
            return;
        }

        _sizeTweener = DOTween.To(
                targetActor.GetGroundCheckSize,
                targetActor.SetGroundCheckSize,
                endSize,
                changeDuration)
            .SetEase(Ease.Linear)
            .OnComplete(CompleteExecution);
    }

    /// <summary>
    /// 用途：停止未完成的尺寸变化并恢复监听器的未执行状态。
    /// 调用边界：供 BaseItemExecutor 新一轮执行前或关卡 Reset 流程调用。
    /// 注意事项：只重置执行状态，不恢复 Actor 触发前的 GroundCheckSize。
    /// </summary>
    public override void ResetState()
    {
        KillTweener();
        base.ResetState();
    }

    #endregion

    #region Internal

    private Actor_IWanner ResolveTarget()
    {
        if (target != null)
            return target;

        target = GetComponentInParent<Actor_IWanner>(true);
        if (target != null)
            return target;

        IWannerLevelCtrl levelCtrl = GetComponentInParent<IWannerLevelCtrl>(true);
        if (levelCtrl != null)
            target = levelCtrl.GetComponentInChildren<Actor_IWanner>(true);

        return target;
    }

    private void CompleteExecution()
    {
        _sizeTweener = null;
        IsRunning = false;
        IsDone = true;
    }

    private void KillTweener()
    {
        if (_sizeTweener != null && _sizeTweener.IsActive())
            _sizeTweener.Kill(false);
        _sizeTweener = null;
    }

    #endregion
}
