using System;
using System.Collections.Generic;
using Bear.EventSystem;
using Bear.Logger;
using Bear.ResourceSystem;
using Bear.SaveModule;
using Game.Events;
using Game.Scripts.Common;
using UnityEngine;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using Sirenix.OdinInspector;
using UnityEditor;
#endif

/// <summary>
/// Draw 玩法的独立画线功能，负责输入采样、线条生成、碰撞体和长度统计。
/// 调用边界：由当前关卡的 DrawLevelCtrl 持有并驱动，不负责关卡胜负或演出状态。
/// 注意事项：线长统一使用 DrawSpace 局部坐标中的折线几何长度。
/// </summary>
public sealed class DrawLineFeature : MonoBehaviour, IDebuger, IEventSender
{
    #region Constants

    private const int NoFinger = int.MinValue;
    private const float MinimumStrokeLength = 0.001f;
    private const float MinimumRigidbodyMass = 0.0001f;
    private const int MaxCompositeColliderSegments = 32;
    private const int CompletedStrokeCornerVertices = 2;
    private const int CompletedStrokeCapVertices = 6;
    private const float CompletedStrokeSmoothingRatio = 0.25f;
    private const string DrawAreaLayerName = "Draw";
    private const string DefaultBrushStyleId = "301001";
    private const string PencilSkinConfigResourceFolder = "PencilSkinConfig";
    private const string PencilSkinConfigAssetPrefix = "PencilSkin_";

    #endregion

    #region Inspector References

    [Header("画线空间")]
    [SerializeField] private Camera inputCamera;
    [SerializeField] private Transform drawSpace;
    /// <summary>
    /// 是否限制玩家只能在 drawArea 内绘制。
    /// 调用边界：仅影响落笔与连续采样的区域判断；默认关闭时允许在投影平面任意非阻挡位置绘制。
    /// 注意事项：地板、门等非 Draw Layer Collider2D 的阻挡始终生效，不受此开关影响。
    /// </summary>
#if UNITY_EDITOR
    [LabelText("是否开启区域限制")]
#endif
    [Tooltip("开启后只能在 Draw Area 范围内画线；关闭后可在任意非阻挡位置画线。")]
    [SerializeField] private bool enableDrawAreaRestriction = false;
    [Tooltip("开启区域限制时，必须绑定位于 Draw Layer 的 Collider2D；其它 Layer 上的 Collider2D 始终阻挡画线。")]
    [SerializeField] private Collider2D drawArea;
    [SerializeField] private LineRenderer lineTemplate;
    /// <summary>
    /// 当前关卡需要同步显隐的全部提示对象。
    /// 调用边界：ShowHint 会统一控制本列表；Reset 会分别恢复每个对象自己的初始显隐。
    /// 注意事项：既有关卡原 TipsObject 必须迁入本列表；允许留空，空引用和重复引用会被安全处理。
    /// </summary>
    [Tooltip("当前关卡需要同步显示/隐藏的全部 TipsObject；Reset 会恢复各自初始状态。")]
    [SerializeField] private List<GameObject> tipsObjects = new();
    [Tooltip("玩家运行时线条的整体宽度；优先于 DrawBrushStyleAsset 的 LineWidth。")]
    [SerializeField, Min(0.001f)] private float drawingLineWidth = 0.15f;

    /// <summary>
    /// 是否使用 Inspector 配置的 PencilSkinConfig ID。
    /// 调用边界：仅由运行时笔刷同步流程读取；默认 false 时使用 DB.DrawGame.CurrentSkinId。
    /// 注意事项：开启后忽略 DB 当前选择，关闭后忽略 Inspector 的 brushStyleId。
    /// </summary>
    [Header("笔刷皮肤配置来源")]
    [Tooltip("默认关闭并使用 DB 当前笔刷 ID；开启后改用 Inspector 配置的 ID。")]
    [SerializeField] private bool useInspectorPencilSkinConfigId = false;

    /// <summary>
    /// Inspector 测试模式使用的 PencilSkinConfig ID，例如 301001。
    /// 调用边界：仅在 useInspectorPencilSkinConfigId=true 时读取。
    /// 注意事项：字段留空或配置无效时回退到默认笔刷 301001，不会改写 DB。
    /// </summary>
    [Tooltip("仅在开启 Inspector ID 时生效；留空时使用默认笔刷 301001。")]
    [SerializeField] private string brushStyleId = DefaultBrushStyleId;

    [Header("画线配置")]
    /// <summary>
    /// 是否使用 DrawLevelData 覆盖 Inspector 墨水配置。
    /// 调用边界：仅在运行时接收 ConfigureInk 时判断；默认 false。
    /// 注意事项：关闭后 maxInkLength 和 stage 均以当前 DLevel prefab 为准。
    /// </summary>
    [Tooltip("是否使用 DrawLevelData 覆盖墨水配置；关闭时使用当前 Prefab Inspector 的 maxInkLength 和 stage。")]
    [SerializeField] private bool isConfig = false;
    [SerializeField, Min(0.001f)] private float sampleSpacing = 0.05f;
    [SerializeField, Min(0.01f)] private float maxInkLength = 10f;
    [SerializeField] private bool allowMultipleStrokes;
    [SerializeField] private bool buildCollider = true;
    [SerializeField, Min(0f)] private float colliderRadius = 0.05f;

    /// <summary>
    /// 是否把当前笔画构建为可与其它笔画碰撞的有厚度复合碰撞体。
    /// 调用边界：仅在 buildCollider=true 的有效笔画完成时生效；默认关闭以保护既有关卡。
    /// 注意事项：同一笔内部胶囊会合并为一个刚体形状，不产生自碰撞；不同笔之间可正常碰撞。
    /// </summary>
    [Tooltip("开启后使用简化胶囊链与 CompositeCollider2D，使不同笔画之间能够发生物理碰撞。")]
    [SerializeField] private bool enableStrokeToStrokeCollision;

    /// <summary>
    /// 当前关卡满基础墨水长度线条的 Rigidbody2D 质量。
    /// 调用边界：每一笔有效线条完成时写入运行时刚体；关闭长度缩放时直接使用该值。
    /// 注意事项：这是运行时质量的唯一配置来源，画线模板上的 mass 会被覆盖。
    /// </summary>
    [Header("画线物理")]
    [Tooltip("满基础墨水长度的线条质量；关闭长度缩放时所有线条直接使用该值。")]
    [SerializeField, Min(MinimumRigidbodyMass)] private float strokeMass = 20f;

    /// <summary>
    /// 是否根据单笔完成线长缩放运行时线条质量，默认开启。
    /// 调用边界：仅在有效笔画完成、二维物理模拟启用前生效。
    /// 注意事项：只修改 Rigidbody2D.mass，不修改 gravityScale；满基础墨水长度使用 strokeMass。
    /// </summary>
    [Tooltip("默认开启；短线降低质量，达到基础最大墨水长度时使用配置的 Mass。")]
    [SerializeField] private bool scaleMassByLength = true;

    /// <summary>
    /// 极短有效线相对 strokeMass 的最低比例。
    /// 调用边界：仅在 scaleMassByLength 开启时参与单笔质量计算。
    /// 注意事项：限制在 0.01～1，避免极短线产生接近零质量的刚体。
    /// </summary>
    [Tooltip("极短有效线相对配置 Mass 的最低比例。")]
    [SerializeField, Range(0.01f, 1f)] private float minimumMassRatio = 0.25f;

    /// <summary>
    /// 归一化单笔长度到质量比例的增长曲线。
    /// 调用边界：横轴为 StrokeLength/maxInkLength，纵轴会限制在 0～1。
    /// 注意事项：满基础墨水长度固定使用 strokeMass，不受曲线末端值影响。
    /// </summary>
    [Tooltip("横轴为单笔长度占基础最大墨水长度的比例，纵轴控制质量增长速度。")]
    [SerializeField] private AnimationCurve massByLengthCurve =
        AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("墨水评级阶段")]
    /// <summary>
    /// Inspector 配置的 1/2/3 星剩余墨水阶段。
    /// 调用边界：x/y/z 依次对应一星、二星、三星；isConfig=true 时可被 DrawLevelData 覆盖。
    /// 注意事项：单位是 DrawSpace 剩余墨水绝对长度，不是 0～1 比例；初始化时会限制在
    /// 0 到 maxInkLength 之间并保持单调不降。
    /// </summary>
    [Tooltip("x/y/z 分别表示获得 1/2/3 星所需的最小剩余墨水绝对长度，不是 0～1 比例。")]
    [FormerlySerializedAs("remainingInkStarThresholds")]
    [SerializeField] private Vector3 stage = new(0f, 2f, 5f);

    #endregion

    #region Runtime State

    private readonly struct HintInitialState
    {
        public GameObject HintObject { get; }
        public bool WasActive { get; }

        public HintInitialState(GameObject hintObject, bool wasActive)
        {
            HintObject = hintObject;
            WasActive = wasActive;
        }
    }

    private readonly List<GameObject> _completedStrokeObjects = new(4);
    private readonly List<HintInitialState> _initialHintStates = new(2);
    private List<Vector2> _activePoints;
    private Transform _runtimeLinesRoot;
    private GameObject _activeStrokeObject;
    private LineRenderer _activeLineRenderer;
    private Material _drawingLineMaterial;
    private Gradient _drawingLineGradient = CreateSolidGradient(Color.black);
    private Material _runtimeFallbackMaterial;
    private string _observedBrushStyleId;
    private int _activeFingerId = NoFinger;
    private bool _usingEditorMouse;
    private bool _isRuntimeInitialized;
    private bool _isInputEnabled;
    private bool _isPaused;
    private int _blockingLayerMask;
    private ContactFilter2D _blockingContactFilter;
    private float _usedInk;
    private float _bonusInk;
    private float _activeStrokeLength;

    #endregion

    #region Public Runtime API

    /// <summary>
    /// 一笔有效画线完成并生成碰撞体后触发。
    /// 调用边界：仅供持有本组件的 DrawLevelCtrl 转发当前关卡画线结果。
    /// </summary>
    public event Action<DrawStrokeResult> StrokeCompleted;

    /// <summary>
    /// 玩家在有效绘制区域内成功开始一笔画线时触发。
    /// 调用边界：仅供同一 DLevel 内需要响应落笔时机的局部表现订阅；不代表该笔最终有效完成。
    /// 注意事项：按压从非绘制区域开始时不会立即触发，按住进入有效区域后才触发；
    /// 从 UI 开始按压或输入关闭时不会进入本次画线检测。
    /// </summary>
    public event Action StrokeStarted;

    /// <summary>必要引用已经解析且允许进入运行时流程。</summary>
    public bool IsRuntimeReady => _isRuntimeInitialized;

    /// <summary>当前是否允许接收画线输入。</summary>
    public bool IsInputEnabled => _isInputEnabled;

    /// <summary>当前是否处于暂停状态。</summary>
    public bool IsPaused => _isPaused;

    /// <summary>当前关卡是否允许完成第一笔后继续画线。</summary>
    public bool AllowsMultipleStrokes => allowMultipleStrokes;

    /// <summary>当前尝试已完成的有效笔数。</summary>
    public int CompletedStrokeCount => _completedStrokeObjects.Count;

    /// <summary>
    /// 按从 1 开始的笔序取得当前尝试中已经完成的运行时线段对象。
    /// 调用边界：仅供同一 DLevel 内的局部机关在 StrokeCompleted 同步回调后读取；
    /// 返回对象仍由 DrawLineFeature 持有，并会在 Reset 或销毁时统一清理。
    /// </summary>
    public bool TryGetCompletedStrokeObject(int strokeIndex, out GameObject strokeObject)
    {
        int objectIndex = strokeIndex - 1;
        if (objectIndex < 0 || objectIndex >= _completedStrokeObjects.Count)
        {
            strokeObject = null;
            return false;
        }

        strokeObject = _completedStrokeObjects[objectIndex];
        return strokeObject != null;
    }

    /// <summary>当前尝试已使用的 DrawSpace 折线总长度。</summary>
    public float DrawnLength => _usedInk;

    /// <summary>当前墨水和星级状态快照。</summary>
    public DrawInkState InkState => CreateInkState();

    /// <summary>
    /// 根据 isConfig 决定是否使用当前 DrawLevelData 覆盖基础墨水长度和评级阶段。
    /// 调用边界：由 DrawPlayCtrl 在 InitializeRuntime 前调用；所有输入均使用 DrawSpace 长度单位。
    /// 注意事项：isConfig=false 时保留当前 Prefab Inspector 配置；不会写回 prefab、配置或存档。
    /// </summary>
    public void ConfigureInk(
        float configuredMaxInkLength,
        float oneStarRemainingInk,
        float twoStarRemainingInk,
        float threeStarRemainingInk)
    {
        if (!isConfig)
            return;

        maxInkLength = Mathf.Max(0f, configuredMaxInkLength);
        stage = new Vector3(
            oneStarRemainingInk,
            twoStarRemainingInk,
            threeStarRemainingInk);
        NormalizeStarThresholds();
    }

    /// <summary>
    /// 解析画线引用并建立一次新的画线尝试。
    /// 调用边界：仅由持有组件的 DrawLevelCtrl 在关卡 prefab 实例化后调用。
    /// </summary>
    public void InitializeRuntime()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        EnsureRuntimeRoot();

        if (drawSpace == null || inputCamera == null)
        {
            _isRuntimeInitialized = false;
            this.LogError("[DrawLineFeature] drawSpace 或 inputCamera 缺失，画线输入保持关闭。");
            return;
        }

        if (!ValidateDrawAreaLayer())
        {
            _isRuntimeInitialized = false;
            return;
        }

        _observedBrushStyleId = null;
        SyncBrushStyleFromCurrentSelection();

        if (lineTemplate == null)
            this.LogWarning("[DrawLineFeature] lineTemplate 未绑定，将使用运行时 LineRenderer。");

        NormalizeConfiguration();
        int pointCapacity = Mathf.Max(4, Mathf.CeilToInt(maxInkLength / sampleSpacing) + 2);
        _activePoints = new List<Vector2>(pointCapacity);
        CacheInitialHintStates();
        _isRuntimeInitialized = true;

        Vector3 drawScale = drawSpace.lossyScale;
        if (!Mathf.Approximately(Mathf.Abs(drawScale.x), Mathf.Abs(drawScale.y)))
        {
            this.LogWarning(
                "[DrawLineFeature] DrawSpace 使用非等比缩放，局部线长与世界视觉长度可能不一致。");
        }

        ResetAttempt();
    }

    /// <summary>
    /// 处理当前活动关卡的鼠标或触摸画线输入。
    /// 调用边界：仅由 DrawLevelCtrl.Tick 转发，不使用独立 Update。
    /// </summary>
    public void Tick()
    {
        if (!_isRuntimeInitialized || !_isInputEnabled || _isPaused)
            return;

#if UNITY_EDITOR || UNITY_STANDALONE
        if (_activeFingerId != NoFinger)
        {
            if (_usingEditorMouse)
                UpdateMouseInput();
            else
                UpdateTouchInput();

            return;
        }

        if (Input.touchCount == 0)
        {
            UpdateMouseInput();
            return;
        }
#endif

        UpdateTouchInput();
    }

    /// <summary>
    /// 开启或关闭画线输入；关闭时回退当前未完成的一笔。
    /// 注意事项：开启输入时会派发一次当前长度快照，供新建 HUD 完成初始刷新。
    /// </summary>
    public void SetInputEnabled(bool enabled)
    {
        _isInputEnabled = enabled && _isRuntimeInitialized;
        if (!_isInputEnabled)
        {
            bool hadActiveStroke = _activeStrokeObject != null;
            CancelPointerSession(true);
            if (!hadActiveStroke)
                NotifyInkChanged();
            return;
        }

        NotifyInkChanged();
    }

    /// <summary>
    /// 设置画线暂停状态；暂停期间不会接收输入。
    /// </summary>
    public void SetPaused(bool paused)
    {
        _isPaused = paused;
        if (_isPaused)
            CancelPointerSession(true);
    }

    /// <summary>
    /// 清理当前尝试的全部运行时线条、碰撞体和长度消耗。
    /// </summary>
    public void ResetAttempt()
    {
        if (!_isRuntimeInitialized)
            return;

        CancelPointerSession(false);
        for (int i = 0; i < _completedStrokeObjects.Count; i++)
        {
            if (_completedStrokeObjects[i] != null)
                Destroy(_completedStrokeObjects[i]);
        }

        _completedStrokeObjects.Clear();
        _usedInk = 0f;
        _bonusInk = 0f;
        RestoreInitialHintStates();

        NotifyInkChanged();
    }

    /// <summary>
    /// 显示或隐藏策划预设的全部虚线提示对象。
    /// 调用边界：统一控制 tipsObjects；不会改变 Reset 初态缓存。
    /// </summary>
    public void ShowHint(bool visible)
    {
        SetHintObjectsActive(visible);
    }

    /// <summary>
    /// 增加当前尝试的奖励墨水容量，返回实际增加的 DrawSpace 长度。
    /// 注意事项：奖励容量可以使剩余墨水超过基础 maxInkLength，Reset 时清零。
    /// </summary>
    public float AddInk(float amount)
    {
        if (!_isRuntimeInitialized || amount <= 0f)
            return 0f;

        float addedInk = Mathf.Max(0f, amount);
        _bonusInk += addedInk;
        NotifyInkChanged();
        return addedInk;
    }

    /// <summary>
    /// 更新玩家实际绘制线条的材质和颜色，并同步当前尝试已经生成的线条。
    /// 注意事项：不会修改 TipsObject 中策划预设的提示虚线。
    /// </summary>
    public void SetDrawingStyle(Material material, Color color)
    {
        _drawingLineMaterial = material;
        _drawingLineGradient = CreateSolidGradient(color);
        RefreshDrawingStyle();
    }

    /// <summary>
    /// 更新玩家实际绘制线条的材质和渐变，并同步当前尝试已经生成的线条。
    /// 注意事项：传入 null 渐变时保留当前渐变配置，不修改提示虚线。
    /// </summary>
    public void SetDrawingStyle(Material material, Gradient gradient)
    {
        _drawingLineMaterial = material;
        if (gradient != null)
            _drawingLineGradient = gradient;

        RefreshDrawingStyle();
    }

    #endregion

    #region Unity Lifecycle

    private void OnDisable()
    {
        if (Application.isPlaying)
            CancelPointerSession(true);
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (Application.isPlaying && pauseStatus)
            CancelPointerSession(true);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (Application.isPlaying && !hasFocus)
            CancelPointerSession(true);
    }

    private void OnDestroy()
    {
        if (_runtimeFallbackMaterial == null)
            return;

        Destroy(_runtimeFallbackMaterial);
        _runtimeFallbackMaterial = null;
    }

    #endregion

    #region Input

#if UNITY_EDITOR || UNITY_STANDALONE
    private void UpdateMouseInput()
    {
        if (_activeFingerId == NoFinger)
        {
            if (Input.GetMouseButtonDown(0))
                TryBeginPointerSession(Input.mousePosition, -1, true);

            return;
        }

        if (!_usingEditorMouse)
            return;

        if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0))
        {
            CompletePointerSession();
            return;
        }

        if (_activeStrokeObject == null)
            TryBeginStroke(Input.mousePosition, -1);
        else
            TryContinueStroke(Input.mousePosition);
    }
#endif

    private void UpdateTouchInput()
    {
        if (_activeFingerId != NoFinger)
        {
            if (_usingEditorMouse)
                return;

            if (!TryGetActiveTouch(out Touch activeTouch))
            {
                CancelPointerSession(true);
                return;
            }

            if (activeTouch.phase == TouchPhase.Ended)
            {
                CompletePointerSession();
                return;
            }

            if (activeTouch.phase == TouchPhase.Canceled)
            {
                CancelPointerSession(true);
                return;
            }

            if (_activeStrokeObject == null)
                TryBeginStroke(activeTouch.position, activeTouch.fingerId);
            else
                TryContinueStroke(activeTouch.position);

            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase != TouchPhase.Began)
                continue;

            if (TryBeginPointerSession(touch.position, touch.fingerId, false))
                break;
        }
    }

    private bool TryBeginPointerSession(
        Vector2 screenPosition,
        int fingerId,
        bool usingEditorMouse)
    {
        if (!CanBeginStroke() || IsPointerOverUi(screenPosition, fingerId))
            return false;

        _activeFingerId = fingerId;
        _usingEditorMouse = usingEditorMouse;
        TryBeginStroke(screenPosition, fingerId);
        return true;
    }

    private bool TryBeginStroke(Vector2 screenPosition, int fingerId)
    {
        if (!CanBeginStroke() || IsPointerOverUi(screenPosition, fingerId))
            return false;

        if (!TryProjectToDrawSpace(screenPosition, out Vector2 localPoint, out Vector2 worldPoint) ||
            !IsInsideDrawArea(worldPoint) ||
            IsBlockedAt(worldPoint))
        {
            return false;
        }

        SyncBrushStyleFromCurrentSelection();

        GameObject strokeObject;
        LineRenderer strokeRenderer;
        if (lineTemplate != null)
        {
            strokeObject = Instantiate(lineTemplate.gameObject, _runtimeLinesRoot);
            strokeRenderer = strokeObject.GetComponent<LineRenderer>();
        }
        else
        {
            strokeObject = new GameObject();
            strokeObject.transform.SetParent(_runtimeLinesRoot, false);
            strokeRenderer = strokeObject.AddComponent<LineRenderer>();
            strokeRenderer.numCornerVertices = CompletedStrokeCornerVertices;
            strokeRenderer.numCapVertices = CompletedStrokeCapVertices;
        }

        strokeObject.name = $"RuntimeStroke_{CompletedStrokeCount + 1}";
        strokeObject.transform.localPosition = Vector3.zero;
        strokeObject.transform.localRotation = Quaternion.identity;
        strokeObject.transform.localScale = Vector3.one;
        PrepareRuntimeStrokeRigidbody(strokeObject);
        strokeObject.SetActive(true);

        _activeStrokeObject = strokeObject;
        _activeLineRenderer = strokeRenderer;
        if (_activeLineRenderer == null)
        {
            Destroy(strokeObject);
            ClearActiveStrokeState();
            ClearPointerSession();
            this.LogError("[DrawLineFeature] 画线模板不包含 LineRenderer。");
            return false;
        }

        _activeLineRenderer.useWorldSpace = false;
        _activeLineRenderer.positionCount = 1;
        _activeLineRenderer.SetPosition(0, new Vector3(localPoint.x, localPoint.y, 0f));
        ApplyDrawingStyle(_activeLineRenderer);

        _activePoints.Clear();
        _activePoints.Add(localPoint);
        _activeStrokeLength = 0f;
        StrokeStarted?.Invoke();
        return true;
    }

    private void TryContinueStroke(Vector2 screenPosition)
    {
        if (!TryProjectToDrawSpace(screenPosition, out Vector2 localPoint, out Vector2 worldPoint))
            return;

        if (!IsInsideDrawArea(worldPoint))
            return;

        AppendSamplesTowards(localPoint);
    }

    private bool AppendSamplesTowards(Vector2 targetPoint)
    {
        if (_activePoints == null || _activePoints.Count == 0 || _activeLineRenderer == null)
            return false;

        Vector2 lastPoint = _activePoints[_activePoints.Count - 1];
        Vector2 delta = targetPoint - lastPoint;
        float spacing = Mathf.Max(0.001f, sampleSpacing);

        while (delta.sqrMagnitude >= spacing * spacing)
        {
            float remainingInk = GetCurrentInkCapacity() - _usedInk;
            if (remainingInk <= MinimumStrokeLength)
                return false;

            float stepLength = Mathf.Min(spacing, remainingInk);
            Vector2 nextPoint = lastPoint + delta.normalized * stepLength;
            if (enableDrawAreaRestriction &&
                !IsInsideDrawArea(drawSpace.TransformPoint(nextPoint)))
            {
                return true;
            }

            if (IsBlockedBetween(lastPoint, nextPoint))
                return true;

            AppendPoint(nextPoint, stepLength);

            lastPoint = nextPoint;
            delta = targetPoint - lastPoint;
        }

        return false;
    }

    private void AppendPoint(Vector2 localPoint, float length)
    {
        _activePoints.Add(localPoint);
        _activeLineRenderer.positionCount = _activePoints.Count;
        _activeLineRenderer.SetPosition(
            _activePoints.Count - 1,
            new Vector3(localPoint.x, localPoint.y, 0f));

        _activeStrokeLength += length;
        _usedInk = Mathf.Min(GetCurrentInkCapacity(), _usedInk + length);
        NotifyInkChanged();
    }

    private void CompleteActiveStroke()
    {
        if (_activeStrokeObject == null)
            return;

        if (_activePoints == null || _activePoints.Count < 2 || _activeStrokeLength < MinimumStrokeLength)
        {
            CancelActiveStroke(true);
            return;
        }

        RecenterActiveStrokeGeometry();

        if (buildCollider)
            BuildActiveStrokeCollider();

        ApplyCompletedStrokeVisualSmoothing();
        ActivateRuntimeStrokeRigidbody(_activeStrokeObject, _activeStrokeLength);
        _completedStrokeObjects.Add(_activeStrokeObject);
        DrawStrokeResult result = new(
            _completedStrokeObjects.Count,
            _activeStrokeLength,
            CreateInkState());

        ClearActiveStrokeState();
        StrokeCompleted?.Invoke(result);

        if (!allowMultipleStrokes)
            _isInputEnabled = false;
    }

    /// <summary>
    /// 把已完成线段的 Transform 原点移动到折线包围盒中心，并同步改写渲染与碰撞局部点。
    /// 调用边界：仅在线段有效性检查通过、Rigidbody2D 仍暂停模拟时调用。
    /// 注意事项：线段世界位置和形状保持不变，后续旋转与物理质心不再围绕 DrawSpace 原点偏转。
    /// </summary>
    private void RecenterActiveStrokeGeometry()
    {
        if (_activeStrokeObject == null ||
            _activeLineRenderer == null ||
            _activePoints == null ||
            _activePoints.Count == 0)
        {
            return;
        }

        Vector2 minimum = _activePoints[0];
        Vector2 maximum = _activePoints[0];
        for (int i = 1; i < _activePoints.Count; i++)
        {
            minimum = Vector2.Min(minimum, _activePoints[i]);
            maximum = Vector2.Max(maximum, _activePoints[i]);
        }

        Vector2 localCenter = (minimum + maximum) * 0.5f;
        Vector3 worldCenter = _activeStrokeObject.transform.TransformPoint(
            new Vector3(localCenter.x, localCenter.y, 0f));

        for (int i = 0; i < _activePoints.Count; i++)
        {
            Vector2 centeredPoint = _activePoints[i] - localCenter;
            _activePoints[i] = centeredPoint;
            _activeLineRenderer.SetPosition(
                i,
                new Vector3(centeredPoint.x, centeredPoint.y, 0f));
        }

        _activeStrokeObject.transform.position = worldCenter;
    }

    /// <summary>
    /// 根据当前关卡配置为已完成线段构建边界或有厚度复合碰撞体。
    /// 调用边界：只在线段中心归位后、Rigidbody2D 开启模拟前调用。
    /// </summary>
    private void BuildActiveStrokeCollider()
    {
        if (enableStrokeToStrokeCollision)
        {
            BuildCompositeStrokeCollider();
            return;
        }

        int createdSegmentCount = BuildBoxStrokeColliderChain(_activePoints);
        if (createdSegmentCount == 0)
            this.LogError("[DrawLineFeature] 未生成有效 BoxCollider2D 线段碰撞体。");
    }

    /// <summary>
    /// 按相邻采样点生成互相重叠的 BoxCollider2D 链，并由根节点唯一 Rigidbody2D 统一控制。
    /// 调用边界：线段中心归位后、根 Rigidbody2D 开启模拟前调用。
    /// 注意事项：Box 长度额外覆盖一个碰撞直径，避免旋转线段在采样点转角处留下缝隙。
    /// </summary>
    private int BuildBoxStrokeColliderChain(IReadOnlyList<Vector2> colliderPoints)
    {
        RemoveRootEdgeCollider();
        if (colliderPoints == null || colliderPoints.Count < 2)
            return 0;

        float radius = Mathf.Max(MinimumStrokeLength, colliderRadius);
        float diameter = radius * 2f;
        int createdSegmentCount = 0;
        for (int i = 1; i < colliderPoints.Count; i++)
        {
            Vector2 start = colliderPoints[i - 1];
            Vector2 end = colliderPoints[i];
            Vector2 delta = end - start;
            float segmentLength = delta.magnitude;
            if (segmentLength < MinimumStrokeLength)
                continue;

            var segmentObject = new GameObject(
                $"StrokeBoxColliderSegment_{createdSegmentCount + 1}");
            segmentObject.layer = _activeStrokeObject.layer;

            Transform segmentTransform = segmentObject.transform;
            segmentTransform.SetParent(_activeStrokeObject.transform, false);
            segmentTransform.localPosition = (start + end) * 0.5f;
            segmentTransform.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            BoxCollider2D boxCollider = segmentObject.AddComponent<BoxCollider2D>();
            boxCollider.isTrigger = false;
            boxCollider.size = new Vector2(segmentLength + diameter, diameter);
            createdSegmentCount++;
        }

        return createdSegmentCount;
    }

    /// <summary>
    /// 用简化后的折线生成胶囊链，并通过 Polygon Composite 合并重叠区域。
    /// 调用边界：同一 RuntimeStroke 只调用一次；所有子碰撞体共用根节点 Rigidbody2D。
    /// 注意事项：单笔最多生成 MaxCompositeColliderSegments 个胶囊，限制乱画时的物理开销。
    /// </summary>
    private void BuildCompositeStrokeCollider()
    {
        List<Vector2> colliderPoints = CreateSimplifiedColliderPoints();
        if (colliderPoints.Count < 2)
        {
            this.LogWarning("[DrawLineFeature] 线段碰撞点不足，回退为 BoxCollider2D 链。");
            BuildBoxStrokeColliderChain(_activePoints);
            return;
        }

        CompositeCollider2D compositeCollider =
            _activeStrokeObject.GetComponent<CompositeCollider2D>();
        if (compositeCollider == null)
            compositeCollider = _activeStrokeObject.AddComponent<CompositeCollider2D>();

        compositeCollider.geometryType = CompositeCollider2D.GeometryType.Polygons;
        compositeCollider.generationType = CompositeCollider2D.GenerationType.Manual;
        compositeCollider.isTrigger = false;

        float radius = Mathf.Max(MinimumStrokeLength, colliderRadius);
        float diameter = radius * 2f;
        int createdSegmentCount = 0;
        for (int i = 1; i < colliderPoints.Count; i++)
        {
            Vector2 start = colliderPoints[i - 1];
            Vector2 end = colliderPoints[i];
            Vector2 delta = end - start;
            float segmentLength = delta.magnitude;
            if (segmentLength < MinimumStrokeLength)
                continue;

            var segmentObject = new GameObject(
                $"StrokeColliderSegment_{createdSegmentCount + 1}");
            segmentObject.layer = _activeStrokeObject.layer;

            Transform segmentTransform = segmentObject.transform;
            segmentTransform.SetParent(_activeStrokeObject.transform, false);
            segmentTransform.localPosition = (start + end) * 0.5f;
            segmentTransform.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

            CapsuleCollider2D capsuleCollider =
                segmentObject.AddComponent<CapsuleCollider2D>();
            capsuleCollider.direction = CapsuleDirection2D.Horizontal;
            capsuleCollider.size = new Vector2(segmentLength + diameter, diameter);
            capsuleCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
            createdSegmentCount++;
        }

        if (createdSegmentCount == 0)
        {
            compositeCollider.enabled = false;
            Destroy(compositeCollider);
            this.LogWarning("[DrawLineFeature] 未生成有效胶囊段，回退为 BoxCollider2D 链。");
            BuildBoxStrokeColliderChain(_activePoints);
            return;
        }

        RemoveRootEdgeCollider();
        compositeCollider.GenerateGeometry();
    }

    private void RemoveRootEdgeCollider()
    {
        EdgeCollider2D edgeCollider = _activeStrokeObject.GetComponent<EdgeCollider2D>();
        if (edgeCollider == null)
            return;

        edgeCollider.enabled = false;
        Destroy(edgeCollider);
    }

    /// <summary>
    /// 使用一次 Chaikin 插值平滑已完成笔画的视觉中心线。
    /// 调用边界：碰撞体已使用原始采样点构建完成后调用，只允许改写 LineRenderer。
    /// 注意事项：保留首尾点，不修改墨水长度、原始采样点或碰撞轮廓。
    /// </summary>
    private void ApplyCompletedStrokeVisualSmoothing()
    {
        if (_activeLineRenderer == null ||
            _activePoints == null ||
            _activePoints.Count < 3)
        {
            return;
        }

        int pointCount = _activePoints.Count;
        var smoothedPositions = new Vector3[pointCount * 2];
        int writeIndex = 0;
        smoothedPositions[writeIndex++] = ToLinePosition(_activePoints[0]);

        for (int i = 0; i < pointCount - 1; i++)
        {
            Vector2 start = _activePoints[i];
            Vector2 end = _activePoints[i + 1];
            smoothedPositions[writeIndex++] = ToLinePosition(
                Vector2.LerpUnclamped(start, end, CompletedStrokeSmoothingRatio));
            smoothedPositions[writeIndex++] = ToLinePosition(
                Vector2.LerpUnclamped(start, end, 1f - CompletedStrokeSmoothingRatio));
        }

        smoothedPositions[writeIndex] = ToLinePosition(_activePoints[pointCount - 1]);
        _activeLineRenderer.positionCount = smoothedPositions.Length;
        _activeLineRenderer.SetPositions(smoothedPositions);
    }

    private static Vector3 ToLinePosition(Vector2 point)
    {
        return new Vector3(point.x, point.y, 0f);
    }

    /// <summary>
    /// 对当前采样点执行 RDP 简化，并限制复合碰撞段数量。
    /// 注意事项：仅改变碰撞近似，不改 LineRenderer、墨水长度或玩家可见笔迹。
    /// </summary>
    private List<Vector2> CreateSimplifiedColliderPoints()
    {
        int sourceCount = _activePoints?.Count ?? 0;
        if (sourceCount <= 2)
            return sourceCount == 0
                ? new List<Vector2>()
                : new List<Vector2>(_activePoints);

        var retained = new bool[sourceCount];
        retained[0] = true;
        retained[sourceCount - 1] = true;

        float tolerance = Mathf.Max(
            MinimumStrokeLength,
            Mathf.Max(sampleSpacing, colliderRadius) * 0.5f);
        MarkSimplifiedPoints(
            _activePoints,
            0,
            sourceCount - 1,
            tolerance * tolerance,
            retained);

        var simplified = new List<Vector2>(
            Mathf.Min(sourceCount, MaxCompositeColliderSegments + 1));
        for (int i = 0; i < sourceCount; i++)
        {
            if (retained[i])
                simplified.Add(_activePoints[i]);
        }

        if (simplified.Count <= MaxCompositeColliderSegments + 1)
            return simplified;

        var limited = new List<Vector2>(MaxCompositeColliderSegments + 1);
        int lastIndex = simplified.Count - 1;
        for (int i = 0; i <= MaxCompositeColliderSegments; i++)
        {
            int sourceIndex = Mathf.RoundToInt(
                i * lastIndex / (float)MaxCompositeColliderSegments);
            limited.Add(simplified[sourceIndex]);
        }

        return limited;
    }

    private static void MarkSimplifiedPoints(
        IReadOnlyList<Vector2> points,
        int startIndex,
        int endIndex,
        float toleranceSquared,
        bool[] retained)
    {
        if (endIndex <= startIndex + 1)
            return;

        Vector2 start = points[startIndex];
        Vector2 end = points[endIndex];
        float greatestDistanceSquared = -1f;
        int greatestDistanceIndex = -1;

        for (int i = startIndex + 1; i < endIndex; i++)
        {
            float distanceSquared =
                GetPointSegmentDistanceSquared(points[i], start, end);
            if (distanceSquared <= greatestDistanceSquared)
                continue;

            greatestDistanceSquared = distanceSquared;
            greatestDistanceIndex = i;
        }

        if (greatestDistanceIndex < 0 ||
            greatestDistanceSquared <= toleranceSquared)
        {
            return;
        }

        retained[greatestDistanceIndex] = true;
        MarkSimplifiedPoints(
            points,
            startIndex,
            greatestDistanceIndex,
            toleranceSquared,
            retained);
        MarkSimplifiedPoints(
            points,
            greatestDistanceIndex,
            endIndex,
            toleranceSquared,
            retained);
    }

    private static float GetPointSegmentDistanceSquared(
        Vector2 point,
        Vector2 segmentStart,
        Vector2 segmentEnd)
    {
        Vector2 segment = segmentEnd - segmentStart;
        float segmentLengthSquared = segment.sqrMagnitude;
        if (segmentLengthSquared <= MinimumStrokeLength * MinimumStrokeLength)
            return (point - segmentStart).sqrMagnitude;

        float interpolation = Mathf.Clamp01(
            Vector2.Dot(point - segmentStart, segment) / segmentLengthSquared);
        Vector2 nearestPoint = segmentStart + segment * interpolation;
        return (point - nearestPoint).sqrMagnitude;
    }

    private void CancelActiveStroke(bool refundInk)
    {
        if (_activeStrokeObject == null)
        {
            ClearActiveStrokeState();
            return;
        }

        if (refundInk)
            _usedInk = Mathf.Max(0f, _usedInk - _activeStrokeLength);

        Destroy(_activeStrokeObject);
        ClearActiveStrokeState();

        if (refundInk)
            NotifyInkChanged();
    }

    private void CompletePointerSession()
    {
        CompleteActiveStroke();
        ClearPointerSession();
    }

    private void CancelPointerSession(bool refundInk)
    {
        CancelActiveStroke(refundInk);
        ClearPointerSession();
    }

    private void ClearActiveStrokeState()
    {
        _activeStrokeObject = null;
        _activeLineRenderer = null;
        _activeStrokeLength = 0f;
        _activePoints?.Clear();
    }

    private void ClearPointerSession()
    {
        _activeFingerId = NoFinger;
        _usingEditorMouse = false;
    }

    private bool CanBeginStroke()
    {
        if (!_isRuntimeInitialized || !_isInputEnabled || _isPaused ||
            _activeStrokeObject != null ||
            CreateInkState().RemainingInk <= MinimumStrokeLength)
        {
            return false;
        }

        return allowMultipleStrokes || CompletedStrokeCount == 0;
    }

    private bool TryGetActiveTouch(out Touch activeTouch)
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId != _activeFingerId)
                continue;

            activeTouch = touch;
            return true;
        }

        activeTouch = default;
        return false;
    }

    private static bool IsPointerOverUi(Vector2 screenPosition, int fingerId)
    {
        return fingerId >= 0
            ? InputUtils.IsPointerOverUI(fingerId)
            : InputUtils.IsPointerOverUI(screenPosition);
    }

    #endregion

    #region Coordinates And Area

    private bool TryProjectToDrawSpace(
        Vector2 screenPosition,
        out Vector2 localPoint,
        out Vector2 worldPoint)
    {
        localPoint = default;
        worldPoint = default;

        if (inputCamera == null || drawSpace == null)
            return false;

        Ray ray = inputCamera.ScreenPointToRay(screenPosition);
        Plane plane = new(drawSpace.forward, drawSpace.position);
        if (!plane.Raycast(ray, out float enter))
            return false;

        Vector3 worldPosition = ray.GetPoint(enter);
        Vector3 localPosition = drawSpace.InverseTransformPoint(worldPosition);
        localPoint = new Vector2(localPosition.x, localPosition.y);
        worldPoint = new Vector2(worldPosition.x, worldPosition.y);
        return true;
    }

    private bool IsInsideDrawArea(Vector2 worldPoint)
    {
        return !enableDrawAreaRestriction ||
               (drawArea != null &&
                drawArea.enabled &&
                drawArea.gameObject.activeInHierarchy &&
                drawArea.OverlapPoint(worldPoint));
    }

    private bool IsBlockedAt(Vector2 worldPoint)
    {
        return Physics2D.defaultPhysicsScene.OverlapCircle(
            worldPoint,
            GetWorldBlockingRadius(),
            _blockingContactFilter) != null;
    }

    private bool IsBlockedBetween(Vector2 fromLocalPoint, Vector2 toLocalPoint)
    {
        Vector2 fromWorldPoint = drawSpace.TransformPoint(fromLocalPoint);
        Vector2 toWorldPoint = drawSpace.TransformPoint(toLocalPoint);
        Vector2 delta = toWorldPoint - fromWorldPoint;
        float distance = delta.magnitude;
        float radius = GetWorldBlockingRadius();

        if (Physics2D.defaultPhysicsScene.OverlapCircle(
                toWorldPoint,
                radius,
                _blockingContactFilter) != null)
        {
            return true;
        }

        return distance > MinimumStrokeLength &&
               Physics2D.defaultPhysicsScene.CircleCast(
                   fromWorldPoint,
                   radius,
                   delta / distance,
                   distance,
                   _blockingContactFilter).collider != null;
    }

    private float GetWorldBlockingRadius()
    {
        Vector3 drawScale = drawSpace.lossyScale;
        float maximumScale = Mathf.Max(Mathf.Abs(drawScale.x), Mathf.Abs(drawScale.y));
        return drawingLineWidth * 0.5f * maximumScale;
    }

    #endregion

    #region Ink And Events

    private DrawInkState CreateInkState()
    {
        float baseMaxInk = Mathf.Max(0f, maxInkLength);
        float currentCapacity = GetCurrentInkCapacity();
        float remainingInk = Mathf.Max(0f, currentCapacity - _usedInk);

        int stars = 0;
        if (remainingInk >= stage.x)
            stars = 1;
        if (remainingInk >= stage.y)
            stars = 2;
        if (remainingInk >= stage.z)
            stars = 3;

        return new DrawInkState(
            baseMaxInk,
            currentCapacity,
            _usedInk,
            stars,
            stage);
    }

    private float GetCurrentInkCapacity()
    {
        return Mathf.Max(0f, maxInkLength) + Mathf.Max(0f, _bonusInk);
    }

    private void NotifyInkChanged()
    {
        DrawInkState inkState = CreateInkState();
        this.DispatchEvent(Witness<DrawInkStateChangedEvent>._, inkState);
    }

    #endregion

    #region Editor Tools

#if UNITY_EDITOR
    /// <summary>
    /// 用途：从当前 Draw 关卡层级中补齐尚未绑定的画线引用。
    /// 调用边界：仅供 Unity Editor Inspector 手动点击，不参与运行时初始化或自动生命周期。
    /// 注意事项：不会覆盖已有人工引用，不创建对象，也不访问 DB、Save、配置或运行时单例。
    /// </summary>
    [Button("AutoBind")]
    public void AutoBind()
    {
        if (Application.isPlaying)
            return;

        Camera bindInputCamera = inputCamera == null
            ? GetComponentInChildren<Camera>(true)
            : null;
        Transform bindDrawSpace = drawSpace == null ? transform : null;
        List<GameObject> bindTipsObjects = !HasConfiguredHintObjects()
            ? FindEditorHintObjects(transform)
            : null;

        Transform lineTemplateTransform = lineTemplate == null
            ? FindEditorChildByName(transform, "DrawGame_DrawLine")
              ?? FindEditorChildByName(transform, "DrawLineRender")
            : null;
        LineRenderer bindLineTemplate = lineTemplateTransform != null
            ? lineTemplateTransform.GetComponent<LineRenderer>()
            : null;
        Collider2D bindDrawArea = drawArea == null
            ? FindEditorDrawArea(transform)
            : null;

        bool hasChanges = bindInputCamera != null
                          || bindDrawSpace != null
                          || bindTipsObjects != null
                          || bindLineTemplate != null
                          || bindDrawArea != null;
        if (!hasChanges)
            return;

        Undo.RecordObject(this, "DrawLineFeature AutoBind");

        if (bindInputCamera != null)
            inputCamera = bindInputCamera;
        if (bindDrawSpace != null)
            drawSpace = bindDrawSpace;
        if (bindTipsObjects != null)
            tipsObjects = bindTipsObjects;
        if (bindLineTemplate != null)
            lineTemplate = bindLineTemplate;
        if (bindDrawArea != null)
            drawArea = bindDrawArea;

        EditorUtility.SetDirty(this);
        if (PrefabUtility.IsPartOfPrefabInstance(this))
            PrefabUtility.RecordPrefabInstancePropertyModifications(this);
    }

    private static Transform FindEditorChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child != root && child.name == childName)
                return child;
        }

        return null;
    }

    private static List<GameObject> FindEditorHintObjects(Transform root)
    {
        if (root == null)
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        List<GameObject> results = new();
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child == root)
                continue;

            string childName = child.name;
            bool isTipsObject = childName == "TipsObject" ||
                                childName == "DrawGame_TipsObjectSubObject" ||
                                childName.StartsWith("DrawGame_TipsObjectSubObject (", StringComparison.Ordinal);
            if (isTipsObject && !results.Contains(child.gameObject))
                results.Add(child.gameObject);
        }

        return results.Count > 0 ? results : null;
    }

    private static Collider2D FindEditorDrawArea(Transform root)
    {
        Transform areaTransform = FindEditorChildByName(root, "DrawLineArea")
                                  ?? FindEditorChildByName(root, "DrawArea")
                                  ?? FindEditorChildByName(root, "DrawAreaCollider");
        Collider2D areaCollider = areaTransform != null
            ? areaTransform.GetComponent<Collider2D>()
            : null;
        int drawLayer = LayerMask.NameToLayer(DrawAreaLayerName);
        return areaCollider != null &&
               drawLayer >= 0 &&
               areaCollider.gameObject.layer == drawLayer
            ? areaCollider
            : null;
    }
#endif

    #endregion

    #region Reference Resolution And Style

    private bool HasConfiguredHintObjects()
    {
        if (tipsObjects == null)
            return false;

        for (int i = 0; i < tipsObjects.Count; i++)
        {
            if (tipsObjects[i] != null)
                return true;
        }

        return false;
    }

    private void CacheInitialHintStates()
    {
        _initialHintStates.Clear();
        if (tipsObjects == null)
            return;

        for (int i = 0; i < tipsObjects.Count; i++)
            CacheInitialHintState(tipsObjects[i]);
    }

    private void CacheInitialHintState(GameObject hintObject)
    {
        if (hintObject == null)
            return;

        for (int i = 0; i < _initialHintStates.Count; i++)
        {
            if (_initialHintStates[i].HintObject == hintObject)
                return;
        }

        _initialHintStates.Add(new HintInitialState(hintObject, hintObject.activeSelf));
    }

    private void RestoreInitialHintStates()
    {
        for (int i = 0; i < _initialHintStates.Count; i++)
        {
            HintInitialState initialState = _initialHintStates[i];
            if (initialState.HintObject != null)
                initialState.HintObject.SetActive(initialState.WasActive);
        }
    }

    private void SetHintObjectsActive(bool visible)
    {
        if (tipsObjects == null)
            return;

        for (int i = 0; i < tipsObjects.Count; i++)
        {
            GameObject hintObject = tipsObjects[i];
            if (hintObject == null)
                continue;

            bool isDuplicate = false;
            for (int previousIndex = 0; previousIndex < i; previousIndex++)
            {
                if (tipsObjects[previousIndex] != hintObject)
                    continue;

                isDuplicate = true;
                break;
            }

            if (!isDuplicate)
                hintObject.SetActive(visible);
        }
    }

    private void ResolveReferences()
    {
        if (drawSpace == null)
            drawSpace = transform;

        if (inputCamera == null)
            inputCamera = GetComponentInChildren<Camera>(true);

        if (inputCamera == null)
            inputCamera = Camera.main;

        if (!HasConfiguredHintObjects())
        {
            Transform tipsTransform = transform.Find("TipsObject");
            if (tipsTransform != null)
            {
                tipsObjects ??= new List<GameObject>();
                tipsObjects.Add(tipsTransform.gameObject);
            }
        }

        if (lineTemplate == null)
            lineTemplate = transform.Find("DrawLineRender")?.GetComponent<LineRenderer>();
    }

    private bool ValidateDrawAreaLayer()
    {
        int drawLayer = LayerMask.NameToLayer(DrawAreaLayerName);
        if (drawLayer < 0)
        {
            this.LogError(
                $"[DrawLineFeature] 项目未定义 {DrawAreaLayerName} Layer，画线输入保持关闭。");
            return false;
        }

        _blockingLayerMask = ~(1 << drawLayer);
        _blockingContactFilter = new ContactFilter2D
        {
            useTriggers = true
        };
        _blockingContactFilter.SetLayerMask(_blockingLayerMask);
        if (!enableDrawAreaRestriction)
            return true;

        if (drawArea == null)
        {
            this.LogError(
                $"[DrawLineFeature] drawArea 未绑定，无法确认 {DrawAreaLayerName} Layer 可绘制区域，画线输入保持关闭。");
            return false;
        }

        if (drawArea.gameObject.layer == drawLayer)
            return true;

        string currentLayer = LayerMask.LayerToName(drawArea.gameObject.layer);
        this.LogError(
            $"[DrawLineFeature] drawArea 必须位于 {DrawAreaLayerName} Layer。" +
            $" object:{drawArea.gameObject.name}, current:{currentLayer}({drawArea.gameObject.layer})");
        return false;
    }

    private void NormalizeConfiguration()
    {
        sampleSpacing = Mathf.Max(0.001f, sampleSpacing);
        maxInkLength = Mathf.Max(sampleSpacing, maxInkLength);
        drawingLineWidth = Mathf.Max(0.001f, drawingLineWidth);
        _drawingLineGradient ??= CreateSolidGradient(Color.black);
        strokeMass = Mathf.Max(MinimumRigidbodyMass, strokeMass);
        minimumMassRatio = Mathf.Clamp(minimumMassRatio, 0.01f, 1f);
        massByLengthCurve ??= AnimationCurve.Linear(0f, 0f, 1f, 1f);

        NormalizeStarThresholds();
    }

    private void NormalizeStarThresholds()
    {
        float maxInk = Mathf.Max(0f, maxInkLength);
        float oneStar = Mathf.Clamp(stage.x, 0f, maxInk);
        float twoStars = Mathf.Clamp(stage.y, oneStar, maxInk);
        float threeStars = Mathf.Clamp(stage.z, twoStars, maxInk);
        stage = new Vector3(oneStar, twoStars, threeStars);
    }

    private void EnsureRuntimeRoot()
    {
        if (_runtimeLinesRoot != null || drawSpace == null)
            return;

        var runtimeRootObject = new GameObject("RuntimeLines");
        _runtimeLinesRoot = runtimeRootObject.transform;
        _runtimeLinesRoot.SetParent(drawSpace, false);
    }

    /// <summary>
    /// 为新建的运行时线条准备二维物理体，并在绘制期间暂停物理模拟。
    /// 调用边界：仅在 RuntimeStroke 创建完成后调用；模板已有 Rigidbody2D 时复用并覆盖为动态刚体。
    /// 注意事项：绘制结束前保持 simulated=false，避免尚未完成的线条受重力或碰撞影响。
    /// </summary>
    private static void PrepareRuntimeStrokeRigidbody(GameObject strokeObject)
    {
        if (strokeObject == null)
            return;

        Rigidbody2D rigidbody2D = strokeObject.GetComponent<Rigidbody2D>();
        if (rigidbody2D == null)
            rigidbody2D = strokeObject.AddComponent<Rigidbody2D>();

        rigidbody2D.bodyType = RigidbodyType2D.Dynamic;
        rigidbody2D.simulated = false;
        rigidbody2D.linearVelocity = Vector2.zero;
        rigidbody2D.angularVelocity = 0f;
    }

    /// <summary>
    /// 在有效线条完成后应用 DrawLineFeature 质量配置并启用二维物理模拟。
    /// 调用边界：必须在线条有效性检查通过、碰撞点配置完成后调用。
    /// 注意事项：质量由 strokeMass 与长度规则决定；重力缩放、阻力与约束继续保留 prefab 配置。
    /// </summary>
    private void ActivateRuntimeStrokeRigidbody(GameObject strokeObject, float strokeLength)
    {
        if (strokeObject == null)
            return;

        Rigidbody2D rigidbody2D = strokeObject.GetComponent<Rigidbody2D>();
        if (rigidbody2D == null)
            rigidbody2D = strokeObject.AddComponent<Rigidbody2D>();

        ApplyConfiguredStrokeMass(rigidbody2D, strokeLength);

        rigidbody2D.bodyType = RigidbodyType2D.Dynamic;
        rigidbody2D.simulated = true;
        rigidbody2D.WakeUp();
    }

    /// <summary>
    /// 将 DrawLineFeature 配置的质量写入运行时线条刚体。
    /// 调用边界：仅由 ActivateRuntimeStrokeRigidbody 在物理模拟开启前调用。
    /// 注意事项：关闭长度缩放时直接使用 strokeMass；奖励墨水不会改变同长度线条的质量。
    /// </summary>
    private void ApplyConfiguredStrokeMass(Rigidbody2D rigidbody2D, float strokeLength)
    {
        if (rigidbody2D == null)
            return;

        float massRatio = 1f;
        if (scaleMassByLength)
        {
            float referenceLength = Mathf.Max(MinimumStrokeLength, maxInkLength);
            float normalizedLength = Mathf.Clamp01(Mathf.Max(0f, strokeLength) / referenceLength);
            float curveValue = normalizedLength >= 1f
                ? 1f
                : Mathf.Clamp01(massByLengthCurve.Evaluate(normalizedLength));
            massRatio = Mathf.Lerp(minimumMassRatio, 1f, curveValue);
        }

        rigidbody2D.useAutoMass = false;
        rigidbody2D.mass = Mathf.Max(MinimumRigidbodyMass, strokeMass * massRatio);
    }

    private void RefreshDrawingStyle()
    {
        ApplyDrawingStyle(_activeLineRenderer);
        for (int i = 0; i < _completedStrokeObjects.Count; i++)
        {
            GameObject strokeObject = _completedStrokeObjects[i];
            if (strokeObject != null)
                ApplyDrawingStyle(strokeObject.GetComponent<LineRenderer>());
        }
    }

    /// <summary>
    /// 根据当前配置来源同步笔刷配置。
    /// 调用边界：仅在运行时初始化及有效落笔前调用，ID 未变化时不会重复加载资源。
    /// 注意事项：切换只影响后续新笔画；选中配置无效时使用固定默认笔刷 ID。
    /// </summary>
    private void SyncBrushStyleFromCurrentSelection()
    {
        string selectedBrushId = useInspectorPencilSkinConfigId
            ? brushStyleId
            : DB.DrawGame?.CurrentSkinId;
        string normalizedBrushId = string.IsNullOrWhiteSpace(selectedBrushId)
            ? DefaultBrushStyleId
            : selectedBrushId.Trim();

        if (string.Equals(_observedBrushStyleId, normalizedBrushId, StringComparison.Ordinal))
            return;

        _observedBrushStyleId = normalizedBrushId;
        if (TryApplyBrushStyle(normalizedBrushId))
            return;

        if (string.Equals(normalizedBrushId, DefaultBrushStyleId, StringComparison.Ordinal))
            return;

        this.LogWarning(
            $"[DrawLineFeature] 当前笔刷配置不可用，改用默认配置。selected:{normalizedBrushId}, fallback:{DefaultBrushStyleId}");
        TryApplyBrushStyle(DefaultBrushStyleId);
    }

    private bool TryApplyBrushStyle(string normalizedBrushId)
    {
        string resourcePath =
            $"{PencilSkinConfigResourceFolder}/{PencilSkinConfigAssetPrefix}{normalizedBrushId}";
        DrawBrushStyleAsset brushStyle =
            ResourceManager.Instance.Load<DrawBrushStyleAsset>(resourcePath);
        if (brushStyle == null)
        {
            this.LogWarning(
                $"[DrawLineFeature] 笔刷配置加载失败。id:{normalizedBrushId}, path:{resourcePath}");
            return false;
        }

        if (!brushStyle.TryValidate(out string validationError))
        {
            this.LogWarning(
                $"[DrawLineFeature] 笔刷配置无效。id:{normalizedBrushId}, error:{validationError}");
            return false;
        }

        if (!string.Equals(brushStyle.BrushId?.Trim(), normalizedBrushId, StringComparison.Ordinal))
        {
            this.LogWarning(
                $"[DrawLineFeature] 笔刷配置 ID 与加载路径不一致。requested:{normalizedBrushId}, asset:{brushStyle.BrushId}, path:{resourcePath}");
            return false;
        }

        _drawingLineMaterial = brushStyle.LineMaterial;
        _drawingLineGradient = brushStyle.ColorGradient;
        return true;
    }

    private void ApplyDrawingStyle(LineRenderer targetRenderer)
    {
        if (targetRenderer == null)
            return;

        Material material = _drawingLineMaterial != null
            ? _drawingLineMaterial
            : GetOrCreateFallbackMaterial();
        if (material != null)
            targetRenderer.sharedMaterial = material;

        targetRenderer.textureMode = LineTextureMode.Tile;
        targetRenderer.numCornerVertices = CompletedStrokeCornerVertices;
        targetRenderer.numCapVertices = CompletedStrokeCapVertices;
        targetRenderer.widthMultiplier = drawingLineWidth;
        targetRenderer.colorGradient = _drawingLineGradient;
    }

    private static Gradient CreateSolidGradient(Color color)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color, 1f),
            },
            new[]
            {
                new GradientAlphaKey(color.a, 0f),
                new GradientAlphaKey(color.a, 1f),
            });
        return gradient;
    }

    private Material GetOrCreateFallbackMaterial()
    {
        if (_runtimeFallbackMaterial != null)
            return _runtimeFallbackMaterial;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            this.LogWarning("[DrawLineFeature] Sprites/Default shader 未找到，将暂时沿用画线模板材质。");
            return lineTemplate != null ? lineTemplate.sharedMaterial : null;
        }

        _runtimeFallbackMaterial = new Material(shader)
        {
            name = "DrawGame_RuntimeLineMaterial",
        };
        return _runtimeFallbackMaterial;
    }

    #endregion
}
