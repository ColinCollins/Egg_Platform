using UnityEngine;

/// <summary>
/// 第 79 关球门触发器：把足球进入球门的 Trigger2D 通知转交给本关控制器。
/// 只负责局部转发，不直接提交胜利、播放 UI 或访问共享事件。
/// </summary>
[DisallowMultipleComponent]
public sealed class DLevel79GoalSuccessHandle : MonoBehaviour
{
    #region Inspector

    /// <summary>本关根节点控制器；必须在 Prefab Inspector 显式绑定。</summary>
    [SerializeField] private DLevel79Ctrl levelCtrl;

    #endregion

    #region Unity Lifecycle

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if (Application.isPlaying)
            levelCtrl?.OnFootballEnteredGoal(collider);
    }

    #endregion
}
