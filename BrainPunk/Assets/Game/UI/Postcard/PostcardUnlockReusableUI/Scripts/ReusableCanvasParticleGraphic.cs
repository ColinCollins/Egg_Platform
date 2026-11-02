using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ReusableVfx.UI
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasRenderer), typeof(ParticleSystem))]
    public sealed class ReusableCanvasParticleGraphic : MaskableGraphic
    {
        [Header("UI粒子绘制")]
        [SerializeField, InspectorName("粒子纹理")]
        Texture2D particleTexture;

        [SerializeField, InspectorName("按速度方向旋转")]
        bool alignToVelocity;

        [SerializeField, InspectorName("旋转偏移")]
        float rotationOffset;

        [SerializeField, InspectorName("速度方向轴心偏移"), Range(-0.5f, 0.5f)]
        float velocityPivotOffset;

        [SerializeField, InspectorName("尺寸倍率"), Min(0.01f)]
        float sizeMultiplier = 1f;

        [SerializeField, InspectorName("位置倍率"), Min(0.01f)]
        float positionMultiplier = 1f;

        [Header("全屏适配")]
        [SerializeField, InspectorName("按矩形尺寸适配")]
        bool adaptToRectSize = true;

        [SerializeField, InspectorName("参考分辨率")]
        Vector2 referenceResolution = new(1920f, 1080f);

        [SerializeField, InspectorName("覆盖倍率"), Range(0.8f, 2f)]
        float coverageMultiplier = 1.15f;

        [Header("编辑器预览")]
        [SerializeField, InspectorName("启用粒子预览")]
        bool editorPreview = true;

        [SerializeField, InspectorName("预览速度"), Min(0f)]
        float previewSpeed = 1f;

        ParticleSystem particleSystemCache;
        RectTransform rectTransformCache;
        ParticleSystem.Particle[] particleBuffer;
        bool missingTextureReported;
        bool particlesAliveLastFrame;

#if UNITY_EDITOR
        double lastEditorUpdateTime;
#endif

        public override Texture mainTexture => particleTexture != null ? particleTexture : s_WhiteTexture;

        protected override void Awake()
        {
            base.Awake();
            CacheComponents();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            CacheComponents();
            ReportMissingTextureIfNeeded();

#if UNITY_EDITOR
            lastEditorUpdateTime = EditorApplication.timeSinceStartup;
            EditorApplication.update -= EditorPreviewUpdate;
            EditorApplication.update += EditorPreviewUpdate;
#endif
        }

        protected override void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorPreviewUpdate;
            if (!Application.isPlaying && particleSystemCache != null)
            {
                particleSystemCache.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
#endif

            base.OnDisable();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            velocityPivotOffset = Mathf.Clamp(velocityPivotOffset, -0.5f, 0.5f);
            sizeMultiplier = Mathf.Max(0.01f, sizeMultiplier);
            positionMultiplier = Mathf.Max(0.01f, positionMultiplier);
            referenceResolution.x = Mathf.Max(1f, referenceResolution.x);
            referenceResolution.y = Mathf.Max(1f, referenceResolution.y);
            coverageMultiplier = Mathf.Clamp(coverageMultiplier, 0.8f, 2f);
            previewSpeed = Mathf.Max(0f, previewSpeed);
            CacheComponents();
            base.OnValidate();
            SetAllDirty();
        }
#endif

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetVerticesDirty();
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || particleSystemCache == null)
            {
                return;
            }

            bool particlesAlive = particleSystemCache.IsAlive(true);
            if (particlesAlive || particlesAliveLastFrame)
            {
                SetVerticesDirty();
            }

            particlesAliveLastFrame = particlesAlive;
        }

#if UNITY_EDITOR
        void EditorPreviewUpdate()
        {
            if (Application.isPlaying || !editorPreview || !isActiveAndEnabled)
            {
                return;
            }

            CacheComponents();
            if (particleSystemCache == null)
            {
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            float deltaTime = Mathf.Clamp((float)(now - lastEditorUpdateTime), 0f, 0.05f) * previewSpeed;
            lastEditorUpdateTime = now;

            if (!particleSystemCache.isPlaying)
            {
                particleSystemCache.Play(true);
            }

            if (deltaTime > 0f)
            {
                particleSystemCache.Simulate(deltaTime, true, false, false);
            }

            if (!particleSystemCache.IsAlive(true))
            {
                particleSystemCache.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystemCache.Play(true);
            }

            SetVerticesDirty();
            EditorApplication.QueuePlayerLoopUpdate();
            Canvas.ForceUpdateCanvases();
        }
#endif

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (particleSystemCache == null)
            {
                return;
            }

            EnsureParticleBuffer();
            int particleCount = particleSystemCache.GetParticles(particleBuffer);
            float coverageScale = CalculateCoverageScale();
            for (int i = 0; i < particleCount; i++)
            {
                DrawParticle(vertexHelper, particleBuffer[i], coverageScale);
            }
        }

        void DrawParticle(VertexHelper vertexHelper, ParticleSystem.Particle particle, float coverageScale)
        {
            Color particleColor = particle.GetCurrentColor(particleSystemCache) * color;
            if (particleColor.a <= 0.002f)
            {
                return;
            }

            Vector3 currentSize = particle.GetCurrentSize3D(particleSystemCache)
                * sizeMultiplier
                * coverageScale;
            Vector2 halfSize = new(
                Mathf.Max(0.5f, currentSize.x * 0.5f),
                Mathf.Max(0.5f, currentSize.y * 0.5f));
            Vector2 center = particle.position * positionMultiplier * coverageScale;

            float angle = particle.rotation + rotationOffset;
            if (alignToVelocity && particle.velocity.sqrMagnitude > 0.0001f)
            {
                angle = Mathf.Atan2(particle.velocity.y, particle.velocity.x) * Mathf.Rad2Deg + rotationOffset;
            }

            float radians = angle * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            if (!Mathf.Approximately(velocityPivotOffset, 0f))
            {
                Vector2 pivotOffset = Rotate(new Vector2(currentSize.x * velocityPivotOffset, 0f), cosine, sine);
                center += pivotOffset;
            }

            int startIndex = vertexHelper.currentVertCount;
            AddVertex(vertexHelper, center, Rotate(new Vector2(-halfSize.x, -halfSize.y), cosine, sine), new Vector2(0f, 0f), particleColor);
            AddVertex(vertexHelper, center, Rotate(new Vector2(-halfSize.x, halfSize.y), cosine, sine), new Vector2(0f, 1f), particleColor);
            AddVertex(vertexHelper, center, Rotate(new Vector2(halfSize.x, halfSize.y), cosine, sine), new Vector2(1f, 1f), particleColor);
            AddVertex(vertexHelper, center, Rotate(new Vector2(halfSize.x, -halfSize.y), cosine, sine), new Vector2(1f, 0f), particleColor);

            vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vertexHelper.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
        }

        static void AddVertex(VertexHelper vertexHelper, Vector2 center, Vector2 offset, Vector2 uv, Color colorValue)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = center + offset;
            vertex.uv0 = uv;
            vertex.color = colorValue;
            vertexHelper.AddVert(vertex);
        }

        static Vector2 Rotate(Vector2 value, float cosine, float sine)
        {
            return new Vector2(
                value.x * cosine - value.y * sine,
                value.x * sine + value.y * cosine);
        }

        void CacheComponents()
        {
            if (particleSystemCache == null)
            {
                particleSystemCache = GetComponent<ParticleSystem>();
            }

            if (rectTransformCache == null)
            {
                rectTransformCache = transform as RectTransform;
            }

            ParticleSystemRenderer particleRenderer = GetComponent<ParticleSystemRenderer>();
            if (particleRenderer != null)
            {
                particleRenderer.enabled = false;
            }

            EnsureParticleBuffer();
        }

        float CalculateCoverageScale()
        {
            if (!adaptToRectSize)
            {
                return coverageMultiplier;
            }

            if (rectTransformCache == null)
            {
                rectTransformCache = transform as RectTransform;
            }

            if (rectTransformCache == null)
            {
                return coverageMultiplier;
            }

            Vector2 currentSize = rectTransformCache.rect.size;
            float widthScale = currentSize.x / Mathf.Max(1f, referenceResolution.x);
            float heightScale = currentSize.y / Mathf.Max(1f, referenceResolution.y);
            float adaptiveScale = Mathf.Max(1f, Mathf.Max(widthScale, heightScale));
            return adaptiveScale * coverageMultiplier;
        }

        void EnsureParticleBuffer()
        {
            int maximum = particleSystemCache != null
                ? Mathf.Max(1, particleSystemCache.main.maxParticles)
                : 1;
            if (particleBuffer == null || particleBuffer.Length != maximum)
            {
                particleBuffer = new ParticleSystem.Particle[maximum];
            }
        }

        void ReportMissingTextureIfNeeded()
        {
            if (!Application.isPlaying || missingTextureReported || particleTexture != null)
            {
                return;
            }

            missingTextureReported = true;
            Debug.LogWarning("可复用UI粒子缺少粒子纹理，已使用白色方块降级显示。", this);
        }
    }
}
