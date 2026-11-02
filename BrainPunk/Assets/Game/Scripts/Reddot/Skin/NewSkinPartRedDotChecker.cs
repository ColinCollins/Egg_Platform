using Config.Game;

/// <summary>
/// 指定 SkinPart 的新皮肤红点判断器。
/// 调用边界：由 NewSkinRedDotRegister 为固定部位节点创建，只读取待预览列表和 Skin 配置。
/// </summary>
public sealed class NewSkinPartRedDotChecker : IRedDotChecker
{
    #region Fields

    private readonly SkinPart _skinPart;

    #endregion

    #region Constructor

    /// <summary>
    /// 创建指定皮肤部位的红点判断器。
    /// </summary>
    /// <param name="skinPart">需要检查的皮肤部位。</param>
    public NewSkinPartRedDotChecker(SkinPart skinPart)
    {
        _skinPart = skinPart;
    }

    #endregion

    #region IRedDotChecker

    /// <summary>
    /// 判断当前部位是否存在待预览皮肤。
    /// </summary>
    /// <returns>true 表示显示该部位红点；false 表示隐藏。</returns>
    public bool IsOn()
    {
        return NewSkinRedDotQuery.HasAny(_skinPart);
    }

    #endregion
}
