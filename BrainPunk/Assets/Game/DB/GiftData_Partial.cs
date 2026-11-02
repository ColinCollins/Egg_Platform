using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Bear.SaveModule
{
    public partial class GiftData
    {
        /// <summary>
        /// 初始化 Gift 系统持久化数据。
        /// 注意事项：版本保持 0，由 ChestGiftController 首次读取时完成历史字段迁移。
        /// </summary>
        public override void Init()
        {
            dataVersion = 0;
            hasReachedGiftHome = false;
            chestGiftDailyRefreshCount = 0;
            lastChestGiftPurchaseLocalDay = 0L;
            chestGiftOfferLocalDay = 0L;
            chestGiftOfferIndex = 0;
            chestGiftOfferProductId = null;
            chestGiftOfferRefreshCount = 0;
            chestGiftCompletedOfferProductId = null;
        }

        /// <summary>GiftData 当前存档结构版本。</summary>
        public int DataVersion
        {
            get => dataVersion;
            set => dataVersion = value;
        }

        /// <summary>是否已经成功到达过主页。</summary>
        public bool HasReachedGiftHome
        {
            get => hasReachedGiftHome;
            set => hasReachedGiftHome = value;
        }

        /// <summary>ChestGift 按设备当地自然日累计的刷新次数。</summary>
        public int ChestGiftDailyRefreshCount
        {
            get => chestGiftDailyRefreshCount;
            set => chestGiftDailyRefreshCount = value;
        }

        /// <summary>历史支付回调日期；现行 ChestGift 入口不再读取该字段。</summary>
        public long LastChestGiftPurchaseLocalDay
        {
            get => lastChestGiftPurchaseLocalDay;
            set => lastChestGiftPurchaseLocalDay = value;
        }

        /// <summary>当天 ChestGift 分配记录的当地自然日起点；仅由 ChestGiftController 维护。</summary>
        public long ChestGiftOfferLocalDay
        {
            get => chestGiftOfferLocalDay;
            set => chestGiftOfferLocalDay = value;
        }

        /// <summary>当天实际分配的礼盒档位；没有可售档位时为 0。</summary>
        public int ChestGiftOfferIndex
        {
            get => chestGiftOfferIndex;
            set => chestGiftOfferIndex = value;
        }

        /// <summary>当天分配档位对应的商品名；必须与档位配置一起校验。</summary>
        public string ChestGiftOfferProductId
        {
            get => chestGiftOfferProductId;
            set => chestGiftOfferProductId = value;
        }

        /// <summary>最近分配礼包时的 Daily 刷新次数；仅用于补足未显示期间的轮换。</summary>
        public int ChestGiftOfferRefreshCount
        {
            get => chestGiftOfferRefreshCount;
            set => chestGiftOfferRefreshCount = value;
        }

        /// <summary>最近一次完成且与当天分配礼包一致的商品名，用于核对每日完成记录。</summary>
        public string ChestGiftCompletedOfferProductId
        {
            get => chestGiftCompletedOfferProductId;
            set => chestGiftCompletedOfferProductId = value;
        }
    }
}
