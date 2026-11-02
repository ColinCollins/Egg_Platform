using Cysharp.Threading.Tasks;
using GF;
using UnityEngine;

namespace BPGame
{
    public class LaunchPageLogic: BaseLogic
    {

        private LaunchPageView _view;


        public LaunchPageLogic(LaunchPageView view)
        {
            _view = view;
        }






        public override void Initialize()
        {
            
        }

        public override void OnBeforeStart()
        {
        }

        public override void OnEnter()
        {
            InitView();
        }

        public override void OnRefresh()
        {
        }

        public override void OnExit()
        {
        }



        #region Event Control


        private void InitView()
        {
            _view.OnClickStart = OnClickStartEvent;
            _view.TitleText = "Brain Punk";
        }


        private void OnClickStartEvent()
        {
            Debug.Log($"Click Start Button !");
            App.UI.OpenAsync<MainPageView>().Forget();
        }



        #endregion
        
        
    }
}