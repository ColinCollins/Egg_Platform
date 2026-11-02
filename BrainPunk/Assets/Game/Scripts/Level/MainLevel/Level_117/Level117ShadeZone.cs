using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Level117 遮阳伞阴影区域。优先使用 Collider2D 触发注册；没有 Collider 时由控制器使用渲染器 bounds 兜底。
/// </summary>
public sealed class Level117ShadeZone : MonoBehaviour
{
    [Header("关卡引用")]
    [Tooltip("可选的 Level117 控制器；为空时自动查找父级或场景中的控制器。")]
    [SerializeField] private Level117Ctrl controller;

    [Tooltip("可选的阴影碰撞体；为空时自动查找本物体及子物体上的 Collider2D。")]
    [SerializeField] private Collider2D shadeCollider;

    [Tooltip("玩家实际看到的地面阴影；为空时自动查找名为 Sp_l117_zys_02 的 SpriteRenderer。")]
    [SerializeField] private SpriteRenderer groundShadowRenderer;

    private ActorCtrl registeredActor;
    private readonly HashSet<Collider2D> registeredColliders = new HashSet<Collider2D>();
    private Collider2D[] cachedActorColliders;
    private ActorCtrl cachedColliderActor;
    private SpriteRenderer canopyRenderer;
    private bool colliderLookupCompleted;
    private bool canopyLookupCompleted;

    /// <summary>
    /// 当前阴影区域是否包含指定角色。无碰撞体时返回渲染器 bounds 的包含结果。
    /// </summary>
    /// <param name="actor">待检测的角色。</param>
    /// <returns>角色位于阴影范围内时返回 true。</returns>
    public bool ContainsActor(ActorCtrl actor)
    {
        if (actor == null || !isActiveAndEnabled)
            return false;

        Collider2D collider = ResolveCollider();
        if (collider != null && collider.enabled && collider.gameObject.activeInHierarchy)
            return IntersectsActor(collider, actor);

        Bounds canopyBounds;
        if (!TryGetShadeCanopyBounds(out canopyBounds))
            return false;

        Bounds actorBounds = GetActorBounds(actor);
        Bounds horizontalBounds = TryGetGroundShadowBounds(out Bounds groundBounds)
            ? groundBounds
            : canopyBounds;
        return actorBounds.center.x >= horizontalBounds.min.x &&
               actorBounds.center.x <= horizontalBounds.max.x &&
               actorBounds.max.y <= canopyBounds.max.y + 0.25f &&
               actorBounds.min.y >= canopyBounds.min.y - GetVerticalDepth();
    }

    private void Awake()
    {
        ResolveController();
        ResolveCollider();
        ResolveGroundShadowRenderer();
    }

    private void OnEnable()
    {
        ResolveController();
    }

    private void OnDisable()
    {
        if (registeredActor != null)
        {
            ResolveController()?.UnregisterShadeZone(this, registeredActor);
            registeredActor = null;
        }

        registeredColliders.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        ActorCtrl actor = other != null ? other.GetComponentInParent<ActorCtrl>() : null;
        if (actor == null)
            return;

        Level117Ctrl level = ResolveController();
        if (level == null)
            return;

        registeredColliders.Add(other);
        registeredActor = actor;
        level.RegisterShadeZone(this, actor);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        ActorCtrl actor = other != null ? other.GetComponentInParent<ActorCtrl>() : null;
        if (actor == null || registeredActor != actor)
            return;

        registeredColliders.Remove(other);
        if (registeredColliders.Count == 0)
        {
            ResolveController()?.UnregisterShadeZone(this, actor);
            registeredActor = null;
        }
    }

    private Level117Ctrl ResolveController()
    {
        if (controller == null)
            controller = GetComponentInParent<Level117Ctrl>();

        if (controller == null)
            controller = FindFirstObjectByType<Level117Ctrl>();

        return controller;
    }

    private Collider2D ResolveCollider()
    {
        if (shadeCollider != null)
            return shadeCollider;

        if (colliderLookupCompleted)
            return null;

        colliderLookupCompleted = true;
        shadeCollider = GetComponent<Collider2D>();

        if (shadeCollider == null)
            shadeCollider = GetComponentInChildren<Collider2D>(true);

        return shadeCollider;
    }

    private SpriteRenderer ResolveGroundShadowRenderer()
    {
        if (groundShadowRenderer != null)
            return groundShadowRenderer;

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer != null &&
                string.Equals(renderer.name, "Sp_l117_zys_02", System.StringComparison.OrdinalIgnoreCase))
            {
                groundShadowRenderer = renderer;
                break;
            }
        }

        return groundShadowRenderer;
    }

    private bool TryGetGroundShadowBounds(out Bounds bounds)
    {
        SpriteRenderer renderer = ResolveGroundShadowRenderer();
        if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
        {
            bounds = renderer.bounds;
            return true;
        }

        bounds = default;
        return false;
    }

    private float GetVerticalDepth()
    {
        Level117Ctrl level = ResolveController();
        return level != null ? Mathf.Max(0f, level.ShadeVerticalDepth) : 4f;
    }

    private bool TryGetShadeCanopyBounds(out Bounds bounds)
    {
        if (!canopyLookupCompleted)
        {
            canopyLookupCompleted = true;
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer != null &&
                    string.Equals(renderer.name, "zheyang", System.StringComparison.OrdinalIgnoreCase))
                {
                    canopyRenderer = renderer;
                    break;
                }
            }
        }

        if (canopyRenderer != null && canopyRenderer.enabled && canopyRenderer.gameObject.activeInHierarchy)
        {
            bounds = canopyRenderer.bounds;
            return true;
        }

        bounds = default;
        return false;
    }

    private Bounds GetActorBounds(ActorCtrl actor)
    {
        Collider2D[] colliders = GetActorColliders(actor);
        bool hasBounds = false;
        Bounds bounds = new Bounds(actor.transform.position, Vector3.zero);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null || !collider.enabled || collider.isTrigger)
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

        return bounds;
    }

    private bool IntersectsActor(Collider2D shade, ActorCtrl actor)
    {
        Bounds shadeBounds = shade.bounds;
        Collider2D[] actorColliders = GetActorColliders(actor);
        for (int i = 0; i < actorColliders.Length; i++)
        {
            Collider2D actorCollider = actorColliders[i];
            if (actorCollider != null && actorCollider.enabled && shadeBounds.Intersects(actorCollider.bounds))
                return true;
        }

        return shade.OverlapPoint(actor.transform.position);
    }

    private Collider2D[] GetActorColliders(ActorCtrl actor)
    {
        if (cachedColliderActor != actor || cachedActorColliders == null)
        {
            cachedColliderActor = actor;
            cachedActorColliders = actor != null
                ? actor.GetComponentsInChildren<Collider2D>(true)
                : System.Array.Empty<Collider2D>();
        }

        return cachedActorColliders;
    }
}
