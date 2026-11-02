using System.Collections.Generic;
using Bear.SaveModule;
using Config.Game;
using Game.ConfigModule;
using UnityEngine;

/// <summary>
/// 新皮肤红点只读查询与刷新入口。
/// 调用边界：仅在 PlayMode 使用，不修改待预览列表、不写存档、不加载资源。
/// </summary>
public static class NewSkinRedDotQuery
{
    #region Query

    /// <summary>
    /// 判断是否存在任意待预览皮肤。
    /// </summary>
    /// <returns>true 表示待预览列表非空；false 表示列表为空或运行时数据未就绪。</returns>
    public static bool HasAny()
    {
        return TryGetWaitingSkinIds(out List<string> waitingSkinIds)
            && waitingSkinIds.Count > 0;
    }

    /// <summary>
    /// 判断是否存在指定部位的待预览皮肤。
    /// </summary>
    /// <param name="skinPart">需要检查的皮肤部位。</param>
    /// <returns>true 表示该部位存在待预览皮肤；false 表示不存在或运行时数据未就绪。</returns>
    public static bool HasAny(SkinPart skinPart)
    {
        if (!TryGetWaitingSkinIds(out List<string> waitingSkinIds)
            || !TryGetSkinTable(out Config.TbSkin skinTable))
        {
            return false;
        }

        for (int i = 0; i < waitingSkinIds.Count; i++)
        {
            string skinId = waitingSkinIds[i];
            if (string.IsNullOrWhiteSpace(skinId))
            {
                continue;
            }

            Config.Skin skin = skinTable.GetOrDefault(skinId);
            if (skin != null && skin.SkinPart == skinPart)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 判断指定皮肤是否仍在待预览列表中。
    /// </summary>
    /// <param name="skinId">需要检查的皮肤配置 ID。</param>
    /// <returns>true 表示该皮肤仍待预览；false 表示已预览或运行时数据未就绪。</returns>
    public static bool Contains(string skinId)
    {
        return !string.IsNullOrWhiteSpace(skinId)
            && TryGetWaitingSkinIds(out List<string> waitingSkinIds)
            && waitingSkinIds.Contains(skinId);
    }

    #endregion

    #region Refresh

    /// <summary>
    /// 刷新 NewSkin 总节点及其全部 ChildNode，并向上同步祖先节点状态。
    /// 调用方应在待预览列表实际新增或移除后调用，不应从编辑器态或每帧轮询触发。
    /// </summary>
    public static void Refresh()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        RedDotManager.RefreshBranch(RedDotKey.NewSkin);
    }

    #endregion

    #region Data Access

    private static bool TryGetWaitingSkinIds(out List<string> waitingSkinIds)
    {
        waitingSkinIds = null;
        if (!Application.isPlaying)
        {
            return false;
        }

        DBManager dbManager = Object.FindAnyObjectByType<DBManager>();
        if (dbManager == null || !dbManager.IsInitialized)
        {
            return false;
        }

        GameData gameData = dbManager.Get<GameData>();
        waitingSkinIds = gameData?.WaitingPreviewSkinId;
        return waitingSkinIds != null;
    }

    private static bool TryGetSkinTable(out Config.TbSkin skinTable)
    {
        skinTable = null;

        ConfigManager configManager = Object.FindAnyObjectByType<ConfigManager>();
        if (configManager == null || !configManager.IsInitialized)
        {
            return false;
        }

        skinTable = configManager.Tables?.TbSkin;
        return skinTable != null;
    }

    #endregion
}
