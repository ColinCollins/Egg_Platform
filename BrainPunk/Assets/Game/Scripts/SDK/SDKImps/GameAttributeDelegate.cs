using System;
using System.Diagnostics;
using System.Threading;
using Guru.SDK.Framework.Core.Spec.Protocols.Game;

namespace Game
{
    public class GameAttribute: IGameAttributeDelegate
    {
        
        private static readonly Lazy<GameAttribute> LazyInstance =
            new(() => new GameAttribute(), LazyThreadSafetyMode.ExecutionAndPublication);
        public static GameAttribute Instance => LazyInstance.Value;
        
        
        public string LevelName => $"main_{_levelId}";
        public int Level => _levelId;
        public int BestLevel => _bestLevelId;

        private int _levelId;
        private int _bestLevelId;


        /// <summary>
        /// 更新通关数量和最大关卡编号
        /// </summary>
        /// <param name="level"></param>
        /// <param name="hasPassed"></param>
        public void SetLevelId(int level, bool hasPassed = false)
        {
            if (level > _bestLevelId && hasPassed)
            {
                _bestLevelId = level;
            }
            _levelId = level;

            GameSDKService.Instance.UpdateBPlay();
            GameSDKService.Instance.UpdateBLevel();
        }

    }

}