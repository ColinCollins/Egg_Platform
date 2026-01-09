using System.Collections.Generic;
using Config;
using Config.LubanConfig;
using Game.ConfigModule;
using UnityEngine;

/// <summary>
/// 用于处理一些 Level 相关的数据
/// </summary>
public class LevelCtrl
{
    private bool isReady = false;

    private List<LevelData> datas;

    public int CurrentLevel
    {
        get
        {
            return DB.GameData.CurrentLevel;
        }
    }

    public void Init()
    {
        if (isReady)
            return;

        // 数据加载配置
        datas = ConfigManager.Instance.Tables.TbLevelData.DataList;
        isReady = true;

        RefreshLevel();
    }

    public void Victory()
    {
        if (!DB.GameData.PassedLevels.Contains(CurrentLevel))
        {
            DB.GameData.PassedLevels.Add(CurrentLevel);
            DB.GameData.Save();
        }

        RefreshLevel();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public bool CanEnterLevel()
    {
        return DB.GameData.CurrentLevel > 0;
    }

    // 根据 pass 和 unlock 确定当前的 最小 level
    public void RefreshLevel()
    {
        // check current config and DB 数据对比
        // _currentLevel = 1
        LevelData data = null;
        var gameData = DB.GameData;
        gameData.CurrentLevel = -1;

        for (int i = 0; i < datas.Count; i++)
        {
            data = datas[i];
            if (gameData.PassedLevels.Contains(data.Id))
            {
                continue;
            }

            if (!gameData.UnlockLevels.Contains(data.Id))
            {
                if (data.LockType == Config.Level.LevelLockType.Unlock)
                {
                    gameData.UnlockLevels.Add(data.Id);
                }
                else
                {
                    continue;
                }
            }

            // 查询最低 Id 关卡
            if (gameData.CurrentLevel > data.Id)
            {
                gameData.CurrentLevel = data.Id;
            }
        }

        DB.GameData.Save();
    }
}
