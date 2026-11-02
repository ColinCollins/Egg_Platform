using UnityEngine;

public class AutoFitScreen : MonoBehaviour
{
    public enum FitMode
    {
        Width,
        Height
    }

    [SerializeField] private Vector2 referenceResolution = new Vector2(1080f, 1920f);
    [SerializeField] private FitMode fitMode = FitMode.Width;
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool autoUpdate = true;

    private Vector3 initialScale;
    private int lastScreenWidth;
    private int lastScreenHeight;

    void Awake()
    {
        initialScale = transform.localScale;
        if (applyOnStart)
            ApplyFit();
    }

    void OnEnable()
    {
        if (applyOnStart)
            ApplyFit();
    }

    void Update()
    {
        if (!autoUpdate)
            return;

        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            ApplyFit();
        }
    }

    public void ApplyFit()
    {
        if (referenceResolution.x <= 0f || referenceResolution.y <= 0f)
            return;

        float scaleFactor = fitMode == FitMode.Width
            ? Screen.width / referenceResolution.x
            : Screen.height / referenceResolution.y;

        transform.localScale = initialScale * scaleFactor;
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
    }
}
