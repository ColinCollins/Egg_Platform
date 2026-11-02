using Bear.Logger;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 道具数量块的短提示组件；只负责显示、延时刷新和隐藏，不负责修改背包或存档数据。
/// 调用方需要在奖励数据已经写入后调用 ShowAndRefreshAfterHold，并确保目标 PropBlock 已绑定正确道具。
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class PropBlockNotice : MonoBehaviour, IDebuger
{
    #region Serialized Fields

    /// <summary>
    /// 需要展示并刷新数量的道具块；为空时会从当前节点或子节点自动查找，找不到只跳过刷新。
    /// </summary>
    [SerializeField] private PropBlock targetPropBlock;

    /// <summary>
    /// 控制提示显隐的 CanvasGroup；为空时使用当前节点上的 CanvasGroup。
    /// </summary>
    [SerializeField] private CanvasGroup targetCanvasGroup;

    /// <summary>
    /// 淡入耗时；用于 HUD 奖励提示出现，不应设置过长以免遮挡操作。
    /// </summary>
    [SerializeField] private float fadeInDuration = 0.2f;

    /// <summary>
    /// 刷新后继续停留的时间；调用边界是奖励飞行动画已经完成之后。
    /// </summary>
    [SerializeField] private float holdDuration = 2f;

    /// <summary>
    /// 淡出耗时；结束后组件会保持透明且不拦截点击。
    /// </summary>
    [SerializeField] private float fadeOutDuration = 0.2f;

    /// <summary>
    /// 是否忽略 Time.timeScale；HUD 提示通常需要在暂停/转场期间也能完成清理。
    /// </summary>
    [SerializeField] private bool ignoreTimeScale = true;

    #endregion

    #region Runtime State

    private Sequence currentSequence;
    private int showVersion;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveRefs();
        HideImmediately();
    }

    private void OnDisable()
    {
        StopNotice();
        HideImmediately();
    }

    private void OnDestroy()
    {
        StopNotice();
    }

    #endregion

    #region Public API

    /// <summary>
    /// 展示道具块但不刷新数量；用于只需要临时显示目标位置的场景。
    /// 调用方仍需保证目标 UI 已处于当前面板生命周期内。
    /// </summary>
    public void Show()
    {
        PlayNotice(false).Forget();
    }

    /// <summary>
    /// 展示提示、刷新目标 PropBlock 数量并在短暂停留后隐藏；奖励数据必须已由调用方写入。
    /// 如果连续触发，后一次会取消前一次动画并以最后一次刷新结果为准。
    /// </summary>
    public void ShowAndRefreshAfterHold()
    {
        PlayNotice(true).Forget();
    }

    /// <summary>
    /// 获取目标道具块的世界坐标，供场景奖励飞行动画作为终点使用。
    /// 返回 false 表示目标未绑定或已失效，调用方应走无 UI 终点的降级表现。
    /// </summary>
    public bool TryGetTargetWorldPosition(out Vector3 position)
    {
        ResolveRefs();
        if (targetPropBlock == null)
        {
            position = default;
            return false;
        }

        position = targetPropBlock.transform.position;
        return true;
    }

    /// <summary>
    /// 立即停止当前提示并隐藏；用于面板关闭、关卡重置或外部生命周期清理。
    /// </summary>
    public void StopAndHide()
    {
        StopNotice();
        HideImmediately();
    }

    /// <summary>
    /// 使用淡出动画隐藏当前提示；用于外部需要保留隐藏过渡而不是立即清理的场景。
    /// 调用后会取消当前提示流程并接管 CanvasGroup.DOFade，结束时保持透明且不拦截点击。
    /// </summary>
    public void HideWithFade()
    {
        ResolveRefs();
        if (targetCanvasGroup == null)
        {
            this.LogWarning("[PropBlockNotice] CanvasGroup is missing.");
            return;
        }

        StopNotice();
        SetVisibleState(true);
        StartHideFade();
    }

    #endregion

    #region Internal Flow

    private async UniTaskVoid PlayNotice(bool refreshAfterFadeIn)
    {
        ResolveRefs();
        if (targetCanvasGroup == null)
        {
            this.LogWarning("[PropBlockNotice] CanvasGroup is missing.");
            return;
        }

        StopNotice();
        int version = ++showVersion;
        SetVisibleState(true);

        currentSequence = DOTween.Sequence()
            .SetUpdate(ignoreTimeScale)
            .Append(targetCanvasGroup.DOFade(1f, Mathf.Max(0f, fadeInDuration)));

        await currentSequence.AsyncWaitForCompletion();
        if (!CanContinue(version))
            return;

        if (refreshAfterFadeIn)
            targetPropBlock?.UpdateData();

        float waitTime = Mathf.Max(0f, holdDuration);
        if (waitTime > 0f)
            await UniTask.WaitForSeconds(waitTime, ignoreTimeScale);

        if (!CanContinue(version))
            return;

        HideWithFade();
    }

    private bool CanContinue(int version)
    {
        return this != null && isActiveAndEnabled && version == showVersion && targetCanvasGroup != null;
    }

    private void ResolveRefs()
    {
        if (targetCanvasGroup == null)
            targetCanvasGroup = GetComponent<CanvasGroup>();

        if (targetPropBlock == null)
            targetPropBlock = GetComponent<PropBlock>() ?? GetComponentInChildren<PropBlock>(true);
    }

    private void SetVisibleState(bool isVisible)
    {
        if (targetCanvasGroup == null)
            return;

        targetCanvasGroup.alpha = isVisible ? targetCanvasGroup.alpha : 0f;
        targetCanvasGroup.blocksRaycasts = false;
        targetCanvasGroup.interactable = false;
    }

    private void HideImmediately()
    {
        ResolveRefs();
        if (targetCanvasGroup == null)
            return;

        targetCanvasGroup.alpha = 0f;
        targetCanvasGroup.blocksRaycasts = false;
        targetCanvasGroup.interactable = false;
    }

    private void StartHideFade()
    {
        Sequence hideSequence = DOTween.Sequence()
            .SetUpdate(ignoreTimeScale);

        hideSequence
            .Append(targetCanvasGroup.DOFade(0f, Mathf.Max(0f, fadeOutDuration)))
            .OnComplete(() => SetVisibleState(false))
            .OnKill(() =>
            {
                if (currentSequence == hideSequence)
                    currentSequence = null;
            });

        currentSequence = hideSequence;
    }

    private void StopNotice()
    {
        showVersion++;
        if (currentSequence != null && currentSequence.IsActive())
        {
            currentSequence.Kill();
            currentSequence = null;
        }

        if (targetCanvasGroup != null)
            targetCanvasGroup.DOKill();
    }

    #endregion
}
