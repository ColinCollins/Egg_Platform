using UnityEngine;

/// <summary>Level 150 的池化导弹表现；移动、命中与回收计数由本关控制器统一驱动。</summary>
[DisallowMultipleComponent]
public sealed class Level150Missile : MonoBehaviour
{
    private SpriteRenderer body;
    private SpriteRenderer flame;
    private Sprite[] flameFrames;
    private float flameScale;
    private float nozzleY;
    private float age;
    private int launchFrame = -1;

    public bool IsFlying { get; private set; }
    public int Direction { get; private set; }
    /// <summary>只有玩家真实移动过所属预警的弹串，才有资格命中绳子或悬门。</summary>
    public bool CanHitDoorOrRope { get; private set; }

    public void Configure(SpriteRenderer bodyRenderer, Sprite[] frames)
    {
        body = bodyRenderer;
        flameFrames = frames;
        var flameObject = new GameObject("Level150_TailFlame");
        flameObject.transform.SetParent(transform, false);
        flame = flameObject.AddComponent<SpriteRenderer>();
        flame.sharedMaterial = body.sharedMaterial;
        flame.sortingLayerID = body.sortingLayerID;
        flame.sortingOrder = body.sortingOrder - 1;
        flame.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);

        float maxWidth = 0.01f;
        foreach (Sprite frame in frames)
            maxWidth = Mathf.Max(maxWidth, frame.bounds.size.x);
        // Double the previous 0.4 exhaust size without scaling the missile or its hit bounds.
        flameScale = body.sprite.bounds.size.x * 0.8f / maxWidth;
        nozzleY = body.sprite.bounds.max.y - 0.12f;
        flame.transform.localScale = Vector3.one * flameScale;
        ReturnToPool();
    }

    public void Launch(Vector3 position, int direction, bool canHitDoorOrRope)
    {
        Direction = direction;
        CanHitDoorOrRope = canHitDoorOrRope;
        age = 0f;
        launchFrame = Time.frameCount;
        transform.position = position;
        // 素材弹头默认朝下；根缩放始终使用人工摆放的 0.5，不靠缩放翻转。
        transform.localRotation = Quaternion.Euler(0f, 0f, direction > 0 ? 90f : -90f);
        IsFlying = true;
        gameObject.SetActive(true);
        RefreshFlame();
    }

    public void Advance(float deltaTime, float speed)
    {
        // This frame's delta predates a newly emitted missile. Do not apply it retroactively,
        // otherwise a frame hitch can collapse the spacing inside a timed salvo.
        if (launchFrame == Time.frameCount)
            return;
        transform.position += Vector3.right * (Direction * speed * deltaTime);
        age += deltaTime;
        RefreshFlame();
    }

    public Bounds BodyBounds
    {
        get
        {
            Bounds bounds = body.bounds;
            // 收紧素材透明边缘与侧翼；尾焰永远不进入命中范围。
            bounds.size = new Vector3(bounds.size.x * 0.85f, bounds.size.y * 0.8f, 1f);
            return bounds;
        }
    }

    public void ReturnToPool()
    {
        IsFlying = false;
        CanHitDoorOrRope = false;
        age = 0f;
        launchFrame = -1;
        gameObject.SetActive(false);
    }

    private void RefreshFlame()
    {
        Sprite frame = flameFrames[Mathf.FloorToInt(age / 0.09f) % flameFrames.Length];
        flame.sprite = frame;
        // 每帧按素材自身的上边缘对齐喷口，避免不同画布尺寸导致尾焰抖动。
        flame.transform.localPosition = new Vector3(0f, nozzleY + frame.bounds.max.y * flameScale, 0f);
    }
}
