using Game.Common;
using UnityEngine;

public class Level56PipeObstacle : MonoBehaviour, IRecycle
{
    private Transform cachedTransform;
    private bool fromPool;

    private void Awake()
    {
        cachedTransform = transform;
    }

    public void Init(Vector3 position, Transform parent)
    {
        if (cachedTransform == null)
        {
            cachedTransform = transform;
        }

        if (parent != null)
        {
            cachedTransform.SetParent(parent);
        }

        cachedTransform.position = position;
        gameObject.SetActive(true);
    }

    public void Move(Vector3 delta)
    {
        if (cachedTransform == null)
        {
            cachedTransform = transform;
        }

        cachedTransform.position += delta;
    }

    public void RecycleSelf()
    {
        if (fromPool && ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.Recycle(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void OnSpawn()
    {
        fromPool = true;
        gameObject.SetActive(true);
    }

    public void OnRecycle()
    {
        fromPool = false;
        gameObject.SetActive(false);
    }
}
