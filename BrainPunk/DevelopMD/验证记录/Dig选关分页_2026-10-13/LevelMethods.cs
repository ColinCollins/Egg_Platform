using System;using System.Linq;using Bear.Logger;public partial class LevelCtrl{public int CurrentLevel
    {
        get
        {
            return DB.GameData.CurrentLevel;
        }
    }public LevelData CurrentLevelData
    {
        get
        {
            if (leveldatas == null || levelsort == null || levelsort.Count == 0)
            {
                this.LogError("[LevelCtrl] CurrentLevelData: level data not initialized.");
                return null;
            }

            // 使用关卡排序表的 Id 范围进行一次安全夹紧
            int minId = levelsort.First().Id;
            int maxId = levelsort.Last().Id;
            int currentId = Math.Clamp(CurrentLevel, minId, maxId);

            var sort = levelsort.Find(d => d.Id == currentId);
            if (sort == null)
            {
                this.LogError($"[LevelCtrl] CurrentLevelData: LevelSort not found for id = {currentId}, fallback to last.");
                sort = levelsort.Last();
            }

            var data = GetLevelDataById(sort.Path);
            if (data == null)
            {
                this.LogError($"[LevelCtrl] CurrentLevelData: LevelData not found for path = {sort.Path}.");
            }

            return data;
        }
    }}