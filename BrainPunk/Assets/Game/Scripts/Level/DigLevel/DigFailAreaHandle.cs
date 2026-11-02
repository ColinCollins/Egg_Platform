using UnityEngine;

namespace Game.Dig
{
    /// <summary>Dig失败区域及陷阱的公共基类；可直接挂载为FailArea，只提交统一失败，不播放陷阱音效或粒子。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Game/Dig/Dig Fail Area Handle")]
    public class DigFailAreaHandle : MonoBehaviour
    {
        #region Inspector 绑定
        /// <summary>所属关卡根控制器，必须显式绑定；失败结算、角色死亡和重开仍由关卡原流程负责。</summary>
        [SerializeField] private DigLevelCtrl level;
        /// <summary>同节点失败区域Collider2D；保留trapCollider序列化名以兼容原Trap资源，支持Trigger与普通碰撞。</summary>
        [SerializeField, InspectorName("失败碰撞体")] private Collider2D trapCollider;
        #endregion

        #region 物理接触与统一失败
        /// <summary>失败区域直接下坠离屏；需要原死亡上跳的陷阱子类覆写，不改变统一结算流程。</summary>
        protected virtual bool PlayDeathHop => false;
        /// <summary>本区域首次接触时上报；子类继承消息入口，无需重复实现判定。</summary>
        protected void OnTriggerEnter2D(Collider2D other) => ReportContact(other);
        /// <summary>持续接触用于暂停恢复等情形；统一结果锁会拒绝重复失败。</summary>
        protected void OnTriggerStay2D(Collider2D other) => ReportContact(other);
        /// <summary>普通碰撞使用实际接触点，不修改Collider配置。</summary>
        protected void OnCollisionEnter2D(Collision2D collision) => ReportCollision(collision);
        /// <summary>普通持续碰撞沿同一结果门禁处理。</summary>
        protected void OnCollisionStay2D(Collision2D collision) => ReportCollision(collision);

        private void ReportCollision(Collision2D collision)
        {
            Vector2? impactPoint = collision.contactCount > 0 ? collision.GetContact(0).point : (Vector2?)null;
            ReportContact(collision.collider, impactPoint);
        }

        private void ReportContact(Collider2D other, Vector2? impactPoint = null)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !level ||
                !trapCollider || !trapCollider.enabled || trapCollider.gameObject != gameObject ||
                !other || !other.attachedRigidbody) return;
            if (!other.attachedRigidbody.TryGetComponent(out DigActorCtrl failingActor)) return;
            // TryFail会关闭角色碰撞体，必须在提交前读取接触位置。
            Vector2 point = impactPoint ?? trapCollider.ClosestPoint(other.bounds.center);
            if (!level.TryFail(failingActor, 0f, PlayDeathHop)) return;
            OnFailureAccepted(point);
        }

        /// <summary>仅首次有效失败被关卡接受后调用；基类无额外表现，子类可追加音效／粒子，不应再次提交失败。</summary>
        /// <param name="point">关闭角色碰撞前获取的世界接触位置。</param>
        protected virtual void OnFailureAccepted(Vector2 point) { }
        #endregion
    }
}
