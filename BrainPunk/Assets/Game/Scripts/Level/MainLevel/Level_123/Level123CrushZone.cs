using UnityEngine;

/// <summary>
/// Level123 绿色无刺墙体区域的局部压力触发器。
/// 仅转发当前接触角色，墙体运动方向与纸片化条件均由 Level123Ctrl 判定。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level123CrushZone : MonoBehaviour
{
    #region Inspector

    /// <summary>
    /// 本关控制器；Prefab 应显式绑定，运行时缺失时只从当前父层级回退解析。
    /// </summary>
    [SerializeField] private Level123Ctrl levelCtrl;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (levelCtrl == null)
            levelCtrl = GetComponentInParent<Level123Ctrl>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryApplyPressure(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryApplyPressure(other);
    }

    #endregion

    #region Pressure Routing

    private void TryApplyPressure(Collider2D other)
    {
        if (!Application.isPlaying || other == null || levelCtrl == null)
            return;

        levelCtrl.TryCrushActorFromGreenZone(other);
    }

    #endregion
}
