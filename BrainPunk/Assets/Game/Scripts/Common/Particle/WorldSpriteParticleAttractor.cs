using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Scripts.Common.VFX
{
    /// <summary>
    /// 驱动 World Space ParticleSystem 的 2D Sprite 粒子先散开、旋转，再在生命周期末段吸附到目标。
    /// ParticleSystem 继续负责发射、序列帧、尺寸、颜色和拖尾，本组件只接管逐粒子位置。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class WorldSpriteParticleAttractor : MonoBehaviour
    {
        #region Serialized Fields

        /// <summary>
        /// 用途：指定由本组件接管位置的场景粒子系统。
        /// 调用边界：只读写当前对象上的 World Space ParticleSystem 粒子，不创建或加载资源。
        /// 注意事项：未绑定时会在运行时回退到当前对象的 ParticleSystem。
        /// </summary>
        [SerializeField] private ParticleSystem targetParticleSystem;

        /// <summary>
        /// 用途：指定所有粒子最终汇聚的世界目标。
        /// 调用边界：目标可以在播放期间移动，也可以在调用 Play(Transform) 时替换。
        /// 注意事项：目标在播放中丢失时会清空本轮粒子，避免零速度粒子滞留。
        /// </summary>
        [SerializeField] private Transform attractionTarget;

        /// <summary>
        /// 用途：控制对象在 PlayMode 启用时是否自动播放一次。
        /// 调用边界：仅在 Application.isPlaying 且目标有效时生效。
        /// 注意事项：由对象池或业务代码显式触发时应关闭，并调用 Play(Transform)。
        /// </summary>
        [SerializeField] private bool playOnEnable = true;

        /// <summary>
        /// 用途：控制吸附时是否保持每颗粒子的原始 Z 平面。
        /// 调用边界：开启时只在 XY 平面向目标汇聚，适用于正交 2D 场景。
        /// 注意事项：需要真实 3D 汇聚时可在 Inspector 关闭。
        /// </summary>
        [SerializeField] private bool preserveParticleZ = true;

        /// <summary>
        /// 用途：控制自由散开结束后、旋涡阶段开始前的完全停顿时间。
        /// 调用边界：按单颗粒子的模拟秒数计算，PlayMode 与 Editor 预览共用。
        /// 注意事项：设为 0 可取消完全停顿；过大的值会被限制，以保留末段旋涡和吸附时间。
        /// </summary>
        [SerializeField, Min(0f), LabelText("中间停顿时间（秒）")]
        private float middlePauseDuration = 0.22f;

        /// <summary>
        /// 用途：控制单颗粒子从出生到抵达目标并消失的最短飞行时间。
        /// 调用边界：点击 Play 或 PlayMode 自动播放时同步到 ParticleSystem 的 Start Lifetime。
        /// 注意事项：若大于最长飞行时间，播放时会自动按较小值作为下限。
        /// </summary>
        [SerializeField, Min(0.05f), LabelText("最短飞行时间（秒）")]
        private float minimumFlightDuration = 1.5f;

        /// <summary>
        /// 用途：控制单颗粒子从出生到抵达目标并消失的最长飞行时间。
        /// 调用边界：点击 Play 或 PlayMode 自动播放时同步到 ParticleSystem 的 Start Lifetime。
        /// 注意事项：与最短飞行时间相同时，每颗粒子使用相同飞行时间。
        /// </summary>
        [SerializeField, Min(0.05f), LabelText("最长飞行时间（秒）")]
        private float maximumFlightDuration = 2f;

        /// <summary>
        /// 用途：定义粒子自由散开速度随自身生命周期的倍率。
        /// 调用边界：只影响脚本保存的初速度积分，不改写 ParticleSystem 的 Inspector 发射速度。
        /// 注意事项：曲线可短暂为负以形成参考效果中的轻微回弹。
        /// </summary>
        [SerializeField] private AnimationCurve freeFlightSpeedCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.24f, -0.17f),
            new Keyframe(0.31f, 0.06f),
            new Keyframe(0.36f, 0f),
            new Keyframe(1f, 0f));

        /// <summary>
        /// 用途：定义粒子从自由轨迹插值到目标位置的比例。
        /// 调用边界：横轴是单颗粒子的归一化生命周期，纵轴建议保持在 0 到 1。
        /// 注意事项：默认约在 69% 生命周期后开始吸附，并在死亡前抵达目标。
        /// </summary>
        [SerializeField] private AnimationCurve attractionCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.69f, 0f),
            new Keyframe(1f, 1f));

        /// <summary>
        /// 用途：定义自由轨迹围绕出生发射中心旋转的强度。
        /// 调用边界：曲线值会乘以 vortexDegreesPerSecond，并按粒子模拟时间积分。
        /// 注意事项：负值代表顺时针旋转；吸附曲线仍拥有最终位置控制权。
        /// </summary>
        [SerializeField] private AnimationCurve vortexCurve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.49f, 0f),
            new Keyframe(1f, -0.7f));

        /// <summary>
        /// 用途：提供 vortexCurve 的最大角速度基准。
        /// 调用边界：仅影响 XY 平面的自由轨迹旋转。
        /// 注意事项：数值过大会让低帧率设备上的轨迹产生明显折线。
        /// </summary>
        [SerializeField, Min(0f)] private float vortexDegreesPerSecond = 800f;

        #endregion

        #region Runtime State

        private const int MinimumBufferSize = 32;
        private const float FreeFlightEndNormalizedAge = 0.36f;
        private const float VortexStartNormalizedAge = 0.487f;
        private const float LatestMiddlePauseEndNormalizedAge = 0.85f;

        private readonly Dictionary<uint, ParticleMotionState> _motionStates =
            new Dictionary<uint, ParticleMotionState>(MinimumBufferSize);

        private ParticleSystem.Particle[] _particleBuffer;

#if UNITY_EDITOR
        private const float MaximumEditorPreviewDelta = 0.05f;

        private bool _isEditorPreviewing;
        private double _editorPreviewLastTimestamp;
        private float _editorPreviewElapsed;
        private float _editorPreviewDuration;
#endif

        private struct ParticleMotionState
        {
            public Vector3 FreePosition;
            public Vector3 InitialVelocity;
            public Vector3 EmitterOrigin;
            public float PreviousRemainingLifetime;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            ResolveParticleSystem();
            EnsureBufferCapacity();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying || !playOnEnable)
                return;

            Play();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying || !ResolveParticleSystem())
                return;

            UpdateParticlePositions();
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            StopEditorPreview(!Application.isPlaying);
#endif
            _motionStates.Clear();
        }

        #endregion

        #region Public API

        /// <summary>
        /// 设置后续播放及当前播放中的吸附目标。
        /// 调用边界：目标应是 World Scene 中有效的 Transform；不会自动触发播放。
        /// 注意事项：传入 null 会让当前粒子在下一帧被安全清空。
        /// </summary>
        public void SetTarget(Transform target)
        {
            attractionTarget = target;
        }

        /// <summary>
        /// 使用当前 Inspector 目标清理残留并播放一轮粒子。
        /// 调用边界：PlayMode 使用正常粒子播放；Editor 非运行态使用本地 Simulate 预览。
        /// 注意事项：重复调用会清空上一轮；Editor 预览不创建对象，也不访问运行时数据链路。
        /// </summary>
        [Button("Play", ButtonSizes.Large)]
        public void Play()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                PlayEditorPreview();
                return;
            }
#endif

            if (!ResolveParticleSystem() || attractionTarget == null)
                return;

            ApplyFlightDuration();
            _motionStates.Clear();
            EnsureBufferCapacity();
            targetParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            targetParticleSystem.Play(true);
        }

        /// <summary>
        /// 替换吸附目标后清理残留并播放一轮粒子。
        /// 调用边界：供关卡、奖励表现或对象池在实例就绪后显式调用。
        /// 注意事项：调用方应保证目标生命周期覆盖本轮最长粒子生命周期。
        /// </summary>
        public void Play(Transform target)
        {
            SetTarget(target);
            Play();
        }

        /// <summary>
        /// 停止发射，可选择同时清空当前存活粒子。
        /// 调用边界：仅控制绑定的局部 ParticleSystem，不销毁对象或目标。
        /// 注意事项：clear 为 false 时，现有粒子会继续完成吸附并自然死亡。
        /// </summary>
        public void Stop(bool clear = true)
        {
            if (!Application.isPlaying || !ResolveParticleSystem())
                return;

            ParticleSystemStopBehavior behavior = clear
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting;
            targetParticleSystem.Stop(true, behavior);

            if (clear)
                _motionStates.Clear();
        }

        #endregion

#if UNITY_EDITOR
        #region Editor Preview

        private void PlayEditorPreview()
        {
            StopEditorPreview(true);
            if (EditorUtility.IsPersistent(this) || !ResolveParticleSystem() ||
                attractionTarget == null || !isActiveAndEnabled)
                return;

            ApplyFlightDuration();
            _motionStates.Clear();
            EnsureBufferCapacity();
            targetParticleSystem.Simulate(0f, true, true, false);

            _editorPreviewElapsed = 0f;
            _editorPreviewDuration = GetEditorPreviewDuration();
            _editorPreviewLastTimestamp = EditorApplication.timeSinceStartup;
            _isEditorPreviewing = true;

            EditorApplication.update -= UpdateEditorPreview;
            EditorApplication.update += UpdateEditorPreview;
            SceneView.RepaintAll();
        }

        private void UpdateEditorPreview()
        {
            if (this == null || Application.isPlaying || !_isEditorPreviewing ||
                !isActiveAndEnabled || attractionTarget == null || !ResolveParticleSystem())
            {
                StopEditorPreview(!Application.isPlaying);
                return;
            }

            double timestamp = EditorApplication.timeSinceStartup;
            float simulationDelta = Mathf.Clamp(
                (float)(timestamp - _editorPreviewLastTimestamp),
                0f,
                MaximumEditorPreviewDelta);
            _editorPreviewLastTimestamp = timestamp;
            if (simulationDelta <= 0f)
                return;

            targetParticleSystem.Simulate(simulationDelta, true, false, false);
            UpdateParticlePositions();
            _editorPreviewElapsed += simulationDelta;

            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();

            if (_editorPreviewElapsed >= _editorPreviewDuration)
                StopEditorPreview(false);
        }

        private float GetEditorPreviewDuration()
        {
            ParticleSystem.MainModule main = targetParticleSystem.main;
            float duration = main.startDelay.constantMax + main.duration + main.startLifetime.constantMax;

            ParticleSystem.TrailModule trails = targetParticleSystem.trails;
            if (trails.enabled)
                duration += trails.lifetime.constantMax;

            return Mathf.Max(0.1f, duration + 0.1f);
        }

        private void StopEditorPreview(bool clearParticles)
        {
            bool wasEditorPreviewing = _isEditorPreviewing;
            EditorApplication.update -= UpdateEditorPreview;
            _isEditorPreviewing = false;
            _editorPreviewElapsed = 0f;

            if (wasEditorPreviewing && clearParticles && targetParticleSystem != null)
                targetParticleSystem.Simulate(0f, true, true, false);

            _motionStates.Clear();
            if (wasEditorPreviewing)
                SceneView.RepaintAll();
        }

        #endregion
#endif

        #region Particle Motion

        private void UpdateParticlePositions()
        {
            if (attractionTarget == null)
            {
                if (_motionStates.Count > 0 || targetParticleSystem.particleCount > 0)
                {
                    targetParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    _motionStates.Clear();
                }

                return;
            }

            EnsureBufferCapacity();
            int particleCount = targetParticleSystem.GetParticles(_particleBuffer);
            if (particleCount <= 0)
            {
                if (!targetParticleSystem.isEmitting)
                    _motionStates.Clear();
                return;
            }

            Vector3 currentTargetPosition = GetSimulationSpacePosition(attractionTarget.position);
            Vector3 currentEmitterOrigin = GetEmitterOriginInSimulationSpace();

            for (int i = 0; i < particleCount; i++)
            {
                ParticleSystem.Particle particle = _particleBuffer[i];
                float normalizedAge = particle.startLifetime <= 0f
                    ? 1f
                    : Mathf.Clamp01(1f - particle.remainingLifetime / particle.startLifetime);
                float motionCurveAge = GetMotionCurveAge(
                    normalizedAge,
                    particle.startLifetime,
                    out bool isMiddlePause);

                if (!_motionStates.TryGetValue(particle.randomSeed, out ParticleMotionState state))
                {
                    state = new ParticleMotionState
                    {
                        FreePosition = particle.position,
                        InitialVelocity = particle.velocity,
                        EmitterOrigin = currentEmitterOrigin,
                        PreviousRemainingLifetime = particle.remainingLifetime
                    };
                }
                else
                {
                    float simulationDelta = Mathf.Max(0f, state.PreviousRemainingLifetime - particle.remainingLifetime);
                    state.FreePosition += state.InitialVelocity
                        * freeFlightSpeedCurve.Evaluate(normalizedAge)
                        * simulationDelta;

                    if (!isMiddlePause)
                    {
                        float vortexDegrees = vortexCurve.Evaluate(motionCurveAge)
                            * vortexDegreesPerSecond
                            * simulationDelta;
                        state.FreePosition = RotateAroundEmitter(state.FreePosition, state.EmitterOrigin, vortexDegrees);
                    }

                    state.PreviousRemainingLifetime = particle.remainingLifetime;
                }

                Vector3 targetPosition = currentTargetPosition;
                if (preserveParticleZ)
                    targetPosition.z = state.FreePosition.z;

                float attraction = isMiddlePause
                    ? 0f
                    : Mathf.Clamp01(attractionCurve.Evaluate(motionCurveAge));
                particle.position = Vector3.LerpUnclamped(state.FreePosition, targetPosition, attraction);
                particle.velocity = Vector3.zero;
                _particleBuffer[i] = particle;
                _motionStates[particle.randomSeed] = state;
            }

            targetParticleSystem.SetParticles(_particleBuffer, particleCount);
        }

        private float GetMotionCurveAge(
            float normalizedAge,
            float particleLifetime,
            out bool isMiddlePause)
        {
            if (particleLifetime <= 0f || normalizedAge <= FreeFlightEndNormalizedAge)
            {
                isMiddlePause = false;
                return normalizedAge;
            }

            float normalizedPause = Mathf.Max(0f, middlePauseDuration) / particleLifetime;
            float pauseEndAge = Mathf.Clamp(
                FreeFlightEndNormalizedAge + normalizedPause,
                FreeFlightEndNormalizedAge,
                LatestMiddlePauseEndNormalizedAge);

            isMiddlePause = normalizedAge < pauseEndAge;
            if (isMiddlePause)
                return VortexStartNormalizedAge;

            float postPauseProgress = Mathf.InverseLerp(pauseEndAge, 1f, normalizedAge);
            return Mathf.Lerp(VortexStartNormalizedAge, 1f, postPauseProgress);
        }

        private static Vector3 RotateAroundEmitter(Vector3 position, Vector3 emitterOrigin, float degrees)
        {
            if (Mathf.Approximately(degrees, 0f))
                return position;

            Vector3 relative = position - emitterOrigin;
            float radians = degrees * Mathf.Deg2Rad;
            float sine = Mathf.Sin(radians);
            float cosine = Mathf.Cos(radians);
            return emitterOrigin + new Vector3(
                relative.x * cosine - relative.y * sine,
                relative.x * sine + relative.y * cosine,
                relative.z);
        }

        #endregion

        #region Simulation Space

        private Vector3 GetSimulationSpacePosition(Vector3 worldPosition)
        {
            ParticleSystem.MainModule main = targetParticleSystem.main;
            switch (main.simulationSpace)
            {
                case ParticleSystemSimulationSpace.World:
                    return worldPosition;
                case ParticleSystemSimulationSpace.Custom:
                    return main.customSimulationSpace != null
                        ? main.customSimulationSpace.InverseTransformPoint(worldPosition)
                        : transform.InverseTransformPoint(worldPosition);
                default:
                    return transform.InverseTransformPoint(worldPosition);
            }
        }

        private Vector3 GetEmitterOriginInSimulationSpace()
        {
            ParticleSystem.MainModule main = targetParticleSystem.main;
            switch (main.simulationSpace)
            {
                case ParticleSystemSimulationSpace.World:
                    return transform.position;
                case ParticleSystemSimulationSpace.Custom:
                    return main.customSimulationSpace != null
                        ? main.customSimulationSpace.InverseTransformPoint(transform.position)
                        : Vector3.zero;
                default:
                    return Vector3.zero;
            }
        }

        #endregion

        #region Setup

        private bool ResolveParticleSystem()
        {
            if (targetParticleSystem == null)
                targetParticleSystem = GetComponent<ParticleSystem>();

            return targetParticleSystem != null;
        }

        private void ApplyFlightDuration()
        {
            float minimum = Mathf.Max(0.05f, Mathf.Min(minimumFlightDuration, maximumFlightDuration));
            float maximum = Mathf.Max(minimum, Mathf.Max(minimumFlightDuration, maximumFlightDuration));

            ParticleSystem.MainModule main = targetParticleSystem.main;
            main.startLifetime = Mathf.Approximately(minimum, maximum)
                ? new ParticleSystem.MinMaxCurve(minimum)
                : new ParticleSystem.MinMaxCurve(minimum, maximum);
        }

        private void EnsureBufferCapacity()
        {
            if (!ResolveParticleSystem())
                return;

            int requiredCapacity = Mathf.Max(MinimumBufferSize, targetParticleSystem.main.maxParticles);
            if (_particleBuffer != null && _particleBuffer.Length >= requiredCapacity)
                return;

            _particleBuffer = new ParticleSystem.Particle[Mathf.NextPowerOfTwo(requiredCapacity)];
        }

        #endregion
    }
}
