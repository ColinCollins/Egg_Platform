using Bear.EventSystem;
using Game.Events;
using Game.Play;
using UnityEngine;

/// <summary>
/// 玩家进入成功区域时提交当前玩法的胜利结果。
/// 调用边界：只接受 Actor Layer 且 Player Tag 的碰撞体；位于 DrawLevel 下时优先调用
/// DrawLevelCtrl，否则保留主线 SwitchGameStateEvent 胜利入口。
/// 注意事项：单个组件实例只会成功提交一次；该入口直接结算，不负责播放门动画。
/// </summary>
public class OnTriggerSuccessCtrl : MonoBehaviour, IEventSender
{
    #region Inspector

    /// <summary>
    /// 是否使用 Collision2D 入口；默认 false 时使用 Trigger2D，两个入口互斥。
    /// </summary>
    [SerializeField] private bool isCollision;

    /// <summary>
    /// 当前组件所属 DrawGame 关卡的本地状态控制器。
    /// 调用边界：仅当组件位于 DLevel 层级时绑定；为空时运行时从父层级解析，
    /// 仍为空则按主线胜利事件处理。
    /// </summary>
    [Tooltip("可选。当前 DLevel 根节点上的 DrawLevelCtrl；主线关卡保持为空。")]
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;

    #endregion

    #region Runtime State

    private bool isTriggered;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (isCollision)
            return;

        TryTriggerSuccess(collider);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isCollision)
            return;

        TryTriggerSuccess(collision.collider);
    }

    #endregion

    #region Success Trigger

    private void TryTriggerSuccess(Collider2D collider)
    {
        if (!Application.isPlaying || isTriggered || collider == null)
            return;

        string layerName = LayerMask.LayerToName(collider.gameObject.layer);
        if (!layerName.Equals("Actor") || !collider.gameObject.CompareTag("Player"))
            return;

        ResolveReferences();
        if (drawLevelCtrl != null)
        {
            if (drawLevelCtrl.TryResolve(new DrawLevelResult(
                    DrawLevelResultType.Success,
                    nameof(OnTriggerSuccessCtrl))))
            {
                isTriggered = true;
            }

            return;
        }

        isTriggered = true;
        this.DispatchEvent(Witness<SwitchGameStateEvent>._, GamePlayStateName.SUCCESS);
    }

    #endregion

    #region Reference Resolution

    private void ResolveReferences()
    {
        if (drawLevelCtrl == null)
            drawLevelCtrl = GetComponentInParent<DrawLevelCtrl>(true);
    }

    #endregion
}
