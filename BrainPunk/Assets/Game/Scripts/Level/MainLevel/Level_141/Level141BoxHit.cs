using Game.ItemEvent;
using UnityEngine;

/// <summary>006 同款下向 CircleCast 的唯一业务出口；不保留金币或三次切换逻辑。</summary>
public sealed class Level141BoxHit : BaseItemEventHandle
{
    // 由 Level 141 Prefab 绑定本关总控；不允许指向其它关卡实例。
    [SerializeField] private Level141Ctrl owner;

    /// <summary>
    /// 将共享 ItemEvent 的单次接触回调转发给本关盒子控制器，并结束当前事件执行。
    /// 不在事件出口中实现产种规则，具体冷却和种植状态由 <see cref="Level141Ctrl.HitSeedBox"/> 判断。
    /// </summary>
    public override void Execute()
    {
        owner?.HitSeedBox();
        IsRunning = false;
        IsDone = true;
    }
}
