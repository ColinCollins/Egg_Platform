using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Bear.ResourceSystem
{
    /// <summary>
    /// 使用 .NET 原生的 ConditionalWeakTable
    /// 用于建立对象到值的弱引用映射，不阻止 Key 被 GC
    /// </summary>
    public class ConditionalWeakTable<TKey, TValue> where TKey : class where TValue : class
    {
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<TKey, TValue> _table = new();

        public void Add(TKey key, TValue value)
        {
            _table.Add(key, value);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return _table.TryGetValue(key, out value);
        }

        public void Remove(TKey key)
        {
            _table.Remove(key);
        }
    }
}
