namespace Bear.UI
{
    /// <summary>
    /// UI 动画类型枚举
    /// </summary>
    public enum UIAnimationType
    {
        /// <summary>
        /// 无动画
        /// </summary>
        None = 0,

        /// <summary>
        /// 淡入淡出
        /// </summary>
        Fade = 1,

        /// <summary>
        /// 缩放
        /// </summary>
        Scale = 2,

        /// <summary>
        /// 从上方滑入
        /// </summary>
        SlideFromTop = 3,

        /// <summary>
        /// 从下方滑入
        /// </summary>
        SlideFromBottom = 4,

        /// <summary>
        /// 从左侧滑入
        /// </summary>
        SlideFromLeft = 5,

        /// <summary>
        /// 从右侧滑入
        /// </summary>
        SlideFromRight = 6,

        /// <summary>
        /// 旋转缩放
        /// </summary>
        RotateScale = 7
    }
}

