using UnityEngine;

public enum Level116ObstacleDirection
{
    Down = 0,
    Up = 1,
}

public enum Level116ObstacleSpeedTier
{
    S1 = 0,
    S2 = 1,
    S3 = 2,
    S4 = 3,
    S5 = 4,
    S6 = 5,
}

/// <summary>
/// Level 116 单个固定三角障碍点位。
/// 由 Level116Ctrl 统一推进；开局/重置停在屏外行程起点，首次玩家移动后才推进。
/// 挡板不负责启动或对齐。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
public sealed class Level116Obstacle : MonoBehaviour
{
    private const float BottomWorldY = -7f;
    private const float TopWorldY = 7f;
    private const float TravelDistance = TopWorldY - BottomWorldY;
    /// <summary>与 Level116Ctrl 一致：现行合同 Scale=0.7。</summary>
    private const float ObstacleLocalScale = 0.7f;

    [SerializeField] private Level116ObstacleDirection direction;
    [SerializeField] private Level116ObstacleSpeedTier speedTier;
    [SerializeField] private float initialDistance;
    [SerializeField] private float worldSpeedOverride;
    [SerializeField] private bool hasWorldSpeedOverride;

    private Level116Ctrl _owner;
    private BoxCollider2D _triggerCollider;
    private float _distanceFromStart;
    private float _fixedWorldX;
    private float _fixedWorldZ;
    private bool _isBound;

    public Level116ObstacleDirection Direction => direction;

    public Level116ObstacleSpeedTier SpeedTier => speedTier;

    /// <summary>
    /// 配置的行程起点相位（沿 14u 行程的距离，[0,14)）。
    /// 当前 Level 116 合同固定为 0：开局/重置停在各自方向的屏外行程起点。
    /// </summary>
    public float InitialDistance => initialDistance;

    public float WorldSpeed
    {
        get
        {
            if (hasWorldSpeedOverride && worldSpeedOverride > 0f)
                return worldSpeedOverride;

            switch (speedTier)
            {
                case Level116ObstacleSpeedTier.S1:
                    return 3.5f;
                case Level116ObstacleSpeedTier.S2:
                    return 7.5f;
                case Level116ObstacleSpeedTier.S3:
                    return 10f;
                case Level116ObstacleSpeedTier.S4:
                    return 21.2f;
                case Level116ObstacleSpeedTier.S5:
                    return 25.2f;
                case Level116ObstacleSpeedTier.S6:
                    return 40f;
                default:
                    return 3.5f;
            }
        }
    }

    internal void Configure(
        Level116ObstacleDirection configuredDirection,
        Level116ObstacleSpeedTier configuredSpeedTier,
        float configuredInitialDistance,
        float configuredWorldSpeedOverride = -1f)
    {
        direction = configuredDirection;
        speedTier = configuredSpeedTier;
        initialDistance = NormalizeDistance(configuredInitialDistance);
        hasWorldSpeedOverride = configuredWorldSpeedOverride > 0f;
        worldSpeedOverride = hasWorldSpeedOverride ? configuredWorldSpeedOverride : 0f;
    }

    internal void Bind(Level116Ctrl owner)
    {
        _owner = owner;
        _triggerCollider = GetComponent<BoxCollider2D>();
        if (!_isBound)
        {
            Vector3 worldPosition = transform.position;
            _fixedWorldX = worldPosition.x;
            _fixedWorldZ = worldPosition.z;
            _isBound = true;
        }

        ResetToInitial();
    }

    /// <summary>
    /// 回到该点位配置的行程起点相位。
    /// 当前合同 initialDistance=0，因此停在各自方向的屏外行程起点并等待 Ctrl 启动推进。
    /// </summary>
    internal void ResetToInitial()
    {
        if (!_isBound)
        {
            Vector3 worldPosition = transform.position;
            _fixedWorldX = worldPosition.x;
            _fixedWorldZ = worldPosition.z;
            _isBound = true;
        }

        _distanceFromStart = NormalizeDistance(initialDistance);
        transform.localScale = Vector3.one * ObstacleLocalScale;
        transform.localRotation = direction == Level116ObstacleDirection.Up
            ? Quaternion.Euler(0f, 0f, 180f)
            : Quaternion.identity;
        ApplyDistanceToTransform();
    }

    internal void Advance(float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        _distanceFromStart = NormalizeDistance(
            _distanceFromStart + WorldSpeed * deltaTime);
        ApplyDistanceToTransform();
    }

    private void ApplyDistanceToTransform()
    {
        float worldY = direction == Level116ObstacleDirection.Up
            ? BottomWorldY + _distanceFromStart
            : TopWorldY - _distanceFromStart;
        SetWorldY(worldY);
    }

    private void SetWorldY(float worldY)
    {
        transform.position = new Vector3(_fixedWorldX, worldY, _fixedWorldZ);
    }

    private static float NormalizeDistance(float distance)
    {
        return Mathf.Repeat(distance, TravelDistance);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_triggerCollider == null)
            _triggerCollider = GetComponent<BoxCollider2D>();

        _owner?.TryHandleObstacleEnter(other, _triggerCollider);
    }
}
