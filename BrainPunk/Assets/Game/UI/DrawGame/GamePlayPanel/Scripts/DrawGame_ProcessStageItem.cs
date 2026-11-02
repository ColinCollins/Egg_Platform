using DG.Tweening;
using UnityEngine;

/// <summary>
/// Draw 墨水进度条上的单个星级阈值节点。
/// 调用边界：由 DrawGame_InkProcessHandle 按当前关卡 InkStarStage 配置位置和达标状态。
/// 注意事项：节点位置以基础短状态 process 宽度为 100%，RewardAD 拉伸时保持不动。
/// </summary>
public sealed class DrawGame_ProcessStageItem : MonoBehaviour
{
    #region Inspector References

    /// <summary>
    /// 实际移动的阈值节点；为空时使用当前对象的 RectTransform。
    /// </summary>
    [SerializeField] private RectTransform stageHandle;

    /// <summary>
    /// 达标时显示的前景星；墨水低于阈值时隐藏，底层未达标图保持显示。
    /// </summary>
    [SerializeField] private GameObject reachedState;

    #endregion

    #region Runtime State

    private float _normalizedPosition;

    #endregion

    #region Public API

    /// <summary>
    /// 绑定到进度条并记录阈值位置。
    /// 调用边界：normalizedPosition 必须表示 0~1 的进度比例；重复调用会覆盖旧位置。
    /// </summary>
    public void Configure(
        RectTransform processParent,
        float normalizedPosition,
        float baseProcessWidth,
        bool reached)
    {
        ResolveHandle();
        if (stageHandle == null || processParent == null)
            return;

        stageHandle.DOKill();
        stageHandle.SetParent(processParent, false);
        stageHandle.anchorMin = new Vector2(0f, 0.5f);
        stageHandle.anchorMax = new Vector2(0f, 0.5f);
        stageHandle.pivot = new Vector2(0.5f, 0.5f);
        _normalizedPosition = Mathf.Clamp01(normalizedPosition);
        SetBaseProcessWidth(baseProcessWidth);
        SetReached(reached);
    }

    private void SetBaseProcessWidth(float baseProcessWidth)
    {
        ResolveHandle();
        if (stageHandle == null)
            return;

        float targetX = Mathf.Max(0f, baseProcessWidth) * _normalizedPosition;
        stageHandle.DOKill();
        Vector2 position = stageHandle.anchoredPosition;
        position.x = targetX;
        position.y = 0f;
        stageHandle.anchoredPosition = position;
    }

    /// <summary>
    /// 切换达标前景星；未达标底图由 prefab 常驻显示。
    /// </summary>
    public void SetReached(bool reached)
    {
        if (reachedState != null && reachedState.activeSelf != reached)
            reachedState.SetActive(reached);
    }

    #endregion

    #region Lifecycle

    private void OnDestroy()
    {
        stageHandle?.DOKill();
    }

    #endregion

    #region Helpers

    private void ResolveHandle()
    {
        if (stageHandle == null)
            stageHandle = transform as RectTransform;
    }

    #endregion
}
