/// <summary>
/// 红点节点标识。UI 上的 RedDotView 通过该枚举选择要监听的红点节点。
/// 新增业务红点时只追加枚举值，不要复用或改动已上线 key 的数值。
/// </summary>
public enum RedDotKey
{
    /// <summary>
    /// 未配置状态。RedDotView 选择该值时不会订阅任何运行时红点。
    /// </summary>
    None = 0,

    /// <summary>
    /// 当前 postcard 进度，存在可以消除的 postcardItem 对象。
    /// 通过 currentIndex 获取对应 postcardConfig，判断 star 数量和 unlock progress 数据
    /// </summary>
    Postcard = 101,

    /// <summary>
    /// 待预览皮肤列表中仍有新皮肤。
    /// </summary>
    NewSkin = 102,

    /// <summary>
    /// 待预览皮肤列表中仍有背部皮肤。
    /// </summary>
    NewSkinBack = 110,

    /// <summary>
    /// 待预览皮肤列表中仍有面部皮肤。
    /// </summary>
    NewSkinFace = 111,

    /// <summary>
    /// 待预览皮肤列表中仍有身体皮肤。
    /// </summary>
    NewSkinBody = 112,

    /// <summary>
    /// 待预览皮肤列表中仍有全身 Spine 皮肤。
    /// </summary>
    NewSkinFullBody = 113,

    /// <summary>
    /// ShopPanel 每日免费金币当前可领取。
    /// </summary>
    DailyGiftCoin = 120
}
