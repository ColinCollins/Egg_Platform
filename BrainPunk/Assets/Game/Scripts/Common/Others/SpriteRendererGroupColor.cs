using UnityEngine;

/// <summary>
/// 统一控制一组 SpriteRenderer 的 RGB 颜色，并保留每个 Renderer 自身的透明度。
/// </summary>
[DisallowMultipleComponent]
public sealed class SpriteRendererGroupColor : MonoBehaviour
{
    #region Inspector Configuration

    /// <summary>
    /// 用途：定义当前应用到整组 SpriteRenderer 的 RGB 颜色。
    /// 注意事项：透明度由各 Renderer 自身或 SpriteRendererGroupAlpha 独立控制。
    /// </summary>
    [ColorUsage(false, false)]
    [SerializeField] private Color color = Color.white;

    /// <summary>
    /// 用途：自动收集时是否包含未激活子节点上的 SpriteRenderer。
    /// </summary>
    [SerializeField] private bool includeInactive = true;

    /// <summary>
    /// 用途：在 Awake 和编辑器校验时自动收集当前节点及子节点的 SpriteRenderer。
    /// 注意事项：需要人工挑选成员时应关闭，并直接配置 renderers 数组。
    /// </summary>
    [SerializeField] private bool autoCollectRenderers = true;

    /// <summary>
    /// 用途：接收统一 RGB 颜色的 SpriteRenderer 列表。
    /// 注意事项：列表可手动配置；空引用会被安全跳过。
    /// </summary>
    [SerializeField] private SpriteRenderer[] renderers;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        if (autoCollectRenderers)
            CollectRenderers();

        ApplyColor();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        color.a = 1f;

        if (autoCollectRenderers)
            CollectRenderers();

        ApplyColor();
    }
#endif

    #endregion

    #region Public API

    /// <summary>
    /// 获取当前整组使用的 RGB 颜色。
    /// </summary>
    public Color GetColor()
    {
        return color;
    }

    /// <summary>
    /// 设置整组 SpriteRenderer 的 RGB 颜色。
    /// </summary>
    /// <param name="value">目标颜色；Alpha 会被忽略，各 Renderer 保留自身透明度。</param>
    public void SetColor(Color value)
    {
        value.a = 1f;
        color = value;
        ApplyColor();
    }

    /// <summary>
    /// 从当前节点及子节点重新收集 SpriteRenderer。
    /// 调用边界：仅在明确需要覆盖手动列表时调用。
    /// </summary>
    public void CollectRenderers()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>(includeInactive);
    }

    #endregion

    #region Internal Application

    private void ApplyColor()
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = renderers[i];
            if (spriteRenderer == null)
                continue;

            Color rendererColor = spriteRenderer.color;
            rendererColor.r = color.r;
            rendererColor.g = color.g;
            rendererColor.b = color.b;
            spriteRenderer.color = rendererColor;
        }
    }

    #endregion
}
