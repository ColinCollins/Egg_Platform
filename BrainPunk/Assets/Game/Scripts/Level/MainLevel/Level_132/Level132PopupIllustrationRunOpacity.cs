using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the opacity of one Level 132 popup illustration independently.
/// </summary>
[RequireComponent(typeof(Image))]
public sealed class Level132PopupIllustrationRunOpacity : MonoBehaviour
{
    [Header("示意图透明度")]
    [Tooltip("仅控制当前 Level132PopupIllustrationRun 图片的透明度。")]
    [SerializeField, Range(0f, 1f)] private float opacity = 1f;

    private void Awake()
    {
        ApplyOpacity();
    }

    private void OnEnable()
    {
        ApplyOpacity();
    }

    private void OnValidate()
    {
        ApplyOpacity();
    }

    private void ApplyOpacity()
    {
        Image image = GetComponent<Image>();
        if (image == null)
            return;

        Color color = image.color;
        color.a = Mathf.Clamp01(opacity);
        image.color = color;
    }
}
