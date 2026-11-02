using System;

namespace Game.Level105
{
    /// <summary>
    /// Level 105「不要操作」自动流程阶段。外部只能通过 <see cref="Level105DontOperateRule"/> 的公开事件按序推进，
    /// 不能跳过开场、坑底重开弹窗入场/关闭、坑中钥匙、宝箱确认、开箱、假弹窗入场、假门进门/出门完成或真门判胜门禁。
    /// </summary>
    public enum Level105Phase
    {
        /// <summary>等待首次起始门出场完成；规则已从第一帧生效，但开局 5 秒尚未开始。</summary>
        WaitingForOpening,

        /// <summary>出场完成或失败重开就绪后，主角在初始站位完整等待 5 秒。</summary>
        OpeningIdle,

        /// <summary>开局等待结束后自动向右奔向固定陷坑。</summary>
        RunningToPit,

        /// <summary>主角继续向右掉入固定陷坑，直到首次落到安全坑底。</summary>
        FallingIntoPit,

        /// <summary>落到安全坑底后 Box 停住保持 idle，坑底重开弹窗已请求打开，等待真实入场表现完成。</summary>
        PitRestartPopupEntering,

        /// <summary>坑底重开弹窗入场完成后完整展示 5 秒；Box 保持坑底停住不开跳。</summary>
        PitRestartPopupShowing,

        /// <summary>坑底重开弹窗展示期已结束并已请求关闭，等待真实界面完全移除；本阶段不计时。</summary>
        PitRestartPopupClosing,

        /// <summary>坑底重开弹窗完全移除后进入坑底原地垂直弹跳阶段；第 5 跳进行中取得钥匙并完成落地前不允许脱坑。</summary>
        PitBouncing,

        /// <summary>持有钥匙的第 5 次落地后立即执行独立第 6 次脱坑跳。</summary>
        PitEscaping,

        /// <summary>脱坑落上右侧地面后奔向关闭宝箱左侧的停点。</summary>
        RunningToChest,

        /// <summary>到达关闭宝箱前并完全停住后，保持空场景原地等待 2 秒。</summary>
        ChestDelay,

        /// <summary>开箱确认弹窗已经请求打开，等待真实入场表现完成。</summary>
        ChestConfirmEntering,

        /// <summary>开箱确认弹窗入场完成后完整展示 5 秒。</summary>
        ChestConfirmShowing,

        /// <summary>确认弹窗展示期已结束并已请求关闭，等待真实界面完全移除；本阶段不计时且仍持有钥匙。</summary>
        ChestConfirmClosing,

        /// <summary>确认弹窗已关闭且本地钥匙已消费，宝箱执行固定 1.2 秒开启表现。</summary>
        ChestOpening,

        /// <summary>金币假弹窗已经请求打开，等待真实入场表现完成。</summary>
        CoinPopupEntering,

        /// <summary>金币假弹窗入场完成后完整展示 5 秒。</summary>
        CoinPopupShowing,

        /// <summary>金币假弹窗完全移除后奔向世界 X=7.6 的假门停点。</summary>
        RunningToFakeDoor,

        /// <summary>
        /// 到达假门停点后，Box 在假门执行与标准 SuccessAnimCtrl 对齐的完整进门成功表现
        /// （开门、彩带、同一 Actor 移动/旋转/缩小进门消失、关门、关门音效、成功震动、彩虹粒子与约 0.8 秒收尾）；
        /// 该表现不产生真实胜利、奖励、存档或解锁业务，也不销毁 Actor。
        /// </summary>
        FakeDoorEntering,

        /// <summary>假门进门成功表现全部完成后才请求打开假胜利面板，等待真实入场表现完成。</summary>
        FakeVictoryEntering,

        /// <summary>假胜利入场完成后完整展示 5 秒。</summary>
        FakeVictoryShowing,

        /// <summary>
        /// 假胜利完全移除后，同一假门重新开门，同一 Box 按门当前朝向向左走出，
        /// 假门完整关门并播放关门音效；Box 停在门左侧并保持朝左。
        /// </summary>
        FakeDoorExiting,

        /// <summary>假门出门与关门全部完成后奔向第二扇门（真胜利门），Box 自动转右继续。</summary>
        RunningToRealDoor,

        /// <summary>
        /// Box 到达镜头即将切到第二屏的节点后原地踏步（保持向右走动画但不位移），
        /// 模拟网络卡顿；固定卡住时长结束后才请求打开断网假弹窗。
        /// </summary>
        NetworkStuckWaiting,

        /// <summary>断网假弹窗已经请求打开，等待真实入场表现完成。</summary>
        NetworkPopupEntering,

        /// <summary>断网假弹窗入场完成后完整展示 5 秒。</summary>
        NetworkPopupShowing,

        /// <summary>断网假弹窗展示期已结束并已请求关闭，等待真实界面完全移除；本阶段不计时。</summary>
        NetworkPopupClosing,

        /// <summary>断网假弹窗完全移除后 Box 恢复向右奔跑，直到中间路程的来电横幅节点。</summary>
        RunningToCallNode,

        /// <summary>Box 在中间路程节点停住，来电横幅已请求打开，等待下滑入场表现完成。</summary>
        CallPopupEntering,

        /// <summary>来电横幅下滑入场完成后完整展示 5 秒；无遮罩，白名单保持安全。</summary>
        CallPopupShowing,

        /// <summary>来电横幅展示期已结束并已请求关闭，等待真实界面完全移除；本阶段不计时。</summary>
        CallPopupClosing,

        /// <summary>来电横幅完全移除后 Box 恢复向右奔跑，直到真门左侧的游戏更新弹窗节点。</summary>
        RunningToUpdateNode,

        /// <summary>游戏更新假弹窗已经请求打开，等待真实入场表现完成。</summary>
        UpdatePopupEntering,

        /// <summary>游戏更新假弹窗入场完成后完整展示 5 秒。</summary>
        UpdatePopupShowing,

        /// <summary>游戏更新假弹窗展示期已结束并已请求关闭，等待真实界面完全移除；本阶段不计时。</summary>
        UpdatePopupClosing,

        /// <summary>游戏更新假弹窗完全移除后奔向真门，直到标准真门触发判胜。</summary>
        ApproachingRealDoor,

        /// <summary>标准真门触发器已经判胜；规则立即解除，此后输入安全。</summary>
        Victory,

        /// <summary>规则窗口内发生非白名单输入，同帧锁定失败。</summary>
        Failure
    }

    /// <summary>
    /// Level 105 最终结果；通关或判负前保持 None。
    /// </summary>
    public enum Level105Result
    {
        None,
        Victory,
        Failure
    }

    /// <summary>
    /// Level 105 的系统 UI 白名单按钮；只有暂停、重来、提示三个入口及其系统流程安全。
    /// </summary>
    public enum Level105SystemButton
    {
        /// <summary>暂停按钮。</summary>
        Pause,

        /// <summary>重来按钮。</summary>
        Restart,

        /// <summary>提示按钮。</summary>
        Hint
    }

    /// <summary>
    /// Level 105「不要操作」的零 Unity 依赖规则模型：负责唯一流程状态机、精确节奏、5+1 跳计数、
    /// 坑中钥匙与宝箱门禁、坑底重开弹窗与三个诱惑弹窗入场/关闭就绪、假门进门/出门完成门禁、本关挂起及胜负终态。
    /// 坐标、物理、钥匙/宝箱表现和真实 UI 由运行时组件适配。
    /// </summary>
    public sealed class Level105DontOperateRule
    {
        #region Public API

        /// <summary>
        /// 创建一次 Level 105 规则会话。初始等待首次出场完成，但非白名单输入已经会立即判负。
        /// </summary>
        public Level105DontOperateRule()
        {
            Phase = Level105Phase.WaitingForOpening;
            Result = Level105Result.None;
            OpeningIdleDuration = 5f;
            ChestDelayDuration = 2f;
            ChestConfirmShowingDuration = 5f;
            ChestOpeningDuration = 1.2f;
            PitRestartPopupShowingDuration = 5f;
            CoinPopupShowingDuration = 5f;
            FakeVictoryShowingDuration = 5f;
            NetworkStuckDuration = 4f;
            NetworkPopupShowingDuration = 5f;
            CallPopupShowingDuration = 5f;
            UpdatePopupShowingDuration = 5f;
            RequiredPitBounceCount = 5;
        }

        /// <summary>当前自动流程阶段；只读，外部不能跳过阶段门禁。</summary>
        public Level105Phase Phase { get; private set; }

        /// <summary>当前胜负结果；Victory / Failure 一旦成立便不可撤销。</summary>
        public Level105Result Result { get; private set; }

        /// <summary>首次出场完成后及失败重开后的可见原地等待时长，默认 5 秒。</summary>
        public float OpeningIdleDuration { get; set; }

        /// <summary>主角停在关闭宝箱左侧后的空场景等待时长，默认 2 秒。</summary>
        public float ChestDelayDuration { get; set; }

        /// <summary>开箱确认弹窗真实入场完成后的完整展示时长，默认 5 秒。</summary>
        public float ChestConfirmShowingDuration { get; set; }

        /// <summary>消费钥匙并开始开箱后，到金币弹窗入场请求之间的固定时长，默认 1.2 秒。</summary>
        public float ChestOpeningDuration { get; set; }

        /// <summary>坑底重开弹窗真实入场完成后的完整展示时长，默认 5 秒。</summary>
        public float PitRestartPopupShowingDuration { get; set; }

        /// <summary>金币假弹窗真实入场完成后的完整展示时长，默认 5 秒。</summary>
        public float CoinPopupShowingDuration { get; set; }

        /// <summary>假胜利弹窗真实入场完成后的完整展示时长，默认 5 秒。</summary>
        public float FakeVictoryShowingDuration { get; set; }

        /// <summary>到达第二屏镜头切换节点后，Box 原地踏步模拟网络卡顿的固定时长，默认 2 秒。</summary>
        public float NetworkStuckDuration { get; set; }

        /// <summary>断网假弹窗真实入场完成后的完整展示时长，默认 5 秒。</summary>
        public float NetworkPopupShowingDuration { get; set; }

        /// <summary>来电横幅下滑入场完成后的完整展示时长，默认 5 秒。</summary>
        public float CallPopupShowingDuration { get; set; }

        /// <summary>游戏更新假弹窗真实入场完成后的完整展示时长，默认 5 秒。</summary>
        public float UpdatePopupShowingDuration { get; set; }

        /// <summary>脱坑前固定完成的坑底完整原地弹跳次数；Level 105 合同固定为 5 次。</summary>
        public int RequiredPitBounceCount { get; }

        /// <summary>本次陷坑流程已经完成并重新落回坑底的原地弹跳次数。</summary>
        public int CompletedPitBounceCount { get; private set; }

        /// <summary>本次尝试当前是否持有尚未消费的坑中宝箱钥匙。</summary>
        public bool HasChestKey { get; private set; }

        /// <summary>系统面板是否正在挂起 Level 105 本地阶段、计时与脚本驱动流程。</summary>
        public bool IsSuspended { get; private set; }

        /// <summary>规则是否已经进入 Victory 或 Failure 终态。</summary>
        public bool IsSettled => Result != Level105Result.None;

        /// <summary>
        /// 当前是否需要运行时持续施加向右脚本输入。坑底五跳、所有等待、确认/金币/假胜利/断网/更新弹窗、
        /// 各关闭等待、开箱表现、假门进门/出门表演及断网卡顿原地踏步期间均返回 false；
        /// 第 6 次脱坑跳及各段自动奔跑返回 true。
        /// </summary>
        public bool IsAutoRunPhase =>
            Phase == Level105Phase.RunningToPit ||
            Phase == Level105Phase.FallingIntoPit ||
            Phase == Level105Phase.PitEscaping ||
            Phase == Level105Phase.RunningToChest ||
            Phase == Level105Phase.RunningToFakeDoor ||
            Phase == Level105Phase.RunningToRealDoor ||
            Phase == Level105Phase.RunningToCallNode ||
            Phase == Level105Phase.RunningToUpdateNode ||
            Phase == Level105Phase.ApproachingRealDoor;

        /// <summary>
        /// 推进当前计时阶段。只有开局 5 秒、宝箱前 2 秒、确认展示 5 秒、开箱 1.2 秒、
        /// 坑底重开展示 5 秒、金币展示 5 秒、假胜利展示 5 秒、断网卡顿 2 秒、断网展示 5 秒
        /// 和更新展示 5 秒会消耗时间；挂起、入场等待、各关闭等待、假门进门/出门表演、
        /// 自动行进和终态不消耗本关节奏。
        /// </summary>
        /// <param name="deltaTime">本帧时间增量；非有限值或小于等于 0 时忽略。</param>
        public void Tick(float deltaTime)
        {
            if (IsSettled || IsSuspended || !IsValidDelta(deltaTime))
                return;

            switch (Phase)
            {
                case Level105Phase.OpeningIdle:
                    TickTimedPhase(deltaTime, OpeningIdleDuration, Level105Phase.RunningToPit);
                    break;

                case Level105Phase.ChestDelay:
                    TickTimedPhase(deltaTime, ChestDelayDuration, Level105Phase.ChestConfirmEntering);
                    break;

                case Level105Phase.ChestConfirmShowing:
                    TickTimedPhase(deltaTime, ChestConfirmShowingDuration, Level105Phase.ChestConfirmClosing);
                    break;

                case Level105Phase.PitRestartPopupShowing:
                    TickTimedPhase(deltaTime, PitRestartPopupShowingDuration, Level105Phase.PitRestartPopupClosing);
                    break;

                case Level105Phase.ChestOpening:
                    TickTimedPhase(deltaTime, ChestOpeningDuration, Level105Phase.CoinPopupEntering);
                    break;

                case Level105Phase.CoinPopupShowing:
                    TickTimedPhase(deltaTime, CoinPopupShowingDuration, Level105Phase.RunningToFakeDoor);
                    break;

                case Level105Phase.FakeVictoryShowing:
                    TickTimedPhase(deltaTime, FakeVictoryShowingDuration, Level105Phase.FakeDoorExiting);
                    break;

                case Level105Phase.NetworkStuckWaiting:
                    TickTimedPhase(deltaTime, NetworkStuckDuration, Level105Phase.NetworkPopupEntering);
                    break;

                case Level105Phase.NetworkPopupShowing:
                    TickTimedPhase(deltaTime, NetworkPopupShowingDuration, Level105Phase.NetworkPopupClosing);
                    break;

                case Level105Phase.CallPopupShowing:
                    TickTimedPhase(deltaTime, CallPopupShowingDuration, Level105Phase.CallPopupClosing);
                    break;

                case Level105Phase.UpdatePopupShowing:
                    TickTimedPhase(deltaTime, UpdatePopupShowingDuration, Level105Phase.UpdatePopupClosing);
                    break;
            }
        }

        /// <summary>
        /// 设置本关本地挂起状态。挂起时 Tick、空间触发、坑底计数、钥匙拾取和弹窗入场/关闭通知均无副作用；
        /// 不修改全局 Time.timeScale。胜负终态不再接受挂起改写。
        /// </summary>
        /// <param name="suspended">true 挂起；false 从剩余阶段与剩余时间继续。</param>
        /// <returns>是否接受本次状态设置。</returns>
        public bool SetSuspended(bool suspended)
        {
            if (IsSettled)
                return false;

            IsSuspended = suspended;
            return true;
        }

        /// <summary>
        /// 标记首次起始门出场已经完整结束，或失败重开后的 Actor 已完成初始站位重置；随后才开始 5 秒等待。
        /// </summary>
        public bool NotifyOpeningCompleted()
        {
            if (CannotAdvance(Level105Phase.WaitingForOpening))
                return false;

            EnterPhase(Level105Phase.OpeningIdle);
            return true;
        }

        /// <summary>
        /// 玩家任何非白名单输入的唯一判负入口。系统面板挂起期间的系统流程交互不会调用本入口；
        /// 若误投递到已挂起规则，也会被忽略。
        /// </summary>
        /// <returns>本次输入是否锁定 Failure。</returns>
        public bool NotifyPlayerOperateInput()
        {
            if (IsSettled || IsSuspended)
                return false;

            Result = Level105Result.Failure;
            Phase = Level105Phase.Failure;
            return true;
        }

        /// <summary>
        /// 暂停、重来、提示三个系统入口的豁免登记；不改变阶段、计时、挂起或胜负结果。
        /// </summary>
        public bool NotifySystemButtonInput(Level105SystemButton button)
        {
            _ = button;
            return !IsSettled;
        }

        /// <summary>到达坑前触发点后进入继续向右掉入固定陷坑的阶段。</summary>
        public bool NotifyPitApproached()
        {
            if (CannotAdvance(Level105Phase.RunningToPit))
                return false;

            EnterPhase(Level105Phase.FallingIntoPit);
            return true;
        }

        /// <summary>
        /// 首次落到安全坑底后清零本次坑内钥匙状态，并进入坑底重开弹窗入场等待；
        /// Box 在坑底停住不开跳，弹窗完全移除后才进入五次原地垂直弹跳阶段。
        /// </summary>
        public bool NotifyPitFloorReached()
        {
            if (CannotAdvance(Level105Phase.FallingIntoPit))
                return false;

            CompletedPitBounceCount = 0;
            HasChestKey = false;
            EnterPhase(Level105Phase.PitRestartPopupEntering);
            return true;
        }

        /// <summary>真实坑底重开弹窗入场已经完成，从此刻开始完整展示 5 秒。</summary>
        public bool NotifyPitRestartPopupEntered()
        {
            if (CannotAdvance(Level105Phase.PitRestartPopupEntering))
                return false;

            EnterPhase(Level105Phase.PitRestartPopupShowing);
            return true;
        }

        /// <summary>
        /// 坑底重开弹窗已经完全关闭。只有关闭等待阶段才接受该通知并进入坑底原地弹跳；
        /// 重复、乱序或挂起通知均无副作用。
        /// </summary>
        public bool NotifyPitRestartPopupClosed()
        {
            if (CannotAdvance(Level105Phase.PitRestartPopupClosing))
                return false;

            EnterPhase(Level105Phase.PitBouncing);
            return true;
        }

        /// <summary>
        /// 上报一次“从坑底起跳并重新落回坑底”的完整闭环。前四次保持 PitBouncing；
        /// 第五次只有已经取得坑中钥匙才会计数并进入独立第 6 次 PitEscaping。
        /// </summary>
        public bool NotifyPitBounceLanded()
        {
            if (CannotAdvance(Level105Phase.PitBouncing))
                return false;

            bool isFinalRequiredLanding = CompletedPitBounceCount == RequiredPitBounceCount - 1;
            if (isFinalRequiredLanding && !HasChestKey)
                return false;

            CompletedPitBounceCount++;
            if (CompletedPitBounceCount >= RequiredPitBounceCount)
                EnterPhase(Level105Phase.PitEscaping);

            return true;
        }

        /// <summary>
        /// 上报第 5 次坑底跳跃进行中的钥匙接触。只有 PitBouncing 且已经完成恰好 4 次落地时可成功一次；
        /// 拾取不改变当前阶段，也不暂停该次跳跃。
        /// </summary>
        public bool NotifyChestKeyCollected()
        {
            if (CannotAdvance(Level105Phase.PitBouncing) ||
                CompletedPitBounceCount != RequiredPitBounceCount - 1 ||
                HasChestKey)
            {
                return false;
            }

            HasChestKey = true;
            return true;
        }

        /// <summary>独立第 6 次脱坑跳已经落上右侧地面，继续奔向关闭宝箱左侧的停点。</summary>
        public bool NotifyPitEscapeLanded()
        {
            if (CannotAdvance(Level105Phase.PitEscaping))
                return false;

            EnterPhase(Level105Phase.RunningToChest);
            return true;
        }

        /// <summary>到达关闭宝箱左侧停点并完全静止，进入无弹窗的 2 秒场景等待。</summary>
        public bool NotifyChestReached()
        {
            if (CannotAdvance(Level105Phase.RunningToChest))
                return false;

            EnterPhase(Level105Phase.ChestDelay);
            return true;
        }

        /// <summary>真实开箱确认弹窗入场已经完成，从此刻开始完整展示 5 秒。</summary>
        public bool NotifyChestConfirmEntered()
        {
            if (CannotAdvance(Level105Phase.ChestConfirmEntering))
                return false;

            EnterPhase(Level105Phase.ChestConfirmShowing);
            return true;
        }

        /// <summary>
        /// 开箱确认弹窗已经完全关闭。只有关闭等待阶段且仍持有钥匙时才原子消费钥匙并开始 1.2 秒开箱；
        /// 重复、乱序或挂起通知均无副作用。
        /// </summary>
        public bool NotifyChestConfirmClosed()
        {
            if (CannotAdvance(Level105Phase.ChestConfirmClosing) || !HasChestKey)
                return false;

            HasChestKey = false;
            EnterPhase(Level105Phase.ChestOpening);
            return true;
        }

        /// <summary>真实金币弹窗入场已经完成，从此刻开始完整展示 5 秒。</summary>
        public bool NotifyCoinPopupEntered()
        {
            if (CannotAdvance(Level105Phase.CoinPopupEntering))
                return false;

            EnterPhase(Level105Phase.CoinPopupShowing);
            return true;
        }

        /// <summary>到达世界 X=7.6 的假门停点后进入假门进门成功表现，不直接弹假胜利，也不增加额外空停顿。</summary>
        public bool NotifyFakeDoorReached()
        {
            if (CannotAdvance(Level105Phase.RunningToFakeDoor))
                return false;

            EnterPhase(Level105Phase.FakeDoorEntering);
            return true;
        }

        /// <summary>
        /// 假门进门成功表现（开门、彩带、进门消失、关门、关门音效、成功震动、彩虹粒子与收尾）已全部完成；
        /// 只有收到该通知后才允许打开假胜利面板。
        /// </summary>
        public bool NotifyFakeDoorEnterCompleted()
        {
            if (CannotAdvance(Level105Phase.FakeDoorEntering))
                return false;

            EnterPhase(Level105Phase.FakeVictoryEntering);
            return true;
        }

        /// <summary>真实假胜利面板入场已经完成，从此刻开始完整展示 5 秒。</summary>
        public bool NotifyFakeVictoryEntered()
        {
            if (CannotAdvance(Level105Phase.FakeVictoryEntering))
                return false;

            EnterPhase(Level105Phase.FakeVictoryShowing);
            return true;
        }

        /// <summary>
        /// 假门重新开门、同一 Box 向左出门与假门完整关门（含关门音效）已全部结束；
        /// 只有收到该通知后才进入奔向真门阶段，Box 随后自动转右继续。
        /// </summary>
        public bool NotifyFakeDoorExitCompleted()
        {
            if (CannotAdvance(Level105Phase.FakeDoorExiting))
                return false;

            EnterPhase(Level105Phase.RunningToRealDoor);
            return true;
        }

        /// <summary>Box 到达镜头即将切到第二屏的节点；随后进入原地踏步卡顿，卡顿结束才弹断网假弹窗。</summary>
        public bool NotifyNetworkNodeReached()
        {
            if (CannotAdvance(Level105Phase.RunningToRealDoor))
                return false;

            EnterPhase(Level105Phase.NetworkStuckWaiting);
            return true;
        }

        /// <summary>真实断网假弹窗入场已经完成，从此刻开始完整展示 5 秒。</summary>
        public bool NotifyNetworkPopupEntered()
        {
            if (CannotAdvance(Level105Phase.NetworkPopupEntering))
                return false;

            EnterPhase(Level105Phase.NetworkPopupShowing);
            return true;
        }

        /// <summary>断网假弹窗已经完全关闭；只有关闭等待阶段才接受该通知并继续奔向中间路程的来电节点。</summary>
        public bool NotifyNetworkPopupClosed()
        {
            if (CannotAdvance(Level105Phase.NetworkPopupClosing))
                return false;

            EnterPhase(Level105Phase.RunningToCallNode);
            return true;
        }

        /// <summary>Box 到达中间路程的来电节点并停住；随后请求打开来电横幅。</summary>
        public bool NotifyCallNodeReached()
        {
            if (CannotAdvance(Level105Phase.RunningToCallNode))
                return false;

            EnterPhase(Level105Phase.CallPopupEntering);
            return true;
        }

        /// <summary>来电横幅下滑入场已经完成，从此刻开始完整展示 5 秒。</summary>
        public bool NotifyCallPopupEntered()
        {
            if (CannotAdvance(Level105Phase.CallPopupEntering))
                return false;

            EnterPhase(Level105Phase.CallPopupShowing);
            return true;
        }

        /// <summary>来电横幅已经完全关闭；只有关闭等待阶段才接受该通知并继续奔向游戏更新弹窗节点。</summary>
        public bool NotifyCallPopupClosed()
        {
            if (CannotAdvance(Level105Phase.CallPopupClosing))
                return false;

            EnterPhase(Level105Phase.RunningToUpdateNode);
            return true;
        }

        /// <summary>Box 到达真门左侧 5 个世界单位的游戏更新弹窗节点；随后请求打开游戏更新假弹窗。</summary>
        public bool NotifyUpdateNodeReached()
        {
            if (CannotAdvance(Level105Phase.RunningToUpdateNode))
                return false;

            EnterPhase(Level105Phase.UpdatePopupEntering);
            return true;
        }

        /// <summary>真实游戏更新假弹窗入场已经完成，从此刻开始完整展示 5 秒。</summary>
        public bool NotifyUpdatePopupEntered()
        {
            if (CannotAdvance(Level105Phase.UpdatePopupEntering))
                return false;

            EnterPhase(Level105Phase.UpdatePopupShowing);
            return true;
        }

        /// <summary>游戏更新假弹窗已经完全关闭；只有关闭等待阶段才接受该通知并继续奔向真门。</summary>
        public bool NotifyUpdatePopupClosed()
        {
            if (CannotAdvance(Level105Phase.UpdatePopupClosing))
                return false;

            EnterPhase(Level105Phase.ApproachingRealDoor);
            return true;
        }

        /// <summary>
        /// 标准真门触发器已经判胜。该入口允许在本关挂起状态下提交，以保证同帧胜利优先；
        /// 一旦成功，规则立即终结，后续任何输入均安全。假门之后的各段右行都允许提交，
        /// 保证触发标定偏差时真门判胜仍然同帧优先。
        /// </summary>
        public bool TryEnterRealDoor()
        {
            if (IsSettled || !IsRealDoorRunPhase)
                return false;

            Phase = Level105Phase.Victory;
            Result = Level105Result.Victory;
            IsSuspended = false;
            return true;
        }

        #endregion

        #region Internal Rules

        private float _phaseElapsed;

        private bool IsRealDoorRunPhase =>
            Phase == Level105Phase.RunningToRealDoor ||
            Phase == Level105Phase.RunningToCallNode ||
            Phase == Level105Phase.RunningToUpdateNode ||
            Phase == Level105Phase.ApproachingRealDoor;

        private bool CannotAdvance(Level105Phase expectedPhase)
        {
            return IsSettled || IsSuspended || Phase != expectedPhase;
        }

        private static bool IsValidDelta(float deltaTime)
        {
            return !float.IsNaN(deltaTime) &&
                   !float.IsInfinity(deltaTime) &&
                   deltaTime > 0f;
        }

        private void TickTimedPhase(float deltaTime, float duration, Level105Phase nextPhase)
        {
            _phaseElapsed += deltaTime;
            if (_phaseElapsed >= Math.Max(0f, duration))
                EnterPhase(nextPhase);
        }

        private void EnterPhase(Level105Phase next)
        {
            Phase = next;
            _phaseElapsed = 0f;
        }

        #endregion
    }
}
