using System.Collections;
using System.Collections.Generic;
using Bear.Fsm;
using UnityEngine;

public class MonoBaseNode : StateNode
{
    private Transform _target;
    
    public MonoBaseNode(Transform target)
    {
        _target = target;
    }
}
