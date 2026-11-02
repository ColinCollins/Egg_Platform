using DG.Tweening;
using Game.ItemEvent;
using UnityEngine;

/// <summary>
/// 缩放监听器：将目标 Transform 的 localScale 缩放到指定大小，完成后设置 IsDone = true
/// </summary>
public class ItemScaleListener : BaseItemEventHandle
{
    [Header("Target Settings")]
    [SerializeField] private Transform target;

    [Header("Scale Settings")]
    [Tooltip("目标缩放值（localScale）")]
    [SerializeField] private Vector3 targetScale = Vector3.one;

    [Tooltip("缩放时长（秒）")]
    [SerializeField] private float duration = 0.5f;

    [Tooltip("缩放缓动曲线")]
    [SerializeField] private AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [SerializeField] private bool isWaitingFinished = false;

    private Tweener scaleTweener;
    private Vector3 initialScale;


    private void Awake()
    {
        if (target == null)
            target = transform;
        initialScale = target.localScale;
    }

    public override void Execute()
    {
        if (target == null)
        {
            Debug.LogWarning("[ItemScaleListener] Target is null!");
            IsDone = true;
            return;
        }

        if (isWaitingFinished)
        {
            IsRunning = true;
            IsDone = false;
        }
        else
        {
            IsRunning = false;
            IsDone = true;
        }

        Debug.Log("Scale changed --------- " + targetScale);
        scaleTweener?.Kill();
        scaleTweener = target.DOScale(targetScale, duration)
            .SetEase(easeCurve)
            .OnComplete(() =>
            {
                IsRunning = false;
                IsDone = true;
            });
    }

    /// <summary>
    /// 停止缩放动画
    /// </summary>
    public void Stop()
    {
        if (scaleTweener != null && scaleTweener.IsActive())
            scaleTweener.Kill();
        IsRunning = false;
        IsDone = true;
    }

    /// <summary>
    /// 重置到初始缩放
    /// </summary>
    public void ResetToInitial()
    {
        Stop();
        if (target != null)
            target.localScale = initialScale;
    }

    private void OnDestroy()
    {
        if (scaleTweener != null && scaleTweener.IsActive())
            scaleTweener.Kill();
    }
}
