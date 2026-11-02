using System;

namespace Bear.UI
{
    /// <summary>
    /// UI 动画接口
    /// 用于集成 DOTween 实现 UI 打开/关闭动画效果
    /// </summary>
    public interface IUIAnimation
    {
        /// <summary>
        /// 播放打开动画
        /// </summary>
        /// <param name="onComplete">动画完成回调</param>
        /// <returns>动画是否立即完成</returns>
        bool PlayOpenAnimation(Action onComplete = null);

        /// <summary>
        /// 播放关闭动画
        /// </summary>
        /// <param name="onComplete">动画完成回调</param>
        /// <returns>动画是否立即完成</returns>
        bool PlayCloseAnimation(Action onComplete = null);


        /// <summary>
        /// 清理状态
        /// </summary>
        /// <param name="onComplete">动画完成回调</param>
        /// <returns>动画是否立即完成</returns>
        void ResetAnim(Action onComplete = null);
    }
}

