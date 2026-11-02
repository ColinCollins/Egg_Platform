using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 控制明信片 Image 临时切换到 UI/PageCurl 运行时材质，并播放翻页开合表现。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Graphic))]
[AddComponentMenu("Game/Postcard/Postcard Turn Effect")]
public class Postcard_TurnEffect : MonoBehaviour
{
    private const string PageCurlShaderName = "UI/PageCurl";
    private const float OpenStartDistance = 1.5f;
    private const float OpenEndDistance = -1f;
    private const float CloseStartDistance = -1f;
    private const float CloseEndDistance = 1.5f;

    private static readonly int AngleId = Shader.PropertyToID("_Angle");
    private static readonly int DistanceId = Shader.PropertyToID("_Distance");
    private static readonly int RadiusId = Shader.PropertyToID("_Radius");
    private static readonly int ShadowStrengthId = Shader.PropertyToID("_ShadowStrength");
    private static readonly int BackColorId = Shader.PropertyToID("_BackColor");

    /// <summary>
    /// 用途：播放翻页表现时被替换材质的目标 Graphic，通常是明信片页 Image。
    /// 调用边界：优先由 prefab/Inspector 显式绑定；为空时运行时回退到当前对象上的 Graphic。
    /// 注意事项：组件只替换该 Graphic 的 material，不递归处理子节点。
    /// </summary>
    [SerializeField] private Graphic targetGraphic;

    /// <summary>
    /// 用途：控制 OpenPage / ClosePage 的播放时长。
    /// 调用边界：由运行时表现或 Inspector 配置读取。
    /// 注意事项：会被限制为不小于 0，0 表示立即写入结束状态。
    /// </summary>
    [SerializeField] private float duration = 1f;

    /// <summary>
    /// 用途：控制翻页动画的缓动曲线。
    /// 调用边界：由 DOTween 播放距离参数时读取。
    /// 注意事项：只影响本组件启动的 tween，不影响其它页面动画。
    /// </summary>
    [SerializeField] private Ease ease = Ease.InOutSine;

    /// <summary>
    /// 用途：写入 UI/PageCurl shader 的 _Angle，控制折线方向。
    /// 调用边界：播放表现和运行时刷新材质参数时写入私有材质实例。
    /// 注意事项：默认值按当前明信片翻页表现配置。
    /// </summary>
    [SerializeField, Range(-180f, 180f)] private float angle = -46f;

    /// <summary>
    /// 用途：写入 UI/PageCurl shader 的 _Radius，控制翻页卷曲半径。
    /// 调用边界：播放表现和运行时刷新材质参数时写入私有材质实例。
    /// 注意事项：默认值按当前明信片翻页表现配置。
    /// </summary>
    [SerializeField, Range(0.01f, 1f)] private float radius = 0.263f;

    /// <summary>
    /// 用途：写入 UI/PageCurl shader 的 _ShadowStrength，控制折页阴影强度。
    /// 调用边界：播放表现和运行时刷新材质参数时写入私有材质实例。
    /// 注意事项：默认值按当前明信片翻页表现配置。
    /// </summary>
    [SerializeField, Range(0f, 1f)] private float shadowStrength = 0.273f;

    /// <summary>
    /// 用途：写入 UI/PageCurl shader 的 _BackColor，控制背面颜色。
    /// 调用边界：播放表现和运行时刷新材质参数时写入私有材质实例。
    /// 注意事项：默认值按当前明信片翻页表现配置。
    /// </summary>
    [SerializeField] private Color backColor = Color.white;

    /// <summary>
    /// 用途：ClosePage 播放完成并回到完全可见状态后，是否恢复目标 Graphic 原材质。
    /// 调用边界：由 ClosePage 完成回调读取。
    /// 注意事项：OpenPage 结束时需要保留翻页材质的隐藏状态，因此不会自动恢复原材质。
    /// </summary>
    [SerializeField] private bool restoreOriginalMaterialAfterClose = true;

    private Graphic _appliedGraphic;
    private Material _originalMaterial;
    private bool _hasOriginalMaterial;
    private Material _runtimeMaterial;
    private Tween _turnTween;
    private bool _forceCompleteOnInterrupt;

    /// <summary>
    /// 用途：外部运行时代码订阅翻页播放完成通知。
    /// 调用边界：`OpenPage`、`ClosePage` 或 `TurnPage` 成功播放到结束后由本组件触发。
    /// 注意事项：新播放打断旧播放或播放未成功启动时不会触发；`TryClosePage` 显式传入强制完成标记后，组件禁用或销毁会触发。
    /// </summary>
    public event Action Complete;

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveTargetGraphic();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ClampSettings();

        if (Application.isPlaying && _runtimeMaterial != null)
            ApplyMaterialProperties(_runtimeMaterial);
    }
#endif

    private void OnDisable()
    {
        StopTurnTween(_forceCompleteOnInterrupt);
        RestoreOriginalMaterial();
    }

    private void OnDestroy()
    {
        StopTurnTween(_forceCompleteOnInterrupt);
        RestoreOriginalMaterial();
        DestroyRuntimeMaterial();
    }

    #endregion

    #region Public API

    /// <summary>
    /// 用途：播放打开页表现，使目标 Image 从可见状态翻到隐藏状态。
    /// 调用边界：供按钮、动画事件或明信片页面流程在运行时调用。
    /// 注意事项：播放开始时会替换目标材质；播放结束后保持翻页材质以维持隐藏结果。
    /// </summary>
    public void OpenPage()
    {
        OpenPage(null);
    }

    /// <summary>
    /// 用途：播放打开页表现，并在表现结束后执行回调。
    /// 调用边界：供需要串联后续表现的运行时代码调用。
    /// 注意事项：该方法不写 DB、不派发全局事件、不修改共享材质资产。
    /// </summary>
    /// <param name="onComplete">打开页表现完成后的回调，可为空。</param>
    public void OpenPage(Action onComplete)
    {
        TurnPageInternal(OpenStartDistance, OpenEndDistance, onComplete, false, false);
    }

    /// <summary>
    /// 用途：尝试播放打开页表现，并把是否成功启动反馈给调用方。
    /// 调用边界：供必须保证后续流程不被 shader 或目标缺失卡住的运行时代码调用。
    /// 注意事项：返回 false 时不会调用 onComplete，也不会触发 Complete 事件，调用方需要自行降级完成。
    /// </summary>
    /// <param name="onComplete">打开页表现完成后的回调，可为空。</param>
    /// <returns>翻页表现成功启动或立即完成时返回 true；缺少运行时、目标或 shader 时返回 false。</returns>
    public bool TryOpenPage(Action onComplete = null)
    {
        return TurnPageInternal(OpenStartDistance, OpenEndDistance, onComplete, false, false);
    }

    /// <summary>
    /// 用途：播放关闭页表现，使目标 Image 从隐藏状态翻到可见状态。
    /// 调用边界：供按钮、动画事件或明信片页面流程在运行时调用。
    /// 注意事项：默认会在播放完成后恢复原材质，可通过 restoreOriginalMaterialAfterClose 关闭该行为。
    /// </summary>
    public void ClosePage()
    {
        ClosePage(null);
    }

    /// <summary>
    /// 用途：播放关闭页表现，并在表现结束后执行回调。
    /// 调用边界：供需要串联后续表现的运行时代码调用。
    /// 注意事项：恢复原材质发生在回调前，避免后续逻辑读到临时翻页材质。
    /// </summary>
    /// <param name="onComplete">关闭页表现完成后的回调，可为空。</param>
    public void ClosePage(Action onComplete)
    {
        TurnPageInternal(CloseStartDistance, CloseEndDistance, onComplete, restoreOriginalMaterialAfterClose, false);
    }

    /// <summary>
    /// 用途：尝试播放关闭页表现，并把是否成功启动反馈给调用方。
    /// 调用边界：供必须保证后续流程不被 shader 或目标缺失卡住的运行时代码调用。
    /// 注意事项：返回 false 时不会触发 Complete 事件，调用方需要自行降级完成。
    /// </summary>
    /// <param name="onComplete">关闭页表现完成后的回调，可为空。</param>
    /// <param name="forceCompleteOnInterrupt">true 时组件被禁用或销毁会强制完成当前 tween 并执行回调；false 时维持中断即取消的既有行为。</param>
    /// <returns>翻页表现成功启动或立即完成时返回 true；缺少运行时、目标或 shader 时返回 false。</returns>
    public bool TryClosePage(Action onComplete = null, bool forceCompleteOnInterrupt = false)
    {
        return TurnPageInternal(
            CloseStartDistance,
            CloseEndDistance,
            onComplete,
            restoreOriginalMaterialAfterClose,
            forceCompleteOnInterrupt);
    }

    /// <summary>
    /// 用途：按自定义距离范围播放翻页材质的 _Distance 参数。
    /// 调用边界：供运行时代码在 OpenPage / ClosePage 之外复用同一材质控制。
    /// 注意事项：自定义播放完成后不会自动恢复原材质，需要调用 RestoreOriginalMaterial()。
    /// </summary>
    /// <param name="fromDistance">动画起始 _Distance。</param>
    /// <param name="toDistance">动画结束 _Distance。</param>
    /// <param name="onComplete">动画完成后的回调，可为空。</param>
    public void TurnPage(float fromDistance, float toDistance, Action onComplete = null)
    {
        TurnPageInternal(fromDistance, toDistance, onComplete, false, false);
    }

    /// <summary>
    /// 用途：尝试按自定义距离范围播放翻页材质的 _Distance 参数。
    /// 调用边界：供外部需要明确知道播放是否成功启动的运行时代码调用。
    /// 注意事项：返回 false 时不会触发 Complete 事件，调用方需要自行降级完成。
    /// </summary>
    /// <param name="fromDistance">动画起始 _Distance。</param>
    /// <param name="toDistance">动画结束 _Distance。</param>
    /// <param name="onComplete">动画完成后的回调，可为空。</param>
    /// <returns>翻页表现成功启动或立即完成时返回 true；缺少运行时、目标或 shader 时返回 false。</returns>
    public bool TryTurnPage(float fromDistance, float toDistance, Action onComplete = null)
    {
        return TurnPageInternal(fromDistance, toDistance, onComplete, false, false);
    }

    /// <summary>
    /// 用途：用 0..1 进度直接写入翻页距离，便于拖拽或 Timeline 驱动。
    /// 调用边界：供运行时表现系统调用。
    /// 注意事项：调用时同样会替换目标材质；恢复原材质需要调用 RestoreOriginalMaterial()。
    /// </summary>
    /// <param name="progress">翻页进度，0 表示隐藏侧，1 表示可见侧。</param>
    public void SetProgress(float progress)
    {
        if (!TryApplyPageCurlMaterial())
            return;

        float distance = Mathf.Lerp(OpenEndDistance, OpenStartDistance, Mathf.Clamp01(progress));
        SetDistance(distance);
    }

    /// <summary>
    /// 用途：把目标 Graphic 恢复到播放翻页表现前缓存的原材质。
    /// 调用边界：供关闭表现完成、页面退出或外部流程需要中断翻页材质时调用。
    /// 注意事项：只恢复本组件缓存过的目标；不会销毁目标原材质。
    /// </summary>
    public void RestoreOriginalMaterial()
    {
        if (!_hasOriginalMaterial)
            return;

        if (_appliedGraphic != null && _appliedGraphic.material != _originalMaterial)
            _appliedGraphic.material = _originalMaterial;

        _appliedGraphic = null;
        _originalMaterial = null;
        _hasOriginalMaterial = false;
    }

    #endregion

    #region Material Control

    private bool TurnPageInternal(
        float fromDistance,
        float toDistance,
        Action onComplete,
        bool restoreOnComplete,
        bool forceCompleteOnInterrupt)
    {
        if (!TryApplyPageCurlMaterial())
            return false;

        StopTurnTween(false);
        _forceCompleteOnInterrupt = forceCompleteOnInterrupt;
        SetDistance(fromDistance);

        if (duration <= 0f)
        {
            SetDistance(toDistance);
            CompleteTurn(onComplete, restoreOnComplete);
            return true;
        }

        _turnTween = DOVirtual.Float(fromDistance, toDistance, duration, SetDistance)
            .SetEase(ease)
            .OnComplete(() => CompleteTurn(onComplete, restoreOnComplete));
        return true;
    }

    private void CompleteTurn(Action onComplete, bool restoreOnComplete)
    {
        _turnTween = null;
        _forceCompleteOnInterrupt = false;

        if (restoreOnComplete)
            RestoreOriginalMaterial();

        onComplete?.Invoke();
        Complete?.Invoke();
    }

    private bool TryApplyPageCurlMaterial()
    {
        if (!Application.isPlaying)
            return false;

        ClampSettings();

        Graphic graphic = ResolveTargetGraphic();
        if (graphic == null)
        {
            Debug.LogWarning("[Postcard_TurnEffect] Cannot play because target Graphic is null.", this);
            return false;
        }

        Material material = ResolveRuntimeMaterial();
        if (material == null)
        {
            Debug.LogWarning($"[Postcard_TurnEffect] Cannot play because shader '{PageCurlShaderName}' was not found.", this);
            return false;
        }

        if (_appliedGraphic != null && _appliedGraphic != graphic)
            RestoreOriginalMaterial();

        if (!_hasOriginalMaterial)
        {
            _originalMaterial = graphic.material;
            _hasOriginalMaterial = true;
            _appliedGraphic = graphic;
        }

        ApplyMaterialProperties(material);

        if (graphic.material != material)
            graphic.material = material;

        return true;
    }

    private Graphic ResolveTargetGraphic()
    {
        if (targetGraphic == null)
            targetGraphic = GetComponent<Graphic>();

        return targetGraphic;
    }

    private Material ResolveRuntimeMaterial()
    {
        if (_runtimeMaterial != null)
            return _runtimeMaterial;

        Shader shader = Shader.Find(PageCurlShaderName);
        if (shader == null)
            return null;

        _runtimeMaterial = new Material(shader)
        {
            name = "PostcardTurnEffect_PageCurlRuntime",
            hideFlags = HideFlags.HideAndDontSave
        };
        return _runtimeMaterial;
    }

    private void ApplyMaterialProperties(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty(AngleId))
            material.SetFloat(AngleId, angle);

        if (material.HasProperty(RadiusId))
            material.SetFloat(RadiusId, radius);

        if (material.HasProperty(ShadowStrengthId))
            material.SetFloat(ShadowStrengthId, shadowStrength);

        if (material.HasProperty(BackColorId))
            material.SetColor(BackColorId, backColor);
    }

    private void SetDistance(float distance)
    {
        if (_runtimeMaterial != null && _runtimeMaterial.HasProperty(DistanceId))
            _runtimeMaterial.SetFloat(DistanceId, distance);
    }

    private void StopTurnTween(bool forceComplete)
    {
        if (_turnTween == null)
        {
            _forceCompleteOnInterrupt = false;
            return;
        }

        _turnTween.Kill(forceComplete);
        _turnTween = null;
        _forceCompleteOnInterrupt = false;
    }

    private void DestroyRuntimeMaterial()
    {
        if (_runtimeMaterial == null)
            return;

        if (Application.isPlaying)
            Destroy(_runtimeMaterial);
        else
            DestroyImmediate(_runtimeMaterial);

        _runtimeMaterial = null;
    }

    private void ClampSettings()
    {
        duration = Mathf.Max(0f, duration);
        angle = Mathf.Clamp(angle, -180f, 180f);
        radius = Mathf.Clamp(radius, 0.01f, 1f);
        shadowStrength = Mathf.Clamp01(shadowStrength);
    }

    #endregion
}
