using System;
using System.Collections.Generic;
using Config.Game;
using GameCommon;
using GuruLog = Guru.SDK.Framework.Utils.Log.Log;

namespace Game
{
    /// <summary>首页奖励指令：用户校验、存档防重与既有背包发奖；不处理进关或 SDK 初始化。</summary>
    public sealed partial class OneLinkManager
    {
        #region 奖励领取

        private void ExecuteReward(OneLinkRequest request)
        {
            // 首页等待期间账号可能变化，消费请求时重新核对；不能把未就绪身份当作不限用户。
            if (!string.IsNullOrEmpty(request.TargetUserId) &&
                (!GameAccountUtils.HasValidUser ||
                 !string.Equals(request.TargetUserId, GameAccountUtils.CurrentUid, StringComparison.Ordinal)))
            {
                GuruLog.W("[OneLink] stage=reward_rejected, reason=user_mismatch.");
                return;
            }

            var data = DB.GameData;
            var bag = GameManager.Instance.Bag;
            if (data == null || bag == null)
            {
                GuruLog.W("[OneLink] stage=reward_rejected, reason=data_not_ready.");
                return;
            }

            data.OneLinkClaimedRewardIds ??= new List<string>();
            if (data.OneLinkClaimedRewardIds.Contains(request.RewardId))
            {
                // 上次保存失败时仍保留内存奖励和标识；重复点击只补存，绝不再次加币或授予皮肤。
                bool saved = DB.SaveGameData();
                GuruLog.I($"[OneLink] stage=reward_duplicate, saved={saved}.");
                return;
            }

            if (request.Action == OneLinkAction.GetCoin)
            {
                int current = bag.GetToolCount(GameProps.Coin);
                if (current < 0 || request.CoinCount <= 0 || (long)current + request.CoinCount > int.MaxValue)
                {
                    GuruLog.W("[OneLink] stage=reward_rejected, reason=invalid_coin_balance_or_overflow.");
                    return;
                }
            }
            else if (request.Action != OneLinkAction.GetSkin || !SkinUtils.TryGetSkin(request.SkinId, out _))
            {
                GuruLog.W("[OneLink] stage=reward_rejected, reason=invalid_skin.");
                return;
            }

            // 两种奖励与标识都属于 GameData；先占用标识，防止背包事件同步重入，最后同份保存。
            data.OneLinkClaimedRewardIds.Add(request.RewardId);
            bool delivered = false;
            bool newSkin = false;
            bool rewardSaved = false;
            try
            {
                if (request.Action == OneLinkAction.GetCoin)
                    bag.AddTool(GameProps.Coin, request.CoinCount, RewardType.Media, autoSave: false);
                else
                    newSkin = bag.GetNewSkin(request.SkinId, GetSkinWayType.Reward, autoSave: false);
                delivered = true;
            }
            finally
            {
                // 背包更新/打点抛错也要保存已修改的奖励及防重记录，不以删除标识触发二次发奖。
                bool saved = DB.SaveGameData();
                rewardSaved = saved;
                if (delivered && saved)
                    GuruLog.I($"[OneLink] stage=reward_completed, action={request.Action}, newSkin={newSkin}.");
                else
                    GuruLog.W($"[OneLink] stage=reward_incomplete, delivered={delivered}, saved={saved}, retainedInMemory=True.");
            }

            if (!delivered || !rewardSaved)
                return;

            // 奖励已由背包发放并保存，弹窗只展示；关闭或播放金币动画时不能再次发奖。
            try
            {
                var rewards = new Dictionary<GameProps, object>();
                if (request.Action == OneLinkAction.GetCoin)
                    rewards.Add(GameProps.Coin, request.CoinCount);
                else
                    rewards.Add(GameProps.Skin, request.SkinId);

                var popup = CommonGetRewardPopup.Create(rewards, multiple: 1, grantNormalReward: false);
                if (popup != null)
                    GuruLog.I($"[OneLink] stage=reward_popup_opened, action={request.Action}.");
                else
                    GuruLog.W($"[OneLink] stage=reward_popup_failed, action={request.Action}, reason=popup_unavailable.");
            }
            catch (Exception exception)
            {
                GuruLog.W($"[OneLink] stage=reward_popup_failed, action={request.Action}, error={exception.Message}.");
            }
        }

        #endregion
    }
}
