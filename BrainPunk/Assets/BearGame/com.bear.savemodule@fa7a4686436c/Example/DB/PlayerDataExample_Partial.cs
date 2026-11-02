using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Bear.SaveModule
{
    public partial class PlayerDataExample
    {
        /// <summary>
        /// 静态 ScriptableObject 实例（编辑器资源）
        /// </summary>
        public static PlayerDataExample Instance
        {
            get
            {
#if UNITY_EDITOR
                return AssetDatabase.LoadAssetAtPath<PlayerDataExample>("Assets/Modules/SaveModule/Example/DB/PlayerDataExample.asset");
#else
                return Resources.Load<PlayerDataExample>("PlayerDataExample");
#endif
            }
        }

        public int Level
        {
            get => level;
            set => level = value;
        }

        public string PlayerName
        {
            get => playerName;
            set => playerName = value;
        }

        public float Experience
        {
            get => experience;
            set => experience = value;
        }

        public int Gold
        {
            get => gold;
            set => gold = value;
        }

        public bool IsVip
        {
            get => isVip;
            set => isVip = value;
        }

    }
}
