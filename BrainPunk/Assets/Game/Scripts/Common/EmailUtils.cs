using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Bear.Game
{
    public class EmailUtils
    {
        private static string Subject
        {
            get
            {
                return string.Format("Feedback-Brainy Boxy({0}{1})",
            Application.platform == RuntimePlatform.Android ? "A" : "I",
            Guru.GuruAppVersion.Load());
            }
        }

        /// <summary>
        /// 评分时候邮件
        /// </summary>
        /// <param name="toEmails"></param>
        /// <param name="uid"></param>
        /// <param name="deviceId"></param>
        /// <param name="rate"></param>
        public static void SendMailByRate(string[] toEmails, string uid, string deviceId, int rate)
        {
            string external = string.Format("Rate: {0}\r\n", rate);
            SendMailByMailApp(toEmails, uid, deviceId, external);
        }

        /// <summary>
        /// 游戏内 bug 反馈
        /// </summary>
        /// <param name="toEmails"></param>
        /// <param name="uid"></param>
        /// <param name="deviceId"></param>
        public static void SendMailInGame(string[] toEmails, string uid, string deviceId)
        {
            string external = string.Format("Level: {0}\r\n", PlayCtrl.Instance.Level.CurrentLevel);
            SendMailByMailApp(toEmails, uid, deviceId, external);
        }

        // 默认
        public static void SendMailByMailApp(string[] toEmails, string uid, string deviceId, string external = "")
        {
            StringBuilder content = new StringBuilder();
            content.Append("\r\n\r\n\r\n");
            content.Append("-----Please type your reply above this line-----\r\n");
            content.Append("\r\n");
            content.Append("It would help us a lot if you could include the following information in your email:\r\n");
            content.Append("* Your Reason For Contacting Us\r\n");
            content.Append("* A Screenshot or Video of the issue (If relevant or necessary)\r\n");
            content.Append("\r\n");
            content.Append($"USER_ID:{uid}\r\n");
            content.Append($"VERSION:{Guru.GuruAppVersion.Load()}\r\n");
            content.Append($"DEVICEID:{deviceId}\r\n");
            content.Append($"COUNTRY:{RegionInfo.CurrentRegion.TwoLetterISORegionName}\r\n");
            content.Append($"LANGUAGE:{DB.GameSetting.CurrentLanguageKeyCode.ToString()}\r\n");
            content.Append($"Platform:{GetPlatformName()}\r\n");
            content.Append(external);

            string sendBody = content.ToString() + "\r\n";
            string toEmail = string.Join(",", toEmails);

            string emailUrl = $"mailto:{toEmail}?subject={EscapeURL(Subject)}&body={EscapeURL(sendBody)}";
            Application.OpenURL(emailUrl);
        }

        private static string EscapeURL(string url)
        {
            return UnityWebRequest.EscapeURL(url).Replace("+", "%20");
        }
        /// <summary>
        /// 当前运行平台名称
        /// </summary>
        /// <returns></returns>
        public static string GetPlatformName()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.Android:
                    return "Android";
                case RuntimePlatform.IPhonePlayer:
                    return "iOS";
                case RuntimePlatform.WebGLPlayer:
                    return "WebGL";
                case RuntimePlatform.WindowsPlayer:
                    return "Windows";
                case RuntimePlatform.OSXPlayer:
                    return "MacOSX";
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.OSXEditor:
                    return "Editor";
                default:
                    return "Unsupported";
            }
        }
    }

}
