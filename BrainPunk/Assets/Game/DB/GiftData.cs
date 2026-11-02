using UnityEngine;

namespace Bear.SaveModule
{
    /// <summary>
    /// Gift 系统专属持久化数据。
    /// 只保存跨启动仍需保留的 Gift 业务状态；当前弹窗、购买请求和本次会话抑制状态仍由 GiftManager 内存管理。
    /// </summary>
    public partial class GiftData : BaseSaveDataSO
    {
        public static StorageType StorageType = StorageType.PlayerPrefs;

        #region 版本与 ChestGift 状态

        /// <summary>
        /// GiftData 存档结构版本。0 表示尚未从历史 GameData/GameSchedule 迁移；2 包含每日礼包分配记录。
        /// </summary>
        [SerializeField] private int dataVersion;

        /// <summary>
        /// 是否已经成功到达过主页。新用户第一次到达主页的当前会话仍不展示 ChestGift。
        /// </summary>
        [SerializeField] private bool hasReachedGiftHome;

        /// <summary>
        /// ChestGift 按设备当地自然日累计的刷新次数，用于五档礼盒轮换。
        /// </summary>
        [SerializeField] private int chestGiftDailyRefreshCount;

        /// <summary>
        /// 历史支付回调日期，仅供旧存档迁移保留；不再参与每日礼包显隐判断。
        /// </summary>
        [SerializeField] private long lastChestGiftPurchaseLocalDay;

        /// <summary>当前当地自然日分配的 ChestGift 日期；0 表示旧存档尚未建立分配记录。</summary>
        [SerializeField] private long chestGiftOfferLocalDay;

        /// <summary>当天实际分配的 1～5 档；0 表示没有可售档位。</summary>
        [SerializeField] private int chestGiftOfferIndex;

        /// <summary>当天实际分配档位对应的商品名，用于防止配置变化后误用旧档位。</summary>
        [SerializeField] private string chestGiftOfferProductId;

        /// <summary>分配该礼包时的 Daily 刷新次数；云控关闭期间仍可补足轮换步数。</summary>
        [SerializeField] private int chestGiftOfferRefreshCount;

        /// <summary>最近一次完成且与当天分配礼包一致的 ChestGift 商品名；恢复购买不写入。</summary>
        [SerializeField] private string chestGiftCompletedOfferProductId;

        #endregion
    }
}
