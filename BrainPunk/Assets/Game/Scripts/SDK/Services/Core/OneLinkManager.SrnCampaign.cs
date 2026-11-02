using System;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Game
{
    /// <summary>
    /// 将两个已约定 SRN 来源的首装 campaign 末尾字段转换为既有 OneLink 目标参数。
    /// 仅由 GCD 首装门禁调用；不负责归因判断、URL 唤起或关卡路由。
    /// </summary>
    public sealed partial class OneLinkManager
    {
        #region 来源与投放命名

        /// <summary>
        /// 只识别关卡 `DL-OL` 和固定领取 ID 的奖励 `DL-GC`／`DL-GS` 尾标；其它广告名及原有协议参数不得被覆盖。
        /// 返回值只包含业务协议参数，调用方仍须复用原 GCD 候选和 OneLinkRequest 校验。
        /// </summary>
        private static bool TryMapSrnCampaignTarget(JObject conversion, out JObject target)
        {
            target = null;
            if (conversion == null || conversion["click_event"] != null ||
                conversion["deep_link_value"] != null || conversion["deep_link_sub1"] != null ||
                conversion["deep_link_sub2"] != null ||
                conversion["media_source"]?.Type != JTokenType.String ||
                conversion["campaign"]?.Type != JTokenType.String)
                return false;

            string source = (string)conversion["media_source"];
            if (!string.Equals(source, "Facebook Ads", StringComparison.Ordinal) &&
                !string.Equals(source, "googleadwords_int", StringComparison.Ordinal))
                return false;

            string campaign = (string)conversion["campaign"];
            int lastField = campaign.LastIndexOf('_');
            if (lastField < 1) return false;

            string[] parts = campaign.Substring(lastField + 1).Split('-');
            if (parts.Length == 3 && parts[0] == "DL" && conversion["deep_link_sub3"] == null)
            {
                if (parts[1] == "GC" &&
                    int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int count) && count > 0)
                {
                    target = new JObject
                    {
                        ["deep_link_value"] = "getCoin",
                        ["deep_link_sub1"] = "coindl",
                        ["deep_link_sub2"] = parts[2]
                    };
                    return true;
                }

                if (parts[1] == "GS" && parts[2].Length > 0)
                {
                    foreach (char digit in parts[2])
                        if (digit < '0' || digit > '9') return false;

                    target = new JObject
                    {
                        ["deep_link_value"] = "getSkin",
                        ["deep_link_sub1"] = "skindl",
                        ["deep_link_sub2"] = parts[2]
                    };
                    return true;
                }
            }

            if (parts.Length != 4 || parts[0] != "DL" || parts[1] != "OL" ||
                parts[3].Length != 3 || parts[3] == "000")
                return false;

            foreach (char digit in parts[3])
                if (digit < '0' || digit > '9') return false;

            string gameType;
            string pathPrefix;
            switch (parts[2])
            {
                case "main":
                case "default": gameType = "main"; pathPrefix = "Level_"; break;
                case "draw": gameType = "draw"; pathPrefix = "DLevel_"; break;
                case "iwanner": gameType = "iwanner"; pathPrefix = "ILevel_"; break;
                case "dig": gameType = "dig"; pathPrefix = "DigLevel_"; break;
                default: return false;
            }

            target = new JObject
            {
                ["deep_link_value"] = "open_level",
                ["deep_link_sub1"] = gameType,
                ["deep_link_sub2"] = pathPrefix + parts[3]
            };
            return true;
        }

        #endregion
    }
}
