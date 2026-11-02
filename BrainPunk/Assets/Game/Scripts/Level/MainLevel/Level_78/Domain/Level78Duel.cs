using System;
using System.Collections.Generic;

namespace Game.Level078
{
    /// <summary>
    /// Level 078 本地随机数边界；生产环境使用 Unity 随机数，测试可注入确定序列。
    /// </summary>
    public interface ILevel78Random
    {
        /// <summary>
        /// 返回左闭右开的随机整数；调用方必须保证上下界有效。
        /// </summary>
        int Range(int minInclusive, int maxExclusive);
    }

    /// <summary>
    /// Level 078 射箭对决阶段；阶段只能通过 <see cref="Level78Duel"/> 的公开行为推进。
    /// </summary>
    public enum Level78Phase
    {
        DemonShooting,
        DemonCleanupAndReturn,
        BoardAdjustment,
        BoxShooting,
        Settled
    }

    /// <summary>
    /// Level 078 射箭对决参与者。
    /// </summary>
    public enum Level78Participant
    {
        Box,
        Demon
    }

    /// <summary>
    /// 小恶魔在开场、射箭与归位流程中的可观察移动状态；具体位移和动画由 Level 078 表现层执行。
    /// </summary>
    public enum Level78DemonMovementState
    {
        WaitingAtHome,
        ApproachingShootingPoint,
        Shooting,
        ReturningHome,
        ReturnedHome
    }

    /// <summary>
    /// 射箭挡板已提交的两个固定点；不表示拖拽过程中的临时坐标。
    /// </summary>
    public enum Level78BoardPosition
    {
        Far,
        NearFullScore
    }

    /// <summary>
    /// Level 078 最终对决结果；第三支 Box 箭提交前保持 None。
    /// </summary>
    public enum Level78DuelResult
    {
        None,
        Victory,
        Failure
    }

    /// <summary>
    /// 一支箭完成后的可观察计分结果；箭序从 0 开始。
    /// </summary>
    public readonly struct Level78ShotResult
    {
        /// <summary>
        /// 创建不可变的单箭计分结果；只由 <see cref="Level78Duel"/> 在有效箭完成时返回。
        /// </summary>
        public Level78ShotResult(Level78Participant participant, int arrowIndex, int score, int total)
        {
            Participant = participant;
            ArrowIndex = arrowIndex;
            Score = score;
            Total = total;
        }

        public Level78Participant Participant { get; }
        public int ArrowIndex { get; }
        public int Score { get; }
        public int Total { get; }
    }

    /// <summary>
    /// 远距蓄力表盘的可标定颜色边界；黄色与红色内部仍分别严格二等分和三等分。
    /// </summary>
    public readonly struct Level78FarScoringProfile
    {
        /// <summary>
        /// 当前素材的初始标定值；Unity Prefab 可根据实际表盘边界覆盖。
        /// </summary>
        public static Level78FarScoringProfile Default => new Level78FarScoringProfile(0.1f, 0.3f);

        /// <summary>
        /// 创建以表盘中心为 0.5、左右对称的远距计分边界。
        /// </summary>
        /// <param name="greenHalfWidth">绿色区域从中心到任一侧边缘的归一化半宽。</param>
        /// <param name="yellowOuterHalfWidth">从中心到任一侧黄色外边缘的归一化半宽。</param>
        public Level78FarScoringProfile(float greenHalfWidth, float yellowOuterHalfWidth)
        {
            if (float.IsNaN(greenHalfWidth) ||
                float.IsInfinity(greenHalfWidth) ||
                greenHalfWidth <= 0f ||
                float.IsNaN(yellowOuterHalfWidth) ||
                float.IsInfinity(yellowOuterHalfWidth) ||
                yellowOuterHalfWidth <= greenHalfWidth ||
                yellowOuterHalfWidth >= 0.5f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(yellowOuterHalfWidth),
                    "Scoring bounds must satisfy 0 < greenHalfWidth < yellowOuterHalfWidth < 0.5.");
            }

            GreenHalfWidth = greenHalfWidth;
            YellowOuterHalfWidth = yellowOuterHalfWidth;
        }

        public float GreenHalfWidth { get; }
        public float YellowOuterHalfWidth { get; }
    }

    /// <summary>
    /// Level 078 挡板单向双固定点吸附规则；动画与输入锁由 Unity 表现层负责。
    /// </summary>
    public static class Level78BoardSnap
    {
        private const float EqualDistanceEpsilon = 0.0001f;

        /// <summary>
        /// 按水平距离选择最近固定点；近似等距时返回本次拖拽开始前的固定点。
        /// </summary>
        /// <param name="releasedX">松手时的挡板水平坐标。</param>
        /// <param name="farX">远距固定点水平坐标。</param>
        /// <param name="nearFullScoreX">近靶满分点水平坐标。</param>
        /// <param name="dragOrigin">本次拖拽开始前已提交的固定点。</param>
        /// <returns>本次吸附应提交的固定点。</returns>
        public static Level78BoardPosition Resolve(
            float releasedX,
            float farX,
            float nearFullScoreX,
            Level78BoardPosition dragOrigin)
        {
            if (dragOrigin == Level78BoardPosition.NearFullScore)
                return Level78BoardPosition.NearFullScore;

            float farDistance = Math.Abs(releasedX - farX);
            float nearDistance = Math.Abs(releasedX - nearFullScoreX);
            if (Math.Abs(farDistance - nearDistance) <= EqualDistanceEpsilon)
                return dragOrigin;

            return farDistance < nearDistance
                ? Level78BoardPosition.Far
                : Level78BoardPosition.NearFullScore;
        }
    }

    /// <summary>
    /// 单支箭相对计分靶 Transform 的固定视觉落点。
    /// </summary>
    public readonly struct Level78ArrowPlacement
    {
        public Level78ArrowPlacement(float offsetX, float offsetY, bool isMiss)
        {
            OffsetX = offsetX;
            OffsetY = offsetY;
            IsMiss = isMiss;
        }

        public float OffsetX { get; }
        public float OffsetY { get; }
        public bool IsMiss { get; }
    }

    /// <summary>
    /// 把已确定分数映射到箭靶素材的真实同心计分区；不参与反向计分。
    /// </summary>
    public static class Level78ArrowLayout
    {
        public const float TargetCenterOffsetX = -0.23f;
        public const float TargetCenterOffsetY = 0.43f;

        private static readonly float[] RingRadii = { 0.27f, 0.46f, 0.63f, 0.80f, 0.97f, 1.12f };
        private static readonly float[] HitAngles = { 18f, 142f, 258f };
        private static readonly float[] MissOffsetX = { -2.20f, -1.10f, 0f };
        private static readonly float[] MissOffsetY = { -2.14f, -2.08f, -2.15f };

        public static Level78ArrowPlacement Resolve(int score, int arrowIndex)
        {
            if (arrowIndex < 0 || arrowIndex >= 3)
                throw new ArgumentOutOfRangeException(nameof(arrowIndex));

            if (score == 0)
            {
                return new Level78ArrowPlacement(
                    MissOffsetX[arrowIndex],
                    MissOffsetY[arrowIndex],
                    true);
            }

            if (score < 5 || score > 10)
                throw new ArgumentOutOfRangeException(nameof(score));

            float radius = RingRadii[10 - score];
            double angleRadians = HitAngles[arrowIndex] * Math.PI / 180d;
            return new Level78ArrowPlacement(
                TargetCenterOffsetX + ((float)Math.Cos(angleRadians) * radius),
                TargetCenterOffsetY + ((float)Math.Sin(angleRadians) * radius),
                false);
        }
    }

    /// <summary>
    /// 把命中箭的最终箭头插入点映射为稳定的局部渲染顺序；数值越大越靠前。
    /// </summary>
    public static class Level78ArrowDepthOrder
    {
        private const int MinimumHitScore = 5;
        private const int MaximumHitScore = 10;
        private const int ArrowCount = 3;

        /// <summary>
        /// 合法命中插入点数量，即最大排序偏移；飞行中的箭以此加一临时抬高到全部留靶箭之上。
        /// </summary>
        public const int MaxSortingOffset = (MaximumHitScore - MinimumHitScore + 1) * ArrowCount;

        /// <summary>
        /// 比较两支命中箭的空间前后关系；正数表示 left 应显示在 right 前方。
        /// 水平位置优先，越靠屏幕右方越靠前；水平位置完全相同时再比较垂直位置，越靠下方越靠前。
        /// </summary>
        public static int Compare(Level78ArrowPlacement left, Level78ArrowPlacement right)
        {
            ValidateHit(left, nameof(left));
            ValidateHit(right, nameof(right));

            if (left.OffsetX > right.OffsetX)
                return 1;
            if (left.OffsetX < right.OffsetX)
                return -1;
            if (left.OffsetY < right.OffsetY)
                return 1;
            if (left.OffsetY > right.OffsetY)
                return -1;
            return 0;
        }

        /// <summary>
        /// 在 Level 078 全部合法命中插入点中计算稳定偏移；最低偏移为 1，保持箭位于靶面前方。
        /// </summary>
        public static int ResolveSortingOffset(Level78ArrowPlacement placement)
        {
            ValidateHit(placement, nameof(placement));

            int sortingOffset = 1;
            for (int score = MinimumHitScore; score <= MaximumHitScore; score++)
            {
                for (int arrowIndex = 0; arrowIndex < ArrowCount; arrowIndex++)
                {
                    Level78ArrowPlacement candidate = Level78ArrowLayout.Resolve(score, arrowIndex);
                    if (Compare(placement, candidate) > 0)
                        sortingOffset++;
                }
            }

            return sortingOffset;
        }

        private static void ValidateHit(Level78ArrowPlacement placement, string parameterName)
        {
            if (placement.IsMiss)
                throw new ArgumentException("Arrow depth order only accepts target hits.", parameterName);
        }
    }

    /// <summary>
    /// Level 078 箭矢飞行时长规则；只根据水平距离和双方共享的水平箭速计算。
    /// </summary>
    public static class Level78ArrowFlightTiming
    {
        /// <summary>
        /// 计算释放点到落点的飞行时长；调用方必须提供不同的水平坐标和有限正速度。
        /// </summary>
        public static float CalculateDuration(float releaseX, float hitX, float horizontalSpeed)
        {
            ValidateFinite(releaseX, nameof(releaseX));
            ValidateFinite(hitX, nameof(hitX));
            ValidateFinite(horizontalSpeed, nameof(horizontalSpeed));
            if (horizontalSpeed <= 0f)
                throw new ArgumentOutOfRangeException(nameof(horizontalSpeed));

            float horizontalDistance = Math.Abs(hitX - releaseX);
            if (horizontalDistance <= float.Epsilon)
                throw new ArgumentOutOfRangeException(nameof(hitX));

            return horizontalDistance / horizontalSpeed;
        }

        private static void ValidateFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    /// <summary>
    /// 箭头轨迹上的二维点；运行时表现层负责补充 Z 与 Sprite 箭头偏移。
    /// </summary>
    public readonly struct Level78ArrowTrajectoryPoint
    {
        public Level78ArrowTrajectoryPoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }
    }

    /// <summary>
    /// 箭头使用的抛物线飞行轨迹；箭身始终跟随弹道切线。
    /// </summary>
    public readonly struct Level78ArrowTrajectory
    {
        private const float ArcHeightRatio = 0.08f;
        private const float MinimumArcHeight = 0.45f;
        private const float MaximumArcHeight = 0.9f;

        private readonly Level78ArrowTrajectoryPoint start;
        private readonly Level78ArrowTrajectoryPoint end;
        private readonly float arcHeight;

        private Level78ArrowTrajectory(
            Level78ArrowTrajectoryPoint start,
            Level78ArrowTrajectoryPoint end,
            float arcHeight)
        {
            this.start = start;
            this.end = end;
            this.arcHeight = arcHeight;
        }

        /// <summary>
        /// 根据释放点和命中点创建先上扬、后沿切线斜插入的弹道。
        /// </summary>
        public static Level78ArrowTrajectory Create(
            Level78ArrowTrajectoryPoint start,
            Level78ArrowTrajectoryPoint end)
        {
            float deltaX = end.X - start.X;
            float deltaY = end.Y - start.Y;
            float distance = (float)Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            if (float.IsNaN(distance) || float.IsInfinity(distance) || distance <= float.Epsilon)
                throw new ArgumentOutOfRangeException(nameof(end));

            float arcHeight = (float)Math.Max(
                MinimumArcHeight,
                Math.Min(MaximumArcHeight, distance * ArcHeightRatio));
            return new Level78ArrowTrajectory(start, end, arcHeight);
        }

        public Level78ArrowTrajectoryPoint EvaluatePosition(float progress)
        {
            ValidateProgress(progress);
            float linearX = start.X + ((end.X - start.X) * progress);
            float linearY = start.Y + ((end.Y - start.Y) * progress);
            float verticalArc = 4f * arcHeight * progress * (1f - progress);
            return new Level78ArrowTrajectoryPoint(
                linearX,
                linearY + verticalArc);
        }

        public float EvaluateTangentAngleDegrees(float progress)
        {
            ValidateProgress(progress);
            float tangentX = end.X - start.X;
            float tangentY = (end.Y - start.Y) + (4f * arcHeight * (1f - (2f * progress)));
            return (float)(Math.Atan2(tangentY, tangentX) * 180d / Math.PI);
        }

        private static void ValidateProgress(float progress)
        {
            if (float.IsNaN(progress) || float.IsInfinity(progress) || progress < 0f || progress > 1f)
                throw new ArgumentOutOfRangeException(nameof(progress));
        }
    }

    /// <summary>
    /// Level 078 射箭对决的本地规则模型；不持有 Unity 对象、动画或 HUD 引用。
    /// </summary>
    public sealed class Level78Duel
    {
        #region Constants

        private static readonly int[][] LegalDemonScoreCombinations =
        {
            new[] { 10, 10, 8 },
            new[] { 10, 9, 9 },
            new[] { 10, 10, 9 }
        };

        #endregion

        #region Fields

        private readonly int[] demonScores;
        private readonly IReadOnlyList<int> readOnlyDemonScores;
        private readonly Level78FarScoringProfile farScoringProfile;
        private int demonShotCount;
        private int demonTotal;
        private bool demonOpeningStarted;
        private bool demonArrowFadeCompleted;
        private bool demonReturnCompleted;
        private bool boxWasAtShootingPointOnDemonReturn;
        private bool requiresBoxExitBeforeEntry;
        private int boxShotCount;
        private int boxTotal;
        private int pendingBoxScore;

        #endregion

        #region Public API

        /// <summary>
        /// 创建一次 Level 078 射箭对决，并立即准备一个等概率选择、随机排序的合法小恶魔三箭组合。
        /// </summary>
        /// <param name="random">仅负责本局合法组合选择与箭序打乱，不得为空。</param>
        public Level78Duel(ILevel78Random random)
            : this(random, Level78FarScoringProfile.Default)
        {
        }

        /// <summary>
        /// 创建一次 Level 078 射箭对决，并使用已按实际表盘标定的远距颜色边界。
        /// </summary>
        /// <param name="random">仅负责本局合法组合选择与箭序打乱，不得为空。</param>
        /// <param name="farScoringProfile">远距绿色半宽与黄色外边界。</param>
        public Level78Duel(ILevel78Random random, Level78FarScoringProfile farScoringProfile)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            this.farScoringProfile = farScoringProfile;
            int combinationIndex = random.Range(0, LegalDemonScoreCombinations.Length);
            demonScores = (int[])LegalDemonScoreCombinations[combinationIndex].Clone();
            Shuffle(demonScores, random);
            readOnlyDemonScores = Array.AsReadOnly(demonScores);
            Phase = Level78Phase.DemonShooting;
            DemonMovementState = Level78DemonMovementState.WaitingAtHome;
            DemonFacingRight = true;
            BoardPosition = Level78BoardPosition.Far;
            Result = Level78DuelResult.None;
        }

        /// <summary>
        /// 当前射箭对决阶段；只读，外部不能跳过阶段门禁。
        /// </summary>
        public Level78Phase Phase { get; private set; }

        /// <summary>
        /// 本局预先准备的小恶魔三箭成绩；表现层必须按顺序消费，不能中途重抽。
        /// </summary>
        public IReadOnlyList<int> DemonScores => readOnlyDemonScores;

        /// <summary>
        /// 小恶魔是否已由玩家首次移动触发离开开场待机位；同一局只允许从 false 变为 true 一次。
        /// </summary>
        public bool DemonOpeningStarted => demonOpeningStarted;

        /// <summary>
        /// 小恶魔当前可观察移动状态；表现层只能通过公开通知推进，不能跳过到位门禁。
        /// </summary>
        public Level78DemonMovementState DemonMovementState { get; private set; }

        /// <summary>
        /// 小恶魔按领域流程要求的最终水平朝向；归位途中向左，到达等待位置后固定向右。
        /// </summary>
        public bool DemonFacingRight { get; private set; }

        /// <summary>
        /// 当前已提交的挡板固定点；Box 射箭阶段开始后保持只读锁定。
        /// </summary>
        public Level78BoardPosition BoardPosition { get; private set; }

        /// <summary>
        /// 当前是否已有一支 Box 箭处于动画、飞行或命中计分事务中。
        /// </summary>
        public bool IsBoxShotBusy { get; private set; }

        /// <summary>
        /// 第三支 Box 箭提交后的胜负结果。
        /// </summary>
        public Level78DuelResult Result { get; private set; }

        /// <summary>
        /// 接受玩家首次移动并启动小恶魔开场动作；重复移动或其他阶段不会重复启动。
        /// </summary>
        /// <returns>是否接受本次首次移动触发。</returns>
        public bool TryStartDemonOpening()
        {
            if (Phase != Level78Phase.DemonShooting ||
                demonOpeningStarted ||
                DemonMovementState != Level78DemonMovementState.WaitingAtHome)
                return false;

            demonOpeningStarted = true;
            DemonMovementState = Level78DemonMovementState.ApproachingShootingPoint;
            DemonFacingRight = true;
            return true;
        }

        /// <summary>
        /// 标记小恶魔已通过正常走路到达射箭位；到位前不能提交第一支箭。
        /// </summary>
        /// <returns>是否接受本次首次到位信号。</returns>
        public bool NotifyDemonApproachCompleted()
        {
            if (Phase != Level78Phase.DemonShooting ||
                DemonMovementState != Level78DemonMovementState.ApproachingShootingPoint)
                return false;

            DemonMovementState = Level78DemonMovementState.Shooting;
            return true;
        }

        /// <summary>
        /// 提交下一支小恶魔箭的预定成绩；只在首次移动已触发且处于小恶魔射箭阶段时接受，第三箭后进入收尾与归位阶段。
        /// </summary>
        /// <param name="result">成功时返回本箭序号、分数和累计总分；失败时为默认值。</param>
        /// <returns>本次是否提交了一支有效小恶魔箭。</returns>
        public bool TryCompleteDemonShot(out Level78ShotResult result)
        {
            if (Phase != Level78Phase.DemonShooting ||
                !demonOpeningStarted ||
                DemonMovementState != Level78DemonMovementState.Shooting ||
                demonShotCount >= demonScores.Length)
            {
                result = default;
                return false;
            }

            int arrowIndex = demonShotCount;
            int score = demonScores[arrowIndex];
            demonShotCount++;
            demonTotal += score;
            result = new Level78ShotResult(Level78Participant.Demon, arrowIndex, score, demonTotal);

            if (demonShotCount == demonScores.Length)
            {
                Phase = Level78Phase.DemonCleanupAndReturn;
                DemonMovementState = Level78DemonMovementState.ReturningHome;
                DemonFacingRight = false;
            }

            return true;
        }

        /// <summary>
        /// 标记三支小恶魔箭均已完成渐隐；只有归位也完成后才开放挡板调整。
        /// </summary>
        /// <returns>是否接受本次首次完成信号。</returns>
        public bool NotifyDemonArrowFadeCompleted()
        {
            if (Phase != Level78Phase.DemonCleanupAndReturn || demonArrowFadeCompleted)
                return false;

            demonArrowFadeCompleted = true;
            TryAdvanceAfterDemonCleanup();
            return true;
        }

        /// <summary>
        /// 标记小恶魔已完成归位，并记录归位时 Box 是否占据射箭点；只有箭组渐隐也完成后才开放挡板调整。
        /// </summary>
        /// <param name="boxIsAtShootingPoint">归位完成瞬间 Box 是否已经位于当前射箭点。</param>
        /// <returns>是否接受本次首次完成信号。</returns>
        public bool NotifyDemonReturnCompleted(bool boxIsAtShootingPoint)
        {
            if (Phase != Level78Phase.DemonCleanupAndReturn || demonReturnCompleted)
                return false;

            demonReturnCompleted = true;
            boxWasAtShootingPointOnDemonReturn = boxIsAtShootingPoint;
            DemonMovementState = Level78DemonMovementState.ReturnedHome;
            DemonFacingRight = true;
            TryAdvanceAfterDemonCleanup();
            return true;
        }

        /// <summary>
        /// 在挡板调整阶段提交远距或近靶满分固定点；近靶满分点提交后不可回退。
        /// </summary>
        /// <param name="position">吸附完成后要提交的固定点。</param>
        /// <returns>是否接受本次固定点变更。</returns>
        public bool TrySetBoardPosition(Level78BoardPosition position)
        {
            if (Phase != Level78Phase.BoardAdjustment ||
                (position != Level78BoardPosition.Far && position != Level78BoardPosition.NearFullScore) ||
                (BoardPosition == Level78BoardPosition.NearFullScore && position != Level78BoardPosition.NearFullScore))
                return false;

            BoardPosition = position;
            return true;
        }

        /// <summary>
        /// 通知 Box 已离开当前射箭点；只用于解除“归位前旧站位不算资格”的重新进入门禁。
        /// </summary>
        public void NotifyBoxExitedShootingPoint()
        {
            if (Phase == Level78Phase.BoardAdjustment)
                requiresBoxExitBeforeEntry = false;
        }

        /// <summary>
        /// 尝试以一次新的就位动作开放 Box 射箭；未完成小恶魔流程或仍需先离开时拒绝。
        /// </summary>
        /// <returns>是否成功进入 Box 射箭阶段。</returns>
        public bool TryEnterBoxShootingPoint()
        {
            if (Phase != Level78Phase.BoardAdjustment || requiresBoxExitBeforeEntry)
                return false;

            Phase = Level78Phase.BoxShooting;
            return true;
        }

        /// <summary>
        /// 尝试锁定本箭蓄力位置并开始 Box 单箭事务；忙碌、阶段错误或输入越界时拒绝。
        /// </summary>
        /// <param name="normalizedPointerPosition">蓄力指针在表盘上的归一化位置，范围为 0 到 1。</param>
        /// <param name="pendingScore">接受时返回本箭已锁定分数；完成前不会写入计分板状态。</param>
        /// <returns>是否接受本次射箭输入。</returns>
        public bool TryStartBoxShot(float normalizedPointerPosition, out int pendingScore)
        {
            if (Phase != Level78Phase.BoxShooting ||
                IsBoxShotBusy ||
                boxShotCount >= 3 ||
                float.IsNaN(normalizedPointerPosition) ||
                float.IsInfinity(normalizedPointerPosition) ||
                normalizedPointerPosition < 0f ||
                normalizedPointerPosition > 1f)
            {
                pendingScore = 0;
                return false;
            }

            pendingBoxScore = BoardPosition == Level78BoardPosition.NearFullScore
                ? 10
                : ResolveFarScore(normalizedPointerPosition, farScoringProfile);
            IsBoxShotBusy = true;
            pendingScore = pendingBoxScore;
            return true;
        }

        /// <summary>
        /// 完成当前 Box 箭并原子提交单箭分与累计总分；没有进行中的箭时拒绝。
        /// </summary>
        /// <param name="result">成功时返回本箭序号、分数与累计总分。</param>
        /// <returns>是否提交了一支有效 Box 箭。</returns>
        public bool TryCompleteBoxShot(out Level78ShotResult result)
        {
            if (Phase != Level78Phase.BoxShooting || !IsBoxShotBusy)
            {
                result = default;
                return false;
            }

            int arrowIndex = boxShotCount;
            boxShotCount++;
            boxTotal += pendingBoxScore;
            result = new Level78ShotResult(Level78Participant.Box, arrowIndex, pendingBoxScore, boxTotal);
            pendingBoxScore = 0;
            IsBoxShotBusy = false;

            if (boxShotCount == 3)
            {
                Result = boxTotal > demonTotal
                    ? Level78DuelResult.Victory
                    : Level78DuelResult.Failure;
                Phase = Level78Phase.Settled;
            }

            return true;
        }

        #endregion

        #region Internal Rules

        private static void Shuffle(int[] values, ILevel78Random random)
        {
            for (int index = values.Length - 1; index > 0; index--)
            {
                int swapIndex = random.Range(0, index + 1);
                int value = values[index];
                values[index] = values[swapIndex];
                values[swapIndex] = value;
            }
        }

        private void TryAdvanceAfterDemonCleanup()
        {
            if (!demonArrowFadeCompleted || !demonReturnCompleted)
                return;

            requiresBoxExitBeforeEntry = boxWasAtShootingPointOnDemonReturn;
            Phase = Level78Phase.BoardAdjustment;
        }

        private static int ResolveFarScore(
            float normalizedPointerPosition,
            Level78FarScoringProfile scoringProfile)
        {
            float distanceFromCenter = Math.Abs(normalizedPointerPosition - 0.5f);
            if (distanceFromCenter <= scoringProfile.GreenHalfWidth)
                return 9;

            float yellowSubdivisionWidth =
                (scoringProfile.YellowOuterHalfWidth - scoringProfile.GreenHalfWidth) / 2f;
            if (distanceFromCenter <= scoringProfile.GreenHalfWidth + yellowSubdivisionWidth)
                return 8;
            if (distanceFromCenter <= scoringProfile.YellowOuterHalfWidth)
                return 7;

            float redSubdivisionWidth = (0.5f - scoringProfile.YellowOuterHalfWidth) / 3f;
            if (distanceFromCenter <= scoringProfile.YellowOuterHalfWidth + redSubdivisionWidth)
                return 6;
            if (distanceFromCenter <= scoringProfile.YellowOuterHalfWidth + (redSubdivisionWidth * 2f))
                return 5;
            return 0;
        }

        #endregion
    }
}
