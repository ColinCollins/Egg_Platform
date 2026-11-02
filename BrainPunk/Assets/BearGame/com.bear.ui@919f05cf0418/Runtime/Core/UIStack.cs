using System.Collections.Generic;
using System.Linq;

namespace Bear.UI
{
    /// <summary>
    /// UI 栈管理器
    /// </summary>
    public class UIStack
    {
        private Stack<BaseUIView> _stack;

        public UIStack()
        {
            _stack = new Stack<BaseUIView>();
        }

        /// <summary>
        /// 压入栈
        /// </summary>
        /// <param name="view">UI 视图</param>
        public void Push(BaseUIView view)
        {
            if (view != null)
            {
                _stack.Push(view);
            }
        }

        /// <summary>
        /// 弹出栈
        /// </summary>
        /// <returns>栈顶 UI 视图</returns>
        public BaseUIView Pop()
        {
            if (_stack.Count > 0)
            {
                return _stack.Pop();
            }
            return null;
        }

        /// <summary>
        /// 查看栈顶
        /// </summary>
        /// <returns>栈顶 UI 视图</returns>
        public BaseUIView Peek()
        {
            if (_stack.Count > 0)
            {
                return _stack.Peek();
            }
            return null;
        }

        /// <summary>
        /// 清空栈
        /// </summary>
        public void Clear()
        {
            _stack.Clear();
        }

        /// <summary>
        /// 检查栈中是否存在指定类型的 UI
        /// </summary>
        /// <typeparam name="T">UI 类型</typeparam>
        /// <returns>是否存在</returns>
        public bool Contains<T>() where T : BaseUIView
        {
            return _stack.Any(view => view is T);
        }

        /// <summary>
        /// 获取栈中指定类型的 UI
        /// </summary>
        /// <typeparam name="T">UI 类型</typeparam>
        /// <returns>UI 实例</returns>
        public T Get<T>() where T : BaseUIView
        {
            return _stack.FirstOrDefault(view => view is T) as T;
        }

        /// <summary>
        /// 移除指定 UI 视图（不改变其他元素的顺序）
        /// </summary>
        /// <param name="view">要移除的 UI 视图</param>
        /// <returns>是否成功移除</returns>
        public bool Remove(BaseUIView view)
        {
            if (view == null)
            {
                return false;
            }

            // 使用临时栈重建，移除指定元素
            Stack<BaseUIView> tempStack = new Stack<BaseUIView>();
            bool found = false;

            while (_stack.Count > 0)
            {
                BaseUIView item = _stack.Pop();
                if (item == view)
                {
                    found = true;
                    break;
                }
                tempStack.Push(item);
            }

            // 将临时栈中的元素放回原栈
            while (tempStack.Count > 0)
            {
                _stack.Push(tempStack.Pop());
            }

            return found;
        }

        /// <summary>
        /// 获取栈中元素数量
        /// </summary>
        public int Count => _stack.Count;
    }
}

