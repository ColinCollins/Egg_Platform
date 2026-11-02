using Bear.SaveModule;

public static partial class DB
{
    /// <summary>
    /// 获取 Gift 系统独立存档实例。
    /// 调用边界：仅可在 DBManager 完成运行时初始化后访问。
    /// </summary>
    public static GiftData GiftData => DBManager.Instance.Get<GiftData>();

    /// <summary>
    /// 保存 Gift 系统独立存档，不再把 Gift 专属状态分散写入 GameData 或 GameSchedule。
    /// </summary>
    /// <returns>存档写入成功时返回 true。</returns>
    public static bool SaveGiftData()
    {
        return DBManager.Instance.Save<GiftData>();
    }
}
