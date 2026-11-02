using UnityEngine;

/// <summary>
/// 正交相机宽度完整显示适配器。
/// 以设计分辨率下的正交 Size 为基准：宽屏保持纵向视野并自然扩展左右，窄屏增大 Size 以完整容纳设计宽度。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
[AddComponentMenu("Game/Camera/Orthographic Size Fit")]
public sealed class CameraOrthographicSizeFit : MonoBehaviour
{
    #region Constants

    private const float MinimumPositiveValue = 0.0001f;

    #endregion

    #region Inspector Settings

    /// <summary>
    /// 设计画面的参考分辨率。
    /// 调用边界：只参与宽高比计算，不会修改屏幕分辨率或 Camera.rect。
    /// </summary>
    [Header("设计基准")]
    [Tooltip("设计画面的参考分辨率；默认 1920×1080。")]
    [SerializeField] private Vector2 referenceResolution = new(1920f, 1080f);

    /// <summary>
    /// 参考分辨率下的正交 Size。
    /// 注意事项：大于 0 时使用该值；为 0 时在 Awake 缓存 Camera 当前 Size 作为基准。
    /// </summary>
    [Tooltip("参考分辨率下的 Camera Orthographic Size；0 表示运行时缓存当前值。")]
    [SerializeField, Min(0f)] private float referenceOrthographicSize = 0f;

    /// <summary>
    /// 是否在 Camera viewport 尺寸变化后重新适配。
    /// 注意事项：只比较整数宽高，不会持续写入 Camera，也不会覆盖其它逐帧镜头表现。
    /// </summary>
    [Header("运行时")]
    [Tooltip("Camera viewport 尺寸变化后自动重新计算 Size。")]
    [SerializeField] private bool refreshOnViewportSizeChanged = true;

    #endregion

    #region Runtime State

    private Camera _targetCamera;
    private float _capturedOrthographicSize;
    private int _lastViewportWidth = -1;
    private int _lastViewportHeight = -1;
    private bool _hasStarted;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        _targetCamera = GetComponent<Camera>();
        if (_targetCamera != null &&
            _capturedOrthographicSize <= MinimumPositiveValue)
        {
            _capturedOrthographicSize = _targetCamera.orthographicSize;
        }
    }

    private void OnEnable()
    {
        if (!Application.isPlaying || !_hasStarted)
            return;

        ApplyFit();
    }

    private void Start()
    {
        if (!Application.isPlaying)
            return;

        _hasStarted = true;
        ApplyFit();
    }

    private void Update()
    {
        if (!Application.isPlaying || !refreshOnViewportSizeChanged || _targetCamera == null)
            return;

        GetViewportSize(out int viewportWidth, out int viewportHeight);
        if (viewportWidth == _lastViewportWidth && viewportHeight == _lastViewportHeight)
            return;

        ApplyFit();
    }

    #endregion

    #region Public API

    /// <summary>
    /// 立即按照当前 Camera viewport 宽高应用正交 Size 适配。
    /// 调用边界：仅在 PlayMode 生效，供分辨率、viewport 或相机基准配置发生变化后显式刷新。
    /// 注意事项：本方法不修改 Camera.rect；持续控制 orthographicSize 的其它脚本应自行协调调用时序。
    /// </summary>
    [ContextMenu("Refresh Orthographic Size Fit")]
    public void ApplyFit()
    {
        if (!Application.isPlaying)
            return;

        if (_targetCamera == null)
            _targetCamera = GetComponent<Camera>();

        if (_targetCamera == null || !_targetCamera.orthographic)
            return;

        if (_capturedOrthographicSize <= MinimumPositiveValue)
            _capturedOrthographicSize = _targetCamera.orthographicSize;

        GetViewportSize(out int viewportWidth, out int viewportHeight);
        if (viewportWidth <= 0 || viewportHeight <= 0 ||
            referenceResolution.x <= MinimumPositiveValue ||
            referenceResolution.y <= MinimumPositiveValue)
        {
            return;
        }

        float baseSize = referenceOrthographicSize > MinimumPositiveValue
            ? referenceOrthographicSize
            : _capturedOrthographicSize;
        if (baseSize <= MinimumPositiveValue)
            return;

        float referenceAspect = referenceResolution.x / referenceResolution.y;
        float viewportAspect = (float)viewportWidth / viewportHeight;
        float widthContainScale = Mathf.Max(1f, referenceAspect / viewportAspect);

        _targetCamera.orthographicSize = baseSize * widthContainScale;
        _lastViewportWidth = viewportWidth;
        _lastViewportHeight = viewportHeight;
    }

    #endregion

    #region Private Helpers

    private void GetViewportSize(out int width, out int height)
    {
        width = _targetCamera != null ? _targetCamera.pixelWidth : 0;
        height = _targetCamera != null ? _targetCamera.pixelHeight : 0;

        if (width > 0 && height > 0)
            return;

        width = Screen.width;
        height = Screen.height;
    }

    #endregion
}
