using DG.Tweening;
using Game.Common;
using UnityEngine;

/// <summary>
/// Shit 对象，prefab 上需挂载此脚本，用于对象池回收与自动销毁
/// </summary>
public class ShitItem : MonoBehaviour, IRecycle
{
    [SerializeField] private float autoRecycleY = -10f;
    [SerializeField] private float spawnScaleDuration = 0.2f;
    [SerializeField] private Ease spawnScaleEase = Ease.OutBack;

    private Transform cachedTransform;
    private bool isActive;
    private Vector3 initialScale;

    private void Awake()
    {
        cachedTransform = transform;
        initialScale = cachedTransform.localScale;
    }

    private void Update()
    {
        if (!isActive)
            return;

        if (cachedTransform.position.y < autoRecycleY)
        {
            RecycleSelf();
        }
    }

    public void OnSpawn()
    {
        cachedTransform.DOKill();
        gameObject.SetActive(true);
        cachedTransform.localScale = Vector3.zero;
        cachedTransform.DOScale(initialScale, Mathf.Max(0f, spawnScaleDuration)).SetEase(spawnScaleEase);
        isActive = true;
    }

    public void OnRecycle()
    {
        cachedTransform.DOKill();
        isActive = false;
        gameObject.SetActive(false);
    }

    public void RecycleSelf()
    {
        if (!isActive)
            return;

        isActive = false;
        if (ObjectPoolManager.Instance != null)
        {
            ObjectPoolManager.Instance.Recycle(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
