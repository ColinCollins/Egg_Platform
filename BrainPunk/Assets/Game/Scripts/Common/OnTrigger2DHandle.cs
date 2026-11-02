using System;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class Collider2DEvent : UnityEvent<Collider2D> { }

public class OnTrigger2DHandle : MonoBehaviour
{
    [SerializeField] private string targetTag = "";

    [SerializeField] private Collider2DEvent onEnter = new Collider2DEvent();


    [SerializeField] private Collider2DEvent onExit = new Collider2DEvent();

    /// <summary>
    /// 在 Inspector 中可绑定的事件
    /// </summary>
    public Collider2DEvent OnEnter => onEnter;


    public Collider2DEvent OnExit => onExit;

    /// <summary>
    /// 运行时更新触发器的 Tag 过滤条件；传入空字符串表示接收所有 Collider2D。
    /// 调用边界：只改变当前组件实例，不修改 Prefab 资源或其它触发器。
    /// </summary>
    public void SetTargetTag(string value)
    {
        targetTag = value ?? string.Empty;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 如果未设置 tag 或 tag 匹配，则触发
        if (string.IsNullOrEmpty(targetTag) || other.CompareTag(targetTag))
        {
            onEnter?.Invoke(other);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // 如果未设置 tag 或 tag 匹配，则触发
        if (string.IsNullOrEmpty(targetTag) || other.CompareTag(targetTag))
        {
            OnExit?.Invoke(other);
        }
    }
}
