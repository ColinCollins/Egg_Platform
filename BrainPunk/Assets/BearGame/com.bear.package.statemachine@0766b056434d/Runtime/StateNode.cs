using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bear.Fsm
{
    public abstract class StateNode : IStateMachineBaseNode
    {
        protected IBearMachineOwner _owner;

        public void SetOwner(IBearMachineOwner owner)
        {
            _owner = owner;
        }

        public virtual void OnEnter()
        {
            
        }

        public virtual void OnExecute()
        {
        }

        public virtual void OnUpdate()
        {
        }

        public virtual void OnFixUpdate()
        {
        }

        public virtual void OnLateUpdate()
        {
        }

        public virtual void OnExit()
        {
        }
    }
}