using UnityEngine;

/// <summary>第 80 关足球专用失败区；只接受显式绑定的足球刚体，走标准 Draw 失败重置。</summary>
[DisallowMultipleComponent]
public sealed class DLevel80FailAreaHandle : MonoBehaviour, IDrawStrokeCompletedHandler
{
    #region Inspector

    /// <summary>同关 Draw 控制器，必须显式绑定；用于结果门禁与原子提交失败。</summary>
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;

    /// <summary>本关足球刚体，必须显式绑定；玩家、enemy 或其它球不会被当作足球。</summary>
    [SerializeField] private Rigidbody2D football;

    #endregion

    #region Runtime State

    private bool _pendingFailure;

    #endregion

    #region Trigger API

    /// <summary>
    /// 足球进入失败区时调用；可接动态 Collider2D 事件，也由本组件 Trigger 自动转发。
    /// 通过 attachedRigidbody 身份匹配足球；暂停期间进入记为待失败，恢复后才提交。
    /// </summary>
    public void OnFootballEnter(Collider2D collider)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || collider == null || football == null ||
            collider.attachedRigidbody != football || drawLevelCtrl == null ||
            !drawLevelCtrl.IsRuntimeReady || drawLevelCtrl.IsOutcomeLocked)
            return;
        _pendingFailure = true;
        TrySubmitFailure();
    }

    private void OnTriggerEnter2D(Collider2D collider) => OnFootballEnter(collider);

    // 保持区域内的判定；重复接触由共享结果门禁去重。
    private void OnTriggerStay2D(Collider2D collider) => OnFootballEnter(collider);

    #endregion

    #region Draw Lifecycle

    /// <summary>笔画完成不触发失败；实现既有接口只为接收本关同实例 Reset。</summary>
    public void OnDrawStrokeCompleted(DrawStrokeResult result) { }

    /// <summary>同实例重置清除暂停期间记录的进球，避免旧尝试失败污染新尝试。</summary>
    public void OnDrawAttemptReset()
    {
        if (Application.isPlaying)
            _pendingFailure = false;
    }

    private void Update()
    {
        if (Application.isPlaying && _pendingFailure)
            TrySubmitFailure();
    }

    private void OnDisable() => _pendingFailure = false;

    private void TrySubmitFailure()
    {
        if (drawLevelCtrl == null || football == null || drawLevelCtrl.IsOutcomeLocked)
        {
            _pendingFailure = false;
            return;
        }
        if (!drawLevelCtrl.IsRuntimeReady || drawLevelCtrl.IsPaused)
            return;
        _pendingFailure = false;
        drawLevelCtrl.TryFailAndReset(nameof(DLevel80FailAreaHandle));
    }

    #endregion
}
