using System;

/// <summary>
/// 红点业务注册器。每个业务模块通过实现该接口注册自己的节点关系和判断规则。
/// 调用边界：只在运行时数据准备完成后注册，不应在编辑器态或 prefab 预览中触发。
/// </summary>
public interface IRedDotRegister
{
    /// <summary>
    /// 注册业务拥有的红点节点、父子关系和判断器。
    /// </summary>
    /// <param name="registry">红点注册入口，由 RedDotManager 实现。</param>
    void Register(IRedDotRegistry registry);
}

/// <summary>
/// 单个红点节点的业务判断器。实现类只读取业务状态并返回是否显示红点。
/// 注意：不应在 IsOn 内发奖、写存档、打开 UI、请求广告/IAP 或加载资源。
/// </summary>
public interface IRedDotChecker
{
    /// <summary>
    /// 计算红点是否显示。异常会被 RedDotManager 捕获并按隐藏处理。
    /// </summary>
    /// <returns>true 表示显示红点；false 表示隐藏红点。</returns>
    bool IsOn();
}

/// <summary>
/// 红点注册入口。业务注册器只依赖该接口，避免直接改写 RedDotManager 内部字典。
/// </summary>
public interface IRedDotRegistry
{
    /// <summary>
    /// 注册红点节点及可选父节点。父节点会根据子节点状态自动聚合。
    /// </summary>
    /// <param name="key">当前红点节点。</param>
    /// <param name="parent">父级红点节点；None 表示没有父节点。</param>
    void RegisterNode(RedDotKey key, RedDotKey parent = RedDotKey.None);

    /// <summary>
    /// 为节点注册业务判断器。一个节点只保留最后注册的判断器。
    /// </summary>
    /// <param name="key">需要计算的红点节点。</param>
    /// <param name="checker">业务判断器。</param>
    void RegisterChecker(RedDotKey key, IRedDotChecker checker);

    /// <summary>
    /// 为节点注册委托形式的业务判断器，适合简单条件判断。
    /// </summary>
    /// <param name="key">需要计算的红点节点。</param>
    /// <param name="checker">返回红点是否显示的委托。</param>
    void RegisterChecker(RedDotKey key, Func<bool> checker);
}
