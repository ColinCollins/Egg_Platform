using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Level62 fragile block platform. Unselected blocks block actor contact; selected
/// blocks allow contact. Actor contact starts a configurable delay, then disables
/// contact, fades out, and restores the block to unselected state.
///
/// version 2.0.0
/// fadesout 后不要 destroy 掉，重新成为未选中状态就可以了。
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRendererGroupAlpha))]
public class SpriteRenderFragilePlatform : MonoBehaviour
{
    private enum BreakState
    {
        Intact,
        Breaking
    }

    [Header("Trigger")]
    [Tooltip("Only objects on these layers can trigger the fragile platform.")]
    [SerializeField] private LayerMask triggerLayers = -1;

    [Header("Contact")]
    [Tooltip("True when the actor can touch this platform. Level62 selection controls this value.")]
    [FormerlySerializedAs("autoBreak")]
    [SerializeField] private bool actorContactEnabled = true;

    [Header("Break")]
    [Tooltip("Seconds after actor contact before disabling this platform's collider and starting fade-out.")]
    [FormerlySerializedAs("destroyAfterSeconds")]
    [SerializeField] private float contactBreakDelaySeconds = 1f;

    [Tooltip("Fade-out duration in seconds.")]
    [SerializeField] private float fadeOutDuration = 0.5f;

    private Collider2D _collider;
    private SpriteRendererGroupAlpha _alphaGroup;
    private Level62BlockItemCtrl _blockItem;
    private BreakState _breakState = BreakState.Intact;
    private float _currentAlpha;
    private Tween _breakDelayTween;
    private Tween _fadeTween;

    public bool IsBreaking => _breakState == BreakState.Breaking;

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        _alphaGroup = GetComponent<SpriteRendererGroupAlpha>();
        _blockItem = GetComponent<Level62BlockItemCtrl>();
        ApplyContactState();
    }

    private void OnDestroy()
    {
        KillTweens();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryBreak(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryBreak(other.gameObject);
    }

    public void SetActorContactEnabled(bool value)
    {
        actorContactEnabled = value;
        ApplyContactState();
    }

    public bool GetActorContactEnabled()
    {
        return actorContactEnabled;
    }

    public void SetAutoBreak(bool value)
    {
        SetActorContactEnabled(value);
    }

    public bool GetAutoBreak()
    {
        return GetActorContactEnabled();
    }

    private void TryBreak(GameObject other)
    {
        if (!actorContactEnabled || other == null || _breakState != BreakState.Intact)
        {
            return;
        }

        if (((1 << other.layer) & triggerLayers) == 0)
        {
            return;
        }

        BeginBreak();
    }

    private void BeginBreak()
    {
        _breakState = BreakState.Breaking;
        ScheduleColliderBreak();
    }

    private void ScheduleColliderBreak()
    {
        if (contactBreakDelaySeconds <= 0f)
        {
            DisableColliderAndFadeOut();
            return;
        }

        _breakDelayTween?.Kill();
        _breakDelayTween = DOVirtual.DelayedCall(contactBreakDelaySeconds, DisableColliderAndFadeOut).SetUpdate(true);
    }

    private void DisableColliderAndFadeOut()
    {
        if (_breakState != BreakState.Breaking)
        {
            return;
        }

        _breakDelayTween?.Kill();
        _breakDelayTween = null;
        SetColliderEnabled(false);
        StartFadeTween();
    }

    private void StartFadeTween()
    {
        if (_alphaGroup == null || fadeOutDuration <= 0f)
        {
            RestoreUnselectedAfterFade();
            return;
        }

        _fadeTween?.Kill();
        _currentAlpha = _alphaGroup.GetAlpha();
        _fadeTween = DOTween.To(
                () => _currentAlpha,
                value =>
                {
                    _currentAlpha = value;
                    _alphaGroup.SetAlpha(value);
                },
                0.3f,
                fadeOutDuration
            )
            .SetEase(Ease.Linear)
            .SetUpdate(true)
            .OnComplete(RestoreUnselectedAfterFade);
    }

    private void RestoreUnselectedAfterFade()
    {
        if (_breakState != BreakState.Breaking)
        {
            return;
        }

        KillTweens();
        _breakState = BreakState.Intact;
        actorContactEnabled = false;

        if (_blockItem != null)
        {
            _blockItem.RestoreUnselectedAfterFragileFade();
        }
        else
        {
            SetColliderEnabled(false);
        }
    }

    private void ApplyContactState()
    {
        if (_breakState != BreakState.Intact)
        {
            return;
        }

        SetColliderEnabled(actorContactEnabled);
    }

    private void SetColliderEnabled(bool value)
    {
        if (_collider != null)
        {
            _collider.enabled = value;
        }
    }

    private void KillTweens()
    {
        _breakDelayTween?.Kill();
        _breakDelayTween = null;
        _fadeTween?.Kill();
        _fadeTween = null;
    }
}
