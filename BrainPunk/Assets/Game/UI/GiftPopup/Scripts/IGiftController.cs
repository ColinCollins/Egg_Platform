/// <summary>
/// 单类礼物业务控制器契约。
/// 实现类负责自身资格、档位和弹窗创建；跨类型互斥、生命周期与通知聚合由 GiftManager 负责。
/// </summary>
public interface IGiftController
{
    /// <summary>当前控制器对应的礼物弹窗类型。</summary>
    GiftPopupType PopupType { get; }

    /// <summary>查询该类型入口当前是否允许展示。</summary>
    bool IsEntryAvailable();

    /// <summary>查询指定类型内档位是否存在，用于打开前拒绝非法档位。</summary>
    bool IsVariantDefined(int variantIndex);

    /// <summary>
    /// 打开当前或指定档位弹窗。
    /// variantIndex 为空时按业务规则选择当前档位；非空时用于明确的档位预览或调用。
    /// </summary>
    GiftPopup Open(int? variantIndex);

    /// <summary>
    /// 奖励完整交付后更新该类型的购买后状态。
    /// 支付成功但本地交付未完成时不得调用。
    /// </summary>
    void NotifyDelivered();
}
