using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bear.Fsm;

[StateMachineNode(typeof(MainGame), GameState.PLAYINGGAME)]
public class MainGame_PlayingGame : StateNode
{
    public override void OnEnter()
    {
       Debug.Log($"{typeof(MainGame_PlayingGame).Name} Enter");

       var game = _owner as MainGame;
       
       Debug.Log($"{typeof(MainGame_PlayingGame).Name} | {game.name}");
    }

    public override void OnExecute()
    {
        Debug.Log($"{typeof(MainGame_PlayingGame).Name} Execute");
    }

    public override void OnUpdate()
    {
        Debug.Log($"{typeof(MainGame_PlayingGame).Name} Update");
    }

    public override void OnFixUpdate()
    {
        
    }

    public override void OnLateUpdate()
    {
        
    }

    public override void OnExit()
    {
        Debug.Log($"{typeof(MainGame_PlayingGame).Name} Exit");
    }
}
