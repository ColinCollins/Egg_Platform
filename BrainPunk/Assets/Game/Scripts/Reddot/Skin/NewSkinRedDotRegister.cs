using Config.Game;

/// <summary>
/// 新皮肤红点注册器。负责注册 NewSkin 总节点、SkinPart 子节点和对应 checker。
/// 调用边界：应在 DB 和 Config 初始化完成后，由运行时初始化流程显式注册。
/// 注意事项：本注册器不主动刷新红点，调用方可使用 RedDotRegisterHub.RegisterAndRefresh(...) 完成注册与首次刷新。
/// </summary>
public sealed class NewSkinRedDotRegister : IRedDotRegister
{
    #region IRedDotRegister

    /// <summary>
    /// 注册新皮肤总节点、固定 SkinPart 子节点和业务判断器。
    /// </summary>
    /// <param name="registry">红点注册入口。</param>
    public void Register(IRedDotRegistry registry)
    {
        if (registry == null)
        {
            return;
        }

        registry.RegisterNode(RedDotKey.NewSkin);
        registry.RegisterChecker(RedDotKey.NewSkin, new NewSkinRedDotChecker());

        RegisterPart(registry, RedDotKey.NewSkinBack, SkinPart.Back);
        RegisterPart(registry, RedDotKey.NewSkinFace, SkinPart.Face);
        RegisterPart(registry, RedDotKey.NewSkinBody, SkinPart.Body);
        RegisterPart(registry, RedDotKey.NewSkinFullBody, SkinPart.FullBody);
    }

    #endregion

    #region Helpers

    private static void RegisterPart(IRedDotRegistry registry, RedDotKey key, SkinPart skinPart)
    {
        registry.RegisterNode(key, RedDotKey.NewSkin);
        registry.RegisterChecker(key, new NewSkinPartRedDotChecker(skinPart));
    }

    #endregion
}
