using Sirenix.OdinInspector;
using UnityEngine;

public class CameraResolutionAutoFit : MonoBehaviour
{
    [Tooltip("宽高比超过此值时按超宽屏处理，直接使用当前屏幕分辨率")]
    private float aspectRatioThreshold = 2.0f;

    [Tooltip("常规模式下的设计分辨率（如 1920x1080）")]
    [SerializeField] private Vector2 fixResolution = new Vector2(1920, 1080);

    [SerializeField] private Camera mainCamera;

    private Vector2 _screenResolution;

    private void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

#if UNITY_EDITOR
        _screenResolution = new Vector2(Screen.width, Screen.height);
#else
        var r = Screen.currentResolution;
        _screenResolution = new Vector2(r.width, r.height);
#endif

        ApplyFit();
    }

    [Button("Refresh")]
    private void ApplyFit()
    {
        if (mainCamera == null)
            return;

        float screenAspect = _screenResolution.x / _screenResolution.y;
        Vector2 target = screenAspect >= aspectRatioThreshold ? _screenResolution : fixResolution;
        float targetAspect = target.x / target.y;

        if (screenAspect > targetAspect)
        {
            float w = targetAspect / screenAspect;
            mainCamera.rect = new Rect((1f - w) * 0.5f, 0f, w, 1f);
        }
        else
        {
            float h = screenAspect / targetAspect;
            mainCamera.rect = new Rect(0f, (1f - h) * 0.5f, 1f, h);
        }
    }
}
