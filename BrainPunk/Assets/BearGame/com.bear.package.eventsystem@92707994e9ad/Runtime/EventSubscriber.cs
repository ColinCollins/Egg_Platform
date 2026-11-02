using System;
using System.Collections.Generic;

namespace Bear.EventSystem
{
    public class EventSubscriber : IDisposable
    {
        private readonly List<Tuple<Type, object>> _subscriptions = new List<Tuple<Type, object>>();

        public void Subscribe<T>(Action<T> handler) where T : class, IEvent, new()
        {
            _subscriptions.Add(new Tuple<Type, object>(typeof(T), handler));
            EventDispatcher.Register(typeof(T), handler);
        }
        
        public void Dispose()
        {
            // 创建副本避免在遍历时修改集合导致的异常
            var subscriptionsCopy = new List<Tuple<Type, object>>(_subscriptions);
            foreach (var (type, action) in subscriptionsCopy)
            {
                EventDispatcher.Unregister(type, action);
            }
            _subscriptions.Clear();
        }
    }
}