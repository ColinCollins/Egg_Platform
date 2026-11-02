using Bear.SaveModule;

public static partial class DB
{
    /// <summary>
    /// 获取 Draw 玩法独立存档实例。
    /// 调用边界：仅可在 DBManager 完成运行时初始化后访问。
    /// </summary>
    public static DrawGame DrawGame => DBManager.Instance.Get<DrawGame>();

    /// <summary>
    /// 保存 Draw 玩法独立存档，不会写入主线 GameData。
    /// </summary>
    /// <returns>存档写入成功时返回 true。</returns>
    public static bool SaveDrawGame()
    {
        return DBManager.Instance.Save<DrawGame>();
    }
}
