using System;
using Bear.Fsm;
using Sirenix.OdinInspector;
using Spine;
using UnityEngine;
using UnityEngine.Scripting;

namespace Game.Dig
{
    public sealed partial class DigActorCtrl : IBearMachineOwner
    {
        #region Spine 绑定与状态
        /// <summary>Prefab 已有的 Spine 控制器，必须显式绑定；旧无 Spine 角色可留空，不扫描层级补找。</summary>
        [SerializeField, LabelText("Spine 控制器")] private ActorSpineCtrl spineCtrl;
        /// <summary>进入 scroll 的刚体移动速度（世界单位／秒）；低于一半时退出，避免接触抖动反复切换。</summary>
        [SerializeField, Min(0.01f), LabelText("滚动速度阈值")] private float scrollSpeedThreshold = 0.1f;

        private const string Idle = "idle", Scroll = "scroll", Eat = "eat", Nervous = "nervous", Hiccup = "hiccup";
        private StateMachine _animationMachine;
        private string _spineState;
        private TrackEntry _oneShotTrack;
        private Action _hiccupCompleted;
        private bool _spinePaused;
        private float _spineTimeScale = 1f;
        /// <summary>仅供本关吞噬调度判断eat是否结束；暂停仍视为播放中，无Spine时不额外等待。</summary>
        internal bool IsEating => _animationMachine != null && _spineState == Eat;
        #endregion

        #region 动画入口与生命周期
        /// <summary>只在关卡运行态初始化时创建局部状态机；显式注册节点，避免依赖全局反射扫描。</summary>
        private void InitializeAnimationState()
        {
            if (!spineCtrl) return;
            if (spineCtrl.AnimationState == null)
                throw new InvalidOperationException("DigActor 的 Spine 尚未初始化，请检查 Prefab 的 SkeletonAnimation 绑定。");
            foreach (string name in new[] { Idle, Scroll, Eat, Nervous, Hiccup })
                if (spineCtrl.AnimationState.Data.SkeletonData.FindAnimation(name) == null)
                    throw new InvalidOperationException($"DigActor 的 Spine 缺少动画：{name}");
            _spineTimeScale = spineCtrl.AnimationState.TimeScale;
            _animationMachine = new StateMachine(this);
            _animationMachine.Inject(typeof(IdleNode), typeof(ScrollNode), typeof(EatNode), typeof(NervousNode), typeof(HiccupNode));
            _animationMachine.Apply(typeof(DigActorCtrl));
        }

        private void Update()
        {
            if (!Application.isPlaying || _animationMachine == null || _dead || _spinePaused || _frozen) return;
            if (_spineState != Eat && _spineState != Hiccup) RefreshMovementAnimation();
        }

        /// <summary>移动优先于画线紧张；刚体锁旋转时仍可根据线速度判定滚动。</summary>
        private void RefreshMovementAnimation()
        {
            float threshold = Mathf.Max(0.01f, scrollSpeedThreshold) * (_spineState == Scroll ? 0.5f : 1f);
            bool moving = !_frozen && physicsBody.linearVelocity.sqrMagnitude > threshold * threshold;
            _animationMachine.Enter(moving ? Scroll : _level && _level.WantsNervousAnimation && !_frozen ? Nervous : Idle);
        }

        /// <summary>仅由成功认领病菌后的 Grow 调用；当前 eat 合并后续触发，不重置轨道时间或排队重播。</summary>
        private void PlayEat()
        {
            if (_animationMachine == null || _dead || _spineState == Eat || _spineState == Hiccup) return;
            _animationMachine.Enter(Eat);
        }

        /// <summary>仅自然吞噬胜利锁定后调用；播放一次嗝动画，完成后通知关卡，禁用／死亡会取消回调。</summary>
        /// <param name="completed">真实 Spine Complete 时调用，关卡仍需检查自身 generation 和暂停状态。</param>
        /// <returns>是否开始播放；无 Spine 的旧角色返回 false，关卡不等待它。</returns>
        internal bool PlayHiccup(Action completed)
        {
            if (!Application.isPlaying || !isActiveAndEnabled || _animationMachine == null || _dead || _spineState == Hiccup) return false;
            _hiccupCompleted = completed;
            _animationMachine.Enter(Hiccup);
            return true;
        }

        /// <summary>仅关卡暂停／胜利演出调用；控制 Spine 时间，不改变物理或成长／死亡 Tween。</summary>
        internal void SetSpinePaused(bool paused)
        {
            if (!Application.isPlaying || _animationMachine == null || _spinePaused == paused) return;
            _spinePaused = paused;
            spineCtrl.AnimationState.TimeScale = paused ? 0f : _spineTimeScale;
        }

        /// <summary>状态节点进入时设置轨道；只对一次性动作订阅完成，不为循环动作累计回调。</summary>
        private void EnterSpineAnimation(string animationName, bool loop)
        {
            _spineState = animationName;
            TrackEntry entry = spineCtrl.PlayAnimation(animationName, loop, false);
            if (loop) return;
            _oneShotTrack = entry;
            entry.Complete += OnOneShotComplete;
        }

        /// <summary>只接受本次轨道；eat 完成恢复基础状态，hiccup 保持末帧并只交付一次完成。</summary>
        private void OnOneShotComplete(TrackEntry entry)
        {
            if (entry != _oneShotTrack || _dead || !isActiveAndEnabled) return;
            DetachOneShot();
            if (_spineState == Eat) RefreshMovementAnimation();
            else if (_spineState == Hiccup)
            {
                Action completed = _hiccupCompleted;
                _hiccupCompleted = null;
                completed?.Invoke();
            }
        }

        /// <summary>切状态和销毁均解除当前 TrackEntry 订阅，避免 Spine 轨道池复用后收到旧回调。</summary>
        private void DetachOneShot()
        {
            if (_oneShotTrack != null) _oneShotTrack.Complete -= OnOneShotComplete;
            _oneShotTrack = null;
        }

        /// <summary>Bear FSM Dispose 不退出节点，因此显式解绑；死亡保留当前 Spine 姿势并沿原 Tween 离屏。</summary>
        private void DisposeAnimationState()
        {
            DetachOneShot();
            _hiccupCompleted = null;
            SetSpinePaused(true);
            _animationMachine?.Dispose();
            _animationMachine = null;
        }
        #endregion

        #region Bear FSM 节点
        private abstract class AnimationNode : StateNode
        {
            protected abstract string AnimationName { get; }
            protected virtual bool Loop => true;
            /// <summary>由 Bear FSM 进入节点时播放对应动作，不在每帧重置轨道。</summary>
            public override void OnEnter() => ((DigActorCtrl)_owner).EnterSpineAnimation(AnimationName, Loop);
            /// <summary>离开节点时移除一次性动作的完成订阅。</summary>
            public override void OnExit() => ((DigActorCtrl)_owner).DetachOneShot();
        }

        [Preserve, StateMachineNode(typeof(DigActorCtrl), Idle, true)]
        private sealed class IdleNode : AnimationNode { protected override string AnimationName => Idle; }
        [Preserve, StateMachineNode(typeof(DigActorCtrl), Scroll)]
        private sealed class ScrollNode : AnimationNode { protected override string AnimationName => Scroll; }
        [Preserve, StateMachineNode(typeof(DigActorCtrl), Nervous)]
        private sealed class NervousNode : AnimationNode { protected override string AnimationName => Nervous; }
        [Preserve, StateMachineNode(typeof(DigActorCtrl), Eat)]
        private sealed class EatNode : AnimationNode
        {
            protected override string AnimationName => Eat;
            protected override bool Loop => false;
        }
        [Preserve, StateMachineNode(typeof(DigActorCtrl), Hiccup)]
        private sealed class HiccupNode : AnimationNode
        {
            protected override string AnimationName => Hiccup;
            protected override bool Loop => false;
        }
        #endregion
    }
}
