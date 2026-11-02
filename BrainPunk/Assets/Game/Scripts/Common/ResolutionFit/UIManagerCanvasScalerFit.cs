using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

public class UIManagerCanvasScalerFit : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform targetRoot;

    [Header("Runtime")]
    [SerializeField] private bool applyOnStart = true;
    [SerializeField] private bool autoUpdateOnResolutionChange = true;

    [Header("Resolution Switch (参考 CameraResolutionAutoFit)")]
    [Tooltip("切换阈值：当 AspectRatio 高于此值时使用 index=1 的分辨率")]
    [SerializeField] private float aspectRatioThreshold = 2.17f;

    [Tooltip("1920x1080=16:9, 2340x1080≈21.67:9；设为空则使用各 CanvasScaler 自身的 referenceResolution；仅影响 Reference Resolution，不控制 Match")]
    [SerializeField] private List<Vector2> fixResolutionList = new List<Vector2>
    {
        new Vector2(1920, 1080),
        new Vector2(2340, 1080)
    };

    [Header("Match Range")]
    [SerializeField, Range(0f, 1f)] private float minMatch = 0f;
    [SerializeField, Range(0f, 1f)] private float maxMatch = 1f;

    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;

    private bool isDirty = false;

    private void Awake()
    {
        if (targetRoot == null)
            targetRoot = transform;
    }

    private void Start()
    {
        isDirty = false;
        if (applyOnStart)
            RefreshAllCanvasScalerMatch();
    }

    private void Update()
    {
        if (!autoUpdateOnResolutionChange)
            return;

        if (Screen.width == lastScreenWidth && Screen.height == lastScreenHeight && !isDirty)
            return;

        RefreshAllCanvasScalerMatch();
    }

    [Button("Refresh All CanvasScaler Match")]
    public void RefreshAllCanvasScalerMatch()
    {
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        if (lastScreenWidth <= 0 || lastScreenHeight <= 0)
            return;

        if (targetRoot == null)
        {
            isDirty = true;
            return;
        }

        var scalers = targetRoot.GetComponentsInChildren<CanvasScaler>(true);
        if (scalers == null || scalers.Length == 0)
        {
            isDirty = true;
            return;
        }

        float screenAspect = (float)lastScreenWidth / lastScreenHeight;

        bool useFixResolution = fixResolutionList != null && fixResolutionList.Count > 0;
        int index = 0;
        Vector2 effectiveReferenceResolution = Vector2.zero;

        if (useFixResolution)
        {
            index = screenAspect > aspectRatioThreshold ? 1 : 0;
            index = Mathf.Clamp(index, 0, fixResolutionList.Count - 1);
            effectiveReferenceResolution = fixResolutionList[index];
        }

        float clampedMin = Mathf.Clamp01(minMatch);
        float clampedMax = Mathf.Clamp01(maxMatch);

        for (int i = 0; i < scalers.Length; i++)
        {
            CanvasScaler scaler = scalers[i];
            if (scaler == null)
                continue;

            Vector2 referenceResolution = useFixResolution
                ? effectiveReferenceResolution
                : scaler.referenceResolution;

            if (referenceResolution.x <= 0f || referenceResolution.y <= 0f)
                continue;

            if (useFixResolution)
                scaler.referenceResolution = referenceResolution;

            float referenceAspect = referenceResolution.x / referenceResolution.y;
            float rawMatch = Mathf.InverseLerp(referenceAspect * 0.75f, referenceAspect * 1.25f, screenAspect);
            scaler.matchWidthOrHeight = Mathf.Lerp(clampedMin, clampedMax, rawMatch);
        }

        isDirty = false;
    }
}
