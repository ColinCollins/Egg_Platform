using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Dig
{
    /// <summary>主角实例大小、触碰上报与物理/表现；不持有存档或游戏结果。</summary>
    public sealed partial class DigActorCtrl : BaseDigComp
    {
        #region Prefab 绑定
        /// <summary>到达目标模式的匹配编号；只与同关目标 Trigger 的 index 比较，不参与大小／成长判定。</summary>
        [SerializeField, LabelText("胜利匹配 Index")] private int index;
        /// <summary>初始大小；运行时只在有效吞噬结算时增长，重建恢复序列化值。</summary>
        [SerializeField, Min(1)] private int currentSize = 10;
        /// <summary>主角被吞噬时的潜在收益；本版不使用，保留数值契约。</summary>
        [SerializeField, Min(0)] private int growthSize;
        /// <summary>身体视觉节点；保留Prefab局部缩放，成长随角色根节点整体缩放。</summary>
        [SerializeField] private Transform body;
        /// <summary>主角唯一动态刚体，由关卡暂停和终止。</summary>
        [SerializeField] private Rigidbody2D physicsBody;
        /// <summary>主角实体圆碰撞体；初始化和成长均保留半径与Offset，世界尺寸随根缩放变化。</summary>
        [SerializeField] private CircleCollider2D circle;
        /// <summary>角色根上的独立排序组，Prefab默认禁用；死亡时启用并脱离祖先排序组，显示于SceneLayer／200。</summary>
        [SerializeField] private SortingGroup deathSortingGroup;
        #endregion

        #region 死亡表现配置
        /// <summary>死亡时相对当前位置向上跳的世界距离；仅用于表现，不恢复物理或碰撞。</summary>
        [SerializeField, Min(0.01f), LabelText("死亡上跳高度")] private float deathHopHeight = 0.6f;
        /// <summary>死亡上跳时长（游戏秒）；暂停和切后台沿用关卡的动画暂停入口。</summary>
        [SerializeField, Min(0.01f), LabelText("死亡上跳时长")] private float deathHopDuration = 0.18f;
        /// <summary>从上跳最高点或FailArea当前位置加速下坠到相机下方的时长（游戏秒）；离屏后才完成死亡回调。</summary>
        [SerializeField, Min(0.01f), LabelText("死亡下坠时长")] private float deathFallDuration = 0.55f;
        #endregion

        #region 状态与入口
        private DigLevelCtrl _level;
        private Vector2 _velocity;
        private float _angularVelocity;
        private bool _frozen = true;
        private bool _dead;
        private Tween _animation;
        private Vector3 _initialRootScale;
        private float _initialSizeScale;
        private float _currentScale = 1f;
        /// <summary>目标触发器读取的主角编号；由 Prefab Inspector 配置，运行中不修改。</summary>
        public int Index => index;
        /// <summary>当前判定大小；不读取身体缩放。</summary>
        public int CurrentSize => currentSize;
        /// <summary>保留的成长收益字段，本版不参与主角自身成长计算。</summary>
        public int GrowthSize => growthSize;

        /// <summary>只由关卡新实例初始化调用；绑定缺失或非法数据使加载失败。</summary>
        public void Initialize(DigLevelCtrl level)
        {
            if (!Application.isPlaying) return;
            if (!body || !physicsBody || !circle || currentSize <= 0 || growthSize < 0 || circle.radius <= 0)
                throw new InvalidOperationException("DigActor Prefab 绑定或大小配置无效。");
            _level = level;
            physicsBody.simulated = false;
            _initialRootScale = transform.localScale;
            _initialSizeScale = DigSizeRules.ScaleFor(currentSize);
            _currentScale = 1f;
            InitializeAnimationState();
        }

        /// <summary>确认吞噬后调用；加值仅执行一次，角色根节点从当前倍率整体插值到目标大小。</summary>
        public void Grow(int amount)
        {
            if (_dead) return;
            currentSize = checked(currentSize + amount);
            _animation?.Kill();
            float from = _currentScale;
            float target = DigSizeRules.ScaleFor(currentSize) / _initialSizeScale;
            _animation = DOVirtual.Float(from, target, 0.22f, ApplyScale).SetEase(Ease.OutQuad);
            PlayEat();
        }

        /// <summary>以当前世界碰撞中心整体缩放并补偿根位置；只消除缩放偏移，不锁定物理移动，不写Body或Collider参数。</summary>
        private void ApplyScale(float scale)
        {
            Vector2 center = circle.transform.TransformPoint(circle.offset);
            transform.localScale = _initialRootScale * scale;
            transform.position += (Vector3)(center - (Vector2)circle.transform.TransformPoint(circle.offset));
            _currentScale = scale;
        }

        /// <summary>仅在一口吞噬完成后唤醒存活且未冻结的刚体，让仍接触的Germ重新上报；不改速度或碰撞参数。</summary>
        internal void WakeForNextContact()
        {
            if (!_dead && !_frozen) physicsBody.WakeUp();
        }

        /// <summary>冻结时保留速度；死亡后的恢复调用无效。</summary>
        public void SetFrozen(bool frozen)
        {
            if (_dead || _frozen == frozen) return;
            _frozen = frozen;
            if (frozen)
            {
                _velocity = physicsBody.linearVelocity;
                _angularVelocity = physicsBody.angularVelocity;
                physicsBody.simulated = false;
                _animation?.Pause();
            }
            else
            {
                physicsBody.simulated = true;
                physicsBody.linearVelocity = _velocity;
                physicsBody.angularVelocity = _angularVelocity;
                _animation?.Play();
            }
        }

        /// <summary>终止物理，保持当前尺寸，先上跳再下坠离屏；完成回调由关卡版本/状态再次核验。</summary>
        /// <param name="levelCamera">关卡显式绑定的正交相机；使用其视口下边界计算离屏终点，不查找场景相机。</param>
        /// <param name="completed">完整演出后调用；失活会取消 Tween，不提交结果。</param>
        public void PlayDeath(Camera levelCamera, Action completed)
            => PlayDeath(levelCamera, completed, true);

        /// <summary>保持当前尺寸并下坠离屏，按失败来源选择是否先上跳；仍由关卡完成回调提交结果。</summary>
        /// <param name="levelCamera">本关显式绑定的相机，用于计算原有离屏终点。</param>
        /// <param name="completed">下坠完成后调用；失活取消Tween时不调用。</param>
        /// <param name="playHop">false跳过上跳段，直接从当前位置下坠；不改变下坠时长。</param>
        public void PlayDeath(Camera levelCamera, Action completed, bool playHop)
        {
            _dead = true;
            if (deathSortingGroup)
            {
                deathSortingGroup.sortingLayerName = "SceneLayer";
                deathSortingGroup.sortingOrder = 200;
                deathSortingGroup.sortAtRoot = true;
                deathSortingGroup.enabled = true;
            }
            DisposeAnimationState();
            physicsBody.simulated = false;
            circle.enabled = false;
            _animation?.Kill();
            float startY = transform.position.y;
            float depth = levelCamera.WorldToViewportPoint(transform.position).z;
            float screenBottom = levelCamera.ViewportToWorldPoint(new Vector3(0.5f, 0f, depth)).y;
            Vector3 worldScale = transform.lossyScale;
            float worldRadius = circle.radius * Mathf.Max(Mathf.Abs(worldScale.x), Mathf.Abs(worldScale.y));
            float centerOffsetY = circle.transform.TransformPoint(circle.offset).y - startY;
            float endY = Mathf.Min(startY, screenBottom - centerOffsetY - worldRadius - 0.1f);
            Sequence sequence = DOTween.Sequence();
            if (playHop)
                sequence.Append(transform.DOMoveY(startY + Mathf.Max(0.01f, deathHopHeight), Mathf.Max(0.01f, deathHopDuration)).SetEase(Ease.OutQuad));
            _animation = sequence
                .Append(transform.DOMoveY(endY, Mathf.Max(0.01f, deathFallDuration)).SetEase(Ease.InQuad))
                .OnComplete(() => completed?.Invoke());
        }

        /// <summary>暂停成长／死亡 Tween 及 Spine；不重新开启物理，胜利 hiccup 可单独恢复 Spine。</summary>
        public void SetAnimationPaused(bool paused)
        {
            if (paused) _animation?.Pause(); else _animation?.Play();
            SetSpinePaused(paused);
        }
        #endregion

        #region Unity 生命周期
        private void OnCollisionEnter2D(Collision2D collision) => ReportContact(collision.collider);
        private void OnCollisionStay2D(Collision2D collision) => ReportContact(collision.collider);
        private void ReportContact(Collider2D other)
        {
            // 以实际碰撞所属刚体识别病菌，支持其Body子节点Collider，不扫描场景或层级。
            if (!_dead && other.attachedRigidbody && other.attachedRigidbody.TryGetComponent(out DigGermCtrl germ))
                _level?.QueueContact(this, germ);
        }
        private void OnDisable() { _animation?.Kill(); _animation = null; DisposeAnimationState(); }
        #endregion
    }
}
