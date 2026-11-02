using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 可复用红点视图组件。挂在红点 prefab 上，通过 Inspector 选择 RedDotKey。
/// 调用边界：只负责订阅 RedDotManager 并切换显示节点，不负责业务判断或状态写入。
/// </summary>
public sealed class RedDotView : MonoBehaviour
{
    #region Inspector

    /// <summary>
    /// 当前 UI 位置对应的红点节点。业务侧通过 RedDotManager 刷新同一个 key 的状态。
    /// </summary>
    [SerializeField] private RedDotKey redDotKey = RedDotKey.None;

    /// <summary>
    /// 附加红点条件，与 redDotKey 按或聚合；任一满足即显示，None 和重复项会忽略。
    /// Inspector 配置在启用时生效；运行时切换请使用 BindAny。
    /// </summary>
    [SerializeField] private RedDotKey[] additionalRedDotKeys = System.Array.Empty<RedDotKey>();

    /// <summary>
    /// 实际显示/隐藏的红点节点。建议绑定为当前对象的子物体，避免隐藏组件所在根物体后无法继续接收刷新。
    /// </summary>
    [SerializeField] private GameObject redDotRoot;

    #endregion

    #region Runtime State

    private readonly List<RedDotKey> subscribedKeys = new();

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveRedDotRoot();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ResolveRedDotRoot();
        RefreshSubscriptions();
    }

    private void OnDisable()
    {
        ClearSubscriptions();
    }

    #endregion

    #region Public API

    /// <summary>
    /// 运行时重新绑定红点节点，适合列表项复用时切换不同业务 key。
    /// 调用后会立即刷新当前显示状态；不要在编辑器态调用。
    /// </summary>
    /// <param name="key">新的红点节点。</param>
    public void Bind(RedDotKey key)
    {
        redDotKey = key;
        additionalRedDotKeys = System.Array.Empty<RedDotKey>();
        RefreshSubscriptions();
    }

    /// <summary>
    /// 替换全部绑定条件，任一 key 为 true 即显示；空数组或 null 表示解除绑定。
    /// 复制传入数组，调用方后续修改不会改变绑定；禁用时只保存配置并隐藏，启用后订阅。
    /// 编辑器态仅保存配置，不查询运行时状态或修改显示节点。
    /// </summary>
    /// <param name="keys">按或关系组合的红点节点，可包含 None 或重复项。</param>
    public void BindAny(params RedDotKey[] keys)
    {
        redDotKey = RedDotKey.None;
        additionalRedDotKeys = keys == null ? System.Array.Empty<RedDotKey>() : (RedDotKey[])keys.Clone();
        RefreshSubscriptions();
    }

    /// <summary>
    /// 取消当前红点绑定并隐藏显示节点。适合列表项回收前清理旧状态。
    /// </summary>
    public void Unbind()
    {
        Bind(RedDotKey.None);
    }

    #endregion

    #region Private Helpers

    private void RefreshSubscriptions()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        ClearSubscriptions();
        if (isActiveAndEnabled)
        {
            SubscribeKey(redDotKey);
            if (additionalRedDotKeys != null)
            {
                for (int i = 0; i < additionalRedDotKeys.Length; i++)
                {
                    SubscribeKey(additionalRedDotKeys[i]);
                }
            }
        }

        OnRedDotChanged(false);
    }

    private void SubscribeKey(RedDotKey key)
    {
        if (key == RedDotKey.None || subscribedKeys.Contains(key))
        {
            return;
        }

        subscribedKeys.Add(key);
        RedDotManager.Subscribe(key, OnRedDotChanged, false);
    }

    private void ClearSubscriptions()
    {
        for (int i = 0; i < subscribedKeys.Count; i++)
        {
            RedDotManager.Unsubscribe(subscribedKeys[i], OnRedDotChanged);
        }

        subscribedKeys.Clear();
    }

    private void ResolveRedDotRoot()
    {
        if (redDotRoot != null)
        {
            return;
        }

        if (transform.childCount > 0)
        {
            redDotRoot = transform.GetChild(0).gameObject;
        }
    }

    private void OnRedDotChanged(bool isOn)
    {
        for (int i = 0; i < subscribedKeys.Count; i++)
        {
            if (RedDotManager.IsOn(subscribedKeys[i]))
            {
                Apply(true);
                return;
            }
        }

        Apply(false);
    }

    private void Apply(bool isOn)
    {
        if (redDotRoot == null)
        {
            return;
        }

        if (redDotRoot.activeSelf != isOn)
        {
            redDotRoot.SetActive(isOn);
        }
    }

    #endregion
}
