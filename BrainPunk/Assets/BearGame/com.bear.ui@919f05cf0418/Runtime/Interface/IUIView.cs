namespace Bear.UI
{
    /// <summary>
    /// UI 视图接口
    /// </summary>
    public interface IUIView
    {
        /// <summary>
        /// UI 创建时调用
        /// </summary>
        void OnCreate();

        /// <summary>
        /// UI 打开时调用
        /// </summary>
        void OnOpen();

        /// <summary>
        /// UI 显示时调用
        /// </summary>
        void OnShow();

        /// <summary>
        /// UI 隐藏时调用
        /// </summary>
        void OnHide();

        /// <summary>
        /// UI 关闭时调用
        /// </summary>
        void OnClose();

        /// <summary>
        /// UI 销毁时调用
        /// </summary>
        void OnDestroyView();
    }
}

