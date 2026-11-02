using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using AudioManager = Game.Scripts.Common.AudioManager;

namespace Game.Dig
{
    /// <summary>Dig 炸弹：接触角色、同关炸弹或 TriggerItem 层碰撞体引爆，在固定世界圆内持续检测死亡与连锁；重开由关卡销毁重建。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Game/Dig/Dig Game Bomb")]
    public sealed class DigGame_Bomb : MonoBehaviour
    {
        #region Inspector 绑定与参数
        /// <summary>所属关卡，必须在关卡实例显式绑定；死亡、结果去重与重开复用该控制器。</summary>
        [SerializeField, LabelText("所属关卡")] private DigLevelCtrl level;
        /// <summary>与本脚本同节点的刚体；子节点碰撞消息由该刚体转交，保留原重力和物理参数。</summary>
        [SerializeField, LabelText("炸弹刚体")] private Rigidbody2D physicsBody;
        /// <summary>炸弹实体碰撞体，必须附着于上述刚体；爆炸前取其世界中心，不改尺寸、Offset 或 Trigger 配置。</summary>
        [SerializeField, LabelText("炸弹碰撞体")] private Collider2D bombCollider;
        /// <summary>炸弹外观与引信的父对象；爆炸时隐藏，粒子必须放在此对象之外。</summary>
        [SerializeField, LabelText("炸弹外观")] private GameObject body;
        /// <summary>引信动画，可不绑定；暂停时保留速度，爆炸时停止。</summary>
        [SerializeField, LabelText("引信动画")] private Animator fuseAnimator;
        /// <summary>显式绑定 ExplosionRoundFire 全部粒子，首项是根粒子；逐个暂停恢复，避免已结束的子粒子被重播，Stop Action 须为 None。</summary>
        [SerializeField, LabelText("爆炸粒子（首项为根）")] private ParticleSystem[] explosionEffects;
        /// <summary>世界单位的爆炸半径，不随炸弹或父节点 Scale 改变；与 Gizmos 显示使用同一数值。</summary>
        [SerializeField, Min(0.01f), LabelText("爆炸半径")] private float explosionRadius = 1.5f;
        /// <summary>爆炸范围保留的游戏秒数；0 表示只检测爆炸瞬间，暂停及后台不消耗时间。</summary>
        [SerializeField, Min(0f), LabelText("范围持续时间")] private float explosionDuration = 1f;
        #endregion

        #region Actor 爆炸死亡表现
        /// <summary>Actor 相对炸弹分左右上抛；初速度为 (±0.5,0.7) 乘力度，默认对齐 Level50，不修改角色刚体。</summary>
        [SerializeField, Min(0.01f), LabelText("爆炸击飞力度")] private float actorLaunchSpeed = 50f;
        /// <summary>飞出期间向世界下方的加速度，0 为直线飞出；只控制本次死亡演出，不改角色刚体重力。</summary>
        [SerializeField, Min(0f), LabelText("爆炸下坠加速度")] private float actorFlightGravity = 20f;
        /// <summary>身体旋转角速度，单位度／秒；默认每半秒一圈，0 不旋转，负数反向。</summary>
        [SerializeField, LabelText("爆炸旋转速度")] private float actorSpinSpeed = 720f;
        /// <summary>爆炸命中后立即应用到本角色 Spine 实例的烧焦 RGB；保留 Alpha，不修改共享材质或其它 Actor。</summary>
        [SerializeField, ColorUsage(false), LabelText("爆炸烧焦颜色")] private Color actorBurnColor = new Color(0.08f, 0.065f, 0.055f);
        #endregion

        #region 运行状态
        private readonly List<Collider2D> _hits = new List<Collider2D>(16);
        private ContactFilter2D _filter;
        private int _triggerItemLayer;
        private bool _exploded;
        private bool _motionPaused;
        private bool _backgroundPaused;
        private Vector2 _velocity;
        private float _angularVelocity;
        private float _fuseSpeed;
        private Vector2 _explosionCenter;
        private float _blastRemaining;
        private bool CanRun => isActiveAndEnabled && level && level.isActiveAndEnabled &&
            level.IsRuntimeReady && !level.IsPaused && !_backgroundPaused;
        #endregion

        #region 生命周期与暂停
        private void Awake()
        {
            if (!Application.isPlaying) return;
            ValidateConfiguration();
            _triggerItemLayer = LayerMask.NameToLayer("TriggerItem");
            _filter = new ContactFilter2D { useTriggers = true };
            _filter.SetLayerMask(Physics2D.AllLayers);
            _fuseSpeed = fuseAnimator ? fuseAnimator.speed : 1f;
            foreach (ParticleSystem effect in explosionEffects)
                effect.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            SetMotionPaused(true);
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            SyncPause();
        }

        private void FixedUpdate()
        {
            if (!Application.isPlaying) return;
            SyncPause();
            if (!CanRun || !_exploded || _blastRemaining <= 0f) return;
            DetectBlast();
            _blastRemaining = Mathf.Max(0f, _blastRemaining - Time.fixedDeltaTime);
        }

        private void OnApplicationPause(bool paused)
        {
            if (!Application.isPlaying) return;
            _backgroundPaused = paused;
            SyncPause();
        }

        private void SyncPause()
        {
            bool paused = !CanRun;
            SetMotionPaused(paused || level.IsResultLocked || _exploded);
            if (!_exploded) return;
            foreach (ParticleSystem effect in explosionEffects)
            {
                if (paused && effect.isPlaying) effect.Pause(false);
                else if (!paused && effect.isPaused) effect.Play(false);
            }
        }

        private void SetMotionPaused(bool paused)
        {
            if (_motionPaused == paused) return;
            _motionPaused = paused;
            if (paused)
            {
                _velocity = physicsBody.linearVelocity;
                _angularVelocity = physicsBody.angularVelocity;
                physicsBody.simulated = false;
            }
            else
            {
                physicsBody.simulated = true;
                physicsBody.linearVelocity = _velocity;
                physicsBody.angularVelocity = _angularVelocity;
            }
            if (fuseAnimator) fuseAnimator.speed = paused ? 0f : _fuseSpeed;
        }

        private void OnDisable()
        {
            if (!Application.isPlaying) return;
            if (physicsBody) physicsBody.simulated = false;
            if (explosionEffects == null) return;
            foreach (ParticleSystem effect in explosionEffects)
                if (effect) effect.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            _blastRemaining = 0f;
            _hits.Clear();
        }
        #endregion

        #region 接触引爆与圆形范围
        private void OnCollisionEnter2D(Collision2D collision) => ReportContact(collision.collider);
        private void OnCollisionStay2D(Collision2D collision) => ReportContact(collision.collider);
        private void OnTriggerEnter2D(Collider2D other) => ReportContact(other);
        private void OnTriggerStay2D(Collider2D other) => ReportContact(other);

        private void ReportContact(Collider2D other)
        {
            if (!Application.isPlaying || !CanRun || _exploded || level.IsResultLocked ||
                !bombCollider.enabled || !other) return;
            Rigidbody2D otherBody = other.attachedRigidbody;
            if (otherBody && otherBody.TryGetComponent(out DigActorCtrl actor))
            {
                // 直接接触也必须生效，不能因配置半径小于实体尺寸而漏判；关卡入口拒绝非本关或已到达角色。
                if (TryFailActor(actor, explosionDuration, bombCollider.bounds.center)) Detonate();
            }
            else if (otherBody && otherBody.TryGetComponent(out DigGame_Bomb bomb))
            {
                if (bomb != this && bomb.level == level && bomb.CanRun)
                {
                    Detonate();
                    bomb.Detonate();
                }
            }
            // 普通机关按实际 Collider 节点的 Layer 判断，静态 Collider 无须添加刚体。
            else if (other.gameObject.layer == _triggerItemLayer) Detonate();
        }

        private bool TryFailActor(DigActorCtrl actor, float failureDelay, Vector2 center)
            => level.TryFailFromExplosion(actor, failureDelay,
                new DigExplosionDeath(center, actorLaunchSpeed, actorFlightGravity, actorSpinSpeed, actorBurnColor));

        private void Detonate()
        {
            if (_exploded || !CanRun) return;
            // 先锁定再进行范围检测；连锁允许在同一爆炸造成失败后继续，循环接触也只爆炸一次。
            _exploded = true;
            _explosionCenter = bombCollider.bounds.center;
            _blastRemaining = explosionDuration;
            SetMotionPaused(true);
            bombCollider.enabled = false;
            if (fuseAnimator) fuseAnimator.enabled = false;
            body.SetActive(false);

            Transform effectRoot = explosionEffects[0].transform;
            effectRoot.position = new Vector3(_explosionCenter.x, _explosionCenter.y, effectRoot.position.z);
            foreach (ParticleSystem effect in explosionEffects) effect.Play(false);
            AudioManager.PlaySound("rocketExplosion");
            DetectBlast();
        }

        private void DetectBlast()
        {
            int count = Physics2D.OverlapCircle(_explosionCenter, explosionRadius, _filter, _hits);
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = _hits[i];
                if (!hit || !hit.attachedRigidbody) continue;
                Rigidbody2D hitBody = hit.attachedRigidbody;
                if (hitBody.TryGetComponent(out DigGame_Bomb bomb))
                {
                    if (bomb != this && bomb.level == level) bomb.Detonate();
                }
                else if (hitBody.TryGetComponent(out DigActorCtrl actor))
                    TryFailActor(actor, _blastRemaining, _explosionCenter);
            }
            _hits.Clear();
        }
        #endregion

        #region 编辑器显示与配置校验
        private void OnDrawGizmosSelected()
        {
            Vector3 center = Application.isPlaying && _exploded
                ? new Vector3(_explosionCenter.x, _explosionCenter.y, transform.position.z)
                : bombCollider ? bombCollider.bounds.center : transform.position;
            Color previous = Gizmos.color;
            Gizmos.color = new Color(1f, 0.35f, 0f, 1f);
            // 在世界 XY 平面画圆，不受炸弹非均匀缩放影响，与 Physics2D 查询保持相同中心和半径。
            const int segments = 64;
            Vector3 last = center + Vector3.right * explosionRadius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * (Mathf.PI * 2f / segments);
                Vector3 next = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * explosionRadius;
                Gizmos.DrawLine(last, next);
                last = next;
            }
            Gizmos.color = previous;
        }

        private void ValidateConfiguration()
        {
            if (!level || !physicsBody || physicsBody.gameObject != gameObject || !bombCollider ||
                bombCollider.attachedRigidbody != physicsBody || !body || body == gameObject ||
                explosionEffects == null || explosionEffects.Length == 0)
                throw new InvalidOperationException("DigGame_Bomb 的关卡、根刚体、实体Collider、外观或爆炸粒子未正确绑定。");
            foreach (ParticleSystem effect in explosionEffects)
                if (!effect || effect.transform.IsChildOf(body.transform) ||
                    effect.main.stopAction != ParticleSystemStopAction.None)
                    throw new InvalidOperationException("DigGame_Bomb 粒子须放在外观对象之外，并将 Stop Action 设为 None。");
            if (float.IsNaN(explosionRadius) || float.IsInfinity(explosionRadius) || explosionRadius <= 0f ||
                float.IsNaN(explosionDuration) || float.IsInfinity(explosionDuration) || explosionDuration < 0f)
                throw new InvalidOperationException("DigGame_Bomb 爆炸半径须大于零，持续时间须为非负有限值。");
            if (!new DigExplosionDeath(Vector2.zero, actorLaunchSpeed, actorFlightGravity, actorSpinSpeed, actorBurnColor).IsValid)
                throw new InvalidOperationException("DigGame_Bomb 击飞力度须为正数，下坠加速度非负，所有爆炸死亡参数须为有限值。");
        }

        #endregion
    }
}
