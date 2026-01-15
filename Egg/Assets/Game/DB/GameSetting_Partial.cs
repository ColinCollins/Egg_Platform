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
            musicOn = false;
            sfxOn = false;
            vibrationOn = false;
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

    }
}
