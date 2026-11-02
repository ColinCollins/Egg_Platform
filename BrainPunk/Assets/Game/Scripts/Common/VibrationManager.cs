using UnityEngine;
using Bear.SaveModule;

namespace Game.Common
{
    /// <summary>
    /// 振动强度级别
    /// </summary>
    public enum VibrationIntensity
    {
        /// <summary>
        /// 轻微振动
        /// </summary>
        Light = 0,
        
        /// <summary>
        /// 中等振动
        /// </summary>
        Medium = 1,
        
        /// <summary>
        /// 强烈振动
        /// </summary>
        Heavy = 2
    }

    /// <summary>
    /// 振动管理器
    /// 提供控制振动强度的 API，支持不同平台的振动实现
    /// </summary>
    public class VibrationManager : Singleton<VibrationManager>
    {
        /// <summary>
        /// 振动强度映射（毫秒）
        /// Light: 50ms, Medium: 100ms, Heavy: 200ms
        /// </summary>
        private static readonly int[] VibrationDurations = { 50, 100, 200 };

        /// <summary>
        /// Android 振动服务类名
        /// </summary>
        private const string AndroidVibratorClass = "android.os.Vibrator";

        /// <summary>
        /// Android VibrationEffect 类名（API 26+）
        /// </summary>
        private const string AndroidVibrationEffectClass = "android.os.VibrationEffect";

        /// <summary>
        /// Android VibrationEffect 常量：DEFAULT_AMPLITUDE
        /// </summary>
        private const int AndroidDefaultAmplitude = -1;

        /// <summary>
        /// 是否启用振动
        /// </summary>
        private bool _isVibrationEnabled = true;

        /// <summary>
        /// 是否已初始化
        /// </summary>
        private bool _isInitialized = false;

        /// <summary>
        /// 初始化振动管理器（从 GameSetting 读取设置）
        /// </summary>
        public void Init()
        {
            if (_isInitialized)
            {
                return;
            }

            GameSetting gameSetting = DBManager.Instance.Get<GameSetting>();
            if (gameSetting != null)
            {
                _isVibrationEnabled = gameSetting.VibrationOn;
            }
            else
            {
                _isVibrationEnabled = true; // 默认启用
                Debug.LogWarning("[VibrationManager] GameSetting not found, using default vibration enabled state.");
            }

            _isInitialized = true;
        }

        /// <summary>
        /// 触发振动（使用默认强度：Medium）
        /// </summary>
        public void Vibrate()
        {
            Vibrate(VibrationIntensity.Medium);
        }

        /// <summary>
        /// 触发指定强度的振动
        /// </summary>
        /// <param name="intensity">振动强度</param>
        public void Vibrate(VibrationIntensity intensity)
        {
            if (!_isInitialized)
            {
                Init();
            }

            if (!_isVibrationEnabled)
            {
                return;
            }

            if (!IsVibrationSupported())
            {
                Debug.LogWarning("[VibrationManager] Vibration is not supported on this platform.");
                return;
            }

            int duration = VibrationDurations[(int)intensity];
            TriggerVibration(duration, intensity);
        }

        /// <summary>
        /// 触发自定义时长的振动
        /// </summary>
        /// <param name="milliseconds">振动时长（毫秒）</param>
        public void Vibrate(int milliseconds)
        {
            if (!_isInitialized)
            {
                Init();
            }

            if (!_isVibrationEnabled)
            {
                return;
            }

            if (!IsVibrationSupported())
            {
                Debug.LogWarning("[VibrationManager] Vibration is not supported on this platform.");
                return;
            }

            milliseconds = Mathf.Clamp(milliseconds, 10, 500); // 限制范围 10-500ms
            TriggerVibration(milliseconds, VibrationIntensity.Medium);
        }

        /// <summary>
        /// 触发振动（内部实现）
        /// </summary>
        private void TriggerVibration(int milliseconds, VibrationIntensity intensity)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            TriggerAndroidVibration(milliseconds, intensity);
#elif UNITY_IOS && !UNITY_EDITOR
            TriggerIOSVibration(intensity);
#else
            // Editor 或其他平台使用 Handheld.Vibrate()
            Handheld.Vibrate();
            Debug.Log($"[VibrationManager] Vibrate triggered (Editor/Other Platform): {intensity} ({milliseconds}ms)");
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Android 平台振动实现
        /// </summary>
        private void TriggerAndroidVibration(int milliseconds, VibrationIntensity intensity)
        {
            try
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject vibrator = currentActivity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    if (vibrator == null)
                    {
                        Debug.LogWarning("[VibrationManager] Android vibrator service is null.");
                        return;
                    }

                    // 检查 Android API 版本（26 = Android 8.0）
                    int apiLevel = GetAndroidAPILevel();
                    
                    if (apiLevel >= 26)
                    {
                        // Android 8.0+ 使用 VibrationEffect
                        using (AndroidJavaClass vibrationEffectClass = new AndroidJavaClass(AndroidVibrationEffectClass))
                        {
                            // 根据强度设置振幅（0-255）
                            int amplitude = GetAmplitudeForIntensity(intensity);
                            
                            // 创建 VibrationEffect
                            AndroidJavaObject vibrationEffect = vibrationEffectClass.CallStatic<AndroidJavaObject>(
                                "createOneShot", 
                                milliseconds, 
                                amplitude
                            );
                            
                            vibrator.Call("vibrate", vibrationEffect);
                        }
                    }
                    else
                    {
                        // Android 8.0 以下使用传统方法
                        vibrator.Call("vibrate", milliseconds);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[VibrationManager] Android vibration failed: {e.Message}");
            }
        }

        /// <summary>
        /// 获取 Android API 级别
        /// </summary>
        private int GetAndroidAPILevel()
        {
            try
            {
                using (AndroidJavaClass versionClass = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    return versionClass.GetStatic<int>("SDK_INT");
                }
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 根据强度获取振幅值（0-255）
        /// </summary>
        private int GetAmplitudeForIntensity(VibrationIntensity intensity)
        {
            switch (intensity)
            {
                case VibrationIntensity.Light:
                    return 50;  // 轻微
                case VibrationIntensity.Medium:
                    return 128; // 中等
                case VibrationIntensity.Heavy:
                    return 255; // 强烈
                default:
                    return 128;
            }
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        /// <summary>
        /// iOS 平台振动实现
        /// </summary>
        private void TriggerIOSVibration(VibrationIntensity intensity)
        {
            // iOS 使用系统振动
            // 注意：iOS 的 Handheld.Vibrate() 不支持强度控制，所有强度使用相同实现
            Handheld.Vibrate();
        }
#endif

        /// <summary>
        /// 检查当前平台是否支持振动
        /// </summary>
        public bool IsVibrationSupported()
        {
#if UNITY_ANDROID || UNITY_IOS
            return true;
#else
            return false;
#endif
        }

        /// <summary>
        /// 刷新振动设置（从 GameSetting 重新读取）
        /// </summary>
        public void RefreshVibrationSetting()
        {
            GameSetting gameSetting = DBManager.Instance.Get<GameSetting>();
            if (gameSetting != null)
            {
                _isVibrationEnabled = gameSetting.VibrationOn;
            }
            else
            {
                _isVibrationEnabled = true; // 默认启用
                Debug.LogWarning("[VibrationManager] GameSetting not found, using default vibration enabled state.");
            }
        }

        /// <summary>
        /// 设置振动开关（临时覆盖 GameSetting）
        /// </summary>
        /// <param name="enabled">是否启用</param>
        public void SetVibrationEnabled(bool enabled)
        {
            _isVibrationEnabled = enabled;
        }

        /// <summary>
        /// 获取当前振动开关状态
        /// </summary>
        public bool IsVibrationEnabled()
        {
            return _isVibrationEnabled;
        }
    }
}
