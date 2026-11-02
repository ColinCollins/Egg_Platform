using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 可复用红点状态管理器。负责节点注册、业务判断、父子聚合和 UI 订阅通知。
/// 调用边界：运行时系统初始化后注册业务；UI 通过 RedDotView 订阅，不直接访问内部字典。
/// </summary>
public static class RedDotManager
{
    #region Fields

    private static readonly Dictionary<RedDotKey, bool> States = new();
    private static readonly Dictionary<RedDotKey, RedDotKey> Parents = new();
    private static readonly Dictionary<RedDotKey, List<RedDotKey>> Children = new();
    private static readonly Dictionary<RedDotKey, IRedDotChecker> Checkers = new();
    private static readonly Dictionary<RedDotKey, Action<bool>> Listeners = new();

    #endregion

    #region Register

    /// <summary>
    /// 注册一个业务红点模块。通常在 DB、Config 和玩家运行时数据准备完成后调用。
    /// </summary>
    /// <param name="register">业务红点注册器。</param>
    public static void Register(IRedDotRegister register)
    {
        if (register == null)
        {
            return;
        }

        register.Register(RedDotRegistry.Instance);
    }

    /// <summary>
    /// 批量注册业务红点模块。注册完成后不会自动刷新，调用方可按初始化时机显式 RefreshAll。
    /// </summary>
    /// <param name="registers">业务红点注册器集合。</param>
    public static void RegisterRange(IEnumerable<IRedDotRegister> registers)
    {
        if (registers == null)
        {
            return;
        }

        foreach (IRedDotRegister register in registers)
        {
            Register(register);
        }
    }

    internal static void RegisterNode(RedDotKey key, RedDotKey parent = RedDotKey.None)
    {
        if (key == RedDotKey.None)
        {
            return;
        }

        EnsureState(key);

        if (parent == RedDotKey.None)
        {
            return;
        }

        EnsureState(parent);

        if (Parents.TryGetValue(key, out RedDotKey oldParent) && oldParent != parent)
        {
            RemoveChild(oldParent, key);
        }

        Parents[key] = parent;
        AddChild(parent, key);
        RefreshParentChain(key);
    }

    internal static void RegisterChecker(RedDotKey key, IRedDotChecker checker)
    {
        if (key == RedDotKey.None || checker == null)
        {
            return;
        }

        RegisterNode(key);
        Checkers[key] = checker;
    }

    internal static void RegisterChecker(RedDotKey key, Func<bool> checker)
    {
        if (checker == null)
        {
            return;
        }

        RegisterChecker(key, new FuncRedDotChecker(checker));
    }

    #endregion

    #region State

    /// <summary>
    /// 查询指定红点当前是否显示。未注册节点默认返回 false。
    /// </summary>
    /// <param name="key">红点节点。</param>
    /// <returns>true 表示显示；false 表示隐藏。</returns>
    public static bool IsOn(RedDotKey key)
    {
        return key != RedDotKey.None && States.TryGetValue(key, out bool isOn) && isOn;
    }

    /// <summary>
    /// 手动设置指定红点状态，并自动向上刷新父节点聚合状态。
    /// 适合没有业务判断器、由业务回调直接驱动的红点。
    /// </summary>
    /// <param name="key">红点节点。</param>
    /// <param name="isOn">是否显示红点。</param>
    public static void Set(RedDotKey key, bool isOn)
    {
        if (key == RedDotKey.None)
        {
            return;
        }

        EnsureState(key);
        SetState(key, isOn);
        RefreshParentChain(key);
    }

    /// <summary>
    /// 重新计算指定节点。节点状态由自身 checker 和全部子节点状态共同聚合。
    /// </summary>
    /// <param name="key">需要刷新的红点节点。</param>
    public static void Refresh(RedDotKey key)
    {
        if (key == RedDotKey.None)
        {
            return;
        }

        EnsureState(key);

        SetState(key, EvaluateNode(key));
        RefreshParentChain(key);
    }

    /// <summary>
    /// 重新计算指定节点及其全部子节点。子节点先刷新，当前节点最后聚合，并继续刷新祖先链。
    /// </summary>
    /// <param name="root">需要刷新的分支根节点。</param>
    public static void RefreshBranch(RedDotKey root)
    {
        if (root == RedDotKey.None)
        {
            return;
        }

        EnsureState(root);

        var visiting = new HashSet<RedDotKey>();
        if (!RefreshBranchInternal(root, visiting))
        {
            return;
        }

        RefreshParentChain(root);
    }

    /// <summary>
    /// 重新计算所有已注册 checker 的节点，并刷新父节点聚合状态。
    /// </summary>
    public static void RefreshAll()
    {
        if (Checkers.Count <= 0)
        {
            RefreshAllParents();
            return;
        }

        List<RedDotKey> keys = new(Checkers.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            Refresh(keys[i]);
        }

        RefreshAllParents();
    }

    #endregion

    #region Subscribe

    /// <summary>
    /// 订阅指定红点节点状态变化。UI 组件应在 OnEnable 订阅，在 OnDisable 取消订阅。
    /// </summary>
    /// <param name="key">红点节点。</param>
    /// <param name="listener">状态变化回调。</param>
    /// <param name="invokeImmediately">是否立即回调当前状态。</param>
    public static void Subscribe(RedDotKey key, Action<bool> listener, bool invokeImmediately = true)
    {
        if (key == RedDotKey.None || listener == null)
        {
            return;
        }

        EnsureState(key);

        if (!Listeners.ContainsKey(key))
        {
            Listeners[key] = null;
        }

        Listeners[key] -= listener;
        Listeners[key] += listener;

        if (invokeImmediately)
        {
            listener.Invoke(IsOn(key));
        }
    }

    /// <summary>
    /// 取消订阅指定红点节点状态变化。
    /// </summary>
    /// <param name="key">红点节点。</param>
    /// <param name="listener">之前订阅的状态变化回调。</param>
    public static void Unsubscribe(RedDotKey key, Action<bool> listener)
    {
        if (key == RedDotKey.None || listener == null)
        {
            return;
        }

        if (!Listeners.TryGetValue(key, out Action<bool> callbacks))
        {
            return;
        }

        callbacks -= listener;
        if (callbacks == null)
        {
            Listeners.Remove(key);
        }
        else
        {
            Listeners[key] = callbacks;
        }
    }

    #endregion

    #region Internal Helpers

    private static void EnsureState(RedDotKey key)
    {
        if (key == RedDotKey.None || States.ContainsKey(key))
        {
            return;
        }

        States.Add(key, false);
    }

    private static void AddChild(RedDotKey parent, RedDotKey child)
    {
        if (!Children.TryGetValue(parent, out List<RedDotKey> children))
        {
            children = new List<RedDotKey>();
            Children.Add(parent, children);
        }

        if (!children.Contains(child))
        {
            children.Add(child);
        }
    }

    private static void RemoveChild(RedDotKey parent, RedDotKey child)
    {
        if (!Children.TryGetValue(parent, out List<RedDotKey> children))
        {
            return;
        }

        children.Remove(child);
    }

    private static void SetState(RedDotKey key, bool isOn)
    {
        bool oldValue = IsOn(key);
        States[key] = isOn;

        if (oldValue != isOn)
        {
            Notify(key, isOn);
        }
    }

    private static bool RefreshBranchInternal(RedDotKey key, HashSet<RedDotKey> visiting)
    {
        if (!visiting.Add(key))
        {
            Debug.LogError($"[RedDotManager] Circular node relationship detected. Key={key}");
            return false;
        }

        if (Children.TryGetValue(key, out List<RedDotKey> children))
        {
            for (int i = 0; i < children.Count; i++)
            {
                if (RefreshBranchInternal(children[i], visiting))
                {
                    continue;
                }

                visiting.Remove(key);
                return false;
            }
        }

        SetState(key, EvaluateNode(key));
        visiting.Remove(key);
        return true;
    }

    private static bool EvaluateNode(RedDotKey key)
    {
        bool hasStateSource = false;
        bool isOn = false;

        if (Checkers.TryGetValue(key, out IRedDotChecker checker))
        {
            hasStateSource = true;
            isOn = EvaluateChecker(key, checker);
        }

        if (Children.TryGetValue(key, out List<RedDotKey> children) && children.Count > 0)
        {
            hasStateSource = true;
            for (int i = 0; i < children.Count; i++)
            {
                if (!IsOn(children[i]))
                {
                    continue;
                }

                isOn = true;
                break;
            }
        }

        return hasStateSource ? isOn : IsOn(key);
    }

    private static bool EvaluateChecker(RedDotKey key, IRedDotChecker checker)
    {
        try
        {
            return checker != null && checker.IsOn();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RedDotManager] Checker failed. Key={key}, Exception={ex}");
            return false;
        }
    }

    private static void RefreshParentChain(RedDotKey key)
    {
        RedDotKey current = key;
        while (Parents.TryGetValue(current, out RedDotKey parent) && parent != RedDotKey.None)
        {
            SetState(parent, EvaluateNode(parent));
            current = parent;
        }
    }

    private static void RefreshAllParents()
    {
        List<RedDotKey> parentKeys = new(Children.Keys);
        for (int i = 0; i < parentKeys.Count; i++)
        {
            Refresh(parentKeys[i]);
        }
    }

    private static void Notify(RedDotKey key, bool isOn)
    {
        if (!Listeners.TryGetValue(key, out Action<bool> callbacks) || callbacks == null)
        {
            return;
        }

        Delegate[] invocationList = callbacks.GetInvocationList();
        for (int i = 0; i < invocationList.Length; i++)
        {
            if (invocationList[i] is not Action<bool> callback)
            {
                continue;
            }

            try
            {
                callback.Invoke(isOn);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RedDotManager] Listener failed. Key={key}, Exception={ex}");
            }
        }
    }

    #endregion

    #region Nested Types

    private sealed class FuncRedDotChecker : IRedDotChecker
    {
        private readonly Func<bool> _checker;

        public FuncRedDotChecker(Func<bool> checker)
        {
            _checker = checker;
        }

        public bool IsOn()
        {
            return _checker != null && _checker.Invoke();
        }
    }

    private sealed class RedDotRegistry : IRedDotRegistry
    {
        public static readonly RedDotRegistry Instance = new();

        public void RegisterNode(RedDotKey key, RedDotKey parent = RedDotKey.None)
        {
            RedDotManager.RegisterNode(key, parent);
        }

        public void RegisterChecker(RedDotKey key, IRedDotChecker checker)
        {
            RedDotManager.RegisterChecker(key, checker);
        }

        public void RegisterChecker(RedDotKey key, Func<bool> checker)
        {
            RedDotManager.RegisterChecker(key, checker);
        }
    }

    #endregion
}
