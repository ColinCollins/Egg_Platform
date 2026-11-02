using System.Collections;
using UnityEngine;

/// <summary>
/// DLevel_005 运行时箭矢的飞行朝向、箭间碰撞隔离与命中定格组件。
/// 只处理本关克隆箭；玩家受伤、失败事件和箭矢清理由原有组件与 DLevel5Ctrl 负责。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[AddComponentMenu("Game/Draw/Level 5 Arrow Impact Handle")]
public sealed class DLevel5ArrowImpactHandle : MonoBehaviour
{
    #region Constants

    private const float MinimumDirectionSqrMagnitude = 0.0001f;

    #endregion

    #region Runtime State

    private Rigidbody2D _arrowRigidbody;
    private Collider2D[] _arrowColliders;
    private SpriteRenderer[] _spriteRenderers;
    private Color[] _spriteBaseColors;
    private Coroutine _disableSimulationCoroutine;
    private Coroutine _fadeInCoroutine;
    private Vector2 _lastFlightDirection = Vector2.left;
    private Vector2 _lastFlightVelocity = Vector2.left;
    private bool _isInitialized;
    private bool _hasImpacted;

    #endregion

    #region Unity Lifecycle

    private void FixedUpdate()
    {
        if (!_isInitialized || _hasImpacted || _arrowRigidbody == null)
            return;

        Vector2 velocity = _arrowRigidbody.linearVelocity;
        if (velocity.sqrMagnitude <= MinimumDirectionSqrMagnitude)
            return;

        _lastFlightVelocity = velocity;
        _lastFlightDirection = velocity.normalized;
        AlignArrowToDirection(_lastFlightDirection);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!_isInitialized || _hasImpacted || collision == null)
            return;

        Collider2D impactCollider = ResolveImpactCollider(collision);
        if (impactCollider == null)
            return;

        DLevel5ArrowImpactHandle otherArrow =
            impactCollider.GetComponentInParent<DLevel5ArrowImpactHandle>();
        if (otherArrow != null && !ReferenceEquals(otherArrow, this))
        {
            IgnoreCollisionsWith(otherArrow);
            RestoreFlightAfterArrowContact();
            return;
        }

        _hasImpacted = true;
        AlignArrowToDirection(_lastFlightDirection);
        Transform impactTarget = impactCollider.transform;
        _disableSimulationCoroutine =
            StartCoroutine(FinalizeImpactAfterPhysicsStep(impactTarget));
    }

    private void OnDisable()
    {
        if (_disableSimulationCoroutine != null)
        {
            StopCoroutine(_disableSimulationCoroutine);
            _disableSimulationCoroutine = null;
        }

        if (_fadeInCoroutine != null)
        {
            StopCoroutine(_fadeInCoroutine);
            _fadeInCoroutine = null;
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// 初始化一支已生成箭矢的飞行表现。
    /// 调用边界：仅由同一 DLevel_005 实例的 DLevel5Ctrl 在写入初速度后调用一次。
    /// 注意事项：传入刚体必须属于当前箭对象；后续命中会将其转为仅保留视觉的定格对象。
    /// </summary>
    /// <param name="arrowRigidbody">当前运行时箭根节点上的刚体。</param>
    /// <param name="initialFlightVelocity">本次物理发射使用的初始世界速度。</param>
    /// <param name="fadeInDuration">从透明渐显到原透明度的时间，单位为秒。</param>
    public void Initialize(
        Rigidbody2D arrowRigidbody,
        Vector2 initialFlightVelocity,
        float fadeInDuration)
    {
        _arrowRigidbody =
            arrowRigidbody != null
                ? arrowRigidbody
                : GetComponent<Rigidbody2D>();
        CacheColliders();
        _hasImpacted = false;
        _isInitialized = _arrowRigidbody != null;
        if (!_isInitialized)
            return;

        Vector2 velocity = initialFlightVelocity;
        if (velocity.sqrMagnitude > MinimumDirectionSqrMagnitude)
        {
            _lastFlightVelocity = velocity;
            _lastFlightDirection = velocity.normalized;
        }

        AlignArrowToDirection(_lastFlightDirection);
        BeginFadeIn(fadeInDuration);
    }

    /// <summary>
    /// 忽略当前箭与另一支 DLevel_005 箭之间的全部 Collider2D 配对。
    /// 调用边界：仅由 DLevel5Ctrl 在新箭启用后、下一次物理步进前调用。
    /// 注意事项：只影响传入的两支箭，不修改全局 Layer Collision Matrix。
    /// </summary>
    public void IgnoreCollisionsWith(DLevel5ArrowImpactHandle otherArrow)
    {
        if (otherArrow == null || ReferenceEquals(otherArrow, this))
            return;

        CacheColliders();
        otherArrow.CacheColliders();
        if (_arrowColliders == null || otherArrow._arrowColliders == null)
            return;

        for (int i = 0; i < _arrowColliders.Length; i++)
        {
            Collider2D currentCollider = _arrowColliders[i];
            if (currentCollider == null)
                continue;

            for (int j = 0; j < otherArrow._arrowColliders.Length; j++)
            {
                Collider2D otherCollider = otherArrow._arrowColliders[j];
                if (otherCollider != null)
                    Physics2D.IgnoreCollision(currentCollider, otherCollider, true);
            }
        }
    }

    #endregion

    #region Impact Control

    private void CacheColliders()
    {
        if (_arrowColliders == null || _arrowColliders.Length == 0)
            _arrowColliders = GetComponentsInChildren<Collider2D>(true);
    }

    private void BeginFadeIn(float duration)
    {
        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (_spriteRenderers == null || _spriteRenderers.Length == 0)
            return;

        _spriteBaseColors = new Color[_spriteRenderers.Length];
        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = _spriteRenderers[i];
            if (spriteRenderer != null)
                _spriteBaseColors[i] = spriteRenderer.color;
        }

        float safeDuration = Mathf.Max(0f, duration);
        if (safeDuration <= Mathf.Epsilon)
        {
            ApplyFadeProgress(1f);
            return;
        }

        ApplyFadeProgress(0f);
        _fadeInCoroutine = StartCoroutine(FadeIn(safeDuration));
    }

    private IEnumerator FadeIn(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            yield return null;
            elapsed += Time.deltaTime;
            ApplyFadeProgress(Mathf.Clamp01(elapsed / duration));
        }

        ApplyFadeProgress(1f);
        _fadeInCoroutine = null;
    }

    private void ApplyFadeProgress(float progress)
    {
        if (_spriteRenderers == null || _spriteBaseColors == null)
            return;

        float clampedProgress = Mathf.Clamp01(progress);
        int rendererCount = Mathf.Min(
            _spriteRenderers.Length,
            _spriteBaseColors.Length);
        for (int i = 0; i < rendererCount; i++)
        {
            SpriteRenderer spriteRenderer = _spriteRenderers[i];
            if (spriteRenderer == null)
                continue;

            Color color = _spriteBaseColors[i];
            color.a *= clampedProgress;
            spriteRenderer.color = color;
        }
    }

    private Collider2D ResolveImpactCollider(Collision2D collision)
    {
        Collider2D firstCollider = collision.collider;
        if (!IsOwnedCollider(firstCollider))
            return firstCollider;

        Collider2D secondCollider = collision.otherCollider;
        return !IsOwnedCollider(secondCollider)
            ? secondCollider
            : null;
    }

    private bool IsOwnedCollider(Collider2D collider)
    {
        if (collider == null)
            return true;

        Transform colliderTransform = collider.transform;
        return ReferenceEquals(colliderTransform, transform) ||
               colliderTransform.IsChildOf(transform);
    }

    private void RestoreFlightAfterArrowContact()
    {
        if (_arrowRigidbody == null)
            return;

        _arrowRigidbody.linearVelocity = _lastFlightVelocity;
        _arrowRigidbody.angularVelocity = 0f;
        AlignArrowToDirection(_lastFlightDirection);
    }

    private void AlignArrowToDirection(Vector2 direction)
    {
        if (_arrowRigidbody == null ||
            direction.sqrMagnitude <= MinimumDirectionSqrMagnitude)
        {
            return;
        }

        float desiredRotation =
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        _arrowRigidbody.SetRotation(desiredRotation);
    }

    private void FreezeRigidbody()
    {
        if (_arrowRigidbody == null)
            return;

        _arrowRigidbody.linearVelocity = Vector2.zero;
        _arrowRigidbody.angularVelocity = 0f;
        _arrowRigidbody.bodyType = RigidbodyType2D.Kinematic;
        _arrowRigidbody.constraints = RigidbodyConstraints2D.FreezeAll;
    }

    private IEnumerator FinalizeImpactAfterPhysicsStep(Transform impactTarget)
    {
        yield return new WaitForFixedUpdate();

        if (_arrowRigidbody != null)
        {
            FreezeRigidbody();

            if (impactTarget != null &&
                !ReferenceEquals(impactTarget, transform) &&
                !impactTarget.IsChildOf(transform))
            {
                transform.SetParent(impactTarget, true);
            }

            _arrowRigidbody.simulated = false;
        }

        _disableSimulationCoroutine = null;
    }

    #endregion
}
