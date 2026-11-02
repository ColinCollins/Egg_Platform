using System.Collections.Generic;
using Config;
using Config.Game;
using Game.ConfigModule;

/// <summary>
/// Shop 奖励列表查询；保留重复类型，只有数量查询按类型合计，不合并 Skin ID。
/// 仅处理配置数据，不触发发奖、存档或运行时单例初始化。
/// </summary>
public static class ShopRewardUtils
{
    #region 配置查询

    /// <summary>判断列表是否含指定类型，供商品状态展示使用。</summary>
    public static bool HasReward(this List<ShopReward> rewards, GameProps prop)
    {
        return rewards != null && rewards.Exists(reward => reward.Key == prop);
    }

    /// <summary>合计同类型 IntParam；参数不合法或合计超出 int 时失败，禁止用此方法查询 Skin。</summary>
    public static bool TryGetTotal(this List<ShopReward> rewards, GameProps prop, out int total)
    {
        total = 0;
        bool found = false;
        long sum = 0;
        if (rewards == null) return false;
        foreach (ShopReward reward in rewards)
        {
            if (reward.Key != prop) continue;
            if (!BaseParamUtils.TryGetIntParam(reward.Value, out int value)) return false;
            sum += value;
            if (sum > int.MaxValue || sum < int.MinValue) return false;
            found = true;
        }
        if (found) total = (int)sum;
        return found;
    }

    #endregion
}
