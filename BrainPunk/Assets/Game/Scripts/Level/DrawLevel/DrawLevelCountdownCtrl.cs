using Bear.EventSystem;
using Bear.Logger;
using Game.Events;
using UnityEngine;

/// <summary>
/// Draw 单关可选的画线后倒计时控制器。
/// 第一笔有效画线完成后启动倒计时，最后五秒通过 Bear Event 通知 Draw HUD，
/// 归零后按配置提交关卡成功，或复用 DrawLevelCtrl 的失败重置链。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Draw Level Countdown Ctrl")]
public sealed class DrawLevelCountdownCtrl : MonoBehaviour,
    IDrawStrokeCompletedHandler,
    IDebuger,
    IEventSender
{
    #region Constants

    private const int CountdownVisibleSeconds = 5;

    #endregion

    #region Inspector

    /// <summary>
    /// 第一笔有效画线完成后的倒计时总时长，单位为秒。
    /// 调用边界：仅在当前 DLevel 的一次尝试内使用；小于或等于 0 时第一笔完成后立即结算。
    /// </summary>
    [Header("倒计时")]
    [Tooltip("第一笔有效画线完成后的倒计时总时长，单位为秒。")]
    [Min(0f)]
    [SerializeField] private float cdTime = 20f;

    /// <summary>
    /// 倒计时结束时是否按关卡成功结算。
    /// 调用边界：默认 false 保持超时失败重置；仅由需要“坚持到时间结束即胜利”的 DLevel 显式开启。
    /// </summary>
    [Tooltip("倒计时结束时是否提交关卡成功；默认关闭并沿用超时失败重置。")]
    [SerializeField] private bool isSuccess = false;

    /// <summary>
    /// 同一 DLevel 根节点上的通用状态控制器。
    /// 调用边界：必须指向当前关卡，不能跨关卡引用；为空时仅在运行时从父层级解析。
    /// </summary>
    [Tooltip("当前 DLevel 根节点上的 DrawLevelCtrl。")]
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;

    #endregion

    #region Runtime State

    private float _remainingTime;
    private int _lastDisplayedSeconds = -1;
    private bool _isCountingDown;
    private bool _isCountdownVisible;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        if (drawLevelCtrl != null)
            drawLevelCtrl.Resolved += OnLevelResolved;
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        if (drawLevelCtrl != null)
            drawLevelCtrl.Resolved -= OnLevelResolved;

        StopCountdown();
    }

    private void Update()
    {
        if (!_isCountingDown ||
            drawLevelCtrl == null ||
            drawLevelCtrl.IsPaused ||
            drawLevelCtrl.IsFailurePerformanceActive)
        {
            return;
        }

        _remainingTime = Mathf.Max(0f, _remainingTime - Time.deltaTime);
        if (_remainingTime <= 0f)
        {
            CompleteCountdown();
            return;
        }

        RefreshCountdownDisplay();
    }

    #endregion

    #region IDrawStrokeCompletedHandler

    /// <inheritdoc />
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying || !result.IsFirstStroke || _isCountingDown)
            return;

        ResolveReferences();
        if (drawLevelCtrl == null)
        {
            this.LogError("[DrawLevelCountdownCtrl] DrawLevelCtrl 缺失，无法启动画线后倒计时。");
            return;
        }

        _remainingTime = Mathf.Max(0f, cdTime);
        _lastDisplayedSeconds = -1;
        _isCountingDown = true;

        if (_remainingTime <= 0f)
        {
            CompleteCountdown();
            return;
        }

        RefreshCountdownDisplay();
    }

    /// <inheritdoc />
    public void OnDrawAttemptReset()
    {
        StopCountdown();
    }

    #endregion

    #region Countdown

    private void RefreshCountdownDisplay()
    {
        int displayedSeconds = Mathf.CeilToInt(_remainingTime);
        if (displayedSeconds > CountdownVisibleSeconds)
        {
            HideCountdown();
            return;
        }

        if (_isCountdownVisible && displayedSeconds == _lastDisplayedSeconds)
            return;

        _isCountdownVisible = true;
        _lastDisplayedSeconds = displayedSeconds;
        this.DispatchEvent(
            Witness<DrawGameCountdownChangedEvent>._,
            true,
            displayedSeconds);
    }

    private void CompleteCountdown()
    {
        _isCountingDown = false;
        HideCountdown();

        if (isSuccess)
        {
            drawLevelCtrl?.TryResolve(new DrawLevelResult(
                DrawLevelResultType.Success,
                nameof(DrawLevelCountdownCtrl)));
            return;
        }

        drawLevelCtrl?.TryFailAndReset(nameof(DrawLevelCountdownCtrl));
    }

    private void OnLevelResolved(DrawLevelResult result)
    {
        StopCountdown();
    }

    private void StopCountdown()
    {
        _isCountingDown = false;
        _remainingTime = 0f;
        _lastDisplayedSeconds = -1;
        HideCountdown();
    }

    private void HideCountdown()
    {
        if (!_isCountdownVisible)
            return;

        _isCountdownVisible = false;
        this.DispatchEvent(
            Witness<DrawGameCountdownChangedEvent>._,
            false,
            0);
    }

    private void ResolveReferences()
    {
        if (drawLevelCtrl == null)
            drawLevelCtrl = GetComponentInParent<DrawLevelCtrl>(true);
    }

    #endregion
}
