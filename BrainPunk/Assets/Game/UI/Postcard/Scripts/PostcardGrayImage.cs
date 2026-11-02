using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 控制明信片页 Image 在原材质和横向灰度填充材质之间切换，并把灰度填充参数写入独立材质实例。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Postcard/Postcard Gray Image")]
public class PostcardGrayImage : MonoBehaviour
{
    private const string DefaultShaderName = "UI/PostcardPageGrayFill";
    private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
    private static readonly int GrayAmountId = Shader.PropertyToID("_GrayAmount");
    private static readonly int FillUVRectId = Shader.PropertyToID("_FillUVRect");

    /// <summary>
    /// 用途：需要被切换材质并写入灰度参数的目标 Graphic，通常是 Postcard page Image。
    /// 调用边界：优先由 prefab/Inspector 显式绑定；为空时运行时回退到当前对象上的 Graphic。
    /// 注意事项：组件只修改该 Graphic 的 material，不递归处理子节点。
    /// </summary>
    [SerializeField] private Graphic targetGraphic;

    /// <summary>
    /// 用途：横向灰度填充材质模板，建议绑定 PostcardPageGrayFill.mat。
    /// 调用边界：运行时会复制为当前组件私有材质实例后再写入参数。
    /// 注意事项：为空时会通过 Shader.Find("UI/PostcardPageGrayFill") 创建运行时材质；不会修改共享材质资产。
    /// </summary>
    [SerializeField] private Material grayMaterial;

    /// <summary>
    /// 用途：传给 shader 的 _FillAmount，控制按 uv.x 从左到右灰化的比例。
    /// 调用边界：可由 Inspector 提供初始值，运行时推荐通过 SetFillAmount(float) 更新。
    /// 注意事项：SetFillAmount(0) 也会补上灰度材质并写入 0；需要恢复原材质时调用 SetGray(false)。
    /// </summary>
    [SerializeField, Range(0f, 1f)] private float fillAmount;

    /// <summary>
    /// 用途：传给 shader 的 _GrayAmount，控制进入填充区域后的灰度强度。
    /// 调用边界：运行时推荐通过 SetGrayAmount(float) 更新。
    /// 注意事项：该值只影响灰度材质实例，不影响原 Image 材质。
    /// </summary>
    [SerializeField, Range(0f, 1f)] private float grayAmount = 1f;

    /// <summary>
    /// 用途：控制是否主动使用灰度材质。
    /// 调用边界：运行时推荐通过 SetGray(bool) 或 SetFillAmount(float) 更新。
    /// 注意事项：关闭时会把 fillAmount 收敛到 0 并恢复原材质。
    /// </summary>
    [SerializeField] private bool isGray;

    private Graphic _appliedGraphic;
    private Material _originalMaterial;
    private bool _hasOriginalMaterial;
    private Material _runtimeMaterial;
    private Material _runtimeMaterialSource;
    private static readonly Vector4 DefaultFillUVRect = new Vector4(0f, 0f, 1f, 1f);

    #region Properties

    /// <summary>
    /// 用途：读取当前灰度填充比例。
    /// 调用边界：供运行时 UI 或动画逻辑查询当前状态。
    /// 注意事项：写入请使用 SetFillAmount(float)，避免绕过材质刷新。
    /// </summary>
    public float FillAmount => fillAmount;

    /// <summary>
    /// 用途：读取当前灰度强度。
    /// 调用边界：供运行时 UI 或动画逻辑查询当前状态。
    /// 注意事项：写入请使用 SetGrayAmount(float)，避免绕过材质刷新。
    /// </summary>
    public float GrayAmount => grayAmount;

    /// <summary>
    /// 用途：读取当前是否处于灰度材质状态。
    /// 调用边界：供运行时 UI 或动画逻辑查询当前状态。
    /// 注意事项：写入请使用 SetGray(bool) 或 SetFillAmount(float)。
    /// </summary>
    public bool IsGray => isGray;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ResolveTargetGraphic();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
            return;

        ApplyMaterialState();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ClampParameters();

        if (Application.isPlaying)
            ApplyMaterialState();
    }
#endif

    private void OnDidApplyAnimationProperties()
    {
        ApplyMaterialState();
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
            return;

        RestoreOriginalMaterial();
    }

    private void OnDestroy()
    {
        RestoreOriginalMaterial();
        DestroyRuntimeMaterial();
    }

    #endregion

    #region Public API

    /// <summary>
    /// 用途：显式切换目标 Graphic 是否使用明信片横向灰度材质。
    /// 调用边界：供 Postcard 页面逻辑、动画回调或 UnityEvent 调用。
    /// 注意事项：传入 false 会把 fillAmount 置为 0 并恢复原材质；传入 true 不会自动改变 fillAmount。
    /// </summary>
    /// <param name="value">是否启用灰度材质。</param>
    public void SetGray(bool value)
    {
        isGray = value;
        if (!isGray)
            fillAmount = 0f;

        ApplyMaterialState();
    }

    /// <summary>
    /// 用途：设置横向灰度填充比例，并按比例自动切换目标 Graphic 材质。
    /// 调用边界：供 DOTween、动画事件或页面逻辑在运行时驱动。
    /// 注意事项：参数会限制到 0..1；即使传入 0 也会启用灰度材质并写入 _FillAmount，便于从 0 开始播放过渡。
    /// </summary>
    /// <param name="value">灰度填充比例。</param>
    public void SetFillAmount(float value)
    {
        fillAmount = Mathf.Clamp01(value);
        isGray = true;
        ApplyMaterialState();
    }

    /// <summary>
    /// 用途：设置横向灰度填充区域内的灰度强度。
    /// 调用边界：供运行时表现按需要调整灰度强弱。
    /// 注意事项：参数会限制到 0..1；目标未处于灰度材质状态时只缓存值，后续启用时再写入材质。
    /// </summary>
    /// <param name="value">灰度强度。</param>
    public void SetGrayAmount(float value)
    {
        grayAmount = Mathf.Clamp01(value);
        ApplyMaterialProperties(_runtimeMaterial);
    }

    /// <summary>
    /// 用途：重新解析目标 Graphic、材质状态和 shader 参数。
    /// 调用边界：目标材质模板或目标 Graphic 在运行时被外部修改后调用。
    /// 注意事项：只处理当前绑定对象，不读取存档、不加载资源地址、不触发全局事件。
    /// </summary>
    public void Refresh()
    {
        ApplyMaterialState();
    }

    #endregion

    #region Material Control

    private void ApplyMaterialState()
    {
        if (!Application.isPlaying || !isActiveAndEnabled)
            return;

        ClampParameters();

        if (!ShouldUseGrayMaterial())
        {
            RestoreOriginalMaterial();
            return;
        }

        Graphic graphic = ResolveTargetGraphic();
        if (graphic == null)
        {
            RestoreOriginalMaterial();
            return;
        }

        if (_appliedGraphic != null && _appliedGraphic != graphic)
            RestoreOriginalMaterial();

        Material material = ResolveRuntimeMaterial();
        if (material == null)
        {
            RestoreOriginalMaterial();
            return;
        }

        ApplyMaterialProperties(material);

        if (!_hasOriginalMaterial)
        {
            _originalMaterial = graphic.material;
            _hasOriginalMaterial = true;
            _appliedGraphic = graphic;
        }

        if (graphic.material != material)
            graphic.material = material;
    }

    private bool ShouldUseGrayMaterial()
    {
        return isGray || fillAmount > 0f;
    }

    private Graphic ResolveTargetGraphic()
    {
        if (targetGraphic == null)
            targetGraphic = GetComponent<Graphic>();

        return targetGraphic;
    }

    private Material ResolveRuntimeMaterial()
    {
        if (_runtimeMaterial != null && _runtimeMaterialSource == grayMaterial)
            return _runtimeMaterial;

        DestroyRuntimeMaterial();

        if (grayMaterial != null)
        {
            _runtimeMaterial = new Material(grayMaterial)
            {
                name = $"{grayMaterial.name}_PostcardGrayImageRuntime",
                hideFlags = HideFlags.HideAndDontSave
            };
            _runtimeMaterialSource = grayMaterial;
            return _runtimeMaterial;
        }

        Shader shader = Shader.Find(DefaultShaderName);
        if (shader == null)
            return null;

        _runtimeMaterial = new Material(shader)
        {
            name = "PostcardGrayImage_DefaultRuntimeMaterial",
            hideFlags = HideFlags.HideAndDontSave
        };
        _runtimeMaterialSource = null;
        return _runtimeMaterial;
    }

    private void ApplyMaterialProperties(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty(FillAmountId))
            material.SetFloat(FillAmountId, fillAmount);

        if (material.HasProperty(GrayAmountId))
            material.SetFloat(GrayAmountId, grayAmount);

        if (material.HasProperty(FillUVRectId))
            material.SetVector(FillUVRectId, ResolveFillUVRect());
    }

    private Vector4 ResolveFillUVRect()
    {
        Graphic graphic = ResolveTargetGraphic();
        if (!(graphic is Image image))
            return DefaultFillUVRect;

        Sprite sprite = image.overrideSprite != null ? image.overrideSprite : image.sprite;
        if (sprite == null || sprite.uv == null || sprite.uv.Length <= 0)
            return DefaultFillUVRect;

        Vector2[] uvs = sprite.uv;
        float minU = uvs[0].x;
        float maxU = uvs[0].x;
        float minV = uvs[0].y;
        float maxV = uvs[0].y;

        for (int i = 1; i < uvs.Length; i++)
        {
            Vector2 uv = uvs[i];
            minU = Mathf.Min(minU, uv.x);
            maxU = Mathf.Max(maxU, uv.x);
            minV = Mathf.Min(minV, uv.y);
            maxV = Mathf.Max(maxV, uv.y);
        }

        return new Vector4(
            minU,
            minV,
            Mathf.Max(maxU - minU, 0.0001f),
            Mathf.Max(maxV - minV, 0.0001f));
    }

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

    private void DestroyRuntimeMaterial()
    {
        if (_runtimeMaterial == null)
            return;

        if (Application.isPlaying)
            Destroy(_runtimeMaterial);
        else
            DestroyImmediate(_runtimeMaterial);

        _runtimeMaterial = null;
        _runtimeMaterialSource = null;
    }

    private void ClampParameters()
    {
        fillAmount = Mathf.Clamp01(fillAmount);
        grayAmount = Mathf.Clamp01(grayAmount);
    }

    #endregion
}
