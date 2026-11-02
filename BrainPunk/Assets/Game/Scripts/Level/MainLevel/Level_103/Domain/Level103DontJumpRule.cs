using System;

namespace Game.Level103
{
    /// <summary>
    /// Level 103「不要跳」事件编排阶段；阶段只能通过 <see cref="Level103DontJumpRule"/> 的公开行为按序推进，
    /// 外部不能跳过阶段门禁。除 <see cref="Victory"/> 与 <see cref="Failure"/> 外，陷坑和门阶段按关卡空间顺序排列；
    /// 诱饵小狗的冲锋/完成状态独立于陷坑阶段，不再阻塞首次塌落。唯一回环边是陷坑：缺口打开后任何阶段再次掉入都回到 <see cref="InPitAutoBounce"/>，
    /// 脱困后返回掉入前阶段（首次掉入从 <see cref="GapOpen"/> 来，脱困后进入 <see cref="ApproachingDoor"/>）。
    /// 2026-08-06 起塌落改为半落回升：半落完成进入 <see cref="GapOpen"/>（当前 3.8 秒等待期），随后经 <see cref="PitRisingBack"/> 复原并进入 <see cref="ApproachingDoor"/>，全程一次性；
    /// 等待/回升期间从块沿主动走下坑道才允许掉入，随块下沉与回升不登记掉入。
    /// 飞门演出与线性阶段解耦：主角到达 Level103Ctrl 当前触发位 X=0.8 后经 <see cref="Level103DontJumpRule.TryLaunchDoorFlight"/>
    /// 一次性表现门禁立即起飞（不占阶段），陷坑完成后由 <see cref="Level103DontJumpRule.NotifyHoveringDoorReadyForClick"/>
    /// 把门阶段从 <see cref="ApproachingDoor"/> 直接推进到 <see cref="DoorHovering"/>；玩家点击门体或任意翅膀后，
    /// 双翼上飞与门体下落同帧开始，当前生产路径直接进入 <see cref="DoorFalling"/>，门落稳后进入 <see cref="DoorGrounded"/>。
    /// <see cref="DoorWingsDeparting"/> 仅保留给旧顺序 API 兼容。
    /// <see cref="DoorGrowingWings"/> 与 <see cref="DoorFlyingUp"/> 保留为线性 API 的组成阶段（提前起飞路径在运行时不再经过）。
    /// </summary>
    public enum Level103Phase
    {
        /// <summary>主角从左侧入场门出场、向右移动；小狗在主角满足其位置条件时一次性启动。</summary>
        Entrance,

        /// <summary>诱饵小狗叫喊、等待或狂奔掠过中（前层渲染、无碰撞、全程仅一次）；陷坑可在此期间独立触发。</summary>
        DogPassing,

        /// <summary>小狗掠过完成后的兼容阶段；陷坑不再以此阶段为前置条件。</summary>
        ApproachingPit,

        /// <summary>坑前半落下坠中：碰撞保持开启，主角随块下沉；掉落本身是唯一可见演出。</summary>
        GroundDropping,

        /// <summary>半落下坠完成后的当前 3.8 秒等待期：坑洞无法步行越过，可以掉入陷坑（从块沿走下坑道）。</summary>
        GapOpen,

        /// <summary>当前等待结束、半落地块自动回升原位中；回升完成后地形恢复平整并进入 <see cref="ApproachingDoor"/>。</summary>
        PitRisingBack,

        /// <summary>掉入陷坑（不致死）后的自动上跳状态：持续弹跳，玩家只用左右键移出坑沿。</summary>
        InPitAutoBounce,

        /// <summary>已移出坑沿、靠近右门之前的行进段。</summary>
        ApproachingDoor,

        /// <summary>飞门一段：门体两侧长出翅膀。</summary>
        DoorGrowingWings,

        /// <summary>飞门二段：门扇翅向上飞起。</summary>
        DoorFlyingUp,

        /// <summary>飞门三段：门在固定世界高度 Y=3.545 高位悬停，等待玩家点击门体或任意翅膀。</summary>
        DoorHovering,

        /// <summary>旧顺序兼容阶段；当前点击生产路径直接进入 <see cref="DoorFalling"/>。</summary>
        DoorWingsDeparting,

        /// <summary>点击后双翼垂直上飞，同时门体沿世界 Y 轴垂直下落。</summary>
        DoorFalling,

        /// <summary>旧拖门兼容阶段；当前运行时不再进入。</summary>
        DoorDragging,

        /// <summary>旧拖门兼容阶段；当前运行时不再进入。</summary>
        DoorReturningToHover,

        /// <summary>旧拖门兼容阶段；当前运行时不再进入。</summary>
        DoorSnappingToGround,

        /// <summary>门体已垂直落到地面并稳定，恢复普通可进入状态。</summary>
        DoorGrounded,

        /// <summary>主角走进已归位的门，沿既有门触发链通关。</summary>
        Victory,

        /// <summary>玩家按下上跳键，触发跳跃即死。</summary>
        Failure
    }

    /// <summary>
    /// Level 103 最终结果；通关或判负前保持 None。
    /// </summary>
    public enum Level103Result
    {
        None,
        Victory,
        Failure
    }

    /// <summary>
    /// Level 103「不要跳」的本地规则模型：跳跃即死判定、事件编排顺序与飞门点击落地状态机。
    /// 不持有 Unity 对象、坐标、动画或 HUD 引用；飞离高度、下落时长、触发距离与时机等标定项由运行时表现层负责。
    /// </summary>
    public sealed class Level103DontJumpRule
    {
        #region Public API

        /// <summary>
        /// 创建一次 Level 103「不要跳」规则会话，初始阶段为入场。
        /// </summary>
        public Level103DontJumpRule()
        {
            Phase = Level103Phase.Entrance;
            Result = Level103Result.None;
        }

        /// <summary>
        /// 当前事件编排阶段；只读，外部不能跳过阶段门禁。
        /// </summary>
        public Level103Phase Phase { get; private set; }

        /// <summary>
        /// 陷坑自动上跳期间，Box 是否已经完全离开画面；运行时据此锁定水平位移。
        /// 只有 <see cref="Level103Phase.InPitAutoBounce"/> 阶段可以进入该状态。
        /// </summary>
        public bool PitActorIsFullyOffscreen { get; private set; }

        /// <summary>
        /// 当前胜负结果；玩家起跳输入判负、走进归位门判胜，之后一切输入被忽略。
        /// </summary>
        public Level103Result Result { get; private set; }

        /// <summary>
        /// 诱饵小狗是否已经触发本局唯一一次叫喊/冲锋序列；同一局只允许从 false 变为 true 一次。
        /// </summary>
        public bool DogHasCharged { get; private set; }

        /// <summary>
        /// 诱饵小狗是否已经到达离场点并隐藏。该表现完成状态不再推进或阻塞陷坑阶段。
        /// </summary>
        public bool DogPassCompleted { get; private set; }

        /// <summary>
        /// 陷坑是否已接收本局首次接近信号。塌落仅能触发一次，与小狗状态无关。
        /// </summary>
        public bool PitHasBeenTriggered { get; private set; }

        /// <summary>
        /// 门前地面尖刺是否已经完成本局唯一一次破土弹出；同一局只允许从 false 变为 true 一次。
        /// </summary>
        public bool FloorSpikesHasEmerged { get; private set; }

        /// <summary>
        /// 飞门演出是否已经启动；同一局只允许从 false 变为 true 一次。当前由主角 X≥0.8
        /// 立即起飞（<see cref="TryLaunchDoorFlight"/>），演出本身不再占用线性阶段。
        /// </summary>
        public bool DoorFlightLaunched { get; private set; }

        /// <summary>
        /// 旧拖门兼容数据；当前点击落地流程不再写入。
        /// </summary>
        public float DoorDragVerticalOffset { get; private set; }

        /// <summary>
        /// 当前流程不再开放拖门；保留属性仅供既有源码引用。
        /// </summary>
        public bool DoorDragAllowed => false;

        /// <summary>
        /// 门当前是否恢复普通可进入状态；只有翅膀飞离且门垂直落稳后为 true。
        /// </summary>
        public bool DoorIsEnterable =>
            Phase == Level103Phase.DoorGrounded ||
            (Phase == Level103Phase.InPitAutoBounce && _phaseBeforePitFall == Level103Phase.DoorGrounded);

        /// <summary>
        /// 玩家按下上跳键的输入事件：通常立即触发跳跃即死；若 Box 正完全离屏，则只记录待处理违规，
        /// 保持陷坑自动反弹，待重新入画时再结算可见死亡。与脚本驱动位移在 API 层面严格分离。
        /// </summary>
        /// <returns>运行时是否应立即显示刺石并触发死亡；离屏延迟处理或已结算时返回 false。</returns>
        public bool NotifyPlayerJumpInput()
        {
            if (IsSettled)
                return false;

            if (Phase == Level103Phase.InPitAutoBounce && PitActorIsFullyOffscreen)
            {
                _hasPendingOffscreenJumpViolation = true;
                return false;
            }

            SetFailure();
            return true;
        }

        /// <summary>
        /// 脚本驱动的位移事件（陷坑自动上跳）：不是玩家起跳输入，绝不触发跳跃即死，也不会引来头顶刺石。
        /// 本入口永不改变胜负结果；仅在陷坑自动上跳阶段承认本次位移有效。
        /// </summary>
        /// <returns>当前是否处于陷坑自动上跳阶段（即本次脚本位移是否属于自动上跳）。</returns>
        public bool NotifyScriptDrivenLift()
        {
            if (IsSettled)
                return false;

            return Phase == Level103Phase.InPitAutoBounce;
        }

        /// <summary>
        /// 触发诱饵小狗的本局唯一叫喊/冲锋序列；小狗与陷坑解耦后，坑洞已开始时仍可启动小狗表现，
        /// 但只有尚未开始坑洞的入场路径才保留 DogPassing 阶段，兼容原有线性门状态。
        /// </summary>
        /// <returns>是否接受本次触发。</returns>
        public bool TryStartDogCharge()
        {
            if (IsSettled || DogHasCharged)
                return false;

            DogHasCharged = true;
            if (Phase == Level103Phase.Entrance)
                Phase = Level103Phase.DogPassing;
            return true;
        }

        /// <summary>
        /// 标记诱饵小狗已掠过主角面前并冲出左侧屏外。仅当仍处于历史 DogPassing 路径时进入
        /// ApproachingPit；坑洞已在其它阶段运行时，此回调只记录小狗完成，绝不覆盖坑洞阶段。
        /// </summary>
        /// <returns>是否接受本次完成信号。</returns>
        public bool NotifyDogPassCompleted()
        {
            if (IsSettled || !DogHasCharged || DogPassCompleted)
                return false;

            DogPassCompleted = true;
            if (Phase == Level103Phase.DogPassing)
                Phase = Level103Phase.ApproachingPit;
            return true;
        }

        /// <summary>
        /// 标记主角已接近坑缘触发位，伪装地板同帧进入半落下坠阶段；不经过抖动或预沉提示。
        /// 小狗尚未启动、正在冲锋或已经完成均不影响本次一次性触发。
        /// </summary>
        /// <returns>是否接受本次触发。</returns>
        public bool NotifyPitApproached()
        {
            if (IsSettled || PitHasBeenTriggered ||
                (Phase != Level103Phase.Entrance &&
                 Phase != Level103Phase.DogPassing &&
                 Phase != Level103Phase.ApproachingPit))
                return false;

            PitHasBeenTriggered = true;
            Phase = Level103Phase.GroundDropping;
            return true;
        }

        /// <summary>
        /// 标记半落下坠已结束，进入当前 3.8 秒等待期（<see cref="GapOpen"/>）；下坠完成前不允许掉入陷坑。
        /// </summary>
        /// <returns>是否接受本次完成信号。</returns>
        public bool NotifyGroundCollapseCompleted()
        {
            if (IsSettled || Phase != Level103Phase.GroundDropping)
                return false;

            Phase = Level103Phase.GapOpen;
            return true;
        }

        /// <summary>
        /// 标记主角掉入陷坑；掉入不致死，进入自动上跳状态，玩家只用左右键移出坑沿。
        /// 缺口打开后（含脱困折返、门流程中途折返）任何阶段再次掉入同样接受，并记录掉入前阶段，
        /// 供 <see cref="NotifyPitEdgeExited"/> 脱困后原路返回；塌落完成前与自动上跳期间的掉入信号仍拒绝。
        /// </summary>
        /// <returns>是否接受本次掉入事件。</returns>
        public bool NotifyFellIntoPit()
        {
            if (IsSettled ||
                Phase < Level103Phase.GapOpen ||
                Phase == Level103Phase.InPitAutoBounce ||
                Phase >= Level103Phase.Victory)
                return false;

            _phaseBeforePitFall = Phase;
            Phase = Level103Phase.InPitAutoBounce;
            return true;
        }

        /// <summary>
        /// 标记陷坑自动上跳中的 Box 已完全离开画面；离屏期间水平位移必须锁定，方向输入由运行时保留。
        /// </summary>
        /// <returns>是否接受本次离屏信号。</returns>
        public bool NotifyPitActorFullyOffscreen()
        {
            if (IsSettled ||
                Phase != Level103Phase.InPitAutoBounce ||
                PitActorIsFullyOffscreen)
                return false;

            PitActorIsFullyOffscreen = true;
            return true;
        }

        /// <summary>
        /// 标记 Box 从陷坑下方重新进入画面并恢复水平控制；若离屏期间玩家按过跳键，
        /// 此时才转为失败，返回 true 供运行时触发可见刺石死亡。
        /// </summary>
        /// <returns>是否应在重新入画时显示刺石并触发死亡。</returns>
        public bool NotifyPitActorReenteredView()
        {
            if (IsSettled ||
                Phase != Level103Phase.InPitAutoBounce ||
                !PitActorIsFullyOffscreen)
                return false;

            PitActorIsFullyOffscreen = false;
            if (!_hasPendingOffscreenJumpViolation)
                return false;

            SetFailure();
            return true;
        }

        /// <summary>
        /// 标记玩家已通过左右移动把主角移出坑沿、回到地面；自动上跳状态结束。
        /// Box 完全离屏时不能直接登记脱困，必须先重新入画。
        /// 首次掉入（来自 <see cref="GapOpen"/>）脱困后沿主流程进入 <see cref="ApproachingDoor"/>；
        /// 折返再次掉入的脱困后返回掉入前阶段，飞门等门流程进度不重复触发。
        /// 若脱困时半落地块已复原（<see cref="_pitRestored"/>），无论掉入前阶段如何都进入 <see cref="ApproachingDoor"/>。
        /// </summary>
        /// <returns>是否接受本次脱困事件。</returns>
        public bool NotifyPitEdgeExited()
        {
            if (IsSettled ||
                Phase != Level103Phase.InPitAutoBounce ||
                PitActorIsFullyOffscreen)
                return false;

            if (_phaseBeforePitFall > Level103Phase.ApproachingDoor)
            {
                Phase = _phaseBeforePitFall;
            }
            else
            {
                Phase = _pitRestored || _phaseBeforePitFall == Level103Phase.GapOpen
                    ? Level103Phase.ApproachingDoor
                    : _phaseBeforePitFall;
            }
            return true;
        }

        /// <summary>
        /// 标记当前等待结束、半落地块开始自动回升原位；回升不做坑道占用检测，按时启动。
        /// 坑内弹跳（<see cref="InPitAutoBounce"/>）期间接受但不改变阶段；否则只接受 <see cref="GapOpen"/> 等待期。
        /// </summary>
        /// <returns>是否接受本次回升开始信号。</returns>
        public bool NotifyPitRiseStarted()
        {
            if (IsSettled)
                return false;

            if (Phase == Level103Phase.InPitAutoBounce)
                return _phaseBeforePitFall >= Level103Phase.GapOpen;

            if (Phase != Level103Phase.GapOpen)
                return false;

            Phase = Level103Phase.PitRisingBack;
            return true;
        }

        /// <summary>
        /// 标记半落地块已回升原位：地形恢复平整、坑壁重新隐藏，此后该地块永久实心、不再重新武装。
        /// 正常路径从 <see cref="PitRisingBack"/> 进入 <see cref="ApproachingDoor"/>；
        /// 坑内弹跳期间只登记复原标记，脱困时据此直接进入 <see cref="ApproachingDoor"/>。
        /// </summary>
        /// <returns>是否接受本次复原信号。</returns>
        public bool NotifyPitRestored()
        {
            if (IsSettled)
                return false;

            if (Phase == Level103Phase.InPitAutoBounce)
            {
                if (_phaseBeforePitFall < Level103Phase.GapOpen)
                    return false;

                _pitRestored = true;
                return true;
            }

            if (Phase != Level103Phase.PitRisingBack)
                return false;

            _pitRestored = true;
            Phase = Level103Phase.ApproachingDoor;
            return true;
        }

        /// <summary>
        /// 启动飞门演出（长翅膀→飞起→悬停）；纯表现一次性门禁，与 <see cref="TryEmergeFloorSpikes"/> 同型，
        /// 不改变事件编排阶段。当前运行时在主角到达 X=0.8 时调用；门阶段仍在陷坑完成后经
        /// <see cref="NotifyHoveringDoorReadyForClick"/> 推进，因此提前起飞不会跳过坑洞或提前开放点击。
        /// </summary>
        /// <returns>是否接受本次启动；已结算、已启动过或门阶段已开始时拒绝。</returns>
        public bool TryLaunchDoorFlight()
        {
            if (IsSettled || DoorFlightLaunched || Phase >= Level103Phase.DoorGrowingWings)
                return false;

            DoorFlightLaunched = true;
            return true;
        }

        /// <summary>
        /// 标记提前起飞的门已物理到位悬停、且陷坑流程已完成：门阶段从 ApproachingDoor 直接推进到
        /// DoorHovering 并开放点击。跳过 DoorGrowingWings/DoorFlyingUp 是提前起飞路径的固有形态
        /// （演出阶段不占线性阶段）；仅当运行时确认门已悬停到位后调用，拒绝时可逐帧重试。
        /// </summary>
        /// <returns>是否接受本次推进。</returns>
        public bool NotifyHoveringDoorReadyForClick()
        {
            if (IsSettled || Phase != Level103Phase.ApproachingDoor)
                return false;

            Phase = Level103Phase.DoorHovering;
            return true;
        }

        /// <summary>旧方法名兼容入口；当前语义为开放悬停门点击，不再开放拖拽。</summary>
        public bool NotifyHoveringDoorReadyForDrag()
        {
            return NotifyHoveringDoorReadyForClick();
        }

        /// <summary>
        /// 标记主角已靠近右门，触发飞门一段（长翅膀）；只有脱困后的行进段才接受，且只触发一次。
        /// 2026-08-07 起运行时不再经此入口起飞（改由 <see cref="TryLaunchDoorFlight"/> 与
        /// <see cref="NotifyHoveringDoorReadyForClick"/> 承载），本方法保留为线性 API 的组成环节。
        /// </summary>
        /// <returns>是否接受本次触发。</returns>
        public bool NotifyDoorApproached()
        {
            if (IsSettled || Phase != Level103Phase.ApproachingDoor)
                return false;

            Phase = Level103Phase.DoorGrowingWings;
            return true;
        }

        /// <summary>
        /// 标记门体两侧翅膀已长出，进入飞门二段（扇翅向上飞起）。
        /// </summary>
        /// <returns>是否接受本次完成信号。</returns>
        public bool NotifyWingsGrown()
        {
            if (IsSettled || Phase != Level103Phase.DoorGrowingWings)
                return false;

            Phase = Level103Phase.DoorFlyingUp;
            return true;
        }

        /// <summary>
        /// 标记门已飞到固定世界高度 Y=3.545 的高位悬停点，以高位门诱导玩家主动跳跃；悬停后等待点击。
        /// </summary>
        /// <returns>是否接受本次到位信号。</returns>
        public bool NotifyDoorReachedHoverPoint()
        {
            if (IsSettled || Phase != Level103Phase.DoorFlyingUp)
                return false;

            Phase = Level103Phase.DoorHovering;
            return true;
        }

        /// <summary>
        /// 玩家点击悬停门体或任意翅膀，启动本局唯一一次“双翼垂直上飞 + 门体同步垂直下落”流程。
        /// </summary>
        /// <returns>是否接受本次点击；非悬停阶段、已结算或重复点击均拒绝。</returns>
        public bool TryStartDoorLanding()
        {
            if (IsSettled || Phase != Level103Phase.DoorHovering)
                return false;

            Phase = Level103Phase.DoorFalling;
            return true;
        }

        /// <summary>旧顺序兼容入口；当前生产路径的双翼隐藏不再推进门体下落阶段。</summary>
        public bool NotifyDoorWingsDeparted()
        {
            if (IsSettled)
                return false;

            if (Phase == Level103Phase.DoorWingsDeparting)
            {
                Phase = Level103Phase.DoorFalling;
                return true;
            }

            if (Phase == Level103Phase.InPitAutoBounce &&
                _phaseBeforePitFall == Level103Phase.DoorWingsDeparting)
            {
                _phaseBeforePitFall = Level103Phase.DoorFalling;
                return true;
            }

            return false;
        }

        /// <summary>标记门体已垂直落稳；此后才允许恢复门进入触发器。</summary>
        public bool NotifyDoorLanded()
        {
            if (IsSettled)
                return false;

            if (Phase == Level103Phase.DoorFalling)
            {
                Phase = Level103Phase.DoorGrounded;
                return true;
            }

            if (Phase == Level103Phase.InPitAutoBounce &&
                _phaseBeforePitFall == Level103Phase.DoorFalling)
            {
                _phaseBeforePitFall = Level103Phase.DoorGrounded;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 旧拖门兼容入口；当前正式流程永久拒绝拖拽。
        /// </summary>
        /// <returns>始终返回 false。</returns>
        public bool TryBeginDoorDrag()
        {
            return false;
        }

        /// <summary>
        /// 拖拽过程中的手指位移增量；垂直轴锁定——水平分量被规则忽略，只累积垂直分量。
        /// </summary>
        /// <param name="horizontalDelta">水平位移增量；按垂直轴锁定规则被忽略，不产生任何状态变化。</param>
        /// <param name="verticalDelta">垂直位移增量（向上为正）。</param>
        /// <returns>是否接受本次拖拽位移；非拖拽状态或垂直分量非有限值时拒绝。</returns>
        public bool NotifyDoorDragDelta(float horizontalDelta, float verticalDelta)
        {
            // 垂直轴锁定：水平输入被刻意忽略，不参与任何规则计算。
            _ = horizontalDelta;

            if (IsSettled ||
                Phase != Level103Phase.DoorDragging ||
                float.IsNaN(verticalDelta) ||
                float.IsInfinity(verticalDelta))
                return false;

            DoorDragVerticalOffset += verticalDelta;
            return true;
        }

        /// <summary>
        /// 玩家松手结束拖拽；到位判定（吸附阈值）由运行时表现层按标定项计算后传入。
        /// 到位松手进入吸附回地面流程；未到位松手门扇翅飞回悬停点，可反复尝试。
        /// </summary>
        /// <param name="releasedWithinSnapZone">松手瞬间门下沿是否已进入地面原位附近的吸附阈值。</param>
        /// <returns>是否接受本次松手；没有进行中的拖拽时拒绝。</returns>
        public bool TryEndDoorDrag(bool releasedWithinSnapZone)
        {
            if (IsSettled || Phase != Level103Phase.DoorDragging)
                return false;

            Phase = releasedWithinSnapZone
                ? Level103Phase.DoorSnappingToGround
                : Level103Phase.DoorReturningToHover;
            return true;
        }

        /// <summary>
        /// 标记未到位松手后门已飞回悬停点，恢复悬停；之后可以再次开始拖拽。
        /// </summary>
        /// <returns>是否接受本次到位信号。</returns>
        public bool NotifyDoorReturnedToHover()
        {
            if (IsSettled || Phase != Level103Phase.DoorReturningToHover)
                return false;

            Phase = Level103Phase.DoorHovering;
            return true;
        }

        /// <summary>
        /// 标记到位松手后门已平滑吸附回地面原位；翅膀收回消失，门恢复普通可进入状态。
        /// </summary>
        /// <returns>是否接受本次完成信号。</returns>
        public bool NotifyDoorSnapCompleted()
        {
            if (IsSettled || Phase != Level103Phase.DoorSnappingToGround)
                return false;

            Phase = Level103Phase.DoorGrounded;
            return true;
        }

        /// <summary>
        /// 触发门前地面尖刺破土弹出；纯表现编排门禁，全程仅一次，判胜或判负后拒绝。
        /// 不改变事件编排阶段，也不影响胜负结果；触发位与弹出演出由运行时表现层按标定项负责。
        /// </summary>
        /// <returns>是否接受本次触发。</returns>
        public bool TryEmergeFloorSpikes()
        {
            if (IsSettled || FloorSpikesHasEmerged)
                return false;

            FloorSpikesHasEmerged = true;
            return true;
        }

        /// <summary>
        /// 主角走进已落稳的门，沿既有门触发链通关；只有翅膀飞离且门垂直落地后才接受。
        /// </summary>
        /// <returns>是否接受本次进门通关。</returns>
        public bool TryEnterDoor()
        {
            if (IsSettled || Phase != Level103Phase.DoorGrounded)
                return false;

            Phase = Level103Phase.Victory;
            Result = Level103Result.Victory;
            return true;
        }

        #endregion

        #region Internal Rules

        private bool _hasPendingOffscreenJumpViolation;

        /// <summary>本次掉入陷坑前的阶段；脱困后原路返回。默认 GapOpen 对应首次掉入的主流程。</summary>
        private Level103Phase _phaseBeforePitFall = Level103Phase.GapOpen;

        /// <summary>半落地块是否已回升复原（一次性）；复原后脱困直接进入 ApproachingDoor。</summary>
        private bool _pitRestored;

        private bool IsSettled => Result != Level103Result.None;

        private void SetFailure()
        {
            Result = Level103Result.Failure;
            Phase = Level103Phase.Failure;
        }

        #endregion
    }
}
