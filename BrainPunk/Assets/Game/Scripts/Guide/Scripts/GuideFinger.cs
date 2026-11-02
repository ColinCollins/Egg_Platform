using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 明信片局部引导的手指表现组件，统一负责目标定位、显隐和点击粒子的定时循环。
/// </summary>
public class GuideFinger : MonoBehaviour
{
    private const float ParticleLoopInterval = 1f;

    /// <summary>
    /// 用途：承载手指动画并移动到当前引导目标中心。
    /// 调用边界：仅在明信片引导运行期间由本组件控制位置和显隐。
    /// 注意事项：未绑定时会尝试查找当前组件下名为 finger 的直接子节点。
    /// </summary>
    [SerializeField] private RectTransform finger;

    /// <summary>
    /// 用途：跟随 finger 的点击提示粒子表现，在目标有效后延迟 0.5 秒开始，并每 1 秒循环播放一次。
    /// 调用边界：仅由本组件运行时显示、隐藏、禁用或销毁流程控制，不参与解锁、存档或资源加载。
    /// 注意事项：未绑定时会尝试从 finger 子节点查找；若粒子不是 finger 子节点，会在播放前同步到 finger 的世界坐标。
    /// </summary>
    [SerializeField] private ParticleSystem fingerPs;

    /// <summary>
    /// 用途：接收独立引导手指的点击，并在消费回调后销毁当前手指实例。
    /// 调用边界：仅由 `Create(...)` 创建的独立手指流程绑定；嵌入 Postcard_GuidePart 的手指不自动绑定。
    /// 注意事项：未绑定时会尝试从当前层级查找 CustomButton；找不到时只保留目标定位和粒子表现。
    /// </summary>
    [SerializeField] private CustomButton clickArea;

    [SerializeField] private float ParticleInitialDelay = .25f;

    private Action callback;
    private UniTaskCompletionSource<GuideFlowResult> _asyncCompletionSource;

    private Coroutine _particleLoop;

    #region Public API

    /// <summary>
    /// 用途：把手指移动到指定引导目标中心并启动点击粒子循环。
    /// 调用边界：仅由 `Postcard_GuidePart` 在运行态目标建立或刷新时调用。
    /// 注意事项：目标为空或手指未绑定时会转为隐藏状态；不会修改按钮、解锁或存档数据。
    /// </summary>
    /// <param name="targetRect">当前有效 guide item 的 RectTransform。</param>
    public void ShowAt(RectTransform targetRect)
    {
        if (!Application.isPlaying)
            return;

        RectTransform currentFinger = ResolveFinger();
        if (targetRect == null || currentFinger == null)
        {
            HideRuntime();
            return;
        }

        RectTransform fingerParent = currentFinger.parent as RectTransform;
        Vector3 worldCenter = targetRect.TransformPoint(targetRect.rect.center);
        Vector2 localCenter = fingerParent == null
            ? (Vector2)transform.InverseTransformPoint(worldCenter)
            : (Vector2)fingerParent.InverseTransformPoint(worldCenter);

        currentFinger.anchoredPosition = localCenter;
        currentFinger.gameObject.SetActive(true);
        StartParticleLoop();
    }

    /// <summary>
    /// 用途：隐藏引导手指，并立即停止和清空点击粒子循环。
    /// 调用边界：由 `Postcard_GuidePart` 的停止、完成和无有效目标路径调用。
    /// 注意事项：只清理本地表现，不触发引导完成回调或数据写入。
    /// </summary>
    public void Hide()
    {
        if (!Application.isPlaying)
            return;

        HideRuntime();
    }

    /// <summary>
    /// 用途：关闭并销毁由 `Create(...)` 创建的独立引导手指。
    /// 调用边界：仅由独立手指的持有者在引导停止、目标切换或主动清理时调用。
    /// 注意事项：调用后会立即解除点击回调，并在帧末销毁当前根 GameObject；不得用于挂在其它 UI 根节点上的组件。
    /// </summary>
    public void Close()
    {
        if (!Application.isPlaying)
            return;

        CompleteAsyncWait(GuideFlowResult.Cancelled);
        callback = null;
        CustomButton currentClickArea = ResolveClickArea();
        if (currentClickArea != null)
        {
            currentClickArea.OnClick -= OnClickArea;
            currentClickArea.Interactable = false;
        }

        HideRuntime();
        Destroy(gameObject);
    }

    [Button("Test")]
    public void TestFingerAnim()
    {
        StartParticleLoop();
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (Application.isPlaying)
            HideRuntime();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        CompleteAsyncWait(GuideFlowResult.Cancelled);
        UnbindClickArea();
        HideRuntime();
    }

    private void OnDestroy()
    {
        if (!Application.isPlaying)
            return;

        CompleteAsyncWait(GuideFlowResult.Cancelled);
        UnbindClickArea();
        callback = null;
        HideRuntime();
    }

    #endregion

    #region Particle Loop

    private void StartParticleLoop()
    {
        if (!isActiveAndEnabled || ResolveFingerParticle() == null)
            return;

        SyncParticlePosition();
        if (_particleLoop == null)
            _particleLoop = StartCoroutine(PlayParticleLoop());
    }

    private IEnumerator PlayParticleLoop()
    {
        yield return new WaitForSeconds(ParticleInitialDelay);

        WaitForSeconds wait = new WaitForSeconds(ParticleLoopInterval);
        while (isActiveAndEnabled && ResolveFinger() != null && finger.gameObject.activeInHierarchy)
        {
            ParticleSystem currentParticle = ResolveFingerParticle();
            if (currentParticle == null)
                break;

            SyncParticlePosition();
            currentParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            currentParticle.Play(true);
            yield return wait;
        }

        _particleLoop = null;
    }

    private void StopParticleLoop()
    {
        if (_particleLoop != null)
        {
            StopCoroutine(_particleLoop);
            _particleLoop = null;
        }

        ParticleSystem currentParticle = ResolveFingerParticle();
        if (currentParticle != null)
            currentParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void SyncParticlePosition()
    {
        RectTransform currentFinger = ResolveFinger();
        ParticleSystem currentParticle = ResolveFingerParticle();
        if (currentFinger == null || currentParticle == null)
            return;

        Transform particleTransform = currentParticle.transform;
        if (particleTransform == currentFinger || particleTransform.IsChildOf(currentFinger))
            return;

        particleTransform.position = currentFinger.position;
    }

    #endregion

    #region Runtime Helpers

    private RectTransform ResolveFinger()
    {
        if (finger != null)
            return finger;

        finger = transform.Find("finger") as RectTransform;
        return finger;
    }

    private ParticleSystem ResolveFingerParticle()
    {
        if (fingerPs != null)
            return fingerPs;

        RectTransform currentFinger = ResolveFinger();
        if (currentFinger != null)
            fingerPs = currentFinger.GetComponentInChildren<ParticleSystem>(true);

        return fingerPs;
    }

    private void HideRuntime()
    {
        StopParticleLoop();

        RectTransform currentFinger = ResolveFinger();
        if (currentFinger != null)
            currentFinger.gameObject.SetActive(false);
    }

    #endregion

    #region Independent Finger

    private void BindClickArea(Action clickCallback)
    {
        callback = clickCallback;

        CustomButton currentClickArea = ResolveClickArea();
        if (currentClickArea == null)
            return;

        currentClickArea.OnClick -= OnClickArea;
        currentClickArea.OnClick += OnClickArea;
    }

    /// <summary>
    /// 为等待式 API 绑定一次点击完成信号；旧等待存在时先按取消结束，避免同一实例悬挂多个等待者。
    /// </summary>
    private bool BindAsyncClick(UniTaskCompletionSource<GuideFlowResult> completionSource)
    {
        if (completionSource == null || ResolveClickArea() == null)
            return false;

        CompleteAsyncWait(GuideFlowResult.Cancelled);
        _asyncCompletionSource = completionSource;
        BindClickArea(() => CompleteAsyncWait(GuideFlowResult.Completed));
        return true;
    }

    /// <summary>
    /// 消费当前等待式 API 的完成源；点击、主动关闭和生命周期清理共用该入口，保证只完成一次。
    /// </summary>
    private void CompleteAsyncWait(GuideFlowResult result)
    {
        UniTaskCompletionSource<GuideFlowResult> completionSource = _asyncCompletionSource;
        _asyncCompletionSource = null;
        completionSource?.TrySetResult(result);
    }

    private void UnbindClickArea()
    {
        CustomButton currentClickArea = ResolveClickArea();
        if (currentClickArea != null)
            currentClickArea.OnClick -= OnClickArea;
    }

    private CustomButton ResolveClickArea()
    {
        if (clickArea == null)
            clickArea = GetComponentInChildren<CustomButton>(true);

        return clickArea;
    }

    private void OnClickArea(CustomButton _)
    {
        Action clickCallback = callback;
        callback = null;
        UnbindClickArea();

        try
        {
            clickCallback?.Invoke();
        }
        finally
        {
            if (this != null)
                Close();
        }
    }

    /// <summary>
    /// 用途：创建独立的明信片引导手指，并立即定位到指定目标中心。
    /// 调用边界：仅运行态由需要独立手指提示的局部 UI 流程调用。
    /// 注意事项：目标或 Resources prefab 缺失时返回 null；点击区域缺失不影响目标定位和粒子表现。
    /// </summary>
    /// <param name="target">需要指向的目标 RectTransform。</param>
    /// <param name="root">独立手指实例的 UI 父节点；GuidePart 调用时传入自身 RectTransform。</param>
    /// <param name="clickCallback">点击后执行一次的回调，可为空；回调后销毁当前手指实例。</param>
    /// <returns>创建并完成目标定位的手指组件；创建失败时返回 null。</returns>
    public static GuideFinger Create(RectTransform target, RectTransform root, Action clickCallback = null)
    {
        if (!Application.isPlaying || target == null)
            return null;

        var fingerPrefab = Resources.Load<GuideFinger>("GuideFinger");
        if (fingerPrefab == null)
            return null;

        var guideFinger = Instantiate(fingerPrefab, root);
        guideFinger.ShowAt(target);
        guideFinger.BindClickArea(clickCallback);
        return guideFinger;
    }

    /// <summary>
    /// 用途：创建独立引导手指并等待一次有效点击，以结果值代替外部回调串联。
    /// 调用边界：仅由 Guide Flow 在 Unity 主线程调用；现有 Create(...) 回调入口保持兼容。
    /// 注意事项：目标、资源或点击区域缺失时返回 Failed；外部取消、主动关闭、禁用或销毁返回 Cancelled，并清理当前实例。
    /// </summary>
    /// <param name="target">需要指向并等待点击的目标 RectTransform。</param>
    /// <param name="root">独立手指实例的 UI 父节点。</param>
    /// <param name="cancellationToken">Guide Flow 传入的统一取消令牌。</param>
    /// <returns>点击完成、取消或创建失败对应的 GuideFlowResult。</returns>
    public static async UniTask<GuideFlowResult> WaitForClickAsync(
        RectTransform target,
        RectTransform root,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return GuideFlowResult.Cancelled;

        GuideFinger guideFinger = Create(target, root);
        if (guideFinger == null)
            return GuideFlowResult.Failed;

        var completionSource = new UniTaskCompletionSource<GuideFlowResult>();
        if (!guideFinger.BindAsyncClick(completionSource))
        {
            guideFinger.Close();
            return GuideFlowResult.Failed;
        }

        try
        {
            return await completionSource.Task.AttachExternalCancellation(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (guideFinger != null)
                guideFinger.Close();

            return GuideFlowResult.Cancelled;
        }
    }

    #endregion
}
