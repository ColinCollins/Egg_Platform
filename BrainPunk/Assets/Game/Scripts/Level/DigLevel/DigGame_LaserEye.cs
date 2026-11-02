using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Dig
{
    /// <summary>Dig 激光眼：沿激光本地X正方向扩张，按Layer截断并在接触点播放火花，命中本关Actor时进入统一失败链。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Game/Dig/Dig Game Laser Eye")]
    public sealed class DigGame_LaserEye : MonoBehaviour
    {
        private const int RaycastCapacity = 32;

        #region Inspector 绑定
        /// <summary>所属Dig关卡；用于核对Actor归属并提交唯一失败结果，必须由Prefab显式绑定。</summary>
        [SerializeField, LabelText("所属关卡")] private DigLevelCtrl level;
        /// <summary>使用Sp_wd_jg_01的激光Renderer；要求Draw Mode为Sliced或Tiled，脚本只修改size.x。</summary>
        [SerializeField, LabelText("激光 Renderer")] private SpriteRenderer laserRenderer;
        /// <summary>射线起点及方向；本地X正方向为发射方向，允许通过Transform旋转和缩放调整。</summary>
        [SerializeField, LabelText("检测起点")] private Transform rayOrigin;
        /// <summary>随激光宽度同步的Trigger；用于保持场景物理轮廓与显示一致，不负责判定命中，Prefab必须显式绑定。</summary>
        [SerializeField, LabelText("激光 Trigger")] private BoxCollider2D laserTrigger;
        /// <summary>显式绑定SparkExplosion2D根粒子实例；命中时播放并持续跟随接触点，同一Collider不重播，不创建新对象。</summary>
        [SerializeField, LabelText("命中特效")] private ParticleSystem hitEffect;
        /// <summary>会截断激光的Collider所属Layer；可选择多层，Nothing仅保留Actor检测，Actor无需加入此掩码。</summary>
        [SerializeField, LabelText("阻挡 Layer")] private LayerMask stoppingLayers;
        #endregion

        #region Inspector 参数
        /// <summary>关卡新实例Awake时的初始本地宽度；重开由关卡销毁重建恢复。</summary>
        [SerializeField, Min(0.001f), LabelText("初始宽度")] private float initialWidth = 0.1f;
        /// <summary>没有命中阻挡物时允许扩张到的最大本地宽度。</summary>
        [SerializeField, Min(0.001f), LabelText("最大宽度")] private float maxWidth = 20f;
        /// <summary>激光每秒增加的本地宽度；0表示保持初始宽度但仍检测范围内命中。</summary>
        [SerializeField, Min(0f), LabelText("扩张速度")] private float expandSpeed = 16f;
        /// <summary>粒子沿命中表面法线外移的世界距离，避免与表面完全重合。</summary>
        [SerializeField, Min(0f), LabelText("特效表面偏移")] private float hitEffectOffset = 0.02f;
        /// <summary>Actor命中至提交失败结果的最短游戏秒数；暂停或切后台时不推进，默认1秒。</summary>
        [SerializeField, Min(0f), LabelText("失败重置等待")] private float failureDelay = 1f;
        #endregion

        #region 运行状态
        private readonly RaycastHit2D[] _hits = new RaycastHit2D[RaycastCapacity];
        private ContactFilter2D _contactFilter;
        private float _currentWidth;
        private Collider2D _lastEffectCollider;
        #endregion

        #region 生命周期
        private void Awake()
        {
            if (!Application.isPlaying) return;
            ValidateConfiguration();
            _contactFilter = new ContactFilter2D { useTriggers = true };
            _contactFilter.SetLayerMask(Physics2D.AllLayers);
            hitEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ApplyWidth(initialWidth);
        }

        /// <summary>每渲染帧按最大范围重新检测；显示端点按扩张速度靠近当前命中点，不使用历史截断长度限制检测。</summary>
        private void LateUpdate()
        {
            if (!Application.isPlaying || !level || !level.IsRuntimeReady || level.IsPaused ||
                level.IsResultLocked) return;

            float candidateWidth = Mathf.Min(maxWidth, _currentWidth + expandSpeed * Time.deltaTime);
            Vector2 worldAxis = rayOrigin.TransformVector(Vector3.right);
            float axisScale = worldAxis.magnitude;
            if (axisScale <= Mathf.Epsilon) return;

            Vector2 direction = worldAxis / axisScale;
            float distance = maxWidth * axisScale;
            if (TryGetNearestHit((Vector2)rayOrigin.position, direction, distance,
                    out RaycastHit2D hit, out DigActorCtrl actor) && hit.distance / axisScale <= candidateWidth)
            {
                candidateWidth = Mathf.Clamp(hit.distance / axisScale, 0f, candidateWidth);
                ApplyWidth(candidateWidth);
                PlayHitEffect(hit);
                if (actor) level.TryFail(actor, failureDelay);
                return;
            }

            ApplyWidth(candidateWidth);
            if (_lastEffectCollider || hitEffect.isEmitting)
            {
                hitEffect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                _lastEffectCollider = null;
            }
        }

        private void OnDisable()
        {
            if (!Application.isPlaying || !hitEffect) return;
            hitEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        #endregion

        #region 激光检测与表现
        private bool TryGetNearestHit(Vector2 origin, Vector2 direction, float distance,
            out RaycastHit2D nearest, out DigActorCtrl actor)
        {
            int count = Physics2D.Raycast(origin, direction, _contactFilter, _hits, distance);
            nearest = default;
            actor = null;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D hit = _hits[i];
                Collider2D candidate = hit.collider;
                if (!candidate || candidate.transform == transform || candidate.transform.IsChildOf(transform)) continue;

                DigActorCtrl candidateActor = null;
                bool isActor = candidate.attachedRigidbody &&
                    candidate.attachedRigidbody.TryGetComponent(out candidateActor);
                if (!isActor && !IsStoppingCollider(candidate)) continue;
                if (hit.distance >= nearestDistance) continue;

                nearest = hit;
                actor = candidateActor;
                nearestDistance = hit.distance;
            }
            return nearest.collider != null;
        }

        private bool IsStoppingCollider(Collider2D candidate)
            => (stoppingLayers.value & (1 << candidate.gameObject.layer)) != 0;

        private void ApplyWidth(float width)
        {
            _currentWidth = width;
            Vector2 renderSize = laserRenderer.size;
            renderSize.x = width;
            laserRenderer.size = renderSize;
            if (!laserTrigger) return;

            Vector2 triggerSize = laserTrigger.size;
            triggerSize.x = width;
            laserTrigger.size = triggerSize;
            Vector2 triggerOffset = laserTrigger.offset;
            triggerOffset.x = width * 0.5f;
            laserTrigger.offset = triggerOffset;
        }

        private void PlayHitEffect(RaycastHit2D hit)
        {
            if (!hitEffect) return;
            Vector2 point = hit.point + hit.normal * hitEffectOffset;
            hitEffect.transform.position = new Vector3(point.x, point.y, hitEffect.transform.position.z);
            if (_lastEffectCollider == hit.collider)
            {
                if (!hitEffect.isEmitting) hitEffect.Play(true);
                return;
            }
            _lastEffectCollider = hit.collider;
            hitEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            hitEffect.Play(true);
        }
        #endregion

        #region 配置校验
        private void ValidateConfiguration()
        {
            if (!level || !laserRenderer || !rayOrigin || !laserTrigger || !hitEffect)
                throw new InvalidOperationException("DigGame_LaserEye 的关卡、激光、检测起点、Trigger或命中特效未绑定。");
            if (laserRenderer.drawMode == SpriteDrawMode.Simple || !laserTrigger.isTrigger)
                throw new InvalidOperationException("DigGame_LaserEye 要求激光Renderer使用Sliced/Tiled，激光Collider勾选Is Trigger。");
            if (IsInvalid(initialWidth) || IsInvalid(maxWidth) || IsInvalid(expandSpeed) ||
                IsInvalid(hitEffectOffset) || IsInvalid(failureDelay) || initialWidth <= 0f ||
                maxWidth < initialWidth || expandSpeed < 0f || hitEffectOffset < 0f || failureDelay < 0f)
                throw new InvalidOperationException("DigGame_LaserEye 的宽度、速度、特效偏移或失败等待配置无效。");
        }

        private static bool IsInvalid(float value) => float.IsNaN(value) || float.IsInfinity(value);
        #endregion
    }
}
