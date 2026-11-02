using System;
using UnityEngine;
using System.Collections.Generic;

namespace Bear.EventSystem
{
    public interface IEventSender {}
    
    public static class EventDispatcher
    {
        private static Dictionary<Type, List<object>> _actions = new Dictionary<Type, List<object>>();
        internal static Dictionary<Type, List<object>> Actions => _actions;
        private static EventPool _pool = new EventPool();
        
        // 跟踪正在执行的事件类型（使用计数器支持嵌套调用）
        private static Dictionary<Type, int> _executingTypes = new Dictionary<Type, int>();
        
        // 延迟注册/注销的缓存队列
        private static Dictionary<Type, List<object>> _pendingRegisters = new Dictionary<Type, List<object>>();
        private static Dictionary<Type, List<object>> _pendingUnregisters = new Dictionary<Type, List<object>>();

        internal static void Register(Type type, object action)
        {
            // 如果该类型正在执行，加入缓存队列
            if (_executingTypes.TryGetValue(type, out int count) && count > 0)
            {
                if (!_pendingRegisters.TryGetValue(type, out var pendingList))
                {
                    pendingList = new List<object>(4);
                    _pendingRegisters.Add(type, pendingList);
                }
                pendingList.Add(action);
                return;
            }
            
            // 否则直接注册
            if (!_actions.TryGetValue(type, out var actionList))
            {
                actionList = new List<object>(4);
                _actions.Add(type, actionList);
            }
            actionList.Add(action);
        }

        internal static void Unregister(Type type, object action)
        {
            // 如果该类型正在执行，加入缓存队列
            if (_executingTypes.TryGetValue(type, out int count) && count > 0)
            {
                if (!_pendingUnregisters.TryGetValue(type, out var pendingList))
                {
                    pendingList = new List<object>(4);
                    _pendingUnregisters.Add(type, pendingList);
                }
                pendingList.Add(action);
                return;
            }
            
            // 否则直接注销
            if (!_actions.TryGetValue(type, out var actionList))
                return;
            actionList.Remove(action);
        }
        
        /// <summary>
        /// 获取委托的目标名称（用于日志）
        /// </summary>
        private static string GetTargetName(object action)
        {
            if (action is Delegate del)
            {
                if (del.Target != null)
                {
                    return del.Target.GetType().Name + "." + del.Method.Name;
                }
                return del.Method.Name + " (static)";
            }
            return action?.GetType().Name ?? "Unknown";
        }
        
        /// <summary>
        /// 处理延迟的注册和注销操作
        /// </summary>
        private static void ProcessPendingOperations(Type eventType)
        {
            int registerCount = 0;
            int unregisterCount = 0;
            
            // 处理延迟注册
            if (_pendingRegisters.TryGetValue(eventType, out var pendingRegisters))
            {
                registerCount = pendingRegisters.Count;
                
                if (!_actions.TryGetValue(eventType, out var actionList))
                {
                    actionList = new List<object>(4);
                    _actions.Add(eventType, actionList);
                }
                
                // 使用 for 循环避免 GC 分配
                for (int i = 0; i < pendingRegisters.Count; i++)
                {
                    var action = pendingRegisters[i];
                    string targetName = GetTargetName(action);
                    Debug.LogWarning($"EventDispatcher: Processing pending register - Event: '{eventType.Name}', Target: '{targetName}'");
                    actionList.Add(action);
                }
                
                pendingRegisters.Clear();
                _pendingRegisters.Remove(eventType);
            }
            
            // 处理延迟注销
            if (_pendingUnregisters.TryGetValue(eventType, out var pendingUnregisters))
            {
                unregisterCount = pendingUnregisters.Count;
                
                if (_actions.TryGetValue(eventType, out var actionList))
                {
                    // 使用 for 循环避免 GC 分配
                    for (int i = 0; i < pendingUnregisters.Count; i++)
                    {
                        var action = pendingUnregisters[i];
                        string targetName = GetTargetName(action);
                        Debug.LogWarning($"EventDispatcher: Processing pending unregister - Event: '{eventType.Name}', Target: '{targetName}'");
                        actionList.Remove(action);
                    }
                }
                else
                {
                    Debug.LogWarning($"EventDispatcher: Attempted to unregister {unregisterCount} actions for event type '{eventType.Name}', but no action list exists.");
                }
                
                pendingUnregisters.Clear();
                _pendingUnregisters.Remove(eventType);
            }
            
            // 记录延迟操作信息（仅在存在延迟操作时记录）
            if (registerCount > 0 || unregisterCount > 0)
            {
                Debug.LogWarning($"EventDispatcher: Processed pending operations for event type '{eventType.Name}': {registerCount} registers, {unregisterCount} unregisters. " +
                               $"This indicates Register/Unregister was called during event execution, which is handled safely but may indicate a design issue.");
            }
        }

        private static void DoFire<TEvent>(this IEventSender sender, TEvent arg)
            where TEvent : class, IEvent
        {
            Type eventType = typeof(TEvent);
            
            if (!_actions.TryGetValue(eventType, out var actionList))
            {
                _pool.Recycle(arg);
                return;
            }

            // 标记该事件类型正在执行（支持嵌套调用）
            if (!_executingTypes.TryGetValue(eventType, out int count))
            {
                _executingTypes[eventType] = 1;
            }
            else
            {
                _executingTypes[eventType] = count + 1;
            }

            try
            {
                // 使用 for 循环避免 GC 分配（foreach 会创建迭代器对象）
                // 直接遍历原列表，Register/Unregister 会进入缓存队列
                for (int i = 0; i < actionList.Count; i++)
                {
                    if (actionList[i] is Action<TEvent> action)
                    {
                        action.Invoke(arg);
                    }
                }
            }
            catch(Exception ex)
            {
                Debug.LogError($"EventDispatcher: Error firing event: {ex.Message}");
                Debug.LogError($"EventDispatcher: Error firing event: {eventType.Name}");
            }
            finally
            {
                // 减少执行计数
                if (_executingTypes.TryGetValue(eventType, out int currentCount))
                {
                    if (currentCount <= 1)
                    {
                        _executingTypes.Remove(eventType);
                        // 执行完成，处理延迟的注册和注销操作
                        ProcessPendingOperations(eventType);
                    }
                    else
                    {
                        _executingTypes[eventType] = currentCount - 1;
                    }
                }
                
                _pool.Recycle(arg);
            }
        }

        public static void DispatchEvent<TEvent>(this IEventSender sender, TEvent witness)
            where TEvent : class, IEventBase, new() =>
            DoFire(sender , _pool.Get(witness));

        public static void DispatchEvent<TEvent, TParam1>(this IEventSender sender, TEvent witness, TParam1 param1)
            where TEvent : class, IEventBase<TParam1>, new() =>
            DoFire(sender , _pool.Get(witness, param1));

        public static void DispatchEvent<TEvent, TParam1, TParam2>(this IEventSender sender, TEvent witness, 
            TParam1 param1, TParam2 param2)
            where TEvent : class, IEventBase<TParam1, TParam2>, new() =>
            DoFire(sender , _pool.Get(witness, param1, param2));

        public static void DispatchEvent<TEvent, TParam1, TParam2, TParam3>(this IEventSender sender, TEvent witness, 
            TParam1 param1, TParam2 param2, TParam3 param3)
            where TEvent : class, IEventBase<TParam1, TParam2, TParam3>, new() =>
            DoFire(sender , _pool.Get(witness, param1, param2, param3));

        public static void DispatchEvent<TEvent, TParam1, TParam2, TParam3, TParam4>(
            this IEventSender sender, TEvent witness, 
            TParam1 param1, TParam2 param2, TParam3 param3, TParam4 param4)
            where TEvent : class, IEventBase<TParam1, TParam2, TParam3, TParam4>, new() =>
            DoFire(sender , _pool.Get(witness, param1, param2, param3, param4));

        public static void DispatchEvent<TEvent, TParam1, TParam2, TParam3, TParam4, TParam5>(
            this IEventSender sender, TEvent witness, 
            TParam1 param1, TParam2 param2, TParam3 param3, TParam4 param4, TParam5 param5)
            where TEvent : class, IEventBase<TParam1, TParam2, TParam3, TParam4, TParam5>, new() =>
            DoFire(sender , _pool.Get(witness, param1, param2, param3, param4, param5));

        public static void DispatchEvent<TEvent, TParam1, TParam2, TParam3, TParam4, TParam5, TParam6>(
            this IEventSender sender, TEvent witness,  
            TParam1 param1, TParam2 param2, TParam3 param3,
            TParam4 param4, TParam5 param5, TParam6 param6)
            where TEvent : class, IEventBase<TParam1, TParam2, TParam3, TParam4, TParam5, TParam6>, new() =>
            DoFire(sender , _pool.Get(witness, param1, param2, param3, param4, param5, param6));

        public static void DispatchEvent<TEvent, TParam1, TParam2, TParam3, TParam4, TParam5, TParam6, TParam7>(
            this IEventSender sender, TEvent witness, 
            TParam1 param1, TParam2 param2, TParam3 param3,
            TParam4 param4, TParam5 param5, TParam6 param6, TParam7 param7)
            where TEvent : class, IEventBase<TParam1, TParam2, TParam3, TParam4, TParam5, TParam6, TParam7>, new() =>
            DoFire(sender , _pool.Get(witness, param1, param2, param3, param4, param5, param6, param7));
    }
}