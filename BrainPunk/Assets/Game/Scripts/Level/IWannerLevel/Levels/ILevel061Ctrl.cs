using Bear.EventSystem;
using Bear.Logger;
using Game.Events;
using UnityEngine;

/// <summary>
/// IWanner 第 061 关局部控制器：记住玩家最后一次左右移动方向，松开按键后仍维持角色的基础水平移动速度。
/// </summary>
[DisallowMultipleComponent]
public sealed class ILevel061Ctrl : MonoBehaviour, IDebuger
{
    #region Serialized Fields

    /// <summary>
    /// 用途：指定第 061 关需要保持水平速度的 IWanner 角色。
    /// 调用边界：仅在收到左右移动事件后，由本控制器通过角色所属关卡控制器重设移动输入并写入水平速度。
    /// 注意事项：留空时会从当前关卡子层级自动查找；垂直速度始终保持不变。
    /// </summary>
    [Header("References")]
    [Tooltip("第 061 关需要保持水平速度的 IWanner 角色；留空时从当前关卡子层级查找")]
    [SerializeField] private Actor_IWanner actor;

    /// <summary>
    /// 用途：复用当前 IWanner 小关的运行时输入、暂停与结算门禁。
    /// 调用边界：每帧保持移动前调用 SetMoveInput；控制器拒绝输入时不会继续写入速度。
    /// 注意事项：留空时从当前对象或父层级查找，不绕过 IWannerLevelCtrl 的生命周期状态。
    /// </summary>
    [Tooltip("当前第 061 关的 IWannerLevelCtrl；留空时自动查找")]
    [SerializeField] private IWannerLevelCtrl levelCtrl;

    #endregion

    #region Runtime State

    private EventSubscriber _subscriber;
    private Rigidbody2D _actorBody;
    private float _maintainedDirection;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        _maintainedDirection = 0f;

        EventsUtils.ResetEvents(ref _subscriber);
        _subscriber.Subscribe<PlayerRightMoveEvent>(OnPlayerRightMove);
        _subscriber.Subscribe<PlayerLeftMoveEvent>(OnPlayerLeftMove);

        if (actor == null || levelCtrl == null)
        {
            this.LogWarning(
                "[ILevel061Ctrl] Actor_IWanner or IWannerLevelCtrl is missing; horizontal velocity cannot be maintained.");
        }
    }

    private void LateUpdate()
    {
        if (Mathf.Approximately(_maintainedDirection, 0f) || actor == null || levelCtrl == null || actor.IsDied)
            return;

        levelCtrl.SetMoveInput(_maintainedDirection);
        if (!Mathf.Approximately(actor.XInput, _maintainedDirection))
            return;

        if (_actorBody == null || !_actorBody.simulated || _actorBody.bodyType == RigidbodyType2D.Static)
            return;

        float horizontalVelocity = _maintainedDirection * Mathf.Abs(actor.MoveSpeed);
        _actorBody.linearVelocity = new Vector2(horizontalVelocity, _actorBody.linearVelocityY);
    }

    private void OnDisable()
    {
        EventsUtils.ResetEvents(ref _subscriber);
        _maintainedDirection = 0f;
    }

    #endregion

    #region Player Input Events

    private void OnPlayerRightMove(PlayerRightMoveEvent evt)
    {
        SetMaintainedDirection(1f);
    }

    private void OnPlayerLeftMove(PlayerLeftMoveEvent evt)
    {
        SetMaintainedDirection(-1f);
    }

    private void SetMaintainedDirection(float inputDirection)
    {
        if (actor == null || levelCtrl == null || actor.IsDied)
            return;

        float direction = levelCtrl.IsReverse
            ? -Mathf.Sign(inputDirection)
            : Mathf.Sign(inputDirection);
        levelCtrl.SetMoveInput(direction);
        if (Mathf.Approximately(actor.XInput, direction))
            _maintainedDirection = direction;
    }

    #endregion

    #region Runtime Helpers

    private void ResolveReferences()
    {
        if (levelCtrl == null)
            levelCtrl = GetComponent<IWannerLevelCtrl>();

        if (levelCtrl == null)
            levelCtrl = GetComponentInParent<IWannerLevelCtrl>();

        if (actor == null)
            actor = GetComponentInChildren<Actor_IWanner>(true);

        if (actor != null)
        {
            _actorBody = actor.RB != null
                ? actor.RB
                : actor.GetComponent<Rigidbody2D>();
        }
    }

    #endregion
}
