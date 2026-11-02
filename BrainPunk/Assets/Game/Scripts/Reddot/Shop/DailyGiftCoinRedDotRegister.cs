/// <summary>
/// 每日免费金币红点注册器。
/// 调用边界：在 DB 与 Config 初始化完成后，由 Loading 的红点初始化流程显式注册。
/// </summary>
public sealed class DailyGiftCoinRedDotRegister : IRedDotRegister
{
    #region IRedDotRegister

    /// <summary>
    /// 注册 DailyGiftCoin 节点及其只读业务判断器。
    /// </summary>
    /// <param name="registry">红点注册入口。</param>
    public void Register(IRedDotRegistry registry)
    {
        if (registry == null)
        {
            return;
        }

        registry.RegisterNode(RedDotKey.DailyGiftCoin);
        registry.RegisterChecker(RedDotKey.DailyGiftCoin, new DailyGiftCoinRedDotChecker());
    }

    #endregion
}
