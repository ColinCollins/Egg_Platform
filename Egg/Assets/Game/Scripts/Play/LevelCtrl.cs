using System;
using System.Collections.Generic;
using System.Data;
using Config;
using Game.ConfigModule;
using Unity.Android.Gradle.Manifest;

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

    /// <summary>
    /// 当前关卡 Id
    /// </summary>
    public LevelData CurrentLevelData
    {
        get
        {
            return datas.Find(d => d.Id == CurrentLevel);
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
        if (!IsPassed(CurrentLevel))
        {
            DB.GameData.PassedLevels.Add(CurrentLevel);
            DB.GameData.MaxLevel = Math.Max(DB.GameData.MaxLevel, CurrentLevel);
        }

        DB.GameData.CurrentLevel++;
        DB.GameData.Save();

        RefreshLevel();
    }

    // 根据 pass 和 unlock 确定当前的 最小 level
    public void RefreshLevel()
    {
        // check current config and DB 数据对比
        // _currentLevel = 1
        LevelData data = null;
        var gameData = DB.GameData;

        for (int i = 0; i < datas.Count; i++)
        {
            data = datas[i];
            if (IsPassed(data.Id))
            {
                continue;
            }

            if (!IsUnlock(data.Id))
            {
                if (data.LockType == Config.Level.LevelLockType.Unlock)
                    gameData.UnlockLevels.Add(data.Id);
                else
                    continue;
            }

            // 查询最低 Id 关卡
            if (gameData.CurrentLevel > data.Id)
            {
                gameData.CurrentLevel = data.Id;
            }
        }

        DB.GameData.Save();
    }

    /// <summary>
    /// 要小于最大关卡数
    /// </summary>
    /// <returns></returns>
    public bool CanEnterLevel(int id)
    {
        return DB.GameData.MaxLevel + 1 <= id;
    }

    /// <summary>
    /// 是否已解锁
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public bool IsUnlock(int id)
    {
        return DB.GameData.UnlockLevels.Contains(id);
    }

    /// <summary>
    /// 是否已通关
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public bool IsPassed(int id)
    {
        return DB.GameData.PassedLevels.Contains(id);
    }


    /// <summary>
    /// 解锁关卡
    /// </summary>
    /// <param name="id"></param>
    public void UnlockLevel(int id)
    {
        if (IsUnlock(id))
            return;

        DB.GameData.UnlockLevels.Add(id);
        DB.GameData.Save();
    }
}
