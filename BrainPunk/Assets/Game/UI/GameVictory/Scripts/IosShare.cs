using UnityEngine;
using System.Runtime.InteropServices;

public class IosShare
{
#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void _ShareText(string text);
#endif
    public static void ShareText(string text)
    {
#if UNITY_IOS && !UNITY_EDITOR
        _ShareText(text);
#endif
    }
}
