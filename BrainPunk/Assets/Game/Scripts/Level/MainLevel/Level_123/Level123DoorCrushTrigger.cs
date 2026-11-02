using UnityEngine;

/// <summary>
/// Level123 门专属压碎触发器。
/// Enter 与 Stay 都转发，保证门先停在刺下方、墙随后下降时也不会漏判。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level123DoorCrushTrigger : MonoBehaviour
{
    /// <summary>本关控制器；Prefab 显式绑定，缺失时从父层级回退解析。</summary>
    [SerializeField] private Level123Ctrl levelCtrl;

    private void Awake()
    {
        if (levelCtrl == null)
            levelCtrl = GetComponentInParent<Level123Ctrl>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryCrush(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryCrush(other);
    }

    private void TryCrush(Collider2D other)
    {
        if (!Application.isPlaying || other == null || levelCtrl == null)
            return;

        levelCtrl.TryCrushDoorFromWall(other);
    }
}
