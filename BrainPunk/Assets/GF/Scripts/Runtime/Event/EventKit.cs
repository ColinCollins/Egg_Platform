using System;
using System.Collections.Generic;

namespace GF
{
    public delegate void EventDelegate();

    public delegate void EventDelegate<T>(T arg);

    public delegate void EventDelegate<T, T2>(T arg1, T2 arg2);

    public delegate void EventDelegate<T, T2, T3>(T arg1, T2 arg2, T3 arg3);

    public delegate void EventDelegate<T, T2, T3, T4>(T arg1, T2 arg2, T3 arg3, T4 arg4);

    /// <summary>
    /// 无拆装箱事件系统
    /// </summary>
    public class EventKit
    {
        private Dictionary<string, Delegate> _events;
        private Queue<EventDelegate> _cache;
        private Dictionary<object, List<EventDelegate>> _taggedCallbacks;

        public EventKit()
        {
            _events = new Dictionary<string, Delegate>();
            _cache = new Queue<EventDelegate>();
            _taggedCallbacks = new Dictionary<object, List<EventDelegate>>();
        }

        #region 添加事件

        /// <summary>
        /// 添加事件
        /// </summary>
        /// <param name="id"></param>
        /// <param name="callback"></param>
        /// <param name="target"></param>
        public void AddEvent(string id, EventDelegate callback, object target = null)
        {
            AddOrRemoveEvent(id, callback, target, true);
        }

        /// <summary>
        /// 添加事件
        /// </summary>
        /// <param name="id"></param>
        /// <param name="callback"></param>
        /// <param name="target"></param>
        /// <typeparam name="T"></typeparam>
        public void AddEvent<T>(string id, EventDelegate<T> callback, object target = null)
        {
            AddOrRemoveEvent(id, callback, target, true);
        }

        /// <summary>
        /// 添加事件
        /// </summary>
        /// <param name="id"></param>
        /// <param name="callback"></param>
        /// <param name="target"></param>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        public void AddEvent<T1, T2>(string id, EventDelegate<T1, T2> callback, object target = null)
        {
            AddOrRemoveEvent(id, callback, target, true);
        }

        /// <summary>
        /// 添加事件
        /// </summary>
        /// <param name="id"></param>
        /// <param name="callback"></param>
        /// <param name="target"></param>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        public void AddEvent<T1, T2, T3>(string id, EventDelegate<T1, T2, T3> callback, object target = null)
        {
            AddOrRemoveEvent(id, callback, target, true);
        }

        /// <summary>
        /// 添加事件
        /// </summary>
        /// <param name="id"></param>
        /// <param name="callback"></param>
        /// <param name="target"></param>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        /// <typeparam name="T4"></typeparam>
        public void AddEvent<T1, T2, T3, T4>(string id, EventDelegate<T1, T2, T3, T4> callback, object target = null)
        {
            AddOrRemoveEvent(id, callback, target, true);
        }

        #endregion

        #region 移除事件

        /// <summary>
        /// 移除事件
        /// </summary>
        /// <param name="id"></param>
        /// <param name="callback"></param>
        public void RemoveEvent(string id, EventDelegate callback)
        {
            AddOrRemoveEvent(id, callback, null, false);
        }

        /// <summary>
        /// 移除事件
        /// </summary>
        /// <param name="id"></param>
        /// <param name="callback"></param>
        /// <typeparam name="T"></typeparam>
        public void RemoveEvent<T>(string id, EventDelegate<T> callback)
        {
            AddOrRemoveEvent(id, callback, null, false);
        }

        /// <summary>
        /// 移除事件
        /// </summary>
        /// <param name="id"></param>
        /// <param name="callback"></param>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        public void RemoveEvent<T1, T2>(string id, EventDelegate<T1, T2> callback)
        {
            AddOrRemoveEvent(id, callback, null, false);
        }

        /// <summary>
        /// 移除事件
        /// </summary>
        /// <param name="id"></param>
        /// <param name="callback"></param>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        public void RemoveEvent<T1, T2, T3>(string id, EventDelegate<T1, T2, T3> callback)
        {
            AddOrRemoveEvent(id, callback, null, false);
        }

        /// <summary>
        /// 移除事件
        /// </summary>
        /// <param name="id"></param>
        /// <param name="callback"></param>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        /// <typeparam name="T4"></typeparam>
        public void RemoveEvent<T1, T2, T3, T4>(string id, EventDelegate<T1, T2, T3, T4> callback)
        {
            AddOrRemoveEvent(id, callback, null, false);
        }

        /// <summary>
        /// 移除所有事件
        /// </summary>
        /// <param name="id"></param>
        public void RemoveEventAll(string id)
        {
            lock (_events)
            {
                if (_events.ContainsKey(id))
                {
                    _events.Remove(id);
                }
            }
        }

        /// <summary>
        /// 移除该tag事件
        /// </summary>
        /// <param name="target"></param>
        public void RemoveEventTarget(object target)
        {
            lock (_events)
            {
                if (_taggedCallbacks.TryGetValue(target, out List<EventDelegate> callbacks))
                {
                    foreach (EventDelegate callback in callbacks)
                    {
                        callback?.Invoke();
                    }

                    callbacks.Clear();
                    _taggedCallbacks.Remove(target);
                }
            }
        }

        #endregion

        #region 派发事件

        /// <summary>
        /// 派发事件
        /// </summary>
        /// <param name="id"></param>
        /// <param name="async"></param>
        public void DispatchEvent(string id, bool async = false)
        {
            DispatchEventInternal(id, async, action => ((EventDelegate)action)?.Invoke());
        }

        /// <summary>
        /// 派发事件
        /// </summary>
        /// <param name="id"></param>
        /// <param name="arg"></param>
        /// <param name="async"></param>
        /// <typeparam name="T"></typeparam>
        public void DispatchEvent<T>(string id, T arg, bool async = false)
        {
            DispatchEventInternal(id, async, action => ((EventDelegate<T>)action)?.Invoke(arg));
        }

        /// <summary>
        /// 派发事件
        /// </summary>
        /// <param name="id"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <param name="async"></param>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        public void DispatchEvent<T1, T2>(string id, T1 arg1, T2 arg2, bool async = false)
        {
            DispatchEventInternal(id, async, action => ((EventDelegate<T1, T2>)action)?.Invoke(arg1, arg2));
        }

        /// <summary>
        /// 派发事件
        /// </summary>
        /// <param name="id"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <param name="arg3"></param>
        /// <param name="async"></param>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        public void DispatchEvent<T1, T2, T3>(string id, T1 arg1, T2 arg2, T3 arg3, bool async = false)
        {
            DispatchEventInternal(id, async, action => ((EventDelegate<T1, T2, T3>)action)?.Invoke(arg1, arg2, arg3));
        }

        /// <summary>
        /// 派发事件
        /// </summary>
        /// <param name="id"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <param name="arg3"></param>
        /// <param name="arg4"></param>
        /// <param name="async"></param>
        /// <typeparam name="T1"></typeparam>
        /// <typeparam name="T2"></typeparam>
        /// <typeparam name="T3"></typeparam>
        /// <typeparam name="T4"></typeparam>
        public void DispatchEvent<T1, T2, T3, T4>(string id, T1 arg1, T2 arg2, T3 arg3, T4 arg4, bool async = false)
        {
            DispatchEventInternal(id, async, action => ((EventDelegate<T1, T2, T3, T4>)action)?.Invoke(arg1, arg2, arg3, arg4));
        }

        #endregion

        #region Private Methods

        private void AddOrRemoveEvent(string id, Delegate callback, object target, bool isAdd)
        {
            lock (_events)
            {
                if (_events.TryGetValue(id, out Delegate existingAction))
                {
                    if (existingAction.GetType() != callback.GetType())
                    {
                        LogKit.E("参数类型或者数量错误！！！");
                        return;
                    }
                }
                else if (isAdd)
                {
                    _events.Add(id, null);
                }

                if (isAdd)
                {
                    _events[id] = Delegate.Combine(existingAction, callback);

                    if (target != null)
                    {
                        if (!_taggedCallbacks.TryGetValue(target, out List<EventDelegate> taggedList))
                        {
                            taggedList = new List<EventDelegate>();
                            _taggedCallbacks[target] = taggedList;
                        }
                        taggedList.Add(() => AddOrRemoveEvent(id, callback, target, false));
                    }
                }
                else
                {
                    _events[id] = Delegate.Remove(existingAction, callback);
                    if (_events[id] == null)
                    {
                        _events.Remove(id);
                    }
                }
            }
        }

        private void DispatchEventInternal(string id, bool async, Action<Delegate> action)
        {
            lock (_events)
            {
                if (_events.TryGetValue(id, out Delegate d))
                {
                    if (async)
                    {
                        _cache.Enqueue(() =>
                        {
                            if (_events.ContainsKey(id))
                            {
                                action(_events[id]);
                            }
                        });
                    }
                    else
                    {
                        action(d);
                    }
                }
            }
        }

        #endregion

        public void Update()
        {
            while (_cache.Count > 0)
            {
                _cache.Dequeue()?.Invoke();
            }
        }
    }
}