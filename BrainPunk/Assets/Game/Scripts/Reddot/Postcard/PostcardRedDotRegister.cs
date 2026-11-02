/// <summary>
/// 明信片红点注册器。负责把 Postcard 节点和对应 checker 注册到 RedDotManager。
/// 调用边界：应在 DB、Config、PlayCtrl 背包数据初始化完成后，由运行时初始化流程显式注册。
/// 注意事项：本注册器不主动刷新红点，调用方可使用 RedDotRegisterHub.RegisterAndRefresh(...) 完成注册与首次刷新。
/// </summary>
public sealed class PostcardRedDotRegister : IRedDotRegister
{
    #region IRedDotRegister

    /// <summary>
    /// 注册明信片红点节点和业务判断器。
    /// </summary>
    /// <param name="registry">红点注册入口。</param>
    public void Register(IRedDotRegistry registry)
    {
        if (registry == null)
        {
            return;
        }

        registry.RegisterNode(RedDotKey.Postcard);
        registry.RegisterChecker(RedDotKey.Postcard, new PostcardRedDotChecker());
    }

    #endregion
}
