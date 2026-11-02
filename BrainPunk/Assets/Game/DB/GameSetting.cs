using System.Collections.Generic;
using UnityEngine;

namespace Bear.SaveModule
{
    // [CreateAssetMenu(fileName = "GameData", menuName = "Save Data/GameData")]
    public partial class GameSetting : BaseSaveDataSO
    {
        public static StorageType StorageType = StorageType.PlayerPrefs;

        // 背景音开关
        [SerializeField] private bool musicOn = true;
        // 音效开关
        [SerializeField] private bool sfxOn = true;
        // 振动开关
        [SerializeField] private bool vibrationOn = true;

        // 已经尝试主动弹出登陆
        [SerializeField] private bool hasTryingGoogleLogin = false;

        // 当前语言
        [SerializeField] private string currentLanguageKeyCode = "en";

        /// <summary>
        /// youtube
        /// </summary>
        [SerializeField] private bool hasClickYoutube = false;
        
        //instagram
        [SerializeField] private bool hasClickInstagram = false;
        
        // discord
        [SerializeField] private bool hasClickDiscord = false;
        // tiktok
        [SerializeField] private bool hasClickTiktok = false;

        // 记录一下当前是否是新版本，确保新版本流程能够正确走完
        [SerializeField] private bool isNewVersion = false;
    }
}
