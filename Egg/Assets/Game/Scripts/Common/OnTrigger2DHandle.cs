using System;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class Collider2DEvent : UnityEvent<Collider2D> { }

public class OnTrigger2DHandle : MonoBehaviour
{
    [SerializeField] private string targetTag = "";
    
    [SerializeField] private Collider2DEvent onEnter = new Collider2DEvent();
    
    /// <summary>
    /// 在 Inspector 中可绑定的事件
    /// </summary>
    public Collider2DEvent OnEnter => onEnter;

    private void OnTriggerEnter2D(Collider2D other) 
    {
        // 如果未设置 tag 或 tag 匹配，则触发
        if (string.IsNullOrEmpty(targetTag) || other.CompareTag(targetTag))
        {
            onEnter?.Invoke(other);
        }
    }
}
