/// <summary>
/// 新皮肤红点判断器。待预览皮肤列表非空时显示红点。
/// 调用边界：仅由 RedDotManager 在运行时刷新红点时调用。
/// 注意事项：本类只读取 GameData.WaitingPreviewSkinId，不写存档、不打开 UI、不加载资源。
/// </summary>
public sealed class NewSkinRedDotChecker : IRedDotChecker
{
    #region IRedDotChecker

    /// <summary>
    /// 判断待预览皮肤列表中是否仍有数据。
    /// </summary>
    /// <returns>true 表示显示新皮肤红点；false 表示隐藏。</returns>
    public bool IsOn()
    {
        return NewSkinRedDotQuery.HasAny();
    }

    #endregion
}
