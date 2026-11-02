using UnityEngine;

/// <summary>
/// IWanner 游戏 HUD 的横向安全区适配组件。
/// 仅在运行时把屏幕左右安全区换算为 Root 父节点坐标，并叠加到 Root 的水平 offset；不会改动上下布局。
/// </summary>
[DisallowMultipleComponent]
public sealed class IWannerGame_GamePlayPanelSafeArea : MonoBehaviour
{
    #region Inspector References

    /// <summary>
    /// 用途：接收横向安全区的 HUD 根节点。
    /// 调用边界：仅由本组件在运行时修改水平 offset。
    /// 注意事项：目标需要保持左右拉伸锚点；上下 offset 会原样保留。
    /// </summary>
    [SerializeField, Tooltip("接收横向安全区的全拉伸 Root，仅修改其左右 offset。")]
    private RectTransform root;

    /// <summary>
    /// 用途：在左侧安全区基础上追加的 UI 本地坐标偏移。
    /// 调用边界：由美术或策划在 IWanner HUD prefab 上配置。
    /// 注意事项：正值向内收缩，负值会向安全区外扩展。
    /// </summary>
    [SerializeField, Tooltip("左侧安全区之外追加的本地偏移；正值向内，负值向外。")]
    private float leftOffset;

    /// <summary>
    /// 用途：在右侧安全区基础上追加的 UI 本地坐标偏移。
    /// 调用边界：由美术或策划在 IWanner HUD prefab 上配置。
    /// 注意事项：正值向内收缩，负值会向安全区外扩展。
    /// </summary>
    [SerializeField, Tooltip("右侧安全区之外追加的本地偏移；正值向内，负值向外。")]
    private float rightOffset;

    #endregion

    #region Runtime State

    private bool _hasBaseline;
    private bool _isApplying;
    private float _baselineLeftOffset;
    private float _baselineRightOffset;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        CaptureBaselineIfNeeded();
        ApplyHorizontalSafeArea();
    }

    private void Start()
    {
        if (!Application.isPlaying)
            return;

        CaptureBaselineIfNeeded();
        ApplyHorizontalSafeArea();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || _isApplying)
            return;

        CaptureBaselineIfNeeded();
        ApplyHorizontalSafeArea();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus || !Application.isPlaying || !isActiveAndEnabled)
            return;

        ApplyHorizontalSafeArea();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus || !Application.isPlaying || !isActiveAndEnabled)
            return;

        ApplyHorizontalSafeArea();
    }

    #endregion

    #region Safe Area

    private void CaptureBaselineIfNeeded()
    {
        if (_hasBaseline || root == null)
            return;

        _baselineLeftOffset = root.offsetMin.x;
        _baselineRightOffset = root.offsetMax.x;
        _hasBaseline = true;
    }

    private void ApplyHorizontalSafeArea()
    {
        if (_isApplying)
            return;

        RectTransform parentRect = root != null ? root.parent as RectTransform : null;
        if (!_hasBaseline || root == null || parentRect == null)
            return;

        float screenWidth = Screen.width;
        float parentWidth = parentRect.rect.width;
        if (screenWidth <= 0f || parentWidth <= 0f)
            return;

        Rect safeArea = Screen.safeArea;
        float leftInsetPixels = Mathf.Clamp(safeArea.xMin, 0f, screenWidth);
        float rightInsetPixels = Mathf.Clamp(screenWidth - safeArea.xMax, 0f, screenWidth);
        float unitsPerPixel = parentWidth / screenWidth;

        float targetLeftOffset = _baselineLeftOffset + leftInsetPixels * unitsPerPixel + leftOffset;
        float targetRightOffset = _baselineRightOffset - rightInsetPixels * unitsPerPixel - rightOffset;
        Vector2 currentMinOffset = root.offsetMin;
        Vector2 currentMaxOffset = root.offsetMax;
        bool needsLeftUpdate = !Mathf.Approximately(currentMinOffset.x, targetLeftOffset);
        bool needsRightUpdate = !Mathf.Approximately(currentMaxOffset.x, targetRightOffset);
        if (!needsLeftUpdate && !needsRightUpdate)
            return;

        _isApplying = true;
        try
        {
            if (needsLeftUpdate)
            {
                currentMinOffset.x = targetLeftOffset;
                root.offsetMin = currentMinOffset;
            }

            if (needsRightUpdate)
            {
                currentMaxOffset.x = targetRightOffset;
                root.offsetMax = currentMaxOffset;
            }
        }
        finally
        {
            _isApplying = false;
        }
    }

    #endregion
}
