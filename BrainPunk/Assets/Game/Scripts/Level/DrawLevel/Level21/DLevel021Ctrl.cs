using Bear.Logger;
using UnityEngine;

/// <summary>
/// 使用单一计时周期控制两侧 WaterSplash；每轮开始时按当前 Draw 接触状态选择播放内容。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/DLevel 021 Ctrl")]
public sealed class DLevel021Ctrl : MonoBehaviour, IDrawStrokeCompletedHandler, IDebuger
{
    #region Animator Parameters

    private static readonly int ShortTriggerHash = Animator.StringToHash("short");
    private static readonly int LongTriggerHash = Animator.StringToHash("long");
    private static readonly int StayShortHash = Animator.StringToHash("stay_short");
    private static readonly int StayLongHash = Animator.StringToHash("stay_long");

    #endregion

    #region Inspector References

    /// <summary>
    /// 左侧 WaterSplash 上方的 Draw 接触感应区。
    /// 调用边界：必须绑定左侧 WaterSplash 子节点的 Trigger Collider2D。
    /// </summary>
    [Header("Level References")]
    [Tooltip("左侧 WaterSplash 上方用于检测 Draw 线段的 Trigger。")]
    [SerializeField] private Collider2D leftTouchArea;

    /// <summary>
    /// 右侧 WaterSplash 上方的 Draw 接触感应区。
    /// 调用边界：必须绑定右侧 WaterSplash 子节点的 Trigger Collider2D。
    /// </summary>
    [Tooltip("右侧 WaterSplash 上方用于检测 Draw 线段的 Trigger。")]
    [SerializeField] private Collider2D rightTouchArea;

    /// <summary>
    /// 左侧 WaterSplash Animator。
    /// 注意事项：Controller 必须提供 short、long、stay_short、stay_long 参数。
    /// </summary>
    [Tooltip("左侧 WaterSplash Animator。")]
    [SerializeField] private Animator leftWaterSplashAnimator;

    /// <summary>
    /// 右侧 WaterSplash Animator。
    /// 注意事项：Controller 必须提供 short、long、stay_short、stay_long 参数。
    /// </summary>
    [Tooltip("右侧 WaterSplash Animator。")]
    [SerializeField] private Animator rightWaterSplashAnimator;

    #endregion

    #region Inspector Settings

    /// <summary>
    /// 可触发 TouchArea 的画线 Layer；当前 Prefab 绑定 Layer 14 Draw。
    /// </summary>
    [Header("Splash Settings")]
    [Tooltip("可触发 TouchArea 的画线 Layer。")]
    [SerializeField] private LayerMask drawLayerMask = 1 << 14;

    /// <summary>
    /// 两轮水花之间的待机时间，沿用主线 WaterSplashCtrl 的 3 秒基线。
    /// </summary>
    [Tooltip("两轮水花之间的待机时间。")]
    [SerializeField, Min(0f)] private float idleInterval = 3f;

    /// <summary>
    /// 单轮 short 或 water_2 保持时间，沿用主线 WaterSplashCtrl 的 3 秒基线。
    /// </summary>
    [Tooltip("单轮 short 或 water_2 的保持时间。")]
    [SerializeField, Min(0f)] private float splashDuration = 3f;

    #endregion

    #region Runtime State

    private float _cycleTimer;
    private bool _isPlaying;
    private bool _hasFirstStrokeCompleted;
    private bool _isInitialized;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        ResetRuntimeState();
        _isInitialized = ValidateReferences();
    }

    private void FixedUpdate()
    {
        if (!_isInitialized || !_hasFirstStrokeCompleted)
            return;

        UpdateSplashCycle(Time.fixedDeltaTime);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        _isInitialized = false;
        ResetRuntimeState();
    }

    #endregion

    #region IDrawStrokeCompletedHandler

    /// <summary>
    /// 第一笔有效画线完成后从零启动喷水周期；后续笔画不会重新计时。
    /// 调用边界：仅由当前关卡的 DrawLevelCtrl 画线完成通知调用。
    /// </summary>
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying || !result.IsFirstStroke || _hasFirstStrokeCompleted)
            return;

        _hasFirstStrokeCompleted = true;
        _cycleTimer = 0f;
    }

    /// <summary>
    /// 当前画线尝试重置时停止喷水，并恢复为等待第一笔完成的状态。
    /// 调用边界：由 DrawLevelCtrl 的本地尝试重置流程调用。
    /// </summary>
    public void OnDrawAttemptReset()
    {
        if (!Application.isPlaying)
            return;

        ResetRuntimeState();
    }

    #endregion

    #region Touch State

    private int GetTouchMask()
    {
        int mask = 0;
        int layerMask = drawLayerMask.value;

        if (leftTouchArea.IsTouchingLayers(layerMask))
            mask |= 1;

        if (rightTouchArea.IsTouchingLayers(layerMask))
            mask |= 2;

        return mask;
    }

    #endregion

    #region Splash Cycle

    private void UpdateSplashCycle(float deltaTime)
    {
        _cycleTimer += deltaTime;

        if (_isPlaying)
        {
            if (_cycleTimer < splashDuration)
                return;

            StopAllWaterSplashes();
            _isPlaying = false;
            _cycleTimer = 0f;
            return;
        }

        if (_cycleTimer < idleInterval)
            return;

        PlayCurrentSelection();
        _isPlaying = true;
        _cycleTimer = 0f;
    }

    private void PlayCurrentSelection()
    {
        switch (GetTouchMask())
        {
            case 0:
                PlayShortSplash(leftWaterSplashAnimator);
                PlayShortSplash(rightWaterSplashAnimator);
                break;
            case 1:
                PlayLongSplash(rightWaterSplashAnimator);
                break;
            case 2:
                PlayLongSplash(leftWaterSplashAnimator);
                break;
        }
    }

    private static void PlayShortSplash(Animator animator)
    {
        if (animator == null)
            return;

        animator.ResetTrigger(LongTriggerHash);
        animator.ResetTrigger(ShortTriggerHash);
        animator.SetBool(StayLongHash, false);
        animator.SetBool(StayShortHash, true);
        animator.SetTrigger(ShortTriggerHash);
    }

    private static void PlayLongSplash(Animator animator)
    {
        if (animator == null)
            return;

        animator.ResetTrigger(ShortTriggerHash);
        animator.ResetTrigger(LongTriggerHash);
        animator.SetBool(StayShortHash, false);
        animator.SetBool(StayLongHash, true);
        animator.SetTrigger(LongTriggerHash);
    }

    private void StopAllWaterSplashes()
    {
        StopWaterSplash(leftWaterSplashAnimator);
        StopWaterSplash(rightWaterSplashAnimator);
    }

    private static void StopWaterSplash(Animator animator)
    {
        if (animator == null)
            return;

        animator.SetBool(StayShortHash, false);
        animator.SetBool(StayLongHash, false);
        animator.ResetTrigger(ShortTriggerHash);
        animator.ResetTrigger(LongTriggerHash);
    }

    private void ResetRuntimeState()
    {
        StopAllWaterSplashes();
        _isPlaying = false;
        _hasFirstStrokeCompleted = false;
        _cycleTimer = 0f;
    }

    #endregion

    #region Validation

    private bool ValidateReferences()
    {
        bool isValid = true;

        if (leftTouchArea == null || rightTouchArea == null)
        {
            this.LogError("[DLevel021Ctrl] 左右 TouchArea 未完整绑定，无法检测 Draw 线段。");
            isValid = false;
        }
        else if (leftTouchArea == rightTouchArea)
        {
            this.LogError("[DLevel021Ctrl] 左右 TouchArea 绑定了同一 Collider2D。");
            isValid = false;
        }

        if (leftWaterSplashAnimator == null || rightWaterSplashAnimator == null)
        {
            this.LogError("[DLevel021Ctrl] 左右 WaterSplash Animator 未完整绑定，无法播放水花循环。");
            isValid = false;
        }
        else if (leftWaterSplashAnimator == rightWaterSplashAnimator)
        {
            this.LogError("[DLevel021Ctrl] 左右 WaterSplash 绑定了同一 Animator。");
            isValid = false;
        }

        if (drawLayerMask.value == 0)
        {
            this.LogError("[DLevel021Ctrl] Draw LayerMask 为空，TouchArea 永远不会响应画线。");
            isValid = false;
        }

        return isValid;
    }

    #endregion
}
