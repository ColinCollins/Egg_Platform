using Game.ItemEvent;
using UnityEngine;

/// <summary>
/// Level099 问号块事件链的收尾节点：只登记本关计数，不修改玩家持久金币。
/// </summary>
public sealed class Level99QuestionBlockReward : BaseItemEventHandle
{
    /// <summary>接收本次顶块计数的 Level099 私有控制器。</summary>
    [SerializeField] private Level99Ctrl levelCtrl;

    private void Awake()
    {
        levelCtrl ??= GetComponentInParent<Level99Ctrl>();
    }

    /// <summary>
    /// 在既有金币音效、反馈和等待节点完成后登记一次有效金币，并立即结束当前节点。
    /// </summary>
    public override void Execute()
    {
        IsRunning = true;
        levelCtrl ??= GetComponentInParent<Level99Ctrl>();
        levelCtrl?.TryRegisterQuestionBlockCoin();
        IsRunning = false;
        IsDone = true;
    }
}
