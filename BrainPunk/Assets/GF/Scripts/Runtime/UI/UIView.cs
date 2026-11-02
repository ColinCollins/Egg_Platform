using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GF
{
    public abstract class UIView : UIBase
    {
        public override void OnEnter()
        {
            base.OnEnter();
        }
        
        public override void AddEvent()
        {
            base.AddEvent();
        }

        public override void OnRefresh()
        {
            base.OnRefresh();
        }

        public override void OnExit()
        {
            base.OnExit();
        }
        
        public override void OnDestroy()
        {
            base.OnDestroy();
        }
    }
}