using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.ItemEvent;
using UnityEngine;

/// <summary>
/// IWanner 顺序显示监听器。
/// 由 TriggerExecutor/BaseItemExecutor 触发后，按列表顺序逐个激活目标对象。
/// </summary>
public sealed class IWannerGame_SequenceDisplayListener : BaseItemEventHandle
{
    #region Inspector

    /// <summary>
    /// 用途：保存需要按顺序显示的目标对象。
    /// 调用边界：触发或 ResetState 时会先统一隐藏；空引用会被安全跳过。
    /// 注意事项：不要把当前 Listener 所在对象配置为目标，否则隐藏自身后将无法接收触发。
    /// </summary>
    [SerializeField] private List<GameObject> targets = new();

    /// <summary>
    /// 用途：控制相邻有效目标之间的显示间隔，单位为秒。
    /// 调用边界：第一个有效目标立即显示；小于等于 0 时全部目标立即显示。
    /// 注意事项：使用游戏时间，玩法暂停时等待也会暂停。
    /// </summary>
    [Min(0f)]
    [SerializeField] private float delay;

    /// <summary>
    /// 用途：开启后从目标列表尾部向头部依次显示。
    /// 调用边界：每次显示流程开始时读取方向，立即显示全部目标时同样生效。
    /// 注意事项：默认关闭，保持从头到尾的顺序；不会修改目标列表本身。
    /// </summary>
    [SerializeField] private bool isReverse;

    #endregion

    #region Runtime State

    private CancellationTokenSource _displayCancellation;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        HideAllTargets();
    }

    private void OnDisable()
    {
        CancelDisplaySequence();
        if (!IsRunning)
            return;

        IsRunning = false;
        IsDone = true;
    }

    private void OnDestroy()
    {
        CancelDisplaySequence();
    }

    #endregion

    #region BaseItemEventHandle

    /// <summary>
    /// 用途：隐藏全部目标后，按列表顺序启动一次显示流程。
    /// 调用边界：由 TriggerExecutor/BaseItemExecutor 或明确的关卡本地逻辑调用。
    /// 注意事项：重复调用会取消旧流程并从当前方向的第一个有效目标重新开始。
    /// </summary>
    public override void Execute()
    {
        CancelDisplaySequence();
        HideAllTargets();
        IsRunning = true;
        IsDone = false;

        if (!HasValidTarget())
        {
            CompleteDisplaySequence();
            return;
        }

        if (delay <= 0f)
        {
            ShowAllTargets();
            CompleteDisplaySequence();
            return;
        }

        _displayCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            this.GetCancellationTokenOnDestroy());
        DisplayTargetsAsync(_displayCancellation.Token).Forget();
    }

    /// <summary>
    /// 用途：取消未完成的显示流程、隐藏全部目标并恢复未执行状态。
    /// 调用边界：供 BaseItemExecutor 每轮执行前和关卡 Reset 流程调用。
    /// </summary>
    public override void ResetState()
    {
        CancelDisplaySequence();
        HideAllTargets();
        base.ResetState();
    }

    #endregion

    #region Sequence

    private async UniTaskVoid DisplayTargetsAsync(CancellationToken cancellationToken)
    {
        bool hasDisplayedTarget = false;
        int step = isReverse ? -1 : 1;
        int startIndex = isReverse ? targets.Count - 1 : 0;
        for (int i = startIndex; i >= 0 && i < targets.Count; i += step)
        {
            GameObject target = targets[i];
            if (target == null)
                continue;

            if (hasDisplayedTarget)
            {
                bool isCanceled = await UniTask.WaitForSeconds(
                        delay,
                        cancellationToken: cancellationToken)
                    .SuppressCancellationThrow();
                if (isCanceled)
                    return;
            }

            target.SetActive(true);
            hasDisplayedTarget = true;
        }

        if (!cancellationToken.IsCancellationRequested)
            CompleteDisplaySequence();
    }

    private void HideAllTargets()
    {
        if (targets == null)
            return;

        for (int i = 0; i < targets.Count; i++)
        {
            GameObject target = targets[i];
            if (target != null)
                target.SetActive(false);
        }
    }

    private void ShowAllTargets()
    {
        int step = isReverse ? -1 : 1;
        int startIndex = isReverse ? targets.Count - 1 : 0;
        for (int i = startIndex; i >= 0 && i < targets.Count; i += step)
        {
            GameObject target = targets[i];
            if (target != null)
                target.SetActive(true);
        }
    }

    private bool HasValidTarget()
    {
        if (targets == null)
            return false;

        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] != null)
                return true;
        }

        return false;
    }

    private void CompleteDisplaySequence()
    {
        _displayCancellation?.Dispose();
        _displayCancellation = null;
        IsRunning = false;
        IsDone = true;
    }

    private void CancelDisplaySequence()
    {
        if (_displayCancellation == null)
            return;

        _displayCancellation.Cancel();
        _displayCancellation.Dispose();
        _displayCancellation = null;
    }

    #endregion
}
