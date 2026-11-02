using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 为 UI 版眩晕星星粒子补充稳定的逐粒子缩小与渐隐表现。
/// 仅挂载在 UIStunStarExplosion 上，避免修改场景版 StunStarExplosion 的既有表现。
/// </summary>
[DisallowMultipleComponent]
[ExecuteAlways]
public sealed class UIStunStarParticleFade : MonoBehaviour
{
    #region Serialized Fields

    /// <summary>
    /// 用途：指定 UIStunStarExplosion 中负责绘制星星的主粒子系统。
    /// 调用边界：由本组件在 PlayMode 或 Prefab 预览的 Canvas 渲染前读写当前粒子；Prefab 未绑定时按子节点名称兜底查找。
    /// 注意事项：不要绑定 StarBurstDebris 或 Glow，否则会改变它们的尺寸和透明度。
    /// </summary>
    [SerializeField] private ParticleSystem targetParticleSystem;

    /// <summary>
    /// 用途：定义每颗星星从生命周期哪个比例开始同步缩小和渐隐。
    /// 调用边界：仅影响 UIStunStarExplosion 的主星星粒子，0.5 表示后半段过渡。
    /// 注意事项：取值越小，缩小和渐隐越早；场景版粒子曲线不受影响。
    /// </summary>
    [SerializeField, Range(0f, 0.95f)] private float transitionStart = 0.5f;

    #endregion

    #region Runtime State

    private const int InitialBufferSize = 32;

    private readonly Dictionary<uint, ParticleBaseline> _baselines =
        new Dictionary<uint, ParticleBaseline>(InitialBufferSize);

    private ParticleSystem.Particle[] _particleBuffer;
    private float _lastSimulationTime = -1f;

    private readonly struct ParticleBaseline
    {
        public ParticleBaseline(Vector3 size, Color32 color)
        {
            Size = size;
            Color = color;
        }

        public Vector3 Size { get; }
        public Color32 Color { get; }
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveTargetParticleSystem();
        EnsureBufferCapacity(InitialBufferSize);
    }

    private void OnEnable()
    {
        ResetTracking();
        Canvas.preWillRenderCanvases -= OnBeforeCanvasRender;
        Canvas.preWillRenderCanvases += OnBeforeCanvasRender;
    }

    private void OnDisable()
    {
        Canvas.preWillRenderCanvases -= OnBeforeCanvasRender;
        ResetTracking();
    }

    private void OnBeforeCanvasRender()
    {
        if (!ResolveTargetParticleSystem())
            return;

        ApplyParticleFade();
    }

    #endregion

    #region Particle Update

    private void ApplyParticleFade()
    {
        float simulationTime = targetParticleSystem.time;
        if (_lastSimulationTime >= 0f && simulationTime < _lastSimulationTime)
            _baselines.Clear();

        _lastSimulationTime = simulationTime;

        int requiredCapacity = Mathf.Max(InitialBufferSize, targetParticleSystem.particleCount);
        EnsureBufferCapacity(requiredCapacity);

        int particleCount = targetParticleSystem.GetParticles(_particleBuffer);
        if (particleCount <= 0)
        {
            if (!targetParticleSystem.isPlaying)
                _baselines.Clear();
            return;
        }

        float start = Mathf.Clamp(transitionStart, 0f, 0.95f);
        for (int i = 0; i < particleCount; i++)
        {
            ParticleSystem.Particle particle = _particleBuffer[i];
            if (!_baselines.TryGetValue(particle.randomSeed, out ParticleBaseline baseline))
            {
                baseline = new ParticleBaseline(particle.startSize3D, particle.startColor);
                _baselines.Add(particle.randomSeed, baseline);
            }

            float normalizedAge = particle.startLifetime <= 0f
                ? 1f
                : 1f - particle.remainingLifetime / particle.startLifetime;
            float transition = Mathf.InverseLerp(start, 1f, normalizedAge);
            float factor = 1f - Mathf.SmoothStep(0f, 1f, transition);

            particle.startSize3D = baseline.Size * factor;
            Color32 color = baseline.Color;
            color.a = (byte)Mathf.RoundToInt(baseline.Color.a * factor);
            particle.startColor = color;
            _particleBuffer[i] = particle;
        }

        targetParticleSystem.SetParticles(_particleBuffer, particleCount);
    }

    private bool ResolveTargetParticleSystem()
    {
        if (targetParticleSystem != null)
            return true;

        ParticleSystem[] particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem candidate = particleSystems[i];
            if (candidate != null && candidate.gameObject.name == "StunStarExplosion")
            {
                targetParticleSystem = candidate;
                return true;
            }
        }

        return false;
    }

    private void EnsureBufferCapacity(int requiredCapacity)
    {
        if (_particleBuffer != null && _particleBuffer.Length >= requiredCapacity)
            return;

        _particleBuffer = new ParticleSystem.Particle[Mathf.NextPowerOfTwo(requiredCapacity)];
    }

    private void ResetTracking()
    {
        _baselines.Clear();
        _lastSimulationTime = -1f;
    }

    #endregion
}
