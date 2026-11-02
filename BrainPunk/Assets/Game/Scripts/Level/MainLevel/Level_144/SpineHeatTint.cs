using Spine.Unity;
using UnityEngine;

/// <summary>
/// 挂在需要变色的 Spine 物体上，独立保存灼热颜色并控制此实例的材质覆盖。
/// 关卡只传入热度；编辑预览使用同一套调色逻辑，不初始化关卡或访问运行时服务。
/// </summary>
[DisallowMultipleComponent]
public sealed class SpineHeatTint : MonoBehaviour
{
    #region 物体引用与调色参数
    [Tooltip("当前变色物体的 Spine，在 Inspector 显式绑定。")]
    [SerializeField] private SkeletonAnimation targetSkeleton;
    [Tooltip("当前变色物体的 Renderer，只修改此实例的属性块。")]
    [SerializeField] private Renderer targetRenderer;
    [Tooltip("Spine 图集的原材质，用于明确指定被覆盖的材质。")]
    [SerializeField] private Material originalMaterial;
    [Tooltip("支持 _Color / _Black 的 Spine 双颜色材质，资产本身保持只读。")]
    [SerializeField] private Material heatMaterial;
    [Tooltip("最热时亮部的暖色乘色；编辑预览和游戏使用同一参数。")]
    [SerializeField, ColorUsage(false, true)] private Color hotTint = new Color(1f, 0.6f, 0.36f, 1f);
    [Tooltip("最热时暗部的暖色补光，保留原图线稿和纹理层次。")]
    [SerializeField, ColorUsage(false, true)] private Color hotGlow = new Color(0.46f, 0.08f, 0.015f, 0f);
    [Tooltip("运行时补光的轻微呼吸幅度，0 表示不闪动；编辑预览使用静态颜色。")]
    [SerializeField, Range(0f, 0.2f)] private float pulseAmount = 0.06f;
    #endregion

    #region 临时材质状态
    private SkeletonAnimation capturedSkeleton;
    private Renderer capturedRenderer;
    private Material capturedOriginalMaterial;
    private Material capturedHeatMaterial;
    private Material previousOverride;
    private bool hadOverride;
    private MaterialPropertyBlock originalProperties;
    private MaterialPropertyBlock tintProperties;
    private static readonly int LightId = Shader.PropertyToID("_Color");
    private static readonly int DarkId = Shader.PropertyToID("_Black");

    private bool CaptureAppearance()
    {
        if (capturedSkeleton != null) return true;
        if (targetSkeleton == null || targetRenderer == null || originalMaterial == null || heatMaterial == null) return false;
        if (targetSkeleton.Skeleton == null)
        {
            // 编辑模式只使用 Spine 自身已准备好的预览，不从此组件触发初始化。
            if (!Application.isPlaying) return false;
            targetSkeleton.Initialize(false);
            if (targetSkeleton.Skeleton == null) return false;
        }
        capturedSkeleton = targetSkeleton;
        capturedRenderer = targetRenderer;
        capturedOriginalMaterial = originalMaterial;
        capturedHeatMaterial = heatMaterial;
        originalProperties = new MaterialPropertyBlock();
        tintProperties = new MaterialPropertyBlock();
        capturedRenderer.GetPropertyBlock(originalProperties);
        hadOverride = capturedSkeleton.CustomMaterialOverride.TryGetValue(capturedOriginalMaterial, out previousOverride);
        return true;
    }

    private void ApplyHeat(float heat, float pulse)
    {
        heat = Mathf.Clamp01(heat);
        if (heat <= 0f)
        {
            RestoreCapturedAppearance();
            return;
        }
        capturedSkeleton.CustomMaterialOverride[capturedOriginalMaterial] = capturedHeatMaterial;
        capturedRenderer.GetPropertyBlock(tintProperties);
        tintProperties.SetColor(LightId, Color.Lerp(Color.white, hotTint, heat));
        tintProperties.SetColor(DarkId, Color.Lerp(Color.clear, hotGlow * pulse, heat));
        capturedRenderer.SetPropertyBlock(tintProperties);
    }

    private void RestoreCapturedAppearance()
    {
        if (capturedSkeleton != null && capturedOriginalMaterial != null)
        {
            if (hadOverride) capturedSkeleton.CustomMaterialOverride[capturedOriginalMaterial] = previousOverride;
            else capturedSkeleton.CustomMaterialOverride.Remove(capturedOriginalMaterial);
        }
        if (capturedRenderer != null && originalProperties != null) capturedRenderer.SetPropertyBlock(originalProperties);
    }

    private void OnDisable()
    {
        RestoreAppearance();
    }
    #endregion

    #region 运行时接口
    /// <summary>运行时由关卡传入 0～1 热度；0 恢复原材质，1 使用组件中配置的最热颜色。</summary>
    public void SetHeat(float heat)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || !CaptureAppearance()) return;
        ApplyHeat(heat, 1f + Mathf.Sin(Time.time * 3.5f) * pulseAmount);
    }

    /// <summary>停止显示并释放临时状态；组件停用、关卡退出、编辑预览结束或保存前调用。</summary>
    public void RestoreAppearance()
    {
        RestoreCapturedAppearance();
        if (!Application.isPlaying && capturedSkeleton != null && capturedSkeleton.Skeleton != null)
            capturedSkeleton.LateUpdate();
        capturedSkeleton = null;
        capturedRenderer = null;
        capturedOriginalMaterial = null;
        capturedHeatMaterial = null;
        previousOverride = null;
        originalProperties = null;
        tintProperties = null;
    }
    #endregion

#if UNITY_EDITOR
    #region 仅编辑器预览
    /// <summary>由本组件的 Inspector 开始本地预览；资源未就绪或组件停用时返回 false。</summary>
    public bool BeginEditorPreview()
    {
        return !Application.isPlaying && isActiveAndEnabled && CaptureAppearance();
    }

    /// <summary>编辑模式显示指定热度，不保存预览状态，不创建物体或材质实例。</summary>
    public void SetEditorPreviewHeat(float heat)
    {
        if (Application.isPlaying || !isActiveAndEnabled || !CaptureAppearance()) return;
        ApplyHeat(heat, 1f);
        // 更新现有 Spine 网格，让场景即时显示当前物体的材质覆盖。
        capturedSkeleton.LateUpdate();
    }
    #endregion
#endif
}
