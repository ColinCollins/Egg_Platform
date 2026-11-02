namespace Game.Play
{
    /// <summary>
    /// 可被通用暂停弹窗控制的玩法能力。
    /// 用途：让 PausePopup 复用暂停、恢复和返回当前玩法选关页行为，而不依赖具体 PlayCtrl。
    /// 调用边界：只操作当前已激活的玩法控制器；非激活控制器必须忽略调用。
    /// </summary>
    public interface IPlayPauseHost
    {
        /// <summary>
        /// 暂停当前玩法。
        /// </summary>
        void Pause();

        /// <summary>
        /// 恢复当前玩法。
        /// </summary>
        void Resume();

        /// <summary>
        /// 退出当前关卡并返回当前玩法对应的选关页。
        /// </summary>
        void ExitToLevelSelection();
    }
}
