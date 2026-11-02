using Bear.EventSystem;
using Game.Events;
using UnityEngine;

/// <summary>岩浆关卡局部 FallArea；仅接受显式绑定的主角，沿用共享失败/重开事件链。</summary>
public sealed class Level9LavaFallArea : MonoBehaviour, IEventSender
{
    #region 绑定与状态
    [SerializeField, Tooltip("本关唯一受控主角；禁止按名称或层级查找。")]
    private Level9ActorCtrl actor;
    [SerializeField, Tooltip("主角根刚体，用于排除工具、其它物体和误触发。")]
    private Rigidbody2D actorBody;
    private bool triggered;
    #endregion

    #region 触发
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!Application.isPlaying || triggered || actor == null || actorBody == null ||
            other.attachedRigidbody != actorBody || actor.IsDied) return;
        triggered = true;
        // 先派发标准 FallArea，再标记死亡；不另发 Reset，不改变统计或失败等待。
        this.DispatchEvent(Witness<OnTriggerFailAreaEvent>._);
        actor.BeginLavaBurn();
    }
    #endregion
}
