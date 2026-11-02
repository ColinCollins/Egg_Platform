using System;
using UnityEngine;
using UnityEngine.Android;
using YooAsset;

namespace GF
{
    public class PermisionKit
    {
        public static void RequestPushNotifications()
        {
#if UNITY_ANDROID
            try
            {
                AndroidJavaClass androidPlugins = new AndroidJavaClass("com.guru.androidplugin.AndroidPlugins");
                int sdkInt = androidPlugins.CallStatic<int>("GetAndroidSystemVersion");
                LogKit.I($"sdkInt = {sdkInt}");
                //Android13版本以上，需要对推送动态申请权限
                if (sdkInt >= 33)
                {
                    // Android直接申请授权
                    string pushPermission = "android.permission.POST_NOTIFICATIONS";
                    bool hasPermission = Permission.HasUserAuthorizedPermission(pushPermission);
                    LogKit.I($"hasPermission = {hasPermission}");
                    if (!hasPermission)
                    {
                        Permission.RequestUserPermission(pushPermission);
                    }
                }

            }
            catch (Exception)
            {
                //pass
            }
#endif
        }
    }
}