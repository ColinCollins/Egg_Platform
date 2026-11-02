using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DrawGame 第 4 关建立的首笔刚体释放控制器，当前由 DLevel_004 与 DLevel_016 复用。
/// 仅响应宿主关卡第一笔有效画线完成回调，不承担关卡结算或画线输入职责。
/// </summary>
public sealed class DLevel4Ctrl : MonoBehaviour, IDrawStrokeCompletedHandler
{
    #region Inspector References

    /// <summary>
    /// 第一笔有效画线完成后切换为 Dynamic 的机关刚体。
    /// 调用边界：只绑定当前宿主 DLevel prefab 内需要同步释放的 Rigidbody2D。
    /// 注意事项：目标的初始 Kinematic 状态由各宿主 prefab 保证。
    /// </summary>
    [SerializeField] private List<Rigidbody2D> boxRigidbodies = new();

    #endregion

    #region IDrawStrokeCompletedHandler

    /// <summary>
    /// 第一笔有效画线完成后，将所有已绑定机关刚体切换为 Dynamic。
    /// 后续笔画不会再次修改刚体状态。
    /// </summary>
    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying || !result.IsFirstStroke)
            return;

        for (int i = 0; i < boxRigidbodies.Count; i++)
        {
            Rigidbody2D boxRigidbody = boxRigidbodies[i];
            if (boxRigidbody == null)
                continue;

            boxRigidbody.bodyType = RigidbodyType2D.Dynamic;
            boxRigidbody.WakeUp();
        }
    }

    /// <summary>
    /// 当前 Reset/Replay 会销毁并重建整个 DLevel，机关初态由新 Prefab 实例提供。
    /// </summary>
    public void OnDrawAttemptReset()
    {
    }

    #endregion
}
