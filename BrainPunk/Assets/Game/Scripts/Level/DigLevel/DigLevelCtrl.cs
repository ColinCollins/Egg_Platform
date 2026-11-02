using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Dig
{
    /// <summary>Dig 关卡生命周期、统一挖掘输入与物理批次结果；不负责 UI/存档/重建。</summary>
    public sealed class DigLevelCtrl : MonoBehaviour, IDigLevelRuntime
    {
        #region Prefab 绑定
        /// <summary>关卡专属正交相机，用于将屏幕笔画投影至 Z=0 平面。</summary>
        [SerializeField] private Camera inputCamera;
        /// <summary>独立主角嵌套 Prefab 实例。</summary>
        [SerializeField, HideInInspector] private DigActorCtrl actor;
        /// <summary>本关参与角色的显式绑定列表；旧Prefab未填写时复用历史actor引用，不运行时扫描场景。</summary>
        [SerializeField, LabelText("场上角色（Actors）")]
        [Tooltip("绑定全部参与角色。全部到达各自匹配的目标才胜利；旧Prefab未填写时继续使用原单角色绑定。")]
        private DigActorCtrl[] actors = Array.Empty<DigActorCtrl>();
        /// <summary>本关全部病菌；顺序只用于检查和表现，不影响批次胜负。</summary>
        [SerializeField] private DigGermCtrl[] germs;
        /// <summary>视觉与 Tilemap 共用笔刷路径的地形控制器。</summary>
        [SerializeField] private DigTerrainCtrl terrain;
        /// <summary>本关独立挖土粒子实例；关闭自动发射并使用世界空间，显式绑定在关卡根下。</summary>
        [SerializeField, LabelText("挖土粒子")]
        private ParticleSystem diggingParticles;
        /// <summary>每段实际挖到新沙时发出的圆粒数量；空洞、无墨及输入关闭时不发射。</summary>
        [SerializeField, Range(1, 12), LabelText("单次挖土粒子数")]
        private int diggingParticleCount = 4;
        /// <summary>本关自然胜利条件；默认保留001的全部吞噬模式，到达模式只由同编号目标触发。</summary>
        [SerializeField] private DigVictoryCondition victoryCondition = DigVictoryCondition.ConsumeAllGerms;
        /// <summary>锁定结果后的最短等待游戏秒数；暂停／后台不推进，0跳过计时，吞噬胜利仍须等 hiccup 完成。</summary>
        [SerializeField, Min(0f)] private float victoryDelay = 1f;
        /// <summary>与Draw相同的墨水来源开关；默认关闭使用Prefab容量和阈值，开启时仅在初始化前接受DigLevelData覆盖。</summary>
        [Tooltip("是否使用 DigLevelData 覆盖墨水配置；关闭时使用当前 Prefab Inspector 的 maxInkLength 和 inkStarStage。")]
        [SerializeField] private bool isConfig = false;
        /// <summary>本关基础挖掘轨迹容量，单位为世界长度；广告增加一份相同容量。</summary>
        [SerializeField, Min(0.01f)] private float maxInkLength = 20f;
        /// <summary>一/二/三星所需剩余墨水绝对长度；与 HUD 同源，默认 0/4/10。</summary>
        [SerializeField] private Vector3 inkStarStage = new Vector3(0f, 4f, 10f);
        /// <summary>本关提示表现的完整根对象，由 Prefab 显式绑定；运行时统一显隐根对象及其全部子节点。</summary>
        [LabelText("提示对象")]
        [Tooltip("绑定提示 Prefab 的完整根对象；购买提示后会连同图标、路径等子节点一起显示。")]
        [SerializeField] private GameObject[] hintObjects = Array.Empty<GameObject>();
        #endregion

        #region 运行状态与契约
        /// <summary>每角色独立的接触快照与到达状态；只在关卡初始化时创建并复用集合。</summary>
        private sealed class ActorState
        {
            internal readonly DigActorCtrl Actor;
            internal readonly HashSet<DigGermCtrl> Contacts = new HashSet<DigGermCtrl>();
            internal readonly List<DigGermCtrl> Batch = new List<DigGermCtrl>();
            internal int ContactSize;
            internal bool Reached;
            internal bool Consuming;
            internal bool ConsumeCompleted;
            internal ActorState(DigActorCtrl current) => Actor = current;
        }

        private ActorState[] _actorStates = Array.Empty<ActorState>();
        private readonly Dictionary<DigActorCtrl, ActorState> _actorLookup = new Dictionary<DigActorCtrl, ActorState>();
        private readonly List<int> _contactSizes = new List<int>();
        private bool _hasContacts;
        private int _reachedActors;
        private DigActorCtrl _failingActor;
        private bool _inputEnabled, _paused = true, _backgroundPaused, _submitted;
        private bool _dragging, _strokeDug;
        private int _fingerId = -1, _remaining, _pendingAnimations;
        private Vector2 _lastPoint;
        private int _generation;
        private float _inkCapacity, _usedInk;
        private bool _victoryStarted;
        private float _victoryTimeRemaining;
        private int _pendingVictoryAnimations;
        private bool _failureStarted;
        private bool _failureAnimationCompleted;
        private float _failureTimeRemaining;
        /// <inheritdoc />
        public bool HasHint
        {
            get
            {
                if (hintObjects == null) return false;
                foreach (GameObject hint in hintObjects)
                {
                    if (hint) return true;
                }
                return false;
            }
        }
        /// <inheritdoc />
        public DrawInkState InkState
        {
            get
            {
                float remaining = Mathf.Max(0f, _inkCapacity - _usedInk);
                int stars = remaining >= inkStarStage.z ? 3 : remaining >= inkStarStage.y ? 2 : remaining >= inkStarStage.x ? 1 : 0;
                return new DrawInkState(maxInkLength, _inkCapacity, _usedInk, stars, inkStarStage);
            }
        }
        /// <inheritdoc />
        public event Action<DrawInkState> InkChanged;
        /// <inheritdoc />
        public bool IsRuntimeReady { get; private set; }
        /// <inheritdoc />
        public bool IsResultLocked { get; private set; }
        /// <inheritdoc />
        public event Action<DigLevelResult> Resolved;
        /// <summary>当前关卡暂停或应用切后台；供局部失败／倒计时脚本停止推进，不包含结果锁。</summary>
        public bool IsPaused => _paused || _backgroundPaused;
        /// <summary>仅本关 Actor 读取到达模式的有效按住画线状态；暂停／结束不产生紧张表现。</summary>
        internal bool WantsNervousAnimation => victoryCondition == DigVictoryCondition.ReachTarget &&
            _dragging && _inputEnabled && !IsPaused && !IsResultLocked;
        /// <summary>玩家正常松手且本笔实际挖到新沙时通知本关组件；取消、暂停、关闭输入与退出不通知。</summary>
        public event Action StrokeCompleted;
        /// <inheritdoc />
        public int CountdownSeconds { get; private set; }
        /// <inheritdoc />
        public event Action<int> CountdownChanged;

        /// <summary>仅由本关倒计时组件发布最后五秒快照；0隐藏，重复整数不通知，胜负锁定后强制隐藏。</summary>
        internal void SetCountdownSeconds(int seconds)
        {
            int next = !IsRuntimeReady || IsResultLocked ? 0 : Mathf.Clamp(seconds, 0, 5);
            if (CountdownSeconds == next) return;
            CountdownSeconds = next;
            CountdownChanged?.Invoke(next);
        }

        /// <summary>仅isConfig开启时接受Dig控制器注入的Luban墨水与评星阈值；关闭时保留Prefab值，必须在运行态初始化前调用。</summary>
        public void ConfigureInk(float capacity, Vector3 stages)
        {
            if (!Application.isPlaying || IsRuntimeReady || !isConfig) return;
            maxInkLength = capacity;
            inkStarStage = stages;
        }

        /// <inheritdoc />
        public void InitializeRuntime()
        {
            if (IsRuntimeReady) return;
            if (!Application.isPlaying || !inputCamera || !terrain ||
                (victoryCondition == DigVictoryCondition.ConsumeAllGerms && (germs == null || germs.Length == 0)))
                throw new InvalidOperationException("DigLevel Prefab 绑定不完整或非运行态初始化。");
            if ((victoryCondition != DigVictoryCondition.ConsumeAllGerms && victoryCondition != DigVictoryCondition.ReachTarget) ||
                float.IsNaN(victoryDelay) || float.IsInfinity(victoryDelay) || victoryDelay < 0f)
                throw new InvalidOperationException("Dig 胜利条件或延迟配置无效。");
            germs ??= Array.Empty<DigGermCtrl>();
            DigActorCtrl[] participants = actors != null && actors.Length > 0 ? actors : new[] { actor };
            _actorLookup.Clear();
            var runtimeStates = new ActorState[participants.Length];
            for (int i = 0; i < participants.Length; i++)
            {
                DigActorCtrl current = participants[i];
                if (!current || _actorLookup.ContainsKey(current))
                    throw new InvalidOperationException("角色引用缺失或重复，请显式绑定全部参与角色。");
                var state = new ActorState(current);
                runtimeStates[i] = state;
                _actorLookup.Add(current, state);
            }
            _actorStates = runtimeStates;
            long maxSize = 0;
            foreach (ActorState state in _actorStates)
            {
                state.Actor.Initialize(this);
                maxSize = Math.Max(maxSize, state.Actor.CurrentSize);
            }
            var unique = new HashSet<DigGermCtrl>();
            foreach (DigGermCtrl germ in germs)
            {
                if (!germ || !unique.Add(germ)) throw new InvalidOperationException("病菌引用缺失或重复。");
                germ.Initialize();
                maxSize += germ.GrowthSize;
            }
            if (maxSize > int.MaxValue) throw new InvalidOperationException("病菌成长总和超出整数范围。");
            terrain.BindRuntime();
            if (diggingParticles) diggingParticles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            maxInkLength = Mathf.Max(0.01f, maxInkLength);
            inkStarStage.x = Mathf.Clamp(inkStarStage.x, 0f, maxInkLength);
            inkStarStage.y = Mathf.Clamp(inkStarStage.y, inkStarStage.x, maxInkLength);
            inkStarStage.z = Mathf.Clamp(inkStarStage.z, inkStarStage.y, maxInkLength);
            _inkCapacity = maxInkLength;
            _usedInk = 0f;
            ShowHint(false);
            _remaining = germs.Length;
            IsRuntimeReady = true;
        }

        /// <inheritdoc />
        public void AddRewardInk()
        {
            if (!IsRuntimeReady || IsResultLocked) return;
            _inkCapacity += maxInkLength;
            InkChanged?.Invoke(InkState);
        }

        /// <inheritdoc />
        public void ShowHint(bool visible)
        {
            if (!Application.isPlaying || hintObjects == null) return;
            foreach (GameObject hint in hintObjects) if (hint) hint.SetActive(visible);
        }

        /// <inheritdoc />
        public bool TryCompleteByHint()
        {
            if (!IsRuntimeReady || !isActiveAndEnabled || IsResultLocked || _paused || _backgroundPaused) return false;
            return BeginVictory();
        }

        /// <inheritdoc />
        public void SetInputEnabled(bool enabled)
        {
            _inputEnabled = enabled && !IsResultLocked;
            if (!_inputEnabled) EndStroke();
        }

        /// <inheritdoc />
        public void SetPaused(bool paused)
        {
            _paused = paused;
            ApplyPause();
        }

        private void ApplyPause()
        {
            if (!IsRuntimeReady) return;
            bool pause = _paused || _backgroundPaused;
            if (diggingParticles)
            {
                if (pause) diggingParticles.Pause(false);
                else if (diggingParticles.isPaused) diggingParticles.Play(false);
            }
            foreach (ActorState state in _actorStates)
            {
                state.Actor.SetFrozen(pause || IsResultLocked || state.Reached);
                state.Actor.SetAnimationPaused(pause || _victoryStarted || state.Reached ||
                    (IsResultLocked && state.Actor != _failingActor));
                if (_pendingVictoryAnimations > 0) state.Actor.SetSpinePaused(pause);
            }
            foreach (DigGermCtrl germ in germs) germ.SetPaused(pause || IsResultLocked);
            if (pause) EndStroke();
        }
        #endregion

        #region 物理批次与唯一结果
        /// <summary>仅由主角碰撞回调记录；下一次 FixedUpdate 在新物理模拟前统一结算上一物理步。</summary>
        /// <param name="contactingActor">实际发生接触的角色；仅接受本关初始化列表中尚未到达的角色。</param>
        /// <param name="germ">当前接触病菌；按所属角色的批次开始大小判定，只能被认领一次。</param>
        public void QueueContact(DigActorCtrl contactingActor, DigGermCtrl germ)
        {
            if (!IsRuntimeReady || IsResultLocked || _paused || _backgroundPaused ||
                !contactingActor || !germ || germ.IsClaimed ||
                !_actorLookup.TryGetValue(contactingActor, out ActorState state) || state.Reached) return;
            if (Array.IndexOf(germs, germ) < 0) return;
            if (state.Contacts.Count == 0) state.ContactSize = contactingActor.CurrentSize;
            state.Contacts.Add(germ);
            _hasContacts = true;
        }

        private void FixedUpdate()
        {
            if (!IsRuntimeReady || IsResultLocked || _paused || _backgroundPaused) return;
            bool consumed = false;
            foreach (ActorState state in _actorStates)
            {
                if (!state.Consuming || !state.ConsumeCompleted || state.Actor.IsEating) continue;
                state.Consuming = false;
                state.ConsumeCompleted = false;
                _pendingAnimations--;
                if (_remaining > 0) state.Actor.WakeForNextContact();
                consumed = true;
            }
            if (consumed && TrySucceed()) return;
            if (!_hasContacts) return;
            _hasContacts = false;
            foreach (ActorState state in _actorStates)
            {
                _contactSizes.Clear();
                state.Batch.Clear();
                foreach (DigGermCtrl germ in state.Contacts)
                {
                    if (germ && !germ.IsClaimed) { state.Batch.Add(germ); _contactSizes.Add(germ.CurrentSize); }
                }
                state.Contacts.Clear();
                if (state.Batch.Count > 0 && !DigSizeRules.CanConsumeBatch(state.ContactSize, _contactSizes))
                {
                    BeginFailure(state.Actor, 0f);
                    return;
                }
            }

            int generation = _generation;
            foreach (ActorState state in _actorStates)
            {
                if (state.Consuming || state.Actor.IsEating) continue;
                foreach (DigGermCtrl germ in state.Batch)
                {
                    if (!germ.TryClaim()) continue;
                    state.Consuming = true;
                    state.ConsumeCompleted = false;
                    _remaining--;
                    _pendingAnimations++;
                    state.Actor.Grow(germ.GrowthSize);
                    germ.PlayConsume(state.Actor.transform, () => OnConsumed(generation, state));
                    break;
                }
            }
        }

        /// <summary>结果锁定或退出时清理接触与单次吞噬占用；不执行成长或补发完成回调。</summary>
        private void ClearContacts()
        {
            _hasContacts = false;
            _pendingAnimations = 0;
            foreach (ActorState state in _actorStates)
            {
                state.Contacts.Clear();
                state.Batch.Clear();
                state.Consuming = false;
                state.ConsumeCompleted = false;
            }
        }

        private void OnConsumed(int generation, ActorState state)
        {
            if (generation != _generation || !isActiveAndEnabled || IsResultLocked || !state.Consuming) return;
            state.ConsumeCompleted = true;
        }

        /// <summary>吞噬模式自然胜利；全部病菌吸入和eat结束后等待hiccup与统一延迟，不放宽大小判定或结束到达模式。</summary>
        public bool TrySucceed()
        {
            if (victoryCondition != DigVictoryCondition.ConsumeAllGerms ||
                !IsRuntimeReady || !isActiveAndEnabled || IsResultLocked || _remaining != 0 || _pendingAnimations != 0)
                return false;
            return BeginVictory(true);
        }

        /// <summary>
        /// 到达模式的角色确认入口；仅接受本关参与列表中未到达且编号匹配的角色，全部到达才开始胜利延迟。
        /// 调用边界：非暂停／后台的运行态接触；吞噬模式、死亡／胜利已锁以及其它关卡主角均拒绝。
        /// </summary>
        /// <param name="arrivingActor">触发目标区域的实际 Actor，由附着刚体确定身份。</param>
        /// <param name="targetIndex">目标区域 Inspector 中配置的匹配编号。</param>
        public bool TryReachTarget(DigActorCtrl arrivingActor, int targetIndex)
        {
            if (victoryCondition != DigVictoryCondition.ReachTarget ||
                !IsRuntimeReady || !isActiveAndEnabled || IsResultLocked || _paused || _backgroundPaused ||
                !arrivingActor || !_actorLookup.TryGetValue(arrivingActor, out ActorState state) ||
                state.Reached || arrivingActor.Index != targetIndex)
                return false;
            state.Reached = true;
            state.Contacts.Clear();
            state.Batch.Clear();
            _reachedActors++;
            arrivingActor.SetFrozen(true);
            arrivingActor.SetAnimationPaused(true);
            if (_reachedActors == _actorStates.Length) BeginVictory();
            return true;
        }

        /// <summary>仅供活动 Dig 控制器的 GM 胜利命令调用；拒绝编辑态、暂停／后台，复用原结果锁与胜利延迟。</summary>
        public bool TryForceVictory()
        {
            if (!Application.isPlaying || IsPaused) return false;
            return BeginVictory();
        }

        /// <summary>所有成功来源共用收尾；自然吞噬并行等待 hiccup 和原延迟，其它入口只等待原延迟。</summary>
        private bool BeginVictory(bool playHiccup = false)
        {
            if (!IsRuntimeReady || !isActiveAndEnabled || IsResultLocked) return false;
            IsResultLocked = true;
            _victoryStarted = true;
            SetCountdownSeconds(0);
            _victoryTimeRemaining = victoryDelay;
            _pendingVictoryAnimations = playHiccup ? _actorStates.Length : 0;
            SetInputEnabled(false);
            ClearContacts();
            ApplyPause();
            if (playHiccup)
            {
                int generation = _generation;
                foreach (ActorState state in _actorStates)
                    if (!state.Actor.PlayHiccup(() => OnVictoryAnimationComplete(generation)))
                        _pendingVictoryAnimations--;
            }
            TrySubmitVictory();
            return true;
        }

        /// <summary>仅接受本关当前胜利演出的完成；退出后的 Spine 晚到回调不能提交旧结果。</summary>
        private void OnVictoryAnimationComplete(int generation)
        {
            if (generation != _generation || !isActiveAndEnabled || !_victoryStarted || _pendingVictoryAnimations <= 0) return;
            _pendingVictoryAnimations--;
            TrySubmitVictory();
        }

        /// <summary>保留配置等待下限，并确保全部 hiccup 真正完成；暂停／后台期间不提交结果。</summary>
        private void TrySubmitVictory()
        {
            if (_victoryStarted && !_submitted && !IsPaused && _pendingVictoryAnimations == 0 && _victoryTimeRemaining <= 0f)
                Submit(DigLevelResult.Succeeded);
        }

        /// <summary>仅胜利等待期间推进游戏时间；禁用后取消，暂停／后台保持剩余时间，不用 Tween 或延迟回调。</summary>
        private void TickVictoryDelay()
        {
            if (!_victoryStarted || _submitted || _paused || _backgroundPaused) return;
            _victoryTimeRemaining -= Time.deltaTime;
            TrySubmitVictory();
        }

        /// <summary>
        /// 局部墨水、超时或陷阱条件的统一失败入口；先锁结果并播放原死亡，完成后由玩法控制器统计和重开。
        /// 仅接受本关尚未到达的参与角色；暂停／后台、失活、未初始化或已锁胜负时拒绝，不直接派发重置事件。
        /// </summary>
        /// <param name="failingActor">陷阱传实际碰撞角色；全关条件省略时使用列表中第一个尚未到达的角色。</param>
        /// <returns>本次是否接受失败；重复或无效请求返回false且不改变已有结果。</returns>
        public bool TryFail(DigActorCtrl failingActor = null)
            => TryFail(failingActor, 0f);

        /// <summary>
        /// 带最短失败等待的统一入口；等待从接受失败时开始，以游戏时间推进，并与死亡动画并行。
        /// 仅供需要控制失败演出节奏的本关组件调用；0保持原死亡完成即提交的行为。
        /// </summary>
        /// <param name="failingActor">实际失败角色，必须属于本关且尚未到达目标。</param>
        /// <param name="failureDelay">从接受失败到提交结果的最短游戏秒数；暂停／后台不推进。</param>
        /// <returns>本次是否接受失败。</returns>
        public bool TryFail(DigActorCtrl failingActor, float failureDelay)
            => TryFail(failingActor, failureDelay, true);

        /// <summary>失败来源显式选择是否先上跳；其余角色校验、暂停、等待和唯一结果门禁与原入口一致。</summary>
        /// <param name="failingActor">本关实际失败角色；为空时选择首个尚未到达者。</param>
        /// <param name="failureDelay">从接受失败到提交结果的最短游戏秒数。</param>
        /// <param name="playDeathHop">FailArea传false直接下坠；原有其它来源默认true。</param>
        /// <returns>本次是否接受失败。</returns>
        public bool TryFail(DigActorCtrl failingActor, float failureDelay, bool playDeathHop)
            => TryFail(failingActor, failureDelay, playDeathHop, null);

        /// <summary>仅供本关炸弹提交相对方向的烧焦飞出；复用统一门禁与失败清理，不改变其它死亡来源。</summary>
        /// <param name="failingActor">实际命中角色，必须属于本关且未到达。</param>
        /// <param name="failureDelay">与飞出演出并行的最短失败等待。</param>
        /// <param name="explosion">本次爆炸的世界中心和演出参数；无效值不锁定失败。</param>
        internal bool TryFailFromExplosion(DigActorCtrl failingActor, float failureDelay, DigExplosionDeath explosion)
            => explosion.IsValid && TryFail(failingActor, failureDelay, true, explosion);

        private bool TryFail(DigActorCtrl failingActor, float failureDelay, bool playDeathHop, DigExplosionDeath? explosion)
        {
            if (!Application.isPlaying || !IsRuntimeReady || !isActiveAndEnabled || IsResultLocked || IsPaused ||
                float.IsNaN(failureDelay) || float.IsInfinity(failureDelay) || failureDelay < 0f) return false;
            if (failingActor)
            {
                if (!_actorLookup.TryGetValue(failingActor, out ActorState state) || state.Reached) return false;
            }
            else
            {
                foreach (ActorState state in _actorStates)
                    if (!state.Reached) { failingActor = state.Actor; break; }
                if (!failingActor) return false;
            }
            BeginFailure(failingActor, failureDelay, playDeathHop, explosion);
            return true;
        }

        private void BeginFailure(DigActorCtrl failingActor, float failureDelay, bool playDeathHop = true,
            DigExplosionDeath? explosion = null)
        {
            IsResultLocked = true;
            _failureStarted = true;
            _failureAnimationCompleted = false;
            _failureTimeRemaining = failureDelay;
            _failingActor = failingActor;
            SetCountdownSeconds(0);
            SetInputEnabled(false);
            ClearContacts();
            ApplyPause();
            _generation++;
            foreach (DigGermCtrl germ in germs) germ.StopConsumption();
            int generation = _generation;
            Action completed = () =>
            {
                if (generation != _generation || !isActiveAndEnabled) return;
                _failureAnimationCompleted = true;
                TrySubmitFailure();
            };
            if (explosion.HasValue) failingActor.PlayExplosionDeath(inputCamera, completed, explosion.Value);
            else failingActor.PlayDeath(inputCamera, completed, playDeathHop);
        }

        /// <summary>推进命中至失败提交的最短游戏时间；暂停／后台保持剩余时间，禁用后由generation取消旧回调。</summary>
        private void TickFailureDelay()
        {
            if (!_failureStarted || _submitted || _paused || _backgroundPaused) return;
            _failureTimeRemaining -= Time.deltaTime;
            TrySubmitFailure();
        }

        private void TrySubmitFailure()
        {
            if (_failureStarted && _failureAnimationCompleted && _failureTimeRemaining <= 0f)
                Submit(DigLevelResult.Failed);
        }

        private void Submit(DigLevelResult result)
        {
            if (_submitted) return;
            _submitted = true;
            Resolved?.Invoke(result);
        }
        #endregion

#if UNITY_EDITOR
        #region 编辑器引用收集
        /// <summary>
        /// 显式点击时按通关条件收集本关子层级组件，含inactive并排除嵌套关卡；编辑态支持Undo并需保存Prefab，播放态只更新当前实例。
        /// 两种模式均收集可吞噬病菌；到达模式另绑定本关目标的Level。不初始化玩法、修改目标编号或烘焙地图。
        /// 两种模式均收集标记的完整提示根并保留本关有效手工提示，不切换提示显隐。
        /// </summary>
        [Button("按通关条件收集", ButtonSizes.Large)]
        private void CollectLevelObjects()
        {
            UnityEditor.Undo.RecordObject(this, "Dig 收集关卡引用");
            actors = Array.FindAll(GetComponentsInChildren<DigActorCtrl>(true),
                item => item.GetComponentInParent<DigLevelCtrl>(true) == this);
            actor = actors.Length > 0 ? actors[0] : null;
            var collectedHints = new List<GameObject>();
            if (hintObjects != null)
                foreach (GameObject hint in hintObjects)
                    if (hint && hint != gameObject && hint.transform.IsChildOf(transform) &&
                        hint.GetComponentInParent<DigLevelCtrl>(true) == this && !collectedHints.Contains(hint))
                        collectedHints.Add(hint);
            foreach (DigHintObject hint in GetComponentsInChildren<DigHintObject>(true))
                if (hint.gameObject != gameObject && hint.GetComponentInParent<DigLevelCtrl>(true) == this &&
                    !collectedHints.Contains(hint.gameObject))
                    collectedHints.Add(hint.gameObject);
            hintObjects = collectedHints.ToArray();
            germs = Array.FindAll(GetComponentsInChildren<DigGermCtrl>(true),
                item => item.GetComponentInParent<DigLevelCtrl>(true) == this);
            if (victoryCondition == DigVictoryCondition.ReachTarget)
            {
                foreach (DigTargetTrigger target in GetComponentsInChildren<DigTargetTrigger>(true))
                {
                    if (target.GetComponentInParent<DigLevelCtrl>(true) != this) continue;
                    using var serializedTarget = new UnityEditor.SerializedObject(target);
                    UnityEditor.SerializedProperty levelProperty = serializedTarget.FindProperty("level");
                    if (levelProperty.objectReferenceValue == this) continue;
                    levelProperty.objectReferenceValue = this;
                    serializedTarget.ApplyModifiedProperties();
                    UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                }
            }
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            if (!Application.isPlaying && gameObject.scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
        #endregion

        #region Editor 调试显示
        private GUIStyle _debugLabelStyle;

        /// <summary>仅 Editor 播放时显示本关数值；复用已绑定对象，不访问运行时存档或创建场景节点。</summary>
        private void OnGUI()
        {
            if (victoryCondition != DigVictoryCondition.ConsumeAllGerms) return;
            if (!Application.isPlaying || !IsRuntimeReady || IsResultLocked || Event.current.type != EventType.Repaint) return;

            _debugLabelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            Rect area = new Rect(Mathf.Max(12f, Screen.width - 272f), 96f, 260f, 36f + (_actorStates.Length + germs.Length) * 24f);
            GUI.Box(area, GUIContent.none);
            Rect line = new Rect(area.x + 10f, area.y + 8f, area.width - 20f, 24f);
            GUI.Label(line, "DIG TO EAT", _debugLabelStyle);
            for (int i = 0; i < _actorStates.Length; i++)
            {
                ActorState state = _actorStates[i];
                line.y += 24f;
                GUI.Label(line, $"Actor {i + 1}: {state.Actor.CurrentSize}" + (state.Reached ? " 已到达" : string.Empty), _debugLabelStyle);
            }
            for (int i = 0; i < germs.Length; i++)
            {
                DigGermCtrl germ = germs[i];
                if (!germ || !germ.isActiveAndEnabled || germ.IsClaimed) continue;
                line.y += 24f;
                GUI.Label(line, $"Germ {i + 1}: {germ.CurrentSize} (+{germ.GrowthSize})", _debugLabelStyle);
            }
        }
        #endregion
#endif

        #region 单指与鼠标输入
        private void Update()
        {
            if (IsRuntimeReady)
            {
                TickVictoryDelay();
                TickFailureDelay();
            }
            if (!IsRuntimeReady || !_inputEnabled || _paused || _backgroundPaused || IsResultLocked) return;
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (!_dragging && touch.phase == TouchPhase.Began && !OverUI(touch.fingerId))
                        BeginStroke(touch.position, touch.fingerId);
                    else if (_dragging && touch.fingerId == _fingerId)
                    {
                        if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                            EndStroke(touch.phase == TouchPhase.Ended);
                        else ContinueStroke(touch.position);
                    }
                }
                return;
            }
            if (_fingerId >= 0) { EndStroke(); return; }
            if (Input.GetMouseButtonDown(0) && !OverUI(-1)) BeginStroke(Input.mousePosition, -1);
            else if (_dragging && Input.GetMouseButton(0)) ContinueStroke(Input.mousePosition);
            if (Input.GetMouseButtonUp(0)) EndStroke(true);
        }

        private static bool OverUI(int pointer) => EventSystem.current && EventSystem.current.IsPointerOverGameObject(pointer);
        private Vector2 WorldPoint(Vector2 screen)
        {
            Ray ray = inputCamera.ScreenPointToRay(screen);
            return ray.GetPoint(-ray.origin.z / ray.direction.z);
        }
        private void BeginStroke(Vector2 screen, int finger)
        {
            _dragging = true;
            _strokeDug = false;
            _fingerId = finger;
            _lastPoint = WorldPoint(screen);
            TryDigSegment(_lastPoint, _lastPoint, true);
        }
        private void ContinueStroke(Vector2 screen)
        {
            Vector2 next = WorldPoint(screen);
            TryDigSegment(_lastPoint, next, false);
            _lastPoint = next;
        }

        /// <summary>当前输入路径的统一挖掘入口；只为擦到新沙的采样计世界长度，空洞免费，耗尽裁剪末段，物理继续运行。</summary>
        /// <param name="from">上一采样位置。</param>
        /// <param name="to">本次目标位置。</param>
        /// <param name="beginsStroke">擦到新沙的首个圆章至少消耗一个采样间距；在旧空洞点按不耗墨。</param>
        public bool TryDigSegment(Vector2 from, Vector2 to, bool beginsStroke)
        {
            if (!IsRuntimeReady || !_inputEnabled || _paused || _backgroundPaused || IsResultLocked || InkState.RemainingInk <= 0f) return false;
            float distance = Vector2.Distance(from, to);
            if (!beginsStroke && distance <= 0f) return false;
            int count = Mathf.Max(1, Mathf.CeilToInt(distance / terrain.StrokeStep));
            float cost = beginsStroke ? Mathf.Max(distance / count, terrain.StrokeStep) : distance / count;
            bool stamped = false;
            Vector2 lastDigPoint = from;
            Vector2 previous = from;
            for (int i = 1; i <= count; i++)
            {
                Vector2 point = Vector2.Lerp(from, to, i / (float)count);
                if (!terrain.ContainsPoint(point)) { previous = point; continue; }
                float remaining = Mathf.Max(0f, _inkCapacity - _usedInk);
                if (remaining <= 0f) break;
                float consumed = Mathf.Min(cost, remaining);
                Vector2 digPoint = !beginsStroke && consumed < cost ? Vector2.Lerp(previous, point, consumed / cost) : point;
                if (terrain.Stamp(digPoint))
                {
                    _usedInk = Mathf.Min(_inkCapacity, _usedInk + consumed);
                    stamped = true;
                    lastDigPoint = digPoint;
                }
                previous = point;
            }
            if (stamped)
            {
                _strokeDug |= _dragging;
                terrain.FlushCollider();
                if (diggingParticles)
                {
                    Vector3 position = diggingParticles.transform.position;
                    diggingParticles.transform.position = new Vector3(lastDigPoint.x, lastDigPoint.y, position.z);
                    if (!diggingParticles.isPlaying) diggingParticles.Play(false);
                    diggingParticles.Emit(Mathf.Clamp(diggingParticleCount, 1, 12));
                }
                InkChanged?.Invoke(InkState);
            }
            return stamped;
        }
        /// <summary>正常松手才结束有效笔画通知；所有强制关输入／暂停／失活路径只清理指针状态。</summary>
        private void EndStroke(bool completed = false)
        {
            bool notify = completed && _dragging && _strokeDug && IsRuntimeReady &&
                _inputEnabled && !IsPaused && !IsResultLocked;
            _dragging = false;
            _strokeDug = false;
            _fingerId = -1;
            if (notify) StrokeCompleted?.Invoke();
        }
        private void OnApplicationPause(bool paused) { _backgroundPaused = paused; ApplyPause(); }
        private void OnDisable()
        {
            if (diggingParticles) diggingParticles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            SetCountdownSeconds(0);
            _generation++;
            _victoryStarted = false;
            _victoryTimeRemaining = 0f;
            _pendingVictoryAnimations = 0;
            _failureStarted = false;
            _failureAnimationCompleted = false;
            _failureTimeRemaining = 0f;
            ClearContacts();
            EndStroke();
            if (IsRuntimeReady)
            {
                foreach (ActorState state in _actorStates) if (state.Actor) state.Actor.SetFrozen(true);
                foreach (DigGermCtrl germ in germs) if (germ) germ.StopConsumption();
            }
            Resolved = null;
            InkChanged = null;
            StrokeCompleted = null;
            CountdownChanged = null;
        }
        #endregion
    }
}
