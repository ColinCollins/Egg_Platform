using DG.Tweening;
using Game.ItemEvent;
using UnityEngine;

/// <summary>
/// IWanner 缩放触发监听器：被执行时将目标 Transform 的 localScale X/Y 缩放到指定值。
/// </summary>
public sealed class IWannerGame_ScaleTriggerListener : BaseItemEventHandle
{
    #region Serialized Fields

    /// <summary>
    /// 用途：指定实际缩放的目标；为空时使用当前组件所在 Transform。
    /// 调用边界：仅由运行时触发执行链读取。
    /// 注意事项：缩放使用 localScale，不改变目标的世界坐标和旋转。
    /// </summary>
    [Header("Target")]
    [Tooltip("要缩放的目标；为空时默认缩放自身所在 Transform")]
    [SerializeField] private Transform target;

    /// <summary>
    /// 用途：配置目标 localScale 的 X/Y 值。
    /// 调用边界：每次 Execute 都从目标当前缩放过渡到该绝对值。
    /// 注意事项：Z 保持触发瞬间的值；允许负值用于翻转表现。
    /// </summary>
    [Header("Scale Settings")]
    [Tooltip("目标 localScale（仅设置 X/Y，Z 保持不变）")]
    [SerializeField] private Vector2 targetScale = Vector2.one;

    /// <summary>
    /// 用途：配置完成缩放所需的时间。
    /// 调用边界：每次 Execute 创建的缩放 Tween 使用该时长。
    /// 注意事项：单位为秒；小于或等于 0 时立即应用目标缩放。
    /// </summary>
    [Tooltip("缩放时长（秒），默认 0.1")]
    [Min(0f)]
    [SerializeField] private float duration = 0.1f;

    /// <summary>
    /// 用途：配置触发后等待多久再开始缩放。
    /// 调用边界：每次 Execute 创建的缩放 Tween 使用该延迟。
    /// 注意事项：单位为秒；运行时按非负值处理。
    /// </summary>
    [Tooltip("触发后延迟多少秒开始缩放，默认 0")]
    [Min(0f)]
    [SerializeField] private float delay = 0f;

    #endregion

    #region Runtime State

    private Tweener _scaleTweener;

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
    /// 用途：把目标 localScale 的 X/Y 线性过渡到 Inspector 配置值。
    /// 调用边界：由 TriggerExecutor/BaseItemExecutor 或明确的关卡本地逻辑调用。
    /// 注意事项：重复调用会停止旧 Tween 并从当前缩放重新开始，Z 值保持不变。
    /// </summary>
    public override void Execute()
    {
        if (!Application.isPlaying)
            return;

        if (target == null)
        {
            IsRunning = false;
            IsDone = true;
            return;
        }

        KillTweener();

        IsRunning = true;
        IsDone = false;

        Vector3 currentScale = target.localScale;
        Vector3 endScale = new Vector3(targetScale.x, targetScale.y, currentScale.z);
        float scaleDuration = Mathf.Max(0f, duration);
        float scaleDelay = Mathf.Max(0f, delay);

        if (scaleDuration <= 0f && scaleDelay <= 0f)
        {
            target.localScale = endScale;
            CompleteScale();
            return;
        }

        _scaleTweener = target.DOScale(endScale, scaleDuration)
            .SetDelay(scaleDelay)
            .SetEase(Ease.Linear)
            .OnComplete(CompleteScale);
    }

    /// <summary>
    /// 用途：停止未完成的缩放并恢复监听器的未执行状态。
    /// 调用边界：供 BaseItemExecutor 新一轮执行前或关卡 Reset 流程调用。
    /// 注意事项：只重置执行状态，不恢复目标触发前的缩放值。
    /// </summary>
    public override void ResetState()
    {
        KillTweener();
        base.ResetState();
    }

    #endregion

    #region Internal

    private void CompleteScale()
    {
        _scaleTweener = null;
        IsRunning = false;
        IsDone = true;
    }

    private void KillTweener()
    {
        if (_scaleTweener != null && _scaleTweener.IsActive())
            _scaleTweener.Kill(false);
        _scaleTweener = null;
    }

    #endregion
}
