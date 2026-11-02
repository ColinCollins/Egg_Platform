using Game.ItemEvent;
using UnityEngine;

/// <summary>
/// Level102 Key 标准执行链中的等待节点。
/// Level002 会在飞行完成后等待 DoorLock 解锁执行器，再执行 Key 销毁；
/// 本关的解锁由 Level102Ctrl 状态机接管，因此用该节点等待同一完成状态。
/// </summary>
public sealed class Level102KeyUnlockWait : BaseItemEventHandle
{
    [SerializeField] private Level102Ctrl levelCtrl;

    public override void Execute()
    {
        ResolveController();
        if (levelCtrl == null)
        {
            Debug.LogWarning("[Level102KeyUnlockWait] Level102Ctrl 引用缺失，跳过解锁等待。", this);
            Complete();
            return;
        }

        IsRunning = true;
        IsDone = levelCtrl.IsDoorUnlocked;
        if (IsDone)
            IsRunning = false;
    }

    private void Update()
    {
        if (!IsRunning || IsDone)
            return;

        ResolveController();
        if (levelCtrl == null || levelCtrl.IsDoorUnlocked)
            Complete();
    }

    private void ResolveController()
    {
        if (levelCtrl == null)
            levelCtrl = GetComponentInParent<Level102Ctrl>();
    }

    private void Complete()
    {
        IsRunning = false;
        IsDone = true;
    }
}
