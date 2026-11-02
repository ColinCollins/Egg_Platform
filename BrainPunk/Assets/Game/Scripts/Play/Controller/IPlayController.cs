using System;
using GameCommon;

namespace Game.Play
{
    #region Context

    /// <summary>
    /// 玩法控制器共享运行时依赖。
    /// 用途：由 GameManager 统一组装，再交给各玩法控制器初始化。
    /// 注意事项：这里只放跨玩法稳定依赖，不放主线或画线的专属关卡数据。
    /// </summary>
    public sealed class PlayControllerContext
    {
        /// <summary>
        /// 全玩法共享的玩家背包。
        /// </summary>
        public SimpleBag Bag { get; }

        /// <summary>
        /// 创建玩法控制器上下文。
        /// </summary>
        /// <param name="bag">由 GameManager 持有的共享背包实例。</param>
        public PlayControllerContext(SimpleBag bag)
        {
            Bag = bag ?? throw new ArgumentNullException(nameof(bag));
        }
    }

    #endregion

    #region Controller Contract

    /// <summary>
    /// 单个玩法控制器的统一生命周期接口。
    /// 用途：隔离 GameManager 与具体玩法实现，由 PlayControllerManager 统一初始化、切换和更新。
    /// </summary>
    public interface IPlayController : IPlayPauseHost
    {
        /// <summary>
        /// 当前控制器负责的玩法类型。
        /// </summary>
        PlayModeType Mode { get; }

        /// <summary>
        /// 控制器是否已完成一次性初始化。
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 控制器当前是否为激活玩法。
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// 注入共享依赖并执行一次性初始化。
        /// </summary>
        /// <param name="context">由 GameManager 创建的共享上下文。</param>
        void Initialize(PlayControllerContext context);

        /// <summary>
        /// 激活控制器并开始接收玩法输入与事件。
        /// </summary>
        void Activate();

        /// <summary>
        /// 停用控制器并停止接收玩法输入与事件。
        /// 注意事项：玩法专属场景和 UI 清理策略由具体控制器负责。
        /// </summary>
        void Deactivate();

        /// <summary>
        /// 驱动当前激活控制器的逐帧逻辑。
        /// </summary>
        void Tick();

        /// <summary>
        /// 释放控制器持有的事件、状态机和运行时资源。
        /// </summary>
        void Shutdown();
    }

    #endregion
}
