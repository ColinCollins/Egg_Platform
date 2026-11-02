using Bear.EventSystem;
using Game.Events;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// 刺石（Sp_l103_dj_01）碰撞判负组件：主角主体物理接触刺石 trigger 时，在接触点播放
/// Knock_Explosion，并同帧播放 "trap"、"failed" 音效，随后沿既有死亡链
/// （OnTriggerTrapEvent → BaseLevelCtrl 死亡动画 → 默认失败重开）收口。
/// 用于 Level 103 头顶刺石与 Level 104 执法刺：输入事件只负责让刺石瞬现并固定，
/// 组件随 103 关卡目录存放（三部曲共用执法刺，同 OnTriggerTrapHandle 居 Level_27 而服务共享 Caltrop 的先例），不占用 Level 框架共享层。
/// 判负以真实碰撞为准（“撞到才死”）；组件在每次激活时重置触发门禁，保证失败重开后可再次触发。
/// </summary>
public class SpikeStoneTrapHandle : MonoBehaviour, IEventSender
{
    [SerializeField] private ParticleSystem impactEffect;

    private bool hasFired;
    private Collider2D sourceCollider;

    private void OnEnable()
    {
        hasFired = false;
        sourceCollider ??= GetComponent<Collider2D>();
        impactEffect?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (hasFired || collider == null)
            return;

        // 与 OnTriggerTrapHandle（Caltrop 地刺）同款主角识别：Player 标签 + Actor 层。
        if (!collider.gameObject.CompareTag("Player"))
            return;

        if (!string.Equals(LayerMask.LayerToName(collider.gameObject.layer), "Actor"))
            return;

        hasFired = true;
        PlayImpact(CalculateContactPoint(collider));
        AudioManager.PlaySound("trap");
        AudioManager.PlaySound("failed");
        this.DispatchEvent(Witness<OnTriggerTrapEvent>._);
    }

    /// <summary>
    /// Trigger 没有 Collision2D.contactCount；用双方 Collider 的最近点中点稳定近似首次接触区域。
    /// </summary>
    private Vector3 CalculateContactPoint(Collider2D targetCollider)
    {
        sourceCollider ??= GetComponent<Collider2D>();

        Vector2 sourceCenter = sourceCollider != null
            ? sourceCollider.bounds.center
            : (Vector2)transform.position;
        Vector2 targetPoint = targetCollider.ClosestPoint(sourceCenter);
        Vector2 sourcePoint = sourceCollider != null
            ? sourceCollider.ClosestPoint(targetPoint)
            : sourceCenter;
        Vector2 contactPoint = (sourcePoint + targetPoint) * 0.5f;
        float z = impactEffect != null ? impactEffect.transform.position.z : transform.position.z;
        return new Vector3(contactPoint.x, contactPoint.y, z);
    }

    private void PlayImpact(Vector3 impactPosition)
    {
        if (impactEffect == null)
            return;

        impactEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        impactEffect.transform.position = impactPosition;
        impactEffect.Play(true);
    }
}
