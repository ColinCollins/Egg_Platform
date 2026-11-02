using Game.Level;

/// <summary>保留本关入场初始化；正常失败/死亡与成功均沿共享结算，每次尝试只提交一次。</summary>
public sealed class Level140BaseLevelCtrl : BaseLevelCtrl
{
    private bool resultCommitted;

    private void Awake()
    {
        if (Actor == null) SetActor(GetComponentInChildren<Level140ActorCtrl>(true));
        SetCtrlable(false);
    }

    private void Start()
    {
        AddListener();
        isPause = false;
        Actor?.ResumeMoving();
    }

    /// <summary>由共享关卡更新链检查本次尝试结果；成功、共享失败或 Actor 死亡只提交一次，机器故障本身不进入结算。</summary>
    public override void CheckFinished()
    {
        // FallArea 由基类 WaitingFail 延迟设置失败标记，不能屏蔽 IsFail 或死亡分支。
        // 机器故障/食物耗尽不设置失败标记，因此不会因此自动重开。
        if (resultCommitted || Actor == null) return;
        if (!IsFail() && !Actor.IsDied && !IsSuccess()) return;
        resultCommitted = true;
        base.CheckFinished();
    }
}
