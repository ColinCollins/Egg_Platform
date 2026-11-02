using System;
using System.Collections;
using System.Collections.Generic;
using Bear.Fsm;
using UnityEngine;

public class MainGame : MonoBehaviour, IBearMachineOwner
{
   private StateMachine _machine;

   void Awake()
   {
      _machine = new StateMachine(this);
      _machine.Inject(
         typeof(MainGame_StartGame), 
                  typeof(MainGame_PlayingGame));
      _machine.Apply(GetType());
      
      _machine.Enter(GameState.PLAYINGGAME);
   }

   private void Update()
   {
      _machine?.Update();
   }

   private void OnDestroy()
   {
      _machine?.Dispose();
   }
}
