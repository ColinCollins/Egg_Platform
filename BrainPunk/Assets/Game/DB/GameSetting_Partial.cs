using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Bear.SaveModule
{
    public partial class GameSetting
    {
        /// <summary>
        /// 初始化数据（设置默认值）
        /// </summary>
        public override void Init()
        {
            musicOn = true;
            sfxOn = true;
            vibrationOn = true;
            hasTryingGoogleLogin = false;
            currentLanguageKeyCode = "en";
            hasClickYoutube = false;
            hasClickInstagram = false;
            hasClickDiscord = false;
            hasClickTiktok = false;
            isNewVersion = false;
        }

        public bool MusicOn
        {
            get => musicOn;
            set => musicOn = value;
        }

        public bool SfxOn
        {
            get => sfxOn;
            set => sfxOn = value;
        }

        public bool VibrationOn
        {
            get => vibrationOn;
            set => vibrationOn = value;
        }

        public bool HasTryingGoogleLogin
        {
            get => hasTryingGoogleLogin;
            set => hasTryingGoogleLogin = value;
        }

        public string CurrentLanguageKeyCode
        {
            get => currentLanguageKeyCode;
            set => currentLanguageKeyCode = value;
        }

        public bool HasClickYoutube
        {
            get => hasClickYoutube;
            set => hasClickYoutube = value;
        }

        public bool HasClickInstagram
        {
            get => hasClickInstagram;
            set => hasClickInstagram = value;
        }

        public bool HasClickDiscord
        {
            get => hasClickDiscord;
            set => hasClickDiscord = value;
        }

        public bool HasClickTiktok
        {
            get => hasClickTiktok;
            set => hasClickTiktok = value;
        }

        public bool IsNewVersion
        {
            get => isNewVersion;
            set => isNewVersion = value;
        }

    }
}
