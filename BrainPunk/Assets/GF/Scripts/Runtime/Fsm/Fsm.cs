using System;
using System.Collections.Generic;

namespace GF
{
    public class Fsm<T> where T : class
    {
        public T Owner { get; private set; }
        private FsmState<T> _currState;
        private Type _prevState = null;
        private Dictionary<Type, FsmState<T>> _stateDic;
        private Dictionary<string, object> _paramDic;

        public Fsm(T owner)
        {
            _stateDic = new Dictionary<Type, FsmState<T>>();
            _paramDic = new Dictionary<string, object>();
            Owner = owner;
        }

        /// <summary>
        /// 添加状态
        /// </summary>
        /// <param name="state"></param>
        private void AddState(FsmState<T> state)
        {
            if (state == null)
            {
                return;
            }

            state.CurrFsm = this;
            Type stateType = state.GetType();
            if (!_stateDic.ContainsKey(stateType))
            {
                state.CurrFsm = this;
                _stateDic.Add(stateType, state);
            }
        }

        /// <summary>
        /// 获取状态
        /// </summary>
        /// <typeparam name="T2"></typeparam>
        /// <returns></returns>
        public FsmState<T> GetState<T2>() where T2 : FsmState<T>
        {
            _stateDic.TryGetValue(typeof(T2), out var state);
            return state;
        }
        
        /// <summary>
        /// 获取上一个状态类型
        /// </summary>
        /// <returns></returns>
        public Type GetPrevStateType()
        {
            return _prevState;
        }

        public void Update()
        {
            _currState?.OnUpdate();
        }

        /// <summary>
        /// 切换状态
        /// </summary>
        /// <param name="args"></param>
        /// <typeparam name="T2"></typeparam>
        public void ChangeState<T2>(params object[] args) where T2 : FsmState<T>, new()
        {
            var stateType = typeof(T2);
            ChangeState(stateType, args);
        }
        
        /// <summary>
        /// 切换状态
        /// </summary>
        /// <param name="type"></param>
        /// <param name="args"></param>
        public void ChangeState(Type type, params object[] args)
        {
            var stateType = type;
            if (!_stateDic.ContainsKey(stateType))
            {
                AddState(Activator.CreateInstance(type) as FsmState<T>);
            }

            if(_currState != null)
            {
                _prevState = _currState.GetType();
            }

            if (_currState == null)
            {
                _currState = _stateDic[stateType];
                _currState.OnEnter(args);
                return;
            }

            if (_currState.GetType() == stateType)
            {
                return;
            }

            _currState.OnLeave();
            _currState = _stateDic[stateType];
            _currState.OnEnter(args);
        }
        
        /// <summary>
        /// 获取当前状态
        /// </summary>
        /// <returns></returns>
        public Type GetCurrState()
        {
            return _currState?.GetType();
        }

        /// <summary>
        /// 设置状态数据
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <typeparam name="T2"></typeparam>
        public void SetData<T2>(string key, T2 value)
        {
            _paramDic[key] = value;
        }

        /// <summary>
        /// 获取状态数据
        /// </summary>
        /// <param name="key"></param>
        /// <typeparam name="T2"></typeparam>
        /// <returns></returns>
        public T2 GetData<T2>(string key)
        {
            if (_paramDic.TryGetValue(key, out var value))
            {
                return (T2) value;
            }

            return default;
        }

        public void Destroy()
        {
            _currState?.OnLeave();
            _currState = null;
            foreach (var state in _stateDic)
            {
                state.Value?.OnDestroy();
            }

            _stateDic.Clear();
            _paramDic.Clear();
        }
    }
}