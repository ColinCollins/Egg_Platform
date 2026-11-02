using DG.Tweening;
using UnityEngine;

/// <summary>
/// 碰上去马上就会碎的平台：碰到后 Collider 消失，并播放自身破碎动画。
/// 放在 Temp/L41 供策划调试，程序审核后可移入正式关卡目录。
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class FragilePlatform : MonoBehaviour
{
    [Header("破碎设置")]
    [Tooltip("破碎动画的 Animator 状态名（与 Animator Controller 里一致）；为空则不播放动画。")]
    [SerializeField] private string breakAnimationStateName = "Break";

    [Tooltip("仅当碰到这些层上的物体时才破碎；Nothing 表示任意物体碰到都会碎。")]
    [SerializeField] private LayerMask triggerLayers = -1;

    [Header("破碎后")]

    [Tooltip("渐隐时长（秒）；≤0 则与「销毁延迟」相同，便于与 Destroy 对齐。")]
    [SerializeField] private float fadeOutDuration;

    [Tooltip("动画播完后多少秒销毁自身；≤0 表示不自动销毁，仅关掉碰撞和播动画。")]
    [SerializeField] private float destroyAfterSeconds = 1f;

    private Collider2D _collider;
    private Animator _animator;
    private bool _hasBroken;
    private SpriteRenderer[] _spriteRenderers;

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        _animator = GetComponent<Animator>();
        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void OnDestroy()
    {
        if (_spriteRenderers != null)
        {
            foreach (var sr in _spriteRenderers)
            {
                if (sr != null)
                    sr.DOKill();
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryBreak(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryBreak(other.gameObject);
    }

    private void TryBreak(GameObject other)
    {
        if (_hasBroken)
            return;

        if (((1 << other.layer) & triggerLayers) == 0)
            return;

        _hasBroken = true;

        if (_collider != null)
            _collider.enabled = false;

        if (_animator != null && !string.IsNullOrEmpty(breakAnimationStateName))
            _animator.Play(breakAnimationStateName, 0, 0f);

        if (destroyAfterSeconds <= 0f)
            return;

        var delay = destroyAfterSeconds;
        if (_spriteRenderers != null && _spriteRenderers.Length > 0)
        {
            foreach (var sr in _spriteRenderers)
            {
                if (sr != null)
                    sr.DOKill();
            }

            var fade = fadeOutDuration > 0f ? fadeOutDuration : destroyAfterSeconds;
            foreach (var sr in _spriteRenderers)
            {
                if (sr != null)
                {
                    sr.DOFade(0f, fade)
                        .SetEase(Ease.Linear)
                        .SetUpdate(true);
                }
            }
            delay = Mathf.Max(destroyAfterSeconds, fade);
        }

        Destroy(gameObject, delay);
    }
}
