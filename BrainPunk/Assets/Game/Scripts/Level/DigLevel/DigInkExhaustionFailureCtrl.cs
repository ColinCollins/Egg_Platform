using UnityEngine;

namespace Game.Dig
{
    /// <summary>可选的墨水耗尽失败条件；只上报所属关卡，死亡、统计和重开由既有结果链处理。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Game/Dig/Dig Ink Exhaustion Failure Ctrl")]
    public sealed class DigInkExhaustionFailureCtrl : MonoBehaviour
    {
        #region Inspector 绑定
        /// <summary>显式绑定当前关卡根DigLevelCtrl；只在挂载本组件的关卡启用耗尽失败，不扫描场景或接入TBC。</summary>
        [SerializeField] private DigLevelCtrl level;
        #endregion

        #region 运行时条件
        private void Update()
        {
            if (!Application.isPlaying || !level || !level.IsRuntimeReady ||
                level.IsPaused || level.IsResultLocked || !level.isActiveAndEnabled) return;
            if (level.InkState.RemainingInk <= 0f) level.TryFail();
        }
        #endregion
    }
}
