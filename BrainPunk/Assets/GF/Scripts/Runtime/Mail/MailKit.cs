using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using GF;
using Guru;
using UnityEngine;
using UnityEngine.Networking;

namespace GF
{
    public class MailKit
    {
        public void SendMailTo(string PlayerTag,string fromEmail,string fromEmailPwd,string[] toEmails, string subject,string body, SendCompletedEventHandler callback)
        {
            MailMessage mailMessage = new MailMessage();
            SmtpClient smtpServer = new SmtpClient("smtp.gmail.com");
            smtpServer.Timeout = 10000;
            smtpServer.DeliveryMethod = SmtpDeliveryMethod.Network;
            smtpServer.UseDefaultCredentials = false;
            smtpServer.Port = 587;

            mailMessage.From = new MailAddress(fromEmail);
            foreach (var email in toEmails)
            {
                mailMessage.To.Add(new MailAddress(email));
            }
            mailMessage.Subject = subject;
            mailMessage.Body = body;

            smtpServer.Credentials = new System.Net.NetworkCredential(fromEmail, fromEmailPwd) as ICredentialsByHost;
            smtpServer.EnableSsl = true;
            ServicePointManager.ServerCertificateValidationCallback = (value, certificate, chain, errors) => true;
            mailMessage.DeliveryNotificationOptions = DeliveryNotificationOptions.OnFailure;
            smtpServer.SendCompleted += new SendCompletedEventHandler(callback);
            string title = $"AP_Comment【{PlayerTag}】";
            smtpServer.SendAsync(mailMessage, title);
        }

        public void SendMailByMailApp(string[] toEmails, string subject, string body, string uid, string deviceId)
        {
            StringBuilder content = new StringBuilder();
            content.Append("--------------------\n");
            content.Append($"USER_ID:{uid}\n");
            content.Append($"VERSION:{Application.version}\n");
            content.Append($"DEVICEID:{deviceId}\n");
            content.Append($"COUNTRY:{RegionInfo.CurrentRegion.TwoLetterISORegionName}\n");
            content.Append($"LANGUAGE:{App.I2.CurrentLanguage.ToString()}\n");
            content.Append($"Platform:{GetPlatformName()}\n");
            
            string sendBody = content.ToString() + "\n" + body;
            string toEmail = string.Join(",", toEmails);
            string emailUrl = $"mailto:{toEmail}?subject={EscapeURL(subject)}&body={EscapeURL(sendBody)}";
            Application.OpenURL(emailUrl);
        }

        private string EscapeURL(string url)
        {
            return UnityWebRequest.EscapeURL(url).Replace("+", "%20");
        }
        /// <summary>
        /// 当前运行平台名称
        /// </summary>
        /// <returns></returns>
        public string GetPlatformName()
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