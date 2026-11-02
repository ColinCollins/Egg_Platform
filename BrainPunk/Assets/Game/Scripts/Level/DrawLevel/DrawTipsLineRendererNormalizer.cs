using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 使用 LineRenderer Positions 作为可编辑路径，在运行时按累计长度生成互不变形的独立圆点 Mesh。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class DrawTipsLineRendererNormalizer : MonoBehaviour
{
    #region Constants

    private const string TipsLineMaterialPath = "Assets/Game/ArtSrc/Materials/DrawGame_TipsLine.mat";
    private const float MinimumPositiveValue = 0.0001f;
    private const float PositionSqrTolerance = 0.00000001f;

    #endregion

    #region Inspector

    /// <summary>所有提示点统一使用的本地 Z 值，路径输入会在生成前转换到当前节点本地空间。</summary>
    [SerializeField] private float positionZ;

    /// <summary>单个圆点在本地 XY 平面中的直径。</summary>
    [FormerlySerializedAs("lineWidth")]
    [SerializeField, Min(MinimumPositiveValue)] private float dotSize = 0.1f;

    /// <summary>相邻圆点中心沿整条路径累计弧长的固定间距，不会在折点处重新计数。</summary>
    [FormerlySerializedAs("textureRepeatLength")]
    [SerializeField, Min(MinimumPositiveValue)] private float dotSpacing = 0.23f;

    /// <summary>圆点颜色；会作为 Mesh 顶点色与固定提示线材质相乘。</summary>
    [FormerlySerializedAs("lineColor")]
    [SerializeField] private Color dotColor = Color.white;

    /// <summary>
    /// 从 23×10 原纹理中裁出 10×10 圆点像素区域，避免把两侧透明间隔压进单个圆点 Quad。
    /// </summary>
    [SerializeField] private Rect dotUvRect = new Rect(6f / 23f, 0f, 10f / 23f, 1f);

    /// <summary>规范化后，相邻路径点距离小于该值时视为重复点并移除。</summary>
    [SerializeField, Min(0f)] private float duplicatePointTolerance = 0.0001f;

    /// <summary>单条路径允许生成的最大圆点数，用于限制异常长路径或错误间距造成的移动端网格开销。</summary>
    [SerializeField, Min(1)] private int maxDotCount = 4096;

    /// <summary>固定提示线材质引用；编辑器态从约定路径自动赋值，运行时不执行资源加载。</summary>
    [SerializeField, HideInInspector] private Material tipsLineMaterial;

    #endregion

    #region Runtime State

    private LineRenderer sourceLineRenderer;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh generatedMesh;

    private List<Vector3> runtimePathPoints;
    private List<Vector3> pathBuffer;
    private List<Vector3> dotCenterBuffer;
    private List<Vector3> vertexBuffer;
    private List<Vector2> uvBuffer;
    private List<Color32> colorBuffer;
    private List<int> triangleBuffer;
    private bool useRuntimePath;

    #endregion

    #region Unity Messages

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        CacheComponents();
        NormalizeSourcePositions();
        EnsureRuntimeState();
        ConfigureSourceLinePreview();
        ConfigureMeshRenderer();
        RebuildDots();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        CacheComponents();
        if (sourceLineRenderer != null)
            sourceLineRenderer.enabled = false;

        if (meshRenderer != null)
            meshRenderer.enabled = generatedMesh != null && generatedMesh.vertexCount > 0;
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        if (sourceLineRenderer != null)
            sourceLineRenderer.enabled = false;

        if (meshRenderer != null)
            meshRenderer.enabled = false;
    }

    private void OnDestroy()
    {
        if (generatedMesh == null)
            return;

        if (meshFilter != null && meshFilter.sharedMesh == generatedMesh)
            meshFilter.sharedMesh = null;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(generatedMesh);
        else
            Destroy(generatedMesh);
#else
        Destroy(generatedMesh);
#endif
        generatedMesh = null;
    }

    private void Reset()
    {
        NormalizeInspectorData();
        AssignTipsLineMaterialInEditor();
        CacheComponents();
        NormalizeSourcePositions();
        ConfigureSourceLinePreview();
        ConfigureMeshRenderer();
    }

    private void OnValidate()
    {
        NormalizeInspectorData();
        AssignTipsLineMaterialInEditor();
        CacheComponents();
        NormalizeSourcePositions();
        ConfigureSourceLinePreview();
        ConfigureMeshRenderer();

        if (!Application.isPlaying)
            return;

        RebuildDots();
    }

    #endregion

    #region Public API

    /// <summary>
    /// 使用任意折线路径生成圆点。
    /// 调用边界：仅限运行时；输入点会被复制并转换为当前节点本地空间，调用后可安全复用原集合。
    /// </summary>
    /// <param name="pathPoints">按连接顺序排列的路径点，至少需要两个不重合点。</param>
    /// <param name="positionsAreWorldSpace">为 true 时把输入点从世界空间转换到当前节点本地空间。</param>
    public void SetPath(IReadOnlyList<Vector3> pathPoints, bool positionsAreWorldSpace = false)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Draw 提示点路径只能在运行时通过 SetPath 设置；编辑器态请修改 LineRenderer Positions。", this);
            return;
        }

        EnsureRuntimeState();
        runtimePathPoints.Clear();
        useRuntimePath = true;

        if (pathPoints != null)
        {
            float duplicateSqrTolerance = duplicatePointTolerance * duplicatePointTolerance;
            for (int index = 0; index < pathPoints.Count; index++)
            {
                Vector3 localPosition = positionsAreWorldSpace
                    ? transform.InverseTransformPoint(pathPoints[index])
                    : pathPoints[index];
                localPosition.z = positionZ;

                if (runtimePathPoints.Count > 0 &&
                    (localPosition - runtimePathPoints[runtimePathPoints.Count - 1]).sqrMagnitude <=
                    duplicateSqrTolerance)
                {
                    continue;
                }

                runtimePathPoints.Add(localPosition);
            }
        }

        RebuildDots();
    }

    /// <summary>
    /// 恢复使用当前 LineRenderer Positions 作为路径并立即重建。
    /// 调用边界：仅限运行时，适合临时动态路径结束后回到 prefab 静态路径。
    /// </summary>
    public void UseSerializedLinePath()
    {
        if (!Application.isPlaying)
            return;

        useRuntimePath = false;
        RebuildDots();
    }

    /// <summary>
    /// 按当前路径和圆点参数重新生成 Mesh。
    /// 调用边界：运行时修改路径或参数后显式调用；编辑器仅在手动调用时生成临时预览，不会在每帧或 OnValidate 中自动重建。
    /// </summary>
    [Button("按当前路径重新生成圆点")]
    public void RebuildDots()
    {
        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            RebuildEditorPreviewMesh();
#endif
            return;
        }

        CacheComponents();
        EnsureRuntimeState();
        ConfigureMeshRenderer();
        CollectCurrentPath();
        SampleDotCenters();
        BuildDotMesh();

        sourceLineRenderer.enabled = false;
        meshRenderer.enabled = isActiveAndEnabled && generatedMesh.vertexCount > 0;
    }

    [Button("按提示线规范重新应用")]
    private void ApplyStandards()
    {
        NormalizeInspectorData();
        AssignTipsLineMaterialInEditor();
        CacheComponents();
        NormalizeSourcePositions();
        ConfigureSourceLinePreview();
        ConfigureMeshRenderer();
        RebuildDots();
    }

    [Button("恢复 LineRenderer 路径编辑")]
    [EnableIf("@!UnityEngine.Application.isPlaying")]
    private void RestoreLineRendererPathEditing()
    {
        if (Application.isPlaying)
            return;

        CacheComponents();

        if (meshRenderer != null)
            meshRenderer.enabled = false;

        if (sourceLineRenderer != null)
            sourceLineRenderer.enabled = true;

#if UNITY_EDITOR
        if (generatedMesh != null)
        {
            if (meshFilter != null && meshFilter.sharedMesh == generatedMesh)
                meshFilter.sharedMesh = null;

            DestroyImmediate(generatedMesh);
            generatedMesh = null;
        }

        UnityEditor.SceneView.RepaintAll();
#endif
    }

    #endregion

    #region Path Processing

    private void NormalizeSourcePositions()
    {
        if (sourceLineRenderer == null || sourceLineRenderer.positionCount <= 0)
            return;

        int sourceCount = sourceLineRenderer.positionCount;
        Vector3[] sourcePositions = new Vector3[sourceCount];
        sourceLineRenderer.GetPositions(sourcePositions);

        int writeCount = 0;
        bool hasChanges = false;
        float duplicateSqrTolerance = duplicatePointTolerance * duplicatePointTolerance;
        Vector3 previousLocalPosition = default;

        for (int index = 0; index < sourceCount; index++)
        {
            Vector3 sourcePosition = sourcePositions[index];
            Vector3 localPosition = sourceLineRenderer.useWorldSpace
                ? transform.InverseTransformPoint(sourcePosition)
                : sourcePosition;

            if (!Mathf.Approximately(localPosition.z, positionZ))
            {
                localPosition.z = positionZ;
                hasChanges = true;
            }

            if (writeCount > 0 &&
                (localPosition - previousLocalPosition).sqrMagnitude <= duplicateSqrTolerance)
            {
                hasChanges = true;
                continue;
            }

            previousLocalPosition = localPosition;
            sourcePositions[writeCount] = sourceLineRenderer.useWorldSpace
                ? transform.TransformPoint(localPosition)
                : localPosition;
            writeCount++;
        }

        if (!hasChanges)
            return;

        sourceLineRenderer.positionCount = writeCount;
        for (int index = 0; index < writeCount; index++)
            sourceLineRenderer.SetPosition(index, sourcePositions[index]);
    }

    private void CollectCurrentPath()
    {
        pathBuffer.Clear();

        if (useRuntimePath)
        {
            pathBuffer.AddRange(runtimePathPoints);
            return;
        }

        NormalizeSourcePositions();
        int sourceCount = sourceLineRenderer.positionCount;
        if (sourceCount <= 0)
            return;

        Vector3[] sourcePositions = new Vector3[sourceCount];
        sourceLineRenderer.GetPositions(sourcePositions);

        for (int index = 0; index < sourceCount; index++)
        {
            Vector3 localPosition = sourceLineRenderer.useWorldSpace
                ? transform.InverseTransformPoint(sourcePositions[index])
                : sourcePositions[index];
            localPosition.z = positionZ;
            pathBuffer.Add(localPosition);
        }
    }

    private void SampleDotCenters()
    {
        dotCenterBuffer.Clear();
        if (pathBuffer.Count < 2)
            return;

        float distanceToNextDot = 0f;
        bool reachedLimit = false;

        for (int segmentIndex = 1; segmentIndex < pathBuffer.Count; segmentIndex++)
        {
            Vector3 segmentStart = pathBuffer[segmentIndex - 1];
            Vector3 segmentDelta = pathBuffer[segmentIndex] - segmentStart;
            float segmentLength = segmentDelta.magnitude;
            if (segmentLength <= MinimumPositiveValue)
                continue;

            Vector3 segmentDirection = segmentDelta / segmentLength;
            while (distanceToNextDot <= segmentLength + MinimumPositiveValue)
            {
                if (dotCenterBuffer.Count >= maxDotCount)
                {
                    reachedLimit = true;
                    break;
                }

                float sampleDistance = Mathf.Min(distanceToNextDot, segmentLength);
                Vector3 dotCenter = segmentStart + segmentDirection * sampleDistance;
                if (dotCenterBuffer.Count == 0 ||
                    (dotCenter - dotCenterBuffer[dotCenterBuffer.Count - 1]).sqrMagnitude >
                    PositionSqrTolerance)
                {
                    dotCenterBuffer.Add(dotCenter);
                }

                distanceToNextDot += dotSpacing;
            }

            if (reachedLimit)
                break;

            distanceToNextDot -= segmentLength;
        }

        if (reachedLimit)
        {
            Debug.LogWarning(
                $"Draw 提示点数量达到上限 {maxDotCount}，剩余路径未生成；请增大间距或确认路径长度。",
                this);
        }
    }

    #endregion

    #region Mesh Generation

#if UNITY_EDITOR
    private void RebuildEditorPreviewMesh()
    {
        if (Application.isPlaying)
            return;

        NormalizeInspectorData();
        AssignTipsLineMaterialInEditor();
        CacheComponents();
        NormalizeSourcePositions();
        ConfigureSourceLinePreview();
        ConfigureMeshRenderer();
        useRuntimePath = false;
        EnsureRuntimeState();
        CollectCurrentPath();
        SampleDotCenters();
        BuildDotMesh();

        sourceLineRenderer.enabled = false;
        meshRenderer.enabled = generatedMesh.vertexCount > 0;
        UnityEditor.SceneView.RepaintAll();
    }
#endif

    private void EnsureRuntimeState()
    {
        runtimePathPoints ??= new List<Vector3>();
        pathBuffer ??= new List<Vector3>();
        dotCenterBuffer ??= new List<Vector3>();
        vertexBuffer ??= new List<Vector3>();
        uvBuffer ??= new List<Vector2>();
        colorBuffer ??= new List<Color32>();
        triangleBuffer ??= new List<int>();

        if (generatedMesh != null)
        {
            if (meshFilter.sharedMesh != generatedMesh)
                meshFilter.sharedMesh = generatedMesh;

            return;
        }

        generatedMesh = new Mesh
        {
            name = $"{name}_DrawTipsDots",
            hideFlags = Application.isPlaying
                ? HideFlags.DontSave
                : HideFlags.HideAndDontSave
        };
        generatedMesh.MarkDynamic();
        meshFilter.sharedMesh = generatedMesh;
    }

    private void BuildDotMesh()
    {
        vertexBuffer.Clear();
        uvBuffer.Clear();
        colorBuffer.Clear();
        triangleBuffer.Clear();

        int dotCount = dotCenterBuffer.Count;
        int vertexCount = dotCount * 4;
        vertexBuffer.Capacity = Mathf.Max(vertexBuffer.Capacity, vertexCount);
        uvBuffer.Capacity = Mathf.Max(uvBuffer.Capacity, vertexCount);
        colorBuffer.Capacity = Mathf.Max(colorBuffer.Capacity, vertexCount);
        triangleBuffer.Capacity = Mathf.Max(triangleBuffer.Capacity, dotCount * 6);

        float halfSize = dotSize * 0.5f;
        Vector2 uvBottomLeft = new Vector2(dotUvRect.xMin, dotUvRect.yMin);
        Vector2 uvTopLeft = new Vector2(dotUvRect.xMin, dotUvRect.yMax);
        Vector2 uvTopRight = new Vector2(dotUvRect.xMax, dotUvRect.yMax);
        Vector2 uvBottomRight = new Vector2(dotUvRect.xMax, dotUvRect.yMin);
        Color32 vertexColor = dotColor;

        for (int dotIndex = 0; dotIndex < dotCount; dotIndex++)
        {
            Vector3 center = dotCenterBuffer[dotIndex];
            int firstVertex = vertexBuffer.Count;

            vertexBuffer.Add(center + new Vector3(-halfSize, -halfSize, 0f));
            vertexBuffer.Add(center + new Vector3(-halfSize, halfSize, 0f));
            vertexBuffer.Add(center + new Vector3(halfSize, halfSize, 0f));
            vertexBuffer.Add(center + new Vector3(halfSize, -halfSize, 0f));

            uvBuffer.Add(uvBottomLeft);
            uvBuffer.Add(uvTopLeft);
            uvBuffer.Add(uvTopRight);
            uvBuffer.Add(uvBottomRight);

            colorBuffer.Add(vertexColor);
            colorBuffer.Add(vertexColor);
            colorBuffer.Add(vertexColor);
            colorBuffer.Add(vertexColor);

            triangleBuffer.Add(firstVertex);
            triangleBuffer.Add(firstVertex + 1);
            triangleBuffer.Add(firstVertex + 2);
            triangleBuffer.Add(firstVertex);
            triangleBuffer.Add(firstVertex + 2);
            triangleBuffer.Add(firstVertex + 3);
        }

        generatedMesh.Clear();
        generatedMesh.indexFormat = vertexCount > ushort.MaxValue
            ? IndexFormat.UInt32
            : IndexFormat.UInt16;
        generatedMesh.SetVertices(vertexBuffer);
        generatedMesh.SetUVs(0, uvBuffer);
        generatedMesh.SetColors(colorBuffer);
        generatedMesh.SetTriangles(triangleBuffer, 0, true);
        generatedMesh.RecalculateBounds();

        Bounds bounds = generatedMesh.bounds;
        bounds.Expand(new Vector3(0f, 0f, 0.01f));
        generatedMesh.bounds = bounds;
    }

    #endregion

    #region Renderer Configuration

    private void CacheComponents()
    {
        sourceLineRenderer ??= GetComponent<LineRenderer>();
        meshFilter ??= GetComponent<MeshFilter>();
        meshRenderer ??= GetComponent<MeshRenderer>();
    }

    private void ConfigureSourceLinePreview()
    {
        if (sourceLineRenderer == null)
            return;

        if (tipsLineMaterial != null)
            sourceLineRenderer.sharedMaterial = tipsLineMaterial;

        sourceLineRenderer.widthMultiplier = dotSize;
        sourceLineRenderer.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
        sourceLineRenderer.startColor = dotColor;
        sourceLineRenderer.endColor = dotColor;
        sourceLineRenderer.textureMode = LineTextureMode.Tile;
        sourceLineRenderer.textureScale = new Vector2(1f / dotSpacing, 1f);
        sourceLineRenderer.alignment = LineAlignment.TransformZ;
        sourceLineRenderer.numCornerVertices = 0;
        sourceLineRenderer.numCapVertices = 0;
        sourceLineRenderer.loop = false;
        sourceLineRenderer.generateLightingData = false;
        sourceLineRenderer.shadowCastingMode = ShadowCastingMode.Off;
        sourceLineRenderer.receiveShadows = false;
        sourceLineRenderer.lightProbeUsage = LightProbeUsage.Off;
        sourceLineRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        sourceLineRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        sourceLineRenderer.allowOcclusionWhenDynamic = false;
    }

    private void ConfigureMeshRenderer()
    {
        if (sourceLineRenderer == null || meshRenderer == null)
            return;

        if (tipsLineMaterial != null)
            meshRenderer.sharedMaterial = tipsLineMaterial;

        meshRenderer.sortingLayerID = sourceLineRenderer.sortingLayerID;
        meshRenderer.sortingOrder = sourceLineRenderer.sortingOrder;
        meshRenderer.renderingLayerMask = sourceLineRenderer.renderingLayerMask;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        meshRenderer.allowOcclusionWhenDynamic = false;
    }

    private void NormalizeInspectorData()
    {
        dotSize = Mathf.Max(MinimumPositiveValue, dotSize);
        dotSpacing = Mathf.Max(MinimumPositiveValue, dotSpacing);
        duplicatePointTolerance = Mathf.Max(0f, duplicatePointTolerance);
        maxDotCount = Mathf.Max(1, maxDotCount);

        float xMin = Mathf.Clamp(dotUvRect.xMin, 0f, 1f - MinimumPositiveValue);
        float yMin = Mathf.Clamp(dotUvRect.yMin, 0f, 1f - MinimumPositiveValue);
        float xMax = Mathf.Clamp(dotUvRect.xMax, xMin + MinimumPositiveValue, 1f);
        float yMax = Mathf.Clamp(dotUvRect.yMax, yMin + MinimumPositiveValue, 1f);
        dotUvRect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
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
