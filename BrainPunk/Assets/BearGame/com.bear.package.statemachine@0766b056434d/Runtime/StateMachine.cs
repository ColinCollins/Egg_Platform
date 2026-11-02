using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Bear.Fsm
{
    public class StateMachine : IBearMachine, IDisposable
    {
        internal IBearMachineOwner _owner;
        public IBearMachineOwner Owner => _owner;
        
        protected IStateMachineBaseNode _currentNode;

        protected List<Type> _cache;
        protected Dictionary<string, Type> nodes;
        
        public StateMachine(IBearMachineOwner owner)
        {
            _owner = owner;
            _cache = new List<Type>();
            nodes = new Dictionary<string, Type>();
        }
        
        public void Apply(Type _type)
        {
            string state = "";
            _cache.ForEach(type =>
            {
                var attribute = type.GetCustomAttribute(typeof(StateMachineNode), true) as StateMachineNode;
                if (attribute.Owner.Equals(_type) && 
                    !nodes.ContainsKey(attribute.State_Name))
                {
                    nodes[attribute.State_Name] = type;
                    
                    if (attribute.IsDefault)
                        state = attribute.State_Name;
                }
            });
            
            if (!string.IsNullOrEmpty(state))
                Enter(state);
        }

        /// <summary>
        /// check state node
        /// </summary>
        public void Inject(params Type[] types)
        {
            foreach (Type obj in types)
            {
                if (!_cache.Contains(obj))
                    _cache.Add(obj);
            }
        }

        public virtual bool IsRunning(string state)
        {
            nodes.TryGetValue(state, out var value);
            if (_currentNode != null && value != null)
            {
                return value.Name.Equals(_currentNode.GetType().Name);
            }

            return false;
        }

        public virtual void Enter(string state)
        {
            if (IsRunning(state))
            {
                Execute();
                return;
            }
        
            nodes.TryGetValue(state, out Type node);
            if (node == null)
                return;

            var instance = Activator.CreateInstance(node) as IStateMachineBaseNode;
            (instance as StateNode).SetOwner(_owner);
            
            _currentNode?.OnExit();
            _currentNode = instance;
            _currentNode?.OnEnter();
        }

        public IStateMachineBaseNode GetCurrent()
        {
            return _currentNode;
        }

        public virtual void Update()
        {
            _currentNode?.OnUpdate();
        }

        public virtual void Execute()
        {
            _currentNode?.OnExecute();
        }

        public virtual void Dispose()
        {
            nodes?.Clear();
        }
    }
}