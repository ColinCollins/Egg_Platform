using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bear.Fsm
{
    public interface IStateMachineBaseNode
    {
        public void OnEnter();
        public void OnExecute();
        public void OnUpdate();
        public void OnFixUpdate();
        public void OnLateUpdate();
        public void OnExit();
    }
}