using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GF
{
    public abstract class FsmState<T> where T : class
    {
        public Fsm<T> CurrFsm;
        public virtual void OnEnter(params object[] args) { }
        public virtual void OnUpdate() { }
        public virtual void OnLeave() { }
        public virtual void OnDestroy() { }
    }
}