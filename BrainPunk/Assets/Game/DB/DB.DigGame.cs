using Bear.SaveModule;

public static partial class DB
{
    /// <summary>DBManager 运行时初始化后取得 Dig 独立存档，不允许编辑器预览访问。</summary>
    public static DigGame DigGame => DBManager.Instance.Get<DigGame>();
    /// <summary>只保存 Dig 进度，不写入 Main/Draw/IWanner。</summary>
    public static bool SaveDigGame() => DBManager.Instance.Save<DigGame>();
}
