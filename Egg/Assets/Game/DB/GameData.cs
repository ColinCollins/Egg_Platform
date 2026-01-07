using System.Collections.Generic;
using UnityEngine;

namespace Bear.SaveModule
{
    // [CreateAssetMenu(fileName = "GameData", menuName = "Save Data/GameData")]
    public partial class GameData : BaseSaveDataSO
    {
        public static StorageType StorageType = StorageType.PlayerPrefs;

        // 当前关卡
        [SerializeField] private int currentLevel = 1;

        // 已解锁关卡
        [SerializeField] private List<int> unlockLevels = new List<int>();

        // 已通关关卡
        [SerializeField] private List<int> passedLevels = new List<int>();
    }
}
