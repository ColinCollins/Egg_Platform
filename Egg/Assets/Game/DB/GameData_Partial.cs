using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Bear.SaveModule
{
    public partial class GameData
    {
        /// <summary>
        /// 静态 ScriptableObject 实例（编辑器资源）
        /// </summary>
        public static GameData Instance
        {
            get
            {
#if UNITY_EDITOR
                return AssetDatabase.LoadAssetAtPath<GameData>("Assets/Game/DB/GameData.asset");
#else
                return Resources.Load<GameData>("GameData");
#endif
            }
        }

        public int CurrentLevel
        {
            get => currentLevel;
            set => currentLevel = value;
        }

        public int MaxLevel
        {
            get => maxLevel;
            set => maxLevel = value;
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
