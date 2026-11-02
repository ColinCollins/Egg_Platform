using System;

namespace GF
{
    [AttributeUsage(AttributeTargets.Class)]
    public class UIViewAttribute : Attribute
    {
        /// <summary>
        /// 窗口层级
        /// </summary>
        public readonly UILayer uiLayer;

        /// <summary>
        /// 资源定位地址。
        /// </summary>
        public readonly string path;
        
        /// <summary>
        /// 界面移除时是否隐藏。
        /// </summary>
        public readonly bool hideWhenRemove;

        public UIViewAttribute(UILayer uiLayer, string path, bool hideWhenRemove = false)
        {
            this.uiLayer = uiLayer;
            this.path = path;
            this.hideWhenRemove = hideWhenRemove;
        }
    }
}