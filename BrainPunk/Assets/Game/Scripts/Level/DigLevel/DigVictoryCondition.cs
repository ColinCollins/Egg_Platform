namespace Game.Dig
{
    /// <summary>Dig 自然胜利条件；数值0保留旧Prefab的吞噬规则，两种类型共用关卡的延迟结算。</summary>
    public enum DigVictoryCondition
    {
        /// <summary>全部病菌已结算且吞噬动画全部结束才胜利。</summary>
        ConsumeAllGerms = 0,
        /// <summary>本关Actor进入相同index的目标Trigger才胜利，吞噬全清不作为此模式的胜利条件。</summary>
        ReachTarget = 1
    }
}
