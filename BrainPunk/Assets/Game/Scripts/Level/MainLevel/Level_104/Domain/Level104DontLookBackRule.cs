namespace Game.Level104
{
    /// <summary>
    /// Level 104「不要回头」的主叙事阶段。小狗、箭阵与竖刺墙同时保留独立事实，
    /// 因而阶段只描述当前需要对外编排的主要状态，不再充当刺墙或飞门的箭阵完成门禁。
    /// </summary>
    public enum Level104Phase
    {
        /// <summary>主角在左门位置直接入场；诱饵小狗尚未触发。</summary>
        Entrance,

        /// <summary>诱饵小狗叫喊、等待或向左冲锋中；全程仅一次。</summary>
        DogCharging,

        /// <summary>小狗已到达左侧隐藏点；箭阵若尚未启动，仍只等待自身的玩家位置门禁。</summary>
        ArrowVolleyPending,

        /// <summary>固定世界轨道的追逐箭阵落下中。</summary>
        ArrowVolleyFalling,

        /// <summary>箭阵已经自行收口、飞门尚未开始；这不是刺墙或飞门的开放门禁。</summary>
        DoorZone,

        /// <summary>飞门一段：门体两侧长出翅膀。</summary>
        DoorGrowingWings,

        /// <summary>飞门二段：门扇翅垂直上飞。</summary>
        DoorFlyingUp,

        /// <summary>飞门三段：门保持固定高度向左飞行，水平位移对玩家可见。</summary>
        DoorFlyingLeft,

        /// <summary>旧离屏换位阶段名的兼容别名；当前表现为可见的高位向左飞行。</summary>
        DoorRelocatingOffscreen = DoorFlyingLeft,

        /// <summary>飞门四段：门到达左门位置后垂直下落。</summary>
        DoorFlyingDown,

        /// <summary>门已落定停稳，翅膀收回消失中。</summary>
        DoorRetractingWings,

        /// <summary>门已落定在左门位置、翅膀收回消失，恢复普通可进入状态。</summary>
        DoorLanded,

        /// <summary>小狗首次命中后，标准撞飞表现与 FallArea 结算进行中；玩法推进全部冻结。</summary>
        DogKnockback,

        /// <summary>主角走进已落定的门，沿既有门触发链通关。</summary>
        Victory,

        /// <summary>玩家回头或被仍在下落的箭命中，立即判负。</summary>
        Failure
    }

    /// <summary>
    /// Level 104 最终结果；通关或立即判负前保持 None。小狗命中后的撞飞期间仍为 None，
    /// 最终失败由既有 FallArea 链结算。
    /// </summary>
    public enum Level104Result
    {
        None,
        Victory,
        Failure
    }

    /// <summary>
    /// Level 104「不要回头」的纯 C# 规则模型：回头与箭致死、狗撞飞冻结、一次性事件事实、
    /// 绝对世界 X 飞门门禁、飞门状态机及循环绕屏。Unity 坐标采样与表现触发由运行时适配层负责。
    /// </summary>
    public sealed class Level104DontLookBackRule
    {
        #region Constants

        /// <summary>玩家中心达到门左 4.5 个世界单位（门根 X=49.4 → 44.9）时允许飞门开始；固定晚于 X=41 的刺墙启动门槛。</summary>
        public const float DoorFlyStartActorX = 44.9f;

        #endregion

        #region Public State

        /// <summary>创建一次规则会话；初始处于入场阶段且尚无胜负结果。</summary>
        public Level104DontLookBackRule()
        {
            Phase = Level104Phase.Entrance;
            Result = Level104Result.None;
        }

        /// <summary>当前主要编排阶段；只读，外部不能跳过飞门阶段门禁。</summary>
        public Level104Phase Phase { get; private set; }

        /// <summary>当前胜负结果。狗撞飞期间玩法被冻结，但结果保持 None，等待既有 FallArea 结算。</summary>
        public Level104Result Result { get; private set; }

        /// <summary>诱饵小狗是否已经触发过本局唯一一次叫喊/冲锋序列。</summary>
        public bool DogHasCharged { get; private set; }

        /// <summary>诱饵小狗是否已经到达世界 X=-12 并由表现层完成隐藏。</summary>
        public bool DogHasExited { get; private set; }

        /// <summary>诱饵小狗是否已首次命中主角并进入撞飞表现。</summary>
        public bool DogHasHit { get; private set; }

        /// <summary>小狗撞飞期间为 true；此时 Result 仍为 None，但所有玩法输入与阶段推进均被拒绝。</summary>
        public bool IsGameplayFrozen => Phase == Level104Phase.DogKnockback;

        /// <summary>追逐箭阵是否已经触发过本局唯一一次启动。</summary>
        public bool ArrowVolleyHasFired { get; private set; }

        /// <summary>追逐箭阵是否已经自行落完；这是独立事实，不控制刺墙或飞门。</summary>
        public bool ArrowVolleyHasCompleted { get; private set; }

        /// <summary>竖刺墙是否已经触发过本局唯一一次向左逼近。</summary>
        public bool SpikeWallAdvanceHasStarted { get; private set; }

        /// <summary>竖刺墙是否已经实际碰到主角并向右反弹。</summary>
        public bool SpikeWallHasBounced { get; private set; }

        /// <summary>竖刺墙实际碰撞时记录的主角世界 X；仅在 <see cref="SpikeWallHasBounced"/> 为 true 时有效。</summary>
        public float SpikeWallContactActorX { get; private set; }

        /// <summary>飞门是否已通过绝对世界 X 门禁并开始；全程仅一次。</summary>
        public bool DoorFlyHasStarted { get; private set; }

        /// <summary>
        /// 兼容表现层的刺墙活动窗口视图：任何未结算、未撞飞冻结的阶段都可按玩家位置尝试启动刺墙，
        /// 不读取飞门、小狗或箭阵状态；一次性由 <see cref="TryStartSpikeWallAdvance"/> 自身保证。
        /// </summary>
        public bool IsSpikeWallWindowOpen => CanAdvanceGameplay;

        /// <summary>门是否已落定收翅并恢复普通可进入状态。</summary>
        public bool DoorIsEnterable => Phase == Level104Phase.DoorLanded;

        #endregion

        #region Failure And Movement

        /// <summary>
        /// 玩家按下左移键（回头）：任何正常玩法阶段都立即失败。脚本绕屏不经过本入口；
        /// 小狗撞飞期间玩法已冻结，因此后到的左移输入被拒绝且不改写撞飞状态。
        /// </summary>
        public bool NotifyPlayerLeftMoveInput()
        {
            if (!CanAdvanceGameplay)
                return false;

            SetFailure();
            return true;
        }

        /// <summary>
        /// 循环绕屏的脚本驱动传送：正常玩法阶段永不判负、永不改变阶段；
        /// 已结算或小狗撞飞冻结后拒绝继续绕屏。
        /// </summary>
        public bool NotifyScriptDrivenWrap()
        {
            return CanAdvanceGameplay;
        }

        /// <summary>
        /// 仍在下落且尚未完成的箭首次命中主角：立即 Failure 并终止后续推进。
        /// 表现层应在同帧冲突中先调用本入口，使随后刺墙反弹信号被终态门禁拒绝。
        /// </summary>
        public bool NotifyArrowHit()
        {
            if (!CanAdvanceGameplay || !ArrowVolleyHasFired || ArrowVolleyHasCompleted)
                return false;

            SetFailure();
            return true;
        }

        #endregion

        #region Dog And Arrow Facts

        /// <summary>
        /// 触发诱饵小狗的本局唯一叫喊/冲锋序列。运行时先按玩家中心相对狗根左侧 8（当前绝对 X=23）播放 bark，
        /// 相对左侧 6（当前绝对 X=25）门槛与完整 bark 表现都完成后才开始 run；箭阵可以更早独立启动，Domain 只保证全程一次。
        /// </summary>
        public bool TryStartDogCharge()
        {
            if (!CanAdvanceGameplay || DogHasCharged)
                return false;

            DogHasCharged = true;
            if (!DoorFlyHasStarted)
                Phase = Level104Phase.DogCharging;
            return true;
        }

        /// <summary>
        /// 小狗冲锋中的首次有效命中：进入玩法冻结/撞飞中状态，但不立即把 Result 设为 Failure。
        /// 此状态不提供恢复入口，仅允许既有撞飞表现与外部 FallArea 失败链继续。
        /// </summary>
        public bool NotifyDogHit()
        {
            if (!CanAdvanceGameplay || !DogHasCharged || DogHasExited || DogHasHit)
                return false;

            DogHasHit = true;
            Phase = Level104Phase.DogKnockback;
            return true;
        }

        /// <summary>
        /// 表现层确认小狗根节点到达世界 X=-12 并隐藏。该事实只记录小狗离场；
        /// 箭阵由玩家进入固定覆盖区独立启动。若独立门剧情已先开始，不覆盖当前飞门阶段。
        /// </summary>
        public bool NotifyDogChargeCompleted()
        {
            if (!CanAdvanceGameplay || !DogHasCharged || DogHasExited || DogHasHit)
                return false;

            DogHasExited = true;
            if (!DoorFlyHasStarted && Phase == Level104Phase.DogCharging)
                Phase = ArrowVolleyHasFired
                    ? (ArrowVolleyHasCompleted ? Level104Phase.DoorZone : Level104Phase.ArrowVolleyFalling)
                    : Level104Phase.ArrowVolleyPending;
            return true;
        }

        /// <summary>
        /// 启动固定世界轨道箭阵；表现层仅在玩家首次进入固定五箭覆盖区时调用，且全程一次。
        /// 不读取小狗冲锋、命中或离场事实。若飞门已独立开始，只记录箭阵启动事实，不回退或覆盖飞门阶段。
        /// </summary>
        public bool TryStartArrowVolley()
        {
            if (!CanAdvanceGameplay || ArrowVolleyHasFired)
                return false;

            ArrowVolleyHasFired = true;
            if (!DoorFlyHasStarted)
                Phase = Level104Phase.ArrowVolleyFalling;
            return true;
        }

        /// <summary>
        /// 标记追逐箭阵自行落完。完成信号只记录独立事实：不会开放、延迟或阻止刺墙与飞门；
        /// 晚于飞门启动到达时不得覆盖当前飞门阶段。
        /// </summary>
        public bool NotifyArrowVolleyCompleted()
        {
            if (!CanAdvanceGameplay || !ArrowVolleyHasFired || ArrowVolleyHasCompleted)
                return false;

            ArrowVolleyHasCompleted = true;
            if (!DoorFlyHasStarted && Phase == Level104Phase.ArrowVolleyFalling)
                Phase = Level104Phase.DoorZone;
            return true;
        }

        #endregion

        #region Spike Wall And Door Gate

        /// <summary>
        /// 触发竖刺墙向左逼近。表现层先确认玩家中心 X≥41，且 Actor 主碰撞体已越过升降坑右缘；
        /// 升降坑改由逼近间距/接触兜底揭开，不再与刺墙同帧启动。Domain 不读取几何或箭阵完成状态，只保证刺墙全程启动一次。
        /// </summary>
        public bool TryStartSpikeWallAdvance()
        {
            if (!IsSpikeWallWindowOpen || SpikeWallAdvanceHasStarted)
                return false;

            SpikeWallAdvanceHasStarted = true;
            return true;
        }

        /// <summary>
        /// 标记竖刺墙已实际碰到主角并反弹，记录碰撞时主角世界 X；碰到本身不致死。
        /// 只有已启动逼近且尚未反弹时接受。
        /// </summary>
        public bool NotifySpikeWallBounced(float actorX)
        {
            if (!CanAdvanceGameplay || !SpikeWallAdvanceHasStarted || SpikeWallHasBounced
                || float.IsNaN(actorX) || float.IsInfinity(actorX))
                return false;

            SpikeWallHasBounced = true;
            SpikeWallContactActorX = actorX;
            return true;
        }

        /// <summary>
        /// 尝试启动飞门。唯一业务前置是主角世界 X 达到门左 4.5 个世界单位（44.9）；不读取刺墙碰撞、箭阵启动或完成状态。
        /// </summary>
        public bool TryStartDoorFly(float actorX)
        {
            if (!CanAdvanceGameplay || DoorFlyHasStarted || DoorIsEnterable
                || float.IsNaN(actorX) || float.IsInfinity(actorX))
                return false;

            if (actorX < DoorFlyStartActorX)
                return false;

            DoorFlyHasStarted = true;
            Phase = Level104Phase.DoorGrowingWings;
            return true;
        }

        #endregion

        #region Door Flight State Machine

        /// <summary>门体两侧翅膀已长出，进入垂直上飞阶段。</summary>
        public bool NotifyWingsGrown()
        {
            if (!CanAdvanceGameplay || Phase != Level104Phase.DoorGrowingWings)
                return false;

            Phase = Level104Phase.DoorFlyingUp;
            return true;
        }

        /// <summary>门已到达固定飞行高度，进入可见的高位向左飞行阶段。</summary>
        public bool NotifyDoorReachedFlightHeight()
        {
            if (!CanAdvanceGameplay || Phase != Level104Phase.DoorFlyingUp)
                return false;

            Phase = Level104Phase.DoorFlyingLeft;
            return true;
        }

        /// <summary>兼容旧表现层 API；当前语义为门到达固定飞行高度。</summary>
        public bool NotifyDoorFullyExitedView()
        {
            return NotifyDoorReachedFlightHeight();
        }

        /// <summary>门已保持固定高度向左飞到正常左门 X，进入垂直下落阶段。</summary>
        public bool NotifyDoorReachedLeftX()
        {
            if (!CanAdvanceGameplay || Phase != Level104Phase.DoorFlyingLeft)
                return false;

            Phase = Level104Phase.DoorFlyingDown;
            return true;
        }

        /// <summary>兼容旧表现层 API；当前语义为门完成可见的高位向左飞行。</summary>
        public bool NotifyDoorRelocatedOffscreen()
        {
            return NotifyDoorReachedLeftX();
        }

        /// <summary>门已在左门位置垂直下落停稳，进入收翅阶段。</summary>
        public bool NotifyDoorLanded()
        {
            if (!CanAdvanceGameplay || Phase != Level104Phase.DoorFlyingDown)
                return false;

            Phase = Level104Phase.DoorRetractingWings;
            return true;
        }

        /// <summary>翅膀已收回消失；门恢复普通可进入状态。</summary>
        public bool NotifyWingsRetracted()
        {
            if (!CanAdvanceGameplay || Phase != Level104Phase.DoorRetractingWings)
                return false;

            Phase = Level104Phase.DoorLanded;
            return true;
        }

        /// <summary>主角走进已落定的门；只有门落定收翅后才登记 Victory。</summary>
        public bool TryEnterDoor()
        {
            if (!CanAdvanceGameplay || Phase != Level104Phase.DoorLanded)
                return false;

            Phase = Level104Phase.Victory;
            Result = Level104Result.Victory;
            return true;
        }

        #endregion

        #region Internal Rules

        private bool IsSettled => Result != Level104Result.None;

        private bool CanAdvanceGameplay => !IsSettled && !IsGameplayFrozen;

        private void SetFailure()
        {
            Result = Level104Result.Failure;
            Phase = Level104Phase.Failure;
        }

        #endregion
    }
}
