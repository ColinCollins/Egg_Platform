using System;
using System.Collections;
using Bear.EventSystem;
using Game.Events;
using Game.Play;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Level117 专用商店按钮拖拽与日食显示控制器。
/// 组件挂在本关 GameplayPanel 根节点，不改变共享 GamePlayPanel 和 CustomButton 的行为。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public sealed class Level117ShopButtonDrag : MonoBehaviour
{
    #region Inspector

    [Header("UI 引用")]
    /// <summary>可选的按钮引用；为空时自动查找 Root/shop_btn。</summary>
    [SerializeField] private RectTransform shopButton;
    /// <summary>商店按钮上的 CustomButton；为空时自动获取。</summary>
    [SerializeField] private CustomButton shopCustomButton;
    /// <summary>用于放置覆盖层和计算安全范围的 Root；为空时自动查找。</summary>
    [SerializeField] private RectTransform visualRoot;
    /// <summary>GameplayPanel 使用的 Canvas；为空时自动查找。</summary>
    [SerializeField] private Canvas targetCanvas;
    /// <summary>可选的 CanvasScaler，用于保留变体面板的缩放配置。</summary>
    [SerializeField] private CanvasScaler canvasScaler;
    /// <summary>投屏太阳位置使用的场景相机；为空时优先使用当前关卡的 GameCamera，再回退主相机。</summary>
    [SerializeField] private Camera worldCamera;
    /// <summary>太阳的世界空间节点；为空时自动在本关对象中查找名为 Sun 的节点。</summary>
    [SerializeField] private Transform sunTransform;
    /// <summary>太阳在世界空间的半径；小于等于零时从 Renderer.bounds 自动估算。</summary>
    [SerializeField, Min(0f)] private float sunRadiusWorld;
    /// <summary>找不到太阳 Renderer 时使用的屏幕半径（像素）。</summary>
    [SerializeField, Min(1f)] private float fallbackSunScreenRadius = 160f;

    [Header("日食视觉")]
    /// <summary>日光下覆盖场景的暖色层；运行时为空会自动创建。</summary>
    [SerializeField] private Image hotTintOverlay;
    /// <summary>日全食时覆盖场景的暗色层；运行时为空会自动创建。</summary>
    [SerializeField] private Image darkOverlay;
    /// <summary>日冕 RawImage；运行时为空会自动创建。</summary>
    [SerializeField] private RawImage coronaOverlay;
    /// <summary>日冕使用的透明贴图；为空时运行时生成柔和圆环。</summary>
    [SerializeField] private Texture2D coronaTexture;
    /// <summary>日冕可选材质，推荐绑定 Additive UI 材质。</summary>
    [SerializeField] private Material coronaMaterial;
    /// <summary>太阳被遮挡前的暖色调和最大透明度。</summary>
    [SerializeField] private Color hotTint = new(1f, 0.55f, 0.30f, 0.30f);
    /// <summary>完全遮挡时的暗场颜色和最大透明度。</summary>
    [SerializeField] private Color eclipseDarkTint = new(0.18f, 0.12f, 0.08f, 0.46f);
    /// <summary>日冕颜色；透明度会随覆盖率渐变。</summary>
    [SerializeField] private Color coronaTint = new(1f, 0.86f, 0.22f, 0.95f);
    /// <summary>达到该覆盖率时视觉层达到日全食状态，默认与本关 80% 免疫阈值一致。</summary>
    [SerializeField, Range(0.01f, 1f)] private float fullEclipseCoverage = 0.8f;
    /// <summary>开始显示日冕的覆盖率。</summary>
    [SerializeField, Range(0f, 1f)] private float coronaStartCoverage = 0.08f;
    /// <summary>日冕相对于太阳直径的尺寸倍率。</summary>
    [SerializeField, Min(1.05f)] private float coronaScale = 1.8f;
    /// <summary>日冕贴边亮光向外渐弱的纹理宽度比例。</summary>
    [SerializeField, Range(0.05f, 0.24f)] private float coronaOuterFeather = 0.2f;

    [Header("拖拽")]
    /// <summary>是否将按钮中心限制在 Screen.safeArea 与 Canvas 的交集内。</summary>
    [SerializeField] private bool clampToSafeArea = true;
    /// <summary>拖拽时覆盖层是否继续刷新日食位置。</summary>
    [SerializeField] private bool updateEclipseWhilePaused = true;

    #endregion

    #region Runtime

    private EventSubscriber subscriber;
    private Level117Ctrl boundController;
    private bool controllerBindingResolved;
    private Level117ShopButtonDragHandle dragHandle;
    private Vector2 initialAnchoredPosition;
    private Vector3 initialButtonLocalScale = Vector3.one;
    private bool initialPositionCaptured;
    private bool initialScaleCaptured;
    private SequentialScaleAnim panelShowAnim;
    private bool isPaused;
    private bool isResetting;
    private Level117Ctrl resettingController;
    private float currentCoverage;
    private float lastReportedCoverage = -1f;
    private float cachedSunRadiusWorld;
    private Transform cachedSunRadiusSource;
    private Coroutine clearBlockRoutine;
    private Texture2D generatedCoronaTexture;
    private readonly Vector3[] shopWorldCorners = new Vector3[4];

    /// <summary>当前帧太阳被商店按钮遮挡的面积比例，范围为 0 到 1。</summary>
    public float CurrentEclipseCoverage => currentCoverage;

    /// <summary>当前覆盖率是否达到本关日食免疫阈值。</summary>
    public bool IsEclipseImmune => currentCoverage >= Mathf.Clamp01(fullEclipseCoverage);

    /// <summary>
    /// 由当前 Level117 实例显式绑定 HUD，避免依赖 HUD Variant 名称或初始化先后顺序。
    /// </summary>
    internal void BindController(Level117Ctrl controller)
    {
        if (controller == null)
            return;

        boundController = controller;
        controllerBindingResolved = true;
        resettingController = null;
        isResetting = false;
        ApplyEclipseVisualSettings(
            controller.SunHotTint,
            controller.SunEclipseDarkTint,
            controller.SunCoronaTint,
            controller.SunCoronaScale,
            controller.SunCoronaOuterFeather);
        sunTransform = controller.SunTransform;
        worldCamera = null;
        cachedSunRadiusSource = null;
        cachedSunRadiusWorld = 0f;
        ResolveWorldCamera();
        EnsureDragHandle();
        EnsureOverlayObjects();
        RestoreVisuals();
    }

    /// <summary>
    /// 接收本关根节点的日食颜色与日冕形状配置。HUD 组件由运行时动态挂载，因此配置由 Level117Ctrl 显式传入。
    /// </summary>
    internal void ApplyEclipseVisualSettings(
        Color hotColor,
        Color darkColor,
        Color coronaColor,
        float coronaSizeScale,
        float coronaFeather)
    {
        float nextScale = Mathf.Max(1.05f, coronaSizeScale);
        float nextFeather = Mathf.Clamp(coronaFeather, 0.05f, 0.24f);
        bool coronaShapeChanged = !Mathf.Approximately(coronaScale, nextScale) ||
                                  !Mathf.Approximately(coronaOuterFeather, nextFeather);

        hotTint = hotColor;
        eclipseDarkTint = darkColor;
        coronaTint = coronaColor;
        coronaScale = nextScale;
        coronaOuterFeather = nextFeather;

        if (coronaShapeChanged)
            RefreshGeneratedCoronaTexture();

        if (hotTintOverlay != null || darkOverlay != null || coronaOverlay != null)
            ApplyCoverageVisuals(currentCoverage, Vector2.zero, 0f);
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        CaptureInitialPosition();
        EnsureDragHandle();
        EnsureOverlayObjects();
        BindEvents();
        ResetVisualAndDragState();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        CaptureInitialPosition();
        EnsureDragHandle();
        EnsureOverlayObjects();
        BindEvents();
        ResetVisualAndDragState();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        CaptureInitialPosition();

        if (isResetting && !TryCompleteResetRebind())
            return;

        if (!isResetting)
            isPaused = !IsPlayingState();

        if (dragHandle != null && dragHandle.HasActivePointerCycle && !CanBeginDrag())
            dragHandle.CancelPointerCycle(true);

        if (dragHandle != null && dragHandle.IsDragging)
            RestoreButtonScale();

        if (isPaused && !updateEclipseWhilePaused)
            return;

        UpdateEclipseVisuals();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        EventsUtils.ResetEvents(ref subscriber);
        dragHandle?.CancelPointerCycle(true);
        StopClearBlockRoutine();
        RestoreButtonPosition();
        RestoreVisuals();
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying)
            return;

        EventsUtils.ResetEvents(ref subscriber);
        dragHandle?.Detach();
        StopClearBlockRoutine();

        if (generatedCoronaTexture != null)
        {
            Destroy(generatedCoronaTexture);
            generatedCoronaTexture = null;
        }
    }

    #endregion

    #region Setup

    private void ResolveReferences()
    {
        TryResolveBoundController();

        if (visualRoot == null)
            visualRoot = transform.Find("Root") as RectTransform;
        visualRoot ??= transform as RectTransform;

        if (shopButton == null && visualRoot != null)
            shopButton = visualRoot.Find("shop_btn") as RectTransform;
        shopButton ??= transform.Find("Root/shop_btn") as RectTransform;

        if (shopCustomButton == null && shopButton != null)
            shopCustomButton = shopButton.GetComponent<CustomButton>();

        if (targetCanvas == null && shopButton != null)
            targetCanvas = shopButton.GetComponentInParent<Canvas>(true);
        if (targetCanvas == null)
            targetCanvas = GetComponent<Canvas>();
        targetCanvas ??= GetComponentInChildren<Canvas>(true);
        targetCanvas ??= GetComponentInParent<Canvas>(true);

        if (canvasScaler == null && targetCanvas != null)
            canvasScaler = targetCanvas.GetComponent<CanvasScaler>();
        canvasScaler ??= GetComponentInParent<CanvasScaler>(true);

        ResolveWorldCamera();

        if (sunTransform == null)
            sunTransform = boundController != null
                ? boundController.SunTransform
                : null;
        sunTransform ??= FindSunInLevel();
    }

    private Transform FindSunInLevel()
    {
        TryResolveBoundController();
        Level117Ctrl controller = boundController;
        if (controller != null && controller.SunTransform != null)
            return controller.SunTransform;

        // 一旦 HUD 已绑定过某局，即使该局对象进入 Unity 销毁态，也不能再回退绑定到新局。
        if (controllerBindingResolved)
            return null;

        Transform levelRoot = PlayCtrl.Instance != null && PlayCtrl.Instance.LevelCtrl != null
            ? PlayCtrl.Instance.LevelCtrl.transform
            : null;
        if (levelRoot != null)
        {
            Transform directSun = levelRoot.Find("Sun");
            if (directSun != null)
                return directSun;
        }

        if (levelRoot == null)
            return null;

        Transform[] children = levelRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && string.Equals(children[i].name, "Sun", StringComparison.OrdinalIgnoreCase))
                return children[i];
        }

        return null;
    }

    private void TryResolveBoundController()
    {
        if (isResetting || controllerBindingResolved)
            return;

        Level117Ctrl controller = Level117RuntimeBridge.ActiveController;
        if (controller == null)
            controller = FindFirstObjectByType<Level117Ctrl>();

        if (controller == null)
            return;

        boundController = controller;
        controllerBindingResolved = true;
    }

    private bool TryCompleteResetRebind()
    {
        Level117Ctrl activeController = Level117RuntimeBridge.ActiveController;
        if (activeController == null || ReferenceEquals(activeController, resettingController))
            return false;

        boundController = activeController;
        controllerBindingResolved = true;
        resettingController = null;
        ApplyEclipseVisualSettings(
            activeController.SunHotTint,
            activeController.SunEclipseDarkTint,
            activeController.SunCoronaTint,
            activeController.SunCoronaScale,
            activeController.SunCoronaOuterFeather);
        sunTransform = activeController.SunTransform;
        worldCamera = null;
        cachedSunRadiusSource = null;
        cachedSunRadiusWorld = 0f;
        ResolveWorldCamera();
        isResetting = false;
        isPaused = !IsPlayingState();
        RestoreButtonPosition();
        RestoreVisuals();
        return true;
    }

    private Camera ResolveWorldCamera()
    {
        if (worldCamera != null)
            return worldCamera;

        Transform levelRoot = PlayCtrl.Instance != null && PlayCtrl.Instance.LevelCtrl != null
            ? PlayCtrl.Instance.LevelCtrl.transform
            : null;
        Transform levelCamera = levelRoot != null ? levelRoot.Find("GameCamera") : null;
        if (levelCamera != null && levelCamera.TryGetComponent(out Camera camera))
            worldCamera = camera;

        worldCamera ??= Camera.main;
        return worldCamera;
    }

    private void CaptureInitialPosition()
    {
        if (shopButton == null)
            return;

        if (!initialPositionCaptured)
        {
            initialAnchoredPosition = shopButton.anchoredPosition;
            initialPositionCaptured = true;
        }

        if (initialScaleCaptured)
            return;

        panelShowAnim ??= GetComponent<SequentialScaleAnim>();
        if (panelShowAnim != null && panelShowAnim.IsPlaying)
            return;

        Vector3 candidateScale = shopButton.localScale;
        if (Mathf.Abs(candidateScale.x) <= 0.0001f ||
            Mathf.Abs(candidateScale.y) <= 0.0001f ||
            Mathf.Abs(candidateScale.z) <= 0.0001f)
        {
            return;
        }

        initialButtonLocalScale = candidateScale;
        initialScaleCaptured = true;
    }

    private void EnsureDragHandle()
    {
        if (shopButton == null)
            return;

        dragHandle = shopButton.GetComponent<Level117ShopButtonDragHandle>();
        if (dragHandle == null)
            dragHandle = shopButton.gameObject.AddComponent<Level117ShopButtonDragHandle>();

        dragHandle.Initialize(this, shopButton, shopCustomButton);
    }

    private void EnsureOverlayObjects()
    {
        if (visualRoot == null)
            return;

        hotTintOverlay ??= CreateFullScreenImage("Level117_HotTint");
        darkOverlay ??= CreateFullScreenImage("Level117_EclipseDark");
        coronaOverlay ??= CreateCoronaImage("Level117_Corona");

        ConfigureOverlay(hotTintOverlay);
        ConfigureOverlay(darkOverlay);
        if (coronaOverlay != null)
        {
            coronaOverlay.gameObject.layer = GetUiLayer();
            coronaOverlay.raycastTarget = false;
            coronaOverlay.texture = coronaTexture != null
                ? coronaTexture
                : (coronaOverlay.texture != null ? coronaOverlay.texture : GetCoronaTexture());
            if (coronaMaterial != null)
                coronaOverlay.material = coronaMaterial;
            coronaOverlay.color = new Color(coronaTint.r, coronaTint.g, coronaTint.b, 0f);
            coronaOverlay.gameObject.SetActive(false);
        }

        // 覆盖层放在 Root 的前三个兄弟节点，保证所有控制按钮仍然显示在其上方。
        if (hotTintOverlay != null)
            hotTintOverlay.transform.SetSiblingIndex(0);
        if (darkOverlay != null)
            darkOverlay.transform.SetSiblingIndex(1);
        if (coronaOverlay != null)
            coronaOverlay.transform.SetSiblingIndex(2);
    }

    private Image CreateFullScreenImage(string objectName)
    {
        GameObject go = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = GetUiLayer();
        go.transform.SetParent(visualRoot, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        Image image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    private RawImage CreateCoronaImage(string objectName)
    {
        GameObject go = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        go.layer = GetUiLayer();
        go.transform.SetParent(visualRoot, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(256f, 256f);
        rect.anchoredPosition = Vector2.zero;
        RawImage rawImage = go.GetComponent<RawImage>();
        rawImage.raycastTarget = false;
        return rawImage;
    }

    private void ConfigureOverlay(Image image)
    {
        if (image == null)
            return;

        image.gameObject.layer = GetUiLayer();
        image.raycastTarget = false;
        image.maskable = false;
    }

    private int GetUiLayer()
    {
        if (targetCanvas != null)
            return targetCanvas.gameObject.layer;
        if (visualRoot != null)
            return visualRoot.gameObject.layer;
        return gameObject.layer;
    }

    private Texture2D GetCoronaTexture()
    {
        if (generatedCoronaTexture != null)
            return generatedCoronaTexture;

        const int size = 128;
        generatedCoronaTexture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            name = "Level117_RuntimeCorona",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[size * size];
        Vector2 center = new((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f / Mathf.Max(1.05f, coronaScale);
        float innerWidth = size * 0.045f;
        float outerWidth = size * Mathf.Clamp(coronaOuterFeather, 0.05f, 0.24f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha;
                if (distance < radius)
                {
                    float innerT = Mathf.InverseLerp(radius - innerWidth, radius, distance);
                    alpha = Mathf.SmoothStep(0f, 1f, innerT);
                }
                else
                {
                    float outerT = Mathf.InverseLerp(radius, radius + outerWidth, distance);
                    alpha = 1f - Mathf.SmoothStep(0f, 1f, outerT);
                }

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        generatedCoronaTexture.SetPixels(pixels);
        generatedCoronaTexture.Apply(false, false);
        return generatedCoronaTexture;
    }

    private void RefreshGeneratedCoronaTexture()
    {
        if (generatedCoronaTexture != null)
        {
            Destroy(generatedCoronaTexture);
            generatedCoronaTexture = null;
        }

        if (coronaOverlay != null && coronaTexture == null)
            coronaOverlay.texture = GetCoronaTexture();
    }

    #endregion

    #region Events And State

    private void BindEvents()
    {
        EventsUtils.ResetEvents(ref subscriber);
        subscriber.Subscribe<GamePauseEvent>(OnGamePause);
        subscriber.Subscribe<GameResumeEvent>(OnGameResume);
        subscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
        subscriber.Subscribe<GameResetEvent>(OnGameReset);
    }

    private void OnGamePause(GamePauseEvent _)
    {
        isPaused = true;
        dragHandle?.CancelPointerCycle(true);
    }

    private void OnGameResume(GameResumeEvent _)
    {
        isPaused = !IsPlayingState();
    }

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        isPaused = evt == null || evt.NewState != GamePlayStateName.PLAYING;
        if (isPaused)
            dragHandle?.CancelPointerCycle(true);
    }

    private void OnGameReset(GameResetEvent _)
    {
        isResetting = true;
        isPaused = true;
        dragHandle?.CancelPointerCycle(true);
        RestoreButtonPosition();
        RestoreVisuals();

        resettingController = boundController != null
            ? boundController
            : Level117RuntimeBridge.ActiveController;
        boundController = null;
        controllerBindingResolved = false;
        sunTransform = null;
        worldCamera = null;
        cachedSunRadiusSource = null;
        cachedSunRadiusWorld = 0f;
    }

    private bool IsPlayingState()
    {
        PlayCtrl playCtrl = PlayCtrl.Instance;
        if (playCtrl == null)
            return !isPaused;

        try
        {
            return playCtrl.CheckState(GamePlayStateName.PLAYING);
        }
        catch
        {
            return !isPaused;
        }
    }

    internal bool CanBeginDrag()
    {
        if (!Application.isPlaying || isResetting || !IsPlayingState())
            return false;

        CaptureInitialPosition();
        if (!initialPositionCaptured || !initialScaleCaptured)
            return false;

        PlayCtrl playCtrl = PlayCtrl.Instance;
        if (playCtrl != null && playCtrl.LevelCtrl != null)
        {
            if (playCtrl.LevelCtrl.IsSuccess() || playCtrl.LevelCtrl.IsFail())
                return false;

            if (playCtrl.LevelCtrl.Actor != null && playCtrl.LevelCtrl.Actor.IsDied)
                return false;
        }

        return enabled && gameObject.activeInHierarchy && shopButton != null;
    }

    #endregion

    #region Drag API

    internal Camera EventCamera
    {
        get
        {
            return targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? targetCanvas.worldCamera != null ? targetCanvas.worldCamera : Camera.main
                : null;
        }
    }

    internal RectTransform DragParent => shopButton != null && shopButton.parent is RectTransform parent
        ? parent
        : visualRoot;

    internal Vector2 GetPointerLocalPosition(Vector2 screenPosition, Camera pointerEventCamera = null)
    {
        RectTransform parent = DragParent;
        Camera conversionCamera = pointerEventCamera != null ? pointerEventCamera : EventCamera;
        if (parent == null ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, conversionCamera, out Vector2 local))
            return shopButton != null ? shopButton.anchoredPosition : Vector2.zero;
        return local;
    }

    internal Vector2 ClampButtonPosition(Vector2 candidate)
    {
        if (!clampToSafeArea || shopButton == null)
            return candidate;

        RectTransform parent = DragParent;
        if (parent == null)
            return candidate;

        Vector2 halfExtents = GetButtonHalfExtents(parent);
        Rect allowed = GetAllowedParentRect(parent);
        Vector2 anchorReference = new(
            shopButton.localPosition.x - shopButton.anchoredPosition.x,
            shopButton.localPosition.y - shopButton.anchoredPosition.y);
        Vector2 localCenter = anchorReference + candidate;
        float minX = allowed.xMin + halfExtents.x;
        float maxX = allowed.xMax - halfExtents.x;
        float minY = allowed.yMin + halfExtents.y;
        float maxY = allowed.yMax - halfExtents.y;

        // 太阳可能位于横屏刘海安全区之外。只为本关谜题目标扩展按钮中心范围，
        // 否则部分设备上即使拖到最边缘也无法达到 80% 遮挡。
        ExpandCenterBoundsToEclipseTarget(parent, ref minX, ref maxX, ref minY, ref maxY);

        if (minX > maxX)
            localCenter.x = (allowed.xMin + allowed.xMax) * 0.5f;
        else
            localCenter.x = Mathf.Clamp(localCenter.x, minX, maxX);

        if (minY > maxY)
            localCenter.y = (allowed.yMin + allowed.yMax) * 0.5f;
        else
            localCenter.y = Mathf.Clamp(localCenter.y, minY, maxY);

        return localCenter - anchorReference;
    }

    private Vector2 GetButtonHalfExtents(RectTransform parent)
    {
        shopButton.GetWorldCorners(shopWorldCorners);
        Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < shopWorldCorners.Length; i++)
        {
            Vector2 local = parent.InverseTransformPoint(shopWorldCorners[i]);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }

        return CompensateForButtonPressScale((max - min) * 0.5f);
    }

    private Vector2 CompensateForButtonPressScale(Vector2 currentSize)
    {
        if (!initialScaleCaptured || shopButton == null)
            return currentSize;

        Vector3 currentScale = shopButton.localScale;
        float scaleX = Mathf.Abs(currentScale.x) > 0.0001f
            ? Mathf.Abs(initialButtonLocalScale.x / currentScale.x)
            : 1f;
        float scaleY = Mathf.Abs(currentScale.y) > 0.0001f
            ? Mathf.Abs(initialButtonLocalScale.y / currentScale.y)
            : 1f;
        return new Vector2(currentSize.x * scaleX, currentSize.y * scaleY);
    }

    private void ExpandCenterBoundsToEclipseTarget(
        RectTransform parent,
        ref float minX,
        ref float maxX,
        ref float minY,
        ref float maxY)
    {
        if (sunTransform == null)
        {
            Level117Ctrl controller = boundController;
            sunTransform = controller != null ? controller.SunTransform : FindSunInLevel();
        }

        Camera camera = ResolveWorldCamera();
        if (sunTransform == null || camera == null)
            return;

        Vector3 sunScreen = camera.WorldToScreenPoint(sunTransform.position);
        if (sunScreen.z <= 0f ||
            !TryScreenToParentLocal(new Vector2(sunScreen.x, sunScreen.y), parent, out Vector2 sunLocal))
        {
            return;
        }

        Rect visibleRect = GetVisibleParentRect(parent);
        sunLocal.x = Mathf.Clamp(sunLocal.x, visibleRect.xMin, visibleRect.xMax);
        sunLocal.y = Mathf.Clamp(sunLocal.y, visibleRect.yMin, visibleRect.yMax);
        minX = Mathf.Min(minX, sunLocal.x);
        maxX = Mathf.Max(maxX, sunLocal.x);
        minY = Mathf.Min(minY, sunLocal.y);
        maxY = Mathf.Max(maxY, sunLocal.y);
    }

    private Rect GetVisibleParentRect(RectTransform parent)
    {
        Rect parentRect = parent.rect;
        if (!TryScreenToParentLocal(Vector2.zero, parent, out Vector2 screenMin) ||
            !TryScreenToParentLocal(new Vector2(Screen.width, Screen.height), parent, out Vector2 screenMax))
        {
            return parentRect;
        }

        float minX = Mathf.Max(parentRect.xMin, Mathf.Min(screenMin.x, screenMax.x));
        float maxX = Mathf.Min(parentRect.xMax, Mathf.Max(screenMin.x, screenMax.x));
        float minY = Mathf.Max(parentRect.yMin, Mathf.Min(screenMin.y, screenMax.y));
        float maxY = Mathf.Min(parentRect.yMax, Mathf.Max(screenMin.y, screenMax.y));
        return minX <= maxX && minY <= maxY
            ? Rect.MinMaxRect(minX, minY, maxX, maxY)
            : parentRect;
    }

    private Rect GetAllowedParentRect(RectTransform parent)
    {
        Rect parentRect = parent.rect;
        if (!TryScreenToParentLocal(Screen.safeArea.min, parent, out Vector2 safeMin) ||
            !TryScreenToParentLocal(Screen.safeArea.max, parent, out Vector2 safeMax))
        {
            return parentRect;
        }

        float minX = Mathf.Max(parentRect.xMin, Mathf.Min(safeMin.x, safeMax.x));
        float maxX = Mathf.Min(parentRect.xMax, Mathf.Max(safeMin.x, safeMax.x));
        float minY = Mathf.Max(parentRect.yMin, Mathf.Min(safeMin.y, safeMax.y));
        float maxY = Mathf.Min(parentRect.yMax, Mathf.Max(safeMin.y, safeMax.y));

        if (minX > maxX || minY > maxY)
            return parentRect;

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }

    private bool TryScreenToParentLocal(Vector2 screenPosition, RectTransform parent, out Vector2 local)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, EventCamera, out local);
    }

    internal void OnDragStarted()
    {
        StopClearBlockRoutine();
        RestoreButtonScale();
        if (shopCustomButton != null)
            shopCustomButton.IsBlockClick = true;
    }

    internal void OnDragEnded()
    {
        RestoreButtonScale();
        ScheduleClearClickBlock();
    }

    internal void OnSuppressedPointerReleased()
    {
        if (shopCustomButton == null)
            return;

        shopCustomButton.IsBlockClick = true;
        ScheduleClearClickBlock();
    }

    internal void SuppressReleasedPointerClick()
    {
        if (shopCustomButton == null)
            return;

        RestoreButtonScale();
        shopCustomButton.IsBlockClick = true;
        ScheduleClearClickBlock();
    }

    private void ScheduleClearClickBlock()
    {
        StopClearBlockRoutine();
        clearBlockRoutine = StartCoroutine(ClearClickBlockNextFrame());
    }

    private IEnumerator ClearClickBlockNextFrame()
    {
        yield return null;
        clearBlockRoutine = null;
        if (dragHandle == null || !dragHandle.IsDragging)
        {
            if (shopCustomButton != null)
                shopCustomButton.IsBlockClick = false;
        }
    }

    private void StopClearBlockRoutine()
    {
        if (clearBlockRoutine == null)
            return;

        StopCoroutine(clearBlockRoutine);
        clearBlockRoutine = null;
    }

    private void RestoreButtonPosition()
    {
        CaptureInitialPosition();
        if (shopButton != null && initialPositionCaptured)
        {
            shopButton.anchoredPosition = initialAnchoredPosition;
            RestoreButtonScale();
        }
        if (shopCustomButton != null)
            shopCustomButton.IsBlockClick = clearBlockRoutine != null;
    }

    private void RestoreButtonScale()
    {
        if (shopButton != null && initialScaleCaptured)
            shopButton.localScale = initialButtonLocalScale;
    }

    private void ResetVisualAndDragState()
    {
        if (!isResetting)
            isPaused = !IsPlayingState();
        RestoreButtonPosition();
        dragHandle?.CancelPointerCycle(false);
        RestoreVisuals();
    }

    #endregion

    #region Eclipse Projection And Visuals

    private void UpdateEclipseVisuals()
    {
        if (sunTransform == null)
        {
            Level117Ctrl controller = boundController;
            sunTransform = controller != null ? controller.SunTransform : null;
            if (sunTransform == null)
                sunTransform = FindSunInLevel();
        }

        ResolveWorldCamera();

        if (shopButton == null || visualRoot == null || sunTransform == null)
        {
            ClearCurrentEclipse();
            return;
        }

        Camera camera = worldCamera != null ? worldCamera : Camera.main;
        if (camera == null)
        {
            ClearCurrentEclipse();
            return;
        }

        Vector3 sunScreen3 = camera.WorldToScreenPoint(sunTransform.position);
        if (sunScreen3.z <= 0f)
        {
            ClearCurrentEclipse();
            return;
        }

        Vector2 sunScreen = new(sunScreen3.x, sunScreen3.y);
        float sunRadius = GetSunScreenRadius(camera, sunScreen3);
        Vector2 shopScreen = GetShopScreenCenter();
        float shopRadius = GetShopScreenRadius();
        float coverage = CalculateCircleCoverage(sunScreen, sunRadius, shopScreen, shopRadius);

        currentCoverage = coverage;
        ReportCoverage(coverage);
        ApplyCoverageVisuals(coverage, sunScreen, sunRadius);
    }

    private void ClearCurrentEclipse()
    {
        currentCoverage = 0f;
        ReportCoverage(0f);
        ApplyCoverageVisuals(0f, Vector2.zero, 0f);
    }

    private Vector2 GetShopScreenCenter()
    {
        shopButton.GetWorldCorners(shopWorldCorners);
        Vector3 center = (shopWorldCorners[0] + shopWorldCorners[2]) * 0.5f;
        return RectTransformUtility.WorldToScreenPoint(EventCamera, center);
    }

    private float GetShopScreenRadius()
    {
        shopButton.GetWorldCorners(shopWorldCorners);
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(EventCamera, shopWorldCorners[0]);
        Vector2 topLeft = RectTransformUtility.WorldToScreenPoint(EventCamera, shopWorldCorners[1]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(EventCamera, shopWorldCorners[2]);
        float width = Vector2.Distance(topLeft, topRight);
        float height = Vector2.Distance(bottomLeft, topLeft);
        Vector2 baseSize = CompensateForButtonPressScale(new Vector2(width, height));
        return Mathf.Max(1f, Mathf.Min(baseSize.x, baseSize.y) * 0.5f);
    }

    private float GetSunScreenRadius(Camera camera, Vector3 centerScreen)
    {
        float worldRadius = sunRadiusWorld;
        if (worldRadius <= 0f)
        {
            if (cachedSunRadiusSource != sunTransform || cachedSunRadiusWorld <= 0f)
                CacheSunRadiusWorld();

            worldRadius = cachedSunRadiusWorld;
        }

        if (worldRadius <= 0f)
            return Mathf.Max(1f, fallbackSunScreenRadius);

        Vector3 xPoint = camera.WorldToScreenPoint(sunTransform.position + Vector3.right * worldRadius);
        Vector3 yPoint = camera.WorldToScreenPoint(sunTransform.position + Vector3.up * worldRadius);
        float radiusX = Vector2.Distance(new Vector2(centerScreen.x, centerScreen.y), new Vector2(xPoint.x, xPoint.y));
        float radiusY = Vector2.Distance(new Vector2(centerScreen.x, centerScreen.y), new Vector2(yPoint.x, yPoint.y));
        return Mathf.Max(1f, Mathf.Max(radiusX, radiusY));
    }

    private void CacheSunRadiusWorld()
    {
        cachedSunRadiusSource = sunTransform;
        cachedSunRadiusWorld = 0f;
        if (sunTransform == null)
            return;

        Renderer[] renderers = sunTransform.GetComponentsInChildren<Renderer>(true);
        float smallestRadius = float.PositiveInfinity;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            float radius = Mathf.Max(renderer.bounds.extents.x, renderer.bounds.extents.y);
            if (radius > 0.001f)
                smallestRadius = Mathf.Min(smallestRadius, radius);
        }

        if (!float.IsPositiveInfinity(smallestRadius))
            cachedSunRadiusWorld = smallestRadius;
    }

    private static float CalculateCircleCoverage(Vector2 sunCenter, float sunRadius, Vector2 blockerCenter, float blockerRadius)
    {
        sunRadius = Mathf.Max(0.001f, sunRadius);
        blockerRadius = Mathf.Max(0.001f, blockerRadius);
        float distance = Vector2.Distance(sunCenter, blockerCenter);

        if (distance >= sunRadius + blockerRadius)
            return 0f;
        if (distance <= Mathf.Abs(sunRadius - blockerRadius))
            return blockerRadius >= sunRadius ? 1f : Mathf.Clamp01((blockerRadius * blockerRadius) / (sunRadius * sunRadius));

        float sunTerm = Mathf.Clamp(
            (distance * distance + sunRadius * sunRadius - blockerRadius * blockerRadius) /
            (2f * distance * sunRadius), -1f, 1f);
        float blockerTerm = Mathf.Clamp(
            (distance * distance + blockerRadius * blockerRadius - sunRadius * sunRadius) /
            (2f * distance * blockerRadius), -1f, 1f);
        float root = Mathf.Sqrt(Mathf.Max(0f,
            (-distance + sunRadius + blockerRadius) *
            (distance + sunRadius - blockerRadius) *
            (distance - sunRadius + blockerRadius) *
            (distance + sunRadius + blockerRadius)));

        float intersection = sunRadius * sunRadius * Mathf.Acos(sunTerm) +
                             blockerRadius * blockerRadius * Mathf.Acos(blockerTerm) -
                             0.5f * root;
        return Mathf.Clamp01(intersection / (Mathf.PI * sunRadius * sunRadius));
    }

    private void ApplyCoverageVisuals(float coverage, Vector2 sunScreen, float sunRadius)
    {
        float full = Mathf.Clamp(fullEclipseCoverage, 0.01f, 1f);
        float eclipseT = Mathf.Clamp01(coverage / full);
        eclipseT = Mathf.SmoothStep(0f, 1f, eclipseT);

        if (hotTintOverlay != null)
        {
            Color color = Color.Lerp(hotTint, eclipseDarkTint, eclipseT);
            color.a = Mathf.Lerp(hotTint.a, eclipseDarkTint.a, eclipseT);
            hotTintOverlay.color = color;
            hotTintOverlay.gameObject.SetActive(color.a > 0.001f);
        }

        if (darkOverlay != null)
        {
            darkOverlay.color = new Color(eclipseDarkTint.r, eclipseDarkTint.g, eclipseDarkTint.b, 0f);
            darkOverlay.gameObject.SetActive(false);
        }

        if (coronaOverlay != null)
        {
            float coronaT = Mathf.InverseLerp(coronaStartCoverage, full, coverage);
            coronaT = Mathf.SmoothStep(0f, 1f, coronaT);
            Color color = coronaTint;
            color.a = coronaTint.a * coronaT;
            coronaOverlay.color = color;
            coronaOverlay.gameObject.SetActive(coronaT > 0.001f && sunRadius > 0f);
            if (coronaT > 0.001f && visualRoot != null)
            {
                PositionCorona(sunScreen, sunRadius);
            }
        }
    }

    private void PositionCorona(Vector2 sunScreen, float sunRadius)
    {
        if (coronaOverlay == null || visualRoot == null)
            return;

        RectTransform rect = coronaOverlay.rectTransform;
        Camera eventCamera = EventCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(visualRoot, sunScreen, eventCamera, out Vector2 local))
            rect.anchoredPosition = local;

        float diameter = sunRadius * 2f * Mathf.Max(0.1f, coronaScale);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(visualRoot, sunScreen + Vector2.right * diameter, eventCamera, out Vector2 edge))
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(visualRoot, sunScreen, eventCamera, out Vector2 center))
                diameter = Mathf.Abs(edge.x - center.x);
        }

        rect.sizeDelta = new Vector2(Mathf.Max(1f, diameter), Mathf.Max(1f, diameter));
    }

    private void ReportCoverage(float coverage)
    {
        if (Mathf.Abs(coverage - lastReportedCoverage) < 0.0005f)
            return;

        TryResolveBoundController();

        if (!controllerBindingResolved || boundController == null ||
            !ReferenceEquals(Level117RuntimeBridge.ActiveController, boundController))
        {
            return;
        }

        lastReportedCoverage = coverage;
        boundController.SetEclipseCoverage(coverage);
    }

    private void RestoreVisuals()
    {
        currentCoverage = 0f;
        lastReportedCoverage = -1f;
        ApplyCoverageVisuals(0f, Vector2.zero, 0f);
        ReportCoverage(0f);
    }

    #endregion
}
