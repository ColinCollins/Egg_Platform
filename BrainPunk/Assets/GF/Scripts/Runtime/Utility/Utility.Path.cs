namespace GF
{
    public partial class Utility
    {
        /// <summary>
        /// 路径相关工具类
        /// </summary>
        public static class Path
        {
            /// <summary>
            /// 当前平台
            /// </summary>
            public static string Platform
            {
                get
                {
#if UNITY_ANDROID
                    return "Android";
#elif UNITY_IOS
            return "iOS";
#else
            return "Android";
#endif
                }
            }
        }
    }
}