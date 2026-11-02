using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Dig
{
    /// <summary>到达目标模式的场景触发区；上报同关主角的到达并回调局部表现，不打开UI、不写存档或提交全局事件。</summary>
    [DisallowMultipleComponent]
    public sealed class DigTargetTrigger : MonoBehaviour
    {
        #region Inspector 绑定
        /// <summary>所属关卡控制器；在Inspector显式绑定本关DigLevelCtrl，不能通过场景名字搜索。</summary>
        [SerializeField] private DigLevelCtrl level;
        /// <summary>本节点唯一目标Collider2D；Inspector绑定并勾选Is Trigger，不在运行时自动添加或改变碰撞体。</summary>
        [SerializeField] private Collider2D triggerCollider;
        /// <summary>目标编号；必须与所属关卡Actor的Index一致，延迟由所属关卡统一配置。</summary>
        [SerializeField, LabelText("胜利匹配 Index")] private int index;
        /// <summary>每个角色首次被本目标接受后执行的局部表现回调；显式绑定，重复接触或被拒绝时不执行。</summary>
        [Tooltip("每个角色首次到达后执行一次；全部角色到达才结算。可绑定亮灯和仅改变美术节点的动画。")]
        [SerializeField] private UnityEvent onReached = new UnityEvent();
        #endregion

        #region 物理接触
        private void OnTriggerEnter2D(Collider2D other) => ReportArrival(other);
        private void OnTriggerStay2D(Collider2D other) => ReportArrival(other);

        /// <summary>Enter／Stay都使用物理附着刚体确定主角，兼容子碰撞体；由关卡去重并核对编号／模式／结果门禁。</summary>
        private void ReportArrival(Collider2D other)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || !level ||
                !triggerCollider || !triggerCollider.enabled || !triggerCollider.isTrigger || triggerCollider.gameObject != gameObject ||
                !other || !other.attachedRigidbody) return;
            if (other.attachedRigidbody.TryGetComponent(out DigActorCtrl arrivingActor) &&
                level.TryReachTarget(arrivingActor, index))
                onReached.Invoke();
        }
        #endregion
    }
}
