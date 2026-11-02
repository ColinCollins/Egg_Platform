using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Sirenix.OdinInspector;

[RequireComponent(typeof(Graphic))]
public class PageTurnEffect : MonoBehaviour
{
    [Title("Settings")]
    [SerializeField] private float _duration = 1.0f;
    [SerializeField] private Ease _ease = Ease.InOutSine;
    
    [Header("Shader Params")]
    [SerializeField, Range(-180, 180)] private float _angle = 45f;
    [SerializeField, Range(0.01f, 1f)] private float _radius = 0.1f;
    [SerializeField, Range(0f, 1f)] private float _shadowStrength = 0.5f;
    [SerializeField] private Color _backColor = new Color(0.9f, 0.9f, 0.9f, 1f);

    private Graphic _graphic;
    private Material _material;
    private Tween _turnTween;

    private static readonly int AngleId = Shader.PropertyToID("_Angle");
    private static readonly int DistanceId = Shader.PropertyToID("_Distance");
    private static readonly int RadiusId = Shader.PropertyToID("_Radius");
    private static readonly int ShadowStrengthId = Shader.PropertyToID("_ShadowStrength");
    private static readonly int BackColorId = Shader.PropertyToID("_BackColor");

    private void Awake()
    {
        _graphic = GetComponent<Graphic>();
        EnsureMaterial();
    }

    private void OnEnable()
    {
        UpdateMaterialProperties();
    }

    private void EnsureMaterial()
    {
        if (_graphic == null) return;
        
        // Use existing material if set in inspector or graphic
        if (_material == null && _graphic.material != null && _graphic.material.shader.name == "UI/PageCurl")
        {
            _material = _graphic.material;
            return;
        }

        // Use a clone of the material or create a new one
        if (_material == null)
        {
            var shader = Shader.Find("UI/PageCurl");
            if (shader != null)
            {
                _material = new Material(shader);
                _graphic.material = _material;
            }
            else
            {
                Debug.LogError("[PageTurnEffect] Shader 'UI/PageCurl' not found!");
            }
        }
    }

    private void UpdateMaterialProperties()
    {
        if (_material == null) EnsureMaterial();
        if (_material == null) return;

        _material.SetFloat(AngleId, _angle);
        _material.SetFloat(RadiusId, _radius);
        _material.SetFloat(ShadowStrengthId, _shadowStrength);
        _material.SetColor(BackColorId, _backColor);
    }
    
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            UpdateMaterialProperties();
        }
    }
#endif

    [Button("Open Page (Hide Cover)")]
    public void OpenPage()
    {
        TurnPage(1.5f, -1.0f);
    }

    [Button("Close Page (Show Cover)")]
    public void ClosePage()
    {
        TurnPage(-1.0f, 1.5f);
    }

    /// <summary>
    /// Animates the page turn.
    /// fromDist/toDist: typically range from -0.5 to 1.5 (depending on Angle)
    /// </summary>
    public void TurnPage(float fromDist, float toDist, System.Action onComplete = null)
    {
        EnsureMaterial();
        if (_material == null) return;

        _turnTween?.Kill();
        
        // Set initial
        _material.SetFloat(DistanceId, fromDist);
        
        _turnTween = DOVirtual.Float(fromDist, toDist, _duration, (val) =>
        {
            _material.SetFloat(DistanceId, val);
        }).SetEase(_ease).OnComplete(() => onComplete?.Invoke());
    }

    public void SetProgress(float progress)
    {
        EnsureMaterial();
        if (_material == null) return;
        
        // Map 0-1 to reasonable distance range
        // For 45 degrees, diagonal of unit square is sqrt(2) ~ 1.414
        // Range -0.5 to 1.5 covers it well.
        float dist = Mathf.Lerp(-0.5f, 1.5f, progress);
        _material.SetFloat(DistanceId, dist);
    }
    
    private void OnDestroy()
    {
        _turnTween?.Kill();
        /* if (_material != null)
        {
            Destroy(_material);
            _material = null;
        } */
    }
}
