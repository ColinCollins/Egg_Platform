using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Dig
{
    /// <summary>单只病菌 Prefab；接触结算由关卡批处理，本对象只负责单次锁定和吸入。</summary>
    public sealed class DigGermCtrl : BaseDigComp
    {
        #region Prefab 绑定
        /// <summary>实例可覆盖的当前大小，必须大于零。</summary>
        [SerializeField, Min(1)] private int currentSize = 5;
        /// <summary>被吞噬时给主角增加的数值，允许零。</summary>
        [SerializeField, Min(0)] private int growthSize = 5;
        /// <summary>身体视觉引用；初始化保留身体及根节点的Prefab缩放，不按判定大小重算。</summary>
        [SerializeField] private Transform body;
        /// <summary>病菌实体Collider；显式绑定任意2D形状，须属于根刚体，锁定吞噬时立即禁用，兼容旧circle引用。</summary>
        [SerializeField, FormerlySerializedAs("circle")]
        [Tooltip("绑定根节点或子节点的实体Collider2D，须附着于Physics Body；初始化保留形状、尺寸及其它Collider配置。")]
        private Collider2D contactCollider;
        /// <summary>与DigGermCtrl同节点的刚体；显式绑定，暂停及吞噬时停止模拟，类型／重力由Prefab配置。</summary>
        [SerializeField] private Rigidbody2D physicsBody;
        #endregion

        #region 实例状态
        private Tween _animation;
        private Vector2 _velocity;
        private float _angularVelocity;
        private bool _paused = true;
        /// <summary>判定大小，仅来源于 Prefab 数值。</summary>
        public int CurrentSize => currentSize;
        /// <summary>主角吞噬本病菌获得的增长量。</summary>
        public int GrowthSize => growthSize;
        /// <summary>是否已被本轮结算锁定；重复物理回调必须忽略。</summary>
        public bool IsClaimed { get; private set; }

        /// <summary>新实例初始化时校验绑定并暂停物理；保留Prefab缩放和Collider配置。</summary>
        public void Initialize()
        {
            if (!Application.isPlaying || !body || !contactCollider || !physicsBody ||
                physicsBody.gameObject != gameObject || contactCollider.attachedRigidbody != physicsBody ||
                currentSize <= 0 || growthSize < 0)
                throw new InvalidOperationException("DigGerm Prefab 绑定或大小配置无效。");
            _velocity = physicsBody.linearVelocity;
            _angularVelocity = physicsBody.angularVelocity;
            physicsBody.simulated = false;
        }

        /// <summary>仅关卡批次提交时调用；先关闭触碰，后增加主角大小和播放动画。</summary>
        public bool TryClaim()
        {
            if (!Application.isPlaying || IsClaimed) return false;
            IsClaimed = true;
            contactCollider.enabled = false;
            physicsBody.simulated = false;
            return true;
        }

        /// <summary>锁定后从当前根缩放播放吸入；由局部Tween驱动，取消不执行完成回调。</summary>
        public void PlayConsume(Transform target, Action completed)
        {
            if (!Application.isPlaying) return;
            Vector3 origin = transform.position;
            Vector3 originScale = transform.localScale;
            _animation = DOVirtual.Float(0f, 1f, 0.3f, t =>
            {
                if (target) transform.position = Vector3.Lerp(origin, target.position, t);
                transform.localScale = originScale * (1f - t);
            }).SetEase(Ease.InQuad).OnComplete(() =>
            {
                gameObject.SetActive(false);
                completed?.Invoke();
            });
        }

        /// <summary>暂停刚体及局部吸入，恢复时保留速度；已认领病菌只恢复动画，不再开启物理。</summary>
        public void SetPaused(bool paused)
        {
            if (!Application.isPlaying) return;
            if (_paused != paused)
            {
                _paused = paused;
                if (!IsClaimed)
                {
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
                }
            }
            if (paused) _animation?.Pause(); else _animation?.Play();
        }
        /// <summary>失败/退出终止物理及未完成吞噬，永不补发完成回调；不在编辑器预览中修改刚体。</summary>
        public void StopConsumption()
        {
            if (!Application.isPlaying) return;
            SetPaused(true);
            _animation?.Kill();
            _animation = null;
        }
        private void OnDisable() => StopConsumption();
        #endregion
    }
}
