using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 使用世界空间切线裁剪 Sprite 网格，生成两个临时网格。
/// 不修改源 Renderer；源对象的隐藏和状态切换由调用方负责。
/// </summary>
public sealed class Level81SliceMesh : MonoBehaviour
{
    private const float MinimumCutLengthSqr = 0.000001f;
    private const float DistanceEpsilon = 0.0001f;
    private const float MinimumPolygonArea = 0.000001f;
    private const double VertexQuantization = 100000.0;

    private static readonly int MainTexturePropertyId = Shader.PropertyToID("_MainTex");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int RendererColorPropertyId = Shader.PropertyToID("_RendererColor");
    private static readonly int SceneLayerId = SortingLayer.NameToID("SceneLayer");

    private MeshRenderer _meshRenderer;
    private Mesh _ownedMesh;
    private Material _ownedMaterial;
    private Sprite _sourceSprite;
    private Color _baseColor = Color.white;
    private Color _effectColor = Color.white;
    private float _fadeDelay;
    private float _fadeDuration;
    private float _elapsed;
    private bool _useUnscaledFadeTime;
    private bool _initialized;
    private bool _assetsReleased;
    private bool _simulationPaused;
    private Vector2 _pausedVelocity;
    private float _pausedAngularVelocity;
    private int _remainingReslices;

    #region Public API

    /// <summary>
    /// 显式 Sprite/Transform 重载使用的渲染数据。
    /// </summary>
    public struct SliceRenderInfo
    {
        public Color Color;
        public Material SourceMaterial;
        public int SortingLayerId;
        public int SortingOrder;
        public int GameObjectLayer;
        public bool FlipX;
        public bool FlipY;

        public static SliceRenderInfo From(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return new SliceRenderInfo
                {
                    Color = Color.white,
                    GameObjectLayer = 0
                };
            }

            return new SliceRenderInfo
            {
                Color = renderer.color,
                SourceMaterial = renderer.sharedMaterial,
                // 生成的半片属于玩法美术，不应继承已移除的 LevelItem 图层。
                SortingLayerId = SceneLayerId,
                SortingOrder = renderer.sortingOrder,
                GameObjectLayer = renderer.gameObject.layer,
                FlipX = renderer.flipX,
                FlipY = renderer.flipY
            };
        }
    }

    /// <summary>
    /// 两个生成半片共用的物理和生命周期设置。
    /// </summary>
    public struct SliceSettings
    {
        public bool AddRigidbody;
        public bool AddPolygonCollider;
        public bool ColliderIsTrigger;
        public Vector2 BaseVelocity;
        public float BaseAngularVelocity;
        public float SeparationSpeed;
        public float DownwardSpeed;
        public float AngularSeparationSpeed;
        public float GravityScale;
        public float Mass;
        public float LinearDamping;
        public float AngularDamping;
        public float FadeDelay;
        public float FadeDuration;
        public bool UseUnscaledFadeTime;
        public float MinimumAreaRatio;
        public int RemainingReslices;
        public Color EffectColor;
        public int SortingOrderOffset;
        public string NamePrefix;

        public static SliceSettings Default
        {
            get
            {
                return new SliceSettings
                {
                    AddRigidbody = true,
                    AddPolygonCollider = true,
                    ColliderIsTrigger = false,
                    BaseVelocity = Vector2.zero,
                    BaseAngularVelocity = 0f,
                    SeparationSpeed = 1.25f,
                    DownwardSpeed = 0.35f,
                    AngularSeparationSpeed = 110f,
                    GravityScale = 1.2f,
                    Mass = 0.2f,
                    LinearDamping = 0.05f,
                    AngularDamping = 0.1f,
                    FadeDelay = 1.25f,
                    FadeDuration = 0.45f,
                    UseUnscaledFadeTime = false,
                    MinimumAreaRatio = 0f,
                    RemainingReslices = 0,
                    EffectColor = Color.white,
                    SortingOrderOffset = 1,
                    NamePrefix = "Level81Slice"
                };
            }
        }
    }

    /// <summary>
    /// 管理两个生成对象，确保 Reset/Replay 时可以立即清理。
    /// </summary>
    public sealed class SliceResult : IDisposable
    {
        private GameObject _positiveHalf;
        private GameObject _negativeHalf;

        internal SliceResult(
            GameObject positiveHalf,
            GameObject negativeHalf,
            Vector2 cutWorldPosition)
        {
            _positiveHalf = positiveHalf;
            _negativeHalf = negativeHalf;
            CutWorldPosition = cutWorldPosition;
        }

        public GameObject PositiveHalf => _positiveHalf;
        public GameObject NegativeHalf => _negativeHalf;
        public Vector2 CutWorldPosition { get; }
        public bool IsAlive => _positiveHalf != null || _negativeHalf != null;

        public bool Contains(GameObject half)
        {
            return half != null &&
                   (ReferenceEquals(_positiveHalf, half) || ReferenceEquals(_negativeHalf, half));
        }

        public bool DetachHalf(GameObject half)
        {
            if (half == null)
                return false;

            if (ReferenceEquals(_positiveHalf, half))
            {
                _positiveHalf = null;
                return true;
            }

            if (ReferenceEquals(_negativeHalf, half))
            {
                _negativeHalf = null;
                return true;
            }

            return false;
        }

        public void SetPaused(bool paused)
        {
            SetHalfPaused(_positiveHalf, paused);
            SetHalfPaused(_negativeHalf, paused);
        }

        public void Dispose()
        {
            DestroyRuntimeObject(_positiveHalf);
            DestroyRuntimeObject(_negativeHalf);
            _positiveHalf = null;
            _negativeHalf = null;
        }

        private static void SetHalfPaused(GameObject half, bool paused)
        {
            if (half == null)
                return;

            Level81SliceMesh lifetime = half.GetComponent<Level81SliceMesh>();
            if (lifetime != null)
                lifetime.SetSimulationPaused(paused);
        }
    }

    public bool CanReslice => _initialized &&
                              !_assetsReleased &&
                              _remainingReslices > 0 &&
                              _ownedMesh != null &&
                              _sourceSprite != null;

    public Color EffectColor => _effectColor;

    public Bounds RenderBounds => _meshRenderer != null
        ? _meshRenderer.bounds
        : new Bounds(transform.position, Vector3.zero);

    /// <summary>
    /// 从 SpriteRenderer 读取 Sprite、Transform、颜色和排序等完整源数据。
    /// </summary>
    public static bool TrySlice(
        SpriteRenderer sourceRenderer,
        Vector2 worldCutStart,
        Vector2 worldCutEnd,
        SliceSettings settings,
        out SliceResult result)
    {
        result = null;
        if (sourceRenderer == null || sourceRenderer.sprite == null)
        {
            return false;
        }

        if (sourceRenderer.drawMode != SpriteDrawMode.Simple)
        {
            Debug.LogWarning("[Level81SliceMesh] Only SpriteDrawMode.Simple is supported.", sourceRenderer);
            return false;
        }

        return TrySlice(
            sourceRenderer.sprite,
            sourceRenderer.transform,
            worldCutStart,
            worldCutEnd,
            SliceRenderInfo.From(sourceRenderer),
            settings,
            out result);
    }

    /// <summary>
    /// 使用默认物理和渐隐设置。
    /// </summary>
    public static bool TrySlice(
        SpriteRenderer sourceRenderer,
        Vector2 worldCutStart,
        Vector2 worldCutEnd,
        out SliceResult result)
    {
        return TrySlice(sourceRenderer, worldCutStart, worldCutEnd, SliceSettings.Default, out result);
    }

    /// <summary>
    /// 为 Sprite 与世界 Transform 分开保存的调用方提供显式重载。
    /// </summary>
    public static bool TrySlice(
        Sprite sprite,
        Transform sourceTransform,
        Vector2 worldCutStart,
        Vector2 worldCutEnd,
        SliceRenderInfo renderInfo,
        SliceSettings settings,
        out SliceResult result)
    {
        result = null;
        if (sprite == null || sourceTransform == null)
            return false;

        Vector2[] spriteVertices = sprite.vertices;
        Vector2[] spriteUvs = sprite.uv;
        ushort[] spriteTriangles = sprite.triangles;
        if (spriteVertices == null || spriteUvs == null || spriteTriangles == null ||
            spriteVertices.Length < 3 || spriteVertices.Length != spriteUvs.Length ||
            spriteTriangles.Length < 3)
            return false;

        Vector3[] vertices = new Vector3[spriteVertices.Length];
        for (int index = 0; index < spriteVertices.Length; index++)
        {
            Vector3 vertex = spriteVertices[index];
            if (renderInfo.FlipX)
                vertex.x = -vertex.x;
            if (renderInfo.FlipY)
                vertex.y = -vertex.y;
            vertices[index] = vertex;
        }

        int[] triangles = new int[spriteTriangles.Length];
        for (int index = 0; index < spriteTriangles.Length; index++)
            triangles[index] = spriteTriangles[index];

        renderInfo.FlipX = false;
        renderInfo.FlipY = false;
        return TrySliceGeometry(
            sprite,
            vertices,
            spriteUvs,
            triangles,
            sourceTransform,
            worldCutStart,
            worldCutEnd,
            renderInfo,
            settings,
            out result);
    }

    /// <summary>
    /// 再次切割一个已生成的水果半片。使用当前运行时网格作为源，
    /// 避免已移除的几何区域重新出现。
    /// </summary>
    public bool TryReslice(
        Vector2 worldCutStart,
        Vector2 worldCutEnd,
        SliceSettings settings,
        out SliceResult result)
    {
        result = null;
        if (!CanReslice || _meshRenderer == null)
            return false;

        Vector3[] vertices = _ownedMesh.vertices;
        Vector2[] uvs = _ownedMesh.uv;
        int[] triangles = _ownedMesh.triangles;
        if (vertices == null || uvs == null || triangles == null ||
            vertices.Length < 3 || vertices.Length != uvs.Length || triangles.Length < 3)
            return false;

        Color currentColor = _baseColor;
        if (_ownedMaterial != null)
        {
            if (_ownedMaterial.HasProperty(RendererColorPropertyId))
                currentColor = _ownedMaterial.GetColor(RendererColorPropertyId);
            else if (_ownedMaterial.HasProperty(ColorPropertyId))
                currentColor = _ownedMaterial.GetColor(ColorPropertyId);
        }

        SliceRenderInfo renderInfo = new SliceRenderInfo
        {
            Color = currentColor,
            SourceMaterial = _ownedMaterial,
            SortingLayerId = _meshRenderer.sortingLayerID,
            SortingOrder = _meshRenderer.sortingOrder,
            GameObjectLayer = gameObject.layer,
            FlipX = false,
            FlipY = false
        };
        settings.RemainingReslices = Mathf.Max(0, _remainingReslices - 1);
        settings.EffectColor = _effectColor;
        return TrySliceGeometry(
            _sourceSprite,
            vertices,
            uvs,
            triangles,
            transform,
            worldCutStart,
            worldCutEnd,
            renderInfo,
            settings,
            out result);
    }

    public void ConsumeAfterReslice()
    {
        if (_meshRenderer != null)
            _meshRenderer.enabled = false;

        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body != null)
            body.simulated = false;

        Collider2D collider = GetComponent<Collider2D>();
        if (collider != null)
            collider.enabled = false;
        DestroyRuntimeObject(gameObject);
    }

    private static bool TrySliceGeometry(
        Sprite sourceSprite,
        Vector3[] vertices,
        Vector2[] uvs,
        int[] triangles,
        Transform sourceTransform,
        Vector2 worldCutStart,
        Vector2 worldCutEnd,
        SliceRenderInfo renderInfo,
        SliceSettings settings,
        out SliceResult result)
    {
        result = null;
        if (sourceSprite == null || sourceTransform == null ||
            vertices == null || uvs == null || triangles == null ||
            vertices.Length < 3 || vertices.Length != uvs.Length || triangles.Length < 3)
        {
            return false;
        }

        Vector2 cutVector = worldCutEnd - worldCutStart;
        if (cutVector.sqrMagnitude <= MinimumCutLengthSqr)
            return false;

        Vector2 cutDirection = cutVector.normalized;
        Vector2 separationNormal = new Vector2(-cutDirection.y, cutDirection.x);
        float minimumProjection = float.PositiveInfinity;
        float maximumProjection = float.NegativeInfinity;
        Vector2 worldCenter = Vector2.zero;
        for (int index = 0; index < vertices.Length; index++)
        {
            Vector2 worldVertex = sourceTransform.TransformPoint(vertices[index]);
            float projection = Vector2.Dot(separationNormal, worldVertex);
            minimumProjection = Mathf.Min(minimumProjection, projection);
            maximumProjection = Mathf.Max(maximumProjection, projection);
            worldCenter += worldVertex;
        }
        worldCenter /= vertices.Length;

        float minimumAreaRatio = Mathf.Clamp(settings.MinimumAreaRatio, 0f, 0.45f);
        float requestedProjection = Vector2.Dot(separationNormal, worldCutStart);
        float minimumCutProjection = Mathf.Lerp(
            minimumProjection,
            maximumProjection,
            minimumAreaRatio);
        float maximumCutProjection = Mathf.Lerp(
            maximumProjection,
            minimumProjection,
            minimumAreaRatio);
        float effectiveProjection = Mathf.Clamp(
            requestedProjection,
            minimumCutProjection,
            maximumCutProjection);
        ShiftCutLine(effectiveProjection, ref worldCutStart, ref worldCutEnd);

        BuildGeometry(
            worldCutStart,
            out HalfGeometry positiveGeometry,
            out HalfGeometry negativeGeometry);
        if (!HasValidArea(positiveGeometry, negativeGeometry, minimumAreaRatio))
        {
            effectiveProjection = (minimumProjection + maximumProjection) * 0.5f;
            ShiftCutLine(effectiveProjection, ref worldCutStart, ref worldCutEnd);
            BuildGeometry(worldCutStart, out positiveGeometry, out negativeGeometry);
            if (!HasValidArea(positiveGeometry, negativeGeometry, minimumAreaRatio))
                return false;
        }

        GameObject positiveHalf = null;
        GameObject negativeHalf = null;
        try
        {
            positiveHalf = CreateHalfObject(
                sourceSprite,
                sourceTransform,
                positiveGeometry,
                renderInfo,
                settings,
                separationNormal,
                1f,
                "Positive");
            if (positiveHalf == null)
            {
                return false;
            }

            negativeHalf = CreateHalfObject(
                sourceSprite,
                sourceTransform,
                negativeGeometry,
                renderInfo,
                settings,
                separationNormal,
                -1f,
                "Negative");
            if (negativeHalf == null)
            {
                DestroyRuntimeObject(positiveHalf);
                return false;
            }

            Vector2 cutWorldPosition = worldCenter - separationNormal *
                (Vector2.Dot(separationNormal, worldCenter) - effectiveProjection);
            result = new SliceResult(positiveHalf, negativeHalf, cutWorldPosition);
            return true;
        }
        catch (Exception exception)
        {
            DestroyRuntimeObject(positiveHalf);
            DestroyRuntimeObject(negativeHalf);
            Debug.LogException(exception);
            return false;
        }

        void ShiftCutLine(
            float targetProjection,
            ref Vector2 cutStart,
            ref Vector2 cutEnd)
        {
            float currentProjection = Vector2.Dot(separationNormal, cutStart);
            Vector2 offset = separationNormal * (targetProjection - currentProjection);
            cutStart += offset;
            cutEnd += offset;
        }

        void BuildGeometry(
            Vector2 effectiveCutStart,
            out HalfGeometry positive,
            out HalfGeometry negative)
        {
            positive = new HalfGeometry();
            negative = new HalfGeometry();
            for (int triangleIndex = 0; triangleIndex + 2 < triangles.Length; triangleIndex += 3)
            {
                int index0 = triangles[triangleIndex];
                int index1 = triangles[triangleIndex + 1];
                int index2 = triangles[triangleIndex + 2];
                if (index0 < 0 || index1 < 0 || index2 < 0 ||
                    index0 >= vertices.Length || index1 >= vertices.Length ||
                    index2 >= vertices.Length)
                {
                    continue;
                }

                List<ClipVertex> triangle = new List<ClipVertex>(3)
                {
                    CreateClipVertex(index0, effectiveCutStart),
                    CreateClipVertex(index1, effectiveCutStart),
                    CreateClipVertex(index2, effectiveCutStart)
                };
                positive.AddPolygon(ClipToHalfPlane(triangle, true));
                negative.AddPolygon(ClipToHalfPlane(triangle, false));
            }
        }

        ClipVertex CreateClipVertex(int vertexIndex, Vector2 effectiveCutStart)
        {
            Vector3 localPosition = vertices[vertexIndex];
            Vector3 worldPosition = sourceTransform.TransformPoint(localPosition);
            Vector2 offsetFromLine = (Vector2)worldPosition - effectiveCutStart;
            float signedDistance = Cross(cutDirection, offsetFromLine);
            return new ClipVertex(localPosition, uvs[vertexIndex], signedDistance);
        }
    }

    private static bool HasValidArea(
        HalfGeometry positiveGeometry,
        HalfGeometry negativeGeometry,
        float minimumAreaRatio)
    {
        if (positiveGeometry == null || negativeGeometry == null ||
            !positiveGeometry.HasArea || !negativeGeometry.HasArea)
        {
            return false;
        }

        float totalArea = positiveGeometry.Area + negativeGeometry.Area;
        return totalArea > MinimumPolygonArea &&
               Mathf.Min(positiveGeometry.Area, negativeGeometry.Area) / totalArea +
               Mathf.Epsilon >= minimumAreaRatio;
    }

    #endregion

    #region Geometry

    private static List<ClipVertex> ClipToHalfPlane(List<ClipVertex> input, bool keepPositive)
    {
        List<ClipVertex> output = new List<ClipVertex>(4);
        if (input == null || input.Count == 0)
        {
            return output;
        }

        ClipVertex previous = input[input.Count - 1];
        bool previousInside = IsInside(previous.SignedDistance, keepPositive);

        for (int index = 0; index < input.Count; index++)
        {
            ClipVertex current = input[index];
            bool currentInside = IsInside(current.SignedDistance, keepPositive);

            if (previousInside != currentInside)
            {
                float denominator = previous.SignedDistance - current.SignedDistance;
                if (Mathf.Abs(denominator) > Mathf.Epsilon)
                {
                    float interpolation = Mathf.Clamp01(previous.SignedDistance / denominator);
                    output.Add(ClipVertex.Lerp(previous, current, interpolation));
                }
            }

            if (currentInside)
            {
                output.Add(current);
            }

            previous = current;
            previousInside = currentInside;
        }

        RemoveSequentialDuplicates(output);
        return output;
    }

    private static bool IsInside(float signedDistance, bool keepPositive)
    {
        return keepPositive
            ? signedDistance >= -DistanceEpsilon
            : signedDistance <= DistanceEpsilon;
    }

    private static void RemoveSequentialDuplicates(List<ClipVertex> vertices)
    {
        if (vertices == null || vertices.Count < 2)
        {
            return;
        }

        for (int index = vertices.Count - 1; index > 0; index--)
        {
            if (ApproximatelySamePosition(vertices[index].LocalPosition, vertices[index - 1].LocalPosition))
            {
                vertices.RemoveAt(index);
            }
        }

        if (vertices.Count > 1 &&
            ApproximatelySamePosition(vertices[0].LocalPosition, vertices[vertices.Count - 1].LocalPosition))
        {
            vertices.RemoveAt(vertices.Count - 1);
        }
    }

    private static bool ApproximatelySamePosition(Vector3 first, Vector3 second)
    {
        return (first - second).sqrMagnitude <= 0.0000000001f;
    }

    private static float Cross(Vector2 first, Vector2 second)
    {
        return first.x * second.y - first.y * second.x;
    }

    private static float SignedArea(IReadOnlyList<Vector2> points)
    {
        if (points == null || points.Count < 3)
        {
            return 0f;
        }

        float twiceArea = 0f;
        for (int index = 0; index < points.Count; index++)
        {
            Vector2 current = points[index];
            Vector2 next = points[(index + 1) % points.Count];
            twiceArea += Cross(current, next);
        }

        return twiceArea * 0.5f;
    }

    private static long Quantize(float value)
    {
        return (long)Math.Round(value * VertexQuantization);
    }

    #endregion

    #region Object Creation

    private static GameObject CreateHalfObject(
        Sprite sprite,
        Transform sourceTransform,
        HalfGeometry geometry,
        SliceRenderInfo renderInfo,
        SliceSettings settings,
        Vector2 separationNormal,
        float sideSign,
        string sideName)
    {
        Mesh mesh = geometry.CreateMesh(sprite.name + "_" + sideName + "Mesh");
        if (mesh == null)
        {
            return null;
        }

        Material material = CreateRuntimeMaterial(renderInfo.SourceMaterial, sprite, renderInfo.Color);
        if (material == null)
        {
            DestroyRuntimeObject(mesh);
            return null;
        }

        string namePrefix = string.IsNullOrWhiteSpace(settings.NamePrefix)
            ? "Level81Slice"
            : settings.NamePrefix;
        GameObject halfObject = new GameObject(namePrefix + "_" + sideName);
        halfObject.layer = Mathf.Clamp(renderInfo.GameObjectLayer, 0, 31);

        Transform halfTransform = halfObject.transform;
        halfTransform.SetParent(sourceTransform.parent, false);
        halfTransform.localPosition = sourceTransform.localPosition;
        halfTransform.localRotation = sourceTransform.localRotation;
        halfTransform.localScale = sourceTransform.localScale;

        MeshFilter meshFilter = halfObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        MeshRenderer meshRenderer = halfObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.sortingLayerID = renderInfo.SortingLayerId;
        meshRenderer.sortingOrder = renderInfo.SortingOrder + settings.SortingOrderOffset;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        meshRenderer.allowOcclusionWhenDynamic = false;

        PolygonCollider2D polygonCollider = null;
        if (settings.AddPolygonCollider)
        {
            List<Vector2[]> paths = geometry.BuildColliderPaths();
            if (paths.Count > 0)
            {
                polygonCollider = halfObject.AddComponent<PolygonCollider2D>();
                polygonCollider.isTrigger = settings.ColliderIsTrigger;
                polygonCollider.pathCount = paths.Count;
                for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
                {
                    polygonCollider.SetPath(pathIndex, paths[pathIndex]);
                }
            }
        }

        if (settings.AddRigidbody)
        {
            Rigidbody2D body = halfObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = Mathf.Max(0f, settings.GravityScale);
            body.mass = Mathf.Max(0.001f, settings.Mass);
            body.linearDamping = Mathf.Max(0f, settings.LinearDamping);
            body.angularDamping = Mathf.Max(0f, settings.AngularDamping);
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.useAutoMass = false;

            float separationSpeed = Mathf.Max(0f, settings.SeparationSpeed);
            float downwardSpeed = Mathf.Max(0f, settings.DownwardSpeed);
            body.linearVelocity = settings.BaseVelocity +
                                  separationNormal * (separationSpeed * sideSign) +
                                  Vector2.down * downwardSpeed;
            body.angularVelocity = settings.BaseAngularVelocity +
                                   Mathf.Abs(settings.AngularSeparationSpeed) * sideSign;
        }

        Level81SliceMesh lifetime = halfObject.AddComponent<Level81SliceMesh>();
        lifetime.Initialize(
            meshRenderer,
            mesh,
            material,
            sprite,
            renderInfo.Color,
            settings.EffectColor,
            settings.FadeDelay,
            settings.FadeDuration,
            settings.UseUnscaledFadeTime,
            settings.RemainingReslices);

        return halfObject;
    }

    private static Material CreateRuntimeMaterial(
        Material sourceMaterial,
        Sprite sprite,
        Color color)
    {
        Shader fallbackShader = sourceMaterial == null ? Shader.Find("Sprites/Default") : null;
        if (sourceMaterial == null && fallbackShader == null)
        {
            Debug.LogWarning("[Level81SliceMesh] No source material or Sprites/Default shader was found.");
            return null;
        }

        Material material = sourceMaterial != null
            ? new Material(sourceMaterial)
            : new Material(fallbackShader);
        material.name = sprite.name + "_SliceMaterial";
        material.hideFlags = HideFlags.HideAndDontSave;

        if (material.HasProperty(MainTexturePropertyId))
        {
            material.SetTexture(MainTexturePropertyId, sprite.texture);
        }

        ApplyMaterialColor(material, color);
        return material;
    }

    private static void ApplyMaterialColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty(ColorPropertyId))
        {
            material.SetColor(ColorPropertyId, color);
        }

        if (material.HasProperty(RendererColorPropertyId))
        {
            material.SetColor(RendererColorPropertyId, color);
        }
    }

    #endregion

    #region Lifetime

    private void Initialize(
        MeshRenderer meshRenderer,
        Mesh ownedMesh,
        Material ownedMaterial,
        Sprite sourceSprite,
        Color baseColor,
        Color effectColor,
        float fadeDelay,
        float fadeDuration,
        bool useUnscaledFadeTime,
        int remainingReslices)
    {
        _meshRenderer = meshRenderer;
        _ownedMesh = ownedMesh;
        _ownedMaterial = ownedMaterial;
        _sourceSprite = sourceSprite;
        _baseColor = baseColor;
        _effectColor = effectColor;
        _fadeDelay = Mathf.Max(0f, fadeDelay);
        _fadeDuration = Mathf.Max(0f, fadeDuration);
        _useUnscaledFadeTime = useUnscaledFadeTime;
        _elapsed = 0f;
        _initialized = true;
        _assetsReleased = false;
        _simulationPaused = false;
        _remainingReslices = Mathf.Max(0, remainingReslices);
    }

    private void Update()
    {
        if (!_initialized || _simulationPaused)
        {
            return;
        }

        _elapsed += _useUnscaledFadeTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (_elapsed < _fadeDelay)
        {
            return;
        }

        if (_fadeDuration <= Mathf.Epsilon)
        {
            DestroyRuntimeObject(gameObject);
            return;
        }

        float fadeProgress = Mathf.Clamp01((_elapsed - _fadeDelay) / _fadeDuration);
        Color fadedColor = _baseColor;
        fadedColor.a *= 1f - fadeProgress;
        ApplyMaterialColor(_ownedMaterial, fadedColor);

        if (fadeProgress >= 1f)
        {
            DestroyRuntimeObject(gameObject);
        }
    }

    private void SetSimulationPaused(bool paused)
    {
        if (_simulationPaused == paused)
            return;

        _simulationPaused = paused;
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        if (body == null)
            return;

        if (paused)
        {
            _pausedVelocity = body.linearVelocity;
            _pausedAngularVelocity = body.angularVelocity;
            body.simulated = false;
        }
        else
        {
            body.simulated = true;
            body.linearVelocity = _pausedVelocity;
            body.angularVelocity = _pausedAngularVelocity;
        }
    }

    private void OnDestroy()
    {
        ReleaseOwnedAssets();
    }

    private void ReleaseOwnedAssets()
    {
        if (_assetsReleased)
        {
            return;
        }

        _assetsReleased = true;
        if (_meshRenderer != null && _meshRenderer.sharedMaterial == _ownedMaterial)
        {
            _meshRenderer.sharedMaterial = null;
        }

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh == _ownedMesh)
        {
            meshFilter.sharedMesh = null;
        }

        DestroyRuntimeObject(_ownedMaterial);
        DestroyRuntimeObject(_ownedMesh);
        _ownedMaterial = null;
        _ownedMesh = null;
        _sourceSprite = null;
    }

    private static void DestroyRuntimeObject(UnityEngine.Object target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(target);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    #endregion

    #region Internal Geometry Types

    private struct ClipVertex
    {
        public readonly Vector3 LocalPosition;
        public readonly Vector2 Uv;
        public readonly float SignedDistance;

        public ClipVertex(Vector3 localPosition, Vector2 uv, float signedDistance)
        {
            LocalPosition = localPosition;
            Uv = uv;
            SignedDistance = signedDistance;
        }

        public static ClipVertex Lerp(ClipVertex first, ClipVertex second, float interpolation)
        {
            return new ClipVertex(
                Vector3.LerpUnclamped(first.LocalPosition, second.LocalPosition, interpolation),
                Vector2.LerpUnclamped(first.Uv, second.Uv, interpolation),
                0f);
        }
    }

    private sealed class HalfGeometry
    {
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<int> _triangles = new List<int>();
        private readonly Dictionary<RenderVertexKey, int> _vertexLookup =
            new Dictionary<RenderVertexKey, int>();
        private readonly Dictionary<UndirectedEdgeKey, BoundaryRecord> _boundaryRecords =
            new Dictionary<UndirectedEdgeKey, BoundaryRecord>();
        private readonly List<Vector2[]> _fallbackColliderPaths = new List<Vector2[]>();
        private float _area;

        public bool HasArea => _triangles.Count >= 3 && _area > MinimumPolygonArea;
        public float Area => _area;

        public void AddPolygon(List<ClipVertex> polygon)
        {
            if (polygon == null || polygon.Count < 3)
            {
                return;
            }

            List<Vector2> localPoints = new List<Vector2>(polygon.Count);
            for (int index = 0; index < polygon.Count; index++)
            {
                localPoints.Add(polygon[index].LocalPosition);
            }

            float polygonArea = Mathf.Abs(SignedArea(localPoints));
            if (polygonArea <= MinimumPolygonArea)
            {
                return;
            }

            int[] polygonIndices = new int[polygon.Count];
            for (int index = 0; index < polygon.Count; index++)
            {
                polygonIndices[index] = GetOrAddVertex(polygon[index]);
            }

            int triangleCountBefore = _triangles.Count;
            for (int index = 1; index < polygonIndices.Length - 1; index++)
            {
                int first = polygonIndices[0];
                int second = polygonIndices[index];
                int third = polygonIndices[index + 1];
                if (first == second || second == third || first == third)
                {
                    continue;
                }

                float triangleArea = Mathf.Abs(Cross(
                    (Vector2)_vertices[second] - (Vector2)_vertices[first],
                    (Vector2)_vertices[third] - (Vector2)_vertices[first])) * 0.5f;
                if (triangleArea <= MinimumPolygonArea)
                {
                    continue;
                }

                _triangles.Add(first);
                _triangles.Add(second);
                _triangles.Add(third);
                _area += triangleArea;
            }

            if (_triangles.Count == triangleCountBefore)
            {
                return;
            }

            _fallbackColliderPaths.Add(localPoints.ToArray());
            for (int index = 0; index < localPoints.Count; index++)
            {
                AddBoundaryEdge(localPoints[index], localPoints[(index + 1) % localPoints.Count]);
            }
        }

        public Mesh CreateMesh(string meshName)
        {
            if (!HasArea)
            {
                return null;
            }

            Mesh mesh = new Mesh
            {
                name = meshName,
                hideFlags = HideFlags.HideAndDontSave,
                indexFormat = _vertices.Count > ushort.MaxValue
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16
            };
            mesh.SetVertices(_vertices);
            mesh.SetUVs(0, _uvs);
            mesh.SetTriangles(_triangles, 0, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        public List<Vector2[]> BuildColliderPaths()
        {
            List<BoundaryEdge> edges = new List<BoundaryEdge>();
            foreach (KeyValuePair<UndirectedEdgeKey, BoundaryRecord> pair in _boundaryRecords)
            {
                if (pair.Value.Count == 1)
                {
                    edges.Add(new BoundaryEdge(pair.Value));
                }
            }

            List<Vector2[]> paths = TraceBoundaryLoops(edges);
            return paths.Count > 0 ? paths : new List<Vector2[]>(_fallbackColliderPaths);
        }

        private int GetOrAddVertex(ClipVertex vertex)
        {
            RenderVertexKey key = new RenderVertexKey(vertex.LocalPosition, vertex.Uv);
            if (_vertexLookup.TryGetValue(key, out int existingIndex))
            {
                return existingIndex;
            }

            int index = _vertices.Count;
            _vertices.Add(vertex.LocalPosition);
            _uvs.Add(vertex.Uv);
            _vertexLookup.Add(key, index);
            return index;
        }

        private void AddBoundaryEdge(Vector2 start, Vector2 end)
        {
            PositionKey startKey = new PositionKey(start);
            PositionKey endKey = new PositionKey(end);
            if (startKey.Equals(endKey))
            {
                return;
            }

            UndirectedEdgeKey edgeKey = new UndirectedEdgeKey(startKey, endKey);
            if (_boundaryRecords.TryGetValue(edgeKey, out BoundaryRecord record))
            {
                record.Count++;
                _boundaryRecords[edgeKey] = record;
                return;
            }

            _boundaryRecords.Add(edgeKey, new BoundaryRecord(startKey, endKey, start, end));
        }

        private static List<Vector2[]> TraceBoundaryLoops(List<BoundaryEdge> edges)
        {
            List<Vector2[]> paths = new List<Vector2[]>();
            if (edges == null || edges.Count == 0)
            {
                return paths;
            }

            bool[] used = new bool[edges.Count];
            for (int startEdgeIndex = 0; startEdgeIndex < edges.Count; startEdgeIndex++)
            {
                if (used[startEdgeIndex])
                {
                    continue;
                }

                BoundaryEdge startEdge = edges[startEdgeIndex];
                used[startEdgeIndex] = true;
                PositionKey loopStartKey = startEdge.StartKey;
                PositionKey currentKey = startEdge.EndKey;
                Vector2 currentPosition = startEdge.End;
                List<Vector2> loop = new List<Vector2> { startEdge.Start };
                int safety = edges.Count + 1;

                while (!currentKey.Equals(loopStartKey) && safety-- > 0)
                {
                    loop.Add(currentPosition);
                    int nextEdgeIndex = FindConnectedEdge(edges, used, currentKey, out bool reverse);
                    if (nextEdgeIndex < 0)
                    {
                        loop.Clear();
                        break;
                    }

                    used[nextEdgeIndex] = true;
                    BoundaryEdge nextEdge = edges[nextEdgeIndex];
                    currentKey = reverse ? nextEdge.StartKey : nextEdge.EndKey;
                    currentPosition = reverse ? nextEdge.Start : nextEdge.End;
                }

                SimplifyLoop(loop);
                if (loop.Count >= 3 && Mathf.Abs(SignedArea(loop)) > MinimumPolygonArea)
                {
                    paths.Add(loop.ToArray());
                }
            }

            return paths;
        }

        private static int FindConnectedEdge(
            IReadOnlyList<BoundaryEdge> edges,
            IReadOnlyList<bool> used,
            PositionKey currentKey,
            out bool reverse)
        {
            for (int index = 0; index < edges.Count; index++)
            {
                if (!used[index] && edges[index].StartKey.Equals(currentKey))
                {
                    reverse = false;
                    return index;
                }
            }

            for (int index = 0; index < edges.Count; index++)
            {
                if (!used[index] && edges[index].EndKey.Equals(currentKey))
                {
                    reverse = true;
                    return index;
                }
            }

            reverse = false;
            return -1;
        }

        private static void SimplifyLoop(List<Vector2> loop)
        {
            if (loop == null || loop.Count < 3)
            {
                return;
            }

            bool removedPoint;
            do
            {
                removedPoint = false;
                for (int index = loop.Count - 1; index >= 0 && loop.Count >= 3; index--)
                {
                    Vector2 previous = loop[(index - 1 + loop.Count) % loop.Count];
                    Vector2 current = loop[index];
                    Vector2 next = loop[(index + 1) % loop.Count];
                    Vector2 incoming = current - previous;
                    Vector2 outgoing = next - current;

                    if (incoming.sqrMagnitude <= 0.0000000001f ||
                        outgoing.sqrMagnitude <= 0.0000000001f)
                    {
                        loop.RemoveAt(index);
                        removedPoint = true;
                        continue;
                    }

                    float cross = Mathf.Abs(Cross(incoming, outgoing));
                    float scaledEpsilon = 0.00001f * Mathf.Sqrt(incoming.sqrMagnitude * outgoing.sqrMagnitude);
                    if (cross <= scaledEpsilon && Vector2.Dot(incoming, outgoing) >= 0f)
                    {
                        loop.RemoveAt(index);
                        removedPoint = true;
                    }
                }
            }
            while (removedPoint && loop.Count >= 3);
        }
    }

    private struct RenderVertexKey : IEquatable<RenderVertexKey>
    {
        private readonly long _x;
        private readonly long _y;
        private readonly long _z;
        private readonly long _u;
        private readonly long _v;

        public RenderVertexKey(Vector3 position, Vector2 uv)
        {
            _x = Quantize(position.x);
            _y = Quantize(position.y);
            _z = Quantize(position.z);
            _u = Quantize(uv.x);
            _v = Quantize(uv.y);
        }

        public bool Equals(RenderVertexKey other)
        {
            return _x == other._x && _y == other._y && _z == other._z &&
                   _u == other._u && _v == other._v;
        }

        public override bool Equals(object obj)
        {
            return obj is RenderVertexKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = _x.GetHashCode();
                hashCode = (hashCode * 397) ^ _y.GetHashCode();
                hashCode = (hashCode * 397) ^ _z.GetHashCode();
                hashCode = (hashCode * 397) ^ _u.GetHashCode();
                hashCode = (hashCode * 397) ^ _v.GetHashCode();
                return hashCode;
            }
        }
    }

    private struct PositionKey : IEquatable<PositionKey>, IComparable<PositionKey>
    {
        private readonly long _x;
        private readonly long _y;

        public PositionKey(Vector2 position)
        {
            _x = Quantize(position.x);
            _y = Quantize(position.y);
        }

        public bool Equals(PositionKey other)
        {
            return _x == other._x && _y == other._y;
        }

        public int CompareTo(PositionKey other)
        {
            int xComparison = _x.CompareTo(other._x);
            return xComparison != 0 ? xComparison : _y.CompareTo(other._y);
        }

        public override bool Equals(object obj)
        {
            return obj is PositionKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (_x.GetHashCode() * 397) ^ _y.GetHashCode();
            }
        }
    }

    private struct UndirectedEdgeKey : IEquatable<UndirectedEdgeKey>
    {
        private readonly PositionKey _first;
        private readonly PositionKey _second;

        public UndirectedEdgeKey(PositionKey first, PositionKey second)
        {
            if (first.CompareTo(second) <= 0)
            {
                _first = first;
                _second = second;
            }
            else
            {
                _first = second;
                _second = first;
            }
        }

        public bool Equals(UndirectedEdgeKey other)
        {
            return _first.Equals(other._first) && _second.Equals(other._second);
        }

        public override bool Equals(object obj)
        {
            return obj is UndirectedEdgeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (_first.GetHashCode() * 397) ^ _second.GetHashCode();
            }
        }
    }

    private struct BoundaryRecord
    {
        public int Count;
        public readonly PositionKey StartKey;
        public readonly PositionKey EndKey;
        public readonly Vector2 Start;
        public readonly Vector2 End;

        public BoundaryRecord(PositionKey startKey, PositionKey endKey, Vector2 start, Vector2 end)
        {
            Count = 1;
            StartKey = startKey;
            EndKey = endKey;
            Start = start;
            End = end;
        }
    }

    private struct BoundaryEdge
    {
        public readonly PositionKey StartKey;
        public readonly PositionKey EndKey;
        public readonly Vector2 Start;
        public readonly Vector2 End;

        public BoundaryEdge(BoundaryRecord record)
        {
            StartKey = record.StartKey;
            EndKey = record.EndKey;
            Start = record.Start;
            End = record.End;
        }
    }

    #endregion
}
