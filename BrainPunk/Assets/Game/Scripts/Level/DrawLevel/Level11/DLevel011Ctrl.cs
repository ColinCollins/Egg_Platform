using Bear.Logger;
using UnityEngine;

/// <summary>
/// DLevel_011 的按钮与水花控制器。
/// 指定对象压下按钮时启动 WaterSplash 的 water_2 长水柱状态，离开按钮后播放收起状态。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/DLevel 011 Ctrl")]
public sealed class DLevel011Ctrl : MonoBehaviour, IDebuger
{
    #region Animator Parameters

    private static readonly int LongTriggerHash = Animator.StringToHash("long");
    private static readonly int StayLongHash = Animator.StringToHash("stay_long");

    #endregion

    #region Inspector References

    /// <summary>
    /// 本关负责按 Tag 过滤触发对象的按钮。
    /// 调用边界：必须绑定 DLevel_011 Button/Trigger 上的 TagPressButton。
    /// </summary>
    [Header("Level References")]
    [Tooltip("DLevel_011 Button/Trigger 上的 TagPressButton。")]
    [SerializeField] private TagPressButton tagPressButton;

    /// <summary>
    /// 本关 WaterSplash 根节点上的 Animator。
    /// 注意事项：Animator Controller 必须包含 long Trigger 和 stay_long Bool 参数。
    /// </summary>
    [Tooltip("WaterSplash 根节点上的 Animator。")]
    [SerializeField] private Animator waterSplashAnimator;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        if (!ValidateReferences())
            return;

        tagPressButton.SetEnter(OnButtonPressed);
        tagPressButton.SetExit(OnButtonReleased);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        if (tagPressButton != null)
        {
            tagPressButton.SetEnter(null);
            tagPressButton.SetExit(null);
        }

        SetWaterSplashActive(false);
    }

    #endregion

    #region Button Callbacks

    private void OnButtonPressed()
    {
        if (waterSplashAnimator == null)
            return;

        waterSplashAnimator.ResetTrigger(LongTriggerHash);
        waterSplashAnimator.SetBool(StayLongHash, true);
        waterSplashAnimator.SetTrigger(LongTriggerHash);
    }

    private void OnButtonReleased()
    {
        SetWaterSplashActive(false);
    }

    #endregion

    #region Internal Helpers

    private bool ValidateReferences()
    {
        bool isValid = true;

        if (tagPressButton == null)
        {
            this.LogError("[DLevel011Ctrl] TagPressButton 未绑定，按钮无法触发关卡水花。");
            isValid = false;
        }

        if (waterSplashAnimator == null)
        {
            this.LogError("[DLevel011Ctrl] WaterSplash Animator 未绑定，按钮下压时无法播放 water_2 动画。");
            isValid = false;
        }

        return isValid;
    }

    private void SetWaterSplashActive(bool isActive)
    {
        if (waterSplashAnimator == null)
            return;

        waterSplashAnimator.SetBool(StayLongHash, isActive);
        if (!isActive)
            waterSplashAnimator.ResetTrigger(LongTriggerHash);
    }

    #endregion
}
