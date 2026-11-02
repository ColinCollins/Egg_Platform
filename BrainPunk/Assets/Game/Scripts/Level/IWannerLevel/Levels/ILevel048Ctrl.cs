using Bear.Logger;
using DG.Tweening;
using MoreMountains.Feedbacks;
using UnityEngine;

/// <summary>
/// 第 048 关局部控制器：一次调用让目标依次跳到两个标记点，并播放各段起落反馈。
/// </summary>
[DisallowMultipleComponent]
public sealed class ILevel048Ctrl : MonoBehaviour, IDebuger
{
    #region Inspector 配置

    /// <summary>受控目标，必须显式绑定；本组件直接移动 Transform，不接管动态刚体。</summary>
    [SerializeField] private Transform target;
    /// <summary>第一跳终点；调用 API 时读取世界坐标，必须显式绑定。</summary>
    [SerializeField] private Transform firstJumpPoint;
    /// <summary>第二跳终点；调用 API 时读取世界坐标，必须显式绑定。</summary>
    [SerializeField] private Transform secondJumpPoint;
    /// <summary>每跳的弧线高度，单位为世界单位，必须大于零。</summary>
    [Min(0.01f)] [SerializeField] private float jumpPower = 1f;
    /// <summary>每跳时长（秒），随游戏 timeScale 推进，必须大于零。</summary>
    [Min(0.01f)] [SerializeField] private float jumpDuration = 0.5f;

    /// <summary>两次跳跃的开始与正常完成共用此反馈，共调用四次；Inspector 显式绑定，取消时不补发。</summary>
    [Header("Feedback")]
    [SerializeField] private MMF_Player jumpFeedback;

    #endregion

    #region 运行状态

    private Sequence _jumpSequence;

    #endregion

    #region 对外 API

    /// <summary>
    /// 供本关 Trigger/UnityEvent 调用：连续执行两跳；运行中重复调用忽略。
    /// 仅运行态且组件启用时有效；结束后可再次从当前位置触发，不自动复位。
    /// </summary>
    public void TriggerJump()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || _jumpSequence != null)
            return;

        if (target == null || !target.gameObject.activeInHierarchy ||
            firstJumpPoint == null || secondJumpPoint == null ||
            !(jumpPower > 0f) || float.IsInfinity(jumpPower) ||
            !(jumpDuration > 0f) || float.IsInfinity(jumpDuration))
        {
            this.LogWarning("[ILevel048Ctrl] 请绑定 Target、两个跳跃终点，并配置有效的正数高度和时长。");
            return;
        }

        Vector3 startPosition = target.position;
        Vector3 firstPosition = firstJumpPoint.position;
        Vector3 secondPosition = secondJumpPoint.position;
        Sequence sequence = DOTween.Sequence();
        _jumpSequence = sequence;
        sequence.SetEase(Ease.Linear);
        // 两跳由本控制器持有，避免门下 Trigger 的 Transform.DOKill 提前终止整段演出。
        sequence.SetTarget(this);
        sequence.SetLink(target.gameObject, LinkBehaviour.KillOnDisable);
        sequence.OnKill(() =>
        {
            if (_jumpSequence == sequence)
                _jumpSequence = null;
        });
        sequence.AppendCallback(PlayFeedback);
        sequence.Append(CreateJump(startPosition, firstPosition));
        sequence.AppendCallback(PlayFeedback);
        sequence.AppendCallback(PlayFeedback);
        sequence.Append(CreateJump(firstPosition, secondPosition));
        sequence.AppendCallback(PlayFeedback);
    }

    #endregion

    #region 跳跃编排

    private Tween CreateJump(Vector3 startPosition, Vector3 endPosition)
    {
        float progress = 0f;
        float height = jumpPower;
        // 显式指定两段起终点并加入同一时间轴，段间不插入等待或回调创建的新 Tween。
        return DOTween.To(() => progress, value =>
        {
            progress = value;
            if (target != null)
                target.position = Vector3.LerpUnclamped(startPosition, endPosition, value)
                    + Vector3.up * (4f * height * value * (1f - value));
        }, 1f, jumpDuration).SetEase(Ease.Linear);
    }

    #endregion

    #region 生命周期与反馈

    private void OnDisable()
    {
        _jumpSequence?.Kill();
        _jumpSequence = null;
    }

    private void OnDestroy()
    {
        _jumpSequence?.Kill();
        _jumpSequence = null;
    }

    private void PlayFeedback()
    {
        if (jumpFeedback != null)
            jumpFeedback.PlayFeedbacks();
    }

    #endregion
}
