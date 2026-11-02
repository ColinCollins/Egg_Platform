using Game.Scripts.Common;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Level 127 单块可击碎砖块：两击状态机（完整 → 裂纹 → 击碎）。
/// 点击路径由 Level127Ctrl 统一做屏幕→世界判定后调用 TapHit；
/// actor 跳跃路径在本组件 OnCollisionEnter2D 计击，两条路径共用同一状态机、特效与音效。
/// 隐藏物品的分配与掉落由 Level127Ctrl 负责，本组件只上报击碎事件。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level127Brick : MonoBehaviour
{
    [Header("Sprites")]
    [Tooltip("完整砖贴图（Sp_l127_st_01）。")]
    [SerializeField] private Sprite intactSprite;

    [Tooltip("第一击后的裂纹贴图（Sp_l127_st_02）。")]
    [SerializeField] private Sprite crackedSprite;

    [Tooltip("第一击弹出的小碎片贴图（Sp_l127_st_04）。")]
    [SerializeField] private Sprite shardSprite;

    [Header("First Hit Feedback")]
    [Tooltip("第一击从砖块边缘弹出的碎片数量。")]
    [SerializeField, Min(1)] private int firstHitShardCount = 4;

    [Tooltip("裂纹切换时的 shake 强度（世界单位）。")]
    [SerializeField, Min(0f)] private float shakeStrength = 0.06f;

    [Tooltip("shake 单程时长（秒）。")]
    [SerializeField, Min(0.01f)] private float shakeDuration = 0.18f;

    [Tooltip("碎片弹出的最大水平距离（世界单位）。")]
    [SerializeField, Min(0f)] private float shardSpreadX = 0.7f;

    [Tooltip("碎片抛物线峰值高度（世界单位）。")]
    [SerializeField, Min(0f)] private float shardPeakHeight = 0.55f;

    [Tooltip("碎片落地深度（相对起点向下，世界单位）。")]
    [SerializeField, Min(0f)] private float shardFallDistance = 1.5f;

    [Tooltip("碎片飞行时长区间（秒）。")]
    [SerializeField] private Vector2 shardDurationRange = new(0.45f, 0.7f);

    [Header("Jump Hit")]
    [Tooltip("actor 跳跃碰撞计击的最小间隔（秒），防止一次跳跃重复计击。")]
    [SerializeField, Min(0f)] private float jumpHitCooldownSeconds = 0.5f;

    [Header("Sfx")]
    [Tooltip("第一击（敲出裂纹）音效 tag。")]
    [SerializeField] private string crackedSfxTag = "crashBroke";

    [Tooltip("第二击（击碎）音效 tag。")]
    [SerializeField] private string smashedSfxTag = "crashBroke";

    private const int ShardSortingOrder = 91;

    private SpriteRenderer spriteRenderer;
    private Collider2D bodyCollider;
    private Level127Ctrl owner;
    private Vector3 baseLocalPosition;
    private int hitCount;
    private float lastJumpHitTime = float.NegativeInfinity;
    private bool smashed;
    private Tween shakeTween;

    /// <summary>砖块是否已被击碎。</summary>
    public bool IsSmashed => smashed;

    /// <summary>砖块世界中心（击碎判定与特效锚点）。</summary>
    public Vector3 Center => spriteRenderer != null ? spriteRenderer.bounds.center : transform.position;

    /// <summary>钥匙合成排队后关闭实体碰撞；砖块保留为后续闪白与收缩演出的纯视觉对象。</summary>
    public void PrepareForSynthesis()
    {
        if (smashed)
            return;

        shakeTween?.Kill();
        transform.localPosition = baseLocalPosition;
        if (bodyCollider != null)
            bodyCollider.enabled = false;
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        bodyCollider = GetComponent<Collider2D>();
        baseLocalPosition = transform.localPosition;

        if (spriteRenderer != null && intactSprite != null)
            spriteRenderer.sprite = intactSprite;
    }

    private void OnEnable()
    {
        if (owner == null)
            owner = GetComponentInParent<Level127Ctrl>(true);

        owner?.RegisterBrick(this);
    }

    private void OnDestroy()
    {
        shakeTween?.Kill();
        owner?.UnregisterBrick(this);
    }

    /// <summary>
    /// 点击路径击打入口；由 Level127Ctrl 在世界点命中时调用。
    /// 砖块已碎、交互锁定或未初始化时返回 false 表示未消费该次点击。
    /// </summary>
    public bool TapHit()
    {
        if (smashed || owner == null || owner.InteractionLocked)
            return false;

        RegisterHit();
        return true;
    }

    /// <summary>点击路径命中检测；世界点落在砖块碰撞体内返回 true。</summary>
    public bool ContainsWorldPoint(Vector2 worldPosition)
    {
        return !smashed && bodyCollider != null && bodyCollider.OverlapPoint(worldPosition);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (smashed || owner == null || owner.InteractionLocked)
            return;

        if (!collision.collider.CompareTag("Player"))
            return;

        if (Time.time - lastJumpHitTime < jumpHitCooldownSeconds)
            return;

        lastJumpHitTime = Time.time;
        RegisterHit();
    }

    private void RegisterHit()
    {
        hitCount++;

        if (hitCount == 1)
        {
            ApplyCracked();
            return;
        }

        ApplySmashed();
    }

    private void ApplyCracked()
    {
        if (spriteRenderer != null && crackedSprite != null)
            spriteRenderer.sprite = crackedSprite;

        PlayShake();
        SpawnFirstHitShards();
        AudioManager.PlaySound(crackedSfxTag);
    }

    private void ApplySmashed()
    {
        smashed = true;
        AudioManager.PlaySound(smashedSfxTag);
        // 先上报再隐藏：owner 需要砖块位置生成击碎特效与掉落物；
        // 立即停用自身以同步移除碰撞体与贴图。
        owner?.NotifyBrickSmashed(this);
        gameObject.SetActive(false);
    }

    private void PlayShake()
    {
        shakeTween?.Kill();
        transform.localPosition = baseLocalPosition;
        shakeTween = transform.DOShakePosition(shakeDuration, shakeStrength)
            .OnComplete(() =>
            {
                if (this != null)
                    transform.localPosition = baseLocalPosition;
            });
    }

    private void SpawnFirstHitShards()
    {
        if (shardSprite == null || owner == null)
            return;

        Vector3 center = Center;
        Vector3 halfSize = spriteRenderer != null
            ? spriteRenderer.bounds.extents
            : new Vector3(0.6f, 0.55f, 0f);

        for (int index = 0; index < firstHitShardCount; index++)
        {
            SpawnShard(center, halfSize);
        }
    }

    private void SpawnShard(Vector3 center, Vector3 halfSize)
    {
        // 碎片从砖块边缘随机角度（四边均匀采样）弹出，抛物线落下后淡出销毁。
        float shardDuration = Random.Range(shardDurationRange.x, shardDurationRange.y);
        int edge = Random.Range(0, 4);
        Vector3 spawnPosition = edge switch
        {
            0 => center + new Vector3(Random.Range(-halfSize.x, halfSize.x), halfSize.y, 0f),
            1 => center + new Vector3(Random.Range(-halfSize.x, halfSize.x), -halfSize.y, 0f),
            2 => center + new Vector3(halfSize.x, Random.Range(-halfSize.y, halfSize.y), 0f),
            _ => center + new Vector3(-halfSize.x, Random.Range(-halfSize.y, halfSize.y), 0f),
        };

        float directionX = Mathf.Sign(spawnPosition.x - center.x + Random.Range(-0.3f, 0.3f));
        float peakHeight = Random.Range(shardPeakHeight * 0.6f, shardPeakHeight);
        float fallDistance = Random.Range(shardFallDistance * 0.7f, shardFallDistance);
        float spreadX = Random.Range(shardSpreadX * 0.4f, shardSpreadX) * directionX;
        float startRotation = Random.Range(0f, 360f);
        float spinRotation = startRotation + Random.Range(-240f, 240f);

        Transform shardParent = owner.EffectRoot;
        GameObject shard = new($"Level127Shard_{name}");
        shard.transform.SetParent(shardParent != null ? shardParent : transform.parent, false);
        shard.transform.position = spawnPosition;
        shard.layer = gameObject.layer;

        SpriteRenderer shardRenderer = shard.AddComponent<SpriteRenderer>();
        shardRenderer.sprite = shardSprite;
        shardRenderer.sortingLayerID = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
        shardRenderer.sortingOrder = ShardSortingOrder;

        float progress = 0f;
        Vector3 startPosition = spawnPosition;
        Tween shardTween = DOTween.To(() => progress, value => progress = value, 1f, shardDuration)
            .SetEase(Ease.Linear)
            .OnUpdate(() =>
            {
                if (shard == null)
                    return;

                float arc = 4f * peakHeight * progress * (1f - progress);
                shard.transform.position = new Vector3(
                    startPosition.x + spreadX * progress,
                    startPosition.y + arc - fallDistance * progress * progress,
                    startPosition.z);
                shard.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(startRotation, spinRotation, progress));
            })
            .OnComplete(() =>
            {
                if (shard == null)
                    return;

                shardRenderer.DOFade(0f, 0.15f).OnComplete(() =>
                {
                    if (shard != null)
                        Destroy(shard);
                });
            });

        shardTween.Play();
    }
}
