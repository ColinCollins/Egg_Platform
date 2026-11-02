using UnityEngine;

/// <summary>
/// Level121 门锁金币碰撞接收器；只把本触发区的碰撞转发给关卡控制器。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class Level121LockHitReceiver : MonoBehaviour
{
    #region Inspector

    [Tooltip("所属 Level121 控制器；仅处理本关金币命中，不派发全局事件。")]
    [SerializeField] private Level121Ctrl levelCtrl;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (levelCtrl == null)
        {
            levelCtrl = GetComponentInParent<Level121Ctrl>();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        levelCtrl?.HandleCoinHitLock(other);
    }

    #endregion
}
