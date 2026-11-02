using UnityEngine;

namespace GF
{
    public static class GameSettingData
    {
        private static GameSetting _setting = null;
        public static GameSetting Setting
        {
            get
            {
                if (_setting == null)
                    LoadSettingData();
                return _setting;
            }
        }

        /// <summary>
        /// 加载配置文件
        /// </summary>
        private static void LoadSettingData()
        {
            _setting = Resources.Load<GameSetting>("GameSetting");
            if (_setting == null)
            {
                LogKit.E("需要在Resources创建GameSetting文件");
            }
        }

        public static string GetGatewayUrl()
        {
            string url = _setting.hostUrl;
            if (string.IsNullOrEmpty(url))
            {
                return null;
            }
            return $"{url}/idle/gateway";
        }

        public static string GetHost()
        {
            return _setting.hostUrl;
        }
    }
}