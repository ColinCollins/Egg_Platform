using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bear.Fsm;

[StateMachineNode(typeof(MainGame), GameState.STARTGAME, true)]
public class MainGame_StartGame : StateNode
{
    public override void OnEnter()
    {
       Debug.Log($"{typeof(MainGame_StartGame).Name} Enter");
    }

    public override void OnExecute()
    {
        Debug.Log($"{typeof(MainGame_StartGame).Name} Execute");
    }

    public override void OnUpdate()
    {
        Debug.Log($"{typeof(MainGame_StartGame).Name} Update");
    }

    public override void OnExit()
    {
        Debug.Log($"{typeof(MainGame_StartGame).Name} Exit");
    }
}
