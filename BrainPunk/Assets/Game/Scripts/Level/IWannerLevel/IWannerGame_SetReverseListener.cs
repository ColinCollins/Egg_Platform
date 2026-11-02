using Bear.Logger;
using Game.ItemEvent;
using UnityEngine;

/// <summary>
/// IWanner 左右移动反向状态设置监听器。
/// 由 TriggerExecutor/BaseItemExecutor 触发后，将配置值写入目标 IWannerLevelCtrl。
/// </summary>
public sealed class IWannerGame_SetReverseListener : BaseItemEventHandle, IDebuger
{
    #region Serialized Fields

    /// <summary>
    /// 用途：指定需要切换左右移动映射的 IWanner 小关控制器。
    /// 调用边界：仅在 Execute 时读取；未配置时会从当前对象的父级层级自动查找。
    /// 注意事项：应指向当前活动小关实例，避免修改非活动或即将销毁的关卡。
    /// </summary>
    [Tooltip("需要切换左右移动映射的 IWanner 小关控制器；留空时从父级自动查找")]
    [SerializeField] private IWannerLevelCtrl levelCtrl;

    /// <summary>
    /// 用途：配置本监听器触发后写入的左右移动反向状态。
    /// 调用边界：true 表示交换左右移动事件，false 表示恢复默认映射。
    /// 注意事项：若同一关卡需要开启和关闭反向，应分别配置两个监听器并设置不同值。
    /// </summary>
    [Tooltip("触发后是否交换左右移动事件")]
    [SerializeField] private bool isReverse = true;

    #endregion

    #region BaseItemEventHandle

    /// <summary>
    /// 用途：把配置的反向状态写入当前 IWanner 小关。
    /// 调用边界：由 TriggerExecutor/BaseItemExecutor 或明确的关卡本地逻辑调用。
    /// 注意事项：找不到目标时只记录警告并结束，避免顺序执行器卡住。
    /// </summary>
    public override void Execute()
    {
        IsRunning = true;
        IsDone = false;

        IWannerLevelCtrl targetLevelCtrl = ResolveLevelCtrl();
        if (targetLevelCtrl == null)
        {
            this.LogWarning("[IWannerGame_SetReverseListener] IWannerLevelCtrl is missing.");
            CompleteExecution();
            return;
        }

        targetLevelCtrl.SetReverse(isReverse);
        CompleteExecution();
    }

    #endregion

    #region Internal

    private IWannerLevelCtrl ResolveLevelCtrl()
    {
        if (levelCtrl != null)
            return levelCtrl;

        levelCtrl = GetComponentInParent<IWannerLevelCtrl>(true);
        return levelCtrl;
    }

    private void CompleteExecution()
    {
        IsRunning = false;
        IsDone = true;
    }

    #endregion
}
