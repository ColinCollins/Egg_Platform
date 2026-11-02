using System;
using System.Collections.Generic;

namespace Bear.UI
{
    /// <summary>
    /// 视图模型基类
    /// 提供数据变更通知机制，实现数据驱动 UI 更新
    /// </summary>
    public abstract class ViewModel
    {
        private Dictionary<string, object> _properties;
        private event Action<string, object> _onPropertyChanged;

        protected ViewModel()
        {
            _properties = new Dictionary<string, object>();
        }

        /// <summary>
        /// 属性变更事件
        /// </summary>
        public event Action<string, object> OnPropertyChanged
        {
            add => _onPropertyChanged += value;
            remove => _onPropertyChanged -= value;
        }

        /// <summary>
        /// 设置属性值
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        /// <param name="propertyName">属性名称</param>
        /// <param name="value">属性值</param>
        protected void SetProperty<T>(string propertyName, T value)
        {
            if (!_properties.TryGetValue(propertyName, out object oldValue) || 
                !EqualityComparer<T>.Default.Equals((T)oldValue, value))
            {
                _properties[propertyName] = value;
                _onPropertyChanged?.Invoke(propertyName, value);
            }
        }

        /// <summary>
        /// 获取属性值
        /// </summary>
        /// <typeparam name="T">属性类型</typeparam>
        /// <param name="propertyName">属性名称</param>
        /// <returns>属性值</returns>
        protected T GetProperty<T>(string propertyName)
        {
            if (_properties.TryGetValue(propertyName, out object value))
            {
                return (T)value;
            }
            return default(T);
        }

        /// <summary>
        /// 通知所有属性变更
        /// </summary>
        protected void NotifyAllPropertiesChanged()
        {
            foreach (var kvp in _properties)
            {
                _onPropertyChanged?.Invoke(kvp.Key, kvp.Value);
            }
        }
    }
}

