using System;
using System.Globalization;
using Config.Level;
using Newtonsoft.Json.Linq;

namespace Game
{
    /// <summary>OneLink 允许的业务指令；奖励与进关分别执行，不以奖励参数推导玩法。</summary>
    internal enum OneLinkAction { OpenLevel, GetCoin, GetSkin }

    /// <summary>OneLink 的一次进关或奖励请求；只保存白名单参数，不把链接当资源地址使用。</summary>
    internal sealed class OneLinkRequest
    {
        #region 参数

        /// <summary>已校验的指令；默认值保留原 open_level 语义。</summary>
        internal OneLinkAction Action { get; private set; }

        /// <summary>奖励唯一标识；getCoin/getSkin 共用同一领取记录，按大小写精确匹配。</summary>
        internal string RewardId { get; private set; }

        /// <summary>金币正整数数量；仅用于 getCoin，余额溢出由执行层检查。</summary>
        internal int CoinCount { get; private set; }

        /// <summary>皮肤配置 Id；仅用于 getSkin，配置是否存在由执行层检查。</summary>
        internal string SkinId { get; private set; }

        /// <summary>奖励限定的 Guru Uid；空字符串表示不限定，执行时核对当前有效用户。</summary>
        internal string TargetUserId { get; private set; }

        /// <summary>目标玩法，由 sub1 映射到既有 GameType。</summary>
        internal GameType GameType { get; private set; }

        /// <summary>sub2 为目标：主线/Draw/Dig 使用资源 Path；IWanner 使用 IGroup_{id} 或 ILevel_{num}；未命中按玩法规则回退。</summary>
        internal string LevelPath { get; private set; }

        /// <summary>SDK 返回的原始链接；延迟深度链接未提供链接时可以为空。</summary>
        internal string Url { get; private set; }

        /// <summary>仅游戏侧 GM 入口赋值的会话标记；不从链接参数读取，只跳过进关历史，不绕过奖励或首页门禁。</summary>
        internal bool BypassEntryHistory { get; set; }

        #endregion

        #region 解析

        /// <summary>兼容 Android 平铺参数与 iOS click_event 参数；解析指令与参数，不读存档、用户或配置。</summary>
        internal static bool TryParse(JObject payload, out OneLinkRequest request, out string error)
        {
            request = null;
            error = null;
            if (payload == null)
            {
                error = "empty_payload";
                return false;
            }

            JToken clickEvent = payload["click_event"];
            JObject values = clickEvent as JObject;
            if (clickEvent?.Type == JTokenType.String)
            {
                try { values = JObject.Parse((string)clickEvent); }
                catch (Exception) { error = "invalid_click_event"; return false; }
            }
            values ??= payload;

            string action = Read(values, "deep_link_value");
            if (string.Equals(action, "getCoin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(action, "getSkin", StringComparison.OrdinalIgnoreCase))
                return TryParseReward(values, action, out request, out error);

            if (!string.Equals(action, "open_level", StringComparison.OrdinalIgnoreCase))
            {
                error = "unsupported_action";
                return false;
            }

            GameType gameType;
            switch (Read(values, "deep_link_sub1").ToLowerInvariant())
            {
                case "main":
                case "default": gameType = GameType.Default; break;
                case "draw": gameType = GameType.Draw; break;
                case "iwanner": gameType = GameType.IWanner; break;
                case "dig": gameType = GameType.Dig; break;
                default: error = "unsupported_game_type"; return false;
            }

            string sub2 = Read(values, "deep_link_sub2");
            JToken sub2Token = values["deep_link_sub2"];
            if (sub2Token != null && sub2Token.Type != JTokenType.Null &&
                sub2Token.Type != JTokenType.String && sub2Token.Type != JTokenType.Integer)
            {
                error = "invalid_level_path_type";
                return false;
            }

            request = new OneLinkRequest
            {
                GameType = gameType,
                LevelPath = sub2,
                Url = Read(values, "link")
            };
            return true;
        }

        private static bool TryParseReward(JObject values, string action, out OneLinkRequest request, out string error)
        {
            request = null;
            error = null;
            string rewardId = Read(values, "deep_link_sub1");
            if (string.IsNullOrEmpty(rewardId))
            {
                error = "missing_reward_id";
                return false;
            }

            JToken userToken = values["deep_link_sub3"];
            if (userToken != null && userToken.Type != JTokenType.Null &&
                userToken.Type != JTokenType.String && userToken.Type != JTokenType.Integer)
            {
                error = "invalid_reward_user_id";
                return false;
            }

            bool isCoin = string.Equals(action, "getCoin", StringComparison.OrdinalIgnoreCase);
            string value = Read(values, "deep_link_sub2");
            int count = 0;
            if (isCoin && (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out count) || count <= 0))
            {
                error = "invalid_coin_count";
                return false;
            }
            if (!isCoin && string.IsNullOrEmpty(value))
            {
                error = "missing_skin_id";
                return false;
            }

            request = new OneLinkRequest
            {
                Action = isCoin ? OneLinkAction.GetCoin : OneLinkAction.GetSkin,
                RewardId = rewardId,
                CoinCount = count,
                SkinId = isCoin ? null : value,
                TargetUserId = Read(values, "deep_link_sub3"),
                Url = Read(values, "link")
            };
            return true;
        }

        private static string Read(JObject values, string key)
        {
            JToken token = values[key];
            return token?.Type == JTokenType.String || token?.Type == JTokenType.Integer
                ? token.ToString().Trim()
                : string.Empty;
        }

        #endregion
    }
}
