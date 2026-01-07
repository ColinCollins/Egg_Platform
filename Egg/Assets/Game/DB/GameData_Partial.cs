using UnityEngine;
using System.Collections.Generic;

namespace Bear.SaveModule
{
    public partial class GameData
    {
        public int CurrentLevel
        {
            get => currentLevel;
            set => currentLevel = value;
        }

        public List<int> UnlockLevels
        {
            get => unlockLevels;
            set => unlockLevels = value;
        }

        public List<int> PassedLevels
        {
            get => passedLevels;
            set => passedLevels = value;
        }

    }
}
