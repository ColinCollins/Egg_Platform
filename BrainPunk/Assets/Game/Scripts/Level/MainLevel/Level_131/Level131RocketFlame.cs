using UnityEngine;

/// <summary>方块火箭专用三帧图集；统一画布与喷口 pivot，不参与背包 Spine。</summary>
[DisallowMultipleComponent]
public sealed class Level131RocketFlame : MonoBehaviour
{
    private const int FrameSize = 192;
    private const int FrameCount = 3;
    private SpriteRenderer spriteRenderer;
    private Sprite[] frames;
    private Vector3 baseScale;
    private float frameTimer;
    private int frameIndex;
    private float speedFactor;
    private bool isPropelling;

    public void Configure(Texture2D atlas, SpriteRenderer rocketRenderer)
    {
        ReleaseFrames();
        spriteRenderer = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
        if (spriteRenderer == null || rocketRenderer == null || atlas == null ||
            atlas.width != FrameSize * FrameCount || atlas.height != FrameSize)
        {
            Debug.LogError("Level 131 方块尾焰缺少 Renderer/火箭，或图集不是 576×192。", this);
            gameObject.SetActive(false);
            return;
        }
        spriteRenderer.sharedMaterial = rocketRenderer.sharedMaterial;
        spriteRenderer.sortingLayerID = rocketRenderer.sortingLayerID;
        spriteRenderer.sortingOrder = rocketRenderer.sortingOrder - 1;
        spriteRenderer.color = Color.white;
        frames = new Sprite[FrameCount];
        for (int i = 0; i < FrameCount; i++)
        {
            frames[i] = Sprite.Create(atlas, new Rect(i * FrameSize, 0, FrameSize, FrameSize),
                new Vector2(0.5f, 184f / FrameSize), 100f, 0, SpriteMeshType.FullRect);
            frames[i].name = "Level131PlatformFlame_" + i;
        }
        frameTimer = 0f;
        frameIndex = 0;
        ApplyFrame();
        gameObject.SetActive(false);
    }

    public void SetPropulsion(bool enabled, float normalizedSpeed)
    {
        bool starting = enabled && !isPropelling;
        isPropelling = enabled && frames != null;
        speedFactor = Mathf.Clamp01(normalizedSpeed);
        if (starting)
        {
            frameTimer = 0f;
            frameIndex = 0;
        }
        if (spriteRenderer != null)
            spriteRenderer.enabled = isPropelling;
        ApplyFrame();
        gameObject.SetActive(isPropelling);
    }

    private void Update()
    {
        if (!isPropelling || frames == null)
            return;
        float interval = Mathf.Lerp(0.14f, 0.035f, speedFactor);
        frameTimer += Time.deltaTime;
        int steps = Mathf.FloorToInt(frameTimer / interval);
        if (steps == 0)
            return;
        frameTimer -= steps * interval;
        frameIndex = (frameIndex + steps) % FrameCount;
        ApplyFrame();
    }

    private void ApplyFrame()
    {
        if (spriteRenderer == null || frames == null)
            return;
        spriteRenderer.sprite = frames[frameIndex];
        transform.localScale = baseScale * Mathf.Lerp(1f, 1.4f, speedFactor);
    }

    private void ReleaseFrames()
    {
        if (frames == null)
            return;
        foreach (Sprite frame in frames)
            if (frame != null)
                Destroy(frame);
        frames = null;
    }

    private void OnDestroy()
    {
        ReleaseFrames();
    }
}
