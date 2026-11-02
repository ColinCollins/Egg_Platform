namespace Game.Play
{
    /// <summary>
    /// 玩法控制器提供给 GMPanel 的统一调试能力。
    /// 用途：让 GMPanel 只依赖能力接口，不感知 Main、Draw、IWanner 等具体控制器类型。
    /// 注意事项：玩法不支持或当前运行态不能执行某项命令时返回 false，不得转而修改其它玩法状态。
    /// </summary>
    public interface IGMFeature
    {
        /// <summary>
        /// 尝试进入当前玩法定义的下一关。
        /// </summary>
        /// <returns>命令被当前玩法接受时返回 true，否则返回 false。</returns>
        bool TryEnterNextLevel();

        /// <summary>
        /// 尝试进入当前玩法定义的上一关。
        /// </summary>
        /// <returns>命令被当前玩法接受时返回 true，否则返回 false。</returns>
        bool TryEnterPreviousLevel();

        /// <summary>
        /// 尝试通过当前玩法正在运行的关卡。
        /// </summary>
        /// <param name="source">调试命令来源，用于具体玩法按需记录。</param>
        /// <returns>胜利命令被当前玩法首次接受时返回 true，否则返回 false。</returns>
        bool TryForceVictory(string source);
    }
}
