using UnityEngine;

/// <summary>
/// DLevel013 的弹性球与篮筐控制器。
/// 复用 DLevel002 的猫夹球入场/首笔释放，并以篮筐左右实体碰撞体推导进筐平面。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Game/Draw/Level 13 Ctrl")]
public sealed class DLevel13Ctrl : DLevel2Ctrl, IDrawStrokeCompletedHandler
{
    #region Constants

    private const float CrossingEpsilon = 0.0001f;

    #endregion

    #region Inspector

    [Header("Basket Success")]
    [SerializeField] private DrawLevelCtrl basketDrawLevelCtrl;
    [SerializeField] private Rigidbody2D basketBallRigidbody;
    [SerializeField] private CircleCollider2D basketBallCollider;
    [Tooltip("篮筐左侧的实体 BoxCollider2D。")]
    [SerializeField] private BoxCollider2D leftRimCollider;
    [Tooltip("篮筐右侧的实体 BoxCollider2D。")]
    [SerializeField] private BoxCollider2D rightRimCollider;
    [Tooltip("进筐平面允许的水平误差；实体篮沿仍负责真实挡球。")]
    [SerializeField, Min(0f)] private float openingHorizontalTolerance = 0.04f;
    [Tooltip("只有向下穿过篮筐才能成功，用于拒绝下方反弹误触。")]
    [SerializeField, Min(0f)] private float minimumDownwardSpeed = 0.1f;
    [Tooltip("球从两侧离开篮筐后取消本次“从上方进入”候选。")]
    [SerializeField, Min(0f)] private float sideEscapePadding = 0.2f;
    [Tooltip("成功占用后继续保留球物理，等待该时长再开始人物与球渐隐。")]
    [SerializeField, Min(0f)] private float successFadeDelay = 0.35f;

    [Header("Basket Success Firework")]
    [Tooltip("复用常规成功门彩纸烟花配置的共享 Prefab；只在进筐成功被正式占用后实例化。")]
    [SerializeField] private GameObject successFireworkPrefab;

    #endregion

    #region Runtime State

    private bool _hasPreviousBallCenter;
    private bool _enteredFromAbove;
    private bool _successTriggered;
    private Vector2 _previousBallCenter;
    private GameObject _successFireworkInstance;

    #endregion

    #region Unity Lifecycle

    private void FixedUpdate()
    {
        if (_successTriggered)
            return;

        if (!CanTrackBall())
        {
            ResetTrackingSample();
            return;
        }

        Vector2 currentBallCenter = basketBallCollider.bounds.center;
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

    protected override void OnDisable()
    {
        CleanupSuccessFirework();
        base.OnDisable();
    }

    #endregion

    #region Draw Lifecycle

    void IDrawStrokeCompletedHandler.OnDrawStrokeCompleted(DrawStrokeResult result)
    {
        base.OnDrawStrokeCompleted(result);
    }

    void IDrawStrokeCompletedHandler.OnDrawAttemptReset()
    {
        base.OnDrawAttemptReset();
        ResetBasketState();
    }

    #endregion

    #region Basket Detection

    private bool CanTrackBall()
    {
        return basketDrawLevelCtrl != null &&
               basketBallRigidbody != null &&
               basketBallCollider != null &&
               basketBallCollider.enabled &&
               basketBallRigidbody.simulated &&
               basketBallRigidbody.bodyType == RigidbodyType2D.Dynamic;
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
            (previousCenter.y - currentCenter.y) / Mathf.Max(Time.fixedDeltaTime, CrossingEpsilon));

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
        if (!basketDrawLevelCtrl.TryTriggerSuccessWithoutDoor(
                basketBallCollider,
                nameof(DLevel13Ctrl),
                successFadeDelay))
        {
            return;
        }

        _successTriggered = true;
        PlaySuccessFirework(scorePoint);
    }

    private void PlaySuccessFirework(Vector2 scorePoint)
    {
        CleanupSuccessFirework();
        if (successFireworkPrefab == null)
        {
            Debug.LogWarning("[DLevel13Ctrl] 未绑定进筐烟花 Prefab，成功结算将继续但不播放烟花。", this);
            return;
        }

        _successFireworkInstance = Instantiate(successFireworkPrefab, transform);
        _successFireworkInstance.transform.position =
            new Vector3(scorePoint.x, scorePoint.y, transform.position.z);

        ParticleSystem rootParticleSystem =
            _successFireworkInstance.GetComponentInChildren<ParticleSystem>(true);
        if (rootParticleSystem == null)
        {
            Debug.LogError("[DLevel13Ctrl] 进筐烟花 Prefab 内未找到 ParticleSystem。", this);
            return;
        }

        rootParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        rootParticleSystem.Play(true);
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
    }

    private void ResetTrackingSample()
    {
        _hasPreviousBallCenter = false;
        _enteredFromAbove = false;
        _previousBallCenter = default;
    }

    private void ResetBasketState()
    {
        CleanupSuccessFirework();
        _successTriggered = false;
        ResetTrackingSample();
    }

    #endregion
}
