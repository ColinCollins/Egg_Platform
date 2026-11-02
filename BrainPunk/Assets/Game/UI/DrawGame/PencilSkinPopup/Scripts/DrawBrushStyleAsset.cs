using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// 单个 DrawGame 笔刷的只读表现配置。
/// 调用边界：由笔刷配置表记录资源路径并在运行时动态加载；消费方不得修改本资产或其共享材质。
/// 注意事项：切换当前笔刷后，本配置只应应用到之后新创建的线条，不能回写活动线或已完成线。
/// </summary>
[CreateAssetMenu(
    fileName = "DrawBrushStyle",
    menuName = "DrawGame/Pencil Skin/Brush Style")]
public sealed class DrawBrushStyleAsset : ScriptableObject
{
    #region Constants

    private const float MinimumLineWidth = 0.001f;

    #endregion

    #region Inspector Configuration

    [Header("标识与预览")]
    [Tooltip("笔刷唯一 ID；需要与外部配置表和玩家当前装备 ID 保持一致。")]
    [SerializeField] private string brushId;
    [Tooltip("笔刷商店使用的轻量预览图；不参与实际线条渲染。")]
    [PreviewField(120f, ObjectFieldAlignment.Left)]
    [SerializeField] private Sprite previewSprite;

    [Header("线条")]
    [Tooltip("新建线条使用的共享材质；运行时不得直接修改该材质属性。")]
    [SerializeField] private Material lineMaterial;
    [Tooltip("新建线条沿 LineRenderer 长度方向使用的颜色与透明度渐变。")]
    [SerializeField] private Gradient colorGradient = CreateSolidGradient(Color.black);
    [Tooltip("新建线条的整体宽度倍率，不修改 LineRenderer 模板上的 widthCurve。")]
    [SerializeField, Min(MinimumLineWidth)] private float lineWidth = 0.15f;

    #endregion

    #region Public Read-Only API

    /// <summary>
    /// 笔刷唯一 ID，用于外部配置、存档 ID 与动态资源映射。
    /// </summary>
    public string BrushId => brushId;

    /// <summary>
    /// 笔刷商店预览图；不得作为实际线条 Cover 纹理使用。
    /// </summary>
    public Sprite PreviewSprite => previewSprite;

    /// <summary>
    /// 新建线条使用的共享材质；消费方只能赋给 sharedMaterial，不得修改材质属性。
    /// </summary>
    public Material LineMaterial => lineMaterial;

    /// <summary>
    /// 新建线条使用的颜色渐变；消费方不得修改该 Gradient 的 Key。
    /// </summary>
    public Gradient ColorGradient => colorGradient;

    /// <summary>
    /// 新建线条的整体宽度倍率，读取时保证不小于最小安全值。
    /// </summary>
    public float LineWidth => Mathf.Max(MinimumLineWidth, lineWidth);

    /// <summary>
    /// 校验本资产是否具备基础线条配置。
    /// 调用边界：供后续动态加载器或编辑器人工检查使用；不访问 DB、配置表或资源管理器。
    /// </summary>
    /// <param name="error">校验失败原因；成功时为空。</param>
    /// <returns>基础配置有效时返回 true。</returns>
    public bool TryValidate(out string error)
    {
        if (string.IsNullOrWhiteSpace(brushId))
        {
            error = "Brush ID is null or empty.";
            return false;
        }

        if (lineMaterial == null)
        {
            error = $"Brush '{brushId}' does not have a line material.";
            return false;
        }

        if (colorGradient == null)
        {
            error = $"Brush '{brushId}' does not have a color gradient.";
            return false;
        }

        if (lineWidth < MinimumLineWidth)
        {
            error = $"Brush '{brushId}' line width must be at least {MinimumLineWidth}.";
            return false;
        }

        error = null;
        return true;
    }

    #endregion

    #region Private Helpers

    private static Gradient CreateSolidGradient(Color color)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color, 1f),
            },
            new[]
            {
                new GradientAlphaKey(color.a, 0f),
                new GradientAlphaKey(color.a, 1f),
            });
        return gradient;
    }

    #endregion
}
