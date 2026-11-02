using UnityEngine;

 public static class AndroidShare
    {
        public static void ShareText(string text, string chooserTitle = "Share")
        {
#if UNITY_ANDROID && !UNITY_EDITOR
        using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        using var intent = new AndroidJavaObject("android.content.Intent");
        intent.Call<AndroidJavaObject>("setAction", "android.intent.action.SEND");
        intent.Call<AndroidJavaObject>("setType", "text/plain");
        intent.Call<AndroidJavaObject>("putExtra", "android.intent.extra.TEXT", text);
        using var chooser = new AndroidJavaClass("android.content.Intent")
            .CallStatic<AndroidJavaObject>("createChooser", intent, chooserTitle);
        activity.Call("startActivity", chooser);
#endif
        }
    }