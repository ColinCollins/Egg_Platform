using System;
using Bear.Logger;
using Cysharp.Threading.Tasks;
using Game.Scripts.Common;
using UnityEngine;

/// <summary>
/// DLevel014 的纯物理投篮与进筐控制器。
/// 第一笔有效画线完成后只施加一次冲量；之后的飞行、撞线、撞框和下落全部交给 Physics2D。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 14 Ctrl")]
public sealed class DLevel14Ctrl : MonoBehaviour, IDrawStrokeCompletedHandler, IDebuger
{
    private const float CrossingEpsilon = 0.0001f;
    private const float FailAreaFailureDelaySeconds = 0.2f;
    private const float GizmoLineLength = 1.5f;
    private const float GizmoArrowHeadLength = 0.35f;
    private const float GizmoArrowHeadAngle = 30f;

    [Header("Basketball Launch")]
    [SerializeField] private Rigidbody2D basketballRigidbody;
    [SerializeField] private CircleCollider2D basketballCollider;
    [SerializeField] private SpriteRenderer basketballRenderer;
    [Tooltip("首笔完成后篮球冲量的世界坐标方向；运行时会归一化。")]
    [SerializeField] private Vector2 launchDirection = new(1f, 0.75f);
    [Tooltip("首笔完成后施加给篮球的一次性 Impulse。")]
    [SerializeField, Min(0f)] private float launchImpulse = 14f;
    [Tooltip("篮球发射时的一次性旋转冲量；负值为顺时针。")]
    [SerializeField] private float spinImpulse = -0.35f;

    [Header("Basket Success")]
    [SerializeField] private DrawLevelCtrl drawLevelCtrl;
    [SerializeField] private BoxCollider2D leftRimCollider;
    [SerializeField] private BoxCollider2D rightRimCollider;
    [Tooltip("本关 FallArea 的 BoxCollider2D；篮球没有 ActorCtrl，因此由本控制器直接判断。")]
    [SerializeField] private BoxCollider2D failAreaCollider;
    [SerializeField, Min(0f)] private float openingHorizontalTolerance = 0.04f;
    [SerializeField, Min(0f)] private float minimumDownwardSpeed = 0.1f;
    [SerializeField, Min(0f)] private float sideEscapePadding = 0.2f;
    [Tooltip("进筐锁定后继续保留篮球物理，等待该时长再开始渐隐。")]
    [SerializeField, Min(0f)] private float successFadeDelay = 0.35f;
    [SerializeField, Min(0.01f)] private float successFadeDuration = 0.4f;
    [SerializeField] private GameObject successFireworkPrefab;

    private Vector3 _initialLocalPosition;
    private Quaternion _initialLocalRotation;
    private Color _initialBallColor = Color.white;
    private Vector2 _previousBallCenter;
    private bool _hasCachedInitialState;
    private bool _hasPreviousBallCenter;
    private bool _enteredFromAbove;
    private bool _hasLaunched;
    private bool _successTriggered;
    private bool _failureTriggered;
    private bool _ownsSuccessPerformance;
    private bool _isPauseApplied;
    private bool _resumeFireworkAfterPause;
    private int _attemptVersion;
    private GameObject _successFireworkInstance;

    private void Awake()
    {
        if (!Application.isPlaying)
            return;

        ResolveReferences();
        CacheInitialState();
        RestoreInitialState();
    }

    private void OnDisable()
    {
        _attemptVersion++;
        CleanupSuccessFirework();
        if (_ownsSuccessPerformance && drawLevelCtrl != null)
            drawLevelCtrl.CancelSuccessPerformance();

        _ownsSuccessPerformance = false;
    }

    private void FixedUpdate()
    {
        ApplyPauseState();
        if (drawLevelCtrl != null && drawLevelCtrl.IsPaused)
            return;

        if (_successTriggered || _failureTriggered)
            return;

        if (TryTriggerBallFailure())
            return;

        if (!CanTrackBall())
        {
            ResetTrackingSample();
            return;
        }

        Vector2 currentBallCenter = basketballCollider.bounds.center;
        if (!_hasPreviousBallCenter)
        {
            _previousBallCenter = currentBallCenter;
            _hasPreviousBallCenter = true;
            return;
        }

        if (TryGetBasketGeometry(
                out float openingMinimumX,
                out float openingMaximumX,
                out float upperPlaneY,
                out float lowerPlaneY))
        {
            EvaluateBasketCrossing(
                _previousBallCenter,
                currentBallCenter,
                openingMinimumX,
                openingMaximumX,
                upperPlaneY,
                lowerPlaneY);
        }

        _previousBallCenter = currentBallCenter;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = basketballRigidbody != null
            ? basketballRigidbody.worldCenterOfMass
            : transform.position;
        Vector2 normalizedDirection = launchDirection.normalized;
        if (normalizedDirection == Vector2.zero)
            return;

        Vector3 direction = normalizedDirection;
        Vector3 end = origin + direction * GizmoLineLength;
        Vector3 firstArrowHead =
            Quaternion.Euler(0f, 0f, GizmoArrowHeadAngle) * -direction;
        Vector3 secondArrowHead =
            Quaternion.Euler(0f, 0f, -GizmoArrowHeadAngle) * -direction;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, end);
        Gizmos.DrawLine(end, end + firstArrowHead * GizmoArrowHeadLength);
        Gizmos.DrawLine(end, end + secondArrowHead * GizmoArrowHeadLength);
    }

    public void OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        if (!Application.isPlaying || !result.IsFirstStroke || _hasLaunched || _successTriggered)
            return;

        ResolveReferences();
        Vector2 normalizedDirection = launchDirection.normalized;
        float impulse = Mathf.Max(0f, launchImpulse);
        if (basketballRigidbody == null || normalizedDirection == Vector2.zero || impulse <= 0f)
        {
            Debug.LogError("[DLevel14Ctrl] 篮球、发射方向或冲量配置无效，无法发射。", this);
            return;
        }

        _hasLaunched = true;
        basketballRigidbody.bodyType = RigidbodyType2D.Dynamic;
        basketballRigidbody.linearVelocity = Vector2.zero;
        basketballRigidbody.angularVelocity = 0f;
        basketballRigidbody.AddForce(normalizedDirection * impulse, ForceMode2D.Impulse);
        if (!Mathf.Approximately(spinImpulse, 0f))
            basketballRigidbody.AddTorque(spinImpulse, ForceMode2D.Impulse);

        basketballRigidbody.WakeUp();
    }

    public void OnDrawAttemptReset()
    {
        _attemptVersion++;
        if (_ownsSuccessPerformance && drawLevelCtrl != null)
            drawLevelCtrl.CancelSuccessPerformance();

        _ownsSuccessPerformance = false;
        CleanupSuccessFirework();
        RestoreInitialState();
    }

    private bool CanTrackBall()
    {
        return _hasLaunched &&
               drawLevelCtrl != null &&
               basketballRigidbody != null &&
               basketballCollider != null &&
               basketballCollider.enabled &&
               basketballRigidbody.simulated &&
               basketballRigidbody.bodyType == RigidbodyType2D.Dynamic;
    }

    private bool TryTriggerBallFailure()
    {
        if (!_hasLaunched ||
            drawLevelCtrl == null ||
            drawLevelCtrl.IsOutcomeLocked ||
            basketballRigidbody == null ||
            basketballCollider == null ||
            failAreaCollider == null ||
            !failAreaCollider.enabled ||
            basketballRigidbody.bodyType != RigidbodyType2D.Dynamic)
        {
            return false;
        }

        Bounds failBounds = failAreaCollider.bounds;
        Vector2 ballCenter = basketballCollider.bounds.center;
        bool isInsideFailArea =
            ballCenter.x >= failBounds.min.x &&
            ballCenter.x <= failBounds.max.x &&
            ballCenter.y >= failBounds.min.y &&
            ballCenter.y <= failBounds.max.y;
        if (!isInsideFailArea)
            return false;

        _failureTriggered = true;
        AudioManager.PlaySound("failed");
        WaitForBallFailureAsync(_attemptVersion).Forget();
        return true;
    }

    private async UniTask WaitForBallFailureAsync(int attemptVersion)
    {
        try
        {
            await UniTask.WaitForSeconds(
                FailAreaFailureDelaySeconds,
                cancellationToken: this.GetCancellationTokenOnDestroy());
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (attemptVersion != _attemptVersion ||
            !_failureTriggered ||
            drawLevelCtrl == null)
        {
            return;
        }

        drawLevelCtrl.TryFailAndReset(nameof(DLevel14Ctrl));
    }

    private bool TryGetBasketGeometry(
        out float openingMinimumX,
        out float openingMaximumX,
        out float upperPlaneY,
        out float lowerPlaneY)
    {
        openingMinimumX = 0f;
        openingMaximumX = 0f;
        upperPlaneY = 0f;
        lowerPlaneY = 0f;

        if (leftRimCollider == null ||
            rightRimCollider == null ||
            !leftRimCollider.enabled ||
            !rightRimCollider.enabled)
        {
            return false;
        }

        Bounds firstBounds = leftRimCollider.bounds;
        Bounds secondBounds = rightRimCollider.bounds;
        Bounds leftBounds = firstBounds.center.x <= secondBounds.center.x
            ? firstBounds
            : secondBounds;
        Bounds rightBounds = firstBounds.center.x <= secondBounds.center.x
            ? secondBounds
            : firstBounds;

        openingMinimumX = leftBounds.max.x;
        openingMaximumX = rightBounds.min.x;
        upperPlaneY = Mathf.Max(leftBounds.max.y, rightBounds.max.y);
        lowerPlaneY = Mathf.Min(leftBounds.min.y, rightBounds.min.y);
        return openingMaximumX > openingMinimumX && upperPlaneY > lowerPlaneY;
    }

    private void EvaluateBasketCrossing(
        Vector2 previousCenter,
        Vector2 currentCenter,
        float openingMinimumX,
        float openingMaximumX,
        float upperPlaneY,
        float lowerPlaneY)
    {
        float downwardSpeed = Mathf.Max(
            0f,
            (previousCenter.y - currentCenter.y) /
            Mathf.Max(Time.fixedDeltaTime, CrossingEpsilon));

        if (!_enteredFromAbove &&
            downwardSpeed >= minimumDownwardSpeed &&
            TryGetDownwardCrossingX(previousCenter, currentCenter, upperPlaneY, out float upperCrossingX) &&
            IsInsideOpening(upperCrossingX, openingMinimumX, openingMaximumX))
        {
            _enteredFromAbove = true;
        }

        if (_enteredFromAbove &&
            downwardSpeed >= minimumDownwardSpeed &&
            TryGetDownwardCrossingX(previousCenter, currentCenter, lowerPlaneY, out float lowerCrossingX) &&
            IsInsideOpening(lowerCrossingX, openingMinimumX, openingMaximumX))
        {
            TryCommitBasketSuccess(new Vector2(lowerCrossingX, lowerPlaneY));
            return;
        }

        if (!_enteredFromAbove)
            return;

        bool escapedSideways =
            currentCenter.x < openingMinimumX - sideEscapePadding ||
            currentCenter.x > openingMaximumX + sideEscapePadding;
        bool escapedUpward = currentCenter.y > upperPlaneY + CrossingEpsilon;
        bool missedBelow = currentCenter.y < lowerPlaneY - CrossingEpsilon;
        if (escapedSideways || escapedUpward || missedBelow)
            _enteredFromAbove = false;
    }

    private bool IsInsideOpening(
        float worldX,
        float openingMinimumX,
        float openingMaximumX)
    {
        return worldX >= openingMinimumX - openingHorizontalTolerance &&
               worldX <= openingMaximumX + openingHorizontalTolerance;
    }

    private static bool TryGetDownwardCrossingX(
        Vector2 previousCenter,
        Vector2 currentCenter,
        float planeY,
        out float crossingX)
    {
        crossingX = 0f;
        float verticalDelta = previousCenter.y - currentCenter.y;
        if (verticalDelta <= CrossingEpsilon ||
            previousCenter.y <= planeY ||
            currentCenter.y > planeY)
        {
            return false;
        }

        float interpolation = Mathf.Clamp01((previousCenter.y - planeY) / verticalDelta);
        crossingX = Mathf.Lerp(previousCenter.x, currentCenter.x, interpolation);
        return true;
    }

    private void TryCommitBasketSuccess(Vector2 scorePoint)
    {
        if (drawLevelCtrl == null || !drawLevelCtrl.TryBeginSuccessPerformance())
            return;

        _successTriggered = true;
        _ownsSuccessPerformance = true;
        PlaySuccessFirework(scorePoint);
        PlaySuccessAsync(_attemptVersion).Forget();
    }

    private async UniTask PlaySuccessAsync(int attemptVersion)
    {
        try
        {
            if (!await WaitForGameplaySecondsAsync(successFadeDelay, attemptVersion))
                return;

            if (!IsCurrentSuccess(attemptVersion))
                return;

            await FadeBasketballOutAsync(attemptVersion);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!IsCurrentSuccess(attemptVersion))
            return;

        _ownsSuccessPerformance = false;
        drawLevelCtrl.CompleteSuccessPerformance(nameof(DLevel14Ctrl));
    }

    private async UniTask<bool> WaitForGameplaySecondsAsync(
        float seconds,
        int attemptVersion)
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0f, seconds);
        while (elapsed < duration)
        {
            if (!IsCurrentSuccess(attemptVersion))
                return false;

            if (drawLevelCtrl != null && !drawLevelCtrl.IsPaused)
                elapsed += Time.deltaTime;

            await UniTask.Yield(
                PlayerLoopTiming.Update,
                this.GetCancellationTokenOnDestroy());
        }

        return IsCurrentSuccess(attemptVersion);
    }

    private async UniTask FadeBasketballOutAsync(int attemptVersion)
    {
        float duration = Mathf.Max(0.01f, successFadeDuration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!IsCurrentSuccess(attemptVersion))
                return;

            if (drawLevelCtrl != null && !drawLevelCtrl.IsPaused)
            {
                elapsed += Time.deltaTime;
                SetBasketballAlpha(1f - Mathf.Clamp01(elapsed / duration));
            }

            await UniTask.Yield(
                PlayerLoopTiming.Update,
                this.GetCancellationTokenOnDestroy());
        }

        if (IsCurrentSuccess(attemptVersion))
            SetBasketballAlpha(0f);
    }

    private bool IsCurrentSuccess(int attemptVersion)
    {
        return attemptVersion == _attemptVersion &&
               _successTriggered &&
               _ownsSuccessPerformance &&
               drawLevelCtrl != null;
    }

    private void PlaySuccessFirework(Vector2 scorePoint)
    {
        CleanupSuccessFirework();
        if (successFireworkPrefab == null)
        {
            Debug.LogWarning("[DLevel14Ctrl] 未绑定进筐烟花 Prefab，成功结算将继续但不播放烟花。", this);
            return;
        }

        _successFireworkInstance = Instantiate(successFireworkPrefab, transform);
        _successFireworkInstance.transform.position =
            new Vector3(scorePoint.x, scorePoint.y, transform.position.z);

        ParticleSystem rootParticleSystem =
            _successFireworkInstance.GetComponentInChildren<ParticleSystem>(true);
        if (rootParticleSystem == null)
        {
            Debug.LogError("[DLevel14Ctrl] 进筐烟花 Prefab 内未找到 ParticleSystem。", this);
            return;
        }

        rootParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        rootParticleSystem.Play(true);
        _resumeFireworkAfterPause = false;
    }

    private void CleanupSuccessFirework()
    {
        if (_successFireworkInstance == null)
            return;

        ParticleSystem rootParticleSystem =
            _successFireworkInstance.GetComponentInChildren<ParticleSystem>(true);
        rootParticleSystem?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        Destroy(_successFireworkInstance);
        _successFireworkInstance = null;
        _resumeFireworkAfterPause = false;
    }

    private void ApplyPauseState()
    {
        bool shouldPause = drawLevelCtrl != null && drawLevelCtrl.IsPaused;
        if (_isPauseApplied == shouldPause)
            return;

        _isPauseApplied = shouldPause;
        if (basketballRigidbody != null && _hasLaunched)
            basketballRigidbody.simulated = !shouldPause;

        if (_successFireworkInstance == null)
            return;

        ParticleSystem rootParticleSystem =
            _successFireworkInstance.GetComponentInChildren<ParticleSystem>(true);
        if (rootParticleSystem == null)
            return;

        if (shouldPause)
        {
            _resumeFireworkAfterPause = rootParticleSystem.isPlaying;
            if (_resumeFireworkAfterPause)
                rootParticleSystem.Pause(true);
            return;
        }

        if (_resumeFireworkAfterPause)
            rootParticleSystem.Play(true);
        _resumeFireworkAfterPause = false;
    }

    private void CacheInitialState()
    {
        if (_hasCachedInitialState || basketballRigidbody == null)
            return;

        _initialLocalPosition = basketballRigidbody.transform.localPosition;
        _initialLocalRotation = basketballRigidbody.transform.localRotation;
        if (basketballRenderer != null)
            _initialBallColor = basketballRenderer.color;

        _hasCachedInitialState = true;
    }

    private void RestoreInitialState()
    {
        _hasLaunched = false;
        _successTriggered = false;
        _failureTriggered = false;
        _isPauseApplied = false;
        _resumeFireworkAfterPause = false;
        ResetTrackingSample();
        SetBasketballAlpha(1f);

        if (basketballRigidbody == null)
            return;

        basketballRigidbody.bodyType = RigidbodyType2D.Kinematic;
        basketballRigidbody.simulated = true;
        basketballRigidbody.linearVelocity = Vector2.zero;
        basketballRigidbody.angularVelocity = 0f;
        if (!_hasCachedInitialState)
            return;

        basketballRigidbody.transform.localPosition = _initialLocalPosition;
        basketballRigidbody.transform.localRotation = _initialLocalRotation;
        if (basketballRenderer != null)
            basketballRenderer.color = _initialBallColor;
    }

    private void SetBasketballAlpha(float alpha)
    {
        if (basketballRenderer == null)
            return;

        Color color = _initialBallColor;
        color.a *= Mathf.Clamp01(alpha);
        basketballRenderer.color = color;
    }

    private void ResetTrackingSample()
    {
        _hasPreviousBallCenter = false;
        _enteredFromAbove = false;
        _previousBallCenter = default;
    }

    private void ResolveReferences()
    {
        if (drawLevelCtrl == null)
            drawLevelCtrl = GetComponent<DrawLevelCtrl>();

        Transform basketball = transform.Find("Sp_prop_1_02");
        if (basketballRigidbody == null)
            basketballRigidbody = basketball?.GetComponent<Rigidbody2D>();
        if (basketballCollider == null)
            basketballCollider = basketball?.GetComponent<CircleCollider2D>();
        if (basketballRenderer == null)
            basketballRenderer = basketball?.GetComponent<SpriteRenderer>();

        Transform rim = transform.Find("Sp_hx_lqk_01");
        if ((leftRimCollider == null || rightRimCollider == null) && rim != null)
        {
            BoxCollider2D[] rimColliders = rim.GetComponents<BoxCollider2D>();
            if (rimColliders.Length >= 2)
            {
                leftRimCollider = rimColliders[0];
                rightRimCollider = rimColliders[1];
            }
        }

        if (failAreaCollider == null)
        {
            BoxCollider2D[] colliders = GetComponentsInChildren<BoxCollider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i].name.StartsWith("FallArea", StringComparison.Ordinal))
                {
                    failAreaCollider = colliders[i];
                    break;
                }
            }
        }
    }
}
