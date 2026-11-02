using System;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Draw 关卡的延迟开放画线 Handle。
/// 由同一 DLevel 的 DrawLevelCtrl 在进关转场完全结束后调用，等待配置时间后放行既有 BeginDrawing 流程。
/// 注意事项：该 Handle 只在首次进关时延迟输入开放，不参与资源加载、Reset/Replay 或 EnterLevelLoading 的 Erase。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Delay Auto Begin Draw Handle")]
public sealed class DelayAutoBeginDrawHandle : MonoBehaviour,
    IDrawLevelVisibleIntroHandler,
    IDrawLevelEnterOnlyIntroHandler,
    IDebuger
{
    #region Inspector

    /// <summary>
    /// 当前 Handle 所属关卡的通用状态控制器。
    /// 调用边界：必须指向同一 DLevel prefab；为空时仅在运行时向父层级解析。
    /// </summary>
    [Header("Target")]
    [Tooltip("同一 DLevel 内负责开放画线输入的 DrawLevelCtrl。")]
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;

    /// <summary>
    /// 进关转场完全结束后继续等待的秒数。
    /// 注意事项：使用游戏缩放时间，暂停或 timeScale 为 0 时不会继续计时。
    /// </summary>
    [Header("Timing")]
    [Tooltip("进关转场完全结束后，延迟多少秒开放画线输入。")]
    [SerializeField, Min(0f)] private float delay = 1f;

    #endregion

    #region IDrawLevelVisibleIntroHandler

    /// <inheritdoc />
    public async UniTask PlayDrawIntroAsync()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        if (drawLevelCtrl == null)
        {
            this.LogError("[DelayAutoBeginDrawHandle] DrawLevelCtrl 缺失，无法开放画线输入。");
            return;
        }

        if (!drawLevelCtrl.IsRuntimeReady)
        {
            this.LogWarning("[DelayAutoBeginDrawHandle] DrawLevelCtrl 尚未完成初始化，跳过延迟门禁。");
            return;
        }

        try
        {
            await UniTask.WaitForSeconds(
                Mathf.Max(0f, delay),
                cancellationToken: this.GetCancellationTokenOnDestroy());
        }
        catch (OperationCanceledException)
        {
            return;
        }
    }

    /// <inheritdoc />
    public void ApplyDrawRetryIntroState()
    {
        // 该组件只提供首次进关等待，并由 IDrawLevelEnterOnlyIntroHandler 在重开时直接跳过。
    }

    #endregion

    #region Reference Resolution

    private void ResolveReferences()
    {
        if (drawLevelCtrl == null)
            drawLevelCtrl = GetComponentInParent<DrawLevelCtrl>(true);
    }

    #endregion
}
