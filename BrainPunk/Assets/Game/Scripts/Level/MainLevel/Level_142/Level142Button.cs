using Game.Turntable;
using UnityEngine;

/// <summary>
/// Level142 的一次性按钮：首次踩下沿用基础下压动画和音效，离开后保持按住。
/// 重开通过主线重建关卡恢复初始状态，不修改其他关卡的公共按钮。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level142Button : PressButtonBase
{
    #region 实际踩稳检测
    [Tooltip("显式绑定按钮随下压移动的实体碰撞盒；用于确认角色已落在压下后的按钮上。")]
    [SerializeField] private Collider2D supportCollider;
    private Collider2D pressingActor;

    /// <summary>
    /// 供本关控制器在可游玩时检测锁定时机：按钮压完且角色实际踩在其上方才允许锁定。
    /// 不以提前命中的触发区或角色预判接地代替物理接触，也不限制角色自然下落。
    /// </summary>
    public bool CanLockActor(Rigidbody2D actor)
    {
        return _isPressed && Mathf.Approximately(GetCurrentState01(), 1f) && pressingActor != null &&
               pressingActor.attachedRigidbody == actor && actor.linearVelocityY <= 0.01f &&
               pressingActor.bounds.min.y >= supportCollider.bounds.max.y - 0.04f &&
               supportCollider.IsTouching(pressingActor);
    }
    #endregion

    #region 踩踏与保持
    protected override void OnTriggerEnter2D(Collider2D other)
    {
        if (!Application.isPlaying || !other.CompareTag("Player")) return;
        pressingActor = other;
        if (_isPressed) return;
        base.OnTriggerEnter2D(other);
    }

    protected override void OnTriggerExit2D(Collider2D other)
    {
        // 本关按钮只压下，不调用基础类的退出复位逻辑。
    }
    #endregion
}
