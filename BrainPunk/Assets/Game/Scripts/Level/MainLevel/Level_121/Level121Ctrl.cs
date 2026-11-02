using System.Collections;
using Bear.EventSystem;
using DG.Tweening;
using Game.Events;
using Game.Play;
using UnityEngine;

/// <summary>
/// Level121“金钱的力量”控制器：保留 Level31 的金币雨与计数，同时负责气泡入场、可推动门和三次金币破锁流程。
/// </summary>
[DisallowMultipleComponent]
public sealed class Level121Ctrl : Level31Ctrl
{
    private const float ScoreBubbleEntranceFallbackDelay = 2.5f;

    #region Inspector

    [Header("Score Bubble")]
    [Tooltip("金币数字、金币图标和气泡共同所在的 World Space Canvas 根节点。")]
    [SerializeField] private RectTransform scoreBubbleRoot;

    [Tooltip("用于气泡入场和消失淡入淡出的 CanvasGroup。")]
    [SerializeField] private CanvasGroup scoreBubbleCanvasGroup;

    [Tooltip("气泡跟随的门节点；推动门时保持初始世界偏移。")]
    [SerializeField] private Transform scoreBubbleFollowTarget;

    [Tooltip("进入关卡后延迟播放气泡入场，避免动画被入场遮罩覆盖。")]
    [SerializeField, Min(0f)] private float scoreBubbleAppearDelay = 0.25f;

    [Tooltip("气泡从右下角放大到当前尺寸的持续时间。")]
    [SerializeField, Min(0.01f)] private float scoreBubbleAppearDuration = 0.42f;

    [Tooltip("气泡循环上下浮动的世界高度。")]
    [SerializeField, Min(0f)] private float scoreBubbleFloatHeight = 0.12f;

    [Tooltip("气泡完成一次上下浮动的时间。")]
    [SerializeField, Min(0.1f)] private float scoreBubbleFloatDuration = 1.6f;

    [Tooltip("门锁掉落后气泡缩小淡出的持续时间。")]
    [SerializeField, Min(0.01f)] private float scoreBubbleDisappearDuration = 0.28f;

    [Header("Coin Rain")]
    [Tooltip("第三次砸落门锁时停止继续生成，并清除仍在下落的金币。")]
    [SerializeField] private Level121VerticalCoinRain coinRain;

    [Header("Movable Door")]
    [Tooltip("锁定期间供玩家水平推动的门刚体；解锁后切为 Kinematic，避免门继续漂移。")]
    [SerializeField] private Rigidbody2D movableDoorBody;

    [Tooltip("锁定期间作为实体阻挡，解锁后切为 Trigger 复用原进入门流程的碰撞体。")]
    [SerializeField] private Collider2D movableDoorCollider;

    [Tooltip("锁定期间门的质量，增加推动阻力并减少碰撞飘移。")]
    [SerializeField, Min(0.1f)] private float movableDoorMass = 12f;

    [Tooltip("锁定期间门的线性阻尼，松开推动后使门快速停下。")]
    [SerializeField, Min(0f)] private float movableDoorLinearDamping = 36f;

    [Tooltip("门被玩家推动时允许的最大水平速度。")]
    [SerializeField, Min(0.1f)] private float movableDoorMaxHorizontalSpeed = 1.5f;

    [Tooltip("门的水平速度低于该值时直接归零，消除尾部滑行。")]
    [SerializeField, Min(0f)] private float movableDoorStopSpeed = 0.12f;

    [Header("Actor Door Shadow")]
    [Tooltip("玩家 Actor 自带 ShadowController 所在节点的 SpriteRenderer；站在门顶时临时关闭整个影子节点。")]
    [SerializeField] private SpriteRenderer actorShadowMarker;

    [Tooltip("判断玩家主碰撞体底边贴住门顶的纵向容差。")]
    [SerializeField, Min(0f)] private float actorDoorContactTolerance = 0.12f;

    [Header("Coin Lock")]
    [Tooltip("随门移动的 DoorLock 根节点。")]
    [SerializeField] private Transform doorLock;

    [Tooltip("金币命中锁时播放的通用 Knock_Explosion；Prefab 中已移除 Knock 文字子节点。")]
    [SerializeField] private ParticleSystem lockHitEffect;

    [Tooltip("打开门锁需要的金币命中次数。")]
    [SerializeField, Min(1)] private int requiredLockHitCount = 3;

    [Tooltip("前两次命中时锁倾斜、下沉到新状态的持续时间。")]
    [SerializeField, Min(0.01f)] private float lockHitDuration = 0.18f;

    [Tooltip("每次有效命中累计增加的本地 Z 轴倾斜角度。")]
    [SerializeField] private float lockTiltPerHit = 12f;

    [Tooltip("第二次命中的倾斜倍率，使第二次比第一次更明显。")]
    [SerializeField, Min(1f)] private float secondLockHitTiltMultiplier = 1.8f;

    [Tooltip("金币命中点距锁中心小于该值时，交替上一次的倾斜方向。")]
    [SerializeField, Min(0f)] private float lockImpactCenterDeadZone = 0.08f;

    [Tooltip("每次有效命中累计下沉的本地距离。")]
    [SerializeField, Min(0f)] private float lockDropPerHit = 0.12f;

    [Tooltip("第三次命中后锁下滑并渐隐的持续时间。")]
    [SerializeField, Min(0.01f)] private float lockReleaseDuration = 0.45f;

    [Tooltip("第三次命中后锁额外下滑的本地距离。")]
    [SerializeField, Min(0f)] private float lockReleaseDropDistance = 0.9f;

    #endregion

    #region Runtime State

    private Vector3 _scoreBubbleTargetScale;
    private Vector3 _scoreBubbleFollowOffset;
    private Vector3 _lockOriginLocalPosition;
    private Vector3 _lockOriginLocalEulerAngles;
    private ActorSpineCtrl _lockSpine;
    private Collider2D _actorCollider;
    private EventSubscriber _eventSubscriber;
    private Tween _scoreBubbleTween;
    private Sequence _lockSequence;
    private Coroutine _scoreBubbleEntranceCoroutine;
    private int _lockHitCount;
    private float _lockTiltDirection = 1f;
    private bool _runtimeStateCached;
    private bool _isLockResolved;
    private bool _isScoreBubbleVisible;
    private bool _actorShadowActiveBeforeDoor;
    private bool _actorShadowSuppressedByDoor;
    private bool _scoreBubbleEntranceStarted;
    private float _scoreBubbleFloatStartTime;

    #endregion

    #region Unity Lifecycle

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        EventsUtils.ResetEvents(ref _eventSubscriber);
        _eventSubscriber.Subscribe<SwitchGameStateEvent>(OnSwitchGameState);
        CacheRuntimeState();
        ConfigureLockedDoor();
        PlayScoreBubbleEntrance();
    }

    private void LateUpdate()
    {
        UpdateScoreBubbleFollowPosition();
        UpdateActorDoorShadowVisibility();
    }

    private void FixedUpdate()
    {
        StabilizeMovableDoor();
    }

    private void OnDisable()
    {
        if (_scoreBubbleEntranceCoroutine != null)
        {
            StopCoroutine(_scoreBubbleEntranceCoroutine);
            _scoreBubbleEntranceCoroutine = null;
        }

        if (Application.isPlaying)
        {
            EventsUtils.ResetEvents(ref _eventSubscriber);
        }

        _scoreBubbleTween?.Kill();
        _scoreBubbleTween = null;

        _lockSequence?.Kill();
        _lockSequence = null;

        SetActorShadowSuppressedByDoor(false);
    }

    #endregion

    #region Initialization

    private void CacheRuntimeState()
    {
        if (_runtimeStateCached)
        {
            return;
        }

        if (scoreBubbleRoot != null)
        {
            _scoreBubbleTargetScale = scoreBubbleRoot.localScale;
            SetPivotKeepingWorldRect(scoreBubbleRoot, new Vector2(1f, 0f));

            if (scoreBubbleCanvasGroup == null)
            {
                scoreBubbleCanvasGroup = scoreBubbleRoot.GetComponent<CanvasGroup>();
            }

            if (scoreBubbleFollowTarget != null)
            {
                _scoreBubbleFollowOffset = scoreBubbleRoot.position - scoreBubbleFollowTarget.position;
            }
        }

        if (doorLock != null)
        {
            _lockOriginLocalPosition = doorLock.localPosition;
            _lockOriginLocalEulerAngles = doorLock.localEulerAngles;
            _lockSpine = doorLock.GetComponent<ActorSpineCtrl>();
        }

        if (actorShadowMarker != null)
        {
            _actorCollider = actorShadowMarker.GetComponentInParent<Collider2D>();
        }

        _runtimeStateCached = true;
    }

    private void ConfigureLockedDoor()
    {
        _lockHitCount = 0;
        _lockTiltDirection = 1f;
        _isLockResolved = false;
        coinRain?.StartRain();

        if (movableDoorCollider != null)
        {
            movableDoorCollider.enabled = true;
            movableDoorCollider.isTrigger = false;
        }

        if (movableDoorBody != null)
        {
            movableDoorBody.simulated = true;
            movableDoorBody.bodyType = RigidbodyType2D.Dynamic;
            movableDoorBody.constraints = RigidbodyConstraints2D.FreezePositionY | RigidbodyConstraints2D.FreezeRotation;
            movableDoorBody.mass = Mathf.Max(0.1f, movableDoorMass);
            movableDoorBody.linearDamping = Mathf.Max(0f, movableDoorLinearDamping);
            movableDoorBody.linearVelocity = Vector2.zero;
            movableDoorBody.angularVelocity = 0f;
        }

        if (doorLock != null)
        {
            doorLock.gameObject.SetActive(true);
            doorLock.localPosition = _lockOriginLocalPosition;
            doorLock.localEulerAngles = _lockOriginLocalEulerAngles;

            Collider2D lockCollider = doorLock.GetComponent<Collider2D>();
            if (lockCollider != null)
            {
                lockCollider.enabled = true;
            }
        }

        _lockSpine?.SetAlpha(1f);

        if (lockHitEffect != null)
        {
            lockHitEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void StabilizeMovableDoor()
    {
        if (_isLockResolved
            || movableDoorBody == null
            || !movableDoorBody.simulated
            || movableDoorBody.bodyType != RigidbodyType2D.Dynamic)
        {
            return;
        }

        float horizontalSpeed = Mathf.Clamp(
            movableDoorBody.linearVelocityX,
            -Mathf.Max(0.1f, movableDoorMaxHorizontalSpeed),
            Mathf.Max(0.1f, movableDoorMaxHorizontalSpeed));

        if (Mathf.Abs(horizontalSpeed) < movableDoorStopSpeed)
        {
            horizontalSpeed = 0f;
        }

        movableDoorBody.linearVelocity = new Vector2(horizontalSpeed, 0f);
    }

    private void UpdateActorDoorShadowVisibility()
    {
        if (actorShadowMarker == null)
        {
            return;
        }

        SetActorShadowSuppressedByDoor(IsActorStandingOnMovableDoor());
    }

    private bool IsActorStandingOnMovableDoor()
    {
        if (_actorCollider == null
            || movableDoorCollider == null
            || !_actorCollider.enabled
            || !movableDoorCollider.enabled
            || movableDoorCollider.isTrigger
            || !_actorCollider.IsTouching(movableDoorCollider))
        {
            return false;
        }

        Bounds actorBounds = _actorCollider.bounds;
        Bounds doorBounds = movableDoorCollider.bounds;
        float horizontalOverlap = Mathf.Min(actorBounds.max.x, doorBounds.max.x)
            - Mathf.Max(actorBounds.min.x, doorBounds.min.x);
        float bottomToDoorTop = actorBounds.min.y - doorBounds.max.y;
        float tolerance = Mathf.Max(0f, actorDoorContactTolerance);

        return actorBounds.center.y > doorBounds.center.y
            && horizontalOverlap > 0.01f
            && Mathf.Abs(bottomToDoorTop) <= tolerance;
    }

    private void SetActorShadowSuppressedByDoor(bool suppressed)
    {
        if (actorShadowMarker == null)
        {
            _actorShadowSuppressedByDoor = false;
            return;
        }

        GameObject shadowRoot = actorShadowMarker.gameObject;
        if (suppressed)
        {
            if (!_actorShadowSuppressedByDoor)
            {
                _actorShadowActiveBeforeDoor = shadowRoot.activeSelf;
            }

            _actorShadowSuppressedByDoor = true;
            if (shadowRoot.activeSelf)
            {
                shadowRoot.SetActive(false);
            }

            return;
        }

        if (!_actorShadowSuppressedByDoor)
        {
            return;
        }

        shadowRoot.SetActive(_actorShadowActiveBeforeDoor);
        _actorShadowSuppressedByDoor = false;
    }

    private void PlayScoreBubbleEntrance()
    {
        if (scoreBubbleRoot == null)
        {
            return;
        }

        _scoreBubbleTween?.Kill();
        scoreBubbleRoot.gameObject.SetActive(true);
        scoreBubbleRoot.localScale = Vector3.zero;
        _isScoreBubbleVisible = true;
        _scoreBubbleEntranceStarted = false;

        if (scoreBubbleCanvasGroup != null)
        {
            scoreBubbleCanvasGroup.alpha = 0f;
        }

        if (_scoreBubbleEntranceCoroutine != null)
        {
            StopCoroutine(_scoreBubbleEntranceCoroutine);
        }

        _scoreBubbleEntranceCoroutine = StartCoroutine(PlayScoreBubbleEntranceFallback());
    }

    private void OnSwitchGameState(SwitchGameStateEvent evt)
    {
        if (evt != null && evt.NewState == GamePlayStateName.PLAYING)
        {
            BeginScoreBubbleEntrance();
        }
    }

    private IEnumerator PlayScoreBubbleEntranceFallback()
    {
        yield return new WaitForSecondsRealtime(ScoreBubbleEntranceFallbackDelay);
        _scoreBubbleEntranceCoroutine = null;
        BeginScoreBubbleEntrance();
    }

    private void BeginScoreBubbleEntrance()
    {
        if (_scoreBubbleEntranceStarted
            || !_isScoreBubbleVisible
            || scoreBubbleRoot == null
            || !isActiveAndEnabled)
        {
            return;
        }

        _scoreBubbleEntranceStarted = true;
        if (_scoreBubbleEntranceCoroutine != null)
        {
            StopCoroutine(_scoreBubbleEntranceCoroutine);
        }

        _scoreBubbleEntranceCoroutine = StartCoroutine(PlayScoreBubbleEntranceAfterDelay());
    }

    private IEnumerator PlayScoreBubbleEntranceAfterDelay()
    {
        float delay = Mathf.Max(0f, scoreBubbleAppearDelay);
        if (delay > 0f)
        {
            yield return new WaitForSecondsRealtime(delay);
        }

        _scoreBubbleEntranceCoroutine = null;
        if (!_isScoreBubbleVisible
            || scoreBubbleRoot == null
            || !scoreBubbleRoot.gameObject.activeInHierarchy)
        {
            yield break;
        }

        _scoreBubbleFloatStartTime = Time.unscaledTime;
        float duration = Mathf.Max(0.01f, scoreBubbleAppearDuration);
        Sequence entranceSequence = DOTween.Sequence();
        entranceSequence.Append(scoreBubbleRoot.DOScale(_scoreBubbleTargetScale, duration).SetEase(Ease.OutBack));
        if (scoreBubbleCanvasGroup != null)
        {
            entranceSequence.Join(scoreBubbleCanvasGroup.DOFade(1f, duration * 0.5f).SetEase(Ease.OutQuad));
        }

        entranceSequence.SetUpdate(true);
        entranceSequence.SetLink(scoreBubbleRoot.gameObject, LinkBehaviour.KillOnDisable);
        entranceSequence.OnComplete(() => _scoreBubbleTween = null);
        _scoreBubbleTween = entranceSequence;
    }

    private void UpdateScoreBubbleFollowPosition()
    {
        if (!_isScoreBubbleVisible
            || scoreBubbleRoot == null
            || scoreBubbleFollowTarget == null
            || !scoreBubbleRoot.gameObject.activeInHierarchy)
        {
            return;
        }

        float duration = Mathf.Max(0.1f, scoreBubbleFloatDuration);
        float phase = Mathf.Max(0f, Time.unscaledTime - _scoreBubbleFloatStartTime) / duration;
        float floatOffset = Mathf.Sin(phase * Mathf.PI * 2f) * scoreBubbleFloatHeight;
        scoreBubbleRoot.position = scoreBubbleFollowTarget.position
            + _scoreBubbleFollowOffset
            + Vector3.up * floatOffset;
    }

    private void PlayScoreBubbleExit()
    {
        if (!_isScoreBubbleVisible || scoreBubbleRoot == null)
        {
            return;
        }

        _isScoreBubbleVisible = false;
        _scoreBubbleEntranceStarted = true;
        if (_scoreBubbleEntranceCoroutine != null)
        {
            StopCoroutine(_scoreBubbleEntranceCoroutine);
            _scoreBubbleEntranceCoroutine = null;
        }

        _scoreBubbleTween?.Kill();

        float duration = Mathf.Max(0.01f, scoreBubbleDisappearDuration);
        Sequence exitSequence = DOTween.Sequence();
        exitSequence.Join(scoreBubbleRoot
            .DOScale(_scoreBubbleTargetScale * 0.35f, duration)
            .SetEase(Ease.InBack));
        if (scoreBubbleCanvasGroup != null)
        {
            exitSequence.Join(scoreBubbleCanvasGroup.DOFade(0f, duration).SetEase(Ease.InQuad));
        }

        exitSequence.SetUpdate(true);
        exitSequence.SetLink(scoreBubbleRoot.gameObject, LinkBehaviour.KillOnDisable);
        exitSequence.OnComplete(() =>
        {
            _scoreBubbleTween = null;
            scoreBubbleRoot.gameObject.SetActive(false);
        });
        _scoreBubbleTween = exitSequence;
    }

    private static void SetPivotKeepingWorldRect(RectTransform target, Vector2 pivot)
    {
        Vector3[] beforeCorners = new Vector3[4];
        Vector3[] afterCorners = new Vector3[4];
        target.GetWorldCorners(beforeCorners);
        target.pivot = pivot;
        target.GetWorldCorners(afterCorners);
        target.position += beforeCorners[0] - afterCorners[0];
    }

    #endregion

    #region Lock Hits

    /// <summary>
    /// 仅供本关 DoorLock 上的 <see cref="Level121LockHitReceiver"/> 转发金币碰撞；其它碰撞不会推进门锁状态。
    /// </summary>
    /// <param name="coinCollider">进入锁触发区的金币碰撞体。</param>
    internal void HandleCoinHitLock(Collider2D coinCollider)
    {
        if (_isLockResolved
            || coinCollider == null
            || coinCollider.GetComponent<Level31CoinTriggerHandle>() == null)
        {
            return;
        }

        _lockHitCount++;
        _lockTiltDirection = ResolveLockTiltDirection(coinCollider);
        PlayLockHitEffect(coinCollider);

        if (_lockHitCount >= Mathf.Max(1, requiredLockHitCount))
        {
            PlayLockRelease();
            return;
        }

        PlayLockDamagePose();
    }

    private float ResolveLockTiltDirection(Collider2D coinCollider)
    {
        if (doorLock == null || coinCollider == null)
        {
            return -_lockTiltDirection;
        }

        float horizontalOffset = coinCollider.bounds.center.x - doorLock.position.x;
        if (Mathf.Abs(horizontalOffset) <= lockImpactCenterDeadZone)
        {
            return -_lockTiltDirection;
        }

        return horizontalOffset < 0f ? 1f : -1f;
    }

    private void PlayLockHitEffect(Collider2D coinCollider)
    {
        if (lockHitEffect == null || doorLock == null)
        {
            return;
        }

        Vector3 effectPosition = lockHitEffect.transform.position;
        Vector3 impactPosition = coinCollider != null ? coinCollider.bounds.center : doorLock.position;
        effectPosition.x = impactPosition.x;
        effectPosition.y = impactPosition.y;
        lockHitEffect.transform.position = effectPosition;
        lockHitEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        lockHitEffect.Play(true);
    }

    private void PlayLockDamagePose()
    {
        if (doorLock == null)
        {
            return;
        }

        _lockSequence?.Kill();
        float duration = Mathf.Max(0.01f, lockHitDuration);
        Vector3 targetPosition = _lockOriginLocalPosition + Vector3.down * (lockDropPerHit * _lockHitCount);
        Vector3 targetEulerAngles = _lockOriginLocalEulerAngles;
        float tiltMultiplier = _lockHitCount <= 1 ? 1f : Mathf.Max(1f, secondLockHitTiltMultiplier);
        targetEulerAngles.z += lockTiltPerHit * tiltMultiplier * _lockTiltDirection;

        _lockSequence = DOTween.Sequence();
        _lockSequence.Join(doorLock.DOLocalMove(targetPosition, duration).SetEase(Ease.OutQuad));
        _lockSequence.Join(doorLock.DOLocalRotate(targetEulerAngles, duration).SetEase(Ease.OutBack));
        _lockSequence.SetLink(doorLock.gameObject, LinkBehaviour.KillOnDisable);
        _lockSequence.OnComplete(() => _lockSequence = null);
    }

    private void PlayLockRelease()
    {
        coinRain?.StopAndClearFallingCoins();

        if (doorLock == null)
        {
            PlayScoreBubbleExit();
            OpenDoorPassage();
            return;
        }

        _isLockResolved = true;
        Collider2D lockCollider = doorLock.GetComponent<Collider2D>();
        if (lockCollider != null)
        {
            lockCollider.enabled = false;
        }

        _lockSequence?.Kill();
        float duration = Mathf.Max(0.01f, lockReleaseDuration);
        Vector3 releasePosition = doorLock.localPosition + Vector3.down * lockReleaseDropDistance;

        _lockSequence = DOTween.Sequence();
        _lockSequence.Append(doorLock.DOLocalMove(releasePosition, duration).SetEase(Ease.InQuad));
        if (_lockSpine != null)
        {
            _lockSequence.Join(DOVirtual.Float(1f, 0f, duration, _lockSpine.SetAlpha).SetEase(Ease.InQuad));
        }

        _lockSequence.SetLink(doorLock.gameObject, LinkBehaviour.KillOnDisable);
        _lockSequence.OnComplete(() =>
        {
            _lockSequence = null;
            doorLock.gameObject.SetActive(false);
            PlayScoreBubbleExit();
            OpenDoorPassage();
        });
    }

    private void OpenDoorPassage()
    {
        _isLockResolved = true;

        if (movableDoorBody != null)
        {
            movableDoorBody.linearVelocity = Vector2.zero;
            movableDoorBody.angularVelocity = 0f;
            movableDoorBody.bodyType = RigidbodyType2D.Kinematic;
            movableDoorBody.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        if (movableDoorCollider != null)
        {
            movableDoorCollider.enabled = true;
            movableDoorCollider.isTrigger = true;
        }
    }

    #endregion
}
