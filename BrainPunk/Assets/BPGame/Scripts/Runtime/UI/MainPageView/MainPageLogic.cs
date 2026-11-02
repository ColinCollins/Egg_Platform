using Cysharp.Threading.Tasks;
using GF;
using UnityEngine;

namespace BPGame
{
    public class MainPageLogic: BaseLogic
    {

        private MainPageView _view;


        public MainPageLogic(MainPageView view)
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
            _view.OnClickBack = OnClickStartEvent;
            _view.TitleText = "Game Scene";
        }


        private void OnClickStartEvent()
        {
            Debug.Log($"Click Back Button !");
            App.UI.OpenAsync<LaunchPageView>().Forget();
        }



        #endregion
        
        
    }
}