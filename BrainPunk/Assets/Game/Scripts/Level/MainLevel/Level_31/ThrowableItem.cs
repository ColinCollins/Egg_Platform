using DG.Tweening;
using Game.Common;
using UnityEngine;

/// <summary>
/// 可投掷的物体，实现 IRecycle 接口用于对象池
/// </summary>
public class ThrowableItem : MonoBehaviour, IRecycle
{
    private bool _fromPool;

    public void OnSpawn()
    {
        _fromPool = true;
        gameObject.SetActive(true);
        // Debug.LogError("Spawn");
    }

    public void OnRecycle()
    {
        _fromPool = false;
        gameObject.SetActive(false);
        // Debug.LogError("Recycle");
    }

    /// <summary>
    /// 回收自身到对象池
    /// </summary>
    public void RecycleSelf()
    {
        if (_fromPool && ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.Recycle(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnDestroy()
    {
        transform.DOKill();
    }
}
