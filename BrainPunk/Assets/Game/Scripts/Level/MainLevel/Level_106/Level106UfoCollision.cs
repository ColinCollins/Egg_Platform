using UnityEngine;

/// <summary>
/// Level 106 UFO 杆体承载碰撞。
/// 该碰撞体只提供杆子一带的实体站立面，不触发死亡；送层物件自身负责踩踏判定。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level106UfoCollision : MonoBehaviour
{
    [SerializeField] private Level106Ctrl levelController;
    [SerializeField] private ParticleSystem knockEffect;

    private Collider2D wholeCollider;

    private void Awake()
    {
        wholeCollider = GetComponent<Collider2D>();
        if (levelController == null)
        {
            levelController = GetComponentInParent<Level106Ctrl>();
        }
    }

    private void OnEnable()
    {
        if (wholeCollider != null)
        {
            wholeCollider.enabled = false;
        }
    }

    private void OnDisable()
    {
        if (wholeCollider != null)
        {
            wholeCollider.enabled = false;
        }

        if (knockEffect != null)
        {
            knockEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    internal void SetPlatformActive(bool isActive)
    {
        if (wholeCollider != null)
        {
            wholeCollider.enabled = isActive;
        }
    }

    /// <summary>
    /// 新关卡实例启用时关闭 UFO 承载碰撞和残留表现；不得用于 Reset/Replay 旧实例复位。
    /// </summary>
    internal void InitializeRuntimeState()
    {
        SetPlatformActive(false);
        if (knockEffect != null)
        {
            knockEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    internal void PlayKnock(Vector3 worldPosition)
    {
        if (knockEffect == null)
        {
            return;
        }

        knockEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        knockEffect.transform.position = worldPosition;
        knockEffect.Play(true);
    }
}
