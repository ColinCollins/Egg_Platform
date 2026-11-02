using Bear.SaveModule;

public static partial class DB
{
    /// <summary>
    /// 用途：获取 IWanner 玩法独立存档实例。
    /// 调用边界：仅可在 DBManager 完成运行时初始化后访问。
    /// 注意事项：不得与主线 GameData 或 DrawGame 的进度字段混用。
    /// </summary>
    public static IWannerGame IWannerGame => DBManager.Instance.Get<IWannerGame>();

    /// <summary>
    /// 用途：保存 IWanner 玩法独立存档。
    /// 调用边界：只在 IWanner 运行时进度发生变化后调用。
    /// 注意事项：返回 false 时调用方必须记录未落盘风险，不得宣称持久化成功。
    /// </summary>
    /// <returns>存档写入成功时返回 true。</returns>
    public static bool SaveIWannerGame()
    {
        return DBManager.Instance.Save<IWannerGame>();
    }
}
