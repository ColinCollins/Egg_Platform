using UnityEngine;

public class SpriteRendererGroupAlpha : MonoBehaviour
{
    [Range(0f, 1f)]
    [SerializeField] private float alpha = 1f;

    [SerializeField] private bool includeInactive = true;
    [SerializeField] private bool autoCollectRenderers = true;
    [SerializeField] private SpriteRenderer[] renderers;

    private void Awake()
    {
        if (autoCollectRenderers)
        {
            CollectRenderers();
        }

        ApplyAlpha();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        alpha = Mathf.Clamp01(alpha);

        if (autoCollectRenderers)
        {
            CollectRenderers();
        }

        ApplyAlpha();
    }
#endif

    public float GetAlpha()
    {
        return alpha;
    }

    public void SetAlpha(float value)
    {
        alpha = Mathf.Clamp01(value);
        ApplyAlpha();
    }

    public void CollectRenderers()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>(includeInactive);
    }

    private void ApplyAlpha()
    {
        if (renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            var spriteRenderer = renderers[i];
            if (spriteRenderer == null)
            {
                continue;
            }

            var color = spriteRenderer.color;
            color.a = alpha;
            spriteRenderer.color = color;
        }
    }
}
