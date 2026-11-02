using System.Collections.Generic;
using DG.Tweening;
using Game.Scripts.Common;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Level 130 platform that only breaks when the rising spike row reaches it
/// from below, then releases the six authored platform fragments as visual debris.
/// </summary>
[DisallowMultipleComponent]
public sealed class Level130BreakablePlatform : MonoBehaviour
{
    public enum PlatformRole
    {
        Movable,
        DoorSupport
    }

    [SerializeField] private PlatformRole role;
    [SerializeField] private Level130Ctrl levelCtrl;
    [SerializeField] private Level130HorizontalPlatformDrag dragController;
    [SerializeField, Min(0f)] private float undersideCenterTolerance = 0.05f;

    [Header("Break Presentation")]
    [SerializeField] private string breakSfxTag = "crashBroke";
    [Tooltip("参考 Level 127 的图集碎裂粒子 Prefab；为空时使用 breakSprites 降级路径。")]
    [SerializeField] private ParticleSystem fragmentVfxPrefab;
    [SerializeField, Min(0.01f)] private float fragmentVfxScale = 1f;
    [SerializeField, Min(1)] private int fragmentVfxBurstCount = 6;
    [SerializeField, Min(0.01f)] private float fragmentVfxDestroyDelay = 3f;
    [Tooltip("图集粒子不可用时的源 Sprite 降级列表。")]
    [SerializeField] private Sprite[] breakSprites;
    [SerializeField, Min(0.01f)] private float fragmentDuration = 0.65f;
    [SerializeField, Min(0f)] private float fragmentJumpPower = 0.55f;
    [SerializeField, Min(0f)] private float fragmentHorizontalSpread = 1.25f;
    [SerializeField, Min(0f)] private float fragmentFallDistance = 1.4f;
    [SerializeField, Min(0.01f)] private float fragmentFadeDuration = 0.15f;
    [SerializeField, Min(0.01f)] private float fragmentScale = 1f;

    private Collider2D[] platformColliders;
    private Renderer[] platformRenderers;
    private readonly List<GameObject> activeFragments = new List<GameObject>();

    public PlatformRole Role => role;
    public bool IsBroken { get; private set; }

    private void Awake()
    {
        ResolveReferences();
        CachePresentation();
    }

    private void OnDestroy()
    {
        for (int i = activeFragments.Count - 1; i >= 0; i--)
        {
            GameObject fragment = activeFragments[i];
            if (fragment == null)
                continue;

            fragment.transform.DOKill();
            ParticleSystem particleSystem = fragment.GetComponent<ParticleSystem>();
            if (particleSystem != null)
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Destroy(fragment);
        }

        activeFragments.Clear();
    }

    public bool TryBreakFromSpike(Level130SpikeRow spikeRow, Collider2D hitCollider)
    {
        if (IsBroken || spikeRow == null || hitCollider == null || !spikeRow.IsAscending)
            return false;

        CachePresentation();
        if (!IsOwnedPlatformCollider(hitCollider))
            return false;

        Bounds platformBounds = hitCollider.bounds;
        if (spikeRow.CenterWorldY >= platformBounds.center.y - Mathf.Max(0f, undersideCenterTolerance))
            return false;

        BreakPlatform();
        return true;
    }

    private bool IsOwnedPlatformCollider(Collider2D candidate)
    {
        if (candidate == null || platformColliders == null)
            return false;

        for (int i = 0; i < platformColliders.Length; i++)
        {
            if (platformColliders[i] == candidate)
                return true;
        }

        return false;
    }

    private void BreakPlatform()
    {
        if (IsBroken)
            return;

        IsBroken = true;
        ResolveReferences();
        CachePresentation();

        if (!string.IsNullOrWhiteSpace(breakSfxTag))
            AudioManager.PlaySound(breakSfxTag);

        dragController?.PrepareForPlatformBreak();
        PlayBreakPresentation();

        for (int i = 0; i < platformColliders.Length; i++)
        {
            if (platformColliders[i] != null)
                platformColliders[i].enabled = false;
        }

        for (int i = 0; i < platformRenderers.Length; i++)
        {
            if (platformRenderers[i] != null)
                platformRenderers[i].enabled = false;
        }

        levelCtrl?.NotifyPlatformBroken(this);
    }

    private void PlayBreakPresentation()
    {
        if (fragmentVfxPrefab != null && PlayFragmentVfx())
        {
            return;
        }

        PlaySpriteBreakFallback();
    }

    private bool PlayFragmentVfx()
    {
        Bounds platformBounds = ResolvePlatformBounds();
        Vector3 effectPosition = new Vector3(
            platformBounds.center.x,
            platformBounds.center.y,
            transform.position.z);
        ParticleSystem instance = Instantiate(
            fragmentVfxPrefab,
            effectPosition,
            fragmentVfxPrefab.transform.rotation,
            transform.parent);

        instance.transform.localScale = Vector3.one * Mathf.Max(0.01f, fragmentVfxScale);
        instance.gameObject.SetActive(false);

        ResolveBreakSorting(out int sortingLayerId, out int sortingOrder);

        ParticleSystemRenderer[] particleRenderers =
            instance.GetComponentsInChildren<ParticleSystemRenderer>(true);
        bool hasRenderableRenderer = false;
        for (int i = 0; i < particleRenderers.Length; i++)
        {
            ParticleSystemRenderer particleRenderer = particleRenderers[i];
            if (particleRenderer == null)
                continue;

            if (particleRenderer.sharedMaterial == null ||
                particleRenderer.sharedMaterial.mainTexture == null)
            {
                continue;
            }

            hasRenderableRenderer = true;
            particleRenderer.sortingLayerID = sortingLayerId;
            particleRenderer.sortingOrder = sortingOrder + 1;
        }

        if (!hasRenderableRenderer)
        {
            Destroy(instance.gameObject);
            return false;
        }

        ParticleSystem.EmissionModule emission = instance.emission;
        if (emission.burstCount > 0)
        {
            ParticleSystem.Burst burst = emission.GetBurst(0);
            burst.count = new ParticleSystem.MinMaxCurve(Mathf.Max(1, fragmentVfxBurstCount));
            emission.SetBurst(0, burst);
        }

        activeFragments.Add(instance.gameObject);
        instance.gameObject.SetActive(true);
        instance.Play(true);
        Destroy(instance.gameObject, Mathf.Max(0.01f, fragmentVfxDestroyDelay));
        return true;
    }

    private void PlaySpriteBreakFallback()
    {
        if (breakSprites == null || breakSprites.Length == 0)
            return;

        Bounds platformBounds = ResolvePlatformBounds();
        SpriteRenderer sourceRenderer = ResolveSourceRenderer();
        ResolveBreakSorting(out int sortingLayerId, out int sortingOrder);
        Transform fragmentParent = transform.parent;
        float duration = Mathf.Max(0.01f, fragmentDuration);
        float fadeDuration = Mathf.Min(duration, Mathf.Max(0.01f, fragmentFadeDuration));

        for (int i = 0; i < breakSprites.Length; i++)
        {
            Sprite sprite = breakSprites[i];
            if (sprite == null)
                continue;

            float normalizedIndex = breakSprites.Length <= 1
                ? 0.5f
                : i / (float)(breakSprites.Length - 1);
            float horizontalDirection = normalizedIndex * 2f - 1f;
            float alternatingY = (i % 2 == 0 ? 1f : -1f) * 0.04f;
            float appliedScale = Mathf.Max(0.01f, fragmentScale);
            float halfSpriteWidth = sprite.bounds.extents.x * appliedScale;
            float startMinX = platformBounds.min.x + halfSpriteWidth;
            float startMaxX = platformBounds.max.x - halfSpriteWidth;
            float startX = startMinX <= startMaxX
                ? Mathf.Lerp(startMinX, startMaxX, normalizedIndex)
                : platformBounds.center.x;
            Vector3 startPosition = new Vector3(
                startX,
                platformBounds.center.y + alternatingY,
                transform.position.z);

            GameObject fragment = new GameObject($"Level130PlatformFragment_{i + 1}");
            fragment.layer = gameObject.layer;
            fragment.transform.SetParent(fragmentParent, true);
            fragment.transform.position = startPosition;
            fragment.transform.localScale = Vector3.one * appliedScale;

            SpriteRenderer renderer = fragment.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            if (sourceRenderer != null)
            {
                renderer.sharedMaterial = sourceRenderer.sharedMaterial;
                renderer.sortingLayerID = sortingLayerId;
                renderer.sortingOrder = sortingOrder + 1;
                renderer.color = sourceRenderer.color;
                renderer.maskInteraction = sourceRenderer.maskInteraction;
            }

            activeFragments.Add(fragment);

            Vector3 endPosition = startPosition + new Vector3(
                horizontalDirection * Mathf.Max(0f, fragmentHorizontalSpread),
                -Mathf.Max(0f, fragmentFallDistance),
                0f);
            float rotation = horizontalDirection * 150f + (i % 2 == 0 ? -35f : 35f);
            float fadeAt = Mathf.Max(0f, duration - fadeDuration);

            Sequence sequence = DOTween.Sequence().SetLink(gameObject);
            sequence.Join(fragment.transform
                .DOJump(endPosition, Mathf.Max(0f, fragmentJumpPower), 1, duration)
                .SetEase(Ease.Linear));
            sequence.Join(fragment.transform
                .DORotate(new Vector3(0f, 0f, rotation), duration, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear));
            sequence.Insert(fadeAt, renderer.DOFade(0f, fadeDuration).SetEase(Ease.Linear));
            sequence.OnComplete(() =>
            {
                activeFragments.Remove(fragment);
                if (fragment != null)
                    Destroy(fragment);
            });
        }
    }

    private void ResolveBreakSorting(out int sortingLayerId, out int sortingOrder)
    {
        SortingGroup sortingGroup = GetComponentInParent<SortingGroup>();
        if (sortingGroup != null)
        {
            sortingLayerId = sortingGroup.sortingLayerID;
            sortingOrder = sortingGroup.sortingOrder;
            return;
        }

        SpriteRenderer sourceRenderer = ResolveSourceRenderer();
        sortingLayerId = sourceRenderer != null ? sourceRenderer.sortingLayerID : 0;
        sortingOrder = sourceRenderer != null ? sourceRenderer.sortingOrder : 0;
    }

    private void ResolveReferences()
    {
        if (levelCtrl == null)
            levelCtrl = GetComponentInParent<Level130Ctrl>();
        if (dragController == null)
            dragController = GetComponent<Level130HorizontalPlatformDrag>();
    }

    private void CachePresentation()
    {
        if (platformColliders == null || platformColliders.Length == 0)
            platformColliders = GetComponentsInChildren<Collider2D>(true);
        if (platformRenderers == null || platformRenderers.Length == 0)
            platformRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private Bounds ResolvePlatformBounds()
    {
        bool hasBounds = false;
        Bounds bounds = new Bounds(transform.position, Vector3.zero);

        if (platformColliders != null)
        {
            for (int i = 0; i < platformColliders.Length; i++)
            {
                Collider2D collider = platformColliders[i];
                if (collider == null || !collider.enabled)
                    continue;

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }
        }

        if (hasBounds || platformRenderers == null)
            return bounds;

        for (int i = 0; i < platformRenderers.Length; i++)
        {
            Renderer renderer = platformRenderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }

    private SpriteRenderer ResolveSourceRenderer()
    {
        if (platformRenderers == null)
            return null;

        for (int i = 0; i < platformRenderers.Length; i++)
        {
            if (platformRenderers[i] is SpriteRenderer spriteRenderer && spriteRenderer.enabled)
                return spriteRenderer;
        }

        return null;
    }
}
