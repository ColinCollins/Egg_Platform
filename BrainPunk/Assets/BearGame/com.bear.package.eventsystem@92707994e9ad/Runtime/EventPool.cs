using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Bear.EventSystem
{
    /// <summary>
    /// 事件池
    /// </summary>
    [SuppressMessage("ReSharper", "UnusedParameter.Global")]
    public class EventPool
    {
        public static bool DisableLeakCheck = false;
        
        private readonly Dictionary<Type, Stack<IEvent>> _recycler;

        public EventPool()
        {
            _recycler = new Dictionary<Type, Stack<IEvent>>();
        }

        public void Clear()
        {
            _recycler.Clear();
        }
        
        internal T GetOrCreate<T>() where T : class, IEvent, new()
        { 
            var type = typeof(T);
            var evt = Get(type) ?? new T();
            return (T)evt;
        }
        
        private IEvent Get(Type type)
        {
            IEvent evt = null;
            if (_recycler.TryGetValue(type, out var recycler) && recycler.Count > 0)
            {
                evt = recycler.Pop();
            }
            return evt;
        }

        /// <summary>
        /// 获取事件（无参）
        /// </summary>
        /// <typeparam name="TEvent">事件类型</typeparam>
        /// <param name="witness"></param>
        /// <returns></returns>
        public TEvent Get<TEvent>(TEvent witness)
            where TEvent : class, IEventBase, new()
        {
            TEvent evt = GetOrCreate<TEvent>();
            evt.Initialize();
            return evt;
        }

        /// <summary>
        /// 获取事件（1参）
        /// </summary>
        /// <typeparam name="TEvent"></typeparam>
        /// <typeparam name="TParam1"></typeparam>
        /// <param name="witness"></param>
        /// <param name="param1"></param>
        /// <returns></returns>
        public TEvent Get<TEvent, TParam1>(TEvent witness, TParam1 param1)
            where TEvent : class, IEventBase<TParam1>, new()
        {
            TEvent evt = GetOrCreate<TEvent>();
            evt.Initialize(param1);
            return evt;
        }

        /// <summary>
        /// 获取事件（2参）
        /// </summary>
        /// <typeparam name="TEvent"></typeparam>
        /// <typeparam name="TParam1"></typeparam>
        /// <typeparam name="TParam2"></typeparam>
        /// <param name="witness"></param>
        /// <param name="param1"></param>
        /// <param name="param2"></param>
        /// <returns></returns>
        public TEvent Get<TEvent, TParam1, TParam2>(TEvent witness, TParam1 param1, TParam2 param2)
            where TEvent : class, IEventBase<TParam1, TParam2>, new()
        {
            TEvent evt = GetOrCreate<TEvent>();
            evt.Initialize(param1, param2);
            return evt;
        }

        /// <summary>
        /// 获取事件（3参）
        /// </summary>
        /// <typeparam name="TEvent"></typeparam>
        /// <typeparam name="TParam1"></typeparam>
        /// <typeparam name="TParam2"></typeparam>
        /// <typeparam name="TParam3"></typeparam>
        /// <param name="witness"></param>
        /// <param name="param1"></param>
        /// <param name="param2"></param>
        /// <param name="param3"></param>
        /// <returns></returns>
        public TEvent Get<TEvent, TParam1, TParam2, TParam3>(TEvent witness, TParam1 param1, TParam2 param2, TParam3 param3)
            where TEvent : class, IEventBase<TParam1, TParam2, TParam3>, new()
        {
            TEvent evt = GetOrCreate<TEvent>();
            evt.Initialize(param1, param2, param3);
            return evt;
        }

        /// <summary>
        /// 获取事件（4参）
        /// </summary>
        /// <typeparam name="TEvent"></typeparam>
        /// <typeparam name="TParam1"></typeparam>
        /// <typeparam name="TParam2"></typeparam>
        /// <typeparam name="TParam3"></typeparam>
        /// <typeparam name="TParam4"></typeparam>
        /// <param name="witness"></param>
        /// <param name="param1"></param>
        /// <param name="param2"></param>
        /// <param name="param3"></param>
        /// <param name="param4"></param>
        /// <returns></returns>
        public TEvent Get<TEvent, TParam1, TParam2, TParam3, TParam4>(TEvent witness, TParam1 param1, TParam2 param2, TParam3 param3, TParam4 param4)
            where TEvent : class, IEventBase<TParam1, TParam2, TParam3, TParam4>, new()
        {
            TEvent evt = GetOrCreate<TEvent>();
            evt.Initialize(param1, param2, param3, param4);
            return evt;
        }

        /// <summary>
        /// 获取事件（5参）
        /// </summary>
        /// <typeparam name="TEvent"></typeparam>
        /// <typeparam name="TParam1"></typeparam>
        /// <typeparam name="TParam2"></typeparam>
        /// <typeparam name="TParam3"></typeparam>
        /// <typeparam name="TParam4"></typeparam>
        /// <typeparam name="TParam5"></typeparam>
        /// <param name="witness"></param>
        /// <param name="param1"></param>
        /// <param name="param2"></param>
        /// <param name="param3"></param>
        /// <param name="param4"></param>
        /// <param name="param5"></param>
        /// <returns></returns>
        public TEvent Get<TEvent, TParam1, TParam2, TParam3, TParam4, TParam5>(TEvent witness, TParam1 param1, TParam2 param2, TParam3 param3, TParam4 param4, TParam5 param5)
            where TEvent : class, IEventBase<TParam1, TParam2, TParam3, TParam4, TParam5>, new()
        {
            TEvent evt = GetOrCreate<TEvent>();
            evt.Initialize(param1, param2, param3, param4, param5);
            return evt;
        }

        /// <summary>
        /// 获取事件（6参）
        /// </summary>
        /// <typeparam name="TEvent"></typeparam>
        /// <typeparam name="TParam1"></typeparam>
        /// <typeparam name="TParam2"></typeparam>
        /// <typeparam name="TParam3"></typeparam>
        /// <typeparam name="TParam4"></typeparam>
        /// <typeparam name="TParam5"></typeparam>
        /// <typeparam name="TParam6"></typeparam>
        /// <param name="witness"></param>
        /// <param name="param1"></param>
        /// <param name="param2"></param>
        /// <param name="param3"></param>
        /// <param name="param4"></param>
        /// <param name="param5"></param>
        /// <param name="param6"></param>
        /// <returns></returns>
        public TEvent Get<TEvent, TParam1, TParam2, TParam3, TParam4, TParam5, TParam6>(TEvent witness, TParam1 param1, TParam2 param2, TParam3 param3, TParam4 param4, TParam5 param5, TParam6 param6)
            where TEvent : class, IEventBase<TParam1, TParam2, TParam3, TParam4, TParam5, TParam6>, new()
        {
            TEvent evt = GetOrCreate<TEvent>();
            evt.Initialize(param1, param2, param3, param4, param5, param6);
            return evt;
        }

        /// <summary>
        /// 获取事件（7参）
        /// </summary>
        /// <param name="witness"></param>
        /// <param name="param1"></param>
        /// <param name="param2"></param>
        /// <param name="param3"></param>
        /// <param name="param4"></param>
        /// <param name="param5"></param>
        /// <param name="param6"></param>
        /// <param name="param7"></param>
        /// <typeparam name="TEvent"></typeparam>
        /// <typeparam name="TParam1"></typeparam>
        /// <typeparam name="TParam2"></typeparam>
        /// <typeparam name="TParam3"></typeparam>
        /// <typeparam name="TParam4"></typeparam>
        /// <typeparam name="TParam5"></typeparam>
        /// <typeparam name="TParam6"></typeparam>
        /// <typeparam name="TParam7"></typeparam>
        /// <returns></returns>
        public TEvent Get<TEvent, TParam1, TParam2, TParam3, TParam4, TParam5, TParam6, TParam7>(TEvent witness, TParam1 param1, TParam2 param2, TParam3 param3, TParam4 param4, TParam5 param5, TParam6 param6, TParam7 param7)
            where TEvent : class, IEventBase<TParam1, TParam2, TParam3, TParam4, TParam5, TParam6, TParam7>, new()
        {
            TEvent evt = GetOrCreate<TEvent>();
            evt.Initialize(param1, param2, param3, param4, param5, param6, param7);
            return evt;
        }

        /// <summary>
        /// 回收事件
        /// </summary>
        /// <param name="evt"></param>
        internal void Recycle(IEvent evt)
        {
            Type type = evt.GetType();
            if (!_recycler.TryGetValue(type, out var recycler))
            {
                recycler = new Stack<IEvent>(4);
                _recycler.Add(type, recycler);
            }
            evt.Release();
            recycler.Push(evt);
        }
    }
}
