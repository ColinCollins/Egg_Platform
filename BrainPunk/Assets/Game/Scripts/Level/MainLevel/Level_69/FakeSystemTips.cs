using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 假 SystemTips，动画状态分为 In 与 Out 两段分离。
/// PlayIn 播放进入动画并停留在最后一帧；PlayOut 播放退出动画。
/// </summary>
public class FakeSystemTips : MonoBehaviour
{
    [Header("目标对象")]
    [Tooltip("动画目标 Transform，为空则使用自身")]
    [SerializeField] private Transform target;

    [Tooltip("用于透明度渐变的 SpriteRenderer 数组，为空则自动收集子节点")]
    [SerializeField] private SpriteRenderer[] renderers;

    [Tooltip("PlayIn 完成进入 Idle 后启用、PlayOut 开始时关闭的碰撞体；防御是否成立仍由 Level69Ctrl 状态判断。")]
    [SerializeField] private Collider2D shieldCollider;

    [Header("In 动画")]
    [Tooltip("进入动画持续时间")]
    [SerializeField] private float inDuration = 0.3f;

    [Tooltip("进入缓动")]
    [SerializeField] private Ease inEase = Ease.OutQuad;

    [Header("Out 动画")]
    [Tooltip("退出动画持续时间")]
    [SerializeField] private float outDuration = 0.3f;

    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform idlePoint;
    [SerializeField] private Transform endPoint;

    [Tooltip("退出缓动")]
    [SerializeField] private Ease outEase = Ease.InQuad;

    private Sequence _currentSequence;
    private Collider2D[] _colliders;
    // private Vector3 _originalPosition;
    private Vector3 _originalPosition;
    private float _currentAlpha;
    private bool _isIn;
    private bool _isInitialized;
    private bool _isShieldEnabled;

    /// <summary>
    /// 当前是否处于 In 状态
    /// </summary>
    public bool IsIn => _isIn;

    /// <summary>
    /// 当前提示碰撞体是否已启用；实际防御仍需 Level69Ctrl 处于 Defending。
    /// </summary>
    public bool IsShieldEnabled => _isShieldEnabled;

    /// <summary>
    /// 供导弹同物理步仲裁使用的盾牌碰撞体，只能读取，不得由外部直接改 enabled。
    /// </summary>
    public Collider2D ShieldCollider => shieldCollider;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void Start()
    {
        EnsureInitialized();

        if (!_isIn)
        {
            _currentAlpha = 0f;
            SetRenderersAlpha(0f);
            SetShieldEnabled(false);
        }
    }

    private void EnsureInitialized()
    {
        if (_isInitialized)
            return;

        if (target == null)
            target = transform;

        if (renderers == null || renderers.Length == 0)
            renderers = target.GetComponentsInChildren<SpriteRenderer>(true);

        _colliders = target.GetComponentsInChildren<Collider2D>(true);
        if (shieldCollider == null && _colliders != null && _colliders.Length > 0)
            shieldCollider = _colliders[0];
        _originalPosition = target.localPosition;
        _isInitialized = true;
        SetShieldEnabled(false);
    }

    /// <summary>
    /// 播放 In 动画，动画结束后停留在最后一帧
    /// </summary>
    public void PlayIn()
    {
        PlayIn(null);
    }

    /// <summary>
    /// 播放 In 动画并在抵达中间停顿点时回调；回调只属于当前动画序列。
    /// </summary>
    /// <param name="completed">抵达停顿点后的回调，动画被中断时不会调用。</param>
    public void PlayIn(Action completed)
    {
        EnsureInitialized();
        KillCurrentSequence();
        _isIn = true;
        gameObject.SetActive(true);
        SetShieldEnabled(false);

        _currentAlpha = 0f;
        SetRenderersAlpha(0f);
        var pos = target.position;
        pos.y = startPoint != null ? startPoint.position.y : target.parent != null
            ? target.parent.TransformPoint(_originalPosition).y
            : _originalPosition.y;
        target.position = pos;

        _currentSequence = DOTween.Sequence();
        float idleY = idlePoint != null ? idlePoint.position.y : pos.y;
        _currentSequence.Append(target.DOMoveY(idleY, Mathf.Max(0f, inDuration)).SetEase(inEase));
        _currentSequence.Join(DOTween.To(() => _currentAlpha, value =>
        {
            _currentAlpha = value;
            SetRenderersAlpha(value);
        }, 1f, Mathf.Max(0f, inDuration)));
        _currentSequence.OnComplete(() =>
        {
            if (_isIn)
            {
                SetShieldEnabled(true);
                completed?.Invoke();
            }
        });
    }

    /// <summary>
    /// 播放 Out 动画
    /// </summary>
    public void PlayOut()
    {
        PlayOut(null);
    }

    /// <summary>
    /// 播放 Out 动画并在完全隐藏后回调；开始退出时立即关闭提示碰撞体。
    /// </summary>
    /// <param name="completed">完全隐藏后的回调，动画被中断时不会调用。</param>
    public void PlayOut(Action completed)
    {
        EnsureInitialized();

        if (!_isIn)
        {
            SetShieldEnabled(false);
            completed?.Invoke();
            return;
        }

        KillCurrentSequence();
        _isIn = false;
        SetShieldEnabled(false);

        _currentSequence = DOTween.Sequence();
        float endY = endPoint != null ? endPoint.position.y : target.position.y;
        _currentSequence.Append(target.DOMoveY(endY, Mathf.Max(0f, outDuration)).SetEase(outEase));
        _currentSequence.Join(DOTween.To(() => _currentAlpha, value =>
        {
            _currentAlpha = value;
            SetRenderersAlpha(value);
        }, 0f, Mathf.Max(0f, outDuration)));
        _currentSequence.OnComplete(() =>
        {
            gameObject.SetActive(false);
            completed?.Invoke();
        });
    }

    /// <summary>
    /// 切换提示碰撞体；PlayIn 完成后可为拖拽命中开启，导弹防御仍由 Level69Ctrl 仲裁。
    /// </summary>
    public void SetShieldEnabled(bool enabled)
    {
        EnsureInitializedIfPossible();
        _isShieldEnabled = enabled && shieldCollider != null && gameObject.activeInHierarchy;
        if (shieldCollider != null)
            shieldCollider.enabled = _isShieldEnabled;
    }

    /// <summary>
    /// 立即中止动画、关闭碰撞并隐藏，用于失败、重开、退出、禁用和销毁清理。
    /// </summary>
    public void AbortAndHide()
    {
        EnsureInitialized();
        KillCurrentSequence();
        _isIn = false;
        SetShieldEnabled(false);
        _currentAlpha = 0f;
        SetRenderersAlpha(0f);
        gameObject.SetActive(false);
    }

    private void KillCurrentSequence()
    {
        if (_currentSequence != null)
        {
            _currentSequence.Kill(false);
            _currentSequence = null;
        }
    }

    private void SetRenderersAlpha(float alpha)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            Color color = renderers[i].color;
            color.a = alpha;
            renderers[i].color = color;
        }
    }

    private void SetCollidersEnabled(bool enabled)
    {
        if (_colliders == null)
            return;

        for (int i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] == null)
                continue;

            _colliders[i].enabled = enabled;
        }
    }

    private void EnsureInitializedIfPossible()
    {
        if (!_isInitialized && gameObject.activeInHierarchy)
            EnsureInitialized();
    }

    private void OnDisable()
    {
        KillCurrentSequence();
        _isShieldEnabled = false;
        if (shieldCollider != null)
            shieldCollider.enabled = false;
    }

    private void OnDestroy()
    {
        KillCurrentSequence();
    }
}
