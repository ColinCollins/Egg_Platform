using System;
using System.Collections.Generic;

/// <summary>Level132 私有跑绳节奏：同一加速模型驱动真实里程和前方组合的可翻身净空；不依赖 Unity 生命周期。</summary>
internal sealed class Level132RunCourse
{
    #region 跑动速度与距离
    private readonly double _startSpeed;
    private readonly double _maximumSpeed;
    private readonly double _accelerationSeconds;
    private readonly double _acceleration;
    private readonly double _accelerationDistance;
    private readonly double _switchSeconds;
    private readonly double _switchEnvelope;

    /// <summary>供身后绳段回收使用的速度上限，保守保留至少四秒的下坠画面。</summary>
    public float MaximumSpeed => (float)_maximumSpeed;

    /// <summary>每次关卡实例创建一次；翻身包络取当前 Actor，随机种子只供本地回归复现。</summary>
    public Level132RunCourse(float startSpeed, float maximumSpeed, float accelerationSeconds,
        float switchSeconds, float switchEnvelope, int? seed = null)
    {
        _startSpeed = Math.Max(0.1, startSpeed);
        _maximumSpeed = Math.Max(_startSpeed, maximumSpeed);
        _accelerationSeconds = Math.Max(1, accelerationSeconds);
        _acceleration = (_maximumSpeed - _startSpeed) / _accelerationSeconds;
        _accelerationDistance = (_startSpeed + _maximumSpeed) * 0.5 * _accelerationSeconds;
        _switchSeconds = Math.Max(0.05, switchSeconds);
        _switchEnvelope = Math.Max(0.01, switchEnvelope);
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>输入仅为实际跑绳秒数；暂停、准备与弹射不累计，达到上限后保持匀速。</summary>
    public double SpeedAtTime(double seconds) => _startSpeed + _acceleration * Math.Min(_accelerationSeconds, Math.Max(0, seconds));

    /// <summary>解析积分保证不同帧率累计同一真实距离；负时间只供身后初始绳段的下坠外推。</summary>
    public double DistanceAtTime(double seconds)
    {
        if (seconds < 0) return _startSpeed * seconds;
        double accelerating = Math.Min(seconds, _accelerationSeconds);
        return _startSpeed * accelerating + 0.5 * _acceleration * accelerating * accelerating +
            _maximumSpeed * Math.Max(0, seconds - _accelerationSeconds);
    }

    /// <summary>由路线位置预测到达时间，提前生成也不提前提高难度；负距离沿初速度外推。</summary>
    public double TimeAtDistance(double meters)
    {
        if (meters <= 0 || _acceleration <= 0) return meters / _startSpeed;
        if (meters >= _accelerationDistance)
            return _accelerationSeconds + (meters - _accelerationDistance) / _maximumSpeed;
        return 2 * meters / (_startSpeed + Math.Sqrt(_startSpeed * _startSpeed + 2 * _acceleration * meters));
    }
    #endregion

    #region 同侧连块与组合推进
    /// <summary>一块原尺寸石刺的相对起跑点左边缘与上下侧；不拉伸素材、不改碰撞。</summary>
    public readonly struct Obstacle
    {
        public readonly float Left;
        public readonly bool Upper;
        public Obstacle(float left, bool upper) { Left = left; Upper = upper; }
    }

    private static readonly int[] Opening = { 1, 1 };
    private static readonly int[][] Early = { new[] { 2, 1 }, new[] { 1, 2 }, new[] { 2 } };
    private static readonly int[][] Middle =
    {
        new[] { 3, 1 }, new[] { 1, 3 }, new[] { 2, 2 }, new[] { 1, 2, 1 }, new[] { 3 }
    };
    private static readonly int[][] Late =
    {
        new[] { 4, 1 }, new[] { 2, 3 }, new[] { 3, 2, 1 },
        new[] { 1, 3, 1 }, new[] { 2, 2 }, new[] { 1, 1, 2 }, new[] { 2, 4 }
    };
    private readonly Random _random;
    private readonly List<Obstacle> _group = new List<Obstacle>(6);
    private double _lastRight;
    private bool _lastUpper;
    private int _lastRunBlocks;
    private int _lastStage = -1;
    private int _lastPattern = -1;
    private bool _hasObstacle;

    /// <summary>最近规划的整组；调用者读取并放置后才请求下一组，列表会复用。</summary>
    public IReadOnlyList<Obstacle> Group => _group;
    /// <summary>已规划的组数，仅供障碍实例命名和前三组教学判断。</summary>
    public int GroupNumber { get; private set; }
    /// <summary>下一组最早可能出现的左边缘，用于保留原前方流式补齐边界。</summary>
    public float NextLeft => _hasObstacle ? (float)(_lastRight + 0.12) : 5f;

    /// <summary>前三组上下单块教学，后续按预计到达时间解锁两/三/四连；整组不得侵入终点前十二米。</summary>
    public bool TryPlanNext(float obstacleWidth, float goal)
    {
        if (obstacleWidth <= 0) throw new ArgumentOutOfRangeException(nameof(obstacleWidth));
        _group.Clear();
        bool opening = GroupNumber < 3;
        double arrival = TimeAtDistance(NextLeft);
        int[] pattern = Opening;
        if (!opening)
        {
            int stage = arrival < 18 ? 0 : arrival < 30 ? 1 : 2;
            int[][] choices = stage == 0 ? Early : stage == 1 ? Middle : Late;
            int choice = _random.Next(choices.Length);
            if (stage == _lastStage && choice == _lastPattern)
                choice = (choice + 1 + _random.Next(choices.Length - 1)) % choices.Length;
            pattern = choices[choice];
            _lastStage = stage;
            _lastPattern = choice;
        }

        bool upper = opening || _random.Next(2) == 0;
        if (!opening && _hasObstacle && upper == _lastUpper && _lastRunBlocks + pattern[0] > 4)
            upper = !upper;
        double right = _lastRight;
        bool previousUpper = _lastUpper;
        bool hasPrevious = _hasObstacle;
        int runBlocks = _lastRunBlocks;
        for (int run = 0; run < pattern.Length; run++)
        {
            for (int block = 0; block < pattern[run]; block++)
            {
                bool firstInGroup = run == 0 && block == 0;
                double left = !hasPrevious ? 5 : upper != previousUpper
                    ? NextSideLeft(right, opening, firstInGroup)
                    : right + (firstInGroup ? Math.Max(0.6, SpeedAtTime(TimeAtDistance(right)) * 0.2) : 0.12);
                // 与最终 Transform 同精度，再向前保留少量距离，避免万米处浮点舍入压缩安全窗口。
                float placedLeft = (float)left + 0.005f;
                right = placedLeft + obstacleWidth;
                if (right > goal - 12f) { _group.Clear(); return false; }
                _group.Add(new Obstacle(placedLeft, upper));
                runBlocks = hasPrevious && upper == previousUpper ? runBlocks + 1 : 1;
                previousUpper = upper;
                hasPrevious = true;
            }
            upper = !upper;
        }
        _lastRight = right;
        _lastUpper = previousUpper;
        _lastRunBlocks = runBlocks;
        _hasObstacle = true;
        GroupNumber++;
        return true;
    }

    private double NextSideLeft(double previousRight, bool opening, bool groupBoundary)
    {
        double start = TimeAtDistance(previousRight + _switchEnvelope * 0.5);
        double progress = Math.Min(1, Math.Max(0, start / _accelerationSeconds));
        double reaction = opening ? 0.55 : 0.42 + (0.18 - 0.42) * progress;
        if (groupBoundary) reaction += 0.12;
        // 让整个翻身轮廓先离开上一块，再完成动作；动作之外仍保留反应余量。
        return DistanceAtTime(start + _switchSeconds + reaction) + _switchEnvelope * 0.5;
    }
    #endregion
}
