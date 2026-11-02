using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// 按 PathRoot 直接子节点的层级顺序定义折线路径，并通过单个 Mesh 渲染连续、带额外间距或多纹理随机的 Repeat 提示线。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[AddComponentMenu("Game/Draw/Draw Tips Line Object Renderer")]
public sealed class DrawTipsLineObjectRenderer : MonoBehaviour
{
    #region Constants

    private const string TipsLineMaterialPath = "Assets/Game/ArtSrc/Materials/DrawGame_TipsLine.mat";
    private const float MinimumPositiveValue = 0.0001f;
    private const float PositionSqrTolerance = 0.00000001f;
    private const string AlphaTestKeyword = "_ALPHATEST_ON";
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int RendererColorPropertyId = Shader.PropertyToID("_RendererColor");
    private static readonly int AlphaClipPropertyId = Shader.PropertyToID("_AlphaClip");
    private static readonly int BaseMapPropertyId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexturePropertyId = Shader.PropertyToID("_MainTex");

    #endregion

    #region Inspector

    [Header("Path")]
    [Tooltip("按该节点下直接子节点的层级顺序生成路径；不绑定时使用当前节点。新增、删除、重排或移动子节点都会自动刷新编辑器预览。")]
    [SerializeField] private Transform pathRoot;

    [Tooltip("运行时 Mesh 首次生成成功后关闭路径控制点 GameObject；编辑器自动预览不会隐藏控制点。")]
    [SerializeField] private bool deactivatePathPointsAfterBuild = true;

    [Header("Line")]
    [Tooltip("线段在当前节点本地空间中的固定宽度。")]
    [SerializeField, Min(MinimumPositiveValue)] private float lineWidth = 0.1f;

    [Tooltip("原纹理每一个本地长度单位重复的次数；完整纹理周期长度为 1 / Repeat，额外间距不会改变该长度。")]
    [SerializeField, Min(MinimumPositiveValue)] private float repeat = 4.347826f;

    /// <summary>
    /// 相邻短划线几何之间沿路径额外留出的本地空白，不会改变由 Repeat 决定的原纹理周期长度。
    /// </summary>
    [Tooltip("相邻短划线末端到下一条起点之间额外留出的本地空白；设为 0 时不额外留白，且未配置纹理变体时保持原连续 Repeat。")]
    [SerializeField, Min(0f)] private float segmentSpacing;

    [Tooltip("短划线周期的起始相位；离散周期模式下作用于“原纹理周期 + 额外空白”的完整周期。")]
    [SerializeField] private float uvOffset;

    [Tooltip("所有路径点转换到当前节点本地空间后使用的统一 Z 值。")]
    [SerializeField] private float positionZ;

    [Tooltip("整条提示线的颜色；生成时会复制共享材质，并只修改当前组件的临时材质实例。")]
    [SerializeField] private Color dotColor = Color.white;

    [Tooltip("允许生成的最大 Mesh 矩形片段数；间距或多纹理模式下，跨折点的短划线会拆成多个片段。")]
    [SerializeField, Min(1)] private int maxSegmentCount = 1024;

    [Tooltip("固定提示线材质；编辑器会规范为 DrawGame_TipsLine.mat。")]
    [SerializeField] private Material tipsLineMaterial;

    [Header("Texture Variants")]

    /// <summary>
    /// 每个完整短划线周期可随机使用的纹理；空列表保持材质原纹理，空引用和重复纹理会在重建时忽略。
    /// </summary>
    [Tooltip("随机短划线纹理列表；要保留材质原纹理，请把对应 Texture2D 也加入列表。空列表保持原单纹理行为。")]
    [SerializeField] private List<Texture2D> segmentTextureVariants = new List<Texture2D>();

    /// <summary>
    /// 确定性纹理序列的随机种子；相同路径、参数与纹理顺序会得到相同排布，不影响 Unity 全局随机状态。
    /// </summary>
    [Tooltip("控制纹理随机排布；相同种子会稳定生成相同序列。")]
    [SerializeField] private int randomSeed = 1;

    #endregion

    #region Runtime State

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh generatedMesh;
    private Material colorMaterialInstance;
    private Material colorMaterialSource;
    private readonly List<Vector3> pathBuffer = new List<Vector3>();
    private readonly List<Vector3> vertexBuffer = new List<Vector3>();
    private readonly List<Vector2> uvBuffer = new List<Vector2>();
    private readonly List<Color32> colorBuffer = new List<Color32>();
    private readonly List<int> triangleBuffer = new List<int>();
    private readonly List<Texture2D> activeTextureVariants = new List<Texture2D>();
    private readonly List<List<int>> textureTriangleBuffers = new List<List<int>>();
    private MaterialPropertyBlock texturePropertyBlock;
    private Material[] rendererMaterialSlots;
    private int configuredMaterialSlotCount;

#if UNITY_EDITOR
    private bool editorPreviewDirty;
    private bool editorSceneGuiRegistered;
    private bool editorPreviewBootstrapRegistered;
#endif

    private bool IsRuntimeObject => Application.IsPlaying(gameObject);

    #endregion

    #region Unity Messages

    private void Awake()
    {
        if (!IsRuntimeObject)
            return;

        CacheComponents();
        EnsureMeshState();
        EnsureColorMaterialInstance();
        ConfigureMeshRenderer();
        RebuildDots();
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        if (!IsRuntimeObject)
        {
            EnableEditorAutoPreview();
            return;
        }
#endif

        if (!IsRuntimeObject)
            return;

        CacheComponents();
        if (meshRenderer != null)
            meshRenderer.enabled = generatedMesh != null && generatedMesh.vertexCount > 0;
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        if (!IsRuntimeObject)
        {
            UnregisterEditorPreviewBootstrap();
            UnregisterEditorPreviewDrawing();
            editorPreviewDirty = false;
            DestroyGeneratedMesh();
            DestroyColorMaterialInstance();
            return;
        }
#endif

        if (meshRenderer == null)
            return;

        meshRenderer.enabled = false;
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        UnregisterEditorPreviewBootstrap();
        UnregisterEditorPreviewDrawing();
        runInEditMode = false;
#endif
        DestroyGeneratedMesh();
        DestroyColorMaterialInstance();
    }

    private void Reset()
    {
        NormalizeInspectorData();
        AssignTipsLineMaterialInEditor();

#if UNITY_EDITOR
        if (!IsRuntimeObject)
        {
            EnableEditorAutoPreview();
            return;
        }
#endif

        CacheComponents();
        SyncColorMaterialInstance();
        ConfigureMeshRenderer();
    }

    private void OnValidate()
    {
        NormalizeInspectorData();
        AssignTipsLineMaterialInEditor();

#if UNITY_EDITOR
        if (!IsRuntimeObject)
        {
            EnableEditorAutoPreview();
            return;
        }
#endif

        CacheComponents();
        SyncColorMaterialInstance();
        ConfigureMeshRenderer();
        RebuildDots();
    }

#if UNITY_EDITOR
    // 编辑器自动创建并刷新临时预览；Player 构建不包含该轮询。
    private void LateUpdate()
    {
        if (!CanAutoRefreshPath())
            return;

        bool previewMissing = generatedMesh == null ||
                              colorMaterialInstance == null;
        if (!previewMissing && !editorPreviewDirty && !HasLocalPathChanged())
            return;

        RebuildEditorPreview();
    }
#endif

    #endregion

    #region Public API

    /// <summary>
    /// 设置当前提示线材质实例的透明度，不修改共享材质资源或 RGB 颜色。
    /// 调用边界：运行时动画可持续调用；编辑器只同步自动预览创建的临时材质实例。
    /// </summary>
    public void SetAlpha(float alpha)
    {
        dotColor.a = Mathf.Clamp01(alpha);

        if (colorMaterialInstance == null && IsRuntimeObject)
        {
            CacheComponents();
            EnsureColorMaterialInstance();
            ConfigureMeshRenderer();
        }

        SyncColorMaterialInstance();
    }

    /// <summary>
    /// 按 PathRoot 直接子节点顺序，以固定宽度、可选额外间距和纹理变体重建单个 Repeat 提示线 Mesh。
    /// 调用边界：运行时可显式调用；编辑器会自动首次生成并持续刷新临时预览。
    /// </summary>
    public void RebuildDots()
    {
        if (!IsRuntimeObject)
        {
#if UNITY_EDITOR
            RebuildEditorPreview();
#endif
            return;
        }

        RebuildRuntimePath(true);
    }

    private void RebuildRuntimePath(bool updatePathPointVisibility)
    {
        CacheComponents();
        EnsureMeshState();
        EnsureColorMaterialInstance();
        ConfigureMeshRenderer();
        CollectLocalPath();
        BuildRepeatedSegmentMesh();
        ConfigureRendererMaterialSlots();
        meshRenderer.enabled = isActiveAndEnabled && generatedMesh.vertexCount > 0;

        if (updatePathPointVisibility && meshRenderer.enabled && deactivatePathPointsAfterBuild)
            SetPathPointObjectsActive(false, false);

#if UNITY_EDITOR
        editorPreviewDirty = false;
#endif
    }

    /// <summary>
    /// 隐藏当前 Repeat 提示线但保留运行时 Mesh，供后续重建复用。
    /// 调用边界：仅在 PlayMode 使用，不修改路径列表或材质。
    /// </summary>
    public void HideDots()
    {
        if (!IsRuntimeObject || meshRenderer == null)
            return;

        meshRenderer.enabled = false;
    }

    #endregion

#if UNITY_EDITOR
    private void RebuildEditorPreview()
    {
        if (!CanBuildEditorPreview())
            return;

        NormalizeInspectorData();
        AssignTipsLineMaterialInEditor();
        EnsureMeshState();
        EnsureColorMaterialInstance();
        CollectLocalPath();
        BuildRepeatedSegmentMesh();

        editorPreviewDirty = false;
        SceneView.RepaintAll();
    }

    private void EnableEditorAutoPreview()
    {
        editorPreviewDirty = true;
        if (EditorUtility.IsPersistent(this))
        {
            UnregisterEditorPreviewBootstrap();
            UnregisterEditorPreviewDrawing();
            runInEditMode = false;
            return;
        }

        runInEditMode = true;
        if (isActiveAndEnabled)
        {
            RegisterEditorPreviewDrawing();
            RegisterEditorPreviewBootstrap();
        }

        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }

    private void TryBuildEditorPreviewWhenReady()
    {
        if (IsRuntimeObject ||
            !isActiveAndEnabled ||
            EditorUtility.IsPersistent(this))
        {
            UnregisterEditorPreviewBootstrap();
            return;
        }

        if (EditorApplication.isCompiling ||
            EditorApplication.isUpdating ||
            !gameObject.scene.IsValid() ||
            !gameObject.scene.isLoaded)
        {
            return;
        }

        bool previewMissing = generatedMesh == null ||
                              colorMaterialInstance == null;
        if (!editorPreviewDirty && !previewMissing)
        {
            UnregisterEditorPreviewBootstrap();
            return;
        }

        UnregisterEditorPreviewBootstrap();
        RebuildEditorPreview();
    }

    private void RegisterEditorPreviewBootstrap()
    {
        if (editorPreviewBootstrapRegistered)
            return;

        // 资源导入期间持续等到 Editor 空闲，避免首次预览请求被一次性跳过。
        EditorApplication.update += TryBuildEditorPreviewWhenReady;
        editorPreviewBootstrapRegistered = true;
    }

    private void UnregisterEditorPreviewBootstrap()
    {
        if (!editorPreviewBootstrapRegistered)
            return;

        EditorApplication.update -= TryBuildEditorPreviewWhenReady;
        editorPreviewBootstrapRegistered = false;
    }

    private bool CanAutoRefreshPath()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return false;

        if (IsRuntimeObject)
            return false;

        if (!runInEditMode)
            return false;

        if (!CanBuildEditorPreview())
        {
            return false;
        }

        return tipsLineMaterial != null;
    }

    private bool CanBuildEditorPreview()
    {
        return !IsRuntimeObject &&
               isActiveAndEnabled &&
               !EditorUtility.IsPersistent(this) &&
               gameObject.scene.IsValid() &&
               gameObject.scene.isLoaded;
    }

    private void RegisterEditorPreviewDrawing()
    {
        if (editorSceneGuiRegistered)
            return;

        SceneView.duringSceneGui += DrawEditorPreview;
        editorSceneGuiRegistered = true;
    }

    private void UnregisterEditorPreviewDrawing()
    {
        if (!editorSceneGuiRegistered)
            return;

        SceneView.duringSceneGui -= DrawEditorPreview;
        editorSceneGuiRegistered = false;
    }

    private void DrawEditorPreview(SceneView sceneView)
    {
        if (sceneView == null ||
            sceneView.camera == null ||
            Event.current == null ||
            Event.current.type != EventType.Repaint ||
            !CanBuildEditorPreview() ||
            !StageUtility.IsGameObjectRenderedByCamera(gameObject, sceneView.camera) ||
            generatedMesh == null ||
            generatedMesh.vertexCount == 0 ||
            colorMaterialInstance == null)
        {
            return;
        }

        Matrix4x4 localToWorldMatrix = transform.localToWorldMatrix;
        if (activeTextureVariants.Count == 0)
        {
            RestoreEditorPreviewBaseTextures();
            if (colorMaterialInstance.SetPass(0))
                Graphics.DrawMeshNow(generatedMesh, localToWorldMatrix, 0);
            return;
        }

        int subMeshCount = Mathf.Min(generatedMesh.subMeshCount, activeTextureVariants.Count);
        for (int textureIndex = 0; textureIndex < subMeshCount; textureIndex++)
        {
            SetEditorPreviewTexture(activeTextureVariants[textureIndex]);
            if (colorMaterialInstance.SetPass(0))
                Graphics.DrawMeshNow(generatedMesh, localToWorldMatrix, textureIndex);
        }
    }

    private void SetEditorPreviewTexture(Texture texture)
    {
        if (colorMaterialInstance.HasProperty(BaseMapPropertyId))
            colorMaterialInstance.SetTexture(BaseMapPropertyId, texture);
        if (colorMaterialInstance.HasProperty(MainTexturePropertyId))
            colorMaterialInstance.SetTexture(MainTexturePropertyId, texture);
    }

    private void RestoreEditorPreviewBaseTextures()
    {
        if (tipsLineMaterial == null)
            return;

        if (colorMaterialInstance.HasProperty(BaseMapPropertyId) &&
            tipsLineMaterial.HasProperty(BaseMapPropertyId))
        {
            colorMaterialInstance.SetTexture(
                BaseMapPropertyId,
                tipsLineMaterial.GetTexture(BaseMapPropertyId));
        }

        if (colorMaterialInstance.HasProperty(MainTexturePropertyId) &&
            tipsLineMaterial.HasProperty(MainTexturePropertyId))
        {
            colorMaterialInstance.SetTexture(
                MainTexturePropertyId,
                tipsLineMaterial.GetTexture(MainTexturePropertyId));
        }
    }
#endif

    #region Path Processing

    private void CollectLocalPath()
    {
        pathBuffer.Clear();
        Transform effectivePathRoot = GetEffectivePathRoot();
        if (effectivePathRoot == null)
            return;

        Vector3 previousPosition = default;
        bool hasPreviousPosition = false;

        for (int pointIndex = 0; pointIndex < effectivePathRoot.childCount; pointIndex++)
        {
            Transform point = effectivePathRoot.GetChild(pointIndex);
            Vector3 localPosition = transform.InverseTransformPoint(point.position);
            localPosition.z = positionZ;

            if (hasPreviousPosition &&
                (localPosition - previousPosition).sqrMagnitude <= PositionSqrTolerance)
            {
                continue;
            }

            pathBuffer.Add(localPosition);
            previousPosition = localPosition;
            hasPreviousPosition = true;
        }
    }

#if UNITY_EDITOR
    private bool HasLocalPathChanged()
    {
        Transform effectivePathRoot = GetEffectivePathRoot();
        if (effectivePathRoot == null)
            return pathBuffer.Count != 0;

        int localPathIndex = 0;
        Vector3 previousPosition = default;
        bool hasPreviousPosition = false;

        for (int pointIndex = 0; pointIndex < effectivePathRoot.childCount; pointIndex++)
        {
            Transform point = effectivePathRoot.GetChild(pointIndex);
            Vector3 localPosition = transform.InverseTransformPoint(point.position);
            localPosition.z = positionZ;

            if (hasPreviousPosition &&
                (localPosition - previousPosition).sqrMagnitude <= PositionSqrTolerance)
            {
                continue;
            }

            if (localPathIndex >= pathBuffer.Count ||
                (pathBuffer[localPathIndex] - localPosition).sqrMagnitude > PositionSqrTolerance)
            {
                return true;
            }

            localPathIndex++;
            previousPosition = localPosition;
            hasPreviousPosition = true;
        }

        return localPathIndex != pathBuffer.Count;
    }
#endif

    private Transform GetEffectivePathRoot()
    {
        return pathRoot != null ? pathRoot : transform;
    }

    private void SetPathPointObjectsActive(bool active, bool recordUndo)
    {
        Transform effectivePathRoot = GetEffectivePathRoot();
        if (effectivePathRoot == null)
            return;

        for (int pointIndex = 0; pointIndex < effectivePathRoot.childCount; pointIndex++)
        {
            Transform point = effectivePathRoot.GetChild(pointIndex);
            if (point == transform || transform.IsChildOf(point))
                continue;

            GameObject pointObject = point.gameObject;
            if (pointObject.activeSelf == active)
                continue;

#if UNITY_EDITOR
            if (recordUndo)
                Undo.RecordObject(pointObject, active ? "显示提示线路径点" : "隐藏提示线路径点");
#endif
            pointObject.SetActive(active);
        }
    }

    #endregion

    #region Mesh Generation

    private void EnsureMeshState()
    {
        if (generatedMesh != null)
        {
            if (IsRuntimeObject && meshFilter.sharedMesh != generatedMesh)
                meshFilter.sharedMesh = generatedMesh;

            return;
        }

        generatedMesh = new Mesh
        {
            name = $"{name}_DrawTipsRepeatLine",
            hideFlags = IsRuntimeObject
                ? HideFlags.DontSave
                : HideFlags.HideAndDontSave
        };
        generatedMesh.MarkDynamic();
        if (IsRuntimeObject)
            meshFilter.sharedMesh = generatedMesh;
    }

    private void BuildRepeatedSegmentMesh()
    {
        PrepareTextureVariantBuffers();
        vertexBuffer.Clear();
        uvBuffer.Clear();
        colorBuffer.Clear();
        triangleBuffer.Clear();

        int availablePathSegmentCount = Mathf.Max(0, pathBuffer.Count - 1);
        int segmentCount = Mathf.Min(availablePathSegmentCount, maxSegmentCount);
        int vertexCount = segmentCount * 4;
        vertexBuffer.Capacity = Mathf.Max(vertexBuffer.Capacity, vertexCount);
        uvBuffer.Capacity = Mathf.Max(uvBuffer.Capacity, vertexCount);
        colorBuffer.Capacity = Mathf.Max(colorBuffer.Capacity, vertexCount);
        triangleBuffer.Capacity = Mathf.Max(triangleBuffer.Capacity, segmentCount * 6);

        float halfWidth = lineWidth * 0.5f;
        Color32 vertexColor = Color.white;
        bool useDiscreteTextureCycles = segmentSpacing > 0f || activeTextureVariants.Count > 0;
        bool reachedSegmentLimit = useDiscreteTextureCycles
            ? BuildDiscreteRepeatedSegments(halfWidth, vertexColor)
            : BuildContinuousRepeatedSegments(halfWidth, vertexColor);

        generatedMesh.Clear();
        generatedMesh.indexFormat = vertexBuffer.Count > ushort.MaxValue
            ? IndexFormat.UInt32
            : IndexFormat.UInt16;
        generatedMesh.SetVertices(vertexBuffer);
        generatedMesh.SetUVs(0, uvBuffer);
        generatedMesh.SetColors(colorBuffer);
        if (activeTextureVariants.Count == 0)
        {
            generatedMesh.subMeshCount = 1;
            generatedMesh.SetTriangles(triangleBuffer, 0, false);
        }
        else
        {
            generatedMesh.subMeshCount = activeTextureVariants.Count;
            for (int textureIndex = 0; textureIndex < activeTextureVariants.Count; textureIndex++)
                generatedMesh.SetTriangles(textureTriangleBuffers[textureIndex], textureIndex, false);
        }

        generatedMesh.RecalculateBounds();

        if (generatedMesh.vertexCount > 0)
        {
            Bounds bounds = generatedMesh.bounds;
            bounds.Expand(new Vector3(0f, 0f, 0.01f));
            generatedMesh.bounds = bounds;
        }

        if (reachedSegmentLimit)
        {
            Debug.LogWarning(
                $"Repeat 提示线 Mesh 片段数量达到上限 {maxSegmentCount}，剩余路径未生成；请增大间距或检查 PathRoot。",
                this);
        }
    }

    private bool BuildContinuousRepeatedSegments(float halfWidth, Color32 vertexColor)
    {
        int availableSegmentCount = Mathf.Max(0, pathBuffer.Count - 1);
        float accumulatedLength = 0f;
        int generatedSegmentCount = 0;

        for (int segmentIndex = 0;
             segmentIndex < availableSegmentCount && generatedSegmentCount < maxSegmentCount;
             segmentIndex++)
        {
            Vector3 segmentStart = pathBuffer[segmentIndex];
            Vector3 segmentEnd = pathBuffer[segmentIndex + 1];
            Vector3 segmentDelta = segmentEnd - segmentStart;
            float segmentLength = segmentDelta.magnitude;
            if (segmentLength <= MinimumPositiveValue)
                continue;

            Vector3 direction = segmentDelta / segmentLength;
            Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0f) * halfWidth;
            float startU = uvOffset + accumulatedLength * repeat;
            float endU = uvOffset + (accumulatedLength + segmentLength) * repeat;
            AddSegmentQuad(
                segmentStart,
                segmentEnd,
                perpendicular,
                startU,
                endU,
                vertexColor);

            accumulatedLength += segmentLength;
            generatedSegmentCount++;
        }

        return availableSegmentCount > maxSegmentCount;
    }

    private bool BuildDiscreteRepeatedSegments(float halfWidth, Color32 vertexColor)
    {
        float baseTexturePeriodLength = 1f / repeat;
        float cycleLength = baseTexturePeriodLength + segmentSpacing;
        float phaseDistance = Mathf.Repeat(uvOffset, 1f) * cycleLength;
        float accumulatedLength = 0f;
        int generatedSegmentCount = 0;
        uint randomState = CreateRandomState(randomSeed);
        int selectedCycleIndex = -1;
        int selectedTextureIndex = -1;

        for (int segmentIndex = 0; segmentIndex < pathBuffer.Count - 1; segmentIndex++)
        {
            Vector3 segmentStart = pathBuffer[segmentIndex];
            Vector3 segmentDelta = pathBuffer[segmentIndex + 1] - segmentStart;
            float segmentLength = segmentDelta.magnitude;
            if (segmentLength <= MinimumPositiveValue)
                continue;

            Vector3 direction = segmentDelta / segmentLength;
            Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0f) * halfWidth;
            float segmentPathStart = accumulatedLength;
            float segmentPathEnd = segmentPathStart + segmentLength;
            int cycleIndex = Mathf.FloorToInt((segmentPathStart + phaseDistance) / cycleLength);
            float dashPathStart = cycleIndex * cycleLength - phaseDistance;
            float dashPathEnd = dashPathStart + baseTexturePeriodLength;

            if (dashPathEnd <= segmentPathStart + MinimumPositiveValue)
            {
                dashPathStart += cycleLength;
                dashPathEnd += cycleLength;
                cycleIndex++;
            }

            while (dashPathStart < segmentPathEnd)
            {
                float piecePathStart = Mathf.Max(segmentPathStart, dashPathStart);
                float piecePathEnd = Mathf.Min(segmentPathEnd, dashPathEnd);
                float pieceLength = piecePathEnd - piecePathStart;
                if (pieceLength > MinimumPositiveValue)
                {
                    if (generatedSegmentCount >= maxSegmentCount)
                        return true;

                    float startU = Mathf.Clamp01(
                        (piecePathStart - dashPathStart) / baseTexturePeriodLength);
                    float endU = Mathf.Clamp01(
                        (piecePathEnd - dashPathStart) / baseTexturePeriodLength);
                    Vector3 pieceStart = segmentStart + direction * (piecePathStart - segmentPathStart);
                    Vector3 pieceEnd = segmentStart + direction * (piecePathEnd - segmentPathStart);
                    int textureIndex = SelectTextureIndexForCycle(
                        cycleIndex,
                        ref randomState,
                        ref selectedCycleIndex,
                        ref selectedTextureIndex);
                    AddSegmentQuad(
                        pieceStart,
                        pieceEnd,
                        perpendicular,
                        startU,
                        endU,
                        vertexColor,
                        textureIndex);
                    generatedSegmentCount++;
                }

                dashPathStart += cycleLength;
                dashPathEnd += cycleLength;
                cycleIndex++;
            }

            accumulatedLength = segmentPathEnd;
        }

        return false;
    }

    private void PrepareTextureVariantBuffers()
    {
        activeTextureVariants.Clear();
        for (int textureIndex = 0; textureIndex < segmentTextureVariants.Count; textureIndex++)
        {
            Texture2D texture = segmentTextureVariants[textureIndex];
            if (texture == null || activeTextureVariants.Contains(texture))
                continue;

            activeTextureVariants.Add(texture);
        }

        for (int bufferIndex = 0; bufferIndex < textureTriangleBuffers.Count; bufferIndex++)
            textureTriangleBuffers[bufferIndex].Clear();

        while (textureTriangleBuffers.Count < activeTextureVariants.Count)
            textureTriangleBuffers.Add(new List<int>());
    }

    private int SelectTextureIndexForCycle(
        int cycleIndex,
        ref uint randomState,
        ref int selectedCycleIndex,
        ref int selectedTextureIndex)
    {
        int textureCount = activeTextureVariants.Count;
        if (textureCount == 0)
            return -1;

        while (selectedCycleIndex < cycleIndex)
        {
            selectedTextureIndex = SelectNextTextureIndex(
                textureCount,
                selectedTextureIndex,
                ref randomState);
            selectedCycleIndex++;
        }

        return selectedTextureIndex;
    }

    private static int SelectNextTextureIndex(
        int textureCount,
        int previousTextureIndex,
        ref uint randomState)
    {
        if (textureCount <= 1)
            return 0;

        if (previousTextureIndex < 0)
            return NextRandomIndex(textureCount, ref randomState);

        int textureIndex = NextRandomIndex(textureCount - 1, ref randomState);
        return textureIndex >= previousTextureIndex
            ? textureIndex + 1
            : textureIndex;
    }

    private static int NextRandomIndex(int exclusiveMaximum, ref uint randomState)
    {
        randomState ^= randomState << 13;
        randomState ^= randomState >> 17;
        randomState ^= randomState << 5;
        return (int)(((ulong)randomState * (uint)exclusiveMaximum) >> 32);
    }

    private static uint CreateRandomState(int seed)
    {
        uint state = unchecked((uint)seed) ^ 0x9E3779B9u;
        return state != 0u
            ? state
            : 0x6D2B79F5u;
    }

    private void AddSegmentQuad(
        Vector3 segmentStart,
        Vector3 segmentEnd,
        Vector3 perpendicular,
        float startU,
        float endU,
        Color32 vertexColor,
        int textureIndex = -1)
    {
        int firstVertex = vertexBuffer.Count;
        vertexBuffer.Add(segmentStart - perpendicular);
        vertexBuffer.Add(segmentStart + perpendicular);
        vertexBuffer.Add(segmentEnd + perpendicular);
        vertexBuffer.Add(segmentEnd - perpendicular);

        uvBuffer.Add(new Vector2(startU, 0f));
        uvBuffer.Add(new Vector2(startU, 1f));
        uvBuffer.Add(new Vector2(endU, 1f));
        uvBuffer.Add(new Vector2(endU, 0f));

        colorBuffer.Add(vertexColor);
        colorBuffer.Add(vertexColor);
        colorBuffer.Add(vertexColor);
        colorBuffer.Add(vertexColor);

        List<int> targetTriangleBuffer = textureIndex >= 0
            ? textureTriangleBuffers[textureIndex]
            : triangleBuffer;
        targetTriangleBuffer.Add(firstVertex);
        targetTriangleBuffer.Add(firstVertex + 1);
        targetTriangleBuffer.Add(firstVertex + 2);
        targetTriangleBuffer.Add(firstVertex);
        targetTriangleBuffer.Add(firstVertex + 2);
        targetTriangleBuffer.Add(firstVertex + 3);
    }

    private void DestroyGeneratedMesh()
    {
        if (generatedMesh == null)
            return;

        if (IsRuntimeObject && meshFilter != null && meshFilter.sharedMesh == generatedMesh)
            meshFilter.sharedMesh = null;

#if UNITY_EDITOR
        if (!IsRuntimeObject)
            DestroyImmediate(generatedMesh);
        else
            Destroy(generatedMesh);
#else
        Destroy(generatedMesh);
#endif
        generatedMesh = null;
    }

    #endregion

    #region Renderer Configuration

    private void CacheComponents()
    {
        meshFilter ??= GetComponent<MeshFilter>();
        meshRenderer ??= GetComponent<MeshRenderer>();
    }

    private void ConfigureMeshRenderer()
    {
        if (!IsRuntimeObject || meshRenderer == null)
            return;

        if (configuredMaterialSlotCount == 0)
        {
            Material renderMaterial = colorMaterialInstance != null
                ? colorMaterialInstance
                : tipsLineMaterial;
            if (renderMaterial != null)
                meshRenderer.sharedMaterial = renderMaterial;
        }

        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        meshRenderer.allowOcclusionWhenDynamic = false;
    }

    private void ConfigureRendererMaterialSlots()
    {
        if (!IsRuntimeObject || meshRenderer == null)
            return;

        ClearRendererTextureOverrides();

        int materialSlotCount = Mathf.Max(1, activeTextureVariants.Count);
        if (rendererMaterialSlots == null || rendererMaterialSlots.Length != materialSlotCount)
            rendererMaterialSlots = new Material[materialSlotCount];

        Material renderMaterial = colorMaterialInstance != null
            ? colorMaterialInstance
            : tipsLineMaterial;
        for (int materialIndex = 0; materialIndex < materialSlotCount; materialIndex++)
            rendererMaterialSlots[materialIndex] = renderMaterial;

        meshRenderer.sharedMaterials = rendererMaterialSlots;
        configuredMaterialSlotCount = materialSlotCount;

        if (activeTextureVariants.Count == 0)
            return;

        texturePropertyBlock ??= new MaterialPropertyBlock();
        for (int textureIndex = 0; textureIndex < activeTextureVariants.Count; textureIndex++)
        {
            texturePropertyBlock.Clear();
            texturePropertyBlock.SetTexture(BaseMapPropertyId, activeTextureVariants[textureIndex]);
            texturePropertyBlock.SetTexture(MainTexturePropertyId, activeTextureVariants[textureIndex]);
            meshRenderer.SetPropertyBlock(texturePropertyBlock, textureIndex);
        }
    }

    private void ClearRendererTextureOverrides()
    {
        if (IsRuntimeObject && meshRenderer != null)
        {
            for (int materialIndex = 0; materialIndex < configuredMaterialSlotCount; materialIndex++)
                meshRenderer.SetPropertyBlock(null, materialIndex);
        }

        configuredMaterialSlotCount = 0;
    }

    private void EnsureColorMaterialInstance()
    {
        if (tipsLineMaterial == null)
            return;

        if (colorMaterialInstance != null && colorMaterialSource == tipsLineMaterial)
        {
            SyncColorMaterialInstance();
            return;
        }

        DestroyColorMaterialInstance();
        colorMaterialInstance = new Material(tipsLineMaterial)
        {
            name = $"{name}_DrawTipsLineColor",
            hideFlags = IsRuntimeObject
                ? HideFlags.DontSave
                : HideFlags.HideAndDontSave
        };
        colorMaterialSource = tipsLineMaterial;
        ConfigureSmoothAlpha();
        SyncColorMaterialInstance();
    }

    private void ConfigureSmoothAlpha()
    {
        if (colorMaterialInstance == null)
            return;

        if (colorMaterialInstance.HasProperty(AlphaClipPropertyId))
            colorMaterialInstance.SetFloat(AlphaClipPropertyId, 0f);

        colorMaterialInstance.DisableKeyword(AlphaTestKeyword);
    }

    private void SyncColorMaterialInstance()
    {
        if (colorMaterialInstance == null)
            return;

        if (colorMaterialInstance.HasProperty(BaseColorPropertyId))
            colorMaterialInstance.SetColor(BaseColorPropertyId, dotColor);
        if (colorMaterialInstance.HasProperty(ColorPropertyId))
            colorMaterialInstance.SetColor(ColorPropertyId, dotColor);
        if (colorMaterialInstance.HasProperty(RendererColorPropertyId))
            colorMaterialInstance.SetColor(RendererColorPropertyId, dotColor);
    }

    private void DestroyColorMaterialInstance()
    {
        if (IsRuntimeObject)
            RestoreBaseRendererMaterial();
        else
        {
            configuredMaterialSlotCount = 0;
            rendererMaterialSlots = null;
        }

        if (colorMaterialInstance == null)
            return;

        Material materialToDestroy = colorMaterialInstance;
        colorMaterialInstance = null;
        colorMaterialSource = null;

#if UNITY_EDITOR
        if (!IsRuntimeObject)
            DestroyImmediate(materialToDestroy);
        else
            Destroy(materialToDestroy);
#else
        Destroy(materialToDestroy);
#endif
    }

    private void RestoreBaseRendererMaterial()
    {
        ClearRendererTextureOverrides();
        rendererMaterialSlots = null;

        if (meshRenderer == null)
            return;

        meshRenderer.sharedMaterials = new[] { tipsLineMaterial };
    }

    private void NormalizeInspectorData()
    {
        lineWidth = Mathf.Max(MinimumPositiveValue, lineWidth);
        repeat = Mathf.Max(MinimumPositiveValue, repeat);
        segmentSpacing = segmentSpacing > 0f
            ? Mathf.Max(MinimumPositiveValue, segmentSpacing)
            : 0f;
        maxSegmentCount = Mathf.Max(1, maxSegmentCount);
        segmentTextureVariants ??= new List<Texture2D>();
    }

    private void AssignTipsLineMaterialInEditor()
    {
#if UNITY_EDITOR
        Material expectedMaterial = AssetDatabase.LoadAssetAtPath<Material>(TipsLineMaterialPath);
        if (expectedMaterial == null)
        {
            Debug.LogWarning($"提示线材质不存在：{TipsLineMaterialPath}", this);
            return;
        }

        if (tipsLineMaterial != expectedMaterial)
            tipsLineMaterial = expectedMaterial;
#endif
    }

    #endregion
}
