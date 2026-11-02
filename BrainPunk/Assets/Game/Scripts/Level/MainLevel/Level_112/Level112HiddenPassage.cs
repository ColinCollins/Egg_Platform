using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Level112 中央暗道：遮挡块初始显示，Actor 进入后渐隐、离开后渐显；箱子由 Stuff 专用实体碰撞体挡住。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class Level112HiddenPassage : MonoBehaviour
{
    [SerializeField] private SpriteRenderer hiddenVisual; // 本关暗道遮挡 Renderer；运行时初始显示。
    [SerializeField] private BoxCollider2D stuffBlocker; // 只挡 Stuff，不参与 Actor 的墙检测。
    [SerializeField] private BoxCollider2D actorRevealTrigger; // 历史字段名；只接收 Actor，进入后控制遮挡块渐隐。
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.25f;
    [SerializeField, Min(0f)] private float revealHorizontalInset; // 保留可调内沿；当前为 0，使角色中心进入黄色范围即渐隐。

    [Header("新素材碰撞范围")]
    [SerializeField] private Vector2 stuffBlockerSize = new Vector2(2.18f, 1.36f);
    [SerializeField] private Vector2 actorVisibilityTriggerSize = new Vector2(1.82f, 1.02f);
    [SerializeField] private Vector2 colliderOffset = new Vector2(0f, -0.21f);

    private readonly HashSet<Collider2D> overlappingActorColliders = new();
    private float visibleAlpha = 1f;
    private float targetAlpha;

    #region Unity 生命周期

    private void Awake()
    {
        if (hiddenVisual == null)
            hiddenVisual = GetComponent<SpriteRenderer>();

        ResolveColliders();
        ConfigureCollisionLayers();
        ConfigureColliderGeometry();

        if (Application.isPlaying)
            SetInitialVisualState();
    }

    private void OnValidate()
    {
        if (hiddenVisual == null)
            hiddenVisual = GetComponent<SpriteRenderer>();

        ResolveColliders();
        ConfigureColliderGeometry();
    }

    private void Update()
    {
        if (hiddenVisual == null)
            return;

        RefreshRevealTarget();

        Color color = hiddenVisual.color;
        if (Mathf.Approximately(color.a, targetAlpha))
            return;

        if (!hiddenVisual.enabled)
            hiddenVisual.enabled = true;

        float alphaPerSecond = visibleAlpha / Mathf.Max(0.01f, fadeDuration);
        color.a = Mathf.MoveTowards(color.a, targetAlpha, alphaPerSecond * Time.deltaTime);
        hiddenVisual.color = color;

        if (Mathf.Approximately(color.a, 0f) && Mathf.Approximately(targetAlpha, 0f))
            hiddenVisual.enabled = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayerCollider(other) || !overlappingActorColliders.Add(other))
            return;

        RefreshRevealTarget();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsPlayerCollider(other) || !overlappingActorColliders.Remove(other))
            return;

        RefreshRevealTarget();
    }

    private void OnDisable()
    {
        overlappingActorColliders.Clear();
        float restoreAlpha = visibleAlpha > 0f ? visibleAlpha : 1f;
        targetAlpha = restoreAlpha;

        if (Application.isPlaying)
            ApplyAlphaImmediately(restoreAlpha);
    }

    #endregion

    #region 显隐状态

    private void SetInitialVisualState()
    {
        if (hiddenVisual == null)
            return;

        visibleAlpha = Mathf.Clamp01(hiddenVisual.color.a);
        if (visibleAlpha <= 0f)
            visibleAlpha = 1f;

        targetAlpha = visibleAlpha;
        ApplyAlphaImmediately(visibleAlpha);
    }

    private void ApplyAlphaImmediately(float alpha)
    {
        if (hiddenVisual == null)
            return;

        Color color = hiddenVisual.color;
        color.a = alpha;
        hiddenVisual.color = color;
        hiddenVisual.enabled = alpha > 0f;
    }

    private static bool IsPlayerCollider(Collider2D other)
    {
        if (other == null)
            return false;

        ActorCtrl actor = other.GetComponentInParent<ActorCtrl>();
        return actor != null && actor.CompareTag("Player");
    }

    private void RefreshRevealTarget()
    {
        bool actorCenterIsInside = false;
        if (actorRevealTrigger != null)
        {
            Bounds triggerBounds = actorRevealTrigger.bounds;
            float inset = Mathf.Min(revealHorizontalInset, triggerBounds.extents.x * 0.9f);
            float innerMinX = triggerBounds.min.x + inset;
            float innerMaxX = triggerBounds.max.x - inset;

            foreach (Collider2D actorCollider in overlappingActorColliders)
            {
                if (!IsPlayerCollider(actorCollider))
                    continue;

                ActorCtrl actor = actorCollider.GetComponentInParent<ActorCtrl>();
                float actorCenterX = actor.transform.position.x;
                if (actorCenterX > innerMinX && actorCenterX < innerMaxX)
                {
                    actorCenterIsInside = true;
                    break;
                }
            }
        }

        targetAlpha = actorCenterIsInside ? 0f : visibleAlpha;
        if (!actorCenterIsInside && hiddenVisual != null)
            hiddenVisual.enabled = true;
    }

    #endregion

    #region 碰撞分流

    private void ResolveColliders()
    {
        if (stuffBlocker != null && actorRevealTrigger != null)
            return;

        BoxCollider2D[] colliders = GetComponents<BoxCollider2D>();
        foreach (BoxCollider2D candidate in colliders)
        {
            if (candidate == null)
                continue;

            if (candidate.isTrigger)
                actorRevealTrigger = candidate;
            else
                stuffBlocker = candidate;
        }
    }

    private void ConfigureCollisionLayers()
    {
        int triggerItemLayer = LayerMask.NameToLayer("TriggerItem");
        if (triggerItemLayer >= 0)
            gameObject.layer = triggerItemLayer;

        LayerMask actorLayer = LayerMask.GetMask("Actor");
        LayerMask stuffLayer = LayerMask.GetMask("Stuff");
        if (stuffBlocker != null)
        {
            stuffBlocker.isTrigger = false;
            stuffBlocker.includeLayers = stuffLayer;
            stuffBlocker.excludeLayers = actorLayer;
            stuffBlocker.layerOverridePriority = 1;
        }

        if (actorRevealTrigger != null)
        {
            actorRevealTrigger.isTrigger = true;
            actorRevealTrigger.includeLayers = actorLayer;
            actorRevealTrigger.excludeLayers = stuffLayer;
            actorRevealTrigger.layerOverridePriority = 2;
        }
    }

    private void ConfigureColliderGeometry()
    {
        ApplyColliderGeometry(stuffBlocker, stuffBlockerSize, colliderOffset);
        ApplyColliderGeometry(actorRevealTrigger, actorVisibilityTriggerSize, colliderOffset);
    }

    private static void ApplyColliderGeometry(
        BoxCollider2D target,
        Vector2 configuredSize,
        Vector2 configuredOffset)
    {
        if (target == null)
            return;

        Vector2 safeSize = new Vector2(
            Mathf.Max(0.01f, configuredSize.x),
            Mathf.Max(0.01f, configuredSize.y));
        if (target.size != safeSize)
            target.size = safeSize;
        if (target.offset != configuredOffset)
            target.offset = configuredOffset;
    }

    #endregion
}
