using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Bear.Fsm
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class StateMachineNode : Attribute
    {
        public Type Owner;
        public string State_Name;
        public bool IsDefault;

        public StateMachineNode(Type owner, string name, bool isDefault = false)
        {
            Owner = owner;
            State_Name = name;
            IsDefault = isDefault;
        }
    }
}

