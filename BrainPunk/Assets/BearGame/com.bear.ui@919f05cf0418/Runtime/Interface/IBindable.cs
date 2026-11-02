namespace Bear.UI
{
    /// <summary>
    /// 数据绑定接口
    /// </summary>
    /// <typeparam name="T">视图模型类型</typeparam>
    public interface IBindable<T> where T : ViewModel
    {
        /// <summary>
        /// 绑定视图模型
        /// </summary>
        /// <param name="viewModel">视图模型实例</param>
        void Bind(T viewModel);

        /// <summary>
        /// 解绑视图模型
        /// </summary>
        void Unbind();

        /// <summary>
        /// 数据变更时调用
        /// </summary>
        void OnDataChanged();
    }
}

