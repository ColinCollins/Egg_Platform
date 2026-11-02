using UnityEngine;

public class SmoothColorByHeight : MonoBehaviour
{
    private static readonly int StartHeightId = Shader.PropertyToID("_StartHeight");
    private static readonly int EndHeightId = Shader.PropertyToID("_EndHeight");
    private static readonly int StartColorId = Shader.PropertyToID("_StartColor");
    private static readonly int EndColorId = Shader.PropertyToID("_EndColor");

    [Header("目标设置")]
    [SerializeField] private Transform target;

    [Header("高度范围")]
    [SerializeField] private float startHeight = 0f;
    [SerializeField] private float endHeight = 10f;

    [Header("颜色设置")]
    [SerializeField] private Color startColor = Color.white;
    [SerializeField] private Color endColor = Color.black;

    [Header("2D Sprite 与材质")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Tooltip("使用 CommonBgColorChange (2D Sprite) 的 Material；为空则使用上方 SpriteRenderer 的 material")]
    [SerializeField] private Material gradientMaterial;

    private Material runtimeMaterial;

    void Start()
    {
        if (target == null)
        {
            Debug.LogWarning("[SmoothColorByHeight] Target is null");
            enabled = false;
            return;
        }

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (gradientMaterial != null && gradientMaterial.HasProperty(StartHeightId))
            runtimeMaterial = gradientMaterial;

        spriteRenderer.material = runtimeMaterial;

        if (runtimeMaterial == null)
        {
            Debug.LogWarning("[SmoothColorByHeight] No valid 2D Sprite Material (CommonBgColorChange). Assign material to SpriteRenderer or Gradient Material.");
            enabled = false;
        }
    }

    void Update()
    {
        if (target == null || runtimeMaterial == null)
            return;

        runtimeMaterial.SetFloat(StartHeightId, startHeight);
        runtimeMaterial.SetFloat(EndHeightId, endHeight);
        runtimeMaterial.SetColor(StartColorId, startColor);
        runtimeMaterial.SetColor(EndColorId, endColor);
    }
}
